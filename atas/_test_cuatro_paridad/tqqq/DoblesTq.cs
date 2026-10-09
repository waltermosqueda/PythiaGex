// DoblesTq.cs — arnes del modulo TQQQ (B4), 08-10-2026. Dobles de ICinta y de IFuenteCboe que leen los MISMOS archivos que tqqq_vivo.py:
//   cinta: profundidad/estado/cinta/cinta-NQ-<dia>.csv + -relleno.csv (tq_comun.ticks: relleno solo con t < el primer t del vivo; orden
//          estable por t). PrecioSoloTick = tq_comun.precio_en (ultimo tick <= ts, searchsorted right - 1).
//   cboe:  %APPDATA%/ATAS/PythiaGex/cboe-local/cadena-<TQQQ|NQ>-<dia>.jsonl.gz (libros.fotos_cboe: sin filas no hay foto; dedup por
//          cadena_ts, gana la bajada mas temprana; t_dato = ultimo_trade NY -> UTC o ts - 902 s; spot = spot_idx o spot).
// Los dos aceptan un "reloj" para simular el vivo (solo se ve lo que existia a esa hora).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using PythiaGexCuatro.Familia;

namespace ArnesTqqq
{
    public sealed class CintaCsvTq : ICinta
    {
        private long[] _t = Array.Empty<long>(); private double[] _p = Array.Empty<double>(); private int _n;
        public long RelojMs = long.MaxValue;
        public int N => _n;

        public static CintaCsvTq Leer(string carpeta, params string[] dias)
        {
            var ts = new List<long>(4_000_000); var ps = new List<double>(4_000_000);
            foreach (var dia in dias)
            {
                var viv = Path.Combine(carpeta, "cinta-NQ-" + dia + ".csv");
                var rel = Path.Combine(carpeta, "cinta-NQ-" + dia + "-relleno.csv");
                var vt = new List<long>(); var vp = new List<double>();
                if (File.Exists(viv)) LeerCsv(viv, vt, vp);
                long minV = long.MaxValue; foreach (var x in vt) if (x < minV) minV = x;
                if (File.Exists(rel))
                {
                    var rt = new List<long>(); var rp = new List<double>();
                    LeerCsv(rel, rt, rp);
                    for (int i = 0; i < rt.Count; i++) if (vt.Count == 0 || rt[i] < minV) { ts.Add(rt[i]); ps.Add(rp[i]); }
                }
                ts.AddRange(vt); ps.AddRange(vp);
            }
            // orden estable por t (pandas sort_values kind="stable")
            int n = ts.Count;
            var idx = new int[n]; for (int i = 0; i < n; i++) idx[i] = i;
            var tArr = ts.ToArray();
            Array.Sort(idx, (a, b) => { int c = tArr[a].CompareTo(tArr[b]); return c != 0 ? c : a.CompareTo(b); });
            var c2 = new CintaCsvTq { _t = new long[n], _p = new double[n], _n = n };
            for (int i = 0; i < n; i++) { c2._t[i] = tArr[idx[i]]; c2._p[i] = ps[idx[i]]; }
            return c2;
        }

