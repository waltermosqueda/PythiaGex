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

namespace PythiaGexCuatro
{
    /// <summary>Como se eligen las dos dominantes. Default DosMasGrandes (lo medido en la referencia, 2.0.5).</summary>
    public enum ReglaDominantes3
    {
        [Description("Clasica: una por lado + empate 20 % + centroide 12 pts (Gamma Hoy 1.11d)")] Clasica,
        [Description("Dos mas grandes, strike exacto (2.0.5, como la referencia)")] DosMasGrandes,
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
    /// PythiaGex 4.0 - Gamma Familia (4.1.0, 08-10-2026): UN solo indicador, todo calculado adentro (pedido del operador). Es el motor de la
    /// 3.0 (3.6.9, copiado por herramientas/clonar_4_0.py y terminado a mano por B1: ver "4.1" mas abajo) + la Familia (las 29 series de la
    /// vista previa) + la descarga propia de CBOE + la pantalla de la 4.0.6. Los puntos de union llevan la marca GANCHO-4.1.
    ///
    /// 4.1 (B1, 08-10): clase renombrada a FamiliaCuatro (el .ws del operador guarda "PythiaGexCuatro.FamiliaCuatro, Version=4.0.0.0": con
    /// AssemblyVersion fija en 4.0.0.0 el grafico del visor carga la 4.1 sin volver a agregarla; SIN VERIFICAR en ATAS). Lo que DIBUJA la 3.0
    /// queda apagado por defecto con casillas NUEVAS (Tres41*: nunca existieron en un .ws) pero se CALCULA siempre (TRES_* sale de aca).
    /// Todo lo que escribe va a %APPDATA%\ATAS\PythiaGex4 y a logs pythiagex4-*: la 3.0 y la 4.0 corren a la vez sin pisarse archivos.
    /// Cero dependencias externas: las capas NDX/QQQ leen la cadena que baja la propia 4.0 (PythiaGex4\cboe), sin cboe-local, sin la nube y
    /// sin la base de la clasica. La cinta del MNQ y el libro de NQ pasan a la Familia por AdaptadoresFamilia.cs (ICinta / ILibroNq).
    ///
    /// Lo que sigue es la descripcion de la 3.0 (el motor no cambio):
    ///
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
    [DisplayName("PythiaGex 4.0 - Gamma Familia")]
    [Category("PythiaGex 4.0")]
    public partial class FamiliaCuatro : Indicator
    {
        private const string LOG = "gammahoy";
        private const string VERSION = "4.1.6e";
        private const string MOTOR_TRES = "3.6.9";    // la version de la 3.0 que corre adentro (clonar_4_0.py)
        private const int MIN_STRIKES_UTILES = 8;     // strikes con IV en call y put para que la cuenta valga

        // colores fijos del pliego (5.3)
        private static readonly Color ColDom = Color.FromArgb(232, 197, 71);     // #E8C547
        private static readonly Color ColZero = Color.FromArgb(200, 200, 200);   // #C8C8C8
        private static readonly Color ColPos = Color.FromArgb(8, 153, 129);      // #089981
        private static readonly Color ColNeg = Color.FromArgb(242, 54, 69);      // #f23645
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

        [Display(Name = "Raiz manual (vacio = del grafico)", GroupName = "9.1 3.0 · Libro", Order = 1010,
                 Description = "NQ o ES. Vacio: se deduce del instrumento del grafico (MNQ -> NQ, MES -> ES, M2K -> RTY).")]
        public string Raiz3Manual { get; set; } = "";

        [Display(Name = "Tope de contratos suscritos", GroupName = "9.1 3.0 · Libro", Order = 1020,
                 Description = "Suscripciones vivas por instancia por la API. El proveedor de ATAS corta en 512; 320 por defecto.")]
        [Range(20, 480)]
        public int Tope3Contratos { get; set; } = 320;

        [Display(Name = "Ventana densa (%)", GroupName = "9.1 3.0 · Libro", Order = 1030, Description = "Todos los strikes hasta este % del precio.")]
        public decimal Ventana3DensaPct { get; set; } = 1.0m;

        [Display(Name = "Ventana rala (%)", GroupName = "9.1 3.0 · Libro", Order = 1040, Description = "Uno de cada dos strikes hasta este % del precio.")]
        public decimal Ventana3RalaPct { get; set; } = 2.0m;

        [Display(Name = "Vencimientos (fechas)", GroupName = "9.1 3.0 · Libro", Order = 1050, Description = "Las fechas mas cercanas (0DTE y 1DTE). El viernes de la trimestral una fecha trae dos series.")]
        [Range(1, 4)]
        public int Vencimientos3 { get; set; } = 2;

        [Display(Name = "Recentrar al alejarse (%)", GroupName = "9.1 3.0 · Libro", Order = 1060, Description = "Rearma la ventana cuando el precio se fue mas que esto del centro (minimo 60 s entre rearmes).")]
        public decimal Recentrar3Pct { get; set; } = 0.5m;

        [Display(Name = "Regla de dominantes", GroupName = "9.2 3.0 · Lectura", Order = 1010,
                 Description = "Clasica = Gamma Hoy 1.11d (una por lado, empate 20 %, centroide 12 pts). DosMasGrandes = 2.0.5 (las dos barras mas largas en valor absoluto, strike exacto). Tres = la clasica con histeresis (F3 V22 del laboratorio, 06-10): cambia de raya 2,8 veces por hora contra 10 de la clasica y el precio la perfora 12,9 pts contra 22,7; ventaja sobre el placebo +7,1 pp con z 1,47 en confirmacion: SIN VALIDAR.")]
        public ReglaDominantes3 Regla3Dominantes { get; set; } = ReglaDominantes3.Tres;

        [Display(Name = "Horizonte de vencimientos", GroupName = "9.2 3.0 · Lectura", Order = 1020, Description = "Hoy = el 0DTE (el mas cercano). Es lo que se contrasta contra la clasica y la 2.0.")]
        public GammaHoyNucleo.HorizonteVenc Horizonte3 { get; set; } = GammaHoyNucleo.HorizonteVenc.Hoy;

        [Display(Name = "Dominantes: radio maximo en puntos (0 = auto: 100 NQ / 25 ES)", GroupName = "9.2 3.0 · Lectura", Order = 1030,
                 Description = "La dominante de cada lado se busca solo entre los strikes a menos de esta distancia del precio. 0 = 100 pts en NQ, 25 en ES, 20 en RTY.")]
        [Range(0, 2000)]
        public decimal Radio3DominantesPts { get; set; } = 0m;

        [Display(Name = "Empate tecnico: gana la mas cercana (%)", GroupName = "9.2 3.0 · Lectura", Order = 1040, Description = "Solo con la regla Clasica. 20 = como la 1.11d.")]
        [Range(0, 90)]
        public int Empate3Pct { get; set; } = 20;

        [Display(Name = "Dominantes de noche", GroupName = "9.2 3.0 · Lectura", Order = 1050,
                 Description = "REGLA DEL OPERADOR (17-09): Volumen. Fuera de la rueda de NY las dominantes salen del volumen operado hoy, no del interes abierto. No cambiar sin aviso y captura antes/despues.")]
        public GammaHoyNucleo.NocheDominantes Noche3Dominantes { get; set; } = GammaHoyNucleo.NocheDominantes.Volumen;

        [Display(Name = "Zero interpolado por strike (como la referencia)", GroupName = "9.2 3.0 · Lectura", Order = 1060, Description = "Prendido: cambio de signo del perfil por strike, interpolado. Apagado: cruce repreciado en grilla.")]
        public bool Zero3Interpolado { get; set; } = true;

        [Display(Name = "↳ perfil visual", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 48, Description = "Necesita la llave '3.0 NQ dom▸' prendida. Solo decide dos cosas cuando estan en 'Segun el perfil': '↳ ver majors' (NQ: Limpio = no; ConMajors y Todo = si; ES: siempre si) y '↳ barras perfil' (solo con Todo). Las demas 'ver' en 'Segun el perfil' quedan en Si con cualquier perfil.")]
        public PerfilVisual3 Perfil3Visual { get; set; } = PerfilVisual3.Limpio;

