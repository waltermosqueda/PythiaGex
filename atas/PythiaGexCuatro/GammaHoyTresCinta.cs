using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ATAS.Indicators;

namespace PythiaGexCuatro
{
    /// <summary>
    /// LA CINTA EN VIVO (3.4.0, 07-10-2026, pedido del operador: "inclui temporalidad 1 m, 30 seg y que se pueda colocar custom; ademas
    /// sincroniza lo maximo posible todo en tiempo real, ya sea milisegundos de ser posible o lo mas cerca"). Profundidad 3.0 solo recibia el
    /// precio una vez por segundo (indicador.json) y velas m2: sin la cinta no se pueden armar velas de 30 s ni de un marco a eleccion. La UNICA
    /// fuente de operaciones en vivo es ATAS, asi que la 3.0 la exporta tal cual llega, operacion por operacion, con la hora de la operacion en ms.
    /// 3.4.1 (misma tarde, revision adversaria, ver CHANGELOG): el escritor se despierta con cada operacion, el relleno se vuelve a pedir tras
    /// un hueco, el archivo queda atado a UN instrumento, el dueño es el que recibe la cinta, sin conversiones de hora, sin tirar la cola ante un
    /// error de disco y una linea cortada queda invalida para cualquier lector.
    ///
    /// Archivo vivo:   &lt;Cinta4Carpeta&gt;\cinta-&lt;raiz&gt;-&lt;yyyy-MM-dd&gt;.csv  (dia de la SESION: la que arranca a las 22:00 UTC de la vispera;
    ///                 hora UTC &gt;= 22 -&gt; dia siguiente). Primera linea "t,precio,dv,lado,id"; despues una linea por evento:
    ///                 t = ms UTC desde 1970 (CumulativeTrade.Time, ticks crudos: ATAS los da en UTC), precio = Lastprice, dv = volumen NUEVO de
    ///                 este evento (en la operacion nueva, su volumen; en cada actualizacion, lo que crecio desde la ultima vez que se vio ESA
    ///                 operacion), lado = 1 compra / -1 venta / 0 sin lado, id = numero de la operacion (creciente en orden de primera aparicion;
    ///                 las actualizaciones repiten el id). Sumar dv por id da el volumen final de la operacion. Una linea que quedo cortada por un
    ///                 cierre de ATAS a mitad de escritura termina en 'x' (la escribe la instancia siguiente): no es numerica, todo lector la ignora.
    ///                 Al lado: cinta-&lt;raiz&gt;-&lt;dia&gt;.csv.instrumento (una linea, p. ej. MNQZ6): el archivo es de ESE instrumento; otro no escribe.
    /// Relleno:        cinta-&lt;raiz&gt;-&lt;dia&gt;-relleno.csv (mismo formato, dv = volumen total, id NEGATIVO -1, -2... en orden de hora) desde las
    ///                 22:00 UTC de la vispera hasta la hora del pedido; cuando esta completo aparece cinta-&lt;raiz&gt;-&lt;dia&gt;-relleno.csv.listo
    ///                 (JSON de una linea: n, desde, hasta, primero, ultimo, vivoDesde...). Costura: relleno para t &lt;= hasta, vivo para t &gt; hasta.
    ///                 Se vuelve a pedir (y se reemplaza entero) si el vivo tuvo un hueco de mas de 60 s que el relleno no cubre.
    ///
    /// Reglas del hilo de ATAS (OnCumulativeTrade / OnUpdateCumulativeTrade, cientos por segundo): ni I/O, ni LINQ, ni locks largos. Cada evento
    /// toma una llave corta (un anillo de 16 operaciones recientes, para el dv de las actualizaciones), arma un struct y lo encola en una
    /// ConcurrentQueue; si la cola estaba vacia despierta al escritor (Cinta3Despertar, ~3 us). Un hilo propio vacia la cola y ANEXA las lineas al
    /// archivo (FileStream abierto una vez, FileShare ReadWrite|Delete, Flush despues de cada tanda); sin despertador, cada Cinta3FlushMs.
    /// Un solo grafico escribe por raiz (dueño con latido): con dos graficos NQ o la instancia extra que ATAS crea al abrir los ajustes, el segundo
    /// no encola nada; si el dueño deja de recibir operaciones que el otro si recibe, el otro toma la cinta. Las excepciones del hilo de ATAS
    /// solo se cuentan; las del escritor se anotan una vez por minuto. No dibuja nada, no suscribe nada nuevo, no toca la caja ni la cuenta.
    /// </summary>
    public partial class FamiliaCuatro
    {
        [Display(Name = "Cinta: exportar operaciones en vivo", GroupName = "9.6 3.0 · Cinta", Order = 1010,
                 Description = "3.4.0. Anexa cada operacion (y cada actualizacion de su volumen) a cinta-<raiz>-<dia de la sesion>.csv: t (ms UTC), precio, dv, lado, id. Para velas de 30 s / 1 min / a eleccion en Profundidad 3.0. No dibuja nada. 4.1: APAGADO por defecto (nombre nuevo): la Familia guarda su propia cinta compacta (precio por segundo + velas de 2 min) en PythiaGex4/cinta/seg-*.bin; el CSV entero (~75 MB por dia) solo si se prende aca.")]
        public bool Cinta4Exportar { get; set; } = false;

        [Display(Name = "Cinta: escribir apenas llega cada operacion", GroupName = "9.6 3.0 · Cinta", Order = 1015,
                 Description = "3.4.1. El hilo de ATAS despierta al escritor cuando la cola estaba vacia (~3 us): la operacion llega al archivo en menos de 1 ms en vez de esperar la tanda (~31 ms). Apagado: tandas cada 'milisegundos entre escrituras'.")]
        public bool Cinta3Despertar { get; set; } = true;

        [Display(Name = "Cinta: milisegundos entre escrituras (5-1000)", GroupName = "9.6 3.0 · Cinta", Order = 1020,
                 Description = "Espera maxima del escritor cuando nadie lo despierta (o con 'escribir apenas llega' apagado). Windows redondea a su reloj de 15,6 ms: 25 queda en ~31 ms reales.")]
        [Range(5, 1000)]
        public int Cinta3FlushMs { get; set; } = 25;

        [Display(Name = "Cinta: carpeta", GroupName = "9.6 3.0 · Cinta", Order = 1030,
                 Description = "Donde se escriben cinta-<raiz>-<dia>.csv y el relleno. Se crea si no existe.")]
        public string Cinta4Carpeta { get; set; } = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex4", "cinta");   // 4.1: carpeta propia (la 3.0 escribe en profundidad\estado\cinta)

        [Display(Name = "Cinta: rellenar la sesion al arrancar", GroupName = "9.6 3.0 · Cinta", Order = 1040,
                 Description = "Cuando ya llegan operaciones en vivo y pasaron 60 s del arranque, pide a ATAS la cinta desde las 22:00 UTC hasta ahora y la escribe en cinta-<raiz>-<dia>-relleno.csv (+ .listo). Solo si no hay un relleno que cubra la sesion; se vuelve a pedir si el vivo tuvo un hueco de mas de 60 s (reinicio de ATAS o corte) que el relleno no cubre. Espera a que no haya posicion abierta ni ordenes activas (bajar la sesion carga a ATAS ~45 s). 4.1: APAGADO por defecto (nombre nuevo; si la 3.0 corre al lado, ella ya lo pide y cada pedido carga a ATAS). Necesita 'exportar' prendido. Si se pide, el relleno TAMBIEN entra a la cinta de la Familia (solo antes del primer tick vivo, como la vista previa).")]
        public bool Cinta4Rellenar { get; set; } = false;

        // ------------------------------------------------------------------ lo que cruza del hilo de ATAS al escritor
        private struct CintaEv { public long T; public decimal P; public decimal Dv; public long Id; public sbyte Lado; }
        private readonly ConcurrentQueue<CintaEv> _cintaCola = new ConcurrentQueue<CintaEv>();
        private int _cintaPendientes;                 // Interlocked: encolados menos sacados (ConcurrentQueue.Count congela segmentos: no se usa)
        private volatile bool _cintaEncolar;          // lo decide el escritor: exportar activo Y este grafico es el dueño de la raiz
        private volatile bool _cintaDespertarOn = true;   // copia de Cinta3Despertar que deja el escritor
        private int _cintaTiradosCola;                // Interlocked: tirados porque la cola paso el tope (escritor trabado)
        private int _cintaErroresAtas;                // Interlocked: excepciones en el hilo de ATAS (se anotan desde el escritor)
        private int _cintaFueraAtas;                  // Interlocked: operaciones con hora a mas de -15/+5 min del reloj (replay, historia)
        private long _cintaVistoMs;                   // Volatile: reloj de la PC (ms UTC) de la ultima operacion EN HORA que vio este grafico, dueño o no
        private Exception _cintaUltimoErrorAtas;
        private const int CINTA_COLA_MAX = 1000000;
        private const long CINTA_HUECO_MS = 60000;    // igual que --hueco-vivo-seg de tiempo_real.py: un hueco mayor lo completa el relleno

