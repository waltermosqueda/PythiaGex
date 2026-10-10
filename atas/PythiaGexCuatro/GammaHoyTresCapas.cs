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

namespace PythiaGexCuatro
{
    /// <summary>Como se muestra una capa de CBOE: No = ni se baja; Fusion = solo engrosa el rotulo de NQ cuando coincide (a
    /// &lt;= 'Capas: fusion (pts)'); Propia = su propia estela de guiones y sus rotulos, en su color.</summary>
    /// <summary>Con que regla eligen sus dominantes las capas (3.0.6). Clasica = lo que dibuja la clasica en sus capas.</summary>
    public enum CapaRegla3
    {
        [Description("Clasica: una por lado + empate 20 % + centroide 12 pts (lo que dibuja la clasica en sus capas)")] Clasica,
        [Description("Dos mas grandes, strike exacto (2.0.5)")] DosMasGrandes,
        [Description("Como la primaria: la regla elegida para NQ (sin histeresis)")] ComoPrimaria,
    }

    public enum CapaModo3 { No, Fusion, Propia }

    /// <summary>
    /// LAS CAPAS QQQ Y NDX DE GAMMA HOY 3.0 (06-10-2026, pedido del operador: "habilitame igualmente el QQQ y el NDX y que yo
    /// pueda juzgar o elegir desactivarlos").
    ///
    /// Lo medido antes de habilitarlas (laboratorio/tres, DISENO, rueda, MNQ m2, contra placebo): NQ vivo +8,8 pp (clasica) /
    /// +7,4 (2.0.5); NDX por CBOE -2,2 / -1,0; QQQ por CBOE -1,2 / -0,8; combinaciones no le ganan a NQ solo. Por eso la
    /// PRIMARIA sigue siendo NQ vivo y estas dos son CAPAS que el operador prende o apaga. Vienen prendidas (modo Propia) porque
    /// lo pidio asi para juzgarlas a la vista; el default limpio del pliego seria No.
    ///
    /// De donde sale el dato (4.1): la ultima cadena de CBOE que baja LA PROPIA 4.0 (descargador B2) a %APPDATA%\ATAS\PythiaGex4\cboe\ultima-&lt;QQQ|NQ&gt;.json
    /// (NQ = NDX, cada 75 s en la rueda), o la que el integrador pase en memoria (FuenteCapaEnMemoria). Sin cboe_local.py y sin la nube: si no
    /// hay cadena propia de menos de 20 min, la capa no existe y el estado lo dice. (La 3.0 leia PythiaGex\cboe-local y, de respaldo, GitHub.)
    /// CBOE llega 15 minutos tarde (medido: 902 s): el rotulo de cada nivel lleva la edad REAL del dato (desde ultimo_trade).
    /// La cuenta es la MISMA (GammaHoyNucleo, mismos ajustes que la primaria). Al futuro: QQQ por RAZON (vela del MNQ abierta a la hora
    /// del ultimo_trade / spot del ETF: la razon 'alineada', estable; la del AUDIT de la clasica saltaba 12-28 pts) y NDX por BASE
    /// aditiva (vela alineada - spot). Sin vela alineada a menos de 30 min se usa el precio actual y se dice ("cruda").
    /// Cada capa escribe su estela en PythiaGex4\estela\estela-&lt;QQQ|NDX&gt;-&lt;dia&gt;.jsonl (mismo formato) para que el laboratorio la juzgue
    /// al lado de la de NQ, y al arrancar se vuelve a sembrar desde ese archivo (vida 25 min, como el juez para CBOE).
    /// </summary>
    public partial class FamiliaCuatro
    {
        [Display(Name = "Capa QQQ (CBOE, 15 min tarde, por razon NQ/QQQ)", GroupName = "9.3 3.0 · Pantalla", Order = 1060,
                 Description = "No: no se baja ni se dibuja. Fusion: cuando un nivel de QQQ coincide a 'fusion (pts)' con D1/D2 de NQ, el rotulo de NQ suma la sigla. Propia: estela de guiones azules por vela y rotulos QQQ D1/D2 con la edad del dato. Medido en el laboratorio (diseño): QQQ por CBOE no le gana al placebo; esta capa es para juzgarla a la vista.")]
        public CapaModo3 Capa3QQQ { get; set; } = CapaModo3.Propia;

