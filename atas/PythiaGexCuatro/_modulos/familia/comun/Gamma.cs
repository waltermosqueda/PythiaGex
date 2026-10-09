// Gamma.cs — PythiaGex 4.1, modulo Familia, CODIGO COMUN (B3a, 08-10-2026).
// La gamma de la vista previa (laboratorio/tres/nucleo.py gamma76 / gamma_bs), con el MISMO orden de operaciones que numpy:
//   Black-76 (libro NQ, opciones sobre el futuro), SIN descuento:  v = iv*sqrt(T); d1 = (ln(F/K) + 0.5*iv*iv*T)/v; g = fi(d1)/(F*v)
//   Black-Scholes (NDX, QQQ, TQQQ de CBOE), r = 0.0375, sin dividendo: d1 = (ln(S/K) + (r + 0.5*iv*iv)*T)/v; g = fi(d1)/(S*v)
//   fi(x) = exp(-0.5*x*x) * (1/sqrt(2 pi)).  g = 0 si S, K, T o iv no son > 0 (NaN incluido).
// OJO: Black76.Gamma de la 3.0 (atas/PythiaGexCuatro/Black76.cs) multiplica por exp(-0.0375 T): NO usarla en la Familia (paridad con la
// vista previa; la diferencia es <= 2,1e-4 relativa con T <= 2 dias y solo podria dar vuelta empates finisimos 0DTE/1DTE).
using System;

namespace PythiaGexCuatro.Familia
{
    public static class GammaFam
    {
        /// <summary>Tasa de CBOE (Black-Scholes) — la 'Tasa libre de riesgo' por defecto de la 3.0 y del laboratorio.</summary>
        public const double TASA = 0.0375;
        /// <summary>Piso de T: un minuto (GammaHoyNucleo / nucleo.PISO_DIAS).</summary>
        public const double PISO_DIAS = 1.0 / 1440.0;
        private static readonly double INV_SQRT_2PI = 1.0 / Math.Sqrt(2.0 * Math.PI);

        public static double Fi(double x) => Math.Exp(-0.5 * x * x) * INV_SQRT_2PI;

        /// <summary>Black-76 sin descuento (nucleo.gamma76).</summary>
        public static double B76(double F, double K, double T, double iv)
        {
            if (!(F > 0) || !(K > 0) || !(T > 0) || !(iv > 0)) return 0.0;
            double v = iv * Math.Sqrt(T);
            double d1 = (Math.Log(F / K) + 0.5 * iv * iv * T) / v;
            return Fi(d1) / (F * v);
        }

        /// <summary>Black-Scholes sin dividendo (nucleo.gamma_bs, r = TASA por defecto).</summary>
        public static double BS(double S, double K, double T, double iv, double r = TASA)
        {
            if (!(S > 0) || !(K > 0) || !(T > 0) || !(iv > 0)) return 0.0;
            double v = iv * Math.Sqrt(T);
            double d1 = (Math.Log(S / K) + (r + 0.5 * iv * iv) * T) / v;
            return Fi(d1) / (S * v);
        }

        /// <summary>backtest_familia._gam: Black-76 si es futuro (NQ), Black-Scholes r 0.0375 si no (CBOE).</summary>
        public static double De(bool esFuturo, double S, double K, double T, double iv) => esFuturo ? B76(S, K, T, iv) : BS(S, K, T, iv, TASA);
    }
}
