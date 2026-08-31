using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
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
    /// PythiaGex - Niveles Gamma.
    ///
    /// Dibuja sobre el grafico en tiempo real de ATAS los niveles de gamma que
    /// calcula el panel de PythiaGex, ya convertidos a precio de futuro.
    ///
    /// La division del trabajo es a proposito:
    ///   - el panel es el mapa: cadena de opciones, interes abierto, gamma
    ///   - ATAS es el reloj: precio en vivo por Rithmic
    ///
    /// El interes abierto es de ayer siempre, asi que los niveles no necesitan
    /// tiempo real. Lo que si necesita tiempo real es el precio, y eso ya lo
    /// tiene el grafico. Este indicador junta las dos mitades.
    ///
    /// Lo que se recalcula en vivo con cada tick, no viene del archivo:
    ///   - la distancia a cada nivel, en puntos y en ticks
    ///   - el GEX neto interpolado al precio actual
    ///   - cuantos contratos tiene que cubrir la mesa desde aca
    ///   - las alertas de proximidad
    /// </summary>
    [DisplayName("PythiaGex - Niveles Gamma")]
    [Category("PythiaGex")]
    public class NivelesGamma : Indicator
    {
        // ------------------------------------------------------------------
        // Modelo de lo que trae el archivo
        // ------------------------------------------------------------------
        private sealed class Nivel
        {
            public string Tipo, Nombre, Criollo, Alias;
            public double? Idx, Fut, GexM, Toque;
            public double? OiC, OiP;
            public bool Es0dte;
        }

        private sealed class Hueco
        {
            public double Desde, Hasta, DesdeFut, HastaFut, Ancho;
            public bool SobreSpot;
        }

        private sealed class Escalon
        {
            public double Fut, GexB;
            public double Contratos;
        }

        private sealed class Datos
        {
            public string Indice = "", Contrato = "", Micro = "", Regimen = "";
            public string CadenaTs = "";
            public int EdadMin;
            public bool CadenaVencida, CadenaMuyVencida, BaseConfiable, IndiceAtrasado;
            public double? Base, BaseErrorTicks, SpotIndice, SpotFuturo;
            public double? NetGexB, Gex0dteB, ExpectedMove, TasaCorta, DividendoImplicito;
            public double? CoberturaContratos, CoberturaMicro;
            public List<Nivel> Niveles = new();
            public List<Hueco> Huecos = new();
            public List<Escalon> Escalera = new();
            public Dictionary<string, double> Sesion = new();
            public DateTime Bajado = DateTime.UtcNow;
        }

        // ------------------------------------------------------------------
        // Estado
        // ------------------------------------------------------------------
        private static readonly HttpClient Http = CrearCliente();
        private volatile Datos _d;
        private volatile string _error = "";
        private int _bajando;
        private DateTime _ultimaAlerta = DateTime.MinValue;
        private readonly HashSet<string> _alertados = new();
        private TimeSpan _periodo = TimeSpan.FromSeconds(60);
        private Action _tick;

        private static HttpClient CrearCliente()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            c.DefaultRequestHeaders.Add("User-Agent", "PythiaGex-ATAS/1.0");
            return c;
        }

        // ------------------------------------------------------------------
        // Ajustes
        // ------------------------------------------------------------------
        [Display(Name = "Direccion del panel", GroupName = "Fuente", Order = 10)]
        public string Url { get; set; } =
            "https://waltermosqueda.github.io/PythiaGex/datos/atas/";

        [Display(Name = "Instrumento (vacio = automatico)", GroupName = "Fuente", Order = 20)]
        public string RaizManual { get; set; } = "";

        [Display(Name = "Refrescar cada (segundos)", GroupName = "Fuente", Order = 30)]
        public int SegundosRefresco { get; set; } = 60;

        [Display(Name = "Paredes de la cadena completa", GroupName = "Que dibujar", Order = 40)]
        public bool VerCadena { get; set; } = true;

        [Display(Name = "Paredes del vencimiento de hoy (0DTE)", GroupName = "Que dibujar", Order = 50)]
        public bool Ver0dte { get; set; } = true;

        [Display(Name = "Tramos sin gamma (gamma voids)", GroupName = "Que dibujar", Order = 60)]
        public bool VerHuecos { get; set; } = true;

        [Display(Name = "Banda de expected move", GroupName = "Que dibujar", Order = 70)]
        public bool VerExpectedMove { get; set; } = true;

        [Display(Name = "Referencias de sesion (open, maximo, minimo, IB)", GroupName = "Que dibujar", Order = 80)]
        public bool VerSesion { get; set; } = false;

        [Display(Name = "Tablero de estado", GroupName = "Que dibujar", Order = 90)]
        public bool VerTablero { get; set; } = true;

        [Display(Name = "Detalle en cada nivel", GroupName = "Que dibujar", Order = 100)]
        public bool VerDetalle { get; set; } = true;

        [Display(Name = "Avisar a (ticks del nivel, 0 = nunca)", GroupName = "Alertas", Order = 110)]
        public int AlertaTicks { get; set; } = 8;

        [Display(Name = "Sonido de la alerta", GroupName = "Alertas", Order = 120)]
        public string SonidoAlerta { get; set; } = "alert1";

        [Display(Name = "Techo (call wall)", GroupName = "Colores", Order = 130)]
        public Color ColTecho { get; set; } = Color.FromArgb(63, 191, 127);

        [Display(Name = "Piso (put wall)", GroupName = "Colores", Order = 140)]
        public Color ColPiso { get; set; } = Color.FromArgb(229, 72, 77);

        [Display(Name = "Iman (gamma pin)", GroupName = "Colores", Order = 150)]
        public Color ColIman { get; set; } = Color.FromArgb(232, 179, 60);

        [Display(Name = "Interruptor (zero gamma)", GroupName = "Colores", Order = 160)]
        public Color ColFlip { get; set; } = Color.FromArgb(170, 180, 195);

        [Display(Name = "Grosor de linea", GroupName = "Colores", Order = 170)]
        public int Grosor { get; set; } = 2;

        // ------------------------------------------------------------------
        public NivelesGamma() : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
            DrawAbovePrice = false;
            ((ValueDataSeries)DataSeries[0]).IsHidden = true;
            ((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
        }

        protected override void OnInitialize()
        {
            _periodo = TimeSpan.FromSeconds(Math.Max(15, SegundosRefresco));
            _tick = () => _ = Bajar();
            SubscribeToTimer(_periodo, _tick);
            _ = Bajar();
        }

        protected override void OnDispose()
        {
            try { if (_tick != null) UnsubscribeFromTimer(_periodo, _tick); } catch { }
        }

        protected override void OnCalculate(int bar, decimal value) { }

        // ------------------------------------------------------------------
        // Descarga
        // ------------------------------------------------------------------
        private string Raiz()
        {
            if (!string.IsNullOrWhiteSpace(RaizManual))
                return RaizManual.Trim().ToUpperInvariant();

            var s = (InstrumentInfo?.Instrument ?? "").ToUpperInvariant().TrimStart('#');
            // MES y ES cotizan el mismo precio, igual con MNQ/NQ y M2K/RTY
            if (s.StartsWith("MNQ") || s.StartsWith("NQ")) return "NQ";
            if (s.StartsWith("M2K") || s.StartsWith("RTY")) return "RTY";
            return "ES";
        }

        private async Task Bajar()
        {
            if (Interlocked.Exchange(ref _bajando, 1) == 1) return;
            try
            {
                var baseUrl = (Url ?? "").Trim();
                if (!baseUrl.EndsWith("/")) baseUrl += "/";
                var url = baseUrl + Raiz() + ".json?t="
                        + DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                var txt = await Http.GetStringAsync(url).ConfigureAwait(false);
                var d = Parsear(txt);
                if (d != null) { _d = d; _error = ""; _alertados.Clear(); }
                else _error = "el archivo no se pudo interpretar";
            }
            catch (Exception e)
            {
                _error = Recortar(e.Message, 70);
            }
            finally
            {
                Interlocked.Exchange(ref _bajando, 0);
                try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            }
        }

        private static double? Num(JsonElement e, string k)
        {
            if (!e.TryGetProperty(k, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return d;
            return null;
        }

        private static string Txt(JsonElement e, string k)
            => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String
               ? v.GetString() : "";

        private static bool Bol(JsonElement e, string k)
            => e.TryGetProperty(k, out var v)
               && (v.ValueKind == JsonValueKind.True
                   || (v.ValueKind == JsonValueKind.Number && v.GetDouble() != 0));

        private static Datos Parsear(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                var d = new Datos
                {
                    Indice = Txt(r, "indice"),
                    Contrato = Txt(r, "contrato"),
                    Micro = Txt(r, "micro"),
                    Regimen = Txt(r, "regimen"),
                    CadenaTs = Txt(r, "cadena_ts"),
                    EdadMin = (int)(Num(r, "cadena_edad_min") ?? 0),
                    CadenaVencida = Bol(r, "cadena_vencida"),
                    CadenaMuyVencida = Bol(r, "cadena_muy_vencida"),
                    BaseConfiable = Bol(r, "base_confiable"),
                    IndiceAtrasado = Bol(r, "indice_atrasado"),
                    Base = Num(r, "base"),
                    BaseErrorTicks = Num(r, "base_error_ticks"),
                    SpotIndice = Num(r, "spot_indice"),
                    SpotFuturo = Num(r, "spot_futuro"),
                    NetGexB = Num(r, "net_gex_B"),
                    Gex0dteB = Num(r, "gex_0dte_B"),
                    ExpectedMove = Num(r, "expected_move"),
                    TasaCorta = Num(r, "tasa_corta"),
                    DividendoImplicito = Num(r, "dividendo_implicito"),
                    CoberturaContratos = Num(r, "cobertura_contratos"),
                    CoberturaMicro = Num(r, "cobertura_micro"),
                };

                if (r.TryGetProperty("niveles", out var ns) && ns.ValueKind == JsonValueKind.Array)
                    foreach (var n in ns.EnumerateArray())
                        d.Niveles.Add(new Nivel
                        {
                            Tipo = Txt(n, "tipo"),
                            Nombre = Txt(n, "nombre"),
                            Criollo = Txt(n, "criollo"),
                            Alias = Txt(n, "alias"),
                            Idx = Num(n, "idx"),
                            Fut = Num(n, "fut"),
                            GexM = Num(n, "gex_M"),
                            OiC = Num(n, "oi_c"),
                            OiP = Num(n, "oi_p"),
                            Toque = Num(n, "toque"),
                            Es0dte = Bol(n, "es0dte"),
                        });

                if (r.TryGetProperty("huecos", out var hs) && hs.ValueKind == JsonValueKind.Array)
                    foreach (var h in hs.EnumerateArray())
                        d.Huecos.Add(new Hueco
                        {
                            Desde = Num(h, "desde") ?? 0,
                            Hasta = Num(h, "hasta") ?? 0,
                            DesdeFut = Num(h, "desde_fut") ?? 0,
                            HastaFut = Num(h, "hasta_fut") ?? 0,
                            Ancho = Num(h, "ancho") ?? 0,
                            SobreSpot = Bol(h, "sobre_spot"),
                        });

                if (r.TryGetProperty("escalera", out var es) && es.ValueKind == JsonValueKind.Array)
                    foreach (var e in es.EnumerateArray())
                    {
                        var f = Num(e, "fut");
                        if (f == null) continue;
                        d.Escalera.Add(new Escalon
                        {
                            Fut = f.Value,
                            GexB = Num(e, "gex_B") ?? 0,
                            Contratos = Num(e, "contratos") ?? 0,
                        });
                    }
                d.Escalera = d.Escalera.OrderBy(x => x.Fut).ToList();

                if (r.TryGetProperty("sesion", out var se) && se.ValueKind == JsonValueKind.Object)
                    foreach (var p in se.EnumerateObject())
                        if (p.Value.ValueKind == JsonValueKind.Number)
                            d.Sesion[p.Name] = p.Value.GetDouble();

                return d;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------------
        // Dibujo
        // ------------------------------------------------------------------
        /// <summary>Las alertas de ATAS usan el Color de WPF; el dibujo usa el de
        /// System.Drawing. Son dos tipos distintos con el mismo nombre.</summary>
        private static System.Windows.Media.Color Wpf(Color c)
            => System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);

        private static string Recortar(string s, int n)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "...");

        private static string Mag(double? v)
        {
            if (v == null) return "";
            var a = Math.Abs(v.Value);
            if (a >= 1000) return (v.Value / 1000).ToString("0.00", CultureInfo.InvariantCulture) + "B";
            return Math.Round(v.Value).ToString("0", CultureInfo.InvariantCulture) + "M";
        }

        private static string Oi(double? v)
        {
            if (v == null) return "";
            var a = Math.Abs(v.Value);
            if (a >= 1000) return (v.Value / 1000).ToString("0.0", CultureInfo.InvariantCulture) + "k";
            return Math.Round(v.Value).ToString("0", CultureInfo.InvariantCulture);
        }

        private static string Miles(double v)
            => Math.Round(v).ToString("#,0", CultureInfo.InvariantCulture);

        private Color ColorDe(Nivel n)
        {
            switch (n.Tipo)
            {
                case "call_wall":
                case "major_positive": return ColTecho;
                case "put_wall":
                case "major_negative": return ColPiso;
                case "gamma_pin": return ColIman;
                default: return ColFlip;
            }
        }

        /// <summary>GEX neto interpolado al precio actual, no al de la cadena.</summary>
        private (double gexB, double contratos)? EnVivo(Datos d, double precio)
        {
            var e = d.Escalera;
            if (e == null || e.Count < 2) return null;
            if (precio <= e[0].Fut) return (e[0].GexB, e[0].Contratos);
            if (precio >= e[^1].Fut) return (e[^1].GexB, e[^1].Contratos);
            for (int i = 0; i < e.Count - 1; i++)
            {
                if (precio >= e[i].Fut && precio <= e[i + 1].Fut)
                {
                    var t = (precio - e[i].Fut) / (e[i + 1].Fut - e[i].Fut);
                    return (e[i].GexB + t * (e[i + 1].GexB - e[i].GexB),
                            e[i].Contratos + t * (e[i + 1].Contratos - e[i].Contratos));
                }
            }
            return null;
        }

        private double PrecioActual()
        {
            try
            {
                var c = GetCandle(Math.Max(0, CurrentBar - 1));
                return (double)c.Close;
            }
            catch { return 0; }
        }

        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            if (ChartInfo == null) return;

            var area = ChartArea;
            var d = _d;

            var fuente = new RenderFont("Consolas", 10f);
            var fuenteBold = new RenderFont("Consolas", 10f, FontStyle.Bold);
            var fuenteChica = new RenderFont("Consolas", 9f);

            if (d == null)
            {
                var msg = string.IsNullOrEmpty(_error)
                    ? "PythiaGex: bajando niveles..."
                    : "PythiaGex: no pude bajar los niveles - " + _error;
                g.DrawString(msg, fuenteBold,
                    string.IsNullOrEmpty(_error) ? Color.Gray : ColPiso,
                    area.Left + 8, area.Top + 8);
                return;
            }

            var precio = PrecioActual();
            var ts = InstrumentInfo?.TickSize ?? 0m;
            var tick = (double)(ts == 0 ? 0.25m : ts);
            var cont = ChartInfo.PriceChartContainer;
            var hi = (double)cont.High;
            var lo = (double)cont.Low;

            // --- banda de expected move -----------------------------------
            if (VerExpectedMove && d.ExpectedMove is > 0 && d.SpotFuturo is > 0)
            {
                var s = d.SpotFuturo.Value;
                for (int k = 2; k >= 1; k--)
                {
                    var em = d.ExpectedMove.Value * k;
                    var yA = cont.GetYByPrice((decimal)(s + em), false);
                    var yB = cont.GetYByPrice((decimal)(s - em), false);
                    var top = Math.Min(yA, yB);
                    var alto = Math.Abs(yB - yA);
                    if (alto <= 0) continue;
                    var rec = Rectangle.Intersect(area, new Rectangle(area.Left, top, area.Width, alto));
                    if (rec.Width > 0 && rec.Height > 0)
                        g.FillRectangle(Color.FromArgb(k == 1 ? 16 : 9, ColIman), rec);
                }
            }

            // --- tramos sin gamma -----------------------------------------
            if (VerHuecos)
            {
                foreach (var h in d.Huecos)
                {
                    if (h.DesdeFut <= 0 || h.HastaFut <= 0) continue;
                    if (h.HastaFut < lo || h.DesdeFut > hi) continue;
                    var yA = cont.GetYByPrice((decimal)h.HastaFut, false);
                    var yB = cont.GetYByPrice((decimal)h.DesdeFut, false);
                    var rec = Rectangle.Intersect(area,
                        new Rectangle(area.Left, Math.Min(yA, yB), area.Width, Math.Abs(yB - yA)));
                    if (rec.Width <= 0 || rec.Height <= 0) continue;
                    g.FillRectangle(Color.FromArgb(14, ColIman), rec);
                    g.DrawString("GAMMA VOID  " + h.Ancho.ToString("0", CultureInfo.InvariantCulture)
                        + " pts sin freno", fuenteChica, Color.FromArgb(150, ColIman),
                        area.Right - 190, rec.Top + 2);
                }
            }

            // --- referencias de sesion ------------------------------------
            if (VerSesion)
            {
                var refs = new (string clave, string etq)[]
                {
                    ("apertura_fut", "OPEN"), ("maximo_fut", "HOD"), ("minimo_fut", "LOD"),
                    ("ib_alto_fut", "IB HIGH"), ("ib_bajo_fut", "IB LOW"),
                };
                var lapiz = new RenderPen(Color.FromArgb(110, 150, 160, 175), 1, DashStyle.Dot);
                foreach (var (clave, etq) in refs)
                {
                    if (!d.Sesion.TryGetValue(clave, out var p) || p < lo || p > hi) continue;
                    var y = cont.GetYByPrice((decimal)p, false);
                    g.DrawLine(lapiz, area.Left, y, area.Right, y);
                    g.DrawString(etq, fuenteChica, Color.FromArgb(150, 150, 160, 175),
                        area.Left + 4, y - 12);
                }
            }

            // --- los niveles ----------------------------------------------
            var usados = new List<int>();
            foreach (var n in d.Niveles.OrderByDescending(x => x.Fut ?? 0))
            {
                if (n.Es0dte && !Ver0dte) continue;
                if (!n.Es0dte && !VerCadena) continue;
                var p = n.Fut ?? n.Idx;
                if (p == null) continue;
                if (p < lo || p > hi) continue;

                var y = cont.GetYByPrice((decimal)p.Value, false);
                var col = ColorDe(n);
                var ancho = n.Es0dte ? Math.Max(1, Grosor - 1) : Grosor;
                var estilo = n.Es0dte ? DashStyle.Dot : DashStyle.Dash;
                var lapiz = new RenderPen(Color.FromArgb(n.Es0dte ? 170 : 230, col), ancho, estilo);
                g.DrawLine(lapiz, area.Left, y, area.Right, y);

                // etiqueta con todos los numeros, y la distancia recalculada en vivo
                var titulo = n.Nombre.ToUpperInvariant() + "  "
                           + p.Value.ToString("0.00", CultureInfo.InvariantCulture);
                if (!d.BaseConfiable) titulo += " *";

                var partes = new List<string>();
                if (n.GexM != null) partes.Add(Mag(n.GexM) + " gamma");
                if (n.OiC != null) partes.Add("OI " + Oi(n.OiC) + "C/" + Oi(n.OiP) + "P");
                if (n.Toque != null)
                    partes.Add("toque " + n.Toque.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%");
                if (precio > 0)
                {
                    var dp = p.Value - precio;
                    var dt = (int)Math.Round(dp / tick);
                    partes.Add((dp >= 0 ? "+" : "") + dp.ToString("0.0", CultureInfo.InvariantCulture)
                               + " pt / " + (dt >= 0 ? "+" : "") + dt + " tk");
                }
                if (n.Idx != null)
                    partes.Add(d.Indice + " " + n.Idx.Value.ToString("0.##", CultureInfo.InvariantCulture));
                var detalle = string.Join("  .  ", partes);

                var yl = y - 15;
                while (usados.Any(u => Math.Abs(u - yl) < 26)) yl += 26;
                usados.Add(yl);

                var tam = g.MeasureString(titulo, fuenteBold);
                var tam2 = VerDetalle ? g.MeasureString(detalle, fuenteChica) : new Size(0, 0);
                var w = Math.Max(tam.Width, tam2.Width) + 10;
                var h2 = tam.Height + (VerDetalle ? tam2.Height + 2 : 0) + 6;
                var caja = new Rectangle(area.Left + 4, yl - 2, w, h2);
                g.FillRectangle(Color.FromArgb(185, 10, 14, 20), caja);
                g.DrawRectangle(new RenderPen(Color.FromArgb(120, col), 1), caja);
                g.DrawString(titulo, fuenteBold, col, caja.Left + 5, caja.Top + 2);
                if (VerDetalle)
                    g.DrawString(detalle, fuenteChica, Color.FromArgb(200, 190, 200, 210),
                        caja.Left + 5, caja.Top + 2 + tam.Height);

                // alerta de proximidad
                if (AlertaTicks > 0 && precio > 0 && !n.Es0dte)
                {
                    var ticks = Math.Abs(p.Value - precio) / tick;
                    var clave = n.Tipo + p.Value.ToString("0.00", CultureInfo.InvariantCulture);
                    if (ticks <= AlertaTicks && !_alertados.Contains(clave)
                        && (DateTime.UtcNow - _ultimaAlerta).TotalSeconds > 20)
                    {
                        _alertados.Add(clave);
                        _ultimaAlerta = DateTime.UtcNow;
                        try
                        {
                            AddAlert(SonidoAlerta, InstrumentInfo?.Instrument ?? "",
                                n.Nombre + " " + p.Value.ToString("0.00", CultureInfo.InvariantCulture)
                                + " a " + Math.Round(ticks) + " ticks",
                                Wpf(Color.FromArgb(30, 30, 30)), Wpf(col));
                        }
                        catch { }
                    }
                    else if (ticks > AlertaTicks * 2) _alertados.Remove(clave);
                }
            }

            // --- tablero de estado ----------------------------------------
            if (VerTablero) DibujarTablero(g, area, d, precio, tick, fuente, fuenteBold, fuenteChica);
        }

        private void DibujarTablero(RenderContext g, Rectangle area, Datos d, double precio,
                                    double tick, RenderFont f, RenderFont fb, RenderFont fc)
        {
            var lineas = new List<(string txt, Color col, bool bold)>();

            var vivo = precio > 0 ? EnVivo(d, precio) : null;
            var gexB = vivo?.gexB ?? d.NetGexB ?? 0;
            var contratos = vivo?.contratos ?? d.CoberturaContratos ?? 0;
            var positivo = gexB >= 0;

            lineas.Add((d.Indice + "  ->  " + d.Contrato
                        + (string.IsNullOrEmpty(d.Micro) ? "" : " / " + d.Micro),
                        Color.FromArgb(235, 235, 240), true));

            lineas.Add(((positivo ? "LONG GAMMA - amortigua" : "SHORT GAMMA - amplifica")
                        + "   " + gexB.ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + " B",
                        positivo ? ColTecho : ColPiso, true));

            lineas.Add(("Cobertura por 1%: " + Miles(Math.Abs(contratos)) + " "
                        + (d.Contrato.Length >= 2 ? d.Contrato.Substring(0, d.Contrato.Length - 2) : "ES")
                        + "  (" + (positivo ? "compra en la baja" : "vende en la baja") + ")",
                        positivo ? ColTecho : ColPiso, false));

            if (vivo != null)
                lineas.Add(("   interpolado al precio de ahora, no al de la cadena",
                            Color.FromArgb(150, 160, 175), false));

            if (d.Gex0dteB != null)
                lineas.Add(("GEX del vencimiento de hoy: "
                            + d.Gex0dteB.Value.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) + " B",
                            d.Gex0dteB >= 0 ? ColTecho : ColPiso, false));

            if (d.ExpectedMove is > 0 && d.SpotFuturo is > 0)
                lineas.Add(("Expected move +/-" + d.ExpectedMove.Value.ToString("0.#", CultureInfo.InvariantCulture)
                            + "   " + (d.SpotFuturo.Value - d.ExpectedMove.Value).ToString("0", CultureInfo.InvariantCulture)
                            + " - " + (d.SpotFuturo.Value + d.ExpectedMove.Value).ToString("0", CultureInfo.InvariantCulture),
                            Color.FromArgb(200, 205, 215), false));

            if (d.Base != null)
                lineas.Add(("Base " + d.Contrato + " - " + d.Indice + " "
                            + d.Base.Value.ToString("+0.00;-0.00", CultureInfo.InvariantCulture)
                            + (d.BaseConfiable
                               ? "  (firme)"
                               : "  (FLOJA: " + (d.BaseErrorTicks ?? 0).ToString("0.#", CultureInfo.InvariantCulture)
                                 + " ticks de error - no dibujes estos niveles)"),
                            d.BaseConfiable ? Color.FromArgb(170, 180, 195) : ColPiso, !d.BaseConfiable));

            if (d.TasaCorta != null)
                lineas.Add(("Tasa " + (d.TasaCorta.Value * 100).ToString("0.00", CultureInfo.InvariantCulture)
                            + "%  dividendo implicito "
                            + ((d.DividendoImplicito ?? 0) * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%",
                            Color.FromArgb(130, 140, 155), false));

            var edad = d.EdadMin;
            var colEdad = d.CadenaMuyVencida ? ColPiso
                        : d.CadenaVencida ? ColIman
                        : Color.FromArgb(140, 150, 165);
            lineas.Add(("Cadena CBOE " + d.CadenaTs + "  (" + edad + " min)", colEdad, d.CadenaVencida));

            if (d.CadenaMuyVencida)
                lineas.Add(("CBOE no refresca hace " + (edad / 60) + " horas. NO OPERES CON ESTO.",
                            ColPiso, true));
            else if (d.CadenaVencida)
                lineas.Add(("Sirve para ubicar niveles, no para cronometrar la entrada.",
                            ColIman, false));

            if (d.IndiceAtrasado)
                lineas.Add(("El " + d.Indice + " publicado esta congelado; se usa el contado implicito "
                            + (d.SpotIndice ?? 0).ToString("0.00", CultureInfo.InvariantCulture),
                            Color.FromArgb(150, 160, 175), false));

            if (!string.IsNullOrEmpty(_error))
                lineas.Add(("Ultima descarga fallo: " + _error, ColPiso, false));

            // caja
            int w = 0, h = 6;
            foreach (var (txt, _, bold) in lineas)
            {
                var t = g.MeasureString(txt, bold ? fb : fc);
                w = Math.Max(w, t.Width);
                h += t.Height + 2;
            }
            w += 16;
            var caja = new Rectangle(area.Right - w - 10, area.Top + 8, w, h);
            g.FillRectangle(Color.FromArgb(215, 8, 12, 18), caja);
            g.DrawRectangle(new RenderPen(Color.FromArgb(90, 120, 130, 145), 1), caja);

            var y = caja.Top + 4;
            foreach (var (txt, col, bold) in lineas)
            {
                var ff = bold ? fb : fc;
                g.DrawString(txt, ff, col, caja.Left + 8, y);
                y += g.MeasureString(txt, ff).Height + 2;
            }
        }
    }
}
