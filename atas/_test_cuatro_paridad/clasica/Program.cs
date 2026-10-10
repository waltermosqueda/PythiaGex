// Program.cs — arnes 'clasica' de PythiaGex 4.1.5 (09-10-2026). Salida 0 = todo verde. Resultados en .\resultados\clasica-2026-10-09.txt.
// La replica de la estela 'NDX 0Γ' de la clasica (R10_NDX_zero "Clasica NDX 0Γ", _modulos/familia/clasica/ClasicaNdx.cs) contra lo que la clasica hizo
// de verdad el 09-10. Todo con datos REALES, solo lectura (copias en %TEMP%\pg4_clasica; el log de la clasica se lee compartido, sin copiar):
//   0. DATOS: cadenas _NDX de la 4.1 (PythiaGex4\cboe, del 02 al 09-10; las del 08-10 son las de cboe-local sembradas), cinta del MNQZ6 de la 4.1,
//      cadenas que uso la clasica (cboe-local y su archivo local, solo las de los AUDIT), los AUDIT capa=NDX de la receta (paridad_clasica_ndx_minuto.csv,
//      910 renglones, 03:00-19:02 UTC) y las lineas "base muestra" de su log (pythiagex-gammahoy.log, hora ART).
//   1. FORMULA: CruceClasica con la MISMA cadena (sello + generado), el mismo fut, la misma base y la misma hora que cada AUDIT: zero igual a 0,05 pt.
//   2. BASE DE LA RUEDA: (a) la regla (ReglaBaseClasica.Paso) con las 92 muestras que logueo la clasica el 08-10 da en cada una su mediana, sus buenas/N y
//      su MAD, y termina en 243,06 a las 20:12:06 UTC; (b) cada muestra contra lo que mide la 4.1 (spot de su cadena con ese sello y cierre de la vela de
//      2 min de su cinta); (c) la base que la replica calcula SOLA (cadenas y cinta de la 4.1) para la rueda del 08-10, congelada el 09-10 en la rueda;
//      (d) informativo: con velas de 1 min; (e) informativo: la base por dia contra base_rueda_por_dia.csv.
//   3. TODO 4.1, por AUDIT: la replica (su cadena, su base) con el fut y la hora del AUDIT: minutos iguales a 0,05 pt del zero y de la base; cada
//      distinto con su causa (la base, la cadena: otro sello u otra bajada, o las dos), aislada con las variantes A-base (la base del AUDIT con la cadena de
//      la 4.1) y A-cadena (la cadena de la clasica con la base de la 4.1).
//   4. LO QUE GUARDA LA 4.1.5 POR MINUTO (t = inicio del minuto, fut = el ultimo tick): contra los AUDIT y los puntitos de cada vela (informativo).
//   5. LAS DOS VELAS DEL OPERADOR (14:10 y 14:18 ART).
//   6. MOTOR: el compuesto (Replica20 + ClasicaNdx) contra la Replica20 sola: las 21 series, la meta y las R20 identicas; la R10 en cada minuto;
//      persistencia (relectura identica); un archivo de la 4.1.4 (con R20, sin R10) se completa en memoria igual al calculo en vivo sin tocar las R20
//      ni el archivo.
//   7. REHECHO: la cinta sin el 08-10 (cargando al arrancar ATAS): la base cae a la CRUDA; al llegar el 08-10, la regla se rehace, sube Generacion y el
//      motor rehace los minutos: quedan iguales a un arranque con la cinta completa.
//   8. ETIQUETA, DETALLE Y ROTULO (ArmadoPantalla) sobre la foto real: "Clasica NDX 0Γ 31.068,xx V"; la pestaña dice la base usada, de cuando es y la de ahora.
//   4.1.5b (09-10-2026): la serie NUEVA R10_NDX_dom (las D1-D3 de la capa NDX de la clasica) sale del mismo ClasicaNdx: el compuesto la agrega junto con el
//      0Γ (M1/C2 comparan sin las DOS; M2b en los mismos minutos; M2c lo guardado = ClasicaNdx.NivelesDom(Zero(t, fut)) al bit; P1/C1/R2 tambien la
//      comparan; E6/E7 su etiqueta con rol y monto y su detalle con la base). La formula contra la clasica y el port Python: arnes clasica_dom.
//   9. 4.1.5d (09-10-2026, revision de la 4.1.5b/c): las muestras de la base de la rueda GUARDADAS (G1 la primera corrida guarda y da las mismas bases;
//      G2 un arranque con la cinta sin el 08 ni el 09-10 reproduce las mismas muestras y bases desde el archivo; G2b sin el archivo no se puede);
//      G3 una vela incompleta queda provisional (entra, no se guarda) y se guarda al completarse; G3d el escalon 229,32/229,98 del 09-10 sale de las
//      velas de 2 min con un hueco de ticks (con los cierres del grafico, del log de la clasica, la regla da 229,32); G4-G6 con cadenas sinteticas: sin cruce del zero
//      las D1-D3 igual, Falta, y las dominantes por OI marcadas en el texto; G7 el formato del archivo. E8-E10: salvedad, sin cambio, strike.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PythiaGexCuatro.Cboe;
using PythiaGexCuatro.Familia;
using PythiaGexCuatro.Familia.Ndx;
using PythiaGexCuatro.Familia.Qqq;

