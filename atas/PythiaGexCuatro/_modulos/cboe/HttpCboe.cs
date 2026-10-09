// HttpCboe.cs — PythiaGex 4.1, modulo cboe (B2). Sin referencias a ATAS.
// La bajada de UNA cadena de CBOE, port de pythiagex/fuentes.bajar con las reglas del proyecto:
//   * siempre comprimida: Accept-Encoding gzip, AutomaticDecompression = None (la pausa tiene que frenar los bytes que viajan por la red,
//     no los ya descomprimidos) y se descomprime en memoria con GZipStream si empieza con 1F 8B o si Content-Encoding es gzip;
//   * sin rafagas: se leen los bytes comprimidos de a trozos con una pausa entre trozos (tope ~ trozo/pausa) Y el socket se abre con un
//     buffer de recepcion chico, asi la ventana de TCP no deja que el servidor mande todo de golpe aunque la aplicacion lea despacio;
//   * todo en el hilo que llama (el hilo propio del descargador): DNS (cancelable, 4.1.0) y conexion en ese hilo, nunca el pool de ATAS;
//   * 4.1.0: una sola conexion TCP por destino y por pedido (SocketsHttpHandler reintentaba solo: 16 conexiones por bajada contra un proxy);
//     el tope de conexion de 15 s se informa como "sin conexion en 15 s (timeout)", no como "cancelado" (eso queda para Parar);
//   * tope de 60 s por pedido (como urllib timeout=60);
//   * URL cdn-api.cboe.com (medido 08-10 22:25 UTC: cdn.cboe.com responde 307 hacia ahi); si falla el DNS o da 404, cdn.cboe.com.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PythiaGexCuatro.Cboe
{
    /// <summary>Resultado de una bajada. Los buffers son del bajador y se reusan en la proxima bajada: consumir antes de volver a llamar.</summary>
    public sealed class RespuestaHttp
    {
        public bool Ok;
        public int Http;                 // 0 si no hubo respuesta
        public string Url = "", Error = "";
        public bool DnsFallo, UsoRespaldo, SinGzip;
        public byte[] Gz; public int GzLargo;          // lo que viajo (comprimido)
        public byte[] Json; public int JsonLargo;      // descomprimido
        public long Ms;
    }

    public interface IHttpCboe
    {
        RespuestaHttp Bajar(string simbolo, int trozoBytes, int pausaMs, CancellationToken ct);
    }

    public sealed class HttpCboe : IHttpCboe, IDisposable
    {
        public static readonly string[] Hosts = { "https://cdn-api.cboe.com", "https://cdn.cboe.com" };
        public const string Ruta = "/api/global/delayed_quotes/options/{0}.json";
        public const string UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36";
        public const int TopeSegundos = 60;
        private const int TopeGz = 64 << 20, TopeJson = 256 << 20;

        private readonly HttpClient _cli;
        private readonly HashSet<string> _conexiones = new HashSet<string>(StringComparer.Ordinal);   // destinos conectados en el pedido en curso
        private byte[] _gz = new byte[1 << 20];
        private byte[] _json = new byte[8 << 20];

        /// <param name="bufferSocketBytes">SO_RCVBUF del socket (0 = el del sistema).</param>
        public HttpCboe(int bufferSocketBytes)
        {
            var h = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.None,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 3,
                UseCookies = false,
                UseProxy = true,
                ConnectTimeout = TimeSpan.FromSeconds(15),
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(100),
                MaxConnectionsPerServer = 1,
            };
            int buf = bufferSocketBytes;
            h.ConnectCallback = (ctx, ct) =>
            {
                // 4.1.0: UNA conexion por destino y por pedido. SocketsHttpHandler reintenta solo cuando una conexion NUEVA se corta sin
                // respuesta (medido contra un proxy local: 16 conexiones por bajada). Una conexion del pool que vencio no pasa por aca, asi que
                // ese reintento legitimo sigue andando; una redireccion a otro host es otro destino.
                string destino = (ctx.InitialRequestMessage?.RequestUri?.Authority ?? "") + "|" + ctx.DnsEndPoint.Host + ":" + ctx.DnsEndPoint.Port;
                lock (_conexiones)
                {
                    if (!_conexiones.Add(destino))
                        throw new IOException("una sola conexion por pedido: la primera se corto sin respuesta (no se reintenta dentro de la vuelta)");
                }
                var s = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    if (buf > 0) s.ReceiveBufferSize = buf;
                    // 4.1.0: el DNS se resuelve con GetAddrInfoEx (cancelable por el token: Parar o el tope de 15 s), ya no con el Connect(host)
                    // sincronico que no se podia cortar. Sigue todo en el hilo que llama (el del descargador), nunca el pool de ATAS.
                    var ips = IPAddress.TryParse(ctx.DnsEndPoint.Host, out var ip) ? new[] { ip }
                              : Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host, ct).GetAwaiter().GetResult();
                    using (ct.Register(() => { try { s.Dispose(); } catch { } }))
                        s.Connect(ips, ctx.DnsEndPoint.Port);
                    ct.ThrowIfCancellationRequested();
                    return new ValueTask<Stream>(new NetworkStream(s, true));
                }
                catch
                {
                    s.Dispose();
                    throw;
                }
            };
            _cli = new HttpClient(h, true) { Timeout = TimeSpan.FromSeconds(TopeSegundos) };
        }

        public RespuestaHttp Bajar(string simbolo, int trozoBytes, int pausaMs, CancellationToken ct)
        {
            RespuestaHttp r = null;
            for (int i = 0; i < Hosts.Length; i++)
            {
                r = BajarUrl(Hosts[i] + string.Format(Ruta, simbolo), trozoBytes, pausaMs, ct);
                r.UsoRespaldo = i > 0;
                if (r.Ok || ct.IsCancellationRequested) return r;
                if (!(r.DnsFallo || r.Http == 404)) return r;    // solo DNS o 404 pasan al host de respaldo
            }
            return r;
        }

        private RespuestaHttp BajarUrl(string url, int trozo, int pausaMs, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var r = new RespuestaHttp { Url = url };
            if (trozo < 4096) trozo = 4096;
            lock (_conexiones) _conexiones.Clear();   // 4.1.0: cada pedido arranca con su cupo de una conexion por destino
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(TopeSegundos));
            HttpResponseMessage resp = null;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.TryAddWithoutValidation("User-Agent", UA);
                req.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip");
                req.Headers.TryAddWithoutValidation("Accept", "application/json");
                resp = _cli.Send(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                r.Http = (int)resp.StatusCode;
                if (!resp.IsSuccessStatusCode) { r.Error = "HTTP " + r.Http; return r; }
                var rsp = resp;
                int total = 0;
                using (cts.Token.Register(() => { try { rsp.Dispose(); } catch { } }))   // corta un Read trabado
                using (var st = resp.Content.ReadAsStream(cts.Token))
                {
                    while (true)
                    {
                        if (_gz.Length < total + trozo) Array.Resize(ref _gz, Math.Max(_gz.Length * 2, total + trozo));
                        int en = 0;
                        while (en < trozo)
                        {
                            int k = st.Read(_gz, total + en, trozo - en);
                            if (k <= 0) break;
                            en += k;
                        }
                        total += en;
                        if (en < trozo) break;                       // fin del cuerpo
                        if (total > TopeGz) throw new InvalidDataException("respuesta de mas de " + (TopeGz >> 20) + " MB");
                        if (pausaMs > 0 && cts.Token.WaitHandle.WaitOne(pausaMs)) cts.Token.ThrowIfCancellationRequested();
                    }
                }
                r.Gz = _gz; r.GzLargo = total;
                bool gzip = resp.Content.Headers.ContentEncoding.Any(e => string.Equals(e, "gzip", StringComparison.OrdinalIgnoreCase))
                            || (total >= 2 && _gz[0] == 0x1F && _gz[1] == 0x8B);
                if (gzip)
                {
                    int n = 0;
                    using (var ms = new MemoryStream(_gz, 0, total, false))
                    using (var z = new GZipStream(ms, CompressionMode.Decompress))
                    {
                        while (true)
                        {
                            if (_json.Length - n < (1 << 16)) Array.Resize(ref _json, _json.Length * 2);
                            int k = z.Read(_json, n, _json.Length - n);
                            if (k <= 0) break;
                            n += k;
                            if (n > TopeJson) throw new InvalidDataException("JSON de mas de " + (TopeJson >> 20) + " MB");
                        }
                    }
                    r.Json = _json; r.JsonLargo = n;
                }
                else
                {
                    if (_json.Length < total) Array.Resize(ref _json, total);
                    Buffer.BlockCopy(_gz, 0, _json, 0, total);
                    r.Json = _json; r.JsonLargo = total; r.SinGzip = true;
                }
                r.Ok = true;
            }
            catch (Exception e) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                r.Error = "sin respuesta completa en " + TopeSegundos + " s (" + e.GetType().Name + ")";
            }
            catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
            {
                // 4.1.0: el ConnectTimeout de 15 s (SYN sin respuesta: sin internet o firewall) NO es un Parar: decirlo como lo que es
                r.Error = (e.InnerException is TimeoutException ? "sin conexion en 15 s" : "sin respuesta") + " (timeout; sin internet?)";
            }
            catch (OperationCanceledException)
            {
                r.Error = "cancelado";
            }
            catch (HttpRequestException e)
            {
                r.DnsFallo = e.HttpRequestError == HttpRequestError.NameResolutionError
                             || (e.InnerException is SocketException se && (se.SocketErrorCode == SocketError.HostNotFound || se.SocketErrorCode == SocketError.TryAgain || se.SocketErrorCode == SocketError.NoData));
                r.Error = (r.DnsFallo ? "DNS: " : "red: ") + Corto(e);
            }
            catch (Exception e)
            {
                r.Error = e.GetType().Name + ": " + Corto(e);
            }
            finally
            {
                try { resp?.Dispose(); } catch { }
                r.Ms = sw.ElapsedMilliseconds;
            }
            return r;
        }

        private static string Corto(Exception e)
        {
            // 4.1.0: primero la causa de adentro ("The response ended prematurely", "No such host is known"); el generico de HttpClient
            // ("An error occurred while sending the request") va despues, porque la pestaña corta el texto
            string m = e.Message;
            var adentro = e.InnerException?.Message;
            if (!string.IsNullOrEmpty(adentro) && adentro != m) m = m.Contains(adentro) ? m : adentro + " / " + m;
            m = m.Replace('\r', ' ').Replace('\n', ' ');
            return m.Length > 160 ? m.Substring(0, 160) : m;
        }

        public void Dispose() { try { _cli.Dispose(); } catch { } }
    }
}
