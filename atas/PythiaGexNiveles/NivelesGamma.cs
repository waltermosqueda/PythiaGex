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
using OFT.Rendering.Control;
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
            public double? Idx, Fut, GexM, Toque, OiC, OiP, DexM, VexM, ChexM;
            public bool Es0dte;
            public int Puntaje;
            public string Razones = "";
            public Contexto.Zona Flujo;
            // Si el segundo strike pesa casi lo mismo, la pared no es una
            // raya: es una zona con dos paredes comparables.
            public double? CompetidorIdx, CompetidorFut, CompetidorPct, SeparacionPts;
            public bool Disputado;
            // Probabilidad que paga el mercado, y cuanto se separan los
            // cuatro caminos con que se calcula. Si se separan mucho el
            // numero no se puede tomar como firme.
            public double? ProbFinal, ProbDelta, ProbDispersion, Iv, ProbFactor;
            public string ProbControl = "";
        }

        private sealed class Hueco
        {
            public double DesdeFut, HastaFut, Ancho;
            public bool SobreSpot;
        }

        private sealed class Escalon { public double Fut, GexB, Contratos; }

        /// <summary>Un strike cargado pegado al precio. Las paredes grandes
        /// suelen estar a mas de cien puntos; esto es lo que se toca en los
        /// proximos minutos.</summary>
        private sealed class Cercano
        {
            public double Idx, Fut, GexM, PctDelMayor, DistPts;
            public int DistTicks;
            public double? OiC, OiP, Toque;
            public bool Solo0dte;
            public string Signo = "";
            public double? ProbFinal, ProbDispersion, Iv, ProbFactor;
            public string ProbControl = "";
        }

        /// <summary>Techo, piso e iman de UN vencimiento. El de hoy y el de
        /// manana no los tienen en el mismo lugar.</summary>
        private sealed class NivelVenc
        {
            public double? Fut, GexM, DistPts;
            public int DistTicks;
        }

        private sealed class Vencimiento
        {
            public string Fecha = "";
            public double GexM, Oi;
            public NivelVenc Techo, Piso, Iman;
        }

        /// <summary>Las griegas agregadas del complejo y los flujos que se
        /// derivan de ellas. Todo viene calculado del backend sobre la cadena;
        /// aca no se estima ni se inventa nada.</summary>
        private sealed class Griegas
        {
            public double? GexB, DexB, VexB, ChexB, TexM, VegaM, PutCallOi;
            public double? DiasAlVencimiento, CharmPendienteB, CharmContratos;
            public double? VannaPorPuntoIv, DexContratos, SkewPp, IvAtm;
            public string SkewLectura = "", TermForma = "", TermLectura = "";
        }

        private sealed class Datos
        {
            public string Indice = "", Contrato = "", Micro = "", Regimen = "", CadenaTs = "";
            public int EdadMin;
            public bool CadenaVencida, CadenaMuyVencida, BaseConfiable, IndiceAtrasado;
            public double? Base, BaseErrorTicks, SpotIndice, SpotFuturo;
            public double? NetGexB, Gex0dteB, ExpectedMove, TasaCorta, DividendoImplicito;
            public double? CoberturaContratos;
            public Griegas G = new();
            public List<Nivel> Niveles = new();
            public List<Hueco> Huecos = new();
            public List<Escalon> Escalera = new();
            public Dictionary<string, double> Sesion = new();
            public List<Cercano> Cercanos = new();
            public List<Vencimiento> PorVenc = new();
            public string ProbVencimiento = "";
            public double? ProbDias, ProbSpotIndice, ProbIvAtm;
            public DateTime? Liquida;
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
        private Rectangle _cabecera = Rectangle.Empty;
        private bool _colapsado;

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

        [Display(Name = "Convexity ladder (cobertura por escalon)", GroupName = "Niveles de gamma", Order = 46)]
        public bool VerEscalera { get; set; } = true;

        // Una pared cuyo segundo candidato pesa casi lo mismo no es una raya:
        // un movimiento minimo del precio da vuelta cual gana. Medido el
        // 2026-08-31: el put wall tenia un competidor al 98% a 25 puntos.
        [Display(Name = "Marcar la zona cuando la pared esta disputada", GroupName = "Niveles de gamma", Order = 47)]
        public bool VerZonaDisputada { get; set; } = true;

        // Las paredes grandes suelen estar a mas de cien puntos del precio.
        // Estos son los strikes cargados que se tocan en los proximos minutos.
        [Display(Name = "Niveles cercanos al precio", GroupName = "Niveles cercanos", Order = 48)]
        public bool VerCercanos { get; set; } = true;

        [Display(Name = "Cuantos dibujar", GroupName = "Niveles cercanos", Order = 49)]
        public int NCercanos { get; set; } = 6;

        [Display(Name = "Peso minimo (% del mayor del barrio)", GroupName = "Niveles cercanos", Order = 50)]
        public int PesoMinCercano { get; set; } = 20;

        [Display(Name = "Grosor", GroupName = "Niveles cercanos", Order = 51)]
        public int GrosorCercano { get; set; } = 1;

        [Display(Name = "Estilo", GroupName = "Niveles cercanos", Order = 52)]
        public TipoLinea LineaCercano { get; set; } = TipoLinea.Punteada;

        [Display(Name = "Que frena (gamma positiva)", GroupName = "Niveles cercanos", Order = 53)]
        public Color ColFrena { get; set; } = Color.FromArgb(120, 200, 160);

        [Display(Name = "Que empuja (gamma negativa)", GroupName = "Niveles cercanos", Order = 54)]
        public Color ColEmpuja { get; set; } = Color.FromArgb(220, 130, 130);

        [Display(Name = "Techo y piso de cada vencimiento", GroupName = "Niveles cercanos", Order = 55)]
        public bool VerPorVencimiento { get; set; } = true;

        [Display(Name = "Cuantos vencimientos", GroupName = "Niveles cercanos", Order = 56)]
        public int NVencimientos { get; set; } = 2;

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

        [Display(Name = "Cuanto muestra (un clic en el titulo lo pliega)", GroupName = "Tablero", Order = 91)]
        public ModoTablero Modo { get; set; } = ModoTablero.Chip;

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

        [Display(Name = "Ancho minimo del tablero (px)", GroupName = "Tablero", Order = 96)]
        public int AnchoTablero { get; set; } = 250;

        [Display(Name = "Interlineado (px)", GroupName = "Tablero", Order = 97)]
        public int Interlineado { get; set; } = 3;

        // El panel de trading de ATAS se dibuja ENCIMA del ChartArea, asi que
        // el borde derecho del area no es el borde visible. Este margen corre
        // el tablero para que no quede tapado.
        [Display(Name = "Separacion del borde (px)", GroupName = "Tablero", Order = 98)]
        public int MargenTablero { get; set; } = 150;

        [Display(Name = "Ancho maximo (% del grafico)", GroupName = "Tablero", Order = 99)]
        public int AnchoMaxPct { get; set; } = 40;

        // ---- Umbrales. Ninguno es una ley de mercado: son cortes elegidos.
        // Por eso estan aca, editables, y no escondidos adentro del codigo.
        [Display(Name = "Area de valor (% del volumen)", GroupName = "Umbrales", Order = 200)]
        public double PctValueArea { get; set; } = 70;

        [Display(Name = "Nodo alto: veces el volumen promedio", GroupName = "Umbrales", Order = 201)]
        public double FactorNodoAlto { get; set; } = 2.0;

        [Display(Name = "Nodo bajo: veces el volumen promedio", GroupName = "Umbrales", Order = 202)]
        public double FactorNodoBajo { get; set; } = 0.3;

        [Display(Name = "Absorcion: minimo % del volumen de la sesion", GroupName = "Umbrales", Order = 203)]
        public double MinPctAbsorcion { get; set; } = 4.0;

        [Display(Name = "Absorcion: maximo |delta| / volumen", GroupName = "Umbrales", Order = 204)]
        public double MaxRatioAbsorcion { get; set; } = 0.12;

        [Display(Name = "Initial Balance (minutos)", GroupName = "Umbrales", Order = 205)]
        public int MinutosIb { get; set; } = 60;

        [Display(Name = "Segundos entre recalculos del contexto", GroupName = "Umbrales", Order = 206)]
        public int SegundosContexto { get; set; } = 5;

        [Display(Name = "Segundos de espera entre alertas", GroupName = "Umbrales", Order = 207)]
        public int SegundosEntreAlertas { get; set; } = 20;

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

        /// <summary>Un clic en el titulo del tablero lo pliega o lo despliega.
        /// Devolver true evita que el clic siga hasta el grafico, asi no
        /// dispara nada del lienzo por accidente.</summary>
        public override bool ProcessMouseClick(RenderControlMouseEventArgs e)
        {
            if (!VerTablero || _cabecera == Rectangle.Empty) return false;
            if (!_cabecera.Contains(e.X, e.Y)) return false;
            _colapsado = !_colapsado;
            try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            return true;
        }

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
            catch (System.Reflection.TargetInvocationException e)
            {
                // Invoke envuelve la excepcion real. Sin desenvolverla el
                // mensaje no dice nada: "thrown by the target of an invocation".
                var i = e.InnerException;
                _opcionesAtas = "NO: " + (i != null
                    ? i.GetType().Name + " - " + Recortar(i.Message, 60)
                    : Recortar(e.Message, 60));
            }
            catch (Exception e)
            {
                _opcionesAtas = "NO: " + e.GetType().Name + " - " + Recortar(e.Message, 60);
            }
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
                if (r.TryGetProperty("griegas", out var gr) && gr.ValueKind == JsonValueKind.Object)
                    d.G = new Griegas
                    {
                        GexB = Num(gr, "gex_B"), DexB = Num(gr, "dex_B"),
                        VexB = Num(gr, "vex_B"), ChexB = Num(gr, "chex_B"),
                        TexM = Num(gr, "tex_M"), VegaM = Num(gr, "vega_M"),
                        PutCallOi = Num(gr, "put_call_oi"),
                        DiasAlVencimiento = Num(gr, "dias_al_vencimiento"),
                        CharmPendienteB = Num(gr, "charm_pendiente_B"),
                        CharmContratos = Num(gr, "charm_pendiente_contratos"),
                        VannaPorPuntoIv = Num(gr, "vanna_contratos_por_punto_iv"),
                        DexContratos = Num(gr, "dex_contratos"),
                        SkewPp = Num(gr, "skew_pp"), IvAtm = Num(gr, "iv_atm"),
                        SkewLectura = Txt(gr, "skew_lectura"),
                        TermForma = Txt(gr, "term_forma"),
                        TermLectura = Txt(gr, "term_lectura"),
                    };

                if (r.TryGetProperty("niveles", out var ns) && ns.ValueKind == JsonValueKind.Array)
                    foreach (var n in ns.EnumerateArray())
                        d.Niveles.Add(new Nivel
                        {
                            Tipo = Txt(n, "tipo"), Nombre = Txt(n, "nombre"),
                            Criollo = Txt(n, "criollo"), Alias = Txt(n, "alias"),
                            Idx = Num(n, "idx"), Fut = Num(n, "fut"), GexM = Num(n, "gex_M"),
                            DexM = Num(n, "dex_M"), VexM = Num(n, "vex_M"), ChexM = Num(n, "chex_M"),
                            OiC = Num(n, "oi_c"), OiP = Num(n, "oi_p"), Toque = Num(n, "toque"),
                            Es0dte = Bol(n, "es0dte"),
                            Iv = Num(n, "iv"),
                            ProbFactor = Num(n, "prob_factor"),
                        });
                d.ProbVencimiento = Txt(r, "prob_vencimiento");
                d.ProbDias = Num(r, "prob_dias");
                d.ProbSpotIndice = Num(r, "prob_spot_indice");
                d.ProbIvAtm = Num(r, "prob_iv_atm");
                var liq = Txt(r, "prob_liquida_utc");
                if (!string.IsNullOrEmpty(liq)
                    && DateTime.TryParse(liq, CultureInfo.InvariantCulture,
                                         System.Globalization.DateTimeStyles.AdjustToUniversal
                                         | System.Globalization.DateTimeStyles.AssumeUniversal,
                                         out var dq))
                    d.Liquida = dq;
                if (r.TryGetProperty("niveles", out var np) && np.ValueKind == JsonValueKind.Array)
                {
                    int j = 0;
                    foreach (var n in np.EnumerateArray())
                    {
                        if (j >= d.Niveles.Count) break;
                        if (n.TryGetProperty("prob", out var pb) && pb.ValueKind == JsonValueKind.Object)
                        {
                            var lv = d.Niveles[j];
                            lv.ProbFinal = Num(pb, "final_mercado");
                            lv.ProbDelta = Num(pb, "final_delta");
                            lv.ProbDispersion = Num(pb, "dispersion_pp");
                            lv.ProbControl = Txt(pb, "control");
                        }
                        j++;
                    }
                }
                if (r.TryGetProperty("niveles", out var ns2) && ns2.ValueKind == JsonValueKind.Array)
                {
                    int i = 0;
                    foreach (var n in ns2.EnumerateArray())
                    {
                        if (i >= d.Niveles.Count) break;
                        if (n.TryGetProperty("competencia", out var cp)
                            && cp.ValueKind == JsonValueKind.Object)
                        {
                            var lv = d.Niveles[i];
                            lv.CompetidorIdx = Num(cp, "competidor");
                            lv.CompetidorPct = Num(cp, "competidor_pct");
                            lv.SeparacionPts = Num(cp, "separacion_pts");
                            lv.Disputado = Bol(cp, "disputado");
                            if (lv.CompetidorIdx.HasValue && d.Base.HasValue)
                                lv.CompetidorFut = lv.CompetidorIdx.Value + d.Base.Value;
                        }
                        i++;
                    }
                }
                if (r.TryGetProperty("cercanos", out var cs2) && cs2.ValueKind == JsonValueKind.Array)
                    foreach (var c in cs2.EnumerateArray())
                        d.Cercanos.Add(new Cercano
                        {
                            Idx = Num(c, "idx") ?? Num(c, "indice") ?? 0,
                            Fut = Num(c, "futuro") ?? 0,
                            GexM = Num(c, "gex_M") ?? 0,
                            PctDelMayor = Num(c, "pct_del_mayor") ?? 0,
                            DistPts = Num(c, "dist_pts") ?? 0,
                            DistTicks = (int)(Num(c, "dist_ticks") ?? 0),
                            OiC = Num(c, "oi_call"), OiP = Num(c, "oi_put"),
                            Toque = Num(c, "prob_toque"),
                            Solo0dte = Bol(c, "solo_0dte"),
                            Signo = Txt(c, "signo"),
                            Iv = Num(c, "iv"),
                            ProbFactor = Num(c, "prob_factor"),
                        });
                if (r.TryGetProperty("cercanos", out var cp3) && cp3.ValueKind == JsonValueKind.Array)
                {
                    int j2 = 0;
                    foreach (var c in cp3.EnumerateArray())
                    {
                        if (j2 >= d.Cercanos.Count) break;
                        if (c.TryGetProperty("prob", out var pb) && pb.ValueKind == JsonValueKind.Object)
                        {
                            d.Cercanos[j2].ProbFinal = Num(pb, "final_mercado");
                            d.Cercanos[j2].ProbDispersion = Num(pb, "dispersion_pp");
                            d.Cercanos[j2].ProbControl = Txt(pb, "control");
                        }
                        j2++;
                    }
                }

                if (r.TryGetProperty("por_vencimiento", out var pv) && pv.ValueKind == JsonValueKind.Array)
                    foreach (var v in pv.EnumerateArray())
                    {
                        NivelVenc Leer(string k)
                        {
                            if (!v.TryGetProperty(k, out var x) || x.ValueKind != JsonValueKind.Object)
                                return null;
                            return new NivelVenc
                            {
                                Fut = Num(x, "futuro"), GexM = Num(x, "gex_M"),
                                DistPts = Num(x, "dist_pts"),
                                DistTicks = (int)(Num(x, "dist_ticks") ?? 0),
                            };
                        }
                        d.PorVenc.Add(new Vencimiento
                        {
                            Fecha = Txt(v, "fecha"),
                            GexM = Num(v, "gex_M") ?? 0, Oi = Num(v, "oi") ?? 0,
                            Techo = Leer("call_wall"), Piso = Leer("put_wall"),
                            Iman = Leer("gamma_pin"),
                        });
                    }

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
        private static double Norm(double x)
            => 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));

        /// <summary>Aproximacion de Abramowitz y Stegun, 7.1.26. Error menor a
        /// 1.5e-7, de sobra para una probabilidad que se muestra redondeada.</summary>
        private static double Erf(double x)
        {
            var sg = x < 0 ? -1.0 : 1.0;
            x = Math.Abs(x);
            var t = 1.0 / (1.0 + 0.3275911 * x);
            var y = 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t
                            - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
            return sg * y;
        }

        /// <summary>
        /// La probabilidad recalculada al precio de AHORA y al tiempo que
        /// queda de verdad.
        ///
        /// El feed publica la probabilidad que pagaba el mercado en el momento
        /// de la corrida. Entre corrida y corrida el precio se mueve y el
        /// tiempo se consume, y el numero se queda quieto: se llegaba a ver un
        /// nivel a 160 ticks diciendo "toque 100%". Es 0DTE, ademas: el tiempo
        /// se come toda la probabilidad antes del cierre.
        ///
        /// Se recalcula por Black-Scholes con la IV del PROPIO strike, que es
        /// la que respeta el skew, contra el precio en vivo y contra los
        /// minutos que faltan hasta las 16:00 ET. Tocar sigue siendo el doble
        /// de terminar del otro lado, por reflexion, topeado en 100%.
        ///
        /// El numero del mercado queda como control: si los dos se separan
        /// mucho, el que manda es el del mercado y el tablero lo dice.
        /// </summary>
        private double? ProbViva(Datos d, double? kIdx, double? iv, decimal precioFut,
                                 double? factor = null)
        {
            if (d?.Liquida == null || kIdx == null || iv == null || iv <= 0) return null;
            if (d.Base == null || precioFut <= 0) return null;
            var T = (d.Liquida.Value - DateTime.UtcNow).TotalDays / 365.0;
            if (T <= 0) return null;                      // ya vencio
            var S = (double)precioFut - d.Base.Value;     // el strike vive en el indice
            if (S <= 0) return null;
            var sig = iv.Value * Math.Sqrt(T);
            if (sig <= 0) return null;
            var d2 = (Math.Log(S / kIdx.Value) - 0.5 * iv.Value * iv.Value * T) / sig;
            var arriba = Norm(d2);
            var final = kIdx.Value >= S ? arriba : 1.0 - arriba;
            var bs = 200.0 * final;
            // El modelo da la DINAMICA correcta contra el precio y el tiempo,
            // pero no ve el skew: en el put wall daba 31,6% donde el mercado
            // pagaba 42,0%. El factor devuelve el nivel que paga el mercado.
            if (factor.HasValue && factor.Value > 0) bs *= factor.Value;
            return Math.Min(100.0, bs);
        }

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
            if (ultima == _ultimaBarraCtx
                && _relojCtx.ElapsedMilliseconds < Math.Max(1, SegundosContexto) * 1000) return;
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
            _ctx.PctValueArea = (decimal)Math.Max(10, Math.Min(95, PctValueArea)) / 100m;
            _ctx.FactorNodoAlto = (decimal)Math.Max(1.1, FactorNodoAlto);
            _ctx.FactorNodoBajo = (decimal)Math.Max(0.01, Math.Min(0.9, FactorNodoBajo));
            _ctx.MinPctAbsorcion = (decimal)Math.Max(0.1, MinPctAbsorcion);
            _ctx.MaxRatioAbsorcion = (decimal)Math.Max(0.01, MaxRatioAbsorcion);
            _ctx.MinutosIb = Math.Max(1, MinutosIb);
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

            // ---- niveles cercanos: lo que se toca en los proximos minutos
            if (VerCercanos && d.Cercanos.Count > 0)
            {
                var fc = Fuente(TamDetalle - 0.5f, false);
                int puestos = 0;
                foreach (var c in d.Cercanos)
                {
                    if (puestos >= Math.Max(1, NCercanos)) break;
                    if (c.PctDelMayor < PesoMinCercano) continue;
                    var pc = (decimal)(c.Fut != 0 ? c.Fut : c.Idx);
                    if (pc < lo || pc > hi) continue;
                    var frena = c.GexM > 0;
                    var col = frena ? ColFrena : ColEmpuja;
                    // el grosor acompana el peso: un strike del 90% se ve mas
                    var gr = Math.Max(1, GrosorCercano + (c.PctDelMayor >= 70 ? 1 : 0));
                    var alfa = (int)Math.Max(60, Math.Min(210, c.PctDelMayor * 2.1));
                    g.DrawLine(new RenderPen(Color.FromArgb(alfa, col), gr, Dash(LineaCercano)),
                               x0, cont.GetYByPrice(pc, false), x1, cont.GetYByPrice(pc, false));
                    var yc = cont.GetYByPrice(pc, false);
                    var t = pc.ToString("0.00", CultureInfo.InvariantCulture)
                          + "  " + Mag(c.GexM) + "  " + c.Signo
                          + "  " + (c.DistTicks >= 0 ? "+" : "") + c.DistTicks + "tk"
                          + (c.Solo0dte ? "  0DTE" : "");
                    var wt = g.MeasureString(t, fc).Width;
                    g.DrawString(t, fc, Color.FromArgb(alfa, col),
                                 x1 - wt - 8, yc - g.MeasureString(t, fc).Height - 1);
                    puestos++;
                }
            }

            // ---- techo y piso de cada vencimiento cercano
            if (VerPorVencimiento && d.PorVenc.Count > 0)
            {
                var fv = Fuente(TamDetalle - 1f, false);
                int iv2 = 0;
                foreach (var v in d.PorVenc)
                {
                    if (iv2 >= Math.Max(1, NVencimientos)) break;
                    var etq = iv2 == 0 ? "0DTE" : v.Fecha.Length >= 10 ? v.Fecha.Substring(5) : v.Fecha;
                    foreach (var par in new[] { (v.Techo, ColTecho, "techo"),
                                                (v.Piso, ColPiso, "piso") })
                    {
                        if (par.Item1?.Fut == null) continue;
                        var pv2 = (decimal)par.Item1.Fut.Value;
                        if (pv2 < lo || pv2 > hi) continue;
                        var yv = cont.GetYByPrice(pv2, false);
                        g.DrawLine(new RenderPen(Color.FromArgb(90, par.Item2), 1, DashStyle.Dash),
                                   x0 + area.Width / 2, yv, x1, yv);
                        var t = etq + " " + par.Item3 + "  "
                              + pv2.ToString("0.00", CultureInfo.InvariantCulture);
                        var wt = g.MeasureString(t, fv).Width;
                        g.DrawString(t, fv, Color.FromArgb(150, par.Item2),
                                     x1 - wt - 8, yv + 1);
                    }
                    iv2++;
                }
            }

            // Zona de pared disputada: se sombrea entre el lider y el
            // competidor, y se dibuja tambien la raya del competidor. Mostrar
            // una sola seria decir que el nivel esta donde no esta.
            if (VerZonaDisputada)
                foreach (var n in d.Niveles.Where(x => x.Disputado && !x.Es0dte))
                {
                    var a1 = n.Fut ?? n.Idx; var b1 = n.CompetidorFut ?? n.CompetidorIdx;
                    if (a1 == null || b1 == null) continue;
                    var pa = (decimal)a1.Value; var pb = (decimal)b1.Value;
                    var alto = Math.Max(pa, pb); var bajo = Math.Min(pa, pb);
                    if (alto < lo || bajo > hi) continue;
                    var col = ColorDe(n);
                    var yA = cont.GetYByPrice(alto, false);
                    var yB = cont.GetYByPrice(bajo, false);
                    var rec = Rectangle.Intersect(area,
                        new Rectangle(x0, Math.Min(yA, yB), area.Width, Math.Abs(yB - yA)));
                    if (rec.Width > 0 && rec.Height > 0)
                        g.FillRectangle(Color.FromArgb(18, col), rec);
                    // la raya del competidor, mas fina
                    var yc = cont.GetYByPrice(pb, false);
                    if (pb >= lo && pb <= hi)
                    {
                        g.DrawLine(new RenderPen(Color.FromArgb(120, col), 1, DashStyle.Dot),
                                   x0, yc, x1, yc);
                        var ft = Fuente(TamDetalle - 0.5f, false);
                        var tt = n.Nombre.ToUpperInvariant() + " 2do  "
                               + pb.ToString("0.00", CultureInfo.InvariantCulture)
                               + "   " + (n.CompetidorPct ?? 0).ToString("0.#", CultureInfo.InvariantCulture)
                               + "% del lider";
                        g.DrawString(tt, ft, Color.FromArgb(170, col), x0 + 6, yc + 2);
                    }
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
                           + (n.Disputado ? "   ZONA" : "")
                           + (destaca ? "   [ x" + n.Puntaje + " ]" : "");

                var partes = new List<string>();
                if (n.GexM != null) partes.Add(Mag(n.GexM) + " gamma");
                if (n.OiC != null) partes.Add("OI " + Oi(n.OiC) + "C/" + Oi(n.OiP) + "P");
                var pViva = ProbViva(d, n.Idx, n.Iv, precio, n.ProbFactor);
                if (pViva.HasValue)
                {
                    var txt = "toque " + pViva.Value.ToString("0", CultureInfo.InvariantCulture) + "%";
                    // el numero del mercado al momento de publicar, como control
                    if (n.Toque.HasValue && Math.Abs(n.Toque.Value - pViva.Value) > 12)
                        txt += " (mercado " + n.Toque.Value.ToString("0", CultureInfo.InvariantCulture) + "%)";
                    partes.Add(txt);
                }
                else if (n.Toque != null)
                    partes.Add("toque " + n.Toque.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%"
                               + (d.Liquida.HasValue && d.Liquida.Value <= DateTime.UtcNow ? " vencido" : ""));
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
                if (n.Disputado && n.CompetidorFut.HasValue)
                    partes.Add("disputada con "
                               + n.CompetidorFut.Value.ToString("0.00", CultureInfo.InvariantCulture)
                               + " al " + (n.CompetidorPct ?? 0).ToString("0.#", CultureInfo.InvariantCulture)
                               + "%, a " + (n.SeparacionPts ?? 0).ToString("0", CultureInfo.InvariantCulture) + " pts");
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
                && (DateTime.UtcNow - _ultimaAlerta).TotalSeconds > Math.Max(1, SegundosEntreAlertas))
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

        // ==================================================================
        // Tablero: dos columnas, plegable, y nada que no se pueda auditar
        // ==================================================================
        private sealed class Fila
        {
            public string Etq = "", Val = "";
            public Color Col;
            public bool Bold;
            public bool Titulo;
            public Fila(string e, string v, Color c, bool b = false, bool t = false)
            { Etq = e; Val = v; Col = c; Bold = b; Titulo = t; }
        }

        private void DibujarTablero(RenderContext g, Rectangle area, Datos d, decimal precio,
                                    decimal tick, RenderFont f, RenderFont fb)
        {
            var vivo = precio > 0 ? EnVivo(d, (double)precio) : null;
            var gexB = vivo.HasValue ? vivo.Value.gexB : (d.NetGexB ?? 0);
            var contratos = vivo.HasValue ? vivo.Value.contratos : (d.CoberturaContratos ?? 0);
            var pos = gexB >= 0;
            var raiz = d.Contrato.Length >= 2 ? d.Contrato.Substring(0, d.Contrato.Length - 2) : "ES";
            var G = d.G;

            // ---- cabecera: siempre visible, y es el boton que pliega
            var flecha = _colapsado ? "+" : "-";
            var titulo = "[" + flecha + "] " + d.Indice + " " + d.Contrato;
            var resumen = (pos ? "LONG" : "SHORT") + " "
                        + gexB.ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + "B  "
                        + Miles(Math.Abs(contratos)) + " " + raiz;

            var L = new List<Fila>();
            bool completo = Modo == ModoTablero.Completo;
            bool compacto = Modo != ModoTablero.Colapsado;

            // ---- CHIP: lo minimo con lo que se puede decidir sin abrir nada.
            // Regimen, flujo forzado, el vencimiento de hoy, los tres niveles
            // que importan y la probabilidad de tocarlos. Nueve renglones.
            if (Modo == ModoTablero.Chip && !_colapsado)
            {
                string Pct(double? v, string ctrl)
                {
                    if (v == null) return "-";
                    var t = v.Value.ToString("0", CultureInfo.InvariantCulture) + "%";
                    // si los cuatro caminos no coinciden, el numero lleva marca
                    return ctrl == "floja" ? t + "?" : t;
                }
                // el porcentaje que se muestra es el recalculado al precio de
                // ahora; el del feed queda solo como control
                double? PVivo(double? k, double? iv, double? fallback, double? fac)
                    => ProbViva(d, k, iv, precio, fac) ?? fallback;
                Color PorSigno(double gex) => gex > 0 ? ColTecho : ColPiso;

                L.Add(new Fila("Cobertura 1%  " + (pos ? "compra baja" : "vende baja"),
                               Miles(Math.Abs(contratos)) + " " + raiz,
                               pos ? ColTecho : ColPiso));
                if (G.CharmContratos.HasValue)
                    L.Add(new Fila("Charm pendiente "
                                   + (G.CharmContratos.Value < 0 ? "compra" : "vende"),
                                   Miles(Math.Abs(G.CharmContratos.Value)) + " " + raiz,
                                   G.CharmContratos.Value < 0 ? ColTecho : ColPiso));

                var cerca = d.Cercanos.FirstOrDefault();
                var pin = d.Niveles.FirstOrDefault(n => n.Tipo == "gamma_pin");
                var piso = d.Niveles.FirstOrDefault(n => n.Tipo == "put_wall" && !n.Es0dte);
                var techo = d.Niveles.FirstOrDefault(n => n.Tipo == "call_wall" && !n.Es0dte);
                var flip = d.Niveles.FirstOrDefault(n => n.Tipo == "gamma_flip");

                var minutos = d.Liquida.HasValue
                    ? (d.Liquida.Value - DateTime.UtcNow).TotalMinutes : (double?)null;
                L.Add(new Fila("CERCA   toque"
                               + (minutos.HasValue && minutos > 0
                                  ? "  " + Math.Round(minutos.Value) + " min"
                                  : ""), "", ColTexto, true, true));
                if (cerca != null)
                    L.Add(new Fila("cerca  " + (cerca.DistTicks >= 0 ? "+" : "") + cerca.DistTicks + " tk"
                                   + "  " + Mag(cerca.GexM) + " " + cerca.Signo,
                                   (cerca.Fut != 0 ? cerca.Fut : cerca.Idx).ToString("0.00", CultureInfo.InvariantCulture)
                                   + "   " + Pct(PVivo(cerca.Idx, cerca.Iv,
                                                 cerca.ProbFinal.HasValue
                                                 ? (double?)Math.Min(100.0, cerca.ProbFinal.Value * 2.0)
                                                 : null, cerca.ProbFactor), cerca.ProbControl),
                                   PorSigno(cerca.GexM)));
                foreach (var t3 in new[] { (pin, "iman"), (flip, "flip"),
                                           (piso, "piso"), (techo, "techo") })
                {
                    var n = t3.Item1; if (n == null) continue;
                    var pv3 = n.Fut ?? n.Idx; if (pv3 == null) continue;
                    var dt3 = precio > 0 ? Contexto.Ticks((decimal)pv3.Value, precio, tick) : 0;
                    L.Add(new Fila(t3.Item2 + "  " + (dt3 >= 0 ? "+" : "") + dt3 + " tk"
                                   + (n.Disputado ? "  ZONA" : ""),
                                   pv3.Value.ToString("0.00", CultureInfo.InvariantCulture)
                                   + "   " + Pct(PVivo(n.Idx, n.Iv, n.Toque, n.ProbFactor), n.ProbControl),
                                   ColorDe(n)));
                }

                var v0 = d.PorVenc.FirstOrDefault();
                if (v0 != null)
                {
                    L.Add(new Fila("0DTE   " + Mag(d.Gex0dteB * 1000), "", ColTexto, true, true));
                    var p0 = d.Niveles.FirstOrDefault(n => n.Tipo == "put_wall" && n.Es0dte);
                    var t0 = d.Niveles.FirstOrDefault(n => n.Tipo == "call_wall" && n.Es0dte);
                    foreach (var z in new[] { (p0, "piso", ColPiso), (t0, "techo", ColTecho) })
                    {
                        var n = z.Item1;
                        if (n == null) continue;
                        var pv4 = n.Fut ?? n.Idx; if (pv4 == null) continue;
                        var dt4 = precio > 0 ? Contexto.Ticks((decimal)pv4.Value, precio, tick) : 0;
                        L.Add(new Fila(z.Item2 + "  " + (dt4 >= 0 ? "+" : "") + dt4 + " tk",
                                       pv4.Value.ToString("0.00", CultureInfo.InvariantCulture)
                                       + "   " + Pct(PVivo(n.Idx, n.Iv, n.Toque, n.ProbFactor), n.ProbControl), z.Item3));
                    }
                }

                var pctCtrl = d.Niveles.Any(n => n.ProbControl == "floja");
                L.Add(new Fila(d.CadenaVencida ? "Cadena " + d.EdadMin + " min" : "Cadena al dia",
                               (d.BaseConfiable ? "base firme" : "BASE FLOJA")
                               + (pctCtrl ? " · % flojo" : "")
                               + (d.ProbDias.HasValue
                                  ? "  ·  % a " + d.ProbDias.Value.ToString("0.0", CultureInfo.InvariantCulture) + "d"
                                  : ""),
                               d.CadenaMuyVencida || !d.BaseConfiable ? ColPiso
                               : d.CadenaVencida ? ColIman : Color.FromArgb(140, 150, 165)));
            }
            else

            if (!_colapsado && compacto)
            {
                L.Add(new Fila("Regimen", pos ? "LONG, amortigua" : "SHORT, amplifica",
                               pos ? ColTecho : ColPiso, true));
                L.Add(new Fila("Net GEX" + (vivo.HasValue ? " (aca)" : ""),
                               gexB.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) + " B",
                               pos ? ColTecho : ColPiso));
                L.Add(new Fila("Cobertura 1%  " + (pos ? "compra baja" : "vende baja"),
                               Miles(Math.Abs(contratos)) + " " + raiz,
                               pos ? ColTecho : ColPiso));

                // --- charm: el motor del arrastre de la tarde
                if (G.CharmContratos.HasValue && G.DiasAlVencimiento.HasValue)
                    L.Add(new Fila("Charm pend. "
                                   + (G.CharmContratos.Value < 0 ? "a comprar" : "a vender")
                                   + " en " + G.DiasAlVencimiento.Value.ToString("0.00", CultureInfo.InvariantCulture) + "d",
                                   Miles(Math.Abs(G.CharmContratos.Value)) + " " + raiz,
                                   G.CharmContratos.Value < 0 ? ColTecho : ColPiso));
                if (G.VannaPorPuntoIv.HasValue)
                    L.Add(new Fila("Vanna por 1% IV",
                                   Miles(Math.Abs(G.VannaPorPuntoIv.Value)) + " " + raiz,
                                   Color.FromArgb(170, 180, 195)));
                if (d.Gex0dteB.HasValue)
                    L.Add(new Fila("GEX 0DTE",
                                   d.Gex0dteB.Value.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) + " B",
                                   d.Gex0dteB >= 0 ? ColTecho : ColPiso));

                if (completo)
                {
                    L.Add(new Fila("GRIEGAS DEL COMPLEJO", "", ColTexto, true, true));
                    if (G.DexB.HasValue)
                        L.Add(new Fila("Net DEX", nfB(G.DexB) + "  ("
                                       + (G.DexContratos.HasValue ? Miles(Math.Abs(G.DexContratos.Value)) + " " + raiz : "-")
                                       + ")", G.DexB >= 0 ? ColTecho : ColPiso));
                    if (G.VexB.HasValue)
                        L.Add(new Fila("Net VEX (vanna)", nfB(G.VexB) + " por 1% de IV",
                                       G.VexB >= 0 ? ColTecho : ColPiso));
                    if (G.ChexB.HasValue)
                        L.Add(new Fila("Net CHEX (charm)", nfB(G.ChexB) + " por dia",
                                       G.ChexB >= 0 ? ColTecho : ColPiso));
                    if (G.TexM.HasValue)
                        L.Add(new Fila("Net TEX (theta)", Miles(G.TexM.Value) + " M",
                                       G.TexM >= 0 ? ColTecho : ColPiso));
                    if (G.VegaM.HasValue)
                        L.Add(new Fila("Net Vega", Miles(G.VegaM.Value) + " M por punto de IV",
                                       Color.FromArgb(170, 180, 195)));
                    if (G.IvAtm.HasValue)
                        L.Add(new Fila("IV at-the-money",
                                       (G.IvAtm.Value * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%",
                                       Color.FromArgb(170, 180, 195)));
                    if (G.SkewPp.HasValue)
                        L.Add(new Fila("Skew",
                                       G.SkewPp.Value.ToString("+0.00;-0.00", CultureInfo.InvariantCulture)
                                       + " pp  " + Recortar(G.SkewLectura, 42),
                                       G.SkewPp > 0 ? ColPiso : ColTecho));
                    if (!string.IsNullOrEmpty(G.TermForma))
                        L.Add(new Fila("Term structure", G.TermForma + "  " + Recortar(G.TermLectura, 42),
                                       Color.FromArgb(170, 180, 195)));
                    if (G.PutCallOi.HasValue)
                        L.Add(new Fila("Put / Call OI",
                                       G.PutCallOi.Value.ToString("0.00", CultureInfo.InvariantCulture),
                                       G.PutCallOi > 1 ? ColPiso : ColTecho));
                }

                // --- contexto de ATAS
                if (TableroContexto && _ctx.Listo)
                {
                    L.Add(new Fila("CONTEXTO ATAS",
                                   _ctx.BarrasUsadas + " barras / " + Kilo(_ctx.VolumenSesion) + " vol",
                                   ColTexto, true, true));
                    L.Add(new Fila("POC", _ctx.Poc.ToString("0.00", CultureInfo.InvariantCulture), ColPoc));
                    L.Add(new Fila("Area de valor " + PctValueArea.ToString("0", CultureInfo.InvariantCulture) + "%",
                                   _ctx.Val.ToString("0.00", CultureInfo.InvariantCulture) + " a "
                                   + _ctx.Vah.ToString("0.00", CultureInfo.InvariantCulture), ColVa));
                    L.Add(new Fila("VWAP  (1s = " + _ctx.Sigma.ToString("0.0", CultureInfo.InvariantCulture) + " pts)",
                                   _ctx.Vwap.ToString("0.00", CultureInfo.InvariantCulture), ColVwap));
                    var dl = _ctx.DeltaAcumulado;
                    L.Add(new Fila("Delta acum.  max " + Kilo(_ctx.DeltaMaximo)
                                   + " min " + Kilo(_ctx.DeltaMinimo),
                                   (dl >= 0 ? "+" : "") + Kilo(dl),
                                   dl >= 0 ? ColTecho : ColPiso));
                    if (precio > 0)
                        L.Add(new Fila("El precio esta",
                                       precio > _ctx.Vah ? "arriba del area de valor"
                                       : precio < _ctx.Val ? "abajo del area de valor"
                                                           : "dentro del area de valor",
                                       Color.FromArgb(170, 180, 195)));
                    if (completo)
                    {
                        L.Add(new Fila("Initial Balance",
                                       _ctx.IbBajo.ToString("0.00", CultureInfo.InvariantCulture) + "  a  "
                                       + _ctx.IbAlto.ToString("0.00", CultureInfo.InvariantCulture)
                                       + "   (" + MinutosIb + " min)", Color.FromArgb(150, 160, 175)));
                        L.Add(new Fila("Rango del dia",
                                       _ctx.Minimo.ToString("0.00", CultureInfo.InvariantCulture) + "  a  "
                                       + _ctx.Maximo.ToString("0.00", CultureInfo.InvariantCulture),
                                       Color.FromArgb(150, 160, 175)));
                    }
                }

                // --- confluencia
                var conf = d.Niveles.Where(n => n.Puntaje >= Math.Max(1, PuntajeResaltar))
                                    .OrderByDescending(n => n.Puntaje).ToList();
                if (VerConfluencia && conf.Count > 0)
                {
                    L.Add(new Fila("CONFLUENCIA", conf.Count + " nivel(es)", ColTexto, true, true));
                    foreach (var n in conf.Take(completo ? 6 : 3))
                    {
                        L.Add(new Fila("x" + n.Puntaje + "  " + n.Nombre,
                                       (n.Fut ?? 0).ToString("0.00", CultureInfo.InvariantCulture), ColorDe(n)));
                        L.Add(new Fila("", Recortar(n.Razones, 52),
                                       Color.FromArgb(150, ColTexto)));
                    }
                }

                if (VerCercanos && d.Cercanos.Count > 0)
                {
                    L.Add(new Fila("CERCA DEL PRECIO", d.Cercanos.Count + " strikes", ColTexto, true, true));
                    foreach (var c in d.Cercanos.Take(completo ? 6 : 4))
                        L.Add(new Fila((c.DistTicks >= 0 ? "+" : "") + c.DistTicks + " tk"
                                       + (c.Solo0dte ? "  0DTE" : ""),
                                       (c.Fut != 0 ? c.Fut : c.Idx).ToString("0.00", CultureInfo.InvariantCulture)
                                       + "   " + Mag(c.GexM) + "  " + c.Signo,
                                       c.GexM > 0 ? ColFrena : ColEmpuja));
                }

                if (VerPorVencimiento && d.PorVenc.Count > 0)
                {
                    L.Add(new Fila("POR VENCIMIENTO", "", ColTexto, true, true));
                    int k2 = 0;
                    foreach (var v in d.PorVenc.Take(completo ? 3 : 2))
                    {
                        var etq = k2 == 0 ? "0DTE" : v.Fecha;
                        L.Add(new Fila(etq + "   GEX " + Mag(v.GexM),
                                       (v.Piso?.Fut ?? 0).ToString("0.00", CultureInfo.InvariantCulture)
                                       + "  a  " + (v.Techo?.Fut ?? 0).ToString("0.00", CultureInfo.InvariantCulture),
                                       v.GexM >= 0 ? ColTecho : ColPiso));
                        k2++;
                    }
                }

                var disp = d.Niveles.Where(n => n.Disputado && !n.Es0dte).ToList();
                if (disp.Count > 0)
                {
                    L.Add(new Fila("PAREDES DISPUTADAS", disp.Count.ToString(), ColIman, true, true));
                    foreach (var n in disp.Take(3))
                        L.Add(new Fila(n.Nombre + "  2do al "
                                       + (n.CompetidorPct ?? 0).ToString("0", CultureInfo.InvariantCulture) + "%",
                                       (n.Fut ?? 0).ToString("0.00", CultureInfo.InvariantCulture) + " / "
                                       + (n.CompetidorFut ?? 0).ToString("0.00", CultureInfo.InvariantCulture),
                                       ColorDe(n)));
                }

                // --- procedencia del dato, siempre
                L.Add(new Fila("PROCEDENCIA", "", ColTexto, true, true));
                if (d.Base.HasValue)
                    L.Add(new Fila("Base " + d.Contrato + "-" + d.Indice
                                   + (d.BaseConfiable ? "  firme"
                                      : "  FLOJA " + (d.BaseErrorTicks ?? 0).ToString("0.#", CultureInfo.InvariantCulture) + "tk"),
                                   d.Base.Value.ToString("+0.00;-0.00", CultureInfo.InvariantCulture),
                                   d.BaseConfiable ? Color.FromArgb(170, 180, 195) : ColPiso, !d.BaseConfiable));
                if (completo && d.TasaCorta.HasValue)
                    L.Add(new Fila("Tasa / dividendo",
                                   (d.TasaCorta.Value * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%  /  "
                                   + ((d.DividendoImplicito ?? 0) * 100).ToString("0.00", CultureInfo.InvariantCulture)
                                   + "%   medidos", Color.FromArgb(130, 140, 155)));
                var colEdad = d.CadenaMuyVencida ? ColPiso : d.CadenaVencida ? ColIman
                            : Color.FromArgb(140, 150, 165);
                L.Add(new Fila("Cadena CBOE", d.CadenaTs + "   " + d.EdadMin + " min", colEdad, d.CadenaVencida));
                if (d.CadenaMuyVencida)
                    L.Add(new Fila("", "CBOE no refresca hace " + (d.EdadMin / 60)
                                   + " horas. NO OPERES CON ESTO.", ColPiso, true));
                else if (d.CadenaVencida)
                    L.Add(new Fila("", "Sirve para ubicar niveles, no para cronometrar la entrada.",
                                   ColIman));
                if (d.IndiceAtrasado)
                    L.Add(new Fila("Contado implicito  (" + d.Indice + " congelado)",
                                   (d.SpotIndice ?? 0).ToString("0.00", CultureInfo.InvariantCulture),
                                   Color.FromArgb(150, 160, 175)));
                if (!string.IsNullOrEmpty(_error))
                    L.Add(new Fila("Ultima descarga", "fallo: " + _error, ColPiso));
                if (TableroDiagnostico)
                {
                    L.Add(new Fila("DIAGNOSTICO", "", ColTexto, true, true));
                    L.Add(new Fila("Feed de opciones", _opcionesAtas, Color.FromArgb(130, 140, 155)));
                    L.Add(new Fila("Umbrales",
                                   "VA " + PctValueArea.ToString("0", CultureInfo.InvariantCulture)
                                   + "%  nodo x" + FactorNodoAlto.ToString("0.0", CultureInfo.InvariantCulture)
                                   + "  absorcion " + MinPctAbsorcion.ToString("0.#", CultureInfo.InvariantCulture)
                                   + "% / " + MaxRatioAbsorcion.ToString("0.00", CultureInfo.InvariantCulture)
                                   + "  tol " + ToleranciaTicks + " tk",
                                   Color.FromArgb(130, 140, 155)));
                }
            }

            // ---- medidas
            var altoFila = g.MeasureString("X", f).Height + Math.Max(0, Interlineado);
            var altoTit = g.MeasureString("X", fb).Height + Math.Max(0, Interlineado);
            int anchoEtq = 0, anchoVal = 0;
            foreach (var it in L)
            {
                var ff = it.Bold ? fb : f;
                anchoEtq = Math.Max(anchoEtq, g.MeasureString(it.Etq, ff).Width);
                anchoVal = Math.Max(anchoVal, g.MeasureString(it.Val, ff).Width);
            }
            var anchoCab = g.MeasureString(titulo + "   " + resumen, fb).Width;
            var w = Math.Max(Math.Max(AnchoTablero, anchoCab + 20), anchoEtq + anchoVal + 34);
            // nunca mas ancho que la porcion del grafico que se eligio
            var tope = Math.Max(160, area.Width * Math.Max(15, Math.Min(90, AnchoMaxPct)) / 100);
            w = Math.Min(w, tope);
            var hCab = altoTit + 6;
            var h = hCab + (L.Count == 0 ? 0 : L.Count * altoFila + 6);

            var margen = Math.Max(0, MargenTablero);
            int cx = (EsquinaTablero == Esquina.ArribaIzquierda || EsquinaTablero == Esquina.AbajoIzquierda)
                     ? area.Left + 8 : area.Right - w - 10 - margen;
            if (cx < area.Left + 4) cx = area.Left + 4;
            int cy = (EsquinaTablero == Esquina.ArribaDerecha || EsquinaTablero == Esquina.ArribaIzquierda)
                     ? area.Top + 8 : area.Bottom - h - 10;
            var caja = new Rectangle(cx, cy, w, h);
            var op = Math.Max(0, Math.Min(255, OpacidadTablero));
            g.FillRectangle(Color.FromArgb(op, ColFondo), caja);
            g.DrawRectangle(new RenderPen(Color.FromArgb(90, 120, 130, 145), 1), caja);

            // cabecera: fondo propio para que se vea que es un boton
            _cabecera = new Rectangle(cx, cy, w, hCab);
            g.FillRectangle(Color.FromArgb(Math.Min(255, op + 25), pos ? ColTecho : ColPiso),
                            new Rectangle(cx, cy, 3, hCab));
            g.DrawString(titulo, fb, ColTexto, cx + 9, cy + 3);
            var wRes = g.MeasureString(resumen, fb).Width;
            g.DrawString(resumen, fb, pos ? ColTecho : ColPiso, cx + w - wRes - 9, cy + 3);
            g.DrawLine(new RenderPen(Color.FromArgb(70, 120, 130, 145), 1),
                       cx + 1, cy + hCab, cx + w - 1, cy + hCab);

            // filas
            var y = cy + hCab + 3;
            foreach (var it in L)
            {
                var ff = it.Bold ? fb : f;
                if (it.Titulo)
                {
                    g.DrawLine(new RenderPen(Color.FromArgb(45, 120, 130, 145), 1),
                               cx + 9, y + altoFila / 2, cx + w - 9, y + altoFila / 2);
                    var wt = g.MeasureString(it.Etq, ff).Width;
                    g.FillRectangle(Color.FromArgb(op, ColFondo),
                                    new Rectangle(cx + 7, y, wt + 6, altoFila));
                    g.DrawString(it.Etq, ff, Color.FromArgb(190, ColTexto), cx + 9, y);
                    if (it.Val.Length > 0)
                    {
                        var wv = g.MeasureString(it.Val, ff).Width;
                        g.FillRectangle(Color.FromArgb(op, ColFondo),
                                        new Rectangle(cx + w - wv - 12, y, wv + 6, altoFila));
                        g.DrawString(it.Val, ff, Color.FromArgb(140, ColTexto), cx + w - wv - 9, y);
                    }
                }
                else
                {
                    if (it.Etq.Length > 0)
                        g.DrawString(it.Etq, ff, Color.FromArgb(165, ColTexto), cx + 9, y);
                    var wv = g.MeasureString(it.Val, ff).Width;
                    g.DrawString(it.Val, ff, it.Col, cx + w - wv - 9, y);
                }
                y += altoFila;
            }
        }

        private static string nfB(double? v)
            => v.HasValue ? v.Value.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) + " B" : "-";
    }
}
