using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ATAS.Indicators;

namespace PythiaLaOla
{
    /// <summary>
    /// LA OLA 1.0 (27-11-2026): LA CINTA DE ATAS. Mudado del Superindicador 2.0a (SuperOla.cs, probado en vivo el 27-11: 52.410 ordenes de
    /// la jornada en 2,7 s, costura sin hueco, X 10,00 auto) a este indicador aparte. El motor de la ronda 17 (OlaMotor.cs) y el clima de
    /// la ronda 21 (OlaClima.cs), alimentados con la cinta de ATAS, una carga por jornada de CME: la viva (la de la ultima vela) arma su
    /// historial con RequestForCumulativeTrades y sigue orden por orden con OnCumulativeTrade / OnUpdateCumulativeTrade; las pasadas se
    /// piden enteras cuando la vela del momento cae en ellas (de a un pedido por vez, hasta 4 en cache). La logica de cargas, costura,
    /// bufer y cortes esta en OlaNucleo.cs (sin ATAS: la corre el arnes atas/_test_pythiaola); aca solo va la union con ATAS: eventos,
    /// pedidos, hilos, registro y auditoria. Lo que cambio al mudarlo: la ola ya no tiene casilla (es el indicador entero), la X fija es el
    /// ajuste PoXFija, la respuesta de la cinta llega directo (no hay sonda) y el csv lleva tambien el clima de cada minuto.
    /// HILOS: todo el motor bajo _llaveOla. El hilo de datos (OnCalculate, OnCumulativeTrade) solo pasa una vela o una orden (O(1)); el
    /// historial se procesa en un Task con motores locales y se integra bajo la llave; el registro se escribe desde el latido y los
    /// archivos en un Task. OnRender copia bajo la llave lo que va a dibujar y dibuja afuera.
    /// API para dibujar: OlaAlMomento (la foto de la jornada de una vela, la de ahora en la vela viva) y OlaBarDeHora (la vela de un
    /// instante UTC, para ubicar los pivotes en el eje x).
    /// AUDITORIA: seccion "ola" en auditoria-*.jsonl y, cada minuto, ola-&lt;inst&gt;-&lt;tf&gt;-&lt;clave&gt;.csv con los pivotes, la foto de cada vela
    /// de la ola y la marea y el clima de cada minuto; herramientas/auditar_pythiaola.py lo rehace desde una cinta de la sonda de la MISMA
    /// jornada.
    /// </summary>
    public partial class PythiaOla
    {
        // ================================================================== estado
        private const int OlaVencePedidoSeg = 180;
        private const double OlaRamMinimaMb = 2048;
        private readonly object _llaveOla = new object();
        private GestorOla _olaGestor;
        private readonly ConcurrentQueue<string> _olaLog = new ConcurrentQueue<string>();   // lo que se anota desde adentro de la llave
        private CumulativeTradesRequest _olaPedido; private CargaOla _olaPedidoCarga; private int _olaPedidoGen; private DateTime _olaPedidoEn;
        private readonly List<int> _olaVencidos = new List<int>();   // pedidos que no contestaron a tiempo (si contestan tarde se descartan)
        private long _olaEventosNuevos, _olaEventosAct, _olaErroresVivo; private Exception _olaUltErrorVivo; private long _olaErroresAnotados;
        private DateTime _olaUltEventoPc = DateTime.MinValue;
        private int _olaViejasClave = -1; private long _olaViejasAnotadas; private int _olaViejasLineas; private DateTime _olaViejasEn = DateTime.MinValue;
        private DateTime _olaUltCsv = DateTime.MinValue; private volatile bool _olaCsvYa;
        private DateTime _olaRamMirada = DateTime.MinValue; private double _olaRamMb = double.NaN; private bool _olaRamAvisada;

        // un solo pedido de cinta a la vez en todo ATAS (dos graficos con La Ola no le piden juntos)
        private static readonly object _olaTurnoLlave = new object();
        private static object _olaTurnoDe; private static DateTime _olaTurnoEn;

        /// <summary>Bajo _llaveOla.</summary>
        private GestorOla OlaG()
        {
            if (_olaGestor == null) { _olaGestor = new GestorOla(Ny()); _olaGestor.Anotar = s => _olaLog.Enqueue(s); }
            return _olaGestor;
        }

