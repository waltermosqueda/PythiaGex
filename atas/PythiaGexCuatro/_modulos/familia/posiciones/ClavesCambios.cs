// ClavesCambios.cs — PythiaGex 4.1.2 (08-10-2026), modulo posiciones (B-pos).
// La CLAVE de una fila de cadena para comparar dos fotos del mismo libro:
//   (K del libro, vencimiento UTC = GeneradoUtc + Dias[V] redondeado a 30 min, lado C/P).
// Un lado esta PRESENTE solo si su IV > 0 en esa foto (NQ: foto_viva3 descarta el lado con iv <= 0 y deja OI/IV/vol en 0; CBOE: construir
// saltea el contrato con iv <= 0 y su OI/volumen quedan en 0 sin ser cero de verdad) y su valor no es NaN. Ausente != 0: lo ausente nunca
// entra como 0 en una resta (queda fuera y baja la cobertura).
// La "fotoΔ" es la foto de AHORA con las cuatro columnas (OI y volumen por lado) reemplazadas: mismas K, V, IV, Dias, Generado, Ts, Dato y
// Spot. Como el perfil es LINEAL en esas columnas (comun/Perfil.cs:100-101, tqqq/TqqqPerfil.cs:78), Perfil(fotoΔ) = cambio valuado con la
// gamma de ahora. Funciones puras, sin estado ni E/S.
using System;
using System.Collections.Generic;

namespace PythiaGexCuatro.Familia
{
    /// <summary>(K, vencimiento redondeado a 30 min, lado). K en milesimas (los strikes de TQQQ/QQQ pueden ser de medio punto).</summary>
    public readonly struct ClaveFila : IEquatable<ClaveFila>
    {
        public readonly long K1000;
        public readonly int Venc30;      // minutos desde 1970 / 30
        public readonly byte Lado;       // 1 = call, 0 = put
        public ClaveFila(double k, int venc30, byte lado) { K1000 = (long)Math.Round(k * 1000.0); Venc30 = venc30; Lado = lado; }
        public bool Equals(ClaveFila o) => K1000 == o.K1000 && Venc30 == o.Venc30 && Lado == o.Lado;
        public override bool Equals(object o) => o is ClaveFila c && Equals(c);
        public override int GetHashCode() { unchecked { long h = K1000 * 1_000_003L + Venc30 * 7_919L + Lado; return (int)(h ^ (h >> 32)); } }
        public double K => K1000 / 1000.0;
        public DateTime VencUtc => DateTime.SpecifyKind(DateTime.UnixEpoch.AddMinutes(Venc30 * 30.0), DateTimeKind.Utc);
    }

    /// <summary>Las claves de las filas de UNA foto, por indice de fila (para armar la fotoΔ con columnas reemplazadas).</summary>
    public sealed class IndiceFilas
    {
        public FotoCadena Foto;
        public ClaveFila[] C, P;
        /// <summary>Lado presente: V valido, IV > 0 y (si se pidio) vencimiento posterior al minuto.</summary>
        public bool[] HayC, HayP;
        public int[] Venc30;             // int.MinValue = sin vencimiento (V fuera de Dias)
        public int Excluidas;            // filas fuera por vencimiento <= t
    }

    public static class ClavesCambios
    {
        public const long MS_30MIN = 1_800_000L;

        /// <summary>Vencimiento de la serie V de la foto: GeneradoUtc + Dias[V], redondeado a 30 min (minutos/30 desde 1970). int.MinValue si no vale.</summary>
        public static int Venc30(FotoCadena f, int v)
        {
            if (f?.Dias == null || v < 0 || v >= f.Dias.Length) return int.MinValue;
            double d = f.Dias[v];
            if (double.IsNaN(d) || double.IsInfinity(d)) return int.MinValue;
            long ms = TiempoFam.Ms(f.GeneradoUtc) + (long)Math.Round(d * 86_400_000.0);
            return (int)TiempoFam.PisoDiv(ms + MS_30MIN / 2, MS_30MIN);
        }

