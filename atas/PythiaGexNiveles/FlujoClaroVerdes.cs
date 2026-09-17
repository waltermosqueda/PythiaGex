using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using System.Drawing;

using ATAS.Indicators;
using OFT.Rendering.Tools;

namespace PythiaGex
{
    /// <summary>
    /// EL RECLAMO EN LAS VERDES, EN SEGUNDOS (17-09-2026, pedido del operador: "bajemos la temporalidad a segundos... contra las rayas verdes...
    /// vemos si funciona en tiempo real con la sesion Asia y si lo grafica con exactitud").
    ///
    /// Que hace: lee las dominantes de la capa del libro propio (NQ para MNQ/NQ, ES para MES/ES) del archivo de estela que escribe Gamma Hoy
    /// (%APPDATA%\ATAS\PythiaGex\estela\estela-&lt;capa&gt;-&lt;dia&gt;.jsonl) y sigue cada TOQUE operacion por operacion:
    ///   1. el precio VENIA de lejos (>= Lejos puntos) de una verde que ya estaba dibujada;
    ///   2. entra a la zona (<= Tolerancia) y puede traspasarla hasta TraspasoMax (la mecha);
    ///   3. RECLAMO: vuelve al lado bueno de la raya (>= Reclamo puntos) dentro de Visita segundos;
    ///   4. lo marca en el grafico y abre una OPERACION EN SOMBRA (no manda ninguna orden): entrada al precio del gatillo, stop 1 punto detras
    ///      de la mecha, objetivo fijo; la sigue hasta que toca uno de los dos (o 15 min) y anota el resultado.
    /// Todo queda en %APPDATA%\ATAS\PythiaGex\flujo\verdes-&lt;instrumento&gt;-&lt;dia&gt;.jsonl para juzgarlo con numeros.
    ///
    /// LO MEDIDO (laboratorio/gatillo/verdes_*.py, 7 dias con el libro de NQ grabado): el 17-09 las verdes rebotaron 58 % contra 30 % de las rayas
    /// corridas; los otros 6 dias, igual que el placebo. Esto es una HERRAMIENTA DE MEDICION Y LECTURA, no una señal validada: la cuenta en sombra
    /// es la que va a decir si sirve.
    /// </summary>
    public partial class FlujoClaro
    {
        public enum GatilloVerde { Cruce, Mecha }

        [Display(Name = "Detector de reclamo en las verdes", GroupName = "8. Verdes (en segundos)", Order = 1,
                 Description = "Sigue cada toque de las dominantes del libro propio (las verdes fuertes de Gamma Hoy) operacion por operacion, marca el reclamo y lleva la cuenta EN SOMBRA (no manda ordenes). Necesita Gamma Hoy con la capa NQ (o ES) prendida en algun grafico.")]
        public bool FcVerdes { get; set; } = true;
        [Display(Name = "Gatillo", GroupName = "8. Verdes (en segundos)", Order = 2,
                 Description = "Cruce = la traspaso (>= 2 ticks) y volvio al lado bueno. Mecha = reboto N puntos desde el extremo aunque no la haya cruzado.")]
        public GatilloVerde FcVerdesGatillo { get; set; } = GatilloVerde.Cruce;
        [Display(Name = "Exigir delta de 10 s a favor del rebote", GroupName = "8. Verdes (en segundos)", Order = 2,
                 Description = "Medido en 7 dias (laboratorio/gatillo/verdes_20_reclamo.py): cruce + delta a favor, objetivo 20: +1,40 pts netos por operacion (n 158; sin el 17-09 +0,70) contra -0,51 de las mismas rayas corridas. Sin el filtro: +0,73 / +0,37. No es significativo todavia (t ~1,5): por eso va EN SOMBRA.")]
        public bool FcVerdesDelta { get; set; } = true;
        [Display(Name = "Tolerancia de la zona (pts de NQ; ES usa 1/4)", GroupName = "8. Verdes (en segundos)", Order = 3)]
        [Range(0.25, 20)] public decimal FcVerdesTol { get; set; } = 1.5m;
        [Display(Name = "Venia de lejos (pts)", GroupName = "8. Verdes (en segundos)", Order = 4)]
        [Range(2, 60)] public decimal FcVerdesLejos { get; set; } = 10m;
        [Display(Name = "Traspaso maximo: mas que esto es ruptura (pts)", GroupName = "8. Verdes (en segundos)", Order = 5)]
        [Range(1, 40)] public decimal FcVerdesPenMax { get; set; } = 10m;
        [Display(Name = "Reclamo: cuanto vuelve del lado bueno (pts)", GroupName = "8. Verdes (en segundos)", Order = 6)]
        [Range(0.25, 10)] public decimal FcVerdesRec { get; set; } = 1.5m;
        [Display(Name = "Mecha: rebote desde el extremo (pts)", GroupName = "8. Verdes (en segundos)", Order = 7)]
        [Range(1, 20)] public decimal FcVerdesReb { get; set; } = 4m;
        [Display(Name = "Visita: segundos para que aparezca el reclamo", GroupName = "8. Verdes (en segundos)", Order = 8)]
        [Range(10, 600)] public int FcVerdesVisita { get; set; } = 90;
        [Display(Name = "Operacion en sombra: objetivo (pts)", GroupName = "8. Verdes (en segundos)", Order = 9)]
        [Range(2, 100)] public decimal FcVerdesObjetivo { get; set; } = 20m;
        [Display(Name = "Alerta sonora en el reclamo", GroupName = "8. Verdes (en segundos)", Order = 10)]
        public bool FcVerdesAlerta { get; set; } = false;
        [Display(Name = "Color del reclamo (abierto)", GroupName = "8. Verdes (en segundos)", Order = 11)]
        public System.Windows.Media.Color FcColorReclamo { get; set; } = System.Windows.Media.Color.FromRgb(240, 240, 240);