        [Display(Name = "Capa NDX (CBOE, 15 min tarde, por base aditiva)", GroupName = "9.3 3.0 · Pantalla", Order = 1061,
                 Description = "Igual que QQQ pero con el libro de NDX (cadena 'NQ' de la nube) llevado a MNQ sumando la base medida con la vela alineada. Gris. Medido en diseño: -2,2 pp contra placebo.")]
        public CapaModo3 Capa3NDX { get; set; } = CapaModo3.Propia;

        [Display(Name = "Capas: fusion con NQ (pts)", GroupName = "9.3 3.0 · Pantalla", Order = 1062,
                 Description = "Distancia maxima para que un nivel de capa 'coincida' con D1/D2 de NQ y le sume su sigla al rotulo (NQ·QQQ D1). Vale en modo Fusion y tambien en Propia.")]
        [Range(0.25, 50)]
        public decimal Capa3FusionPts { get; set; } = 5m;

        [Display(Name = "↳ capas 0Γ▸", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 74, Description = "Necesita la llave '3.0 capas▸' prendida. Sub-llave: sin ella no se dibuja ningun 0Γ de las capas ('↳ NDX 0Γ', '↳ NDX 0Γ enf.', '↳ NDX techo/piso', '↳ QQQ 0Γ' y '↳ QQQ 0Γ enf.', abajo). 3.3.1 (pedido 07-10): el zero gamma de NDX y QQQ como rombos por vela del color de la capa, mas su rotulo. Es la fila de 31485 de la 2.0 (misma formula: cruce de signo mas cercano al precio; la base de NDX aca es la mediana de la rueda, la 2.0 usa la cruda de ticks, ~3,6 pts). Con GuionGris queda la estela de 1 px.")]
        public bool Capa3ZeroRombos { get; set; } = true;

        [Display(Name = "Capas: tamaño de letra (% del de NQ)", GroupName = "9.3 3.0 · Pantalla", Order = 1064,
                 Description = "Los rotulos de QQQ y NDX van con letra mas chica que los de NQ (pedido 06-10). 100 = igual tamaño.")]
        [Range(50, 100)]
        public int Capa3LetraPct { get; set; } = 75;

        [Display(Name = "Capas: cuantas dominantes (2 o 3)", GroupName = "9.3 3.0 · Pantalla", Order = 1065,
                 Description = "La clasica dibuja tres por capa (D1, D2, D3). 3.0.5: default 3 por pedido del operador (06-10). La estela guardada sigue con D1/D2.")]
        [Range(2, 3)]
        public int Capa3Cuantas { get; set; } = 3;

        [Display(Name = "↳ linea al eje", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 80, Description = "Necesita la llave '3.0 capas▸' prendida. Solo cambia algo con '↳ rayas largas' prendida (por defecto apagada). Cada dominante de capa dentro del radio de dibujo lleva una linea fina de 1 px desde la ultima vela hasta el eje, en su color (3.0.5). Sin esto solo se ve el chip.")]
        public bool Capa3LineaActual { get; set; } = true;

        [Display(Name = "Capas: regla de dominantes", GroupName = "9.3 3.0 · Pantalla", Order = 1067,
                 Description = "3.0.6: Clasica = una por lado + empate 20 % + centroide 12 pts, lo que dibuja la clasica en sus capas (el 06-10 puso NDX D1 en 31.511 y la zona se respeto). ComoPrimaria = la regla de NQ sin histeresis.")]
        public CapaRegla3 Capa3Regla { get; set; } = CapaRegla3.Clasica;

        [Display(Name = "Capas: radio de dibujo (pts, 0 = el de NQ)", GroupName = "9.3 3.0 · Pantalla", Order = 1068,
                 Description = "3.0.7: los guiones y la linea de una capa se dibujan solo a esta distancia del precio (60 por defecto); mas lejos queda el rotulo con su precio y la estela muy tenue. Los niveles lejanos de las capas son reales (p. ej. el muro de calls 762 de QQQ, +83 pts) pero el laboratorio midio que rara vez se tocan.")]
        [Range(0, 500)]
        public decimal Capa3RadioDibujoPts { get; set; } = 60m;

