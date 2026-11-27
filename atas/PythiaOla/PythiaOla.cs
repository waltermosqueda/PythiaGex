using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;

using ATAS.Indicators;

using MColor = System.Windows.Media.Color;

namespace PythiaLaOla
{
    /// <summary>
    /// LA OLA 1.0 (27-11-2026). Indicador NUEVO y AUTOCONTENIDO (DLL propia, PythiaOla.dll), aparte del Superindicador. Pedido del
    /// operador: "mejor construir un indicador aparte, no lo mezcles con el Superindicador (ese es delta, footprint y volumen; este es mas
    /// general)". De las demos, lo que mejor leyo fue el FONDO DE MAREA; el panel en palabras y las rayas de giro le ayudaron a entender
    /// el mecanismo.
    ///
    /// QUE MUESTRA (todo "al momento": la vela del momento es la ultima a la vista; al correr el grafico hacia atras, todo se ve como
    /// estaba en ese momento, como en el Superindicador 1.7):
    ///   - FONDO DE MAREA: la marea es el zigzag de 4X sobre la cinta orden por orden (ronda 17). Verde = sube con compradores (el delta
    ///     de la pierna va a favor); rojo = baja con vendedores; ambar = se mueve SIN agresores (contra el delta: el libro pasivo que se
    ///     retira). Transparente y detras de las velas.
    ///   - LINEA DE LA OLA (zigzag de X): llena si la pierna fue con agresores, punteada ambar si fue sin; la pierna en curso, mas gruesa.
    ///   - RAYAS DE GIRO: el precio donde la ola (blanco punteado) y la marea (violeta) se dan vuelta: extremo -/+ X (o 4X). Etiqueta corta
    ///     a la derecha: "ola gira 7.047,75".
    ///   - ROMBO en cada giro de la marea.
    ///   - VELAS PINTADAS por la ola (apagado por defecto).
    ///   - PANEL arriba a la izquierda (estilo del encabezado del Superindicador, variante E): punto LIVE verde (en vivo) o rojo apagado
    ///     (un momento pasado) y la hora de la PC; CLIMA (el semaforo D1 de la ronda 21, con su numero: "1,8x lo normal a esta hora");
    ///     MAREA y OLA (direccion, con o sin agresores, delta de la pierna y cuanto le falta al precio para la raya de giro).
    ///     1.0c: compacto (un renglon por dato; el pie solo cuando hay algo que avisar; la X en el cartelito del mouse), se minimiza a una
    ///     pastilla con el "–" y se mueve arrastrandolo (fuera del cuadro el mouse va siempre al grafico).
    ///   - SENALES: NO en esta version (ninguna regla paso la validacion; ronda 18 y 21). Hay una casilla "Senales (en prueba)" apagada
    ///     y sin implementar.
    ///
    /// DE DONDE SALE: solo de ATAS (Rithmic). La cinta de la jornada por RequestForCumulativeTrades (un pedido por jornada, de a uno) y
    /// despues orden por orden por OnCumulativeTrade / OnUpdateCumulativeTrade; la X y la mediana del clima, de las velas del grafico
    /// (10 ruedas completas previas; las medias ruedas como 26 y 27-11 quedan afuera). No depende de otro indicador, de ningun programa
    /// externo ni de archivos. Lo unico que escribe afuera: pythiaola.log (arranque, pedidos de cinta y errores; siempre, es chico) y la
    /// auditoria (jsonl y csv; apagable con PoRegistro).
    ///
    /// AUDITORIA: %APPDATA%\ATAS\PythiaGex\pythiaola\: una linea JSON por minuto con lo que muestra la pantalla (auditoria-*.jsonl) y el
    /// csv de pivotes, fotos por vela y clima por minuto (ola-*.csv). herramientas/auditar_pythiaola.py lo rehace por fuera desde la cinta
    /// de la sonda (la ola con la Maquina de test_ola.py; el clima con la definicion de la R21). Arnes offline: atas/_test_pythiaola,
    /// 0 diferencias contra la referencia de la R21 en ES 20-11 y 25-11.
    ///
    /// Los ajustes llevan el prefijo "Po" (ATAS los guarda POR NOMBRE en el workspace: nombres nuevos, sin choques con los de otros).
    /// </summary>
    [DisplayName("PythiaGex - La Ola (marea, ola y clima)")]
    [Category("PythiaGex")]
    public partial class PythiaOla : Indicator
    {
        // 1.0b (27-11): cortes de datos -> re-pide la jornada y avisa (OlaCortes.cs, PythiaOlaCortes.cs)
        // 1.0c (27-11): SOLO PRESENTACION del cuadro (ningun calculo cambia): compacto (un renglon por dato, pie solo con avisos y la X en el
        // cartelito del mouse), minimizable a una pastilla y arrastrable (PythiaOlaMouse.cs). El de la 1.0b sigue en PoPanelEstilo.
        public const string Version = "1.0e";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Donde va el fondo de marea. ATAS tiene UNA sola llave por indicador para dibujar arriba o abajo de las velas
        /// (DrawAbovePrice): con el panel y las rayas arriba, el fondo "detras" se logra pintando alrededor de cada vela.</summary>
        public enum CapaFondo
        {
            [Display(Name = "Detrás de las velas (pinta alrededor de cada vela)")] DetrasDeLasVelas,
            [Display(Name = "Encima, parejo y transparente")] EncimaParejo,
            [Display(Name = "Todo el indicador detrás de las velas (panel y rayas incluidos)")] TodoDetras
        }

