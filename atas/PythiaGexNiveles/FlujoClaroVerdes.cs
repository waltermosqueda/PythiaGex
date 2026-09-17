using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using ATAS.Indicators;
using OFT.Rendering.Tools;

namespace PythiaGex
{
    /// <summary>
    /// EL RECLAMO EN LAS VERDES, EN SEGUNDOS Y EN SOMBRA (1.7, 17-09-2026). Este archivo es solo el ADAPTADOR a ATAS: lee la estela que escribe Gamma Hoy,
    /// le pasa cada operacion al nucleo (VerdesNucleo.cs, el MISMO codigo que corre el banco de pruebas atas/VerdesBanco), dibuja las marcas y anota.
    /// NO manda ordenes. Un solo detector por raiz (NQ): si hay dos graficos del mismo futuro, uno alimenta y anota; el otro solo dibuja lo mismo.
    ///
    /// LO MEDIDO (17-09, dos revisores): con 7 dias la ventaja del reclamo en las verdes NO se distingue del azar (toda la ganancia del banco salia del 16 y
    /// el 17-09; los otros 5 dias -0,09 pts por operacion; contra rayas corridas fuera del tunel la diferencia es +0,54 con intervalo que incluye el cero;
    /// de noche, igual que el placebo). Por eso esto es una CUENTA EN SOMBRA con su placebo en vivo (rayas corridas +-37,5 y +-62,5). Criterio escrito antes
    /// de juntar datos: 30 ruedas posteriores al 17-09, solo 13:30-20:00 UTC, sin tocar parametros; sirve si el neto por operacion de las verdes tiene
    /// intervalo del 90 % por dias arriba de cero Y queda arriba del de las rayas corridas; si a las 20 ruedas el neto es menor que +0,5, se abandona.
    /// </summary>
    public partial class FlujoClaro
    {
        [Display(Name = "Detector de reclamo en las verdes (en sombra)", GroupName = "8. Verdes (en segundos)", Order = 1,
                 Description = "Sigue cada toque de las dominantes del libro de opciones de NQ (las verdes fuertes de Gamma Hoy) operacion por operacion, marca el reclamo y lleva la cuenta EN SOMBRA (no manda ordenes), junto con rayas corridas de control. Solo NQ/MNQ. Necesita Gamma Hoy con la capa NQ prendida en algun grafico.")]
        public bool FcVerdes { get; set; } = true;
        [Display(Name = "Exigir delta de 10 s a favor del rebote", GroupName = "8. Verdes (en segundos)", Order = 2,
                 Description = "Parte de la regla congelada para la cuenta en sombra. No tocar hasta juntar las 30 ruedas: cambiarlo reinicia la medicion.")]
        public bool FcVerdesDelta { get; set; } = true;
        [Display(Name = "Operacion en sombra: objetivo (pts)", GroupName = "8. Verdes (en segundos)", Order = 3)]
        [Range(2, 100)] public decimal FcVerdesObjetivo { get; set; } = 20m;
        [Display(Name = "Alerta sonora en el reclamo", GroupName = "8. Verdes (en segundos)", Order = 4)]
        public bool FcVerdesAlerta { get; set; } = false;
        [Display(Name = "Dibujar tambien los reclamos de las rayas de control", GroupName = "8. Verdes (en segundos)", Order = 5)]
        public bool FcVerdesVerPlacebo { get; set; } = false;
        [Display(Name = "Color del reclamo (abierto)", GroupName = "8. Verdes (en segundos)", Order = 6)]
        public System.Windows.Media.Color FcColorReclamo { get; set; } = System.Windows.Media.Color.FromRgb(240, 240, 240);

        // ---- un solo detector por raiz, compartido por todos los graficos de ese futuro
        private sealed class Compartido
        {
            public readonly VerdesNucleo N = new VerdesNucleo(); public readonly object Llave = new object();
            public object Dueno; public DateTime LatidoDueno = DateTime.MinValue; public long Posicion = -1; public string Ruta = ""; public bool Restaurado;
        }
        private static readonly Dictionary<string, Compartido> _vTodos = new Dictionary<string, Compartido>();
        private Compartido _vC; private DateTime _vLeido = DateTime.MinValue;

        private bool VerdesDisponible()
        {
            string i = (InstrumentInfo?.Instrument ?? "").ToUpperInvariant();
            return i.Contains("NQ");      // la capa ES de Gamma Hoy esta en precio de NQ (por beta): en MES no hay rayas propias que seguir
        }

