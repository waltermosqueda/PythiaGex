// DobleCboe.cs — arnes NDX (B3b). IFuenteCboe de prueba con las MISMAS fotos de NDX que uso la vista previa para una sesion:
//   backtest_familia.cargar_cboe('NQ', dias_cal = dia-6 .. dia) sobre libros.fotos_cboe (las tres carpetas de cadenas + la bajada local
//   directa; ventana de cada sesion [d-2h, d+21h] con los extremos incluidos; dedup por cadena_ts: gana la bajada MAS TEMPRANA) y el corte
//   'generado <= hasta' (dia 21:00 UTC) de preview_niveles.Motor(vivo=False).
// La primera corrida CONGELA la lista en entrada/cboe-NDX-<dia>.jsonl.gz (los archivos de origen siguen creciendo); las siguientes leen eso.
// No es el IFuenteCboe del indicador (ese es de B2).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PythiaGexCuatro.Familia;
using PythiaGexCuatro.Familia.Ndx;

namespace ArnesNdx
{
    public sealed class DobleCboe : IFuenteCboe
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly Regex RxTs = new Regex("\"cadena_ts\":\"([^\"]+)\"", RegexOptions.Compiled);
        static readonly Regex RxGen = new Regex("\"generado\":\"([^\"]+)\"", RegexOptions.Compiled);
        public const int RETRASO_S = 902;

        readonly List<FotoCadena> _fotos;
        public readonly List<string> Fuentes = new List<string>();
        public string Origen = "";

        public DobleCboe(List<FotoCadena> fotos) { _fotos = fotos; }

        public void Arrancar() { }
        public void Parar() { }
        public long Version => 1;
        public string Estado => "doble de prueba (" + _fotos.Count + " fotos NDX)";
        public FotoCadena Ultima(string libro) => libro == "NDX" && _fotos.Count > 0 ? _fotos[_fotos.Count - 1] : null;
        public IReadOnlyList<FotoCadena> Fotos(string libro, DateTime desdeUtc, DateTime hastaUtc)
            => libro == "NDX" ? _fotos.Where(f => f.GeneradoUtc >= desdeUtc && f.GeneradoUtc < hastaUtc).ToList() : new List<FotoCadena>();
        public int Cuenta => _fotos.Count;

        // ------------------------------------------------------------------ armado como la vista previa (una sola vez) o desde la entrada congelada
        public static DobleCboe ParaSesion(DateTime dia, string appAtas, string carpetaEntrada, Action<string> log)
        {
            var congelada = Path.Combine(carpetaEntrada, "cboe-NDX-" + dia.ToString("yyyy-MM-dd", Inv) + ".jsonl.gz");
            if (File.Exists(congelada))
            {
                var l = LeerCongelada(congelada);
                log("  CBOE NDX: " + l.Count + " fotos de la entrada congelada " + Path.GetFileName(congelada));
                return new DobleCboe(l) { Origen = congelada };
            }
            var t0 = DateTime.UtcNow;
            var fotos = CargarComoPython(dia, appAtas, log);
            Directory.CreateDirectory(carpetaEntrada);
            Congelar(congelada, fotos);
            log("  CBOE NDX: " + fotos.Count + " fotos leidas de los archivos de origen en " + (DateTime.UtcNow - t0).TotalSeconds.ToString("0.0", Inv) + " s; congeladas en " + Path.GetFileName(congelada));
            return new DobleCboe(fotos) { Origen = "origen" };
        }

        sealed class Cruda { public FotoCadena F; public string Ts; public string Fuente; }

        static List<FotoCadena> CargarComoPython(DateTime dia, string appAtas, Action<string> log)
        {
            var p1 = Path.Combine(appAtas, "PythiaGex"); var p2 = Path.Combine(appAtas, "PythiaGex2");
            var carpetas = new[] { Path.Combine(p1, "cadenas"), Path.Combine(p2, "cadenas"), Path.Combine(p1, "cboe-local") };
            // cargar_cboe: vistos por ts entre todos los dias (gana la mas temprana; una igual no reemplaza), en orden de insercion
            var orden = new List<string>(); var vistos = new Dictionary<string, Cruda>();
            for (int k = 6; k >= 0; k--)
            {
                var d = dia.AddDays(-k);
                foreach (var c in FotosCboe("NQ", d, carpetas, p1, log))
                {
                    if (!vistos.TryGetValue(c.Ts, out var v)) { orden.Add(c.Ts); vistos[c.Ts] = c; }
                    else if (v.F.GeneradoUtc > c.F.GeneradoUtc) vistos[c.Ts] = c;
                }
            }
            var hasta = CalendarioNdx.Utc(dia.AddHours(21));
            return orden.Select(ts => vistos[ts].F).OrderBy(f => f.GeneradoUtc).Where(f => f.GeneradoUtc <= hasta).ToList();   // OrderBy estable
        }