        // ================================================================== API (para dibujar despues)
        /// <summary>
        /// La ola y la marea de la jornada de la vela 'bar' AL CIERRE de esa vela (si es la vela viva, las de ahora), con sus pivotes
        /// confirmados (los primeros Foto.Pivotes, de una copia que no cambia), la X de esa jornada y su estado ("en vivo", "completa",
        /// "pedida", "sin cinta: solo en vivo"... y "; X piso" / "; X auto con N ruedas" / "; X fija" si la X no es la automatica con 10
        /// ruedas). false: esa jornada todavia no tiene ola (sin carga, pedida, procesando o sin cinta); estado dice por que.
        /// </summary>
        public bool OlaAlMomento(int bar, out FotoOla ola, out FotoOla marea, out IReadOnlyList<PivoteOla> pivOla, out IReadOnlyList<PivoteOla> pivMarea,
                                 out decimal x, out string estado)
        {
            ola = marea = default; pivOla = pivMarea = Array.Empty<PivoteOla>(); x = 0m;
            try { lock (_llaveOla) return OlaG().AlMomento(bar, out ola, out marea, out pivOla, out pivMarea, out x, out estado, out _); }
            catch (Exception e) { Anotar(e); estado = "error: " + e.Message; return false; }
        }

        /// <summary>La vela de un instante UTC (busqueda binaria sobre las aperturas): la ultima que abre a esa hora o antes; -1 si es anterior
        /// a la primera vela del grafico. Para ubicar en el eje x un pivote (PivoteOla.T) o un extremo.</summary>
        public int OlaBarDeHora(DateTime utc)
        {
            try { lock (_llaveOla) return OlaG().BarDeHora(utc); }
            catch { return -1; }
        }

        // ================================================================== velas (desde OnCalculate)
        /// <summary>Cada llamada de OnCalculate: apertura, ultima operacion, alto y bajo de la vela (O(1), bajo la llave).</summary>
        private void OlaVela(int bar, IndicatorCandle c)
        {
            if (c == null) return;
            try
            {
                lock (_llaveOla)
                {
                    var g = OlaG();
                    if (g.Vela(bar, c.Time, c.LastTime, c.High, c.Low)) return;
                    // un salto en los numeros de vela (no deberia pasar): se completan las que faltan con las velas del grafico
                    for (int b = g.Velas; b < bar; b++)
                    {
                        var cb = GetCandle(b);
                        if (cb == null || !g.Vela(b, cb.Time, cb.LastTime, cb.High, cb.Low)) return;
                    }
                    g.Vela(bar, c.Time, c.LastTime, c.High, c.Low);
                }
            }
            catch (Exception e) { Anotar(e); }
        }

        // ================================================================== la cinta en vivo
        protected override void OnCumulativeTrade(CumulativeTrade trade)
        {
            if (trade != null) OlaEvento(trade, true);
        }

        protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
        {
            if (trade != null) OlaEvento(trade, false);
        }

        /// <summary>La respuesta de un pedido de cinta (en el Superindicador pasaba primero por la sonda; aca es solo de la ola).</summary>
        protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
        {
            try { OlaRecibirCinta(request, cumulativeTrades); }
            catch (Exception e) { Anotar(e); }
        }

        /// <summary>Hilo de datos (cientos por segundo): sin archivos ni LINQ; una llave corta y un paso del motor. La referencia del objeto
        /// de ATAS es la identidad de la orden (OnUpdateCumulativeTrade manda el mismo objeto, crecido).</summary>
        private void OlaEvento(CumulativeTrade tr, bool nueva)
        {
            try
            {
                var d = tr.Direction;
                var o = new OrdenOla(tr.Time, tr.FirstPrice, tr.Lastprice, tr.Volume, d == TradeDirection.Buy ? 1 : d == TradeDirection.Sell ? -1 : 0);
                lock (_llaveOla)
                {
                    var g = OlaG();
                    if (nueva) { g.Nueva(tr, o); _olaEventosNuevos++; }
                    else { g.Actualizada(tr, o); _olaEventosAct++; }
                    _olaUltEventoPc = DateTime.UtcNow;
                }
            }
            catch (Exception e) { Interlocked.Increment(ref _olaErroresVivo); _olaUltErrorVivo = e; }
        }

