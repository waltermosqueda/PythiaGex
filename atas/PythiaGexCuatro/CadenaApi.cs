using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using ATAS.DataFeedsCore;
using ATAS.Indicators;
using OFT.Platform.Core.Providers.Options;

namespace PythiaGexCuatro
{
    /// <summary>
    /// LA CADENA DE OPCIONES DE NQ (o ES), EN VIVO, POR LA API PUBLICA DE ATAS 8.0.15.
    ///
    /// POR QUE EXISTE ESTE ARCHIVO (06-10-2026)
    ///
    /// ATAS 8.0.15.302 (instalado el 29-09) empezo a LIMITAR las suscripciones a
    /// opciones hechas "por fuera" de su API nueva: 200 contratos a la vez, 30
    /// suscripciones y 2 consultas de cadena por minuto. La 1.x y la 2.0 van por
    /// fuera (SubscribeToMarketData directo y el PuenteRithmic por reflexion), y
    /// el log de la plataforma lo dice con todas las letras:
    ///
    ///     Subscription of option Q1BV6 C31130@CME refused: 200 options are
    ///     already held outside the options API. Use IOptionsDataProvider.
    ///     Option chain request ... refused: the rate of option lookups outside
    ///     the options API is exceeded.
    ///
    /// Resultado medido hoy: el libro vivo de NQ quedo en 84-86 strikes y con
    /// huecos. Esta clase usa la via nueva, que NO cuenta en ese cupo.
    ///
    /// LA API (descompilada de ATAS.Indicators / OFT.Platform.Core 8.0.15):
    ///
    ///   Indicator.OptionsDataProvider : IOptionsDataProvider
    ///       IsAvailable, GetOptionSeriesAsync(ct), GetOptionsAsync(serie, ct),
    ///       SubscribeToOption(Security) -> IOptionQuoteSubscription
    ///       { Option, Summary (SecuritySummary?), event Changed }
    ///
    /// EL PROBLEMA: ese proveedor resuelve el subyacente como la Security DEL
    /// GRAFICO. El operador opera en MNQ, y las opciones de MNQ son iliquidas
    /// (solo trimestrales): el libro que importa es el de NQ. Por eso aca se va
    /// un escalon mas abajo, a la clase PUBLICA que el proveedor usa por dentro:
    ///
    ///   OFT.Platform.Core.Providers.Options.OptionsSubscriptionService
    ///       GetOptionSeriesAsync(conector, subyacente, ct)
    ///       GetOptionsAsync(conector, serie, ct)
    ///       Subscribe(conector, opcion, dueño) / ReleaseOwned(dueño)
    ///       TryGetFeed(conector, out feed) / CanServeOptions(conector)
    ///
    /// y se le pasa el futuro NQ del trimestre del grafico. La instancia del
    /// servicio es un campo privado (nombre ofuscado) del proveedor: se busca
    /// POR TIPO, nunca por nombre. Si el grafico ya es NQ (o ES), el proveedor
    /// directo alcanza y se usa ese.
    ///
    /// LIMITES QUE ESTA CLASE RESPETA (leidos del descompilado, no supuestos):
    ///   - 512 suscripciones vivas por instancia de indicador (el proveedor tira
    ///     InvalidOperationException al pasarse). Aca el tope duro es 320.
    ///   - 3000 vivas en total; rafaga de 2000 suscripciones con recarga 5/s;
    ///     20 consultas de cadena con recarga 1/s; cache de consultas 10 min;
    ///     timeout 30 s; al soltar hay un "hold" de 5 s / 30 s / 5 min / 15 min
    ///     que escala si el mismo contrato se suelta y se vuelve a pedir
    ///     (anti-flapping). Por eso la ventana se recentra solo cuando el precio
    ///     se fue mas de 0,5 %, y solo se suelta lo que SALE de la ventana.
    ///   - Lookups: a lo sumo una tanda cada 30 s por instancia, con cache
    ///     propio de 10 min. Un espaciador global de 10 s entre tandas de
    ///     suscripcion de instancias distintas (Rithmic se ahoga con rafagas,
    ///     medido en la 1.x con 75 s; la API ya pacea por su lado).
    ///
    /// LO QUE LLEGA: la API suscribe SOLO SubscriptionType.Summary. El
    /// SecuritySummary trae BestBid/Ask (precio y volumen), LastTrade, Settlement,
    /// OpenInterest, CurrentDayTotalVolume, PrevDayTotalVolume. Si las puntas
    /// no vinieran en el Summary de Rithmic, se leen del Security (BestBidPrice /
    /// BestAskPrice): la sonda cuenta las dos cosas por separado para saberlo.
    /// La IV no viene servida: se despeja del punto medio con Black-76.
    ///
    /// SEMANA DEL ROLL (copiado de CadenaViva 1.10d/1.11c): con el grafico ya en
    /// Z6 y el U6 todavia vivo, las weeklies que vencen ANTES del U6 y la Regular
    /// (trimestral) de ese dia son opciones SOBRE U6 (se piden con su serie, con
    /// UnderlyingCode = U6), y sus strikes se dibujan corridos por el spread vivo
    /// Z6 - U6. La weekly de la tarde de ese viernes es sobre Z6.
    /// </summary>
    public sealed class CadenaApi : IDisposable
    {
        // ------------------------------------------------------------------
        // lo que se publica
        // ------------------------------------------------------------------

        public sealed class Fila
        {
            public double K;          // strike en precio del grafico (corrido si es del trimestre que vence)
            public double K0;         // strike CRUDO, solo si fue corrido (misma convencion que la viva vieja); 0 si no
            public double Dias;       // al vencimiento, con hora NY
            public bool EsCall;
            public double OI;
            public double IV = double.NaN;
            public double Bid, Ask, Mid;
            public double VolHoy, VolAyer, Last;
            public double BidVol, AskVol;   // contratos apoyados en la punta (BestBidVolume / BestAskVolume del Summary), 3.2.3
            public bool ConPuntas;    // bid y ask validos
            public bool ConResumen;   // llego al menos un Summary
            public string Codigo = "";
        }

        public sealed class Estado
        {
            public bool ApiDisponible;
            public string Via = "";          // "servicio" | "proveedor" | ""
            public string Conector = "";
            public string Futuro = "";
            public int Series, Contratos, Suscritos, ConResumen, ConPuntas, ConOI, Rechazadas, Errores, Lookups;
            /// <summary>Filas suscritas que ya vencieron y Filas() dejo afuera (la gracia de 30 min de la suscripcion).</summary>
            public int Vencidas;
            public DateTime UltimoResumenUtc = DateTime.MinValue;
            public DateTime UltimoArmadoUtc = DateTime.MinValue;
            public string Texto = "sin arrancar";
            public double EdadSegundos => UltimoResumenUtc == DateTime.MinValue ? double.NaN : (DateTime.UtcNow - UltimoResumenUtc).TotalSeconds;
        }

        // ------------------------------------------------------------------
        // ajustes (los pone el indicador antes de Arrancar)
        // ------------------------------------------------------------------

        /// <summary>Tope duro de contratos suscritos por instancia. 512 es el limite del proveedor; aca 320.</summary>
        public int TopeContratos = 320;
        /// <summary>Todos los strikes hasta este porcentaje del precio...</summary>
        public double VentanaDensaPct = 1.0;
        /// <summary>...y uno de cada dos hasta este otro.</summary>
        public double VentanaRalaPct = 2.0;
        /// <summary>Cuantas FECHAS de vencimiento (las mas cercanas). El viernes de la trimestral una fecha trae dos series.</summary>
        public int Vencimientos = 2;
        /// <summary>Se recentra la ventana cuando el precio se alejo mas que esto del centro.</summary>
        public double RecentrarPct = 0.5;

        public const int SEGUNDOS_ENTRE_LOOKUPS = 30;
        public const int MINUTOS_CACHE_LOOKUPS = 10;
        public const double ESPACIO_GLOBAL_S = 10;

        // ------------------------------------------------------------------
        // estado
        // ------------------------------------------------------------------

