using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Linq;
using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGexTres
{
    /// <summary>
    /// FORMULAS DE LAS DOMINANTES (3.6.0-3.6.1, 08-10-2026). Pedido del operador (01:20 ART): "la clasica y la 2.0 dibujaron 31.350/31.352 y el
    /// precio las respeto; la 3.0 dibuja rayas en el medio de las velas; quiero la formula para que las dominantes caigan en los EXTREMOS de las
    /// velas y despues rebote". Causa medida: de noche la 3.0 usaba el tunel por CRUCES DE SIGNO (3.3.3), que por construccion se pega al precio
    /// (estela 01:23: D1 31.364,54 / D2 31.359,55 con 1 M de GEX); la clasica y la 2.0 dibujan strikes con mucha gamma (120-156 M).
    /// (1) Doms3FormulaB: con que se eligen D1/D2 donde corre el tunel (3.7.0: DosMasGrandesVol, la regla de la 2.0), en vivo y en el rebobinado.
    /// (2) 3.6.1, "todo esto tambien quiero verlo y juzgarlo ya": cada formula dibujada A LA VEZ como capa propia (F1..F8), con su color, su
    ///     estela por vela (rebobinada desde la memoria al arrancar) y un rotulo chico. F7 = majors por OI (GammaHoyTresMajorOi.cs).
    /// 3.7.0: la cuenta de cada formula vive en Seleccion37.Formula (sin ATAS): F1 sin islas (C3) y sin cruces por OI de anteayer (C1);
    /// F3/F4/F6/F7 no existen con el OI de anteayer. Cada marca se dibuja a &lt;= 'Dominantes: radio de dibujo' del cierre de SU vela (D1).
    /// Lo medido con el criterio del operador va a laboratorio/tres/resultados/extremos_*.md y auditoria_0810/resultados/backtest_familia.md.
    /// </summary>
    public partial class GammaHoyTres
    {
        [Display(Name = "Dominantes: formula (donde corre el tunel)", GroupName = "2. Lectura", Order = 22,
                 Description = "3.7.0 (D2): DosMasGrandesVol por defecto (la regla de la 2.0) y nombre nuevo (Doms3FormulaB) para pisar el 'Cruces' guardado en el .ws. Solo cuenta donde corre el tunel (regla Tunel, o Tres de noche con 'Tunel de noche' prendido). Cruces = el cruce mas cercano por lado (se pega al precio).")]
        public FormulaDoms3 Doms3FormulaB { get; set; } = FormulaDoms3.DosMasGrandesVol;

        [Display(Name = "Dominantes: mantener si sigue siendo >= (% del mejor)", GroupName = "2. Lectura", Order = 27,
                 Description = "3.6.0. Anti-parpadeo de la formula elegida: la dominante se mantiene mientras su fuerza siga siendo al menos este % de la mejor de su lado. 100 = se reelige siempre.")]
        [Range(50, 100)]
        public int Doms3MantenerPct { get; set; } = 80;

        // ---- 3.6.1: las formulas a la vez, para juzgarlas a ojo
        [Display(Name = "Ver las formulas a juzgar (F1..F8)", GroupName = "9. Formulas a juzgar", Order = 1,
                 Description = "3.6.1. Cada formula de dominantes dibujada a la vez, con su color, su estela por vela y un rotulo chico. F7 (majors por OI) se prende en '7. Rayas una por una'. 3.7.0: radio = 'Dominantes: radio de dibujo' contra el cierre de cada vela.")]
        public bool Formulas3Ver { get; set; } = true;
        [Display(Name = "F1 cruce de signo mas cercano, sin islas (blanco)", GroupName = "9. Formulas a juzgar", Order = 11,
                 Description = "3.7.1: APAGADA por defecto y nombre nuevo (Formula3F1b) para pisar el 'true' del .ws: medido en cara_a_cara (3 sesiones, 1053 velas m2) F1 era lo que mas ruido metia, 23,4 rayas en el cuerpo de las velas cada 100 velas, y no le gana al azar. Queda como opcion.")]
        public bool Formula3F1b { get; set; } = false;
        [Display(Name = "F2 una por lado por volumen (naranja)", GroupName = "9. Formulas a juzgar", Order = 12)] public bool Formula3F2b { get; set; } = false;   // 3.6.2: el operador se quedo con F1 y F5
        [Display(Name = "F3 una por lado por OI (magenta)", GroupName = "9. Formulas a juzgar", Order = 13)] public bool Formula3F3b { get; set; } = false;   // 3.6.2: el operador se quedo con F1 y F5
        [Display(Name = "F4 una por lado vol + OI (violeta)", GroupName = "9. Formulas a juzgar", Order = 14)] public bool Formula3F4b { get; set; } = false;   // 3.6.2: el operador se quedo con F1 y F5
        [Display(Name = "F5 muros de calls y puts por volumen (lima)", GroupName = "9. Formulas a juzgar", Order = 15)] public bool Formula3F5 { get; set; } = true;
        [Display(Name = "F6 muros de calls y puts por OI (rosa)", GroupName = "9. Formulas a juzgar", Order = 16)] public bool Formula3F6b { get; set; } = false;   // 3.6.2: el operador se quedo con F1 y F5
        [Display(Name = "F8 las dos mas grandes por volumen, como la 2.0 (celeste)", GroupName = "9. Formulas a juzgar", Order = 18)] public bool Formula3F8b { get; set; } = false;   // 3.6.2: el operador se quedo con F1 y F5

        private static readonly FormulaDoms3[] FormulasJuzgar = Dibujo37.FormulasJuzgar;   // 3.7.0: el orden y los nombres viven en Dibujo37 (sin ATAS)
        private static readonly Color[] ColFormula = { Color.FromArgb(235, 235, 235), Color.FromArgb(255, 152, 0), Color.FromArgb(224, 64, 251), Color.FromArgb(149, 117, 255), Color.FromArgb(174, 234, 0), Color.FromArgb(255, 105, 180), Color.FromArgb(64, 196, 255) };
        private static readonly string[][] NomFormula = Dibujo37.NomFormula;
        private bool FormulaVisible(int i) => Formulas3Ver && (i == 0 ? Formula3F1b : i == 1 ? Formula3F2b : i == 2 ? Formula3F3b : i == 3 ? Formula3F4b : i == 4 ? Formula3F5 : i == 5 ? Formula3F6b : Formula3F8b);

        private readonly Dictionary<DateTime, double[]> _estelaFormulas = new Dictionary<DateTime, double[]>();   // 2 niveles por formula de FormulasJuzgar
        private double _fTecho = double.NaN, _fPiso = double.NaN, _fTechoM = double.NaN, _fPisoM = double.NaN;

        /// <summary>3.6.1 / 3.7.0: los dos niveles de una formula (Seleccion37.Formula, sin estado). ref0 = ultimo cierre (o el precio).</summary>
        private (double A, double B) NivelesFormula(FormulaDoms3 f, GammaHoyNucleo.Lectura L, double fut, double ref0, string raiz, bool oiFresco)
            => Seleccion37.Formula(f, L, fut, ref0, RadioDominantes(raiz), oiFresco);

        /// <summary>3.6.1: todas las formulas a juzgar, aplanadas (2 por formula), para la estela y el rebobinado.</summary>
        private double[] NivelesTodas(GammaHoyNucleo.Lectura L, double fut, double ref0, string raiz, bool oiFresco)
            => Dibujo37.NivelesFormulas(L, fut, ref0, RadioDominantes(raiz), oiFresco);   // 3.7.0: sin ATAS (Simulador37 llama lo mismo)

        private void RegistrarFormulasVela(DateTime tVela, GammaHoyNucleo.Lectura L, double fut, double cierre)
        {
            try
            {
                if (L == null) return;
                var r = NivelesTodas(L, fut, cierre > 0 ? cierre : fut, Raiz(), _oiNqFresco);
                lock (_candado)
                {
                    _estelaFormulas[tVela] = r;
                    if (_estelaFormulas.Count > 6000) foreach (var k in _estelaFormulas.Keys.OrderBy(x => x).Take(_estelaFormulas.Count - 5000).ToList()) _estelaFormulas.Remove(k);
                }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "RegistrarFormulasVela", e); }
        }

        private void SembrarFormulas(Dictionary<DateTime, double[]> d) { lock (_candado) foreach (var kv in d) if (!_estelaFormulas.ContainsKey(kv.Key)) _estelaFormulas[kv.Key] = kv.Value; }

        /// <summary>3.6.1: la estela de cada formula (raya fina del ancho de la vela, en su color) y sus rotulos chicos (no entran al recuadro).
        /// 3.7.0 D1: cada marca solo a &lt;= radio del cierre de SU vela; los rotulos lejanos los lleva el borde.</summary>
        private void PintarFormulas(RenderContext g, Func<int, int> XBar, int desde, int hasta, List<(int B, DateTime T)> horas, double fut,
                                    GammaHoyNucleo.Lectura L, Func<double, int> Y, Func<int, bool> EnPantalla, ExtraRotulos extra, Func<int, double, bool> enRadio)
        {
            try
            {
                if (!Formulas3Ver) return;
                var est = new Dictionary<int, double[]>();
                lock (_candado) foreach (var (b, t) in horas) if (_estelaFormulas.TryGetValue(t, out var e0)) est[b] = e0;
                int ultima = CurrentBar - 1;
                string raiz = Raiz();
                double[] ahora = L != null ? NivelesTodas(L, fut, UltimoCierre() > 0 ? UltimoCierre() : fut, raiz, _oiNqFresco) : null;
                if (ahora != null && ultima >= desde && ultima <= hasta) est[ultima] = ahora;
                int inicio = Estela3VelasAtras > 0 ? Math.Max(0, ultima - Estela3VelasAtras) : _barInicioSesion;
                int bw = 5;
                { int xa = XBar(desde), xb = XBar(hasta); if (hasta > desde && xa != int.MinValue && xb != int.MinValue) bw = Math.Max(3, (xb - xa) / Math.Max(1, hasta - desde)); }
                int ancho = Math.Max(3, bw - 1);
                foreach (var kv in est)
                {
                    int b = kv.Key; if (b < desde || b > hasta) continue;
                    bool previa = b < inicio; if (previa && !Estela3SesionAnterior) continue;
                    int x = XBar(b); if (x == int.MinValue) continue;
                    for (int i = 0; i < FormulasJuzgar.Length; i++)
                    {
                        if (!FormulaVisible(i)) continue;
                        for (int k = 0; k < 2; k++)
                        {
                            int j = 2 * i + k; if (j >= kv.Value.Length) continue;
                            double p = kv.Value[j];
                            if (double.IsNaN(p) || p <= 0 || !enRadio(b, p)) continue;   // 3.7.0 D1
                            int y = Y(p); bool vis = EnPantalla(y);   // 3.7.2: se encola igual (la regla de la vela cuenta lo de fuera de pantalla)
                            int alfa = b >= ultima - 30 ? 230 : 150; if (previa) alfa = alfa * 45 / 100;
                            var col = ColFormula[i];
                            // 3.7.1 (A5): se encola con su nombre (F5 muroC vol...); una sola marca por (libro, strike) en la vela
                            EncolarMarca(b, raiz, NomFormula[i][k], p, vis, () => g.FillRectangle(Color.FromArgb(AlfaAtravesada(alfa, p, b), col), new Rectangle(x - ancho / 2, y - 1, ancho, 2)));
                        }
                    }
                }
                if (ahora != null && extra != null)
                    foreach (var r in Dibujo37.RotulosFormulas(ahora, FormulaVisible, raiz))   // 3.7.0: sin ATAS (Simulador37)
                        extra.Rotulos.Add(new Rot37(r, ColFormula[int.Parse(r.Clase.Substring(1), System.Globalization.CultureInfo.InvariantCulture)]));
            }
            catch (Exception e) { Registro.Excepcion(LOG, "PintarFormulas", e); }
        }

        /// <summary>3.6.1: desde el rebobinado, TODAS las fotos de viva3 (aunque el archivo de estela mande en D1/D2): a cada vela el ultimo punto
        /// anterior a su cierre (vida 5 min) siembra la estela de las formulas, de los majors por OI y de los cruces por OI.</summary>
        private void SembrarFormulasDesde(List<PuntoMem> viva, List<VelaMem> velas)
        {
            try
            {
                if (viva == null || viva.Count == 0 || velas == null) return;
                var pts = viva.OrderBy(p => p.T).ToList(); var ts = pts.Select(p => p.T).ToList();
                var dF = new Dictionary<DateTime, double[]>(); var dM = new Dictionary<DateTime, double[]>(); var dO = new Dictionary<DateTime, double[]>();
                foreach (var v in velas)
                {
                    int i = ts.BinarySearch(v.TClose); if (i < 0) i = ~i; i--;
                    if (i < 0 || (v.TClose - ts[i]).TotalSeconds > VIDA_MEMORIA_S) continue;
                    var p = pts[i];
                    if (p.Formulas != null) dF[v.TOpen] = p.Formulas;
                    if (p.MpOi > 0 || p.MnOi > 0) dM[v.TOpen] = new[] { p.MpOi, p.MnOi };
                    if (p.CrucesOi != null) dO[v.TOpen] = p.CrucesOi;
                }
                SembrarFormulas(dF); SembrarMajorOi(dM); SembrarCrucesOi(dO);
                Log("memoria: formulas F1..F8 sembradas en " + dF.Count + " velas, majors OI en " + dM.Count + ", cruces OI en " + dO.Count + " (de " + viva.Count + " fotos viva3)");
            }
            catch (Exception e) { Registro.Excepcion(LOG, "SembrarFormulasDesde", e); }
        }

        /// <summary>3.6.0: aplica Doms3FormulaB a D1/D2 (con anti-parpadeo). true si armo al menos una dominante (si no, sigue el camino viejo).
        /// pulso = sin mover el estado del anti-parpadeo.</summary>
        private bool AplicarFormulaDoms(GammaHoyNucleo.Lectura L, double fut, double cierre, string raiz, bool memoria, bool oiFresco = true, bool pulso = false)
        {
            if (Doms3FormulaB == FormulaDoms3.Cruces || L?.Perfil == null || L.Perfil.Count == 0) return false;
            double ref0 = cierre > 0 ? cierre : fut;
            (double nt, double np) = NivelesFormula(Doms3FormulaB, L, fut, ref0, raiz, oiFresco);
            double techo = memoria ? _fTechoM : _fTecho, piso = memoria ? _fPisoM : _fPiso;
            if (Doms3FormulaB == FormulaDoms3.UnoPorLadoVol || Doms3FormulaB == FormulaDoms3.UnoPorLadoOi || Doms3FormulaB == FormulaDoms3.UnoPorLadoVolOi)
            {
                Func<GammaHoyNucleo.Strike, double> F = Doms3FormulaB == FormulaDoms3.UnoPorLadoOi ? (s => Math.Abs(s.GexOi))
                    : Doms3FormulaB == FormulaDoms3.UnoPorLadoVolOi ? (s => Math.Abs(s.GexVol) + Math.Abs(s.GexOi)) : (Func<GammaHoyNucleo.Strike, double>)(s => Math.Abs(s.GexVol));
                double radio = Seleccion37.RadioDominantes(fut, RadioDominantes(raiz));
                double Fz(double p) { var s = L.Perfil.FirstOrDefault(x => Math.Abs(x.Fut - p) < 0.01 && Math.Abs(x.Fut - ref0) <= radio); return s == null ? 0 : F(s); }
                double mantener = Math.Max(0.5, Math.Min(1.0, Doms3MantenerPct / 100.0));
                if (!double.IsNaN(techo) && techo > ref0 && !double.IsNaN(nt) && Fz(techo) >= mantener * Fz(nt)) nt = techo;
                if (!double.IsNaN(piso) && piso < ref0 && !double.IsNaN(np) && Fz(piso) >= mantener * Fz(np)) np = piso;
            }
            if (double.IsNaN(nt) && double.IsNaN(np)) return false;
            if (!memoria && !pulso && (Math.Abs((double.IsNaN(nt) ? -1 : nt) - (double.IsNaN(techo) ? -1 : techo)) > 0.01 || Math.Abs((double.IsNaN(np) ? -1 : np) - (double.IsNaN(piso) ? -1 : piso)) > 0.01))
                Log("formula " + Doms3FormulaB + ": " + (double.IsNaN(nt) ? "-" : nt.ToString("0.00")) + " / " + (double.IsNaN(np) ? "-" : np.ToString("0.00")) + " (cierre " + ref0.ToString("0.00") + ")");
            if (!pulso) { if (memoria) { _fTechoM = nt; _fPisoM = np; } else { _fTecho = nt; _fPiso = np; } }
            bool porOi = Doms3FormulaB == FormulaDoms3.UnoPorLadoOi || Doms3FormulaB == FormulaDoms3.MurosOi || Doms3FormulaB == FormulaDoms3.MajorsOi;
            double Gex(double p) { var s = L.Perfil.FirstOrDefault(x => Math.Abs(x.Fut - p) < 0.01); return s == null ? 0 : (porOi ? s.GexOi : s.GexVol); }
            var doms = new List<(double Fut, double Gex)>();
            if (!double.IsNaN(nt) && nt > 0) doms.Add((nt, Gex(nt)));
            if (!double.IsNaN(np) && np > 0 && (doms.Count == 0 || Math.Abs(np - doms[0].Fut) > 0.01)) doms.Add((np, Gex(np)));
            if (doms.Count == 0) return false;
            L.Doms = Seleccion37.PorCercania(doms, fut);   // D1 = la mas cercana
            L.LibroDom = porOi ? "OI" : "vol";
            return true;
        }
    }
}
