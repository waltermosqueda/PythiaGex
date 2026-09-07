using System;
using System.Collections.Generic;
using System.Linq;

namespace PythiaGex
{
    /// <summary>
    /// LO QUE GAMMA VIVO ANOTA, SIN ATAS.
    ///
    /// Gamma Vivo (GammaVivo.cs) tiene la cuenta pegada al indicador: 5.600
    /// lineas con dibujo, chips, zonas del radar y la cadena viva de Rithmic.
    /// Separarle el nucleo entero es otra obra. Lo que SI se puede reproducir
    /// letra por letra, porque son bloques cerrados y estan copiados de ahi,
    /// es lo que ese indicador anota en su centinela y por lo tanto lo que el
    /// laboratorio juzga:
    ///
    ///   zero      cruce por cero del GEX por interes abierto (grilla +-3 %,
    ///             60 pasos, filas con dias <= DiasMax), en precio del futuro
    ///   wall_pos  el strike de mayor GEX por encima del spot (si no hay, el global)
    ///   wall_neg  el strike de menor GEX por debajo del spot (idem)
    ///   dom0..7   los "picos": ModoDominante = 1 (el estandar): cada strike con
    ///             |GEX| >= 15 % del maximo, ordenados por |GEX|, hasta 14
    ///             (el centinela anota los primeros 8)
    ///
    /// Libro del indice (SPX -> Black-Scholes). La rama de opciones de ES por
    /// Rithmic (Black-76, multiplicador del futuro) no entra porque el
    /// simulador no tiene esa cadena. Si GammaVivo.cs cambia estos bloques,
    /// hay que cambiarlos aca: la referencia son las lineas citadas.
    /// </summary>
    public sealed class GammaVivoNucleo
    {
        public sealed class Ajustes
        {
            public double Tasa = 0.0375;
            public int DiasMax = 7;                 // GammaVivo.DiasMax
            public int MaxDominantes = 14;          // GammaVivo.MaxDominantes
            public int MinFuerzaDominante = 15;     // % del maximo (GammaVivo.MinFuerzaDominante)
            public int ModoDominante = 1;           // 0 centroide, 1 strike, 2 pico interpolado
            public double RadioCentroide = 15;      // modo 0
            public double SeparacionPuntos = 4;     // modos 0 y 2
        }

        public sealed class Nivel { public double K, Fut, Gex; }

        public sealed class Lectura
        {
            public bool SinBase;
            public double S, Base;
            public string BaseOrigen = "";
            public List<Nivel> Perfil = new();
            public double Zero = double.NaN, WallPos = double.NaN, WallNeg = double.NaN;
            public double[] Picos = new double[0];
            public double MaxGex, NetGex;
        }

        public Ajustes A = new();
        private const double MULT = 100.0;
        private const double PISO_DIAS = 1.0 / 1440.0;