        [Display(Name = "Base de NDX: de donde sale", GroupName = "9.3 3.0 · Pantalla", Order = 1095,
                 Description = "3.6.5 (08-10). Forwards = la base que mide el bajador de CBOE con la recta de forwards de la misma cadena (contado + carry; la que usa la 2.0: 238,36 esa noche). Rueda = la mediana de 24 muestras vela-contra-spot con el atraso de 16 min (248,15 esa noche, fuera de rango). Medido al cierre del 07-10 (futuro a las 16:00:00 NY contra el NDX congelado): 236-242.")]
        public BaseNdx3 Capa3BaseNdx { get; set; } = BaseNdx3.Forwards;

        private sealed class CapaCboe
        {
            public string Nombre;            // QQQ / NDX (lo que se dibuja y el sufijo de su estela)
            public string Archivo;           // QQQ / NQ (como lo archiva cboe_local.py y la nube)
            public bool PorRazon;            // QQQ: K x razon; NDX: K + base
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
            public double BaseRueda = double.NaN;
            public DateTime BaseRuedaUtc = DateTime.MinValue;
            public int BaseMuestras;
            public string UltimoTsBase = "";
            public bool BaseCargada, BaseDeLaClasica;
            public List<(DateTime Gen, double Spot)> SpotHist = new List<(DateTime, double)>();   // 3.0.7: para saber si el spot esta vivo o congelado
            public double BaseForwards = double.NaN, BaseForwardsErrTicks = double.NaN;   // 3.6.5: base_cruda / base_error_ticks del archivo de la cadena
        }

        private readonly List<CapaCboe> _capas = new List<CapaCboe>
        {
            new CapaCboe { Nombre = "QQQ", Archivo = "QQQ", PorRazon = true,  Color = Color.FromArgb(130, 177, 255) },  // celeste (3.0.5: el #2962ff de la referencia no se veia sobre el fondo oscuro)
            new CapaCboe { Nombre = "NDX", Archivo = "NQ",  PorRazon = false, Color = Color.FromArgb(38, 198, 218) },   // turquesa (3.0.5: el gris 158 de la clasica se perdia; el operador no la veia)
        };
        private readonly Dictionary<string, Dictionary<DateTime, double[]>> _estelaCapas = new Dictionary<string, Dictionary<DateTime, double[]>>(StringComparer.OrdinalIgnoreCase);
        private const double VIDA_CAPA_S = 25 * 60;       // sin cadena fresca el nivel no existe (CBOE: generado + 25 min, como el juez)
        // GANCHO-4.1 descargador: la 3.0 leia %APPDATA%\ATAS\PythiaGex\cboe-local\ultima-<QQQ|NQ>.json (lo escribe cboe_local.py, un programa
        // EXTERNO) y, si tenia mas de 20 min, la nube (GitHub raw). La 4.1 lee SOLO lo que baja ella misma: el descargador propio (B2, IFuenteCboe)
        // escribe %APPDATA%\ATAS\PythiaGex4\cboe\ultima-<NQ|QQQ|TQQQ>.json con la MISMA linea flaca de archivar_cadena.py (NQ = la cadena de _NDX).
        // Si el integrador prefiere pasarla en memoria (sin el archivo), asigna FuenteCapaEnMemoria: (Archivo "QQQ"|"NQ") -> (json, escrito UTC)
        // y aca se usa eso; null o sin dato = el archivo propio. Nada mas: ni cboe-local, ni nube, ni base de la clasica.
        private static string CarpetaCboePropia => Path.Combine(Registro.CarpetaDatos, "cboe");
        internal static Func<string, (string Json, DateTime EscritoUtc)?> FuenteCapaEnMemoria;

        private CapaModo3 ModoDe(CapaCboe k) => k.Nombre == "QQQ" ? Capa3QQQ : Capa3NDX;

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
        private bool CercoCapa(string nombre) => Raya3NdxZeroCerco && string.Equals(nombre, "NDX", StringComparison.OrdinalIgnoreCase);