        private sealed class LineaVerde
        {
            public double Nivel, Fuerza; public bool PorOi;
            public DateTime LejosArriba = DateTime.MinValue, LejosAbajo = DateTime.MinValue;   // ultima vez que el precio estuvo lejos, de cada lado
            public Visita Activa;
        }
        private sealed class Visita { public int Lado; public DateTime T0; public double Extremo, L, Vel60; }
        private sealed class Sombra
        {
            public DateTime T; public int Lado, Bar; public double Entrada, Stop, Objetivo, L, Traspaso, Fuerza; public bool PorOi;
            public int Resultado;   // 0 abierta, +1 objetivo, -1 stop, 2 vencida
            public double Puntos;
        }

        private readonly object _vLlave = new object();
        private readonly Dictionary<double, LineaVerde> _vLineas = new();
        private readonly List<Sombra> _vSombras = new();
        private readonly SortedDictionary<long, double> _vPrecioSeg = new();
        private DateTime _vLeido = DateTime.MinValue, _vFrescura = DateTime.MinValue; private long _vLargo = -1; private string _vRuta = "";
        private long _vSegPrevio; private double _vPrecioPrevio;
        private double _vNeto; private string _vEstado = ""; private DateTime _vEstadoHasta = DateTime.MinValue; private bool _vRestaurado;

        private string CapaVerde()
        {
            string i = (InstrumentInfo?.Instrument ?? "").ToUpperInvariant();
            return i.Contains("ES") && !i.Contains("NQ") ? "ES" : "NQ";
        }
        private double EscalaVerde() => CapaVerde() == "ES" ? 0.25 : 1.0;
        private double PasoVerde() => CapaVerde() == "ES" ? 1.0 : 5.0;

