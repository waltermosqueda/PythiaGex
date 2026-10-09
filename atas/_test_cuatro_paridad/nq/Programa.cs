// Programa.cs — arnes de PARIDAD del libro NQ de la Familia (B3a, 08-10-2026). Sin ATAS, prioridad BelowNormal.
// Entrada: los viva3 archivados de la 3.0 (%APPDATA%/ATAS/PythiaGex3/viva) y la cinta archivada (profundidad/estado/cinta), las MISMAS que uso
// la referencia. Corre MinuteroNq minuto a minuto sobre la sesion entera (22:00 UTC .. 20:59 UTC) y compara las 7 series visibles de NQ con
// atas/_test_cuatro_paridad/referencia/ref-<dia>.json (preview_niveles.Motor(vivo=False).historico(): niv[minuto][serie] = [[p, etq, gex]]).
// Criterio: misma presencia por minuto, mismas etiquetas en el mismo orden, precio a <= 0,01, monto igual (redondeado a 0,1 como Python).
// Uso: dotnet bin/Release/ParidadNq.dll [dia ...] [--vivo] [--ejemplos N] [--corr-sesion]
//   --vivo        simula el vivo: el libro solo ve las fotos con ts <= t y el reloj de C2 es t (la referencia es historica: debe dar igual
//                 en los dias con salto de OI).
//   --corr-sesion corrimiento acumulado en toda la sesion (en vez de por tanda de 240, como Python).
//   --zero-cada-minuto  sin cache del zero (lo que hacia la vista previa EN VIVO): mide cuanto se aparta de la referencia historica.
//   --autoprueba  verifica NumFam (round de Python, np.sum por pares, mediana y robusta) contra casos_num.txt (generar_casos_num.py)
//                 y las ramas de C2 (OiNq) que el 07/08-10 no ejercitan (sin salto, esperando, primera >= 01:55, lunes, posferiado).
// Compilar: dotnet build -c Release -o bin/Release (aca). El codigo comun solo: comun_solo/ComunSolo.csproj.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using PythiaGexCuatro.Familia;

namespace ParidadNq
{
    public static class Programa
    {
        static readonly CultureInfo INV = CultureInfo.InvariantCulture;
        static string Raiz = @"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex";

        public static int Main(string[] args)
        {
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            var dias = args.Where(a => !a.StartsWith("--") && a.Length == 10 && a[4] == '-').ToList();
            if (dias.Count == 0) dias = new List<string> { "2026-10-07", "2026-10-08" };
            bool vivo = args.Contains("--vivo");
            bool corrSesion = args.Contains("--corr-sesion");
            bool zeroCadaMin = args.Contains("--zero-cada-minuto");
            if (args.Contains("--autoprueba")) return Autoprueba();
            int ejemplos = 6;
            int ie = Array.IndexOf(args, "--ejemplos");
            if (ie >= 0 && ie + 1 < args.Length) int.TryParse(args[ie + 1], out ejemplos);
            string app = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string carpViva = Path.Combine(app, "ATAS", "PythiaGex3", "viva");
            string carpCinta = Path.Combine(Raiz, "profundidad", "estado", "cinta");
            string carpRef = Path.Combine(Raiz, "atas", "_test_cuatro_paridad", "referencia");
            int fallas = 0;
            foreach (var dia in dias) fallas += Una(dia, carpViva, carpCinta, carpRef, vivo, corrSesion, zeroCadaMin, ejemplos);
            Console.WriteLine();
            Console.WriteLine(fallas == 0 ? "RESULTADO: PARIDAD EXACTA en las series de NQ" : "RESULTADO: " + fallas + " diferencias (ver arriba)");
            return fallas == 0 ? 0 : 1;
        }

