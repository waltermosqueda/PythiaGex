// TqqqUtil.cs — PythiaGex 4.1, modulo TQQQ (B4), 08-10-2026.
// Aritmetica igual a Python/numpy (round de Python, np.median, tq_comun.robusta, np.polyfit grado 1), la gamma de Black-Scholes de
// nucleo.gamma_bs (r 0.0375, sin dividendo) y la hora de Nueva York (feriados y medios dias de la bolsa).
// Autocontenido a proposito: el modulo compila solo con Contratos.cs (nombres con prefijo Tqqq para no chocar con comun/ de B3a).
// Sin E/S, sin estado mutable: se puede llamar desde cualquier hilo.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace PythiaGexCuatro.Familia
{
    public static class TqqqNum
    {
        /// <summary>round(x, n) de Python 3 (n >= 0): redondeo correcto del valor binario exacto, mitad al par. Math.Round(x, n) de .NET escala
        /// en doble y puede dar otro digito (0,03125 -> .NET 0,0313; Python 0,0312). Mismo algoritmo que NumFam.PyRound de comun/.</summary>
        public static double PyRound(double x, int n)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || x == 0.0) return x;
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
            long bits = BitConverter.DoubleToInt64Bits(x);
            bool neg = bits < 0;
            int expo = (int)((bits >> 52) & 0x7FF);
            long man = bits & 0xFFFFFFFFFFFFFL;
            if (expo == 0) expo = 1; else man |= 1L << 52;
            expo -= 1075;                                   // |x| = man * 2^expo
            BigInteger num = new BigInteger(man) * BigInteger.Pow(10, n);
            BigInteger den = BigInteger.One;
            if (expo >= 0) num <<= expo; else den <<= -expo;
            BigInteger q = BigInteger.DivRem(num, den, out BigInteger r);
            int c = (r << 1).CompareTo(den);
            if (c > 0 || (c == 0 && !q.IsEven)) q += 1;
            if (q.IsZero) return neg ? -0.0 : 0.0;
            string s = (neg ? "-" : "") + q.ToString(CultureInfo.InvariantCulture) + "E-" + n.ToString(CultureInfo.InvariantCulture);
            return double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        /// <summary>np.median de los n primeros de o (los ORDENA en el lugar). Par = (a + b) / 2. NaN si n = 0.</summary>
        public static double MedianaOrdenando(double[] o, int n)
        {
            if (n <= 0) return double.NaN;
            Array.Sort(o, 0, n);
            return (n % 2 == 1) ? o[n / 2] : (o[n / 2 - 1] + o[n / 2]) / 2.0;
        }

        /// <summary>tq_comun.robusta(x, piso): saca NaN; med = mediana; mad = mediana(|x - med|); conserva |x - med| &lt;= max(3 mad, piso);
        /// devuelve (mediana de los conservados, cuantos). Sin conservados: (med, n). Vacio: (NaN, 0).</summary>
        public static (double V, int N) Robusta(IReadOnlyList<double> xs, double piso)
        {
            int m = xs?.Count ?? 0;
            var x = new double[m]; int n = 0;
            for (int i = 0; i < m; i++) { double v = xs[i]; if (!double.IsNaN(v)) x[n++] = v; }
            if (n == 0) return (double.NaN, 0);
            var o = new double[n]; Array.Copy(x, o, n);
            double med = MedianaOrdenando(o, n);
            var d = new double[n];
            for (int i = 0; i < n; i++) d[i] = Math.Abs(x[i] - med);
            double mad = MedianaOrdenando(d, n);
            double lim = Math.Max(3.0 * mad, piso);
            var b = new double[n]; int nb = 0;
            for (int i = 0; i < n; i++) if (Math.Abs(x[i] - med) <= lim) b[nb++] = x[i];
            return nb > 0 ? (MedianaOrdenando(b, nb), nb) : (med, n);
        }

        /// <summary>Pendiente de la recta por minimos cuadrados (np.polyfit(x, y, 1)[0]). NaN con menos de 2 puntos o x constante.</summary>
        public static double Pendiente(IReadOnlyList<double> x, IReadOnlyList<double> y)
        {
            int n = Math.Min(x?.Count ?? 0, y?.Count ?? 0);
            if (n < 2) return double.NaN;
            double mx = 0, my = 0;
            for (int i = 0; i < n; i++) { mx += x[i]; my += y[i]; }
            mx /= n; my /= n;
            double sxy = 0, sxx = 0;
            for (int i = 0; i < n; i++) { double dx = x[i] - mx; sxy += dx * (y[i] - my); sxx += dx * dx; }
            return sxx > 0 ? sxy / sxx : double.NaN;
        }

        /// <summary>Busqueda binaria: ultimo indice i con a[i] &lt;= v en un arreglo ascendente (np.searchsorted side="right" - 1). -1 si ninguno.</summary>
        public static int UltimoMenorIgual(long[] a, int n, long v)
        {
            int lo = 0, hi = n;
            while (lo < hi) { int m = (lo + hi) >> 1; if (a[m] <= v) lo = m + 1; else hi = m; }
            return lo - 1;
        }

        public static string F(double v, string fmt = "0.##") => double.IsNaN(v) ? "NaN" : v.ToString(fmt, CultureInfo.InvariantCulture);
    }

    /// <summary>nucleo.gamma_bs (Black-Scholes, r = 0.0375, sin dividendo), mismo orden de operaciones que numpy.</summary>
    public static class TqqqGamma
    {
        public const double TASA = 0.0375;
        public const double PISO_DIAS = 1.0 / 1440.0;
        private static readonly double INV_SQRT_2PI = 1.0 / Math.Sqrt(2.0 * Math.PI);

        public static double BS(double S, double K, double T, double iv, double r = TASA)
        {
            if (!(S > 0) || !(K > 0) || !(T > 0) || !(iv > 0)) return 0.0;
            double v = iv * Math.Sqrt(T);
            double d1 = (Math.Log(S / K) + (r + 0.5 * iv * iv) * T) / v;
            return Math.Exp(-0.5 * d1 * d1) * INV_SQRT_2PI / (S * v);
        }
    }

    /// <summary>Hora de Nueva York y calendario de la bolsa (NYSE/Nasdaq) para la rueda de TQQQ: feriados y medios dias (13:00 NY).</summary>
    public static class TqqqHora
    {
        private static readonly long TicksEpoca = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        private static TimeZoneInfo _ny;
        public static TimeZoneInfo Ny
        {
            get
            {
                var z = _ny; if (z != null) return z;
                foreach (var id in new[] { "Eastern Standard Time", "America/New_York" })
                    try { z = TimeZoneInfo.FindSystemTimeZoneById(id); break; } catch { }
                _ny = z ?? TimeZoneInfo.Utc;
                return _ny;
            }
        }

        public static long Ms(DateTime utc) => (utc.Ticks - TicksEpoca) / TimeSpan.TicksPerMillisecond;
        public static DateTime DeMs(long ms) => new DateTime(TicksEpoca + ms * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
        public static DateTime UtcANy(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Ny);
        public static DateTime NyAUtc(DateTime ny) => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(ny, DateTimeKind.Unspecified), Ny), DateTimeKind.Utc);
        public static string Fecha(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>Feriados de la bolsa de EE.UU. (reglas de NYSE: Año Nuevo sin observado en sabado, MLK, Presidentes, Viernes Santo,
        /// Memorial, Juneteenth, Independencia, Trabajo, Accion de Gracias, Navidad; sabado -> viernes, domingo -> lunes).</summary>
        public static bool EsFeriado(DateTime fecha)
        {
            var d = fecha.Date; int y = d.Year;
            if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) return false;
            foreach (var f in Feriados(y)) if (f == d) return true;
            return false;                                   // Año Nuevo en sabado: NYSE no lo observa el viernes 31-12
        }

        public static bool EsHabil(DateTime fecha) => fecha.DayOfWeek != DayOfWeek.Saturday && fecha.DayOfWeek != DayOfWeek.Sunday && !EsFeriado(fecha);

        /// <summary>El dia habil anterior a fecha (fecha excluida).</summary>
        public static DateTime HabilAnterior(DateTime fecha)
        {
            var d = fecha.Date.AddDays(-1);
            for (int i = 0; i < 10 && !EsHabil(d); i++) d = d.AddDays(-1);
            return d;
        }

        /// <summary>Hora NY del cierre de la rueda: 16:00, o 13:00 en los medios dias (viernes despues de Accion de Gracias, 24-12 y 03-07
        /// habiles). SIN VERIFICAR contra el calendario oficial de cada año: la regla de 03-07 varia.</summary>
        public static TimeSpan CierreNy(DateTime fecha)
        {
            var d = fecha.Date;
            if (!EsHabil(d)) return new TimeSpan(16, 0, 0);
            var acc = NesimoDia(d.Year, 11, DayOfWeek.Thursday, 4);
            if (d == acc.AddDays(1)) return new TimeSpan(13, 0, 0);
            if (d.Month == 12 && d.Day == 24) return new TimeSpan(13, 0, 0);
            if (d.Month == 7 && d.Day == 3) return new TimeSpan(13, 0, 0);
            return new TimeSpan(16, 0, 0);
        }

        public static DateTime CierreUtc(DateTime fecha) => NyAUtc(fecha.Date + CierreNy(fecha));

        private static IEnumerable<DateTime> Feriados(int y)
        {
            DateTime Obs(DateTime d) => d.DayOfWeek == DayOfWeek.Saturday ? d.AddDays(-1) : d.DayOfWeek == DayOfWeek.Sunday ? d.AddDays(1) : d;
            var ano = new DateTime(y, 1, 1);
            if (ano.DayOfWeek == DayOfWeek.Sunday) yield return ano.AddDays(1); else if (ano.DayOfWeek != DayOfWeek.Saturday) yield return ano;
            yield return NesimoDia(y, 1, DayOfWeek.Monday, 3);
            yield return NesimoDia(y, 2, DayOfWeek.Monday, 3);
            yield return Pascua(y).AddDays(-2);
            var mayo = new DateTime(y, 5, 31); while (mayo.DayOfWeek != DayOfWeek.Monday) mayo = mayo.AddDays(-1);
            yield return mayo;
            if (y >= 2022) yield return Obs(new DateTime(y, 6, 19));
            yield return Obs(new DateTime(y, 7, 4));
            yield return NesimoDia(y, 9, DayOfWeek.Monday, 1);
            yield return NesimoDia(y, 11, DayOfWeek.Thursday, 4);
            yield return Obs(new DateTime(y, 12, 25));
        }

        private static DateTime NesimoDia(int y, int mes, DayOfWeek dw, int n)
        {
            var d = new DateTime(y, mes, 1);
            while (d.DayOfWeek != dw) d = d.AddDays(1);
            return d.AddDays(7 * (n - 1));
        }

        private static DateTime Pascua(int y)
        {   // algoritmo anonimo gregoriano
            int a = y % 19, b = y / 100, c = y % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
            int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7, m = (a + 11 * h + 22 * l) / 451;
            int mes = (h + l - 7 * m + 114) / 31, dia = ((h + l - 7 * m + 114) % 31) + 1;
            return new DateTime(y, mes, dia);
        }
    }
}
