// OiNq.cs — PythiaGex 4.1, modulo Familia, libro NQ (B3a, 08-10-2026).
// C2: el interes abierto del libro NQ con FECHA (preview_niveles._oi_hasta_vivo / _oi_avanzar = backtest_familia.oi_nq_viejo_hasta en vivo).
// Entre las 22:00 UTC y el SALTO de OI de Rithmic de esa noche (medido 01:24-01:52 UTC en 11 noches) el OI de NQ es de DOS sesiones atras y
// los niveles por OI de NQ (MUROS_NQ_oi, MAJORS_NQ_oi, ZEST_NQ_oi, FAM_MUROS_oi) NO existen.
//   * lunes o posferiado: no hay supresion (el OI ya llego actualizado). Posferiado = TiempoFam.EsPosferiado (calendario; la vista previa
//     tiene escrito fijo solo 2026-09-08) o una lista explicita.
//   * salto: entre las fotos con ts en [dia - 2 h, dia + 3 h 30 m] (ambos incluidos), el primer par de fotos SEGUIDAS con >= 20 claves comunes
//     (K, MM-dd del vencimiento, call|put) con OI > 0 en alguna de las dos y >= 50 % de ellas con OI distinto. salto = ts de la segunda.
//   * viejo_hasta: salto si hubo; si no, si la PRIMERA foto de la noche es de [01:55, 13:30) UTC: sin supresion (SUPUESTO: ya paso el salto);
//     si no, si ahora < dia + 3:30: dia + 3:30 ("esperando el salto"); si no: dia + 2:00 ("sin salto visible", conservador).
//   * oi_ok(t) = !(viejo_hasta != null && dia - 2 h <= t < viejo_hasta). Sin fotos de noche: sin supresion.
// Con 'ahora' >= dia + 3:30 da EXACTAMENTE lo mismo que la version de backtest (la de la referencia historica); antes es la version en vivo
// (conservadora). Los minutos ya calculados no se recalculan (eso lo decide el motor).
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PythiaGexCuatro.Familia
{
    public sealed class OiNq
    {
        public readonly string Dia;
        public readonly DateTime Ini, Lim, DosAm;
        private readonly bool _sinSupresion;
        private int _idx;
        private Dictionary<(double, string, int), double> _prev;
        public DateTime? Salto { get; private set; }
        public DateTime? Primera { get; private set; }

        /// <param name="posferiados">null: calendario de feriados (TiempoFam.EsPosferiado); si no, la lista explicita ("yyyy-MM-dd").</param>
        public OiNq(string dia, IReadOnlyCollection<string> posferiados = null)
        {
            Dia = dia;
            var d = TiempoFam.DiaSesion(dia);
            Ini = d.AddHours(-2); Lim = d.AddHours(3.5); DosAm = d.AddHours(2);
            bool posf = posferiados != null ? Contiene(posferiados, dia) : TiempoFam.EsPosferiado(dia);
            _sinSupresion = d.DayOfWeek == DayOfWeek.Monday || posf;
        }

        private static bool Contiene(IReadOnlyCollection<string> l, string d) { foreach (var x in l) if (x == d) return true; return false; }

        /// <summary>Recorre las fotos nuevas (la lista crece por el final; ordenada por TsUtc). Corta en el primer salto.</summary>
        public void Avanzar(IReadOnlyList<FotoCadena> fotos)
        {
            if (_sinSupresion || Salto != null || fotos == null) return;
            while (_idx < fotos.Count)
            {
                var f = fotos[_idx++];
                var t = f.TsUtc;
                if (t < Ini || t > Lim) continue;
                if (Primera == null) { Primera = t; _prev = Mapa(f); continue; }
                var cur = Mapa(f);
                int com = 0, dist = 0;
                foreach (var kv in cur)
                {
                    if (!_prev.TryGetValue(kv.Key, out double pv)) continue;
                    if (!(kv.Value > 0 || pv > 0)) continue;
                    com++;
                    if (kv.Value != pv) dist++;
                }
                if (com >= 20 && dist >= 0.5 * com) { Salto = t; _prev = null; return; }
                _prev = cur;
            }
        }

        /// <summary>backtest mapa(f): (K, MM-dd de ts + dias[V], 1|0) -> OI (las claves repetidas se pisan, como el dict de Python).</summary>
        private static Dictionary<(double, string, int), double> Mapa(FotoCadena f)
        {
            var o = new Dictionary<(double, string, int), double>();
            if (f?.Filas == null) return o;
            var venc = new string[f.Dias?.Length ?? 0];
            for (int i = 0; i < venc.Length; i++)
                venc[i] = f.TsUtc.AddTicks((long)Math.Round(f.Dias[i] * TimeSpan.TicksPerDay)).ToString("MM-dd", CultureInfo.InvariantCulture);
            foreach (var x in f.Filas)
            {
                if (x.V < 0 || x.V >= venc.Length) continue;
                o[(x.K, venc[x.V], 1)] = x.OiC;
                o[(x.K, venc[x.V], 0)] = x.OiP;
            }
            return o;
        }

        /// <summary>(viejo_hasta | null, como) con lo visto hasta ahora. Llamar Avanzar antes.</summary>
        public (DateTime? Hasta, string Como) Hasta(DateTime ahoraUtc)
        {
            if (_sinSupresion) return (null, "lunes o posferiado: OI ya actualizado");
            if (Primera == null) return (null, "sin fotos de noche");
            if (Salto != null) return (Salto, "salto de OI medido a las " + Salto.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " UTC");
            string hm = TiempoFam.HM(Primera.Value);
            if (string.CompareOrdinal(hm, "01:55") >= 0 && string.CompareOrdinal(hm, "13:30") < 0)
                return (null, "primera foto " + hm + " UTC (despues del salto habitual): OI supuesto actualizado");
            if (ahoraUtc < Lim) return (Lim, "esperando el salto de OI de Rithmic (medido 01:24-01:52 UTC en 11 noches): OI de anteayer");
            return (DosAm, "sin salto visible: suprimido hasta 02:00 UTC (conservador)");
        }

        public bool OiOk(DateTime tUtc, DateTime ahoraUtc)
        {
            var (h, _) = Hasta(ahoraUtc);
            return !(h != null && Ini <= tUtc && tUtc < h.Value);
        }
    }
}
