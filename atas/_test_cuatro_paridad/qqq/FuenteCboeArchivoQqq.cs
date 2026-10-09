// FuenteCboeArchivoQqq.cs — DOBLE DE PRUEBA de IFuenteCboe para el arnes de QQQ (B3c). Solo lectura de los archivos YA guardados de la
// clasica/2.0/cboe-local (los mismos que lee la referencia); no baja nada.
// Reproduce backtest_familia.cargar_cboe(tk, dias_cal) + libros.fotos_cboe(tk, d):
//   * dias_cal = los 6 dias de calendario previos + el dia; por cada d se leen los archivos de [d-1, d] de PythiaGex/cadenas,
//     PythiaGex2/cadenas, PythiaGex/cboe-local (cadena-<T>-<d>.jsonl.gz) y PythiaGex/cadenas/local-<T>-<d>.jsonl, en orden de ruta;
//   * entra una linea si 'generado' (cabecera, primeros 500 caracteres; '+' -> '+') esta en [d-2h, d+21h];
//   * dedup por cadena_ts: gana la bajada MAS TEMPRANA (en empate, la primera leida); orden final por generado;
//   * t_dato = ultimo_trade (hora NY) -> UTC, o ts - retraso_s (902) si falta; spot = cadena.spot_idx (o la cabecera 'spot');
//   * filas = las de 8+ columnas tal cual; dias = vencimientos[].dias; oi_total = suma de oi_call + oi_put de todas las filas.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using PythiaGexCuatro.Familia;