        /// <summary>libros.fotos_cboe(ticker, d).</summary>
        static List<Cruda> FotosCboe(string tk, DateTime d, string[] carpetas, string p1, Action<string> log)
        {
            var ini = CalendarioNdx.Utc(d.AddHours(-2)); var fin = CalendarioNdx.Utc(d.AddHours(21));
            var arch = new List<string>();
            foreach (var dd in new[] { d.AddDays(-1), d })
            {
                var s = dd.ToString("yyyy-MM-dd", Inv);
                foreach (var c in carpetas) { var p = Path.Combine(c, "cadena-" + tk + "-" + s + ".jsonl.gz"); if (File.Exists(p)) arch.Add(p); }
                var lp = Path.Combine(p1, "cadenas", "local-" + tk + "-" + s + ".jsonl"); if (File.Exists(lp)) arch.Add(lp);
            }
            arch.Sort(StringComparer.Ordinal);                      // sorted(archivos) de Python (orden de codigo: 'PythiaGex2' < 'PythiaGex\')
            var orden = new List<string>(); var vistos = new Dictionary<string, Cruda>();
            foreach (var p in arch)
            {
                string fuente = Path.GetFileName(p).StartsWith("local-") ? "local-directo" : Path.GetFileName(Path.GetDirectoryName(p));
                try
                {
                    using var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
                    Stream st = p.EndsWith(".gz") ? new GZipStream(fs, CompressionMode.Decompress) : (Stream)fs;
                    using var sr = new StreamReader(st, new UTF8Encoding(false), false, 1 << 16);
                    string l;
                    while (true)
                    {
                        try { l = sr.ReadLine(); } catch (InvalidDataException) { break; }   // gz a medio escribir: lo leido vale (EOFError en Python)
                        if (l == null) break;
                        var cab = l.Length > 500 ? l.Substring(0, 500) : l;
                        var m = RxTs.Match(cab); var g = RxGen.Match(cab);
                        if (!m.Success || !g.Success) continue;
                        string ts = m.Groups[1].Value;
                        if (!DateTimeOffset.TryParse(g.Groups[1].Value.Replace("\\u002B", "+"), Inv, DateTimeStyles.AssumeUniversal, out var go)) continue;
                        var gen = CalendarioNdx.Utc(go.UtcDateTime);
                        if (gen < ini || gen > fin) continue;
                        if (vistos.TryGetValue(ts, out var v) && v.F.GeneradoUtc <= gen) continue;
                        var f = Parsear(l, gen, ts);
                        if (f == null) continue;
                        if (!vistos.ContainsKey(ts)) orden.Add(ts);
                        vistos[ts] = new Cruda { F = f, Ts = ts, Fuente = fuente };
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is InvalidDataException) { log("  aviso: " + Path.GetFileName(p) + " ilegible (" + ex.Message + ")"); }
            }
            return orden.Select(ts => vistos[ts]).OrderBy(c => c.F.GeneradoUtc).ToList();
        }

        static double Num(JsonElement e) => e.ValueKind == JsonValueKind.Number ? e.GetDouble() : double.NaN;

        /// <summary>El dict de libros.fotos_cboe / preview_niveles.foto_cboe_de_json como FotoCadena.</summary>
        static FotoCadena Parsear(string l, DateTime gen, string ts)
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(l); } catch { return null; }
            using (doc)
            {
                var j = doc.RootElement;
                if (!j.TryGetProperty("cadena", out var c) || c.ValueKind != JsonValueKind.Object) return null;
                var filas = new List<FilaCadena>();
                if (c.TryGetProperty("filas", out var fs) && fs.ValueKind == JsonValueKind.Array)
                    foreach (var x in fs.EnumerateArray())
                    {
                        if (x.GetArrayLength() < 8) continue;
                        var a = new double[8]; int i = 0;
                        foreach (var y in x.EnumerateArray()) { if (i >= 8) break; a[i++] = Num(y); }
                        filas.Add(new FilaCadena(a[0], (int)a[1], a[2], a[3], a[4], a[5], a[6], a[7]));
                    }
                if (filas.Count == 0) return null;
                var tsUtc = CalendarioNdx.Utc(DateTime.ParseExact(ts, "yyyy-MM-dd HH:mm:ss", Inv));
                DateTime dato;
                string ut = c.TryGetProperty("ultimo_trade", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
                if (string.IsNullOrEmpty(ut) || !CalendarioNdx.NyTextoAUtc(ut, out dato))
                {
                    int ret = j.TryGetProperty("retraso_s", out var rs) && rs.ValueKind == JsonValueKind.Number && rs.GetDouble() != 0 ? (int)rs.GetDouble() : RETRASO_S;
                    dato = tsUtc.AddSeconds(-ret);
                }
                double spot = 0;
                if (c.TryGetProperty("spot_idx", out var si) && si.ValueKind == JsonValueKind.Number && si.GetDouble() != 0) spot = si.GetDouble();
                else if (j.TryGetProperty("spot", out var sp) && sp.ValueKind == JsonValueKind.Number) spot = sp.GetDouble();
                var dias = new List<double>();
                if (c.TryGetProperty("vencimientos", out var vs) && vs.ValueKind == JsonValueKind.Array)
                    foreach (var v in vs.EnumerateArray()) dias.Add(v.TryGetProperty("dias", out var dd) && dd.ValueKind == JsonValueKind.Number ? dd.GetDouble() : 0.0);
                double oi = 0; foreach (var x in filas) oi += x.OiC; double oip = 0; foreach (var x in filas) oip += x.OiP;
                var f = new FotoCadena
                {
                    Libro = "NDX", GeneradoUtc = gen, TsUtc = tsUtc, DatoUtc = dato, Spot = spot, Dias = dias.ToArray(), Filas = filas.ToArray(),
                    EsFuturo = false, OiTotal = oi + oip,
                };
                if (j.TryGetProperty("base_cruda", out var bc) && bc.ValueKind == JsonValueKind.Number) f.BaseCruda = bc.GetDouble();
                if (j.TryGetProperty("base_error_ticks", out var be) && be.ValueKind == JsonValueKind.Number) f.BaseErrorTicks = be.GetDouble();
                return f;
            }
        }

