// Programa.cs — arnes de paridad del modulo Familia QQQ (B3c), 08-10-2026.
// Corre MinuteroQqq + SeriesQqq minuto a minuto sobre la sesion completa (22:00 UTC de la vispera a 21:00 UTC) con los dobles de prueba
// (cinta y cadenas YA archivadas, lo mismo que leyo la referencia) y compara contra atas/_test_cuatro_paridad/referencia/ref-<dia>.json
// (= preview_niveles.Motor(vivo=False).historico()). Criterio (PLAN_PARALELO.md): misma presencia por minuto, mismas etiquetas, precio en NQ
// a <= 0,01 (round 2 de Python contra el de la referencia) y monto igual (round 1). Imprime la paridad por serie y las primeras diferencias.
// Uso: dotnet run -c Release -- [dias...] [--modo paridad|corregida|ambos] [--detalle N]
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using PythiaGexCuatro.Familia;
using PythiaGexCuatro.Familia.Qqq;

namespace ParidadQqq
{
    public static class Programa
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static int Main(string[] args)
        {
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            var dias = args.Where(a => a.Length == 10 && a[4] == '-' && !a.StartsWith("--")).ToList();
            if (dias.Count == 0) dias = new List<string> { "2026-10-07", "2026-10-08" };
            string modo = Opt(args, "--modo", "ambos");
            int detalle = int.Parse(Opt(args, "--detalle", "8"), Inv);
            string raiz = BuscarRaiz();
            string app = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS");
            Console.WriteLine("raiz " + raiz + " | app " + app + " | modo " + modo);
            bool todoOk = ExtrasQqq.Hora();
            foreach (var dia in dias)
            {
                var modos = modo == "ambos" ? new[] { "paridad", "corregida" } : new[] { modo };
                foreach (var m in modos) todoOk &= UnDia(raiz, app, dia, m, detalle);
            }
            Console.WriteLine(todoOk ? "RESULTADO: PARIDAD OK" : "RESULTADO: HAY DIFERENCIAS (ver arriba)");
            return todoOk ? 0 : 1;
        }

        static string Opt(string[] a, string k, string def) { int i = Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : def; }

        static string BuscarRaiz()
        {
            foreach (var ini in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var d = new DirectoryInfo(ini);
                while (d != null)
                {
                    if (Directory.Exists(Path.Combine(d.FullName, "laboratorio")) && Directory.Exists(Path.Combine(d.FullName, "atas"))) return d.FullName;
                    d = d.Parent;
                }
            }
            return @"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex";
        }

        sealed class Est { public int Ref, Mio, Ambos, SoloRef, SoloMio, Etq, Precio, Monto; public double MaxDp, MaxDm; }

        static bool UnDia(string raiz, string app, string dia, string modo, int detalle)
        {
            var sw = Stopwatch.StartNew();
            Console.WriteLine();
            Console.WriteLine("==================== " + dia + " (" + modo + ") ====================");
            var ini = TiempoFam.IniSesion(dia); var fin = TiempoFam.FinSesion(dia);
            var cinta = new CintaArchivoQqq(raiz, dia);
            Console.WriteLine(cinta.Resumen + $" ({sw.ElapsedMilliseconds} ms)");
            var cboe = new FuenteCboeArchivoQqq(app, "QQQ", "QQQ", dia, fin);
            Console.WriteLine(cboe.Resumen + $" ({sw.ElapsedMilliseconds} ms)");
            Func<OpcionesQqq> fop = () => modo == "paridad"
                ? new OpcionesQqq { Corregida = false, ContratoDe = OpcionesQqq.ContratoTablaPython }
                : new OpcionesQqq { Corregida = true, ContratoDe = OpcionesQqq.ContratoFijo(OpcionesQqq.ContratoDeCodigo("MNQZ6")) };
            var min = new MinuteroQqq(cboe, cinta, fop(), TiempoFam.IniSesion(dia).AddDays(-6));
            var firma = new Dictionary<long, string>();
            var lineas = new List<string>();
            min.AlProcesar = r => lineas.Add(ConversionQqq.LineaMuestra(r));

            // referencia
            var refPath = Path.Combine(raiz, "atas", "_test_cuatro_paridad", "referencia", "ref-" + dia + ".json");
            // json.dump de Python escribe NaN / Infinity sin comillas: a null para System.Text.Json
            var texto = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(refPath), @"(?<=[\[,:\s])-?(NaN|Infinity)(?=[\],}\s])", "null");
            using var doc = JsonDocument.Parse(texto);
            var niv = doc.RootElement.GetProperty("niv");
            var refPorClave = new Dictionary<long, Dictionary<string, List<(double P, string E, double G)>>>();
            foreach (var kv in niv.EnumerateObject())
            {
                var d = new Dictionary<string, List<(double, string, double)>>();
                foreach (var s in kv.Value.EnumerateObject())
                {
                    if (!SeriesQqq.IDS.Contains(s.Name)) continue;
                    var l = new List<(double, string, double)>();
                    foreach (var x in s.Value.EnumerateArray())
                        l.Add((x[0].ValueKind == JsonValueKind.Number ? x[0].GetDouble() : double.NaN, x[1].GetString(), x[2].ValueKind == JsonValueKind.Number ? x[2].GetDouble() : double.NaN));
                    d[s.Name] = l;
                }
                refPorClave[long.Parse(kv.Name, Inv)] = d;
            }

