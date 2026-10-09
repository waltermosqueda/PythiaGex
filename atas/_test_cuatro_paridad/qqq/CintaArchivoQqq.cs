// CintaArchivoQqq.cs — DOBLE DE PRUEBA de ICinta para el arnes de QQQ (B3c). Solo lectura de archivos guardados, sin ATAS.
// Reproduce lo que arma preview_niveles.Motor(vivo=False) para la paridad:
//   * velas previas = cache de ATAS exportada (laboratorio/dom/ronda3/velas + laboratorio/tres/datos/velas_cache_2026-10-07, la nueva pisa
//     sesiones enteras; velas.cargar) + velas m2 de las cintas de los 6 dias previos (pisan por t), solo t < inicio de la sesion;
//   * ticks = cintas de los dias previos (cada una en su ventana de sesion) + la sesion (relleno antes del primer tick vivo + vivo);
//   * velas de la sesion = velas_de_ticks (orden estable por t, solo las cerradas respecto del ultimo tick);
//   * Precio(ts) = backtest_familia.Precio (ultimo tick <= ts a <= 120 s y ts <= ultimo tick; si no, la vela m2 que contiene ts interpolada);
//   * CierreConocido(t) = velas.cierre_conocido sobre las velas de la SESION (como Motor.calcular), NaN si cerro hace > 4 h.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PythiaGexCuatro.Familia;

