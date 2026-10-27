// SeriesEs.cs — PythiaGex 4.1, modulo Familia, libro ES (B3a, 08-10-2026).
// Las 7 series VISIBLES del libro ES en la vista previa (preview_niveles.SERIES): MUROS_ES_vol, MAJORS_ES_vol, MUROS_ES_oi, MAJORS_ES_oi,
// ZEST_ES_vol, ZEST_ES_oi, ZTP_ES_vol (ZTP_ES_oi se calcula para CONF_oi pero no es serie visible). Las de OI solo si oi_ok (C2).
// El monto (C1) usa el multiplicador de ES: 20 USD/pt -> x0.2 sobre la escala de la 3.0.
using System.Collections.Generic;

namespace PythiaGexCinco.Familia
{
    public static class SeriesEs
    {
        public static readonly string[] VISIBLES = { "MUROS_ES_vol", "MAJORS_ES_vol", "MUROS_ES_oi", "MAJORS_ES_oi", "ZEST_ES_vol", "ZEST_ES_oi", "ZTP_ES_vol" };

        /// <summary>serie -> niveles crudos (P sin redondear, E = D1/D2/Z, GexM) de las series visibles de ES en ese minuto (vacio si b es null).</summary>
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
