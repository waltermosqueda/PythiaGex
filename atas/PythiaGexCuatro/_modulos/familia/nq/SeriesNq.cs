// SeriesNq.cs — PythiaGex 4.1, modulo Familia, libro NQ (B3a, 08-10-2026).
// Las 7 series VISIBLES del libro NQ en la vista previa (preview_niveles.SERIES): MUROS_NQ_vol, MAJORS_NQ_vol, MUROS_NQ_oi, MAJORS_NQ_oi,
// ZEST_NQ_vol, ZEST_NQ_oi, ZTP_NQ_vol (ZTP_NQ_oi se calcula para CONF_oi pero no es serie visible). Las de OI solo si oi_ok (C2).
// El monto (C1) usa el multiplicador de NQ: 20 USD/pt -> x0.2 sobre la escala de la 3.0.
using System.Collections.Generic;

namespace PythiaGexCuatro.Familia
{
    public static class SeriesNq
    {
        public static readonly string[] VISIBLES = { "MUROS_NQ_vol", "MAJORS_NQ_vol", "MUROS_NQ_oi", "MAJORS_NQ_oi", "ZEST_NQ_vol", "ZEST_NQ_oi", "ZTP_NQ_vol" };

        /// <summary>serie -> niveles crudos (P sin redondear, E = D1/D2/Z, GexM) de las series visibles de NQ en ese minuto (vacio si b es null).</summary>
        public static Dictionary<string, List<NivelFam>> Visibles(LibroMinuto b)
        {
            var o = new Dictionary<string, List<NivelFam>>();
            if (b == null) return o;
            var s = SeriesLibroFam.Calcular(b);
            foreach (var id in VISIBLES)
                if (s.Series.TryGetValue(id, out var l) && l.Count > 0) o[id] = l;
            return o;
        }
    }
}