        // ================================================================== latido (cada 250 ms, desde Latido)
        private void OlaLatido()
        {
            try
            {
                int n = CurrentBar, m = -1;
                try { m = Momento(); } catch { }
                decimal tick = 0m; string tipo = "", marco = "";
                try { if (InstrumentInfo != null) tick = InstrumentInfo.TickSize; } catch { }
                try { tipo = ChartInfo?.ChartType ?? ""; marco = ChartInfo?.TimeFrame ?? ""; } catch { }
                lock (_llaveOla)
                {
                    var g = OlaG();
                    // la viva, en cuanto el grafico tiene todas sus velas (OnCalculate paso por la ultima)
                    if (g.Viva == null && n > 0 && g.Velas >= n)
                    {
                        if (tick > 0m) g.Tick = tick;
                        g.XFija = _xFija;
                        g.Declarado = PeriodoVelas.Declarado(tipo, marco, out bool noEsTiempo);
                        g.NoEsTiempo = noEsTiempo;
                        g.CrearViva();
                    }
                    // la vela del momento: si cae en una jornada sin carga, se pide
                    if (g.Viva != null && m >= 0 && m < g.Velas) g.QuererMomento(m);
                }
                OlaPedidos();
                OlaVigilar();
                OlaCortes();   // 1.0b: corte de datos -> re-pedir la jornada y avisar (PythiaOlaCortes.cs)
                if (_registro) OlaCsv();
            }
            catch (Exception e) { Anotar(e); }
            while (_olaLog.TryDequeue(out var s)) AnotarLinea(s);
        }

        private bool OlaTomarTurno()
        {
            lock (_olaTurnoLlave)
            {
                if (_olaTurnoDe != null && !ReferenceEquals(_olaTurnoDe, this) && (DateTime.UtcNow - _olaTurnoEn).TotalSeconds < OlaVencePedidoSeg + 20) return false;
                _olaTurnoDe = this; _olaTurnoEn = DateTime.UtcNow;
                return true;
            }
        }

        private void OlaSoltarTurno()
        {
            lock (_olaTurnoLlave) { if (ReferenceEquals(_olaTurnoDe, this)) _olaTurnoDe = null; }
        }

        /// <summary>Desde OnDispose: libera el turno de pedidos (si no, otro grafico esperaria hasta que venza).</summary>
        private void OlaSoltar() { try { OlaSoltarTurno(); } catch { } }

        // La Ola 1.0, arreglo de la revision del 27-11: ATAS admite UN solo pedido de cinta pendiente por indicador (RequestForCumulativeTrades
        // tira ArgumentException si hay otro) y el pendiente solo sale cuando llega su respuesta. Un pedido vencido (180 s sin respuesta)
        // sigue pendiente adentro de ATAS: mientras tanto no se pide otro (se reintenta cada 60 s por si ATAS lo solto) y el pie del panel
        // lo dice. Y con el indicador oculto (el ojo de ATAS) ATAS guarda el pedido hasta que se vea: oculto no se pide y el reloj del
        // vencimiento no corre.
        private const int OlaReintentoSeg = 60;
        private CumulativeTradesRequest _olaTrabado;              // pedido vencido que ATAS todavia tiene pendiente
        private DateTime _olaReintentoEn = DateTime.MinValue;     // no pedir antes de esta hora (despues de un pedido rechazado)
        private DateTime _olaAvisoTrabadoEn = DateTime.MinValue, _olaUltPedidos = DateTime.MinValue;
        private string _olaEspera = "";                           // por que no se pide (para el pie del panel); bajo _llaveOla

