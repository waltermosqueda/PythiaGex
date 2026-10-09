// Program.cs — arnes replica20 de PythiaGex 4.1.3 (09-10-2026). Salida 0 = todo verde. Resultados en .\resultados\replica20-2026-10-09.txt.
// Que prueba (todo con datos REALES, solo lectura: copias en %TEMP%\pg4_replica20):
//   1. PARIDAD CONTRA LA 2.0, minuto a minuto, las columnas q_* (dominantes QQQ: precio y monto; razon) y n_* (dominantes NDX: precio y monto;
//      base) de laboratorio/calibracion_1009/receta_2_0/paridad_2_0_minuto.csv (los AUDIT de la 2.0 de la sesion 2026-10-09), tolerancia
//      0,02 pt y 1 M. Variantes (para atribuir cada diferencia):
//        A  cadenas de la 4.1 (PythiaGex4\cboe) + fut del AUDIT de la 2.0           = la formula y la conversion con los datos de la 4.1;
//        B  cadenas de la 4.1 + fut de la cinta de la 4.1 en el instante del AUDIT    = todo de la 4.1 (lo pedido);
//        C  cadenas de la 2.0 (PythiaGex\cboe-local) + fut del AUDIT                  = solo la formula (mismos datos que la 2.0);
//        D  lo que la 4.1.3 guarda y dibuja en ese minuto (t = inicio del minuto, fut = cierre de la ultima m2 cerrada) = informativo.
//   2. SIN EFECTOS: el motor con la Replica20 (OpcionesMotorFamilia.Extras) y sin ella, sobre las mismas fuentes: las 21 series, la meta y la
//      linea de niv-*.jsonl (sin las claves nuevas) identicas minuto a minuto; historia y actuales de las 21 identicos.
//   3. PERSISTENCIA: las claves nuevas (R20_*, DOMS_*, "mx") van y vuelven del archivo sin cambiar (relectura = mismo texto).
//   4. COMPLETAR: un archivo de la 4.1.2 (sin extras) se completa en memoria con las mismas R20 que el calculo en vivo.
//   5. CAMBIOS (CambiosFamilia): DOMS_* con cambio neto; R20_* NaN con nota.
//   6. ETIQUETA (ArmadoPantalla) sobre la foto real: "2.0 QQQ D1 +417M 31.083,50 V".
//   4.1.4 (09-10-2026, arreglos de la revision de la 4.1.3):
//   1b. variante E (informativa): las R20 con el ultimo precio del MNQ al empezar el minuto (Replica20.FutR20 con FutDelInstante, el default de la
//       4.1.4: lo que la 4.1.4 guarda) contra la D (el fut del minuto: lo que guardaba la 4.1.3).
//   7. RAZON REHECHA: la Replica20 sobre una cinta SIN el 10-08 (cargando, como al arrancar ATAS) da un respaldo; al cargar el 10-08 el sondeo la
//      rehace (41,4447) y el motor rehace los minutos ya calculados: quedan iguales a un arranque con la cinta completa, y guardados.
//   8. ROTULOS DE TRAMOS (ArmadoPantalla 4.1.4) sobre la historia real de esta noche con las R20 calculadas (hasta las 05:44Z).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PythiaGexCuatro.Cboe;
using PythiaGexCuatro.Familia;
using PythiaGexCuatro.Familia.Ndx;
using PythiaGexCuatro.Familia.Qqq;

