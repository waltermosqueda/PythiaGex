// Program.cs — arnes de paridad del modulo fam (B3d) de PythiaGex 4.1 (08-10-2026). Sin ATAS, sin red, prioridad BelowNormal.
//   reglas    ReglasFam (MUROS, MAJORS, ZTP con islas, ZEST, CONF, FAM C1, montos, redondeos) contra el codigo REAL de Python
//             (datos/casos-reglas.json de gen_casos_reglas.py: backtest_familia + preview_niveles importados). Exacto.
//   tres      AlmacenTres (TRES_*) leyendo las estelas de la 3.0 (PythiaGex3, solo lectura) contra ref-<dia>.json 'tres'. Exacto.
//   historia  SalidaFamilia.Entrada (historia por vela m2) y Actuales contra ref-<dia>.json 'salida' (con niv y tres de la referencia).
//   motor     MotorFamilia de punta a punta con dobles (minuteros que sirven los casos sinteticos, cinta, fuentes y TQQQ falsos), con
//             las estelas reales copiadas a una carpeta temporal: tandas alineadas, persistencia, reinicio sin recalcular, historia, actuales.
//   invierno  la sesion en hora NY (corregida) contra la de paridad, del lado de verano y de invierno (01-11).
//   gm        4.1.2 (B-tres): el monto con signo de las rayas 3.0 ("gm" de la estela): unidad, lectura, capas NDX/QQQ rehechas con el nucleo
//             sobre datos reales (datos/gm) contra MAJORS_*_vol, y el factor NQ x0,2 con la estela real de la rueda (Gm.cs).
//   finde     4.1.5d/e (09-10-2026, pedido del operador: "quiero que la 4.0 se vuelva a ver, o sea que tenga memoria"): SesionFamilia.De el fin de
//             semana en modo NY (Familia41Corregida): del viernes 17:00 NY al domingo 17:59 NY la sesion es la del VIERNES (antes: la del sabado o
//             el domingo, sin datos); el domingo 18:00 NY empieza la del lunes; el cambio de horario 31-10/01-11; el modo UTC (paridad) SIN cambios
//             (dia = fecha(t + 2 h), como la vista previa). Y el motor: con ahora >= FinUtc el aviso de la foto EMPIEZA con "mercado cerrado: se
//             muestra la sesion del ...", el sabado reabre la sesion del viernes desde el archivo (memoria) y el domingo 18:00 NY pasa a la del lunes.
// Uso: dotnet run -c Release -- [reglas|tres|historia|motor|invierno|finde|gm|todo]   Salida: resultados/paridad-fam.txt; codigo 0 = todo verde.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using PythiaGexCuatro.Familia;

namespace FamTest
{
    public static partial class Program
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static string AQUI, REF, PG3_ESTELA, RES;
        static StreamWriter _out;
        static int _fallas;
        static readonly string[] DIAS = { "2026-10-07", "2026-10-08" };

        static void P(string s = "") { Console.WriteLine(s); _out?.WriteLine(s); }
        static void Falla(string s) { _fallas++; P("  FALLA " + s); }

