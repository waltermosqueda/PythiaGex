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

        [Display(Name = "Dias de vencimiento a incluir", GroupName = "Calculo", Order = 50)]
        public int DiasMax { get; set; } = 7;

        [Display(Name = "Tasa anual (para Black-Scholes)", GroupName = "Calculo", Order = 51)]
        public decimal Tasa { get; set; } = 0.0375m;

        [Display(Name = "Ver perfil de gamma (izquierda)", GroupName = "Dibujo", Order = 60)]
        public bool VerGamma { get; set; } = true;

        [Display(Name = "Ver aceleracion (derecha)", GroupName = "Dibujo", Order = 61)]
        public bool VerAcel { get; set; } = true;

        [Display(Name = "Ancho maximo de barra (px)", GroupName = "Dibujo", Order = 62)]
        public int AnchoBarra { get; set; } = 150;

        [Display(Name = "Alto de barra (px, 0 = automatico)", GroupName = "Dibujo", Order = 63)]
        public int AltoBarra { get; set; } = 0;

        [Display(Name = "Marcar los niveles fuera de pantalla", GroupName = "Dibujo", Order = 68,
                 Description = "Si un nivel quedo arriba o abajo del rango visible, lo avisa en el borde.")]
        public bool MarcarFueraDePantalla { get; set; } = true;

        [Display(Name = "Ver Zero Gamma y Majors", GroupName = "Dibujo", Order = 64)]
        public bool VerLineas { get; set; } = true;

        [Display(Name = "Margen del eje de precios (px)", GroupName = "Dibujo", Order = 65)]
        public int MargenEje { get; set; } = 62;

        [Display(Name = "Ver el tablero de datos", GroupName = "Dibujo", Order = 67)]
        public bool VerTablero { get; set; } = true;

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
        public int AltoPunto { get; set; } = 3;

        [Display(Name = "Ancho del punto (px)", GroupName = "Dominantes", Order = 84)]
        public int AnchoPunto { get; set; } = 7;

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
        public int OpacidadZona { get; set; } = 16;

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
        public int TamPunto { get; set; } = 26;

        [Display(Name = "Escribir los contratos adentro", GroupName = "BigTrades", Order = 97)]
        public bool NumeroAdentro { get; set; } = true;

        [Display(Name = "Circulos de la estela", GroupName = "Perfil", Order = 53,
                 Description = "Donde estuvo ese strike antes. Adentro = encogio, afuera = crecio.")]
        public bool VerEstela { get; set; } = true;

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

            _periodo = TimeSpan.FromSeconds(Math.Max(60, SegundosRefresco));
            _tick = () => _ = Bajar();
            SubscribeToTimer(_periodo, _tick);
            _ = Bajar();
        }

        protected override void OnDispose()
        {
            try { if (_tick != null) UnsubscribeFromTimer(_periodo, _tick); } catch { }
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
                            "ATAS", "pythiagex-cadena-usada.json");
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
            var gC = GammaBs(S, f.K, T, f.IvC, r);
            var gP = GammaBs(S, f.K, T, f.IvP, r);
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
            var c = _c;
            if (c == null || c.Filas.Count == 0) return;

            double baseUsada;
            if (BaseManual != 0m)
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

            foreach (var f in c.Filas)
            {
                if (f.V < 0 || f.V >= c.Dias.Length) continue;
                var dias = c.Dias[f.V];
                if (dias > DiasMax) continue;
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
                n.Gex += g;
                n.GexVol += gv;
                n.Acel += (gUp - g);
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
                    double[] dd = null, ii = null;
                    lock (_zonas)
                    {
                        var act = _zonas.Where(z => z.Relevante && z.Fut > 0).ToList();
                        if (act.Count > 0)
                        {
                            dd = act.Select(z => z.Fut).ToArray();
                            ii = act.Select(z => z.Incentivo).ToArray();
                        }
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
                    Registrar2(string.Format(CultureInfo.InvariantCulture,
                        "AUDIT spot_idx={0:F4} base={1:F4} origen=" + _baseOrigen.Replace(" ", "_") + " strikes={2} visibles=" + _visiblesUlt + " " +
                        "zero={3:F4} majorpos={4:F4} majorneg={5:F4} netgex={6:F6} netgexvol={7:F6} diasmax={8} " +
                        "cadenafilas={9} cadenats={10}" + AtrasoDom(),
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
            var c = _c;
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

                if (VerGamma && Math.Abs(n.Gex) > 0)
                {
                    int w = Math.Max(1, (int)(Math.Abs(n.Gex) / mx * ancho));
                    var col = n.Gex >= 0 ? ColPos : ColNeg;
                    g.FillRectangle(Color.FromArgb(215, col),
                        new Rectangle(x0, y - alto / 2, w, alto));
                    if (VerEstela) Estela(g, n.K, mx, ancho, x0, y, true);
                }
                if (VerAcel && mxA > 0 && Math.Abs(n.Acel) > 0)
                {
                    int w = Math.Max(1, (int)(Math.Abs(n.Acel) / mxA * ancho));
                    var col = n.Acel >= 0 ? ColAcelPos : ColAcelNeg;
                    g.FillRectangle(Color.FromArgb(215, col),
                        new Rectangle(x1 - w, y - alto / 2, w, alto));
                    if (VerEstela) Estela(g, n.K, mxA, ancho, x1, y, false);
                }
            }

            if (VerPuntosDominantes) PuntosDominantes(g, cont, x0, x1);
            if (VerPelotitas) Pelotitas(g, cont, x0, x1);
            if (VerDominantes) Zonas(g, cont, x0, x1);
            if (VerBigTrades) Puntos(g, cont, x0, x1);

            if (VerLineas)
            {
                Linea(g, cont, x0, x1, zero, ColZero, "Zero Gamma", true);
                Linea(g, cont, x0, x1, mp, ColPos, "Major Positive", false);
                Linea(g, cont, x0, x1, mn, ColNeg, "Major Negative", false);
            }

            // rotulos de las dos mitades
            var f8 = new RenderFont("Arial", 8f);
            if (VerGamma)
                g.DrawString("EXPOSICION GAMMA", f8, Color.FromArgb(150, ColTexto),
                             x0 + 4, area.Top + 4);
            if (VerAcel)
            {
                var m = g.MeasureString("ACELERACION", f8);
                g.DrawString("ACELERACION", f8, Color.FromArgb(150, ColTexto),
                             x1 - m.Width - 4, area.Top + 4);
            }
        }

        /// <summary>Las zonas dominantes, como bandas al fondo.</summary>
        private void Zonas(RenderContext g, IChartContainer cont, int x0, int x1)
        {
            List<ZonaDom> zs;
            lock (_zonas) zs = new List<ZonaDom>(_zonas);
            foreach (var z in zs)
            {
                int ya, yb;
                try
                {
                    ya = cont.GetYByPrice((decimal)Math.Max(z.Desde, z.Hasta), false);
                    yb = cont.GetYByPrice((decimal)Math.Min(z.Desde, z.Hasta), false);
                }
                catch { continue; }
                if (yb < ChartArea.Top || ya > ChartArea.Bottom) continue;
                if (!z.Relevante && !VerZonasDebiles) continue;
                var col = z.Caracter == "freno" ? ColPos : ColNeg;
                var alfa = Math.Max(0, Math.Min(255, OpacidadZona * 255 / 100));
                int alt = Math.Max(3, yb - ya);
                // Relleno apenas insinuado y BORDE punteado. Verificado en
                // pantalla: con el relleno fuerte, tres zonas de 10 y 20
                // puntos tapaban mas de un tercio del grafico y no se veian
                // las velas. El borde marca donde empieza y termina sin
                // inundar nada.
                g.FillRectangle(Color.FromArgb(z.Relevante ? alfa : alfa / 2, col),
                    new Rectangle(x0, ya, Math.Max(1, x1 - x0), alt));
                var pluma = new RenderPen(Color.FromArgb(z.Relevante ? 150 : 80, col), 1f,
                                          System.Drawing.Drawing2D.DashStyle.Dot);
                g.DrawLine(pluma, x0, ya, x1, ya);
                g.DrawLine(pluma, x0, yb, x1, yb);
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

            var elegidos = visibles
                .OrderByDescending(t => t.Item1.Volumen)
                .Take(Math.Max(1, CuantosDibujar))
                .ToList();

            decimal mayor = 1m;
            foreach (var t in elegidos) if (t.Item1.Volumen > mayor) mayor = t.Item1.Volumen;
            var usados = new List<Tuple<int, int>>();

            foreach (var t in elegidos)
            {
                var b = t.Item1;
                int x = t.Item2, y = t.Item3;

                // CIRCULO CON EL NUMERO DE CONTRATOS ADENTRO.
                //
                // Asi los dibuja el producto original: en las capturas del
                // operador se leen circulos verdes y rojos con "291", "313",
                // "245", "204", "722", "390" adentro. El numero importa: un
                // punto sin numero obliga a adivinar el tamano por el area,
                // que el ojo estima mal.
                var col = b.Lado >= 0 ? ColCompra : ColVenta;
                var r = (int)Math.Max(11, Math.Sqrt((double)(b.Volumen / mayor)) * TamPunto);
                // APILADOS, no encimados. En las capturas se ven varios
                // circulos en vertical sobre la misma vela, cada uno con su
                // numero. Dibujarlos uno arriba del otro tapa los de abajo y
                // se pierde justo lo que hay que ver: cuantos entraron.
                while (usados.Any(u => Math.Abs(u.Item1 - x) < r && Math.Abs(u.Item2 - y) < r))
                    y += r + 2;
                usados.Add(Tuple.Create(x, y));
                // translucido con anillo claro: deja ver la vela debajo
                g.FillEllipse(Color.FromArgb(120, col),
                    new Rectangle(x - r / 2, y - r / 2, r, r));
                g.DrawEllipse(new RenderPen(Color.FromArgb(200, 235, 240, 245), 1.4f),
                    new Rectangle(x - r / 2, y - r / 2, r, r));
                if (NumeroAdentro && r >= 16)
                {
                    var txt = ((int)b.Volumen).ToString(CultureInfo.InvariantCulture);
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

        private void Linea(RenderContext g, IChartContainer cont, int x0, int x1,
                           double precio, Color col, string nombre, bool grueso)
        {
            if (double.IsNaN(precio) || precio <= 0) return;
            int y;
            try { y = cont.GetYByPrice((decimal)precio, false); }
            catch { return; }

            // NIVEL FUERA DE PANTALLA: no se calla, se marca en el borde.
            //
            // Medido en vivo: con la ventana de precio que usa el operador
            // (unos 16 puntos) NINGUN strike entra, porque los de SPX van de
            // 5 en 5 y los Majors del dia estaban a 27 y 73 puntos. El
            // indicador dibujaba en silencio y parecia roto.
            //
            // Ahora, si el nivel quedo arriba o abajo, se pone una flecha
            // pegada al borde con su precio y cuantos puntos falta para
            // llegar. Un nivel que no se ve sigue siendo un nivel.
            if (y < ChartArea.Top || y > ChartArea.Bottom)
            {
                if (!MarcarFueraDePantalla) return;
                bool arriba = y < ChartArea.Top;
                int yb2 = arriba ? ChartArea.Top + 8 : ChartArea.Bottom - 16;
                var fa = new RenderFont("Arial", 8.5f);
                decimal px = 0;
                try { px = GetCandle(Math.Max(0, CurrentBar - 1)).Close; } catch { }
                var falta = px > 0 ? Math.Abs((double)px - precio) : 0;
                var t2 = string.Format(CultureInfo.GetCultureInfo("es-AR"),
                    "{0} {1}  {2:N2}  ({3:N0} pts)", arriba ? "^" : "v", nombre, precio, falta);
                var mm = g.MeasureString(t2, fa);
                int xx = Math.Max(x0 + 4, x1 - mm.Width - 10);
                g.FillRectangle(Color.FromArgb(170, ColFondo),
                    new Rectangle(xx, yb2, mm.Width + 8, mm.Height + 2));
                g.DrawString(t2, fa, Color.FromArgb(210, col), xx + 4, yb2 + 1);
                return;
            }
            g.DrawLine(new RenderPen(col, grueso ? 2f : 1.4f), x0, y, x1, y);
            var f = new RenderFont("Arial", 9f);
            // El nombre va del lado IZQUIERDO, como en el producto original
            // ("Zero Gamma", "Major Positive"), y el precio del lado del eje.
            // Poner las dos cosas juntas contra el eje tapaba las velas.
            var m1 = g.MeasureString(nombre, f);
            // Si la etiqueta caeria sobre el tablero se corre a su derecha.
            // Verificado en pantalla: "Zero Gamma" y "Major Negative" quedaban
            // tapados por el panel y no se leia ninguno de los dos.
            // LAS ETIQUETAS NO SE PISAN ENTRE SI.
            //
            // Verificado en pantalla: con el zero gamma en 7691 y el major
            // negative en 7684 -- siete puntos -- los dos rotulos quedaban uno
            // encima del otro y no se leia ninguno. Se corre en vertical hasta
            // encontrar lugar.
            int yTxt = y - m1.Height;
            while (_etiquetasUsadas.Any(u => Math.Abs(u - yTxt) < m1.Height + 2))
                yTxt -= m1.Height + 3;
            _etiquetasUsadas.Add(yTxt);

            int xl = x0 + 2;
            if (VerTablero && !_tableroRect.IsEmpty
                && y >= _tableroRect.Top - 4 && y <= _tableroRect.Bottom + 4
                && xl < _tableroRect.Right)
                xl = _tableroRect.Right + 8;
            g.FillRectangle(Color.FromArgb(185, ColFondo),
                new Rectangle(xl, yTxt - 1, m1.Width + 8, m1.Height + 2));
            g.DrawString(nombre, f, col, xl + 4, yTxt);
            // si la etiqueta quedo lejos de su linea, un tirante fino para que
            // se vea a cual pertenece
            if (Math.Abs(yTxt - (y - m1.Height)) > 3)
                g.DrawLine(new RenderPen(Color.FromArgb(110, col), 1f),
                           xl + 2, yTxt + m1.Height, xl + 2, y);

            var pr = precio.ToString("N2", CultureInfo.GetCultureInfo("es-AR"));
            var m2 = g.MeasureString(pr, f);
            g.FillRectangle(Color.FromArgb(215, col),
                new Rectangle(x1 - m2.Width - 8, y - m2.Height / 2 - 1, m2.Width + 8, m2.Height + 2));
            g.DrawString(pr, f, Color.FromArgb(250, 15, 20, 26), x1 - m2.Width - 4, y - m2.Height / 2);
        }

        private void Cinta(RenderContext g, int x0, Rectangle area, int nStrikes,
                           double neto, double spot)
        {
            var f = new RenderFont("Arial", 9.5f);
            var ls = new List<Tuple<string, Color>>();
            var c = _c;

            if (c == null)
            {
                ls.Add(Tuple.Create(string.IsNullOrEmpty(_error)
                    ? "bajando la cadena..." : "sin cadena: " + _error, ColAviso));
            }
            else
            {
                ls.Add(Tuple.Create(string.Format(CultureInfo.InvariantCulture,
                    "{0}  ·  {1} strikes repreciados con el precio de cada tick",
                    c.Contrato, nStrikes), ColTexto));
                ls.Add(Tuple.Create(string.Format(CultureInfo.InvariantCulture,
                    "net gex {0:+0.00;-0.00} B  ·  indice implicito {1:0.00}  ·  cadena de hace {2:0.#} min",
                    neto / 1e9, spot, c.EdadMin), ColTexto));
                if (_baseOrigen == "sin base")
                    ls.Add(Tuple.Create(
                        "sin base indice->futuro: no se dibuja ningun nivel", ColNeg));
                else if (_baseOrigen != "medida")
                    ls.Add(Tuple.Create("base " + _baseOrigen +
                        " -- los niveles pueden estar corridos unos ticks", ColAviso));
                if (_visiblesUlt == 0 && nStrikes > 0)
                    ls.Add(Tuple.Create(
                        "ningun strike entra en la ventana de precio: abri el grafico para ver el perfil",
                        ColNeg));
                ls.Add(Tuple.Create(
                    "el interes abierto es de ayer para todos; lo que late es el precio", ColAviso));
            }

            var med = ls.Select(l => g.MeasureString(l.Item1, f)).ToList();
            int w = 0, h = 8;
            foreach (var m in med) { w = Math.Max(w, m.Width); h += m.Height + 2; }
            var x = x0 + 6; var y = area.Bottom - h - 6;
            g.FillRectangle(Color.FromArgb(215, ColFondo), new Rectangle(x, y, w + 20, h));
            g.DrawRectangle(new RenderPen(Color.FromArgb(90, ColTexto), 1f),
                new Rectangle(x, y, w + 20, h));
            var yy = y + 4;
            for (int i = 0; i < ls.Count; i++)
            {
                g.DrawString(ls[i].Item1, f, ls[i].Item2, x + 9, yy);
                yy += med[i].Height + 2;
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

            ls.Add(Tuple.Create("volume   (de hoy, 15 min tarde)", ColAviso));
            ls.Add(Tuple.Create("  zero gamma      " + P(zeroVol > 0 ? zeroVol : zero), ColZero));
            ls.Add(Tuple.Create("  major positive  " + P(mpv), ColPos));
            ls.Add(Tuple.Create("  major negative  " + P(mnv), ColNeg));
            ls.Add(Tuple.Create("  net gex         " + M(netoV), colNet));
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