        /// <summary>Una vez por segundo: relee la cola de la estela si crecio y actualiza las verdes vigentes.</summary>
        private void VerdesLatido()
        {
            if (!FcVerdes) return;
            try
            {
                var ahora = DateTime.UtcNow;
                if ((ahora - _vLeido).TotalSeconds < 1) return; _vLeido = ahora;
                if (!_vRestaurado && _cargado) { _vRestaurado = true; VerdesRestaurar(); }
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex", "estela");
                string ruta = Path.Combine(dir, "estela-" + CapaVerde() + "-" + ahora.ToString("yyyy-MM-dd", Inv) + ".jsonl");
                if (!File.Exists(ruta)) { ruta = Path.Combine(dir, "estela-" + CapaVerde() + "-" + ahora.AddDays(-1).ToString("yyyy-MM-dd", Inv) + ".jsonl"); if (!File.Exists(ruta)) return; }
                var fi = new FileInfo(ruta);
                if (ruta == _vRuta && fi.Length == _vLargo) return;
                _vRuta = ruta; _vLargo = fi.Length;
                string cola;
                using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long desde = Math.Max(0, fs.Length - 2048); fs.Seek(desde, SeekOrigin.Begin);
                    var buf = new byte[fs.Length - desde]; int n = fs.Read(buf, 0, buf.Length); cola = Encoding.UTF8.GetString(buf, 0, n);
                }
                string ultima = cola.Split('\n').Select(x => x.Trim()).LastOrDefault(x => x.StartsWith("{") && x.EndsWith("}"));
                if (ultima == null) return;
                using var doc = System.Text.Json.JsonDocument.Parse(ultima); var r = doc.RootElement;
                if (!DateTime.TryParseExact(r.GetProperty("t").GetString(), "yyyy-MM-dd'T'HH:mm:ss'Z'", Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)) return;
                var niveles = r.GetProperty("d").EnumerateArray().Select(x => x.GetDouble()).Take(2).ToList();
                var fuerzas = r.TryGetProperty("g", out var g) ? g.EnumerateArray().Select(x => x.GetDouble()).ToList() : new List<double>();
                bool porOi = r.TryGetProperty("b", out var b) && b.GetString() == "OI";
                double neto = r.TryGetProperty("n", out var nn) ? nn.GetDouble() : 0;
                lock (_vLlave)
                {
                    _vFrescura = t; _vNeto = neto; var vivos = new HashSet<double>();
                    for (int i = 0; i < niveles.Count; i++)
                    {
                        if (niveles[i] <= 0) continue;
                        double k = Math.Round(niveles[i] / PasoVerde()) * PasoVerde(); vivos.Add(k);
                        if (!_vLineas.TryGetValue(k, out var ln)) { ln = new LineaVerde(); _vLineas[k] = ln; }
                        ln.Nivel = niveles[i]; ln.Fuerza = i < fuerzas.Count ? fuerzas[i] : 0; ln.PorOi = porOi;
                    }
                    foreach (var k in _vLineas.Keys.Where(k => !vivos.Contains(k) && _vLineas[k].Activa == null).ToList()) _vLineas.Remove(k);   // dejo de ser dominante y no hay visita en curso
                }
            }
            catch (Exception e) { Registrar(e); }
        }

