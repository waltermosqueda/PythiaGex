using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ATAS.Indicators;
using OFT.Rendering;
using OFT.Rendering.Context;
using OFT.Rendering.Control;
using OFT.Rendering.Tools;

using MColor = System.Windows.Media.Color;

namespace PythiaSuper
{
    /// <summary>
    /// SUPERINDICADOR 1.0 (23-11-2026). Pedido del operador, con cuatro reels de muestra (uno de ATAS y tres de otra plataforma con un
    /// perfil de delta en columna): "el grafico en dos colores (varias paletas de dos colores, las mas usadas), el order flow bien
    /// calculado en una columna y el perfil de volumen; todo en un unico superindicador que se pueda activar, desactivar y cambiar
    /// a voluntad de un clic".
    ///
    /// UN SOLO INDICADOR, AUTOCONTENIDO: no depende de otro indicador, de ningun programa externo ni de archivos. Todo sale de
    /// las velas de ATAS (Rithmic). Lo unico que escribe afuera es el registro de auditoria (apagable).
    ///
    /// 1) VELAS EN DOS COLORES: la serie oficial de ATAS para pintar velas (PaintbarsDataSeries, la misma que usa su "Color Bar
    ///    Open/Close"): alcista si cierre &gt; apertura, bajista si cierre &lt; apertura; el doji (cierre = apertura) toma el color de
    ///    la vela anterior (convencion de ATAS), o el alcista, o gris.
    /// 2) COLUMNA DE ORDER FLOW: delta por precio (compras agresivas - ventas agresivas, Ask - Bid de cada nivel del footprint de
    ///    ATAS) sumado sobre una ventana (vela en curso / ultimas N velas / sesion / rango visible), en filas FIJAS de K ticks. Cada
    ///    fila: numero exacto + barra del color de la paleta, largo e intensidad proporcionales al mayor de la ventana.
    /// 3) PERFIL DE VOLUMEN: volumen por precio de la ventana elegida (sesion / RTH / visible / ultimas N), POC, VAH y VAL con el
    ///    MISMO algoritmo de area de valor que ATAS (DynamicLevels.GetValueArea) y su porcentaje y paso de la plataforma.
    /// 4) BOTONERA: botones en el propio grafico para prender/apagar cada parte, cambiar la paleta y la ventana con un clic. Los
    ///    mismos ajustes estan en el dialogo del indicador.
    ///
    /// INTEGRIDAD: ningun numero depende del zoom. Si una fila no tiene alto para su numero, la columna queda con colores y la
    /// LUPA (mouse encima) da el valor exacto. Los ajustes llevan el prefijo "Sp" (ATAS guarda los ajustes POR NOMBRE en el .ws).
    /// Registro: %APPDATA%\ATAS\PythiaGex\superindicador\ (auditoria cada 60 s con lo dibujado y sus pixeles, footprint de cada vela
    /// cerrada y la cinta en vivo) para recalcular todo POR FUERA con herramientas/auditar_superindicador.py.
    ///
    /// 1.5 (26-11-2026) JORNADA ELEGIDA. Pedido del operador: "ver el indicador con los valores del dia anterior o de la sesion que
    /// yo elija, con la data limpia SOLO de esa jornada; con el rango visible me suma la ultima sesion y la del dia donde estoy
    /// parado"; y que sirva para analizar un dia pasado con el replay de ATAS. Columna y perfil tienen el periodo "Jornada elegida":
    /// suman SOLO las velas de una jornada de CME (18:00 a 17:00 NY), completa, solo RTH (9:30-16:00 NY) o solo la noche
    /// (18:00-9:30 NY). Cual jornada: la ultima (la de hoy o la del replay), la de una fecha elegida con los botones &lt; &gt; o la que
    /// mas se ve en pantalla. Si el grafico no cargo la jornada desde su comienzo, o alguna vela vino sin footprint, lo dice en
    /// amarillo: nunca muestra media jornada como si fuera entera.
    ///
    /// 1.6 (26-11-2026) TODO SINCRONIZADO CON LA VISTA. Pedido: "si voy corriendo el grafico, el perfil de volumen queda fijo y esta
    /// mal; tiene que ser dinamico, sincronizado segun la vista o la fecha". Por defecto columna y perfil siguen la MISMA jornada: la
    /// que se esta mirando (la que mas velas tiene en pantalla; con la ultima vela a la vista, la de hoy o la del replay). Una fecha
    /// fijada con &lt; &gt; se suelta sola en cuanto se mueve el grafico, y mientras no sea la de la pantalla lo dice en amarillo.
    ///
    /// 1.7 (26-11-2026) AL MOMENTO. Pedido: "al ir moviendo el grafico, el delta y el perfil tienen que mostrar como estaban EN ESE
    /// MOMENTO, como un footprint real; si todo queda estatico no sirve para analizar retrocediendo". Todo se suma hasta la vela del
    /// momento: la ultima a la vista (boton "al momento"; "en vivo" vuelve a la ultima vela). La jornada es la de esa vela, desde su
    /// comienzo hasta ella; si a esa hora la parte elegida no habia empezado (la RTH a las 8:00), la anterior completa.
    /// </summary>
    [DisplayName("PythiaGex - Superindicador (velas + delta + perfil)")]
    [Category("PythiaGex")]
    public class SuperindicadorOF : Indicator
    {
        public const string Version = "1.8";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ================================================================== enumeraciones de los ajustes
        public enum Paleta
        {
            [Display(Name = "Turquesa / Magenta (reel de ATAS)")] TurquesaMagenta,
            [Display(Name = "Violeta / Blanco (reels de muestra)")] VioletaBlanco,
            [Display(Name = "Verde / Rojo (#089981 / #F23645)")] TradingView,
            [Display(Name = "Verde agua / Coral (#26A69A / #EF5350)")] TradingViewClasico,
            [Display(Name = "Aqua / Magenta oscuro (Color Bar de fabrica de ATAS)")] AquaMagentaAtas,
            [Display(Name = "Verde / Rojo vivo (#00C853 / #FF1744)")] VerdeRojo,
            [Display(Name = "Azul / Rojo")] AzulRojo,
            [Display(Name = "Azul / Naranja (apta daltonismo, Okabe-Ito)")] AzulNaranja,
            [Display(Name = "Blanco / Gris (monocromo)")] BlancoGris,
            [Display(Name = "Personalizada (los dos colores de abajo)")] Personalizada
        }

        public enum ModoDoji
        {
            [Display(Name = "Color de la vela anterior (como ATAS)")] Anterior,
            [Display(Name = "Color alcista")] ComoAlcista,
            [Display(Name = "Gris (tercer color)")] Gris
        }

        // 1.5: los valores nuevos van AL FINAL (ATAS guarda el ajuste elegido; asi los ya guardados no cambian de significado)
        public enum VentanaColumna
        {
            [Display(Name = "Vela en curso")] VelaEnCurso,
            [Display(Name = "Ultimas N velas")] UltimasN,
            [Display(Name = "Sesion (desde la apertura de la sesion de ATAS)")] Sesion,
            [Display(Name = "Rango visible en pantalla (junta las jornadas que se vean)")] Visible,
            [Display(Name = "Jornada elegida (solo esa jornada, ver grupo 6)")] Jornada
        }

        public enum VentanaPerfil
        {
            [Display(Name = "Sesion (desde la apertura de la sesion de ATAS)")] Sesion,
            [Display(Name = "RTH mas reciente (9:30-16:00 NY)")] Rth,
            [Display(Name = "Rango visible en pantalla (junta las jornadas que se vean)")] Visible,
            [Display(Name = "Ultimas N velas")] UltimasN,
            [Display(Name = "Jornada elegida (solo esa jornada, ver grupo 6)")] Jornada
        }

        public enum ParteJornada
        {
            [Display(Name = "Completa (18:00 de la vispera a 17:00 NY)")] Completa,
            [Display(Name = "Solo RTH (9:30 a 16:00 NY)")] Rth,
            [Display(Name = "Solo la noche (18:00 a 9:30 NY)")] Noche
        }

        // 1.7: la jornada es la DEL MOMENTO (la de la vela hasta la que se suma) o una fecha fija completa
        public enum ModoJornada
        {
            [Display(Name = "La del momento: desde su comienzo hasta la vela del momento")] Momento,
            [Display(Name = "Una fecha fija, completa (botones < >; se suelta al mover el grafico)")] Fecha
        }

        // 1.7: hasta que vela se suma TODO (columna y perfil, en cualquier periodo salvo 'rango visible')
        public enum HastaVela
        {
            [Display(Name = "Hasta la vela del momento: la ultima a la vista (al correr el grafico, muestra como estaba)")] ElMomento,
            [Display(Name = "Hasta la ultima vela (en vivo; no sigue la vista)")] EnVivo
        }

        public enum DatoColumna
        {
            [Display(Name = "Delta (compras - ventas a mercado)")] Delta,
            [Display(Name = "Ventas x Compras (bid x ask)")] VentasCompras,
            [Display(Name = "Volumen")] Volumen
        }

        public enum LugarColumna
        {
            [Display(Name = "Despues de la ultima vela")] DespuesDeLaVela,
            [Display(Name = "Pegada al perfil (borde derecho)")] JuntoAlPerfil
        }

        public enum LugarBotonera
        {
            [Display(Name = "Abajo a la izquierda")] AbajoIzquierda,
            [Display(Name = "Arriba a la izquierda")] ArribaIzquierda,
            [Display(Name = "Oculta (solo el dialogo de ajustes)")] Oculta
        }

        private sealed class DefPaleta
        {
            public readonly MColor Alc, Baj; public readonly string Corto;
            public DefPaleta(string a, string b, string corto) { Alc = Hex(a); Baj = Hex(b); Corto = corto; }
        }

        // Colores medidos en los reels (pixeles del video, percentil 80 de brillo dentro del tono de las velas) y los de fabrica
        // documentados de plataformas muy usadas (el par #089981/#F23645 y el clasico #26A69A/#EF5350 de las plataformas web;
        // el Color Bar de fabrica de ATAS = Colors.Aqua / DarkMagenta).
        private static readonly Dictionary<Paleta, DefPaleta> Paletas = new Dictionary<Paleta, DefPaleta>
        {
            [Paleta.TurquesaMagenta] = new DefPaleta("#11CAEB", "#E22674", "Turquesa/Magenta"),
            [Paleta.VioletaBlanco] = new DefPaleta("#8F20C4", "#F5F1EA", "Violeta/Blanco"),
            [Paleta.TradingView] = new DefPaleta("#089981", "#F23645", "Verde/Rojo"),
            [Paleta.TradingViewClasico] = new DefPaleta("#26A69A", "#EF5350", "Verde agua/Coral"),
            [Paleta.AquaMagentaAtas] = new DefPaleta("#00FFFF", "#8B008B", "Aqua/Magenta ATAS"),
            [Paleta.VerdeRojo] = new DefPaleta("#00C853", "#FF1744", "Verde/Rojo vivo"),
            [Paleta.AzulRojo] = new DefPaleta("#2196F3", "#F44336", "Azul/Rojo"),
            [Paleta.AzulNaranja] = new DefPaleta("#56B4E9", "#E69F00", "Azul/Naranja"),
            [Paleta.BlancoGris] = new DefPaleta("#E8EAED", "#6B7078", "Blanco/Gris"),
        };

        private static MColor Hex(string h)
        {
            h = h.TrimStart('#');
            return MColor.FromRgb(Convert.ToByte(h.Substring(0, 2), 16), Convert.ToByte(h.Substring(2, 2), 16), Convert.ToByte(h.Substring(4, 2), 16));
        }

        private static Color D(MColor c) => Color.FromArgb(c.A, c.R, c.G, c.B);
        private static Color Alfa(Color c, int a) => Color.FromArgb(Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B);

        // ================================================================== ajustes
        // 1. Botonera
        private LugarBotonera _lugarBot = LugarBotonera.AbajoIzquierda;
        [Display(Name = "Botonera en el grafico", GroupName = "1. Un clic", Order = 10,
            Description = "Botones sobre el grafico para prender/apagar velas, columna y perfil, cambiar paleta y ventanas con un clic.")]
        public LugarBotonera SpBotonera { get => _lugarBot; set { _lugarBot = value; Redibujar(); } }

        // 2. Velas
        private bool _velas = true;
        [Display(Name = "Velas en dos colores", GroupName = "2. Velas", Order = 20)]
        public bool SpVelas { get => _velas; set { _velas = value; RepintarTodo(); } }

        private Paleta _paleta = Paleta.TurquesaMagenta;
        [Display(Name = "Paleta", GroupName = "2. Velas", Order = 21)]
        public Paleta SpPaleta { get => _paleta; set { _paleta = value; RepintarTodo(); } }

        private ModoDoji _doji = ModoDoji.Anterior;
        [Display(Name = "Doji (cierre = apertura)", GroupName = "2. Velas", Order = 22)]
        public ModoDoji SpDoji { get => _doji; set { _doji = value; RepintarTodo(); } }

        private MColor _cAlc = Hex("#11CAEB"), _cBaj = Hex("#E22674"), _cDoji = Hex("#9AA0A6");
        [Display(Name = "Alcista (paleta personalizada)", GroupName = "2. Velas", Order = 23)]
        public MColor SpColorAlcista { get => _cAlc; set { _cAlc = value; RepintarTodo(); } }

        [Display(Name = "Bajista (paleta personalizada)", GroupName = "2. Velas", Order = 24)]
        public MColor SpColorBajista { get => _cBaj; set { _cBaj = value; RepintarTodo(); } }

        [Display(Name = "Doji gris", GroupName = "2. Velas", Order = 25)]
        public MColor SpColorDoji { get => _cDoji; set { _cDoji = value; RepintarTodo(); } }

        // 3. Columna
        private bool _columna = true;
        [Display(Name = "Columna de order flow", GroupName = "3. Columna de order flow", Order = 30)]
        public bool SpColumna { get => _columna; set { _columna = value; Redibujar(); } }

        // 1.6: por defecto la columna y el perfil siguen la MISMA jornada (la que se mira). Propiedad renombrada (antes SpColVentana)
        // para que el valor guardado en el workspace no pise el defecto nuevo (ATAS guarda los ajustes por nombre).
        private VentanaColumna _colVentana = VentanaColumna.Jornada;
        [Display(Name = "Periodo", GroupName = "3. Columna de order flow", Order = 31)]
        public VentanaColumna SpColPeriodo { get => _colVentana; set { _colVentana = value; Redibujar(); } }

