// Gm.cs — modo 'gm' del arnes fam (B-tres, PythiaGex 4.1.2, 08-10-2026): el monto con signo de las rayas 3.0 (campo "gm" de la estela).
//   unidad   AlmacenTres.MontoDominante3 / MontosDominantes3 / CampoGm: relleno +-1e6 del tunel por cruces, 0 (vigente sin strike), valor
//            fuera del perfil, raiz sin unidad de familia, alineacion con d[0]/d[1], texto.
//   lectura  AlmacenTres.Parsear con "gm" (alineado con d; null / sin "gm" / raro -> NaN), Agregar y LeerArchivos (ultima gana), Niveles
//            (GexM round 1) y SalidaFamilia.Actuales (TRES con GexM; estela vieja -> NaN).
//   real     datos/gm (gen_datos_gm.py: foto FIJA de lo que escribio la 4.1 en vivo la noche del 08 al 09-10). Cada linea de las capas
//            NDX/QQQ se REHACE con el nucleo de la 3.0 (GammaHoyNucleo.cs compilado tal cual, con los ajustes de las capas) sobre la MISMA
//            cadena de CBOE: se exige que D1/D2 y "g" coincidan con la linea guardada; despues el gm que escribiria GuardarEstela
//            (MontosDominantes3 con esa lectura) se compara con MAJORS_<capa>_vol de la familia del mismo minuto y strike: signo igual y
//            diferencia <= 5 % (+-0,5 M). Y la linea con "gm" pasa por AlmacenTres hasta NivelActual.GexM.
//   nq       el factor NQ x0,2 con datos reales: |g| (x100) de la estela 3.0 de NQ en la rueda del 08-10 contra |MAJORS_NQ_vol| (x20) de la
//            referencia, mismo minuto y mismo strike: la mediana de g x FactorMonto3("NQ") / |MAJORS| tiene que caer en [0,95; 1,05].
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using PythiaGexCuatro.Familia;
using Nucleo3 = PythiaGexCuatro.GammaHoyNucleo;
using Feed3 = PythiaGexCuatro.Feed;

namespace FamTest
{
    public static partial class Program
    {
        static int _gmOk;
        static void Chequear(string que, bool ok, string detalle = "") { if (ok) _gmOk++; else Falla("gm " + que + (string.IsNullOrEmpty(detalle) ? "" : ": " + detalle)); }
        static bool Cerca(double a, double b, double tol = 1e-9) => (double.IsNaN(a) && double.IsNaN(b)) || Math.Abs(a - b) <= tol;

        static void Gm()
        {
            P(); P("== gm (4.1.2): el monto con signo de las rayas 3.0 (campo nuevo de la estela) ==");
            GmUnidad();
            GmLectura();
            GmReal();
            GmNq();
        }