        /// <summary>Con cada operacion: arma / sigue / dispara / resuelve. Barato: dos o tres rayas y unas pocas sombras.</summary>
        private void VerdesOperacion(double p, DateTime tUtc)
        {
            if (!FcVerdes || p <= 0) return;
            try
            {
                double esc = EscalaVerde(), tol = (double)FcVerdesTol * esc, lejos = (double)FcVerdesLejos * esc, penMax = (double)FcVerdesPenMax * esc, rec = (double)FcVerdesRec * esc, reb = (double)FcVerdesReb * esc;
                long seg = tUtc.Ticks / TimeSpan.TicksPerSecond;
                lock (_vLlave)
                {
                    bool cambioDeSegundo = seg != _vSegPrevio; double cierrePrevio = _vPrecioPrevio > 0 ? _vPrecioPrevio : p;
                    _vSegPrevio = seg; _vPrecioPrevio = p;
                    _vPrecioSeg[seg] = p;
                    while (_vPrecioSeg.Count > 200) _vPrecioSeg.Remove(_vPrecioSeg.Keys.First());
                    // las sombras abiertas
                    foreach (var s in _vSombras)
                    {
                        if (s.Resultado != 0) continue;
                        if ((p - s.Objetivo) * s.Lado >= 0) VerdesCerrar(s, +1, (s.Objetivo - s.Entrada) * s.Lado, tUtc);
                        else if ((p - s.Stop) * s.Lado <= 0) VerdesCerrar(s, -1, (s.Stop - s.Entrada) * s.Lado, tUtc);
                        else if ((tUtc - s.T).TotalSeconds > 900) VerdesCerrar(s, 2, (p - s.Entrada) * s.Lado, tUtc);
                    }
                    if ((DateTime.UtcNow - _vFrescura).TotalMinutes > 10) return;   // sin estela fresca no hay rayas: no se inventa nada
                    foreach (var ln in _vLineas.Values)
                    {
                        double L = ln.Nivel;
                        if (p >= L + lejos) ln.LejosArriba = tUtc; else if (p <= L - lejos) ln.LejosAbajo = tUtc;
                        var v = ln.Activa;
                        if (v == null)
                        {
                            int lado = 0;
                            if (p <= L + tol && p > L - penMax && (tUtc - ln.LejosArriba).TotalSeconds <= 1800) lado = +1;
                            else if (p >= L - tol && p < L + penMax && (tUtc - ln.LejosAbajo).TotalSeconds <= 1800) lado = -1;
                            if (lado == 0) continue;
                            if (lado > 0) ln.LejosArriba = DateTime.MinValue; else ln.LejosAbajo = DateTime.MinValue;   // hay que volver a alejarse para otro toque
                            double hace60 = _vPrecioSeg.Where(kv => kv.Key <= seg - 60).Select(kv => kv.Value).LastOrDefault();
                            ln.Activa = v = new Visita { Lado = lado, T0 = tUtc, Extremo = p, L = L, Vel60 = hace60 > 0 ? Math.Abs(p - hace60) : 0 };
                            VerdesAnotar("toque", tUtc, L, lado, p, 0, ln, v, null);
                            _vEstado = "VERDE " + L.ToString("#,0.00", Es) + (lado > 0 ? " soporte" : " techo") + ": en zona"; _vEstadoHasta = DateTime.UtcNow.AddSeconds(FcVerdesVisita);
                            continue;
                        }
                        v.Extremo = v.Lado > 0 ? Math.Min(v.Extremo, p) : Math.Max(v.Extremo, p);
                        double pen = (v.L - v.Extremo) * v.Lado;
                        if (pen > penMax) { VerdesAnotar("rota", tUtc, v.L, v.Lado, p, pen, ln, v, null); ln.Activa = null; _vEstado = "VERDE " + v.L.ToString("#,0.00", Es) + " ROTA (" + pen.ToString("0.0", Es) + " pts)"; _vEstadoHasta = DateTime.UtcNow.AddSeconds(45); continue; }
                        if ((tUtc - v.T0).TotalSeconds > FcVerdesVisita) { VerdesAnotar("sin_gatillo", tUtc, v.L, v.Lado, p, pen, ln, v, null); ln.Activa = null; continue; }
                        // la decision se toma una vez por segundo con el CIERRE del segundo anterior (paridad con el banco de pruebas); se entra al precio de ahora
                        double pc = cierrePrevio;
                        bool gat = cambioDeSegundo && (FcVerdesGatillo == GatilloVerde.Cruce ? pen >= 0.5 * esc && (pc - v.L) * v.Lado >= rec
                                                                                            : (pc - v.Extremo) * v.Lado >= reb && (pc - v.L) * v.Lado >= -0.5 * esc);
                        if (gat && FcVerdesDelta) gat = Ventana(10).S * v.Lado > 0;
                        if (!gat) { _vEstado = "VERDE " + v.L.ToString("#,0.00", Es) + ": traspaso " + Math.Max(0, pen).ToString("0.0", Es); _vEstadoHasta = DateTime.UtcNow.AddSeconds(30); continue; }
                        var s = new Sombra { T = tUtc, Lado = v.Lado, Bar = Math.Max(0, CurrentBar - 1), Entrada = p, Stop = v.Extremo - v.Lado * 1.0 * esc, Objetivo = p + v.Lado * (double)FcVerdesObjetivo * esc,
                                             L = v.L, Traspaso = Math.Max(0, pen), Fuerza = ln.Fuerza, PorOi = ln.PorOi };
                        _vSombras.Add(s); if (_vSombras.Count > 400) _vSombras.RemoveAt(0);
                        VerdesAnotar("gatillo", tUtc, v.L, v.Lado, p, pen, ln, v, s); ln.Activa = null;
                        _vEstado = "RECLAMO " + (v.Lado > 0 ? "▲ " : "▼ ") + v.L.ToString("#,0.00", Es) + " (mecha " + Math.Max(0, pen).ToString("0.0", Es) + ")"; _vEstadoHasta = DateTime.UtcNow.AddSeconds(90);
                        if (FcVerdesAlerta) Alertar("RECLAMO " + (v.Lado > 0 ? "comprador" : "vendedor") + " en la verde " + v.L.ToString("0.00", Es));
                    }
                }
            }
            catch (Exception e) { Registrar(e); }
        }

