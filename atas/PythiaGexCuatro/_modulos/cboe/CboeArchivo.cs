// CboeArchivo.cs — PythiaGex 4.1, modulo cboe (B2). Sin referencias a ATAS.
// El archivo propio en %APPDATA%\ATAS\PythiaGex4\cboe\ (mismo formato que cboe-local, asi el laboratorio lo lee cambiando solo la ruta):
//   ultima-<NQ|QQQ|TQQQ>.json            la ultima linea flaca, sin comprimir, escrita atomica (temporal + reemplazo)
//   cadena-<X>-<yyyy-MM-dd UTC de generado>.jsonl.gz   un miembro gzip por linea, agregado al final
//   cadena-<X>.ultimo                    el sello (timestamp de CBOE) de la ultima linea archivada
//   cierres.json                         por fecha NY y libro: cierre anterior (prev_day_close de la rueda), close tras 15:59 NY y el
//                                        prev_day_close visto despues del cierre (para ver cuando cambia). Solo datos, sin interpretar.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace PythiaGexCuatro.Cboe
{
    public static class CboeArchivo
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        private static readonly Regex RxDia = new Regex(@"^cadena-(NQ|QQQ|TQQQ)-(\d{4}-\d{2}-\d{2})\.jsonl\.gz$", RegexOptions.CultureInvariant);

        public static string RutaDia(string carpeta, string archivo, DateTime diaUtc) =>
            Path.Combine(carpeta, "cadena-" + archivo + "-" + diaUtc.ToString("yyyy-MM-dd", Inv) + ".jsonl.gz");
        public static string RutaUltima(string carpeta, string archivo) => Path.Combine(carpeta, "ultima-" + archivo + ".json");
        public static string RutaSello(string carpeta, string archivo) => Path.Combine(carpeta, "cadena-" + archivo + ".ultimo");
        public static string RutaCierres(string carpeta) => Path.Combine(carpeta, "cierres.json");

        /// <summary>Agrega la linea como un miembro gzip completo (como gzip.compress de Python): el archivo se puede seguir leyendo
        /// sin descomprimir lo anterior.</summary>
        public static void AgregarLinea(string ruta, string linea)
        {
            byte[] gz;
            using (var ms = new MemoryStream())
            {
                using (var z = new GZipStream(ms, CompressionLevel.Optimal, true))
                {
                    var b = Utf8.GetBytes(linea + "\n");
                    z.Write(b, 0, b.Length);
                }
                gz = ms.ToArray();
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            Reintentar(() =>
            {
                using var fs = new FileStream(ruta, FileMode.Append, FileAccess.Write, FileShare.Read);
                fs.Write(gz, 0, gz.Length);
            });
        }

        /// <summary>Escritura atomica: temporal en la misma carpeta y reemplazo. Los lectores abren con FileShare.Delete.</summary>
        public static void EscribirAtomico(string ruta, string texto)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            string tmp = ruta + ".tmp" + Environment.CurrentManagedThreadId.ToString(Inv);
            File.WriteAllText(tmp, texto, Utf8);
            Reintentar(() => File.Move(tmp, ruta, true));
        }

        public static string LeerTexto(string ruta)
        {
            try
            {
                using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var sr = new StreamReader(fs, Utf8);
                return sr.ReadToEnd();
            }
            catch { return null; }
        }

        private static void Reintentar(Action a)
        {
            for (int i = 0; ; i++)
            {
                try { a(); return; }
                catch (IOException) when (i < 4) { Thread.Sleep(60); }
                catch (UnauthorizedAccessException) when (i < 4) { Thread.Sleep(60); }
            }
        }

        /// <summary>Lee un archivo del dia (miembros gzip concatenados) y devuelve las lineas leidas en orden. Las ilegibles se saltean.
        /// Descomprime de a bloques y corta por '\n' sin armar el texto entero.</summary>
        public static List<LineaCboe> LeerDia(string ruta, Func<DateTime, bool> conFilas, out int ilegibles)
        {
            ilegibles = 0;
            var res = new List<LineaCboe>();
            if (!File.Exists(ruta)) return res;
            byte[] buf = new byte[1 << 20];
            int len = 0;
            using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16))
            using (var z = new GZipStream(fs, CompressionMode.Decompress))
            {
                while (true)
                {
                    if (len == buf.Length) Array.Resize(ref buf, buf.Length * 2);
                    int k;
                    try { k = z.Read(buf, len, buf.Length - len); }
                    catch (InvalidDataException) { k = 0; ilegibles++; }      // cola truncada (se corto la luz a mitad de un miembro)
                    if (k <= 0) break;
                    len += k;
                    int ini = 0;
                    for (int i = 0; i < len; i++)
                    {
                        if (buf[i] != (byte)'\n') continue;
                        Procesar(new ReadOnlySpan<byte>(buf, ini, i - ini), conFilas, res, ref ilegibles);
                        ini = i + 1;
                    }
                    if (ini > 0) { Buffer.BlockCopy(buf, ini, buf, 0, len - ini); len -= ini; }
                }
            }
            if (len > 0) Procesar(new ReadOnlySpan<byte>(buf, 0, len), conFilas, res, ref ilegibles);
            return res;
        }

        private static void Procesar(ReadOnlySpan<byte> l, Func<DateTime, bool> conFilas, List<LineaCboe> res, ref int ilegibles)
        {
            while (l.Length > 0 && (l[l.Length - 1] == (byte)'\r' || l[l.Length - 1] == (byte)' ')) l = l.Slice(0, l.Length - 1);
            if (l.Length == 0) return;
            var x = CboeCadena.DeLinea(l, conFilas);
            if (x == null) ilegibles++; else res.Add(x);
        }

        public static string LeerSello(string carpeta, string archivo)
        {
            var t = LeerTexto(RutaSello(carpeta, archivo));
            return t?.Trim() ?? "";
        }

        public static void AnotarSello(string carpeta, string archivo, string sello)
        {
            Directory.CreateDirectory(carpeta);
            Reintentar(() => File.WriteAllText(RutaSello(carpeta, archivo), sello ?? "", Utf8));
        }

        /// <summary>Poda los cadena-*-dia.jsonl.gz PROPIOS con dia &lt; hoy - dias. Solo toca archivos con ese nombre exacto en la carpeta propia.</summary>
        public static int Podar(string carpeta, int dias, DateTime hoyUtc, LogCboe log)
        {
            if (dias <= 0 || !Directory.Exists(carpeta)) return 0;
            int n = 0;
            var corte = hoyUtc.Date.AddDays(-dias);
            foreach (var p in Directory.GetFiles(carpeta, "cadena-*.jsonl.gz"))
            {
                var m = RxDia.Match(Path.GetFileName(p));
                if (!m.Success) continue;
                if (!DateTime.TryParseExact(m.Groups[2].Value, "yyyy-MM-dd", Inv, DateTimeStyles.None, out var d)) continue;
                if (d >= corte) continue;
                try { File.Delete(p); n++; log?.Linea("poda: " + Path.GetFileName(p) + " (retencion " + dias + " dias)"); } catch { }
            }
            return n;
        }
    }

    /// <summary>Lo observado de un subyacente para una fecha NY (cierres.json). Solo datos con su hora; no decide nada.</summary>
    public sealed class CierreCboe
    {
        public double Anterior = double.NaN;      // prev_day_close validado en la rueda/premarket de esta fecha (= cierre de la sesion previa)
        public DateTime AnteriorUtc;
        public double Close = double.NaN;         // data.close visto con last_trade_time >= 15:59 NY de esta fecha (el ultimo visto)
        public DateTime CloseUtc;
        public double PdcTrasCierreIni = double.NaN;   // prev_day_close visto por primera vez despues de las 15:59 NY de esta fecha
        public DateTime PdcTrasCierreIniUtc;
        public double PdcTrasCierreFin = double.NaN;   // el ultimo visto (si difiere de Ini, CBOE ya lo cambio: es el cierre de esta fecha)
        public DateTime PdcTrasCierreFinUtc;
        public DateTime PdcCambioUtc;                  // primera foto en que difirio de Ini (default si no cambio)

        public CierreCboe Copia() => (CierreCboe)MemberwiseClone();
    }

    /// <summary>cierres.json: fecha NY (yyyy-MM-dd) -> libro -> CierreCboe. Hilo-seguro.</summary>
    public sealed class CierresCboe
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly object _llave = new object();
        private readonly SortedDictionary<string, Dictionary<string, CierreCboe>> _d = new SortedDictionary<string, Dictionary<string, CierreCboe>>(StringComparer.Ordinal);
        public bool Cambiado;

        public CierreCboe Ver(string libro, string fechaNy)
        {
            lock (_llave)
                return _d.TryGetValue(fechaNy, out var m) && m.TryGetValue(libro, out var c) ? c.Copia() : null;
        }

        /// <summary>Anota lo que dice la foto. generado = hora UTC en que se vio.</summary>
        public void Observar(string libro, SubCboe s, DateTime tsUtc, DateTime generadoUtc)
        {
            if (s == null || string.IsNullOrEmpty(s.LastTradeTime)) return;
            if (!DateTime.TryParse(s.LastTradeTime, Inv, DateTimeStyles.None, out var lt)) return;
            string f = lt.ToString("yyyy-MM-dd", Inv);
            var hoyNy = HoraNyCboe.DeUtc(tsUtc).Date;
            bool trasCierre = lt.TimeOfDay >= new TimeSpan(15, 59, 0);
            lock (_llave)
            {
                if (!_d.TryGetValue(f, out var m)) { m = new Dictionary<string, CierreCboe>(); _d[f] = m; }
                if (!m.TryGetValue(libro, out var c)) { c = new CierreCboe(); m[libro] = c; }
                // rueda/premarket de f: el prev_day_close es el cierre de la sesion previa (y la cuenta de price_change lo confirma)
                if (!trasCierre && lt.Date == hoyNy && !double.IsNaN(s.PrevDayClose) && s.PrevDayClose > 0 && !double.IsNaN(s.CurrentPrice) && !double.IsNaN(s.PriceChange)
                    && Math.Abs(s.CurrentPrice - s.PrevDayClose - s.PriceChange) < 0.01)
                {
                    if (c.Anterior != s.PrevDayClose) { c.Anterior = s.PrevDayClose; c.AnteriorUtc = generadoUtc; Cambiado = true; }
                }
                if (trasCierre)
                {
                    if (!double.IsNaN(s.Close) && s.Close > 0 && c.Close != s.Close) { c.Close = s.Close; c.CloseUtc = generadoUtc; Cambiado = true; }
                    if (!double.IsNaN(s.PrevDayClose) && s.PrevDayClose > 0)
                    {
                        if (double.IsNaN(c.PdcTrasCierreIni)) { c.PdcTrasCierreIni = s.PrevDayClose; c.PdcTrasCierreIniUtc = generadoUtc; Cambiado = true; }
                        if (c.PdcTrasCierreFin != s.PrevDayClose)
                        {
                            if (c.PdcCambioUtc == default && !double.IsNaN(c.PdcTrasCierreFin) && s.PrevDayClose != c.PdcTrasCierreIni) c.PdcCambioUtc = generadoUtc;
                            c.PdcTrasCierreFin = s.PrevDayClose; c.PdcTrasCierreFinUtc = generadoUtc; Cambiado = true;
                        }
                    }
                }
                while (_d.Count > 40) { var e = _d.GetEnumerator(); e.MoveNext(); _d.Remove(e.Current.Key); }
            }
        }

        public string Json()
        {
            var sb = new StringBuilder(4096);
            lock (_llave)
            {
                sb.Append('{');
                bool p1 = true;
                foreach (var kv in _d)
                {
                    if (!p1) sb.Append(','); p1 = false;
                    PyCboe.Str(sb, kv.Key); sb.Append(":{");
                    bool p2 = true;
                    foreach (var lv in kv.Value)
                    {
                        if (!p2) sb.Append(','); p2 = false;
                        var c = lv.Value;
                        PyCboe.Str(sb, lv.Key); sb.Append(":{");
                        sb.Append("\"anterior\":").Append(N(c.Anterior)).Append(",\"anterior_utc\":"); T(sb, c.AnteriorUtc);
                        sb.Append(",\"close\":").Append(N(c.Close)).Append(",\"close_utc\":"); T(sb, c.CloseUtc);
                        sb.Append(",\"pdc_tras_cierre_ini\":").Append(N(c.PdcTrasCierreIni)).Append(",\"pdc_tras_cierre_ini_utc\":"); T(sb, c.PdcTrasCierreIniUtc);
                        sb.Append(",\"pdc_tras_cierre_fin\":").Append(N(c.PdcTrasCierreFin)).Append(",\"pdc_tras_cierre_fin_utc\":"); T(sb, c.PdcTrasCierreFinUtc);
                        sb.Append(",\"pdc_cambio_utc\":"); T(sb, c.PdcCambioUtc);
                        sb.Append('}');
                    }
                    sb.Append('}');
                }
                sb.Append('}');
            }
            return sb.ToString();
        }

        private static string N(double v) => double.IsNaN(v) ? "null" : PyCboe.Repr(v);
        private static void T(StringBuilder sb, DateTime t) { if (t == default) sb.Append("null"); else PyCboe.Str(sb, CboeCadena.IsoGenerado(t)); }

        public void Cargar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return;
            try
            {
                using var doc = JsonDocument.Parse(texto);
                lock (_llave)
                {
                    foreach (var f in doc.RootElement.EnumerateObject())
                    {
                        if (f.Value.ValueKind != JsonValueKind.Object) continue;
                        if (!_d.TryGetValue(f.Name, out var m)) { m = new Dictionary<string, CierreCboe>(); _d[f.Name] = m; }
                        foreach (var l in f.Value.EnumerateObject())
                        {
                            var c = new CierreCboe
                            {
                                Anterior = D(l.Value, "anterior"), AnteriorUtc = U(l.Value, "anterior_utc"),
                                Close = D(l.Value, "close"), CloseUtc = U(l.Value, "close_utc"),
                                PdcTrasCierreIni = D(l.Value, "pdc_tras_cierre_ini"), PdcTrasCierreIniUtc = U(l.Value, "pdc_tras_cierre_ini_utc"),
                                PdcTrasCierreFin = D(l.Value, "pdc_tras_cierre_fin"), PdcTrasCierreFinUtc = U(l.Value, "pdc_tras_cierre_fin_utc"),
                                PdcCambioUtc = U(l.Value, "pdc_cambio_utc"),
                            };
                            m[l.Name] = c;
                        }
                    }
                }
            }
            catch { }
        }

        private static double D(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN;
        private static DateTime U(JsonElement e, string k) =>
            e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String && CboeCadena.ParsearGenerado(v.GetString(), out var t) ? t : default;
    }
}
