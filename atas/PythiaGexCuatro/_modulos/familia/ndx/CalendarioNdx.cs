// CalendarioNdx.cs — PythiaGex 4.1, modulo Familia NDX (B3b). Horarios de C7/C9/oi_fresco/sesion en los dos modos:
//   Paridad  = lo que hace la vista previa (preview_niveles.py + backtest_familia.py): horas fijas en UTC (validas solo con horario de
//              verano de EE.UU.), tabla fija de contratos (velas.contrato / velas.EXPIRA, con el H7 a 14:30 UTC que es un error del Python).
//   Corregido = las mismas reglas expresadas en hora de Nueva York (no se rompen el 01-11) y el contrato sacado del codigo del grafico.
// Truco: el "reloj" de una regla es la hora UTC en paridad y (hora NY + 4 h) en corregido. En horario de verano NY+4 h == UTC, asi que
// los dos modos dan identico entre marzo y noviembre; en invierno el corregido se corre una hora con NY (que es lo que se busca).
// Sin ATAS. Todo en UTC (Kind=Utc).
using System;
using System.Globalization;

namespace PythiaGexCuatro.Familia.Ndx
{
    public static class CalendarioNdx
    {
        private static TimeZoneInfo _ny;

        /// <summary>Zona de Nueva York (Windows "Eastern Standard Time"; IANA "America/New_York" de respaldo).</summary>
        public static TimeZoneInfo Ny
        {
            get
            {
                if (_ny != null) return _ny;
                try { _ny = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
                catch { _ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
                return _ny;
            }
        }

        public static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

        /// <summary>UTC -> hora de pared de Nueva York (Kind Unspecified).</summary>
        public static DateTime ANy(DateTime utc) => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(Utc(utc), Ny), DateTimeKind.Unspecified);

        /// <summary>Hora de pared de Nueva York -> UTC. Una hora inexistente (salto de primavera) se corre 1 h adelante; una ambigua toma la de verano
        /// (como zoneinfo con fold=0 en Python).</summary>
        public static DateTime DeNy(DateTime ny)
        {
            var w = DateTime.SpecifyKind(ny, DateTimeKind.Unspecified);
            if (Ny.IsInvalidTime(w)) w = w.AddHours(1);
            if (Ny.IsAmbiguousTime(w))
            {
                var offs = Ny.GetAmbiguousTimeOffsets(w);
                var mayor = offs[0] > offs[1] ? offs[0] : offs[1];      // verano (-4 h) es el mayor
                return Utc(w - mayor);
            }
            return Utc(TimeZoneInfo.ConvertTimeToUtc(w, Ny));
        }

        /// <summary>Hora de pared NY (sin zona, "2026-10-07T16:14:57") -> UTC, como libros._ny_a_utc.</summary>
        public static bool NyTextoAUtc(string s, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrEmpty(s)) return false;
            if (!DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return false;
            if (d.Kind == DateTimeKind.Utc) { utc = d; return true; }
            if (d.Kind == DateTimeKind.Local) { utc = Utc(d.ToUniversalTime()); return true; }
            utc = DeNy(d);
            return true;
        }

        public static int MinutoDelDia(DateTime d) => d.Hour * 60 + d.Minute;

        // ------------------------------------------------------------------ el reloj de las reglas
        /// <summary>Paridad: el UTC. Corregido: NY + 4 h (== UTC con horario de verano).</summary>
        public static DateTime Reloj(DateTime utc, bool paridad) => paridad ? Utc(utc) : DateTime.SpecifyKind(ANy(utc).AddHours(4), DateTimeKind.Unspecified);

        public static DateTime DeReloj(DateTime reloj, bool paridad) => paridad ? Utc(reloj) : DeNy(reloj.AddHours(-4));

        // ------------------------------------------------------------------ sesion de CME (paridad: dia = fecha(t + 2 h), de 22:00 a 21:00 UTC)
        public static DateTime SesionDe(DateTime utc, bool paridad) => Reloj(utc, paridad).AddHours(2).Date;

        public static DateTime InicioSesion(DateTime dia, bool paridad) => DeReloj(dia.Date.AddHours(-2), paridad);

        public static DateTime FinSesion(DateTime dia, bool paridad) => DeReloj(dia.Date.AddHours(21), paridad);

        /// <summary>Foto de CBOE fuera de toda ventana de sesion: 'generado' estrictamente entre las 21:00 y las 22:00 del reloj
        /// (libros.fotos_cboe toma [dia-2h, dia+21h] con los dos extremos incluidos).</summary>
        public static bool EntreSesiones(DateTime generadoUtc, bool paridad)
        {
            var tod = Reloj(generadoUtc, paridad).TimeOfDay;
            return tod > TimeSpan.FromHours(21) && tod < TimeSpan.FromHours(22);
        }

        // ------------------------------------------------------------------ contrato del futuro y su vencimiento
        /// <summary>velas.contrato(sesion_de(t)): U6 hasta la sesion del 2026-09-14, Z6 hasta la del 2026-12-17, despues H7 (tabla fija del Python).</summary>
        public static string ContratoParidad(DateTime utc)
        {
            var s = utc.AddHours(2).Date;
            if (s <= new DateTime(2026, 9, 14)) return "U6";
            if (s <= new DateTime(2026, 12, 17)) return "Z6";
            return "H7";
        }

        /// <summary>velas.EXPIRA (tabla fija del Python; el H7 a las 14:30 UTC es su error: el 19-03-2027 ya es horario de verano).</summary>
        public static DateTime ExpiraParidad(string contrato)
        {
            switch (contrato)
            {
                case "U6": return Utc(new DateTime(2026, 9, 18, 13, 30, 0));
                case "Z6": return Utc(new DateTime(2026, 12, 18, 14, 30, 0));
                case "H7": return Utc(new DateTime(2027, 3, 19, 14, 30, 0));
                default: return ExpiraContrato(contrato);
            }
        }

        /// <summary>Vencimiento de un trimestral de indices de CME por su codigo ("Z6", "H7", "MNQZ6"): tercer viernes del mes a las 09:30 NY, en UTC.
        /// default(DateTime) si el codigo no se entiende.</summary>
        public static DateTime ExpiraContrato(string contrato)
        {
            var k = CodigoMes(contrato);
            if (k == null) return default;
            int mes = "FGHJKMNQUVXZ".IndexOf(k[0]) + 1;
            int anio = 2020 + (k[1] - '0');
            if (anio < DateTime.UtcNow.Year - 5) anio += 10;
            return DeNy(TercerViernes(anio, mes).AddHours(9).AddMinutes(30));
        }

        /// <summary>Trimestral vigente por fecha cuando el grafico es la continua (sin mes en el codigo): el del proximo tercer viernes de
        /// marzo/junio/septiembre/diciembre tal que hoy (NY) &lt; tercer viernes - 8 dias (la regla de pythiagex/base.py). OJO: la continua de ATAS
        /// rolo el U6 -> Z6 en la sesion del 15-09 (velas.contrato) y esta regla dice 10-09: solo difiere en la semana del roll.</summary>
        public static string ContratoPorFecha(DateTime utc)
        {
            var d = ANy(utc).Date;
            int y = d.Year, m = ((d.Month - 1) / 3) * 3 + 3;
            for (int i = 0; i < 8; i++)
            {
                var tv = TercerViernes(y, m);
                if (d < tv.AddDays(-8)) return "HMUZ"[(m / 3) - 1] + (y % 10).ToString(CultureInfo.InvariantCulture);
                m += 3; if (m > 12) { m = 3; y++; }
            }
            return "";
        }

        public static DateTime TercerViernes(int anio, int mes)
        {
            var d = new DateTime(anio, mes, 1);
            int dif = ((int)DayOfWeek.Friday - (int)d.DayOfWeek + 7) % 7;
            return d.AddDays(dif + 14);
        }

        /// <summary>"MNQZ6" -> "Z6"; "Z6" -> "Z6"; null si no termina en letra de mes + digito.</summary>
        public static string CodigoMes(string codigo)
        {
            if (string.IsNullOrEmpty(codigo) || codigo.Length < 2) return null;
            char l = char.ToUpperInvariant(codigo[codigo.Length - 2]), n = codigo[codigo.Length - 1];
            if (!char.IsDigit(n) || "FGHJKMNQUVXZ".IndexOf(l) < 0) return null;
            return new string(new[] { l, n });
        }

        // ------------------------------------------------------------------ C9: la cadena congelada vale hasta la proxima apertura
        /// <summary>Paridad: 13:30 UTC del dia de 'generado' (o del siguiente si ya paso). Corregido: 09:30 NY.</summary>
        public static DateTime ProximaApertura(DateTime generadoUtc, bool paridad)
        {
            var r = Reloj(generadoUtc, paridad);
            var prox = r.Date.AddHours(13).AddMinutes(30);
            if (prox <= r) prox = prox.AddDays(1);
            return DeReloj(prox, paridad);
        }

        /// <summary>Cierre de la rueda de referencia para el decaimiento del carry de C7: paridad 20:00 UTC de esa fecha; corregido 16:00 NY.</summary>
        public static DateTime CierreRueda(DateTime fechaNy, bool paridad) => DeReloj(fechaNy.Date.AddHours(20), paridad);

        /// <summary>C7 'en_hora': la hora NY del spot entre 09:35 y 15:59 (incluidas, a nivel de minuto), lunes a viernes. Igual en los dos modos
        /// (el Python ya la expresa en NY). esHabil (opcional) agrega feriados en el modo corregido.</summary>
        public static bool EnHora(DateTime tSpotUtc, Func<DateTime, bool> esHabil = null)
        {
            var ny = ANy(tSpotUtc);
            int m = MinutoDelDia(ny);
            bool habil = esHabil != null ? esHabil(ny.Date) : (ny.DayOfWeek != DayOfWeek.Saturday && ny.DayOfWeek != DayOfWeek.Sunday);
            return habil && m >= 9 * 60 + 35 && m <= 15 * 60 + 59;
        }

        /// <summary>congelada = HH:MM de t_dato en NY &gt;= "15:59".</summary>
        public static bool Congelada(DateTime datoUtc) => MinutoDelDia(ANy(datoUtc)) >= 15 * 60 + 59;
    }
}