        // el anillo de operaciones recientes (hilo de ATAS, bajo una llave corta): para el dv de OnUpdateCumulativeTrade
        private const int CINTA_ANILLO = 16;
        private readonly object _cintaLlaveVivo = new object();
        private readonly CumulativeTrade[] _caRef = new CumulativeTrade[CINTA_ANILLO];
        private readonly long[] _caTicks = new long[CINTA_ANILLO];
        private readonly decimal[] _caPrimero = new decimal[CINTA_ANILLO];
        private readonly decimal[] _caVol = new decimal[CINTA_ANILLO];
        private readonly decimal[] _caPx = new decimal[CINTA_ANILLO];
        private readonly int[] _caDir = new int[CINTA_ANILLO];
        private readonly long[] _caId = new long[CINTA_ANILLO];
        private int _caPos, _caCuantos;
        private long _caSiguienteId;

        // lo que el reloj de 2 s (temporizador de ATAS) le deja al escritor
        private volatile string _cintaRaiz = "";
        private volatile string _cintaInstrumento = "?";
        private readonly ConcurrentQueue<string> _cintaLogCola = new ConcurrentQueue<string>();   // logs pedidos desde hilos de ATAS: los escribe el escritor

        // el escritor
        private Thread _cintaHilo;
        private volatile bool _cintaParar;
        private readonly AutoResetEvent _cintaDespertar = new AutoResetEvent(false);
        private DateTime _cintaArranque = DateTime.MinValue;
        private Action _cintaReloj;
        private readonly TimeSpan _cintaRelojPeriodo = TimeSpan.FromSeconds(2);   // periodo distinto del Tick (5 s) y del pulso (1 s)
        private string _cintaRaizDuena = "";          // la raiz de la que este grafico es dueño (solo el escritor)
        private int _cintaPapel;                      // 1 dueño, 2 escribe otro grafico, 3 apagado, 4 archivo de otro instrumento: para anotar solo los cambios
        private string _cintaTomadaDe;                // instrumento del dueño anterior cuando se le tomo la cinta porque no recibia
        private long _cintaSospechaVisto = long.MinValue; private DateTime _cintaSospechaDesde;   // el dueño no ve la cinta que este si: desde cuando
        private string _cintaVeto = "", _cintaVetoDe = "";   // "raiz|dia|instrumento" que no puede escribir ese dia (el archivo es de _cintaVetoDe)
        private DateTime _cintaPrimerVivoUtc = DateTime.MinValue;   // primera linea en vivo escrita por esta instancia (dispara el relleno)

        // el archivo abierto (solo el escritor)
        private FileStream _cfFs;
        private StreamWriter _cfSw;
        private string _cfRuta, _cfDia;
        private long _cfDesdeMs, _cfHastaMs;           // la sesion del archivo abierto, [22:00 UTC vispera, 22:00 UTC dia)
        private long _cfUltimoTArchivo, _cfUltimoTEscrito, _cfMaxId;
        private bool _cfPrimeraLinea;
        private DateTime _cfReintentoAbrir = DateTime.MinValue;
        private readonly StringBuilder _cfSb = new StringBuilder(1 << 16);
        private CintaEv _cfRetenido; private bool _cfHayRetenido;   // sin archivo (error de disco): la operacion espera aca, con la cola detras
        private bool _cfVetado;                        // CintaAbrir se nego porque el archivo es de otro instrumento

        // los ids del archivo: id interno de esta instancia -> id del archivo, para las ultimas 256 operaciones (las actualizaciones solo llegan
        // para las 16 del anillo). Uno por dia (los 3 ultimos): al reabrir un dia (error de disco, cambio de dueño, o una actualizacion de la sesion
        // vieja despues de rotar) cada operacion conserva su id y las nuevas siguen al mas alto del archivo.
        private const int CINTA_IDS = 256;
        private sealed class CintaIds { public readonly long[] Interno = new long[CINTA_IDS]; public readonly long[] Archivo = new long[CINTA_IDS]; public long MaxDia; }
        private readonly Dictionary<string, CintaIds> _idPorDia = new Dictionary<string, CintaIds>(StringComparer.Ordinal);
        private CintaIds _cfIds;

        // contadores de la ventana de 60 s (solo el escritor)
        private DateTime _cvDesde = DateTime.MinValue;
        private long _cvOps, _cvLineas, _cvFuera, _cvTandas, _cvVueltas, _cvRetrasoN, _cvEsperasArchivo;
        private double _cvFlushMsSuma, _cvFlushMsMax, _cvRetrasoSuma, _cvRetrasoMax;
        private int _cvColaMax, _cvHuecos;
        private long _cvVueltaTicks0;
        private bool _cvAvisoSinArchivo;

        // el limitador de excepciones (escritor, relleno, reloj)
        private readonly Dictionary<string, (DateTime T, int Callados)> _cintaErrLog = new Dictionary<string, (DateTime, int)>();

        // la duena de cada raiz (un solo escritor por raiz en todo ATAS)
        private static readonly object _cintaDuenosLlave = new object();
        private static readonly Dictionary<string, (object Dueno, DateTime Latido, string Instrumento)> _cintaDuenos =
            new Dictionary<string, (object, DateTime, string)>(StringComparer.OrdinalIgnoreCase);

        // el relleno (estado bajo _rlLlave)
        private const int RL_LIBRE = 0, RL_POR_PEDIR = 1, RL_ESPERANDO = 2, RL_ESCRIBIENDO = 3;
        private readonly object _rlLlave = new object();
        private int _rlEstado = RL_LIBRE;
        private string _rlDia = "", _rlRaiz = "", _rlDiaResuelto = "", _rlIdsDia = "", _rlIntentosDia = "";
        private DateTime _rlDesde, _rlHasta, _rlPedidoEn, _rlProximoIntento = DateTime.MinValue, _rlUltimaRevision = DateTime.MinValue;
        private readonly HashSet<int> _rlIds = new HashSet<int>();
        private bool _rlAceptado;
        private int _rlIntentos;
        private long _rlVivoDesdeMs; private string _rlVivoDia = "";
        private string _rlHuecoDia = ""; private long _rlHuecoFinMs; private DateTime _rlHuecoFinUtc;   // el ultimo hueco del vivo (> 60 s) de esta instancia
        private DateTime _rlAvisoPosicion = DateTime.MinValue, _rlAvisoEnVuelo = DateTime.MinValue;
        private (string Ruta, DateTime Mtime, long Hasta) _rlListo;
        private Func<string> _cintaPosicion = null;    // null = CintaPosicionAbierta (el arnes de prueba la reemplaza)
        // un pedido por sesion en todo ATAS: si una instancia se quita con el pedido en vuelo, la nueva espera esa respuesta (no carga a ATAS dos veces)
        private static readonly Dictionary<string, DateTime> _rlEnVuelo = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly Regex _rlReHasta = new Regex("\"hasta\"\\s*:\\s*(\\d+)", RegexOptions.CultureInvariant);