        /// <summary>1.0c: el aspecto del cuadro (propiedad nueva PoPanelEstilo).</summary>
        public enum EstiloPanel
        {
            [Display(Name = "Compacto (1.0c): un renglón por dato, pie solo con avisos")] Compacto,
            [Display(Name = "Clásico (1.0b): dos renglones por dato y pie siempre")] Clasico
        }

        private static MColor Hex(string h, byte a = 255)
        {
            h = h.TrimStart('#');
            return MColor.FromArgb(a, Convert.ToByte(h.Substring(0, 2), 16), Convert.ToByte(h.Substring(2, 2), 16), Convert.ToByte(h.Substring(4, 2), 16));
        }

        private static System.Drawing.Color D(MColor c) => System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);

        // ================================================================== ajustes (prefijo Po: nombres nuevos)
        // 1. Capas: cada una con su casilla (regla del operador: "que todo se pueda activar/desactivar a placer en pantalla")
        private bool _fondo = true;
        [Display(Name = "Fondo de marea", GroupName = "1. Capas", Order = 10,
            Description = "Verde: la marea (zigzag de 4X) sube con compradores. Rojo: baja con vendedores. Ámbar: se mueve sin agresores (contra el delta). Transparente, detrás de las velas.")]
        public bool PoFondo { get => _fondo; set { _fondo = value; Redibujar(); } }

        private bool _linea = true;
        [Display(Name = "Línea de la ola", GroupName = "1. Capas", Order = 11,
            Description = "El zigzag de X: llena si la pierna fue con agresores, punteada ámbar si fue sin agresores; la pierna en curso, más gruesa.")]
        public bool PoLinea { get => _linea; set { _linea = value; Redibujar(); } }

        private bool _rayas = true;
        [Display(Name = "Rayas de giro (ola y marea)", GroupName = "1. Capas", Order = 12,
            Description = "El precio donde se dan vuelta: la de la ola en blanco punteado y la de la marea en violeta, con su etiqueta a la derecha.")]
        public bool PoRayas { get => _rayas; set { _rayas = value; Redibujar(); } }

