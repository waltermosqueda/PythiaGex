using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using ATAS.Indicators;

namespace PythiaSuper
{
    /// <summary>
    /// 1.9 (26-11-2026) LA SONDA, dentro del Superindicador. Pedido del operador: "busca data integra AHORA y empeza a estudiar y
    /// construir ya". ATAS entrega gratis el historial orden por orden con el lado agresor (verificado el 26-11 con el replay: identico a
    /// lo que se vio en vivo), pero solo a un indicador cargado. Copia de la sonda de Flujo Claro (17-09), solo la parte de la cinta, para
    /// pedirla sin tocar la pantalla: se escribe una linea en %APPDATA%\ATAS\PythiaGex\flujo\sonda-&lt;instrumento&gt;.txt
    ///     cinta;2026-11-23T23:00:00;2026-11-24T22:00:00;1;1380      desde;hasta (UTC);volumen minimo;minutos por tramo
    /// y sale cinta-&lt;instrumento&gt;-&lt;desde&gt;-&lt;hasta&gt;.csv (+ .listo con la cantidad de ordenes), con el mismo formato que las de ES:
    /// t,primero,ultimo,vol,lado,prints,abid,abidv,aask,aaskv,dbid,dbidv,dask,daskv (punta del libro antes y despues de cada orden).
    /// OJO (medido 17-09): para una fecha pasada ATAS devuelve la sesion ENTERA aunque se pida menos: pedir de a una sesion.
    /// </summary>
    public partial class SuperindicadorOF
    {
        private static readonly string DirFlujo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex", "flujo");
        private readonly object _sondaLlave = new object();
        private DateTime _sondaMirado = DateTime.MinValue, _sondaPedidoEn = DateTime.MinValue;
        private bool _sondaActiva, _sondaEsperando;
        private Queue<(DateTime Desde, DateTime Hasta)> _sondaTramos;
        private CumulativeTradesRequest _sondaPedido;
        private int _sondaCrudasAntes, _sondaMinVol, _sondaFilas, _sondaTramosHechos;
        private string _sondaSalida;

        private string SondaInstrumento() => (InstrumentInfo?.Instrument ?? "x").Replace('#', ' ').Trim();

