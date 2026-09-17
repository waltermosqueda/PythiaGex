using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using ATAS.Indicators;

namespace PythiaGex
{
    /// <summary>
    /// LA SONDA (17-09-2026): saca de ATAS, por encargo, la materia prima que el laboratorio necesita para buscar un
    /// complemento al CVD, sin tocar la pantalla del operador. Se maneja con un archivo de pedido:
    ///   %APPDATA%\ATAS\PythiaGex\flujo\sonda-&lt;instrumento&gt;.txt   (una linea; el indicador lo borra al tomarlo)
    ///     info
    ///     cinta;2026-09-17T13:30:00;2026-09-17T20:00:00;1;30      desde;hasta (UTC);volumen minimo;minutos por tramo
    ///     libro;2026-09-17T13:30:00;2026-09-17T20:00:00;5;10;60   desde;hasta (UTC);periodo en segundos;niveles;minutos por tramo
    /// 'cinta' = todas las ordenes agresoras (CumulativeTrade) con la punta del libro ANTES y DESPUES de cada una
    /// (PreviousBid/Ask, NewBid/Ask): con eso se ve si la punta aguanto (iceberg / absorcion real) o se la llevaron puesta.
    /// 'libro' = fotos historicas de la profundidad (GetMarketDepthSnapshotsAsync), si ATAS las tiene.
    /// Todo va de a tramos cortos y en segundo plano: un tramo por pedido, el siguiente recien cuando el anterior quedo escrito.
    /// </summary>
    public partial class FlujoClaro
    {
        private static readonly string DirFlujo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex", "flujo");
        private readonly object _sondaLlave = new object();
        private DateTime _sondaMirado = DateTime.MinValue, _sondaPedidoEn = DateTime.MinValue;
        private string _sondaTipo;                       // null = libre; "cinta" o "libro" = trabajando
        private Queue<(DateTime Desde, DateTime Hasta)> _sondaTramos;
        private CumulativeTradesRequest _sondaPedido; private bool _sondaEsperando;
        private int _sondaCrudasAntes;
        private string _sondaSalida; private int _sondaMinVol, _sondaNiveles, _sondaFilas, _sondaTramosHechos; private double _sondaPeriodo;

        private string SondaInstrumento() => (InstrumentInfo?.Instrument ?? "x").Replace('#', ' ').Trim();

