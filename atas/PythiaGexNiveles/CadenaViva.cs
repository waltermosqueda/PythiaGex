using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ATAS.DataFeedsCore;

namespace PythiaGex
{
    /// <summary>
    /// LA CADENA DE OPCIONES DE ES, EN VIVO, POR RITHMIC.
    ///
    /// POR QUE EXISTE ESTE ARCHIVO
    ///
    /// Todo lo anterior se apoyaba en el CDN de CBOE, que llega 902 segundos
    /// tarde -- medido, catorce de catorce, sin dispersion. Para estructura eso
    /// alcanza y sobra: se midio que el retraso corre el zero gamma 0,29 puntos
    /// de media y no mueve NI UN PUNTO los muros. Pero el operador scalpea en
    /// minutos y segundos, y dijo con todas las letras que no le sirve. Es su
    /// decision y esta bien tomada: un dato que llega tarde obliga a confiar en
    /// que nada cambio en el medio, y esa confianza no se puede auditar.
    ///
    /// La sonda encontro la salida. El conector de Rithmic que ATAS ya tiene
    /// conectado entrega la cadena de opciones de ES completa:
    ///
    ///     11 vencimientos, 6938 contratos
    ///     0DTE (vence hoy) ....... 726 contratos
    ///     manana ................. 730
    ///     diarios de 5, 6, 7 y 8 dias
    ///
    /// Y suscribiendo 60 contratos 0DTE al dinero: 60 con interes abierto, 60
    /// con punta compradora, 60 con punta vendedora, a los quince segundos.
    /// Cotizaciones reales y ajustadas -- E1DU6 P7750 en 5,90/6,10.
    ///
    /// Asi que de aca sale todo en vivo: el precio del futuro, las puntas, el
    /// interes abierto. La volatilidad implicita NO viene servida, se despeja
    /// del punto medio de las puntas, que es mejor que recibirla cocinada
    /// porque se sabe exactamente de donde sale.
    ///
    /// BLACK-76, NO BLACK-SCHOLES
    ///
    /// Las opciones de ES son opciones SOBRE UN FUTURO, no sobre un contado.
    /// El modelo que corresponde es Black-76, donde el subyacente es el futuro
    /// y no hay que arrastrar dividendos ni costo de acarreo: ya estan adentro
    /// del precio del futuro. Usar Black-Scholes sobre contado aca seria una
    /// aproximacion sin motivo, teniendo el modelo exacto a mano.
    ///
    /// LO QUE ESTO NO ARREGLA
    ///
    /// El interes abierto sigue siendo de AYER, y lo es para todo el mundo,
    /// GEXbot incluido: la OCC lo consolida de noche. Eso no tiene solucion
    /// comprando nada. Lo que se gana aca es que el precio, las puntas y la
    /// volatilidad pasan a ser de este segundo.
    /// </summary>
    public sealed class CadenaViva : IDisposable
    {
        /// <summary>Una pata de la cadena, ya con la volatilidad despejada.</summary>
        public sealed class Fila
        {
            public double K;          // strike
            public double Dias;       // al vencimiento
            public double OI;         // interes abierto (de ayer, para todos)
            public double Bid, Ask, Mid;
            public double IV;         // despejada del punto medio, no servida
            public bool EsCall;
            public string Codigo = "";
            // CONTRATOS OPERADOS HOY EN ESE STRIKE, acumulados en vivo.
            // Es el unico dato del mapa que NO es de ayer, y es el punto ciego
            // de todos los tableros de GEX: construyen sobre interes abierto,
            // que la OCC consolida de noche.
            public double VolumenHoy;
            // DE DONDE SALE CADA NUMERO (agregado el 2026-09-06):
            //   VolumenDia  = CurrentDayTotalVolume del resumen que manda el
            //                 conector (SecuritySummaryChanged). Es el mismo
            //                 dato que muestra la columna Volume del Options
            //                 Board de ATAS. Incluye lo operado ANTES de que
            //                 nos suscribieramos. NaN si no llego resumen.
            //   VolCinta    = suma de las operaciones recibidas por NewTrades
            //                 desde la suscripcion, con su lado agresor.
            // VolumenHoy toma el del resumen si existe, y si no el de la cinta.
            public double VolumenDia = double.NaN;
            public double VolCinta, VolCompra, VolVenta;
            public double OIResumen = double.NaN;
        }

        /// <summary>Un bloque grande de opciones visto en el tape (para Gamma
        /// Hoy, 2026-09-07). GAMMAlito los llama Big Trades y los define como
        /// "operaciones institucionales en el mercado de opciones".</summary>
        public sealed class Grande
        {
            public DateTime Hora;      // UTC, cuando llego
            public double K;           // strike (precio de FUTURO, sin base)
            public bool EsCall, Compra;
            public double Contratos, Precio;
        }
        /// <summary>Tamaño minimo para anotar un bloque. 0 = no anotar.</summary>
        public double UmbralGrande;
        private readonly List<Grande> _grandes = new();
        public List<Grande> Grandes() { lock (_llave) return new List<Grande>(_grandes); }

        private readonly object _llave = new();
        private List<Security> _suscritos = new();
        // volumen acumulado por contrato, y la ultima operacion vista, para no
        // contar dos veces el mismo print
        private readonly Dictionary<string, double> _volHoy = new();
        private readonly Dictionary<string, (decimal p, decimal v)> _ultPrint = new();
        private readonly List<Security> _enganchados = new();
        // contadores del camino del volumen, para poder decir DONDE se corta
        private long _evAvisos, _evConVol, _evContados;
        // TODOS los avisos, sin filtrar por nombre de campo, y que campos son.
        //
        // El operador objeto -- con razon -- que la tabla de opciones de ATAS
        // SI se mueve de noche, asi que "cero avisos" no puede significar que
        // los contratos esten mudos. Y no lo significa: yo solo contaba los
        // avisos de OPERACION. Si la tabla se mueve por cotizacion (bid/ask) y
        // nunca hay operaciones, mi contador da cero sin que nada este roto...
        // pero tambien daria cero si estuviera escuchando el campo equivocado.
        // Contando todo y anotando los nombres, las dos cosas se separan.
        private long _evTodos;
        private readonly HashSet<string> _nombresVistos = new();
        // Y la prueba que no depende de eventos: leer el campo directo al armar
        // la cadena. Si algun contrato tiene LastTradeVolume > 0 y mi
        // acumulador sigue en cero, el roto soy yo, y queda probado sin esperar
        // a la rueda americana.
        private int _conUltimoVol;
        private double _maxUltimoVol;
        private IDataFeedConnector _conn;
        private Security _futuro;