namespace ParidadQqq
{
    public sealed class FuenteCboeArchivoQqq : IFuenteCboe
    {
        private static readonly Regex RxTs = new Regex("\"cadena_ts\":\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex RxGen = new Regex("\"generado\":\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly TimeZoneInfo Ny = BuscarNy();
        private static TimeZoneInfo BuscarNy() { foreach (var id in new[] { "Eastern Standard Time", "America/New_York" }) try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { } return TimeZoneInfo.Utc; }

        private readonly string _libro;
        private readonly List<FotoCadena> _fotos;
        public int Lineas, Leidas;
        public string Resumen = "";

        /// <param name="ticker">nombre del archivo ("QQQ"; "NQ" = NDX)</param>
        public FuenteCboeArchivoQqq(string appAtas, string libro, string ticker, string dia, DateTime? hastaUtc = null, int diasCboe = 6)
        {
            _libro = libro;
            var inv = CultureInfo.InvariantCulture;
            var d1 = DateTime.ParseExact(dia, "yyyy-MM-dd", inv);
            var carpetas = new[] { Path.Combine(appAtas, "PythiaGex", "cadenas"), Path.Combine(appAtas, "PythiaGex2", "cadenas"), Path.Combine(appAtas, "PythiaGex", "cboe-local") };
            var cacheArchivo = new Dictionary<string, List<(string Ts, DateTime Gen, string Linea)>>();
            var vistos = new Dictionary<string, (FotoCadena F, int Orden)>();
            int orden = 0;
            for (int k = diasCboe; k >= 0; k--)
            {
                var d = d1.AddDays(-k);
                var ini = DateTime.SpecifyKind(d.AddHours(-2), DateTimeKind.Utc); var fin = DateTime.SpecifyKind(d.AddHours(21), DateTimeKind.Utc);
                var archivos = new List<string>();
                foreach (var dd in new[] { d.AddDays(-1), d })
                {
                    string ds = dd.ToString("yyyy-MM-dd", inv);
                    foreach (var c in carpetas) { var p = Path.Combine(c, "cadena-" + ticker + "-" + ds + ".jsonl.gz"); if (File.Exists(p)) archivos.Add(p); }
                    var pl = Path.Combine(appAtas, "PythiaGex", "cadenas", "local-" + ticker + "-" + ds + ".jsonl"); if (File.Exists(pl)) archivos.Add(pl);
                }
                archivos.Sort(StringComparer.Ordinal);
                // fotos_cboe(tk, d): dedup dentro del dia (gana la mas temprana), despues cargar_cboe dedup entre dias
                var delDia = new Dictionary<string, FotoCadena>();
                var ordenDia = new List<string>();
                foreach (var p in archivos)
                {
                    if (!cacheArchivo.TryGetValue(p, out var lineas)) { lineas = LeerCabeceras(p); cacheArchivo[p] = lineas; }
                    foreach (var x in lineas)
                    {
                        if (x.Gen < ini || x.Gen > fin) continue;
                        if (delDia.TryGetValue(x.Ts, out var ya) && ya.GeneradoUtc <= x.Gen) continue;
                        var f = Armar(libro, x.Ts, x.Gen, x.Linea);     // json invalido o sin filas: no registra el ts (como el Python)
                        if (f == null) continue;
                        if (!delDia.ContainsKey(x.Ts)) ordenDia.Add(x.Ts);
                        delDia[x.Ts] = f;
                    }
                }
                foreach (var ts in ordenDia.OrderBy(t => delDia[t].GeneradoUtc))
                {
                    var f = delDia[ts];
                    if (!vistos.TryGetValue(ts, out var v)) vistos[ts] = (f, orden++);
                    else if (v.F.GeneradoUtc > f.GeneradoUtc) vistos[ts] = (f, v.Orden);
                }
            }
            var lista = new List<FotoCadena>();
            foreach (var kv in vistos.OrderBy(kv => kv.Value.F.GeneradoUtc).ThenBy(kv => kv.Value.Orden))
            {
                if (hastaUtc != null && kv.Value.F.GeneradoUtc > hastaUtc.Value) continue;
                lista.Add(kv.Value.F);
            }
            _fotos = lista;
            Resumen = $"{libro} ({ticker}) {dia}: {lista.Count} fotos de {Lineas} lineas ({vistos.Count} ts distintos)";
        }

        private List<(string Ts, DateTime Gen, string Linea)> LeerCabeceras(string p)
        {
            var r = new List<(string, DateTime, string)>();
            try
            {
                using var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                Stream s = p.EndsWith(".gz", StringComparison.Ordinal) ? new GZipStream(fs, CompressionMode.Decompress) : (Stream)fs;
                using var sr = new StreamReader(s);
                string l;
                try
                {
                    while ((l = sr.ReadLine()) != null)
                    {
                        Lineas++;
                        var cab = l.Length > 500 ? l.Substring(0, 500) : l;
                        var m = RxTs.Match(cab); var g = RxGen.Match(cab);
                        if (!m.Success || !g.Success) continue;
                        if (!DateTimeOffset.TryParse(g.Groups[1].Value.Replace("\\u002B", "+"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var gen)) continue;
                        r.Add((m.Groups[1].Value, DateTime.SpecifyKind(gen.UtcDateTime, DateTimeKind.Utc), l));
                    }
                }
                catch (InvalidDataException) { }
                catch (EndOfStreamException) { }
            }
            catch (IOException) { }
            return r;
        }

        private FotoCadena Armar(string libro, string ts, DateTime gen, string linea)
        {
            var inv = CultureInfo.InvariantCulture;
            try
            {
                using var doc = JsonDocument.Parse(linea);
                Leidas++;
                var j = doc.RootElement;
                if (!j.TryGetProperty("cadena", out var c) || c.ValueKind != JsonValueKind.Object) return null;
                var filas = new List<FilaCadena>();
                if (c.TryGetProperty("filas", out var fs) && fs.ValueKind == JsonValueKind.Array)
                    foreach (var x in fs.EnumerateArray())
                    {
                        if (x.ValueKind != JsonValueKind.Array || x.GetArrayLength() < 8) continue;
                        double N(int i) { var e = x[i]; return e.ValueKind == JsonValueKind.Number ? e.GetDouble() : double.NaN; }
                        var fila = new FilaCadena(N(0), (int)N(1), N(2), N(3), N(4), N(5), N(6), N(7));
                        filas.Add(fila);
                    }
                if (filas.Count == 0) return null;
                // oi_total = Fl[:,2].sum() + Fl[:,3].sum() (dos sumas por separado, como numpy)
                double sC = 0, sP = 0; foreach (var f in filas) { sC += f.OiC; } foreach (var f in filas) { sP += f.OiP; }
                var tsUtc = DateTime.SpecifyKind(DateTime.ParseExact(ts, "yyyy-MM-dd HH:mm:ss", inv), DateTimeKind.Utc);
                DateTime dato;
                string ut = c.TryGetProperty("ultimo_trade", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
                int retraso = 902;
                if (j.TryGetProperty("retraso_s", out var rs) && rs.ValueKind == JsonValueKind.Number && (int)rs.GetDouble() != 0) retraso = (int)rs.GetDouble();
                if (!string.IsNullOrEmpty(ut) && DateTime.TryParse(ut, inv, DateTimeStyles.None, out var utNy))
                    dato = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(utNy, DateTimeKind.Unspecified), Ny), DateTimeKind.Utc);
                else dato = tsUtc.AddSeconds(-retraso);
                double spot = 0;
                if (c.TryGetProperty("spot_idx", out var si) && si.ValueKind == JsonValueKind.Number && si.GetDouble() != 0) spot = si.GetDouble();
                else if (j.TryGetProperty("spot", out var sp) && sp.ValueKind == JsonValueKind.Number) spot = sp.GetDouble();
                var dias = new List<double>();
                if (c.TryGetProperty("vencimientos", out var vs) && vs.ValueKind == JsonValueKind.Array)
                    foreach (var v in vs.EnumerateArray()) dias.Add(v.TryGetProperty("dias", out var dv) && dv.ValueKind == JsonValueKind.Number ? dv.GetDouble() : 0.0);
                return new FotoCadena
                {
                    Libro = libro, GeneradoUtc = gen, TsUtc = tsUtc, DatoUtc = dato, Spot = spot, Dias = dias.ToArray(), Filas = filas.ToArray(),
                    EsFuturo = false, OiTotal = sC + sP,
                };
            }
            catch (JsonException) { return null; }
        }

        // ---------------------------------------------------------------- IFuenteCboe
        public void Arrancar() { }
        public void Parar() { }
        public IReadOnlyList<FotoCadena> Fotos(string libro, DateTime desdeUtc, DateTime hastaUtc)
        {
            if (libro != _libro) return Array.Empty<FotoCadena>();
            return _fotos.Where(f => f.GeneradoUtc >= desdeUtc && f.GeneradoUtc < hastaUtc).ToList();
        }
        public FotoCadena Ultima(string libro) => libro == _libro && _fotos.Count > 0 ? _fotos[_fotos.Count - 1] : null;
        public long Version => _fotos.Count;
        public string Estado => Resumen;
    }
}
