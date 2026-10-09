using System;
using System.ComponentModel.DataAnnotations;
using ATAS.Indicators;

namespace PythiaGexCuatro
{
    /// <summary>
    /// RAYAS ATRAVESADAS (3.6.4, 08-10-2026). Pedido del operador (02:50 ART) sobre 00:50-01:30: "en 31.372 no esta graficado bien; las rayas del
    /// medio si estan bien dibujadas pero no sirven ahi; la 2.0 lo dibujo mejor y con menos ruido". Medido esa ventana: el techo 31.371-31.376
    /// (cruce por volumen 31.372,6 y zero de QQQ 31.375,7) solo lo tocaron las MECHAS; los cruces del medio (31.359,6 y 31.364,5 por OI, 31.366,3
    /// por volumen, este ultimo el MAS FUERTE: 86 M contra 60 M) los atravesaron los CUERPOS una y otra vez. La fuerza del cruce no los separa;
    /// lo que el precio hizo con ellos, si.
    /// Regla (solo mira velas ANTERIORES, no adelanta nada): en la vela b, una raya en p se dibuja tenue si en las N velas previas (b-N..b-1) hubo
    /// K o mas con p adentro del cuerpo (entre apertura y cierre). Es LIMPIEZA VISUAL, no acierto: si sirve para acertar se mide en
    /// laboratorio/tres/atravesadas.py (pre-registrado) y se dice.
    /// </summary>
    public partial class FamiliaCuatro
    {
        [Display(Name = "Atenuar rayas atravesadas por los cuerpos", GroupName = "9.3 3.0 · Pantalla", Order = 40,
                 Description = "3.6.4, APAGADO desde la 3.6.7. Medido (laboratorio/tres/atravesadas.py, 22 sesiones, pre-registrado y verificado): las rayas atravesadas rinden IGUAL (F1) o MEJOR (F5: rebote 77 % vs 64 % de noche) que las no atravesadas, y el filtro llega tarde (en el caso 31.372 apaga el medio 16 min despues de empezar el serrucho). Solo limpieza visual, y esconde rayas que funcionan igual o mejor.")]
        public bool Atravesadas3AtenuarB { get; set; } = false;

        [Display(Name = "Atravesadas: velas hacia atras (N)", GroupName = "9.3 3.0 · Pantalla", Order = 41)]
        [Range(5, 60)]
        public int Atravesadas3Velas { get; set; } = 20;

        [Display(Name = "Atravesadas: cuerpos que la cruzan (K)", GroupName = "9.3 3.0 · Pantalla", Order = 42)]
        [Range(1, 10)]
        public int Atravesadas3Cuerpos { get; set; } = 3;

        [Display(Name = "Atravesadas: opacidad (%)", GroupName = "9.3 3.0 · Pantalla", Order = 43, Description = "0 = no se dibujan.")]
        [Range(0, 100)]
        public int Atravesadas3AlfaPct { get; set; } = 20;

        // apertura y cierre de las velas visibles (y N antes), armados una vez por render
        private int _atrDesde = -1;
        private double[] _atrO, _atrC;

        private void PrepararAtravesadas(int desde, int hasta)
        {
            _atrO = null;
            if (!Atravesadas3AtenuarB) return;
            try
            {
                int n = Math.Max(5, Math.Min(60, Atravesadas3Velas));
                int a = Math.Max(0, desde - n), b = Math.Min(CurrentBar - 1, hasta);
                if (b < a) return;
                var o = new double[b - a + 1]; var c = new double[b - a + 1];
                for (int i = a; i <= b; i++)
                {
                    try { var v = GetCandle(i); o[i - a] = (double)v.Open; c[i - a] = (double)v.Close; }
                    catch { o[i - a] = double.NaN; c[i - a] = double.NaN; }
                }
                _atrDesde = a; _atrO = o; _atrC = c;
            }
            catch { _atrO = null; }
        }

        /// <summary>La opacidad de una marca de estela en el nivel p de la vela 'bar': la misma, o tenue si la raya fue atravesada.</summary>
        private int AlfaAtravesada(int alfa, double p, int bar)
        {
            var o = _atrO; var c = _atrC;
            if (!Atravesadas3AtenuarB || o == null || double.IsNaN(p)) return alfa;
            int n = Math.Max(5, Math.Min(60, Atravesadas3Velas)), k = Math.Max(1, Math.Min(10, Atravesadas3Cuerpos)), cnt = 0;
            for (int i = bar - n; i < bar; i++)
            {
                int j = i - _atrDesde; if (j < 0 || j >= o.Length) continue;
                double a = o[j], b = c[j]; if (double.IsNaN(a) || double.IsNaN(b)) continue;
                if (p > Math.Min(a, b) && p < Math.Max(a, b) && ++cnt >= k)
                    return alfa * Math.Max(0, Math.Min(100, Atravesadas3AlfaPct)) / 100;
            }
            return alfa;
        }
    }
}