        private int _colVelas = 30;
        [Display(Name = "N velas (si el periodo es 'Ultimas N')", GroupName = "3. Columna de order flow", Order = 32)]
        [Range(1, 5000)]
        public int SpColVelas { get => _colVelas; set { _colVelas = Math.Max(1, Math.Min(5000, value)); Redibujar(); } }

        private int _colTicks = 4;
        [Display(Name = "Ticks por fila (fijo, no depende del zoom)", GroupName = "3. Columna de order flow", Order = 33,
            Description = "Cuantos ticks suma cada fila. MES/ES: 4 ticks = 1 punto. Las filas van alineadas a la grilla de precios.")]
        [Range(1, 200)]
        public int SpColTicks { get => _colTicks; set { _colTicks = Math.Max(1, Math.Min(200, value)); Redibujar(); } }

        private DatoColumna _colDato = DatoColumna.Delta;
        [Display(Name = "Dato", GroupName = "3. Columna de order flow", Order = 34)]
        public DatoColumna SpColDato { get => _colDato; set { _colDato = value; Redibujar(); } }

        private LugarColumna _colLugar = LugarColumna.DespuesDeLaVela;
        [Display(Name = "Ubicacion", GroupName = "3. Columna de order flow", Order = 35)]
        public LugarColumna SpColLugar { get => _colLugar; set { _colLugar = value; Redibujar(); } }

        private int _colAncho = 86;
        [Display(Name = "Ancho (px)", GroupName = "3. Columna de order flow", Order = 36)]
        [Range(40, 400)]
        public int SpColAncho { get => _colAncho; set { _colAncho = Math.Max(40, Math.Min(400, value)); Redibujar(); } }

        private int _colSep = 30;
        [Display(Name = "Separacion de la ultima vela (px)", GroupName = "3. Columna de order flow", Order = 37)]
        [Range(0, 600)]
        public int SpColSeparacion { get => _colSep; set { _colSep = Math.Max(0, Math.Min(600, value)); Redibujar(); } }

        private int _colLetra = 10;
        [Display(Name = "Letra maxima", GroupName = "3. Columna de order flow", Order = 38)]
        [Range(6, 18)]
        public int SpColLetra { get => _colLetra; set { _colLetra = Math.Max(6, Math.Min(18, value)); Redibujar(); } }

        private bool _colEncab = true;
        [Display(Name = "Encabezado con el total", GroupName = "3. Columna de order flow", Order = 39)]
        public bool SpColEncabezado { get => _colEncab; set { _colEncab = value; Redibujar(); } }

        // 4. Perfil
        private bool _perfil = true;
        [Display(Name = "Perfil de volumen", GroupName = "4. Perfil de volumen", Order = 50)]
        public bool SpPerfil { get => _perfil; set { _perfil = value; Redibujar(); } }

        private VentanaPerfil _perVentana = VentanaPerfil.Jornada;
        [Display(Name = "Periodo", GroupName = "4. Perfil de volumen", Order = 51)]
        public VentanaPerfil SpPerVentana { get => _perVentana; set { _perVentana = value; Redibujar(); } }

        private int _perVelas = 120;
        [Display(Name = "N velas (si el periodo es 'Ultimas N')", GroupName = "4. Perfil de volumen", Order = 52)]
        [Range(1, 20000)]
        public int SpPerVelas { get => _perVelas; set { _perVelas = Math.Max(1, Math.Min(20000, value)); Redibujar(); } }

        private int _perTicks = 1;
        [Display(Name = "Ticks por fila", GroupName = "4. Perfil de volumen", Order = 53)]
        [Range(1, 200)]
        public int SpPerTicks { get => _perTicks; set { _perTicks = Math.Max(1, Math.Min(200, value)); Redibujar(); } }

        private int _perAncho = 170;
        [Display(Name = "Ancho (px)", GroupName = "4. Perfil de volumen", Order = 54)]
        [Range(30, 800)]
        public int SpPerAncho { get => _perAncho; set { _perAncho = Math.Max(30, Math.Min(800, value)); Redibujar(); } }

        private int _perPct = 0;
        [Display(Name = "Area de valor % (0 = la de ATAS)", GroupName = "4. Perfil de volumen", Order = 55,
            Description = "0 usa el porcentaje de la plataforma (Settings de ATAS, normalmente 70).")]
        [Range(0, 100)]
        public int SpAreaValor { get => _perPct; set { _perPct = Math.Max(0, Math.Min(100, value)); Redibujar(); } }

        private bool _perPartido = false;
        [Display(Name = "Barras partidas compras / ventas", GroupName = "4. Perfil de volumen", Order = 56)]
        public bool SpPerPartido { get => _perPartido; set { _perPartido = value; Redibujar(); } }

        private bool _perLineas = true;
        [Display(Name = "Lineas POC / VAH / VAL", GroupName = "4. Perfil de volumen", Order = 57)]
        public bool SpPerLineas { get => _perLineas; set { _perLineas = value; Redibujar(); } }

        private bool _perEtiq = true;
        [Display(Name = "Precios en el eje", GroupName = "4. Perfil de volumen", Order = 58)]
        public bool SpPerEtiquetas { get => _perEtiq; set { _perEtiq = value; Redibujar(); } }

        private MColor _cAv = Hex("#4F8FC0"), _cFuera = Hex("#6B7280"), _cPoc = Hex("#FFB300");
        [Display(Name = "Color area de valor", GroupName = "4. Perfil de volumen", Order = 59)]
        public MColor SpColorAV { get => _cAv; set { _cAv = value; Redibujar(); } }

        [Display(Name = "Color fuera del area", GroupName = "4. Perfil de volumen", Order = 60)]
        public MColor SpColorFueraAV { get => _cFuera; set { _cFuera = value; Redibujar(); } }

        [Display(Name = "Color POC", GroupName = "4. Perfil de volumen", Order = 61)]
        public MColor SpColorPOC { get => _cPoc; set { _cPoc = value; Redibujar(); } }

        private int _perOpac = 60;
        [Display(Name = "Opacidad del perfil %", GroupName = "4. Perfil de volumen", Order = 62)]
        [Range(10, 100)]
        public int SpPerOpacidad { get => _perOpac; set { _perOpac = Math.Max(10, Math.Min(100, value)); Redibujar(); } }

        // 5. Avanzado
        private int _margen = 0;
        [Display(Name = "Margen derecho (px)", GroupName = "5. Avanzado", Order = 70,
            Description = "Corre el perfil a la izquierda si algo lo tapa contra el eje de precios.")]
        [Range(0, 300)]
        public int SpMargenDerecho { get => _margen; set { _margen = Math.Max(0, Math.Min(300, value)); Redibujar(); } }

        private bool _lupa = true;
        [Display(Name = "Lupa (valor exacto con el mouse)", GroupName = "5. Avanzado", Order = 71)]
        public bool SpLupa { get => _lupa; set { _lupa = value; Redibujar(); } }

        private bool _registro = true;
        [Display(Name = "Registro de auditoria", GroupName = "5. Avanzado", Order = 72,
            Description = "Escribe en %APPDATA%\\ATAS\\PythiaGex\\superindicador lo dibujado, el footprint y la cinta para recalcular por fuera.")]
        public bool SpRegistro { get => _registro; set { _registro = value; } }

        // 6. Momento y jornada (1.5; 1.7: todo se suma hasta la vela del momento; propiedades renombradas para pisar lo guardado,
        // antes SpJornadaModo / SpJornadaQue)
        private HastaVela _hasta = HastaVela.ElMomento;
        [Display(Name = "Hasta que vela suma", GroupName = "6. Momento y jornada", Order = 79,
            Description = "La vela del momento (la ultima a la vista): al correr el grafico hacia atras, columna y perfil muestran como estaban en ese momento. O la ultima vela del grafico (en vivo).")]
        public HastaVela SpHasta { get => _hasta; set { _hasta = value; Redibujar(); } }

        private ModoJornada _jorModo = ModoJornada.Momento;
        private int _fechaFin = int.MinValue;   // velas entre la vista y el final cuando se fijo la fecha (si cambia, se movio el grafico)
        [Display(Name = "Que jornada", GroupName = "6. Momento y jornada", Order = 80,
            Description = "La del momento (desde su comienzo hasta la vela del momento) o una fecha fija completa con < > (se suelta al mover el grafico).")]
        public ModoJornada SpJornadaCual { get => _jorModo; set { _jorModo = value; _fechaFin = int.MinValue; Redibujar(); } }

        private int _jorFecha = 0;
        [Display(Name = "Fecha (aaaammdd; la cambian los botones < >)", GroupName = "6. Momento y jornada", Order = 81,
            Description = "Dia de la rueda de la jornada, por ejemplo 20261125. Solo cuenta si 'Que jornada' es 'La de la fecha elegida'.")]
        [Range(0, 99991231)]
        public int SpJornadaFecha { get => _jorFecha; set { _jorFecha = Math.Max(0, value); Redibujar(); } }

        private ParteJornada _jorParte = ParteJornada.Completa;
        [Display(Name = "Parte", GroupName = "6. Momento y jornada", Order = 82,
            Description = "Completa (18:00 a 17:00 NY), solo RTH (9:30 a 16:00 NY) o solo la noche (18:00 a 9:30 NY). Para RTH exacta, velas que abran a horario (1, 5, 15 o 30 min).")]
        public ParteJornada SpJornadaParte { get => _jorParte; set { _jorParte = value; Redibujar(); } }

        // ================================================================== estado
        private readonly PaintbarsDataSeries _pintura = new PaintbarsDataSeries("SpPintura", "Velas en dos colores") { IsHidden = true };
        private readonly object _llave = new object();
        private readonly Dictionary<int, BarraFp> _fp = new Dictionary<int, BarraFp>();   // velas cerradas (inmutables)
        private BarraFp _vivo; private int _vivoBar = -1; private long _version;
        private readonly List<int> _sesiones = new List<int>();
        private decimal _tick = 0.25m;
        // 1.5: jornadas de CME de las velas cargadas (en orden) y la ultima elegida (para la botonera, la lupa y la auditoria)
        private readonly List<Jornada> _jornadas = new List<Jornada>();
        private int _jornadaUltBar = -1;
        private int _carga;   // sube con cada recarga de datos (al arrancar, al cambiar de marco o al conectar el replay)
        private sealed class EstadoJornada
        {
            public int Clave = -1, Parte, Indice = -1, Desde = -1, Hasta = -1, Total;
            public int VistaClave = -1;   // 1.6: la jornada que se ve en pantalla (para avisar si la fija es otra)
            public int Momento = -1;      // 1.7: la vela hasta la que se suma
            public bool Hay, Cortada, EnCurso, NoCargada, Recortada; public DateTime PrimeraNy;
        }
        private EstadoJornada _ej = new EstadoJornada();

        private sealed class CacheVentana
        {
            public string Firma; public Acum Cerrado; public long Version = -1; public Perfil Listo; public int Pct = -1, Paso = -1;
        }
        private readonly CacheVentana _cCol = new CacheVentana(), _cPer = new CacheVentana();

        // geometria de lo ultimo dibujado (lupa, botones y auditoria)
        private sealed class FilaDibujada { public int Fila; public int Y0, Y1; public string Texto; }
        private sealed class Dibujo
        {
            public Perfil P; public Rectangle R; public List<FilaDibujada> Filas = new List<FilaDibujada>();
            public string Nombre = ""; public int Letra; public bool Numeros; public int YPoc = -1, YVah = -1, YVal = -1;
            public bool Live; public string Cuando = "";   // 1.8: lo que dice el encabezado (para la auditoria)
        }
        private readonly object _llaveUi = new object();
        private Dibujo _dCol, _dPer;
        private readonly List<KeyValuePair<string, Rectangle>> _botones = new List<KeyValuePair<string, Rectangle>>();
        private Point _mouseUlt = new Point(-1, -1);
        private string _hoverUlt = "";

        private Action _latido;
        private readonly TimeSpan _periodo = TimeSpan.FromMilliseconds(250);
        private DateTime _ultAudit = DateTime.MinValue, _ultError = DateTime.MinValue, _ultFlush = DateTime.MinValue;
        private readonly ConcurrentQueue<string> _cinta = new ConcurrentQueue<string>();
        private readonly object _llaveArchivo = new object();
        // 1.5: lo volcado se recuerda vela por vela (antes era "hasta tal hora" y una jornada vieja elegida despues no se volcaba)
        private readonly HashSet<DateTime> _velasVolcadas = new HashSet<DateTime>();
        private string _velasVolcadasDia = "";
        private static TimeZoneInfo _ny;

        // ================================================================== ciclo de vida
        public SuperindicadorOF() : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
            DrawAbovePrice = true;
            DataSeries[0] = _pintura;   // como el Color Bar de fabrica: la serie 0 es la de pintar velas
        }

        protected override void OnInitialize()
        {
            try
            {
                var ps = DataProvider != null ? DataProvider.Panels : null;
                if (ps != null && ps.Count > 0) Panel = ps.Contains("Chart") ? "Chart" : ps[0];
            }
            catch (Exception e) { Anotar(e); }
            DrawAbovePrice = true;
            _latido = Latido;
            try { SubscribeToTimer(_periodo, _latido); } catch (Exception e) { Anotar(e); }
        }

        protected override void OnDispose()
        {
            try { if (_latido != null) UnsubscribeFromTimer(_periodo, _latido); } catch { }
            try { VolcarCinta(); } catch { }
            base.OnDispose();
        }