        /// <summary>Claves de las filas de f. excluirHasta: las filas con vencimiento &lt;= ese instante quedan sin lado presente (vencidas).</summary>
        public static IndiceFilas Indexar(FotoCadena f, DateTime? excluirHasta)
        {
            var F = f?.Filas ?? Array.Empty<FilaCadena>();
            int n = F.Length, nd = f?.Dias?.Length ?? 0;
            var ix = new IndiceFilas { Foto = f, C = new ClaveFila[n], P = new ClaveFila[n], HayC = new bool[n], HayP = new bool[n], Venc30 = new int[n] };
            var vc = new int[nd];
            for (int v = 0; v < nd; v++) vc[v] = Venc30(f, v);
            long lim = excluirHasta.HasValue ? TiempoFam.Ms(excluirHasta.Value) : long.MinValue;
            for (int i = 0; i < n; i++)
            {
                var x = F[i];
                int ve = x.V >= 0 && x.V < nd ? vc[x.V] : int.MinValue;
                ix.Venc30[i] = ve;
                if (ve == int.MinValue) continue;
                if (excluirHasta.HasValue && (long)ve * MS_30MIN <= lim) { ix.Excluidas++; continue; }
                ix.C[i] = new ClaveFila(x.K, ve, 1); ix.P[i] = new ClaveFila(x.K, ve, 0);
                ix.HayC[i] = x.IvC > 0; ix.HayP[i] = x.IvP > 0;
            }
            return ix;
        }

        /// <summary>Clave -> OI (oi = true) o volumen (oi = false) de los lados presentes de la foto. Claves repetidas: gana la ultima (como un dict).</summary>
        public static Dictionary<ClaveFila, double> Mapa(FotoCadena f, bool oi)
        {
            var F = f?.Filas;
            var o = new Dictionary<ClaveFila, double>(F == null ? 0 : F.Length * 2);
            if (F == null) return o;
            int nd = f.Dias?.Length ?? 0;
            var vc = new int[nd];
            for (int v = 0; v < nd; v++) vc[v] = Venc30(f, v);
            foreach (var x in F)
            {
                if (x.V < 0 || x.V >= nd || vc[x.V] == int.MinValue) continue;
                double c = oi ? x.OiC : x.VolC, p = oi ? x.OiP : x.VolP;
                if (x.IvC > 0 && !double.IsNaN(c)) o[new ClaveFila(x.K, vc[x.V], 1)] = c;
                if (x.IvP > 0 && !double.IsNaN(p)) o[new ClaveFila(x.K, vc[x.V], 0)] = p;
            }
            return o;
        }

        /// <summary>La fotoΔ: copia de f con las cuatro columnas reemplazadas (mismas K, V, IV, Dias, Generado, Ts, Dato, Spot).</summary>
        public static FotoCadena ConColumnas(FotoCadena f, double[] oiC, double[] oiP, double[] volC, double[] volP)
        {
            var F = f.Filas ?? Array.Empty<FilaCadena>();
            var n = new FilaCadena[F.Length];
            for (int i = 0; i < F.Length; i++)
            {
                var x = F[i];
                n[i] = new FilaCadena(x.K, x.V, oiC[i], oiP[i], x.IvC, x.IvP, volC[i], volP[i]);
            }
            return new FotoCadena
            {
                Libro = f.Libro, GeneradoUtc = f.GeneradoUtc, TsUtc = f.TsUtc, DatoUtc = f.DatoUtc, Spot = f.Spot, Dias = f.Dias, Filas = n,
                EsFuturo = f.EsFuturo, OiTotal = f.OiTotal, BaseCruda = f.BaseCruda, BaseErrorTicks = f.BaseErrorTicks,
                CierreAnterior = f.CierreAnterior, FuturoFoto = f.FuturoFoto,
            };
        }

        /// <summary>Indice del K mas cercano a x en K (ascendente) si esta a &lt;= tol; -1 si no.</summary>
        public static int Buscar(double[] K, double x, double tol)
        {
            if (K == null || K.Length == 0 || double.IsNaN(x)) return -1;
            int lo = 0, hi = K.Length;
            while (lo < hi) { int m = (lo + hi) >> 1; if (K[m] < x - tol) lo = m + 1; else hi = m; }
            int mejor = -1; double dm = double.PositiveInfinity;
            for (int i = lo; i < K.Length && K[i] <= x + tol; i++)
            {
                double d = Math.Abs(K[i] - x);
                if (d < dm) { dm = d; mejor = i; }
            }
            return mejor;
        }
    }
}