        // ================================================================== unidad
        static void GmUnidad()
        {
            int ok0 = _gmOk, f0 = _fallas;
            // un perfil con un strike de 1e6 EXACTO y uno en 0: el relleno del tunel y el 0 no pasan aunque coincidan con un strike
            var gv = new[] { -145.4e6, 12.3e6, 0.0, 1e6 };
            var go = new[] { -30e6, 5e6, 7e6, -1e6 };
            Chequear("NQ x0,2", Cerca(AlmacenTres.MontoDominante3("NQ", -145.4e6, gv, go), -145.4 * 0.2, 1e-9), F(AlmacenTres.MontoDominante3("NQ", -145.4e6, gv, go)));
            Chequear("NDX x1", Cerca(AlmacenTres.MontoDominante3("NDX", 12.3e6, gv, go), 12.3));
            Chequear("QQQ x1 (strike del libro OI)", Cerca(AlmacenTres.MontoDominante3("QQQ", 5e6, gv, go), 5.0));
            Chequear("NQ por OI x0,2", Cerca(AlmacenTres.MontoDominante3("NQ", -30e6, gv, go), -6.0));
            Chequear("relleno +1e6 del tunel", double.IsNaN(AlmacenTres.MontoDominante3("NQ", 1e6, gv, go)));
            Chequear("relleno -1e6 del tunel", double.IsNaN(AlmacenTres.MontoDominante3("NQ", -1e6, gv, go)));
            Chequear("relleno en NDX tambien", double.IsNaN(AlmacenTres.MontoDominante3("NDX", 1e6, gv, go)));
            Chequear("gex 0 (vigente de la histeresis sin strike)", double.IsNaN(AlmacenTres.MontoDominante3("NQ", 0.0, gv, go)));
            Chequear("fuera del perfil", double.IsNaN(AlmacenTres.MontoDominante3("NQ", 99e6, gv, go)));
            Chequear("casi igual no alcanza (exacto)", double.IsNaN(AlmacenTres.MontoDominante3("NQ", -145.4e6 * (1 + 1e-15), gv, go)));
            Chequear("NaN", double.IsNaN(AlmacenTres.MontoDominante3("NQ", double.NaN, gv, go)));
            Chequear("Infinito", double.IsNaN(AlmacenTres.MontoDominante3("NQ", double.PositiveInfinity, gv, go)));
            Chequear("ES sin unidad de familia", double.IsNaN(AlmacenTres.MontoDominante3("ES", -145.4e6, gv, go)));
            Chequear("RTY sin unidad", double.IsNaN(AlmacenTres.MontoDominante3("RTY", -145.4e6, gv, go)));
            Chequear("raiz vacia / null", double.IsNaN(AlmacenTres.MontoDominante3("", -145.4e6, gv, go)) && double.IsNaN(AlmacenTres.MontoDominante3(null, -145.4e6, gv, go)));
            Chequear("perfil vacio / null", double.IsNaN(AlmacenTres.MontoDominante3("NQ", -145.4e6, new double[0], new double[0])) && double.IsNaN(AlmacenTres.MontoDominante3("NQ", -145.4e6, null, null)));
            Chequear("FactorMonto3", AlmacenTres.FactorMonto3("NQ") == 0.2 && AlmacenTres.FactorMonto3("NDX") == 1.0 && AlmacenTres.FactorMonto3("QQQ") == 1.0 && double.IsNaN(AlmacenTres.FactorMonto3("ES")));

            // alineacion con d[0]/d[1] (GuardarEstela escribe "0" en d si Fut es NaN o <= 0)
            var (a1, a2) = AlmacenTres.MontosDominantes3("NQ", new List<(double, double)> { (31088.48, -145.4e6), (0.0, 12.3e6) }, gv, go);
            Chequear("D2 con Fut 0 -> null", Cerca(a1, -29.08, 1e-9) && double.IsNaN(a2), F(a1) + " " + F(a2));
            var (b1, b2) = AlmacenTres.MontosDominantes3("NQ", new List<(double, double)>(), gv, go);
            Chequear("sin dominantes", double.IsNaN(b1) && double.IsNaN(b2));
            var (c1, c2) = AlmacenTres.MontosDominantes3("NQ", null, gv, go);
            Chequear("doms null", double.IsNaN(c1) && double.IsNaN(c2));
            // la noche del 08-10 (pythiagex4-gammahoy.log, AUDIT3 02:33Z: doms=31088.48=1M/31076.06=-1M): tunel por cruces -> sin monto
            var (n1, n2) = AlmacenTres.MontosDominantes3("NQ", new List<(double, double)> { (31088.48, 1e6), (31076.06, -1e6) }, gv, go);
            Chequear("tunel por cruces de noche -> [null,null]", double.IsNaN(n1) && double.IsNaN(n2));
            var (e1, e2) = AlmacenTres.MontosDominantes3("NDX", new List<(double, double)> { (31038.23, 12.3e6), (31036.22, 5e6), (31150.80, -145.4e6) }, gv, go);
            Chequear("tres dominantes de capa: solo D1/D2", Cerca(e1, 12.3) && Cerca(e2, 5.0));
            var (h1, h2) = AlmacenTres.MontosDominantes3("NQ", new List<(double, double)> { (double.NaN, -145.4e6), (31000, 0.0) }, gv, go);
            Chequear("Fut NaN y Gex 0", double.IsNaN(h1) && double.IsNaN(h2));

            // texto (1 decimal como los montos de la familia; null = sin monto; siempre 2 elementos)
            Chequear("CampoGm(-29.08, NaN)", AlmacenTres.CampoGm(-29.08, double.NaN) == ",\"gm\":[-29.1,null]", AlmacenTres.CampoGm(-29.08, double.NaN));
            Chequear("CampoGm(412.66, -130.14)", AlmacenTres.CampoGm(412.66, -130.14) == ",\"gm\":[412.7,-130.1]", AlmacenTres.CampoGm(412.66, -130.14));
            Chequear("CampoGm(NaN, Inf)", AlmacenTres.CampoGm(double.NaN, double.PositiveInfinity) == ",\"gm\":[null,null]");
            Chequear("CampoGm grande", AlmacenTres.CampoGm(2585.94, -12345.67) == ",\"gm\":[2585.9,-12345.7]", AlmacenTres.CampoGm(2585.94, -12345.67));
            P("  unidad: " + (_gmOk - ok0) + " chequeos ok, " + (_fallas - f0) + " fallas");
        }

        // ================================================================== lectura
        const string LINEA_NDX = "{\"t\":\"2026-10-09T02:32:48Z\",\"d\":[31038.23,31036.22,31081.93],\"g\":[141,118],\"n\":-356,\"b\":\"vol\",\"f\":31083.00,\"m\":[31341.83,30766.83],\"bs\":241.83";

