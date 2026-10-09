// DobleCinta.cs — arnes NDX (B3b). ICinta de prueba que reproduce EXACTO lo que arma preview_niveles.Motor para una sesion historica:
//   ticks: cintas de los dias previos (<= 6, cada una en su ventana de sesion; relleno solo antes del primer tick vivo) + la de la sesion;
//   velas previas: cache de ATAS (velas.cargar('MNQ', 2): dom/ronda3 + velas_cache_2026-10-07, la nueva pisa sesiones enteras) + las velas de
//   las cintas previas (pisan a la cache), solo antes del inicio de la sesion; velas de la sesion: velas_de_ticks (cerradas).
//   Precio(ts) = backtest_familia.Precio; CierreConocido(t) = velas.cierre_conocido sobre las velas de la SESION (lo que usa calcular()).
// No es el ICinta del indicador (ese es de B1); solo sirve de entrada congelada para la paridad.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PythiaGexCuatro.Familia;

namespace ArnesNdx
{
    public sealed class DobleCinta : ICinta
    {
        const long MS2 = 120_000;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        long[] _tt; double[] _tp;                 // todos los ticks (previos + sesion), orden estable por t
        long[] _vt; double[] _vo, _vc;            // velas previas + de la sesion (aperturas ms)
        long[] _st; double[] _so, _sh, _sl, _sc;  // velas cerradas de la sesion
        public int NTicksSesion, NTicksPrevios, NVelasPrevias, NVelasSesion;
        public bool SoloSesion;                   // prueba de reinicio: sin ticks ni velas de dias previos

