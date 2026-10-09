// Program.cs — arnes de paridad del modulo TQQQ (B4) de PythiaGex 4.1, 08-10-2026. Re-ejecutable, sin ATAS, prioridad BelowNormal.
//   dotnet run -c Release --project atas/_test_cuatro_paridad/tqqq/ArnesTqqq.csproj
// Referencias (tqqq_vivo.py 1.0 sin tocar):
//   referencia/tqqq_vivo-2026-10-08T215912Z.json  el json que escribio tqqq_vivo a las 21:59:12Z (copia de profundidad/pagina/preview_datos)
//   referencia/tqqq_vivo-2026-10-07-sim.json      tqqq_vivo.ciclo con el reloj en 2026-10-07 21:59:12Z (gen_ref_tqqq.py)
// Pruebas:
//   A  paridad 08-10 (s forzada de conversion.json, como tqqq_vivo ese dia): todas las velas de las 5 series, actuales y conv
//   B  paridad 07-10 (s = tqqq_vivo.s_del_dia con las fotos del 06-10)
//   C  modo corregido 08-10 (hora NY, s del prev_day_close: 83,62 / 31.160,08 por el delegado de cierres): tiene que dar lo mismo que A
//   E  vivo simulado 08-10: el reloj avanza de a 2 min, se ven solo las fotos y los ticks de ese momento y se piden las ultimas 16 velas
//      (como el motor): al final tiene que dar lo mismo que A (sin mirar adelante)
//   F  reinicio sin cinta: muestras persistidas de A + cinta VACIA: las 4 series de niveles tienen que dar lo mismo (T_DOMS_raz no: necesita nq)
//   D  ancla de noche 07-10 -> 08-10 (SIN VALIDAR): NQ_1600, NDX congelado, c al cierre, modos B y C, error contra el primer c del 08-10
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using PythiaGexCuatro.Familia;

namespace ArnesTqqq
{
    public static class Program
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static StringBuilder _sal = new StringBuilder();
        private static void P(string s = "") { Console.WriteLine(s); _sal.AppendLine(s); }

