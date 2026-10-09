using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGexTres
{
    /// <summary>Como se eligen las dos dominantes. Default DosMasGrandes (lo medido en la referencia, 2.0.5).</summary>
    public enum ReglaDominantes3
    {
        [Description("Clasica: una por lado + empate 20 % + centroide 12 pts (Gamma Hoy 1.11d)")] Clasica,
        [Description("Dos mas grandes por volumen, strike exacto, sin centroide ni histeresis, de dia y de noche (la regla de la 2.0; default 3.7.0)")] DosMasGrandes,
        [Description("Tres: la clasica con histeresis (la retadora supera a la vigente 25 % durante 5 min) + centroide 6 pts. F3 V22 del laboratorio: SIN VALIDAR")] Tres,
        [Description("Tunel (3.3.0): techo = strike mas cercano ARRIBA con |gex| >= umbral del mayor del radio, piso = idem ABAJO, con histeresis mientras el precio siga adentro. Medido con el criterio del operador (laboratorio/tres/criterio_operador.py)")] Tunel,
    }

    /// <summary>Un ajuste que fija los defaults de las casillas visuales sin tocarlas una por una.</summary>
    /// <summary>Como se dibuja la estela del zero gamma (3.2.4).</summary>
    public enum ZeroEstilo3
    {
        [Description("Rombos verdes por vela, como los de la 2.0 (el zero por OI mas chico y oscuro)")] RomboVerde,
        [Description("Guiones grises finos (3.1.2)")] GuionGris,
    }

    public enum PerfilVisual3
    {
        [Description("Limpio: D1, D2, zero, tunel y toques (majors solo en ES)")] Limpio,
        [Description("Con majors: lo limpio + Major Positive / Negative")] ConMajors,
        [Description("Todo: lo anterior + barras del perfil a la izquierda")] Todo,
    }

    /// <summary>Cada casilla visual: segun el perfil, o forzada.</summary>
    public enum Tri3
    {
        [Description("Segun el perfil visual")] SegunPerfil,
        [Description("Si")] Si,
        [Description("No")] No,
    }

    public enum BarrasLado3
    {
        [Description("Segun el perfil visual")] SegunPerfil,
        [Description("Ninguna")] Ninguna,
        [Description("Izquierda")] Izquierda,
    }

    /// <summary>
    /// PythiaGex 3.0 - Gamma Hoy.
    ///
    /// La misma CUENTA que la clasica (1.11d) y la 2.0 (GammaHoyNucleo.cs es copia textual), con
    /// la cadena viva de NQ/ES por la API publica de ATAS 8.0.15 (CadenaApi.cs) y el DIBUJO MINIMO
    /// del pliego (laboratorio/tres/resultados/censo_capturas.md, seccion 5):
    ///
    ///   - D1 y D2 como estela de guiones cortos por vela (2 px, amarillo), nada proyectado a la derecha;
    ///   - UN zero gamma punteado fino gris que cruza el grafico;
    ///   - tunel translucido entre D1 y D2 desde la ultima vela hasta el eje (solo si estan cerca);
    ///   - marca de toque (bloque de una vela de ancho, 2 ticks de alto) en la vela que llego a la dominante;
    ///   - una columna de rotulos pegada al borde derecho, en escalera, tope 5 (hasta 7);
    ///   - una cabecera de UN renglon y un cartel de estado con color cuando el libro no esta.
    ///
    /// NADA de pelotitas, gatillos, capas, rombos, nubes, perfil derecho ni textos de depuracion: eso va al log.
    /// Corre AL LADO de la clasica y la 2.0 (otro ensamblado, otros nombres de ajustes, otros archivos) para
    /// contrastarlas en tiempo real: estela, centinela y viva en archivos propios con los formatos de siempre.
    ///
    /// ATAS persiste los ajustes POR NOMBRE en el .ws: todos los nombres de propiedades llevan "3" para que
    /// ningun valor guardado de la 2.0 los pise. TODO lo que corre (OnCalculate, OnRender, temporizador) va
    /// en try/catch que loguea: ATAS se traga las excepciones.
    /// </summary>
    [DisplayName("PythiaGex 3.0 - Gamma Hoy")]
    [Category("PythiaGex 3.0")]
    public partial class GammaHoyTres : Indicator
    {
        private const string LOG = "gammahoy";
        private const string VERSION = "3.7.3";
        private const int MIN_STRIKES_UTILES = 8;     // strikes con IV en call y put para que la cuenta valga

        // colores fijos del pliego (5.3)
        private static readonly Color ColDom = Color.FromArgb(232, 197, 71);     // #E8C547
        private static readonly Color ColZero = Color.FromArgb(200, 200, 200);   // #C8C8C8
        private static readonly Color ColPos = Color.FromArgb(8, 153, 129);      // #089981
        private static readonly Color ColNeg = Color.FromArgb(242, 54, 69);      // #f23645
        // 3.7.3 (revisor visual de la vuelta 2): los M± OI de NQ (y el filo de los de las capas) en un verde y un rosa que no se confunden con las
        // velas (#26a69a / #ef5350) ni con NDX (#26C6DA). Antes usaban ColPos / ColNeg, los mismos verde-agua y rojo de las velas.
        private static readonly Color ColMasOi = Color.FromArgb(0, 230, 118);     // #00E676
        private static readonly Color ColMenosOi = Color.FromArgb(255, 64, 129);  // #FF4081
        private static readonly Color ColFondo = Color.FromArgb(8, 12, 18);
        private static readonly Color ColTexto = Color.FromArgb(220, 228, 236);
        private static readonly Color ColNaranja = Color.FromArgb(255, 150, 40);
        private static readonly Color ColRojo = Color.FromArgb(240, 60, 60);

        private readonly CadenaApi _cadena = new CadenaApi();
        private readonly GammaHoyNucleo _nucleo = new GammaHoyNucleo();
        private readonly TimeSpan _periodo = TimeSpan.FromSeconds(5);
        private Action _tick;

        // estado compartido entre el temporizador, OnCalculate y OnRender
        private readonly object _candado = new object();
        private GammaHoyNucleo.Lectura _L;
        private Feed.Cadena _c;
        private double _futuro;                 // el precio del grafico con el que se hizo la cuenta
        private int _strikesUtiles;
        private DateTime _ultimaCuentaUtc = DateTime.MinValue;
        // La estela y los toques van indexados por la HORA DE APERTURA de la vela (UTC), no por el numero de barra: si ATAS
        // reindexa la serie (recarga de datos, reconexion de Rithmic, cambio de marco) los guiones no se corren de vela.
        private readonly Dictionary<DateTime, double[]> _estela = new Dictionary<DateTime, double[]>();            // vela cerrada -> {D1, D2, zero}
        private readonly Dictionary<DateTime, List<(double Nivel, bool Arriba)>> _toques = new Dictionary<DateTime, List<(double, bool)>>();
        private DateTime _ultimaVelaUtc = DateTime.MinValue;    // la ultima vela cerrada que se anoto (guarda por hora, no por barra)
        private readonly object _llaveVelas = new object();     // OnCalculate y el temporizador pueden querer anotar la misma vela a la vez
        private readonly Dictionary<(int Dom, int Lado), bool> _toqueArmado = new Dictionary<(int, int), bool>();   // un toque por (dominante, lado) hasta que el precio se aleje LEJOS
        private int _barInicioSesion = -1;
        private Centinela _centinela;
        private int _nCentinela;                                 // contador monotono para la guarda del centinela (no depende del numero de barra)

        private bool _arrancada, _sinRaizAvisado;
        private DateTime _ultimoIntentoArranque = DateTime.MinValue, _ultimoRearme = DateTime.MinValue, _ultimoMinuto = DateTime.MinValue, _ultimoAudit = DateTime.MinValue;
        private int _renders;
        private DateTime _ultimoRearmeVencidas = DateTime.MinValue;

        // un solo escritor de la estela por raiz (dos graficos, o la instancia extra que ATAS crea al abrir los ajustes)
        private static readonly object _estelaLlave = new object();
        private static readonly Dictionary<string, (object Dueno, DateTime Hora)> _estelaEscritor = new Dictionary<string, (object, DateTime)>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _estelaUltimaLinea = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, DateTime> _estelaUltimaHora = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        // ------------------------------------------------------------------
        // ajustes (nombres NUEVOS, con "3": ATAS persiste por nombre)
        // ------------------------------------------------------------------

        [Display(Name = "Raiz manual (vacio = del grafico)", GroupName = "1. Libro", Order = 10,
                 Description = "NQ o ES. Vacio: se deduce del instrumento del grafico (MNQ -> NQ, MES -> ES, M2K -> RTY).")]
        public string Raiz3Manual { get; set; } = "";

        [Display(Name = "Tope de contratos suscritos", GroupName = "1. Libro", Order = 20,
                 Description = "Suscripciones vivas por instancia por la API. El proveedor de ATAS corta en 512; 320 por defecto.")]
        [Range(20, 480)]
        public int Tope3Contratos { get; set; } = 320;

        [Display(Name = "Ventana densa (%)", GroupName = "1. Libro", Order = 30, Description = "Todos los strikes hasta este % del precio.")]
        public decimal Ventana3DensaPct { get; set; } = 1.0m;

        [Display(Name = "Ventana rala (%)", GroupName = "1. Libro", Order = 40, Description = "Uno de cada dos strikes hasta este % del precio.")]
        public decimal Ventana3RalaPct { get; set; } = 2.0m;

        [Display(Name = "Vencimientos (fechas)", GroupName = "1. Libro", Order = 50, Description = "Las fechas mas cercanas (0DTE y 1DTE). El viernes de la trimestral una fecha trae dos series.")]
        [Range(1, 4)]
        public int Vencimientos3 { get; set; } = 2;

        [Display(Name = "Recentrar al alejarse (%)", GroupName = "1. Libro", Order = 60, Description = "Rearma la ventana cuando el precio se fue mas que esto del centro (minimo 60 s entre rearmes).")]
        public decimal Recentrar3Pct { get; set; } = 0.5m;

        [Display(Name = "Regla de dominantes", GroupName = "2. Lectura", Order = 10,
                 Description = "3.7.0 (D2): DosMasGrandes por defecto = la regla de la 2.0: las dos barras de mayor |GEX por volumen| a <= min(2 % del precio, 100 pts), strike exacto, sin centroide ni histeresis, de dia y de noche. Nombre nuevo (Regla3DominantesB) para pisar el 'Tres' guardado en el .ws. Clasica = 1.11d (una por lado, empate 20 %, centroide 12). Tres = clasica con histeresis (SIN VALIDAR). Tunel = techo/piso por cruces. Medido (backtest_familia.md, 20 sesiones): ninguna regla le gana al azar.")]
        public ReglaDominantes3 Regla3DominantesB { get; set; } = ReglaDominantes3.DosMasGrandes;

        [Display(Name = "Horizonte de vencimientos", GroupName = "2. Lectura", Order = 20, Description = "Hoy = el 0DTE (el mas cercano). Es lo que se contrasta contra la clasica y la 2.0.")]
        public GammaHoyNucleo.HorizonteVenc Horizonte3 { get; set; } = GammaHoyNucleo.HorizonteVenc.Hoy;

        [Display(Name = "Dominantes: radio maximo en puntos (0 = auto: 100 NQ / 25 ES)", GroupName = "2. Lectura", Order = 30,
                 Description = "La dominante de cada lado se busca solo entre los strikes a menos de esta distancia del precio. 0 = 100 pts en NQ, 25 en ES, 20 en RTY.")]
        [Range(0, 2000)]
        public decimal Radio3DominantesPts { get; set; } = 0m;

        [Display(Name = "Empate tecnico: gana la mas cercana (%)", GroupName = "2. Lectura", Order = 40, Description = "Solo con la regla Clasica. 20 = como la 1.11d.")]
        [Range(0, 90)]
        public int Empate3Pct { get; set; } = 20;

        [Display(Name = "Dominantes de noche", GroupName = "2. Lectura", Order = 50,
                 Description = "REGLA DEL OPERADOR (17-09): Volumen. Fuera de la rueda de NY las dominantes salen del volumen operado hoy, no del interes abierto. No cambiar sin aviso y captura antes/despues.")]
        public GammaHoyNucleo.NocheDominantes Noche3Dominantes { get; set; } = GammaHoyNucleo.NocheDominantes.Volumen;

        [Display(Name = "Zero interpolado por strike (como la referencia)", GroupName = "2. Lectura", Order = 60, Description = "Prendido: cambio de signo del perfil por strike, interpolado. Apagado: cruce repreciado en grilla.")]
        public bool Zero3Interpolado { get; set; } = true;

        [Display(Name = "Perfil visual", GroupName = "3. Pantalla", Order = 5,
                 Description = "Fija los defaults de las casillas de abajo sin tocarlas una por una. Limpio (default): D1, D2, zero, tunel, toques, rotulos y cabecera; majors solo en ES; sin barras. ConMajors: lo mismo + majors. Todo: + barras del perfil a la izquierda.")]
        public PerfilVisual3 Perfil3Visual { get; set; } = PerfilVisual3.Limpio;

        [Display(Name = "Estela de D1 y D2 (guion por vela)", GroupName = "3. Pantalla", Order = 10)]
        public Tri3 Ver3Estela { get; set; } = Tri3.SegunPerfil;

        [Display(Name = "Zero gamma (punteado fino)", GroupName = "3. Pantalla", Order = 20)]
        public Tri3 Ver3Zero { get; set; } = Tri3.SegunPerfil;

        [Display(Name = "Zero gamma: estela (guiones por vela)", GroupName = "3. Pantalla", Order = 31,
                 Description = "3.1.2 (pedido del operador): el zero gamma de cada vela cerrada queda como guion gris, igual que D1/D2, para ver donde estuvo el flip y si el precio reacciono ahi. Lo medido en el laboratorio: como nivel de rebote el zero rebota igual que su placebo y cambia 26-31 veces por hora; la caja negra lo graba como fam ZERO para medirlo con muestra nueva.")]
        public Tri3 Ver3EstelaZero { get; set; } = Tri3.SegunPerfil;

        [Display(Name = "Zero gamma: estilo de la estela", GroupName = "3. Pantalla", Order = 32,
                 Description = "3.2.4. RomboVerde = rombos por vela como los de la 2.0 (lo que el operador ve como zona; el zero por OI va mas chico y oscuro). GuionGris = guiones finos. Lo medido no cambia: el zero como nivel de rebote iguala a su placebo.")]
        public ZeroEstilo3 Zero3Estilo { get; set; } = ZeroEstilo3.RomboVerde;

        [Display(Name = "Tunel entre D1 y D2", GroupName = "3. Pantalla", Order = 30)]
        public Tri3 Ver3Tunel { get; set; } = Tri3.SegunPerfil;

        [Display(Name = "Marca de toque", GroupName = "3. Pantalla", Order = 40)]
        public Tri3 Ver3Toques { get; set; } = Tri3.SegunPerfil;

        [Display(Name = "Majors (Major Positive verde / Major Negative rojo)", GroupName = "3. Pantalla", Order = 50,
                 Description = "Segun el perfil: apagados en NQ, prendidos en ES (perfil Limpio); prendidos en ConMajors y Todo. No se dibuja la raya si estan a mas del radio de dibujo: solo el rotulo.")]
        public Tri3 Ver3Majors { get; set; } = Tri3.SegunPerfil;

        [Display(Name = "Rotulos pegados al eje", GroupName = "3. Pantalla", Order = 60)]
        public Tri3 Ver3Rotulos { get; set; } = Tri3.SegunPerfil;

        [Display(Name = "Cabecera (un renglon)", GroupName = "3. Pantalla", Order = 70)]
        public Tri3 Ver3Cabecera { get; set; } = Tri3.SegunPerfil;

        [Display(Name = "Barras del perfil (GEX por strike)", GroupName = "3. Pantalla", Order = 80,
                 Description = "Ninguna por defecto. Izquierda: ancho maximo 12 % del lienzo, alpha 50 %, sin pelotitas ni montos.")]
        public BarrasLado3 Barras3Lado { get; set; } = BarrasLado3.SegunPerfil;

        [Display(Name = "Zona de toque en puntos (0 = auto: 2,5 NQ / 0,75 ES)", GroupName = "3. Pantalla", Order = 100,
                 Description = "La primera vela que ENTRA a esta distancia de D1/D2 lleva la marca (la anterior no estaba en la zona). Default = la TOL del pre-registro del laboratorio (laboratorio/tres/PRE_REGISTRO.md: NQ 2,5 / ES 0,75), NO un valor medido: la penetracion p75 medida en F3 es otra cosa (NQ vivo ~20 pts de noche, zona sugerida ~45).")]
        public decimal Zona3ToquePts { get; set; } = 0m;

        [Display(Name = "Rearme del toque en puntos (0 = auto: 15 NQ / 4 ES)", GroupName = "3. Pantalla", Order = 105,
                 Description = "Un toque por dominante y por lado: despues de una marca no hay otra para ese lado hasta que una vela cierre a esta distancia del nivel (LEJOS del pre-registro: NQ 15, ES 4). Lo que el juez del laboratorio cuenta como 'toque'.")]
        [Range(0, 500)]
        public decimal Lejos3Pts { get; set; } = 0m;

        [Display(Name = "Tope de rotulos", GroupName = "3. Pantalla", Order = 110, Description = "Cuantos rotulos (adentro + flechas del borde) lleva la columna, por jerarquia, despues de agrupar por (libro, strike). 3.7.3: solo los rotulos; las rayas por vela tienen su propio tope ('Tope de rayas por vela', Tope373Rayas). En la 3.7.2 (nunca instalada) este numero tambien topeaba las rayas.")]
        [Range(1, 7)]
        public int Tope3Rotulos { get; set; } = 5;

        [Display(Name = "Estela: velas hacia atras (0 = la sesion desde las 18:00 NY)", GroupName = "3. Pantalla", Order = 120)]
        [Range(0, 5000)]
        public int Estela3VelasAtras { get; set; } = 0;

        [Display(Name = "Estela: mostrar la sesion anterior (atenuada)", GroupName = "3. Pantalla", Order = 121,
                 Description = "3.0.5: los guiones y toques de antes del inicio de sesion (18:00 NY) se dibujan al 45 % en vez de esconderse. Vale para NQ y para las capas.")]
        public bool Estela3SesionAnterior { get; set; } = true;

        [Display(Name = "Tunel: alpha (%)", GroupName = "3. Pantalla", Order = 130)]
        [Range(2, 60)]
        public int Tunel3AlphaPct { get; set; } = 12;

        [Display(Name = "Barras: ancho maximo (% del lienzo)", GroupName = "3. Pantalla", Order = 140)]
        [Range(3, 30)]
        public int Barras3AnchoPct { get; set; } = 12;

        [Display(Name = "Barras: alpha (%)", GroupName = "3. Pantalla", Order = 150)]
        [Range(10, 100)]
        public int Barras3AlphaPct { get; set; } = 50;

        [Display(Name = "Tamaño de letra", GroupName = "3. Pantalla", Order = 160)]
        [Range(6, 14)]
        public decimal Tam3Letra { get; set; } = 9m;

        [Display(Name = "Margen inferior (px)", GroupName = "3. Pantalla", Order = 170,
                 Description = "ATAS entrega un ChartArea mas alto que lo visible: lo anclado abajo descuenta esto (medido: 48).")]
        [Range(0, 200)]
        public int Margen3Inferior { get; set; } = 48;

        [Display(Name = "Guardar estela (jsonl)", GroupName = "4. Archivo", Order = 10,
                 Description = "%APPDATA%\\ATAS\\PythiaGex3\\estela\\estela-<raiz>-<dia UTC>.jsonl: una linea cuando cambian las dominantes o cada 60 s. Un solo escritor por raiz.")]
        public bool Guardar3Estela { get; set; } = true;

        [Display(Name = "Guardar centinela (una linea por vela cerrada)", GroupName = "4. Archivo", Order = 20,
                 Description = "%APPDATA%\\ATAS\\pythiagex3-centinela-hoy-<instrumento>-<marco>.jsonl, mismo formato que la 2.0.")]
        public bool Guardar3Centinela { get; set; } = true;

        [Display(Name = "Guardar viva3 (foto de la cadena por minuto)", GroupName = "4. Archivo", Order = 30,
                 Description = "%APPDATA%\\ATAS\\PythiaGex3\\viva\\viva3-<raiz>-<dia>.jsonl, mismo formato que la viva vieja. Un solo escritor por raiz (la sonda no duplica).")]
        public bool Guardar3Viva { get; set; } = true;

        [Display(Name = "Rearmar la cadena ahora", GroupName = "5. Accion", Order = 10, Description = "Cambialo a mano para forzar un rearme de la ventana de strikes.")]
        public bool Rearmar3Ahora
        {
            get => false;
            set { if (value) { try { Log("rearme pedido a mano"); _ = Task.Run(() => _cadena.Rearmar(OptionsDataProvider, TradingManager, DataProvider)); } catch (Exception e) { Registro.Excepcion(LOG, "Rearmar3Ahora", e); } } }
        }

        // ------------------------------------------------------------------
        // ciclo de vida
        // ------------------------------------------------------------------

        /// <summary>Sin EnableCustomDrawing + SubscribeToDrawingEvents(Final) ATAS no llama a OnRender nunca.</summary>
        public GammaHoyTres() : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
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
                var raiz = Raiz();
                Log("Gamma Hoy " + VERSION + " arranca. instrumento=" + (InstrumentInfo?.Instrument ?? "?") + " security=" + (TradingManager?.Security?.Code ?? "?")
                    + " raiz=" + (raiz == "" ? "(no soportada)" : raiz) + " regla=" + Regla3DominantesB + (Regla3DominantesB == ReglaDominantes3.Tres ? " (clasica + histeresis " + Histeresis3Pct + " %/" + Histeresis3Min + " min, centroide " + Histeresis3CentroidePts + " pts; SIN VALIDAR)" : Regla3DominantesB == ReglaDominantes3.DosMasGrandes ? " (la de la 2.0: dos mayores |GEX vol|, strike exacto)" : "")
                    + " mult=" + Feed.MultiplicadorDe(raiz).ToString("0", CultureInfo.InvariantCulture) + " radioDibujo37=" + RadioDibujo37.ToString("0.##", CultureInfo.InvariantCulture) + " formulaTunel=" + Doms3FormulaB + " tunelNoche=" + Tunel3DeNocheB + " baseNdx=" + Capa3BaseNdxB
                    + " perfil=" + Perfil3Visual + " horizonte=" + Horizonte3 + " noche=" + Noche3Dominantes
                    + " radioDom=" + RadioDominantes(raiz).ToString("0.##", CultureInfo.InvariantCulture) + " radioDibujo=" + RadioDibujo(raiz).ToString("0.##", CultureInfo.InvariantCulture)
                    + " zona=" + ZonaToque(raiz).ToString("0.##", CultureInfo.InvariantCulture) + " lejos=" + Lejos(raiz).ToString("0.##", CultureInfo.InvariantCulture) + " tope=" + Tope3Contratos + " ventana=" + Ventana3DensaPct + "/" + Ventana3RalaPct + " % vencimientos=" + Vencimientos3
                    + " datos=" + Registro.CarpetaDatos);
                _tick = Tick;
                SubscribeToTimer(_periodo, _tick);
                EstadoPulsoArrancar();   // 3.2.1: el pulso por cambio (1 s) para Profundidad 3.0; solo escribe indicador.json
                CintaArrancar();         // 3.4.0: la cinta en vivo (hilo escritor + reloj de 2 s); nunca tira
            }
            catch (Exception e) { Registro.Excepcion(LOG, "OnInitialize", e); }
        }

        protected override void OnDispose()
        {
            try { if (_tick != null) UnsubscribeFromTimer(_periodo, _tick); } catch { }
            EstadoPulsoParar();
            CintaParar();            // 3.4.0: vacia la cola, cierra el archivo y suelta la raiz
            try { _cadena.Parar(); } catch (Exception e) { Registro.Excepcion(LOG, "OnDispose", e); }
            try { _centinela?.Volcar(true); } catch { }
            Log("Gamma Hoy 3.0 quitada del grafico");
        }

        // ------------------------------------------------------------------
        // raiz y defaults por raiz
        // ------------------------------------------------------------------

        private string Raiz()
        {
            if (!string.IsNullOrWhiteSpace(Raiz3Manual)) return CadenaApi.RaizGrande(Raiz3Manual.Trim());
            return CadenaApi.RaizGrande(TradingManager?.Security?.Code ?? InstrumentInfo?.Instrument ?? "");
        }

        private string CodigoGrafico()
        {
            if (!string.IsNullOrWhiteSpace(Raiz3Manual)) return Raiz3Manual.Trim().ToUpperInvariant();
            var c = TradingManager?.Security?.Code;
            if (!string.IsNullOrEmpty(c)) return c;
            return InstrumentInfo?.Instrument ?? "";
        }

        private double RadioDominantes(string raiz) => Radio3DominantesPts > 0 ? (double)Radio3DominantesPts : (raiz == "ES" ? 25.0 : raiz == "RTY" ? 20.0 : 100.0);
        private double RadioDibujo(string raiz) => RadioDibujo37;   // 3.7.0 (D1): un solo radio, 'Dominantes: radio de dibujo (pts)', contra el cierre de cada vela
        private double ZonaToque(string raiz) => Zona3ToquePts > 0 ? (double)Zona3ToquePts : (raiz == "ES" ? 0.75 : raiz == "RTY" ? 0.6 : 2.5);
        private double Lejos(string raiz) => Lejos3Pts > 0 ? (double)Lejos3Pts : (raiz == "ES" ? 4.0 : raiz == "RTY" ? 3.0 : 15.0);
        private ReglaDominantes3 ReglaEfectiva() => Regla3DominantesB;

        private static bool Ef(Tri3 t, bool porPerfil) => t == Tri3.SegunPerfil ? porPerfil : t == Tri3.Si;
        private bool VerEstelaEf => Ef(Ver3Estela, true);
        private bool VerZeroEf => Ef(Ver3Zero, true);
        private bool VerEstelaZeroEf => Ef(Ver3EstelaZero, true);
        private bool VerTunelEf => Ef(Ver3Tunel, true);
        private bool VerToquesEf => Ef(Ver3Toques, true);
        private bool VerMajorsEf(string raiz) => Ef(Ver3Majors, Perfil3Visual != PerfilVisual3.Limpio || raiz == "ES");
        private bool VerRotulosEf => Ef(Ver3Rotulos, true);
        private bool VerCabeceraEf => Ef(Ver3Cabecera, true);
        private bool BarrasIzqEf => Barras3Lado == BarrasLado3.SegunPerfil ? Perfil3Visual == PerfilVisual3.Todo : Barras3Lado == BarrasLado3.Izquierda;

        private double PrecioGrafico()
        {
            try { return CurrentBar > 0 ? (double)GetCandle(Math.Max(0, CurrentBar - 1)).Close : 0; } catch { return 0; }
        }

        private static DateTime Utc(DateTime t) => t.Kind == DateTimeKind.Utc ? t : (t.Kind == DateTimeKind.Local ? t.ToUniversalTime() : DateTime.SpecifyKind(t, DateTimeKind.Utc));

        // ------------------------------------------------------------------
        // el latido: cada 5 s (con el mercado cerrado no hay ticks: aca se reprecia igual)
        // ------------------------------------------------------------------

        private void Tick()
        {
            try
            {
                var ahora = DateTime.UtcNow;
                var raiz = Raiz();
                if (raiz == "")
                {
                    if (!_sinRaizAvisado) { _sinRaizAvisado = true; Log("el grafico es " + CodigoGrafico() + ": no es NQ/ES/RTY, no hago nada"); }
                    try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
                    return;
                }
                AplicarAjustes(raiz);

                // la cadena: arranque, latido, recentrado
                if (!_arrancada || (!_cadena.Activa && (ahora - _ultimoIntentoArranque).TotalSeconds >= 120))
                {
                    _arrancada = true; _ultimoIntentoArranque = ahora;
                    _ = Task.Run(() => _cadena.Arrancar(OptionsDataProvider, TradingManager, DataProvider, CodigoGrafico(), PrecioGrafico, Log));
                }
                else
                {
                    _cadena.Latido();
                    // REARME POR VENCIMIENTO (06-10, 'funciona de noche tal cual?'): las series se eligen al armar; si no se rearmara, a las 16:00 NY
                    // el libro quedaria con una sola serie (la de mañana) hasta que el precio se moviera 0,5 %. Con filas vencidas en el libro se
                    // rearma cada 15 min (la API lista las series con 30 min de gracia) y, siempre, una vez cada 6 h para refrescar la lista.
                    var fotoR = _cadena.Foto();
                    bool porVencidas = fotoR.Vencidas > 0 && (ahora - _ultimoRearmeVencidas).TotalMinutes >= 15;
                    bool periodico = fotoR.UltimoArmadoUtc != DateTime.MinValue && (ahora - fotoR.UltimoArmadoUtc).TotalHours >= 6;
                    if ((porVencidas || periodico) && (ahora - _ultimoRearme).TotalSeconds >= 60)
                    {
                        _ultimoRearme = ahora; _ultimoRearmeVencidas = ahora;
                        Log("rearmo: " + (porVencidas ? fotoR.Vencidas + " fila(s) de una serie vencida en el libro (cambio de vencimiento)" : "refresco periodico de series (6 h)"));
                        _ = Task.Run(() => _cadena.Rearmar(OptionsDataProvider, TradingManager, DataProvider));
                    }
                    if (_cadena.HayQueRecentrar() && (ahora - _ultimoRearme).TotalSeconds >= 60)
                    {
                        _ultimoRearme = ahora;
                        Log("el precio se alejo del centro (" + _cadena.Futuro.ToString("0.##", CultureInfo.InvariantCulture) + " vs " + _cadena.CentroVentana.ToString("0.##", CultureInfo.InvariantCulture) + "): rearmo");
                        _ = Task.Run(() => _cadena.Rearmar(OptionsDataProvider, TradingManager, DataProvider));
                    }
                }

                // la cuenta, con el precio del GRAFICO como futuro (MNQ cotiza los mismos puntos que NQ)
                double fut = PrecioGrafico();
                var foto = _cadena.Foto();
                Feed.Cadena c = null; int utiles = 0;
                List<CadenaApi.Fila> fsApi = null;
                if (_cadena.Activa && fut > 0)
                {
                    try { fsApi = _cadena.Filas(); c = DesdeApi(raiz, foto, fsApi, out utiles); } catch (Exception e) { Registro.Excepcion(LOG, "DesdeApi", e); }
                }
                ActualizarOiNq(raiz, fsApi, ahora);   // 3.7.0 C1: el salto de OI de Rithmic (una foto por minuto) y si el OI de NQ es de anteayer
                if (c != null)
                {
                    var L = _nucleo.Calcular(c, fut, ahora);
                    if (L != null && !L.SinBase)
                    {
                        // 3.7.0 D2: DosMasGrandes por volumen (la 2.0) siempre; las otras reglas como antes, con el tunel C2 y el OI con fecha (C1)
                        SeleccionarDoms(L, fut, UltimoCierre(), ahora, raiz, false, LibroCompleto(foto, ahora), _oiNqFresco);
                        ActualizarApoyo(c, L, fut, RadioDibujo(raiz));   // 3.2.3: libro profundo (contratos apoyados por strike)
                        lock (_candado) { _L = L; _c = c; _futuro = fut; _strikesUtiles = utiles; _ultimaCuentaUtc = ahora; }
                        if ((ahora - _ultimoAudit).TotalSeconds >= 60)
                        {
                            _ultimoAudit = ahora;
                            Log("AUDIT3 regla=" + ReglaEfectiva() + (ReglaEfectiva() == ReglaDominantes3.Tres ? " hist[" + Hist.Resumen() + "]" : "") + " oiNQ=" + (_oiNqFresco ? "ayer" : "anteayer") + " " + GammaHoyNucleo.Audit(L, c, _cadena.Activa).Substring(6)
                                + " strikesUtiles=" + utiles + " suscritos=" + foto.Suscritos + " conPuntas=" + foto.ConPuntas + " conOI=" + foto.ConOI
                                + " edadDato=" + (double.IsNaN(foto.EdadSegundos) ? "NaN" : foto.EdadSegundos.ToString("0", CultureInfo.InvariantCulture)) + "s"
                                + " dias=" + string.Join("/", c.Dias.Select(d => d.ToString("0.###", CultureInfo.InvariantCulture))) + " precioGrafico=" + fut.ToString("0.##", CultureInfo.InvariantCulture)
                                + " futuroApi=" + _cadena.Futuro.ToString("0.##", CultureInfo.InvariantCulture) + " via=" + foto.Via);
                            try { var inv0 = CultureInfo.InvariantCulture; Log("CRUCES3 S=" + L.S.ToString("0.00", inv0) + " oi=[" + string.Join(" ", (L.ZeroCrucesOiLista ?? new List<double>()).Select(z => z.ToString("0.00", inv0))) + "] vol=[" + string.Join(" ", (L.ZeroCrucesVolLista ?? new List<double>()).Select(z => z.ToString("0.00", inv0))) + "] perfilOI=" + string.Join(" ", L.Perfil.Where(s => Math.Abs(s.K - L.S) <= 50).Select(s => s.K.ToString("0", inv0) + ":" + (s.GexOi / 1e6).ToString("0.0", inv0)))); } catch { }   // 3.5.4: para auditar los cruces
                        }
                        if (Guardar3Estela) GuardarEstela(raiz, ahora, L);
                    }
                    else if (L != null && L.SinBase) Log("la cuenta vino SinBase (no deberia: EsFuturo=true)");
                }
                else lock (_candado) { _strikesUtiles = utiles; }
                ActualizarCapas(raiz, fut, ahora);   // capas QQQ/NDX de CBOE (06-10): se bajan, se cuentan y se guardan aparte

                RecalcularInicioSesion();
                SembrarMemoriaSiHaceFalta(raiz);   // la memoria: estela guardada + rebobinado de viva3 (06-10)
                // la vela cerrada se anota TAMBIEN desde aca: en una pestaña oculta ATAS no manda OnCalculate (medido 15-09)
                RegistrarVelaCerrada(CurrentBar - 2);
                CajaLatido(raiz, ahora);   // 3.1.0: la caja negra (toques con opciones + cinta + libro, y resultados a 5/15/30 min)
                EstadoPulso(raiz, ahora);  // 3.2.0: el latido de 5 s para Profundidad 3.0 (indicador.json atomico; no dibuja nada); 3.2.1: ademas el pulso por cambio de 1 s (GammaHoyTresEstado.cs)

                if ((ahora - _ultimoMinuto).TotalSeconds >= 60)
                {
                    _ultimoMinuto = ahora;
                    Log("minuto: via=" + foto.Via + " series=" + foto.Series + " suscritos=" + foto.Suscritos + "/" + foto.Contratos + " conPuntas=" + foto.ConPuntas + " conOI=" + foto.ConOI
                        + " rechazadas=" + foto.Rechazadas + " errores=" + foto.Errores + " vencidasFuera=" + foto.Vencidas + " cambios=" + _cadena.Cambios + " strikesUtiles=" + utiles
                        + " cuenta=" + (_L == null ? "no" : "si") + " edad=" + (double.IsNaN(foto.EdadSegundos) ? "-" : foto.EdadSegundos.ToString("0") + " s") + " | " + foto.Texto);
                    if (Guardar3Viva) Viva3.Guardar(_cadena, raiz, this, LOG);
                }
                try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "Tick", e); }
        }

        private void AplicarAjustes(string raiz)
        {
            _cadena.TopeContratos = Math.Max(20, Math.Min(480, Tope3Contratos));
            _cadena.VentanaDensaPct = (double)Math.Max(0.1m, Ventana3DensaPct);
            _cadena.VentanaRalaPct = (double)Math.Max(Ventana3DensaPct, Ventana3RalaPct);
            _cadena.Vencimientos = Math.Max(1, Math.Min(4, Vencimientos3));
            _cadena.RecentrarPct = (double)Math.Max(0.1m, Recentrar3Pct);

            var a = _nucleo.A;
            a.Horizonte = Horizonte3;
            a.CuantasDominantes = 2;
            a.RadioDominantesPct = 2.0;
            a.RadioDominantesMaxPts = RadioDominantes(raiz);
            a.EmpatePct = Math.Max(0, Math.Min(90, Empate3Pct));
            a.DominantesDeNoche = Noche3Dominantes;
            a.ZeroInterpolado = Zero3Interpolado;
            var regla = ReglaEfectiva();
            a.UnaPorLado = regla != ReglaDominantes3.DosMasGrandes;      // Clasica y Tres: una por lado + empate
            // Tres: en la rueda hace su propio centroide (6 pts) sobre strikes exactos; fuera de la rueda es la clasica a secas (centroide 12) (3.2.2)
            a.Centroide = !Rayas3Independientes && (regla == ReglaDominantes3.Clasica || (regla == ReglaDominantes3.Tres && !Histeresis3.EsRueda(DateTime.UtcNow)));   // 3.5.0   // Tunel: strikes exactos (3.3.0)
            a.RadioCentroidePts = 12.0;
        }

        /// <summary>La cadena del nucleo armada desde CadenaApi.Filas(): una Fila por (strike corrido, vencimiento) con call y put
        /// juntos; Dias por vencimiento con hora NY (ya vienen asi de la API); EsFuturo = true (Black-76), sin base. Como el
        /// DesdeViva de la 2.0, pero con la API como unica fuente.</summary>
        private Feed.Cadena DesdeApi(string raiz, CadenaApi.Estado foto, out int utiles) => DesdeApi(raiz, foto, _cadena.Filas(), out utiles);

        /// <summary>3.2.1: la misma cuenta con las filas ya pedidas (el pulso por cambio llama a Filas() una sola vez para la cuenta y la escalera).</summary>
        private Feed.Cadena DesdeApi(string raiz, CadenaApi.Estado foto, List<CadenaApi.Fila> fs, out int utiles)
        {
            utiles = 0;
            if (fs == null || fs.Count == 0) return null;
            var dias = fs.Where(f => f.ConPuntas && !double.IsNaN(f.IV) && f.IV > 0).Select(f => Math.Round(f.Dias, 4)).Distinct().OrderBy(x => x).ToList();
            if (dias.Count == 0) return null;
            var idx = new Dictionary<double, int>();
            for (int i = 0; i < dias.Count; i++) idx[dias[i]] = i;
            var porClave = new Dictionary<(double, int), Feed.Fila>();
            foreach (var f in fs)
            {
                if (!f.ConPuntas || double.IsNaN(f.IV) || f.IV <= 0) continue;
                if (f.OI <= 0 && f.VolHoy <= 0) continue;
                if (!idx.TryGetValue(Math.Round(f.Dias, 4), out int v)) continue;
                if (!porClave.TryGetValue((f.K, v), out var fila)) { fila = new Feed.Fila { K = f.K, K0 = f.K0, V = v }; porClave[(f.K, v)] = fila; }
                if (f.EsCall) { fila.OiC = f.OI; fila.IvC = f.IV; fila.VolC = f.VolHoy; fila.BidVolC = f.BidVol; fila.AskVolC = f.AskVol; }
                else { fila.OiP = f.OI; fila.IvP = f.IV; fila.VolP = f.VolHoy; fila.BidVolP = f.BidVol; fila.AskVolP = f.AskVol; }
            }
            utiles = porClave.Values.Where(x => x.IvC > 0 && x.IvP > 0).Select(x => x.K).Distinct().Count();
            if (utiles < MIN_STRIKES_UTILES) return null;
            var ahora = DateTime.UtcNow;
            double edadMin = double.IsNaN(foto.EdadSegundos) ? 0 : foto.EdadSegundos / 60.0;
            return new Feed.Cadena
            {
                Ts = ahora.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), SpotIdx = _cadena.Futuro,
                Dias = dias.ToArray(), Filas = porClave.Values.OrderBy(x => x.K).ThenBy(x => x.V).ToList(),
                Base = 0, BaseConfiable = true, EdadMin = edadMin, UltimoTrade = "", HorizonteCadena = dias[dias.Count - 1],
                RecibidoUtc = ahora, GeneradoUtc = ahora, EsFuturo = true, Fuente = raiz + " vivo (API)",
                Multiplicador = Feed.MultiplicadorDe(raiz),   // 3.7.0 C4: NQ 20 USD/pt (antes 100: montos x5)
            };
        }

        /// <summary>Desde que vela se dibuja la estela: la sesion arranca a las 18:00 de Nueva York de la vispera.</summary>
        private void RecalcularInicioSesion()
        {
            try
            {
                int ult = CurrentBar - 1;
                if (ult < 0) { _barInicioSesion = -1; return; }
                if (Estela3VelasAtras > 0) { _barInicioSesion = Math.Max(0, ult - Estela3VelasAtras); return; }
                var ny = RelojNy.Ahora();
                var inicioNy = ny.Hour >= 18 ? ny.Date.AddHours(18) : ny.Date.AddDays(-1).AddHours(18);
                var inicioUtc = inicioNy + (DateTime.UtcNow - ny);
                int b = ult;
                for (int n = 0; b > 0 && n < 6000; n++, b--)
                {
                    IndicatorCandle c; try { c = GetCandle(b); } catch { break; }
                    if (c == null || Utc(c.Time) < inicioUtc) break;
                }
                _barInicioSesion = Math.Max(0, b);
            }
            catch (Exception e) { Registro.Excepcion(LOG, "RecalcularInicioSesion", e); }
        }

        // ------------------------------------------------------------------
        // la vela CERRADA: estela, toque y centinela (desde OnCalculate en el borde vivo y desde el temporizador)
        // ------------------------------------------------------------------

        protected override void OnCalculate(int bar, decimal value)
        {
            try
            {
                // SOLO el borde vivo (la misma guarda que la 2.0, GammaHoy.cs OnCalculate). En un recorrido historico (carga del
                // grafico, reconexion de Rithmic, cambio de marco, pestaña que vuelve) ATAS pasa por TODAS las velas en rafaga:
                // sin esta guarda se anotaban velas viejas con los niveles de AHORA y el centinela escribia dato falso.
                if (bar != CurrentBar - 1) return;
                RegistrarVelaCerrada(bar - 1);
            }
            catch (Exception e) { Registro.Excepcion(LOG, "OnCalculate", e); }
        }

        /// <summary>
        /// Anota la vela CERRADA (la anterior a la que se esta formando) con los niveles vigentes AHORA: guion de la estela,
        /// marca de toque y linea del centinela. La llaman OnCalculate (borde vivo) y el temporizador (pestaña oculta: ATAS no
        /// manda OnCalculate, medido 15-09, memoria traspaso-2026-09-15-capas-nq). La guarda es por HORA de la vela: la misma
        /// vela no se anota dos veces aunque la pidan los dos, y si ATAS reindexa la serie no se repite ni se corre. Si hubo un
        /// hueco (mucho rato en otra pestaña), las velas del medio quedan SIN guion antes que con uno falso: solo se anota la
        /// ultima cerrada. Si la cuenta tiene mas de 3 minutos, el centinela lleva la vela sin niveles (niv vacio) y no hay guion.
        /// </summary>
        private void RegistrarVelaCerrada(int cerrada)
        {
            try
            {
                if (cerrada < 1 || cerrada > CurrentBar - 2) return;
                lock (_llaveVelas)
                {
                    GammaHoyNucleo.Lectura L; double fut; DateTime cuenta;
                    lock (_candado) { L = _L; fut = _futuro; cuenta = _ultimaCuentaUtc; }
                    if (L == null || fut <= 0) return;   // sin cadena todavia: nada que anotar
                    IndicatorCandle c; try { c = GetCandle(cerrada); } catch { return; }
                    if (c == null) return;
                    var tVela = Utc(c.Time);
                    if (tVela <= _ultimaVelaUtc) return;   // ya anotada (o reindexada hacia atras)
                    _ultimaVelaUtc = tVela;
                    IndicatorCandle prev = null; try { prev = GetCandle(cerrada - 1); } catch { }

                    bool cuentaFresca = cuenta != DateTime.MinValue && (DateTime.UtcNow - cuenta).TotalMinutes <= 3;
                    double d1 = L.Doms.Count > 0 ? L.Doms[0].Fut : double.NaN;
                    double d2 = L.Doms.Count > 1 ? L.Doms[1].Fut : double.NaN;
                    double hi = (double)c.High, lo = (double)c.Low, cl = (double)c.Close;

                    List<(double Nivel, bool Arriba)> toques = null;
                    if (cuentaFresca)
                    {
                        var raiz = Raiz();
                        double zona = ZonaToque(raiz), lejos = Lejos(raiz);
                        var doms = new[] { d1, d2 };
                        for (int i = 0; i < doms.Length; i++)
                        {
                            double d = doms[i];
                            if (double.IsNaN(d) || d <= 0) continue;
                            // toque = la primera vela que ENTRA en la banda [d - zona, d + zona]: la anterior no estaba (pre-registro
                            // del laboratorio, PRE_REGISTRO.md seccion 3). Una vela que se queda apoyada en la banda no vuelve a marcar.
                            bool enZona = lo <= d + zona && hi >= d - zona;
                            bool prevEnZona = prev != null && (double)prev.Low <= d + zona && (double)prev.High >= d - zona;
                            if (enZona && !prevEnZona && prev != null)
                            {
                                int lado = (double)prev.Close > d ? +1 : -1;   // +1 llega desde arriba (soporte), -1 desde abajo (resistencia)
                                if (!_toqueArmado.TryGetValue((i, lado), out bool armado) || armado)   // la primera vez esta armado
                                {
                                    toques ??= new List<(double, bool)>();
                                    toques.Add((d, lado == -1));
                                    _toqueArmado[(i, lado)] = false;
                                }
                            }
                            // rearme: un cierre a >= LEJOS del nivel, del lado de la llegada, habilita el proximo toque de ese lado
                            if (cl >= d + lejos) _toqueArmado[(i, +1)] = true;
                            if (cl <= d - lejos) _toqueArmado[(i, -1)] = true;
                        }
                    }
                    lock (_candado)
                    {
                        if (cuentaFresca) _estela[tVela] = new[] { d1, d2, L.ZeroVol };
                        if (toques != null) _toques[tVela] = toques;
                        if (_estela.Count > 6000)
                            foreach (var k in _estela.Keys.OrderBy(x => x).Take(_estela.Count - 5000).ToList()) { _estela.Remove(k); _toques.Remove(k); }
                    }

                    RegistrarCapasVela(tVela, cl);   // 3.7.0: con el cierre de la vela (techo/piso del zero de NDX contra SU cierre)
                    RegistrarApoyoVela(tVela, L);   // 3.2.3
                    if (cuentaFresca) { RegistrarMajorOiVela(tVela, L, _oiNqFresco); RegistrarFormulasVela(tVela, L, fut, cl); }   // 3.6.1; 3.7.0 C1: M± OI solo con OI de ayer
                    if (Guardar3Centinela)
                    {
                        if (_centinela == null)
                        {
                            var instr = InstrumentInfo != null ? InstrumentInfo.Instrument : "x";
                            var marco = ChartInfo != null && ChartInfo.ChartType != null ? ChartInfo.ChartType + "-" + ChartInfo.TimeFrame : "x";
                            _centinela = new Centinela("hoy-" + instr, marco);
                            Log("centinela: " + _centinela.Ruta);
                        }
                        bool oiOk = _oiNqFresco;   // 3.7.0 C1: con el OI de anteayer el zero por OI no se anota (dato falso)
                        var niv = cuentaFresca
                            ? GammaHoyNucleo.Niveles(L).Where(kv => kv.Key == "dom0" || kv.Key == "dom1" || kv.Key == "zero_vol" || (kv.Key == "zero_oi" && oiOk) || kv.Key == "mp_vol" || kv.Key == "mn_vol").ToList()
                            : new List<KeyValuePair<string, double>>();
                        // el contador monotono reemplaza al numero de barra en la guarda del centinela (una reindexacion no lo confunde)
                        _centinela.Anotar(++_nCentinela, Utc(c.LastTime != default(DateTime) ? c.LastTime : c.Time),
                            (double)c.Open, hi, lo, cl, (double)c.Volume, (double)c.Ticks, (double)c.Delta, cuentaFresca ? fut : double.NaN, niv);
                    }
                }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "RegistrarVelaCerrada", e); }
        }

        // ------------------------------------------------------------------
        // la estela en archivo (formato identico a GammaHoyCapas.GuardarGuionCapa)
        // ------------------------------------------------------------------

        private void GuardarEstela(string raiz, DateTime horaUtc, GammaHoyNucleo.Lectura L, string extraJson = null)
        {
            try
            {
                var inv = CultureInfo.InvariantCulture;
                double d1 = L.Doms.Count > 0 ? L.Doms[0].Fut : double.NaN, d2 = L.Doms.Count > 1 ? L.Doms[1].Fut : double.NaN;
                string F(double x) => double.IsNaN(x) || x <= 0 ? "0" : x.ToString("0.00", inv);
                string d = F(d1) + "," + F(d2) + "," + F(L.ZeroVol);
                bool capa = string.Equals(raiz, "QQQ", StringComparison.OrdinalIgnoreCase) || string.Equals(raiz, "NDX", StringComparison.OrdinalIgnoreCase);
                bool oiOk = capa || _oiNqFresco;   // 3.7.0 C1: el libro de NQ con OI de anteayer no guarda majors por OI (la memoria los sembraria)
                string m = oiOk ? F(L.MpOi) + "," + F(L.MnOi) : "0,0";   // 3.6.2: majors por OI, para la memoria
                extraJson = (extraJson ?? "") + ",\"mu\":" + L.Multiplicador.ToString("0", inv) + (capa ? "" : ",\"oi\":\"" + (oiOk ? "ayer" : "anteayer") + "\"");   // 3.7.0 C4/C1
                if (d == "0,0,0") return;
                lock (_estelaLlave)
                {
                    if (_estelaEscritor.TryGetValue(raiz, out var esc) && !ReferenceEquals(esc.Dueno, this) && (horaUtc - esc.Hora).TotalSeconds < 180) return;   // escribe otro grafico
                    _estelaEscritor[raiz] = (this, horaUtc);
                    // cuando cambia, o cada 60 s
                    bool igual = _estelaUltimaLinea.TryGetValue(raiz, out var u) && u == d + "|" + m;
                    if (igual && _estelaUltimaHora.TryGetValue(raiz, out var h) && (horaUtc - h).TotalSeconds < 60) return;
                    _estelaUltimaLinea[raiz] = d + "|" + m; _estelaUltimaHora[raiz] = horaUtc;
                }
                string fz = string.Join(",", L.Doms.Take(2).Select(x => (Math.Abs(x.Gex) / 1e6).ToString("0", inv)));
                string extra = ",\"g\":[" + fz + "],\"n\":" + (L.NetVol / 1e6).ToString("0", inv) + ",\"b\":\"" + (L.LibroDom ?? "vol") + "\",\"f\":" + L.Futuro.ToString("0.00", inv) + ",\"m\":[" + m + "]" + (L.Base != 0 ? ",\"bs\":" + L.Base.ToString("0.00", inv) : "") + extraJson;   // 3.6.6: base usada; 3.7.0: mu (USD/pt), oi
                var ruta = Path.Combine(Registro.CarpetaDatos, "estela", "estela-" + raiz + "-" + horaUtc.ToString("yyyy-MM-dd", inv) + ".jsonl");
                Registro.Anexar(ruta, "{\"t\":\"" + horaUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", inv) + "\",\"d\":[" + d + "]" + extra + ",\"v\":\"" + VERSION + "\"}");   // 3.7.2: 'v' (la memoria solo siembra estela de reglas 3.7.2+)
            }
            catch (Exception e) { Registro.Excepcion(LOG, "GuardarEstela", e); }
        }

        // ------------------------------------------------------------------
        // el dibujo
        // ------------------------------------------------------------------

        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            try { Pintar(g); }
            catch (Exception e) { Registro.Excepcion(LOG, "OnRender", e); }
        }

        private void Pintar(RenderContext g)
        {
            if (ChartInfo == null) return;
            var cont = ChartInfo.PriceChartContainer;
            if (cont == null) return;
            var area = ChartArea;
            int xr = area.Right;
            try { var cb = g.ClipBounds; if (cb.Width > 0) xr = Math.Min(area.Right, cb.Right); } catch { }
            int piso = area.Bottom - Math.Max(0, Margen3Inferior);
            var es = CultureInfo.GetCultureInfo("es-AR");
            float tam = (float)Math.Max(6m, Math.Min(14m, Tam3Letra));
            var f = new RenderFont("Consolas", tam);
            if (_renders++ == 0) Log("primer render: area=" + area + " clip=" + g.ClipBounds);

            var raiz = Raiz();
            if (raiz == "")
            {
                Cartel(g, f, area, "PythiaGex 3.0: el grafico es " + CodigoGrafico() + ", no NQ/ES/RTY: no hago nada", ColNaranja);
                return;
            }

            // copia del estado bajo llave
            GammaHoyNucleo.Lectura L; Feed.Cadena c; double fut; int strikes; DateTime cuenta;
            var est = new Dictionary<int, double[]>(); var toq = new Dictionary<int, List<(double Nivel, bool Arriba)>>();
            int desde = Math.Max(0, FirstVisibleBarNumber), hasta = Math.Min(CurrentBar - 1, LastVisibleBarNumber);
            // la estela esta indexada por hora de vela: se traducen las barras visibles a su hora (fuera de la llave)
            var horas = new List<(int B, DateTime T)>();
            var cierreBar = new Dictionary<int, double>();   // 3.7.0 D1: el cierre de cada vela visible (el radio de dibujo es contra SU cierre)
            for (int b = desde; b <= hasta && horas.Count < 6000; b++) { try { var cb = GetCandle(b); if (cb != null) { horas.Add((b, Utc(cb.Time))); cierreBar[b] = (double)cb.Close; } } catch { } }
            lock (_candado)
            {
                L = _L; c = _c; fut = _futuro; strikes = _strikesUtiles; cuenta = _ultimaCuentaUtc;
                foreach (var (b, t) in horas)
                {
                    if (_estela.TryGetValue(t, out var e0)) est[b] = e0;
                    if (_toques.TryGetValue(t, out var t0)) toq[b] = t0;
                }
            }
            var foto = _cadena.Foto();
            _marcasPend.Clear();   // 3.7.1: la cola de marcas es de este dibujo
            PrepararAtravesadas(desde, hasta);   // 3.6.4

            // ---- cabecera (si esta prendida) y cartel de estado (siempre que haga falta: que se sepa por que no hay cuenta)
            Cabecera(g, f, area, raiz, L, fut, strikes, foto, cuenta, VerCabeceraEf);

            // sin cuenta viva (arranque, pausa de Globex, libro caido) igual se dibuja lo que la memoria sembro: estela, toques y capas,
            // con el precio del grafico como referencia (3.0.4; antes el grafico quedaba vacio hasta la primera cuenta)
            bool sinCuenta = L == null;
            if (fut <= 0) { try { fut = PrecioGrafico(); } catch { fut = 0; } }
            if (fut <= 0) return;
            double radioDib = RadioDibujo(raiz);
            int ultimaBar = CurrentBar - 1;
            // 3.7.0 D1: una marca de estela en p se dibuja en la vela b solo si |p - cierre de b| <= radio (la vela viva: el precio de ahora)
            bool EnRadioBar(int b, double p) => Seleccion37.EnRadio(p, b >= ultimaBar ? fut : (cierreBar.TryGetValue(b, out var cc) ? cc : fut), radioDib);
            double tick = 0.25; try { tick = (double)(InstrumentInfo?.TickSize ?? 0.25m); if (tick <= 0) tick = 0.25; } catch { }
            int Y(double p) { try { return cont.GetYByPrice((decimal)p, false); } catch { return int.MinValue; } }
            bool EnPantalla(int y) => y != int.MinValue && y >= area.Top && y <= piso;
            double d1 = !sinCuenta && L.Doms.Count > 0 ? L.Doms[0].Fut : double.NaN, d2 = !sinCuenta && L.Doms.Count > 1 ? L.Doms[1].Fut : double.NaN;
            bool Cerca(double p) => !double.IsNaN(p) && p > 0 && Math.Abs(p - fut) <= radioDib;

            // ---- barras del perfil a la izquierda (opcional, apagadas por defecto)
            if (BarrasIzqEf && !sinCuenta && L.Perfil.Count > 0 && L.MaxAbsVol > 0)
            {
                int anchoMax = Math.Max(10, (xr - area.Left) * Math.Max(3, Math.Min(30, Barras3AnchoPct)) / 100);
                int alfa = 255 * Math.Max(10, Math.Min(100, Barras3AlphaPct)) / 100;
                int esp = int.MaxValue;
                for (int i = 1; i < L.Perfil.Count; i++) { int ya = Y(L.Perfil[i - 1].Fut), yb = Y(L.Perfil[i].Fut); if (ya != int.MinValue && yb != int.MinValue) esp = Math.Min(esp, Math.Abs(ya - yb)); }
                int alto = esp == int.MaxValue ? 10 : Math.Max(2, Math.Min(10, esp - 1));
                foreach (var s in L.Perfil)
                {
                    if (s.GexVol == 0) continue;
                    int y = Y(s.Fut); if (!EnPantalla(y)) continue;
                    int largo = (int)Math.Round(anchoMax * Math.Abs(s.GexVol) / L.MaxAbsVol);
                    if (largo < 1) continue;
                    g.FillRectangle(Color.FromArgb(alfa, s.GexVol > 0 ? ColPos : ColNeg), new Rectangle(area.Left, y - alto / 2, largo, alto));
                }
            }

            // ---- tunel entre D1 y D2 vigentes: desde la ultima vela hasta el eje, solo si las dos estan cerca
            int xUlt = int.MinValue; try { xUlt = cont.GetXByBar(Math.Max(0, CurrentBar - 1), false); } catch { }
            if (VerTunelEf && Tunel3Sombreado && Cerca(d1) && Cerca(d2) && xUlt != int.MinValue)   // 3.5.5
            {
                int y1 = Y(d1), y2 = Y(d2);
                if (y1 != int.MinValue && y2 != int.MinValue)
                {
                    int yt = Math.Max(area.Top, Math.Min(y1, y2)), yb = Math.Min(piso, Math.Max(y1, y2));
                    int x0 = Math.Max(area.Left, xUlt);
                    if (yb > yt && xr > x0) g.FillRectangle(Color.FromArgb(255 * Math.Max(2, Math.Min(60, Tunel3AlphaPct)) / 100, ColDom), new Rectangle(x0, yt, xr - x0, yb - yt));
                }
            }

            // ---- zero gamma: UNA linea punteada fina (3 px raya, 3 px hueco) que cruza el grafico
            if (VerZeroEf && Rayas3Largas && Raya3NqZeroB && !sinCuenta && !double.IsNaN(L.ZeroVol) && L.ZeroVol > 0)   // 3.5.5; 3.7.0 D4: apagado por defecto
            {
                int y = Y(L.ZeroVol);
                if (EnPantalla(y))
                {
                    var pen = new RenderPen(Color.FromArgb(200, ColZero), 1f);
                    for (int x = area.Left; x < xr; x += 6) g.DrawLine(pen, x, y, Math.Min(x + 3, xr), y);
                }
            }

            // ---- majors: 1 px continuo, solo dentro del radio de dibujo (fuera: solo rotulo)
            bool majors = VerMajorsEf(raiz);
            if (majors && !sinCuenta)
            {
                if (Rayas3Largas && Raya3NqMasVolB && Cerca(L.MpVol)) { int y = Y(L.MpVol); if (EnPantalla(y)) g.DrawLine(new RenderPen(Color.FromArgb(200, ColPos), 1f), area.Left, y, xr, y); }
                if (Rayas3Largas && Raya3NqMenosVolB && Cerca(L.MnVol)) { int y = Y(L.MnVol); if (EnPantalla(y)) g.DrawLine(new RenderPen(Color.FromArgb(200, ColNeg), 1f), area.Left, y, xr, y); }
            }

            // ---- estela de D1/D2 (un guion por vela cerrada) y marcas de toque
            if ((VerEstelaEf || VerToquesEf) && hasta >= desde)
            {
                int bw = 5;
                try { if (hasta > desde) bw = Math.Max(3, (cont.GetXByBar(hasta, false) - cont.GetXByBar(desde, false)) / Math.Max(1, hasta - desde)); } catch { }
                int wGuion = Math.Max(1, bw - 1);
                int ultima = CurrentBar - 1;
                int inicio = Estela3VelasAtras > 0 ? Math.Max(0, ultima - Estela3VelasAtras) : _barInicioSesion;
                for (int b = desde; b <= hasta; b++)
                {
                    bool previa = b < inicio;                       // sesion anterior: atenuada (3.0.5) u oculta
                    if (previa && !Estela3SesionAnterior) continue;
                    int x; try { x = cont.GetXByBar(b, false); } catch { continue; }
                    if (VerEstelaEf && est.TryGetValue(b, out var e))
                    {
                        int bb = b, xx = x;   // 3.7.1: copias para las marcas encoladas (el 'for' comparte su variable entre vueltas)
                        for (int i = 0; i < 3 && i < e.Length; i++)
                        {
                            if (i == 2 && !VerEstelaZeroEf) continue;   // e[2] = zero gamma de esa vela (3.1.2)
                            if ((i == 0 && !Raya3NqD1) || (i == 1 && !Raya3NqD2) || (i == 2 && !Raya3NqZeroB)) continue;   // 3.5.0: una por una (3.7.0 D4: el cruce mas cercano apagado por defecto)
                            double p = e[i]; if (double.IsNaN(p) || p <= 0) continue;
                            if (!EnRadioBar(b, p)) continue;   // 3.7.0 D1: radio contra el cierre de SU vela; sin atenuacion por el precio de ahora
                            int y = Y(p); bool vis = EnPantalla(y);   // 3.7.2: se encola igual (la regla de la vela cuenta lo que esta fuera de pantalla)
                            int alfa = b >= ultima - 30 ? 255 : 153;
                            if (previa) alfa = alfa * 45 / 100;
                            // 3.7.1 (A5): se ENCOLA; PintarMarcasAgrupadas pinta una sola marca por (libro, strike) en esta vela
                            if (i == 2 && Zero3Estilo == ZeroEstilo3.RomboVerde)
                            {
                                int rz = Math.Max(2, Math.Min(4, wGuion / 2 + 1));
                                int a0 = alfa;
                                EncolarMarca(bb, raiz, "cruce", p, vis, () => g.FillPolygon(Color.FromArgb(AlfaAtravesada(a0, p, bb), ColApoyo), new[] { new Point(xx, y - rz), new Point(xx + rz, y), new Point(xx, y + rz), new Point(xx - rz, y) }));
                                continue;
                            }
                            if (i == 2) alfa = alfa * 80 / 100;
                            int a1 = alfa, w0 = wGuion; bool z = i == 2;
                            EncolarMarca(bb, raiz, i == 0 ? "D1" : i == 1 ? "D2" : "cruce", p, vis, () => g.FillRectangle(Color.FromArgb(AlfaAtravesada(a1, p, bb), z ? ColZero : ColDom), new Rectangle(xx - w0 / 2, y - 1, w0, 2)));
                        }
                    }
                    if (VerToquesEf && toq.TryGetValue(b, out var lt))
                    {
                        foreach (var t in lt)
                        {
                            int ya = Y(t.Nivel + tick), yb = Y(t.Nivel - tick);
                            if (ya == int.MinValue || yb == int.MinValue) continue;
                            int yt = Math.Min(ya, yb), h = Math.Max(2, Math.Abs(yb - ya));
                            if (yt + h < area.Top || yt > piso) continue;
                            g.FillRectangle(previa ? Color.FromArgb(110, ColDom) : ColDom, new Rectangle(x - bw / 2, yt, bw, h));
                        }
                    }
                }
            }

            // ---- capas QQQ / NDX (06-10): su estela en su color y sus rotulos; la fusion con D1/D2 de NQ va al rotulo de NQ
            int XBar(int b) { try { return cont.GetXByBar(b, false); } catch { return int.MinValue; } }
            var extra = PintarCapas(g, XBar, area, xr, piso, desde, hasta, horas, fut, radioDib, es, Y, EnPantalla, EnRadioBar);
            PintarApoyo(g, XBar, area, xr, piso, desde, hasta, horas, fut, radioDib, es, Y, EnPantalla, extra, EnRadioBar);   // 3.2.3: rombos del apoyo + zero por OI
            PintarFormulas(g, XBar, desde, hasta, horas, fut, L, Y, EnPantalla, extra, EnRadioBar);   // 3.6.1: F1..F8 a la vez, para juzgarlas
            PintarMajorOi(g, XBar, desde, hasta, horas, fut, L, Y, EnPantalla, extra, EnRadioBar);   // 3.6.1: majors por OI como dominantes
            // 3.7.2: la vista de los rotulos de la vela viva se arma ANTES de pintar las marcas, con la MISMA regla de la vela (Dibujo37.ElegirVela
            // dentro de VistaRotulos); en la vela viva se pinta solo lo que tiene rotulo. En las cerradas, la regla contra el cierre de cada una.
            List<Rot37> candViva = null; Dibujo37.Vista37 vistaViva = null;
            try
            {
                candViva = Candidatos(raiz, L, fut, majors, extra);
                var neutros = candViva.Select((q, i) => { var n = q.Neutro(); n.Id = i; return n; }).ToList();
                vistaViva = Dibujo37.VistaRotulos(neutros, fut, RadioDibujo(raiz), TopeRotulos37, InfoCapasRotulo(), Juntas37, TopeVela37);   // 3.7.3: tope de rotulos y de rayas, cada uno el suyo
            }
            catch (Exception e) { Registro.Excepcion(LOG, "VistaRotulos", e); }
            PintarMarcasAgrupadas(b => b >= ultimaBar ? fut : (cierreBar.TryGetValue(b, out var cc) ? cc : fut), ultimaBar, vistaViva == null ? null : Dibujo37.ClavesRotuladas(vistaViva));   // 3.7.2
            // ---- rotulos: una columna pegada al borde derecho del lienzo (el eje nativo queda fuera del recorte)
            if (VerRotulosEf && candViva != null && vistaViva != null) Rotulos(g, f, es, area, xr, piso, raiz, L, fut, tick, majors, Y, candViva, vistaViva, xUlt);
            if (!RecuadroLibro3Ver) _rectRecuadroCab = Rectangle.Empty;
            if (RecuadroLibro3Ver) { try { PintarRecuadro(g, f, es, area, xr, piso, raiz, L, fut, tick, majors, extra, cuenta); } catch (Exception e) { Registro.Excepcion(LOG, "Recuadro", e); } }   // 3.5.7
        }

        /// <summary>Precio redondeado al tick, con separador de miles: 31.545 / 6.745,25.</summary>
        private static string Precio(double p, double tick, CultureInfo es) => Dibujo37.Precio(p, tick, es);   // 3.7.0: sin ATAS (Simulador37)

        /// <summary>3.5.7: los niveles a rotular (los mismos que usa el recuadro del libro), en orden de jerarquia. 3.7.0: cada uno con su LIBRO y
        /// su TIPO (rotulo del borde, D1); el cruce mas cercano se llama 'cruce' (C8); los M± OI de NQ solo con el OI de ayer (C1) y van
        /// siempre al borde si quedan lejos (D4).</summary>
        private List<Rot37> Candidatos(string raiz, GammaHoyNucleo.Lectura L, double fut, bool majors, ExtraRotulos extra)
        {
            // en orden de jerarquia; el tope corta desde abajo. Chico = rotulo de capa (letra mas chica, pedido 06-10); Sufijo = la edad del dato
            // 3.7.0: la lista (orden, condiciones, libro/tipo, 'Siempre') sale de Dibujo37.CandidatosNq (sin ATAS, la misma que corre Simulador37);
            // aca solo se pone el color. D1, D2, cruce (3.2.5; C8: no es el zero gamma), M+/M− vol (3.5.0), M± OI solo con el OI de ayer (C1).
            var cand = Dibujo37.CandidatosNq(raiz, L, fut, Raya3NqD1, Raya3NqD2, Raya3NqZeroB, majors, Raya3NqMasVolB, Raya3NqMenosVolB, _oiNqFresco, Raya3NqMasOi, Raya3NqMenosOi)
                               .Select(r => new Rot37(r, r.Clase == "D" ? ColDom : r.Clase == "cruce" ? ColZero : r.Clase == "M+" ? (r.Tipo == "M+ OI" ? ColMasOi : ColPos) : (r.Tipo == "M− OI" ? ColMenosOi : ColNeg))).ToList();   // 3.7.3: M± OI con sus colores
            // fusion: un nivel de capa que coincide con D1/D2 de NQ le suma su sigla al rotulo (NQ·QQQ D1), no se dibuja aparte
            if (!Rayas3Independientes && extra != null && extra.Fusion.Count > 0)   // 3.5.0: independientes = sin fusion
            {
                double fus = (double)Math.Max(0.25m, Capa3FusionPts);
                for (int i = 0; i < cand.Count; i++)
                {
                    if (!cand[i].Nombre.Contains(" D")) continue;
                    var siglas = extra.Fusion.Where(fz => Math.Abs(fz.P - cand[i].P) <= fus).Select(fz => fz.Sigla).Distinct().ToList();
                    if (siglas.Count > 0) { var q = cand[i]; q.Nombre = q.Nombre.Replace(raiz + " ", raiz + "·" + string.Join("·", siglas) + " "); q.Sufijo = ""; cand[i] = q; }
                }
            }
            // 3.7.1 (A5): el tope ya no se aplica aca (con 'Todas independientes' era 99 y las capas y formulas no lo comian): lo aplica
            // Dibujo37.VistaRotulos DESPUES de agrupar por (libro, strike), a todos los rotulos juntos y por jerarquia.
            if (extra != null) cand.AddRange(extra.Rotulos);
            return cand;
        }

        private string TextoRotulo(Rot37 q, double fut, double tick, CultureInfo es, bool minimal)
        {
            if (!minimal && Rotulos3Cortos) return Dibujo37.TextoCorto(q.Neutro(), tick, es);   // 3.7.0: el texto de Columna sale de Dibujo37 (sin ATAS)
            string t = minimal ? RotuloMinimal(q.Nombre, q.Sufijo) : q.Nombre + " " + Precio(q.P, tick, es) + " " + (q.P - fut).ToString("+0;-0", es) + q.Sufijo;   // 3.5.5
            return string.IsNullOrEmpty(q.Extra) ? t : t + " ·" + q.Extra;   // 3.7.0: fecha del OI / alarma de base
        }

        private void Rotulos(RenderContext g, RenderFont f, CultureInfo es, Rectangle area, int xr, int piso, string raiz,
                             GammaHoyNucleo.Lectura L, double fut, double tick, bool majors, Func<double, int> Y, List<Rot37> cand, Dibujo37.Vista37 vista, int xUlt = int.MinValue)
        {
            // 3.7.2: la lista (Candidatos, la misma que alimenta el recuadro) y la vista (Dibujo37.VistaRotulos) vienen armadas de Pintar
            if (cand == null || cand.Count == 0 || vista == null) return;

            float tamC = Math.Max(5f, f.Size * Math.Max(50, Math.Min(100, Capa3LetraPct)) / 100f);
            var fC = new RenderFont("Consolas", tamC);
            f = new RenderFont("Consolas", Math.Max(5f, f.Size * Math.Max(50, Math.Min(120, Rotulos3TamPct)) / 100f));   // 3.5.5: rotulos mas chicos
            fC = new RenderFont("Consolas", Math.Max(5f, tamC * Math.Max(50, Math.Min(120, Rotulos3TamPct)) / 100f));
            int hf = g.MeasureString("X", f).Height + 2, hfC = g.MeasureString("X", fC).Height + 2;
            bool minimal = Rotulos3Estilo == EstiloRotulos3.Minimal;   // 3.5.7: maqueta 1
            // 3.7.2: agrupado por (libro, strike) en UN rotulo, la regla de la vela (radio, juntas, tope por jerarquia), las flechas del borde en
            // los lugares que sobran y la edad / fecha del OI en cada rotulo: todo en Dibujo37.VistaRotulos (sin ATAS; Simulador37 llama lo mismo)
            var filas = new List<(string Txt, Color C, int YExacto, bool EnPant, bool Arriba, bool Chico)>();
            foreach (var gr in vista.Dentro)
            {
                var q = cand[gr.Primero.Id];
                string txt = Dibujo37.TextoGrupo(gr, tick, es) + (!minimal && !Rotulos3Cortos ? " " + (gr.P - fut).ToString("+0;-0", es) : "");
                int y = Y(gr.P);
                bool enPant = y != int.MinValue && y >= area.Top + 2 && y <= piso - 2;
                filas.Add((txt, q.C, y, enPant, y != int.MinValue && y < area.Top + 2, q.Chico));
            }
            RenderFont FDe(bool chico) => chico ? fC : f;
            int HDe(bool chico) => chico ? hfC : hf;
            // el ancho se mide CON el prefijo de flecha y con la letra de cada fila
            int wMax = filas.Count > 0 ? filas.Max(r => g.MeasureString("↑ " + r.Txt, FDe(r.Chico)).Width) + (minimal ? 4 : 16) : 0;
            foreach (var gr in vista.Borde) wMax = Math.Max(wMax, g.MeasureString(Dibujo37.TextoBordeGrupo(gr, fut, es), FDe(cand[gr.Primero.Id].Chico)).Width + (minimal ? 4 : 16));
            foreach (var cb in vista.Cabeceras) wMax = Math.Max(wMax, g.MeasureString(cb.Texto, fC).Width + (minimal ? 4 : 16));
            int xCol = xr - wMax - 2;
            if (minimal && xUlt != int.MinValue) xCol = Math.Min(xUlt + 12, xCol);   // 3.5.7: al lado de la ultima vela
            void Marca(RenderFont ff, int x, int yy, int w, int h, string txt, Color c)
            {
                if (!minimal) { Chip(g, ff, x, yy, w, h, txt, c); return; }
                g.DrawString(txt, ff, Color.FromArgb(210, ColFondo), x + 1, yy + 2);   // sombra para leer encima de las velas
                g.DrawString(txt, ff, Color.FromArgb(250, c), x, yy + 1);
            }
            if (xCol < area.Left + 40) xCol = area.Left + 40;

            int yTop = area.Top + 2, yBot = piso - 2;
            // 3.7.3: un renglon por libro (o libros con el mismo texto) con la edad del dato y la fecha del OI, ARRIBA de todos los numeros
            // (Dibujo37.Cabeceras: 'NDX · QQQ · dato de hace 14 h · OI 07-10', 'NQ · OI 07-10'); 3.7.2 los ponia en cada rotulo
            foreach (var cb in vista.Cabeceras)
            {
                var k = Capa(cb.Libro);
                Marca(fC, xCol, yTop, wMax, hfC, cb.Texto, k != null ? k.Color : ColTexto);
                yTop += hfC + 1;
            }
            // 3.7.0 D1: los lejanos de la vela viva, al borde con flecha, libro, tipos y distancia
            (yTop, yBot) = PintarBorde37(g, f, fC, es, xCol, wMax, yTop, yBot, vista.Borde, cand, fut, minimal);

            // dentro del radio pero fuera de pantalla (zoom): un rotulo con flecha por lado, el mas cercano al borde
            var fueraArr = filas.Where(r => !r.EnPant && r.Arriba).OrderByDescending(r => r.YExacto).ToList();
            var fueraAba = filas.Where(r => !r.EnPant && !r.Arriba && r.YExacto != int.MinValue).OrderBy(r => r.YExacto).ToList();
            foreach (var r in fueraArr.Take(1)) { Marca(FDe(r.Chico), xCol, yTop, wMax, HDe(r.Chico), "↑ " + r.Txt, r.C); yTop += HDe(r.Chico) + 1; }
            foreach (var r in fueraAba.Take(1)) { Marca(FDe(r.Chico), xCol, yBot - HDe(r.Chico), wMax, HDe(r.Chico), "↓ " + r.Txt, r.C); yBot -= HDe(r.Chico) + 1; }

            // en pantalla: escalera anti-solape con la altura de cada fila (se corren, y un pelito los une a su precio real)
            var en = filas.Where(r => r.EnPant).OrderBy(r => r.YExacto).ToList();
            int n = en.Count; if (n == 0) return;
            var y0 = new int[n]; var hh = new int[n];
            for (int i = 0; i < n; i++) { hh[i] = HDe(en[i].Chico); y0[i] = en[i].YExacto - hh[i] / 2; }
            int minY = yTop; for (int i = 0; i < n; i++) { if (y0[i] < minY) y0[i] = minY; minY = y0[i] + hh[i] + 1; }
            int maxY = yBot; for (int i = n - 1; i >= 0; i--) { if (y0[i] + hh[i] > maxY) y0[i] = maxY - hh[i]; maxY = y0[i] - 1; }
            for (int i = 0; i < n; i++)
            {
                if (y0[i] < area.Top) continue;
                Marca(FDe(en[i].Chico), xCol, y0[i], wMax, hh[i], en[i].Txt, en[i].C);
                int ymed = y0[i] + hh[i] / 2;
                if (Rotulos3Conectores)   // 3.5.6: las rayitas que unen el rotulo con su precio, apagadas por defecto (pedido 08-10)
                {
                    if (Math.Abs(ymed - en[i].YExacto) > 2) g.DrawLine(new RenderPen(Color.FromArgb(200, en[i].C), 1f), xCol - 10, en[i].YExacto, xCol, ymed);
                    else g.FillRectangle(Color.FromArgb(230, en[i].C), new Rectangle(xCol - 6, en[i].YExacto - 1, 6, 2));
                }
            }
        }
        private static void Chip(RenderContext g, RenderFont f, int x, int y, int w, int h, string txt, Color c)
        {
            g.FillRectangle(Color.FromArgb(205, ColFondo), new Rectangle(x, y, w, h));
            g.FillRectangle(Color.FromArgb(240, c), new Rectangle(x, y, 3, h));
            g.DrawString(txt, f, Color.FromArgb(245, ColTexto), x + 7, y + 1);
        }

        private static void Cartel(RenderContext g, RenderFont f, Rectangle area, string txt, Color col, int yExtra = 0)
        {
            var m = g.MeasureString(txt, f);
            int x = area.Left + 8, y = area.Top + 26 + yExtra;
            g.FillRectangle(Color.FromArgb(225, ColFondo), new Rectangle(x, y, m.Width + 12, m.Height + 6));
            g.DrawString(txt, f, col, x + 6, y + 3);
        }

        /// <summary>UN renglon arriba a la izquierda, debajo del OHLC nativo: "PythiaGex 3.0 · NQ vivo (API) · 86 strikes · dato 0:04 ·
        /// 0Γ 31.500 · D1 31.545 +12 · D2 31.500 -33 · regla DosMasGrandes". La edad del dato va ANTES de los numeros y en
        /// naranja si pasa de 30 min. Debajo, si hace falta, un cartel de una linea con color: LIBRO VIVO CAIDO / SIN CADENA.</summary>
        private void Cabecera(RenderContext g, RenderFont f, Rectangle area, string raiz, GammaHoyNucleo.Lectura L, double fut, int strikes, CadenaApi.Estado foto, DateTime cuenta, bool renglon)
        {
            var es = CultureInfo.GetCultureInfo("es-AR");
            double tick = 0.25; try { tick = (double)(InstrumentInfo?.TickSize ?? 0.25m); if (tick <= 0) tick = 0.25; } catch { }
            double edadS = foto.EdadSegundos;   // segundos desde el ultimo Summary que MANDO la API (NaN hasta el primero)
            bool vieja = !double.IsNaN(edadS) && edadS > 30 * 60;
            string dato = double.IsNaN(edadS) ? "dato --" : edadS < 3600 ? "dato " + ((int)(edadS / 60)).ToString("0", es) + ":" + ((int)(edadS % 60)).ToString("00", es) : "dato " + Hace(edadS);
            int h = 0;
            if (renglon)
            {
                string pre = "PythiaGex 3.0 · " + raiz + " vivo (API) · " + strikes + " strikes · ";
                string post;
                if (L == null) post = " · sin cuenta";
                else
                {
                    string Niv(string n, double p) => double.IsNaN(p) || p <= 0 ? "" : " · " + n + " " + Precio(p, tick, es) + (n == "cruce" ? "" : " " + (p - fut).ToString("+0;-0", es));
                    // 3.7.0 C8: el cruce mas cercano se llama 'cruce' (no 0Γ) y solo si su raya esta prendida (D4); C1: el OI de NQ con fecha
                    post = (Raya3NqZeroB ? Niv("cruce", L.ZeroVol) + (L.ZeroCrucesVol > 1 && !double.IsNaN(L.ZeroVol) && L.ZeroVol > 0 ? " ·" + L.ZeroCrucesVol + " cruces" : "") : "") + (L.Doms.Count > 0 ? Niv("D1", L.Doms[0].Fut) : "") + (L.Doms.Count > 1 ? Niv("D2", L.Doms[1].Fut) : "")
                         + " · regla " + ReglaTexto() + (L.LibroDom == "OI" ? " · dominantes por OI" : "") + " · " + OiNqTexto + " · x" + L.Multiplicador.ToString("0", es) + " USD/pt";
                }
                int x = area.Left + 8, y = area.Top + 26;
                var m1 = g.MeasureString(pre, f); var m2 = g.MeasureString(dato, f); var m3 = g.MeasureString(post, f);
                int w = m1.Width + m2.Width + m3.Width + 12; h = Math.Max(m1.Height, m2.Height) + 6;
                g.FillRectangle(Color.FromArgb(200, ColFondo), new Rectangle(x, y, w, h));
                g.DrawString(pre, f, Color.FromArgb(215, ColTexto), x + 6, y + 3);
                g.DrawString(dato, f, vieja ? ColNaranja : Color.FromArgb(215, ColTexto), x + 6 + m1.Width, y + 3);
                g.DrawString(post, f, Color.FromArgb(215, ColTexto), x + 6 + m1.Width + m2.Width, y + 3);
            }

            // cartel de estado, de una linea y con color (se dibuja aunque la cabecera este en No: es lo que explica el vacio)
            string cartel = null; Color col = ColNaranja;
            if (L == null)
            {
                cartel = "SIN CADENA" + (string.IsNullOrEmpty(foto.Texto) ? "" : " · " + Recortar(foto.Texto, 90)); col = ColRojo;
                if (foto.Suscritos > 0 && strikes < MIN_STRIKES_UTILES) cartel = "SIN CADENA · " + foto.Suscritos + " contratos suscritos, " + foto.ConPuntas + " con puntas, " + strikes + " strikes con IV (minimo " + MIN_STRIKES_UTILES + ")";
            }
            else if (!double.IsNaN(edadS) && edadS > 5 * 60)
            {
                // sin Summary hace mas de 5 min: con el mercado abierto (reloj de NY) es la API o Rithmic que no mandan; con el
                // mercado cerrado (sabado, domingo antes de las 18:00, la pausa 17:00-18:00) es que no hay cotizacion, y no es una
                // caida. Los feriados de CME no los conoce el reloj: ese dia el cartel va a decir CAIDO.
                var ny = RelojNy.Ahora();
                if (RelojNy.MercadoAbierto(ny)) cartel = "LIBRO VIVO CAIDO hace " + Hace(edadS) + " · dibujo la ultima cuenta";
                else { cartel = "MERCADO CERRADO (NY " + ny.ToString("ddd HH:mm", es) + ") · ultimo dato hace " + Hace(edadS) + " · dibujo la ultima cuenta"; col = Color.FromArgb(215, ColTexto); }
            }
            else if (!_cadena.Activa) cartel = "LIBRO VIVO CAIDO · " + Recortar(foto.Texto, 90);
            else if (cuenta != DateTime.MinValue && (DateTime.UtcNow - cuenta).TotalMinutes > 3) cartel = "SIN CUENTA hace " + ((int)(DateTime.UtcNow - cuenta).TotalMinutes) + " min (" + strikes + " strikes con IV)";
            if (cartel != null) Cartel(g, f, area, cartel, col, renglon ? h + 4 : 0);
        }

        /// <summary>"7 min" / "3 h 20 min" / "2 d 5 h", para la edad del dato.</summary>
        private static string Hace(double segundos)
        {
            if (double.IsNaN(segundos) || segundos < 0) return "--";
            int min = (int)(segundos / 60);
            if (min < 90) return min + " min";
            if (min < 48 * 60) return (min / 60) + " h " + (min % 60).ToString("00") + " min";
            return (min / 1440) + " d " + (min % 1440 / 60) + " h";
        }

        private static string Recortar(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "…");

        private static void Log(string msg) => Registro.Linea(LOG, msg);
    }
}