        private static readonly TimeZoneInfo ZonaNyCapas = ZonaNyONull();
        private static TimeZoneInfo ZonaNyONull() { try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); } catch { return null; } }
        private string RutaBaseRueda(CapaCboe k) => Path.Combine(Registro.CarpetaDatos, (k.PorRazon ? "razon-rueda-" : "base-rueda-") + k.Archivo + ".json");

        // 4.1: el spot de CBOE es el de (sello de CBOE - 900 s) exactos (medido 08-10 sobre el crudo de TQQQ; ts - ultimo_trade: mediana 901 s en
        // NDX/QQQ/TQQQ). La 3.0 anclaba la vela en 'generado' - 960 (generado = la hora de la bajada de cboe_local, ~35-40 s despues del sello).
        // Con el descargador propio 'generado' es otra bajada: se ancla al SELLO (cadena.ts) - 900 y, solo si el sello no se puede leer, a
        // generado - 960 como la 3.0. OJO paridad: TRES_QQQ/TRES_NDX de la 4.1 no son identicos a los de la 3.0 por esto (la razon de QQQ se
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
        ///   spot CONGELADO o dato viejo -> la ultima mediana (persistida en PythiaGex4; 4.1: ya NO se lee la de la clasica).
        ///   4.1.1: sin mediana y con el spot QUIETO -> la conversion de la familia (C7 base de NDX / C8 razon de QQQ, sincronizadas y
        ///     persistidas). Medido 08-10 22:05 NY (ATAS recien abierto, sin mediana propia): la alineada comparaba el MNQ de las 20:48 con el
        ///     NDX congelado a las 16:00 y dio base 299.44 contra 241.03 de la familia y 241.83 de forwards (rayas "3.0 NDX" 58 pts arriba);
        ///     QQQ 41.4915 contra 41.4352 (~42 pts). Con el spot quieto la alineada YA NO se usa ni para descartar la base de forwards.
        ///   sin mediana, spot vivo -> la muestra alineada (como la 3.0); sin vela -> CRUDA (precio actual).
        ///   Si no queda nada confiable devuelve false y la capa no se calcula ni se dibuja (mejor sin raya que corrida).</summary>
        private bool Mapear(CapaCboe k, Feed.Cadena cad, double spot, string ts, DateTime generado, DateTime ultimoUtc, double fUlt, double fSpot, double fut, DateTime ahora)
        {
            var inv = CultureInfo.InvariantCulture;
            string F(double x) => k.PorRazon ? x.ToString("0.0000", inv) : x.ToString("0.00", inv);
            string Que() => k.PorRazon ? "razon" : "base";
            // 1) el reloj del spot
            if (k.SpotHist.Count == 0 || k.SpotHist[k.SpotHist.Count - 1].Gen != generado) { k.SpotHist.Add((generado, spot)); if (k.SpotHist.Count > 60) k.SpotHist.RemoveAt(0); }
            var recientes = k.SpotHist.Where(h => (generado - h.Gen).TotalMinutes <= 10).ToList();
            bool vivo = recientes.Any(h => Math.Abs(h.Spot - spot) > 1e-9);
            var iguales = k.SpotHist.Where(h => Math.Abs(h.Spot - spot) <= 1e-9).ToList();
            bool congelado = !vivo && iguales.Count >= 2 && (generado - iguales.Min(h => h.Gen)).TotalMinutes >= 6;
            bool fresca = (ahora - generado).TotalMinutes <= 20;
            // 2) la muestra de este latido: vela de la hora del spot; si no hay, la del ultimo trade
            double muestra = double.NaN; string ancla = "";
            if (!double.IsNaN(fSpot) && fSpot > 0) { muestra = k.PorRazon ? fSpot / spot : fSpot - spot; ancla = "vela en generado-16min"; }
            else if (!double.IsNaN(fUlt) && fUlt > 0) { muestra = k.PorRazon ? fUlt / spot : fUlt - spot; ancla = "vela al ultimo trade"; }
            CargarBaseRuedaSiHaceFalta(k);
            string selloMuestra = ts + "|" + spot.ToString("0.####", inv);
            if (vivo && fresca && !double.IsNaN(muestra) && selloMuestra != k.UltimoTsBase)
            {
                k.UltimoTsBase = selloMuestra;
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
                    try { File.WriteAllText(RutaBaseRueda(k), "{\"base\":" + med.ToString("0.######", inv) + ",\"utc\":\"" + ahora.ToString("yyyy-MM-dd'T'HH:mm:ss", inv) + "\",\"muestras\":" + buenas.Count + ",\"tipo\":\"" + Que() + "\"}"); }
                    catch (Exception e) { Registro.Excepcion(LOG, "mapa-rueda guardar " + k.Nombre, e); }
                }
                Log("capa " + k.Nombre + ": " + Que() + " muestra " + F(muestra) + " (" + ancla + ", spot " + spot.ToString("0.####", inv) + ") -> mediana " + F(med) + " de " + buenas.Count + "/" + k.BaseObs.Count + " · spot vivo");
            }
            bool medianaVale = !double.IsNaN(k.BaseRueda) && (ahora - k.BaseRuedaUtc).TotalHours <= 24;
            string estadoSpot = vivo ? "spot vivo" : congelado ? "spot congelado" : "spot sin cambio reciente";
            double mapa; string origen; bool dudosa = false;
            double conFam = ConversionDeLaFamilia(k, ahora, out var txtFam);
            if (medianaVale && (!vivo || !fresca || k.BaseMuestras >= 5))
            {
                mapa = k.BaseRueda;
                origen = Que() + " mediana " + F(mapa) + " (" + k.BaseMuestras + " muestras, " + k.BaseRuedaUtc.ToString("HH:mm", inv) + "Z" + (k.BaseDeLaClasica ? ", archivo de la clasica" : "") + "); " + estadoSpot
                       + (double.IsNaN(muestra) ? "" : ", alineada ahora " + F(muestra));
            }
            else if (!vivo && !double.IsNaN(conFam))
            {
                mapa = conFam;
                origen = Que() + " de la familia " + F(conFam) + " (" + txtFam + "); " + estadoSpot + ", sin mediana propia todavia"
                       + (double.IsNaN(muestra) ? "" : " (la alineada daba " + F(muestra) + ": no se usa con el spot quieto)");
            }
            else if (!double.IsNaN(muestra))
            {
                mapa = muestra; dudosa = !vivo;
                origen = Que() + " alineada " + F(muestra) + " (" + ancla + "); " + estadoSpot + ", sin mediana todavia" + (congelado ? " · OJO: spot congelado, puede estar corrida" : "");
            }
            else { mapa = k.PorRazon ? fut / spot : fut - spot; dudosa = !vivo; origen = "CRUDA (sin vela alineada): " + Que() + " " + F(mapa); }
            // 3.6.5: para NDX, la base de la recta de forwards de la cadena (la de la 2.0) si es razonable (a menos de 40 pts de la otra).
            // 4.1.1: si la otra es dudosa (alineada o cruda con el spot quieto) no sirve de control: va la de forwards.
            if (!k.PorRazon && Capa3BaseNdx == BaseNdx3.Forwards && !double.IsNaN(k.BaseForwards) && k.BaseForwards > 0 && (double.IsNaN(mapa) || dudosa || Math.Abs(k.BaseForwards - mapa) <= 40))
            {
                origen = "base forwards " + F(k.BaseForwards) + " (recta de forwards de la cadena, como la 2.0" + (double.IsNaN(k.BaseForwardsErrTicks) ? "" : "; residuo " + k.BaseForwardsErrTicks.ToString("0.#", inv) + " ticks") + "); " + (dudosa ? "la alineada (" + F(mapa) + ") no sirve con el spot quieto" : "la rueda daba " + F(mapa)) + "; " + estadoSpot;
                mapa = k.BaseForwards; dudosa = false;
            }
            if (dudosa || double.IsNaN(mapa))
            {
                cad.EscalaOrigen = "sin conversion confiable: " + origen + " · ni mediana propia ni conversion de la familia todavia: no dibujo";
                return false;
            }
            if (k.PorRazon) { cad.Escala = mapa; cad.EscalaOrigen = origen; cad.Base = 0; cad.BaseConfiable = false; }
            else { cad.Base = mapa; cad.BaseCruda = double.IsNaN(muestra) ? mapa : muestra; cad.BaseConfiable = true; cad.EscalaOrigen = origen; }
            return true;
        }

        /// <summary>4.1.1: la conversion vigente de la familia para esta capa (NDX: base C7; QQQ: razon C8), de la foto del motor de este mismo
        /// indicador (nada externo). NaN si no hay foto reciente (menos de 10 min) o el valor no es razonable.</summary>
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
                    if (double.IsNaN(v) || (k.PorRazon ? (v < 30 || v > 60) : (v <= 0 || v > 2000))) return double.NaN;
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
            // programa). La 4.1 solo la suya (PythiaGex4\base-rueda-NQ.json / razon-rueda-QQQ.json, que escribe Mapear). Sin archivo propio de
            // menos de 24 h, la primera noche la capa NDX va por la base de forwards de la cadena (Capa3BaseNdx = Forwards) o por la alineada.
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
                    if ((DateTime.UtcNow - u).TotalHours > 24) { Log("capa " + k.Nombre + ": base de la rueda de " + ruta + " es vieja (" + u.ToString("yyyy-MM-dd HH:mm", inv) + "Z): no se usa"); continue; }
                    k.BaseRueda = b; k.BaseRuedaUtc = u; k.BaseMuestras = m; k.BaseDeLaClasica = deLaClasica;
                    Log("capa " + k.Nombre + ": " + (k.PorRazon ? "razon" : "base") + " de la rueda cargada " + b.ToString(k.PorRazon ? "0.0000" : "0.00", inv) + " (" + m + " muestras, " + u.ToString("HH:mm", inv) + "Z" + (deLaClasica ? ", archivo de la clasica, solo lectura" : "") + ")");
                    return;
                }
                catch (Exception e) { Registro.Excepcion(LOG, "CargarBaseRueda " + k.Nombre, e); }
            }
        }

        // ------------------------------------------------------------------ carga y cuenta (desde el latido, cada 60 s por capa)

        private void ActualizarCapas(string raiz, double fut, DateTime ahora)
        {
            if (raiz != "NQ" || fut <= 0) return;   // las capas QQQ/NDX son para graficos de NQ/MNQ; en ES irian SPX/SPY (pendiente)
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
                    if (mem.HasValue && !string.IsNullOrEmpty(mem.Value.Json) && (ahora - mem.Value.EscritoUtc).TotalMinutes <= 20) { json = mem.Value.Json; origen = "cboe-4.0 (memoria)"; }
                    else if (File.Exists(ruta) && (ahora - File.GetLastWriteTimeUtc(ruta)).TotalMinutes <= 20) { json = File.ReadAllText(ruta); origen = "cboe-4.0"; }
                    if (json == null)
                    {
                        lock (_candado) { k.Estado = "sin cadena propia fresca (" + ruta + ", menos de 20 min): la baja el descargador de la 4.0"; }
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
                var dias = new List<double>();
                foreach (var v in c.GetProperty("vencimientos").EnumerateArray()) dias.Add(v.GetProperty("dias").GetDouble());
                var filas = new List<Feed.Fila>();
                foreach (var x in c.GetProperty("filas").EnumerateArray())
                {
                    var f = new double[8]; int i = 0;
                    foreach (var y in x.EnumerateArray()) { if (i < 8) f[i] = y.GetDouble(); i++; }
                    if (i < 8) continue;
                    int vi = (int)f[1]; if (vi < 0 || vi >= dias.Count) continue;
                    filas.Add(new Feed.Fila { K = f[0], V = vi, OiC = f[2], OiP = f[3], IvC = f[4], IvP = f[5], VolC = f[6], VolP = f[7] });
                }
                if (dias.Count == 0 || filas.Count == 0) { lock (_candado) { k.Estado = "cadena vacia"; } return; }
                cad = new Feed.Cadena
                {
                    Ts = ts, SpotIdx = spot, Dias = dias.ToArray(), Filas = filas, UltimoTrade = ultimo,
                    HorizonteCadena = dias.Max(), RecibidoUtc = ahora, GeneradoUtc = generado, EsFuturo = false, Fuente = k.Nombre + " CBOE",
                    PorRazon = k.PorRazon, Apalancamiento = 1.0,
                };
                k.UltimoSello = sello;
            }
            if (!Mapear(k, cad, spot, ts, generado, ultimoUtc, fUlt, fSpot, fut, ahora))   // 3.0.7: razon o base siguiendo el reloj del spot
            {
                // 4.1.1: sin conversion confiable no hay capa (ni estela): se reintenta en el proximo latido de 60 s y se vuelve a parsear la cadena
                lock (_candado) { k.L = null; k.Estado = cad.EscalaOrigen; }
                k.UltimoSello = "";
                if ((ahora - _ultimoAvisoSinConv).TotalSeconds >= 60) { _ultimoAvisoSinConv = ahora; Log("capa " + k.Nombre + ": " + cad.EscalaOrigen); }
                return;
            }

            var a = k.Nucleo.A; var s = _nucleo.A;   // la misma cuenta y los mismos ajustes que la primaria
            a.Horizonte = s.Horizonte; a.CuantasDominantes = Math.Max(2, Math.Min(3, Capa3Cuantas)); a.RadioDominantesPct = s.RadioDominantesPct; a.RadioDominantesMaxPts = s.RadioDominantesMaxPts;
            a.EmpatePct = s.EmpatePct; a.DominantesDeNoche = s.DominantesDeNoche; a.ZeroInterpolado = s.ZeroInterpolado;
            // 3.0.6: las capas eligen con su propia regla (default Clasica: una por lado + centroide 12, como las capas de la clasica)
            a.UnaPorLado = Capa3Regla == CapaRegla3.Clasica || (Capa3Regla == CapaRegla3.ComoPrimaria && s.UnaPorLado);
            a.Centroide = Capa3Regla == CapaRegla3.Clasica || (Capa3Regla == CapaRegla3.ComoPrimaria && s.Centroide);
            a.RadioCentroidePts = 12.0;
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

        /// <summary>Al arrancar (desde SembrarMemoria): la estela guardada de cada capa vuelve a su vela, con vida de 25 min.</summary>
        private void SembrarMemoriaCapas(List<VelaMem> velas, List<DateTime> dias)
        {
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

        /// <summary>Estela de guiones de cada capa en modo Propia (mas finos y mas tenues que los de NQ) y la lista de rotulos extra.
        /// Devuelve tambien las coincidencias para la fusion con D1/D2 de NQ.</summary>
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
                            double radioCapa = Capa3RadioDibujoPts > 0 ? (double)Capa3RadioDibujoPts : radioDib;
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
                // rotulos: "QQQ D1 ▲ 31.545 +12 ·17m" (la edad REAL del dato, desde el ultimo trade de CBOE), solo si estan dentro del radio de dibujo
                string edad = ult == DateTime.MinValue ? "" : " ·" + ((int)(ahora - ult).TotalMinutes).ToString("0", es) + "m";
                int xUlt = XBar(Math.Max(0, CurrentBar - 1));
                for (int i = 0; i < Math.Min(cuantas, L.Doms.Count); i++)
                {
                    double p = L.Doms[i].Fut; if (p <= 0 || Math.Abs(p - fut) > radioDib) continue;
                    if (!RayaCapa(k.Nombre, i)) continue;   // 3.5.0: una por una
                    // si coincide con D1/D2 de NQ, no se repite: el rotulo de NQ ya lleva la sigla (fusion)
                    GammaHoyNucleo.Lectura Lnq; lock (_candado) { Lnq = _L; }
                    bool coincide = Lnq != null && Lnq.Doms.Any(dq => dq.Fut > 0 && Math.Abs(dq.Fut - p) <= fus);
                    if (coincide && !Rayas3Independientes) continue;   // 3.5.0: independientes = se dibuja aunque coincida con NQ
                    // 3.0.5: el nivel ACTUAL de la capa se ve como linea fina desde la ultima vela hasta el eje (como en la clasica)
                    if (Rayas3Largas && Capa3LineaActual && xUlt != int.MinValue && Math.Abs(p - fut) <= (Capa3RadioDibujoPts > 0 ? (double)Capa3RadioDibujoPts : radioDib))
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