        // LO QUE LLEGA POR EL CONECTOR, NO POR Security.
        //
        // Tres noches el acumulador dio cero y la explicacion no era el
        // horario: Security implementa INotifyPropertyChanged pero el conector
        // de Rithmic nunca dispara PropertyChanged (medido: 0 avisos de
        // cualquier tipo en 140 contratos con las puntas cambiando). El dato
        // viaja por OTRO lado, y se encontro volcando la API por reflexion el
        // 2026-09-06:
        //   IDataFeedConnector.SecuritySummaryChanged -> SecuritySummary con
        //       CurrentDayTotalVolume, PrevDayTotalVolume, OpenInterest,
        //       SettlementPrice. Es lo que el propio Options Board de ATAS
        //       consume (OptionModel.ProcessSummary).
        //   IDataFeedConnector.NewTrades -> cada operacion con Security,
        //       Price, Volume, Time y OrderDirection (el lado del agresor).
        // Los dos llegan para cualquier contrato suscrito con Prints|Summary.
        private readonly Dictionary<string, SecuritySummary> _resumen = new();
        private readonly Dictionary<string, (double compra, double venta, double total, long n)> _cinta = new();
        private readonly HashSet<long> _tradesVistos = new();
        private HashSet<string> _codigos = new();
        private long _evResumenes, _evTrades, _evTradesPropios;
        private bool _enganchadoConector;
        private volatile bool _armando;
        private static DateTime _ultimoIntento = DateTime.MinValue;

        /// <summary>Ultimo estado legible, para mostrar en pantalla sin mentir.</summary>
        public string Estado { get; private set; } = "sin arrancar";

        /// <summary>true solo si hay contratos suscritos Y estan llegando puntas.</summary>
        public bool Activa { get; private set; }

        /// <summary>Precio del futuro de la cadena, en vivo.</summary>
        public double Futuro { get; private set; }

        public DateTime UltimaLectura { get; private set; }

        /// <summary>Dias al vencimiento mas cercano que se pudo conseguir.
        /// Si no es 0 en pleno dia, el mapa NO es el del 0DTE y hay que decirlo.</summary>
        public int DiasReales { get; private set; } = -1;

        // ------------------------------------------------------------------
        // arranque
        // ------------------------------------------------------------------