            var est = SeriesQqq.IDS.ToDictionary(s => s, s => new Est());
            int impresos = 0, minutosLibro = 0;
            long k0 = TiempoFam.Clave(ini), k1 = TiempoFam.Clave(fin) - 1;
            LibroMinuto ultimoB = null; long ultimoK = 0;
            for (long k = k0; k <= k1; k++)
            {
                var t = TiempoFam.DeClave(k);
                double fut = cinta.CierreConocido(t);
                var b = min.Minuto(k, t, fut);
                if (b != null) { minutosLibro++; ultimoB = b; ultimoK = k; }
                var mio = b != null ? SeriesQqq.Calcular(b) : new Dictionary<string, List<NivelFam>>();
                firma[k] = ExtrasQqq.Firma(b, mio);
                refPorClave.TryGetValue(k, out var rf);
                foreach (var id in SeriesQqq.IDS)
                {
                    var e = est[id];
                    List<(double P, string E, double G)> r = null;
                    bool hayRef = rf != null && rf.TryGetValue(id, out r);
                    bool hayMio = mio.TryGetValue(id, out var mm);
                    if (hayRef) e.Ref++;
                    if (hayMio) e.Mio++;
                    string dif = null;
                    if (hayRef && !hayMio) { e.SoloRef++; dif = "solo en la referencia"; }
                    else if (!hayRef && hayMio) { e.SoloMio++; dif = "solo en C#"; }
                    else if (hayRef && hayMio)
                    {
                        e.Ambos++;
                        if (r.Count != mm.Count || Enumerable.Range(0, r.Count).Any(i => r[i].E != mm[i].E)) { e.Etq++; dif = "etiquetas"; }
                        else
                        {
                            bool bp = false, bm = false;
                            for (int i = 0; i < r.Count; i++)
                            {
                                double dp = Math.Abs(NumFam.PyRound(mm[i].P, 2) - r[i].P);
                                e.MaxDp = Math.Max(e.MaxDp, dp);                                  // round 2 de Python contra la referencia (ya redondeada)
                                if (dp > 0.01 + 1e-9) bp = true;
                                bool nr = double.IsNaN(r[i].G), nm = double.IsNaN(mm[i].GexM);
                                if (nr != nm) bm = true;
                                else if (!nr) { double dm = Math.Abs(mm[i].GexM - r[i].G); e.MaxDm = Math.Max(e.MaxDm, dm); if (dm > 1e-9) bm = true; }
                            }
                            if (bp) { e.Precio++; dif = "precio"; }
                            if (bm) { e.Monto++; dif = (dif == null ? "" : dif + "+") + "monto"; }
                        }
                    }
                    if (dif != null && impresos < detalle)
                    {
                        impresos++;
                        var f = min.FotoEn(t);
                        Console.WriteLine($"  DIF {id} {t:yyyy-MM-dd HH:mm}Z (clave {k}) {dif}: ref [{(hayRef ? string.Join("; ", r.Select(x => $"{x.P.ToString(Inv)} {x.E} {Fmt(x.G)}")) : "-")}] C# [{(hayMio ? string.Join("; ", mm.Select(x => $"{x.P.ToString("0.####", Inv)} {x.E} {Fmt(x.GexM)}")) : "-")}]"
                            + $" | fut {Fmt(fut)} foto gen {(f == null ? "-" : f.GeneradoUtc.ToString("HH:mm:ss", Inv))} razon {Fmt(f?.Razon ?? double.NaN, "0.######")} n {f?.RazonN} vig {(f == null ? "-" : f.VigenteHastaUtc.ToString("MM-dd HH:mm", Inv))} contr {f?.RazonContrato}");
                    }
                }
            }

            bool ok = true;
            Console.WriteLine($"minutos con libro QQQ: {minutosLibro} de {k1 - k0 + 1}; fotos procesadas {min.Fotos.Count}; zero calculados {min.CalculosZero}, reusados {min.AciertosZero}; {sw.ElapsedMilliseconds} ms");
            foreach (var id in SeriesQqq.IDS)
            {
                var e = est[id];
                bool bien = e.SoloRef == 0 && e.SoloMio == 0 && e.Etq == 0 && e.Precio == 0 && e.Monto == 0;
                ok &= bien;
                Console.WriteLine($"  {(bien ? "OK " : "DIF")} {id,-15} ref {e.Ref,4} | C# {e.Mio,4} | ambos {e.Ambos,4} | solo ref {e.SoloRef} | solo C# {e.SoloMio} | etiquetas {e.Etq} | precio>0,01 {e.Precio} (max |dp| redondeado {e.MaxDp:0.######}) | monto {e.Monto} (max {e.MaxDm:0.###})");
            }

            // la conversion y la fuente del ultimo minuto contra la salida de la referencia (fuentes.QQQ)
            try
            {
                var fq = doc.RootElement.GetProperty("salida").GetProperty("fuentes").GetProperty("QQQ");
                var f = min.FotoEn(TiempoFam.DeClave(ultimoK));
                double rzRef = fq.GetProperty("conv_valor").GetDouble();
                string datoRef = fq.GetProperty("dato_utc").GetString(), vigRef = fq.GetProperty("vigente_hasta").GetString();
                bool congRef = fq.GetProperty("congelada").GetBoolean(), oiRef = fq.GetProperty("oi_ok").GetBoolean();
                string dato = f.DatoUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", Inv), vig = f.VigenteHastaUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", Inv);
                bool bien = Math.Abs(rzRef - ultimoB.Conv) < 1e-12 && datoRef == dato && vigRef == vig && congRef == f.Congelada && oiRef == !f.OiViejo;
                ok &= bien;
                Console.WriteLine($"  {(bien ? "OK " : "DIF")} fuente QQQ del ultimo minuto ({TiempoFam.DeClave(ultimoK):HH:mm}Z): razon C# {ultimoB.Conv:R} ref {rzRef:R} | dato {dato} ref {datoRef} | vigente hasta {vig} ref {vigRef} | congelada {f.Congelada} ref {congRef} | OI fresco {!f.OiViejo} ref {oiRef}");
                Console.WriteLine($"      texto: {ultimoB.ConvTexto} | ref: {fq.GetProperty("conv").GetString()}");
            }
            catch (Exception ex) { Console.WriteLine("  (sin fuentes.QQQ en la referencia: " + ex.Message + ")"); }
            ok &= ExtrasQqq.Vivo(cboe, cinta, fop, dia, firma);
            ok &= ExtrasQqq.Vivo(cboe, cinta, fop, dia, firma, 400);
            ok &= ExtrasQqq.Reinicio(cboe, cinta, fop, dia, firma, lineas);
            Console.WriteLine($"  ({sw.ElapsedMilliseconds} ms en total)");
            return ok;
        }

        static string Fmt(double v, string f = "0.##") => double.IsNaN(v) ? "None" : v.ToString(f, Inv);
    }
}