        [Display(Name = "↳ ver estela", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 94, Description = "Necesita alguna de las llaves '3.0 NQ dom▸', '3.0 NQ 0Γ▸' o '3.0 capas▸' prendida. Vale para todas las que esten prendidas. La estela por vela (guiones o rombos) de D1/D2 de NQ, del 0Γ de NQ y de las capas. Segun el perfil = Si. No toca las F1-F8, los majors por OI ni el apoyo (tienen su estela propia).")]
        public Tri3 Ver3Estela { get; set; } = Tri3.SegunPerfil;

        [Display(Name = "↳ ver 0Γ raya", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 64, Description = "Necesita la llave '3.0 NQ 0Γ▸' prendida. Solo cambia algo con '↳ rayas largas' prendida (por defecto apagada). La raya punteada del zero de NQ que cruza el grafico (Segun el perfil = Si).")]
        public Tri3 Ver3Zero { get; set; } = Tri3.SegunPerfil;

        [Display(Name = "↳ ver 0Γ estela", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 63, Description = "Necesita la llave '3.0 NQ 0Γ▸' prendida. Tambien necesita '↳ NQ 0Γ' prendida y '↳ ver estela' en Si (el default). La estela del zero de NQ por vela cerrada: rombos verdes o guiones grises segun 'Zero gamma: estilo de la estela' (grupo 9.3). Segun el perfil = Si. 3.1.2 (pedido del operador): para ver donde estuvo el flip y si el precio reacciono ahi. Lo medido en el laboratorio: como nivel de rebote el zero rebota igual que su placebo y cambia 26-31 veces por hora; la caja negra lo graba como fam ZERO para medirlo con muestra nueva.")]
        public Tri3 Ver3EstelaZero { get; set; } = Tri3.SegunPerfil;

        [Display(Name = "Zero gamma: estilo de la estela", GroupName = "9.3 3.0 · Pantalla", Order = 1032,
                 Description = "3.2.4. RomboVerde = rombos por vela como los de la 2.0 (lo que el operador ve como zona; el zero por OI va mas chico y oscuro). GuionGris = guiones finos. Lo medido no cambia: el zero como nivel de rebote iguala a su placebo.")]
        public ZeroEstilo3 Zero3Estilo { get; set; } = ZeroEstilo3.RomboVerde;

        [Display(Name = "↳ ver tunel", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 66, Description = "Necesita la llave '3.0 tunel▸' prendida. Segun el perfil = Si. Con No la llave no dibuja nada.")]
        public Tri3 Ver3Tunel { get; set; } = Tri3.SegunPerfil;

        [Display(Name = "↳ toques", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 45, Description = "Necesita la llave '3.0 NQ dom▸' prendida. La marca de toque (un bloque de una vela de ancho) en la vela que llega a D1 o D2. Segun el perfil = Si.")]
        public Tri3 Ver3Toques { get; set; } = Tri3.SegunPerfil;

        [Display(Name = "↳ ver majors", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 49, Description = "Necesita la llave '3.0 NQ dom▸' prendida. Si los majors por volumen de NQ ('↳ NQ M+ vol' y '↳ NQ M− vol') se ven. Segun el perfil: no en NQ con Limpio (el default), si con ConMajors o Todo, y en ES siempre. El rotulo siempre; la raya ademas con '↳ rayas largas' y dentro del radio de dibujo. Tambien la usa el '3.0 libro▸'.")]
        public Tri3 Ver3Majors { get; set; } = Tri3.SegunPerfil;

        [Display(Name = "↳ ver rotulos", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 95, Description = "Necesita alguna de las llaves '3.0 NQ dom▸', '3.0 NQ 0Γ▸', '3.0 capas▸' o '3.0 F1-F8▸' prendida. Vale para todas las que esten prendidas. Los rotulos de la 3.0 (la columna de nombres al lado de las velas). Segun el perfil = Si.")]
        public Tri3 Ver3Rotulos { get; set; } = Tri3.SegunPerfil;

        [Display(Name = "↳ ver cabecera", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 93, Description = "Necesita la llave '3.0 cabecera▸' prendida. El renglon de la cabecera (Segun el perfil = Si). Con No queda solo el cartel de estado cuando hace falta.")]
        public Tri3 Ver3Cabecera { get; set; } = Tri3.SegunPerfil;

        [Display(Name = "↳ barras perfil", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 52, Description = "Necesita la llave '3.0 NQ dom▸' prendida. Las barras del perfil de GEX a la izquierda. Ninguna por defecto; Izquierda: ancho maximo 12 % del lienzo, alpha 50 %, sin pelotitas ni montos. Segun el perfil = solo con '↳ perfil visual' en Todo.")]
        public BarrasLado3 Barras3Lado { get; set; } = BarrasLado3.SegunPerfil;

        [Display(Name = "Radio de dibujo en puntos (0 = auto: 150 NQ / 40 ES)", GroupName = "9.3 3.0 · Pantalla", Order = 1090,
                 Description = "Mas lejos que esto: la estela se atenua al 30 %, el tunel no se dibuja y los majors quedan solo como rotulo.")]
        [Range(0, 5000)]
        public decimal Radio3DibujoPts { get; set; } = 0m;

        [Display(Name = "Zona de toque en puntos (0 = auto: 2,5 NQ / 0,75 ES)", GroupName = "9.3 3.0 · Pantalla", Order = 1100,
                 Description = "La primera vela que ENTRA a esta distancia de D1/D2 lleva la marca (la anterior no estaba en la zona). Default = la TOL del pre-registro del laboratorio (laboratorio/tres/PRE_REGISTRO.md: NQ 2,5 / ES 0,75), NO un valor medido: la penetracion p75 medida en F3 es otra cosa (NQ vivo ~20 pts de noche, zona sugerida ~45).")]
        public decimal Zona3ToquePts { get; set; } = 0m;

        [Display(Name = "Rearme del toque en puntos (0 = auto: 15 NQ / 4 ES)", GroupName = "9.3 3.0 · Pantalla", Order = 1105,
                 Description = "Un toque por dominante y por lado: despues de una marca no hay otra para ese lado hasta que una vela cierre a esta distancia del nivel (LEJOS del pre-registro: NQ 15, ES 4). Lo que el juez del laboratorio cuenta como 'toque'.")]
        [Range(0, 500)]
        public decimal Lejos3Pts { get; set; } = 0m;

        [Display(Name = "Tope de rotulos", GroupName = "9.3 3.0 · Pantalla", Order = 1110)]
        [Range(1, 7)]
        public int Tope3Rotulos { get; set; } = 5;

        [Display(Name = "Estela: velas hacia atras (0 = la sesion desde las 18:00 NY)", GroupName = "9.3 3.0 · Pantalla", Order = 1120)]
        [Range(0, 5000)]
        public int Estela3VelasAtras { get; set; } = 0;

        [Display(Name = "↳ sesion ant.", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 99, Description = "Necesita alguna de las llaves '3.0 NQ dom▸', '3.0 NQ 0Γ▸', '3.0 capas▸' o '3.0 F1-F8▸' prendida. Vale para todas las que esten prendidas. 3.0.5: lo de antes del inicio de sesion (18:00 NY) se dibuja al 45 % en vez de esconderse: estelas, toques, rombos, majors por OI y F1-F8.")]
        public bool Estela3SesionAnterior { get; set; } = true;

        [Display(Name = "Tunel: alpha (%)", GroupName = "9.3 3.0 · Pantalla", Order = 1130)]
        [Range(2, 60)]
        public int Tunel3AlphaPct { get; set; } = 12;

        [Display(Name = "Barras: ancho maximo (% del lienzo)", GroupName = "9.3 3.0 · Pantalla", Order = 1140)]
        [Range(3, 30)]
        public int Barras3AnchoPct { get; set; } = 12;

        [Display(Name = "Barras: alpha (%)", GroupName = "9.3 3.0 · Pantalla", Order = 1150)]
        [Range(10, 100)]
        public int Barras3AlphaPct { get; set; } = 50;

