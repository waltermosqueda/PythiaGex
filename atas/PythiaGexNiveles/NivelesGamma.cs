using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
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
    /// Junta dos mitades que por separado no alcanzan:
    ///
    ///   1. Los niveles de gamma calculados sobre la cadena de opciones, con
    ///      todo el protocolo de verificacion encima, que llegan por HTTP.
    ///   2. El order flow en vivo de Rithmic, que ya esta adentro de ATAS.
    ///
    /// La cadena dice DONDE esta la pared. El footprint dice QUIEN esta
    /// ganando ahi. Un Call Wall con delta comprador fuerte se esta rompiendo;
    /// el mismo Call Wall con volumen alto y delta chico se esta defendiendo.
    /// Esa diferencia no la da ningun tablero de GEX, y sale del dato que ya
    /// se paga con la licencia de ATAS.
    /// </summary>
    [DisplayName("PythiaGex - Niveles Gamma")]
    [Category("PythiaGex")]
    public class NivelesGamma : Indicator
    {
        // ==================================================================
        // Modelo del archivo
        // ==================================================================
        private sealed class Nivel
        {
            public string Tipo, Nombre, Criollo, Alias;
            public double? Idx, Fut, GexM, Toque, OiC, OiP;
            public bool Es0dte;
            public int Puntaje;
            public string Razones = "";
            public Contexto.Zona Flujo;
        }

        private sealed class Hueco
        {
            public double DesdeFut, HastaFut, Ancho;
            public bool SobreSpot;
        }

        private sealed class Escalon { public double Fut, GexB, Contratos; }

        private sealed class Datos
        {
            public string Indice = "", Contrato = "", Micro = "", Regimen = "", CadenaTs = "";
            public int EdadMin;
            public bool CadenaVencida, CadenaMuyVencida, BaseConfiable, IndiceAtrasado;
            public double? Base, BaseErrorTicks, SpotIndice, SpotFuturo;
            public double? NetGexB, Gex0dteB, ExpectedMove, TasaCorta, DividendoImplicito;
            public double? CoberturaContratos;
            public List<Nivel> Niveles = new();
            public List<Hueco> Huecos = new();
            public List<Escalon> Escalera = new();
            public Dictionary<string, double> Sesion = new();
        }

        // ==================================================================
        // Estado
        // ==================================================================
        private static readonly HttpClient Http = CrearCliente();
        private volatile Datos _d;
        private volatile string _error = "";
        private int _bajando;
        private readonly Contexto _ctx = new();
        private readonly Stopwatch _relojCtx = Stopwatch.StartNew();
        private int _ultimaBarraCtx = -1;
        private DateTime _ultimaAlerta = DateTime.MinValue;
        private readonly HashSet<string> _alertados = new();
        private TimeSpan _periodo = TimeSpan.FromSeconds(60);
        private Action _tick;
        private string _opcionesAtas = "sin probar";

        private static HttpClient CrearCliente()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            c.DefaultRequestHeaders.Add("User-Agent", "PythiaGex-ATAS/2.0");
            return c;
        }

        // ==================================================================
        // Ajustes - Fuente
        // ==================================================================
        [Display(Name = "Direccion del panel", GroupName = "Fuente", Order = 10)]
        public string Url { get; set; } = "https://waltermosqueda.github.io/PythiaGex/datos/atas/";

        [Display(Name = "Instrumento (vacio = automatico)", GroupName = "Fuente", Order = 20)]
        public string RaizManual { get; set; } = "";

        [Display(Name = "Refrescar cada (segundos)", GroupName = "Fuente", Order = 30)]
        public int SegundosRefresco { get; set; } = 60;

        // ==================================================================
        // Ajustes - Niveles de gamma
        // ==================================================================
        [Display(Name = "Paredes de la cadena completa", GroupName = "Niveles de gamma", Order = 40)]
        public bool VerCadena { get; set; } = true;

        [Display(Name = "Paredes del vencimiento de hoy (0DTE)", GroupName = "Niveles de gamma", Order = 41)]
        public bool Ver0dte { get; set; } = true;

        [Display(Name = "Zero gamma / flip", GroupName = "Niveles de gamma", Order = 42)]
        public bool VerFlip { get; set; } = true;

        [Display(Name = "Major positive / negative", GroupName = "Niveles de gamma", Order = 43)]
        public bool VerMajor { get; set; } = true;

        [Display(Name = "Tramos sin gamma (gamma voids)", GroupName = "Niveles de gamma", Order = 44)]
        public bool VerHuecos { get; set; } = true;

        [Display(Name = "Banda de expected move", GroupName = "Niveles de gamma", Order = 45)]
        public bool VerExpectedMove { get; set; } = true;

        // ==================================================================
        // Ajustes - Contexto de ATAS
        // ==================================================================
        [Display(Name = "Perfil de volumen (histograma)", GroupName = "Contexto de ATAS", Order = 50)]
        public LadoPerfil Perfil { get; set; } = LadoPerfil.Izquierda;

        [Display(Name = "Alcance del perfil", GroupName = "Contexto de ATAS", Order = 51)]
        public AlcancePerfil Alcance { get; set; } = AlcancePerfil.Sesion;

        [Display(Name = "Barras (si el alcance es fijo)", GroupName = "Contexto de ATAS", Order = 52)]
        public int BarrasPerfil { get; set; } = 300;

        [Display(Name = "Ancho del histograma (px)", GroupName = "Contexto de ATAS", Order = 53)]
        public int AnchoPerfil { get; set; } = 90;

        [Display(Name = "POC, VAH y VAL", GroupName = "Contexto de ATAS", Order = 54)]
        public bool VerPoc { get; set; } = true;

        [Display(Name = "Nodos de alto volumen", GroupName = "Contexto de ATAS", Order = 55)]
        public bool VerHvn { get; set; } = true;

        [Display(Name = "VWAP de la sesion", GroupName = "Contexto de ATAS", Order = 56)]
        public bool VerVwap { get; set; } = true;

        [Display(Name = "Bandas de desvio del VWAP", GroupName = "Contexto de ATAS", Order = 57)]
        public bool VerBandas { get; set; } = true;

        [Display(Name = "Referencias de sesion (open, HOD, LOD, IB)", GroupName = "Contexto de ATAS", Order = 58)]
        public bool VerSesion { get; set; } = true;

        // ==================================================================
        // Ajustes - Confluencia y order flow
        // ==================================================================
        [Display(Name = "Puntuar confluencia", GroupName = "Confluencia", Order = 60)]
        public bool VerConfluencia { get; set; } = true;

        [Display(Name = "Tolerancia (ticks)", GroupName = "Confluencia", Order = 61)]
        public int ToleranciaTicks { get; set; } = 8;

        [Display(Name = "Resaltar desde puntaje", GroupName = "Confluencia", Order = 62)]
        public int PuntajeResaltar { get; set; } = 2;

        [Display(Name = "Volumen y delta en cada nivel", GroupName = "Confluencia", Order = 63)]
        public bool VerFlujo { get; set; } = true;

        [Display(Name = "Ancho de la zona del nivel (ticks)", GroupName = "Confluencia", Order = 64)]
        public int TicksZona { get; set; } = 6;

        // ==================================================================
        // Ajustes - Estilo
        // ==================================================================
        [Display(Name = "Tipografia", GroupName = "Estilo", Order = 70)]
        public string Tipografia { get; set; } = "Consolas";

        [Display(Name = "Tamano del titulo", GroupName = "Estilo", Order = 71)]
        public float TamTitulo { get; set; } = 10f;

        [Display(Name = "Tamano del detalle", GroupName = "Estilo", Order = 72)]
        public float TamDetalle { get; set; } = 9f;

        [Display(Name = "Titulo en negrita", GroupName = "Estilo", Order = 73)]
        public bool TituloNegrita { get; set; } = true;

        [Display(Name = "Lado de la etiqueta", GroupName = "Estilo", Order = 74)]
        public LadoEtiqueta LadoTexto { get; set; } = LadoEtiqueta.Izquierda;

        [Display(Name = "Mostrar el detalle del nivel", GroupName = "Estilo", Order = 75)]
        public bool VerDetalle { get; set; } = true;

        [Display(Name = "Caja detras de la etiqueta", GroupName = "Estilo", Order = 76)]
        public bool CajaEtiqueta { get; set; } = true;

        [Display(Name = "Opacidad de la caja (0-255)", GroupName = "Estilo", Order = 77)]
        public int OpacidadCaja { get; set; } = 185;

        [Display(Name = "Grosor de las paredes", GroupName = "Estilo", Order = 78)]
        public int GrosorPared { get; set; } = 2;

        [Display(Name = "Estilo de las paredes", GroupName = "Estilo", Order = 79)]
        public TipoLinea LineaPared { get; set; } = TipoLinea.Cortada;

        [Display(Name = "Grosor del 0DTE", GroupName = "Estilo", Order = 80)]
        public int Grosor0dte { get; set; } = 1;

        [Display(Name = "Estilo del 0DTE", GroupName = "Estilo", Order = 81)]
        public TipoLinea Linea0dte { get; set; } = TipoLinea.Punteada;

        [Display(Name = "Estilo del contexto (VWAP, POC)", GroupName = "Estilo", Order = 82)]
        public TipoLinea LineaContexto { get; set; } = TipoLinea.Continua;

        [Display(Name = "Grosor del contexto", GroupName = "Estilo", Order = 83)]
        public int GrosorContexto { get; set; } = 1;

        [Display(Name = "Extender lineas a todo el ancho", GroupName = "Estilo", Order = 84)]
        public bool LineaCompleta { get; set; } = true;

        // ==================================================================
        // Ajustes - Tablero
        // ==================================================================
        [Display(Name = "Mostrar tablero", GroupName = "Tablero", Order = 90)]
        public bool VerTablero { get; set; } = true;

        [Display(Name = "Esquina", GroupName = "Tablero", Order = 91)]
        public Esquina EsquinaTablero { get; set; } = Esquina.ArribaDerecha;

        [Display(Name = "Tamano de letra", GroupName = "Tablero", Order = 92)]
        public float TamTablero { get; set; } = 9f;

        [Display(Name = "Opacidad del fondo (0-255)", GroupName = "Tablero", Order = 93)]
        public int OpacidadTablero { get; set; } = 215;

        [Display(Name = "Incluir contexto de ATAS", GroupName = "Tablero", Order = 94)]
        public bool TableroContexto { get; set; } = true;

        [Display(Name = "Incluir diagnostico", GroupName = "Tablero", Order = 95)]
        public bool TableroDiagnostico { get; set; } = false;

        // ==================================================================
        // Ajustes - Alertas
        // ==================================================================
        [Display(Name = "Avisar a (ticks del nivel, 0 = nunca)", GroupName = "Alertas", Order = 100)]
        public int AlertaTicks { get; set; } = 8;

        [Display(Name = "Solo niveles con confluencia", GroupName = "Alertas", Order = 101)]
        public bool AlertaSoloConfluencia { get; set; } = false;

        [Display(Name = "Sonido", GroupName = "Alertas", Order = 102)]
        public string SonidoAlerta { get; set; } = "alert1";

        // ==================================================================
        // Ajustes - Colores
        // ==================================================================
        [Display(Name = "Techo (call wall)", GroupName = "Colores", Order = 110)]
        public Color ColTecho { get; set; } = Color.FromArgb(63, 191, 127);

        [Display(Name = "Piso (put wall)", GroupName = "Colores", Order = 111)]
        public Color ColPiso { get; set; } = Color.FromArgb(229, 72, 77);

        [Display(Name = "Iman (gamma pin)", GroupName = "Colores", Order = 112)]
        public Color ColIman { get; set; } = Color.FromArgb(232, 179, 60);

        [Display(Name = "Interruptor (zero gamma)", GroupName = "Colores", Order = 113)]
        public Color ColFlip { get; set; } = Color.FromArgb(170, 180, 195);

        [Display(Name = "POC", GroupName = "Colores", Order = 114)]
        public Color ColPoc { get; set; } = Color.FromArgb(255, 140, 60);

        [Display(Name = "Area de valor", GroupName = "Colores", Order = 115)]
        public Color ColVa { get; set; } = Color.FromArgb(120, 130, 150);

        [Display(Name = "VWAP", GroupName = "Colores", Order = 116)]
        public Color ColVwap { get; set; } = Color.FromArgb(90, 160, 255);

        [Display(Name = "Perfil de volumen", GroupName = "Colores", Order = 117)]
        public Color ColPerfil { get; set; } = Color.FromArgb(70, 110, 160);

        [Display(Name = "Texto del tablero", GroupName = "Colores", Order = 118)]
        public Color ColTexto { get; set; } = Color.FromArgb(225, 230, 238);

        [Display(Name = "Fondo del tablero", GroupName = "Colores", Order = 119)]
        public Color ColFondo { get; set; } = Color.FromArgb(8, 12, 18);

        // ==================================================================
        public NivelesGamma() : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
            DrawAbovePrice = false;
            if (DataSeries.Count > 0 && DataSeries[0] is ValueDataSeries v)
            {
                v.IsHidden = true;
                v.VisualType = VisualMode.Hide;
            }
        }

        protected override void OnInitialize()
        {
            _periodo = TimeSpan.FromSeconds(Math.Max(15, SegundosRefresco));
            _tick = () => _ = Bajar();
            SubscribeToTimer(_periodo, _tick);
            _ = Bajar();
            ProbarOpcionesAtas();
        }

        protected override void OnDispose()
        {
            try { if (_tick != null) UnsubscribeFromTimer(_periodo, _tick); } catch { }
        }

        protected override void OnCalculate(int bar, decimal value) { }

        /// <summary>
        /// Pregunta si un indicador puede llegar al feed de opciones de ATAS.
        /// Si se pudiera, el GEX se calcularia sobre las opciones de ES en
        /// tiempo real, sin CBOE y sin conversion de base. El resultado se
        /// muestra en el tablero para dejarlo documentado en pantalla.
        /// </summary>
        private void ProbarOpcionesAtas()
        {
            try
            {
                var t = Type.GetType("ATAS.DataFeedsCore.IOptionsDataFeed, ATAS.DataFeedsCore");
                if (t == null) { _opcionesAtas = "no existe el tipo"; return; }
                var mi = typeof(IIndicatorDataProvider).GetMethod("GetService");
                if (mi == null) { _opcionesAtas = "sin GetService"; return; }
                var obj = mi.MakeGenericMethod(t).Invoke(DataProvider, null);
                _opcionesAtas = obj != null
                    ? "SI, alcanzable: " + obj.GetType().Name
                    : "el servicio no esta registrado para indicadores";
            }
            catch (Exception e) { _opcionesAtas = "falla: " + Recortar(e.Message, 50); }
        }

        // ==================================================================
        // Descarga
        // ==================================================================
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
                    b + Raiz() + ".json?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    .ConfigureAwait(false);
                var d = Parsear(txt);
                if (d != null) { _d = d; _error = ""; _alertados.Clear(); }
                else _error = "el archivo no se pudo interpretar";
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

        private static bool Bol(JsonElement e, string k)
            => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;

        private static Datos Parsear(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                var d = new Datos
                {
                    Indice = Txt(r, "indice"), Contrato = Txt(r, "contrato"),
                    Micro = Txt(r, "micro"), Regimen = Txt(r, "regimen"),
                    CadenaTs = Txt(r, "cadena_ts"),
                    EdadMin = (int)(Num(r, "cadena_edad_min") ?? 0),
                    CadenaVencida = Bol(r, "cadena_vencida"),
                    CadenaMuyVencida = Bol(r, "cadena_muy_vencida"),
                    BaseConfiable = Bol(r, "base_confiable"),
                    IndiceAtrasado = Bol(r, "indice_atrasado"),
                    Base = Num(r, "base"), BaseErrorTicks = Num(r, "base_error_ticks"),
                    SpotIndice = Num(r, "spot_indice"), SpotFuturo = Num(r, "spot_futuro"),
                    NetGexB = Num(r, "net_gex_B"), Gex0dteB = Num(r, "gex_0dte_B"),
                    ExpectedMove = Num(r, "expected_move"), TasaCorta = Num(r, "tasa_corta"),
                    DividendoImplicito = Num(r, "dividendo_implicito"),
                    CoberturaContratos = Num(r, "cobertura_contratos"),
                };
                if (r.TryGetProperty("niveles", out var ns) && ns.ValueKind == JsonValueKind.Array)
                    foreach (var n in ns.EnumerateArray())
                        d.Niveles.Add(new Nivel
                        {
                            Tipo = Txt(n, "tipo"), Nombre = Txt(n, "nombre"),
                            Criollo = Txt(n, "criollo"), Alias = Txt(n, "alias"),
                            Idx = Num(n, "idx"), Fut = Num(n, "fut"), GexM = Num(n, "gex_M"),
                            OiC = Num(n, "oi_c"), OiP = Num(n, "oi_p"), Toque = Num(n, "toque"),
                            Es0dte = Bol(n, "es0dte"),
                        });
                if (r.TryGetProperty("huecos", out var hs) && hs.ValueKind == JsonValueKind.Array)
                    foreach (var h in hs.EnumerateArray())
                        d.Huecos.Add(new Hueco
                        {
                            DesdeFut = Num(h, "desde_fut") ?? 0, HastaFut = Num(h, "hasta_fut") ?? 0,
                            Ancho = Num(h, "ancho") ?? 0, SobreSpot = Bol(h, "sobre_spot"),
                        });
                if (r.TryGetProperty("escalera", out var es) && es.ValueKind == JsonValueKind.Array)
                    foreach (var e in es.EnumerateArray())
                    {
                        var f = Num(e, "fut"); if (f == null) continue;
                        d.Escalera.Add(new Escalon
                        {
                            Fut = f.Value, GexB = Num(e, "gex_B") ?? 0,
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

        // ==================================================================
        // Utilidades
        // ==================================================================
        private static System.Windows.Media.Color Wpf(Color c)
            => System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);

        private static string Recortar(string s, int n)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "...");

        private static string Mag(double? v)
        {
            if (v == null) return "";
            var a = Math.Abs(v.Value);
            return a >= 1000 ? (v.Value / 1000).ToString("0.00", CultureInfo.InvariantCulture) + "B"
                             : Math.Round(v.Value).ToString("0", CultureInfo.InvariantCulture) + "M";
        }

        private static string Kilo(decimal v)
        {
            var a = Math.Abs(v);
            if (a >= 1_000_000) return (v / 1_000_000m).ToString("0.0", CultureInfo.InvariantCulture) + "M";
            if (a >= 1000) return (v / 1000m).ToString("0.0", CultureInfo.InvariantCulture) + "k";
            return Math.Round(v).ToString("0", CultureInfo.InvariantCulture);
        }

        private static string Oi(double? v)
        {
            if (v == null) return "";
            return Math.Abs(v.Value) >= 1000
                ? (v.Value / 1000).ToString("0.0", CultureInfo.InvariantCulture) + "k"
                : Math.Round(v.Value).ToString("0", CultureInfo.InvariantCulture);
        }

        private static string Miles(double v) => Math.Round(v).ToString("#,0", CultureInfo.InvariantCulture);

        private static DashStyle Dash(TipoLinea t)
        {
            switch (t)
            {
                case TipoLinea.Continua: return DashStyle.Solid;
                case TipoLinea.Punteada: return DashStyle.Dot;
                case TipoLinea.RayaPunto: return DashStyle.DashDot;
                default: return DashStyle.Dash;
            }
        }

        private RenderFont Fuente(float tam, bool negrita)
            => new RenderFont(string.IsNullOrWhiteSpace(Tipografia) ? "Consolas" : Tipografia,
                              Math.Max(6f, tam), negrita ? FontStyle.Bold : FontStyle.Regular);

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

        private bool Mostrar(Nivel n)
        {
            if (n.Es0dte) return Ver0dte;
            switch (n.Tipo)
            {
                case "gamma_flip": return VerFlip;
                case "major_positive":
                case "major_negative": return VerMajor;
                default: return VerCadena;
            }
        }

        private (double gexB, double contratos)? EnVivo(Datos d, double precio)
        {
            var e = d.Escalera;
            if (e == null || e.Count < 2) return null;
            if (precio <= e[0].Fut) return (e[0].GexB, e[0].Contratos);
            if (precio >= e[e.Count - 1].Fut) return (e[e.Count - 1].GexB, e[e.Count - 1].Contratos);
            for (int i = 0; i < e.Count - 1; i++)
                if (precio >= e[i].Fut && precio <= e[i + 1].Fut)
                {
                    var t = (precio - e[i].Fut) / (e[i + 1].Fut - e[i].Fut);
                    return (e[i].GexB + t * (e[i + 1].GexB - e[i].GexB),
                            e[i].Contratos + t * (e[i + 1].Contratos - e[i].Contratos));
                }
            return null;
        }

        private decimal PrecioActual()
        {
            try { return GetCandle(Math.Max(0, CurrentBar - 1)).Close; } catch { return 0m; }
        }

        // ==================================================================
        // Contexto: se recalcula en barra nueva, no en cada tick
        // ==================================================================
        private void ActualizarContexto()
        {
            if (CurrentBar < 2) return;
            var ultima = CurrentBar - 1;
            if (ultima == _ultimaBarraCtx && _relojCtx.ElapsedMilliseconds < 5000) return;
            _ultimaBarraCtx = ultima;
            _relojCtx.Restart();

            int desde;
            if (Alcance == AlcancePerfil.Visibles)
                desde = Math.Max(0, FirstVisibleBarNumber);
            else if (Alcance == AlcancePerfil.Fijo)
                desde = Math.Max(0, ultima - Math.Max(10, BarrasPerfil));
            else
            {
                desde = 0;
                for (int b = ultima; b > 0; b--)
                    if (IsNewSession(b)) { desde = b; break; }
            }
            var ts = InstrumentInfo != null ? InstrumentInfo.TickSize : 0.25m;
            try { _ctx.Calcular(GetCandle, desde, ultima, ts); } catch { }
        }

        // ==================================================================
        // Confluencia
        // ==================================================================
        private void PuntuarNiveles(Datos d, decimal tick)
        {
            if (d == null) return;
            var tol = tick * Math.Max(1, ToleranciaTicks);
            foreach (var n in d.Niveles)
            {
                n.Puntaje = 0; n.Razones = "";
                var p = (decimal)(n.Fut ?? n.Idx ?? 0);
                if (p <= 0) continue;
                var razones = new List<string>();

                if (_ctx.Listo)
                {
                    if (VerPoc && Math.Abs(p - _ctx.Poc) <= tol) { n.Puntaje++; razones.Add("POC"); }
                    if (VerPoc && (Math.Abs(p - _ctx.Vah) <= tol || Math.Abs(p - _ctx.Val) <= tol))
                    { n.Puntaje++; razones.Add("area de valor"); }
                    if (VerHvn && _ctx.NodosAltos.Any(h => Math.Abs(p - h) <= tol))
                    { n.Puntaje++; razones.Add("nodo alto"); }
                    if (VerVwap && _ctx.Vwap > 0 && Math.Abs(p - _ctx.Vwap) <= tol)
                    { n.Puntaje++; razones.Add("VWAP"); }
                    if (VerBandas && _ctx.Sigma > 0 &&
                        new[] { _ctx.VwapMas1, _ctx.VwapMenos1, _ctx.VwapMas2, _ctx.VwapMenos2 }
                        .Any(x => Math.Abs(p - x) <= tol))
                    { n.Puntaje++; razones.Add("banda VWAP"); }
                    if (VerSesion &&
                        new[] { _ctx.Apertura, _ctx.Maximo, _ctx.Minimo, _ctx.IbAlto, _ctx.IbBajo }
                        .Any(x => x > 0 && Math.Abs(p - x) <= tol))
                    { n.Puntaje++; razones.Add("referencia de sesion"); }
                }

                // una pared del 0DTE encima de una de la cadena completa pesa doble
                if (d.Niveles.Any(o => !ReferenceEquals(o, n) && o.Es0dte != n.Es0dte
                                       && Math.Abs((decimal)(o.Fut ?? 0) - p) <= tol))
                { n.Puntaje++; razones.Add(n.Es0dte ? "coincide con la cadena" : "coincide con 0DTE"); }

                if (VerFlujo && _ctx.Listo)
                {
                    n.Flujo = _ctx.EnNivel(p, tick, Math.Max(1, TicksZona));
                    if (n.Flujo.Absorcion) { n.Puntaje++; razones.Add("absorcion"); }
                }
                n.Razones = string.Join(" + ", razones);
            }
        }

        // ==================================================================
        // Dibujo
        // ==================================================================
        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            if (ChartInfo == null) return;
            var area = ChartArea;
            var d = _d;

            var fTitulo = Fuente(TamTitulo, TituloNegrita);
            var fDetalle = Fuente(TamDetalle, false);
            var fTab = Fuente(TamTablero, false);
            var fTabB = Fuente(TamTablero, true);

            ActualizarContexto();

            var tick = InstrumentInfo != null ? InstrumentInfo.TickSize : 0.25m;
            if (tick <= 0) tick = 0.25m;
            var precio = PrecioActual();
            var cont = ChartInfo.PriceChartContainer;
            var hi = cont.High; var lo = cont.Low;
            int x0 = area.Left, x1 = area.Right;

            if (d != null) PuntuarNiveles(d, tick);

            if (Perfil != LadoPerfil.Apagado && _ctx.Listo && _ctx.Nodos.Count > 0)
                DibujarPerfil(g, area, cont, hi, lo);

            if (d != null && VerExpectedMove && d.ExpectedMove.HasValue && d.ExpectedMove.Value > 0
                && d.SpotFuturo.HasValue && d.SpotFuturo.Value > 0)
            {
                var s = (decimal)d.SpotFuturo.Value;
                for (int k = 2; k >= 1; k--)
                {
                    var em = (decimal)(d.ExpectedMove.Value * k);
                    var yA = cont.GetYByPrice(s + em, false);
                    var yB = cont.GetYByPrice(s - em, false);
                    var rec = Rectangle.Intersect(area,
                        new Rectangle(x0, Math.Min(yA, yB), area.Width, Math.Abs(yB - yA)));
                    if (rec.Width > 0 && rec.Height > 0)
                        g.FillRectangle(Color.FromArgb(k == 1 ? 16 : 9, ColIman), rec);
                }
            }

            if (d != null && VerHuecos)
                foreach (var h in d.Huecos)
                {
                    if (h.DesdeFut <= 0 || h.HastaFut <= 0) continue;
                    var a = (decimal)h.HastaFut; var b = (decimal)h.DesdeFut;
                    if (b > hi || a < lo) continue;
                    var yA = cont.GetYByPrice(a, false); var yB = cont.GetYByPrice(b, false);
                    var rec = Rectangle.Intersect(area,
                        new Rectangle(x0, Math.Min(yA, yB), area.Width, Math.Abs(yB - yA)));
                    if (rec.Width <= 0 || rec.Height <= 0) continue;
                    g.FillRectangle(Color.FromArgb(14, ColIman), rec);
                    g.DrawString("GAMMA VOID " + h.Ancho.ToString("0", CultureInfo.InvariantCulture) + " pts",
                        Fuente(TamDetalle - 1, false), Color.FromArgb(150, ColIman), x1 - 150, rec.Top + 2);
                }

            if (_ctx.Listo) DibujarContexto(g, area, cont, hi, lo);

            if (d == null)
            {
                var msg = string.IsNullOrEmpty(_error)
                    ? "PythiaGex: bajando niveles..."
                    : "PythiaGex: no pude bajar los niveles - " + _error;
                g.DrawString(msg, fTitulo, string.IsNullOrEmpty(_error) ? Color.Gray : ColPiso,
                             x0 + 8, area.Top + 8);
                return;
            }

            var usados = new List<int>();
            foreach (var n in d.Niveles.OrderByDescending(x => x.Fut ?? 0))
            {
                if (!Mostrar(n)) continue;
                var pv = n.Fut ?? n.Idx; if (pv == null) continue;
                var p = (decimal)pv.Value;
                if (p < lo || p > hi) continue;

                var y = cont.GetYByPrice(p, false);
                var col = ColorDe(n);
                var destaca = VerConfluencia && n.Puntaje >= Math.Max(1, PuntajeResaltar);
                var grosor = n.Es0dte ? Grosor0dte : GrosorPared;
                if (destaca) grosor += 1;
                var pen = new RenderPen(Color.FromArgb(n.Es0dte ? 175 : 235, col),
                                        Math.Max(1, grosor), Dash(n.Es0dte ? Linea0dte : LineaPared));
                var xa = LineaCompleta ? x0 : x0 + area.Width / 3;
                g.DrawLine(pen, xa, y, x1, y);

                if (LadoTexto == LadoEtiqueta.Ninguna) { EvaluarAlerta(n, p, precio, tick, col); continue; }

                var titulo = n.Nombre.ToUpperInvariant() + "  "
                           + p.ToString("0.00", CultureInfo.InvariantCulture)
                           + (d.BaseConfiable ? "" : " *")
                           + (destaca ? "   [ x" + n.Puntaje + " ]" : "");

                var partes = new List<string>();
                if (n.GexM != null) partes.Add(Mag(n.GexM) + " gamma");
                if (n.OiC != null) partes.Add("OI " + Oi(n.OiC) + "C/" + Oi(n.OiP) + "P");
                if (n.Toque != null) partes.Add("toque " + n.Toque.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%");
                if (precio > 0)
                {
                    var dp = p - precio;
                    partes.Add((dp >= 0 ? "+" : "") + dp.ToString("0.0", CultureInfo.InvariantCulture)
                               + " pt / " + (dp >= 0 ? "+" : "") + Contexto.Ticks(p, precio, tick) + " tk");
                }
                if (VerFlujo && n.Flujo != null && n.Flujo.Volumen > 0)
                    partes.Add("vol " + Kilo(n.Flujo.Volumen)
                               + " (" + n.Flujo.PctVolumenSesion.ToString("0.#", CultureInfo.InvariantCulture) + "%)"
                               + "  delta " + (n.Flujo.Delta >= 0 ? "+" : "") + Kilo(n.Flujo.Delta)
                               + (n.Flujo.Absorcion ? "  ABSORCION" : ""));
                if (!string.IsNullOrEmpty(n.Razones)) partes.Add(n.Razones);
                if (n.Idx != null) partes.Add(d.Indice + " " + n.Idx.Value.ToString("0.##", CultureInfo.InvariantCulture));
                var detalle = string.Join("  .  ", partes);

                var salto = Math.Max(14, (int)(TamTitulo * 2.6f));
                var yl = y - 15;
                while (usados.Any(u => Math.Abs(u - yl) < salto)) yl += salto;
                usados.Add(yl);

                var t1 = g.MeasureString(titulo, fTitulo);
                var t2 = VerDetalle ? g.MeasureString(detalle, fDetalle) : new Size(0, 0);
                var w = Math.Max(t1.Width, t2.Width) + 10;
                var h2 = t1.Height + (VerDetalle ? t2.Height + 2 : 0) + 6;

                int lx;
                if (LadoTexto == LadoEtiqueta.Derecha) lx = x1 - w - 6;
                else if (LadoTexto == LadoEtiqueta.SigueAlPrecio)
                    lx = Math.Max(x0 + 4, Math.Min(x1 - w - 6,
                         cont.GetXByBar(Math.Max(0, CurrentBar - 1), false) - w - 10));
                else lx = x0 + 4;

                var caja = new Rectangle(lx, yl - 2, w, h2);
                if (CajaEtiqueta)
                {
                    g.FillRectangle(Color.FromArgb(Math.Max(0, Math.Min(255, OpacidadCaja)), ColFondo), caja);
                    g.DrawRectangle(new RenderPen(Color.FromArgb(destaca ? 200 : 120, col),
                                                  destaca ? 2 : 1), caja);
                }
                g.DrawString(titulo, fTitulo, col, caja.Left + 5, caja.Top + 2);
                if (VerDetalle)
                    g.DrawString(detalle, fDetalle, Color.FromArgb(205, ColTexto),
                                 caja.Left + 5, caja.Top + 2 + t1.Height);

                EvaluarAlerta(n, p, precio, tick, col);
            }

            if (VerTablero) DibujarTablero(g, area, d, precio, tick, fTab, fTabB);
        }

        private void DibujarContexto(RenderContext g, Rectangle area, IChartContainer cont,
                                     decimal hi, decimal lo)
        {
            int x0 = area.Left, x1 = area.Right;
            void Linea(decimal p, Color col, string etq, bool fuerte)
            {
                if (p <= 0 || p < lo || p > hi) return;
                var y = cont.GetYByPrice(p, false);
                var pen = new RenderPen(Color.FromArgb(fuerte ? 210 : 130, col),
                                        Math.Max(1, fuerte ? GrosorContexto + 1 : GrosorContexto),
                                        Dash(LineaContexto));
                g.DrawLine(pen, x0, y, x1, y);
                var f = Fuente(TamDetalle - 0.5f, false);
                var t = etq + "  " + p.ToString("0.00", CultureInfo.InvariantCulture);
                var w = g.MeasureString(t, f).Width;
                g.DrawString(t, f, Color.FromArgb(200, col), x1 - w - 6, y - 13);
            }

            if (VerPoc)
            {
                Linea(_ctx.Poc, ColPoc, "POC", true);
                Linea(_ctx.Vah, ColVa, "VAH", false);
                Linea(_ctx.Val, ColVa, "VAL", false);
            }
            if (VerVwap) Linea(_ctx.Vwap, ColVwap, "VWAP", true);
            if (VerBandas && _ctx.Sigma > 0)
            {
                Linea(_ctx.VwapMas1, ColVwap, "VWAP +1s", false);
                Linea(_ctx.VwapMenos1, ColVwap, "VWAP -1s", false);
                Linea(_ctx.VwapMas2, ColVwap, "VWAP +2s", false);
                Linea(_ctx.VwapMenos2, ColVwap, "VWAP -2s", false);
            }
            if (VerSesion)
            {
                var gris = Color.FromArgb(150, 160, 175);
                Linea(_ctx.Apertura, gris, "OPEN", false);
                Linea(_ctx.Maximo, gris, "HOD", false);
                Linea(_ctx.Minimo, gris, "LOD", false);
                Linea(_ctx.IbAlto, gris, "IB HIGH", false);
                Linea(_ctx.IbBajo, gris, "IB LOW", false);
            }
            if (VerHvn)
                foreach (var h in _ctx.NodosAltos)
                {
                    if (h < lo || h > hi) continue;
                    var y = cont.GetYByPrice(h, false);
                    g.DrawLine(new RenderPen(Color.FromArgb(70, ColPerfil), 1, DashStyle.Dot), x0, y, x1, y);
                }
        }

        private void DibujarPerfil(RenderContext g, Rectangle area, IChartContainer cont,
                                   decimal hi, decimal lo)
        {
            var visibles = _ctx.Nodos.Where(n => n.Precio >= lo && n.Precio <= hi).ToList();
            if (visibles.Count == 0) return;
            var max = visibles.Max(n => n.Volumen);
            if (max <= 0) return;
            var alto = Math.Max(1, (int)Math.Round((double)cont.PriceRowHeight));
            var ancho = Math.Max(20, AnchoPerfil);
            foreach (var n in visibles)
            {
                var y = cont.GetYByPrice(n.Precio, false);
                var w = (int)Math.Round((double)(n.Volumen / max) * ancho);
                if (w <= 0) continue;
                var esPoc = n.Precio == _ctx.Poc;
                var enVa = n.Precio >= _ctx.Val && n.Precio <= _ctx.Vah;
                var col = esPoc ? Color.FromArgb(190, ColPoc)
                        : enVa ? Color.FromArgb(120, ColPerfil)
                               : Color.FromArgb(65, ColPerfil);
                var x = Perfil == LadoPerfil.Izquierda ? area.Left : area.Right - w;
                g.FillRectangle(col, new Rectangle(x, y - alto / 2, w, alto));
            }
        }

        private void EvaluarAlerta(Nivel n, decimal p, decimal precio, decimal tick, Color col)
        {
            if (AlertaTicks <= 0 || precio <= 0 || n.Es0dte) return;
            if (AlertaSoloConfluencia && n.Puntaje < Math.Max(1, PuntajeResaltar)) return;
            var ticks = Math.Abs(p - precio) / tick;
            var clave = n.Tipo + p.ToString("0.00", CultureInfo.InvariantCulture);
            if (ticks <= AlertaTicks && !_alertados.Contains(clave)
                && (DateTime.UtcNow - _ultimaAlerta).TotalSeconds > 20)
            {
                _alertados.Add(clave);
                _ultimaAlerta = DateTime.UtcNow;
                var extra = string.IsNullOrEmpty(n.Razones) ? "" : " (" + n.Razones + ")";
                try
                {
                    AddAlert(SonidoAlerta, InstrumentInfo != null ? InstrumentInfo.Instrument : "",
                        n.Nombre + " " + p.ToString("0.00", CultureInfo.InvariantCulture)
                        + " a " + Math.Round(ticks) + " ticks" + extra,
                        Wpf(Color.FromArgb(30, 30, 30)), Wpf(col));
                }
                catch { }
            }
            else if (ticks > AlertaTicks * 2) _alertados.Remove(clave);
        }

        private void DibujarTablero(RenderContext g, Rectangle area, Datos d, decimal precio,
                                    decimal tick, RenderFont f, RenderFont fb)
        {
            var L = new List<Tuple<string, Color, bool>>();
            void Add(string t, Color c, bool b) => L.Add(Tuple.Create(t, c, b));

            var vivo = precio > 0 ? EnVivo(d, (double)precio) : null;
            var gexB = vivo.HasValue ? vivo.Value.gexB : (d.NetGexB ?? 0);
            var contratos = vivo.HasValue ? vivo.Value.contratos : (d.CoberturaContratos ?? 0);
            var pos = gexB >= 0;
            var raiz = d.Contrato.Length >= 2 ? d.Contrato.Substring(0, d.Contrato.Length - 2) : "ES";

            Add(d.Indice + "  ->  " + d.Contrato + (d.Micro == "" ? "" : " / " + d.Micro), ColTexto, true);
            Add((pos ? "LONG GAMMA - amortigua" : "SHORT GAMMA - amplifica") + "   "
                + gexB.ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + " B",
                pos ? ColTecho : ColPiso, true);
            Add("Cobertura por 1%: " + Miles(Math.Abs(contratos)) + " " + raiz
                + "  (" + (pos ? "compra en la baja" : "vende en la baja") + ")",
                pos ? ColTecho : ColPiso, false);
            if (vivo.HasValue)
                Add("   interpolado al precio de ahora, no al de la cadena",
                    Color.FromArgb(150, 160, 175), false);
            if (d.Gex0dteB != null)
                Add("GEX del vencimiento de hoy: "
                    + d.Gex0dteB.Value.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) + " B",
                    d.Gex0dteB >= 0 ? ColTecho : ColPiso, false);

            if (TableroContexto && _ctx.Listo)
            {
                Add("", ColTexto, false);
                Add("CONTEXTO ATAS  (" + _ctx.BarrasUsadas + " barras, "
                    + Kilo(_ctx.VolumenSesion) + " de volumen)", ColTexto, true);
                Add("POC " + _ctx.Poc.ToString("0.00", CultureInfo.InvariantCulture)
                    + "   VAH " + _ctx.Vah.ToString("0.00", CultureInfo.InvariantCulture)
                    + "   VAL " + _ctx.Val.ToString("0.00", CultureInfo.InvariantCulture), ColPoc, false);
                Add("VWAP " + _ctx.Vwap.ToString("0.00", CultureInfo.InvariantCulture)
                    + "   1 sigma = " + _ctx.Sigma.ToString("0.0", CultureInfo.InvariantCulture) + " pts",
                    ColVwap, false);
                var dl = _ctx.DeltaAcumulado;
                Add("Delta acumulado " + (dl >= 0 ? "+" : "") + Kilo(dl)
                    + "   (max " + Kilo(_ctx.DeltaMaximo) + " / min " + Kilo(_ctx.DeltaMinimo) + ")",
                    dl >= 0 ? ColTecho : ColPiso, false);
                if (precio > 0)
                {
                    var rel = precio > _ctx.Vah ? "arriba del area de valor"
                            : precio < _ctx.Val ? "abajo del area de valor"
                                                : "dentro del area de valor";
                    Add("El precio esta " + rel, Color.FromArgb(170, 180, 195), false);
                }
            }

            if (VerConfluencia && d.Niveles.Any(n => n.Puntaje >= Math.Max(1, PuntajeResaltar)))
            {
                Add("", ColTexto, false);
                Add("CONFLUENCIA", ColTexto, true);
                foreach (var n in d.Niveles.Where(n => n.Puntaje >= Math.Max(1, PuntajeResaltar))
                                           .OrderByDescending(n => n.Puntaje).Take(4))
                    Add("x" + n.Puntaje + "  " + n.Nombre + " "
                        + (n.Fut ?? 0).ToString("0.00", CultureInfo.InvariantCulture)
                        + "  -  " + n.Razones, ColorDe(n), false);
            }

            Add("", ColTexto, false);
            if (d.Base != null)
                Add("Base " + d.Contrato + " - " + d.Indice + " "
                    + d.Base.Value.ToString("+0.00;-0.00", CultureInfo.InvariantCulture)
                    + (d.BaseConfiable ? "  (firme)"
                       : "  (FLOJA: " + (d.BaseErrorTicks ?? 0).ToString("0.#", CultureInfo.InvariantCulture)
                         + " ticks - no dibujes estos niveles)"),
                    d.BaseConfiable ? Color.FromArgb(170, 180, 195) : ColPiso, !d.BaseConfiable);
            if (d.TasaCorta != null)
                Add("Tasa " + (d.TasaCorta.Value * 100).ToString("0.00", CultureInfo.InvariantCulture)
                    + "%   dividendo implicito "
                    + ((d.DividendoImplicito ?? 0) * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%",
                    Color.FromArgb(130, 140, 155), false);

            var colEdad = d.CadenaMuyVencida ? ColPiso : d.CadenaVencida ? ColIman
                        : Color.FromArgb(140, 150, 165);
            Add("Cadena CBOE " + d.CadenaTs + "  (" + d.EdadMin + " min)", colEdad, d.CadenaVencida);
            if (d.CadenaMuyVencida)
                Add("CBOE no refresca hace " + (d.EdadMin / 60) + " horas. NO OPERES CON ESTO.", ColPiso, true);
            else if (d.CadenaVencida)
                Add("Sirve para ubicar niveles, no para cronometrar la entrada.", ColIman, false);
            if (d.IndiceAtrasado)
                Add("El " + d.Indice + " publicado esta congelado; se usa el contado implicito "
                    + (d.SpotIndice ?? 0).ToString("0.00", CultureInfo.InvariantCulture),
                    Color.FromArgb(150, 160, 175), false);
            if (!string.IsNullOrEmpty(_error))
                Add("Ultima descarga fallo: " + _error, ColPiso, false);
            if (TableroDiagnostico)
                Add("Feed de opciones de ATAS: " + _opcionesAtas, Color.FromArgb(130, 140, 155), false);

            int w2 = 0, h = 8;
            foreach (var it in L)
            {
                var t = g.MeasureString(it.Item1.Length == 0 ? " " : it.Item1, it.Item3 ? fb : f);
                w2 = Math.Max(w2, t.Width); h += t.Height + 2;
            }
            w2 += 18;
            int cx = (EsquinaTablero == Esquina.ArribaIzquierda || EsquinaTablero == Esquina.AbajoIzquierda)
                     ? area.Left + 8 : area.Right - w2 - 10;
            int cy = (EsquinaTablero == Esquina.ArribaDerecha || EsquinaTablero == Esquina.ArribaIzquierda)
                     ? area.Top + 8 : area.Bottom - h - 10;
            var caja = new Rectangle(cx, cy, w2, h);
            g.FillRectangle(Color.FromArgb(Math.Max(0, Math.Min(255, OpacidadTablero)), ColFondo), caja);
            g.DrawRectangle(new RenderPen(Color.FromArgb(90, 120, 130, 145), 1), caja);

            var y = caja.Top + 4;
            foreach (var it in L)
            {
                var ff = it.Item3 ? fb : f;
                if (it.Item1.Length > 0) g.DrawString(it.Item1, ff, it.Item2, caja.Left + 9, y);
                y += g.MeasureString(it.Item1.Length == 0 ? " " : it.Item1, ff).Height + 2;
            }
        }
    }
}
