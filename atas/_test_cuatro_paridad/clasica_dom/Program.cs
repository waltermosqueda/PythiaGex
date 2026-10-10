// Program.cs — arnes 'clasica_dom' de PythiaGex 4.1.5b (09-10-2026). Salida 0 = todo verde. Resultados en .\resultados\clasica_dom.txt.
// Las dominantes D1-D3 de la capa NDX de la clasica (serie R10_NDX_dom "Clasica NDX", DominantesClasica.Calcular en _modulos/familia/clasica/ClasicaNdx.cs).
// Datos REALES, solo lectura: se copian a %TEMP%\pg4_clasica_dom (primero los niv, despues las cadenas: toda cadena que uso un minuto copiado ya esta en la
// copia); el log de la clasica se lee compartido y se extraen sus AUDIT capa=NDX del dia. En el medio corre ref_dominantes.py (python -I -B), que calcula
// lo mismo con el port Python VERIFICADO de la clasica (laboratorio/calibracion_1009/receta_clasica_dominantes/dominantes/extraer_dominantes_clasica.py).
//   A. NIV: TODOS los minutos de niv-*.jsonl con R10_NDX_dom. Por minuto: la cadena con la regla de ClasicaNdx (dedup por sello, la ultima con generado al
//      segundo <= t) en C# y en Python (la misma); la cuenta en C# con el fut, la base y la hora del minuto da EXACTAMENTE lo que guardo la 4.1.5b
//      (precio, rol, monto y strike); el port Python da lo mismo (con el redondeo de lo guardado); C# = Python a 1e-6.
//   B. LA RUEDA DE HOY SIMULADA con base 243,06 contra los AUDIT capa=NDX de la clasica con esa base (log, hora ART). Criterio sin tolerancias libres: la
//      hora del AUDIT es la de su renglon (TRUNCADA al segundo) y la cuenta de la clasica es de un instante de ese segundo; el AUDIT tiene que caer dentro de
//      lo que da DominantesClasica en ese segundo (-0,1 a +1,0 s), con los mismos strikes y su redondeo (2 decimales en la posicion, M enteros en el monto).
//      B1 FORMULA: con la cadena de la clasica (sello + generado: cboe-local o su archivo local), el fut del AUDIT y la base 243,06; y, a la hora del renglon,
//         lo mismo que el port (audit_dominantes_lineas.csv) a 0,006 pt / 0,51 M.
//      B2 TODO 4.1, DONDE LA CADENA COINCIDE: con la cadena de la 4.1 del MISMO sello (otra bajada: otro 'generado' y sus dias redondeados a 4 decimales desde
//         ahi, lo que corre su reloj unos segundos: ErrDias0 lo calcula y el segundo del renglon se corre eso): C# = Python a 1e-6 (todas), y donde la 4.1
//         YA TENIA esa cadena a la hora del AUDIT (generado <= t, la regla de ClasicaNdx; con una bajada posterior Envejecer da 0 y T queda corrido los
//         minutos de diferencia: no es comparable y se cuenta aparte): los mismos strikes que con la cadena de la clasica y el AUDIT dentro de su segundo.
//      B3 (informativo): la regla del minuto de ClasicaNdx (la ultima cadena con generado <= t) a la hora de cada AUDIT: cuantas caen en el mismo sello.
//   C. CASOS BORDE (cadenas sinteticas de ref_dominantes.py): sin volumen en el radio -> OI, un solo strike con volumen, un solo lado (arriba / abajo),
//      empate del 20 %, el strike justo en el precio, el tope del radio, dos strikes, el centroide de +-12 pts, volumen que se cancela, horizonte Hoy y
//      la cadena envejecida: C# contra la expectativa escrita a mano y contra el port.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PythiaGexCuatro.Cboe;
using PythiaGexCuatro.Familia;

namespace PruebaClasicaDom
{
    /// <summary>Las cadenas _NDX de la 4.1 de una carpeta como las ve ClasicaNdx: por sello la de generado mas temprano (BajadorCboe), en orden de generado;
    /// la del minuto t = la ULTIMA con generado (truncado al segundo) &lt;= t (ClasicaNdx.Refrescar + Zero).</summary>
    sealed class FuenteNdx
    {
        public readonly FotoCadena[] Fotos;
        public readonly List<long> GenSeg = new List<long>();
        public readonly Dictionary<DateTime, FotoCadena> PorSello = new Dictionary<DateTime, FotoCadena>();
        public int Lineas, Archivos;
        static DateTime Seg(DateTime d) => new DateTime(d.Ticks - d.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        public FuenteNdx(string carpeta)
        {
            var todas = new List<FotoCadena>();
            foreach (var p in Directory.GetFiles(carpeta, "cadena-NQ-*.jsonl.gz").OrderBy(x => x, StringComparer.Ordinal))
            {
                Archivos++;
                foreach (var L in CboeArchivo.LeerDia(p, null, out _)) { todas.Add(L.AFoto("NDX")); Lineas++; }
            }
            foreach (var f in todas.OrderBy(f => f.GeneradoUtc)) if (!PorSello.ContainsKey(f.TsUtc)) PorSello[f.TsUtc] = f;
            Fotos = PorSello.Values.OrderBy(f => f.GeneradoUtc).ToArray();
            foreach (var f in Fotos) GenSeg.Add(TiempoFam.Ms(Seg(f.GeneradoUtc)));
        }
        public FotoCadena DelMinuto(DateTime t) { int k = NumFam.UltimoMenorIgual(GenSeg, TiempoFam.Ms(t)); return k < 0 ? null : Fotos[k]; }
    }

    static class Program
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static StreamWriter _out;
        static int _ok, _mal;
        static readonly List<string> _fallas = new List<string>();
        static void P(string s = "") { Console.WriteLine(s); _out?.WriteLine(s); _out?.Flush(); }
        static void Ok(bool c, string que, string det = "")
        {
            if (c) { _ok++; P("  OK   " + que); }
            else { _mal++; _fallas.Add(que + (det == "" ? "" : " -> " + det)); P("  MAL  " + que + (det == "" ? "" : " -> " + det)); }
        }
        static string F(double v, string f = "0.00") => double.IsNaN(v) ? "NaN" : v.ToString(f, Inv);
        static readonly DateTime EPOCA = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        const string DIA_AUDIT = "2026-10-09";          // la rueda simulada: los AUDIT capa=NDX de este dia (hora ART) con base 243.06
        const double BASE_RUEDA = 243.06;