namespace ParidadQqq
{
    public sealed class CintaArchivoQqq : ICinta
    {
        public static readonly long EpocaTicks = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        public static long Ms(DateTime t) => (t.Ticks - EpocaTicks) / TimeSpan.TicksPerMillisecond;
        public static DateTime DeMs(long ms) => new DateTime(EpocaTicks + ms * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
        const long MS2 = 120_000;

        private long[] _tt; private double[] _tp;                       // ticks (previos + sesion)
        private long[] _vt; private double[] _vo, _vc;                  // velas previas + sesion (para Precio)
        private long[] _st; private double[] _so, _sh, _sl, _sc;        // velas de la sesion (cerradas)
        public int NTicksSesion, NTicksPrev, NVelasPrev;
        public string Resumen = "";

        public CintaArchivoQqq(string raiz, string dia, int diasCinta = 6)
        {
            var inv = CultureInfo.InvariantCulture;
            var d0 = DateTime.ParseExact(dia, "yyyy-MM-dd", inv);
            var ini = DateTime.SpecifyKind(d0.AddHours(-2), DateTimeKind.Utc);
            var cintaDir = Path.Combine(raiz, "profundidad", "estado", "cinta");

            // ---- velas de la cache (velas.cargar)
            var viejas = LeerVelasCsv(Path.Combine(raiz, "laboratorio", "dom", "ronda3", "velas", "velas-MNQ-m2.csv"));
            var nuevas = LeerVelasCsv(Path.Combine(raiz, "laboratorio", "tres", "datos", "velas_cache_2026-10-07", "velas-MNQ-m2.csv"));
            var sesNuevas = new HashSet<DateTime>(nuevas.Select(v => DeMs(v.T).AddHours(2).Date));
            var cache = viejas.Where(v => !sesNuevas.Contains(DeMs(v.T).AddHours(2).Date)).Concat(nuevas).ToList();

            // ---- cintas de los dias previos
            var prevT = new List<long>(); var prevP = new List<double>();
            var velasPrevDias = new List<(long T, double O, double H, double L, double C)>();
            foreach (var p in Directory.GetFiles(cintaDir, "cinta-NQ-????-??-??.csv").OrderBy(x => x, StringComparer.Ordinal))
            {
                var dn = Path.GetFileName(p).Substring(9, 10);
                if (string.CompareOrdinal(dn, dia) >= 0) continue;
                var dd = DateTime.ParseExact(dn, "yyyy-MM-dd", inv);
                if ((d0 - dd).TotalDays > diasCinta) continue;
                var (tt, pp) = LeerCintaDia(cintaDir, dn);
                if (tt.Length == 0) continue;
                prevT.AddRange(tt); prevP.AddRange(pp);
                velasPrevDias.AddRange(VelasDeTicks(tt, pp));
            }
            // vprev = concat(cache, velas de cintas) drop_duplicates(t, keep=last), orden por t, t < ini
            var porT = new Dictionary<long, (long T, double O, double H, double L, double C)>();
            foreach (var v in cache) porT[v.T] = v;
            foreach (var v in velasPrevDias) porT[v.T] = v;
            long iniMs = Ms(ini);
            var vprev = porT.Values.Where(v => v.T < iniMs).OrderBy(v => v.T).ToList();
            NVelasPrev = vprev.Count;
            // ordenar ticks previos (estable por t)
            var idxPrev = Enumerable.Range(0, prevT.Count).OrderBy(i => prevT[i]).ToArray();
            var ptt = idxPrev.Select(i => prevT[i]).ToArray(); var ptp = idxPrev.Select(i => prevP[i]).ToArray();
            NTicksPrev = ptt.Length;

            // ---- la sesion
            var (st, sp) = LeerCintaDia(cintaDir, dia);
            NTicksSesion = st.Length;
            var vs = VelasDeTicks(st, sp);
            _st = vs.Select(v => v.T).ToArray(); _so = vs.Select(v => v.O).ToArray(); _sh = vs.Select(v => v.H).ToArray(); _sl = vs.Select(v => v.L).ToArray(); _sc = vs.Select(v => v.C).ToArray();

            _tt = ptt.Concat(st).ToArray(); _tp = ptp.Concat(sp).ToArray();
            var todas = vprev.Concat(vs).ToList();
            _vt = todas.Select(v => v.T).ToArray(); _vo = todas.Select(v => v.O).ToArray(); _vc = todas.Select(v => v.C).ToArray();
            Resumen = $"cinta {dia}: ticks sesion {NTicksSesion}, ticks previos {NTicksPrev}, velas sesion {_st.Length}, velas previas {NVelasPrev}";
        }

        // velas.cargar/_leer: t,o,h,l,c; dropna(c); drop_duplicates(t, keep=last); orden por t
        private static List<(long T, double O, double H, double L, double C)> LeerVelasCsv(string ruta)
        {
            var inv = CultureInfo.InvariantCulture;
            var d = new Dictionary<long, (long, double, double, double, double)>();
            if (!File.Exists(ruta)) return new List<(long, double, double, double, double)>();
            using var sr = new StreamReader(ruta);
            var cab = sr.ReadLine().Split(',');
            int it = Array.IndexOf(cab, "t"), io = Array.IndexOf(cab, "o"), ih = Array.IndexOf(cab, "h"), il = Array.IndexOf(cab, "l"), ic = Array.IndexOf(cab, "c");
            string l;
            while ((l = sr.ReadLine()) != null)
            {
                var a = l.Split(',');
                if (a.Length <= ic || string.IsNullOrEmpty(a[ic])) continue;
                if (!DateTime.TryParse(a[it], inv, DateTimeStyles.None, out var t)) continue;
                double P(int i) => string.IsNullOrEmpty(a[i]) ? double.NaN : double.Parse(a[i], inv);
                long ms = Ms(DateTime.SpecifyKind(t, DateTimeKind.Utc));
                d[ms] = (ms, P(io), P(ih), P(il), P(ic));
            }
            return d.Values.OrderBy(v => v.Item1).Select(v => (v.Item1, v.Item2, v.Item3, v.Item4, v.Item5)).ToList();
        }

        // Motor._leer_cinta_dia / _recargar_sesion_ticks: relleno (solo t < primer tick vivo) + vivo, orden estable por t, ventana [d-2h, d+21h)
        private static (long[] T, double[] P) LeerCintaDia(string dir, string d)
        {
            var inv = CultureInfo.InvariantCulture;
            var (vt, vp) = TicksCsv(Path.Combine(dir, "cinta-NQ-" + d + ".csv"));
            var (rt, rp) = TicksCsv(Path.Combine(dir, "cinta-NQ-" + d + "-relleno.csv"));
            var t = new List<long>(); var p = new List<double>();
            if (rt.Count > 0)
            {
                long min = vt.Count > 0 ? vt.Min() : long.MaxValue;
                for (int i = 0; i < rt.Count; i++) if (vt.Count == 0 || rt[i] < min) { t.Add(rt[i]); p.Add(rp[i]); }
            }
            t.AddRange(vt); p.AddRange(vp);
            var idx = Enumerable.Range(0, t.Count).OrderBy(i => t[i]).ToArray();   // OrderBy es estable
            var dd = DateTime.SpecifyKind(DateTime.ParseExact(d, "yyyy-MM-dd", inv), DateTimeKind.Utc);
            long a = Ms(dd.AddHours(-2)), b = Ms(dd.AddHours(21));
            var T = new List<long>(idx.Length); var P = new List<double>(idx.Length);
            foreach (var i in idx) if (t[i] >= a && t[i] < b) { T.Add(t[i]); P.Add(p[i]); }
            return (T.ToArray(), P.ToArray());
        }

        // preview_niveles.ticks_csv: 't,precio,...' en el orden del archivo, sin cabecera ni lineas rotas
        private static (List<long>, List<double>) TicksCsv(string ruta)
        {
            var inv = CultureInfo.InvariantCulture;
            var t = new List<long>(); var p = new List<double>();
            if (!File.Exists(ruta)) return (t, p);
            using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            string l;
            while ((l = sr.ReadLine()) != null)
            {
                if (l.Length == 0 || l[0] == 't') continue;
                int c1 = l.IndexOf(',');
                if (c1 < 0) continue;
                int c2 = l.IndexOf(',', c1 + 1);
                var sp = c2 < 0 ? l.Substring(c1 + 1) : l.Substring(c1 + 1, c2 - c1 - 1);
                if (!long.TryParse(l.AsSpan(0, c1), NumberStyles.Integer, inv, out var tt)) continue;
                if (!double.TryParse(sp, NumberStyles.Float, inv, out var pp)) continue;
                t.Add(tt); p.Add(pp);
            }
            return (t, p);
        }

        // preview_niveles.velas_de_ticks (cerradas = b + 120 s <= ultimo tick)
        public static List<(long T, double O, double H, double L, double C)> VelasDeTicks(long[] t, double[] p)
        {
            var r = new List<(long, double, double, double, double)>();
            if (t.Length == 0) return r;
            long last = t[t.Length - 1];
            int i = 0;
            while (i < t.Length)
            {
                long b = Math.DivRem(t[i], MS2, out long rem) * MS2; if (rem < 0) b -= MS2;
                int j = i; double h = p[i], lo = p[i];
                while (j + 1 < t.Length && FloorM2(t[j + 1]) == b) { j++; if (p[j] > h) h = p[j]; if (p[j] < lo) lo = p[j]; }
                if (b + MS2 <= last) r.Add((b, p[i], h, lo, p[j]));
                i = j + 1;
            }
            return r;
        }
        private static long FloorM2(long t) { long b = Math.DivRem(t, MS2, out long rem) * MS2; if (rem < 0) b -= MS2; return b; }

        // ---------------------------------------------------------------- ICinta
        public double Precio(DateTime tUtc)
        {
            long ts = Ms(tUtc);
            if (_tt.Length > 0)
            {
                int j = UltimoLe(_tt, ts);
                if (j >= 0 && ts <= _tt[_tt.Length - 1] && ts - _tt[j] <= 120_000) return _tp[j];
            }
            int i = UltimoLe(_vt, ts);
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
            int j = UltimoLe(_tt, ts);
            return j >= 0 && ts - _tt[j] <= maxEdadS * 1000L ? _tp[j] : double.NaN;
        }

        public double CierreConocido(DateTime tUtc)
        {
            long ts = Ms(tUtc);
            // ultima vela de la sesion con fin = t + 120 s <= ts
            int lo = 0, hi = _st.Length - 1, k = -1;
            while (lo <= hi) { int m = (lo + hi) >> 1; if (_st[m] + MS2 <= ts) { k = m; lo = m + 1; } else hi = m - 1; }
            if (k < 0) return double.NaN;
            if (ts - (_st[k] + MS2) > 4L * 3600_000) return double.NaN;
            return _sc[k];
        }

        public IReadOnlyList<(long Ms, double O, double H, double L, double C)> VelasM2(DateTime desdeUtc, DateTime hastaUtc)
        {
            long a = Ms(desdeUtc), b = Ms(hastaUtc);
            var r = new List<(long, double, double, double, double)>();
            for (int i = 0; i < _st.Length; i++) if (_st[i] >= a && _st[i] < b) r.Add((_st[i], _so[i], _sh[i], _sl[i], _sc[i]));
            return r;
        }

        public DateTime UltimoTickUtc => _tt.Length > 0 ? DeMs(_tt[_tt.Length - 1]) : DateTime.MinValue;

        private static int UltimoLe(long[] a, long x)
        {
            int lo = 0, hi = a.Length - 1, k = -1;
            while (lo <= hi) { int m = (lo + hi) >> 1; if (a[m] <= x) { k = m; lo = m + 1; } else hi = m - 1; }
            return k;
        }
    }
}
