// RegimenesOi.cs — PythiaGex 4.1.2 (08-10-2026), modulo posiciones (B-pos).
// Detector de REGIMENES de interes abierto de un libro, incremental (como OiNq._idx), sobre la historia de fotos en orden.
// El OI lo consolida la OCC una vez por noche: dentro de un regimen no cambia (medido: NQ salta una vez por noche 01:24/01:30/01:48 UTC;
// NDX 06:26-06:36 UTC; QQQ 07:31-09:12 UTC; fuera de esos saltos 0 cambios reales). Entre fotos CONSECUTIVAS con filas:
//   C = claves presentes (lado con IV > 0) con OI > 0 en las dos fotos; SALTO si |C| >= 20 y >= 20 % de C cambio de valor.
//   (No el 50 % de OiNq: NDX salta con 52-59 % de las claves, queda al borde. Los saltos a/desde cero de CBOE son artefactos de iv <= 0 y
//   quedan fuera porque exigen OI > 0 en las dos.)
// R0 = el regimen desde el ultimo salto; R1 = el anterior. OI de cada clave en un regimen = el ultimo valor presente en el.
// Si el regimen mas viejo en memoria no empezo con un salto visto, su inicio real es anterior (InicioEsSalto = false).
// 09-10-2026 (revision): LIBRO FLACO. TQQQ salta con 52, 69, 56, 43, 40 y 28 claves comunes (10-01 a 10-08, bajando; 2 vencimientos a
// <= 8 dias el jueves): con menos de 20 el salto no se veia, R0 seguia con el OI nuevo encima y R1 quedaba en la publicacion de hace dos
// noches (ΔOI de DOS publicaciones con las fechas del salto anterior). Ahora, ademas de la regla de arriba:
//   * salto de libro flaco: |C| >= 8 y >= 80 % de C cambio (medido en el arnes, P1: los saltos reales de QQQ/TQQQ cambian 91,9-100 % de C;
//     fuera de los saltos la fraccion maxima es 0,00 % en NQ/QQQ/TQQQ y 0,10 % en NDX; ningun par sin salto con menos de 20 claves comunes);
//   * cambio SIN CONFIRMAR: cualquier otro par con >= 50 % de C cambiado (C >= 1) abre un regimen marcado Dudoso. Mientras R0 sea dudoso
//     el ΔOI no se publica (NaN con nota): ni se inventa una publicacion ni se tapa el cambio pisando R0 en silencio.
// Sin E/S. No es seguro entre hilos: lo usa el hilo del host de la familia.
using System;
using System.Collections.Generic;

namespace PythiaGexCuatro.Familia
{
    public sealed class SaltoOi
    {
        public string Libro;
        public DateTime TsUtc;           // sello de la primera foto con el OI nuevo
        public DateTime AntesUtc;        // sello de la foto anterior (con el OI viejo)
        public int Comunes, Distintas;
        /// <summary>Detectado con la regla de libro flaco (|C| &lt; MinClaves).</summary>
        public bool Flaco;
        /// <summary>No es un salto confirmado: cambio de OI con muy pocas claves para decir que es una publicacion nueva.</summary>
        public bool Dudoso;
        public double Fraccion => Comunes > 0 ? (double)Distintas / Comunes : double.NaN;
    }

    public sealed class RegimenOi
    {
        public DateTime InicioUtc;       // sello de la primera foto del regimen
        public bool InicioEsSalto;       // false: es el primero en memoria (empezo antes de lo que hay guardado)
        public bool Dudoso;              // empezo con un cambio de OI SIN CONFIRMAR (libro flaco): mientras sea R0, sin ΔOI
        public int ComunesInicio, DistintasInicio;   // claves comunes y cambiadas en el par que lo abrio (0 si es el primero)
        public DateTime UltimaUtc;       // sello de la ultima foto procesada en el regimen
        public int Fotos;
        public readonly Dictionary<ClaveFila, double> Oi = new Dictionary<ClaveFila, double>();
    }

    public sealed class RegimenesOi
    {
        public readonly string Libro;
        public readonly int MinClaves;
        public readonly double Fraccion;
        /// <summary>Libro flaco: salto con MinClavesFlaco &lt;= |C| &lt; MinClaves si cambio al menos FraccionFlaco de C.</summary>
        public readonly int MinClavesFlaco;
        public readonly double FraccionFlaco;
        /// <summary>Cambio sin confirmar: cualquier otro par con al menos esta fraccion de C cambiada (y C &gt;= 1).</summary>
        public readonly double FraccionDudoso;
        private FotoCadena _prevFoto;
        private Dictionary<ClaveFila, double> _prevMapa;