        protected override void OnRecalculate()
        {
            lock (_llave)
            {
                _fp.Clear(); _vivo = null; _vivoBar = -1; _version++; _sesiones.Clear();
                _jornadas.Clear(); _jornadaUltBar = -1; _carga++;
            }
            _cCol.Firma = null; _cPer.Firma = null;
            lock (_llaveArchivo) _velasVolcadas.Clear();
            AnotarLinea("recarga de datos -> carga " + _carga);
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            try
            {
                var c = GetCandle(bar);
                if (c == null) return;
                Pintar(bar, c);
                bool nueva = bar == 0;
                if (!nueva) { try { nueva = IsNewSession(bar); } catch { } }
                int ultima = CurrentBar - 1;
                DateTime ny = HoraNyDe(c);
                lock (_llave)
                {
                    if (bar == 0) _sesiones.Clear();
                    if (nueva && (_sesiones.Count == 0 || _sesiones[_sesiones.Count - 1] < bar)) _sesiones.Add(bar);
                    // 1.5: cada vela, una sola vez, a su jornada de CME (por la hora NY de su apertura)
                    if (bar == 0) { _jornadas.Clear(); _jornadaUltBar = -1; }
                    if (bar > _jornadaUltBar) { Jornadas.Agregar(_jornadas, bar, ny); _jornadaUltBar = bar; }
                }
                if (bar >= ultima)
                {
                    long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    var b = Extraer(bar, c);
                    lock (_llave)
                    {
                        if (_vivoBar >= 0 && _vivoBar != bar) _fp.Remove(_vivoBar);   // la que era vela viva se vuelve a leer cerrada
                        _vivo = b; _vivoBar = bar; _version++;
                    }
                    double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    _msVivo += ms; _nVivo++; if (ms > _msVivoMax) _msVivoMax = ms;
                }
            }
            catch (Exception e) { Anotar(e); }
        }

