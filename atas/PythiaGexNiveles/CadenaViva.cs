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
        }

        private readonly object _llave = new();
        private List<Security> _suscritos = new();
        private IDataFeedConnector _conn;
        private Security _futuro;
        private volatile bool _armando;

        /// <summary>Ultimo estado legible, para mostrar en pantalla sin mentir.</summary>
        public string Estado { get; private set; } = "sin arrancar";

        /// <summary>true solo si hay contratos suscritos Y estan llegando puntas.</summary>
        public bool Activa { get; private set; }

        /// <summary>Precio del futuro de la cadena, en vivo.</summary>
        public double Futuro { get; private set; }

        public DateTime UltimaLectura { get; private set; }

        // ------------------------------------------------------------------
        // arranque
        // ------------------------------------------------------------------

        /// <summary>
        /// Engancha el conector y se suscribe a la ventana de strikes que
        /// interesa. Es lento (busca series y espera precios), asi que se
        /// llama UNA vez y en segundo plano.
        /// </summary>
        public async Task Arrancar(object proveedor, object manager, Security seguridad,
                                   int diasMax, int strikesPorLado, int vencMax,
                                   int topeContratos, Action<string> log)
        {
            if (_armando) return;
            _armando = true;
            try
            {
                void L(string m) { Estado = m; log?.Invoke("[cadena viva] " + m); }

                var tOpt = Type.GetType("ATAS.DataFeedsCore.IOptionsDataFeed, ATAS.DataFeedsCore");
                if (tOpt == null) { L("el tipo IOptionsDataFeed no existe en esta version"); return; }

                // GetService no la entrega a los indicadores (NotSupportedException,
                // verificado). Se rastrea el conector por los campos privados.
                object feed = Rastrear(proveedor, tOpt, 0)
                           ?? Rastrear(manager, tOpt, 0)
                           ?? Rastrear(seguridad, tOpt, 0);
                if (feed == null) { L("no se encontro el conector de opciones"); return; }

                _conn = feed as IDataFeedConnector;
                if (_conn == null) { L("el conector no expone IDataFeedConnector"); return; }
                if (!_conn.IsConnected) { L("el conector no esta conectado"); return; }

                var todas = (_conn.Securities ?? Enumerable.Empty<Security>()).ToList();
                _futuro = todas.Where(x => x.Type == SecType.Future
                                      && (x.Code ?? "").ToUpperInvariant().StartsWith("ES"))
                               .OrderBy(x => x.Expiration).FirstOrDefault();
                if (_futuro == null) { L("no esta el futuro de ES en el catalogo"); return; }

                // EL PRECIO DE REFERENCIA ES EL DEL FUTURO DE LA CADENA.
                // Tomarlo del grafico fue un error que ya se cometio: desde un
                // grafico de MNQ se buscaron los strikes de ES alrededor de
                // 29511 y se cayo en 10800-12000, donde no cotiza nadie.
                try { _conn.SubscribeToMarketData(new[] { _futuro },
                          SubscriptionType.Prints | SubscriptionType.Best); }
                catch { }
                for (int i = 0; i < 15 && Futuro <= 0; i++)
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                    Futuro = (double)(_futuro.LastTradePrice ?? 0m);
                    if (Futuro <= 0 && _futuro.BestBidPrice > 0 && _futuro.BestAskPrice > 0)
                        Futuro = (double)((_futuro.BestBidPrice + _futuro.BestAskPrice) / 2m);
                }
                if (Futuro <= 0) { L("no llego el precio del futuro"); return; }
                L("futuro " + _futuro.Code + " en " + Futuro.ToString("0.##", CultureInfo.InvariantCulture));

                // las series y sus contratos
                List<Security> ops = new();
                var hoy = DateTime.Now.Date;
                try
                {
                    var ss = await ((dynamic)feed).GetOptionSeriesAsync(_futuro);
                    var series = ((IEnumerable<OptionSeries>)ss)
                                 .Where(z => (z.Expiration.Date - hoy).Days >= 0
                                          && (z.Expiration.Date - hoy).Days <= diasMax)
                                 .OrderBy(z => z.Expiration).ToList();
                    foreach (var serie in series)
                    {
                        var cc = await ((dynamic)feed).GetOptionsAsync(serie);
                        ops.AddRange(((IEnumerable<Security>)cc).Where(o => o.StrikePrice.HasValue));
                    }
                    L(series.Count + " vencimientos, " + ops.Count + " contratos");
                }
                catch (Exception e) { L("no se pudieron listar las series: " + e.Message); return; }
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

                lock (_llave) _suscritos = elegidos;
                L("suscritos " + elegidos.Count + " contratos, esperando puntas");

                // esperar a que efectivamente lleguen, sin darlo por hecho
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(1500).ConfigureAwait(false);
                    int conPunta = elegidos.Count(x => x.BestBidPrice > 0 && x.BestAskPrice > 0);
                    if (conPunta >= Math.Max(8, elegidos.Count / 10))
                    {
                        Activa = true;
                        L("EN VIVO: " + conPunta + " de " + elegidos.Count + " con las dos puntas");
                        return;
                    }
                }
                L("suscrito pero no llegaron suficientes puntas");
            }
            catch (Exception e) { Estado = "error al arrancar: " + e.Message; }
            finally { _armando = false; }
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
            foreach (var o in ss)
            {
                double bid = (double)o.BestBidPrice, ask = (double)o.BestAskPrice;
                if (bid <= 0 || ask <= 0 || ask < bid) continue;
                double mid = (bid + ask) / 2.0;
                double K = (double)(o.StrikePrice ?? 0m);
                if (K <= 0) continue;
                double dias = (o.Expiration.Date - hoy).Days;
                // El 0DTE no vale cero: con T=0 la formula explota. Se le da la
                // fraccion de dia que le queda, con un piso para no dividir por
                // algo ridiculamente chico.
                double T = Math.Max(dias, 0.0) / 365.0;
                if (dias <= 0) T = Math.Max(TiempoQueQuedaHoy(), 0.5 / 24.0) / 365.0;

                bool esCall = o.OptionType == OptionTypes.Call;
                double iv = Black76.DespejarIV(mid, Futuro, K, T, esCall);
                if (double.IsNaN(iv) || iv <= 0) continue;

                salida.Add(new Fila
                {
                    K = K,
                    Dias = Math.Max(dias, T * 365.0),
                    OI = (double)(o.OpenInterest ?? 0m),
                    Bid = bid, Ask = ask, Mid = mid,
                    IV = iv, EsCall = esCall,
                    Codigo = o.Code ?? "",
                });
            }
            UltimaLectura = DateTime.Now;
            return salida.Count > 0 ? salida : null;
        }

        /// <summary>Fraccion de dia que le queda al 0DTE hasta las 16:00 de Nueva York.</summary>
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

        private static object Rastrear(object raiz, Type buscada, int nivel)
        {
            if (raiz == null || nivel > 3) return null;
            try
            {
                if (buscada.IsInstanceOfType(raiz)) return raiz;
                foreach (var f in raiz.GetType().GetFields(
                             BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (f.FieldType.IsPrimitive || f.FieldType == typeof(string)) continue;
                    object v;
                    try { v = f.GetValue(raiz); } catch { continue; }
                    if (v == null) continue;
                    if (buscada.IsInstanceOfType(v)) return v;
                    var r = Rastrear(v, buscada, nivel + 1);
                    if (r != null) return r;
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
            Activa = false;
        }
    }
}
