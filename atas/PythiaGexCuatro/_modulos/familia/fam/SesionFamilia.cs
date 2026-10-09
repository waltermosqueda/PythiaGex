// SesionFamilia.cs — PythiaGex 4.1, modulo fam (B3d), 08-10-2026.
// La sesion del motor. Dos modos (DISENO_4_1 §A.1):
//   Paridad (la vista previa): dia = fecha(t_utc + 2 h); [dia - 2 h, dia + 21 h) en UTC fijo; la rueda de TQQQ desde las 13:30 UTC.
//   Corregida (default del indicador): la sesion de CME en hora de Nueva York, 18:00 NY de la vispera a 17:00 NY; rueda desde las 09:30 NY.
//   En horario de verano de EE.UU. las dos dan lo mismo; desde el 01-11 la paridad se corre una hora (el bug del Python).
// Sin referencias a ATAS. NY con TimeZoneInfo "Eastern Standard Time" (Windows) o "America/New_York".
using System;
using System.Globalization;

namespace PythiaGexCuatro.Familia
{
    public sealed class SesionFamilia
    {
        public string Dia;                 // yyyy-MM-dd (la fecha de la rueda)
        public DateTime IniUtc, FinUtc;    // ventana de minutos [ini, fin)
        public DateTime SiguienteIniUtc;   // donde empieza la proxima (hasta ahi vive esta: historia de TQQQ despues del cierre)
        public DateTime RuedaIniUtc;       // apertura de la rueda (desde donde se piden las velas de TQQQ)
        public long IniClave => IniUtc.Ticks / TimeSpan.TicksPerMinute - EpocaMin;
        public long FinClave => FinUtc.Ticks / TimeSpan.TicksPerMinute - EpocaMin;
        public bool Corregida;

        private static readonly long EpocaMin = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks / TimeSpan.TicksPerMinute;
        private static TimeZoneInfo _ny;
        public static TimeZoneInfo Ny
        {
            get
            {
                if (_ny != null) return _ny;
                foreach (var id in new[] { "Eastern Standard Time", "America/New_York" })
                    try { _ny = TimeZoneInfo.FindSystemTimeZoneById(id); return _ny; } catch { }
                _ny = TimeZoneInfo.Utc; return _ny;
            }
        }

        public static DateTime NyAUtc(DateTime ny) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(ny, DateTimeKind.Unspecified), Ny);
        public static DateTime UtcANy(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Ny);

        /// <summary>ms UTC desde 1970 (las claves del contrato: minuto = ms / 60000, vela m2 = floor(ms / 120000) * 120000).</summary>
        public static long Ms(DateTime utc) => (utc.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks) / TimeSpan.TicksPerMillisecond;
        public static DateTime DeMs(long ms) => new DateTime(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks + ms * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
        public static DateTime DeClave(long clave) => DeMs(clave * 60000L);

        public static SesionFamilia De(DateTime ahoraUtc, bool corregida)
        {
            ahoraUtc = DateTime.SpecifyKind(ahoraUtc, DateTimeKind.Utc);
            var s = new SesionFamilia { Corregida = corregida };
            if (!corregida)
            {   // backtest_familia.sesion_de / preview_niveles.Motor: dia = (t + 2 h).date
                var d = ahoraUtc.AddHours(2).Date;
                s.Dia = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                s.IniUtc = DateTime.SpecifyKind(d.AddHours(-2), DateTimeKind.Utc);
                s.FinUtc = DateTime.SpecifyKind(d.AddHours(21), DateTimeKind.Utc);
                s.SiguienteIniUtc = DateTime.SpecifyKind(d.AddHours(22), DateTimeKind.Utc);
                s.RuedaIniUtc = DateTime.SpecifyKind(d.AddHours(13).AddMinutes(30), DateTimeKind.Utc);
            }
            else
            {   // CME: 18:00 NY de la vispera -> 17:00 NY; el dia es la fecha NY de (t + 6 h)
                var ny = UtcANy(ahoraUtc);
                var d = ny.AddHours(6).Date;
                s.Dia = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                s.IniUtc = NyAUtc(d.AddDays(-1).AddHours(18));
                s.FinUtc = NyAUtc(d.AddHours(17));
                s.SiguienteIniUtc = NyAUtc(d.AddHours(18));
                s.RuedaIniUtc = NyAUtc(d.AddHours(9).AddMinutes(30));
            }
            return s;
        }

        /// <summary>La sesion de un dia dado (para el arnes y para releer archivos).</summary>
        public static SesionFamilia DelDia(string dia, bool corregida)
        {
            var d = DateTime.ParseExact(dia, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            return De(corregida ? NyAUtc(d.AddHours(12)) : DateTime.SpecifyKind(d.AddHours(12), DateTimeKind.Utc), corregida);
        }

        public override string ToString() => Dia + " [" + IniUtc.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) + ", " + FinUtc.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) + ") UTC" + (Corregida ? " (NY)" : " (paridad)");
    }
}
