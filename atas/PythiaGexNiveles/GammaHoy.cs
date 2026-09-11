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
    /// contrastarlo al lado de Gamma Vivo y de la referencia.
    ///
    /// LO QUE APRENDIMOS DE LOS 30 VIDEOS Y DEL LABORATORIO, Y QUE ACA MANDA:
    ///
    ///   1. El mapa del dia es el de VOLUMEN. La referencia tiene dos libros en su
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
                 Description = "CBOE_SPX: la cadena de SPX de la nube (llega 902 s tarde, cada minuto en la rueda). Rithmic_ES: las opciones de ES desde tu ATAS, volumen del dia por strike EN TIEMPO REAL e IV de las puntas, sin retraso y sin nube; strikes del futuro, sin base. Con Rithmic el mapa respira con cada operacion, como la referencia. El pasado (archivo) sigue siendo SPX.")]
        public LibroEnVivo Libro { get; set; } = LibroEnVivo.CBOE_SPX;

        [Display(Name = "Rithmic: rearmar el libro cada (s)", GroupName = "1. Datos", Order = 14)]
        [Range(5, 120)]
        public int SegundosLibroRithmic { get; set; } = 10;

        [Display(Name = "Feed por minuto (rama cadenas)", GroupName = "1. Datos", Order = 13,
                 Description = "Ademas del feed de la nube (cada 5 min) baja ultima-<raiz>.json de la rama cadenas, que se escribe cada minuto en la rueda americana, y usa la mas nueva.")]
        public bool FeedMinuto { get; set; } = true;

        public enum EstiloRayas { Ninguna, Tenues, Normales }

        [Display(Name = "Rayas de los niveles", GroupName = "3. Pantalla", Order = 11,
                 Description = "La referencia no cruza el grafico con rayas: los niveles viven en los guiones por vela y en el eje. Tenues = al 30 %.")]
        public EstiloRayas Rayas { get; set; } = EstiloRayas.Tenues;

        [Display(Name = "Guion de dominante por vela: grosor (px)", GroupName = "3. Pantalla", Order = 12)]
        [Range(1, 8)]
        public int GrosorGuion { get; set; } = 3;

        [Display(Name = "Semillas del Max Change por vela (30, 5 y 1 min)", GroupName = "3. Pantalla", Order = 13,
                 Description = "Tres puntos naranjas por vela con el strike de mayor cambio de GEX a 30, 5 y 1 min. Alineados varios minutos = ahi suele nacer la proxima dominante (La referencia: 'la semillita').")]
        public bool VerSemillas { get; set; } = true;

        [Display(Name = "Zero gamma por vela (puntitos)", GroupName = "3. Pantalla", Order = 14)]
        public bool VerZeroPorVela { get; set; } = true;

        public enum MouseVela { Cabecera, Todo, Nunca }

        [Display(Name = "Mouse sobre una vela del pasado", GroupName = "3. Pantalla", Order = 17,
                 Description = "Cabecera: las bandas, las rayas y las barras quedan quietas con el vivo; al pasar el mouse por una vela solo aparece un renglon en la cabecera con lo que regia en esa vela (dominantes, zero, precio). Todo: la escalera, las rayas y las bandas saltan a esa vela (asi funciona el rebobinado). Nunca: el mouse no hace nada. Con Fuente = Archivo, Cabecera se comporta como Todo.")]
        public MouseVela MouseSobreVela { get; set; } = MouseVela.Cabecera;

        public enum GatillosEnPantalla { RechazoTren, Todos, Ninguno }

        [Display(Name = "Gatillos de order flow en la banda (EXPERIMENTAL)", GroupName = "3. Pantalla", Order = 18,
                 Description = "Triangulo en la vela donde, recien entrado el precio en la banda de una dominante QUIETA, el order flow dio un gatillo. RechazoTren: solo 'tres deltas seguidos en contra de la llegada' (la unica pista del laboratorio del 08-09: 27 casos, 70 % contra 37 % del placebo en ES; POCOS casos, no es una prueba). Todos: tambien rechazo por delta fuerte, divergencia y ruptura con delta (esta salio al reves: se dibuja gris). Cada disparo se registra en pythiagex-gatillos-<inst>.jsonl para que laboratorio/gatillos.py lo juzgue contra placebo.")]
        public GatillosEnPantalla VerGatillos { get; set; } = GatillosEnPantalla.RechazoTren;

        public enum GatilloModeloModo { SoloTarde, TodoElDia, Ninguno }

        [Display(Name = "Gatillo MODELO (ES, 1 min): regresion del laboratorio", GroupName = "3. Pantalla", Order = 24,
                 Description = "Regresion logistica ajustada el 10-09 sobre MES: 10 rasgos (momentum corto, delta, distancias al zero, majors, dominantes y Max Change) -> probabilidad de tocar +3 antes que -3 en 10 min. En velas de 1 min (13 dias): fuera de muestra 61 % con p >= 0,70, en la tarde de Nueva York (14-16 h) 83 % de 59 casos, 7 por dia. En velas de 2 min (16 dias): 62 % con p >= 0,70, tarde 77 % de 30, 3 por dia (mas debil). Rombo verde = largo, rojo = corto, con la p. SOLO raiz ES, graficos de 1 o 2 minutos. En NQ no hay señal. Objetivo 3 pts. Cada disparo se registra y se juzga con dias nuevos.")]
        public GatilloModeloModo ModoModelo { get; set; } = GatilloModeloModo.SoloTarde;

        [Display(Name = "Gatillo MODELO: umbral de probabilidad", GroupName = "3. Pantalla", Order = 25,
                 Description = "0,70 = 7 disparos por dia en la tarde (83 % medido); 0,65 = el doble de disparos con ~80 %; mas bajo es ruido.")]
        [Range(0.55, 0.95)]
        public decimal UmbralModelo { get; set; } = 0.70m;

        [Display(Name = "Base de la rueda: retraso del spot de CBOE (segundos)", GroupName = "2. Datos", Order = 29,
                 Description = "Para medir la base (futuro - indice) se alinea la vela de Rithmic con el spot de la cadena de CBOE, que llega tarde. Medido el 10-09-2026: 960 s alinea mejor (dispersion 2,9 pts) que 902 (6,6). Una muestra por cada cadena nueva de CBOE, sin los primeros 20 min de la rueda, mediana robusta; cada muestra queda en el log ('base muestra').")]
        [Range(0, 3600)]
        public int RetrasoCboeSeg { get; set; } = 960;

        public enum GatilloReboteModo { SoloTendencia, Todos, PrimerToqueActual, Ninguno }

        [Display(Name = "Gatillo REBOTE en las rayas (dominantes del dia, zero, majors)", GroupName = "3. Pantalla", Order = 26,
                 Description = "Triangulo hueco en la vela que toca una raya (una dominante que hubo hoy, el zero o un major), viene de mas lejos, cierra del lado bueno y es la primera que aguanta: la entrada que el operador toma a ojo (los 7 ejemplos del 10-09 en MNQ disparan todos con Todos, 5 con SoloTendencia). SoloTendencia: largo solo con el precio sobre el zero, corto solo debajo (51 disparos por dia en MNQ 1 min). Todos: los dos lados (102 por dia). PrimerToqueActual: solo el primer toque de la dominante actual a favor del zero (3,2 por dia). MEDIDO en 16 dias de MNQ contra las mismas rayas corridas +-85/+-145 pts (acierto = +20 antes que -20 en 20 min desde el cierre): SoloTendencia 44 % contra 42 % del placebo; Todos 45 % contra 46 %; PrimerToqueActual 50 % contra 42 % con 52 casos (con +10/-10, 60 % contra 39 %), pero en MES 36 % contra 40 %: hipotesis, no señal validada. En 2 y 5 min no mejora. Lo que gano en los ejemplos fue el dia alcista (comprar cada retroceso), no la raya. Cada disparo se registra en pythiagex-gatillos-<inst>.jsonl y laboratorio/gatillo_rebote.py lo juzga con dias nuevos.")]
        public GatilloReboteModo ModoRebote { get; set; } = GatilloReboteModo.SoloTendencia;

        [Display(Name = "Gatillo REBOTE: enfriamiento por nivel (velas)", GroupName = "3. Pantalla", Order = 27,
                 Description = "Despues de un disparo en un nivel y lado, no vuelve a disparar ahi durante estas velas. 5 = como se midio; mas alto = menos triangulos.")]
        [Range(1, 60)]
        public int ReboteEnfriamiento { get; set; } = 5;

        [Display(Name = "Gatillo REBOTE: tamaño del circulo (px)", GroupName = "3. Pantalla", Order = 28,
                 Description = "Circulo hueco en el punto exacto (la raya que toco, en la vela del disparo); la letra R va por fuera de la vela. 4 = chico; mas grande si no se ve.")]
        [Range(2, 12)]
        public int TamanoRebote { get; set; } = 4;

        [Display(Name = "Gatillos: dominante quieta (% del precio en 5 velas)", GroupName = "3. Pantalla", Order = 19,
                 Description = "El toque cuenta solo si la dominante se movio menos que esto en las 5 velas previas (0,026 % = 2 pts en ES, ~8 en NQ): el precio fue a la banda, no la banda al precio.")]
        public decimal GatilloQuietaPct { get; set; } = 0.026m;

        [Display(Name = "Gatillos: solo en la rueda americana (13:30-20:00 UTC)", GroupName = "3. Pantalla", Order = 20,
                 Description = "Donde el laboratorio vio la pista. De noche la cadena de CBOE esta congelada y el order flow es fino: la misma regla dio 60 % contra 50 % del placebo (nada) frente a 70 % contra 37 % en la rueda.")]
        public bool GatillosSoloRueda { get; set; } = true;

        [Display(Name = "Dominantes nuevas: resaltar los guiones de los ultimos (min)", GroupName = "3. Pantalla", Order = 21,
                 Description = "Los guiones de dominante que se dibujaron en vivo hace menos de estos minutos salen en LILA fluo, mas grandes y con borde: ahi esta el tren de dominantes de este instante. Con los minutos se van fundiendo solos al amarillo normal. 0 = sin resaltar.")]
        [Range(0, 120)]
        public int EnfasisNuevasMin { get; set; } = 10;

        [Display(Name = "Barras pesadas cercanas: cuantas por lado (raya punteada)", GroupName = "3. Pantalla", Order = 22,
                 Description = "Las N barras del perfil con mas GEX (del libro que dibuja) arriba y abajo del precio, dentro del radio, con una raya punteada tenue y su GEX; 0DTE pesa 1,5x. No repite las que ya son dominante o major. 0 = sin rayas.")]
        [Range(0, 6)]
        public int RayasPesadas { get; set; } = 2;

        [Display(Name = "Barras pesadas cercanas: radio (% del precio)", GroupName = "3. Pantalla", Order = 23,
                 Description = "0,6 % = ~46 pts en ES, ~180 en NQ.")]
        public decimal RadioPesadasPct { get; set; } = 0.6m;

        [Display(Name = "Print grande: contratos por operacion acumulada", GroupName = "4. Auditoria", Order = 3,
                 Description = "Se registran por vela en el centinela (of: big_n, big_max, big_buy, big_sell) para medir despues si las 'ballenas' en la banda sirven. No se dibuja nada.")]
        [Range(1, 100000)]
        public int UmbralPrintGrande { get; set; } = 50;

        [Display(Name = "Zona de dominancia: banda alrededor de cada dominante (% del precio)", GroupName = "3. Pantalla", Order = 16,
                 Description = "Franja desde la dominante HACIA ADENTRO (hacia el lado del precio), con el borde interno marcado. 0,08 % = ~24 puntos en NQ, ~6 en ES. 0 = sin banda. La banda es DIBUJO: si el precio la respeta o no lo mide laboratorio/canal.py contra placebo.")]
        public decimal BandaDominantesPct { get; set; } = 0.08m;

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

        [Display(Name = "Dividendo del indice (para acotar la base; 0 = 1,2 % SPX / 0,8 % NDX)", GroupName = "1. Datos", Order = 15,
                 Description = "La base (futuro menos indice) se compara con el carry teorico del contrato del grafico: precio x (tasa - dividendo) x dias al vencimiento / 365. Una base que se aleja mas del 60 % del carry (o de 0,06 % del precio) se descarta y se usa la siguiente: medida, medida reciente, de la rueda, cruda, teorica. Medido el 09-09: la cruda de la nube salto a 322 pts al rolar su cotizacion a diciembre.")]
        public decimal Dividendo { get; set; } = 0m;

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
                 Description = "Hoy: solo el 0DTE (si no hay, el mas cercano). Semana: hasta 7 dias. Todo: la cadena entera. La referencia: 'nosotros trabajamos en 0DTE'.")]
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

        [Display(Name = "Canal: una dominante por lado (la mas fuerte arriba y la mas fuerte abajo)", GroupName = "2. Lectura", Order = 6,
                 Description = "Apagado: las N barras mas fuertes sin mirar el lado (pueden caer las dos del mismo lado).")]
        public bool UnaPorLado { get; set; } = true;

        [Display(Name = "Dominantes: empate tecnico, gana la mas cercana al precio (%)", GroupName = "2. Lectura", Order = 8,
                 Description = "Si dos barras del mismo lado del precio estan dentro de este porcentaje de la mas grande, la dominante es la MAS CERCANA al precio, no la mas grande. Medido de noche con Rithmic: 29.049 (-84 M) contra 28.800 (-91 M) saltaban por 7 M. 0 = siempre la mas grande.")]
        [Range(0, 90)]
        public int EmpateDominantesPct { get; set; } = 20;

        [Display(Name = "Dominante como centroide (ondula, como la referencia)", GroupName = "2. Lectura", Order = 7,
                 Description = "Promedio de precio ponderado por gamma alrededor del strike ganador. Medido en los videos: la dominante de la referencia es una banda de ~5 puntos que ondula, no una raya plana en un strike.")]
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
                 Description = "La referencia usa ~180 en QQQ. Las opciones de ES son menos liquidas: 50 de arranque, se calibra midiendo.")]
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
        // UN GUION POR CADA ACTUALIZACION, no uno por vela: medido en la referencia hasta
        // 4-6 guiones por columna en las velas recientes. Cada vez que se reprecia y
        // la dominante se movio mas de un cuarto de punto, se agrega un guion a la vela.
        // Cada guion lleva la hora en que nacio: los del vivo recientes se resaltan (1.4).
        private readonly Dictionary<int, List<(double Fut, int Rango, DateTime Hora)>> _guiones = new();
        private void AgregarGuiones(int bar, double[] est, DateTime hora)
        {
            if (est == null) return;
            if (!_guiones.TryGetValue(bar, out var lg)) { lg = new List<(double, int, DateTime)>(); _guiones[bar] = lg; }
            for (int i = 0; i < est.Length; i++)
            {
                if (double.IsNaN(est[i]) || est[i] <= 0) continue;
                bool hay = false;
                for (int k = lg.Count - 1; k >= 0; k--) if (lg[k].Rango == i) { hay = Math.Abs(lg[k].Fut - est[i]) < 0.25; break; }
                if (!hay && lg.Count < 24) lg.Add((est[i], i, hora));
            }
        }
        // por vela: zero por volumen y el strike del Max Change a 30, 5 y 1 min (semillas)
        private readonly Dictionary<int, (double Zero, double[] Mc)> _marcas = new();
        // gatillos de order flow en la banda (GatilloBanda): por vela, para dibujar y registrar
        private readonly GatilloBanda _gatVivo = new();
        private readonly GatilloModelo _modVivo = new();
        private readonly GatilloRebote _rebVivo = new();
        private bool _rebSembrado;
        private bool _modAvisado; private double _modUltimaP = double.NaN;
        // la base de la rueda, medida por el indicador (1.5): futuro del grafico a la hora real
        // del spot de la cadena (902 s de retraso de CBOE) menos ese spot; mediana de 30
        private readonly List<double> _baseObs = new();
        private double _baseRueda = double.NaN; private DateTime _baseRuedaUtc = DateTime.MinValue, _baseObsUltimaCadena = DateTime.MinValue;
        private bool _baseCargada; private string _baseOrigenUlt = "";
        private DateTime _ultimoLogPelotitas = DateTime.MinValue;

        /// <summary>Auditoria de las pelotitas: para cada dominante, el GEX de su strike ahora y
        /// en las fotos de hace 1, 5 y 15 min, y el veredicto que se deberia ver en pantalla.</summary>
        private void LogPelotitas(GammaHoyNucleo.Lectura L)
        {
            var fotos = _nucleo.FotosCopia();
            if (fotos.Count == 0 || L.Perfil == null) return;
            long ahora = L.Hora.Ticks / TimeSpan.TicksPerMinute;
            var iv = CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder("PELOTITAS (fotos " + fotos.Count + ", desde hace " + (ahora - fotos[0].Minuto) + " min):");
            foreach (var dm in L.Doms.Take(2))
            {
                var s = L.Perfil.OrderBy(x => Math.Abs(x.Fut - dm.Fut)).FirstOrDefault();
                if (s == null) continue;
                sb.Append(" K" + s.K.ToString("0", iv) + " ahora " + (s.GexVol / 1e6).ToString("+0;-0", iv) + "M");
                int adentro = 0, afuera = 0;
                foreach (int n in new[] { 1, 5, 15 })
                {
                    var f = fotos.Where(z => z.Minuto <= ahora - n).LastOrDefault();
                    if (f == null || !f.GexVol.TryGetValue(s.K, out var g)) { sb.Append(" hace" + n + " --"); continue; }
                    sb.Append(" hace" + n + " " + (g / 1e6).ToString("+0;-0", iv) + "M");
                    if (Math.Abs(g) < Math.Abs(s.GexVol)) adentro++; else if (Math.Abs(g) > Math.Abs(s.GexVol)) afuera++;
                }
                sb.Append(adentro == 3 ? " => CRECE (las 3 adentro)" : afuera == 3 ? " => DECRECE (las 3 afuera)" : adentro + afuera == 0 ? " => QUIETO (en la punta)" : " => MEZCLADO");
                sb.Append(" |");
            }
            Log(sb.ToString());
        }
        private readonly Dictionary<int, List<GatilloBanda.Marca>> _disparos = new();
        private int _barraGat = -1, _barraBig = -1, _bigVistos;
        private readonly object _bigLlave = new();
        private double _bigMax, _bigBuy, _bigSell; private int _bigN;          // prints grandes de la vela en curso
        private (int N, double Max, double Buy, double Sell) _bigCerrada;       // ... y de la ultima cerrada
        private CumulativeTrade _bigActual;
        // centinela
        private Centinela _cent, _centES;      // vivo por libro: CBOE (SPX) y Rithmic (ES), aparte
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
        private DateTime _ultimoReprecio = DateTime.MinValue; private int _barraRepreciada = -1;
        /// <summary>Las fotos de las velas viejas se quedan con los niveles pero sueltan el perfil (150-211
        /// strikes cada una): con 19.000 velas de 1 min por grafico y cuatro graficos eran ~1 GB retenidos
        /// (medido el 10-09: ATAS en 7,6 GB privados con la PC en 16 GB y 2 MB libres). El mouse sobre una
        /// vela y las pelotitas del pasado usan a lo sumo las ultimas FOTOS_CON_PERFIL velas.</summary>
        private const int FOTOS_CON_PERFIL = 2500;
        private void PodarFotos(int barraActual)
        {
            lock (_candado)
                foreach (var kv in _fotosBarra)
                    if (kv.Key < barraActual - FOTOS_CON_PERFIL && kv.Value.Perfil != null) kv.Value.Perfil = null;
        }
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
        private static readonly Color ColDom = Color.FromArgb(232, 200, 60);    // primaria: amarillo (hue 29 medido en la referencia)
        /// <summary>Mezcla lineal de dos colores: t = 0 da a, t = 1 da b.</summary>
        private static Color Mezclar(Color a, Color b, double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            return Color.FromArgb(255, (int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));
        }
        private static readonly Color ColDom2 = Color.FromArgb(232, 168, 56);   // secundaria: naranja (hue 19 medido)
        private static readonly Color ColNuevo = Color.FromArgb(255, 205, 120, 255);  // guion recien nacido en vivo: LILA fluo (pedido 2026-09-11), se funde al amarillo con los minutos
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
                RearmarVivaSiHaceFalta(ahora);
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
                    RearmarVivaSiHaceFalta(ahora);
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
                Log("Gamma Hoy 1.8j arranca en REBOBINADO. raiz=" + Raiz() + " horizonte=" + Horizonte + " carpeta=" + Feed.Archivo.Carpeta);
                return;
            }
            SubscribeToTimer(_periodo, _tick);
            _ = Reloj.Medir(Log);
            _ultimaBajada = DateTime.UtcNow;
            _ultimoIntentoViva = DateTime.UtcNow;
            _ = BajarFeed();
            if (UsarCadenaViva) ArrancarViva();
            Log("Gamma Hoy 1.8j arranca" + (Fuente == FuenteDatos.Hibrido ? " en HIBRIDO (archivo + vivo)" : " en VIVO (con el pasado del archivo)") + ". raiz=" + Raiz() + " horizonte=" + Horizonte);
        }

        protected override void OnDispose()
        {
            try { if (_tick != null) UnsubscribeFromTimer(_periodo, _tick); } catch { }
            try { _cent?.Volcar(true); } catch { }
            try { _centES?.Volcar(true); } catch { }
            try { _centArchivo?.Volcar(true); } catch { }
            try { _viva.Dispose(); } catch { }
        }

        /// <summary>Apagada: se reintenta cada 3 min. Activa pero sin el vencimiento mas cercano (el 11-09
        /// Rithmic no contesto el 0DTE de NQ y el libro quedo con lunes/martes todo el dia): cada 5 min,
        /// para recuperarlo sin martillar el feed.</summary>
        private void RearmarVivaSiHaceFalta(DateTime ahora)
        {
            if (!UsarCadenaViva || _vivaCorriendo) return;
            double seg = (ahora - _ultimoIntentoViva).TotalSeconds;
            if ((!_viva.Activa && seg >= 180) || (_viva.Activa && _viva.FaltaCercano && seg >= 300)) { _ultimoIntentoViva = ahora; ArrancarViva(); }
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
            // OnCalculate llega en CADA tick de la ultima vela y Repreciar rehace todo el perfil (211 strikes,
            // cruce de 61 pasos por libro): con cuatro graficos era un nucleo entero de CPU de corrido (medido
            // 10-09 21:55: 19-21 s de CPU por cada 20 s). Alcanza con repreciar una vez por segundo, y siempre
            // en el primer tick de una vela nueva.
            var ahoraRep = DateTime.UtcNow;
            if (bar == _barraRepreciada && (ahoraRep - _ultimoReprecio).TotalMilliseconds < 1000) return;
            _barraRepreciada = bar; _ultimoReprecio = ahoraRep;
            try { Repreciar(); } catch (Exception e) { Registrar(e); }
            if (bar != _barraBig) { CerrarBig(); _barraBig = bar; }
            try { Anotar(bar); } catch (Exception e) { Registrar(e); }
            try { GatillosVivo(bar); } catch (Exception e) { Registrar(e); }
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
            if (bar % 300 == 0) PodarFotos(bar);
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
                (double)c.Open, (double)c.High, (double)c.Low, (double)c.Close, (double)c.Volume, (double)c.Ticks, (double)c.Delta, L.S, GammaHoyNucleo.Niveles(L), ExtraOf(c, null));
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
                    if (Libro == LibroEnVivo.Rithmic_ES)
                    {
                        // el mismo libro que el vivo: la grabacion de Rithmic de las horas en que
                        // ATAS estuvo abierto; CBOE solo en los dias sin grabacion
                        var viva = Feed.Archivo.CargarViva(raiz, desde, hasta, Log);
                        if (viva.Count >= 30)
                        {
                            var diasViva = new HashSet<DateTime>(viva.Select(v => v.GeneradoUtc.Date));
                            var mezcla = ls.Where(x => !diasViva.Contains(x.GeneradoUtc.Date)).ToList();
                            mezcla.AddRange(viva);
                            mezcla.Sort((x, y) => x.GeneradoUtc.CompareTo(y.GeneradoUtc));
                            Log("archivo con libro Rithmic: " + viva.Count + " cadenas grabadas en " + diasViva.Count + " dias + " + (mezcla.Count - viva.Count) + " de CBOE en los demas");
                            ls = mezcla;
                        }
                        else Log("archivo con libro Rithmic: grabacion insuficiente (" + viva.Count + "), el pasado queda con CBOE");
                    }
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
            a.Centroide = DominanteCentroide; a.RadioCentroidePts = (double)RadioCentroidePts; a.UnaPorLado = UnaPorLado; a.EmpatePct = EmpateDominantesPct;
            a.ExpiracionFuturoUtc = ExpiracionFuturo(); a.ExpiracionFuturoAltUtc = _expAlt; a.Dividendo = DividendoUsado();
            double edadMax = (double)Math.Max(0.05m, ArchivoEdadMaxHoras);
            int fin = Math.Max(0, CurrentBar - 1);      // la ultima vela es del vivo (Hibrido) o se muestra con la ultima foto (Archivo)
            int i = 0, con = 0, sin = 0;
            DateTime ultimaCad = DateTime.MinValue, ultLog = DateTime.UtcNow;
            // los gatillos de order flow sobre el pasado: la misma logica que en vivo, vela a
            // vela y en orden, con las dominantes que regian en cada una (sin cadena: sin banda)
            var gat = new GatilloBanda(); ConfigurarGatillo(gat);
            var modArch = new GatilloModelo();
            var rebArch = new GatilloRebote(); ConfigurarRebote(rebArch);
            var tirosArch = new List<GatilloBanda.Marca>();
            lock (_candado) _disparos.Clear();
            void Gat(int barG, IndicatorCandle cg, DateTime horaG, List<(double Fut, double Gex)> domsG)
            {
                var ts = gat.Procesar(barG, horaG, (double)cg.Open, (double)cg.High, (double)cg.Low, (double)cg.Close, (double)cg.Delta, domsG);
                if (ts.Count == 0) return;
                lock (_candado) _disparos[barG] = ts;
                tirosArch.AddRange(ts);
            }
            // el gatillo REBOTE sobre el pasado: todas las velas en orden (las sin cadena solo
            // alimentan la ventana), con las filas de dominantes de la rueda acumuladas
            void Reb(int barR, IndicatorCandle cr, DateTime horaR, List<(double Fut, double Gex)> domsR, double zeroR, double mpR, double mnR)
            {
                var tr = rebArch.Procesar(barR, horaR, (double)cr.Open, (double)cr.High, (double)cr.Low, (double)cr.Close, (double)cr.Delta, domsR, zeroR, mpR, mnR);
                if (tr.Count == 0) return;
                lock (_candado) { if (!_disparos.TryGetValue(barR, out var ltr)) _disparos[barR] = ltr = new List<GatilloBanda.Marca>(); ltr.AddRange(tr); }
                tirosArch.AddRange(tr);
            }
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
                    Gat(bar, c, cierra, null);
                    try { Reb(bar, c, cierra, null, double.NaN, double.NaN, double.NaN); } catch (Exception e) { Registrar(e); }
                    continue;
                }
                // un guion por cada cadena que llego DURANTE la vela (varias por vela, como
                // la referencia); la ultima es la que queda como foto y centinela de la vela
                GammaHoyNucleo.Lectura L = null;
                for (int j = Math.Max(i0, i - 12); j <= i; j++)
                {
                    var cj = arch[j];
                    if (j < i && cj.GeneradoUtc <= abre) continue;
                    var Lj = nuc.Calcular(cj, (double)c.Close, j == i ? cierra : cj.GeneradoUtc);
                    if (Lj == null || Lj.SinBase) continue;
                    lock (_candado) AgregarGuiones(bar, Lj.Estela, DateTime.MinValue);   // del archivo: nunca "nuevo"
                    L = Lj;
                }
                if (L == null) { sin++; Gat(bar, c, cierra, null); try { Reb(bar, c, cierra, null, double.NaN, double.NaN, double.NaN); } catch (Exception e) { Registrar(e); } continue; }
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
                try { Gat(bar, c, cierra, L.Doms); } catch (Exception e) { Registrar(e); }
                try
                {
                    var tm = GatilloModeloVela(modArch, bar, c, cierra, L.Doms, L.ZeroVol, L.MpVol, L.MnVol, L.MaxChange);
                    if (tm != null) { lock (_candado) { if (!_disparos.TryGetValue(bar, out var lt0)) _disparos[bar] = lt0 = new List<GatilloBanda.Marca>(); lt0.Add(tm); } tirosArch.Add(tm); }
                }
                catch (Exception e) { Registrar(e); }
                try { Reb(bar, c, cierra, L.Doms, L.ZeroVol, L.MpVol, L.MnVol); } catch (Exception e) { Registrar(e); }
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
            // las fotos del Max Change del vivo se siembran con los perfiles de las ultimas velas
            // del archivo: las pelotitas y el Δ de la escalera existen desde el primer minuto
            if (Fuente != FuenteDatos.Archivo)
            {
                try
                {
                    var semillas = new List<(long, Dictionary<double, double>, Dictionary<double, double>)>();
                    lock (_candado)
                        foreach (var kv in _fotosBarra.OrderByDescending(k => k.Key).Take(35))
                        {
                            if (kv.Value.Perfil == null) continue;
                            var dct = new Dictionary<double, double>(); var dcv = new Dictionary<double, double>();
                            foreach (var st in kv.Value.Perfil) { dct[st.K] = st.GexVol; dcv[st.K] = st.Conv; }
                            semillas.Add((Utc(kv.Value.Vela).Ticks / TimeSpan.TicksPerMinute, dct, dcv));
                        }
                    _nucleo.SembrarFotos(semillas);
                    Log("fotos del Max Change sembradas con " + semillas.Count + " velas del archivo");
                }
                catch (Exception e) { Registrar(e); }
            }
            var es = CultureInfo.GetCultureInfo("es-AR");
            _rebRotulo = (Fuente != FuenteDatos.Archivo ? "archivo " : "REBOBINADO  ") + con.ToString("N0", es) + " velas con cadena, " + sin.ToString("N0", es) + " sin";
            Log("REBOBINADO termino: " + con + " velas con cadena, " + sin + " sin; ultima cadena " + (ultimaCad == DateTime.MinValue ? "--" : ultimaCad.ToString("yyyy-MM-dd HH:mm") + " UTC"));
            Log("gatillos en el archivo: " + gat.Entradas + " entradas a banda quieta, " + gat.Disparos + " disparos (" + tirosArch.Count(t => t.Principal && t.Tipo == "rechazo·tren") + " rechazo·tren, " + tirosArch.Count(t => t.Tipo == "modelo·es10") + " modelo, " + tirosArch.Count(t => t.Tipo.StartsWith("rebote")) + " rebote de " + rebArch.Candidatos + " toques)");
            RegistrarGatillos(tirosArch, "archivo", true);
            _rebSembrado = false;      // el vivo se vuelve a sembrar con los guiones y los disparos de este recorrido
            PodarFotos(fin);
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
                RecibidoUtc = ahora, GeneradoUtc = ahora, EsFuturo = true, Fuente = "Rithmic " + Raiz(),
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
                // fuera del grafico o arrastrandolo: nada (si no, quedaba pegada la ultima vela)
                if (m.IsMouseLeave || m.IsMovingChartUsingMouse) return -1;
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
            try { MedirBaseRueda(_c); } catch (Exception e) { Registrar(e); }
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
            a.Centroide = DominanteCentroide; a.RadioCentroidePts = (double)RadioCentroidePts; a.UnaPorLado = UnaPorLado; a.EmpatePct = EmpateDominantesPct;

            var L = _nucleo.Calcular(c, futuro, ahoraUtc);
            if (L == null) return;
            if (L.SinBase) { lock (_candado) { _baseOrigen = "sin base"; } return; }
            if (L.TransicionNueva) Log("TRANSICION " + L.Alerta);

            lock (_candado)
            {
                _perfil = L.Perfil; _S = L.S; _futuro = L.Futuro; _base = L.Base; _baseOrigen = L.BaseOrigen; _masCercaUlt = L.MasCerca;
                if (L.BaseOrigen != _baseOrigenUlt) { _baseOrigenUlt = L.BaseOrigen; Log("base: " + L.Base.ToString("0.00", CultureInfo.InvariantCulture) + " (" + L.BaseOrigen + ")" + (double.IsNaN(L.Carry) ? "" : " carry teorico " + L.Carry.ToString("0.0", CultureInfo.InvariantCulture))); }
                if ((ahoraUtc - _ultimoLogPelotitas).TotalMinutes >= 5) { _ultimoLogPelotitas = ahoraUtc; try { LogPelotitas(L); } catch { } }
                _zeroVol = L.ZeroVol; _zeroOi = L.ZeroOi; _netVol = L.NetVol; _netOi = L.NetOi;
                _mpVol = L.MpVol; _mnVol = L.MnVol; _mpOi = L.MpOi; _mnOi = L.MnOi;
                _maxAbsVol = L.MaxAbsVol; _maxAbsOi = L.MaxAbsOi; _maxAbsConv = L.MaxAbsConv;
                _doms = L.Doms; _libroConvUsado = L.LibroConv; _libroDomUsado = L.LibroDom;
                _cuadrante = L.Cuadrante; _cuadranteCorto = L.CuadranteCorto; _cuadranteN = L.CuadranteN;
                _picoFut = L.PicoFut; _picoGex = L.PicoGex; _convEnPrecio = L.ConvEnPrecio; _mucho = L.Mucho;
                _alerta = L.Alerta; _alertaHasta = L.AlertaHasta;
                _maxChange = L.MaxChange;
                _estela[barra] = L.Estela;
                AgregarGuiones(barra, L.Estela, DateTime.UtcNow);
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
            // el centinela del vivo, por libro: los niveles del libro de ES (Rithmic) no se
            // mezclan con los del libro de SPX (CBOE); el laboratorio los juzga aparte
            bool esFut = _c != null && _c.EsFuturo;
            var instrC = InstrumentInfo != null ? InstrumentInfo.Instrument : "x";
            var marcoC = ChartInfo != null && ChartInfo.ChartType != null ? ChartInfo.ChartType + "-" + ChartInfo.TimeFrame : "x";
            if (esFut && _centES == null) _centES = new Centinela("hoyrithmic-" + instrC, marcoC);
            if (!esFut && _cent == null) _cent = new Centinela("hoy-" + instrC, marcoC);
            var centUsar = esFut ? _centES : _cent;
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
            (int N, double Max, double Buy, double Sell) big; lock (_bigLlave) big = _bigCerrada;
            centUsar.Anotar(cerrada, c.LastTime != default(DateTime) ? c.LastTime : c.Time,
                (double)c.Open, (double)c.High, (double)c.Low, (double)c.Close,
                (double)c.Volume, (double)c.Ticks, (double)c.Delta, sp, niv, ExtraOf(c, big));
        }

        // ==================================================================
        // La base acotada (1.5): vencimiento del contrato, dividendo y la base de la rueda
        // ==================================================================
        private DateTime ExpiracionFuturo()
        {
            DateTime exp = default(DateTime); string de = "";
            try
            {
                var s = TradingManager?.Security;
                if (s != null && s.Expiration != default(DateTime) && s.Expiration.Year >= 2000)
                {
                    // el contrato vence a la apertura de Nueva York de ese dia (13:30 UTC): alcanza para el carry
                    exp = DateTime.SpecifyKind(s.Expiration.Date, DateTimeKind.Utc).AddHours(13.5); de = "Security " + s.Code;
                }
            }
            catch { }
            // sin Security (pestaña oculta al arrancar, medido el 09-09): del codigo del instrumento,
            // #MESU6 -> tercer viernes de septiembre de 2026 (regla de los futuros de indices del CME)
            if (exp == default(DateTime))
            {
                try { exp = ExpiracionDeCodigo(InstrumentInfo?.Instrument); de = "codigo " + InstrumentInfo?.Instrument; } catch { }
            }
            _expAlt = default(DateTime);
            if (exp == default(DateTime))
            {
                // ni Security ni mes en el codigo (ATAS da la raiz sola, "MES"): se supone el
                // trimestral mas cercano y se acepta tambien el siguiente, por si el grafico ya rolo
                exp = TrimestralDesde(DateTime.UtcNow.AddDays(1)); _expAlt = TrimestralDesde(exp.AddDays(1)); de = "SUPUESTO: trimestral mas cercano";
            }
            if (!_expLogueada) { _expLogueada = true; Log("vencimiento del contrato: " + exp.ToString("yyyy-MM-dd") + " (" + de + ")" + (_expAlt != default(DateTime) ? ", alternativo " + _expAlt.ToString("yyyy-MM-dd") : "")); }
            return exp;
        }
        private bool _expLogueada;
        private DateTime _expAlt;

        /// <summary>El tercer viernes de marzo/junio/septiembre/diciembre que sigue a la fecha dada.</summary>
        private static DateTime TrimestralDesde(DateTime desdeUtc)
        {
            for (int k = 0; k < 8; k++)
            {
                int mes = ((desdeUtc.Month - 1) / 3 + 1 + k) * 3;       // 3, 6, 9, 12, 15...
                int anio = desdeUtc.Year + (mes - 1) / 12; mes = (mes - 1) % 12 + 1;
                var d1 = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
                int haciaViernes = ((int)DayOfWeek.Friday - (int)d1.DayOfWeek + 7) % 7;
                var venc = d1.AddDays(haciaViernes + 14).AddHours(13.5);
                if (venc > desdeUtc) return venc;
            }
            return default(DateTime);
        }

        private static DateTime ExpiracionDeCodigo(string codigo)
        {
            if (string.IsNullOrEmpty(codigo)) return default(DateTime);
            var limpio = System.Text.RegularExpressions.Regex.Replace(codigo.ToUpperInvariant(), "[^A-Z0-9]", "");
            var m = System.Text.RegularExpressions.Regex.Match(limpio, @"^[A-Z0-9]*?([FGHJKMNQUVXZ])(\d{1,2})$");
            if (!m.Success) return default(DateTime);
            int mes = "FGHJKMNQUVXZ".IndexOf(m.Groups[1].Value[0]) + 1;
            int y = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            int anio = m.Groups[2].Value.Length == 2 ? 2000 + y : (DateTime.UtcNow.Year / 10) * 10 + y;
            if (m.Groups[2].Value.Length == 1 && anio < DateTime.UtcNow.Year - 1) anio += 10;
            var d1 = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
            int haciaViernes = ((int)DayOfWeek.Friday - (int)d1.DayOfWeek + 7) % 7;
            return d1.AddDays(haciaViernes + 14).AddHours(13.5);      // tercer viernes, 9:30 de Nueva York
        }

        private double DividendoUsado() => Dividendo > 0 ? (double)Dividendo : (Raiz() == "NQ" ? 0.008 : 0.012);

        private string RutaBaseRueda() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex", "base-rueda-" + Raiz() + ".json");

        /// <summary>Mide la base en la rueda: el cierre de la vela del grafico a la hora real del
        /// spot de la cadena (su ts menos los 902 s de retraso de CBOE) menos ese spot. Mediana de
        /// las ultimas 30 cadenas distintas; se guarda en disco para las noches y los reinicios.</summary>
        private void MedirBaseRueda(Feed.Cadena c)
        {
            var iv = CultureInfo.InvariantCulture;
            if (!_baseCargada)
            {
                _baseCargada = true;
                try
                {
                    var p = RutaBaseRueda();
                    if (File.Exists(p))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(p));
                        _baseRueda = doc.RootElement.GetProperty("base").GetDouble();
                        _baseRuedaUtc = DateTime.ParseExact(doc.RootElement.GetProperty("utc").GetString(), "yyyy-MM-ddTHH:mm:ss", iv, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
                        Log("base de la rueda cargada: " + _baseRueda.ToString("0.00", iv) + " medida " + _baseRuedaUtc.ToString("yyyy-MM-dd HH:mm") + " UTC");
                    }
                }
                catch (Exception e) { Registrar(e); }
            }
            _nucleo.A.ExpiracionFuturoUtc = ExpiracionFuturo(); _nucleo.A.ExpiracionFuturoAltUtc = _expAlt;
            _nucleo.A.Dividendo = DividendoUsado();
            var ahora = DateTime.UtcNow;
            // UNA muestra por cada ts NUEVO de CBOE (no por cada "generado" del feed: el radar de 5 min
            // repite el mismo spot viejo con generado nuevo y la vela de ahora contra ese spot sesga con
            // la tendencia; medido el 10-09: 18 a 36 en el dia contra 24,7 real). La vela va en
            // ts - RetrasoCboeSeg (960 s alinea mejor que 902: MAD 2,9 contra 6,6). Sin los primeros 20
            // min de la rueda (el indice abre con acciones sin operar). Mediana robusta: se descartan
            // las muestras a mas de 3 MAD de la mediana. Cada muestra queda en el log.
            DateTime tsCboe = default(DateTime);
            if (c != null && !string.IsNullOrEmpty(c.Ts))
                DateTime.TryParseExact(c.Ts, "yyyy-MM-dd HH:mm:ss", iv, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out tsCboe);
            if (c != null && !c.EsFuturo && tsCboe != default(DateTime) && tsCboe != _baseObsUltimaCadena && c.SpotIdx > 0)
            {
                int mUtc = tsCboe.Hour * 60 + tsCboe.Minute;
                // solo con el contado abierto y ya estable (9:50-16:10 de Nueva York) y la cadena fresca
                if (mUtc >= 13 * 60 + 50 && mUtc <= 20 * 60 + 10 && (ahora - tsCboe).TotalMinutes <= 30)
                {
                    _baseObsUltimaCadena = tsCboe;
                    var horaVela = tsCboe.AddSeconds(-Math.Max(0, RetrasoCboeSeg));
                    int b = BarraDe(horaVela);
                    if (b >= 0)
                    {
                        double precio; DateTime tVela = default(DateTime);
                        try { var cb = GetCandle(b); precio = (double)cb.Close; tVela = Utc(cb.Time); } catch { precio = 0; }
                        if (precio > 0)
                        {
                            double muestra = precio - c.SpotIdx;
                            _baseObs.Add(muestra);
                            if (_baseObs.Count > 24) _baseObs.RemoveAt(0);
                            // mediana robusta
                            var ord = _baseObs.OrderBy(x => x).ToList();
                            double med = ord[ord.Count / 2];
                            double mad = ord.Select(x => Math.Abs(x - med)).OrderBy(x => x).ToList()[ord.Count / 2];
                            double tope = Math.Max(3 * mad, precio * 0.0002);
                            var buenas = _baseObs.Where(x => Math.Abs(x - med) <= tope).OrderBy(x => x).ToList();
                            if (buenas.Count >= 3) med = buenas[buenas.Count / 2];
                            if (buenas.Count >= 5 || double.IsNaN(_baseRueda)) { _baseRueda = med; _baseRuedaUtc = ahora; }
                            Log("base muestra: cboe " + tsCboe.ToString("HH:mm:ss", iv) + " spot " + c.SpotIdx.ToString("0.00", iv) + " vela " + tVela.ToString("HH:mm", iv) + " cierre " + precio.ToString("0.00", iv)
                                + " => " + muestra.ToString("0.00", iv) + " | mediana robusta " + med.ToString("0.00", iv) + " de " + buenas.Count + "/" + _baseObs.Count + " (MAD " + mad.ToString("0.0", iv) + ")");
                            if (buenas.Count >= 5)
                                try { File.WriteAllText(RutaBaseRueda(), "{\"base\":" + _baseRueda.ToString("0.####", iv) + ",\"utc\":\"" + ahora.ToString("yyyy-MM-ddTHH:mm:ss", iv) + "\",\"muestras\":" + buenas.Count + "}"); } catch { }
                        }
                    }
                }
            }
            _nucleo.A.BaseRueda = _baseRueda;
            _nucleo.A.BaseRuedaEdadMin = double.IsNaN(_baseRueda) ? double.NaN : (ahora - _baseRuedaUtc).TotalMinutes;
        }

        // ==================================================================
        // Gatillos de order flow en la banda (EXPERIMENTAL) y prints grandes
        // ==================================================================
        private void ConfigurarGatillo(GatilloBanda g)
        {
            g.BandaPct = (double)Math.Max(0.01m, BandaDominantesPct); g.QuietaPct = (double)GatilloQuietaPct; g.SoloRueda = GatillosSoloRueda;
        }

        private void ConfigurarRebote(GatilloRebote r)
        {
            r.ModoUso = (GatilloRebote.Modo)(int)ModoRebote; r.Enfriamiento = Math.Max(1, ReboteEnfriamiento); r.SoloRueda = GatillosSoloRueda;
            r.Paso = Raiz().Contains("NQ") ? 10 : 5;
        }

        /// <summary>El vivo arranca con las filas de la rueda de hoy ya dibujadas (los guiones) y
        /// con los disparos que el recorrido del archivo ya hizo hoy, y calienta la ventana de velas.</summary>
        private void SembrarRebote(int cerrada, DateTime horaUtc)
        {
            DateTime ini = horaUtc.Date.AddHours(13.5);
            if (horaUtc < ini) ini = ini.AddDays(-1);
            int desde = cerrada;
            for (int b = cerrada - 1; b >= Math.Max(0, cerrada - 1500); b--)
            {
                IndicatorCandle cb; try { cb = GetCandle(b); } catch { break; }
                if (cb == null || Utc(cb.Time) < ini) break;
                desde = b;
            }
            for (int b = Math.Max(desde, cerrada - (_rebVivo.Atras + 2)); b < cerrada; b++)
            {
                IndicatorCandle cb; try { cb = GetCandle(b); } catch { continue; }
                if (cb != null) _rebVivo.Procesar(b, Utc(cb.Time), (double)cb.Open, (double)cb.High, (double)cb.Low, (double)cb.Close, (double)cb.Delta, null, double.NaN, double.NaN, double.NaN);
            }
            var filas = new List<(double Fut, int Bar)>(); int hechos = 0;
            lock (_candado)
                for (int b = desde; b < cerrada; b++)
                {
                    if (_guiones.TryGetValue(b, out var lg)) foreach (var g in lg) filas.Add((g.Fut, b));
                    if (_disparos.TryGetValue(b, out var ld)) foreach (var t in ld) if (t.Tipo.StartsWith("rebote")) { _rebVivo.SembrarDisparo(t.Dom, t.Lado, b); hechos++; }
                }
            _rebVivo.Sembrar(filas);
            Log("gatillo rebote: arranca en vivo con " + _rebVivo.Filas.Count + " filas de la rueda (guiones desde la vela " + desde + ") y " + hechos + " disparos previos del archivo");
        }

        /// <summary>La vela que acaba de cerrar pasa por GatilloBanda con las dominantes
        /// vivas. Al primer llamado se calientan las estadisticas con las 60 velas previas
        /// (sin banda: no dispara sobre el pasado, eso ya lo hizo el recorrido del archivo).</summary>
        private void GatillosVivo(int bar)
        {
            int cerrada = bar - 1;
            if (cerrada < 1 || cerrada <= _barraGat) return;
            ConfigurarGatillo(_gatVivo);
            if (_barraGat < 0)
                for (int b = Math.Max(1, cerrada - 70); b < cerrada; b++)
                {
                    IndicatorCandle cb; try { cb = GetCandle(b); } catch { continue; }
                    if (cb != null) _gatVivo.Procesar(b, Utc(cb.Time), (double)cb.Open, (double)cb.High, (double)cb.Low, (double)cb.Close, (double)cb.Delta, null);
                }
            _barraGat = cerrada;
            IndicatorCandle c; try { c = GetCandle(cerrada); } catch { return; }
            if (c == null) return;
            List<(double Fut, double Gex)> domsG; lock (_candado) domsG = _doms != null ? _doms.ToList() : new List<(double Fut, double Gex)>();
            var tiros = _gatVivo.Procesar(cerrada, Utc(c.LastTime != default(DateTime) ? c.LastTime : c.Time), (double)c.Open, (double)c.High, (double)c.Low, (double)c.Close, (double)c.Delta, domsG);
            try
            {
                var tm = GatilloModeloVela(_modVivo, cerrada, c, Utc(c.LastTime != default(DateTime) ? c.LastTime : c.Time), domsG, _zeroVol, _mpVol, _mnVol, _maxChange);
                if (tm != null) tiros.Add(tm);
            }
            catch (Exception e) { Registrar(e); }
            try
            {
                ConfigurarRebote(_rebVivo);
                var horaR = Utc(c.LastTime != default(DateTime) ? c.LastTime : c.Time);
                if (!_rebSembrado) { _rebSembrado = true; SembrarRebote(cerrada, horaR); }
                tiros.AddRange(_rebVivo.Procesar(cerrada, horaR, (double)c.Open, (double)c.High, (double)c.Low, (double)c.Close, (double)c.Delta, domsG, _zeroVol, _mpVol, _mnVol));
            }
            catch (Exception e) { Registrar(e); }
            if (tiros.Count == 0) return;
            lock (_candado) _disparos[cerrada] = tiros;
            RegistrarGatillos(tiros, "vivo", false);
            var iv = CultureInfo.InvariantCulture;
            foreach (var t in tiros)
                Log("GATILLO " + t.Tipo + " " + (t.Lado > 0 ? "LARGO" : "CORTO") + " en " + t.Precio.ToString("0.00", iv)
                    + (t.Tipo == "modelo·es10" ? " p=" + t.Dz.ToString("0.00", iv) + " objetivo +-" + GatilloModelo_G()
                       : t.Tipo.StartsWith("rebote") ? " nivel " + t.Dom.ToString("0.00", iv) + " (" + t.Tipo.Substring(7) + ", toque " + t.Dz.ToString("0", iv) + " del dia)"
                       : " dominante " + t.Dom.ToString("0.00", iv) + (t.Arriba ? " (arriba)" : " (abajo)") + " dz " + t.Dz.ToString("0.0", iv)));
            try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
        }

        /// <summary>El gatillo MODELO sobre una vela cerrada: solo raiz ES y grafico de 1 minuto
        /// (asi se ajusto); niveles en precio del futuro; hora de Nueva York para el modo tarde.</summary>
        private GatilloBanda.Marca GatilloModeloVela(GatilloModelo mod, int bar, IndicatorCandle c, DateTime horaUtc,
                                                     List<(double Fut, double Gex)> doms, double zero, double mp, double mn, (double Fut, double Delta)[] mc)
        {
            if (ModoModelo == GatilloModeloModo.Ninguno || c == null) return null;
            if (Raiz() != "ES") return null;
            string marco = ChartInfo != null ? (ChartInfo.TimeFrame ?? "") : "";
            if (!GatilloModelo.Soporta(marco))
            {
                if (!_modAvisado) { _modAvisado = true; Log("gatillo modelo: solo en velas de 1 o 2 minutos (este grafico es " + marco + "): no dispara"); }
                return null;
            }
            mod.Marco = marco;
            mod.Umbral = (double)UmbralModelo;
            double cl = (double)c.Close;
            double domArr = double.NaN, domAba = double.NaN;
            if (doms != null)
                foreach (var dm in doms)
                {
                    if (dm.Fut > cl) { if (double.IsNaN(domArr) || dm.Fut < domArr) domArr = dm.Fut; }
                    else if (double.IsNaN(domAba) || dm.Fut > domAba) domAba = dm.Fut;
                }
            double mc30 = mc != null && mc.Length > 4 ? mc[4].Fut : double.NaN;
            var s = mod.Procesar(bar, (double)c.Open, (double)c.High, (double)c.Low, cl, (double)c.Delta, zero, mp, mn, domArr, domAba, mc30);
            if (ReferenceEquals(mod, _modVivo)) _modUltimaP = s.P;
            if (s.Lado == 0) return null;
            int horaNY = (horaUtc.Hour + 20) % 24;      // UTC-4 (horario de verano de Nueva York)
            if (ModoModelo == GatilloModeloModo.SoloTarde && (horaNY < 14 || horaNY >= 16)) return null;
            return new GatilloBanda.Marca { Bar = bar, Hora = horaUtc, Tipo = "modelo·es10", Lado = s.Lado, Precio = cl, Dom = double.IsNaN(zero) ? 0 : zero, Dz = s.P, Arriba = s.Lado < 0, Principal = true };
        }

        /// <summary>Cada disparo a un JSONL propio (vivo: se agrega; archivo: se pisa en cada
        /// recorrido), para que laboratorio/gatillos.py lo juzgue contra placebo.</summary>
        private void RegistrarGatillos(IEnumerable<GatilloBanda.Marca> tiros, string fuente, bool pisar)
        {
            try
            {
                var instr = InstrumentInfo != null ? InstrumentInfo.Instrument : "x";
                var marco = ChartInfo != null && ChartInfo.ChartType != null ? ChartInfo.ChartType + "-" + ChartInfo.TimeFrame : "x";
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS");
                var ruta = Path.Combine(dir, "pythiagex-gatillos-" + (fuente == "vivo" ? "" : "archivo-") + Lim(instr) + "-" + Lim(marco) + ".jsonl");
                var iv = CultureInfo.InvariantCulture;
                var sb = new System.Text.StringBuilder();
                foreach (var t in tiros)
                    sb.Append("{\"t\":\"").Append(t.Hora.ToString("yyyy-MM-ddTHH:mm:ss", iv)).Append("\",\"tipo\":\"").Append(t.Tipo).Append("\",\"lado\":").Append(t.Lado)
                      .Append(",\"precio\":").Append(t.Precio.ToString("0.####", iv)).Append(",\"dom\":").Append(t.Dom.ToString("0.####", iv))
                      .Append(",\"arriba\":").Append(t.Arriba ? "true" : "false").Append(",\"dz\":").Append(t.Dz.ToString("0.00", iv))
                      .Append(",\"principal\":").Append(t.Principal ? "true" : "false").Append(",\"fuente\":\"").Append(fuente).Append("\"}\n");
                if (pisar) File.WriteAllText(ruta, sb.ToString(), new System.Text.UTF8Encoding(false));
                else File.AppendAllText(ruta, sb.ToString(), new System.Text.UTF8Encoding(false));
            }
            catch (Exception e) { Registrar(e); }
        }

        private static string GatilloModelo_G() => GatilloModelo.G.ToString("0", CultureInfo.InvariantCulture) + " en " + GatilloModelo.H.ToString("0", CultureInfo.InvariantCulture) + " min";

        private static string Lim(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var ch in s ?? "x") sb.Append(char.IsLetterOrDigit(ch) ? ch : '-');
            return sb.ToString();
        }

        /// <summary>Cambio de vela: lo acumulado de prints grandes pasa a ser "de la vela cerrada".</summary>
        private void CerrarBig()
        {
            lock (_bigLlave)
            {
                if (_bigActual != null) { ContarBig(_bigActual); _bigActual = null; }
                _bigCerrada = (_bigN, _bigMax, _bigBuy, _bigSell);
                _bigN = 0; _bigMax = _bigBuy = _bigSell = 0;
            }
        }

        private void ContarBig(CumulativeTrade t)
        {
            double v = (double)t.Volume;
            if (v > _bigMax) _bigMax = v;
            if (v >= UmbralPrintGrande)
            {
                _bigN++;
                if (t.Direction == ATAS.Indicators.TradeDirection.Buy) _bigBuy += v;
                else if (t.Direction == ATAS.Indicators.TradeDirection.Sell) _bigSell += v;
            }
        }

        /// <summary>Operaciones acumuladas (una orden agresora que se llena en varios prints):
        /// lo que ATAS llama Big Trades. Solo en vivo. La anterior se cuenta cuando llega una
        /// nueva, asi entra con su volumen final.</summary>
        protected override void OnCumulativeTrade(CumulativeTrade trade)
        {
            try
            {
                if (trade == null) return;
                lock (_bigLlave)
                {
                    if (_bigActual != null && !ReferenceEquals(_bigActual, trade)) ContarBig(_bigActual);
                    _bigActual = trade;
                    if (_bigVistos++ == 0) Log("prints acumulados: llegan por OnCumulativeTrade (primero " + trade.Volume.ToString(CultureInfo.InvariantCulture) + " contratos)");
                }
            }
            catch (Exception e) { Registrar(e); }
        }

        protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
        {
            try { lock (_bigLlave) { if (trade != null) _bigActual = trade; } } catch { }
        }

        /// <summary>Los campos extra del centinela ("of"): delta maximo y minimo de la vela
        /// (los tiene el historico) y los prints grandes (solo en vivo).</summary>
        private static string ExtraOf(IndicatorCandle c, (int N, double Max, double Buy, double Sell)? big)
        {
            var iv = CultureInfo.InvariantCulture;
            string s = "\"dmax\":" + ((double)c.MaxDelta).ToString("0.#", iv) + ",\"dmin\":" + ((double)c.MinDelta).ToString("0.#", iv);
            if (big != null) s += ",\"big_n\":" + big.Value.N + ",\"big_max\":" + big.Value.Max.ToString("0.#", iv) + ",\"big_buy\":" + big.Value.Buy.ToString("0.#", iv) + ",\"big_sell\":" + big.Value.Sell.ToString("0.#", iv);
            return s;
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
            Foto foto = null, fotoTxt = null; int barFoto = -1;
            // el mouse sobre una vela del pasado: con "Cabecera" (default) NO se toca nada de lo
            // dibujado (bandas, rayas y barras siguen con el vivo) y solo se agrega un renglon;
            // con "Todo" (o con Fuente = Archivo) la pantalla entera pasa a esa vela
            bool mouseTodo = MouseSobreVela == MouseVela.Todo || (MouseSobreVela == MouseVela.Cabecera && Fuente == FuenteDatos.Archivo);
            bool mouseAlgo = MouseSobreVela != MouseVela.Nunca && Fuente != FuenteDatos.Vivo;
            lock (_candado)
            {
                perfil = _perfil; S = _S; futuro = _futuro; zeroVol = _zeroVol; zeroOi = _zeroOi;
                mpVol = _mpVol; mnVol = _mnVol; mpOi = _mpOi; mnOi = _mnOi; maxV = _maxAbsVol; maxO = _maxAbsOi; maxC = _maxAbsConv;
                netVol = _netVol; netOi = _netOi; doms = _doms; cuad = _cuadrante; corto = _cuadranteCorto; origenBase = _baseOrigen;
                libroConv = _libroConvUsado; libroDom = _libroDomUsado; alerta = _alerta; alertaHasta = _alertaHasta;
                mc = _maxChange; mucho = _mucho; convPrecio = _convEnPrecio; picoFut = _picoFut;
                if (mouseAlgo)
                {
                    barFoto = BarraBajoMouse();
                    // la foto de esa vela o, si no tiene (vela sin cadena), la mas cercana hacia atras:
                    // asi el mouse no salta al vivo cada vez que pasa por una vela vacia
                    if (barFoto >= 0)
                        for (int bb = barFoto; bb >= Math.Max(0, barFoto - 30) && fotoTxt == null; bb--) _fotosBarra.TryGetValue(bb, out fotoTxt);
                    if (fotoTxt != null && mouseTodo)
                    {
                        foto = fotoTxt;
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
            string l2 = (c != null && c.EsFuturo ? "libro " + Raiz() + " Rithmic " + edad + " · " + c.Filas.Count + " filas" : "vol CBOE " + edad) + " · OI de ayer · base " + origenBase + " · dominantes por " + libroDom
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
            // el renglon del mouse (modo Cabecera): que regia en esa vela, sin mover nada de lo dibujado
            string l3 = "";
            if (fotoTxt != null && foto == null)
            {
                var dtxt = fotoTxt.Doms ?? new List<(double Fut, double Gex)>();
                string dm = dtxt.Count == 0 ? "--" : string.Join(" / ", dtxt.Take(2).Select(z => z.Fut.ToString("N0", es)));
                l3 = "MOUSE vela " + fotoTxt.Vela.ToString("MM-dd HH:mm") + " UTC · fut " + fotoTxt.Futuro.ToString("N2", es)
                   + " · dominantes " + dm + " · zero " + (double.IsNaN(fotoTxt.ZeroVol) ? "--" : fotoTxt.ZeroVol.ToString("N0", es))
                   + " · " + fotoTxt.Corto + " · cadena " + fotoTxt.Cadena.ToString("HH:mm") + " UTC · (lo dibujado sigue siendo el vivo)";
            }
            int yc = area.Top + 26;
            var m1 = g.MeasureString(l1, f); var m2 = g.MeasureString(l2, fChica);
            var m3 = g.MeasureString(l3.Length > 0 ? l3 : "0", fChica);
            int h3 = l3.Length > 0 ? m3.Height + 2 : 0;
            int wc = Math.Max(Math.Max(m1.Width, m2.Width), l3.Length > 0 ? m3.Width : 0) + 12;
            g.FillRectangle(Color.FromArgb(200, ColFondo), new Rectangle(area.Left + 6, yc - 3, wc, m1.Height + m2.Height + h3 + 8));
            g.DrawString(l1, f, perfil.Count == 0 ? ColAviso : (convPrecio >= 0 ? ColConvPos : ColConvNeg), area.Left + 12, yc);
            g.DrawString(l2, fChica, Color.FromArgb(170, ColTexto), area.Left + 12, yc + m1.Height + 2);
            if (l3.Length > 0) g.DrawString(l3, fChica, Color.FromArgb(215, ColDom), area.Left + 12, yc + m1.Height + m2.Height + 4);
            if (DateTime.UtcNow < alertaHasta && alerta.Length > 0)
            {
                var ma = g.MeasureString("TRANSICION: " + alerta, f);
                int ya = yc + m1.Height + m2.Height + h3 + 10;
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
            // las pelotitas del Max Change: la foto vieja de cada ventana, POR STRIKE. En vivo, las
            // fotos por minuto del nucleo; con el mouse sobre una vela del pasado (modo Todo), los
            // perfiles de las velas anteriores a esa (la mas cercana con al menos N minutos de edad)
            int[] ventPel = { 15, 5, 1 };
            var antesPel = new Dictionary<double, double>[3];
            var antesConv = new Dictionary<double, double>[3];
            if (VerPelotitas)
            {
                if (foto == null)
                {
                    long ahoraMin = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
                    for (int i = 0; i < 3; i++)
                    {
                        var fv = fotos.Where(z => z.Minuto <= ahoraMin - ventPel[i]).LastOrDefault();
                        antesPel[i] = fv?.GexVol; antesConv[i] = fv?.Conv;
                    }
                }
                else
                {
                    Dictionary<int, Foto> fb; lock (_candado) fb = new Dictionary<int, Foto>(_fotosBarra);
                    for (int i = 0; i < 3; i++)
                    {
                        Foto fv = null;
                        for (int b = barFoto - 1; b >= 0 && b >= barFoto - 400; b--)
                            if (fb.TryGetValue(b, out var fx) && fx.Perfil != null && (foto.Vela - fx.Vela).TotalMinutes >= ventPel[i]) { fv = fx; break; }
                        if (fv != null) { var dct = new Dictionary<double, double>(); var dcv = new Dictionary<double, double>(); foreach (var st in fv.Perfil) { dct[st.K] = st.GexVol; dcv[st.K] = st.Conv; } antesPel[i] = dct; antesConv[i] = dcv; }
                    }
                }
            }
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
                        // donde estaba la punta hace 15 (grande), 5 (mediana) y 1 min (chica): adentro
                        // de la barra = el strike crece, afuera = decrece, juntas en la punta = quieto.
                        // Se dibujan de la grande a la chica, asi la chica queda encima si se pisan.
                        int[] rad = { Math.Max(2, alto / 2), Math.Max(2, alto / 2 - 1), Math.Max(1, alto / 2 - 2) };
                        for (int i = 0; i < 3; i++)
                        {
                            if (antesPel[i] == null || !antesPel[i].TryGetValue(s.K, out var gAntes)) continue;
                            if (Math.Sign(gAntes) != Math.Sign(s.GexVol) && gAntes != 0) gAntes = 0;   // cambio de signo: "estaba en cero"
                            int wa = Math.Max(0, (int)(Math.Sqrt(Math.Abs(gAntes) / maxV) * ancho));
                            int rr = rad[i];
                            g.FillEllipse(Color.FromArgb(225, 205, 205, 210), new Rectangle(x0 + wa - rr, y - rr, 2 * rr, 2 * rr));
                            g.DrawEllipse(new RenderPen(Color.FromArgb(230, col), 1f), new Rectangle(x0 + wa - rr, y - rr, 2 * rr, 2 * rr));
                        }
                    }
                }
                if (VerConvexidad && maxC > 0 && Math.Abs(s.Conv) > 0)
                {
                    double fr = Math.Sqrt(Math.Abs(s.Conv) / maxC);
                    int w = Math.Max(1, (int)(fr * ancho * 0.7));
                    var col = s.Conv >= 0 ? ColConvPos : ColConvNeg;
                    g.FillRectangle(Color.FromArgb((int)(110 + 120 * fr), col), new Rectangle(xConv - w, y - alto / 2, w, alto));
                    if (VerPelotitas)
                    {
                        // las mismas tres pelotitas sobre la convexidad (el producto las lleva en los dos perfiles)
                        int[] radC = { Math.Max(2, alto / 2), Math.Max(2, alto / 2 - 1), Math.Max(1, alto / 2 - 2) };
                        for (int i = 0; i < 3; i++)
                        {
                            if (antesConv[i] == null || !antesConv[i].TryGetValue(s.K, out var cAntes)) continue;
                            if (Math.Sign(cAntes) != Math.Sign(s.Conv) && cAntes != 0) cAntes = 0;
                            int wa = Math.Max(0, (int)(Math.Sqrt(Math.Abs(cAntes) / maxC) * ancho * 0.7));
                            int rr = radC[i];
                            g.FillEllipse(Color.FromArgb(225, 205, 205, 210), new Rectangle(xConv - wa - rr, y - rr, 2 * rr, 2 * rr));
                            g.DrawEllipse(new RenderPen(Color.FromArgb(230, col), 1f), new Rectangle(xConv - wa - rr, y - rr, 2 * rr, 2 * rr));
                        }
                    }
                    if (rotEsta)
                    {
                        // la convexidad de la barra: cuanto cambia su GEX si el precio sube 1 %
                        // (con Δ adelante: no es el GEX de la barra ni lleva vencimiento)
                        string lc = "Δ" + BmR(s.Conv);
                        var mc1 = g.MeasureString(lc, fRot);
                        int xc0 = xConv - w - 4 - mc1.Width;
                        g.FillRectangle(Color.FromArgb(150, ColFondo), new Rectangle(xc0 - 1, y - altoRot / 2, mc1.Width + 2, altoRot));
                        g.DrawString(lc, fRot, Color.FromArgb(225, col), xc0, y - altoRot / 2);
                    }
                }
            }
            g.DrawString("volumen hoy · sombra OI ayer", fChica, Color.FromArgb(110, ColTexto), x0 + 4, area.Top + 8);
            if (VerConvexidad) { var mcx = g.MeasureString("convexity ladder (" + libroConv + ")", fChica); g.DrawString("convexity ladder (" + libroConv + ")", fChica, Color.FromArgb(110, ColTexto), xConv - mcx.Width, area.Top + 8); }

            // ---- barras pesadas cercanas: raya punteada tenue en las N barras con mas GEX de
            // cada lado del precio, dentro del radio; 0DTE pesa 1,5x; sin repetir dominantes/majors
            if (RayasPesadas > 0 && !double.IsNaN(futuro) && perfil.Count > 0)
            {
                double radioP = futuro * (double)Math.Max(0.05m, RadioPesadasPct) / 100.0;
                bool Repetida(double fut) => doms.Any(dd => Math.Abs(dd.Fut - fut) < 0.5)
                                              || (!double.IsNaN(mpVol) && Math.Abs(mpVol - fut) < 0.5)
                                              || (!double.IsNaN(mnVol) && Math.Abs(mnVol - fut) < 0.5);
                var cerca = perfil.Where(s => Math.Abs(s.Fut - futuro) <= radioP && Math.Abs(s.GexVol) > 0 && !Repetida(s.Fut)).ToList();
                Func<Strike, double> pesoP = s => Math.Abs(s.GexVol) * (s.Dte < 1.0 ? 1.5 : 1.0);
                var pesadas = cerca.Where(s => s.Fut > futuro).OrderByDescending(pesoP).Take(RayasPesadas)
                         .Concat(cerca.Where(s => s.Fut <= futuro).OrderByDescending(pesoP).Take(RayasPesadas)).ToList();
                foreach (var s in pesadas)
                {
                    int y; try { y = cont.GetYByPrice((decimal)s.Fut, false); } catch { continue; }
                    if (y < area.Top || y > piso) continue;
                    var colP = s.GexVol >= 0 ? ColPos : ColNeg;
                    g.DrawLine(new RenderPen(Color.FromArgb(80, colP), 1f, System.Drawing.Drawing2D.DashStyle.Dot), xl0, y, xl1, y);
                    string rotP = BmR(s.GexVol) + (s.Dte < 1.0 ? " 0DTE" : (s.Dte < 1e6 ? " " + Math.Round(s.Dte).ToString(es) + "d" : ""));
                    var mrP = g.MeasureString(rotP, fRot);
                    g.DrawString(rotP, fRot, Color.FromArgb(150, colP), xl1 - mrP.Width - 2, y - mrP.Height - 1);
                }
            }

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
            // la zona de dominancia: una franja tenue alrededor de cada dominante, del ancho elegido
            if (BandaDominantesPct > 0 && !double.IsNaN(futuro))
            {
                // HACIA ADENTRO, NO HACIA AFUERA (pedido del operador, 2026-09-08): la franja
                // va de la dominante hacia el lado donde esta el precio, que es donde el
                // precio se da vuelta antes de tocar. Del otro lado del muro ya no hay canal.
                // El borde interno lleva una linea fina: "la linea de los 25 puntos".
                double semi = futuro * (double)BandaDominantesPct / 100.0;
                int xa = xRaya != int.MinValue ? Math.Max(xl0, Math.Min(xl1 - 4, xRaya)) : xl0;
                for (int i = 0; i < doms.Count; i++)
                {
                    bool arriba = doms[i].Fut >= futuro;              // dominante por encima del precio
                    double borde = arriba ? doms[i].Fut - semi : doms[i].Fut + semi;
                    int ya, yb; try { ya = cont.GetYByPrice((decimal)doms[i].Fut, false); yb = cont.GetYByPrice((decimal)borde, false); } catch { continue; }
                    int yt = Math.Max(area.Top, Math.Min(ya, yb)), ybt = Math.Min(piso, Math.Max(ya, yb));
                    if (ybt <= yt) continue;
                    g.FillRectangle(Color.FromArgb(i == 0 ? 28 : 18, ColDom), new Rectangle(xa, yt, Math.Max(1, xl1 - xa), ybt - yt));
                    if (yb >= area.Top && yb <= piso)
                        g.DrawLine(new RenderPen(Color.FromArgb(i == 0 ? 120 : 80, ColDom), 1f, System.Drawing.Drawing2D.DashStyle.Dot), xa, yb, xl1, yb);
                }
            }
            Raya(zeroOi, Color.FromArgb(160, 160, 170), 1f, System.Drawing.Drawing2D.DashStyle.Dot, 120);
            Raya(zeroVol, ColZero, 1.4f, System.Drawing.Drawing2D.DashStyle.Dash, 200);
            Raya(mpVol, ColPos, 1.6f, System.Drawing.Drawing2D.DashStyle.Solid, 190);
            Raya(mnVol, ColNeg, 1.6f, System.Drawing.Drawing2D.DashStyle.Solid, 190);
            for (int i = 0; i < doms.Count; i++) Raya(doms[i].Fut, ColDom, i == 0 ? 1.6f : 1.1f, System.Drawing.Drawing2D.DashStyle.Solid, i == 0 ? 220 : 160);

            // ---- estela: la dominante que regia en cada vela
            if (VerEstela || VerSemillas || VerZeroPorVela || VerGatillos != GatillosEnPantalla.Ninguno || ModoModelo != GatilloModeloModo.Ninguno || ModoRebote != GatilloReboteModo.Ninguno)
            {
                Dictionary<int, double[]> est; Dictionary<int, (double Zero, double[] Mc)> mar; Dictionary<int, List<(double Fut, int Rango, DateTime Hora)>> gui;
                var ahoraUtc = DateTime.UtcNow;
                Dictionary<int, List<GatilloBanda.Marca>> dis;
                lock (_candado)
                {
                    est = new Dictionary<int, double[]>(_estela); mar = new Dictionary<int, (double, double[])>(_marcas);
                    gui = _guiones.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
                    dis = _disparos.ToDictionary(kv => kv.Key, kv => kv.Value);
                }
                int desde = Math.Max(0, FirstVisibleBarNumber), hasta = Math.Min(CurrentBar - 1, LastVisibleBarNumber);
                // ancho de una vela en pixeles, medido en el grafico (no supuesto)
                int bw = 5;
                try { if (hasta > desde) bw = Math.Max(3, (cont.GetXByBar(hasta, false) - cont.GetXByBar(desde, false)) / Math.Max(1, hasta - desde)); } catch { }
                int grueso = Math.Max(1, GrosorGuion), fino = Math.Max(1, GrosorGuion - 1);
                for (int b = desde; b <= hasta; b++)
                {
                    int x; try { x = cont.GetXByBar(b, false); } catch { continue; }
                    // los gatillos de order flow: un triangulo apuntando hacia adentro del canal,
                    // pegado a la vela (arriba del maximo para cortos, abajo del minimo para largos)
                    if ((VerGatillos != GatillosEnPantalla.Ninguno || ModoModelo != GatilloModeloModo.Ninguno || ModoRebote != GatilloReboteModo.Ninguno) && dis.TryGetValue(b, out var lt))
                        foreach (var t in lt)
                        {
                            if (t.Tipo == "modelo·es10")
                            {
                                // el gatillo MODELO: rombo verde (largo) / rojo (corto) con su probabilidad
                                IndicatorCandle cm; try { cm = GetCandle(b); } catch { continue; }
                                int ym; try { ym = cont.GetYByPrice(t.Lado > 0 ? cm.Low : cm.High, false); } catch { continue; }
                                int rm = 6; int yy2 = t.Lado > 0 ? ym + rm + 5 : ym - rm - 5;
                                if (yy2 - rm < area.Top || yy2 + rm > piso) continue;
                                var colM = t.Lado > 0 ? ColPos : ColNeg;
                                var ptsM = new[] { new Point(x, yy2 - rm), new Point(x + rm, yy2), new Point(x, yy2 + rm), new Point(x - rm, yy2) };
                                g.FillPolygon(Color.FromArgb(240, colM), ptsM);
                                g.DrawPolygon(new RenderPen(Color.FromArgb(220, ColFondo), 1f), ptsM);
                                var rotM = "M " + t.Dz.ToString("0.00", es); var mrm = g.MeasureString(rotM, fChica);
                                g.DrawString(rotM, fChica, Color.FromArgb(235, colM), x + rm + 3, yy2 - mrm.Height / 2);
                                continue;
                            }
                            if (t.Tipo.StartsWith("rebote"))
                            {
                                // el gatillo REBOTE: circulo hueco chico en el PUNTO EXACTO donde se cumplio la
                                // condicion (la raya que toco, en la vela del disparo), verde largo / rojo corto;
                                // hueco para no tapar la mecha; la letra R (R1 = primer toque del nivel en el
                                // dia) va debajo del circulo en los largos y encima en los cortos: por fuera de la vela
                                if (ModoRebote == GatilloReboteModo.Ninguno) continue;
                                int yr; try { yr = cont.GetYByPrice((decimal)t.Dom, false); } catch { continue; }
                                int rr = Math.Max(2, TamanoRebote);
                                if (yr - rr < area.Top || yr + rr > piso) continue;
                                var colR = t.Lado > 0 ? ColPos : ColNeg;
                                g.DrawEllipse(new RenderPen(Color.FromArgb(245, colR), 1.6f), new Rectangle(x - rr, yr - rr, 2 * rr, 2 * rr));
                                var rotR = t.Dz <= 1 ? "R1" : "R"; var mrr = g.MeasureString(rotR, fChica);
                                int yl = t.Lado > 0 ? yr + rr + 1 : yr - rr - 1 - mrr.Height;
                                if (yl >= area.Top && yl + mrr.Height <= piso) g.DrawString(rotR, fChica, Color.FromArgb(225, colR), x - mrr.Width / 2, yl);
                                continue;
                            }
                            if (!t.Principal && VerGatillos != GatillosEnPantalla.Todos) continue;
                            IndicatorCandle cb; try { cb = GetCandle(b); } catch { continue; }
                            int yv; try { yv = cont.GetYByPrice(t.Lado < 0 ? cb.High : cb.Low, false); } catch { continue; }
                            int r = t.Principal ? 6 : 4;
                            int yy = t.Lado < 0 ? yv - r - 4 : yv + r + 4;
                            if (yy - r < area.Top || yy + r > piso) continue;
                            var col = t.Tipo == "ruptura·delta" ? Color.FromArgb(160, 150, 150, 160) : (t.Principal ? ColDom : ColDom2);
                            var pts = t.Lado < 0
                                ? new[] { new Point(x - r, yy - r), new Point(x + r, yy - r), new Point(x, yy + r) }
                                : new[] { new Point(x - r, yy + r), new Point(x + r, yy + r), new Point(x, yy - r) };
                            g.FillPolygon(Color.FromArgb(235, col), pts);
                            g.DrawPolygon(new RenderPen(Color.FromArgb(200, ColFondo), 1f), pts);
                            if (t.Principal)
                            {
                                var mr = g.MeasureString("tren", fChica);
                                g.DrawString("tren", fChica, Color.FromArgb(230, col), x + r + 3, yy - mr.Height / 2);
                            }
                        }
                    // La referencia: la dominante es un GUION amarillo por vela, primaria gruesa y secundaria fina.
                    // Puesto uno al lado del otro forman la linea sola: se ve donde nacio y cuando salto.
                    if (VerEstela && gui.TryGetValue(b, out var lg))
                    {
                        // primero los viejos, encima los NUEVOS (nacidos en vivo hace menos de
                        // EnfasisNuevasMin): mas grandes, claros y con borde oscuro, para ubicar
                        // el tren de dominantes de este instante; despues vuelven solos al normal
                        for (int pasada = 0; pasada < 2; pasada++)
                            foreach (var (fut, rango, hora) in lg)
                            {
                                bool nueva = EnfasisNuevasMin > 0 && hora != DateTime.MinValue && (ahoraUtc - hora).TotalMinutes <= EnfasisNuevasMin;
                                if (nueva != (pasada == 1)) continue;
                                int y; try { y = cont.GetYByPrice((decimal)fut, false); } catch { continue; }
                                if (y < area.Top || y > piso) continue;
                                int h = rango == 0 ? grueso : fino;
                                if (!nueva)
                                {
                                    g.FillRectangle(Color.FromArgb(rango == 0 ? 230 : 170, rango == 0 ? ColDom : ColDom2), new Rectangle(x - bw / 2, y - h / 2, bw, h));
                                    continue;
                                }
                                // NUEVA: nace lila fluo y se funde al amarillo normal a medida que envejece
                                // (t = 0 recien nacida, t = 1 cumplio EnfasisNuevasMin); el borde y el tamano
                                // extra se apagan con ella. Pedido del operador, 2026-09-11.
                                double tEdad = Math.Max(0.0, Math.Min(1.0, (ahoraUtc - hora).TotalMinutes / Math.Max(1, EnfasisNuevasMin)));
                                var colNueva = Mezclar(ColNuevo, rango == 0 ? ColDom : ColDom2, tEdad);
                                int extra = (int)Math.Round(2 * (1 - tEdad));
                                int hn = h + extra, wn = bw + extra;
                                if (tEdad < 0.75) g.FillRectangle(Color.FromArgb((int)(230 * (1 - tEdad)), ColFondo), new Rectangle(x - wn / 2 - 1, y - hn / 2 - 1, wn + 2, hn + 2));
                                g.FillRectangle(Color.FromArgb(rango == 0 ? 255 : 225, colNueva), new Rectangle(x - wn / 2, y - hn / 2, wn, hn));
                            }
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
                    // la hora de la vela viene "Unspecified" y ES UTC: ToUniversalTime() le sumaba 3 h
                    // y devolvia la vela de 3 horas antes (medido: base de la rueda 271 en vez de 22)
                    var t0 = Utc(c.Time);
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