        private void VerdesCerrar(Sombra s, int resultado, double puntos, DateTime t)
        {
            s.Resultado = resultado; s.Puntos = puntos;
            VerdesEscribir("{\"ver\":\"1.6\",\"ev\":\"resultado\",\"utc\":\"" + t.ToString("yyyy-MM-ddTHH:mm:ss", Inv) + "\",\"abierta\":\"" + s.T.ToString("yyyy-MM-ddTHH:mm:ss", Inv) + "\",\"lado\":" + s.Lado
                + ",\"raya\":" + s.L.ToString("0.00", Inv) + ",\"entrada\":" + s.Entrada.ToString("0.00", Inv) + ",\"resultado\":" + resultado + ",\"puntos\":" + puntos.ToString("0.00", Inv) + "}");
        }

        private void VerdesAnotar(string ev, DateTime t, double L, int lado, double p, double pen, LineaVerde ln, Visita v, Sombra s)
        {
            var (d10, v10) = Ventana(10);
            string l = "{\"ver\":\"1.6\",\"ev\":\"" + ev + "\",\"utc\":\"" + t.ToString("yyyy-MM-ddTHH:mm:ss", Inv) + "\",\"raya\":" + L.ToString("0.00", Inv) + ",\"lado\":" + lado + ",\"precio\":" + p.ToString("0.00", Inv)
                + ",\"traspaso\":" + Math.Max(0, pen).ToString("0.00", Inv) + ",\"seg_en_zona\":" + (t - v.T0).TotalSeconds.ToString("0", Inv) + ",\"vel60\":" + v.Vel60.ToString("0.00", Inv)
                + ",\"delta10\":" + d10.ToString("0", Inv) + ",\"vol10\":" + v10.ToString("0", Inv) + ",\"fuerza\":" + ln.Fuerza.ToString("0", Inv) + ",\"neto\":" + _vNeto.ToString("0", Inv) + ",\"libro\":\"" + (ln.PorOi ? "OI" : "vol") + "\""
                + ",\"gatillo\":\"" + FcVerdesGatillo + "\"" + (s != null ? ",\"stop\":" + s.Stop.ToString("0.00", Inv) + ",\"objetivo\":" + s.Objetivo.ToString("0.00", Inv) : "") + "}";
            VerdesEscribir(l);
        }

        private void VerdesEscribir(string linea)
        {
            string inst = SondaInstrumento(); var pth = Path.Combine(DirFlujo, "verdes-" + inst + "-" + DateTime.UtcNow.ToString("yyyy-MM-dd", Inv) + ".jsonl");
            lock (_llaveArchivo) _colaArchivo = _colaArchivo.ContinueWith(_ => { try { Directory.CreateDirectory(DirFlujo); File.AppendAllText(pth, linea + "\n"); } catch { } });
        }