        private Compartido VerdesCompartido()
        {
            if (_vC != null) return _vC;
            lock (_vTodos)
            {
                if (!_vTodos.TryGetValue("NQ", out var c))
                {
                    c = new Compartido(); c.N.A.ExigirDelta = FcVerdesDelta; c.N.A.Objetivo = (double)FcVerdesObjetivo; c.N.Arrancar(DateTime.UtcNow);
                    c.N.Evento = e => VerdesEscribir(e); _vTodos["NQ"] = c;
                }
                return _vC = c;
            }
        }

        /// <summary>Soy el grafico que alimenta y anota? El primero que llega; si deja de latir 10 s (se cerro), lo releva otro.</summary>
        private bool VerdesSoyDueno(Compartido c)
        {
            lock (c.Llave)
            {
                var ahora = DateTime.UtcNow;
                if (c.Dueno == null || ReferenceEquals(c.Dueno, this) || (ahora - c.LatidoDueno).TotalSeconds > 10) { if (!ReferenceEquals(c.Dueno, this)) Log("verdes: este grafico (" + SondaInstrumento() + ") alimenta el detector"); c.Dueno = this; c.LatidoDueno = ahora; return true; }
                return false;
            }
        }

        /// <summary>Una vez por segundo: lee TODAS las lineas nuevas de la estela de la capa NQ, en orden, y se las pasa al nucleo.</summary>
        private void VerdesLatido()
        {
            if (!FcVerdes || !VerdesDisponible()) return;
            try
            {
                var ahora = DateTime.UtcNow;
                if ((ahora - _vLeido).TotalSeconds < 1) return; _vLeido = ahora;
                var c = VerdesCompartido(); if (!VerdesSoyDueno(c)) return;
                if (!c.Restaurado && _cargado) { c.Restaurado = true; VerdesRestaurar(c); }
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex", "estela");
                string ruta = Path.Combine(dir, "estela-NQ-" + ahora.ToString("yyyy-MM-dd", Inv) + ".jsonl");
                if (!File.Exists(ruta)) return;
                if (ruta != c.Ruta) { c.Ruta = ruta; c.Posicion = -1; }
                var fi = new FileInfo(ruta);
                if (c.Posicion < 0) c.Posicion = Math.Max(0, fi.Length - 4096);      // al arrancar: solo la cola (el nucleo descarta lo anterior a su arranque)
                if (fi.Length <= c.Posicion) return;
                string nuevo;
                using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fs.Seek(c.Posicion, SeekOrigin.Begin); var buf = new byte[Math.Min(1 << 20, fs.Length - c.Posicion)]; int n = fs.Read(buf, 0, buf.Length);
                    int fin = Array.LastIndexOf(buf, (byte)'\n', n - 1); if (fin < 0) return;          // linea a medio escribir: la proxima
                    nuevo = Encoding.UTF8.GetString(buf, 0, fin + 1); c.Posicion += fin + 1;
                }
                foreach (var l in nuevo.Split('\n'))
                {
                    var x = l.Trim(); if (!x.StartsWith("{") || !x.EndsWith("}")) continue;
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(x); var r = doc.RootElement;
                        if (!DateTime.TryParseExact(r.GetProperty("t").GetString(), "yyyy-MM-dd'T'HH:mm:ss'Z'", Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)) continue;
                        var niveles = r.GetProperty("d").EnumerateArray().Select(v => v.GetDouble()).Take(2).ToList();
                        var fuerzas = r.TryGetProperty("g", out var g) ? g.EnumerateArray().Select(v => v.GetDouble()).ToList() : new List<double>();
                        bool porOi = r.TryGetProperty("b", out var b) && b.GetString() == "OI";
                        double neto = r.TryGetProperty("n", out var nn) ? nn.GetDouble() : 0, fut = r.TryGetProperty("f", out var ff) ? ff.GetDouble() : 0;
                        lock (c.Llave) c.N.Rayas(t, niveles, fuerzas, porOi, neto, fut);
                    }
                    catch { }
                }
            }
            catch (Exception e) { Registrar(e); }
        }

        /// <summary>Con cada operacion de la cinta (solo el grafico dueño): todo el trabajo lo hace el nucleo.</summary>
        private void VerdesOperacion(double p, DateTime tUtc, double volFirmado)
        {
            if (!FcVerdes || _vC == null || !ReferenceEquals(_vC.Dueno, this)) return;
            try { lock (_vC.Llave) _vC.N.Operacion(tUtc, p, volFirmado); }
            catch (Exception e) { Registrar(e); }
        }

        private void VerdesEscribir(VerdesNucleo.Suceso e)
        {
            try
            {
                double bid, ask; lock (_libroLlave) { bid = _lbPx; ask = _laPx; }
                string l = VerdesNucleo.AJson(e, "1.7", SondaInstrumento());
                l = l.Substring(0, l.Length - 1) + ",\"pc\":\"" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv) + "\",\"bid\":" + bid.ToString("0.00", Inv) + ",\"ask\":" + ask.ToString("0.00", Inv) + "}";
                var pth = Path.Combine(DirFlujo, "verdes-NQ-" + DateTime.UtcNow.ToString("yyyy-MM-dd", Inv) + ".jsonl");
                lock (_llaveArchivo) _colaArchivo = _colaArchivo.ContinueWith(_ => { try { Directory.CreateDirectory(DirFlujo); File.AppendAllText(pth, l + "\n"); } catch { } });
                if (e.Tipo == "gatillo" && e.Corrimiento == 0 && FcVerdesAlerta) Alertar("RECLAMO " + (e.Lado > 0 ? "comprador" : "vendedor") + " en la verde " + e.Raya.ToString("0.00", Es) + " (en sombra)");
            }
            catch (Exception ex) { Registrar(ex); }
        }

        /// <summary>Al cargar: los reclamos de hoy y de ayer vuelven al grafico con su resultado. Los que quedaron sin resultado van como 'desconocida' (3) y no cuentan.</summary>
        private void VerdesRestaurar(Compartido c)
        {
            try
            {
                var porId = new Dictionary<string, VerdesNucleo.Sombra>(); int n = 0;
                for (int atras = 1; atras >= 0; atras--)
                {
                    var pth = Path.Combine(DirFlujo, "verdes-NQ-" + DateTime.UtcNow.AddDays(-atras).ToString("yyyy-MM-dd", Inv) + ".jsonl");
                    if (!File.Exists(pth)) continue;
                    foreach (var l in File.ReadAllLines(pth))
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(l); var r = doc.RootElement; string ev = r.GetProperty("ev").GetString(), id = r.GetProperty("id").GetString() + "|" + r.GetProperty("corr").GetDouble().ToString("0.##", Inv);
                            if (ev == "gatillo")
                            {
                                var t = DateTime.ParseExact(r.GetProperty("utc").GetString(), "yyyy-MM-ddTHH:mm:ss.fff", Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                                porId[id] = new VerdesNucleo.Sombra { Id = r.GetProperty("id").GetString(), T = t, Lado = r.GetProperty("lado").GetInt32(), Entrada = r.GetProperty("entrada").GetDouble(), Stop = r.GetProperty("stop").GetDouble(),
                                    Objetivo = r.GetProperty("objetivo").GetDouble(), L = r.GetProperty("raya").GetDouble(), Corr = r.GetProperty("corr").GetDouble(), Traspaso = r.GetProperty("traspaso").GetDouble(),
                                    Rueda = r.GetProperty("rueda").GetBoolean(), Resultado = 3 };
                                n++;
                            }
                            else if (ev == "resultado" && porId.TryGetValue(id, out var s0)) { s0.Resultado = r.GetProperty("resultado").GetInt32(); s0.Puntos = r.GetProperty("puntos").GetDouble(); }
                        }
                        catch { }
                    }
                }
                lock (c.Llave) c.N.Sombras.InsertRange(0, porId.Values.OrderBy(s => s.T));
                if (n > 0) Log("verdes: " + n + " reclamos del registro vueltos al grafico");
            }
            catch (Exception e) { Registrar(e); }
        }

        /// <summary>Triangulo en la entrada de cada reclamo (blanco = abierta, verde = objetivo, rojo = stop, gris = vencida o desconocida). La vela sale de la HORA, no de un indice guardado.</summary>
        private void VerdesPintar(OFT.Rendering.Context.RenderContext g, Func<int, int> xDeBarra, Func<double, int> yDePrecio, Rectangle rp, int desde, int hasta, int xMax, Color cCompra, Color cVenta, Color cTxt)
        {
            if (!FcVerdes || !VerdesDisponible()) return;
            var c = VerdesCompartido(); List<VerdesNucleo.Sombra> ss;
            lock (c.Llave) ss = c.N.Sombras.Where(s => FcVerdesVerPlacebo || s.Corr == 0).ToList();
            if (ss.Count == 0) return;
            var bars = new int[ss.Count]; lock (_llave) for (int i = 0; i < ss.Count; i++) bars[i] = BarraDeAprox(ss[i].T);
            var cAb = De(FcColorReclamo);
            for (int i = 0; i < ss.Count; i++)
            {
                var s = ss[i]; int bar = bars[i]; if (bar < desde || bar > hasta) continue;
                int xc; try { xc = xDeBarra(bar); } catch { continue; }
                if (xc < rp.Left || xc > Math.Min(xMax, rp.Right)) continue;
                int y, yS, yO; try { y = yDePrecio(s.Entrada); yS = yDePrecio(s.Stop); yO = yDePrecio(s.Objetivo); } catch { continue; }
                if (y < rp.Top + 8 || y > rp.Bottom - 8) continue;
                var col = s.Resultado == 0 ? cAb : s.Resultado == 1 ? cCompra : s.Resultado == -1 ? cVenta : Alfa(cTxt, 150);
                if (s.Corr != 0) col = Alfa(col, 110);
                int t = s.Corr == 0 ? 6 : 4;
                var tri = s.Lado > 0 ? new[] { new Point(xc - t, y + t + 3), new Point(xc + t, y + t + 3), new Point(xc, y + 1) } : new[] { new Point(xc - t, y - t - 3), new Point(xc + t, y - t - 3), new Point(xc, y - 1) };
                g.FillPolygon(col, tri); if (s.Corr == 0) g.DrawPolygon(new RenderPen(Color.Black, 1f), tri);
                if (s.Resultado == 0 && s.Corr == 0)
                {
                    var pen = new RenderPen(Alfa(cAb, 150), 1f, System.Drawing.Drawing2D.DashStyle.Dot);
                    int a = Math.Max(rp.Top, Math.Min(rp.Bottom, yS)), b = Math.Max(rp.Top, Math.Min(rp.Bottom, yO));
                    g.DrawLine(pen, xc, a, xc + 26, a); g.DrawLine(pen, xc, b, xc + 26, b);
                }
            }
        }

        /// <summary>La vela que contiene esa hora (sin exigir que caiga dentro de su ultimo tick: sirve para marcas). Con _llave tomada.</summary>
        private int BarraDeAprox(DateTime t)
        {
            int lo = 0, hi = _t.Count - 1, r = -1;
            while (lo <= hi) { int mid = (lo + hi) / 2; if (_t[mid] <= t) { r = mid; lo = mid + 1; } else hi = mid - 1; }
            return r;
        }

        /// <summary>Para la fila AHORA: el estado del toque en curso y la cuenta en sombra de HOY, rueda y fuera de rueda por separado, contra sus rayas de control.</summary>
        private (string Estado, string Cuenta, int Signo) VerdesTexto()
        {
            if (!FcVerdes) return ("", "", 0);
            if (!VerdesDisponible()) return ("", "VERDES: solo en NQ/MNQ", 0);
            var c = VerdesCompartido();
            lock (c.Llave)
            {
                var n = c.N; var ahora = DateTime.UtcNow; const double costo = 0.96;
                if (!n.HayRayas(ahora)) return ("", "VERDES sin rayas frescas", 0);
                var hoy = n.Sombras.Where(s => (ahora - s.T).TotalHours < 14 && (s.Resultado == 1 || s.Resultado == -1 || s.Resultado == 2)).ToList();
                string Parte(string nombre, IEnumerable<VerdesNucleo.Sombra> q) { var l = q.ToList(); return l.Count == 0 ? "" : " " + nombre + " " + l.Count(s => s.Puntos > 0) + "/" + l.Count + " " + l.Sum(s => s.Puntos - costo).ToString("+0.0;-0.0", Es); }
                var reales = hoy.Where(s => s.Corr == 0).ToList(); int abiertas = n.Sombras.Count(s => s.Resultado == 0 && s.Corr == 0);
                string cuenta = "VERDES" + (reales.Count == 0 ? " 0" : Parte("rueda", reales.Where(s => s.Rueda)) + Parte("noche", reales.Where(s => !s.Rueda))) + (abiertas > 0 ? " (+" + abiertas + " abierta)" : "")
                              + Parte("| control", hoy.Where(s => s.Corr != 0 && s.Rueda)) + " (sombra)";
                double neto = reales.Where(s => s.Rueda).Sum(s => s.Puntos - costo);
                return (ahora <= n.EstadoHasta ? n.Estado : "", cuenta, Math.Sign(neto));
            }
        }
    }
}
