using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGex
{
    /// <summary>La razon futuro/libro de un libro por razon (vela alineada + mediana de la rueda). Una por libro:
    /// la del indicador (Libro = CBOE_ETF) y una por cada capa extra (15-09).</summary>
    public sealed class RazonEtf
    {
        public readonly List<double> Obs = new();
        public double Rueda = double.NaN;
    }

    /// <summary>Una capa extra (15-09): un libro mas, calculado con el MISMO nucleo y dibujado con su color
    /// encima del grafico de NQ/MNQ. Solo baja su cadena, corre Calcular() y se dibuja; nada de la lectura
    /// primaria (centinela, gatillos, AUDIT, archivo, pelotitas) la lee. Ver conocimiento/traspasos/2026-09-15-capas-nq.md.
    ///
    /// Capas del mismo subyacente (QQQ, TQQQ, NDX, Rithmic NQ): el strike va al futuro por razon (y por
    /// apalancamiento en TQQQ). Capas de OTRO subyacente (SPX, SPY, ES): un muro de SPX no es un precio de NQ;
    /// se lleva por la distancia porcentual al spot, multiplicada por la beta NQ/S&P MEDIDA en la rueda
    /// (minuto a minuto, spot del libro alineado contra la vela de NQ): Fut = F x (1 + beta x (K/S - 1)),
    /// que es el mismo mapeo del apalancamiento con apalancamiento = 1/beta. Sin muestra, beta = 1 y se dice SUPUESTA.</summary>
    public sealed class CapaLibro
    {
        public enum TipoCapa { EtfPorRazon, IndiceConBase, RithmicViva, VivaLocal }

        public readonly string Nombre;        // "QQQ", "TQQQ", "NDX", "RITHMIC", "SPX", "SPY", "ES"
        public readonly string Ticker;        // lo que se baja: ultima-<Ticker>.json / "NQ" (radar -> NDX) / "ES" (radar -> SPX, o viva local)
        public readonly TipoCapa Tipo;
        public readonly bool PorBeta;         // otro subyacente: apalancamiento = 1 / beta medida
        public double Apalancamiento;         // 1 (QQQ), 3 (TQQQ), 1/beta (SPX, SPY, ES): solo cambia a que precio del futuro va cada strike
        public readonly Color Color;

        public Feed.Cadena C;                                   // la ultima cadena bajada
        public readonly GammaHoyNucleo Nucleo = new();          // su propio nucleo (fotos del Max Change propias, sin uso)
        public GammaHoyNucleo.Lectura L;                        // la ultima lectura (bajo el candado del indicador)
        public readonly RazonEtf Razon = new();
        public string Error = "";
        public DateTime UltimaBajada = DateTime.MinValue, UltimoCalculo = DateTime.MinValue, UltimoAudit = DateTime.MinValue;
        public Feed.Cadena CCalculada;                          // con que cadena se calculo L (referencia)

        // beta NQ/S&P: pares (ln spot del libro, ln NQ alineado) por cadena nueva; regresion de los retornos por minuto
        public readonly List<(double LnSpot, double LnNq, DateTime Ts)> Pares = new();
        public double Beta = double.NaN, BetaR2 = double.NaN;
        public int BetaN;
        public string BetaOrigen = "";

        // toques de hoy en las dominantes de esta capa (contados en el indicador, SIN placebo: el laboratorio juzga)
        public int Toques, Rebotes;
        public readonly List<Toque> Pendientes = new();
        public sealed class Toque { public double Nivel, Ent; public int Lado, Barra; }

        public CapaLibro(string nombre, string ticker, TipoCapa tipo, double apalancamiento, Color color, bool porBeta = false)
        { Nombre = nombre; Ticker = ticker; Tipo = tipo; Apalancamiento = apalancamiento; Color = color; PorBeta = porBeta; }

        /// <summary>Edad del dato, dicha ANTES del numero (regla 4 del protocolo): "vivo", "N min" (ya con los 902 s de CBOE),
        /// "hace N min" para el viva grabado por otro grafico, o "sin dato".</summary>
        public string Edad(CultureInfo es)
        {
            var c = C;
            if (c == null) return "sin dato";
            if (Tipo == TipoCapa.VivaLocal) return "hace " + Math.Max(0, (DateTime.UtcNow - c.GeneradoUtc).TotalMinutes).ToString("0", es) + " min";
            if (c.EsFuturo) return "vivo";
            return (c.EdadMin + 902.0 / 60.0).ToString("0", es) + " min";
        }

        /// <summary>Un par nuevo (spot del libro, NQ alineado) cuando la razon salio de la vela alineada.</summary>
        public void AnotarPar(Feed.Cadena c)
        {
            if (c == null || c.SpotIdx <= 0 || c.Escala <= 0 || c.EscalaOrigen != "vela alineada") return;
            var ts = c.GeneradoUtc == default(DateTime) ? DateTime.UtcNow : c.GeneradoUtc;
            lock (Pares)
            {
                if (Pares.Count > 0 && (ts - Pares[Pares.Count - 1].Ts).TotalSeconds < 30) return;
                Pares.Add((Math.Log(c.SpotIdx), Math.Log(c.Escala * c.SpotIdx), ts));
                if (Pares.Count > 180) Pares.RemoveAt(0);
            }
        }

        /// <summary>beta = pendiente de los retornos por minuto de NQ sobre los del libro (sin intercepto), acotada a [0,4, 3];
        /// hace falta 13+ pares seguidos (huecos de mas de 10 min se saltean). Manual > 0 manda. Sin muestra: 1, SUPUESTA.</summary>
        public void MedirBeta(double manual)
        {
            if (!PorBeta) { if (Apalancamiento <= 0) Apalancamiento = 1; return; }
            if (manual > 0) { Beta = manual; BetaOrigen = "manual"; BetaN = 0; BetaR2 = double.NaN; Apalancamiento = 1.0 / Beta; return; }
            double sxy = 0, sxx = 0, syy = 0; int n = 0;
            lock (Pares)
            {
                for (int i = 1; i < Pares.Count; i++)
                {
                    if ((Pares[i].Ts - Pares[i - 1].Ts).TotalMinutes > 10) continue;
                    double dx = Pares[i].LnSpot - Pares[i - 1].LnSpot, dy = Pares[i].LnNq - Pares[i - 1].LnNq;
                    sxy += dx * dy; sxx += dx * dx; syy += dy * dy; n++;
                }
            }
            if (n >= 13 && sxx > 0 && syy > 0 && (sxy * sxy) / (sxx * syy) >= 0.2)
            {
                // r2 < 0,2 = los retornos no se parecen (visto el 15-09 con la viva de ES: beta 0,4 y r2 0,00, basura):
                // ahi no hay beta medida, se sigue con 1 y se dice
                double b = sxy / sxx;
                Beta = Math.Max(0.4, Math.Min(3.0, b)); BetaN = n; BetaR2 = (sxy * sxy) / (sxx * syy);
                BetaOrigen = "medida" + (b != Beta ? " ACOTADA" : "");
            }
            else if (n >= 13 && sxx > 0 && syy > 0) { Beta = 1.0; BetaN = n; BetaR2 = (sxy * sxy) / (sxx * syy); BetaOrigen = "SUPUESTA (r2 " + BetaR2.ToString("0.00", CultureInfo.InvariantCulture) + " bajo, n " + n + ")"; }
            else { Beta = 1.0; BetaN = n; BetaR2 = double.NaN; BetaOrigen = "SUPUESTA (n " + n + " < 13)"; }
            Apalancamiento = 1.0 / Beta;
        }
    }

    public partial class GammaHoy
    {
        // ==================================================================
        // Capas extra (15-09): QQQ + TQQQ + NDX + Rithmic + SPX + SPY + ES a la vez, en NQ/MNQ
        // ==================================================================
        [Display(Name = "Capa QQQ (celeste)", GroupName = "5. Capas extra (NQ)", Order = 1,
                 Description = "Libro de QQQ 0DTE por volumen (CBOE, 902 s tarde) llevado a NQ por razon, como la referencia. Solo en graficos de NQ/MNQ. Apagada no cambia nada.")]
        public bool CapaQqq { get; set; } = false;

        [Display(Name = "Capa TQQQ (magenta)", GroupName = "5. Capas extra (NQ)", Order = 2,
                 Description = "Libro de TQQQ (3x) 0DTE por volumen. Cada strike va a NQ con el apalancamiento (un strike a +3 % de TQQQ es NQ a +1 %). Las MAGNITUDES no son comparables con QQQ ni NDX: cada capa se normaliza a su propio maximo.")]
        public bool CapaTqqq { get; set; } = false;

        [Display(Name = "Capa NDX (gris)", GroupName = "5. Capas extra (NQ)", Order = 3,
                 Description = "Libro de NDX (CBOE, el mismo que 'Libro en vivo = CBOE_SPX' en NQ) con la base de la rueda de la lectura primaria; si la primaria es un ETF, cae a la base medida o teorica y lo dice.")]
        public bool CapaNdx { get; set; } = false;

        [Display(Name = "Capa Rithmic (lima)", GroupName = "5. Capas extra (NQ)", Order = 4,
                 Description = "Las opciones de NQ desde tu ATAS (la cadena viva). Necesita 'Cadena viva de Rithmic' prendida; no abre una segunda suscripcion.")]
        public bool CapaRithmic { get; set; } = false;

        [Display(Name = "Capa SPX (violeta): otro subyacente, por beta", GroupName = "5. Capas extra (NQ)", Order = 5,
                 Description = "Libro de SPX 0DTE por volumen (CBOE, 902 s tarde) llevado a NQ por la distancia porcentual al spot x beta NQ/SPX medida en la rueda. Un muro de SPX NO es un precio de NQ: es 'donde estaria NQ si el S&P llega a su muro y NQ lo sigue con su beta'. Se dice la beta y si es medida o supuesta.")]
        public bool CapaSpx { get; set; } = false;

        [Display(Name = "Capa SPY (turquesa): otro subyacente, por beta", GroupName = "5. Capas extra (NQ)", Order = 6,
                 Description = "Libro de SPY 0DTE por volumen (CBOE), el que dibuja la referencia para ES, llevado a NQ por beta como SPX.")]
        public bool CapaSpy { get; set; } = false;

        [Display(Name = "Capa ES Rithmic (salmon): otro subyacente, por beta", GroupName = "5. Capas extra (NQ)", Order = 7,
                 Description = "Las opciones de ES por Rithmic que graba el grafico de MES cada minuto (viva-ES-<dia>.jsonl, 'Guardar la cadena viva'); sin segunda suscripcion. Strikes del futuro ES, Black-76, llevados a NQ por beta. Si el grafico de MES no esta abierto, dice hace cuanto es el dato.")]
        public bool CapaEs { get; set; } = false;

        [Display(Name = "Beta NQ vs S&P (0 = medir en la rueda)", GroupName = "5. Capas extra (NQ)", Order = 8,
                 Description = "Cuanto se mueve NQ por cada 1 % del S&P. 0 = se mide con los retornos por minuto de la rueda (13+ pares); un valor fijo manda sobre la medida y se rotula 'manual'.")]
        [Range(0, 3)]
        public decimal BetaManual { get; set; } = 0m;

        [Display(Name = "Capas: dibujar barras", GroupName = "5. Capas extra (NQ)", Order = 10)]
        public bool CapasBarras { get; set; } = true;

        [Display(Name = "Capas: dibujar dominantes", GroupName = "5. Capas extra (NQ)", Order = 11)]
        public bool CapasDominantes { get; set; } = true;

        [Display(Name = "Capas: dibujar el zero gamma de cada una", GroupName = "5. Capas extra (NQ)", Order = 12)]
        public bool CapasZero { get; set; } = false;

        [Display(Name = "Capas: contar toques y rebotes de hoy (sin placebo)", GroupName = "5. Capas extra (NQ)", Order = 13,
                 Description = "Por capa: cuantas veces el precio llego a una dominante desde lejos y cuantas rebato (misma regla que el laboratorio: banda, llegada de lejos, R a favor antes que R en contra en 20 min). Es un CONTEO del dia, sin placebo: el laboratorio (capas_respeto.py) es el que juzga.")]
        public bool CapasToques { get; set; } = true;

        private readonly CapaLibro[] _capas =
        {
            new CapaLibro("QQQ", "QQQ", CapaLibro.TipoCapa.EtfPorRazon, 1, Color.FromArgb(80, 180, 255)),
            new CapaLibro("TQQQ", "TQQQ", CapaLibro.TipoCapa.EtfPorRazon, 3, Color.FromArgb(255, 90, 200)),
            new CapaLibro("NDX", "NQ", CapaLibro.TipoCapa.IndiceConBase, 1, Color.FromArgb(200, 200, 210)),
            new CapaLibro("RITHMIC", "", CapaLibro.TipoCapa.RithmicViva, 1, Color.FromArgb(170, 255, 90)),
            new CapaLibro("SPX", "ES", CapaLibro.TipoCapa.EtfPorRazon, 1, Color.FromArgb(180, 120, 255), porBeta: true),
            new CapaLibro("SPY", "SPY", CapaLibro.TipoCapa.EtfPorRazon, 1, Color.FromArgb(0, 210, 190), porBeta: true),
            new CapaLibro("ES", "ES", CapaLibro.TipoCapa.VivaLocal, 1, Color.FromArgb(255, 150, 120), porBeta: true),
        };
        private DateTime _diaToques = DateTime.MinValue;

        /// <summary>Solo en NQ/MNQ (no inventar capas de ES) y solo si su llave esta prendida.</summary>
        private bool CapaActiva(CapaLibro k)
        {
            if (Fuente == FuenteDatos.Archivo) return false;
            string raiz; try { raiz = Raiz(); } catch { return false; }
            if (raiz != "NQ") return false;
            switch (k.Nombre)
            {
                case "QQQ": return CapaQqq;
                case "TQQQ": return CapaTqqq;
                case "NDX": return CapaNdx;
                case "RITHMIC": return CapaRithmic;
                case "SPX": return CapaSpx;
                case "SPY": return CapaSpy;
                case "ES": return CapaEs;
            }
            return false;
        }

        /// <summary>La capa Rithmic se refresca desde el temporizador (cada SegundosLibroRithmic), reusando la viva.
        /// Si la primaria ya es Rithmic, comparte su cadena.</summary>
        private void RefrescarCapaRithmic(DateTime ahora)
        {
            var k = _capas.First(z => z.Tipo == CapaLibro.TipoCapa.RithmicViva);
            if (!CapaActiva(k)) return;
            if (Libro == LibroEnVivo.Rithmic_ES) { var c0 = _c; if (c0 != null && c0.EsFuturo) { k.C = c0; k.Error = ""; k.UltimaBajada = ahora; } return; }
            if (!_viva.Activa) { k.Error = UsarCadenaViva ? "viva: " + _viva.Estado : "prender 'Cadena viva de Rithmic'"; return; }
            if ((ahora - k.UltimaBajada).TotalSeconds < Math.Max(5, SegundosLibroRithmic)) return;
            k.UltimaBajada = ahora;
            try { var cv = DesdeViva(); if (cv != null) { k.C = cv; k.Error = ""; } }
            catch (Exception e) { k.Error = e.Message; Registrar(e); }
        }

        /// <summary>Un libro de otro subyacente (SPX, SPY, ES) o de un ETF: razon por vela alineada, par para la beta,
        /// beta medida o manual, y el apalancamiento resultante en la cadena.</summary>
        private void PrepararCapa(CapaLibro k, Feed.Cadena c, int retrasoSeg, string fuente, bool esFuturo)
        {
            EscalarCon(c, k.Nombre, k.Razon, retrasoSeg);
            c.EsFuturo = esFuturo;
            c.Fuente = fuente;
            if (k.PorBeta) { k.AnotarPar(c); k.MedirBeta((double)BetaManual); }
            c.Apalancamiento = k.Apalancamiento;
        }

        /// <summary>Baja las cadenas de las capas de CBOE y del viva local, en serie y cada una en su try: una que falle
        /// no tumba a las otras ni a la primaria. Se llama al final de BajarFeed (misma cadencia que el feed).</summary>
        private async Task BajarCapas()
        {
            foreach (var k in _capas)
            {
                if (!CapaActiva(k) || k.Tipo == CapaLibro.TipoCapa.RithmicViva) continue;
                try
                {
                    Feed.Cadena c = null;
                    Action<string> err = m => k.Error = (m ?? "").Contains("404") ? "sin archivo en la nube todavia (404)" : m;
                    if (k.Tipo == CapaLibro.TipoCapa.EtfPorRazon)
                    {
                        if (k.Nombre == "SPX")
                        {
                            // la cadena de SPX: el feed de la nube (radar, cada 5 min) y la de la rama cadenas (cada minuto), la mas nueva
                            c = await Feed.Bajar(Url, "ES", err).ConfigureAwait(false);
                            if (FeedMinuto)
                            {
                                var u = await Feed.BajarUltima(UrlArchivo, "ES", null).ConfigureAwait(false);
                                if (u != null && (c == null || u.GeneradoUtc > c.GeneradoUtc)) c = u;
                            }
                        }
                        else c = await Feed.BajarUltima(UrlArchivo, k.Ticker, err).ConfigureAwait(false);
                        if (c != null)
                            PrepararCapa(k, c, Math.Max(0, RetrasoCboeSeg), "CBOE " + k.Nombre + (k.Apalancamiento != 1 && !k.PorBeta ? " x" + k.Apalancamiento.ToString("0", CultureInfo.InvariantCulture) : ""), false);
                    }
                    else if (k.Tipo == CapaLibro.TipoCapa.VivaLocal)
                    {
                        c = Feed.Archivo.UltimaViva(k.Ticker);
                        if (c == null) { k.Error = "sin viva-" + k.Ticker + " local: el grafico de MES la graba ('Guardar la cadena viva')"; }
                        else PrepararCapa(k, c, 0, "Rithmic " + k.Ticker + " (grabado)", true);
                    }
                    else // NDX: si la primaria ya es la cadena de NDX (CBOE_SPX en NQ), es la misma
                    {
                        var c0 = _c;
                        if (Libro == LibroEnVivo.CBOE_SPX && c0 != null && !c0.EsFuturo && !c0.PorRazon) c = c0;
                        else
                        {
                            c = await Feed.Bajar(Url, k.Ticker, err).ConfigureAwait(false);
                            if (FeedMinuto)
                            {
                                var u = await Feed.BajarUltima(UrlArchivo, k.Ticker, null).ConfigureAwait(false);
                                if (u != null && (c == null || u.GeneradoUtc > c.GeneradoUtc)) c = u;
                            }
                        }
                    }
                    if (c != null) { k.C = c; k.Error = ""; k.UltimaBajada = DateTime.UtcNow; }
                }
                catch (Exception e) { k.Error = e.Message; Registrar(e); }
            }
        }

        /// <summary>Corre Calcular() de cada capa con los MISMOS ajustes que la primaria (copiados por reflexion,
        /// incluida la base de la rueda y el vencimiento del futuro). Solo cuando cambio su cadena o cada 5 s:
        /// cada Calcular son ~211 strikes x 61 pasos y ya se midio (10-09) que repreciar por tick funde un nucleo.
        /// Se llama al final de RepreciarCon. AUDIT por capa cada 60 s, con reloj propio.</summary>
        private void RepreciarCapas(double futuro, DateTime ahoraUtc)
        {
            foreach (var k in _capas)
            {
                if (!CapaActiva(k)) { if (k.L != null) lock (_candado) k.L = null; continue; }
                var c = k.C;
                if (c == null) continue;
                if (ReferenceEquals(c, k.CCalculada) && (ahoraUtc - k.UltimoCalculo).TotalSeconds < 5) continue;
                var a = k.Nucleo.A; var de = _nucleo.A;
                var t = typeof(GammaHoyNucleo.Ajustes);
                foreach (var fi in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) fi.SetValue(a, fi.GetValue(de));
                foreach (var pi in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) if (pi.CanRead && pi.CanWrite) pi.SetValue(a, pi.GetValue(de));
                GammaHoyNucleo.Lectura L = null;
                try { L = k.Nucleo.Calcular(c, futuro, ahoraUtc); }
                catch (Exception e) { k.Error = e.Message; Registrar(e); }
                lock (_candado) { k.L = L; }
                k.UltimoCalculo = ahoraUtc; k.CCalculada = c;
                if (L != null && !L.SinBase && (ahoraUtc - k.UltimoAudit).TotalSeconds >= 60)
                {
                    k.UltimoAudit = ahoraUtc;
                    var inv = CultureInfo.InvariantCulture;
                    Log("AUDIT capa=" + k.Nombre + " " + GammaHoyNucleo.Audit(L, c, k.Tipo == CapaLibro.TipoCapa.RithmicViva).Substring(6)
                        + (k.PorBeta ? " beta=" + k.Beta.ToString("0.###", inv) + " betaN=" + k.BetaN + " betaR2=" + (double.IsNaN(k.BetaR2) ? "NaN" : k.BetaR2.ToString("0.00", inv)) + " betaOrigen=" + k.BetaOrigen.Replace(' ', '_') : "")
                        + " toques=" + k.Toques + " rebotes=" + k.Rebotes);
                }
            }
        }

        /// <summary>Los niveles de cada capa activa, para el centinela: <capa>_dom0, <capa>_dom1, <capa>_zero_vol,
        /// <capa>_mp_vol, <capa>_mn_vol (en minuscula). Se llama bajo _candado desde Anotar, una vez por vela cerrada.
        /// Con esto el laboratorio (capas_respeto.py) juzga cada fuente contra su placebo con la misma regla de siempre.</summary>
        private void NivelesCapas(List<KeyValuePair<string, double>> niv)
        {
            foreach (var k in _capas)
            {
                if (!CapaActiva(k)) continue;
                var L = k.L;
                if (L == null || L.SinBase) continue;
                string pre = k.Nombre.ToLowerInvariant() + "_";
                foreach (var kv in GammaHoyNucleo.Niveles(L))
                    if (kv.Key.StartsWith("dom") || kv.Key == "zero_vol" || kv.Key == "mp_vol" || kv.Key == "mn_vol")
                        niv.Add(new KeyValuePair<string, double>(pre + kv.Key, kv.Value));
            }
        }

        /// <summary>El conteo de hoy, por capa, con la regla del laboratorio (rebote_niveles.py): toque = la vela cerrada
        /// entra en la banda (+-TOL) de una dominante y la anterior no, viniendo de lejos (el cierre de hace ATRAS velas a
        /// mas de LEJOS); rebote = desde el cierre del toque, R a favor de la llegada antes que R en contra, dentro del
        /// horizonte. Ni placebo ni veredicto: es el conteo crudo, para ver EN VIVO que fuente esta trabajando.</summary>
        private void ContarToquesCapas(int cerrada, IndicatorCandle vela)
        {
            if (!CapasToques || vela == null || cerrada < 4) return;
            bool nq = Raiz() == "NQ";
            double tol = nq ? 8 : 2, lejos = nq ? 30 : 7, reb = nq ? 20 : 5;
            int atras = 3;
            double minPorVela = 1;
            try { var d = (vela.Time - GetCandle(cerrada - 1).Time).TotalMinutes; if (d > 0 && d < 240) minPorVela = d; } catch { }
            int horiz = Math.Max(1, (int)Math.Round(20.0 / minPorVela));
            var dia = vela.Time.Date;
            if (dia != _diaToques) { _diaToques = dia; foreach (var k in _capas) { k.Toques = 0; k.Rebotes = 0; k.Pendientes.Clear(); } }
            double h = (double)vela.High, l = (double)vela.Low, cl = (double)vela.Close;
            double hp, lp, c0;
            try { var p = GetCandle(cerrada - 1); hp = (double)p.High; lp = (double)p.Low; c0 = (double)GetCandle(cerrada - atras).Close; } catch { return; }
            foreach (var k in _capas)
            {
                if (!CapaActiva(k)) continue;
                // primero se resuelven los toques pendientes con esta vela
                for (int i = k.Pendientes.Count - 1; i >= 0; i--)
                {
                    var t = k.Pendientes[i];
                    if (cerrada <= t.Barra) continue;
                    bool gano = t.Lado > 0 ? h >= t.Ent + reb : l <= t.Ent - reb;
                    bool perdio = t.Lado > 0 ? l <= t.Ent - reb : h >= t.Ent + reb;
                    if (gano && perdio) { k.Pendientes.RemoveAt(i); continue; }          // ambiguo: no cuenta
                    if (gano) { k.Toques++; k.Rebotes++; k.Pendientes.RemoveAt(i); continue; }
                    if (perdio) { k.Toques++; k.Pendientes.RemoveAt(i); continue; }
                    if (cerrada - t.Barra >= horiz) k.Pendientes.RemoveAt(i);            // se vencio sin definirse
                }
                GammaHoyNucleo.Lectura L; lock (_candado) L = k.L;
                if (L == null || L.SinBase) continue;
                foreach (var d in L.Doms)
                {
                    double N = d.Fut;
                    bool toca = l <= N + tol && h >= N - tol;
                    bool tocaba = lp <= N + tol && hp >= N - tol;
                    if (!toca || tocaba) continue;
                    if (Math.Abs(c0 - N) < lejos) continue;                                 // no venia de lejos
                    if (k.Pendientes.Any(t => Math.Abs(t.Nivel - N) <= tol)) continue;       // ya hay un toque abierto en ese nivel
                    k.Pendientes.Add(new CapaLibro.Toque { Nivel = N, Ent = cl, Lado = c0 > N ? 1 : -1, Barra = cerrada });
                }
            }
        }

        /// <summary>Dibujo de las capas: una columna de barras por capa a la derecha de la columna primaria (cada
        /// una normalizada a SU maximo; las negativas llevan un borde rojo), las dominantes como raya discontinua
        /// del color de la capa con rotulo "D1 QQQ 29.150" en su propia columna a la derecha, y una leyenda abajo
        /// (a la derecha de las barras, para no pisar el cuadro Account de ATAS) con la edad de cada dato, la beta
        /// si es de otro subyacente y el conteo de toques de hoy. La primaria se dibuja despues (encima).</summary>
        private void PintarCapas(RenderContext g, IChartContainer cont, Rectangle area, int piso, int x0, int ancho, int alto,
                                 int xl0, int xl1, int altoRot, RenderFont fRot, CultureInfo es,
                                 Action<double, Color, float, System.Drawing.Drawing2D.DashStyle, int> raya)
        {
            var activas = _capas.Where(CapaActiva).ToList();
            if (activas.Count == 0) return;
            int anchoCapa = Math.Max(12, (int)(ancho * 0.45));
            int xLey = Math.Max(x0 + ancho + 8, x0 + 235);   // a la derecha del cuadro Account de ATAS (visto el 15-09: lo pisaba)
            for (int i = 0; i < activas.Count; i++)
            {
                var k = activas[i];
                GammaHoyNucleo.Lectura L; lock (_candado) L = k.L;
                int xk = x0 + ancho + 4 + i * (anchoCapa + 4);
                var col = k.Color;

                // leyenda, una linea por capa (el numero en su propia linea, nunca al lado de un control)
                string doms = L == null || L.Doms.Count == 0 ? "" : " · " + string.Join(" ", L.Doms.Select((d, j) => "D" + (j + 1) + " " + d.Fut.ToString("N0", es)));
                string zero = L == null || double.IsNaN(L.ZeroVol) ? "" : " · 0Γ " + L.ZeroVol.ToString("N0", es);
                string estado = L == null ? (k.C == null ? "sin dato" : "calculando") : k.Edad(es) + (L.SinBase ? " SIN BASE" : "");
                string beta = !k.PorBeta ? "" : " · β " + (double.IsNaN(k.Beta) ? "?" : k.Beta.ToString("0.00", es)) + " " + (k.BetaOrigen.StartsWith("medida") ? "(n " + k.BetaN + (double.IsNaN(k.BetaR2) ? "" : ", r² " + k.BetaR2.ToString("0.00", es)) + ")" : k.BetaOrigen);
                string toques = !CapasToques ? "" : " · toques " + k.Toques + " rebota " + k.Rebotes + (k.Pendientes.Count > 0 ? " (+" + k.Pendientes.Count + " abierto)" : "");
                string ley = "■ " + k.Nombre + " " + estado + beta + doms + zero + toques + (string.IsNullOrEmpty(k.Error) ? "" : " · " + k.Error);
                int yl = piso - 4 - altoRot * (activas.Count - i);
                var ml = g.MeasureString(ley, fRot);
                g.FillRectangle(Color.FromArgb(160, ColFondo), new Rectangle(xLey - 2, yl, ml.Width + 4, altoRot));
                g.DrawString(ley, fRot, Color.FromArgb(230, col), xLey, yl);
                if (L == null || L.SinBase || L.Perfil.Count == 0) continue;

                if (CapasBarras)
                {
                    bool porOi = L.MaxAbsVol <= 0;                 // de noche no hay volumen: OI, y la columna lo dice
                    double maxK = porOi ? L.MaxAbsOi : L.MaxAbsVol;
                    if (maxK > 0)
                    {
                        foreach (var s in L.Perfil)
                        {
                            double v = porOi ? s.GexOi : s.GexVol;
                            if (v == 0) continue;
                            bool fijo = L.Doms.Any(d => d.Fut == s.Fut);
                            if (UmbralBarraPct > 0 && Math.Abs(v) < maxK * UmbralBarraPct / 100.0 && !fijo) continue;
                            int y; try { y = cont.GetYByPrice((decimal)s.Fut, false); } catch { continue; }
                            if (y < area.Top || y > piso) continue;
                            double fr = Math.Sqrt(Math.Abs(v) / maxK);
                            int w = Math.Max(1, (int)(fr * anchoCapa));
                            g.FillRectangle(Color.FromArgb((int)(80 + 120 * fr), col), new Rectangle(xk, y - alto / 2, w, alto));
                            if (v < 0) g.DrawLine(new RenderPen(Color.FromArgb(220, ColNeg), 1f), xk, y + alto / 2, xk + w, y + alto / 2);
                        }
                    }
                    g.DrawString(k.Nombre + (porOi ? " OI" : ""), fRot, Color.FromArgb(220, col), xk, area.Top + 8 + altoRot + 2);
                }

                if (CapasDominantes)
                {
                    for (int d = 0; d < L.Doms.Count; d++)
                    {
                        double p = L.Doms[d].Fut;
                        raya(p, col, d == 0 ? 1.4f : 1.0f, System.Drawing.Drawing2D.DashStyle.Dash, d == 0 ? 190 : 140);
                        int y; try { y = cont.GetYByPrice((decimal)p, false); } catch { continue; }
                        if (y < area.Top || y + altoRot > piso) continue;
                        string t = "D" + (d + 1) + " " + k.Nombre + " " + p.ToString("N0", es);
                        var m = g.MeasureString(t, fRot);
                        int xt = xl1 - m.Width - 2 - i * (m.Width + 8);   // cada capa en su propia columna, de derecha a izquierda
                        if (xt < xl0) xt = xl0;
                        g.FillRectangle(Color.FromArgb(150, ColFondo), new Rectangle(xt - 1, y + 1, m.Width + 2, altoRot));
                        g.DrawString(t, fRot, Color.FromArgb(230, col), xt, y + 1);
                    }
                    if (CapasZero) raya(L.ZeroVol, col, 1f, System.Drawing.Drawing2D.DashStyle.Dot, 120);
                }
            }
        }
    }
}
