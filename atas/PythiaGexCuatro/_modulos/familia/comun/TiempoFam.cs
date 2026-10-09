// TiempoFam.cs — PythiaGex 4.1, modulo Familia, CODIGO COMUN (B3a, 08-10-2026).
// Claves de tiempo de la vista previa y hora de Nueva York SIN horarios clavados en UTC (el 01-11 vuelve el horario de invierno).
//   * Clave de minuto = ms UTC / 60000 (Python: key = t_ns // 60e9). Vela m2 = floor(ms / 120000) * 120000 (ms de la APERTURA).
//   * Sesion de CME = fecha(t_utc + 2 h): de 22:00 UTC de la vispera (dia - 2 h) a 21:00 UTC del dia (dia + 21 h), fin excluido.
//   * Tiempos de ATAS: Kind Unspecified = UTC (medido): se usan los Ticks crudos, nunca ToUniversalTime.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PythiaGexCuatro.Familia
{
    public static class TiempoFam
    {
        public const long MS_MIN = 60_000L, MS_M2 = 120_000L;
        private const long TICKS_EPOCA = 621355968000000000L;      // 1970-01-01 en Ticks

        /// <summary>ms desde 1970 de un DateTime que YA esta en UTC (Kind Utc o Unspecified-que-es-UTC): ticks crudos.</summary>
        public static long Ms(DateTime utc) => (utc.Ticks - TICKS_EPOCA) / 10_000L;

        /// <summary>DateTime UTC (Kind Utc) de ms desde 1970.</summary>
        public static DateTime DeMs(long ms) => new DateTime(ms * 10_000L + TICKS_EPOCA, DateTimeKind.Utc);

        /// <summary>Clave de minuto (ms / 60000, piso). Python: t_ns // 60e9.</summary>
        public static long Clave(DateTime utc) => PisoDiv(Ms(utc), MS_MIN);

        public static DateTime DeClave(long clave) => DeMs(clave * MS_MIN);

        /// <summary>Apertura (ms) de la vela m2 que contiene ms.</summary>
        public static long AperturaM2(long ms) => PisoDiv(ms, MS_M2) * MS_M2;

        public static long PisoDiv(long a, long b) { long q = a / b; if ((a % b != 0) && ((a < 0) != (b < 0))) q--; return q; }

        // ------------------------------------------------------------------ sesion de CME
        /// <summary>"yyyy-MM-dd" de la sesion que contiene t (backtest_familia.sesion_de: (t + 2 h).fecha).</summary>
        public static string Sesion(DateTime utc) => utc.AddHours(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static DateTime DiaSesion(string dia) => DateTime.SpecifyKind(DateTime.ParseExact(dia, "yyyy-MM-dd", CultureInfo.InvariantCulture), DateTimeKind.Utc);
        /// <summary>22:00 UTC de la vispera (dia - 2 h), incluido.</summary>
        public static DateTime IniSesion(string dia) => DiaSesion(dia).AddHours(-2);
        /// <summary>21:00 UTC del dia (dia + 21 h). La grilla de minutos va hasta fin - 1 min.</summary>
        public static DateTime FinSesion(string dia) => DiaSesion(dia).AddHours(21);

        // ------------------------------------------------------------------ hora de Nueva York
        private static readonly TimeZoneInfo _ny = ZonaNy();
        private static TimeZoneInfo ZonaNy()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
            catch { try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); } catch { return null; } }
        }

        /// <summary>UTC -> hora de NY (Kind Unspecified). Sin zona instalada: UTC-4 (horario de verano).</summary>
        public static DateTime ANy(DateTime utc)
        {
            var u = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            if (_ny == null) return DateTime.SpecifyKind(u.AddHours(-4), DateTimeKind.Unspecified);
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(u, _ny), DateTimeKind.Unspecified);
        }

        /// <summary>Hora de NY sin zona -> UTC (Kind Utc). Como libros._ny_a_utc.</summary>
        public static DateTime NyAUtc(DateTime ny)
        {
            var n = DateTime.SpecifyKind(ny, DateTimeKind.Unspecified);
            if (_ny == null) return DateTime.SpecifyKind(n.AddHours(4), DateTimeKind.Utc);
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(n, _ny), DateTimeKind.Utc);
        }

        /// <summary>"HH:mm" de un DateTime (para las comparaciones de texto de Python: "01:55" &lt;= hm &lt; "13:30").</summary>
        public static string HM(DateTime d) => d.ToString("HH:mm", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ feriados de la bolsa de EE.UU. (para C2 "posferiado")
        /// <summary>
        /// Dias habiles (lunes a viernes) en que NYSE/CME-equity no tienen rueda completa por feriado: Año Nuevo, MLK, Presidentes, Viernes Santo,
        /// Memorial, Juneteenth, Independencia, Trabajo, Accion de Gracias, Navidad (con los corrimientos de sabado -> viernes y domingo -> lunes).
        /// La vista previa tiene escrito fijo solo POSFERIADO = ("2026-09-08",) (el dia despues de Labor Day): esta regla lo reproduce.
        /// SUPUESTO (no medido): que el OI de Rithmic llegue actualizado tambien despues de los demas feriados.
        /// </summary>
        public static bool EsFeriadoBolsa(DateTime fecha)
        {
            var d = fecha.Date; int y = d.Year;
            if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) return false;
            foreach (var f in FeriadosDelAnio(y)) if (f == d) return true;
            if (d.Month == 12 && d.Day == 31) { foreach (var f in FeriadosDelAnio(y + 1)) if (f == d) return true; }
            return false;
        }

        private static readonly Dictionary<int, List<DateTime>> _cacheFeriados = new Dictionary<int, List<DateTime>>();
        private static readonly object _llaveFeriados = new object();

        public static IReadOnlyList<DateTime> FeriadosDelAnio(int y)
        {
            lock (_llaveFeriados)
            {
                if (_cacheFeriados.TryGetValue(y, out var l)) return l;
                l = new List<DateTime>
                {
                    Observado(new DateTime(y, 1, 1)),
                    NesimoDia(y, 1, DayOfWeek.Monday, 3),
                    NesimoDia(y, 2, DayOfWeek.Monday, 3),
                    Pascua(y).AddDays(-2),
                    UltimoDia(y, 5, DayOfWeek.Monday),
                    Observado(new DateTime(y, 6, 19)),
                    Observado(new DateTime(y, 7, 4)),
                    NesimoDia(y, 9, DayOfWeek.Monday, 1),
                    NesimoDia(y, 11, DayOfWeek.Thursday, 4),
                    Observado(new DateTime(y, 12, 25)),
                };
                _cacheFeriados[y] = l;
                return l;
            }
        }

        /// <summary>La sesion `dia` es la primera rueda despues de un feriado habil (el dia habil anterior fue feriado). Lunes no cuenta aca.</summary>
        public static bool EsPosferiado(string dia)
        {
            var d = DiaSesion(dia).Date;
            var a = d.AddDays(-1);
            while (a.DayOfWeek == DayOfWeek.Saturday || a.DayOfWeek == DayOfWeek.Sunday) a = a.AddDays(-1);
            return EsFeriadoBolsa(a);
        }

        private static DateTime Observado(DateTime d) => d.DayOfWeek == DayOfWeek.Saturday ? d.AddDays(-1) : d.DayOfWeek == DayOfWeek.Sunday ? d.AddDays(1) : d;
        private static DateTime NesimoDia(int y, int m, DayOfWeek dw, int n)
        {
            var d = new DateTime(y, m, 1);
            while (d.DayOfWeek != dw) d = d.AddDays(1);
            return d.AddDays(7 * (n - 1));
        }
        private static DateTime UltimoDia(int y, int m, DayOfWeek dw)
        {
            var d = new DateTime(y, m, DateTime.DaysInMonth(y, m));
            while (d.DayOfWeek != dw) d = d.AddDays(-1);
            return d;
        }
        /// <summary>Domingo de Pascua (algoritmo de Meeus/Jones/Butcher, calendario gregoriano).</summary>
        private static DateTime Pascua(int y)
        {
            int a = y % 19, b = y / 100, c = y % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
            int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7, m = (a + 11 * h + 22 * l) / 451;
            int mes = (h + l - 7 * m + 114) / 31, dia = ((h + l - 7 * m + 114) % 31) + 1;
            return new DateTime(y, mes, dia);
        }
    }
}
