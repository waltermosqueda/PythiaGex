// Program.cs — arnes de paridad del modulo Familia NDX (B3b) contra la vista previa (referencia ya generada por el agente principal:
// atas/_test_cuatro_paridad/referencia/ref-<dia>.json = preview_niveles.Motor(vivo=False).historico()).
// Uso (desde esta carpeta):  dotnet run -c Release [-- --dia 2026-10-08] [--sin-corregido] [--sin-reinicio]
// Prioridad BelowNormal. Lee la cinta y las velas de la cache en su lugar (no cambian) y las fotos de NDX de entrada/ (se congelan la 1.a vez).
// Criterio (PLAN_PARALELO): strike elegido identico, precio en NQ a <= 0,01, misma presencia por minuto; montos a <= 0,1.
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

namespace ArnesNdx
{
    public static class Program
    {
        public static readonly string[] SERIES = { "MUROS_NDX_vol", "MUROS_NDX_oi", "MAJORS_NDX_vol", "MAJORS_NDX_oi", "ZEST_NDX_vol", "ZTP_NDX_vol" };

        /// <summary>Las 6 series NDX con la seleccion del codigo comun (SeriesLibroFam de B3a, la que usa el motor) sobre el LibroMinuto del modulo.</summary>
        static Dictionary<string, List<(double P, string E, double M)>> Series(LibroMinuto b)
        {
            var r = new Dictionary<string, List<(double, string, double)>>();
            foreach (var kv in SeriesLibroFam.Calcular(b).Series)
                if (SERIES.Contains(kv.Key)) r[kv.Key] = kv.Value.Select(n => (n.P, n.E, n.GexM)).ToList();
            return r;
        }

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static StreamWriter _log;
        static void Log(string s) { Console.WriteLine(s); _log?.WriteLine(s); _log?.Flush(); }

