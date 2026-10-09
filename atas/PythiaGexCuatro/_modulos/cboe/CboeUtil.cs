// CboeUtil.cs — PythiaGex 4.1, modulo cboe (B2). Sin referencias a ATAS.
// Lo que hace falta para que el C# de EXACTAMENTE lo mismo que el Python de produccion (pythiagex/cadena_atas.py, base.py,
// archivar_cadena.py): el round() de Python (mitad al par sobre el valor binario exacto), el repr() de los float (para que la
// linea flaca salga igual byte a byte), la hora de Nueva York y el log propio.
using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace PythiaGexCuatro.Cboe
{
    /// <summary>round() y repr() de Python 3 para double.</summary>
    public static class PyCboe
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly double[] Pow10 = { 1, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15 };

        /// <summary>round(x, nd) de Python: decimal correctamente redondeado (mitad al par sobre el valor binario EXACTO) y vuelta a double.
        /// Camino rapido cuando la direccion es segura; si no, cuenta exacta con BigInteger.</summary>
        public static double Round(double x, int nd)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || x == 0) return x;
            if (nd < 0 || nd > 15) return RoundExacto(x, nd);
            double p = Pow10[nd];
            double y = x * p;
            if (Math.Abs(y) < 4.0e15)
            {
                double fl = Math.Floor(y);
                double fr = y - fl;                                 // exacto para |y| < 2^52
                double tol = Math.Abs(y) * 4e-16 + 1e-300;          // error de x*p <= medio ulp(y)
                if (Math.Abs(fr - 0.5) > tol && fr > tol && 1.0 - fr > tol)
                {
                    double q = fr < 0.5 ? fl : fl + 1.0;
                    if (q == 0) return x < 0 ? -0.0 : 0.0;
                    return q / p;                                   // q y p exactos: la division IEEE es el strtod de "q e-nd"
                }
            }
            return RoundExacto(x, nd);
        }

        private static double RoundExacto(double x, int nd)
        {
            long bits = BitConverter.DoubleToInt64Bits(x);
            bool neg = bits < 0;
            int exp = (int)((bits >> 52) & 0x7FF);
            long man = bits & 0xFFFFFFFFFFFFFL;
            if (exp == 0) exp = 1; else man |= 1L << 52;
            exp -= 1075;                                            // |x| = man * 2^exp
            BigInteger num = man, den = BigInteger.One;
            if (exp > 0) num <<= exp; else den <<= -exp;
            if (nd >= 0) num *= BigInteger.Pow(10, nd); else den *= BigInteger.Pow(10, -nd);
            BigInteger q = BigInteger.DivRem(num, den, out BigInteger r);
            int c = (r * 2).CompareTo(den);
            if (c > 0 || (c == 0 && !q.IsEven)) q += 1;
            if (q.IsZero) return neg ? -0.0 : 0.0;
            double v = double.Parse(q.ToString(Inv) + "E" + (-nd).ToString(Inv), NumberStyles.Float, Inv);
            return neg ? -v : v;
        }

        /// <summary>repr(float) de Python: los digitos mas cortos que vuelven al mismo double; notacion fija si -4 &lt; decpt &lt;= 16
        /// (con ".0" si es entero), si no exponencial "1e-05" / "1.5e+16".</summary>
        public static string Repr(double x)
        {
            if (double.IsNaN(x)) return "NaN";
            if (double.IsPositiveInfinity(x)) return "Infinity";
            if (double.IsNegativeInfinity(x)) return "-Infinity";
            if (x == 0) return (BitConverter.DoubleToInt64Bits(x) < 0) ? "-0.0" : "0.0";
            string r = x.ToString("R", Inv);                         // .NET Core 3+: el mas corto que vuelve al mismo double
            bool neg = r[0] == '-';
            if (neg) r = r.Substring(1);
            int e = 0;
            int iE = r.IndexOfAny(new[] { 'E', 'e' });
            if (iE >= 0) { e = int.Parse(r.Substring(iE + 1), NumberStyles.AllowLeadingSign, Inv); r = r.Substring(0, iE); }
            int iP = r.IndexOf('.');
            string ent = iP >= 0 ? r.Substring(0, iP) : r, fra = iP >= 0 ? r.Substring(iP + 1) : "";
            string dig = ent + fra;
            int decpt = ent.Length + e;                              // valor = 0.dig x 10^decpt
            int lz = 0; while (lz < dig.Length - 1 && dig[lz] == '0') lz++;
            dig = dig.Substring(lz); decpt -= lz;
            dig = dig.TrimEnd('0'); if (dig.Length == 0) { dig = "0"; }
            var sb = new StringBuilder(32);
            if (neg) sb.Append('-');
            if (decpt <= -4 || decpt > 16)
            {
                sb.Append(dig[0]);
                if (dig.Length > 1) { sb.Append('.'); sb.Append(dig, 1, dig.Length - 1); }
                int ex = decpt - 1;
                sb.Append('e'); sb.Append(ex < 0 ? '-' : '+');
                int a = Math.Abs(ex);
                if (a < 10) sb.Append('0');
                sb.Append(a.ToString(Inv));
            }
            else if (decpt <= 0)
            {
                sb.Append("0."); sb.Append('0', -decpt); sb.Append(dig);
            }
            else if (decpt < dig.Length)
            {
                sb.Append(dig, 0, decpt); sb.Append('.'); sb.Append(dig, decpt, dig.Length - decpt);
            }
            else
            {
                sb.Append(dig); sb.Append('0', decpt - dig.Length); sb.Append(".0");
            }
            return sb.ToString();
        }

        /// <summary>Un numero como lo escribiria json.dumps: entero si en Python era int, float con repr si era float.</summary>
        public static string Num(double v, bool esEntero)
        {
            if (double.IsNaN(v)) return "NaN";
            if (esEntero && Math.Abs(v) < 9.0e15 && v == Math.Floor(v)) return ((long)v).ToString(Inv);
            return Repr(v);
        }

        /// <summary>Texto JSON como json.dumps(ensure_ascii=False): solo se escapan comillas, barra y controles.</summary>
        public static void Str(StringBuilder sb, string s)
        {
            if (s == null) { sb.Append("null"); return; }
            sb.Append('"');
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4", Inv));
                        else sb.Append(ch);
                        break;
                }
            }
            sb.Append('"');
        }
    }

    /// <summary>Hora de Nueva York. Nunca ToUniversalTime/ToLocalTime: la zona se nombra explicita.</summary>
    public static class HoraNyCboe
    {
        private static readonly TimeZoneInfo Tz = Buscar();

        private static TimeZoneInfo Buscar()
        {
            foreach (var id in new[] { "Eastern Standard Time", "America/New_York" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
            }
            return null;
        }

        /// <summary>Horario de verano de EE.UU. por FECHA, igual que pythiagex/exposicion.dst_eeuu: 2do domingo de marzo &lt;= f &lt; 1er domingo de noviembre.</summary>
        public static bool DstEeuu(DateTime fecha)
        {
            var f = fecha.Date;
            return Domingo(f.Year, 3, 2) <= f && f < Domingo(f.Year, 11, 1);
        }

        private static DateTime Domingo(int anio, int mes, int cual)
        {
            var d = new DateTime(anio, mes, 1);
            int hasta = ((int)DayOfWeek.Sunday - (int)d.DayOfWeek + 7) % 7;
            return d.AddDays(hasta + 7 * (cual - 1));
        }

        /// <summary>16:00 de Nueva York de esa fecha, en UTC (20 en verano, 21 en invierno) — exposicion.hora_cierre_utc.</summary>
        public static DateTime CierreUtc(int anio, int mes, int dia)
        {
            var f = new DateTime(anio, mes, dia, 0, 0, 0, DateTimeKind.Utc);
            return f.AddHours(DstEeuu(f) ? 20 : 21);
        }

        /// <summary>Hora de NY sin zona -> UTC (Kind Utc).</summary>
        public static DateTime AUtc(DateTime ny)
        {
            var u = DateTime.SpecifyKind(ny, DateTimeKind.Unspecified);
            if (Tz != null)
            {
                try { return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(u, Tz), DateTimeKind.Utc); } catch { }
            }
            return DateTime.SpecifyKind(u.AddHours(DstEeuu(u) ? 4 : 5), DateTimeKind.Utc);   // respaldo (y hueco de marzo)
        }

        /// <summary>UTC -> hora de NY (Kind Unspecified).</summary>
        public static DateTime DeUtc(DateTime utc)
        {
            var u = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            if (Tz != null)
            {
                try { return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(u, Tz), DateTimeKind.Unspecified); } catch { }
            }
            var aprox = u.AddHours(-5);
            return DateTime.SpecifyKind(DstEeuu(aprox) ? u.AddHours(-4) : aprox, DateTimeKind.Unspecified);
        }

        /// <summary>"2026-10-08T15:59:59" (NY sin zona, como last_trade_time de CBOE) -> UTC. false si no se entiende.</summary>
        public static bool TextoNyAUtc(string s, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrEmpty(s)) return false;
            // datetime.fromisoformat de Python acepta 'T' o ' ' y fraccion; CBOE manda yyyy-MM-ddTHH:mm:ss
            if (!DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault, out var ny))
                return false;
            if (ny.Kind == DateTimeKind.Utc) { utc = ny; return true; }
            if (ny.Kind == DateTimeKind.Local) return false;     // traia zona: no es el formato esperado
            utc = AUtc(ny);
            return true;
        }
    }

    /// <summary>Log propio del descargador (%APPDATA%\ATAS\pythiagex4-cboe.log). ATAS se traga las excepciones: todo pasa por aca.</summary>
    public sealed class LogCboe
    {
        private readonly object _llave = new object();
        private readonly string _ruta;
        private readonly long _tope;

        public LogCboe(string ruta, long topeBytes = 10L * 1024 * 1024) { _ruta = ruta; _tope = topeBytes; }

        public string Ruta => _ruta;

        public void Linea(string m)
        {
            if (string.IsNullOrEmpty(_ruta)) return;
            try
            {
                lock (_llave)
                {
                    var dir = Path.GetDirectoryName(_ruta);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    try
                    {
                        var fi = new FileInfo(_ruta);
                        if (fi.Exists && fi.Length > _tope) File.Move(_ruta, _ruta + ".1", true);   // un respaldo, no crece sin fin
                    }
                    catch { }
                    File.AppendAllText(_ruta, DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'  ", CultureInfo.InvariantCulture) + m + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch { }
        }
    }
}
