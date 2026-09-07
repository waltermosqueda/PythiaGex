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
    internal static class Feed
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
    }
}