        public Lectura Calcular(Feed.Cadena c, double futuro)
        {
            if (c == null || c.Filas.Count == 0 || c.Dias == null || c.Dias.Length == 0 || futuro <= 0) return null;
            var L = new Lectura();
            double baseUsada;
            if (c.BaseConfiable && c.Base != 0) { baseUsada = c.Base; L.BaseOrigen = "medida"; }
            else if (c.BaseUltimaBuena != 0 && c.BaseUltimaBuenaEdad <= 360) { baseUsada = c.BaseUltimaBuena; L.BaseOrigen = "medida hace " + c.BaseUltimaBuenaEdad.ToString("0") + " min"; }
            else if (c.BaseCruda != 0) { baseUsada = c.BaseCruda; L.BaseOrigen = "CRUDA"; }
            else { L.SinBase = true; L.BaseOrigen = "sin base"; return L; }
            double S = futuro - baseUsada;
            if (S <= 0) return null;
            double r = A.Tasa;
            L.S = S; L.Base = baseUsada;

            // horizonte efectivo: DiasMax, salvo que lo mas cercano este mas lejos (GammaVivo 1799-1805)
            double horizonte = A.DiasMax;
            double masCerca = double.MaxValue;
            foreach (var d in c.Dias) if (d >= 0 && d < masCerca) masCerca = d;
            if (masCerca != double.MaxValue && masCerca > horizonte) horizonte = masCerca;

            // el perfil por strike, GEX por interes abierto (GammaVivo GexStrike, 1658-1684)
            var por = new Dictionary<double, Nivel>();
            foreach (var f in c.Filas)
            {
                if (f.V < 0 || f.V >= c.Dias.Length) continue;
                double dias = c.Dias[f.V];
                if (dias < 0 || dias > horizonte) continue;
                double T = Math.Max(dias, PISO_DIAS) / 365.0;
                double g = (GammaHoyNucleo.GammaBs(S, f.K, T, f.IvC, r) * f.OiC - GammaHoyNucleo.GammaBs(S, f.K, T, f.IvP, r) * f.OiP) * MULT * S * S * 0.01;
                if (g == 0) continue;
                if (!por.TryGetValue(f.K, out var n)) { n = new Nivel { K = f.K, Fut = f.K + baseUsada }; por[f.K] = n; }
                n.Gex += g;
            }
            var perfil = por.Values.OrderBy(n => n.K).ToList();
            L.Perfil = perfil;
            double mx = perfil.Count > 0 ? perfil.Max(n => Math.Abs(n.Gex)) : 0;
            L.MaxGex = mx; L.NetGex = perfil.Sum(n => n.Gex);

            // zero: grilla +-3 %, 60 pasos, filas con dias <= DiasMax (GammaVivo CruceCero, 1628-1656)
            L.Zero = Cruce(c, S, r) ;
            if (!double.IsNaN(L.Zero)) L.Zero += baseUsada;

            // muros (GammaVivo 2019-2046): el mayor GEX arriba del spot y el menor abajo
            if (perfil.Count > 0)
            {
                double mpGlobal = perfil.Aggregate((a, b) => a.Gex >= b.Gex ? a : b).K;
                double mnGlobal = perfil.Aggregate((a, b) => a.Gex <= b.Gex ? a : b).K;
                var arriba = perfil.Where(p => p.K > S).ToList();
                var abajo = perfil.Where(p => p.K < S).ToList();
                double mp = arriba.Count > 0 ? arriba.Aggregate((a, b) => a.Gex >= b.Gex ? a : b).K : mpGlobal;
                double mn = abajo.Count > 0 ? abajo.Aggregate((a, b) => a.Gex <= b.Gex ? a : b).K : mnGlobal;
                L.WallPos = mp + baseUsada; L.WallNeg = mn + baseUsada;
            }

            // picos = dominantes del centinela (GammaVivo 2130-2277)
            var picos = new List<(double Fut, double Peso)>();
            int max = Math.Max(1, A.MaxDominantes);
            if (perfil.Count >= 3 && A.ModoDominante == 0)
            {
                double piso = mx * Math.Max(0.0, Math.Min(0.95, A.MinFuerzaDominante / 100.0));
                double rad = Math.Max(1, A.RadioCentroide);
                for (int i = 1; i < perfil.Count - 1; i++)
                {
                    double a = Math.Abs(perfil[i - 1].Gex), b2 = Math.Abs(perfil[i].Gex), c2 = Math.Abs(perfil[i + 1].Gex);
                    if (b2 <= a || b2 <= c2 || b2 < piso) continue;
                    double sw = 0, sx = 0;
                    foreach (var n in perfil)
                    {
                        if (Math.Abs(n.Fut - perfil[i].Fut) > rad) continue;
                        double w = Math.Abs(n.Gex); if (w <= 0) continue;
                        sw += w; sx += w * n.Fut;
                    }
                    if (sw <= 0) continue;
                    picos.Add((sx / sw, b2));
                }
                picos.Sort((u, v) => v.Peso.CompareTo(u.Peso));
                picos = Separar(picos, A.SeparacionPuntos);
            }
            else if (perfil.Count >= 3 && A.ModoDominante == 1)
            {
                double piso = mx * Math.Max(0.0, Math.Min(0.95, A.MinFuerzaDominante / 100.0));
                foreach (var n in perfil)
                {
                    double p = Math.Abs(n.Gex);
                    if (p <= 0 || p < piso) continue;
                    picos.Add((n.Fut, p));
                }
                picos.Sort((u, v) => v.Peso.CompareTo(u.Peso));
            }
            else if (perfil.Count >= 3)
            {
                for (int i = 1; i < perfil.Count - 1; i++)
                {
                    double a = Math.Abs(perfil[i - 1].Gex), b2 = Math.Abs(perfil[i].Gex), c2 = Math.Abs(perfil[i + 1].Gex);
                    if (b2 <= a || b2 <= c2) continue;
                    if (b2 < mx * 0.12) continue;
                    double den = a - 2 * b2 + c2;
                    double delta = Math.Abs(den) > 1e-12 ? 0.5 * (a - c2) / den : 0.0;
                    delta = Math.Max(-0.5, Math.Min(0.5, delta));
                    double paso = (perfil[i + 1].K - perfil[i - 1].K) / 2.0;
                    picos.Add((perfil[i].K + delta * paso + baseUsada, b2));
                }
                picos.Sort((u, v) => v.Peso.CompareTo(u.Peso));
                picos = Separar(picos, A.SeparacionPuntos);
            }
            if (picos.Count > max) picos.RemoveRange(max, picos.Count - max);
            L.Picos = picos.Select(p => p.Fut).ToArray();
            return L;
        }