        /// <summary>Cada operacion en vivo, para el registro de la cinta (fuente independiente del footprint).</summary>
        protected override void OnNewTrade(MarketDataArg t)
        {
            if (!_registro || t == null) return;
            try
            {
                if (_cinta.Count > 400000) return;   // tope de seguridad si el disco se traba
                string dir = t.Direction == TradeDirection.Buy ? "C" : t.Direction == TradeDirection.Sell ? "V" : "E";
                _cinta.Enqueue(t.Time.ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv) + "," + t.Price.ToString(Inv) + "," + t.Volume.ToString(Inv) + "," + dir);
            }
            catch { }
        }

        private void Latido()
        {
            try
            {
                // lupa: redibujar si el mouse cambio de fila o de boton aunque no entren ticks
                if (_lupa || _lugarBot != LugarBotonera.Oculta)
                {
                    string h = HoverActual();
                    if (h != _hoverUlt) { _hoverUlt = h; Redibujar(); }
                }
                if (_registro && (DateTime.UtcNow - _ultFlush).TotalSeconds >= 2) { _ultFlush = DateTime.UtcNow; VolcarCinta(); }
            }
            catch (Exception e) { Anotar(e); }
        }

        private void Redibujar()
        {
            try { var c = ChartInfo?.PriceChartContainer; if (c != null) RedrawChart(new RedrawArg(c.Region)); } catch { }
        }

        // ================================================================== velas
        private void ColoresPaleta(out MColor alc, out MColor baj)
        {
            if (_paleta == Paleta.Personalizada || !Paletas.TryGetValue(_paleta, out var d)) { alc = _cAlc; baj = _cBaj; }
            else { alc = d.Alc; baj = d.Baj; }
        }

        private void Pintar(int bar, IndicatorCandle c)
        {
            if (!_velas) { _pintura[bar] = null; return; }
            ColoresPaleta(out var alc, out var baj);
            if (c.Close > c.Open) _pintura[bar] = alc;
            else if (c.Close < c.Open) _pintura[bar] = baj;
            else if (_doji == ModoDoji.ComoAlcista) _pintura[bar] = alc;
            else if (_doji == ModoDoji.Gris) _pintura[bar] = _cDoji;
            else
            {
                MColor? prev = bar > 0 ? _pintura[bar - 1] : null;
                // la anterior pudo quedar pintada con otra paleta si se repinta a medias: se toma su LADO, no su color
                if (prev.HasValue && (prev.Value == baj || EsBajista(bar - 1))) _pintura[bar] = baj;
                else _pintura[bar] = alc;
            }
        }

        private bool EsBajista(int bar)
        {
            // lado de la vela anterior para el doji: el de la ultima vela no-doji hacia atras (como ATAS encadena los dojis)
            for (int b = bar, n = 0; b >= 0 && n < 500; b--, n++)
            {
                var c = GetCandle(b); if (c == null) return false;
                if (c.Close < c.Open) return true;
                if (c.Close > c.Open) return false;
            }
            return false;
        }

        /// <summary>Repinta toda la historia con la paleta y el modo vigentes (los cambios de color no esperan un tick).</summary>
        private void RepintarTodo()
        {
            try
            {
                int n = CurrentBar;
                if (n <= 0) return;
                for (int b = 0; b < n; b++) { var c = GetCandle(b); if (c != null) Pintar(b, c); }
            }
            catch (Exception e) { Anotar(e); }
            Redibujar();
        }

        // ================================================================== footprint
        private int Tick(decimal precio) => (int)Math.Round(precio / _tick, 0, MidpointRounding.AwayFromZero);

        private BarraFp Extraer(int bar, IndicatorCandle c)
        {
            if (c == null) return null;
            try { if (InstrumentInfo != null && InstrumentInfo.TickSize > 0) _tick = InstrumentInfo.TickSize; } catch { }
            var b = new BarraFp
            {
                Bar = bar, Hora = c.Time, HoraFin = c.LastTime, O = c.Open, H = c.High, L = c.Low, C = c.Close,
                DeltaAtas = c.Delta, VolAtas = c.Volume
            };
            var lista = new List<(int t, decimal bid, decimal ask, decimal vol)>(64);
            int tmin = int.MaxValue, tmax = int.MinValue;
            var niveles = c.GetAllPriceLevels();
            if (niveles != null)
                foreach (var p in niveles)
                {
                    if (p == null) continue;
                    int t = Tick(p.Price);
                    lista.Add((t, p.Bid, p.Ask, p.Volume));
                    if (t < tmin) tmin = t;
                    if (t > tmax) tmax = t;
                }
            if (lista.Count == 0) return b;
            long n = (long)tmax - tmin + 1;
            if (n > 200000) { Anotar(new Exception("vela " + bar + " con rango absurdo de " + n + " ticks; se ignora")); return b; }
            b.TickMin = tmin;
            b.Bid = new decimal[n]; b.Ask = new decimal[n]; b.Vol = new decimal[n];
            var vistos = new HashSet<int>();
            foreach (var x in lista)
            {
                int i = x.t - tmin;
                if (!vistos.Add(x.t)) b.NivelesRepetidos++;
                b.Bid[i] += x.bid; b.Ask[i] += x.ask; b.Vol[i] += x.vol;
                b.SumBid += x.bid; b.SumAsk += x.ask; b.SumVol += x.vol;
            }
            b.Niveles = vistos.Count;
            return b;
        }

        private BarraFp Cerrada(int bar)
        {
            lock (_llave) { if (_fp.TryGetValue(bar, out var x)) return x; }
            BarraFp b = null;
            try { b = Extraer(bar, GetCandle(bar)); } catch (Exception e) { Anotar(e); }
            if (b != null) lock (_llave) { _fp[bar] = b; }
            return b;
        }

        // ================================================================== ventanas
        private int InicioSesion(int bar)
        {
            lock (_llave)
            {
                for (int i = _sesiones.Count - 1; i >= 0; i--) if (_sesiones[i] <= bar) return _sesiones[i];
            }
            return 0;
        }

        private static TimeZoneInfo Ny()
        {
            if (_ny == null) { try { _ny = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); } catch { _ny = TimeZoneInfo.Utc; } }
            return _ny;
        }

        private DateTime HoraNy(int bar) => HoraNyDe(GetCandle(bar));

        private static DateTime HoraNyDe(IndicatorCandle c)
            => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(c.Time, DateTimeKind.Utc), Ny());

        /// <summary>1.7: la vela del MOMENTO, hasta la que se suma todo: la ultima a la vista (al correr el grafico hacia atras el
        /// momento retrocede y columna y perfil muestran como estaban ahi) o la ultima del grafico (en vivo).</summary>
        private int Momento()
        {
            int ultima = CurrentBar - 1;
            if (_hasta == HastaVela.EnVivo) return ultima;
            try { int v = LastVisibleBarNumber; if (v >= 0) return Math.Min(ultima, v); } catch { }
            return ultima;
        }

        private static string HoraLocal(DateTime utc)
            => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.Local).ToString("HH:mm", Inv);

        /// <summary>1.5/1.7: la jornada elegida (grupo 6) y su rango de velas de la parte pedida: la DEL MOMENTO (desde su comienzo hasta
        /// la vela del momento) o una fecha fija completa. Deja en _ej lo elegido para la botonera, la lupa y la auditoria.</summary>
        private bool VentanaJornada(out int desde, out int hasta, out string nombre)
        {
            desde = hasta = -1;
            int ultima = CurrentBar - 1;
            int m = Momento();
            var e = new EstadoJornada { Parte = (int)_jorParte, Momento = m };
            int ultVis = Math.Min(ultima, LastVisibleBarNumber);
            // 1.6: una fecha fijada con < > se SUELTA si despues se mueve el grafico (lo ultimo que hace el operador manda): si cambia
            // la distancia entre la ultima vela a la vista y el final del grafico, vuelve a la del momento. Las velas nuevas en vivo no
            // la mueven (la vista corre junto con el final).
            if (_jorModo == ModoJornada.Fecha && _fechaFin != int.MinValue && Math.Abs((ultima - ultVis) - _fechaFin) > 2)
            {
                _jorModo = ModoJornada.Momento; _fechaFin = int.MinValue;
                AnotarLinea("se movio el grafico: la fecha fija " + _jorFecha + " se suelta y vuelve a la del momento");
            }
            lock (_llave)
            {
                e.Total = _jornadas.Count;
                bool hayM = Jornadas.AlMomento(_jornadas, m, e.Parte, out int im, out int dm, out int hm);
                if (hayM) e.VistaClave = _jornadas[im].Clave;
                if (_jorModo == ModoJornada.Fecha && _jorFecha > 0)
                {
                    int i = -1;
                    for (int k = 0; k < _jornadas.Count; k++) if (_jornadas[k].Clave == _jorFecha) { i = k; break; }
                    if (i < 0) { e.Clave = _jorFecha; e.NoCargada = true; }
                    else { e.Indice = i; e.Clave = _jornadas[i].Clave; e.Hay = _jornadas[i].Rango(e.Parte, out e.Desde, out e.Hasta); }
                }
                else if (hayM)
                {
                    e.Indice = im; e.Clave = _jornadas[im].Clave; e.Hay = true; e.Desde = dm; e.Hasta = hm;
                    _jornadas[im].Rango(e.Parte, out _, out int hEntera);
                    e.Recortada = hm < hEntera;   // la parte sigue despues del momento: se muestra como estaba ahi
                }
                else if (_jornadas.Count > 0) { e.Indice = _jornadas.Count - 1; e.Clave = _jornadas[e.Indice].Clave; }
            }
            if (e.Hay)
            {
                desde = e.Desde; hasta = e.Hasta;
                e.EnCurso = hasta >= ultima;
                try
                {
                    e.PrimeraNy = HoraNy(desde);
                    // la primera vela de la parte es la primera del grafico y abre despues de la hora en que empieza esa parte:
                    // el grafico no cargo la jornada desde su comienzo
                    e.Cortada = desde == 0 && e.PrimeraNy > Jornadas.InicioNominal(e.Clave, e.Parte);
                }
                catch { }
            }
            nombre = e.Clave > 0 ? Jornadas.Rotulo(e.Clave) + " " + Jornadas.NombreParte(e.Parte) : "sin jornadas";
            if (e.Hay && e.Recortada) { try { nombre += " hasta " + HoraLocal(GetCandle(hasta).Time); } catch { } }
            _ej = e;
            return e.Hay;
        }

        /// <summary>Aviso en amarillo cuando la suma no es la jornada entera o le falta footprint a alguna vela.</summary>
        private string Aviso(Perfil p, bool esJornada, bool corto)
        {
            var partes = new List<string>();
            if (esJornada && _ej.Cortada)
                partes.Add(corto ? "INCOMPLETA" : "INCOMPLETA: el grafico carga desde " + _ej.PrimeraNy.ToString("dd-MM HH:mm", Inv) + " NY (cargar mas dias)");
            // 1.6: una jornada fija que no es la que se ve en pantalla se dice siempre (nunca un perfil de otro dia como si fuera este)
            if (esJornada && _jorModo == ModoJornada.Fecha && _ej.Clave > 0 && _ej.VistaClave > 0 && _ej.Clave != _ej.VistaClave)
                partes.Add(corto ? "FIJA: otro dia" : "FIJA en " + Jornadas.Rotulo(_ej.Clave) + ", en pantalla " + Jornadas.Rotulo(_ej.VistaClave)
                           + " (mover el grafico o 'fijar' la suelta)");
            if (p != null && p.VelasSinDatos > 0) partes.Add(p.VelasSinDatos + (corto ? " sin footprint" : " velas sin footprint"));
            return string.Join(" | ", partes);
        }

        private static readonly TimeSpan Rth0 = new TimeSpan(9, 30, 0), Rth1 = new TimeSpan(16, 0, 0);
        private int _rthClave = -1, _rthDesde = -1, _rthHasta = -1;

        /// <summary>La rueda RTH (9:30-16:00 NY) mas reciente con velas: la vela abre a las 9:30 o despues y antes de las 16:00.</summary>
        private bool RangoRth(int ultima, out int desde, out int hasta)
        {
            if (_rthClave == ultima && _rthDesde >= 0) { desde = _rthDesde; hasta = _rthHasta; return true; }
            desde = hasta = -1;
            for (int b = ultima, n = 0; b >= 0 && n < 30000; b--, n++)
            {
                var t = HoraNy(b);
                if (t.TimeOfDay >= Rth0 && t.TimeOfDay < Rth1)
                {
                    hasta = b; desde = b;
                    for (int k = b - 1; k >= 0; k--)
                    {
                        var tk = HoraNy(k);
                        if (tk.Date != t.Date || tk.TimeOfDay < Rth0) break;
                        desde = k;
                    }
                    _rthClave = ultima; _rthDesde = desde; _rthHasta = hasta;
                    return true;
                }
            }
            return false;
        }

        private bool VentanaColumnaActual(out int desde, out int hasta, out string nombre)
        {
            int ultima = CurrentBar - 1; desde = hasta = -1; nombre = "";
            if (ultima < 0) return false;
            int m = Momento();   // 1.7: todo termina en la vela del momento (la ultima a la vista), salvo el rango visible
            switch (_colVentana)
            {
                case VentanaColumna.VelaEnCurso: desde = hasta = m; nombre = "vela"; break;
                case VentanaColumna.UltimasN: desde = Math.Max(0, m - _colVelas + 1); hasta = m; nombre = _colVelas + "v"; break;
                case VentanaColumna.Sesion: desde = InicioSesion(m); hasta = m; nombre = "sesion"; break;
                case VentanaColumna.Jornada: if (!VentanaJornada(out desde, out hasta, out nombre)) return false; break;
                default:
                    desde = Math.Max(0, FirstVisibleBarNumber); hasta = Math.Min(ultima, LastVisibleBarNumber);
                    if (hasta - desde > 20000) desde = hasta - 20000;
                    nombre = "visible"; break;
            }
            return hasta >= desde && desde >= 0;
        }

        private bool VentanaPerfilActual(out int desde, out int hasta, out string nombre)
        {
            int ultima = CurrentBar - 1; desde = hasta = -1; nombre = "";
            if (ultima < 0) return false;
            int m = Momento();   // 1.7: todo termina en la vela del momento (la ultima a la vista), salvo el rango visible
            switch (_perVentana)
            {
                case VentanaPerfil.Sesion: desde = InicioSesion(m); hasta = m; nombre = "sesion"; break;
                case VentanaPerfil.Rth: if (!RangoRth(m, out desde, out hasta)) return false; nombre = "RTH"; break;
                case VentanaPerfil.UltimasN: desde = Math.Max(0, m - _perVelas + 1); hasta = m; nombre = _perVelas + "v"; break;
                case VentanaPerfil.Jornada: if (!VentanaJornada(out desde, out hasta, out nombre)) return false; break;
                default:
                    desde = Math.Max(0, FirstVisibleBarNumber); hasta = Math.Min(ultima, LastVisibleBarNumber);
                    if (hasta - desde > 20000) desde = hasta - 20000;
                    nombre = "visible"; break;
            }
            return hasta >= desde && desde >= 0;
        }

        /// <summary>Suma la ventana: velas cerradas (cacheadas hasta que cambie la ventana) + la vela en curso (cada tick).</summary>
        private Perfil Calcular(CacheVentana cv, int desde, int hasta, int k, bool conAv, int pct, int paso)
        {
            int ultima = CurrentBar - 1;
            int hastaCerrado = Math.Min(hasta, ultima - 1);
            bool incluyeViva = hasta >= ultima;
            BarraFp viva; int vivaBar; long ver;
            lock (_llave) { viva = _vivo; vivaBar = _vivoBar; ver = _version; }
            string firma = desde + "|" + hastaCerrado + "|" + k;
            if (cv.Firma == firma && cv.Listo != null && (!incluyeViva || cv.Version == ver) && (!conAv || (cv.Pct == pct && cv.Paso == paso)))
                return cv.Listo;
            if (cv.Firma != firma || cv.Cerrado == null)
            {
                var a = new Acum(k);
                for (int b = desde; b <= hastaCerrado; b++) a.Sumar(Cerrada(b));
                cv.Cerrado = a; cv.Firma = firma;
            }
            Acum t = cv.Cerrado;
            BarraFp usada = null;
            if (incluyeViva)
            {
                t = t.Copia();
                BarraFp bv = vivaBar == ultima ? viva : null;
                if (bv == null) { try { bv = Extraer(ultima, GetCandle(ultima)); } catch { } }
                t.Sumar(bv);
                usada = bv;
            }
            var p = Perfil.DeAcum(t, desde, hasta);
            p.Viva = usada;
            try { p.HoraDesde = GetCandle(desde).Time; p.HoraHasta = GetCandle(hasta).Time; } catch { }
            if (conAv) Calculos.AreaDeValor(p, pct, paso);
            cv.Listo = p; cv.Version = ver; cv.Pct = pct; cv.Paso = paso;
            return p;
        }

        private void PctPaso(out int pct, out int paso)
        {
            pct = _perPct; paso = 1;
            try
            {
                var s = PlatformSettings;
                if (s != null)
                {
                    if (pct <= 0) pct = s.ValueAreaPercent;
                    paso = Math.Max(1, s.ValueAreaStep);
                }
            }
            catch { }
            if (pct <= 0 || pct > 100) pct = 70;
        }

        // ================================================================== dibujo
        // 1.2: cuanto le cuesta a ATAS (ms por dibujo y por tick de la vela en curso), al registro cada 60 s
        private readonly System.Diagnostics.Stopwatch _cron = new System.Diagnostics.Stopwatch();
        private double _msRender, _msRenderMax, _msVivo, _msVivoMax; private int _nRender, _nVivo;

        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                var pc = ChartInfo?.PriceChartContainer;
                if (pc == null || CurrentBar <= 0) return;
                var reg = pc.Region;
                if (reg.Width < 80 || reg.Height < 60) return;
                try { if (InstrumentInfo != null && InstrumentInfo.TickSize > 0) _tick = InstrumentInfo.TickSize; } catch { }
                int xDer = reg.Right - _margen;

                Dibujo dPer = null, dCol = null;
                int xTopeCol = xDer - 6;
                string avisoPer = "", avisoCol = "";
                if (_perfil && VentanaPerfilActual(out int pd, out int ph, out string pn))
                {
                    PctPaso(out int pct, out int paso);
                    var per = Calcular(_cPer, pd, ph, _perTicks, true, pct, paso);
                    dPer = DibujarPerfil(g, pc, reg, xDer, per, pn);
                    xTopeCol = xDer - _perAncho - 8;
                    avisoPer = Aviso(per, _perVentana == VentanaPerfil.Jornada, false);
                }
                if (_columna && VentanaColumnaActual(out int cd, out int ch, out string cn))
                {
                    var col = Calcular(_cCol, cd, ch, _colTicks, false, 0, 1);
                    dCol = DibujarColumna(g, pc, reg, xTopeCol, xDer, col, cn, Aviso(col, _colVentana == VentanaColumna.Jornada, true));
                    avisoCol = Aviso(col, _colVentana == VentanaColumna.Jornada, false);
                }
                _avisoBotonera = avisoPer.Length > 0 ? avisoPer : avisoCol;
                if (dPer != null && _lugarBot == LugarBotonera.Oculta) RotuloPerfil(g, reg, xDer, dPer, dCol);   // 1.4: con botonera, va en su boton
                lock (_llaveUi) { _dPer = dPer; _dCol = dCol; }
                DibujarBotonera(g, reg);
                if (_lupa) DibujarLupa(g, pc, reg);
                if (_registro && (DateTime.UtcNow - _ultAudit).TotalSeconds >= 60)
                {
                    _ultAudit = DateTime.UtcNow;
                    Auditar(pc, reg, xDer, dCol, dPer);
                    _msRender = _msRenderMax = _msVivo = _msVivoMax = 0; _nRender = _nVivo = 0;
                }
            }
            catch (Exception e) { Anotar(e); }
            finally
            {
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                _msRender += ms; _nRender++; if (ms > _msRenderMax) _msRenderMax = ms;
            }
        }

        /// <summary>1.2: el rotulo "PERFIL ... AV 70%" va arriba del perfil, pero si la columna esta encima de esa zona se sube por
        /// arriba del encabezado de la columna (en la 1.1 la columna lo tapaba).</summary>
        private void RotuloPerfil(RenderContext g, Rectangle reg, int xDer, Dibujo dPer, Dibujo dCol)
        {
            if (dPer.P == null || dPer.P.Filas == 0 || dPer.R.IsEmpty) return;
            var fe = Fuente(8, true);
            string rot = "PERFIL " + dPer.Nombre + "  AV " + dPer.P.Pct + "%";
            var sz = g.MeasureString(rot, fe);
            int x = xDer - sz.Width - 2, y = dPer.R.Top - sz.Height - 2;
            if (dCol != null && !dCol.R.IsEmpty && x < dCol.R.Right && x + sz.Width > dCol.R.Left)
            {
                int topeCol = dCol.R.Top - (_colEncab ? (dCol.Numeros ? 2 : 3) * 14 + 2 : 0);
                if (y + sz.Height > topeCol) y = topeCol - sz.Height - 2;
            }
            // 1.3b: ATAS pone su boton "ir a la ultima vela" (~20 px) arriba a la derecha del grafico: el rotulo va debajo
            y = Math.Max(reg.Top + 24, y);
            Texto(g, rot, fe, Color.FromArgb(215, 210, 215, 222), x, y);
        }

        private static readonly Dictionary<string, RenderFont> _fuentes = new Dictionary<string, RenderFont>();
        private static RenderFont Fuente(float tam, bool negrita)
        {
            string k = tam.ToString("0.0", Inv) + (negrita ? "b" : "");
            lock (_fuentes)
            {
                if (!_fuentes.TryGetValue(k, out var f))
                {
                    f = negrita ? new RenderFont("Segoe UI", tam, FontStyle.Bold) : new RenderFont("Segoe UI", tam);
                    _fuentes[k] = f;
                }
                return f;
            }
        }

        /// <summary>Borde de arriba y de abajo (px) de una fila de K ticks: el precio de cada nivel es el CENTRO de su renglon
        /// (GetYByPrice(precio, false), como dibuja ATAS sus lineas en MaxLevels) y el renglon mide PriceRowHeight.</summary>
        private void BordesFila(IChartContainer pc, Perfil p, int i, out int y0, out int y1)
        {
            double h = (double)pc.PriceRowHeight;
            int ya = pc.GetYByPrice(p.TickAlto(i) * _tick, false), yb = pc.GetYByPrice(p.TickBajo(i) * _tick, false);
            y0 = (int)Math.Floor(ya - h / 2.0); y1 = (int)Math.Ceiling(yb + h / 2.0);
            if (y1 <= y0) y1 = y0 + 1;
        }

        private static void Texto(RenderContext g, string s, RenderFont f, Color c, int x, int y)
        {
            g.DrawString(s, f, Color.FromArgb(200, 0, 0, 0), x + 1, y + 1);
            g.DrawString(s, f, c, x, y);
        }

        private string _avisoBotonera = "";
        private static readonly string[] DiasCortos = { "dom", "lun", "mar", "mie", "jue", "vie", "sab" };

        private Dibujo DibujarColumna(RenderContext g, IChartContainer pc, Rectangle reg, int xTope, int xDer, Perfil p, string nombreVentana, string aviso)
        {
            var d = new Dibujo { P = p, Nombre = nombreVentana };
            if (p.Filas == 0) return d;
            int ancho = _colAncho;
            int x0;
            if (_colLugar == LugarColumna.DespuesDeLaVela)
            {
                // 1.1: NUNCA encima de las velas. Va despues de la ultima vela; si no entra antes del eje, se apoya ENCIMA del perfil
                // (pegada al eje), no sobre las velas (la 1.0 se corria a la izquierda del perfil y tapaba la vela en curso).
                // 1.6: con el grafico corrido hacia atras (la ultima vela fuera de pantalla) no hay lugar libre en ningun lado: la
                // columna va AL LADO del perfil (a su izquierda), no encima (medido el 26-11: encimada se mezclaban las dos).
                int xv = reg.Right;
                try { xv = pc.GetXByBar(CurrentBar - 1, false) + (int)Math.Ceiling((double)pc.BarsWidth / 2.0); } catch { }
                x0 = xv + _colSep;
                bool vivaVisible = true;
                try { vivaVisible = LastVisibleBarNumber >= CurrentBar - 1; } catch { }
                if (x0 + ancho > xDer - 2) x0 = (!vivaVisible && xTope < xDer - 6) ? xTope - ancho : xDer - 2 - ancho;
            }
            else x0 = xTope - ancho;
            if (x0 < reg.Left + 2) return d;
            int x1 = x0 + ancho;

            // escala comun de la ventana
            decimal max = 0;
            for (int i = 0; i < p.Filas; i++)
            {
                decimal v = _colDato == DatoColumna.Volumen ? p.Vol[i] : Math.Abs(p.Delta(i));
                if (v > max) max = v;
            }
            // 1.4, ESTILO DE LOS REELS, pedido del operador: "se ven muy apretados, chocando los numeros; que sea mas
            // armonico, facil de leer; compara con los videos". Medido en los reels: la fila mide ~1,7-2 veces el alto del numero,
            // las cajas tienen 2-3 px de aire entre si, el numero va en negrita, alineado a la derecha ADENTRO de su caja, sobre un
            // panel gris; sin recuadros. Aca: la letra se elige MIDIENDO el texto (renglon <= alto de la fila, digito ~55 %); si
            // no entra ni la minima legible, la fila queda solo con su caja de color (el numero exacto lo da la lupa) y el
            // encabezado dice cuanto subir la fila. La cuenta y las filas NUNCA cambian con el zoom: solo la letra.
            BordesFila(pc, p, 0, out int ya0, out int yb0);
            int altoFila = Math.Max(1, yb0 - ya0);
            float letra = LetraParaFila(g, altoFila);
            bool numeros = letra > 0;
            d.Letra = (int)Math.Round(letra); d.Numeros = numeros;
            var fN = numeros ? Fuente(letra, letra >= 7.5f) : null;
            _colSinNumeros = !numeros;

            ColoresPaleta(out var mAlc, out var mBaj);
            Color cAlc = D(mAlc), cBaj = D(mBaj), cVol = D(_cAv), cTxt = Color.FromArgb(244, 246, 250), cOscuro = Color.FromArgb(16, 18, 24);
            Color cPanel = Color.FromArgb(232, 34, 38, 46);

            // panel gris (como el de los reels)
            BordesFila(pc, p, p.Filas - 1, out int yTop, out _);
            BordesFila(pc, p, 0, out _, out int yBot);
            // 1.8: tres renglones fijos (LIVE, fecha y hora del dato, total) + la pista de la fila y el aviso cuando hacen falta
            int lineasEnc = _colEncab ? (numeros ? 3 : 4) + (aviso.Length > 0 ? 1 : 0) : 0;
            int yEnc = yTop - lineasEnc * 14 - (lineasEnc > 0 ? 4 : 0);
            var fondo = new Rectangle(x0, Math.Max(reg.Top, yEnc), ancho, Math.Max(2, Math.Min(reg.Bottom, yBot + 2) - Math.Max(reg.Top, yEnc)));
            if (fondo.Height > 0) g.FillRectangle(cPanel, fondo, 3);
            d.R = new Rectangle(x0, yTop, ancho, Math.Max(1, yBot - yTop));

            // marca del ultimo precio DE LA VENTANA (1.5: en una jornada vieja, su cierre; en vivo, el precio actual)
            int tickUlt = int.MinValue;
            try { var cu = GetCandle(Math.Min(CurrentBar - 1, Math.Max(0, p.Hasta))); if (cu != null) tickUlt = Tick(cu.Close); } catch { }
            int aire = altoFila >= 14 ? 2 : altoFila >= 7 ? 1 : 0;   // aire entre cajas, como en los reels
            // 1.4b: con el encabezado fijo arriba, un numero que quedaria cortado debajo de el no se dibuja (nunca medio numero)
            int yTapa = (_colEncab && yEnc < reg.Top) ? reg.Top + lineasEnc * 14 + 5 : int.MinValue;

            for (int i = p.Filas - 1; i >= 0; i--)
            {
                BordesFila(pc, p, i, out int y0, out int y1);
                if (y1 < reg.Top || y0 > reg.Bottom) continue;
                decimal dl = p.Delta(i), vol = p.Vol[i];
                decimal mag = _colDato == DatoColumna.Volumen ? vol : Math.Abs(dl);
                string txt = _colDato == DatoColumna.Delta ? Calculos.Numero(dl, true)
                           : _colDato == DatoColumna.Volumen ? Calculos.Numero(vol, false)
                           : Calculos.Numero(p.Bid[i], false) + "x" + Calculos.Numero(p.Ask[i], false);
                if (vol == 0 && p.Bid[i] == 0 && p.Ask[i] == 0) txt = "";
                d.Filas.Add(new FilaDibujada { Fila = i, Y0 = y0, Y1 = y1, Texto = txt });
                int yc = y0 + aire / 2, alto = Math.Max(1, (y1 - y0) - aire);
                int w = 0; Color cCaja = cPanel; int alfa = 0;
                if (max > 0 && mag > 0)
                {
                    double r = (double)(mag / max);
                    w = Math.Max(3, (int)Math.Round((ancho - 4) * r));
                    Color cb = _colDato == DatoColumna.Volumen ? cVol : (dl >= 0 ? cAlc : cBaj);
                    alfa = 90 + (int)(150 * r);
                    cCaja = cb;
                    g.FillRectangle(Alfa(cb, alfa), new Rectangle(x1 - 2 - w, yc, w, alto));
                }
                int tb = p.TickBajo(i), ta = p.TickAlto(i);
                if (tickUlt >= tb && tickUlt <= ta)
                {
                    // marca del precio actual: triangulo a la izquierda de su fila
                    int ym = (y0 + y1) / 2, s = Math.Max(3, Math.Min(6, alto / 2));
                    g.FillPolygon(Color.FromArgb(240, 255, 255, 255), new[] { new Point(x0 - 1, ym), new Point(x0 - 1 - s, ym - s), new Point(x0 - 1 - s, ym + s) });
                }
                if (numeros && txt.Length > 0)
                {
                    var sz = g.MeasureString(txt, fN);
                    int tx = x1 - 6 - sz.Width, ty = y0 + (y1 - y0 - sz.Height) / 2;
                    if (ty < yTapa || ty + sz.Height > reg.Bottom + 1) { d.Filas[d.Filas.Count - 1].Texto = "(oculto)"; continue; }
                    // contraste: si el numero cae sobre una caja clara (paleta blanca o cian fuerte), va oscuro
                    bool sobreCaja = w >= sz.Width + 6;
                    bool claro = sobreCaja && Luminancia(Mezcla(cCaja, cPanel, alfa / 255.0)) > 0.58;
                    if (claro) g.DrawString(txt, fN, cOscuro, tx, ty);
                    else Texto(g, txt, fN, cTxt, tx, ty);
                }
            }

            if (_colEncab)
            {
                // 1.8, pedido del operador: "que diga la fecha y hora exacta a la que pertenece el dato mostrado, y un puntito con la
                // palabra LIVE en verde luminoso, o rojo apagado si no es en vivo". Renglon 1: punto + LIVE (verde con halo si la suma
                // incluye la vela en curso; rojo apagado si es un momento pasado) + el dato. Renglon 2: dia y hora con segundos (hora de
                // la PC, la misma del eje del grafico) de la ULTIMA operacion que entro en la suma. Renglon 3: el total.
                string pre = _colDato == DatoColumna.Delta ? "DELTA" : _colDato == DatoColumna.Volumen ? "VOL" : "V x C";
                bool enVivo = p.Viva != null; d.Live = enVivo;
                DateTime tDato = DateTime.MinValue;
                try
                {
                    if (enVivo) tDato = p.Viva.HoraFin > p.Viva.Hora ? p.Viva.HoraFin : p.Viva.Hora;
                    else { var cm = GetCandle(Math.Max(0, p.Hasta)); if (cm != null) tDato = cm.LastTime > cm.Time ? cm.LastTime : cm.Time; }
                }
                catch { }
                string t2 = _colDato == DatoColumna.Delta ? Calculos.Numero(p.SumAsk - p.SumBid, true)
                          : _colDato == DatoColumna.Volumen ? Calculos.Numero(p.SumVol, false)
                          : Calculos.Numero(p.SumBid, false) + "x" + Calculos.Numero(p.SumAsk, false);
                int ye = yEnc + 2;
                if (yEnc < reg.Top)
                {
                    // 1.4: la columna empieza por encima del borde: encabezado FIJO arriba, con fondo propio que tapa las filas que
                    // pasan por debajo (en la 1.3 se encimaba con los primeros numeros)
                    ye = reg.Top + 2;
                    g.FillRectangle(Color.FromArgb(250, 34, 38, 46), new Rectangle(x0, reg.Top, ancho, lineasEnc * 14 + 4), 3);
                    g.DrawLine(new RenderPen(Color.FromArgb(160, 120, 126, 136), 1), x0 + 3, reg.Top + lineasEnc * 14 + 4, x1 - 3, reg.Top + lineasEnc * 14 + 4);
                }
                // renglon 1: el punto y LIVE
                var f8 = Fuente(8, true);
                Color cLive = enVivo ? Color.FromArgb(255, 0, 255, 118) : Color.FromArgb(230, 128, 48, 48);
                int cy = ye + 7;
                if (enVivo) g.FillEllipse(Color.FromArgb(70, 0, 255, 118), new Rectangle(x0 + 3, cy - 6, 12, 12));   // halo luminoso
                g.FillEllipse(cLive, new Rectangle(x0 + 5, cy - 4, 8, 8));
                var szLive = g.MeasureString("LIVE", f8);
                g.DrawString("LIVE", f8, cLive, x0 + 17, ye);
                if (x0 + 17 + szLive.Width + 4 + g.MeasureString(pre, Fuente(7, true)).Width <= x1 - 3)
                    g.DrawString(pre, Fuente(7, true), Color.FromArgb(190, 200, 206, 216), x0 + 17 + szLive.Width + 4, ye + 1);
                // renglon 2: fecha y hora exacta del dato (se achica la letra para que entre; si no, sin el dia de la semana)
                if (tDato > DateTime.MinValue)
                {
                    var l = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(tDato, DateTimeKind.Utc), TimeZoneInfo.Local);
                    string cuando = DiasCortos[(int)l.DayOfWeek] + " " + l.ToString("dd-MM HH:mm:ss", Inv);
                    float fs = 7.5f; RenderFont fc = Fuente(fs, true);
                    while (g.MeasureString(cuando, fc).Width > ancho - 8 && fs > 6.5f) { fs -= 0.5f; fc = Fuente(fs, true); }
                    if (g.MeasureString(cuando, fc).Width > ancho - 8) cuando = l.ToString("dd-MM HH:mm:ss", Inv);
                    Texto(g, cuando, fc, enVivo ? Color.FromArgb(235, 236, 239, 244) : Color.FromArgb(215, 214, 168, 168), x0 + 5, ye + 13);
                    d.Cuando = cuando;
                }
                decimal tot = p.SumAsk - p.SumBid;
                Color c2 = _colDato == DatoColumna.Delta ? (tot >= 0 ? cAlc : cBaj) : cTxt;
                Texto(g, t2, Fuente(9, true), c2, x0 + 5, ye + 26);
                // sin alto para un numero legible: se dice cuanto subir la fila (el valor exacto sale igual con la lupa)
                if (!numeros)
                {
                    double pxTick = altoFila / (double)Math.Max(1, p.K);
                    int kSug = TicksFila[TicksFila.Length - 1];
                    foreach (int k in TicksFila) if (k > p.K && k * pxTick >= 13) { kSug = k; break; }
                    string pista = "subi a fila " + Puntos(kSug) + " o acerca";
                    if (g.MeasureString(pista, Fuente(7, false)).Width > ancho - 8) pista = "subi a fila " + Puntos(kSug);   // 1.6: que entre
                    Texto(g, pista, Fuente(7, false), Color.FromArgb(230, 255, 214, 0), x0 + 5, ye + 42);
                }
                // 1.5: la suma no es la jornada entera o falta footprint: se dice en amarillo (el detalle va en la botonera)
                if (aviso.Length > 0)
                    Texto(g, aviso, Fuente(7, true), Color.FromArgb(240, 255, 214, 0), x0 + 5, ye + (numeros ? 42 : 56));
            }
            return d;
        }

        private bool _colSinNumeros;
        private readonly Dictionary<float, int> _altoTexto = new Dictionary<float, int>();

        /// <summary>La letra mas grande (hasta la maxima de los ajustes) cuyo renglon medido (con su interlineado) no pasa el alto de la fila;
        /// 0 si ni 6,5 entra (la fila queda solo con color). El digito ocupa ~55 % de la fila: el aire de los reels (fila = 1,7-2 digitos).</summary>
        private float LetraParaFila(RenderContext g, int altoFila)
        {
            for (float f = _colLetra; f >= 6.5f; f -= 0.5f)
            {
                if (!_altoTexto.TryGetValue(f, out int h)) { h = g.MeasureString("+8888", Fuente(f, f >= 7.5f)).Height; _altoTexto[f] = h; }
                if (h <= altoFila) return f;
            }
            return 0;
        }

        private static Color Mezcla(Color a, Color fondo, double t)
            => Color.FromArgb(255, (int)(a.R * t + fondo.R * (1 - t)), (int)(a.G * t + fondo.G * (1 - t)), (int)(a.B * t + fondo.B * (1 - t)));

        private static double Luminancia(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;

        private Dibujo DibujarPerfil(RenderContext g, IChartContainer pc, Rectangle reg, int xDer, Perfil p, string nombreVentana)
        {
            var d = new Dibujo { P = p, Nombre = nombreVentana };
            if (p.Filas == 0 || p.SumVol <= 0) return d;
            int ancho = _perAncho;
            decimal vmax = 0; for (int i = 0; i < p.Filas; i++) if (p.Vol[i] > vmax) vmax = p.Vol[i];
            if (vmax <= 0) return d;
            int a = (int)Math.Round(255 * _perOpac / 100.0);
            Color cAv = Alfa(D(_cAv), a), cFuera = Alfa(D(_cFuera), (int)(a * 0.75)), cPoc = Alfa(D(_cPoc), Math.Min(255, a + 60));
            ColoresPaleta(out var mAlc, out var mBaj);
            Color cAlc = Alfa(D(mAlc), a), cBaj = Alfa(D(mBaj), a);
            BordesFila(pc, p, p.Filas - 1, out int yTop, out _);
            BordesFila(pc, p, 0, out _, out int yBot);
            d.R = new Rectangle(xDer - ancho, yTop, ancho, Math.Max(1, yBot - yTop));

            for (int i = 0; i < p.Filas; i++)
            {
                if (p.Vol[i] <= 0) continue;
                BordesFila(pc, p, i, out int y0, out int y1);
                if (y1 < reg.Top || y0 > reg.Bottom) continue;
                d.Filas.Add(new FilaDibujada { Fila = i, Y0 = y0, Y1 = y1, Texto = Calculos.Numero(p.Vol[i], false) });
                int alto = Math.Max(1, (y1 - y0) - ((y1 - y0) >= 5 ? 1 : 0));
                int w = Math.Max(1, (int)Math.Round(ancho * (double)(p.Vol[i] / vmax)));
                bool dentro = i >= p.Val && i <= p.Vah;
                if (_perPartido && p.Vol[i] > 0)
                {
                    // derecha (pegado al eje) compras, a su izquierda ventas, y lo que no tiene agresor en gris
                    int wc = (int)Math.Round(w * (double)(p.Ask[i] / p.Vol[i]));
                    int wv = (int)Math.Round(w * (double)(p.Bid[i] / p.Vol[i]));
                    if (wc + wv > w) wv = w - wc;
                    g.FillRectangle(cAlc, new Rectangle(xDer - wc, y0, Math.Max(0, wc), alto));
                    g.FillRectangle(cBaj, new Rectangle(xDer - wc - wv, y0, Math.Max(0, wv), alto));
                    if (w - wc - wv > 0) g.FillRectangle(cFuera, new Rectangle(xDer - w, y0, w - wc - wv, alto));
                    if (i == p.Poc) g.DrawRectangle(new RenderPen(cPoc, 1), new Rectangle(xDer - w, y0, w, alto));
                }
                else g.FillRectangle(i == p.Poc ? cPoc : dentro ? cAv : cFuera, new Rectangle(xDer - w, y0, w, alto));
            }

            // POC / VAH / VAL: lineas desde el comienzo de la ventana y precio en el eje
            int xIni = reg.Left;
            try { xIni = Math.Max(reg.Left, pc.GetXByBar(p.Desde, true)); } catch { }
            decimal pPoc = (p.TickBajo(p.Poc) + p.TickAlto(p.Poc)) / 2m * _tick;
            decimal pVah = p.TickAlto(p.Vah) * _tick, pVal = p.TickBajo(p.Val) * _tick;
            d.YPoc = pc.GetYByPrice(pPoc, false); d.YVah = pc.GetYByPrice(pVah, false); d.YVal = pc.GetYByPrice(pVal, false);
            var fe = Fuente(8, true);
            if (_perLineas)
            {
                var penPoc = new RenderPen(Alfa(D(_cPoc), 230), 2);
                var penVa = new RenderPen(Alfa(D(_cAv), 230), 1, System.Drawing.Drawing2D.DashStyle.Dash);
                if (d.YVah >= reg.Top && d.YVah <= reg.Bottom) g.DrawLine(penVa, xIni, d.YVah, xDer, d.YVah);
                if (d.YVal >= reg.Top && d.YVal <= reg.Bottom) g.DrawLine(penVa, xIni, d.YVal, xDer, d.YVal);
                if (d.YPoc >= reg.Top && d.YPoc <= reg.Bottom) g.DrawLine(penPoc, xIni, d.YPoc, xDer, d.YPoc);
                // 1.1: el nombre de cada linea va adentro del perfil, pegado al eje y apenas arriba de su linea (en la 1.0 iba a la
                // izquierda del perfil y la columna lo tapaba)
                foreach (var (nom, y) in new[] { ("VAH", d.YVah), ("POC", d.YPoc), ("VAL", d.YVal) })
                {
                    if (y < reg.Top + 14 || y > reg.Bottom - 2) continue;
                    var sz = g.MeasureString(nom, fe);
                    Texto(g, nom, fe, Color.FromArgb(240, 240, 242, 246), xDer - sz.Width - 4, y - sz.Height - 1);
                }
            }
            if (_perEtiq)
            {
                string Pr(decimal v) { try { return ChartInfo.GetPriceString(v); } catch { return v.ToString("0.00", Inv); } }
                try
                {
                    var fa = Fuente(8, true);
                    if (d.YVah >= reg.Top && d.YVah <= reg.Bottom) this.DrawLabelOnPriceAxis(g, Pr(pVah), d.YVah, fa, Alfa(D(_cAv), 255), Color.White);
                    if (d.YVal >= reg.Top && d.YVal <= reg.Bottom) this.DrawLabelOnPriceAxis(g, Pr(pVal), d.YVal, fa, Alfa(D(_cAv), 255), Color.White);
                    if (d.YPoc >= reg.Top && d.YPoc <= reg.Bottom) this.DrawLabelOnPriceAxis(g, Pr(pPoc), d.YPoc, fa, Alfa(D(_cPoc), 255), Color.Black);
                }
                catch (Exception e) { Anotar(e); }
            }
            // el rotulo del perfil lo dibuja RotuloPerfil despues de la columna (1.2)
            return d;
        }

        // ================================================================== botonera (un clic)
        private void Boton(RenderContext g, string id, string texto, bool prendido, ref int x, int y, Color acento, MColor? c1 = null, MColor? c2 = null)
        {
            var f = Fuente(8, true);
            var sz = g.MeasureString(texto, f);
            int extra = c1.HasValue ? 22 : 0;
            var r = new Rectangle(x, y, sz.Width + 12 + extra, 18);
            g.FillRectangle(prendido ? Alfa(acento, 215) : Color.FromArgb(185, 38, 42, 50), r, 3);
            g.DrawRectangle(new RenderPen(prendido ? Color.FromArgb(230, 255, 255, 255) : Color.FromArgb(150, 140, 146, 156), 1), r, 3);
            int tx = r.X + 6;
            if (c1.HasValue && c2.HasValue)
            {
                g.FillRectangle(D(c1.Value), new Rectangle(tx, r.Y + 4, 8, 10));
                g.FillRectangle(D(c2.Value), new Rectangle(tx + 9, r.Y + 4, 8, 10));
                tx += extra;
            }
            g.DrawString(texto, f, prendido ? Color.White : Color.FromArgb(200, 200, 205, 212), tx, r.Y + (18 - sz.Height) / 2);
            lock (_llaveUi) _botones.Add(new KeyValuePair<string, Rectangle>(id, r));
            x = r.Right + 4;
        }

        private void DibujarBotonera(RenderContext g, Rectangle reg)
        {
            lock (_llaveUi) _botones.Clear();
            if (_lugarBot == LugarBotonera.Oculta) return;
            int y = _lugarBot == LugarBotonera.ArribaIzquierda ? reg.Top + 4 : reg.Bottom - 24;
            int x = reg.Left + 6;
            ColoresPaleta(out var alc, out var baj);
            Color acento = Color.FromArgb(46, 125, 190);
            string nomPal = _paleta == Paleta.Personalizada ? "Propia" : Paletas[_paleta].Corto;
            Boton(g, "velas", "VELAS", _velas, ref x, y, acento);
            Boton(g, "paleta", nomPal, true, ref x, y, Color.FromArgb(60, 64, 72), alc, baj);
            x += 6;
            Boton(g, "columna", "DELTA", _columna, ref x, y, acento);
            string vc = _colVentana == VentanaColumna.VelaEnCurso ? "vela" : _colVentana == VentanaColumna.UltimasN ? _colVelas + " velas"
                      : _colVentana == VentanaColumna.Sesion ? "sesion" : _colVentana == VentanaColumna.Jornada ? "jornada" : "visible";
            Boton(g, "colventana", vc, _columna, ref x, y, Color.FromArgb(60, 64, 72));
            string dc = _colDato == DatoColumna.Delta ? "delta" : _colDato == DatoColumna.Volumen ? "vol" : "VxC";
            Boton(g, "coldato", dc, _columna, ref x, y, Color.FromArgb(60, 64, 72));
            int xFila = x;
            Boton(g, "colticks", "fila " + Puntos(_colTicks), _columna, ref x, y, Color.FromArgb(60, 64, 72));
            if (_columna && _colSinNumeros)   // 1.4: sin lugar para los numeros, el boton que lo arregla se marca en amarillo
                g.DrawRectangle(new RenderPen(Color.FromArgb(240, 255, 214, 0), 2), new Rectangle(xFila - 1, y - 1, x - 4 - xFila + 2, 20), 3);
            x += 6;
            Boton(g, "perfil", "PERFIL", _perfil, ref x, y, acento);
            string vp = _perVentana == VentanaPerfil.Sesion ? "sesion" : _perVentana == VentanaPerfil.Rth ? "RTH"
                      : _perVentana == VentanaPerfil.UltimasN ? _perVelas + " velas" : _perVentana == VentanaPerfil.Jornada ? "jornada" : "visible";
            int pctAv = _perPct; if (pctAv <= 0) { PctPaso(out pctAv, out _); }
            Boton(g, "perventana", vp + " - AV " + pctAv + "%", _perfil, ref x, y, Color.FromArgb(60, 64, 72));
            // 1.7: hasta que vela se suma todo: "al momento" (la ultima a la vista: al correr el grafico se rebobina) o "en vivo"
            x += 6;
            Boton(g, "hasta", _hasta == HastaVela.ElMomento ? "al momento" : "en vivo", _hasta == HastaVela.ElMomento, ref x, y, acento);

            // 1.5: la jornada elegida, con un clic: < una mas vieja, > una mas nueva (al llegar a la ultima vuelve a la del momento),
            // el rotulo cambia la parte (completa / RTH / noche; con Mayus para atras) y "fijar" congela la que se esta viendo
            bool usaJornada = (_columna && _colVentana == VentanaColumna.Jornada) || (_perfil && _perVentana == VentanaPerfil.Jornada);
            if (!usaJornada) return;
            x += 6;
            var e = _ej;
            Color gris = Color.FromArgb(60, 64, 72);
            Boton(g, "jor_atras", " < ", true, ref x, y, gris);
            string etq = e.Clave > 0 ? Jornadas.Rotulo(e.Clave) + " " + Jornadas.NombreParte(e.Parte) : "sin jornadas";
            if (e.NoCargada) etq += " (no cargada)";
            else if (!e.Hay) etq += e.Parte == Jornadas.ParteRth ? " (todavia sin RTH)" : " (sin velas)";
            else if (e.EnCurso) etq += " (en curso)";
            else if (e.Recortada) { try { etq += " hasta " + HoraLocal(GetCandle(e.Hasta).Time); } catch { } }
            if (_jorModo == ModoJornada.Fecha) etq = "fija: " + etq;
            int xEtq = x;
            Boton(g, "jor_parte", etq, true, ref x, y, acento);
            if (_avisoBotonera.Length > 0)
                g.DrawRectangle(new RenderPen(Color.FromArgb(240, 255, 214, 0), 2), new Rectangle(xEtq - 1, y - 1, x - 4 - xEtq + 2, 20), 3);
            Boton(g, "jor_adelante", " > ", true, ref x, y, gris);
            Boton(g, "jor_fijar", "fijar", _jorModo == ModoJornada.Fecha, ref x, y, acento);
            if (_avisoBotonera.Length > 0)
            {
                var fa = Fuente(8, true);
                var sz = g.MeasureString(_avisoBotonera, fa);
                // 1.6: si no entra al lado de la botonera (se cortaba contra el eje), va en su propio renglon
                int xa = x + 2, ya = y;
                if (xa + sz.Width + 10 > reg.Right - 4)
                {
                    xa = reg.Left + 6;
                    ya = _lugarBot == LugarBotonera.ArribaIzquierda ? y + 22 : y - 22;
                }
                g.FillRectangle(Color.FromArgb(225, 20, 22, 28), new Rectangle(xa, ya, sz.Width + 10, 18), 3);
                g.DrawString(_avisoBotonera, fa, Color.FromArgb(250, 255, 214, 0), xa + 5, ya + (18 - sz.Height) / 2);
            }
        }

        /// <summary>1.5: mueve la jornada elegida (-1 = una mas vieja, +1 = una mas nueva). Llegar a la ultima con '>' vuelve al modo
        /// "la ultima" (asi en vivo y en el replay sigue sola al dia nuevo).</summary>
        private void MoverJornada(int paso)
        {
            int clave = -1; bool esUltima = false;
            lock (_llave)
            {
                int total = _jornadas.Count;
                if (total == 0) return;
                int idx = _ej.Indice;
                if (idx < 0 || idx >= total || _jornadas[idx].Clave != _ej.Clave)
                {
                    // la fecha elegida no esta cargada: '<' va a la mas nueva anterior a esa fecha y '>' a la mas vieja posterior
                    idx = -1;
                    if (paso < 0) { for (int k = total - 1; k >= 0; k--) if (_jornadas[k].Clave < _ej.Clave) { idx = k; break; } if (idx < 0) idx = 0; }
                    else { for (int k = 0; k < total; k++) if (_jornadas[k].Clave > _ej.Clave) { idx = k; break; } if (idx < 0) idx = total - 1; }
                    paso = 0;
                }
                int n = Math.Max(0, Math.Min(total - 1, idx + paso));
                clave = _jornadas[n].Clave; esUltima = n == total - 1;
            }
            // 1.6/1.7: '>' hasta la ultima vuelve a la del momento; si no, queda FIJA (completa) hasta que se mueva el grafico
            if (esUltima && paso > 0) SpJornadaCual = ModoJornada.Momento;
            else { SpJornadaFecha = clave; SpJornadaCual = ModoJornada.Fecha; FijarAnclaVista(); }
            RaisePropertyChanged(nameof(SpJornadaFecha)); RaisePropertyChanged(nameof(SpJornadaCual));
        }

        /// <summary>1.6: recuerda donde estaba la vista al fijar la fecha; si despues se mueve el grafico, la fecha se suelta.</summary>
        private void FijarAnclaVista()
        {
            try { int ultima = CurrentBar - 1; _fechaFin = ultima - Math.Min(ultima, LastVisibleBarNumber); } catch { _fechaFin = int.MinValue; }
        }

        /// <summary>Alto de una fila en puntos del instrumento ("1 pt", "0,5 pt", "1 tick").</summary>
        private string Puntos(int k)
        {
            if (k == 1) return "1 tick";
            decimal p = k * _tick;
            return (p == decimal.Truncate(p) ? p.ToString("0", Inv) : p.ToString("0.##", Inv).Replace('.', ',')) + " pt";
        }

        private static readonly int[] TicksFila = { 1, 2, 4, 8, 12, 20, 40 };

        private string BotonEn(int x, int y)
        {
            lock (_llaveUi)
                foreach (var b in _botones) if (b.Value.Contains(x, y)) return b.Key;
            return null;
        }

        private void Accion(string id, bool atras)
        {
            switch (id)
            {
                case "velas": SpVelas = !_velas; RaisePropertyChanged(nameof(SpVelas)); break;
                case "paleta":
                    {
                        int n = Enum.GetValues(typeof(Paleta)).Length, i = (int)_paleta;
                        i = atras ? (i - 1 + n) % n : (i + 1) % n;
                        SpPaleta = (Paleta)i; RaisePropertyChanged(nameof(SpPaleta)); break;
                    }
                case "columna": SpColumna = !_columna; RaisePropertyChanged(nameof(SpColumna)); break;
                case "colventana":
                    {
                        int n = Enum.GetValues(typeof(VentanaColumna)).Length, i = (int)_colVentana;
                        SpColPeriodo = (VentanaColumna)(atras ? (i - 1 + n) % n : (i + 1) % n); RaisePropertyChanged(nameof(SpColPeriodo)); break;
                    }
                case "coldato":
                    {
                        int n = Enum.GetValues(typeof(DatoColumna)).Length, i = (int)_colDato;
                        SpColDato = (DatoColumna)(atras ? (i - 1 + n) % n : (i + 1) % n); RaisePropertyChanged(nameof(SpColDato)); break;
                    }
                case "colticks":
                    {
                        int i = Array.IndexOf(TicksFila, _colTicks);
                        if (i < 0) { i = 0; while (i < TicksFila.Length - 1 && TicksFila[i] < _colTicks) i++; if (!atras) i--; }
                        i = atras ? (i - 1 + TicksFila.Length) % TicksFila.Length : (i + 1) % TicksFila.Length;
                        SpColTicks = TicksFila[i]; RaisePropertyChanged(nameof(SpColTicks)); break;
                    }
                case "perfil": SpPerfil = !_perfil; RaisePropertyChanged(nameof(SpPerfil)); break;
                case "perventana":
                    {
                        int n = Enum.GetValues(typeof(VentanaPerfil)).Length, i = (int)_perVentana;
                        SpPerVentana = (VentanaPerfil)(atras ? (i - 1 + n) % n : (i + 1) % n); RaisePropertyChanged(nameof(SpPerVentana)); break;
                    }
                case "jor_atras": MoverJornada(-1); break;
                case "jor_adelante": MoverJornada(+1); break;
                case "jor_parte":
                    {
                        int n = Enum.GetValues(typeof(ParteJornada)).Length, i = (int)_jorParte;
                        SpJornadaParte = (ParteJornada)(atras ? (i - 1 + n) % n : (i + 1) % n); RaisePropertyChanged(nameof(SpJornadaParte)); break;
                    }
                case "jor_fijar":
                    if (_jorModo == ModoJornada.Momento)
                    {
                        // se congela la jornada que se esta viendo, completa (queda fija hasta que se mueva el grafico)
                        if (_ej.Clave > 0 && !_ej.NoCargada) { SpJornadaFecha = _ej.Clave; RaisePropertyChanged(nameof(SpJornadaFecha)); }
                        SpJornadaCual = ModoJornada.Fecha; FijarAnclaVista();
                    }
                    else SpJornadaCual = ModoJornada.Momento;
                    RaisePropertyChanged(nameof(SpJornadaCual)); break;
                case "hasta":
                    SpHasta = _hasta == HastaVela.ElMomento ? HastaVela.EnVivo : HastaVela.ElMomento; RaisePropertyChanged(nameof(SpHasta)); break;
            }
            AnotarLinea("boton " + id + (atras ? " (atras)" : "") + " -> velas " + _velas + ", paleta " + _paleta + ", columna " + _columna + " " + _colVentana + " " + _colTicks + "t"
                        + " " + _colDato + ", perfil " + _perfil + " " + _perVentana + ", hasta " + _hasta + ", jornada " + _jorModo + " " + _jorFecha + " " + _jorParte);
            Redibujar();
        }

        public override bool ProcessMouseDown(RenderControlMouseEventArgs e)
        {
            try
            {
                if (e.Button == RenderControlMouseButtons.Left)
                {
                    var id = BotonEn(e.X, e.Y);
                    if (id != null) { Accion(id, e.Shift); return true; }
                }
            }
            catch (Exception ex) { Anotar(ex); }
            return base.ProcessMouseDown(e);
        }

        public override bool ProcessMouseUp(RenderControlMouseEventArgs e)
        {
            try { if (BotonEn(e.X, e.Y) != null) return true; } catch { }
            return base.ProcessMouseUp(e);
        }

        public override bool ProcessMouseClick(RenderControlMouseEventArgs e)
        {
            try { if (BotonEn(e.X, e.Y) != null) return true; } catch { }
            return base.ProcessMouseClick(e);
        }

        public override bool ProcessMouseDoubleClick(RenderControlMouseEventArgs e)
        {
            try { if (BotonEn(e.X, e.Y) != null) return true; } catch { }
            return base.ProcessMouseDoubleClick(e);
        }

        public override StdCursor GetCursor(RenderControlMouseEventArgs e)
        {
            try { if (BotonEn(e.X, e.Y) != null) return StdCursor.Hand; } catch { }
            return base.GetCursor(e);
        }

        public override bool ProcessMouseMove(RenderControlMouseEventArgs e)
        {
            try
            {
                _mouseUlt = new Point(e.X, e.Y);
                string h = HoverActual();
                if (h != _hoverUlt) { _hoverUlt = h; Redibujar(); }
            }
            catch { }
            return base.ProcessMouseMove(e);
        }

        // ================================================================== lupa
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct PuntoW { public int X, Y; }
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out PuntoW p);
        private readonly object _llaveMouse = new object();
        private Point _posAtasUlt = new Point(int.MinValue, int.MinValue), _cursorAncla;
        private bool _hayAncla;

        /// <summary>Posicion del mouse sobre el grafico, o (-1,-1) si esta afuera. 1.3: ATAS NO siempre avisa IsMouseLeave (medido el
        /// 23-11: con el mouse ya en la cinta de opciones de arriba la lupa seguia mostrando la ultima fila, tapando la columna; lo
        /// mismo le paso a la Estadistica Visible el 29-10). Regla: se ancla el cursor REAL de Windows cada vez que ATAS reporta una
        /// posicion nueva; si el cursor real se aleja mas de 4 px del ancla y ATAS sigue con la misma posicion, el mouse se fue.</summary>
        private Point PosMouse()
        {
            try
            {
                var mi = ChartInfo?.MouseLocationInfo ?? MouseLocationInfo;
                if (mi == null) return new Point(-1, -1);
                if (mi.IsMouseLeave || mi.IsMovingChartUsingMouse) return new Point(-1, -1);
                var pg = mi.LastPosition;
                if (GetCursorPos(out var w))
                {
                    var pr = new Point(w.X, w.Y);
                    lock (_llaveMouse)
                    {
                        if (pg != _posAtasUlt) { _posAtasUlt = pg; _cursorAncla = pr; _hayAncla = true; }
                        else if (_hayAncla && (Math.Abs(pr.X - _cursorAncla.X) > 4 || Math.Abs(pr.Y - _cursorAncla.Y) > 4)) return new Point(-1, -1);
                    }
                }
                var reg = ChartInfo?.PriceChartContainer?.Region ?? Rectangle.Empty;
                if (!reg.IsEmpty && !reg.Contains(pg)) return new Point(-1, -1);
                return pg;
            }
            catch { }
            return new Point(-1, -1);
        }

        private string HoverActual()
        {
            var m = PosMouse();
            if (m.X < 0) return "";
            string b = BotonEn(m.X, m.Y);
            if (b != null) return "b:" + b;
            lock (_llaveUi)
            {
                foreach (var (dd, tipo) in new[] { (_dCol, "c"), (_dPer, "p") })
                {
                    if (dd == null || dd.R.IsEmpty) continue;
                    if (m.X < dd.R.Left || m.X > dd.R.Right) continue;
                    foreach (var f in dd.Filas) if (m.Y >= f.Y0 && m.Y < f.Y1) return tipo + ":" + f.Fila;
                }
            }
            return "";
        }

        private void DibujarLupa(RenderContext g, IChartContainer pc, Rectangle reg)
        {
            string h = HoverActual();
            if (h.Length < 3 || h.StartsWith("b:")) return;
            Dibujo dd; lock (_llaveUi) dd = h[0] == 'c' ? _dCol : _dPer;
            if (dd == null || dd.P == null) return;
            int i = int.Parse(h.Substring(2), Inv);
            var p = dd.P; if (i < 0 || i >= p.Filas) return;
            string Pr(decimal v) { try { return ChartInfo.GetPriceString(v); } catch { return v.ToString("0.00", Inv); } }
            decimal pb = p.TickBajo(i) * _tick, pa = p.TickAlto(i) * _tick;
            var lineas = new List<string>
            {
                (h[0] == 'c' ? "COLUMNA " : "PERFIL ") + dd.Nombre + "  |  " + (p.K == 1 ? Pr(pb) : Pr(pb) + " a " + Pr(pa) + " (" + p.K + " ticks)"),
                "Compras " + Calculos.Miles(p.Ask[i]) + "   Ventas " + Calculos.Miles(p.Bid[i]),
                "Delta " + (p.Delta(i) > 0 ? "+" : "") + Calculos.Miles(p.Delta(i)) + "   Volumen " + Calculos.Miles(p.Vol[i])
            };
            if (h[0] == 'p' && p.SumVol > 0)
                lineas.Add(((double)(p.Vol[i] / p.SumVol) * 100).ToString("0.0", Inv).Replace('.', ',') + " % del perfil" + (i == p.Poc ? "  |  POC" : (i >= p.Val && i <= p.Vah) ? "  |  dentro del area de valor" : "  |  fuera del area de valor"));
            if (h[0] == 'c' && p.Vol[i] - p.Ask[i] - p.Bid[i] != 0)
                lineas.Add("Sin agresor: " + Calculos.Miles(p.Vol[i] - p.Ask[i] - p.Bid[i]));
            lineas.Add("Velas " + p.Velas + "  |  " + Ventana(p));
            var f = Fuente(9, false); var fb = Fuente(9, true);
            int w = 0, hl = 0;
            foreach (var s in lineas) { var sz = g.MeasureString(s, f); w = Math.Max(w, sz.Width); hl = Math.Max(hl, sz.Height); }
            w += 16; int alto = lineas.Count * (hl + 2) + 8;
            var m = PosMouse();
            int x = m.X - w - 14; if (x < reg.Left + 4) x = m.X + 18;
            int y = Math.Max(reg.Top + 4, Math.Min(reg.Bottom - alto - 4, m.Y - alto / 2));
            var r = new Rectangle(x, y, w, alto);
            g.FillRectangle(Color.FromArgb(235, 22, 25, 31), r, 4);
            g.DrawRectangle(new RenderPen(Color.FromArgb(220, 255, 214, 0), 1), r, 4);
            for (int k = 0; k < lineas.Count; k++) g.DrawString(lineas[k], k == 0 ? fb : f, Color.FromArgb(236, 239, 244), x + 8, y + 5 + k * (hl + 2));
            // marca la fila
            var fd = dd.Filas.Find(q => q.Fila == i);
            if (fd != null) g.DrawRectangle(new RenderPen(Color.FromArgb(240, 255, 214, 0), 1), new Rectangle(dd.R.Left, fd.Y0, dd.R.Width, Math.Max(1, fd.Y1 - fd.Y0)));
        }

        private string Ventana(Perfil p)
        {
            try
            {
                var a = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(p.T0, DateTimeKind.Utc), TimeZoneInfo.Local);
                var b = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(p.T1, DateTimeKind.Utc), TimeZoneInfo.Local);
                return a.ToString("dd/MM HH:mm", Inv) + " a " + b.ToString("HH:mm", Inv) + " (hora local)";
            }
            catch { return ""; }
        }

        // ================================================================== registro de auditoria
        private static string Carpeta()
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex", "superindicador");
            Directory.CreateDirectory(d);
            return d;
        }

        private string Inst() { try { return (InstrumentInfo?.Instrument ?? "inst").Replace(' ', '_').Replace('/', '_'); } catch { return "inst"; } }
        private string Marco() { try { return (ChartInfo?.TimeFrame ?? "tf").Replace(' ', '_'); } catch { return "tf"; } }

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
                lock (_llaveArchivo) File.AppendAllText(Path.Combine(Carpeta(), "superindicador.log"), linea);
            }
            catch { }
        }

        private void VolcarCinta()
        {
            if (_cinta.IsEmpty) return;
            var sb = new StringBuilder();
            int n = 0;
            while (n < 200000 && _cinta.TryDequeue(out var s)) { sb.Append(s).Append('\n'); n++; }
            if (n == 0) return;
            string ruta = Path.Combine(Carpeta(), "cinta-" + Inst() + "-" + DateTime.UtcNow.ToString("yyyyMMdd", Inv) + ".csv");
            string txt = sb.ToString();
            Task.Run(() =>
            {
                try
                {
                    lock (_llaveArchivo)
                    {
                        bool nuevo = !File.Exists(ruta);
                        File.AppendAllText(ruta, (nuevo ? "hora_utc,precio,volumen,agresor\n" : "") + txt);
                    }
                }
                catch { }
            });
        }

        private static string F(decimal v) => v.ToString(Inv);

        private void Auditar(IChartContainer pc, Rectangle reg, int xDer, Dibujo dCol, Dibujo dPer)
        {
            try
            {
                int ultima = CurrentBar - 1;
                var sb = new StringBuilder(64 * 1024);
                PctPaso(out int pct, out int paso);
                int vaAtasPct = -1, vaAtasPaso = -1;
                try { vaAtasPct = PlatformSettings.ValueAreaPercent; vaAtasPaso = PlatformSettings.ValueAreaStep; } catch { }
                decimal precioRef = 0; int yF = 0, yT = 0;
                try { var cu = GetCandle(ultima); precioRef = cu.Close; yF = pc.GetYByPrice(precioRef, false); yT = pc.GetYByPrice(precioRef, true); } catch { }
                Rectangle area = Rectangle.Empty; try { area = ChartArea; } catch { }
                sb.Append("{\"utc\":\"").Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv)).Append("\",\"v\":\"").Append(Version)
                  .Append("\",\"inst\":\"").Append(Inst()).Append("\",\"tf\":\"").Append(Marco()).Append("\",\"tick\":").Append(F(_tick))
                  .Append(",\"ultima\":").Append(ultima).Append(",\"ultima_hora\":\"").Append(GetCandle(ultima).Time.ToString("yyyy-MM-ddTHH:mm:ss", Inv)).Append('"')
                  .Append(",\"va_plataforma\":[").Append(vaAtasPct).Append(',').Append(vaAtasPaso).Append("],\"va_usada\":[").Append(pct).Append(',').Append(paso).Append(']')
                  .Append(",\"y_ref\":{\"precio\":").Append(F(precioRef)).Append(",\"y_false\":").Append(yF).Append(",\"y_true\":").Append(yT)
                  .Append(",\"fila_px\":").Append(F(pc.PriceRowHeight)).Append("}")
                  .Append(",\"reg\":[").Append(reg.X).Append(',').Append(reg.Y).Append(',').Append(reg.Width).Append(',').Append(reg.Height).Append(']')
                  .Append(",\"chartarea\":[").Append(area.X).Append(',').Append(area.Y).Append(',').Append(area.Width).Append(',').Append(area.Height).Append(']')
                  .Append(",\"xder\":").Append(xDer)
                  .Append(",\"visibles\":[").Append(FirstVisibleBarNumber).Append(',').Append(LastVisibleBarNumber).Append(']')
                  .Append(",\"carga\":").Append(_carga);
                // 1.5: la jornada elegida y la lista de jornadas con sus velas (para rehacer por fuera la division por jornada)
                var ej = _ej;
                sb.Append(",\"jornada\":{\"hasta_modo\":\"").Append(_hasta).Append("\",\"momento\":").Append(ej.Momento).Append(",\"recortada\":").Append(ej.Recortada ? "true" : "false").Append(",\"modo\":\"").Append(_jorModo).Append("\",\"fecha\":").Append(_jorFecha).Append(",\"parte\":\"").Append(_jorParte)
                  .Append("\",\"clave\":").Append(ej.Clave).Append(",\"vista_clave\":").Append(ej.VistaClave).Append(",\"indice\":").Append(ej.Indice).Append(",\"hay\":").Append(ej.Hay ? "true" : "false")
                  .Append(",\"desde\":").Append(ej.Desde).Append(",\"hasta\":").Append(ej.Hasta).Append(",\"cortada\":").Append(ej.Cortada ? "true" : "false")
                  .Append(",\"en_curso\":").Append(ej.EnCurso ? "true" : "false").Append(",\"no_cargada\":").Append(ej.NoCargada ? "true" : "false")
                  .Append(",\"primera_ny\":\"").Append(ej.PrimeraNy.ToString("yyyy-MM-ddTHH:mm:ss", Inv)).Append("\",\"aviso\":\"").Append(_avisoBotonera.Replace("\"", "'"))
                  .Append("\",\"lista\":[");
                lock (_llave)
                    for (int k = 0; k < _jornadas.Count; k++)
                    {
                        var j = _jornadas[k];
                        if (k > 0) sb.Append(',');
                        sb.Append('[').Append(j.Clave).Append(',').Append(j.Primera).Append(',').Append(j.Ultima).Append(',').Append(j.NochePrimera).Append(',')
                          .Append(j.NocheUltima).Append(',').Append(j.RthPrimera).Append(',').Append(j.RthUltima).Append(']');
                    }
                sb.Append("]}")
                  .Append(",\"velas\":{\"on\":").Append(_velas ? "true" : "false").Append(",\"paleta\":\"").Append(_paleta).Append("\",\"doji\":\"").Append(_doji).Append("\",\"ultimas\":[");
                for (int b = Math.Max(0, ultima - 9), k = 0; b <= ultima; b++, k++)
                {
                    var c = GetCandle(b); var col = _pintura[b];
                    if (k > 0) sb.Append(',');
                    sb.Append("[").Append(b).Append(",\"").Append(c.Time.ToString("HH:mm", Inv)).Append("\",").Append(F(c.Open)).Append(',').Append(F(c.Close))
                      .Append(",\"").Append(col.HasValue ? "#" + col.Value.R.ToString("X2") + col.Value.G.ToString("X2") + col.Value.B.ToString("X2") : "nativo").Append("\"]");
                }
                sb.Append("]}");
                AuditarDibujo(sb, "col", dCol, _columna, _colVentana.ToString(), _colDato.ToString());
                AuditarDibujo(sb, "per", dPer, _perfil, _perVentana.ToString(), "Volumen");
                // la vela en curso tal como se sumo (para rehacer la cuenta por fuera con el footprint de las cerradas)
                BarraFp viva; int vivaBar; lock (_llave) { viva = _vivo; vivaBar = _vivoBar; }
                sb.Append(",\"viva\":");
                if (viva == null) sb.Append("null");
                else
                {
                    sb.Append("{\"bar\":").Append(vivaBar).Append(",\"hora\":\"").Append(viva.Hora.ToString("yyyy-MM-ddTHH:mm:ss", Inv)).Append("\",\"delta_atas\":")
                      .Append(F(viva.DeltaAtas)).Append(",\"vol_atas\":").Append(F(viva.VolAtas)).Append(",\"tick_min\":").Append(viva.TickMin).Append(",\"niveles\":[");
                    for (int i = 0, k = 0; i < viva.Vol.Length; i++)
                    {
                        if (viva.Vol[i] == 0 && viva.Bid[i] == 0 && viva.Ask[i] == 0) continue;
                        if (k++ > 0) sb.Append(',');
                        sb.Append('[').Append(viva.TickMin + i).Append(',').Append(F(viva.Bid[i])).Append(',').Append(F(viva.Ask[i])).Append(',').Append(F(viva.Vol[i])).Append(']');
                    }
                    sb.Append("]}");
                }
                // control cruzado en las velas cerradas cacheadas: suma de niveles contra lo que ATAS trae en la vela
                int rev = 0, difD = 0, difV = 0, rep = 0; decimal entre = 0;
                lock (_llave)
                    foreach (var b in _fp.Values)
                    {
                        rev++;
                        if (b.SumAsk - b.SumBid != b.DeltaAtas) difD++;
                        if (b.SumVol != b.VolAtas) difV++;
                        rep += b.NivelesRepetidos;
                        entre += b.SumVol - b.SumAsk - b.SumBid;
                    }
                sb.Append(",\"control\":{\"velas\":").Append(rev).Append(",\"delta_distinto\":").Append(difD).Append(",\"volumen_distinto\":").Append(difV)
                  .Append(",\"niveles_repetidos\":").Append(rep).Append(",\"volumen_sin_agresor\":").Append(F(entre)).Append('}');
                sb.Append(",\"rendimiento\":{\"dibujos\":").Append(_nRender).Append(",\"ms_dibujo_prom\":").Append((_nRender > 0 ? _msRender / _nRender : 0).ToString("0.000", Inv))
                  .Append(",\"ms_dibujo_max\":").Append(_msRenderMax.ToString("0.000", Inv)).Append(",\"ticks_vivos\":").Append(_nVivo)
                  .Append(",\"ms_tick_prom\":").Append((_nVivo > 0 ? _msVivo / _nVivo : 0).ToString("0.000", Inv)).Append(",\"ms_tick_max\":").Append(_msVivoMax.ToString("0.000", Inv))
                  .Append(",\"velas_en_cache\":").Append(rev).Append("}}");
                string ruta = Path.Combine(Carpeta(), "auditoria-" + Inst() + "-" + Marco() + "-" + DateTime.UtcNow.ToString("yyyyMMdd", Inv) + ".jsonl");
                string linea = sb.ToString();
                // 1.5: una vela ANTES de la ventana (para comprobar por fuera donde empieza la jornada) y archivo "velas2" con la carga
                string diaArchivo = DateTime.UtcNow.ToString("yyyyMMdd", Inv);
                string velas = VolcadoVelas(Math.Min(dCol?.P?.Desde ?? ultima, dPer?.P?.Desde ?? ultima) - 1, ultima - 1, diaArchivo);
                string rutaV = Path.Combine(Carpeta(), "velas2-" + Inst() + "-" + Marco() + "-" + diaArchivo + ".csv");
                Task.Run(() =>
                {
                    try
                    {
                        lock (_llaveArchivo)
                        {
                            File.AppendAllText(ruta, linea + "\n");
                            if (velas.Length > 0)
                            {
                                bool nuevo = !File.Exists(rutaV);
                                File.AppendAllText(rutaV, (nuevo ? "bar,hora_utc,hora_fin_utc,o,h,l,c,delta_atas,vol_atas,vah_atas,val_atas,poc_atas,tick,bid,ask,vol,carga\n" : "") + velas);
                            }
                        }
                    }
                    catch { }
                });
            }
            catch (Exception e) { Anotar(e); }
        }

        private void AuditarDibujo(StringBuilder sb, string clave, Dibujo d, bool on, string ventana, string dato)
        {
            sb.Append(",\"").Append(clave).Append("\":{\"on\":").Append(on ? "true" : "false").Append(",\"ventana\":\"").Append(ventana).Append("\",\"dato\":\"").Append(dato).Append('"');
            if (d == null || d.P == null) { sb.Append(",\"dibujado\":false}"); return; }
            var p = d.P;
            sb.Append(",\"dibujado\":true,\"nombre\":\"").Append(d.Nombre).Append("\",\"desde\":").Append(p.Desde).Append(",\"hasta\":").Append(p.Hasta)
              .Append(",\"t_desde\":\"").Append(p.HoraDesde.ToString("yyyy-MM-ddTHH:mm:ss", Inv)).Append("\",\"t_hasta\":\"").Append(p.HoraHasta.ToString("yyyy-MM-ddTHH:mm:ss", Inv)).Append('"');
            sb.Append(",\"viva\":");
            if (p.Viva == null) sb.Append("null");
            else
            {
                var v = p.Viva;
                sb.Append("{\"bar\":").Append(v.Bar).Append(",\"hora\":\"").Append(v.Hora.ToString("yyyy-MM-ddTHH:mm:ss", Inv)).Append("\",\"hora_fin\":\"").Append(v.HoraFin.ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv)).Append("\",\"delta_atas\":")
                  .Append(F(v.DeltaAtas)).Append(",\"vol_atas\":").Append(F(v.VolAtas)).Append(",\"niveles\":[");
                for (int i = 0, k = 0; i < v.Vol.Length; i++)
                {
                    if (v.Vol[i] == 0 && v.Bid[i] == 0 && v.Ask[i] == 0) continue;
                    if (k++ > 0) sb.Append(',');
                    sb.Append('[').Append(v.TickMin + i).Append(',').Append(F(v.Bid[i])).Append(',').Append(F(v.Ask[i])).Append(',').Append(F(v.Vol[i])).Append(']');
                }
                sb.Append("]}");
            }
            sb.Append("")
              .Append(",\"t0\":\"").Append(p.T0.ToString("yyyy-MM-ddTHH:mm:ss", Inv)).Append("\",\"t1\":\"").Append(p.T1.ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv))
              .Append("\",\"k\":").Append(p.K).Append(",\"fila_min\":").Append(p.FilaMin).Append(",\"filas\":").Append(p.Filas)
              .Append(",\"velas\":").Append(p.Velas).Append(",\"sin_datos\":").Append(p.VelasSinDatos)
              .Append(",\"sum_bid\":").Append(F(p.SumBid)).Append(",\"sum_ask\":").Append(F(p.SumAsk)).Append(",\"sum_vol\":").Append(F(p.SumVol))
              .Append(",\"sum_delta_atas\":").Append(F(p.SumDeltaAtas)).Append(",\"sum_vol_atas\":").Append(F(p.SumVolAtas))
              .Append(",\"rect\":[").Append(d.R.X).Append(',').Append(d.R.Y).Append(',').Append(d.R.Width).Append(',').Append(d.R.Height).Append(']')
              .Append(",\"letra\":").Append(d.Letra).Append(",\"numeros\":").Append(d.Numeros ? "true" : "false").Append(",\"live\":").Append(d.Live ? "true" : "false").Append(",\"cuando\":\"").Append(d.Cuando).Append('"');
            if (p.Poc >= 0)
                sb.Append(",\"poc\":").Append(p.Poc).Append(",\"vah\":").Append(p.Vah).Append(",\"val\":").Append(p.Val).Append(",\"vol_av\":").Append(F(p.VolAv))
                  .Append(",\"pct\":").Append(p.Pct).Append(",\"paso\":").Append(p.Paso)
                  .Append(",\"y_poc\":").Append(d.YPoc).Append(",\"y_vah\":").Append(d.YVah).Append(",\"y_val\":").Append(d.YVal);
            sb.Append(",\"filas_datos\":[");
            for (int i = 0, k = 0; i < p.Filas; i++)
            {
                if (p.Vol[i] == 0 && p.Bid[i] == 0 && p.Ask[i] == 0) continue;
                if (k++ > 0) sb.Append(',');
                sb.Append('[').Append(i).Append(',').Append(F(p.Bid[i])).Append(',').Append(F(p.Ask[i])).Append(',').Append(F(p.Vol[i])).Append(']');
            }
            sb.Append("],\"dibujadas\":[");
            for (int k = 0; k < d.Filas.Count; k++)
            {
                var f = d.Filas[k];
                if (k > 0) sb.Append(',');
                sb.Append('[').Append(f.Fila).Append(',').Append(f.Y0).Append(',').Append(f.Y1).Append(",\"").Append(f.Texto).Append("\"]");
            }
            sb.Append("]}");
        }

        /// <summary>Footprint de las velas cerradas [desde, hasta] que todavia no se escribieron en el archivo de hoy en esta carga (por
        /// hora de apertura, no por numero de vela: si ATAS recarga la historia los numeros cambian). Una fila por nivel; la ultima
        /// columna es la carga (en el replay las mismas horas vuelven a llegar con otra carga y se escriben de nuevo).</summary>
        private string VolcadoVelas(int desde, int hasta, string diaArchivo)
        {
            var sb = new StringBuilder();
            desde = Math.Max(0, desde);
            if (hasta < desde) return "";
            lock (_llaveArchivo)
            {
                if (_velasVolcadasDia != diaArchivo) { _velasVolcadas.Clear(); _velasVolcadasDia = diaArchivo; }
            }
            string cola = "," + _carga + "\n";
            for (int b = desde; b <= hasta; b++)
            {
                IndicatorCandle c; try { c = GetCandle(b); } catch { continue; }
                if (c == null) continue;
                lock (_llaveArchivo) { if (_velasVolcadas.Contains(c.Time)) continue; }
                var x = Cerrada(b); if (x == null) continue;
                decimal vah = 0, val = 0, poc = 0;
                try { var va = c.ValueArea; if (va != null) { vah = va.ValueAreaHigh; val = va.ValueAreaLow; } } catch { }
                try { var m = c.MaxVolumePriceInfo; if (m != null) poc = m.Price; } catch { }
                string cab = b + "," + c.Time.ToString("yyyy-MM-ddTHH:mm:ss", Inv) + "," + c.LastTime.ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv) + ","
                           + F(c.Open) + "," + F(c.High) + "," + F(c.Low) + "," + F(c.Close) + "," + F(c.Delta) + "," + F(c.Volume) + ","
                           + F(vah) + "," + F(val) + "," + F(poc) + ",";
                bool alguno = false;
                for (int i = 0; i < x.Vol.Length; i++)
                {
                    if (x.Vol[i] == 0 && x.Bid[i] == 0 && x.Ask[i] == 0) continue;
                    sb.Append(cab).Append(x.TickMin + i).Append(',').Append(F(x.Bid[i])).Append(',').Append(F(x.Ask[i])).Append(',').Append(F(x.Vol[i])).Append(cola);
                    alguno = true;
                }
                if (!alguno) sb.Append(cab).Append(",,,").Append(cola);   // tick, bid, ask y vol vacios + la carga
                lock (_llaveArchivo) _velasVolcadas.Add(c.Time);
            }
            return sb.ToString();
        }
    }
}