        /// <summary>
        /// Engancha el conector y se suscribe a la ventana de strikes que
        /// interesa. Es lento (busca series y espera precios), asi que se
        /// llama UNA vez y en segundo plano.
        /// </summary>
        public async Task Arrancar(object proveedor, object manager, Security seguridad,
                                   string raiz, int diasMax, int strikesPorLado, int vencMax,
                                   int topeContratos, Action<string> log)
        {
            if (_armando) return;
            if ((DateTime.UtcNow - _ultimoIntento).TotalSeconds < 60) return;
            _ultimoIntento = DateTime.UtcNow;
            _armando = true;
            try
            {
                void L(string m) { Estado = m; log?.Invoke("[cadena viva] " + m); }

                var tOpt = Type.GetType("ATAS.DataFeedsCore.IOptionsDataFeed, ATAS.DataFeedsCore");
                // ATAS 8.0.14.399: el conector de Rithmic dejo de declarar IOptionsDataFeed pero sigue teniendo
                // GetOptionSeriesAsync/GetOptionsAsync (medido con atas/_api el 10-09). Se busca por FORMA:
                // cualquier IDataFeedConnector con esos dos metodos; la interfaz, si existe, tambien vale.
                Func<object, bool> es = o => (tOpt != null && tOpt.IsInstanceOfType(o)) || EsFeedOpciones(o);

                // GetService no la entrega a los indicadores (NotSupportedException,
                // verificado). Se rastrea el conector por los campos privados.
                // ATAS 8.0.14.399 (instalado el 09-09 a las 11:08) movio el conector: la busqueda por
                // campos privados a 3 niveles dejo de encontrarlo y la cadena viva se apago a las 11:57
                // sin avisar. Ahora se busca mas hondo (5 niveles, sin ciclos, adentro de colecciones) y,
                // si aun no aparece, en los campos ESTATICOS de los ensamblados de ATAS/OFT.
                _camino = "";
                _reloj = System.Diagnostics.Stopwatch.StartNew(); _presupuestoGlobal = 150000;
                bool honda = (DateTime.UtcNow - _ultimaHonda).TotalMinutes >= 5;
                object feed = Rastrear(proveedor, es, 0, "DataProvider")
                           ?? Rastrear(manager, es, 0, "TradingManager")
                           ?? Rastrear(seguridad, es, 0, "Security")
                           ?? (honda ? RastrearEstaticos(es) : null);
                if (honda) _ultimaHonda = DateTime.UtcNow;
                if (feed == null)
                {
                    L("no se encontro el conector de opciones (buscado a 5 niveles y en estaticos de ATAS/OFT)");
                    if (!_diagnosticado) { _diagnosticado = true; try { Diagnostico(proveedor, manager, seguridad, log); } catch (Exception e) { log?.Invoke("[cadena viva] diagnostico fallo: " + e.Message); } }
                    return;
                }
                L("conector de opciones encontrado en " + _camino + " (" + feed.GetType().FullName + ")");

                _conn = feed as IDataFeedConnector;
                if (_conn == null) { L("el conector no expone IDataFeedConnector"); return; }
                if (!_conn.IsConnected) { L("el conector no esta conectado"); return; }

                // EL FUTURO SALE DE LA RAIZ DEL GRAFICO, NO DE UNA CONSTANTE.
                //
                // Esto estaba escrito como StartsWith("ES") a secas: en un
                // grafico de NQ o MNQ se enganchaba igual a la cadena de ES,
                // los strikes no tenian nada que ver con el precio y no se
                // dibujaba nada. Ahora sirve para ES/MES, NQ/MNQ y RTY/M2K.
                //
                // Se prefiere SIEMPRE el contrato grande sobre el micro: el
                // grande lista los vencimientos diarios y el 0DTE, que es lo
                // que se opera. Medido: ESU6 da 11 vencimientos con 0DTE,
                // mientras que MNQU6 daba uno solo, el trimestral.
                string grande = raiz, micro = raiz == "ES" ? "MES"
                                            : raiz == "NQ" ? "MNQ"
                                            : raiz == "RTY" ? "M2K" : "M" + raiz;

                var todas = (_conn.Securities ?? Enumerable.Empty<Security>()).ToList();
                Security Buscar(string cod) =>
                    todas.Where(x => x.Type == SecType.Future
                                && string.Equals(Raiz(x.Code), cod, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(x => x.Expiration).FirstOrDefault();

                // CANDIDATOS DEL FUTURO GRANDE, EN ORDEN: el local por raiz; si no esta, el
                // servidor POR CODIGO DE CONTRATO (Code = "ESU6"), derivado del micro que si
                // esta local (MESU6 -> ESU6), y su trimestre siguiente (ESZ6). Medido el
                // 2026-09-08: el filtro Type+Exchange tira NullReference siempre, y Code = "ES"
                // devuelve la raiz sin series ("Get option series error: no data"): hace falta
                // el contrato con vencimiento. El micro queda de ultimo recurso (solo trimestral).
                var candidatos = new List<Security>();
                var localGrande = Buscar(grande);
                if (localGrande != null) candidatos.Add(localGrande);
                var microSec = Buscar(micro);
                if (localGrande == null && microSec != null)
                {
                    foreach (var cod in CodigosGrande(microSec.Code, grande))
                    {
                        try
                        {
                            var r = await _conn.SearchSecuritiesAsync(new SecurityFilter { Code = cod, Exchange = "CME", RequestId = DateTime.UtcNow.Ticks % 1000000000L })
                                .ConfigureAwait(false);
                            var lista = (r ?? Enumerable.Empty<Security>()).ToList();
                            var hit = lista.FirstOrDefault(x => string.Equals(x.Code, cod, StringComparison.OrdinalIgnoreCase))
                                   ?? lista.Where(x => string.Equals(Raiz(x.Code), grande, StringComparison.OrdinalIgnoreCase) && x.Expiration > DateTime.Now.Date)
                                           .OrderBy(x => x.Expiration).FirstOrDefault();
                            if (hit != null) { candidatos.Add(hit); L("del servidor: " + hit.Code + " (pedido " + cod + ", " + lista.Count + " devueltos)"); }
                            else L("el servidor no devolvio " + cod + " (" + lista.Count + " devueltos)");
                        }
                        catch (Exception e) { L("no se pudo buscar " + cod + ": " + e.Message); }
                    }
                }
                if (microSec != null) candidatos.Add(microSec);
                if (candidatos.Count == 0) { L("no esta el futuro de " + raiz + " en el catalogo"); return; }

                // LA FECHA DE HOY ES LA DE NUEVA YORK, NO LA DE ACA (entre medianoche y las 2
                // en Argentina en Nueva York todavia es el dia anterior).
                var hoy = HoyEnNuevaYork();
                List<Security> ops = new();
                List<OptionSeries> series = new(), todasSeries = new();
                foreach (var cand in candidatos)
                {
                    _futuro = cand; Futuro = 0; ops.Clear(); series.Clear(); todasSeries.Clear();
                    // EL PRECIO DE REFERENCIA ES EL DEL FUTURO DE LA CADENA.
                    try { _conn.SubscribeToMarketData(new[] { _futuro }, SubscriptionType.Prints | SubscriptionType.Best); }
                    catch { }
                    for (int i = 0; i < 15 && Futuro <= 0; i++)
                    {
                        await Task.Delay(1000).ConfigureAwait(false);
                        Futuro = (double)(_futuro.LastTradePrice ?? 0m);
                        if (Futuro <= 0 && _futuro.BestBidPrice > 0 && _futuro.BestAskPrice > 0)
                            Futuro = (double)((_futuro.BestBidPrice + _futuro.BestAskPrice) / 2m);
                    }
                    if (Futuro <= 0) { L("no llego el precio de " + _futuro.Code); continue; }
                    L("futuro " + _futuro.Code + " en " + Futuro.ToString("0.##", CultureInfo.InvariantCulture));
                    try
                    {
                        var ss = await ((dynamic)feed).GetOptionSeriesAsync(_futuro);
                        todasSeries = ((IEnumerable<OptionSeries>)ss)
                                      .Where(z => (z.Expiration.Date - hoy).Days >= 0)
                                      .OrderBy(z => z.Expiration).ToList();
                        series = todasSeries.Where(z => (z.Expiration.Date - hoy).Days <= diasMax).ToList();
                        // ANTES DE NO DIBUJAR NADA, DIBUJAR LO QUE HAY Y DECIRLO.
                        if (series.Count == 0 && todasSeries.Count > 0)
                        {
                            series = todasSeries.Take(Math.Max(1, vencMax)).ToList();
                            DiasReales = (series[0].Expiration.Date - hoy).Days;
                            L("sin vencimientos a " + diasMax + " dias en " + _futuro.Code + "; el mas cercano esta a "
                              + DiasReales + ": se usa igual y se avisa en pantalla");
                        }
                        else if (series.Count > 0)
                            DiasReales = (series[0].Expiration.Date - hoy).Days;
                        foreach (var serie in series)
                        {
                            var cc = await ((dynamic)feed).GetOptionsAsync(serie);
                            ops.AddRange(((IEnumerable<Security>)cc).Where(o => o.StrikePrice.HasValue));
                        }
                        L(series.Count + " vencimientos, " + ops.Count + " contratos (" + _futuro.Code + ")");
                    }
                    catch (Exception e) { L("no se pudieron listar las series de " + _futuro.Code + ": " + e.Message); continue; }
                    bool ultimo = ReferenceEquals(cand, candidatos[candidatos.Count - 1]);
                    if (ops.Count > 0 && (DiasReales <= diasMax || ultimo)) break;
                    if (ops.Count > 0) L(_futuro.Code + " solo lista vencimientos lejanos: pruebo el siguiente candidato");
                }
                if (ops.Count == 0) { L("las series vinieron vacias"); return; }

                // VENTANA ALREDEDOR DEL DINERO.
                //
                // Pedir los casi siete mil de golpe es maltratar el feed sin
                // necesidad: los strikes lejanos no mueven la aguja del GEX y
                // igual habria que descartarlos. Se toman los N de cada lado
                // POR VENCIMIENTO, para que el 0DTE no se coma toda la cuota.
                // CUIDADO CON LA CUOTA: ESTO LE COMPITE AL FEED DE FUTUROS.
                //
                // La primera version se suscribia a 40 strikes por lado en cada
                // uno de los 6 vencimientos: unos 960 contratos. ATAS empezo a
                // mostrar "Market Data Latency: 7772 ms" -- o sea que la cinta
                // de futuros, que es con la que se opera, llegaba casi ocho
                // segundos tarde. Inaceptable: el indicador no puede degradar
                // justo el dato que vino a mejorar.
                //
                // Se recorta a los vencimientos mas cercanos y a una ventana
                // angosta. Los strikes lejanos no mueven la aguja del GEX: su
                // gamma es practicamente cero y solo gastan ancho de banda.
                var fechas = ops.Select(o => o.Expiration.Date).Distinct()
                                .OrderBy(d => d).Take(Math.Max(1, vencMax)).ToList();
                // MALLA NO UNIFORME: DENSA AL DINERO, RALA LEJOS.
                //
                // Con una ventana pareja de +/-60 puntos el zero gamma quedaba
                // afuera -- suele estar 70 puntos por debajo del precio -- y el
                // tablero mostraba "--" porque la suma nunca cruzaba cero
                // dentro de lo observado. Ampliarla pareja gastaria el triple de
                // ancho de banda en strikes cuya gamma es casi cero.
                //
                // Cerca del dinero se toman TODOS los strikes, que es donde
                // vive la gamma. De ahi para afuera uno cada varios, que
                // alcanza para que la suma cruce y para ver los muros lejanos.
                var elegidos = new List<Security>();
                foreach (var f2 in fechas)
                {
                    var grupo = ops.Where(o => o.Expiration.Date == f2).ToList();
                    var todosK = grupo.Select(o => (double)(o.StrikePrice ?? 0m))
                                      .Distinct().OrderBy(k => k).ToList();
                    if (todosK.Count == 0) continue;
                    // paso tipico de la cadena, medido y no supuesto
                    var pasos = new List<double>();
                    for (int i = 1; i < todosK.Count; i++) pasos.Add(todosK[i] - todosK[i-1]);
                    pasos.Sort();
                    double paso = pasos.Count > 0 ? pasos[pasos.Count / 2] : 5.0;
                    if (paso <= 0) paso = 5.0;

                    double radioDenso = paso * strikesPorLado;         // todos
                    double radioRalo  = paso * strikesPorLado * 4;     // uno cada 4
                    var ks = new HashSet<double>();
                    foreach (var k in todosK)
                    {
                        double d = Math.Abs(k - Futuro);
                        if (d <= radioDenso) ks.Add(k);
                        else if (d <= radioRalo && Math.Abs((k / paso) % 4) < 0.01) ks.Add(k);
                    }
                    elegidos.AddRange(grupo.Where(o => ks.Contains((double)(o.StrikePrice ?? 0m))));
                }
                if (elegidos.Count > topeContratos)
                {
                    // Si hay que recortar se sacan los del vencimiento mas lejano
                    // primero: el 0DTE es el que manda el GEX intradia.
                    elegidos = elegidos
                        .OrderBy(o => o.Expiration.Date)
                        .ThenBy(o => Math.Abs((double)(o.StrikePrice ?? 0m) - Futuro))
                        .Take(topeContratos).ToList();
                }

                try
                {
                    _conn.SubscribeToMarketData(elegidos,
                        SubscriptionType.Prints | SubscriptionType.Best | SubscriptionType.Summary);
                }
                catch (Exception e) { L("la suscripcion fallo: " + e.Message); return; }

                lock (_llave)
                {
                    _suscritos = elegidos;
                    _codigos = new HashSet<string>(elegidos.Select(x => x.Code ?? "")
                                                           .Where(x => x.Length > 0));
                }

                // EL ENGANCHE QUE SI FUNCIONA: LOS EVENTOS DEL CONECTOR.
                // Una sola vez por instancia; los manejadores filtran por el
                // codigo del contrato contra _codigos, que se rearma en cada
                // suscripcion. Ver el comentario de los campos.
                if (!_enganchadoConector)
                {
                    try
                    {
                        _conn.SecuritySummaryChanged += AlResumen;
                        _conn.NewTrades += AlTrades;
                        _enganchadoConector = true;
                        L("enganchados SecuritySummaryChanged y NewTrades del conector");
                    }
                    catch (Exception e) { L("no se pudo enganchar el conector: " + e.Message); }
                }

                // EL VOLUMEN DE HOY, POR EVENTO Y NO POR SONDEO.
                //
                // Security no tiene un campo de volumen acumulado de la sesion:
                // solo LastTradeVolume, que es el de la ULTIMA operacion. Asi
                // que hay que sumarlo a mano.
                //
                // Se hace por evento porque Security implementa
                // INotifyPropertyChanged (verificado volcando la API): con
                // sondeo cada N segundos se perderian todas las operaciones que
                // ocurran entre dos consultas, que en un 0DTE al dinero son
                // casi todas.
                foreach (var sec in elegidos) EngancharVolumen(sec);
                L("suscritos " + elegidos.Count + " contratos, esperando puntas");

                // ESPERAR A QUE LLEGUEN DE VERDAD, CON PACIENCIA.
                //
                // En ES las puntas entran en quince segundos. En MNQ, cuyo
                // unico vencimiento listado es el trimestral, el mercado de
                // opciones es mucho mas flaco y tarda. Con la espera corta y el
                // umbral del 10 % se daba por perdida una cadena que si estaba
                // llegando, solo que despacio.
                // EL MINIMO NO PUEDE SER CUALQUIERA.
                //
                // Bajarlo a 6 hizo que la cadena de MNQ se declarara "EN VIVO"
                // con 10 patas de 142, y con eso no se puede armar un perfil de
                // gamma: hacen falta varios STRIKES con call y put. Peor
                // todavia, al darse por viva bloqueaba la caida a CBOE y el
                // indicador no dibujaba nada. Mejor decir que no se pudo.
                int minimo = Math.Max(24, elegidos.Count / 6);
                for (int i = 0; i < 40; i++)
                {
                    await Task.Delay(1500).ConfigureAwait(false);
                    int conPunta = elegidos.Count(x => x.BestBidPrice > 0 && x.BestAskPrice > 0);
                    if (conPunta >= minimo)
                    {
                        Activa = true;
                        L("EN VIVO: " + conPunta + " de " + elegidos.Count + " con las dos puntas");
                        return;
                    }
                }
                int fin = elegidos.Count(x => x.BestBidPrice > 0 && x.BestAskPrice > 0);
                L("suscrito pero solo " + fin + " de " + elegidos.Count +
                  " con las dos puntas (hacian falta " + minimo + "); se reintenta");
            }
            catch (Exception e) { Estado = "error al arrancar: " + e.Message; }
            finally { _armando = false; }
        }

        /// <summary>
        /// Suma cada operacion que se imprima en ese contrato.
        ///
        /// Se compara contra la ultima vista (precio Y volumen) porque
        /// PropertyChanged puede dispararse mas de una vez por el mismo print
        /// -- por ejemplo si cambian precio y volumen en dos avisos separados --
        /// y sin ese control el volumen del dia saldria inflado.
        /// </summary>
        private void EngancharVolumen(Security sec)
        {
            if (sec == null || string.IsNullOrEmpty(sec.Code)) return;
            try
            {
                sec.PropertyChanged += (o, e) =>
                {
                    var n = e?.PropertyName;
                    System.Threading.Interlocked.Increment(ref _evTodos);
                    if (n != null)
                        lock (_llave) { if (_nombresVistos.Count < 25) _nombresVistos.Add(n); }
                    if (n != "LastTradeVolume" && n != "LastTradePrice") return;
                    // UN CONTADOR EN CADA ESCALON DEL CAMINO.
                    //
                    // El acumulador dio cero dos noches seguidas y hay dos
                    // explicaciones que NO se pueden distinguir sin
                    // operaciones: o de noche no se opera, o esto esta roto.
                    // Con los contadores el registro lo dice solo:
                    //   avisos=0                    -> no llega nada: es la suscripcion
                    //   avisos>0 y convol=0         -> LastTradeVolume viene vacio
                    //   convol>0 y contados=0       -> el filtro de duplicado se come todo
                    //   contados>0 y flujoviva=0    -> la clave de guardado no coincide
                    //                                  con la de lectura
                    System.Threading.Interlocked.Increment(ref _evAvisos);
                    try
                    {
                        var s2 = o as Security; if (s2 == null) return;
                        var p = s2.LastTradePrice ?? 0m;
                        var v = s2.LastTradeVolume ?? 0m;
                        if (v <= 0) return;
                        System.Threading.Interlocked.Increment(ref _evConVol);
                        lock (_llave)
                        {
                            if (_ultPrint.TryGetValue(s2.Code, out var ant)
                                && ant.p == p && ant.v == v) return;
                            _ultPrint[s2.Code] = (p, v);
                            _volHoy[s2.Code] = (_volHoy.TryGetValue(s2.Code, out var a) ? a : 0) + (double)v;
                            _evContados++;
                        }
                    }
                    catch { }
                };
                lock (_llave) _enganchados.Add(sec);
            }
            catch { }
        }

        /// <summary>El resumen diario de un contrato: se guarda el ultimo por codigo.</summary>
        private void AlResumen(IDataFeedConnector c, SecuritySummary s)
        {
            try
            {
                var code = s?.Security?.Code;
                if (string.IsNullOrEmpty(code)) return;
                lock (_llave)
                {
                    if (!_codigos.Contains(code)) return;
                    _evResumenes++;
                    _resumen[code] = s;
                }
            }
            catch { }
        }

        /// <summary>Cada operacion de opciones, con su lado. Se descarta el
        /// duplicado por Id cuando el feed lo trae.</summary>
        private void AlTrades(IDataFeedConnector c, IEnumerable<Trade> ts)
        {
            if (ts == null) return;
            try
            {
                foreach (var t in ts)
                {
                    if (t == null) continue;
                    Interlocked.Increment(ref _evTrades);
                    var code = t.Security?.Code;
                    if (string.IsNullOrEmpty(code)) continue;
                    lock (_llave)
                    {
                        if (!_codigos.Contains(code)) continue;
                        _evTradesPropios++;
                        if (t.Id != 0)
                        {
                            if (_tradesVistos.Contains(t.Id)) continue;
                            _tradesVistos.Add(t.Id);
                            if (_tradesVistos.Count > 200000) _tradesVistos.Clear();
                        }
                        double v = (double)t.Volume;
                        if (v <= 0) continue;
                        _cinta.TryGetValue(code, out var a);
                        bool compra = t.OrderDirection == TradeDirection.Buy;
                        bool venta = t.OrderDirection == TradeDirection.Sell;
                        _cinta[code] = (a.compra + (compra ? v : 0), a.venta + (venta ? v : 0), a.total + v, a.n + 1);
                        // los bloques grandes, con su strike y su lado, para dibujarlos
                        if (UmbralGrande > 0 && v >= UmbralGrande && t.Security != null)
                        {
                            _grandes.Add(new Grande
                            {
                                Hora = DateTime.UtcNow,
                                K = (double)(t.Security.StrikePrice ?? 0m),
                                EsCall = t.Security.OptionType == OptionTypes.Call,
                                Compra = compra, Contratos = v, Precio = (double)t.Price,
                            });
                            if (_grandes.Count > 400) _grandes.RemoveRange(0, 100);
                        }
                        // el acumulador viejo sigue: es "desde la suscripcion"
                        _volHoy[code] = (_volHoy.TryGetValue(code, out var b) ? b : 0) + v;
                    }
                }
            }
            catch { }
        }

        /// <summary>Volumen del dia en toda la ventana suscrita: el del resumen
        /// del conector cuando llego, y si no el de la cinta.</summary>
        public double VolumenTotalHoy()
        {
            lock (_llave)
            {
                double t = 0;
                foreach (var code in _codigos)
                {
                    if (_resumen.TryGetValue(code, out var s) && s.CurrentDayTotalVolume.HasValue)
                        t += (double)s.CurrentDayTotalVolume.Value;
                    else if (_volHoy.TryGetValue(code, out var v)) t += v;
                }
                return t;
            }
        }

        /// <summary>Volumen del dia por STRIKE (call + put), desde los resumenes
        /// del conector. Barato: no reprecia nada. Los strikes de las opciones
        /// de ES ya estan en precio de FUTURO, sin base.</summary>
        public Dictionary<double, (double total, double calls, double puts)> VolumenPorStrike()
        {
            var d = new Dictionary<double, (double total, double calls, double puts)>();
            lock (_llave)
            {
                foreach (var code in _codigos)
                {
                    if (!_resumen.TryGetValue(code, out var s) || s.Security == null) continue;
                    double v = (double)(s.CurrentDayTotalVolume ?? 0m);
                    if (v <= 0) continue;
                    double K = (double)(s.Security.StrikePrice ?? 0m);
                    if (K <= 0) continue;
                    bool call = s.Security.OptionType == OptionTypes.Call;
                    d.TryGetValue(K, out var a);
                    d[K] = (a.total + v, a.calls + (call ? v : 0), a.puts + (call ? 0 : v));
                }
            }
            return d;
        }

        /// <summary>Cuantos contratos suscritos traen volumen del dia en su resumen.</summary>
        public int ContratosConVolumen()
        {
            lock (_llave)
            {
                int n = 0;
                foreach (var code in _codigos)
                    if (_resumen.TryGetValue(code, out var s) && (s.CurrentDayTotalVolume ?? 0m) > 0) n++;
                return n;
            }
        }

        /// <summary>Solo lo visto por la cinta desde la suscripcion.</summary>
        public double VolumenCintaTotal()
        {
            lock (_llave) { double t = 0; foreach (var v in _volHoy.Values) t += v; return t; }
        }

        /// <summary>
        /// Donde se corta el camino del volumen, si se corta.
        ///
        /// El acumulador dio cero dos noches seguidas y sin operaciones no hay
        /// forma de distinguir "no se opera de noche" de "esto esta roto". Esto
        /// lo resuelve en una sola mirada al registro, sin esperar otra noche.
        /// </summary>
        public string Diagnostico()
        {
            long a, c, k; int eng;
            lock (_llave) { a = _evAvisos; c = _evConVol; k = _evContados; eng = _enganchados.Count; }
            long tod; int conUlt; double maxUlt; string nombres;
            lock (_llave)
            {
                tod = _evTodos; conUlt = _conUltimoVol; maxUlt = _maxUltimoVol;
                nombres = _nombresVistos.Count == 0 ? "ninguno"
                        : string.Join(",", _nombresVistos);
            }
            long res, tr, trp; int resConVol, resOI;
            lock (_llave)
            {
                res = _evResumenes; tr = _evTrades; trp = _evTradesPropios;
                resConVol = 0; resOI = 0;
                foreach (var s in _resumen.Values)
                {
                    if ((s.CurrentDayTotalVolume ?? 0m) > 0) resConVol++;
                    if ((s.OpenInterest ?? 0m) > 0) resOI++;
                }
            }
            // COMO LEERLO:
            //   volresumenes=0                 -> el conector no manda resumenes: la suscripcion Summary no llego
            //   volresumenes>0 y volresconvol=0 -> llegan resumenes pero nadie opero hoy en la ventana (de noche puede pasar)
            //   voltrades>0 y voltradesprop=0  -> llegan operaciones pero de otros instrumentos (el futuro), no de las opciones
            return " volenganchados=" + eng + " volavisostodos=" + tod
                 + " volavisos=" + a + " volconvol=" + c + " volcontados=" + k
                 + " volconultimo=" + conUlt
                 + " volmaxultimo=" + maxUlt.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)
                 + " volcampos=" + nombres
                 + " volresumenes=" + res + " volresconvol=" + resConVol + " volresconoi=" + resOI
                 + " voltrades=" + tr + " voltradesprop=" + trp;
        }

        // ------------------------------------------------------------------
        // lectura
        // ------------------------------------------------------------------

        /// <summary>
        /// Foto de la cadena AHORA, con la volatilidad ya despejada de cada
        /// punto medio. Devuelve null si todavia no hay nada confiable: es
        /// preferible que el indicador no dibuje a que dibuje humo.
        /// </summary>
        public List<Fila> Instantanea()
        {
            List<Security> ss;
            lock (_llave) ss = new List<Security>(_suscritos);
            if (ss.Count == 0 || !Activa) return null;

            var f = (double)(_futuro?.LastTradePrice ?? 0m);
            if (f <= 0 && _futuro != null && _futuro.BestBidPrice > 0 && _futuro.BestAskPrice > 0)
                f = (double)((_futuro.BestBidPrice + _futuro.BestAskPrice) / 2m);
            if (f > 0) Futuro = f;
            if (Futuro <= 0) return null;

            var hoy = DateTime.Now.Date;
            var salida = new List<Fila>(ss.Count);
            // foto del momento, no acumulado: cuantos contratos tienen
            // volumen de ultima operacion AHORA
            lock (_llave) { _conUltimoVol = 0; _maxUltimoVol = 0; }
            foreach (var o in ss)
            {
                double bid = (double)o.BestBidPrice, ask = (double)o.BestAskPrice;
                if (bid <= 0 || ask <= 0 || ask < bid) continue;
                double mid = (bid + ask) / 2.0;
                double K = (double)(o.StrikePrice ?? 0m);
                if (K <= 0) continue;
                // EL TIEMPO AL VENCIMIENTO LLEVA LA HORA, NO SOLO EL DIA.
                //
                // Antes esto contaba DIAS ENTEROS desde la medianoche de hoy y
                // le daba tratamiento aparte al 0DTE. Se vio en el volcado de
                // la cadena: un vencimiento decia "4,0000 dias" exacto, y
                // ningun contrato vence a las cuatro de la manana.
                //
                // Cuanto costaba: a las 03:01 de Nueva York, un vencimiento del
                // dia 8 a las 16:00 esta a 4,54 dias y nosotros le poniamos
                // 4,00. Doce por ciento de menos en T, y como el gamma va como
                // uno sobre raiz de T, el gamma de esa serie salia un 6 % mas
                // grande de lo que es. No es un desplazamiento parejo: pesa
                // distinto en cada vencimiento, asi que DESBALANCEA la mezcla
                // entre el 0DTE y el resto, y eso si mueve los niveles.
                //
                // Ahora el 0DTE deja de ser un caso especial: sale de la misma
                // formula, porque con dias=0 queda justo la fraccion que falta
                // hasta el cierre.
                double diasCal = (o.Expiration.Date - hoy).Days;
                double dias = Math.Max(0.0, diasCal) + TiempoQueQuedaHoy();
                // piso de un minuto, no de media hora: ver PISO_DIAS en
                // GammaVivo. Medido: con 29 minutos subestimabamos el muro del
                // 0DTE un 12 % en la ultima lectura de la rueda.
                double T = Math.Max(dias, 1.0 / 1440.0) / 365.0;

                bool esCall = o.OptionType == OptionTypes.Call;
                double iv = Black76.DespejarIV(mid, Futuro, K, T, esCall);
                if (double.IsNaN(iv) || iv <= 0) continue;

                double vh = 0, volDia = double.NaN, oiRes = double.NaN, vc = 0, vcomp = 0, vvent = 0;
                lock (_llave)
                {
                    var code = o.Code ?? "";
                    _volHoy.TryGetValue(code, out vc);
                    if (_cinta.TryGetValue(code, out var ci)) { vcomp = ci.compra; vvent = ci.venta; }
                    if (_resumen.TryGetValue(code, out var rs))
                    {
                        if (rs.CurrentDayTotalVolume.HasValue) volDia = (double)rs.CurrentDayTotalVolume.Value;
                        if (rs.OpenInterest.HasValue) oiRes = (double)rs.OpenInterest.Value;
                    }
                    // el resumen manda: incluye lo operado antes de suscribirnos
                    vh = !double.IsNaN(volDia) ? volDia : vc;
                }

                // LA PRUEBA QUE NO DEPENDE DE EVENTOS.
                //
                // Se lee el campo directo, aca, cada vez que se arma la cadena.
                // Si algun contrato tiene LastTradeVolume > 0 y el acumulador
                // por eventos sigue en cero, el problema es MIO y no del
                // horario -- y queda demostrado sin esperar a la rueda.
                var ulv = (double)(o.LastTradeVolume ?? 0m);
                if (ulv > 0)
                    lock (_llave)
                    {
                        _conUltimoVol++;
                        if (ulv > _maxUltimoVol) _maxUltimoVol = ulv;
                    }

                salida.Add(new Fila
                {
                    VolumenHoy = vh,
                    VolumenDia = volDia, VolCinta = vc, VolCompra = vcomp, VolVenta = vvent,
                    OIResumen = oiRes,
                    K = K,
                    Dias = dias,
                    // el OI de Security viene en cero hasta que el feed lo manda;
                    // el del resumen es el mismo dato por otro camino
                    OI = (double)(o.OpenInterest ?? 0m) > 0 ? (double)(o.OpenInterest ?? 0m)
                       : (!double.IsNaN(oiRes) ? oiRes : 0.0),
                    Bid = bid, Ask = ask, Mid = mid,
                    IV = iv, EsCall = esCall,
                    Codigo = o.Code ?? "",
                });
            }
            UltimaLectura = DateTime.Now;
            return salida.Count > 0 ? salida : null;
        }

        /// <summary>Fraccion de dia que le queda al 0DTE hasta las 16:00 de Nueva York.</summary>
        /// <summary>La fecha de HOY en Nueva York, que es la que manda para
        /// contar dias al vencimiento de un contrato de CME.</summary>
        private static DateTime HoyEnNuevaYork()
        {
            try
            {
                var ny = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                return TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Utc, ny).Date;
            }
            catch { return DateTime.UtcNow.AddHours(-5).Date; }
        }