        /// <summary>Una vez por segundo (desde Latido): toma un pedido nuevo o empuja el tramo siguiente del que esta en curso.</summary>
        private void SondaLatido()
        {
            try
            {
                lock (_sondaLlave)
                {
                    if (_sondaTipo != null)
                    {
                        var pc = Path.Combine(DirFlujo, "sonda-" + SondaInstrumento() + ".txt");
                        if (File.Exists(pc) && File.ReadAllText(pc).Trim().StartsWith("cancelar")) { File.Delete(pc); _sondaTramos?.Clear(); Log("sonda: CANCELADO por pedido"); }
                        if (_sondaEsperando && (DateTime.UtcNow - _sondaPedidoEn).TotalSeconds > 120) { Log("sonda: el tramo no contesto en 120 s; se saltea"); _sondaEsperando = false; }
                        if (!_sondaEsperando) SondaSiguienteTramo();
                        return;
                    }
                    if ((DateTime.UtcNow - _sondaMirado).TotalSeconds < 2) return;
                    _sondaMirado = DateTime.UtcNow;
                    var p = Path.Combine(DirFlujo, "sonda-" + SondaInstrumento() + ".txt");
                    if (!File.Exists(p)) return;
                    string[] c = File.ReadAllText(p).Trim().Split(';'); File.Delete(p);
                    Log("sonda: pedido '" + string.Join(";", c) + "'");
                    if (c[0] == "info") { SondaInfo(); return; }
                    if ((c[0] != "cinta" && c[0] != "libro") || c.Length < 4) { Log("sonda: pedido no entendido"); return; }
                    DateTime desde = DateTime.Parse(c[1], Inv, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
                    DateTime hasta = DateTime.Parse(c[2], Inv, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
                    int tramoMin;
                    if (c[0] == "cinta") { _sondaMinVol = Math.Max(1, int.Parse(c[3], Inv)); tramoMin = c.Length > 4 ? int.Parse(c[4], Inv) : 30; }
                    else { _sondaPeriodo = Math.Max(0.1, double.Parse(c[3], Inv)); _sondaNiveles = c.Length > 4 ? int.Parse(c[4], Inv) : 10; tramoMin = c.Length > 5 ? int.Parse(c[5], Inv) : 60; }
                    _sondaTramos = new Queue<(DateTime, DateTime)>();
                    for (var t = desde; t < hasta; t = t.AddMinutes(tramoMin)) _sondaTramos.Enqueue((t, t.AddMinutes(tramoMin) < hasta ? t.AddMinutes(tramoMin) : hasta));
                    _sondaSalida = Path.Combine(DirFlujo, c[0] + "-" + SondaInstrumento() + "-" + desde.ToString("yyyyMMddHHmm", Inv) + "-" + hasta.ToString("yyyyMMddHHmm", Inv) + ".csv");
                    Directory.CreateDirectory(DirFlujo);
                    if (c[0] == "cinta") File.WriteAllText(_sondaSalida, "t,primero,ultimo,vol,lado,prints,abid,abidv,aask,aaskv,dbid,dbidv,dask,daskv\n");
                    else
                    {
                        var cab = new StringBuilder("t");
                        for (int i = 1; i <= _sondaNiveles; i++) cab.Append(",b").Append(i).Append(",bv").Append(i);
                        for (int i = 1; i <= _sondaNiveles; i++) cab.Append(",a").Append(i).Append(",av").Append(i);
                        File.WriteAllText(_sondaSalida, cab.Append('\n').ToString());
                    }
                    _sondaFilas = 0; _sondaTramosHechos = 0; _sondaEsperando = false; _sondaTipo = c[0];
                    Log("sonda: " + c[0] + " en " + _sondaTramos.Count + " tramo(s) de " + tramoMin + " min -> " + Path.GetFileName(_sondaSalida));
                }
            }
            catch (Exception e) { Registrar(e); lock (_sondaLlave) { _sondaTipo = null; _sondaEsperando = false; } }
        }

        private void SondaInfo()
        {
            try
            {
                var od = DataProvider?.OnlineDataProvider; var sb = new StringBuilder("sonda info: ");
                if (od == null) { Log(sb.Append("sin OnlineDataProvider").ToString()); return; }
                foreach (CumulativeTradesMode m in Enum.GetValues(typeof(CumulativeTradesMode)))
                {
                    try { sb.Append(m).Append(" profundidad ").Append(od.GetCumulativeTradesMaxDepth(m)).Append(" limite ").Append(od.GetCumulativeTradesSessionLimit(m)).Append(" | "); } catch (Exception e) { sb.Append(m).Append(" ").Append(e.Message).Append(" | "); }
                }
                try { var foto = GetMarketDepthSnapshot()?.ToList(); sb.Append("libro en vivo: ").Append(foto?.Count ?? -1).Append(" niveles"); } catch (Exception e) { sb.Append("libro en vivo: ").Append(e.Message); }
                Log(sb.ToString());
            }
            catch (Exception e) { Registrar(e); }
        }

        /// <summary>Con la llave tomada. Pide el tramo siguiente o cierra el trabajo.</summary>
        private void SondaSiguienteTramo()
        {
            if (_sondaTramos == null || _sondaTramos.Count == 0)
            {
                Log("sonda: " + _sondaTipo + " TERMINADO, " + _sondaFilas + " filas en " + _sondaTramosHechos + " tramos -> " + Path.GetFileName(_sondaSalida));
                try { File.WriteAllText(_sondaSalida + ".listo", _sondaFilas.ToString(Inv)); } catch { }
                _sondaTipo = null; return;
            }
            var (desde, hasta) = _sondaTramos.Dequeue(); _sondaEsperando = true; _sondaPedidoEn = DateTime.UtcNow;
            if (_sondaTipo == "cinta")
            {
                _sondaPedido = new CumulativeTradesRequest(desde, hasta, _sondaMinVol, 0);
                RequestForCumulativeTrades(_sondaPedido);
            }
            else
            {
                var od = DataProvider?.OnlineDataProvider; if (od == null) { Log("sonda: sin OnlineDataProvider"); _sondaTipo = null; return; }
                var req = new MarketDepthSnapshotRequest { From = desde, To = hasta, Period = TimeSpan.FromSeconds(_sondaPeriodo) };
                string salida = _sondaSalida; int niveles = _sondaNiveles;
                var tarea = od.GetMarketDepthSnapshotsAsync(req, CancellationToken.None);
                tarea.ContinueWith(t =>
                {
                    int filas = 0;
                    try
                    {
                        if (t.IsFaulted) Log("sonda libro: " + (t.Exception?.GetBaseException().Message ?? "fallo"));
                        else if (t.Result != null)
                        {
                            var sb = new StringBuilder(1 << 16);
                            foreach (var foto in t.Result)
                            {
                                if (foto == null) continue;
                                sb.Append(foto.StartTime.ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv));
                                var bids = (foto.Bids ?? Array.Empty<MarketDataArg>()).OrderByDescending(x => x.Price).Take(niveles).ToList();
                                var asks = (foto.Asks ?? Array.Empty<MarketDataArg>()).OrderBy(x => x.Price).Take(niveles).ToList();
                                for (int i = 0; i < niveles; i++) { if (i < bids.Count) sb.Append(',').Append(bids[i].Price.ToString("0.####", Inv)).Append(',').Append(bids[i].Volume.ToString("0", Inv)); else sb.Append(",,"); }
                                for (int i = 0; i < niveles; i++) { if (i < asks.Count) sb.Append(',').Append(asks[i].Price.ToString("0.####", Inv)).Append(',').Append(asks[i].Volume.ToString("0", Inv)); else sb.Append(",,"); }
                                sb.Append('\n'); filas++;
                            }
                            lock (_llaveArchivo) File.AppendAllText(salida, sb.ToString());
                        }
                    }
                    catch (Exception e) { Log("sonda libro: " + e.Message); }
                    lock (_sondaLlave) { _sondaFilas += filas; _sondaTramosHechos++; _sondaEsperando = false; }
                    Log("sonda libro: tramo " + desde.ToString("MM-dd HH:mm", Inv) + " -> " + filas + " fotos");
                });
            }
        }

        /// <summary>La respuesta de ATAS a un tramo de 'cinta'. Devuelve true si era de la sonda (y entonces no es de los grandes historicos).</summary>
        private bool SondaRecibirCinta(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> trades)
        {
            string salida;
            lock (_sondaLlave)
            {
                if (_sondaPedido == null || request == null || request.RequestId != _sondaPedido.RequestId) return false;
                _sondaPedido = null; salida = _sondaSalida;
            }
            var lista = trades?.ToList() ?? new List<CumulativeTrade>();
            int crudas = lista.Count; DateTime d0 = request.BeginTime, d1 = request.EndTime;
            lista = lista.Where(x => x != null && x.Time >= d0 && x.Time < d1).ToList();
            lock (_sondaLlave)
            {
                // ATAS, para una fecha pasada, devuelve la sesion entera aunque el tramo sea de 30 min (medido 17-09: 414.184 ordenes por cada tramo).
                // Si dos tramos seguidos traen el mismo bloque enorme, el pedido esta mal armado: se corta para no cargar la plataforma.
                if (crudas > 50000 && crudas == _sondaCrudasAntes && _sondaTramos != null && _sondaTramos.Count > 0) { _sondaTramos.Clear(); Log("sonda: CORTADO, ATAS devuelve el mismo bloque de " + crudas + " ordenes en cada tramo: pedir de a una sesion (tramo de 1380 min desde las 22:00 UTC)"); }
                _sondaCrudasAntes = crudas;
            }
            System.Threading.Tasks.Task.Run(() =>
            {
                int filas = 0;
                try
                {
                    var sb = new StringBuilder(1 << 20);
                    foreach (var t in lista)
                    {
                        if (t == null) continue;
                        sb.Append(t.Time.ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv)).Append(',').Append(t.FirstPrice.ToString("0.####", Inv)).Append(',').Append(t.Lastprice.ToString("0.####", Inv)).Append(',')
                          .Append(t.Volume.ToString("0", Inv)).Append(',').Append(t.Direction == TradeDirection.Buy ? 1 : t.Direction == TradeDirection.Sell ? -1 : 0).Append(',').Append(t.Ticks?.Count ?? 0);
                        foreach (var q in new[] { t.PreviousBid, t.PreviousAsk, t.NewBid, t.NewAsk })
                        { if (q != null) sb.Append(',').Append(q.Price.ToString("0.####", Inv)).Append(',').Append(q.Volume.ToString("0", Inv)); else sb.Append(",,"); }
                        sb.Append('\n'); filas++;
                    }
                    lock (_llaveArchivo) File.AppendAllText(salida, sb.ToString());
                }
                catch (Exception e) { Log("sonda cinta: " + e.Message); }
                lock (_sondaLlave) { _sondaFilas += filas; _sondaTramosHechos++; _sondaEsperando = false; }
                Log("sonda cinta: tramo " + request.BeginTime.ToString("MM-dd HH:mm", Inv) + " -> " + filas + " ordenes (de " + crudas + " recibidas)");
            });
            return true;
        }

