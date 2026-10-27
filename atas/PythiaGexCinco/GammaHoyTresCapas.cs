using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGexCinco
{
    /// <summary>Como se muestra una capa de CBOE: No = ni se baja; Fusion = solo engrosa el rotulo de NQ cuando coincide (a
    /// &lt;= 'Capas: fusion (pts)'); Propia = su propia estela de guiones y sus rotulos, en su color.</summary>
    /// <summary>Con que regla eligen sus dominantes las capas (3.0.6). Clasica = lo que dibuja la clasica en sus capas.</summary>
    public enum CapaRegla3
    {
        [Description("Clasica: una por lado + empate 20 % + centroide 12 pts (lo que dibuja la clasica en sus capas)")] Clasica,
        [Description("Dos mas grandes, strike exacto (2.0.5)")] DosMasGrandes,
        [Description("Como la primaria: la regla elegida para el libro del grafico (ES en la 5.0), sin histeresis")] ComoPrimaria,
    }

    public enum CapaModo3 { No, Fusion, Propia }

    /// <summary>
    /// 5.0 (27-10-2026, P4): EN LA 5.0 LAS CAPAS SON SPX Y SPY, y solo en un grafico de ES/MES (raiz "ES"). La capa SPX lee la cadena de
    /// _SPX que baja la 5.0 (archivo "ES", como cboe-local: SPXW diaria + SPX mensual AM) y se lleva al futuro del grafico con una BASE
    /// aditiva; la capa SPY lee la de SPY y se lleva por RAZON ES/SPY. Las dos postean su estela al motor de la familia (TRES_SPX y
    /// TRES_SPY, GuardarEstela con raiz "SPX"/"SPY") y escriben su estado en PythiaGex5 (base-rueda-ES.json, razon-rueda-SPY.json: nunca los
    /// de la clasica, la 2.0 ni la 4.1). Cotas MEDIDAS (MEDICIONES_ES.md 3.8, 30 ruedas de Z6): base 0 &lt; b &lt;= 150 y a max(25 %, 3 pts) del
    /// carry fut x 3,4 % x dias/365 del contrato DEL GRAFICO; razon 9,95-10,25; muestras de SPY solo con t_spot de 04:00 a 20:00 NY y reinicio
    /// el tercer viernes trimestral a las 04:00 NY (ex-dividendo); muestras de SPX de 09:35 a 16:00 NY; el vencimiento ya vencido fuera del
    /// libro (5.0.1: con la hora de los cierres anticipados, CierresAnticipados). En otro grafico (p. ej. MNQ) no se bajan, no se cuentan y no
    /// se dibujan. Lo que sigue es la historia de la 3.0/4.1 (NQ).
    ///
    /// LAS CAPAS QQQ Y NDX DE GAMMA HOY 3.0 (06-10-2026, pedido del operador: "habilitame igualmente el QQQ y el NDX y que yo
    /// pueda juzgar o elegir desactivarlos").
    ///
    /// Lo medido antes de habilitarlas (laboratorio/tres, DISENO, rueda, MNQ m2, contra placebo): NQ vivo +8,8 pp (clasica) /
    /// +7,4 (2.0.5); NDX por CBOE -2,2 / -1,0; QQQ por CBOE -1,2 / -0,8; combinaciones no le ganan a NQ solo. Por eso la
    /// PRIMARIA sigue siendo NQ vivo y estas dos son CAPAS que el operador prende o apaga. Vienen prendidas (modo Propia) porque
    /// lo pidio asi para juzgarlas a la vista; el default limpio del pliego seria No.
    ///
    /// De donde sale el dato (4.1): la ultima cadena de CBOE que baja LA PROPIA 4.0 (descargador B2) a %APPDATA%\ATAS\PythiaGex5\cboe\ultima-&lt;QQQ|NQ&gt;.json
    /// (NQ = NDX, cada 75 s en la rueda), o la que el integrador pase en memoria (FuenteCapaEnMemoria). Sin cboe_local.py y sin la nube: si no
    /// hay cadena propia de menos de 20 min, la capa no existe y el estado lo dice. (La 3.0 leia PythiaGex\cboe-local y, de respaldo, GitHub.)
    /// CBOE llega 15 minutos tarde (medido: 902 s): el rotulo de cada nivel lleva la edad REAL del dato (desde ultimo_trade).
    /// La cuenta es la MISMA (GammaHoyNucleo, mismos ajustes que la primaria). Al futuro: QQQ por RAZON (vela del MNQ abierta a la hora
    /// del ultimo_trade / spot del ETF: la razon 'alineada', estable; la del AUDIT de la clasica saltaba 12-28 pts) y NDX por BASE
    /// aditiva (vela alineada - spot). Sin vela alineada a menos de 30 min se usa el precio actual y se dice ("cruda").
    /// Cada capa escribe su estela en PythiaGex5\estela\estela-&lt;QQQ|NDX&gt;-&lt;dia&gt;.jsonl (mismo formato) para que el laboratorio la juzgue
    /// al lado de la de NQ, y al arrancar se vuelve a sembrar desde ese archivo (vida 25 min, como el juez para CBOE).
    /// </summary>
    public partial class GammaCinco
    {
        [Display(Name = "Capa SPY (CBOE, 15 min tarde, por razon ES/SPY)", GroupName = "9.3 3.0 · Pantalla", Order = 1060,
                 Description = "Solo en un grafico de ES/MES. No: no se baja ni se dibuja. Fusion: cuando un nivel de SPY coincide a 'fusion (pts)' con D1/D2 de ES, el rotulo de ES suma la sigla. Propia: estela de guiones celestes por vela y rotulos SPY D1/D2 con la edad del dato. Con Fusion o Propia se calcula y de aca sale la estela TRES_SPY de la familia (con No queda vacia). La razon ES/SPY es la mediana de la rueda (vela del MES a la hora del spot / spot de SPY), solo con el spot entre 04:00 y 20:00 NY, cota 9,95-10,25 y reinicio el tercer viernes trimestral a las 04:00 NY (ex-dividendo). SIN MEDIR EN ES como nivel (en NQ, QQQ por CBOE no le gano al placebo).")]
        public CapaModo3 Capa3SPY { get; set; } = CapaModo3.Propia;

        [Display(Name = "Capa SPX (CBOE, 15 min tarde, por base aditiva)", GroupName = "9.3 3.0 · Pantalla", Order = 1061,
                 Description = "Solo en un grafico de ES/MES. Igual que SPY pero con el libro de SPX (cadena _SPX: SPXW diaria + SPX mensual) llevado al futuro del grafico SUMANDO la base ES - SPX (ver 'Base de SPX: de donde sale'). Turquesa. Con Fusion o Propia de aca sale la estela TRES_SPX de la familia (prendida por defecto en la pestaña; con No queda vacia). SIN MEDIR EN ES como nivel (en NQ, NDX por CBOE dio -2,2 pp contra el placebo).")]
        public CapaModo3 Capa3SPX { get; set; } = CapaModo3.Propia;

        [Display(Name = "Capas: fusion con ES (pts)", GroupName = "9.3 3.0 · Pantalla", Order = 1062,
                 Description = "Distancia maxima para que un nivel de capa 'coincida' con D1/D2 de ES y le sume su sigla al rotulo (ES·SPX D1). Vale en modo Fusion y tambien en Propia; con 'Todas independientes' (9.7, el default) no hay fusion. 5.0: queda en 5 = un strike (ES y SPX listan cada 5, igual que NQ; un strike de SPX llevado a ES cae corrido por la base, ~2 pts mod 5).")]
        [Range(0.25, 50)]
        public decimal Capa3FusionPts { get; set; } = 5m;

        [Display(Name = "↳ capas 0Γ▸", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 69, Description = "Necesita la llave '3.0 capas▸' prendida. Sub-llave: sin ella no se dibuja ningun 0Γ de las capas ('↳ SPX 0Γ', '↳ SPX 0Γ enf', '↳ SPX 0Γ t/p', '↳ SPY 0Γ' y '↳ SPY 0Γ enf', abajo). 3.3.1 (pedido 07-10): el zero gamma de SPX y SPY como rombos por vela del color de la capa, mas su rotulo (misma formula que la 2.0: cruce de signo del perfil por strike mas cercano al precio), en precio de ES con la base o la razon de la capa. Con GuionGris queda la estela de 1 px.")]
        public bool Capa3ZeroRombos { get; set; } = true;

        [Display(Name = "Capas: tamaño de letra (% del de ES)", GroupName = "9.3 3.0 · Pantalla", Order = 1064,
                 Description = "Los rotulos de SPY y SPX van con letra mas chica que los del libro ES (pedido 06-10). 100 = igual tamaño.")]
        [Range(50, 100)]
        public int Capa3LetraPct { get; set; } = 75;

        [Display(Name = "Capas: cuantas dominantes (2 o 3)", GroupName = "9.3 3.0 · Pantalla", Order = 1065,
                 Description = "La clasica dibuja tres por capa (D1, D2, D3). 3.0.5: default 3 por pedido del operador (06-10). La estela guardada sigue con D1/D2.")]
        [Range(2, 3)]
        public int Capa3Cuantas { get; set; } = 3;

        [Display(Name = "↳ linea al eje", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 75, Description = "Necesita la llave '3.0 capas▸' prendida. Solo cambia algo con '↳ rayas largas' prendida (por defecto apagada). Cada dominante de capa dentro del radio de dibujo lleva una linea fina de 1 px desde la ultima vela hasta el eje, en su color (3.0.5). Sin esto solo se ve el chip.")]
        public bool Capa3LineaActual { get; set; } = true;

        [Display(Name = "Capas: regla de dominantes", GroupName = "9.3 3.0 · Pantalla", Order = 1067,
                 Description = "3.0.6: Clasica = una por lado + empate 20 % + centroide 12 pts, lo que dibuja la clasica en sus capas (el 12 nacio para SPX: ~2 strikes a cada lado). ComoPrimaria = la regla del libro ES sin histeresis. El radio de dominantes es el del libro ES (25 pts). SIN MEDIR EN ES (en NQ el 06-10 la Clasica puso NDX D1 en 31.511 y la zona se respeto).")]
        public CapaRegla3 Capa3Regla { get; set; } = CapaRegla3.Clasica;

        [Display(Name = "Capas: radio de dibujo (pts, 0 = el de ES)", GroupName = "9.3 3.0 · Pantalla", Order = 1068,
                 Description = "3.0.7: los guiones y la linea de una capa se dibujan solo a esta distancia del precio; mas lejos queda el rotulo con su precio y la estela muy tenue. 5.0: 15 por defecto (los 60 de NQ / 4, cociente de precios NQ/ES 3,8-3,9, DISENO 4); 0 = el radio de dibujo del libro ES (40). Los rotulos de las capas siguen hasta 40.")]
        [Range(0, 500)]
        public decimal Capa3RadioDibujoPtsEs { get; set; } = 15m;

        [Display(Name = "Base de SPX: de donde sale", GroupName = "9.3 3.0 · Pantalla", Order = 1095,
                 Description = "5.0. La base ES - SPX con la que la capa SPX se dibuja en ES. Rueda = la mediana robusta de 24 muestras sincronizadas (vela del MES a la hora del spot = sello de CBOE - 900 s, menos el spot de SPX, solo de 09:35 a 16:00 NY), de noche decaida con el carry hasta el vencimiento del contrato del grafico. Forwards (el default de la 4.1; en la 5.0 el default es Rueda) = la recta de forwards de la cadena (la de la 2.0), solo si pasa la cota del carry y queda a 3 pts o menos de la sincronizada en la rueda (de noche: 4 pts y residuo de 8 ticks o menos); si no, va la sincronizada. Medido en 30 ruedas de Z6: forwards - sincronizada mediana -0,72 (p05 -2,44, p95 +1,39). Cota de toda base: 0 < b <= 150 y a max(25 %, 3 pts) de fut x 3,4 % x dias/365.")]
        // 5.0 (27-10-2026, agente principal): default Rueda, no Forwards como la 4.1. Medido (MEDICIONES_ES 6): la base por forwards queda ~0,72 pts por debajo de la sincronizada (p05 -2,44, p95 +1,39) y la sincronizada tiene menos ruido (MAD 0,48); la familia (MUROS_SPX, ZEST_SPX) usa la sincronizada: con Forwards la raya TRES_SPX y la de la familia sobre el MISMO strike quedaban ~3 ticks separadas.
        public BaseSpx3 Capa3BaseSpx { get; set; } = BaseSpx3.Rueda;

        private sealed class CapaCboe
        {
            public string Nombre;            // 5.0: SPY / SPX (lo que se dibuja, el sufijo de su estela y la raiz del AlEstela: TRES_SPY / TRES_SPX)
            public string Archivo;           // 5.0: SPY / ES (como lo archiva el descargador de la 5.0 y cboe-local: "ES" = la cadena de _SPX)
            public bool PorRazon;            // SPY: K x razon; SPX: K + base
            public Color Color;
            public GammaHoyNucleo Nucleo = new GammaHoyNucleo();
            public GammaHoyNucleo.Lectura L;
            public Feed.Cadena Cadena;
            public DateTime GeneradoUtc = DateTime.MinValue, UltimoTradeUtc = DateTime.MinValue, UltimaCargaUtc = DateTime.MinValue;
            public string Estado = "sin dato", Origen = "";
            public double Escala = double.NaN, Base = double.NaN;
            public string UltimoSello = "";
            // 4.1: sin NubeJson / UltimaNubeUtc (la 4.0 no baja de la nube: la cadena la baja ella misma, ver CarpetaCboePropia)
            // 3.0.6: base de la rueda (solo capas aditivas): mediana robusta de (vela alineada - spot) con el indice vivo, persistida
            public List<double> BaseObs = new List<double>();
            public DateTime FechaObsNy = DateTime.MinValue;      // 5.0: la fecha NY de las muestras de BaseObs (se vacia al cambiar)
            public double BaseRueda = double.NaN;
            public DateTime BaseRuedaUtc = DateTime.MinValue;
            public int BaseMuestras;
            public string UltimoTsBase = "";
            public bool BaseCargada, BaseDeLaClasica;
            public List<(DateTime Gen, double Spot)> SpotHist = new List<(DateTime, double)>();   // 3.0.7: para saber si el spot esta vivo o congelado
            public double BaseForwards = double.NaN, BaseForwardsErrTicks = double.NaN;   // 3.6.5: base_cruda / base_error_ticks del archivo de la cadena
            public string Contrato;                              // 5.0: el contrato del grafico con el que se midieron BaseObs/BaseRueda (null = todavia ninguno)
            public DateTime ReinicioUtc = DateTime.MinValue;     // 5.0 (SPY): el ultimo reinicio por ex-dividendo ya aplicado
        }

        // 5.0 (27-10-2026): SPY primero y SPX al final (el AUDIT de las capas se marca con la ultima, como en la 4.1 con QQQ/NDX). Colores de la 4.1.
        private readonly List<CapaCboe> _capas = new List<CapaCboe>
        {
            new CapaCboe { Nombre = "SPY", Archivo = "SPY", PorRazon = true,  Color = Color.FromArgb(130, 177, 255) },  // celeste (3.0.5: el #2962ff de la referencia no se veia sobre el fondo oscuro)
            new CapaCboe { Nombre = "SPX", Archivo = "ES",  PorRazon = false, Color = Color.FromArgb(38, 198, 218) },   // turquesa (3.0.5: el gris 158 de la clasica se perdia; el operador no la veia)
        };
        private readonly Dictionary<string, Dictionary<DateTime, double[]>> _estelaCapas = new Dictionary<string, Dictionary<DateTime, double[]>>(StringComparer.OrdinalIgnoreCase);
        private const double VIDA_CAPA_S = 25 * 60;       // sin cadena fresca el nivel no existe (CBOE: generado + 25 min, como el juez)
        // GANCHO-4.1 descargador: la 3.0 leia %APPDATA%\ATAS\PythiaGex\cboe-local\ultima-<QQQ|NQ>.json (lo escribe cboe_local.py, un programa
        // EXTERNO) y, si tenia mas de 20 min, la nube (GitHub raw). La 4.1 lee SOLO lo que baja ella misma: el descargador propio (B2, IFuenteCboe)
        // escribe %APPDATA%\ATAS\PythiaGex5\cboe\ultima-<ES|SPY>.json con la MISMA linea flaca de archivar_cadena.py (ES = la cadena de _SPX).
        // 5.0 (27-10-2026): el descargador baja SPX y SPY (sin TQQQ). Si el integrador prefiere pasarla en memoria (sin el archivo), asigna
        // FuenteCapaEnMemoria: (Archivo "ES"|"SPY") -> (json, escrito UTC) (IntegracionFamilia: "ES" -> UltimoRegistro("SPX"), "SPY" -> "SPY")
        // y aca se usa eso; null o sin dato = el archivo propio. Nada mas: ni cboe-local, ni nube, ni base de la clasica.
        private static string CarpetaCboePropia => Path.Combine(Registro.CarpetaDatos, "cboe");
        internal static Func<string, (string Json, DateTime EscritoUtc)?> FuenteCapaEnMemoria;

        private CapaModo3 ModoDe(CapaCboe k) => k.PorRazon ? Capa3SPY : Capa3SPX;   // 5.0: SPY = la de razon, SPX = la aditiva

        /// <summary>3.6.8: el cruce de signo mas cercano ARRIBA y el mas cercano ABAJO de f (lista ya en precio del futuro).</summary>
        private static (double Techo, double Piso) Cerco(List<double> l, double f)
        {
            double up = double.NaN, dn = double.NaN;
            if (l != null && f > 0) foreach (var z in l) { if (double.IsNaN(z) || z <= 0) continue; if (z > f && (double.IsNaN(up) || z < up)) up = z; if (z < f && (double.IsNaN(dn) || z > dn)) dn = z; }
            return (up, dn);
        }
        /// <summary>3.6.8: TODOS los cruces de signo del perfil por volumen (strike por strike, interpolados como ZeroPorSigno), en precio del futuro.
        /// La lista del nucleo llega solo a +-0,2 % del precio de ahora; para reconstruir la noche hace falta el perfil entero.</summary>
        private static List<double> CrucesPerfil(GammaHoyNucleo.Lectura L)
        {
            var r = new List<double>(); if (L?.Perfil == null) return r;
            var p = L.Perfil.Where(s => s.Fut > 0 && s.GexVol != 0 && !double.IsNaN(s.GexVol)).OrderBy(s => s.Fut).ToList();
            for (int i = 1; i < p.Count; i++) { double g0 = p[i - 1].GexVol, g1 = p[i].GexVol; if ((g0 < 0 && g1 > 0) || (g0 > 0 && g1 < 0)) r.Add(p[i - 1].Fut + (p[i].Fut - p[i - 1].Fut) * (-g0) / (g1 - g0)); }
            return r;
        }
        private bool CercoCapa(string nombre) => Raya3SpxZeroCerco && string.Equals(nombre, "SPX", StringComparison.OrdinalIgnoreCase);   // 5.0: SPX (la 4.1: NDX)

        private static readonly TimeZoneInfo ZonaNyCapas = ZonaNyONull();
        private static TimeZoneInfo ZonaNyONull() { try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); } catch { return null; } }
        // 5.0: PythiaGex5\base-rueda-ES.json (capa SPX) y PythiaGex5\razon-rueda-SPY.json (capa SPY). Mismo nombre que la base de la clasica
        // (PythiaGex\base-rueda-ES.json) y de la 2.0 (PythiaGex2\...), pero en la carpeta de la 5.0: no se leen ni se pisan.
        private string RutaBaseRueda(CapaCboe k) => Path.Combine(Registro.CarpetaDatos, (k.PorRazon ? "razon-rueda-" : "base-rueda-") + k.Archivo + ".json");

        // ------------------------------------------------------------------ 5.0 (27-10-2026): contrato del grafico, cotas medidas y calendario
        // Todo MEDIDO en MEDICIONES_ES.md (3.2-3.8, 30 ruedas de ESZ6 y 28 de SPY, 15-09..26-10). Nada de esto sale de NQ.

        /// <summary>(r - q) implicito de la base ES - SPX MEDIDO: 3,4 % (p05 3,3 %, p95 3,6 %, 30 ruedas de Z6). La formula de los indicadores
        /// (3,75 % - 1,2 %) queda 25-43 % corta; la de NDX (0,6 % de dividendo) no aplica al S&amp;P.</summary>
        private const double CARRY_SPX = 0.034;
        /// <summary>Sanidad de la base ES - SPX: 0 &lt; base &lt;= 150 (medido en Z6: 35-72; un trimestral recien rolado ~65-75).</summary>
        private const double BASE_SPX_MAX = 150.0;
        /// <summary>Sanidad de la razon ES/SPY (medido 10,081-10,127; SPX/SPY 10,015-10,040). La 4.1 tenia 30-60 (QQQ) y rechazaria SPY siempre.</summary>
        private const double RAZON_SPY_MIN = 9.95, RAZON_SPY_MAX = 10.25;
        /// <summary>Forwards contra la sincronizada (MEDIDO, foto por foto): en la rueda |d| p05/p95 -2,44/+1,39 -> tope 3; de noche solo con
        /// residuo de la recta &lt;= 8 ticks (sin eso el p95 es 5-13 pts: la foto trae el 0DTE vencido) y |d| &lt;= 4 (p05/p95 -1,70/+3,80).
        /// Reemplaza la tolerancia de 40 pts de la 4.1 (la base NDX ~240; la de SPX entera son 35-37).</summary>
        private const double FWD_TOL_RUEDA = 3.0, FWD_TOL_NOCHE = 4.0, FWD_ERR_NOCHE_TICKS = 8.0;

        private string _contratoCapas = "";
        /// <summary>El contrato del grafico (MESZ6 -> "Z6"). ATAS da la raiz sola ("MES") en pestañas ocultas: entonces vale el ultimo conocido
        /// (asi un "" de paso no borra lo medido) y, si nunca hubo, "" (= trimestral supuesto).</summary>
        private string ContratoCapas()
        {
            string c = ""; try { c = ContratoGrafico(); } catch { }
            if (!string.IsNullOrEmpty(c)) _contratoCapas = c;
            return _contratoCapas;
        }

        private static DateTime TercerViernesCapa(int anio, int mes)
        {
            var d = new DateTime(anio, mes, 1);
            int hasta = ((int)DayOfWeek.Friday - (int)d.DayOfWeek + 7) % 7;
            return d.AddDays(hasta + 14);
        }
        private static DateTime NyAUtcCapa(DateTime ny)
        {
            if (ZonaNyCapas == null) return DateTime.SpecifyKind(ny.AddHours(4), DateTimeKind.Utc);
            try { return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(ny, DateTimeKind.Unspecified), ZonaNyCapas); }
            catch { return DateTime.SpecifyKind(ny.AddHours(5), DateTimeKind.Utc); }
        }
        private static DateTime UtcANyCapa(DateTime utc)
        {
            if (ZonaNyCapas == null) return utc.AddHours(-4);
            try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ZonaNyCapas); } catch { return utc.AddHours(-4); }
        }

        /// <summary>Vencimiento (UTC) de un futuro de ES por su mes y año ("Z6" -> 18-12-2026 09:30 NY, el SOQ del tercer viernes; "Z26" tambien).
        /// MinValue si el codigo no se entiende.</summary>
        private static DateTime VencimientoContratoUtc(string contrato, DateTime ahoraUtc)
        {
            if (string.IsNullOrEmpty(contrato) || contrato.Length < 2) return DateTime.MinValue;
            int mes = "FGHJKMNQUVXZ".IndexOf(char.ToUpperInvariant(contrato[0])) + 1;
            if (mes <= 0 || !int.TryParse(contrato.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out int yy)) return DateTime.MinValue;
            int y0 = ahoraUtc.Year - 1;
            int anio = contrato.Length >= 3 ? 2000 + yy : y0 + ((yy - y0 % 10) % 10 + 10) % 10;   // "Z6": el primer año >= (este - 1) que termina en 6
            if (anio < 2000 || anio > 2100) return DateTime.MinValue;
            return NyAUtcCapa(TercerViernesCapa(anio, mes).AddHours(9.5));
        }

        /// <summary>Contra que vencimiento(s) se acota la base: el del contrato del grafico; si el codigo no trae mes y año, el trimestral mas
        /// cercano que no vencio y tambien el siguiente (regla 1.5 de la clasica: el grafico pudo haber rolado). Vacia = sin cota de carry.</summary>
        private List<DateTime> VencimientosGrafico(DateTime ahoraUtc, out string contrato)
        {
            contrato = ContratoCapas();
            var l = new List<DateTime>();
            var v = VencimientoContratoUtc(contrato, ahoraUtc);
            if (v != DateTime.MinValue) { if (v > ahoraUtc) l.Add(v); return l; }
            var ny = UtcANyCapa(ahoraUtc);
            for (int anio = ny.Year; anio <= ny.Year + 1 && l.Count < 2; anio++)
                foreach (int mes in new[] { 3, 6, 9, 12 })
                {
                    var u = NyAUtcCapa(TercerViernesCapa(anio, mes).AddHours(9.5));
                    if (u > ahoraUtc && l.Count < 2) l.Add(u);
                }
            return l;
        }

        /// <summary>El ultimo reinicio por ex-dividendo de SPY a la hora 'ahora': el tercer viernes de mar/jun/sep/dic a las 04:00 NY. MEDIDO el
        /// 18-09-2026: la razon ES/SPY salto +0,0204 (~15,6 pts de ES) y la mediana sin reinicio quedo corrida hasta el mediodia.</summary>
        private static DateTime ReinicioExDivSpyUtc(DateTime ahoraUtc)
        {
            var ny = UtcANyCapa(ahoraUtc);
            var mejor = DateTime.MinValue;
            for (int anio = ny.Year - 1; anio <= ny.Year; anio++)
                foreach (int mes in new[] { 3, 6, 9, 12 })
                {
                    var u = NyAUtcCapa(TercerViernesCapa(anio, mes).AddHours(4));
                    if (u <= ahoraUtc && u > mejor) mejor = u;
                }
            return mejor;
        }

        /// <summary>Si el spot de CBOE a la hora t_spot (sello - 900 s) es un precio de ESE momento. SPY: de 04:00 a 20:00 NY (a las 20:00 el
        /// spot vuelve al cierre oficial: 23 muestras de 20:00-20:06 dieron hasta 36 pts de error; de 20:00 a 04:00 no se mueve). SPX: la regla
        /// C7, de 09:35 a 16:00 NY (fuera de la rueda el indice esta congelado; la apertura es ruido). Lunes a viernes.</summary>
        private static bool HoraSpotValida(CapaCboe k, DateTime tSpotUtc)
        {
            if (tSpotUtc == DateTime.MinValue) return false;
            var ny = UtcANyCapa(tSpotUtc);
            if (ny.DayOfWeek == DayOfWeek.Saturday || ny.DayOfWeek == DayOfWeek.Sunday) return false;
            double h = ny.TimeOfDay.TotalHours;
            return k.PorRazon ? h >= 4.0 && h < 20.0 : h >= 9.0 + 35.0 / 60.0 && h < 16.0;
        }

        /// <summary>Las cotas MEDIDAS que tiene que pasar una conversion para usarse (MEDICIONES_ES.md 3.8). SPY: 9,95 &lt;= razon &lt;= 10,25.
        /// SPX: 0 &lt; base &lt;= 150 y |base - fut x 0,034 x dias/365| &lt;= max(25 % de eso, 3 pts), con los dias al vencimiento del contrato DEL
        /// GRAFICO (en el roll una base del trimestre viejo, ~5, no cabe en la del nuevo, ~70: medido 10/11-09, U6 6,4/3,6 contra Z6 70,9/73,0).</summary>
        private bool ConversionCabe(CapaCboe k, double v, double fut, DateTime ahora, out string porque)
        {
            var inv = CultureInfo.InvariantCulture;
            porque = "";
            if (double.IsNaN(v) || double.IsInfinity(v)) { porque = "sin valor"; return false; }
            if (k.PorRazon)
            {
                if (v >= RAZON_SPY_MIN && v <= RAZON_SPY_MAX) return true;
                porque = "razon fuera de " + RAZON_SPY_MIN.ToString("0.00", inv) + "-" + RAZON_SPY_MAX.ToString("0.00", inv);
                return false;
            }
            if (!(v > 0 && v <= BASE_SPX_MAX)) { porque = "base fuera de (0, " + BASE_SPX_MAX.ToString("0", inv) + "]"; return false; }
            var vencs = VencimientosGrafico(ahora, out var contrato);
            if (vencs.Count == 0 || fut <= 0) return true;   // sin vencimiento conocido: solo la sanidad
            var partes = new List<string>();
            foreach (var vu in vencs)
            {
                double c = fut * CARRY_SPX * (vu - ahora).TotalDays / 365.0, tol = Math.Max(0.25 * c, 3.0);
                if (Math.Abs(v - c) <= tol) return true;
                partes.Add("carry " + c.ToString("0.0", inv) + " +-" + tol.ToString("0.0", inv) + " al " + UtcANyCapa(vu).ToString("dd-MM", inv));
            }
            porque = "fuera de la cota del carry: " + string.Join(" / ", partes) + (contrato == "" ? " (grafico sin mes: trimestral supuesto)" : " (contrato " + contrato + ")");
            return false;
        }

        /// <summary>La mediana de la rueda llevada a AHORA. SPX: x (vto - ahora) / (vto - hora de la mediana) con el vencimiento del contrato del
        /// grafico (la regla C7 de la noche: la base es carry y decae con los dias; MEDIDO contra la rueda siguiente: mediana -0,09 pts, MAD 1,07;
        /// sin decaer se corre ~0,5-1 pt por noche). SPY: tal cual (C8 no decae: -1 pt por dia medido pero no probado como correccion).</summary>
        private double MedianaAhora(CapaCboe k, DateTime ahora, out string txt)
        {
            txt = "";
            double b = k.BaseRueda;
            if (k.PorRazon || double.IsNaN(b) || k.BaseRuedaUtc == DateTime.MinValue) return b;
            var vencs = VencimientosGrafico(ahora, out var contrato);
            if (contrato == "" || vencs.Count != 1) return b;   // sin el contrato del grafico no se sabe a que vencimiento decae
            double total = (vencs[0] - k.BaseRuedaUtc).TotalDays, resta = (vencs[0] - ahora).TotalDays;
            if (total <= 0 || resta <= 0 || resta > total) return b;
            double f = resta / total;
            if (f < 0.9995) txt = ", decaida x" + f.ToString("0.0000", CultureInfo.InvariantCulture) + " por el carry al " + UtcANyCapa(vencs[0]).ToString("dd-MM", CultureInfo.InvariantCulture) + " (era " + b.ToString("0.00", CultureInfo.InvariantCulture) + ")";
            return b * f;
        }

        /// <summary>Lo medido para otro contrato del grafico (roll) o, en SPY, antes del ultimo ex-dividendo no vale: se borran las muestras y la
        /// mediana. Un contrato "" (raiz sola) no cambia nada.</summary>
        private void ControlarVigenciaRueda(CapaCboe k, DateTime ahora)
        {
            string contrato = ContratoCapas();
            if (contrato != "")
            {
                if (string.IsNullOrEmpty(k.Contrato)) k.Contrato = contrato;
                else if (k.Contrato != contrato)
                {
                    Log("capa " + k.Nombre + ": el contrato del grafico paso de " + k.Contrato + " a " + contrato + ": borro " + k.BaseObs.Count + " muestras y la " + (k.PorRazon ? "razon" : "base") + " de la rueda (eran del otro contrato)");
                    k.Contrato = contrato; k.BaseObs.Clear(); k.UltimoTsBase = ""; k.BaseRueda = double.NaN; k.BaseRuedaUtc = DateTime.MinValue; k.BaseMuestras = 0;
                }
            }
            if (!k.PorRazon) return;
            var rei = ReinicioExDivSpyUtc(ahora);
            if (rei != k.ReinicioUtc)
            {
                if (k.ReinicioUtc != DateTime.MinValue && k.BaseObs.Count > 0)
                {
                    Log("capa " + k.Nombre + ": ex-dividendo (tercer viernes, 04:00 NY = " + rei.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "Z): borro " + k.BaseObs.Count + " muestras de la razon");
                    k.BaseObs.Clear(); k.UltimoTsBase = "";
                }
                k.ReinicioUtc = rei;
            }
            if (!double.IsNaN(k.BaseRueda) && k.BaseRuedaUtc < k.ReinicioUtc)
            {
                Log("capa " + k.Nombre + ": la razon de la rueda " + k.BaseRueda.ToString("0.0000", CultureInfo.InvariantCulture) + " es de antes del ex-dividendo (" + k.ReinicioUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "Z): no se usa");
                k.BaseRueda = double.NaN; k.BaseRuedaUtc = DateTime.MinValue; k.BaseMuestras = 0;
            }
        }

        /// <summary>Un vencimiento de la linea flaca ({"f":"yyyy-MM-dd","dias":...}) que ya vencio: fecha anterior a hoy NY, u hoy desde su hora
        /// (16:00 NY; 09:30 NY si viene marcado "am": true, la SPX mensual). Sin fecha legible no se toca (el nucleo igual lo saca cuando sus dias,
        /// envejecidos desde 'generado', quedan negativos). 5.0.1 (27-10-2026): la hora de la PM es la de su producto en la tabla de cierres
        /// anticipados (CierresAnticipados.HoraPm: 13:00 NY el 27-11-2026 y el 24-12-2026; 16:00 NY los demas dias).</summary>
        private static bool VencimientoYaVencido(JsonElement v, DateTime ahoraNy, ProductoVenc producto, out string fecha)
        {
            fecha = "";
            if (!v.TryGetProperty("f", out var fe) || fe.ValueKind != JsonValueKind.String) return false;
            fecha = fe.GetString() ?? "";
            if (!DateTime.TryParseExact(fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia)) return false;
            bool am = v.TryGetProperty("am", out var a) && a.ValueKind == JsonValueKind.True;
            return YaVencioCapa(dia, am, producto, ahoraNy);
        }

        /// <summary>5.0.1: la regla de VencimientoYaVencido sin el JSON: vencio si su fecha es anterior a hoy NY u hoy desde su hora (AM 09:30;
        /// PM la de CierresAnticipados.HoraPm para el producto: 16:00 o la del cierre anticipado).</summary>
        private static bool YaVencioCapa(DateTime dia, bool am, ProductoVenc producto, DateTime ahoraNy)
        {
            if (dia.Date < ahoraNy.Date) return true;
            return dia.Date == ahoraNy.Date && ahoraNy.TimeOfDay >= (am ? new TimeSpan(9, 30, 0) : CierresAnticipados.HoraPm(dia.Date, producto));
        }

        // 4.1: el spot de CBOE es el de (sello de CBOE - 900 s) exactos (medido 08-10 sobre el crudo de TQQQ; ts - ultimo_trade: mediana 901 s en
        // NDX/QQQ/TQQQ). La 3.0 anclaba la vela en 'generado' - 960 (generado = la hora de la bajada de cboe_local, ~35-40 s despues del sello).
        // Con el descargador propio 'generado' es otra bajada: se ancla al SELLO (cadena.ts) - 900 y, solo si el sello no se puede leer, a
        // generado - 960 como la 3.0. OJO paridad: TRES_SPY/TRES_SPX de la 4.1 no son identicos a los de la 3.0 por esto (la razon de QQQ se
        // mueve unas diezmilesimas; NDX va por base de forwards y casi no lo nota).
        private const double RETRASO_CBOE_S = 900;           // desde el sello de CBOE (cadena.ts)
        private const double RETRASO_CBOE_GENERADO_S = 960;  // respaldo: desde 'generado', como la 3.0

        /// <summary>Cierre de la vela del grafico que contiene tUtc (la que abre a lo sumo 30 min antes); NaN si no hay.</summary>
        private double CierreEn(DateTime tUtc)
        {
            if (tUtc == DateTime.MinValue) return double.NaN;
            for (int b = CurrentBar - 1, n = 0; b >= 0 && n < 6000; b--, n++)
            {
                IndicatorCandle cb; try { cb = GetCandle(b); } catch { break; }
                if (cb == null) continue;
                var t = Utc(cb.Time);
                if (t <= tUtc) return (tUtc - t).TotalMinutes <= 30 ? (double)cb.Close : double.NaN;
            }
            return double.NaN;
        }

        /// <summary>3.0.7. Como se lleva la cadena de CBOE al futuro: razon (QQQ) o base (NDX), siguiendo EL RELOJ DEL SPOT.
        /// Medido el 06-10: el indice NDX se congela a las 16:00 NY (spot identico a las 16:00, 16:05, 16:10 y 16:14) mientras las opciones
        /// operan hasta las 16:15 y el futuro sigue: anclar la vela al ultimo trade dio base 271 contra 251 real (capa 20 pts corrida toda la
        /// noche). El ETF QQQ es al reves: sigue cotizando en el after-hours hasta las 20:00 NY con el ultimo trade de opciones clavado en
        /// 16:14: anclar al ultimo trade hace que la razon DERIVE con el spot (41,4407 -> 41,4505 en 10 min). Regla:
        ///   spot VIVO (cambio en los ultimos 10 min) y dato fresco -> muestra = vela de la hora del spot (generado - 16 min) contra el spot;
        ///     mediana robusta de las ultimas 24 muestras (como MedirBaseRueda de la clasica: 3 MAD) y esa mediana es el mapa.
        ///   spot CONGELADO o dato viejo -> la ultima mediana (persistida en PythiaGex5; 4.1: ya NO se lee la de la clasica).
        ///   4.1.1: sin mediana y con el spot QUIETO -> la conversion de la familia (C7 base de NDX / C8 razon de QQQ, sincronizadas y
        ///     persistidas). Medido 08-10 22:05 NY (ATAS recien abierto, sin mediana propia): la alineada comparaba el MNQ de las 20:48 con el
        ///     NDX congelado a las 16:00 y dio base 299.44 contra 241.03 de la familia y 241.83 de forwards (rayas "3.0 NDX" 58 pts arriba);
        ///     QQQ 41.4915 contra 41.4352 (~42 pts). Con el spot quieto la alineada YA NO se usa ni para descartar la base de forwards.
        ///   sin mediana, spot vivo -> la muestra alineada (como la 3.0); sin vela -> CRUDA (precio actual).
        ///   Si no queda nada confiable devuelve false y la capa no se calcula ni se dibuja (mejor sin raya que corrida).
        /// 5.0 (27-10-2026): ademas (a) el spot solo cuenta como vivo en su horario (HoraSpotValida: SPY 04:00-20:00 NY, SPX 09:35-16:00 NY), (b)
        /// cada candidata (mediana, familia, alineada, cruda, forwards) tiene que pasar ConversionCabe (cotas medidas: si no, se prueba la
        /// siguiente y el descarte queda en el origen), (c) la mediana de SPX se decae con el carry hasta el vencimiento del contrato del grafico,
        /// (d) lo medido para otro contrato del grafico o (SPY) antes del ex-dividendo se borra, y (e) forwards entra solo a 3 pts de la
        /// sincronizada en la rueda o a 4 de noche con residuo &lt;= 8 ticks (la 4.1 aceptaba 40 pts: mas que toda la base de SPX).</summary>
        private bool Mapear(CapaCboe k, Feed.Cadena cad, double spot, string ts, DateTime generado, DateTime tSpot, double fUlt, double fSpot, double fut, DateTime ahora)
        {
            var inv = CultureInfo.InvariantCulture;
            string F(double x) => k.PorRazon ? x.ToString("0.0000", inv) : x.ToString("0.00", inv);
            string Que() => k.PorRazon ? "razon" : "base";
            // 0) 5.0: lo medido para otro contrato del grafico (o, en SPY, antes del ex-dividendo) no vale
            ControlarVigenciaRueda(k, ahora);
            // 1) el reloj del spot
            if (k.SpotHist.Count == 0 || k.SpotHist[k.SpotHist.Count - 1].Gen != generado) { k.SpotHist.Add((generado, spot)); if (k.SpotHist.Count > 60) k.SpotHist.RemoveAt(0); }
            var recientes = k.SpotHist.Where(h => (generado - h.Gen).TotalMinutes <= 10).ToList();
            bool cambia = recientes.Any(h => Math.Abs(h.Spot - spot) > 1e-9);
            var iguales = k.SpotHist.Where(h => Math.Abs(h.Spot - spot) <= 1e-9).ToList();
            bool congelado = !cambia && iguales.Count >= 2 && (generado - iguales.Min(h => h.Gen)).TotalMinutes >= 6;
            bool fresca = (ahora - generado).TotalMinutes <= 20;
            // 5.0: fuera de su horario el spot no es un precio de ese momento (SPY a las 20:00 vuelve al cierre oficial): cuenta como quieto
            bool horaOk = HoraSpotValida(k, tSpot);
            bool vivo = cambia && horaOk;
            // 2) la muestra de este latido: vela de la hora del spot (sello - 900 s); si no hay, la del ultimo trade
            double muestra = double.NaN; string ancla = "";
            if (!double.IsNaN(fSpot) && fSpot > 0) { muestra = k.PorRazon ? fSpot / spot : fSpot - spot; ancla = "vela a la hora del spot (sello - 900 s)"; }
            else if (!double.IsNaN(fUlt) && fUlt > 0) { muestra = k.PorRazon ? fUlt / spot : fUlt - spot; ancla = "vela al ultimo trade"; }
            CargarBaseRuedaSiHaceFalta(k);
            string selloMuestra = ts + "|" + spot.ToString("0.####", inv);
            if (vivo && fresca && !double.IsNaN(muestra) && selloMuestra != k.UltimoTsBase)
            {
                k.UltimoTsBase = selloMuestra;
                if (!ConversionCabe(k, muestra, fut, ahora, out var pqM))
                    Log("capa " + k.Nombre + ": " + Que() + " muestra " + F(muestra) + " (" + ancla + ", spot " + spot.ToString("0.####", inv) + ") DESCARTADA: " + pqM);
                else
                {
                    // 5.0 (revision adversarial M1): la primera muestra de una fecha NY nueva vacia las de ayer (antes la mediana de la manana era la
                    // de ayer sin decaer: ~+0,7 pts por dia; la familia ya reinicia sus 24 por dia de NY en ConversionSpx).
                    var fechaMuestraNy = UtcANyCapa(tSpot == DateTime.MinValue ? ahora : tSpot).Date;
                    if (k.BaseObs.Count > 0 && k.FechaObsNy != DateTime.MinValue && fechaMuestraNy != k.FechaObsNy) k.BaseObs.Clear();
                    k.FechaObsNy = fechaMuestraNy;
                    k.BaseObs.Add(muestra); if (k.BaseObs.Count > 24) k.BaseObs.RemoveAt(0);
                    var ord = k.BaseObs.OrderBy(x => x).ToList();
                    double med = ord[ord.Count / 2];
                    double mad = ord.Select(x => Math.Abs(x - med)).OrderBy(x => x).ToList()[ord.Count / 2];
                    double tope = Math.Max(3 * mad, Math.Abs(med) * 0.0002);
                    var buenas = k.BaseObs.Where(x => Math.Abs(x - med) <= tope).OrderBy(x => x).ToList();
                    if (buenas.Count >= 3) med = buenas[buenas.Count / 2];
                    if (buenas.Count >= 5 || double.IsNaN(k.BaseRueda)) { k.BaseRueda = med; k.BaseRuedaUtc = ahora; k.BaseMuestras = buenas.Count; k.BaseDeLaClasica = false; }
                    if (buenas.Count >= 5)
                    {
                        try { File.WriteAllText(RutaBaseRueda(k), "{\"base\":" + med.ToString("0.######", inv) + ",\"utc\":\"" + ahora.ToString("yyyy-MM-dd'T'HH:mm:ss", inv) + "\",\"muestras\":" + buenas.Count + ",\"tipo\":\"" + Que() + "\",\"contrato\":\"" + (k.Contrato ?? "") + "\"}"); }
                        catch (Exception e) { Registro.Excepcion(LOG, "mapa-rueda guardar " + k.Nombre, e); }
                    }
                    Log("capa " + k.Nombre + ": " + Que() + " muestra " + F(muestra) + " (" + ancla + ", spot " + spot.ToString("0.####", inv) + ") -> mediana " + F(med) + " de " + buenas.Count + "/" + k.BaseObs.Count + " · spot vivo" + (string.IsNullOrEmpty(k.Contrato) ? "" : " · contrato " + k.Contrato));
                }
            }
            bool medianaVale = !double.IsNaN(k.BaseRueda) && (ahora - k.BaseRuedaUtc).TotalHours <= 24;
            string estadoSpot = !horaOk ? (cambia ? "spot fuera de su horario" : "spot quieto fuera de su horario") + " (t_spot " + (tSpot == DateTime.MinValue ? "?" : UtcANyCapa(tSpot).ToString("ddd HH:mm", inv) + " NY") + ")"
                              : vivo ? "spot vivo" : congelado ? "spot congelado" : "spot sin cambio reciente";
            // 3) la conversion: la primera candidata que pasa las cotas medidas (5.0); las descartadas quedan escritas en el origen
            double mapa = double.NaN; string origen = "", cual = ""; bool dudosa = false;
            var descartes = new List<string>();
            bool Probar(double v, string nombre, Func<string> texto, bool dud)
            {
                if (double.IsNaN(v)) return false;
                if (!ConversionCabe(k, v, fut, ahora, out var pq)) { descartes.Add(nombre + " " + F(v) + " (" + pq + ")"); return false; }
                mapa = v; origen = texto(); dudosa = dud; cual = nombre; return true;
            }
            string txtMed = "", txtFam = "";
            double medAhora = medianaVale ? MedianaAhora(k, ahora, out txtMed) : double.NaN;
            double conFam = ConversionDeLaFamilia(k, ahora, out txtFam);
            double cruda = spot > 0 ? (k.PorRazon ? fut / spot : fut - spot) : double.NaN;
            // 5.0 (27-10-2026, revision adversarial A2): la conversion de la FAMILIA va primero, tambien con el spot vivo: es la que muestra el eje
            // doble y la de MUROS_SPX/ZEST_SPX (C7/C8, el tick a la hora del spot). Medido hoy 15:25Z tras reiniciar: con la mediana propia en
            // armado, la capa uso UNA muestra alineada (33,99) contra 36,31 de la familia: TRES_SPX quedo 2,3 pts corrida; ya con mediana, 0,64
            // abajo. La mediana propia queda de respaldo (sin familia: primeros minutos o si la familia no tiene base vigente).
            bool hay =
                Probar(conFam, "familia", () => Que() + " de la familia " + F(conFam) + " (" + txtFam + "); " + estadoSpot
                                                        + (double.IsNaN(muestra) ? "" : ", alineada ahora " + F(muestra)), false)
                || (medianaVale && (!vivo || !fresca || k.BaseMuestras >= 5)
                    && Probar(medAhora, "mediana", () => Que() + " mediana " + F(medAhora) + " (" + k.BaseMuestras + " muestras, " + k.BaseRuedaUtc.ToString("HH:mm", inv) + "Z" + txtMed + (k.BaseDeLaClasica ? ", archivo de la clasica" : "") + "); " + estadoSpot
                                                        + (double.IsNaN(muestra) ? "" : ", alineada ahora " + F(muestra)), false))
                || (!vivo && Probar(conFam, "familia", () => Que() + " de la familia " + F(conFam) + " (" + txtFam + "); " + estadoSpot + ", sin mediana propia que sirva"
                                                        + (double.IsNaN(muestra) ? "" : " (la alineada daba " + F(muestra) + ": no se usa con el spot quieto)"), false))
                || Probar(muestra, "alineada", () => Que() + " alineada " + F(muestra) + " (" + ancla + "); " + estadoSpot + ", sin mediana todavia" + (congelado ? " · OJO: spot congelado, puede estar corrida" : ""), !vivo)
                || Probar(cruda, "cruda", () => "CRUDA (sin vela alineada): " + Que() + " " + F(cruda) + "; " + estadoSpot, !vivo);
            // 3.6.5: para la capa aditiva, la base de la recta de forwards de la cadena (la de la 2.0). 4.1.1: si la otra es dudosa (alineada o
            // cruda con el spot quieto) no sirve de control. 5.0: forwards tiene que pasar la cota del carry y quedar a FWD_TOL de la sincronizada
            // (de noche, ademas, con residuo <= 8 ticks: la foto de la noche trae el 0DTE vencido y la recta se tuerce 6-13 pts).
            if (!k.PorRazon && Capa3BaseSpx == BaseSpx3.Forwards && !double.IsNaN(k.BaseForwards) && k.BaseForwards > 0)
            {
                bool rueda = Histeresis3.EsRueda(ahora);
                double tol = rueda ? FWD_TOL_RUEDA : FWD_TOL_NOCHE;
                bool errOk = rueda || (!double.IsNaN(k.BaseForwardsErrTicks) && k.BaseForwardsErrTicks <= FWD_ERR_NOCHE_TICKS);
                bool cabe = ConversionCabe(k, k.BaseForwards, fut, ahora, out var pqF);
                bool sinOtra = double.IsNaN(mapa) || dudosa;
                bool cerca = sinOtra || Math.Abs(k.BaseForwards - mapa) <= tol;
                string err = double.IsNaN(k.BaseForwardsErrTicks) ? "" : "; residuo " + k.BaseForwardsErrTicks.ToString("0.#", inv) + " ticks";
                if (cabe && errOk && cerca)
                {
                    origen = "base forwards " + F(k.BaseForwards) + " (recta de forwards de la cadena, como la 2.0" + err + "); "
                           + (double.IsNaN(mapa) ? "ninguna otra pasa las cotas" : dudosa ? "la " + cual + " (" + F(mapa) + ") no sirve con el spot quieto" : "la " + cual + " daba " + F(mapa) + " (a " + Math.Abs(k.BaseForwards - mapa).ToString("0.00", inv) + ", tope " + tol.ToString("0", inv) + ")") + "; " + estadoSpot;
                    mapa = k.BaseForwards; dudosa = false;
                }
                else
                {
                    string pq = !cabe ? pqF : !errOk ? "de noche con " + (double.IsNaN(k.BaseForwardsErrTicks) ? "?" : k.BaseForwardsErrTicks.ToString("0.#", inv)) + " ticks de residuo (tope " + FWD_ERR_NOCHE_TICKS.ToString("0", inv) + ")"
                              : "a " + Math.Abs(k.BaseForwards - mapa).ToString("0.00", inv) + " de la " + cual + " (tope " + tol.ToString("0", inv) + (rueda ? " en la rueda" : " de noche") + ")";
                    descartes.Add("forwards " + F(k.BaseForwards) + " (" + pq + ")");
                }
            }
            string desc = descartes.Count == 0 ? "" : " · descartadas: " + string.Join("; ", descartes);
            if (dudosa || double.IsNaN(mapa))
            {
                cad.EscalaOrigen = "sin conversion confiable: " + (hay ? origen : "ninguna candidata pasa las cotas") + desc + " · no dibujo";
                return false;
            }
            if (k.PorRazon) { cad.Escala = mapa; cad.EscalaOrigen = origen + desc; cad.Base = 0; cad.BaseConfiable = false; }
            else { cad.Base = mapa; cad.BaseCruda = double.IsNaN(muestra) ? mapa : muestra; cad.BaseConfiable = true; cad.EscalaOrigen = origen + desc; }
            return true;
        }

        /// <summary>4.1.1: la conversion vigente de la familia para esta capa (5.0: SPX = base C7, SPY = razon C8; FuenteEstado.Libro "SPX"/"SPY"),
        /// de la foto del motor de este mismo indicador (nada externo). NaN si no hay foto reciente (menos de 10 min). 5.0: las cotas (las mismas
        /// para todas las candidatas) las aplica Mapear con ConversionCabe; la 4.1 tenia aca razon 30-60 y base (0, 2000], que en ES rechazaban SPY.</summary>
        private double ConversionDeLaFamilia(CapaCboe k, DateTime ahora, out string texto)
        {
            texto = "";
            try
            {
                var foto = FamiliaFoto;
                if (foto?.Fuentes == null || Math.Abs((ahora - foto.CalculadoUtc).TotalMinutes) > 10) return double.NaN;
                foreach (var fe in foto.Fuentes)
                {
                    if (fe == null || !string.Equals(fe.Libro, k.Nombre, StringComparison.OrdinalIgnoreCase)) continue;
                    double v = fe.ConvValor;
                    if (double.IsNaN(v) || double.IsInfinity(v) || v <= 0) return double.NaN;
                    texto = fe.Texto ?? "";
                    return v;
                }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "ConversionDeLaFamilia " + k.Nombre, e); }
            return double.NaN;
        }

        private void CargarBaseRuedaSiHaceFalta(CapaCboe k)
        {
            if (k.BaseCargada) return;
            k.BaseCargada = true;
            var inv = CultureInfo.InvariantCulture;
            // GANCHO-4.1 descargador: la 3.0 tambien leia la base de la rueda de la CLASICA (%APPDATA%\ATAS\PythiaGex\base-rueda-NQ.json, otro
            // programa). La 4.1 solo la suya (que escribe Mapear). 5.0: PythiaGex5\base-rueda-ES.json (SPX) / razon-rueda-SPY.json (SPY); nunca
            // los de la clasica, la 2.0 ni la 4.1. Sin archivo propio de menos de 24 h (del mismo contrato del grafico y, en SPY, de despues del
            // ultimo ex-dividendo), la primera noche la capa SPX va por la conversion de la familia o por forwards (si pasan las cotas).
            var rutas = new[] { (RutaBaseRueda(k), false) };
            foreach (var (ruta, deLaClasica) in rutas)
            {
                try
                {
                    if (!File.Exists(ruta)) continue;
                    using var doc = JsonDocument.Parse(File.ReadAllText(ruta));
                    double b = doc.RootElement.GetProperty("base").GetDouble();
                    var u = DateTime.ParseExact(doc.RootElement.GetProperty("utc").GetString() ?? "", "yyyy-MM-dd'T'HH:mm:ss", inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                    int m = doc.RootElement.TryGetProperty("muestras", out var mm) ? mm.GetInt32() : 0;
                    string cf = doc.RootElement.TryGetProperty("contrato", out var ce) && ce.ValueKind == JsonValueKind.String ? (ce.GetString() ?? "") : "";
                    if ((DateTime.UtcNow - u).TotalHours > 24) { Log("capa " + k.Nombre + ": base de la rueda de " + ruta + " es vieja (" + u.ToString("yyyy-MM-dd HH:mm", inv) + "Z): no se usa"); continue; }
                    string ca = ContratoCapas();
                    if (cf != "" && ca != "" && cf != ca) { Log("capa " + k.Nombre + ": la " + (k.PorRazon ? "razon" : "base") + " de " + ruta + " es del contrato " + cf + " y el grafico es " + ca + ": no se usa"); continue; }   // 5.0
                    if (k.PorRazon && u < ReinicioExDivSpyUtc(DateTime.UtcNow)) { Log("capa " + k.Nombre + ": la razon de " + ruta + " (" + u.ToString("yyyy-MM-dd HH:mm", inv) + "Z) es de antes del ultimo ex-dividendo: no se usa"); continue; }   // 5.0
                    k.BaseRueda = b; k.BaseRuedaUtc = u; k.BaseMuestras = m; k.BaseDeLaClasica = deLaClasica;
                    if (string.IsNullOrEmpty(k.Contrato) && cf != "") k.Contrato = cf;
                    Log("capa " + k.Nombre + ": " + (k.PorRazon ? "razon" : "base") + " de la rueda cargada " + b.ToString(k.PorRazon ? "0.0000" : "0.00", inv) + " (" + m + " muestras, " + u.ToString("HH:mm", inv) + "Z" + (cf == "" ? "" : ", contrato " + cf) + (deLaClasica ? ", archivo de la clasica, solo lectura" : "") + ")");
                    return;
                }
                catch (Exception e) { Registro.Excepcion(LOG, "CargarBaseRueda " + k.Nombre, e); }
            }
        }

        // ------------------------------------------------------------------ carga y cuenta (desde el latido, cada 60 s por capa)

        private void ActualizarCapas(string raiz, double fut, DateTime ahora)
        {
            // 5.0 (27-10-2026): las capas SPX/SPY son de un grafico de ES/MES (raiz "ES"). En otro grafico (p. ej. MNQ) no se bajan, no se cuentan,
            // no se guardan ni se dibujan; si el grafico dejo de ser ES (Raiz3Manual) se suelta lo que habia.
            if (raiz != "ES")
            {
                lock (_candado) foreach (var k0 in _capas) if (k0.L != null || k0.Estado != "el grafico no es ES/MES") { k0.L = null; k0.Estado = "el grafico no es ES/MES"; }
                return;
            }
            if (fut <= 0) return;
            foreach (var k in _capas)
            {
                try
                {
                    if (ModoDe(k) == CapaModo3.No) { lock (_candado) { k.L = null; } continue; }
                    if ((ahora - k.UltimaCargaUtc).TotalSeconds < 60) continue;
                    k.UltimaCargaUtc = ahora;
                    string json = null; string origen = "";
                    // GANCHO-4.1 descargador: primero lo que pase el integrador en memoria; si no, el archivo que escribe el descargador propio.
                    // Mismo control de frescura que la 3.0 (20 min desde que se escribio). Sin cadena fresca no hay capa (y lo dice el estado).
                    var ruta = Path.Combine(CarpetaCboePropia, "ultima-" + k.Archivo + ".json");
                    (string Json, DateTime EscritoUtc)? mem = null;
                    try { mem = FuenteCapaEnMemoria?.Invoke(k.Archivo); } catch (Exception e) { Registro.Excepcion(LOG, "FuenteCapaEnMemoria " + k.Nombre, e); }
                    if (mem.HasValue && !string.IsNullOrEmpty(mem.Value.Json) && (ahora - mem.Value.EscritoUtc).TotalMinutes <= 20) { json = mem.Value.Json; origen = "cboe-5.0 (memoria)"; }
                    else if (File.Exists(ruta) && (ahora - File.GetLastWriteTimeUtc(ruta)).TotalMinutes <= 20) { json = File.ReadAllText(ruta); origen = "cboe-5.0"; }
                    if (json == null)
                    {
                        lock (_candado) { k.Estado = "sin cadena propia fresca (" + ruta + ", menos de 20 min): la baja el descargador de la 5.0"; }
                        continue;
                    }
                    CargarCapa(k, json, origen, fut, ahora);
                }
                catch (Exception e) { Registro.Excepcion(LOG, "ActualizarCapas " + k.Nombre, e); }
            }
        }

        /// <summary>Parsea ultima-&lt;ticker&gt;.json (mismo formato que las lineas del archivo de la nube), lo lleva al futuro con la vela
        /// alineada al ultimo_trade y corre la cuenta. Si el sello (ts|ultimo_trade|spot) no cambio, solo repone el precio.</summary>
        private void CargarCapa(CapaCboe k, string json, string origen, double fut, DateTime ahora)
        {
            var inv = CultureInfo.InvariantCulture;
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            var c = r.GetProperty("cadena");
            string ts = c.GetProperty("ts").GetString() ?? "", ultimo = c.TryGetProperty("ultimo_trade", out var ut) ? (ut.GetString() ?? "") : "";
            double spot = c.TryGetProperty("spot_idx", out var sp) ? sp.GetDouble() : 0;
            string sello = ts + "|" + ultimo + "|" + spot.ToString("0.####", inv);
            DateTime generado = ahora;
            if (r.TryGetProperty("generado", out var g) && DateTimeOffset.TryParse(g.GetString(), inv, DateTimeStyles.AssumeUniversal, out var go)) generado = go.UtcDateTime;
            DateTime ultimoUtc = DateTime.MinValue;
            if (!string.IsNullOrEmpty(ultimo) && DateTime.TryParseExact(ultimo, "yyyy-MM-dd'T'HH:mm:ss", inv, DateTimeStyles.None, out var ultNy))
            {
                try { ultimoUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(ultNy, DateTimeKind.Unspecified), TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")); } catch { ultimoUtc = DateTime.MinValue; }
            }
            if (spot <= 0) { lock (_candado) { k.Estado = "cadena sin spot"; } return; }
            k.BaseForwards = r.TryGetProperty("base_cruda", out var bcr) && bcr.ValueKind == JsonValueKind.Number ? bcr.GetDouble() : double.NaN;   // 3.6.5
            k.BaseForwardsErrTicks = r.TryGetProperty("base_error_ticks", out var bce) && bce.ValueKind == JsonValueKind.Number ? bce.GetDouble() : double.NaN;

            // 3.0.7: dos velas del MNQ: la del ultimo trade de las opciones y la de la HORA DEL SPOT (generado - 16 min: el spot de cboe-local
            // llega con ese retraso; validado hoy en la rueda: misma dispersion que anclar al ultimo trade, MAD 0,0030 vs 0,0035 en la razon
            // de QQQ y 2,72 vs 2,72 en la base de NDX). Cual se usa lo decide Mapear segun el spot este vivo o congelado.
            double fUlt = ultimoUtc == DateTime.MinValue ? double.NaN : CierreEn(ultimoUtc);
            // 4.1: la hora del spot = sello de CBOE - 900 s (ver RETRASO_CBOE_S); sin sello legible, generado - 960 como la 3.0
            DateTime tSpot = DateTime.TryParseExact(ts, "yyyy-MM-dd HH:mm:ss", inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var tsUtc)
                ? tsUtc.AddSeconds(-RETRASO_CBOE_S) : generado.AddSeconds(-RETRASO_CBOE_GENERADO_S);
            double fSpot = CierreEn(tSpot);
            double fAlin = !double.IsNaN(fUlt) && fUlt > 0 ? fUlt : fSpot;
            string origenMapa = double.IsNaN(fAlin) || fAlin <= 0 ? "CRUDA (sin vela alineada)" : "vela alineada";
            if (double.IsNaN(fAlin) || fAlin <= 0) fAlin = fut;

            Feed.Cadena cad;
            if (sello == k.UltimoSello && k.Cadena != null) cad = k.Cadena;   // misma foto: no se vuelve a parsear
            else
            {
                // 5.0 (27-10-2026): el vencimiento YA VENCIDO fuera del libro de la capa. De noche CBOE sigue mandando el 0DTE del dia (bid/ask 0,
                // IV basura; medido 26-10 22:48 NY: 504 opciones). Vencido = fecha "f" anterior a hoy NY, u hoy despues de su hora: SPXW/SPY 16:00
                // NY (5.0.1: 13:00 NY en un cierre anticipado, CierresAnticipados); SPX mensual AM 09:30 NY si la linea lo marca ("am": true). Se le
                // pone dias = -1: el nucleo no lo cuenta en el perfil ni en el vencimiento "mas cercano" del horizonte Hoy (si no, a la noche el
                // horizonte quedaba en el 0DTE muerto y el perfil vacio).
                var dias = new List<double>(); var vencidas = new List<string>();
                var nyAhora = UtcANyCapa(ahora);
                var producto = k.PorRazon ? ProductoVenc.Spy : ProductoVenc.SpxPm;   // 5.0.1: la hora de vencimiento de un cierre anticipado
                foreach (var v in c.GetProperty("vencimientos").EnumerateArray())
                {
                    double d = v.GetProperty("dias").GetDouble();
                    if (VencimientoYaVencido(v, nyAhora, producto, out var fv)) { vencidas.Add(fv); d = -1; }
                    dias.Add(d);
                }
                var filas = new List<Feed.Fila>(); int filasVencidas = 0;
                foreach (var x in c.GetProperty("filas").EnumerateArray())
                {
                    var f = new double[8]; int i = 0;
                    foreach (var y in x.EnumerateArray()) { if (i < 8) f[i] = y.GetDouble(); i++; }
                    if (i < 8) continue;
                    int vi = (int)f[1]; if (vi < 0 || vi >= dias.Count) continue;
                    if (dias[vi] < 0) { filasVencidas++; continue; }
                    filas.Add(new Feed.Fila { K = f[0], V = vi, OiC = f[2], OiP = f[3], IvC = f[4], IvP = f[5], VolC = f[6], VolP = f[7] });
                }
                if (filasVencidas > 0) Log("capa " + k.Nombre + ": " + filasVencidas + " filas del vencimiento ya vencido (" + string.Join(", ", vencidas) + ", NY " + nyAhora.ToString("ddd HH:mm", inv) + ") fuera del libro");
                if (dias.Count == 0 || filas.Count == 0) { lock (_candado) { k.Estado = "cadena vacia" + (filasVencidas > 0 ? " (solo traia el vencimiento ya vencido)" : ""); } return; }
                cad = new Feed.Cadena
                {
                    Ts = ts, SpotIdx = spot, Dias = dias.ToArray(), Filas = filas, UltimoTrade = ultimo,
                    HorizonteCadena = dias.Max(), RecibidoUtc = ahora, GeneradoUtc = generado, EsFuturo = false, Fuente = k.Nombre + " CBOE",
                    PorRazon = k.PorRazon, Apalancamiento = 1.0,
                };
                k.UltimoSello = sello;
            }
            if (!Mapear(k, cad, spot, ts, generado, tSpot, fUlt, fSpot, fut, ahora))   // 3.0.7: razon o base siguiendo el reloj del spot (5.0: y su horario)
            {
                // 4.1.1: sin conversion confiable no hay capa (ni estela): se reintenta en el proximo latido de 60 s y se vuelve a parsear la cadena
                lock (_candado) { k.L = null; k.Estado = cad.EscalaOrigen; }
                k.UltimoSello = "";
                if ((ahora - _ultimoAvisoSinConv).TotalSeconds >= 60) { _ultimoAvisoSinConv = ahora; Log("capa " + k.Nombre + ": " + cad.EscalaOrigen); }
                return;
            }

            var a = k.Nucleo.A; var s = _nucleo.A;   // la misma cuenta y los mismos ajustes que la primaria
            // 5.0: el radio de dominantes es el del libro ES (RadioDominantes("ES") = 25 pts, AplicarAjustes), en puntos del grafico
            a.Horizonte = s.Horizonte; a.CuantasDominantes = Math.Max(2, Math.Min(3, Capa3Cuantas)); a.RadioDominantesPct = s.RadioDominantesPct; a.RadioDominantesMaxPts = s.RadioDominantesMaxPts;
            a.EmpatePct = s.EmpatePct; a.DominantesDeNoche = s.DominantesDeNoche; a.ZeroInterpolado = s.ZeroInterpolado;
            // 3.0.6: las capas eligen con su propia regla (default Clasica: una por lado + centroide 12, como las capas de la clasica)
            a.UnaPorLado = Capa3Regla == CapaRegla3.Clasica || (Capa3Regla == CapaRegla3.ComoPrimaria && s.UnaPorLado);
            a.Centroide = Capa3Regla == CapaRegla3.Clasica || (Capa3Regla == CapaRegla3.ComoPrimaria && s.Centroide);
            a.RadioCentroidePts = 12.0;   // 5.0: queda: el 12 nacio para SPX (GammaHoyNucleo.cs: "~2 strikes de SPX a cada lado"; la clasica usa 12 en ES)
            var L = k.Nucleo.Calcular(cad, fut, ahora);
            if (L != null && L.Doms != null && L.Doms.Count > 1) L.Doms = L.Doms.OrderBy(d => Math.Abs(d.Fut - fut)).ToList();   // 3.0.9: D1 = la mas CERCANA al precio (el nucleo devuelve [arriba, abajo])
            lock (_candado)
            {
                k.Cadena = cad; k.L = (L != null && !L.SinBase) ? L : null; k.GeneradoUtc = generado; k.UltimoTradeUtc = ultimoUtc;
                k.Escala = k.PorRazon ? cad.Escala : double.NaN; k.Base = k.PorRazon ? double.NaN : cad.Base; k.Origen = origen + ", " + origenMapa;
                k.Estado = k.L == null ? "sin cuenta (" + (L == null ? "cadena no sirve" : "sin base") + ")" : "ok";
            }
            if (k.L != null)
            {
                if (Guardar3Estela) GuardarEstela(k.Nombre, ahora, k.L);
                if ((ahora - _ultimoAuditCapas).TotalSeconds >= 60)
                    Log("capa " + k.Nombre + ": " + origen + " generado hace " + (ahora - generado).TotalMinutes.ToString("0", inv) + " min, ultimo trade hace "
                        + (ultimoUtc == DateTime.MinValue ? "?" : (ahora - ultimoUtc).TotalMinutes.ToString("0", inv)) + " min, " + (k.PorRazon ? "razon " + cad.Escala.ToString("0.0000", inv) : "base " + cad.Base.ToString("0.00", inv))
                        + " (" + cad.EscalaOrigen + "), strikes " + k.L.Perfil.Count + ", doms " + string.Join("/", k.L.Doms.Select(d => d.Fut.ToString("0.00", inv))) + ", zero " + k.L.ZeroVol.ToString("0.00", inv));
            }
            if (k == _capas[_capas.Count - 1] && (ahora - _ultimoAuditCapas).TotalSeconds >= 60) _ultimoAuditCapas = ahora;
        }
        private DateTime _ultimoAuditCapas = DateTime.MinValue;
        private DateTime _ultimoAvisoSinConv = DateTime.MinValue;

        // ------------------------------------------------------------------ la estela de cada capa por vela cerrada, y su memoria

        /// <summary>Desde RegistrarVelaCerrada: anota D1/D2/zero de cada capa viva para esa vela (si su dato tiene menos de 25 min).</summary>
        private void RegistrarCapasVela(DateTime tVela)
        {
            try
            {
                var ahora = DateTime.UtcNow;
                lock (_candado)
                {
                    foreach (var k in _capas)
                    {
                        if (k.L == null || ModoDe(k) == CapaModo3.No) continue;
                        if ((ahora - k.GeneradoUtc).TotalSeconds > VIDA_CAPA_S) continue;
                        if (!_estelaCapas.TryGetValue(k.Nombre, out var d)) { d = new Dictionary<DateTime, double[]>(); _estelaCapas[k.Nombre] = d; }
                        var cer = Cerco(CrucesPerfil(k.L), k.L.Futuro);   // 3.6.8
                        d[tVela] = new[] { k.L.Doms.Count > 0 ? k.L.Doms[0].Fut : double.NaN, k.L.Doms.Count > 1 ? k.L.Doms[1].Fut : double.NaN, k.L.ZeroVol, k.PorRazon ? double.NaN : k.L.Base, cer.Techo, cer.Piso };   // [3] = base usada (3.6.6); [4]/[5] = techo/piso del zero (3.6.8)
                        AnotarMajorOiCapa(k.Nombre, tVela, k.L.MpOi, k.L.MnOi, true);   // 3.6.2
                        if (d.Count > 6000) foreach (var kk in d.Keys.OrderBy(x => x).Take(d.Count - 5000).ToList()) d.Remove(kk);
                    }
                }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "RegistrarCapasVela", e); }
        }

        /// <summary>Al arrancar (desde SembrarMemoria): la estela guardada de cada capa vuelve a su vela, con vida de 25 min. 5.0: solo en un
        /// grafico de ES/MES (en otro las estelas de SPX/SPY, en precio de ES, no tienen donde ir).</summary>
        private void SembrarMemoriaCapas(List<VelaMem> velas, List<DateTime> dias)
        {
            string raiz = ""; try { raiz = Raiz(); } catch { }
            if (raiz != "ES") { Log("memoria capas: el grafico no es ES/MES (raiz '" + raiz + "'): no siembro SPX/SPY"); return; }
            foreach (var k in _capas)
            {
                try
                {
                    var puntos = LeerEstelaGuardada(k.Nombre, dias);
                    if (puntos.Count == 0) continue;
                    puntos.Sort((x, y) => x.T.CompareTo(y.T));
                    var tiempos = puntos.Select(p => p.T).ToList();
                    int n = 0;
                    if (!k.PorRazon) CargarBaseRuedaSiHaceFalta(k);   // 3.6.6: para saber con que base se guardaron los puntos viejos (sin 'bs')
                    lock (_candado)
                    {
                        if (!_estelaCapas.TryGetValue(k.Nombre, out var d)) { d = new Dictionary<DateTime, double[]>(); _estelaCapas[k.Nombre] = d; }
                        foreach (var v in velas)
                        {
                            if (d.ContainsKey(v.TOpen)) continue;
                            int i = tiempos.BinarySearch(v.TClose); if (i < 0) i = ~i; i--;
                            if (i < 0 || (v.TClose - tiempos[i]).TotalSeconds > VIDA_CAPA_S) continue;
                            var p = puntos[i];
                            double bsP = !double.IsNaN(p.Bs) && p.Bs > 0 ? p.Bs : (!k.PorRazon && !double.IsNaN(k.BaseRueda) && p.T >= k.BaseRuedaUtc ? k.BaseRueda : double.NaN);   // 3.6.6
                            d[v.TOpen] = new[] { p.D1, p.D2, p.Zero, k.PorRazon ? double.NaN : bsP }; n++;
                            AnotarMajorOiCapa(k.Nombre, v.TOpen, p.MpOi, p.MnOi, false);   // 3.6.2: los majors guardados en la estela
                        }
                    }
                    Log("memoria capa " + k.Nombre + ": " + puntos.Count + " puntos de la estela guardada -> " + n + " velas sembradas");
                }
                catch (Exception e) { Registro.Excepcion(LOG, "SembrarMemoriaCapas " + k.Nombre, e); }
            }
        }

        // ------------------------------------------------------------------ dibujo

        private sealed class ExtraRotulos
        {
            public List<(string Nombre, double P, Color C, string Sufijo)> Rotulos = new List<(string, double, Color, string)>();
            public List<(string Sigla, double P)> Fusion = new List<(string, double)>();
            public int TopeExtra;
        }

        /// <summary>Estela de guiones de cada capa en modo Propia (mas finos y mas tenues que los del libro ES) y la lista de rotulos extra.
        /// Devuelve tambien las coincidencias para la fusion con D1/D2 del libro ES. 5.0: capas SPX y SPY; todo en precio del grafico (MES).</summary>
        private ExtraRotulos PintarCapas(RenderContext g, Func<int, int> XBar, Rectangle area, int xr, int piso, int desde, int hasta,
                                         List<(int B, DateTime T)> horas, double fut, double radioDib, CultureInfo es, Func<double, int> Y, Func<int, bool> EnPantalla)
        {
            var ex = new ExtraRotulos();
            var ahora = DateTime.UtcNow;
            var inv = CultureInfo.InvariantCulture;
            foreach (var k in _capas)
            {
                var modo = ModoDe(k);
                if (modo == CapaModo3.No) continue;
                GammaHoyNucleo.Lectura L; Dictionary<int, double[]> est = new Dictionary<int, double[]>(); DateTime gen, ult;
                lock (_candado)
                {
                    L = k.L; gen = k.GeneradoUtc; ult = k.UltimoTradeUtc;
                    if (_estelaCapas.TryGetValue(k.Nombre, out var d)) foreach (var (b, t) in horas) if (d.TryGetValue(t, out var e0)) est[b] = e0;
                }
                double fus = (double)Math.Max(0.25m, Capa3FusionPts);
                int cuantas = Math.Max(2, Math.Min(3, Capa3Cuantas));
                if (L != null)
                    for (int i = 0; i < Math.Min(cuantas, L.Doms.Count); i++) if (L.Doms[i].Fut > 0) ex.Fusion.Add((k.Nombre, L.Doms[i].Fut));
                if (modo != CapaModo3.Propia) continue;

                // la estela de la capa: guiones de 2 px en su color (3.0.5; antes 1 px tenue y el operador no la veia); la sesion
                // anterior va atenuada en vez de oculta
                if (VerEstelaEf && hasta >= desde && est.Count > 0)
                {
                    int bw = 5;
                    { int xa = XBar(desde), xb = XBar(hasta); if (hasta > desde && xa != int.MinValue && xb != int.MinValue) bw = Math.Max(3, (xb - xa) / Math.Max(1, hasta - desde)); }
                    int w = Math.Max(1, bw - 1);
                    int ultima = CurrentBar - 1;
                    int inicio = Estela3VelasAtras > 0 ? Math.Max(0, ultima - Estela3VelasAtras) : _barInicioSesion;
                    double baseAhora = !k.PorRazon && L != null && L.Base != 0 ? L.Base : double.NaN;   // 3.6.6: la estela se corre a la base de ahora
                    var horaDe = new Dictionary<int, DateTime>(); foreach (var (hb, ht) in horas) horaDe[hb] = ht;   // 3.6.8
                    foreach (var kv in est)
                    {
                        int b = kv.Key; if (b < desde || b > hasta) continue;
                        bool previa = b < inicio;
                        double corr = kv.Value.Length > 3 && !double.IsNaN(kv.Value[3]) && !double.IsNaN(baseAhora) ? baseAhora - kv.Value[3] : 0;
                        if (previa && !Estela3SesionAnterior) continue;
                        int x = XBar(b); if (x == int.MinValue) continue;
                        bool cerco = CercoCapa(k.Nombre) && kv.Value.Length > 5;   // 3.6.8
                        bool congeladaB = CercoCapa(k.Nombre) && ult != DateTime.MinValue && horaDe.TryGetValue(b, out var tB) && tB >= ult;   // 3.6.8: la reconstruye el lazo de abajo
                        for (int i0 = 0; i0 < 6 && i0 < kv.Value.Length; i0++)
                        {
                            if (i0 == 3) continue;                                     // [3] = base, no es un nivel
                            if (i0 == 2 && (!Capa3ZeroRombos || congeladaB || (cerco && (!double.IsNaN(kv.Value[4]) || !double.IsNaN(kv.Value[5]))))) continue;   // con techo/piso no hace falta el mas cercano
                            if (i0 >= 4 && congeladaB) continue;
                            if (i0 >= 4 && (!cerco || !Capa3ZeroRombos)) continue;
                            int i = i0 >= 4 ? 2 : i0;                                  // techo y piso se dibujan como el zero
                            double p = kv.Value[i0]; if (double.IsNaN(p) || p <= 0) continue;   // [2] = zero de la capa (3.1.2, solo con Capa3Zero)
                            p += corr;   // 3.6.6
                            if (i < 2 ? !RayaCapa(k.Nombre, i) : !RayaCapaZero(k.Nombre)) continue;   // 3.5.0: una por una
                            int y = Y(p); if (!EnPantalla(y)) continue;
                            double radioCapa = Capa3RadioDibujoPtsEs > 0 ? (double)Capa3RadioDibujoPtsEs : radioDib;
                            int alfa = Math.Abs(p - fut) > radioCapa ? 60 : (b >= ultima - 30 ? 230 : 160);
                            if (previa) alfa = alfa * 45 / 100;
                            if (i == 2 && Zero3Estilo == ZeroEstilo3.RomboVerde)
                            {
                                int rz = Math.Max(2, Math.Min(4, w / 2 + 1));   // 3.3.1: el zero de la capa como rombo, como la fila de la 2.0
                                if (EnfasisCapaZero(k.Nombre)) { rz = Math.Max(3, Math.Min(6, w / 2 + 2)); alfa = previa ? 140 : 255; }   // 3.5.0: enfasis
                                g.FillPolygon(Color.FromArgb(AlfaAtravesada(alfa, p, b), k.Color), new[] { new Point(x, y - rz), new Point(x + rz, y), new Point(x, y + rz), new Point(x - rz, y) });
                                continue;
                            }
                            g.FillRectangle(Color.FromArgb(AlfaAtravesada(alfa, p, b), k.Color), new Rectangle(x - w / 2, i == 2 ? y : y - 1, w, i == 2 ? 1 : 2));
                        }
                    }
                }
                // 3.6.8: con la cadena de CBOE CONGELADA (velas posteriores al ultimo trade de opciones) los cruces no cambian: el techo y el piso de
                // cada vela salen de la lista de ahora contra el cierre de esa vela. Es una reconstruccion valida solo mientras la cadena siga congelada.
                if (VerEstelaEf && CercoCapa(k.Nombre) && Capa3ZeroRombos && RayaCapaZero(k.Nombre) && L != null && ult != DateTime.MinValue)
                {
                    int bw2 = 5;
                    { int xa = XBar(desde), xb = XBar(hasta); if (hasta > desde && xa != int.MinValue && xb != int.MinValue) bw2 = Math.Max(3, (xb - xa) / Math.Max(1, hasta - desde)); }
                    int w2 = Math.Max(1, bw2 - 1), ultima2 = CurrentBar - 1;
                    var crucesTodos = CrucesPerfil(L);
                    int rz = Math.Max(2, Math.Min(4, w2 / 2 + 1)); if (EnfasisCapaZero(k.Nombre)) rz = Math.Max(3, Math.Min(6, w2 / 2 + 2));
                    foreach (var (b, tv) in horas)
                    {
                        if (b < desde || b > hasta || tv < ult) continue;
                        double cl; try { var cb = GetCandle(b); if (cb == null) continue; cl = (double)cb.Close; } catch { continue; }
                        var cer = Cerco(crucesTodos, cl);
                        int x = XBar(b); if (x == int.MinValue) continue;
                        foreach (var p in new[] { cer.Techo, cer.Piso })
                        {
                            if (double.IsNaN(p) || p <= 0) continue;
                            int y = Y(p); if (!EnPantalla(y)) continue;
                            int alfa = EnfasisCapaZero(k.Nombre) ? 255 : (b >= ultima2 - 30 ? 230 : 160);
                            g.FillPolygon(Color.FromArgb(AlfaAtravesada(alfa, p, b), k.Color), new[] { new Point(x, y - rz), new Point(x + rz, y), new Point(x, y + rz), new Point(x - rz, y) });
                        }
                    }
                }
                if (L == null) continue;
                // rotulos: "SPX D1 ▲ 7.745 +12 ·17m" (la edad REAL del dato, desde el ultimo trade de CBOE), solo si estan dentro del radio de dibujo
                string edad = ult == DateTime.MinValue ? "" : " ·" + ((int)(ahora - ult).TotalMinutes).ToString("0", es) + "m";
                int xUlt = XBar(Math.Max(0, CurrentBar - 1));
                for (int i = 0; i < Math.Min(cuantas, L.Doms.Count); i++)
                {
                    double p = L.Doms[i].Fut; if (p <= 0 || Math.Abs(p - fut) > radioDib) continue;
                    if (!RayaCapa(k.Nombre, i)) continue;   // 3.5.0: una por una
                    // si coincide con D1/D2 del libro ES, no se repite: el rotulo de ES ya lleva la sigla (fusion)
                    GammaHoyNucleo.Lectura Lnq; lock (_candado) { Lnq = _L; }
                    bool coincide = Lnq != null && Lnq.Doms.Any(dq => dq.Fut > 0 && Math.Abs(dq.Fut - p) <= fus);
                    if (coincide && !Rayas3Independientes) continue;   // 3.5.0: independientes = se dibuja aunque coincida con NQ
                    // 3.0.5: el nivel ACTUAL de la capa se ve como linea fina desde la ultima vela hasta el eje (como en la clasica)
                    if (Rayas3Largas && Capa3LineaActual && xUlt != int.MinValue && Math.Abs(p - fut) <= (Capa3RadioDibujoPtsEs > 0 ? (double)Capa3RadioDibujoPtsEs : radioDib))
                    {
                        int y = Y(p);
                        if (EnPantalla(y) && xr > xUlt) g.DrawLine(new RenderPen(Color.FromArgb(170, k.Color), 1f), Math.Max(area.Left, xUlt), y, xr, y);
                    }
                    ex.Rotulos.Add((k.Nombre + " D" + (i + 1) + " " + (p >= fut ? "▲" : "▼"), p, k.Color, edad));
                    ex.TopeExtra++;
                }
                if (Capa3ZeroRombos && RayaCapaZero(k.Nombre) && CercoCapa(k.Nombre))   // 3.6.8: techo y piso del zero
                {
                    var cer = Cerco(CrucesPerfil(L), fut);
                    if (!double.IsNaN(cer.Techo) && Math.Abs(cer.Techo - fut) <= radioDib) { ex.Rotulos.Add((k.Nombre + " 0Γ techo", cer.Techo, k.Color, edad)); ex.TopeExtra++; }
                    if (!double.IsNaN(cer.Piso) && Math.Abs(cer.Piso - fut) <= radioDib) { ex.Rotulos.Add((k.Nombre + " 0Γ piso", cer.Piso, k.Color, edad)); ex.TopeExtra++; }
                }
                else if (Capa3ZeroRombos && RayaCapaZero(k.Nombre) && !double.IsNaN(L.ZeroVol) && L.ZeroVol > 0 && Math.Abs(L.ZeroVol - fut) <= radioDib)
                {
                    ex.Rotulos.Add((k.Nombre + " 0Γ", L.ZeroVol, k.Color, edad)); ex.TopeExtra++;
                    if (Rayas3Largas && EnfasisCapaZero(k.Nombre) && xUlt != int.MinValue) { int y = Y(L.ZeroVol); if (EnPantalla(y) && xr > xUlt) g.DrawLine(new RenderPen(Color.FromArgb(220, k.Color), 2f), Math.Max(area.Left, xUlt), y, xr, y); }   // 3.5.0
                }
            }
            return ex;
        }
    }
}