        public static long Ms(DateTime utc) => (utc.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMillisecond;
        public static DateTime DeMs(long ms) => DateTime.SpecifyKind(DateTime.UnixEpoch.AddTicks(ms * TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);

        public DobleCinta(DateTime dia, string carpetaCinta, string velasViejas, string velasNuevas, int diasCinta = 6, bool soloSesion = false)
        {
            SoloSesion = soloSesion;
            long ini = Ms(dia.AddHours(-2)), fin = Ms(dia.AddHours(21));
            // ---- velas previas: cache de ATAS
            var cache = soloSesion ? new List<(long T, double O, double H, double L, double C)>() : CargarCache(velasViejas, velasNuevas);
            var porT = new Dictionary<long, (double O, double H, double L, double C)>();
            var orden = new List<long>();
            void Poner(long t, double o, double h, double l, double c) { if (!porT.ContainsKey(t)) orden.Add(t); porT[t] = (o, h, l, c); }
            foreach (var v in cache) Poner(v.T, v.O, v.H, v.L, v.C);
            // ---- cintas de los dias previos
            var prevT = new List<long>(); var prevP = new List<double>();
            if (!soloSesion && Directory.Exists(carpetaCinta))
            {
                foreach (var p in Directory.GetFiles(carpetaCinta, "cinta-NQ-????-??-??.csv").OrderBy(x => x, StringComparer.Ordinal))
                {
                    var ds = Path.GetFileName(p).Substring(9, 10);
                    if (!DateTime.TryParseExact(ds, "yyyy-MM-dd", Inv, DateTimeStyles.None, out var d)) continue;
                    if (d >= dia.Date || (dia.Date - d).TotalDays > diasCinta) continue;
                    var (tt, pp) = LeerCintaDia(carpetaCinta, d);
                    if (tt.Length == 0) continue;
                    prevT.AddRange(tt); prevP.AddRange(pp);
                    foreach (var v in VelasDeTicks(tt, pp).Cerradas) Poner(v.T, v.O, v.H, v.L, v.C);
                }
            }
            var vprev = orden.Where(t => t < ini).OrderBy(t => t).Select(t => (T: t, porT[t].O, porT[t].H, porT[t].L, porT[t].C)).ToList();
            // ticks previos ordenados (estable)
            var (pt, pp2) = OrdenEstable(prevT.ToArray(), prevP.ToArray());
            // ---- la sesion
            var (st, sp) = LeerCintaDia(carpetaCinta, dia.Date);
            var vs = VelasDeTicks(st, sp).Cerradas;
            NTicksSesion = st.Length; NTicksPrevios = pt.Length; NVelasPrevias = vprev.Count; NVelasSesion = vs.Count;
            _tt = pt.Concat(st).ToArray(); _tp = pp2.Concat(sp).ToArray();
            var todas = vprev.Concat(vs).ToList();
            _vt = todas.Select(v => v.T).ToArray(); _vo = todas.Select(v => v.O).ToArray(); _vc = todas.Select(v => v.C).ToArray();
            _st = vs.Select(v => v.T).ToArray(); _so = vs.Select(v => v.O).ToArray(); _sh = vs.Select(v => v.H).ToArray(); _sl = vs.Select(v => v.L).ToArray(); _sc = vs.Select(v => v.C).ToArray();
        }

        // ------------------------------------------------------------------ ICinta
        public double Precio(DateTime tUtc)
        {
            long ts = Ms(tUtc);
            int n = _tt.Length;
            if (n > 0)
            {
                int j = UltimoHasta(_tt, ts);
                if (j >= 0 && ts <= _tt[n - 1] && ts - _tt[j] <= 120_000) return _tp[j];
            }
            int i = UltimoHasta(_vt, ts);
            if (i >= 0)
            {
                double dt = (ts - _vt[i]) / 1000.0;
                if (dt >= 0 && dt < 120) return _vo[i] + (_vc[i] - _vo[i]) * dt / 120.0;
            }
            return double.NaN;
        }

        public double PrecioSoloTick(DateTime tUtc, int maxEdadS = 120)
        {
            long ts = Ms(tUtc);
            int n = _tt.Length;
            if (n == 0) return double.NaN;
            int j = UltimoHasta(_tt, ts);
            if (j >= 0 && ts <= _tt[n - 1] && ts - _tt[j] <= maxEdadS * 1000L) return _tp[j];
            return double.NaN;
        }

        public double CierreConocido(DateTime tUtc)
        {
            long ts = Ms(tUtc);
            // fin = t + 120 s; i = searchsorted(fin, ts, 'right') - 1
            int lo = 0, hi = _st.Length;
            while (lo < hi) { int m = (lo + hi) >> 1; if (_st[m] + MS2 <= ts) lo = m + 1; else hi = m; }
            int i = lo - 1;
            if (i < 0) return double.NaN;
            if (ts - (_st[i] + MS2) > 4L * 3600 * 1000) return double.NaN;
            return _sc[i];
        }

        public IReadOnlyList<(long Ms, double O, double H, double L, double C)> VelasM2(DateTime desdeUtc, DateTime hastaUtc)
        {
            long a = Ms(desdeUtc), b = Ms(hastaUtc);
            var r = new List<(long, double, double, double, double)>();
            for (int i = 0; i < _st.Length; i++) if (_st[i] >= a && _st[i] < b) r.Add((_st[i], _so[i], _sh[i], _sl[i], _sc[i]));
            return r;
        }

        public DateTime UltimoTickUtc => _tt.Length > 0 ? DeMs(_tt[_tt.Length - 1]) : DateTime.MinValue;

        // ------------------------------------------------------------------ lectura (port de preview_niveles.ticks_csv / _leer_cinta_dia / velas_de_ticks)
        static int UltimoHasta(long[] a, long x)
        {
            int lo = 0, hi = a.Length;
            while (lo < hi) { int m = (lo + hi) >> 1; if (a[m] <= x) lo = m + 1; else hi = m; }
            return lo - 1;
        }

        static (long[] T, double[] P) TicksCsv(string ruta)
        {
            var t = new List<long>(1 << 20); var p = new List<double>(1 << 20);
            if (!File.Exists(ruta)) return (new long[0], new double[0]);
            using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
            using var sr = new StreamReader(fs);
            string l;
            while ((l = sr.ReadLine()) != null)
            {
                if (l.Length == 0 || l[0] == 't') continue;
                int c1 = l.IndexOf(',');
                if (c1 < 0) continue;
                int c2 = l.IndexOf(',', c1 + 1);
                var s0 = l.AsSpan(0, c1); var s1 = c2 < 0 ? l.AsSpan(c1 + 1) : l.AsSpan(c1 + 1, c2 - c1 - 1);
                if (!long.TryParse(s0, NumberStyles.Integer, Inv, out long tv)) continue;
                if (!double.TryParse(s1, NumberStyles.Float, Inv, out double pv)) continue;
                t.Add(tv); p.Add(pv);
            }
            return (t.ToArray(), p.ToArray());
        }

        static (long[] T, double[] P) OrdenEstable(long[] t, double[] p)
        {
            var idx = Enumerable.Range(0, t.Length).ToArray();
            var ord = idx.OrderBy(i => t[i]).ToArray();      // OrderBy es estable
            return (ord.Select(i => t[i]).ToArray(), ord.Select(i => p[i]).ToArray());
        }

        /// <summary>preview_niveles._leer_cinta_dia: vivo + relleno (solo antes del primer tick vivo), orden estable, ventana [d-2h, d+21h).</summary>
        static (long[] T, double[] P) LeerCintaDia(string carpeta, DateTime d)
        {
            var ds = d.ToString("yyyy-MM-dd", Inv);
            var (vt, vp) = TicksCsv(Path.Combine(carpeta, "cinta-NQ-" + ds + ".csv"));
            var rel = Path.Combine(carpeta, "cinta-NQ-" + ds + "-relleno.csv");
            if (File.Exists(rel))
            {
                var (rt, rp) = TicksCsv(rel);
                if (vt.Length > 0)
                {
                    long mn = vt.Min();
                    var keep = Enumerable.Range(0, rt.Length).Where(i => rt[i] < mn).ToArray();
                    rt = keep.Select(i => rt[i]).ToArray(); rp = keep.Select(i => rp[i]).ToArray();
                }
                vt = rt.Concat(vt).ToArray(); vp = rp.Concat(vp).ToArray();
            }
            if (vt.Length == 0) return (vt, vp);
            var (ot, op) = OrdenEstable(vt, vp);
            long ini = Ms(d.AddHours(-2)), fin = Ms(d.AddHours(21));
            var k = Enumerable.Range(0, ot.Length).Where(i => ot[i] >= ini && ot[i] < fin).ToArray();
            return (k.Select(i => ot[i]).ToArray(), k.Select(i => op[i]).ToArray());
        }

        /// <summary>preview_niveles.velas_de_ticks: baldes de 120 s; o = primer precio, c = ultimo, h/l = max/min; cerrada si b + 120 s &lt;= ultimo tick.</summary>
        static (List<(long T, double O, double H, double L, double C)> Cerradas, (long T, double O, double H, double L, double C)? Viva) VelasDeTicks(long[] t, double[] p)
        {
            var r = new List<(long, double, double, double, double)>();
            if (t.Length == 0) return (r, null);
            long ult = t[t.Length - 1];
            int i = 0;
            (long, double, double, double, double)? viva = null;
            while (i < t.Length)
            {
                long b = (long)Math.Floor(t[i] / (double)MS2) * MS2;
                if (t[i] >= 0) b = t[i] / MS2 * MS2;
                double o = p[i], h = p[i], l = p[i], c = p[i];
                int j = i;
                while (j < t.Length && (t[j] >= 0 ? t[j] / MS2 * MS2 : (long)Math.Floor(t[j] / (double)MS2) * MS2) == b)
                {
                    if (p[j] > h) h = p[j]; if (p[j] < l) l = p[j]; c = p[j]; j++;
                }
                if (b + MS2 <= ult) r.Add((b, o, h, l, c)); else viva = (b, o, h, l, c);
                i = j;
            }
            return (r, viva);
        }

        /// <summary>velas.cargar('MNQ', 2): vieja + nueva (la nueva pisa sesiones enteras); por archivo: sin c vacio, sin t repetida (gana la ultima), orden por t.</summary>
        static List<(long T, double O, double H, double L, double C)> CargarCache(string viejas, string nuevas)
        {
            var n = LeerVelasCsv(nuevas);
            var sesN = new HashSet<DateTime>(n.Select(v => DeMs(v.T).AddHours(2).Date));
            var r = new List<(long, double, double, double, double)>();
            foreach (var v in LeerVelasCsv(viejas)) if (!sesN.Contains(DeMs(v.T).AddHours(2).Date)) r.Add(v);
            r.AddRange(n);
            return r.OrderBy(v => v.Item1).ToList();
        }

        static List<(long T, double O, double H, double L, double C)> LeerVelasCsv(string ruta)
        {
            var porT = new Dictionary<long, (double, double, double, double)>();
            if (!File.Exists(ruta)) return new List<(long, double, double, double, double)>();
            using var sr = new StreamReader(ruta);
            var cab = sr.ReadLine().Split(',');
            int it = Array.IndexOf(cab, "t"), io = Array.IndexOf(cab, "o"), ih = Array.IndexOf(cab, "h"), il = Array.IndexOf(cab, "l"), ic = Array.IndexOf(cab, "c");
            string l;
            while ((l = sr.ReadLine()) != null)
            {
                var a = l.Split(',');
                if (a.Length <= Math.Max(Math.Max(it, io), Math.Max(Math.Max(ih, il), ic))) continue;
                if (string.IsNullOrWhiteSpace(a[ic]) || !double.TryParse(a[ic], NumberStyles.Float, Inv, out double c)) continue;
                if (!DateTime.TryParse(a[it], Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)) continue;
                double.TryParse(a[io], NumberStyles.Float, Inv, out double o);
                double.TryParse(a[ih], NumberStyles.Float, Inv, out double h);
                double.TryParse(a[il], NumberStyles.Float, Inv, out double lo);
                porT[Ms(DateTime.SpecifyKind(t, DateTimeKind.Utc))] = (o, h, lo, c);
            }
            return porT.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value.Item1, kv.Value.Item2, kv.Value.Item3, kv.Value.Item4)).ToList();
        }
    }
}
