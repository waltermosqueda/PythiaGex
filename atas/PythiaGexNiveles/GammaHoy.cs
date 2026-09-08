using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGex
{
    using Strike = GammaHoyNucleo.Strike;
    using Snap = GammaHoyNucleo.Snap;

    /// <summary>
    /// GAMMA HOY. Construido de cero el 2026-09-07 a pedido del operador, para
    /// contrastarlo al lado de Gamma Vivo y de GAMMAlito.
    ///
    /// LO QUE APRENDIMOS DE LOS 30 VIDEOS Y DEL LABORATORIO, Y QUE ACA MANDA:
    ///
    ///   1. El mapa del dia es el de VOLUMEN. GAMMAlito tiene dos libros en su
    ///      panel, "Volume" y "Open Interest", y sus barras "respiran" porque
    ///      salen del volumen de hoy. En nuestro laboratorio las formulas de
    ///      volumen del dia le ganan al placebo por 22 y 42 puntos; la de gamma
    ///      por OI, que Gamma Vivo dibuja, pierde por 4. Aca el libro de
    ///      VOLUMEN es el principal y el de OI es la sombra de ayer.
    ///   2. La dominante es la barra mas larga del libro de volumen. Dos por
    ///      defecto (primaria y secundaria), como en su web.
    ///   3. El Max Change: donde estaba la punta de cada barra hace 15, 5 y 1
    ///      minutos (las tres pelotitas), y el strike con mayor cambio de GEX
    ///      en 1/5/10/15/30 minutos. "Lo mas importante no es la dominante
    ///      sino la pelotita: es adelantado".
    ///   4. La Convexity Ladder: la aceleracion por strike, aguamarina positiva
    ///      (colchon) y purpura negativa (tobogan), y el cuadrante de regimen:
    ///      mucho o poco GEX cerca del precio por convexidad positiva o
    ///      negativa = iman colchon / nivel explosivo / mercado estable /
    ///      salvese quien pueda. Y la alerta de transicion: perder el maximo
    ///      GEX con convexidad negativa.
    ///   5. Los Big Trades son bloques de OPCIONES con tamaño. Aca salen del
    ///      tape de Rithmic, contrato por contrato y sin retraso.
    ///   6. Nada se publica sin fuente y edad. Y todo se anota por vela para
    ///      que el laboratorio lo juzgue contra placebo: un indicador que
    ///      decide si tiene razon siempre tiene razon.
    ///
    /// LO QUE SE REUTILIZA: la cañeria. Feed (la cadena de CBOE que arma
    /// radar.py), CadenaViva (opciones de ES por Rithmic), Black76, Reloj y
    /// Centinela. Todo lo demas es nuevo.
    /// </summary>
    [DisplayName("PythiaGex - Gamma Hoy")]
    [Category("PythiaGex")]
    public class GammaHoy : Indicator
    {
        // ==================================================================
        // Ajustes (pocos, y cada uno con su porque)
        // ==================================================================
        public enum HorizonteVenc { Hoy, Semana, Todo }
        public enum LibroConv { Auto, Volumen, OI }
        public enum FuenteDatos { Vivo, Archivo, Hibrido }

        [Display(Name = "Fuente", GroupName = "1. Datos", Order = 0,
                 Description = "Vivo: el feed de la nube y la cadena de Rithmic. Archivo: REBOBINADO, recorre toda la historia cargada del grafico con la cadena que se tenia en cada minuto (archivo por dia en %APPDATA%\\ATAS\\PythiaGex\\cadenas; los dias que falten se bajan de la nube). Hibrido: la historia con el archivo Y la vela que se forma con el vivo; cada vela que cierra queda guardada, asi lo de hoy es el archivo de manana. La escalera y las rayas siguen la vela bajo el mouse.")]
        public FuenteDatos Fuente { get; set; } = FuenteDatos.Hibrido;

        [Display(Name = "Archivo: bajar de la nube los dias que falten", GroupName = "1. Datos", Order = 10)]
        public bool BajarArchivo { get; set; } = true;

        [Display(Name = "Archivo: url de las cadenas por dia", GroupName = "1. Datos", Order = 12,
                 Description = "La rama 'cadenas' del repo: un archivo por dia y raiz, escrito cada minuto por GitHub Actions.")]
        public string UrlArchivo { get; set; } = "https://raw.githubusercontent.com/waltermosqueda/PythiaGex/cadenas/";

        public enum LibroEnVivo { CBOE_SPX, Rithmic_ES }

        [Display(Name = "Libro en vivo", GroupName = "1. Datos", Order = 0,
                 Description = "CBOE_SPX: la cadena de SPX de la nube (llega 902 s tarde, cada minuto en la rueda). Rithmic_ES: las opciones de ES desde tu ATAS, volumen del dia por strike EN TIEMPO REAL e IV de las puntas, sin retraso y sin nube; strikes del futuro, sin base. Con Rithmic el mapa respira con cada operacion, como GAMMAlito. El pasado (archivo) sigue siendo SPX.")]
        public LibroEnVivo Libro { get; set; } = LibroEnVivo.CBOE_SPX;

        [Display(Name = "Rithmic: rearmar el libro cada (s)", GroupName = "1. Datos", Order = 14)]
        [Range(5, 120)]
        public int SegundosLibroRithmic { get; set; } = 10;

        [Display(Name = "Feed por minuto (rama cadenas)", GroupName = "1. Datos", Order = 13,
                 Description = "Ademas del feed de la nube (cada 5 min) baja ultima-<raiz>.json de la rama cadenas, que se escribe cada minuto en la rueda americana, y usa la mas nueva.")]
        public bool FeedMinuto { get; set; } = true;

        public enum EstiloRayas { Ninguna, Tenues, Normales }

        [Display(Name = "Rayas de los niveles", GroupName = "3. Pantalla", Order = 11,
                 Description = "GAMMAlito no cruza el grafico con rayas: los niveles viven en los guiones por vela y en el eje. Tenues = al 30 %.")]
        public EstiloRayas Rayas { get; set; } = EstiloRayas.Tenues;

        [Display(Name = "Guion de dominante por vela: grosor (px)", GroupName = "3. Pantalla", Order = 12)]
        [Range(1, 8)]
        public int GrosorGuion { get; set; } = 3;

        [Display(Name = "Semillas del Max Change por vela (30, 5 y 1 min)", GroupName = "3. Pantalla", Order = 13,
                 Description = "Tres puntos naranjas por vela con el strike de mayor cambio de GEX a 30, 5 y 1 min. Alineados varios minutos = ahi suele nacer la proxima dominante (GAMMAlito: 'la semillita').")]
        public bool VerSemillas { get; set; } = true;

        [Display(Name = "Zero gamma por vela (puntitos)", GroupName = "3. Pantalla", Order = 14)]
        public bool VerZeroPorVela { get; set; } = true;

        public enum RotulosBarras { Auto, Siempre, Nunca }

        [Display(Name = "Datos en las barras", GroupName = "3. Pantalla", Order = 15,
                 Description = "A la derecha de cada barra de volumen: GEX del libro (M/B), OI, volumen del dia e IV media; a la izquierda de cada barra de convexidad: su ΔGEX por +1 %. Auto: solo si las filas tienen lugar; si no, solo dominantes y majors.")]
        public RotulosBarras DatosEnBarras { get; set; } = RotulosBarras.Auto;

        [Display(Name = "Guardar la cadena viva de Rithmic por minuto", GroupName = "4. Auditoria", Order = 2,
                 Description = "viva-ES-<dia>.jsonl en %APPDATA%\\ATAS\\PythiaGex\\viva. Solo mientras ATAS esta abierto: es lo unico que la nube no puede grabar.")]
        public bool GuardarViva { get; set; } = true;

        [Display(Name = "Archivo: edad maxima de la cadena (horas)", GroupName = "1. Datos", Order = 11,
                 Description = "De noche la cadena de CBOE se congela y el indicador en vivo sigue mostrando la ultima: 20 horas reproduce eso. Con 0,3 (20 min) solo hay niveles en la rueda americana.")]
        public decimal ArchivoEdadMaxHoras { get; set; } = 20m;

        [Display(Name = "Feed (url base)", GroupName = "1. Datos", Order = 1)]
        public string Url { get; set; } = "https://waltermosqueda.github.io/PythiaGex/datos/atas/";

        [Display(Name = "Raiz manual (ES, NQ, RTY; vacio = del grafico)", GroupName = "1. Datos", Order = 2)]
        public string RaizManual { get; set; } = "";

        [Display(Name = "Refresco del feed (s)", GroupName = "1. Datos", Order = 3)]
        [Range(60, 3600)]
        public int SegundosRefresco { get; set; } = 60;

        [Display(Name = "Tasa libre de riesgo", GroupName = "1. Datos", Order = 4)]
        public decimal Tasa { get; set; } = 0.0375m;

        [Display(Name = "Vencimientos del mapa", GroupName = "1. Datos", Order = 5,
                 Description = "Hoy: solo el 0DTE (si no hay, el mas cercano). Semana: hasta 7 dias. Todo: la cadena entera. GAMMAlito: 'nosotros trabajamos en 0DTE'.")]
        public HorizonteVenc Horizonte { get; set; } = HorizonteVenc.Hoy;

        [Display(Name = "Cadena viva de Rithmic (volumen y bloques de opciones de ES)", GroupName = "1. Datos", Order = 6)]
        public bool UsarCadenaViva { get; set; } = true;

        [Display(Name = "Viva: strikes por lado", GroupName = "1. Datos", Order = 7)]
        [Range(5, 60)]
        public int StrikesEnVivo { get; set; } = 25;

        [Display(Name = "Viva: vencimientos", GroupName = "1. Datos", Order = 8)]
        [Range(1, 6)]
        public int VencimientosEnVivo { get; set; } = 2;

        [Display(Name = "Viva: tope de contratos suscritos", GroupName = "1. Datos", Order = 9)]
        [Range(20, 600)]
        public int TopeContratos { get; set; } = 200;

        [Display(Name = "Dominantes (barras mas largas del volumen)", GroupName = "2. Lectura", Order = 1)]
        [Range(1, 5)]
        public int CuantasDominantes { get; set; } = 2;

        [Display(Name = "Dominantes: radio alrededor del precio (%)", GroupName = "2. Lectura", Order = 2)]
        public decimal RadioDominantesPct { get; set; } = 2.0m;

        [Display(Name = "Dominante como centroide (ondula, como GAMMAlito)", GroupName = "2. Lectura", Order = 7,
                 Description = "Promedio de precio ponderado por gamma alrededor del strike ganador. Medido en los videos: la dominante de GAMMAlito es una banda de ~5 puntos que ondula, no una raya plana en un strike.")]
        public bool DominanteCentroide { get; set; } = true;

        [Display(Name = "Centroide: radio (puntos)", GroupName = "2. Lectura", Order = 8)]
        [Range(2, 60)]
        public decimal RadioCentroidePts { get; set; } = 12m;

        [Display(Name = "Pico de GEX 'cerca del precio': radio (%)", GroupName = "2. Lectura", Order = 3,
                 Description = "Para el cuadrante. El pico del volumen dentro de este radio se compara con el maximo de todo el libro.")]
        public decimal PicoRadioPct { get; set; } = 0.35m;

        [Display(Name = "'Mucho GEX' = pico cercano >= % del maximo", GroupName = "2. Lectura", Order = 4)]
        [Range(10, 100)]
        public int MuchoPct { get; set; } = 50;

        [Display(Name = "Convexidad: de que libro", GroupName = "2. Lectura", Order = 5,
                 Description = "Auto: volumen si ya hay volumen (>= 20 % del OI en gamma), si no OI. Se rotula cual se uso.")]
        public LibroConv Convexidad { get; set; } = LibroConv.Auto;

        [Display(Name = "Big Trade: contratos minimos (opciones de ES)", GroupName = "2. Lectura", Order = 6,
                 Description = "GAMMAlito usa ~180 en QQQ. Las opciones de ES son menos liquidas: 50 de arranque, se calibra midiendo.")]
        [Range(5, 5000)]
        public int UmbralBigTrade { get; set; } = 50;

        [Display(Name = "Ancho de las barras (px)", GroupName = "3. Pantalla", Order = 1)]
        [Range(30, 300)]
        public int AnchoBarras { get; set; } = 90;

        [Display(Name = "Sombra del libro de OI (ayer) detras de las barras", GroupName = "3. Pantalla", Order = 2)]
        public bool VerSombraOI { get; set; } = true;

        [Display(Name = "Convexity Ladder (derecha)", GroupName = "3. Pantalla", Order = 3)]
        public bool VerConvexidad { get; set; } = true;

        [Display(Name = "Escalera pegada al eje", GroupName = "3. Pantalla", Order = 4)]
        public bool VerEscalera { get; set; } = true;

        [Display(Name = "Escalera: ancho (px)", GroupName = "3. Pantalla", Order = 5)]
        [Range(80, 300)]
        public int AnchoEscalera { get; set; } = 118;

        [Display(Name = "Estela de las dominantes por vela", GroupName = "3. Pantalla", Order = 6)]
        public bool VerEstela { get; set; } = true;

        [Display(Name = "Pelotitas del Max Change (15, 5 y 1 min)", GroupName = "3. Pantalla", Order = 7)]
        public bool VerPelotitas { get; set; } = true;

        [Display(Name = "Big Trades sobre las velas", GroupName = "3. Pantalla", Order = 8)]
        public bool VerBigTrades { get; set; } = true;

        [Display(Name = "Tamaño de letra", GroupName = "3. Pantalla", Order = 9)]
        [Range(6, 14)]
        public decimal TamLetra { get; set; } = 8m;

        [Display(Name = "Margen de abajo (px)", GroupName = "3. Pantalla", Order = 10,
                 Description = "ChartArea es mas alto que lo visible: lo pegado al fondo cae detras del eje de tiempo.")]
        [Range(0, 200)]
        public int MargenInferior { get; set; } = 48;

        [Display(Name = "Anotar cada vela para el laboratorio", GroupName = "4. Auditoria", Order = 1)]
        public bool AnotarCentinela { get; set; } = true;

        // ==================================================================
        // Estado
        // ==================================================================
        // la cuenta vive en GammaHoyNucleo: una sola fuente para ATAS y el simulador
        private readonly GammaHoyNucleo _nucleo = new();

        private readonly object _candado = new();
        private Feed.Cadena _c;
        private string _error = "";
        private DateTime _ultimaBajada = DateTime.MinValue;
        private int _bajando;
        private TimeSpan _periodo;
        private Action _tick;

        private readonly CadenaViva _viva = new();
        private bool _vivaCorriendo;
        private DateTime _ultimoIntentoViva = DateTime.MinValue;

        // resultado del ultimo repricing (se copia bajo llave para dibujar)
        private List<Strike> _perfil = new();
        private double _S = double.NaN, _futuro = double.NaN, _base = double.NaN;
        private string _baseOrigen = "sin base";
        private double _zeroVol = double.NaN, _zeroOi = double.NaN, _netVol, _netOi;
        private double _mpVol = double.NaN, _mnVol = double.NaN, _mpOi = double.NaN, _mnOi = double.NaN;
        private double _maxAbsVol, _maxAbsOi, _maxAbsConv;
        private List<(double Fut, double Gex)> _doms = new();
        private string _libroConvUsado = "";
        private string _libroDomUsado = "vol";
        // cuadrante
        private string _cuadrante = "", _cuadranteCorto = "";
        private int _cuadranteN;
        private double _picoFut = double.NaN, _picoGex, _convEnPrecio;
        private bool _mucho;
        private DateTime _alertaHasta = DateTime.MinValue;
        private string _alerta = "";
        // max change
        private (double Fut, double Delta)[] _maxChange = new (double, double)[GammaHoyNucleo.Ventanas.Length];
        // estela de dominantes por vela
        private readonly Dictionary<int, double[]> _estela = new();
        // UN GUION POR CADA ACTUALIZACION, no uno por vela: medido en GAMMAlito hasta
        // 4-6 guiones por columna en las velas recientes. Cada vez que se reprecia y
        // la dominante se movio mas de un cuarto de punto, se agrega un guion a la vela.
        private readonly Dictionary<int, List<(double Fut, int Rango)>> _guiones = new();
        private void AgregarGuiones(int bar, double[] est)
        {
            if (est == null) return;
            if (!_guiones.TryGetValue(bar, out var lg)) { lg = new List<(double, int)>(); _guiones[bar] = lg; }
            for (int i = 0; i < est.Length; i++)
            {
                if (double.IsNaN(est[i]) || est[i] <= 0) continue;
                bool hay = false;
                for (int k = lg.Count - 1; k >= 0; k--) if (lg[k].Rango == i) { hay = Math.Abs(lg[k].Fut - est[i]) < 0.25; break; }
                if (!hay && lg.Count < 24) lg.Add((est[i], i));
            }
        }
        // por vela: zero por volumen y el strike del Max Change a 30, 5 y 1 min (semillas)
        private readonly Dictionary<int, (double Zero, double[] Mc)> _marcas = new();
        // centinela
        private Centinela _cent;
        private int _barraCent = -1;
        // rebobinado (Fuente = Archivo)
        private sealed class Foto
        {
            public double S, Futuro, ZeroVol, ZeroOi, MpVol, MnVol, MpOi, MnOi, PicoFut, ConvEnPrecio, NetVol, NetOi;
            public List<(double Fut, double Gex)> Doms; public string Cuad, Corto, LibroDom, LibroConv; public bool Mucho;
            public (double Fut, double Delta)[] Mc; public DateTime Vela, Cadena;
            public List<Strike> Perfil;      // las barras de ese minuto, para el mouse
        }
        private readonly Dictionary<int, Foto> _fotosBarra = new();
        private List<Feed.Cadena> _archivo;
        private int _iArchivo, _barraReb = -1, _rebConCadena, _rebSinCadena;
        private volatile bool _archivoCargando, _archivoListo;
        private Centinela _centArchivo;
        private int _barraVivaUlt = -1;
        private DateTime _ultimaViva = DateTime.MinValue;
        private double _masCercaUlt = double.NaN;    // dias al vencimiento mas cercano del mapa (para el titulo)
        private DateTime _ultimoLibroViva = DateTime.MinValue;
        private int _vivaFlaca = -1;                 // strikes utiles cuando el libro de Rithmic no alcanzo
        private string _rebRotulo = "";
        private DateTime _rebCadenaHora = DateTime.MinValue, _rebUltimoLog = DateTime.MinValue;
        private DateTime _ultimoAudit = DateTime.MinValue;
        private int _renders;

        private static readonly Color ColPos = Color.FromArgb(45, 220, 130);
        private static readonly Color ColNeg = Color.FromArgb(235, 60, 60);
        private static readonly Color ColConvPos = Color.FromArgb(93, 217, 208);
        private static readonly Color ColConvNeg = Color.FromArgb(168, 107, 255);
        private static readonly Color ColDom = Color.FromArgb(232, 200, 60);    // primaria: amarillo (hue 29 medido en GAMMAlito)
        private static readonly Color ColDom2 = Color.FromArgb(232, 168, 56);   // secundaria: naranja (hue 19 medido)
        private static readonly Color ColZero = Color.FromArgb(235, 235, 235);
        private static readonly Color ColTexto = Color.FromArgb(225, 230, 236);
        private static readonly Color ColFondo = Color.FromArgb(11, 16, 23);
        private static readonly Color ColAviso = Color.FromArgb(240, 160, 88);

        // ==================================================================
        // Ciclo de vida
        // ==================================================================
        /// <summary>Sin esto ATAS no llama a OnRender nunca (verificado el
        /// 2026-09-07: el indicador arrancaba, bajaba la cadena y no dibujaba
        /// ni la cabecera). La serie por defecto se esconde: todo es dibujo propio.</summary>
        public GammaHoy() : base(true)
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
            try
            {
                var ps = DataProvider?.Panels;
                if (ps != null && ps.Count > 0) Panel = ps.Contains("Chart") ? "Chart" : ps[0];
            }
            catch (Exception e) { Registrar(e); }

            _periodo = TimeSpan.FromSeconds(10);
            _tick = () =>
            {
                var ahora = DateTime.UtcNow;
                if (Libro == LibroEnVivo.Rithmic_ES && _viva.Activa && (ahora - _ultimoLibroViva).TotalSeconds >= Math.Max(5, SegundosLibroRithmic))
                {
                    _ultimoLibroViva = ahora;
                    try { var cv = DesdeViva(); if (cv != null) { _c = cv; _error = ""; } } catch (Exception e) { Registrar(e); }
                }
                if (Reloj.UltimaMedicion == DateTime.MinValue || (ahora - Reloj.UltimaMedicion).TotalMinutes >= 30)
                    _ = Reloj.Medir(Log);
                if ((ahora - _ultimaBajada).TotalSeconds >= Math.Max(60, SegundosRefresco))
                {
                    _ultimaBajada = ahora;
                    _ = BajarFeed();
                }
                if (UsarCadenaViva && !_viva.Activa && !_vivaCorriendo && (ahora - _ultimoIntentoViva).TotalSeconds >= 180)
                {
                    _ultimoIntentoViva = ahora;
                    ArrancarViva();
                }
                _viva.UmbralGrande = UmbralBigTrade;
                // la cadena viva, un renglon por minuto, al archivo local
                if (GuardarViva && _viva.Activa && (ahora - _ultimaViva).TotalSeconds >= 60)
                {
                    _ultimaViva = ahora;
                    try { Feed.Archivo.GuardarViva(Raiz(), VivaJson()); } catch (Exception e) { Registrar(e); }
                }
                // HIBRIDO: el archivo se carga desde aca (con el mercado cerrado no hay OnCalculate)
                if (Fuente != FuenteDatos.Archivo && !_archivoListo && !_archivoCargando && CurrentBar > 0)
                {
                    try { CargarArchivo(Utc(GetCandle(0).Time).AddDays(-1)); } catch (Exception e) { Registrar(e); }
                }
                // CON EL MERCADO CERRADO NO HAY TICKS Y OnCalculate NO CORRE
                // (Labor Day 2026-09-07, 13:00 ET: el indicador arranco, bajo la
                // cadena y nunca calculo). El mapa se reprecia tambien desde el
                // temporizador, con el ultimo cierre, para que la pantalla no
                // quede vacia ni vieja.
                try { Repreciar(); } catch (Exception e) { Registrar(e); }
                try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            };
            if (Fuente == FuenteDatos.Archivo)
            {
                // REBOBINADO: nada de vivo. La carga del archivo arranca en la
                // primera vela (ahi se sabe desde que dia va el grafico). El
                // temporizador queda solo para redibujar mientras carga.
                _tick = () =>
                {
                    // CON EL MERCADO CERRADO ATAS NO LLAMA A OnCalculate (visto el
                    // 2026-09-07 al aplicar el modo: "esperando la primera vela" para
                    // siempre). El archivo se carga desde aca en cuanto haya velas, y
                    // RecalculateValues() recorre el grafico.
                    try
                    {
                        if (!_archivoListo && !_archivoCargando && CurrentBar > 0)
                            CargarArchivo(Utc(GetCandle(0).Time).AddDays(-1));
                    }
                    catch (Exception e) { Registrar(e); }
                    // LA CADENA VIVA SE GRABA EN TODOS LOS MODOS: es lo unico que la nube no puede
                    // hacer por nosotros y el operador la quiere siempre ("no es excusa valida")
                    var ahora = DateTime.UtcNow;
                    if (UsarCadenaViva && !_viva.Activa && !_vivaCorriendo && (ahora - _ultimoIntentoViva).TotalSeconds >= 180) { _ultimoIntentoViva = ahora; ArrancarViva(); }
                    _viva.UmbralGrande = UmbralBigTrade;
                    if (GuardarViva && _viva.Activa && (ahora - _ultimaViva).TotalSeconds >= 60)
                    {
                        _ultimaViva = ahora;
                        try { Feed.Archivo.GuardarViva(Raiz(), VivaJson()); } catch (Exception e) { Registrar(e); }
                    }
                    try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
                };
                SubscribeToTimer(_periodo, _tick);
                _ultimoIntentoViva = DateTime.UtcNow;
                if (UsarCadenaViva) ArrancarViva();
                Log("Gamma Hoy 1.0 arranca en REBOBINADO. raiz=" + Raiz() + " horizonte=" + Horizonte + " carpeta=" + Feed.Archivo.Carpeta);
                return;
            }
            SubscribeToTimer(_periodo, _tick);
            _ = Reloj.Medir(Log);
            _ultimaBajada = DateTime.UtcNow;
            _ultimoIntentoViva = DateTime.UtcNow;
            _ = BajarFeed();
            if (UsarCadenaViva) ArrancarViva();
            Log("Gamma Hoy 1.0 arranca" + (Fuente == FuenteDatos.Hibrido ? " en HIBRIDO (archivo + vivo)" : " en VIVO (con el pasado del archivo)") + ". raiz=" + Raiz() + " horizonte=" + Horizonte);
        }

        protected override void OnDispose()
        {
            try { if (_tick != null) UnsubscribeFromTimer(_periodo, _tick); } catch { }
            try { _cent?.Volcar(true); } catch { }
            try { _centArchivo?.Volcar(true); } catch { }
            try { _viva.Dispose(); } catch { }
        }

        private void ArrancarViva()
        {
            _vivaCorriendo = true;
            _viva.UmbralGrande = UmbralBigTrade;
            _ = Task.Run(async () =>
            {
                try
                {
                    await _viva.Arrancar(DataProvider, TradingManager, TradingManager?.Security, Raiz(),
                                         7, Math.Max(5, StrikesEnVivo), Math.Max(1, VencimientosEnVivo),
                                         Math.Max(20, TopeContratos), m => Log("[viva] " + m)).ConfigureAwait(false);
                }
                catch (Exception e) { Registrar(e); }
                finally { _vivaCorriendo = false; }
            });
        }

        private async Task BajarFeed()
        {
            if (Interlocked.Exchange(ref _bajando, 1) == 1) return;
            try
            {
                var c = await Feed.Bajar(Url, Raiz(), m => _error = m).ConfigureAwait(false);
                if (FeedMinuto)
                {
                    var u = await Feed.BajarUltima(UrlArchivo, Raiz(), null).ConfigureAwait(false);
                    if (u != null && (c == null || u.GeneradoUtc > c.GeneradoUtc)) c = u;
                }
                if (c != null && !(Libro == LibroEnVivo.Rithmic_ES && _c != null && _c.EsFuturo)) { _c = c; _error = ""; }
            }
            finally
            {
                Interlocked.Exchange(ref _bajando, 0);
                try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            }
        }

        private string Raiz()
        {
            if (!string.IsNullOrWhiteSpace(RaizManual)) return RaizManual.Trim().ToUpperInvariant();
            var s = (InstrumentInfo?.Instrument ?? "").ToUpperInvariant().TrimStart('#');
            if (s.StartsWith("MNQ") || s.StartsWith("NQ")) return "NQ";
            if (s.StartsWith("M2K") || s.StartsWith("RTY")) return "RTY";
            return "ES";
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            // el archivo se recorre en su propio hilo (RecorrerArchivo): aca solo el vivo
            if (Fuente == FuenteDatos.Archivo) return;
            if (bar != CurrentBar - 1) return;
            try { Repreciar(); } catch (Exception e) { Registrar(e); }
            try { Anotar(bar); } catch (Exception e) { Registrar(e); }
            // HIBRIDO: cuando arranca una vela nueva, la que acaba de cerrar guarda
            // su foto con el estado vivo de ese instante: la historia sigue creciendo
            // con lo de verdad y el mouse la puede revisar como al archivo
            if (Fuente != FuenteDatos.Archivo && bar > _barraVivaUlt)
            {
                if (_barraVivaUlt >= 0) try { GuardarFotoViva(bar - 1); } catch (Exception e) { Registrar(e); }
                _barraVivaUlt = bar;
            }
        }

        private void GuardarFotoViva(int bar)
        {
            IndicatorCandle c; try { c = GetCandle(bar); } catch { return; }
            if (c == null) return;
            var cad = _c;
            lock (_candado)
            {
                if (_perfil.Count == 0) return;
                _fotosBarra[bar] = new Foto
                {
                    S = _S, Futuro = _futuro, ZeroVol = _zeroVol, ZeroOi = _zeroOi, MpVol = _mpVol, MnVol = _mnVol, MpOi = _mpOi, MnOi = _mnOi,
                    PicoFut = _picoFut, ConvEnPrecio = _convEnPrecio, NetVol = _netVol, NetOi = _netOi, Doms = _doms, Cuad = _cuadrante, Corto = _cuadranteCorto,
                    LibroDom = _libroDomUsado, LibroConv = _libroConvUsado, Mucho = _mucho, Mc = _maxChange, Vela = Utc(c.Time), Perfil = _perfil,
                    Cadena = cad != null && cad.GeneradoUtc != default ? cad.GeneradoUtc : (cad != null ? cad.RecibidoUtc : DateTime.MinValue),
                };
            }
        }

        /// <summary>El centinela del archivo, separado del vivo ("hoy-"): asi el
        /// laboratorio no mezcla lo rebobinado con lo que paso en pantalla.</summary>
        private void AnotarArchivo(int bar, IndicatorCandle c, GammaHoyNucleo.Lectura L)
        {
            if (!AnotarCentinela || c == null || L == null) return;
            if (_centArchivo == null)
            {
                var instr = InstrumentInfo != null ? InstrumentInfo.Instrument : "x";
                var marco = ChartInfo != null && ChartInfo.ChartType != null ? ChartInfo.ChartType + "-" + ChartInfo.TimeFrame : "x";
                _centArchivo = new Centinela("rebobinado-atas-" + instr, marco);
            }
            _centArchivo.Anotar(bar, c.LastTime != default(DateTime) ? c.LastTime : c.Time,
                (double)c.Open, (double)c.High, (double)c.Low, (double)c.Close, (double)c.Volume, (double)c.Ticks, (double)c.Delta, L.S, GammaHoyNucleo.Niveles(L));
        }

        private static DateTime Utc(DateTime t) => t.Kind == DateTimeKind.Utc ? t : (t.Kind == DateTimeKind.Local ? t.ToUniversalTime() : DateTime.SpecifyKind(t, DateTimeKind.Utc));

        private void CargarArchivo(DateTime desde)
        {
            if (_archivoCargando || _archivoListo) return;
            _archivoCargando = true;
            var raiz = Raiz();
            var hasta = DateTime.UtcNow;
            _ = Task.Run(async () =>
            {
                try
                {
                    if (BajarArchivo)
                        for (var d = desde.Date; d <= hasta.Date; d = d.AddDays(1))
                            await Feed.Archivo.BajarDia(string.IsNullOrWhiteSpace(UrlArchivo) ? Url : UrlArchivo, raiz, d, Log).ConfigureAwait(false);
                    var ls = Feed.Archivo.Cargar(raiz, desde, hasta, Log);
                    lock (_candado) { _archivo = ls; _iArchivo = 0; _barraReb = -1; _fotosBarra.Clear(); }
                    Log("REBOBINADO: " + ls.Count + " cadenas cargadas; recorro el grafico en un hilo aparte");
                    RecorrerArchivo(ls);
                    _archivoListo = true;
                }
                catch (Exception e) { Registrar(e); }
                finally { _archivoCargando = false; }
            });
        }

        /// <summary>Recorre todas las velas cargadas (menos la que se forma) con la
        /// cadena vigente al cierre de cada una. Corre en el hilo de fondo que cargo
        /// el archivo, con un nucleo PROPIO para no mezclar sus fotos del Max Change
        /// con las del vivo. No toca RecalculateValues: el hilo de ticks de ATAS no
        /// se entera (antes, 5.472 velas en el hilo de ticks = "Slow ticks
        /// processing 6 s" en el log de ATAS).</summary>
        private void RecorrerArchivo(List<Feed.Cadena> arch)
        {
            if (arch == null || arch.Count == 0) return;
            var nuc = new GammaHoyNucleo();
            var a = nuc.A; var b0 = _nucleo.A;
            a.Tasa = (double)Tasa; a.Horizonte = (GammaHoyNucleo.HorizonteVenc)(int)Horizonte; a.CuantasDominantes = CuantasDominantes;
            a.RadioDominantesPct = (double)RadioDominantesPct; a.PicoRadioPct = (double)PicoRadioPct; a.MuchoPct = MuchoPct; a.Convexidad = (GammaHoyNucleo.LibroConv)(int)Convexidad;
            a.Centroide = DominanteCentroide; a.RadioCentroidePts = (double)RadioCentroidePts;
            double edadMax = (double)Math.Max(0.05m, ArchivoEdadMaxHoras);
            int fin = Math.Max(0, CurrentBar - 1);      // la ultima vela es del vivo (Hibrido) o se muestra con la ultima foto (Archivo)
            int i = 0, con = 0, sin = 0;
            DateTime ultimaCad = DateTime.MinValue, ultLog = DateTime.UtcNow;
            for (int bar = 0; bar < fin; bar++)
            {
                IndicatorCandle c;
                try { c = GetCandle(bar); } catch { break; }
                if (c == null) continue;
                var abre = Utc(c.Time);
                DateTime cierra;
                try { cierra = Utc(GetCandle(bar + 1).Time); } catch { cierra = abre.AddMinutes(1); }
                if (cierra <= abre) cierra = abre.AddMinutes(1);
                int i0 = i;
                while (i + 1 < arch.Count && arch[i + 1].GeneradoUtc <= cierra) i++;
                var cad = arch[i];
                if (cad.GeneradoUtc > cierra || (cierra - cad.GeneradoUtc).TotalHours > edadMax)
                {
                    sin++;
                    lock (_candado) { _estela[bar] = new double[0]; _fotosBarra.Remove(bar); _guiones.Remove(bar); }
                    continue;
                }
                // un guion por cada cadena que llego DURANTE la vela (varias por vela, como
                // GAMMAlito); la ultima es la que queda como foto y centinela de la vela
                GammaHoyNucleo.Lectura L = null;
                for (int j = Math.Max(i0, i - 12); j <= i; j++)
                {
                    var cj = arch[j];
                    if (j < i && cj.GeneradoUtc <= abre) continue;
                    var Lj = nuc.Calcular(cj, (double)c.Close, j == i ? cierra : cj.GeneradoUtc);
                    if (Lj == null || Lj.SinBase) continue;
                    lock (_candado) AgregarGuiones(bar, Lj.Estela);
                    L = Lj;
                }
                if (L == null) { sin++; continue; }
                con++; ultimaCad = cad.GeneradoUtc;
                var foto = new Foto
                {
                    S = L.S, Futuro = L.Futuro, ZeroVol = L.ZeroVol, ZeroOi = L.ZeroOi, MpVol = L.MpVol, MnVol = L.MnVol, MpOi = L.MpOi, MnOi = L.MnOi,
                    PicoFut = L.PicoFut, ConvEnPrecio = L.ConvEnPrecio, NetVol = L.NetVol, NetOi = L.NetOi, Doms = L.Doms, Cuad = L.Cuadrante, Corto = L.CuadranteCorto,
                    LibroDom = L.LibroDom, LibroConv = L.LibroConv, Mucho = L.Mucho, Mc = L.MaxChange, Vela = abre, Cadena = cad.GeneradoUtc,
                    Perfil = L.Perfil,
                };
                lock (_candado) { _fotosBarra[bar] = foto; _estela[bar] = L.Estela; _marcas[bar] = (L.ZeroVol, new[] { L.MaxChange[4].Fut, L.MaxChange[1].Fut, L.MaxChange[0].Fut }); }
                try { AnotarArchivo(bar, c, L); } catch (Exception e) { Registrar(e); }
                if ((DateTime.UtcNow - ultLog).TotalSeconds >= 10)
                {
                    ultLog = DateTime.UtcNow;
                    Log("REBOBINADO avanza: vela " + bar + " de " + fin + " (" + abre.ToString("yyyy-MM-dd HH:mm") + " UTC), con cadena " + con + ", sin " + sin);
                    try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
                }
            }
            // en Archivo puro la pantalla muestra la ultima foto (no hay vivo)
            if (Fuente == FuenteDatos.Archivo)
            {
                Foto f = null; lock (_candado) { for (int bar = fin; bar >= 0 && f == null; bar--) _fotosBarra.TryGetValue(bar, out f); }
                if (f != null) lock (_candado)
                {
                    _S = f.S; _futuro = f.Futuro; _zeroVol = f.ZeroVol; _zeroOi = f.ZeroOi; _mpVol = f.MpVol; _mnVol = f.MnVol; _mpOi = f.MpOi; _mnOi = f.MnOi;
                    _picoFut = f.PicoFut; _convEnPrecio = f.ConvEnPrecio; _netVol = f.NetVol; _netOi = f.NetOi; _doms = f.Doms; _cuadrante = f.Cuad; _cuadranteCorto = f.Corto;
                    _libroDomUsado = f.LibroDom; _libroConvUsado = f.LibroConv; _mucho = f.Mucho; _maxChange = f.Mc; _baseOrigen = "archivo";
                    _perfil = nuc.Calcular(arch[i], f.Futuro, f.Vela)?.Perfil ?? _perfil;
                }
            }
            _rebConCadena = con; _rebSinCadena = sin; _rebCadenaHora = ultimaCad;
            var es = CultureInfo.GetCultureInfo("es-AR");
            _rebRotulo = (Fuente != FuenteDatos.Archivo ? "archivo " : "REBOBINADO  ") + con.ToString("N0", es) + " velas con cadena, " + sin.ToString("N0", es) + " sin";
            Log("REBOBINADO termino: " + con + " velas con cadena, " + sin + " sin; ultima cadena " + (ultimaCad == DateTime.MinValue ? "--" : ultimaCad.ToString("yyyy-MM-dd HH:mm") + " UTC"));
            try { _centArchivo?.Volcar(true); } catch { }
            try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
        }

        /// <summary>La cadena de opciones de ES armada con lo que llega por Rithmic:
        /// interes abierto (de ayer), IV despejada de las puntas y VOLUMEN DEL DIA por
        /// contrato en tiempo real. Strikes del futuro: base 0. Copiado del
        /// ArmarDesdeViva de Gamma Vivo (2026-09-08).</summary>
        private Feed.Cadena DesdeViva()
        {
            List<CadenaViva.Fila> fs;
            try { fs = _viva.Instantanea(); } catch { return null; }
            if (fs == null || fs.Count == 0) return null;
            var dias = fs.Select(f => Math.Round(f.Dias, 4)).Distinct().OrderBy(x => x).ToList();
            var idx = new Dictionary<double, int>();
            for (int i = 0; i < dias.Count; i++) idx[dias[i]] = i;
            var porClave = new Dictionary<(double, int), Feed.Fila>();
            foreach (var f in fs)
            {
                if (f.IV <= 0 || (f.OI <= 0 && f.VolumenHoy <= 0)) continue;
                int v = idx[Math.Round(f.Dias, 4)];
                if (!porClave.TryGetValue((f.K, v), out var fila)) { fila = new Feed.Fila { K = f.K, V = v }; porClave[(f.K, v)] = fila; }
                if (f.EsCall) { fila.OiC = f.OI; fila.IvC = f.IV; fila.VolC = f.VolumenHoy; }
                else { fila.OiP = f.OI; fila.IvP = f.IV; fila.VolP = f.VolumenHoy; }
            }
            int utiles = porClave.Values.Where(x => x.IvC > 0 && x.IvP > 0).Select(x => x.K).Distinct().Count();
            if (utiles < 12) { _vivaFlaca = utiles; return null; }
            _vivaFlaca = -1;
            var ahora = DateTime.UtcNow;
            return new Feed.Cadena
            {
                Ts = ahora.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), SpotIdx = _viva.Futuro,
                Dias = dias.ToArray(), Filas = porClave.Values.OrderBy(x => x.K).ThenBy(x => x.V).ToList(),
                Base = 0, BaseConfiable = true, EdadMin = 0, UltimoTrade = "", HorizonteCadena = dias.Count > 0 ? dias[dias.Count - 1] : double.NaN,
                RecibidoUtc = ahora, GeneradoUtc = ahora, EsFuturo = true, Fuente = "Rithmic ES",
            };
        }

        /// <summary>La foto de la cadena viva de Rithmic con los mismos campos que
        /// vuelca Gamma Vivo (pythiagex-cadena-viva-<raiz>.json), en una linea.</summary>
        private string VivaJson()
        {
            List<CadenaViva.Fila> fs;
            try { fs = _viva.Instantanea(); } catch { return null; }
            if (fs == null || fs.Count == 0) return null;
            var inv = CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder(1 << 15);
            sb.Append("{\"ts\":\"").Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", inv))
              .Append("\",\"futuro\":").Append(_viva.Futuro.ToString("0.####", inv))
              .Append(",\"grandes\":").Append(_viva.Grandes().Count.ToString(inv))
              .Append(",\"campos\":\"strike,dias,es_call,oi,iv,bid,ask,vol_hoy,vol_cinta,vol_compra,vol_venta\",\"filas\":[");
            bool primero = true;
            foreach (var f in fs)
            {
                if (!primero) sb.Append(','); primero = false;
                sb.Append('[').Append(f.K.ToString("0.##", inv)).Append(',').Append(f.Dias.ToString("0.#####", inv))
                  .Append(',').Append(f.EsCall ? 1 : 0).Append(',').Append(f.OI.ToString("0.#", inv))
                  .Append(',').Append(f.IV.ToString("0.######", inv)).Append(',').Append(f.Bid.ToString("0.####", inv))
                  .Append(',').Append(f.Ask.ToString("0.####", inv)).Append(',').Append(f.VolumenHoy.ToString("0.#", inv))
                  .Append(',').Append(f.VolCinta.ToString("0.#", inv)).Append(',').Append(f.VolCompra.ToString("0.#", inv))
                  .Append(',').Append(f.VolVenta.ToString("0.#", inv)).Append(']');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>La vela bajo el mouse, o -1. En rebobinado la escalera y las
        /// rayas muestran ESA vela: asi se revisa cualquier hora del archivo.</summary>
        private int BarraBajoMouse()
        {
            try
            {
                var m = ChartInfo?.MouseLocationInfo;
                if (m == null) return -1;
                int b = m.BarBelowMouse;
                return (b >= 0 && b < CurrentBar) ? b : -1;
            }
            catch { return -1; }
        }

        // ==================================================================
        // La cuenta: la hace el nucleo. Aca solo se eligen cadena, precio y hora
        // ==================================================================
        private void Repreciar()
        {
            decimal cierre;
            try { cierre = GetCandle(Math.Max(0, CurrentBar - 1)).Close; } catch { return; }
            if (cierre <= 0) return;
            RepreciarCon(_c, (double)cierre, DateTime.UtcNow, Math.Max(0, CurrentBar - 1));
        }

        private void RepreciarCon(Feed.Cadena c, double futuro, DateTime ahoraUtc, int barra)
        {
            if (c == null) return;
            var a = _nucleo.A;
            a.Tasa = (double)Tasa;
            a.Horizonte = (GammaHoyNucleo.HorizonteVenc)(int)Horizonte;
            a.CuantasDominantes = CuantasDominantes;
            a.RadioDominantesPct = (double)RadioDominantesPct;
            a.PicoRadioPct = (double)PicoRadioPct;
            a.MuchoPct = MuchoPct;
            a.Convexidad = (GammaHoyNucleo.LibroConv)(int)Convexidad;
            a.Centroide = DominanteCentroide; a.RadioCentroidePts = (double)RadioCentroidePts;

            var L = _nucleo.Calcular(c, futuro, ahoraUtc);
            if (L == null) return;
            if (L.SinBase) { lock (_candado) { _baseOrigen = "sin base"; } return; }
            if (L.TransicionNueva) Log("TRANSICION " + L.Alerta);

            lock (_candado)
            {
                _perfil = L.Perfil; _S = L.S; _futuro = L.Futuro; _base = L.Base; _baseOrigen = L.BaseOrigen; _masCercaUlt = L.MasCerca;
                _zeroVol = L.ZeroVol; _zeroOi = L.ZeroOi; _netVol = L.NetVol; _netOi = L.NetOi;
                _mpVol = L.MpVol; _mnVol = L.MnVol; _mpOi = L.MpOi; _mnOi = L.MnOi;
                _maxAbsVol = L.MaxAbsVol; _maxAbsOi = L.MaxAbsOi; _maxAbsConv = L.MaxAbsConv;
                _doms = L.Doms; _libroConvUsado = L.LibroConv; _libroDomUsado = L.LibroDom;
                _cuadrante = L.Cuadrante; _cuadranteCorto = L.CuadranteCorto; _cuadranteN = L.CuadranteN;
                _picoFut = L.PicoFut; _picoGex = L.PicoGex; _convEnPrecio = L.ConvEnPrecio; _mucho = L.Mucho;
                _alerta = L.Alerta; _alertaHasta = L.AlertaHasta;
                _maxChange = L.MaxChange;
                _estela[barra] = L.Estela;
                AgregarGuiones(barra, L.Estela);
                _marcas[barra] = (L.ZeroVol, new[] { L.MaxChange[4].Fut, L.MaxChange[1].Fut, L.MaxChange[0].Fut });
                if (_estela.Count > 6000) foreach (var k in _estela.Keys.Where(k => k < barra - 5000).ToList()) { _estela.Remove(k); _marcas.Remove(k); _guiones.Remove(k); }
            }

            if ((DateTime.UtcNow - _ultimoAudit).TotalSeconds >= 60)
            {
                _ultimoAudit = DateTime.UtcNow;
                Log(GammaHoyNucleo.Audit(L, c, _viva.Activa));
            }
        }

        // ==================================================================
        // Lo que se anota para el laboratorio
        // ==================================================================
        private void Anotar(int bar)
        {
            if (!AnotarCentinela || bar <= _barraCent) return;
            int cerrada = bar - 1;
            if (cerrada < 1) { _barraCent = bar; return; }
            _barraCent = bar;
            if (_cent == null)
            {
                var instr = InstrumentInfo != null ? InstrumentInfo.Instrument : "x";
                var marco = ChartInfo != null && ChartInfo.ChartType != null ? ChartInfo.ChartType + "-" + ChartInfo.TimeFrame : "x";
                _cent = new Centinela("hoy-" + instr, marco);
            }
            IndicatorCandle c;
            try { c = GetCandle(cerrada); } catch { return; }
            if (c == null) return;
            var niv = new List<KeyValuePair<string, double>>();
            double sp;
            lock (_candado)
            {
                sp = _S;
                var L = new GammaHoyNucleo.Lectura
                {
                    ZeroVol = _zeroVol, ZeroOi = _zeroOi, MpVol = _mpVol, MnVol = _mnVol, MpOi = _mpOi, MnOi = _mnOi,
                    Doms = _doms, MaxChange = _maxChange, PicoFut = _picoFut, CuadranteN = _cuadranteN
                };
                niv = GammaHoyNucleo.Niveles(L);
            }
            _cent.Anotar(cerrada, c.LastTime != default(DateTime) ? c.LastTime : c.Time,
                (double)c.Open, (double)c.High, (double)c.Low, (double)c.Close,
                (double)c.Volume, (double)c.Ticks, (double)c.Delta, sp, niv);
        }

        // ==================================================================
        // Dibujo
        // ==================================================================
        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            try { Pintar(g); }
            catch (Exception e) { Registrar(e); }
        }

        private void Pintar(RenderContext g)
        {
            if (ChartInfo == null) return;
            var cont = ChartInfo.PriceChartContainer;
            if (cont == null) return;
            try { g.SetSmoothingMode(OFT.Rendering.Context.RenderSmoothingModes.AntiAlias); } catch { }
            var area = ChartArea;
            int xr = area.Right;
            try { var cb = g.ClipBounds; if (cb.Width > 0) xr = Math.Min(area.Right, cb.Right); } catch { }
            int piso = area.Bottom - Math.Max(0, MargenInferior);
            var es = CultureInfo.GetCultureInfo("es-AR");
            var f = new RenderFont("Consolas", (float)Math.Max(6m, Math.Min(14m, TamLetra)));
            var fChica = new RenderFont("Consolas", (float)Math.Max(6m, Math.Min(12m, TamLetra - 1m)));

            if (_renders++ == 0) Log("primer render: area=" + area + " clip=" + g.ClipBounds);

            // copia del estado bajo llave
            List<Strike> perfil; double S, futuro, zeroVol, zeroOi, mpVol, mnVol, mpOi, mnOi, maxV, maxO, maxC, netVol, netOi;
            List<(double Fut, double Gex)> doms; string cuad, corto, origenBase, libroConv, libroDom, alerta; DateTime alertaHasta;
            (double Fut, double Delta)[] mc; bool mucho; double convPrecio, picoFut;
            Foto foto = null; int barFoto = -1;
            lock (_candado)
            {
                perfil = _perfil; S = _S; futuro = _futuro; zeroVol = _zeroVol; zeroOi = _zeroOi;
                mpVol = _mpVol; mnVol = _mnVol; mpOi = _mpOi; mnOi = _mnOi; maxV = _maxAbsVol; maxO = _maxAbsOi; maxC = _maxAbsConv;
                netVol = _netVol; netOi = _netOi; doms = _doms; cuad = _cuadrante; corto = _cuadranteCorto; origenBase = _baseOrigen;
                libroConv = _libroConvUsado; libroDom = _libroDomUsado; alerta = _alerta; alertaHasta = _alertaHasta;
                mc = _maxChange; mucho = _mucho; convPrecio = _convEnPrecio; picoFut = _picoFut;
                if (Fuente != FuenteDatos.Vivo)
                {
                    barFoto = BarraBajoMouse();
                    if (barFoto >= 0 && _fotosBarra.TryGetValue(barFoto, out foto))
                    {
                        S = foto.S; futuro = foto.Futuro; zeroVol = foto.ZeroVol; zeroOi = foto.ZeroOi; mpVol = foto.MpVol; mnVol = foto.MnVol;
                        mpOi = foto.MpOi; mnOi = foto.MnOi; netVol = foto.NetVol; netOi = foto.NetOi; doms = foto.Doms; cuad = foto.Cuad; corto = foto.Corto;
                        libroConv = foto.LibroConv; libroDom = foto.LibroDom; mc = foto.Mc; mucho = foto.Mucho; convPrecio = foto.ConvEnPrecio; picoFut = foto.PicoFut;
                        alerta = "";
                        if (foto.Perfil != null && foto.Perfil.Count > 0)
                        {
                            perfil = foto.Perfil;
                            maxV = perfil.Max(z => Math.Abs(z.GexVol)); maxO = perfil.Max(z => Math.Abs(z.GexOi)); maxC = perfil.Max(z => Math.Abs(z.Conv));
                        }
                    }
                    else foto = null;
                }
            }

            var c = _c;
            // ---- cabecera: siempre, aunque no haya datos, para que se sepa por que
            string edad = c == null ? "sin feed" : (c.EsFuturo ? "en tiempo real" : (c.EdadMin + 902.0 / 60.0).ToString("0", es) + " min tarde");
            string l1 = perfil.Count == 0
                ? "GAMMA HOY  esperando cadena" + (string.IsNullOrEmpty(_error) ? "" : " (" + _error + ")")
                : "GAMMA HOY  " + corto + "  " + cuad + "   conv " + (convPrecio >= 0 ? "+" : "-") + " (" + libroConv + ")  pico " + (double.IsNaN(picoFut) ? "--" : picoFut.ToString("N0", es)) + (mucho ? " mucho" : " poco");
            string l2 = (c != null && c.EsFuturo ? "libro ES Rithmic " + edad + " · " + c.Filas.Count + " filas" : "vol CBOE " + edad) + " · OI de ayer · base " + origenBase + " · dominantes por " + libroDom
                      + (Libro == LibroEnVivo.Rithmic_ES && _vivaFlaca >= 0 ? " · RITHMIC FLACO: " + _vivaFlaca + " strikes con puntas, sigo con CBOE" : "")
                      + (_viva.Activa ? " · vivo Rithmic " + ((int)_viva.VolumenTotalHoy()).ToString("N0", es) + " contr" : " · vivo: " + _viva.Estado);
            if (Fuente != FuenteDatos.Archivo)
            {
                if (foto != null)
                    l2 = "vela " + foto.Vela.ToString("yyyy-MM-dd HH:mm") + " UTC · cadena publicada " + foto.Cadena.ToString("HH:mm") + " UTC · fut " + foto.Futuro.ToString("N2", es) + " · dominantes por " + libroDom + " · MOUSE sobre la vela (archivo)";
                else
                    l2 += " · " + (_archivoListo ? (string.IsNullOrEmpty(_rebRotulo) ? "archivo recorriendo..." : _rebRotulo.Replace("HIBRIDO  ", "")) : (_archivoCargando ? "archivo cargando..." : "archivo esperando velas"));
            }
            if (Fuente == FuenteDatos.Archivo)
            {
                string estado = _archivoListo ? (string.IsNullOrEmpty(_rebRotulo) ? "REBOBINADO  recorriendo el grafico..." : _rebRotulo)
                              : (_archivoCargando ? "REBOBINADO  cargando el archivo de cadenas..." : "REBOBINADO  esperando la primera vela");
                l1 = estado + (perfil.Count == 0 ? "" : "  |  " + corto + "  " + cuad + "  pico " + (double.IsNaN(picoFut) ? "--" : picoFut.ToString("N0", es)));
                l2 = foto != null
                    ? "vela " + foto.Vela.ToString("yyyy-MM-dd HH:mm") + " UTC · cadena publicada " + foto.Cadena.ToString("HH:mm") + " UTC (" + ((foto.Vela - foto.Cadena).TotalMinutes).ToString("0", es) + " min antes) · fut " + foto.Futuro.ToString("N2", es) + " · dominantes por " + libroDom + " · MOUSE sobre la vela"
                    : "ultima vela · cadena " + (_rebCadenaHora == DateTime.MinValue ? "--" : _rebCadenaHora.ToString("yyyy-MM-dd HH:mm") + " UTC") + " · base " + origenBase + " · sin vivo · pasa el mouse por una vela para ver su escalera";
            }
            int yc = area.Top + 26;
            var m1 = g.MeasureString(l1, f); var m2 = g.MeasureString(l2, fChica);
            int wc = Math.Max(m1.Width, m2.Width) + 12;
            g.FillRectangle(Color.FromArgb(200, ColFondo), new Rectangle(area.Left + 6, yc - 3, wc, m1.Height + m2.Height + 8));
            g.DrawString(l1, f, perfil.Count == 0 ? ColAviso : (convPrecio >= 0 ? ColConvPos : ColConvNeg), area.Left + 12, yc);
            g.DrawString(l2, fChica, Color.FromArgb(170, ColTexto), area.Left + 12, yc + m1.Height + 2);
            if (DateTime.UtcNow < alertaHasta && alerta.Length > 0)
            {
                var ma = g.MeasureString("TRANSICION: " + alerta, f);
                int ya = yc + m1.Height + m2.Height + 10;
                g.FillRectangle(Color.FromArgb(215, 60, 35, 10), new Rectangle(area.Left + 6, ya - 2, ma.Width + 12, ma.Height + 4));
                g.DrawString("TRANSICION: " + alerta, f, ColAviso, area.Left + 12, ya);
            }
            if (perfil.Count == 0 || double.IsNaN(futuro)) return;

            int x0 = area.Left;
            int ancho = Math.Max(30, AnchoBarras);
            int xLad = VerEscalera ? xr - Math.Max(80, AnchoEscalera) : xr;
            int xConv = xLad - 6;                       // borde derecho de la convexidad
            int xl0 = x0 + ancho + 8, xl1 = (VerConvexidad ? xConv - (int)(ancho * 0.7) - 8 : xLad - 8);
            if (xl1 - xl0 < 60) { xl0 = x0 + 2; xl1 = xLad - 2; }

            // ---- barras: sombra de OI, volumen encima, convexidad a la derecha
            int alto = 5;
            try { int y1 = cont.GetYByPrice((decimal)perfil[0].Fut, false); if (perfil.Count > 1) { int y2 = cont.GetYByPrice((decimal)perfil[1].Fut, false); alto = Math.Max(2, Math.Min(9, Math.Abs(y2 - y1) - 2)); } } catch { }
            var fotos = _nucleo.FotosCopia();
            // espacio entre filas (px): decide cuanto dato entra sin pisarse
            int esp = 0;
            try { if (perfil.Count > 1) esp = Math.Abs(cont.GetYByPrice((decimal)perfil[1].Fut, false) - cont.GetYByPrice((decimal)perfil[0].Fut, false)); } catch { }
            var fRot = new RenderFont("Consolas", (float)Math.Max(6m, Math.Min(11m, TamLetra - 1m)));
            int altoRot = g.MeasureString("0", fRot).Height;
            bool rotAuto = DatosEnBarras == RotulosBarras.Auto, rotSiempre = DatosEnBarras == RotulosBarras.Siempre;
            // PROFESIONAL = POCO: en Auto se rotulan solo las barras que importan (las 3
            // mas grandes de cada lado del libro, las dominantes y los majors), en una
            // linea, y solo si la fila tiene lugar. "Siempre" rotula todas con dos lineas.
            bool rotTodas = rotSiempre && esp >= altoRot + 1;
            bool rotDos = rotSiempre && esp >= 2 * altoRot + 2;
            var elegidos = new HashSet<double>();
            foreach (var dm in doms) elegidos.Add(dm.Fut);
            if (!double.IsNaN(mpVol)) elegidos.Add(mpVol); if (!double.IsNaN(mnVol)) elegidos.Add(mnVol);
            foreach (var z in perfil.Where(z => z.GexVol > 0).OrderByDescending(z => z.GexVol).Take(3)) elegidos.Add(z.Fut);
            foreach (var z in perfil.Where(z => z.GexVol < 0).OrderBy(z => z.GexVol).Take(3)) elegidos.Add(z.Fut);
            bool rotHayLugar = esp >= altoRot + 1;
            string Km(double v) => Math.Abs(v) >= 1e6 ? (v / 1e6).ToString("0.0", es) + "M" : Math.Abs(v) >= 1e3 ? (v / 1e3).ToString("0.0", es) + "k" : v.ToString("0", es);
            string BmR(double v) => Math.Abs(v) >= 1e9 ? (v / 1e9).ToString("+0.0;-0.0", es) + "B" : Math.Abs(v) >= 1e6 ? (v / 1e6).ToString("+0;-0", es) + "M" : (v / 1e3).ToString("+0;-0", es) + "k";
            // titulo del perfil: que libro y que vencimiento
            if (DatosEnBarras != RotulosBarras.Nunca)
            {
                double mc0 = double.NaN; lock (_candado) mc0 = _masCercaUlt;
                string venc = double.IsNaN(mc0) ? "" : (mc0 < 1.0 ? "0DTE" : mc0 < 2 ? "1 dia" : mc0.ToString("0", es) + " dias");
                string tit = "GEX " + (libroDom == "vol" ? "volumen hoy" : "OI") + (venc.Length > 0 ? " · " + venc : "") + (VerSombraOI ? " · sombra OI" : "");
                g.DrawString(tit, fRot, Color.FromArgb(150, ColTexto), x0 + 2, area.Top + 8);
            }
            foreach (var s in perfil)
            {
                int y; try { y = cont.GetYByPrice((decimal)s.Fut, false); } catch { continue; }
                if (y < area.Top || y > piso) continue;
                bool rotEsta = DatosEnBarras != RotulosBarras.Nunca && rotHayLugar && (rotTodas || elegidos.Contains(s.Fut));
                if (VerSombraOI && maxO > 0 && Math.Abs(s.GexOi) > 0)
                {
                    int w = Math.Max(1, (int)(Math.Sqrt(Math.Abs(s.GexOi) / maxO) * ancho));
                    g.FillRectangle(Color.FromArgb(55, s.GexOi >= 0 ? ColPos : ColNeg), new Rectangle(x0, y - alto / 2 - 1, w, alto + 2));
                }
                if (maxV > 0 && Math.Abs(s.GexVol) > 0)
                {
                    double fr = Math.Sqrt(Math.Abs(s.GexVol) / maxV);
                    int w = Math.Max(1, (int)(fr * ancho));
                    var col = s.GexVol >= 0 ? ColPos : ColNeg;
                    g.FillRectangle(Color.FromArgb((int)(120 + 120 * fr), col), new Rectangle(x0, y - alto / 2, w, alto));
                    if (rotEsta)
                    {
                        // el dato de la barra, a la derecha de la punta: GEX del libro que dibuja
                        // (y abajo, si hay lugar: OI, volumen del dia e IV media)
                        string l1r = BmR(s.GexVol) + (VerSombraOI && Math.Abs(s.GexOi) > 0 ? " oi" + BmR(s.GexOi) : "");
                        string l2r = "OI " + Km(s.Oi) + " v " + Km(s.VolHoy) + (double.IsNaN(s.IvMedia) ? "" : " iv" + (s.IvMedia * 100).ToString("0", es));
                        int xr0 = x0 + w + 4;
                        var m1r = g.MeasureString(l1r, fRot);
                        g.FillRectangle(Color.FromArgb(150, ColFondo), new Rectangle(xr0 - 1, y - altoRot / 2, m1r.Width + 2, rotDos ? altoRot * 2 : altoRot));
                        g.DrawString(l1r, fRot, Color.FromArgb(235, col), xr0, y - altoRot / 2);
                        if (rotDos) g.DrawString(l2r, fRot, Color.FromArgb(175, ColTexto), xr0, y + altoRot / 2);
                    }
                    if (VerPelotitas)
                    {
                        // donde estaba la punta hace 15, 5 y 1 minutos: adentro = crece, afuera = decrece
                        long ahora = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
                        int[] vent = { 15, 5, 1 }; int[] rad = { 4, 3, 2 };
                        for (int i = 0; i < 3; i++)
                        {
                            var vieja = fotos.Where(z => z.Minuto <= ahora - vent[i]).LastOrDefault();
                            if (vieja == null || !vieja.GexVol.TryGetValue(s.Fut, out var gAntes)) continue;
                            int wa = Math.Max(0, (int)(Math.Sqrt(Math.Abs(gAntes) / maxV) * ancho));
                            int rr = rad[i];
                            g.FillEllipse(Color.FromArgb(235, 250, 250, 250), new Rectangle(x0 + wa - rr, y - rr, 2 * rr, 2 * rr));
                            g.DrawEllipse(new RenderPen(Color.FromArgb(200, col), 1f), new Rectangle(x0 + wa - rr, y - rr, 2 * rr, 2 * rr));
                        }
                    }
                }
                if (VerConvexidad && maxC > 0 && Math.Abs(s.Conv) > 0)
                {
                    double fr = Math.Sqrt(Math.Abs(s.Conv) / maxC);
                    int w = Math.Max(1, (int)(fr * ancho * 0.7));
                    var col = s.Conv >= 0 ? ColConvPos : ColConvNeg;
                    g.FillRectangle(Color.FromArgb((int)(110 + 120 * fr), col), new Rectangle(xConv - w, y - alto / 2, w, alto));
                    if (rotEsta)
                    {
                        // la convexidad de la barra: cuanto cambia su GEX si el precio sube 1 %
                        string lc = BmR(s.Conv);
                        var mc1 = g.MeasureString(lc, fRot);
                        int xc0 = xConv - w - 4 - mc1.Width;
                        g.FillRectangle(Color.FromArgb(150, ColFondo), new Rectangle(xc0 - 1, y - altoRot / 2, mc1.Width + 2, altoRot));
                        g.DrawString(lc, fRot, Color.FromArgb(225, col), xc0, y - altoRot / 2);
                    }
                }
            }
            g.DrawString("volumen hoy · sombra OI ayer", fChica, Color.FromArgb(110, ColTexto), x0 + 4, area.Top + 8);
            if (VerConvexidad) { var mcx = g.MeasureString("convexity ladder (" + libroConv + ")", fChica); g.DrawString("convexity ladder (" + libroConv + ")", fChica, Color.FromArgb(110, ColTexto), xConv - mcx.Width, area.Top + 8); }

            // ---- rayas: zero de cada libro, majors del volumen, dominantes
            int xRaya = int.MinValue;
            if (foto != null) { try { xRaya = cont.GetXByBar(barFoto, false); } catch { xRaya = int.MinValue; } }
            void Raya(double p, Color col, float w, System.Drawing.Drawing2D.DashStyle ds, int alfa)
            {
                if (double.IsNaN(p) || p <= 0) return;
                if (Rayas == EstiloRayas.Ninguna) return;
                if (Rayas == EstiloRayas.Tenues) { alfa = Math.Max(30, (int)(alfa * 0.3)); w = Math.Max(1f, w - 0.4f); }
                int y; try { y = cont.GetYByPrice((decimal)p, false); } catch { return; }
                if (y < area.Top || y > piso) return;
                // en rebobinado la raya nace en la vela del mouse: se ve desde
                // donde regia ese nivel hacia adelante, no una linea de punta a punta
                int xa = xRaya != int.MinValue ? Math.Max(xl0, Math.Min(xl1 - 4, xRaya)) : xl0;
                g.DrawLine(new RenderPen(Color.FromArgb(alfa, col), w, ds), xa, y, xl1, y);
                if (xRaya != int.MinValue) g.FillEllipse(Color.FromArgb(alfa, col), new Rectangle(xa - 3, y - 3, 6, 6));
            }
            Raya(zeroOi, Color.FromArgb(160, 160, 170), 1f, System.Drawing.Drawing2D.DashStyle.Dot, 120);
            Raya(zeroVol, ColZero, 1.4f, System.Drawing.Drawing2D.DashStyle.Dash, 200);
            Raya(mpVol, ColPos, 1.6f, System.Drawing.Drawing2D.DashStyle.Solid, 190);
            Raya(mnVol, ColNeg, 1.6f, System.Drawing.Drawing2D.DashStyle.Solid, 190);
            for (int i = 0; i < doms.Count; i++) Raya(doms[i].Fut, ColDom, i == 0 ? 1.6f : 1.1f, System.Drawing.Drawing2D.DashStyle.Solid, i == 0 ? 220 : 160);

            // ---- estela: la dominante que regia en cada vela
            if (VerEstela || VerSemillas || VerZeroPorVela)
            {
                Dictionary<int, double[]> est; Dictionary<int, (double Zero, double[] Mc)> mar; Dictionary<int, List<(double Fut, int Rango)>> gui;
                lock (_candado)
                {
                    est = new Dictionary<int, double[]>(_estela); mar = new Dictionary<int, (double, double[])>(_marcas);
                    gui = _guiones.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
                }
                int desde = Math.Max(0, FirstVisibleBarNumber), hasta = Math.Min(CurrentBar - 1, LastVisibleBarNumber);
                // ancho de una vela en pixeles, medido en el grafico (no supuesto)
                int bw = 5;
                try { if (hasta > desde) bw = Math.Max(3, (cont.GetXByBar(hasta, false) - cont.GetXByBar(desde, false)) / Math.Max(1, hasta - desde)); } catch { }
                int grueso = Math.Max(1, GrosorGuion), fino = Math.Max(1, GrosorGuion - 1);
                for (int b = desde; b <= hasta; b++)
                {
                    int x; try { x = cont.GetXByBar(b, false); } catch { continue; }
                    // GAMMAlito: la dominante es un GUION amarillo por vela, primaria gruesa y secundaria fina.
                    // Puesto uno al lado del otro forman la linea sola: se ve donde nacio y cuando salto.
                    if (VerEstela && gui.TryGetValue(b, out var lg))
                        foreach (var (fut, rango) in lg)
                        {
                            int y; try { y = cont.GetYByPrice((decimal)fut, false); } catch { continue; }
                            if (y < area.Top || y > piso) continue;
                            int h = rango == 0 ? grueso : fino;
                            g.FillRectangle(Color.FromArgb(rango == 0 ? 230 : 170, rango == 0 ? ColDom : ColDom2), new Rectangle(x - bw / 2, y - h / 2, bw, h));
                        }
                    if (mar.TryGetValue(b, out var m))
                    {
                        if (VerZeroPorVela && !double.IsNaN(m.Zero))
                        {
                            int y; try { y = cont.GetYByPrice((decimal)m.Zero, false); } catch { y = int.MinValue; }
                            if (y >= area.Top && y <= piso) g.FillEllipse(Color.FromArgb(150, ColZero), new Rectangle(x - 1, y - 1, 3, 3));
                        }
                        // las semillas: el strike de mayor cambio a 30 (grande), 5 (mediana) y 1 min (chica)
                        // las semillas solo en las ultimas 90 velas (lo "adelantado" es de ahora,
                        // no de hace tres dias) y chicas: no compiten con las velas
                        if (VerSemillas && m.Mc != null && b >= CurrentBar - 90)
                            for (int i = 0; i < m.Mc.Length && i < 3; i++)
                            {
                                if (double.IsNaN(m.Mc[i]) || m.Mc[i] <= 0) continue;
                                int y; try { y = cont.GetYByPrice((decimal)m.Mc[i], false); } catch { continue; }
                                if (y < area.Top || y > piso) continue;
                                int r = i == 0 ? 2 : 1;
                                g.FillEllipse(Color.FromArgb(i == 0 ? 190 : (i == 1 ? 140 : 100), ColAviso), new Rectangle(x - r, y - r, 2 * r + 1, 2 * r + 1));
                            }
                    }
                }
            }

            // ---- big trades de opciones, sobre la vela del momento
            if (VerBigTrades && _viva.Activa)
            {
                foreach (var t in _viva.Grandes())
                {
                    int b = BarraDe(t.Hora); if (b < 0) continue;
                    int x; try { x = cont.GetXByBar(b, false); } catch { continue; }
                    double pr; try { pr = (double)GetCandle(b).Close; } catch { continue; }
                    int y; try { y = cont.GetYByPrice((decimal)pr, false); } catch { continue; }
                    if (y < area.Top || y > piso || x < xl0 || x > xl1) continue;
                    var col = t.EsCall ? ColPos : ColNeg;
                    int rr = 9;
                    g.FillEllipse(Color.FromArgb(150, col), new Rectangle(x - rr, y - rr, 2 * rr, 2 * rr));
                    var tx = ((int)t.Contratos).ToString(es); var mt = g.MeasureString(tx, fChica);
                    g.DrawString(tx, fChica, Color.White, x - mt.Width / 2, y - mt.Height / 2);
                }
            }

            // ---- la escalera pegada al eje
            if (VerEscalera) Escalera(g, cont, area, xLad, xr, piso, f, fChica, futuro, zeroVol, zeroOi, mpVol, mnVol, doms, mc, netVol, netOi);
        }

        private int BarraDe(DateTime horaUtc)
        {
            try
            {
                for (int b = CurrentBar - 1; b >= Math.Max(0, CurrentBar - 600); b--)
                {
                    var c = GetCandle(b);
                    var t0 = c.Time.Kind == DateTimeKind.Utc ? c.Time : c.Time.ToUniversalTime();
                    if (t0 <= horaUtc) return b;
                }
            }
            catch { }
            return -1;
        }

        private void Escalera(RenderContext g, IChartContainer cont, Rectangle area, int xLad, int xr, int piso,
                              RenderFont f, RenderFont fChica, double futuro, double zeroVol, double zeroOi, double mpVol, double mnVol,
                              List<(double Fut, double Gex)> doms, (double Fut, double Delta)[] mc, double netVol, double netOi)
        {
            var es = CultureInfo.GetCultureInfo("es-AR");
            var rect = new Rectangle(xLad, area.Top, xr - xLad, Math.Max(40, piso - area.Top));
            g.FillRectangle(Color.FromArgb(235, ColFondo), rect);
            g.DrawLine(new RenderPen(Color.FromArgb(90, ColTexto), 1f), rect.Left, rect.Top, rect.Left, rect.Bottom);

            int hf = g.MeasureString("X", f).Height;
            int yc = rect.Top + 26;
            string Bm(double v) => Math.Abs(v) >= 1e9 ? (v / 1e9).ToString("+0.0;-0.0", es) + "B" : (v / 1e6).ToString("+0;-0", es) + "M";
            g.DrawString("net vol " + Bm(netVol), f, netVol >= 0 ? ColPos : ColNeg, rect.Left + 6, yc); yc += hf + 1;
            g.DrawString("net OI  " + Bm(netOi), fChica, Color.FromArgb(160, ColTexto), rect.Left + 6, yc); yc += hf + 1;
            // max change: strike con mayor cambio de GEX por volumen
            for (int i = 0; i < mc.Length; i++)
            {
                if (double.IsNaN(mc[i].Fut) || mc[i].Delta == 0) continue;
                string t = "Δ" + GammaHoyNucleo.Ventanas[i].ToString(es).PadLeft(2) + "' " + mc[i].Fut.ToString("N0", es) + " " + Bm(mc[i].Delta);
                g.DrawString(t, fChica, mc[i].Delta >= 0 ? ColPos : ColNeg, rect.Left + 6, yc); yc += hf;
            }
            yc += 4;
            g.DrawLine(new RenderPen(Color.FromArgb(60, ColTexto), 1f), rect.Left + 4, yc, rect.Right - 4, yc); yc += 4;

            // filas a la altura de su precio, como un DOM, sin pisarse
            var filas = new List<(string N, double P, Color C, bool esPrecio)>();
            void Add(string n, double p, Color c) { if (!double.IsNaN(p) && p > 0) filas.Add((n, p, c, false)); }
            Add("0Γ vol", zeroVol, ColZero); Add("0Γ ayer", zeroOi, Color.FromArgb(160, 160, 170));
            Add("+Γ", mpVol, ColPos); Add("−Γ", mnVol, ColNeg);
            for (int i = 0; i < doms.Count; i++) Add("D" + (i + 1), doms[i].Fut, ColDom);
            filas.Add(("", futuro, Color.FromArgb(31, 143, 124), true));
            filas.Sort((a, b) => b.P.CompareTo(a.P));
            int n = filas.Count; var y = new int[n]; var alt = new int[n]; var enPant = new bool[n];
            int yTop = yc, yBot = rect.Bottom - 4;
            for (int i = 0; i < n; i++)
            {
                alt[i] = hf + 4;
                int yp = int.MinValue; try { yp = cont.GetYByPrice((decimal)filas[i].P, false); } catch { }
                enPant[i] = yp != int.MinValue && yp >= area.Top && yp <= area.Bottom;
                y[i] = enPant[i] ? yp - alt[i] / 2 : (yp != int.MinValue && yp < area.Top ? yTop : yBot - alt[i]);
            }
            int minY = yTop; for (int i = 0; i < n; i++) { if (y[i] < minY) y[i] = minY; minY = y[i] + alt[i] + 1; }
            int maxY = yBot; for (int i = n - 1; i >= 0; i--) { if (y[i] + alt[i] > maxY) y[i] = maxY - alt[i]; maxY = y[i] - 1; }
            for (int i = 0; i < n; i++)
            {
                if (y[i] < yTop) continue;
                var q = filas[i];
                if (q.esPrecio)
                {
                    g.FillRectangle(q.C, new Rectangle(rect.Left + 2, y[i], rect.Width - 4, alt[i] - 1));
                    g.DrawString("▶ " + futuro.ToString("N2", es), f, Color.White, rect.Left + 6, y[i] + 1);
                    continue;
                }
                double dist = q.P - futuro;
                string izq = q.N + " " + q.P.ToString("N0", es) + " " + (dist >= 0 ? "+" : "") + dist.ToString("0", es);
                g.FillRectangle(Color.FromArgb(230, q.C), new Rectangle(rect.Left + 3, y[i] + 2, 3, hf - 2));
                g.DrawString(izq, f, Color.FromArgb(240, ColTexto), rect.Left + 9, y[i] + 1);
                if (enPant[i]) g.FillRectangle(Color.FromArgb(230, q.C), new Rectangle(rect.Right - 5, y[i] + hf / 2, 5, 2));
            }
        }

        // ==================================================================
        // Registro
        // ==================================================================
        private static void Log(string msg)
        {
            try
            {
                var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "pythiagex-gammahoy.log");
                File.AppendAllText(p, DateTime.Now.ToString("s") + "  " + msg + "\n");
            }
            catch { }
        }

        private static void Registrar(Exception e)
        {
            Log("EXCEPCION " + e.GetType().Name + ": " + e.Message + " | " + (e.StackTrace ?? "").Replace("\n", " ").Substring(0, Math.Min(300, (e.StackTrace ?? "").Length)));
        }
    }
}