        private bool _rombo = true;
        [Display(Name = "Rombo de giro de la marea", GroupName = "1. Capas", Order = 13,
            Description = "Un rombo violeta en cada giro de la marea (el extremo donde se dio vuelta).")]
        public bool PoRombo { get => _rombo; set { _rombo = value; Redibujar(); } }

        private bool _velas = false;
        [Display(Name = "Velas pintadas por la ola", GroupName = "1. Capas", Order = 14,
            Description = "Pinta cada vela con el estado de la ola a su cierre: verde/rojo con agresores, ámbar sin agresores.")]
        public bool PoVelas { get => _velas; set { if (_velas == value) return; _velas = value; lock (_llavePintura) _pintadas.Clear(); Pintar(); Redibujar(); } }

        private bool _panel = true;
        [Display(Name = "Panel (clima, marea y ola)", GroupName = "1. Capas", Order = 15,
            Description = "Arriba a la izquierda: LIVE y la hora; el clima del día con su número; la marea y la ola en palabras.")]
        public bool PoPanel { get => _panel; set { _panel = value; Redibujar(); } }

        private bool _senales = false;
        [Display(Name = "Señales (en prueba)", GroupName = "1. Capas", Order = 16,
            Description = "Pendiente de validación: en esta versión no dibuja nada (ninguna regla de entrada pasó la validación).")]
        public bool PoSenales { get => _senales; set { _senales = value; } }

        // 2. Escala
        private decimal _xFija = 0m;
        [Display(Name = "X fija en puntos (0 = automática)", GroupName = "2. Escala de la ola", Order = 20,
            Description = "0 = automática por jornada: 0,08 x la mediana del rango RTH de las 10 ruedas completas anteriores (piso 4). Otro valor: esa X para todas las jornadas (la marea es 4X). El clima usa siempre la mediana de las ruedas.")]
        [Range(0, 1000)]
        public decimal PoXFija
        {
            get => _xFija;
            set
            {
                decimal v = Math.Max(0m, Math.Min(1000m, value)); if (v == _xFija) return; _xFija = v;
                OlaReiniciarCargas("X fija " + v.ToString(Inv));
                Despintar();   // las velas pintadas con la X vieja vuelven a su color hasta que carguen las jornadas con la nueva
            }
        }

        // 3. Colores
        private MColor _cSube = Hex("#00D47E"), _cBaja = Hex("#F23645"), _cSin = Hex("#FFB300"), _cRayaOla = Hex("#F2F2F2"), _cRayaMarea = Hex("#B388FF");
        [Display(Name = "Sube con compradores", GroupName = "3. Colores", Order = 30)]
        public MColor PoColorSube { get => _cSube; set { _cSube = value; Repintar(); } }

        [Display(Name = "Baja con vendedores", GroupName = "3. Colores", Order = 31)]
        public MColor PoColorBaja { get => _cBaja; set { _cBaja = value; Repintar(); } }

        [Display(Name = "Sin agresores", GroupName = "3. Colores", Order = 32)]
        public MColor PoColorSinAgresores { get => _cSin; set { _cSin = value; Repintar(); } }

        [Display(Name = "Raya de giro de la ola", GroupName = "3. Colores", Order = 33)]
        public MColor PoColorRayaOla { get => _cRayaOla; set { _cRayaOla = value; Redibujar(); } }

        [Display(Name = "Raya y rombo de la marea", GroupName = "3. Colores", Order = 34)]
        public MColor PoColorRayaMarea { get => _cRayaMarea; set { _cRayaMarea = value; Redibujar(); } }

        private int _fondoOpac = 14;
        [Display(Name = "Opacidad del fondo %", GroupName = "3. Colores", Order = 35,
            Description = "Qué tan fuerte se ve el color de la marea en el fondo (14 = suave).")]
        [Range(3, 60)]
        public int PoFondoOpacidad { get => _fondoOpac; set { _fondoOpac = Math.Max(3, Math.Min(60, value)); Redibujar(); } }