        /// <summary>Al cargar: vuelve a poner en el grafico los reclamos de hoy y de ayer (con su resultado) desde el registro.</summary>
        private void VerdesRestaurar()
        {
            try
            {
                var abiertos = new Dictionary<string, Sombra>(); int n = 0;
                for (int atras = 1; atras >= 0; atras--)
                {
                    var pth = Path.Combine(DirFlujo, "verdes-" + SondaInstrumento() + "-" + DateTime.UtcNow.AddDays(-atras).ToString("yyyy-MM-dd", Inv) + ".jsonl");
                    if (!File.Exists(pth)) continue;
                    foreach (var l in File.ReadAllLines(pth))
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(l); var r = doc.RootElement; string ev = r.GetProperty("ev").GetString();
                            if (ev == "gatillo")
                            {
                                var t = DateTime.ParseExact(r.GetProperty("utc").GetString(), "yyyy-MM-ddTHH:mm:ss", Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                                int bar; lock (_llave) bar = BarraDe(t); if (bar < 0) continue;
                                var s = new Sombra { T = t, Lado = r.GetProperty("lado").GetInt32(), Bar = bar, Entrada = r.GetProperty("precio").GetDouble(), Stop = r.GetProperty("stop").GetDouble(),
                                                     Objetivo = r.GetProperty("objetivo").GetDouble(), L = r.GetProperty("raya").GetDouble(), Traspaso = r.GetProperty("traspaso").GetDouble(), Resultado = 2 };
                                abiertos[r.GetProperty("utc").GetString()] = s; lock (_vLlave) _vSombras.Add(s); n++;
                            }
                            else if (ev == "resultado" && abiertos.TryGetValue(r.GetProperty("abierta").GetString(), out var s0)) { s0.Resultado = r.GetProperty("resultado").GetInt32(); s0.Puntos = r.GetProperty("puntos").GetDouble(); }
                        }
                        catch { }
                    }
                }
                if (n > 0) Log("verdes: " + n + " reclamos del registro vueltos al grafico");
            }
            catch (Exception e) { Registrar(e); }
        }

        /// <summary>Marcas sobre el precio: triangulo en la entrada de cada reclamo (blanco = abierta, verde = objetivo, rojo = stop, gris = vencida) y el tramo stop-objetivo.</summary>
        private void VerdesPintar(OFT.Rendering.Context.RenderContext g, Func<int, int> xDeBarra, Func<double, int> yDePrecio, Rectangle rp, int desde, int hasta, int xMax, Color cCompra, Color cVenta, Color cTxt)
        {
            if (!FcVerdes) return;
            List<Sombra> ss; lock (_vLlave) ss = _vSombras.Where(s => s.Bar >= desde && s.Bar <= hasta).ToList();
            var cAb = De(FcColorReclamo);
            foreach (var s in ss)
            {
                int xc; try { xc = xDeBarra(s.Bar); } catch { continue; }
                if (xc < rp.Left || xc > Math.Min(xMax, rp.Right)) continue;
                int y, yS, yO; try { y = yDePrecio(s.Entrada); yS = yDePrecio(s.Stop); yO = yDePrecio(s.Objetivo); } catch { continue; }
                if (y < rp.Top + 8 || y > rp.Bottom - 8) continue;
                var col = s.Resultado == 0 ? cAb : s.Resultado == 1 ? cCompra : s.Resultado == -1 ? cVenta : Alfa(cTxt, 170);
                int t = 6;
                var tri = s.Lado > 0 ? new[] { new Point(xc - t, y + t + 3), new Point(xc + t, y + t + 3), new Point(xc, y + 1) } : new[] { new Point(xc - t, y - t - 3), new Point(xc + t, y - t - 3), new Point(xc, y - 1) };
                g.FillPolygon(col, tri); g.DrawPolygon(new RenderPen(Color.Black, 1f), tri);
                if (s.Resultado == 0)
                {
                    var pen = new RenderPen(Alfa(cAb, 150), 1f, System.Drawing.Drawing2D.DashStyle.Dot);
                    g.DrawLine(pen, xc, Math.Max(rp.Top, Math.Min(rp.Bottom, yS)), xc + 26, Math.Max(rp.Top, Math.Min(rp.Bottom, yS))); g.DrawLine(pen, xc, Math.Max(rp.Top, Math.Min(rp.Bottom, yO)), xc + 26, Math.Max(rp.Top, Math.Min(rp.Bottom, yO)));
                }
            }
        }

        /// <summary>Para la fila AHORA: estado del toque en curso y la cuenta en sombra de hoy.</summary>
        private (string Estado, string Cuenta, int Signo) VerdesTexto()
        {
            if (!FcVerdes) return ("", "", 0);
            lock (_vLlave)
            {
                var hoy = _vSombras.Where(s => s.T.Date == DateTime.UtcNow.Date || (DateTime.UtcNow - s.T).TotalHours < 12).ToList();
                var cerr = hoy.Where(s => s.Resultado != 0).ToList(); double costo = 0.96 * EscalaVerde();
                double neto = cerr.Sum(s => s.Puntos - costo);
                string cuenta = hoy.Count == 0 ? ((DateTime.UtcNow - _vFrescura).TotalMinutes > 10 ? "VERDES sin estela" : "VERDES 0") :
                    "VERDES " + cerr.Count(s => s.Puntos > 0) + "/" + cerr.Count + " " + neto.ToString("+0.0;-0.0", Es) + " pts en sombra" + (hoy.Count > cerr.Count ? " (+" + (hoy.Count - cerr.Count) + " abierta)" : "");
                return (DateTime.UtcNow <= _vEstadoHasta ? _vEstado : "", cuenta, Math.Sign(neto));
            }
        }
    }
}
