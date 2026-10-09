// NumFam.cs — PythiaGex 4.1, modulo Familia, CODIGO COMUN (B3a, 08-10-2026).
// Aritmetica que tiene que dar LO MISMO que Python/numpy en la vista previa (preview_niveles.py + backtest_familia.py):
//   * PyRound(x, n): el round(x, n) de Python (redondeo correcto del valor binario EXACTO, mitad al par). Math.Round(x, n) de .NET escala
//     por 10^n en doble y puede dar otro digito (ej. 0,03125 -> .NET 0,0313; Python 0,0312). Se usa para round(dias, 4) de viva3,
//     round(p, 2) de los precios y round(gex, 1) de los montos.
//   * SumaNumpy: la suma por pares de numpy (pairwise_sum, bloques de 8, corte a 128) — la que usa np.sum(axis=1) del zero estandar.
//     Verificado contra numpy 2.4.3: 900 de 900 sumas identicas al bit (la secuencial acierta 98 de 900).
//   * Mediana: np.median (cantidad par = promedio de los dos del medio).  Robusta: backtest_familia.robusta.
// Sin E/S, sin estado: se puede llamar desde cualquier hilo.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace PythiaGexCuatro.Familia
{
    public static class NumFam
    {
        // ------------------------------------------------------------------ round de Python

        /// <summary>round(x, n) de Python 3 para n >= 0: redondeo del valor binario exacto de x a n decimales, empate al par, y vuelta al
        /// doble mas cercano del decimal resultante. NaN/Inf pasan igual.</summary>
        public static double PyRound(double x, int n)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || x == 0.0) return x;
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
            // atajo exacto: si x * 10^n ya es entero exacto y chico, no hay nada que redondear
            long bits = BitConverter.DoubleToInt64Bits(x);
            bool neg = bits < 0;
            int expo = (int)((bits >> 52) & 0x7FF);
            long man = bits & 0xFFFFFFFFFFFFFL;
            if (expo == 0) expo = 1; else man |= 1L << 52;
            expo -= 1075;                                   // x = man * 2^expo
            BigInteger num = new BigInteger(man) * BigInteger.Pow(10, n);
            BigInteger den = BigInteger.One;
            if (expo >= 0) num <<= expo; else den <<= -expo;
            BigInteger q = BigInteger.DivRem(num, den, out BigInteger r);
            BigInteger dos = r << 1;
            int c = dos.CompareTo(den);
            if (c > 0 || (c == 0 && !q.IsEven)) q += 1;
            if (q.IsZero) return neg ? -0.0 : 0.0;
            string s = (neg ? "-" : "") + q.ToString(CultureInfo.InvariantCulture) + "E-" + n.ToString(CultureInfo.InvariantCulture);
            return double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        /// <summary>round(x) de Python sin decimales (entero, mitad al par) — igual a Math.Round(x, ToEven) en doble (no hay escala).</summary>
        public static double PyRound0(double x) => Math.Round(x, MidpointRounding.ToEven);

        // ------------------------------------------------------------------ sumas como numpy

        /// <summary>np.sum de un vector contiguo (pairwise_sum de numpy: &lt; 8 secuencial; &lt;= 128 ocho acumuladores; si no, mitades
        /// multiplos de 8). Igual al bit que numpy 2.x en float64.</summary>
        public static double SumaNumpy(double[] a, int ini, int n)
        {
            if (n < 8)
            {
                double res = 0.0;
                for (int i = 0; i < n; i++) res += a[ini + i];
                return res;
            }
            if (n <= 128)
            {
                double r0 = a[ini], r1 = a[ini + 1], r2 = a[ini + 2], r3 = a[ini + 3], r4 = a[ini + 4], r5 = a[ini + 5], r6 = a[ini + 6], r7 = a[ini + 7];
                int i = 8, lim = n - (n % 8);
                for (; i < lim; i += 8)
                {
                    r0 += a[ini + i]; r1 += a[ini + i + 1]; r2 += a[ini + i + 2]; r3 += a[ini + i + 3];
                    r4 += a[ini + i + 4]; r5 += a[ini + i + 5]; r6 += a[ini + i + 6]; r7 += a[ini + i + 7];
                }
                double res = ((r0 + r1) + (r2 + r3)) + ((r4 + r5) + (r6 + r7));
                for (; i < n; i++) res += a[ini + i];
                return res;
            }
            int n2 = n / 2; n2 -= n2 % 8;
            return SumaNumpy(a, ini, n2) + SumaNumpy(a, ini + n2, n - n2);
        }

        public static double SumaNumpy(double[] a) => SumaNumpy(a, 0, a.Length);

        // ------------------------------------------------------------------ medianas

        /// <summary>np.median: ordena; par = (a + b) / 2. Ignora nada (los NaN los saca quien llama). NaN si vacio.</summary>
        public static double Mediana(IReadOnlyList<double> x)
        {
            int n = x?.Count ?? 0;
            if (n == 0) return double.NaN;
            var o = new double[n];
            for (int i = 0; i < n; i++) o[i] = x[i];
            Array.Sort(o);
            return (n % 2 == 1) ? o[n / 2] : (o[n / 2 - 1] + o[n / 2]) / 2.0;
        }

        /// <summary>backtest_familia.robusta(x, piso): saca NaN; med = mediana; mad = mediana(|x - med|); conserva |x - med| &lt;= max(3 mad, piso);
        /// devuelve (mediana de los conservados, cuantos). Sin conservados: (med, n). Vacio: (NaN, 0).</summary>
        public static (double V, int N) Robusta(IEnumerable<double> xs, double piso)
        {
            var x = new List<double>();
            if (xs != null) foreach (var v in xs) if (!double.IsNaN(v)) x.Add(v);
            if (x.Count == 0) return (double.NaN, 0);
            double med = Mediana(x);
            var d = new double[x.Count];
            for (int i = 0; i < x.Count; i++) d[i] = Math.Abs(x[i] - med);
            double mad = Mediana(d);
            double lim = Math.Max(3.0 * mad, piso);
            var b = new List<double>(x.Count);
            for (int i = 0; i < x.Count; i++) if (Math.Abs(x[i] - med) <= lim) b.Add(x[i]);
            return b.Count > 0 ? (Mediana(b), b.Count) : (med, x.Count);
        }

        // ------------------------------------------------------------------ busquedas (np.searchsorted side="right" - 1)

        /// <summary>Ultimo indice i con a[i] &lt;= v en un arreglo ordenado ascendente (searchsorted right - 1). -1 si ninguno.</summary>
        public static int UltimoMenorIgual(IReadOnlyList<long> a, long v)
        {
            int lo = 0, hi = a.Count;                     // primer indice con a[i] > v
            while (lo < hi) { int m = (lo + hi) >> 1; if (a[m] <= v) lo = m + 1; else hi = m; }
            return lo - 1;
        }

        public static int UltimoMenorIgual(IReadOnlyList<double> a, double v)
        {
            int lo = 0, hi = a.Count;
            while (lo < hi) { int m = (lo + hi) >> 1; if (a[m] <= v) lo = m + 1; else hi = m; }
            return lo - 1;
        }

        /// <summary>Formato invariante corto para logs y rotulos ("0.##").</summary>
        public static string F(double v, string fmt = "0.##") => double.IsNaN(v) ? "NaN" : v.ToString(fmt, CultureInfo.InvariantCulture);
    }
}
