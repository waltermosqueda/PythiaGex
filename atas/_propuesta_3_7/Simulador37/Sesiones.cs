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
    /// SESIONES (Simulador37 --sesiones, 08-10-2026). Reproduce minuto a minuto lo que la 3.7.0 CALCULARIA y DIBUJARIA en un grafico m1 de
    /// MNQZ6, con los MISMOS archivos del indicador (nucleo, Feed, Black-76, Seleccion37, Conversion37 y Dibujo37) y sin mirar adelante:
    /// en el minuto T solo entra lo que existia a las T (fotos viva3 con ts &lt;= T, cadenas de CBOE con generado &lt;= T, ticks &lt;= T).
    ///
    /// Una sola linea de tiempo continua desde la primera foto viva3 (10-06 19:06 UTC) hasta el ultimo dato, con el estado que el indicador
    /// arrastra (salto de OI, conversion sincronizada sembrada con 5 dias de archivo, pares NDX/QQQ, ultima capa calculada); se escribe por
    /// sesion de CME (18:00 NY de la vispera a 17:00 NY):
    ///   salidas/niveles37-&lt;sesion&gt;.csv   una fila por minuto: todos los niveles por libro y tipo, con su fuente, su edad y el estado del OI,
    ///                                      la base de NDX / razon de QQQ usada, la alarma de familia y, como referencia, lo que DIBUJARON la
    ///                                      clasica, la 2.0 y la 3.0 instalada (sus estelas guardadas).
    ///   salidas/marcas37-&lt;sesion&gt;.jsonl  una linea por vela (m1; m2 donde no hay m1): las marcas que se ANOTAN al cerrar y si se DIBUJAN con el
    ///                                      radio de 50 pts contra el cierre de esa vela (D1), y la vista de la vela viva en su ultimo instante:
    ///                                      marcas de la vela viva, rotulos de adentro (texto Columna) y rotulos del borde (texto exacto).
    ///   salidas/resumen37-&lt;sesion&gt;.json   fuentes, cobertura, huecos, casillas usadas (.ws del operador + defaults 3.7.0), conteos y controles.
    /// Solo lectura de todos los datos (FileShare.ReadWrite: la 3.0 instalada sigue escribiendo). Prioridad baja.
    /// </summary>
    internal static partial class Program
    {
        static readonly string SALIDAS_BASE = @"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\atas\_propuesta_3_7\Simulador37";
        /// <summary>3.7.1: 'salidas' con las casillas del operador + defaults 3.7.1; con --todas, 'salidas_todas' (todas las familias prendidas,
        /// para medir cuanto ruido mete cada una sin volver a correr: laboratorio/tres/auditoria_0810/ablacion_371.py).</summary>
        static string SALIDAS = Path.Combine(SALIDAS_BASE, "salidas");
        static readonly double TASA_CAPA = new GammaHoyNucleo().A.Tasa;   // la del nucleo de la capa (CargarCapa usa k.Nucleo.A.Tasa)
        static readonly string VELAS_M2 = @"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\tres\datos\velas_cache_2026-10-07\velas-MNQ-m2.csv";
        static readonly string S30_30 = Path.Combine(APP, "pythiagex3-centinela-hoy-MNQZ6-Seconds-30.jsonl");
        static readonly string WS = Path.Combine(APP, "Workspaces_v3", "MNQ liviano.ws");
        static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-AR");
        const double VIDA_CUENTA_S = 180, TICK = 0.25;
        const string CONTRATO = "Z6";

        // ------------------------------------------------------------------ casillas: el .ws del operador + los defaults de la 3.7.0

        sealed class Casillas
        {
            public readonly Dictionary<string, string> Origen = new Dictionary<string, string>();
            private readonly Dictionary<string, JsonElement> _ws;
            public Casillas(Dictionary<string, JsonElement> ws) { _ws = ws ?? new Dictionary<string, JsonElement>(); }
            public bool B(string n, bool def)
            {
                if (_ws.TryGetValue(n, out var e) && (e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.False)) { Origen[n] = ".ws " + e.GetBoolean(); return e.GetBoolean(); }
                Origen[n] = "default 3.7.3 " + def; return def;
            }
            public string S(string n, string def)
            {
                if (_ws.TryGetValue(n, out var e) && e.ValueKind == JsonValueKind.String) { Origen[n] = ".ws " + e.GetString(); return e.GetString(); }
                Origen[n] = "default 3.7.3 " + def; return def;
            }
            public double D(string n, double def)
            {
                if (_ws.TryGetValue(n, out var e) && e.ValueKind == JsonValueKind.Number) { Origen[n] = ".ws " + e.GetDouble().ToString(Inv); return e.GetDouble(); }
                Origen[n] = "default 3.7.3 " + def.ToString(Inv); return def;
            }
        }

        static Dictionary<string, JsonElement> LeerWs(out string msg)
        {
            msg = "";
            try
            {
                string txt;
                using (var fs = new FileStream(WS, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var sr = new StreamReader(fs, Encoding.UTF8)) txt = sr.ReadToEnd();
                var hallados = new List<Dictionary<string, JsonElement>>();
                void Buscar(JsonElement e)
                {
                    if (e.ValueKind == JsonValueKind.Object)
                        foreach (var p in e.EnumerateObject())
                        {
                            if (p.Name == "SerializedIndicators" && p.Value.ValueKind == JsonValueKind.Array)
                                foreach (var x in p.Value.EnumerateArray())
                                {
                                    if (x.ValueKind != JsonValueKind.String) continue;
                                    try
                                    {
                                        using var d1 = JsonDocument.Parse(x.GetString());
                                        if (!(d1.RootElement.TryGetProperty("Type", out var ty) && (ty.GetString() ?? "").StartsWith("PythiaGexTres.GammaHoyTres,", StringComparison.Ordinal))) continue;
                                        var d2 = JsonDocument.Parse(d1.RootElement.GetProperty("Settings").GetString());
                                        hallados.Add(d2.RootElement.EnumerateObject().ToDictionary(q => q.Name, q => q.Value.Clone()));
                                    }
                                    catch { }
                                }
                            else Buscar(p.Value);
                        }
                    else if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) Buscar(x);
                }
                using var doc = JsonDocument.Parse(txt);
                Buscar(doc.RootElement);
                msg = hallados.Count + " instancia(s) de PythiaGexTres.GammaHoyTres en " + WS + " (" + File.GetLastWriteTime(WS).ToString("dd-MM HH:mm", Inv) + " local)";
                return hallados.FirstOrDefault();
            }
            catch (Exception e) { msg = "no pude leer el .ws (" + e.GetType().Name + ": " + e.Message + "): uso los defaults de la 3.7.0"; return null; }
        }

        // ------------------------------------------------------------------ velas y precio

        sealed class Vela { public DateTime T; public int Marco; public double O, H, L, C; public string Fuente; public DateTime Cierre => T.AddSeconds(Marco); }

        static readonly DateTime EPOCA = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        static long Ms(DateTime t) => (long)(t - EPOCA).TotalMilliseconds;

        /// <summary>El ultimo tick de la cinta en o antes de t (sin limite de edad: lo decide quien llama). (NaN, MinValue) si no hay.</summary>
        static (double P, DateTime T) UltimoTick((long[] T, double[] P) c, DateTime t)
        {
            if (c.T == null || c.T.Length == 0) return (double.NaN, DateTime.MinValue);
            long ms = Ms(t);
            int j = Array.BinarySearch(c.T, ms);
            if (j < 0) j = ~j - 1; else while (j + 1 < c.T.Length && c.T[j + 1] == ms) j++;
            return j < 0 ? (double.NaN, DateTime.MinValue) : (c.P[j], DeMs(c.T[j]));
        }
        static DateTime DeMs(long ms) => EPOCA.AddMilliseconds(ms);

        /// <summary>La cinta de la 3.0 (profundidad/estado/cinta): el VIVO manda; el RELLENO (historia que ATAS bajo al reconectar) entra antes del
        /// primer tick del vivo, despues del ultimo y en los huecos del vivo de mas de 20 s (medido: el vivo del 08-10 no tiene 07:08:10-07:21:04
        /// UTC y otros 11 huecos de 61-119 s; el relleno los tiene). LeerCinta (Prueba6) usa solo el relleno previo, como fuentes_3_0.py.</summary>
        static (long[] T, double[] P) LeerCintaRellena(IEnumerable<string> sesiones, out int rellenados, out string huecosVivo)
        {
            rellenados = 0; var huecos = new List<string>();
            var t = new List<long>(); var p = new List<double>();
            foreach (var s in sesiones)
            {
                List<(long T, double P)> Leer(string ruta)
                {
                    var l0 = new List<(long, double)>();
                    if (!File.Exists(ruta)) return l0;
                    using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var sr = new StreamReader(fs);
                    string l;
                    while ((l = sr.ReadLine()) != null)
                    {
                        if (l.Length < 5 || l[0] == 't') continue;
                        int a = l.IndexOf(','); if (a < 0) continue; int b = l.IndexOf(',', a + 1); if (b < 0) b = l.Length;
                        if (long.TryParse(l.AsSpan(0, a), NumberStyles.Integer, Inv, out var tt) && double.TryParse(l.AsSpan(a + 1, b - a - 1), NumberStyles.Float, Inv, out var pp)) l0.Add((tt, pp));
                    }
                    return l0.Select((x, i) => (x, i)).OrderBy(z => z.x.Item1).ThenBy(z => z.i).Select(z => z.x).ToList();
                }
                var vt = Leer(Path.Combine(CINTA, "cinta-NQ-" + s + ".csv"));
                var rt = Leer(Path.Combine(CINTA, "cinta-NQ-" + s + "-relleno.csv"));
                var tv = vt.Select(x => x.T).ToArray();
                for (int i = 1; i < tv.Length; i++) if (tv[i] - tv[i - 1] > 60000) huecos.Add(DeMs(tv[i - 1]).ToString("MM-dd HH:mm:ss", Inv) + "-" + DeMs(tv[i]).ToString("HH:mm:ss", Inv));
                foreach (var x in vt) { t.Add(x.T); p.Add(x.P); }
                foreach (var x in rt)
                {
                    bool entra;
                    if (tv.Length == 0 || x.T < tv[0] || x.T > tv[tv.Length - 1]) entra = true;
                    else
                    {
                        int j = Array.BinarySearch(tv, x.T);
                        if (j >= 0) entra = false;
                        else { j = ~j; entra = j > 0 && j < tv.Length && tv[j] - tv[j - 1] > 20000; }
                        if (entra) rellenados++;
                    }
                    if (entra) { t.Add(x.T); p.Add(x.P); }
                }
            }
            var idx = Enumerable.Range(0, t.Count).OrderBy(i => t[i]).ToArray();
            huecosVivo = huecos.Count == 0 ? "ninguno" : huecos.Count + " (" + string.Join(", ", huecos.Take(14)) + (huecos.Count > 14 ? ", ..." : "") + ")";
            return (idx.Select(i => t[i]).ToArray(), idx.Select(i => p[i]).ToArray());
        }

        /// <summary>Velas m1 de la cinta (minuto UTC; solo las CERRADAS respecto del ultimo tick; minuto sin ticks = sin vela, como ATAS).</summary>
        static List<Vela> VelasCinta((long[] T, double[] P) c)
        {
            var r = new List<Vela>();
            if (c.T.Length == 0) return r;
            long ultimo = c.T[c.T.Length - 1];
            int i = 0, n = c.T.Length;
            while (i < n)
            {
                long b = c.T[i] / 60000 * 60000;
                double o = c.P[i], h = o, l = o, cl = o;
                int j = i;
                while (j < n && c.T[j] / 60000 * 60000 == b) { double p = c.P[j]; if (p > h) h = p; if (p < l) l = p; cl = p; j++; }
                if (b + 60000 <= ultimo) r.Add(new Vela { T = DeMs(b), Marco = 60, O = o, H = h, L = l, C = cl, Fuente = "cinta (ticks)" });
                i = j;
            }
            return r;
        }

        /// <summary>Las velas m2 de MNQ exportadas de la cache de ATAS (hasta la sesion del 06-10 inclusive): la fuente que uso backtest_familia
        /// para el precio donde no hay cinta.</summary>
        static List<Vela> VelasM2Export()
        {
            var r = new List<Vela>();
            if (!File.Exists(VELAS_M2)) return r;
            foreach (var l in File.ReadLines(VELAS_M2))
            {
                if (l.Length < 10 || l[0] == 't') continue;
                var p = l.Split(',');
                if (p.Length < 6) continue;
                if (!DateTime.TryParseExact(p[0], "yyyy-MM-dd'T'HH:mm:ss", Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)) continue;
                r.Add(new Vela { T = DateTime.SpecifyKind(t, DateTimeKind.Utc), Marco = 120, O = double.Parse(p[2], Inv), H = double.Parse(p[3], Inv), L = double.Parse(p[4], Inv), C = double.Parse(p[5], Inv), Fuente = "cache ATAS m2 (no hay m1)" });
            }
            return r.OrderBy(v => v.T).ToList();
        }

        /// <summary>Velas m1 armadas de pares de velas de 30 s del centinela de la 3.0 (10-06 20:10-20:58 UTC): solo minutos con las dos mitades.</summary>
        static List<Vela> VelasM1DeS30()
        {
            var r = new List<Vela>();
            if (!File.Exists(S30_30)) return r;
            var porMin = new SortedDictionary<DateTime, List<(DateTime T, double O, double H, double L, double C)>>();
            using (var fs = new FileStream(S30_30, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sr = new StreamReader(fs))
            {
                string l;
                while ((l = sr.ReadLine()) != null)
                {
                    try
                    {
                        using var d = JsonDocument.Parse(l); var e = d.RootElement;
                        var t = DateTime.SpecifyKind(DateTime.ParseExact(e.GetProperty("t").GetString(), "yyyy-MM-dd'T'HH:mm:ss", Inv), DateTimeKind.Utc);
                        var m = new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, DateTimeKind.Utc);
                        if (!porMin.TryGetValue(m, out var lst)) porMin[m] = lst = new List<(DateTime, double, double, double, double)>();
                        lst.Add((t, e.GetProperty("o").GetDouble(), e.GetProperty("h").GetDouble(), e.GetProperty("l").GetDouble(), e.GetProperty("c").GetDouble()));
                    }
                    catch { }
                }
            }
            foreach (var kv in porMin)
            {
                var x = kv.Value.OrderBy(q => q.T).ToList();
                if (x.Count != 2) continue;
                r.Add(new Vela { T = kv.Key, Marco = 60, O = x[0].O, H = Math.Max(x[0].H, x[1].H), L = Math.Min(x[0].L, x[1].L), C = x[1].C, Fuente = "centinela 3.0 S30 (2 x 30 s)" });
            }
            return r;
        }

        sealed class Precios
        {
            public (long[] T, double[] P) Cinta;
            public List<Vela> M2;
            private DateTime[] _m2t;
            public int PorTick, PorVela, Sin;
            public Precios((long[] T, double[] P) cinta, List<Vela> m2) { Cinta = cinta; M2 = m2; _m2t = m2.Select(v => v.T).ToArray(); }
            /// <summary>backtest_familia.Precio: ultimo tick de los 120 s previos; si no, la vela m2 que contiene el instante interpolada.</summary>
            public double En(DateTime t, out string fuente)
            {
                double p = Cinta.T.Length > 0 ? PrecioEn(Cinta, t) : double.NaN;
                if (!double.IsNaN(p)) { fuente = "tick"; PorTick++; return p; }
                int i = Array.BinarySearch(_m2t, t); if (i < 0) i = ~i - 1;
                if (i >= 0)
                {
                    double dt = (t - _m2t[i]).TotalSeconds;
                    if (dt >= 0 && dt < 120) { fuente = "m2 interpolada"; PorVela++; return M2[i].O + (M2[i].C - M2[i].O) * dt / 120.0; }
                }
                fuente = "-"; Sin++; return double.NaN;
            }
        }

        // ------------------------------------------------------------------ CBOE: el archivo de cboe-local, linea por linea

        sealed class LineaCboe
        {
            public DateTime Gen, Ts, UltimoUtc; public double Spot, SpotIdx, BaseCruda = double.NaN, OiTot; public double[] Dias; public List<Feed.Fila> Filas; public string TsTxt, UltimoTxt;
        }

        static IEnumerable<LineaCboe> LeerCboe(string archivo, DateTime desdeDia, DateTime hastaDia)
        {
            for (var d = desdeDia.Date; d <= hastaDia.Date; d = d.AddDays(1))
            {
                var ruta = Path.Combine(CBOE, "cadena-" + archivo + "-" + d.ToString("yyyy-MM-dd", Inv) + ".jsonl.gz");
                if (!File.Exists(ruta)) continue;
                var lineas = new List<string>();
                using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                using (var sr = new StreamReader(gz, Encoding.UTF8))
                {
                    try { string l; while ((l = sr.ReadLine()) != null) lineas.Add(l); }
                    catch (InvalidDataException) { }   // el bajador puede estar escribiendo el final
                    catch (EndOfStreamException) { }
                }
                foreach (var l in lineas)
                {
                    LineaCboe x = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(l);
                        var r = doc.RootElement; var c = r.GetProperty("cadena");
                        x = new LineaCboe
                        {
                            Gen = DateTimeOffset.Parse(r.GetProperty("generado").GetString(), Inv).UtcDateTime,
                            Ts = DateTime.SpecifyKind(DateTime.ParseExact(r.GetProperty("cadena_ts").GetString(), "yyyy-MM-dd HH:mm:ss", Inv), DateTimeKind.Utc),
                            Spot = r.GetProperty("spot").GetDouble(), SpotIdx = c.TryGetProperty("spot_idx", out var si) && si.ValueKind == JsonValueKind.Number ? si.GetDouble() : r.GetProperty("spot").GetDouble(),
                            TsTxt = c.GetProperty("ts").GetString() ?? "", UltimoTxt = c.TryGetProperty("ultimo_trade", out var ut) ? ut.GetString() ?? "" : "",
                        };
                        if (r.TryGetProperty("base_cruda", out var bc) && bc.ValueKind == JsonValueKind.Number) x.BaseCruda = bc.GetDouble();
                        x.UltimoUtc = DateTime.TryParseExact(x.UltimoTxt, "yyyy-MM-dd'T'HH:mm:ss", Inv, DateTimeStyles.None, out var uny) ? Seleccion37.AUtc(uny) : DateTime.MinValue;
                        x.Dias = c.GetProperty("vencimientos").EnumerateArray().Select(v => v.GetProperty("dias").GetDouble()).ToArray();
                        x.Filas = new List<Feed.Fila>();
                        foreach (var f in c.GetProperty("filas").EnumerateArray())
                        {
                            var v = new double[8]; int i = 0;
                            foreach (var y in f.EnumerateArray()) { if (i < 8) v[i] = y.GetDouble(); i++; }
                            if (i < 8) continue;
                            int vi = (int)v[1]; if (vi < 0 || vi >= x.Dias.Length) continue;
                            x.Filas.Add(new Feed.Fila { K = v[0], V = vi, OiC = v[2], OiP = v[3], IvC = v[4], IvP = v[5], VolC = v[6], VolP = v[7] });
                            x.OiTot += v[2] + v[3];
                        }
                        if (x.Dias.Length == 0 || x.Filas.Count == 0 || x.Spot <= 0) x = null;
                    }
                    catch { x = null; }
                    if (x != null) yield return x;
                }
            }
        }

        /// <summary>Una capa de CBOE como la lleva el indicador (CargarCapa / Mapear37 / RegistrarCapasVela), sin ATAS.</summary>
        sealed class CapaSim
        {
            public string Nombre, Archivo; public bool PorRazon;
            public ConversionCboe37 Conv; public FechaOiCboe37 FechaOi;
            public IEnumerator<LineaCboe> Flujo; public LineaCboe Proxima, Ultima;
            public int Leidas, Nuevas;
            // lo ultimo calculado (k.L y compania)
            public GammaHoyNucleo.Lectura L; public DateTime Gen = DateTime.MinValue, UltimoUtc = DateTime.MinValue, Vigente = DateTime.MinValue, CalcUtc = DateTime.MinValue;
            public double Spot = double.NaN, Valor = double.NaN, ValorSinCarry = double.NaN; public string Modo = "", Texto = ""; public int N;
            public bool Recalculada;
            public void Avanzar() { Proxima = Flujo.MoveNext() ? Flujo.Current : null; }
        }

        static GammaHoyNucleo NucleoCapa()
        {
            var n = new GammaHoyNucleo(); var a = n.A;   // CargarCapa: los ajustes de la primaria + una por lado, strike exacto
            a.Horizonte = GammaHoyNucleo.HorizonteVenc.Hoy; a.CuantasDominantes = 2; a.RadioDominantesPct = 2.0; a.RadioDominantesMaxPts = 100;
            a.EmpatePct = 20; a.DominantesDeNoche = GammaHoyNucleo.NocheDominantes.Volumen; a.ZeroInterpolado = true; a.UnaPorLado = false; a.Centroide = false; a.RadioCentroidePts = 12.0;   // 3.7.1 (A6)
            return n;
        }

        /// <summary>CargarCapa + Mapear37 en el minuto T con la ULTIMA cadena (generado &lt;= T), el precio de ahora y la conversion de ahora. El
        /// indicador relee ultima-*.json cada 60 s si tiene &lt;= 20 min y si no, la de la nube; con la cadena vieja igual vuelve a contar cada minuto
        /// (MEDIDO en pythiagex3-gammahoy.log, 08-10 05:46 ART: 'capa QQQ: nube generado hace 285 min ... doms 31241.06/31282.50/31199.63' minuto
        /// a minuto, con la misma cadena de las 04:01 UTC que el archivo de cboe-local). SUPUESTO: la nube respondio toda la noche.</summary>
        static void CalcularCapa(CapaSim k, DateTime T, double fut, DateTime venc)
        {
            k.Recalculada = false;
            if (k.Ultima == null || fut <= 0 || double.IsNaN(fut)) return;
            k.Recalculada = true;
            var v = k.Conv.Calcular(T, CONTRATO, venc);
            double mapa = v.V; string texto = v.Texto, modo = v.Modo; double sinCarry = v.VSinCarry;
            if (!k.PorRazon && double.IsNaN(mapa) && !double.IsNaN(k.Ultima.BaseCruda) && k.Ultima.BaseCruda > 0)
            { texto = "SIN SINCRONIZAR (" + v.Texto + "): base forwards " + k.Ultima.BaseCruda.ToString("0.00", Inv) + " hasta tener la rueda"; mapa = k.Ultima.BaseCruda; modo = "forwards"; sinCarry = mapa; }
            k.Valor = mapa; k.ValorSinCarry = sinCarry; k.Modo = modo; k.Texto = texto; k.N = v.N;
            if (double.IsNaN(mapa) || mapa == 0) { k.L = null; return; }
            var u = k.Ultima;
            var cad = new Feed.Cadena
            {
                Ts = u.TsTxt, SpotIdx = u.SpotIdx, Dias = u.Dias, Filas = u.Filas, UltimoTrade = u.UltimoTxt, HorizonteCadena = u.Dias.Max(), RecibidoUtc = T, GeneradoUtc = u.Gen,
                EsFuturo = false, Fuente = k.Nombre + " CBOE (simulador)", PorRazon = k.PorRazon, Apalancamiento = 1.0, Multiplicador = 100.0,
            };
            if (k.PorRazon) { cad.Escala = mapa; cad.EscalaOrigen = texto; cad.Base = 0; cad.BaseConfiable = false; }
            else { cad.Base = mapa; cad.BaseCruda = mapa; cad.BaseConfiable = true; cad.EscalaOrigen = texto; }
            var L = NucleoCapa().Calcular(cad, fut, T);
            if (L != null && !L.SinBase && L.Perfil != null) Dibujo37.PrepararCapa(L, cad, fut, T, 100, TASA_CAPA);   // 3.7.1 (A6/A4): la MISMA que CargarCapa
            k.L = L != null && !L.SinBase ? L : null;
            k.Gen = u.Gen; k.UltimoUtc = u.UltimoUtc; k.Vigente = Seleccion37.VigenciaCboe(u.Gen, u.UltimoUtc); k.Spot = u.SpotIdx; k.CalcUtc = T;   // paridad 08-10: spot_idx, como CargarCapa
        }

        // ------------------------------------------------------------------ salida

        static string N2(double x, string f = "0.00") => double.IsNaN(x) || double.IsInfinity(x) ? "" : x.ToString(f, Inv);
        static string JN(double x, string f = "0.00") => double.IsNaN(x) || double.IsInfinity(x) ? "null" : x.ToString(f, Inv);
        static string JS(string s) => s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        static string Csv(string s) => string.IsNullOrEmpty(s) ? "" : (s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s);
        static string Hm(DateTime utc, int offH) => utc.AddHours(offH).ToString("HH:mm", Inv);

        sealed class Marca { public string Libro, Tipo; public double P, Bs = double.NaN; public bool Dib; public List<string> Tipos; public string Junto; public bool Cortado, Dentro; }   // 3.7.2: Junto / Cortado / Dentro

        sealed class SesionOut
        {
            public string Nombre; public DateTime Ini, Fin;
            public StringBuilder Csv = new StringBuilder(), Jsonl = new StringBuilder();
            public int Minutos, Velas, VelasM1, VelasM2, MinSinVela, MinNqFresca, MinNqVieja, MinNqSin, MinNdxVig, MinQqqVig, MinNdxL, MinQqqL, MinAlarma;
            public Dictionary<string, int> Anotadas = new Dictionary<string, int>(), Dibujadas = new Dictionary<string, int>(), Borde = new Dictionary<string, int>(), Dentro = new Dictionary<string, int>();
            public List<(DateTime T, string Libro, string Tipo, double P, double Bs, double Cierre, int Linea)> MarcasNdx = new List<(DateTime, string, string, double, double, double, int)>();
            public List<double> BaseNoche = new List<double>(), BaseDia = new List<double>(), RazonNoche = new List<double>(), RazonDia = new List<double>(), Difs = new List<double>();
            public int ConEst2, Igual2, ConEst3, Igual3, ConEstC, IgualC, IgualC5;
            public List<string> Huecos = new List<string>();
            public HashSet<DateTime> NqFresca = new HashSet<DateTime>();
            public string OiSalto = "";
            public double BaseFin = double.NaN, BaseNocheInicio = double.NaN;
            public List<string> Lineas = new List<string>();   // marcas37
            public int MarcasCrudas, MarcasAgrupadas, MarcasCrudasDib, MarcasDibAgr, GruposDib, VelasConRotulos, RotulosTot, CabecerasTot, Cortados, RotulosMax;   // 3.7.1 (A5)
        }

        static void Inc(Dictionary<string, int> d, string k) => d[k] = d.TryGetValue(k, out var v) ? v + 1 : 1;

        // ------------------------------------------------------------------ la corrida

        static void Sesiones(string[] args)
        {
            var t0 = DateTime.UtcNow;
            bool todas = args.Contains("--todas");
            if (todas) SALIDAS = Path.Combine(SALIDAS_BASE, "salidas_todas");
            Directory.CreateDirectory(SALIDAS);
            var ws = LeerWs(out var msgWs);
            Console.WriteLine("  casillas: " + msgWs + (todas ? " | --todas: F1, cruces de NQ / NDX (techo y piso) / QQQ PRENDIDOS para medir cada familia (salidas_todas)" : ""));
            var cs = new Casillas(ws);
            // las casillas que deciden que se anota, que se dibuja y que se rotula (nombre de la propiedad = el del indicador).
            // 3.7.1/3.7.3: las RENOMBRADAS (Raya3NqMasVolB, Raya3NqMenosVolB, Raya3QqqZeroB, Formula3F1b; 3.7.3: Raya3NdxZeroE, Raya3NdxZeroCercoD,
            // Raya3QqqD1c, Raya3QqqD2d, Raya3QqqMasOiC, Raya3QqqMenosOiC, Radio37DibujoPtsC, Juntas373Pts, Tope373Rayas) no estan en el .ws del
            // operador: toman el default 3.7.3, como le va a pasar al indicador al abrir el espacio de trabajo.
            bool nqD1 = cs.B("Raya3NqD1", true), nqD2 = cs.B("Raya3NqD2", true), nqCruce = cs.B("Raya3NqZeroB", false), nqMas = cs.B("Raya3NqMasVolB", false), nqMenos = cs.B("Raya3NqMenosVolB", false);
            bool nqMasOi = cs.B("Raya3NqMasOi", true), nqMenosOi = cs.B("Raya3NqMenosOi", true);
            string verMajors = cs.S("Ver3Majors", "segunPerfil"), perfil = cs.S("Perfil3Visual", "limpio"), verEstela = cs.S("Ver3Estela", "segunPerfil");
            bool majors = verMajors == "si" || (verMajors == "segunPerfil" && perfil != "limpio");   // VerMajorsEf("NQ")
            bool estela = verEstela != "no";
            bool ndxD1 = cs.B("Raya3NdxD1", true), ndxD2 = cs.B("Raya3NdxD2b", true), ndxZero = cs.B("Raya3NdxZeroE", false), ndxCerco = cs.B("Raya3NdxZeroCercoD", false);   // 3.7.3: cruce / techo y piso de NDX APAGADOS (renombradas)
            bool qqqD1 = cs.B("Raya3QqqD1c", true), qqqD2 = cs.B("Raya3QqqD2d", true), qqqZero = cs.B("Raya3QqqZeroB", false), zeroRombos = cs.B("Capa3ZeroRombos", true);   // 3.7.3: QQQ D1/D2 PRENDIDAS (renombradas)
            bool ndxMas = cs.B("Raya3NdxMasOi", true), ndxMenos = cs.B("Raya3NdxMenosOi", true), qqqMas = cs.B("Raya3QqqMasOiC", true), qqqMenos = cs.B("Raya3QqqMenosOiC", true);   // 3.7.3: QQQ M± OI PRENDIDOS (renombradas)
            string modoQqq = cs.S("Capa3QQQ", "propia"), modoNdx = cs.S("Capa3NDX", "propia");
            bool fVer = cs.B("Formulas3Ver", true);
            var fVis = new[] { cs.B("Formula3F1b", false), cs.B("Formula3F2b", false), cs.B("Formula3F3b", false), cs.B("Formula3F4b", false), cs.B("Formula3F5", true), cs.B("Formula3F6b", false), cs.B("Formula3F8b", false) };
            if (todas) { nqCruce = true; ndxZero = true; ndxCerco = !args.Contains("--cerco-no"); qqqZero = true; fVis[0] = true; fVer = true; qqqD1 = qqqD2 = qqqMas = qqqMenos = true; }
            if (todas && args.Contains("--cerco-no")) { SALIDAS = Path.Combine(SALIDAS_BASE, "salidas_todas_cercano"); Directory.CreateDirectory(SALIDAS); }   // NDX: solo el cruce mas cercano (como la 2.0)
            bool Visible(int i) => fVer && fVis[i];
            bool independientes = cs.B("Rayas3Independientes", true);
            double tope = Math.Max(1, Math.Min(7, cs.D("Tope3Rotulos", 5)));   // 3.7.1 (A5): se respeta siempre, despues de agrupar (3.7.3: solo rotulos)
            double radioDib = Math.Max(5, Math.Min(1000, cs.D("Radio37DibujoPtsC", Dibujo37.RADIO_DIBUJO_DEFECTO)));   // 3.7.3: 50 (renombrada; 3.7.2: 35)
            double juntas = Math.Max(0, Math.Min(20, cs.D("Juntas373Pts", Dibujo37.JUNTAS_DEFECTO)));                   // 3.7.3: rayas a <= 8 pts = una (3.7.2: 3)
            int topeVela = (int)Math.Max(1, Math.Min(7, cs.D("Tope373Rayas", Dibujo37.TOPE_RAYAS_DEFECTO)));            // 3.7.3: tope de RAYAS por vela, propio (3.7.2: Tope3Rotulos)
            if (todas) { radioDib = 50; juntas = 0; topeVela = 99; Console.WriteLine("  --todas: marcas CRUDAS (radio 50, sin juntas, sin tope por vela) para emular reglas en Python (eleccion_372.py); los rotulos con los mismos parametros crudos"); }
            double radioDomCs = cs.D("Radio3DominantesPts", 0); double radioDom = radioDomCs > 0 ? radioDomCs : 100;
            string regla = cs.S("Regla3DominantesB", "dosMasGrandes"), baseNdx = cs.S("Capa3BaseNdxB", "sincronizada");
            Console.WriteLine("  efectivas: regla " + regla + ", radio de dibujo " + radioDib.ToString(Inv) + ", juntas " + juntas.ToString(Inv) + " pts, tope por vela " + topeVela + ", radio dominantes " + radioDom.ToString(Inv) + ", majors NQ (rotulos) " + majors + " (M+vol " + nqMas + ", M-vol " + nqMenos + "), NQ cruce " + nqCruce + ", F1 " + Visible(0) + ", F5 " + Visible(4)
                + ", capas QQQ " + modoQqq + " / NDX " + modoNdx + ", NDX D2 " + ndxD2 + ", QQQ D2 " + qqqD2 + ", NDX cruce " + ndxZero + " (cerco " + ndxCerco + "), QQQ cruce " + qqqZero + ", base NDX " + baseNdx + ", independientes " + independientes + ", tope de rotulos " + tope.ToString(Inv));
            if (regla != "dosMasGrandes" || baseNdx != "sincronizada" || modoQqq != "propia" || modoNdx != "propia" || !independientes)
                Console.WriteLine("  OJO: el simulador implementa DosMasGrandes + Sincronizada + capas Propia + independientes; otra casilla no se reproduce.");

            // ---- datos
            var cinta = LeerCintaRellena(new[] { "2026-10-07", "2026-10-08" }, out int rellenados, out string huecosVivo);
            var m2 = VelasM2Export();
            var precios = new Precios(cinta, m2);
            var velasCinta = VelasCinta(cinta);
            var velasS30 = VelasM1DeS30();
            Console.WriteLine("  cinta: vivo + relleno donde el vivo no llega (antes de su primer tick o en sus huecos de > 20 s: " + rellenados + " ticks del relleno; huecos del vivo > 60 s: " + huecosVivo + ")");
            Console.WriteLine("  cinta: " + cinta.T.Length + " ticks (" + DeMs(cinta.T.First()).ToString("MM-dd HH:mm:ss", Inv) + " .. " + DeMs(cinta.T.Last()).ToString("MM-dd HH:mm:ss", Inv) + " UTC) -> " + velasCinta.Count + " velas m1; m2 de la cache de ATAS: " + m2.Count
                + " (" + m2.First().T.ToString("MM-dd HH:mm", Inv) + " .. " + m2.Last().T.ToString("MM-dd HH:mm", Inv) + "); m1 del centinela S30 de la 3.0: " + velasS30.Count);
            // velas del grafico m1: cinta; donde no hay, S30 armadas; donde tampoco, la m2 de la cache (marcada)
            var velasM1 = new SortedDictionary<DateTime, Vela>();
            foreach (var v in velasS30) velasM1[v.T] = v;
            foreach (var v in velasCinta) velasM1[v.T] = v;
            var velas = velasM1.Values.ToList();
            foreach (var v in m2) if (!velasM1.ContainsKey(v.T) && !velasM1.ContainsKey(v.T.AddMinutes(1)) && v.T >= new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc)) velas.Add(v);
            velas = velas.OrderBy(v => v.T).ToList();
            var velaPorCierre = new Dictionary<DateTime, Vela>(); foreach (var v in velas) velaPorCierre[v.Cierre] = v;

            // ---- sesiones
            var ultimoDato = DeMs(cinta.T.Last());
            var fotos = new[] { "2026-10-06", "2026-10-07", "2026-10-08" }.SelectMany(d => LeerViva3(d)).GetEnumerator();
            var primeraFoto = LeerViva3("2026-10-06").FirstOrDefault();
            if (primeraFoto == null) { Check("sesiones: hay viva3 del 10-06", false, "sin viva3-NQ-2026-10-06"); return; }
            var tInicio = new DateTime(primeraFoto.Ts.Year, primeraFoto.Ts.Month, primeraFoto.Ts.Day, primeraFoto.Ts.Hour, primeraFoto.Ts.Minute, 0, DateTimeKind.Utc);
            var sesiones = new List<SesionOut>
            {
                new SesionOut { Nombre = "2026-10-06-tarde", Ini = tInicio, Fin = new DateTime(2026, 10, 6, 21, 0, 0, DateTimeKind.Utc) },
                new SesionOut { Nombre = "2026-10-07", Ini = new DateTime(2026, 10, 6, 22, 0, 0, DateTimeKind.Utc), Fin = new DateTime(2026, 10, 7, 21, 0, 0, DateTimeKind.Utc) },
                new SesionOut { Nombre = "2026-10-08", Ini = new DateTime(2026, 10, 7, 22, 0, 0, DateTimeKind.Utc), Fin = new DateTime(ultimoDato.Year, ultimoDato.Month, ultimoDato.Day, ultimoDato.Hour, ultimoDato.Minute, 0, DateTimeKind.Utc) },
            };
            Console.WriteLine("  sesiones: " + string.Join(" | ", sesiones.Select(s => s.Nombre + " " + s.Ini.ToString("MM-dd HH:mm", Inv) + ".." + s.Fin.ToString("MM-dd HH:mm", Inv) + " UTC")));

            // ---- referencias: lo que DIBUJARON (estelas guardadas)
            var diasRef = new[] { new DateTime(2026, 10, 6), new DateTime(2026, 10, 7), new DateTime(2026, 10, 8) };
            var refC = LeerEstela("PythiaGex", "NQ", diasRef); var ref2 = LeerEstela("PythiaGex2", "NQ", diasRef); var ref3 = LeerEstela("PythiaGex3", "NQ", diasRef);
            var ref3n = LeerEstela("PythiaGex3", "NDX", diasRef); var ref3q = LeerEstela("PythiaGex3", "QQQ", diasRef);
            Console.WriteLine("  estelas de referencia: clasica NQ " + refC.Count + ", 2.0 NQ " + ref2.Count + ", 3.0 NQ " + ref3.Count + " / NDX " + ref3n.Count + " / QQQ " + ref3q.Count);

            // ---- capas: conversion sembrada con 5 dias de archivo antes del inicio (como SembrarDesdeArchivo), despues en vivo
            var venc = Seleccion37.VencimientoTrimestralUtc("MNQZ6", tInicio);
            var pares = new ParesFamilia37();
            var capas = new List<CapaSim>
            {
                new CapaSim { Nombre = "QQQ", Archivo = "QQQ", PorRazon = true, Conv = new ConversionCboe37(true), FechaOi = new FechaOiCboe37(new TimeSpan(5, 15, 0)) },
                new CapaSim { Nombre = "NDX", Archivo = "NQ", PorRazon = false, Conv = new ConversionCboe37(false), FechaOi = new FechaOiCboe37(new TimeSpan(2, 35, 0)) },
            };
            var desdeCboe = tInicio.Date.AddDays(-5);
            foreach (var k in capas) { k.Flujo = LeerCboe(k.Archivo, desdeCboe, sesiones.Last().Fin.Date).GetEnumerator(); k.Avanzar(); }
            void AvanzarCapas(DateTime T)
            {
                while (true)
                {
                    CapaSim sig = null;
                    foreach (var k in capas) if (k.Proxima != null && k.Proxima.Gen <= T && (sig == null || k.Proxima.Gen < sig.Proxima.Gen)) sig = k;
                    if (sig == null) break;
                    var x = sig.Proxima; sig.Leidas++;
                    var tP = x.Ts.AddSeconds(-ConversionCboe37.RETRASO_S);
                    double p = precios.En(tP, out _);
                    // paridad 08-10 (resultados/paridad_3_7.md): la muestra usa cadena.spot_idx, como el vivo (CargarCapa -> Mapear37) y
                    // backtest_familia; con la cabecera 'spot' (4 decimales en NDX) la base difería hasta 0,13 pts de la del vivo
                    if (sig.Conv.Observar(x.Gen, x.Ts, x.SpotIdx, p, CONTRATO))
                    {
                        sig.Nuevas++;
                        sig.FechaOi.Observar(x.Gen, x.OiTot);
                        if (sig.PorRazon) pares.Qqq(x.Gen, tP, x.SpotIdx, sig.Conv.UltimaViva); else pares.Ndx(x.Gen, tP, x.SpotIdx, sig.Conv.UltimaViva);
                    }
                    sig.Ultima = x;
                    sig.Avanzar();
                }
            }
            AvanzarCapas(tInicio);
            foreach (var k in capas) Console.WriteLine("  siembra " + k.Nombre + " hasta " + tInicio.ToString("MM-dd HH:mm", Inv) + "Z: " + k.Leidas + " fotos, " + k.Conv.Resumen() + " -> " + k.Conv.Calcular(tInicio, CONTRATO, venc).Texto);

            // ---- NQ: fotos viva3 y salto de OI
            var salto = new SaltoOi37();
            var alarma37 = new AlarmaFamilia37();   // 3.7.1 (A1): sostenida 10 min, solo log y recuadro
            var deltasBaseRueda = new List<(DateTime T, double D, int N, string Modo)>(); double basePrev = double.NaN; DateTime tBasePrev = DateTime.MinValue;
            int controlesMoi = 0, controlesMoiIguales = 0; double moiMaxDist = 0;
            FotoViva foto = null; bool hayFoto = fotos.MoveNext();
            GammaHoyNucleo.Lectura Lnq = null; double futNq = double.NaN; DateTime lnqUtc = DateTime.MinValue;
            double cierrePrev = double.NaN;
            var saltosVistos = new List<string>();

            // ---- encabezado del CSV
            string[] cols =
            {
                "t_utc","t_ny","t_art","precio","precio_fuente","vela",
                "nq_foto_utc","nq_foto_edad_s","nq_cuenta","nq_oi","nq_oi_motivo","nq_d1","nq_d1_gex_m","nq_d2","nq_d2_gex_m","nq_d_libro","nq_f1_techo","nq_f1_piso","nq_f5_muroC","nq_f5_muroP",
                "nq_m_mas_oi","nq_m_menos_oi","nq_m_mas_vol","nq_m_menos_vol","nq_cruce_apagado","nq_mult",
                "ndx_generado_utc","ndx_ultimo_trade_ny","ndx_edad_dato_min","ndx_congelada","ndx_vigente_hasta_utc","ndx_vigente","ndx_cadena_edad_min","ndx_base","ndx_base_modo","ndx_base_sin_carry","ndx_base_n","ndx_base_texto",
                "ndx_d1","ndx_d2","ndx_cruce_techo","ndx_cruce_piso","ndx_cruce","ndx_m_mas_oi","ndx_m_menos_oi","ndx_oi_fecha",
                "qqq_generado_utc","qqq_ultimo_trade_ny","qqq_edad_dato_min","qqq_congelada","qqq_vigente_hasta_utc","qqq_vigente","qqq_cadena_edad_min","qqq_razon","qqq_razon_modo","qqq_razon_n","qqq_razon_texto",
                "qqq_d1","qqq_d2","qqq_cruce","qqq_m_mas_oi","qqq_m_menos_oi","qqq_oi_fecha",
                "familia_r_par","familia_pares","familia_base_eq_qqq","familia_dif","familia_alarma",
                "ref_clasica_d1","ref_clasica_d2","ref_20_d1","ref_20_d2","ref_30_d1","ref_30_d2","ref_30_ndx_d1","ref_30_ndx_d2","ref_30_qqq_d1","ref_30_qqq_d2",
                "ndx_base_full","qqq_razon_full",   // paridad 08-10: base y razon con todos los decimales (para rehacer la cuenta afuera sin el redondeo)
            };
            foreach (var s in sesiones) s.Csv.Append(string.Join(",", cols)).Append('\n');

            int checks0 = _ok + _falla;
            double nq0710d1 = double.NaN, nq0710d2 = double.NaN;
            int violA3 = 0, minutosA3 = 0, violA6 = 0, minutosA6 = 0;
            // 3.7.2: la regla de la vela (ElegirVela) y la vela viva rotulada
            int v372Velas = 0, v372SobreTope = 0, v372Juntas = 0, v372Dib = 0, v372Abs = 0, v372Cort = 0, v372SinRot = 0, v372DibCierre = 0, v372VivaFiltradas = 0;
            int v372OiSinFecha = 0, v372OiRot = 0, v372EdadSinTexto = 0, v372EdadRot = 0, v372SinRot10 = 0;
            int v373Rotulos = 0, v373LargoMax = 0, v373RotuloConFecha = 0, v373Cab = 0, v373CabLargoMax = 0, v373VelasCab = 0; var v373Largos = new List<int>(); string v373LargoMaxTxt = "";   // 3.7.3
            var v372SinRotTipo = new Dictionary<string, int>();
            var fin = sesiones.Last().Fin;
            for (var T = tInicio.AddMinutes(1); T <= fin; T = T.AddMinutes(1))
            {
                var ses = sesiones.FirstOrDefault(s => T > s.Ini && T <= s.Fin);
                // 1) lo que existia a las T
                while (hayFoto && fotos.Current.Ts <= T)
                {
                    var f = fotos.Current;
                    var mapa = new Dictionary<string, double>();
                    foreach (var x in f.Filas) mapa[x[0].ToString("0.##", Inv) + "|" + f.Ts.AddDays(x[1]).ToString("MM-dd", Inv) + "|" + (x[2] >= 0.5 ? "C" : "P")] = x[3];
                    if (salto.Observar(f.Ts, mapa)) saltosVistos.Add("sesion " + salto.Sesion.ToString("yyyy-MM-dd", Inv) + ": salto de OI de Rithmic a las " + f.Ts.ToString("HH:mm:ss", Inv) + " UTC (" + Seleccion37.ANy(f.Ts).ToString("HH:mm:ss", Inv) + " NY), " + salto.UltimoCambiados + " de " + salto.UltimoComunes + " contratos");
                    foto = f; hayFoto = fotos.MoveNext();
                }
                AvanzarCapas(T);
                // 2) el precio de las T (cierre de la vela que cierra a las T si hay; si no, tick / m2 interpolada; si no, el futuro de la foto)
                velaPorCierre.TryGetValue(T, out var vela);
                string pf; double precio;
                if (vela != null) { precio = vela.C; pf = "cierre " + (vela.Marco == 60 ? "m1" : "m2"); }
                else precio = precios.En(T, out pf);
                if ((double.IsNaN(precio) || precio <= 0) && foto != null && (T - foto.Ts).TotalSeconds <= VIDA_CUENTA_S) { precio = foto.Futuro; pf = "futuro de la foto viva3"; }
                // paridad 08-10 (resultados/paridad_3_7.md): sin tick en 120 s ni foto fresca el grafico igual tiene precio (el ultimo trade:
                // PrecioGrafico del Tick) y el indicador sigue contando las capas con el cada 60 s. Antes quedaba NaN y la capa no se recontaba:
                // el 10-07 de 20:57 a 21:00 UTC el CSV mostraba la cadena de las 20:51:33 cuando la de las 20:56:29 ya estaba.
                if (double.IsNaN(precio) || precio <= 0)
                {
                    var ut = UltimoTick(cinta, T);
                    if (!double.IsNaN(ut.P) && ut.P > 0 && (T - ut.T).TotalHours <= 4) { precio = ut.P; pf = "ultimo tick de hace " + ((int)(T - ut.T).TotalSeconds).ToString(Inv) + " s (precio del grafico)"; }
                }
                // 3) NQ (la cuenta del Tick con la foto de ese minuto)
                bool oiFresco = !salto.OiDeAnteayer(T, out var oiMotivo);
                double edadFoto = foto == null ? double.NaN : (T - foto.Ts).TotalSeconds;
                bool fresca = foto != null && edadFoto <= VIDA_CUENTA_S && precio > 0;
                if (fresca)
                {
                    var cad = CadenaDeViva(foto, 20);
                    var L = cad == null ? null : NucleoNq().Calcular(cad, precio, T);
                    if (L != null && !L.SinBase) { L.Doms = Dibujo37.DominantesNq(L.Perfil, precio, radioDom, oiFresco, out var lib); L.LibroDom = lib; Lnq = L; futNq = precio; lnqUtc = T; }
                    else fresca = false;
                }
                if (T == new DateTime(2026, 10, 8, 7, 11, 0, DateTimeKind.Utc) && fresca) { nq0710d1 = Lnq.Doms.ElementAtOrDefault(0).Fut; nq0710d2 = Lnq.Doms.ElementAtOrDefault(1).Fut; }
                if (fresca && Lnq.Doms.Count == 2) { minutosA3++; if (Math.Abs(Lnq.Doms[0].Gex) < Math.Abs(Lnq.Doms[1].Gex)) violA3++; }   // 3.7.1 (A3)
                double[] fNq = fresca ? Dibujo37.NivelesFormulas(Lnq, precio, precio, radioDom, oiFresco) : null;
                // 4) capas (cada 60 s, como ActualizarCapas) y la alarma de la familia
                foreach (var k in capas) CalcularCapa(k, T, precio, venc);
                foreach (var k in capas)   // 3.7.1 (A6): D1/D2 de la capa = las dos de mayor |GEX vol| a <= min(2 %·F, 100), D1 la mayor
                {
                    if (k.L == null || k.L.Perfil == null || double.IsNaN(precio)) continue;
                    double rad = Seleccion37.RadioDominantes(precio, 100);
                    var top = k.L.Perfil.Where(x => x.Fut > 0 && Math.Abs(x.Fut - precio) <= rad && Math.Abs(x.GexVol) > 0).OrderByDescending(x => Math.Abs(x.GexVol)).Take(2).Select(x => x.Fut).ToList();
                    minutosA6++;
                    if (!k.L.Doms.Select(d => d.Fut).SequenceEqual(top)) violA6++;
                }
                var kq = capas[0]; var kn = capas[1];
                double be = double.NaN, dif = double.NaN; string alarma = "";
                var rp = pares.RPar();
                if (kn.L != null && kq.L != null && !double.IsNaN(kq.Valor) && kn.Spot > 0)
                {
                    be = ParesFamilia37.BaseEquivalente(kn.Spot, kq.Valor, rp.R);
                    if (!double.IsNaN(be) && !double.IsNaN(kn.ValorSinCarry) && kn.ValorSinCarry != 0) dif = kn.ValorSinCarry - be;
                }
                alarma = alarma37.Evaluar(T, dif);   // 3.7.1 (A1): > 2 pts durante 10 min seguidos
                // control A2: la base de NDX minuto a minuto (salto entre minutos con capa)
                if (kn.L != null && !double.IsNaN(kn.Valor)) { if (!double.IsNaN(basePrev) && (T - tBasePrev).TotalMinutes <= 1.01) deltasBaseRueda.Add((T, kn.Valor - basePrev, kn.N, kn.Modo)); basePrev = kn.Valor; tBasePrev = T; }
                // control A4: con ninguna serie por cerrar, M+/- OI de la capa = el strike del perfil del nucleo filtrado al radio
                foreach (var k in capas)
                {
                    if (k.L == null || k.L.Perfil == null || k.Ultima == null || double.IsNaN(precio) || precio <= 0) continue;
                    double rad = Seleccion37.RadioDominantes(precio, 100);
                    if (k.L.MasCerca < 30.0 / 1440.0) continue;
                    var po = k.L.Perfil.Where(x => Math.Abs(x.Fut - precio) <= rad && x.GexOi > 0).OrderByDescending(x => x.GexOi).FirstOrDefault();
                    var no = k.L.Perfil.Where(x => Math.Abs(x.Fut - precio) <= rad && x.GexOi < 0).OrderBy(x => x.GexOi).FirstOrDefault();
                    bool igual = Math.Abs((po?.Fut ?? double.NaN) - k.L.MpOi) < 0.01 == (po != null) && Math.Abs((no?.Fut ?? double.NaN) - k.L.MnOi) < 0.01 == (no != null);
                    controlesMoi++; if (igual) controlesMoiIguales++;
                    foreach (var x in new[] { k.L.MpOi, k.L.MnOi }) if (!double.IsNaN(x) && !double.IsNaN(precio)) moiMaxDist = Math.Max(moiMaxDist, Math.Abs(x - precio));
                }
                if (ses == null) { if (vela != null) cierrePrev = vela.C; continue; }

                // ---------------- fila del minuto
                ses.Minutos++;
                if (vela == null) ses.MinSinVela++;
                if (fresca) { ses.MinNqFresca++; ses.NqFresca.Add(T); } else if (Lnq != null) ses.MinNqVieja++; else ses.MinNqSin++;
                var ny = Seleccion37.ANy(T);
                var row = new List<string>
                {
                    T.ToString("yyyy-MM-dd HH:mm", Inv), ny.ToString("HH:mm", Inv), Hm(T, -3), N2(precio), pf, vela == null ? "" : (vela.Marco == 60 ? "m1" : "m2") + " " + vela.Fuente,
                    foto == null ? "" : foto.Ts.ToString("HH:mm:ss", Inv), N2(edadFoto, "0"), fresca ? "fresca" : (Lnq != null ? "vieja (sin foto en 3 min): sin marcas" : "sin cuenta"), oiFresco ? "ayer" : "anteayer", oiMotivo,
                };
                var dq = fresca ? Lnq.Doms : new List<(double Fut, double Gex)>();
                row.Add(N2(dq.Count > 0 ? dq[0].Fut : double.NaN)); row.Add(N2(dq.Count > 0 ? dq[0].Gex / 1e6 : double.NaN, "0.0"));
                row.Add(N2(dq.Count > 1 ? dq[1].Fut : double.NaN)); row.Add(N2(dq.Count > 1 ? dq[1].Gex / 1e6 : double.NaN, "0.0")); row.Add(fresca ? Lnq.LibroDom : "");
                row.Add(N2(fNq?[0] ?? double.NaN)); row.Add(N2(fNq?[1] ?? double.NaN)); row.Add(N2(fNq?[8] ?? double.NaN)); row.Add(N2(fNq?[9] ?? double.NaN));
                row.Add(N2(fresca && oiFresco ? Lnq.MpOi : double.NaN)); row.Add(N2(fresca && oiFresco ? Lnq.MnOi : double.NaN));
                row.Add(N2(fresca ? Lnq.MpVol : double.NaN)); row.Add(N2(fresca ? Lnq.MnVol : double.NaN)); row.Add(N2(fresca ? Lnq.ZeroVol : double.NaN)); row.Add(fresca ? "20" : "");
                foreach (var k in new[] { kn, kq })
                {
                    bool hay = k.L != null;
                    bool vig = hay && T <= k.Vigente;
                    if (k == kn) { if (vig) ses.MinNdxVig++; if (hay) ses.MinNdxL++; } else { if (vig) ses.MinQqqVig++; if (hay) ses.MinQqqL++; }
                    row.Add(hay ? k.Gen.ToString("MM-dd HH:mm:ss", Inv) : ""); row.Add(hay && k.UltimoUtc != DateTime.MinValue ? Seleccion37.ANy(k.UltimoUtc).ToString("MM-dd HH:mm:ss", Inv) : "");
                    row.Add(hay && k.UltimoUtc != DateTime.MinValue ? N2((T - k.UltimoUtc).TotalMinutes, "0") : ""); row.Add(hay ? (Seleccion37.Congelada(k.UltimoUtc) ? "si" : "no") : "");
                    row.Add(hay ? k.Vigente.ToString("MM-dd HH:mm", Inv) : ""); row.Add(hay ? (vig ? "si" : "no") : ""); row.Add(hay ? N2((T - k.Gen).TotalMinutes, "0") : "");
                    row.Add(N2(k.Valor, k.PorRazon ? "0.0000" : "0.00")); row.Add(k.Modo); if (!k.PorRazon) row.Add(N2(k.ValorSinCarry)); row.Add(k.N.ToString(Inv)); row.Add(k.Texto);
                    var cer = hay && !k.PorRazon ? Seleccion37.Cerco(Seleccion37.CrucesPerfil(k.L.Perfil, true, true).Select(c => c.Z), precio) : (double.NaN, double.NaN);
                    row.Add(N2(hay && k.L.Doms.Count > 0 ? k.L.Doms[0].Fut : double.NaN)); row.Add(N2(hay && k.L.Doms.Count > 1 ? k.L.Doms[1].Fut : double.NaN));
                    if (!k.PorRazon) { row.Add(N2(cer.Item1)); row.Add(N2(cer.Item2)); }
                    row.Add(N2(hay ? k.L.ZeroVol : double.NaN)); row.Add(N2(hay ? k.L.MpOi : double.NaN)); row.Add(N2(hay ? k.L.MnOi : double.NaN)); row.Add(hay ? k.FechaOi.Rotulo(T) : "");
                    // medianas por ventana para el control contra backtest_familia (noche 22:00-13:30 UTC, dia = rueda)
                    if (vig && !double.IsNaN(k.Valor))
                    {
                        bool rueda = !GammaHoyNucleo.FueraDeRueda(T);
                        var hmu = T.TimeOfDay; bool noche = hmu >= new TimeSpan(22, 0, 0) || hmu < new TimeSpan(13, 30, 0);
                        if (k.PorRazon) { if (noche) ses.RazonNoche.Add(k.Valor); else if (rueda) ses.RazonDia.Add(k.Valor); }
                        else { if (noche) { if (ses.BaseNoche.Count == 0) ses.BaseNocheInicio = k.Valor; ses.BaseNoche.Add(k.Valor); } else if (rueda) ses.BaseDia.Add(k.Valor); }
                    }
                }
                row.Add(N2(rp.R, "0.0000")); row.Add(rp.N.ToString(Inv)); row.Add(N2(be)); row.Add(N2(dif)); row.Add(alarma);
                if (!double.IsNaN(dif)) ses.Difs.Add(dif);
                if (alarma != "") ses.MinAlarma++;
                var rc = EstelaEn(refC, T); var r2 = EstelaEn(ref2, T); var r3 = EstelaEn(ref3, T); var r3n = EstelaEn(ref3n, T, 1500); var r3q = EstelaEn(ref3q, T, 1500);
                foreach (var x in new[] { rc.D1, rc.D2, r2.D1, r2.D2, r3.D1, r3.D2, r3n.D1, r3n.D2, r3q.D1, r3q.D2 }) row.Add(N2(x));
                row.Add(N2(kn.Valor, "R")); row.Add(N2(kq.Valor, "R"));
                ses.Csv.Append(string.Join(",", row.Select(Csv))).Append('\n');
                if (fresca)
                {
                    bool Igual(double x1, double x2, double tol = 0.01) { var a = new[] { dq.ElementAtOrDefault(0).Fut, dq.ElementAtOrDefault(1).Fut }.Where(v => v > 0).OrderBy(v => v).ToArray(); var b = new[] { x1, x2 }.Where(v => !double.IsNaN(v) && v > 0).OrderBy(v => v).ToArray(); return a.Length == b.Length && a.Zip(b, (p, q) => Math.Abs(p - q) <= tol).All(v => v); }
                    if (!double.IsNaN(r2.D1)) { ses.ConEst2++; if (Igual(r2.D1, r2.D2)) ses.Igual2++; }
                    if (!double.IsNaN(r3.D1)) { ses.ConEst3++; if (Igual(r3.D1, r3.D2)) ses.Igual3++; }
                    if (!double.IsNaN(rc.D1)) { ses.ConEstC++; if (Igual(rc.D1, rc.D2)) ses.IgualC++; if (Igual(rc.D1, rc.D2, 5.0)) ses.IgualC5++; }
                }

                // ---------------- la vela que cierra a las T: marcas anotadas, dibujadas, y la vista de la vela viva
                if (vela != null)
                {
                    ses.Velas++; if (vela.Marco == 60) ses.VelasM1++; else ses.VelasM2++;
                    double cl = vela.C;
                    var marcas = new List<Marca>();
                    void Anotar(string libro, string tipo, double p, double bs = double.NaN)
                    {
                        if (double.IsNaN(p) || p <= 0) return;
                        var m = new Marca { Libro = libro, Tipo = tipo, P = p, Bs = bs, Dib = estela && Seleccion37.EnRadio(p, cl, radioDib) };
                        marcas.Add(m); Inc(ses.Anotadas, libro + " " + tipo); if (m.Dib) Inc(ses.Dibujadas, libro + " " + tipo);
                    }
                    // NQ: RegistrarVelaCerrada con cuenta fresca (<= 3 min): D1/D2, formulas contra SU cierre, M± OI solo con el OI de ayer (C1)
                    if (fresca)
                    {
                        if (nqD1) Anotar("NQ", "D1", dq.ElementAtOrDefault(0).Fut);
                        if (nqD2) Anotar("NQ", "D2", dq.ElementAtOrDefault(1).Fut);
                        if (nqCruce) Anotar("NQ", "cruce", Lnq.ZeroVol);
                        var fr = Dibujo37.NivelesFormulas(Lnq, precio, cl, radioDom, oiFresco);
                        for (int i = 0; i < Dibujo37.FormulasJuzgar.Length; i++) if (Visible(i)) { Anotar("NQ", Dibujo37.NomFormula[i][0], fr[2 * i]); Anotar("NQ", Dibujo37.NomFormula[i][1], fr[2 * i + 1]); }
                        if (oiFresco) { if (nqMasOi) Anotar("NQ", "M+ OI", Lnq.MpOi); if (nqMenosOi) Anotar("NQ", "M− OI", Lnq.MnOi); }
                    }
                    // capas: RegistrarCapasVela mientras la cadena este VIGENTE (C6)
                    foreach (var k in new[] { kq, kn })
                    {
                        if (k.L == null || T > k.Vigente) continue;
                        if ((k.PorRazon ? modoQqq : modoNdx) != "propia") continue;
                        var e = Dibujo37.RegistroCapa(k.L, cl, k.PorRazon);
                        bool cerco = !k.PorRazon && ndxCerco;
                        foreach (var (i0, _, p) in Dibujo37.PuntosCapa(e, cerco, zeroRombos, k.PorRazon ? qqqD1 : ndxD1, k.PorRazon ? qqqD2 : ndxD2, k.PorRazon ? qqqZero : ndxZero, 0))
                            Anotar(k.Nombre, i0 == 0 ? "D1" : i0 == 1 ? "D2" : i0 == 2 ? "cruce" : i0 == 4 ? "cruce techo" : "cruce piso", p, k.PorRazon ? double.NaN : e[3]);
                        if (k.PorRazon ? qqqMas : ndxMas) Anotar(k.Nombre, "M+ OI", k.L.MpOi, k.PorRazon ? double.NaN : e[3]);
                        if (k.PorRazon ? qqqMenos : ndxMenos) Anotar(k.Nombre, "M− OI", k.L.MnOi, k.PorRazon ? double.NaN : e[3]);
                    }
                    // 3.7.2: LA regla de la vela (Dibujo37.ElegirVela, la misma que PintarMarcasAgrupadas del indicador): una marca por (libro,
                    // strike) con el tipo de mayor jerarquia y la lista de los que agrupa, a <= radio del cierre de ESTA vela, lo que queda a <= 'juntas'
                    // pts de una de mayor jerarquia no se dibuja aparte (Junto), y a lo sumo Tope3Rotulos por vela
                    int crudas = marcas.Count;
                    {
                        var gv = Dibujo37.ElegirVela(marcas.Select((m, i) => new Dibujo37.Marca37(m.Libro, m.Tipo, m.P, i)), cl, radioDib, juntas, topeVela);
                        var agr = new List<Marca>();
                        foreach (var g in gv)
                        {
                            var m0 = marcas[g.Primero.Id];
                            agr.Add(new Marca { Libro = m0.Libro, Tipo = m0.Tipo, P = m0.P, Bs = m0.Bs, Dib = estela && g.Dib, Tipos = g.Miembros.Select(x => x.Tipo).ToList(),
                                                Junto = g.Absorbido ? g.Dueno.Libro + " " + g.Dueno.P.ToString("0.00", Inv) : null, Cortado = g.Cortado, Dentro = g.Dentro });
                        }
                        marcas = agr;
                        ses.MarcasCrudas += crudas; ses.MarcasAgrupadas += marcas.Count; ses.MarcasCrudasDib += 0;
                        foreach (var m in marcas) if (m.Dib) { ses.MarcasDibAgr++; if (m.Tipos.Count > 1) ses.GruposDib++; }
                        // controles: tope y juntas en CADA vela
                        v372Velas++;
                        var dibs = marcas.Where(m => m.Dib).ToList();
                        v372Dib += dibs.Count; v372Abs += marcas.Count(m => m.Junto != null); v372Cort += marcas.Count(m => m.Cortado);
                        if (dibs.Count > topeVela) v372SobreTope++;
                        for (int a = 0; a < dibs.Count; a++) for (int b2 = a + 1; b2 < dibs.Count; b2++) if (juntas > 0 && Math.Abs(dibs[a].P - dibs[b2].P) <= juntas) v372Juntas++;
                    }
                    // la vela viva en su ultimo instante: fut = el precio de la ultima cuenta (_futuro), formulas contra el ultimo cierre
                    double fut = Lnq != null ? (fresca ? precio : futNq) : precio;
                    double ref0 = cierrePrev > 0 ? cierrePrev : fut;
                    var vivas = new List<Marca>();
                    void Viva(string libro, string tipo, double p) { if (!double.IsNaN(p) && p > 0) vivas.Add(new Marca { Libro = libro, Tipo = tipo, P = p, Dib = Seleccion37.EnRadio(p, fut, radioDib) }); }
                    double[] fViva = Lnq != null ? Dibujo37.NivelesFormulas(Lnq, fut, ref0, radioDom, oiFresco) : null;
                    if (fViva != null) for (int i = 0; i < Dibujo37.FormulasJuzgar.Length; i++) if (Visible(i)) { Viva("NQ", Dibujo37.NomFormula[i][0], fViva[2 * i]); Viva("NQ", Dibujo37.NomFormula[i][1], fViva[2 * i + 1]); }
                    if (Lnq != null && oiFresco) { if (nqMasOi) Viva("NQ", "M+ OI", Lnq.MpOi); if (nqMenosOi) Viva("NQ", "M− OI", Lnq.MnOi); }
                    foreach (var k in new[] { kq, kn }) if (k.L != null) { if (k.PorRazon ? qqqMas : ndxMas) Viva(k.Nombre, "M+ OI", k.L.MpOi); if (k.PorRazon ? qqqMenos : ndxMenos) Viva(k.Nombre, "M− OI", k.L.MnOi); }
                    // rotulos: Candidatos (NQ) + capas (QQQ, NDX) + formulas + majors por OI de las capas, en el orden del indicador
                    var cand = Dibujo37.CandidatosNq("NQ", Lnq, fut, nqD1, nqD2, nqCruce, majors, nqMas, nqMenos, oiFresco, nqMasOi, nqMenosOi);
                    foreach (var k in new[] { kq, kn })
                    {
                        if (k.L == null || (k.PorRazon ? modoQqq : modoNdx) != "propia") continue;
                        string edad = k.UltimoUtc == DateTime.MinValue ? "" : " ·" + ((int)(T - k.UltimoUtc).TotalMinutes).ToString("0", Es) + "m";
                        cand.AddRange(Dibujo37.RotulosCapa(k.Nombre, k.L, fut, edad, k.PorRazon ? qqqD1 : ndxD1, k.PorRazon ? qqqD2 : ndxD2, zeroRombos, k.PorRazon ? qqqZero : ndxZero, !k.PorRazon && ndxCerco));   // 3.7.1 (A1): sin alarma
                    }
                    cand.AddRange(Dibujo37.RotulosFormulas(fViva, Visible, "NQ"));
                    foreach (var k in new[] { kq, kn })
                    {
                        if (k.L == null) continue;
                        string edad = k.UltimoUtc == DateTime.MinValue ? "" : " ·" + ((int)(T - k.UltimoUtc).TotalMinutes).ToString("0", Inv) + "m";
                        cand.AddRange(Dibujo37.RotulosMajorOiCapa(k.Nombre, new[] { k.L.MpOi, k.L.MnOi }, edad, k.FechaOi.Rotulo(T), k.PorRazon ? qqqMas : ndxMas, k.PorRazon ? qqqMenos : ndxMenos));
                    }
                    // 3.7.1 (A5): agrupado por (libro, strike), reparto, tope por jerarquia y renglon por capa (Dibujo37.VistaRotulos, la misma del indicador)
                    var infoCapas = new Dictionary<string, (double EdadMin, string FechaOi)>();
                    foreach (var k in new[] { kq, kn }) if (k.L != null) infoCapas[k.Nombre] = (k.UltimoUtc == DateTime.MinValue ? double.NaN : (T - k.UltimoUtc).TotalMinutes, k.FechaOi.Rotulo(T));
                    if (oiFresco) infoCapas["NQ"] = (double.NaN, SaltoOi37.RotuloFechaOi(T));   // 3.7.2: la fecha del OI de NQ (InfoCapasRotulo); 3.7.3: va al renglon de NQ
                    var vista = Dibujo37.VistaRotulos(cand, fut, radioDib, (int)tope, infoCapas, juntas, topeVela);   // 3.7.3: misma regla que la estela (radio, juntas, el menor de los dos topes) y renglones por libro
                    var dentro = vista.Dentro; var borde = vista.Borde;
                    // 3.7.2: la vela viva pinta con la regla de la vela contra el precio de ahora y SOLO lo que tiene rotulo (PintarMarcasAgrupadas)
                    var clavesRot = Dibujo37.ClavesRotuladas(vista);
                    vivas = Dibujo37.ElegirVela(vivas.Select((m, i) => new Dibujo37.Marca37(m.Libro, m.Tipo, m.P, i)), fut, radioDib, juntas, topeVela).Select(g =>
                    {
                        var m0 = vivas[g.Primero.Id];
                        bool d = g.Dib && clavesRot.Contains(g.Clave); if (g.Dib && !d) v372VivaFiltradas++;
                        return new Marca { Libro = m0.Libro, Tipo = m0.Tipo, P = m0.P, Dib = estela && d, Tipos = g.Miembros.Select(x => x.Tipo).ToList(), Junto = g.Absorbido ? g.Dueno.Libro + " " + g.Dueno.P.ToString("0.00", Inv) : null, Cortado = g.Cortado, Dentro = g.Dentro };
                    }).ToList();
                    // control (revisor visual 08-10): rayas dibujadas en la vela que cierra SIN rotulo en la vista de ese instante (ni propio ni 'junto')
                    {
                        var rotuladas = new HashSet<string>(clavesRot);
                        foreach (var gq in dentro) foreach (var j in gq.Junto) rotuladas.Add(j.Clave);
                        foreach (var m in marcas.Where(m => m.Dib))
                        {
                            v372DibCierre++;
                            if (rotuladas.Contains(Dibujo37.Clave(m.Libro, m.P))) continue;
                            v372SinRot++; Inc(v372SinRotTipo, m.Libro + " " + m.Tipo);
                            if (Math.Abs(m.P - cl) <= 10) v372SinRot10++;
                        }
                        // 3.7.3: la fecha del OI y la edad (> 30 min) van en el renglon de SU libro arriba de la columna (vista.Cabeceras), antes de
                        // todos los numeros; los rotulos ya no las llevan. Se controla: todo grupo rotulado (propio o 'junto') de un libro con OI o con
                        // dato viejo tiene su renglon, y ningun rotulo lleva 'hace' ni 'OI dd-MM' (eso alargaba los rotulos a 54 caracteres)
                        string CabDe(string libro) => vista.Cabeceras.Where(c => c.Texto.Split(new[] { " · " }, StringSplitOptions.None).Contains(libro)).Select(c => c.Texto).FirstOrDefault();
                        foreach (var gq0 in dentro.Concat(borde))
                            foreach (var gq in new[] { gq0 }.Concat(gq0.Junto))
                            {
                                string cab = CabDe(gq.Libro);
                                if (gq.TieneOi && infoCapas.TryGetValue(gq.Libro, out var inf0) && !string.IsNullOrEmpty(inf0.FechaOi)) { v372OiRot++; if (cab == null || !cab.Contains(inf0.FechaOi)) v372OiSinFecha++; }
                                if (infoCapas.TryGetValue(gq.Libro, out var inf) && !double.IsNaN(inf.EdadMin) && inf.EdadMin > 30)
                                {
                                    v372EdadRot++;
                                    if (cab == null || !cab.Contains("dato de hace " + Dibujo37.Hace(inf.EdadMin))) v372EdadSinTexto++;
                                }
                            }
                        foreach (var gq in dentro.Concat(borde))
                        {
                            string txt = dentro.Contains(gq) ? Dibujo37.TextoGrupo(gq, TICK, Es) : Dibujo37.TextoBordeGrupo(gq, fut, Es);
                            v373Rotulos++; v373LargoMax = Math.Max(v373LargoMax, txt.Length); v373Largos.Add(txt.Length);
                            if (txt.Length > v373LargoMaxTxt.Length) v373LargoMaxTxt = txt;
                            if (txt.Contains("hace") || System.Text.RegularExpressions.Regex.IsMatch(txt, @"OI \d\d-\d\d")) v373RotuloConFecha++;
                        }
                        foreach (var c in vista.Cabeceras) { v373Cab++; v373CabLargoMax = Math.Max(v373CabLargoMax, c.Texto.Length); }
                        v373VelasCab++;
                    }
                    foreach (var q in dentro) Inc(ses.Dentro, q.Libro + " " + q.Tipos);
                    foreach (var q in borde) Inc(ses.Borde, q.Libro + " " + q.Tipos);
                    ses.VelasConRotulos++; ses.RotulosTot += dentro.Count + borde.Count; ses.CabecerasTot += vista.Cabeceras.Count; ses.Cortados += vista.Cortados;
                    ses.RotulosMax = Math.Max(ses.RotulosMax, dentro.Count + borde.Count);

                    var sb = new StringBuilder();
                    sb.Append("{\"t\":\"").Append(vela.T.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv)).Append("\",\"t_art\":\"").Append(Hm(vela.T, -3)).Append("\",\"t_ny\":\"").Append(Seleccion37.ANy(vela.T).ToString("HH:mm", Inv))
                      .Append("\",\"marco_s\":").Append(vela.Marco).Append(",\"fuente\":").Append(JS(vela.Fuente))
                      .Append(",\"o\":").Append(JN(vela.O)).Append(",\"h\":").Append(JN(vela.H)).Append(",\"l\":").Append(JN(vela.L)).Append(",\"c\":").Append(JN(cl))
                      .Append(",\"nq\":").Append(JS(fresca ? "fresca (foto " + foto.Ts.ToString("HH:mm:ss", Inv) + "Z)" : Lnq != null ? "sin foto en 3 min: sin marcas de NQ" : "sin cuenta")).Append(",\"oi_nq\":").Append(JS(oiFresco ? "ayer" : "anteayer"))
                      .Append(",\"capas\":{\"NDX\":").Append(JS(kn.L == null ? "sin capa" : T <= kn.Vigente ? "vigente" : "vencida (no se anota)")).Append(",\"QQQ\":").Append(JS(kq.L == null ? "sin capa" : T <= kq.Vigente ? "vigente" : "vencida (no se anota)")).Append('}')
                      .Append(",\"marcas\":[");
                    for (int i = 0; i < marcas.Count; i++)
                    {
                        var m = marcas[i];
                        if (i > 0) sb.Append(',');
                        sb.Append("{\"l\":").Append(JS(m.Libro)).Append(",\"tipo\":").Append(JS(m.Tipo));
                        if (m.Tipos != null && m.Tipos.Count > 1) sb.Append(",\"tipos\":[").Append(string.Join(",", m.Tipos.Select(JS))).Append(']');   // 3.7.1 (A5): lo que agrupa
                        sb.Append(",\"p\":").Append(JN(m.P)).Append(",\"d\":").Append(JN(m.P - cl)).Append(",\"dib\":").Append(m.Dib ? "true" : "false");
                        if (m.Junto != null) sb.Append(",\"junto\":").Append(JS(m.Junto));   // 3.7.2: a <= juntas pts de este grupo de mayor jerarquia (no se dibuja aparte)
                        if (m.Cortado) sb.Append(",\"corte\":true");                           // 3.7.2: dentro del radio pero pasado el tope de la vela
                        // 3.7.1 (A2): la base con que se anoto (informativa). La estela de NDX ya NO se corre a la base de ahora: se dibuja donde se anoto.
                        if (!double.IsNaN(m.Bs) && m.Bs > 0) sb.Append(",\"base\":").Append(JN(m.Bs));
                        sb.Append('}');
                    }
                    sb.Append("],\"n_dib\":").Append(marcas.Count(m => m.Dib)).Append(",\"n_fuera\":").Append(marcas.Count(m => !m.Dib))
                      .Append(",\"viva\":{\"fut\":").Append(JN(fut)).Append(",\"ref_formulas\":").Append(JN(ref0)).Append(",\"marcas\":[")
                      .Append(string.Join(",", vivas.Select(m => "{\"l\":" + JS(m.Libro) + ",\"tipo\":" + JS(m.Tipo) + (m.Tipos != null && m.Tipos.Count > 1 ? ",\"tipos\":[" + string.Join(",", m.Tipos.Select(JS)) + "]" : "") + ",\"p\":" + JN(m.P) + ",\"d\":" + JN(m.P - fut) + ",\"dib\":" + (m.Dib ? "true" : "false") + (m.Junto != null ? ",\"junto\":" + JS(m.Junto) : "") + (m.Cortado ? ",\"corte\":true" : "") + "}")))
                      .Append("],\"cabeceras\":[").Append(string.Join(",", vista.Cabeceras.Select(c => "{\"l\":" + JS(c.Libro) + ",\"txt\":" + JS(c.Texto) + "}")))
                      .Append("],\"rotulos\":[").Append(string.Join(",", dentro.Select(q => "{\"l\":" + JS(q.Libro) + ",\"tipo\":" + JS(q.Primero.Tipo) + ",\"tipos\":" + JS(q.Tipos) + ",\"p\":" + JN(q.P) + ",\"rango\":" + q.Rango + (q.Junto.Count > 0 ? ",\"junto\":[" + string.Join(",", q.Junto.Select(j => "{\"l\":" + JS(j.Libro) + ",\"tipos\":" + JS(j.Tipos) + ",\"p\":" + JN(j.P) + "}")) + "]" : "") + ",\"txt\":" + JS(Dibujo37.TextoGrupo(q, TICK, Es)) + "}")))
                      .Append("],\"borde\":[").Append(string.Join(",", borde.Select(q => "{\"l\":" + JS(q.Libro) + ",\"tipo\":" + JS(q.Primero.Tipo) + ",\"tipos\":" + JS(q.Tipos) + ",\"p\":" + JN(q.P) + ",\"rango\":" + q.Rango + ",\"txt\":" + JS(Dibujo37.TextoBordeGrupo(q, fut, Es)) + (q.Siempre ? ",\"siempre\":true" : "") + "}")))
                      .Append("],\"tope\":").Append(((int)tope).ToString(Inv)).Append(",\"cortados\":").Append(vista.Cortados.ToString(Inv)).Append(",\"alarma\":").Append(JS(alarma))
                      .Append("}}");
                    ses.Lineas.Add(sb.ToString());
                    cierrePrev = cl;
                }
                if (kn.L != null && !double.IsNaN(kn.L.Base) && kn.L.Base != 0) ses.BaseFin = kn.L.Base;
            }

            // ---- escribir, con el corrimiento de NDX al final de cada sesion (3.6.6: la estela se corre a la base de AHORA al dibujar)
            Console.WriteLine();
            foreach (var s in saltosVistos) Console.WriteLine("  " + s);
            foreach (var ses in sesiones)
            {
                // 3.7.1 (A2): la estela de NDX ya no se corre a la base de ahora: lo que se escribe es lo que se dibuja (sin corr_fin / dib_fin)
                for (int li = 0; li < ses.Lineas.Count; li++) ses.Jsonl.Append(ses.Lineas[li]).Append('\n');
                var rCsv = Path.Combine(SALIDAS, "niveles37-" + ses.Nombre + ".csv");
                var rLargo = Path.Combine(SALIDAS, "marcas37-" + ses.Nombre + ".csv");
                var largo = new StringBuilder("t_utc,t_art,t_ny,marco_s,o,h,l,c,que,libro,tipo,tipos,p,d,dib,base,txt\n");
                foreach (var linea in ses.Jsonl.ToString().Split('\n'))
                {
                    if (linea.Length < 10) continue;
                    using var doc = JsonDocument.Parse(linea);
                    var e = doc.RootElement;
                    string V(JsonElement x, string n) => x.TryGetProperty(n, out var y) ? (y.ValueKind == JsonValueKind.String ? y.GetString() : y.ValueKind == JsonValueKind.Number ? y.GetDouble().ToString("0.##", Inv) : y.ValueKind == JsonValueKind.True ? "si" : y.ValueKind == JsonValueKind.False ? "no" : "") : "";
                    string cab = string.Join(",", new[] { V(e, "t").Replace("T", " ").Replace("Z", ""), V(e, "t_art"), V(e, "t_ny"), V(e, "marco_s"), V(e, "o"), V(e, "h"), V(e, "l"), V(e, "c") }.Select(Csv));
                    void Filas(JsonElement arr, string que)
                    {
                        foreach (var m in arr.EnumerateArray())
                            largo.Append(cab).Append(',').Append(string.Join(",", new[] { que, V(m, "l"), V(m, "tipo"), m.TryGetProperty("tipos", out var tps) ? (tps.ValueKind == JsonValueKind.Array ? string.Join("·", tps.EnumerateArray().Select(z => z.GetString())) : tps.GetString()) : "", V(m, "p"), V(m, "d"), V(m, "dib"), V(m, "base"), V(m, "txt") }.Select(Csv))).Append('\n');
                    }
                    Filas(e.GetProperty("marcas"), "marca al cerrar");
                    var vv = e.GetProperty("viva");
                    Filas(vv.GetProperty("marcas"), "vela viva: marca");
                    Filas(vv.GetProperty("rotulos"), "vela viva: rotulo");
                    Filas(vv.GetProperty("borde"), "vela viva: borde");
                    Filas(vv.GetProperty("cabeceras"), "vela viva: renglon de capa");
                }
                File.WriteAllText(rLargo, largo.ToString(), new UTF8Encoding(false));
                var rJs = Path.Combine(SALIDAS, "marcas37-" + ses.Nombre + ".jsonl");
                File.WriteAllText(rCsv, ses.Csv.ToString(), new UTF8Encoding(false));
                File.WriteAllText(rJs, ses.Jsonl.ToString(), new UTF8Encoding(false));
                // huecos: minutos sin vela, sin cuenta fresca de NQ (rachas de >= 3 min)
                string Rachas(Func<DateTime, bool> falta)
                {
                    var r = new List<string>(); DateTime? a = null; DateTime ult = ses.Ini;
                    for (var t = ses.Ini.AddMinutes(1); t <= ses.Fin; t = t.AddMinutes(1))
                    {
                        if (falta(t)) { if (a == null) a = t; ult = t; }
                        else if (a != null) { if ((ult - a.Value).TotalMinutes >= 2) r.Add(Hm(a.Value.AddMinutes(-1), 0) + "-" + Hm(ult, 0)); a = null; }
                    }
                    if (a != null && (ult - a.Value).TotalMinutes >= 2) r.Add(Hm(a.Value.AddMinutes(-1), 0) + "-" + Hm(ult, 0));
                    return r.Count == 0 ? "ninguno" : string.Join(", ", r) + " UTC";
                }
                var huecoVela = Rachas(t => !velaPorCierre.ContainsKey(t) && !velaPorCierre.ContainsKey(t.AddMinutes(1)));
                var huecoNq = Rachas(t => !ses.NqFresca.Contains(t));
                double medBn = Seleccion37.Mediana(ses.BaseNoche), medBd = Seleccion37.Mediana(ses.BaseDia), medRn = Seleccion37.Mediana(ses.RazonNoche), medRd = Seleccion37.Mediana(ses.RazonDia);
                string Dic(Dictionary<string, int> d) => "{" + string.Join(",", d.OrderByDescending(kv => kv.Value).Select(kv => JS(kv.Key) + ":" + kv.Value)) + "}";
                var res = new StringBuilder();
                res.Append("{\n \"sesion\":").Append(JS(ses.Nombre)).Append(",\n \"ventana_utc\":").Append(JS(ses.Ini.ToString("yyyy-MM-dd HH:mm", Inv) + " .. " + ses.Fin.ToString("yyyy-MM-dd HH:mm", Inv)))
                   .Append(",\n \"generado_utc\":").Append(JS(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", Inv)))
                   .Append(",\n \"archivos\":{\"niveles\":").Append(JS(rCsv)).Append(",\"marcas\":").Append(JS(rJs)).Append(",\"marcas_csv\":").Append(JS(rLargo)).Append('}')
                   .Append(",\n \"minutos\":").Append(ses.Minutos).Append(",\n \"velas\":{\"total\":").Append(ses.Velas).Append(",\"m1\":").Append(ses.VelasM1).Append(",\"m2_sin_m1\":").Append(ses.VelasM2).Append(",\"huecos_sin_vela\":").Append(JS(huecoVela)).Append('}')
                   .Append(",\n \"nq\":{\"min_cuenta_fresca\":").Append(ses.MinNqFresca).Append(",\"min_cuenta_vieja\":").Append(ses.MinNqVieja).Append(",\"min_sin_cuenta\":").Append(ses.MinNqSin)
                   .Append(",\"sin_cuenta_fresca\":").Append(JS(huecoNq)).Append(",\"salto_oi\":").Append(JS(string.Join(" | ", saltosVistos.Where(x => x.Contains(ses.Nombre.Substring(0, 10)))))).Append('}')
                   .Append(",\n \"capas\":{\"NDX_min_con_capa\":").Append(ses.MinNdxL).Append(",\"NDX_min_vigente\":").Append(ses.MinNdxVig).Append(",\"QQQ_min_con_capa\":").Append(ses.MinQqqL).Append(",\"QQQ_min_vigente\":").Append(ses.MinQqqVig)
                   .Append(",\"base_ndx_mediana_noche_22a1330utc\":").Append(JN(medBn)).Append(",\"base_ndx_mediana_rueda\":").Append(JN(medBd))
                   .Append(",\"razon_qqq_mediana_noche\":").Append(JN(medRn, "0.0000")).Append(",\"razon_qqq_mediana_rueda\":").Append(JN(medRd, "0.0000"))
                   .Append(",\"base_ndx_al_final\":").Append(JN(ses.BaseFin)).Append('}')
                   .Append(",\n \"familia\":{\"min_con_dif\":").Append(ses.Difs.Count).Append(",\"dif_mediana\":").Append(JN(Seleccion37.Mediana(ses.Difs))).Append(",\"dif_max_abs\":").Append(JN(ses.Difs.Count > 0 ? ses.Difs.Max(Math.Abs) : double.NaN)).Append(",\"min_con_alarma\":").Append(ses.MinAlarma).Append('}')
                   .Append(",\n \"d1d2_nq_vs_dibujado\":{\"2.0\":").Append(JS(ses.Igual2 + " de " + ses.ConEst2)).Append(",\"3.0_instalada\":").Append(JS(ses.Igual3 + " de " + ses.ConEst3)).Append(",\"clasica\":").Append(JS(ses.IgualC + " de " + ses.ConEstC + " exactos; " + ses.IgualC5 + " a <= 5 pts (la clasica dibuja el centroide de strikes vecinos, no el strike)"))
                   .Append(",\"nota\":\"mismos strikes (+-0,01) en los minutos con cuenta fresca y estela de esa version a <= 5 min; cada version tiene su propio libro (la 2.0 y la clasica por otra via)\"}")
                   .Append(",\n \"marcas_anotadas\":").Append(Dic(ses.Anotadas)).Append(",\n \"marcas_dibujadas_radio\":").Append(Dic(ses.Dibujadas))
                   .Append(",\n \"rotulos_adentro\":").Append(Dic(ses.Dentro)).Append(",\n \"rotulos_borde\":").Append(Dic(ses.Borde))
                   .Append(",\n \"agrupado_371\":{\"marcas_anotadas_crudas\":").Append(ses.MarcasCrudas).Append(",\"marcas_agrupadas\":").Append(ses.MarcasAgrupadas).Append(",\"marcas_dibujadas_agrupadas\":").Append(ses.MarcasDibAgr)
                   .Append(",\"de_ellas_con_mas_de_un_tipo\":").Append(ses.GruposDib).Append(",\"rotulos_por_vela\":").Append(JN(ses.VelasConRotulos > 0 ? (double)ses.RotulosTot / ses.VelasConRotulos : double.NaN))
                   .Append(",\"rotulos_max\":").Append(ses.RotulosMax).Append(",\"renglones_de_capa_por_vela\":").Append(JN(ses.VelasConRotulos > 0 ? (double)ses.CabecerasTot / ses.VelasConRotulos : double.NaN))
                   .Append(",\"cortados_por_el_tope_por_vela\":").Append(JN(ses.VelasConRotulos > 0 ? (double)ses.Cortados / ses.VelasConRotulos : double.NaN)).Append(",\"tope\":").Append(((int)tope).ToString(Inv)).Append('}')
                   .Append(",\n \"regla_372\":{\"radio\":").Append(JN(radioDib)).Append(",\"juntas\":").Append(JN(juntas)).Append(",\"tope_por_vela\":").Append(topeVela).Append('}')
                   .Append(",\n \"casillas\":{").Append(string.Join(",", cs.Origen.Select(kv => JS(kv.Key) + ":" + JS(kv.Value)))).Append('}')
                   .Append(",\n \"supuestos\":[")
                   .Append(JS("NQ: la cuenta del minuto T usa la ultima foto viva3 (ts <= T, una por minuto) con el precio de las T; el vivo recalcula cada segundo con la cadena viva (el volumen de opciones dentro del minuto no esta)")).Append(',')
                   .Append(JS("NQ: cuenta 'fresca' = foto de <= 3 min (cuentaFresca del indicador); sin foto fresca no se anota la vela y los rotulos quedan con la ultima cuenta")).Append(',')
                   .Append(JS("capas: la ultima linea archivada con generado <= T se vuelve a contar cada minuto con el precio de ahora (el vivo: archivo local de <= 20 min o la nube; MEDIDO en el log de la 3.0: con la cadena de QQQ de 285 min de la nube siguio contando minuto a minuto; SUPUESTO: la nube respondio siempre); spot = cadena.spot_idx, como el vivo (CargarCapa), la siembra (ArchivoCboe37) y backtest_familia (libros.fotos_cboe); la cabecera 'spot' de NDX tiene 4 decimales y movia la base hasta 0,13 pts (paridad_3_7.md)")).Append(',')
                   .Append(JS("cinta = vivo + relleno en los huecos del vivo de > 20 s (el vivo del 08-10 no tiene 07:08-07:21 UTC); precio para la conversion sincronizada: tick de la cinta a <= 120 s; si no, vela m2 de la cache de ATAS interpolada (como backtest_familia; el vivo usa su anillo de ticks de 2 h o la vela del grafico)")).Append(',')
                   .Append(JS("vela viva: se muestra su ultimo instante (fut = cierre si la cuenta es fresca); las formulas de la vela viva van contra el cierre ANTERIOR (UltimoCierre del indicador)")).Append(',')
                   .Append(JS("NDX (3.7.1, A2): cada marca guarda 'base' (la base con que se anoto, informativa) y se DIBUJA donde se anoto: ya no se corre a la base de ahora (3.6.6), asi el dibujo es lo que juzga la auditoria. Base de dia = mediana robusta ACUMULADA de la rueda en curso")).Append(',')
                   .Append(JS("QQQ (3.7.1, A1): la razon solo con muestras en hora (09:35-15:59 NY); alarma de familia solo si pasa de 2 pts 10 min seguidos y solo en log / recuadro (columna familia_alarma), no en los rotulos")).Append(',')
                   .Append(JS("marcas (3.7.3): la regla de la vela Dibujo37.ElegirVela: UNA por (libro, strike) ('tipo' = el de mayor jerarquia, 'tipos' = todos), dib solo a <= " + radioDib.ToString(Inv) + " pts del cierre de esa vela, lo que queda a <= " + juntas.ToString(Inv) + " pts de una de mayor jerarquia no se dibuja ('junto'), a lo sumo " + topeVela + " por vela ('corte', Tope373Rayas); rotulos con la misma regla contra el precio de ahora (el menor de los dos topes), flechas del borde solo en los lugares que sobran del tope de rotulos " + ((int)tope).ToString(Inv) + ", la edad (> 30 min) y la fecha del OI en un renglon por libro ARRIBA de la columna ('cabeceras')")).Append(',')
                   .Append(JS("memoria al instalar (3.7.2/3.7.3): la estela guardada sin 'v' >= 3.7.2 (la de la 3.6.9) NO se siembra; NQ se rebobina de viva3 con las reglas nuevas; las capas quedan sin historia hasta que vuelvan a anotar. Lo de aca es la 3.7.3 'pura', corriendo desde el principio")).Append(',')
                   .Append(JS("10-06 tarde: no hay cinta; velas m1 solo de 20:10 a 20:58 UTC (centinela S30 de la 3.0, dos mitades); el resto m2 de la cache de ATAS marcadas 'marco_s':120"))
                   .Append("]\n}\n");
                File.WriteAllText(Path.Combine(SALIDAS, "resumen37-" + ses.Nombre + ".json"), res.ToString(), new UTF8Encoding(false));
                Console.WriteLine("  " + ses.Nombre + ": " + ses.Minutos + " min, " + ses.Velas + " velas (" + ses.VelasM1 + " m1, " + ses.VelasM2 + " m2); NQ fresca " + ses.MinNqFresca + " / vieja " + ses.MinNqVieja + " / sin " + ses.MinNqSin
                    + "; NDX vigente " + ses.MinNdxVig + ", QQQ vigente " + ses.MinQqqVig + "; base NDX noche " + N2(medBn) + " / rueda " + N2(medBd) + ", razon QQQ noche " + N2(medRn, "0.0000") + " / rueda " + N2(medRd, "0.0000")
                    + "; alarma " + ses.MinAlarma + " min (dif mediana " + N2(Seleccion37.Mediana(ses.Difs)) + ", max |" + N2(ses.Difs.Count > 0 ? ses.Difs.Max(Math.Abs) : double.NaN) + "|); D1/D2 = 2.0 en " + ses.Igual2 + "/" + ses.ConEst2 + ", = 3.0 en " + ses.Igual3 + "/" + ses.ConEst3 + ", = clasica en " + ses.IgualC + "/" + ses.ConEstC + " (a <= 5 pts: " + ses.IgualC5 + ")");
                Console.WriteLine("     marcas dibujadas/anotadas: " + string.Join(", ", ses.Anotadas.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + (ses.Dibujadas.TryGetValue(kv.Key, out var dd) ? dd : 0) + "/" + kv.Value)));
                Console.WriteLine("     borde (veces): " + string.Join(", ", ses.Borde.OrderByDescending(kv => kv.Value).Take(12).Select(kv => kv.Key + " " + kv.Value)) + "; huecos sin vela: " + huecoVela + "; NQ sin cuenta fresca: " + huecoNq);
                Console.WriteLine("     -> " + rCsv + " (" + new FileInfo(rCsv).Length / 1024 + " KB), " + rJs + " (" + new FileInfo(rJs).Length / 1024 + " KB), " + Path.GetFileName(rLargo) + " (" + new FileInfo(rLargo).Length / 1024 + " KB)");
            }
            Console.WriteLine("  precio: " + precios.PorTick + " por tick, " + precios.PorVela + " por m2 interpolada, " + precios.Sin + " sin precio; " + (DateTime.UtcNow - t0).TotalSeconds.ToString("0", Inv) + " s");

            // ---- controles contra lo medido (backtest_familia.md, Prueba4 / preview 07:10)
            var s07 = sesiones[1]; var s08 = sesiones[2];
            Check("sesiones: D1/D2 de NQ a las 07:10Z del 08-10 = 31.200 / 31.250 (preview 2.0, Prueba4)", new[] { nq0710d1, nq0710d2 }.OrderBy(x => x).SequenceEqual(new[] { 31200.0, 31250.0 }), N2(nq0710d1) + " / " + N2(nq0710d2));
            Check("sesiones: base NDX de noche 10-07 = 246,73 (backtest_familia, mediana 22:00-13:30 UTC)", Math.Abs(Seleccion37.Mediana(s07.BaseNoche) - 246.73) <= 0.10, N2(Seleccion37.Mediana(s07.BaseNoche)) + " en " + s07.BaseNoche.Count + " min");
            // 3.7.1 (A2): la base de dia es la mediana ACUMULADA de la rueda: la de la 3.7.0 (ultimas 24) daba 244,05 de mediana en la rueda del
            // 10-07 (backtest_familia); la nueva converge a la de la rueda entera (244,31 con 301 muestras, Prueba6). Controles nuevos: no salta.
            {
                var rueda07 = deltasBaseRueda.Where(d => { var ny = Seleccion37.ANy(d.T); return ny.Date == new DateTime(2026, 10, 7) && d.Modo == "dia" && d.N >= ConversionCboe37.MIN_RUEDA && ny.TimeOfDay < new TimeSpan(16, 0, 0); }).ToList();
                var arranque07 = deltasBaseRueda.Where(d => { var ny = Seleccion37.ANy(d.T); return ny.Date == new DateTime(2026, 10, 7) && ny.TimeOfDay >= new TimeSpan(9, 30, 0) && (d.Modo != "dia" || d.N < ConversionCboe37.MIN_RUEDA) && ny.TimeOfDay < new TimeSpan(12, 0, 0); }).ToList();
                Console.WriteLine("  A2: arranque de la rueda 10-07 (de la base de la noche a la de dia, menos de " + ConversionCboe37.MIN_RUEDA + " muestras): " + arranque07.Count(d => Math.Abs(d.D) > 0.01) + " cambios, el mayor " + N2(arranque07.Count > 0 ? arranque07.Max(d => Math.Abs(d.D)) : double.NaN) + " pts, suma " + N2(arranque07.Sum(d => d.D)) + " pts (la base converge desde la de la noche a la de la rueda)");
                var cierre07 = deltasBaseRueda.Where(d => { var ny = Seleccion37.ANy(d.T); return ny.Date == new DateTime(2026, 10, 7) && ny.TimeOfDay >= new TimeSpan(16, 0, 0) && ny.TimeOfDay <= new TimeSpan(16, 30, 0); }).ToList();
                double maxR = rueda07.Count > 0 ? rueda07.Max(d => Math.Abs(d.D)) : double.NaN, maxC = cierre07.Count > 0 ? cierre07.Max(d => Math.Abs(d.D)) : double.NaN;
                double rangoR = s07.BaseDia.Count > 0 ? s07.BaseDia.Max() - s07.BaseDia.Min() : double.NaN;
                Console.WriteLine("  A2: base NDX de rueda 10-07 (acumulada): mediana " + N2(Seleccion37.Mediana(s07.BaseDia)) + " en " + s07.BaseDia.Count + " min, de " + N2(s07.BaseDia.FirstOrDefault()) + " a " + N2(s07.BaseDia.LastOrDefault()) + " (rango " + N2(rangoR) + "; la 3.7.0 con las ultimas 24: mediana 244,05, se movia ~6 pts)");
                Check("sesiones A2: base de dia acumulada 10-07 con >= " + ConversionCboe37.MIN_RUEDA + " muestras: salto minuto a minuto <= 0,25 pts", rueda07.Count > 0 && maxR <= 0.25, "max |salto| " + N2(maxR) + " en " + rueda07.Count + " minutos");
                Check("sesiones A2: sin salto al pasar a la noche (16:00-16:30 NY del 10-07) <= 0,1 pts", cierre07.Count > 0 && maxC <= 0.1, "max |salto| " + N2(maxC) + " en " + cierre07.Count + " minutos");
            }
            Check("sesiones: base NDX al empezar la noche del 10-08 (22:01Z) = 244,03 (Prueba6: 'de 244,03 a 242,10'; la mediana 243,07 es sobre 817 min y esta sesion lleva " + s08.BaseNoche.Count + ")", Math.Abs(s08.BaseNocheInicio - 244.03) <= 0.03, N2(s08.BaseNocheInicio) + "; mediana de lo que va de la noche " + N2(Seleccion37.Mediana(s08.BaseNoche)));
            // 3.7.1 (A1): la razon de QQQ solo con muestras en hora: de noche queda QUIETA en la ultima de la rueda (la 3.7.0 con el after-hours:
            // 41,4420 la noche del 10-07 contra 41,4342 de rueda, backtest_familia)
            {
                double rmin07 = s07.RazonNoche.Count > 0 ? s07.RazonNoche.Min() : double.NaN, rmax07 = s07.RazonNoche.Count > 0 ? s07.RazonNoche.Max() : double.NaN;
                double rmin08 = s08.RazonNoche.Count > 0 ? s08.RazonNoche.Min() : double.NaN, rmax08 = s08.RazonNoche.Count > 0 ? s08.RazonNoche.Max() : double.NaN;
                Console.WriteLine("  A1: razon QQQ noche 10-07 " + N2(Seleccion37.Mediana(s07.RazonNoche), "0.0000") + " (" + N2(rmin07, "0.0000") + ".." + N2(rmax07, "0.0000") + "), rueda 10-07 " + N2(Seleccion37.Mediana(s07.RazonDia), "0.0000") + ", noche 10-08 " + N2(Seleccion37.Mediana(s08.RazonNoche), "0.0000") + " (" + N2(rmin08, "0.0000") + ".." + N2(rmax08, "0.0000") + "); 3.7.0: 41,4420 / 41,4342 / 41,4342");
                Check("sesiones A1: razon QQQ de la noche del 10-08 quieta (sin muestras fuera de hora: max - min < 0,0001)", s08.RazonNoche.Count > 0 && rmax08 - rmin08 < 0.0001, N2(rmin08, "0.0000") + ".." + N2(rmax08, "0.0000"));
                Check("sesiones A1: alarma de familia (sostenida 10 min) 0 min en 10-07 y 10-08 (la 3.7.0: 731 min el 10-07)", s07.MinAlarma == 0 && s08.MinAlarma == 0, "10-07 " + s07.MinAlarma + " min (dif mediana " + N2(Seleccion37.Mediana(s07.Difs)) + ", max |" + N2(s07.Difs.Count > 0 ? s07.Difs.Max(Math.Abs) : double.NaN) + "|), 10-08 " + s08.MinAlarma + " min (dif mediana " + N2(Seleccion37.Mediana(s08.Difs)) + ", max |" + N2(s08.Difs.Count > 0 ? s08.Difs.Max(Math.Abs) : double.NaN) + "|)");
            }
            Check("sesiones A3: NQ D1 = la de mayor |GEX| en todos los minutos con cuenta fresca", violA3 == 0 && minutosA3 > 0, violA3 + " minutos con |D1| < |D2| de " + minutosA3);
            Check("sesiones A6: capas D1 = la de mayor |GEX vol| y D1/D2 = las dos mas grandes a <= min(2 %·F, 100)", violA6 == 0 && minutosA6 > 0, violA6 + " de " + minutosA6 + " minutos-capa fuera de la regla");
            Check("sesiones A4: M+/- OI de capas = strike del perfil filtrado al radio (sin serie por cerrar) y nunca a mas de 100 pts", controlesMoi > 0 && controlesMoiIguales == controlesMoi && moiMaxDist <= 100.0001, controlesMoiIguales + " de " + controlesMoi + " iguales; distancia maxima al precio " + N2(moiMaxDist) + " pts (la 3.7.0: hasta 1.500)");
            Check("sesiones: saltos de OI 10-07 01:24:00 y 10-08 01:30:26 UTC (backtest_familia)", saltosVistos.Any(x => x.Contains("2026-10-07") && x.Contains("01:24:00")) && saltosVistos.Any(x => x.Contains("2026-10-08") && x.Contains("01:30:26")), string.Join(" | ", saltosVistos));
            {
                // C1 con el agrupado (3.7.1): un M+/- OI de NQ puede ir dentro de un grupo cuyo tipo principal es D1: se mira 'tipos' tambien
                int malas = 0, malasRot = 0, gruposMalArmados = 0, rotSobreTope = 0;
                foreach (var l in sesiones.SelectMany(x => x.Lineas))
                {
                    using var doc = JsonDocument.Parse(l); var e = doc.RootElement;
                    bool ante = e.GetProperty("oi_nq").GetString() == "anteayer";
                    IEnumerable<string> Tipos(JsonElement m) => m.TryGetProperty("tipos", out var t) && t.ValueKind == JsonValueKind.Array ? t.EnumerateArray().Select(z => z.GetString()) : new[] { m.GetProperty("tipo").GetString() };
                    var claves = new HashSet<string>();
                    foreach (var m in e.GetProperty("marcas").EnumerateArray())
                    {
                        if (ante && m.GetProperty("l").GetString() == "NQ" && Tipos(m).Any(t => t == "M+ OI" || t == "M− OI")) malas++;
                        if (!claves.Add(Dibujo37.Clave(m.GetProperty("l").GetString(), m.GetProperty("p").GetDouble()))) gruposMalArmados++;
                    }
                    var vv = e.GetProperty("viva");
                    foreach (var arr in new[] { vv.GetProperty("marcas"), vv.GetProperty("rotulos"), vv.GetProperty("borde") })
                        foreach (var m in arr.EnumerateArray())
                            if (ante && m.GetProperty("l").GetString() == "NQ" && (m.GetProperty("tipo").GetString().Contains("OI") || (m.TryGetProperty("tipos", out var ts) && (ts.ValueKind == JsonValueKind.String ? ts.GetString().Contains("OI") : ts.EnumerateArray().Any(z => z.GetString().Contains("OI")))))) malasRot++;
                    if (vv.GetProperty("rotulos").GetArrayLength() + vv.GetProperty("borde").GetArrayLength() > (int)tope) rotSobreTope++;
                }
                Check("sesiones: ninguna marca ni rotulo de NQ M+/- OI con el OI de anteayer (C1, mirando tambien lo agrupado)", malas == 0 && malasRot == 0, "marcas " + malas + ", vela viva " + malasRot);
                Check("sesiones A5: una sola marca por (libro, strike) en cada vela", gruposMalArmados == 0, gruposMalArmados + " repetidas");
                Check("sesiones A5: rotulos (adentro + borde) <= tope " + ((int)tope).ToString(Inv) + " en todas las velas", rotSobreTope == 0, rotSobreTope + " velas por encima");
            }
            // 3.7.2: la regla de la vela y la vela viva rotulada
            Console.WriteLine("  3.7.2: " + v372Velas + " velas; grupos dibujados " + v372Dib + " (" + N2(v372Velas > 0 ? (double)v372Dib / v372Velas : double.NaN) + " por vela), juntos " + v372Abs + ", cortados por el tope " + v372Cort
                + "; vela viva: " + v372VivaFiltradas + " marcas vivas sin rotulo NO pintadas");
            Console.WriteLine("  3.7.2: rayas de la vela que cierra sin rotulo en la vista de ese instante: " + v372SinRot + " de " + v372DibCierre + " (" + N2(v372DibCierre > 0 ? 100.0 * v372SinRot / v372DibCierre : double.NaN, "0.0") + " %; a <= 10 pts del cierre: " + v372SinRot10 + "; 3.7.1: 26-30 %) por tipo: "
                + string.Join(", ", v372SinRotTipo.OrderByDescending(kv => kv.Value).Take(8).Select(kv => kv.Key + " " + kv.Value)));
            Check("sesiones 3.7.3: a lo sumo " + topeVela + " grupos dibujados en cada vela (Tope373Rayas)", v372SobreTope == 0 && v372Velas > 0, v372SobreTope + " velas por encima de " + v372Velas);
            Check("sesiones 3.7.3: ninguna pareja de rayas dibujadas de la misma vela a <= " + juntas.ToString(Inv) + " pts (con 8: ninguna 'raya doble' de 3-8 pts del revisor visual)", juntas <= 0 || v372Juntas == 0, v372Juntas + " parejas");
            Check("sesiones 3.7.2: rayas de la vela que cierra sin rotulo <= 5 % (3.7.1: 26-30 %, revisor visual)", todas || (v372DibCierre > 0 && v372SinRot <= 0.05 * v372DibCierre), v372SinRot + " de " + v372DibCierre);
            Check("sesiones 3.7.3: todo M± OI rotulado (NQ incluido, tambien los 'junto') tiene la fecha de su OI en el renglon de su libro, arriba de los numeros", v372OiSinFecha == 0 && v372OiRot > 0, v372OiSinFecha + " sin renglon con la fecha de " + v372OiRot);
            Check("sesiones 3.7.3: todo nivel rotulado de una capa con dato de mas de 30 min tiene 'dato de hace ...' en el renglon de su libro, arriba de los numeros", v372EdadSinTexto == 0 && v372EdadRot > 0, v372EdadSinTexto + " sin renglon de " + v372EdadRot);
            Check("sesiones 3.7.3: ningun rotulo lleva la edad ni la fecha del OI (van en el renglon de su libro)", v373RotuloConFecha == 0 && v373Rotulos > 0, v373RotuloConFecha + " de " + v373Rotulos);
            {
                var ls = v373Largos.OrderBy(x => x).ToList();
                int p95 = ls.Count > 0 ? ls[(int)Math.Floor(0.95 * (ls.Count - 1))] : 0, med = ls.Count > 0 ? ls[ls.Count / 2] : 0;
                Console.WriteLine("  3.7.3: largo de los rotulos (caracteres): mediana " + med + ", p95 " + p95 + ", maximo " + v373LargoMax + " ('" + v373LargoMaxTxt + "'); renglones de libro " + N2(v373VelasCab > 0 ? (double)v373Cab / v373VelasCab : double.NaN) + " por vela, el mas largo " + v373CabLargoMax + " caracteres (3.7.2: rotulos de hasta 54)");
                Check("sesiones 3.7.3: el rotulo mas largo es mas corto que el de la 3.7.2 (54 caracteres, revisor visual)", v373LargoMax > 0 && v373LargoMax < 54, "maximo " + v373LargoMax + ", p95 " + p95);
            }
        }
    }
}