        private static readonly long CINTA_EPOCA = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        /// <summary>ms desde 1970 con los ticks CRUDOS, sin mirar Kind (3.4.1): ATAS da la hora de la operacion en UTC (medido con la caja:
        /// edadS -0,4..+1,3 s) y si alguna vez la marcara Local, convertirla la correria 3 h y la tiraria por fuera de hora. Nuestras horas son UtcNow.</summary>
        private static long CintaMs(DateTime t) => (t.Ticks - CINTA_EPOCA) / TimeSpan.TicksPerMillisecond;
        private static DateTime CintaFecha(long ms) => new DateTime(CINTA_EPOCA + ms * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
        /// <summary>El dia de la sesion: la que arranca a las 22:00 UTC lleva la fecha del dia siguiente.</summary>
        private static DateTime CintaDiaSesion(DateTime utc) => utc.Hour >= 22 ? utc.Date.AddDays(1) : utc.Date;
        private static DateTime CintaInicioSesion(DateTime dia) => DateTime.SpecifyKind(dia.Date.AddDays(-1).AddHours(22), DateTimeKind.Utc);
        private string CintaCarpeta() => string.IsNullOrWhiteSpace(Cinta4Carpeta) ? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex4", "cinta") : Cinta4Carpeta.Trim();
        private static string CintaClaveVeto(string raiz, DateTime dia, string ins) => raiz + "|" + dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + ins;

        // ================================================================== hilo de ATAS
        /// <summary>
        /// Desde OnCumulativeTrade (nueva = true) y OnUpdateCumulativeTrade (nueva = false). Sin I/O, sin LINQ, una llave corta; nunca tira.
        /// La operacion se reconoce por REFERENCIA (en una nueva, ademas, con la misma hora, primer precio y lado: si ATAS reusara el objeto no
        /// se mezclan); una actualizacion que no aparece por referencia se busca por hora + primer precio + lado. Si no aparece, es nueva.
        /// </summary>
        private void CintaEvento(CumulativeTrade tr, bool nueva)
        {
            if (tr == null) return;
            try
            {
                long ticks = tr.Time.Ticks;                                    // crudos: UTC (ver CintaMs)
                long tMs = (ticks - CINTA_EPOCA) / TimeSpan.TicksPerMillisecond;
                long relojMs = (DateTime.UtcNow.Ticks - CINTA_EPOCA) / TimeSpan.TicksPerMillisecond;   // ~25 ns: no depende de que el escritor ande
                // Market Replay, historia o reloj roto: no van al archivo de hoy, no gastan id y no cuentan como "recibe la cinta"
                if (tMs < relojMs - 15 * 60000L || tMs > relojMs + 5 * 60000L) { if (_cintaEncolar) Interlocked.Increment(ref _cintaFueraAtas); return; }
                Volatile.Write(ref _cintaVistoMs, relojMs);                   // el latido del dueño mide cinta, no hilo (lo anota tambien el que no es dueño)
                // 4.1 (B1): el 'if (!_cintaEncolar) return;' de la 3.0 se corrio mas abajo: la Familia ve CADA evento que iria al CSV (operacion
                // nueva con volumen, o actualizacion con volumen nuevo o precio distinto) aunque la exportacion este apagada o escriba otro grafico.
                var famCinta = _famCinta;
                if (!_cintaEncolar && famCinta == null) return;
                decimal vol = tr.Volume, px = tr.Lastprice, primero = tr.FirstPrice;
                var d = tr.Direction;
                int dir = d == TradeDirection.Buy ? 1 : d == TradeDirection.Sell ? -1 : 0;
                decimal dv; long id;
                lock (_cintaLlaveVivo)
                {
                    int slot = -1;
                    for (int k = 0; k < _caCuantos; k++)
                    {
                        int i = (_caPos - 1 - k + CINTA_ANILLO) % CINTA_ANILLO;
                        if (!ReferenceEquals(_caRef[i], tr)) continue;
                        if (!nueva || (_caTicks[i] == ticks && _caPrimero[i] == primero && _caDir[i] == dir)) slot = i;
                        break;
                    }
                    if (slot < 0 && !nueva)
                        for (int k = 0; k < _caCuantos; k++)
                        {
                            int i = (_caPos - 1 - k + CINTA_ANILLO) % CINTA_ANILLO;
                            if (_caTicks[i] == ticks && _caPrimero[i] == primero && _caDir[i] == dir) { slot = i; _caRef[i] = tr; break; }
                        }
                    bool precioCambio;
                    if (slot < 0)
                    {
                        slot = _caPos; _caPos = (_caPos + 1) % CINTA_ANILLO; if (_caCuantos < CINTA_ANILLO) _caCuantos++;
                        _caRef[slot] = tr; _caTicks[slot] = ticks; _caPrimero[slot] = primero; _caDir[slot] = dir; _caVol[slot] = 0; _caPx[slot] = px; _caId[slot] = 0;
                        precioCambio = false;   // una operacion nueva sin volumen no es una operacion
                    }
                    else precioCambio = px != _caPx[slot];
                    dv = vol - _caVol[slot];
                    if (dv < 0) dv = 0;                         // el volumen no baja: se mide contra el maximo visto
                    if (vol > _caVol[slot]) _caVol[slot] = vol;
                    _caPx[slot] = px;
                    if (dv <= 0 && !precioCambio) return;       // nada nuevo: no se encola
                    if (_caId[slot] == 0) _caId[slot] = ++_caSiguienteId;   // el id nace con la primera linea encolada (asi el escritor ve ids crecientes)
                    id = _caId[slot];
                }
                // GANCHO-4.1 familia (B1): el mismo (t, precio) que va a la linea del CSV entra al ICinta. O(1), una llave corta, sin I/O.
                if (famCinta != null) famCinta.Tick(tMs, (double)px);
                if (!_cintaEncolar) return;
                int pend = Interlocked.Increment(ref _cintaPendientes);
                if (pend > CINTA_COLA_MAX)
                {
                    Interlocked.Decrement(ref _cintaPendientes); Interlocked.Increment(ref _cintaTiradosCola); return;
                }
                _cintaCola.Enqueue(new CintaEv { T = tMs, P = px, Dv = dv, Id = id, Lado = (sbyte)dir });
                // la cola estaba vacia: el escritor duerme. Despertarlo cuesta ~3 us (medido); si ya tenia trabajo, lo ve al terminar esa tanda
                if (pend == 1 && _cintaDespertarOn) _cintaDespertar.Set();
            }
            catch (Exception e) { Interlocked.Increment(ref _cintaErroresAtas); _cintaUltimoErrorAtas = e; }
        }

        /// <summary>Posicion abierta u orden activa en la cuenta de este grafico (null = nada). Solo lee; desde el reloj de ATAS.</summary>
        private string CintaPosicionAbierta()
        {
            var tm = TradingManager;
            if (tm == null) return null;
            try
            {
                var pos = tm.Position;
                if (pos != null && pos.Volume != 0) return "posicion abierta (" + pos.Volume.ToString("0.##", CultureInfo.InvariantCulture) + ")";
            }
            catch { }
            try
            {
                var ords = tm.Orders;
                if (ords != null)
                    foreach (var o in ords)
                        if (o != null && o.State == ATAS.DataFeedsCore.OrderStates.Active) return "orden activa";
            }
            catch { }
            return null;
        }

        /// <summary>El reloj de 2 s (temporizador de ATAS): deja la raiz y el instrumento para el escritor y, si el escritor lo pidio, manda el
        /// pedido del relleno (RequestForCumulativeTrades desde un temporizador, como la sonda de Flujo Claro). Sin I/O. El pedido espera mientras
        /// haya posicion abierta u ordenes activas: bajar la sesion entera carga a ATAS ~45 s.</summary>
        private void CintaRelojLatido()
        {
            try
            {
                string r = ""; try { r = Raiz(); } catch { }
                _cintaRaiz = r ?? "";
                string ins = "?"; try { ins = InstrumentInfo?.Instrument ?? "?"; } catch { }
                _cintaInstrumento = string.IsNullOrWhiteSpace(ins) ? "?" : ins.Trim();
                bool quiere;
                lock (_rlLlave) quiere = _rlEstado == RL_POR_PEDIR && !_cintaParar;
                if (!quiere) return;
                string pos = null;
                try { pos = (_cintaPosicion ?? CintaPosicionAbierta)(); } catch { }
                if (pos != null)
                {
                    var ya = DateTime.UtcNow;
                    if ((ya - _rlAvisoPosicion).TotalMinutes >= 5)
                    {
                        _rlAvisoPosicion = ya;
                        _cintaLogCola.Enqueue("cinta: relleno en espera: " + pos + " en " + _cintaInstrumento + " (bajar la sesion carga a ATAS ~45 s); se pide cuando no haya posicion ni ordenes activas");
                    }
                    return;
                }
                CumulativeTradesRequest req = null; DateTime desde = DateTime.MinValue, hasta = DateTime.MinValue; string dia = "", raiz = "";
                lock (_rlLlave)
                {
                    if (_rlEstado == RL_POR_PEDIR && !_cintaParar)
                    {
                        hasta = DateTime.UtcNow; desde = _rlDesde; dia = _rlDia; raiz = _rlRaiz;
                        req = new CumulativeTradesRequest(desde, hasta, 1, 0);
                        _rlHasta = hasta; _rlPedidoEn = hasta; _rlIds.Add(req.RequestId); _rlEstado = RL_ESPERANDO;
                    }
                }
                if (req != null)
                {
                    lock (_rlEnVuelo) _rlEnVuelo[raiz + "|" + dia] = hasta.AddSeconds(300);
                    RequestForCumulativeTrades(req);
                    _cintaLogCola.Enqueue("cinta: relleno pedido (sesion " + dia + ", pedido " + req.RequestId + ") " + desde.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture)
                                          + " -> " + hasta.ToString("yyyy-MM-dd HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture) + ", volumen minimo 1, sin posicion ni ordenes activas");
                }
            }
            catch (Exception e) { Interlocked.Increment(ref _cintaErroresAtas); _cintaUltimoErrorAtas = e; }
        }

        /// <summary>La respuesta de ATAS al pedido del relleno. Solo copia las referencias y pasa la lista a un Task: el filtro, el orden y la
        /// escritura van fuera de este hilo. Una respuesta que no es nuestra se ignora; si llega una segunda para el mismo pedido, tambien.
        /// 3.4.1: se escribe aunque el indicador ya se haya quitado (la instancia nueva espera esta respuesta en vez de pedir otra vez).</summary>
        protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
        {
            try
            {
                if (request == null) return;
                string dia, raiz; DateTime desde, hasta, pedidoEn; bool nuestro, primera;
                lock (_rlLlave)
                {
                    nuestro = _rlIds.Contains(request.RequestId);
                    primera = nuestro && !_rlAceptado;
                    if (primera) { _rlAceptado = true; _rlEstado = RL_ESCRIBIENDO; }
                    dia = _rlIdsDia; raiz = _rlRaiz; desde = _rlDesde; hasta = _rlHasta; pedidoEn = _rlPedidoEn;
                }
                if (!nuestro) return;
                if (!primera) { _cintaLogCola.Enqueue("cinta: relleno: llego OTRA respuesta (pedido " + request.RequestId + ") para la sesion " + dia + ": se ignora"); return; }
                // las fechas del pedido que llego (si un pedido viejo contesta tarde, vale su propio rango)
                if (request.BeginTime != default(DateTime)) desde = request.BeginTime;
                if (request.EndTime != default(DateTime)) hasta = request.EndTime;
                var lista = cumulativeTrades == null ? new List<CumulativeTrade>() : new List<CumulativeTrade>(cumulativeTrades);
                var recibido = DateTime.UtcNow; int idPedido = request.RequestId;
                Task.Run(() => CintaRellenoEscribir(lista, raiz, dia, desde, hasta, pedidoEn, recibido, idPedido));
            }
            catch (Exception e)
            {
                Interlocked.Increment(ref _cintaErroresAtas); _cintaUltimoErrorAtas = e;
                lock (_rlLlave) { if (_rlEstado == RL_ESCRIBIENDO) { _rlEstado = RL_LIBRE; _rlAceptado = false; _rlProximoIntento = DateTime.UtcNow.AddMinutes(10); } }
            }
        }

        // ================================================================== ciclo de vida
        /// <summary>Desde OnInitialize: el hilo escritor y el reloj de 2 s (idempotente).</summary>
        private void CintaArrancar()
        {
            try
            {
                if (_cintaHilo != null) return;
                _cintaArranque = DateTime.UtcNow;
                _cintaParar = false;
                _cintaDespertarOn = Cinta3Despertar;
                _cintaHilo = new Thread(CintaBucle) { IsBackground = true, Name = "PythiaGex4 cinta", Priority = ThreadPriority.Normal };
                _cintaHilo.Start();
                _cintaReloj = CintaRelojLatido;
                SubscribeToTimer(_cintaRelojPeriodo, _cintaReloj);
                CintaRelojLatido();
            }
            catch (Exception e) { Registro.Excepcion(LOG, "CintaArrancar", e); }
        }

        /// <summary>Desde OnDispose: corta el reloj, despierta al escritor para que vacie la cola y cierre, y suelta la raiz.</summary>
        private void CintaParar()
        {
            try { if (_cintaReloj != null) UnsubscribeFromTimer(_cintaRelojPeriodo, _cintaReloj); } catch { }
            _cintaReloj = null;
            _cintaEncolar = false;
            _cintaParar = true;
            try { _cintaDespertar.Set(); } catch { }
            try { if (_cintaHilo != null && !_cintaHilo.Join(1000)) Log("cinta: el escritor no termino en 1 s al quitar el indicador"); } catch { }
            _cintaHilo = null;
        }

        // ================================================================== el escritor (hilo propio)
        private void CintaBucle()
        {
            _cvDesde = DateTime.UtcNow; _cvVueltaTicks0 = Stopwatch.GetTimestamp();
            bool ok = true;
            while (!_cintaParar)
            {
                int espera = Math.Max(5, Math.Min(1000, Cinta3FlushMs));
                // llego algo mientras se escribia la tanda anterior (y hay archivo): seguir sin dormir. Con error de disco no: se reintenta a su ritmo
                if (ok && _cintaEncolar && !_cfHayRetenido && Volatile.Read(ref _cintaPendientes) > 0) espera = 0;
                try { if (espera > 0) _cintaDespertar.WaitOne(espera); } catch { Thread.Sleep(25); }
                if (_cintaParar) break;
                try { CintaVuelta(); ok = true; } catch (Exception e) { ok = false; CintaError("CintaVuelta", e); }
            }
            // al quitar el indicador: lo que quedo en la cola se escribe, y se cierra
            try { if (_cintaRaizDuena != "" && (_cfSw != null || _cfHayRetenido || Volatile.Read(ref _cintaPendientes) > 0)) { _cfReintentoAbrir = DateTime.MinValue; CintaVaciar(DateTime.UtcNow); } }
            catch (Exception e) { CintaError("CintaVaciarFinal", e); }
            CintaCerrarArchivo();
            CintaSoltar();
            CintaLogsDiferidos();
        }

        private void CintaVuelta()
        {
            var ahora = DateTime.UtcNow;
            _cvVueltas++;
            _cintaDespertarOn = Cinta3Despertar;
            CintaLogsDiferidos();
            string raiz = _cintaRaiz, ins = _cintaInstrumento ?? "?";
            bool quiere = Cinta4Exportar && !string.IsNullOrEmpty(raiz) && ins != "?";
            bool vetado = quiere && _cintaVeto.Length > 0 && _cintaVeto == CintaClaveVeto(raiz, CintaDiaSesion(ahora), ins);
            bool dueno = quiere && !vetado && CintaTomarDuenio(raiz, ahora);
            _cintaEncolar = dueno;
            int papel = dueno ? 1 : vetado ? 4 : quiere ? 2 : 3;
            if (papel != _cintaPapel)
            {
                _cintaPapel = papel;
                if (dueno) Log("cinta: " + VERSION + " exporto la cinta de " + raiz + " (" + ins + ") en " + CintaCarpeta() + (Cinta3Despertar ? ", apenas llega cada operacion (tope " + Cinta3FlushMs + " ms)" : ", cada " + Cinta3FlushMs + " ms")
                    + (_cintaTomadaDe != null ? "; la tomo de " + _cintaTomadaDe + ", que no recibe operaciones que este grafico si" : ""));
                else if (vetado) Log("cinta: el archivo de esta sesion de " + raiz + " es de " + _cintaVetoDe + " y este grafico es " + ins + ": no escribe hasta la sesion siguiente (mezclar instrumentos rompe precios o volumenes)");
                else if (quiere) Log("cinta: la cinta de " + raiz + " ya la escribe otro grafico (" + CintaOtroDuenio(raiz) + "): este no encola");
                else Log("cinta: exportar apagado (o grafico sin raiz/instrumento todavia): no encolo");
                _cintaTomadaDe = null;
            }
            if (!dueno)
            {
                if (_cfSw != null) CintaCerrarArchivo();
                if (_cintaRaizDuena != "") CintaSoltar();
                _cfHayRetenido = false;
                while (_cintaCola.TryDequeue(out _)) Interlocked.Decrement(ref _cintaPendientes);   // lo que quedo de cuando era dueño
                CintaResumen(ahora, false);
                return;
            }
            _cintaRaizDuena = raiz;
            CintaVaciar(ahora);
            CintaRellenoRevisar(ahora, raiz);
            CintaResumen(ahora, true);
        }

        /// <summary>Saca de la cola, arma las lineas y las anexa; Flush al final de la tanda. Rota de archivo cuando una operacion cae en otra sesion.
        /// Sin archivo (error de disco, reintento a los 5 s) NO saca nada mas: la operacion queda retenida con la cola detras (tope 1M).</summary>
        private void CintaVaciar(DateTime ahora)
        {
            int pend = Volatile.Read(ref _cintaPendientes);
            if (pend > _cvColaMax) _cvColaMax = pend;
            if (pend <= 0 && !_cfHayRetenido) return;
            long t0 = Stopwatch.GetTimestamp();
            long ahoraMs = CintaMs(ahora), minOk = ahoraMs - 15 * 60000L, maxOk = ahoraMs + 5 * 60000L;
            var inv = CultureInfo.InvariantCulture;
            int n = 0; long sumaT = 0, minT = long.MaxValue, escritas = 0;
            while (n < 200000)
            {
                CintaEv ev;
                if (_cfHayRetenido) { ev = _cfRetenido; _cfHayRetenido = false; }
                else if (_cintaCola.TryDequeue(out ev)) Interlocked.Decrement(ref _cintaPendientes);
                else break;
                n++;
                if (ev.T < minOk || ev.T > maxOk) { _cvFuera++; continue; }   // Market Replay, historia o reloj roto (o retenida mas de 15 min)
                if (_cfSw == null || ev.T < _cfDesdeMs || ev.T >= _cfHastaMs)
                {
                    CintaVolcar();
                    if (ahora < _cfReintentoAbrir || !CintaAbrir(_cintaRaizDuena, ev.T))
                    {
                        if (_cfVetado) { _cfVetado = false; _cintaEncolar = false; break; }   // archivo de otro instrumento: no se retiene; la vuelta siguiente suelta la raiz
                        _cfRetenido = ev; _cfHayRetenido = true; _cvEsperasArchivo++;
                        if (!_cvAvisoSinArchivo) { _cvAvisoSinArchivo = true; Log("cinta: sin archivo para escribir: retengo la cola (" + Volatile.Read(ref _cintaPendientes) + " detras) y reintento cada 5 s"); }
                        break;
                    }
                    if (_cvAvisoSinArchivo) { _cvAvisoSinArchivo = false; Log("cinta: archivo de nuevo abierto; sigue con la cola retenida (" + Volatile.Read(ref _cintaPendientes) + ")"); }
                }
                // id del archivo: una operacion ya escrita conserva el suyo; una nueva toma el siguiente al mas alto del archivo
                int s = (int)(ev.Id & (CINTA_IDS - 1));
                long id;
                if (_cfIds.Interno[s] == ev.Id) id = _cfIds.Archivo[s];
                else { id = ++_cfMaxId; _cfIds.Interno[s] = ev.Id; _cfIds.Archivo[s] = id; _cfIds.MaxDia = _cfMaxId; _cvOps++; }
                if (_cfPrimeraLinea)
                {
                    _cfPrimeraLinea = false;
                    if (_cintaPrimerVivoUtc == DateTime.MinValue) _cintaPrimerVivoUtc = ahora;
                    lock (_rlLlave) { if (_rlVivoDia != _cfDia) { _rlVivoDesdeMs = ev.T; _rlVivoDia = _cfDia; } }
                    if (_cfUltimoTArchivo > 0 && ev.T - _cfUltimoTArchivo > 5000) CintaHueco(_cfUltimoTArchivo, ev.T, "al abrir el archivo (reinicio o cambio de dueño)");
                }
                else if (ev.T - _cfUltimoTEscrito > CINTA_HUECO_MS) CintaHueco(_cfUltimoTEscrito, ev.T, "sin operaciones en vivo (corte de datos?)");
                if (ev.T > _cfUltimoTEscrito) _cfUltimoTEscrito = ev.T;
                _cfSb.Append(inv, $"{ev.T},{ev.P:0.####},{ev.Dv:0.####},{ev.Lado},{id}\n");
                escritas++; sumaT += ev.T; if (ev.T < minT) minT = ev.T;
            }
            CintaVolcar();
            if (escritas > 0)
            {
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                _cvTandas++; _cvLineas += escritas; _cvFlushMsSuma += ms; if (ms > _cvFlushMsMax) _cvFlushMsMax = ms;
                long finMs = CintaMs(DateTime.UtcNow);
                _cvRetrasoSuma += finMs * (double)escritas - sumaT; _cvRetrasoN += escritas;
                double rmax = finMs - minT; if (rmax > _cvRetrasoMax) _cvRetrasoMax = rmax;
            }
        }

        /// <summary>Un hueco en el vivo: se anota y, si pasa de 60 s (el criterio de tiempo_real.py), deja pedido un relleno nuevo para esta sesion
        /// (lo decide CintaRellenoRevisar: solo si el relleno que hay no llega hasta el final del hueco).</summary>
        private void CintaHueco(long desdeMs, long hastaMs, string motivo)
        {
            var inv = CultureInfo.InvariantCulture;
            bool grande = hastaMs - desdeMs > CINTA_HUECO_MS;
            _cvHuecos++;
            Log("cinta: HUECO de " + ((hastaMs - desdeMs) / 1000.0).ToString("0.#", inv) + " s en " + Path.GetFileName(_cfRuta) + " " + motivo + ": ultimo dato "
                + CintaFecha(desdeMs).ToString("HH:mm:ss.fff'Z'", inv) + ", vuelve " + CintaFecha(hastaMs).ToString("HH:mm:ss.fff'Z'", inv)
                + (grande ? (Cinta4Rellenar ? "; si el relleno no lo cubre se pide de nuevo" : "; queda sin cubrir (Cinta4Rellenar apagado)") : " (menos de 60 s: el vivo lo da por seguido)"));
            if (!grande) return;
            lock (_rlLlave)
            {
                if (_rlHuecoDia != _cfDia || hastaMs > _rlHuecoFinMs) { _rlHuecoDia = _cfDia; _rlHuecoFinMs = hastaMs; _rlHuecoFinUtc = DateTime.UtcNow; }
                if (_rlDiaResuelto == _cfDia) _rlDiaResuelto = "";
            }
        }

        /// <summary>Escribe lo armado y hace Flush (al FileStream y de ahi al sistema: lo ve cualquier lector). Si falla, cierra y reintenta en 5 s
        /// (las lineas de ESA tanda se pierden: no se sabe cuanto llego al disco y repetirlas duplicaria).</summary>
        private void CintaVolcar()
        {
            if (_cfSb.Length == 0) return;
            if (_cfSw == null) { _cfSb.Clear(); return; }
            try { _cfSw.Write(_cfSb); _cfSw.Flush(); }
            catch (Exception e) { CintaError("CintaVolcar", e); CintaCerrarArchivo(); _cfReintentoAbrir = DateTime.UtcNow.AddSeconds(5); }
            finally { _cfSb.Clear(); }
        }

        private bool CintaAbrir(string raiz, long tMs)
        {
            CintaCerrarArchivo();
            try
            {
                var inv = CultureInfo.InvariantCulture;
                var dia = CintaDiaSesion(CintaFecha(tMs));
                string diaS = dia.ToString("yyyy-MM-dd", inv);
                string carpeta = CintaCarpeta();
                Directory.CreateDirectory(carpeta);
                string ruta = Path.Combine(carpeta, "cinta-" + raiz + "-" + diaS + ".csv");
                // el archivo es de UN instrumento: MNQ y NQ (o Z6 y H7 en el roll) en el mismo archivo mezclarian volumenes o precios
                string ins = _cintaInstrumento ?? "?", rutaIns = ruta + ".instrumento", atado = null;
                try { if (File.Exists(rutaIns)) atado = File.ReadAllText(rutaIns).Trim(); } catch (Exception e) { CintaError("CintaAbrir.instrumento", e); }
                if (!string.IsNullOrEmpty(atado) && !string.Equals(atado, ins, StringComparison.OrdinalIgnoreCase))
                {
                    _cintaVeto = CintaClaveVeto(raiz, dia, ins); _cintaVetoDe = atado; _cfVetado = true;
                    Log("cinta: " + Path.GetFileName(ruta) + " es de " + atado + " (" + Path.GetFileName(rutaIns) + ") y este grafico es " + ins + ": no escribo en el");
                    return false;
                }
                long ultimoId = 0, ultimoT = 0; bool terminaEnSalto = true;
                if (File.Exists(ruta)) CintaLeerCola(ruta, out ultimoId, out ultimoT, out terminaEnSalto);
                var fs = new FileStream(ruta, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 1 << 16, FileOptions.None);
                var sw = new StreamWriter(fs, new UTF8Encoding(false), 1 << 16) { AutoFlush = false };
                bool nuevo = fs.Length == 0;
                if (nuevo) sw.Write("t,precio,dv,lado,id\n");
                else if (!terminaEnSalto) sw.Write("x\n");   // la ultima linea quedo cortada (ATAS se cerro escribiendo): la 'x' la invalida para todo lector
                sw.Flush();
                if (string.IsNullOrEmpty(atado))
                    try { File.WriteAllText(rutaIns, ins + "\n", new UTF8Encoding(false)); } catch (Exception e) { CintaError("CintaAbrir.instrumento", e); }
                _cfFs = fs; _cfSw = sw; _cfRuta = ruta; _cfDia = diaS;
                var ini = CintaInicioSesion(dia);
                _cfDesdeMs = CintaMs(ini); _cfHastaMs = CintaMs(ini.AddDays(1));
                if (!_idPorDia.TryGetValue(diaS, out _cfIds))
                {
                    _cfIds = _idPorDia[diaS] = new CintaIds();
                    if (_idPorDia.Count > 3) { string viejo = null; foreach (var k in _idPorDia.Keys) if (viejo == null || string.CompareOrdinal(k, viejo) < 0) viejo = k; _idPorDia.Remove(viejo); }
                }
                _cfMaxId = Math.Max(ultimoId, _cfIds.MaxDia);
                _cfUltimoTArchivo = ultimoT; _cfUltimoTEscrito = ultimoT; _cfPrimeraLinea = true;
                Log("cinta: escribo " + ruta + (nuevo ? " (nuevo)" : " (sigue: ultimo id " + ultimoId + (ultimoT > 0 ? ", ultimo dato " + CintaFecha(ultimoT).ToString("HH:mm:ss.fff'Z'", inv) : "")
                    + (!terminaEnSalto ? ", la ultima linea estaba cortada: marcada con x" : "") + ")") + " desde " + ins);
                return true;
            }
            catch (Exception e)
            {
                CintaError("CintaAbrir", e); CintaCerrarArchivo();
                _cfReintentoAbrir = DateTime.UtcNow.AddSeconds(5);
                return false;
            }
        }

        /// <summary>Los ultimos 16 KB del archivo: el id mas alto, la ultima hora y si termina en salto de linea.</summary>
        private static void CintaLeerCola(string ruta, out long ultimoId, out long ultimoT, out bool terminaEnSalto)
        {
            ultimoId = 0; ultimoT = 0; terminaEnSalto = true;
            using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long len = fs.Length; if (len == 0) return;
                int cuanto = (int)Math.Min(len, 16384);
                var buf = new byte[cuanto];
                fs.Seek(len - cuanto, SeekOrigin.Begin);
                int leidos = 0; while (leidos < cuanto) { int r = fs.Read(buf, leidos, cuanto - leidos); if (r <= 0) break; leidos += r; }
                terminaEnSalto = leidos > 0 && buf[leidos - 1] == (byte)'\n';
                var lineas = Encoding.UTF8.GetString(buf, 0, leidos).Split('\n');
                // la primera puede venir cortada por el corte de 16 KB y la ultima por un cierre a mitad: solo cuentan las que parsean enteras
                for (int i = 1; i < lineas.Length; i++)
                {
                    if (i == lineas.Length - 1 && !terminaEnSalto) break;
                    var c = lineas[i].Split(',');
                    if (c.Length != 5) continue;
                    if (long.TryParse(c[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) && id > ultimoId) ultimoId = id;
                    if (long.TryParse(c[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long t) && t > ultimoT) ultimoT = t;
                }
            }
        }

        private void CintaCerrarArchivo()
        {
            try { _cfSw?.Flush(); } catch { }
            try { _cfSw?.Dispose(); } catch { }
            try { _cfFs?.Dispose(); } catch { }
            _cfSw = null; _cfFs = null;
        }

        // ------------------------------------------------------------------ la duena de la raiz
        /// <summary>Un solo escritor por raiz. Toma la raiz si nadie la tiene; el dueño la renueva en cada vuelta. Otro grafico se la saca solo si
        /// el dueño no da señales hace 3 s (hilo muerto) o si este grafico recibe operaciones (ultimos 5 s) que el dueño no ve hace mas de 10 s
        /// (dueño sin cinta: otro instrumento, replay, alimentacion cortada). La ultima operacion del dueño se lee EN VIVO de su campo (no de una
        /// copia del latido): con la misma cinta los dos la anotan con microsegundos de diferencia y no hay vaiven.</summary>
        private bool CintaTomarDuenio(string raiz, DateTime ahora)
        {
            long ahoraMs = CintaMs(ahora), visto = Volatile.Read(ref _cintaVistoMs);
            lock (_cintaDuenosLlave)
            {
                if (_cintaRaizDuena != "" && !string.Equals(_cintaRaizDuena, raiz, StringComparison.OrdinalIgnoreCase))
                {
                    if (_cintaDuenos.TryGetValue(_cintaRaizDuena, out var vieja) && ReferenceEquals(vieja.Dueno, this)) _cintaDuenos.Remove(_cintaRaizDuena);
                    _cintaRaizDuena = "";
                }
                if (!_cintaDuenos.TryGetValue(raiz, out var v) || ReferenceEquals(v.Dueno, this))
                {
                    _cintaDuenos[raiz] = (this, ahora, _cintaInstrumento);
                    return true;
                }
                long vistoDueno = v.Dueno is FamiliaCuatro g ? Volatile.Read(ref g._cintaVistoMs) : 0;
                bool muerto = (ahora - v.Latido).TotalSeconds > 3;
                bool sinCinta = visto > 0 && ahoraMs - visto <= 5000 && visto - vistoDueno > 10000;
                // y que dure: 2 s seguidos con la ultima operacion del dueño quieta (si el dueño la ve unos ms despues que este grafico, no es falta de cinta)
                if (!sinCinta || _cintaSospechaVisto != vistoDueno) { _cintaSospechaVisto = sinCinta ? vistoDueno : long.MinValue; _cintaSospechaDesde = ahora; sinCinta = false; }
                else sinCinta = (ahora - _cintaSospechaDesde).TotalSeconds >= 2;
                if (muerto || sinCinta)
                {
                    _cintaSospechaVisto = long.MinValue;
                    if (sinCinta && !muerto) _cintaTomadaDe = v.Instrumento + " (" + (vistoDueno > 0 ? "su ultima operacion fue hace " + ((ahoraMs - vistoDueno) / 1000.0).ToString("0", CultureInfo.InvariantCulture) + " s" : "no recibio ninguna") + ")";
                    _cintaDuenos[raiz] = (this, ahora, _cintaInstrumento);
                    return true;
                }
                return false;
            }
        }

        private string CintaOtroDuenio(string raiz)
        {
            lock (_cintaDuenosLlave) return _cintaDuenos.TryGetValue(raiz, out var v) ? v.Instrumento : "?";
        }

        private void CintaSoltar()
        {
            try
            {
                lock (_cintaDuenosLlave)
                {
                    if (_cintaRaizDuena != "" && _cintaDuenos.TryGetValue(_cintaRaizDuena, out var v) && ReferenceEquals(v.Dueno, this)) _cintaDuenos.Remove(_cintaRaizDuena);
                    _cintaRaizDuena = "";
                }
            }
            catch { }
        }

        // ------------------------------------------------------------------ el relleno
        /// <summary>El "hasta" (ms) del .listo; -1 si no se puede leer. Cacheado por fecha de escritura.</summary>
        private long CintaListoHasta(string rutaListo)
        {
            try
            {
                var mt = File.GetLastWriteTimeUtc(rutaListo);
                if (_rlListo.Ruta == rutaListo && _rlListo.Mtime == mt) return _rlListo.Hasta;
                var m = _rlReHasta.Match(File.ReadAllText(rutaListo));
                long h = m.Success && long.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long x) ? x : -1;
                _rlListo = (rutaListo, mt, h);
                return h;
            }
            catch { return -1; }
        }

        private static bool CintaEnVuelo(string clave, DateTime ahora)
        {
            lock (_rlEnVuelo)
            {
                if (!_rlEnVuelo.TryGetValue(clave, out var hasta)) return false;
                if (ahora < hasta) return true;
                _rlEnVuelo.Remove(clave);
                return false;
            }
        }

        /// <summary>Una vez por segundo, en el escritor: decide si hace falta el relleno de la sesion y deja el pedido para el reloj de ATAS.
        /// Hace falta si la 3.0 arranco despues de las 22:00 UTC (falta el principio) o si el vivo tuvo un hueco de mas de 60 s; no hace falta si
        /// el relleno que hay (con su .listo) llega hasta el final del ultimo hueco. Maximo 3 pedidos por sesion y por instancia.</summary>
        private void CintaRellenoRevisar(DateTime ahora, string raiz)
        {
            if ((ahora - _rlUltimaRevision).TotalSeconds < 1) return;
            _rlUltimaRevision = ahora;
            try
            {
                var inv = CultureInfo.InvariantCulture;
                int estado; DateTime pedidoEn, proximo, huecoFinUtc; string resuelto, huecoDia; long huecoFin;
                lock (_rlLlave) { estado = _rlEstado; pedidoEn = _rlPedidoEn; proximo = _rlProximoIntento; resuelto = _rlDiaResuelto; huecoDia = _rlHuecoDia; huecoFin = _rlHuecoFinMs; huecoFinUtc = _rlHuecoFinUtc; }
                if (estado == RL_ESPERANDO)
                {
                    if ((ahora - pedidoEn).TotalSeconds > 300)
                    {
                        lock (_rlLlave) { if (_rlEstado == RL_ESPERANDO) { _rlEstado = RL_LIBRE; _rlProximoIntento = ahora.AddMinutes(10); } }
                        Log("cinta: relleno SIN respuesta de ATAS en 300 s: se reintenta en 10 min (si llega tarde, igual se usa)");
                    }
                    return;
                }
                if (estado != RL_LIBRE || !Cinta4Rellenar) return;
                if ((ahora - _cintaArranque).TotalSeconds < 60) return;                                                     // 60 s despues del arranque
                if (_cintaPrimerVivoUtc == DateTime.MinValue || (ahora - _cintaPrimerVivoUtc).TotalSeconds < 30) return;    // y con el vivo ya grabando (solapa)
                if (ahora < proximo) return;
                var dia = CintaDiaSesion(ahora); string diaS = dia.ToString("yyyy-MM-dd", inv);
                if (diaS == resuelto) return;
                bool hueco = huecoDia == diaS && huecoFin > 0;
                if (hueco && (ahora - huecoFinUtc).TotalSeconds < 30) return;                                               // el vivo volvio hace menos de 30 s: que solape
                var inicio = CintaInicioSesion(dia);
                if (_cintaArranque <= inicio && !hueco)
                {
                    lock (_rlLlave) _rlDiaResuelto = diaS;
                    Log("cinta: la sesion " + diaS + " se graba entera en vivo (la 3.0 arranco antes de las 22:00 UTC y sin huecos): sin relleno");
                    return;
                }
                string csv = Path.Combine(CintaCarpeta(), "cinta-" + raiz + "-" + diaS + "-relleno.csv");
                if (File.Exists(csv) && File.Exists(csv + ".listo"))
                {
                    long hasta = CintaListoHasta(csv + ".listo");
                    if (!hueco || hasta >= huecoFin)
                    {
                        lock (_rlLlave) _rlDiaResuelto = diaS;
                        Log("cinta: el relleno de la sesion " + diaS + " ya esta (" + Path.GetFileName(csv) + (hasta > 0 ? ", hasta " + CintaFecha(hasta).ToString("HH:mm:ss'Z'", inv) : "")
                            + (hueco ? ", cubre el hueco que termino " + CintaFecha(huecoFin).ToString("HH:mm:ss'Z'", inv) : "") + "): no se pide");
                        return;
                    }
                    Log("cinta: el relleno de " + diaS + " llega hasta " + (hasta > 0 ? CintaFecha(hasta).ToString("HH:mm:ss'Z'", inv) : "?") + " y el vivo tuvo un hueco hasta "
                        + CintaFecha(huecoFin).ToString("HH:mm:ss'Z'", inv) + ": se pide de nuevo (reemplaza al anterior)");
                }
                if (CintaEnVuelo(raiz + "|" + diaS, ahora))
                {
                    if ((ahora - _rlAvisoEnVuelo).TotalMinutes >= 5) { _rlAvisoEnVuelo = ahora; Log("cinta: otro grafico (o la instancia anterior) ya pidio el relleno de " + diaS + ": espero su respuesta"); }
                    return;
                }
                lock (_rlLlave)
                {
                    if (_rlIntentosDia != diaS) { _rlIntentosDia = diaS; _rlIntentos = 0; }
                    if (_rlIntentos >= 3) { _rlDiaResuelto = diaS; Log("cinta: relleno de " + diaS + ": 3 pedidos en esta instancia, no se pide mas (el vivo sigue)"); return; }
                    _rlIntentos++;
                    if (_rlIdsDia != diaS || _rlAceptado) { _rlIdsDia = diaS; _rlIds.Clear(); _rlAceptado = false; }   // ciclo nuevo: solo vale la respuesta a este pedido (o a uno anterior sin contestar)
                    _rlDia = diaS; _rlRaiz = raiz; _rlDesde = inicio; _rlEstado = RL_POR_PEDIR;
                }
            }
            catch (Exception e) { CintaError("CintaRellenoRevisar", e); }
        }

        /// <summary>En un Task: filtra al rango pedido (la trampa de la sesion entera), ordena por hora si hace falta y escribe el relleno a un
        /// .tmp que despues se renombra; al final el .listo. Ids negativos. El .listo anterior (si hay) queda hasta que lo reemplaza el nuevo:
        /// un lector nunca se queda sin relleno mientras se escribe el siguiente. Nunca tira.</summary>
        private void CintaRellenoEscribir(List<CumulativeTrade> lista, string raiz, string dia, DateTime desde, DateTime hasta, DateTime pedidoEn, DateTime recibido, int idPedido)
        {
            var reloj = Stopwatch.StartNew();
            var inv = CultureInfo.InvariantCulture;
            bool ok = false, sinReintento = false;
            try
            {
                long desdeMs = CintaMs(desde), hastaMs = CintaMs(hasta);
                int crudas = lista.Count, nulas = 0, fuera = 0;
                long minCrudo = long.MaxValue, maxCrudo = long.MinValue;
                var ts = new long[crudas];
                var idx = new List<int>(crudas);
                bool ordenado = true; long prev = long.MinValue;
                for (int i = 0; i < crudas; i++)
                {
                    var tr = lista[i];
                    if (tr == null) { nulas++; continue; }
                    long t = CintaMs(tr.Time); ts[i] = t;
                    if (t < minCrudo) minCrudo = t; if (t > maxCrudo) maxCrudo = t;
                    if (t < desdeMs || t > hastaMs) { fuera++; continue; }
                    if (t < prev) ordenado = false; prev = t;
                    idx.Add(i);
                }
                Log("cinta: relleno recibido " + idx.Count + " operaciones (" + crudas + " crudas, " + fuera + " fuera del rango pedido" + (nulas > 0 ? ", " + nulas + " nulas" : "")
                    + (crudas - nulas > 0 ? ", crudas de " + CintaFecha(minCrudo).ToString("MM-dd HH:mm:ss'Z'", inv) + " a " + CintaFecha(maxCrudo).ToString("MM-dd HH:mm:ss'Z'", inv) : "")
                    + ") en " + (recibido - pedidoEn).TotalSeconds.ToString("0.0", inv) + " s, pedido " + idPedido + (_cintaParar ? " (el indicador ya se quito: igual se escribe)" : ""));
                if (idx.Count == 0)
                {
                    // con datos crudos fuera del rango, reintentar trae lo mismo (y cada pedido carga a ATAS ~45 s): se deja; sin nada, se reintenta
                    sinReintento = crudas - nulas > 0;
                    Log("cinta: relleno VACIO para " + desde.ToString("MM-dd HH:mm'Z'", inv) + " -> " + hasta.ToString("MM-dd HH:mm:ss'Z'", inv)
                        + (sinReintento ? ": ATAS devolvio otra ventana (la trampa de la sesion entera?); no se escribe ni se reintenta en esta sesion"
                                        : ": ATAS no devolvio nada; no se escribe, se reintenta en 10 min"));
                    return;
                }
                if (!ordenado) { var arr = idx.ToArray(); Array.Sort(arr, (a, b) => ts[a] != ts[b] ? ts[a].CompareTo(ts[b]) : a.CompareTo(b)); idx = new List<int>(arr); }

                string carpeta = CintaCarpeta();
                Directory.CreateDirectory(carpeta);
                string csv = Path.Combine(carpeta, "cinta-" + raiz + "-" + dia + "-relleno.csv"), tmp = csv + ".tmp";
                long n = 0;
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 20))
                using (var sw = new StreamWriter(fs, new UTF8Encoding(false), 1 << 20))
                {
                    sw.Write("t,precio,dv,lado,id\n");
                    var sb = new StringBuilder(1 << 20);
                    foreach (int i in idx)
                    {
                        var tr = lista[i];
                        int lado = tr.Direction == TradeDirection.Buy ? 1 : tr.Direction == TradeDirection.Sell ? -1 : 0;
                        n++;
                        sb.Append(inv, $"{ts[i]},{tr.Lastprice:0.####},{tr.Volume:0.####},{lado},{-n}\n");
                        if (sb.Length > (1 << 20) - 256) { sw.Write(sb); sb.Clear(); }
                    }
                    sw.Write(sb); sw.Flush();
                }
                File.Move(tmp, csv, true);
                // GANCHO-4.1 familia (B1): el relleno tambien entra al ICinta (solo t < primer tick vivo de la sesion, como la vista previa)
                try
                {
                    var fam = _famCinta;
                    if (fam != null)
                    {
                        var rl = new List<(long T, double P)>(idx.Count);
                        foreach (int i in idx) rl.Add((ts[i], (double)lista[i].Lastprice));
                        int entraron = fam.Relleno(rl, desdeMs, hastaMs);
                        Log("cinta: relleno -> familia: " + entraron + " de " + rl.Count + " operaciones (las demas ya las tenia el vivo)");
                    }
                }
                catch (Exception e) { CintaError("CintaRellenoEscribir.familia", e); }
                long t0 = ts[idx[0]], t1 = ts[idx[idx.Count - 1]];
                long vivoDesde = 0; lock (_rlLlave) { if (_rlVivoDia == dia) vivoDesde = _rlVivoDesdeMs; }
                double mb = new FileInfo(csv).Length / 1048576.0;
                string listo = "{\"n\":" + n + ",\"crudas\":" + crudas + ",\"fuera\":" + fuera + ",\"desde\":" + desdeMs + ",\"hasta\":" + hastaMs + ",\"primero\":" + t0 + ",\"ultimo\":" + t1
                             + ",\"vivoDesde\":" + (vivoDesde > 0 ? vivoDesde.ToString(inv) : "null") + ",\"dia\":\"" + dia + "\",\"raiz\":\"" + raiz + "\",\"instrumento\":\"" + (_cintaInstrumento ?? "?").Replace("\"", "") + "\""
                             + ",\"pedido\":\"" + pedidoEn.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", inv) + "\",\"respuestaS\":" + (recibido - pedidoEn).TotalSeconds.ToString("0.0", inv)
                             + ",\"escrituraS\":" + reloj.Elapsed.TotalSeconds.ToString("0.00", inv) + ",\"mb\":" + mb.ToString("0.00", inv) + ",\"formato\":\"t,precio,dv,lado,id\",\"version\":\"" + VERSION + "\"}";
                File.WriteAllText(csv + ".listo.tmp", listo + "\n", new UTF8Encoding(false));
                File.Move(csv + ".listo.tmp", csv + ".listo", true);
                ok = true;
                Log("cinta: relleno escrito en " + reloj.Elapsed.TotalSeconds.ToString("0.00", inv) + " s -> " + Path.GetFileName(csv) + " (" + n + " lineas, " + mb.ToString("0.0", inv) + " MB, "
                    + CintaFecha(t0).ToString("HH:mm:ss.fff", inv) + " a " + CintaFecha(t1).ToString("HH:mm:ss.fff'Z'", inv) + ", hasta " + CintaFecha(hastaMs).ToString("HH:mm:ss.fff'Z'", inv) + ")");
            }
            catch (Exception e) { CintaError("CintaRellenoEscribir", e); }
            finally
            {
                lock (_rlEnVuelo) _rlEnVuelo.Remove(raiz + "|" + dia);
                lock (_rlLlave)
                {
                    _rlEstado = RL_LIBRE;
                    if (ok || sinReintento) { if (!(ok && _rlHuecoDia == dia && _rlHuecoFinMs > CintaMs(hasta))) _rlDiaResuelto = dia; _rlProximoIntento = DateTime.UtcNow.AddMinutes(ok ? 2 : 10); }
                    else { _rlAceptado = false; _rlProximoIntento = DateTime.UtcNow.AddMinutes(10); }
                }
            }
        }

