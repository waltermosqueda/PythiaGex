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
        public FuenteDatos Fuente { get; set; } = FuenteDatos.Vivo;

        [Display(Name = "Archivo: bajar de la nube los dias que falten", GroupName = "1. Datos", Order = 10)]
        public bool BajarArchivo { get; set; } = true;

        [Display(Name = "Archivo: edad maxima de la cadena (horas)", GroupName = "1. Datos", Order = 11,
                 Description = "De noche la cadena de CBOE se congela y el indicador en vivo sigue mostrando la ultima: 20 horas reproduce eso. Con 0,3 (20 min) solo hay niveles en la rueda americana.")]
        public decimal ArchivoEdadMaxHoras { get; set; } = 20m;

        [Display(Name = "Feed (url base)", GroupName = "1. Datos", Order = 1)]
        public string Url { get; set; } = "https://waltermosqueda.github.io/PythiaGex/datos/atas/";

        [Display(Name = "Raiz manual (ES, NQ, RTY; vacio = del grafico)", GroupName = "1. Datos", Order = 2)]
        public string RaizManual { get; set; } = "";

        [Display(Name = "Refresco del feed (s)", GroupName = "1. Datos", Order = 3)]
        [Range(60, 3600)]
        public int SegundosRefresco { get; set; } = 120;

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
        // centinela
        private Centinela _cent;
        private int _barraCent = -1;
        // rebobinado (Fuente = Archivo)
        private sealed class Foto
        {
            public double S, Futuro, ZeroVol, ZeroOi, MpVol, MnVol, MpOi, MnOi, PicoFut, ConvEnPrecio, NetVol, NetOi;
            public List<(double Fut, double Gex)> Doms; public string Cuad, Corto, LibroDom, LibroConv; public bool Mucho;
            public (double Fut, double Delta)[] Mc; public DateTime Vela, Cadena;
        }
        private readonly Dictionary<int, Foto> _fotosBarra = new();
        private List<Feed.Cadena> _archivo;
        private int _iArchivo, _barraReb = -1, _rebConCadena, _rebSinCadena;
        private volatile bool _archivoCargando, _archivoListo;
        private Centinela _centArchivo;
        private int _barraVivaUlt = -1;
        private string _rebRotulo = "";
        private DateTime _rebCadenaHora = DateTime.MinValue, _rebUltimoLog = DateTime.MinValue;
        private DateTime _ultimoAudit = DateTime.MinValue;
        private int _renders;

        private static readonly Color ColPos = Color.FromArgb(45, 220, 130);
        private static readonly Color ColNeg = Color.FromArgb(235, 60, 60);
        private static readonly Color ColConvPos = Color.FromArgb(93, 217, 208);
        private static readonly Color ColConvNeg = Color.FromArgb(168, 107, 255);
        private static readonly Color ColDom = Color.FromArgb(232, 200, 60);
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

            _periodo = TimeSpan.FromSeconds(30);
            _tick = () =>
            {
                var ahora = DateTime.UtcNow;
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
                // HIBRIDO: el archivo se carga desde aca (con el mercado cerrado no hay OnCalculate)
                if (Fuente == FuenteDatos.Hibrido && !_archivoListo && !_archivoCargando && CurrentBar > 0)
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
                    try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
                };
                SubscribeToTimer(_periodo, _tick);
                Log("Gamma Hoy 0.3b arranca en REBOBINADO. raiz=" + Raiz() + " horizonte=" + Horizonte + " carpeta=" + Feed.Archivo.Carpeta);
                return;
            }
            SubscribeToTimer(_periodo, _tick);
            _ = Reloj.Medir(Log);
            _ultimaBajada = DateTime.UtcNow;
            _ultimoIntentoViva = DateTime.UtcNow;
            _ = BajarFeed();
            if (UsarCadenaViva) ArrancarViva();
            Log("Gamma Hoy 0.4 arranca" + (Fuente == FuenteDatos.Hibrido ? " en HIBRIDO (archivo + vivo)" : "") + ". raiz=" + Raiz() + " horizonte=" + Horizonte);
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
                if (c != null) { _c = c; _error = ""; }
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
            if (Fuente == FuenteDatos.Archivo) { try { RebobinarBarra(bar); } catch (Exception e) { Registrar(e); } return; }
            if (Fuente == FuenteDatos.Hibrido && bar < CurrentBar - 1) { try { RebobinarBarra(bar); } catch (Exception e) { Registrar(e); } return; }
            if (bar != CurrentBar - 1) return;
            try { Repreciar(); } catch (Exception e) { Registrar(e); }
            try { Anotar(bar); } catch (Exception e) { Registrar(e); }
            // HIBRIDO: cuando arranca una vela nueva, la que acaba de cerrar guarda
            // su foto con el estado vivo de ese instante: la historia sigue creciendo
            // con lo de verdad y el mouse la puede revisar como al archivo
            if (Fuente == FuenteDatos.Hibrido && bar > _barraVivaUlt)
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
                    LibroDom = _libroDomUsado, LibroConv = _libroConvUsado, Mucho = _mucho, Mc = _maxChange, Vela = Utc(c.Time),
                    Cadena = cad != null && cad.GeneradoUtc != default ? cad.GeneradoUtc : (cad != null ? cad.RecibidoUtc : DateTime.MinValue),
                };
            }
        }

        /// <summary>El centinela del archivo, separado del vivo ("hoy-"): asi el
        /// laboratorio no mezcla lo rebobinado con lo que paso en pantalla.</summary>
        private void AnotarArchivo(int bar)
        {
            if (!AnotarCentinela) return;
            if (_centArchivo == null)
            {
                var instr = InstrumentInfo != null ? InstrumentInfo.Instrument : "x";
                var marco = ChartInfo != null && ChartInfo.ChartType != null ? ChartInfo.ChartType + "-" + ChartInfo.TimeFrame : "x";
                _centArchivo = new Centinela("rebobinado-atas-" + instr, marco);
            }
            IndicatorCandle c; try { c = GetCandle(bar); } catch { return; }
            if (c == null) return;
            List<KeyValuePair<string, double>> niv; double sp;
            lock (_candado)
            {
                sp = _S;
                niv = GammaHoyNucleo.Niveles(new GammaHoyNucleo.Lectura
                {
                    ZeroVol = _zeroVol, ZeroOi = _zeroOi, MpVol = _mpVol, MnVol = _mnVol, MpOi = _mpOi, MnOi = _mnOi,
                    Doms = _doms, MaxChange = _maxChange, PicoFut = _picoFut, CuadranteN = _cuadranteN
                });
            }
            _centArchivo.Anotar(bar, c.LastTime != default(DateTime) ? c.LastTime : c.Time,
                (double)c.Open, (double)c.High, (double)c.Low, (double)c.Close, (double)c.Volume, (double)c.Ticks, (double)c.Delta, sp, niv);
        }

        // ==================================================================
        // Rebobinado: la historia cargada del grafico con la cadena de cada minuto
        // ==================================================================
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
                            await Feed.Archivo.BajarDia(Url, raiz, d, Log).ConfigureAwait(false);
                    var ls = Feed.Archivo.Cargar(raiz, desde, hasta, Log);
                    lock (_candado) { _archivo = ls; _iArchivo = 0; _barraReb = -1; _fotosBarra.Clear(); }
                    _archivoListo = true;
                    Log("REBOBINADO: " + ls.Count + " cadenas cargadas; recalculo el grafico");
                    try { RecalculateValues(); } catch (Exception e) { Registrar(e); }
                }
                catch (Exception e) { Registrar(e); }
                finally { _archivoCargando = false; }
            });
        }

        private void RebobinarBarra(int bar)
        {
            IndicatorCandle c;
            try { c = GetCandle(bar); } catch { return; }
            if (c == null) return;
            var abre = Utc(c.Time);
            if (!_archivoListo)
            {
                if (bar == 0) CargarArchivo(abre.AddDays(-1));
                return;
            }
            List<Feed.Cadena> arch; lock (_candado) arch = _archivo;
            if (arch == null || arch.Count == 0) return;
            // el cierre de la vela: la siguiente abre ahi; la ultima, ahora
            DateTime cierra;
            try { cierra = bar + 1 < CurrentBar ? Utc(GetCandle(bar + 1).Time) : DateTime.UtcNow; } catch { cierra = abre.AddMinutes(1); }
            if (cierra <= abre) cierra = abre.AddMinutes(1);
            // puntero monotono; si ATAS recalcula desde cero, se reinicia todo
            if (bar <= _barraReb) { _iArchivo = 0; _nucleo.Reiniciar(); lock (_candado) _fotosBarra.Clear(); _rebConCadena = _rebSinCadena = 0; }
            _barraReb = bar;
            while (_iArchivo + 1 < arch.Count && arch[_iArchivo + 1].GeneradoUtc <= cierra) _iArchivo++;
            var cad = arch[_iArchivo];
            double edadMax = (double)Math.Max(0.05m, ArchivoEdadMaxHoras);
            if (cad.GeneradoUtc > cierra || (cierra - cad.GeneradoUtc).TotalHours > edadMax)
            {
                _rebSinCadena++;
                lock (_candado) { _estela[bar] = new double[0]; _fotosBarra.Remove(bar); }
                return;
            }
            _rebConCadena++;
            _rebCadenaHora = cad.GeneradoUtc;
            // la misma cuenta que en vivo: esta cadena, este cierre, esta hora, esta vela
            RepreciarCon(cad, (double)c.Close, cierra, bar);
            lock (_candado)
            {
                _fotosBarra[bar] = new Foto
                {
                    S = _S, Futuro = _futuro, ZeroVol = _zeroVol, ZeroOi = _zeroOi, MpVol = _mpVol, MnVol = _mnVol, MpOi = _mpOi, MnOi = _mnOi,
                    PicoFut = _picoFut, ConvEnPrecio = _convEnPrecio, NetVol = _netVol, NetOi = _netOi, Doms = _doms, Cuad = _cuadrante, Corto = _cuadranteCorto,
                    LibroDom = _libroDomUsado, LibroConv = _libroConvUsado, Mucho = _mucho, Mc = _maxChange, Vela = abre, Cadena = cad.GeneradoUtc,
                };
                if (_fotosBarra.Count > 40000) foreach (var k in _fotosBarra.Keys.Where(k => k < bar - 35000).ToList()) _fotosBarra.Remove(k);
            }
            // la vela ya cerrada, con el estado vigente, al centinela del archivo
            try { AnotarArchivo(bar); } catch (Exception e) { Registrar(e); }
            if ((DateTime.UtcNow - _rebUltimoLog).TotalSeconds >= 10)
            {
                _rebUltimoLog = DateTime.UtcNow;
                Log("REBOBINADO avanza: vela " + bar + " de " + CurrentBar + " (" + abre.ToString("yyyy-MM-dd HH:mm") + " UTC), con cadena " + _rebConCadena + ", sin " + _rebSinCadena);
            }
            if (bar >= CurrentBar - (Fuente == FuenteDatos.Hibrido ? 2 : 1))
            {
                var es = CultureInfo.GetCultureInfo("es-AR");
                _rebRotulo = (Fuente == FuenteDatos.Hibrido ? "HIBRIDO  archivo " : "REBOBINADO  ") + _rebConCadena.ToString("N0", es) + " velas con cadena, " + _rebSinCadena.ToString("N0", es) + " sin";
                Log("REBOBINADO termino: " + _rebConCadena + " velas con cadena, " + _rebSinCadena + " sin; ultima cadena " + cad.GeneradoUtc.ToString("yyyy-MM-dd HH:mm") + " UTC");
                try { _centArchivo?.Volcar(true); } catch { }
                try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            }
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

            var L = _nucleo.Calcular(c, futuro, ahoraUtc);
            if (L == null) return;
            if (L.SinBase) { lock (_candado) { _baseOrigen = "sin base"; } return; }
            if (L.TransicionNueva) Log("TRANSICION " + L.Alerta);

            lock (_candado)
            {
                _perfil = L.Perfil; _S = L.S; _futuro = L.Futuro; _base = L.Base; _baseOrigen = L.BaseOrigen;
                _zeroVol = L.ZeroVol; _zeroOi = L.ZeroOi; _netVol = L.NetVol; _netOi = L.NetOi;
                _mpVol = L.MpVol; _mnVol = L.MnVol; _mpOi = L.MpOi; _mnOi = L.MnOi;
                _maxAbsVol = L.MaxAbsVol; _maxAbsOi = L.MaxAbsOi; _maxAbsConv = L.MaxAbsConv;
                _doms = L.Doms; _libroConvUsado = L.LibroConv; _libroDomUsado = L.LibroDom;
                _cuadrante = L.Cuadrante; _cuadranteCorto = L.CuadranteCorto; _cuadranteN = L.CuadranteN;
                _picoFut = L.PicoFut; _picoGex = L.PicoGex; _convEnPrecio = L.ConvEnPrecio; _mucho = L.Mucho;
                _alerta = L.Alerta; _alertaHasta = L.AlertaHasta;
                _maxChange = L.MaxChange;
                _estela[barra] = L.Estela;
                if (_estela.Count > 6000) foreach (var k in _estela.Keys.Where(k => k < barra - 5000).ToList()) _estela.Remove(k);
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
                    }
                    else foto = null;
                }
            }

            var c = _c;
            // ---- cabecera: siempre, aunque no haya datos, para que se sepa por que
            string edad = c == null ? "sin feed" : (c.EdadMin + 902.0 / 60.0).ToString("0", es) + " min tarde";
            string l1 = perfil.Count == 0
                ? "GAMMA HOY  esperando cadena" + (string.IsNullOrEmpty(_error) ? "" : " (" + _error + ")")
                : "GAMMA HOY  " + corto + "  " + cuad + "   conv " + (convPrecio >= 0 ? "+" : "-") + " (" + libroConv + ")  pico " + (double.IsNaN(picoFut) ? "--" : picoFut.ToString("N0", es)) + (mucho ? " mucho" : " poco");
            string l2 = "vol CBOE " + edad + " · OI de ayer · base " + origenBase + " · dominantes por " + libroDom
                      + (_viva.Activa ? " · vivo Rithmic " + ((int)_viva.VolumenTotalHoy()).ToString("N0", es) + " contr" : " · vivo: " + _viva.Estado);
            if (Fuente == FuenteDatos.Hibrido)
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
            foreach (var s in perfil)
            {
                int y; try { y = cont.GetYByPrice((decimal)s.Fut, false); } catch { continue; }
                if (y < area.Top || y > piso) continue;
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
            if (VerEstela)
            {
                Dictionary<int, double[]> est; lock (_candado) est = new Dictionary<int, double[]>(_estela);
                int desde = Math.Max(0, FirstVisibleBarNumber), hasta = Math.Min(CurrentBar - 1, LastVisibleBarNumber);
                for (int b = desde; b <= hasta; b++)
                {
                    if (!est.TryGetValue(b, out var d)) continue;
                    int x; try { x = cont.GetXByBar(b, false); } catch { continue; }
                    for (int i = 0; i < d.Length; i++)
                    {
                        if (double.IsNaN(d[i])) continue;
                        int y; try { y = cont.GetYByPrice((decimal)d[i], false); } catch { continue; }
                        if (y < area.Top || y > piso) continue;
                        g.FillRectangle(Color.FromArgb(i == 0 ? 210 : 130, ColDom), new Rectangle(x - 1, y - 1, 3, 2));
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