        // 4. Panel
        // 1.0d (27-11): el cuadro arrancaba arriba a la izquierda, ENCIMA de lo que escribe ATAS (nombre del contrato, la linea O/H/L/C del
        // mouse, el aviso "Last Market Data Update" y el recuadro "7 v"): el operador: "choca, se mezcla, compite con la data de ATAS". El lugar
        // nuevo es PoCuadroX / PoCuadroY (nombres NUEVOS: ATAS guarda los ajustes por nombre y el 8/30 viejo quedaba pegado); -1 = automatico,
        // debajo de esa franja de ATAS. PoPanelX / PoPanelY quedan ocultos y sin uso.
        private int _lugarX = -1, _lugarY = -1;
        [Display(Name = "Lugar del cuadro: desde la izquierda (px)", GroupName = "4. Panel", Order = 38,
            Description = "-1 = automatico (a la izquierda, debajo de lo que escribe ATAS arriba). Lo cambia arrastrar el cuadro con el mouse.")]
        [Range(-1, 4000)]
        public int PoCuadroX { get => _lugarX; set { _lugarX = Math.Max(-1, Math.Min(4000, value)); Redibujar(); } }

        [Display(Name = "Lugar del cuadro: desde arriba (px)", GroupName = "4. Panel", Order = 39,
            Description = "-1 = automatico (debajo del nombre del contrato, la linea O/H/L/C y los avisos de ATAS). Lo cambia arrastrar el cuadro.")]
        [Range(-1, 4000)]
        public int PoCuadroY { get => _lugarY; set { _lugarY = Math.Max(-1, Math.Min(4000, value)); Redibujar(); } }

        private int _panelX = 8, _panelY = 30;
        [Browsable(false)]
        [Display(Name = "Corrimiento a la derecha (px)", GroupName = "4. Panel", Order = 40,
            Description = "Desde el borde izquierdo del gráfico. Lo cambia arrastrar el cuadro con el mouse.")]
        [Range(0, 2000)]
        public int PoPanelX { get => _panelX; set { _panelX = Math.Max(0, Math.Min(2000, value)); Redibujar(); } }

        [Browsable(false)]
        [Display(Name = "Corrimiento hacia abajo (px)", GroupName = "4. Panel", Order = 41,
            Description = "Desde el borde de arriba del gráfico (ATAS escribe ahí el nombre del instrumento y de los indicadores). Lo cambia arrastrar el cuadro con el mouse.")]
        [Range(0, 2000)]
        public int PoPanelY { get => _panelY; set { _panelY = Math.Max(0, Math.Min(2000, value)); Redibujar(); } }

        // 1.0c (27-11-2026), pedido del operador: "el cuadro tapa, molesta: que sea minimizable y que se pueda correr solo arrastrando el
        // mouse". Propiedades NUEVAS (ATAS guarda los ajustes por nombre en el .ws: un defecto nuevo solo vale con un nombre nuevo).
        private EstiloPanel _panelEstilo = EstiloPanel.Compacto;
        [Display(Name = "Estilo del cuadro", GroupName = "4. Panel", Order = 42,
            Description = "Compacto (de fábrica): un renglón por dato y el pie solo cuando hay algo que avisar. Clásico: el cuadro de la 1.0b.")]
        public EstiloPanel PoPanelEstilo { get => _panelEstilo; set { _panelEstilo = value; Redibujar(); } }

        private bool _panelMin = false;
        [Display(Name = "Cuadro minimizado", GroupName = "4. Panel", Order = 43,
            Description = "Una pastilla de un renglón (LIVE, la letra del clima, MAREA y OLA con sus colores). El – de arriba a la derecha del cuadro lo minimiza y un clic en la pastilla lo abre.")]
        public bool PoPanelMinimizado { get => _panelMin; set { _panelMin = value; Redibujar(); } }

