using System;
using System.Collections.Generic;
using System.Linq;

namespace PythiaGex
{
    /// <summary>
    /// GATILLO MODELO (ES, velas de 1 minuto): regresion logistica ajustada el 2026-09-10 sobre
    /// 13 dias de MES por minuto (laboratorio/ml_check.py --reducido --exportar), que predice si
    /// el precio toca +3 antes que -3 puntos en los 10 minutos siguientes.
    ///
    /// Diez rasgos, EXACTAMENTE como en el laboratorio (si se cambia uno hay que reajustar):
    ///   ret15/rt   cambio del cierre en 15 velas, en rangos tipicos (rt = mediana del rango de las 60 velas previas)
    ///   d_zero     (zero gamma por volumen - cierre) / rt
    ///   rango/rt   rango de la vela / rt
    ///   d_mn       (major negativo - cierre) / rt          (-40 si no hay)
    ///   d_dom_arr  (dominante de arriba - cierre) / rt     (+40 si no hay)
    ///   cum15      delta acumulado de 16 velas, comprimido: cum / (|cum| + 500)
    ///   d_mc30     (strike del Max Change a 30 min - cierre) / rt   (0 si no hay)
    ///   dz         delta de la vela / desvio de los deltas de las 60 previas (0 si hay menos de 20)
    ///   d_mp       (major positivo - cierre) / rt           (+40 si no hay)
    ///   d_dom_aba  (dominante de abajo - cierre) / rt       (-40 si no hay)
    ///
    /// LO MEDIDO (fuera de muestra por bloques de dias y en avance hacia adelante):
    ///   AUC 0,57; operando solo cuando p >= 0,70 o p <= 0,30: 61 % de acierto con la regla
    ///   simetrica (+3 antes que -3 en 10 min; moneda 50 %); en la tarde de Nueva York (14-16 h)
    ///   83 % de 59 casos, 7 por dia. Con los niveles permutados el modelo pierde la señal: la
    ///   ventaja es la INTERACCION entre el momentum corto y la geometria de los niveles.
    ///   En NQ NO hay señal (AUC 0,51): no se usa.
    ///   Es una ventaja chica (+0,6 a +1,9 pts brutos por operacion); el objetivo es 3 pts, no
    ///   un movimiento grande. La regla 2:1 no funciona (31,6 %).
    /// NADA DE ESTO ES UNA PROMESA: cada disparo se registra y el laboratorio lo juzga con los
    /// dias nuevos, que nunca se usaron para ajustar.
    /// </summary>
    public sealed class GatilloModelo
    {
        public const string Version = "MES_10min_2026-09-10";
        public const double G = 3.0, H = 10;
        static readonly string[] Rasgos = { "ret15/rt", "d_zero", "rango/rt", "d_mn", "d_dom_arr", "cum15", "d_mc30", "dz", "d_mp", "d_dom_aba" };
        static readonly double[] Media = { -0.0114, -2.3426, 1.0194, -8.0738, 12.0941, -0.0733, 1.4828, -0.0341, 6.2616, -20.9202 };
        static readonly double[] Escala = { 2.5417, 7.8334, 0.4459, 8.0235, 15.71, 0.6618, 5.5771, 1.023, 6.4654, 18.8736 };
        static readonly double[] Coef = { 0.176, 0.1786, -0.0857, 0.1194, -0.1015, 0.1064, 0.0721, -0.0165, 0.004, 0.0826 };
        const double Intercepto = -0.0178;

        public double Umbral = 0.70;
        public int Enfriamiento = 5;

        private readonly List<double> _cierres = new(), _deltas = new(), _rangos = new();
        private int _ultimoBar = -1, _ultimoDisparo = -10000;

        public sealed class Salida { public double P; public int Lado; public double[] Rasgos; public string Detalle = ""; }

        /// <summary>Una vela cerrada (en orden). Devuelve la probabilidad y, si supera el umbral
        /// (y paso el enfriamiento), el lado. Los niveles van en precio del futuro; NaN si no hay.</summary>
        public Salida Procesar(int bar, double o, double h, double l, double c, double delta,
                               double zero, double mp, double mn, double domArr, double domAba, double mc30)
        {
            var s = new Salida { P = double.NaN, Lado = 0 };
            if (bar <= _ultimoBar || c <= 0) return s;
            _ultimoBar = bar;
            try
            {
                double rango = h - l;
                double rt;
                if (_rangos.Count >= 20) { var ord = _rangos.OrderBy(x => x).ToList(); rt = ord[ord.Count / 2]; if (rt <= 0) rt = 0.25; }
                else rt = rango > 0 ? rango : 0.25;
                double dz = 0;
                if (_deltas.Count >= 20)
                {
                    double m = _deltas.Average(); double sd = Math.Sqrt(_deltas.Sum(x => (x - m) * (x - m)) / _deltas.Count);
                    dz = delta / (sd > 0 ? sd : 1.0);
                }
                double cum = _deltas.Skip(Math.Max(0, _deltas.Count - 15)).Sum() + delta;
                double ret15 = _cierres.Count >= 15 ? c - _cierres[_cierres.Count - 15] : (_cierres.Count > 0 ? c - _cierres[0] : 0.0);
                double D(double x, double sinNivel) => double.IsNaN(x) || x <= 0 ? sinNivel : (x - c) / rt;
                var f = new double[] {
                    ret15 / rt, D(zero, 0.0), rango / rt, D(mn, -40.0), D(domArr, 40.0), cum / (Math.Abs(cum) + 500.0),
                    D(mc30, 0.0), dz, D(mp, 40.0), D(domAba, -40.0) };
                double logit = Intercepto;
                for (int i = 0; i < f.Length; i++) logit += Coef[i] * (f[i] - Media[i]) / Escala[i];
                double p = 1.0 / (1.0 + Math.Exp(-logit));
                s.P = p; s.Rasgos = f;
                s.Detalle = string.Join(" ", Rasgos.Select((n, i) => n + "=" + f[i].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
                if (bar - _ultimoDisparo >= Enfriamiento)
                {
                    if (p >= Umbral) { s.Lado = 1; _ultimoDisparo = bar; }
                    else if (p <= 1 - Umbral) { s.Lado = -1; _ultimoDisparo = bar; }
                }
            }
            finally
            {
                _cierres.Add(c); _deltas.Add(delta); _rangos.Add(h - l);
                if (_cierres.Count > 60) { _cierres.RemoveAt(0); _deltas.RemoveAt(0); _rangos.RemoveAt(0); }
            }
            return s;
        }
    }
}
