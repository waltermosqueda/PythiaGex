using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace PythiaGexTres
{
    /// <summary>
    /// SIMULADOR 3.7.0 (08-10-2026). Corre la seleccion de la propuesta (Seleccion37 + Conversion37 + el nucleo, los MISMOS archivos que
    /// compila el indicador) sobre datos reales y la compara con lo MEDIDO por la auditoria (laboratorio/tres/auditoria_0810/resultados):
    ///   1 islas de NDX (rev_02_isla_ndx.txt)        2 multiplicador de NQ (cod_08_recalculo_perfil_nq.txt)
    ///   3 salto de OI de Rithmic (backtest_familia.md: 01:24:00 / 01:30:26 UTC)    4 D1/D2 = regla de la 2.0 (preview/regla_2_0_noche_0810.md)
    ///   5 tunel C2 (rev_06_tunel_0632.txt)          6 base NDX y razon QQQ sincronizadas (backtest_familia.md: 244,32 / 243,07 / 41,4342)
    ///   7 vencimiento, vigencia de CBOE, rotulos del borde.
    /// Solo lectura (viva3, cboe-local, cinta; FileShare.ReadWrite porque la 3.0 sigue escribiendo). Prioridad baja. Sin ATAS.
    /// Uso: dotnet run -c Release --project atas/_propuesta_3_7/Simulador37 [--sin-conversion] [--exportar [--sesion yyyy-MM-dd]] [--sesiones [--todas [--cerco-no]]]
    ///   --sesiones: Sesiones.cs, niveles por minuto y marcas por vela de las sesiones 10-06 tarde, 10-07 y 10-08 en Simulador37/salidas/.
    ///   --todas (3.7.1): todas las familias prendidas (F1, cruces de NQ / NDX / QQQ, capa QQQ) en salidas_todas/, para la ablacion
    ///   (3.7.2: con --todas las marcas salen CRUDAS: radio 50, sin juntas ni tope por vela, para emular reglas en eleccion_372.py / eleccion_373.py)
    ///   (laboratorio/tres/auditoria_0810/ablacion_371.py); con --cerco-no el cruce de NDX es el mas cercano (como la 2.0) en salidas_todas_cercano/.
    /// </summary>
    internal static partial class Program
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly string APP = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS");
        static readonly string VIVA = Path.Combine(APP, "PythiaGex3", "viva");
        static readonly string CBOE = Path.Combine(APP, "PythiaGex", "cboe-local");
        static readonly string CINTA = @"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\profundidad\estado\cinta";
        static int _ok, _falla;

        static void Check(string nombre, bool cond, string detalle)
        {
            if (cond) _ok++; else _falla++;
            Console.WriteLine((cond ? "  [OK]    " : "  [FALLA] ") + nombre + " :: " + detalle);
        }

        static string F(double x, string f = "0.00") => double.IsNaN(x) ? "NaN" : x.ToString(f, Inv);

        static int Main(string[] args)
        {
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            Console.OutputEncoding = Encoding.UTF8;
            var t0 = DateTime.UtcNow;
            Console.WriteLine("Simulador37 - seleccion 3.7.3 sin ATAS - " + t0.ToString("yyyy-MM-dd HH:mm:ss", Inv) + " UTC");
            Correr("1 islas NDX", Prueba1_Islas);
            Correr("2 multiplicador NQ", Prueba2_Multiplicador);
            Correr("3 salto de OI", Prueba3_SaltoOi);
            Correr("4 D1/D2 regla 2.0", Prueba4_DosMasGrandes);
            Correr("5 tunel C2", Prueba5_TunelC2);
            if (!args.Contains("--sin-conversion")) Correr("6 conversion sincronizada", Prueba6_Conversion);
            Correr("7 varias", Prueba7_Varias);
            if (args.Contains("--exportar")) Correr("8 exportar los niveles 3.7.0 de la sesion (para la auditoria visual)", () => Exportar(args));
            if (args.Contains("--sesiones")) Correr("9 sesiones 10-06 tarde, 10-07 y 10-08: niveles por minuto y marcas por vela (3.7.3: radio 50, juntas 8, tope 5 rayas por vela, renglon por libro) en salidas/", () => Sesiones(args));
            Console.WriteLine();
            Console.WriteLine("RESUMEN: " + _ok + " OK, " + _falla + " FALLA, " + (DateTime.UtcNow - t0).TotalSeconds.ToString("0", Inv) + " s");
            return _falla == 0 ? 0 : 1;
        }

        static void Correr(string nombre, Action a)
        {
            Console.WriteLine(); Console.WriteLine("== " + nombre);
            try { a(); } catch (Exception e) { _falla++; Console.WriteLine("  [FALLA] excepcion: " + e.GetType().Name + ": " + e.Message + " | " + e.StackTrace); }
        }

        // ------------------------------------------------------------------ lectura de viva3 (como GammaHoyTresMemoria.RebobinarViva3)

        sealed class FotoViva { public DateTime Ts; public double Futuro; public List<double[]> Filas; }

        static IEnumerable<FotoViva> LeerViva3(string dia)
        {
            var ruta = Path.Combine(VIVA, "viva3-NQ-" + dia + ".jsonl");
            if (!File.Exists(ruta)) yield break;
            using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            string l;
            while ((l = sr.ReadLine()) != null)
            {
                if (l.Length < 60 || !l.EndsWith("}")) continue;
                FotoViva f = null;
                try
                {
                    using var doc = JsonDocument.Parse(l);
                    var r = doc.RootElement;
                    if (!DateTime.TryParseExact(r.GetProperty("ts").GetString(), "yyyy-MM-dd HH:mm:ss", Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var ts)) continue;
                    var filas = new List<double[]>();
                    foreach (var x in r.GetProperty("filas").EnumerateArray())
                    {
                        var v = new double[12]; int i = 0;
                        foreach (var y in x.EnumerateArray()) { if (i < 12) v[i] = y.GetDouble(); i++; }
                        if (i >= 8) filas.Add(v);
                    }
                    f = new FotoViva { Ts = DateTime.SpecifyKind(ts, DateTimeKind.Utc), Futuro = r.GetProperty("futuro").GetDouble(), Filas = filas };
                }
                catch { }
                if (f != null) yield return f;
            }
        }

        static Feed.Cadena CadenaDeViva(FotoViva f, double mult)
        {
            var diasV = f.Filas.Where(x => x[4] > 0).Select(x => Math.Round(x[1], 4)).Distinct().OrderBy(x => x).ToList();
            if (diasV.Count == 0) return null;
            var idx = new Dictionary<double, int>(); for (int i = 0; i < diasV.Count; i++) idx[diasV[i]] = i;
            var porClave = new Dictionary<(double, int), Feed.Fila>();
            foreach (var x in f.Filas)
            {
                double K = x[0], iv = x[4], oi = x[3], vol = x[7]; bool call = x[2] >= 0.5;
                if (iv <= 0 || (oi <= 0 && vol <= 0)) continue;
                if (!idx.TryGetValue(Math.Round(x[1], 4), out int v)) continue;
                if (!porClave.TryGetValue((K, v), out var fila)) { fila = new Feed.Fila { K = K, K0 = x[11], V = v }; porClave[(K, v)] = fila; }
                if (call) { fila.OiC = oi; fila.IvC = iv; fila.VolC = vol; } else { fila.OiP = oi; fila.IvP = iv; fila.VolP = vol; }
            }
            if (porClave.Values.Where(x => x.IvC > 0 && x.IvP > 0).Select(x => x.K).Distinct().Count() < 8) return null;
            return new Feed.Cadena
            {
                Ts = f.Ts.ToString("yyyy-MM-dd HH:mm:ss", Inv), SpotIdx = f.Futuro, Dias = diasV.ToArray(), Filas = porClave.Values.OrderBy(x => x.K).ThenBy(x => x.V).ToList(),
                Base = 0, BaseConfiable = true, RecibidoUtc = f.Ts, GeneradoUtc = f.Ts, EsFuturo = true, Fuente = "NQ viva3 (simulador)", Multiplicador = mult,
            };
        }

        /// <summary>El nucleo con los ajustes del indicador 3.7.0 para NQ (Hoy, radio 100, empate 20, noche Volumen, zero interpolado,
        /// regla DosMasGrandes: sin una por lado ni centroide).</summary>
        static GammaHoyNucleo NucleoNq()
        {
            var n = new GammaHoyNucleo();
            var a = n.A;
            a.Horizonte = GammaHoyNucleo.HorizonteVenc.Hoy; a.CuantasDominantes = 2; a.RadioDominantesPct = 2.0; a.RadioDominantesMaxPts = 100;
            a.EmpatePct = 20; a.DominantesDeNoche = GammaHoyNucleo.NocheDominantes.Volumen; a.ZeroInterpolado = true; a.UnaPorLado = false; a.Centroide = false;
            return n;
        }

        static FotoViva FotoCerca(string dia, DateTime t)
        {
            FotoViva mejor = null;
            foreach (var f in LeerViva3(dia)) if (mejor == null || Math.Abs((f.Ts - t).TotalSeconds) < Math.Abs((mejor.Ts - t).TotalSeconds)) mejor = f;
            return mejor;
        }

        // ------------------------------------------------------------------ 1. islas (C3) en NDX

        static void Prueba1_Islas()
        {
            var objetivo = new DateTime(2026, 10, 8, 6, 41, 21, DateTimeKind.Utc);
            var ruta = Path.Combine(CBOE, "cadena-NQ-2026-10-08.jsonl.gz");
            string linea = null; double mejor = double.MaxValue;
            using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var gz = new GZipStream(fs, CompressionMode.Decompress))
            using (var sr = new StreamReader(gz, Encoding.UTF8))
            {
                string l;
                try
                {
                    while ((l = sr.ReadLine()) != null)
                    {
                        int i = l.IndexOf("\"generado\":\"", StringComparison.Ordinal); if (i < 0) continue;
                        var g = DateTimeOffset.Parse(l.Substring(i + 12, 25), Inv).UtcDateTime;
                        double d = Math.Abs((g - objetivo).TotalSeconds);
                        if (d < mejor) { mejor = d; linea = l; }
                    }
                }
                catch (InvalidDataException) { }
            }
            if (linea == null) { Check("cadena NDX 06:41:21Z", false, "no esta en " + ruta); return; }
            using var doc = JsonDocument.Parse(linea);
            var r = doc.RootElement; var c = r.GetProperty("cadena");
            var gen = DateTimeOffset.Parse(r.GetProperty("generado").GetString(), Inv).UtcDateTime;
            var dias = c.GetProperty("vencimientos").EnumerateArray().Select(v => v.GetProperty("dias").GetDouble()).ToArray();
            var filas = new List<Feed.Fila>();
            foreach (var x in c.GetProperty("filas").EnumerateArray())
            {
                var v = x.EnumerateArray().Select(y => y.GetDouble()).ToArray(); if (v.Length < 8) continue;
                int vi = (int)v[1]; if (vi < 0 || vi >= dias.Length) continue;
                filas.Add(new Feed.Fila { K = v[0], V = vi, OiC = v[2], OiP = v[3], IvC = v[4], IvP = v[5], VolC = v[6], VolP = v[7] });
            }
            const double BASE = 238.36, S = 31031.39;   // rev_02_isla_ndx.txt: base de la linea, S indice
            var cad = new Feed.Cadena { Ts = c.GetProperty("ts").GetString(), SpotIdx = c.GetProperty("spot_idx").GetDouble(), Dias = dias, Filas = filas, EsFuturo = false, PorRazon = false, Base = BASE, BaseConfiable = true, GeneradoUtc = gen, Multiplicador = 100 };
            var n = NucleoNq();
            double fut = S + BASE;
            var ahora = gen.AddDays(dias[0] - 0.5498);   // envejecida como en rev_02 (dias 0,5498)
            var L = n.Calcular(cad, fut, ahora);
            Console.WriteLine("  cadena NDX generado " + gen.ToString("HH:mm:ss", Inv) + "Z, filas " + filas.Count + ", S " + F(L.S) + ", base " + F(L.Base) + " (" + L.BaseOrigen + ")");
            var crudo = Seleccion37.CrucesPerfil(L.Perfil, true, true, 0).Select(z => z.Z).Where(z => Math.Abs(z - fut) <= 120).ToList();
            var limpio = Seleccion37.CrucesPerfil(L.Perfil, true, true, Seleccion37.ISLA, out int islas).Select(z => z.Z).Where(z => Math.Abs(z - fut) <= 120).ToList();
            Console.WriteLine("  cruces vol a +-120 SIN filtro: " + string.Join(" ", crudo.Select(z => F(z))));
            Console.WriteLine("  cruces vol a +-120 CON islas : " + string.Join(" ", limpio.Select(z => F(z))) + "   (islas en todo el perfil: " + islas + ")");
            var s31040 = L.Perfil.FirstOrDefault(s => s.K == 31040);
            Console.WriteLine("  strike 31040 (31278,36): GEX vol " + F((s31040?.GexVol ?? double.NaN) / 1e6, "0.00") + " M (rev_02: 1,21 M entre -27,93 y -100,00)");
            var esperados = new[] { 31224.83, 31230.9, 31277.94, 31278.48, 31329.92, 31346.54, 31350.94, 31375.04, 31379.35 };
            Check("crudo = rev_02", esperados.All(e => crudo.Any(z => Math.Abs(z - e) <= 0.06)) && crudo.Count == esperados.Length, "esperado " + string.Join(" ", esperados.Select(e => F(e))));
            Check("isla 31040 sacada", !limpio.Any(z => Math.Abs(z - 31277.94) <= 0.06 || Math.Abs(z - 31278.48) <= 0.06) && limpio.Count == esperados.Length - 2, "quedan " + limpio.Count + " cruces");
            var techoPiso = Seleccion37.Cerco(limpio, 31267.25);
            Console.WriteLine("  cerco con el precio 31.267,25: techo " + F(techoPiso.Techo) + " piso " + F(techoPiso.Piso) + " (sin el filtro el techo era 31.277,94)");
        }

        // ------------------------------------------------------------------ 2. multiplicador (C4)

        static void Prueba2_Multiplicador()
        {
            var f = FotoCerca("2026-10-08", new DateTime(2026, 10, 8, 6, 40, 11, DateTimeKind.Utc));
            if (f == null) { Check("foto 06:40:11", false, "sin viva3 del 08-10"); return; }
            const double FUT = 31275.25;   // precio del grafico del AUDIT3 (cod_08)
            var L20 = NucleoNq().Calcular(CadenaDeViva(f, 20), FUT, f.Ts);
            var L100 = NucleoNq().Calcular(CadenaDeViva(f, 100), FUT, f.Ts);
            double G(GammaHoyNucleo.Lectura L, double k, bool oi) { var s = L.Perfil.FirstOrDefault(x => x.K == k); return s == null ? double.NaN : (oi ? s.GexOi : s.GexVol) / 1e6; }
            Console.WriteLine("  foto " + f.Ts.ToString("HH:mm:ss", Inv) + "Z; K 31250 GexOi x100 " + F(G(L100, 31250, true), "0.0") + " M, x20 " + F(G(L20, 31250, true), "0.0") + " M (cod_08: -47,8 / -9,6); K 31300 x20 " + F(G(L20, 31300, true), "0.0") + " M (cod_08: -13,3)");
            Console.WriteLine("  netVol x100 " + F(L100.NetVol / 1e9, "0.000") + " B, x20 " + F(L20.NetVol / 1e9, "0.000") + " B (cod_08: -1,529 / -0,306)");
            Check("x20 = x100 / 5 (K 31250 OI)", Math.Abs(G(L20, 31250, true) * 5 - G(L100, 31250, true)) < 1e-6 && Math.Abs(G(L20, 31250, true) - (-9.6)) <= 0.1, F(G(L20, 31250, true), "0.00") + " M");
            Check("netVol x20 = -0,306 B", Math.Abs(L20.NetVol / 1e9 - (-0.306)) <= 0.002, F(L20.NetVol / 1e9, "0.000"));
            bool igual = Math.Abs(L20.ZeroVol - L100.ZeroVol) < 1e-6 && Math.Abs(L20.ZeroOi - L100.ZeroOi) < 1e-6 && L20.MpVol == L100.MpVol && L20.MnVol == L100.MnVol && L20.MpOi == L100.MpOi && L20.MnOi == L100.MnOi;
            var d20 = Seleccion37.DosMasGrandes(L20.Perfil, FUT, Seleccion37.RadioDominantes(FUT, 100), true); var d100 = Seleccion37.DosMasGrandes(L100.Perfil, FUT, Seleccion37.RadioDominantes(FUT, 100), true);
            igual &= d20.Count == d100.Count && d20.Zip(d100, (a, b) => a.Fut == b.Fut).All(x => x);
            Check("los niveles no se mueven con el multiplicador", igual, "zeroVol " + F(L20.ZeroVol) + " zeroOi " + F(L20.ZeroOi) + " M+vol " + F(L20.MpVol) + " M-vol " + F(L20.MnVol) + " D " + string.Join("/", d20.Select(d => F(d.Fut))));
        }

        // ------------------------------------------------------------------ 3. salto de OI (C1)

        static void Prueba3_SaltoOi()
        {
            var det = new SaltoOi37();
            var saltos = new List<(DateTime Ses, DateTime T, int Com, int Cam)>();
            var consultas = new List<DateTime> { new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 8, 1, 20, 0, DateTimeKind.Utc), new DateTime(2026, 10, 8, 1, 35, 0, DateTimeKind.Utc), new DateTime(2026, 10, 8, 5, 0, 0, DateTimeKind.Utc) };
            var resp = new Dictionary<DateTime, string>();
            int fotos = 0;
            foreach (var dia in new[] { "2026-10-06", "2026-10-07", "2026-10-08" })
                foreach (var f in LeerViva3(dia))
                {
                    fotos++;
                    var mapa = new Dictionary<string, double>();
                    foreach (var x in f.Filas) mapa[x[0].ToString("0.##", Inv) + "|" + f.Ts.AddDays(x[1]).ToString("MM-dd", Inv) + "|" + (x[2] >= 0.5 ? "C" : "P")] = x[3];
                    foreach (var q in consultas.Where(q => !resp.ContainsKey(q) && f.Ts > q).ToList()) { bool v = det.OiDeAnteayer(q, out var m); resp[q] = (v ? "ANTEAYER" : "ayer") + " (" + m + ")"; }
                    if (det.Observar(f.Ts, mapa)) saltos.Add((det.Sesion, f.Ts, det.UltimoComunes, det.UltimoCambiados));
                }
            Console.WriteLine("  " + fotos + " fotos viva3 (06..08-10)");
            foreach (var s in saltos) Console.WriteLine("  salto sesion " + s.Ses.ToString("yyyy-MM-dd", Inv) + ": " + s.T.ToString("HH:mm:ss", Inv) + " UTC (" + Seleccion37.ANy(s.T).ToString("HH:mm:ss", Inv) + " NY), " + s.Cam + " de " + s.Com + " contratos cambiaron");
            foreach (var kv in resp) Console.WriteLine("  OI de NQ a las " + kv.Key.ToString("MM-dd HH:mm", Inv) + " UTC: " + kv.Value);
            var s07 = saltos.FirstOrDefault(s => s.Ses == new DateTime(2026, 10, 7)); var s08 = saltos.FirstOrDefault(s => s.Ses == new DateTime(2026, 10, 8));
            Check("salto 10-07 = 01:24:00 UTC (backtest_familia)", s07.T == new DateTime(2026, 10, 7, 1, 24, 0), s07.T == default(DateTime) ? "no detectado" : s07.T.ToString("HH:mm:ss", Inv));
            Check("salto 10-08 = 01:30:26 UTC (backtest_familia)", s08.T == new DateTime(2026, 10, 8, 1, 30, 26), s08.T == default(DateTime) ? "no detectado" : s08.T.ToString("HH:mm:ss", Inv));
            Check("01:20 UTC anteayer / 01:35 ayer", resp.TryGetValue(consultas[1], out var a1) && a1.StartsWith("ANTEAYER") && resp.TryGetValue(consultas[2], out var a2) && a2.StartsWith("ayer"), "ver arriba");
        }

        // ------------------------------------------------------------------ 4. D1/D2 = regla de la 2.0 (D2)

        static void Prueba4_DosMasGrandes()
        {
            var f = FotoCerca("2026-10-08", new DateTime(2026, 10, 8, 7, 10, 0, DateTimeKind.Utc));
            if (f == null) { Check("foto 07:10", false, "sin viva3"); return; }
            var ticks = LeerCinta(new[] { "2026-10-08" });
            double fut = PrecioEn(ticks, f.Ts); if (double.IsNaN(fut)) fut = f.Futuro;
            var n = NucleoNq();
            var L = n.Calcular(CadenaDeViva(f, 20), fut, f.Ts);
            double radio = Seleccion37.RadioDominantes(fut, 100);
            var d = Seleccion37.PorCercania(Seleccion37.DosMasGrandes(L.Perfil, fut, radio, true), fut);
            Console.WriteLine("  foto " + f.Ts.ToString("HH:mm:ss", Inv) + "Z, precio " + F(fut) + ", radio " + F(radio) + ": D1 " + F(d.ElementAtOrDefault(0).Fut) + " (" + F(d.ElementAtOrDefault(0).Gex / 1e6, "0.0") + " M x20), D2 " + F(d.ElementAtOrDefault(1).Fut) + " (" + F(d.ElementAtOrDefault(1).Gex / 1e6, "0.0") + " M x20)");
            Console.WriteLine("  nucleo con UnaPorLado/Centroide apagados (la 2.0): " + string.Join(" / ", L.Doms.Select(x => F(x.Fut))));
            Check("= nucleo de la 2.0", L.Doms.Select(x => x.Fut).OrderBy(x => x).SequenceEqual(d.Select(x => x.Fut).OrderBy(x => x)), "mismos strikes");
            Check("= preview 07:10 (31.250 / 31.200)", d.Select(x => x.Fut).OrderBy(x => x).SequenceEqual(new[] { 31200.0, 31250.0 }), string.Join("/", d.Select(x => F(x.Fut))));
        }

        // ------------------------------------------------------------------ 5. tunel C2

        static void Prueba5_TunelC2()
        {
            var f1 = FotoCerca("2026-10-08", new DateTime(2026, 10, 8, 6, 32, 0, DateTimeKind.Utc));
            var f2 = FotoCerca("2026-10-08", new DateTime(2026, 10, 8, 6, 40, 11, DateTimeKind.Utc));
            var st = new Seleccion37.EstadoTunel();
            List<Seleccion37.Cruce> Cruces(FotoViva f, out GammaHoyNucleo.Lectura L)
            {
                L = NucleoNq().Calcular(CadenaDeViva(f, 20), f.Futuro, f.Ts);
                double lim = L.S * 0.002; double fu = f.Futuro;
                return Seleccion37.CrucesPerfil(L.Perfil, true, true).Where(c => Math.Abs(c.Z - fu) <= lim).ToList();
            }
            var c1 = Cruces(f1, out var L1);
            var d1 = Seleccion37.TunelCruces(c1, f1.Futuro, true, st, out bool re1);
            Console.WriteLine("  " + f1.Ts.ToString("HH:mm:ss", Inv) + "Z F " + F(f1.Futuro) + " cruces vol " + string.Join(" ", c1.Select(c => F(c.Z))) + " -> techo " + F(st.Techo) + " (" + F(d1.FirstOrDefault().Gex / 1e6, "0.0") + " M) piso " + F(st.Piso));
            Check("06:32: techo 31.287,12 / piso 31.226,66 (rev_06)", Math.Abs(st.Techo - 31287.12) <= 0.06 && Math.Abs(st.Piso - 31226.66) <= 0.06, F(st.Techo) + "/" + F(st.Piso));
            var c2 = Cruces(f2, out var L2);
            var d2 = Seleccion37.TunelCruces(c2, f2.Futuro, true, st, out bool re2);
            Console.WriteLine("  " + f2.Ts.ToString("HH:mm:ss", Inv) + "Z F " + F(f2.Futuro) + " cruces vol " + string.Join(" ", c2.Select(c => F(c.Z))) + " -> techo " + F(st.Techo) + " piso " + F(st.Piso) + " (reeligio " + re2 + "; la 3.0 sostenia 31.287,11 sin cruce)");
            Check("06:40: el techo sin cruce NO se sostiene", double.IsNaN(st.Techo) || Math.Abs(st.Techo - 31287.11) > 1.0, "techo " + F(st.Techo));
            Check("06:40: el piso sigue a su cruce (31.226,64)", Math.Abs(st.Piso - 31226.64) <= 0.06, "piso " + F(st.Piso));
            Check("Gex = fuerza real (no 1e6)", d2 != null && d2.All(x => Math.Abs(Math.Abs(x.Gex) - 1e6) > 1), string.Join("/", (d2 ?? new List<(double, double)>()).Select(x => F(x.Gex / 1e6, "0.00") + "M")));
        }

        // ------------------------------------------------------------------ 6. base NDX / razon QQQ sincronizadas (C5)

        static (long[] T, double[] P) LeerCinta(IEnumerable<string> sesiones)
        {
            var t = new List<long>(); var p = new List<double>();
            foreach (var s in sesiones)
            {
                var vt = new List<(long, double)>(); var rt = new List<(long, double)>();
                foreach (var (ruta, dest) in new[] { (Path.Combine(CINTA, "cinta-NQ-" + s + ".csv"), vt), (Path.Combine(CINTA, "cinta-NQ-" + s + "-relleno.csv"), rt) })
                {
                    if (!File.Exists(ruta)) continue;
                    using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var sr = new StreamReader(fs);
                    string l;
                    while ((l = sr.ReadLine()) != null)
                    {
                        if (l.Length < 5 || l[0] == 't') continue;
                        int a = l.IndexOf(','); if (a < 0) continue; int b = l.IndexOf(',', a + 1); if (b < 0) b = l.Length;
                        if (long.TryParse(l.AsSpan(0, a), NumberStyles.Integer, Inv, out var tt) && double.TryParse(l.AsSpan(a + 1, b - a - 1), NumberStyles.Float, Inv, out var pp)) dest.Add((tt, pp));
                    }
                }
                long minV = vt.Count > 0 ? vt.Min(x => x.Item1) : long.MaxValue;
                foreach (var x in rt) if (x.Item1 < minV) { t.Add(x.Item1); p.Add(x.Item2); }
                foreach (var x in vt) { t.Add(x.Item1); p.Add(x.Item2); }
            }
            var idx = Enumerable.Range(0, t.Count).OrderBy(i => t[i]).ToArray();   // estable
            return (idx.Select(i => t[i]).ToArray(), idx.Select(i => p[i]).ToArray());
        }

        /// <summary>backtest_familia.Precio: el ultimo tick de los 120 s previos (NaN si no hay).</summary>
        static double PrecioEn((long[] T, double[] P) c, DateTime tUtc)
        {
            long ms = (long)(tUtc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            int j = Array.BinarySearch(c.T, ms);
            if (j < 0) j = ~j - 1; else { while (j + 1 < c.T.Length && c.T[j + 1] == ms) j++; }
            if (j < 0 || ms > c.T[c.T.Length - 1] || ms - c.T[j] > 120_000) return double.NaN;
            return c.P[j];
        }

        static void Prueba6_Conversion()
        {
            var cinta = LeerCinta(new[] { "2026-10-07", "2026-10-08" });
            Console.WriteLine("  cinta: " + cinta.T.Length + " ticks (" + DateTimeOffset.FromUnixTimeMilliseconds(cinta.T.First()).UtcDateTime.ToString("MM-dd HH:mm", Inv) + " .. " + DateTimeOffset.FromUnixTimeMilliseconds(cinta.T.Last()).UtcDateTime.ToString("MM-dd HH:mm", Inv) + " UTC)");
            var dias = new[] { new DateTime(2026, 10, 6), new DateTime(2026, 10, 7), new DateTime(2026, 10, 8) };
            var fotos = new List<(bool Qqq, DateTime Gen, DateTime Ts, double Spot)>();
            foreach (var c in ArchivoCboe37.Cabeceras(CBOE, "NQ", dias)) fotos.Add((false, c.Gen, c.Ts, c.Spot));
            foreach (var c in ArchivoCboe37.Cabeceras(CBOE, "QQQ", dias)) fotos.Add((true, c.Gen, c.Ts, c.Spot));
            fotos = fotos.OrderBy(x => x.Gen).ThenBy(x => x.Ts).ToList();
            Console.WriteLine("  fotos CBOE: NDX " + fotos.Count(x => !x.Qqq) + ", QQQ " + fotos.Count(x => x.Qqq));
            var ndx = new ConversionCboe37(false); var qqq = new ConversionCboe37(true); var pares = new ParesFamilia37();
            var venc = Seleccion37.VencimientoTrimestralUtc("MNQZ6", new DateTime(2026, 10, 8));
            Console.WriteLine("  vencimiento MNQZ6: " + venc.ToString("yyyy-MM-dd HH:mm", Inv) + " UTC (backtest: 2026-12-18 14:30)");
            // backtest_familia.md (sesion 08-10): 'min con libro noche ... NDX 817': la mediana 243,07 es sobre 817 minutos desde las 22:00 UTC
            var ini = new DateTime(2026, 10, 7, 22, 0, 0, DateTimeKind.Utc); var fin = ini.AddMinutes(816);
            var basesNoche = new List<double>(); var razonesNoche = new List<double>(); var difs = new List<double>();
            int i = 0, conPrecio = 0;
            for (var t = ini; t <= fin; t = t.AddMinutes(1))
            {
                while (i < fotos.Count && fotos[i].Gen <= t)
                {
                    var x = fotos[i++];
                    var tp = x.Ts.AddSeconds(-ConversionCboe37.RETRASO_S);
                    double p = PrecioEn(cinta, tp); if (!double.IsNaN(p)) conPrecio++;
                    var cv = x.Qqq ? qqq : ndx;
                    if (!cv.Observar(x.Gen, x.Ts, x.Spot, p, "Z6")) continue;
                    if (x.Qqq) pares.Qqq(x.Gen, tp, x.Spot, cv.UltimaViva); else pares.Ndx(x.Gen, tp, x.Spot, cv.UltimaViva);
                    if (t == ini) { }
                }
                if (t == ini)
                {
                    var r0 = ndx.Calcular(t, "Z6", venc);
                    Console.WriteLine("  al empezar la noche: " + ndx.Resumen() + " | " + r0.Texto);
                }
                var vb = ndx.Calcular(t, "Z6", venc); var vr = qqq.Calcular(t, "Z6", venc);
                if (!double.IsNaN(vb.V)) basesNoche.Add(vb.V);
                if (!double.IsNaN(vr.V)) razonesNoche.Add(vr.V);
                var rp = pares.RPar();
                var ultimoNdx = fotos.Take(i).LastOrDefault(z => !z.Qqq);
                double be = ParesFamilia37.BaseEquivalente(ultimoNdx.Spot, vr.V, rp.R);
                if (!double.IsNaN(be) && !double.IsNaN(vb.VSinCarry)) difs.Add(vb.VSinCarry - be);
                if ((t.Minute == 0 && t.Hour % 2 == 0) || (t.Hour >= 7 && t.Hour < 12 && t.Minute % 20 == 0)) Console.WriteLine("  " + t.ToString("MM-dd HH:mm", Inv) + "Z base NDX " + F(vb.V) + " [" + vb.Modo + "] (sin carry " + F(vb.VSinCarry) + ") razon QQQ " + F(vr.V, "0.0000") + " (ultima viva " + qqq.UltimaMuestraSpotUtc.ToString("HH:mm", Inv) + "Z, spot " + F(fotos.Take(i).LastOrDefault(z => z.Qqq).Spot) + ") base eq. QQQ " + F(be) + " (R " + F(rp.R, "0.0000") + ", " + rp.N + " pares)");
            }
            Console.WriteLine("  fotos con precio del MNQ en ts-900 s: " + conPrecio);
            Console.WriteLine("  NDX: " + ndx.Resumen());
            Console.WriteLine("  QQQ: " + qqq.Resumen());
            double medB = Seleccion37.Mediana(basesNoche), medR = Seleccion37.Mediana(razonesNoche), medD = Seleccion37.Mediana(difs);
            Console.WriteLine("  noche 08-10 (22:00 UTC + 817 min): base NDX mediana " + F(medB) + " (" + basesNoche.Count + " min, de " + F(basesNoche.FirstOrDefault()) + " a " + F(basesNoche.LastOrDefault()) + "), razon QQQ mediana " + F(medR, "0.0000") + " (" + razonesNoche.Count + " min); base NDX - base eq. QQQ: mediana " + F(medD) + ", max |dif| " + F(difs.Count > 0 ? difs.Max(Math.Abs) : double.NaN));
            var v1007 = ndx.Calcular(new DateTime(2026, 10, 7, 20, 0, 0, DateTimeKind.Utc), "Z6", default(DateTime));
            Check("rueda 07-10 sincronizada = 244,32 con 305 muestras (backtest_familia.md)", Math.Abs(v1007.V - 244.32) <= 0.3 && Math.Abs(v1007.N - 305) <= 15, v1007.Texto);
            Check("base de noche 08-10 = 243,07 (backtest_familia.md, mismos 817 min)", Math.Abs(medB - 243.07) <= 0.05, F(medB));
            Check("razon QQQ de noche = 41,4342 (backtest_familia.md)", Math.Abs(medR - 41.4342) <= 0.0006, F(medR, "0.0000"));
            Check("alarma: base NDX (sin carry) y la equivalente de QQQ a <= 2 pts toda la noche", difs.Count > 0 && difs.Max(Math.Abs) <= 2.0, "mediana " + F(medD) + ", max |dif| " + F(difs.Count > 0 ? difs.Max(Math.Abs) : double.NaN) + " (la alarma de C5 no se dispara esta noche)");
            // persistencia: ida y vuelta
            var js = ndx.Serializar(fin); var ndx2 = new ConversionCboe37(false); ndx2.Cargar(js, out var msg);
            var a = ndx.Calcular(fin, "Z6", venc); var b = ndx2.Calcular(fin, "Z6", venc);
            Check("persistencia NDX: ida y vuelta da la misma base de noche", Math.Abs(a.V - b.V) < 1e-9 || (a.Modo == "dia"), F(a.V) + " vs " + F(b.V) + " [" + msg + "]");
            Check("roll: otro contrato no tiene base", double.IsNaN(ndx2.Calcular(fin, "H7", venc).V), ndx2.Calcular(fin, "H7", venc).Texto);
        }

        // ------------------------------------------------------------------ 7. varias

        static void Prueba7_Varias()
        {
            var v = Seleccion37.VencimientoTrimestralUtc("MNQZ6", new DateTime(2026, 10, 8));
            Check("vencimiento MNQZ6 = 2026-12-18 14:30 UTC", v == new DateTime(2026, 12, 18, 14, 30, 0), v.ToString("yyyy-MM-dd HH:mm", Inv));
            var gen = new DateTime(2026, 10, 8, 6, 41, 21, DateTimeKind.Utc); var ult = Seleccion37.AUtc(new DateTime(2026, 10, 7, 16, 14, 57));
            var vig = Seleccion37.VigenciaCboe(gen, ult);
            Check("C6: cadena congelada vale hasta las 13:30 UTC", vig == new DateTime(2026, 10, 8, 13, 30, 0), vig.ToString("MM-dd HH:mm", Inv));
            var vig2 = Seleccion37.VigenciaCboe(new DateTime(2026, 10, 8, 15, 0, 0), Seleccion37.AUtc(new DateTime(2026, 10, 8, 10, 45, 0)));
            Check("C6: cadena viva vale 25 min", vig2 == new DateTime(2026, 10, 8, 15, 25, 0), vig2.ToString("MM-dd HH:mm", Inv));
            var niv = new List<Seleccion37.Nivel37>
            {
                new Seleccion37.Nivel37 { Libro = "NQ", Tipo = "D1", P = 31300, Indice = 1 }, new Seleccion37.Nivel37 { Libro = "NQ", Tipo = "M+ OI", P = 31500, Indice = 2, Siempre = true },
                new Seleccion37.Nivel37 { Libro = "NQ", Tipo = "F5 muroC vol", P = 31340, Indice = 3 }, new Seleccion37.Nivel37 { Libro = "NDX", Tipo = "D2", P = 31180, Indice = 4 },
                new Seleccion37.Nivel37 { Libro = "NDX", Tipo = "D1", P = 31120, Indice = 5 }, new Seleccion37.Nivel37 { Libro = "QQQ", Tipo = "cruce", P = 31270, Indice = 6 },
            };
            var fuera = Seleccion37.FueraDeRadio(niv, 31250, 50);
            Console.WriteLine("  borde (precio 31.250, radio 50): " + string.Join(" | ", fuera.Select(n => Seleccion37.TextoBorde(n, 31250, CultureInfo.GetCultureInfo("es-AR")))));
            Check("borde: NQ mas cercano arriba + M+ OI siempre + NDX mas cercano abajo; sin los de adentro", fuera.Select(n => n.Indice).SequenceEqual(new[] { 3, 2, 4 }), string.Join(",", fuera.Select(n => n.Indice)));
            Check("radio por vela", Seleccion37.EnRadio(31300, 31260, 50) && !Seleccion37.EnRadio(31300, 31240, 50), "|p - cierre de su vela| <= radio");
            var islas = Seleccion37.Cruces(new double[] { 1, 2, 3, 4, 5 }, new double[] { -10, -8, 0.5, -9, 5 }, 0.10, out int ni);
            Check("isla simple: -8 / +0,5 / -9 no cruza", ni == 1 && islas.Count == 1 && islas[0].XIzq == 4, "islas " + ni + ", cruces " + string.Join(" ", islas.Select(c => F(c.Z))));
        }
    }
}