        private bool _panelMouse = true;
        [Display(Name = "Mover y minimizar con el mouse", GroupName = "4. Panel", Order = 44,
            Description = "Arrastrar el cuadro lo mueve; clic en el – lo minimiza; clic en la pastilla lo abre. Fuera del cuadro el mouse va siempre al gráfico. Apagado: el cuadro no toma ningún clic (como la 1.0b).")]
        public bool PoPanelConMouse { get => _panelMouse; set { _panelMouse = value; Redibujar(); } }

        // 5. Avanzado
        private CapaFondo _capa = CapaFondo.DetrasDeLasVelas;
        [Display(Name = "Fondo: dónde se dibuja", GroupName = "5. Avanzado", Order = 50,
            Description = "Detrás de las velas: pinta alrededor de cada vela y las velas quedan con su color. Encima: un velo parejo. Todo detrás: el indicador entero (panel y rayas también) va detrás de las velas, como el Session Color de ATAS.")]
        public CapaFondo PoFondoCapa { get => _capa; set { _capa = value; DrawAbovePrice = value != CapaFondo.TodoDetras; Redibujar(); } }

        private int _margen = 64;
        [Display(Name = "Margen derecho para las etiquetas (px)", GroupName = "5. Avanzado", Order = 51,
            Description = "El eje de precios de ATAS se dibuja encima del borde derecho del gráfico: las etiquetas de las rayas terminan antes.")]
        [Range(0, 400)]
        public int PoMargenDerecho { get => _margen; set { _margen = Math.Max(0, Math.Min(400, value)); Redibujar(); } }

        private bool _registro = true;
        [Display(Name = "Registro de auditoría", GroupName = "5. Avanzado", Order = 52,
            Description = "Escribe en %APPDATA%\\ATAS\\PythiaGex\\pythiaola lo que muestra la pantalla (una línea por minuto) y la ola, la marea y el clima (csv) para recalcularlos por fuera.")]
        public bool PoRegistro { get => _registro; set { _registro = value; } }

        private bool _recargaTrasCorte = true;
        [Display(Name = "Tras un corte de datos: recargar el gráfico solo", GroupName = "5. Avanzado", Order = 53,
            Description = "1.0b (27-11): cuando el log de ATAS anota un corte de conexión (o la cinta se calla 20 s en la rueda) y los datos vuelven, aprieta por dentro el \"Reload all historical data\" de ESTE gráfico (sin mouse ni teclado): el gráfico queda en blanco unos 6-15 s y vuelve con las órdenes del hueco. Como máximo una vez cada 3 minutos. Apagado: La Ola se repone sola y el pie avisa que falta recargar el gráfico.")]
        public bool PoRecargaTrasCorte { get => _recargaTrasCorte; set { _recargaTrasCorte = value; } }

        // ================================================================== estado
        // la serie 0 pinta las velas (como el Color Bar de fabrica); oculta en la lista de series
        private readonly PaintbarsDataSeries _pintura = new PaintbarsDataSeries("PoPintura", "Velas pintadas por la ola") { IsHidden = true };
        private Action _latido;
        private readonly TimeSpan _periodo = TimeSpan.FromMilliseconds(250);
        private readonly object _llaveArchivo = new object();
        private DateTime _ultError = DateTime.MinValue;
        private static TimeZoneInfo _ny;

