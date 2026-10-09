// FotosCambios.cs — PythiaGex 4.1.2 (08-10-2026), modulo posiciones (B-pos).
// Que foto es la "de ahora" de cada libro en el minuto t, con la MISMA regla de su minutero (sin llamar a los minuteros, que tienen estado):
//   NQ  (MinuteroNq:117-120): fotos de la sesion de t (TiempoFam, 22:00 UTC); la ultima con TsUtc <= t; vale si t - ts < 300 s.
//   NDX (LibroMinuteroNdx:170-179 + ConversionNdx:187-195): generado al segundo; sin las de la pausa de CME (CalendarioNdx.EntreSesiones);
//       la ultima con generado <= t; vale si t < generado + 1500 s, o, congelada (dato NY >= 15:59), hasta la proxima apertura.
//   QQQ (MinuteroQqq:125-133 + ConversionQqq:163-189): igual con FueraDeSesion y Vigencia de ConversionQqq (congelada exige dato >= 2000).
//   TQQQ (CalculoTqqq.VelaRueda): fotos con filas de la sesion de la vela; la ultima con generado <= apertura + 120 s; vale 1500 s.
// La foto de "antes" de una ventana de W min: la ultima con filas y TsUtc <= TsUtc(ahora) - W.
// Sin estado. Solo lectura de las fuentes.
using System;
using System.Collections.Generic;
using PythiaGexCuatro.Familia.Ndx;
using PythiaGexCuatro.Familia.Qqq;

namespace PythiaGexCuatro.Familia
{
    public static class FotosCambios
    {
        public const double VIDA_NQ_S = 300, VIDA_CBOE_S = 1500, VIDA_TQQQ_S = 1500;

        /// <summary>Generado truncado al segundo (los minuteros de NDX/QQQ envejecen desde ahi). Copia solo si hace falta.</summary>
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

        /// <summary>NQ: la foto de MinuteroNq en el minuto t. null + motivo si no hay.</summary>
        public static FotoCadena AhoraNq(ILibroNq libro, DateTime t, out string motivo)
        {
            motivo = "";
            if (libro == null) { motivo = "sin libro NQ"; return null; }
            var ini = TiempoFam.IniSesion(TiempoFam.Sesion(t));
            var desde = t.AddSeconds(-VIDA_NQ_S) > ini ? t.AddSeconds(-VIDA_NQ_S) : ini;
            var l = libro.Fotos(desde, t.AddMilliseconds(1));      // LibroNqFamilia.Fotos compara en milisegundos: [desde, hasta)
            FotoCadena f = null;
            for (int i = (l?.Count ?? 0) - 1; i >= 0; i--)
                if (l[i] != null && TiempoFam.Ms(l[i].TsUtc) <= TiempoFam.Ms(t)) { f = l[i]; break; }
            if (f == null || (t - f.TsUtc).TotalSeconds >= VIDA_NQ_S) { motivo = "sin foto del libro NQ de los ultimos 5 min"; return null; }
            if (f.Filas == null || f.Filas.Length == 0) { motivo = "foto de NQ sin filas"; return null; }
            return f;
        }

        /// <summary>NDX o QQQ: (la foto ORIGINAL de la fuente, su copia con el generado al segundo como la usa el minutero). null + motivo si no hay.</summary>
        public static (FotoCadena Original, FotoCadena Usada) AhoraCboe(IFuenteCboe cboe, string libro, DateTime t, bool corregida, ConversionQqq convQqq, out string motivo)
        {
            motivo = "";
            if (cboe == null) { motivo = "sin descarga de CBOE"; return (null, null); }
            bool par = !corregida, esQqq = libro == "QQQ";
            var l = cboe.Fotos(libro, t.AddDays(-5), t.AddSeconds(1));
            FotoCadena orig = null;
            for (int i = (l?.Count ?? 0) - 1; i >= 0; i--)
            {
                var f = l[i];
                if (f == null) continue;
                var gen = ASegundos(f).GeneradoUtc;
                if (gen > t) continue;
                if (esQqq ? convQqq.FueraDeSesion(gen) : CalendarioNdx.EntreSesiones(gen, par)) continue;   // pausa de CME: el minutero la saltea
                orig = f; break;
            }
            if (orig == null) { motivo = "sin cadena de " + libro + " anterior al minuto"; return (null, null); }
            var usada = ASegundos(orig);
            bool congelada = esQqq ? (usada.DatoUtc.Year >= 2000 && TiempoFam.ANy(usada.DatoUtc).Hour * 60 + TiempoFam.ANy(usada.DatoUtc).Minute >= 15 * 60 + 59)
                                   : CalendarioNdx.Congelada(usada.DatoUtc);
            DateTime vig;
            if (esQqq) vig = convQqq.Vigencia(usada.GeneradoUtc, congelada);
            else
            {
                vig = usada.GeneradoUtc.AddSeconds(VIDA_CBOE_S);
                if (congelada) { var prox = CalendarioNdx.ProximaApertura(usada.GeneradoUtc, par); if (prox > vig) vig = prox; }
            }
            if (t >= vig) { motivo = "cadena de " + libro + " vencida (vigente hasta " + vig.ToString("MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) + "Z)"; return (null, null); }
            if (usada.Filas == null || usada.Filas.Length == 0) { motivo = "la cadena vigente de " + libro + " no tiene filas en memoria"; return (null, null); }
            return (orig, usada);
        }

        /// <summary>TQQQ: la foto de la vela (CalculoTqqq.VelaRueda). Se exige el mismo t_dato que la vela (si no, no es la misma foto).</summary>
        public static FotoCadena AhoraTqqq(IFuenteCboe cboe, TqqqVela v, bool corregida, bool continua, out string motivo)
        {
            motivo = "";
            if (cboe == null || v == null) { motivo = "sin vela de TQQQ"; return null; }
            long t = v.AperturaM2Ms + 120_000L;
            var ses = TqqqSesion.De(v.AperturaM2Ms, corregida, continua);
            var l = cboe.Fotos("TQQQ", ses.FotosIniUtc, ses.FotosFinUtc);
            FotoCadena f = null;
            for (int i = (l?.Count ?? 0) - 1; i >= 0; i--)
            {
                var x = l[i];
                if (x?.Filas == null || x.Filas.Length == 0) continue;
                if (TiempoFam.Ms(x.GeneradoUtc) <= t) { f = x; break; }
            }
            if (f == null || t - TiempoFam.Ms(f.GeneradoUtc) > (long)(VIDA_TQQQ_S * 1000)) { motivo = "sin foto de TQQQ vigente para la vela"; return null; }
            if (f.DatoUtc != v.DatoUtc) { motivo = "la vela de TQQQ no usa la foto vigente (ancla de noche?): sin cambios"; return null; }
            return f;
        }

        /// <summary>La foto de "antes": la ultima con filas y TsUtc &lt;= limite. NQ por TsUtc; CBOE por generado (se mira hasta 20 min mas atras).</summary>
        public static FotoCadena Antes(ILibroNq nq, IFuenteCboe cboe, string libro, DateTime limiteTs, DateTime generadoAhora, double toleranciaS)
        {
            IReadOnlyList<FotoCadena> l;
            if (libro == "NQ")
            {
                if (nq == null) return null;
                l = nq.Fotos(limiteTs.AddSeconds(-toleranciaS - 60), limiteTs.AddMilliseconds(1));
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