        private static List<(double Fut, double Peso)> Separar(List<(double Fut, double Peso)> picos, double sep)
        {
            if (sep <= 0) return picos;
            var fl = new List<(double Fut, double Peso)>();
            foreach (var q in picos) if (!fl.Any(z => Math.Abs(z.Fut - q.Fut) < sep)) fl.Add(q);
            return fl;
        }

        private double Cruce(Feed.Cadena c, double S, double r)
        {
            double lo = S * 0.97, hi = S * 1.03; const int pasos = 60;
            double ant = double.NaN, xAnt = 0;
            for (int i = 0; i <= pasos; i++)
            {
                double x = lo + (hi - lo) * i / pasos, t = 0;
                foreach (var f in c.Filas)
                {
                    if (f.V < 0 || f.V >= c.Dias.Length) continue;
                    double dias = c.Dias[f.V];
                    if (dias > A.DiasMax) continue;
                    double T = Math.Max(dias, PISO_DIAS) / 365.0;
                    t += (GammaHoyNucleo.GammaBs(x, f.K, T, f.IvC, r) * f.OiC - GammaHoyNucleo.GammaBs(x, f.K, T, f.IvP, r) * f.OiP) * MULT * x * x * 0.01;
                }
                if (!double.IsNaN(ant) && ((ant < 0 && t >= 0) || (ant > 0 && t <= 0)))
                    return (t != ant) ? xAnt + (x - xAnt) * (-ant) / (t - ant) : x;
                ant = t; xAnt = x;
            }
            return double.NaN;
        }

        /// <summary>Lo que Gamma Vivo anota por vela (GammaVivo.AnotarVela, 1134-1180), mismas llaves.</summary>
        public static List<KeyValuePair<string, double>> Niveles(Lectura L)
        {
            var niv = new List<KeyValuePair<string, double>>
            {
                new KeyValuePair<string, double>("wall_pos", double.IsNaN(L.WallPos) ? 0 : L.WallPos),
                new KeyValuePair<string, double>("wall_neg", double.IsNaN(L.WallNeg) ? 0 : L.WallNeg),
                new KeyValuePair<string, double>("zero", double.IsNaN(L.Zero) ? 0 : L.Zero),
            };
            for (int i = 0; i < L.Picos.Length && i < 8; i++)
                niv.Add(new KeyValuePair<string, double>("dom" + i, L.Picos[i]));
            return niv;
        }
    }
}