        /// <summary>Vence el pedido en vuelo (180 s con el indicador a la vista) y pide la carga que sigue: la viva primero, despues la pasada
        /// del momento. De a un pedido por vez; espera si otro grafico tiene el turno, si la RAM libre esta debajo de 2 GB, si el indicador
        /// esta oculto o si ATAS todavia tiene un pedido viejo sin contestar.</summary>
        private void OlaPedidos()
        {
            CumulativeTradesRequest req = null; CargaOla c = null; bool soltar = false; string aviso = null;
            double ram = RamLibreMb();
            bool visible = true;
            try { visible = Visible; } catch { }
            var ahora = DateTime.UtcNow;
            var dt = _olaUltPedidos == DateTime.MinValue ? TimeSpan.Zero : ahora - _olaUltPedidos;
            _olaUltPedidos = ahora;
            lock (_llaveOla)
            {
                var g = OlaG();
                // oculto: ATAS no manda el pedido hasta que se vea; el reloj del vencimiento se frena (corre solo con el indicador a la vista)
                if (_olaPedido != null && !visible && dt > TimeSpan.Zero) _olaPedidoEn = _olaPedidoEn.Add(dt);
                if (_olaPedido != null && visible && (ahora - _olaPedidoEn).TotalSeconds > OlaVencePedidoSeg)
                {
                    var pc = _olaPedidoCarga;
                    _olaVencidos.Add(_olaPedido.RequestId); if (_olaVencidos.Count > 20) _olaVencidos.RemoveAt(0);
                    _olaTrabado = _olaPedido; _olaReintentoEn = ahora.AddSeconds(OlaReintentoSeg);
                    if (pc != null && _olaPedidoGen == g.Generacion && g.Conoce(pc) && pc.Estado == EstadoOla.Pedida)
                        g.SinRespuesta(pc, "ATAS no contesto el pedido en " + OlaVencePedidoSeg + " s");
                    aviso = "ola: el pedido de la jornada " + (pc != null ? pc.Clave.ToString(Inv) : "?") + " no contesto en " + OlaVencePedidoSeg + " s"
                          + (pc != null && pc.RecibeVivo ? ": la viva sigue solo con lo que llega en vivo" : "") + "; no se pide otro hasta que ATAS lo suelte";
                    _olaPedido = null; _olaPedidoCarga = null; soltar = true;
                }
                if (_olaPedido == null)
                {
                    var sig = g.SiguientePedido();
                    if (sig != null)
                    {
                        if (!visible) _olaEspera = "indicador oculto: la cinta se pide cuando se vea";
                        else if (ahora < _olaReintentoEn)
                            _olaEspera = _olaTrabado != null ? "ATAS tiene un pedido de cinta sin contestar: reintento cada " + OlaReintentoSeg + " s"
                                                             : "ATAS rechazo el pedido de cinta: reintento cada " + OlaReintentoSeg + " s";
                        else if (!double.IsNaN(ram) && ram < OlaRamMinimaMb)
                        {
                            _olaEspera = "RAM libre debajo de 2 GB: el pedido espera";
                            if (!_olaRamAvisada) { _olaRamAvisada = true; aviso = "ola: RAM libre " + ram.ToString("0", Inv) + " MB (< 2 GB): el pedido de la jornada " + sig.Clave + " espera"; }
                        }
                        else if (OlaTomarTurno())
                        {
                            _olaRamAvisada = false; _olaEspera = "";
                            req = new CumulativeTradesRequest(sig.Desde, sig.Hasta, 1, 0);
                            g.MarcarPedida(sig, req);
                            _olaPedido = req; _olaPedidoCarga = sig; _olaPedidoGen = g.Generacion; _olaPedidoEn = ahora; c = sig;
                        }
                        else _olaEspera = "otro grafico esta pidiendo su cinta: espera el turno";
                    }
                    else _olaEspera = "";
                }
            }
            if (soltar && req == null) OlaSoltarTurno();
            if (aviso != null) AnotarLinea(aviso);
            if (req == null) return;
            try
            {
                RequestForCumulativeTrades(req);
                lock (_llaveOla) _olaTrabado = null;   // ATAS lo acepto: ya no tenia pendiente el viejo
                AnotarLinea("ola: pedida la jornada " + c.Clave + (c.RecibeVivo ? " (viva)" : " (pasada)") + " " + c.Desde.ToString("MM-dd HH:mm", Inv) + " a "
                            + c.Hasta.ToString("MM-dd HH:mm", Inv) + " UTC, X " + c.X.ToString(Inv) + " (" + c.FuenteX + "), " + c.Cortes + " cortes");
            }
            catch (Exception e)
            {
                lock (_llaveOla)
                {
                    if (ReferenceEquals(_olaPedido, req)) { _olaPedido = null; _olaPedidoCarga = null; }
                    OlaG().VolverACola(c);
                    _olaReintentoEn = DateTime.UtcNow.AddSeconds(OlaReintentoSeg);
                    _olaEspera = "ATAS rechazo el pedido de cinta (" + e.GetType().Name + "): reintento cada " + OlaReintentoSeg + " s";
                }
                OlaSoltarTurno();
                if ((DateTime.UtcNow - _olaAvisoTrabadoEn).TotalSeconds >= 60)
                {
                    _olaAvisoTrabadoEn = DateTime.UtcNow;
                    AnotarLinea("ola: ATAS rechazo el pedido de la jornada " + c.Clave + " (" + e.GetType().Name + ": " + e.Message + "); reintento en " + OlaReintentoSeg + " s");
                }
            }
        }

