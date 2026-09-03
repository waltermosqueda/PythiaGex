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
        }

        /// <summary>Lo que sale de repreciar: un renglon por strike.</summary>
        private struct Nivel
        {
            public double K;       // strike en indice
            public double Fut;     // el mismo strike en precio de futuro
            public double Gex;     // exposicion gamma por interes abierto
            public double GexVol;  // lo mismo pero sobre el volumen del dia
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

        private sealed class ZonaDom
        {
            public double Fut, Desde, Hasta;
            public string Caracter = "", Lado = "", Criollo = "";
            public double Incentivo;
            public bool Relevante;
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
            public double MajorPos, MajorNeg, Zero;
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
        private bool _esFuturo;
        private string _fuente = "CBOE";
        // la cadena que REALMENTE se uso en el ultimo repricing: la cinta
        // leia _c y decia "CBOE, 15 min tarde" mientras el calculo iba con
        // la viva. Dos renglones de la misma pantalla se contradecian.
        private Cadena _cUsada;
        private readonly CadenaViva _viva = new();
        private bool _vivaPedida;
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

        [Display(Name = "Dias de vencimiento a incluir", GroupName = "Calculo", Order = 50)]
        public int DiasMax { get; set; } = 7;

        [Display(Name = "Tasa anual (para Black-Scholes)", GroupName = "Calculo", Order = 51)]
        public decimal Tasa { get; set; } = 0.0375m;

        [Display(Name = "Ver perfil de gamma (izquierda)", GroupName = "Dibujo", Order = 60)]
        public bool VerGamma { get; set; } = true;

        [Display(Name = "Ver aceleracion (derecha)", GroupName = "Dibujo", Order = 61)]
        public bool VerAcel { get; set; } = true;

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

        [Display(Name = "Ver los titulos de las mitades", GroupName = "Dibujo", Order = 69)]
        public bool VerTitulos { get; set; } = false;

        [Display(Name = "Alto de barra (px, 0 = automatico)", GroupName = "Dibujo", Order = 63)]
        public int AltoBarra { get; set; } = 0;

        [Display(Name = "Marcar los niveles fuera de pantalla", GroupName = "Dibujo", Order = 68,
                 Description = "Si un nivel quedo arriba o abajo del rango visible, lo avisa en el borde.")]
        public bool MarcarFueraDePantalla { get; set; } = true;

        [Display(Name = "Ver Zero Gamma y Majors", GroupName = "Dibujo", Order = 64)]
        public bool VerLineas { get; set; } = true;

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

        [Display(Name = "Ver las dominantes como puntos", GroupName = "Dominantes", Order = 82,
                 Description = "Un punto por vela al nivel dominante. Asi forman las bandas.")]
        public bool VerPuntosDominantes { get; set; } = true;

        [Display(Name = "Alto del punto (px)", GroupName = "Dominantes", Order = 83)]
        public int AltoPunto { get; set; } = 4;

        [Display(Name = "Ancho del punto (px)", GroupName = "Dominantes", Order = 84)]
        public int AnchoPunto { get; set; } = 11;

        [Display(Name = "Punto de la dominante", GroupName = "Colores", Order = 87)]
        public Color ColPuntoDom { get; set; } = Color.FromArgb(232, 168, 56);

        [Display(Name = "Punto del Zero Gamma", GroupName = "Colores", Order = 88)]
        public Color ColPuntoZero { get; set; } = Color.FromArgb(225, 228, 232);

        [Display(Name = "Ver Max Change (las pelotitas)", GroupName = "Max Change", Order = 100)]
        public bool VerPelotitas { get; set; } = true;

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
        public bool VerBigTrades { get; set; } = true;

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
                 Description = "Donde estuvo ese strike antes. Adentro = encogio, afuera = crecio.")]
        public bool VerEstela { get; set; } = false;

        [Display(Name = "Cuantos circulos por barra", GroupName = "Perfil", Order = 54)]
        public int CirculosPorBarra { get; set; } = 3;

        [Display(Name = "Tamano del circulo (px)", GroupName = "Perfil", Order = 55)]
        public int TamCirculo { get; set; } = 9;

        [Display(Name = "Circulo de la estela (izquierda)", GroupName = "Colores", Order = 86)]
        public Color ColCircIzq { get; set; } = Color.FromArgb(60, 90, 235);

        [Display(Name = "Circulo de la estela (derecha)", GroupName = "Colores", Order = 89)]
        public Color ColCircDer { get; set; } = Color.FromArgb(225, 228, 234);

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
            try { _viva.Dispose(); } catch { }
        }

        /// <summary>El repricing va aca, no en el timer: OnCalculate corre con
        /// cada barra nueva y con cada tick de la ultima, que es exactamente la
        /// cadencia a la que tiene que latir el perfil.</summary>
        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar != CurrentBar - 1) return;
            try { Repreciar(); } catch (Exception e) { Registrar(e); }
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
                if (f.EsCall) { fila.OiC = f.OI; fila.IvC = f.IV; }
                else          { fila.OiP = f.OI; fila.IvP = f.IV; }
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
                };

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
                                if (fut == null || d1 == null || d2 == null) continue;
                                if (_zonas.Any(x => Math.Abs(x.Fut - fut.Value) < 0.01)) continue;
                                _zonas.Add(new ZonaDom
                                {
                                    Fut = fut.Value, Desde = d1.Value, Hasta = d2.Value,
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
            return (gC * f.VolC - gP * f.VolP) * MULT * S * S * 0.01;
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
            // LA VIVA MANDA CUANDO ESTA.
            //
            // El operador scalpea en minutos y segundos y dijo que 902 s de
            // retraso no le sirven. Tiene razon: un dato que llega tarde obliga
            // a confiar en que nada cambio en el medio, y esa confianza no se
            // puede auditar. Cuando Rithmic esta entregando, se usa Rithmic.
            var c = ArmarDesdeViva() ?? _c;
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
                var T = Math.Max(dias, 0.02) / 365.0;

                var g = GexStrike(f, S, T, r);
                var gUp = GexStrike(f, Sup, T, r);
                var gv = GexVolStrike(f, S, T, r);
                if (g == 0 && gUp == 0 && gv == 0) continue;

                if (!porStrike.TryGetValue(f.K, out var n))
                    n = new Nivel { K = f.K, Gex = 0, GexVol = 0, Acel = 0 };
                // el perfil de gamma (izquierda) y el de convexidad (derecha)
                // se llenan cada uno con SU vencimiento
                if (enIzq) { n.Gex += g; n.GexVol += gv; }
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
            var picos = new List<(double Fut, double Peso)>();
            if (perfil.Count < 3) lock (_candado) _picosUlt = null;   // no dejar los viejos colgados
            if (perfil.Count >= 3)
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
                picos.Sort((u, v) => v.Peso.CompareTo(u.Peso));
                if (picos.Count > 6) picos.RemoveRange(6, picos.Count - 6);
                lock (_candado) _picosUlt = picos.Select(p => p.Fut).ToArray();
            }

            // Guardar la foto de cada strike para poder dibujar la estela.
            // Se poda a 35 minutos: mas atras no lo pide ninguna ventana y el
            // diccionario crece sin techo con el grafico abierto todo el dia.
            var ahora = DateTime.UtcNow;
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
            if (perfil.Count > 0)
            {
                foreach (var n3 in perfil) netoVol += n3.GexVol;
                mpv = perfil.Aggregate((a, b) => a.GexVol >= b.GexVol ? a : b).K;
                mnv = perfil.Aggregate((a, b) => a.GexVol <= b.GexVol ? a : b).K;
                if (!double.IsNaN(baseUsada)) { mpv += baseUsada; mnv += baseUsada; }
            }

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
                        MajorPos = mp, MajorNeg = mn,
                        Zero = double.IsNaN(zero) ? 0
                             : (double.IsNaN(baseUsada) ? zero : zero + baseUsada),
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
                    Registrar2(string.Format(CultureInfo.InvariantCulture,
                        "AUDIT spot_idx={0:F4} base={1:F4} origen=" + _baseOrigen.Replace(" ", "_") + " strikes={2} visibles=" + _visiblesUlt + " " +
                        "zero={3:F4} majorpos={4:F4} majorneg={5:F4} netgex={6:F6} netgexvol={7:F6} diasmax={8} " +
                        "cadenafilas={9} cadenats={10}" + AtrasoDom() + Picos(),
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

        private bool _latido;

        private void Pintar(RenderContext g)
        {
            if (ChartInfo == null) return;
            try { g.SetSmoothingMode(OFT.Rendering.Context.RenderSmoothingModes.AntiAlias); } catch { }
            _etiquetasUsadas.Clear();
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
            lock (_candado)
            {
                perfil = _perfil; mx = _maxGex; mxA = _maxAcel;
                zero = _zeroGamma; mp = _majorPos; mn = _majorNeg;
                neto = _netGex; spot = _spotUsado;
            }

            if (VerTablero) Tablero(g, area);
            if (VerCinta) Cinta(g, x0, area, perfil.Count, neto, spot);
            if (perfil.Count == 0 || mx <= 0) return;

            var cont = ChartInfo.PriceChartContainer;
            var c = _cUsada ?? _c;   // la que de verdad se uso, no la de CBOE por defecto
            var br = _baseResuelta;
            var baseUsada = br == null ? double.NaN : (double)br;

            // Si no hay base confiable no se dibuja NADA sobre el grafico. Un
            // nivel de SPX puesto crudo sobre el ES esta unos veinte puntos
            // corrido, y eso es una perdida sistematica en cada operacion.
            if (double.IsNaN(baseUsada)) return;

            int alto = AltoBarra > 0 ? AltoBarra : AltoAutomatico(cont, perfil);
            int ancho = Math.Max(20, AnchoBarra);

            // LA ESCALA SE NORMALIZA CONTRA LO QUE SE VE, NO CONTRA TODA LA CADENA.
            //
            // Con el maximo global, un strike enorme y lejano se lleva todo el
            // ancho y los strikes de al lado del precio quedan en un pixel: o
            // sea invisibles, que es exactamente lo que pasaba. Verificado en
            // pantalla el 2026-09-03: 146 strikes calculados y ni una barra
            // dibujada.
            //
            // Renormalizar contra los strikes VISIBLES hace que el perfil
            // siempre use el ancho completo y que la estructura de al lado del
            // precio -- la unica que se puede operar hoy -- se lea.
            double mxV = 0, mxAV = 0;
            int visibles = 0;
            foreach (var n in perfil)
            {
                int yy;
                try { yy = cont.GetYByPrice((decimal)n.Fut, false); }
                catch { continue; }
                if (yy < area.Top - 6 || yy > area.Bottom + 6) continue;
                visibles++;
                mxV = Math.Max(mxV, Math.Abs(n.Gex));
                mxAV = Math.Max(mxAV, Math.Abs(n.Acel));
            }
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
                    if (VerEstela) Estela(g, n.K, mx, ancho, x0, y, true);
                }
                if (VerAcel && mxA > 0 && Math.Abs(n.Acel) > 0)
                {
                    double f = Math.Abs(n.Acel) / mxA;
                    int w = Math.Max(1, (int)(f * ancho));
                    var col = n.Acel >= 0 ? ColAcelPos : ColAcelNeg;
                    int al = (int)(OpacidadPerfil * 2.55 * (0.45 + 0.55 * f));
                    g.FillRectangle(Color.FromArgb(Math.Min(255, Math.Max(12, al)), col),
                        new Rectangle(x1 - w, y - alto / 2, w, alto));
                    if (VerEstela) Estela(g, n.K, mxA, ancho, x1, y, false);
                }
            }

            if (VerPuntosDominantes) PuntosDominantes(g, cont, x0, x1);
            if (VerPelotitas) Pelotitas(g, cont, x0, x1);
            if (VerDominantes) Zonas(g, cont, x0, x1);
            if (VerBigTrades) Puntos(g, cont, x0, x1);

            // LAS LINEAS NO CRUZAN EL PERFIL.
            //
            // Cruzarlo mezcla dos lecturas distintas -- cuanta gamma hay en ese
            // strike y donde esta el nivel -- y el ojo tiene que separarlas
            // solo. Empiezan despues del perfil y terminan antes del de la
            // derecha, asi cada cosa ocupa su franja.
            int xl0 = x0 + (VerGamma ? ancho + 6 : 2);
            int xl1 = x1 - (VerAcel ? ancho + 6 : 2);
            if (xl1 - xl0 < 60) { xl0 = x0 + 2; xl1 = x1 - 2; }
            if (VerLineas)
            {
                Linea(g, cont, xl0, xl1, mp, ColPos, "+wall", false, spot, x1);
                Linea(g, cont, xl0, xl1, mn, ColNeg, "-wall", false, spot, x1);
                Linea(g, cont, xl0, xl1, zero, ColZero, "zero", true, spot, x1);
            }

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
                    bool pos = spot > zero;
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

            var elegidos = juntados.Values
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
                            int origen, int y, bool izquierda)
        {
            if (mx <= 0) return;
            List<KeyValuePair<DateTime, double>> h;
            lock (_estela) { if (!_estela.TryGetValue(strike, out h)) return; h = new List<KeyValuePair<DateTime, double>>(h); }
            if (h.Count < 2) return;

            var ahora = DateTime.UtcNow;
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
                int r = Math.Max(4, TamCirculo - i * 2);
                g.FillEllipse(Color.FromArgb(225, col),
                    new Rectangle(cx - r / 2, y - r / 2, r, r));
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

            for (int b = desde; b <= hasta; b++)
            {
                if (!copia.TryGetValue(b, out var m) || !m.Hay) continue;
                int x;
                try { x = cont.GetXByBar(b, false); }
                catch { continue; }
                if (x < x0 - 10 || x > x1 + 10) continue;

                void Punto(double precio, Color col, double fuerza)
                {
                    if (precio <= 0) return;
                    int y;
                    try { y = cont.GetYByPrice((decimal)precio, false); }
                    catch { return; }
                    if (y < ChartArea.Top - 4 || y > ChartArea.Bottom + 4) return;
                    var f = Math.Max(0.25, Math.Min(1.0, fuerza));
                    int ww = Math.Max(3, (int)(w * (0.6 + 0.4 * f)));
                    int hh = Math.Max(2, (int)(h * (0.7 + 0.5 * f)));
                    g.FillRectangle(Color.FromArgb((int)(140 + 115 * f), col),
                        new Rectangle(x - ww / 2, y - hh / 2, ww, hh));
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
            }

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
        private void Linea(RenderContext g, IChartContainer cont, int x0, int x1,
                           double precio, Color col, string nombre, bool grueso,
                           double spot, int xEje)
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

            var f = new RenderFont("Arial", 8.5f);

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
                int yb2 = arriba ? ChartArea.Top + 6
                                 : ChartArea.Bottom - Math.Max(18, MargenInferior + 4);
                var t2 = string.Format(CultureInfo.GetCultureInfo("es-AR"),
                    "{0} {1} {2:N0} ({3} pts)", arriba ? "▲" : "▼", nombre, precio, dist);
                var mm = g.MeasureString(t2, f);
                int xx = xEje - mm.Width - 10;
                // el marcador tampoco puede pisar el tablero
                if (VerTablero && !_tableroRect.IsEmpty
                    && yb2 + mm.Height > _tableroRect.Top && yb2 < _tableroRect.Bottom
                    && xx + mm.Width > _tableroRect.Left)
                    xx = _tableroRect.Left - mm.Width - 12;
                g.FillRectangle(Color.FromArgb(150, ColFondo),
                    new Rectangle(xx, yb2, mm.Width + 8, mm.Height + 2));
                g.DrawString(t2, f, Color.FromArgb(190, col), xx + 4, yb2 + 1);
                return;
            }

            // la linea: fina y translucida, es una referencia y no un borde
            var pluma = new RenderPen(Color.FromArgb(grueso ? 190 : 140, col),
                                      grueso ? 1.6f : 1.1f,
                                      grueso ? System.Drawing.Drawing2D.DashStyle.Dash
                                             : System.Drawing.Drawing2D.DashStyle.Solid);
            g.DrawLine(pluma, x0, y, x1, y);

            // el chip, todo junto, contra el eje
            var txt = string.Format(CultureInfo.GetCultureInfo("es-AR"),
                                    "{0} {1:N2}", nombre, precio);
            var mt = g.MeasureString(txt, f);
            bool hayDist = !string.IsNullOrEmpty(dist);
            var md = hayDist ? g.MeasureString(dist, f) : default;
            int wChip = mt.Width + 10 + (hayDist ? md.Width + 10 : 0);
            int hChip = mt.Height + 3;

            // que no se pisen entre ellos: se corren en vertical y se les deja
            // un tirante fino para saber a que linea pertenecen
            int yTxt = y - hChip / 2;
            while (_etiquetasUsadas.Any(u => Math.Abs(u - yTxt) < hChip + 1))
                yTxt -= hChip + 2;
            _etiquetasUsadas.Add(yTxt);

            // contra el EJE, no contra el final de la linea: la linea ahora
            // termina antes del perfil de la derecha y el chip quedaba
            // flotando en el medio del grafico, que es donde menos sirve.
            int xc = xEje - wChip - 2;
            if (!_tableroRect.IsEmpty && VerTablero
                && yTxt + hChip > _tableroRect.Top && yTxt < _tableroRect.Bottom
                && xc + wChip > _tableroRect.Left)
                xc = _tableroRect.Left - wChip - 6;

            g.FillRectangle(Color.FromArgb(225, ColFondo), new Rectangle(xc, yTxt, wChip, hChip));
            g.DrawRectangle(new RenderPen(Color.FromArgb(150, col), 1f),
                            new Rectangle(xc, yTxt, wChip, hChip));
            // una barrita del color a la izquierda del chip: identifica el
            // nivel sin tener que leer el nombre
            g.FillRectangle(Color.FromArgb(230, col), new Rectangle(xc, yTxt, 3, hChip));
            g.DrawString(txt, f, Color.FromArgb(235, ColTexto), xc + 7, yTxt + 1);
            if (hayDist)
                g.DrawString(dist, f, Color.FromArgb(165, ColTexto),
                             xc + mt.Width + 13, yTxt + 1);

            if (Math.Abs(yTxt + hChip / 2 - y) > 3)
                g.DrawLine(new RenderPen(Color.FromArgb(90, col), 1f),
                           xc + 1, yTxt + hChip / 2, xc + 1, y);
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
            lock (_candado)
            {
                perfil = _perfil; neto = _netGex; zero = _zeroGamma; spot = _spotUsado;
                mp = _majorPos; mn = _majorNeg;
                netoV = _netGexVol; mpv = _majorPosVol; mnv = _majorNegVol;
                cam = (FilaCambio[])_cambios.Clone();
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
                    ls.Add(Tuple.Create(spot > zero ? "GAMMA +  rango" : "GAMMA -  expansion",
                                        spot > zero ? ColPos : ColNeg));
                ls.Add(Tuple.Create("zero  " + P(zero), ColZero));
                ls.Add(Tuple.Create("+wall " + P(mp), ColPos));
                ls.Add(Tuple.Create("-wall " + P(mn), ColNeg));
                ls.Add(Tuple.Create("net   " + M(neto), colNet));
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
            if (fs == null || fs.Count == 0 || c == null || !c.EsFuturo) return;
            try
            {
                var sb = new System.Text.StringBuilder(1 << 16);
                sb.Append("{\"ts\":\"").Append(c.Ts)
                  .Append("\",\"futuro\":").Append(c.SpotIdx.ToString("0.####", CultureInfo.InvariantCulture))
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
                      .Append(']');
                }
                sb.Append("]}");
                File.WriteAllText(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ATAS", "pythiagex-cadena-viva-" + Raiz() + ".json"), sb.ToString());
            }
            catch { }
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
            int enRejilla = 0;
            foreach (var p in pp) if (Math.Abs(p / 5.0 - Math.Round(p / 5.0)) < 0.002) enRejilla++;
            sb.Append(" picos_en_rejilla=").Append(enRejilla).Append('/').Append(pp.Length);
            return sb.ToString();
        }

        /// <summary>Mediana del atraso del libro, en ms, para el renglon de auditoria.</summary>
        private string AtrasoDom()
        {
            List<double> c;
            lock (_atrasoDom)
            {
                if (_atrasoDom.Count < 20) return " lagdom=sinmuestra";
                c = new List<double>(_atrasoDom);
            }
            c.Sort();
            double mediana = c[c.Count / 2];
            double p95 = c[Math.Min(c.Count - 1, (int)(c.Count * 0.95))];
            return string.Format(CultureInfo.InvariantCulture,
                " lagdom_ms={0:F0} lagdom_p95_ms={1:F0} lagdom_n={2}",
                mediana, p95, c.Count);
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