        [Display(Name = "Tamaño de letra", GroupName = "9.3 3.0 · Pantalla", Order = 1160)]
        [Range(6, 14)]
        public decimal Tam3Letra { get; set; } = 9m;

        [Display(Name = "Margen inferior (px)", GroupName = "9.3 3.0 · Pantalla", Order = 1170,
                 Description = "ATAS entrega un ChartArea mas alto que lo visible: lo anclado abajo descuenta esto (medido: 48).")]
        [Range(0, 200)]
        public int Margen3Inferior { get; set; } = 48;

        [Display(Name = "Guardar estela (jsonl)", GroupName = "9.4 3.0 · Archivo", Order = 1010,
                 Description = "%APPDATA%\\ATAS\\PythiaGex4\\estela\\estela-<raiz>-<dia UTC>.jsonl: una linea cuando cambian las dominantes o cada 60 s. Un solo escritor por raiz.")]
        public bool Guardar3Estela { get; set; } = true;

        [Display(Name = "Guardar centinela (una linea por vela cerrada)", GroupName = "9.4 3.0 · Archivo", Order = 1020,
                 Description = "%APPDATA%\\ATAS\\pythiagex4-centinela-hoy-<instrumento>-<marco>.jsonl, mismo formato que la 2.0. 4.1: apagado por defecto (nombre nuevo; la 3.0 sigue con el suyo).")]
        public bool Guardar4Centinela { get; set; } = false;

        [Display(Name = "Guardar viva3 (foto de la cadena por minuto)", GroupName = "9.4 3.0 · Archivo", Order = 1030,
                 Description = "%APPDATA%\\ATAS\\PythiaGex4\\viva\\viva3-<raiz>-<dia>.jsonl, mismo formato que la viva vieja. Un solo escritor por raiz (la sonda no duplica).")]
        public bool Guardar3Viva { get; set; } = true;

        [Display(Name = "Rearmar la cadena ahora", GroupName = "9.5 3.0 · Accion", Order = 1010, Description = "Cambialo a mano para forzar un rearme de la ventana de strikes.")]
        public bool Rearmar3Ahora
        {
            get => false;
            set { if (value) { try { Log("rearme pedido a mano"); _ = Task.Run(() => _cadena.Rearmar(OptionsDataProvider, TradingManager, DataProvider)); } catch (Exception e) { Registro.Excepcion(LOG, "Rearmar3Ahora", e); } } }
        }

        // ------------------------------------------------------------------
        // 4.1: lo que DIBUJA la 3.0, apagado por defecto. Casillas NUEVAS (nunca existieron en ningun .ws: el default vale). Con una
        // prendida, esa parte se dibuja EXACTAMENTE como en la 3.0 (deciden los ajustes de la 3.0 de los grupos 2-9). Con todas apagadas
        // OnRender no entra a Pintar. La CUENTA de la 3.0 (libro NQ, capas NDX/QQQ, regla Tres, estela) corre siempre: TRES_* sale de ahi.
        // ------------------------------------------------------------------

        [Display(Name = "3.0 NQ dom▸", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 42, Description = "Llave: sin ella no se dibuja ninguno de los renglones con ↳ que la siguen. Lo de NQ que dibuja la 3.0: la estela de D1/D2, las marcas de toque, los majors por volumen y por OI, las barras del perfil y sus rotulos; ADEMAS los majors por OI de NDX y QQQ, el apoyo y los cruces del zero (asi esta en el codigo). Apagada: no se dibuja nada de eso (se calcula igual). Lo comun a varias llaves (ver estela, ver rotulos, rayas largas, sesion anterior, atravesadas) esta al final del grupo.")]
        public bool Tres41Dominantes { get; set; } = false;

        [Display(Name = "3.0 tunel▸", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 65, Description = "Llave: sin ella no se dibuja ninguno de los renglones con ↳ que la siguen. La franja entre D1 y D2 de NQ de la 3.0, desde la ultima vela hasta el eje, si las dos estan dentro del radio de dibujo. 4.1.5d: se prende SOLA con esta llave (antes ademas hacia falta 'Tunel sombreado' de 9.7, que estaba apagado: no dibujaba nada).")]
        public bool Tres41Tunel { get; set; } = false;

        [Display(Name = "3.0 NQ 0Γ▸", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 61, Description = "Llave: sin ella no se dibuja ninguno de los renglones con ↳ que la siguen. El zero gamma de NQ de la 3.0: su estela por vela, su raya larga y su rotulo. Apagada: no se dibuja (se calcula igual). Lo comun a varias llaves esta al final del grupo.")]
        public bool Tres41Zero { get; set; } = false;

        [Display(Name = "3.0 capas▸", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 67, Description = "Llave: sin ella no se dibuja ninguno de los renglones con ↳ que la siguen. Las capas NDX y QQQ de la 3.0 (CBOE, 15 min tarde): D1-D3, el 0Γ con su techo/piso, la linea al eje y sus rotulos (con 'Capa NDX' y 'Capa QQQ' en Propia, grupo 9.3: el default). Se CALCULAN igual: TRES_NDX y TRES_QQQ de la Familia salen de ahi. Los majors por OI de NDX y QQQ NO van con esta llave: van con '3.0 NQ dom▸'. Lo comun a varias llaves esta al final del grupo.")]
        public bool Tres41Capas { get; set; } = false;

        [Display(Name = "3.0 libro▸", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 90, Description = "Llave: sin ella no se dibuja ninguno de los renglones con ↳ que la siguen. La pestañita 'LIBRO NQ ▸' de la 3.0 (el recuadro del libro: niveles cercanos con GEX por volumen y por OI, muros, majors y neto). Apagada tampoco toma el clic (la pestaña de la 4.0 va en la misma esquina).")]
        public bool Tres41Recuadro { get; set; } = false;

        [Display(Name = "3.0 cabecera▸", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 92, Description = "Llave: sin ella no se dibuja ninguno de los renglones con ↳ que la siguen. La cabecera de la 3.0: un renglon arriba a la izquierda con el estado del libro NQ y sus niveles, y el cartel de estado (LIBRO VIVO CAIDO, SIN CADENA, MERCADO CERRADO) cuando hace falta. La 4.0 tiene su propia pestaña con fuentes y edades.")]
        public bool Tres41Cabecera { get; set; } = false;

        [Display(Name = "3.0 F1-F8▸", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 81, Description = "Llave: sin ella no se dibuja ninguno de los renglones con ↳ que la siguen. Las formulas de dominantes de la 3.0 dibujadas a la vez para juzgarlas (F1..F8, cada una en su color, con su estela y un rotulo chico). Apagada: no se dibuja ninguna (se calculan igual). F7 son los majors por OI: '↳ NQ M+ OI' y siguientes, con '3.0 NQ dom▸'. Lo comun a varias llaves esta al final del grupo.")]
        public bool Tres41Formulas { get; set; } = false;

        /// <summary>4.1: hay algo de la 3.0 para dibujar.</summary>
        private bool Tres41AlgoVisible => Tres41Dominantes || Tres41Tunel || Tres41Zero || Tres41Capas || Tres41Recuadro || Tres41Cabecera || Tres41Formulas;

        // ------------------------------------------------------------------
        // ciclo de vida
        // ------------------------------------------------------------------