        static int Main(string[] args)
        {
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            Console.OutputEncoding = Encoding.UTF8;
            var d0 = new DirectoryInfo(AppContext.BaseDirectory);
            while (d0 != null && !File.Exists(Path.Combine(d0.FullName, "clasica_dom.csproj"))) d0 = d0.Parent;
            string aqui = d0?.FullName ?? Directory.GetCurrentDirectory();
            string raiz = Path.GetFullPath(Path.Combine(aqui, "..", "..", ".."));                 // ...\PythiaGex
            string app = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS");
            string res = Path.Combine(aqui, "resultados"); Directory.CreateDirectory(res);
            string tmp = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "pg4_clasica_dom");
            var sw = Stopwatch.StartNew();
            using (_out = new StreamWriter(Path.Combine(res, "clasica_dom.txt"), false, new UTF8Encoding(false)))
            {
                P("=== arnes clasica_dom (PythiaGex 4.1.5b) " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv) + " — " + ClasicaNdx.VERSION + " | serie " + ClasicaNdx.SERIE_DOM);
                try { Correr(aqui, raiz, app, tmp); }
                catch (Exception e) { Ok(false, "excepcion", e.ToString()); }
                P();
                P("=== " + _ok + " OK, " + _mal + " MAL en " + sw.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s");
                foreach (var f in _fallas) P("  MAL " + f);
            }
            return _mal == 0 ? 0 : 1;
        }