        static void GmLectura()
        {
            int ok0 = _gmOk, f0 = _fallas;
            bool Pa(string l, out double d1, out double d2, out double g1, out double g2) => AlmacenTres.Parsear(l, out _, out d1, out d2, out g1, out g2);
            double d1, d2, g1, g2;
            Chequear("linea con gm", Pa(LINEA_NDX + AlmacenTres.CampoGm(-141.94, 118.36) + "}", out d1, out d2, out g1, out g2) && d1 == 31038.23 && d2 == 31036.22 && g1 == -141.9 && g2 == 118.4,
                     F(d1) + " " + F(d2) + " " + F(g1) + " " + F(g2));
            Chequear("linea vieja sin gm", Pa(LINEA_NDX + "}", out d1, out d2, out g1, out g2) && d1 == 31038.23 && double.IsNaN(g1) && double.IsNaN(g2));
            Chequear("d[0] = 0 -> gm[0] tambien se descarta", Pa("{\"t\":\"2026-10-09T02:32:48Z\",\"d\":[0,31036.22,31081.93],\"gm\":[-141.9,118.4]}", out d1, out d2, out g1, out g2)
                     && double.IsNaN(d1) && double.IsNaN(g1) && d2 == 31036.22 && g2 == 118.4);
            Chequear("d[1] < 15000 -> gm[1] se descarta", Pa("{\"t\":\"2026-10-09T02:32:48Z\",\"d\":[31038.23,6745.25,0],\"gm\":[-141.9,118.4]}", out d1, out d2, out g1, out g2)
                     && g1 == -141.9 && double.IsNaN(d2) && double.IsNaN(g2));
            Chequear("gm null", Pa("{\"t\":\"2026-10-09T02:32:48Z\",\"d\":[31038.23,31036.22,0],\"gm\":[null,5]}", out d1, out d2, out g1, out g2) && double.IsNaN(g1) && g2 == 5);
            Chequear("gm no numero", Pa("{\"t\":\"2026-10-09T02:32:48Z\",\"d\":[31038.23,31036.22,0],\"gm\":[\"x\",5]}", out d1, out d2, out g1, out g2) && double.IsNaN(g1) && g2 == 5);
            Chequear("gm no arreglo: la linea vale igual", Pa("{\"t\":\"2026-10-09T02:32:48Z\",\"d\":[31038.23,31036.22,0],\"gm\":{\"a\":1}}", out d1, out d2, out g1, out g2) && d1 == 31038.23 && double.IsNaN(g1) && double.IsNaN(g2));
            Chequear("gm de un elemento", Pa("{\"t\":\"2026-10-09T02:32:48Z\",\"d\":[31038.23,31036.22,0],\"gm\":[7]}", out d1, out d2, out g1, out g2) && g1 == 7 && double.IsNaN(g2));
            Chequear("gm de tres elementos", Pa("{\"t\":\"2026-10-09T02:32:48Z\",\"d\":[31038.23,31036.22,0],\"gm\":[1,2,3]}", out d1, out d2, out g1, out g2) && g1 == 1 && g2 == 2);
            Chequear("sobrecarga vieja (t, d1, d2) igual", AlmacenTres.Parsear(LINEA_NDX + AlmacenTres.CampoGm(-141.94, 118.36) + "}", out var tv, out var v1, out var v2) && v1 == 31038.23 && v2 == 31036.22
                     && tv == new DateTime(2026, 10, 9, 2, 32, 48, DateTimeKind.Utc));

            // AlmacenTres: por el gancho (Agregar) y por la cola del archivo (LeerArchivos); mismo t -> gana la ultima (con su gm)
            var ses = SesionFamilia.DelDia("2026-10-09", false);
            string L(string t, string d, string gm) => "{\"t\":\"" + t + "\",\"d\":[" + d + "],\"g\":[1,1],\"n\":0,\"b\":\"vol\",\"f\":31083.00,\"m\":[0,0]" + gm + "}";
            var lineasNdx = new[]
            {
                L("2026-10-09T02:30:10Z", "31038.23,31036.22,31081.93", AlmacenTres.CampoGm(-141.94, 118.36)),
                L("2026-10-09T02:31:05Z", "31038.23,31036.22,31081.93", AlmacenTres.CampoGm(-140.0, double.NaN)),
                L("2026-10-09T02:31:05Z", "31038.23,31036.22,31081.93", AlmacenTres.CampoGm(-139.04, 117.0)),   // mismo t: gana esta
                L("2026-10-09T02:32:05Z", "0,31036.22,31081.93", AlmacenTres.CampoGm(-139.0, 116.5)),           // d[0] = 0
            };
            var lineasQqq = new[] { L("2026-10-09T02:30:10Z", "31076.37,31117.80,31107.86", "") };            // estela vieja
            long K(int h, int m) => SesionFamilia.Ms(new DateTime(2026, 10, 9, h, m, 0, DateTimeKind.Utc)) / 60000L;
            void Ver(AlmacenTres at, string como)
            {
                var x1 = at.Niveles("NDX", K(2, 31), out _);   // la linea de 02:30:10
                var x2 = at.Niveles("NDX", K(2, 32), out _);   // la de 02:31:05 (la ultima con ese t)
                var x3 = at.Niveles("NDX", K(2, 33), out _);   // la de 02:32:05: solo D2
                var q1 = at.Niveles("QQQ", K(2, 31), out _);
                Chequear(como + ": NDX 02:31", x1 != null && x1.Length == 2 && x1[0].GexM == -141.9 && x1[1].GexM == 118.4, x1 == null ? "null" : string.Join(" ", x1.Select(n => n.Etq + " " + F(n.GexM))));
                Chequear(como + ": NDX 02:32 (ultima gana)", x2 != null && x2.Length == 2 && x2[0].GexM == -139.0 && x2[1].GexM == 117.0, x2 == null ? "null" : string.Join(" ", x2.Select(n => n.Etq + " " + F(n.GexM))));
                Chequear(como + ": NDX 02:33 (d[0]=0: solo D2 con su gm)", x3 != null && x3.Length == 1 && x3[0].Etq == "D2" && x3[0].GexM == 116.5, x3 == null ? "null" : string.Join(" ", x3.Select(n => n.Etq + " " + F(n.GexM))));
                Chequear(como + ": QQQ estela vieja -> NaN", q1 != null && q1.Length == 2 && double.IsNaN(q1[0].GexM) && double.IsNaN(q1[1].GexM) && q1[0].Precio == 31076.37);
            }
            var a1 = new AlmacenTres(ses.IniUtc, ses.FinUtc);
            foreach (var l in lineasNdx) a1.Agregar("NDX", l);
            foreach (var l in lineasQqq) a1.Agregar("QQQ", l);
            Ver(a1, "gancho");
            var tmp = Path.Combine(RES, "gm_tmp"); var est = Path.Combine(tmp, "estela");
            try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { }
            Directory.CreateDirectory(est);
            File.WriteAllText(Path.Combine(est, "estela-NDX-2026-10-09.jsonl"), string.Join("\n", lineasNdx) + "\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(est, "estela-QQQ-2026-10-09.jsonl"), string.Join("\r\n", lineasQqq) + "\r\n", new UTF8Encoding(false));
            var a2 = new AlmacenTres(ses.IniUtc, ses.FinUtc);
            int nl = a2.LeerArchivos(est, "2026-10-09");
            Chequear("archivo: lineas leidas", nl == 5, nl.ToString(Inv));
            Ver(a2, "archivo");
            try { Directory.Delete(tmp, true); } catch { }

            // SalidaFamilia.Actuales: TRES con GexM = gm (NDX), NaN con la estela vieja (QQQ); el strike sigue NaN
            var calc = CatalogoFamilia.Calc;
            var ru = new RegistroMinuto
            {
                Clave = K(2, 32), Libros = new[] { "NDX", "QQQ" }, Series = new Nivel[calc.Length][], Strikes = new double[calc.Length][],
                Meta = new[] { new MetaLibro { Libro = "NDX", DatoUtc = new DateTime(2026, 10, 8, 20, 14, 53, DateTimeKind.Utc) }, new MetaLibro { Libro = "QQQ", DatoUtc = new DateTime(2026, 10, 8, 20, 14, 59, DateTimeKind.Utc) } }
            };
            var act = SalidaFamilia.Actuales(ru, a1, null);
            var tn = act.Where(a => a.Serie == "TRES_NDX").ToList(); var tq = act.Where(a => a.Serie == "TRES_QQQ").ToList();
            Chequear("Actuales TRES_NDX con GexM", tn.Count == 2 && tn[0].GexM == -139.0 && tn[1].GexM == 117.0 && double.IsNaN(tn[0].Strike) && tn[0].Fuente == "3.0",
                     string.Join(" ", tn.Select(a => a.Rol + " " + F(a.GexM))));
            Chequear("Actuales TRES_QQQ vieja -> NaN", tq.Count == 2 && double.IsNaN(tq[0].GexM) && double.IsNaN(tq[1].GexM));
            P("  lectura: " + (_gmOk - ok0) + " chequeos ok, " + (_fallas - f0) + " fallas");
        }

        // ================================================================== real (capas NDX/QQQ rehechas con el nucleo)
        sealed class NivMin { public double RazonQqq = double.NaN; public Dictionary<string, List<(double K, double M, string E)>> Majors = new Dictionary<string, List<(double, double, string)>>(); }

        static Dictionary<long, NivMin> LeerNiv(string ruta)
        {
            var d = new Dictionary<long, NivMin>();
            foreach (var l in File.ReadLines(ruta))
            {
                if (string.IsNullOrWhiteSpace(l)) continue;
                using var doc = JsonDocument.Parse(l);
                var r = doc.RootElement;
                if (!r.TryGetProperty("k", out var k)) continue;
                var n = new NivMin();
                foreach (var m in r.GetProperty("m").EnumerateArray())
                    if (m.GetProperty("l").GetString() == "QQQ" && m.TryGetProperty("c", out var c) && c.ValueKind == JsonValueKind.Number) n.RazonQqq = c.GetDouble();
                foreach (var sid in new[] { "MAJORS_NDX_vol", "MAJORS_QQQ_vol" })
                    if (r.GetProperty("s").TryGetProperty(sid, out var lv))
                        n.Majors[sid] = lv.EnumerateArray().Where(x => x[3].ValueKind == JsonValueKind.Number && x[2].ValueKind == JsonValueKind.Number)
                                          .Select(x => (x[3].GetDouble(), x[2].GetDouble(), x[1].GetString())).ToList();
                d[k.GetInt64()] = n;
            }
            return d;
        }

        /// <summary>Las cadenas de CBOE del archivo del descargador, parseadas como GammaHoyTresCapas.CargarCapa (8 columnas, vencimiento valido).</summary>
        static List<(DateTime Gen, Feed3.Cadena C)> LeerCadenas(string ruta, string capa, bool porRazon)
        {
            var l0 = new List<(DateTime, Feed3.Cadena)>();
            using var fs = File.OpenRead(ruta);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var sr = new StreamReader(gz, Encoding.UTF8);
            string l;
            while ((l = sr.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(l)) continue;
                using var doc = JsonDocument.Parse(l);
                var r = doc.RootElement; var c = r.GetProperty("cadena");
                if (!DateTimeOffset.TryParse(r.GetProperty("generado").GetString(), Inv, DateTimeStyles.AssumeUniversal, out var go)) continue;
                var dias = c.GetProperty("vencimientos").EnumerateArray().Select(v => v.GetProperty("dias").GetDouble()).ToList();
                var filas = new List<Feed3.Fila>();
                foreach (var x in c.GetProperty("filas").EnumerateArray())
                {
                    var f = new double[8]; int i = 0;
                    foreach (var y in x.EnumerateArray()) { if (i < 8) f[i] = y.GetDouble(); i++; }
                    if (i < 8) continue;
                    int vi = (int)f[1]; if (vi < 0 || vi >= dias.Count) continue;
                    filas.Add(new Feed3.Fila { K = f[0], V = vi, OiC = f[2], OiP = f[3], IvC = f[4], IvP = f[5], VolC = f[6], VolP = f[7] });
                }
                if (dias.Count == 0 || filas.Count == 0) continue;
                var cad = new Feed3.Cadena
                {
                    Ts = c.GetProperty("ts").GetString() ?? "", SpotIdx = c.GetProperty("spot_idx").GetDouble(), Dias = dias.ToArray(), Filas = filas,
                    UltimoTrade = c.TryGetProperty("ultimo_trade", out var ut) ? (ut.GetString() ?? "") : "", HorizonteCadena = dias.Max(),
                    GeneradoUtc = go.UtcDateTime, EsFuturo = false, Fuente = capa + " CBOE", PorRazon = porRazon, Apalancamiento = 1.0,
                };
                l0.Add((go.UtcDateTime, cad));
            }
            return l0.OrderBy(x => x.Item1).ToList();
        }

        static void GmReal()
        {
            int ok0 = _gmOk, f0 = _fallas;
            string dir = Path.Combine(AQUI, "datos", "gm"), dia = "2026-10-09";
            var rutaNiv = Path.Combine(dir, "niv-" + dia + "-MNQZ6.jsonl");
            if (!File.Exists(rutaNiv)) { Falla("gm real: no existe " + rutaNiv + " (correr 'python -I gen_datos_gm.py' en esta carpeta)"); return; }
            var niv = LeerNiv(rutaNiv);
            var ses = SesionFamilia.DelDia(dia, false);
            var at = new AlmacenTres(ses.IniUtc, ses.FinUtc);
            var esperado = new Dictionary<string, List<(DateTime T, double M1, double M2)>>();
            foreach (var (capa, archivo, porRazon) in new[] { ("NDX", "NQ", false), ("QQQ", "QQQ", true) })
            {
                var cadenas = LeerCadenas(Path.Combine(dir, "cadena-" + archivo + "-" + dia + ".jsonl.gz"), capa, porRazon);
                // los ajustes de las capas (GammaHoyTresCapas.CargarCapa con los defaults de la 4.1: Capa3Cuantas 3, Capa3Regla Clasica, radio 100).
                // Un nucleo NUEVO por linea: su estado (fotos del Max Change, lado del pico y alerta) no toca las dominantes, y la alerta arma un
                // texto con la cultura es-AR que este arnes (InvariantGlobalization) no tiene.
                Nucleo3 Nuevo()
                {
                    var n = new Nucleo3(); var a = n.A;
                    a.Horizonte = Nucleo3.HorizonteVenc.Hoy; a.CuantasDominantes = 3; a.RadioDominantesPct = 2.0; a.RadioDominantesMaxPts = 100.0;
                    a.EmpatePct = 20; a.DominantesDeNoche = Nucleo3.NocheDominantes.Volumen; a.ZeroInterpolado = true; a.UnaPorLado = true; a.Centroide = true; a.RadioCentroidePts = 12.0;
                    return n;
                }
                int lineas = 0, rehechas = 0, conPrevia = 0, sinCadena = 0, noRehechas = 0, conMonto = 0, sinMonto = 0, malSigno = 0, comparadas = 0, fuera = 0;
                var difs = new List<double>(); var ej = new List<string>();
                var esp = esperado[capa] = new List<(DateTime, double, double)>();
                foreach (var linea in File.ReadLines(Path.Combine(dir, "estela-" + capa + "-" + dia + ".jsonl")))
                {
                    if (string.IsNullOrWhiteSpace(linea)) continue;
                    lineas++;
                    using var doc = JsonDocument.Parse(linea);
                    var r = doc.RootElement;
                    var t = DateTime.SpecifyKind(DateTime.Parse(r.GetProperty("t").GetString(), Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), DateTimeKind.Utc);
                    var d = r.GetProperty("d").EnumerateArray().Select(x => x.GetDouble()).ToArray();
                    var g = r.GetProperty("g").EnumerateArray().Select(x => x.GetDouble()).ToArray();
                    double fut = r.GetProperty("f").GetDouble();
                    double bs = r.TryGetProperty("bs", out var bsv) ? bsv.GetDouble() : double.NaN;
                    long kt = SesionFamilia.Ms(t) / 60000L;
                    double razon = double.NaN;
                    for (long kk = kt; kk > kt - 30 && double.IsNaN(razon); kk--) if (niv.TryGetValue(kk, out var nm)) razon = nm.RazonQqq;
                    // la cadena: la ultima bajada con generado <= t (la capa la toma de memoria en su latido de 60 s); si no reproduce, la anterior
                    int ic = cadenas.FindLastIndex(x => x.Gen <= t);
                    if (ic < 0) { sinCadena++; continue; }
                    Nucleo3.Lectura L = null; bool rehecha = false;
                    for (int intento = 0; intento < 2 && !rehecha && ic - intento >= 0; intento++)
                    {
                        var cad = cadenas[ic - intento].C;
                        if (porRazon) { cad.Escala = razon; cad.Base = 0; cad.BaseConfiable = false; }
                        else { cad.Base = bs; cad.BaseCruda = bs; cad.BaseConfiable = true; }
                        L = Nuevo().Calcular(cad, fut, t);
                        if (L == null || L.SinBase) continue;
                        if (L.Doms.Count > 1) L.Doms = L.Doms.OrderBy(x => Math.Abs(x.Fut - fut)).ToList();   // CargarCapa: D1 = la mas cercana
                        rehecha = true;
                        for (int i = 0; i < 2 && rehecha; i++)
                        {
                            bool hay = i < d.Length && d[i] > 0;
                            if (!hay) { rehecha = L.Doms.Count <= i || L.Doms[i].Fut <= 0; continue; }
                            rehecha = L.Doms.Count > i && Math.Abs(L.Doms[i].Fut - d[i]) <= 0.011 && i < g.Length && Math.Abs(Math.Round(Math.Abs(L.Doms[i].Gex) / 1e6) - g[i]) <= 1;
                        }
                        if (rehecha && intento > 0) conPrevia++;
                    }
                    if (!rehecha)
                    {
                        noRehechas++;
                        if (ej.Count < 4) ej.Add("no rehecha " + capa + " " + linea.Substring(0, Math.Min(160, linea.Length)) + " | c# " + (L == null ? "null" : string.Join("/", L.Doms.Select(x => x.Fut.ToString("0.00", Inv) + "=" + (x.Gex / 1e6).ToString("0.0", Inv)))));
                        continue;
                    }
                    rehechas++;
                    // lo que escribiria GuardarEstela (CampoGmEstela): los arreglos del perfil y MontosDominantes3
                    var gv = L.Perfil.Select(s => s.GexVol).ToArray(); var go = L.Perfil.Select(s => s.GexOi).ToArray();
                    var (m1, m2) = AlmacenTres.MontosDominantes3(capa, L.Doms, gv, go);
                    var ms = new[] { m1, m2 };
                    for (int i = 0; i < 2; i++)
                    {
                        if (!(i < d.Length && d[i] > 0)) { if (!double.IsNaN(ms[i])) { Falla("gm real: d[" + i + "] = 0 con monto " + F(ms[i])); } continue; }
                        if (double.IsNaN(ms[i])) { sinMonto++; if (ej.Count < 8) ej.Add("sin monto " + capa + " " + r.GetProperty("t").GetString() + " D" + (i + 1) + " gex " + F(L.Doms[i].Gex)); continue; }
                        conMonto++;
                        double gex = L.Doms[i].Gex;
                        if (Math.Sign(ms[i]) != Math.Sign(gex) || Math.Abs(Math.Round(Math.Abs(ms[i])) - g[i]) > 1) { malSigno++; if (ej.Count < 8) ej.Add("signo/g " + capa + " " + F(ms[i]) + " g " + F(g[i])); }
                        // el strike de la dominante: el del perfil con ese gex EXACTO (centroide: el precio se corre, el gex es el del strike)
                        var s = L.Perfil.FirstOrDefault(x => x.GexVol == gex || x.GexOi == gex);
                        if (s == null) continue;
                        if (!niv.TryGetValue(kt, out var nm) || !nm.Majors.TryGetValue("MAJORS_" + capa + "_vol", out var maj)) continue;
                        foreach (var (kk, mm, ee) in maj)
                        {
                            if (Math.Abs(kk - s.K) > 0.006) continue;
                            comparadas++;
                            double dif = Math.Abs(ms[i] - mm) / Math.Max(1.0, Math.Abs(mm));
                            difs.Add(dif);
                            bool okM = Math.Sign(ms[i]) == Math.Sign(mm) && (Math.Abs(ms[i] - mm) <= 0.5 || dif <= 0.05);
                            if (!okM) { fuera++; if (ej.Count < 8) ej.Add("contra MAJORS " + capa + " " + r.GetProperty("t").GetString() + " K " + F(s.K) + ": gm " + ms[i].ToString("0.00", Inv) + " vs " + ee + " " + mm.ToString("0.0", Inv)); }
                            else if (ej.Count < 3 && comparadas <= 2) ej.Add("ej. " + capa + " " + r.GetProperty("t").GetString() + " D" + (i + 1) + " " + d[i].ToString("0.00", Inv) + " K " + F(s.K) + ": gm " + ms[i].ToString("0.00", Inv) + " (g " + g[i] + ") vs MAJORS_" + capa + "_vol " + ee + " " + mm.ToString("0.0", Inv));
                        }
                    }
                    // la linea como la escribe GuardarEstela (lo de siempre + ,"gm":[...] al final) entra a AlmacenTres
                    string conGm = linea.TrimEnd().Substring(0, linea.TrimEnd().Length - 1) + AlmacenTres.CampoGm(m1, m2) + "}";
                    at.Agregar(capa, conGm);
                    double r1 = NumPy.RoundPy(double.Parse(m1.ToString("0.0", Inv), Inv), 1), r2 = NumPy.RoundPy(double.Parse(m2.ToString("0.0", Inv), Inv), 1);
                    esp.Add((t, double.IsNaN(m1) ? double.NaN : r1, double.IsNaN(m2) ? double.NaN : r2));
                }
                difs.Sort();
                P("  real " + capa + ": " + lineas + " lineas, rehechas " + rehechas + " (" + conPrevia + " con la cadena anterior), no rehechas " + noRehechas + ", sin cadena " + sinCadena
                  + "; dominantes con monto " + conMonto + ", sin monto " + sinMonto + ", signo/g mal " + malSigno + "; contra MAJORS_" + capa + "_vol mismo minuto y strike: " + comparadas
                  + " (dif mediana " + (difs.Count > 0 ? (difs[difs.Count / 2] * 100).ToString("0.00", Inv) : "-") + " %, maxima " + (difs.Count > 0 ? (difs[difs.Count - 1] * 100).ToString("0.00", Inv) : "-") + " %), fuera de tolerancia " + fuera);
                foreach (var e in ej) P("    " + e);
                Chequear("real " + capa + ": todas las lineas rehechas", lineas > 0 && rehechas == lineas, rehechas + "/" + lineas);
                Chequear("real " + capa + ": toda dominante de capa tiene monto", sinMonto == 0 && conMonto > 0);
                Chequear("real " + capa + ": signo y |gm| = g", malSigno == 0);
                Chequear("real " + capa + ": contra MAJORS (signo igual, <= 5 %)", comparadas > 0 && fuera == 0, comparadas + " comparadas, " + fuera + " fuera");
            }
            // de la linea con gm a NivelActual.GexM (AlmacenTres.Niveles minuto a minuto y SalidaFamilia.Actuales)
            int okN = 0, malN = 0;
            foreach (var kv in esperado)
            {
                var lst = kv.Value.OrderBy(x => x.T).ToList();
                if (lst.Count == 0) continue;
                long k0 = SesionFamilia.Ms(lst[0].T) / 60000L + 1, k1 = SesionFamilia.Ms(lst[lst.Count - 1].T) / 60000L + 1;
                for (long k = k0; k <= k1; k++)
                {
                    var tn = SesionFamilia.DeClave(k);
                    int j = lst.FindLastIndex(x => x.T <= tn); if (j < 0 || (tn - lst[j].T).TotalSeconds >= AlmacenTres.VidaS[kv.Key]) continue;
                    var x = at.Niveles(kv.Key, k, out _);
                    bool igual = x != null && x.Length >= 1 && Cerca(x[0].GexM, lst[j].M1, 0) && (x.Length < 2 || Cerca(x[1].GexM, lst[j].M2, 0));
                    if (igual) okN++; else { malN++; if (malN <= 3) P("    Niveles " + kv.Key + " " + tn.ToString("HH:mm", Inv) + ": " + (x == null ? "null" : string.Join(" ", x.Select(n => n.Etq + " " + F(n.GexM)))) + " esperado " + F(lst[j].M1) + " " + F(lst[j].M2)); }
                }
            }
            Chequear("real: AlmacenTres.Niveles devuelve el gm escrito (round 1)", okN > 0 && malN == 0, okN + " ok, " + malN + " mal");
            var kUlt = esperado.Values.SelectMany(v => v).Max(v => SesionFamilia.Ms(v.T) / 60000L) + 1;
            var calc = CatalogoFamilia.Calc;
            var ru = new RegistroMinuto { Clave = kUlt, Libros = new[] { "NDX", "QQQ" }, Series = new Nivel[calc.Length][], Strikes = new double[calc.Length][],
                                          Meta = new[] { new MetaLibro { Libro = "NDX", DatoUtc = DateTime.UtcNow }, new MetaLibro { Libro = "QQQ", DatoUtc = DateTime.UtcNow } } };
            var act = SalidaFamilia.Actuales(ru, at, null).Where(a => a.Tipo == "TRES").ToList();
            P("  real: minutos con Niveles iguales al gm escrito " + okN + ", distintos " + malN + "; actuales TRES del ultimo minuto: "
              + string.Join(" | ", act.Select(a => a.Serie + " " + a.Rol + " " + a.Precio.ToString("0.00", Inv) + " GexM " + F(a.GexM))));
            Chequear("real: Actuales TRES_NDX/QQQ con GexM", act.Count >= 2 && act.All(a => !double.IsNaN(a.GexM)));
            P("  real: " + (_gmOk - ok0) + " chequeos ok, " + (_fallas - f0) + " fallas");
        }

        // ================================================================== nq: el factor x0,2 con datos reales
        static void GmNq()
        {
            int ok0 = _gmOk, f0 = _fallas;
            string dia = "2026-10-08";
            var rr = Path.Combine(REF, "ref-" + dia + ".json"); var re = Path.Combine(PG3_ESTELA, "estela-NQ-" + dia + ".jsonl");
            if (!File.Exists(rr) || !File.Exists(re)) { Falla("gm nq: falta " + (!File.Exists(rr) ? rr : re)); return; }
            var niv = LeerJson(rr).RootElement.GetProperty("niv");
            var razones = new List<double>();
            int pares = 0;
            foreach (var l in File.ReadLines(re))
            {
                if (string.IsNullOrWhiteSpace(l)) continue;
                using var doc = JsonDocument.Parse(l);
                var r = doc.RootElement;
                var t = DateTime.SpecifyKind(DateTime.Parse(r.GetProperty("t").GetString(), Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), DateTimeKind.Utc);
                if (t.TimeOfDay < new TimeSpan(13, 30, 0) || t.TimeOfDay >= new TimeSpan(20, 0, 0)) continue;   // la rueda: la histeresis, strikes reales (de noche: relleno)
                if (!r.TryGetProperty("g", out var ge) || !r.TryGetProperty("d", out var de)) continue;
                var d = de.EnumerateArray().Select(x => x.GetDouble()).ToArray(); var g = ge.EnumerateArray().Select(x => x.GetDouble()).ToArray();
                if (!niv.TryGetProperty((SesionFamilia.Ms(t) / 60000L).ToString(Inv), out var nm) || !nm.TryGetProperty("MAJORS_NQ_vol", out var maj)) continue;
                for (int i = 0; i < 2 && i < g.Length && i < d.Length; i++)
                {
                    if (g[i] < 5) continue;   // montos chicos: el redondeo a entero de "g" pesa
                    foreach (var x in maj.EnumerateArray())
                    {
                        if (Math.Abs(x[0].GetDouble() - d[i]) > 0.005 || x[2].ValueKind != JsonValueKind.Number || x[2].GetDouble() == 0) continue;
                        pares++; razones.Add(g[i] * AlmacenTres.FactorMonto3("NQ") / Math.Abs(x[2].GetDouble()));
                    }
                }
            }
            razones.Sort();
            double med = razones.Count > 0 ? razones[razones.Count / 2] : double.NaN;
            P("  nq " + dia + " rueda: " + pares + " pares (linea de la estela 3.0, strike de MAJORS_NQ_vol de la referencia); |g| x FactorMonto3(NQ) / |MAJORS|: mediana "
              + F(Math.Round(med, 4)) + ", p05 " + (razones.Count > 0 ? razones[(int)(razones.Count * 0.05)].ToString("0.000", Inv) : "-") + ", p95 " + (razones.Count > 0 ? razones[(int)(razones.Count * 0.95)].ToString("0.000", Inv) : "-")
              + " (sin el x0,2 la mediana seria " + F(Math.Round(med / 0.2, 3)) + ")");
            Chequear("nq: factor x0,2 (mediana en [0,95; 1,05])", pares >= 100 && med >= 0.95 && med <= 1.05, pares + " pares, mediana " + F(med));
            P("  nq: " + (_gmOk - ok0) + " chequeos ok, " + (_fallas - f0) + " fallas");
        }
    }
}
