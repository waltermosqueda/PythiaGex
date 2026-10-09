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
    /// 3.7.0: APAGADO por defecto de noche (Tunel3DeNocheB, D2: las dominantes son la regla de la 2.0); si se usa, C2 (un lado se mantiene
    /// solo si sigue existiendo un cruce a &lt;= 1 pt, con su valor actual y su fuerza real), C3 (cruces sin islas) y C1 (sin cruces por OI de
    /// anteayer). La eleccion vive en Seleccion37.TunelCruces (sin ATAS).
    /// </summary>
    /// <summary>Con que se arma el tunel (3.3.3).</summary>
    public enum TunelFuente3
    {
        [System.ComponentModel.Description("Cruces de signo de la gamma (vol + OI) a +-0,2 % del precio, sin islas: el mas cercano arriba y el mas cercano abajo. Son las filas de la 2.0 (pedido 07-10 04:10)")] Cruces,
        [System.ComponentModel.Description("Strikes con fuerza (3.3.2): el mas cercano por lado con |gex| >= umbral del mayor del radio")] Strikes,
    }

    /// <summary>3.7.0 C5: de donde sale la base de NDX (indice -> futuro NQ).</summary>
    public enum BaseNdx37
    {
        [System.ComponentModel.Description("Sincronizada (3.7.0): MNQ en cadena.ts - 900 s menos el spot vivo de 09:35-15:59 NY; de dia mediana de 24, de noche la rueda entera con el carry")] Sincronizada,
        [System.ComponentModel.Description("Recta de forwards de la cadena (base_cruda del bajador, la de la 2.0 y la 3.6.5): salta 2 pts de mediana entre bajadas")] Forwards,
    }

    public partial class GammaHoyTres
    {
        [Display(Name = "Tunel: con que se arma", GroupName = "2. Lectura", Order = 23,
                 Description = "3.3.3. Cruces = techo y piso en los cruces de signo de la gamma mas cercanos al ultimo cierre (las filas de la 2.0), con histeresis mientras el precio cierre adentro (3.7.0: y mientras el cruce exista a <= 1 pt). Strikes = el strike con fuerza mas cercano por lado (3.3.2).")]
        public TunelFuente3 Tunel3Fuente { get; set; } = TunelFuente3.Cruces;

        [Display(Name = "Tunel: umbral de fuerza (% del mayor |gex| del radio)", GroupName = "2. Lectura", Order = 24,
                 Description = "3.3.0. Solo con la regla Tunel. Un strike cuenta como pared si su |gex por volumen| es al menos este % del mayor del radio. 10 % = tunel angosto que cambia seguido; 50 % = solo las paredes grandes (parecido a la clasica).")]
        [Range(5, 90)]
        public int Tunel3UmbralPct { get; set; } = 25;

        [Display(Name = "Tunel: histeresis (mantener el par mientras el precio siga adentro)", GroupName = "2. Lectura", Order = 25,
                 Description = "3.3.0. Solo con la regla Tunel. Prendido: el techo y el piso no cambian mientras el ultimo cierre de vela siga entre los dos (3.7.0: y su cruce siga existiendo). Apagado: se reeligen en cada cuenta.")]
        public bool Tunel3Histeresis { get; set; } = true;

        [Display(Name = "Tunel de noche con la regla Tres", GroupName = "2. Lectura", Order = 26,
                 Description = "3.7.0 (D2): APAGADO por defecto y nombre nuevo (Tunel3DeNocheB) para pisar el 'true' guardado en el .ws: de noche las dominantes son la regla de la 2.0. Prendido (solo con la regla Tres): fuera de la rueda de NY las dominantes son el tunel. Medido (backtest_familia.md, H3): el tunel no retiene mas que uno puesto al azar.")]
        public bool Tunel3DeNocheB { get; set; } = false;

        private readonly Seleccion37.EstadoTunel _tunelSt = new Seleccion37.EstadoTunel(), _tunelStM = new Seleccion37.EstadoTunel();   // vivo / rebobinado (3.3.3)

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

        /// <summary>Desde SeleccionarDoms, despues del nucleo: con la regla Tunel (o Tres de noche con Tunel3DeNocheB) reemplaza L.Doms por
        /// (techo, piso). `cierre` = ultimo cierre de vela conocido; si no hay, el precio. pulso = sobre una copia del estado (no lo mueve).</summary>
        private void AplicarTunel(GammaHoyNucleo.Lectura L, double fut, double cierre, string raiz, bool memoria = false, DateTime? cuando = null, bool oiFresco = true, bool pulso = false)
        {
            try
            {
                var regla = ReglaEfectiva();
                DateTime t0 = cuando ?? DateTime.UtcNow;
                bool activa = regla == ReglaDominantes3.Tunel || (regla == ReglaDominantes3.Tres && Tunel3DeNocheB && !Histeresis3.EsRueda(t0));   // 3.3.2
                if (L == null || !activa || L.Perfil == null || fut <= 0) return;
                var st = pulso ? _tunelSt.Clon() : memoria ? _tunelStM : _tunelSt;
                if (AplicarFormulaDoms(L, fut, cierre, raiz, memoria, oiFresco, pulso)) return;   // 3.6.0: formula elegida (strikes con gamma grande en vez de cruces)
                if (Tunel3Fuente == TunelFuente3.Cruces && AplicarTunelCruces(L, fut, cierre, memoria, st, oiFresco, pulso)) return;   // 3.3.3: las filas de la 2.0; si no hay cruces, cae a los strikes
                double radio = Math.Min(fut * 0.02, RadioDominantes(raiz));
                var cand = L.Perfil.Where(s => s.Fut > 0 && Math.Abs(s.Fut - fut) <= radio && !double.IsNaN(s.GexVol) && s.GexVol != 0).ToList();
                if (cand.Count == 0) return;
                double mx = cand.Max(s => Math.Abs(s.GexVol)); if (mx <= 0) return;
                double q = Math.Max(0.05, Math.Min(0.9, Tunel3UmbralPct / 100.0));
                double ref0 = cierre > 0 ? cierre : fut;
                bool mantener = false;
                if (Tunel3Histeresis && !double.IsNaN(st.Techo) && !double.IsNaN(st.Piso))
                {
                    double ft = FuerzaEn(cand, st.Techo), fp = FuerzaEn(cand, st.Piso);
                    mantener = ft >= q * mx / 2 && fp >= q * mx / 2 && ref0 < st.Techo && ref0 > st.Piso;
                }
                if (!mantener)
                {
                    var arriba = cand.Where(s => s.Fut > ref0 && Math.Abs(s.GexVol) >= q * mx).OrderBy(s => s.Fut - ref0).FirstOrDefault();
                    var abajo = cand.Where(s => s.Fut < ref0 && Math.Abs(s.GexVol) >= q * mx).OrderByDescending(s => s.Fut).FirstOrDefault();
                    double techo = arriba != null ? arriba.Fut : double.NaN, piso = abajo != null ? abajo.Fut : double.NaN;
                    if (!memoria && !pulso && ((techo != st.Techo && !(double.IsNaN(techo) && double.IsNaN(st.Techo))) || (piso != st.Piso && !(double.IsNaN(piso) && double.IsNaN(st.Piso)))))
                        Log("tunel: techo " + (double.IsNaN(techo) ? "-" : techo.ToString("0.00")) + " piso " + (double.IsNaN(piso) ? "-" : piso.ToString("0.00")) + " (umbral " + Tunel3UmbralPct + " % de " + (mx / 1e6).ToString("0") + "M, cierre " + ref0.ToString("0.00") + ")");
                    st.Techo = techo; st.Piso = piso;
                }
                var doms = new List<(double Fut, double Gex)>();
                if (!double.IsNaN(st.Techo)) doms.Add((st.Techo, GexEn(cand, st.Techo)));
                if (!double.IsNaN(st.Piso)) doms.Add((st.Piso, GexEn(cand, st.Piso)));
                if (doms.Count > 0) { L.Doms = doms; L.LibroDom = "vol"; }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "AplicarTunel", e); }
        }

        /// <summary>3.3.3 + 3.7.0: tunel con los cruces de signo SIN ISLAS (C3), por volumen y, solo con el OI de ayer, por OI (C1), a +-0,2 % del
        /// precio; la eleccion con C2 (Seleccion37.TunelCruces: un lado vive solo si su cruce sigue a &lt;= 1 pt; Gex = fuerza real). true si armo el par.</summary>
        private bool AplicarTunelCruces(GammaHoyNucleo.Lectura L, double fut, double cierre, bool memoria, Seleccion37.EstadoTunel st, bool oiFresco, bool pulso)
        {
            double ref0 = cierre > 0 ? cierre : fut;
            double lim = (L.S > 0 ? L.S : fut) * 0.002;   // como las listas del nucleo (3.5.3)
            var cruces = Seleccion37.CrucesPerfil(L.Perfil, true, true).Where(c => Math.Abs(c.Z - fut) <= lim).ToList();
            if (oiFresco) cruces.AddRange(Seleccion37.CrucesPerfil(L.Perfil, false, true).Where(c => Math.Abs(c.Z - fut) <= lim));
            if (cruces.Count == 0) return false;
            double t0 = st.Techo, p0 = st.Piso;
            var doms = Seleccion37.TunelCruces(cruces, ref0, Tunel3Histeresis, st, out bool reeligio);
            if (doms == null || doms.Count == 0) return false;
            if (!memoria && !pulso && reeligio)
                Log("tunel(cruces): techo " + (double.IsNaN(st.Techo) ? "-" : st.Techo.ToString("0.00")) + " piso " + (double.IsNaN(st.Piso) ? "-" : st.Piso.ToString("0.00"))
                    + " (antes " + (double.IsNaN(t0) ? "-" : t0.ToString("0.00")) + "/" + (double.IsNaN(p0) ? "-" : p0.ToString("0.00")) + ", cierre " + ref0.ToString("0.00") + ", " + cruces.Count + " cruces sin islas" + (oiFresco ? "" : ", solo volumen: OI de anteayer") + ")");
            L.Doms = doms; L.LibroDom = "vol";
            return true;
        }

        /// <summary>Ultimo cierre de vela conocido del grafico (la vela anterior a la viva), 0 si no hay.</summary>
        private double UltimoCierre()
        {
            try { int b = CurrentBar - 2; if (b >= 0) { var cb = GetCandle(b); if (cb != null) return (double)cb.Close; } } catch { }
            return 0;
        }
    }
}