        /// <summary>NumFam contra Python/numpy al bit (casos_num.txt).</summary>
        static int Autoprueba()
        {
            var ruta = Path.Combine(AppContext.BaseDirectory, "..", "..", "casos_num.txt");
            if (!File.Exists(ruta)) ruta = "casos_num.txt";
            int nR = 0, fR = 0, nS = 0, fS = 0, nM = 0, fM = 0;
            foreach (var l in File.ReadLines(ruta))
            {
                var a = l.Split(' ');
                if (a[0] == "R")
                {
                    nR++;
                    double x = double.Parse(a[1], INV), e = double.Parse(a[3], INV); int n = int.Parse(a[2], INV);
                    double r = NumFam.PyRound(x, n);
                    if (BitConverter.DoubleToInt64Bits(r) != BitConverter.DoubleToInt64Bits(e) && !(r == 0 && e == 0)) { fR++; if (fR <= 5) Console.WriteLine("  round(" + a[1] + ", " + n + "): python " + a[3] + " mio " + r.ToString("R", INV)); }
                }
                else if (a[0] == "S")
                {
                    nS++;
                    double e = double.Parse(a[1], INV);
                    var v = a.Skip(2).Select(x => double.Parse(x, INV)).ToArray();
                    double r = NumFam.SumaNumpy(v);
                    if (r != e) { fS++; if (fS <= 5) Console.WriteLine("  sum n=" + v.Length + ": numpy " + a[1] + " mio " + r.ToString("R", INV)); }
                }
                else if (a[0] == "M")
                {
                    nM++;
                    double med = double.Parse(a[1], INV), piso = double.Parse(a[2], INV), rv = double.Parse(a[3], INV); int rn = int.Parse(a[4], INV);
                    var v = a.Skip(5).Select(x => double.Parse(x, INV)).ToArray();
                    var (mv, mk) = NumFam.Robusta(v, piso);
                    if (NumFam.Mediana(v) != med || mv != rv || mk != rn) { fM++; if (fM <= 5) Console.WriteLine("  robusta: python " + rv + "/" + rn + " mio " + mv + "/" + mk); }
                }
            }
            Console.WriteLine(string.Format(INV, "autoprueba NumFam: round {0}/{1} iguales al bit, np.sum {2}/{3}, mediana+robusta {4}/{5}", nR - fR, nR, nS - fS, nS, nM - fM, nM));
            // dias de viva3: 5 decimales -> round 4 (comparar con Math.Round de .NET para dejar constancia de por que no se usa)
            int dif = 0;
            foreach (var l in File.ReadLines(ruta))
            {
                var a = l.Split(' ');
                if (a[0] != "R" || a[2] != "4") continue;
                double x = double.Parse(a[1], INV), e = double.Parse(a[3], INV);
                if (Math.Round(x, 4, MidpointRounding.ToEven) != e) dif++;
            }
            Console.WriteLine("  (Math.Round(x, 4) de .NET difiere de Python en " + dif + " de esos casos: por eso PyRound)");
            int fO = PruebaOi();
            return fR + fS + fM + fO == 0 ? 0 : 1;
        }