        /// <summary>Desde OnCumulativeTradesResponse: true si el pedido es de la ola. Se copia la lista (referencias, ~10 ms para una rueda)
        /// y se procesa en un Task con motores locales.</summary>
        private bool OlaRecibirCinta(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> trades)
        {
            if (request == null) return false;
            CargaOla c; int gen; double seg;
            try
            {
                lock (_llaveOla)
                {
                    bool mio = _olaPedido != null && (ReferenceEquals(request, _olaPedido) || request.RequestId == _olaPedido.RequestId);
                    if (!mio)
                    {
                        int i = _olaVencidos.IndexOf(request.RequestId);
                        if (i < 0) return false;   // no es de este indicador
                        _olaVencidos.RemoveAt(i);
                        // ATAS ya lo solto: se puede volver a pedir enseguida
                        if (_olaTrabado != null && _olaTrabado.RequestId == request.RequestId) { _olaTrabado = null; _olaReintentoEn = DateTime.MinValue; }
                        _olaLog.Enqueue("ola: llego tarde la respuesta de un pedido vencido: se descarta (ATAS ya lo solto)");
                        return true;
                    }
                    c = _olaPedidoCarga; gen = _olaPedidoGen; seg = (DateTime.UtcNow - _olaPedidoEn).TotalSeconds;
                    _olaPedido = null; _olaPedidoCarga = null;
                }
            }
            catch (Exception e) { Anotar(e); return false; }
            OlaSoltarTurno();
            try
            {
                var lista = trades != null ? new List<CumulativeTrade>(trades) : new List<CumulativeTrade>();
                TrabajoOla trabajo;
                lock (_llaveOla) { var g = OlaG(); trabajo = gen == g.Generacion && c != null ? g.Preparar(c) : null; }
                if (trabajo == null)
                {
                    _olaLog.Enqueue("ola: respuesta de la jornada " + (c != null ? c.Clave.ToString(Inv) : "?") + " descartada (" + lista.Count + " ordenes): la carga se reinicio o cambio de estado");
                    return true;
                }
                _olaLog.Enqueue("ola: llego el historial de la jornada " + c.Clave + ": " + lista.Count + " ordenes en " + seg.ToString("0.0", Inv) + " s; costura "
                                + (trabajo.ConBufer ? "con el bufer en " + trabajo.Tope.ToString("HH:mm:ss.fff", Inv) + " UTC" : trabajo.Tope < DateTime.MaxValue ? "en la ultima vela " + trabajo.Tope.ToString("HH:mm:ss.fff", Inv) + " UTC" : "sin tope"));
                Task.Run(() => OlaProcesar(trabajo, lista));
            }
            catch (Exception e) { Anotar(e); }
            return true;
        }

        /// <summary>El Task del historial: motores locales, sin llave; al final se integra bajo la llave.</summary>
        private void OlaProcesar(TrabajoOla t, List<CumulativeTrade> lista)
        {
            var hilo = Thread.CurrentThread; var prio = ThreadPriority.Normal;
            try { prio = hilo.Priority; hilo.Priority = ThreadPriority.BelowNormal; } catch { }
            try
            {
                t.Empezar();
                for (int i = 0; i < lista.Count; i++)
                {
                    var x = lista[i];
                    if (x == null) { t.Nula(); continue; }
                    var d = x.Direction;
                    t.Orden(x, new OrdenOla(x.Time, x.FirstPrice, x.Lastprice, x.Volume, d == TradeDirection.Buy ? 1 : d == TradeDirection.Sell ? -1 : 0));
                }
                t.Terminar();
            }
            catch (Exception e) { t.Error = e; }
            finally { try { hilo.Priority = prio; } catch { } }
            string r;
            try { lock (_llaveOla) r = OlaG().Integrar(t); }
            catch (Exception e) { r = null; Anotar(e); }
            _olaLog.Enqueue(r != null ? "ola: integrada " + r : "ola: el historial de la jornada " + t.Carga.Clave + " se descarto (se reinicio mientras se procesaba)");
            _olaCsvYa = true;
            _redibujarPedido = true;   // con el mercado quieto no hay tick que redibuje: lo pide el latido
        }

