using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ATAS.Indicators;

namespace PythiaGexTres
{
    /// <summary>
    /// TUNEL (3.3.0, 07-10-2026): la regla de dominantes NH_sig del laboratorio (laboratorio/tres/criterio_operador.py), pedida por el operador
    /// como "tunel y rebote": techo = el strike mas cercano ARRIBA del precio con |gexVol| >= umbral x el mayor |gexVol| del radio; piso = el
    /// mas cercano ABAJO. Histeresis: el par (techo, piso) se mantiene mientras el ultimo cierre siga adentro y los dos strikes sigan con
    /// >= umbral/2; cuando el precio cierra afuera (o un strike se cae), se vuelve a elegir. Strikes exactos, sin centroide.
    /// Lo medido con el criterio del operador esta en laboratorio/tres/resultados/criterio_operador_*.md; el default de la regla se decide ahi.
    /// </summary>
    /// <summary>Con que se arma el tunel (3.3.3).</summary>
    public enum TunelFuente3
    {
        [System.ComponentModel.Description("Cruces de signo de la gamma (vol + OI) a +-0,1 % del precio: el mas cercano arriba y el mas cercano abajo. Son las filas de la 2.0 (pedido 07-10 04:10)")] Cruces,
        [System.ComponentModel.Description("Strikes con fuerza (3.3.2): el mas cercano por lado con |gex| >= umbral del mayor del radio")] Strikes,
    }

    /// <summary>3.6.5: de donde sale la base de NDX (indice -> futuro NQ).</summary>
    public enum BaseNdx3
    {
        [System.ComponentModel.Description("Recta de forwards de la cadena (la de la 2.0)")] Forwards,
        [System.ComponentModel.Description("Mediana de la rueda (vela contra spot con 16 min de atraso)")] Rueda,
    }

    public partial class GammaHoyTres
    {
        [Display(Name = "Tunel: con que se arma", GroupName = "2. Lectura", Order = 23,
                 Description = "3.3.3. Cruces = techo y piso en los cruces de signo de la gamma mas cercanos al ultimo cierre (las filas de la 2.0, p. ej. 31484 / 31456), con histeresis mientras el precio cierre adentro. Strikes = el strike con fuerza mas cercano por lado (3.3.2; medido de noche 65,9 % vs 62,1 / 59,2 de las filas). Lo medido para los cruces mas cercanos: 62,1 % (vol) y 59,2 % (OI) de noche, 20 sesiones.")]
        public TunelFuente3 Tunel3Fuente { get; set; } = TunelFuente3.Cruces;

        [Display(Name = "Tunel: umbral de fuerza (% del mayor |gex| del radio)", GroupName = "2. Lectura", Order = 24,
                 Description = "3.3.0. Solo con la regla Tunel. Un strike cuenta como pared si su |gex por volumen| es al menos este % del mayor del radio. 10 % = tunel angosto que cambia seguido; 50 % = solo las paredes grandes (parecido a la clasica).")]
        [Range(5, 90)]
        public int Tunel3UmbralPct { get; set; } = 25;

        [Display(Name = "Tunel: histeresis (mantener el par mientras el precio siga adentro)", GroupName = "2. Lectura", Order = 25,
                 Description = "3.3.0. Solo con la regla Tunel. Prendido: el techo y el piso no cambian mientras el ultimo cierre de vela siga entre los dos y los dos sigan con al menos la mitad del umbral. Apagado: se reeligen en cada cuenta.")]
        public bool Tunel3Histeresis { get; set; } = true;

        [Display(Name = "Tunel de noche con la regla Tres", GroupName = "2. Lectura", Order = 26,
                 Description = "3.3.2. Con la regla Tres, fuera de la rueda de NY (antes de las 09:30 y desde las 16:00) las dominantes son el Tunel en vez de la clasica a secas. Medido con el criterio del operador (laboratorio/tres/criterio_operador.py, 20 sesiones, noche): 65,9 % de rebote en 587 toques contra 62,1 % y 59,2 % de las filas de la 2.0; rayas al azar 65 %. Apagado: la clasica a secas (63,3 %).")]
        public bool Tunel3DeNoche { get; set; } = true;

        private double _tunelTecho = double.NaN, _tunelPiso = double.NaN;
        private double _tunelTechoM = double.NaN, _tunelPisoM = double.NaN;   // estado propio del rebobinado (3.3.3)

        private static double FuerzaEn(List<GammaHoyNucleo.Strike> cand, double fut)
        {
            double mejor = 0; foreach (var s in cand) if (Math.Abs(s.Fut - fut) < 0.01) mejor = Math.Max(mejor, Math.Abs(s.GexVol));
            return mejor;
        }
        private static double GexEn(List<GammaHoyNucleo.Strike> cand, double fut)
        {
            foreach (var s in cand) if (Math.Abs(s.Fut - fut) < 0.01) return s.GexVol;
            return 0;
        }