        /// <summary>C2 (OiNq) en las ramas que el 07/08-10 no ejercitan (los dos dias tuvieron salto): fotos sinteticas.</summary>
        static int PruebaOi()
        {
            int f = 0;
            FotoCadena Foto(string ts, double oiBase)
            {
                var t = DateTime.SpecifyKind(DateTime.ParseExact(ts, "yyyy-MM-dd HH:mm:ss", INV), DateTimeKind.Utc);
                var filas = new List<FilaViva>();
                for (int i = 0; i < 25; i++) { filas.Add(new FilaViva(31000 + 10 * i, 0.8, true, oiBase + i, 0.15, 1)); filas.Add(new FilaViva(31000 + 10 * i, 0.8, false, oiBase + 2 * i, 0.15, 1)); }
                return FotoNq.Armar(t, 31100, filas);
            }
            DateTime U(string s) => DateTime.SpecifyKind(DateTime.ParseExact(s, "yyyy-MM-dd HH:mm", INV), DateTimeKind.Utc);
            void Chequear(string caso, bool cond) { Console.WriteLine("  C2 " + (cond ? "OK   " : "FALLA") + " " + caso); if (!cond) f++; }
            // 1) salto: miercoles 2026-10-07, OI cambia a las 01:30
            var fs = new List<FotoCadena> { Foto("2026-10-06 22:01:00", 10), Foto("2026-10-07 00:30:00", 10), Foto("2026-10-07 01:30:10", 50), Foto("2026-10-07 02:30:00", 50) };
            var o = new OiNq("2026-10-07"); o.Avanzar(fs);
            var h = o.Hasta(U("2026-10-07 01:31"));
            Chequear("salto medido a las 01:30:10 -> " + h.Como, h.Hasta == U("2026-10-07 01:30").AddSeconds(10) && !o.OiOk(U("2026-10-07 01:30"), U("2026-10-07 05:00")) && o.OiOk(U("2026-10-07 01:31"), U("2026-10-07 05:00")));
            // 2) sin salto, ahora despues de las 03:30: suprimido hasta las 02:00
            fs = new List<FotoCadena> { Foto("2026-10-06 22:01:00", 10), Foto("2026-10-07 00:30:00", 10), Foto("2026-10-07 03:00:00", 10) };
            o = new OiNq("2026-10-07"); o.Avanzar(fs); h = o.Hasta(U("2026-10-07 04:00"));
            Chequear("sin salto, ahora 04:00 -> " + h.Como, h.Hasta == U("2026-10-07 02:00") && !o.OiOk(U("2026-10-07 01:59"), U("2026-10-07 04:00")) && o.OiOk(U("2026-10-07 02:00"), U("2026-10-07 04:00")));
            // 3) sin salto todavia, ahora 01:00: esperando hasta las 03:30
            h = o.Hasta(U("2026-10-07 01:00"));
            Chequear("sin salto, ahora 01:00 -> " + h.Como, h.Hasta == U("2026-10-07 03:30") && !o.OiOk(U("2026-10-07 02:30"), U("2026-10-07 01:00")));
            // 4) primera foto de la noche a las 02:10: OI supuesto actualizado
            fs = new List<FotoCadena> { Foto("2026-10-07 02:10:00", 10), Foto("2026-10-07 02:11:00", 10) };
            o = new OiNq("2026-10-07"); o.Avanzar(fs); h = o.Hasta(U("2026-10-07 04:00"));
            Chequear("primera 02:10 -> " + h.Como, h.Hasta == null);
            // 5) lunes (sesion 2026-10-05) y posferiado (2026-09-08, el dia despues de Labor Day): sin supresion
            o = new OiNq("2026-10-05"); h = o.Hasta(U("2026-10-05 04:00"));
            Chequear("lunes 2026-10-05 -> " + h.Como, h.Hasta == null);
            o = new OiNq("2026-09-08"); h = o.Hasta(U("2026-09-08 04:00"));
            Chequear("posferiado 2026-09-08 (calendario) -> " + h.Como, h.Hasta == null);
            o = new OiNq("2026-09-08", new[] { "2026-09-08" }); h = o.Hasta(U("2026-09-08 04:00"));
            Chequear("posferiado 2026-09-08 (lista de la vista previa) -> " + h.Como, h.Hasta == null);
            Chequear("calendario: 2026-11-27 posferiado (Thanksgiving 26-11), 2026-10-08 no", TiempoFam.EsPosferiado("2026-11-27") && !TiempoFam.EsPosferiado("2026-10-08") && !TiempoFam.EsPosferiado("2026-10-07"));
            // 6) sin fotos de noche
            o = new OiNq("2026-10-07"); o.Avanzar(new List<FotoCadena>()); h = o.Hasta(U("2026-10-07 01:00"));
            Chequear("sin fotos -> " + h.Como, h.Hasta == null);
            return f;
        }

        sealed class Stat
        {
            public int Ref, Mio, Ambos, SoloRef, SoloMio, Etq, Precio, Monto; public double MaxDp;
            public List<string> Ej = new List<string>();
        }