        public static int Main(string[] args)
        {
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            AQUI = BuscarArriba(AppContext.BaseDirectory, "fam_test.csproj") ?? Directory.GetCurrentDirectory();
            REF = Path.GetFullPath(Path.Combine(AQUI, "..", "referencia"));
            PG3_ESTELA = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex3", "estela");
            RES = Path.Combine(AQUI, "resultados"); Directory.CreateDirectory(RES);
            var que = new HashSet<string>(args.Length == 0 ? new[] { "todo" } : args, StringComparer.OrdinalIgnoreCase);
            bool todo = que.Contains("todo");
            using (_out = new StreamWriter(Path.Combine(RES, "paridad-fam.txt"), false, new UTF8Encoding(false)))
            {
                P("arnes fam (B3d) " + MotorFamilia.VERSION + " | " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", Inv) + " UTC");
                P("referencia: " + REF + " | estelas 3.0 (solo lectura): " + PG3_ESTELA);
                var sw = Stopwatch.StartNew();
                if (todo || que.Contains("reglas")) Reglas();
                if (todo || que.Contains("tres")) Tres();
                if (todo || que.Contains("historia")) Historia();
                if (todo || que.Contains("motor")) Motor();
                if (todo || que.Contains("invierno")) Invierno();
                if (todo || que.Contains("finde")) Finde();
                if (todo || que.Contains("gm")) Gm();
                P();
                P(_fallas == 0 ? "RESULTADO: VERDE (0 fallas) en " + sw.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s"
                               : "RESULTADO: ROJO (" + _fallas + " fallas) en " + sw.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s");
            }
            return _fallas == 0 ? 0 : 1;
        }

        static string BuscarArriba(string dir, string archivo)
        {
            var d = new DirectoryInfo(dir);
            while (d != null) { if (File.Exists(Path.Combine(d.FullName, archivo))) return d.FullName; d = d.Parent; }
            return null;
        }

        static JsonDocument LeerJson(string ruta)
        {   // Python json.dump escribe NaN/Infinity sin comillas: System.Text.Json no los acepta -> null
            var txt = File.ReadAllText(ruta, Encoding.UTF8);
            txt = System.Text.RegularExpressions.Regex.Replace(txt, @"(?<=[\[,:\s])(NaN|-?Infinity)(?=[\],}\s])", "null");
            return JsonDocument.Parse(txt, new JsonDocumentOptions { MaxDepth = 256 });
        }

        static double Num(JsonElement e) => e.ValueKind == JsonValueKind.Number ? e.GetDouble() : double.NaN;
        static bool Igual(double a, double b) => (double.IsNaN(a) && double.IsNaN(b)) || a == b;
        static string F(double x) => double.IsNaN(x) ? "NaN" : x.ToString("R", Inv);

        // ================================================================== casos sinteticos
        static List<Caso> _casos;
        static List<Caso> Casos()
        {
            if (_casos != null) return _casos;
            var ruta = Path.Combine(AQUI, "datos", "casos-reglas.json");
            if (!File.Exists(ruta)) { Falla("no existe " + ruta + ": correr 'python -I gen_casos_reglas.py' en esta carpeta"); return _casos = new List<Caso>(); }
            var doc = LeerJson(ruta);
            _casos = new List<Caso>();
            foreach (var c in doc.RootElement.GetProperty("casos").EnumerateArray())
            {
                var x = new Caso { I = c.GetProperty("i").GetInt32(), Fut = c.GetProperty("fut").GetDouble(), Reg = c.GetProperty("reg"), Crudo = c.GetProperty("crudo") };
                foreach (var p in c.GetProperty("libros").EnumerateObject()) x.Libros[p.Name] = p.Value;
                _casos.Add(x);
            }
            return _casos;
        }

        /// <summary>Compara un RegistroMinuto con el reg de Python ({sid: [[p, e, m]]}). Devuelve las diferencias (texto).</summary>
        static List<string> CompararReg(RegistroMinuto r, JsonElement reg, JsonElement? crudo, Dictionary<string, (int ok, int mal)> tabla)
        {
            var dif = new List<string>();
            var calc = CatalogoFamilia.Calc;
            for (int s = 0; s < calc.Length; s++)
            {
                string sid = calc[s];
                bool hayP = reg.TryGetProperty(sid, out var lp) && lp.GetArrayLength() > 0;
                var mio = r?.Series?[s];
                bool hayC = mio != null && mio.Length > 0;
                if (!hayP && !hayC) continue;
                if (!tabla.TryGetValue(sid, out var t)) t = (0, 0);
                string d = null;
                if (hayP != hayC) d = sid + ": presencia python=" + hayP + " c#=" + hayC + (hayC ? " (" + string.Join(" ", mio.Select(x => x.Etq + " " + F(x.Precio))) + ")" : "")
                                     + (hayP ? " (" + lp.ToString() + ")" : "");
                else if (lp.GetArrayLength() != mio.Length) d = sid + ": cantidad python=" + lp.GetArrayLength() + " c#=" + mio.Length;
                else
                {
                    int k = 0;
                    foreach (var x in lp.EnumerateArray())
                    {
                        double pp = x[0].GetDouble(); string pe = x[1].GetString(); double pm = Num(x[2]);
                        var m = mio[k];
                        if (pe != m.Etq || !Igual(pp, m.Precio) || !Igual(pm, m.GexM))
                        {
                            string cr = "";
                            if (crudo.HasValue && crudo.Value.TryGetProperty(sid, out var cp) && k < cp.GetArrayLength())
                                cr = " | crudo python " + F(cp[k].GetDouble()) + " c# " + F(r.Crudo?[s]?[k] ?? double.NaN);
                            d = sid + "[" + k + "]: python (" + F(pp) + ", " + pe + ", " + F(pm) + ") c# (" + F(m.Precio) + ", " + m.Etq + ", " + F(m.GexM) + ")" + cr;
                            break;
                        }
                        k++;
                    }
                }
                if (d == null) t.ok++; else { t.mal++; dif.Add(d); }
                tabla[sid] = t;
            }
            return dif;
        }

        static void Tabla(Dictionary<string, (int ok, int mal)> tabla)
        {
            P("  " + "serie".PadRight(16) + "iguales".PadLeft(9) + "distintos".PadLeft(11));
            foreach (var sid in CatalogoFamilia.Calc)
                if (tabla.TryGetValue(sid, out var t)) P("  " + sid.PadRight(16) + t.ok.ToString(Inv).PadLeft(9) + t.mal.ToString(Inv).PadLeft(11));
        }

        // ================================================================== reglas
        static void Reglas()
        {
            P(); P("== reglas: ReglasFam contra backtest_familia/preview_niveles (casos sinteticos, Python real) ==");
            var casos = Casos(); if (casos.Count == 0) return;
            var tabla = new Dictionary<string, (int, int)>(); int malos = 0; var ejemplos = new List<string>();
            var sw = Stopwatch.StartNew(); long n = 0;
            foreach (var c in casos)
            {
                var bk = new List<LibroMinuto>();
                foreach (var lb in CatalogoFamilia.Libros) { var lm = c.Libro(lb, c.I); if (lm != null) bk.Add(lm); }
                var r = ReglasFam.Minuto(c.I, bk); n++;
                var dif = CompararReg(r, c.Reg, c.Crudo, tabla);
                if (dif.Count > 0) { malos++; foreach (var d in dif) if (ejemplos.Count < 25) ejemplos.Add("caso " + c.I + ": " + d); }
            }
            double us = sw.Elapsed.TotalMilliseconds * 1000.0 / Math.Max(1, n);
            Tabla(tabla);
            P("  casos " + casos.Count + ", con alguna diferencia " + malos + "; " + us.ToString("0", Inv) + " us por minuto (las 21 series, 1-3 libros)");
            foreach (var e in ejemplos) P("    " + e);
            if (malos > 0) Falla("reglas: " + malos + " casos distintos");
            // redondeo de Python: bordes conocidos
            var bordes = new (double x, int nd, double py)[] { (2.675, 2, 2.67), (1.005, 2, 1.0), (0.125, 2, 0.12), (0.375, 2, 0.38), (-0.125, 2, -0.12),
                                                             (31444.025, 2, 31444.03), (252.95, 1, 252.9), (-62.75, 1, -62.8), (1756.45, 1, 1756.5), (0.05, 1, 0.1),
                                                             (31000.005, 2, 31000.01), (30799.99996506721, 2, 30800.0) };   // valores de round() de Python 3 medidos
            foreach (var (x, nd, py) in bordes)
                if (NumPy.RoundPy(x, nd) != py) Falla("RoundPy(" + F(x) + ", " + nd + ") = " + F(NumPy.RoundPy(x, nd)) + ", Python " + F(py));
        }

        // ================================================================== tres
        static AlmacenTres TresDe(string dia, string carpeta, SesionFamilia ses)
        {
            var at = new AlmacenTres(ses.IniUtc, ses.FinUtc);
            at.LeerArchivos(carpeta, dia);
            return at;
        }

        static void Tres()
        {
            P(); P("== tres: AlmacenTres (estelas de la 3.0) contra ref 'tres' (extremos_rebote.niveles_estela) ==");
            foreach (var dia in DIAS)
            {
                var rr = Path.Combine(REF, "ref-" + dia + ".json");
                if (!File.Exists(rr)) { Falla("no existe " + rr); continue; }
                if (!Directory.Exists(PG3_ESTELA)) { Falla("no existe " + PG3_ESTELA); continue; }
                var doc = LeerJson(rr); var tres = doc.RootElement.GetProperty("tres");
                var ses = SesionFamilia.DelDia(dia, false);
                var sw = Stopwatch.StartNew();
                var at = TresDe(dia, PG3_ESTELA, ses);
                double ms = sw.Elapsed.TotalMilliseconds;
                foreach (var capa in CatalogoFamilia.CapasTres)
                {
                    int ok = 0, mal = 0, soloP = 0, soloC = 0; var ej = new List<string>();
                    tres.TryGetProperty(capa, out var rc);
                    for (long k = ses.IniClave; k < ses.FinClave; k++)
                    {
                        var mio = at.Niveles(capa, k, out var linea);
                        JsonElement x = default; bool hayP = rc.ValueKind == JsonValueKind.Object && rc.TryGetProperty(k.ToString(Inv), out x);
                        if (!hayP && mio == null) continue;
                        if (hayP != (mio != null)) { if (hayP) soloP++; else soloC++; if (ej.Count < 5) ej.Add(k + " presencia python=" + hayP); continue; }
                        long hastaP = x[0].GetInt64();
                        long hastaC = (linea.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks) * 100L + AlmacenTres.VidaS[capa] * 1_000_000_000L;
                        var lv = x[2]; bool igual = hastaP == hastaC && lv.GetArrayLength() == mio.Length;
                        for (int i = 0; igual && i < mio.Length; i++) igual = lv[i][0].GetDouble() == mio[i].Precio && lv[i][1].GetString() == mio[i].Etq;
                        if (igual) ok++; else { mal++; if (ej.Count < 5) ej.Add(k + " python " + x.ToString() + " c# " + string.Join(" ", mio.Select(m => m.Etq + " " + F(m.Precio))) + " hasta " + hastaC); }
                    }
                    P("  " + dia + " " + capa.PadRight(4) + " minutos iguales " + ok + ", distintos " + mal + ", solo python " + soloP + ", solo c# " + soloC);
                    foreach (var e in ej) P("    " + e);
                    if (mal + soloP + soloC > 0) Falla("tres " + dia + " " + capa);
                }
                P("  " + dia + ": " + at.LineasLeidas + " lineas leidas, " + at.LineasRotas + " rotas, " + ms.ToString("0", Inv) + " ms");
            }
        }

        // ================================================================== historia y actuales
        static Dictionary<long, RegistroMinuto> NivDeRef(JsonElement niv)
        {
            var d = new Dictionary<long, RegistroMinuto>();
            var calc = CatalogoFamilia.Calc;
            foreach (var p in niv.EnumerateObject())
            {
                var r = new RegistroMinuto { Clave = long.Parse(p.Name, Inv), Libros = new[] { "ref" }, Series = new Nivel[calc.Length][], Strikes = new double[calc.Length][] };
                foreach (var s in p.Value.EnumerateObject())
                {
                    int i = CatalogoFamilia.IndiceCalc(s.Name); if (i < 0) continue;
                    r.Series[i] = s.Value.EnumerateArray().Select(x => new Nivel(x[0].GetDouble(), x[1].GetString(), Num(x[2]))).ToArray();
                }
                d[r.Clave] = r;
            }
            return d;
        }

        static void Historia()
        {
            P(); P("== historia: SalidaFamilia (historia por vela m2 y actuales) contra ref 'salida' ==");
            foreach (var dia in DIAS)
            {
                var rr = Path.Combine(REF, "ref-" + dia + ".json");
                if (!File.Exists(rr)) { Falla("no existe " + rr); continue; }
                var doc = LeerJson(rr); var root = doc.RootElement; var sal = root.GetProperty("salida");
                var ses = SesionFamilia.DelDia(dia, false);
                var niv = NivDeRef(root.GetProperty("niv"));
                var at = TresDe(dia, PG3_ESTELA, ses);
                // historia de la referencia: (serie, t) -> [[p, e]]
                var refH = new Dictionary<(string, long), JsonElement>();
                foreach (var s in sal.GetProperty("historia").EnumerateObject())
                    foreach (var x in s.Value.EnumerateArray()) refH[(s.Name, x[0].GetInt64())] = x[1];
                var velas = sal.GetProperty("velas").EnumerateArray().Select(v => v[0].GetInt64()).ToList();
                var series = CatalogoFamilia.Series.Take(24).Select(x => x.Id).ToArray();
                int ok = 0, mal = 0; var ej = new List<string>();
                var porSerie = new Dictionary<string, int>();
                foreach (var t in velas)
                {
                    var e = SalidaFamilia.Entrada(t, k => niv.TryGetValue(k, out var r) ? r : null, at, null);
                    foreach (var sid in series)
                    {
                        bool hayP = refH.TryGetValue((sid, t), out var lp); double[] mio = null; bool hayC = e != null && e.TryGetValue(sid, out mio);
                        if (!hayP && !hayC) continue;
                        bool igual = hayP == hayC && lp.GetArrayLength() == mio.Length;
                        for (int i = 0; igual && i < mio.Length; i++) igual = lp[i][0].GetDouble() == mio[i];
                        if (igual) { ok++; porSerie[sid] = porSerie.TryGetValue(sid, out var q) ? q + 1 : 1; }
                        else { mal++; if (ej.Count < 8) ej.Add(sid + " vela " + t + ": python " + (hayP ? lp.ToString() : "-") + " c# " + (hayC ? string.Join(",", mio.Select(F)) : "-")); }
                    }
                }
                P("  " + dia + ": " + velas.Count + " velas; pares (vela, serie) iguales " + ok + ", distintos " + mal + "; TRES_NQ " + (porSerie.TryGetValue("TRES_NQ", out var a1) ? a1 : 0) +
                  ", TRES_NDX " + (porSerie.TryGetValue("TRES_NDX", out var a2) ? a2 : 0) + ", CONF_vol " + (porSerie.TryGetValue("CONF_vol", out var a3) ? a3 : 0) + ", FAM_MUROS_oi " + (porSerie.TryGetValue("FAM_MUROS_oi", out var a4) ? a4 : 0));
                foreach (var x in ej) P("    " + x);
                if (mal > 0) Falla("historia " + dia);

                // actuales del ultimo minuto (con la meta de la referencia: salida.fuentes)
                var kAct = SesionFamilia.Ms(DateTime.Parse(sal.GetProperty("minuto_utc").GetString(), Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)) / 60000L;
                if (!niv.TryGetValue(kAct, out var ru)) { Falla("actuales: la referencia no tiene niv[" + kAct + "]"); continue; }
                var meta = new List<MetaLibro>();
                foreach (var f in sal.GetProperty("fuentes").EnumerateObject())
                {
                    var fe = f.Value;
                    meta.Add(new MetaLibro
                    {
                        Libro = f.Name, Conv = Num(fe.GetProperty("conv_valor")), ConvTexto = fe.GetProperty("conv").GetString(),
                        DatoUtc = DateTime.SpecifyKind(DateTime.Parse(fe.GetProperty("dato_utc").GetString(), Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), DateTimeKind.Utc),
                        OiOk = fe.GetProperty("oi_ok").GetBoolean(), Congelada = fe.TryGetProperty("congelada", out var cz) && cz.ValueKind == JsonValueKind.True
                    });
                }
                ru.Meta = meta.ToArray(); ru.Libros = meta.Select(m => m.Libro).ToArray();
                var calc = CatalogoFamilia.Calc;
                for (int s = 0; s < calc.Length; s++)
                {
                    var lv = ru.Series[s]; if (lv == null) continue;
                    var si = CatalogoFamilia.PorId[calc[s]]; var m = ru.MetaDe(si.Libro);
                    ru.Strikes[s] = lv.Select(x => si.Libro == "familia" || m == null ? double.NaN : si.Libro == "QQQ" ? NumPy.RoundPy(x.Precio / m.Conv, 2) : NumPy.RoundPy(x.Precio - m.Conv, 2)).ToArray();
                }
                var act = SalidaFamilia.Actuales(ru, at, null);
                var refA = sal.GetProperty("actuales").EnumerateArray().ToList();
                int aok = 0, amal = 0; var aej = new List<string>();
                if (refA.Count != act.Count) { amal++; aej.Add("cantidad python " + refA.Count + " c# " + act.Count); }
                for (int i = 0; i < Math.Min(refA.Count, act.Count); i++)
                {
                    var p = refA[i]; var c = act[i];
                    string dp = p.GetProperty("dato_utc").GetString(), dc = c.DatoUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv);
                    bool igual = p.GetProperty("serie").GetString() == c.Serie && p.GetProperty("rol").GetString() == c.Rol && p.GetProperty("precio").GetDouble() == c.Precio
                                 && Igual(Num(p.GetProperty("strike")), c.Strike) && Igual(Num(p.GetProperty("gex_musd")), c.GexM) && dp == dc
                                 && p.GetProperty("oi_viejo").GetBoolean() == c.OiViejo && p.GetProperty("banda").GetDouble() == c.Banda
                                 && p.GetProperty("libro").GetString() == c.Libro && p.GetProperty("fuente").GetString() == c.Fuente && p.GetProperty("tipo").GetString() == c.Tipo;
                    if (igual) aok++; else { amal++; if (aej.Count < 6) aej.Add("python " + p.ToString() + "\n      c# " + c.Serie + " " + c.Rol + " " + F(c.Precio) + " k " + F(c.Strike) + " g " + F(c.GexM) + " " + dc + " oiv " + c.OiViejo + " banda " + c.Banda); }
                }
                P("  " + dia + ": actuales del minuto " + sal.GetProperty("minuto_utc").GetString() + ": iguales " + aok + ", distintos " + amal);
                foreach (var x in aej) P("    " + x);
                if (amal > 0) Falla("actuales " + dia);
            }
        }

        // ================================================================== motor de punta a punta
        static void Motor()
        {
            P(); P("== motor: MotorFamilia de punta a punta con dobles (casos sinteticos como minutos, estelas reales copiadas) ==");
            var casos = Casos(); if (casos.Count == 0) return;
            string dia = "2026-10-08";
            var ses = SesionFamilia.DelDia(dia, false);
            long ini = ses.IniClave;
            var tmp = Path.Combine(RES, "motor_tmp");
            try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { }
            Directory.CreateDirectory(Path.Combine(tmp, "estela"));
            int copiadas = 0;
            foreach (var capa in CatalogoFamilia.CapasTres) foreach (var d in new[] { "2026-10-07", "2026-10-08" })
            {
                var o = Path.Combine(PG3_ESTELA, "estela-" + capa + "-" + d + ".jsonl");
                if (File.Exists(o)) { File.Copy(o, Path.Combine(tmp, "estela", Path.GetFileName(o)), true); copiadas++; }
            }
            P("  estelas copiadas a la carpeta temporal: " + copiadas);
            var op = new OpcionesMotorFamilia { Carpeta = tmp, Corregida = false, Persistir = true, PresupuestoMs = 600_000, EsperaArranqueS = 0, RutaLog = Path.Combine(tmp, "pythiagex4-familia.log") };

            // ---- fase 1: arranque a las 03:10 UTC (310 minutos de atraso) + vivo minuto a minuto hasta las 15:00
            var cinta = new CintaFalsa(casos, ini); var fuente = new FuenteFalsa();
            var tq = new TqqqFalso { RuedaIni = ses.RuedaIniUtc };
            var mins = CatalogoFamilia.Libros.Select(lb => new MinuteroFalso(lb, casos, ini)).ToList();
            var m1 = new MotorFamilia(op);
            m1.Configurar(cinta, fuente, fuente, mins, tq, "MNQZ6");
            var ahora = ses.IniUtc.AddMinutes(310).AddSeconds(10);
            cinta.Ahora = ahora; tq.Ahora = ahora;
            var sw = Stopwatch.StartNew();
            m1.Avanzar(ahora);
            double msArranque = sw.Elapsed.TotalMilliseconds;
            int vueltas = 1; while (m1.Pendiente && vueltas < 50) { m1.Avanzar(ahora); vueltas++; }
            var t0 = mins[0].Tandas.ToList();
            P("  arranque 03:10: " + m1.MinutosCalculados + " minutos en " + vueltas + " vuelta(s), " + msArranque.ToString("0", Inv) + " ms (" + (msArranque / Math.Max(1, m1.MinutosCalculados)).ToString("0.00", Inv) +
              " ms/minuto con dobles); tandas " + string.Join(" ", t0.Select(x => "[" + (x.Primera - ini) + ".." + (x.Ultima - ini) + "]")));
            // a las 03:10:10 el ultimo minuto calculable es el 310 (03:10, a los 6 s): historico() hace range(ini, k1 + 1) de a 240
            if (t0.Count != 2 || t0[0].Primera != ini || t0[0].Ultima != ini + 239 || t0[1].Primera != ini + 240 || t0[1].Ultima != ini + 310)
                Falla("tandas del arranque: se esperaban [0..239] [240..310] (alineadas al inicio de la sesion, como preview_niveles.historico)");
            int nT = mins[0].Tandas.Count;
            var msVivo = new List<double>();
            while (ahora < ses.IniUtc.AddHours(17))   // hasta las 15:00 UTC
            {
                ahora = ahora.AddMinutes(1); cinta.Ahora = ahora; tq.Ahora = ahora;
                m1.Avanzar(ahora); msVivo.Add(m1.MsUltimoAvanzar);
            }
            int tandasVivo = mins[0].Tandas.Count - nT;
            bool todasDeUno = mins[0].Tandas.Skip(nT).All(x => x.Primera == x.Ultima);
            msVivo.Sort();
            P("  vivo 03:11 -> 15:00: " + tandasVivo + " tandas de un minuto (" + (todasDeUno ? "todas" : "NO todas") + "); Avanzar mediana " + msVivo[msVivo.Count / 2].ToString("0.0", Inv) +
              " ms, p99 " + msVivo[(int)(msVivo.Count * 0.99)].ToString("0.0", Inv) + " ms (incluye historia, TQQQ falso y foto)");
            if (!todasDeUno) Falla("en vivo cada minuto nuevo tiene que ser su propia tanda (preview_niveles.ciclo)");
            if (mins.Any(x => x.FueraDeOrden)) Falla("algun minutero recibio claves fuera de orden");
            var claves1 = m1.Almacen.Todos.Select(r => r.Clave).OrderBy(k => k).ToList();
            var lineas1 = m1.Almacen.Todos.ToDictionary(r => r.Clave, r => AlmacenNiveles.Linea(r));

            // ---- fase 2: reinicio a las 15:30 (los 30 minutos que faltan se calculan; lo persistido NO se vuelve a pedir)
            var mins2 = CatalogoFamilia.Libros.Select(lb => new MinuteroFalso(lb, casos, ini)).ToList();
            var m2 = new MotorFamilia(op);
            m2.Configurar(cinta, fuente, fuente, mins2, tq, "MNQZ6");
            ahora = ses.IniUtc.AddHours(17).AddMinutes(30).AddSeconds(10); cinta.Ahora = ahora; tq.Ahora = ahora;
            m2.Avanzar(ahora);
            int cargados = m2.Almacen.Cargados;
            var repedidas = mins2[0].Pedidas.Where(k => lineas1.ContainsKey(k)).Count();
            P("  reinicio 15:30: " + cargados + " minutos cargados del archivo, " + mins2[0].Llamadas + " pedidos nuevos al minutero NQ (repetidos de lo persistido: " + repedidas + ")");
            if (cargados != lineas1.Count) Falla("se persistieron " + lineas1.Count + " minutos y se cargaron " + cargados);
            if (repedidas > 0) Falla("se recalcularon minutos ya persistidos");
            int distintosCarga = 0;
            foreach (var kv in lineas1) { var r2 = m2.Almacen.Get(kv.Key); if (r2 == null || AlmacenNiveles.Linea(r2) != kv.Value) distintosCarga++; }
            if (distintosCarga > 0) Falla(distintosCarga + " minutos distintos despues de cargar del archivo");
            while (ahora < ses.FinUtc.AddMinutes(30))
            {
                ahora = ahora.AddMinutes(1); cinta.Ahora = ahora; tq.Ahora = ahora;
                m2.Avanzar(ahora);
            }

            // ---- todos los minutos de la sesion contra Python (cada minuto sirvio el caso (k - ini) % n)
            var tabla = new Dictionary<string, (int, int)>(); int malos = 0, faltan = 0; var ej = new List<string>();
            for (long k = ini; k < ses.FinClave; k++)
            {
                var r = m2.Almacen.Get(k); var c = casos[(int)((k - ini) % casos.Count)];
                if (r == null) { faltan++; continue; }
                var dif = CompararReg(r, c.Reg, null, tabla);
                if (dif.Count > 0) { malos++; if (ej.Count < 6) ej.Add("minuto " + (k - ini) + ": " + dif[0]); }
            }
            P("  sesion completa: " + (ses.FinClave - ini) + " minutos, faltan " + faltan + ", distintos de Python " + malos);
            foreach (var x in ej) P("    " + x);
            if (faltan > 0 || malos > 0) Falla("motor: minutos faltantes o distintos");

            // ---- la foto: historia (contra niv y contra la TRES de la referencia), actuales, fuentes, TQQQ
            var foto = m2.Foto;
            var h = foto.HistoriaM2;
            int hOk = 0, hMal = 0, tresOk = 0, tresMal = 0, tqVelas = 0;
            var refDoc = File.Exists(Path.Combine(REF, "ref-" + dia + ".json")) ? LeerJson(Path.Combine(REF, "ref-" + dia + ".json")) : null;
            var refH = new Dictionary<(string, long), JsonElement>();
            if (refDoc != null) foreach (var s in refDoc.RootElement.GetProperty("salida").GetProperty("historia").EnumerateObject())
                    if (s.Name.StartsWith("TRES_", StringComparison.Ordinal)) foreach (var x in s.Value.EnumerateArray()) refH[(s.Name, x[0].GetInt64())] = x[1];
            var velasRef = refDoc?.RootElement.GetProperty("salida").GetProperty("velas").EnumerateArray().Select(v => v[0].GetInt64()).ToList() ?? new List<long>();
            for (long T = SesionFamilia.Ms(ses.IniUtc); T < SesionFamilia.Ms(ses.FinUtc); T += 120000)
            {
                var r = m2.Almacen.Get(T / 60000); if (r == null || r.Vacio) r = m2.Almacen.Get(T / 60000 - 1);
                h.TryGetValue(T, out var e);
                foreach (var sid in CatalogoFamilia.Calc)
                {
                    int i = CatalogoFamilia.IndiceCalc(sid); var lv = r?.Series[i];
                    double[] mio = null; bool hayC = e != null && e.TryGetValue(sid, out mio);
                    bool igual = (lv == null) == !hayC && (lv == null || lv.Select(x => x.Precio).SequenceEqual(mio));
                    if (igual) hOk++; else hMal++;
                }
                if (e != null && e.ContainsKey("T_DOMS_vol")) tqVelas++;
            }
            foreach (var T in velasRef)
                foreach (var capa in CatalogoFamilia.CapasTres)
                {
                    string sid = "TRES_" + capa;
                    bool hayP = refH.TryGetValue((sid, T), out var lp); double[] mio = null; bool hayC = h.TryGetValue(T, out var e) && e.TryGetValue(sid, out mio);
                    bool igual = hayP == hayC && (!hayP || (lp.GetArrayLength() == mio.Length && Enumerable.Range(0, mio.Length).All(i => lp[i][0].GetDouble() == mio[i])));
                    if (igual) { if (hayP) tresOk++; } else tresMal++;
                }
            P("  historia: " + h.Count + " velas m2; (vela, serie) contra niv iguales " + hOk + ", distintos " + hMal + "; TRES contra la referencia iguales " + tresOk + ", distintos " + tresMal + "; velas con TQQQ " + tqVelas);
            if (hMal > 0 || tresMal > 0) Falla("historia del motor");
            var ult = m2.Almacen.Ultimo;
            P("  foto: sesion " + foto.Sesion + ", instrumento " + foto.Instrumento + ", actuales " + foto.Actuales.Count + " (" + string.Join(" ", foto.Actuales.GroupBy(a => a.Serie).Select(g => g.Key + "x" + g.Count()).Take(40)) + ")");
            P("  fuentes: " + string.Join(" | ", foto.Fuentes.Select(f => f.Libro + ": " + f.Texto + (f.DatoUtc != default ? " (dato " + f.DatoUtc.ToString("HH:mm:ss", Inv) + ")" : ""))));
            P("  TQQQ s " + F(foto.TqS) + " c " + F(foto.TqC) + "; aviso: '" + foto.Aviso + "'");
            if (ult == null || ult.Clave != ses.FinClave - 1) Falla("el ultimo minuto con libros tiene que ser 20:59 UTC");
            int esperadas = 0; var cu = casos[(int)((ses.FinClave - 1 - ini) % casos.Count)];
            foreach (var p in cu.Reg.EnumerateObject()) esperadas += p.Value.GetArrayLength();
            int tresAct = foto.Actuales.Count(a => a.Tipo == "TRES"), tqAct = foto.Actuales.Count(a => a.Libro == "TQQQ");
            if (foto.Actuales.Count != esperadas + tresAct + tqAct || tqAct != 4) Falla("actuales: " + foto.Actuales.Count + " (esperadas " + esperadas + " de la cuenta + " + tresAct + " TRES + 4 TQQQ)");
            if (double.IsNaN(foto.TqS) || foto.Fuentes.All(f => f.Libro != "CBOE")) Falla("foto sin TQQQ o sin estado de CBOE");

            // ---- corregida (NY) en verano: la misma ventana
            var sN = SesionFamilia.DelDia(dia, true);
            if (sN.IniUtc != ses.IniUtc || sN.FinUtc != ses.FinUtc || sN.RuedaIniUtc != ses.RuedaIniUtc) Falla("en verano la sesion corregida tiene que coincidir con la de paridad");
            try { Directory.Delete(tmp, true); } catch { }
        }

        // ================================================================== invierno
        static void Invierno()
        {
            P(); P("== invierno: sesion en hora NY (corregida) contra la de paridad (UTC fijo) ==");
            void Ver(string dia, bool corr, string ini, string fin, string rueda)
            {
                var s = SesionFamilia.DelDia(dia, corr);
                string a = s.IniUtc.ToString("yyyy-MM-dd HH:mm", Inv), b = s.FinUtc.ToString("yyyy-MM-dd HH:mm", Inv), c = s.RuedaIniUtc.ToString("yyyy-MM-dd HH:mm", Inv);
                bool ok = a == ini && b == fin && c == rueda;
                P("  " + (ok ? "ok    " : "FALLA ") + s + " rueda " + c);
                if (!ok) Falla("sesion " + dia + (corr ? " NY" : " UTC") + ": esperado [" + ini + ", " + fin + ") rueda " + rueda);
            }
            Ver("2026-10-30", true, "2026-10-29 22:00", "2026-10-30 21:00", "2026-10-30 13:30");
            Ver("2026-11-02", true, "2026-11-01 23:00", "2026-11-02 22:00", "2026-11-02 14:30");
            Ver("2026-11-02", false, "2026-11-01 22:00", "2026-11-02 21:00", "2026-11-02 13:30");
            Ver("2027-03-15", true, "2027-03-14 22:00", "2027-03-15 21:00", "2027-03-15 13:30");
            // el dia que corresponde a un instante: 17:30 NY sigue en la sesion que cerro; 18:00 NY ya es la siguiente
            var a1 = SesionFamilia.De(SesionFamilia.NyAUtc(new DateTime(2026, 11, 2, 17, 30, 0)), true);
            var a2 = SesionFamilia.De(SesionFamilia.NyAUtc(new DateTime(2026, 11, 2, 18, 0, 0)), true);
            P("  17:30 NY del 02-11 -> sesion " + a1.Dia + "; 18:00 NY -> " + a2.Dia);
            if (a1.Dia != "2026-11-02" || a2.Dia != "2026-11-03") Falla("cambio de sesion a las 18:00 NY");
        }

        // ================================================================== fin de semana (4.1.5d) y el aviso del motor
        /// <summary>4.1.5d (pedido del operador 09-10: "quiero que la 4.0 se vuelva a ver, o sea que tenga memoria"). CME cierra del viernes 17:00 NY
        /// al domingo 18:00 NY: en modo NY la sesion de ese rato es la del VIERNES (antes: la del sabado o el domingo, que nunca tienen datos: tras
        /// reiniciar ATAS la 4.1 no dibujaba nada). El modo UTC (paridad con la vista previa) NO cambia. Y el motor: con ahora >= FinUtc el aviso de
        /// la foto EMPIEZA con "mercado cerrado: se muestra la sesion del ..." (el dato es viejo: se dice antes que nada, protocolo).</summary>
        static void Finde()
        {
            P(); P("== finde (4.1.5d): la sesion del fin de semana en hora NY, el modo UTC sin cambios y el aviso 'mercado cerrado' del motor ==");
            string[] DIA = { "dom", "lun", "mar", "mie", "jue", "vie", "sab" };
            string Z(DateTime u) => u.ToString("yyyy-MM-dd HH:mm", Inv) + "Z";
            string N(DateTime ny) => DIA[(int)ny.DayOfWeek] + " " + ny.ToString("dd-MM HH:mm", Inv);
            int okNy = 0, okUtc = 0, okMotor = 0;

            // (a) modo NY (Familia41Corregida = true, el del indicador)
            void Ny(string que, DateTime ny, string dia, bool abierta, string ini = null, string fin = null, string rueda = null)
            {
                var t = SesionFamilia.NyAUtc(ny);
                var s = SesionFamilia.De(t, true);
                bool ab = t >= s.IniUtc && t < s.FinUtc;
                string antes = SesionFamilia.UtcANy(t).AddHours(6).Date.ToString("yyyy-MM-dd", Inv);   // la cuenta de antes de la 4.1.5d (sin el fin de semana)
                bool bien = s.Dia == dia && ab == abierta && (ini == null || Z(s.IniUtc) == ini) && (fin == null || Z(s.FinUtc) == fin) && (rueda == null || Z(s.RuedaIniUtc) == rueda);
                P("  " + (bien ? "ok    " : "FALLA ") + ("NY " + que).PadRight(48) + N(ny) + " NY = " + Z(t) + " -> " + s + (ab ? " ABIERTA" : " cerrada") + " rueda " + Z(s.RuedaIniUtc)
                  + (antes != s.Dia ? " (antes de la 4.1.5d: sesion " + antes + ", sin datos)" : ""));
                if (bien) okNy++; else Falla("finde NY " + que + ": esperado " + dia + (abierta ? " abierta" : " cerrada") + (ini != null ? " [" + ini + ", " + fin + ") rueda " + rueda : ""));
            }
            Ny("viernes 16:59 (abierta)", new DateTime(2026, 10, 9, 16, 59, 0), "2026-10-09", true, "2026-10-08 22:00Z", "2026-10-09 21:00Z", "2026-10-09 13:30Z");
            Ny("viernes 17:00 (cierra)", new DateTime(2026, 10, 9, 17, 0, 0), "2026-10-09", false, "2026-10-08 22:00Z", "2026-10-09 21:00Z");
            Ny("viernes 18:05", new DateTime(2026, 10, 9, 18, 5, 0), "2026-10-09", false, "2026-10-08 22:00Z", "2026-10-09 21:00Z");
            Ny("sabado 12:00", new DateTime(2026, 10, 10, 12, 0, 0), "2026-10-09", false, "2026-10-08 22:00Z", "2026-10-09 21:00Z");
            Ny("domingo 17:59", new DateTime(2026, 10, 11, 17, 59, 0), "2026-10-09", false, "2026-10-08 22:00Z", "2026-10-09 21:00Z");
            Ny("domingo 18:00 (abre la del lunes)", new DateTime(2026, 10, 11, 18, 0, 0), "2026-10-12", true, "2026-10-11 22:00Z", "2026-10-12 21:00Z", "2026-10-12 13:30Z");
            // el cambio de horario: el domingo 01-11 a las 2:00 NY vuelve el de invierno (EST, UTC-5): el viernes 30-10 cierra 21:00Z, el lunes abre 23:00Z
            Ny("sabado 31-10 12:00 (EDT)", new DateTime(2026, 10, 31, 12, 0, 0), "2026-10-30", false, "2026-10-29 22:00Z", "2026-10-30 21:00Z", "2026-10-30 13:30Z");
            Ny("domingo 01-11 17:59 (EST)", new DateTime(2026, 11, 1, 17, 59, 0), "2026-10-30", false, "2026-10-29 22:00Z", "2026-10-30 21:00Z");
            Ny("domingo 01-11 18:00 (EST, abre la del lunes)", new DateTime(2026, 11, 1, 18, 0, 0), "2026-11-02", true, "2026-11-01 23:00Z", "2026-11-02 22:00Z", "2026-11-02 14:30Z");
            // control: entre semana nada cambia (la pausa de 17:00 a 18:00 NY sigue en la sesion que cerro; 18:00 NY ya es la siguiente)
            Ny("jueves 17:30 (pausa de cada dia)", new DateTime(2026, 10, 8, 17, 30, 0), "2026-10-08", false, "2026-10-07 22:00Z", "2026-10-08 21:00Z");
            Ny("jueves 18:00", new DateTime(2026, 10, 8, 18, 0, 0), "2026-10-09", true, "2026-10-08 22:00Z", "2026-10-09 21:00Z");

            // (b) modo UTC (paridad con la vista previa): SIN cambios, dia = fecha(t + 2 h) en UTC fijo (el sabado y el domingo tienen su sesion)
            void Utc(string que, DateTime ny, string dia)
            {
                var t = SesionFamilia.NyAUtc(ny);
                var s = SesionFamilia.De(t, false);
                var d = t.AddHours(2).Date;
                bool bien = s.Dia == dia && s.Dia == d.ToString("yyyy-MM-dd", Inv) && s.IniUtc == DateTime.SpecifyKind(d.AddHours(-2), DateTimeKind.Utc)
                            && s.FinUtc == DateTime.SpecifyKind(d.AddHours(21), DateTimeKind.Utc) && s.SiguienteIniUtc == DateTime.SpecifyKind(d.AddHours(22), DateTimeKind.Utc)
                            && s.RuedaIniUtc == DateTime.SpecifyKind(d.AddHours(13).AddMinutes(30), DateTimeKind.Utc);
                P("  " + (bien ? "ok    " : "FALLA ") + ("UTC " + que).PadRight(48) + N(ny) + " NY = " + Z(t) + " -> " + s);
                if (bien) okUtc++; else Falla("finde UTC " + que + ": esperado " + dia + " = fecha(t + 2 h)");
            }
            Utc("viernes 16:59", new DateTime(2026, 10, 9, 16, 59, 0), "2026-10-09");
            Utc("viernes 17:00", new DateTime(2026, 10, 9, 17, 0, 0), "2026-10-09");
            Utc("viernes 18:05", new DateTime(2026, 10, 9, 18, 5, 0), "2026-10-10");
            Utc("sabado 12:00", new DateTime(2026, 10, 10, 12, 0, 0), "2026-10-10");
            Utc("domingo 17:59", new DateTime(2026, 10, 11, 17, 59, 0), "2026-10-11");
            Utc("domingo 18:00", new DateTime(2026, 10, 11, 18, 0, 0), "2026-10-12");
            Utc("sabado 31-10 12:00 (EDT)", new DateTime(2026, 10, 31, 12, 0, 0), "2026-10-31");
            Utc("domingo 01-11 17:59 (EST: corre una hora)", new DateTime(2026, 11, 1, 17, 59, 0), "2026-11-02");
            Utc("domingo 01-11 18:00 (EST)", new DateTime(2026, 11, 1, 18, 0, 0), "2026-11-02");

            // (c) el motor en modo NY con los dobles (casos sinteticos como minutos) sobre la sesion del viernes 09-10
            var casos = Casos(); if (casos.Count == 0) return;
            var ses = SesionFamilia.DelDia("2026-10-09", true);
            long ini = ses.IniClave, total = ses.FinClave - ses.IniClave;
            var tmp = Path.Combine(RES, "finde_tmp"); var tmp2 = Path.Combine(RES, "finde_tmp2");
            foreach (var d in new[] { tmp, tmp2 }) { try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch { } Directory.CreateDirectory(Path.Combine(d, "estela")); }
            var cinta = new CintaFalsa(casos, ini); var fuente = new FuenteFalsa(); var tq = new TqqqFalso { RuedaIni = ses.RuedaIniUtc };
            OpcionesMotorFamilia Op(string carpeta, int presupuesto) => new OpcionesMotorFamilia { Carpeta = carpeta, Corregida = true, Persistir = true, PresupuestoMs = presupuesto, EsperaArranqueS = 0, RutaLog = Path.Combine(carpeta, "pythiagex4-familia.log") };
            MotorFamilia Nuevo(OpcionesMotorFamilia op)
            {
                var m = new MotorFamilia(op);
                m.Configurar(cinta, fuente, fuente, CatalogoFamilia.Libros.Select(lb => (ILibroMinutero)new MinuteroFalso(lb, casos, ini)).ToList(), tq, "MNQZ6");
                return m;
            }
            DateTime Poner(DateTime ny) { var t = SesionFamilia.NyAUtc(ny); cinta.Ahora = t; tq.Ahora = t; return t; }
            FotoFamilia Paso(MotorFamilia m, DateTime ny) { var t = Poner(ny); m.Avanzar(t); for (int v = 0; m.Pendiente && v < 200; v++) m.Avanzar(t); return m.Foto; }
            const string AVISO = "mercado cerrado: se muestra la sesion del ";
            void Ver(bool c, string que, string det)
            {
                P("  " + (c ? "ok    " : "FALLA ") + que + (det == "" ? "" : " | " + det));
                if (c) okMotor++; else Falla("finde motor: " + que);
            }
            string Av(FotoFamilia f) => "aviso '" + (f?.Aviso ?? "") + "'";

            // 1) viernes 16:59:30 NY, mercado abierto: se calcula la sesion entera y va al archivo; sin el aviso
            var m1 = Nuevo(Op(tmp, 600_000));
            var f1 = Paso(m1, new DateTime(2026, 10, 9, 16, 59, 30));
            int calc1 = m1.Almacen.Todos.Count();
            Ver(f1.Sesion == "2026-10-09" && calc1 == total && !(f1.Aviso ?? "").Contains("mercado cerrado"),
                "M1 viernes 16:59:30 NY (abierta): sesion " + f1.Sesion + ", " + calc1 + " de " + total + " minutos, sin 'mercado cerrado'", Av(f1));
            // 2) viernes 17:30 NY (cerro hace 30 min): la misma sesion y el aviso PRIMERO
            var f2 = Paso(m1, new DateTime(2026, 10, 9, 17, 30, 0));
            Ver(f2.Sesion == "2026-10-09" && (f2.Aviso ?? "").StartsWith(AVISO + "2026-10-09 (cerro hace 30 min)", StringComparison.Ordinal),
                "M2 viernes 17:30 NY: el aviso empieza con '" + AVISO + "2026-10-09 (cerro hace 30 min)'", Av(f2));
            // 3) sabado 12:00 NY con un motor NUEVO (ATAS reiniciado): reabre la sesion del viernes DESDE EL ARCHIVO (memoria), no recalcula nada
            var m2 = Nuevo(Op(tmp, 600_000));
            var f3 = Paso(m2, new DateTime(2026, 10, 10, 12, 0, 0));
            int igualH = 0, distH = 0;
            foreach (var kv in f1.HistoriaM2)
            {
                if (!f3.HistoriaM2.TryGetValue(kv.Key, out var e3)) { distH++; continue; }
                foreach (var s1 in kv.Value.Where(x => !x.Key.StartsWith("T_", StringComparison.Ordinal)))   // TQQQ falso depende de la hora de la llamada
                    if (e3.TryGetValue(s1.Key, out var v3) && v3.SequenceEqual(s1.Value)) igualH++; else distH++;
            }
            Ver(f3.Sesion == "2026-10-09" && m2.Almacen.Cargados == total && m2.MinutosCalculados == 0 && f3.HistoriaM2.Count > 0 && f3.Actuales.Count > 0 && distH == 0 && igualH > 0
                && (f3.Aviso ?? "").StartsWith(AVISO + "2026-10-09 (cerro hace ", StringComparison.Ordinal),
                "M3 sabado 12:00 NY, motor nuevo: sesion " + f3.Sesion + " con " + m2.Almacen.Cargados + " minutos del archivo, " + m2.MinutosCalculados + " recalculados, historia "
                + f3.HistoriaM2.Count + " velas m2 (series de la cuenta iguales a las del viernes: " + igualH + ", distintas " + distH + "), actuales " + f3.Actuales.Count + ", aviso primero", Av(f3));
            // 3b) sabado con el motor a medio calcular (carpeta vacia, presupuesto 1 ms): el aviso sigue PRIMERO, antes de 'calculando la sesion'
            var m3 = Nuevo(Op(tmp2, 1));
            var t3b = Poner(new DateTime(2026, 10, 10, 12, 0, 0)); m3.Avanzar(t3b);
            var f3b = m3.Foto;
            Ver(m3.Pendiente && (f3b.Aviso ?? "").StartsWith(AVISO + "2026-10-09", StringComparison.Ordinal) && (f3b.Aviso ?? "").Contains(" · calculando la sesion"),
                "M3b sabado, motor a medio calcular (" + m3.Almacen.Todos.Count() + " de " + total + " minutos): 'mercado cerrado' va antes que 'calculando la sesion'", Av(f3b));
            // 4) domingo 17:59 NY: sigue la del viernes con el aviso
            var f4 = Paso(m2, new DateTime(2026, 10, 11, 17, 59, 0));
            Ver(f4.Sesion == "2026-10-09" && (f4.Aviso ?? "").StartsWith(AVISO + "2026-10-09 (cerro hace ", StringComparison.Ordinal) && m2.MinutosCalculados == 0,
                "M4 domingo 17:59 NY: sigue la sesion del viernes, aviso primero", Av(f4));
            // 5) domingo 18:00:10 NY: empieza la del lunes 12-10 (sin 'mercado cerrado')
            var f5 = Paso(m2, new DateTime(2026, 10, 11, 18, 0, 10));
            Ver(f5.Sesion == "2026-10-12" && m2.Sesion?.Dia == "2026-10-12" && !(f5.Aviso ?? "").Contains("mercado cerrado"),
                "M5 domingo 18:00:10 NY: sesion " + f5.Sesion + " (la del lunes), sin 'mercado cerrado'", Av(f5));
            // 6) ningun archivo de una sesion de sabado o domingo (antes de la 4.1.5d el sabado abria la 2026-10-10)
            var niv = Directory.Exists(Path.Combine(tmp, "familia")) ? Directory.GetFiles(Path.Combine(tmp, "familia"), "niv-*.jsonl").Select(Path.GetFileName).OrderBy(x => x).ToList() : new List<string>();
            Ver(niv.Contains("niv-2026-10-09-MNQZ6.jsonl") && !niv.Any(x => x.Contains("2026-10-10") || x.Contains("2026-10-11")),
                "M6 archivos de la carpeta: " + string.Join(", ", niv) + " (ninguno del sabado ni del domingo)", "");
            foreach (var d in new[] { tmp, tmp2 }) { try { Directory.Delete(d, true); } catch { } }
            P("  finde: NY " + okNy + " de 11 bien, UTC " + okUtc + " de 9 bien, motor " + okMotor + " de 7 bien");
        }
    }
}