        /// <summary>Desde el Tick, despues del nucleo: con la regla Tunel reemplaza L.Doms por (techo, piso). `cierre` = ultimo cierre de vela
        /// conocido (la histeresis mira cierres, no ticks, como en el laboratorio); si no hay, el precio.</summary>
        private void AplicarTunel(GammaHoyNucleo.Lectura L, double fut, double cierre, string raiz, bool memoria = false, DateTime? cuando = null)
        {
            try
            {
                var regla = ReglaEfectiva();
                DateTime t0 = cuando ?? DateTime.UtcNow;
                bool activa = regla == ReglaDominantes3.Tunel || (regla == ReglaDominantes3.Tres && Tunel3DeNoche && !Histeresis3.EsRueda(t0));   // 3.3.2
                if (L == null || !activa || L.Perfil == null || fut <= 0) return;
                if (AplicarFormulaDoms(L, fut, cierre, raiz, memoria)) return;   // 3.6.0: formula elegida (strikes con gamma grande en vez de cruces)
                if (Tunel3Fuente == TunelFuente3.Cruces && AplicarTunelCruces(L, fut, cierre, memoria)) return;   // 3.3.3: las filas de la 2.0; si no hay cruces, cae a los strikes
                double radio = Math.Min(fut * 0.02, RadioDominantes(raiz));
                var cand = L.Perfil.Where(s => s.Fut > 0 && Math.Abs(s.Fut - fut) <= radio && !double.IsNaN(s.GexVol) && s.GexVol != 0).ToList();
                if (cand.Count == 0) return;
                double mx = cand.Max(s => Math.Abs(s.GexVol)); if (mx <= 0) return;
                double q = Math.Max(0.05, Math.Min(0.9, Tunel3UmbralPct / 100.0));
                double ref0 = cierre > 0 ? cierre : fut;
                bool mantener = false;
                if (Tunel3Histeresis && !double.IsNaN(_tunelTecho) && !double.IsNaN(_tunelPiso))
                {
                    double ft = FuerzaEn(cand, _tunelTecho), fp = FuerzaEn(cand, _tunelPiso);
                    mantener = ft >= q * mx / 2 && fp >= q * mx / 2 && ref0 < _tunelTecho && ref0 > _tunelPiso;
                }
                if (!mantener)
                {
                    var arriba = cand.Where(s => s.Fut > ref0 && Math.Abs(s.GexVol) >= q * mx).OrderBy(s => s.Fut - ref0).FirstOrDefault();
                    var abajo = cand.Where(s => s.Fut < ref0 && Math.Abs(s.GexVol) >= q * mx).OrderByDescending(s => s.Fut).FirstOrDefault();
                    double techo = arriba != null ? arriba.Fut : double.NaN, piso = abajo != null ? abajo.Fut : double.NaN;
                    if ((techo != _tunelTecho && !(double.IsNaN(techo) && double.IsNaN(_tunelTecho))) || (piso != _tunelPiso && !(double.IsNaN(piso) && double.IsNaN(_tunelPiso))))
                        Log("tunel: techo " + (double.IsNaN(techo) ? "-" : techo.ToString("0.00")) + " piso " + (double.IsNaN(piso) ? "-" : piso.ToString("0.00")) + " (umbral " + Tunel3UmbralPct + " % de " + (mx / 1e6).ToString("0") + "M, cierre " + ref0.ToString("0.00") + ")");
                    _tunelTecho = techo; _tunelPiso = piso;
                }
                var doms = new List<(double Fut, double Gex)>();
                if (!double.IsNaN(_tunelTecho)) doms.Add((_tunelTecho, GexEn(cand, _tunelTecho)));
                if (!double.IsNaN(_tunelPiso)) doms.Add((_tunelPiso, GexEn(cand, _tunelPiso)));
                if (doms.Count > 0) L.Doms = doms;
            }
            catch (Exception e) { Registro.Excepcion(LOG, "AplicarTunel", e); }
        }

        /// <summary>3.3.3: tunel con los cruces de signo (vol + OI, +-0,1 % del precio, ya en precio del grafico). true si armo el par.</summary>
        private bool AplicarTunelCruces(GammaHoyNucleo.Lectura L, double fut, double cierre, bool memoria)
        {
            var zs = L.ZeroCrucesLista; if (zs == null || zs.Count == 0) return false;
            double ref0 = cierre > 0 ? cierre : fut;
            double techo = memoria ? _tunelTechoM : _tunelTecho, piso = memoria ? _tunelPisoM : _tunelPiso;
            bool mantener = Tunel3Histeresis && !double.IsNaN(techo) && !double.IsNaN(piso) && ref0 < techo && ref0 > piso;
            if (!mantener)
            {
                double nt = double.NaN, np = double.NaN;
                foreach (var z in zs) { if (z > ref0 && (double.IsNaN(nt) || z < nt)) nt = z; if (z < ref0 && (double.IsNaN(np) || z > np)) np = z; }
                if (double.IsNaN(nt) && double.IsNaN(np)) return false;
                if (!memoria && (Math.Abs((double.IsNaN(nt) ? -1 : nt) - (double.IsNaN(techo) ? -1 : techo)) > 0.01 || Math.Abs((double.IsNaN(np) ? -1 : np) - (double.IsNaN(piso) ? -1 : piso)) > 0.01))
                    Log("tunel(cruces): techo " + (double.IsNaN(nt) ? "-" : nt.ToString("0.00")) + " piso " + (double.IsNaN(np) ? "-" : np.ToString("0.00")) + " (cierre " + ref0.ToString("0.00") + ", " + zs.Count + " cruces a +-0,1 %)");
                techo = nt; piso = np;
                if (memoria) { _tunelTechoM = techo; _tunelPisoM = piso; } else { _tunelTecho = techo; _tunelPiso = piso; }
            }
            var doms = new List<(double Fut, double Gex)>();
            if (!double.IsNaN(techo)) doms.Add((techo, 1e6));
            if (!double.IsNaN(piso)) doms.Add((piso, -1e6));
            if (doms.Count > 0) L.Doms = doms;
            return doms.Count > 0;
        }

        /// <summary>Ultimo cierre de vela conocido del grafico (la vela anterior a la viva), 0 si no hay.</summary>
        private double UltimoCierre()
        {
            try { int b = CurrentBar - 2; if (b >= 0) { var cb = GetCandle(b); if (cb != null) return (double)cb.Close; } } catch { }
            return 0;
        }
    }
}