        // ------------------------------------------------------------------ grabador del libro EN VIVO (una fila por segundo)
        // ATAS no tiene fotos historicas del libro (GetMarketDepthSnapshotsAsync devuelve 0): lo unico que se puede hacer es grabar hacia adelante.
        // OFI = desbalance del flujo de ORDENES en la punta (Cont, Kukanov y Stoikov 2014): suma, evento por evento, lo que se agrega al bid
        // y se saca del ask, menos lo que se saca del bid y se agrega al ask. Es la lectura que en la literatura mas explica el precio del
        // MISMO intervalo; aca se graba para medir, con datos propios, si ademas dice algo del intervalo SIGUIENTE.
        private double _lbPx, _lbV, _laPx, _laV, _ofi; private int _ofiEventos; private long _libroSegundo;
        private readonly object _libroLlave = new object();

        protected override void OnBestBidAskChanged(MarketDataArg q)
        {
            try
            {
                if (q == null || !FcGrabarLibro) return;
                double px = (double)q.Price, v = (double)q.Volume;
                lock (_libroLlave)
                {
                    if (q.IsBid)
                    {
                        if (_lbPx > 0) _ofi += (px >= _lbPx ? v : 0) - (px <= _lbPx ? _lbV : 0);
                        _lbPx = px; _lbV = v; _ofiEventos++;
                    }
                    else if (q.IsAsk)
                    {
                        if (_laPx > 0) _ofi += -(px <= _laPx ? v : 0) + (px >= _laPx ? _laV : 0);
                        _laPx = px; _laV = v; _ofiEventos++;
                    }
                }
            }
            catch { }
        }

