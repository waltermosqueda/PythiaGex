using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGexTres
{
    /// <summary>Como se muestra una capa de CBOE: No = ni se baja; Fusion = solo engrosa el rotulo de NQ cuando coincide (a
    /// &lt;= 'Capas: fusion (pts)'); Propia = su propia estela de guiones y sus rotulos, en su color.</summary>
    public enum CapaModo3 { No, Fusion, Propia }

    /// <summary>
    /// LAS CAPAS QQQ Y NDX DE GAMMA HOY 3.0 (06-10-2026, pedido del operador: "habilitame igualmente el QQQ y el NDX y que yo
    /// pueda juzgar o elegir desactivarlos").
    ///
    /// Lo medido antes de habilitarlas (laboratorio/tres, DISENO, rueda, MNQ m2, contra placebo): NQ vivo +8,8 pp (clasica) /
    /// +7,4 (2.0.5); NDX por CBOE -2,2 / -1,0; QQQ por CBOE -1,2 / -0,8; combinaciones no le ganan a NQ solo. Por eso la
    /// PRIMARIA sigue siendo NQ vivo y estas dos son CAPAS que el operador prende o apaga.
    ///
    /// De donde sale el dato: la ultima cadena de CBOE que baja cboe_local.py a %APPDATA%\ATAS\PythiaGex\cboe-local\ultima-&lt;QQQ|NQ&gt;.json
    /// (NQ = NDX, cada 75 s); si tiene mas de 20 min, la de la nube (rama cadenas de GitHub), a lo sumo una bajada cada 5 min.
    /// CBOE llega 15 minutos tarde (medido: 902 s): el rotulo de cada nivel lleva la edad REAL del dato (desde ultimo_trade).
    ///
    /// 3.7.0 (auditoria 08-10, laboratorio/tres/auditoria_0810):
    ///   C5 la conversion al futuro es SINCRONIZADA (Conversion37.cs, la de backtest_familia.py): precio del MNQ en el segundo
    ///      (cadena.ts - 900 s) contra el spot vivo; NDX de dia mediana de 24, de noche la rueda entera con el decaimiento del carry
    ///      (243,07 la noche del 08-10 contra 248,15 / 238,36 de antes); QQQ mediana de 24 sin tope de 24 h. Estado persistido con el
    ///      contrato; si no hay, se siembra desde el archivo de cboe-local. Alarma si la base equivalente de QQQ y la de NDX difieren &gt; 2 pts.
    ///   C6 una cadena CONGELADA (ultimo trade &gt;= 15:59 NY) vale hasta las 13:30 UTC siguientes para la estela (antes 25 min).
    ///   D3 D1/D2 = una por lado en el STRIKE EXACTO (sin centroide 12, sin tercera), D2 prendida; cerco del zero de NDX sin islas (C3);
    ///      los cruces se rotulan 'cruce' (C8); cada marca se dibuja a &lt;= radio del cierre de SU vela (D1).
    /// 3.7.1: A6 D1/D2 = la regla de la 2.0 (dos de mayor |GEX vol|, D1 la mayor); A4 M± OI a &lt;= min(2 %·F, 100) sin la serie que cierra
    ///   en &lt; 30 min (Dibujo37.PrepararCapa); A2 la estela ya no se corre a la base de ahora; A1 la alarma sostenida 10 min y solo en log y
    ///   recuadro; cruces de NDX y QQQ y toda la capa QQQ APAGADOS por defecto (renombrados; QQQ se sigue contando para la alarma).
    /// </summary>
    public partial class GammaHoyTres
    {
        [Display(Name = "Capa QQQ (CBOE, 15 min tarde, por razon NQ/QQQ)", GroupName = "3. Pantalla", Order = 60,
                 Description = "No: no se baja ni se dibuja. Fusion: cuando un nivel de QQQ coincide a 'fusion (pts)' con D1/D2 de NQ, el rotulo de NQ suma la sigla. Propia: estela de guiones por vela y rotulos QQQ D1/D2 con la edad del dato. Medido en el laboratorio: QQQ por CBOE no le gana al placebo; esta capa es para juzgarla a la vista.")]
        public CapaModo3 Capa3QQQ { get; set; } = CapaModo3.Propia;

        [Display(Name = "Capa NDX (CBOE, 15 min tarde, por base aditiva)", GroupName = "3. Pantalla", Order = 61,
                 Description = "Igual que QQQ pero con el libro de NDX (cadena 'NQ' de cboe-local) llevado a MNQ sumando la base sincronizada (3.7.0). Turquesa.")]
        public CapaModo3 Capa3NDX { get; set; } = CapaModo3.Propia;

        [Display(Name = "Capas: fusion con NQ (pts)", GroupName = "3. Pantalla", Order = 62,
                 Description = "Distancia maxima para que un nivel de capa 'coincida' con D1/D2 de NQ y le sume su sigla al rotulo (NQ·QQQ D1). Solo con 'Todas independientes' apagado.")]
        [Range(0.25, 50)]
        public decimal Capa3FusionPts { get; set; } = 5m;

        [Display(Name = "Capas: cruce como rombos por vela", GroupName = "3. Pantalla", Order = 63,
                 Description = "3.3.1: el cruce de signo de NDX (techo y piso) y de QQQ (el mas cercano) como rombos por vela del color de la capa, mas su rotulo 'cruce' (3.7.0 C8). Sin islas desde la 3.7.0 (C3).")]
        public bool Capa3ZeroRombos { get; set; } = true;

        [Display(Name = "Capas: tamaño de letra (% del de NQ)", GroupName = "3. Pantalla", Order = 64,
                 Description = "Los rotulos de QQQ y NDX van con letra mas chica que los de NQ (pedido 06-10). 100 = igual tamaño.")]
        [Range(50, 100)]
        public int Capa3LetraPct { get; set; } = 75;

        [Display(Name = "Capas: linea del nivel actual hasta el eje", GroupName = "3. Pantalla", Order = 66,
                 Description = "Solo con 'Rayas largas' prendido (apagado por defecto desde la 3.5.5): linea fina de 1 px desde la ultima vela hasta el eje.")]
        public bool Capa3LineaActual { get; set; } = true;

        [Display(Name = "Base de NDX: de donde sale", GroupName = "3. Pantalla", Order = 95,
                 Description = "3.7.0 (C5): Sincronizada por defecto y nombre nuevo (Capa3BaseNdxB) para pisar el 'forwards' guardado en el .ws: muestra = MNQ en el segundo (cadena.ts - 900 s) menos el spot de NDX vivo de 09:35-15:59 NY; de dia mediana robusta de 24; de noche la rueda entera con el decaimiento del carry hasta el vencimiento del contrato (medido la noche del 08-10: 243,07). Forwards = base_cruda del bajador (la 3.6.5 y la 2.0: 238,36 esa noche; salta 2 pts de mediana entre bajadas, cod_06).")]
        public BaseNdx37 Capa3BaseNdxB { get; set; } = BaseNdx37.Sincronizada;

        private sealed class CapaCboe
        {
            public string Nombre;            // QQQ / NDX (lo que se dibuja y el sufijo de su estela)
            public string Archivo;           // QQQ / NQ (como lo archiva cboe_local.py y la nube)
            public bool PorRazon;            // QQQ: K x razon; NDX: K + base
            public Color Color;
            public GammaHoyNucleo Nucleo = new GammaHoyNucleo();
            public GammaHoyNucleo.Lectura L;
            public Feed.Cadena Cadena;
            public DateTime GeneradoUtc = DateTime.MinValue, UltimoTradeUtc = DateTime.MinValue, UltimaCargaUtc = DateTime.MinValue, UltimaNubeUtc = DateTime.MinValue;
            public DateTime TsUtc = DateTime.MinValue, VigenteHastaUtc = DateTime.MinValue;   // 3.7.0 C6
            public string Estado = "sin dato", Origen = "";
            public double Escala = double.NaN, Base = double.NaN, Spot = double.NaN;
            public string UltimoSello = "";
            public string NubeJson;          // la bajada de la nube, pendiente de parsear en el proximo latido
            public double BaseForwards = double.NaN, BaseForwardsErrTicks = double.NaN;   // 3.6.5: base_cruda / base_error_ticks del archivo de la cadena
            public double OiTotal = double.NaN;
            // 3.7.0 C5: la conversion sincronizada y la fecha del OI
            public ConversionCboe37 Conv;
            public FechaOiCboe37 FechaOi;
            public bool ConvCargada;
            public DateTime ConvGuardadaUtc = DateTime.MinValue;
            public string ConvTexto = "", Alarma = "";
            public double BaseSinCarry = double.NaN;   // 3.7.0: la base de NDX en uso SIN el decaimiento de la noche (para la alarma de la familia)
        }

        private readonly List<CapaCboe> _capas = new List<CapaCboe>
        {
            new CapaCboe { Nombre = "QQQ", Archivo = "QQQ", PorRazon = true,  Color = Color.FromArgb(130, 177, 255), Conv = new ConversionCboe37(true),  FechaOi = new FechaOiCboe37(new TimeSpan(5, 15, 0)) },  // celeste; OI medido 03:31 / 05:12 NY
            new CapaCboe { Nombre = "NDX", Archivo = "NQ",  PorRazon = false, Color = Color.FromArgb(38, 198, 218),  Conv = new ConversionCboe37(false), FechaOi = new FechaOiCboe37(new TimeSpan(2, 35, 0)) },  // turquesa; OI medido 02:31-02:34 NY
        };
        private readonly Dictionary<string, Dictionary<DateTime, double[]>> _estelaCapas = new Dictionary<string, Dictionary<DateTime, double[]>>(StringComparer.OrdinalIgnoreCase);
        private static readonly HttpClient _http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) { Timeout = TimeSpan.FromSeconds(20) };
        private const string NUBE = "https://raw.githubusercontent.com/waltermosqueda/PythiaGex/cadenas/ultima-{0}.json";
        private const double VIDA_CAPA_S = 25 * 60;       // sin cadena fresca el nivel no existe (CBOE: generado + 25 min, como el juez); congelada: C6
        private static readonly string CarpetaCboeLocal = Path.Combine(Registro.CarpetaAtas, "PythiaGex", "cboe-local");   // la escribe cboe_local.py; solo lectura
        private readonly ParesFamilia37 _pares = new ParesFamilia37();   // 3.7.0 C5: NDX/QQQ pareados, para la alarma
        private bool _paresCargados, _siembraArchivoLanzada;
        private DateTime _paresGuardadosUtc = DateTime.MinValue, _ultimoLogAlarma = DateTime.MinValue;
        private string _alarmaFamilia = "";
        private readonly AlarmaFamilia37 _alarma37 = new AlarmaFamilia37();   // 3.7.1 (A1): sostenida 10 min

        private CapaModo3 ModoDe(CapaCboe k) => k.Nombre == "QQQ" ? Capa3QQQ : Capa3NDX;
        private CapaCboe Capa(string nombre) => _capas.FirstOrDefault(k => string.Equals(k.Nombre, nombre, StringComparison.OrdinalIgnoreCase));

        /// <summary>3.6.8 / 3.7.0: los cruces de signo por volumen del perfil entero (strike por strike), en precio del futuro, SIN ISLAS (C3).</summary>
        private static List<double> CrucesPerfil(GammaHoyNucleo.Lectura L)
            => L?.Perfil == null ? new List<double>() : Seleccion37.CrucesPerfil(L.Perfil, true, true).Select(c => c.Z).ToList();
        private bool CercoCapa(string nombre) => Raya3NdxZeroCercoD && string.Equals(nombre, "NDX", StringComparison.OrdinalIgnoreCase);   // 3.7.3: apagado (renombrada)

        private string RutaConv(CapaCboe k) => Path.Combine(Registro.CarpetaDatos, "conv37-" + k.Archivo + ".json");
        private string RutaPares => Path.Combine(Registro.CarpetaDatos, "conv37-pares.json");

        // ------------------------------------------------------------------ C5: la conversion sincronizada

        private void CargarConvSiHaceFalta(CapaCboe k)
        {
            if (k.ConvCargada) return;
            k.ConvCargada = true;
            try
            {
                var r = RutaConv(k);
                if (File.Exists(r))
                {
                    bool ok = k.Conv.Cargar(File.ReadAllText(r), out var msg);
                    Log("capa " + k.Nombre + ": conversion sincronizada " + (ok ? "cargada de " + r + ": " + msg : "NO cargada (" + msg + ")"));
                }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "CargarConv " + k.Nombre, e); }
            if (!_paresCargados)
            {
                _paresCargados = true;
                try { if (File.Exists(RutaPares) && _pares.Cargar(File.ReadAllText(RutaPares))) { var rp = _pares.RPar(); Log("familia: pares NDX/QQQ cargados (R " + (double.IsNaN(rp.R) ? "-" : rp.R.ToString("0.0000", CultureInfo.InvariantCulture)) + ", " + rp.N + " pares)"); } } catch { }
            }
        }

        private void GuardarConv(CapaCboe k, DateTime ahora, bool forzar = false)
        {
            if (!forzar && (ahora - k.ConvGuardadaUtc).TotalSeconds < 60) return;
            k.ConvGuardadaUtc = ahora;
            try { Directory.CreateDirectory(Registro.CarpetaDatos); File.WriteAllText(RutaConv(k), k.Conv.Serializar(ahora)); } catch (Exception e) { Registro.Excepcion(LOG, "GuardarConv " + k.Nombre, e); }
            if ((ahora - _paresGuardadosUtc).TotalSeconds >= 60) { _paresGuardadosUtc = ahora; try { File.WriteAllText(RutaPares, _pares.Serializar()); } catch { } }
        }

        /// <summary>3.7.0 C5. Lleva la cadena al futuro con la conversion SINCRONIZADA (Conversion37) y deja el origen escrito. Devuelve false si
        /// no hay conversion (la capa no se dibuja: no se inventa una).</summary>
        private bool Mapear37(CapaCboe k, Feed.Cadena cad, double spot, DateTime tsUtc, DateTime generado, bool nueva, DateTime ahora)
        {
            var inv = CultureInfo.InvariantCulture;
            foreach (var kc in _capas) CargarConvSiHaceFalta(kc);   // las dos antes de decidir si hace falta sembrar desde el archivo
            string contrato = Trimestre37();
            DateTime venc = VencimientoGrafico37();
            if (nueva && tsUtc != DateTime.MinValue)
            {
                double precio = PrecioEn(tsUtc.AddSeconds(-ConversionCboe37.RETRASO_S));
                if (k.Conv.Observar(generado, tsUtc, spot, precio, contrato))
                {
                    k.FechaOi.Observar(generado, k.OiTotal);
                    if (k.PorRazon) _pares.Qqq(generado, tsUtc.AddSeconds(-ConversionCboe37.RETRASO_S), spot, k.Conv.UltimaViva);
                    else _pares.Ndx(generado, tsUtc.AddSeconds(-ConversionCboe37.RETRASO_S), spot, k.Conv.UltimaViva);
                    if (k.Conv.UltimaViva && !double.IsNaN(k.Conv.UltimaMuestra))
                        Log("capa " + k.Nombre + ": muestra sincronizada " + k.Conv.UltimaMuestra.ToString(k.PorRazon ? "0.0000" : "0.00", inv) + " (MNQ " + precio.ToString("0.##", inv) + " en " + tsUtc.AddSeconds(-ConversionCboe37.RETRASO_S).ToString("HH:mm:ss", inv) + "Z, spot " + spot.ToString("0.####", inv) + (k.PorRazon || k.Conv.UltimaEnHora ? "" : ", fuera de 09:35-15:59 NY: no entra") + ") · " + k.Conv.Resumen());
                    GuardarConv(k, ahora);
                }
            }
            if (!_siembraArchivoLanzada && contrato != "" && (_capas.Any(c => ModoDe(c) != CapaModo3.No && !c.Conv.TieneValor(contrato)) || double.IsNaN(_pares.RPar().R))) SembrarDesdeArchivo(contrato);
            var v = k.Conv.Calcular(ahora, contrato, venc);
            double mapa = v.V; string origen = v.Texto;
            k.BaseSinCarry = v.VSinCarry;
            if (!k.PorRazon)
            {
                bool fwdOk = !double.IsNaN(k.BaseForwards) && k.BaseForwards > 0;
                if (Capa3BaseNdxB == BaseNdx37.Forwards && fwdOk)
                {
                    origen = "base forwards " + k.BaseForwards.ToString("0.00", inv) + " (recta de forwards, la 3.6.5" + (double.IsNaN(k.BaseForwardsErrTicks) ? "" : "; residuo " + k.BaseForwardsErrTicks.ToString("0.#", inv) + " ticks") + "); la sincronizada daba " + (double.IsNaN(mapa) ? "-" : mapa.ToString("0.00", inv));
                    mapa = k.BaseForwards; k.BaseSinCarry = mapa;
                }
                else if (double.IsNaN(mapa) && fwdOk)
                {
                    origen = "SIN SINCRONIZAR (" + v.Texto + "): base forwards " + k.BaseForwards.ToString("0.00", inv) + " hasta tener la rueda";
                    mapa = k.BaseForwards; k.BaseSinCarry = mapa;
                }
            }
            k.ConvTexto = origen;
            if (double.IsNaN(mapa) || mapa == 0) return false;
            if (k.PorRazon) { cad.Escala = mapa; cad.EscalaOrigen = origen; cad.Base = 0; cad.BaseConfiable = false; }
            else { cad.Base = mapa; cad.BaseCruda = mapa; cad.BaseConfiable = true; cad.EscalaOrigen = origen; }
            return true;
        }

        /// <summary>C5: si alguna capa no tiene conversion para el contrato del grafico (primer arranque, o la persistida es de otro contrato),
        /// la siembra UNA vez desde el archivo de cboe-local (los ultimos 5 dias UTC) con el precio de las velas del grafico (interpoladas) y
        /// del anillo de ticks; tambien los pares NDX/QQQ de la alarma. En otro hilo; adopta solo donde el vivo no tiene valor.</summary>
        private void SembrarDesdeArchivo(string contrato)
        {
            _siembraArchivoLanzada = true;
            var velas = FotoVelas(20000);   // en el hilo del temporizador, como el resto de GetCandle
            var vencSiembra = VencimientoGrafico37();   // idem: lee el codigo del grafico (ATAS) aca, no en el otro hilo
            var hoy = DateTime.UtcNow.Date;
            var dias = Enumerable.Range(0, 6).Select(i => hoy.AddDays(-5 + i)).ToList();
            Log("capas: siembro la conversion sincronizada desde el archivo de cboe-local (" + dias[0].ToString("MM-dd", CultureInfo.InvariantCulture) + ".." + hoy.ToString("MM-dd", CultureInfo.InvariantCulture) + ", " + velas.Count + " velas del grafico)");
            _ = Task.Run(() =>
            {
                try
                {
                    var t0 = DateTime.UtcNow;
                    var nuevos = _capas.ToDictionary(k => k.Nombre, k => new ConversionCboe37(k.PorRazon));
                    var pares = new ParesFamilia37();
                    var todas = new List<(CapaCboe K, DateTime Gen, DateTime Ts, double Spot)>();
                    foreach (var k in _capas) foreach (var c in ArchivoCboe37.Cabeceras(CarpetaCboeLocal, k.Archivo, dias)) todas.Add((k, c.Gen, c.Ts, c.Spot));
                    todas = todas.OrderBy(x => x.Gen).ThenBy(x => x.Ts).ToList();
                    int conPrecio = 0;
                    foreach (var x in todas)
                    {
                        var tP = x.Ts.AddSeconds(-ConversionCboe37.RETRASO_S);
                        double p = PrecioArchivo(tP, velas);
                        if (!double.IsNaN(p)) conPrecio++;
                        var cv = nuevos[x.K.Nombre];
                        if (!cv.Observar(x.Gen, x.Ts, x.Spot, p, contrato)) continue;
                        if (x.K.PorRazon) pares.Qqq(x.Gen, tP, x.Spot, cv.UltimaViva); else pares.Ndx(x.Gen, tP, x.Spot, cv.UltimaViva);
                    }
                    var ahora = DateTime.UtcNow;
                    foreach (var k in _capas)
                    {
                        var nv = nuevos[k.Nombre];
                        bool adopta = !k.Conv.TieneValor(contrato) && nv.TieneValor(contrato);
                        if (adopta) { lock (_candado) { k.Conv = nv; } GuardarConv(k, ahora, true); }
                        var v = nv.Calcular(ahora, contrato, vencSiembra);
                        Log("capa " + k.Nombre + ": siembra desde el archivo " + (adopta ? "ADOPTADA" : "no adoptada (el vivo ya tiene valor o el archivo no alcanza)") + ": " + nv.Resumen() + " -> " + v.Texto);
                    }
                    if (double.IsNaN(_pares.RPar().R) && !double.IsNaN(pares.RPar().R)) { _pares.Cargar(pares.Serializar()); Log("familia: pares NDX/QQQ sembrados desde el archivo (R " + pares.RPar().R.ToString("0.0000", CultureInfo.InvariantCulture) + ", " + pares.RPar().N + " pares)"); }
                    Log("capas: siembra terminada en " + (DateTime.UtcNow - t0).TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s: " + todas.Count + " fotos de CBOE, " + conPrecio + " con precio del MNQ en cadena.ts-900 s");
                }
                catch (Exception e) { Registro.Excepcion(LOG, "SembrarDesdeArchivo", e); }
            });
        }

        /// <summary>El precio en tUtc para la siembra: el anillo de ticks si lo cubre, si no la foto de velas interpolada.</summary>
        private double PrecioArchivo(DateTime tUtc, List<VelaPx> velas)
        {
            long s = (tUtc.Ticks - EPOCA) / TimeSpan.TicksPerSecond;
            for (int k = 0; k <= 120; k++)
            {
                long q = s - k; if (q <= 0) break;
                int i = (int)(q % ANILLO_S);
                if (_anilloSeg[i] == q) { double p = _anilloPx[i]; if (p > 0) return p; }
            }
            return PrecioVelaEn(tUtc, velas);
        }

        /// <summary>C5: la alarma de la familia. Base equivalente desde QQQ = NDX x (razon / R - 1), R = NDX/QQQ pareados de la rueda, contra la
        /// base de NDX en uso SIN el decaimiento de la noche (la razon de QQQ no decae: Simulador37). Mas de 2 pts: alarma en el log y en los
        /// rotulos de las capas.</summary>
        private void AlarmaFamilia(DateTime ahora)
        {
            try
            {
                var inv = CultureInfo.InvariantCulture;
                var kn = Capa("NDX"); var kq = Capa("QQQ");
                string alarma = "";
                if (kn?.L != null && kq?.L != null && !double.IsNaN(kq.Escala) && kn.Spot > 0)
                {
                    var rp = _pares.RPar();
                    double be = ParesFamilia37.BaseEquivalente(kn.Spot, kq.Escala, rp.R);
                    double bn = kn.BaseSinCarry;   // la de NDX sin el decaimiento de la noche (la razon de QQQ tampoco decae)
                    if (!double.IsNaN(be) && !double.IsNaN(bn) && bn != 0)
                    {
                        double dif = bn - be;
                        alarma = _alarma37.Evaluar(ahora, dif);   // 3.7.1 (A1): solo si pasa de 2 pts durante 10 min seguidos
                        bool cambio = (alarma == "") != (_alarmaFamilia == "");
                        if (cambio || (ahora - _ultimoLogAlarma).TotalMinutes >= 10)
                        {
                            _ultimoLogAlarma = ahora;
                            Log((alarma == "" ? "familia: base NDX " : "ALARMA familia: base NDX ") + bn.ToString("0.00", inv) + " contra la equivalente de QQQ " + be.ToString("0.00", inv)
                                + " (NDX " + kn.Spot.ToString("0.##", inv) + " x (razon " + kq.Escala.ToString("0.0000", inv) + " / R " + rp.R.ToString("0.0000", inv) + " de " + rp.N + " pares del " + rp.Dia.ToString("dd-MM", inv) + " - 1), base NDX sin el carry de la noche): diferencia " + dif.ToString("+0.00;-0.00", inv) + " pts" + (alarma != "" ? " (> 2 hace " + ((int)(ahora - _alarma37.DesdeUtc).TotalMinutes) + " min: ALARMA)" : Math.Abs(dif) > 2.0 ? " (> 2 hace " + ((int)(ahora - _alarma37.DesdeUtc).TotalMinutes) + " min: todavia no, pide 10 seguidos)" : " (<= 2: ok)"));
                        }
                    }
                }
                _alarmaFamilia = alarma;   // 3.7.1 (A1): solo log y recuadro; ya no va en los rotulos de las capas
            }
            catch (Exception e) { Registro.Excepcion(LOG, "AlarmaFamilia", e); }
        }

        // ------------------------------------------------------------------ carga y cuenta (desde el latido, cada 60 s por capa)

        private void ActualizarCapas(string raiz, double fut, DateTime ahora)
        {
            if (raiz != "NQ" || fut <= 0) return;   // las capas QQQ/NDX son para graficos de NQ/MNQ; en ES irian SPX/SPY (pendiente)
            bool alguna = false;
            foreach (var k in _capas)
            {
                try
                {
                    if (ModoDe(k) == CapaModo3.No) { lock (_candado) { k.L = null; } continue; }
                    if ((ahora - k.UltimaCargaUtc).TotalSeconds < 60) continue;
                    k.UltimaCargaUtc = ahora; alguna = true;
                    string json = null; string origen = "";
                    var ruta = Path.Combine(CarpetaCboeLocal, "ultima-" + k.Archivo + ".json");
                    if (File.Exists(ruta) && (ahora - File.GetLastWriteTimeUtc(ruta)).TotalMinutes <= 20) { json = File.ReadAllText(ruta); origen = "cboe-local"; }
                    else if (k.NubeJson != null) { json = k.NubeJson; origen = "nube"; }
                    if (json == null || (ahora - k.GeneradoUtc).TotalMinutes > 20 && origen == "nube" && (ahora - k.UltimaNubeUtc).TotalMinutes >= 5)
                    {
                        // la nube como respaldo, a lo sumo una bajada cada 5 min; el resultado se usa en el proximo latido
                        if ((ahora - k.UltimaNubeUtc).TotalMinutes >= 5)
                        {
                            k.UltimaNubeUtc = ahora;
                            var cap = k;
                            _ = Task.Run(async () =>
                            {
                                try { cap.NubeJson = await _http.GetStringAsync(string.Format(NUBE, cap.Archivo)).ConfigureAwait(false); }
                                catch (Exception e) { Log("capa " + cap.Nombre + ": la nube no respondio: " + e.Message); }
                            });
                        }
                        if (json == null) { lock (_candado) { k.Estado = "sin archivo local (" + ruta + ") ni nube todavia"; } continue; }
                    }
                    CargarCapa(k, json, origen, fut, ahora);
                }
                catch (Exception e) { Registro.Excepcion(LOG, "ActualizarCapas " + k.Nombre, e); }
            }
            if (alguna) AlarmaFamilia(ahora);
        }

        /// <summary>Parsea ultima-&lt;ticker&gt;.json, lo lleva al futuro con la conversion sincronizada (C5) y corre la cuenta (C4: multiplicador
        /// 100 de CBOE). D1/D2 = una por lado en el strike exacto (D3). Si el sello (ts|ultimo_trade|spot) no cambio, no se vuelve a parsear.</summary>
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
            DateTime tsUtc = DateTime.TryParseExact(ts, "yyyy-MM-dd HH:mm:ss", inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var tsu) ? DateTime.SpecifyKind(tsu, DateTimeKind.Utc) : DateTime.MinValue;
            DateTime ultimoUtc = DateTime.MinValue;
            if (!string.IsNullOrEmpty(ultimo) && DateTime.TryParseExact(ultimo, "yyyy-MM-dd'T'HH:mm:ss", inv, DateTimeStyles.None, out var ultNy)) ultimoUtc = Seleccion37.AUtc(ultNy);
            if (spot <= 0) { lock (_candado) { k.Estado = "cadena sin spot"; } return; }
            k.BaseForwards = r.TryGetProperty("base_cruda", out var bcr) && bcr.ValueKind == JsonValueKind.Number ? bcr.GetDouble() : double.NaN;   // 3.6.5
            k.BaseForwardsErrTicks = r.TryGetProperty("base_error_ticks", out var bce) && bce.ValueKind == JsonValueKind.Number ? bce.GetDouble() : double.NaN;

            Feed.Cadena cad; bool nueva;
            if (sello == k.UltimoSello && k.Cadena != null) { cad = k.Cadena; nueva = false; }   // misma foto: no se vuelve a parsear
            else
            {
                var dias = new List<double>();
                foreach (var v in c.GetProperty("vencimientos").EnumerateArray()) dias.Add(v.GetProperty("dias").GetDouble());
                var filas = new List<Feed.Fila>();
                double oiTot = 0;
                foreach (var x in c.GetProperty("filas").EnumerateArray())
                {
                    var f = new double[8]; int i = 0;
                    foreach (var y in x.EnumerateArray()) { if (i < 8) f[i] = y.GetDouble(); i++; }
                    if (i < 8) continue;
                    int vi = (int)f[1]; if (vi < 0 || vi >= dias.Count) continue;
                    filas.Add(new Feed.Fila { K = f[0], V = vi, OiC = f[2], OiP = f[3], IvC = f[4], IvP = f[5], VolC = f[6], VolP = f[7] });
                    oiTot += f[2] + f[3];
                }
                if (dias.Count == 0 || filas.Count == 0) { lock (_candado) { k.Estado = "cadena vacia"; } return; }
                cad = new Feed.Cadena
                {
                    Ts = ts, SpotIdx = spot, Dias = dias.ToArray(), Filas = filas, UltimoTrade = ultimo,
                    HorizonteCadena = dias.Max(), RecibidoUtc = ahora, GeneradoUtc = generado, EsFuturo = false, Fuente = k.Nombre + " CBOE",
                    PorRazon = k.PorRazon, Apalancamiento = 1.0, Multiplicador = 100.0,   // 3.7.0 C4: opciones de CBOE, 100 USD por punto
                };
                k.UltimoSello = sello; k.OiTotal = oiTot; nueva = true;
            }
            if (!Mapear37(k, cad, spot, tsUtc, generado, nueva, ahora))
            {
                lock (_candado) { k.L = null; k.Cadena = cad; k.Estado = "sin conversion sincronizada (" + k.ConvTexto + ")"; }
                if ((ahora - _ultimoAuditCapas).TotalSeconds >= 60) Log("capa " + k.Nombre + ": sin conversion: " + k.ConvTexto + " -> la capa no se dibuja (no se inventa una base)");
                return;
            }

            var a = k.Nucleo.A; var s = _nucleo.A;   // la misma cuenta y los mismos ajustes que la primaria
            a.Horizonte = s.Horizonte; a.CuantasDominantes = 2; a.RadioDominantesPct = s.RadioDominantesPct; a.RadioDominantesMaxPts = s.RadioDominantesMaxPts;
            a.EmpatePct = s.EmpatePct; a.DominantesDeNoche = s.DominantesDeNoche; a.ZeroInterpolado = s.ZeroInterpolado;
            a.UnaPorLado = false; a.Centroide = false; a.RadioCentroidePts = 12.0;   // 3.7.1 (A6): strike exacto, sin una por lado
            var L = k.Nucleo.Calcular(cad, fut, ahora);
            if (L != null && !L.SinBase && L.Perfil != null)
            {
                // 3.7.1 (A6): D1/D2 = la regla de la 2.0 (las dos de mayor |GEX vol| a <= min(2 %·F, 100), strike exacto, D1 = la mayor) y
                // (A4) M+/- OI a <= ese radio sin la serie que cierra en < 30 min: Dibujo37.PrepararCapa, la MISMA que corre Simulador37
                Dibujo37.PrepararCapa(L, cad, fut, ahora, RadioDominantes("NQ"), a.Tasa);
            }
            var vigente = Seleccion37.VigenciaCboe(generado, ultimoUtc);   // 3.7.0 C6
            lock (_candado)
            {
                k.Cadena = cad; k.L = (L != null && !L.SinBase) ? L : null; k.GeneradoUtc = generado; k.UltimoTradeUtc = ultimoUtc; k.TsUtc = tsUtc; k.VigenteHastaUtc = vigente; k.Spot = spot;
                k.Escala = k.PorRazon ? cad.Escala : double.NaN; k.Base = k.PorRazon ? double.NaN : cad.Base; k.Origen = origen;
                k.Estado = k.L == null ? "sin cuenta (" + (L == null ? "cadena no sirve" : "sin base") + ")" : "ok";
            }
            if (k.L != null)
            {
                if (Guardar3Estela) GuardarEstela(k.Nombre, ahora, k.L, ",\"of\":\"" + k.FechaOi.FechaOi(ahora).Fecha.ToString("yyyy-MM-dd", inv) + "\"" + (k.PorRazon ? ",\"rz\":" + cad.Escala.ToString("0.######", inv) : ""));
                if ((ahora - _ultimoAuditCapas).TotalSeconds >= 60)
                {
                    var cer = Seleccion37.Cerco(CrucesPerfil(k.L), fut);
                    Log("capa " + k.Nombre + ": " + origen + " generado hace " + (ahora - generado).TotalMinutes.ToString("0", inv) + " min, ultimo trade hace "
                        + (ultimoUtc == DateTime.MinValue ? "?" : (ahora - ultimoUtc).TotalMinutes.ToString("0", inv)) + " min" + (Seleccion37.Congelada(ultimoUtc) ? " (CONGELADA: vale hasta " + vigente.ToString("dd-MM HH:mm", inv) + "Z)" : "")
                        + ", " + (k.PorRazon ? "razon " + cad.Escala.ToString("0.0000", inv) : "base " + cad.Base.ToString("0.00", inv)) + " (" + cad.EscalaOrigen + "), " + k.FechaOi.Rotulo(ahora)
                        + ", strikes " + k.L.Perfil.Count + ", doms " + string.Join("/", k.L.Doms.Select(d => d.Fut.ToString("0.00", inv))) + ", cruce " + k.L.ZeroVol.ToString("0.00", inv)
                        + (k.PorRazon ? "" : ", techo/piso " + (double.IsNaN(cer.Techo) ? "-" : cer.Techo.ToString("0.00", inv)) + "/" + (double.IsNaN(cer.Piso) ? "-" : cer.Piso.ToString("0.00", inv))));
                }
            }
            if (k == _capas[_capas.Count - 1] && (ahora - _ultimoAuditCapas).TotalSeconds >= 60) _ultimoAuditCapas = ahora;
        }
        private DateTime _ultimoAuditCapas = DateTime.MinValue;

        // ------------------------------------------------------------------ la estela de cada capa por vela cerrada, y su memoria

        /// <summary>Desde RegistrarVelaCerrada: anota D1/D2/cruce de cada capa para esa vela mientras su cadena este VIGENTE (C6: 25 min, o hasta
        /// las 13:30 UTC si esta congelada). El techo/piso del cruce de NDX es contra el CIERRE de esa vela (sin islas, C3).</summary>
        private void RegistrarCapasVela(DateTime tVela, double cierre)
        {
            try
            {
                var ahora = DateTime.UtcNow;
                lock (_candado)
                {
                    foreach (var k in _capas)
                    {
                        if (k.L == null || ModoDe(k) == CapaModo3.No) continue;
                        if (ahora > k.VigenteHastaUtc) continue;   // 3.7.0 C6 (antes: 25 min desde 'generado' aunque la cadena estuviera congelada)
                        if (!_estelaCapas.TryGetValue(k.Nombre, out var d)) { d = new Dictionary<DateTime, double[]>(); _estelaCapas[k.Nombre] = d; }
                        d[tVela] = Dibujo37.RegistroCapa(k.L, cierre, k.PorRazon);   // [D1, D2, cruce, base usada (3.6.6), techo, piso contra SU cierre (3.6.8)]: sin ATAS (Simulador37)
                        AnotarMajorOiCapa(k.Nombre, tVela, k.L.MpOi, k.L.MnOi, true);   // 3.6.2
                        if (d.Count > 6000) foreach (var kk in d.Keys.OrderBy(x => x).Take(d.Count - 5000).ToList()) d.Remove(kk);
                    }
                }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "RegistrarCapasVela", e); }
        }

        /// <summary>Al arrancar (desde SembrarMemoria): la estela guardada de cada capa vuelve a su vela, con vida de 25 min entre puntos.</summary>
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
                    lock (_candado)
                    {
                        if (!_estelaCapas.TryGetValue(k.Nombre, out var d)) { d = new Dictionary<DateTime, double[]>(); _estelaCapas[k.Nombre] = d; }
                        foreach (var v in velas)
                        {
                            if (d.ContainsKey(v.TOpen)) continue;
                            int i = tiempos.BinarySearch(v.TClose); if (i < 0) i = ~i; i--;
                            if (i < 0 || (v.TClose - tiempos[i]).TotalSeconds > VIDA_CAPA_S) continue;
                            var p = puntos[i];
                            double bsP = !double.IsNaN(p.Bs) && p.Bs > 0 ? p.Bs : double.NaN;   // 3.6.6 (3.7.0: sin 'bs' no se corre)
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
            public List<Rot37> Rotulos = new List<Rot37>();
            public List<(string Sigla, double P)> Fusion = new List<(string, double)>();
            public int TopeExtra;
        }

        /// <summary>Estela de cada capa en modo Propia y la lista de rotulos extra (con libro y tipo para el borde). 3.7.0 D1: cada marca solo a
        /// &lt;= radio del cierre de SU vela; sin atenuacion por el precio de ahora.</summary>
        private ExtraRotulos PintarCapas(RenderContext g, Func<int, int> XBar, Rectangle area, int xr, int piso, int desde, int hasta,
                                         List<(int B, DateTime T)> horas, double fut, double radioDib, CultureInfo es, Func<double, int> Y, Func<int, bool> EnPantalla, Func<int, double, bool> enRadio)
        {
            var ex = new ExtraRotulos();
            var ahora = DateTime.UtcNow;
            foreach (var k in _capas)
            {
                var modo = ModoDe(k);
                if (modo == CapaModo3.No) continue;
                GammaHoyNucleo.Lectura L; Dictionary<int, double[]> est = new Dictionary<int, double[]>(); DateTime ult;
                lock (_candado)
                {
                    L = k.L; ult = k.UltimoTradeUtc;
                    if (_estelaCapas.TryGetValue(k.Nombre, out var d)) foreach (var (b, t) in horas) if (d.TryGetValue(t, out var e0)) est[b] = e0;
                }
                if (L != null)
                    for (int i = 0; i < Math.Min(2, L.Doms.Count); i++) if (L.Doms[i].Fut > 0) ex.Fusion.Add((k.Nombre, L.Doms[i].Fut));
                if (modo != CapaModo3.Propia) continue;

                int bw = 5;
                { int xa = XBar(desde), xb = XBar(hasta); if (hasta > desde && xa != int.MinValue && xb != int.MinValue) bw = Math.Max(3, (xb - xa) / Math.Max(1, hasta - desde)); }
                int w = Math.Max(1, bw - 1);
                int ultima = CurrentBar - 1;
                int inicio = Estela3VelasAtras > 0 ? Math.Max(0, ultima - Estela3VelasAtras) : _barInicioSesion;
                // la estela de la capa: guiones de 2 px en su color (3.0.5); la sesion anterior va atenuada en vez de oculta
                if (VerEstelaEf && hasta >= desde && est.Count > 0)
                {
                    var col = k.Color; string libro = k.Nombre; bool enf = EnfasisCapaZero(k.Nombre);
                    foreach (var kv in est)
                    {
                        int b = kv.Key; if (b < desde || b > hasta) continue;
                        bool previa = b < inicio;
                        // 3.7.1 (A2): la estela de la capa YA NO se corre a la base de ahora (3.6.6): cada marca queda donde se anoto, que es lo que
                        // juzga la auditoria; con la base de dia acumulada no salta en la rueda (Dibujo37.CORRER_ESTELA_NDX = false)
                        double corr = Dibujo37.CORRER_ESTELA_NDX ? Dibujo37.Corrimiento(kv.Value, !k.PorRazon && L != null && L.Base != 0 ? L.Base : double.NaN) : 0;
                        if (previa && !Estela3SesionAnterior) continue;
                        int x = XBar(b); if (x == int.MinValue) continue;
                        // 3.7.0: que puntos de la estela pasan las casillas (cerco 3.6.8, una por una 3.5.0) sale de Dibujo37 (sin ATAS)
                        foreach (var (i0, i, p) in Dibujo37.PuntosCapa(kv.Value, CercoCapa(k.Nombre), Capa3ZeroRombos, RayaCapa(k.Nombre, 0), RayaCapa(k.Nombre, 1), RayaCapaZero(k.Nombre), corr))
                        {
                            if (!enRadio(b, p)) continue;   // 3.7.0 D1
                            int y = Y(p); bool vis = EnPantalla(y);   // 3.7.2: se encola igual (la regla de la vela cuenta lo de fuera de pantalla)
                            int alfa = b >= ultima - 30 ? 230 : 160;
                            if (previa) alfa = alfa * 45 / 100;
                            string tipo = i0 == 0 ? "D1" : i0 == 1 ? "D2" : i0 == 4 ? "cruce techo" : i0 == 5 ? "cruce piso" : "cruce";
                            // 3.7.1 (A5): se encola; una sola marca por (libro, strike) en la vela (PintarMarcasAgrupadas)
                            if (i == 2 && Zero3Estilo == ZeroEstilo3.RomboVerde)
                            {
                                int rz = Math.Max(2, Math.Min(3, w / 2 + 1));   // 3.3.1: el cruce de la capa como rombo, como la fila de la 2.0 (3.7.2: hasta 3 px como el de la 2.0, no 4: no mas grueso que NQ D1)
                                if (enf) { rz = Math.Max(3, Math.Min(6, w / 2 + 2)); alfa = previa ? 140 : 255; }   // 3.5.0: enfasis
                                int a0 = alfa;
                                EncolarMarca(b, libro, tipo, p, vis, () => g.FillPolygon(Color.FromArgb(AlfaAtravesada(a0, p, b), col), new[] { new Point(x, y - rz), new Point(x + rz, y), new Point(x, y + rz), new Point(x - rz, y) }));
                                continue;
                            }
                            int a1 = alfa; bool z = i == 2;
                            EncolarMarca(b, libro, tipo, p, vis, () => g.FillRectangle(Color.FromArgb(AlfaAtravesada(a1, p, b), col), new Rectangle(x - w / 2, z ? y : y - 1, w, z ? 1 : 2)));
                        }
                    }
                }
                // 3.6.8: con la cadena de CBOE CONGELADA, las velas posteriores al ultimo trade SIN estela (p. ej. antes de que arrancara el indicador)
                // se reconstruyen con los cruces de ahora (sin islas) contra el cierre de cada vela (cod_09: |reconstruido - vivo| <= 0,47 pts).
                if (VerEstelaEf && CercoCapa(k.Nombre) && Capa3ZeroRombos && RayaCapaZero(k.Nombre) && L != null && Seleccion37.Congelada(ult))
                {
                    var crucesTodos = CrucesPerfil(L);
                    int rz = Math.Max(2, Math.Min(3, w / 2 + 1)); if (EnfasisCapaZero(k.Nombre)) rz = Math.Max(3, Math.Min(6, w / 2 + 2));   // 3.7.2: hasta 3 px
                    foreach (var (b, tv) in horas)
                    {
                        if (b < desde || b > hasta || tv < ult || est.ContainsKey(b)) continue;
                        double cl; try { var cb = GetCandle(b); if (cb == null) continue; cl = (double)cb.Close; } catch { continue; }
                        var cer = Seleccion37.Cerco(crucesTodos, cl);
                        int x = XBar(b); if (x == int.MinValue) continue;
                        int ip = 0;
                        foreach (var p in new[] { cer.Techo, cer.Piso })
                        {
                            string tipo = ip++ == 0 ? "cruce techo" : "cruce piso";
                            if (double.IsNaN(p) || p <= 0 || !enRadio(b, p)) continue;
                            int y = Y(p); bool vis = EnPantalla(y);   // 3.7.2
                            int alfa = EnfasisCapaZero(k.Nombre) ? 255 : (b >= ultima - 30 ? 230 : 160);
                            var col = k.Color;
                            EncolarMarca(b, k.Nombre, tipo, p, vis, () => g.FillPolygon(Color.FromArgb(AlfaAtravesada(alfa, p, b), col), new[] { new Point(x, y - rz), new Point(x + rz, y), new Point(x, y + rz), new Point(x - rz, y) }));   // 3.7.1 (A5)
                        }
                    }
                }
                if (L == null) continue;
                // rotulos: "QQQ D1 ▲ ·17m" (la edad REAL del dato, desde el ultimo trade de CBOE); los lejanos van al borde (Rotulos)
                string edad = ult == DateTime.MinValue ? "" : " ·" + ((int)(ahora - ult).TotalMinutes).ToString("0", es) + "m";
                int xUlt = XBar(Math.Max(0, CurrentBar - 1));
                // 3.7.0: la lista de rotulos de la capa (D1/D2 una por una 3.5.0, techo/piso 3.6.8 o el cruce mas cercano C8) sale de Dibujo37
                // (sin ATAS, la misma que corre Simulador37); aca solo el color y, con 'Rayas largas', la linea de la ultima vela al eje.
                GammaHoyNucleo.Lectura Lnq; lock (_candado) { Lnq = _L; }
                double fusion = (double)Math.Max(0.25m, Capa3FusionPts);
                Func<double, bool> ocultar = Rayas3Independientes ? null : (Func<double, bool>)(p => Lnq != null && Lnq.Doms.Any(dq => dq.Fut > 0 && Math.Abs(dq.Fut - p) <= fusion));   // 3.5.0: independientes = se dibuja aunque coincida con NQ
                foreach (var r in Dibujo37.RotulosCapa(k.Nombre, L, fut, edad, RayaCapa(k.Nombre, 0), RayaCapa(k.Nombre, 1), Capa3ZeroRombos, RayaCapaZero(k.Nombre), CercoCapa(k.Nombre), ocultar))   // 3.7.1 (A1): sin la alarma
                {
                    bool esD = r.Tipo == "D1" || r.Tipo == "D2";
                    if (Rayas3Largas && xUlt != int.MinValue && Math.Abs(r.P - fut) <= radioDib && (esD ? Capa3LineaActual : (r.Tipo == "cruce" && EnfasisCapaZero(k.Nombre))))
                    {
                        int y = Y(r.P);
                        if (EnPantalla(y) && xr > xUlt) g.DrawLine(esD ? new RenderPen(Color.FromArgb(170, k.Color), 1f) : new RenderPen(Color.FromArgb(220, k.Color), 2f), Math.Max(area.Left, xUlt), y, xr, y);   // 3.5.0
                    }
                    ex.Rotulos.Add(new Rot37(r, k.Color)); ex.TopeExtra++;
                }
            }
            return ex;
        }
    }
}
