using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGex
{
    /// <summary>
    /// TABLERO AHORA (1.8, 18-09-2026). Cuatro medidores en la franja derecha del panel (286 x 123 px, anclado arriba, antes del eje;
    /// si no entra se OCULTA, nunca cae sobre velas viejas) y un resumen en palabras de FLUJO, no de posicion:
    ///   FLUJO 60s   quien manda en este minuto: r60 = delta / volumen de los ultimos 60 s (cubetas por segundo de OnNewTrade). Aguja.
    ///   TENDENCIA   de donde viene: promedio de flujo de 5 min, CVD de 15 min y precio de 15 min, cada uno normalizado a +-1. Aguja.
    ///   GAMMA       tres estados, sin aguja y sin verde ni rojo: frenan (azul) / sin lectura (gris) / empujan (ambar). Sale de la estela
    ///               de Gamma Hoy (capa NQ): distancia precio - zero y signo de la gamma neta. Guardia de libro flaco: zero a mas de 150 pts
    ///               o linea de mas de 150 s = sin lectura. NO vota. En MES no hay capa: "solo NQ/MNQ".
    ///   VELOCIDAD   cinco escalones por el rango crudo de los ultimos 5 min (MNQ: <16, 16-24, 24-32, 32-48, >=48 pts) con la tabla medida
    ///               de segundos hasta +-8 y % abierto a 3 min (techo_ml.md, techo_ml_08). Es lo unico validado como anticipacion (del
    ///               TAMAÑO, nunca del lado). NO vota. En MES los cortes van /4 y se rotula "MNQ/4 sin medir".
    ///   RESUMEN     S = 2*v60 + v300 + vG + vCvd + vPx, cada v en {-1, 0, +1} con zona muerta. |S| <= 1 PAREJO, 2-3 compran/venden,
    ///               >= 4 COMPRAN FUERTE / VENDEN FUERTE, con "a favor n/5". Describe quien empuja AHORA: la vela siguiente sale del lado
    ///               del flujo el 48-50 % (FlujoClaro.md 1.5, escalas.md A_align 48,8 %). Por eso las palabras son de flujo y la cabecera
    ///               fija dice "AHORA  estado, no pronostico". Cada cambio de estado se graba en flujo/tablero-<inst>-<dia>.jsonl para
    ///               juzgarlo contra placebo antes de llamarlo algo.
    /// Todo se calcula en TableroLatido (una vez por segundo, fuera de OnRender); OnRender solo copia el ultimo cuadro y lo dibuja.
    /// </summary>
    public partial class FlujoClaro
    {
        [Display(Name = "Tablero AHORA (cuatro medidores)", GroupName = "5. Bloque AHORA", Order = 20,
                 Description = "Franja derecha del panel, 286 x 123 px, arriba (antes del eje de precios). FLUJO 60s y TENDENCIA con aguja; GAMMA (frenan / sin lectura / empujan) y VELOCIDAD (DORMIDO a DESATADO) por escalones. El resumen dice quien empuja ahora (compran / venden / parejo), no para donde va el precio. Si no hay lugar a la derecha de la vela en curso, se oculta solo.")]
        public bool FcTablero { get; set; } = true;

        private const int TabAncho = 286, TabAlto = 123;              // completo: cuatro medidores en fila
        private const int TabAnchoMin = 150, TabAnchoDos = 200, TabAltoDos = 145, TabAltoDosMax = 180;   // con menos lugar a la derecha de la vela: dos filas de dos (18-09, grafico de 5 min con ~190 px libres)
        private static readonly Color TabFondo = Color.FromArgb(255, 26, 33, 46), TabBorde = Color.FromArgb(255, 52, 63, 82), TabGris = Color.FromArgb(139, 148, 158);
        private static readonly Color TabAzul = Color.FromArgb(74, 144, 217), TabCeleste = Color.FromArgb(120, 180, 220), TabNaranja = Color.FromArgb(224, 138, 60), TabRojo = Color.FromArgb(214, 60, 140);   // DESATADO: magenta, nunca el rojo de venden

        // VELOCIDAD: tabla por rango crudo de 5 min en MNQ (laboratorio/gatillo/resultados/techo_ml.md, techo_ml_08_lectura_rango.py, 20 ruedas,
        // explorar / confirmar): segundos hasta resolver +-8: 97-125 / 75-86 / 52 / 31-29 / 13-14; sin resolver a los 180 s: 24-36 / 17-26 / 11-9 / 3 / 1 %.
        // Se usa el promedio de los dos tramos. Son medianas: la mitad de las veces tarda mas.
        private static readonly double[] TabCortesNq = { 16, 24, 32, 48 };
        private static readonly string[] TabVelocNombre = { "DORMIDO", "tranquilo", "normal", "movido", "DESATADO" };
        private static readonly int[] TabVelocSeg = { 110, 80, 52, 30, 14 }, TabVelocAbierto = { 30, 21, 10, 3, 1 };
        // zonas muertas y escalas de los cinco votos (supuestos de diseño, escritos aca para que el juez los use iguales)
        // TabMuerta60/300 = medianas de |r60| y |r300| en rueda (laboratorio/gatillo/escalas_umbrales.json, 12 ruedas): cada voto se prende en su mitad mas fuerte
        private const double TabTope60 = 0.30, TabTope300 = 0.15, TabMuerta60 = 0.083, TabMuerta300 = 0.045, TabMuertaGr = 0.30, TabMuertaZ = 0.5;
        private const double TabUmbralGr = 10;   // grandes del tablero: ordenes agresoras de 10+ (base.py d10_49 + d50); la vela sigue con FcUmbralGrande
        private readonly List<double> _tabGAnillo = new List<double>();   // |grandes de 5 min| por segundo de la ultima hora: su p95 es la escala del voto
        private const double TabGuardiaNq = 150, TabGuardiaEs = 40; private const int TabGuardiaSeg = 150;

        // cubetas propias del tablero, por segundo (la de _cubetas queda como esta: la sonda la lee con (S, V))
        private readonly Dictionary<long, (double Hi, double Lo)> _tabPx = new Dictionary<long, (double Hi, double Lo)>();
        private readonly Dictionary<long, double> _tabG = new Dictionary<long, double>();
        private long _tabPxDesde = long.MaxValue;

        /// <summary>El ultimo cuadro calculado. Se reemplaza entero (OnRender solo lo lee).</summary>
        private sealed class Cuadro
        {
            public DateTime Utc; public bool Nq;
            public double R60, R300, G300, ZCvd, ZPx, Tend, Precio, Rango5; public bool Z15Ok, RangoVivo;
            public int V60, V300, VG, VCvd, VPx, S, Favor, Compran, Venden;
            public string Estado = "";
            public int Veloc; public string VelocTxt = "", VelocCtx = "";
            public int Gamma; public string GammaTxt = "", GammaCtx = ""; public double Zero, DZero, Neto; public int EdadSeg; public string Libro = "";
        }
        private Cuadro _tab;
        private string _tabEstadoPrevio;
        private DateTime _tabEstelaLeida = DateTime.MinValue;
        private (bool Ok, DateTime T, double Zero, double Neto, string Libro, double Fut) _tabEstela;

        private bool TableroEsNq() => (InstrumentInfo?.Instrument ?? "").ToUpperInvariant().Contains("NQ");

        // ------------------------------------------------------------------ entradas por operacion
        /// <summary>Con cada operacion (desde OnNewTrade, fuera de la llave de _cubetas): maximo y minimo del segundo, para el rango vivo de 5 min.</summary>
        private void TableroOperacion(double precio, long seg)
        {
            if (!FcTablero || precio <= 0) return;
            lock (_tabPx)
            {
                if (_tabPx.TryGetValue(seg, out var q)) _tabPx[seg] = (Math.Max(q.Hi, precio), Math.Min(q.Lo, precio)); else _tabPx[seg] = (precio, precio);
                if (seg < _tabPxDesde) _tabPxDesde = seg;
                if (_tabPx.Count > 420) foreach (var viejo in _tabPx.Keys.Where(x => x < seg - 330).ToList()) _tabPx.Remove(viejo);
            }
        }

        /// <summary>Con cada operacion grande contada (desde ContarGrande, con _llave tomada): compras menos ventas por segundo.</summary>
        private void TableroGrande(long seg, double firmado)
        {
            if (!FcTablero || firmado == 0) return;
            lock (_tabG)
            {
                _tabG.TryGetValue(seg, out var q); _tabG[seg] = q + firmado;
                if (_tabG.Count > 420) foreach (var viejo in _tabG.Keys.Where(x => x < seg - 330).ToList()) _tabG.Remove(viejo);
            }
        }

        /// <summary>El "ahora" de las cubetas: el ultimo segundo con operacion mas lo que corrio el reloj desde entonces (misma regla que Ventana).</summary>
        private long TableroSegAhora()
        {
            lock (_cubetas) { if (_ultSeg == 0) return 0; return _ultSeg + (long)Math.Max(0, (DateTime.UtcNow - _relojUltTrade).TotalSeconds); }
        }

        private double TableroGrandes(int seg)
        {
            long ahora = TableroSegAhora(); if (ahora == 0) return 0;
            double s = 0; lock (_tabG) foreach (var kv in _tabG) if (kv.Key > ahora - seg && kv.Key <= ahora) s += kv.Value;
            return s;
        }

        /// <summary>Rango (max - min) de las operaciones de los ultimos 'seg' segundos. Cubre = las cubetas ya tienen esa historia (si no, se usan las velas).</summary>
        private (double Rango, bool Cubre) TableroRangoVivo(int seg)
        {
            long ahora = TableroSegAhora(); if (ahora == 0) return (0, false);
            lock (_tabPx)
            {
                if (_tabPxDesde == long.MaxValue || ahora - _tabPxDesde < seg) return (0, false);
                double hi = double.MinValue, lo = double.MaxValue;
                foreach (var kv in _tabPx) if (kv.Key > ahora - seg && kv.Key <= ahora) { if (kv.Value.Hi > hi) hi = kv.Value.Hi; if (kv.Value.Lo < lo) lo = kv.Value.Lo; }
                return hi >= lo ? (hi - lo, true) : (0, true);   // sin operaciones en la ventana: rango 0 (dormido de verdad)
            }
        }

        // ------------------------------------------------------------------ calculo (una vez por segundo, desde Latido)
        private void TableroLatido()
        {
            if (!FcTablero) { _tab = null; return; }
            try
            {
                var ahora = DateTime.UtcNow; bool nq = TableroEsNq();
                if (nq && (ahora - _tabEstelaLeida).TotalSeconds >= 5) { _tabEstelaLeida = ahora; TableroLeerEstela(ahora); }
                // lecturas por operacion (fuera de _llave: cada una toma su propia llave chica)
                var (s60, v60) = Ventana(60); var (s300, v300) = Ventana(300);
                double r60 = v60 > 0 ? s60 / v60 : 0, r300 = v300 > 0 ? s300 / v300 : 0;
                double g300 = TableroGrandes(300);
                var (rngVivo, cubre) = TableroRangoVivo(300);
                // lecturas por vela (bajo _llave, copia rapida)
                double precio, dPx = 0, dCvd = 0, sdPx = 0, sdCvd = 0, rngVelas = 0, bmax = 0, dur = 60; int muestras = 0; bool velasVivas;
                lock (_llave)
                {
                    int ult = CurrentBar - 1;
                    if (!_cargado || ult < 2 || _d.Count <= ult) return;
                    precio = _c[ult];
                    // una pestana oculta no recibe OnCalculate: sus velas se congelan mientras las cubetas siguen vivas (memoria traspaso-2026-09-15)
                    velasVivas = (ahora - _ultimoProvisorio).TotalSeconds < 180;
                    DateTime tNow = _tf[ult] > _t[ult] ? _tf[ult] : _t[ult];
                    double d1 = (_t[ult] - _t[ult - 1]).TotalSeconds, d2 = (_t[ult - 1] - _t[ult - 2]).TotalSeconds;
                    dur = Math.Min(d1, d2); if (dur <= 0 || dur > 3600) dur = 60;
                    int m15 = Math.Max(1, (int)Math.Round(900 / dur));
                    int b15 = BarraDeAprox(tNow.AddSeconds(-900)); if (b15 < 0 || b15 > ult - 1) b15 = Math.Max(0, ult - m15);
                    dPx = precio - _c[b15]; dCvd = _cvdC[ult] - _cvdC[b15];
                    // lo normal del cambio de 15 min: raiz del promedio de los cuadrados de los cambios de 15 min de las ultimas 2 horas (causal, sin la vela viva)
                    double ssPx = 0, ssCvd = 0;
                    for (int k = ult - 1; k >= m15 && (tNow - _t[k]).TotalSeconds <= 7200; k--)
                    {
                        double a = _c[k] - _c[k - m15], b = _cvdC[k] - _cvdC[k - m15]; ssPx += a * a; ssCvd += b * b; muestras++;
                    }
                    if (muestras >= 10) { sdPx = Math.Sqrt(ssPx / muestras); sdCvd = Math.Sqrt(ssCvd / muestras); }
                    // rango de 5 min por velas (respaldo hasta que las cubetas tengan 5 min de historia): velas que operaron dentro de la ventana
                    double hi = double.MinValue, lo = double.MaxValue;
                    for (int k = ult; k >= 0 && (_tf[k] == DateTime.MinValue || (tNow - _tf[k]).TotalSeconds <= 300); k--) { if (_h[k] > hi) hi = _h[k]; if (_l[k] < lo) lo = _l[k]; if (ult - k > 400) break; }
                    if (hi > lo) rngVelas = hi - lo;
                    // p95 de |grandes| por vela de las ultimas 300 velas con grandes (la misma vara que la cinta y el voto de la vela)
                    var bg = new List<double>(); for (int b = Math.Max(0, ult - 299); b <= ult; b++) if (_big[b] != 0) bg.Add(Math.Abs(_big[b])); bg.Sort();
                    bmax = bg.Count >= 5 ? bg[(int)Math.Min(bg.Count - 1, bg.Count * 0.95)] : (bg.Count > 0 ? bg[bg.Count - 1] : 0);
                }
                var c = new Cuadro { Utc = ahora, Nq = nq, Precio = precio, R60 = r60, R300 = r300, G300 = g300 };
                // ---- los cinco votos (-1 / 0 / +1 con zona muerta)
                c.V60 = v60 > 0 && Math.Abs(r60) >= TabMuerta60 ? Math.Sign(r60) : 0;
                c.V300 = v300 > 0 && Math.Abs(r300) >= TabMuerta300 ? Math.Sign(r300) : 0;
                double escalaG = 0;
                lock (_tabGAnillo)
                {
                    _tabGAnillo.Add(Math.Abs(g300)); if (_tabGAnillo.Count > 3600) _tabGAnillo.RemoveAt(0);
                    if (_tabGAnillo.Count >= 120) { var o = _tabGAnillo.OrderBy(x => x).ToList(); escalaG = o[(int)Math.Min(o.Count - 1, o.Count * 0.95)]; }
                }
                c.VG = escalaG > 0 && Math.Abs(g300) >= TabMuertaGr * escalaG ? Math.Sign(g300) : 0;   // los primeros 2 min no vota: sin vara
                c.Z15Ok = sdPx > 0 && sdCvd > 0;
                c.ZCvd = sdCvd > 0 ? Math.Max(-3, Math.Min(3, dCvd / sdCvd)) : 0; c.ZPx = sdPx > 0 ? Math.Max(-3, Math.Min(3, dPx / sdPx)) : 0;
                c.VCvd = Math.Abs(c.ZCvd) >= TabMuertaZ ? Math.Sign(c.ZCvd) : 0; c.VPx = Math.Abs(c.ZPx) >= TabMuertaZ ? Math.Sign(c.ZPx) : 0;
                if (!velasVivas) { c.VCvd = 0; c.VPx = 0; c.Z15Ok = false; }
                c.S = 2 * c.V60 + c.V300 + c.VG + c.VCvd + c.VPx;
                int[] votos = { c.V60, c.V300, c.VG, c.VCvd, c.VPx };
                c.Compran = votos.Count(v => v > 0); c.Venden = votos.Count(v => v < 0); c.Favor = c.S > 0 ? c.Compran : c.S < 0 ? c.Venden : 0;
                c.Estado = !velasVivas ? "sin velas (grafico oculto)" : Math.Abs(c.S) <= 1 ? "PAREJO" : c.S >= 4 ? "COMPRAN FUERTE" : c.S <= -4 ? "VENDEN FUERTE" : c.S > 0 ? "compran" : "venden";
                // ---- TENDENCIA: promedio de las tres lecturas de fondo, cada una topeada a +-1
                double Tope(double x) => Math.Max(-1, Math.Min(1, x));
                c.Tend = (Tope(r300 / TabTope300) + Tope(c.ZCvd / 2) + Tope(c.ZPx / 2)) / 3;
                // ---- VELOCIDAD: rango crudo de 5 min (cinta si ya cubre 5 min, si no las velas) contra los cortes de MNQ (en MES /4, sin medir)
                c.RangoVivo = cubre; c.Rango5 = cubre ? rngVivo : rngVelas;
                double[] cortes = nq ? TabCortesNq : TabCortesNq.Select(x => x / 4).ToArray();
                c.Veloc = cortes.Count(x => c.Rango5 >= x);
                int apuesta = nq ? 8 : 2;
                c.VelocTxt = TabVelocNombre[c.Veloc] + " " + c.Rango5.ToString("0", Es);
                string apuestaTxt = "±" + apuesta + " tarda ~" + TabVelocSeg[c.Veloc] + " s · " + TabVelocAbierto[c.Veloc] + " % abierto a 3 min";
                c.VelocCtx = nq ? apuestaTxt : "MNQ/4 sin medir · " + apuestaTxt;
                // ---- GAMMA: tres estados desde la estela de Gamma Hoy (capa NQ); nunca vota
                if (!nq) { c.Gamma = -2; c.GammaTxt = "solo NQ/MNQ"; c.GammaCtx = "GAMMA: solo NQ/MNQ · en MES no hay capa"; }
                else
                {
                    var e = _tabEstela;
                    if (!e.Ok) { c.Gamma = 0; c.GammaTxt = "sin lectura"; c.GammaCtx = "sin estela de NQ · ¿Gamma Hoy con capa NQ?"; }
                    else
                    {
                        c.Zero = e.Zero; c.Neto = e.Neto; c.Libro = e.Libro ?? "vol"; c.EdadSeg = (int)Math.Max(0, (ahora - e.T).TotalSeconds); c.DZero = precio - e.Zero;
                        string hace = c.EdadSeg < 90 ? "hace " + c.EdadSeg + " s" : "hace " + (c.EdadSeg / 60) + " min";
                        string zeroTxt = "zero " + e.Zero.ToString("#,0", Es) + " · " + c.DZero.ToString("+0;-0", Es) + " pts";
                        if (c.EdadSeg > TabGuardiaSeg) { c.Gamma = 0; c.GammaTxt = "sin lectura"; c.GammaCtx = "estela vieja · " + hace + (e.Zero > 0 ? " · " + zeroTxt : ""); }
                        else if (e.Zero <= 0) { c.Gamma = 0; c.GammaTxt = "sin lectura"; c.GammaCtx = "sin zero en la capa NQ · " + hace; }
                        else if (Math.Abs(c.DZero) > TabGuardiaNq) { c.Gamma = 0; c.GammaTxt = "sin lectura"; c.GammaCtx = "libro flaco · " + zeroTxt + " · " + hace; }
                        else { c.Gamma = c.DZero >= 0 && e.Neto > 0 ? 1 : -1; c.GammaTxt = c.Gamma > 0 ? "frenan" : "empujan"; c.GammaCtx = zeroTxt + " · NQ " + c.Libro + " · " + hace; }
                    }
                }
                _tab = c;
                if (c.Estado != _tabEstadoPrevio) { bool arranque = _tabEstadoPrevio == null; _tabEstadoPrevio = c.Estado; if (velasVivas) TableroGrabar(c, arranque); }
            }
            catch (Exception e) { Registrar(e); }
        }

        /// <summary>La ultima linea completa de la estela de la capa NQ (hoy; si no existe, ayer). Solo la cola del archivo, cada 5 s.</summary>
        private void TableroLeerEstela(DateTime ahora)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex", "estela");
                string ruta = Path.Combine(dir, "estela-NQ-" + ahora.ToString("yyyy-MM-dd", Inv) + ".jsonl");
                if (!File.Exists(ruta)) ruta = Path.Combine(dir, "estela-NQ-" + ahora.AddDays(-1).ToString("yyyy-MM-dd", Inv) + ".jsonl");
                if (!File.Exists(ruta)) { _tabEstela = default; return; }
                string cola;
                using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long desde = Math.Max(0, fs.Length - 4096); fs.Seek(desde, SeekOrigin.Begin);
                    var buf = new byte[fs.Length - desde]; int n = fs.Read(buf, 0, buf.Length); cola = Encoding.UTF8.GetString(buf, 0, n);
                }
                var lineas = cola.Split('\n'); DateTime tUlt = DateTime.MinValue; double netoUlt = 0; string libroUlt = "vol";
                // la linea mas nueva CON zero manda (Gamma Hoy intercala lineas con d[2] = 0 cuando no hay cruce); si ninguna lo trae, la mas nueva sin zero
                for (int i = lineas.Length - 1; i >= 0; i--)
                {
                    var x = lineas[i].Trim(); if (!x.StartsWith("{") || !x.EndsWith("}")) continue;
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(x); var r = doc.RootElement;
                        if (!DateTime.TryParseExact(r.GetProperty("t").GetString(), "yyyy-MM-dd'T'HH:mm:ss'Z'", Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)) continue;
                        var d = r.GetProperty("d").EnumerateArray().Select(v => v.GetDouble()).ToList();
                        double zero = d.Count >= 3 ? d[2] : 0;   // d = [dominante 1, dominante 2, zero gamma] (GammaHoyCapas.PuntosEstela)
                        double neto = r.TryGetProperty("n", out var nn) ? nn.GetDouble() : 0, fut = r.TryGetProperty("f", out var ff) ? ff.GetDouble() : 0;
                        string libro = r.TryGetProperty("b", out var b) ? b.GetString() : "vol";
                        if (tUlt == DateTime.MinValue) { tUlt = t; netoUlt = neto; libroUlt = libro; }
                        if (zero <= 0) continue;
                        _tabEstela = (true, t, zero, neto, libro, fut);
                        return;
                    }
                    catch { }
                }
                if (tUlt != DateTime.MinValue) _tabEstela = (true, tUlt, 0, netoUlt, libroUlt, 0);
            }
            catch (Exception e) { Registrar(e); }
        }

        /// <summary>Cada cambio de estado del resumen, a un jsonl (hora UTC, S, las cinco lecturas con su voto, escalon de velocidad, gamma). Fuera de toda llave.</summary>
        private void TableroGrabar(Cuadro c, bool arranque)
        {
            if (!FcGuardarMarcas) return;
            try
            {
                string inst = SondaInstrumento(), marco = (ChartInfo?.TimeFrame ?? "").Replace(" ", "");
                var pth = Path.Combine(DirFlujo, "tablero-" + inst + "-" + marco + "-" + c.Utc.ToString("yyyy-MM-dd", Inv) + ".jsonl");   // un archivo por grafico: dos marcos no se mezclan
                var sb = new StringBuilder(400);
                sb.Append("{\"ver\":\"1.8\"").Append(arranque ? ",\"arranque\":1" : "").Append(",\"utc\":\"").Append(c.Utc.ToString("yyyy-MM-ddTHH:mm:ss", Inv)).Append("\",\"inst\":\"").Append(inst)
                  .Append("\",\"marco\":\"").Append((ChartInfo?.ChartType.ToString() ?? "") + " " + (ChartInfo?.TimeFrame ?? "")).Append("\",\"estado\":\"").Append(c.Estado)
                  .Append("\",\"S\":").Append(c.S).Append(",\"favor\":").Append(c.Favor).Append(",\"compran\":").Append(c.Compran).Append(",\"venden\":").Append(c.Venden)
                  .Append(",\"precio\":").Append(c.Precio.ToString("0.##", Inv))
                  .Append(",\"r60\":").Append(c.R60.ToString("0.000", Inv)).Append(",\"v60\":").Append(c.V60)
                  .Append(",\"r300\":").Append(c.R300.ToString("0.000", Inv)).Append(",\"v300\":").Append(c.V300)
                  .Append(",\"g300\":").Append(c.G300.ToString("0", Inv)).Append(",\"vg\":").Append(c.VG)
                  .Append(",\"zcvd\":").Append(c.ZCvd.ToString("0.00", Inv)).Append(",\"vcvd\":").Append(c.VCvd)
                  .Append(",\"zpx\":").Append(c.ZPx.ToString("0.00", Inv)).Append(",\"vpx\":").Append(c.VPx).Append(",\"z15ok\":").Append(c.Z15Ok ? 1 : 0)
                  .Append(",\"tend\":").Append(c.Tend.ToString("0.00", Inv))
                  .Append(",\"rango5\":").Append(c.Rango5.ToString("0.00", Inv)).Append(",\"rangovivo\":").Append(c.RangoVivo ? 1 : 0).Append(",\"veloc\":").Append(c.Veloc).Append(",\"velocnombre\":\"").Append(TabVelocNombre[c.Veloc])
                  .Append("\",\"gamma\":").Append(c.Gamma).Append(",\"gammanombre\":\"").Append(c.GammaTxt).Append("\",\"zero\":").Append(c.Zero.ToString("0.##", Inv))
                  .Append(",\"dzero\":").Append(c.DZero.ToString("0.##", Inv)).Append(",\"neto\":").Append(c.Neto.ToString("0", Inv)).Append(",\"edad\":").Append(c.EdadSeg).Append(",\"libro\":\"").Append(c.Libro).Append("\"}");
                string l = sb.ToString();
                lock (_llaveArchivo) _colaArchivo = _colaArchivo.ContinueWith(_ => { try { Directory.CreateDirectory(DirFlujo); File.AppendAllText(pth, l + "\n"); } catch { } });
            }
            catch (Exception e) { Registrar(e); }
        }

        // ------------------------------------------------------------------ dibujo (solo lee el ultimo cuadro)
        /// <summary>Saca segmentos " · " del final hasta que el texto entre en 'ancho'.</summary>
        private static string TableroRecortar(RenderContext g, string s, RenderFont f, int ancho)
        {
            while (s.Length > 0 && g.MeasureString(s, f).Width > ancho) { int i = s.LastIndexOf(" · ", StringComparison.Ordinal); if (i <= 0) break; s = s.Substring(0, i); }
            return s;
        }

        /// <summary>Semicirculo de sectores (anillo) con aguja opcional. Escala -1 (izquierda) .. +1 (derecha); 180 grados = izquierda, 270 = arriba (GDI+).</summary>
        private static void TableroMedidor(RenderContext g, int cx, int cy, int R, IList<(double A, double B, Color C)> sectores, double? aguja)
        {
            var rect = new Rectangle(cx - R, cy - R, 2 * R, 2 * R);
            foreach (var s in sectores) g.FillPie(s.C, rect, (float)(180 + 90 * (1 + s.A)), (float)(90 * (s.B - s.A)));
            int ri = R - 7; g.FillPie(TabFondo, new Rectangle(cx - ri, cy - ri, 2 * ri, 2 * ri), 180f, 180f);   // el hueco del anillo
            if (!aguja.HasValue) return;
            double th = (180 + 90 * (1 + Math.Max(-1, Math.Min(1, aguja.Value)))) * Math.PI / 180;
            int x2 = (int)Math.Round(cx + (R - 3) * Math.Cos(th)), y2 = (int)Math.Round(cy + (R - 3) * Math.Sin(th));
            g.DrawLine(new RenderPen(Color.White, 2f), cx, cy, x2, y2);
            g.FillEllipse(Color.White, new Rectangle(cx - 2, cy - 2, 5, 5));
        }

        private void TableroPintar(RenderContext g, Rectangle r, int xMaxEje, int yTitulo, int altoTitulo, RenderFont fCh, Color cCompra, Color cVenta, Color cAviso, Color cTxt)
        {
            var c = _tab;
            var fTit = new RenderFont("Consolas", Math.Max(6, FcLetra - 1), FontStyle.Bold);
            var fRes = new RenderFont("Consolas", FcLetra + 1, FontStyle.Bold);
            var fMin = new RenderFont("Consolas", Math.Max(6, FcLetra - 2));   // titulos de los medidores: 7 pt, para que entren las cuatro lineas de contexto en 123 px
            int altoCh = g.MeasureString("0", fCh).Height;
            Color dim = Alfa(cTxt, 150);
            // cabecera fija en el renglon del titulo, encima del tablero: no se recorta ni se apaga
            {
                string a = "AHORA", b = "  estado, no pronostico"; var ma = g.MeasureString(a, fTit);
                int y = yTitulo + Math.Max(0, (altoTitulo - ma.Height) / 2);
                g.DrawString(a, fTit, cTxt, r.Left + 2, y);
                if (r.Left + 2 + ma.Width + g.MeasureString(b, fCh).Width <= xMaxEje) g.DrawString(b, fCh, dim, r.Left + 2 + ma.Width, y);
            }
            g.FillRectangle(TabFondo, r, 4); g.DrawRectangle(new RenderPen(TabBorde, 1f), r, 4);
            if (c == null) { g.DrawString("tablero: esperando la primera vela en vivo", fCh, dim, r.Left + 6, r.Top + 6); return; }
            try { g.SetSmoothingMode(RenderSmoothingModes.AntiAlias); } catch { }
            int altoMin = g.MeasureString("0", fMin).Height;
            int cols = r.Width >= TabAncho ? 4 : 2, filas = 4 / cols;
            int colW = (r.Width - 8) / cols, R = Math.Min(20, (colW - 6) / 2), y0 = r.Top + 2, ancho = r.Width - 12, altoMed = altoMin + 1 + R + 2 + altoCh + 2;
            bool compacto = colW < 66;   // menos de 286 px: rotulos cortos y palabras en la letra chica
            string[] tit = compacto ? new[] { "FLUJO 1m", "TEND.", "GAMMA", "VELOC." } : new[] { "FLUJO 60s", "TENDENCIA", "GAMMA", "VELOCIDAD" };
            var fPal = compacto ? fMin : fCh;
            void Centrado(string s, RenderFont f, Color col, int cx, int y) { var m = g.MeasureString(s, f); g.DrawString(s, f, col, cx - m.Width / 2, y); }
            // ---- los cuatro medidores
            var sec5 = new (double A, double B, Color C)[] { (-1, -0.5, Alfa(cVenta, 235)), (-0.5, -0.1, Alfa(cVenta, 110)), (-0.1, 0.1, Alfa(TabGris, 90)), (0.1, 0.5, Alfa(cCompra, 110)), (0.5, 1, Alfa(cCompra, 235)) };
            var secT = new (double A, double B, Color C)[] { (-1, -0.6, Alfa(cVenta, 235)), (-0.6, -0.2, Alfa(cVenta, 110)), (-0.2, 0.2, Alfa(TabGris, 90)), (0.2, 0.6, Alfa(cCompra, 110)), (0.6, 1, Alfa(cCompra, 235)) };
            for (int i = 0; i < 4; i++)
            {
                int cx = r.Left + 4 + colW * (i % cols) + colW / 2, y0i = y0 + (i / cols) * altoMed, cy = y0i + altoMin + 1 + R;
                Centrado(tit[i], fMin, dim, cx, y0i);
                string palabra; Color col;
                switch (i)
                {
                    case 0:
                    {
                        double v = Math.Max(-1, Math.Min(1, c.R60 / TabTope60)); TableroMedidor(g, cx, cy, R, sec5, v);
                        string num = (c.R60 * 100).ToString("+0;-0", Es);
                        palabra = Math.Abs(c.R60) < TabMuerta60 ? "parejo " + num : (c.R60 > 0 ? "compran " : "venden ") + num;
                        col = Math.Abs(c.R60) < TabMuerta60 ? TabGris : c.R60 > 0 ? cCompra : cVenta; break;
                    }
                    case 1:
                    {
                        TableroMedidor(g, cx, cy, R, secT, c.Tend);
                        string num = (c.Tend * 100).ToString("+0;-0", Es);
                        palabra = !c.Z15Ok ? "sin historia" : Math.Abs(c.Tend) < 0.2 ? "parejo " + num : (c.Tend > 0 ? "subió " : "bajó ") + num;
                        col = !c.Z15Ok || Math.Abs(c.Tend) < 0.2 ? TabGris : c.Tend > 0 ? cCompra : cVenta; break;
                    }
                    case 2:
                    {
                        int act = c.Gamma < 0 && c.Gamma != -2 ? 0 : c.Gamma > 0 ? 2 : 1;
                        var secG = new (double A, double B, Color C)[] { (-1, -1.0 / 3, Alfa(cAviso, act == 0 ? 255 : 60)), (-1.0 / 3, 1.0 / 3, Alfa(TabGris, act == 1 ? 220 : 60)), (1.0 / 3, 1, Alfa(TabAzul, act == 2 ? 255 : 60)) };
                        TableroMedidor(g, cx, cy, R, secG, null);
                        palabra = c.GammaTxt; col = act == 0 ? cAviso : act == 2 ? TabAzul : TabGris; break;
                    }
                    default:
                    {
                        Color[] cv = { TabAzul, TabCeleste, TabGris, TabNaranja, TabRojo };
                        var secV = new (double A, double B, Color C)[5];
                        for (int k = 0; k < 5; k++) secV[k] = (-1 + 0.4 * k, -1 + 0.4 * (k + 1), Alfa(cv[k], k == c.Veloc ? 255 : 60));
                        TableroMedidor(g, cx, cy, R, secV, null);
                        palabra = c.VelocTxt; col = cv[c.Veloc]; break;
                    }
                }
                Centrado(palabra, fPal, col, cx, cy + 2);
            }
            // ---- separador y resumen en palabras de flujo (nunca de posicion)
            int ySep = y0 + filas * altoMed - 1;
            g.DrawLine(new RenderPen(TabBorde, 1f), r.Left + 6, ySep, r.Right - 6, ySep);
            Color cRes = Math.Abs(c.S) <= 1 ? TabGris : c.S > 0 ? Alfa(cCompra, c.S >= 4 ? 255 : 200) : Alfa(cVenta, c.S <= -4 ? 255 : 200);
            var mRes = g.MeasureString(c.Estado, fRes); int yRes = ySep + 2;
            g.DrawString(c.Estado, fRes, cRes, r.Left + 6, yRes);
            string favor = c.S != 0 ? "a favor " + c.Favor + "/5 · 1m x2" : "compran " + c.Compran + " · venden " + c.Venden + " de 5";
            g.DrawString(TableroRecortar(g, favor, fCh, r.Right - 6 - (r.Left + 6 + mRes.Width + 8)), fCh, dim, r.Left + 6 + mRes.Width + 8, yRes + Math.Max(0, (mRes.Height - altoCh) / 2));
            // ---- contexto: las cinco lecturas, gamma con fuente y edad, la apuesta por velocidad, quien vota
            string cvd = c.Z15Ok ? c.ZCvd.ToString("+0.0;-0.0", Es) : "—", px = c.Z15Ok ? c.ZPx.ToString("+0.0;-0.0", Es) : "—";
            var lineas = new List<(string T, Color C)>
            {
                ("1m " + (c.R60 * 100).ToString("+0;-0", Es) + " · 5m " + (c.R300 * 100).ToString("+0;-0", Es) + " · gr " + c.G300.ToString("+#,0;-#,0;0", Es) + " · cvd " + cvd + " · px " + px, cTxt),
                (c.GammaCtx, c.Gamma == 0 || c.Gamma == -2 ? dim : cTxt),
                (c.VelocCtx, cTxt),
                ("gamma y velocidad no votan · votan 1m x2, 5m, gr, cvd, px", dim),
            };
            int paso = Math.Max(10, altoCh - 1), yL = yRes + mRes.Height;   // presupuesto a 123 px: 2+11+1+20+2+13+1+2+16+(3x12+13)+2 = 119; si una fuente mide mas, cae la ultima linea
            foreach (var (txt, col) in lineas)
            {
                if (yL + altoCh > r.Bottom - 2) break;
                g.DrawString(TableroRecortar(g, txt, fCh, ancho), fCh, col, r.Left + 6, yL); yL += paso;
            }
        }
    }
}
