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

    /// <summary>La pizarra compartida del DLL (15-09): cada grafico con Gamma Hoy anota su ultimo precio por minuto
    /// bajo su raiz (ES, NQ, RTY). Con eso el grafico de NQ mide la beta NQ/ES con las velas de los DOS mercados a la
    /// misma hora, sin depender del spot del libro (que llega cada 5 min por el cache de la nube y salio con r2 0).
    /// Vive mientras ATAS esta abierto; con un solo grafico no hay beta y se dice.</summary>
    public static class VelasCompartidas
    {
        private static readonly object _llave = new();
        private static readonly Dictionary<string, SortedDictionary<long, double>> _series = new();

        public static void Anotar(string raiz, DateTime utc, double precio)
        {
            if (string.IsNullOrEmpty(raiz) || precio <= 0) return;
            long min = utc.Ticks / TimeSpan.TicksPerMinute;
            lock (_llave)
            {
                if (!_series.TryGetValue(raiz, out var s)) { s = new SortedDictionary<long, double>(); _series[raiz] = s; }
                s[min] = precio;
                if (s.Count > 720) s.Remove(s.Keys.First());
            }
        }

        public static SortedDictionary<long, double> Serie(string raiz)
        {
            lock (_llave) return _series.TryGetValue(raiz, out var s) ? new SortedDictionary<long, double>(s) : new SortedDictionary<long, double>();
        }
    }

    /// <summary>Una capa extra (15-09): un libro mas, calculado con el MISMO nucleo y dibujado con su color
    /// encima del grafico de NQ/MNQ. Solo baja su cadena, corre Calcular() y se dibuja; nada de la lectura
    /// primaria (centinela, gatillos, AUDIT, archivo, pelotitas) la lee. Ver conocimiento/traspasos/2026-09-15-capas-nq.md.
    ///
    /// Capas del mismo subyacente (QQQ, TQQQ, NDX, Rithmic NQ): el strike va al futuro por razon (y por
    /// apalancamiento en TQQQ). Capas de OTRO subyacente (SPX, SPY, ES): un muro de SPX no es un precio de NQ;
    /// se lleva por la distancia porcentual al spot, multiplicada por la beta NQ/S&P MEDIDA con las velas de NQ
    /// y de ES: Fut = F x (1 + beta x (K/S - 1)), que es el mismo mapeo del apalancamiento con
    /// apalancamiento = 1/beta. Sin muestra, beta = 1 y se dice SUPUESTA.</summary>
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

        // la beta con la que se dibuja (la mide el indicador con las velas compartidas; manual manda)
        public double Beta = 1.0, BetaR2 = double.NaN;
        public int BetaN;
        public string BetaOrigen = "SUPUESTA (sin velas)";

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
                 Description = "Las opciones de NQ desde tu ATAS (la cadena viva). Necesita 'Cadena viva de Rithmic' prendida; no abre una segunda suscripcion. OJO: su volumen arranca en cero con cada reinicio de ATAS.")]
        public bool CapaRithmic { get; set; } = false;

        [Display(Name = "Capa SPX (violeta): otro subyacente, por beta", GroupName = "5. Capas extra (NQ)", Order = 5,
                 Description = "Libro de SPX 0DTE por volumen (CBOE, 902 s tarde) llevado a NQ por la distancia porcentual al spot x beta NQ/ES medida con las velas de los dos graficos. Un muro de SPX NO es un precio de NQ: es 'donde estaria NQ si el S&P llega a su muro y NQ lo sigue con su beta'. Se dice la beta y si es medida o supuesta.")]
        public bool CapaSpx { get; set; } = false;

        [Display(Name = "Capa SPY (turquesa): otro subyacente, por beta", GroupName = "5. Capas extra (NQ)", Order = 6,
                 Description = "Libro de SPY 0DTE por volumen (CBOE), el que dibuja la referencia para ES, llevado a NQ por beta como SPX.")]
        public bool CapaSpy { get; set; } = false;

        [Display(Name = "Capa ES Rithmic (salmon): otro subyacente, por beta", GroupName = "5. Capas extra (NQ)", Order = 7,
                 Description = "Las opciones de ES por Rithmic que graba el grafico de MES cada minuto (viva-ES-<dia>.jsonl, 'Guardar la cadena viva'); sin segunda suscripcion. Strikes del futuro ES, Black-76, llevados a NQ por beta. Si el grafico de MES no esta abierto, dice hace cuanto es el dato.")]
        public bool CapaEs { get; set; } = false;

        [Display(Name = "Beta NQ vs S&P (0 = medir con las velas de NQ y MES)", GroupName = "5. Capas extra (NQ)", Order = 8,
                 Description = "Cuanto se mueve NQ por cada 1 % del S&P. 0 = se mide con los retornos por minuto de las velas de este grafico y del grafico de MES (los dos tienen que estar abiertos), ultimos 90 minutos, 20+ pares y r2 >= 0,2; si no, 1 y se rotula SUPUESTA. Un valor fijo manda y se rotula 'manual'.")]
        [Range(0, 3)]
        public decimal BetaManual { get; set; } = 0m;

        [Display(Name = "Capas: dibujar barras (izquierda: gamma x volumen)", GroupName = "5. Capas extra (NQ)", Order = 10)]
        public bool CapasBarras { get; set; } = true;

        [Display(Name = "Capas: barras solo si pesan mas del % del maximo de su fuente", GroupName = "5. Capas extra (NQ)", Order = 10,
                 Description = "Las barras chicas no se dibujan (las dominantes siempre). 25 = un perfil ralo, solo lo que pesa. 0 = todas.")]
        [Range(0, 90)]
        public int CapasUmbralPct { get; set; } = 25;

        [Display(Name = "Capas: perfil derecho (convexidad), apagado por defecto", GroupName = "5. Capas extra (NQ)", Order = 11,
                 Description = "En 0DTE la convexidad de cada strike es casi menos su gamma: el perfil derecho es un espejo del izquierdo y solo ensucia. Prenderlo cuando haya vencimientos mas largos en el horizonte.")]
        public bool CapasConvexidadVisible { get; set; } = false;

        [Display(Name = "Capas: fusionar niveles que coinciden (tolerancia, % del precio)", GroupName = "5. Capas extra (NQ)", Order = 12,
                 Description = "Si dos fuentes tienen un nivel a menos de esta distancia (0,03 % = unos 9 pts de NQ), se dibuja UNA raya gruesa con los colores de las dos alternados y un solo rotulo (SPX·SPY D1). 0 = no fusionar.")]
        [Range(0, 0.2)]
        public decimal CapasFusionPct { get; set; } = 0.03m;

        [Display(Name = "Capas: dibujar dominantes", GroupName = "5. Capas extra (NQ)", Order = 12)]
        public bool CapasDominantes { get; set; } = true;

        [Display(Name = "Capas: majors (+Γ / −Γ), apagados por defecto", GroupName = "5. Capas extra (NQ)", Order = 13,
                 Description = "La barra positiva mas grande y la negativa mas grande de cada capa, punteadas y tenues, en su color, solo dentro del radio de abajo y solo si no son ya una dominante. Suman rayas: prender solo si hacen falta.")]
        public bool CapasMajorsVisibles { get; set; } = false;

        [Display(Name = "Capas: majors solo a menos de (% del precio)", GroupName = "5. Capas extra (NQ)", Order = 14)]
        [Range(0.1, 5)]
        public decimal CapasMajorsRadioPct { get; set; } = 1.0m;

        [Display(Name = "Capas: dibujar el zero gamma de cada una", GroupName = "5. Capas extra (NQ)", Order = 15)]
        public bool CapasZero { get; set; } = false;

        [Display(Name = "Capas: precios de las capas tambien en la escalera del eje", GroupName = "5. Capas extra (NQ)", Order = 15,
                 Description = "Apagado: la guia es el color, la barra y la abreviatura, y el precio se lee del eje. Prendido: ademas cada nivel va como caja de color en la escalera pegada al eje.")]
        public bool CapasPreciosEnEscalera { get; set; } = false;

        [Display(Name = "Capas: rayas y bandas de la primaria al (%)", GroupName = "5. Capas extra (NQ)", Order = 16,
                 Description = "Con alguna capa prendida, las rayas y la banda de dominancia del libro primario (amarillo) se dibujan a este porcentaje de su intensidad, para que las capas se lean. 100 = como siempre.")]
        [Range(0, 100)]
        public int CapasAtenuarPrimariaPct { get; set; } = 40;

        [Display(Name = "Capas: ancho de sus columnas (% del ancho de barras)", GroupName = "5. Capas extra (NQ)", Order = 17)]
        [Range(15, 100)]
        public int CapasAnchoPct { get; set; } = 35;

        [Display(Name = "Capas: contar toques y rebotes de hoy (sin placebo)", GroupName = "5. Capas extra (NQ)", Order = 18,
                 Description = "Por capa: cuantas veces el precio llego a una dominante desde lejos y cuantas reboto (misma regla que el laboratorio: banda, llegada de lejos, R a favor antes que R en contra en 20 min). Es un CONTEO del dia, sin placebo: el laboratorio (capas_respeto.py) es el que juzga.")]
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
        private bool _pintandoCapas;

        // la beta NQ/ES medida con las velas compartidas (una para todas las capas del S&P)
        private double _betaSp = 1.0, _betaR2 = double.NaN;
        private int _betaN;
        private string _betaOrigen = "SUPUESTA (sin velas)";
        private DateTime _ultimaBeta = DateTime.MinValue;

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

        /// <summary>Con capas activas y atenuacion < 100, la primaria es un fantasma: sus barras no llevan rotulo.</summary>
        private bool PrimariaSilenciada()
        {
            if (CapasAtenuarPrimariaPct >= 100) return false;
            foreach (var k in _capas) if (CapaActiva(k)) return true;
            return false;
        }

        /// <summary>Con capas activas, las rayas y bandas de la primaria bajan al porcentaje elegido; las de las capas no.</summary>
        private int AtenuarPrimaria(int alfa)
        {
            if (_pintandoCapas || CapasAtenuarPrimariaPct >= 100) return alfa;
            bool hay = false; foreach (var k in _capas) if (CapaActiva(k)) { hay = true; break; }
            return hay ? Math.Max(0, alfa * CapasAtenuarPrimariaPct / 100) : alfa;
        }

        /// <summary>beta = pendiente de los retornos por minuto de NQ (este grafico) sobre los de ES (el grafico de MES),
        /// minutos comunes de los ultimos 90, sin intercepto; 20+ pares y r2 >= 0,2, acotada a [0,4; 3]. Manual manda.
        /// Se recalcula una vez por minuto.</summary>
        private void MedirBetaVelas(DateTime ahoraUtc)
        {
            if ((ahoraUtc - _ultimaBeta).TotalSeconds < 60) return;
            _ultimaBeta = ahoraUtc;
            double manual = (double)BetaManual;
            if (manual > 0) { _betaSp = manual; _betaN = 0; _betaR2 = double.NaN; _betaOrigen = "manual"; return; }
            var nq = VelasCompartidas.Serie(Raiz());
            var es = VelasCompartidas.Serie("ES");
            long desde = ahoraUtc.Ticks / TimeSpan.TicksPerMinute - 90;
            var comunes = nq.Keys.Where(m => m >= desde && es.ContainsKey(m)).OrderBy(m => m).ToList();
            double sxy = 0, sxx = 0, syy = 0; int n = 0;
            for (int i = 1; i < comunes.Count; i++)
            {
                if (comunes[i] - comunes[i - 1] > 3) continue;
                double dx = Math.Log(es[comunes[i]] / es[comunes[i - 1]]), dy = Math.Log(nq[comunes[i]] / nq[comunes[i - 1]]);
                if (dx == 0 && dy == 0) continue;
                sxy += dx * dy; sxx += dx * dx; syy += dy * dy; n++;
            }
            _betaN = n;
            if (n >= 20 && sxx > 0 && syy > 0)
            {
                double r2 = (sxy * sxy) / (sxx * syy), b = sxy / sxx;
                _betaR2 = r2;
                if (r2 >= 0.2) { _betaSp = Math.Max(0.4, Math.Min(3.0, b)); _betaOrigen = "velas" + (b != _betaSp ? " ACOTADA" : ""); }
                else { _betaSp = 1.0; _betaOrigen = "SUPUESTA (r2 " + r2.ToString("0.00", CultureInfo.InvariantCulture) + " bajo)"; }
            }
            else { _betaSp = 1.0; _betaR2 = double.NaN; _betaOrigen = es.Count == 0 ? "SUPUESTA (sin velas de MES: abrir su grafico)" : "SUPUESTA (n " + n + " < 20)"; }
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

        /// <summary>Un libro de otro subyacente (SPX, SPY, ES) o de un ETF: razon por vela alineada y el apalancamiento
        /// (3 en TQQQ, 1/beta en el S&P) en la cadena.</summary>
        private void PrepararCapa(CapaLibro k, Feed.Cadena c, int retrasoSeg, string fuente, bool esFuturo)
        {
            EscalarCon(c, k.Nombre, k.Razon, retrasoSeg);
            c.EsFuturo = esFuturo;
            c.Fuente = fuente;
            if (k.PorBeta) AplicarBeta(k);
            c.Apalancamiento = k.Apalancamiento;
        }

        private void AplicarBeta(CapaLibro k)
        {
            k.Beta = _betaSp; k.BetaN = _betaN; k.BetaR2 = _betaR2; k.BetaOrigen = _betaOrigen;
            k.Apalancamiento = 1.0 / Math.Max(0.1, _betaSp);
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
            try { MedirBetaVelas(ahoraUtc); } catch (Exception e) { Registrar(e); }
            foreach (var k in _capas)
            {
                if (!CapaActiva(k)) { if (k.L != null) lock (_candado) k.L = null; continue; }
                var c = k.C;
                if (c == null) continue;
                bool betaCambio = k.PorBeta && k.Beta != _betaSp;
                if (betaCambio) { AplicarBeta(k); c.Apalancamiento = k.Apalancamiento; }
                else if (k.PorBeta) { k.BetaN = _betaN; k.BetaR2 = _betaR2; k.BetaOrigen = _betaOrigen; }   // la leyenda dice por que sigue SUPUESTA (n, r2, sin velas)
                if (!betaCambio && ReferenceEquals(c, k.CCalculada) && (ahoraUtc - k.UltimoCalculo).TotalSeconds < 5) continue;
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
                        + (k.PorBeta ? " beta=" + k.Beta.ToString("0.###", inv) + " betaN=" + k.BetaN + " betaR2=" + (double.IsNaN(k.BetaR2) ? "NaN" : k.BetaR2.ToString("0.00", inv)) + " betaOrigen=" + k.BetaOrigen.Replace(' ', '_') + " velasNQ=" + VelasCompartidas.Serie(Raiz()).Count + " velasES=" + VelasCompartidas.Serie("ES").Count : "")
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

        /// <summary>Los niveles de las capas con su texto corto y su color: D1/D2 (y zero si se pide) de cada capa, y los
        /// majors si estan cerca del precio y no son ya una dominante. Con `raya` dibuja las rayas; con null solo lista.</summary>
        private List<(double Precio, string Texto, Color Col, int Peso, CapaLibro K)> EtiquetasCapas(
            List<CapaLibro> activas, Dictionary<CapaLibro, GammaHoyNucleo.Lectura> lecturas, double futuro,
            Action<double, Color, float, System.Drawing.Drawing2D.DashStyle, int> raya)
        {
            var etiquetas = new List<(double Precio, string Texto, Color Col, int Peso, CapaLibro K)>();
            double radioMajors = double.IsNaN(futuro) ? double.MaxValue : futuro * (double)CapasMajorsRadioPct / 100.0;
            foreach (var k in activas)
            {
                var L = lecturas[k]; if (L == null || L.SinBase || L.Perfil.Count == 0) continue;
                var col = k.Color;
                if (CapasDominantes)
                {
                    for (int d = 0; d < L.Doms.Count; d++)
                    {
                        double p = L.Doms[d].Fut;
                        raya?.Invoke(p, col, d == 0 ? 1.6f : 1.1f, System.Drawing.Drawing2D.DashStyle.Dash, d == 0 ? 210 : 150);
                        etiquetas.Add((p, k.Nombre + " D" + (d + 1), col, d == 0 ? 3 : 2, k));
                    }
                    if (CapasZero && !double.IsNaN(L.ZeroVol))
                    {
                        raya?.Invoke(L.ZeroVol, col, 1f, System.Drawing.Drawing2D.DashStyle.Dot, 120);
                        etiquetas.Add((L.ZeroVol, k.Nombre + " 0Γ", col, 1, k));
                    }
                }
                if (CapasMajorsVisibles)
                {
                    bool porOi = L.MaxAbsVol <= 0;
                    double mp = porOi ? L.MpOi : L.MpVol, mn = porOi ? L.MnOi : L.MnVol;
                    bool mpEsDom = L.Doms.Any(d => d.Fut == mp), mnEsDom = L.Doms.Any(d => d.Fut == mn);
                    if (!double.IsNaN(mp) && !mpEsDom && Math.Abs(mp - futuro) <= radioMajors)
                    { raya?.Invoke(mp, col, 1f, System.Drawing.Drawing2D.DashStyle.Dot, 110); etiquetas.Add((mp, k.Nombre + " +Γ", col, 1, k)); }
                    if (!double.IsNaN(mn) && !mnEsDom && Math.Abs(mn - futuro) <= radioMajors)
                    { raya?.Invoke(mn, col, 1f, System.Drawing.Drawing2D.DashStyle.Dot, 110); etiquetas.Add((mn, k.Nombre + " −Γ", col, 1, k)); }
                }
            }
            return etiquetas;
        }

        /// <summary>Los renglones de las capas para la escalera primaria (nombre corto, precio, color). Vacio sin capas.</summary>
        private List<(string N, double P, Color C)> FilasCapas()
        {
            var salida = new List<(string N, double P, Color C)>();
            if (!CapasPreciosEnEscalera) return salida;
            var activas = _capas.Where(CapaActiva).ToList();
            if (activas.Count == 0) return salida;
            var lecturas = new Dictionary<CapaLibro, GammaHoyNucleo.Lectura>();
            double futuro;
            lock (_candado) { futuro = _futuro; foreach (var k in activas) lecturas[k] = k.L; }
            foreach (var e in EtiquetasCapas(activas, lecturas, futuro, null)) salida.Add((e.Texto, e.Precio, e.Col));
            return salida;
        }

        /// <summary>Dibujo de las capas, disposicion C "superpuestas" afinada (15-09, 17:00): solo las barras que pesan
        /// (umbral por fuente), todas desde el borde izquierdo, transparentes y la mas larga atras; la primaria de
        /// fantasma. Sin perfil derecho ni columna de precios por defecto: la guia es el COLOR de la fuente, la barra y
        /// la abreviatura ("SPX D1") en la punta de la barra. Si dos fuentes tienen un nivel en el mismo lugar, se
        /// FUSIONAN: una sola raya gruesa con los colores alternados y un solo rotulo ("SPX·SPY D1", con un cuadrado
        /// por fuente). Leyenda abajo a la izquierda.</summary>
        private void PintarCapas(RenderContext g, IChartContainer cont, Rectangle area, int piso, int x0, int ancho, int alto,
                                 int xl0, int xl1, int xConv, int altoRot, RenderFont fRot, CultureInfo es,
                                 Action<double, Color, float, System.Drawing.Drawing2D.DashStyle, int> raya)
        {
            var activas = _capas.Where(CapaActiva).ToList();
            if (activas.Count == 0) return;
            _pintandoCapas = true;
            try
            {
                int xLey = Math.Max(x0 + ancho + 8, x0 + 235);   // a la derecha del cuadro Account de ATAS
                double futuro; lock (_candado) futuro = _futuro;
                var lecturas = new Dictionary<CapaLibro, GammaHoyNucleo.Lectura>();
                foreach (var k in activas) { GammaHoyNucleo.Lectura L; lock (_candado) L = k.L; lecturas[k] = L; }

                // 1) la leyenda, una linea por capa
                for (int i = 0; i < activas.Count; i++)
                {
                    var k = activas[i]; var L = lecturas[k]; var col = k.Color;
                    string doms = L == null || L.Doms.Count == 0 ? "" : " · " + string.Join(" ", L.Doms.Select((d, j) => "D" + (j + 1) + " " + d.Fut.ToString("N0", es)));
                    string estado = L == null ? (k.C == null ? "sin dato" : "calculando") : "dato de hace " + k.Edad(es).Replace("hace ", "") + (L.SinBase ? " SIN BASE" : "");
                    if (L != null && k.C != null && k.C.EsFuturo && k.Tipo != CapaLibro.TipoCapa.VivaLocal) estado = "en vivo";
                    string beta = !k.PorBeta ? "" : " · β " + k.Beta.ToString("0.00", es) + " " + (k.BetaOrigen.StartsWith("velas") ? "medida (n " + k.BetaN + (double.IsNaN(k.BetaR2) ? "" : ", r² " + k.BetaR2.ToString("0.00", es)) + ")" : k.BetaOrigen.Replace("SUPUESTA", "supuesta"));
                    string toques = !CapasToques ? "" : " · rebotó " + k.Rebotes + " de " + k.Toques + " toques" + (k.Pendientes.Count > 0 ? " (+" + k.Pendientes.Count + " abierto)" : "");
                    string ley = "■ " + k.Nombre + " · " + estado + beta + doms + toques + (string.IsNullOrEmpty(k.Error) ? "" : " · " + k.Error);
                    int yl = piso - 4 - altoRot * (activas.Count - i);
                    var ml = g.MeasureString(ley, fRot);
                    g.FillRectangle(Color.FromArgb(170, ColFondo), new Rectangle(xLey - 2, yl, ml.Width + 4, altoRot));
                    g.DrawString(ley, fRot, Color.FromArgb(235, col), xLey, yl);
                }

                // 2) barras que pesan, superpuestas desde el borde izquierdo, de la mas larga a la mas corta
                var anchoBarra = new Dictionary<(CapaLibro, double), int>();
                if (CapasBarras)
                {
                    var barras = new List<(int W, int Y, Color Col, bool Neg)>();
                    foreach (var k in activas)
                    {
                        var L = lecturas[k]; if (L == null || L.SinBase || L.Perfil.Count == 0) continue;
                        bool porOi = L.MaxAbsVol <= 0; double maxK = porOi ? L.MaxAbsOi : L.MaxAbsVol; if (maxK <= 0) continue;
                        double umbral = Math.Max(UmbralBarraPct, CapasUmbralPct) / 100.0;
                        foreach (var s in L.Perfil)
                        {
                            double v = porOi ? s.GexOi : s.GexVol; if (v == 0) continue;
                            bool fijo = L.Doms.Any(d => d.Fut == s.Fut);
                            if (Math.Abs(v) < maxK * umbral && !fijo) continue;
                            int y; try { y = cont.GetYByPrice((decimal)s.Fut, false); } catch { continue; }
                            if (y < area.Top || y > piso) continue;
                            int w = Math.Max(3, (int)(Math.Sqrt(Math.Abs(v) / maxK) * ancho));
                            anchoBarra[(k, s.Fut)] = w;
                            barras.Add((w, y, k.Color, v < 0));
                        }
                    }
                    foreach (var b in barras.OrderByDescending(b => b.W))
                    {
                        g.FillRectangle(Color.FromArgb(120, b.Col), new Rectangle(x0, b.Y - alto / 2, b.W, alto));
                        g.DrawLine(new RenderPen(Color.FromArgb(b.Neg ? 220 : 170, b.Neg ? ColNeg : b.Col), 1f), x0, b.Y + alto / 2, x0 + b.W, b.Y + alto / 2);
                    }
                    int xt = x0 + 2;
                    foreach (var k in activas) { g.DrawString(k.Nombre, fRot, Color.FromArgb(220, k.Color), xt, area.Top + 8 + altoRot + 2); xt += g.MeasureString(k.Nombre + " ", fRot).Width; }
                }

                // 3) perfil derecho solo si se pide (en 0DTE es un espejo)
                if (VerConvexidad && CapasConvexidadVisible)
                {
                    int anchoDer = Math.Max(20, (int)(ancho * 0.7));
                    var barras = new List<(int W, int Y, Color Col, bool Neg)>();
                    foreach (var k in activas)
                    {
                        var L = lecturas[k]; if (L == null || L.SinBase || L.Perfil.Count == 0 || L.MaxAbsConv <= 0) continue;
                        double umbral = Math.Max(UmbralBarraPct, CapasUmbralPct) / 100.0;
                        foreach (var s in L.Perfil)
                        {
                            if (s.Conv == 0) continue;
                            double fr2 = Math.Abs(s.Conv) / L.MaxAbsConv;
                            if (fr2 < umbral) continue;
                            int y; try { y = cont.GetYByPrice((decimal)s.Fut, false); } catch { continue; }
                            if (y < area.Top || y > piso) continue;
                            barras.Add((Math.Max(3, (int)(Math.Sqrt(fr2) * anchoDer)), y, k.Color, s.Conv < 0));
                        }
                    }
                    foreach (var b in barras.OrderByDescending(b => b.W))
                    {
                        g.FillRectangle(Color.FromArgb(120, b.Col), new Rectangle(xConv - b.W, b.Y - alto / 2, b.W, alto));
                        g.DrawLine(new RenderPen(Color.FromArgb(b.Neg ? 220 : 170, b.Neg ? ColNeg : b.Col), 1f), xConv - b.W, b.Y + alto / 2, xConv, b.Y + alto / 2);
                    }
                }

                // 4) niveles: se listan sin dibujar, se agrupan los que coinciden, y recien ahi se dibujan las rayas
                var etiquetas = EtiquetasCapas(activas, lecturas, futuro, null);
                if (Rayas == EstiloRayas.Ninguna || etiquetas.Count == 0) return;
                double tolFusion = double.IsNaN(futuro) ? 0 : futuro * (double)CapasFusionPct / 100.0;
                var grupos = new List<List<(double Precio, string Texto, Color Col, int Peso, CapaLibro K)>>();
                foreach (var e in etiquetas.OrderByDescending(r => r.Precio))
                {
                    var ult = grupos.Count > 0 ? grupos[grupos.Count - 1] : null;
                    if (ult != null && tolFusion > 0 && ult[0].Precio - e.Precio <= tolFusion && !ult.Any(z => z.K == e.K)) ult.Add(e);
                    else grupos.Add(new List<(double, string, Color, int, CapaLibro)> { e });
                }
                int xRaya0 = xl0, xRaya1 = xl1;
                if (Rayas == EstiloRayas.Tenues) { /* las capas no se atenuan: son lo que se quiere ver */ }
                foreach (var gr in grupos)
                {
                    if (gr.Count == 1)
                    {
                        var e = gr[0];
                        bool dom = e.Texto.Contains(" D");
                        raya(e.Precio, e.Col, dom ? (e.Peso >= 3 ? 1.7f : 1.2f) : 1f, dom ? System.Drawing.Drawing2D.DashStyle.Dash : System.Drawing.Drawing2D.DashStyle.Dot, dom ? (e.Peso >= 3 ? 220 : 160) : 120);
                        continue;
                    }
                    // fusion: una raya gruesa, colores alternados por tramo, al precio medio del grupo
                    double pm = gr.Average(z => z.Precio);
                    int y; try { y = cont.GetYByPrice((decimal)pm, false); } catch { continue; }
                    if (y < area.Top || y > piso) continue;
                    int tramo = 9, n = gr.Count, j = 0;
                    for (int x = xRaya0; x < xRaya1; x += tramo, j++)
                        g.DrawLine(new RenderPen(Color.FromArgb(235, gr[j % n].Col), 2.6f), x, y, Math.Min(xRaya1, x + tramo - 2), y);
                }

                // 5) los rotulos: en la punta de la barra (o del borde) de ese nivel, ordenados y sin pisarse;
                //    fusionados: un cuadrado por fuente y un solo texto ("SPX·SPY D1")
                int ultimoFondo = area.Top;
                foreach (var gr in grupos)
                {
                    double pm = gr.Count == 1 ? gr[0].Precio : gr.Average(z => z.Precio);
                    int y; try { y = cont.GetYByPrice((decimal)pm, false); } catch { continue; }
                    if (y < area.Top - altoRot || y > piso + altoRot) continue;
                    int top = y - altoRot / 2;
                    if (top < ultimoFondo + 1) top = ultimoFondo + 1;
                    if (top + altoRot > piso) break;
                    int w = gr.Max(z => anchoBarra.TryGetValue((z.K, z.Precio), out var wb) ? wb : 0);
                    string nombres = string.Join("·", gr.Select(z => z.K.Nombre));
                    string tipo = gr[0].Texto.Substring(gr[0].Texto.IndexOf(' ') + 1);
                    string texto = nombres + " " + tipo;
                    var m = g.MeasureString(texto, fRot);
                    int cuad = altoRot - 4, xq = x0 + w + 4;
                    g.FillRectangle(Color.FromArgb(190, ColFondo), new Rectangle(xq - 1, top, gr.Count * (cuad + 2) + m.Width + 6, altoRot));
                    foreach (var z in gr) { g.FillRectangle(Color.FromArgb(240, z.Col), new Rectangle(xq, top + 2, cuad, cuad)); xq += cuad + 2; }
                    g.DrawString(texto, fRot, Color.FromArgb(245, gr.Count == 1 ? gr[0].Col : ColTexto), xq + 2, top);
                    if (Math.Abs(top + altoRot / 2 - y) > 2 && y >= area.Top && y <= piso)
                        g.DrawLine(new RenderPen(Color.FromArgb(180, gr[0].Col), 1f), x0 + w, y, x0 + w + 4, y);
                    ultimoFondo = top + altoRot;
                }
            }
            finally { _pintandoCapas = false; }
        }
    }
}
