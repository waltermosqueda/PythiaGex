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

using ATAS.DataFeedsCore;
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
            public double BaseCruda;
            public double BaseErrorTicks;
            public double BaseUltimaBuena;
            public double BaseUltimaBuenaEdad;
            public bool BaseConfiable;
            public string Contrato = "";
            public double EdadMin;
            // true cuando la cadena vino de Rithmic: los strikes ya estan en
            // precio de FUTURO y corresponde Black-76 en vez de Black-Scholes.
            public bool EsFuturo;
            public string Fuente = "CBOE";
            // Lo que el feed agrego el 2026-09-06 para poder rotular con
            // honestidad: el ultimo trade de la cadena entera (hora de Nueva
            // York, sin zona) y hasta cuantos dias llegan las filas.
            public string UltimoTrade = "";
            public double HorizonteCadena = double.NaN;
        }

        /// <summary>Lo que sale de repreciar: un renglon por strike.</summary>
        private struct Nivel
        {
            public double K;       // strike en indice
            public double Fut;     // el mismo strike en precio de futuro
            public double Gex;     // exposicion gamma por interes abierto
            public double GexVol;  // lo mismo pero sobre el volumen del dia
            public double Acel;    // cuanto cambia el GEX si S sube 1 %
            public double VolTot;  // contratos operados HOY en ese strike
            public double Oi;      // interes abierto del strike (de ayer, para todos)
            // LA PARTE QUE VENCE HOY, aparte. Hoy el 0DTE entra sumado adentro
            // de Gex y no hay forma de saber cuanto de un nivel es de hoy y
            // cuanto es estructura de la semana. Son cosas distintas: el 0DTE
            // se evapora al cierre y el resto sigue manana.
            public double Gex0;
            // EL VENCIMIENTO QUE SOSTIENE ESE NIVEL.
            //
            // Un muro de 0DTE y uno semanal del mismo tamano NO son lo mismo:
            // la gamma crece como uno sobre raiz del tiempo que falta, asi que
            // al acercarse el vencimiento se dispara Y SE ANGOSTA. El de hoy es
            // un poste clavado en un punto; el semanal es un terraplen
            // repartido. Cerca del precio manda el de hoy; lejos, no existe.
            // Y el de hoy se evapora al cierre.
            //
            // Se guarda el vencimiento de la MAYOR contribucion, no un promedio:
            // lo que importa es quien lo sostiene, no el reparto.
            public double DiasDom;
            public double GexDom;
        }

        // ==============================================================
        private static readonly HttpClient Http = CrearCliente();
        private static HttpClient CrearCliente()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            c.DefaultRequestHeaders.Add("User-Agent", "PythiaGex-GammaVivo/1.0");
            return c;
        }

        private sealed class ZonaDom
        {
            public double Fut, Desde, Hasta;
            public string Caracter = "", Lado = "", Criollo = "";
            public double Incentivo;
            public bool Relevante;
            // El strike en INDICE, la gamma que le dio el radar (Python, su
            // horizonte) y la gamma que le da ESTE indicador (su horizonte),
            // para poder decir en el AUDIT cuando los dos no coinciden.
            public double Idx = double.NaN, GexPy = double.NaN, GexCs = double.NaN;
        }

        private volatile Cadena _c;
        private volatile string _error = "";

        /// <summary>Los barridos del futuro, en vivo, por Rithmic.
        ///
        /// ESTOS SON LOS PUNTITOS, Y NO DEPENDEN DE NINGUNA WEB.
        ///
        /// El glosario del producto que se esta imitando define BigTrades como
        /// "deteccion en tiempo real de operaciones de tamano significativo en
        /// el libro de ordenes (DOM) del futuro". O sea que NO son del mercado
        /// de opciones: son del libro del futuro, que ya entra por Rithmic.
        ///
        /// Un barrido es otra cosa que un print grande. El print grande es el
        /// volumen de un precio en una vela -- un agregado que pueden ser mil
        /// ordenes de un contrato. El barrido son los ticks consecutivos de UN
        /// agresor comiendose varios precios de una. Eso es la mano grande
        /// entrando, y es lo unico que no se puede fingir.
        /// </summary>
        /// <summary>MAX CHANGE: el rastro de donde estuvo cada strike.
        ///
        /// QUE SON "LAS PELOTITAS"
        ///
        /// El propio autor del producto que se esta imitando tiene un video
        /// titulado "Te explico en Vivo el Max Change (Las pelotitas)". En la
        /// descripcion lo dice sin vueltas: son las zonas donde estan
        /// ocurriendo los cambios relevantes de gamma, y por eso el precio
        /// reacciona ahi.
        ///
        /// Mirando ese video cuadro por cuadro en alta resolucion se ve que
        /// NO son marcas sueltas tiradas sobre el grafico: son circulos
        /// apoyados sobre la MISMA fila de cada barra del perfil, a distintas
        /// distancias del borde. O sea que caen sobre el mismo eje de magnitud
        /// que la barra. Eso solo tiene una lectura posible: cada pelotita es
        /// el valor que ese strike tenia ANTES.
        ///
        /// La API de GEXbot lo confirma por otro lado: cada fila de strikes
        /// viene con un arreglo de valores previos, y hay un endpoint
        /// /maxchange que devuelve el strike que mas se movio en ventanas de
        /// 1, 5, 10, 15 y 30 minutos.
        ///
        /// Asi que la barra es el ahora y las pelotitas son la estela. Donde
        /// la estela es larga, ese nivel se esta moviendo; donde esta pegada
        /// a la barra, esta quieto. El strike con la estela mas larga ES el
        /// Max Change del momento.
        ///
        /// ESTO ES CIEN POR CIENTO EN VIVO. No necesita dato nuevo de la
        /// cadena: como el perfil se reprecia con cada tick, la estela sale
        /// de guardar lo que el propio indicador calculo hace 1, 5, 15 y 30
        /// minutos.
        /// </summary>
        private readonly Dictionary<double, List<KeyValuePair<DateTime, double>>> _estela = new();

        /// <summary>Las ventanas, en minutos, igual que las publica GEXbot.</summary>
        private static readonly int[] Ventanas = { 1, 5, 15, 30 };

        /// <summary>Una pelotita: un cambio de gamma detectado, con su hora y
        /// su precio. Se planta en el grafico y se queda.</summary>
        private sealed class Pelotita
        {
            public DateTime Hora;
            public double Strike;     // en indice
            public double Fut;        // convertido a precio de futuro
            public double Delta;      // cuanto cambio el GEX, con signo
            public double Fuerza;     // 0..1 contra el mayor cambio de la sesion
            public int Barra = -1;
        }

        /// <summary>Lo que quedo anotado en cada vela: los niveles calculados
        /// con el precio de esa vela. De aca salen las bandas de puntitos.</summary>
        private sealed class Marca
        {
            // La hora es lo que hace que la marca sobreviva a un reinicio: el
            // NUMERO de vela no es estable entre sesiones, la hora si.
            public DateTime Hora;
            public double MajorPos, MajorNeg, Zero;
            // las dos hipotesis: lo mismo pero sobre el VOLUMEN de hoy, que es
            // lo unico del mapa que se mueve durante la rueda
            public double MajorPosVol, MajorNegVol, ZeroVol;
            // hipotesis 3 y 4: continuas y sin depender del volumen
            public double MaxChange;
            // Donde estaban las dominantes EN ESE MOMENTO. Guardarlas por vela
            // es lo que hace que los guiones ondulen y tengan huecos, en vez de
            // salir una linea recta que seria mentira. Ver PuntosDominantes().
            public double[] Doms;
            public double[] Incs;
            public bool Hay;
        }

        private readonly Dictionary<int, Marca> _porBarra = new();

        private readonly List<Pelotita> _pelotitas = new();
        private DateTime _ultimaPelotita = DateTime.MinValue;
        private DateTime _ultimoVolcado = DateTime.MinValue;
        private int _ultimoMapeoPel = -1;
        private int _visiblesUlt;
        private Rectangle _tableroRect = Rectangle.Empty;
        // atraso del libro de futuros, para poder AFIRMAR que esta en vivo
        private readonly List<double> _atrasoDom = new();
        private readonly List<int> _etiquetasUsadas = new();
        // los rectangulos de los chips ya puestos en este cuadro, y la altura
        // en pixeles de TODAS las rayas (gamma y nodos): un chip no puede
        // tapar la raya de otro nivel
        private readonly List<Rectangle> _rectsUsados = new();
        private readonly List<int> _ysNiveles = new();

        /// <summary>La ultima base que dio confiable, con su hora.
        ///
        /// POR QUE SE GUARDA Y NO SE APAGA TODO
        ///
        /// La medicion de la base parpadea: de noche el libro esta fino, los
        /// forwards no caen sobre una recta y el control la rechaza. Medido el
        /// 2026-09-03: en corridas seguidas dio 8,99 confiable, despues 9,65
        /// confiable, despues nada, despues 11,11 con 20 ticks de error.
        ///
        /// Con la regla dura de "sin base no se dibuja", el indicador se
        /// apagaba entero cada dos por tres. Y apagarse no es mas seguro: es
        /// dejar al operador sin mapa justo cuando el mercado esta fino.
        ///
        /// La base es CARRY: tasa menos dividendo por el plazo que falta. Se
        /// mueve lento, unos pocos puntos por dia. Una base de hace unos
        /// minutos es una estimacion buena; una base de hace horas ya no. Asi
        /// que se guarda la ultima buena, se usa mientras sea reciente, y la
        /// cinta dice de cuando es. Lo que NO se hace nunca es inventarla.
        /// </summary>
        private double _baseBuena = double.NaN;
        private DateTime _baseBuenaHora = DateTime.MinValue;
        private string _baseOrigen = "";
        /// <summary>La base ya resuelta por Repreciar. Pintar la LEE de aca
        /// en vez de volver a decidirla: tener la misma regla escrita dos
        /// veces fue exactamente el error -- se arreglo en el calculo y no
        /// en el dibujo, y el indicador siguio sin dibujar nada durante
        /// media hora con la base ya resuelta.</summary>
        private volatile object _baseResuelta = null;
        private double _mayorDelta = 1;

        private double _maxCambioStrike = double.NaN;
        private double _maxCambioValor;

        /// <summary>Una fila del max change: el strike que mas se movio en esa
        /// ventana, y cuanto. Las ventanas son las mismas que publica GEXbot y
        /// las mismas que muestra el tablero del producto original.</summary>
        private struct FilaCambio { public double Strike; public double Delta; public bool Hay; }
        private readonly FilaCambio[] _cambios = new FilaCambio[5];
        private static readonly int[] VentanasTablero = { 1, 5, 10, 15, 30 };

        private double _netGexVol, _zeroVol;
        private double _maxChangeUlt = double.NaN;
        // los muros de lo que vence hoy, ya en precio de futuro
        private double _mp0Ult, _mn0Ult;
        private Centinela _centinela;
        private int _barraCentinela = -1;
        // volatilidad al dinero, movimiento esperado y GEX total en magnitud
        private double _ivAtmUlt, _movEspUlt, _gexTotalUlt;
        // el zero calculado sobre la cadena ancha, y cuantos strikes tenia
        private double _zeroAnchoUlt = double.NaN;
        private int _strikesAnchoUlt;
        private bool _gammaPositiva;
        private double _majorPosVol, _majorNegVol;

        private readonly Libro _libro = new();
        private readonly List<ZonaDom> _zonas = new();
        private int _ultimoMapeo = -1;
        private int _bajando;
        private TimeSpan _periodo;
        private Action _tick;

        // resultado del ultimo repricing
        private List<Nivel> _perfil = new();
        private double _zeroGamma, _netGex, _maxGex, _maxAcel;
        private double _majorPos, _majorNeg;
        private double _spotUsado = double.NaN;
        private double[] _picosUlt;
        // los extremos SIN la condicion del lado, para poder medir
        // cuanto se apartan de los muros que se dibujan
        private double _mpGlobal, _mnGlobal;
        // el rival de cada muro (en precio de futuro) cuando esta disputado,
        // y cuanto pesa contra el lider (0..1)
        private double _mpRival = double.NaN, _mnRival = double.NaN, _mpRatio, _mnRatio;
        // hasta cuantos dias sumo el radar para armar sus zonas (viene en el feed)
        private double _horizonteZonas = double.NaN;
        // los strikes que REALMENTE tiene la cadena en ese momento,
        // para poder auditar si un pico cae sobre uno de ellos
        private double[] _futsUlt;
        private int _marcasDibujadas, _marcasConRegistro;
        private bool _esFuturo;
        private string _fuente = "CBOE";
        // la cadena que REALMENTE se uso en el ultimo repricing: la cinta
        // leia _c y decia "CBOE, 15 min tarde" mientras el calculo iba con
        // la viva. Dos renglones de la misma pantalla se contradecian.
        private Cadena _cUsada;
        private readonly CadenaViva _viva = new();
        private bool _vivaPedida;
        private Historia.Paquete _histPend;
        private int _histMapeadoEn = -1;
        private int _histRestauradas;
        private DateTime _ultimoGuardado = DateTime.MinValue;
        private Cadena _vivaCache;
        private List<CadenaViva.Fila> _vivaFilasCache;
        private DateTime _vivaCacheHora = DateTime.MinValue;
        private volatile bool _vivaCorriendo;
        // cuantos strikes utiles trajo la viva cuando no alcanzaron
        private int _vivaFlaca = -1;
        private DateTime _ultimaBajada = DateTime.MinValue;
        private DateTime _ultimoIntentoViva = DateTime.MinValue;
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

        [Display(Name = "Aceptar la base cruda si no hay mejor", GroupName = "Fuente", Order = 41,
                 Description = "Antes que dejar la pantalla en blanco. Siempre avisado en la cinta.")]
        public bool UsarBaseCruda { get; set; } = true;

        [Display(Name = "Cuantos minutos vale la base vieja", GroupName = "Fuente", Order = 42)]
        public int MinutosBaseVieja { get; set; } = 45;

        [Display(Name = "Usar la cadena EN VIVO de Rithmic", GroupName = "Fuente", Order = 5,
                 Description = "Sin retraso. Si no esta disponible cae a CBOE, que llega 902 s tarde, " +
                               "y lo avisa en la cinta.")]
        public bool UsarCadenaViva { get; set; } = true;

        [Display(Name = "Refrescar las puntas cada (segundos)", GroupName = "Fuente", Order = 6,
                 Description = "El PRECIO se reprecia en cada tick igual; esto es cada cuanto se " +
                               "releen las puntas de las opciones, que no se mueven tan rapido.")]
        public int SegundosCadenaViva { get; set; } = 5;

        [Display(Name = "Strikes por lado en vivo", GroupName = "Fuente", Order = 6,
                 Description = "Por vencimiento. Subirlo le compite ancho de banda al feed de futuros.")]
        public int StrikesEnVivo { get; set; } = 14;

        [Display(Name = "Vencimientos en vivo", GroupName = "Fuente", Order = 7,
                 Description = "Los mas cercanos. El 0DTE y el de manana ya explican casi todo el GEX.")]
        public int VencimientosEnVivo { get; set; } = 3;

        [Display(Name = "Tope de contratos suscritos", GroupName = "Fuente", Order = 8,
                 Description = "Techo duro. Con ~960 ATAS acuso 7772 ms de atraso en la cinta de futuros.")]
        public int TopeContratos { get; set; } = 180;

        [Display(Name = "Vencimiento del perfil IZQUIERDO", GroupName = "Calculo", Order = 48,
                 Description = "0 = solo el 0DTE (para scalpear) / 1 = hasta los dias de abajo / " +
                               "2 = todos los que haya (vista macro). Asi lo ofrece el producto " +
                               "original: cada perfil con su propio vencimiento.")]
        public int VencIzq { get; set; } = 1;

        [Display(Name = "Vencimiento del perfil DERECHO", GroupName = "Calculo", Order = 49,
                 Description = "Mismo criterio. Poner 0 a la izquierda y 2 a la derecha deja las " +
                               "dos lecturas en la misma pantalla: el mapa de hoy y el de fondo.")]
        public int VencDer { get; set; } = 1;

        [Display(Name = "Guardar lo acumulado entre reinicios", GroupName = "Historia", Order = 120,
                 Description = "Las dominantes por vela, el rastro de los strikes y las pelotitas " +
                               "sobreviven al reinicio. NO se guarda ningun nivel calculado: el zero " +
                               "gamma, los muros y el perfil se rehacen desde la cadena en cada tick.")]
        public bool PersistirHistoria { get; set; } = true;

        [Display(Name = "Cuantas horas guardar", GroupName = "Historia", Order = 121,
                 Description = "Mas viejo que esto se descarta al restaurar: una marca de la semana " +
                               "pasada sobre el grafico de hoy es ruido, no historia.")]
        public int HorasHistoria { get; set; } = 24;

        [Display(Name = "Dias de vencimiento a incluir", GroupName = "Calculo", Order = 50)]
        public int DiasMax { get; set; } = 7;

        [Display(Name = "Tasa anual (para Black-Scholes)", GroupName = "Calculo", Order = 51)]
        public decimal Tasa { get; set; } = 0.0375m;

        [Display(Name = "Ver perfil de gamma (izquierda)", GroupName = "Dibujo", Order = 60)]
        public bool VerGamma { get; set; } = true;

        [Display(Name = "Ver aceleracion (derecha)", GroupName = "Dibujo", Order = 61)]
        public bool VerAcel { get; set; } = true;

        [Display(Name = "Radio para escalar el perfil (pts)", GroupName = "Dibujo", Order = 61,
                 Description = "Contra que se mide el largo de las barras: los strikes dentro de " +
                               "este radio del PRECIO. No contra los que se ven, porque entonces el " +
                               "largo cambiaria al desplazar la pantalla y dejaria de significar lo " +
                               "mismo.")]
        public decimal RadioNormalizar { get; set; } = 80m;

        [Display(Name = "Ancho maximo de barra (px)", GroupName = "Dibujo", Order = 62,
                 Description = "El perfil vive en el borde. Ancho pisa las velas y no deja leer.")]
        public int AnchoBarra { get; set; } = 78;

        [Display(Name = "Intensidad del perfil (0-100)", GroupName = "Dibujo", Order = 62,
                 Description = "El perfil es contexto, no protagonista: apagado deja ver el precio.")]
        public int OpacidadPerfil { get; set; } = 58;

        [Display(Name = "Franja de regimen arriba", GroupName = "Dibujo", Order = 59,
                 Description = "Verde = gamma positiva (rango). Roja = negativa (expansion). " +
                               "Es lo primero que hay que saber y se lee sin leer.")]
        public bool VerFranjaRegimen { get; set; } = true;

        [Display(Name = "Rotulos pegados al eje", GroupName = "Dibujo", Order = 64,
                 Description = "Con la distancia en puntos. Ahi ya esta mirando el ojo.")]
        public bool RotulosDerecha { get; set; } = true;

        [Display(Name = "Anotar el CENTINELA de actividad", GroupName = "Calculo", Order = 44,
                 Description = "Escribe una fila por vela con la hora, el rango, el volumen, las " +
                               "OPERACIONES (la velocidad del tape) y los niveles vigentes. No " +
                               "dibuja nada y no concluye nada: junta materia prima para poder " +
                               "medir despues, afuera y con placebo, si la actividad se acelera " +
                               "cuando el precio llega a un nivel. Cuesta una linea de texto por " +
                               "vela.")]
        public bool AnotarCentinela { get; set; } = true;

        [Display(Name = "Ver los niveles del 0DTE aparte", GroupName = "Dibujo", Order = 66,
                 Description = "Los muros calculados SOLO con lo que vence hoy. Hoy el 0DTE esta " +
                               "mezclado adentro del perfil y no se ve. Se separa porque el " +
                               "laboratorio lo midio: sobre 300 fotos de una rueda, los niveles " +
                               "de solo 0DTE frenaron al precio el 67,9 % contra 49,3 % de su " +
                               "placebo, y con 81 toques, la muestra mas grande de todas las " +
                               "formulas probadas.")]
        public bool VerNiveles0DTE { get; set; } = true;

        [Display(Name = "Anotar el tamano en las etiquetas", GroupName = "Dibujo", Order = 67,
                 Description = "Le agrega al chip que ya existe cuanta gamma hay en ese nivel y si " +
                               "la manda el 0DTE. Queda '+wall 7.832  1,2B 0D'. No agrega ningun " +
                               "dibujo nuevo: escribe adentro de la etiqueta que ya esta.")]
        public bool AnotarTamano { get; set; } = true;

        [Display(Name = "Usar el libro de ES en vivo (en vez de SPX)", GroupName = "Calculo", Order = 46,
                 Description = "APAGADO (recomendado): todos los niveles salen del libro de SPX, " +
                               "que es 8 veces mas grande y cuyas mesas cubren con futuros de ES. " +
                               "PRENDIDO: usa la cadena de ES en vivo cuando esta.  |  Lo que NO se " +
                               "puede hacer es alternar: son dos mercados distintos y al cambiar de " +
                               "uno a otro los muros saltan 33 puntos sin que el precio se mueva.")]
        public bool LibroViva { get; set; } = false;

        [Display(Name = "El ZERO GAMMA sale de la cadena mas ANCHA", GroupName = "Calculo", Order = 47,
                 Description = "El zero gamma es el punto donde EQUILIBRA la suma de toda la cadena, " +
                               "asi que depende de las puntas y no solo del centro. La cadena viva " +
                               "solo suscribe una ventana angosta alrededor del precio -- unos 41 " +
                               "strikes contra 141 de la cadena completa -- y con esa ventana el " +
                               "cruce queda corrido. Medido sobre 1112 lecturas: al cambiar de " +
                               "fuente el zero gamma pega saltos de 28 puntos que NO son mercado. " +
                               "Prendido, el zero se calcula siempre sobre la cadena mas ancha " +
                               "disponible, repreciada al precio de AHORA.")]
        public bool ZeroDeCadenaAncha { get; set; } = true;

        [Display(Name = "Ver los titulos de las mitades", GroupName = "Dibujo", Order = 69)]
        public bool VerTitulos { get; set; } = false;

        [Display(Name = "Alto de barra (px, 0 = automatico)", GroupName = "Dibujo", Order = 63)]
        public int AltoBarra { get; set; } = 0;

        [Display(Name = "Marcar los niveles fuera de pantalla", GroupName = "Dibujo", Order = 68,
                 Description = "Si un nivel quedo arriba o abajo del rango visible, lo avisa en el borde.")]
        public bool MarcarFueraDePantalla { get; set; } = true;

        [Display(Name = "Ver Zero Gamma y Majors", GroupName = "Dibujo", Order = 64)]
        public bool VerLineas { get; set; } = true;

        // NACEN APAGADOS, A PROPOSITO.
        //
        // Cada uno de estos dibuja su propio chip contra el eje, con su propio
        // color. Con todos prendidos quedan seis colores apilados en la misma
        // columna -- flujo, acel, freno, +wall, -wall, zero -- y el operador ya
        // no distingue cual es cual. Lo llamo "el arcoiris", y tenia razon.
        //
        // El que los quiera los prende. Lo que no puede pasar es que una
        // instancia nueva nazca con todo encendido y haya que limpiarla a mano
        // cada vez.
        // RENOMBRADA (era VerFlujo) para que el default APAGADO llegue al
        // workspace guardado. Estas lineas usan el volumen de la cadena de
        // CBOE: 15 minutos tarde, y de noche el del dia anterior. Desde el
        // 2026-09-06 el volumen vivo de Rithmic va en cada chip ("v181"), asi
        // que estas lineas duplicaban el strike con un numero viejo al lado
        // del vivo. Visto el 2026-09-07: "G2 freno 7.756" y "G2 flujo 20618
        // 7.756" uno arriba del otro.
        [Display(Name = "Ver el FLUJO de CBOE (retrasado)", GroupName = "Flujo", Order = 130,
                 Description = "Los strikes donde mas se opero segun la cadena de CBOE, que llega 15 " +
                               "minutos tarde y de noche trae el dia anterior. El volumen VIVO de " +
                               "Rithmic ya va en cada chip; esto queda apagado salvo que quieras " +
                               "comparar los dos.")]
        public bool VerFlujoCboe { get; set; } = false;

        [Display(Name = "Cuantos niveles de flujo", GroupName = "Flujo", Order = 131)]
        public int CuantosFlujo { get; set; } = 3;

        [Display(Name = "Minimo de contratos", GroupName = "Flujo", Order = 132,
                 Description = "Por debajo de esto no es flujo, es ruido.")]
        public int MinContratosFlujo { get; set; } = 25;

        [Display(Name = "Color del flujo", GroupName = "Flujo", Order = 133)]
        public Color ColFlujo { get; set; } = Color.FromArgb(215, 180, 255);

        [Display(Name = "Ver los strikes CERCANOS", GroupName = "Cercanos", Order = 110,
                 Description = "Los mas cargados que estan al lado del precio. Medido: con el " +
                               "futuro en 7752 el major positive apuntaba a 7800 -- 48 puntos -- " +
                               "mientras 7760, a OCHO puntos, tenia el 96 % de esa gamma y no se " +
                               "dibujaba. Para scalpear eso es al reves de lo que sirve.")]
        // RENOMBRADA (era VerCercanos = false): el operador no veia ningun
        // strike entre el precio y los muros, que estaban a 116 y 59 puntos.
        // Los cercanos son justamente "el siguiente arriba y el siguiente
        // abajo" con su chip completo. Renombrar fuerza el default nuevo.
        public bool VerNivelesCercanos { get; set; } = true;

        [Display(Name = "Cuantos cercanos", GroupName = "Cercanos", Order = 111)]
        public int CuantosCercanos { get; set; } = 4;

        [Display(Name = "Hasta cuantos puntos", GroupName = "Cercanos", Order = 112,
                 Description = "Radio alrededor del precio. Mas alla de esto ya no es 'cerca' " +
                               "para un scalp.")]
        public decimal RadioCercanos { get; set; } = 45m;

        [Display(Name = "Minimo, en % del mayor", GroupName = "Cercanos", Order = 113,
                 Description = "Un strike chico al lado del precio no frena nada. Por debajo de " +
                               "este porcentaje del strike mas grande de la cadena, no se dibuja.")]
        // RENOMBRADA (era MinimoCercano = 22). Medido el 2026-09-07 00:11 con el
        // maximo en 2,9 B: con 22 % ningun strike de ARRIBA pasaba (7726 tenia
        // 546 M) y el operador se quedaba sin "siguiente arriba". Con 15 %
        // entran 7726 y 7736 arriba, 7706 y 7691 abajo.
        public int MinimoCercanoPct { get; set; } = 15;

        [Display(Name = "Freno (gamma +)", GroupName = "Cercanos", Order = 114,
                 Description = "Donde la mesa absorbe: compra caidas y vende subas.")]
        public Color ColCercanoPos { get; set; } = Color.FromArgb(70, 200, 145);

        [Display(Name = "Acelerador (gamma -)", GroupName = "Cercanos", Order = 115,
                 Description = "Donde la mesa amplifica: vende caidas y compra subas.")]
        public Color ColCercanoNeg { get; set; } = Color.FromArgb(235, 110, 95);

        [Display(Name = "Margen del eje de precios (px)", GroupName = "Dibujo", Order = 65)]
        public int MargenEje { get; set; } = 62;

        [Display(Name = "Margen inferior (px)", GroupName = "Dibujo", Order = 65,
                 Description = "ATAS entrega un area mas alta que la que se ve: sin este margen " +
                               "lo que se ancla al fondo queda detras del eje de tiempo.")]
        public int MargenInferior { get; set; } = 48;

        [Display(Name = "Ver el tablero de datos", GroupName = "Dibujo", Order = 67)]
        public bool VerTablero { get; set; } = true;

        [Display(Name = "Tablero compacto", GroupName = "Dibujo", Order = 67,
                 Description = "Chiquito a un costado. Apagalo para ver el tablero completo " +
                               "con volumen, interes abierto y max change.")]
        public bool TableroCompacto { get; set; } = true;

        [Display(Name = "Muro disputado: segundo >= % del primero", GroupName = "Calculo", Order = 47,
                 Description = "Si el segundo strike del mismo lado pesa al menos este porcentaje del " +
                               "primero, el muro se marca DISPUTADO: el panel muestra al rival y se dibuja " +
                               "una linea fina. Medido el 2026-08-31: un call wall salto 50 puntos con el " +
                               "segundo al 89 %. El 2026-09-06, 7750 y 7825 se alternaron toda la noche.")]
        [Range(50, 99)]
        public int UmbralDisputa { get; set; } = 85;

        [Display(Name = "Jerarquia visual por distancia y vencimiento", GroupName = "Pantalla", Order = 3,
                 Description = "Los muros se atenuan si estan a mas de un movimiento esperado del precio " +
                               "y si su gamma la sostiene un vencimiento lejano. Lo que esta cerca y vence " +
                               "hoy se ve entero; lo que esta lejos y vence en dos semanas, apagado. " +
                               "Pedido por el operador el 2026-09-06: 'no se cual es mas importante'.")]
        public bool JerarquiaVisual { get; set; } = true;

        [Display(Name = "Etiqueta detallada (GEX, OI, aceleracion, volumen vivo)", GroupName = "Pantalla", Order = 8,
                 Description = "Cada nivel de gamma lleva una segunda linea con su GEX repreciado al precio de " +
                               "ahora, su interes abierto (de ayer, para todos), su aceleracion (cuanto cambia " +
                               "el GEX si el precio sube 1 %) y los contratos de opciones operados HOY en ese " +
                               "strike (Rithmic, en vivo). Modelo 3 elegido por el operador el 2026-09-06.")]
        public bool EtiquetaDetallada { get; set; } = true;

        [Display(Name = "Tamano de letra de las etiquetas", GroupName = "Pantalla", Order = 10,
                 Description = "Una sola linea, chica pero legible. 8 es el compromiso medido el 2026-09-06; " +
                               "con 7 la coma decimal deja de distinguirse.")]
        [Range(6.5, 12.0)]
        public decimal TamEtiqueta { get; set; } = 7.5m;

        [Display(Name = "Decimales del precio en la etiqueta", GroupName = "Pantalla", Order = 11,
                 Description = "0 = '7831', como en la maqueta B aprobada. Los niveles de SPX llevan la base " +
                               "sumada (7831,16): con 0 se redondea al entero. Subilo a 2 si queres verla.")]
        [Range(0, 2)]
        public int DecimalesPrecio { get; set; } = 0;

        [Display(Name = "Banda ACA sobre el nivel donde esta el precio", GroupName = "Pantalla", Order = 9,
                 Description = "Si el precio esta a dos ticks o menos de un nivel (gamma o nodo de volumen), " +
                               "se pinta una banda tenue de su color y se escribe ACA con su nombre.")]
        public bool VerBandaAca { get; set; } = true;

        [Display(Name = "Ver volumen vivo de opciones por strike (Rithmic)", GroupName = "Flujo", Order = 1,
                 Description = "Circulos en el borde derecho, uno por strike, con tamano segun los contratos " +
                               "operados HOY en las opciones de ES (SecuritySummaryChanged del conector). " +
                               "Es el unico dato del mapa que no es de ayer, y es en vivo. Los tres " +
                               "mayores llevan el numero.")]
        public bool VerVolumenVivo { get; set; } = true;

        [Display(Name = "Ver la ESCALERA de niveles", GroupName = "Pantalla", Order = 4,
                 Description = "Los peldanos mas cercanos al precio, arriba y abajo, en una sola lista: " +
                               "nodos de volumen (del indicador PythiaFlow, si esta en el grafico), zero, " +
                               "muros, pin y el vencimiento mas cercano. Cada uno con distancia y con la " +
                               "probabilidad de tocarlo dentro del horizonte, calculada con la volatilidad " +
                               "REALIZADA del propio grafico: de noche da poco, en la rueda da mucho. Es " +
                               "un modelo, no un pronostico.")]
        public bool VerEscalera { get; set; } = false;   // el operador eligio el modelo 3; esta queda opcional

        [Display(Name = "Escalera: peldanos por lado", GroupName = "Pantalla", Order = 5)]
        [Range(1, 6)]
        public int EscaleraPorLado { get; set; } = 3;

        [Display(Name = "Escalera: horizonte de la probabilidad (min)", GroupName = "Pantalla", Order = 6,
                 Description = "En cuantos minutos se pregunta si el precio toca el nivel. 60 para scalping.")]
        [Range(5, 600)]
        public int HorizonteProbMin { get; set; } = 60;

        [Display(Name = "Escalera: velas para la volatilidad realizada", GroupName = "Pantalla", Order = 7,
                 Description = "Cuantas velas cerradas se usan para medir cuanto se mueve el precio AHORA.")]
        [Range(10, 500)]
        public int EscaleraVelasVol { get; set; } = 60;

        [Display(Name = "Tablero a la derecha", GroupName = "Dibujo", Order = 67,
                 Description = "Para que no tape el perfil de gamma, que se dibuja a la izquierda.")]
        public bool TableroDerecha { get; set; } = true;

        [Display(Name = "Tablero abajo a la izquierda", GroupName = "Dibujo", Order = 68,
                 Description = "Arriba a la izquierda tapa la accion del precio en un grafico angosto.")]
        public bool TableroAbajo { get; set; } = true;

        [Display(Name = "Tamano del tablero", GroupName = "Dibujo", Order = 69)]
        public decimal TamTablero { get; set; } = 9m;

        [Display(Name = "Ver la cinta de estado", GroupName = "Dibujo", Order = 66)]
        public bool VerCinta { get; set; } = true;

        [Display(Name = "Marcas de las dominantes", GroupName = "Dominantes", Order = 82,
                 Description = "Una marca por vela en cada nivel dominante de ESE momento. " +
                               "Evidencia en conflicto: el video de NinjaTrader no las dibuja en el " +
                               "cuerpo del grafico, pero las capturas del operador si -- una rotulada " +
                               "'niveles de mayor exposicion gamma'. Probablemente sea la version web " +
                               "contra la de NinjaTrader.")]
        public bool VerPuntosDominantes { get; set; } = true;

        [Display(Name = "Forma de la dominante", GroupName = "Dominantes", Order = 83,
                 Description = "0 = cuadradito  ·  1 = redondito  ·  2 = guioncito (mas ancho que alto)")]
        public int FormaDominante { get; set; } = 0;


        [Display(Name = "Separacion minima entre dominantes (pts)", GroupName = "Dominantes", Order = 85,
                 Description = "En PUNTOS DE PRECIO, no en pixeles. Dos dominantes mas cerca que " +
                               "esto cuentan como una sola y queda la mas fuerte. Se decide al " +
                               "calcular, asi que el zoom no cambia cuantas hay.")]
        public decimal SeparacionPuntos { get; set; } = 4m;

        [Display(Name = "Cuantas dominantes como maximo", GroupName = "Dominantes", Order = 84)]
        public int MaxDominantes { get; set; } = 14;

        [Display(Name = "Como se ubica la dominante", GroupName = "Dominantes", Order = 83,
                 Description = "1 = EL STRIKE (por defecto, y es la definicion estandar): el " +
                               "strike donde el GEX es mas alto en valor absoluto. Se dibuja plano " +
                               "PORQUE ES PLANO.  |  0 = centro de gravedad: promedio de precio " +
                               "ponderado por gamma; ondula, pero es invento nuestro y no coincide " +
                               "con ninguna fuente.  |  2 = el pico interpolado: se queda clavado y " +
                               "despues salta.")]
        public int ModoDominante { get; set; } = 1;

        [Display(Name = "Radio del centro de gravedad (pts)", GroupName = "Dominantes", Order = 88,
                 Description = "Cuanto perfil entra en el promedio a cada lado del cumulo. Mas " +
                               "chico sigue mas de cerca al strike mandante; mas grande se mueve " +
                               "mas suave.")]
        public decimal RadioCentroide { get; set; } = 15m;

        [Display(Name = "Fuerza minima de una dominante (% del maximo)", GroupName = "Dominantes", Order = 87,
                 Description = "Por debajo de esto el strike no tiene gamma suficiente para " +
                               "merecer una marca. Subilo si ves demasiadas.")]
        public int MinFuerzaDominante { get; set; } = 15;

        [Display(Name = "Separacion minima entre marcas (px)", GroupName = "Dominantes", Order = 86,
                 Description = "Si dos dominantes caen mas cerca que esto en la misma vela, se dibuja " +
                               "solo la mas fuerte. Asi nunca se ven pegadas y no hay que agrandar la " +
                               "pantalla para saber si son una o dos.")]
        public int SeparacionMinima { get; set; } = 4;

        [Display(Name = "Alto del punto (px)", GroupName = "Dominantes", Order = 83)]
        public int AltoPunto { get; set; } = 4;

        [Display(Name = "Ancho del punto (px)", GroupName = "Dominantes", Order = 84)]
        public int AnchoPunto { get; set; } = 4;

        [Display(Name = "Punto de la dominante", GroupName = "Colores", Order = 87)]
        public Color ColPuntoDom { get; set; } = Color.FromArgb(232, 168, 56);

        [Display(Name = "Punto del Zero Gamma", GroupName = "Colores", Order = 88)]
        public Color ColPuntoZero { get; set; } = Color.FromArgb(225, 228, 232);

        // ------------------------------------------------------------------
        // DOS HIPOTESIS SOBRE QUE ES LO QUE SE MUEVE.
        //
        // El problema medido: nuestras marcas por vela salen planas. La causa
        // es que de los tres niveles que calculamos, el unico que anotamos por
        // vela es el de INTERES ABIERTO -- y el interes abierto lo consolida la
        // OCC de noche, asi que el strike ganador es el mismo de la apertura al
        // cierre. Estabamos dibujando justo el unico que no se puede mover.
        //
        // HIPOTESIS 1 (escalones): los majors por VOLUMEN. Tambien se paran
        // sobre un strike, pero el volumen se acumula durante la rueda: si el
        // strike ganador cambia varias veces por dia, eso dibuja una escalera
        // sin necesidad de inventar ningun promedio.
        //
        // HIPOTESIS 2 (ondulacion): el zero gamma sobre el VOLUMEN de hoy. No
        // esta clavado a un strike -- es el precio donde la suma cruza cero --
        // y ademas lo mueve el flujo del dia. Tiene las dos propiedades a la
        // vez, y es la que mas cierra.
        //
        // CADA UNA CON SU COLOR, a proposito: asi la pantalla misma dice cual
        // acierta, sin que haya que creerle a nadie. Y cada una con su
        // interruptor, para poder volver atras sin recompilar.
        // ------------------------------------------------------------------

        [Display(Name = "HIPOTESIS 1: majors por VOLUMEN (celeste)", GroupName = "Hipotesis", Order = 200,
                 Description = "El strike con mas gamma por volumen de HOY. Deberia dar escalones, " +
                               "porque el volumen se acumula durante la rueda y el ganador cambia.")]
        public bool VerMajorsVolumen { get; set; } = false;

        [Display(Name = "Color de la hipotesis 1", GroupName = "Hipotesis", Order = 201)]
        public Color ColPuntoVol { get; set; } = Color.FromArgb(120, 220, 255);

        [Display(Name = "HIPOTESIS 2: zero gamma por VOLUMEN (violeta)", GroupName = "Hipotesis", Order = 202,
                 Description = "El precio donde la suma ponderada por el volumen de hoy cruza cero. " +
                               "No es un strike, asi que se mueve continuo, y ademas responde al " +
                               "flujo del dia.")]
        public bool VerZeroVolumen { get; set; } = false;

        [Display(Name = "Color de la hipotesis 2", GroupName = "Hipotesis", Order = 203)]
        public Color ColPuntoZeroVol { get; set; } = Color.FromArgb(200, 130, 255);

        // Las hipotesis 1 y 2 se alimentan del VOLUMEN, y de noche el volumen
        // es cero: quedan en negro y no se pueden comparar. Estas dos usan solo
        // el interes abierto, que esta siempre, asi que dibujan a cualquier
        // hora. Las dos son CONTINUAS -- se recalculan repreciando la cadena al
        // precio del momento -- asi que se mueven con cada tick sin estar
        // clavadas a ningun strike.

        [Display(Name = "HIPOTESIS 3: donde el gamma CAMBIA mas rapido (verde)", GroupName = "Hipotesis", Order = 204,
                 Description = "El precio donde la suma de gamma cambia mas rapido si el precio se " +
                               "mueve. Uno de los videos de GAMMAlito se llama justamente 'Max " +
                               "Change'. Es continuo: no cae sobre un strike.")]
        public bool VerMaxChange { get; set; } = false;

        [Display(Name = "Color de la hipotesis 3", GroupName = "Hipotesis", Order = 205)]
        public Color ColPuntoMaxChange { get; set; } = Color.FromArgb(140, 240, 120);

        // LA HIPOTESIS 4 SE ELIMINO. No se apago: se saco.
        //
        // Era el centro de gravedad del perfil, y la propuse yo. Se midio sobre
        // 228 lecturas de una rueda americana: correlacion 0,995 con el precio
        // y beta 0,93. O sea que no era un nivel, era el precio con retardo.
        //
        // Se saca en vez de dejarla apagada porque el codigo refutado que
        // queda dando vueltas es el que un dia se vuelve a prender solo.

        [Display(Name = "Ver Max Change (las pelotitas)", GroupName = "Max Change", Order = 100)]
        public bool VerPelotitas { get; set; } = false;

        [Display(Name = "Tamano de la pelotita (px)", GroupName = "Max Change", Order = 101)]
        public int TamPelotita { get; set; } = 11;

        [Display(Name = "Ventana del cambio (minutos)", GroupName = "Max Change", Order = 103)]
        public int VentanaCambio { get; set; } = 5;

        [Display(Name = "Anotar como maximo cada (segundos)", GroupName = "Max Change", Order = 104,
                 Description = "Para no llenar la pantalla de pelotitas identicas.")]
        public int CadaSegundos { get; set; } = 30;

        [Display(Name = "Cuantas pelotitas guardar", GroupName = "Max Change", Order = 105)]
        public int MaxPelotitas { get; set; } = 500;

        [Display(Name = "Resaltar el mayor cambio", GroupName = "Max Change", Order = 102)]
        public bool ResaltarMaxCambio { get; set; } = true;

        [Display(Name = "Pelotita", GroupName = "Colores", Order = 84)]
        public Color ColPelotita { get; set; } = Color.FromArgb(210, 150, 60);

        [Display(Name = "Pelotita del mayor cambio", GroupName = "Colores", Order = 85)]
        public Color ColPelotitaMax { get; set; } = Color.FromArgb(235, 185, 70);

        [Display(Name = "Ver zonas dominantes", GroupName = "Dominantes", Order = 80)]
        public bool VerDominantes { get; set; } = true;

        [Display(Name = "Tambien las zonas debiles", GroupName = "Dominantes", Order = 85,
                 Description = "Las que hoy son decorativas. Apagadas: ensucian la pantalla.")]
        public bool VerZonasDebiles { get; set; } = false;

        [Display(Name = "Relleno de la zona (0-100)", GroupName = "Dominantes", Order = 81)]
        public int OpacidadZona { get; set; } = 10;

        [Display(Name = "Ver BigTrades del libro", GroupName = "BigTrades", Order = 90)]
        public bool VerBigTrades { get; set; } = false;

        [Display(Name = "Cuantos BigTrades dibujar", GroupName = "BigTrades", Order = 92,
                 Description = "Solo se dibujan los N mas grandes de los que hay en pantalla. " +
                               "El resto se sigue guardando para el analisis.")]
        public int CuantosDibujar { get; set; } = 14;

        [Display(Name = "Umbral automatico", GroupName = "BigTrades", Order = 91,
                 Description = "El corte sale del propio flujo del instrumento, no de un numero fijo.")]
        public bool UmbralAuto { get; set; } = true;

        [Display(Name = "Cuantas veces la mediana", GroupName = "BigTrades", Order = 92)]
        public decimal FactorUmbral { get; set; } = 8m;

        [Display(Name = "Minimo de contratos", GroupName = "BigTrades", Order = 93)]
        public decimal MinContratos { get; set; } = 15m;

        [Display(Name = "Memoria (minutos)", GroupName = "BigTrades", Order = 94)]
        public int MemoriaMin { get; set; } = 240;

        [Display(Name = "Cuantos mostrar", GroupName = "BigTrades", Order = 95)]
        public int MaxPuntos { get; set; } = 400;

        [Display(Name = "Tamano del circulo (px)", GroupName = "BigTrades", Order = 96)]
        public int TamPunto { get; set; } = 20;

        [Display(Name = "Tamano minimo del circulo (px)", GroupName = "BigTrades", Order = 98)]
        public int TamPuntoMin { get; set; } = 9;

        [Display(Name = "Escribir los contratos adentro", GroupName = "BigTrades", Order = 97)]
        public bool NumeroAdentro { get; set; } = true;

        [Display(Name = "Circulos de la estela", GroupName = "Perfil", Order = 53,
                 Description = "ESTO SI es lo que dibuja el original: circulos sobre la barra de cada " +
                               "strike marcando donde estuvo su valor antes. Adentro de la barra = " +
                               "encogio, afuera = crecio. Verificado en cuadros de 1920x1080 del " +
                               "producto real (MES 30s, titulo 'GAMMAlito - Gexbot').")]
        public bool VerEstela { get; set; } = true;

        [Display(Name = "Cuantos circulos por barra", GroupName = "Perfil", Order = 54)]
        public int CirculosPorBarra { get; set; } = 3;

        [Display(Name = "Tamano del circulo (px)", GroupName = "Perfil", Order = 55,
                 Description = "Techo, no valor fijo: nunca se dibuja mas alto que la separacion " +
                               "entre strikes, porque si no los de filas vecinas se tocan y se ve " +
                               "una mancha en vez de puntos.")]
        public int TamCirculo { get; set; } = 7;

        [Display(Name = "Forma de la marca", GroupName = "Perfil", Order = 56,
                 Description = "0 = circulo lleno  ·  1 = anillo (el que mejor se separa a tamano " +
                               "chico)  ·  2 = rectangulito. Probar cual se lee mejor con tu zoom.")]
        public int FormaMarca { get; set; } = 1;

        [Display(Name = "Achicar la marca (px)", GroupName = "Perfil", Order = 57,
                 Description = "Se le restan al techo. Subilo si todavia se ven pegadas.")]
        public int AchicarMarca { get; set; } = 1;

        [Display(Name = "Circulo de la estela (izquierda)", GroupName = "Colores", Order = 86)]
        public Color ColCircIzq { get; set; } = Color.FromArgb(150, 158, 168);

        [Display(Name = "Circulo de la estela (derecha)", GroupName = "Colores", Order = 89)]
        public Color ColCircDer { get; set; } = Color.FromArgb(150, 158, 168);

        [Display(Name = "Compra agresiva", GroupName = "Colores", Order = 78)]
        public Color ColCompra { get; set; } = Color.FromArgb(80, 220, 150);

        [Display(Name = "Venta agresiva", GroupName = "Colores", Order = 79)]
        public Color ColVenta { get; set; } = Color.FromArgb(240, 100, 90);

        [Display(Name = "Gamma positiva", GroupName = "Colores", Order = 70)]
        public Color ColPos { get; set; } = Color.FromArgb(45, 220, 130);

        [Display(Name = "Gamma negativa", GroupName = "Colores", Order = 71)]
        public Color ColNeg { get; set; } = Color.FromArgb(235, 60, 60);

        [Display(Name = "Aceleracion positiva", GroupName = "Colores", Order = 72)]
        public Color ColAcelPos { get; set; } = Color.FromArgb(190, 40, 190);

        [Display(Name = "Aceleracion negativa", GroupName = "Colores", Order = 73)]
        public Color ColAcelNeg { get; set; } = Color.FromArgb(40, 225, 225);

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
            // EL PANEL TIENE QUE SER EL DEL PRECIO.
            //
            // Un indicador agregado a mano cae en un panel propio, y ahi
            // ChartArea es una franja de cien pixeles mientras que las barras
            // se posicionan con GetYByPrice contra la escala del precio: todo
            // queda fuera del recorte y no se ve NADA, sin ningun error.
            // Costo un ciclo entero de compilar-reiniciar descubrirlo.
            try
            {
                var ps = DataProvider?.Panels;
                if (ps != null && ps.Count > 0)
                    Panel = ps.Contains("Chart") ? "Chart" : ps[0];
                Registrar2("panel elegido: " + Panel + "  (disponibles: " +
                           (ps == null ? "?" : string.Join(", ", ps)) + ")");
            }
            catch (Exception e) { Registrar(e); }

            // UN SOLO TEMPORIZADOR PARA LAS DOS TAREAS.
            //
            // Antes habia dos SubscribeToTimer, uno para bajar la cadena de
            // CBOE y otro para reintentar la cadena viva. ATAS parece honrar
            // uno solo: el segundo piso al primero y la cadena de CBOE dejo de
            // refrescarse. Se vio auditando -- el feed publicado tenia 0,6
            // minutos y el indicador seguia usando el de hacia catorce.
            //
            // Con un tick fijo y el tiempo controlado aca adentro no se depende
            // de cuantas suscripciones soporte la plataforma.
            _periodo = TimeSpan.FromSeconds(30);
            _tick = () =>
            {
                var ahora = DateTime.UtcNow;

                // re-medir el reloj cada media hora: la deriva se acumula
                if (Reloj.UltimaMedicion == DateTime.MinValue
                    || (ahora - Reloj.UltimaMedicion).TotalMinutes >= 30)
                    _ = Reloj.Medir(Registrar2);

                if ((ahora - _ultimaBajada).TotalSeconds >= Math.Max(60, SegundosRefresco))
                {
                    _ultimaBajada = ahora;
                    _ = Bajar();
                }

                if (UsarCadenaViva && !_viva.Activa && !_vivaCorriendo
                    && (ahora - _ultimoIntentoViva).TotalSeconds >= 180)
                {
                    _ultimoIntentoViva = ahora;
                    _vivaCorriendo = true;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _viva.Arrancar(DataProvider, TradingManager,
                                                 TradingManager?.Security, Raiz(),
                                                 Math.Max(1, DiasMax),
                                                 Math.Max(5, StrikesEnVivo),
                                                 Math.Max(1, VencimientosEnVivo),
                                                 Math.Max(20, TopeContratos),
                                                 m => Registrar2(m)).ConfigureAwait(false);
                        }
                        catch (Exception e) { Registrar(e); }
                        finally { _vivaCorriendo = false; }
                    });
                }
            };
            SubscribeToTimer(_periodo, _tick);

            // el primer arranque no espera al tick
            LeerHistoria();

            // el desfase del reloj, para que el atraso del libro salga corregido
            _ = Reloj.Medir(Registrar2);

            _ultimaBajada = DateTime.UtcNow;
            _ultimoIntentoViva = DateTime.UtcNow;
            _ = Bajar();
            if (UsarCadenaViva)
            {
                _vivaCorriendo = true;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _viva.Arrancar(DataProvider, TradingManager,
                                             TradingManager?.Security, Raiz(),
                                             Math.Max(1, DiasMax),
                                             Math.Max(5, StrikesEnVivo),
                                             Math.Max(1, VencimientosEnVivo),
                                             Math.Max(20, TopeContratos),
                                             m => Registrar2(m)).ConfigureAwait(false);
                    }
                    catch (Exception e) { Registrar(e); }
                    finally { _vivaCorriendo = false; }
                });
            }
        }

        protected override void OnDispose()
        {
            try { if (_tick != null) UnsubscribeFromTimer(_periodo, _tick); } catch { }
            try { GuardarHistoria(); } catch { }
            try { _viva.Dispose(); } catch { }
        }

        /// <summary>El repricing va aca, no en el timer: OnCalculate corre con
        /// cada barra nueva y con cada tick de la ultima, que es exactamente la
        /// cadencia a la que tiene que latir el perfil.</summary>
        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar != CurrentBar - 1) return;
            try { Repreciar(); } catch (Exception e) { Registrar(e); }
            try { AnotarVela(bar); } catch (Exception e) { Registrar(e); }
        }

        /// <summary>
        /// Anota la vela que acaba de cerrar, con los niveles que estaban
        /// vigentes en ese momento.
        ///
        /// Se anota la ANTERIOR, no la que se esta formando: una vela a medio
        /// hacer tiene un volumen y un conteo de operaciones que todavia van a
        /// crecer, y compararlos contra velas completas seria comparar peras
        /// con medias peras.
        /// </summary>
        private void AnotarVela(int bar)
        {
            if (!AnotarCentinela) return;
            if (bar <= _barraCentinela) return;
            int cerrada = bar - 1;
            if (cerrada < 1) { _barraCentinela = bar; return; }
            _barraCentinela = bar;

            if (_centinela == null)
            {
                var instr = InstrumentInfo != null ? InstrumentInfo.Instrument : "x";
                var marco = ChartInfo != null && ChartInfo.ChartType != null
                          ? ChartInfo.ChartType + "-" + ChartInfo.TimeFrame : "x";
                _centinela = new Centinela(instr, marco);
            }

            IndicatorCandle c;
            try { c = GetCandle(cerrada); } catch { return; }
            if (c == null) return;

            double mp, mn, ze, a0, b0, sp;
            lock (_candado)
            {
                mp = _majorPos; mn = _majorNeg; ze = _zeroGamma;
                a0 = _mp0Ult; b0 = _mn0Ult; sp = _spotUsado;
            }
            var niv = new List<KeyValuePair<string, double>>
            {
                new KeyValuePair<string, double>("wall_pos", mp),
                new KeyValuePair<string, double>("wall_neg", mn),
                new KeyValuePair<string, double>("zero", double.IsNaN(ze) ? 0 : ze),
                new KeyValuePair<string, double>("dte0_pos", a0),
                new KeyValuePair<string, double>("dte0_neg", b0),
            };
            double[] pp;
            lock (_candado) pp = _picosUlt;
            if (pp != null)
                for (int i = 0; i < pp.Length && i < 8; i++)
                    niv.Add(new KeyValuePair<string, double>(
                        "dom" + i.ToString(CultureInfo.InvariantCulture), pp[i]));

            _centinela.Anotar(cerrada, c.LastTime != default(DateTime) ? c.LastTime : c.Time,
                (double)c.Open, (double)c.High, (double)c.Low, (double)c.Close,
                (double)c.Volume, (double)c.Ticks, (double)c.Delta, sp, niv);
        }

        /// <summary>Cada barrido de un agresor, tal como los agrupa ATAS.
        /// Corre en el hilo de datos: aca solo se apila y se sale.</summary>
        protected override void OnCumulativeTrade(CumulativeTrade trade)
        {
            if (!VerBigTrades) return;
            try
            {
                var ts = InstrumentInfo != null ? InstrumentInfo.TickSize : 0.25m;
                _libro.UmbralAutomatico = UmbralAuto;
                _libro.FactorUmbral = (decimal)Math.Max(1.5, (double)FactorUmbral);
                _libro.MinBarrido = (decimal)Math.Max(1.0, (double)MinContratos);
                _libro.MemoriaMin = Math.Max(1, MemoriaMin);
                _libro.MaxMostrados = Math.Max(1, MaxPuntos);
                _libro.ResolverUmbral();

                // CUANTO TARDA EL LIBRO EN LLEGAR.
                //
                // Se le viene diciendo al operador que el flujo del libro de
                // futuros esta en vivo mientras la cadena de opciones llega
                // 902 s tarde. Eso hay que MEDIRLO, no suponerlo: se guarda
                // la diferencia entre la hora del barrido y la hora en que
                // llego, y sale en el renglon de auditoria.
                try
                {
                    var th = trade.Time;
                    if (th != default(DateTime))
                    {
                        // ATAS entrega la hora del barrido SIN zona horaria, asi
                        // que restarle a ciegas la hora local daba tres horas de
                        // diferencia y la ventana de cordura descartaba todas las
                        // muestras: el renglon salia "sinmuestra" con el mercado
                        // abierto y los circulos dibujandose en pantalla. Se
                        // prueba contra las dos referencias y gana la que da la
                        // diferencia mas chica.
                        var lagLocal = (DateTime.Now - th).TotalMilliseconds;
                        var lagUtc = (DateTime.UtcNow - th).TotalMilliseconds;
                        var lag = Math.Abs(lagUtc) < Math.Abs(lagLocal) ? lagUtc : lagLocal;
                        if (lag > -60000 && lag < 600000)
                            lock (_atrasoDom)
                            {
                                _atrasoDom.Add(lag);
                                if (_atrasoDom.Count > 4000) _atrasoDom.RemoveRange(0, 2000);
                            }
                    }
                }
                catch { }

                _libro.Anotar(trade, ts > 0 ? ts : 0.25m);
            }
            catch (Exception e) { Registrar(e); }
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

        /// <summary>
        /// Arma una Cadena con lo que hay AHORA en el feed de Rithmic.
        ///
        /// Devuelve null si la cadena viva todavia no esta lista o no trajo
        /// nada usable. Eso no es un error: significa seguir con CBOE, que
        /// llega tarde pero llega, y avisarlo en pantalla.
        ///
        /// Los strikes salen en precio de FUTURO, asi que la base vale cero y
        /// no hay ninguna conversion que hacer. Ese es justamente el otro
        /// beneficio de esta fuente: se elimina el paso donde se metia el error
        /// sistematico de ~9 puntos si la base salia mal.
        /// </summary>
        private Cadena ArmarDesdeViva()
        {
            if (!UsarCadenaViva || !_viva.Activa) return null;

            // LA CADENA SE REARMA CADA POCOS SEGUNDOS, NO EN CADA TICK.
            //
            // Antes se rehacia entera en cada llamada -- o sea miles de veces
            // por minuto -- para nada: las puntas de las opciones no se mueven
            // a esa velocidad y la volatilidad implicita menos. Lo que si tiene
            // que ir en cada tick es el PRECIO, y eso pasa igual porque el
            // repricing corre despues con el precio del momento.
            //
            // Ademas resuelve una carrera de la auditoria: la foto en disco se
            // escribia cada 20 s mientras la cadena cambiaba en cada tick, asi
            // que el auditor NUNCA agarraba el mismo par y no podia comparar.
            // Ahora la foto y la cadena se sellan en el mismo instante.
            var ahoraC = DateTime.Now;
            if (_vivaCache != null &&
                (ahoraC - _vivaCacheHora).TotalSeconds < Math.Max(1, SegundosCadenaViva))
                return _vivaCache;

            List<CadenaViva.Fila> fs;
            try { fs = _viva.Instantanea(); } catch { return null; }
            if (fs == null || fs.Count == 0) return null;

            var dias = fs.Select(f => Math.Round(f.Dias, 4)).Distinct().OrderBy(d => d).ToList();
            var idx = new Dictionary<double, int>();
            for (int i = 0; i < dias.Count; i++) idx[dias[i]] = i;

            var porClave = new Dictionary<(double, int), Fila>();
            foreach (var f in fs)
            {
                if (f.OI <= 0 || f.IV <= 0) continue;
                int v = idx[Math.Round(f.Dias, 4)];
                var clave = (f.K, v);
                if (!porClave.TryGetValue(clave, out var fila))
                    fila = new Fila { K = f.K, V = v };
                if (f.EsCall) { fila.OiC = f.OI; fila.IvC = f.IV; fila.VolC = f.VolumenHoy; }
                else          { fila.OiP = f.OI; fila.IvP = f.IV; fila.VolP = f.VolumenHoy; }
                porClave[clave] = fila;
            }
            // UNA CADENA FLACA NO SIRVE Y NO PUEDE TAPAR AL RESPALDO.
            //
            // Con pocos strikes el perfil no tiene forma: el zero gamma no
            // cruza, los muros son cualquier cosa y la interpolacion de picos
            // no tiene vecinos. Si no llega a un minimo se devuelve null, que
            // hace caer a CBOE -- 15 min tarde pero completo -- en vez de
            // dibujar un mapa hecho con cuatro puntos.
            const int MinStrikes = 12;
            int strikesUtiles = porClave.Values
                .Where(v => v.OiC > 0 && v.OiP > 0 && v.IvC > 0 && v.IvP > 0)
                .Select(v => v.K).Distinct().Count();
            if (strikesUtiles < MinStrikes)
            {
                _vivaFlaca = strikesUtiles;
                return null;
            }
            _vivaFlaca = -1;
            if (porClave.Count == 0) return null;

            // VOLCADO PARA PODER AUDITARLA.
            //
            // Igual que con la cadena de CBOE: si no se puede rehacer la cuenta
            // desde afuera con EXACTAMENTE los mismos numeros, el resultado no
            // es auditable. Se escribe cada tanto, no en cada tick.
            var selloC = ahoraC.ToString("yyyy-MM-dd_HH:mm:ss", CultureInfo.InvariantCulture);
            // Las filas se guardan y la foto se escribe en el MISMO momento en
            // que se escribe el renglon de auditoria, no aca: si se vuelcan en
            // distintos instantes el auditor nunca agarra el mismo par y no
            // puede comparar. Se probo y daban once segundos de diferencia.
            lock (_candado) _vivaFilasCache = fs;

            var salida = new Cadena
            {
                Ts = selloC,
                SpotIdx = _viva.Futuro,
                Dias = dias.ToArray(),
                Filas = porClave.Values.ToList(),
                Base = 0, BaseCruda = 0, BaseConfiable = true,
                Contrato = "ES (Rithmic)",
                EdadMin = 0,
                EsFuturo = true,
                Fuente = "Rithmic EN VIVO",
            };
            _vivaCache = salida;
            _vivaCacheHora = ahoraC;
            return salida;
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
                if (c != null && c.Filas.Count > 0)
                {
                    _c = c; _error = "";
                    // LA FOTO EXACTA DE LO QUE SE USO.
                    //
                    // Hay un radar.py --vigilar regenerando el feed cada 60 s.
                    // Sin esta copia, el auditor compara contra un archivo que
                    // ya cambio y acusa un error de calculo que no existe: la
                    // primera corrida marco 0,81 B de diferencia por eso.
                    try
                    {
                        var dst = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "ATAS", "pythiagex-cadena-usada-" + Raiz() + ".json");
                        File.WriteAllText(dst, txt);
                    }
                    catch { }
                }
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
                    BaseCruda = Num(r, "base_cruda") ?? 0,
                    BaseErrorTicks = Num(r, "base_error_ticks") ?? 0,
                    BaseUltimaBuena = Num(r, "base_ultima_buena") ?? 0,
                    BaseUltimaBuenaEdad = Num(r, "base_ultima_buena_edad_min") ?? 0,
                    Contrato = Txt(r, "contrato"),
                    EdadMin = Num(r, "edad_min") ?? 0,
                    UltimoTrade = Txt(cd, "ultimo_trade"),
                    HorizonteCadena = Num(cd, "horizonte_dias") ?? double.NaN,
                };
                // con que horizonte se armaron las zonas del radar; si el feed
                // es viejo y no lo trae, queda NaN y el panel no afirma nada
                _horizonteZonas = Num(r, "horizonte_zonas_dias") ?? double.NaN;

                // las zonas dominantes viajan en el mismo archivo, ya calculadas
                lock (_zonas)
                {
                    _zonas.Clear();
                    foreach (var clave in new[] { "dominantes", "zonas" })
                        if (r.TryGetProperty(clave, out var zz) && zz.ValueKind == JsonValueKind.Array)
                            foreach (var z in zz.EnumerateArray())
                            {
                                var fut = Num(z, "fut");
                                var d1 = Num(z, "desde");
                                var d2 = Num(z, "hasta");

                                // EL ARCHIVO YA NO TRAE LOS VALORES CONVERTIDOS.
                                //
                                // Vienen en null -- 'fut', 'desde' y 'hasta' --
                                // y los buenos estan en 'idx', 'idx_desde' e
                                // 'idx_hasta', que son precios de INDICE.
                                //
                                // Con el descarte de abajo se caian TODAS las
                                // zonas, una por una y en silencio: la lista
                                // quedaba vacia y las franjas desaparecian del
                                // grafico sin ningun error a la vista. El
                                // operador lo noto mirando la pantalla, no yo
                                // mirando el codigo.
                                //
                                // Se convierten con la base, igual que todos los
                                // demas niveles del proyecto. Un nivel de indice
                                // dibujado en el futuro esta ~21 puntos corrido.
                                if (fut == null) { var i0 = Num(z, "idx"); if (i0 != null) fut = i0 + c.Base; }
                                if (d1 == null) { var i1 = Num(z, "idx_desde"); if (i1 != null) d1 = i1 + c.Base; }
                                if (d2 == null) { var i2 = Num(z, "idx_hasta"); if (i2 != null) d2 = i2 + c.Base; }

                                if (fut == null || d1 == null || d2 == null) continue;
                                if (_zonas.Any(x => Math.Abs(x.Fut - fut.Value) < 0.01)) continue;
                                _zonas.Add(new ZonaDom
                                {
                                    Fut = fut.Value, Desde = d1.Value, Hasta = d2.Value,
                                    Idx = Num(z, "idx") ?? double.NaN,
                                    GexPy = (Num(z, "gex_M") ?? double.NaN) * 1e6,
                                    Caracter = Txt(z, "caracter"), Lado = Txt(z, "lado"),
                                    Criollo = Txt(z, "criollo"),
                                    Incentivo = Num(z, "incentivo") ?? 0,
                                    Relevante = z.TryGetProperty("relevante", out var rv)
                                                && rv.ValueKind == JsonValueKind.True,
                                });
                            }
                }

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
        private const double MULT = 100.0;   // multiplicador de opciones de INDICE (SPX, NDX)

        /// <summary>
        /// EL PISO DEL TIEMPO AL VENCIMIENTO: UN MINUTO.
        ///
        /// Hace falta un piso porque con T tendiendo a cero la gamma tiende a
        /// infinito y un 0DTE a punto de liquidar se comeria todo el mapa.
        ///
        /// Estaba en 0,02 dias, o sea 29 minutos, y eso recortaba el muro justo
        /// en la media hora en que mas aprieta. Medido sobre las fotos
        /// archivadas de la rueda del 3 de septiembre, con el 0DTE VERDADERO:
        ///
        ///   09:34 a 15:13  ->  diferencia 0 % en todas las lecturas
        ///   15:44 (cierre) ->  subestimabamos un 12 %
        ///
        /// Y el muro NO se movio en ninguna fila: 7750 contra 7750 en la
        /// ultima. El piso cambia el TAMANO, nunca la ubicacion.
        ///
        /// El efecto real es otro y es mas interesante: con menos tiempo la
        /// gamma no crece pareja, se CONCENTRA. Los strikes al dinero se
        /// disparan y los de los costados se apagan. Por eso el total sube
        /// solo 12 % mientras el pico se afila muchisimo mas. Es el poste
        /// contra el terraplen, medido.
        /// </summary>
        private const double PISO_DIAS = 1.0 / 1440.0;

        /// <summary>
        /// El multiplicador del contrato, que NO es 100 para opciones sobre futuros.
        ///
        /// Las opciones de indice (SPX, NDX) valen 100 dolares por punto. Las
        /// opciones sobre futuros de CME valen lo mismo que su futuro:
        /// ES 50, NQ 20, RTY 50. Aplicarles 100 infla el GEX en dolares por 2
        /// en ES y por 5 en NQ.
        ///
        /// QUE CAMBIA Y QUE NO. Es una constante que multiplica a TODOS los
        /// strikes por igual, asi que no mueve ningun nivel: no cambia cual es
        /// el strike de maximo GEX, ni donde la suma cruza el cero, ni la forma
        /// del perfil, ni los picos. Lo unico que estaba mal era el NUMERO EN
        /// DOLARES del titular -- que es justamente el error que este proyecto
        /// le encontro a los tableros ajenos, asi que con mas razon hay que
        /// arreglarlo en el propio.
        ///
        /// Se usa la raiz de tamano completo porque la cadena que se suscribe
        /// es la del contrato grande: un grafico de MES lee opciones de ES.
        /// </summary>
        private double Multiplicador()
        {
            if (!_esFuturo) return MULT;
            switch (Raiz())
            {
                case "NQ":  return 20.0;
                case "RTY": return 50.0;
                default:    return 50.0;   // ES
            }
        }

        private static double Fi(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);

        /// <summary>Gamma de Black-Scholes. Devuelve 0 si los datos no dan.</summary>
        /// <summary>
        /// Un numero de gamma en dolares, abreviado y legible de un vistazo.
        ///
        /// El chip vive contra el eje y tiene lugar para tres o cuatro
        /// caracteres: "3B", "2,1B", "840M". Con el numero entero -- 3.052.117.884 --
        /// no se lee nada y ademas no aporta: nadie decide por el septimo digito.
        /// </summary>
        private static string Magnitud(double v)
        {
            v = Math.Abs(v);
            var es = CultureInfo.GetCultureInfo("es-AR");
            if (v >= 1e12) return (v / 1e12).ToString("0.#", es) + "T";
            if (v >= 1e9) return (v / 1e9).ToString("0.#", es) + "B";
            if (v >= 1e6) return (v / 1e6).ToString("0", es) + "M";
            if (v >= 1e5) return (v / 1e6).ToString("0.0", es) + "M";
            if (v > 0) return (v / 1e3).ToString("0", es) + "K";
            return "";
        }

        private static double GammaBs(double S, double K, double T, double iv, double r)
        {
            if (S <= 0 || K <= 0 || T <= 0 || iv <= 0) return 0;
            var v = iv * Math.Sqrt(T);
            if (v <= 0) return 0;
            var d1 = (Math.Log(S / K) + (r + 0.5 * iv * iv) * T) / v;
            return Fi(d1) / (S * v);
        }

        /// <summary>GEX de un strike a un precio dado, sumando vencimientos.</summary>
        /// <summary>GEX de un strike sobre el VOLUMEN del dia en vez del
        /// interes abierto.
        ///
        /// Por que las dos cuentas y no una: el interes abierto es de ayer, y
        /// el volumen es de hoy. GEXbot publica las dos y el tablero del
        /// producto original las muestra en dos bloques separados, justamente
        /// porque dicen cosas distintas. El de interes abierto es el mapa
        /// heredado; el de volumen es donde se esta jugando la sesion.
        ///
        /// OJO: el volumen por strike de CBOE llega con quince minutos de
        /// atraso, medido. El de interes abierto no pierde nada porque ya era
        /// de ayer. Por eso el bloque de volumen va marcado en el tablero.
        /// </summary>
        private double GexVolStrike(Fila f, double S, double T, double r)
        {
            var gC = GammaBs(S, f.K, T, f.IvC, r);
            var gP = GammaBs(S, f.K, T, f.IvP, r);
            return (gC * f.VolC - gP * f.VolP) * Multiplicador() * S * S * 0.01;
        }

        /// <summary>
        /// Igual que GexStrike pero con el modelo y el multiplicador EXPLICITOS,
        /// para poder evaluar una cadena que no es la que esta en uso.
        ///
        /// Hace falta porque el zero gamma se calcula sobre la cadena ancha
        /// aunque el perfil venga de la viva, y las dos no comparten ni modelo
        /// ni multiplicador: una es sobre futuro y la otra sobre indice.
        /// </summary>
        private double GexStrikeCon(Fila f, double S, double T, double r, bool esFut, double mult)
        {
            var gC = esFut ? Black76.Gamma(S, f.K, T, f.IvC) : GammaBs(S, f.K, T, f.IvC, r);
            var gP = esFut ? Black76.Gamma(S, f.K, T, f.IvP) : GammaBs(S, f.K, T, f.IvP, r);
            return (gC * f.OiC - gP * f.OiP) * mult * S * S * 0.01;
        }

        /// <summary>
        /// EL CRUCE POR CERO SOBRE UNA CADENA CUALQUIERA.
        ///
        /// Se ancla la grilla al precio de AHORA, no al spot que traia la
        /// cadena: asi lo unico atrasado es el interes abierto y la volatilidad
        /// -- que es lo que de verdad se consolida de noche -- y el precio es
        /// del segundo. Devuelve el cruce en el espacio de precios de ESA
        /// cadena; el que llama le suma la base si hace falta.
        /// </summary>
        private double CruceCero(Cadena cc, double futuro, double baseCc, double r)
        {
            if (cc == null || cc.Filas == null || cc.Filas.Count == 0 || cc.Dias == null)
                return double.NaN;
            bool esFut = cc.EsFuturo;
            double mult = esFut ? Multiplicador() : MULT;
            double S = esFut ? futuro : futuro - baseCc;
            if (S <= 0) return double.NaN;

            double lo = S * 0.97, hi = S * 1.03;
            const int pasos = 60;
            double ant = double.NaN, xAnt = 0;
            for (int i = 0; i <= pasos; i++)
            {
                double x = lo + (hi - lo) * i / pasos;
                double t = 0;
                foreach (var f in cc.Filas)
                {
                    if (f.V < 0 || f.V >= cc.Dias.Length) continue;
                    var dias = cc.Dias[f.V];
                    if (dias > DiasMax) continue;
                    t += GexStrikeCon(f, x, Math.Max(dias, PISO_DIAS) / 365.0, r, esFut, mult);
                }
                if (!double.IsNaN(ant) && ((ant < 0 && t >= 0) || (ant > 0 && t <= 0)))
                    return (t != ant) ? xAnt + (x - xAnt) * (-ant) / (t - ant) : x;
                ant = t; xAnt = x;
            }
            return double.NaN;
        }

        private double GexStrike(Fila f, double S, double T, double r)
        {
            // EL MODELO CORRECTO SEGUN DE DONDE VINO LA CADENA.
            //
            // Las opciones de ES son opciones sobre un FUTURO y les corresponde
            // Black-76. Las de SPX son sobre el indice al contado y les
            // corresponde Black-Scholes. La diferencia a 7 dias es de decimas
            // de por mil, pero teniendo el modelo exacto no hay motivo para
            // usar el aproximado.
            var gC = _esFuturo ? Black76.Gamma(S, f.K, T, f.IvC) : GammaBs(S, f.K, T, f.IvC, r);
            var gP = _esFuturo ? Black76.Gamma(S, f.K, T, f.IvP) : GammaBs(S, f.K, T, f.IvP, r);
            // Convencion estandar de la industria: +1 para calls, -1 para puts.
            // Es una ASUNCION sobre de que lado quedo la mesa, no un dato
            // medido. Cuando el flujo dominante se da vuelta, el signo miente.
            return (gC * f.OiC - gP * f.OiP) * Multiplicador() * S * S * 0.01;
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
            // LA VIVA MANDA CUANDO ESTA.
            //
            // El operador scalpea en minutos y segundos y dijo que 902 s de
            // retraso no le sirven. Tiene razon: un dato que llega tarde obliga
            // a confiar en que nada cambio en el medio, y esa confianza no se
            // puede auditar. Cuando Rithmic esta entregando, se usa Rithmic.
            // UN SOLO LIBRO, SIEMPRE EL MISMO.
            //
            // Antes esto decia "la viva manda cuando esta", y alternaba entre
            // dos MERCADOS DISTINTOS varias veces por hora: opciones de SPX
            // (CBOE) y opciones sobre el futuro de ES (Rithmic).
            //
            // LO MEDIDO, 1140 lecturas de una rueda, con el PRECIO como control:
            //   sin cambiar de libro   el precio salta 0,25 y los muros 0,00
            //   AL CAMBIAR DE LIBRO    el precio salta 0,25 y los muros 33,10
            // El mercado no se movia: el salto era puro cambio de libro.
            //
            // Y no se parecen en nada. Mismo instante, misma ventana de strikes:
            // el call wall difiere 92,51 puntos, el neto tiene SIGNOS OPUESTOS,
            // de los 6 strikes mas fuertes de cada uno coinciden CERO, y SPX
            // tiene 7,8 veces mas interes abierto.
            //
            // El operador eligio SPX, y el motivo es solido: sus mesas cubren
            // con futuros de ES, asi que es esa gamma la que empuja el precio
            // que el opera. El precio de referencia igual es del segundo --
            // la cadena se reprecia al futuro de ahora -- lo unico atrasado es
            // el interes abierto, que es de ayer para todo el mundo.
            var c = LibroViva ? (ArmarDesdeViva() ?? _c) : (_c ?? ArmarDesdeViva());
            if (c == null || c.Filas.Count == 0) return;
            _esFuturo = c.EsFuturo;
            _fuente = c.Fuente;
            _cUsada = c;

            double baseUsada;
            if (c.EsFuturo)
            {
                // los strikes YA vienen en precio de futuro: no hay conversion
                baseUsada = 0;
                _baseOrigen = "no hace falta (cadena de futuros)";
            }
            else if (BaseManual != 0m)
            {
                baseUsada = (double)BaseManual;
                _baseOrigen = "manual";
            }
            else if (c.BaseConfiable && c.Base != 0)
            {
                baseUsada = c.Base;
                _baseBuena = c.Base;
                _baseBuenaHora = DateTime.UtcNow;
                _baseOrigen = "medida";
            }
            else if (!double.IsNaN(_baseBuena) &&
                     (DateTime.UtcNow - _baseBuenaHora).TotalMinutes <= MinutosBaseVieja)
            {
                baseUsada = _baseBuena;
                _baseOrigen = string.Format(CultureInfo.InvariantCulture, "de hace {0:0} min",
                    (DateTime.UtcNow - _baseBuenaHora).TotalMinutes);
            }
            else if (c.BaseUltimaBuena != 0 && c.BaseUltimaBuenaEdad <= MinutosBaseVieja * 12)
            {
                // La ultima medicion CONFIABLE, guardada en disco por radar.py.
                // Va antes que la cruda a proposito: una base que paso el
                // control hace un rato es mejor estimacion que una que lo
                // reprueba ahora. Fuera del horario de opciones de SPX la base
                // no se puede medir en absoluto, y este es el unico respaldo
                // honesto que queda.
                baseUsada = c.BaseUltimaBuena;
                _baseOrigen = string.Format(CultureInfo.InvariantCulture,
                    "medida hace {0:0} min", c.BaseUltimaBuenaEdad);
            }
            else if (UsarBaseCruda && c.BaseCruda != 0)
            {
                baseUsada = c.BaseCruda;
                _baseOrigen = string.Format(CultureInfo.InvariantCulture,
                    "CRUDA, {0:0} ticks de error", c.BaseErrorTicks);
            }
            else
            {
                baseUsada = double.NaN;
                _baseOrigen = "sin base";
            }

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

            // EL HORIZONTE SE ADAPTA A LO QUE HAY.
            //
            // Bug encontrado auditando MNQ: la cadena viva aflojaba el
            // horizonte para traer el unico vencimiento que existe (el
            // trimestral a 15 dias), y ACA se seguia aplicando el corte de 7 y
            // se tiraban TODAS las filas. El renglon de auditoria lo mostraba
            // sin lugar a dudas: cadenafilas=59 y strikes=0. Una parte del
            // codigo relajo el criterio y la otra no se entero.
            //
            // El horizonte efectivo nunca es menor que el vencimiento mas
            // cercano disponible: si hay 0DTE, el corte de 7 dias manda igual
            // que siempre; si lo unico que hay esta a 15, se usan esos.
            double horizonte = DiasMax;
            if (c.Dias != null && c.Dias.Length > 0)
            {
                double masCerca = double.MaxValue;
                foreach (var d in c.Dias) if (d >= 0 && d < masCerca) masCerca = d;
                if (masCerca != double.MaxValue && masCerca > horizonte) horizonte = masCerca;
            }

            // CADA PERFIL CON SU PROPIO VENCIMIENTO.
            //
            // Asi lo ofrece el producto original: su dialogo se llama
            // "Configurar Perfil" y adentro tiene Metrica y VENCIMIENTO
            // (0DTE / Latest / Next) por perfil, no uno global.
            //
            // Para que sirve de verdad: el mapa del 0DTE y el de la cadena
            // entera son estructuralmente distintos. El de hoy es el que
            // aprieta para scalpear -- gamma concentrada, muros filosos -- y el
            // completo es el de fondo, mas plano y mas estable. Poder mirarlos
            // a la vez, uno de cada lado, es tener el corto y el largo sin
            // cambiar de pantalla.
            bool PasaVenc(double dias, int modo) =>
                modo <= 0 ? dias < 1.0            // solo lo que vence hoy
              : modo == 1 ? dias <= horizonte     // la ventana elegida
              : true;                             // todo lo que haya

            foreach (var f in c.Filas)
            {
                if (f.V < 0 || f.V >= c.Dias.Length) continue;
                var dias = c.Dias[f.V];
                bool enIzq = PasaVenc(dias, VencIzq);
                bool enDer = PasaVenc(dias, VencDer);
                if (!enIzq && !enDer) continue;
                // El plazo nunca baja de media hora: con T tendiendo a cero la
                // gamma explota y un 0DTE a punto de liquidar se comeria todo
                // el mapa con un numero que no significa nada.
                var T = Math.Max(dias, PISO_DIAS) / 365.0;

                var g = GexStrike(f, S, T, r);
                var gUp = GexStrike(f, Sup, T, r);
                var gv = GexVolStrike(f, S, T, r);
                if (g == 0 && gUp == 0 && gv == 0) continue;

                if (!porStrike.TryGetValue(f.K, out var n))
                    n = new Nivel { K = f.K, Gex = 0, GexVol = 0, Acel = 0 };
                // el perfil de gamma (izquierda) y el de convexidad (derecha)
                // se llenan cada uno con SU vencimiento
                if (enIzq)
                {
                    n.Gex += g; n.GexVol += gv; n.VolTot += f.VolC + f.VolP;
                    n.Oi += f.OiC + f.OiP;
                    if (dias < 1.0) n.Gex0 += g;   // lo que vence hoy
                    if (Math.Abs(g) > n.GexDom) { n.GexDom = Math.Abs(g); n.DiasDom = dias; }
                }
                if (enDer) { n.Acel += (gUp - g); }
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
                        t += GexStrike(f, x, Math.Max(dias, PISO_DIAS) / 365.0, r);
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

            // EL MISMO CRUCE, PERO SOBRE EL VOLUMEN DE HOY.
            //
            // Identico al de arriba y a proposito: misma grilla, mismo paso,
            // misma interpolacion en el cruce. Lo unico que cambia es que suma
            // GexVolStrike en vez de GexStrike, o sea que pondera por los
            // contratos operados HOY en vez de por el interes abierto de ayer.
            //
            // El campo _zeroVol estaba declarado desde hace rato y nunca se
            // llenaba; el compilador lo venia avisando.
            double zeroVol = double.NaN;
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
                        t += GexVolStrike(f, x, Math.Max(dias, PISO_DIAS) / 365.0, r);
                    }
                    if (!double.IsNaN(ant) && ((ant < 0 && t >= 0) || (ant > 0 && t <= 0)))
                    {
                        zeroVol = (t != ant) ? xAnt + (x - xAnt) * (-ant) / (t - ant) : x;
                        break;
                    }
                    ant = t; xAnt = x;
                }
            }
            lock (_candado) _zeroVol = zeroVol;

            // HIPOTESIS 3: DONDE EL GAMMA CAMBIA MAS RAPIDO.
            //
            // Se recorre la misma grilla de +/-3 % repreciando toda la cadena y
            // se busca el tramo donde la suma total pega el mayor salto de un
            // paso al siguiente. Ese es el precio donde la cobertura de las
            // mesas cambia mas de golpe. Es continuo por construccion: sale de
            // la grilla, no de la rejilla de strikes.
            double maxChange = double.NaN;
            {
                double lo = S * 0.97, hi = S * 1.03;
                int pasos = 60;
                double ant = double.NaN, xAnt = 0, mejor = 0;
                for (int i = 0; i <= pasos; i++)
                {
                    double x = lo + (hi - lo) * i / pasos;
                    double t = 0;
                    foreach (var f in c.Filas)
                    {
                        if (f.V < 0 || f.V >= c.Dias.Length) continue;
                        var dias = c.Dias[f.V];
                        if (dias > DiasMax) continue;
                        t += GexStrike(f, x, Math.Max(dias, PISO_DIAS) / 365.0, r);
                    }
                    if (!double.IsNaN(ant))
                    {
                        double cambio = Math.Abs(t - ant);
                        if (cambio > mejor) { mejor = cambio; maxChange = (x + xAnt) / 2.0; }
                    }
                    ant = t; xAnt = x;
                }
            }

            // EL ZERO GAMMA SOBRE LA CADENA ANCHA (apagado por defecto).
            //
            // Queda disponible pero NO se usa: mezclar el zero de un libro con
            // los muros de otro produce un mapa que no describe a ningun
            // mercado. Se probo y el operador lo vio en pantalla al toque.
            // Los dos libros -- SPX y ES -- discrepan en TODO: el call wall
            // difiere 92 puntos y de los 6 strikes mas fuertes coinciden cero.
            double zeroAncho = double.NaN;
            int strikesAncho = 0;
            if (ZeroDeCadenaAncha && _c != null && !ReferenceEquals(_c, c)
                && _c.Filas != null && _c.Filas.Count > c.Filas.Count)
            {
                double bA = _c.EsFuturo ? 0
                          : (_c.BaseConfiable ? _c.Base
                             : (_baseBuena != 0 ? _baseBuena : _c.BaseCruda));
                double zi = CruceCero(_c, futuro, bA, r);
                if (!double.IsNaN(zi))
                {
                    zeroAncho = zi + (_c.EsFuturo ? 0 : bA);
                    var vistos = new HashSet<double>();
                    foreach (var f in _c.Filas) vistos.Add(f.K);
                    strikesAncho = vistos.Count;
                }
            }
            lock (_candado) { _zeroAnchoUlt = zeroAncho; _strikesAnchoUlt = strikesAncho; }

            lock (_candado) { _maxChangeUlt = maxChange; }

            // EL CALL WALL VA ARRIBA DEL PRECIO Y EL PUT WALL ABAJO.
            //
            // Antes se tomaba el maximo y el minimo GLOBALES de la cadena, sin
            // mirar de que lado del precio caian. Se midio sobre 1777 renglones
            // del registro propio: el "call wall" quedo POR DEBAJO del precio
            // 217 veces (12,2 %) y el "put wall" POR ENCIMA 167 veces (9,4 %).
            // Un caso real de NQ: precio 29.516 y put wall dibujado en 29.600,
            // ochenta y cuatro puntos arriba. Eso es dibujar un piso por encima
            // del techo, y en scalping se opera contra el nivel equivocado.
            //
            // La definicion estandar tiene la condicion del lado justamente por
            // esto: el call wall es la resistencia de arriba y el put wall el
            // soporte de abajo. Un cumulo de gamma positiva POR DEBAJO del
            // precio existe y es informacion, pero no es una resistencia y no
            // se puede etiquetar como tal.
            //
            // Si de un lado no hay ningun strike, se cae al global antes que
            // no dar nada: es mejor un nivel con la etiqueta puesta que un
            // hueco silencioso.
            double mp = 0, mn = 0, mpGlobal = 0, mnGlobal = 0;

            // EL RIVAL DE CADA MURO.
            //
            // Un argmax entre dos strikes casi empatados es una moneda al aire
            // presentada como dato: con el 2do al 89 % del 1ro, un movimiento
            // minimo del precio da vuelta cual gana y el muro "salta" 50 o 75
            // puntos sin que el mercado haya hecho nada. Paso el 2026-08-31
            // (7750 -> 7800) y otra vez el 2026-09-06 (7750 <-> 7825 toda la
            // noche, registrado por el centinela). NivelesGamma ya lo marcaba
            // como DISPUTADO; este indicador no, y era el que estaba en
            // pantalla. Aca se calcula el segundo de cada lado y su peso
            // relativo; si pasa el umbral, el rival se publica y se dibuja.
            double mpRival = double.NaN, mnRival = double.NaN, mpRatio = 0, mnRatio = 0;
            if (perfil.Count > 0)
            {
                mpGlobal = perfil.Aggregate((a, b) => a.Gex >= b.Gex ? a : b).K;
                mnGlobal = perfil.Aggregate((a, b) => a.Gex <= b.Gex ? a : b).K;

                var arriba = perfil.Where(p => p.K > S).ToList();
                var abajo  = perfil.Where(p => p.K < S).ToList();
                mp = arriba.Count > 0
                     ? arriba.Aggregate((a, b) => a.Gex >= b.Gex ? a : b).K
                     : mpGlobal;
                mn = abajo.Count > 0
                     ? abajo.Aggregate((a, b) => a.Gex <= b.Gex ? a : b).K
                     : mnGlobal;

                double umbral = Math.Max(0.5, Math.Min(0.99, UmbralDisputa / 100.0));
                if (arriba.Count > 1)
                {
                    var lider = arriba.Aggregate((a, b) => a.Gex >= b.Gex ? a : b);
                    var segundo = arriba.Where(q => q.K != lider.K)
                                        .Aggregate((a, b) => a.Gex >= b.Gex ? a : b);
                    if (lider.Gex > 0 && segundo.Gex > 0)
                    {
                        mpRatio = segundo.Gex / lider.Gex;
                        if (mpRatio >= umbral) mpRival = segundo.K;
                    }
                }
                if (abajo.Count > 1)
                {
                    var lider = abajo.Aggregate((a, b) => a.Gex <= b.Gex ? a : b);
                    var segundo = abajo.Where(q => q.K != lider.K)
                                       .Aggregate((a, b) => a.Gex <= b.Gex ? a : b);
                    if (lider.Gex < 0 && segundo.Gex < 0)
                    {
                        mnRatio = segundo.Gex / lider.Gex;   // los dos negativos: sale positivo
                        if (mnRatio >= umbral) mnRival = segundo.K;
                    }
                }
            }
            lock (_candado) { _mpGlobal = mpGlobal; _mnGlobal = mnGlobal; }

            // LA GAMMA DE ESTE INDICADOR EN CADA ZONA DEL RADAR.
            //
            // El radar (Python) suma la gamma de cada strike hasta 45 dias; este
            // indicador la corta en DiasMax (7 por defecto). Medido el
            // 2026-09-06 desde la cadena cruda de CBOE: el 7700 de SPX daba
            // -1.350 M a 7 dias y -2.844 M a 45, y la diferencia era casi toda
            // el vencimiento del 30 de septiembre (21.716 puts). Por eso el
            // radar marcaba 7700 como "acelerador" mientras el muro de este
            // indicador caia en 7675: no es un error de calculo de ninguno de
            // los dos, son dos horizontes. Aca se guarda la cifra propia de
            // cada zona para publicarla junto a la del radar en el AUDIT.
            lock (_zonas)
                foreach (var z in _zonas)
                {
                    if (double.IsNaN(z.Idx)) continue;
                    double g = 0; bool hay = false;
                    foreach (var q in perfil)
                        if (Math.Abs(q.K - z.Idx) < 0.01) { g += q.Gex; hay = true; }
                    z.GexCs = hay ? g : double.NaN;
                }

            // LOS PICOS DEL PERFIL, INTERPOLADOS.
            //
            // POR QUE INTERPOLADOS Y NO EL STRIKE PELADO. El operador dijo que
            // las dominantes dibujadas se veian "muy lineales" y que en el
            // producto real se ven dispersas. Se midio sobre tres capturas
            // suyas: entre el 59 % y el 89 % de los guiones tienen una altura
            // UNICA, y el ajuste a rejilla da un desvio de 0,19 a 0,30 donde
            // cero seria rejilla perfecta. O sea que el nivel de ellos es una
            // cantidad CONTINUA, no un strike.
            //
            // Devolver el strike pelado solo puede pararse en la rejilla de 5
            // puntos, y por eso se veia una escalera. El pico real del perfil
            // cae ENTRE strikes: se ajusta una parabola por el maximo y sus dos
            // vecinos y se toma el vertice. Se mueve con cada tick, que es lo
            // que se ve en los videos.
            // LO DE ARRIBA QUEDO REFUTADO. Se deja como opcion, no como camino.
            //
            // Aquella medicion se hizo con deteccion de color sobre capturas
            // donde las VELAS NARANJAS se contaban como marcas del indicador.
            // Estaba contaminada y ya se retracto.
            //
            // La medicion buena: 2443 cuadros de dos videos, con control de
            // paneo (en 816 pares de cuadros NUNCA se movieron todas las barras
            // lo mismo, asi que el movimiento es dato y no camara). El producto
            // real dibuja UNA BARRA POR STRIKE sobre una rejilla fija -- la
            // separacion medida entre barras vecinas es de 28 px y se repite --
            // y no elige ganadores. Lo que se mueve es el LARGO de cada barra.
            //
            // Y nuestro propio registro dice por que habia que cambiarlo: 731
            // muestras de ES en 399 minutos seguidos, con las dominantes
            // clavadas en el mismo lugar entre el 65 % y el 92 % de las
            // muestras y saltos de hasta 117 puntos cuando cambiaba el ganador.
            // Eso no es una escalera, es una raya con precipicios, y el
            // precipicio es el sintoma de estar eligiendo.
            //
            // Sobre la rejilla no hay ganador que cambiar, asi que no hay salto.
            var picos = new List<(double Fut, double Peso)>();
            if (perfil.Count < 3) lock (_candado) _picosUlt = null;   // no dejar los viejos colgados

            // MODO 0: EL CENTRO DE GRAVEDAD. Un PROMEDIO, no una eleccion.
            //
            // ---------------------------------------------------------------
            // NO ES EL MODO POR DEFECTO, Y ESTE COMENTARIO EXPLICA POR QUE NO.
            //
            // Lo propuse yo porque medi que unas lineas de nivel de un video
            // del producto real ondulan (284 lineas, ninguna plana). Esa
            // medicion esta bien hecha. El problema es otro: NUNCA IDENTIFIQUE
            // QUE NIVEL ERAN ESAS LINEAS. Uno de los videos se llama "Max
            // Change", que no es un muro de gamma.
            //
            // Las dos fuentes que trajo el operador coinciden entre si: la zona
            // dominante es EL STRIKE donde |GEX| es mas alto. Es una ELECCION
            // sobre strikes. Un nivel definido asi se dibuja plano porque es
            // plano, y eso no es un defecto.
            //
            // Ademas se verifico por un camino independiente, recalculando en
            // Python desde la cadena cruda: las dos formulas que circulan
            // (cruda OIxGamma, y en dolares xMULTxS^2/100) dan LOS MISMOS ocho
            // strikes en el MISMO orden, y coinciden con lo que sacaba el modo
            // 1. Solo cambian las unidades del titular.
            //
            // Queda disponible por si algun dia se identifica cual es la linea
            // que ondula. Hasta entonces, el estandar manda.
            // ---------------------------------------------------------------
            //
            // POR QUE ESTE Y NO LOS OTROS DOS. Se midieron 284 lineas de nivel
            // del producto real, cada una por separado y sobre el amarillo, que
            // ninguna vela de ese grafico tiene y por lo tanto no se contamina:
            // NINGUNA es plana. Cero de 284. Cada una toma unas 13 alturas
            // distintas en la misma pantalla, con 15 px de recorrido y
            // escalones de 2,4 px.
            //
            // Los tres comportamientos salen de una sola diferencia:
            //   - elegir un STRIKE     -> no se mueve nunca (raya perfecta)
            //   - elegir el PICO       -> clavado, y salta un intervalo entero
            //                             cuando cambia el strike ganador (se
            //                             midieron saltos de 117 puntos)
            //   - PROMEDIAR            -> se mueve un poco siempre que cambia
            //                             cualquier peso, y no salta nunca
            //
            // Un promedio no tiene ganador que cambiar. Por eso ondula en vez
            // de pegar acantilados, y por eso es este.
            if (perfil.Count >= 3 && ModoDominante == 0)
            {
                double piso = mx * Math.Max(0.0, Math.Min(0.95, MinFuerzaDominante / 100.0));
                double rad = (double)Math.Max(1m, RadioCentroide);
                for (int i = 1; i < perfil.Count - 1; i++)
                {
                    double a = Math.Abs(perfil[i - 1].Gex);
                    double b2 = Math.Abs(perfil[i].Gex);
                    double c2 = Math.Abs(perfil[i + 1].Gex);
                    if (b2 <= a || b2 <= c2) continue;      // el cumulo se ubica con el maximo local
                    if (b2 < piso) continue;

                    // ...pero el nivel NO es ese maximo: es el promedio de
                    // precio ponderado por gamma en su entorno. Se usa n.Fut,
                    // la misma conversion con la que se dibuja el perfil.
                    double sw = 0, sx = 0;
                    foreach (var n in perfil)
                    {
                        if (Math.Abs(n.Fut - perfil[i].Fut) > rad) continue;
                        double w = Math.Abs(n.Gex);
                        if (w <= 0) continue;
                        sw += w; sx += w * n.Fut;
                    }
                    if (sw <= 0) continue;
                    picos.Add((sx / sw, b2));
                }
                picos.Sort((u, v) => v.Peso.CompareTo(u.Peso));
                double sepC = (double)Math.Max(0m, SeparacionPuntos);
                if (sepC > 0)
                {
                    var fl = new List<(double Fut, double Peso)>();
                    foreach (var q in picos)
                        if (!fl.Any(z => Math.Abs(z.Fut - q.Fut) < sepC)) fl.Add(q);
                    picos = fl;
                }
                if (picos.Count > Math.Max(1, MaxDominantes))
                    picos.RemoveRange(Math.Max(1, MaxDominantes),
                                      picos.Count - Math.Max(1, MaxDominantes));
                lock (_candado) _picosUlt = picos.Select(q => q.Fut).ToArray();
            }
            else if (perfil.Count >= 3 && ModoDominante == 1)
            {
                // Se usa n.Fut, la MISMA conversion a futuro con la que se
                // dibujan las barras del perfil, para que una dominante no
                // pueda quedar corrida respecto de su propia barra.
                double piso = mx * Math.Max(0.0, Math.Min(0.95, MinFuerzaDominante / 100.0));
                foreach (var n in perfil)
                {
                    double p = Math.Abs(n.Gex);
                    if (p <= 0 || p < piso) continue;
                    picos.Add((n.Fut, p));
                }
                picos.Sort((u, v) => v.Peso.CompareTo(u.Peso));
                if (picos.Count > Math.Max(1, MaxDominantes))
                    picos.RemoveRange(Math.Max(1, MaxDominantes),
                                      picos.Count - Math.Max(1, MaxDominantes));
                lock (_candado) _picosUlt = picos.Select(p => p.Fut).ToArray();
            }
            else if (perfil.Count >= 3)
            {
                for (int i = 1; i < perfil.Count - 1; i++)
                {
                    double a = Math.Abs(perfil[i - 1].Gex);
                    double b2 = Math.Abs(perfil[i].Gex);
                    double c2 = Math.Abs(perfil[i + 1].Gex);
                    if (b2 <= a || b2 <= c2) continue;          // no es maximo local
                    if (b2 < mx * 0.12) continue;               // ruido de fondo

                    double den = a - 2 * b2 + c2;
                    double delta = Math.Abs(den) > 1e-12 ? 0.5 * (a - c2) / den : 0.0;
                    if (delta > 0.5) delta = 0.5;
                    if (delta < -0.5) delta = -0.5;
                    // paso local de la cadena, medido y no supuesto
                    double paso = (perfil[i + 1].K - perfil[i - 1].K) / 2.0;
                    double kInt = perfil[i].K + delta * paso;
                    picos.Add((double.IsNaN(baseUsada) ? kInt : kInt + baseUsada, b2));
                }
                // EL CONJUNTO SE DECIDE ACA, EN PUNTOS, NO AL DIBUJAR EN PIXELES.
                //
                // BUG QUE ESTO ARREGLA. El descarte de picos pegados se hacia
                // al dibujar, comparando PIXELES. Dos dominantes separadas por
                // 5 puntos quedan a 2 px con el grafico alejado y a 20 px con
                // el grafico acercado: en un caso se descartaba una y en el
                // otro se dibujaban las dos. Por eso CAMBIABA LA CANTIDAD al
                // cambiar el tamano o al correr la pantalla.
                //
                // Decidiendolo en puntos de precio, el conjunto es el mismo
                // siempre. El zoom cambia donde se ven, nunca cuantas hay.
                picos.Sort((u, v) => v.Peso.CompareTo(u.Peso));
                double sepPts = (double)Math.Max(0m, SeparacionPuntos);
                if (sepPts > 0)
                {
                    var filtrados = new List<(double Fut, double Peso)>();
                    foreach (var p in picos)
                        if (!filtrados.Any(q => Math.Abs(q.Fut - p.Fut) < sepPts))
                            filtrados.Add(p);
                    picos = filtrados;
                }
                if (picos.Count > Math.Max(1, MaxDominantes))
                    picos.RemoveRange(Math.Max(1, MaxDominantes),
                                      picos.Count - Math.Max(1, MaxDominantes));
                lock (_candado) _picosUlt = picos.Select(p => p.Fut).ToArray();
            }

            // Guardar la foto de cada strike para poder dibujar la estela.
            // Se poda a 35 minutos: mas atras no lo pide ninguna ventana y el
            // diccionario crece sin techo con el grafico abierto todo el dia.
            var ahora = Ahora();
            var corte = ahora.AddMinutes(-35);
            double mcK = double.NaN, mcV = 0;
            lock (_estela)
            {
                foreach (var n in perfil)
                {
                    if (!_estela.TryGetValue(n.K, out var h))
                        _estela[n.K] = h = new List<KeyValuePair<DateTime, double>>();
                    h.Add(new KeyValuePair<DateTime, double>(ahora, n.Gex));
                    if (h.Count > 4 && h[0].Key < corte)
                        h.RemoveAll(x => x.Key < corte);

                    // el mayor cambio contra la ventana mas larga disponible
                    var viejoVal = Anterior(h, ahora, Ventanas[Ventanas.Length - 1]);
                    if (!double.IsNaN(viejoVal))
                    {
                        var d = Math.Abs(n.Gex - viejoVal);
                        if (d > mcV) { mcV = d; mcK = n.K; }
                    }
                }
                // ANOTAR LA PELOTITA.
                //
                // Medido sobre el video que el propio autor dedico al Max
                // Change: de 1135 puntos grises detectados, 351 -- el 31 % --
                // caen en el CUERPO del grafico, lejos de los dos bordes, y
                // aparecen en grupos. O sea que no son una estela sobre la
                // barra: llevan coordenada de TIEMPO. Se plantan donde y
                // cuando se detecto el cambio, y se acumulan.
                //
                // La primera version las dibujaba sobre el eje de la barra y
                // por eso el operador no las reconocia.
                if (!double.IsNaN(mcK) && mcV > 0 &&
                    (ahora - _ultimaPelotita).TotalSeconds >= Math.Max(5, CadaSegundos))
                {
                    _ultimaPelotita = ahora;
                    var nucleo = perfil.FirstOrDefault(p => Math.Abs(p.K - mcK) < 0.01);
                    if (nucleo.K != 0)
                    {
                        if (mcV > _mayorDelta) _mayorDelta = mcV;
                        lock (_pelotitas)
                        {
                            _pelotitas.Add(new Pelotita
                            {
                                Hora = ahora, Strike = nucleo.K, Fut = nucleo.Fut,
                                Delta = mcV, Fuerza = Math.Min(1.0, mcV / _mayorDelta),
                            });
                            var tope = Math.Max(20, MaxPelotitas);
                            if (_pelotitas.Count > tope)
                                _pelotitas.RemoveRange(0, _pelotitas.Count - tope);
                        }
                    }
                }

                // MAX CHANGE POR VENTANA, igual que el tablero del original:
                // 1, 5, 10, 15 y 30 minutos, cada uno con su strike y su delta.
                for (int vi = 0; vi < VentanasTablero.Length; vi++)
                {
                    double bK = double.NaN, bV = 0;
                    foreach (var n2 in perfil)
                    {
                        if (!_estela.TryGetValue(n2.K, out var hh)) continue;
                        var v0 = Anterior(hh, ahora, VentanasTablero[vi]);
                        if (double.IsNaN(v0)) continue;
                        var dd = Math.Abs(n2.Gex - v0);
                        if (dd > bV) { bV = dd; bK = n2.K; }
                    }
                    _cambios[vi] = new FilaCambio
                    {
                        Strike = double.IsNaN(bK) ? 0 : (double.IsNaN(baseUsada) ? bK : bK + baseUsada),
                        Delta = bV,
                        Hay = !double.IsNaN(bK),
                    };
                }

                if (_estela.Count > 600)
                {
                    var sobran = _estela.Keys.Where(k => !perfil.Any(p => p.K == k)).ToList();
                    foreach (var k in sobran) _estela.Remove(k);
                }
            }

            double netoVol = 0, mpv = 0, mnv = 0;
            // LA VOLATILIDAD AL DINERO Y EL MOVIMIENTO ESPERADO.
            //
            // Es lo que el propio mercado de opciones esta cobrando por el
            // rango de HOY, y no lo teniamos. Sale del strike mas cercano al
            // precio en el vencimiento mas corto: un desvio estandar es
            //     S x IV x raiz(T)
            // que para un 0DTE a media rueda son unos pocos puntos y da un
            // marco honesto de cuanto se espera que se mueva.
            //
            // Tambien se publica el GEX TOTAL en magnitud, para que el neto
            // tenga escala: un neto de 36 M no significa nada si no se sabe si
            // el total es 100 M o 8.000 M. En el segundo caso el mercado esta
            // en el filo y se da vuelta con nada.
            {
                double mejorD = double.MaxValue, ivC = 0, ivP = 0, diasAtm = 0;
                foreach (var f in c.Filas)
                {
                    if (f.V < 0 || f.V >= c.Dias.Length) continue;
                    var dd = c.Dias[f.V];
                    if (dd > DiasMax) continue;
                    double dist = Math.Abs(f.K - S) + dd * 1000.0;   // primero el mas corto
                    if (dist < mejorD && (f.IvC > 0 || f.IvP > 0))
                    { mejorD = dist; ivC = f.IvC; ivP = f.IvP; diasAtm = dd; }
                }
                double iv = (ivC > 0 && ivP > 0) ? (ivC + ivP) / 2.0 : Math.Max(ivC, ivP);
                double tt = Math.Max(diasAtm, PISO_DIAS) / 365.0;
                double em = (iv > 0 && tt > 0) ? S * iv * Math.Sqrt(tt) : 0;
                double tg = 0;
                foreach (var n5 in perfil) tg += Math.Abs(n5.Gex);
                lock (_candado) { _ivAtmUlt = iv; _movEspUlt = em; _gexTotalUlt = tg; }
            }

            lock (_candado) _futsUlt = perfil.Select(p => p.Fut).ToArray();

            if (perfil.Count > 0)
            {
                foreach (var n3 in perfil) netoVol += n3.GexVol;
                mpv = perfil.Aggregate((a, b) => a.GexVol >= b.GexVol ? a : b).K;
                mnv = perfil.Aggregate((a, b) => a.GexVol <= b.GexVol ? a : b).K;
                if (!double.IsNaN(baseUsada)) { mpv += baseUsada; mnv += baseUsada; }
            }

            // PUBLICARLOS ACA, NO CIEN LINEAS MAS ABAJO.
            //
            // Estaban calculados aca pero se publicaban recien despues de
            // escribir el renglon de auditoria, asi que la auditoria leia el
            // valor de la pasada ANTERIOR -- y en la primera pasada leia cero.
            // Salio a la luz sola: la hipotesis 1 aparecia como majorposvol=0
            // con netgexvol=47,5, y un maximo no puede ser cero si la suma no
            // lo es. El dato estaba bien; lo que estaba mal era cuando lo miraba.
            lock (_candado) { _netGexVol = netoVol; _majorPosVol = mpv; _majorNegVol = mnv; }

            // ANOTAR LA VELA. Un punto por vela al nivel de ese momento: eso
            // es lo que forma las bandas de puntitos de las capturas.
            try
            {
                int b = Math.Max(0, CurrentBar - 1);
                lock (_porBarra)
                {
                    // Los picos interpolados de ESTE momento. Antes se guardaban
                    // los nucleos de las zonas, que son strikes pelados: solo
                    // podian caer en la rejilla de 5 puntos y por eso la banda
                    // salia como una escalera en vez de ondular.
                    double[] dd = null, ii = null;
                    if (picos.Count > 0)
                    {
                        dd = picos.Select(p => p.Fut).ToArray();
                        ii = picos.Select(p => p.Peso).ToArray();
                    }
                    _porBarra[b] = new Marca
                    {
                        Hora = ahora,
                        MajorPos = mp, MajorNeg = mn,
                        Zero = !double.IsNaN(zeroAncho) ? zeroAncho
                             : (double.IsNaN(zero) ? 0
                                : (double.IsNaN(baseUsada) ? zero : zero + baseUsada)),
                        MajorPosVol = mpv, MajorNegVol = mnv,
                        MaxChange = double.IsNaN(maxChange) ? 0
                                  : (double.IsNaN(baseUsada) ? maxChange : maxChange + baseUsada),
                        ZeroVol = double.IsNaN(zeroVol) ? 0
                                : (double.IsNaN(baseUsada) ? zeroVol : zeroVol + baseUsada),
                        Doms = dd, Incs = ii,
                        Hay = true,
                    };
                    // no dejar crecer sin techo con el grafico abierto todo el dia
                    if (_porBarra.Count > 4000)
                    {
                        var viejas = _porBarra.Keys.Where(k => k < b - 3000).ToList();
                        foreach (var k in viejas) _porBarra.Remove(k);
                    }
                }
            }
            catch { }

            // AUDITORIA. Cada tanto se vuelca lo calculado para poder
            // contrastarlo, con los mismos insumos, contra el motor de Python.
            // Si los dos no dan lo mismo, el indicador esta mintiendo y hay
            // que saberlo antes de operar con el, no despues.
            try
            {
                if ((ahora - _ultimoVolcado).TotalSeconds >= 60)
                {
                    _ultimoVolcado = ahora;
                    // LA FOTO, SELLADA EN EL MISMO INSTANTE QUE EL RENGLON.
                    VolcarCadenaViva();
                    // el volumen vivo por strike, para los circulos del borde:
                    // se refresca aca, una vez por minuto, no en cada render
                    try { if (_viva.Activa) lock (_candado) _volVivoCache = _viva.VolumenPorStrike(); } catch { }
                    GuardarHistoria();
                    Registrar2(string.Format(CultureInfo.InvariantCulture,
                        "AUDIT spot_idx={0:F4} base={1:F4} origen=" + _baseOrigen.Replace(" ", "_") + " strikes={2} visibles=" + _visiblesUlt + " " +
                        "zero={3:F4} majorpos={4:F4} majorneg={5:F4} netgex={6:F6} netgexvol={7:F6} diasmax={8} " +
                        // el regimen tal como lo va a mostrar el panel, para poder
                        // auditar el rotulo contra estos mismos numeros (los dos en indice)
                        "regimen=" + ((!double.IsNaN(zero) && S > zero) ? "positivo" : (double.IsNaN(zero) ? "sindato" : "negativo")) + " " +
                        "cadenafilas={9} cadenats={10}" + AtrasoDom() + Picos() + Flujo() + Disputa() + ZonasAudit(),
                        S, baseUsada, perfil.Count,
                        double.IsNaN(zero) ? 0 : zero, mp, mn, neto / 1e9, netoVol / 1e9, DiasMax,
                        c.Filas.Count, (c.Ts ?? "").Replace(" ", "_")));
                }
            }
            catch { }

            _baseResuelta = double.IsNaN(baseUsada) ? (object)null : (object)baseUsada;

            // EL REGIMEN SE DECIDE ACA, NO EN EL DIBUJO.
            //
            // En este punto S y zero estan los DOS en puntos del indice; recien
            // despues al zero se le suma la base para poder dibujarlo. Comparar
            // el spot del indice contra el zero ya convertido a futuro son unos
            // nueve puntos de diferencia, y da vuelta el regimen justo cuando
            // el precio esta cerca del cruce -- que es cuando mas importa.
            bool positiva = !double.IsNaN(zero) && S > zero;

            lock (_candado)
            {
                _gammaPositiva = positiva;
                _netGexVol = netoVol; _majorPosVol = mpv; _majorNegVol = mnv;
                _maxCambioStrike = mcK;
                _maxCambioValor = mcV;
                _perfil = perfil;
                _netGex = neto; _maxGex = mx; _maxAcel = mxA;
                _zeroGamma = double.IsNaN(zero) ? double.NaN
                           : (double.IsNaN(baseUsada) ? zero : zero + baseUsada);
                // si hay zero de cadena ancha, ESE es el que se dibuja
                if (!double.IsNaN(zeroAncho)) _zeroGamma = zeroAncho;
                _majorPos = double.IsNaN(baseUsada) ? mp : mp + baseUsada;
                _majorNeg = double.IsNaN(baseUsada) ? mn : mn + baseUsada;
                _mpRival = double.IsNaN(mpRival) ? double.NaN
                         : (double.IsNaN(baseUsada) ? mpRival : mpRival + baseUsada);
                _mnRival = double.IsNaN(mnRival) ? double.NaN
                         : (double.IsNaN(baseUsada) ? mnRival : mnRival + baseUsada);
                _mpRatio = mpRatio; _mnRatio = mnRatio;
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

        private bool _latido;

        private void Pintar(RenderContext g)
        {
            if (ChartInfo == null) return;
            try { g.SetSmoothingMode(OFT.Rendering.Context.RenderSmoothingModes.AntiAlias); } catch { }
            _etiquetasUsadas.Clear();
            _rectsUsados.Clear();
            try { PrepararRender(); } catch (Exception e) { Registrar(e); }
            MapearHistoria();
            var area = ChartArea;
            if (!_latido)
            {
                _latido = true;
                Registrar2(string.Format(CultureInfo.InvariantCulture,
                    "primer render: area={0},{1} {2}x{3}  cadena={4}  panel={5}",
                    area.Left, area.Top, area.Width, area.Height,
                    _c == null ? "null" : _c.Filas.Count.ToString(), Panel));
            }
            int x0 = area.Left, x1 = area.Right - Math.Max(0, MargenEje);

            List<Nivel> perfil; double mx, mxA, zero, mp, mn, neto, spot;
            bool positiva; double mpRiv, mnRiv;
            lock (_candado)
            {
                perfil = _perfil; mx = _maxGex; mxA = _maxAcel;
                zero = _zeroGamma; mp = _majorPos; mn = _majorNeg;
                neto = _netGex; spot = _spotUsado;
                positiva = _gammaPositiva; mpRiv = _mpRival; mnRiv = _mnRival;
            }

            if (VerTablero) Tablero(g, area);
            // despues del tablero, porque se apoya encima de su rectangulo
            if (VerEscalera) { try { Escalera(g, area); } catch (Exception e) { Registrar(e); } }
            if (VerCinta) Cinta(g, x0, area, perfil.Count, neto, spot);
            if (perfil.Count == 0 || mx <= 0) return;

            var cont = ChartInfo.PriceChartContainer;
            try { LlenarYsNiveles(cont); } catch (Exception e) { Registrar(e); }
            var c = _cUsada ?? _c;   // la que de verdad se uso, no la de CBOE por defecto
            var br = _baseResuelta;
            var baseUsada = br == null ? double.NaN : (double)br;

            // Si no hay base confiable no se dibuja NADA sobre el grafico. Un
            // nivel de SPX puesto crudo sobre el ES esta unos veinte puntos
            // corrido, y eso es una perdida sistematica en cada operacion.
            if (double.IsNaN(baseUsada)) return;

            int alto = AltoBarra > 0 ? AltoBarra : AltoAutomatico(cont, perfil);
            int ancho = Math.Max(20, AnchoBarra);

            // LA ESCALA SE NORMALIZA CONTRA EL ENTORNO DEL PRECIO, NO CONTRA
            // LO QUE SE VE.
            //
            // BUG QUE ESTO ARREGLA. Antes se normalizaba contra los strikes
            // VISIBLES. Al desplazar el grafico cambiaba el conjunto visible,
            // cambiaba el maximo y por lo tanto CAMBIABA EL LARGO DE TODAS LAS
            // BARRAS: la misma barra media distinto segun donde estuviera la
            // pantalla, y algunas desaparecian. Eso no es un parpadeo, es que
            // el largo dejaba de significar lo mismo de un momento a otro, y
            // sobre eso no se puede decidir nada.
            //
            // Tampoco sirve el maximo GLOBAL: un strike enorme y lejano se
            // lleva todo el ancho y los de al lado del precio quedan en un
            // pixel, que fue el problema original.
            //
            // La solucion es normalizar contra una ventana centrada en el
            // PRECIO, que no se mueve cuando uno desplaza la pantalla. Da el
            // mismo contraste cerca del dinero y es estable.
            double pxNorm = 0;
            try { pxNorm = (double)GetCandle(Math.Max(0, CurrentBar - 1)).Close; } catch { }
            double radioNorm = (double)Math.Max(20m, RadioNormalizar);
            double mxV = 0, mxAV = 0;
            int visibles = 0;
            foreach (var n in perfil)
            {
                if (pxNorm > 0 && Math.Abs(n.Fut - pxNorm) <= radioNorm)
                {
                    mxV = Math.Max(mxV, Math.Abs(n.Gex));
                    mxAV = Math.Max(mxAV, Math.Abs(n.Acel));
                }
                int yy;
                try { yy = cont.GetYByPrice((decimal)n.Fut, false); }
                catch { continue; }
                if (yy >= area.Top - 6 && yy <= area.Bottom + 6) visibles++;
            }
            // si no hubo nada cerca del precio se cae al maximo global, que es
            // estable aunque comprima: mejor comprimido que cambiante
            if (mxV <= 0) foreach (var n in perfil) mxV = Math.Max(mxV, Math.Abs(n.Gex));
            if (mxAV <= 0) foreach (var n in perfil) mxAV = Math.Max(mxAV, Math.Abs(n.Acel));
            if (mxV > 0) mx = mxV;
            if (mxAV > 0) mxA = mxAV;
            _visiblesUlt = visibles;

            foreach (var n in perfil)
            {
                int y;
                try { y = cont.GetYByPrice((decimal)n.Fut, false); }
                catch { continue; }
                if (y < area.Top - 6 || y > area.Bottom + 6) continue;

                // EL PERFIL ES CONTEXTO, NO PROTAGONISTA.
                //
                // Antes se dibujaba a 215 de opacidad y 150 px de ancho: se
                // comia un tercio del grafico y ahogaba las velas. En las
                // capturas del producto real el perfil vive en el borde y el
                // centro queda limpio para el precio. La intensidad ademas
                // sigue al tamano: las barras chicas casi no se ven y las
                // grandes saltan, que es lo que hay que leer de un vistazo.
                if (VerGamma && Math.Abs(n.Gex) > 0)
                {
                    double f = Math.Abs(n.Gex) / mx;
                    int w = Math.Max(1, (int)(f * ancho));
                    var col = n.Gex >= 0 ? ColPos : ColNeg;
                    int al = (int)(OpacidadPerfil * 2.55 * (0.45 + 0.55 * f));
                    g.FillRectangle(Color.FromArgb(Math.Min(255, Math.Max(12, al)), col),
                        new Rectangle(x0, y - alto / 2, w, alto));
                    if (VerEstela) Estela(g, n.K, mx, ancho, x0, y, true, alto);
                }
                if (VerAcel && mxA > 0 && Math.Abs(n.Acel) > 0)
                {
                    double f = Math.Abs(n.Acel) / mxA;
                    int w = Math.Max(1, (int)(f * ancho));
                    var col = n.Acel >= 0 ? ColAcelPos : ColAcelNeg;
                    int al = (int)(OpacidadPerfil * 2.55 * (0.45 + 0.55 * f));
                    g.FillRectangle(Color.FromArgb(Math.Min(255, Math.Max(12, al)), col),
                        new Rectangle(x1 - w, y - alto / 2, w, alto));
                    if (VerEstela) Estela(g, n.K, mxA, ancho, x1, y, false, alto);
                }
            }

            if (VerPuntosDominantes) PuntosDominantes(g, cont, x0, x1);
            if (VerPelotitas) Pelotitas(g, cont, x0, x1);
            if (VerDominantes) Zonas(g, cont, x0, x1);
            if (VerBigTrades) Puntos(g, cont, x0, x1);
            if (VerVolumenVivo) VolumenVivo(g, cont, x1);

            // LAS LINEAS NO CRUZAN EL PERFIL.
            //
            // Cruzarlo mezcla dos lecturas distintas -- cuanta gamma hay en ese
            // strike y donde esta el nivel -- y el ojo tiene que separarlas
            // solo. Empiezan despues del perfil y terminan antes del de la
            // derecha, asi cada cosa ocupa su franja.
            int xl0 = x0 + (VerGamma ? ancho + 6 : 2);
            int xl1 = x1 - (VerAcel ? ancho + 6 : 2);
            if (xl1 - xl0 < 60) { xl0 = x0 + 2; xl1 = x1 - 2; }
            // LOS CERCANOS PRIMERO, PARA QUE LOS MAJORS LES GANEN EL LUGAR.
            //
            // POR QUE HACEN FALTA. Los majors son el argmax GLOBAL de la
            // cadena, y eso deja un hueco enorme entre el precio y el primer
            // nivel dibujado. Medido con el futuro en 7752: el major positive
            // apuntaba a 7800 -- a 48 puntos -- con 661 M, mientras 7760, a
            // OCHO puntos, tenia 637 M. O sea el 96 % de la gamma del mayor,
            // invisible, y era el unico que el precio iba a tocar en el proximo
            // rato. Para scalpear eso es exactamente al reves.
            //
            // El signo dice que hace la mesa en ese nivel: gamma positiva la
            // obliga a comprar caidas y vender subas (frena), negativa a lo
            // contrario (acelera).
            if (VerNivelesCercanos && perfil.Count > 0 && spot > 0)
            {
                // POR LADO, NO EN BLOQUE. Antes se tomaban los N mas pesados
                // dentro del radio, y como abajo del precio suele haber mas
                // gamma, arriba no quedaba ninguno: el operador no tenia
                // "siguiente arriba". Ahora la mitad de los cercanos va por
                // encima y la otra mitad por debajo. Llevan el chip completo
                // (detalle = true): son exactamente los peldanos que se miran.
                var cerca = SeleccionarCercanos(perfil, _pxRender, mp, mn, mx);
                foreach (var nv in cerca)
                {
                    var col = nv.Gex >= 0 ? ColCercanoPos : ColCercanoNeg;
                    Linea(g, cont, xl0, xl1, nv.Fut, col,
                          nv.Gex >= 0 ? "freno" : "acel", false, spot, x1, true, true);
                }
            }

            // DONDE SE ESTA REESCRIBIENDO EL MAPA HOY.
            //
            // Todos los tableros de GEX -- este incluido -- dibujan sobre el
            // interes abierto, que la OCC consolida de NOCHE. O sea que el mapa
            // es siempre el de ayer, y eso vale igual para GEXbot y para
            // cualquiera que pague lo que pague: no hay interes abierto
            // intradia, no existe.
            //
            // Lo que si existe es el VOLUMEN de hoy, contrato por contrato, y
            // llega en vivo por Rithmic. Un strike con mucho volumen hoy es un
            // strike donde se estan armando o cerrando posiciones AHORA: ahi el
            // mapa de manana va a ser distinto del de hoy.
            //
            // No reemplaza al mapa de gamma. Lo complementa con lo unico que al
            // mapa le falta: el presente.
            if (VerFlujoCboe && perfil.Count > 0)
            {
                var flujo = perfil
                    .Where(nv => nv.VolTot >= Math.Max(1, MinContratosFlujo))
                    .OrderByDescending(nv => nv.VolTot)
                    .Take(Math.Max(1, CuantosFlujo))
                    .ToList();
                foreach (var nv in flujo)
                    Linea(g, cont, xl0, xl1, nv.Fut, ColFlujo,
                          "flujo " + ((int)nv.VolTot).ToString(CultureInfo.InvariantCulture),
                          false, spot, x1, true, false);
            }

            // LA ANOTACION VA ADENTRO DEL CHIP QUE YA EXISTE.
            //
            // El operador pidio ver el tamano y el vencimiento sin recargar la
            // pantalla. No hace falta ningun dibujo nuevo: la etiqueta recibe
            // el nombre como texto, asi que se le agrega ahi. Queda
            // "+wall 7.832  1,2B 0D" en el mismo chip de siempre.
            string Tam(double precioFut)
            {
                if (!AnotarTamano) return "";
                // Nivel es un struct: no admite null, se usa una bandera
                Nivel n = default;
                bool hay = false;
                double mejor = double.MaxValue;
                foreach (var q in perfil)
                {
                    double dd = Math.Abs(q.Fut - precioFut);
                    if (dd < mejor) { mejor = dd; n = q; hay = true; }
                }
                if (!hay || mejor > 3.0) return "";
                double g0 = Math.Abs(n.Gex0), gt = Math.Abs(n.Gex);

                // EL VENCIMIENTO, SIEMPRE, Y CUANTO DE ESO VENCE HOY.
                //
                // El operador scalpea intradia: saber si el nivel que tiene
                // enfrente vence hoy o el viernes le cambia la decision. Si
                // solo se marcara el 0DTE, la ausencia de marca seria ambigua
                // -- no se sabria si es de otro dia o si no se pudo calcular.
                //
                // Y cuando el nivel es MIXTO se separan los dos numeros, porque
                // no son lo mismo: la parte de hoy se evapora al cierre y la
                // otra sigue manana. Un muro de "2,1B 7DTE +0,9B 0D" al cierre
                // pasa a valer 1,2B y se corre.
                string dte;
                if (n.GexDom <= 0) dte = "";
                else if (n.DiasDom < 1.0) dte = " 0DTE";
                else dte = " " + Math.Round(n.DiasDom).ToString("0", CultureInfo.InvariantCulture) + "DTE";

                string tot = Magnitud(gt);
                // la porcion de hoy, aparte, solo cuando el nivel es mixto:
                // si TODO vence hoy el "0DTE" de arriba ya lo dice
                string hoy = "";
                if (g0 > 0 && n.DiasDom >= 1.0 && g0 / Math.Max(1e-9, gt) >= 0.10)
                    hoy = " +" + Magnitud(g0) + " 0D";

                return (string.IsNullOrEmpty(tot) ? "" : "  " + tot) + dte + hoy;
            }

            if (VerNiveles0DTE)
            {
                double a0, b0;
                lock (_candado) { a0 = _mp0Ult; b0 = _mn0Ult; }
                if (a0 > 0) Linea(g, cont, xl0, xl1, a0, ColPos, "0DTE +", false, spot, x1, true);
                if (b0 > 0) Linea(g, cont, xl0, xl1, b0, ColNeg, "0DTE -", false, spot, x1, true);
            }

            if (VerLineas)
            {
                // con la etiqueta detallada el tamano y el vencimiento van en
                // la segunda linea; sin ella, en el nombre como antes
                Linea(g, cont, xl0, xl1, mp, ColPos, "+wall" + (EtiquetaDetallada ? "" : Tam(mp)), false, spot, x1);
                Linea(g, cont, xl0, xl1, mn, ColNeg, "-wall" + (EtiquetaDetallada ? "" : Tam(mn)), false, spot, x1);
                Linea(g, cont, xl0, xl1, zero, ColZero, "zero " + DiasMax + "d", true, spot, x1, false, false);
                // el rival del muro disputado, fino: es el otro candidato, no
                // un nivel mas. Ver el comentario donde se calcula.
                if (!double.IsNaN(mpRiv)) Linea(g, cont, xl0, xl1, mpRiv, ColPos, "+wall? disputado", false, spot, x1, true);
                if (!double.IsNaN(mnRiv)) Linea(g, cont, xl0, xl1, mnRiv, ColNeg, "-wall? disputado", false, spot, x1, true);
            }

            // la banda del peldano donde esta parado el precio
            if (VerBandaAca) { try { BandaAca(g, cont, xl0, xl1); } catch (Exception e) { Registrar(e); } }

            // LA FRANJA DE REGIMEN.
            //
            // Es lo primero que hay que saber para scalpear -- si el mercado
            // esta en rango o en expansion -- y hasta ahora habia que leerlo
            // en un renglon de texto. Un color arriba se lee sin leer.
            if (VerFranjaRegimen && spot > 0)
            {
                // sin zero gamma la franja va gris: no se sabe, y pintarla de
                // rojo seria decir "expansion" sin tener con que
                if (double.IsNaN(zero) || zero <= 0)
                    g.FillRectangle(Color.FromArgb(45, ColAviso),
                                    new Rectangle(x0, area.Top, x1 - x0, 3));
                else
                {
                    // NO comparar spot con zero aca: spot esta en INDICE y zero
                    // ya lleva la base sumada (precio de FUTURO). Con base 6,17
                    // la franja salia roja con el precio 2 puntos ARRIBA del
                    // zero. _gammaPositiva se calcula con los dos en indice.
                    bool pos = positiva;
                    var cr = pos ? ColPos : ColNeg;
                    g.FillRectangle(Color.FromArgb(pos ? 42 : 58, cr),
                                    new Rectangle(x0, area.Top, x1 - x0, 3));
                }
            }

            // Los titulos de las mitades van APAGADOS por defecto: chocaban con
            // la marca de agua de ATAS ("Trading Platform by Rithmic") y no
            // aportan nada que no diga el color. El que quiera verlos los
            // prende, y ahi salen mas abajo para no pisarla.
            if (VerTitulos)
            {
                var f8 = new RenderFont("Arial", 7.5f);
                if (VerGamma)
                    g.DrawString("EXPOSICION GAMMA", f8, Color.FromArgb(95, ColTexto),
                                 x0 + 4, area.Top + 22);
                if (VerAcel)
                {
                    var m = g.MeasureString("ACELERACION", f8);
                    g.DrawString("ACELERACION", f8, Color.FromArgb(95, ColTexto),
                                 x1 - m.Width - 4, area.Top + 22);
                }
            }
        }

        /// <summary>Las zonas dominantes, como bandas al fondo.</summary>
        /// <summary>
        /// LAS ZONAS, COMO CORCHETES AL BORDE Y NO COMO MANCHAS.
        ///
        /// Antes cada zona era un rectangulo translucido que cruzaba TODO el
        /// grafico. Con tres o cuatro zonas activas eso pinta media pantalla y
        /// las velas quedan atras de un vidrio de color: exactamente lo que el
        /// operador llamo "poco profesional visualmente".
        ///
        /// Una zona es un RANGO DE PRECIO, o sea informacion del eje vertical.
        /// No necesita ancho: le alcanza con un corchete al costado, como los
        /// que se usan para marcar tramos en un eje. Asi se ve donde empieza y
        /// donde termina, se distingue freno de acelerador por color, y el
        /// centro del grafico queda libre para el precio, que es lo que se
        /// mira para operar.
        /// </summary>
        private void Zonas(RenderContext g, IChartContainer cont, int x0, int x1)
        {
            List<ZonaDom> zs;
            lock (_zonas) zs = new List<ZonaDom>(_zonas);
            if (zs.Count == 0) return;

            double incMax = 0;
            foreach (var z in zs) if (z.Incentivo > incMax) incMax = z.Incentivo;
            if (incMax <= 0) incMax = 1;

            // el corchete vive pegado al perfil, en su propia franja
            int xb = x0 + (VerGamma ? Math.Max(20, AnchoBarra) + 10 : 6);
            const int gruesoCorchete = 3, patita = 6;

            foreach (var z in zs)
            {
                if (!z.Relevante && !VerZonasDebiles) continue;
                int ya, yb;
                try
                {
                    ya = cont.GetYByPrice((decimal)Math.Max(z.Desde, z.Hasta), false);
                    yb = cont.GetYByPrice((decimal)Math.Min(z.Desde, z.Hasta), false);
                }
                catch { continue; }
                if (yb < ChartArea.Top || ya > ChartArea.Bottom) continue;
                ya = Math.Max(ya, ChartArea.Top);
                yb = Math.Min(yb, ChartArea.Bottom);
                int alt = Math.Max(3, yb - ya);

                var col = z.Caracter == "freno" ? ColPos : ColNeg;
                double fz = Math.Max(0.25, Math.Min(1.0, z.Incentivo / incMax));
                int alfa = (int)(120 + 120 * fz);

                // el cuerpo del corchete
                g.FillRectangle(Color.FromArgb(alfa, col), new Rectangle(xb, ya, gruesoCorchete, alt));
                // las dos patitas, que cierran el tramo
                g.FillRectangle(Color.FromArgb(alfa, col), new Rectangle(xb, ya, patita, 1));
                g.FillRectangle(Color.FromArgb(alfa, col), new Rectangle(xb, yb - 1, patita, 1));

                // y un relleno apenas insinuado, para que se lea como banda sin
                // tapar nada: 10 % sobre fondo oscuro son 25 de 255
                var alfaR = Math.Max(0, Math.Min(255, OpacidadZona * 255 / 100));
                if (alfaR > 0)
                    g.FillRectangle(Color.FromArgb(z.Relevante ? alfaR : alfaR / 2, col),
                        new Rectangle(xb + patita, ya, Math.Max(1, x1 - xb - patita), alt));
            }
        }

        /// <summary>Los BigTrades: un punto por barrido, acumulandose.
        ///
        /// El tamano sale de la raiz del volumen contra el mayor visto, no del
        /// volumen crudo: con escala lineal un solo barrido enorme deja a
        /// todos los demas convertidos en un pixel y se pierde la textura del
        /// flujo, que es justamente lo que hay que mirar.
        /// </summary>
        private void Puntos(RenderContext g, IChartContainer cont, int x0, int x1)
        {
            List<Libro.Barrido> bs;
            try { bs = _libro.Todos(Math.Max(1, MemoriaMin)); }
            catch { return; }
            if (bs == null || bs.Count == 0) return;

            // a que barra corresponde cada barrido; se recalcula solo cuando
            // aparecen barras nuevas
            if (_ultimoMapeo != CurrentBar)
            {
                _ultimoMapeo = CurrentBar;
                var pend = bs.OrderByDescending(b => b.Hora).ToList();
                int i = Math.Max(0, CurrentBar - 1), k = 0;
                while (i >= 0 && k < pend.Count)
                {
                    IndicatorCandle c;
                    try { c = GetCandle(i); } catch { break; }
                    while (k < pend.Count && pend[k].Hora >= c.Time) { pend[k].Barra = i; k++; }
                    i--;
                }
            }

            // SOLO LOS MAS GRANDES SE DIBUJAN.
            //
            // El umbral de captura queda bajo a proposito para no perder
            // material, pero dibujar todo lo capturado llena la pantalla de
            // ruido: en MES la mediana es de 1 contrato, asi que entraban
            // barridos de 8, 9 y 10. En las capturas del operador los
            // circulos dicen 291, 313, 245, 722 -- tres cifras. Un barrido de
            // 9 contratos en el micro no es un BigTrade y decirle asi es
            // mentirle al ojo.
            //
            // Se ordena por tamano y se dibujan los N mayores QUE ESTAN EN
            // PANTALLA. Asi el corte se adapta solo: si el mercado se pone
            // pesado suben los numeros y siguen entrando los mismos catorce.
            var visibles = new List<Tuple<Libro.Barrido, int, int>>();
            foreach (var b in bs)
            {
                if (b.Barra < 0) continue;
                int xv, yv;
                try
                {
                    xv = cont.GetXByBar(b.Barra, false);
                    yv = cont.GetYByPrice(b.Precio, false);
                }
                catch { continue; }
                if (xv < x0 - 20 || xv > x1 + 20) continue;
                if (yv < ChartArea.Top - 6 || yv > ChartArea.Bottom + 6) continue;
                visibles.Add(Tuple.Create(b, xv, yv));
            }
            if (visibles.Count == 0) return;

            // UN CIRCULO NO SE MUEVE DE SU PRECIO.
            //
            // La version anterior, cuando dos barridos caian encimados, corria
            // el segundo hacia abajo para que se vieran los dos. El resultado
            // eran columnas verticales perfectamente rectas, y el operador
            // desconfio con razon: la ALTURA del circulo es el PRECIO al que
            // se opero, asi que correrlo es mentir sobre donde paso la cosa.
            // En las capturas del producto real el circulo -- ese rojo con
            // "205" adentro -- esta sentado en el precio de las velas, no
            // apilado en una columna.
            //
            // Lo correcto es SUMARLOS: si entraron varios barridos al mismo
            // precio en la misma vela, eso es un solo evento mas grande, y el
            // circulo crece. Ademas es mas informativo que tres circulos
            // chicos, porque lo que importa es cuanto entro en ese nivel.
            // el universo completo, agrupado igual que el visible, para que el
            // corte no dependa de la pantalla
            var juntadosTodos = new Dictionary<(int, decimal), decimal>();
            foreach (var b0 in bs)
            {
                if (b0.Barra < 0) continue;
                var cl = (b0.Barra, b0.Precio);
                juntadosTodos[cl] = (juntadosTodos.TryGetValue(cl, out var v0) ? v0 : 0m) + b0.Volumen;
            }

            var juntados = new Dictionary<(int, int), (Libro.Barrido B, decimal Vol, int X, int Y)>();
            foreach (var t in visibles)
            {
                var clave = (t.Item2, t.Item3);
                if (juntados.TryGetValue(clave, out var y0))
                    juntados[clave] = (y0.B.Volumen >= t.Item1.Volumen ? y0.B : t.Item1,
                                       y0.Vol + t.Item1.Volumen, t.Item2, t.Item3);
                else
                    juntados[clave] = (t.Item1, t.Item1.Volumen, t.Item2, t.Item3);
            }

            // EL RANKING NO PUEDE DEPENDER DE LO QUE SE VE.
            //
            // Mismo bug que el de las barras: se tomaban los N mayores DE LOS
            // VISIBLES, asi que al desplazar el grafico cambiaba el conjunto y
            // cambiaban cuales se dibujaban. Un barrido aparecia o desaparecia
            // segun donde estuviera la pantalla, no segun su tamano.
            //
            // Ahora el corte sale del universo COMPLETO de la memoria: un
            // barrido que entra en los N mayores se dibuja siempre que este en
            // pantalla, y uno que no entra no se dibuja nunca. Estable.
            decimal corte = 0;
            {
                var todosVol = juntadosTodos.Values
                                            .OrderByDescending(v => v).ToList();
                int k = Math.Max(1, CuantosDibujar) - 1;
                if (todosVol.Count > 0) corte = todosVol[Math.Min(k, todosVol.Count - 1)];
            }
            var elegidos = juntados.Values
                .Where(t => t.Vol >= corte)
                .OrderByDescending(t => t.Vol)
                .Take(Math.Max(1, CuantosDibujar))
                .ToList();

            decimal mayor = 1m;
            foreach (var t in elegidos) if (t.Vol > mayor) mayor = t.Vol;

            foreach (var t in elegidos)
            {
                var b = t.B;
                decimal vol = t.Vol;
                int x = t.X, y = t.Y;

                // CIRCULO CON EL NUMERO DE CONTRATOS ADENTRO.
                //
                // Asi los dibuja el producto original: en las capturas del
                // operador se leen circulos verdes y rojos con "291", "313",
                // "245", "204", "722", "390" adentro. El numero importa: un
                // punto sin numero obliga a adivinar el tamano por el area,
                // que el ojo estima mal.
                var col = b.Lado >= 0 ? ColCompra : ColVenta;
                // el area crece con el volumen, asi que el radio va con la
                // raiz: con escala lineal un barrido enorme deja al resto en un
                // pixel y se pierde el racimo, que es lo que hay que ver
                var r = (int)Math.Max(TamPuntoMin, Math.Sqrt((double)(vol / mayor)) * TamPunto);
                // translucido con anillo claro: deja ver la vela debajo
                g.FillEllipse(Color.FromArgb(120, col),
                    new Rectangle(x - r / 2, y - r / 2, r, r));
                g.DrawEllipse(new RenderPen(Color.FromArgb(200, 235, 240, 245), 1.4f),
                    new Rectangle(x - r / 2, y - r / 2, r, r));
                if (NumeroAdentro && r >= 16)
                {
                    var txt = ((int)vol).ToString(CultureInfo.InvariantCulture);
                    var ft = new RenderFont("Arial", r >= 24 ? 9f : 7.5f);
                    var m = g.MeasureString(txt, ft);
                    if (m.Width < r - 2)
                        g.DrawString(txt, ft, Color.FromArgb(245, 255, 255, 255),
                                     x - m.Width / 2, y - m.Height / 2);
                }
            }
        }

        /// <summary>El valor que tenia ese strike hace N minutos. NaN si
        /// todavia no hay historia suficiente -- al abrir el grafico la estela
        /// arranca vacia y se va llenando, que es lo correcto: no se inventa
        /// un pasado que no se midio.</summary>
        private static double Anterior(List<KeyValuePair<DateTime, double>> h,
                                       DateTime ahora, int minutos)
        {
            if (h == null || h.Count == 0) return double.NaN;
            var blanco = ahora.AddMinutes(-minutos);
            if (h[0].Key > blanco) return double.NaN;   // no llega tan atras
            double mejor = double.NaN; var dist = TimeSpan.MaxValue;
            foreach (var kv in h)
            {
                var d = kv.Key > blanco ? kv.Key - blanco : blanco - kv.Key;
                if (d < dist) { dist = d; mejor = kv.Value; }
            }
            return mejor;
        }

        /// <summary>Los circulitos: donde estuvo ese strike hace 1, 5 y 15
        /// minutos, sobre el MISMO eje de magnitud que su barra.
        ///
        /// Si el circulo queda DENTRO de la barra, el nivel encogio. Si queda
        /// AFUERA, pasado de la punta, crecio. Medido en el video: los dos
        /// casos aparecen, y esa es toda la gracia.
        ///
        /// izquierda = true dibuja creciendo hacia la derecha desde x0;
        /// false dibuja creciendo hacia la izquierda desde x1.
        /// </summary>
        private void Estela(RenderContext g, double strike, double mx, int ancho,
                            int origen, int y, bool izquierda, int altoFila)
        {
            if (mx <= 0) return;
            List<KeyValuePair<DateTime, double>> h;
            lock (_estela) { if (!_estela.TryGetValue(strike, out h)) return; h = new List<KeyValuePair<DateTime, double>>(h); }
            if (h.Count < 2) return;

            var ahora = Ahora();
            var col = izquierda ? ColCircIzq : ColCircDer;
            int cuantos = Math.Max(1, Math.Min(4, CirculosPorBarra));

            for (int i = 0; i < cuantos && i < Ventanas.Length; i++)
            {
                var v = Anterior(h, ahora, Ventanas[i]);
                if (double.IsNaN(v) || v == 0) continue;
                int w = Math.Max(1, (int)(Math.Abs(v) / mx * ancho));
                int cx = izquierda ? origen + w : origen - w;
                // el mas reciente, mas grande: al reves se lee como si el
                // pasado pesara mas que el presente
                // LA MARCA NUNCA ES MAS ALTA QUE LA FILA.
                //
                // Antes el tamano era fijo y no miraba la separacion entre
                // strikes: con el grafico alejado los circulos de filas vecinas
                // se tocaban y habia que agrandar mucho la pantalla para saber
                // si eran uno o varios. Ahora el alto de fila es el techo, y
                // encima se le resta AchicarMarca para dejar aire.
                int techo = Math.Max(3, altoFila - Math.Max(0, AchicarMarca));
                int r = Math.Max(3, Math.Min(techo, TamCirculo - i * 2));
                var rect = new Rectangle(cx - r / 2, y - r / 2, r, r);

                if (FormaMarca >= 2)
                {
                    g.FillRectangle(Color.FromArgb(215, col), rect);
                }
                else if (FormaMarca == 1)
                {
                    // ANILLO: a tamano chico es el que mejor se separa, porque
                    // el hueco del medio marca el limite de cada marca aunque
                    // dos queden pegadas.
                    g.FillEllipse(Color.FromArgb(70, col), rect);
                    g.DrawEllipse(new RenderPen(Color.FromArgb(240, col), 1.3f), rect);
                }
                else
                {
                    g.FillEllipse(Color.FromArgb(210, col), rect);
                    g.DrawEllipse(new RenderPen(Color.FromArgb(120, 235, 240, 245), 1f), rect);
                }
            }
        }

        /// <summary>LAS DOMINANTES, COMO BANDAS DE PUNTITOS.
        ///
        /// Un punto por vela al nivel dominante de esa vela. Cuando el nivel
        /// se sostiene la banda se hace densa; cuando se mueve, ondula. Es
        /// exactamente lo que se ve en las capturas rotuladas "NIVELES DE
        /// MAYOR EXPOSICION GAMMA".
        ///
        /// Dibujar una sola linea recta en lugar de esto pierde informacion:
        /// la linea no dice si el nivel estuvo firme toda la rueda o si se
        /// vino moviendo, y esa diferencia es justamente la que hace que el
        /// precio lo respete o no.
        /// </summary>
        private void PuntosDominantes(RenderContext g, IChartContainer cont, int x0, int x1)
        {
            Dictionary<int, Marca> copia;
            lock (_porBarra) copia = new Dictionary<int, Marca>(_porBarra);
            if (copia.Count == 0) return;

            int w = Math.Max(3, AnchoPunto), h = Math.Max(2, AltoPunto);
            int desde = Math.Max(0, FirstVisibleBarNumber);
            int hasta = Math.Min(CurrentBar - 1, LastVisibleBarNumber);

            // LA CANTIDAD DE MARCAS NO DEPENDE DEL ZOOM. NUNCA.
            //
            // ESTE FUE EL BUG QUE MAS COSTO Y LO CAUSO UN ARREGLO MIO ANTERIOR.
            // Yo habia puesto que si las marcas se tocarian de costado se
            // dibujara UNA CADA N VELAS. Al cambiar el zoom ese N saltaba de 2
            // a 4 y desaparecia la mitad de las marcas. Lo justifique diciendo
            // que "al menos ninguna se mueve", y estaba mal: desaparecer es
            // exactamente lo inadmisible. Un mapa que cambia de contenido
            // segun como uno mire la pantalla no se puede usar para decidir.
            //
            // No hay salteo. Se dibuja una marca por cada vela registrada,
            // siempre. El conjunto queda determinado SOLO por el dato: que
            // velas tienen registro. El zoom no participa.
            //
            // Lo unico que se adapta al zoom es el TAMANO: con las velas muy
            // juntas la marca se angosta para que no se coma a la vecina. Si al
            // final igual se tocan, se tocan -- a suficiente zoom de salida
            // TODO se junta, incluidas las velas, y eso es honesto. Lo que no
            // es honesto es que falten marcas.
            try
            {
                int xa = cont.GetXByBar(desde, false);
                int xb = cont.GetXByBar(Math.Min(hasta, desde + 1), false);
                int sepX = Math.Abs(xb - xa);
                if (sepX > 0) w = Math.Min(w, Math.Max(1, sepX - 1));
            }
            catch { }

            int dibujadas = 0, conRegistro = 0;
            for (int b = Math.Max(0, desde); b <= hasta; b++)
            {
                if (!copia.TryGetValue(b, out var m) || !m.Hay) continue;
                conRegistro++;
                int x;
                try { x = cont.GetXByBar(b, false); }
                catch { continue; }
                if (x < x0 - 10 || x > x1 + 10) continue;
                dibujadas++;

                // NUNCA DOS MARCAS PEGADAS EN LA MISMA VELA.
                //
                // El operador dijo que tenia que agrandar mucho la pantalla
                // para saber si eran una o varias. Si dos dominantes caen mas
                // cerca que SeparacionMinima, se dibuja solo la mas fuerte:
                // dos marcas que se tocan no informan mas que una, informan
                // menos, porque ya no se sabe cuantas hay.
                // Ya NO se descarta nada aca: el conjunto se decidio en puntos
                // cuando se calcularon los picos. Filtrar de nuevo por pixeles
                // haria que la cantidad dependiera del zoom, que es justo el
                // bug que se arreglo.
                void Punto(double precio, Color col, double fuerza)
                {
                    if (precio <= 0) return;
                    int y;
                    try { y = cont.GetYByPrice((decimal)precio, false); }
                    catch { return; }
                    if (y < ChartArea.Top - 4 || y > ChartArea.Bottom + 4) return;
                    int sep = Math.Max(2, SeparacionMinima);

                    var f = Math.Max(0.25, Math.Min(1.0, fuerza));
                    // la fuerza cambia el tamano pero NO la proporcion: un
                    // cuadradito que se estira deja de leerse como cuadradito
                    int ww = Math.Max(2, (int)Math.Round(w * (0.65 + 0.35 * f)));
                    int hh = Math.Max(2, (int)Math.Round(h * (0.65 + 0.35 * f)));
                    // y nunca mas alta que la separacion, para que aunque dos
                    // queden en velas vecinas se lea el hueco entre ellas
                    hh = Math.Min(hh, Math.Max(2, sep - 1));
                    var rc = new Rectangle(x - ww / 2, y - hh / 2, ww, hh);
                    var cc2 = Color.FromArgb((int)(150 + 105 * f), col);

                    if (FormaDominante == 1) g.FillEllipse(cc2, rc);
                    else if (FormaDominante >= 2)
                        g.FillRectangle(cc2, new Rectangle(x - Math.Max(3, ww) / 2, y - 1,
                                                           Math.Max(3, ww), Math.Max(1, hh - 1)));
                    else g.FillRectangle(cc2, rc);   // cuadradito
                }

                // LAS DOMINANTES DE ESA VELA, no las de ahora.
                //
                // Se dibuja lo que se midio en el momento de esa vela. Si el
                // nivel se movio, los guiones ondulan; si en esa vela ninguna
                // zona califico, no hay guion y queda el hueco. Asi es como se
                // ve en los cuadros del producto real, medido con deteccion de
                // color sobre tres capturas del operador.
                if (m.Doms != null)
                {
                    double imax = 0.0;
                    if (m.Incs != null) foreach (var v in m.Incs) if (v > imax) imax = v;
                    if (imax <= 0) imax = 1.0;
                    for (int k = 0; k < m.Doms.Length; k++)
                    {
                        double inc = (m.Incs != null && k < m.Incs.Length) ? m.Incs[k] : imax;
                        Punto(m.Doms[k], ColPuntoDom, inc / imax);
                    }
                }

                Punto(m.MajorPos, ColPuntoDom, 1.0);
                Punto(m.MajorNeg, ColPuntoDom, 1.0);
                Punto(m.Zero, ColPuntoZero, 1.0);

                // las dos hipotesis, cada una con su color, para poder
                // distinguirlas a simple vista de las de interes abierto
                if (VerMajorsVolumen)
                {
                    Punto(m.MajorPosVol, ColPuntoVol, 1.0);
                    Punto(m.MajorNegVol, ColPuntoVol, 1.0);
                }
                if (VerZeroVolumen) Punto(m.ZeroVol, ColPuntoZeroVol, 1.0);
                if (VerMaxChange) Punto(m.MaxChange, ColPuntoMaxChange, 1.0);
            }

            // EL CONTROL: dibujadas tiene que ser IGUAL a las que tienen
            // registro en el rango visible. Si alguna vez difieren, hay un
            // salteo escondido en algun lado y el bug volvio.
            _marcasDibujadas = dibujadas;
            _marcasConRegistro = conRegistro;

            // POR QUE NO SE DIBUJA UNA BANDA A LO ANCHO.
            //
            // Antes se dibujaba, a la altura de cada dominante, una linea de
            // puntos recta que cruzaba todo el grafico. El operador dijo que
            // en el producto real eso no se ve asi, y midiendo tres cuadros
            // suyos con deteccion de color resulto que tenia razon:
            //
            //   banda de 17 guiones -- ondula 47 px = 9,4 alturas de guion
            //   banda de 18 guiones -- ondula 80 px = 13,3 alturas
            //   banda de 28 guiones -- ondula 196 px = 28,1 alturas
            //
            // Ninguna es recta, todas tienen huecos (el mayor de 352 px) y
            // ninguna llega de punta a punta del lienzo. El guion mide 7 a 13
            // px de ancho, o sea UNA VELA.
            //
            // La banda recta ademas mentia: pintaba el nivel de AHORA sobre
            // velas de hace horas, donde ese nivel no se habia medido. Eso es
            // dibujar una hipotesis como si fuera un registro.
            //
            // Lo correcto es lo de arriba: cada vela muestra donde estaba la
            // dominante en SU momento. Ondula solo, tiene huecos solos, y
            // arranca cuando arranco la medicion.
        }

        /// <summary>Las pelotitas del Max Change, plantadas en el grafico.
        ///
        /// Cada una marca DONDE y CUANDO se detecto el mayor cambio de gamma.
        /// Se acumulan a lo largo de la sesion, y donde se juntan varias es
        /// donde el mapa se estuvo moviendo: por eso el precio suele ir a
        /// buscarlas.
        ///
        /// El tamano sale de la raiz de la fuerza y no de la fuerza cruda: con
        /// escala lineal un solo cambio enorme deja al resto en un pixel y se
        /// pierde justamente el racimo, que es lo que hay que ver.
        /// </summary>
        private void Pelotitas(RenderContext g, IChartContainer cont, int x0, int x1)
        {
            List<Pelotita> ps;
            lock (_pelotitas) ps = new List<Pelotita>(_pelotitas);
            if (ps.Count == 0) return;

            // a que barra corresponde cada una
            if (_ultimoMapeoPel != CurrentBar)
            {
                _ultimoMapeoPel = CurrentBar;
                var pend = ps.OrderByDescending(p => p.Hora).ToList();
                int i = Math.Max(0, CurrentBar - 1), k = 0;
                while (i >= 0 && k < pend.Count)
                {
                    IndicatorCandle c;
                    try { c = GetCandle(i); } catch { break; }
                    var tc = c.Time.Kind == DateTimeKind.Utc ? c.Time : c.Time.ToUniversalTime();
                    while (k < pend.Count && pend[k].Hora >= tc) { pend[k].Barra = i; k++; }
                    i--;
                }
            }

            foreach (var p in ps)
            {
                if (p.Barra < 0) continue;
                int x, y;
                try
                {
                    x = cont.GetXByBar(p.Barra, false);
                    y = cont.GetYByPrice((decimal)p.Fut, false);
                }
                catch { continue; }
                if (x < x0 - 20 || x > x1 + 20) continue;
                if (y < ChartArea.Top - 8 || y > ChartArea.Bottom + 8) continue;

                // GUION, no circulo. En NinjaTrader se dibujan como rayitas
                // anchas y bajas; en la version web se ven mas redondas. Se
                // sigue la forma de NinjaTrader porque es donde va a correr.
                int w = Math.Max(5, (int)(Math.Sqrt(p.Fuerza) * TamPelotita * 1.6));
                int h = Math.Max(2, TamPelotita / 4);
                var col = p.Delta >= 0 ? ColPelotitaMax : ColPelotita;
                g.FillRectangle(Color.FromArgb(225, col),
                    new Rectangle(x - w / 2, y - h / 2, w, h));
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

        /// <summary>
        /// UN NIVEL: linea fina y un chip pegado al eje con precio y distancia.
        ///
        /// POR QUE EL CHIP VA A LA DERECHA. Antes el nombre iba flotando a la
        /// izquierda y el precio contra el eje: dos objetos separados por todo
        /// el ancho del grafico para decir una sola cosa, y el ojo tenia que
        /// unirlos. Pegado al eje esta donde el ojo ya mira -- ahi lee el
        /// precio actual -- y en un solo golpe sale que nivel es, a que precio
        /// y CUANTOS PUNTOS FALTAN, que es el numero con el que se decide una
        /// entrada.
        /// </summary>
        // ---- lo que se calcula UNA vez por cuadro y usan todas las etiquetas ----
        private double _sigHRender, _pxRender, _probRender = double.NaN;
        private Dictionary<double, (double total, double calls, double puts)> _volVivoRender;
        private List<(double precio, double vol, double delta, int rango, bool flojo)> _nodosRender;
        private readonly Dictionary<double, int> _rangoNivel = new();

        /// <summary>El nivel del perfil mas cercano a un precio de futuro, si esta a
        /// 3 puntos o menos.</summary>
        private static Nivel? NivelCerca(List<Nivel> pf, double fut)
        {
            double mejor = double.MaxValue; Nivel q = default; bool hay = false;
            foreach (var n in pf)
            {
                double d = Math.Abs(n.Fut - fut);
                if (d < mejor) { mejor = d; q = n; hay = true; }
            }
            return hay && mejor <= 3.0 ? q : (Nivel?)null;
        }

        /// <summary>
        /// UNA VEZ POR CUADRO: el precio actual, la volatilidad realizada para la
        /// chance de toque, el volumen vivo por strike, los nodos del otro
        /// indicador y el RANKING de los niveles de gamma que se van a dibujar
        /// (G1 = mas |GEX|). Asi todas las etiquetas del cuadro cuentan lo mismo.
        /// </summary>
        private void PrepararRender()
        {
            try { _pxRender = (double)GetCandle(Math.Max(0, CurrentBar - 1)).Close; } catch { _pxRender = 0; }
            var (sv, mv) = VolRealizada(Math.Max(10, EscaleraVelasVol));
            _sigHRender = (sv > 0 && mv > 0) ? sv * Math.Sqrt(Math.Max(1.0, HorizonteProbMin / mv)) : 0;
            lock (_candado) _volVivoRender = _volVivoCache;
            _nodosRender = LeerNodos();

            _rangoNivel.Clear();
            List<Nivel> pf; double mp, mn, mpR, mnR, a0, b0, mx;
            lock (_candado) { pf = _perfil; mp = _majorPos; mn = _majorNeg; mpR = _mpRival; mnR = _mnRival; a0 = _mp0Ult; b0 = _mn0Ult; mx = _maxGex; }
            if (pf == null || pf.Count == 0) return;
            var cands = new List<(double fut, double g)>();
            foreach (var p in new[] { mp, mn, mpR, mnR, a0, b0 })
            {
                if (double.IsNaN(p) || p <= 0) continue;
                var n = NivelCerca(pf, p);
                if (n.HasValue) cands.Add((p, Math.Abs(n.Value.Gex)));
            }
            // los cercanos entran al mismo ranking: G1 es el mas pesado de
            // TODO lo que se dibuja, este donde este
            if (VerNivelesCercanos)
                foreach (var nv in SeleccionarCercanos(pf, _pxRender, mp, mn, mx))
                    cands.Add((nv.Fut, Math.Abs(nv.Gex)));
            int r = 0;
            foreach (var c in cands.OrderByDescending(c => c.g))
            {
                r++;
                if (!_rangoNivel.ContainsKey(c.fut)) _rangoNivel[c.fut] = r;
            }
        }

        /// <summary>La altura en pixeles de todas las rayas de este cuadro (gamma
        /// propias y nodos del otro indicador), para que ningun chip tape una
        /// raya ajena. Ademas publica los precios de los niveles de gamma por
        /// AppDomain, para que el indicador de nodos esquive estas rayas con
        /// sus propias etiquetas.</summary>
        private void LlenarYsNiveles(IChartContainer cont)
        {
            _ysNiveles.Clear();
            if (cont == null) return;
            double zero, mp, mn, mpR, mnR, a0, b0;
            lock (_candado) { zero = _zeroGamma; mp = _majorPos; mn = _majorNeg; mpR = _mpRival; mnR = _mnRival; a0 = _mp0Ult; b0 = _mn0Ult; }
            var precios = new List<double>();
            foreach (var p in new[] { zero, mp, mn, mpR, mnR, a0, b0 })
                if (!double.IsNaN(p) && p > 0) precios.Add(p);
            var sb = new System.Text.StringBuilder(DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
            foreach (var p in precios)
            {
                try { _ysNiveles.Add(cont.GetYByPrice((decimal)p, false)); } catch { }
                sb.Append(';').Append(p.ToString("0.####", CultureInfo.InvariantCulture));
            }
            if (_nodosRender != null)
                foreach (var nd in _nodosRender)
                    try { _ysNiveles.Add(cont.GetYByPrice((decimal)nd.precio, false)); } catch { }
            try
            {
                string inst = (InstrumentInfo?.Instrument ?? "").ToUpperInvariant().TrimStart('#');
                if (inst.Length > 0) AppDomain.CurrentDomain.SetData("PythiaGex.Niveles." + inst, sb.ToString());
            }
            catch { }
        }

        /// <summary>Los strikes cercanos que se dibujan: dentro del radio, con al
        /// menos el minimo de |GEX| relativo al mayor, distintos de los muros, y
        /// la mitad por encima del precio y la otra mitad por debajo. La misma
        /// seleccion la usan el dibujo y el ranking, para que cuenten lo mismo.</summary>
        private List<Nivel> SeleccionarCercanos(List<Nivel> perfil, double px, double mp, double mn, double mx)
        {
            var salida = new List<Nivel>();
            if (perfil == null || px <= 0 || mx <= 0) return salida;
            double radio = (double)Math.Max(5m, RadioCercanos);
            double piso = mx * Math.Max(0, Math.Min(90, MinimoCercanoPct)) / 100.0;
            int porLado = Math.Max(1, (Math.Max(1, CuantosCercanos) + 1) / 2);
            bool Ok(Nivel nv) => Math.Abs(nv.Fut - px) <= radio && Math.Abs(nv.Gex) >= piso
                                 && Math.Abs(nv.Fut - mp) > 0.01 && Math.Abs(nv.Fut - mn) > 0.01;
            // EL MAS CERCANO PRIMERO, no el mas pesado. Con "mas pesado" el
            // siguiente arriba salia 7.756 (+43) y se salteaba 7.726 (+13),
            // que es el que el precio toca antes. El piso de peso ya filtro
            // los que no cuentan; entre los que cuentan, manda la distancia.
            salida.AddRange(perfil.Where(nv => Ok(nv) && nv.Fut > px).OrderBy(nv => nv.Fut - px).Take(porLado));
            salida.AddRange(perfil.Where(nv => Ok(nv) && nv.Fut <= px).OrderBy(nv => px - nv.Fut).Take(porLado));
            return salida;
        }

        /// <summary>Miles de millones o millones, con signo: +3,4B / -791M.</summary>
        private static string Bm(double v)
        {
            var es = CultureInfo.GetCultureInfo("es-AR");
            if (Math.Abs(v) >= 1e9) return (v / 1e9).ToString("+0.0;-0.0", es) + "B";
            return (v / 1e6).ToString("+0;-0", es) + "M";
        }

        /// <summary>
        /// LA BANDA "ACA": el nivel donde esta parado el precio.
        ///
        /// Si el precio esta a dos ticks o menos de un nivel de gamma o de un
        /// nodo de volumen, se pinta una banda tenue del color de ese nivel y se
        /// escribe ACA con su nombre. Es la respuesta a "en que nivel estamos".
        /// Si no hay ninguno tan cerca, no se pinta nada: no hay peldano.
        /// </summary>
        private void BandaAca(RenderContext g, IChartContainer cont, int x0, int x1)
        {
            if (_pxRender <= 0) return;
            decimal tick = 0.25m;
            try { if (InstrumentInfo != null && InstrumentInfo.TickSize > 0) tick = InstrumentInfo.TickSize; } catch { }
            double tol = (double)tick * 2;
            string nombre = null; double precio = 0; Color col = ColTexto; double mejor = double.MaxValue;
            void Cand(string n, double p, Color c)
            {
                if (double.IsNaN(p) || p <= 0) return;
                double d = Math.Abs(p - _pxRender);
                if (d <= tol && d < mejor) { mejor = d; nombre = n; precio = p; col = c; }
            }
            double zero, mp, mn, mpR, mnR;
            lock (_candado) { zero = _zeroGamma; mp = _majorPos; mn = _majorNeg; mpR = _mpRival; mnR = _mnRival; }
            Cand("zero", zero, ColZero); Cand("+wall", mp, ColPos); Cand("-wall", mn, ColNeg);
            Cand("+wall?", mpR, ColPos); Cand("-wall?", mnR, ColNeg);
            if (_nodosRender != null)
                foreach (var nd in _nodosRender)
                    if (!nd.flojo) Cand("nodo #" + nd.rango, nd.precio, Color.FromArgb(235, 200, 60));
            if (nombre == null) return;

            int ya, yb;
            try
            {
                ya = cont.GetYByPrice((decimal)precio + tick, false);
                yb = cont.GetYByPrice((decimal)precio - tick, false);
            }
            catch { return; }
            int top = Math.Min(ya, yb), alto = Math.Max(2, Math.Abs(yb - ya));
            g.FillRectangle(Color.FromArgb(30, col), new Rectangle(x0, top, Math.Max(1, x1 - x0), alto));
            var f = new RenderFont("Arial", 9.5f);
            string t = "ACA  " + nombre + "  " + precio.ToString("N2", CultureInfo.GetCultureInfo("es-AR"));
            var m = g.MeasureString(t, f);
            int yt = top - m.Height - 3;
            if (yt < ChartArea.Top + 2) yt = top + alto + 3;
            g.FillRectangle(Color.FromArgb(200, ColFondo), new Rectangle(x0 + 6, yt, m.Width + 8, m.Height + 2));
            g.FillRectangle(Color.FromArgb(230, col), new Rectangle(x0 + 6, yt, 3, m.Height + 2));
            g.DrawString(t, f, Color.FromArgb(245, col), x0 + 12, yt + 1);
        }

        /// <summary>
        /// EL TEXTO DEL CHIP, FORMATO B (elegido el 2026-09-06):
        ///   txt1  "G1 +wall·3d 7831 +111 0%"     brillante: ranking por |GEX|,
        ///         nombre, vencimiento que lo sostiene, precio, distancia y
        ///         chance de toque en el horizonte (vol realizada del grafico)
        ///   txt2  " | γ+3,4B oi9,6k ac+0,8B v181" atenuado: GEX repreciado al
        ///         precio de ahora, interes abierto (de AYER, para todos),
        ///         aceleracion (cuanto cambia el GEX si el precio sube 1 %) y
        ///         contratos de opciones operados HOY en el strike de ES mas
        ///         cercano (Rithmic, en vivo). Solo con detalle.
        /// Deja en _probRender la chance, para la barrita.
        /// </summary>
        private void TextoChip(double precio, string nombre, string dist, bool detalle,
                               out string txt1, out string txt2)
        {
            var es = CultureInfo.GetCultureInfo("es-AR");
            // el ranking solo en los niveles de gamma con detalle: una linea de
            // flujo al mismo precio NO es "G2" otra vez
            string rango = detalle && _rangoNivel.TryGetValue(precio, out var rk) ? "G" + rk + " " : "";
            string tag = "", extra = "";
            if (detalle && EtiquetaDetallada)
            {
                List<Nivel> pf2; lock (_candado) pf2 = _perfil;
                var nq = pf2 != null ? NivelCerca(pf2, precio) : null;
                if (nq.HasValue)
                {
                    var n = nq.Value;
                    tag = n.DiasDom > 0
                        ? (n.DiasDom < 1.0 ? "hoy" : Math.Round(n.DiasDom).ToString("0", es) + "d") : "";
                    string vv = "-";
                    var vr = _volVivoRender;
                    if (vr != null)
                    {
                        double mejor = 2.6, tot = double.NaN;
                        foreach (var kv in vr)
                        {
                            double d0 = Math.Abs(kv.Key - precio);
                            if (d0 < mejor) { mejor = d0; tot = kv.Value.total; }
                        }
                        if (!double.IsNaN(tot)) vv = ((int)tot).ToString("N0", es);
                    }
                    string oi = n.Oi >= 1000 ? (n.Oi / 1000).ToString("0.#", es) + "k" : n.Oi.ToString("0", es);
                    extra = string.Format(es, " | γ{0} oi{1} ac{2} v{3}", Bm(n.Gex), oi, Bm(n.Acel), vv);
                }
            }
            string prob = "";
            _probRender = double.NaN;
            if (_sigHRender > 0 && _pxRender > 0)
            {
                double z = Math.Abs(Math.Log(precio / _pxRender)) / _sigHRender;
                double p = Math.Min(1.0, Math.Max(0.0, 2.0 * Phi(-z)));
                prob = (p * 100).ToString("0", es) + "%";
                _probRender = p;
            }
            string precioTxt = DecimalesPrecio <= 0
                ? Math.Round(precio).ToString("N0", es)
                : precio.ToString("N" + Math.Min(2, DecimalesPrecio), es);
            txt1 = rango + nombre + (tag.Length > 0 ? "·" + tag : "") + " " + precioTxt
                 + (string.IsNullOrEmpty(dist) ? "" : " " + dist)
                 + (prob.Length > 0 ? " " + prob : "");
            txt2 = extra;
        }

        /// <summary>Un lugar para un chip vale si esta dentro del area visible, no
        /// choca con otro chip ya puesto, no cubre la raya de OTRO nivel (gamma
        /// o nodo) y no pisa el tablero. yPropio es la raya del propio nivel
        /// (int.MinValue si no tiene, como los fijados en el borde).</summary>
        private bool LugarLibre(Rectangle r, int yPropio, bool mirarRayas = true)
        {
            int piso = ChartArea.Bottom - Math.Max(6, MargenInferior);
            if (r.Top < ChartArea.Top + 2 || r.Bottom > piso) return false;
            var r2 = Rectangle.Inflate(r, 2, 2);
            foreach (var u in _rectsUsados) if (u.IntersectsWith(r2)) return false;
            if (mirarRayas)
                foreach (var yl in _ysNiveles)
                    if (yl != yPropio && yl >= r.Top - 1 && yl <= r.Bottom + 1) return false;
            if (!_tableroRect.IsEmpty && VerTablero && _tableroRect.IntersectsWith(r2)) return false;
            return true;
        }

        /// <summary>Intenta poner el chip en la fila yy: primero contra el eje; si
        /// lo unico que molesta es el tablero, a la izquierda del tablero en la
        /// MISMA fila (asi el chip no sube a flotar a mitad de grafico, que fue
        /// lo que paso el 2026-09-07 con el "-wall? disputado"). Devuelve la x.</summary>
        private bool Ubicar(int xc, int yy, int w, int h, int yPropio, bool mirarRayas, out int xFinal)
        {
            xFinal = xc;
            var r = new Rectangle(xc, yy, w, h);
            if (LugarLibre(r, yPropio, mirarRayas)) return true;
            if (!_tableroRect.IsEmpty && VerTablero && _tableroRect.IntersectsWith(Rectangle.Inflate(r, 2, 2)))
            {
                int x2 = _tableroRect.Left - w - 6;
                if (x2 >= ChartArea.Left + 2 && LugarLibre(new Rectangle(x2, yy, w, h), yPropio, mirarRayas))
                { xFinal = x2; return true; }
            }
            return false;
        }

        private void Linea(RenderContext g, IChartContainer cont, int x0, int x1,
                           double precio, Color col, string nombre, bool grueso,
                           double spot, int xEje, bool secundario = false, bool detalle = true)
        {
            if (double.IsNaN(precio) || precio <= 0) return;
            int y;
            try { y = cont.GetYByPrice((decimal)precio, false); }
            catch { return; }

            decimal pxAct = 0;
            try { pxAct = GetCandle(Math.Max(0, CurrentBar - 1)).Close; } catch { }
            double falta = pxAct > 0 ? precio - (double)pxAct : double.NaN;
            string dist = double.IsNaN(falta) ? "" :
                (falta >= 0 ? "+" : "") + falta.ToString("N0", CultureInfo.GetCultureInfo("es-AR"));

            // chica pero legible: el operador pidio una sola linea, minimizada
            var f = new RenderFont("Arial", (float)Math.Max(6.5m, Math.Min(12m, TamEtiqueta)));

            // NIVEL FUERA DE PANTALLA: no se calla, se marca en el borde.
            //
            // Medido en vivo: con la ventana de precio que suele usar el
            // operador NINGUN strike entra, porque los de SPX van de 5 en 5 y
            // los muros del dia estaban a 27 y 73 puntos. El indicador
            // dibujaba en silencio y parecia roto.
            if (y < ChartArea.Top || y > ChartArea.Bottom)
            {
                if (!MarcarFueraDePantalla) return;
                bool arriba = y < ChartArea.Top;
                // el mismo texto que el chip sobre la raya, con la flecha adelante
                TextoChip(precio, nombre, dist, detalle, out var o1, out var o2);
                o1 = (arriba ? "▲ " : "▼ ") + o1;
                var m1 = g.MeasureString(o1, f);
                var m2 = o2.Length > 0 ? g.MeasureString(o2, f) : default;
                int wc = m1.Width + (o2.Length > 0 ? m2.Width : 0) + 14, hc = m1.Height + 3;
                int xx = xEje - wc - 2;
                // APILADOS, NO ENCIMADOS. Antes todos los niveles fuera de
                // pantalla caian en la misma fila (Top + 26) y solo se veia el
                // ultimo dibujado: el "siguiente arriba" quedaba tapado por el
                // +wall. Visto el 2026-09-07. Ahora cada uno toma la primera
                // fila libre desde el borde. 26 y no 6 en la primera porque ahi
                // ATAS pone su boton de reproduccion.
                // Solo cinco filas desde el borde y SIN mirar las rayas: un chip
                // fijado al borde que baja hasta el medio del grafico esquivando
                // rayas deja de parecer "fuera de pantalla" (visto el 2026-09-07
                // con el +wall a la altura de los nodos). Si choca con el
                // tablero, va a la izquierda del tablero en la misma fila.
                int yb2 = int.MinValue, xFin = xx;
                for (int k = 0; k < 5 && yb2 == int.MinValue; k++)
                {
                    int cand = arriba
                        ? ChartArea.Top + 26 + k * (hc + 3)
                        : ChartArea.Bottom - Math.Max(18, MargenInferior + 4) - hc - k * (hc + 3);
                    if (Ubicar(xx, cand, wc, hc, int.MinValue, false, out xFin)) yb2 = cand;
                }
                if (yb2 == int.MinValue)
                {
                    yb2 = arriba ? ChartArea.Top + 26 : ChartArea.Bottom - Math.Max(18, MargenInferior + 4) - hc;
                    xFin = xx;
                    if (!_tableroRect.IsEmpty && VerTablero
                        && _tableroRect.IntersectsWith(new Rectangle(xx, yb2, wc, hc)))
                        xFin = Math.Max(ChartArea.Left + 2, _tableroRect.Left - wc - 6);
                }
                xx = xFin;
                var rc = new Rectangle(xx, yb2, wc, hc);
                _rectsUsados.Add(rc);
                g.FillRectangle(Color.FromArgb(170, ColFondo), rc);
                g.DrawRectangle(new RenderPen(Color.FromArgb(120, col), 1f), rc);
                g.FillRectangle(Color.FromArgb(200, col), new Rectangle(xx, yb2, 3, hc));
                g.DrawString(o1, f, Color.FromArgb(215, ColTexto), xx + 7, yb2 + 1);
                if (o2.Length > 0)
                    g.DrawString(o2, f, Color.FromArgb((int)(215 * 0.55), ColTexto), xx + 7 + m1.Width, yb2 + 1);
                return;
            }

            // JERARQUIA: CERCA Y VENCE HOY = ENTERO; LEJOS Y VENCE EN DOS SEMANAS = APAGADO.
            //
            // Dos factores medibles, no una opinion:
            //   distancia   contra el movimiento esperado del dia (1 sigma de la
            //               cadena). A mas de un sigma el nivel probablemente no
            //               se toca hoy; a mas de dos, casi seguro no.
            //   vencimiento el que sostiene ese nivel (DiasDom). Un muro del 0DTE
            //               es un poste clavado que se evapora a las 16:00 ET; uno
            //               de dos semanas es un terraplen repartido.
            // El zero gamma no se atenua: es la referencia del regimen.
            double peso = 1.0;
            if (JerarquiaVisual && !secundario && !grueso)
            {
                double em; lock (_candado) em = _movEspUlt;
                if (!double.IsNaN(falta) && em > 0)
                {
                    double sig = Math.Abs(falta) / Math.Max(em, 10.0);
                    peso *= sig <= 1.0 ? 1.0 : sig <= 2.0 ? 0.75 : 0.5;
                }
                List<Nivel> pf; lock (_candado) pf = _perfil;
                if (pf != null)
                {
                    double mejor = double.MaxValue, dd = double.NaN;
                    foreach (var q in pf)
                    {
                        double d0 = Math.Abs(q.Fut - precio);
                        if (d0 < mejor) { mejor = d0; dd = q.DiasDom; }
                    }
                    if (mejor <= 3.0 && !double.IsNaN(dd))
                        peso *= dd <= 1.1 ? 1.0 : dd <= 3.0 ? 0.85 : 0.7;
                }
                peso = Math.Max(0.35, Math.Min(1.0, peso));
            }

            // la linea: fina y translucida, es una referencia y no un borde
            // Los cercanos van mas finos y mas apagados que los majors: son
            // mas, y si pesaran lo mismo la pantalla perderia jerarquia.
            int alfaLinea = (int)((secundario ? 95 : (grueso ? 190 : 140)) * peso);
            var pluma = new RenderPen(Color.FromArgb(alfaLinea, col),
                                      (float)((secundario ? 1f : (grueso ? 1.6f : 1.1f)) * (0.6 + 0.4 * peso)),
                                      secundario ? System.Drawing.Drawing2D.DashStyle.Dot
                                      : grueso ? System.Drawing.Drawing2D.DashStyle.Dash
                                               : System.Drawing.Drawing2D.DashStyle.Solid);
            g.DrawLine(pluma, x0, y, x1, y);

            // EL CHIP: UNA O DOS LINEAS, Y NUNCA PISADO (modelo 3, 2026-09-06).
            //
            // Linea 1: ranking por |GEX| (G1, G2...), nombre, que vencimiento lo
            //          sostiene ("hoy", "2d"), precio, distancia y chance de
            //          tocarlo en el horizonte (vol realizada del grafico).
            // Linea 2: GEX repreciado al precio de ahora, interes abierto (de
            //          AYER, para todos), aceleracion (cuanto cambia el GEX si
            //          el precio sube 1 %) y contratos de opciones operados HOY
            //          en ese strike (Rithmic, en vivo). Solo en los niveles de
            //          gamma; los "cercanos" y los de flujo van con una linea.
            // El texto nunca baja del 80 % de brillo: la jerarquia la lleva la
            // linea, no la legibilidad de la etiqueta.
            // el texto lo arma TextoChip, el mismo para el chip sobre la raya y
            // para el chip fijado en el borde cuando el nivel esta fuera de pantalla
            TextoChip(precio, nombre, dist, detalle, out var txt1, out var txt2);
            var mt1 = g.MeasureString(txt1, f);
            var mt2 = txt2.Length > 0 ? g.MeasureString(txt2, f) : default;
            int wChip = mt1.Width + (txt2.Length > 0 ? mt2.Width : 0) + 14;
            int hChip = mt1.Height + 3;

            // contra el EJE, no contra el final de la linea: la linea ahora
            // termina antes del perfil de la derecha y el chip quedaba
            // flotando en el medio del grafico, que es donde menos sirve.
            int xc = xEje - wChip - 2;

            // ARRIBA O ABAJO DE LA RAYA, NUNCA ENCIMA. Y SIN TAPAR LA VECINA.
            //
            // Pedido del operador: la etiqueta no puede pisar su propia raya ni
            // confundirse con la de al lado. Se prueba primero pegada arriba,
            // despues pegada abajo, y despues alejandose alternadamente; un
            // lugar vale si no choca con otro chip, no cubre la raya de OTRO
            // nivel (gamma o nodo de volumen) y no se sale del area visible.
            // Si el chip queda lejos de su raya, un tirante fino lo une.
            int yTxt = int.MinValue, xUb = xc;
            for (int k = 0; k < 8 && yTxt == int.MinValue; k++)
            {
                int arriba = y - 3 - hChip - k * (hChip + 3);
                int abajo = y + 3 + k * (hChip + 3);
                if (Ubicar(xc, arriba, wChip, hChip, y, true, out xUb)) yTxt = arriba;
                else if (Ubicar(xc, abajo, wChip, hChip, y, true, out xUb)) yTxt = abajo;
            }
            if (yTxt == int.MinValue)
            {
                // no quedo lugar limpio: arriba de su raya, y si el tablero
                // esta ahi, a la izquierda del tablero. Antes caia ENCIMA del
                // tablero (visto el 2026-09-07 con "G5 acel 7.706").
                yTxt = Math.Max(ChartArea.Top + 2, y - 3 - hChip);
                xUb = xc;
                if (!_tableroRect.IsEmpty && VerTablero
                    && _tableroRect.IntersectsWith(new Rectangle(xc, yTxt, wChip, hChip)))
                    xUb = Math.Max(ChartArea.Left + 2, _tableroRect.Left - wChip - 6);
            }
            xc = xUb;
            var rectChip = new Rectangle(xc, yTxt, wChip, hChip);
            _rectsUsados.Add(rectChip);
            _etiquetasUsadas.Add(yTxt);

            g.FillRectangle(Color.FromArgb(secundario ? 190 : 230, ColFondo), rectChip);
            g.DrawRectangle(new RenderPen(Color.FromArgb((int)((secundario ? 90 : 150) * peso), col), 1f), rectChip);
            // una barrita del color a la izquierda del chip: identifica el
            // nivel sin tener que leer el nombre
            g.FillRectangle(Color.FromArgb((int)(230 * peso), col), new Rectangle(xc, yTxt, 3, hChip));
            g.DrawString(txt1, f, Color.FromArgb((int)(240 * Math.Max(0.8, peso)), ColTexto), xc + 7, yTxt + 1);
            if (txt2.Length > 0)
                g.DrawString(txt2, f, Color.FromArgb((int)(240 * 0.55 * Math.Max(0.8, peso)), ColTexto),
                             xc + 7 + mt1.Width, yTxt + 1);

            // la barrita de chance, a la izquierda del chip: se lee sin leer
            if (!double.IsNaN(_probRender))
            {
                int bw = 40, bx = xc - bw - 6, by = yTxt + hChip / 2 - 3;
                g.FillRectangle(Color.FromArgb(160, ColFondo), new Rectangle(bx, by, bw, 6));
                g.FillRectangle(Color.FromArgb((int)(210 * Math.Max(0.6, peso)), col),
                                new Rectangle(bx, by, Math.Max(1, (int)(bw * _probRender)), 6));
            }

            // el tirante: del borde del chip mas cercano a la raya, hasta la raya
            int borde = yTxt > y ? yTxt : yTxt + hChip;
            if (Math.Abs(borde - y) > 4)
                g.DrawLine(new RenderPen(Color.FromArgb(110, col), 1f), xc + 1, borde, xc + 1, y);
        }

        /// <summary>
        /// LA CINTA: UN SOLO RENGLON, Y LOS AVISOS SOLO CUANDO IMPORTAN.
        ///
        /// Antes eran cuatro renglones largos abajo a la izquierda, con la
        /// advertencia del interes abierto repetida en cada repintado. Una
        /// advertencia permanente deja de leerse a los cinco minutos y encima
        /// ocupa un cuarto del ancho del grafico.
        ///
        /// Ahora va la procedencia en una linea corta -- fuente, cuantos
        /// strikes, el neto -- y debajo SOLO lo que este realmente mal. Si no
        /// hay nada mal, no hay segundo renglon.
        /// </summary>
        /// <summary>
        /// SOLO LOS AVISOS. Si esta todo bien, no dibuja nada.
        ///
        /// Antes esto era una cinta de cuatro renglones abajo a la izquierda
        /// que decia CASI LO MISMO que el tablero de la otra esquina: dos
        /// cajas de estado peleando por la pantalla. La procedencia -- fuente,
        /// si esta en vivo, el neto -- se mudo al tablero, que es donde ya se
        /// miran los numeros.
        ///
        /// Aca queda unicamente lo que este realmente mal, arriba a la
        /// izquierda para que se vea, y desaparece cuando se arregla. Una
        /// advertencia permanente deja de leerse a los cinco minutos.
        /// </summary>
        private void Cinta(RenderContext g, int x0, Rectangle area, int nStrikes,
                           double neto, double spot)
        {
            var f = new RenderFont("Arial", 8.5f);
            var ls = new List<Tuple<string, Color>>();
            var c = _cUsada ?? _c;

            if (c == null)
                ls.Add(Tuple.Create(string.IsNullOrEmpty(_error)
                    ? "bajando la cadena..." : "sin cadena: " + _error, ColAviso));
            else
            {
                if (_baseOrigen == "sin base")
                    ls.Add(Tuple.Create("sin base indice->futuro: no se dibuja ningun nivel", ColNeg));
                else if (_baseOrigen != "medida" && !c.EsFuturo
                         && !_baseOrigen.StartsWith("no hace falta"))
                    ls.Add(Tuple.Create("base " + _baseOrigen + ": los niveles pueden estar corridos", ColAviso));
                if (_visiblesUlt == 0 && nStrikes > 0)
                    ls.Add(Tuple.Create("ningun strike entra en pantalla: abri la escala de precios", ColNeg));
                if (_vivaFlaca >= 0)
                    ls.Add(Tuple.Create(
                        "la cadena en vivo solo trajo " + _vivaFlaca + " strikes utiles: se usa CBOE",
                        ColAviso));
            }
            if (ls.Count == 0) return;

            var med = ls.Select(l => g.MeasureString(l.Item1, f)).ToList();
            int w = 0, h = 5;
            foreach (var m in med) { w = Math.Max(w, m.Width); h += m.Height + 1; }
            int x = x0 + 5, y = area.Top + 10;
            g.FillRectangle(Color.FromArgb(215, ColFondo), new Rectangle(x, y, w + 12, h));
            g.DrawRectangle(new RenderPen(Color.FromArgb(120, ColAviso), 1f),
                            new Rectangle(x, y, w + 12, h));
            int yy = y + 2;
            for (int i = 0; i < ls.Count; i++)
            {
                g.DrawString(ls[i].Item1, f, ls[i].Item2, x + 6, yy);
                yy += med[i].Height + 1;
            }
        }

        /// <summary>El tablero de numeros, arriba a la izquierda.
        ///
        /// Reproduce el panel que muestra el producto original: los niveles
        /// calculados por VOLUMEN y por INTERES ABIERTO en bloques separados,
        /// y abajo el max change por ventana. Separarlos no es cosmetico: el
        /// interes abierto es el mapa de ayer y el volumen es lo de hoy.
        /// </summary>
        private void Tablero(RenderContext g, Rectangle area)
        {
            var fb = new RenderFont("Consolas", (float)Math.Max(7m, Math.Min(14m, TamTablero)));
            List<Nivel> perfil; double neto, zero, mp, mn, netoV, mpv, mnv, spot;
            FilaCambio[] cam;
            bool positiva; double mpRiv, mnRiv, mpRat, mnRat, hz;
            lock (_candado)
            {
                perfil = _perfil; neto = _netGex; zero = _zeroGamma; spot = _spotUsado;
                mp = _majorPos; mn = _majorNeg;
                netoV = _netGexVol; mpv = _majorPosVol; mnv = _majorNegVol;
                cam = (FilaCambio[])_cambios.Clone();
                positiva = _gammaPositiva;
                mpRiv = _mpRival; mnRiv = _mnRival; mpRat = _mpRatio; mnRat = _mnRatio;
                hz = _horizonteZonas;
            }
            if (perfil == null || perfil.Count == 0) return;

            string P(double v) => v <= 0 || double.IsNaN(v) ? "--"
                : v.ToString("N2", CultureInfo.GetCultureInfo("es-AR"));
            string M(double v) => Math.Abs(v) >= 1e9
                ? (v / 1e9).ToString("N2", CultureInfo.GetCultureInfo("es-AR")) + "B"
                : (v / 1e6).ToString("N0", CultureInfo.GetCultureInfo("es-AR")) + "M";

            // EL TABLERO, COPIADO DE LAS CAPTURAS DEL OPERADOR.
            //
            // Cuatro bloques en este orden, con los mismos rotulos en minuscula
            // y el mismo codigo de color: verde el major positive, rojo el
            // major negative, cyan el net gex. Los rotulos van en ingles
            // porque asi estan en la fuente y asi los reconoce el operador;
            // la aclaracion entre parentesis va en castellano porque es
            // nuestra, no de ellos.
            // El zero gamma por volumen todavia no se calcula aparte: se
            // muestra el de interes abierto y NO se inventa otro numero.
            double zeroVol = 0;
            var colNet = Color.FromArgb(90, 210, 230);
            var ls = new List<Tuple<string, Color>>();

            // MODO COMPACTO, QUE ES EL POR DEFECTO.
            //
            // El tablero completo son catorce renglones y le tapa el grafico.
            // El operador pidio que sea chiquito y a un costado, y que se
            // despliegue solo si quiere mas. Aca va lo minimo que hace falta
            // para operar: en que regimen esta, donde cambia, y de donde salio
            // el dato -- porque un nivel sin fuente no se publica.
            if (TableroCompacto)
            {
                // SIN ZERO GAMMA NO HAY REGIMEN.
                //
                // Cuando la suma no cruza cero dentro de lo observado, el zero
                // sale NaN y esto mostraba "GAMMA - expansion": afirmaba
                // regimen negativo sin saberlo, solo porque la comparacion con
                // NaN da false. Un regimen sin dato no es un regimen negativo,
                // y de los dos se opera distinto.
                bool haySzero = !double.IsNaN(zero) && zero > 0 && spot > 0;
                if (!haySzero)
                    ls.Add(Tuple.Create("REGIMEN  sin dato", ColAviso));
                else
                    // EL REGIMEN SE LEE DE _gammaPositiva, NO DE spot > zero.
                    //
                    // spot esta en INDICE (_spotUsado = S) y zero ya lleva la
                    // base sumada (precio de FUTURO). Compararlos decia
                    // "expansion" con el net en +3,06 B y el precio 2 puntos
                    // arriba del zero: mentia en toda la franja de 6 puntos
                    // entre zero y zero+base, que es donde el precio estuvo la
                    // noche entera del 2026-09-06. _gammaPositiva se calcula
                    // en Repreciar con los dos numeros en indice.
                    ls.Add(Tuple.Create(positiva ? "GAMMA +  rango" : "GAMMA -  expansion",
                                        positiva ? ColPos : ColNeg));
                // EL HORIZONTE VA AL LADO DEL NUMERO. Opensera publica el zero
                // de SPX sumando todos los vencimientos y da 7688; este
                // indicador corta a 7 dias y da 7713 (medido el 2026-09-06,
                // misma cadena). Los dos son correctos en su definicion; sin el
                // horizonte escrito, el operador cree que uno de los dos miente.
                ls.Add(Tuple.Create("zero  " + P(zero) + "   " + DiasMax + "d", ColZero));
                ls.Add(Tuple.Create("+wall " + P(mp) + (double.IsNaN(mpRiv) ? "" : "  disp " + P(mpRiv)), ColPos));
                ls.Add(Tuple.Create("-wall " + P(mn) + (double.IsNaN(mnRiv) ? "" : "  disp " + P(mnRiv)), ColNeg));
                if (!double.IsNaN(hz) && Math.Abs(hz - DiasMax) > 0.5)
                    ls.Add(Tuple.Create("zonas radar " + hz.ToString("0", CultureInfo.InvariantCulture)
                                        + "d, barras " + DiasMax + "d", ColAviso));
                double ivA, emA, tgA;
                lock (_candado) { ivA = _ivAtmUlt; emA = _movEspUlt; tgA = _gexTotalUlt; }
                // el neto CON su escala: sin el total, un neto chico no se
                // distingue de un mercado equilibrado en el filo
                ls.Add(Tuple.Create(tgA > 0
                    ? "net   " + M(neto) + "  de " + Magnitud(tgA)
                    : "net   " + M(neto), colNet));
                if (ivA > 0)
                    ls.Add(Tuple.Create(string.Format(CultureInfo.GetCultureInfo("es-AR"),
                        "IV {0:N1}%   mov esp +-{1:N1}", ivA * 100.0, emA), ColTexto));
                // la procedencia viaja CON los numeros, no en otra caja: un
                // nivel sin fuente no se publica, y separarlos hacia que el ojo
                // tuviera que cruzar el grafico para saber de donde salio
                var cc = _cUsada ?? _c;
                // EL ATRASO QUE SE PUBLICA ES EL REAL, NO EL NOMINAL.
                //
                // Decia "15 min tarde" fijo, que son los 902 s de CBOE. Pero a
                // eso hay que sumarle la edad del archivo: auditando se vio el
                // feed publicado con 0,6 minutos y el indicador usando uno de
                // hacia catorce, o sea casi treinta minutos de atraso real
                // mientras la pantalla decia quince.
                double atrasoMin = 902.0 / 60.0 + Math.Max(0, cc?.EdadMin ?? 0);
                ls.Add(Tuple.Create(
                    _esFuturo ? "EN VIVO · " + perfil.Count + " strikes"
                              : string.Format(CultureInfo.GetCultureInfo("es-AR"),
                                    "{0:N0} min tarde · {1} strikes", atrasoMin, perfil.Count),
                    _esFuturo ? ColPos : ColAviso));

                // SI EL MAPA NO ES DEL 0DTE, HAY QUE DECIRLO.
                //
                // En MNQ el unico vencimiento listado es el trimestral. Un mapa
                // de 15 dias tiene la gamma repartida y plana: sirve para ver
                // estructura, no para scalpear, porque lo que aprieta intradia
                // es la gamma que vence hoy. Callarlo seria dejar que se opere
                // un mapa distinto del que se cree estar mirando.
                if (_esFuturo && _viva.DiasReales > 1)
                    ls.Add(Tuple.Create("OJO: vence en " + _viva.DiasReales + "d, no es 0DTE", ColAviso));

                // QUE VENCIMIENTO MIRA CADA LADO.
                //
                // Solo si son distintos: si los dos miran lo mismo el renglon
                // no aporta y ocupa lugar. Pero cuando difieren hay que
                // decirlo, porque dos perfiles con distinta forma en la misma
                // pantalla parecen un error si no se sabe que es a proposito.
                // el flujo del dia, que es lo unico que no es de ayer
                double volTot = 0;
                foreach (var nv in perfil) volTot += nv.VolTot;
                if (volTot > 0)
                    ls.Add(Tuple.Create(RotuloFlujo() + "  " + ((int)volTot).ToString("N0",
                        CultureInfo.GetCultureInfo("es-AR")) + " contr", ColFlujo));

                // EL VOLUMEN EN VIVO DE RITHMIC, AL LADO DEL DE CBOE Y CON SU NOMBRE.
                //
                // Son dos libros distintos (SPX arriba, opciones de ES aca) y
                // dos relojes distintos (15 min tarde arriba, en vivo aca). Se
                // muestran los dos porque los dos son verdad, cada uno con su
                // etiqueta. Llega por SecuritySummaryChanged del conector: es
                // el mismo dato que la columna Volume del Options Board. Medido
                // el 2026-09-06: 114 de 180 contratos con volumen, 3.842 en
                // total, dos minutos despues de suscribirse.
                if (_viva.Activa)
                {
                    double vv = _viva.VolumenTotalHoy();
                    int cv = _viva.ContratosConVolumen();
                    ls.Add(Tuple.Create("vivo Rithmic  " + ((int)vv).ToString("N0",
                        CultureInfo.GetCultureInfo("es-AR")) + " contr  " + cv + " strikes",
                        vv > 0 ? ColFlujo : ColAviso));
                }

                // DE DONDE SALE CADA NUMERO, EN UNA LINEA. El operador pregunto
                // si "todo" podia ser en tiempo real: esto es la respuesta
                // honesta, siempre a la vista. El OI es de ayer para todo el
                // mundo; la IV depende del libro elegido; el volumen es vivo.
                {
                    var cu = _cUsada;
                    string iv = cu != null && cu.EsFuturo ? "IV Rithmic vivo" : "IV CBOE retrasada";
                    ls.Add(Tuple.Create("OI de ayer (OCC) · " + iv + " · vol Rithmic vivo",
                                        Color.FromArgb(175, ColTexto)));
                }

                if (VencIzq != VencDer)
                {
                    string V(int m) => m <= 0 ? "0DTE" : m == 1 ? DiasMax + "d" : "todos";
                    ls.Add(Tuple.Create("izq " + V(VencIzq) + " · der " + V(VencDer),
                                        Color.FromArgb(170, ColTexto)));
                }

                var fc = new RenderFont("Consolas", (float)Math.Max(6m, Math.Min(12m, TamTablero - 1m)));
                var medc = ls.Select(l => g.MeasureString(l.Item1, fc)).ToList();
                int wc = 0, hc = 6;
                foreach (var m in medc) { wc = Math.Max(wc, m.Width); hc += m.Height + 1; }
                int xc = TableroDerecha ? area.Right - wc - MargenEje - 46 : area.Left + 8;
                // ATAS entrega un area mas alta que la visible: sin descontar
                // el margen, el ultimo renglon queda detras del eje de tiempo.
                // Y un poco mas arriba todavia para no pisar el reloj de la vela.
                int yc = TableroAbajo
                       ? area.Bottom - hc - Math.Max(6, MargenInferior)
                       : area.Top + 14;
                _tableroRect = new Rectangle(xc, yc, wc + 14, hc);
                g.FillRectangle(Color.FromArgb(205, ColFondo), _tableroRect);
                g.DrawRectangle(new RenderPen(Color.FromArgb(70, ColTexto), 1f), _tableroRect);
                int yyc = yc + 3;
                for (int i = 0; i < ls.Count; i++)
                {
                    g.DrawString(ls[i].Item1, fc, ls[i].Item2, xc + 7, yyc);
                    yyc += medc[i].Height + 1;
                }
                return;
            }

            // SI NO HAY VOLUMEN, NO SE INVENTAN NIVELES.
            //
            // Con la cadena de Rithmic todavia no llega el volumen de opciones
            // (el ultimo negociado viene en cero). Sin volumen, el "major" del
            // bloque salia igual: daba 7.500 con el net gex en 0M, o sea un
            // nivel sin nada atras. Un numero sin respaldo es peor que un
            // guion, porque el guion no engana.
            bool hayVol = Math.Abs(netoV) > 1e-6;
            ls.Add(Tuple.Create(
                _esFuturo ? (hayVol ? "volume   (de hoy, en vivo)"
                                    : "volume   (Rithmic todavia no manda volumen de opciones)")
                          : "volume   (de hoy, 15 min tarde)",
                _esFuturo ? (hayVol ? ColPos : ColAviso) : ColAviso));
            if (hayVol)
            {
                ls.Add(Tuple.Create("  zero gamma      " + P(zeroVol > 0 ? zeroVol : zero), ColZero));
                ls.Add(Tuple.Create("  major positive  " + P(mpv), ColPos));
                ls.Add(Tuple.Create("  major negative  " + P(mnv), ColNeg));
                ls.Add(Tuple.Create("  net gex         " + M(netoV), colNet));
            }
            else
            {
                ls.Add(Tuple.Create("  zero gamma        --", ColTexto));
                ls.Add(Tuple.Create("  major positive    --", ColTexto));
                ls.Add(Tuple.Create("  major negative    --", ColTexto));
                ls.Add(Tuple.Create("  net gex           --", ColTexto));
            }
            ls.Add(Tuple.Create("", ColTexto));

            ls.Add(Tuple.Create("open interest   (de ayer, para todos)", ColAviso));
            ls.Add(Tuple.Create("  zero gamma      " + P(zero), ColZero));
            ls.Add(Tuple.Create("  major positive  " + P(mp), ColPos));
            ls.Add(Tuple.Create("  major negative  " + P(mn), ColNeg));
            ls.Add(Tuple.Create("  net gex         " + M(neto), colNet));
            ls.Add(Tuple.Create("", ColTexto));

            ls.Add(Tuple.Create("max change gex", ColAviso));
            for (int i = 0; i < VentanasTablero.Length; i++)
            {
                var c = cam[i];
                ls.Add(Tuple.Create(string.Format("  {0,2} min  {1,10}  {2,11}",
                    VentanasTablero[i], c.Hay ? P(c.Strike) : "--",
                    c.Hay ? M(c.Delta) : "--"),
                    c.Hay ? colNet : Color.FromArgb(110, ColTexto)));
            }
            ls.Add(Tuple.Create("", ColTexto));

            // ESTADO DE LA GAMMA: el bloque que faltaba.
            //
            // No es un numero mas: es la lectura que ordena la sesion entera.
            // Arriba del zero gamma la mesa amortigua y el dia tiende a rango;
            // abajo, amplifica y tiende a tramos. Que el operador tenga que
            // deducirlo comparando dos numeros de otra fila es justamente lo
            // que este renglon evita.
            bool porEncima; lock (_candado) porEncima = _gammaPositiva;
            ls.Add(Tuple.Create("estado de la gamma", ColAviso));
            ls.Add(Tuple.Create("  gamma           " + M(neto), colNet));
            ls.Add(Tuple.Create(porEncima ? "  POSITIVA  amortigua, tiende a rango"
                                          : "  NEGATIVA  amplifica, tiende a tramo",
                                porEncima ? ColPos : ColNeg));

            var med = ls.Select(l => g.MeasureString(l.Item1.Length == 0 ? " " : l.Item1, fb)).ToList();
            int w = 0, h = 10;
            foreach (var m in med) { w = Math.Max(w, m.Width); h += m.Height + 1; }
            // EL TABLERO NO PUEDE TAPAR EL PRECIO. Arriba a la izquierda queda
            // justo encima de las velas cuando el grafico esta angosto, que es
            // como lo tiene el operador. Por defecto va abajo.
            // SI NO ENTRA, SE ACHICA -- NO SE DESBORDA.
            //
            // Verificado en pantalla: con los cuatro bloques y el grafico
            // partido con un subpanel, las filas de max change quedaban
            // cortadas por el borde de abajo. Un tablero que muestra medio
            // numero es peor que uno que muestra menos filas.
            int disponible = Math.Max(60, area.Height - 16);
            while (h > disponible && ls.Count > 6)
            {
                // se recortan las filas de abajo, que son las menos urgentes
                int ult = ls.Count - 1;
                h -= med[ult].Height + 1;
                ls.RemoveAt(ult); med.RemoveAt(ult);
            }

            // EL TABLERO NO TAPA EL PERFIL.
            //
            // Verificado en pantalla: quedaba justo encima de la franja
            // donde se dibujan las barras de gamma, que es la informacion
            // principal del indicador. Arranca despues de ellas.
            int x = area.Left + (VerGamma ? Math.Max(20, AnchoBarra) + 16 : 8);
            int y = TableroAbajo ? Math.Max(area.Top + 4, area.Bottom - h - 8) : area.Top + 8;
            _tableroRect = new Rectangle(x, y, w + 20, h);
            g.FillRectangle(Color.FromArgb(225, ColFondo), new Rectangle(x, y, w + 20, h));
            g.DrawRectangle(new RenderPen(Color.FromArgb(90, ColTexto), 1f),
                new Rectangle(x, y, w + 20, h));
            int yy = y + 5;
            for (int i = 0; i < ls.Count; i++)
            {
                if (ls[i].Item1.Length > 0)
                    g.DrawString(ls[i].Item1, fb, ls[i].Item2, x + 9, yy);
                yy += med[i].Height + 1;
            }
        }

        private static string Recortar(string s, int n)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "...");


        // ==============================================================
        // Historia: lo acumulado sobrevive al reinicio
        // ==============================================================

        private string RutaHistoria()
        {
            var ins = (InstrumentInfo?.Instrument ?? Raiz()).Replace("#", "");
            var per = (ChartInfo?.ChartType ?? "") + "-" + (ChartInfo?.TimeFrame ?? "");
            return Historia.Ruta(ins, per);
        }

        /// <summary>
        /// Vuelca lo acumulado. NO guarda ningun nivel calculado: solo el
        /// registro de lo que ya se midio. El zero gamma, los muros y el perfil
        /// se rehacen desde la cadena en cada tick y no leen nada de aca.
        /// </summary>
        private void GuardarHistoria()
        {
            if (!PersistirHistoria) return;
            try
            {
                var p = new Historia.Paquete
                {
                    Guardado = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture),
                    Instrumento = (InstrumentInfo?.Instrument ?? "").Replace("#", ""),
                };

                lock (_porBarra)
                    foreach (var m in _porBarra.Values)
                    {
                        if (!m.Hay || m.Hora == default(DateTime)) continue;
                        p.Marcas.Add(new Historia.MarcaDto
                        {
                            T = Historia.AUnix(m.Hora),
                            Mp = m.MajorPos, Mn = m.MajorNeg, Z = m.Zero,
                            D = m.Doms, I = m.Incs,
                        });
                    }

                lock (_pelotitas)
                    foreach (var q in _pelotitas)
                        p.Pelotitas.Add(new Historia.PelotitaDto
                        {
                            T = Historia.AUnix(q.Hora), K = q.Strike,
                            Fut = q.Fut, Delta = q.Delta, Fuerza = q.Fuerza,
                        });

                // EL RASTRO SE DIEZMA ANTES DE GUARDAR.
                //
                // _estela anota un punto en CADA repricing, o sea por tick: en
                // MNQ eran 82.000 puntos y 2,4 MB de archivo. Pero las ventanas
                // que lo consultan son de 1, 5, 10, 15 y 30 minutos, asi que
                // esa resolucion no aporta nada y solo pesa. Se guarda un punto
                // cada 30 segundos, que sigue siendo veinte veces mas fino que
                // la ventana mas corta.
                const int SegRastro = 30;
                lock (_estela)
                    foreach (var kv in _estela)
                    {
                        if (kv.Value == null || kv.Value.Count == 0) continue;
                        var tt = new List<long>(); var vv = new List<double>();
                        long ult = long.MinValue;
                        foreach (var x in kv.Value)
                        {
                            var u = Historia.AUnix(x.Key);
                            if (u - ult < SegRastro) continue;
                            ult = u; tt.Add(u); vv.Add(x.Value);
                        }
                        // el ultimo siempre, que es el valor de ahora
                        var fin = kv.Value[kv.Value.Count - 1];
                        var uf = Historia.AUnix(fin.Key);
                        if (tt.Count == 0 || tt[tt.Count - 1] != uf) { tt.Add(uf); vv.Add(fin.Value); }
                        p.Estela.Add(new Historia.EstelaDto
                        {
                            K = kv.Key, T = tt.ToArray(), V = vv.ToArray(),
                        });
                    }

                Historia.Guardar(RutaHistoria(), p);
            }
            catch (Exception e) { Registrar(e); }
        }

        private void LeerHistoria()
        {
            if (!PersistirHistoria) return;
            try
            {
                var ins = (InstrumentInfo?.Instrument ?? "").Replace("#", "");
                string motivo;
                _histPend = Historia.Leer(RutaHistoria(), ins, HorasHistoria, out motivo);
                Registrar2(_histPend == null
                    ? "historia: " + motivo
                    : "historia: " + _histPend.Marcas.Count + " marcas, "
                      + _histPend.Pelotitas.Count + " pelotitas, "
                      + _histPend.Estela.Count + " strikes con rastro (a mapear)");
            }
            catch (Exception e) { Registrar(e); }
        }

        /// <summary>
        /// Le asigna a cada registro guardado la vela que le corresponde POR
        /// HORA, no por numero. Es la parte que evita el error silencioso: el
        /// numero de vela cambia entre sesiones y restaurar por indice dejaria
        /// cada marca corrida sin dar ningun error.
        ///
        /// Se descarta lo que caiga fuera del rango cargado y lo de la vela en
        /// formacion, que se mide en vivo.
        /// </summary>
        private void MapearHistoria()
        {
            if (_histPend == null) return;
            int n = CurrentBar;
            if (n < 10) return;
            if (_histMapeadoEn > 0 && n < _histMapeadoEn * 3 / 2) return;
            _histMapeadoEn = n;

            try
            {
                var t = new long[n];
                for (int i = 0; i < n; i++)
                {
                    try { t[i] = Historia.AUnix(GetCandle(i).Time); }
                    catch { t[i] = 0; }
                }
                if (t[0] == 0 || t[n - 1] == 0) return;

                Func<long, int> Buscar = x =>
                {
                    if (x < t[0] || x > t[n - 1] + 86400) return -1;
                    int lo = 0, hi = n - 1;
                    while (lo < hi)
                    {
                        int m = (lo + hi + 1) / 2;
                        if (t[m] <= x) lo = m; else hi = m - 1;
                    }
                    return lo;
                };

                int puestas = 0;
                lock (_porBarra)
                    foreach (var m in _histPend.Marcas)
                    {
                        int b = Buscar(m.T);
                        if (b < 0 || b >= n - 1) continue;
                        if (_porBarra.ContainsKey(b)) continue;
                        _porBarra[b] = new Marca
                        {
                            Hora = Historia.DeUnix(m.T), Hay = true,
                            MajorPos = m.Mp, MajorNeg = m.Mn, Zero = m.Z,
                            Doms = m.D, Incs = m.I,
                        };
                        puestas++;
                    }

                lock (_pelotitas)
                {
                    var yaHay = new HashSet<long>(_pelotitas.Select(q => Historia.AUnix(q.Hora)));
                    foreach (var q in _histPend.Pelotitas)
                    {
                        if (yaHay.Contains(q.T)) continue;
                        int b = Buscar(q.T);
                        if (b < 0) continue;
                        _pelotitas.Add(new Pelotita
                        {
                            Hora = Historia.DeUnix(q.T), Strike = q.K, Fut = q.Fut,
                            Delta = q.Delta, Fuerza = q.Fuerza, Barra = b,
                        });
                    }
                    _pelotitas.Sort((a, b2) => a.Hora.CompareTo(b2.Hora));
                }

                lock (_estela)
                    foreach (var e in _histPend.Estela)
                    {
                        if (_estela.ContainsKey(e.K)) continue;
                        var l = new List<KeyValuePair<DateTime, double>>();
                        for (int i = 0; i < e.T.Length && i < e.V.Length; i++)
                            l.Add(new KeyValuePair<DateTime, double>(Historia.DeUnix(e.T[i]), e.V[i]));
                        _estela[e.K] = l;
                    }

                _histRestauradas = puestas;
                Registrar2("historia mapeada: " + puestas + " marcas puestas en su vela por hora");
                _histPend = null;
            }
            catch (Exception e) { Registrar(e); _histPend = null; }
        }

        /// <summary>
        /// El volumen de opciones de hoy, al renglon de auditoria.
        ///
        /// Va al log a proposito: el acumulador solo cuenta operaciones que
        /// llegan DESPUES de suscribirse, asi que la unica forma de comprobar
        /// que funciona es verlo crecer a lo largo de una rueda. En la sesion
        /// nocturna da cero y eso no prueba nada -- ni a favor ni en contra.
        /// </summary>
        private string Flujo()
        {
            try
            {
                // DOS NUMEROS DISTINTOS, Y HAY QUE DECIR CUAL ES CUAL.
                //
                // "viva" es lo que acumulo yo contrato por contrato desde
                // Rithmic: solo cuenta operaciones posteriores a la suscripcion
                // y en la sesion nocturna da cero, cosa que no prueba nada.
                // "perfil" es el volumen que trae la cadena en uso, que si viene
                // de CBOE ya llega con el dia entero pero 15 minutos tarde.
                //
                // Publicarlos con el mismo nombre hacia parecer que se
                // contradecian cuando son cosas distintas.
                var v = _viva.VolumenTotalHoy();
                double vp = 0; int strikes = 0;
                lock (_candado)
                    foreach (var n in _perfil) { vp += n.VolTot; if (n.VolTot > 0) strikes++; }
                return string.Format(CultureInfo.InvariantCulture,
                    " flujoviva={0:F0} flujocinta={5:F0} flujoperfil={1:F0} strikesconflujo={2} marcas={3}/{4}",
                    v, vp, strikes, _marcasDibujadas, _marcasConRegistro, _viva.VolumenCintaTotal())
                    + _viva.Diagnostico();
            }
            catch { return " flujohoy=?"; }
        }

        /// <summary>
        /// Los picos interpolados, al renglon de auditoria.
        ///
        /// Sirve para PROBAR que son continuos y no strikes pelados: si
        /// estuvieran pegados a la rejilla, todos caerian en multiplos de 5.
        /// Se publica tambien el resto contra 5 para no tener que mirarlo a ojo.
        /// </summary>
        /// <summary>
        /// Escribe la foto de la cadena viva que se esta usando AHORA.
        ///
        /// Se llama junto con el renglon de auditoria y no cada N segundos por
        /// su cuenta: si los dos sellos no coinciden, el auditor rechaza la
        /// comparacion -- con razon, porque comparar dos cadenas distintas no
        /// prueba nada -- y nunca llega a auditar.
        /// </summary>
        private void VolcarCadenaViva()
        {
            List<CadenaViva.Fila> fs;
            Cadena c;
            lock (_candado) { fs = _vivaFilasCache; c = _cUsada; }
            // SE VUELCA AUNQUE EL LIBRO EN USO SEA EL DE SPX. Antes solo se
            // escribia cuando la cadena viva era la que dibujaba, y con
            // LibroViva apagado el archivo quedo congelado el 2026-09-04 a las
            // 14:02: el volumen en vivo que se arreglo el 06-09 no se podia
            // auditar desde afuera. Ahora la foto sale siempre que la cadena
            // viva este activa, con su propio sello.
            if ((fs == null || fs.Count == 0) && _viva.Activa)
                try { fs = _viva.Instantanea(); } catch { fs = null; }
            if (fs == null || fs.Count == 0) return;
            bool usada = c != null && c.EsFuturo;
            try
            {
                var sb = new System.Text.StringBuilder(1 << 16);
                sb.Append("{\"ts\":\"").Append(usada ? c.Ts : DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                  .Append("\",\"futuro\":").Append((usada ? c.SpotIdx : _viva.Futuro).ToString("0.####", CultureInfo.InvariantCulture))
                  .Append(",\"libro_en_uso\":\"").Append(usada ? "ES" : "SPX")
                  .Append("\",\"campos\":\"strike,dias,es_call,oi,iv,bid,ask,vol_hoy,vol_dia_resumen,vol_cinta,vol_compra,vol_venta,oi_resumen\"")
                  .Append(",\"filas\":[");
                bool primero = true;
                foreach (var f in fs)
                {
                    if (!primero) sb.Append(',');
                    primero = false;
                    sb.Append('[').Append(f.K.ToString("0.##", CultureInfo.InvariantCulture))
                      .Append(',').Append(f.Dias.ToString("0.#####", CultureInfo.InvariantCulture))
                      .Append(',').Append(f.EsCall ? 1 : 0)
                      .Append(',').Append(f.OI.ToString("0.#", CultureInfo.InvariantCulture))
                      .Append(',').Append(f.IV.ToString("0.######", CultureInfo.InvariantCulture))
                      .Append(',').Append(f.Bid.ToString("0.####", CultureInfo.InvariantCulture))
                      .Append(',').Append(f.Ask.ToString("0.####", CultureInfo.InvariantCulture))
                      .Append(',').Append(f.VolumenHoy.ToString("0.#", CultureInfo.InvariantCulture))
                      .Append(',').Append(double.IsNaN(f.VolumenDia) ? "null" : f.VolumenDia.ToString("0.#", CultureInfo.InvariantCulture))
                      .Append(',').Append(f.VolCinta.ToString("0.#", CultureInfo.InvariantCulture))
                      .Append(',').Append(f.VolCompra.ToString("0.#", CultureInfo.InvariantCulture))
                      .Append(',').Append(f.VolVenta.ToString("0.#", CultureInfo.InvariantCulture))
                      .Append(',').Append(double.IsNaN(f.OIResumen) ? "null" : f.OIResumen.ToString("0.#", CultureInfo.InvariantCulture))
                      .Append(']');
                }
                sb.Append("]}");
                File.WriteAllText(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ATAS", "pythiagex-cadena-viva-" + Raiz() + ".json"), sb.ToString());
            }
            catch { }
        }

        private Dictionary<double, (double total, double calls, double puts)> _volVivoCache;

        // ==============================================================
        // LA ESCALERA
        // ==============================================================

        private sealed class Peldano
        {
            public string Nombre = "", Tag = "";
            public double Precio;
            public Color Col;
        }

        /// <summary>Los nodos que publica PythiaFlow por AppDomain, si esta en el
        /// grafico y publico hace menos de diez minutos. Ver PublicarNodos() alla.</summary>
        private List<(double precio, double vol, double delta, int rango, bool flojo)> LeerNodos()
        {
            var salida = new List<(double, double, double, int, bool)>();
            try
            {
                string inst = (InstrumentInfo?.Instrument ?? "").ToUpperInvariant().TrimStart('#');
                var raw = AppDomain.CurrentDomain.GetData("PythiaFlow.Nodos." + inst) as string;
                if (string.IsNullOrEmpty(raw)) return salida;
                var partes = raw.Split(';');
                var inv = CultureInfo.InvariantCulture;
                if (partes.Length < 2 || !long.TryParse(partes[0], NumberStyles.Integer, inv, out var ts)) return salida;
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts > 600) return salida;
                for (int i = 1; i < partes.Length; i++)
                {
                    var c = partes[i].Split('|');
                    if (c.Length < 5) continue;
                    if (!double.TryParse(c[0], NumberStyles.Float, inv, out var p)) continue;
                    double.TryParse(c[1], NumberStyles.Float, inv, out var v);
                    double.TryParse(c[2], NumberStyles.Float, inv, out var d);
                    int.TryParse(c[3], NumberStyles.Integer, inv, out var r);
                    salida.Add((p, v, d, r, c[4] == "1"));
                }
            }
            catch { }
            return salida;
        }

        /// <summary>Desvio estandar de los retornos por vela de las ultimas n velas
        /// cerradas, y la duracion mediana de una vela en minutos. (0,0) si no hay
        /// con que.</summary>
        private (double sigmaVela, double minutosVela) VolRealizada(int n)
        {
            try
            {
                int fin = CurrentBar - 1;
                if (fin < 3) return (0, 0);
                int ini = Math.Max(1, fin - n + 1);
                var rets = new List<double>();
                var durs = new List<double>();
                for (int i = ini; i <= fin; i++)
                {
                    var a = GetCandle(i - 1); var b = GetCandle(i);
                    if (a == null || b == null || a.Close <= 0 || b.Close <= 0) continue;
                    rets.Add(Math.Log((double)(b.Close / a.Close)));
                    double m = (b.Time - a.Time).TotalMinutes;
                    if (m > 0) durs.Add(m);
                }
                if (rets.Count < 10 || durs.Count < 3) return (0, 0);
                double med = rets.Average();
                double var2 = rets.Sum(r => (r - med) * (r - med)) / (rets.Count - 1);
                durs.Sort();
                double dur = durs[durs.Count / 2];
                return (Math.Sqrt(var2), dur);
            }
            catch { return (0, 0); }
        }

        /// <summary>Normal acumulada (Abramowitz-Stegun 7.1.26, error 1,5e-7).</summary>
        private static double Phi(double x)
        {
            double t = 1.0 / (1.0 + 0.2316419 * Math.Abs(x));
            double d = 0.3989422804014327 * Math.Exp(-x * x / 2.0);
            double p = d * t * (0.319381530 + t * (-0.356563782 + t * (1.781477937 + t * (-1.821255978 + t * 1.330274429))));
            return x >= 0 ? 1.0 - p : p;
        }

        /// <summary>
        /// LA ESCALERA: EN QUE PELDANO ESTAS, CUAL ES EL SIGUIENTE.
        ///
        /// Junta en una sola lista, ordenada por distancia al precio, los
        /// niveles que este indicador calcula (zero, muros, rivales, pin, el
        /// vencimiento mas cercano) y los nodos de volumen que publica
        /// PythiaFlow. Muestra los N mas cercanos arriba y los N mas cercanos
        /// abajo, con:
        ///   - el nombre y su color (el mismo que la linea en el grafico)
        ///   - que lo sostiene: "hoy", "2d", "7d" o "vol 24h"
        ///   - la distancia en puntos
        ///   - la probabilidad de TOCARLO dentro del horizonte
        ///
        /// LA PROBABILIDAD, Y POR QUE ES HONESTA. Se calcula con la volatilidad
        /// REALIZADA de las ultimas velas del propio grafico, no con la
        /// implicita de la cadena: la implicita es anual y de noche
        /// sobreestima diez veces lo que el precio se mueve de verdad. La
        /// formula es la del principio de reflexion, P = 2 * Phi(-|ln(K/S)| /
        /// (sigma * raiz(n))), sin deriva. Es un MODELO: dice cuan lejos esta
        /// el nivel medido en "cuanto se movio el precio en la ultima hora".
        /// No es un pronostico y no dice direccion.
        ///
        /// EL PIN. El strike con mas |gamma| dentro del 0,5 % del precio. Es
        /// donde la mesa tiene mas que cubrir si el precio se mueve poco, y por
        /// eso "pega" el precio los dias de mucho 0DTE.
        /// </summary>
        private void Escalera(RenderContext g, Rectangle area)
        {
            decimal px = 0;
            try { px = GetCandle(Math.Max(0, CurrentBar - 1)).Close; } catch { }
            if (px <= 0) return;
            double S = (double)px;
            decimal tick = 0.25m;
            try { if (InstrumentInfo != null && InstrumentInfo.TickSize > 0) tick = InstrumentInfo.TickSize; } catch { }

            List<Nivel> pf; double zero, mp, mn, mpR, mnR;
            lock (_candado) { pf = _perfil; zero = _zeroGamma; mp = _majorPos; mn = _majorNeg; mpR = _mpRival; mnR = _mnRival; }
            if (pf == null || pf.Count == 0) return;

            var es = CultureInfo.GetCultureInfo("es-AR");
            var colNodo = Color.FromArgb(235, 200, 60);
            var cand = new List<Peldano>();

            string TagDe(double fut)
            {
                double mejor = double.MaxValue, dd = double.NaN;
                foreach (var q in pf)
                {
                    double d0 = Math.Abs(q.Fut - fut);
                    if (d0 < mejor) { mejor = d0; dd = q.DiasDom; }
                }
                if (mejor > 3.0 || double.IsNaN(dd) || dd <= 0) return "";
                return dd < 1.0 ? "hoy" : Math.Round(dd).ToString("0", es) + "d";
            }
            void Add(string nombre, double precio, Color col, string tag)
            {
                if (double.IsNaN(precio) || precio <= 0) return;
                cand.Add(new Peldano { Nombre = nombre, Precio = precio, Col = col, Tag = tag ?? "" });
            }

            Add("zero", zero, ColZero, DiasMax + "d");
            Add("+wall", mp, ColPos, TagDe(mp));
            Add("-wall", mn, ColNeg, TagDe(mn));
            Add("+wall?", mpR, ColPos, TagDe(mpR));
            Add("-wall?", mnR, ColNeg, TagDe(mnR));

            // el pin: mas |gamma| dentro del 0,5 % del precio
            double best = 0; Nivel pin = default; bool hayPin = false;
            foreach (var q in pf)
                if (Math.Abs(q.Fut - S) <= S * 0.005 && Math.Abs(q.Gex) > best) { best = Math.Abs(q.Gex); pin = q; hayPin = true; }
            if (hayPin) Add("pin", pin.Fut, pin.Gex >= 0 ? ColPos : ColNeg, TagDe(pin.Fut));

            // el vencimiento mas cercano: su strike mas pesado arriba y abajo
            double dmin = double.MaxValue;
            foreach (var q in pf) if (q.DiasDom > 0 && q.DiasDom < dmin) dmin = q.DiasDom;
            if (dmin < double.MaxValue)
            {
                Nivel ba = default, bb = default; double ga = 0, gb = 0; bool ha = false, hb = false;
                foreach (var q in pf)
                {
                    if (q.DiasDom > dmin + 0.01) continue;
                    if (q.Fut > S && Math.Abs(q.Gex) > ga) { ga = Math.Abs(q.Gex); ba = q; ha = true; }
                    if (q.Fut < S && Math.Abs(q.Gex) > gb) { gb = Math.Abs(q.Gex); bb = q; hb = true; }
                }
                string tv = dmin < 1.0 ? "hoy" : Math.Round(dmin).ToString("0", es) + "d";
                if (ha) Add("venc+", ba.Fut, ba.Gex >= 0 ? ColPos : ColNeg, tv);
                if (hb) Add("venc-", bb.Fut, bb.Gex >= 0 ? ColPos : ColNeg, tv);
            }

            // los nodos de volumen, si PythiaFlow esta en el grafico
            var nodos = LeerNodos();
            foreach (var nd in nodos)
                if (!nd.flojo) Add("nodo #" + nd.rango, nd.precio, colNodo, "vol 24h");

            // sin duplicados: dos peldanos a menos de un tick son el mismo
            var lista = new List<Peldano>();
            foreach (var c in cand.OrderBy(c => Math.Abs(c.Precio - S)))
            {
                var igual = lista.FirstOrDefault(x => Math.Abs(x.Precio - c.Precio) < (double)tick);
                if (igual != null) { if (!igual.Nombre.Contains(c.Nombre)) igual.Nombre += "+" + c.Nombre; continue; }
                lista.Add(c);
            }
            var arriba = lista.Where(c => c.Precio >= S).OrderBy(c => c.Precio).Take(EscaleraPorLado).Reverse().ToList();
            var abajo  = lista.Where(c => c.Precio <  S).OrderByDescending(c => c.Precio).Take(EscaleraPorLado).ToList();
            if (arriba.Count + abajo.Count == 0) return;

            // la probabilidad de toque en el horizonte, con la vol realizada
            var (sigVela, minVela) = VolRealizada(Math.Max(10, EscaleraVelasVol));
            double sigH = 0;
            if (sigVela > 0 && minVela > 0)
                sigH = sigVela * Math.Sqrt(Math.Max(1.0, HorizonteProbMin / minVela));
            string Prob(double K)
            {
                if (sigH <= 0) return "  --";
                double z = Math.Abs(Math.Log(K / S)) / sigH;
                double p = Math.Min(1.0, Math.Max(0.0, 2.0 * Phi(-z)));
                return (p * 100).ToString("0", es).PadLeft(3) + "%";
            }
            string Fila(Peldano c, bool esArriba)
            {
                double d = c.Precio - S;
                bool aca = Math.Abs(d) <= (double)tick * 2;
                string nom = (c.Nombre + (c.Tag.Length > 0 ? " " + c.Tag : ""));
                if (nom.Length > 15) nom = nom.Substring(0, 15);
                return string.Format(es, "{0} {1,-15} {2,9:N2} {3,5} {4}{5}",
                    esArriba ? "▲" : "▼", nom, c.Precio,
                    (d >= 0 ? "+" : "") + d.ToString("0", es), Prob(c.Precio),
                    aca ? "  ◀ aca" : "");
            }

            var f = new RenderFont("Consolas", (float)Math.Max(6m, Math.Min(12m, TamTablero - 1m)));
            var filas = new List<Tuple<string, Color, bool>>();
            string cab = string.Format(es, "ESCALERA  toque en {0} min · vol {1} velas",
                                       HorizonteProbMin, EscaleraVelasVol);
            filas.Add(Tuple.Create(cab, ColAviso, false));
            foreach (var c in arriba) filas.Add(Tuple.Create(Fila(c, true), c.Col, Math.Abs(c.Precio - S) <= (double)tick * 2));
            filas.Add(Tuple.Create(string.Format(es, "── precio {0,9:N2} ──", S), ColTexto, false));
            foreach (var c in abajo) filas.Add(Tuple.Create(Fila(c, false), c.Col, Math.Abs(c.Precio - S) <= (double)tick * 2));
            if (nodos.Count == 0)
                filas.Add(Tuple.Create("(sin nodos: PythiaFlow no esta en este grafico)", Color.FromArgb(150, ColTexto), false));

            var med = filas.Select(l => g.MeasureString(l.Item1, f)).ToList();
            int w = 0, h = 6;
            foreach (var m in med) { w = Math.Max(w, m.Width); h += m.Height + 1; }
            int x, y;
            if (!_tableroRect.IsEmpty && VerTablero)
            {
                x = _tableroRect.Right - (w + 14);
                y = _tableroRect.Top - h - 6;
            }
            else
            {
                x = area.Right - w - MargenEje - 46;
                y = area.Bottom - h - Math.Max(6, MargenInferior);
            }
            if (x < area.Left + 4) x = area.Left + 4;
            if (y < area.Top + 4) y = area.Top + 4;
            var rect = new Rectangle(x, y, w + 14, h);
            g.FillRectangle(Color.FromArgb(205, ColFondo), rect);
            g.DrawRectangle(new RenderPen(Color.FromArgb(70, ColTexto), 1f), rect);
            int yy = y + 3;
            for (int i = 0; i < filas.Count; i++)
            {
                if (filas[i].Item3)
                    g.FillRectangle(Color.FromArgb(45, filas[i].Item2), new Rectangle(x + 2, yy - 1, w + 10, med[i].Height + 2));
                g.DrawString(filas[i].Item1, f, filas[i].Item2, x + 7, yy);
                yy += med[i].Height + 1;
            }
        }

        /// <summary>
        /// EL VOLUMEN VIVO DE OPCIONES, POR STRIKE, EN EL BORDE DERECHO.
        ///
        /// Un circulo por strike con contratos operados HOY en las opciones de
        /// ES (resumen del conector de Rithmic, ver CadenaViva). El radio va con
        /// la raiz del volumen contra el mayor, como los BigTrades, para que un
        /// strike enorme no convierta a los demas en un pixel. Los tres mayores
        /// llevan el numero. Los strikes de ES ya son precio de futuro: sin base.
        ///
        /// Es lo unico del mapa que no es de ayer. Lo que NO dice: direccion.
        /// Dice donde se esta armando o cerrando posicion hoy.
        /// </summary>
        private void VolumenVivo(RenderContext g, IChartContainer cont, int x1)
        {
            Dictionary<double, (double total, double calls, double puts)> vv;
            lock (_candado) vv = _volVivoCache;
            if (vv == null || vv.Count == 0)
            {
                // primer render antes del primer minuto: se pide una vez
                try { if (_viva.Activa) { vv = _viva.VolumenPorStrike(); lock (_candado) _volVivoCache = vv; } } catch { }
                if (vv == null || vv.Count == 0) return;
            }
            double mx = 0;
            foreach (var kv in vv) if (kv.Value.total > mx) mx = kv.Value.total;
            if (mx <= 0) return;
            var top = vv.OrderByDescending(k => k.Value.total).Take(3).Select(k => k.Key).ToHashSet();
            var f = new RenderFont("Arial", 7.5f);
            int xc = x1 - 12;
            foreach (var kv in vv)
            {
                int y;
                try { y = cont.GetYByPrice((decimal)kv.Key, false); } catch { continue; }
                if (y < ChartArea.Top + 4 || y > ChartArea.Bottom - 4) continue;
                int r = 2 + (int)Math.Round(6 * Math.Sqrt(kv.Value.total / mx));
                // el color dice de que lado del strike se opero mas: calls o puts
                var col = kv.Value.calls >= kv.Value.puts ? ColPos : ColNeg;
                g.FillEllipse(Color.FromArgb(150, ColFlujo), new Rectangle(xc - r, y - r, 2 * r, 2 * r));
                g.DrawEllipse(new RenderPen(Color.FromArgb(200, col), 1f), new Rectangle(xc - r, y - r, 2 * r, 2 * r));
                if (top.Contains(kv.Key))
                {
                    var t = ((int)kv.Value.total).ToString("N0", CultureInfo.GetCultureInfo("es-AR"));
                    var m = g.MeasureString(t, f);
                    g.DrawString(t, f, Color.FromArgb(220, ColFlujo), xc - r - m.Width - 3, y - m.Height / 2);
                }
            }
        }

        /// <summary>El muro disputado, para el renglon de auditoria: cuanto pesa
        /// el segundo contra el primero de cada lado y cual es.</summary>
        private string Disputa()
        {
            double a, b, ra, rb;
            lock (_candado) { a = _mpRival; b = _mnRival; ra = _mpRatio; rb = _mnRatio; }
            var inv = CultureInfo.InvariantCulture;
            return string.Format(inv, " mpratio={0:F2} mpdisp={1} mnratio={2:F2} mndisp={3}",
                ra, double.IsNaN(a) ? "no" : a.ToString("F2", inv),
                rb, double.IsNaN(b) ? "no" : b.ToString("F2", inv));
        }

        /// <summary>Cada zona del radar con la gamma que le dio el radar (py) y la
        /// que le da este indicador (cs), en millones. Cuando difieren mucho es el
        /// horizonte, y el numero de horizonte va adelante.</summary>
        private string ZonasAudit()
        {
            List<ZonaDom> zs;
            lock (_zonas) zs = new List<ZonaDom>(_zonas);
            double hz = _horizonteZonas;
            var inv = CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder(" zonashorizonte=")
                .Append(double.IsNaN(hz) ? "?" : hz.ToString("0", inv))
                .Append(" zonas=");
            bool primero = true;
            foreach (var z in zs)
            {
                if (double.IsNaN(z.Idx)) continue;
                if (!primero) sb.Append(';');
                primero = false;
                sb.Append(z.Idx.ToString("0", inv)).Append(':')
                  .Append(double.IsNaN(z.GexPy) ? "?" : (z.GexPy / 1e6).ToString("+0;-0", inv)).Append("Mpy/")
                  .Append(double.IsNaN(z.GexCs) ? "?" : (z.GexCs / 1e6).ToString("+0;-0", inv)).Append("Mcs");
            }
            if (primero) sb.Append("ninguna");
            return sb.ToString();
        }

        /// <summary>
        /// EL ROTULO DEL FLUJO DICE DE QUE DIA ES.
        ///
        /// Decia "flujo hoy" siempre. El domingo 2026-09-06 a la noche la
        /// cadena de CBOE tenia el sello del domingo pero el ultimo trade era
        /// del viernes 4 a las 16:14 (verificado en el crudo): los 622.725
        /// contratos eran del viernes y la pantalla decia "hoy". Si el feed
        /// trae el ultimo trade, el rotulo lleva ese dia; si no, la hora de la
        /// cadena; y "hoy" solo si el ultimo trade es de hoy en Nueva York.
        /// </summary>
        private string RotuloFlujo()
        {
            var c = _c;
            if (c == null) return "flujo";
            var inv = CultureInfo.InvariantCulture;
            DateTime t;
            if (!string.IsNullOrEmpty(c.UltimoTrade)
                && DateTime.TryParse(c.UltimoTrade, inv, DateTimeStyles.None, out t))
            {
                DateTime hoyNy;
                try
                {
                    var ny = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                    hoyNy = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ny).Date;
                }
                catch { hoyNy = DateTime.UtcNow.AddHours(-4).Date; }
                string dia = t.Date == hoyNy ? "hoy" : DiaCorto(t) + " " + t.ToString("dd/MM", inv);
                return "flujo " + dia + " " + t.ToString("HH:mm", inv) + " NY";
            }
            if (DateTime.TryParseExact(c.Ts ?? "", "yyyy-MM-dd HH:mm:ss", inv,
                                       DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out t))
                return "flujo cadena " + t.ToLocalTime().ToString("dd/MM HH:mm", inv);
            return "flujo cadena";
        }

        private static string DiaCorto(DateTime t)
        {
            switch (t.DayOfWeek)
            {
                case DayOfWeek.Monday: return "lun";
                case DayOfWeek.Tuesday: return "mar";
                case DayOfWeek.Wednesday: return "mie";
                case DayOfWeek.Thursday: return "jue";
                case DayOfWeek.Friday: return "vie";
                case DayOfWeek.Saturday: return "sab";
                default: return "dom";
            }
        }

        private string Picos()
        {
            double[] pp;
            lock (_candado) pp = _picosUlt;
            if (pp == null || pp.Length == 0) return " picos=ninguno";
            var sb = new System.Text.StringBuilder(" picos=");
            for (int i = 0; i < pp.Length; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append(pp[i].ToString("F3", CultureInfo.InvariantCulture));
            }
            // CONTRA LOS STRIKES DE VERDAD, no contra una rejilla supuesta.
            //
            // Antes esto comparaba p/5 contra un entero, y estaba mal por dos
            // motivos a la vez. Primero, p es el precio de FUTURO, que ya lleva
            // la base sumada: con base 9,42 un strike exacto de 7825 sale
            // 7834,42 y jamas es multiplo de 5. Segundo, ni siquiera todos los
            // productos tienen strikes de a 5 -- NQ no los tiene -- asi que la
            // rejilla de 5 era una suposicion mia disfrazada de medicion.
            //
            // Daba 0 de 6 justo cuando los picos SI estaban sobre los strikes.
            // Un control que dice que fallo cuando funciono es peor que no
            // tener control, porque manda a buscar el problema al lugar
            // equivocado.
            double[] ff;
            lock (_candado) ff = _futsUlt;
            int enRejilla = 0;
            if (ff != null && ff.Length > 0)
                foreach (var p in pp)
                    if (ff.Any(k => Math.Abs(k - p) < 0.001)) enRejilla++;
            sb.Append(" picos_en_rejilla=").Append(enRejilla).Append('/').Append(pp.Length);
            double gp, gn;
            lock (_candado) { gp = _mpGlobal; gn = _mnGlobal; }
            // LOS MAJORS POR VOLUMEN, AL RENGLON DE AUDITORIA.
            //
            // HIPOTESIS QUE ESTO SIRVE PARA PROBAR O TIRAR ABAJO. Los niveles
            // de INTERES ABIERTO no se pueden mover en todo el dia: la OCC
            // consolida el OI de noche, asi que el strike ganador es el mismo
            // de la apertura al cierre. Por eso nuestras marcas salen planas
            // -- estamos dibujando justo el unico de los tres que esta
            // congelado por definicion.
            //
            // El VOLUMEN, en cambio, se acumula durante la rueda. Si el strike
            // con mas gamma por volumen cambia varias veces por dia, ESO dibuja
            // una escalera, sin necesidad de inventar ningun promedio.
            //
            // No se dibuja nada todavia: primero se mide. Si mpv/mnv resultan
            // tan quietos como majorpos/majorneg, la hipotesis es falsa y hay
            // que buscar por otro lado.
            double mvp, mvn;
            lock (_candado) { mvp = _majorPosVol; mvn = _majorNegVol; }
            double zv;
            lock (_candado) zv = _zeroVol;
            sb.Append(" majorposvol=").Append(mvp.ToString("F2", CultureInfo.InvariantCulture))
              .Append(" majornegvol=").Append(mvn.ToString("F2", CultureInfo.InvariantCulture))
              .Append(" zerovol=").Append(double.IsNaN(zv) ? "sindato"
                     : zv.ToString("F2", CultureInfo.InvariantCulture));
            double mc;
            lock (_candado) { mc = _maxChangeUlt; }
            double za; int sa;
            lock (_candado) { za = _zeroAnchoUlt; sa = _strikesAnchoUlt; }
            sb.Append(" zeroancho=").Append(double.IsNaN(za) ? "sindato"
                     : za.ToString("F2", CultureInfo.InvariantCulture))
              .Append(" strikesancho=").Append(sa);
            sb.Append(" maxchange=").Append(double.IsNaN(mc) ? "sindato"
                     : mc.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(" maxglobal=").Append(gp.ToString("F2", CultureInfo.InvariantCulture))
              .Append(" minglobal=").Append(gn.ToString("F2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>Mediana del atraso del libro, en ms, para el renglon de auditoria.</summary>
        private string AtrasoDom()
        {
            // EL DESFASE DEL RELOJ SE DESCUENTA, Y SE PUBLICA.
            //
            // El atraso crudo es hora de la maquina menos hora del servidor.
            // Si la maquina atrasa, sale negativo: el 2026-09-06 el AUDIT decia
            // lagdom_ms=-3158 con w32tm dando +3,27 s de desfase. Un atraso
            // negativo es imposible y publicarlo era una mentira por omision.
            // Reloj.Medir lo mide contra NTP al arrancar y cada media hora; si
            // no pudo, se publica el crudo con la marca "sin corregir".
            double des = Reloj.DesfaseMs;
            string reloj = double.IsNaN(des)
                ? " reloj=sinmedir"
                : string.Format(CultureInfo.InvariantCulture, " reloj_ms={0:F0}", des);
            List<double> c;
            lock (_atrasoDom)
            {
                if (_atrasoDom.Count < 20) return " lagdom=sinmuestra" + reloj;
                c = new List<double>(_atrasoDom);
            }
            c.Sort();
            double mediana = c[c.Count / 2];
            double p95 = c[Math.Min(c.Count - 1, (int)(c.Count * 0.95))];
            if (!double.IsNaN(des)) { mediana += des; p95 += des; }
            return string.Format(CultureInfo.InvariantCulture,
                " lagdom_ms={0:F0} lagdom_p95_ms={1:F0} lagdom_n={2}{3}{4}",
                mediana, p95, c.Count, reloj, double.IsNaN(des) ? " lagdom_sin_corregir=1" : "");
        }

        /// <summary>
        /// LA HORA DEL GRAFICO, NO LA DE LA PARED.
        ///
        /// Todo lo que mide "hace cuanto" -- la estela, el Max Change, la poda
        /// del historial -- tiene que usar el mismo reloj que las velas. Con el
        /// reloj real, en Market Replay las velas avanzan por 2013 mientras el
        /// reloj marca hoy: cada ventana da "hace tres mil dias" y no se dibuja
        /// nada, o peor, se dibuja mal y parece que funciona.
        ///
        /// En vivo la ultima vela es de hace segundos, asi que devuelve
        /// practicamente lo mismo que DateTime.UtcNow y no cambia nada.
        ///
        /// Lo que importa no es que sea la hora "correcta" sino que sea LA
        /// MISMA al guardar y al leer: las ventanas son diferencias.
        /// </summary>
        private DateTime Ahora()
        {
            try
            {
                var c = GetCandle(Math.Max(0, CurrentBar - 1));
                if (c != null && c.LastTime != default(DateTime)) return c.LastTime;
                if (c != null && c.Time != default(DateTime)) return c.Time;
            }
            catch { }
            return DateTime.UtcNow;
        }

        private static void Registrar2(string msg)
        {
            try
            {
                var p = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ATAS", "pythiagex-gammavivo.log");
                File.AppendAllText(p, DateTime.Now.ToString("s") + "  " + msg + "\n");
            }
            catch { }
        }

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