namespace PruebaReplica20
{
    /// <summary>IFuenteCboe sobre los archivos cadena-&lt;T&gt;-&lt;dia&gt;.jsonl.gz de UNA carpeta, con el lector de produccion (CboeArchivo.LeerDia):
    /// dedup por sello (gana el generado mas temprano) y orden por generado, como BajadorCboe.Fotos.</summary>
    sealed class FuenteArchivo20 : IFuenteCboe
    {
        private readonly Dictionary<string, FotoCadena[]> _porLibro = new Dictionary<string, FotoCadena[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, long[]> _gen = new Dictionary<string, long[]>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> Lineas = new Dictionary<string, int>();
        public FuenteArchivo20(string carpeta, IEnumerable<string> dias)
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
        static readonly DateTime AHORA = new DateTime(2026, 10, 9, 3, 47, 0, DateTimeKind.Utc);   // un minuto despues del ultimo AUDIT del csv
        // las cadenas de los dos dias previos alcanzan: la razon de la 2.0 de esta sesion sale de la vela del ultimo trade (sus respaldos no se usan si
        // la cinta tiene la vela) y C7/C8 de los minuteros necesitan la rueda del 08-10; menos archivos = menos CPU (la PC esta cargada)
        static readonly string[] DIAS_CBOE = { "2026-10-07", "2026-10-08", "2026-10-09" };

        static int Main(string[] args)
        {
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            Console.OutputEncoding = Encoding.UTF8;
            var d0 = new DirectoryInfo(AppContext.BaseDirectory);
            while (d0 != null && !File.Exists(Path.Combine(d0.FullName, "replica20.csproj"))) d0 = d0.Parent;
            string aqui = d0?.FullName ?? Directory.GetCurrentDirectory();
            string raiz = Path.GetFullPath(Path.Combine(aqui, "..", "..", ".."));                 // ...\PythiaGex
            string app = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS");
            string res = Path.Combine(aqui, "resultados"); Directory.CreateDirectory(res);
            string tmp = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "pg4_replica20");
            var sw = Stopwatch.StartNew();
            using (_out = new StreamWriter(Path.Combine(res, "replica20-" + SESION + ".txt"), false, new UTF8Encoding(false)))
            {
                P("=== arnes replica20 (PythiaGex 4.1.3/4.1.4) " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv) + " — " + Replica20.VERSION + " | " + MotorFamilia.VERSION);
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
        static void Correr(string raiz, string app, string tmp)
        {
            P("== 0. Datos (copias de solo lectura en " + tmp + ")");
            try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch (Exception e) { P("  no pude borrar " + tmp + ": " + e.Message); }
            long bytes = 0; int n = 0;
            foreach (var (orig, dest) in new[] { (Path.Combine(app, "PythiaGex4", "cboe"), Path.Combine(tmp, "cboe4")), (Path.Combine(app, "PythiaGex", "cboe-local"), Path.Combine(tmp, "cboe2")) })
                foreach (var tk in new[] { "NQ", "QQQ" }) foreach (var d in DIAS_CBOE)
                    { var f = Path.Combine(orig, "cadena-" + tk + "-" + d + ".jsonl.gz"); if (File.Exists(f)) { bytes += Copiar(f, Path.Combine(dest, Path.GetFileName(f))); n++; } }
            foreach (var f in Directory.GetFiles(Path.Combine(app, "PythiaGex4", "cinta"), "seg-MNQZ6-2026-10-0?.bin").Where(x => DIAS_CBOE.Any(d => x.EndsWith("-" + d + ".bin", StringComparison.Ordinal)))) { bytes += Copiar(f, Path.Combine(tmp, "cinta", Path.GetFileName(f))); n++; }
            foreach (var f in Directory.GetFiles(Path.Combine(app, "PythiaGex4", "familia"), "muestras-*.jsonl"))
                foreach (var x in new[] { "A", "B", "B2", "C", "R", "R2", "D", "D8" }) { bytes += Copiar(f, Path.Combine(tmp, x, "familia", Path.GetFileName(f))); n++; }
            string csv = Path.Combine(raiz, "laboratorio", "calibracion_1009", "receta_2_0", "paridad_2_0_minuto.csv");
            P("  " + n + " archivos, " + (bytes >> 20) + " MB; csv de la 2.0: " + csv);

            var cinta = new CintaFamilia("MNQZ6", "NQ");
            foreach (var f in Directory.GetFiles(Path.Combine(tmp, "cinta"), "*.bin").OrderBy(x => x, StringComparer.Ordinal))
                P("  cinta " + Path.GetFileName(f) + ": " + (cinta.CargarArchivoDia(f) ? "cargada" : "NO cargada"));
            P("  cinta: ultimo tick " + cinta.UltimoTickUtc.ToString("yyyy-MM-dd HH:mm:ss", Inv) + "Z; " + cinta.Estado);
            var f4 = new FuenteArchivo20(Path.Combine(tmp, "cboe4"), DIAS_CBOE);
            var f2 = new FuenteArchivo20(Path.Combine(tmp, "cboe2"), DIAS_CBOE);
            P("  cadenas 4.1 (PythiaGex4\\cboe): QQQ " + f4.Cuantas("QQQ") + " sellos de " + f4.Lineas["QQQ"] + " lineas, NDX " + f4.Cuantas("NDX") + " de " + f4.Lineas["NDX"]);
            P("  cadenas 2.0 (PythiaGex\\cboe-local): QQQ " + f2.Cuantas("QQQ") + " sellos de " + f2.Lineas["QQQ"] + " lineas, NDX " + f2.Cuantas("NDX") + " de " + f2.Lineas["NDX"]);
            var r4 = new Replica20(f4, cinta, new OpcionesReplica20 { Contrato = "MNQZ6", Log = s => log4.Add(s) });
            var r2 = new Replica20(f2, cinta, new OpcionesReplica20 { Contrato = "MNQZ6" });

            // ---- la vela de la razon (verificacion a mano contra el informe de la receta: 30990,25 / 747,75 = 41,4447)
            var (pAl, deTick) = r4.CierreVela1m(new DateTime(2026, 10, 8, 20, 14, 59, DateTimeKind.Utc));
            P("  vela de 1 min del MNQ que contiene el ultimo trade de QQQ (2026-10-08 20:14:59 UTC = 16:14:59 NY): cierre " + F(pAl) + (deTick ? " (ticks de la cinta)" : " (m2 interpolada)"));
            Ok(pAl == 30990.25 && deTick, "R0 la cinta de la 4.1 da el cierre de la vela de las 20:14 UTC = 30990,25 (el del grafico de la 2.0)", F(pAl));

            Paridad(csv, cinta, r4, r2);
            Motor(tmp, cinta, f4);
            RazonRehecha(tmp, f4);
            RotulosReales(tmp, cinta, f4);
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

        /// <summary>Hora UTC de una linea AUDIT: la fecha del minuto local + la hora local del AUDIT, + 3 h (la PC esta en UTC-3, sin horario de verano).</summary>
        static DateTime Utc(string minLocal, string horaLocal)
        {
            var t = DateTime.ParseExact(minLocal.Substring(0, 10) + " " + horaLocal, "yyyy-MM-dd HH:mm:ss", Inv);
            return DateTime.SpecifyKind(t.AddHours(3), DateTimeKind.Utc);
        }

        sealed class Cmp { public int Iguales, Distintos, SinRes, IgualesConRazon20; public readonly List<string> Ej = new List<string>(); public readonly Dictionary<string, int> Causas = new Dictionary<string, int>(); }

        /// <summary>Diagnostico de un minuto distinto por la razon: la MISMA cadena, el mismo fut y la misma hora, pero con la razon que logueo la 2.0 (csv).
        /// Si da igual, la diferencia es solo la razon (no la formula ni la seleccion).</summary>
        static bool IgualConRazon20(Resultado20 r, Fila f, DateTime t, double fut, out double rzUsada)
        {
            rzUsada = double.NaN;
            if (r?.Foto == null) return false;
            // la razon del csv: la exacta del archivo de la 2.0 si coincidia con el log, si no la del log con 4 decimales; en ese caso tambien la
            // exacta que se despeja de la D1 que dibujo la 2.0 (precio / strike, el strike redondeado a medio dolar)
            double rz4 = D(f["q_razon_usada"]);
            double k1 = Math.Round(D(f["q_d1"]) / rz4 * 2) / 2, rzD1 = D(f["q_d1"]) / k1;
            foreach (var rz in new[] { rz4, rzD1 })
            {
                if (!(rz > 0)) continue;
                var perfil = Nucleo20.Perfil(r.Foto, r.GeneradoUtc, fut / rz, t, rz, true);
                var rr = new Resultado20 { Foto = r.Foto, Conv = rz, Doms = Nucleo20.Dominantes(perfil, fut, out _) };
                if (Comparar(rr, f, "q_", out _)) { rzUsada = rz; return true; }
            }
            return false;
        }

        /// <summary>Las dominantes de un resultado contra las de la 2.0 (precio a 0,02 pt, monto a 1 M). true = iguales.</summary>
        static bool Comparar(Resultado20 r, Fila f, string pre, out string dif)
        {
            var esp = new List<(double P, double M)>();
            for (int i = 1; i <= 2; i++) { var p = D(f[pre + "d" + i]); if (!double.IsNaN(p)) esp.Add((p, D(f[pre + "d" + i + "_M"]))); }
            dif = "";
            if (r == null) { dif = "sin resultado de la 4.1"; return esp.Count == 0; }
            var mio = r.Doms.Select(x => (P: NumPy.RoundPy(x.Fut, 2), M: NumPy.RoundPy(x.G / 1e6, 1), K: x.K)).ToList();
            bool ok = mio.Count == esp.Count;
            for (int i = 0; ok && i < mio.Count; i++)
                if (!(Math.Abs(mio[i].P - esp[i].P) <= 0.02 + 1e-9 && Math.Abs(mio[i].M - esp[i].M) <= 1.0 + 1e-9)) ok = false;
            if (!ok) dif = "4.1 [" + string.Join(" ", mio.Select((x, i) => "D" + (i + 1) + " " + F(x.P) + " " + F(x.M, "0.0") + "M K" + F(x.K, "0.##"))) + "] vs 2.0 ["
                           + string.Join(" ", esp.Select((x, i) => "D" + (i + 1) + " " + F(x.P) + " " + F(x.M, "0") + "M")) + "]";
            return ok;
        }

        static void Paridad(string csv, CintaFamilia cinta, Replica20 r4, Replica20 r2)
        {
            P(); P("== 1. PARIDAD contra la 2.0 (paridad_2_0_minuto.csv, sesion " + SESION + "; tolerancia 0,02 pt y 1 M)");
            var filas = LeerCsv(csv);
            P("  " + filas.Count + " minutos en el csv (AUDIT de la 2.0 de 2026-10-08 22:00 a 2026-10-09 03:46 UTC)");
            var vQ = new Dictionary<string, Cmp> { ["A"] = new Cmp(), ["B"] = new Cmp(), ["C"] = new Cmp(), ["D"] = new Cmp() };
            var vN = new Dictionary<string, Cmp> { ["A"] = new Cmp(), ["B"] = new Cmp(), ["C"] = new Cmp(), ["D"] = new Cmp() };
            int nQ = 0, nN = 0, razonIg = 0, razonDif = 0, baseIg = 0, baseDif = 0;
            int eQ = 0, eN = 0; var dFutE = new List<double>(); var dFutD = new List<double>();      // 4.1.4: variante E
            var ejRazon = new List<string>(); var ejBase = new List<string>();
            var detalleQ = new List<string>(); var detalleN = new List<string>();
            foreach (var f in filas)
            {
                // ---------------- QQQ (primaria de la 2.0)
                if (f["q_d1"] != "")
                {
                    nQ++;
                    var t = Utc(f["min_local"], f["q_hora_local"]);
                    double futA = D(f["q_fut"]), futB = r4.PrecioEn(t);
                    var t0 = new DateTime(t.Ticks - t.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);
                    double futD = cinta.CierreConocido(t0);
                    var ra = r4.Qqq(t, futA); var rb = double.IsNaN(futB) ? null : r4.Qqq(t, futB); var rc = r2.Qqq(t, futA); var rd = double.IsNaN(futD) ? null : r4.Qqq(t0, futD);
                    {   // 4.1.4: variante E (informativa): el ultimo precio del MNQ al empezar el minuto (FutDelInstante) en vez del cierre de la ultima m2
                        double futE = r4.FutR20(t0, double.NaN); var re = double.IsNaN(futE) ? null : r4.Qqq(t0, futE);
                        if (Comparar(re, f, "q_", out _)) eQ++;
                        if (!double.IsNaN(futE)) dFutE.Add(Math.Abs(futE - futA)); if (!double.IsNaN(futD)) dFutD.Add(Math.Abs(futD - futA));
                    }
                    double rz2 = D(f["q_razon_usada"]);
                    if (ra != null && Math.Abs(ra.Conv - rz2) <= 5e-7) razonIg++;
                    else { razonDif++; if (ejRazon.Count < 6) ejRazon.Add(f["min_utc"] + " 4.1 " + F(ra?.Conv ?? double.NaN, "0.000000") + " vs 2.0 " + F(rz2, "0.000000")); }
                    foreach (var (v, r, fut) in new[] { ("A", ra, futA), ("B", rb, futB), ("C", rc, futA), ("D", rd, futD) })
                    {
                        var c = vQ[v];
                        if (Comparar(r, f, "q_", out var dif)) { c.Iguales++; continue; }
                        if (r == null) c.SinRes++; else c.Distintos++;
                        string causa = Causa(r, f, "q_", futA, fut, v);
                        if (causa.StartsWith("razon:", StringComparison.Ordinal) && IgualConRazon20(r, f, v == "D" ? t0 : t, fut, out var rzU))
                        { c.IgualesConRazon20++; causa = "razon (con la razon de la 2.0 da igual)"; dif += " | con la razon de la 2.0 (" + F(rzU, "0.000000") + "): IGUAL (= " + F(rzU * r.Foto.Spot, "0.00") + " / spot " + F(r.Foto.Spot) + "; la vela de las 16:14 NY cierra " + F(r.Razon?.PAl ?? double.NaN) + ")"; }
                        c.Causas[causa] = c.Causas.TryGetValue(causa, out var k) ? k + 1 : 1;
                        if (c.Ej.Count < 40) c.Ej.Add(f["min_utc"] + " [" + causa + "] " + dif + " | " + Contexto(r, f, "q_", fut, futA));
                    }
                    detalleQ.Add(f["min_utc"] + " fut 2.0 " + f["q_fut"] + " cinta " + F(futB) + " | 2.0 D1 " + f["q_d1"] + " " + f["q_d1_M"] + "M D2 " + f["q_d2"] + " " + f["q_d2_M"] + "M razon " + f["q_razon_log"]
                                 + " | 4.1(A) " + (ra == null ? "-" : string.Join(" ", ra.Doms.Select((x, i) => "D" + (i + 1) + " " + F(NumPy.RoundPy(x.Fut, 2)) + " " + F(NumPy.RoundPy(x.G / 1e6, 1), "0.0") + "M")) + " razon " + F(ra.Conv, "0.0000"))
                                 + " | 4.1.3 en el minuto (D) " + (rd == null ? "-" : string.Join(" ", rd.Doms.Select((x, i) => "D" + (i + 1) + " " + F(NumPy.RoundPy(x.Fut, 2)) + " " + F(NumPy.RoundPy(x.G / 1e6, 1), "0.0") + "M"))));
                }
                // ---------------- capa NDX
                if (f["n_d1"] != "")
                {
                    nN++;
                    var t = Utc(f["min_local"], f["n_hora_local"]);
                    double futA = D(f["n_fut"]), futB = r4.PrecioEn(t);
                    var t0 = new DateTime(t.Ticks - t.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);
                    double futD = cinta.CierreConocido(t0);
                    var ra = r4.Ndx(t, futA); var rb = double.IsNaN(futB) ? null : r4.Ndx(t, futB); var rc = r2.Ndx(t, futA); var rd = double.IsNaN(futD) ? null : r4.Ndx(t0, futD);
                    {   // 4.1.4: variante E (informativa)
                        double futE = r4.FutR20(t0, double.NaN); var re = double.IsNaN(futE) ? null : r4.Ndx(t0, futE);
                        if (Comparar(re, f, "n_", out _)) eN++;
                    }
                    double b2 = D(f["n_base"]);
                    if (ra != null && Math.Abs(ra.Conv - b2) <= 0.005 + 1e-9) baseIg++;
                    else { baseDif++; if (ejBase.Count < 6) ejBase.Add(f["min_utc"] + " 4.1 " + F(ra?.Conv ?? double.NaN) + " (" + ra?.OrigenBase + ") vs 2.0 " + f["n_base"] + " (" + f["n_origen"] + ")"); }
                    foreach (var (v, r, fut) in new[] { ("A", ra, futA), ("B", rb, futB), ("C", rc, futA), ("D", rd, futD) })
                    {
                        var c = vN[v];
                        if (Comparar(r, f, "n_", out var dif)) { c.Iguales++; continue; }
                        if (r == null) c.SinRes++; else c.Distintos++;
                        string causa = Causa(r, f, "n_", futA, fut, v);
                        c.Causas[causa] = c.Causas.TryGetValue(causa, out var k) ? k + 1 : 1;
                        if (c.Ej.Count < 40) c.Ej.Add(f["min_utc"] + " [" + causa + "] " + dif + " | " + Contexto(r, f, "n_", fut, futA));
                    }
                    detalleN.Add(f["min_utc"] + " fut 2.0 " + f["n_fut"] + " cinta " + F(futB) + " | 2.0 D1 " + f["n_d1"] + " " + f["n_d1_M"] + "M D2 " + f["n_d2"] + " " + f["n_d2_M"] + "M base " + f["n_base"]
                                 + " | 4.1(A) " + (ra == null ? "-" : string.Join(" ", ra.Doms.Select((x, i) => "D" + (i + 1) + " " + F(NumPy.RoundPy(x.Fut, 2)) + " " + F(NumPy.RoundPy(x.G / 1e6, 1), "0.0") + "M")) + " base " + F(ra.Conv)));
                }
            }
            string[] nombres = { "A cadenas 4.1 + fut del AUDIT", "B cadenas 4.1 + fut de la cinta 4.1 (todo de la 4.1)", "C cadenas de la 2.0 (cboe-local) + fut del AUDIT", "D 4.1.3 en su minuto (fut m2 cerrada; informativo)" };
            P(); P("  QQQ (" + nQ + " minutos con dominantes en la 2.0) — razon igual a la de la 2.0 (5e-7): " + razonIg + ", distinta " + razonDif + (ejRazon.Count > 0 ? ": " + string.Join(" | ", ejRazon) : ""));
            foreach (var v in new[] { "A", "B", "C", "D" })
            {
                var c = vQ[v];
                P("    " + nombres["ABCD".IndexOf(v)].PadRight(58) + " iguales " + c.Iguales.ToString(Inv).PadLeft(4) + " | distintos " + c.Distintos.ToString(Inv).PadLeft(3) + " | sin resultado " + c.SinRes
                  + (c.Causas.Count > 0 ? " | causas: " + string.Join(", ", c.Causas.OrderByDescending(x => x.Value).Select(x => x.Key + " " + x.Value)) : ""));
            }
            {   // 4.1.4: la variante E contra la D (lo que guarda la 4.1.3): el fut del minuto (cierre de la ultima m2 cerrada) o el ultimo precio al empezarlo
                double Med(List<double> l) { if (l.Count == 0) return double.NaN; var o = l.OrderBy(x => x).ToList(); return o[o.Count / 2]; }
                double P90(List<double> l) { if (l.Count == 0) return double.NaN; var o = l.OrderBy(x => x).ToList(); return o[(int)Math.Min(o.Count - 1, Math.Floor(o.Count * 0.9))]; }
                P("    E 4.1.4: fut = ultimo precio del MNQ al empezar el minuto (informativo)  iguales " + eQ.ToString(Inv).PadLeft(4) + " (D: " + vQ["D"].Iguales + ") | |fut - fut de la 2.0| mediana "
                  + F(Med(dFutE)) + " p90 " + F(P90(dFutE)) + " max " + F(dFutE.DefaultIfEmpty(double.NaN).Max()) + " (D: mediana " + F(Med(dFutD)) + " p90 " + F(P90(dFutD)) + " max " + F(dFutD.DefaultIfEmpty(double.NaN).Max()) + ")");
            }
            P(); P("  NDX (" + nN + " minutos con dominantes en la 2.0) — base igual a la de la 2.0 (0,005): " + baseIg + ", distinta " + baseDif + (ejBase.Count > 0 ? ": " + string.Join(" | ", ejBase) : ""));
            foreach (var v in new[] { "A", "B", "C", "D" })
            {
                var c = vN[v];
                P("    " + nombres["ABCD".IndexOf(v)].PadRight(58) + " iguales " + c.Iguales.ToString(Inv).PadLeft(4) + " | distintos " + c.Distintos.ToString(Inv).PadLeft(3) + " | sin resultado " + c.SinRes
                  + (c.Causas.Count > 0 ? " | causas: " + string.Join(", ", c.Causas.OrderByDescending(x => x.Value).Select(x => x.Key + " " + x.Value)) : ""));
            }
            P("    E 4.1.4: fut = ultimo precio del MNQ al empezar el minuto (informativo)  iguales " + eN.ToString(Inv).PadLeft(4) + " (D: " + vN["D"].Iguales + ")");
            foreach (var (lb, d) in new[] { ("QQQ", vQ), ("NDX", vN) })
                foreach (var v in new[] { "A", "B", "C", "D" })
                {
                    if (d[v].Ej.Count == 0) continue;
                    P(); P("  " + lb + " variante " + v + ": cada distinto (minuto UTC [causa] lo de la 4.1 vs lo de la 2.0 | contexto)");
                    foreach (var e in d[v].Ej) P("    " + e);
                }
            P(); P("  detalle QQQ minuto a minuto (2.0 contra 4.1 variante A y lo que dibuja la 4.1.3 en ese minuto):");
            foreach (var x in detalleQ) P("    " + x);
            P(); P("  detalle NDX minuto a minuto:");
            foreach (var x in detalleN) P("    " + x);
            // criterio de verde: la formula (C) y la conversion con los datos de la 4.1 (A) reproducen a la 2.0 en todo minuto explicado
            int noExpQ = vQ["A"].Causas.Where(x => !x.Key.StartsWith("cadena", StringComparison.Ordinal) && !x.Key.StartsWith("razon (con la razon de la 2.0 da igual)", StringComparison.Ordinal)).Sum(x => x.Value);
            int noExpN = vN["A"].Causas.Where(x => !x.Key.StartsWith("cadena", StringComparison.Ordinal)).Sum(x => x.Value);
            P();
            Ok(vQ["C"].Iguales + vQ["C"].IgualesConRazon20 == nQ, "P1 QQQ con las cadenas de la 2.0 (cboe-local) + fut del AUDIT: " + vQ["C"].Iguales + " de " + nQ + " iguales; los otros "
               + vQ["C"].IgualesConRazon20 + " dan iguales con la razon que logueo la 2.0 (la diferencia es solo la razon de esos minutos, no la formula)");
            Ok(vN["C"].Iguales == nN, "P2 NDX con las cadenas de la 2.0 (solo la formula): " + vN["C"].Iguales + " de " + nN + " iguales");
            Ok(noExpQ == 0, "P3 QQQ con las cadenas de la 4.1 + fut del AUDIT (A): " + vQ["A"].Iguales + " de " + nQ + " iguales; los " + (nQ - vQ["A"].Iguales)
               + " distintos se explican (" + vQ["A"].IgualesConRazon20 + " solo por la razon: con la de la 2.0 dan iguales)", noExpQ + " sin explicar");
            Ok(noExpN == 0, "P4 NDX con las cadenas de la 4.1 (A): " + vN["A"].Iguales + " de " + nN + " iguales; los distintos se explican por la cadena", noExpN + " sin explicar");
            Ok(log4.Count > 0, "P5 la replica anota en su log las cadenas que escala (" + log4.Count + " lineas; ultima: " + (log4.LastOrDefault() ?? "-") + ")");
        }
        // ==============================================================================================================================
        /// <summary>7 (4.1.4): la razon de la 2.0 rehecha cuando la cinta completa su historia DESPUES de escalar las cadenas (el arranque de ATAS: la
        /// cinta carga los dias viejos primero y el ultimo al final). Caso de la revision de la 4.1.3: sin el 10-08 la cadena congelada de esta noche
        /// queda con un respaldo (41,6095 'CRUDA sin alinear') en vez de 41,4447 (la vela de 1 min de las 16:14 NY del 10-08 = 30.990,25).</summary>
        static void RazonRehecha(string tmp, IFuenteCboe f4)
        {
            P(); P("== 7. RAZON REHECHA (4.1.4): la cinta completa su historia DESPUES de que la replica escalo las cadenas");
            var dias = Directory.GetFiles(Path.Combine(tmp, "cinta"), "*.bin").OrderBy(x => x, StringComparer.Ordinal).ToList();
            string d0810 = dias.FirstOrDefault(x => x.EndsWith("-2026-10-08.bin", StringComparison.Ordinal));
            if (d0810 == null) { Ok(false, "R0 la copia de la cinta del 10-08 existe"); return; }
            CintaFamilia Parcial() { var c = new CintaFamilia("MNQZ6", "NQ"); foreach (var f in dias) if (f != d0810) c.CargarArchivoDia(f); return c; }
            var t1 = new DateTime(2026, 10, 9, 3, 0, 0, DateTimeKind.Utc);
            // 7a. la replica sola
            {
                var c = Parcial(); var log = new List<string>();
                var r = new Replica20(f4, c, new OpcionesReplica20 { Contrato = "MNQZ6", SondeoCadaS = 0, Log = s => log.Add(s) });
                double fut = c.CierreConocido(t1);
                var q1 = r.Qqq(t1, fut);
                P("  sin el 10-08: " + t1.ToString("HH:mm", Inv) + "Z razon " + F(q1?.Conv ?? double.NaN, "0.000000") + " (" + q1?.Razon?.Origen + "); cadenas provisionales " + r.Provisionales + "; D1 " + (q1 == null || q1.Doms.Count == 0 ? "-" : F(q1.Doms[0].Fut)));
                var q1b = r.Qqq(t1.AddMinutes(1), fut);
                bool igualSinCambio = q1b != null && q1 != null && q1b.Conv == q1.Conv && r.Generacion == 0 && r.Resondeos == 0;
                c.CargarArchivoDia(d0810);                                         // la cinta termina de cargar
                var q2 = r.Qqq(t1.AddMinutes(2), fut);
                P("  con el 10-08 ya cargado: razon " + F(q2?.Conv ?? double.NaN, "0.000000") + " (" + q2?.Razon?.Origen + "); resondeos " + r.Resondeos + ", generacion " + r.Generacion
                  + ", rehacer desde " + r.RehacerDesdeUtc.ToString("MM-dd HH:mm:ss", Inv) + "Z; D1 " + (q2 == null || q2.Doms.Count == 0 ? "-" : F(q2.Doms[0].Fut)));
                foreach (var l in log.Where(l => l.Contains("REHECHA"))) P("    log: " + l);
                Ok(q1 != null && Math.Abs(q1.Conv - 41.44466733533935) > 1e-3 && r.Provisionales > 0 && igualSinCambio,
                   "R1 sin el 10-08 en la cinta la razon de esta noche sale de un respaldo (" + F(q1?.Conv ?? double.NaN, "0.000000") + ", " + q1?.Razon?.Origen + ") y queda marcada para volver a mirarla; sin cinta nueva no cambia");
                Ok(q2 != null && q2.Conv == 41.44466733533935 && r.Resondeos == 1 && r.Generacion == 1 && log.Any(l => l.Contains("REHECHA")) && q2.Doms.Count > 0 && q2.Doms[0].Fut == 750 * 41.44466733533935,
                   "R2 al cargar el 10-08 el sondeo la rehace: razon 41,444667 (la vela de las 16:14 NY), D1 = K750 x razon = " + F(750 * 41.44466733533935) + "; un rehecho, generacion 1, con su linea en el log");
            }
            // 7b. el motor: los minutos ya calculados con el respaldo se rehacen y quedan iguales a un arranque con la cinta completa (motor B de la seccion 2)
            {
                var c = Parcial();
                var r20 = new Replica20(f4, c, new OpcionesReplica20 { Contrato = "MNQZ6", SondeoCadaS = 0 });
                var R = CorrerMotor(Path.Combine(tmp, "R"), c, f4, true, AHORA, r20);
                int iq = CatalogoFamilia.IndiceExtra("R20_QQQ_vol");
                var k300 = TiempoFam.Clave(t1);
                var antes = R.Motor.Almacen.Get(k300)?.Extra?[iq];
                P("  motor R (cinta sin el 10-08): " + R.Motor.Almacen.Cuantos + " minutos; 03:00Z R20_QQQ " + Nv(antes, null) + "; generacion " + r20.Generacion);
                c.CargarArchivoDia(d0810);
                R.Motor.Avanzar(AHORA.AddMinutes(1));                             // el minuto siguiente: el sondeo encuentra la vela y el motor rehace
                var despues = R.Motor.Almacen.Get(k300)?.Extra?[iq];
                P("  despues de cargar el 10-08 y un minuto mas: 03:00Z R20_QQQ " + Nv(despues, null) + "; generacion " + r20.Generacion + ", minutos rehechos " + R.Motor.MinutosRehechos + " (" + R.Motor.Rehechuras + " vez)");
                // contra un arranque con la cinta completa (la de la seccion 0) hasta el mismo minuto
                var cin = new CintaFamilia("MNQZ6", "NQ"); foreach (var f in dias) cin.CargarArchivoDia(f);
                var B = CorrerMotor(Path.Combine(tmp, "D"), cin, f4, true, AHORA.AddMinutes(1));
                // (los minutos de la sesion antes de las 00:00Z no estan en R: sin el dia UTC 10-08 en la cinta no tenian fut y quedaron sin libros en esa
                //  corrida, como a cualquier serie; se comparan los que R calculo)
                int ig = 0, dif = 0; var ej = new List<string>();
                foreach (var rb in B.Motor.Almacen.Todos.Where(x => R.Motor.Almacen.Get(x.Clave) != null).OrderBy(x => x.Clave))
                {
                    var rr = R.Motor.Almacen.Get(rb.Clave);
                    string sb = Nv(rb.Extra?[iq], rb.ExtraStrikes?[iq]) + "|" + string.Join(";", (rb.MetaExtra ?? new MetaLibro[0]).Select(m => m.Libro + F(m.Conv, "0.000000") + m.ConvTexto));
                    string sr = rr == null ? "-" : Nv(rr.Extra?[iq], rr.ExtraStrikes?[iq]) + "|" + string.Join(";", (rr.MetaExtra ?? new MetaLibro[0]).Select(m => m.Libro + F(m.Conv, "0.000000") + m.ConvTexto));
                    if (sb == sr) ig++; else { dif++; if (ej.Count < 3) ej.Add(TiempoFam.DeClave(rb.Clave).ToString("HH:mm", Inv) + " completa " + sb + " | rehecha " + sr); }
                }
                Ok(antes != null && antes.Length > 0 && Math.Abs(antes[0].Precio - 31083.5) > 1 && despues != null && despues.Length > 0 && despues[0].Precio == 31083.5 && R.Motor.MinutosRehechos >= 100 && R.Motor.Rehechuras == 1,
                   "R3 el motor rehace los minutos ya calculados con el respaldo: 03:00Z D1 " + (antes == null || antes.Length == 0 ? "-" : F(antes[0].Precio)) + " -> " + (despues == null || despues.Length == 0 ? "-" : F(despues[0].Precio))
                   + " (la raya de la 2.0); " + R.Motor.MinutosRehechos + " minutos rehechos");
                Ok(dif == 0 && ig >= 100 && ig == R.Motor.Almacen.Cuantos, "R4 rehecho = arranque con la cinta completa: R20_QQQ y su conversion iguales en " + ig + " minutos (todos los de R), distintos " + dif, string.Join(" || ", ej));
                // y quedo guardado: un motor nuevo relee el archivo de R (las lineas rehechas se agregaron al final: gana la ultima)
                Copiar(Directory.GetFiles(Path.Combine(tmp, "R", "familia"), "niv-*.jsonl").Single(), Path.Combine(tmp, "R2", "familia", "niv-" + SESION + "-MNQZ6.jsonl"));
                var R2 = CorrerMotor(Path.Combine(tmp, "R2"), cin, f4, true, AHORA.AddMinutes(1));
                var r2 = R2.Motor.Almacen.Get(k300)?.Extra?[iq];
                int lineas = File.ReadAllLines(Directory.GetFiles(Path.Combine(tmp, "R", "familia"), "niv-*.jsonl").Single()).Length;
                Ok(r2 != null && r2.Length > 0 && r2[0].Precio == 31083.5 && R2.Motor.Almacen.Cargados >= 100 && R2.Motor.MinutosCompletados == 0,
                   "R5 guardado: un motor nuevo relee el archivo (" + lineas + " lineas: los minutos rehechos van otra vez al final) y a las 03:00Z tiene la raya buena (" + (r2 == null || r2.Length == 0 ? "-" : F(r2[0].Precio)) + ")");
            }
        }

        /// <summary>8 (4.1.4): los rotulos de los tramos de historia sobre la historia REAL de esta noche, con las R20 calculadas por la replica (la copia de
        /// niv-*.jsonl del arnes de pantalla no tiene las R20 antes de las 05:37Z: las completaba la 4.1.3 en memoria), hasta las 05:44Z.</summary>
        static void RotulosReales(string tmp, CintaFamilia cinta, IFuenteCboe f4)
        {
            P(); P("== 8. ROTULOS DE TRAMOS (4.1.4) sobre la historia real de esta noche con las R20, hasta las 05:44Z (sin el libro NQ: este arnes no lo carga)");
            var ahora = new DateTime(2026, 10, 9, 5, 45, 0, DateTimeKind.Utc);
            if (!(cinta.UltimoTickUtc >= ahora)) { P("  la copia de la cinta termina a las " + cinta.UltimoTickUtc.ToString("HH:mm", Inv) + "Z: seccion omitida"); return; }
            var Dm = CorrerMotor(Path.Combine(tmp, "D8"), cinta, f4, true, ahora);
            var foto = Dm.Motor.Foto;
            var aj = new AjustesPantalla { Eje = PythiaGexCuatro.EjeFamilia4.Ninguno, PanelAbierto = false };
            foreach (var s in new[] { "MAJORS_NDX_vol", "MAJORS_NQ_oi", "MUROS_NDX_oi", "MUROS_NDX_vol", "MUROS_NQ_oi", "R20_NDX_vol", "R20_QQQ_vol", "T_MUROS_oi", "ZEST_QQQ_vol", "ZTP_NDX_vol" }) aj.Visibles.Add(s);
            var area = new Rectangle(0, 0, 852, 581);
            double alto = 31185, bajo = 30995;
            var v = new VistaPantalla { Area = area, XDerecha = 781, Instrumento = "MNQZ6", PrecioAlto = alto, PrecioBajo = bajo, Y = p => (int)Math.Round(area.Top + (alto - p) / (alto - bajo) * area.Height), PrecioUltimo = 31164.25, AnchoVela = 2 };
            for (int i = 0; i < 325; i++) { var t = ahora.AddMinutes(-325 + i).AddSeconds(59); long ms = TiempoFam.Ms(t); v.Velas.Add((20 + 2 * i, ms / 120000 * 120000)); }
            var d = new DibujoPantalla();
            ArmadoPantalla.Armar(d, foto, new CatalogoPantalla(CatalogoFamilia.Series), aj, v, ahora, (s, t) => new Size((int)Math.Ceiling((s?.Length ?? 0) * t * 0.74), (int)Math.Ceiling(t * 1.45)));
            P("  motor D8: " + Dm.Motor.Almacen.Cuantos + " minutos; tramos de >= 15 velas " + d.TramosLargos + " (vigentes " + d.TramosVigentes + "), rayas " + d.TramosGrupos + ", rotulos " + d.RotulosTramos.Count + ", descartados " + d.TramosDescartados);
            P("  etiquetas de la columna: " + string.Join(" | ", d.Etiquetas.Select(e => e.Txt)));
            foreach (var r in d.RotulosTramos)
                P("    rotulo '" + r.Txt + "' (" + string.Join("+", r.Series) + ", " + r.Tramos + " tramos) raya " + ahora.AddMinutes(-325 + r.VelaIni).ToString("HH:mm", Inv) + "Z -> " + ahora.AddMinutes(-325 + r.VelaFin).ToString("HH:mm", Inv) + "Z (" + r.Largo + " velas)");
            // revision 4.1.4: el tramo del 2.0 NDX de 31.041,83 que termina a la 01:05Z, horas antes que la raya de los muros (05:23Z), lleva SU rotulo en SU
            // final (antes se absorbia en la raya de los muros y su nombre iba al final de esa, 516 px despues); el tramo del 2.0 NDX que termina con los
            // muros va en el rotulo de ellos (final a <= 40 px)
            var r40 = d.RotulosTramos.FirstOrDefault(r => r.Series.Contains("MUROS_NDX_vol") && r.Precio >= 31040 && r.Precio <= 31042.5);
            var r20 = d.RotulosTramos.Where(r => r.Series.Contains("R20_NDX_vol") && r.Precio >= 31040 && r.Precio <= 31042.5).OrderBy(r => r.VelaFin).FirstOrDefault();
            Ok(r40 != null && r40.Txt.Contains("NDX muro V/OI") && r20 != null && r20.Txt.Contains("2.0 NDX") && r20.VelaFin < r40.VelaFin - 100,
               "T1 la doble/triple raya de 31.040/31.041: '" + r40?.Txt + "' al final de los muros (vela " + r40?.VelaFin + ") y el 2.0 NDX (31.041,83), que termino antes, con SU rotulo en su final (vela "
               + r20?.VelaFin + "): '" + r20?.Txt + "'");
            Ok(d.RotulosTramos.Any(r => r.Series.Contains("ZTP_NDX_vol") && r.Precio >= 31063.5 && r.Precio <= 31064.6),
               "T2 la raya roja de 31.064 (NDX cruce) lleva rotulo: " + string.Join(" | ", d.RotulosTramos.Where(r => r.Series.Contains("ZTP_NDX_vol") && r.Precio >= 31063.5 && r.Precio <= 31064.6).Select(r => "'" + r.Txt + "'")));
        }

        static readonly List<string> log4 = new List<string>();

        /// <summary>Por que difiere un minuto: la cadena (otro sello o spot), la razon/base, el fut, o "seleccion" (mismos datos y distinto resultado: no deberia pasar).</summary>
        static string Causa(Resultado20 r, Fila f, string pre, double futAudit, double futUsado, string variante)
        {
            if (r == null) return double.IsNaN(futUsado) ? "sin precio en la cinta" : "sin cadena o sin conversion";
            bool q = pre == "q_";
            if (q)
            {
                string ts2 = f["q_cadena_ts"]; double sp2 = D(f["q_spot_idx"]);
                string ts1 = r.Foto.TsUtc.ToString("yyyy-MM-dd HH:mm:ss", Inv);
                bool mismaCad = ts1 == ts2;
                if (!mismaCad && Math.Abs(r.Foto.Spot - sp2) > 1e-9) return "cadena: otra bajada con otro spot";
                if (!mismaCad) return "cadena: otro sello (mismo spot)";
                double rz2 = D(f["q_razon_usada"]);
                if (Math.Abs(r.Conv - rz2) > 5e-7) return "razon: " + (r.Razon?.Origen ?? "");
            }
            else
            {
                string ts2 = f["n_cadenaTs"].Replace('_', ' ');
                string ts1 = r.Foto.TsUtc.ToString("yyyy-MM-dd HH:mm:ss", Inv);
                if (ts1 != ts2) return "cadena: otro sello";
                if (Math.Abs(r.Conv - D(f["n_base"])) > 0.005 + 1e-9) return "base: " + r.OrigenBase;
            }
            if (!double.IsNaN(futUsado) && Math.Abs(futUsado - futAudit) > 1e-9) return "fut distinto del de la 2.0";
            if (variante == "D") return "minuto de la 4.1.3 (otra hora y otro fut)";
            return "seleccion (mismos datos)";
        }

        static string Contexto(Resultado20 r, Fila f, string pre, double fut, double futAudit)
        {
            if (r == null) return "fut " + F(fut);
            bool q = pre == "q_";
            return "fut usado " + F(fut) + " (2.0 " + F(futAudit) + ") | cadena 4.1 gen " + r.GeneradoUtc.ToString("HH:mm:ss", Inv) + " sello " + r.Foto.TsUtc.ToString("HH:mm:ss", Inv) + " spot " + F(r.Foto.Spot)
                   + (q ? " razon " + F(r.Conv, "0.000000") + " | 2.0 gen " + f["q_cadena_gen"] + " sello " + f["q_cadena_ts"] + " spot " + f["q_spot_idx"] + " razon " + f["q_razon_usada"]
                        : " base " + F(r.Conv) + " (" + r.OrigenBase + ") | 2.0 gen " + f["n_gen"] + " sello " + f["n_cadenaTs"] + " base " + f["n_base"] + " (" + f["n_origen"] + ")");
        }

        // ==============================================================================================================================
        sealed class Corrida { public MotorFamilia Motor; public LibroMinuteroNdx Ndx; public MinuteroQqq Qqq; public Replica20 R20; public double Seg; }

        static Corrida CorrerMotor(string carpeta, CintaFamilia cinta, IFuenteCboe fuente, bool extras, DateTime? ahora = null, Replica20 r20Dada = null)
        {
            var sw = Stopwatch.StartNew();
            var nd = new LibroMinuteroNdx(fuente, cinta, new OpcionesNdx { ParidadPython = false, ContratoGrafico = "MNQZ6", CarpetaDatos = carpeta, Log = s => { } });
            var mq = new MinuteroQqq(fuente, cinta, new OpcionesQqq { Corregida = true, ContratoDe = OpcionesQqq.ContratoFijo("Z6") });
            // como HostFamilia.SembrarMuestrasQqq: las dos ultimas sesiones de muestras de C8
            var arch = Directory.GetFiles(Path.Combine(carpeta, "familia"), "muestras-QQQ-*.jsonl").OrderBy(x => x, StringComparer.Ordinal).ToList();
            foreach (var a in arch.Skip(Math.Max(0, arch.Count - 2)))
                foreach (var l in File.ReadAllLines(a))
                    if (ConversionQqq.LeerLineaMuestra(l, out var ts, out var p, out var k)) mq.Conversion.SembrarPrecio(ts, p, k);
            var r20 = r20Dada ?? (extras ? new Replica20(fuente, cinta, new OpcionesReplica20 { Contrato = "MNQZ6" }) : null);
            var m = new MotorFamilia(new OpcionesMotorFamilia
            {
                Carpeta = carpeta, Corregida = true, FuentesListas = () => true, EsperaArranqueS = 0, EsperaCierreS = 0,
                RutaLog = Path.Combine(carpeta, "pythiagex4-familia.log"), Extras = r20
            });
            m.Configurar(cinta, null, fuente, new ILibroMinutero[] { nd, mq }, null, "MNQZ6");
            for (int i = 0; i < 400; i++) { m.Avanzar(ahora ?? AHORA); if (!m.Pendiente) break; }
            return new Corrida { Motor = m, Ndx = nd, Qqq = mq, R20 = r20, Seg = sw.Elapsed.TotalSeconds };
        }

        /// <summary>La linea de niv-*.jsonl de un minuto SIN las series extra (como la escribia la 4.1.2).</summary>
        static string LineaSinExtras(RegistroMinuto r) => AlmacenNiveles.Linea(new RegistroMinuto
        {
            Clave = r.Clave, Libros = r.Libros, Series = r.Series, Strikes = r.Strikes, Meta = r.Meta, FutMnq = r.FutMnq
        });

        static string Nv(Nivel[] lv, double[] ks) => lv == null ? "-" : string.Join(" ", lv.Select((x, i) => x.Etq + " " + F(x.Precio) + " " + F(x.GexM, "0.0") + "M K" + F(ks != null && i < ks.Length ? ks[i] : double.NaN, "0.##")));

        static void Motor(string tmp, CintaFamilia cinta, IFuenteCboe f4)
        {
            P(); P("== 2. SIN EFECTOS: motor sin extras (A) y con la Replica20 (B), mismas fuentes (NDX + QQQ de la 4.1), sesion " + SESION + " hasta " + AHORA.ToString("HH:mm", Inv) + " UTC");
            var A = CorrerMotor(Path.Combine(tmp, "A"), cinta, f4, false);
            var B = CorrerMotor(Path.Combine(tmp, "B"), cinta, f4, true);
            P("  A: " + A.Motor.Almacen.Cuantos + " minutos con libros en " + A.Seg.ToString("0.0", Inv) + " s | B: " + B.Motor.Almacen.Cuantos + " en " + B.Seg.ToString("0.0", Inv) + " s");
            int ig = 0, dif = 0, conExtra = 0; var ej = new List<string>();
            var claves = A.Motor.Almacen.Todos.Select(r => r.Clave).Union(B.Motor.Almacen.Todos.Select(r => r.Clave)).OrderBy(x => x).ToList();
            foreach (var k in claves)
            {
                var ra = A.Motor.Almacen.Get(k); var rb = B.Motor.Almacen.Get(k);
                if (ra != null && rb != null && AlmacenNiveles.Linea(ra) == LineaSinExtras(rb)) ig++;
                else { dif++; if (ej.Count < 5) ej.Add(k + " A " + (ra == null ? "-" : AlmacenNiveles.Linea(ra).Substring(0, 120)) + " | B " + (rb == null ? "-" : LineaSinExtras(rb).Substring(0, 120))); }
                if (rb != null && rb.TieneExtra) conExtra++;
            }
            Ok(dif == 0 && ig > 300, "S1 las 21 series, la meta y la linea de niv (sin las claves nuevas) identicas con y sin extras: " + ig + " minutos iguales, " + dif + " distintos", string.Join(" || ", ej));
            // la foto: historia y actuales de las 21 series
            var fa = A.Motor.Foto; var fb = B.Motor.Foto;
            int hIg = 0, hDif = 0;
            foreach (var T in fa.HistoriaM2.Keys.Union(fb.HistoriaM2.Keys))
            {
                fa.HistoriaM2.TryGetValue(T, out var ea); fb.HistoriaM2.TryGetValue(T, out var eb);
                foreach (var sid in CatalogoFamilia.Calc)
                {
                    double[] xa = null, xb = null; bool ha = ea != null && ea.TryGetValue(sid, out xa), hb = eb != null && eb.TryGetValue(sid, out xb);
                    if (!ha && !hb) continue;
                    if (ha && hb && xa.SequenceEqual(xb)) hIg++; else hDif++;
                }
            }
            string Act(NivelActual a) => a.Serie + " " + a.Rol + " " + F(a.Precio) + " " + F(a.Strike) + " " + F(a.GexM) + " " + a.DatoUtc.ToString("o", Inv) + " " + a.OiViejo;
            var aa = fa.Actuales.Where(a => CatalogoFamilia.IndiceExtra(a.Serie) < 0).Select(Act).ToList();
            var ab = fb.Actuales.Where(a => CatalogoFamilia.IndiceExtra(a.Serie) < 0).Select(Act).ToList();
            var fua = fa.Fuentes.Select(x => x.Libro + "|" + x.Texto).ToList();
            var fub = fb.Fuentes.Where(x => !x.Libro.StartsWith("2.0 ", StringComparison.Ordinal)).Select(x => x.Libro + "|" + x.Texto).ToList();
            Ok(hDif == 0 && hIg > 0, "S2 historia de la foto (21 series por vela m2) identica: " + hIg + " iguales, " + hDif + " distintas");
            Ok(aa.SequenceEqual(ab) && aa.Count > 0, "S3 actuales de las 21 series identicos (" + aa.Count + ") y en el mismo orden; con extras se agregan " + (fb.Actuales.Count - ab.Count) + " (R20/DOMS)");
            Ok(fua.SequenceEqual(fub), "S4 fuentes de los libros identicas; con extras se agregan " + (fb.Fuentes.Count - fub.Count) + " (" + string.Join(" | ", fb.Fuentes.Where(x => x.Libro.StartsWith("2.0 ", StringComparison.Ordinal)).Select(x => x.Libro + ": " + x.Texto)) + ")");
            Ok(conExtra > 300, "S5 la Replica20 dio series extra en " + conExtra + " de " + B.Motor.Almacen.Cuantos + " minutos");

            // ---- una muestra de lo que guarda la 4.1.3 (para leer a mano)
            P();
            P("  muestra de minutos de B (R20_QQQ | R20_NDX | DOMS_QQQ | DOMS_NDX):");
            foreach (var k in claves.Where(k => B.Motor.Almacen.Get(k)?.TieneExtra == true).Where((k, i) => i % 30 == 0 || TiempoFam.DeClave(k).ToString("HH:mm", Inv) == "03:00"))
            {
                var r = B.Motor.Almacen.Get(k);
                int iq = CatalogoFamilia.IndiceExtra("R20_QQQ_vol"), inx = CatalogoFamilia.IndiceExtra("R20_NDX_vol"), dq = CatalogoFamilia.IndiceExtra("DOMS_QQQ_vol"), dn = CatalogoFamilia.IndiceExtra("DOMS_NDX_vol");
                P("    " + TiempoFam.DeClave(k).ToString("HH:mm", Inv) + "Z fut " + F(r.FutMnq) + " | " + Nv(r.Extra[iq], r.ExtraStrikes[iq]) + " | " + Nv(r.Extra[inx], r.ExtraStrikes[inx]) + " | " + Nv(r.Extra[dq], r.ExtraStrikes[dq]) + " | " + Nv(r.Extra[dn], r.ExtraStrikes[dn]));
            }
            {   // los numeros del informe de calibracion: 01:41-03:24 UTC la 2.0 dibuja D1 31.083,50 (K750) y D2 31.042,06 (K749); la 4.1 tiene K750 en 31.076,37
                var r = B.Motor.Almacen.Get(TiempoFam.Clave(new DateTime(2026, 10, 9, 3, 0, 0, DateTimeKind.Utc)));
                var q = r?.Extra?[CatalogoFamilia.IndiceExtra("R20_QQQ_vol")]; var dq = r?.Extra?[CatalogoFamilia.IndiceExtra("DOMS_QQQ_vol")];
                var kq = r?.ExtraStrikes?[CatalogoFamilia.IndiceExtra("DOMS_QQQ_vol")];
                Ok(q != null && q.Length == 2 && q[0].Precio == 31083.5 && q[0].Etq == "D1" && q[1].Precio == 31042.06 && q[0].GexM > 400 && q[0].GexM < 440,
                   "S6 03:00 UTC: 2.0 QQQ D1 31.083,50 (K750, " + (q == null ? "-" : F(q[0].GexM, "0.0")) + "M) y D2 31.042,06 (K749), como la raya de la 2.0", Nv(q, null));
                Ok(dq != null && dq.Length == 2 && kq[0] == 750 && Math.Abs(dq[0].Precio - 31076.37) <= 0.5,
                   "S7 03:00 UTC: QQQ dom D1 = el mismo strike 750 con la razon sincronizada (" + (dq == null ? "-" : F(dq[0].Precio)) + "; informe de calibracion 31.076,37) y D2 K" + (kq == null || kq.Length < 2 ? "-" : F(kq[1], "0.##")), Nv(dq, kq));
            }

            // ---- 3. persistencia
            P(); P("== 3. PERSISTENCIA de las claves nuevas: el motor B2 (con extras) relee el archivo de B");
            Copiar(Directory.GetFiles(Path.Combine(tmp, "B", "familia"), "niv-*.jsonl").Single(), Path.Combine(tmp, "B2", "familia", "niv-" + SESION + "-MNQZ6.jsonl"));
            var lineaB = File.ReadAllLines(Directory.GetFiles(Path.Combine(tmp, "B", "familia"), "niv-*.jsonl").Single());
            int conR20 = lineaB.Count(l => l.Contains("\"R20_QQQ_vol\"")), conMx = lineaB.Count(l => l.Contains("\"mx\":[")), conDoms = lineaB.Count(l => l.Contains("\"DOMS_QQQ_vol\""));
            P("  niv de B: " + lineaB.Length + " lineas; con R20_QQQ_vol " + conR20 + ", con DOMS_QQQ_vol " + conDoms + ", con \"mx\" " + conMx);
            P("  ej.: " + (lineaB.FirstOrDefault(l => l.Contains("\"mx\":[")) is string ejl ? ejl.Substring(Math.Max(0, ejl.IndexOf("\"R20_QQQ_vol\"", StringComparison.Ordinal))) : "-"));
            var B2 = CorrerMotor(Path.Combine(tmp, "B2"), cinta, f4, true);
            int pIg = 0, pDif = 0; var pej = new List<string>();
            foreach (var k in claves)
            {
                var rb = B.Motor.Almacen.Get(k); var r2 = B2.Motor.Almacen.Get(k);
                if (rb == null && r2 == null) continue;
                if (rb != null && r2 != null && AlmacenNiveles.Linea(rb) == AlmacenNiveles.Linea(r2)) pIg++;
                else { pDif++; if (pej.Count < 4) pej.Add(k.ToString(Inv)); }
            }
            Ok(conR20 > 300 && conMx > 300 && pDif == 0 && B2.Motor.Almacen.Cargados > 300 && B2.Motor.MinutosCompletados == 0,
               "P6 releido (" + B2.Motor.Almacen.Cargados + " minutos del archivo, " + B2.Motor.MinutosCompletados + " completados): " + pIg + " lineas iguales, " + pDif + " distintas", string.Join(",", pej));

            // ---- 4. completar un archivo de la 4.1.2
            P(); P("== 4. COMPLETAR: el motor C (con extras) abre el archivo de A (4.1.2, sin extras)");
            Copiar(Directory.GetFiles(Path.Combine(tmp, "A", "familia"), "niv-*.jsonl").Single(), Path.Combine(tmp, "C", "familia", "niv-" + SESION + "-MNQZ6.jsonl"));
            var C = CorrerMotor(Path.Combine(tmp, "C"), cinta, f4, true);
            int cIg = 0, cDif = 0, cDomsNull = 0; var cej = new List<string>();
            int i20 = CatalogoFamilia.IndiceExtra("R20_QQQ_vol"), n20 = CatalogoFamilia.IndiceExtra("R20_NDX_vol");
            foreach (var k in claves)
            {
                var rb = B.Motor.Almacen.Get(k); var rc = C.Motor.Almacen.Get(k);
                if (rb == null || rc == null) continue;
                string sb = Nv(rb.Extra?[i20], rb.ExtraStrikes?[i20]) + "|" + Nv(rb.Extra?[n20], rb.ExtraStrikes?[n20]) + "|" + string.Join(";", (rb.MetaExtra ?? new MetaLibro[0]).Select(m => m.Libro + F(m.Conv, "0.000000") + m.ConvTexto));
                string sc = Nv(rc.Extra?[i20], rc.ExtraStrikes?[i20]) + "|" + Nv(rc.Extra?[n20], rc.ExtraStrikes?[n20]) + "|" + string.Join(";", (rc.MetaExtra ?? new MetaLibro[0]).Select(m => m.Libro + F(m.Conv, "0.000000") + m.ConvTexto));
                if (sb == sc) cIg++; else { cDif++; if (cej.Count < 4) cej.Add(TiempoFam.DeClave(k).ToString("HH:mm", Inv) + " B " + sb + " C " + sc); }
                if (rc.Extra == null || (rc.Extra[CatalogoFamilia.IndiceExtra("DOMS_QQQ_vol")] == null && rc.Extra[CatalogoFamilia.IndiceExtra("DOMS_NDX_vol")] == null)) cDomsNull++;
            }
            Ok(C.Motor.MinutosCompletados > 300 && cDif == 0 && cIg > 300,
               "C1 " + C.Motor.MinutosCompletados + " minutos de la 4.1.2 completados en memoria: R20 iguales al calculo en vivo en " + cIg + ", distintos " + cDif + " (DOMS_* quedan vacias: " + cDomsNull + ")", string.Join(" || ", cej));
            var lineaC = File.ReadAllLines(Directory.GetFiles(Path.Combine(tmp, "C", "familia"), "niv-*.jsonl").Single());
            var lineaA = File.ReadAllLines(Directory.GetFiles(Path.Combine(tmp, "A", "familia"), "niv-*.jsonl").Single());
            Ok(lineaC.SequenceEqual(lineaA), "C2 el archivo de la 4.1.2 no se toca al completar (" + lineaA.Length + " lineas iguales)");

            // ---- 5. cambios por nivel
            P(); P("== 5. CAMBIOS por nivel (CambiosFamilia sobre la foto de B)");
            var cam = new CambiosFamilia(null, f4, new OpcionesCambios { Corregida = true, RutaLog = Path.Combine(tmp, "cambios.log") });
            var fan = cam.Anotar(B.Motor.Foto, B.Motor.Almacen.Ultimo, null);
            foreach (var a in fan.Actuales.Where(a => CatalogoFamilia.IndiceExtra(a.Serie) >= 0))
                P("    " + a.Serie.PadRight(13) + " " + a.Rol + " " + F(a.Precio) + " K " + F(a.Strike, "0.##") + " " + F(a.GexM, "0.0") + "M | vol 15 min " + F(a.CambioVolM?[1] ?? double.NaN, "0.0") + " | nota: " + a.CambioNota);
            var r20s = fan.Actuales.Where(a => a.Serie.StartsWith("R20_", StringComparison.Ordinal)).ToList();
            var doms = fan.Actuales.Where(a => a.Serie.StartsWith("DOMS_", StringComparison.Ordinal)).ToList();
            Ok(r20s.Count > 0 && r20s.All(a => a.CambioVolM.All(double.IsNaN) && double.IsNaN(a.CambioOiDiaM) && a.CambioNota == CambiosFamilia.NOTA_REPLICA20),
               "K1 R20_*: sin cambio (NaN) con la nota '" + CambiosFamilia.NOTA_REPLICA20 + "'");
            Ok(doms.Count > 0 && doms.All(a => a.CambioVolM.All(x => !double.IsNaN(x))),
               "K2 DOMS_*: cambio neto por volumen calculado en las 3 ventanas (de noche, cadena congelada: " + string.Join(" ", doms.Select(a => F(a.CambioVolM[1], "0.0"))) + "; nota '" + doms[0].CambioNota + "')");

            // ---- 6. la etiqueta sobre la foto real
            P(); P("== 6. ETIQUETA (ArmadoPantalla) sobre la foto real de B anotada");
            var aj = new AjustesPantalla();
            // 4.1.4: QQQ majors OI, QQQ muros OI, FAM muros OI y QQQ dom ya no son defaults: se prenden a mano las de la 4.1.3 (lo que esta seccion mira)
            foreach (var s in new[] { "MAJORS_QQQ_oi", "MUROS_NQ_oi", "MUROS_NDX_vol", "MUROS_QQQ_oi", "FAM_MUROS_oi", "ZEST_QQQ_vol", "TRES_NDX", "T_MUROS_oi", "R20_QQQ_vol", "R20_NDX_vol", "DOMS_QQQ_vol" })
                aj.Visibles.Add(s);
            aj.Visibles.Add("DOMS_NDX_vol"); aj.PanelAbierto = true;
            var area = new Rectangle(0, 0, 4000, 800);
            double alto = 31200, bajo = 30900;
            var v = new VistaPantalla { Area = area, XDerecha = 4000, Instrumento = "MNQZ6", PrecioAlto = alto, PrecioBajo = bajo, Y = p => (int)Math.Round(area.Top + (alto - p) / (alto - bajo) * area.Height), PrecioUltimo = 31076.5 };
            var d = new DibujoPantalla();
            ArmadoPantalla.Armar(d, fan, new CatalogoPantalla(CatalogoFamilia.Series), aj, v, AHORA, (s, t) => new Size((int)Math.Ceiling((s?.Length ?? 0) * t * 0.74), (int)Math.Ceiling(t * 1.45)));
            foreach (var e in d.Rotulos.Where(r => CatalogoFamilia.IndiceExtra(r.Serie) >= 0)) P("    etiqueta " + e.Serie.PadRight(13) + " '" + e.Txt + "'");
            foreach (var l in d.Panel.Where(l => l.Contains("2.0") || l.Contains(" dom ") || l.Contains("dom D"))) P("    pestaña: " + l);
            Ok(d.Rotulos.Any(r => r.Serie == "R20_QQQ_vol" && r.Txt.StartsWith("2.0 QQQ D1 +", StringComparison.Ordinal) && r.Txt.EndsWith(" 31.083,50 V", StringComparison.Ordinal)),
               "E1 la etiqueta de la 2.0 QQQ D1 con el formato vigente: '" + (d.Rotulos.FirstOrDefault(r => r.Serie == "R20_QQQ_vol")?.Txt ?? "-") + "'");
            Ok(d.Panel.Any(l => l.Contains("2.0 QQQ (replica, CBOE)") && l.Contains("razon 2.0 41.4447")), "E2 la pestaña dice la conversion de la 2.0 (razon 2.0 = MNQ del ultimo trade / QQQ spot)");
            Ok(d.Panel.Any(l => l.Contains("QQQ dom D1·vol") && l.Contains("razon sincronizada")), "E3 el detalle de QQQ dom dice 'razon sincronizada'");
        }
    }
}