        public static int Main(string[] args)
        {
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            string aqui = AppContext.BaseDirectory;
            // subir hasta la carpeta del arnes (la que tiene ArnesNdx.csproj)
            var dir = new DirectoryInfo(aqui);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ArnesNdx.csproj"))) dir = dir.Parent;
            string arnes = dir?.FullName ?? Directory.GetCurrentDirectory();
            string raiz = Path.GetFullPath(Path.Combine(arnes, "..", "..", ".."));
            string refDir = Path.Combine(arnes, "..", "referencia");
            string entrada = Path.Combine(arnes, "entrada"), res = Path.Combine(arnes, "resultados");
            Directory.CreateDirectory(res);
            _log = new StreamWriter(Path.Combine(res, "paridad_ndx.txt"), false, new UTF8Encoding(false));
            string app = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS");
            string cinta = Path.Combine(raiz, "profundidad", "estado", "cinta");
            string vViejas = Path.Combine(raiz, "laboratorio", "dom", "ronda3", "velas", "velas-MNQ-m2.csv");
            string vNuevas = Path.Combine(raiz, "laboratorio", "tres", "datos", "velas_cache_2026-10-07", "velas-MNQ-m2.csv");

            var dias = new List<DateTime>();
            for (int i = 0; i < args.Length; i++) if (args[i] == "--dia" && i + 1 < args.Length) dias.Add(DateTime.ParseExact(args[++i], "yyyy-MM-dd", Inv));
            if (dias.Count == 0) dias.AddRange(new[] { new DateTime(2026, 10, 7), new DateTime(2026, 10, 8) });
            bool corregido = !args.Contains("--sin-corregido"), reinicio = !args.Contains("--sin-reinicio"), replay = !args.Contains("--sin-replay"), sens = args.Contains("--sensibilidad"), motor = !args.Contains("--sin-motor");

            Log("ArnesNdx (B3b) " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", Inv) + "Z — modulo _modulos/familia/ndx contra preview_niveles (referencia del agente principal)");
            bool todoOk = true;
            foreach (var dia in dias)
            {
                Log("");
                Log("=== sesion " + dia.ToString("yyyy-MM-dd", Inv) + " ===");
                var refPath = Path.Combine(refDir, "ref-" + dia.ToString("yyyy-MM-dd", Inv) + ".json");
                if (!File.Exists(refPath)) { Log("  falta la referencia " + refPath); todoOk = false; continue; }
                var sw = Stopwatch.StartNew();
                var ci = new DobleCinta(dia, cinta, vViejas, vNuevas);
                Log("  cinta: " + ci.NTicksSesion + " ticks de la sesion, " + ci.NTicksPrevios + " previos, velas previas " + ci.NVelasPrevias + ", de la sesion " + ci.NVelasSesion
                    + " (" + sw.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s)");
                sw.Restart();
                var cb = DobleCboe.ParaSesion(dia, app, entrada, Log);
                var refe = LeerReferencia(refPath, out var fuenteRef);

                var (sal, diag, tiempos) = Correr(dia, ci, cb, new OpcionesNdx { ParidadPython = true });
                todoOk &= Comparar(dia, "paridad", sal, refe, res);
                Log("  tiempo: " + tiempos);
                CompararFuente(diag, fuenteRef);

                if (corregido)
                {
                    var (sal2, _, t2) = Correr(dia, ci, cb, new OpcionesNdx { ParidadPython = false, ContratoGrafico = "MNQZ6" });
                    todoOk &= Comparar(dia, "corregido", sal2, refe, res);
                    Log("  tiempo: " + t2);
                }
                if (reinicio)
                {
                    // reinicio de noche sin la cinta vieja: 1) corrida con persistencia; 2) motor nuevo que solo tiene la cinta de la sesion + las muestras guardadas
                    var tmp = Path.Combine(res, "datos4_prueba_" + dia.ToString("yyyyMMdd", Inv));
                    if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                    var (s1, _, _) = Correr(dia, ci, cb, new OpcionesNdx { ParidadPython = true, CarpetaDatos = tmp });
                    var ciSola = new DobleCinta(dia, cinta, vViejas, vNuevas, soloSesion: true);
                    var (s2, _, _) = Correr(dia, ciSola, cb, new OpcionesNdx { ParidadPython = true, CarpetaDatos = tmp });
                    var (s3, _, _) = Correr(dia, ciSola, cb, new OpcionesNdx { ParidadPython = true });
                    int dif = DifSalidas(s1, s2), difSin = DifSalidas(s1, s3);
                    var archivos = Directory.Exists(Path.Combine(tmp, "familia")) ? Directory.GetFiles(Path.Combine(tmp, "familia")).Select(Path.GetFileName).ToArray() : new string[0];
                    Log("  reinicio con muestras guardadas (" + string.Join(", ", archivos) + ") y SIN la cinta de los dias previos: " + dif + " minutos distintos"
                        + " (sin las muestras: " + difSin + " minutos distintos, para ver que la prueba muerde)");
                    todoOk &= dif == 0;
                }
                if (replay)
                {
                    // prueba E: reloj simulado minuto a minuto + reinicios a las 03:00 y 15:00 UTC con solo la cinta de la sesion y las muestras guardadas
                    var tmp = Path.Combine(res, "datos4_replay_" + dia.ToString("yyyyMMdd", Inv));
                    if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                    var swr = Stopwatch.StartNew();
                    var (sr, info) = CorrerReplay(dia, ci, new DobleCinta(dia, cinta, vViejas, vNuevas, soloSesion: true), cb, tmp);
                    var (sal0, _, _) = Correr(dia, ci, cb, new OpcionesNdx { ParidadPython = true });
                    int dif = DifSalidas(sal0, sr);
                    Log("  replay minuto a minuto con reinicios 03:00 y 15:00 UTC (" + info + ", " + swr.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s): " + dif + " minutos distintos de la corrida historica");
                    todoOk &= dif == 0;
                }
                if (motor)
                {
                    // protocolo de tandas del motor fam (ITandaMinutero.NuevaTanda):
                    //  T1 en vivo: una tanda por minuto (= la vista previa EN VIVO) -> tiene que dar lo mismo que la cache por minuto (TandaZeroMin = 1)
                    //  T2 puesta al dia con tandas de 240 alineadas + reinicio a las 15:00 UTC (motor nuevo, primera tanda 15:00-17:59) -> = historico
                    var (sal0, _, _) = Correr(dia, ci, cb, new OpcionesNdx { ParidadPython = true });
                    var (salMin, _, _) = Correr(dia, ci, cb, new OpcionesNdx { ParidadPython = true, TandaZeroMin = 1 });
                    var t1 = CorrerMotor(dia, ci, cb, vivo: true, reinicio: -1);
                    var t2 = CorrerMotor(dia, ci, cb, vivo: false, reinicio: DobleCinta.Ms(dia.AddHours(15)) / 60000);
                    int d1 = DifSalidas(salMin, t1), d2 = DifSalidas(sal0, t2);
                    Log("  protocolo del motor (NuevaTanda): en vivo (tanda por minuto) contra cache por minuto: " + d1 + " minutos distintos; puesta al dia de a 240 con reinicio a las 15:00 contra el historico: " + d2 + " minutos distintos");
                    todoOk &= d1 == 0 && d2 == 0;
                }
                if (sens)
                {
                    foreach (var tz in new[] { 1, 0 })
                    {
                        var (ss, _, _) = Correr(dia, ci, cb, new OpcionesNdx { ParidadPython = true, TandaZeroMin = tz });
                        Log("  sensibilidad cache del zero " + (tz == 1 ? "por minuto (como la vista previa EN VIVO)" : "una por sesion") + ":");
                        Comparar(dia, "sens-tanda" + tz, ss, refe, res);
                    }
                    var (sh, _, _) = Correr(dia, ci, cb, new OpcionesNdx { ParidadPython = true, ZeroCacheConHorizonte = true });
                    Log("  sensibilidad cache del zero con las filas del horizonte en la clave (el 0DTE vencido no queda en el zero):");
                    Comparar(dia, "sens-horizonte", sh, refe, res);
                }
            }
            Log("");
            todoOk &= PruebaInvierno();
            Log("");
            Log(todoOk ? "RESULTADO: PARIDAD OK" : "RESULTADO: HAY DIFERENCIAS (ver arriba y resultados/dif-*.txt)");
            _log.Dispose();
            return todoOk ? 0 : 1;
        }

        // ------------------------------------------------------------------ una corrida del modulo sobre toda la sesion
        sealed class MinutoCs { public long Key; public Dictionary<string, List<(double P, string E, double M)>> S; public double Base, Sx, Fut; public DateTime Gen; }

        static (Dictionary<long, MinutoCs> Sal, DiagMinutoNdx Diag, string Tiempos) Correr(DateTime dia, ICinta ci, IFuenteCboe cb, OpcionesNdx op)
        {
            var m = new LibroMinuteroNdx(cb, ci, op);
            var sal = new Dictionary<long, MinutoCs>();
            long ini = DobleCinta.Ms(dia.AddHours(-2)) / 60000, fin = DobleCinta.Ms(dia.AddHours(21)) / 60000;
            var ms = new List<double>();
            var sw = Stopwatch.StartNew();
            DiagMinutoNdx ult = null;
            for (long k = ini; k <= fin - 1; k++)
            {
                var t = DobleCinta.DeMs(k * 60000);
                double fut = ci.CierreConocido(t);
                var t0 = sw.Elapsed.TotalMilliseconds;
                var L = m.Minuto(k, t, fut);
                ms.Add(sw.Elapsed.TotalMilliseconds - t0);
                if (L == null) continue;
                ult = m.Ultimo;
                sal[k] = new MinutoCs { Key = k, S = Series(L), Base = L.Conv, Sx = L.S, Fut = fut, Gen = m.Ultimo.Estado.Foto.GeneradoUtc };
            }
            ms.Sort();
            string tiempos = "total " + sw.Elapsed.TotalSeconds.ToString("0.00", Inv) + " s, por minuto mediana " + ms[ms.Count / 2].ToString("0.00", Inv) + " ms, p99 "
                             + ms[(int)(ms.Count * 0.99)].ToString("0.00", Inv) + " ms, max " + ms[ms.Count - 1].ToString("0", Inv) + " ms (incluye rehacer C7: " + m.Recalculos + " vez/veces, "
                             + m.Conversion.Estados.Count + " fotos)";
            return (sal, ult, tiempos);
        }

        static (Dictionary<long, MinutoCs> Sal, string Info) CorrerReplay(DateTime dia, DobleCinta llena, DobleCinta soloSesion, IFuenteCboe cb, string carpeta)
        {
            var rc = new ReplayCboe(cb);
            var cinta = new ReplayCinta(llena);
            var op = new OpcionesNdx { ParidadPython = true, CarpetaDatos = carpeta };
            var m = new LibroMinuteroNdx(rc, cinta, op);
            var sal = new Dictionary<long, MinutoCs>();
            long ini = DobleCinta.Ms(dia.AddHours(-2)) / 60000, fin = DobleCinta.Ms(dia.AddHours(21)) / 60000;
            var reinicios = new[] { DobleCinta.Ms(dia.AddHours(3)) / 60000, DobleCinta.Ms(dia.AddHours(15)) / 60000 };
            int recalc = 0, calent = 0, calentMin = 0;
            for (long k = ini; k <= fin - 1; k++)
            {
                if (reinicios.Contains(k))
                {
                    recalc += m.Recalculos; calent += m.Calentamientos;
                    cinta = new ReplayCinta(soloSesion);            // ATAS reiniciado: la cinta solo tiene el relleno de la sesion
                    m = new LibroMinuteroNdx(rc, cinta, op);
                }
                var t = DobleCinta.DeMs(k * 60000);
                rc.Ahora = t.AddSeconds(6); cinta.Ahora = t.AddSeconds(6);
                double fut = cinta.CierreConocido(t);
                var L = m.Minuto(k, t, fut);
                if (m.CalentadosUltimo > 0 && reinicios.Contains(k)) calentMin += m.CalentadosUltimo;
                if (L == null) continue;
                sal[k] = new MinutoCs { Key = k, S = Series(L), Base = L.Conv, Sx = L.S, Fut = fut, Gen = m.Ultimo.Estado.Foto.GeneradoUtc };
            }
            recalc += m.Recalculos; calent += m.Calentamientos;
            return (sal, recalc + " veces rehecho C7, " + calent + " calentamientos de la cache del zero (" + calentMin + " minutos rehechos al reiniciar)");
        }

        /// <summary>El modulo manejado como lo maneja MotorFamilia: NuevaTanda antes de cada tanda (vivo: una por minuto; si no, de a 240 alineadas
        /// a la sesion). reinicio = clave donde se crea un modulo nuevo (su primera tanda va de ahi al proximo borde de 240).</summary>
        static Dictionary<long, MinutoCs> CorrerMotor(DateTime dia, ICinta ci, IFuenteCboe cb, bool vivo, long reinicio)
        {
            var op = new OpcionesNdx { ParidadPython = true };
            var m = new LibroMinuteroNdx(cb, ci, op);
            var sal = new Dictionary<long, MinutoCs>();
            long ini = DobleCinta.Ms(dia.AddHours(-2)) / 60000, fin = DobleCinta.Ms(dia.AddHours(21)) / 60000;
            long finTanda = long.MinValue;
            for (long k = ini; k <= fin - 1; k++)
            {
                if (k == reinicio) { m = new LibroMinuteroNdx(cb, ci, op); finTanda = long.MinValue; }
                if (k >= finTanda)
                {
                    finTanda = vivo ? k + 1 : Math.Min(fin, ini + 240 * ((k - ini) / 240 + 1));
                    ((ITandaMinutero)m).NuevaTanda(DobleCinta.DeMs(k * 60000), k, finTanda - 1);
                }
                var t = DobleCinta.DeMs(k * 60000);
                double fut = ci.CierreConocido(t);
                var L = m.Minuto(k, t, fut);
                if (L == null) continue;
                sal[k] = new MinutoCs { Key = k, S = Series(L), Base = L.Conv, Sx = L.S, Fut = fut, Gen = m.Ultimo.Estado.Foto.GeneradoUtc };
            }
            return sal;
        }

        /// <summary>Prueba G del diseño (horario de invierno, fechas sinteticas): el modo corregido sigue a NY; la paridad conserva el error del Python.</summary>
        static bool PruebaInvierno()
        {
            int mal = 0, n = 0;
            DateTime U(int y, int mo, int d, int h, int mi, int s = 0) => DateTime.SpecifyKind(new DateTime(y, mo, d, h, mi, s), DateTimeKind.Utc);
            void Ver(string que, object obtenido, object esperado)
            {
                n++;
                bool ok = Equals(obtenido, esperado);
                if (!ok) { mal++; Log("  invierno MAL: " + que + " = " + obtenido + " (esperado " + esperado + ")"); }
            }
            Ver("C9 corregido 02-11 22:00Z", CalendarioNdx.ProximaApertura(U(2026, 11, 2, 22, 0), false), U(2026, 11, 3, 14, 30));
            Ver("C9 paridad 02-11 22:00Z", CalendarioNdx.ProximaApertura(U(2026, 11, 2, 22, 0), true), U(2026, 11, 3, 13, 30));
            Ver("C9 corregido 30-10 20:30Z (verano)", CalendarioNdx.ProximaApertura(U(2026, 10, 30, 20, 30), false), U(2026, 10, 31, 13, 30));
            Ver("en_hora 02-11 14:34Z (09:34 EST)", CalendarioNdx.EnHora(U(2026, 11, 2, 14, 34)), false);
            Ver("en_hora 02-11 14:35Z (09:35 EST)", CalendarioNdx.EnHora(U(2026, 11, 2, 14, 35)), true);
            Ver("en_hora 02-11 20:59:59Z (15:59 EST)", CalendarioNdx.EnHora(U(2026, 11, 2, 20, 59, 59)), true);
            Ver("en_hora 02-11 21:00Z (16:00 EST)", CalendarioNdx.EnHora(U(2026, 11, 2, 21, 0)), false);
            Ver("cierre de rueda corregido 02-11", CalendarioNdx.CierreRueda(new DateTime(2026, 11, 2), false), U(2026, 11, 2, 21, 0));
            Ver("cierre de rueda paridad 02-11", CalendarioNdx.CierreRueda(new DateTime(2026, 11, 2), true), U(2026, 11, 2, 20, 0));
            Ver("vencimiento Z6", CalendarioNdx.ExpiraContrato("Z6"), U(2026, 12, 18, 14, 30));
            Ver("vencimiento H7 (corregido)", CalendarioNdx.ExpiraContrato("MNQH7"), U(2027, 3, 19, 13, 30));
            Ver("vencimiento H7 (paridad: error del Python)", CalendarioNdx.ExpiraParidad("H7"), U(2027, 3, 19, 14, 30));
            Ver("vencimiento U6", CalendarioNdx.ExpiraContrato("U6"), U(2026, 9, 18, 13, 30));
            Ver("sesion corregida 02-11 23:00Z", CalendarioNdx.SesionDe(U(2026, 11, 2, 23, 0), false), new DateTime(2026, 11, 3));
            Ver("sesion corregida 02-11 22:30Z (pausa)", CalendarioNdx.SesionDe(U(2026, 11, 2, 22, 30), false), new DateTime(2026, 11, 2));
            Ver("sesion paridad 02-11 22:30Z", CalendarioNdx.SesionDe(U(2026, 11, 2, 22, 30), true), new DateTime(2026, 11, 3));
            Ver("inicio sesion corregida 03-11", CalendarioNdx.InicioSesion(new DateTime(2026, 11, 3), false), U(2026, 11, 2, 23, 0));
            Ver("inicio sesion paridad 03-11", CalendarioNdx.InicioSesion(new DateTime(2026, 11, 3), true), U(2026, 11, 2, 22, 0));
            Ver("pausa CME corregida 02-11 22:30Z", CalendarioNdx.EntreSesiones(U(2026, 11, 2, 22, 30), false), true);
            Ver("pausa CME paridad 02-11 22:30Z", CalendarioNdx.EntreSesiones(U(2026, 11, 2, 22, 30), true), false);
            Ver("pausa CME corregida 02-11 21:30Z", CalendarioNdx.EntreSesiones(U(2026, 11, 2, 21, 30), false), false);
            Ver("pausa CME paridad 08-10 21:30Z", CalendarioNdx.EntreSesiones(U(2026, 10, 8, 21, 30), true), true);
            Ver("pausa CME corregida 08-10 21:30Z", CalendarioNdx.EntreSesiones(U(2026, 10, 8, 21, 30), false), true);
            Ver("congelada 02-11 20:59:30Z (15:59 EST)", CalendarioNdx.Congelada(U(2026, 11, 2, 20, 59, 30)), true);
            Ver("congelada 02-11 20:58Z", CalendarioNdx.Congelada(U(2026, 11, 2, 20, 58)), false);
            Ver("reloj oi_fresco corregido 03-11 14:29Z", CalendarioNdx.MinutoDelDia(CalendarioNdx.Reloj(U(2026, 11, 3, 14, 29), false)), 13 * 60 + 29);
            Ver("reloj oi_fresco corregido 03-11 14:30Z", CalendarioNdx.MinutoDelDia(CalendarioNdx.Reloj(U(2026, 11, 3, 14, 30), false)), 13 * 60 + 30);
            Ver("codigo MNQZ6", CalendarioNdx.CodigoMes("MNQZ6"), "Z6");
            Ver("contrato del grafico", new OpcionesNdx { ContratoGrafico = "MNQZ6" }.Contrato(U(2026, 11, 2, 12, 0)), "Z6");
            Ver("continua sin mes 09-09 (antes del roll)", new OpcionesNdx { ContratoGrafico = "MNQ" }.Contrato(U(2026, 9, 9, 15, 0)), "U6");
            Ver("continua sin mes 10-09 (roll: tercer viernes - 8)", new OpcionesNdx { ContratoGrafico = "MNQ" }.Contrato(U(2026, 9, 10, 15, 0)), "Z6");
            Ver("continua sin mes 12-10 (roll a H7)", new OpcionesNdx { ContratoGrafico = "MNQ" }.Contrato(U(2026, 12, 10, 15, 0)), "H7");
            Log("  horario de invierno (prueba G, fechas sinteticas): " + (n - mal) + " de " + n + " bien");
            return mal == 0;
        }

        static int DifSalidas(Dictionary<long, MinutoCs> a, Dictionary<long, MinutoCs> b)
        {
            int n = 0;
            foreach (var k in a.Keys.Union(b.Keys))
            {
                if (!a.TryGetValue(k, out var x) || !b.TryGetValue(k, out var y)) { n++; continue; }
                bool igual = x.S.Count == y.S.Count && x.S.All(kv => y.S.TryGetValue(kv.Key, out var l) && l.Count == kv.Value.Count
                             && l.Zip(kv.Value, (u, v) => u.E == v.E && Math.Abs(u.P - v.P) < 1e-9).All(z => z));
                if (!igual) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ referencia
        static Dictionary<long, Dictionary<string, List<(double P, string E, double M)>>> LeerReferencia(string ruta, out JsonElement? fuenteNdx)
        {
            var r = new Dictionary<long, Dictionary<string, List<(double, string, double)>>>();
            // json.dump de Python escribe NaN/Infinity sin comillas (no es JSON valido para System.Text.Json): se pasan a null
            var texto = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(ruta, Encoding.UTF8), @"(?<=[\[,:]\s*)-?(NaN|Infinity)(?=\s*[\],}])", "null");
            using var doc = JsonDocument.Parse(texto);
            foreach (var kv in doc.RootElement.GetProperty("niv").EnumerateObject())
            {
                long key = long.Parse(kv.Name, Inv);
                var d = new Dictionary<string, List<(double, string, double)>>();
                foreach (var s in kv.Value.EnumerateObject())
                {
                    if (!SERIES.Contains(s.Name)) continue;
                    var l = new List<(double, string, double)>();
                    foreach (var x in s.Value.EnumerateArray())
                    {
                        var a = x.EnumerateArray().ToArray();
                        l.Add((a[0].GetDouble(), a[1].GetString(), a[2].ValueKind == JsonValueKind.Number ? a[2].GetDouble() : double.NaN));
                    }
                    d[s.Name] = l;
                }
                if (d.Count > 0) r[key] = d;
            }
            fuenteNdx = null;
            if (doc.RootElement.TryGetProperty("salida", out var sal) && sal.TryGetProperty("fuentes", out var fu) && fu.TryGetProperty("NDX", out var n))
                fuenteNdx = n.Clone();
            return r;
        }

        static void CompararFuente(DiagMinutoNdx d, JsonElement? f)
        {
            if (d == null || f == null) return;
            var e = f.Value;
            string S(string n) => e.TryGetProperty(n, out var v) ? v.ToString() : "?";
            Log("  ultimo minuto " + d.TUtc.ToString("HH:mm", Inv) + "Z: C# base " + d.Base.ToString("0.0000", Inv) + " S " + d.S.ToString("0.00", Inv) + " foto " + d.Estado.Foto.GeneradoUtc.ToString("HH:mm:ss", Inv)
                + "Z (ts " + d.Estado.Foto.TsUtc.ToString("HH:mm:ss", Inv) + ", dato " + d.Estado.Foto.DatoUtc.ToString("HH:mm:ss", Inv) + "Z, vigente hasta " + d.Estado.Vigencia.ToString("MM-dd HH:mm", Inv)
                + "Z, congelada " + d.Estado.Congelada + ", oi_fresco " + !d.Estado.OiViejo + ", modo " + d.Estado.Modo + ")");
            Log("                       Python base " + S("conv_valor") + " S " + S("S") + " foto " + S("generado_utc") + " (ts " + S("cadena_ts") + ", dato " + S("dato_utc") + ", vigente hasta " + S("vigente_hasta")
                + ", congelada " + S("congelada") + ", oi_ok " + S("oi_ok") + ")");
            Log("                       Python: " + S("conv"));
            string texto; new ConversionNdx(new OpcionesNdx { ParidadPython = true }).Base(d.Estado, d.TUtc, out texto);
            Log("                       C#:     " + texto);
        }

        // ------------------------------------------------------------------ comparacion por serie
        static bool Comparar(DateTime dia, string modo, Dictionary<long, MinutoCs> cs, Dictionary<long, Dictionary<string, List<(double P, string E, double M)>>> refe, string res)
        {
            bool ok = true;
            var dif = new StringBuilder();
            int nDif = 0;
            Log("  [" + modo + "] serie            ref    C#  ambos  solo_ref  solo_C#  etq/cant  strike!=  |dp|>0,01  max|dp|   |dM|>0,1  max|dM|");
            foreach (var s in SERIES)
            {
                int nr = 0, nc = 0, amb = 0, sr = 0, sc = 0, etq = 0, dpMal = 0, dmMal = 0, kMal = 0; double dpMax = 0, dmMax = 0;
                var keys = new SortedSet<long>(refe.Keys.Where(k => refe[k].ContainsKey(s)).Concat(cs.Keys.Where(k => cs[k].S.ContainsKey(s))));
                foreach (var k in keys)
                {
                    List<(double P, string E, double M)> a = null, b = null;
                    bool hr = refe.TryGetValue(k, out var rk) && rk.TryGetValue(s, out a);
                    bool hc = cs.TryGetValue(k, out var ck) && ck.S.TryGetValue(s, out b);
                    if (hr) nr++; if (hc) nc++;
                    if (hr && !hc) { sr++; if (nDif++ < 5000) dif.AppendLine(s + " " + k + " " + Hm(k) + " solo ref: " + Txt(a) + Ctx(ck)); continue; }
                    if (!hr && hc) { sc++; if (nDif++ < 5000) dif.AppendLine(s + " " + k + " " + Hm(k) + " solo C#: " + Txt(b) + Ctx(ck)); continue; }
                    amb++;
                    if (a.Count != b.Count || a.Where((x, i) => x.E != b[i].E).Any()) { etq++; if (nDif++ < 5000) dif.AppendLine(s + " " + k + " " + Hm(k) + " etq: ref " + Txt(a) + " C# " + Txt(b) + Ctx(ck)); continue; }
                    bool malP = false;
                    for (int i = 0; i < a.Count; i++)
                    {
                        // la referencia viene redondeada a 2 decimales (round(p, 2)); se compara contra el C# sin redondear (|dp| <= 0,005 = mismo numero)
                        double dp = Math.Abs(b[i].P - a[i].P);
                        if (dp > dpMax) dpMax = dp;
                        if (dp > 0.01 + 1e-9) malP = true;
                        // strike elegido distinto (MUROS/MAJORS: los strikes de NDX estan a >= 5 pts; una base distinta se ve como dp chico)
                        if (!s.StartsWith("ZEST") && !s.StartsWith("ZTP") && dp > 1.0) kMal++;
                        if (!double.IsNaN(a[i].M) || !double.IsNaN(b[i].M))
                        {
                            double dm = double.IsNaN(a[i].M) || double.IsNaN(b[i].M) ? double.PositiveInfinity : Math.Abs(a[i].M - b[i].M);
                            if (dm > dmMax) dmMax = dm;
                            if (dm > 0.1 + 1e-9) dmMal++;
                        }
                    }
                    if (malP) { dpMal++; if (nDif++ < 5000) dif.AppendLine(s + " " + k + " " + Hm(k) + " precio: ref " + Txt(a) + " C# " + Txt(b) + Ctx(ck)); }
                }
                bool sOk = sr == 0 && sc == 0 && etq == 0 && dpMal == 0 && dmMal == 0 && kMal == 0;
                ok &= sOk;
                Log("  [" + modo + "] " + s.PadRight(15) + nr.ToString().PadLeft(6) + nc.ToString().PadLeft(6) + amb.ToString().PadLeft(7) + sr.ToString().PadLeft(10) + sc.ToString().PadLeft(9)
                    + etq.ToString().PadLeft(10) + kMal.ToString().PadLeft(10) + dpMal.ToString().PadLeft(11) + dpMax.ToString("0.0000", Inv).PadLeft(9) + dmMal.ToString().PadLeft(11) + dmMax.ToString("0.00", Inv).PadLeft(9) + (sOk ? "  OK" : "  DIF"));
            }
            File.WriteAllText(Path.Combine(res, "dif-" + dia.ToString("yyyy-MM-dd", Inv) + "-" + modo + ".txt"), dif.ToString(), new UTF8Encoding(false));
            return ok;
        }

        static string Hm(long k) => DobleCinta.DeMs(k * 60000).ToString("MM-dd HH:mm", Inv) + "Z";
        static string Txt(List<(double P, string E, double M)> l) => "[" + string.Join(" ", l.Select(x => x.E + "@" + x.P.ToString("0.00", Inv) + (double.IsNaN(x.M) ? "" : "(" + x.M.ToString("0.0", Inv) + ")"))) + "]";
        static string Ctx(MinutoCs c) => c == null ? "" : "  | C# fut " + c.Fut.ToString("0.00", Inv) + " base " + c.Base.ToString("0.0000", Inv) + " foto " + c.Gen.ToString("HH:mm:ss", Inv);
    }
}
