// Integrado.cs — corrida INTEGRADA del motor fam (B3d) con los minuteros reales de B3a (NQ), B3b (NDX) y B3c (QQQ) y sus dobles de archivo,
// sobre las sesiones archivadas del 07-10 y el 08-10 (solo lectura), contra la referencia de la vista previa (21 series, minuto a minuto).
// Es la unica prueba de FAM_MUROS_* y CONF_vol sobre datos reales: dependen de los perfiles completos de los tres libros.
// Atribucion: una diferencia en FAM/CONF en un minuto donde las series por libro tambien difieren se cuenta como "heredada" (viene del
// minutero); si las series por libro de ese minuto son iguales, es del modulo fam.
// Prioridad BelowNormal. Uso: dotnet bin/Release/fam_integrado.dll [dia ...] [--ejemplos N]
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using PythiaGexCuatro.Familia;
using PythiaGexCuatro.Familia.Ndx;
using PythiaGexCuatro.Familia.Qqq;

namespace FamIntegrado
{
    public static class Integrado
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static StreamWriter _out;
        static void P(string s = "") { Console.WriteLine(s); _out?.WriteLine(s); _out?.Flush(); }

        public static int Main(string[] args)
        {
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            var d0 = new DirectoryInfo(AppContext.BaseDirectory);
            while (d0 != null && !File.Exists(Path.Combine(d0.FullName, "fam_integrado.csproj"))) d0 = d0.Parent;
            string aqui = d0?.FullName ?? Directory.GetCurrentDirectory();
            string raiz = Path.GetFullPath(Path.Combine(aqui, "..", "..", "..", ".."));
            string app = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS");
            string refDir = Path.Combine(raiz, "atas", "_test_cuatro_paridad", "referencia");
            string cintaDir = Path.Combine(raiz, "profundidad", "estado", "cinta");
            string ndxEntrada = Path.Combine(raiz, "atas", "_test_cuatro_paridad", "ndx", "entrada");
            string vViejas = Path.Combine(raiz, "laboratorio", "dom", "ronda3", "velas", "velas-MNQ-m2.csv");
            string vNuevas = Path.Combine(raiz, "laboratorio", "tres", "datos", "velas_cache_2026-10-07", "velas-MNQ-m2.csv");
            var dias = args.Where(a => a.Length == 10 && a[4] == '-').ToList();
            if (dias.Count == 0) dias = new List<string> { "2026-10-07", "2026-10-08" };
            int ejemplos = 8; int ie = Array.IndexOf(args, "--ejemplos"); if (ie >= 0 && ie + 1 < args.Length) ejemplos = int.Parse(args[ie + 1], Inv);
            var res = Path.Combine(aqui, "..", "resultados"); Directory.CreateDirectory(res);
            bool ok = true;
            using (_out = new StreamWriter(Path.Combine(res, "integrado-fam.txt"), false, new UTF8Encoding(false)))
            {
                P("corrida integrada fam (B3d) " + MotorFamilia.VERSION + " | " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", Inv) + " UTC");
                P("minuteros: _modulos/familia/{nq,ndx,qqq} (B3a/B3b/B3c) con sus dobles de archivo; motor y reglas: _modulos/familia/fam");
                foreach (var dia in dias) ok &= UnDia(dia, raiz, app, refDir, cintaDir, ndxEntrada, vViejas, vNuevas, ejemplos);
                P(); P(ok ? "RESULTADO: VERDE (las 21 series iguales a la referencia)" : "RESULTADO: HAY DIFERENCIAS (ver la atribucion)");
            }
            return ok ? 0 : 1;
        }

        static bool UnDia(string dia, string raiz, string app, string refDir, string cintaDir, string ndxEntrada, string vViejas, string vNuevas, int ejemplos)
        {
            P(); P("==================== " + dia + " (paridad) ====================");
            var refPath = Path.Combine(refDir, "ref-" + dia + ".json");
            if (!File.Exists(refPath)) { P("  falta " + refPath); return false; }
            var fNdx = Path.Combine(ndxEntrada, "cboe-NDX-" + dia + ".jsonl.gz");
            if (!File.Exists(fNdx)) { P("  falta la entrada congelada de NDX (la arma el arnes de B3b): " + fNdx); return false; }
            var sw = Stopwatch.StartNew();
            var d = DateTime.SpecifyKind(DateTime.ParseExact(dia, "yyyy-MM-dd", Inv), DateTimeKind.Utc);
            var ini = TiempoFam.IniSesion(dia); var fin = TiempoFam.FinSesion(dia);
            // ---- los tres minuteros con sus dobles (los mismos que usan sus arneses)
            var cintaNq = ParidadNq.CintaCsv.Cargar(cintaDir, dia);
            var libro = ParidadNq.LibroNqViva3.Cargar(Path.Combine(app, "PythiaGex3", "viva"), dia, fin);
            var minNq = new MinuteroNq(libro, cintaNq, dia, new OpcionesNq { Reloj = () => fin });
            var ciNdx = new ArnesNdx.DobleCinta(d, cintaDir, vViejas, vNuevas);
            var cbNdx = ArnesNdx.DobleCboe.ParaSesion(d, app, ndxEntrada, s => P(s));
            var minNdx = new LibroMinuteroNdx(cbNdx, ciNdx, new OpcionesNdx { ParidadPython = true });
            var ciQqq = new ParidadQqq.CintaArchivoQqq(raiz, dia);
            var cbQqq = new ParidadQqq.FuenteCboeArchivoQqq(app, "QQQ", "QQQ", dia, fin);
            var minQqq = new MinuteroQqq(cbQqq, ciQqq, new OpcionesQqq { Corregida = false, ContratoDe = OpcionesQqq.ContratoTablaPython }, ini.AddDays(-6));
            P("  cargado en " + sw.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s");

            // ---- el motor: sin persistencia, paridad, todo en una llamada (tandas de 240 internas); las estelas de la 3.0 COPIADAS a una carpeta
            //      temporal (el motor lee <Carpeta>\estela como en el indicador lee PythiaGex4\estela)
            var tmp = Path.Combine(Path.GetTempPath(), "fam_integrado_" + dia);
            try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { }
            Directory.CreateDirectory(Path.Combine(tmp, "estela"));
            var dAnt = d.AddDays(-1).ToString("yyyy-MM-dd", Inv);
            foreach (var capa in CatalogoFamilia.CapasTres) foreach (var dd in new[] { dAnt, dia })
            {
                var o = Path.Combine(app, "PythiaGex3", "estela", "estela-" + capa + "-" + dd + ".jsonl");
                if (File.Exists(o)) File.Copy(o, Path.Combine(tmp, "estela", Path.GetFileName(o)), true);
            }
            var motor = new MotorFamilia(new OpcionesMotorFamilia { Carpeta = tmp, Persistir = false, Corregida = false, PresupuestoMs = int.MaxValue, EsperaArranqueS = 0, RutaLog = Path.Combine(tmp, "log.txt") });
            motor.Configurar(cintaNq, libro, cbQqq, new ILibroMinutero[] { minQqq, minNdx, minNq }, null, "MNQZ6");   // orden al reves a proposito: el motor ordena NQ, NDX, QQQ
            sw.Restart();
            motor.Avanzar(fin.AddMinutes(30));
            int v = 1; while (motor.Pendiente && v < 20) { motor.Avanzar(fin.AddMinutes(30)); v++; }
            double seg = sw.Elapsed.TotalSeconds;
            P("  motor: " + motor.MinutosCalculados + " minutos en " + seg.ToString("0.0", Inv) + " s (" + (seg * 1000 / Math.Max(1, motor.MinutosCalculados)).ToString("0.0", Inv) + " ms/minuto, todo incluido), "
              + motor.TandasIniciadas + " tandas, " + motor.Almacen.Cuantos + " minutos con libros");

            // ---- contra la referencia
            JsonDocument doc;
            {
                var txt = File.ReadAllText(refPath, Encoding.UTF8);
                txt = System.Text.RegularExpressions.Regex.Replace(txt, @"(?<=[\[,:\s])(NaN|-?Infinity)(?=[\],}\s])", "null");
                doc = JsonDocument.Parse(txt);
            }
            var niv = doc.RootElement.GetProperty("niv");
            var calc = CatalogoFamilia.Calc;
            var st = calc.ToDictionary(s => s, s => new int[6]);   // iguales exactos, iguales con tolerancia, distintos, solo ref, solo c#, heredadas(FAM/CONF)
            var ej = new List<string>(); int minRef = 0, minMio = 0, minAmbos = 0;
            long k0 = TiempoFam.Clave(ini), k1 = TiempoFam.Clave(fin) - 1;
            for (long k = k0; k <= k1; k++)
            {
                bool hayRef = niv.TryGetProperty(k.ToString(Inv), out var rr);
                var r = motor.Almacen.Get(k);
                if (hayRef) minRef++; if (r != null) minMio++; if (hayRef && r != null) minAmbos++;
                // primero las series por libro (para atribuir FAM/CONF)
                bool librosIguales = true;
                var res = new Dictionary<string, int>();
                for (int s = 0; s < calc.Length; s++)
                {
                    string sid = calc[s];
                    bool hp = hayRef && rr.TryGetProperty(sid, out var lp) && lp.GetArrayLength() > 0;
                    var lm = r?.Series?[s]; bool hc = lm != null && lm.Length > 0;
                    int c;
                    if (!hp && !hc) continue;
                    if (hp && !hc) c = 3; else if (!hp && hc) c = 4;
                    else
                    {
                        rr.TryGetProperty(sid, out lp);
                        if (lp.GetArrayLength() != lm.Length) c = 2;
                        else
                        {
                            bool exacto = true, tol = true; int i = 0;
                            foreach (var x in lp.EnumerateArray())
                            {
                                double pp = x[0].GetDouble(); string pe = x[1].GetString(); double pm = x[2].ValueKind == JsonValueKind.Number ? x[2].GetDouble() : double.NaN;
                                var m = lm[i++];
                                bool mismoM = (double.IsNaN(pm) && double.IsNaN(m.GexM)) || pm == m.GexM;
                                bool tolM = (double.IsNaN(pm) && double.IsNaN(m.GexM)) || Math.Abs(pm - m.GexM) <= 0.1 + 1e-9;
                                if (pe != m.Etq) { exacto = tol = false; break; }
                                if (pp != m.Precio || !mismoM) exacto = false;
                                if (Math.Abs(pp - m.Precio) > 0.01 + 1e-9 || !tolM) tol = false;
                            }
                            c = exacto ? 0 : tol ? 1 : 2;
                        }
                    }
                    res[sid] = c;
                    bool esFam = sid.StartsWith("FAM_", StringComparison.Ordinal) || sid == "CONF_vol";
                    if (!esFam && c >= 2) librosIguales = false;
                }
                foreach (var kv in res)
                {
                    bool esFam = kv.Key.StartsWith("FAM_", StringComparison.Ordinal) || kv.Key == "CONF_vol";
                    if (esFam && kv.Value >= 2 && !librosIguales) st[kv.Key][5]++;
                    else st[kv.Key][kv.Value]++;
                    if (kv.Value >= 2 && ej.Count < ejemplos && (!esFam || librosIguales))
                    {
                        int s = CatalogoFamilia.IndiceCalc(kv.Key);
                        string py = hayRef && rr.TryGetProperty(kv.Key, out var lp2) ? lp2.ToString() : "-";
                        string cs = r?.Series?[s] != null ? "[" + string.Join(",", r.Series[s].Select((m, i) => "[" + m.Precio.ToString("R", Inv) + "," + m.Etq + "," + m.GexM.ToString("R", Inv) + "|crudo " + (r.Crudo?[s]?[i] ?? double.NaN).ToString("R", Inv) + "]")) + "]" : "-";
                        ej.Add(TiempoFam.DeClave(k).ToString("HH:mm", Inv) + " " + kv.Key + ": ref " + py + " c# " + cs);
                    }
                }
            }
            P("  minutos con niveles: referencia " + minRef + ", c# " + minMio + ", ambos " + minAmbos);
            P("  " + "serie".PadRight(16) + "exactos".PadLeft(9) + "a<=0,01".PadLeft(9) + "distintos".PadLeft(11) + "solo ref".PadLeft(10) + "solo c#".PadLeft(9) + "heredadas".PadLeft(11));
            bool ok = true, okFam = true;
            foreach (var sid in calc)
            {
                var a = st[sid];
                P("  " + sid.PadRight(16) + a[0].ToString(Inv).PadLeft(9) + a[1].ToString(Inv).PadLeft(9) + a[2].ToString(Inv).PadLeft(11) + a[3].ToString(Inv).PadLeft(10) + a[4].ToString(Inv).PadLeft(9) + a[5].ToString(Inv).PadLeft(11));
                if (a[2] + a[3] + a[4] + a[5] > 0) ok = false;
                if ((sid.StartsWith("FAM_", StringComparison.Ordinal) || sid == "CONF_vol") && a[2] + a[3] + a[4] > 0) okFam = false;
            }
            P("  FAM/CONF propias del modulo fam (con las series por libro iguales en ese minuto): " + (okFam ? "0 diferencias" : "HAY diferencias"));
            foreach (var e in ej) P("    " + e);

            // ---- la foto publicada por el motor contra la salida de la vista previa: historia por vela m2 (24 series) y actuales
            var sal = doc.RootElement.GetProperty("salida");
            var foto = motor.Foto;
            var refH = new Dictionary<(string, long), JsonElement>();
            foreach (var se in sal.GetProperty("historia").EnumerateObject()) foreach (var x in se.Value.EnumerateArray()) refH[(se.Name, x[0].GetInt64())] = x[1];
            int hOk = 0, hMal = 0; var hej = new List<string>();
            foreach (var vv in sal.GetProperty("velas").EnumerateArray())
            {
                long t = vv[0].GetInt64();
                foto.HistoriaM2.TryGetValue(t, out var e);
                foreach (var si in CatalogoFamilia.Series.Take(24))
                {
                    bool hp = refH.TryGetValue((si.Id, t), out var lp); double[] mio = null; bool hc = e != null && e.TryGetValue(si.Id, out mio);
                    if (!hp && !hc) continue;
                    bool igual = hp == hc && lp.GetArrayLength() == mio.Length;
                    for (int i = 0; igual && i < mio.Length; i++) igual = lp[i][0].GetDouble() == mio[i];
                    if (igual) hOk++; else { hMal++; if (hej.Count < 6) hej.Add(si.Id + " vela " + t + ": ref " + (hp ? lp.ToString() : "-") + " c# " + (hc ? string.Join(",", mio) : "-")); }
                }
            }
            P("  historia de la foto (velas de la referencia, 24 series con TRES): iguales " + hOk + ", distintos " + hMal);
            foreach (var x in hej) P("    " + x);
            var refA = sal.GetProperty("actuales").EnumerateArray().ToList();
            var act = foto.Actuales.Where(a => a.Libro != "TQQQ").ToList();
            int aOk = 0, aMal = 0; var aej = new List<string>();
            if (refA.Count != act.Count) { aMal++; aej.Add("cantidad ref " + refA.Count + " c# " + act.Count); }
            for (int i = 0; i < Math.Min(refA.Count, act.Count); i++)
            {
                var pa = refA[i]; var c = act[i];
                double sp = pa.GetProperty("strike").ValueKind == JsonValueKind.Number ? pa.GetProperty("strike").GetDouble() : double.NaN;
                double gp = pa.GetProperty("gex_musd").ValueKind == JsonValueKind.Number ? pa.GetProperty("gex_musd").GetDouble() : double.NaN;
                string dp = pa.GetProperty("dato_utc").GetString(), dc = c.DatoUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv);
                bool igual = pa.GetProperty("serie").GetString() == c.Serie && pa.GetProperty("rol").GetString() == c.Rol && pa.GetProperty("precio").GetDouble() == c.Precio
                             && ((double.IsNaN(sp) && double.IsNaN(c.Strike)) || sp == c.Strike) && ((double.IsNaN(gp) && double.IsNaN(c.GexM)) || gp == c.GexM)
                             && dp == dc && pa.GetProperty("oi_viejo").GetBoolean() == c.OiViejo;
                if (igual) aOk++; else { aMal++; if (aej.Count < 8) aej.Add("ref " + pa.ToString() + " | c# " + c.Serie + " " + c.Rol + " " + c.Precio.ToString("R", Inv) + " k " + c.Strike.ToString("R", Inv) + " g " + c.GexM.ToString("R", Inv) + " " + dc + " oiv " + c.OiViejo); }
            }
            P("  actuales de la foto: iguales " + aOk + ", distintos " + aMal + " | fuentes: " + string.Join(" | ", foto.Fuentes.Select(f => f.Libro + " " + f.Texto)));
            foreach (var x in aej) P("    " + x);
            try { Directory.Delete(tmp, true); } catch { }
            return ok && hMal == 0 && aMal == 0;
        }
    }
}