        public RegimenOi R0 { get; private set; }
        public RegimenOi R1 { get; private set; }
        /// <summary>Saltos vistos (los ultimos 64), para el log, la pestaña y el arnes.</summary>
        public readonly List<SaltoOi> Saltos = new List<SaltoOi>();
        /// <summary>Hasta donde se proceso: el orden de la ultima foto (NQ: TsUtc; CBOE: GeneradoUtc tal como lo da la fuente).</summary>
        public DateTime Cursor { get; private set; } = DateTime.MinValue;
        public int Procesadas { get; private set; }
        public int ConFilas { get; private set; }

        public RegimenesOi(string libro, int minClaves = 20, double fraccion = 0.20, int minClavesFlaco = 8, double fraccionFlaco = 0.80, double fraccionDudoso = 0.50)
        {
            Libro = libro; MinClaves = minClaves; Fraccion = fraccion;
            MinClavesFlaco = minClavesFlaco; FraccionFlaco = fraccionFlaco; FraccionDudoso = fraccionDudoso;
        }

        /// <summary>Cambios sin confirmar vistos (para el log y el arnes).</summary>
        public int Dudosos { get; private set; }

        /// <summary>Procesa la foto siguiente (orden creciente). orden = su clave de orden en la fuente (para el cursor).</summary>
        public void Procesar(FotoCadena f, DateTime orden)
        {
            if (orden > Cursor) Cursor = orden;
            Procesadas++;
            if (f?.Filas == null || f.Filas.Length == 0) return;          // foto liviana (sin filas): no informa
            ConFilas++;
            if (_prevFoto != null && R0 != null && ReferenceEquals(f.Filas, _prevFoto.Filas))
            {   // la noche de CBOE repite las filas con otro sello: mismas claves, mismo OI
                R0.UltimaUtc = f.TsUtc; R0.Fotos++; _prevFoto = f;
                return;
            }
            var cur = ClavesCambios.Mapa(f, true);
            if (cur.Count == 0) return;
            if (_prevMapa != null)
            {
                int com = 0, dist = 0;
                foreach (var kv in cur)
                {
                    if (!(kv.Value > 0)) continue;
                    if (!_prevMapa.TryGetValue(kv.Key, out double pv) || !(pv > 0)) continue;
                    com++;
                    if (pv != kv.Value) dist++;
                }
                bool normal = com >= MinClaves && dist >= Fraccion * com;
                bool flaco = !normal && com >= MinClavesFlaco && com < MinClaves && dist >= FraccionFlaco * com;
                bool dudoso = !normal && !flaco && dist > 0 && dist >= FraccionDudoso * com;
                if (normal || flaco || dudoso)
                {
                    Saltos.Add(new SaltoOi { Libro = Libro, TsUtc = f.TsUtc, AntesUtc = _prevFoto?.TsUtc ?? default, Comunes = com, Distintas = dist, Flaco = flaco, Dudoso = dudoso });
                    if (Saltos.Count > 64) Saltos.RemoveAt(0);
                    if (dudoso) Dudosos++;
                    R1 = R0;
                    R0 = new RegimenOi { InicioUtc = f.TsUtc, InicioEsSalto = true, Dudoso = dudoso, ComunesInicio = com, DistintasInicio = dist };
                }
            }
            if (R0 == null) R0 = new RegimenOi { InicioUtc = f.TsUtc, InicioEsSalto = false };
            foreach (var kv in cur) R0.Oi[kv.Key] = kv.Value;
            R0.UltimaUtc = f.TsUtc; R0.Fotos++;
            _prevMapa = cur; _prevFoto = f;
        }

        /// <summary>Ya se proceso todo lo que la fuente tiene hasta 'hasta' (aunque no haya devuelto una foto justo ahi).</summary>
        public void Avanzado(DateTime hasta) { if (hasta > Cursor) Cursor = hasta; }

        /// <summary>Vuelve a cero (historia nueva de la fuente).</summary>
        public void Reiniciar()
        {
            _prevFoto = null; _prevMapa = null; R0 = null; R1 = null; Saltos.Clear();
            Cursor = DateTime.MinValue; Procesadas = 0; ConFilas = 0; Dudosos = 0;
        }
    }
}
