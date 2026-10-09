// CintaCsv.cs — arnes de paridad NQ (B3a, 08-10-2026). DOBLE DE PRUEBA de ICinta (el de verdad lo escribe B1 sobre la cinta del clon).
// Lee la cinta archivada de la 3.0 (profundidad/estado/cinta/cinta-NQ-<sesion>.csv + -relleno.csv) igual que preview_niveles:
//   ticks_csv (salta cabecera/lineas rotas) -> relleno solo antes del primer tick vivo -> orden ESTABLE por t -> ventana [dia-2h, dia+21h)
//   velas m2 = velas_de_ticks: balde floor(t/120000)*120000, o primero, h max, l min, c ultimo; CERRADA si b + 120000 <= ultimo tick.
// Solo la sesion (la vista previa usa los 6 dias previos para Precio(); el libro NQ no los necesita).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PythiaGexCuatro.Familia;

namespace ParidadNq
{
    public sealed class CintaCsv : ICinta
    {
        public long[] Tt = Array.Empty<long>();
        public double[] Tp = Array.Empty<double>();
        public readonly List<(long Ms, double O, double H, double L, double C)> Velas = new List<(long, double, double, double, double)>();
        private long[] _fin = Array.Empty<long>();
        public (long Ms, double O, double H, double L, double C)? Viva;
        public string Origen = "";

        public static CintaCsv Cargar(string carpeta, string dia)
        {
            var c = new CintaCsv();
            string viv = Path.Combine(carpeta, "cinta-NQ-" + dia + ".csv"), rel = Path.Combine(carpeta, "cinta-NQ-" + dia + "-relleno.csv");
            var (vt, vp) = Leer(viv);
            var (rt, rp) = Leer(rel);
            var t = new List<long>(rt.Count + vt.Count); var p = new List<double>(rt.Count + vt.Count);
            long minV = long.MaxValue; foreach (var x in vt) if (x < minV) minV = x;
            for (int i = 0; i < rt.Count; i++) if (vt.Count == 0 || rt[i] < minV) { t.Add(rt[i]); p.Add(rp[i]); }
            int nRel = t.Count;
            t.AddRange(vt); p.AddRange(vp);
            // orden estable por t
            var idx = new int[t.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            Array.Sort(idx, (a, b) => { int k = t[a].CompareTo(t[b]); return k != 0 ? k : a.CompareTo(b); });
            long ini = TiempoFam.Ms(TiempoFam.IniSesion(dia)), fin = TiempoFam.Ms(TiempoFam.FinSesion(dia));
            var tt = new List<long>(idx.Length); var tp = new List<double>(idx.Length);
            foreach (var i in idx) { long x = t[i]; if (x >= ini && x < fin) { tt.Add(x); tp.Add(p[i]); } }
            c.Tt = tt.ToArray(); c.Tp = tp.ToArray();
            c.ArmarVelas();
            c.Origen = string.Format(CultureInfo.InvariantCulture, "cinta {0}: {1} ticks vivos + {2} de relleno -> {3} en la sesion, {4} velas m2 cerradas",
                dia, vt.Count, nRel, c.Tt.Length, c.Velas.Count);
            return c;
        }

        private static (List<long>, List<double>) Leer(string ruta)
        {
            var t = new List<long>(); var p = new List<double>();
            if (!File.Exists(ruta)) return (t, p);
            using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sr = new StreamReader(fs))
            {
                string l;
                while ((l = sr.ReadLine()) != null)
                {
                    if (l.Length == 0 || l[0] == 't') continue;
                    var a = l.Split(',');
                    if (a.Length < 2) continue;
                    if (!long.TryParse(a[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long x)) continue;
                    if (!double.TryParse(a[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double y)) continue;
                    t.Add(x); p.Add(y);
                }
            }
            return (t, p);
        }

        private void ArmarVelas()
        {
            Velas.Clear(); Viva = null;
            if (Tt.Length == 0) { _fin = Array.Empty<long>(); return; }
            long ultimo = Tt[Tt.Length - 1];
            int i = 0;
            while (i < Tt.Length)
            {
                long b = TiempoFam.AperturaM2(Tt[i]);
                double o = Tp[i], h = Tp[i], l = Tp[i], c = Tp[i];
                int j = i;
                while (j < Tt.Length && TiempoFam.AperturaM2(Tt[j]) == b) { double x = Tp[j]; if (x > h) h = x; if (x < l) l = x; c = x; j++; }
                if (b + TiempoFam.MS_M2 <= ultimo) Velas.Add((b, o, h, l, c)); else Viva = (b, o, h, l, c);
                i = j;
            }
            _fin = new long[Velas.Count];
            for (int k = 0; k < Velas.Count; k++) _fin[k] = Velas[k].Ms + TiempoFam.MS_M2;
        }

        // ------------------------------------------------------------------ ICinta
        public DateTime UltimoTickUtc => Tt.Length > 0 ? TiempoFam.DeMs(Tt[Tt.Length - 1]) : default;

        public double CierreConocido(DateTime tUtc)
        {
            long ts = TiempoFam.Ms(tUtc);
            int i = NumFam.UltimoMenorIgual(_fin, ts);
            if (i < 0) return double.NaN;
            if (ts - _fin[i] > 4L * 3600_000L) return double.NaN;
            return Velas[i].C;
        }

        public double PrecioSoloTick(DateTime tUtc, int maxEdadS = 120)
        {
            long ts = TiempoFam.Ms(tUtc);
            int j = NumFam.UltimoMenorIgual(Tt, ts);
            if (j < 0 || ts - Tt[j] > maxEdadS * 1000L) return double.NaN;
            return Tp[j];
        }

        public double Precio(DateTime tUtc)
        {
            long ts = TiempoFam.Ms(tUtc);
            if (Tt.Length > 0)
            {
                int j = NumFam.UltimoMenorIgual(Tt, ts);
                if (j >= 0 && ts <= Tt[Tt.Length - 1] && ts - Tt[j] <= 120_000L) return Tp[j];
            }
            var ms = new long[Velas.Count];
            for (int k = 0; k < ms.Length; k++) ms[k] = Velas[k].Ms;
            int i = NumFam.UltimoMenorIgual(ms, ts);
            if (i >= 0)
            {
                double dt = (ts - Velas[i].Ms) / 1000.0;
                if (dt >= 0 && dt < 120) return Velas[i].O + (Velas[i].C - Velas[i].O) * dt / 120.0;
            }
            return double.NaN;
        }

        public IReadOnlyList<(long Ms, double O, double H, double L, double C)> VelasM2(DateTime desdeUtc, DateTime hastaUtc)
        {
            long a = TiempoFam.Ms(desdeUtc), b = TiempoFam.Ms(hastaUtc);
            var o = new List<(long, double, double, double, double)>();
            foreach (var v in Velas) if (v.Ms >= a && v.Ms < b) o.Add(v);
            return o;
        }
    }
}
