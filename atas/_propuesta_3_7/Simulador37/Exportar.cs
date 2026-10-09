using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace PythiaGexTres
{
    /// <summary>
    /// EXPORTAR (Simulador37 --exportar [--sesion yyyy-MM-dd]): los niveles que la 3.7.0 dibujaria en la sesion, minuto a minuto, con la
    /// MISMA seleccion del indicador (Seleccion37 / Conversion37 / nucleo), para la auditoria visual contra lo que dibujaron la 3.0 y la 2.0
    /// (sus estelas, al lado en cada linea). NQ: una linea por foto de viva3 (libro de la 3.0). NDX / QQQ: una linea por cadena de cboe-local
    /// dentro de la sesion. Salida: atas/_propuesta_3_7/Simulador37/salida/niveles37-&lt;sesion&gt;.jsonl. Solo lectura de los datos.
    /// </summary>
    internal static partial class Program
    {
        static readonly string SALIDA = Path.Combine(@"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\atas\_propuesta_3_7\Simulador37", "salida");

        static string J(double x, string f = "0.00") => double.IsNaN(x) || double.IsInfinity(x) || x <= 0 ? "null" : x.ToString(f, Inv);
        static string JArr(params double[] xs) => "[" + string.Join(",", xs.Select(x => J(x))) + "]";

        /// <summary>(t, d1, d2) de una estela (la 3.0 o la 2.0), ordenada.</summary>
        static List<(DateTime T, double D1, double D2)> LeerEstela(string carpeta, string libro, IEnumerable<DateTime> dias)
        {
            var r = new List<(DateTime, double, double)>();
            foreach (var d in dias)
            {
                var ruta = Path.Combine(APP, carpeta, "estela", "estela-" + libro + "-" + d.ToString("yyyy-MM-dd", Inv) + ".jsonl");
                if (!File.Exists(ruta)) continue;
                using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var sr = new StreamReader(fs);
                string l;
                while ((l = sr.ReadLine()) != null)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(l);
                        var e = doc.RootElement;
                        var t = DateTime.Parse(e.GetProperty("t").GetString(), Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                        var dd = e.GetProperty("d").EnumerateArray().Select(x => x.GetDouble()).ToList();
                        r.Add((t, dd.Count > 0 && dd[0] > 15000 ? dd[0] : double.NaN, dd.Count > 1 && dd[1] > 15000 ? dd[1] : double.NaN));
                    }
                    catch { }
                }
            }
            return r.OrderBy(x => x.Item1).ToList();
        }

        static (double D1, double D2) EstelaEn(List<(DateTime T, double D1, double D2)> e, DateTime t, double vidaS = 300)
        {
            int lo = 0, hi = e.Count - 1, j = -1;
            while (lo <= hi) { int m = (lo + hi) / 2; if (e[m].T <= t) { j = m; lo = m + 1; } else hi = m - 1; }
            if (j < 0 || (t - e[j].T).TotalSeconds > vidaS) return (double.NaN, double.NaN);
            return (e[j].D1, e[j].D2);
        }

        static void Exportar(string[] args)
        {
            int ia = Array.IndexOf(args, "--sesion");
            DateTime sesion = ia >= 0 && ia + 1 < args.Length ? DateTime.ParseExact(args[ia + 1], "yyyy-MM-dd", Inv) : SaltoOi37.SesionDe(DateTime.UtcNow);
            var ini = Seleccion37.AUtc(sesion.AddDays(-1).AddHours(18)); var fin = Seleccion37.AUtc(sesion.AddHours(17));
            if (fin > DateTime.UtcNow) fin = DateTime.UtcNow;
            Directory.CreateDirectory(SALIDA);
            var ruta = Path.Combine(SALIDA, "niveles37-" + sesion.ToString("yyyy-MM-dd", Inv) + ".jsonl");
            var cinta = LeerCinta(new[] { sesion.AddDays(-1).ToString("yyyy-MM-dd", Inv), sesion.ToString("yyyy-MM-dd", Inv) });
            var diasUtc = new List<DateTime>(); for (var d = ini.Date.AddDays(-1); d <= fin.Date; d = d.AddDays(1)) diasUtc.Add(d);
            var est3 = LeerEstela("PythiaGex3", "NQ", diasUtc); var est2 = LeerEstela("PythiaGex2", "NQ", diasUtc);
            var est3n = LeerEstela("PythiaGex3", "NDX", diasUtc); var est3q = LeerEstela("PythiaGex3", "QQQ", diasUtc);
            Console.WriteLine("  sesion " + sesion.ToString("yyyy-MM-dd", Inv) + ": " + ini.ToString("MM-dd HH:mm", Inv) + " .. " + fin.ToString("MM-dd HH:mm", Inv) + " UTC; estelas 3.0 NQ " + est3.Count + ", 2.0 NQ " + est2.Count);
            var sb = new StringBuilder();
            int nNq = 0, igual2 = 0, con2 = 0, igual3 = 0, con3 = 0;

            // ---- NQ (viva3, multiplicador 20), con el OI con fecha
            var det = new SaltoOi37();
            foreach (var dia in diasUtc)
                foreach (var f in LeerViva3(dia.ToString("yyyy-MM-dd", Inv)))
                {
                    var mapa = new Dictionary<string, double>();
                    foreach (var x in f.Filas) mapa[x[0].ToString("0.##", Inv) + "|" + f.Ts.AddDays(x[1]).ToString("MM-dd", Inv) + "|" + (x[2] >= 0.5 ? "C" : "P")] = x[3];
                    det.Observar(f.Ts, mapa);
                    if (f.Ts < ini || f.Ts > fin) continue;
                    var c = CadenaDeViva(f, 20); if (c == null) continue;
                    double fut = PrecioEn(cinta, f.Ts); if (double.IsNaN(fut)) fut = f.Futuro;
                    var L = NucleoNq().Calcular(c, fut, f.Ts); if (L == null || L.SinBase) continue;
                    bool oiOk = !det.OiDeAnteayer(f.Ts, out _);
                    var d = Dibujo37.DominantesNq(L.Perfil, fut, 100, oiOk, out string libro);   // la misma funcion que llama el indicador (sin ATAS)
                    var f1 = Seleccion37.Formula(FormulaDoms3.Cruces, L, fut, fut, 100, oiOk); var f5 = Seleccion37.Formula(FormulaDoms3.MurosVol, L, fut, fut, 100, oiOk);
                    var t3 = EstelaEn(est3, f.Ts); var t2 = EstelaEn(est2, f.Ts);
                    double a1 = d.Count > 0 ? d[0].Fut : double.NaN, a2 = d.Count > 1 ? d[1].Fut : double.NaN;
                    bool Igual(double x1, double x2) { var s1 = new[] { a1, a2 }.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToArray(); var s2 = new[] { x1, x2 }.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToArray(); return s1.Length == s2.Length && s1.Zip(s2, (p, q) => Math.Abs(p - q) <= 0.01).All(v => v); }
                    if (!double.IsNaN(t2.D1)) { con2++; if (Igual(t2.D1, t2.D2)) igual2++; }
                    if (!double.IsNaN(t3.D1)) { con3++; if (Igual(t3.D1, t3.D2)) igual3++; }
                    nNq++;
                    sb.Append("{\"t\":\"").Append(f.Ts.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv)).Append("\",\"libro\":\"NQ\",\"f\":").Append(J(fut))
                      .Append(",\"d\":").Append(JArr(a1, a2)).Append(",\"g\":[").Append(string.Join(",", d.Select(x => (x.Gex / 1e6).ToString("0.0", Inv)))).Append("],\"b\":\"").Append(libro)
                      .Append("\",\"f1\":").Append(JArr(f1.A, f1.B)).Append(",\"f5\":").Append(JArr(f5.A, f5.B))
                      .Append(",\"m\":").Append(oiOk ? JArr(L.MpOi, L.MnOi) : "null").Append(",\"cruce\":").Append(J(L.ZeroVol)).Append(",\"oi\":\"").Append(oiOk ? "ayer" : "anteayer")
                      .Append("\",\"mu\":20,\"tres\":").Append(JArr(t3.D1, t3.D2)).Append(",\"dos\":").Append(JArr(t2.D1, t2.D2)).Append("}\n");
                }
            Console.WriteLine("  NQ: " + nNq + " fotos; D1/D2 de la 3.7.0 = los de la 2.0 en " + igual2 + " de " + con2 + " minutos con estela de la 2.0 (" + (con2 > 0 ? (100.0 * igual2 / con2).ToString("0", Inv) : "-") + " %; libros distintos: la 2.0 tiene el suyo)"
                + ", = los de la 3.0 en " + igual3 + " de " + con3);
            if (det.SaltoUtc != null) Console.WriteLine("  salto de OI de la sesion: " + det.SaltoUtc.Value.ToString("HH:mm:ss", Inv) + " UTC");

            // ---- NDX / QQQ (cboe-local), conversion sincronizada desde 5 dias antes
            var venc = Seleccion37.VencimientoTrimestralUtc("MNQZ6", sesion);
            foreach (var (archivo, nombre, porRazon) in new[] { ("NQ", "NDX", false), ("QQQ", "QQQ", true) })
            {
                var conv = new ConversionCboe37(porRazon); var fOi = new FechaOiCboe37(porRazon ? new TimeSpan(5, 15, 0) : new TimeSpan(2, 35, 0));
                var estCapa = porRazon ? est3q : est3n;
                int n = 0;
                for (var d = ini.Date.AddDays(-5); d <= fin.Date; d = d.AddDays(1))
                {
                    var rutaA = Path.Combine(CBOE, "cadena-" + archivo + "-" + d.ToString("yyyy-MM-dd", Inv) + ".jsonl.gz");
                    if (!File.Exists(rutaA)) continue;
                    using var fs = new FileStream(rutaA, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var gz = new GZipStream(fs, CompressionMode.Decompress);
                    using var sr = new StreamReader(gz, Encoding.UTF8);
                    string l;
                    while (true)
                    {
                        try { l = sr.ReadLine(); } catch (InvalidDataException) { break; }
                        if (l == null) break;
                        try
                        {
                            using var doc = JsonDocument.Parse(l);
                            var r = doc.RootElement; var c = r.GetProperty("cadena");
                            var gen = DateTimeOffset.Parse(r.GetProperty("generado").GetString(), Inv).UtcDateTime;
                            var ts = DateTime.SpecifyKind(DateTime.ParseExact(r.GetProperty("cadena_ts").GetString(), "yyyy-MM-dd HH:mm:ss", Inv), DateTimeKind.Utc);
                            double spot = r.GetProperty("spot").GetDouble();
                            double precio = PrecioEn(cinta, ts.AddSeconds(-ConversionCboe37.RETRASO_S));
                            bool nuevo = conv.Observar(gen, ts, spot, precio, "Z6");
                            var filas = new List<Feed.Fila>(); double oiTot = 0;
                            var dias = c.GetProperty("vencimientos").EnumerateArray().Select(v => v.GetProperty("dias").GetDouble()).ToArray();
                            foreach (var x in c.GetProperty("filas").EnumerateArray())
                            {
                                var v = x.EnumerateArray().Select(y => y.GetDouble()).ToArray(); if (v.Length < 8) continue;
                                int vi = (int)v[1]; if (vi < 0 || vi >= dias.Length) continue;
                                filas.Add(new Feed.Fila { K = v[0], V = vi, OiC = v[2], OiP = v[3], IvC = v[4], IvP = v[5], VolC = v[6], VolP = v[7] }); oiTot += v[2] + v[3];
                            }
                            if (nuevo) fOi.Observar(gen, oiTot);
                            if (gen < ini || gen > fin) continue;
                            double fut = PrecioEn(cinta, gen); if (double.IsNaN(fut)) continue;
                            var val = conv.Calcular(gen, "Z6", venc); if (double.IsNaN(val.V)) continue;
                            DateTime ult = DateTime.MinValue;
                            if (c.TryGetProperty("ultimo_trade", out var ut) && DateTime.TryParseExact(ut.GetString(), "yyyy-MM-dd'T'HH:mm:ss", Inv, DateTimeStyles.None, out var uny)) ult = Seleccion37.AUtc(uny);
                            var cad = new Feed.Cadena { Ts = c.GetProperty("ts").GetString(), SpotIdx = spot, Dias = dias, Filas = filas, EsFuturo = false, PorRazon = porRazon, Apalancamiento = 1.0, GeneradoUtc = gen, Multiplicador = 100 };
                            if (porRazon) cad.Escala = val.V; else { cad.Base = val.V; cad.BaseConfiable = true; }
                            var nuc = NucleoNq();
                            var L = nuc.Calcular(cad, fut, gen); if (L == null || L.SinBase) continue;
                            Dibujo37.PrepararCapa(L, cad, fut, gen, 100, nuc.A.Tasa);   // 3.7.1 (A6/A4): la misma funcion que el indicador
                            var dd = L.Doms;
                            var cer = Seleccion37.Cerco(Seleccion37.CrucesPerfil(L.Perfil, true, true).Select(z => z.Z), fut);
                            var e3 = EstelaEn(estCapa, gen, 1500);
                            n++;
                            sb.Append("{\"t\":\"").Append(gen.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv)).Append("\",\"libro\":\"").Append(nombre).Append("\",\"f\":").Append(J(fut))
                              .Append(",\"conv\":").Append(val.V.ToString(porRazon ? "0.0000" : "0.00", Inv)).Append(",\"modo\":\"").Append(val.Modo).Append("\"")
                              .Append(",\"d\":").Append(JArr(dd.Select(x => x.Fut).Concat(new[] { double.NaN, double.NaN }).Take(2).ToArray()))
                              .Append(",\"techo\":").Append(J(cer.Techo)).Append(",\"piso\":").Append(J(cer.Piso)).Append(",\"cruce\":").Append(J(L.ZeroVol))
                              .Append(",\"m\":").Append(JArr(L.MpOi, L.MnOi)).Append(",\"of\":\"").Append(fOi.Rotulo(gen)).Append("\",\"congelada\":").Append(Seleccion37.Congelada(ult) ? "true" : "false")
                              .Append(",\"tres\":").Append(JArr(e3.D1, e3.D2)).Append("}\n");
                        }
                        catch { }
                    }
                }
                Console.WriteLine("  " + nombre + ": " + n + " cadenas en la sesion; " + conv.Resumen());
            }
            File.WriteAllText(ruta, sb.ToString(), new UTF8Encoding(false));
            Console.WriteLine("  escrito " + ruta + " (" + (new FileInfo(ruta).Length / 1024) + " KB)");
            Check("exportar: hay niveles de NQ de la sesion", nNq > 0, nNq + " fotos");
        }
    }
}