        private static double TiempoQueQuedaHoy()
        {
            try
            {
                var ny = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                var ahora = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Utc, ny);
                var cierre = ahora.Date.AddHours(16);
                var h = (cierre - ahora).TotalHours;
                return Math.Max(0.25, h) / 24.0;
            }
            catch { return 4.0 / 24.0; }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// La raiz de un codigo de futuro: ESU6 -> ES, MESU6 -> MES, MNQZ5 -> MNQ.
        ///
        /// El codigo termina en letra de mes + digito(s) de ano. Se cortan
        /// desde atras mientras haya digitos, y despues una letra mas.
        /// </summary>
        /// <summary>Del codigo del micro al del grande y su trimestre siguiente:
        /// MESU6 -> ESU6, ESZ6 (y M2KU6 -> RTYU6, RTYZ6).</summary>
        private static IEnumerable<string> CodigosGrande(string codMicro, string grande)
        {
            var salida = new List<string>();
            string cod = codMicro ?? "";
            if (cod.StartsWith("M2K", StringComparison.OrdinalIgnoreCase)) cod = "RTY" + cod.Substring(3);
            else if (cod.Length > 1 && cod.StartsWith("M", StringComparison.OrdinalIgnoreCase)
                     && cod.Substring(1).StartsWith(grande, StringComparison.OrdinalIgnoreCase)) cod = cod.Substring(1);
            if (!cod.StartsWith(grande, StringComparison.OrdinalIgnoreCase) || cod.Length < grande.Length + 2) { salida.Add(grande); return salida; }
            salida.Add(cod);
            const string meses = "HMUZ";
            char m = char.ToUpperInvariant(cod[cod.Length - 2]); char y = cod[cod.Length - 1];
            int im = meses.IndexOf(m);
            if (im >= 0 && char.IsDigit(y))
            {
                int im2 = (im + 1) % 4; int y2 = (y - '0') + (im2 == 0 ? 1 : 0);
                salida.Add(grande + meses[im2] + (char)('0' + (y2 % 10)));
            }
            return salida;
        }