        /// <summary>Desde el latido: toma un pedido nuevo (cada 2 s) o empuja el tramo siguiente del que esta en curso.</summary>
        private void SondaLatido()
        {
            try
            {
                lock (_sondaLlave)
                {
                    if (_sondaActiva)
                    {
                        if (_sondaEsperando && (DateTime.UtcNow - _sondaPedidoEn).TotalSeconds > 180) { AnotarLinea("sonda: el tramo no contesto en 180 s; se saltea"); _sondaEsperando = false; }
                        if (!_sondaEsperando) SondaSiguienteTramo();
                        return;
                    }
                    if ((DateTime.UtcNow - _sondaMirado).TotalSeconds < 2) return;
                    _sondaMirado = DateTime.UtcNow;
                    var p = Path.Combine(DirFlujo, "sonda-" + SondaInstrumento() + ".txt");
                    if (!File.Exists(p)) return;
                    string[] c = File.ReadAllText(p).Trim().Split(';'); File.Delete(p);
                    AnotarLinea("sonda: pedido '" + string.Join(";", c) + "'");
                    if (c[0] != "cinta" || c.Length < 4) { AnotarLinea("sonda: pedido no entendido (solo 'cinta;desde;hasta;volmin;minutos')"); return; }
                    DateTime desde = DateTime.Parse(c[1], Inv, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
                    DateTime hasta = DateTime.Parse(c[2], Inv, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
                    _sondaMinVol = Math.Max(1, int.Parse(c[3], Inv));
                    int tramoMin = c.Length > 4 ? int.Parse(c[4], Inv) : 1380;
                    _sondaTramos = new Queue<(DateTime, DateTime)>();
                    for (var t = desde; t < hasta; t = t.AddMinutes(tramoMin)) _sondaTramos.Enqueue((t, t.AddMinutes(tramoMin) < hasta ? t.AddMinutes(tramoMin) : hasta));
                    _sondaSalida = Path.Combine(DirFlujo, "cinta-" + SondaInstrumento() + "-" + desde.ToString("yyyyMMddHHmm", Inv) + "-" + hasta.ToString("yyyyMMddHHmm", Inv) + ".csv");
                    Directory.CreateDirectory(DirFlujo);
                    File.WriteAllText(_sondaSalida, "t,primero,ultimo,vol,lado,prints,abid,abidv,aask,aaskv,dbid,dbidv,dask,daskv\n");
                    _sondaFilas = 0; _sondaTramosHechos = 0; _sondaEsperando = false; _sondaActiva = true;
                    AnotarLinea("sonda: cinta en " + _sondaTramos.Count + " tramo(s) de " + tramoMin + " min -> " + Path.GetFileName(_sondaSalida));
                }
            }
            catch (Exception e) { Anotar(e); lock (_sondaLlave) { _sondaActiva = false; _sondaEsperando = false; } }
        }

        /// <summary>Con la llave tomada. Pide el tramo siguiente o cierra el trabajo.</summary>
        private void SondaSiguienteTramo()
        {
            if (_sondaTramos == null || _sondaTramos.Count == 0)
            {
                AnotarLinea("sonda: TERMINADO, " + _sondaFilas + " ordenes en " + _sondaTramosHechos + " tramo(s) -> " + Path.GetFileName(_sondaSalida));
                try { File.WriteAllText(_sondaSalida + ".listo", _sondaFilas.ToString(Inv)); } catch { }
                _sondaActiva = false; return;
            }
            var (desde, hasta) = _sondaTramos.Dequeue(); _sondaEsperando = true; _sondaPedidoEn = DateTime.UtcNow;
            _sondaPedido = new CumulativeTradesRequest(desde, hasta, _sondaMinVol, 0);
            RequestForCumulativeTrades(_sondaPedido);
        }

        protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
        {
            try { SondaRecibirCinta(request, cumulativeTrades); }
            catch (Exception e) { Anotar(e); }
        }

        private void SondaRecibirCinta(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> trades)
        {
            string salida;
            lock (_sondaLlave)
            {
                if (_sondaPedido == null || request == null || request.RequestId != _sondaPedido.RequestId) return;
                _sondaPedido = null; salida = _sondaSalida;
            }
            var lista = trades?.ToList() ?? new List<CumulativeTrade>();
            int crudas = lista.Count; DateTime d0 = request.BeginTime, d1 = request.EndTime;
            lista = lista.Where(x => x != null && x.Time >= d0 && x.Time < d1).ToList();
            lock (_sondaLlave)
            {
                if (crudas > 50000 && crudas == _sondaCrudasAntes && _sondaTramos != null && _sondaTramos.Count > 0)
                { _sondaTramos.Clear(); AnotarLinea("sonda: CORTADO, ATAS devuelve el mismo bloque de " + crudas + " ordenes en cada tramo: pedir de a una sesion"); }
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
                        sb.Append(t.Time.ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv)).Append(',').Append(t.FirstPrice.ToString("0.####", Inv)).Append(',').Append(t.Lastprice.ToString("0.####", Inv)).Append(',')
                          .Append(t.Volume.ToString("0", Inv)).Append(',').Append(t.Direction == TradeDirection.Buy ? 1 : t.Direction == TradeDirection.Sell ? -1 : 0).Append(',').Append(t.Ticks?.Count ?? 0);
                        foreach (var q in new[] { t.PreviousBid, t.PreviousAsk, t.NewBid, t.NewAsk })
                        { if (q != null) sb.Append(',').Append(q.Price.ToString("0.####", Inv)).Append(',').Append(q.Volume.ToString("0", Inv)); else sb.Append(",,"); }
                        sb.Append('\n'); filas++;
                    }
                    lock (_llaveArchivo) File.AppendAllText(salida, sb.ToString());
                }
                catch (Exception e) { AnotarLinea("sonda cinta: " + e.Message); }
                lock (_sondaLlave) { _sondaFilas += filas; _sondaTramosHechos++; _sondaEsperando = false; }
                AnotarLinea("sonda cinta: tramo " + request.BeginTime.ToString("MM-dd HH:mm", Inv) + " -> " + filas + " ordenes (de " + crudas + " recibidas)");
            });
        }
    }
}