        private sealed class Entrada
        {
            public Security Op;
            public IOptionQuoteSubscription Sub;
            public SecuritySummary Resumen;      // el ultimo que llego por Changed (o el inicial)
            public DateTime UltimoUtc = DateTime.MinValue;
            public bool DelAnterior;             // sobre el trimestre que vence: strike corrido
            public bool VenceALaManana;          // trimestral: 9:30 NY
            public long Cambios;
        }

        private readonly object _llave = new object();
        private readonly Dictionary<string, Entrada> _entradas = new Dictionary<string, Entrada>(StringComparer.OrdinalIgnoreCase);
        private readonly Estado _estado = new Estado();
        private IPuerta _puerta;
        private IDataFeedConnector _conn;
        private Security _futuro, _futuroAnterior;
        private double _futuroAnteriorPrecio;
        private bool _desplazListo;
        private int _armando;
        private DateTime _ultimoIntento = DateTime.MinValue, _ultimoLookup = DateTime.MinValue, _desplazLog = DateTime.MinValue;
        private Action<string> _log;
        private Func<double> _precioGrafico;
        private string _raiz = "";
        private string _codigoGrafico = "";
        private long _evCambios, _evPrimerResumen;
        private bool _parada;

        // cache propio de lookups (10 min): series por subyacente y contratos por serie
        private readonly Dictionary<string, (DateTime cuando, List<OptionSeries> lista)> _cacheSeries = new Dictionary<string, (DateTime, List<OptionSeries>)>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (DateTime cuando, List<Security> lista)> _cacheOps = new Dictionary<string, (DateTime, List<Security>)>(StringComparer.OrdinalIgnoreCase);

        // espaciador global entre tandas de suscripcion de TODAS las instancias de este ensamblado
        private static readonly object _llaveGlobal = new object();
        // 4.1: el espaciador es estatico POR ENSAMBLADO: no separa a la 3.0 (PythiaGexTres.dll) de la 4.0. Al reiniciar ATAS las dos arman a la
        // vez; la 4.0 espera DESFASE_PRIMER_ARMADO_S desde que se toca esta clase por primera vez (el primer indicador), asi la 3.0 suscribe
        // primero y la 4.0 despues. Las suscripciones al mismo contrato se comparten en el servicio de ATAS (un dueño mas, no otra suscripcion).
        public const double DESFASE_PRIMER_ARMADO_S = 20;
        private static readonly DateTime _primerArmadoDesdeUtc = DateTime.UtcNow.AddSeconds(DESFASE_PRIMER_ARMADO_S);   // consultas y suscripciones
        private static DateTime _proximoTurnoGlobal = _primerArmadoDesdeUtc;
        /// <summary>4.1: constructor estatico explicito (sin beforefieldinit): los estaticos se inicializan al crear la PRIMERA instancia (el
        /// campo _cadena del indicador), no recien cuando Arrancar toca el espaciador. Asi el desfase se cuenta desde que ATAS carga la 4.0.</summary>
        static CadenaApi() { }

        /// <summary>Precio del futuro grande (NQ), en vivo. 0 hasta que llega.</summary>
        public double Futuro { get; private set; }
        public string CodigoFuturo => _futuro?.Code ?? "";
        public string CodigoAnterior => _futuroAnterior?.Code ?? "";
        public double DesplazamientoAnterior { get; private set; }
        /// <summary>Centro de la ventana armada (precio del grafico).</summary>
        public double CentroVentana { get; private set; }
        /// <summary>Hay contratos suscritos y llego al menos un resumen con puntas.</summary>
        public bool Activa { get { lock (_llave) return _entradas.Count > 0 && _estado.ConPuntas > 0; } }
        public string Raiz => _raiz;

        public Estado Foto()
        {
            lock (_llave)
            {
                return new Estado
                {
                    ApiDisponible = _estado.ApiDisponible, Via = _estado.Via, Conector = _estado.Conector, Futuro = _estado.Futuro,
                    Series = _estado.Series, Contratos = _estado.Contratos, Suscritos = _entradas.Count, ConResumen = _estado.ConResumen,
                    ConPuntas = _estado.ConPuntas, ConOI = _estado.ConOI, Rechazadas = _estado.Rechazadas, Errores = _estado.Errores,
                    Lookups = _estado.Lookups, Vencidas = _estado.Vencidas, UltimoResumenUtc = _estado.UltimoResumenUtc, UltimoArmadoUtc = _estado.UltimoArmadoUtc, Texto = _estado.Texto,
                };
            }
        }

        // ------------------------------------------------------------------
        // la puerta: servicio (cualquier subyacente) o proveedor (el del grafico)
        // ------------------------------------------------------------------

        private interface IPuerta
        {
            string Nombre { get; }
            bool SirveParaOtroSubyacente { get; }
            Task<IReadOnlyCollection<OptionSeries>> Series(Security subyacente, CancellationToken ct);
            Task<IReadOnlyCollection<Security>> Opciones(OptionSeries serie, CancellationToken ct);
            IOptionQuoteSubscription Suscribir(Security opcion);
            void SoltarTodo(IEnumerable<IOptionQuoteSubscription> vivas);
        }

        /// <summary>El servicio publico de OFT.Platform.Core con el conector y el subyacente que ELEGIMOS.</summary>
        private sealed class PuertaServicio : IPuerta
        {
            private readonly OptionsSubscriptionService _s;
            private readonly IDataFeedConnector _c;
            private readonly object _dueno;
            public PuertaServicio(OptionsSubscriptionService s, IDataFeedConnector c, object dueno) { _s = s; _c = c; _dueno = dueno; }
            public string Nombre => "servicio";
            public bool SirveParaOtroSubyacente => true;
            public Task<IReadOnlyCollection<OptionSeries>> Series(Security sub, CancellationToken ct) => _s.GetOptionSeriesAsync(_c, sub, ct);
            public Task<IReadOnlyCollection<Security>> Opciones(OptionSeries serie, CancellationToken ct) => _s.GetOptionsAsync(_c, serie, ct);
            public IOptionQuoteSubscription Suscribir(Security op) => _s.Subscribe(_c, op, _dueno);
            public void SoltarTodo(IEnumerable<IOptionQuoteSubscription> vivas)
            {
                // ReleaseOwned suelta TODO lo que este dueño pidio en este conector, aunque alguna referencia se haya perdido
                try { _s.ReleaseOwned(_dueno); } catch { }
                foreach (var v in vivas) { try { v.Dispose(); } catch { } }
            }
        }

        /// <summary>El proveedor del indicador: solo sirve si el grafico ES el futuro grande (NQ/ES).</summary>
        private sealed class PuertaProveedor : IPuerta
        {
            private readonly IOptionsDataProvider _p;
            public PuertaProveedor(IOptionsDataProvider p) { _p = p; }
            public string Nombre => "proveedor";
            public bool SirveParaOtroSubyacente => false;
            public Task<IReadOnlyCollection<OptionSeries>> Series(Security sub, CancellationToken ct) => _p.GetOptionSeriesAsync(ct);
            public Task<IReadOnlyCollection<Security>> Opciones(OptionSeries serie, CancellationToken ct) => _p.GetOptionsAsync(serie, ct);
            public IOptionQuoteSubscription Suscribir(Security op) => _p.SubscribeToOption(op);
            public void SoltarTodo(IEnumerable<IOptionQuoteSubscription> vivas) { foreach (var v in vivas) { try { v.Dispose(); } catch { } } }
        }

        // ------------------------------------------------------------------
        // arranque
        // ------------------------------------------------------------------

