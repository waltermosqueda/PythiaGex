using System;

namespace PythiaGexTres
{
    /// <summary>
    /// LA HORA DE NUEVA YORK Y LOS DIAS AL VENCIMIENTO, CON LA CONVENCION DE CME.
    ///
    /// Copiado (y reducido a lo minimo) de atas/PythiaGexDos/CadenaViva.cs
    /// (AhoraEnNuevaYork / HoyEnNuevaYork / la cuenta de dias de Instantanea) y
    /// de GammaHoyNucleo. Las reglas que trae, todas medidas en la 1.x/2.0:
    ///
    ///   - La fecha de "hoy" es la de Nueva York, no la de la maquina: entre
    ///     medianoche y la 1 de Argentina en Nueva York todavia es ayer. Con la
    ///     fecha local el vencimiento de hoy quedaba en 0,01 dias (gamma 8x).
    ///   - Las weeklies vencen a las 16:00 NY. La trimestral (la serie Regular
    ///     del dia en que vence el futuro) se liquida a las 9:30 NY.
    ///   - El tiempo al vencimiento lleva la HORA, no solo el dia: a las 03:01 NY
    ///     un vencimiento del dia 8 esta a 4,54 dias, no a 4,00 (12 % menos en T,
    ///     6 % de gamma de mas, y desbalancea el 0DTE contra el resto).
    ///   - Piso de un minuto en T (no media hora): con 29 minutos se
    ///     subestimaba el muro del 0DTE un 12 % en la ultima lectura.
    /// </summary>
    internal static class RelojNy
    {
        private static TimeZoneInfo _ny;

        private static TimeZoneInfo Zona()
        {
            if (_ny != null) return _ny;
            try { _ny = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
            catch { try { _ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); } catch { _ny = null; } }
            return _ny;
        }

        /// <summary>Ahora, en hora de Nueva York (si no hay zona horaria instalada: UTC-4, y se nota en el log).</summary>
        public static DateTime Ahora()
        {
            var z = Zona();
            if (z == null) return DateTime.UtcNow.AddHours(-4);
            try { return TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Utc, z); }
            catch { return DateTime.UtcNow.AddHours(-4); }
        }

        /// <summary>La fecha de HOY en Nueva York: la que manda para contar dias a un vencimiento de CME.</summary>
        public static DateTime Hoy() => Ahora().Date;

        /// <summary>Hora de cierre de un vencimiento: 16:00 NY la weekly/diaria, 9:30 NY la trimestral de la mañana.</summary>
        public static DateTime Cierre(DateTime fechaVencimiento, bool venceALaManana)
            => fechaVencimiento.Date.AddHours(venceALaManana ? 9.5 : 16.0);

        /// <summary>Dias (con fraccion) hasta el cierre del vencimiento. Negativo si ya vencio.</summary>
        public static double DiasAlVencimiento(DateTime fechaVencimiento, bool venceALaManana, DateTime ahoraNy)
            => (Cierre(fechaVencimiento, venceALaManana) - ahoraNy).TotalDays;

        /// <summary>Años para Black-76: el piso es UN minuto.</summary>
        public static double AniosParaModelo(double dias) => Math.Max(dias, 1.0 / 1440.0) / 365.0;

        /// <summary>Ya vencio hace mas de 30 minutos: no se PIDE (gracia de 30 min como en 2.0). Que no se cuente en el libro
        /// desde el minuto cero lo decide CadenaApi.Filas (dias &lt;= 0 queda afuera).</summary>
        public static bool YaVencio(DateTime fechaVencimiento, bool venceALaManana, DateTime ahoraNy)
            => Cierre(fechaVencimiento, venceALaManana).AddMinutes(30) < ahoraNy;

        /// <summary>3.7.0 C7: ya cerro (desde el minuto cero, SIN la gracia de 30 min). Para ELEGIR las series: la serie vencida no ocupa
        /// la plaza de una fecha (con 2 fechas, de 16:00 a 16:30 NY el libro quedaba con la de mañana sola: Filas() ya la sacaba).</summary>
        public static bool YaCerro(DateTime fechaVencimiento, bool venceALaManana, DateTime ahoraNy)
            => Cierre(fechaVencimiento, venceALaManana) <= ahoraNy;

        /// <summary>
        /// Los futuros de CME cotizan de domingo 18:00 a viernes 17:00 NY con una pausa diaria 17:00-18:00. Solo el RELOJ: los
        /// feriados de CME (y sus cierres anticipados) no estan aca. Sirve para distinguir "no hay cotizacion" de "la API no manda".
        /// </summary>
        public static bool MercadoAbierto(DateTime ahoraNy)
        {
            var d = ahoraNy.DayOfWeek;
            if (d == DayOfWeek.Saturday) return false;
            if (d == DayOfWeek.Sunday) return ahoraNy.Hour >= 18;
            if (d == DayOfWeek.Friday && ahoraNy.Hour >= 17) return false;
            return ahoraNy.Hour != 17;   // pausa diaria 17:00-18:00
        }
    }
}