        /// <summary>Una vez por segundo: la punta, el OFI del segundo, la profundidad sumada a 5/10/20 niveles, la orden mas grande a 20 niveles y lo operado.</summary>
        private void LibroLatido()
        {
            try
            {
                if (!FcGrabarLibro || !_cargado) return;
                long seg = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond; if (seg == _libroSegundo) return; _libroSegundo = seg;
                double bp, bv, ap, av, ofi; int ev;
                lock (_libroLlave) { bp = _lbPx; bv = _lbV; ap = _laPx; av = _laV; ofi = _ofi; ev = _ofiEventos; _ofi = 0; _ofiEventos = 0; }
                if (bp <= 0 || ap <= 0) return;
                double tick = (double)(InstrumentInfo?.TickSize ?? 0.25m); if (tick <= 0) tick = 0.25;
                double[] sb = new double[3], sa = new double[3]; double mb = 0, mbp = 0, ma = 0, map = 0; int[] cortes = { 5, 10, 20 };
                var foto = GetMarketDepthSnapshot();
                if (foto != null)
                    foreach (var n in foto)
                    {
                        if (n == null) continue; double p = (double)n.Price, v = (double)n.Volume;
                        if (n.IsBid) { int d = (int)Math.Round((bp - p) / tick); if (d < 0 || d >= 20) continue; for (int i = 0; i < 3; i++) if (d < cortes[i]) sb[i] += v; if (v > mb) { mb = v; mbp = p; } }
                        else if (n.IsAsk) { int d = (int)Math.Round((p - ap) / tick); if (d < 0 || d >= 20) continue; for (int i = 0; i < 3; i++) if (d < cortes[i]) sa[i] += v; if (v > ma) { ma = v; map = p; } }
                    }
                double compra = 0, venta = 0;
                lock (_cubetas) { if (_cubetas.TryGetValue(seg - 1, out var q)) { compra = (q.V + q.S) / 2; venta = (q.V - q.S) / 2; } }
                var pth = Path.Combine(DirFlujo, "libro-vivo-" + SondaInstrumento() + "-" + DateTime.UtcNow.ToString("yyyy-MM-dd", Inv) + ".csv");
                string fila = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", Inv) + "," + bp.ToString("0.####", Inv) + "," + bv.ToString("0", Inv) + "," + ap.ToString("0.####", Inv) + "," + av.ToString("0", Inv)
                    + "," + ofi.ToString("0", Inv) + "," + ev + "," + sb[0].ToString("0", Inv) + "," + sa[0].ToString("0", Inv) + "," + sb[1].ToString("0", Inv) + "," + sa[1].ToString("0", Inv) + "," + sb[2].ToString("0", Inv) + "," + sa[2].ToString("0", Inv)
                    + "," + mb.ToString("0", Inv) + "," + mbp.ToString("0.####", Inv) + "," + ma.ToString("0", Inv) + "," + map.ToString("0.####", Inv) + "," + compra.ToString("0", Inv) + "," + venta.ToString("0", Inv) + "\n";
                lock (_llaveArchivo) _colaArchivo = _colaArchivo.ContinueWith(_ =>
                {
                    try
                    {
                        Directory.CreateDirectory(DirFlujo);
                        if (!File.Exists(pth)) File.WriteAllText(pth, "t,bid,bidv,ask,askv,ofi,eventos,sb5,sa5,sb10,sa10,sb20,sa20,maxb,maxbpx,maxa,maxapx,compra,venta\n");
                        File.AppendAllText(pth, fila);
                    }
                    catch { }
                });
            }
            catch (Exception e) { Registrar(e); }
        }
    }
}