        /// <summary>
        /// Arma (o rearma) la cadena. Lento (consultas y esperas): se llama en segundo plano.
        /// </summary>
        /// <param name="proveedor">Indicator.OptionsDataProvider (IOptionsDataProvider), puede ser null.</param>
        /// <param name="manager">Indicator.TradingManager (ITradingManager), puede ser null.</param>
        /// <param name="dataProvider">Indicator.DataProvider, para el rastreo de ultimo recurso.</param>
        /// <param name="codigoGrafico">Codigo del contrato del grafico ("MNQZ6") o el nombre del instrumento ("#MNQ").</param>
        /// <param name="precioGrafico">Ultimo cierre del grafico, por si el precio del futuro grande no llega.</param>
        public async Task Arrancar(object proveedor, object manager, object dataProvider, string codigoGrafico,
                                   Func<double> precioGrafico, Action<string> log, bool forzar = false)
        {
            if (_parada) return;
            if (Interlocked.Exchange(ref _armando, 1) == 1) return;
            try
            {
                if (!forzar && (DateTime.UtcNow - _ultimoIntento).TotalSeconds < 60) return;
                _ultimoIntento = DateTime.UtcNow;
                _log = log; _precioGrafico = precioGrafico; _codigoGrafico = codigoGrafico ?? "";
                _raiz = RaizGrande(_codigoGrafico);
                void L(string m) { lock (_llave) _estado.Texto = m; log?.Invoke("[cadena api] " + m); }

                if (string.IsNullOrEmpty(_raiz)) { L("raiz no soportada (" + _codigoGrafico + "): solo ES/MES y NQ/MNQ (y RTY/M2K)"); return; }

                // 4.1: desfase del PRIMER armado del proceso (consultas + suscripciones), para no armar a la vez que la 3.0 al reiniciar ATAS
                double espera0 = (_primerArmadoDesdeUtc - DateTime.UtcNow).TotalSeconds;
                if (espera0 > 0)
                {
                    L("4.1: espero " + espera0.ToString("0", CultureInfo.InvariantCulture) + " s antes del primer armado (desfase de " + DESFASE_PRIMER_ARMADO_S.ToString("0", CultureInfo.InvariantCulture) + " s contra la 3.0, que arma al mismo tiempo al reiniciar ATAS)");
                    await Task.Delay(TimeSpan.FromSeconds(espera0)).ConfigureAwait(false);
                    if (_parada) return;
                    _ultimoIntento = DateTime.UtcNow;
                }

                // 1) la puerta
                if (_puerta == null || _conn == null || !_conn.IsConnected)
                {
                    _puerta = null; _conn = null;
                    ResolverPuerta(proveedor, manager, dataProvider, L);
                    if (_puerta == null) { L("sin puerta a la API de opciones: ni el servicio ni el proveedor estan disponibles"); return; }
                }

                // 2) el futuro grande del trimestre del grafico
                if (_futuro == null || _futuro.Expiration.Date < RelojNy.Hoy().AddDays(-1))
                {
                    _futuro = await BuscarFuturo(_raiz, _codigoGrafico, (manager as ITradingManager)?.Security, L).ConfigureAwait(false);
                    if (_futuro == null) { L("no esta el futuro de " + _raiz + " ni en el catalogo local ni en el servidor"); return; }
                    lock (_llave) _estado.Futuro = _futuro.Code ?? "";
                    try { _conn?.SubscribeToMarketData(new[] { _futuro }, SubscriptionType.Prints | SubscriptionType.Best); } catch (Exception e) { L("no pude suscribir el futuro " + _futuro.Code + ": " + e.Message); }
                }
                if (!_puerta.SirveParaOtroSubyacente && !string.Equals(_futuro.Code, _codigoGrafico, StringComparison.OrdinalIgnoreCase))
                    L("OJO: la puerta es el proveedor del grafico (" + _codigoGrafico + ") y el futuro elegido es " + _futuro.Code + ": las series seran las del grafico");

                for (int i = 0; i < 15 && RefrescarFuturo() <= 0; i++) await Task.Delay(1000).ConfigureAwait(false);
                if (Futuro <= 0)
                {
                    double p = 0; try { p = precioGrafico?.Invoke() ?? 0; } catch { }
                    if (p > 0) { Futuro = p; L("el precio de " + _futuro.Code + " no llego en 15 s: uso el cierre del grafico " + p.ToString("0.##", CultureInfo.InvariantCulture) + " como centro"); }
                    else { L("no llego el precio de " + _futuro.Code + " y el grafico tampoco tiene cierre"); return; }
                }
                else L("futuro " + _futuro.Code + " en " + Futuro.ToString("0.##", CultureInfo.InvariantCulture) + " (via " + _puerta.Nombre + ", conector " + (_conn?.GetType().Name ?? "?") + ")");

                // 3) el trimestre que vence (semana del roll)
                var hoy = RelojNy.Hoy();
                Security anterior = null;
                try
                {
                    string codAnt = CodigoTrimestreAnterior(_futuro.Code);
                    if (!string.IsNullOrEmpty(codAnt) && _conn != null && _puerta.SirveParaOtroSubyacente)
                    {
                        var hit = (_conn.Securities ?? Enumerable.Empty<Security>()).FirstOrDefault(x => string.Equals(x.Code, codAnt, StringComparison.OrdinalIgnoreCase));
                        if (hit == null)
                        {
                            var r = await _conn.SearchSecuritiesAsync(new SecurityFilter { Code = codAnt, Exchange = "CME", RequestId = DateTime.UtcNow.Ticks % 1000000000L }).ConfigureAwait(false);
                            hit = (r ?? Enumerable.Empty<Security>()).FirstOrDefault(x => string.Equals(x.Code, codAnt, StringComparison.OrdinalIgnoreCase));
                        }
                        if (hit != null && hit.Expiration.Date >= hoy)
                        {
                            anterior = hit;
                            if (_futuroAnterior == null || !string.Equals(_futuroAnterior.Code, hit.Code, StringComparison.OrdinalIgnoreCase)) { _futuroAnterior = hit; _desplazListo = false; DesplazamientoAnterior = 0; }
                            try { _conn.SubscribeToMarketData(new[] { hit }, SubscriptionType.Prints | SubscriptionType.Best); } catch { }
                            for (int i = 0; i < 15 && !_desplazListo; i++) { await Task.Delay(1000).ConfigureAwait(false); RefrescarDesplazamiento(); }
                            L("roll: el trimestre que vence " + hit.Code + " (" + hit.Expiration.ToString("yyyy-MM-dd") + ") todavia cotiza"
                              + (_desplazListo ? " en " + _futuroAnteriorPrecio.ToString("0.##", CultureInfo.InvariantCulture) + ": spread " + DesplazamientoAnterior.ToString("+0.##;-0.##", CultureInfo.InvariantCulture) + " pts" : ", pero su precio no llego: sus opciones no se publican hasta tenerlo")
                              + "; las weeklies ANTERIORES a esa fecha y la Regular de ese dia son sobre " + hit.Code);
                        }
                        else { _futuroAnterior = null; _desplazListo = false; DesplazamientoAnterior = 0; }
                    }
                }
                catch (Exception e) { L("roll: no pude buscar el trimestre anterior: " + e.Message); }

                // 4) series (una tanda de lookups cada 30 s como maximo; cache 10 min)
                double esperaLookup = SEGUNDOS_ENTRE_LOOKUPS - (DateTime.UtcNow - _ultimoLookup).TotalSeconds;
                if (esperaLookup > 0) { L("espero " + esperaLookup.ToString("0") + " s antes de otra tanda de consultas (una cada " + SEGUNDOS_ENTRE_LOOKUPS + " s)"); await Task.Delay(TimeSpan.FromSeconds(esperaLookup)).ConfigureAwait(false); }
                _ultimoLookup = DateTime.UtcNow;

                var series = await SeriesDe(_futuro, L).ConfigureAwait(false);
                if (anterior != null)
                {
                    var delViejo = await SeriesDe(anterior, L).ConfigureAwait(false);
                    var ya = new HashSet<string>(series.Select(LlaveSerie));
                    var extra = delViejo.Where(z => z.Expiration.Date <= anterior.Expiration.Date && z.Expiration.Date >= hoy && !ya.Contains(LlaveSerie(z)))
                                        .GroupBy(LlaveSerie).Select(g => g.First()).ToList();
                    if (extra.Count > 0) { series = series.Concat(extra).ToList(); L("roll: " + extra.Count + " vencimiento(s) que solo lista " + anterior.Code + ": " + string.Join(", ", extra.Select(z => z.Expiration.ToString("MM-dd") + " " + z.Type))); }
                    // las que el nuevo lista pero son del viejo: se piden con la serie del viejo (UnderlyingCode = U6), si la tiene
                    var porLlaveViejo = delViejo.GroupBy(LlaveSerie).ToDictionary(g => g.Key, g => g.First());
                    series = series.Select(z => EsDelAnterior(z, anterior) && porLlaveViejo.TryGetValue(LlaveSerie(z), out var v) ? v : z).ToList();
                }
                var ahoraNy = RelojNy.Ahora();
                bool VenceAm(OptionSeries z) => EsRegular(z) && ((anterior != null && z.Expiration.Date == anterior.Expiration.Date) || z.Expiration.Date == _futuro.Expiration.Date);
                int vencidas = series.Count(z => z.Expiration.Date >= hoy && RelojNy.YaVencio(z.Expiration.Date, VenceAm(z), ahoraNy));
                if (vencidas > 0) L(vencidas + " serie(s) de hoy ya vencida(s): no se piden");
                var vivas = series.Where(z => z.Expiration.Date >= hoy && !RelojNy.YaVencio(z.Expiration.Date, VenceAm(z), ahoraNy))
                                  .GroupBy(LlaveSerie).Select(g => g.First())
                                  .OrderBy(z => z.Expiration.Date).ThenBy(z => VenceAm(z) ? 0 : 1).ToList();
                var fechas = vivas.Select(z => z.Expiration.Date).Distinct().OrderBy(d => d).Take(Math.Max(1, Vencimientos)).ToList();
                var elegidasSeries = vivas.Where(z => fechas.Contains(z.Expiration.Date)).ToList();
                if (elegidasSeries.Count == 0) { L("sin series vivas para " + _futuro.Code + " (" + series.Count + " listadas)"); lock (_llave) _estado.Series = 0; return; }
                L(elegidasSeries.Count + " serie(s) elegidas: " + string.Join(", ", elegidasSeries.Select(z => z.Expiration.ToString("MM-dd") + " " + z.Type + " sobre " + z.UnderlyingCode))
                  + ((fechas[0] - hoy).Days > 0 && ahoraNy.TimeOfDay < TimeSpan.FromHours(16.5) && hoy.DayOfWeek != DayOfWeek.Saturday && hoy.DayOfWeek != DayOfWeek.Sunday ? "  OJO: sin el vencimiento de HOY" : ""));

                // 5) contratos por serie
                var ops = new List<(Security op, bool delAnt, bool am)>();
                var codigosYa = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var z in elegidasSeries)
                {
                    bool delAnt = anterior != null && EsDelAnterior(z, anterior);
                    var lista = await OpcionesDe(z, delAnt ? anterior : _futuro, L).ConfigureAwait(false);
                    int antes = ops.Count;
                    foreach (var o in lista)
                        if (o != null && o.StrikePrice.HasValue && o.StrikePrice.Value > 0 && codigosYa.Add(o.Code ?? o.SecurityId ?? Guid.NewGuid().ToString()))
                            ops.Add((o, delAnt, VenceAm(z)));
                    L("serie " + z.Expiration.ToString("MM-dd") + " " + z.Type + ": " + lista.Count + " contratos (" + (ops.Count - antes) + " nuevos con strike), sobre " + (delAnt ? anterior.Code : _futuro.Code) + ", vence " + (VenceAm(z) ? "9:30" : "16:00") + " NY");
                }
                lock (_llave) { _estado.Series = elegidasSeries.Count; _estado.Contratos = ops.Count; }
                if (ops.Count == 0) { L("las series vinieron sin contratos"); return; }

                // 6) la ventana: todo a +-densa %, uno de cada dos hasta +-rala %, por (fecha, trimestre), tope duro
                var elegidos = Ventana(ops, Futuro);
                CentroVentana = Futuro;

                // 7) soltar lo que sale, pedir lo que entra
                List<Entrada> soltar; List<(Security op, bool delAnt, bool am)> pedir;
                lock (_llave)
                {
                    var nuevos = new HashSet<string>(elegidos.Select(x => x.op.Code ?? ""), StringComparer.OrdinalIgnoreCase);
                    soltar = _entradas.Values.Where(e => !nuevos.Contains(e.Op.Code ?? "")).ToList();
                    pedir = elegidos.Where(x => !_entradas.ContainsKey(x.op.Code ?? "")).ToList();
                    // los que siguen, actualizan sus hechos (por si cambio el roll)
                    foreach (var x in elegidos) if (_entradas.TryGetValue(x.op.Code ?? "", out var e)) { e.DelAnterior = x.delAnt; e.VenceALaManana = x.am; }
                }
                foreach (var e in soltar)
                {
                    try { e.Sub?.Dispose(); } catch (Exception ex) { L("al soltar " + e.Op.Code + ": " + ex.Message); }
                    lock (_llave) _entradas.Remove(e.Op.Code ?? "");
                }
                if (soltar.Count > 0) L("soltados " + soltar.Count + " contratos que salieron de la ventana (la API los retiene 5 s minimo antes de desuscribir)");

                if (pedir.Count > 0)
                {
                    DateTime turno;
                    lock (_llaveGlobal)
                    {
                        var desde = _proximoTurnoGlobal > DateTime.UtcNow ? _proximoTurnoGlobal : DateTime.UtcNow;
                        turno = desde; _proximoTurnoGlobal = desde.AddSeconds(ESPACIO_GLOBAL_S);
                    }
                    double faltan = (turno - DateTime.UtcNow).TotalSeconds;
                    if (faltan > 0) { L("espero " + faltan.ToString("0") + " s: otra instancia acaba de suscribir"); await Task.Delay(TimeSpan.FromSeconds(faltan)).ConfigureAwait(false); }

                    int ok = 0, rech = 0; string primerError = null; var ejemplos = new List<string>();
                    foreach (var x in pedir)
                    {
                        if (_parada) break;
                        try
                        {
                            var sub = _puerta.Suscribir(x.op);
                            // el Summary que devuelve Subscribe puede ser una foto vieja de la cache de la API: se usa (puntas, OI) pero NO
                            // cuenta como "dato recibido ahora" (UltimoUtc queda en blanco hasta el primer Changed), para que la edad
                            // del dato en pantalla no parezca fresca el primer minuto sin serlo
                            var e = new Entrada { Op = x.op, Sub = sub, DelAnterior = x.delAnt, VenceALaManana = x.am, Resumen = sub.Summary };
                            sub.Changed += AlCambio;
                            lock (_llave) _entradas[x.op.Code ?? ""] = e;
                            ok++;
                        }
                        catch (Exception ex)
                        {
                            rech++;
                            var msg = (ex.InnerException ?? ex).Message;
                            primerError ??= ex.GetType().Name + ": " + msg;
                            if (ejemplos.Count < 3) ejemplos.Add(x.op.Code + " -> " + msg);
                            // si la API dice que esta instancia esta al tope o drenada, seguir pidiendo es gastar cuota
                            if (ex is InvalidOperationException && rech >= 5 && ok == 0) break;
                        }
                    }
                    lock (_llave) { _estado.Rechazadas += rech; }
                    L("suscripcion: " + ok + " aceptadas, " + rech + " rechazadas" + (primerError != null ? " (primera: " + primerError + ")" : "") + (ejemplos.Count > 0 ? " [" + string.Join("; ", ejemplos) + "]" : ""));
                }
                else L("ventana sin cambios: " + elegidos.Count + " contratos ya suscritos");

                lock (_llave) { _estado.UltimoArmadoUtc = DateTime.UtcNow; _estado.Texto = "armada: " + _entradas.Count + " contratos, centro " + Futuro.ToString("0.##", CultureInfo.InvariantCulture); }
                Recontar();
                L("armado listo: " + Foto().Suscritos + " suscritos (tope " + TopeContratos + "), ventana +-" + VentanaDensaPct.ToString("0.##", CultureInfo.InvariantCulture) + " % densa / +-" + VentanaRalaPct.ToString("0.##", CultureInfo.InvariantCulture) + " % rala, centro " + Futuro.ToString("0.##", CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                lock (_llave) { _estado.Errores++; _estado.Texto = "error: " + e.Message; }
                log?.Invoke("[cadena api] EXCEPCION en Arrancar: " + e.GetType().Name + ": " + e.Message + " | " + (e.StackTrace ?? "").Replace("\n", " ").Substring(0, Math.Min(400, (e.StackTrace ?? "").Length)));
            }
            finally { Interlocked.Exchange(ref _armando, 0); }
        }

        /// <summary>Rearma con la ventana centrada en el precio actual (sin esperar los 60 s entre intentos).</summary>
        public Task Rearmar(object proveedor, object manager, object dataProvider)
            => Arrancar(proveedor, manager, dataProvider, _codigoGrafico, _precioGrafico, _log, forzar: true);

        /// <summary>Hace falta recentrar: el precio se fue mas de RecentrarPct del centro de la ventana.</summary>
        public bool HayQueRecentrar()
        {
            if (CentroVentana <= 0 || Futuro <= 0) return false;
            return Math.Abs(Futuro - CentroVentana) > CentroVentana * RecentrarPct / 100.0;
        }

        // ------------------------------------------------------------------
        // la puerta: como se consigue
        // ------------------------------------------------------------------

        private void ResolverPuerta(object proveedor, object manager, object dataProvider, Action<string> L)
        {
            var prov = proveedor as IOptionsDataProvider;
            bool disponible = false;
            try { disponible = prov != null && prov.IsAvailable; } catch (Exception e) { L("IsAvailable tiro: " + e.Message); }
            lock (_llave) _estado.ApiDisponible = disponible;
            L("IOptionsDataProvider: " + (prov == null ? "null (ATAS no se lo dio al indicador)" : prov.GetType().FullName + ", IsAvailable=" + disponible));

            // A) el servicio publico, por TIPO, adentro del proveedor; y el conector por los caminos que el propio proveedor usa
            if (prov != null)
            {
                try
                {
                    var (servicio, conn, camino) = ArmarServicio(prov, manager, dataProvider, L);
                    if (servicio != null && conn != null)
                    {
                        _puerta = new PuertaServicio(servicio, conn, this);
                        _conn = conn;
                        lock (_llave) { _estado.Via = "servicio"; _estado.Conector = conn.GetType().Name; }
                        L("puerta: OptionsSubscriptionService + conector " + conn.GetType().Name + " (" + camino + ")");
                        return;
                    }
                    L("servicio: " + (servicio == null ? "no encontre un campo de tipo OptionsSubscriptionService en el proveedor" : "sin conector que sirva opciones"));
                }
                catch (Exception e) { L("no pude armar la puerta por el servicio: " + e.GetType().Name + ": " + e.Message); }
            }

            // B) el proveedor directo: solo sirve si el grafico ya es el futuro grande
            if (prov != null && disponible)
            {
                _puerta = new PuertaProveedor(prov);
                _conn = BuscarConectorSinServicio(manager, dataProvider);
                lock (_llave) { _estado.Via = "proveedor"; _estado.Conector = _conn?.GetType().Name ?? "?"; }
                L("puerta: el proveedor del indicador (series del grafico " + _codigoGrafico + ")" + (_conn == null ? "; sin conector para el precio del futuro: se usa el cierre del grafico" : ""));
            }
        }

        /// <summary>
        /// Saca de IndicatorOptionsDataProvider (por reflexion, por TIPO de campo) el OptionsSubscriptionService
        /// y un conector que sirva opciones. Va en un metodo aparte para que, si OFT.Platform.Core cambiara y el
        /// tipo no cargara, la excepcion salte aca y la atrape el que llama (ATAS no muestra nada por su cuenta).
        /// </summary>
        private (OptionsSubscriptionService, IDataFeedConnector, string) ArmarServicio(IOptionsDataProvider prov, object manager, object dataProvider, Action<string> L)
        {
            OptionsSubscriptionService servicio = null;
            ITradingManager tm = manager as ITradingManager;
            object tradingCore = null, instrumento = null; IDataFeedConnector connCache = null;
            // POR TIPO DECLARADO DEL CAMPO, nunca por nombre (estan ofuscados). Ojo con el orden: IPlatformTradingCore
            // tambien implementa IDataFeedConnector, asi que el conector cacheado se reconoce por el tipo EXACTO del campo.
            var campos = new List<string>();
            foreach (var f in CamposDe(prov.GetType()))
            {
                object v; try { v = f.GetValue(prov); } catch { continue; }
                campos.Add(f.FieldType.Name + (v == null ? "=null" : ""));
                if (f.FieldType == typeof(OptionsSubscriptionService)) servicio = v as OptionsSubscriptionService;
                else if (f.FieldType == typeof(IDataFeedConnector)) connCache = v as IDataFeedConnector;
                else if (typeof(ITradingManager).IsAssignableFrom(f.FieldType)) { if (tm == null) tm = v as ITradingManager; }
                else if (f.FieldType.Name == "Instrument") instrumento = v;
                else if (f.FieldType.Name.Contains("TradingCore")) tradingCore = v;
            }
            L("campos del proveedor: " + string.Join(", ", campos));
            if (servicio == null) return (null, null, "");

            // 1) como el proveedor: tradingCore.GetConnector(portfolio del manager), o GetConnector(security del grafico)
            try
            {
                if (tradingCore != null)
                {
                    var port = tm?.Portfolio;
                    var mP = tradingCore.GetType().GetMethod("GetConnector", new[] { typeof(Portfolio) });
                    if (port != null && mP?.Invoke(tradingCore, new object[] { port }) is IDataFeedConnector c1 && servicio.TryGetFeed(c1, out _)) return (servicio, c1, "tradingCore.GetConnector(portfolio)");
                    var sec = tm?.Security;
                    var mS = tradingCore.GetType().GetMethod("GetConnector", new[] { typeof(Security) });
                    if (sec != null && mS?.Invoke(tradingCore, new object[] { sec }) is IDataFeedConnector c2 && servicio.TryGetFeed(c2, out _)) return (servicio, c2, "tradingCore.GetConnector(security)");
                    if (port == null && sec == null) L("el TradingManager no tiene portfolio ni security todavia");
                }
            }
            catch (Exception e) { L("GetConnector fallo: " + (e.InnerException ?? e).Message); }
            // 2) el conector que el proveedor dejo cacheado
            if (connCache != null && servicio.TryGetFeed(connCache, out _)) return (servicio, connCache, "campo cacheado del proveedor");
            // 3) Instrument.SecuritiesByConnector: las claves son conectores
            try
            {
                var p = instrumento?.GetType().GetProperty("SecuritiesByConnector");
                if (p?.GetValue(instrumento) is IEnumerable en)
                {
                    foreach (var kv in en)
                    {
                        var k = kv?.GetType().GetProperty("Key")?.GetValue(kv) as IDataFeedConnector;
                        if (k != null && servicio.TryGetFeed(k, out _)) return (servicio, k, "Instrument.SecuritiesByConnector");
                    }
                }
            }
            catch (Exception e) { L("SecuritiesByConnector fallo: " + e.Message); }
            // 4) ultimo recurso: rastrear los campos privados del DataProvider / TradingManager (como CadenaViva 1.x).
            //    TryGetFeed ya descarta el PlatformTradingCore (no implementa IOptionsDataFeed) y los conectores desconectados.
            Func<object, bool> sirve = o => o is IDataFeedConnector c && !(o.GetType().Name.Contains("TradingCore")) && servicio.TryGetFeed(c, out _);
            var hallado = Rastrear(dataProvider, sirve, 0, new HashSet<object>(ReferenceEqualityComparer.Instance), 20000)
                       ?? Rastrear(manager, sirve, 0, new HashSet<object>(ReferenceEqualityComparer.Instance), 20000);
            if (hallado is IDataFeedConnector hc) return (servicio, hc, "rastreo por reflexion");
            return (servicio, null, "");
        }

        private IDataFeedConnector BuscarConectorSinServicio(object manager, object dataProvider)
        {
            try
            {
                Func<object, bool> es = o => o is IDataFeedConnector c && c.IsConnected && !(o is ITradingManager) && !o.GetType().Name.Contains("TradingCore");
                return (Rastrear(dataProvider, es, 0, new HashSet<object>(ReferenceEqualityComparer.Instance), 20000)
                     ?? Rastrear(manager, es, 0, new HashSet<object>(ReferenceEqualityComparer.Instance), 20000)) as IDataFeedConnector;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------------
        // futuro, series, contratos
        // ------------------------------------------------------------------

        private async Task<Security> BuscarFuturo(string grande, string codigoGrafico, Security delGrafico, Action<string> L)
        {
            var hoy = RelojNy.Hoy();
            // puerta = proveedor (el grafico ya es NQ/ES): el futuro es la Security del grafico, que es la que el proveedor usa
            if (_puerta != null && !_puerta.SirveParaOtroSubyacente && delGrafico != null && delGrafico.Type == SecType.Future
                && string.Equals(RaizDe(delGrafico.Code), grande, StringComparison.OrdinalIgnoreCase))
            { L("futuro = el del grafico: " + delGrafico.Code); return delGrafico; }
            if (_conn == null)
            {
                L("sin conector: no puedo buscar " + grande + " en el catalogo" + (delGrafico != null ? " (el grafico es " + delGrafico.Code + ", no " + grande + ")" : ""));
                return null;
            }
            List<Security> todas;
            try { todas = (_conn.Securities ?? Enumerable.Empty<Security>()).ToList(); } catch { todas = new List<Security>(); }
            var preferidos = CodigosGrande(codigoGrafico, grande);
            string pref = preferidos.FirstOrDefault() ?? "";
            int Pref(Security x) => string.Equals(x.Code, pref, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            var local = todas.Where(x => x.Type == SecType.Future && string.Equals(RaizDe(x.Code), grande, StringComparison.OrdinalIgnoreCase) && x.Expiration.Date >= hoy.AddDays(-1))
                             .OrderBy(Pref).ThenBy(x => x.Expiration).FirstOrDefault();
            if (local != null) { L("futuro local: " + local.Code + " (vence " + local.Expiration.ToString("yyyy-MM-dd") + ")"); return local; }
            foreach (var cod in preferidos)
            {
                try
                {
                    var r = await _conn.SearchSecuritiesAsync(new SecurityFilter { Code = cod, Exchange = "CME", RequestId = DateTime.UtcNow.Ticks % 1000000000L }).ConfigureAwait(false);
                    var lista = (r ?? Enumerable.Empty<Security>()).ToList();
                    var hit = lista.FirstOrDefault(x => string.Equals(x.Code, cod, StringComparison.OrdinalIgnoreCase))
                           ?? lista.Where(x => string.Equals(RaizDe(x.Code), grande, StringComparison.OrdinalIgnoreCase) && x.Type == SecType.Future && x.Expiration.Date > hoy).OrderBy(x => x.Expiration).FirstOrDefault();
                    if (hit != null) { L("futuro del servidor: " + hit.Code + " (pedido " + cod + ", " + lista.Count + " devueltos)"); return hit; }
                    L("el servidor no devolvio " + cod + " (" + lista.Count + " devueltos)");
                }
                catch (Exception e) { L("no pude buscar " + cod + ": " + e.Message); }
            }
            return null;
        }

        private async Task<List<OptionSeries>> SeriesDe(Security sub, Action<string> L)
        {
            string llave = sub?.Code ?? "";
            lock (_llave) if (_cacheSeries.TryGetValue(llave, out var c) && (DateTime.UtcNow - c.cuando).TotalMinutes < MINUTOS_CACHE_LOOKUPS) return c.lista;
            var salida = new List<OptionSeries>();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
                var r = await _puerta.Series(sub, cts.Token).ConfigureAwait(false);
                salida = (r ?? Array.Empty<OptionSeries>()).ToList();
                lock (_llave) _estado.Lookups++;
                L(salida.Count + " series para " + llave + (salida.Count > 0 ? ": " + string.Join(", ", salida.OrderBy(z => z.Expiration).Take(8).Select(z => z.Expiration.ToString("MM-dd") + " " + z.Type)) + (salida.Count > 8 ? ", ..." : "") : ""));
                if (salida.Count > 0) lock (_llave) _cacheSeries[llave] = (DateTime.UtcNow, salida);
            }
            catch (Exception e) { lock (_llave) _estado.Errores++; L("series de " + llave + " fallaron: " + e.GetType().Name + ": " + (e.InnerException ?? e).Message); }
            return salida;
        }

        private async Task<List<Security>> OpcionesDe(OptionSeries serie, Security sobre, Action<string> L)
        {
            // si la serie es del trimestre que vence pero vino listada bajo el nuevo, se pide con el codigo del viejo
            if (sobre != null && !string.Equals(serie.UnderlyingCode, sobre.Code, StringComparison.OrdinalIgnoreCase))
                serie = new OptionSeries { Code = serie.Code, Exchange = serie.Exchange, UnderlyingCode = sobre.Code, Expiration = serie.Expiration, Type = serie.Type };
            string llave = LlaveSerie(serie) + "@" + serie.UnderlyingCode;
            lock (_llave) if (_cacheOps.TryGetValue(llave, out var c) && (DateTime.UtcNow - c.cuando).TotalMinutes < MINUTOS_CACHE_LOOKUPS) return c.lista;
            var salida = new List<Security>();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
                var r = await _puerta.Opciones(serie, cts.Token).ConfigureAwait(false);
                salida = (r ?? Array.Empty<Security>()).ToList();
                lock (_llave) _estado.Lookups++;
                if (salida.Count > 0) lock (_llave) _cacheOps[llave] = (DateTime.UtcNow, salida);
                else L("0 contratos para " + serie.Expiration.ToString("MM-dd") + " " + serie.Type + " sobre " + serie.UnderlyingCode);
            }
            catch (Exception e) { lock (_llave) _estado.Errores++; L("contratos de " + serie.Expiration.ToString("MM-dd") + " " + serie.Type + " sobre " + serie.UnderlyingCode + " fallaron: " + e.GetType().Name + ": " + (e.InnerException ?? e).Message); }
            return salida;
        }

        /// <summary>Todos los strikes a +-densa %, uno de cada dos hasta +-rala %, por (fecha, trimestre), en strikes CRUDOS; tope duro.</summary>
        private List<(Security op, bool delAnt, bool am)> Ventana(List<(Security op, bool delAnt, bool am)> ops, double futuro)
        {
            var elegidos = new List<(Security op, bool delAnt, bool am)>();
            double rDenso = futuro * VentanaDensaPct / 100.0, rRalo = futuro * VentanaRalaPct / 100.0;
            foreach (var grupo in ops.GroupBy(x => (x.op.Expiration.Date, x.delAnt)).OrderBy(g => g.Key.Date))
            {
                double corr = grupo.Key.delAnt ? DesplazamientoAnterior : 0;
                double centro = futuro - corr;
                var ks = grupo.Select(x => (double)x.op.StrikePrice.Value).Distinct().OrderBy(k => k).ToList();
                if (ks.Count == 0) continue;
                var pasos = new List<double>(); for (int i = 1; i < ks.Count; i++) pasos.Add(ks[i] - ks[i - 1]);
                pasos.Sort(); double paso = pasos.Count > 0 ? pasos[pasos.Count / 2] : 5.0; if (paso <= 0) paso = 5.0;
                var dentro = new HashSet<double>();
                foreach (var k in ks)
                {
                    double d = Math.Abs(k - centro);
                    if (d <= rDenso) dentro.Add(k);
                    else if (d <= rRalo && Math.Abs(Math.Round(k / paso) % 2) < 0.01) dentro.Add(k);
                }
                elegidos.AddRange(grupo.Where(x => dentro.Contains((double)x.op.StrikePrice.Value)));
            }
            if (elegidos.Count > TopeContratos)
                elegidos = elegidos.OrderBy(x => x.op.Expiration.Date)
                                   .ThenBy(x => Math.Abs((double)x.op.StrikePrice.Value + (x.delAnt ? DesplazamientoAnterior : 0) - futuro))
                                   .Take(TopeContratos).ToList();
            return elegidos;
        }

        // ------------------------------------------------------------------
        // eventos y temporizador
        // ------------------------------------------------------------------

        /// <summary>El Changed de cada suscripcion: barato a proposito (guardar y contar). Nada de IV aca.</summary>
        private void AlCambio(IOptionQuoteSubscription s)
        {
            try
            {
                var code = s?.Option?.Code; if (string.IsNullOrEmpty(code)) return;
                Entrada e; lock (_llave) { if (!_entradas.TryGetValue(code, out e)) return; }
                e.Resumen = s.Summary; e.UltimoUtc = DateTime.UtcNow; Interlocked.Increment(ref e.Cambios);
                Interlocked.Increment(ref _evCambios);
                if (Interlocked.CompareExchange(ref _evPrimerResumen, 1, 0) == 0)
                {
                    var r = s.Summary;
                    _log?.Invoke("[cadena api] PRIMER resumen: " + code + " bid " + F(r?.BestBidPrice) + " x " + F(r?.BestBidVolume) + "  ask " + F(r?.BestAskPrice) + " x " + F(r?.BestAskVolume)
                        + "  last " + F(r?.LastTradePrice) + "  OI " + F(r?.OpenInterest) + "  volHoy " + F(r?.CurrentDayTotalVolume) + "  volAyer " + F(r?.PrevDayTotalVolume) + "  settle " + F(r?.SettlementPrice)
                        + "  | Security: bid " + F(s.Option.BestBidPrice) + " ask " + F(s.Option.BestAskPrice) + " OI " + F(s.Option.OpenInterest));
                }
            }
            catch { }
        }

        private static string F(decimal? v) => v.HasValue ? v.Value.ToString("0.####", CultureInfo.InvariantCulture) : "-";

        /// <summary>Cada 5 s desde el indicador: precio del futuro, spread del roll, conteos. Nunca tira.</summary>
        public void Latido()
        {
            try { RefrescarFuturo(); RefrescarDesplazamiento(); Recontar(); } catch { }
        }

        public long Cambios => Interlocked.Read(ref _evCambios);

        private double RefrescarFuturo()
        {
            var f = _futuro; if (f == null) return Futuro;
            double p = (double)(f.LastTradePrice ?? 0m);
            if (f.BestBidPrice > 0 && f.BestAskPrice >= f.BestBidPrice)
            {
                double mid = (double)((f.BestBidPrice + f.BestAskPrice) / 2m);
                // el punto medio manda de noche (un ultimo trade viejo contra un libro vivo mete ruido); si no hay puntas, el ultimo
                p = mid;
            }
            if (p > 0) Futuro = p;
            return Futuro;
        }

        private void RefrescarDesplazamiento()
        {
            var fa = _futuroAnterior; if (fa == null || Futuro <= 0) return;
            if (RelojNy.Ahora() >= fa.Expiration.Date.AddHours(9.5)) return;   // vencido: queda el ultimo spread
            double p = 0;
            if (fa.BestBidPrice > 0 && fa.BestAskPrice >= fa.BestBidPrice) p = (double)((fa.BestBidPrice + fa.BestAskPrice) / 2m);
            if (p <= 0) p = (double)(fa.LastTradePrice ?? 0m);
            if (p <= 0) return;
            _futuroAnteriorPrecio = p;
            double nuevo = Math.Round((Futuro - p) * 4) / 4;
            if (_desplazListo && Math.Abs(nuevo - DesplazamientoAnterior) < 1.0) return;   // histeresis de 1 pt
            DesplazamientoAnterior = nuevo; _desplazListo = true;
            if ((DateTime.UtcNow - _desplazLog).TotalMinutes >= 5)
            {
                _desplazLog = DateTime.UtcNow;
                _log?.Invoke("[cadena api] roll: " + fa.Code + " en " + p.ToString("0.##", CultureInfo.InvariantCulture) + " y " + (_futuro?.Code ?? "?") + " en " + Futuro.ToString("0.##", CultureInfo.InvariantCulture) + ": strikes del viejo corridos " + nuevo.ToString("+0.##;-0.##", CultureInfo.InvariantCulture) + " pts");
            }
        }

        private void Recontar()
        {
            lock (_llave)
            {
                int res = 0, puntas = 0, oi = 0; DateTime ult = _estado.UltimoResumenUtc;
                foreach (var e in _entradas.Values)
                {
                    var r = e.Resumen;
                    if (r != null) res++;
                    var (b, a) = Puntas(e);
                    if (b > 0 && a > 0 && a >= b) puntas++;
                    if ((r?.OpenInterest ?? e.Op.OpenInterest ?? 0m) > 0) oi++;
                    if (e.UltimoUtc > ult) ult = e.UltimoUtc;
                }
                _estado.ConResumen = res; _estado.ConPuntas = puntas; _estado.ConOI = oi; _estado.UltimoResumenUtc = ult; _estado.Suscritos = _entradas.Count;
            }
        }

        /// <summary>Las puntas: del Summary si vinieron; si no, del Security (la API suscribe solo Summary; la sonda mide cual trae).</summary>
        private static (double bid, double ask) Puntas(Entrada e)
        {
            var r = e.Resumen;
            double b = (double)(r?.BestBidPrice ?? 0m), a = (double)(r?.BestAskPrice ?? 0m);
            if (b <= 0 || a <= 0) { b = (double)(e.Op.BestBidPrice); a = (double)(e.Op.BestAskPrice); }
            return (b, a);
        }

        /// <summary>Cuantos contratos tienen puntas en el Summary y cuantos solo en el Security: para saber por donde llegan.</summary>
        public (int enResumen, int soloEnSecurity) OrigenDePuntas()
        {
            lock (_llave)
            {
                int r = 0, s = 0;
                foreach (var e in _entradas.Values)
                {
                    if ((e.Resumen?.BestBidPrice ?? 0m) > 0 && (e.Resumen?.BestAskPrice ?? 0m) > 0) r++;
                    else if (e.Op.BestBidPrice > 0 && e.Op.BestAskPrice > 0) s++;
                }
                return (r, s);
            }
        }

        // ------------------------------------------------------------------
        // lectura
        // ------------------------------------------------------------------

        /// <summary>
        /// Foto de la cadena AHORA, con la IV despejada del punto medio (Black-76). Incluye TODAS las filas
        /// suscritas (ConPuntas / IV NaN dicen que falta); el que escribe el viva filtra las que tienen IV.
        /// Vacia si no hay futuro.
        /// </summary>
        public List<Fila> Filas()
        {
            List<Entrada> es; double desplaz; bool desplazListo;
            lock (_llave) { es = _entradas.Values.ToList(); desplaz = DesplazamientoAnterior; desplazListo = _desplazListo; }
            RefrescarFuturo();
            if (Futuro <= 0 || es.Count == 0) return new List<Fila>();
            var ahoraNy = RelojNy.Ahora();
            var salida = new List<Fila>(es.Count);
            int vencidas = 0;
            foreach (var e in es)
            {
                var o = e.Op; var r = e.Resumen;
                if (e.DelAnterior && !desplazListo) continue;   // sin spread no se ubica
                double k0 = (double)(o.StrikePrice ?? 0m); if (k0 <= 0) continue;
                double K = k0 + (e.DelAnterior ? desplaz : 0);
                double dias = RelojNy.DiasAlVencimiento(o.Expiration.Date, e.VenceALaManana, ahoraNy);
                // VENCIDO = FUERA DEL LIBRO desde el minuto cero (16:00 NY la weekly, 9:30 la trimestral). Trampa de CLAUDE.md:
                // "siguen contando el 0DTE ya vencido despues del cierre: excluilo". La 2.0 (CadenaViva.cs:1158-1160) lo dejaba
                // 30 minutos con dias = 0, el nucleo lo aceptaba con T = piso de 1 minuto y la gamma del strike al dinero se
                // disparaba: entre 16:00 y 16:30 NY las dominantes y el zero salian de un vencimiento muerto. La SUSCRIPCION
                // sigue esos 30 min de gracia (Series vivas); la fila no se publica. Se cuentan para el log.
                if (dias <= 0) { vencidas++; continue; }
                var (bid, ask) = Puntas(e);
                bool conPuntas = bid > 0 && ask > 0 && ask >= bid;
                double mid = conPuntas ? (bid + ask) / 2.0 : 0;
                bool esCall = o.OptionType == OptionTypes.Call;
                double iv = conPuntas ? Black76.DespejarIV(mid, Futuro, K, RelojNy.AniosParaModelo(dias), esCall) : double.NaN;
                salida.Add(new Fila
                {
                    K = K, K0 = e.DelAnterior ? k0 : 0, Dias = dias, EsCall = esCall,
                    OI = (double)(r?.OpenInterest ?? o.OpenInterest ?? 0m),
                    IV = iv, Bid = bid, Ask = ask, Mid = mid, ConPuntas = conPuntas, ConResumen = r != null,
                    VolHoy = (double)(r?.CurrentDayTotalVolume ?? 0m), VolAyer = (double)(r?.PrevDayTotalVolume ?? 0m),
                    Last = (double)(r?.LastTradePrice ?? o.LastTradePrice ?? 0m),
                    BidVol = (double)(r?.BestBidVolume ?? 0m), AskVol = (double)(r?.BestAskVolume ?? 0m),
                    Codigo = o.Code ?? "",
                });
            }
            lock (_llave) _estado.Vencidas = vencidas;
            return salida;
        }

        // ------------------------------------------------------------------
        // parar
        // ------------------------------------------------------------------

        /// <summary>Suelta todo (ReleaseOwned + Dispose de cada suscripcion). El futuro NO se desuscribe: es un
        /// contrato y otros indicadores (prod, 2.0, el propio grafico) pueden estar leyendolo.</summary>
        public void Parar()
        {
            _parada = true;
            List<Entrada> es;
            lock (_llave) { es = _entradas.Values.ToList(); _entradas.Clear(); }
            foreach (var e in es) { try { if (e.Sub != null) e.Sub.Changed -= AlCambio; } catch { } }
            try { _puerta?.SoltarTodo(es.Select(e => e.Sub).Where(s => s != null)); } catch (Exception ex) { _log?.Invoke("[cadena api] al soltar todo: " + ex.Message); }
            _log?.Invoke("[cadena api] parada: " + es.Count + " suscripciones soltadas (la API las retiene 5 s minimo)");
            lock (_llave) { _estado.Texto = "parada"; _estado.Suscritos = 0; }
        }

        public void Dispose() => Parar();

        // ------------------------------------------------------------------
        // codigos (copiados de CadenaViva.cs: Raiz, CodigosGrande, CodigoTrimestreAnterior)
        // ------------------------------------------------------------------

        private static bool EsRegular(OptionSeries z) => z.Type == OptionSeriesType.Regular;
        private static string LlaveSerie(OptionSeries z) => z.Expiration.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + (EsRegular(z) ? "R" : "W");
        /// <summary>Antes del vencimiento del viejo: del viejo. El mismo dia: solo la Regular (9:30); la Weekly de la tarde es del nuevo.</summary>
        private static bool EsDelAnterior(OptionSeries z, Security anterior)
            => anterior != null && (z.Expiration.Date < anterior.Expiration.Date || (z.Expiration.Date == anterior.Expiration.Date && EsRegular(z)));

        /// <summary>"MNQZ6" / "#MNQ" / "NQZ6" -> "NQ"; "MESU6" -> "ES"; "M2K" -> "RTY"; otro -> "".</summary>
        public static string RaizGrande(string codigoOInstrumento)
        {
            var s = (codigoOInstrumento ?? "").ToUpperInvariant().TrimStart('#').Trim();
            if (s.StartsWith("MNQ") || s.StartsWith("NQ")) return "NQ";
            if (s.StartsWith("MES") || s.StartsWith("ES")) return "ES";
            if (s.StartsWith("M2K") || s.StartsWith("RTY")) return "RTY";
            return "";
        }

        private static string RaizDe(string codigo)
        {
            var c = (codigo ?? "").Trim().ToUpperInvariant();
            if (c.Length < 3) return c;
            int i = c.Length - 1;
            while (i >= 0 && char.IsDigit(c[i])) i--;
            if (i >= 0 && char.IsLetter(c[i])) i--;      // la letra del mes
            return i >= 0 ? c.Substring(0, i + 1) : c;
        }

        /// <summary>Del codigo del grafico al del grande y su trimestre siguiente: MNQZ6 -> NQZ6, NQH7 (y M2KU6 -> RTYU6, RTYZ6).
        /// Si el grafico no trae mes (instrumento "#MNQ"), devuelve la raiz sola.</summary>
        private static List<string> CodigosGrande(string codGrafico, string grande)
        {
            var salida = new List<string>();
            string cod = (codGrafico ?? "").ToUpperInvariant().TrimStart('#').Trim();
            if (cod.StartsWith("M2K")) cod = "RTY" + cod.Substring(3);
            else if (cod.Length > 1 && cod.StartsWith("M") && cod.Substring(1).StartsWith(grande)) cod = cod.Substring(1);
            if (!cod.StartsWith(grande) || cod.Length < grande.Length + 2) { salida.Add(grande); return salida; }
            salida.Add(cod);
            const string meses = "HMUZ";
            char m = cod[cod.Length - 2]; char y = cod[cod.Length - 1];
            int im = meses.IndexOf(m);
            if (im >= 0 && char.IsDigit(y))
            {
                int im2 = (im + 1) % 4; int y2 = (y - '0') + (im2 == 0 ? 1 : 0);
                salida.Add(grande + meses[im2] + (char)('0' + (y2 % 10)));
            }
            return salida;
        }

        /// <summary>NQZ6 -> NQU6, NQH7 -> NQZ6. "" si el codigo no es trimestral.</summary>
        private static string CodigoTrimestreAnterior(string cod)
        {
            cod = (cod ?? "").Trim().ToUpperInvariant();
            if (cod.Length < 3) return "";
            const string meses = "HMUZ";
            char m = cod[cod.Length - 2]; char y = cod[cod.Length - 1];
            int im = meses.IndexOf(m);
            if (im < 0 || !char.IsDigit(y)) return "";
            int im2 = (im + 3) % 4; int y2 = (y - '0') + (im == 0 ? -1 : 0);
            if (y2 < 0) y2 = 9;
            return cod.Substring(0, cod.Length - 2) + meses[im2] + (char)('0' + y2);
        }

        // ------------------------------------------------------------------
        // reflexion
        // ------------------------------------------------------------------

        private static IEnumerable<FieldInfo> CamposDe(Type t)
        {
            for (var b = t; b != null && b != typeof(object); b = b.BaseType)
                foreach (var f in b.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    yield return f;
        }

        /// <summary>Recorre campos privados (y colecciones) hasta 5 niveles buscando algo que cumpla "es". Con presupuesto. Como en 1.x.</summary>
        private static object Rastrear(object raiz, Func<object, bool> es, int nivel, HashSet<object> vistos, int presupuesto)
        {
            if (raiz == null || nivel > 5 || presupuesto <= 0) return null;
            try
            {
                if (es(raiz)) return raiz;
                if (!vistos.Add(raiz)) return null;
                var t = raiz.GetType();
                if (t.IsPrimitive || t.IsEnum || raiz is string || raiz is Delegate) return null;
                if (raiz is IDictionary dic)
                {
                    foreach (var it in dic.Values) { var r0 = Rastrear(it, es, nivel + 1, vistos, presupuesto - vistos.Count); if (r0 != null) return r0; }
                }
                else if (raiz is IEnumerable en)
                {
                    int i = 0;
                    foreach (var it in en) { if (i++ > 300) break; var r0 = Rastrear(it, es, nivel + 1, vistos, presupuesto - vistos.Count); if (r0 != null) return r0; }
                }
                foreach (var f in CamposDe(t))
                {
                    if (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string)) continue;
                    object v; try { v = f.GetValue(raiz); } catch { continue; }
                    if (v == null) continue;
                    var r = Rastrear(v, es, nivel + 1, vistos, presupuesto - vistos.Count);
                    if (r != null) return r;
                }
            }
            catch { }
            return null;
        }
    }
}
