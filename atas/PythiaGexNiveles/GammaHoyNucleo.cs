using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PythiaGex
{
    /// <summary>
    /// EL NUCLEO DE GAMMA HOY: LA CUENTA, SIN ATAS.
    ///
    /// Todo lo que Gamma Hoy calcula a partir de una cadena, un precio del
    /// futuro y una hora vive aca, sin una sola referencia a ATAS. El
    /// indicador (GammaHoy.cs) le pasa esos tres datos en vivo y dibuja lo que
    /// sale; el simulador (atas/Rebobina) le pasa los mismos tres datos leidos
    /// de archivos y anota lo que sale. Asi lo que se backtestea es, letra por
    /// letra, lo mismo que se mira en pantalla. Si un dia esto se bifurca,
    /// el backtest deja de valer: no copiar la cuenta a otro lado.
    ///
    /// Estado entre llamadas: las fotos por minuto del GEX por volumen (Max
    /// Change), el lado del pico (transicion) y la alerta vigente.
    /// </summary>
    public sealed class GammaHoyNucleo
    {
        public enum HorizonteVenc { Hoy, Semana, Todo }
        public enum LibroConv { Auto, Volumen, OI }

        /// <summary>Los mismos ajustes y valores por defecto que muestra ATAS.</summary>
        public sealed class Ajustes
        {
            public double Tasa = 0.0375;
            public HorizonteVenc Horizonte = HorizonteVenc.Hoy;
            public int CuantasDominantes = 2;
            public double RadioDominantesPct = 2.0;
            public double PicoRadioPct = 0.35;
            public int MuchoPct = 50;
            public LibroConv Convexidad = LibroConv.Auto;
        }

        public sealed class Strike
        {
            public double K, Fut;
            public double GexOi, GexVol, Conv;
            public double Oi, VolHoy;
        }

        public sealed class Snap
        {
            public long Minuto;
            public Dictionary<double, double> GexVol = new();
        }

        /// <summary>Todo lo que sale de una cuenta. Los NaN son "no hay".</summary>
        public sealed class Lectura
        {
            public bool SinBase;
            public List<Strike> Perfil = new();
            public double S, Futuro, Base;
            public string BaseOrigen = "";
            public double ZeroVol = double.NaN, ZeroOi = double.NaN, NetVol, NetOi;
            public double MpVol = double.NaN, MnVol = double.NaN, MpOi = double.NaN, MnOi = double.NaN;
            public double MaxAbsVol, MaxAbsOi, MaxAbsConv;
            public List<(double Fut, double Gex)> Doms = new();
            public string LibroConv = "", LibroDom = "vol";
            public string Cuadrante = "", CuadranteCorto = "";
            public int CuadranteN;
            public double PicoFut = double.NaN, PicoGex, ConvEnPrecio;
            public bool Mucho;
            public string Alerta = "";
            public DateTime AlertaHasta = DateTime.MinValue;
            public bool TransicionNueva;
            public (double Fut, double Delta)[] MaxChange = new (double, double)[Ventanas.Length];
            public double[] Estela = new double[0];
            public DateTime Hora;
        }

        public static readonly int[] Ventanas = { 1, 5, 10, 15, 30 };
        public const double MULT_INDICE = 100.0;
        public const double PISO_DIAS = 1.0 / 1440.0;

        public Ajustes A = new();

        private readonly object _llave = new();
        private readonly List<Snap> _fotos = new();
        private readonly Dictionary<double, double> _convOiTmp = new();
        private int _ladoPico;             // +1 precio arriba del pico, -1 abajo
        private string _alerta = "";
        private DateTime _alertaHasta = DateTime.MinValue;

        public List<Snap> FotosCopia() { lock (_llave) return _fotos.ToList(); }

        /// <summary>Olvida las fotos y la alerta: para empezar otro dia en el simulador.</summary>
        public void Reiniciar()
        {
            lock (_llave) { _fotos.Clear(); _ladoPico = 0; _alerta = ""; _alertaHasta = DateTime.MinValue; }
        }

        // ------------------------------------------------------------------ Black-Scholes
        private static double Fi(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);

        public static double GammaBs(double S, double K, double T, double iv, double r)
        {
            if (S <= 0 || K <= 0 || T <= 0 || iv <= 0) return 0;
            var v = iv * Math.Sqrt(T);
            if (v <= 0) return 0;
            var d1 = (Math.Log(S / K) + (r + 0.5 * iv * iv) * T) / v;
            return Fi(d1) / (S * v);
        }

        /// <summary>GEX de una fila (un strike, un vencimiento) ponderado por lo
        /// que se pida: interes abierto o volumen. Convencion estandar, +call
        /// -put: es una ASUNCION sobre de que lado quedo la mesa, no un dato.</summary>
        public static double Gex(Feed.Fila f, double S, double T, double r, bool porVolumen)
        {
            var gC = GammaBs(S, f.K, T, f.IvC, r);
            var gP = GammaBs(S, f.K, T, f.IvP, r);
            double wC = porVolumen ? f.VolC : f.OiC, wP = porVolumen ? f.VolP : f.OiP;
            return (gC * wC - gP * wP) * MULT_INDICE * S * S * 0.01;
        }

        private bool PasaHorizonte(double dias, double masCerca)
        {
            switch (A.Horizonte)
            {
                case HorizonteVenc.Hoy: return dias >= 0 && dias <= Math.Max(1.0, masCerca + 0.01);
                case HorizonteVenc.Semana: return dias >= 0 && dias <= Math.Max(7.0, masCerca + 0.01);
                default: return dias >= 0;
            }
        }

        /// <summary>El cruce por cero de la suma repreciada a cada precio de una
        /// grilla de +-3 %, interpolado. Devuelve en precio de INDICE.</summary>
        private double Cruce(Feed.Cadena c, double S, double r, double masCerca, bool porVolumen)
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
                    if (!PasaHorizonte(dias, masCerca)) continue;
                    t += Gex(f, x, Math.Max(dias, PISO_DIAS) / 365.0, r, porVolumen);
                }
                if (!double.IsNaN(ant) && ((ant < 0 && t >= 0) || (ant > 0 && t <= 0)))
                    return (t != ant) ? xAnt + (x - xAnt) * (-ant) / (t - ant) : x;
                ant = t; xAnt = x;
            }
            return double.NaN;
        }

        // ------------------------------------------------------------------ la cuenta
        /// <summary>La cuenta entera para UNA cadena, UN precio del futuro y UNA
        /// hora. Devuelve null si la cadena no sirve; Lectura.SinBase si no hay
        /// base para convertir el indice a futuro (no se inventa una).</summary>
        public Lectura Calcular(Feed.Cadena c, double futuro, DateTime ahoraUtc)
        {
            if (c == null || c.Filas.Count == 0 || c.Dias == null || c.Dias.Length == 0) return null;
            if (futuro <= 0) return null;
            lock (_llave) return CalcularAdentro(c, futuro, ahoraUtc);
        }

        private Lectura CalcularAdentro(Feed.Cadena c, double futuro, DateTime ahoraUtc)
        {
            var L = new Lectura { Futuro = futuro, Hora = ahoraUtc };

            // la base: medida > ultima buena reciente > cruda; nunca inventada
            double baseUsada; string origen;
            if (c.BaseConfiable && c.Base != 0) { baseUsada = c.Base; origen = "medida"; }
            else if (c.BaseUltimaBuena != 0 && c.BaseUltimaBuenaEdad <= 360) { baseUsada = c.BaseUltimaBuena; origen = "medida hace " + c.BaseUltimaBuenaEdad.ToString("0", CultureInfo.InvariantCulture) + " min"; }
            else if (c.BaseCruda != 0) { baseUsada = c.BaseCruda; origen = "CRUDA " + c.BaseErrorTicks.ToString("0", CultureInfo.InvariantCulture) + " ticks"; }
            else { L.SinBase = true; L.BaseOrigen = "sin base"; return L; }

            double S = futuro - baseUsada;
            if (S <= 0) return null;
            double r = A.Tasa, Sup = S * 1.01;

            double masCerca = double.MaxValue;
            foreach (var d in c.Dias) if (d >= 0 && d < masCerca) masCerca = d;
            if (masCerca == double.MaxValue) masCerca = 0;

            var por = new Dictionary<double, Strike>();
            _convOiTmp.Clear();
            foreach (var f in c.Filas)
            {
                if (f.V < 0 || f.V >= c.Dias.Length) continue;
                double dias = c.Dias[f.V];
                if (!PasaHorizonte(dias, masCerca)) continue;
                double T = Math.Max(dias, PISO_DIAS) / 365.0;
                double gOi = Gex(f, S, T, r, false), gVol = Gex(f, S, T, r, true);
                double gOiUp = Gex(f, Sup, T, r, false), gVolUp = Gex(f, Sup, T, r, true);
                if (gOi == 0 && gVol == 0) continue;
                if (!por.TryGetValue(f.K, out var s)) { s = new Strike { K = f.K, Fut = f.K + baseUsada }; por[f.K] = s; }
                s.GexOi += gOi; s.GexVol += gVol;
                s.Oi += f.OiC + f.OiP; s.VolHoy += f.VolC + f.VolP;
                // la convexidad de cada libro se guarda aparte y se elige despues
                s.Conv += (gVolUp - gVol);          // por volumen (provisorio)
                _convOiTmp[f.K] = (_convOiTmp.TryGetValue(f.K, out var q) ? q : 0) + (gOiUp - gOi);
            }
            var perfil = por.Values.OrderBy(x => x.K).ToList();

            double sumVol = perfil.Sum(x => Math.Abs(x.GexVol)), sumOi = perfil.Sum(x => Math.Abs(x.GexOi));
            bool convPorVol = A.Convexidad == LibroConv.Volumen || (A.Convexidad == LibroConv.Auto && sumVol >= 0.2 * sumOi && sumVol > 0);
            if (!convPorVol) foreach (var s in perfil) s.Conv = _convOiTmp.TryGetValue(s.K, out var q) ? q : 0;
            _convOiTmp.Clear();

            double netVol = perfil.Sum(x => x.GexVol), netOi = perfil.Sum(x => x.GexOi);
            double maxAbsVol = perfil.Count > 0 ? perfil.Max(x => Math.Abs(x.GexVol)) : 0;
            double maxAbsOi = perfil.Count > 0 ? perfil.Max(x => Math.Abs(x.GexOi)) : 0;
            double maxAbsConv = perfil.Count > 0 ? perfil.Max(x => Math.Abs(x.Conv)) : 0;

            // zero gamma de cada libro: donde la suma repreciada cruza cero
            double zeroVol = Cruce(c, S, r, masCerca, true), zeroOi = Cruce(c, S, r, masCerca, false);
            if (!double.IsNaN(zeroVol)) zeroVol += baseUsada;
            if (!double.IsNaN(zeroOi)) zeroOi += baseUsada;

            // majors de cada libro
            double mpVol = double.NaN, mnVol = double.NaN, mpOi = double.NaN, mnOi = double.NaN;
            if (perfil.Count > 0)
            {
                var pv = perfil.Where(x => x.GexVol > 0).OrderByDescending(x => x.GexVol).FirstOrDefault();
                var nv = perfil.Where(x => x.GexVol < 0).OrderBy(x => x.GexVol).FirstOrDefault();
                var po = perfil.Where(x => x.GexOi > 0).OrderByDescending(x => x.GexOi).FirstOrDefault();
                var no = perfil.Where(x => x.GexOi < 0).OrderBy(x => x.GexOi).FirstOrDefault();
                if (pv != null) mpVol = pv.Fut; if (nv != null) mnVol = nv.Fut;
                if (po != null) mpOi = po.Fut; if (no != null) mnOi = no.Fut;
            }

            // dominantes: las barras mas largas del volumen cerca del precio;
            // si todavia no hay volumen (noche), las del OI, y se dice
            double radio = futuro * A.RadioDominantesPct / 100.0;
            string libroDom = "vol";
            int cuantas = Math.Max(1, A.CuantasDominantes);
            var candDom = perfil.Where(x => Math.Abs(x.Fut - futuro) <= radio && Math.Abs(x.GexVol) > 0)
                                .OrderByDescending(x => Math.Abs(x.GexVol)).Take(cuantas)
                                .Select(x => (x.Fut, x.GexVol)).ToList();
            if (candDom.Count == 0)
            {
                libroDom = "OI";
                candDom = perfil.Where(x => Math.Abs(x.Fut - futuro) <= radio && Math.Abs(x.GexOi) > 0)
                                .OrderByDescending(x => Math.Abs(x.GexOi)).Take(cuantas)
                                .Select(x => (x.Fut, x.GexOi)).ToList();
            }

            // el cuadrante: pico de GEX cerca del precio? convexidad ahi?
            double rPico = futuro * A.PicoRadioPct / 100.0;
            var cerca = perfil.Where(x => Math.Abs(x.Fut - futuro) <= rPico).ToList();
            double picoGex = 0, picoFut = double.NaN, convPrecio = 0;
            bool porVolCuad = sumVol > 0 && sumVol >= 0.2 * sumOi;
            foreach (var x in cerca)
            {
                double gg = porVolCuad ? x.GexVol : x.GexOi;
                if (Math.Abs(gg) > Math.Abs(picoGex)) { picoGex = gg; picoFut = x.Fut; }
                convPrecio += x.Conv;
            }
            if (cerca.Count == 0 && perfil.Count > 0)
            {
                var vecino = perfil.OrderBy(x => Math.Abs(x.Fut - futuro)).First();
                convPrecio = vecino.Conv;
            }
            double maxLibro = porVolCuad ? maxAbsVol : maxAbsOi;
            bool mucho = maxLibro > 0 && Math.Abs(picoGex) >= maxLibro * A.MuchoPct / 100.0;
            bool convPos = convPrecio >= 0;
            int cuadN; string nombre, corto;
            if (mucho && convPos) { cuadN = 1; nombre = "iman colchon: rango, reversion"; corto = "IMAN"; }
            else if (mucho && !convPos) { cuadN = 2; nombre = "nivel explosivo: ruptura, momentum"; corto = "EXPLOSIVO"; }
            else if (!mucho && convPos) { cuadN = 3; nombre = "mercado estable: rangos amplios"; corto = "ESTABLE"; }
            else { cuadN = 4; nombre = "salvese quien pueda: tendencia, tamaño chico"; corto = "RIESGO"; }

            // transicion: perder el maximo GEX del libro con convexidad negativa.
            // El maximo no es un strike sino una ZONA: los strikes con al menos
            // el 80 % del maximo, mas una banda de un punto. Medido en el
            // rebobinado del 2026-09-03: con un solo strike, 7.759 y 7.764 se
            // alternaban el maximo y la alerta se disparaba a cada minuto.
            // Adentro de la zona el lado no cambia; solo cuenta salir de ella.
            double maxFut = double.NaN; double maxG = 0;
            foreach (var x in perfil) { double gg = porVolCuad ? x.GexVol : x.GexOi; if (Math.Abs(gg) > maxG) { maxG = Math.Abs(gg); maxFut = x.Fut; } }
            double zonaAbajo = maxFut, zonaArriba = maxFut;
            if (maxG > 0)
                foreach (var x in perfil)
                {
                    double gg = Math.Abs(porVolCuad ? x.GexVol : x.GexOi);
                    if (gg >= 0.8 * maxG) { if (x.Fut < zonaAbajo) zonaAbajo = x.Fut; if (x.Fut > zonaArriba) zonaArriba = x.Fut; }
                }
            // media distancia entre strikes (2,5 puntos): salir de la zona de verdad
            const double banda = 2.5;
            int lado = double.IsNaN(maxFut) ? 0 : (futuro >= zonaArriba + banda ? 1 : (futuro <= zonaAbajo - banda ? -1 : _ladoPico));
            bool nueva = false;
            // una alerta por vez: mientras la anterior sigue en pantalla (10 min)
            // el lado se actualiza pero no se vuelve a gritar
            if (lado != 0 && _ladoPico != 0 && lado != _ladoPico && !convPos && ahoraUtc >= _alertaHasta)
            {
                var es = CultureInfo.GetCultureInfo("es-AR");
                string zona = zonaAbajo == zonaArriba ? maxFut.ToString("N0", es) : zonaAbajo.ToString("N0", es) + "-" + zonaArriba.ToString("N0", es);
                _alerta = (lado < 0 ? "perdio" : "recupero") + " el maximo GEX " + zona + " con convexidad negativa: pensar en TENDENCIA";
                _alertaHasta = ahoraUtc.AddMinutes(10);
                nueva = true;
            }
            if (lado != 0) _ladoPico = lado;

            // max change: fotos por minuto del libro de volumen
            long minuto = ahoraUtc.Ticks / TimeSpan.TicksPerMinute;
            var foto = _fotos.LastOrDefault();
            if (foto == null || foto.Minuto != minuto) { foto = new Snap { Minuto = minuto }; _fotos.Add(foto); while (_fotos.Count > 40) _fotos.RemoveAt(0); }
            foto.GexVol.Clear();
            foreach (var x in perfil) foto.GexVol[x.Fut] = x.GexVol;
            var mc = new (double Fut, double Delta)[Ventanas.Length];
            for (int i = 0; i < Ventanas.Length; i++)
            {
                var vieja = _fotos.Where(s0 => s0.Minuto <= minuto - Ventanas[i]).LastOrDefault();
                double mejor = 0, futM = double.NaN;
                if (vieja != null)
                    foreach (var x in perfil)
                    {
                        double antes = vieja.GexVol.TryGetValue(x.Fut, out var a0) ? a0 : 0;
                        double d = x.GexVol - antes;
                        if (Math.Abs(d) > Math.Abs(mejor)) { mejor = d; futM = x.Fut; }
                    }
                mc[i] = (futM, mejor);
            }

            // estela: la dominante de esta vela
            var est = new double[cuantas];
            for (int i = 0; i < est.Length; i++) est[i] = i < candDom.Count ? candDom[i].Item1 : double.NaN;

            L.Perfil = perfil; L.S = S; L.Base = baseUsada; L.BaseOrigen = origen;
            L.ZeroVol = zeroVol; L.ZeroOi = zeroOi; L.NetVol = netVol; L.NetOi = netOi;
            L.MpVol = mpVol; L.MnVol = mnVol; L.MpOi = mpOi; L.MnOi = mnOi;
            L.MaxAbsVol = maxAbsVol; L.MaxAbsOi = maxAbsOi; L.MaxAbsConv = maxAbsConv;
            L.Doms = candDom; L.LibroConv = convPorVol ? "vol" : "OI"; L.LibroDom = libroDom;
            L.Cuadrante = nombre; L.CuadranteCorto = corto; L.CuadranteN = cuadN;
            L.PicoFut = picoFut; L.PicoGex = picoGex; L.ConvEnPrecio = convPrecio; L.Mucho = mucho;
            L.Alerta = _alerta; L.AlertaHasta = _alertaHasta; L.TransicionNueva = nueva;
            L.MaxChange = mc; L.Estela = est;
            return L;
        }

        /// <summary>La linea de auditoria, identica en el indicador y en el simulador.</summary>
        public static string Audit(Lectura L, Feed.Cadena c, bool vivaActiva)
        {
            var inv = CultureInfo.InvariantCulture;
            return string.Format(inv, "AUDIT fut={0:F2} S={1:F2} base={2:F2} origen={3} strikes={4} netVol={5:F3}B netOi={6:F3}B zeroVol={7:F2} zeroOi={8:F2} mpVol={9:F2} mnVol={10:F2} doms={11} libroDom={12} conv={13} q={14} pico={15:F2} picoGex={16:F0}M mucho={17} convPrecio={18:F0}M mc30={19:F2}:{20:F0}M edadFeed={21:F1}min vivaActiva={22}",
                L.Futuro, L.S, L.Base, L.BaseOrigen.Replace(' ', '_'), L.Perfil.Count, L.NetVol / 1e9, L.NetOi / 1e9, L.ZeroVol, L.ZeroOi, L.MpVol, L.MnVol,
                string.Join("/", L.Doms.Select(d => d.Fut.ToString("F2", inv) + "=" + (d.Gex / 1e6).ToString("F0", inv) + "M")),
                L.LibroDom, L.LibroConv, L.CuadranteN, L.PicoFut, L.PicoGex / 1e6, L.Mucho, L.ConvEnPrecio / 1e6, L.MaxChange[4].Fut, L.MaxChange[4].Delta / 1e6, c.EdadMin, vivaActiva);
        }

        /// <summary>Los niveles que se anotan por vela para el laboratorio, con los
        /// mismos nombres en el indicador y en el simulador.</summary>
        public static List<KeyValuePair<string, double>> Niveles(Lectura L)
        {
            var niv = new List<KeyValuePair<string, double>>();
            void Add(string k, double v) { if (!double.IsNaN(v) && v > 0) niv.Add(new KeyValuePair<string, double>(k, v)); }
            Add("zero_vol", L.ZeroVol); Add("zero_oi", L.ZeroOi);
            Add("mp_vol", L.MpVol); Add("mn_vol", L.MnVol); Add("mp_oi", L.MpOi); Add("mn_oi", L.MnOi);
            for (int i = 0; i < L.Doms.Count; i++) Add("dom" + i, L.Doms[i].Fut);
            for (int i = 0; i < Ventanas.Length; i++) Add("mc" + Ventanas[i], L.MaxChange[i].Fut);
            Add("pico", L.PicoFut);
            Add("q_cuadrante", L.CuadranteN);
            return niv;
        }
    }
}