        private static string Raiz(string codigo)
        {
            var c = (codigo ?? "").Trim().ToUpperInvariant();
            if (c.Length < 3) return c;
            int i = c.Length - 1;
            while (i >= 0 && char.IsDigit(c[i])) i--;
            if (i >= 0 && char.IsLetter(c[i])) i--;      // la letra del mes
            return i >= 0 ? c.Substring(0, i + 1) : c;
        }

        private static string _camino = "";
        private static bool _diagnosticado;

        /// <summary>Que conectores hay a la vista y que interfaces implementan: para ubicar el de opciones
        /// cuando la busqueda falla (ATAS 8.0.14.399 lo movio de lugar).</summary>
        private static void Diagnostico(object proveedor, object manager, Security seguridad, Action<string> log)
        {
            var tConn = typeof(IDataFeedConnector);
            var vistos = new Dictionary<string, string>();
            var relojD = System.Diagnostics.Stopwatch.StartNew(); int nodosD = 200000;
            void Recorrer(object raiz, int nivel, HashSet<object> ya, ref int presupuesto)
            {
                if (raiz == null || nivel > 5 || presupuesto-- <= 0 || nodosD-- <= 0 || relojD.ElapsedMilliseconds > 10000) return;
                try
                {
                    var t = raiz.GetType();
                    if (t.IsPrimitive || t.IsEnum || raiz is string || raiz is Delegate) return;
                    if (!ya.Add(raiz)) return;
                    if (tConn.IsInstanceOfType(raiz) && !vistos.ContainsKey(t.FullName))
                        vistos[t.FullName] = string.Join(",", t.GetInterfaces().Select(i => i.Name).Where(n => n.Contains("Feed") || n.Contains("Option") || n.Contains("Connector")).Distinct());
                    if (raiz is System.Collections.IDictionary dic) { foreach (var it in dic.Values) Recorrer(it, nivel + 1, ya, ref presupuesto); }
                    else if (raiz is System.Collections.IEnumerable en) { int i = 0; foreach (var it in en) { if (i++ > 300) break; Recorrer(it, nivel + 1, ya, ref presupuesto); } }
                    for (var tt = t; tt != null && tt != typeof(object); tt = tt.BaseType)
                        foreach (var f in tt.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                        {
                            if (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string)) continue;
                            object v; try { v = f.GetValue(raiz); } catch { continue; }
                            Recorrer(v, nivel + 1, ya, ref presupuesto);
                        }
                }
                catch { }
            }
            var sb = new System.Text.StringBuilder("DIAGNOSTICO conector: proveedor=" + (proveedor?.GetType().FullName ?? "null") + " manager=" + (manager?.GetType().FullName ?? "null") + " security=" + (seguridad?.GetType().FullName ?? "null") + " | conectores a la vista: ");
            foreach (var raiz in new[] { proveedor, manager, (object)seguridad }) { var ya = new HashSet<object>(ReferenceEqualityComparer.Instance); int pres = 60000; Recorrer(raiz, 0, ya, ref pres); }
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var nombre = asm.GetName().Name ?? "";
                if (!(nombre.StartsWith("ATAS") || nombre.StartsWith("OFT"))) continue;
                Type[] tipos; try { tipos = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { tipos = e.Types.Where(x => x != null).ToArray(); } catch { continue; }
                foreach (var t in tipos)
                {
                    if (t.IsGenericTypeDefinition || t.IsEnum || t.IsInterface) continue;
                    var nt = t.FullName ?? "";
                    if (!(nt.Contains("Connector") || nt.Contains("DataFeed") || nt.Contains("Manager") || nt.Contains("Service") || nt.Contains("Provider") || nt.Contains("Container"))) continue;
                    if (relojD.ElapsedMilliseconds > 10000 || nodosD <= 0) break;
                    FieldInfo[] campos; try { campos = t.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly); } catch { continue; }
                    foreach (var f in campos)
                    {
                        if (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string)) continue;
                        object v; try { v = f.GetValue(null); } catch { continue; }
                        var ya = new HashSet<object>(ReferenceEqualityComparer.Instance); int pres = 5000; Recorrer(v, 0, ya, ref pres);
                    }
                }
            }
            sb.Append("(").Append(relojD.ElapsedMilliseconds).Append(" ms) ");
            if (vistos.Count == 0) sb.Append("NINGUNO (ningun IDataFeedConnector alcanzable)");
            foreach (var kv in vistos) sb.Append(kv.Key).Append(" [").Append(kv.Value).Append("] ");
            var tOpt = Type.GetType("ATAS.DataFeedsCore.IOptionsDataFeed, ATAS.DataFeedsCore");
            if (tOpt != null)
            {
                var imp = new List<string>();
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] tipos; try { tipos = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { tipos = e.Types.Where(x => x != null).ToArray(); } catch { continue; }
                    foreach (var t in tipos) { try { if (!t.IsInterface && tOpt.IsAssignableFrom(t)) imp.Add(t.FullName); } catch { } }
                }
                sb.Append("| implementan IOptionsDataFeed: ").Append(imp.Count == 0 ? "ninguno" : string.Join(", ", imp));
            }
            log?.Invoke("[cadena viva] " + sb);
        }
        private static readonly HashSet<object> _vistos = new HashSet<object>(ReferenceEqualityComparer.Instance);
        private static int _presupuesto;

        private static System.Diagnostics.Stopwatch _reloj = System.Diagnostics.Stopwatch.StartNew();
        private static int _presupuestoGlobal = 150000;
        private static DateTime _ultimaHonda = DateTime.MinValue;

        /// <summary>Un conector de datos que sabe de opciones: tiene GetOptionSeriesAsync y GetOptionsAsync
        /// (por forma, no por interfaz: en .399 Rithmic dejo de declararla).</summary>
        private static bool EsFeedOpciones(object o)
        {
            if (!(o is IDataFeedConnector)) return false;
            try { var t = o.GetType(); return t.GetMethod("GetOptionSeriesAsync") != null && t.GetMethod("GetOptionsAsync") != null; } catch { return false; }
        }

        private static object Rastrear(object raiz, Func<object, bool> es, int nivel, string camino)
        {
            if (nivel == 0) { _vistos.Clear(); _presupuesto = 20000; }
            if (raiz == null || nivel > 5 || _presupuesto-- <= 0 || _presupuestoGlobal-- <= 0 || _reloj.ElapsedMilliseconds > 8000) return null;
            try
            {
                if (es(raiz)) { _camino = camino; return raiz; }
                if (!_vistos.Add(raiz)) return null;
                var t = raiz.GetType();
                if (t.IsPrimitive || t.IsEnum || raiz is string || raiz is Delegate) return null;
                // colecciones: mirar adentro (hasta 300 elementos)
                if (raiz is System.Collections.IDictionary dic)
                {
                    foreach (var it in dic.Values)
                    {
                        if (it == null) continue;
                        if (es(it)) { _camino = camino + "[valor]"; return it; }
                        var r0 = Rastrear(it, es, nivel + 1, camino + "[valor]");
                        if (r0 != null) return r0;
                    }
                }
                else if (raiz is System.Collections.IEnumerable en)
                {
                    int i = 0;
                    foreach (var it in en)
                    {
                        if (it == null || i++ > 300) continue;
                        if (es(it)) { _camino = camino + "[" + (i - 1) + "]"; return it; }
                        var r0 = Rastrear(it, es, nivel + 1, camino + "[" + (i - 1) + "]");
                        if (r0 != null) return r0;
                    }
                }
                for (var tt = t; tt != null && tt != typeof(object); tt = tt.BaseType)
                    foreach (var f in tt.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    {
                        if (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string)) continue;
                        object v;
                        try { v = f.GetValue(raiz); } catch { continue; }
                        if (v == null) continue;
                        if (es(v)) { _camino = camino + "." + f.Name; return v; }
                        var r = Rastrear(v, es, nivel + 1, camino + "." + f.Name);
                        if (r != null) return r;
                    }
            }
            catch { }
            return null;
        }

        /// <summary>Los campos estaticos de los ensamblados de ATAS/OFT (administradores de
        /// conectores, singletons): el ultimo recurso cuando el conector no cuelga de nada que el
        /// indicador reciba.</summary>
        private static object RastrearEstaticos(Func<object, bool> es)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var nombre = asm.GetName().Name ?? "";
                    if (!(nombre.StartsWith("ATAS") || nombre.StartsWith("OFT"))) continue;
                    Type[] tipos;
                    try { tipos = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { tipos = e.Types.Where(x => x != null).ToArray(); } catch { continue; }
                    foreach (var t in tipos)
                    {
                        if (t.IsGenericTypeDefinition || t.IsEnum || t.IsInterface) continue;
                        var nt = t.FullName ?? "";
                        if (!(nt.Contains("Connector") || nt.Contains("DataFeed") || nt.Contains("Manager") || nt.Contains("Service") || nt.Contains("Provider") || nt.Contains("Container"))) continue;
                        if (_reloj.ElapsedMilliseconds > 8000 || _presupuestoGlobal <= 0) return null;
                        FieldInfo[] campos;
                        try { campos = t.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly); } catch { continue; }
                        foreach (var f in campos)
                        {
                            if (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string)) continue;
                            object v;
                            try { v = f.GetValue(null); } catch { continue; }
                            if (v == null) continue;
                            var r = Rastrear(v, es, 0, t.FullName + "." + f.Name);
                            if (r != null) return r;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public void Dispose()
        {
            try
            {
                List<Security> ss;
                lock (_llave) { ss = new List<Security>(_suscritos); _suscritos.Clear(); }
                if (_conn != null && ss.Count > 0)
                    _conn.UnsubscribeFromMarketData(ss,
                        SubscriptionType.Prints | SubscriptionType.Best | SubscriptionType.Summary);
            }
            catch { }
            try
            {
                if (_conn != null && _enganchadoConector)
                {
                    _conn.SecuritySummaryChanged -= AlResumen;
                    _conn.NewTrades -= AlTrades;
                    _enganchadoConector = false;
                }
            }
            catch { }
            Activa = false;
        }
    }
}