        // ------------------------------------------------------------------ log
        private void CintaResumen(DateTime ahora, bool dueno)
        {
            if ((ahora - _cvDesde).TotalSeconds < 60) return;
            var inv = CultureInfo.InvariantCulture;
            double seg = (ahora - _cvDesde).TotalSeconds;
            int tirados = Interlocked.Exchange(ref _cintaTiradosCola, 0), errAtas = Interlocked.Exchange(ref _cintaErroresAtas, 0);
            _cvFuera += Interlocked.Exchange(ref _cintaFueraAtas, 0);
            var ultErr = _cintaUltimoErrorAtas;
            if (dueno || tirados > 0 || errAtas > 0)
            {
                double mb = 0; try { if (_cfFs != null) mb = _cfFs.Length / 1048576.0; } catch { }
                long tv = Stopwatch.GetTimestamp();
                double vuelta = _cvVueltas > 0 ? (tv - _cvVueltaTicks0) * 1000.0 / Stopwatch.Frequency / _cvVueltas : double.NaN;
                Log("cinta: " + _cvOps + " operaciones en " + seg.ToString("0", inv) + " s, cola max " + _cvColaMax + ", flush medio "
                    + (_cvTandas > 0 ? (_cvFlushMsSuma / _cvTandas).ToString("0.00", inv) : "-") + " ms, archivo " + mb.ToString("0.00", inv) + " MB"
                    + " | " + _cvLineas + " lineas en " + _cvTandas + " tandas, flush max " + _cvFlushMsMax.ToString("0.0", inv) + " ms, vuelta media " + (double.IsNaN(vuelta) ? "-" : vuelta.ToString("0.0", inv)) + " ms"
                    + ", retraso medio " + (_cvRetrasoN > 0 ? (_cvRetrasoSuma / _cvRetrasoN).ToString("0", inv) : "-") + " ms (max " + _cvRetrasoMax.ToString("0", inv) + ", reloj PC - hora de la operacion)"
                    + (_cvFuera > 0 ? ", fuera de hora " + _cvFuera : "") + (_cvEsperasArchivo > 0 ? ", vueltas sin archivo " + _cvEsperasArchivo + (_cfHayRetenido ? " (cola retenida " + Volatile.Read(ref _cintaPendientes) + ")" : "") : "")
                    + (_cvHuecos > 0 ? ", huecos " + _cvHuecos : "") + (tirados > 0 ? ", TIRADAS por cola llena " + tirados : "")
                    + (errAtas > 0 ? ", errores en el hilo de ATAS " + errAtas + (ultErr != null ? " (ultimo: " + ultErr.GetType().Name + ": " + ultErr.Message + ")" : "") : "")
                    + (_cfRuta != null ? " -> " + Path.GetFileName(_cfRuta) : ""));
            }
            _cvDesde = ahora; _cvVueltaTicks0 = Stopwatch.GetTimestamp();
            _cvOps = _cvLineas = _cvFuera = _cvTandas = _cvVueltas = _cvRetrasoN = _cvEsperasArchivo = 0;
            _cvFlushMsSuma = _cvFlushMsMax = _cvRetrasoSuma = _cvRetrasoMax = 0; _cvColaMax = 0; _cvHuecos = 0;
        }

        private void CintaLogsDiferidos()
        {
            int n = 0;
            while (n < 50 && _cintaLogCola.TryDequeue(out var s)) { Log(s); n++; }
        }

        /// <summary>Una excepcion por lugar y por minuto; las demas se cuentan y se dicen en la siguiente.</summary>
        private void CintaError(string donde, Exception e)
        {
            try
            {
                int callados = 0;
                lock (_cintaErrLog)
                {
                    var ahora = DateTime.UtcNow;
                    if (_cintaErrLog.TryGetValue(donde, out var u))
                    {
                        if ((ahora - u.T).TotalSeconds < 60) { _cintaErrLog[donde] = (u.T, u.Callados + 1); return; }
                        callados = u.Callados;
                    }
                    _cintaErrLog[donde] = (ahora, 0);
                }
                if (callados > 0) Log("cinta: " + callados + " excepcion(es) mas en " + donde + " en el minuto anterior (no se anotaron)");
                Registro.Excepcion(LOG, "Cinta." + donde, e);
            }
            catch { }
        }
    }
}
