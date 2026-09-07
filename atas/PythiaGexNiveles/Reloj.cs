using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PythiaGex
{
    /// <summary>
    /// EL DESFASE DEL RELOJ DE LA MAQUINA, MEDIDO CONTRA NTP.
    ///
    /// POR QUE EXISTE
    /// El atraso del libro de futuros se mide restando la hora del barrido
    /// (que la pone el servidor) de la hora de la maquina. Si el reloj de la
    /// maquina esta atrasado, el atraso sale NEGATIVO y se publica igual:
    /// el 2026-09-06 el renglon AUDIT decia lagdom_ms=-3158 mientras
    /// `w32tm /stripchart` daba +3,27 s de desfase. El dato verdadero era
    /// +112 ms. Un numero negativo en un atraso es imposible por definicion,
    /// y publicarlo sin corregir es exactamente el tipo de mentira por
    /// omision que este proyecto existe para no cometer.
    ///
    /// COMO SE MIDE
    /// Un paquete SNTP de 48 bytes al puerto 123 (RFC 4330), sin ninguna
    /// libreria. Desfase = ((t2 - t1) + (t3 - t4)) / 2, con t1/t4 de la
    /// maquina y t2/t3 del servidor. Positivo = la maquina ATRASA. El atraso
    /// corregido es el crudo MAS este desfase.
    ///
    /// LO QUE NO HACE
    /// No toca el reloj del sistema. Solo mide, y si no puede medir lo dice
    /// (Estado = "sinmedir") en vez de suponer cero.
    /// </summary>
    internal static class Reloj
    {
        /// <summary>Servidor menos maquina, en milisegundos. NaN hasta medir.</summary>
        public static double DesfaseMs = double.NaN;
        public static DateTime UltimaMedicion = DateTime.MinValue;
        public static string Estado = "sinmedir";

        private static readonly string[] Servidores =
            { "time.windows.com", "time.google.com", "pool.ntp.org" };

        private static int _midiendo;

        public static async Task Medir(Action<string> log)
        {
            if (System.Threading.Interlocked.Exchange(ref _midiendo, 1) == 1) return;
            try
            {
                foreach (var host in Servidores)
                {
                    try
                    {
                        var r = await ConsultarAsync(host, 3000).ConfigureAwait(false);
                        if (r.HasValue)
                        {
                            DesfaseMs = r.Value;
                            UltimaMedicion = DateTime.UtcNow;
                            Estado = host;
                            log?.Invoke(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                "reloj: desfase {0:+0;-0} ms contra {1} (positivo = la maquina atrasa)",
                                r.Value, host));
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        log?.Invoke("reloj: " + host + " fallo: " + e.Message);
                    }
                }
                Estado = "sinmedir";
                log?.Invoke("reloj: no se pudo medir contra ningun servidor; el atraso se publica SIN corregir");
            }
            finally { System.Threading.Interlocked.Exchange(ref _midiendo, 0); }
        }

        private static async Task<double?> ConsultarAsync(string host, int timeoutMs)
        {
            var addrs = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
            IPAddress ip = null;
            foreach (var a in addrs) if (a.AddressFamily == AddressFamily.InterNetwork) { ip = a; break; }
            if (ip == null && addrs.Length > 0) ip = addrs[0];
            if (ip == null) return null;
            var ep = new IPEndPoint(ip, 123);

            var pkt = new byte[48];
            pkt[0] = 0x1B;   // LI=0, VN=3, Mode=3 (cliente)

            using (var udp = new UdpClient(ep.AddressFamily))
            {
                var t1 = DateTime.UtcNow;
                await udp.SendAsync(pkt, pkt.Length, ep).ConfigureAwait(false);
                var recv = udp.ReceiveAsync();
                var done = await Task.WhenAny(recv, Task.Delay(timeoutMs)).ConfigureAwait(false);
                if (done != recv) return null;
                var t4 = DateTime.UtcNow;
                var b = recv.Result.Buffer;
                if (b == null || b.Length < 48) return null;

                double t2 = NtpSeg(b, 32);   // receive timestamp del servidor
                double t3 = NtpSeg(b, 40);   // transmit timestamp del servidor
                if (t2 <= 0 || t3 <= 0) return null;
                double t1s = UnixSeg(t1), t4s = UnixSeg(t4);
                double desfase = ((t2 - t1s) + (t3 - t4s)) / 2.0;
                // cordura: un desfase de mas de un dia es un paquete roto
                if (Math.Abs(desfase) > 86400) return null;
                return desfase * 1000.0;
            }
        }

        private static double NtpSeg(byte[] b, int i)
        {
            ulong ent = ((ulong)b[i] << 24) | ((ulong)b[i + 1] << 16) | ((ulong)b[i + 2] << 8) | b[i + 3];
            ulong fra = ((ulong)b[i + 4] << 24) | ((ulong)b[i + 5] << 16) | ((ulong)b[i + 6] << 8) | b[i + 7];
            if (ent == 0) return 0;
            // la era NTP arranca en 1900; la de Unix en 1970: 2.208.988.800 s
            return ent - 2208988800.0 + fra / 4294967296.0;
        }

        private static double UnixSeg(DateTime t)
            => (t - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    }
}