        /// <summary>Anota las actualizaciones viejas (una orden que crecio cuando ya no era la ultima: el motor no la puede corregir) y los
        /// errores del hilo de datos. Las primeras 20 una por una; despues, un resumen por minuto.</summary>
        private void OlaVigilar()
        {
            long viejas = 0; OrdenOla uv = default; int clave = -1;
            lock (_llaveOla) { var v = OlaG().Viva; if (v != null) { viejas = v.Viejas; uv = v.UltimaVieja; clave = v.Clave; } }
            if (clave != _olaViejasClave) { _olaViejasClave = clave; _olaViejasAnotadas = 0; }
            if (viejas > _olaViejasAnotadas && (_olaViejasLineas < 20 || (DateTime.UtcNow - _olaViejasEn).TotalSeconds >= 60))
            {
                AnotarLinea("ola: " + (viejas - _olaViejasAnotadas) + " actualizacion(es) vieja(s) en la jornada " + clave + " (total " + viejas + "); la ultima: "
                            + uv.T.ToString("HH:mm:ss.fff", Inv) + " UTC " + uv.Primero.ToString(Inv) + "->" + uv.Ultimo.ToString(Inv) + " vol " + uv.Vol.ToString(Inv) + " lado " + uv.Lado);
                _olaViejasAnotadas = viejas; _olaViejasLineas++; _olaViejasEn = DateTime.UtcNow;
            }
            long err = Interlocked.Read(ref _olaErroresVivo);
            if (err > _olaErroresAnotados)
            {
                var e = _olaUltErrorVivo;
                AnotarLinea("ola: " + (err - _olaErroresAnotados) + " error(es) en el hilo de datos; el ultimo: " + (e != null ? e.GetType().Name + ": " + e.Message : "?"));
                _olaErroresAnotados = err;
            }
        }

        /// <summary>Cada minuto (o apenas se integra un historial): ola-&lt;inst&gt;-&lt;tf&gt;-&lt;clave&gt;.csv de cada carga que cambio. La copia se hace
        /// bajo la llave; el formato y el archivo, en un Task.</summary>
        private void OlaCsv()
        {
            if (!_olaCsvYa && (DateTime.UtcNow - _olaUltCsv).TotalSeconds < 60) return;
            _olaUltCsv = DateTime.UtcNow; _olaCsvYa = false;
            var copias = new List<(CargaOla C, InstantaneaOla S)>();
            lock (_llaveOla)
            {
                var g = OlaG();
                var cargas = new List<CargaOla>();
                if (g.Viva != null) cargas.Add(g.Viva);
                cargas.AddRange(g.Pasadas);
                foreach (var c in cargas)
                {
                    if (c.Bufereando || c.VersionEscrita == c.Version) continue;
                    copias.Add((c, g.Instantanea(c)));
                    c.VersionEscrita = c.Version;
                }
            }
            if (copias.Count == 0) return;
            string inst = Inst(), tf = Marco(), dir = Carpeta();
            Task.Run(() =>
            {
                foreach (var (c, s) in copias)
                {
                    string tmp = null;
                    try
                    {
                        s.Inst = inst; s.Marco = tf; s.VersionIndicador = Version;
                        // el archivo temporal se escribe SIN la llave (nombre unico): la llave solo cubre el cambio de nombre, asi el
                        // registro del latido no espera lo que tarda en escribirse el csv
                        string ruta = Path.Combine(dir, "ola-" + inst + "-" + tf + "-" + s.Clave.ToString(Inv) + ".csv");
                        tmp = ruta + "." + Guid.NewGuid().ToString("N") + ".tmp";
                        using (var w = new StreamWriter(tmp, false, new UTF8Encoding(false), 1 << 16)) s.Escribir(w);
                        lock (_llaveArchivo) File.Move(tmp, ruta, true);
                        tmp = null;
                    }
                    catch (Exception e)
                    {
                        // se reintenta en el minuto siguiente (por ejemplo, si el csv estaba abierto por el auditor)
                        lock (_llaveOla) c.VersionEscrita = -1;
                        _olaLog.Enqueue("ola: no se pudo escribir el csv de la jornada " + s.Clave + ": " + e.Message);
                    }
                    finally { if (tmp != null) { try { File.Delete(tmp); } catch { } } }
                }
            });
        }