        /// <summary>Sin EnableCustomDrawing + SubscribeToDrawingEvents(Final) ATAS no llama a OnRender nunca.</summary>
        public FamiliaCuatro() : base(true)
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
                Log("PythiaGex 4.0 " + VERSION + " (motor 3.0 " + MOTOR_TRES + ") arranca. instrumento=" + (InstrumentInfo?.Instrument ?? "?") + " security=" + (TradingManager?.Security?.Code ?? "?")
                    + " raiz=" + (raiz == "" ? "(no soportada)" : raiz) + " regla=" + Regla3Dominantes + (Regla3Dominantes == ReglaDominantes3.Tres ? " (clasica + histeresis " + Histeresis3Pct + " %/" + Histeresis3Min + " min, centroide " + Histeresis3CentroidePts + " pts; SIN VALIDAR)" : "")
                    + " perfil=" + Perfil3Visual + " horizonte=" + Horizonte3 + " noche=" + Noche3Dominantes
                    + " radioDom=" + RadioDominantes(raiz).ToString("0.##", CultureInfo.InvariantCulture) + " radioDibujo=" + RadioDibujo(raiz).ToString("0.##", CultureInfo.InvariantCulture)
                    + " zona=" + ZonaToque(raiz).ToString("0.##", CultureInfo.InvariantCulture) + " lejos=" + Lejos(raiz).ToString("0.##", CultureInfo.InvariantCulture) + " tope=" + Tope3Contratos + " ventana=" + Ventana3DensaPct + "/" + Ventana3RalaPct + " % vencimientos=" + Vencimientos3
                    + " datos=" + Registro.CarpetaDatos
                    + " | 4.1: dibujo 3.0 " + (Tres41AlgoVisible ? "PRENDIDO (dom=" + Tres41Dominantes + " tunel=" + Tres41Tunel + " zero=" + Tres41Zero + " capas=" + Tres41Capas + " recuadro=" + Tres41Recuadro + " cabecera=" + Tres41Cabecera + " formulas=" + Tres41Formulas + ")" : "apagado")
                    + " cintaCsv=" + Cinta4Exportar + " relleno=" + Cinta4Rellenar + " caja=" + Caja4Activa + " centinela=" + Guardar4Centinela + " pulso=" + Estado4Pulso);
                _tick = Tick;
                SubscribeToTimer(_periodo, _tick);
                EstadoPulsoArrancar();   // 3.2.1: el pulso por cambio (1 s) para Profundidad 3.0; solo escribe indicador.json
                CintaArrancar();         // 3.4.0: la cinta en vivo (hilo escritor + reloj de 2 s); nunca tira
                CasillasArrancar();      // 4.1.5c: casillas de dibujo -> redibujo inmediato (reloj de 250 ms) y latencia al log
                try { Log(AjustesPantalla4Texto()); } catch { }   // 4.1 (B5): ajustes de la pantalla leidos del workspace
                // GANCHO-4.1 familia: aca el integrador registra esta instancia en el host de la Familia (FamiliaHost.Registrar(this), perezoso:
                // se concreta en el primer Tick con ChartInfo/InstrumentInfo, asi no arranca la instancia extra que ATAS crea al abrir los ajustes).
                // Los adaptadores ICinta/ILibroNq de B1 se enganchan solos en el primer Tick (AsegurarAdaptadoresFamilia).
                // GANCHO-4.1 descargador: IFuenteCboe.Arrancar() (singleton por proceso, hilo propio BelowNormal) tambien desde el primer Tick.
            }
            catch (Exception e) { Registro.Excepcion(LOG, "OnInitialize", e); }
        }

