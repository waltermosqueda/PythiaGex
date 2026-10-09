// TqqqPerfil.cs — PythiaGex 4.1, modulo TQQQ (B4), 08-10-2026.
// Port exacto de tqqq_vivo.perfil y tqqq_vivo.niveles_tqqq (laboratorio/tres/tqqq/tqqq_vivo.py 1.0):
//   perfil: GEX por strike del vencimiento MAS CERCANO, con los dias contados desde la hora del DATO (vd = dias - (t_dato - generado)/86400),
//           entra 0 <= d <= min(vd >= 0) + 0.01 (sin el max(1, ...) del horizonte "Hoy"), T = max(d, 1/1440)/365, Black-Scholes r 0.0375,
//           S = spot_idx de la foto (NO se reprecia con NQ), esc = 100 S^2 0.01; calls +, puts -; por volumen y por OI.
//   niveles (en strikes de TQQQ, q = |K - S| <= 5 % S):
//     T_DOMS_vol  las 2 de mayor |gv| (> 0), orden descendente (empates: orden de K, como el sort de pandas con <= 16 filas)
//     T_MUROS_vol muro C = K del primer max(gvc) si > 0; muro P = K del primer min(gvp) si < 0
//     T_MUROS_oi  idem con goc / gop
//     T_ZERO_oi   perfil a <= 6 % de S, sin los go == 0, cruces de signo entre vecinos interpolados; el mas cercano a S (sin islas, sin C5)
//   Montos: g / 1e6 (M USD por 1 %), con signo; round 2 como tqqq_vivo 'actuales' (gex_musd). T_ZERO_oi sin monto (NaN).
// Puro: sin estado, sin E/S.
using System;
using System.Collections.Generic;

namespace PythiaGexCuatro.Familia
{
    /// <summary>Un nivel de TQQQ en SU strike (antes de convertir a NQ).</summary>
    public readonly struct TqqqNivelK
    {
        public readonly double K; public readonly string Rol; public readonly double GexM;
        public TqqqNivelK(double k, string rol, double gexM) { K = k; Rol = rol; GexM = gexM; }
    }

    /// <summary>Lo que sale de UNA foto de TQQQ (cache por foto): perfil del vencimiento mas cercano y las 4 series en strikes.</summary>
    public sealed class TqqqNivelesFoto
    {
        public static readonly string[] Orden = { "T_DOMS_vol", "T_MUROS_vol", "T_MUROS_oi", "T_ZERO_oi" };
        public double Spot = double.NaN;
        public double VencD = double.NaN;               // dias al vencimiento mas cercano (desde la hora del dato); NaN si no hubo perfil
        public int NStrikes;                            // strikes del perfil
        /// <summary>Vacio si no hubo perfil o q quedo vacio (tqqq_vivo: {}); si no, las 4 claves (listas posiblemente vacias).</summary>
        public readonly Dictionary<string, TqqqNivelK[]> Series = new Dictionary<string, TqqqNivelK[]>(StringComparer.Ordinal);
    }

    public static class TqqqPerfil
    {
        public const double RADIO = 0.05;               // tqqq_vivo.RADIO
        public const double RADIO_ZERO = 0.06;

        public sealed class Perfil
        {
            public double[] K, Gvc, Gvp, Goc, Gop, Gv, Go;
            public double VencD;
        }