        // ================================================================== reinicios
        /// <summary>PoXFija cambio: las cargas se tiran (la X cambia) y se vuelven a pedir; las velas quedan.</summary>
        private void OlaReiniciarCargas(string motivo)
        {
            bool habia;
            lock (_llaveOla) { var g = OlaG(); habia = g.Viva != null || g.Pasadas.Count > 0; g.ReiniciarCargas(); }
            if (habia) AnotarLinea("ola: cargas reiniciadas (" + motivo + ")");
        }

        /// <summary>OnRecalculate: el grafico se recarga (los numeros de vela cambian): se tira todo. Un pedido en vuelo se deja vencer o
        /// contestar (su respuesta se descarta) para no pedirle dos cintas juntas a ATAS.</summary>
        private void OlaReiniciarTodo(string motivo)
        {
            bool habia;
            lock (_llaveOla) { var g = OlaG(); habia = g.Viva != null || g.Velas > 0; g.ReiniciarTodo(); }
            if (habia) _olaLog.Enqueue("ola: todo reiniciado (" + motivo + ")");
        }

        // ================================================================== auditoria (desde Auditar, cada 60 s)
        private static string Hu(DateTime t) => t == DateTime.MinValue || t == DateTime.MaxValue ? "" : t.ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv);

        private void AuditarOla(StringBuilder sb)
        {
            try
            {
                int m = -1;
                try { m = Momento(); } catch { }
                var s = new StringBuilder(4096);
                lock (_llaveOla)
                {
                    var g = OlaG();
                    s.Append(",\"ola\":{\"x_fija\":").Append(F(_xFija))
                     .Append(",\"eventos_nuevos\":").Append(_olaEventosNuevos).Append(",\"eventos_act\":").Append(_olaEventosAct)
                     .Append(",\"errores_vivo\":").Append(Interlocked.Read(ref _olaErroresVivo))
                     .Append(",\"ultimo_evento_pc\":\"").Append(Hu(_olaUltEventoPc)).Append('"');
                    g.AuditoriaJson(s, m);   // la carga del momento, su foto de la ola y de la marea, el clima y la lista de cargas (OlaNucleo.cs)
                    s.Append(",\"pedido\":");
                    if (_olaPedido == null) s.Append("null");
                    else s.Append("{\"clave\":").Append(_olaPedidoCarga != null ? _olaPedidoCarga.Clave : -1).Append(",\"en_vuelo_s\":")
                          .Append((DateTime.UtcNow - _olaPedidoEn).TotalSeconds.ToString("0.0", Inv)).Append('}');
                    s.Append(",\"trabado\":").Append(_olaTrabado != null ? "true" : "false").Append(",\"espera\":\"")
                     .Append((_olaEspera ?? "").Replace("\"", "'")).Append('"');
                    s.Append(",\"cortes\":{\"vistos\":").Append(_cortes.Vistos).Append(",\"repedidos\":").Append(_cortes.Repedidos)
                     .Append(",\"ultimo\":\"").Append(Hu(_cortes.UltimoCorte)).Append("\",\"motivo\":\"").Append((_cortes.UltimoMotivo ?? "").Replace("\"", "'"))
                     .Append("\",\"pendiente\":\"").Append(Hu(_cortes.Pendiente)).Append("\",\"aviso\":\"").Append((_cortes.Aviso ?? "").Replace("\"", "'")).Append("\"}");
                    s.Append(",\"ram_libre_mb\":").Append(double.IsNaN(_olaRamMb) ? "null" : _olaRamMb.ToString("0", Inv)).Append('}');
                }
                sb.Append(s.ToString());   // solo si se armo entera: un error a mitad no deja el renglon de la auditoria roto
            }
            catch (Exception e) { Anotar(e); }
        }

        // ================================================================== RAM libre (no pedir una rueda entera con la PC al limite)
        [StructLayout(LayoutKind.Sequential)]
        private struct EstadoMemoria
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref EstadoMemoria m);

        private double RamLibreMb()
        {
            if ((DateTime.UtcNow - _olaRamMirada).TotalSeconds < 2) return _olaRamMb;
            _olaRamMirada = DateTime.UtcNow;
            try
            {
                var m = new EstadoMemoria { dwLength = (uint)Marshal.SizeOf<EstadoMemoria>() };
                _olaRamMb = GlobalMemoryStatusEx(ref m) ? m.ullAvailPhys / 1048576.0 : double.NaN;
            }
            catch { _olaRamMb = double.NaN; }
            return _olaRamMb;
        }
    }
}