        protected override void OnDispose()
        {
            try { if (_tick != null) UnsubscribeFromTimer(_periodo, _tick); } catch { }
            EstadoPulsoParar();
            CasillasParar();         // 4.1.5c
            CintaParar();            // 3.4.0: vacia la cola, cierra el archivo y suelta la raiz
            try { _cadena.Parar(); } catch (Exception e) { Registro.Excepcion(LOG, "OnDispose", e); }
            try { _centinela?.Volcar(true); } catch { }
            // GANCHO-4.1 familia: FamiliaHost.Soltar(this) va aca (antes de soltar los adaptadores). GANCHO-4.1 descargador: IFuenteCboe.Parar()
            // solo cuando se suelta la ULTIMA instancia del proceso (contador de instancias del singleton).
            FamiliaSoltarHost();          // 4.1 (integracion): suelta el host (el ultimo dueño para el hilo y el descargador)
            SoltarAdaptadoresFamilia();   // 4.1 (B1): ICinta / ILibroNq (contador de dueños; el ultimo persiste y para su hilo)
            Log("PythiaGex 4.0 " + VERSION + " quitada del grafico");
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
        private double RadioDibujo(string raiz) => Radio3DibujoPts > 0 ? (double)Radio3DibujoPts : (raiz == "ES" ? 40.0 : raiz == "RTY" ? 30.0 : 150.0);
        private double ZonaToque(string raiz) => Zona3ToquePts > 0 ? (double)Zona3ToquePts : (raiz == "ES" ? 0.75 : raiz == "RTY" ? 0.6 : 2.5);
        private double Lejos(string raiz) => Lejos3Pts > 0 ? (double)Lejos3Pts : (raiz == "ES" ? 4.0 : raiz == "RTY" ? 3.0 : 15.0);
        private ReglaDominantes3 ReglaEfectiva() => Regla3Dominantes;

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
                AsegurarAdaptadoresFamilia(raiz);   // 4.1 (B1): ICinta (cinta del MNQ) e ILibroNq (libro NQ) para la Familia; idempotente
                FamiliaAsegurarHost(raiz);          // 4.1 (integracion): descargador CBOE + minuteros + TQQQ + motor en su hilo

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
                if (_cadena.Activa && fut > 0)
                {
                    try { c = DesdeApi(raiz, foto, out utiles); } catch (Exception e) { Registro.Excepcion(LOG, "DesdeApi", e); }
                }
                if (c != null)
                {
                    var L = _nucleo.Calcular(c, fut, ahora);
                    if (L != null && !L.SinBase)
                    {
                        AplicarRegla(L, ahora, false, LibroCompleto(foto, ahora));
                        AplicarTunel(L, fut, UltimoCierre(), raiz);   // 3.3.0: regla Tunel (techo y piso mas cercanos con fuerza, con histeresis)
                        if (ReglaEfectiva() != ReglaDominantes3.Tres && L.Doms.Count > 1) L.Doms = L.Doms.OrderBy(d => Math.Abs(d.Fut - fut)).ToList();   // 3.0.9: D1 = la mas cercana
                        ActualizarApoyo(c, L, fut, RadioDibujo(raiz));   // 3.2.3: libro profundo (contratos apoyados por strike)   // regla Tres: histeresis sobre la seleccion de la clasica (3.0.4; 3.0.7: sin decidir con el libro incompleto)
                        lock (_candado) { _L = L; _c = c; _futuro = fut; _strikesUtiles = utiles; _ultimaCuentaUtc = ahora; }
                        if ((ahora - _ultimoAudit).TotalSeconds >= 60)
                        {
                            _ultimoAudit = ahora;
                            Log("AUDIT3 regla=" + ReglaEfectiva() + (ReglaEfectiva() == ReglaDominantes3.Tres ? " hist[" + Hist.Resumen() + "]" : "") + " " + GammaHoyNucleo.Audit(L, c, _cadena.Activa).Substring(6)
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
                AlimentarVelasGraficoFamilia();   // 4.1 (B1): las velas CERRADAS del grafico van al ICinta como respaldo (solo donde no hay ticks)

                if ((ahora - _ultimoMinuto).TotalSeconds >= 60)
                {
                    _ultimoMinuto = ahora;
                    Log("minuto: via=" + foto.Via + " series=" + foto.Series + " suscritos=" + foto.Suscritos + "/" + foto.Contratos + " conPuntas=" + foto.ConPuntas + " conOI=" + foto.ConOI
                        + " rechazadas=" + foto.Rechazadas + " errores=" + foto.Errores + " vencidasFuera=" + foto.Vencidas + " cambios=" + _cadena.Cambios + " strikesUtiles=" + utiles
                        + " cuenta=" + (_L == null ? "no" : "si") + " edad=" + (double.IsNaN(foto.EdadSegundos) ? "-" : foto.EdadSegundos.ToString("0") + " s") + " | " + foto.Texto
                        + " | familia: " + EstadoAdaptadoresFamilia());
                    // 4.1 (B1): la foto viva3 del minuto va SIEMPRE al ILibroNq (si esta instancia tiene el turno de la raiz); el archivo
                    // PythiaGex4\viva solo si Guardar3Viva (es la siembra del ILibroNq al reiniciar: apagarlo deja a la Familia sin historia de NQ).
                    Viva3.Guardar(_cadena, raiz, this, LOG, Guardar3Viva);
                }
                // GANCHO-4.1 familia: el motor de la Familia corre en SU hilo ("PythiaGex4 familia", BelowNormal, cada 5 s o al llegar una foto):
                // aca, a lo sumo, el integrador le avisa al host que hay velas/fotos nuevas (AutoResetEvent.Set) — nada de calculo en este hilo.
                // Configurar(cinta: FamiliaCinta, nq: FamiliaLibroNq, cboe: IFuenteCboe, libros: [nq, ndx, qqq], tqqq, contrato: ContratoGrafico()).
                // GANCHO-4.1 TQQQ: ICalculoTqqq.Vela(aperturaM2, FamiliaCinta, cboe) lo llama el MOTOR (B3d), no este latido; el NQ_1600 del
                // anclaje de noche sale de FamiliaCinta.PrecioSoloTick(16:00 NY) o, sin ticks, de FamiliaCinta.VelasM2 (respaldo del grafico).
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
                // GANCHO-4.1 familia/pantalla: nada mas aca. Las pestañas ocultas no reciben OnCalculate (medido 15-09): las velas del grafico
                // van a la Familia desde Tick (AlimentarVelasGraficoFamilia) y los ticks desde CintaEvento.
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

                    RegistrarCapasVela(tVela);
                    RegistrarApoyoVela(tVela, L);   // 3.2.3
                    if (cuentaFresca) { RegistrarMajorOiVela(tVela, L); RegistrarFormulasVela(tVela, L, fut, cl); }   // 3.6.1
                    if (Guardar4Centinela)
                    {
                        if (_centinela == null)
                        {
                            var instr = InstrumentInfo != null ? InstrumentInfo.Instrument : "x";
                            var marco = ChartInfo != null && ChartInfo.ChartType != null ? ChartInfo.ChartType + "-" + ChartInfo.TimeFrame : "x";
                            _centinela = new Centinela("hoy-" + instr, marco);
                            Log("centinela: " + _centinela.Ruta);
                        }
                        var niv = cuentaFresca
                            ? GammaHoyNucleo.Niveles(L).Where(kv => kv.Key == "dom0" || kv.Key == "dom1" || kv.Key == "zero_vol" || kv.Key == "zero_oi" || kv.Key == "mp_vol" || kv.Key == "mn_vol").ToList()
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

        private void GuardarEstela(string raiz, DateTime horaUtc, GammaHoyNucleo.Lectura L)
        {
            try
            {
                var inv = CultureInfo.InvariantCulture;
                double d1 = L.Doms.Count > 0 ? L.Doms[0].Fut : double.NaN, d2 = L.Doms.Count > 1 ? L.Doms[1].Fut : double.NaN;
                string F(double x) => double.IsNaN(x) || x <= 0 ? "0" : x.ToString("0.00", inv);
                string d = F(d1) + "," + F(d2) + "," + F(L.ZeroVol);
                string m = F(L.MpOi) + "," + F(L.MnOi);   // 3.6.2: majors por OI, para la memoria
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
                string extra = ",\"g\":[" + fz + "],\"n\":" + (L.NetVol / 1e6).ToString("0", inv) + ",\"b\":\"" + (L.LibroDom ?? "vol") + "\",\"f\":" + L.Futuro.ToString("0.00", inv) + ",\"m\":[" + m + "]" + (L.Base != 0 ? ",\"bs\":" + L.Base.ToString("0.00", inv) : "");   // 3.6.6: base usada
                extra += CampoGmEstela(raiz, L);   // 4.1.2: el monto con signo de D1/D2 en unidades de la familia (al final: "g" y el resto no cambian)
                var ruta = Path.Combine(Registro.CarpetaDatos, "estela", "estela-" + raiz + "-" + horaUtc.ToString("yyyy-MM-dd", inv) + ".jsonl");
                string linea = "{\"t\":\"" + horaUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", inv) + "\",\"d\":[" + d + "]" + extra + "}";
                Registro.Anexar(ruta, linea);
                // GANCHO-4.1 familia (TRES_NQ / TRES_NDX / TRES_QQQ): la MISMA linea que va al archivo, despues del control de escritor unico
                // (solo postea el dueño de la raiz). raiz = "NQ" | "NDX" | "QQQ". La lee el motor como la vista previa: d[0..1] > 15000, vida
                // 300 s (NQ) / 1500 s (NDX, QQQ). Al reiniciar, el motor lee PythiaGex4\estela\estela-<raiz>-<dia>.jsonl (los mismos renglones).
                try { Familia.AdaptadoresFamilia.AlEstela?.Invoke(raiz, linea); } catch (Exception e) { Registro.Excepcion(LOG, "GuardarEstela.AlEstela", e); }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "GuardarEstela", e); }
        }

        /// <summary>4.1.2 (montos en las etiquetas): el campo nuevo ,"gm":[m1,m2] de la estela, alineado con d[0]/d[1]: el GEX NETO con signo del
        /// strike elegido para cada dominante / 1e6 en unidades de la FAMILIA (NQ x0,2: la 3.0 cuenta con multiplicador 100 y el NQ es 20;
        /// NDX/QQQ x1), con el libro que dice "b" (vol u OI). null donde no hay monto: d[i] es 0 (no hay dominante), el relleno +-1e6 del tunel
        /// por cruces (de noche con la regla Tres: los cruces de signo no son strikes), una vigente de la histeresis cuyo strike no esta en el
        /// perfil (Gex 0), cualquier valor que no sea exactamente el GexVol/GexOi de un strike del perfil, o una raiz sin unidad de familia
        /// (ES/RTY). La regla y las unidades viven en Familia.AlmacenTres (MontosDominantes3/CampoGm), el mismo archivo que lo lee; el arnes fam
        /// (atas/_test_cuatro_paridad/fam, modo gm) las corre con lecturas REALES del nucleo sobre las cadenas de CBOE.</summary>
        private static string CampoGmEstela(string raiz, GammaHoyNucleo.Lectura L)
        {
            try
            {
                var perfil = L.Perfil ?? new List<GammaHoyNucleo.Strike>();
                var gv = new double[perfil.Count]; var go = new double[perfil.Count];
                for (int i = 0; i < perfil.Count; i++) { gv[i] = perfil[i].GexVol; go[i] = perfil[i].GexOi; }
                var (m1, m2) = Familia.AlmacenTres.MontosDominantes3(raiz, L.Doms, gv, go);
                return Familia.AlmacenTres.CampoGm(m1, m2);
            }
            catch (Exception e) { Registro.Excepcion(LOG, "CampoGmEstela", e); return Familia.AlmacenTres.CampoGm(double.NaN, double.NaN); }
        }

        // ------------------------------------------------------------------
        // el dibujo
        // ------------------------------------------------------------------

        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            // 4.1: lo de la 3.0 solo si alguna casilla Tres41* esta prendida (por defecto ninguna: no se entra a Pintar)
            try { if (Tres41AlgoVisible) Pintar(g); }
            catch (Exception e) { Registro.Excepcion(LOG, "OnRender", e); }
            // 4.1 (integracion): el dibujo de la 4.0 (B5), SIEMPRE y DESPUES de lo de la 3.0: solo la FotoFamilia publicada (inmutable)
            try { _pantalla.Pintar(g, FamiliaFoto); } catch (Exception e) { Registro.Excepcion(LOG, "Pantalla", e); }
            CasillasRender();        // 4.1.5c: anota cuanto tardo en verse un cambio de casilla
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
                if (Tres41Cabecera) Cartel(g, f, area, "PythiaGex 4.0: el grafico es " + CodigoGrafico() + ", no NQ/ES/RTY: no hago nada", ColNaranja);
                return;
            }

            // copia del estado bajo llave
            GammaHoyNucleo.Lectura L; Feed.Cadena c; double fut; int strikes; DateTime cuenta;
            var est = new Dictionary<int, double[]>(); var toq = new Dictionary<int, List<(double Nivel, bool Arriba)>>();
            int desde = Math.Max(0, FirstVisibleBarNumber), hasta = Math.Min(CurrentBar - 1, LastVisibleBarNumber);
            // la estela esta indexada por hora de vela: se traducen las barras visibles a su hora (fuera de la llave)
            var horas = new List<(int B, DateTime T)>();
            for (int b = desde; b <= hasta && horas.Count < 6000; b++) { try { var cb = GetCandle(b); if (cb != null) horas.Add((b, Utc(cb.Time))); } catch { } }
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
            PrepararAtravesadas(desde, hasta);   // 3.6.4

            // ---- cabecera (si esta prendida) y cartel de estado (siempre que haga falta: que se sepa por que no hay cuenta)
            // 4.1: solo con Tres41Cabecera (la 4.0 tiene su propia pestaña con fuentes y edades; el aviso de la Familia va en FotoFamilia.Aviso)
            if (Tres41Cabecera) Cabecera(g, f, area, raiz, L, fut, strikes, foto, cuenta, VerCabeceraEf);

            // sin cuenta viva (arranque, pausa de Globex, libro caido) igual se dibuja lo que la memoria sembro: estela, toques y capas,
            // con el precio del grafico como referencia (3.0.4; antes el grafico quedaba vacio hasta la primera cuenta)
            bool sinCuenta = L == null;
            if (fut <= 0) { try { fut = PrecioGrafico(); } catch { fut = 0; } }
            if (fut <= 0) return;
            double radioDib = RadioDibujo(raiz);
            double tick = 0.25; try { tick = (double)(InstrumentInfo?.TickSize ?? 0.25m); if (tick <= 0) tick = 0.25; } catch { }
            int Y(double p) { try { return cont.GetYByPrice((decimal)p, false); } catch { return int.MinValue; } }
            bool EnPantalla(int y) => y != int.MinValue && y >= area.Top && y <= piso;
            double d1 = !sinCuenta && L.Doms.Count > 0 ? L.Doms[0].Fut : double.NaN, d2 = !sinCuenta && L.Doms.Count > 1 ? L.Doms[1].Fut : double.NaN;
            bool Cerca(double p) => !double.IsNaN(p) && p > 0 && Math.Abs(p - fut) <= radioDib;

            // ---- barras del perfil a la izquierda (opcional, apagadas por defecto)
            if (Tres41Dominantes && BarrasIzqEf && !sinCuenta && L.Perfil.Count > 0 && L.MaxAbsVol > 0)
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
            // 4.1.5d (revision 4.1.5c): sin "&& Tunel3Sombreado": la casilla de arriba "3.0 tunel D1-D2" prende el tunel sola (con el ajuste de la 3.0
            // apagado, como en el .ws del operador, prenderla no dibujaba nada aunque el log anotara "primer render con el cambio")
            if (Tres41Tunel && VerTunelEf && Cerca(d1) && Cerca(d2) && xUlt != int.MinValue)   // 3.5.5
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
            if (Tres41Zero && VerZeroEf && Rayas3Largas && Raya3NqZero && !sinCuenta && !double.IsNaN(L.ZeroVol) && L.ZeroVol > 0)   // 3.5.5
            {
                int y = Y(L.ZeroVol);
                if (EnPantalla(y))
                {
                    var pen = new RenderPen(Color.FromArgb(200, ColZero), 1f);
                    for (int x = area.Left; x < xr; x += 6) g.DrawLine(pen, x, y, Math.Min(x + 3, xr), y);
                }
            }

            // ---- majors: 1 px continuo, solo dentro del radio de dibujo (fuera: solo rotulo)
            bool majors = Tres41Dominantes && VerMajorsEf(raiz);   // 4.1: los majors van con las dominantes de la 3.0
            if (majors && !sinCuenta)
            {
                if (Rayas3Largas && Raya3NqMas && Cerca(L.MpVol)) { int y = Y(L.MpVol); if (EnPantalla(y)) g.DrawLine(new RenderPen(Color.FromArgb(200, ColPos), 1f), area.Left, y, xr, y); }
                if (Rayas3Largas && Raya3NqMenos && Cerca(L.MnVol)) { int y = Y(L.MnVol); if (EnPantalla(y)) g.DrawLine(new RenderPen(Color.FromArgb(200, ColNeg), 1f), area.Left, y, xr, y); }
            }

            // ---- estela de D1/D2 (un guion por vela cerrada) y marcas de toque
            if ((Tres41Dominantes || Tres41Zero) && (VerEstelaEf || VerToquesEf) && hasta >= desde)   // 4.1: D1/D2 y toques con Tres41Dominantes, el zero con Tres41Zero
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
                        for (int i = 0; i < 3 && i < e.Length; i++)
                        {
                            if (i == 2 && !VerEstelaZeroEf) continue;   // e[2] = zero gamma de esa vela (3.1.2)
                            if (i < 2 ? !Tres41Dominantes : !Tres41Zero) continue;   // 4.1
                            if ((i == 0 && !Raya3NqD1) || (i == 1 && !Raya3NqD2) || (i == 2 && !Raya3NqZero)) continue;   // 3.5.0: una por una
                            double p = e[i]; if (double.IsNaN(p) || p <= 0) continue;
                            int y = Y(p); if (!EnPantalla(y)) continue;
                            int alfa = Math.Abs(p - fut) > radioDib ? 77 : (b >= ultima - 30 ? 255 : 153);
                            if (previa) alfa = alfa * 45 / 100;
                            if (i == 2 && Zero3Estilo == ZeroEstilo3.RomboVerde)
                            {
                                int rz = Math.Max(2, Math.Min(4, wGuion / 2 + 1));
                                g.FillPolygon(Color.FromArgb(AlfaAtravesada(alfa, p, b), ColApoyo), new[] { new Point(x, y - rz), new Point(x + rz, y), new Point(x, y + rz), new Point(x - rz, y) });
                                continue;
                            }
                            if (i == 2) alfa = alfa * 80 / 100;
                            g.FillRectangle(Color.FromArgb(AlfaAtravesada(alfa, p, b), i == 2 ? ColZero : ColDom), new Rectangle(x - wGuion / 2, y - 1, wGuion, 2));
                        }
                    }
                    if (Tres41Dominantes && VerToquesEf && toq.TryGetValue(b, out var lt))
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
            // 4.1: cada parte con su casilla Tres41* (las capas se calculan igual: ActualizarCapas no mira estas casillas)
            var extra = Tres41Capas ? PintarCapas(g, XBar, area, xr, piso, desde, hasta, horas, fut, radioDib, es, Y, EnPantalla) : new ExtraRotulos();
            if (Tres41Dominantes) PintarApoyo(g, XBar, area, xr, piso, desde, hasta, horas, fut, radioDib, es, Y, EnPantalla, extra);   // 3.2.3: rombos del apoyo + zero por OI
            if (Tres41Formulas) PintarFormulas(g, XBar, desde, hasta, horas, fut, L, Y, EnPantalla, extra);   // 3.6.1: F1..F8 a la vez, para juzgarlas
            if (Tres41Dominantes) PintarMajorOi(g, XBar, desde, hasta, horas, fut, L, Y, EnPantalla, extra);   // 3.6.1: majors por OI como dominantes
            // ---- rotulos: una columna pegada al borde derecho del lienzo (el eje nativo queda fuera del recorte)
            if (VerRotulosEf && (Tres41Dominantes || Tres41Zero || Tres41Capas || Tres41Formulas)) Rotulos(g, f, es, area, xr, piso, raiz, L, fut, tick, majors, Y, extra, xUlt);
            bool recuadro = RecuadroLibro3Ver && Tres41Recuadro;   // 4.1
            if (!recuadro) _rectRecuadroCab = Rectangle.Empty;
            if (recuadro) { try { PintarRecuadro(g, f, es, area, xr, piso, raiz, L, fut, tick, VerMajorsEf(raiz), extra, cuenta); } catch (Exception e) { Registro.Excepcion(LOG, "Recuadro", e); } }   // 3.5.7
        }

        /// <summary>Precio redondeado al tick, con separador de miles: 31.545 / 6.745,25.</summary>
        private static string Precio(double p, double tick, CultureInfo es)
        {
            if (tick > 0) p = Math.Round(p / tick) * tick;
            return Math.Abs(p - Math.Round(p)) < 1e-9 || tick >= 1 ? p.ToString("N0", es) : p.ToString("N2", es);
        }

        /// <summary>3.5.7: los niveles a rotular (los mismos que usa el recuadro del libro), en orden de jerarquia.</summary>
        private List<(string Nombre, double P, Color C, bool Chico, string Sufijo)> Candidatos(string raiz, GammaHoyNucleo.Lectura L, double fut, bool majors, ExtraRotulos extra, bool todos = false)
        {
            // en orden de jerarquia; el tope corta desde abajo. Chico = rotulo de capa (letra mas chica, pedido 06-10); Sufijo = la edad del dato
            var cand = new List<(string Nombre, double P, Color C, bool Chico, string Sufijo)>();
            // 4.1: D1/D2 y majors OI con Tres41Dominantes, el zero con Tres41Zero (los majors por volumen ya vienen en 'majors'); todos = el recuadro (lista completa)
            if ((todos || Tres41Dominantes) && Raya3NqD1 && L != null && L.Doms.Count > 0 && L.Doms[0].Fut > 0) cand.Add((raiz + " D1 " + (L.Doms[0].Fut >= fut ? "▲" : "▼"), L.Doms[0].Fut, ColDom, false, ""));
            if ((todos || Tres41Dominantes) && Raya3NqD2 && L != null && L.Doms.Count > 1 && L.Doms[1].Fut > 0) cand.Add((raiz + " D2 " + (L.Doms[1].Fut >= fut ? "▲" : "▼"), L.Doms[1].Fut, ColDom, false, ""));
            if ((todos || Tres41Zero) && Raya3NqZero && L != null && !double.IsNaN(L.ZeroVol) && L.ZeroVol > 0) cand.Add((raiz + " 0Γ", L.ZeroVol, ColZero, false, L.ZeroCrucesVol > 1 ? " ·" + L.ZeroCrucesVol + " cruces" : ""));   // 3.2.5
            if (majors && L != null)
            {
                if (Raya3NqMas && !double.IsNaN(L.MpVol) && L.MpVol > 0) cand.Add((raiz + " M+ vol", L.MpVol, ColPos, false, ""));     // 3.5.0: nombre claro (antes "MP")
                if (Raya3NqMenos && !double.IsNaN(L.MnVol) && L.MnVol > 0) cand.Add((raiz + " M− vol", L.MnVol, ColNeg, false, ""));   // 3.5.0: (antes "MN"); 3.6.1: "vol" (pedido: identificar volumen y OI)
            }
            double rMaj = Math.Max(20, MajorOi3RadioMaxPts);   // 3.6.1: majors por interes abierto como dominantes (pedido 08-10 sobre 31.350)
            if ((todos || Tres41Dominantes) && Raya3NqMasOi && L != null && !double.IsNaN(L.MpOi) && L.MpOi > 0 && Math.Abs(L.MpOi - fut) <= rMaj) cand.Add((raiz + " M+ OI", L.MpOi, ColPos, false, ""));
            if ((todos || Tres41Dominantes) && Raya3NqMenosOi && L != null && !double.IsNaN(L.MnOi) && L.MnOi > 0 && Math.Abs(L.MnOi - fut) <= rMaj) cand.Add((raiz + " M− OI", L.MnOi, ColNeg, false, ""));
            // fusion: un nivel de capa que coincide con D1/D2 de NQ le suma su sigla al rotulo (NQ·QQQ D1), no se dibuja aparte
            if (!Rayas3Independientes && extra != null && extra.Fusion.Count > 0)   // 3.5.0: independientes = sin fusion
            {
                double fus = (double)Math.Max(0.25m, Capa3FusionPts);
                for (int i = 0; i < cand.Count; i++)
                {
                    if (!cand[i].Nombre.Contains(" D")) continue;
                    var siglas = extra.Fusion.Where(fz => Math.Abs(fz.P - cand[i].P) <= fus).Select(fz => fz.Sigla).Distinct().ToList();
                    if (siglas.Count > 0) cand[i] = (cand[i].Nombre.Replace(raiz + " ", raiz + "·" + string.Join("·", siglas) + " "), cand[i].P, cand[i].C, false, "");
                }
            }
            int tope = Rayas3Independientes ? 99 : Math.Max(1, Math.Min(7, Tope3Rotulos));   // 3.5.0
            if (cand.Count > tope) cand = cand.Take(tope).ToList();
            if (extra != null) foreach (var r in extra.Rotulos) cand.Add((r.Nombre, r.P, r.C, true, r.Sufijo));   // los de las capas no comen el tope de NQ
            return cand;
        }

        private void Rotulos(RenderContext g, RenderFont f, CultureInfo es, Rectangle area, int xr, int piso, string raiz,
                             GammaHoyNucleo.Lectura L, double fut, double tick, bool majors, Func<double, int> Y, ExtraRotulos extra = null, int xUlt = int.MinValue)
        {
            var cand = Candidatos(raiz, L, fut, majors, extra);   // 3.5.7: la misma lista alimenta el recuadro
            if (cand.Count == 0) return;

            float tamC = Math.Max(5f, f.Size * Math.Max(50, Math.Min(100, Capa3LetraPct)) / 100f);
            var fC = new RenderFont("Consolas", tamC);
            f = new RenderFont("Consolas", Math.Max(5f, f.Size * Math.Max(50, Math.Min(120, Rotulos3TamPct)) / 100f));   // 3.5.5: rotulos mas chicos
            fC = new RenderFont("Consolas", Math.Max(5f, tamC * Math.Max(50, Math.Min(120, Rotulos3TamPct)) / 100f));
            int hf = g.MeasureString("X", f).Height + 2, hfC = g.MeasureString("X", fC).Height + 2;
            bool minimal = Rotulos3Estilo == EstiloRotulos3.Minimal;   // 3.5.7: maqueta 1
            var filas = new List<(string Txt, Color C, int YExacto, bool EnPant, bool Arriba, bool Chico)>();
            foreach (var q in cand)
            {
                double dist = q.P - fut;
                string txt = minimal ? RotuloMinimal(q.Nombre, q.Sufijo) : Rotulos3Cortos ? RotuloCorto(q.Nombre, Precio(q.P, tick, es), q.Sufijo) : q.Nombre + " " + Precio(q.P, tick, es) + " " + dist.ToString("+0;-0", es) + q.Sufijo;   // 3.5.5
                int y = Y(q.P);
                bool enPant = y != int.MinValue && y >= area.Top + 2 && y <= piso - 2;
                filas.Add((txt, q.C, y, enPant, y != int.MinValue && y < area.Top + 2, q.Chico));
            }
            RenderFont FDe(bool chico) => chico ? fC : f;
            int HDe(bool chico) => chico ? hfC : hf;
            // el ancho se mide CON el prefijo de flecha y con la letra de cada fila
            int wMax = filas.Max(r => g.MeasureString("↑ " + r.Txt, FDe(r.Chico)).Width) + (minimal ? 4 : 16);
            int xCol = xr - wMax - 2;
            if (minimal && xUlt != int.MinValue) xCol = Math.Min(xUlt + 12, xCol);   // 3.5.7: al lado de la ultima vela
            void Marca(RenderFont ff, int x, int yy, int w, int h, string txt, Color c)
            {
                if (!minimal) { Chip(g, ff, x, yy, w, h, txt, c); return; }
                g.DrawString(txt, ff, Color.FromArgb(210, ColFondo), x + 1, yy + 2);   // sombra para leer encima de las velas
                g.DrawString(txt, ff, Color.FromArgb(250, c), x, yy + 1);
            }
            if (xCol < area.Left + 40) xCol = area.Left + 40;

            // fuera de pantalla: un rotulo con flecha por lado, el mas cercano al borde
            // fuera de pantalla: el mas cercano al borde por lado y, ademas, los majors por OI siempre (3.6.2, pedido 08-10)
            bool EsMajOi(string s) => s != null && s.Contains(" OI") && (s.Contains("M+") || s.Contains("M−"));
            var fueraArr = filas.Where(r => !r.EnPant && r.Arriba).OrderByDescending(r => r.YExacto).ToList();
            var fueraAba = filas.Where(r => !r.EnPant && !r.Arriba && r.YExacto != int.MinValue).OrderBy(r => r.YExacto).ToList();
            int yTop = area.Top + 2, yBot = piso - 2;
            foreach (var r in fueraArr.Take(1).Concat(fueraArr.Skip(1).Where(r0 => EsMajOi(r0.Txt)))) { Marca(FDe(r.Chico), xCol, yTop, wMax, HDe(r.Chico), "↑ " + r.Txt, r.C); yTop += HDe(r.Chico) + 1; }
            foreach (var r in fueraAba.Take(1).Concat(fueraAba.Skip(1).Where(r0 => EsMajOi(r0.Txt)))) { Marca(FDe(r.Chico), xCol, yBot - HDe(r.Chico), wMax, HDe(r.Chico), "↓ " + r.Txt, r.C); yBot -= HDe(r.Chico) + 1; }

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
                string pre = "PythiaGex 4.0 · " + raiz + " vivo (API) · " + strikes + " strikes · ";
                string post;
                if (L == null) post = " · sin cuenta";
                else
                {
                    string Niv(string n, double p) => double.IsNaN(p) || p <= 0 ? "" : " · " + n + " " + Precio(p, tick, es) + (n == "0Γ" ? "" : " " + (p - fut).ToString("+0;-0", es));
                    post = Niv("0Γ", L.ZeroVol) + (L.ZeroCrucesVol > 1 && !double.IsNaN(L.ZeroVol) && L.ZeroVol > 0 ? " ·" + L.ZeroCrucesVol + " cruces" : "") + (L.Doms.Count > 0 ? Niv("D1", L.Doms[0].Fut) : "") + (L.Doms.Count > 1 ? Niv("D2", L.Doms[1].Fut) : "")
                         + " · regla " + ReglaTexto() + (L.LibroDom == "OI" ? " · dominantes por OI" : "");
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

        // ------------------------------------------------------------------
        // 4.1 (B1): los adaptadores de la Familia (AdaptadoresFamilia.cs, sin ATAS). Aca solo el pegamento con ATAS: registrar esta instancia,
        // pasarle las velas CERRADAS del grafico (respaldo donde no hay ticks) y soltar al quitar el indicador. Los ticks entran por
        // CintaEvento (GammaHoyTresCinta.cs) y la foto del libro NQ por Viva3.Guardar.
        // ------------------------------------------------------------------

        private volatile Familia.CintaFamilia _famCinta;
        private Familia.LibroNqFamilia _famLibro;
        private long _famUltimaVelaMs = long.MinValue;      // apertura (ms UTC) de la ultima vela del grafico ya pasada al ICinta
        private string _famAvisoMarco;

        /// <summary>GANCHO-4.1 familia: el ICinta de esta instancia (cinta del MNQ del grafico; null hasta el primer Tick con raiz NQ).</summary>
        internal Familia.ICinta FamiliaCinta => _famCinta;
        /// <summary>GANCHO-4.1 familia: el ILibroNq (fotos viva3 por minuto del libro NQ de esta 4.0; null hasta el primer Tick con raiz NQ).</summary>
        internal Familia.ILibroNq FamiliaLibroNq => _famLibro;

        /// <summary>GANCHO-4.1 familia: el contrato del grafico para el motor (MNQZ6 -> "Z6"; "" si el codigo no termina en mes+año).</summary>
        internal string ContratoGrafico() => Familia.AdaptadoresFamilia.ContratoDeCodigo(CodigoGrafico());

        private static long MsCrudo(DateTime t) => (t.Ticks - CINTA_EPOCA) / TimeSpan.TicksPerMillisecond;   // ticks crudos: Kind Unspecified = UTC

        /// <summary>Desde Tick: toma (o cambia) el ICinta del instrumento y el ILibroNq de la raiz. Solo NQ (la Familia es de NQ). Nunca tira.</summary>
        private void AsegurarAdaptadoresFamilia(string raiz)
        {
            try
            {
                if (raiz != "NQ") return;
                string ins = ""; try { ins = (InstrumentInfo?.Instrument ?? "").Trim(); } catch { }
                if (string.IsNullOrEmpty(ins)) return;
                var c = _famCinta;
                if (c != null && !string.Equals(c.Instrumento, ins, StringComparison.OrdinalIgnoreCase))
                {
                    Log("familia: el grafico cambio de " + c.Instrumento + " a " + ins + ": suelto la cinta vieja y tomo la nueva");
                    c.Soltar(this); _famCinta = null; _famUltimaVelaMs = long.MinValue; c = null;
                }
                if (c == null)
                {
                    c = Familia.CintaFamilia.Para(ins, raiz);
                    c.Registrar(this);
                    _famCinta = c;
                    Log("familia: ICinta de " + ins + " tomado (" + c.Estado + ")");
                }
                if (_famLibro == null)
                {
                    _famLibro = Familia.LibroNqFamilia.Para(raiz);
                    _famLibro.Registrar(this);
                    Log("familia: ILibroNq de " + raiz + " tomado (" + _famLibro.Estado + ")");
                }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "AsegurarAdaptadoresFamilia", e); }
        }

        private void SoltarAdaptadoresFamilia()
        {
            try { _famCinta?.Soltar(this); } catch (Exception e) { Registro.Excepcion(LOG, "SoltarAdaptadoresFamilia cinta", e); }
            try { _famLibro?.Soltar(this); } catch (Exception e) { Registro.Excepcion(LOG, "SoltarAdaptadoresFamilia libro", e); }
            _famCinta = null; _famLibro = null;
        }

        /// <summary>Desde Tick (cada 5 s; las pestañas ocultas tambien): las velas CERRADAS del grafico que el ICinta todavia no vio, con la hora en
        /// ticks crudos. La primera vez, hasta 20000 velas hacia atras (7 dias de 1 min). El ICinta las usa solo donde no tiene ticks.</summary>
        private void AlimentarVelasGraficoFamilia()
        {
            var c = _famCinta;
            if (c == null) return;
            try
            {
                int ult = CurrentBar - 2;            // la ultima CERRADA (CurrentBar - 1 es la que se forma)
                if (ult < 0) return;
                var nuevas = new List<(long Ap, long Ult, double O, double H, double L, double C)>();
                for (int b = ult, n = 0; b >= 0 && n < 20000; b--, n++)
                {
                    IndicatorCandle v; try { v = GetCandle(b); } catch { break; }
                    if (v == null) continue;
                    long ap = MsCrudo(v.Time);
                    if (ap <= _famUltimaVelaMs) break;
                    long ul = v.LastTime != default(DateTime) ? MsCrudo(v.LastTime) : ap;
                    if (ul < ap) ul = ap;
                    nuevas.Add((ap, ul, (double)v.Open, (double)v.High, (double)v.Low, (double)v.Close));
                }
                if (nuevas.Count == 0) return;
                nuevas.Reverse();
                _famUltimaVelaMs = nuevas[nuevas.Count - 1].Ap;
                int rechazadas = c.VelasGrafico(nuevas);
                if (rechazadas > 0 && _famAvisoMarco == null)
                {
                    _famAvisoMarco = "x";
                    Log("familia: " + rechazadas + " de " + nuevas.Count + " velas del grafico cruzan un balde de 2 min (marco mayor a 2 min?): no sirven de respaldo del ICinta; los ticks si");
                }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "AlimentarVelasGraficoFamilia", e); }
        }

        private string EstadoAdaptadoresFamilia()
        {
            try { return (_famCinta?.Estado ?? "cinta -") + " | " + (_famLibro?.Estado ?? "libro -"); } catch { return "?"; }
        }

        private static void Log(string msg) => Registro.Linea(LOG, msg);
    }
}