        // ================================================================== ciclo de vida
        public PythiaOla() : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
            DrawAbovePrice = true;
            DataSeries[0] = _pintura;
        }

        protected override void OnInitialize()
        {
            try
            {
                var ps = DataProvider != null ? DataProvider.Panels : null;
                if (ps != null && ps.Count > 0) Panel = ps.Contains("Chart") ? "Chart" : ps[0];
            }
            catch (Exception e) { Anotar(e); }
            DrawAbovePrice = _capa != CapaFondo.TodoDetras;   // ATAS guarda este valor en el .ws: se fuerza con el ajuste
            _latido = Latido;
            try { SubscribeToTimer(_periodo, _latido); } catch (Exception e) { Anotar(e); }
            AnotarLinea("arranca La Ola " + Version);
        }

        protected override void OnDispose()
        {
            try { if (_latido != null) UnsubscribeFromTimer(_periodo, _latido); } catch { }
            OlaSoltar();   // libera el turno de pedidos de cinta
            RetirarCuadro();   // 1.0e: la manija del Superindicador vuelve a su lugar automatico
            base.OnDispose();
        }

        protected override void OnRecalculate()
        {
            OlaReiniciarTodo("recarga de datos");   // los numeros de vela cambian: se rehace todo
            CortesRecarga();                         // 1.0b: el grafico se recargo ("Reload all historical data"): se apaga el aviso de corte
            lock (_llavePintura) _pintadas.Clear();
            AnotarLinea("recarga de datos");
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            try
            {
                var c = GetCandle(bar);
                if (c == null) return;
                // la serie de pintura crece aca, en el hilo de datos (si no, el dibujo de ATAS la agranda desde otro hilo mientras Pintar escribe)
                if (bar >= _pintura.Count) lock (_llavePintura) { if (bar >= _pintura.Count) _pintura[bar] = null; }
                OlaVela(bar, c);   // apertura, ultima operacion, alto y bajo de la vela para la ola, el corte y las ruedas (O(1))
                if (bar >= CurrentBar - 1) { _ultCalculoPc = DateTime.UtcNow; Pintar(); }
            }
            catch (Exception e) { Anotar(e); }
        }

        /// <summary>Cada 250 ms: los pedidos de cinta, el registro y, con el mercado quieto (sin OnCalculate), las velas pintadas.</summary>
        private void Latido()
        {
            try
            {
                OlaLatido();
                PanelLatido();   // 1.0c: el arrastre del cuadro nunca queda colgado y el cartelito se borra si el mouse se fue
                if (_velas && (DateTime.UtcNow - _ultCalculoPc).TotalSeconds > 2) Pintar();
                if (_redibujarPedido) { _redibujarPedido = false; Redibujar(); }
            }
            catch (Exception e) { Anotar(e); }
        }

        private volatile bool _redibujarPedido;

        private void Redibujar()
        {
            try { var c = ChartInfo?.PriceChartContainer; if (c != null) RedrawChart(new RedrawArg(c.Region)); } catch { }
        }

        private void Repintar()
        {
            lock (_llavePintura) _pintadas.Clear();
            Pintar();
            Redibujar();
        }

        // ================================================================== la vela del momento
        /// <summary>La vela del MOMENTO: la ultima a la vista (al correr el grafico hacia atras, todo se muestra como estaba ahi).</summary>
        private int Momento()
        {
            int ultima = CurrentBar - 1;
            try { int v = LastVisibleBarNumber; if (v >= 0) return Math.Min(ultima, v); } catch { }
            return ultima;
        }

        // ================================================================== velas pintadas por la ola
        private readonly object _llavePintura = new object();
        // jornada -> la carga (su identidad: al cambiar la X o recargar se crea otra), la version de sus motores y las velas ya pintadas
        private readonly Dictionary<int, (CargaOla Carga, long Ver, int Hecho)> _pintadas = new Dictionary<int, (CargaOla, long, int)>();
        private bool _hayPintadas;
        private DateTime _ultCalculoPc = DateTime.MinValue;

        /// <summary>Todas las velas a su color (null) y se olvida lo pintado: se vuelve a pintar a medida que cargan las jornadas.</summary>
        private void Despintar()
        {
            try
            {
                lock (_llavePintura)
                {
                    _pintadas.Clear();
                    if (!_hayPintadas) return;
                    int n = CurrentBar;
                    for (int b = 0; b < n; b++) _pintura[b] = null;
                    _hayPintadas = false;
                }
            }
            catch (Exception e) { Anotar(e); }
        }

        /// <summary>El color de una vela por la ola a su cierre: verde / rojo si la pierna va con agresores, ambar si va sin; null sin pierna.</summary>
        private MColor? ColorOla(in FotoOla f)
        {
            if (!f.HayPierna) return null;
            if (!f.ConAgresores) return _cSin;
            return f.Dir > 0 ? _cSube : _cBaja;
        }

        /// <summary>
        /// Escribe la serie de pintura de las velas cuyo estado cambio: por jornada cargada, desde dos velas antes de la ultima ya pintada (la
        /// ultima foto tomada se rehace si la ultima orden crece) hasta la ultima vela; toda la jornada si sus motores cambiaron (historial
        /// integrado). Apagada: deja todas las velas con su color (null) una sola vez. Desde OnCalculate y, con el mercado quieto, el latido.
        /// </summary>
        private void Pintar()
        {
            try
            {
                lock (_llavePintura)
                {
                    int n = CurrentBar;
                    if (!_velas)
                    {
                        if (_hayPintadas) { for (int b = 0; b < n; b++) _pintura[b] = null; _hayPintadas = false; }
                        return;
                    }
                    var trabajos = new List<(int Bar, MColor? Color)>();
                    lock (_llaveOla)
                    {
                        var g = OlaG();
                        var cargas = new List<CargaOla>();
                        if (g.Viva != null) cargas.Add(g.Viva);
                        cargas.AddRange(g.Pasadas);
                        foreach (var c in cargas)
                        {
                            if (!c.TieneMotor || c.Bufereando || c.PrimerBar < 0) continue;
                            if (!g.BarrasDe(c.Clave, out int p, out int u)) continue;
                            _pintadas.TryGetValue(c.Clave, out var st);
                            int desde = ReferenceEquals(st.Carga, c) && st.Ver == c.VersionMotores ? Math.Max(0, st.Hecho - 2) : 0;
                            for (int b = p + desde; b <= u; b++)
                                trabajos.Add((b, g.FotosDe(b, out var fo, out _) ? ColorOla(fo) : (MColor?)null));
                            _pintadas[c.Clave] = (c, c.VersionMotores, Math.Min(c.Ola.Tomadas, u - p + 1));
                        }
                    }
                    foreach (var (b, col) in trabajos) if (b >= 0 && b < n) _pintura[b] = col;
                    if (trabajos.Count > 0) _hayPintadas = true;
                }
            }
            catch (Exception e) { Anotar(e); }
        }

        // ================================================================== utilidades
        private static TimeZoneInfo Ny()
        {
            if (_ny == null) { try { _ny = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); } catch { _ny = TimeZoneInfo.Utc; } }
            return _ny;
        }

        private static string Carpeta()
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex", "pythiaola");
            Directory.CreateDirectory(d);
            return d;
        }

        private string Inst() { try { return (InstrumentInfo?.Instrument ?? "inst").Replace(' ', '_').Replace('/', '_').Replace('#', '_'); } catch { return "inst"; } }
        private string Marco() { try { return (ChartInfo?.TimeFrame ?? "tf").Replace(' ', '_'); } catch { return "tf"; } }
        private static string F(decimal v) => v.ToString(Inv);

        private void Anotar(Exception e)
        {
            if ((DateTime.UtcNow - _ultError).TotalSeconds < 5) return;
            _ultError = DateTime.UtcNow;
            AnotarLinea("ERROR " + e.GetType().Name + ": " + e.Message + " | " + (e.StackTrace ?? "").Replace("\r", " ").Replace("\n", " "));
        }

        private void AnotarLinea(string s)
        {
            try
            {
                string linea = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", Inv) + " UTC v" + Version + " " + Inst() + " " + Marco() + " " + s + Environment.NewLine;
                lock (_llaveArchivo) File.AppendAllText(Path.Combine(Carpeta(), "pythiaola.log"), linea);
            }
            catch { }
        }
    }
}
