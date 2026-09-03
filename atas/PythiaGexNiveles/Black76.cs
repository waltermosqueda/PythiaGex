using System;

namespace PythiaGex
{
    /// <summary>
    /// BLACK-76: EL MODELO QUE CORRESPONDE A LAS OPCIONES DE ES.
    ///
    /// Las opciones de ES son opciones SOBRE UN FUTURO, no sobre un contado.
    /// En Black-76 el subyacente es el futuro y no hay que arrastrar dividendos
    /// ni costo de acarreo: ya viven adentro del precio del futuro. Usar
    /// Black-Scholes sobre contado aca seria una aproximacion sin motivo,
    /// teniendo el modelo exacto a mano.
    ///
    /// Esta en su propio archivo, sin una sola referencia a ATAS, para que el
    /// arnes de prueba pueda compilar EXACTAMENTE este codigo. Un test que
    /// prueba una copia pegada del original no prueba nada: las dos se van
    /// separando y el test sigue pasando.
    /// </summary>
    public static class Black76
    {
        public const double R = 0.0375;

        public static double Fi(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);

        /// <summary>Normal acumulada. Abramowitz-Stegun 7.1.26, error &lt; 7,5e-8.</summary>
        public static double N(double x)
        {
            double s = x < 0 ? -1.0 : 1.0;
            x = Math.Abs(x) / Math.Sqrt(2.0);
            double t = 1.0 / (1.0 + 0.3275911 * x);
            double y = 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t
                        - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
            return 0.5 * (1.0 + s * y);
        }

        public static double Precio(double F, double K, double T, double s, bool esCall)
        {
            if (F <= 0 || K <= 0 || T <= 0 || s <= 0) return 0.0;
            double v = s * Math.Sqrt(T);
            double d1 = (Math.Log(F / K) + 0.5 * s * s * T) / v;
            double d2 = d1 - v;
            double desc = Math.Exp(-R * T);
            return esCall ? desc * (F * N(d1) - K * N(d2))
                          : desc * (K * N(-d2) - F * N(-d1));
        }

        /// <summary>Gamma de una opcion sobre futuro.</summary>
        public static double Gamma(double F, double K, double T, double s)
        {
            if (F <= 0 || K <= 0 || T <= 0 || s <= 0) return 0.0;
            double v = s * Math.Sqrt(T);
            double d1 = (Math.Log(F / K) + 0.5 * s * s * T) / v;
            return Math.Exp(-R * T) * Fi(d1) / (F * v);
        }

        /// <summary>
        /// Despeja la volatilidad implicita por biseccion.
        ///
        /// Biseccion y no Newton a proposito: cerca del vencimiento el vega se
        /// va a cero y Newton se dispara o no converge, y justamente el 0DTE es
        /// lo que mas interesa aca. La biseccion es mas lenta y no falla nunca.
        /// </summary>
        public static double DespejarIV(double precio, double F, double K, double T, bool esCall)
        {
            if (precio <= 0 || F <= 0 || K <= 0 || T <= 0) return double.NaN;

            // por debajo del valor intrinseco no hay volatilidad que lo explique
            double desc = Math.Exp(-R * T);
            double intr = esCall ? Math.Max(0.0, desc * (F - K)) : Math.Max(0.0, desc * (K - F));
            if (precio <= intr) return double.NaN;

            double lo = 1e-4, hi = 5.0;
            if (Precio(F, K, T, hi, esCall) < precio) return double.NaN;
            for (int i = 0; i < 60; i++)
            {
                double m = 0.5 * (lo + hi);
                if (Precio(F, K, T, m, esCall) < precio) lo = m; else hi = m;
            }
            double iv = 0.5 * (lo + hi);
            return (iv > 1e-3 && iv < 4.99) ? iv : double.NaN;
        }
    }
}