        /// <summary>tqqq_vivo.perfil(cad, S = spot, t_eval = t_dato, gen = generado). null si no hay filas, S &lt;= 0, ningun vd >= 0 o nada entra.</summary>
        public static Perfil Calcular(FotoCadena f)
        {
            if (f == null || f.Filas == null || f.Filas.Length == 0) return null;
            double S = f.Spot;
            if (!(S > 0)) return null;
            var dias = f.Dias ?? Array.Empty<double>();
            int nv = dias.Length;
            double corr = (f.DatoUtc - f.GeneradoUtc).TotalSeconds / 86400.0;
            var vd = new double[nv];
            double min = double.PositiveInfinity; bool hay = false;
            for (int v = 0; v < nv; v++)
            {
                vd[v] = dias[v] - corr;
                if (vd[v] >= 0) { hay = true; if (vd[v] < min) min = vd[v]; }
            }
            if (!hay) return null;
            double tope = min + 0.01;
            // filas que entran (en el orden de la cadena), despues agrupadas por K (groupby("K").sum(): K ascendente)
            var ks = new List<double>(); var a = new List<double[]>();
            foreach (var r in f.Filas)
            {
                int v = r.V;
                if (v < 0 || v >= nv) continue;
                double d = vd[v];
                if (!(d >= 0) || !(d <= tope)) continue;
                double T = Math.Max(d, TqqqGamma.PISO_DIAS) / 365.0;
                double gc = TqqqGamma.BS(S, r.K, T, r.IvC), gp = TqqqGamma.BS(S, r.K, T, r.IvP);
                double esc = 100.0 * S * S * 0.01;
                double volC = Nz(r.VolC), volP = Nz(r.VolP), oiC = Nz(r.OiC), oiP = Nz(r.OiP);
                ks.Add(r.K);
                a.Add(new[] { gc * volC * esc, -gp * volP * esc, gc * oiC * esc, -gp * oiP * esc });
            }
            if (ks.Count == 0) return null;
            var idx = new int[ks.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            // orden total (K, indice): K ascendente y, dentro de un K repetido, el orden de aparicion (se suman en ese orden)
            Array.Sort(idx, (x, y) => { int c = ks[x].CompareTo(ks[y]); return c != 0 ? c : x.CompareTo(y); });
            var K = new List<double>(); var gvc = new List<double>(); var gvp = new List<double>(); var goc = new List<double>(); var gop = new List<double>();
            foreach (int i in idx)
            {
                double k = ks[i]; var x = a[i];
                if (K.Count > 0 && K[K.Count - 1] == k)
                {
                    int j = K.Count - 1;
                    gvc[j] += x[0]; gvp[j] += x[1]; goc[j] += x[2]; gop[j] += x[3];
                }
                else { K.Add(k); gvc.Add(x[0]); gvp.Add(x[1]); goc.Add(x[2]); gop.Add(x[3]); }
            }
            int n = K.Count;
            var p = new Perfil { K = K.ToArray(), Gvc = gvc.ToArray(), Gvp = gvp.ToArray(), Goc = goc.ToArray(), Gop = gop.ToArray(),
                                 Gv = new double[n], Go = new double[n], VencD = min };
            for (int i = 0; i < n; i++) { p.Gv[i] = p.Gvc[i] + p.Gvp[i]; p.Go[i] = p.Goc[i] + p.Gop[i]; }
            return p;
        }

        private static double Nz(double v) => double.IsNaN(v) ? 0.0 : v;   // nulo de CBOE = 0

        /// <summary>tqqq_vivo.niveles_tqqq(p, S) -> serie -> [(K, rol, gex M)] en strikes de TQQQ.</summary>
        public static TqqqNivelesFoto Niveles(FotoCadena f)
        {
            var o = new TqqqNivelesFoto { Spot = f?.Spot ?? double.NaN };
            var p = Calcular(f);
            if (p == null) return o;
            o.VencD = p.VencD; o.NStrikes = p.K.Length;
            double S = f.Spot;
            var q = new List<int>();
            for (int i = 0; i < p.K.Length; i++) if (Math.Abs(p.K[i] - S) <= RADIO * S) q.Add(i);
            if (q.Count == 0) return o;

            // T_DOMS_vol: sort_values(|gv|, descendente).head(2), ab > 0
            var orden = q.ToArray();
            Array.Sort(orden, (x, y) => { int c = Math.Abs(p.Gv[y]).CompareTo(Math.Abs(p.Gv[x])); return c != 0 ? c : x.CompareTo(y); });
            var doms = new List<TqqqNivelK>();
            for (int j = 0; j < Math.Min(2, orden.Length); j++)
            {
                int i = orden[j];
                if (Math.Abs(p.Gv[i]) > 0) doms.Add(new TqqqNivelK(p.K[i], "dom", TqqqNum.PyRound(p.Gv[i] / 1e6, 2)));
            }
            o.Series["T_DOMS_vol"] = doms.ToArray();
            o.Series["T_MUROS_vol"] = Muros(p, q, p.Gvc, p.Gvp);
            o.Series["T_MUROS_oi"] = Muros(p, q, p.Goc, p.Gop);

            // T_ZERO_oi: pz = |K - S| <= 6 % S, sin go == 0, cruces entre vecinos, el mas cercano a S (el primero si empatan)
            var kz = new List<double>(); var gz = new List<double>();
            for (int i = 0; i < p.K.Length; i++)
                if (Math.Abs(p.K[i] - S) <= RADIO_ZERO * S && p.Go[i] != 0) { kz.Add(p.K[i]); gz.Add(p.Go[i]); }
            double mejor = double.NaN, dMejor = double.PositiveInfinity;
            for (int i = 0; i + 1 < kz.Count; i++)
            {
                if (!(gz[i] * gz[i + 1] < 0)) continue;
                double z = kz[i] + (kz[i + 1] - kz[i]) * (0 - gz[i]) / (gz[i + 1] - gz[i]);
                double dz = Math.Abs(z - S);
                if (dz < dMejor) { dMejor = dz; mejor = z; }
            }
            o.Series["T_ZERO_oi"] = double.IsNaN(mejor) ? Array.Empty<TqqqNivelK>() : new[] { new TqqqNivelK(mejor, "zero", double.NaN) };
            return o;
        }

        /// <summary>muro C = K[idxmax(c)] si max(c) > 0; muro P = K[idxmin(p)] si min(p) &lt; 0 (idxmax/idxmin: primera ocurrencia en q).</summary>
        private static TqqqNivelK[] Muros(Perfil pf, List<int> q, double[] c, double[] p)
        {
            var l = new List<TqqqNivelK>(2);
            int iMax = -1, iMin = -1;
            foreach (int i in q)
            {
                if (iMax < 0 || c[i] > c[iMax]) iMax = i;
                if (iMin < 0 || p[i] < p[iMin]) iMin = i;
            }
            if (iMax >= 0 && c[iMax] > 0) l.Add(new TqqqNivelK(pf.K[iMax], "muro C", TqqqNum.PyRound(c[iMax] / 1e6, 2)));
            if (iMin >= 0 && p[iMin] < 0) l.Add(new TqqqNivelK(pf.K[iMin], "muro P", TqqqNum.PyRound(p[iMin] / 1e6, 2)));
            return l.ToArray();
        }
    }
}