        private static void LeerCsv(string ruta, List<long> t, List<double> p)
        {
            using var sr = new StreamReader(ruta, Encoding.UTF8, false, 1 << 20);
            string l = sr.ReadLine();   // cabecera t,precio,...
            while ((l = sr.ReadLine()) != null)
            {
                int c1 = l.IndexOf(','); if (c1 <= 0) continue;
                int c2 = l.IndexOf(',', c1 + 1); if (c2 < 0) c2 = l.Length;
                if (!long.TryParse(l.AsSpan(0, c1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tt)) continue;
                if (!double.TryParse(l.AsSpan(c1 + 1, c2 - c1 - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var pp) || double.IsNaN(pp)) continue;
                t.Add(tt); p.Add(pp);
            }
        }

        private int Hasta()
        {
            if (RelojMs == long.MaxValue) return _n;
            int lo = 0, hi = _n; while (lo < hi) { int m = (lo + hi) >> 1; if (_t[m] <= RelojMs) lo = m + 1; else hi = m; }
            return lo;
        }

        private int UltimoMenorIgual(long ms, int hasta)
        {
            int lo = 0, hi = hasta; while (lo < hi) { int m = (lo + hi) >> 1; if (_t[m] <= ms) lo = m + 1; else hi = m; }
            return lo - 1;
        }

        public DateTime UltimoTickUtc { get { int h = Hasta(); return h > 0 ? TqqqHora.DeMs(_t[h - 1]) : default; } }

        public double PrecioSoloTick(DateTime tUtc, int maxEdadS = 120)
        {
            long ms = TqqqHora.Ms(tUtc); int h = Hasta();
            int j = UltimoMenorIgual(ms, h);
            if (j < 0 || ms - _t[j] > maxEdadS * 1000L) return double.NaN;
            return _p[j];
        }

        // ---- velas m2 (preview_niveles.velas_de_ticks) para Precio y CierreConocido (respaldos; el modulo TQQQ casi no los usa)
        private (long B, double O, double H, double L, double C)? VelaDe(long b, int hasta)
        {
            int i = UltimoMenorIgual(b - 1, hasta) + 1; if (i >= hasta || _t[i] >= b + 120000) return null;
            double o = _p[i], hi = o, lo = o, c = o;
            for (; i < hasta && _t[i] < b + 120000; i++) { var p = _p[i]; if (p > hi) hi = p; if (p < lo) lo = p; c = p; }
            return (b, o, hi, lo, c);
        }

        public double CierreConocido(DateTime tUtc)
        {
            int h = Hasta(); if (h == 0) return double.NaN;
            long ms = TqqqHora.Ms(tUtc), ult = _t[h - 1];
            long b = (Math.Min(ms, ult) / 120000L) * 120000L - 120000L;   // ultima vela con b + 120 s <= t y cerrada (b + 120 s <= ultimo tick)
            for (int k = 0; k < 240 && b >= 0; k++, b -= 120000L)
            {
                if (b + 120000 > ms || b + 120000 > ult) continue;
                var v = VelaDe(b, h);
                if (v != null) return ms - (b + 120000) > 4 * 3600_000L ? double.NaN : v.Value.C;
            }
            return double.NaN;
        }

        public double Precio(DateTime tUtc)
        {
            int h = Hasta(); if (h == 0) return double.NaN;
            long ms = TqqqHora.Ms(tUtc);
            int j = UltimoMenorIgual(ms, h);
            if (j >= 0 && ms - _t[j] <= 120000 && ms <= _t[h - 1]) return _p[j];
            long b = (ms / 120000L) * 120000L; var v = VelaDe(b, h);
            return v == null ? double.NaN : v.Value.O + (v.Value.C - v.Value.O) * (ms - b) / 120000.0;
        }

        public IReadOnlyList<(long Ms, double O, double H, double L, double C)> VelasM2(DateTime desdeUtc, DateTime hastaUtc)
        {
            var l = new List<(long, double, double, double, double)>(); int h = Hasta(); if (h == 0) return l;
            long ult = _t[h - 1];
            for (long b = TqqqHora.Ms(desdeUtc) / 120000L * 120000L; b < TqqqHora.Ms(hastaUtc) && b + 120000 <= ult; b += 120000L)
            { var v = VelaDe(b, h); if (v != null) l.Add(v.Value); }
            return l;
        }
    }

    public sealed class CboeLocalTq : IFuenteCboe
    {
        private readonly Dictionary<string, List<FotoCadena>> _l = new Dictionary<string, List<FotoCadena>>(StringComparer.Ordinal);
        public DateTime Reloj = DateTime.MaxValue;
        private static readonly TimeZoneInfo Ny = TqqqHora.Ny;

        /// <summary>libro: "TQQQ" | "NDX"; archivo: "TQQQ" | "NQ" (cboe-local llama NQ a la cadena de _NDX). conFilas=false: fotos livianas.</summary>
        public void Cargar(string carpeta, string libro, string archivo, IEnumerable<string> dias, bool conFilas)
        {
            var vistos = new Dictionary<string, FotoCadena>(StringComparer.Ordinal);
            var orden = new List<string>();
            foreach (var dia in dias)
            {
                var p = Path.Combine(carpeta, "cadena-" + archivo + "-" + dia + ".jsonl.gz");
                if (!File.Exists(p)) { Console.WriteLine("  aviso: falta " + p); continue; }
                using var fs = File.OpenRead(p); using var gz = new GZipStream(fs, CompressionMode.Decompress); using var sr = new StreamReader(gz, Encoding.UTF8);
                string l;
                while ((l = sr.ReadLine()) != null)
                {
                    if (l.Length < 40) continue;
                    FotoCadena f; string ts;
                    try { f = Parsear(l, libro, conFilas, out ts); } catch { continue; }
                    if (f == null) continue;
                    if (vistos.TryGetValue(ts, out var ya) && ya.GeneradoUtc <= f.GeneradoUtc) continue;
                    if (!vistos.ContainsKey(ts)) orden.Add(ts);
                    vistos[ts] = f;
                }
            }
            var lista = new List<FotoCadena>(); foreach (var ts in orden) lista.Add(vistos[ts]);
            // sorted(..., key=generado): estable
            var idx = new int[lista.Count]; for (int i = 0; i < idx.Length; i++) idx[i] = i;
            Array.Sort(idx, (a, b) => { int c = lista[a].GeneradoUtc.CompareTo(lista[b].GeneradoUtc); return c != 0 ? c : a.CompareTo(b); });
            var ord = new List<FotoCadena>(); foreach (var i in idx) ord.Add(lista[i]);
            if (_l.TryGetValue(libro, out var prev)) { prev.AddRange(ord); prev.Sort((a, b) => a.GeneradoUtc.CompareTo(b.GeneradoUtc)); }
            else _l[libro] = ord;
        }

        private static FotoCadena Parsear(string linea, string libro, bool conFilas, out string tsTxt)
        {
            tsTxt = null;
            using var doc = JsonDocument.Parse(linea);
            var r = doc.RootElement;
            if (!r.TryGetProperty("generado", out var g) || !r.TryGetProperty("cadena_ts", out var cts)) return null;
            var gen = DateTimeOffset.Parse(g.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).UtcDateTime;
            tsTxt = cts.GetString();
            if (!r.TryGetProperty("cadena", out var c) || c.ValueKind != JsonValueKind.Object) return null;
            var filasEl = c.TryGetProperty("filas", out var fe) && fe.ValueKind == JsonValueKind.Array ? fe : default;
            int nf = 0; var filas = new List<FilaCadena>();
            if (filasEl.ValueKind == JsonValueKind.Array)
                foreach (var x in filasEl.EnumerateArray())
                {
                    if (x.ValueKind != JsonValueKind.Array || x.GetArrayLength() < 8) continue;
                    nf++;
                    if (!conFilas) continue;
                    double D(int i) { var e = x[i]; return e.ValueKind == JsonValueKind.Number ? e.GetDouble() : double.NaN; }
                    filas.Add(new FilaCadena(D(0), (int)D(1), D(2), D(3), D(4), D(5), D(6), D(7)));
                }
            if (nf == 0) return null;                                  // libros.fotos_cboe: if not filas: continue
            var ts = DateTime.SpecifyKind(DateTime.ParseExact(tsTxt, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), DateTimeKind.Utc);
            DateTime dato;
            string ut = c.TryGetProperty("ultimo_trade", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
            if (!string.IsNullOrEmpty(ut))
                dato = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(DateTime.ParseExact(ut, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture), Ny), DateTimeKind.Utc);
            else
            {
                int ret = r.TryGetProperty("retraso_s", out var rs) && rs.ValueKind == JsonValueKind.Number ? rs.GetInt32() : 902;
                dato = ts.AddSeconds(-(ret == 0 ? 902 : ret));
            }
            double spot = c.TryGetProperty("spot_idx", out var si) && si.ValueKind == JsonValueKind.Number ? si.GetDouble() : 0;
            if (spot == 0 && r.TryGetProperty("spot", out var sp) && sp.ValueKind == JsonValueKind.Number) spot = sp.GetDouble();
            var dias = new List<double>();
            if (c.TryGetProperty("vencimientos", out var ve) && ve.ValueKind == JsonValueKind.Array)
                foreach (var v in ve.EnumerateArray()) dias.Add(v.TryGetProperty("dias", out var dd) && dd.ValueKind == JsonValueKind.Number ? dd.GetDouble() : 0.0);
            double pdc = double.NaN;
            if (r.TryGetProperty("sub", out var sub) && sub.ValueKind == JsonValueKind.Object && sub.TryGetProperty("prev_day_close", out var pd) && pd.ValueKind == JsonValueKind.Number) pdc = pd.GetDouble();
            double oit = 0; foreach (var f in filas) oit += (double.IsNaN(f.OiC) ? 0 : f.OiC) + (double.IsNaN(f.OiP) ? 0 : f.OiP);
            return new FotoCadena
            {
                Libro = libro, GeneradoUtc = DateTime.SpecifyKind(gen, DateTimeKind.Utc), TsUtc = ts, DatoUtc = dato, Spot = spot == 0 ? double.NaN : spot,
                Dias = dias.ToArray(), Filas = filas.ToArray(), EsFuturo = false, OiTotal = oit, CierreAnterior = pdc,
            };
        }

        public void Arrancar() { }
        public void Parar() { }
        public IReadOnlyList<FotoCadena> Fotos(string libro, DateTime desdeUtc, DateTime hastaUtc)
        {
            var r = new List<FotoCadena>();
            if (!_l.TryGetValue(libro, out var l)) return r;
            foreach (var f in l) if (f.GeneradoUtc >= desdeUtc && f.GeneradoUtc < hastaUtc && f.GeneradoUtc <= Reloj) r.Add(f);
            return r;
        }
        public FotoCadena Ultima(string libro)
        {
            if (!_l.TryGetValue(libro, out var l)) return null;
            for (int i = l.Count - 1; i >= 0; i--) if (l[i].GeneradoUtc <= Reloj) return l[i];
            return null;
        }
        public long Version
        {
            get { long n = 0; foreach (var l in _l.Values) foreach (var f in l) if (f.GeneradoUtc <= Reloj) n++; return n; }
        }
        public string Estado => "arnes (cboe-local)";
        public int Cuantas(string libro) => _l.TryGetValue(libro, out var l) ? l.Count : 0;

        /// <summary>Simula el bloque 'sub' del descargador propio (cboe-local no lo trae): prev_day_close en las fotos con generado en [desde, hasta).
        /// NaN lo borra.</summary>
        public int PonerPdc(string libro, DateTime desde, DateTime hasta, double valor)
        {
            int n = 0;
            if (_l.TryGetValue(libro, out var l)) foreach (var f in l) if (f.GeneradoUtc >= desde && f.GeneradoUtc < hasta) { f.CierreAnterior = valor; n++; }
            return n;
        }
    }
}
