// ZeroEstandar.cs — PythiaGex 4.1, modulo Familia, CODIGO COMUN (B3a, 08-10-2026).
// C5, port exacto de backtest_familia.zero_estandar: la suma de TODO el libro (filas del horizonte Hoy, sin el filtro de +-3 %),
// reevaluada a cada precio x de una grilla alrededor de S (T e IV fijas), y el cruce de signo mas cercano a S.
//   c0 = round(S/cp)*cp (mitad al par)   n = round(semi/paso)   xs = c0 + paso*j, j = -n..n
//   Cv(x) = sum_filas[ g(x,K,T,iv_c)*vol_c - g(x,K,T,iv_p)*vol_p ] * x^2      (suma por pares de numpy: NumFam.SumaNumpy)
//   Co(x) = lo mismo con oi
//   cruce donde sign(C_i)*sign(C_i+1) < 0 (estricto); z = x_i + (x_i+1 - x_i)*(-C_i)/(C_i+1 - C_i); el z que minimiza |z - S| (el primero)
// Devuelve (zv, zo) en el EJE DEL LIBRO (NaN si no hay); quien llama lo pasa a NQ (NQ z + corr, NDX z + base, QQQ z * razon).
// Parametros: NQ y NDX paso 2.5 / semi 300 / cp 50; QQQ 0.05 / 7.5 / 1.0.
//
// CACHE (CacheZero): la vista previa calcula el zero con el S y el envejecimiento del PRIMER minuto de cada clave y lo reutiliza:
//   NQ  (foto, round(S/50))       CBOE (foto, floor((t - generado)/900 s), round(S/50) NDX | round(S/1) QQQ)
// y el diccionario es LOCAL a cada llamada de minutos_nq/minutos_cboe: en el historico de preview_niveles eso es una TANDA de 240 minutos
// desde el inicio de la sesion (preview_niveles.historico). CacheZero reproduce eso con la tanda = (clave - claveIni) / 240: determinista,
// igual en vivo y al reiniciar, y en paridad con la referencia (que es el historico).
using System;
using System.Collections.Generic;

namespace PythiaGexCuatro.Familia
{
    public readonly struct ParamZero
    {
        public readonly double Paso, Semi, CentroPaso;
        public ParamZero(double paso, double semi, double centroPaso) { Paso = paso; Semi = semi; CentroPaso = centroPaso; }
    }

    public static class ZeroEstandarFam
    {
        public static readonly ParamZero NQ = new ParamZero(2.5, 300.0, 50.0);
        public static readonly ParamZero NDX = new ParamZero(2.5, 300.0, 50.0);
        public static readonly ParamZero QQQ = new ParamZero(0.05, 7.5, 1.0);

        /// <summary>(zv, zo) en el eje del libro. NaN, NaN si fl es null o S &lt;= 0.</summary>
        public static (double Zv, double Zo) Calcular(FilasHoy fl, double S, bool esFuturo, ParamZero p)
            => Calcular(fl, S, esFuturo, p.Paso, p.Semi, p.CentroPaso);

        public static (double Zv, double Zo) Calcular(FilasHoy fl, double S, bool esFuturo, double paso, double semi, double centroPaso)
        {
            if (fl == null || S <= 0) return (double.NaN, double.NaN);
            double c0 = Math.Round(S / centroPaso, MidpointRounding.ToEven) * centroPaso;
            int n = (int)Math.Round(semi / paso, MidpointRounding.ToEven);
            int nx = 2 * n + 1;
            var xs = new double[nx];
            for (int j = 0; j < nx; j++) xs[j] = c0 + paso * (double)(j - n);
            var cv = new double[nx]; var co = new double[nx];
            int m = fl.N;
            var fv = new double[m]; var fo = new double[m];
            for (int j = 0; j < nx; j++)
            {
                double x = xs[j];
                for (int i = 0; i < m; i++)
                {
                    double gc = GammaFam.De(esFuturo, x, fl.K[i], fl.T[i], fl.IvC[i]);
                    double gp = GammaFam.De(esFuturo, x, fl.K[i], fl.T[i], fl.IvP[i]);
                    fv[i] = gc * fl.VolC[i] - gp * fl.VolP[i];
                    fo[i] = gc * fl.OiC[i] - gp * fl.OiP[i];
                }
                double x2 = x * x;
                cv[j] = NumFam.SumaNumpy(fv, 0, m) * x2;
                co[j] = NumFam.SumaNumpy(fo, 0, m) * x2;
            }
            return (Cruce(xs, cv, S), Cruce(xs, co, S));
        }

        /// <summary>El cruce de signo estricto mas cercano a S (el primero si empatan), interpolado; NaN si no hay.</summary>
        public static double Cruce(double[] xs, double[] c, double S)
        {
            double mejor = double.NaN, dmin = double.PositiveInfinity;
            for (int i = 0; i + 1 < c.Length; i++)
            {
                if (double.IsNaN(c[i]) || double.IsNaN(c[i + 1]) || Math.Sign(c[i]) * Math.Sign(c[i + 1]) >= 0) continue;
                double z = xs[i] + (xs[i + 1] - xs[i]) * (-c[i]) / (c[i + 1] - c[i]);
                double d = Math.Abs(z - S);
                if (d < dmin) { dmin = d; mejor = z; }
            }
            return mejor;
        }
    }

    /// <summary>
    /// La cache del zero estandar de la vista previa (ver cabecera). Clave = (foto, b1, b2) dentro de la tanda vigente; al cambiar de tanda
    /// se vacia. No es segura entre hilos: una por minutero, usada desde el hilo del motor.
    /// </summary>
    public sealed class CacheZero
    {
        public const int MINUTOS_TANDA = 240;
        private readonly Dictionary<(object, long, long), (double, double)> _d = new Dictionary<(object, long, long), (double, double)>();
        private long _tanda = long.MinValue;
        public int Calculos { get; private set; }
        public int Aciertos { get; private set; }

        /// <summary>Tanda de la vista previa: (clave - claveIniSesion) / 240 (preview_niveles.historico calcula de a 240 minutos).</summary>
        public static long TandaDe(long clave, long claveIniSesion) => TiempoFam.PisoDiv(clave - claveIniSesion, MINUTOS_TANDA);

        /// <summary>Vacia la cache si cambio la tanda. Devuelve true si la vacio.</summary>
        public bool Tanda(long tanda)
        {
            if (tanda == _tanda) return false;
            _tanda = tanda; _d.Clear();
            return true;
        }

        public (double Zv, double Zo) Obtener(object foto, long b1, long b2, Func<(double, double)> calcular)
        {
            var k = (foto, b1, b2);
            if (_d.TryGetValue(k, out var v)) { Aciertos++; return v; }
            v = calcular(); Calculos++;
            _d[k] = v;
            return v;
        }

        public void Vaciar() { _d.Clear(); _tanda = long.MinValue; }
    }
}
