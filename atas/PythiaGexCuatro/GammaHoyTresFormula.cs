using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Linq;
using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGexCuatro
{
    /// <summary>
    /// FORMULAS DE LAS DOMINANTES (3.6.0-3.6.1, 08-10-2026). Pedido del operador (01:20 ART): "la clasica y la 2.0 dibujaron 31.350/31.352 y el
    /// precio las respeto; la 3.0 dibuja rayas en el medio de las velas; quiero la formula para que las dominantes caigan en los EXTREMOS de las
    /// velas y despues rebote". Causa medida: de noche la 3.0 usaba el tunel por CRUCES DE SIGNO (3.3.3), que por construccion se pega al precio
    /// (estela 01:23: D1 31.364,54 / D2 31.359,55 con 1 M de GEX); la clasica y la 2.0 dibujan strikes con mucha gamma (120-156 M).
    /// (1) Doms3Formula: con que se eligen D1/D2 donde corre el tunel (de noche con la regla Tres), en vivo y en el rebobinado.
    /// (2) 3.6.1, "todo esto tambien quiero verlo y juzgarlo ya": cada formula dibujada A LA VEZ como capa propia (F1..F8), con su color, su
    ///     estela por vela (rebobinada desde la memoria al arrancar) y un rotulo chico. F7 = majors por OI (GammaHoyTresMajorOi.cs).
    /// Lo medido con el criterio del operador va a laboratorio/tres/resultados/extremos_*.md.
    /// </summary>
    public enum FormulaDoms3
    {
        [System.ComponentModel.Description("F1 Cruces de signo (3.3.3): el cruce mas cercano por lado. Se pega al precio")] Cruces,
        [System.ComponentModel.Description("F2 Una por lado por volumen: el strike con mas |GEX vol| arriba y abajo del precio en el radio (como la clasica)")] UnoPorLadoVol,
        [System.ComponentModel.Description("F3 Una por lado por OI: el strike con mas |GEX OI| arriba y abajo del precio en el radio")] UnoPorLadoOi,
        [System.ComponentModel.Description("F4 Una por lado, volumen + OI: el strike con mas |GEX vol| + |GEX OI| arriba y abajo")] UnoPorLadoVolOi,
        [System.ComponentModel.Description("F5 Muros por volumen: el mayor GEX de calls y el mayor de puts a +-100 pts (como la pagina)")] MurosVol,
        [System.ComponentModel.Description("F6 Muros por OI: el mayor GEX de calls y el mayor de puts por interes abierto a +-100 pts")] MurosOi,
        [System.ComponentModel.Description("F7 Majors por OI: el mayor GEX neto positivo y el mayor negativo por interes abierto")] MajorsOi,
        [System.ComponentModel.Description("F8 Las dos mas grandes por volumen (como la 2.0), sin importar el lado")] DosMasGrandesVol,
    }

    public partial class FamiliaCuatro
    {
        [Display(Name = "Dominantes: formula (donde corre el tunel)", GroupName = "9.2 3.0 · Lectura", Order = 22,
                 Description = "3.6.0. Con que se eligen D1 y D2 donde corre el tunel (de noche con la regla Tres). Cruces = lo de antes (se pega al precio). Las demas eligen strikes con gamma grande (como la clasica y la 2.0). Lo medido esta en laboratorio/tres/resultados/extremos_*.md.")]
        public FormulaDoms3 Doms3Formula { get; set; } = FormulaDoms3.Cruces;

        [Display(Name = "Dominantes: mantener si sigue siendo >= (% del mejor)", GroupName = "9.2 3.0 · Lectura", Order = 27,
                 Description = "3.6.0. Anti-parpadeo de la formula elegida: la dominante se mantiene mientras su fuerza siga siendo al menos este % de la mejor de su lado. 100 = se reelige siempre.")]
        [Range(50, 100)]
        public int Doms3MantenerPct { get; set; } = 80;

        // ---- 3.6.1: las formulas a la vez, para juzgarlas a ojo
        [Display(Name = "Ver las formulas a juzgar (F1..F8)", GroupName = "9.9 3.0 · Formulas a juzgar", Order = 1,
                 Description = "3.6.1. Cada formula de dominantes dibujada a la vez, con su color, su estela por vela y un rotulo chico. F7 (majors por OI) se prende en '7. Rayas una por una'.")]
        public bool Formulas3Ver { get; set; } = true;
        [Display(Name = "F1 cruce de signo mas cercano (blanco)", GroupName = "9.9 3.0 · Formulas a juzgar", Order = 11)] public bool Formula3F1 { get; set; } = true;
        [Display(Name = "F2 una por lado por volumen (naranja)", GroupName = "9.9 3.0 · Formulas a juzgar", Order = 12)] public bool Formula3F2b { get; set; } = false;   // 3.6.2: el operador se quedo con F1 y F5
        [Display(Name = "F3 una por lado por OI (magenta)", GroupName = "9.9 3.0 · Formulas a juzgar", Order = 13)] public bool Formula3F3b { get; set; } = false;   // 3.6.2: el operador se quedo con F1 y F5
        [Display(Name = "F4 una por lado vol + OI (violeta)", GroupName = "9.9 3.0 · Formulas a juzgar", Order = 14)] public bool Formula3F4b { get; set; } = false;   // 3.6.2: el operador se quedo con F1 y F5
        [Display(Name = "F5 muros de calls y puts por volumen (lima)", GroupName = "9.9 3.0 · Formulas a juzgar", Order = 15)] public bool Formula3F5 { get; set; } = true;
        [Display(Name = "F6 muros de calls y puts por OI (rosa)", GroupName = "9.9 3.0 · Formulas a juzgar", Order = 16)] public bool Formula3F6b { get; set; } = false;   // 3.6.2: el operador se quedo con F1 y F5
        [Display(Name = "F8 las dos mas grandes por volumen, como la 2.0 (celeste)", GroupName = "9.9 3.0 · Formulas a juzgar", Order = 18)] public bool Formula3F8b { get; set; } = false;   // 3.6.2: el operador se quedo con F1 y F5
        [Display(Name = "Formulas: hasta cuantos pts del precio", GroupName = "9.9 3.0 · Formulas a juzgar", Order = 20)]
        [Range(20, 400)]
        public int Formulas3RadioPts { get; set; } = 150;

        private static readonly FormulaDoms3[] FormulasJuzgar = { FormulaDoms3.Cruces, FormulaDoms3.UnoPorLadoVol, FormulaDoms3.UnoPorLadoOi, FormulaDoms3.UnoPorLadoVolOi, FormulaDoms3.MurosVol, FormulaDoms3.MurosOi, FormulaDoms3.DosMasGrandesVol };
        private static readonly Color[] ColFormula = { Color.FromArgb(235, 235, 235), Color.FromArgb(255, 152, 0), Color.FromArgb(224, 64, 251), Color.FromArgb(149, 117, 255), Color.FromArgb(174, 234, 0), Color.FromArgb(255, 105, 180), Color.FromArgb(64, 196, 255) };
        private static readonly string[][] NomFormula = { new[] { "F1 cruce", "F1 cruce" }, new[] { "F2 vol", "F2 vol" }, new[] { "F3 OI", "F3 OI" }, new[] { "F4 v+OI", "F4 v+OI" }, new[] { "F5 muroC vol", "F5 muroP vol" }, new[] { "F6 muroC OI", "F6 muroP OI" }, new[] { "F8 2.0", "F8 2.0" } };
        private bool FormulaVisible(int i) => Formulas3Ver && (i == 0 ? Formula3F1 : i == 1 ? Formula3F2b : i == 2 ? Formula3F3b : i == 3 ? Formula3F4b : i == 4 ? Formula3F5 : i == 5 ? Formula3F6b : Formula3F8b);

        private readonly Dictionary<DateTime, double[]> _estelaFormulas = new Dictionary<DateTime, double[]>();   // 2 niveles por formula de FormulasJuzgar
        private double _fTecho = double.NaN, _fPiso = double.NaN, _fTechoM = double.NaN, _fPisoM = double.NaN;

        private double RadioFormula(FormulaDoms3 f, double fut, string raiz) => f == FormulaDoms3.MurosVol || f == FormulaDoms3.MurosOi ? 100.0 : Math.Min(fut * 0.02, RadioDominantes(raiz));

        private static Func<GammaHoyNucleo.Strike, double> FuerzaFormula(FormulaDoms3 f)
        {
            switch (f)
            {
                case FormulaDoms3.UnoPorLadoOi: return s => Math.Abs(s.GexOi);
                case FormulaDoms3.UnoPorLadoVolOi: return s => Math.Abs(s.GexVol) + Math.Abs(s.GexOi);
                default: return s => Math.Abs(s.GexVol);
            }
        }

        /// <summary>3.6.1: los dos niveles de una formula, sin estado (sin anti-parpadeo). ref0 = ultimo cierre (o el precio).</summary>
        private (double A, double B) NivelesFormula(FormulaDoms3 f, GammaHoyNucleo.Lectura L, double fut, double ref0, string raiz)
        {
            if (L?.Perfil == null || L.Perfil.Count == 0 || ref0 <= 0) return (double.NaN, double.NaN);
            if (f == FormulaDoms3.Cruces)
            {
                double nt = double.NaN, np = double.NaN;
                if (L.ZeroCrucesLista != null) foreach (var z in L.ZeroCrucesLista) { if (z > ref0 && (double.IsNaN(nt) || z < nt)) nt = z; if (z < ref0 && (double.IsNaN(np) || z > np)) np = z; }
                return (nt, np);
            }
            if (f == FormulaDoms3.MajorsOi) return (L.MpOi, L.MnOi);
            double radio = RadioFormula(f, fut, raiz);
            var cand = L.Perfil.Where(s => s.Fut > 0 && Math.Abs(s.Fut - ref0) <= radio).ToList();
            if (cand.Count == 0) return (double.NaN, double.NaN);
            switch (f)
            {
                case FormulaDoms3.MurosVol:
                case FormulaDoms3.MurosOi:
                {
                    bool v = f == FormulaDoms3.MurosVol;
                    var c0 = cand.Where(s => (v ? s.GexVolC : s.GexOiC) > 0).OrderByDescending(s => v ? s.GexVolC : s.GexOiC).FirstOrDefault();
                    var p0 = cand.Where(s => (v ? s.GexVolP : s.GexOiP) < 0).OrderBy(s => v ? s.GexVolP : s.GexOiP).FirstOrDefault();
                    return (c0?.Fut ?? double.NaN, p0?.Fut ?? double.NaN);   // A = muro de calls, B = muro de puts (pueden estar del mismo lado)
                }
                case FormulaDoms3.DosMasGrandesVol:
                {
                    var dos = cand.Where(s => s.GexVol != 0).OrderByDescending(s => Math.Abs(s.GexVol)).Take(2).ToList();
                    return (dos.Count > 0 ? dos[0].Fut : double.NaN, dos.Count > 1 ? dos[1].Fut : double.NaN);
                }
                default:
                {
                    var F = FuerzaFormula(f);
                    var a = cand.Where(s => s.Fut > ref0 && F(s) > 0).OrderByDescending(F).FirstOrDefault();
                    var b = cand.Where(s => s.Fut < ref0 && F(s) > 0).OrderByDescending(F).FirstOrDefault();
                    return (a?.Fut ?? double.NaN, b?.Fut ?? double.NaN);
                }
            }
        }

        /// <summary>3.6.1: todas las formulas a juzgar, aplanadas (2 por formula), para la estela y el rebobinado.</summary>
        private double[] NivelesTodas(GammaHoyNucleo.Lectura L, double fut, double ref0, string raiz)
        {
            var r = new double[FormulasJuzgar.Length * 2];
            for (int i = 0; i < FormulasJuzgar.Length; i++)
            {
                (double a, double b) = (double.NaN, double.NaN);
                try { (a, b) = NivelesFormula(FormulasJuzgar[i], L, fut, ref0, raiz); } catch { }
                r[2 * i] = a; r[2 * i + 1] = b;
            }
            return r;
        }

        private void RegistrarFormulasVela(DateTime tVela, GammaHoyNucleo.Lectura L, double fut, double cierre)
        {
            try
            {
                if (L == null) return;
                var r = NivelesTodas(L, fut, cierre > 0 ? cierre : fut, Raiz());
                lock (_candado)
                {
                    _estelaFormulas[tVela] = r;
                    if (_estelaFormulas.Count > 6000) foreach (var k in _estelaFormulas.Keys.OrderBy(x => x).Take(_estelaFormulas.Count - 5000).ToList()) _estelaFormulas.Remove(k);
                }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "RegistrarFormulasVela", e); }
        }

        private void SembrarFormulas(Dictionary<DateTime, double[]> d) { lock (_candado) foreach (var kv in d) if (!_estelaFormulas.ContainsKey(kv.Key)) _estelaFormulas[kv.Key] = kv.Value; }

        /// <summary>3.6.1: la estela de cada formula (raya fina del ancho de la vela, en su color) y sus rotulos chicos (no entran al recuadro).</summary>
        private void PintarFormulas(RenderContext g, Func<int, int> XBar, int desde, int hasta, List<(int B, DateTime T)> horas, double fut,
                                    GammaHoyNucleo.Lectura L, Func<double, int> Y, Func<int, bool> EnPantalla, ExtraRotulos extra)
        {
            try
            {
                if (!Formulas3Ver) return;
                var est = new Dictionary<int, double[]>();
                lock (_candado) foreach (var (b, t) in horas) if (_estelaFormulas.TryGetValue(t, out var e0)) est[b] = e0;
                int ultima = CurrentBar - 1;
                double[] ahora = L != null ? NivelesTodas(L, fut, UltimoCierre() > 0 ? UltimoCierre() : fut, Raiz()) : null;
                if (ahora != null && ultima >= desde && ultima <= hasta) est[ultima] = ahora;
                int inicio = Estela3VelasAtras > 0 ? Math.Max(0, ultima - Estela3VelasAtras) : _barInicioSesion;
                int bw = 5;
                { int xa = XBar(desde), xb = XBar(hasta); if (hasta > desde && xa != int.MinValue && xb != int.MinValue) bw = Math.Max(3, (xb - xa) / Math.Max(1, hasta - desde)); }
                int ancho = Math.Max(3, bw - 1);
                double radio = Math.Max(20, Formulas3RadioPts);
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
                            if (double.IsNaN(p) || p <= 0 || Math.Abs(p - fut) > radio) continue;
                            int y = Y(p); if (!EnPantalla(y)) continue;
                            int alfa = b >= ultima - 30 ? 230 : 150; if (previa) alfa = alfa * 45 / 100;
                            g.FillRectangle(Color.FromArgb(AlfaAtravesada(alfa, p, b), ColFormula[i]), new Rectangle(x - ancho / 2, y - 1, ancho, 2));
                        }
                    }
                }
                if (ahora != null && extra != null)
                    for (int i = 0; i < FormulasJuzgar.Length; i++)
                    {
                        if (!FormulaVisible(i)) continue;
                        for (int k = 0; k < 2; k++)
                        {
                            double p = ahora[2 * i + k];
                            if (double.IsNaN(p) || p <= 0 || Math.Abs(p - fut) > radio) continue;
                            if (k == 1 && NomFormula[i][0] == NomFormula[i][1] && Math.Abs(p - ahora[2 * i]) < 0.01) continue;
                            extra.Rotulos.Add((NomFormula[i][k], p, ColFormula[i], ""));
                        }
                    }
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

        /// <summary>3.6.0: aplica Doms3Formula a D1/D2 (con anti-parpadeo). true si armo al menos una dominante (si no, sigue el camino viejo).</summary>
        private bool AplicarFormulaDoms(GammaHoyNucleo.Lectura L, double fut, double cierre, string raiz, bool memoria)
        {
            if (Doms3Formula == FormulaDoms3.Cruces || L?.Perfil == null || L.Perfil.Count == 0) return false;
            double ref0 = cierre > 0 ? cierre : fut;
            (double nt, double np) = NivelesFormula(Doms3Formula, L, fut, ref0, raiz);
            double techo = memoria ? _fTechoM : _fTecho, piso = memoria ? _fPisoM : _fPiso;
            if (Doms3Formula == FormulaDoms3.UnoPorLadoVol || Doms3Formula == FormulaDoms3.UnoPorLadoOi || Doms3Formula == FormulaDoms3.UnoPorLadoVolOi)
            {
                var F = FuerzaFormula(Doms3Formula);
                double radio = RadioFormula(Doms3Formula, fut, raiz);
                double Fz(double p) { var s = L.Perfil.FirstOrDefault(x => Math.Abs(x.Fut - p) < 0.01 && Math.Abs(x.Fut - ref0) <= radio); return s == null ? 0 : F(s); }
                double mantener = Math.Max(0.5, Math.Min(1.0, Doms3MantenerPct / 100.0));
                if (!double.IsNaN(techo) && techo > ref0 && !double.IsNaN(nt) && Fz(techo) >= mantener * Fz(nt)) nt = techo;
                if (!double.IsNaN(piso) && piso < ref0 && !double.IsNaN(np) && Fz(piso) >= mantener * Fz(np)) np = piso;
            }
            if (double.IsNaN(nt) && double.IsNaN(np)) return false;
            if (!memoria && (Math.Abs((double.IsNaN(nt) ? -1 : nt) - (double.IsNaN(techo) ? -1 : techo)) > 0.01 || Math.Abs((double.IsNaN(np) ? -1 : np) - (double.IsNaN(piso) ? -1 : piso)) > 0.01))
                Log("formula " + Doms3Formula + ": " + (double.IsNaN(nt) ? "-" : nt.ToString("0.00")) + " / " + (double.IsNaN(np) ? "-" : np.ToString("0.00")) + " (cierre " + ref0.ToString("0.00") + ")");
            if (memoria) { _fTechoM = nt; _fPisoM = np; } else { _fTecho = nt; _fPiso = np; }
            bool porOi = Doms3Formula == FormulaDoms3.UnoPorLadoOi || Doms3Formula == FormulaDoms3.MurosOi || Doms3Formula == FormulaDoms3.MajorsOi;
            double Gex(double p) { var s = L.Perfil.FirstOrDefault(x => Math.Abs(x.Fut - p) < 0.01); return s == null ? 0 : (porOi ? s.GexOi : s.GexVol); }
            var doms = new List<(double Fut, double Gex)>();
            if (!double.IsNaN(nt) && nt > 0) doms.Add((nt, Gex(nt)));
            if (!double.IsNaN(np) && np > 0 && (doms.Count == 0 || Math.Abs(np - doms[0].Fut) > 0.01)) doms.Add((np, Gex(np)));
            if (doms.Count == 0) return false;
            L.Doms = doms.OrderBy(d => Math.Abs(d.Fut - fut)).ToList();   // D1 = la mas cercana
            return true;
        }
    }
}