namespace PruebaClasica
{
    /// <summary>IFuenteCboe sobre los archivos cadena-&lt;T&gt;-&lt;dia&gt;.jsonl.gz de UNA carpeta, con el lector de produccion (CboeArchivo.LeerDia): dedup por
    /// sello (gana el generado mas temprano) y orden por generado, como BajadorCboe.Fotos. (Copia de FuenteArchivo20 del arnes replica20.)</summary>
    sealed class FuenteArchivo : IFuenteCboe
    {
        private readonly Dictionary<string, FotoCadena[]> _porLibro = new Dictionary<string, FotoCadena[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, long[]> _gen = new Dictionary<string, long[]>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> Lineas = new Dictionary<string, int>();
        public FuenteArchivo(string carpeta, IEnumerable<string> dias)
        {
            foreach (var (libro, arch) in new[] { ("NDX", "NQ"), ("QQQ", "QQQ") })
            {
                var todas = new List<FotoCadena>(); int n = 0;
                foreach (var d in dias)
                {
                    var p = Path.Combine(carpeta, "cadena-" + arch + "-" + d + ".jsonl.gz");
                    if (!File.Exists(p)) continue;
                    foreach (var L in CboeArchivo.LeerDia(p, null, out _)) { todas.Add(L.AFoto(libro)); n++; }
                }
                var porTs = new Dictionary<DateTime, FotoCadena>();
                foreach (var f in todas.OrderBy(f => f.GeneradoUtc)) if (!porTs.ContainsKey(f.TsUtc)) porTs[f.TsUtc] = f;
                var arr = porTs.Values.OrderBy(f => f.GeneradoUtc).ToArray();
                _porLibro[libro] = arr; _gen[libro] = arr.Select(f => f.GeneradoUtc.Ticks).ToArray(); Lineas[libro] = n;
            }
        }
        public void Arrancar() { }
        public void Parar() { }
        public IReadOnlyList<FotoCadena> Fotos(string libro, DateTime desdeUtc, DateTime hastaUtc)
        {
            if (!_porLibro.TryGetValue(libro ?? "", out var a)) return Array.Empty<FotoCadena>();
            var g = _gen[libro];
            int i = Array.BinarySearch(g, desdeUtc.Ticks); if (i < 0) i = ~i; else while (i > 0 && g[i - 1] == desdeUtc.Ticks) i--;
            int j = Array.BinarySearch(g, hastaUtc.Ticks); if (j < 0) j = ~j; else while (j > 0 && g[j - 1] == hastaUtc.Ticks) j--;
            return j > i ? new ArraySegment<FotoCadena>(a, i, j - i) : (IReadOnlyList<FotoCadena>)Array.Empty<FotoCadena>();
        }
        public FotoCadena Ultima(string libro) => _porLibro.TryGetValue(libro ?? "", out var a) && a.Length > 0 ? a[a.Length - 1] : null;
        public long Version => 1;
        public string Estado => "archivo";
        public int Cuantas(string libro) => _porLibro.TryGetValue(libro, out var a) ? a.Length : 0;
        public FotoCadena PorSello(string libro, DateTime ts) { if (!_porLibro.TryGetValue(libro, out var a)) return null; foreach (var f in a) if (f.TsUtc == ts) return f; return null; }
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
        static double D(string s) => double.TryParse(s, NumberStyles.Float, Inv, out var v) ? v : double.NaN;

        const string SESION = "2026-10-09";
        const double TOL = 0.05;                                                                   // lo pedido: a 0,05 pt del zero y de la base
        static readonly string[] DIAS_NQ = { "2026-10-02", "2026-10-03", "2026-10-04", "2026-10-05", "2026-10-06", "2026-10-07", "2026-10-08", "2026-10-09" };
        static readonly string[] DIAS_QQQ = { "2026-10-07", "2026-10-08", "2026-10-09" };
        // el motor corre la sesion del 09-10 hasta aca (despues de las dos velas del operador, 17:10 y 17:18 UTC)
        static readonly DateTime AHORA = new DateTime(2026, 10, 9, 17, 31, 0, DateTimeKind.Utc);
        static readonly DateTime T_OPERADOR = new DateTime(2026, 10, 9, 17, 10, 0, DateTimeKind.Utc);

        static int Main(string[] args)
        {
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            Console.OutputEncoding = Encoding.UTF8;
            var d0 = new DirectoryInfo(AppContext.BaseDirectory);
            while (d0 != null && !File.Exists(Path.Combine(d0.FullName, "clasica.csproj"))) d0 = d0.Parent;
            string aqui = d0?.FullName ?? Directory.GetCurrentDirectory();
            string raiz = Path.GetFullPath(Path.Combine(aqui, "..", "..", ".."));                 // ...\PythiaGex
            string app = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS");
            string res = Path.Combine(aqui, "resultados"); Directory.CreateDirectory(res);
            string tmp = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "pg4_clasica");
            var sw = Stopwatch.StartNew();
            using (_out = new StreamWriter(Path.Combine(res, "clasica-" + SESION + ".txt"), false, new UTF8Encoding(false)))
            {
                P("=== arnes clasica (PythiaGex 4.1.5) " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv) + " — " + ClasicaNdx.VERSION + " | " + Replica20.VERSION + " | " + MotorFamilia.VERSION);
                try { Correr(raiz, app, tmp); }
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

        // ==============================================================================================================================
        sealed class Fila { public Dictionary<string, string> C = new Dictionary<string, string>(); public string this[string k] => C.TryGetValue(k, out var v) ? v ?? "" : ""; }

        static List<Fila> LeerCsv(string ruta)
        {
            var l = new List<Fila>();
            var lineas = File.ReadAllLines(ruta, Encoding.UTF8);
            var cab = Partir(lineas[0]);
            for (int i = 1; i < lineas.Length; i++)
            {
                if (lineas[i].Trim().Length == 0) continue;
                var v = Partir(lineas[i]); var f = new Fila();
                for (int j = 0; j < cab.Count; j++) f.C[cab[j]] = j < v.Count ? v[j] : "";
                l.Add(f);
            }
            return l;
        }

        static List<string> Partir(string s)
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

        static DateTime U(string s) => DateTime.SpecifyKind(DateTime.ParseExact(s, "yyyy-MM-dd HH:mm:ss", Inv), DateTimeKind.Utc);

        /// <summary>Un AUDIT capa=NDX de la clasica (renglon del csv de la receta).</summary>
        sealed class Audit
        {
            public DateTime T, Ts; public string Gen = "", Fuente = "", Instancia = ""; public double Fut, Base, S, Zero, ZeroIdx, Zest41, VelaL, VelaH, VelaC; public string Ztp41 = "";
        }

        static List<Audit> LeerAudits(string csv) => LeerCsv(csv).Select(f => new Audit
        {
            T = U(f["t_utc"]), Ts = U(f["cadenaTs"].Replace('_', ' ')), Gen = f["gen"], Fuente = f["fuente"], Instancia = f["instancia"], Fut = D(f["fut"]), Base = D(f["base"]),
            S = D(f["S"]), Zero = D(f["zeroVol"]), ZeroIdx = D(f["zeroVol_idx"]), Zest41 = D((f["v41_ZEST_NDX_vol"] ?? "").Replace("Z:", "")), Ztp41 = f["v41_ZTP_NDX_vol"],
            VelaL = D(f["vela_l"]), VelaH = D(f["vela_h"]), VelaC = D(f["vela_c"])
        }).ToList();

        /// <summary>Una linea "base muestra" del log de la clasica.</summary>
        sealed class MuestraLog { public DateTime Pared, Ts, Vela; public double Spot, Cierre, Muestra, Mediana, Mad; public int Buenas, N; }

        static readonly Regex ReMuestra = new Regex(@"^(\S+)\s+base muestra: cboe (\S+) spot (\S+) vela (\S+) cierre (\S+) => (\S+) \| mediana robusta (\S+) de (\d+)/(\d+) \(MAD (\S+)\)", RegexOptions.Compiled);
        static readonly Regex ReCargada = new Regex(@"^(\S+)\s+base de la rueda cargada: (\S+) medida (\S+ \S+) UTC", RegexOptions.Compiled);

        /// <summary>Las muestras de la clasica de los dias pedidos (hora de la PC = ART = UTC-3) y la ultima "base cargada" antes de la primera de cada dia.</summary>
        static (List<MuestraLog> M, Dictionary<string, (double Base, string Cuando)> Cargada) LeerLog(string ruta, string[] dias)
        {
            var m = new List<MuestraLog>(); var carg = new Dictionary<string, (double, string)>();
            (double, string) ultimaCarga = (double.NaN, "");
            using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            string l;
            while ((l = sr.ReadLine()) != null)
            {
                if (l.Length < 20) continue;
                bool hoy = false; foreach (var d in dias) if (l.StartsWith(d, StringComparison.Ordinal)) { hoy = true; break; }
                if (!hoy) continue;
                if (l.IndexOf("base de la rueda cargada", StringComparison.Ordinal) > 0)
                {
                    var c = ReCargada.Match(l); if (c.Success) ultimaCarga = (D(c.Groups[2].Value), c.Groups[3].Value);
                    continue;
                }
                if (l.IndexOf("base muestra", StringComparison.Ordinal) < 0) continue;
                var x = ReMuestra.Match(l); if (!x.Success) continue;
                var pared = DateTime.SpecifyKind(DateTime.ParseExact(x.Groups[1].Value, "yyyy-MM-dd'T'HH:mm:ss", Inv), DateTimeKind.Utc).AddHours(3);
                var dia = pared.Date;
                var ts = DateTime.SpecifyKind(dia + TimeSpan.ParseExact(x.Groups[2].Value, "hh\\:mm\\:ss", Inv), DateTimeKind.Utc);
                var vela = DateTime.SpecifyKind(dia + TimeSpan.ParseExact(x.Groups[4].Value, "hh\\:mm", Inv), DateTimeKind.Utc);
                string k = pared.ToString("yyyy-MM-dd", Inv);
                if (!carg.ContainsKey(k)) carg[k] = ultimaCarga;
                m.Add(new MuestraLog { Pared = pared, Ts = ts, Vela = vela, Spot = D(x.Groups[3].Value), Cierre = D(x.Groups[5].Value), Muestra = D(x.Groups[6].Value),
                                       Mediana = D(x.Groups[7].Value), Buenas = int.Parse(x.Groups[8].Value, Inv), N = int.Parse(x.Groups[9].Value, Inv), Mad = D(x.Groups[10].Value) });
            }
            return (m, carg);
        }

        // ==============================================================================================================================
        static void Correr(string raiz, string app, string tmp)
        {
            P("== 0. Datos (copias de solo lectura en " + tmp + ")");
            try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch (Exception e) { P("  no pude borrar " + tmp + ": " + e.Message); }
            long bytes = 0; int n = 0;
            foreach (var d in DIAS_NQ) { var f = Path.Combine(app, "PythiaGex4", "cboe", "cadena-NQ-" + d + ".jsonl.gz"); if (File.Exists(f)) { bytes += Copiar(f, Path.Combine(tmp, "cboe4", Path.GetFileName(f))); n++; } }
            foreach (var d in DIAS_QQQ) { var f = Path.Combine(app, "PythiaGex4", "cboe", "cadena-QQQ-" + d + ".jsonl.gz"); if (File.Exists(f)) { bytes += Copiar(f, Path.Combine(tmp, "cboe4", Path.GetFileName(f))); n++; } }
            foreach (var f in Directory.GetFiles(Path.Combine(app, "PythiaGex4", "cinta"), "seg-MNQZ6-2026-10-0?.bin").Where(x => DIAS_NQ.Any(d => x.EndsWith("-" + d + ".bin", StringComparison.Ordinal))))
            { bytes += Copiar(f, Path.Combine(tmp, "cinta", Path.GetFileName(f))); n++; }
            foreach (var f in Directory.GetFiles(Path.Combine(app, "PythiaGex4", "familia"), "muestras-*.jsonl"))
                foreach (var x in new[] { "A", "B", "B2", "C", "R", "S" }) { bytes += Copiar(f, Path.Combine(tmp, x, "familia", Path.GetFileName(f))); n++; }
            string dirRec = Path.Combine(raiz, "laboratorio", "calibracion_1009", "receta_clasica");
            string csv = Path.Combine(dirRec, "paridad_clasica_ndx_minuto.csv");
            var audits = LeerAudits(csv);
            P("  " + n + " archivos, " + (bytes >> 20) + " MB; AUDIT capa=NDX de la clasica: " + audits.Count + " (" + audits.First().T.ToString("HH:mm:ss", Inv) + "-" + audits.Last().T.ToString("HH:mm:ss", Inv)
              + " UTC; base " + string.Join("/", audits.Select(a => F(a.Base)).Distinct()) + ") de " + csv);
            // las cadenas que uso la clasica: cboe-local y, las que no esten ahi, su archivo local (solo los renglones de los AUDIT; gz para el lector de produccion)
            var claves = new HashSet<(DateTime, string)>(audits.Select(a => (a.Ts, a.Gen)));
            var c2 = new Dictionary<(DateTime, string), FotoCadena>();
            foreach (var d in new[] { "2026-10-08", "2026-10-09" })
            {
                var f = Path.Combine(app, "PythiaGex", "cboe-local", "cadena-NQ-" + d + ".jsonl.gz");
                if (!File.Exists(f)) continue;
                var dst = Path.Combine(tmp, "cboe2", Path.GetFileName(f)); bytes += Copiar(f, dst);
                foreach (var L in CboeArchivo.LeerDia(dst, null, out _)) { var fo = L.AFoto("NDX"); var k = (fo.TsUtc, fo.GeneradoUtc.ToString("HH:mm:ss", Inv)); if (claves.Contains(k) && !c2.ContainsKey(k)) c2[k] = fo; }
            }
            int deLocal = c2.Count;
            var faltan = new HashSet<(DateTime, string)>(claves.Where(k => !c2.ContainsKey(k)));
            if (faltan.Count > 0)
                foreach (var orig in new[] { Path.Combine(app, "PythiaGex", "cadenas", "local-NQ-2026-10-09.jsonl"), Path.Combine(app, "PythiaGex", "cadenas", "local-NQ-2026-10-08.jsonl") })
                {
                    if (!File.Exists(orig) || faltan.Count == 0) continue;
                    var dst = Path.Combine(tmp, "cboe2", "clasica-" + Path.GetFileName(orig) + ".gz");
                    int nl = 0;
                    using (var fs = new FileStream(orig, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16))
                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                    using (var o = new GZipStream(File.Create(dst), CompressionLevel.Fastest))
                    using (var w = new StreamWriter(o, new UTF8Encoding(false)))
                    {
                        string l;
                        while ((l = sr.ReadLine()) != null)
                        {
                            int i = l.IndexOf("\"cadena_ts\":\"", StringComparison.Ordinal), j = l.IndexOf("\"generado\":\"", StringComparison.Ordinal);
                            if (i < 0 || j < 0 || l.Length < i + 32 || l.Length < j + 31) continue;
                            if (!DateTime.TryParseExact(l.Substring(i + 13, 19), "yyyy-MM-dd HH:mm:ss", Inv, DateTimeStyles.None, out var ts)) continue;
                            var k = (DateTime.SpecifyKind(ts, DateTimeKind.Utc), l.Substring(j + 12 + 11, 8));
                            if (!faltan.Contains(k)) continue;
                            w.Write(l); w.Write('\n'); nl++;
                        }
                    }
                    foreach (var L in CboeArchivo.LeerDia(dst, null, out _)) { var fo = L.AFoto("NDX"); var k = (fo.TsUtc, fo.GeneradoUtc.ToString("HH:mm:ss", Inv)); if (faltan.Remove(k)) c2[k] = fo; }
                    P("  del archivo local de la clasica " + Path.GetFileName(orig) + ": " + nl + " renglones de los AUDIT que no estaban en cboe-local");
                }
            P("  cadenas de la clasica para los AUDIT: " + c2.Count + " de " + claves.Count + " (sello + generado): " + deLocal + " de cboe-local, " + (c2.Count - deLocal) + " de su archivo local; sin encontrar " + faltan.Count);

            var cinta = new CintaFamilia("MNQZ6", "NQ");
            var dias = Directory.GetFiles(Path.Combine(tmp, "cinta"), "*.bin").OrderBy(x => x, StringComparer.Ordinal).ToList();
            foreach (var f in dias) P("  cinta " + Path.GetFileName(f) + ": " + (cinta.CargarArchivoDia(f) ? "cargada" : "NO cargada"));
            P("  cinta: ultimo tick " + cinta.UltimoTickUtc.ToString("yyyy-MM-dd HH:mm:ss", Inv) + "Z; " + cinta.Estado);
            var f4 = new FuenteArchivo(Path.Combine(tmp, "cboe4"), DIAS_NQ);
            P("  cadenas 4.1 (PythiaGex4\\cboe): NDX " + f4.Cuantas("NDX") + " sellos de " + f4.Lineas["NDX"] + " lineas, QQQ " + f4.Cuantas("QQQ") + " de " + f4.Lineas["QQQ"]);
            var (mlog, cargada) = LeerLog(Path.Combine(app, "pythiagex-gammahoy.log"), new[] { "2026-10-08", "2026-10-09" });
            P("  log de la clasica: " + mlog.Count + " lineas 'base muestra' del 08 y 09-10 (" + string.Join(", ", mlog.GroupBy(x => x.Pared.ToString("MM-dd", Inv)).Select(g => g.Key + ": " + g.Count())) + ")");

            var rep = new ClasicaNdx(f4, cinta, new OpcionesClasicaNdx { Contrato = "MNQZ6", SondeoCadaS = 0 });

            Formula(audits, c2);
            Base(rep, f4, cinta, mlog, cargada, Path.Combine(dirRec, "base_rueda_por_dia.csv"));
            TodoCuatro(rep, audits, c2, f4);
            PorMinuto(rep, audits, cinta, LeerCsv(Path.Combine(dirRec, "velas_clasica_ndx_m1.csv")));
            Motor(tmp, cinta, f4, dias);
            Revision415d(tmp, cinta, f4, dias, rep, mlog);
        }

        // ==============================================================================================================================
        static void Formula(List<Audit> audits, Dictionary<(DateTime, string), FotoCadena> c2)
        {
            P(); P("== 1. FORMULA: CruceClasica con la MISMA cadena (sello + generado), el mismo fut, la misma base y la misma hora que cada AUDIT de la clasica");
            int ig = 0, dif = 0, sin = 0; double maxd = 0, maxi = 0; var ej = new List<string>();
            foreach (var a in audits)
            {
                if (!c2.TryGetValue((a.Ts, a.Gen), out var c)) { sin++; continue; }
                double S = a.Fut - a.Base;
                double z = CruceClasica.Cruce(c, S, a.T, out int filas);
                double zero = z + a.Base, d = Math.Abs(zero - a.Zero), di = Math.Abs(z - a.ZeroIdx);
                maxd = Math.Max(maxd, double.IsNaN(d) ? 99 : d); maxi = Math.Max(maxi, double.IsNaN(di) ? 99 : di);
                if (d <= TOL) ig++;
                else { dif++; if (ej.Count < 8) ej.Add(a.T.ToString("HH:mm:ss", Inv) + "Z sello " + a.Ts.ToString("HH:mm:ss", Inv) + " gen " + a.Gen + " fut " + F(a.Fut) + ": 4.1 " + F(zero) + " (" + filas + " filas) clasica " + F(a.Zero)); }
            }
            foreach (var e in ej) P("    " + e);
            P("  diferencia maxima: zero " + F(maxd, "0.0000") + " pt, en el indice " + F(maxi, "0.0000") + " pt");
            Ok(dif == 0 && sin == 0 && ig == audits.Count, "F1 zero de la clasica (grilla de 61, primer cruce de abajo hacia arriba) igual a 0,05 pt en " + ig + " de " + audits.Count + " AUDIT; distintos " + dif + ", sin cadena " + sin);
        }

        // ==============================================================================================================================
        static void Base(ClasicaNdx rep, FuenteArchivo f4, CintaFamilia cinta, List<MuestraLog> mlog, Dictionary<string, (double Base, string Cuando)> cargada, string csvDias)
        {
            P(); P("== 2. BASE DE LA RUEDA (MedirBaseRueda)");
            var m08 = mlog.Where(x => x.Pared.ToString("yyyy-MM-dd", Inv) == "2026-10-08").ToList();
            // 2a. la regla con las muestras de la clasica
            var (b0, cuando0) = cargada.TryGetValue("2026-10-08", out var cc) ? cc : (double.NaN, "");
            P("  2a. la regla con las " + m08.Count + " muestras que logueo la clasica el 08-10 (arranco a las " + (m08.Count > 0 ? m08[0].Pared.AddHours(-3).ToString("HH:mm:ss", Inv) : "-") + " ART con la base cargada "
              + F(b0) + " medida " + cuando0 + " UTC)");
            var obs = new List<double>(); double bR = b0; DateTime bU = default; int okM = 0; var ejM = new List<string>();
            foreach (var x in m08)
            {
                // la muestra como la calcula la clasica (cierre - spot, en double); el log la imprime con 2 decimales y con esa el tope de 3 MAD cambia
                // de lado en el borde (medido: 4 de 92 con la del log, 18:17:51 247,29 a 7,35 justo)
                ReglaBaseClasica.Paso(obs, x.Cierre - x.Spot, x.Cierre, x.Pared, ref bR, ref bU, out var med, out var mad, out var buenas, out _);
                bool igual = Math.Abs(med - x.Mediana) <= 0.005 + 1e-9 && buenas == x.Buenas && obs.Count == x.N && mad.ToString("0.0", Inv) == x.Mad.ToString("0.0", Inv)
                             && Math.Abs(x.Cierre - x.Spot - x.Muestra) <= 0.005 + 1e-9;
                if (igual) okM++; else if (ejM.Count < 5) ejM.Add(x.Ts.ToString("HH:mm:ss", Inv) + ": regla " + F(med) + " " + buenas + "/" + obs.Count + " MAD " + F(mad, "0.0") + " | log " + F(x.Mediana) + " " + x.Buenas + "/" + x.N + " MAD " + F(x.Mad, "0.0"));
            }
            foreach (var e in ejM) P("    " + e);
            Ok(okM == m08.Count && m08.Count == 92, "B1 la regla da la mediana, las buenas/N y la MAD que logueo la clasica en " + okM + " de " + m08.Count + " muestras del 08-10");
            Ok(Math.Abs(bR - 243.06) <= 0.005 && bU == new DateTime(2026, 10, 8, 20, 12, 6, DateTimeKind.Utc),
               "B2 y termina en la base que la clasica dibujo todo el 09-10: " + F(bR) + " adoptada " + bU.ToString("MM-dd HH:mm:ss", Inv) + " UTC (base-rueda-NQ.json: 243.06, 2026-10-08T20:12:06)");

            // 2b. cada muestra contra lo que mide la 4.1
            var mr = rep.Muestras;   // dispara la cuenta (la primera llamada refresca)
            rep.BaseRueda(new DateTime(2026, 10, 9, 17, 10, 0, DateTimeKind.Utc));
            mr = rep.Muestras;
            var porTs = mr.GroupBy(x => x.TsUtc).ToDictionary(g => g.Key, g => g.First());
            int spotOk = 0, velaOk = 0, sinCadena = 0, sinVela = 0; var ejV = new List<string>(); var huecos = new List<DateTime>();
            foreach (var x in m08)
            {
                var fo = f4.PorSello("NDX", x.Ts);
                if (fo == null) { sinCadena++; continue; }
                if (Math.Abs(fo.Spot - x.Spot) <= 0.005) spotOk++;
                double c = rep.CierreVela(x.Ts.AddSeconds(-960));
                if (!(c > 0)) { sinVela++; huecos.Add(x.Vela); continue; }
                if (Math.Abs(c - x.Cierre) <= 1e-9) velaOk++; else if (ejV.Count < 5) ejV.Add(x.Ts.ToString("HH:mm:ss", Inv) + " vela " + x.Vela.ToString("HH:mm", Inv) + ": 4.1 " + F(c) + " clasica " + F(x.Cierre));
            }
            foreach (var e in ejV) P("    " + e);
            Ok(spotOk == m08.Count && sinCadena == 0, "B3 el spot de la cadena de la 4.1 con el mismo sello es el de la clasica en " + spotOk + " de " + m08.Count + " (sin cadena: " + sinCadena + ")");
            if (huecos.Count > 0) P("    sin vela en la cinta de la 4.1 (velas de " + huecos.Min().ToString("HH:mm", Inv) + " a " + huecos.Max().ToString("HH:mm", Inv) + " UTC): " + string.Join(" ", huecos.Distinct().Select(t => t.ToString("HH:mm", Inv))));
            // el hueco: Rithmic se desconecto a las 14:49 ART (17:49 UTC) y la clasica reinicio a las 15:14 ART con el historial de ATAS; la cinta de la 4.1
            // (importada del CSV de la cinta de la 3.0, que tambien perdio a Rithmic) no tiene esas velas. Ninguna de esas muestras esta en las ultimas 24.
            bool huecoOk = huecos.All(t => t >= new DateTime(2026, 10, 8, 17, 48, 0, DateTimeKind.Utc) && t <= new DateTime(2026, 10, 8, 18, 30, 0, DateTimeKind.Utc));
            Ok(velaOk == m08.Count - sinVela - sinCadena && huecoOk && m08.Skip(m08.Count - 24).All(x => !huecos.Contains(x.Vela)),
               "B4 el cierre de la vela de 2 min de la cinta de la 4.1 es el del grafico de la clasica en " + velaOk + " de " + (m08.Count - sinVela) + " con vela; " + sinVela
               + " sin vela, todas en el hueco de la cinta del 08-10 (Rithmic caido 17:49-18:1x UTC) y fuera de las ultimas 24");

            // 2c. la base que calcula la replica SOLA, congelada el 09-10 en la rueda
            var eb = rep.BaseRueda(T_OPERADOR);
            var r08 = mr.Where(x => x.Dia == new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc)).ToList();
            P("  2c. la replica sola (cadenas y cinta de la 4.1): " + r08.Count + " muestras el 08-10 (la clasica " + m08.Count + ", desde las 18:12Z: se reinicio a las 15:14 ART); "
              + rep.SinVela + " sin vela en la cinta");
            P("      el 09-10 a las 17:10Z (congelada: " + eb.Congelada + "): base " + F(eb.Base) + " de la rueda " + eb.BaseDia.ToString("yyyy-MM-dd", Inv) + ", adoptada " + eb.BaseUtc.ToString("MM-dd HH:mm:ss", Inv)
              + "Z con " + eb.Buenas + "/" + eb.N + " buenas (" + eb.MuestrasDia + " muestras del dia); edad " + F(eb.EdadMin(T_OPERADOR), "0") + " min");
            var u24r = r08.Skip(Math.Max(0, r08.Count - 24)).Select(x => x.TsUtc).ToList();
            var u24c = m08.Skip(Math.Max(0, m08.Count - 24)).Select(x => x.Ts).ToList();
            var soloR = u24r.Except(u24c).ToList(); var soloC = u24c.Except(u24r).ToList();
            P("      ultimas 24 muestras: replica " + u24r.First().ToString("HH:mm:ss", Inv) + "-" + u24r.Last().ToString("HH:mm:ss", Inv) + "Z, clasica " + u24c.First().ToString("HH:mm:ss", Inv) + "-" + u24c.Last().ToString("HH:mm:ss", Inv)
              + "Z; solo en la replica: " + (soloR.Count == 0 ? "-" : string.Join(" ", soloR.Select(t => t.ToString("HH:mm:ss", Inv)))) + "; solo en la clasica: " + (soloC.Count == 0 ? "-" : string.Join(" ", soloC.Select(t => t.ToString("HH:mm:ss", Inv)))));
            bool baseIgual = Math.Abs(eb.Base - 243.06) <= TOL;
            P("      " + (baseIgual ? "IGUAL a 243,06 a 0,05 pt" : "DISTINTA de 243,06 por " + F(eb.Base - 243.06) + " pts: la explicacion es la de arriba (la clasica muestrea solo los sellos que le llegan en su bajada de cada 60 s; la 4.1 ve todos los de su cadena)"));
            Ok(eb.Hay && eb.Congelada && eb.BaseDia == new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc) && eb.EdadMin(T_OPERADOR) < 24 * 60 && Math.Abs(eb.Base - 243.06) <= 1.0,
               "B5 la replica usa el 09-10 en la rueda la base CONGELADA de la rueda del 08-10 (" + F(eb.Base) + "; la clasica 243,06; diferencia " + F(eb.Base - 243.06) + " pt, tope de esta prueba 1 pt) y vale (menos de 24 h)");
            // fuera de la ventana: la de las muestras hasta ese momento (despues de las 20:11Z del 09-10, la rueda de HOY)
            var eNoche = rep.BaseRueda(new DateTime(2026, 10, 9, 20, 45, 0, DateTimeKind.Utc));
            var e0800 = rep.BaseRueda(new DateTime(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc));
            P("      fuera de la ventana: 09-10 08:00Z base " + F(e0800.Base) + " (rueda " + e0800.BaseDia.ToString("MM-dd", Inv) + ", congelada " + e0800.Congelada + "); 09-10 20:45Z base " + F(eNoche.Base)
              + " (rueda " + eNoche.BaseDia.ToString("MM-dd", Inv) + ", " + eNoche.MuestrasDia + " muestras)");
            Ok(!e0800.Congelada && Math.Abs(e0800.Base - eb.Base) < 1e-9 && eNoche.BaseDia == new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc),
               "B6 de noche (08:00Z) la misma base que en la rueda (la del 08-10); desde las 20:11Z del 09-10 la de la rueda del 09-10 (" + F(eNoche.Base) + ")");

