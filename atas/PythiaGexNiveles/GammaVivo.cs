using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGex
{
    /// <summary>
    /// PythiaGex - Gamma Vivo.
    ///
    /// EL PERFIL DE GAMMA, RECALCULADO CON EL PRECIO DE CADA TICK.
    ///
    /// Mirando los videos de GAMMAlito cuadro por cuadro medi que sus barras
    /// laterales se recalculan varias veces por segundo: en 4,2 s reales la
    /// barra de un strike pasa de 152 px a 110 px, bajando parejo. Parecia
    /// imposible de igualar sin comprar un feed de opciones en tiempo real.
    ///
    /// No lo es. La formula manda:
    ///
    ///     GEX(K) = Gamma(S, K, T, sigma) x OI(K) x 100 x S^2 x 0.01 x signo
    ///
    /// El interes abierto es de AYER -- para todos, incluido GEXbot: la OCC lo
    /// consolida de noche. Lo unico que se mueve tick a tick es S, el precio.
    /// Ese perfil que late en pantalla es la MISMA cadena vieja, repreciada
    /// contra el spot en vivo. La pagina de GEXbot lo dice sin querer cuando
    /// ofrece "real-time per second GEX calculation": calculo por segundo,
    /// sobre una cadena cuyo OI es de ayer.
    ///
    /// Y el precio en vivo ya lo tenemos: entra por Rithmic, pago.
    ///
    /// Asi que este indicador baja la cadena comprimida una vez cada tanto
    /// (strike, OI de call y de put, IV de cada lado, plazo) y hace la cuenta
    /// completa en cada barra, local, sin pedirle nada a nadie.
    ///
    /// QUE DIBUJA
    ///   izquierda   EXPOSICION GAMMA por strike, repreciada al precio de ahora
    ///   derecha     ACELERACION: cuanto cambia esa gamma si el precio se mueve
    ///               un 1 %. Es la "convexidad": no cuanta hay, sino que tan
    ///               rapido cambia.
    ///   lineas      Zero Gamma, Major Positive, Major Negative
    ///
    /// LO QUE SIGUE CON RETRASO
    /// El VOLUMEN por strike si cambia intradia y ese llega 15 minutos tarde.
    /// Por eso el perfil por volumen se dibuja aparte y marcado. El perfil por
    /// interes abierto -- el principal -- no pierde nada.
    /// </summary>
    [DisplayName("PythiaGex - Gamma Vivo")]
    [Category("PythiaGex")]
    public class GammaVivo : Indicator
    {
        // ==============================================================
        // Modelo: la cadena comprimida que baja del panel
        // ==============================================================
        private sealed class Fila
        {
            public double K;        // strike, en puntos del INDICE
            public int V;           // indice del vencimiento
            public double OiC, OiP; // interes abierto
            public double IvC, IvP; // volatilidad implicita
            public double VolC, VolP;
        }

        private sealed class Cadena
        {
            public string Ts = "";
            public double SpotIdx;
            public double[] Dias = Array.Empty<double>();
            public List<Fila> Filas = new();
            public double Base;
            public bool BaseConfiable;
            public string Contrato = "";
            public double EdadMin;
        }

        /// <summary>Lo que sale de repreciar: un renglon por strike.</summary>
        private struct Nivel
        {
            public double K;       // strike en indice
            public double Fut;     // el mismo strike en precio de futuro
            public double Gex;     // exposicion gamma, USD por 1 %
            public double Acel;    // cuanto cambia el GEX si S sube 1 %
        }

        // ==============================================================
        private static readonly HttpClient Http = CrearCliente();
        private static HttpClient CrearCliente()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            c.DefaultRequestHeaders.Add("User-Agent", "PythiaGex-GammaVivo/1.0");
            return c;
        }

        private volatile Cadena _c;
        private volatile string _error = "";
        private int _bajando;
        private TimeSpan _periodo;
        private Action _tick;

        // resultado del ultimo repricing
        private List<Nivel> _perfil = new();
        private double _zeroGamma, _netGex, _maxGex, _maxAcel;
        private double _majorPos, _majorNeg;
        private double _spotUsado = double.NaN;
        private readonly object _candado = new();

        // ==============================================================
        // Ajustes
        // ==============================================================
        [Display(Name = "Direccion del panel", GroupName = "Fuente", Order = 10)]
        public string Url { get; set; } = "https://waltermosqueda.github.io/PythiaGex/datos/atas/";

        [Display(Name = "Instrumento (vacio = automatico)", GroupName = "Fuente", Order = 20)]
        public string RaizManual { get; set; } = "";

        [Display(Name = "Rebajar la cadena cada (segundos)", GroupName = "Fuente", Order = 30)]
        public int SegundosRefresco { get; set; } = 300;

        [Display(Name = "Base manual (0 = la medida)", GroupName = "Fuente", Order = 40,
                 Description = "Puntos a sumarle al strike del indice para llevarlo a precio de futuro.")]
        public decimal BaseManual { get; set; } = 0m;

        [Display(Name = "Dias de vencimiento a incluir", GroupName = "Calculo", Order = 50)]
        public int DiasMax { get; set; } = 7;

        [Display(Name = "Tasa anual (para Black-Scholes)", GroupName = "Calculo", Order = 51)]
        public decimal Tasa { get; set; } = 0.0375m;

        [Display(Name = "Ver perfil de gamma (izquierda)", GroupName = "Dibujo", Order = 60)]
        public bool VerGamma { get; set; } = true;

        [Display(Name = "Ver aceleracion (derecha)", GroupName = "Dibujo", Order = 61)]
        public bool VerAcel { get; set; } = true;

        [Display(Name = "Ancho maximo de barra (px)", GroupName = "Dibujo", Order = 62)]
        public int AnchoBarra { get; set; } = 150;

        [Display(Name = "Alto de barra (px, 0 = automatico)", GroupName = "Dibujo", Order = 63)]
        public int AltoBarra { get; set; } = 0;

        [Display(Name = "Ver Zero Gamma y Majors", GroupName = "Dibujo", Order = 64)]
        public bool VerLineas { get; set; } = true;

        [Display(Name = "Margen del eje de precios (px)", GroupName = "Dibujo", Order = 65)]
        public int MargenEje { get; set; } = 62;

        [Display(Name = "Ver la cinta de estado", GroupName = "Dibujo", Order = 66)]
        public bool VerCinta { get; set; } = true;

        [Display(Name = "Gamma positiva", GroupName = "Colores", Order = 70)]
        public Color ColPos { get; set; } = Color.FromArgb(63, 191, 169);

        [Display(Name = "Gamma negativa", GroupName = "Colores", Order = 71)]
        public Color ColNeg { get; set; } = Color.FromArgb(232, 115, 74);

        [Display(Name = "Aceleracion positiva", GroupName = "Colores", Order = 72)]
        public Color ColAcelPos { get; set; } = Color.FromArgb(180, 120, 230);

        [Display(Name = "Aceleracion negativa", GroupName = "Colores", Order = 73)]
        public Color ColAcelNeg { get; set; } = Color.FromArgb(90, 200, 235);

        [Display(Name = "Zero Gamma", GroupName = "Colores", Order = 74)]
        public Color ColZero { get; set; } = Color.FromArgb(235, 235, 235);

        [Display(Name = "Texto", GroupName = "Colores", Order = 75)]
        public Color ColTexto { get; set; } = Color.FromArgb(225, 230, 238);

        [Display(Name = "Fondo de la cinta", GroupName = "Colores", Order = 76)]
        public Color ColFondo { get; set; } = Color.FromArgb(10, 14, 20);

        [Display(Name = "Aviso", GroupName = "Colores", Order = 77)]
        public Color ColAviso { get; set; } = Color.FromArgb(224, 163, 46);

        // ==============================================================
        public GammaVivo() : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
            DrawAbovePrice = false;
            if (DataSeries.Count > 0 && DataSeries[0] is ValueDataSeries v)
            {
                v.IsHidden = true;
                v.VisualType = VisualMode.Hide;
                v.ShowCurrentValue = false;
            }
        }

        protected override void OnInitialize()
        {
            _periodo = TimeSpan.FromSeconds(Math.Max(60, SegundosRefresco));
            _tick = () => _ = Bajar();
            SubscribeToTimer(_periodo, _tick);
            _ = Bajar();
        }

        protected override void OnDispose()
        {
            try { if (_tick != null) UnsubscribeFromTimer(_periodo, _tick); } catch { }
        }

        /// <summary>El repricing va aca, no en el timer: OnCalculate corre con
        /// cada barra nueva y con cada tick de la ultima, que es exactamente la
        /// cadencia a la que tiene que latir el perfil.</summary>
        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar != CurrentBar - 1) return;
            try { Repreciar(); } catch (Exception e) { Registrar(e); }
        }

        // ==============================================================
        // Descarga
        // ==============================================================
        private string Raiz()
        {
            if (!string.IsNullOrWhiteSpace(RaizManual)) return RaizManual.Trim().ToUpperInvariant();
            var s = (InstrumentInfo?.Instrument ?? "").ToUpperInvariant().TrimStart('#');
            if (s.StartsWith("MNQ") || s.StartsWith("NQ")) return "NQ";
            if (s.StartsWith("M2K") || s.StartsWith("RTY")) return "RTY";
            return "ES";
        }

        private async Task Bajar()
        {
            if (Interlocked.Exchange(ref _bajando, 1) == 1) return;
            try
            {
                var b = (Url ?? "").Trim();
                if (!b.EndsWith("/")) b += "/";
                var txt = await Http.GetStringAsync(
                    b + Raiz() + "_radar.json?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    .ConfigureAwait(false);
                var c = Parsear(txt);
                if (c != null && c.Filas.Count > 0) { _c = c; _error = ""; }
                else _error = "la cadena vino vacia";
            }
            catch (Exception e) { _error = Recortar(e.Message, 70); }
            finally
            {
                Interlocked.Exchange(ref _bajando, 0);
                try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            }
        }

        private static double? Num(JsonElement e, string k)
            => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number
               && v.TryGetDouble(out var d) ? d : (double?)null;

        private static string Txt(JsonElement e, string k)
            => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";

        private Cadena Parsear(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                if (!r.TryGetProperty("cadena", out var cd) || cd.ValueKind != JsonValueKind.Object)
                    return null;

                var c = new Cadena
                {
                    Ts = Txt(cd, "ts"),
                    SpotIdx = Num(cd, "spot_idx") ?? 0,
                    Base = Num(r, "base") ?? 0,
                    BaseConfiable = r.TryGetProperty("base_confiable", out var bc)
                                    && bc.ValueKind == JsonValueKind.True,
                    Contrato = Txt(r, "contrato"),
                    EdadMin = Num(r, "edad_min") ?? 0,
                };

                if (cd.TryGetProperty("vencimientos", out var vs) && vs.ValueKind == JsonValueKind.Array)
                    c.Dias = vs.EnumerateArray().Select(x => Num(x, "dias") ?? 0).ToArray();

                // filas posicionales:
                // [strike, venc, oi_call, oi_put, iv_call, iv_put, vol_call, vol_put]
                if (cd.TryGetProperty("filas", out var fs) && fs.ValueKind == JsonValueKind.Array)
                    foreach (var f in fs.EnumerateArray())
                    {
                        if (f.ValueKind != JsonValueKind.Array) continue;
                        var a = f.EnumerateArray().ToArray();
                        if (a.Length < 8) continue;
                        double G(int i) => a[i].TryGetDouble(out var d) ? d : 0;
                        c.Filas.Add(new Fila
                        {
                            K = G(0), V = (int)G(1),
                            OiC = G(2), OiP = G(3),
                            IvC = G(4), IvP = G(5),
                            VolC = G(6), VolP = G(7),
                        });
                    }
                return c;
            }
            catch { return null; }
        }

        // ==============================================================
        // La cuenta
        // ==============================================================
        private const double MULT = 100.0;   // multiplicador de opciones de indice

        private static double Fi(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);

        /// <summary>Gamma de Black-Scholes. Devuelve 0 si los datos no dan.</summary>
        private static double GammaBs(double S, double K, double T, double iv, double r)
        {
            if (S <= 0 || K <= 0 || T <= 0 || iv <= 0) return 0;
            var v = iv * Math.Sqrt(T);
            if (v <= 0) return 0;
            var d1 = (Math.Log(S / K) + (r + 0.5 * iv * iv) * T) / v;
            return Fi(d1) / (S * v);
        }

        /// <summary>GEX de un strike a un precio dado, sumando vencimientos.</summary>
        private double GexStrike(Fila f, double S, double T, double r)
        {
            var gC = GammaBs(S, f.K, T, f.IvC, r);
            var gP = GammaBs(S, f.K, T, f.IvP, r);
            // Convencion estandar de la industria: +1 para calls, -1 para puts.
            // Es una ASUNCION sobre de que lado quedo la mesa, no un dato
            // medido. Cuando el flujo dominante se da vuelta, el signo miente.
            return (gC * f.OiC - gP * f.OiP) * MULT * S * S * 0.01;
        }

        /// <summary>Reprecia toda la cadena contra el precio de ahora.
        ///
        /// LA ACELERACION SE MIDE, NO SE DERIVA A MANO. En vez de meter la
        /// griega de tercer orden (speed) con su formula y sus signos, se
        /// reprecia el mismo strike a S y a S x 1,01 y se resta. Es
        /// exactamente lo que la pregunta operativa quiere saber -- "cuanto
        /// cambia esta pared si el precio se mueve un uno por ciento" -- y no
        /// tiene margen para equivocarse en una derivada.
        /// </summary>
        private void Repreciar()
        {
            var c = _c;
            if (c == null || c.Filas.Count == 0) return;

            var baseUsada = BaseManual != 0m ? (double)BaseManual
                          : (c.BaseConfiable ? c.Base : double.NaN);

            decimal cierre;
            try { cierre = GetCandle(Math.Max(0, CurrentBar - 1)).Close; }
            catch { return; }
            if (cierre <= 0) return;

            // el precio del futuro se lleva a precio de indice para poder
            // compararlo con los strikes de la cadena
            double futuro = (double)cierre;
            double S = double.IsNaN(baseUsada) ? c.SpotIdx : futuro - baseUsada;
            if (S <= 0) return;

            double r = (double)Tasa;
            double Sup = S * 1.01;

            var perfil = new List<Nivel>(256);
            double neto = 0, mx = 0, mxA = 0;
            var porStrike = new Dictionary<double, Nivel>();

            foreach (var f in c.Filas)
            {
                if (f.V < 0 || f.V >= c.Dias.Length) continue;
                var dias = c.Dias[f.V];
                if (dias > DiasMax) continue;
                // El plazo nunca baja de media hora: con T tendiendo a cero la
                // gamma explota y un 0DTE a punto de liquidar se comeria todo
                // el mapa con un numero que no significa nada.
                var T = Math.Max(dias, 0.02) / 365.0;

                var g = GexStrike(f, S, T, r);
                var gUp = GexStrike(f, Sup, T, r);
                if (g == 0 && gUp == 0) continue;

                if (!porStrike.TryGetValue(f.K, out var n))
                    n = new Nivel { K = f.K, Gex = 0, Acel = 0 };
                n.Gex += g;
                n.Acel += (gUp - g);
                porStrike[f.K] = n;
            }

            foreach (var kv in porStrike)
            {
                var n = kv.Value;
                n.Fut = double.IsNaN(baseUsada) ? n.K : n.K + baseUsada;
                perfil.Add(n);
                neto += n.Gex;
                mx = Math.Max(mx, Math.Abs(n.Gex));
                mxA = Math.Max(mxA, Math.Abs(n.Acel));
            }
            perfil.Sort((a, b) => a.K.CompareTo(b.K));

            // Zero Gamma: el precio donde la suma de todos los strikes cruza
            // cero. No es el strike donde cambia el signo -- los signos saltan
            // entre vecinos. Hay que repreciar TODO a cada precio de la grilla
            // y volver a sumar.
            double zero = double.NaN;
            {
                double lo = S * 0.97, hi = S * 1.03;
                int pasos = 60;
                double ant = double.NaN, xAnt = 0;
                for (int i = 0; i <= pasos; i++)
                {
                    double x = lo + (hi - lo) * i / pasos;
                    double t = 0;
                    foreach (var f in c.Filas)
                    {
                        if (f.V < 0 || f.V >= c.Dias.Length) continue;
                        var dias = c.Dias[f.V];
                        if (dias > DiasMax) continue;
                        t += GexStrike(f, x, Math.Max(dias, 0.02) / 365.0, r);
                    }
                    if (!double.IsNaN(ant) && ((ant < 0 && t >= 0) || (ant > 0 && t <= 0)))
                    {
                        // interpolado: con 60 pasos sobre +/-3 % cada paso mide
                        // unos 7 puntos de indice, y devolver el punto de la
                        // grilla erraria casi dos ticks de ES
                        zero = (t != ant) ? xAnt + (x - xAnt) * (-ant) / (t - ant) : x;
                        break;
                    }
                    ant = t; xAnt = x;
                }
            }

            double mp = 0, mn = 0;
            if (perfil.Count > 0)
            {
                mp = perfil.Aggregate((a, b) => a.Gex >= b.Gex ? a : b).K;
                mn = perfil.Aggregate((a, b) => a.Gex <= b.Gex ? a : b).K;
            }

            lock (_candado)
            {
                _perfil = perfil;
                _netGex = neto; _maxGex = mx; _maxAcel = mxA;
                _zeroGamma = double.IsNaN(zero) ? double.NaN
                           : (double.IsNaN(baseUsada) ? zero : zero + baseUsada);
                _majorPos = double.IsNaN(baseUsada) ? mp : mp + baseUsada;
                _majorNeg = double.IsNaN(baseUsada) ? mn : mn + baseUsada;
                _spotUsado = S;
            }
        }

        // ==============================================================
        // Dibujo
        // ==============================================================
        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            try { Pintar(g); }
            catch (Exception e) { Registrar(e); }
        }

        private void Pintar(RenderContext g)
        {
            if (ChartInfo == null) return;
            var area = ChartArea;
            int x0 = area.Left, x1 = area.Right - Math.Max(0, MargenEje);

            List<Nivel> perfil; double mx, mxA, zero, mp, mn, neto, spot;
            lock (_candado)
            {
                perfil = _perfil; mx = _maxGex; mxA = _maxAcel;
                zero = _zeroGamma; mp = _majorPos; mn = _majorNeg;
                neto = _netGex; spot = _spotUsado;
            }

            if (VerCinta) Cinta(g, x0, area, perfil.Count, neto, spot);
            if (perfil.Count == 0 || mx <= 0) return;

            var cont = ChartInfo.PriceChartContainer;
            var c = _c;
            var baseUsada = BaseManual != 0m ? (double)BaseManual
                          : (c != null && c.BaseConfiable ? c.Base : double.NaN);

            // Si no hay base confiable no se dibuja NADA sobre el grafico. Un
            // nivel de SPX puesto crudo sobre el ES esta unos veinte puntos
            // corrido, y eso es una perdida sistematica en cada operacion.
            if (double.IsNaN(baseUsada)) return;

            int alto = AltoBarra > 0 ? AltoBarra : AltoAutomatico(cont, perfil);
            int ancho = Math.Max(20, AnchoBarra);

            foreach (var n in perfil)
            {
                int y;
                try { y = cont.GetYByPrice((decimal)n.Fut, false); }
                catch { continue; }
                if (y < area.Top - 6 || y > area.Bottom + 6) continue;

                if (VerGamma && Math.Abs(n.Gex) > 0)
                {
                    int w = Math.Max(1, (int)(Math.Abs(n.Gex) / mx * ancho));
                    var col = n.Gex >= 0 ? ColPos : ColNeg;
                    g.FillRectangle(Color.FromArgb(215, col),
                        new Rectangle(x0, y - alto / 2, w, alto));
                }
                if (VerAcel && mxA > 0 && Math.Abs(n.Acel) > 0)
                {
                    int w = Math.Max(1, (int)(Math.Abs(n.Acel) / mxA * ancho));
                    var col = n.Acel >= 0 ? ColAcelPos : ColAcelNeg;
                    g.FillRectangle(Color.FromArgb(215, col),
                        new Rectangle(x1 - w, y - alto / 2, w, alto));
                }
            }

            if (VerLineas)
            {
                Linea(g, cont, x0, x1, zero, ColZero, "ZERO GAMMA", true);
                Linea(g, cont, x0, x1, mp, ColPos, "MAJOR POSITIVE", false);
                Linea(g, cont, x0, x1, mn, ColNeg, "MAJOR NEGATIVE", false);
            }

            // rotulos de las dos mitades
            var f8 = new RenderFont("Arial", 8f);
            if (VerGamma)
                g.DrawString("EXPOSICION GAMMA", f8, Color.FromArgb(150, ColTexto),
                             x0 + 4, area.Top + 4);
            if (VerAcel)
            {
                var m = g.MeasureString("ACELERACION", f8);
                g.DrawString("ACELERACION", f8, Color.FromArgb(150, ColTexto),
                             x1 - m.Width - 4, area.Top + 4);
            }
        }

        private int AltoAutomatico(IChartContainer cont, List<Nivel> perfil)
        {
            // el alto sale de la separacion entre strikes vecinos: con el
            // grafico alejado las barras se pisan y el perfil se vuelve mancha
            try
            {
                if (perfil.Count < 2) return 5;
                var ks = perfil.Select(p => p.Fut).OrderBy(v => v).ToList();
                var difs = new List<double>();
                for (int i = 1; i < ks.Count; i++)
                    if (ks[i] - ks[i - 1] > 0) difs.Add(ks[i] - ks[i - 1]);
                if (difs.Count == 0) return 5;
                difs.Sort();
                var paso = difs[difs.Count / 2];
                var ya = cont.GetYByPrice((decimal)ks[0], false);
                var yb = cont.GetYByPrice((decimal)(ks[0] + paso), false);
                return Math.Max(2, Math.Min(16, (int)(Math.Abs(ya - yb) * 0.7)));
            }
            catch { return 5; }
        }

        private void Linea(RenderContext g, IChartContainer cont, int x0, int x1,
                           double precio, Color col, string nombre, bool grueso)
        {
            if (double.IsNaN(precio) || precio <= 0) return;
            int y;
            try { y = cont.GetYByPrice((decimal)precio, false); }
            catch { return; }
            if (y < ChartArea.Top || y > ChartArea.Bottom) return;
            g.DrawLine(new RenderPen(col, grueso ? 2f : 1.4f), x0, y, x1, y);
            var f = new RenderFont("Arial", 9f);
            var txt = nombre + "  " + precio.ToString("0.00", CultureInfo.InvariantCulture);
            var m = g.MeasureString(txt, f);
            g.FillRectangle(Color.FromArgb(190, ColFondo),
                new Rectangle(x1 - m.Width - 12, y - m.Height - 2, m.Width + 10, m.Height + 2));
            g.DrawString(txt, f, col, x1 - m.Width - 7, y - m.Height - 1);
        }

        private void Cinta(RenderContext g, int x0, Rectangle area, int nStrikes,
                           double neto, double spot)
        {
            var f = new RenderFont("Arial", 9.5f);
            var ls = new List<Tuple<string, Color>>();
            var c = _c;

            if (c == null)
            {
                ls.Add(Tuple.Create(string.IsNullOrEmpty(_error)
                    ? "bajando la cadena..." : "sin cadena: " + _error, ColAviso));
            }
            else
            {
                ls.Add(Tuple.Create(string.Format(CultureInfo.InvariantCulture,
                    "{0}  ·  {1} strikes repreciados con el precio de cada tick",
                    c.Contrato, nStrikes), ColTexto));
                ls.Add(Tuple.Create(string.Format(CultureInfo.InvariantCulture,
                    "net gex {0:+0.00;-0.00} B  ·  indice implicito {1:0.00}  ·  cadena de hace {2:0.#} min",
                    neto / 1e9, spot, c.EdadMin), ColTexto));
                if (!c.BaseConfiable && BaseManual == 0m)
                    ls.Add(Tuple.Create(
                        "la base indice->futuro no dio confiable: no se dibuja nada", ColAviso));
                ls.Add(Tuple.Create(
                    "el interes abierto es de ayer para todos; lo que late es el precio", ColAviso));
            }

            var med = ls.Select(l => g.MeasureString(l.Item1, f)).ToList();
            int w = 0, h = 8;
            foreach (var m in med) { w = Math.Max(w, m.Width); h += m.Height + 2; }
            var x = x0 + 6; var y = area.Bottom - h - 6;
            g.FillRectangle(Color.FromArgb(215, ColFondo), new Rectangle(x, y, w + 20, h));
            g.DrawRectangle(new RenderPen(Color.FromArgb(90, ColTexto), 1f),
                new Rectangle(x, y, w + 20, h));
            var yy = y + 4;
            for (int i = 0; i < ls.Count; i++)
            {
                g.DrawString(ls[i].Item1, f, ls[i].Item2, x + 9, yy);
                yy += med[i].Height + 2;
            }
        }

        private static string Recortar(string s, int n)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "...");

        private static void Registrar(Exception e)
        {
            try
            {
                var p = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ATAS", "pythiagex-gammavivo.log");
                File.AppendAllText(p, DateTime.Now.ToString("s") + "  " + e + "\n");
            }
            catch { }
        }
    }
}
