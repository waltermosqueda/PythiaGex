// TqqqArchivo.cs — PythiaGex 4.1, modulo TQQQ (B4), 08-10-2026.
// Persistencia propia en %APPDATA%\ATAS\PythiaGex4\familia (ningun nombre coincide con los de la 3.0):
//   tqqq-muestras-<sesion>-<contrato>.csv  una linea por foto con precio del MNQ valido en (sello - 900 s):
//                                          ts,generado,spot,mnq,muestra,vivo,rueda  (al arrancar repone el precio de las fotos cuya cinta ya no
//                                          esta en memoria: un reinicio de noche no necesita la cinta vieja para rehacer c)
//   tqqq-cierres.json                      por fecha NY y contrato: s, P0/N0, NQ_1600, NDX congelado, c al cierre, anclas B/C y sus errores
// Log propio: %APPDATA%\ATAS\pythiagex4-tqqq.log (ATAS se traga las excepciones). Errores: una linea por minuto y lugar como maximo.
// Todo se llama desde el hilo de la Familia (nunca desde el render): escrituras chicas, append o atomicas (temporal + reemplazo).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PythiaGexCuatro.Familia
{
    public sealed class TqqqLog
    {
        private readonly string _ruta;
        private readonly object _llave = new object();
        private readonly Dictionary<string, DateTime> _ultimoError = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        public const long TOPE_BYTES = 10L << 20;
        private int _escrituras;
        public TqqqLog(string ruta) { _ruta = ruta; }
        public string Ruta => _ruta;

        public void Linea(string texto)
        {
            if (string.IsNullOrEmpty(_ruta)) return;
            try
            {
                lock (_llave)
                {
                    var dir = Path.GetDirectoryName(_ruta);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    // 4.1.0: tope de 10 MB (como LogCboe): al pasarlo se aparta a .1 (pisando el anterior)
                    if ((++_escrituras & 63) == 1) { try { var fi = new FileInfo(_ruta); if (fi.Exists && fi.Length > TOPE_BYTES) File.Move(_ruta, _ruta + ".1", true); } catch { } }
                    File.AppendAllText(_ruta, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC " + texto + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }

        public void Error(string lugar, Exception e)
        {
            var ahora = DateTime.UtcNow;
            lock (_llave)
            {
                if (_ultimoError.TryGetValue(lugar ?? "", out var u) && (ahora - u).TotalSeconds < 60) return;
                _ultimoError[lugar ?? ""] = ahora;
            }
            Linea("ERROR " + lugar + ": " + (e == null ? "?" : e.GetType().Name + ": " + e.Message + " | " + (e.StackTrace ?? "").Replace(Environment.NewLine, " | ")));
        }
    }

    public static class TqqqArchivo
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public const string CabMuestras = "ts,generado,spot,mnq,muestra,vivo,rueda";

        public static string RutaMuestras(string carpeta, string dia, string contrato) =>
            string.IsNullOrEmpty(carpeta) ? null : Path.Combine(carpeta, "tqqq-muestras-" + dia + "-" + Limpio(contrato) + ".csv");

        public static string RutaCierres(string carpeta) => string.IsNullOrEmpty(carpeta) ? null : Path.Combine(carpeta, "tqqq-cierres.json");

        private static string Limpio(string c)
        {
            if (string.IsNullOrEmpty(c)) return "sin";
            var sb = new StringBuilder();
            foreach (var ch in c) if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            return sb.Length > 0 ? sb.ToString() : "sin";
        }

        public static string Iso(DateTime utc) => utc == default ? "" : utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", Inv);

        public static DateTime DeIso(string s)
        {
            if (string.IsNullOrEmpty(s)) return default;
            return DateTime.TryParse(s, Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) ? DateTime.SpecifyKind(t, DateTimeKind.Utc) : default;
        }

        /// <summary>Lee las muestras persistidas: sello (ticks UTC) -> precio del MNQ en (sello - 900 s).</summary>
        public static Dictionary<long, double> LeerMuestras(string ruta)
        {
            var d = new Dictionary<long, double>();
            if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) return d;
            foreach (var l in File.ReadLines(ruta))
            {
                if (l.Length < 10 || l.StartsWith("ts,", StringComparison.Ordinal)) continue;
                var x = l.Split(',');
                if (x.Length < 4) continue;
                var ts = DeIso(x[0]);
                if (ts == default) continue;
                if (double.TryParse(x[3], NumberStyles.Float, Inv, out var px) && !double.IsNaN(px)) d[ts.Ticks] = px;
            }
            return d;
        }

        public static void AnexarMuestras(string ruta, List<string> lineas)
        {
            if (string.IsNullOrEmpty(ruta) || lineas == null || lineas.Count == 0) return;
            var dir = Path.GetDirectoryName(ruta);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            bool nuevo = !File.Exists(ruta);
            var sb = new StringBuilder();
            if (nuevo) sb.Append(CabMuestras).Append('\n');
            foreach (var l in lineas) sb.Append(l).Append('\n');
            File.AppendAllText(ruta, sb.ToString(), new UTF8Encoding(false));
        }

        public static string LineaMuestra(DateTime ts, DateTime gen, double spot, double px, double muestra, bool vivo, bool rueda) =>
            Iso(ts) + "," + Iso(gen) + "," + N(spot) + "," + N(px) + "," + N(muestra) + "," + (vivo ? "1" : "0") + "," + (rueda ? "1" : "0");

        public static string N(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "" : v.ToString("R", Inv);

        public static void EscribirAtomico(string ruta, string texto)
        {
            if (string.IsNullOrEmpty(ruta)) return;
            var dir = Path.GetDirectoryName(ruta);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = ruta + ".tmp";
            File.WriteAllText(tmp, texto, new UTF8Encoding(false));
            if (File.Exists(ruta)) File.Replace(tmp, ruta, null); else File.Move(tmp, ruta);
        }

        /// <summary>Borra tqqq-muestras-* de mas de 'dias' dias (por fecha de modificacion).</summary>
        public static int Podar(string carpeta, int dias)
        {
            if (string.IsNullOrEmpty(carpeta) || dias <= 0 || !Directory.Exists(carpeta)) return 0;
            int n = 0; var corte = DateTime.UtcNow.AddDays(-dias);
            foreach (var p in Directory.GetFiles(carpeta, "tqqq-muestras-*.csv"))
                try { if (File.GetLastWriteTimeUtc(p) < corte) { File.Delete(p); n++; } } catch { }
            return n;
        }
    }
}