        // ------------------------------------------------------------------ entrada congelada (formato propio del arnes, doubles "R")
        static void Congelar(string ruta, List<FotoCadena> fotos)
        {
            var tmp = ruta + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
            using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
            using (var w = new StreamWriter(gz, new UTF8Encoding(false)))
            {
                foreach (var f in fotos)
                {
                    var sb = new StringBuilder(8192);
                    sb.Append("{\"generado\":\"").Append(f.GeneradoUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", Inv))
                      .Append("\",\"ts\":\"").Append(f.TsUtc.ToString("yyyy-MM-dd HH:mm:ss", Inv))
                      .Append("\",\"dato\":\"").Append(f.DatoUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", Inv))
                      .Append("\",\"spot\":").Append(R(f.Spot)).Append(",\"oi_total\":").Append(R(f.OiTotal))
                      .Append(",\"base_cruda\":").Append(R(f.BaseCruda)).Append(",\"err\":").Append(R(f.BaseErrorTicks))
                      .Append(",\"dias\":[").Append(string.Join(",", f.Dias.Select(R))).Append("],\"filas\":[");
                    for (int i = 0; i < f.Filas.Length; i++)
                    {
                        var x = f.Filas[i];
                        if (i > 0) sb.Append(',');
                        sb.Append('[').Append(R(x.K)).Append(',').Append(x.V).Append(',').Append(R(x.OiC)).Append(',').Append(R(x.OiP)).Append(',').Append(R(x.IvC)).Append(',')
                          .Append(R(x.IvP)).Append(',').Append(R(x.VolC)).Append(',').Append(R(x.VolP)).Append(']');
                    }
                    sb.Append("]}");
                    w.WriteLine(sb.ToString());
                }
            }
            File.Move(tmp, ruta, true);
        }

        static string R(double v) => double.IsNaN(v) ? "null" : v.ToString("R", Inv);

        static List<FotoCadena> LeerCongelada(string ruta)
        {
            var r = new List<FotoCadena>();
            using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var sr = new StreamReader(gz, new UTF8Encoding(false));
            string l;
            while ((l = sr.ReadLine()) != null)
            {
                using var doc = JsonDocument.Parse(l);
                var j = doc.RootElement;
                DateTime U(string n) => CalendarioNdx.Utc(DateTime.Parse(j.GetProperty(n).GetString(), Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));
                var filas = new List<FilaCadena>();
                foreach (var x in j.GetProperty("filas").EnumerateArray())
                {
                    var a = x.EnumerateArray().Select(Num).ToArray();
                    filas.Add(new FilaCadena(a[0], (int)a[1], a[2], a[3], a[4], a[5], a[6], a[7]));
                }
                r.Add(new FotoCadena
                {
                    Libro = "NDX", GeneradoUtc = U("generado"),
                    TsUtc = CalendarioNdx.Utc(DateTime.ParseExact(j.GetProperty("ts").GetString(), "yyyy-MM-dd HH:mm:ss", Inv)),
                    DatoUtc = U("dato"), Spot = Num(j.GetProperty("spot")), OiTotal = Num(j.GetProperty("oi_total")),
                    BaseCruda = Num(j.GetProperty("base_cruda")), BaseErrorTicks = Num(j.GetProperty("err")),
                    Dias = j.GetProperty("dias").EnumerateArray().Select(Num).ToArray(), Filas = filas.ToArray(), EsFuturo = false,
                });
            }
            return r;
        }
    }
}
