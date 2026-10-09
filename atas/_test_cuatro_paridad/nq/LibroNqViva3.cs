// LibroNqViva3.cs — arnes de paridad NQ (B3a, 08-10-2026). DOBLE DE PRUEBA de ILibroNq (el de verdad es el adaptador de B1 sobre CadenaApi).
// Lee los viva3 ARCHIVADOS de la 3.0 (%APPDATA%/ATAS/PythiaGex3/viva/viva3-NQ-<dia-1|dia>.jsonl) como preview_niveles._nuevas_nq:
//   lineas en orden de archivo (vispera y dia), foto_viva3 con la ventana [dia-2h, dia+21h] (incluida), dedup por ts quedandose con la de
//   MAS filas crudas (empate: la primera), ordenadas por ts.
// Modo vivo simulado: VisibleHasta limita lo que se ve (como si el reloj estuviera en ese instante) y Version sube cuando aparece una foto.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PythiaGexCuatro.Familia;

namespace ParidadNq
{
    public sealed class LibroNqViva3 : ILibroNq
    {
        public readonly List<FotoCadena> Todas = new List<FotoCadena>();
        private long[] _ts = Array.Empty<long>();
        public DateTime? VisibleHasta;
        public string Origen = "";
        public int Lineas, Descartadas, Repetidas;

        public static LibroNqViva3 Cargar(string carpeta, string dia, DateTime? hastaUtc = null)
        {
            var L = new LibroNqViva3();
            var ini = TiempoFam.IniSesion(dia); var fin = TiempoFam.FinSesion(dia);
            if (hastaUtc != null && hastaUtc.Value < fin) fin = hastaUtc.Value;
            var vistos = new Dictionary<DateTime, (FotoCadena F, int N)>();
            var d0 = TiempoFam.DiaSesion(dia);
            foreach (var d in new[] { d0.AddDays(-1), d0 })
            {
                var ruta = Path.Combine(carpeta, "viva3-NQ-" + d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jsonl");
                if (!File.Exists(ruta)) continue;
                using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var sr = new StreamReader(fs))
                {
                    string l;
                    while ((l = sr.ReadLine()) != null)
                    {
                        L.Lineas++;
                        var f = FotoNq.DesdeLineaViva3(l, ini, fin, out int n);
                        if (f == null) { L.Descartadas++; continue; }
                        if (vistos.TryGetValue(f.TsUtc, out var v)) { L.Repetidas++; if (v.N >= n) continue; }
                        vistos[f.TsUtc] = (f, n);
                    }
                }
            }
            foreach (var v in vistos.Values) L.Todas.Add(v.F);
            L.Todas.Sort((a, b) => a.TsUtc.CompareTo(b.TsUtc));
            L._ts = new long[L.Todas.Count];
            for (int i = 0; i < L.Todas.Count; i++) L._ts[i] = TiempoFam.Ms(L.Todas[i].TsUtc);
            L.Origen = string.Format(CultureInfo.InvariantCulture, "viva3 {0}: {1} lineas, {2} fotos (descartadas {3}, ts repetidos {4})",
                dia, L.Lineas, L.Todas.Count, L.Descartadas, L.Repetidas);
            return L;
        }

        private int Visibles()
        {
            if (VisibleHasta == null) return Todas.Count;
            return NumFam.UltimoMenorIgual(_ts, TiempoFam.Ms(VisibleHasta.Value)) + 1;
        }

        public long Version => Visibles();

        public IReadOnlyList<FotoCadena> Fotos(DateTime desdeUtc, DateTime hastaUtc)
        {
            int n = Visibles();
            var o = new List<FotoCadena>(n);
            for (int i = 0; i < n; i++) { var t = Todas[i].TsUtc; if (t >= desdeUtc && t < hastaUtc) o.Add(Todas[i]); }
            return o;
        }

        public FotoCadena Ultima() { int n = Visibles(); return n > 0 ? Todas[n - 1] : null; }
    }
}
