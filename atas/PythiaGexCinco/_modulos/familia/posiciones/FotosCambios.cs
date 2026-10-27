// FotosCambios.cs — PythiaGex 4.1.2 (08-10-2026), modulo posiciones (B-pos).
// Que foto es la "de ahora" de cada libro en el minuto t, con la MISMA regla de su minutero (sin llamar a los minuteros, que tienen estado):
//   ES  (MinuteroEs:117-120): fotos de la sesion de t (TiempoFam, 22:00 UTC); la ultima con TsUtc <= t; vale si t - ts < 300 s.
//   SPX (LibroMinuteroSpx:170-179 + ConversionSpx:187-195): generado al segundo; sin las de la pausa de CME (CalendarioSpx.EntreSesiones);
//       la ultima con generado <= t; vale si t < generado + 1500 s, o, congelada (dato NY >= 15:59), hasta la proxima apertura.
//   SPY (MinuteroSpy:125-133 + ConversionSpy:163-189): igual con FueraDeSesion y Vigencia de ConversionSpy (congelada exige dato >= 2000).
//   5.0 (27-10-2026): sin TQQQ (AhoraTqqq y VIDA_TQQQ_S afuera).
// La foto de "antes" de una ventana de W min: la ultima con filas y TsUtc <= TsUtc(ahora) - W.
// Sin estado. Solo lectura de las fuentes.
using System;
using System.Collections.Generic;
using PythiaGexCinco.Familia.Spx;
using PythiaGexCinco.Familia.Spy;

namespace PythiaGexCinco.Familia
{
    public static class FotosCambios
    {
        public const double VIDA_ES_S = 300, VIDA_CBOE_S = 1500;

        /// <summary>Generado truncado al segundo (los minuteros de SPX/SPY envejecen desde ahi). Copia solo si hace falta.</summary>
        public static FotoCadena ASegundos(FotoCadena f)
        {
            long sobra = f.GeneradoUtc.Ticks % TimeSpan.TicksPerSecond;
            if (sobra == 0 && f.GeneradoUtc.Kind == DateTimeKind.Utc) return f;
            return new FotoCadena
            {
                Libro = f.Libro, GeneradoUtc = new DateTime(f.GeneradoUtc.Ticks - sobra, DateTimeKind.Utc), TsUtc = f.TsUtc, DatoUtc = f.DatoUtc, Spot = f.Spot,
                Dias = f.Dias, Filas = f.Filas, EsFuturo = f.EsFuturo, OiTotal = f.OiTotal, BaseCruda = f.BaseCruda, BaseErrorTicks = f.BaseErrorTicks,
                CierreAnterior = f.CierreAnterior, FuturoFoto = f.FuturoFoto,
            };
        }

        /// <summary>ES: la foto de MinuteroEs en el minuto t. null + motivo si no hay.</summary>
        public static FotoCadena AhoraEs(ILibroEs libro, DateTime t, out string motivo)
        {
            motivo = "";
            if (libro == null) { motivo = "sin libro ES"; return null; }
            var ini = TiempoFam.IniSesion(TiempoFam.Sesion(t));
            var desde = t.AddSeconds(-VIDA_ES_S) > ini ? t.AddSeconds(-VIDA_ES_S) : ini;
            var l = libro.Fotos(desde, t.AddMilliseconds(1));      // LibroEsFamilia.Fotos compara en milisegundos: [desde, hasta)
            FotoCadena f = null;
            for (int i = (l?.Count ?? 0) - 1; i >= 0; i--)
                if (l[i] != null && TiempoFam.Ms(l[i].TsUtc) <= TiempoFam.Ms(t)) { f = l[i]; break; }
            if (f == null || (t - f.TsUtc).TotalSeconds >= VIDA_ES_S) { motivo = "sin foto del libro ES de los ultimos 5 min"; return null; }
            if (f.Filas == null || f.Filas.Length == 0) { motivo = "foto de ES sin filas"; return null; }
            return f;
        }

        /// <summary>SPX o SPY: (la foto ORIGINAL de la fuente, su copia con el generado al segundo como la usa el minutero). null + motivo si no hay.</summary>
        public static (FotoCadena Original, FotoCadena Usada) AhoraCboe(IFuenteCboe cboe, string libro, DateTime t, bool corregida, ConversionSpy convSpy, out string motivo)
        {
            motivo = "";
            if (cboe == null) { motivo = "sin descarga de CBOE"; return (null, null); }
            bool par = !corregida, esSpy = libro == "SPY";
            var l = cboe.Fotos(libro, t.AddDays(-5), t.AddSeconds(1));
            FotoCadena orig = null;
            for (int i = (l?.Count ?? 0) - 1; i >= 0; i--)
            {
                var f = l[i];
                if (f == null) continue;
                var gen = ASegundos(f).GeneradoUtc;
                if (gen > t) continue;
                if (esSpy ? convSpy.FueraDeSesion(gen) : CalendarioSpx.EntreSesiones(gen, par)) continue;   // pausa de CME: el minutero la saltea
                orig = f; break;
            }
            if (orig == null) { motivo = "sin cadena de " + libro + " anterior al minuto"; return (null, null); }
            var usada = ASegundos(orig);
            bool congelada = esSpy ? (usada.DatoUtc.Year >= 2000 && TiempoFam.ANy(usada.DatoUtc).Hour * 60 + TiempoFam.ANy(usada.DatoUtc).Minute >= 15 * 60 + 59)
                                   : CalendarioSpx.Congelada(usada.DatoUtc);
            DateTime vig;
            if (esSpy) vig = convSpy.Vigencia(usada.GeneradoUtc, congelada);
            else
            {
                vig = usada.GeneradoUtc.AddSeconds(VIDA_CBOE_S);
                if (congelada) { var prox = CalendarioSpx.ProximaApertura(usada.GeneradoUtc, par); if (prox > vig) vig = prox; }
            }
            if (t >= vig) { motivo = "cadena de " + libro + " vencida (vigente hasta " + vig.ToString("MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) + "Z)"; return (null, null); }
            if (usada.Filas == null || usada.Filas.Length == 0) { motivo = "la cadena vigente de " + libro + " no tiene filas en memoria"; return (null, null); }
            return (orig, usada);
        }

        /// <summary>La foto de "antes": la ultima con filas y TsUtc &lt;= limite. ES por TsUtc; CBOE por generado (se mira hasta 20 min mas atras).</summary>
        public static FotoCadena Antes(ILibroEs es, IFuenteCboe cboe, string libro, DateTime limiteTs, DateTime generadoAhora, double toleranciaS)
        {
            IReadOnlyList<FotoCadena> l;
            if (libro == "ES")
            {
                if (es == null) return null;
                l = es.Fotos(limiteTs.AddSeconds(-toleranciaS - 60), limiteTs.AddMilliseconds(1));
            }
            else
            {
                if (cboe == null) return null;
                l = cboe.Fotos(libro, limiteTs.AddSeconds(-toleranciaS - 1200), generadoAhora);
            }
            for (int i = (l?.Count ?? 0) - 1; i >= 0; i--)
            {
                var f = l[i];
                if (f?.Filas == null || f.Filas.Length == 0) continue;
                if (f.TsUtc <= limiteTs) return f;
            }
            return null;
        }
    }
}