        static long Copiar(string o, string d)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(d));
            for (int i = 0; ; i++)
            {
                try { using var a = new FileStream(o, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); using var b = File.Create(d); a.CopyTo(b); return b.Length; }
                catch (IOException) when (i < 5) { System.Threading.Thread.Sleep(300); }
            }
        }

        static IEnumerable<string> Lineas(string ruta)
        {
            using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            string l;
            while ((l = sr.ReadLine()) != null) yield return l;
        }

        static DateTime U(string iso) => DateTime.SpecifyKind(DateTime.ParseExact(iso, "yyyy-MM-dd'T'HH:mm:ss'Z'", Inv), DateTimeKind.Utc);
        static string Iso(DateTime t) => t.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv);
        static string Sello(DateTime t) => t.ToString("yyyy-MM-dd HH:mm:ss", Inv);
        static string Sha(string ruta) { using var s = SHA256.Create(); return Convert.ToHexString(s.ComputeHash(File.ReadAllBytes(ruta))).ToLowerInvariant(); }

        /// <summary>Lo que se compara de una dominante: precio (posicion), monto en USD, lado (o rol) y strike.</summary>
        sealed class Dom { public double Pos, Gex, K; public string Lado = ""; }
        static List<Dom> DeCs(List<DomClasica> l) => l.Select(d => new Dom { Pos = d.Pos, Gex = d.Gex, K = d.K, Lado = d.Lado }).ToList();
        static List<Dom> DePy(JsonElement e) => e.ValueKind != JsonValueKind.Array ? null
            : e.EnumerateArray().Select(a => new Dom { Pos = a[0].GetDouble(), Gex = a[1].GetDouble(), Lado = a[2].GetString(), K = a[3].GetDouble() }).ToList();
        static string Txt(IEnumerable<Dom> l) => l == null ? "-" : string.Join(" ", l.Select(d => F(d.Pos) + "=" + F(d.Gex / 1e6, "0.0") + "M(K" + F(d.K, "0.##") + "," + d.Lado + ")"));

        /// <summary>C# contra Python con la MISMA entrada: mismos strikes y lados; posicion y monto a 1e-6 (relativo en el monto).</summary>
        static bool IgualesCsPy(List<Dom> a, List<Dom> b, out double dp, out double dg)
        {
            dp = 0; dg = 0;
            if (a == null || b == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                dp = Math.Max(dp, Math.Abs(a[i].Pos - b[i].Pos)); dg = Math.Max(dg, Math.Abs(a[i].Gex - b[i].Gex) / Math.Max(1.0, Math.Abs(b[i].Gex)));
                if (Math.Abs(a[i].K - b[i].K) > 1e-9 || a[i].Lado != b[i].Lado) return false;
            }
            return dp <= 1e-6 && dg <= 1e-9;
        }

        // ==============================================================================================================================
        static void Correr(string aqui, string raiz, string app, string tmp)
        {
            P("== 0. Datos (copias de solo lectura en " + tmp + ")");
            try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch (Exception e) { P("  no pude borrar " + tmp + ": " + e.Message); }
            Directory.CreateDirectory(tmp);
            // (1) los niv con R10_NDX_dom, PRIMERO
            var dias = new SortedSet<DateTime>(); int nivLineas = 0; long bytes = 0;
            foreach (var f in Directory.GetFiles(Path.Combine(app, "PythiaGex4", "familia"), "niv-*-MNQZ6.jsonl").OrderBy(x => x, StringComparer.Ordinal))
            {
                if (!Lineas(f).Any(l => l.Contains("\"R10_NDX_dom\""))) continue;
                var dst = Path.Combine(tmp, "niv", Path.GetFileName(f)); bytes += Copiar(f, dst);
                foreach (var l in File.ReadLines(dst))
                {
                    if (!l.Contains("\"R10_NDX_dom\"")) continue;
                    nivLineas++;
                    int i = l.IndexOf("\"k\":", StringComparison.Ordinal), j = i < 0 ? -1 : l.IndexOf(',', i);
                    if (j > i && long.TryParse(l.Substring(i + 4, j - i - 4), NumberStyles.Integer, Inv, out long k)) dias.Add(EPOCA.AddMinutes(k).Date);
                }
                P("  " + Path.GetFileName(f) + ": copiado");
            }
            // (2) las cadenas de la 4.1: de la vispera del primer minuto al dia siguiente del ultimo, y las del dia de los AUDIT
            var diaAudit = DateTime.SpecifyKind(DateTime.ParseExact(DIA_AUDIT, "yyyy-MM-dd", Inv), DateTimeKind.Utc);
            var cad = new SortedSet<DateTime> { diaAudit.AddDays(-1), diaAudit, diaAudit.AddDays(1) };
            foreach (var d in dias) { cad.Add(d.AddDays(-1)); cad.Add(d); cad.Add(d.AddDays(1)); }
            int nc = 0;
            foreach (var d in cad)
            {
                var f = Path.Combine(app, "PythiaGex4", "cboe", "cadena-NQ-" + d.ToString("yyyy-MM-dd", Inv) + ".jsonl.gz");
                if (File.Exists(f)) { bytes += Copiar(f, Path.Combine(tmp, "cboe4", Path.GetFileName(f))); nc++; }
            }
            // (3) los AUDIT capa=NDX del dia del log de la clasica (hora ART)
            var audits = new List<string>();
            foreach (var l in Lineas(Path.Combine(app, "pythiagex-gammahoy.log")))
                if (l.StartsWith(DIA_AUDIT, StringComparison.Ordinal) && l.IndexOf("  AUDIT capa=NDX ", StringComparison.Ordinal) > 0) audits.Add(l);
            File.WriteAllLines(Path.Combine(tmp, "audit_ndx.txt"), audits, new UTF8Encoding(false));
            P("  niv con R10_NDX_dom: " + nivLineas + " minutos (dias UTC " + string.Join(", ", dias.Select(d => d.ToString("MM-dd", Inv))) + "); cadenas de la 4.1: " + nc + " archivos; "
              + "AUDIT capa=NDX del " + DIA_AUDIT + " (ART): " + audits.Count + "; " + (bytes >> 20) + " MB copiados");

            // (4) las cadenas de la CLASICA de los AUDIT con base 243.06 (B1): cboe-local y, las que falten, su archivo local
            var claves = new HashSet<(DateTime, string)>();
            foreach (var l in audits)
            {
                if (!l.Contains(" base=243.06 ")) continue;
                var ts = Valor(l, "cadenaTs"); var gen = Valor(l, "gen");
                if (DateTime.TryParseExact(ts.Replace('_', ' '), "yyyy-MM-dd HH:mm:ss", Inv, DateTimeStyles.None, out var t0)) claves.Add((DateTime.SpecifyKind(t0, DateTimeKind.Utc), gen));
            }
            var c2 = new Dictionary<(DateTime, string), FotoCadena>();
            foreach (var d in new[] { diaAudit.AddDays(-1), diaAudit })
            {
                var f = Path.Combine(app, "PythiaGex", "cboe-local", "cadena-NQ-" + d.ToString("yyyy-MM-dd", Inv) + ".jsonl.gz");
                if (!File.Exists(f)) continue;
                var dst = Path.Combine(tmp, "cboe2", Path.GetFileName(f)); Copiar(f, dst);
                foreach (var L in CboeArchivo.LeerDia(dst, null, out _)) { var fo = L.AFoto("NDX"); var k = (fo.TsUtc, fo.GeneradoUtc.ToString("HH:mm:ss", Inv)); if (claves.Contains(k) && !c2.ContainsKey(k)) c2[k] = fo; }
            }
            int deLocal = c2.Count;
            var faltan = new HashSet<(DateTime, string)>(claves.Where(k => !c2.ContainsKey(k)));
            foreach (var orig in new[] { diaAudit, diaAudit.AddDays(-1) }.Select(d => Path.Combine(app, "PythiaGex", "cadenas", "local-NQ-" + d.ToString("yyyy-MM-dd", Inv) + ".jsonl")))
            {
                if (!File.Exists(orig) || faltan.Count == 0) continue;
                var dst = Path.Combine(tmp, "cboe2", "clasica-" + Path.GetFileName(orig) + ".gz");
                using (var o = new GZipStream(File.Create(dst), CompressionLevel.Fastest))
                using (var w = new StreamWriter(o, new UTF8Encoding(false)))
                    foreach (var l in Lineas(orig))
                    {
                        int i = l.IndexOf("\"cadena_ts\":\"", StringComparison.Ordinal), j = l.IndexOf("\"generado\":\"", StringComparison.Ordinal);
                        if (i < 0 || j < 0 || l.Length < i + 32 || l.Length < j + 31) continue;
                        if (!DateTime.TryParseExact(l.Substring(i + 13, 19), "yyyy-MM-dd HH:mm:ss", Inv, DateTimeStyles.None, out var ts)) continue;
                        if (!faltan.Contains((DateTime.SpecifyKind(ts, DateTimeKind.Utc), l.Substring(j + 12 + 11, 8)))) continue;
                        w.Write(l); w.Write('\n');
                    }
                foreach (var L in CboeArchivo.LeerDia(dst, null, out _)) { var fo = L.AFoto("NDX"); var k = (fo.TsUtc, fo.GeneradoUtc.ToString("HH:mm:ss", Inv)); if (faltan.Remove(k)) c2[k] = fo; }
            }
            P("  cadenas de la clasica para los AUDIT con base 243.06: " + c2.Count + " de " + claves.Count + " (sello + generado): " + deLocal + " de cboe-local, " + (c2.Count - deLocal) + " de su archivo local; sin encontrar " + faltan.Count);

            // (5) la referencia Python
            string script = Path.Combine(aqui, "ref_dominantes.py"), salida = Path.Combine(tmp, "ref_python.json");
            string port = Path.GetFullPath(Path.Combine(raiz, "laboratorio", "calibracion_1009", "receta_clasica_dominantes", "dominantes", "extraer_dominantes_clasica.py"));
            P("  port Python: " + port + " (sha256 " + (File.Exists(port) ? Sha(port).Substring(0, 16) + "..." : "NO EXISTE") + ")");
            var sw = Stopwatch.StartNew();
            var psi = new ProcessStartInfo("python", "-I -B \"" + script + "\" \"" + tmp + "\" \"" + salida + "\"")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, CreateNoWindow = true };
            int code; string so, se;
            using (var pr = Process.Start(psi))
            {
                try { pr.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
                var tE = pr.StandardError.ReadToEndAsync();
                so = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); se = tE.Result; code = pr.ExitCode;
            }
            foreach (var l in so.Split('\n')) if (l.Trim().Length > 0) P("    py| " + l.TrimEnd('\r'));
            foreach (var l in se.Split('\n')) if (l.Trim().Length > 0) P("    py(err)| " + l.TrimEnd('\r'));
            Ok(code == 0 && File.Exists(salida), "R0 ref_dominantes.py corrio con el port (exit " + code + ", " + sw.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s) y sus casos borde cumplen la expectativa escrita a mano");
            if (!File.Exists(salida)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(salida, Encoding.UTF8));
            var root = doc.RootElement;
            var f4 = new FuenteNdx(Path.Combine(tmp, "cboe4"));
            P("  cadenas de la 4.1 en C#: " + f4.Fotos.Length + " sellos de " + f4.Lineas + " lineas (" + f4.Archivos + " archivos)");

            Niv(root, f4);
            Rueda(root, f4, c2, raiz);
            Casos(root);
        }

        static string Valor(string linea, string clave)
        {
            int i = linea.IndexOf(" " + clave + "=", StringComparison.Ordinal);
            if (i < 0) return "";
            int j = linea.IndexOf(' ', i + clave.Length + 2);
            return j < 0 ? linea.Substring(i + clave.Length + 2) : linea.Substring(i + clave.Length + 2, j - i - clave.Length - 2);
        }

        // ==============================================================================================================================
        static void Niv(JsonElement root, FuenteNdx f4)
        {
            P(); P("== A. LO QUE GUARDO LA 4.1.5b (R10_NDX_dom en niv-*.jsonl) contra la cuenta en C# y el port Python, minuto por minuto");
            int n = 0, err = 0, mismaCad = 0, csG = 0, pyG = 0, csPy = 0, futOk = 0; double maxDp = 0, maxDg = 0;
            var ej = new List<string>(); var porBase = new SortedDictionary<string, int>(StringComparer.Ordinal);
            DateTime t0 = DateTime.MaxValue, t1 = DateTime.MinValue;
            foreach (var m in root.GetProperty("niv").EnumerateArray())
            {
                n++;
                if (m.TryGetProperty("error", out var er)) { err++; if (ej.Count < 6) ej.Add("k " + m.GetProperty("k").GetInt64() + ": " + er.GetString()); continue; }
                var t = EPOCA.AddMinutes(m.GetProperty("k").GetInt64());
                if (t < t0) t0 = t; if (t > t1) t1 = t;
                double b = m.GetProperty("base").GetDouble(), S = m.GetProperty("S").GetDouble(), futPy = m.GetProperty("fut").GetDouble();
                double fut = NumPy.RoundPy(S + b, 2);
                if (fut == futPy && fut - b == S) futOk++;
                string kb = F(b) + (ClasicaNdx.EnVentanaRueda(t, out _) ? " (en la ventana de la rueda)" : "");
                porBase[kb] = porBase.TryGetValue(kb, out var nb) ? nb + 1 : 1;
                var c = f4.DelMinuto(t);
                bool misma = c != null && Iso(c.GeneradoUtc) == m.GetProperty("gen").GetString() && Sello(c.TsUtc) == m.GetProperty("ts").GetString();
                if (misma) mismaCad++;
                var cs = c == null ? new List<DomClasica>() : DominantesClasica.Calcular(c, fut - b, fut, b, t, Nucleo20.TASA, out _);
                // lo guardado: [precio, rol, monto M, strike]
                var g = m.GetProperty("guardado").EnumerateArray().Select(a => (P: a[0].GetDouble(), Rol: a[1].GetString(), M: a[2].GetDouble(), K: a[3].GetDouble())).ToList();
                bool okCs = cs.Count == g.Count;
                for (int i = 0; okCs && i < g.Count; i++)
                    okCs = NumPy.RoundPy(cs[i].Pos, 2) == g[i].P && "D" + (i + 1) == g[i].Rol && NumPy.RoundPy(cs[i].Gex / 1e6, 1) == g[i].M && cs[i].K == g[i].K;
                if (okCs) csG++;
                var py = DePy(m.GetProperty("doms"));
                bool okPy = py != null && py.Count == g.Count;
                for (int i = 0; okPy && i < g.Count; i++)
                    okPy = Math.Abs(py[i].Pos - g[i].P) <= 0.005 + 1e-9 && Math.Abs(py[i].Gex / 1e6 - g[i].M) <= 0.05 + 1e-6 * Math.Abs(g[i].M) && Math.Abs(py[i].K - g[i].K) <= 1e-9;
                if (okPy) pyG++;
                if (IgualesCsPy(DeCs(cs), py, out var dp, out var dg)) csPy++;
                maxDp = Math.Max(maxDp, dp); maxDg = Math.Max(maxDg, dg);
                if ((!okCs || !okPy || !misma) && ej.Count < 8)
                    ej.Add(t.ToString("HH:mm", Inv) + "Z fut " + F(fut) + " base " + F(b) + " sello " + (c == null ? "-" : c.TsUtc.ToString("HH:mm:ss", Inv)) + "/" + m.GetProperty("ts").GetString()
                           + " | guardado " + string.Join(" ", g.Select(x => F(x.P) + "=" + F(x.M, "0.0") + "M(" + x.Rol + ",K" + F(x.K, "0.##") + ")")) + " | C# " + Txt(DeCs(cs)) + " | py " + Txt(py));
            }
            foreach (var e in ej) P("    " + e);
            P("  minutos: " + n + (n > 0 ? " (" + t0.ToString("MM-dd HH:mm", Inv) + "Z-" + t1.ToString("MM-dd HH:mm", Inv) + "Z = " + t0.AddHours(-3).ToString("HH:mm", Inv) + "-" + t1.AddHours(-3).ToString("HH:mm", Inv) + " ART)" : "")
              + "; base usada: " + string.Join(", ", porBase.Select(kv => kv.Key + " x" + kv.Value)));
            P("  diferencia maxima C# - Python con la misma entrada: posicion " + maxDp.ToString("0.0e0", Inv) + " pt, monto " + maxDg.ToString("0.0e0", Inv) + " relativo");
            Ok(n > 0 && err == 0 && futOk == n, "A0 " + n + " minutos con R10_NDX_dom; el fut de la cuenta = round(S + base, 2) y S = fut - base al bit en " + futOk + " (sin la meta o sin cadena: " + err + ")");
            Ok(n > 0 && mismaCad == n, "A1 la cadena del minuto con la regla de ClasicaNdx (dedup por sello, la ultima con generado al segundo <= t): la misma en C# y en Python en " + mismaCad + " de " + n);
            Ok(n > 0 && csG == n, "A2 DominantesClasica en C# con la cadena, el fut, la base y la hora del minuto da EXACTAMENTE lo que guardo la 4.1.5b (precio, rol D1-D3, monto 0,1 M, strike) en " + csG + " de " + n);
            Ok(n > 0 && pyG == n, "A3 el port Python da lo guardado en " + pyG + " de " + n + " (precio a 0,005 y monto a 0,05 M: lo guardado esta redondeado a 2 y a 1 decimal; strike exacto)");
            Ok(n > 0 && csPy == n, "A4 C# = Python con la misma entrada en " + csPy + " de " + n + " (mismos strikes y lados; posicion a 1e-6 pt, monto a 1e-9 relativo)");
        }

        // ==============================================================================================================================
        static void Rueda(JsonElement root, FuenteNdx f4, Dictionary<(DateTime, string), FotoCadena> c2, string raiz)
        {
            P(); P("== B. LA RUEDA DE HOY SIMULADA CON BASE 243,06 contra los AUDIT capa=NDX de la clasica con esa base (" + DIA_AUDIT + ", log en hora ART)");
            P("  criterio (sin tolerancias libres): la hora del AUDIT es la de su renglon del log, TRUNCADA al segundo; la cuenta de la clasica es de un instante");
            P("  de ese segundo. El AUDIT tiene que caer dentro de lo que da DominantesClasica en ese segundo (de -0,1 a +1,0 s) con su redondeo: posicion a");
            P("  0,011 pt y monto a 0,51 M (los escribe con 2 decimales y en M enteros), con los mismos strikes. Con la cadena de la 4.1 el segundo se corre");
            P("  lo que el redondeo a 4 decimales de sus 'dias' la separa de la de la clasica (ErrDias0: otra bajada del mismo sello, otro 'generado').");
            // lo que dio el port con la cadena de la clasica (receta, hasta las 16:20 ART): t_utc -> d1..d3 (2 decimales) y sus M (enteros)
            var csv = Path.Combine(raiz, "laboratorio", "calibracion_1009", "receta_clasica_dominantes", "dominantes", "audit_dominantes_lineas.csv");
            var porCsv = new Dictionary<string, List<(double P, double M)>>();
            if (File.Exists(csv))
            {
                var ls = File.ReadAllLines(csv, Encoding.UTF8); var cab = ls[0].Split(',');
                int iB = Array.IndexOf(cab, "bloque"), iT = Array.IndexOf(cab, "t_utc"), iPar = Array.IndexOf(cab, "paridad");
                int[] iD = { Array.IndexOf(cab, "r_d1"), Array.IndexOf(cab, "r_d2"), Array.IndexOf(cab, "r_d3") }, iM = { Array.IndexOf(cab, "r_d1_M"), Array.IndexOf(cab, "r_d2_M"), Array.IndexOf(cab, "r_d3_M") };
                foreach (var l in ls.Skip(1))
                {
                    var v = SplitCsv(l); if (v.Count <= Math.Max(iM[2], iPar) || v[iB] != "NDX") continue;
                    var lst = new List<(double, double)>();
                    for (int q = 0; q < 3; q++) if (v[iD[q]] != "") lst.Add((double.Parse(v[iD[q]], Inv), double.Parse(v[iM[q]], Inv)));
                    porCsv[v[iT]] = lst;      // t_utc HH:mm:ss (el dia es el de la receta)
                }
            }
            int n = 0, rueda = 0, b1 = 0, b1ok = 0, b1sin = 0, b1csv = 0, b1csvOk = 0, b1log = 0, b1R = 0, b1RBien = 0;
            int b2 = 0, b2csPy = 0, b2mismosK = 0, b2ok = 0, b2sin = 0, b2log = 0, b2R = 0, b2RBien = 0, b3mismo = 0, b2con = 0, b2despues = 0;
            double b2maxDtS = 0, desfMin = double.MaxValue, desfMax = double.MinValue, b2maxP = 0, b2maxMr = 0;
            var ejB1 = new List<string>(); var ejB2 = new List<string>();
            foreach (var a in root.GetProperty("audit").EnumerateArray())
            {
                n++;
                var t = U(a.GetProperty("t").GetString()); double fut = a.GetProperty("fut").GetDouble();
                bool enRueda = t.TimeOfDay >= new TimeSpan(13, 30, 0) && t.TimeOfDay < new TimeSpan(20, 0, 0);
                if (enRueda) rueda++;
                var da = a.GetProperty("doms_audit").EnumerateArray().Select(x => (P: x[0].GetDouble(), M: x[1].GetDouble())).ToList();
                var ts = DateTime.SpecifyKind(DateTime.ParseExact(a.GetProperty("ts").GetString(), "yyyy-MM-dd HH:mm:ss", Inv), DateTimeKind.Utc);
                string gen = a.GetProperty("gen_clasica").GetString();
                // B1: la cadena de la clasica (sello + generado)
                c2.TryGetValue((ts, gen), out var cc);
                if (cc != null)
                {
                    b1++; if (enRueda) b1R++;
                    var cs = DominantesClasica.Calcular(cc, fut - BASE_RUEDA, fut, BASE_RUEDA, t, Nucleo20.TASA, out var lib);
                    bool alLog = lib == a.GetProperty("libro_audit").GetString() && IgualRedondeo(cs, da);
                    if (alLog) b1log++;
                    if (lib == a.GetProperty("libro_audit").GetString() && (alLog || EnSuSegundo(cc, fut, t, 0, da, out _))) { b1ok++; if (enRueda) b1RBien++; }
                    else
                    {
                        EnSuSegundo(cc, fut, t, 0, da, out var det);
                        string hora = ExplicaHora(cc, fut, t, da, out var dt, out _, out _) ? "; iguales con la hora corrida " + dt.ToString("+0.00;-0.00", Inv) + " s" : "; ni corriendo la hora +-10 s";
                        if (ejB1.Count < 8) ejB1.Add(t.ToString("HH:mm:ss", Inv) + "Z AUDIT " + string.Join(" ", da.Select(x => F(x.P) + "=" + F(x.M, "0") + "M")) + " | C# " + Txt(DeCs(cs)) + " | " + det + hora);
                    }
                    if (porCsv.TryGetValue(t.ToString("HH:mm:ss", Inv), out var pc))
                    {   // el port calculo a la hora del renglon con la misma cadena: C# a esa hora
                        b1csv++;
                        bool okc = pc.Count == cs.Count;
                        for (int i = 0; okc && i < pc.Count; i++) okc = Math.Abs(NumPy.RoundPy(cs[i].Pos, 2) - pc[i].P) <= 0.006 && Math.Abs(cs[i].Gex / 1e6 - pc[i].M) <= 0.51;
                        if (okc) b1csvOk++;
                    }
                }
                else b1sin++;
                // B2: la cadena de la 4.1 con el MISMO sello (donde la cadena coincide)
                if (a.TryGetProperty("sin_cadena_41", out _) || !f4.PorSello.TryGetValue(ts, out var c4)) { b2sin++; }
                else
                {
                    // la formula, C# contra Python, con esa cadena y esa hora (aunque la 4.1 la haya bajado despues: es la misma cuenta con la misma entrada)
                    b2con++;
                    var cs = DominantesClasica.Calcular(c4, fut - BASE_RUEDA, fut, BASE_RUEDA, t, Nucleo20.TASA, out var lib);
                    var py = DePy(a.GetProperty("doms"));
                    string genPy = a.TryGetProperty("gen", out var gp) ? gp.GetString() : "", libPy = a.TryGetProperty("libro", out var lp) && lp.ValueKind == JsonValueKind.String ? lp.GetString() : "";
                    if (Iso(c4.GeneradoUtc) == genPy && IgualesCsPy(DeCs(cs), py, out _, out _) && lib == libPy) b2csPy++;
                    // contra el AUDIT, solo si la 4.1 YA TENIA esa cadena a esa hora (generado al segundo <= t, la regla de ClasicaNdx): con una cadena bajada
                    // despues, la cuenta la envejece desde un 'generado' futuro (Envejecer da 0) y T queda corrido los minutos de diferencia: no es comparable
                    bool yaLaTenia = new DateTime(c4.GeneradoUtc.Ticks - c4.GeneradoUtc.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc) <= t;
                    if (!yaLaTenia) b2despues++;
                    else
                    {
                    b2++; if (enRueda) b2R++;
                    for (int i = 0; i < Math.Min(cs.Count, da.Count); i++)
                    {   // (informativo) cuanto se aparta a la hora del renglon, sin corregir nada
                        double dM = Math.Abs(cs[i].Gex / 1e6 - da[i].M);
                        b2maxP = Math.Max(b2maxP, Math.Abs(cs[i].Pos - da[i].P)); if (dM > 1.01) b2maxMr = Math.Max(b2maxMr, dM / Math.Max(1, Math.Abs(da[i].M)));
                    }
                    if (IgualRedondeo(cs, da)) b2log++;
                    if (cc != null)
                    {
                        b2maxDtS = Math.Max(b2maxDtS, Math.Abs((c4.GeneradoUtc - cc.GeneradoUtc).TotalSeconds));
                        var cl = DominantesClasica.Calcular(cc, fut - BASE_RUEDA, fut, BASE_RUEDA, t, Nucleo20.TASA, out _);
                        if (cl.Count == cs.Count && cl.Zip(cs, (x, y) => x.K == y.K).All(z => z)) b2mismosK++;
                        double desf = ErrDias0(c4) - ErrDias0(cc);
                        desfMin = Math.Min(desfMin, desf); desfMax = Math.Max(desfMax, desf);
                        bool enSeg = EnSuSegundo(c4, fut, t, desf, da, out var det);
                        if (lib == a.GetProperty("libro_audit").GetString() && enSeg) { b2ok++; if (enRueda) b2RBien++; }
                        else if (ejB2.Count < 8)
                        {
                            string hora = ExplicaHora(c4, fut, t, da, out var dt, out _, out _) ? "; iguales con la hora corrida " + dt.ToString("+0.00;-0.00", Inv) + " s" : "; ni corriendo la hora +-10 s";
                            ejB2.Add(t.ToString("HH:mm:ss", Inv) + "Z sello " + ts.ToString("HH:mm:ss", Inv) + " gen 4.1 " + c4.GeneradoUtc.ToString("HH:mm:ss", Inv) + " / clasica " + gen + " (desfase "
                                     + desf.ToString("+0.00;-0.00", Inv) + " s) | AUDIT " + string.Join(" ", da.Select(x => F(x.P) + "=" + F(x.M, "0") + "M")) + " | C# " + Txt(DeCs(cs)) + " | " + det + hora);
                        }
                    }
                    }
                }
                // B3 (informativo): la cadena que eligiria la regla del minuto de ClasicaNdx a la hora del AUDIT
                var cm = f4.DelMinuto(t);
                if (cm != null && cm.TsUtc == ts) b3mismo++;
            }
            P("  AUDIT con base 243.06: " + n + " (en la rueda 13:30-20:00 UTC: " + rueda + ")");
            P("  B1 con la cadena de la clasica: " + b1 + " (sin su cadena: " + b1sin + "); iguales con su redondeo ya a la hora EXACTA del renglon: " + b1log + "; en la rueda: " + b1RBien + " de " + b1R);
            foreach (var e in ejB1) P("    " + e);
            Ok(n > 0 && b1 > 0 && b1ok == b1 && b1 >= n - 5, "B1 FORMULA: con la MISMA cadena (sello + generado), el fut del AUDIT y la base 243,06, el AUDIT cae dentro de lo que da DominantesClasica en el segundo de su renglon en "
               + b1ok + " de " + b1 + " (a la hora exacta del renglon " + b1log + "; los demas, a minutos del vencimiento del 0DTE, donde un segundo mueve la gamma ~0,1 %); sin su cadena " + b1sin);
            Ok(b1csv > 800 && b1csvOk == b1csv, "B1b y a la hora del renglon da lo mismo que el port Python con esa cadena (audit_dominantes_lineas.csv de la receta) en " + b1csvOk + " de " + b1csv + " (a 0,006 pt y 0,51 M: el csv redondea)");
            P("  B2 la cadena de la 4.1 con el MISMO sello: la hay en " + b2con + " AUDIT (no la hay en " + b2sin + "); la 4.1 ya la tenia a esa hora en " + b2 + " (la bajo despues del AUDIT en " + b2despues + ": no se comparan)");
            P("  B2 en esos " + b2 + ": la otra bajada difiere hasta " + F(b2maxDtS, "0") + " s en el generado y el redondeo de sus dias"
              + " la corre de " + (b2 == 0 ? "-" : desfMin.ToString("+0.00;-0.00", Inv) + " a " + desfMax.ToString("+0.00;-0.00", Inv)) + " s; sin corregir nada (a la hora del renglon) se aparta hasta " + F(b2maxP, "0.000") + " pt y "
              + (b2maxMr * 100).ToString("0.00", Inv) + " % del monto (iguales con su redondeo: " + b2log + "); en la rueda: " + b2RBien + " de " + b2R);
            foreach (var e in ejB2) P("    " + e);
            Ok(b2con > 100 && b2csPy == b2con, "B2a C# = Python (misma cadena de la 4.1, mismo generado, misma base, fut y hora) en " + b2csPy + " de " + b2con);
            Ok(b2 > 100 && b2mismosK == b2, "B2b con la cadena de la 4.1 del mismo sello (ya bajada a esa hora) se eligen los MISMOS strikes que con la de la clasica en " + b2mismosK + " de " + b2);
            Ok(b2 > 100 && b2ok == b2, "B2c contra el AUDIT de la clasica, donde la cadena coincide y la 4.1 ya la tenia: el AUDIT cae dentro de lo que da la cadena de la 4.1 en el segundo de su renglon (corrido por el redondeo de sus dias) en "
               + b2ok + " de " + b2 + "; en la rueda (13:30-20:00 UTC) " + b2RBien + " de " + b2R);
            P("  B3 (informativo) la cadena del minuto de ClasicaNdx (la ultima con generado <= t) a la hora del AUDIT tiene el sello del AUDIT en " + b3mismo + " de " + n + " (en esos, la cuenta es la de B2)");
        }

        /// <summary>Las D1-D3 iguales a las del AUDIT con su redondeo: misma cantidad, posicion a 0,011 pt y monto a 0,51 M.</summary>
        static bool IgualRedondeo(List<DomClasica> cs, List<(double P, double M)> da)
        {
            if (cs.Count != da.Count) return false;
            for (int i = 0; i < da.Count; i++) if (Math.Abs(cs[i].Pos - da[i].P) > 0.011 || Math.Abs(cs[i].Gex / 1e6 - da[i].M) > 0.51) return false;
            return true;
        }

        /// <summary>El AUDIT cae dentro de lo que da la cuenta en el segundo de su renglon: con la cadena c, a las horas t + desfase + d, d de -0,10 a +1,00 s
        /// (de a 0,05 s; la cuenta es suave en el tiempo), las mismas dominantes (cantidad y strikes) en todo el segundo, y cada posicion y cada monto del AUDIT
        /// entre el minimo y el maximo de ese segundo con su redondeo (0,011 pt; 0,51 M). Sin otra tolerancia.</summary>
        static bool EnSuSegundo(FotoCadena c, double fut, DateTime t, double desfaseS, List<(double P, double M)> da, out string det)
        {
            det = ""; int n = da.Count;
            var pMin = Enumerable.Repeat(double.MaxValue, n).ToArray(); var pMax = Enumerable.Repeat(double.MinValue, n).ToArray();
            var mMin = Enumerable.Repeat(double.MaxValue, n).ToArray(); var mMax = Enumerable.Repeat(double.MinValue, n).ToArray();
            double[] k0 = null;
            for (int i = -2; i <= 20; i++)
            {
                double d = desfaseS + i * 0.05;
                var cs = DominantesClasica.Calcular(c, fut - BASE_RUEDA, fut, BASE_RUEDA, t.AddTicks((long)Math.Round(d * TimeSpan.TicksPerSecond)), Nucleo20.TASA, out _);
                if (cs.Count != n) { det = cs.Count + " dominantes a " + d.ToString("+0.00;-0.00", Inv) + " s (el AUDIT " + n + ")"; return false; }
                var ks = cs.Select(x => x.K).ToArray();
                if (k0 == null) k0 = ks; else if (!ks.SequenceEqual(k0)) { det = "los strikes cambian adentro del segundo"; return false; }
                for (int q = 0; q < n; q++)
                {
                    pMin[q] = Math.Min(pMin[q], cs[q].Pos); pMax[q] = Math.Max(pMax[q], cs[q].Pos);
                    mMin[q] = Math.Min(mMin[q], cs[q].Gex / 1e6); mMax[q] = Math.Max(mMax[q], cs[q].Gex / 1e6);
                }
            }
            for (int q = 0; q < n; q++)
                if (da[q].P < pMin[q] - 0.011 || da[q].P > pMax[q] + 0.011 || da[q].M < mMin[q] - 0.51 || da[q].M > mMax[q] + 0.51)
                {
                    det = "D" + (q + 1) + " del AUDIT " + F(da[q].P) + "=" + F(da[q].M, "0") + "M fuera de lo que da su segundo: " + F(pMin[q]) + ".." + F(pMax[q]) + " / " + F(mMin[q], "0.0") + ".." + F(mMax[q], "0.0") + " M";
                    return false;
                }
            return true;
        }

        /// <summary>(Diagnostico de los que no caen en su segundo.) La hora del AUDIT es la del renglon del log, TRUNCADA al segundo, y la cuenta de la clasica es de un instante de ese segundo. A minutos
        /// del vencimiento del 0DTE (20:00 UTC) T es chico y un segundo mueve la gamma ~0,1 %: este diagnostico busca los corrimientos de la hora dt (de -10
        /// a +10 s, de a 0,05 s) con los que las TRES dominantes del AUDIT salen iguales (posicion a 0,011 pt y monto a 0,51 M: el AUDIT lo escribe redondeado
        /// al entero). Despues se exige que dt sea el que corresponde: dt = delta + (err4.1 - errClasica), con delta en [0, 1) s (el instante dentro del
        /// segundo del log) y err = lo que el redondeo a 4 decimales de los 'dias' del 0DTE corre a T en cada cadena (ErrDias0). Con la misma cadena, err4.1 =
        /// errClasica y dt = delta. Tres montos, un parametro libre y su valor predicho: si cae, la diferencia es la hora; si no, no.</summary>
        static bool ExplicaHora(FotoCadena c, double fut, DateTime t, List<(double P, double M)> da, out double dtS, out double dtMin, out double dtMax)
        {
            dtS = dtMin = dtMax = double.NaN;
            for (int i = -200; i <= 200; i++)
            {
                double d = i * 0.05;
                var cs = DominantesClasica.Calcular(c, fut - BASE_RUEDA, fut, BASE_RUEDA, t.AddTicks((long)Math.Round(d * TimeSpan.TicksPerSecond)), Nucleo20.TASA, out _);
                if (cs.Count != da.Count) continue;
                bool ok = true;
                for (int q = 0; ok && q < cs.Count; q++) ok = Math.Abs(cs[q].Pos - da[q].P) <= 0.011 && Math.Abs(cs[q].Gex / 1e6 - da[q].M) <= 0.51;
                if (!ok) continue;
                if (double.IsNaN(dtS) || Math.Abs(d) < Math.Abs(dtS)) dtS = d;
                dtMin = double.IsNaN(dtMin) ? d : Math.Min(dtMin, d); dtMax = double.IsNaN(dtMax) ? d : Math.Max(dtMax, d);
            }
            return !double.IsNaN(dtS);
        }

        /// <summary>Segundos que el redondeo a 4 decimales de los 'dias' del vencimiento mas cercano corre a T en esta cadena: dias - (vencimiento - generado),
        /// con el vencimiento = generado + dias redondeado al minuto (los vencimientos son en minuto exacto: 16:00:00 NY; el redondeo mueve a lo sumo 4,3 s).</summary>
        static double ErrDias0(FotoCadena c)
        {
            double d0 = double.MaxValue;
            foreach (var d in c.Dias) if (d >= 0 && d < d0) d0 = d;
            if (d0 == double.MaxValue) return 0;
            var aprox = c.GeneradoUtc.AddSeconds(d0 * 86400);
            var exp = new DateTime((aprox.Ticks + TimeSpan.TicksPerMinute / 2) / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc);
            return (d0 - (exp - c.GeneradoUtc).TotalDays) * 86400;
        }

        /// <summary>El intervalo [dtMin, dtMax] de ExplicaHora es compatible con dt = delta + desfase, delta en [0, 1) s (con la grilla de 0,05 s de margen).</summary>
        static bool DeltaEnElSegundo(double dtMin, double dtMax, double desfase) => dtMax - desfase >= -0.06 && dtMin - desfase <= 1.06;

        static List<string> SplitCsv(string s)
        {
            var r = new List<string>(); var sb = new StringBuilder(); bool q = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (q) { if (c == '"') { if (i + 1 < s.Length && s[i + 1] == '"') { sb.Append('"'); i++; } else q = false; } else sb.Append(c); }
                else if (c == '"') q = true;
                else if (c == ',') { r.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            r.Add(sb.ToString());
            return r;
        }

        // ==============================================================================================================================
        static void Casos(JsonElement root)
        {
            P(); P("== C. CASOS BORDE (cadenas sinteticas): C# contra la expectativa escrita a mano y contra el port");
            foreach (var x in root.GetProperty("casos").EnumerateArray())
            {
                string id = x.GetProperty("id").GetString(), desc = x.GetProperty("desc").GetString();
                var gen = U(x.GetProperty("generado").GetString()); var t = U(x.GetProperty("t").GetString());
                var dias = x.GetProperty("dias").EnumerateArray().Select(v => v.GetDouble()).ToArray();
                var filas = x.GetProperty("filas").EnumerateArray().Select(a => new FilaCadena(a[0].GetDouble(), a[1].GetInt32(), a[2].GetDouble(), a[3].GetDouble(), a[4].GetDouble(), a[5].GetDouble(), a[6].GetDouble(), a[7].GetDouble())).ToArray();
                var c = new FotoCadena { Libro = "NDX", GeneradoUtc = gen, TsUtc = gen, DatoUtc = gen, Dias = dias, Filas = filas };
                double fut = x.GetProperty("fut").GetDouble(), b = x.GetProperty("base").GetDouble(), S = x.GetProperty("S").GetDouble();
                var cs = DominantesClasica.Calcular(c, S, fut, b, t, Nucleo20.TASA, out var lib);
                var esp = x.GetProperty("esperado"); string libE = esp.GetProperty("libro").GetString();
                var dE = esp.GetProperty("doms").EnumerateArray().Select(a => (dK: a[0].GetDouble(), Lado: a[1].GetString())).ToList();
                bool okEsp = lib == libE && cs.Count == dE.Count && cs.Zip(dE, (d, e) => Math.Abs(d.K - S - e.dK) <= 1e-9 && d.Lado == e.Lado).All(z => z);
                var py = DePy(x.GetProperty("doms"));
                bool okPy = x.GetProperty("port_ok").GetBoolean() && IgualesCsPy(DeCs(cs), py, out var dp, out var dg) && lib == x.GetProperty("libro").GetString();
                string extra = "";
                if (id == "C7" && cs.Count > 0) { extra = "; D1 en " + F(cs[0].Pos - fut) + " (el +105, fuera del radio, la corre desde +100)"; okEsp &= cs[0].Pos > fut + 104 && cs[0].Pos < fut + 104.3; }
                if (id == "C9" && cs.Count > 0) { extra = "; D1 en " + F(cs[0].Pos - fut) + " (centroide de +30/+40/+50, sin el +55)"; okEsp &= Math.Abs(cs[0].Pos - fut - 680.0 / 18.0) < 0.05; }
                Ok(okEsp && okPy, id + " " + desc + ": libro " + lib + ", " + string.Join(" ", cs.Select(d => (d.K - S).ToString("+0.##;-0.##", Inv) + ":" + d.Lado)) + extra
                   + " (C# = port: " + (okPy ? "si" : "NO") + ")", "esperado " + libE + " " + string.Join(" ", dE.Select(e => e.dK.ToString("+0.##;-0.##", Inv) + ":" + e.Lado)) + " | port " + Txt(py) + " | C# " + Txt(DeCs(cs)));
            }
        }
    }
}