        public static int Main(string[] args)
        {
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            var sw = Stopwatch.StartNew();
            string aqui = Aqui();
            string raiz = Path.GetFullPath(Path.Combine(aqui, "..", "..", ".."));
            string app = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string cboeDir = Path.Combine(app, "ATAS", "PythiaGex", "cboe-local");
            string cintaDir = Path.Combine(raiz, "profundidad", "estado", "cinta");
            string refDir = Path.Combine(aqui, "referencia");
            string resDir = Path.Combine(aqui, "resultados");
            Directory.CreateDirectory(resDir);
            foreach (var lg in Directory.GetFiles(resDir, "tqqq-*.log")) try { File.Delete(lg); } catch { }
            P("arnes TQQQ (B4) — " + CalculoTqqq.VERSION + " — " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", Inv) + " UTC");
            P("cboe-local: " + cboeDir); P("cinta: " + cintaDir);

            var dias = new[] { "2026-10-05", "2026-10-06", "2026-10-07", "2026-10-08" };
            var cboe = new CboeLocalTq();
            cboe.Cargar(cboeDir, "TQQQ", "TQQQ", dias, true);
            cboe.Cargar(cboeDir, "NDX", "NQ", dias, false);
            P("fotos: TQQQ " + cboe.Cuantas("TQQQ") + ", NDX " + cboe.Cuantas("NDX") + " (" + sw.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s)");
            var cinta08 = CintaCsvTq.Leer(cintaDir, "2026-10-08");
            var cinta07 = CintaCsvTq.Leer(cintaDir, "2026-10-07");
            P("cinta: 08-10 " + cinta08.N + " ticks (ultimo " + cinta08.UltimoTickUtc.ToString("MM-dd HH:mm:ss.fff", Inv) + "), 07-10 " + cinta07.N +
              " (ultimo " + cinta07.UltimoTickUtc.ToString("MM-dd HH:mm:ss.fff", Inv) + ") (" + sw.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s)");

            var ref08 = Leer(Path.Combine(refDir, "tqqq_vivo-2026-10-08T215912Z.json"));
            var ref07 = Leer(Path.Combine(refDir, "tqqq_vivo-2026-10-07-sim.json"));
            double sForz08 = ref08.GetProperty("conv").GetProperty("s").GetDouble();
            int fallas = 0;

            // ---------------------------------------------------------------- A
            P(); P("== A  paridad 08-10 (s forzada " + sForz08.ToString("R", Inv) + ", la de conversion.json que uso tqqq_vivo)");
            var tmpA = Limpia(Path.Combine(resDir, "tmp_A"));
            var calcA = new CalculoTqqq(new OpcionesTqqq { Corregida = false, Carpeta = tmpA, RutaLog = Path.Combine(resDir, "tqqq-A.log"),
                                                           SForzada = new Dictionary<string, double> { ["2026-10-08"] = sForz08 } });
            var velasA = Correr(calcA, cinta08, cboe, "2026-10-08 13:30:00", "2026-10-08 21:59:12");
            fallas += Comparar("A", ref08, velasA, calcA, "2026-10-08", true);
            {   // t02 (conversion.json): c del dia = robusta(piso 0,5) de las muestras de rueda con spot vivo hasta su corrida (ts 17:36:14 UTC)
                var conv = Leer(Path.Combine(refDir, "conversion.json"));
                double cT02 = conv.GetProperty("c_dia").GetDouble();
                var ms = calcA.Muestras("2026-10-08").Where(m => !double.IsNaN(m.Muestra) && m.Ts <= new DateTime(2026, 10, 8, 17, 36, 14, DateTimeKind.Utc)).Select(m => m.Muestra).ToList();
                var (cDia, nDia) = TqqqNum.Robusta(ms, 0.5);
                double k82 = sForz08 * 82 + cDia, k82t = sForz08 * 82 + cT02;
                P("  t02: c del dia " + cDia.ToString("0.000", Inv) + " con " + nDia + " muestras (t02 " + cT02.ToString("0.000", Inv) + " con 177: rueda [13:30, 20:00) y spot crudo)" +
                  " | NQ(82) = " + k82.ToString("0.00", Inv) + " (t02 " + k82t.ToString("0.00", Inv) + ")");
                fallas += Chequeo("t02: s 124,213 | c ~21.009 (+-0,5) | 82 -> ~31.195 (+-0,5)", Math.Abs(sForz08 - 124.213) < 0.0005 && Math.Abs(cDia - cT02) <= 0.5 && Math.Abs(k82 - 31194.9) <= 0.5);
            }

            // ---------------------------------------------------------------- B
            P(); P("== B  paridad 07-10 (s = tqqq_vivo.s_del_dia: spot de la ultima foto del 06-10 con t_dato <= 20:00 UTC)");
            var calcB = new CalculoTqqq(new OpcionesTqqq { Corregida = false, Carpeta = Limpia(Path.Combine(resDir, "tmp_B")), RutaLog = Path.Combine(resDir, "tqqq-B.log") });
            var velasB = Correr(calcB, cinta07, cboe, "2026-10-07 13:30:00", "2026-10-07 21:59:12");
            fallas += Comparar("B", ref07, velasB, calcB, "2026-10-07", true);

            // ---------------------------------------------------------------- C
            P(); P("== C  modo corregido 08-10 (hora NY; s del prev_day_close TQQQ 83,62 / NDX 31160,08 por el delegado de cierres)");
            var calcC = new CalculoTqqq(new OpcionesTqqq { Corregida = true, RegistrarAnclas = false, Carpeta = Limpia(Path.Combine(resDir, "tmp_C")),
                                                           RutaLog = Path.Combine(resDir, "tqqq-C.log"), Cierres = CierresConocidos(false) });
            var velasC = Correr(calcC, cinta08, cboe, "2026-10-08 13:30:00", "2026-10-08 21:59:12");
            fallas += Comparar("C", ref08, velasC, calcC, "2026-10-08", true);

            // ---------------------------------------------------------------- E
            P(); P("== E  vivo simulado 08-10 (reloj de a 2 min; solo lo que existia; se piden las ultimas 16 velas como el motor)");
            var calcE = new CalculoTqqq(new OpcionesTqqq { Corregida = false, Carpeta = Limpia(Path.Combine(resDir, "tmp_E")), RutaLog = Path.Combine(resDir, "tqqq-E.log"),
                                                           SForzada = new Dictionary<string, double> { ["2026-10-08"] = sForz08 }, RefrescoS = 0 });
            var velasE = new SortedDictionary<long, TqqqVela>();
            long ini08 = Ms("2026-10-08 13:30:00"), finE = Ms("2026-10-08 21:59:12");
            int llamadas = 0;
            for (long reloj = ini08 + 6000; reloj <= finE; reloj += 120000)
            {
                cboe.Reloj = TqqqHora.DeMs(reloj); cinta08.RelojMs = reloj;
                long form = reloj / 120000 * 120000;
                for (long T = Math.Max(ini08, form - 15 * 120000L); T <= form; T += 120000)
                {
                    var v = calcE.Vela(T, cinta08, cboe); llamadas++;
                    if (v == null) velasE.Remove(T); else velasE[T] = v;
                }
            }
            cboe.Reloj = DateTime.MaxValue; cinta08.RelojMs = long.MaxValue;
            P("  " + llamadas + " llamadas a Vela");
            fallas += Comparar("E", ref08, velasE, calcE, "2026-10-08", false);

            // ---------------------------------------------------------------- F
            P(); P("== F  reinicio sin cinta (precios del MNQ repuestos de tqqq-muestras-2026-10-08-*.csv de A; cinta vacia)");
            var vacia = CintaCsvTq.Leer(Path.Combine(resDir, "no-existe"));
            var calcF = new CalculoTqqq(new OpcionesTqqq { Corregida = false, Carpeta = tmpA, RutaLog = Path.Combine(resDir, "tqqq-F.log"),
                                                           SForzada = new Dictionary<string, double> { ["2026-10-08"] = sForz08 } });
            var velasF = Correr(calcF, vacia, cboe, "2026-10-08 13:30:00", "2026-10-08 21:59:12");
            fallas += Comparar("F", ref08, velasF, calcF, "2026-10-08", false, sinRaz: true);

            // ---------------------------------------------------------------- G / H
            P(); P("== G  modo corregido 08-10 SIN delegado: s del prev_day_close que traen las fotos (simulado: TQQQ 83,62 y NDX 31160,08 desde las 08:00 UTC)");
            var d08 = new DateTime(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc);
            int nt = cboe.PonerPdc("TQQQ", d08, d08.AddHours(16), 83.62), nn = cboe.PonerPdc("NDX", d08, d08.AddHours(16), 31160.08);
            P("  fotos con prev_day_close: TQQQ " + nt + ", NDX " + nn);
            var calcG = new CalculoTqqq(new OpcionesTqqq { Corregida = true, RegistrarAnclas = false, Carpeta = Limpia(Path.Combine(resDir, "tmp_G")), RutaLog = Path.Combine(resDir, "tqqq-G.log") });
            var velasG = Correr(calcG, cinta08, cboe, "2026-10-08 13:30:00", "2026-10-08 21:59:12");
            P("  " + calcG.SDe("2026-10-08").Texto);
            fallas += Comparar("G", ref08, velasG, calcG, "2026-10-08", true);
            cboe.PonerPdc("TQQQ", d08, d08.AddHours(16), double.NaN); cboe.PonerPdc("NDX", d08, d08.AddHours(16), double.NaN);

            P(); P("== H  modo corregido 08-10 sin ningun cierre: respaldo por recta libre de la rueda (>= 20 muestras), CON AVISO (no se compara)");
            var calcH = new CalculoTqqq(new OpcionesTqqq { Corregida = true, RegistrarAnclas = false, Carpeta = Limpia(Path.Combine(resDir, "tmp_H")), RutaLog = Path.Combine(resDir, "tqqq-H.log") });
            var velasH = Correr(calcH, cinta08, cboe, "2026-10-08 13:30:00", "2026-10-08 21:59:12");
            var (sH, txtH) = calcH.SDe("2026-10-08");
            P("  " + txtH + " | velas con niveles " + velasH.Count);
            fallas += Chequeo("H: s de respaldo dentro de 0,5 % de 124,213 y rotulada AVISO", Math.Abs(sH / sForz08 - 1) < 0.005 && txtH.StartsWith("AVISO"));

            // ---------------------------------------------------------------- D
            fallas += Noche(cboe, cintaDir, resDir, ref07, ref08);

            P(); P("TOTAL: " + (fallas == 0 ? "OK, 0 diferencias" : fallas + " diferencias") + " (" + sw.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s)");
            File.WriteAllText(Path.Combine(resDir, "paridad_tqqq.txt"), _sal.ToString(), new UTF8Encoding(false));
            return fallas == 0 ? 0 : 1;
        }

        // ================================================================== D: la noche
        private static int Noche(CboeLocalTq cboe, string cintaDir, string resDir, JsonElement ref07, JsonElement ref08)
        {
            P(); P("== D  ancla de noche 07-10 -> 08-10 (SIN VALIDAR, 1 noche). s del 07-10 = la de tqqq_vivo (cierres del 06-10 no oficiales: el");
            P("      archivo de cboe-local no trae prev_day_close); s del 08-10 = 83,62 / 31160,08. Cinta 07-10 + 08-10.");
            var cinta = CintaCsvTq.Leer(cintaDir, "2026-10-07", "2026-10-08");
            double s07 = ref07.GetProperty("conv").GetProperty("s").GetDouble();
            int fallas = 0;
            foreach (var conOficial in new[] { false, true })
            {
                P(); P("  -- " + (conOficial ? "D2: con P0' oficial = 83,62 visto a las 01:36 UTC del 08-10 (hora medida en SPX el 03-09; en TQQQ SIN MEDIR) -> modo C"
                                              : "D1: sin P0' oficial antes de la rueda -> modo B toda la noche"));
                var tmp = Limpia(Path.Combine(resDir, conOficial ? "tmp_D2" : "tmp_D1"));
                var calc = new CalculoTqqq(new OpcionesTqqq
                {
                    Corregida = true, AnclaNoche = true, Carpeta = tmp, RutaLog = Path.Combine(resDir, conOficial ? "tqqq-D2.log" : "tqqq-D1.log"),
                    SForzada = new Dictionary<string, double> { ["2026-10-07"] = s07 }, Cierres = CierresConocidos(conOficial),
                });
                calc.Contrato = "MNQZ6";
                var velas = new SortedDictionary<long, TqqqVela>();
                long ini = Ms("2026-10-07 13:30:00"), fin = Ms("2026-10-08 21:58:00");
                for (long reloj = ini + 6000; reloj <= fin + 120000 + 6000; reloj += 120000)
                {   // como el motor con TqqqDeNoche: el reloj avanza de a 2 min (6 s despues del borde) y se piden las ultimas 16 velas
                    cboe.Reloj = TqqqHora.DeMs(reloj); cinta.RelojMs = reloj;
                    long form = reloj / 120000 * 120000;
                    for (long T = Math.Max(ini, form - 15 * 120000L); T <= Math.Min(form, fin); T += 120000)
                    {
                        var v = calc.Vela(T, cinta, cboe);
                        if (v == null) velas.Remove(T); else velas[T] = v;
                    }
                }
                cboe.Reloj = DateTime.MaxValue; cinta.RelojMs = long.MaxValue;
                var a = calc.Ancla(new DateTime(2026, 10, 7));
                if (a == null) { P("  FALLA: no hay ancla del 07-10"); fallas++; continue; }
                P("  ancla 07-10: NQ_1600 " + F(a.Nq1600) + " (" + a.Nq1600Fuente + ") | NDX congelado " + F(a.N0p) + " visto " + Hm(a.N0pUtc) + " (" + a.N0pFuente + ")");
                P("               s 07-10 " + F(a.S, "0.000000") + " | c al cierre " + F(a.CCierre, "0.000") + " (n " + a.CCierreN + ", " + Hm(a.CCierreUtc) + ") | P0' implicito " + F(a.P0pImpl, "0.0000") +
                  " | P0' oficial " + F(a.P0pOficial, "0.0000") + (a.P0pOficialUtc != default ? " visto " + Hm(a.P0pOficialUtc) : "") + " " + a.P0pFuente);
                P("               c' = NQ_1600 - N0'/3 = " + F(a.CPrima, "0.000") + " | s'B " + F(a.SPrima(a.P0pImpl), "0.0000") + " | s'C " + F(a.SPrima(a.P0pOficial), "0.0000"));
                P("               primer c del 08-10 " + F(a.CSig, "0.000") + " (" + Hm(a.CSigUtc) + ", s " + F(a.SSig, "0.000000") + ") | K " + F(a.KErr, "0.0000") +
                  " | error B " + F(a.ErrB, "+0.00;-0.00") + " pts | error C " + F(a.ErrC, "+0.00;-0.00") + " pts");
                // controles numericos contra la especificacion (medidos el 08-10 por el informe TQQQ)
                fallas += Chequeo("NQ_1600 = 31396,5 (cinta MNQZ6)", Math.Abs(a.Nq1600 - 31396.5) <= 0.001);
                fallas += Chequeo("N0' = 31160,08 (NDX congelado del 07-10)", Math.Abs(a.N0p - 31160.08) <= 0.001);
                double c07 = ref07.GetProperty("conv").GetProperty("c").GetDouble();
                fallas += Chequeo("c al cierre = c final de tqqq_vivo 07-10 (" + c07.ToString("0.000", Inv) + ")", Math.Abs(a.CCierre - c07) <= 1e-6);
                fallas += Chequeo("c' = 31396,5 - 31160,08/3 = 21009,807", Math.Abs(a.CPrima - (31396.5 - 31160.08 / 3.0)) <= 1e-9);
                // las velas por modo
                var porModo = new SortedDictionary<string, (int n, long ini, long fin)>(StringComparer.Ordinal);
                foreach (var kv in velas)
                {
                    var d = calc.Detalle(kv.Value); string m = d?.Modo ?? "?";
                    if (!porModo.TryGetValue(m, out var x)) x = (0, kv.Key, kv.Key);
                    porModo[m] = (x.n + 1, Math.Min(x.ini, kv.Key), Math.Max(x.fin, kv.Key));
                }
                foreach (var kv in porModo) P("  modo " + kv.Key.PadRight(32) + kv.Value.n.ToString().PadLeft(4) + " velas, " + Hm(TqqqHora.DeMs(kv.Value.ini)) + " .. " + Hm(TqqqHora.DeMs(kv.Value.fin)) + " UTC");
                foreach (var hora in new[] { "2026-10-07 19:58:00", "2026-10-07 20:30:00", "2026-10-08 02:00:00", "2026-10-08 08:00:00", "2026-10-08 13:34:00", "2026-10-08 14:30:00" })
                {
                    long T = Ms(hora);
                    if (!velas.TryGetValue(T, out var v)) { P("  vela " + hora + ": sin niveles"); continue; }
                    var d = calc.Detalle(v);
                    string niv = string.Join(" | ", v.Series.Where(s => s.Key != "T_DOMS_raz").Select(s => s.Key.Substring(2) + " " +
                                 string.Join(",", s.Value.Select((n, j) => n.Etq + " " + v.Strikes[s.Key][j].ToString("0.##", Inv) + "->" + n.Precio.ToString("0.00", Inv)))));
                    P("  vela " + hora + " [" + d.Modo + ", +-" + d.Banda + "] NQ = " + v.S.ToString("0.0000", Inv) + " K + " + v.C.ToString("0.000", Inv) + " | " + niv);
                }
                // en la rueda del 08-10, despues de las 5 muestras, tiene que ser la rueda de A (misma s, mismo c): se compara con la referencia
                var soloRueda = new SortedDictionary<long, TqqqVela>();
                foreach (var kv in velas) if (kv.Key >= Ms("2026-10-08 13:30:00") && calc.Detalle(kv.Value)?.Modo == "rueda") soloRueda[kv.Key] = kv.Value;
                fallas += CompararSoloPresentes("D" + (conOficial ? "2" : "1") + " rueda 08-10", ref08, soloRueda);
                if (conOficial)
                {
                    fallas += Chequeo("modo C en la noche despues de las 01:36 UTC", velas.TryGetValue(Ms("2026-10-08 02:00:00"), out var v2) && calc.Detalle(v2).Modo.StartsWith("ancla C"));
                    fallas += Chequeo("error C (K = 83,62) chico: |err| <= 5 pts (spec: +0,4 contra el c del dia; contra el primer c, +2,4)", Math.Abs(a.ErrC) <= 5);
                }
                else
                {
                    fallas += Chequeo("modo B en la noche (sin P0' oficial)", velas.TryGetValue(Ms("2026-10-08 02:00:00"), out var v2) && calc.Detalle(v2).Modo.StartsWith("ancla B"));
                    fallas += Chequeo("P0' implicito ~ 83,59 (spec: 83,592)", Math.Abs(a.P0pImpl - 83.592) <= 0.01);
                }
                var b = calc.Ancla(new DateTime(2026, 10, 8));
                if (b != null)
                {
                    P("  ancla 08-10 (para esta noche): NQ_1600 " + F(b.Nq1600) + " | NDX congelado " + F(b.N0p) + " visto " + Hm(b.N0pUtc) + " | c al cierre " + F(b.CCierre, "0.000") +
                      " | P0' implicito " + F(b.P0pImpl, "0.0000") + " | c' " + F(b.CPrima, "0.000") + " | s'B " + F(b.SPrima(b.P0pImpl), "0.0000"));
                    fallas += Chequeo("08-10: NQ_1600 = 30976,25 (spec)", Math.Abs(b.Nq1600 - 30976.25) <= 0.001);
                    fallas += Chequeo("08-10: N0' = 30725,81 (spec)", Math.Abs(b.N0p - 30725.81) <= 0.001);
                    fallas += Chequeo("08-10: c' = 30976,25 - 30725,81/3 = 20734,31 (spec)", Math.Abs(b.CPrima - 20734.31) <= 0.005);
                    fallas += Chequeo("08-10: P0' implicito ~ 80,2186 (spec)", Math.Abs(b.P0pImpl - 80.2186) <= 0.0005);
                }
                else { P("  FALLA: no hay ancla del 08-10"); fallas++; }
                var json = File.Exists(Path.Combine(tmp, "tqqq-cierres.json")) ? File.ReadAllText(Path.Combine(tmp, "tqqq-cierres.json")) : "(no se escribio)";
                fallas += Chequeo("tqqq-cierres.json escrito con el ancla del 07-10", json.Contains("\"2026-10-07|MNQZ6\""));
            }
            return fallas;
        }

        /// <summary>Los cierres que se conocen de esos dias (el archivo de cboe-local no trae 'sub'): 08-10 rueda: TQQQ 83,62 y NDX 31160,08
        /// (prev_day_close, t02). conOficial: el prev_day_close nuevo de TQQQ del 07-10 visto a las 01:36 UTC del 08-10.</summary>
        private static Func<string, DateTime, CierresTqqqDia> CierresConocidos(bool conOficial) => (libro, f) =>
        {
            if (f.Date == new DateTime(2026, 10, 8))
            {
                if (libro == "TQQQ") return new CierresTqqqDia { Anterior = 83.62, AnteriorUtc = new DateTime(2026, 10, 8, 13, 31, 0, DateTimeKind.Utc) };
                if (libro == "NDX") return new CierresTqqqDia { Anterior = 31160.08, AnteriorUtc = new DateTime(2026, 10, 8, 13, 31, 0, DateTimeKind.Utc) };
            }
            if (conOficial && f.Date == new DateTime(2026, 10, 7) && libro == "TQQQ")
                return new CierresTqqqDia { PdcNuevo = 83.62, PdcNuevoUtc = new DateTime(2026, 10, 8, 1, 36, 0, DateTimeKind.Utc) };
            return null;
        };

        // ================================================================== correr y comparar

        private static SortedDictionary<long, TqqqVela> Correr(CalculoTqqq calc, ICinta cinta, IFuenteCboe cboe, string desde, string ahora)
        {   // tqqq_vivo.ciclo: velas m2 desde las 13:30 UTC hasta floor(ahora / 2 min) (la que se esta formando incluida)
            var r = new SortedDictionary<long, TqqqVela>();
            long ini = Ms(desde) / 120000 * 120000, fin = Ms(ahora) / 120000 * 120000;
            for (long T = ini; T <= fin; T += 120000) { var v = calc.Vela(T, cinta, cboe); if (v != null) r[T] = v; }
            return r;
        }

        private static int Comparar(string nombre, JsonElement refe, IDictionary<long, TqqqVela> velas, CalculoTqqq calc, string dia, bool conActuales, bool sinRaz = false)
        {
            int fallas = 0;
            var hist = refe.GetProperty("historia");
            foreach (var serie in CalculoTqqq.Series)
            {
                if (sinRaz && serie == "T_DOMS_raz") { P("  " + serie.PadRight(12) + " (no se compara: necesita el precio del MNQ al cierre de cada vela)"); continue; }
                var esperado = new Dictionary<long, JsonElement>();
                if (hist.TryGetProperty(serie, out var arr)) foreach (var x in arr.EnumerateArray()) esperado[x[0].GetInt64()] = x[1];
                int ok = 0, mal = 0, sobran = 0, faltan = 0; double maxD = 0; var ejemplos = new List<string>();
                foreach (var kv in esperado)
                {
                    if (!velas.TryGetValue(kv.Key, out var v) || !v.Series.TryGetValue(serie, out var lv)) { faltan++; if (ejemplos.Count < 4) ejemplos.Add("falta " + Hm(TqqqHora.DeMs(kv.Key))); continue; }
                    bool igual = lv.Length == kv.Value.GetArrayLength();
                    for (int j = 0; igual && j < lv.Length; j++)
                    {
                        double p = kv.Value[j][0].GetDouble(); string rol = kv.Value[j][1].GetString();
                        double d = Math.Abs(p - lv[j].Precio); if (d > maxD) maxD = d;
                        if (d > 0.01 || rol != lv[j].Etq) igual = false;
                    }
                    if (igual) ok++;
                    else { mal++; if (ejemplos.Count < 4) ejemplos.Add(Hm(TqqqHora.DeMs(kv.Key)) + " ref " + kv.Value.GetRawText() + " vs " + string.Join(",", lv.Select(n => n.Precio.ToString("0.00", Inv) + " " + n.Etq))); }
                }
                foreach (var kv in velas) if (kv.Value.Series.ContainsKey(serie) && !esperado.ContainsKey(kv.Key)) { sobran++; if (ejemplos.Count < 4) ejemplos.Add("sobra " + Hm(TqqqHora.DeMs(kv.Key))); }
                bool bien = mal == 0 && sobran == 0 && faltan == 0;
                if (!bien) fallas++;
                P("  " + serie.PadRight(12) + " ref " + esperado.Count.ToString().PadLeft(4) + " | iguales " + ok.ToString().PadLeft(4) + " | distintas " + mal + " | faltan " + faltan + " | sobran " + sobran +
                  " | max dif " + maxD.ToString("0.000", Inv) + " pts" + (bien ? "  OK" : "  FALLA: " + string.Join(" ; ", ejemplos)));
            }
            // conv y actuales: la ultima vela con foto vigente y c (tqqq_vivo 'ultimo')
            TqqqVela u = null; foreach (var kv in velas) u = kv.Value;
            var conv = refe.GetProperty("conv");
            int nMuestras = calc.Muestras(dia).Count(m => !double.IsNaN(m.C));
            double sR = conv.GetProperty("s").GetDouble(), cR = conv.GetProperty("c").GetDouble(); int mR = conv.GetProperty("muestras").GetInt32();
            bool convOk = u != null && u.S == sR && Math.Abs(u.C - cR) <= 1e-9 && nMuestras == mR;
            if (!convOk) fallas++;
            P("  conv: s " + F(u?.S ?? double.NaN, "R") + " (ref " + sR.ToString("R", Inv) + ") | c " + F(u?.C ?? double.NaN, "R") + " (ref " + cR.ToString("R", Inv) + ") | fotos con c " + nMuestras + " (ref " + mR + ")" + (convOk ? "  OK" : "  FALLA"));
            if (!conActuales || u == null) return fallas;
            var fu = refe.GetProperty("fuentes").GetProperty("TQQQ");
            var det = calc.Detalle(u);
            bool fuOk = TqqqArchivo.Iso(u.DatoUtc).StartsWith(fu.GetProperty("dato_utc").GetString().TrimEnd('Z')) && det != null &&
                        TqqqArchivo.Iso(det.GeneradoUtc).StartsWith(fu.GetProperty("generado_utc").GetString().TrimEnd('Z')) && u.Congelada == fu.GetProperty("congelada").GetBoolean();
            if (!fuOk) fallas++;
            P("  fuente: dato " + TqqqArchivo.Iso(u.DatoUtc) + " generado " + TqqqArchivo.Iso(det?.GeneradoUtc ?? default) + " congelada " + u.Congelada + " | ref " +
              fu.GetProperty("dato_utc").GetString() + " " + fu.GetProperty("generado_utc").GetString() + " " + fu.GetProperty("congelada").GetBoolean() + (fuOk ? "  OK" : "  FALLA"));
            int okA = 0, malA = 0; var ej = new List<string>();
            var usados = new HashSet<string>();
            foreach (var a in refe.GetProperty("actuales").EnumerateArray())
            {
                string serie = a.GetProperty("serie").GetString(), etq = a.GetProperty("etq").GetString();
                double k = a.GetProperty("strike").GetDouble(), p = a.GetProperty("precio").GetDouble();
                double g = a.GetProperty("gex_musd").ValueKind == JsonValueKind.Number ? a.GetProperty("gex_musd").GetDouble() : double.NaN;
                bool hallado = false;
                if (u.Series.TryGetValue(serie, out var lv))
                    for (int j = 0; j < lv.Length; j++)
                    {
                        string id = serie + "#" + j; if (usados.Contains(id)) continue;
                        if (lv[j].Etq == etq && u.Strikes[serie][j] == k && Math.Abs(lv[j].Precio - p) <= 0.01 &&
                            (double.IsNaN(g) ? double.IsNaN(lv[j].GexM) : Math.Abs(lv[j].GexM - g) <= 0.005)) { hallado = true; usados.Add(id); break; }
                    }
                if (hallado) okA++; else { malA++; ej.Add(serie + " " + etq + " " + k + " -> " + p); }
                if (ej.Count == 0 && okA <= 7)
                    P("    " + serie.PadRight(12) + etq.PadRight(8) + " K " + k.ToString("0.####", Inv).PadRight(16) + " NQ " + p.ToString("0.00", Inv).PadLeft(9) +
                      " | GEX " + (double.IsNaN(g) ? "-" : g.ToString("0.00", Inv)) + " M  igual" + (det != null ? "   [" + det.TextoK(k) + "]" : ""));
            }
            int nMias = u.Series.Sum(s => s.Value.Length);
            bool actOk = malA == 0 && nMias == okA;
            if (!actOk) fallas++;
            P("  actuales: " + okA + " iguales, " + malA + " distintos, mios " + nMias + (actOk ? "  OK" : "  FALLA: " + string.Join(" ; ", ej)));
            return fallas;
        }

        private static int CompararSoloPresentes(string nombre, JsonElement refe, IDictionary<long, TqqqVela> velas)
        {
            int ok = 0, mal = 0; var hist = refe.GetProperty("historia");
            foreach (var serie in CalculoTqqq.Series)
            {
                if (!hist.TryGetProperty(serie, out var arr)) continue;
                foreach (var x in arr.EnumerateArray())
                {
                    long k = x[0].GetInt64();
                    if (!velas.TryGetValue(k, out var v)) continue;
                    if (!v.Series.TryGetValue(serie, out var lv) || lv.Length != x[1].GetArrayLength()) { mal++; continue; }
                    bool igual = true;
                    for (int j = 0; j < lv.Length; j++) if (Math.Abs(lv[j].Precio - x[1][j][0].GetDouble()) > 0.01) igual = false;
                    if (igual) ok++; else mal++;
                }
            }
            P("  " + nombre + ": velas en modo rueda contra la referencia: " + ok + " iguales, " + mal + " distintas" + (mal == 0 && ok > 0 ? "  OK" : "  FALLA"));
            return mal == 0 && ok > 0 ? 0 : 1;
        }

        private static int Chequeo(string que, bool ok) { P("  [" + (ok ? "OK" : "FALLA") + "] " + que); return ok ? 0 : 1; }

        // ================================================================== util
        private static JsonElement Leer(string ruta) { using var d = JsonDocument.Parse(File.ReadAllText(ruta)); return d.RootElement.Clone(); }
        private static long Ms(string utc) => TqqqHora.Ms(DateTime.SpecifyKind(DateTime.ParseExact(utc, "yyyy-MM-dd HH:mm:ss", Inv), DateTimeKind.Utc));
        private static string Hm(DateTime t) => t == default ? "-" : t.ToString("MM-dd HH:mm:ss", Inv);
        private static string F(double v, string f = "0.00") => double.IsNaN(v) ? "NaN" : v.ToString(f, Inv);
        private static string Limpia(string d) { try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch { } Directory.CreateDirectory(d); return d; }
        private static string Aqui()
        {
            var d = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && d != null; i++) { if (File.Exists(Path.Combine(d, "ArnesTqqq.csproj"))) return d; d = Path.GetDirectoryName(d.TrimEnd(Path.DirectorySeparatorChar)); }
            return Directory.GetCurrentDirectory();
        }
    }
}
