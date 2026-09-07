using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace PythiaGex
{
    /// <summary>
    /// EL FEED, COMO BIBLIOTECA.
    ///
    /// Gamma Vivo tenia la descarga y el parseo del feed adentro de la clase del
    /// indicador. Gamma Hoy (2026-09-07) nace aparte y no quiere depender de
    /// Gamma Vivo, asi que la cañeria del dato vive aca, sin dibujo ni logica:
    /// baja el JSON que publica radar.py y lo deja en filas de strike.
    ///
    /// Formato del archivo (radar.py):
    ///   { "base": .., "base_confiable": .., "base_cruda": .., "base_error_ticks": ..,
    ///     "base_ultima_buena": .., "base_ultima_buena_edad_min": .., "edad_min": ..,
    ///     "cadena": { "ts": .., "spot_idx": .., "ultimo_trade": .., "horizonte_dias": ..,
    ///                 "vencimientos": [{"dias": ..}, ...],
    ///                 "filas": [[strike, venc, oi_call, oi_put, iv_call, iv_put, vol_call, vol_put], ...] } }
    /// </summary>
    public static class Feed
    {
        public sealed class Fila
        {
            public double K;        // strike en puntos del INDICE
            public int V;           // indice del vencimiento
            public double OiC, OiP; // interes abierto (de AYER, para todos)
            public double IvC, IvP; // volatilidad implicita
            public double VolC, VolP; // contratos operados HOY (CBOE, 902 s tarde)
        }

        public sealed class Cadena
        {
            public string Ts = "";
            public double SpotIdx;
            public double[] Dias = Array.Empty<double>();
            public List<Fila> Filas = new();
            public double Base, BaseCruda, BaseErrorTicks, BaseUltimaBuena, BaseUltimaBuenaEdad;
            public bool BaseConfiable;
            public double EdadMin;
            public string UltimoTrade = "";
            public double HorizonteCadena = double.NaN;
            public DateTime RecibidoUtc;
            /// <summary>Cuando la nube publico este archivo (UTC). Es la hora
            /// a la que el indicador HUBIERA tenido esta cadena: en el
            /// rebobinado manda esto, no el sello de CBOE.</summary>
            public DateTime GeneradoUtc;
        }

        private static readonly HttpClient Http = Crear();
        private static HttpClient Crear()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            c.DefaultRequestHeaders.Add("User-Agent", "PythiaGex-GammaHoy/0.1");
            return c;
        }

        /// <summary>Baja <raiz>_radar.json de la url base. Devuelve null si fallo;
        /// el motivo queda en <paramref name="error"/> via el callback.</summary>
        public static async Task<Cadena> Bajar(string url, string raiz, Action<string> error)
        {
            try
            {
                var b = (url ?? "").Trim();
                if (!b.EndsWith("/")) b += "/";
                var txt = await Http.GetStringAsync(
                    b + raiz + "_radar.json?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    .ConfigureAwait(false);
                var c = Parsear(txt);
                if (c == null || c.Filas.Count == 0) { error?.Invoke("la cadena vino vacia"); return null; }
                try
                {
                    var dst = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "ATAS", "pythiagex-gammahoy-cadena-" + raiz + ".json");
                    File.WriteAllText(dst, txt);
                }
                catch { }
                try { Archivo.GuardarLocal(raiz, txt, c); } catch { }
                return c;
            }
            catch (Exception e) { error?.Invoke(e.Message.Length > 80 ? e.Message.Substring(0, 80) : e.Message); return null; }
        }

        private static double? Num(JsonElement e, string k)
            => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number
               && v.TryGetDouble(out var d) ? d : (double?)null;

        private static string Txt(JsonElement e, string k)
            => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";

        public static Cadena Parsear(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                if (!r.TryGetProperty("cadena", out var cd) || cd.ValueKind != JsonValueKind.Object) return null;
                var c = new Cadena
                {
                    Ts = Txt(cd, "ts"),
                    SpotIdx = Num(cd, "spot_idx") ?? 0,
                    Base = Num(r, "base") ?? 0,
                    BaseConfiable = r.TryGetProperty("base_confiable", out var bc) && bc.ValueKind == JsonValueKind.True,
                    BaseCruda = Num(r, "base_cruda") ?? 0,
                    BaseErrorTicks = Num(r, "base_error_ticks") ?? 0,
                    BaseUltimaBuena = Num(r, "base_ultima_buena") ?? 0,
                    BaseUltimaBuenaEdad = Num(r, "base_ultima_buena_edad_min") ?? 0,
                    EdadMin = Num(r, "edad_min") ?? 0,
                    UltimoTrade = Txt(cd, "ultimo_trade"),
                    HorizonteCadena = Num(cd, "horizonte_dias") ?? double.NaN,
                    RecibidoUtc = DateTime.UtcNow,
                };
                var gen = Txt(r, "generado");
                if (!string.IsNullOrEmpty(gen) && DateTimeOffset.TryParse(gen, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal, out var go))
                    c.GeneradoUtc = go.UtcDateTime;
                if (cd.TryGetProperty("vencimientos", out var vs) && vs.ValueKind == JsonValueKind.Array)
                    c.Dias = vs.EnumerateArray().Select(x => Num(x, "dias") ?? 0).ToArray();
                if (cd.TryGetProperty("filas", out var fs) && fs.ValueKind == JsonValueKind.Array)
                    foreach (var f in fs.EnumerateArray())
                    {
                        if (f.ValueKind != JsonValueKind.Array) continue;
                        var a = f.EnumerateArray().ToArray();
                        if (a.Length < 8) continue;
                        double G(int i) => a[i].TryGetDouble(out var d) ? d : 0;
                        c.Filas.Add(new Fila { K = G(0), V = (int)G(1), OiC = G(2), OiP = G(3), IvC = G(4), IvP = G(5), VolC = G(6), VolP = G(7) });
                    }
                return c;
            }
            catch { return null; }
        }

        /// <summary>
        /// EL ARCHIVO DE CADENAS, PARA REBOBINAR.
        ///
        /// Un archivo por dia UTC y por raiz: cadena-ES-2026-09-07.jsonl.gz, una
        /// linea (un miembro gzip) por corrida, con "generado" = cuando se
        /// publico. La nube lo arma con archivar_cadena.py; la maquina local
        /// agrega lo que baja mientras ATAS esta abierto, en texto plano.
        /// </summary>
        internal static class Archivo
        {
            public static string Carpeta => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex", "cadenas");

            private static readonly Dictionary<string, string> _ultimoSello = new();

            private static string Sello(Cadena c)
                => c.Ts + "|" + c.Base.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

            /// <summary>Agrega la cadena recien bajada al archivo local del dia,
            /// solo si el sello de CBOE cambio (de noche se congela).</summary>
            public static void GuardarLocal(string raiz, string json, Cadena c)
            {
                if (c == null || c.Filas.Count == 0) return;
                var sello = Sello(c);
                lock (_ultimoSello)
                {
                    if (_ultimoSello.TryGetValue(raiz, out var u) && u == sello) return;
                    _ultimoSello[raiz] = sello;
                }
                var gen = c.GeneradoUtc != default ? c.GeneradoUtc : DateTime.UtcNow;
                Directory.CreateDirectory(Carpeta);
                var linea = Flaca(json, gen);
                if (linea == null) return;
                File.AppendAllText(Path.Combine(Carpeta, "local-" + raiz + "-" + gen.ToString("yyyy-MM-dd") + ".jsonl"), linea + "\n");
            }

            /// <summary>Lo minimo que Parsear necesita, en una linea. Misma forma
            /// que archivar_cadena.py, para que el lector sea uno solo.</summary>
            private static string Flaca(string json, DateTime gen)
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var r = doc.RootElement;
                    using var ms = new MemoryStream();
                    using (var w = new Utf8JsonWriter(ms))
                    {
                        w.WriteStartObject();
                        w.WriteString("generado", gen.ToString("yyyy-MM-dd'T'HH:mm:ss'+00:00'"));
                        foreach (var k in new[] { "cadena_ts", "edad_min", "retraso_s", "spot", "base", "base_confiable", "base_cruda", "base_error_ticks", "base_ultima_buena", "base_ultima_buena_edad_min", "contrato" })
                            if (r.TryGetProperty(k, out var v)) { w.WritePropertyName(k); v.WriteTo(w); }
                        if (r.TryGetProperty("cadena", out var cd)) { w.WritePropertyName("cadena"); cd.WriteTo(w); }
                        w.WriteEndObject();
                    }
                    return System.Text.Encoding.UTF8.GetString(ms.ToArray());
                }
                catch { return null; }
            }

            /// <summary>Lee un archivo por dia (gz de la nube o jsonl local) y
            /// devuelve las cadenas ordenadas por hora de publicacion.</summary>
            public static List<Cadena> Leer(string ruta)
            {
                var salida = new List<Cadena>();
                if (!File.Exists(ruta)) return salida;
                using var fs = File.OpenRead(ruta);
                Stream s = ruta.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                    ? new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress)
                    : fs;
                using (s)
                using (var sr = new StreamReader(s, System.Text.Encoding.UTF8))
                {
                    string l;
                    while ((l = sr.ReadLine()) != null)
                    {
                        if (l.Length < 10) continue;
                        var c = Parsear(l);
                        if (c != null && c.Filas.Count > 0 && c.GeneradoUtc != default) salida.Add(c);
                    }
                }
                salida.Sort((a, b) => a.GeneradoUtc.CompareTo(b.GeneradoUtc));
                return salida;
            }

            /// <summary>Todas las cadenas de una raiz entre dos dias (UTC),
            /// juntando lo de la nube y lo local, sin repetir sellos.</summary>
            public static List<Cadena> Cargar(string raiz, DateTime desdeUtc, DateTime hastaUtc, Action<string> log)
            {
                var todo = new List<Cadena>();
                var vistos = new HashSet<string>();
                int archivos = 0;
                for (var d = desdeUtc.Date; d <= hastaUtc.Date; d = d.AddDays(1))
                {
                    var dia = d.ToString("yyyy-MM-dd");
                    foreach (var nombre in new[] { "cadena-" + raiz + "-" + dia + ".jsonl.gz", "local-" + raiz + "-" + dia + ".jsonl" })
                    {
                        var p = Path.Combine(Carpeta, nombre);
                        if (!File.Exists(p)) continue;
                        List<Cadena> ls;
                        try { ls = Leer(p); } catch (Exception e) { log?.Invoke("no pude leer " + nombre + ": " + e.Message); continue; }
                        archivos++;
                        foreach (var c in ls)
                            if (vistos.Add(Sello(c))) todo.Add(c);
                    }
                }
                todo.Sort((a, b) => a.GeneradoUtc.CompareTo(b.GeneradoUtc));
                log?.Invoke("archivo: " + archivos + " archivos, " + todo.Count + " cadenas distintas de " + desdeUtc.ToString("yyyy-MM-dd") + " a " + hastaUtc.ToString("yyyy-MM-dd"));
                return todo;
            }

            /// <summary>Baja de la nube el archivo del dia si no esta, o si es de hoy
            /// y ya tiene mas de 15 minutos. Devuelve true si hay archivo.</summary>
            public static async Task<bool> BajarDia(string url, string raiz, DateTime diaUtc, Action<string> log)
            {
                var dia = diaUtc.ToString("yyyy-MM-dd");
                var nombre = "cadena-" + raiz + "-" + dia + ".jsonl.gz";
                var p = Path.Combine(Carpeta, nombre);
                bool hoy = diaUtc.Date == DateTime.UtcNow.Date;
                if (File.Exists(p) && (!hoy || (DateTime.UtcNow - File.GetLastWriteTimeUtc(p)).TotalMinutes < 15)) return true;
                try
                {
                    var b = (url ?? "").Trim();
                    if (!b.EndsWith("/")) b += "/";
                    var bytes = await Http.GetByteArrayAsync(b + "cadenas/" + nombre + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
                    if (bytes == null || bytes.Length < 20) return File.Exists(p);
                    Directory.CreateDirectory(Carpeta);
                    File.WriteAllBytes(p, bytes);
                    log?.Invoke("bajado " + nombre + " (" + bytes.Length + " bytes)");
                    return true;
                }
                catch (Exception e)
                {
                    if (!(e is HttpRequestException)) log?.Invoke("no pude bajar " + nombre + ": " + e.Message);
                    return File.Exists(p);
                }
            }
        }
    }
}