            // 2d. informativo: con la vela de 1 min (el grafico de la clasica los otros dias)
            var rep1 = new ClasicaNdx(f4, cinta, new OpcionesClasicaNdx { Contrato = "MNQZ6", SondeoCadaS = 0, VelaMin = 1 });
            var e1 = rep1.BaseRueda(T_OPERADOR);
            P("  2d. (informativo) con velas de 1 min: base de la rueda del " + e1.BaseDia.ToString("MM-dd", Inv) + " = " + F(e1.Base) + " (" + e1.Buenas + "/" + e1.N + "); con 2 min " + F(eb.Base)
              + ". La clasica midio el 08-10 en un grafico de 2 min (sus 92 velas son de minuto par); los otros dias, de 1 min.");

            // 2e. informativo: la base por dia contra la tabla de la receta
            P("  2e. (informativo) la base de cada rueda: replica (2 min / 1 min) | clasica escrita al archivo | simulada en la receta | 4.1 sincronizada (mediana del dia)");
            var tab = File.Exists(csvDias) ? LeerCsv(csvDias).ToDictionary(f => f["dia_utc"], f => f) : new Dictionary<string, Fila>();
            foreach (var d in new[] { "2026-10-05", "2026-10-06", "2026-10-07", "2026-10-08", "2026-10-09" })
            {
                var dia = DateTime.SpecifyKind(DateTime.ParseExact(d, "yyyy-MM-dd", Inv), DateTimeKind.Utc);
                var t = dia.AddHours(20).AddMinutes(45);
                var a2 = rep.BaseRueda(t); var a1 = rep1.BaseRueda(t);
                tab.TryGetValue(d, out var fl);
                P("      " + d + ": " + F(a2.BaseDia == dia ? a2.Base : double.NaN) + " / " + F(a1.BaseDia == dia ? a1.Base : double.NaN) + " | " + (fl?["clasica_escrita_al_archivo"] ?? "") + " | " + (fl?["simulada_algoritmo_clasica_fin_dia"] ?? "")
                  + " | " + (fl?["v41_mediana_dia"] ?? "") + "  (" + a2.MuestrasDia + " muestras)");
            }
        }

        // ==============================================================================================================================
        static void TodoCuatro(ClasicaNdx rep, List<Audit> audits, Dictionary<(DateTime, string), FotoCadena> c2, FuenteArchivo f4)
        {
            P(); P("== 3. TODO 4.1 por AUDIT: la replica (su cadena, su base congelada) con el fut y la hora de cada AUDIT de la clasica; tolerancia 0,05 pt");
            int ig = 0, n = 0, soloBase = 0, soloCad = 0, ambas = 0, otra = 0, mismoSello = 0, baseIg = 0, zeroIg = 0, conCadClasicaIg = 0;
            var difs = new List<double>(); var ej = new List<string>();
            foreach (var a in audits)
            {
                var r = rep.Zero(a.T, a.Fut);
                n++;
                if (r == null) { otra++; if (ej.Count < 12) ej.Add(a.T.ToString("HH:mm:ss", Inv) + "Z sin resultado"); continue; }
                bool bOk = Math.Abs(r.Base - a.Base) <= TOL, zOk = Math.Abs(r.Zero - a.Zero) <= TOL;
                if (bOk) baseIg++; if (zOk) zeroIg++;
                if (r.Foto.TsUtc == a.Ts) mismoSello++;
                difs.Add(r.Zero - a.Zero);
                if (bOk && zOk) { ig++; continue; }
                // aislar: A-base = la cadena de la 4.1 con la base del AUDIT; A-cadena = la cadena de la clasica con la base de la 4.1
                double zB = CruceClasica.Cruce(r.Foto, a.Fut - a.Base, a.T, out _) + a.Base;
                double zC = c2.TryGetValue((a.Ts, a.Gen), out var cc) ? CruceClasica.Cruce(cc, a.Fut - r.Base, a.T, out _) + r.Base : double.NaN;
                bool porCadena = !(Math.Abs(zB - a.Zero) <= TOL), porBase = !bOk;
                if (Math.Abs(zC - a.Zero) <= TOL) conCadClasicaIg++;
                string causa;
                if (porBase && !porCadena) { soloBase++; causa = "la base"; }
                else if (porCadena && !porBase) { soloCad++; causa = r.Foto.TsUtc == a.Ts ? "la cadena (mismo sello, otra bajada)" : "la cadena (otro sello)"; }
                else if (porBase && porCadena) { ambas++; causa = "la base y la cadena"; }
                else { otra++; causa = "otra"; }
                if (ej.Count < 12 || (causa != "la base" && ej.Count < 30))
                    ej.Add(a.T.ToString("HH:mm:ss", Inv) + "Z [" + causa + "] 4.1 " + F(r.Zero) + " (base " + F(r.Base) + ", sello " + r.Foto.TsUtc.ToString("HH:mm:ss", Inv) + ") vs clasica " + F(a.Zero) + " (base " + F(a.Base) + ", sello "
                           + a.Ts.ToString("HH:mm:ss", Inv) + ") | con la base del AUDIT " + F(zB) + " | con la cadena de la clasica " + F(zC));
            }
            difs.Sort();
            double Pct(double p) => difs.Count == 0 ? double.NaN : difs[Math.Min(difs.Count - 1, (int)Math.Floor(p * (difs.Count - 1)))];
            P("  AUDIT " + n + ": iguales (zero y base a 0,05) " + ig + "; base igual " + baseIg + "; zero igual " + zeroIg + "; mismo sello de CBOE " + mismoSello);
            P("  distintos por causa: la base " + soloBase + ", la cadena " + soloCad + ", las dos " + ambas + ", otra " + otra + "; con la cadena de la clasica y la base de la 4.1 quedan iguales "
              + conCadClasicaIg + " de " + (n - ig) + " (la base de la 4.1 es la de la clasica; lo que cambia es la cadena: la 4.1 baja cada 75 s y la clasica lee cboe-local)");
            P("  zero 4.1 - zero clasica: mediana " + F(Pct(0.5), "0.00") + " pt (p10 " + F(Pct(0.1)) + ", p90 " + F(Pct(0.9)) + ", min " + F(Pct(0)) + ", max " + F(Pct(1)) + ")");
            foreach (var e in ej) P("    " + e);
            Ok(otra == 0 && conCadClasicaIg == n - ig, "T1 cada AUDIT distinto tiene su causa medida (la base, la cadena o las dos): sin explicar " + otra + "; con la cadena de la clasica, todos iguales");
            Ok(baseIg == n, "T3 la base: la de la clasica (243,06, de la rueda del 08-10) en " + baseIg + " de " + n + " AUDIT, calculada por la 4.1 con sus cadenas y su cinta");
            Ok(n > 0 && Pct(0.1) > -1.5 && Pct(0.9) < 1.5, "T2 la replica cae sobre la estela de la clasica: zero 4.1 - clasica entre " + F(Pct(0.1)) + " y " + F(Pct(0.9)) + " pt (p10-p90; tope de esta prueba +-1,5)");
        }

        // ==============================================================================================================================
        static void PorMinuto(ClasicaNdx rep, List<Audit> audits, CintaFamilia cinta, List<Fila> velas)
        {
            P(); P("== 4. LO QUE GUARDA LA 4.1.5 POR MINUTO (t = inicio del minuto, fut = el ultimo tick del MNQ): contra los AUDIT y los puntitos de la vela (informativo)");
            var porMin = velas.ToDictionary(f => f["min_utc"], f => f);
            var dA = new List<double>(); var dP = new List<double>(); int dentro = 0, nv = 0;
            var porAudMin = audits.GroupBy(a => new DateTime(a.T.Ticks - a.T.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc)).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var kv in porAudMin.OrderBy(k => k.Key))
            {
                var t = kv.Key;
                double fut = rep.FutClasica(t, cinta.CierreConocido(t));
                var r = rep.Zero(t, fut);
                if (r == null) continue;
                foreach (var a in kv.Value) dA.Add(NumPy.RoundPy(r.Zero, 2) - a.Zero);
                if (porMin.TryGetValue(t.ToString("yyyy-MM-dd HH:mm", Inv), out var fv))
                {
                    double zmin = D(fv["zero_min"]), zmax = D(fv["zero_max"]);
                    if (!double.IsNaN(zmin)) { nv++; double z = NumPy.RoundPy(r.Zero, 2); if (z >= zmin - TOL && z <= zmax + TOL) dentro++; dP.Add(z < zmin ? z - zmin : z > zmax ? z - zmax : 0); }
                }
            }
            dA.Sort(); dP.Sort();
            double Q(List<double> l, double p) => l.Count == 0 ? double.NaN : l[Math.Min(l.Count - 1, (int)Math.Floor(p * (l.Count - 1)))];
            P("  contra los AUDIT del mismo minuto (" + dA.Count + "): mediana " + F(Q(dA, 0.5)) + " pt, p10 " + F(Q(dA, 0.1)) + ", p90 " + F(Q(dA, 0.9)) + "; |dif| <= 0,05 en " + dA.Count(x => Math.Abs(x) <= TOL));
            P("  contra los puntitos de la clasica en esa vela de 1 min (" + nv + " velas): adentro del rango de sus puntitos (+-0,05) en " + dentro + "; fuera: mediana " + F(Q(dP, 0.5)) + ", p10 " + F(Q(dP, 0.1)) + ", p90 " + F(Q(dP, 0.9)));
            P("  (la clasica recalcula cada 5 s y pone hasta 24 puntitos por vela; la 4.1 guarda UNA cuenta por minuto y la historia es por vela m2)");

            P(); P("== 5. LAS DOS VELAS DEL OPERADOR (14:10 y 14:18 ART = 17:10 y 17:18 UTC)");
            foreach (var t in new[] { new DateTime(2026, 10, 9, 17, 10, 0, DateTimeKind.Utc), new DateTime(2026, 10, 9, 17, 18, 0, DateTimeKind.Utc), new DateTime(2026, 10, 9, 17, 19, 0, DateTimeKind.Utc) })
            {
                double fut = rep.FutClasica(t, cinta.CierreConocido(t));
                var r = rep.Zero(t, fut);
                porMin.TryGetValue(t.ToString("yyyy-MM-dd HH:mm", Inv), out var fv);
                var a = audits.Where(x => x.T >= t && x.T < t.AddMinutes(1)).ToList();
                P("  " + t.ToString("HH:mm", Inv) + "Z (" + t.AddHours(-3).ToString("HH:mm", Inv) + " ART): 4.1.5 'Clasica NDX 0Γ' " + (r == null ? "-" : F(NumPy.RoundPy(r.Zero, 2)) + " = zero " + F(r.ZeroIdx) + " + base " + F(r.Base) + " (" + r.Origen + ")")
                  + " | clasica: puntitos " + (fv?["puntitos_dibujados"] ?? "-") + (a.Count > 0 ? ", AUDIT " + string.Join("/", a.Select(x => F(x.Zero))) : "")
                  + " | 4.1 ZEST_NDX " + (a.Count > 0 ? F(a[0].Zest41) : "-") + ", ZTP " + (a.Count > 0 ? a[0].Ztp41 : "-") + " | vela " + (fv == null ? "-" : "min " + fv["vela_l"] + " cierre " + fv["vela_c"]));
                if (r != null) P("      texto de la fuente: " + r.Texto);
            }
            var r17 = rep.Zero(new DateTime(2026, 10, 9, 17, 10, 0, DateTimeKind.Utc), rep.FutClasica(new DateTime(2026, 10, 9, 17, 10, 0, DateTimeKind.Utc), double.NaN));
            Ok(r17 != null && r17.Origen == "de la rueda" && r17.Zero > 31066.0 && r17.Zero < 31070.5,
               "V1 a las 17:10Z la replica dibuja la estela de la clasica (puntitos 31.067,33-31.068,32): " + (r17 == null ? "-" : F(r17.Zero)) + " con la base de la rueda del 08-10");
        }

        // ==============================================================================================================================
        sealed class Corrida { public MotorFamilia Motor; public Replica20 R20; public ClasicaNdx Cl; public double Seg; }

        static Corrida CorrerMotor(string carpeta, CintaFamilia cinta, IFuenteCboe fuente, bool clasica, DateTime ahora, ClasicaNdx clDada = null)
        {
            var sw = Stopwatch.StartNew();
            var nd = new LibroMinuteroNdx(fuente, cinta, new OpcionesNdx { ParidadPython = false, ContratoGrafico = "MNQZ6", CarpetaDatos = carpeta, Log = s => { } });
            var mq = new MinuteroQqq(fuente, cinta, new OpcionesQqq { Corregida = true, ContratoDe = OpcionesQqq.ContratoFijo("Z6") });
            var arch = Directory.GetFiles(Path.Combine(carpeta, "familia"), "muestras-QQQ-*.jsonl").OrderBy(x => x, StringComparer.Ordinal).ToList();
            foreach (var a in arch.Skip(Math.Max(0, arch.Count - 2)))
                foreach (var l in File.ReadAllLines(a))
                    if (ConversionQqq.LeerLineaMuestra(l, out var ts, out var p, out var k)) mq.Conversion.SembrarPrecio(ts, p, k);
            var r20 = new Replica20(fuente, cinta, new OpcionesReplica20 { Contrato = "MNQZ6" });
            var cl = clasica ? (clDada ?? new ClasicaNdx(fuente, cinta, new OpcionesClasicaNdx { Contrato = "MNQZ6" })) : null;
            IExtrasMinuto ex = clasica ? (IExtrasMinuto)new ExtrasCompuestos(r20, cl) { Error = (d, e) => P("    ERROR extras " + d + ": " + e) } : r20;
            var m = new MotorFamilia(new OpcionesMotorFamilia
            {
                Carpeta = carpeta, Corregida = true, FuentesListas = () => true, EsperaArranqueS = 0, EsperaCierreS = 0,
                RutaLog = Path.Combine(carpeta, "pythiagex4-familia.log"), Extras = ex
            });
            m.Configurar(cinta, null, fuente, new ILibroMinutero[] { nd, mq }, null, "MNQZ6");
            for (int i = 0; i < 600; i++) { m.Avanzar(ahora); if (!m.Pendiente) break; }
            return new Corrida { Motor = m, R20 = r20, Cl = cl, Seg = sw.Elapsed.TotalSeconds };
        }

        /// <summary>La linea de niv-*.jsonl de un minuto SIN las series de la clasica (lo que escribiria la 4.1.4 con la Replica20). 4.1.5b: sin R10_NDX_zero
        /// NI R10_NDX_dom (las dos van en Extra y comparten la meta 'Clasica NDX').</summary>
        static string LineaSinR10(RegistroMinuto r)
        {
            int i = CatalogoFamilia.IndiceExtra(ClasicaNdx.SERIE), j = CatalogoFamilia.IndiceExtra(ClasicaNdx.SERIE_DOM);
            Nivel[][] ex = null; double[][] ks = null;
            if (r.Extra != null) { ex = (Nivel[][])r.Extra.Clone(); if (i < ex.Length) ex[i] = null; if (j >= 0 && j < ex.Length) ex[j] = null; }
            if (r.ExtraStrikes != null) { ks = (double[][])r.ExtraStrikes.Clone(); if (i < ks.Length) ks[i] = null; if (j >= 0 && j < ks.Length) ks[j] = null; }
            return AlmacenNiveles.Linea(new RegistroMinuto
            {
                Clave = r.Clave, Libros = r.Libros, Series = r.Series, Strikes = r.Strikes, Meta = r.Meta, FutMnq = r.FutMnq, Extra = ex, ExtraStrikes = ks,
                MetaExtra = (r.MetaExtra ?? new MetaLibro[0]).Where(x => x.Libro != ResultadoClasica.LIBRO).ToArray()
            });
        }

        static string R10(RegistroMinuto r)
        {
            int i = CatalogoFamilia.IndiceExtra(ClasicaNdx.SERIE);
            var lv = r?.Extra != null && i < r.Extra.Length ? r.Extra[i] : null;
            var ks = r?.ExtraStrikes != null && i < r.ExtraStrikes.Length ? r.ExtraStrikes[i] : null;
            var m = r?.MetaExtraDe(ResultadoClasica.LIBRO);
            return lv == null ? "-" : lv[0].Etq + " " + F(lv[0].Precio) + " K" + F(ks?[0] ?? double.NaN) + " base " + F(m?.Conv ?? double.NaN, "0.000000") + " | " + (m?.ConvTexto ?? "");
        }

        /// <summary>4.1.5b: las dominantes de la clasica guardadas en un minuto ("D1 31090.09 -5550.9 K30850 | D2 ...", "-" si no hay).</summary>
        static string R10Dom(RegistroMinuto r)
        {
            int j = CatalogoFamilia.IndiceExtra(ClasicaNdx.SERIE_DOM);
            var lv = r?.Extra != null && j >= 0 && j < r.Extra.Length ? r.Extra[j] : null;
            var ks = r?.ExtraStrikes != null && j >= 0 && j < r.ExtraStrikes.Length ? r.ExtraStrikes[j] : null;
            return lv == null ? "-" : string.Join(" | ", lv.Select((n, q) => n.Etq + " " + F(n.Precio) + " " + F(n.GexM, "0.0") + " K" + F(ks != null && q < ks.Length ? ks[q] : double.NaN)));
        }

        static void Motor(string tmp, CintaFamilia cinta, FuenteArchivo f4, List<string> dias)
        {
            P(); P("== 6. MOTOR: Replica20 sola (A, la 4.1.4) contra el compuesto Replica20 + ClasicaNdx (B, la 4.1.5); sesion " + SESION + " hasta " + AHORA.ToString("HH:mm", Inv) + " UTC");
            var A = CorrerMotor(Path.Combine(tmp, "A"), cinta, f4, false, AHORA);
            var B = CorrerMotor(Path.Combine(tmp, "B"), cinta, f4, true, AHORA);
            P("  A: " + A.Motor.Almacen.Cuantos + " minutos con libros en " + A.Seg.ToString("0.0", Inv) + " s | B: " + B.Motor.Almacen.Cuantos + " en " + B.Seg.ToString("0.0", Inv) + " s");
            int ig = 0, dif = 0, conR10 = 0, conDom = 0; var ej = new List<string>();
            var claves = A.Motor.Almacen.Todos.Select(r => r.Clave).Union(B.Motor.Almacen.Todos.Select(r => r.Clave)).OrderBy(x => x).ToList();
            int iR = CatalogoFamilia.IndiceExtra(ClasicaNdx.SERIE), iD = CatalogoFamilia.IndiceExtra(ClasicaNdx.SERIE_DOM);
            foreach (var k in claves)
            {
                var ra = A.Motor.Almacen.Get(k); var rb = B.Motor.Almacen.Get(k);
                if (ra != null && rb != null && AlmacenNiveles.Linea(ra) == LineaSinR10(rb)) ig++;
                else { dif++; if (ej.Count < 4) ej.Add(k + " A " + (ra == null ? "-" : AlmacenNiveles.Linea(ra).Substring(0, 160)) + " | B " + (rb == null ? "-" : LineaSinR10(rb).Substring(0, 160))); }
                if (rb?.Extra != null && iR < rb.Extra.Length && rb.Extra[iR] != null) conR10++;
                if (rb?.Extra != null && iD >= 0 && iD < rb.Extra.Length && rb.Extra[iD] != null) conDom++;
            }
            Ok(dif == 0 && ig > 900, "M1 las 21 series, la meta, las R20/DOMS y su conversion identicas con y sin la clasica (linea de niv sin la R10 ni, 4.1.5b, la R10_NDX_dom): " + ig + " minutos iguales, " + dif + " distintos", string.Join(" || ", ej));
            int enRueda = claves.Count(k => { var t = TiempoFam.DeClave(k); return t >= new DateTime(2026, 10, 9, 3, 0, 0, DateTimeKind.Utc); });
            Ok(conR10 >= B.Motor.Almacen.Cuantos - 5 && conR10 > 900, "M2 la R10 en " + conR10 + " de " + B.Motor.Almacen.Cuantos + " minutos con libros (" + enRueda + " desde las 03:00Z)");
            Ok(conDom == conR10, "M2b (4.1.5b) la R10_NDX_dom (Clasica NDX D1-D3) en " + conDom + " minutos: en todos los que tienen el 0Γ (" + conR10 + "; se calculan juntas, con la misma cadena y la misma base)");
            {   // M2c (4.1.5b) lo que guardo el motor = la funcion: NivelesDom(Zero(t, fut)) del mismo ClasicaNdx, minuto por minuto
                int mIg = 0, mDif = 0; var mej = new List<string>();
                foreach (var r in B.Motor.Almacen.Todos)
                {
                    if (r?.Extra == null || iD < 0 || iD >= r.Extra.Length || r.Extra[iD] == null) continue;
                    var t = TiempoFam.DeClave(r.Clave);
                    var z = B.Cl.Zero(t, B.Cl.FutClasica(t, r.FutMnq));
                    var (ld, kd) = z == null ? (null, null) : ClasicaNdx.NivelesDom(z);
                    bool igual = ld != null && ld.Length == r.Extra[iD].Length;
                    for (int q = 0; igual && q < ld.Length; q++)
                        igual = ld[q].Etq == r.Extra[iD][q].Etq && ld[q].Precio.Equals(r.Extra[iD][q].Precio) && ld[q].GexM.Equals(r.Extra[iD][q].GexM) && kd[q].Equals(r.ExtraStrikes[iD][q]);
                    if (igual) mIg++; else { mDif++; if (mej.Count < 4) mej.Add(t.ToString("HH:mm", Inv) + " guardado " + R10Dom(r) + " | funcion " + (ld == null ? "-" : string.Join(" ", ld.Select(n => n.Etq + " " + F(n.Precio))))); }
                }
                Ok(mDif == 0 && mIg == conDom, "M2c (4.1.5b) las D1-D3 que guardo el motor son las de ClasicaNdx.NivelesDom(Zero(t, fut)) en " + mIg + " de " + conDom + " minutos (rol, precio, monto y strike al bit)", string.Join(" || ", mej));
            }
            var fa = A.Motor.Foto; var fb = B.Motor.Foto;
            string Act(NivelActual a) => a.Serie + " " + a.Rol + " " + F(a.Precio) + " " + F(a.Strike) + " " + F(a.GexM) + " " + a.DatoUtc.ToString("o", Inv) + " " + a.OiViejo;
            var aa = fa.Actuales.Select(Act).ToList();
            var ab = fb.Actuales.Where(a => a.Serie != ClasicaNdx.SERIE && a.Serie != ClasicaNdx.SERIE_DOM).Select(Act).ToList();
            var r10a = fb.Actuales.Where(a => a.Serie == ClasicaNdx.SERIE).ToList();
            var doma = fb.Actuales.Where(a => a.Serie == ClasicaNdx.SERIE_DOM).ToList();
            Ok(aa.SequenceEqual(ab) && r10a.Count == 1 && r10a[0].Rol == "0G est." && r10a[0].Tipo == "ZEST" && double.IsNaN(r10a[0].GexM)
               && doma.Count >= 1 && doma.Count <= 3 && doma.Select((a, q) => a.Rol == "D" + (q + 1) && a.Tipo == "DOMS" && a.Libro == "NDX" && !double.IsNaN(a.GexM)).All(x => x),
               "M3 los actuales de A identicos en B; B agrega la R10 (" + (r10a.Count == 0 ? "-" : r10a[0].Rol + " " + F(r10a[0].Precio) + " K " + F(r10a[0].Strike) + ", dato " + r10a[0].DatoUtc.ToString("HH:mm:ss", Inv) + "Z")
               + ") y (4.1.5b) las D1-D3 de la clasica con su monto (" + string.Join(", ", doma.Select(a => a.Rol + " " + F(a.Precio) + " " + F(a.GexM, "0.0") + "M K " + F(a.Strike))) + ")");
            var fuB = fb.Fuentes.Where(x => x.Libro == ResultadoClasica.LIBRO).ToList();
            Ok(fa.Fuentes.Select(x => x.Libro + "|" + x.Texto).SequenceEqual(fb.Fuentes.Where(x => x.Libro != ResultadoClasica.LIBRO).Select(x => x.Libro + "|" + x.Texto)) && fuB.Count == 1,
               "M4 las fuentes de A identicas en B; B agrega 'Clasica NDX': " + (fuB.Count == 0 ? "-" : F(fuB[0].ConvValor) + " · " + fuB[0].Texto));
            {
                var r = B.Motor.Almacen.Get(TiempoFam.Clave(T_OPERADOR));
                P("  17:10Z en B: " + R10(r));
                double p = r?.Extra?[iR]?[0].Precio ?? double.NaN;
                Ok(p > 31066 && p < 31070.5, "M5 el minuto 17:10Z guardado por el motor: Clasica NDX 0Γ " + F(p) + " (la clasica: puntitos 31.067,33-31.068,32)");
            }

            // ---- persistencia
            P(); P("== 6b. PERSISTENCIA: el motor B2 (compuesto) relee el archivo de B");
            var lineasB = File.ReadAllLines(Directory.GetFiles(Path.Combine(tmp, "B", "familia"), "niv-*.jsonl").Single());
            int conClave = lineasB.Count(l => l.Contains("\"R10_NDX_zero\":[[")), conMx = lineasB.Count(l => l.Contains("{\"l\":\"Clasica NDX\"")), conDomL = lineasB.Count(l => l.Contains("\"R10_NDX_dom\":[["));
            P("  niv de B: " + lineasB.Length + " lineas; con R10_NDX_zero " + conClave + ", con R10_NDX_dom " + conDomL + " (4.1.5b), con la meta 'Clasica NDX' en mx " + conMx);
            P("  ej.: " + (lineasB.FirstOrDefault(l => l.Contains("\"R10_NDX_zero\"") && l.Contains("17:1")) is string e1 ? e1.Substring(e1.IndexOf("\"R10_NDX_zero\"", StringComparison.Ordinal), 60) : "-"));
            Copiar(Directory.GetFiles(Path.Combine(tmp, "B", "familia"), "niv-*.jsonl").Single(), Path.Combine(tmp, "B2", "familia", "niv-" + SESION + "-MNQZ6.jsonl"));
            var B2 = CorrerMotor(Path.Combine(tmp, "B2"), cinta, f4, true, AHORA);
            int pIg = 0, pDif = 0;
            foreach (var k in claves)
            {
                var rb = B.Motor.Almacen.Get(k); var r2 = B2.Motor.Almacen.Get(k);
                if (rb == null && r2 == null) continue;
                if (rb != null && r2 != null && AlmacenNiveles.Linea(rb) == AlmacenNiveles.Linea(r2)) pIg++; else pDif++;
            }
            Ok(conClave > 900 && conMx > 900 && conDomL == conClave && pDif == 0 && B2.Motor.MinutosCompletados == 0,
               "P1 releido: " + pIg + " lineas iguales, " + pDif + " distintas; completados " + B2.Motor.MinutosCompletados + " (nada que completar)");

            // ---- completar un archivo de la 4.1.4 (con R20, sin R10)
            P(); P("== 6c. COMPLETAR: el compuesto (C) abre el archivo de A (la 4.1.4: R20 sin R10)");
            var nivA = Directory.GetFiles(Path.Combine(tmp, "A", "familia"), "niv-*.jsonl").Single();
            Copiar(nivA, Path.Combine(tmp, "C", "familia", "niv-" + SESION + "-MNQZ6.jsonl"));
            var C = CorrerMotor(Path.Combine(tmp, "C"), cinta, f4, true, AHORA);
            int cIg = 0, cDif = 0, r20Ig = 0; var cej = new List<string>();
            foreach (var k in claves)
            {
                var rb = B.Motor.Almacen.Get(k); var rc = C.Motor.Almacen.Get(k); var ra = A.Motor.Almacen.Get(k);
                if (rb == null || rc == null || ra == null) continue;
                if (R10(rb) == R10(rc) && R10Dom(rb) == R10Dom(rc)) cIg++; else { cDif++; if (cej.Count < 4) cej.Add(TiempoFam.DeClave(k).ToString("HH:mm", Inv) + " B " + R10(rb) + " / " + R10Dom(rb) + " C " + R10(rc) + " / " + R10Dom(rc)); }
                if (LineaSinR10(rc) == AlmacenNiveles.Linea(ra)) r20Ig++;
            }
            Ok(C.Motor.MinutosCompletados > 900 && cDif == 0 && cIg > 900, "C1 " + C.Motor.MinutosCompletados + " minutos de la 4.1.4 completados en memoria: R10 y (4.1.5b) R10_NDX_dom iguales al calculo en vivo en " + cIg + ", distintos " + cDif, string.Join(" || ", cej));
            Ok(r20Ig == cIg + cDif, "C2 las R20/DOMS del archivo de la 4.1.4 quedan como estaban (" + r20Ig + " minutos)");
            Ok(File.ReadAllLines(Path.Combine(tmp, "C", "familia", "niv-" + SESION + "-MNQZ6.jsonl")).SequenceEqual(File.ReadAllLines(nivA)), "C3 el archivo de la 4.1.4 no se toca al completar");

            // ---- rehecho: la cinta sin el 08-10 al arrancar
            P(); P("== 7. REHECHO: la cinta todavia sin el 08-10 cuando arranca el motor (ATAS cargando la historia)");
            string d0810 = dias.FirstOrDefault(x => x.EndsWith("-2026-10-08.bin", StringComparison.Ordinal));
            var parcial = new CintaFamilia("MNQZ6", "NQ"); foreach (var f in dias) if (f != d0810) parcial.CargarArchivoDia(f);
            var clR = new ClasicaNdx(f4, parcial, new OpcionesClasicaNdx { Contrato = "MNQZ6", SondeoCadaS = 0 });
            var R = CorrerMotor(Path.Combine(tmp, "R"), parcial, f4, true, AHORA, clR);
            var kOp = TiempoFam.Clave(T_OPERADOR);
            string antes = R10(R.Motor.Almacen.Get(kOp));
            P("  R: " + R.Motor.Almacen.Cuantos + " minutos con libros (sin el dia UTC 08-10 en la cinta los de antes de las 00:00Z no tienen fut y quedan sin libros, como cualquier serie)");
            P("  sin el 08-10: 17:10Z " + antes + "; muestras sin vela " + clR.SinVela + ", generacion " + clR.Generacion);
            parcial.CargarArchivoDia(d0810);
            for (int i = 0; i < 50; i++) { R.Motor.Avanzar(AHORA.AddMinutes(1)); if (!R.Motor.Pendiente) break; }
            string despues = R10(R.Motor.Almacen.Get(kOp));
            P("  con el 08-10 cargado: 17:10Z " + despues + "; generacion " + clR.Generacion + ", desde " + clR.RehacerDesdeUtc.ToString("MM-dd HH:mm:ss", Inv) + "Z; minutos rehechos " + R.Motor.MinutosRehechos);
            int rIg = 0, rDif = 0; var rej = new List<string>();
            foreach (var k in R.Motor.Almacen.Todos.Select(r => r.Clave).OrderBy(x => x))
            {
                var rr = R.Motor.Almacen.Get(k); var rb = B.Motor.Almacen.Get(k);
                if (rb == null || TiempoFam.DeClave(k) > AHORA) continue;
                // la meta lleva la base sincronizada de la 4.1 del minuto (su libro NDX puede salir distinto sin la cinta del 08-10): se compara nivel, strike y base
                string Corto(RegistroMinuto x) { var s = R10(x); int c = s.IndexOf(" | ", StringComparison.Ordinal); return (c > 0 ? s.Substring(0, c) : s) + " || " + R10Dom(x); }
                if (Corto(rr) == Corto(rb)) rIg++; else { rDif++; if (rej.Count < 4) rej.Add(TiempoFam.DeClave(k).ToString("HH:mm", Inv) + " R " + Corto(rr) + " B " + Corto(rb)); }
            }
            Ok(antes.Contains("= CRUDA") && !antes.Contains("= de la rueda"), "R1 sin el 08-10 en la cinta la base de la rueda del 08-10 no se puede medir: la del 07-10 vencio (24 h) y la replica usa la CRUDA, como la clasica");
            Ok(clR.Generacion > 0 && R.Motor.MinutosRehechos > 0 && rDif == 0 && rIg > 900,
               "R2 al llegar el 08-10 la regla se rehace (generacion " + clR.Generacion + ") y el motor rehace " + R.Motor.MinutosRehechos + " minutos: R10 y (4.1.5b) R10_NDX_dom iguales al arranque con la cinta completa en " + rIg + ", distintos " + rDif, string.Join(" || ", rej));

            Etiqueta(B, tmp);
        }

        // ==============================================================================================================================
        static void Etiqueta(Corrida B, string tmp)
        {
            P(); P("== 8. ETIQUETA, DETALLE Y ROTULO (ArmadoPantalla) sobre la foto real de B");
            var aj = new AjustesPantalla();
            foreach (var s in SeriesPantalla.PrendidasPorDefecto) aj.Visibles.Add(s);
            aj.PanelAbierto = true;
            var area = new Rectangle(0, 0, 4000, 900);
            double alto = 31200, bajo = 30900;
            var v = new VistaPantalla { Area = area, XDerecha = 4000, Instrumento = "MNQZ6", PrecioAlto = alto, PrecioBajo = bajo, Y = p => (int)Math.Round(area.Top + (alto - p) / (alto - bajo) * area.Height), PrecioUltimo = 31080 };
            var d = new DibujoPantalla();
            var foto = B.Motor.Foto;
            ArmadoPantalla.Armar(d, foto, new CatalogoPantalla(CatalogoFamilia.Series), aj, v, AHORA, (s, t) => new Size((int)Math.Ceiling((s?.Length ?? 0) * t * 0.74), (int)Math.Ceiling(t * 1.45)));
            var et = d.Rotulos.Where(r => r.Serie == ClasicaNdx.SERIE || (r.Acompanan ?? "").Contains("Clasica")).ToList();
            foreach (var e in et) P("    etiqueta '" + e.Txt + "'");
            foreach (var l in d.Panel.Where(l => l.Contains("Clasica"))) P("    pestaña: " + l);
            var r10 = foto.Actuales.First(a => a.Serie == ClasicaNdx.SERIE);
            string pr = ArmadoPantalla.P(r10.Precio);
            Ok(d.Rotulos.Any(r => (r.Serie == ClasicaNdx.SERIE && r.Txt == "Clasica NDX 0Γ " + pr + " V") || (r.Acompanan ?? "").Contains("Clasica NDX 0Γ")),
               "E1 la etiqueta con el formato vigente, zero sin monto: 'Clasica NDX 0Γ " + pr + " V' (o nombrada al final de un grupo)");
            Ok(d.Panel.Any(l => l.Contains("Clasica NDX (replica, CBOE)") && l.Contains("base clasica") && l.Contains("de la rueda 2026-10-08")),
               "E2 la pestaña dice de donde sale: la fuente 'Clasica NDX (replica, CBOE) · base clasica ... = de la rueda 2026-10-08 (...)'");
            Ok(d.Panel.Any(l => l.Contains("Clasica NDX 0Γ·vol (base clasica ") && l.Contains("de la rueda del 08-10, congelada") && l.Contains("base de ahora")),
               "E3 el detalle del nivel: la base usada y de cuando es ('de la rueda del 08-10, congelada') junto a la base de ahora y la diferencia");
            var nt = new CatalogoPantalla(CatalogoFamilia.Series).NombreTramo[ClasicaNdx.SERIE];
            Ok(nt.Base == "Clasica NDX 0Γ" && nt.Suf == "", "E4 el rotulo de los tramos de historia la nombra 'Clasica NDX 0Γ' (" + nt.Base + "/" + nt.Suf + ")");
            var (o1, n1, a1, _) = ArmadoPantalla.OrigenClasica("base clasica 236.55 = CRUDA 29 ticks (la de la rueda 2026-10-08 243.06 tiene 1450 min: vencio a las 24 h) · ahora: 4.1 sincronizada 228.62 · zero en el indice 30820.00");
            Ok(!n1 && o1.StartsWith("CRUDA de forwards", StringComparison.Ordinal) && o1.Contains("vencio") && Math.Abs(a1 - 228.62) < 1e-9, "E5 con la base de respaldo el detalle lo dice en naranja: '" + o1 + "'");
            // 4.1.5b: las D1-D3 de la clasica (R10_NDX_dom, prendida por defecto) sobre la misma foto real
            var doms = foto.Actuales.Where(a => a.Serie == ClasicaNdx.SERIE_DOM).ToList();
            foreach (var e in d.Rotulos.Where(r => r.Serie == ClasicaNdx.SERIE_DOM || (r.Acompanan ?? "").Contains("Clasica NDX D"))) P("    etiqueta '" + e.Txt + "'");
            foreach (var l in d.Panel.Where(l => l.Contains("Clasica NDX D"))) P("    pestaña: " + l);
            Ok(doms.Count > 0 && doms.All(a => d.Rotulos.Any(r => (r.Serie == ClasicaNdx.SERIE_DOM && r.Txt.StartsWith("Clasica NDX " + a.Rol + " ", StringComparison.Ordinal)
                                                                   && r.Txt.Contains(" " + ArmadoPantalla.Monto(a.GexM) + " ") && r.Txt.EndsWith(" " + ArmadoPantalla.P(a.Precio) + " V", StringComparison.Ordinal))
                                                                  || (r.Acompanan ?? "").Contains("Clasica NDX " + a.Rol))),
               "E6 (4.1.5b) cada D1-D3 de la clasica (" + doms.Count + ") con su etiqueta: 'Clasica NDX <rol> <monto> <precio> V' (o nombrada al final de un grupo)");
            Ok(d.Panel.Any(l => l.Contains("Clasica NDX D1·vol") && l.Contains("(base clasica ") && l.Contains("de la rueda del 08-10, congelada")),
               "E7 (4.1.5b) el detalle de la D1 de la clasica dice la MISMA base que el 0Γ (la de la rueda del 08-10, congelada) y la de ahora");
            // 4.1.5d: la salvedad debajo de la fuente, las D1-D3 sin cambio y el detalle con el strike, sobre la foto real
            int iF = d.Panel.FindIndex(l => l.Contains("Clasica NDX (replica, CBOE)"));
            Ok(iF >= 0 && iF + 1 < d.Panel.Count && d.Panel[iF + 1] == ArmadoPantalla.AVISO_CLASICA, "E8 (4.1.5d) debajo de la fuente va la salvedad medida del 09-10 ('" + ArmadoPantalla.AVISO_CLASICA.Trim() + "')");
            Ok(doms.All(a => !ArmadoPantalla.TieneLado(a)) && !d.Rotulos.Any(r => r.Serie == ClasicaNdx.SERIE_DOM && (r.Txt.Contains("▲") || r.Txt.Contains("▼"))),
               "E9 (4.1.5d) las D1-D3 de la clasica sin cambio ▲▼ (TieneLado false, como las R20)");
            Ok(d.Panel.Where(l => l.Contains("Clasica NDX D1·vol")).All(l => !l.Contains("en el indice") && (l.Contains(", centroide ±12)") || !l.Contains("(strike"))),
               "E10 (4.1.5d) el detalle de las D1-D3 dice '(strike NDX K, centroide ±12)', nunca 'en el indice' (la leyenda del zero)");
        }

        // ==============================================================================================================================
        /// <summary>IFuenteCboe con una lista fija (casos sinteticos de la 4.1.5d).</summary>
        sealed class FuenteFija : IFuenteCboe
        {
            private readonly FotoCadena[] _f;
            public FuenteFija(params FotoCadena[] f) { _f = f; }
            public void Arrancar() { }
            public void Parar() { }
            public IReadOnlyList<FotoCadena> Fotos(string libro, DateTime desdeUtc, DateTime hastaUtc) => _f.Where(x => x.GeneradoUtc >= desdeUtc && x.GeneradoUtc < hastaUtc).ToList();
            public FotoCadena Ultima(string libro) => _f.LastOrDefault();
            public long Version => 1;
            public string Estado => "fija";
        }

        static int LineasEn(string dir) => Directory.Exists(Path.Combine(dir, "familia"))
            ? Directory.GetFiles(Path.Combine(dir, "familia"), MuestrasClasicaArchivo.PREFIJO + "*.jsonl").Sum(p => File.ReadAllLines(p).Count(l => l.Trim().Length > 0)) : 0;

        static HashSet<DateTime> SellosEn(string dir)
        {
            var h = new HashSet<DateTime>();
            if (!Directory.Exists(Path.Combine(dir, "familia"))) return h;
            foreach (var p in Directory.GetFiles(Path.Combine(dir, "familia"), MuestrasClasicaArchivo.PREFIJO + "*.jsonl"))
                foreach (var l in File.ReadAllLines(p)) { var m = MuestrasClasicaArchivo.Parsear(l); if (m != null) h.Add(m.TsUtc); }
            return h;
        }

        /// <summary>4.1.5d (revision de la 4.1.5b/c): (G1-G3) las muestras de la base de la rueda guardadas, con la cinta REAL del 09-10: la rueda cerrada no
        /// cambia aunque la cinta arranque sin esos dias; una vela incompleta queda provisional (no se guarda) hasta completarse; (G4-G7) dominantes sin
        /// cruce del zero, dominantes por OI en el texto, Falta y el formato del archivo, con cadenas sinteticas.</summary>
        static void Revision415d(string tmp, CintaFamilia cinta, FuenteArchivo f4, List<string> dias, ClasicaNdx rep, List<MuestraLog> mlog)
        {
            P(); P("== 9. 4.1.5d (revision de la 4.1.5b/c): muestras guardadas, vela incompleta, dominantes sin cruce, OI, Falta, archivo");
            var t2045 = new DateTime(2026, 10, 9, 20, 45, 0, DateTimeKind.Utc);
            var eRep = rep.BaseRueda(t2045); var e08Rep = rep.BaseRueda(T_OPERADOR);
            // G1: la primera corrida guarda TODAS las muestras con vela (sin el dato de la cinta: VelaCompleta null = completas, como antes de la 4.1.5d)
            string dirG = Path.Combine(tmp, "G"); try { if (Directory.Exists(dirG)) Directory.Delete(dirG, true); } catch { }
            Directory.CreateDirectory(dirG);
            var a = new ClasicaNdx(f4, cinta, new OpcionesClasicaNdx { Contrato = "MNQZ6", SondeoCadaS = 0, CarpetaDatos = dirG });
            var eA = a.BaseRueda(t2045); var mA = a.Muestras; var arA = a.Archivo;
            int lineas = LineasEn(dirG);
            P("  G1 primera corrida con CarpetaDatos: " + mA.Count + " muestras con vela (" + a.SinVela + " sin vela), " + arA.Guardadas + " guardadas en " + lineas + " lineas; base de la rueda del 09-10 a las 20:45Z "
              + F(eA.Base) + " (sin guardar: " + F(eRep.Base) + ")");
            Ok(mA.Count > 0 && arA.Guardadas == mA.Count && lineas == mA.Count && arA.Usadas == 0 && eA.Base.Equals(eRep.Base) && a.BaseRueda(T_OPERADOR).Base.Equals(e08Rep.Base),
               "G1 la primera corrida guarda las " + mA.Count + " muestras con vela (una linea por sello) y da las MISMAS bases que sin guardar (09-10 20:45Z " + F(eA.Base) + "; 08-10 congelada " + F(e08Rep.Base) + ")");
            // G2: un arranque con la cinta SIN el 08 ni el 09-10 (ATAS cargando): con las muestras guardadas la rueda cerrada no cambia
            var cintaCorta = new CintaFamilia("MNQZ6", "NQ");
            foreach (var f in dias.Where(x => x.EndsWith("-2026-10-06.bin", StringComparison.Ordinal) || x.EndsWith("-2026-10-07.bin", StringComparison.Ordinal))) cintaCorta.CargarArchivoDia(f);
            var b = new ClasicaNdx(f4, cintaCorta, new OpcionesClasicaNdx { Contrato = "MNQZ6", SondeoCadaS = 0, CarpetaDatos = dirG });
            var eB = b.BaseRueda(t2045); var mB = b.Muestras; var arB = b.Archivo;
            var porTsA = mA.ToDictionary(x => x.TsUtc);
            int iguales = mB.Count(x => porTsA.TryGetValue(x.TsUtc, out var y) && y.Precio.Equals(x.Precio) && y.Base.Equals(x.Base) && y.BaseUtc == x.BaseUtc && y.Buenas == x.Buenas);
            var c = new ClasicaNdx(f4, cintaCorta, new OpcionesClasicaNdx { Contrato = "MNQZ6", SondeoCadaS = 0 });
            var eC = c.BaseRueda(t2045);
            P("  G2 arranque con la cinta sin el 08 ni el 09-10: con las guardadas " + mB.Count + " muestras (" + arB.Usadas + " del archivo), base 09-10 " + F(eB.Base) + " rueda " + eB.BaseDia.ToString("MM-dd", Inv)
              + "; SIN las guardadas " + c.Muestras.Count + " muestras, base " + F(eC.Base) + " rueda " + (eC.Hay ? eC.BaseDia.ToString("MM-dd", Inv) : "-"));
            Ok(mB.Count == mA.Count && iguales == mA.Count && arB.Usadas == mA.Count && arB.Guardadas == 0 && eB.Base.Equals(eA.Base) && eB.BaseDia == eA.BaseDia && b.BaseRueda(T_OPERADOR).Base.Equals(e08Rep.Base),
               "G2 con la cinta sin el 08 ni el 09-10 las muestras guardadas dan las MISMAS " + iguales + " de " + mA.Count + " muestras (precio, base, adopcion) y las mismas bases (09-10 " + F(eB.Base) + "; 08-10 "
               + F(b.BaseRueda(T_OPERADOR).Base) + "): la rueda cerrada no depende de como arranque la cinta");
            Ok(!(eC.Hay && eC.BaseDia == eA.BaseDia && eC.Base.Equals(eA.Base)), "G2b sin las guardadas, con esa cinta la rueda del 09-10 NO se puede reproducir (base " + F(eC.Base) + "): lo que arregla la 4.1.5d");
            // G3: la vela incompleta (ticks con un hueco y sin la del grafico) queda provisional: entra, no se guarda, se vuelve a mirar; al completarse se guarda
            string dirQ = Path.Combine(tmp, "Q"); try { if (Directory.Exists(dirQ)) Directory.Delete(dirQ, true); } catch { }
            Directory.CreateDirectory(dirQ);
            bool todas = false;
            var ini = new DateTime(2026, 10, 9, 19, 30, 0, DateTimeKind.Utc); var fin = new DateTime(2026, 10, 9, 19, 56, 0, DateTimeKind.Utc);
            var q = new ClasicaNdx(f4, cinta, new OpcionesClasicaNdx { Contrato = "MNQZ6", SondeoCadaS = 0, CarpetaDatos = dirQ, VelaCompleta = t => todas || !(t >= ini && t < fin) });
            var eQ = q.BaseRueda(t2045); int prov = q.Provisionales; var arQ = q.Archivo;
            var enRango = q.Muestras.Where(x => x.Provisional).Select(x => x.TsUtc).ToList();
            var sellosQ = SellosEn(dirQ);
            P("  G3 con las velas de 19:30-19:56Z del 09-10 incompletas: " + prov + " provisionales (sellos " + (enRango.Count > 0 ? enRango.Min().ToString("HH:mm:ss", Inv) + "-" + enRango.Max().ToString("HH:mm:ss", Inv) : "-")
              + "), " + arQ.Guardadas + " guardadas; base " + F(eQ.Base));
            Ok(prov > 0 && prov == enRango.Count && enRango.All(ts => !sellosQ.Contains(ts)) && arQ.Guardadas == q.Muestras.Count - prov && eQ.Base.Equals(eA.Base),
               "G3 " + prov + " muestras con la vela incompleta ENTRAN provisionales (la base es la misma: " + F(eQ.Base) + ") pero NO se guardan (" + arQ.Guardadas + " guardadas de " + q.Muestras.Count + ")");
            long gen0 = q.Generacion; todas = true;
            var eQ2 = q.BaseRueda(t2045); var arQ2 = q.Archivo; var sellosQ2 = SellosEn(dirQ);
            Ok(q.Provisionales == 0 && arQ2.Guardadas == prov && enRango.All(ts => sellosQ2.Contains(ts)) && eQ2.Base.Equals(eQ.Base) && q.Generacion == gen0,
               "G3b al completarse la vela (sondeo) las " + prov + " provisionales se guardan (" + arQ2.Guardadas + "), la base no cambia (el cierre era el mismo) y la generacion tampoco (" + gen0 + " -> " + q.Generacion + ")");
            // G3c (informativo): con la cinta REAL (solo ticks: sin las velas del grafico) cuantas muestras del 09-10 tienen la vela con un hueco
            var r2 = new ClasicaNdx(f4, cinta, new OpcionesClasicaNdx { Contrato = "MNQZ6", SondeoCadaS = 0, VelaCompleta = t => cinta.VelaM2Completa(t) });
            r2.BaseRueda(t2045);
            var pv = r2.Muestras.Where(x => x.Provisional).ToList();
            var d09 = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
            var u24 = new HashSet<DateTime>(r2.Muestras.Where(x => x.Dia == d09).Select(x => x.TsUtc).OrderBy(x => x).Reverse().Take(24));
            P("  G3c (informativo) con la cinta del arnes (ticks, sin velas del grafico) y CintaFamilia.VelaM2Completa: " + pv.Count + " provisionales; del 09-10: "
              + pv.Count(x => x.Dia == d09) + " (velas " + string.Join(" ", pv.Where(x => x.Dia == d09).Select(x => x.VelaUtc.ToString("HH:mm", Inv)).Distinct().Take(20)) + "), "
              + pv.Count(x => u24.Contains(x.TsUtc)) + " entre las ultimas 24 del dia (" + string.Join(" ", pv.Where(x => u24.Contains(x.TsUtc)).Select(x => x.TsUtc.ToString("HH:mm:ss", Inv))) + ")");
            // G3d (MEDIDO, el mecanismo del 229,32 contra 229,98): el cierre de la vela de 2 min del GRAFICO = el cierre de 1 min de su minuto impar, que la
            // clasica logueo el 09-10 (corre en 1 min) para esos mismos sellos. Con esos cierres en las provisionales, la regla del dia da ...
            var cierre1 = mlog.Where(x => x.Pared.ToString("yyyy-MM-dd", Inv) == "2026-10-09").GroupBy(x => x.Vela).ToDictionary(gq => gq.Key, gq => gq.First().Cierre);
            var dia09 = r2.Muestras.Where(x => x.Dia == d09).OrderBy(x => x.GenUtc).ToList();
            var e0 = r2.BaseRueda(T_OPERADOR);                                  // el estado con que empieza el 09-10 (la base de la rueda del 08-10)
            var obsG = new List<double>(); double bG = e0.Base; DateTime uG = e0.BaseUtc; int cambiadas = 0, sinCierre = 0; var detG = new List<string>();
            foreach (var x in dia09)
            {
                double p = x.Precio;
                if (x.Provisional)
                {
                    var b2 = TiempoFam.DeMs(TiempoFam.AperturaM2(TiempoFam.Ms(x.VelaUtc)));
                    var minImpar = new DateTime(b2.Ticks, DateTimeKind.Utc).AddMinutes(1);
                    if (cierre1.TryGetValue(minImpar, out var cg)) { if (!cg.Equals(p)) { cambiadas++; detG.Add(x.TsUtc.ToString("HH:mm:ss", Inv) + " " + F(p) + "->" + F(cg)); } p = cg; }
                    else sinCierre++;
                }
                ReglaBaseClasica.Paso(obsG, p - x.Spot, p, x.GenUtc, ref bG, ref uG, out _, out _, out _, out _);
            }
            var eR2 = r2.BaseRueda(t2045);
            P("  G3d (MEDIDO) las provisionales del 09-10 con el cierre de 2 min del GRAFICO (el de 1 min de su minuto impar, del log de la clasica): " + cambiadas
              + " cambian de cierre (" + string.Join(", ", detG) + "), " + sinCierre + " sin ese cierre en el log; la regla del dia da " + F(bG) + " (con los de la cinta de ticks: " + F(eR2.Base)
              + "; la 4.1.5c en ATAS con las velas del grafico: 229,32 de 17:12 a 17:35 ART y desde las 18:08)");
            Ok(cambiadas > 0 && Math.Abs(bG - 229.32) <= 0.005 && Math.Abs(eR2.Base - 229.98) <= 0.005,
               "G3d el escalon 229,32 / 229,98 de la rueda del 09-10 (revision 4.1.5b/c) sale ENTERO de las velas de 2 min con un hueco de ticks: con los cierres del grafico en "
               + "esas provisionales la regla da " + F(bG) + " (lo que tenia la 4.1.5c en ATAS con las velas del grafico) y con los de la cinta de ticks " + F(eR2.Base));

            // ---- casos sinteticos: S = 30.900, base CRUDA 230 (pasa la cota del carry), 0DTE de ~7 h; sin muestras de la rueda (sello de noche)
            var gen = new DateTime(2026, 10, 9, 21, 0, 0, DateTimeKind.Utc); var t = gen.AddMinutes(1); double fut = 31130;
            FotoCadena Sint(bool conVol, bool soloCalls)
            {
                var filas = new List<FilaCadena>();
                for (double k = 30800; k <= 31000; k += 10)
                    filas.Add(new FilaCadena(k, 0, 100, soloCalls ? 0 : 100, 0.2, 0.2, conVol ? 500 : 0, conVol && !soloCalls ? 500 : 0));
                return new FotoCadena { Libro = "NDX", GeneradoUtc = gen, TsUtc = gen, DatoUtc = gen, Spot = 30900, Dias = new[] { 0.3 }, Filas = filas.ToArray(), BaseCruda = 230, BaseErrorTicks = 10 };
            }
            // G4: solo calls con volumen: gamma positiva en toda la grilla (sin cruce) -> el zero NaN pero las D1-D3 igual (como la clasica)
            var cl4 = new ClasicaNdx(new FuenteFija(Sint(true, true)), null, new OpcionesClasicaNdx { Contrato = "MNQZ6", SondeoCadaS = 0 });
            var r4 = cl4.Zero(t, fut);
            var (lv4, ks4, meta4) = r4 == null ? ((Nivel[])null, (double[])null, (MetaLibro)null) : ClasicaNdx.Niveles(r4);
            var (ld4, _) = r4 == null ? ((Nivel[])null, (double[])null) : ClasicaNdx.NivelesDom(r4);
            P("  G4 cadena solo de calls: " + (r4 == null ? "null" : "zero " + F(r4.Zero) + ", " + r4.Doms.Count + " dominantes (" + r4.LibroDom + "), base " + F(r4.Base) + " " + r4.Origen + " | " + r4.Texto));
            Ok(r4 != null && double.IsNaN(r4.Zero) && r4.Doms.Count > 0 && r4.LibroDom == "vol" && lv4 == null && meta4 != null && ld4 != null && ld4.Length == r4.Doms.Count && r4.Texto.Contains("zero sin cruce en +-3 %"),
               "G4 sin cruce del zero las D1-D3 se calculan igual (antes Zero devolvia null y no habia ninguna): 0Γ sin nivel, meta y D1-D3 con nivel, texto 'zero sin cruce'");
            var rm = new RegistroMinuto { Extra = new Nivel[CatalogoFamilia.Extra.Length][], ExtraStrikes = new double[CatalogoFamilia.Extra.Length][], FutMnq = fut };
            Ok(new ClasicaNdx(new FuenteFija(Sint(true, true)), null, new OpcionesClasicaNdx { Contrato = "MNQZ6", SondeoCadaS = 0 }).Completar(rm, t) && !cl4.Falta(rm)
               && rm.Extra[CatalogoFamilia.IndiceExtra(ClasicaNdx.SERIE)] == null && rm.Extra[CatalogoFamilia.IndiceExtra(ClasicaNdx.SERIE_DOM)] != null && rm.MetaExtraDe(ResultadoClasica.LIBRO) != null
               && cl4.Falta(new RegistroMinuto { Extra = new Nivel[CatalogoFamilia.Extra.Length][], FutMnq = fut }),
               "G5 ese minuto se guarda con las D1-D3 y la meta y sin el 0Γ, y Falta no lo pide de nuevo (uno sin nada, si)");
            // G6: sin volumen en el radio: las dominantes por OI -> el texto lo marca ('dominantes por OI') para la etiqueta
            var r6 = new ClasicaNdx(new FuenteFija(Sint(false, true)), null, new OpcionesClasicaNdx { Contrato = "MNQZ6", SondeoCadaS = 0 }).Zero(t, fut);
            P("  G6 cadena sin volumen: " + (r6 == null ? "null" : r6.Doms.Count + " dominantes (" + r6.LibroDom + ") | " + r6.Texto));
            Ok(r6 != null && r6.LibroDom == "OI" && r6.Doms.Count > 0 && r6.Texto.Contains(ResultadoClasica.MARCA_DOM_OI) && r6.Texto.Contains("dominantes por OI"),
               "G6 sin volumen en el radio las D1-D3 salen del OI y el texto de la fuente lo dice ('dominantes por OI'): la etiqueta lleva OI (pantalla C119)");
            // G7: el archivo: ida y vuelta, linea rota ignorada, otro marco de vela no se usa
            string dirA = Path.Combine(tmp, "A7"); try { if (Directory.Exists(dirA)) Directory.Delete(dirA, true); } catch { }
            var arch = new MuestrasClasicaArchivo(dirA, null);
            var mu = new MuestraClasica { TsUtc = new DateTime(2026, 10, 9, 19, 40, 37, DateTimeKind.Utc), GenUtc = new DateTime(2026, 10, 9, 19, 41, 2, 123, DateTimeKind.Utc), VelaUtc = new DateTime(2026, 10, 9, 19, 24, 37, DateTimeKind.Utc),
                                         Spot = 30900.62, Precio = 31130.5, Muestra = 31130.5 - 30900.62 };
            int g7 = arch.Guardar(new[] { mu, mu }, 2, 960, ClasicaNdx.VERSION);
            File.AppendAllText(MuestrasClasicaArchivo.Ruta(Path.Combine(dirA, "familia"), mu.TsUtc.Date), "{\"ts\":\"2026-10-09 19:4\n");
            var arch2 = new MuestrasClasicaArchivo(dirA, null); arch2.Cargar(mu.TsUtc.Date, mu.TsUtc.Date);
            bool ida = arch2.Buscar(mu.TsUtc, 2, 960, out var g) && g.Precio.Equals(mu.Precio) && g.Spot.Equals(mu.Spot) && g.VelaUtc == mu.VelaUtc && g.GeneradoUtc == mu.GenUtc && g.Version == ClasicaNdx.VERSION;
            Ok(g7 == 1 && arch2.Cuantas == 1 && arch2.LineasRotas == 1 && ida && !arch2.Buscar(mu.TsUtc, 1, 960, out _) && !arch2.Buscar(mu.TsUtc, 2, 900, out _),
               "G7 el archivo: una linea por sello (la repetida no), ida y vuelta exacta, la linea rota se ignora (y se cuenta), otro marco u otro retraso no la usa: " + MuestrasClasicaArchivo.Linea(g ?? new MuestraClasicaGuardada()));
        }
    }
}
