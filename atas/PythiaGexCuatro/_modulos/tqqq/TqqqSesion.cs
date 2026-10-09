// TqqqSesion.cs — PythiaGex 4.1, modulo TQQQ (B4), 08-10-2026.
// La sesion de TQQQ en los dos modos (DISENO_4_1 §4.4, anexo B):
//   Paridad (tqqq_vivo.py tal cual): dia = fecha(t + 2 h) (sabado/domingo -> lunes, como tqqq_vivo.sesion_de); fotos con generado en
//     [dia - 2 h, dia + 21 h] (libros.fotos_cboe, extremos incluidos); rueda de las muestras 13,55 <= h <= 20,0 UTC (h = hora + minuto/60
//     del DATO = sello - 900 s); velas desde las 13:30 UTC; congelada si la hora decimal UTC de generado > 20,3.
//   Corregida (default del indicador): la sesion de CME en hora de Nueva York, igual que SesionFamilia de B3d: D = fecha NY de (t + 6 h);
//     fotos con generado en [18:00 NY de D-1, 17:00 NY de D) — o hasta las 18:00 NY de D con el ancla de noche (sesion continua);
//     rueda de las muestras: minuto NY del dato entre 09:33 y el cierre (16:00, 13:00 en medio dia), ambos incluidos; velas desde las
//     09:30 NY; congelada si generado > cierre + 18 min. En horario de verano da lo mismo que la paridad (verificado en el arnes).
using System;
using System.Globalization;

namespace PythiaGexCuatro.Familia
{
    public sealed class TqqqSesion
    {
        public string Dia;                      // yyyy-MM-dd (la fecha de la rueda)
        public DateTime Fecha;                  // la misma, como fecha
        public bool Corregida, Continua;
        public DateTime FotosIniUtc;            // ventana de fotos de la sesion: [ini, fin) — en paridad fin = dia + 21 h + 1 tick (incluido)
        public DateTime FotosFinUtc;
        public DateTime RuedaIniUtc;            // desde donde hay velas de TQQQ (sin ancla de noche)
        public DateTime CierreUtc;              // cierre de la rueda (paridad 20:00 UTC fijo)
        public DateTime SiguienteIniUtc;        // donde empieza la proxima sesion
        public string Clave => Dia + (Corregida ? (Continua ? "|nyc" : "|ny") : "|py");

        public static TqqqSesion De(long ms, bool corregida, bool continua) => De(TqqqHora.DeMs(ms), corregida, continua);

        public static TqqqSesion De(DateTime tUtc, bool corregida, bool continua)
        {
            tUtc = DateTime.SpecifyKind(tUtc, DateTimeKind.Utc);
            if (!corregida)
            {
                var d = tUtc.AddHours(2).Date;
                if (d.DayOfWeek == DayOfWeek.Saturday) d = d.AddDays(2); else if (d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(1);
                return Paridad(d);
            }
            var ny = TqqqHora.UtcANy(tUtc);
            return Nueva(ny.AddHours(6).Date, continua);
        }

        /// <summary>La sesion de una fecha (arnes, dia anterior, anclas).</summary>
        public static TqqqSesion DelDia(DateTime fecha, bool corregida, bool continua) => corregida ? Nueva(fecha.Date, continua) : Paridad(fecha.Date);

        private static TqqqSesion Paridad(DateTime d)
        {
            d = DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);
            return new TqqqSesion
            {
                Dia = TqqqHora.Fecha(d), Fecha = d, Corregida = false, Continua = false,
                FotosIniUtc = d.AddHours(-2), FotosFinUtc = d.AddHours(21).AddTicks(1),
                RuedaIniUtc = d.AddHours(13).AddMinutes(30), CierreUtc = d.AddHours(20), SiguienteIniUtc = d.AddHours(22),
            };
        }

        private static TqqqSesion Nueva(DateTime d, bool continua)
        {
            d = DateTime.SpecifyKind(d.Date, DateTimeKind.Unspecified);
            return new TqqqSesion
            {
                Dia = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Fecha = d, Corregida = true, Continua = continua,
                FotosIniUtc = TqqqHora.NyAUtc(d.AddDays(-1).AddHours(18)),
                FotosFinUtc = TqqqHora.NyAUtc(d.AddHours(continua ? 18 : 17)),
                RuedaIniUtc = TqqqHora.NyAUtc(d.AddHours(9).AddMinutes(30)),
                CierreUtc = TqqqHora.NyAUtc(d + TqqqHora.CierreNy(d)),
                SiguienteIniUtc = TqqqHora.NyAUtc(d.AddHours(18)),
            };
        }

        /// <summary>La muestra cuenta en la rueda si la hora del DATO (sello - 900 s) cae en la rueda.</summary>
        public bool EnRueda(DateTime tDatoUtc)
        {
            if (!Corregida)
            {   // tqqq_vivo: h = hora + minuto/60 (UTC); RUEDA_INI_H + 0.05 <= h <= RUEDA_FIN_H (13.5, 20.0)
                double h = tDatoUtc.Hour + tDatoUtc.Minute / 60.0;
                return 13.5 + 0.05 <= h && h <= 20.0;
            }
            var ny = TqqqHora.UtcANy(tDatoUtc);
            if (ny.Date != Fecha.Date || !TqqqHora.EsHabil(ny.Date)) return false;
            int hm = ny.Hour * 60 + ny.Minute;
            var c = TqqqHora.CierreNy(ny.Date);
            return hm >= 9 * 60 + 33 && hm <= (int)c.TotalMinutes;
        }

        /// <summary>tqqq_vivo: congelada = hora decimal de generado > RUEDA_FIN_H + 0.3 (UTC). Corregida: > cierre NY + 0,3 h.</summary>
        public bool Congelada(DateTime generadoUtc)
        {
            if (!Corregida) return generadoUtc.Hour + generadoUtc.Minute / 60.0 > 20.0 + 0.3;
            var ny = TqqqHora.UtcANy(generadoUtc);
            if (ny.Date > Fecha.Date) return true;
            if (ny.Date < Fecha.Date) return false;
            return ny.Hour + ny.Minute / 60.0 > TqqqHora.CierreNy(Fecha).TotalHours + 0.3;
        }

        public override string ToString() => Dia + " [" + FotosIniUtc.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) + ", " +
                                             FotosFinUtc.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) + ") UTC " +
                                             (Corregida ? (Continua ? "(NY, continua)" : "(NY)") : "(paridad UTC)");
    }
}