        static int Una(string dia, string carpViva, string carpCinta, string carpRef, bool vivo, bool corrSesion, bool zeroCadaMin, int nEj)
        {
            var sw = Stopwatch.StartNew();
            Console.WriteLine("=================================================================== sesion " + dia + (vivo ? " (VIVO simulado)" : " (historico)"));
            var rutaRef = Path.Combine(carpRef, "ref-" + dia + ".json");
            if (!File.Exists(rutaRef)) { Console.WriteLine("  falta la referencia " + rutaRef); return 1; }
            var refNiv = new Dictionary<long, Dictionary<string, List<(double P, string E, double G)>>>();
            string refOi = "";
            var texto = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(rutaRef), @"(?<=[\[,:]\s*)(-?Infinity|NaN)(?=\s*[,\]\}])", "null");   // json.dump de Python escribe NaN
            using (var doc = JsonDocument.Parse(texto))
            {
                var r = doc.RootElement;
                if (r.TryGetProperty("oi_nq", out var oi) && oi.ValueKind == JsonValueKind.Array) refOi = string.Join(" | ", oi.EnumerateArray().Select(x => x.ToString()));
                foreach (var m in r.GetProperty("niv").EnumerateObject())
                {
                    long k = long.Parse(m.Name, INV);
                    var d = new Dictionary<string, List<(double, string, double)>>();
                    foreach (var s in m.Value.EnumerateObject())
                    {
                        if (!SeriesNq.VISIBLES.Contains(s.Name)) continue;
                        var l = new List<(double, string, double)>();
                        foreach (var x in s.Value.EnumerateArray())
                        {
                            var a = x.EnumerateArray().ToList();
                            l.Add((a[0].GetDouble(), a[1].GetString(), a[2].ValueKind == JsonValueKind.Null ? double.NaN : a[2].GetDouble()));
                        }
                        d[s.Name] = l;
                    }
                    refNiv[k] = d;
                }
            }
            var fin = TiempoFam.FinSesion(dia);
            var cinta = CintaCsv.Cargar(carpCinta, dia);
            var libro = LibroNqViva3.Cargar(carpViva, dia, fin);
            Console.WriteLine("  " + cinta.Origen);
            Console.WriteLine("  " + libro.Origen);
            Console.WriteLine(string.Format(INV, "  cargado en {0:0.0} s", sw.Elapsed.TotalSeconds));
            DateTime reloj = fin;
            var op = new OpcionesNq { Reloj = () => reloj, CorrPorTanda = !corrSesion, ZeroCadaMinuto = zeroCadaMin };
            var mn = new MinuteroNq(libro, cinta, dia, op);
            long k0 = TiempoFam.Clave(TiempoFam.IniSesion(dia)), k1 = TiempoFam.Clave(fin) - 1;
            var st = SeriesNq.VISIBLES.ToDictionary(s => s, s => new Stat());
            int conLibro = 0, nMin = 0; double corrMax = 0;
            var sw2 = Stopwatch.StartNew();
            for (long k = k0; k <= k1; k++)
            {
                var t = TiempoFam.DeClave(k);
                if (vivo) { libro.VisibleHasta = t; reloj = t; }
                double fut = cinta.CierreConocido(t);
                var b = mn.Minuto(k, t, fut);
                nMin++;
                if (b != null) { conLibro++; corrMax = Math.Max(corrMax, Math.Abs(b.Conv)); }
                var mio = SeriesNq.Visibles(b);
                refNiv.TryGetValue(k, out var rr);
                foreach (var sid in SeriesNq.VISIBLES)
                {
                    var s = st[sid];
                    List<(double P, string E, double G)> lr = null;
                    if (rr != null && rr.TryGetValue(sid, out var x)) lr = x;
                    mio.TryGetValue(sid, out var lm);
                    bool hr = lr != null && lr.Count > 0, hm = lm != null && lm.Count > 0;
                    if (hr) s.Ref++;
                    if (hm) s.Mio++;
                    string Det() => string.Format(INV, "{0:yyyy-MM-dd HH:mm} UTC fut {1} | ref [{2}] | mio [{3}] | foto {4:HH:mm:ss} S {5} oiOk {6} ({7})",
                        t, NumFam.F(fut), lr == null ? "" : string.Join("; ", lr.Select(q => NumFam.F(q.P) + " " + q.E + " " + NumFam.F(q.G, "0.0"))),
                        lm == null ? "" : string.Join("; ", lm.Select(q => NumFam.F(NumFam.PyRound(q.P, 2)) + " " + q.E + " " + NumFam.F(q.GexM, "0.0"))),
                        mn.FotoUsada?.TsUtc, b == null ? "-" : NumFam.F(b.S), b?.OiOk, mn.OiComo);
                    if (hr && !hm) { s.SoloRef++; if (s.Ej.Count < nEj) s.Ej.Add("solo ref: " + Det()); continue; }
                    if (!hr && hm) { s.SoloMio++; if (s.Ej.Count < nEj) s.Ej.Add("solo mio: " + Det()); continue; }
                    if (!hr) continue;
                    s.Ambos++;
                    if (lr.Count != lm.Count || lr.Where((q, i) => q.E != lm[i].E).Any()) { s.Etq++; if (s.Ej.Count < nEj) s.Ej.Add("etiquetas: " + Det()); continue; }
                    bool dp = false, dg = false;
                    for (int i = 0; i < lr.Count; i++)
                    {
                        double p = NumFam.PyRound(lm[i].P, 2);
                        double d = Math.Abs(p - lr[i].P);
                        if (d > s.MaxDp) s.MaxDp = d;
                        if (d > 0.01 + 1e-9) dp = true;
                        double g1 = lr[i].G, g2 = lm[i].GexM;
                        if (double.IsNaN(g1) != double.IsNaN(g2) || (!double.IsNaN(g1) && Math.Abs(g1 - g2) > 1e-9)) dg = true;
                    }
                    if (dp) { s.Precio++; if (s.Ej.Count < nEj) s.Ej.Add("precio: " + Det()); }
                    else if (dg) { s.Monto++; if (s.Ej.Count < nEj) s.Ej.Add("monto: " + Det()); }
                }
            }
            Console.WriteLine(string.Format(INV, "  {0} minutos, {1} con libro NQ; corr max {2}; zero: {3} calculos, {4} aciertos de cache; {5:0.0} s",
                nMin, conLibro, NumFam.F(corrMax), mn.CacheZ.Calculos, mn.CacheZ.Aciertos, sw2.Elapsed.TotalSeconds));
            Console.WriteLine("  C2 mio: " + mn.OiComo + (mn.OiViejoHasta != null ? " (hasta " + mn.OiViejoHasta.Value.ToString("HH:mm:ss", INV) + ")" : ""));
            Console.WriteLine("  C2 ref: " + refOi);
            Console.WriteLine("  serie            ref    mio  ambos solo_ref solo_mio etiq precio monto  max|dp|");
            int fallas = 0;
            foreach (var sid in SeriesNq.VISIBLES)
            {
                var s = st[sid];
                int f = s.SoloRef + s.SoloMio + s.Etq + s.Precio + s.Monto;
                fallas += f;
                Console.WriteLine(string.Format(INV, "  {0,-15} {1,5} {2,6} {3,6} {4,8} {5,8} {6,4} {7,6} {8,5}  {9:0.0000} {10}",
                    sid, s.Ref, s.Mio, s.Ambos, s.SoloRef, s.SoloMio, s.Etq, s.Precio, s.Monto, s.MaxDp, f == 0 ? "OK" : "DIFIERE"));
                foreach (var e in s.Ej) Console.WriteLine("      " + e);
            }
            Console.WriteLine(string.Format(INV, "  total {0:0.0} s", sw.Elapsed.TotalSeconds));
            return fallas;
        }
    }
}
