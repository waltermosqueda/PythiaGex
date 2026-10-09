// COPIA TEXTUAL de atas/PythiaGexDos/GammaHoyNucleo.cs (PythiaGex 2.0, 18-09-2026), solo con el namespace cambiado
// a PythiaGexTres (06-10-2026). La cuenta (GEX, zero, majors, dominantes, Max Change, AUDIT, Niveles) es LETRA POR LETRA
// la misma que la clasica 1.11d y la 2.0: por eso 3.0 se puede contrastar contra ellas. Si se corrige algo aca, anotarlo
// en DISENO.md y en el CHANGELOG, y revisar si corresponde llevarlo tambien a la 2.0 (prod no se toca).
// 3.7.0 (08-10-2026, auditoria laboratorio/tres/auditoria_0810) DOS diferencias con la 2.0, las dos pedidas por la especificacion:
//   C4  el multiplicador es el del LIBRO (Feed.Cadena.Multiplicador: NQ 20, ES 50, CBOE 100) y no MULT_INDICE = 100 para todos.
//       Dentro de un libro es una constante: no mueve zero, majors, muros ni dominantes (nq_04_multiplicador / cod_08: posiciones
//       identicas); cambia el TAMAÑO en M/B (NQ /5).
//   C3  ZeroPorSigno saltea los strikes ISLA (Seleccion37.Cruces): signo distinto a sus dos vecinos y |G| < 10 % del menor. Eso cambia
//       el zero 'cruce mas cercano' y las listas de cruces (los 'mellizos' de NQ y el 31.278 falso de NDX, rev_02_isla_ndx.txt).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PythiaGexTres
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
        public enum NocheDominantes { Volumen, InteresAbierto }
        /// <summary>Fuera de la rueda de Nueva York (antes de las 9:30 o desde las 16:00, hora de NY).</summary>
        public static bool FueraDeRueda(DateTime ahoraUtc)
        {
            try
            {
                var ny = TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
                int m = ny.Hour * 60 + ny.Minute;
                return ny.DayOfWeek == DayOfWeek.Saturday || ny.DayOfWeek == DayOfWeek.Sunday || m < 9 * 60 + 30 || m >= 16 * 60;
            }
            catch { return false; }
        }

        public sealed class Ajustes
        {
            public double Tasa = 0.0375;
            public HorizonteVenc Horizonte = HorizonteVenc.Hoy;
            // la base acotada (1.5): vencimiento del contrato del grafico y dividendo del indice
            // para el carry teorico; y la base medida en la rueda por el indicador
            public DateTime ExpiracionFuturoUtc = default(DateTime);
            public DateTime ExpiracionFuturoAltUtc = default(DateTime);   // el trimestre siguiente, cuando el vencimiento es supuesto
            public double Dividendo = 0.012;
            public double BaseRueda = double.NaN, BaseRuedaEdadMin = double.NaN;
            public int CuantasDominantes = 2;
            public double RadioDominantesPct = 2.0;
            // TOPE EN PUNTOS (16-09, pedido: "hasta 100 puntos de distancia para no llenar de ruido"): 2 % de 29.300 son 585 pts
            // por lado y aparecian dominantes a 200-300 pts, ruido para scalping. El radio efectivo es el menor entre el % y este tope.
            public double RadioDominantesMaxPts = 100.0;
            public double PicoRadioPct = 0.35;
            public int MuchoPct = 50;
            public LibroConv Convexidad = LibroConv.Auto;
            // LA DOMINANTE COMO CENTROIDE. Medido en los videos de la referencia
            // (2026-09-07, analizar_guiones.py): la dominante no es una raya plana en
            // un strike; sus guiones forman una banda de ~5 puntos de NQ que ondula
            // minuto a minuto. Un promedio de precio ponderado por gamma alrededor
            // del pico se mueve con cada cambio de peso y no salta de strike en strike.
            public bool Centroide = true;
            public double RadioCentroidePts = 12.0;    // ~2 strikes de SPX a cada lado
            // EL CANAL: una dominante por lado. La primera es la barra mas fuerte POR
            // ENCIMA del precio y la segunda la mas fuerte POR DEBAJO (2026-09-08, pedido
            // del operador: "son dos bandas nomas, a cada extremo y para adentro"). Si se
            // piden mas de dos, el resto se completa por fuerza.
            public bool UnaPorLado = true;
            // EMPATE TECNICO (2026-09-11, pedido del operador tras ver la banda pegada a una
            // dominante lejana): medido de noche con el libro vivo de Rithmic, la dominante de
            // abajo saltaba entre 29.049 (-84 M) y 28.800 (-91 M) por 7 M de diferencia, es
            // decir, por un punado de contratos. Si dos barras del mismo lado estan dentro de
            // este porcentaje de la mas grande, gana la MAS CERCANA al precio: es la que el
            // precio puede tocar (el "alcance" de dominantes.py). 0 = siempre la mas grande.
            public double EmpatePct = 20.0;
            // DE NOCHE, DOMINANTES POR INTERES ABIERTO (16-09): el libro por volumen entre las 16:00 y las 9:30 de Nueva York es
            // el resto de manana (flaco, lejos: medido 15-09, D1 +270 con 233M). El "si no hay volumen, OI" de antes nunca se
            // disparaba porque el volumen nunca es cero. Con InteresAbierto, fuera de la rueda las dominantes salen del OI del
            // vencimiento mas cercano (posiciones abiertas), y el AUDIT lo dice: libroDom=OI.
            public NocheDominantes DominantesDeNoche = NocheDominantes.Volumen;   // 17-09: Volumen por pedido del operador (scalping: donde se opero HOY, no las posiciones acumuladas)
            // F5 (2.0.1): el zero como la referencia (medida 11-09 y 18-09): cambio de signo del perfil POR STRIKE, interpolado
            // linealmente entre los dos strikes vecinos (715 -3,7B y 716 +2,2B => 715,62). false = el cruce REPRECIADO en la
            // grilla de +-3 % (Cruce(), 716,17 en ese mismo libro: no es el de la referencia).
            public bool ZeroInterpolado = true;
        }

        public sealed class Strike
        {
            public double K, Fut;
            public double Clave;      // clave ESTABLE de las fotos del Max Change: el strike crudo del contrato (16-09: en semana de
                                      // roll el K corrido del libro Rithmic cambia con el spread cada minuto y las fotos no coincidian)
            public double GexOi, GexVol, Conv;
            public double GexVolC, GexVolP, GexOiC, GexOiP;   // 3.5.7: por lado (call +, put -): los muros de calls y de puts del recuadro
            public double GexFlujo;   // gamma del DEALER por flujo firmado (solo Rithmic, 1.9): + largo (colchon), - corto (tobogan)
            public double Oi, VolHoy;
            public double IvSum, IvW;        // acumuladores (IV ponderada por OI + volumen)
            public double Dte = double.MaxValue;   // dias al vencimiento mas cercano que aporta
            public double IvMedia => IvW > 0 ? IvSum / IvW : double.NaN;
        }

        public sealed class Snap
        {
            public long Minuto;
            public Dictionary<double, double> GexVol = new();   // POR STRIKE (K), nunca por precio del futuro: la base cambia y rompe las claves (1.5f)
            public Dictionary<double, double> Conv = new();     // la convexidad por strike, para las pelotitas de la escalera derecha (1.6b)
        }

        /// <summary>Todo lo que sale de una cuenta. Los NaN son "no hay".</summary>
        public sealed class Lectura
        {
            public bool SinBase;
            public List<Strike> Perfil = new();
            public double S, Futuro, Base;
            public string BaseOrigen = "";
            public double ZeroVol = double.NaN, ZeroOi = double.NaN, NetVol, NetOi;
            /// <summary>F5: como se saco el zero: "interp" (cambio de signo por strike interpolado) o "cruce" (repreciado en grilla).</summary>
            public string ZeroModo = "";
            /// <summary>3.2.5: cruces de signo del perfil por strike a +-0,1 % del precio (por volumen / por OI). Mas de 1 = el zero es una eleccion, no un nivel.</summary>
            public int ZeroCrucesVol, ZeroCrucesOi;
            /// <summary>3.3.1: TODOS los cruces de signo (vol y OI) a +-0,1 % del precio, en precio del grafico: las filas de la 2.0 y sus vecinas.</summary>
            public List<double> ZeroCrucesLista = new List<double>();
            public List<double> ZeroCrucesVolLista = new List<double>(), ZeroCrucesOiLista = new List<double>();   // 3.5.1: separados
            public double MpVol = double.NaN, MnVol = double.NaN, MpOi = double.NaN, MnOi = double.NaN;
            public double MaxAbsVol, MaxAbsOi, MaxAbsConv;
            public double MaxAbsFlujo; public bool TieneFlujo;
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
            public double MasCerca = double.NaN;   // dias al vencimiento mas cercano del mapa
            public double Carry = double.NaN;      // carry teorico de la base (NaN si no se conoce el vencimiento del futuro)
            public double Multiplicador = MULT_INDICE;   // 3.7.0 C4: USD por punto del libro con que se hizo la cuenta (NQ 20, ES 50, CBOE 100)
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

        /// <summary>Siembra fotos viejas (del archivo) por delante de las que ya hay, para que
        /// el Max Change y las pelotitas existan desde el primer minuto tras un arranque.</summary>
        public void SembrarFotos(IEnumerable<(long Minuto, Dictionary<double, double> GexVol, Dictionary<double, double> Conv)> semillas)
        {
            lock (_llave)
            {
                long primera = _fotos.Count > 0 ? _fotos[0].Minuto : long.MaxValue;
                var nuevas = semillas.Where(f => f.Minuto < primera && f.GexVol != null && f.GexVol.Count > 0)
                                     .OrderBy(f => f.Minuto)
                                     .Select(f => new Snap { Minuto = f.Minuto, GexVol = new Dictionary<double, double>(f.GexVol), Conv = f.Conv != null ? new Dictionary<double, double>(f.Conv) : new Dictionary<double, double>() }).ToList();
                _fotos.InsertRange(0, nuevas);
                while (_fotos.Count > 40) _fotos.RemoveAt(0);
            }
        }

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
        public static double Gex(Feed.Fila f, double S, double T, double r, bool porVolumen, bool esFut = false, double mult = MULT_INDICE)
        {
            // opciones sobre el futuro (ES por Rithmic): Black-76; sobre el indice (SPX): Black-Scholes
            var gC = esFut ? Black76.Gamma(S, f.K, T, f.IvC) : GammaBs(S, f.K, T, f.IvC, r);
            var gP = esFut ? Black76.Gamma(S, f.K, T, f.IvP) : GammaBs(S, f.K, T, f.IvP, r);
            double wC = porVolumen ? f.VolC : f.OiC, wP = porVolumen ? f.VolP : f.OiP;
            return (gC * wC - gP * wP) * mult * S * S * 0.01;   // 3.7.0 C4: el multiplicador del libro (NQ 20, ES 50, CBOE 100)
        }

        /// <summary>3.5.7: la parte de UN lado de Gex() (call positiva, put negativa): los muros del recuadro, como cgex/pgex de motor.py.</summary>
        public static double GexLado(Feed.Fila f, double S, double T, double r, bool porVolumen, bool esFut, bool call, double mult = MULT_INDICE)
        {
            double iv = call ? f.IvC : f.IvP, w = call ? (porVolumen ? f.VolC : f.OiC) : (porVolumen ? f.VolP : f.OiP);
            if (w == 0) return 0;
            var gm = esFut ? Black76.Gamma(S, f.K, T, iv) : GammaBs(S, f.K, T, iv, r);
            return (call ? gm * w : -gm * w) * mult * S * S * 0.01;
        }

        /// <summary>Gamma del DEALER por flujo firmado (solo Rithmic: compras - ventas por lado agresor, 1.9).
        /// El cliente que compra deja al dealer corto de esa opcion (gamma negativa); el que vende lo deja largo.
        /// Positivo = dealers largos gamma (colchon, aguamarina); negativo = cortos (tobogan, purpura).
        /// Hipotesis del perfil derecho de la referencia (14-09: su signo no es ninguna griega estatica).</summary>
        public static double GexFlujo(Feed.Fila f, double S, double T, double r, bool esFut, double mult = MULT_INDICE)
        {
            if (f.FluC == 0 && f.FluP == 0) return 0;
            var gC = esFut ? Black76.Gamma(S, f.K, T, f.IvC) : GammaBs(S, f.K, T, f.IvC, r);
            var gP = esFut ? Black76.Gamma(S, f.K, T, f.IvP) : GammaBs(S, f.K, T, f.IvP, r);
            return -(gC * f.FluC + gP * f.FluP) * mult * S * S * 0.01;
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
        /// <summary>Cuanto envejecio la cadena desde que se calcularon sus dias al
        /// vencimiento: se resta a cada vencimiento para que la gamma use el tiempo que
        /// de verdad queda y para que el 0DTE vencido salga solo del perfil.</summary>
        public static double Envejecer(Feed.Cadena c, DateTime ahoraUtc)
        {
            if (c == null || c.GeneradoUtc == default(DateTime) || ahoraUtc <= c.GeneradoUtc) return 0;
            return Math.Min(2.0, (ahoraUtc - c.GeneradoUtc).TotalDays);
        }

        /// <summary>F5 (2.0.1): el zero de la referencia. Entre dos strikes vecinos (sin contar los de GEX cero) con GEX de
        /// signo distinto, el punto donde la recta que los une corta cero: K0 + (K1 - K0) x (-G0) / (G1 - G0). Si hay varios
        /// cruces, el mas cercano a S. En el eje del LIBRO (K), como Cruce(); NaN si el perfil no cambia de signo.</summary>
        private static double ZeroPorSigno(List<Strike> perfil, double S, bool porVolumen, out int cruces, List<double> lista = null)
        {
            // 3.2.5: cuantos cruces de signo hay a +-0,1 % de S (31 pts en NQ). Medido el 07-10 02:20 en el 0DTE: 5 por volumen y 17 por OI
            // en +-120 pts; el "zero" elegido es el mas cercano al precio y SALTA al vecino cuando el precio se mueve (se pega al precio).
            // 3.7.0 C3: antes de buscar los cruces se saltean los strikes ISLA (Seleccion37.Cruces, la misma regla de backtest_familia.cruces):
            // un strike casi neutro de signo contrario a sus dos vecinos creaba DOS cruces pegados ('mellizos') que no son ninguna pared.
            cruces = 0;
            double mejor = double.NaN, dist = double.MaxValue;
            var xs = new List<double>(perfil.Count); var gs = new List<double>(perfil.Count);
            foreach (var s in perfil) { xs.Add(s.K); gs.Add(porVolumen ? s.GexVol : s.GexOi); }   // ya ordenado por K
            foreach (var c in Seleccion37.Cruces(xs, gs, Seleccion37.ISLA))
            {
                double k = c.Z;
                double d = Math.Abs(k - S);
                if (d <= S * 0.001) cruces++;
                if (d <= S * 0.002) lista?.Add(k);   // 3.5.3: la lista llega a +-0,2 % (~63 pts) (el conteo sigue a +-0,1 %)
                if (d < dist) { dist = d; mejor = k; }
            }
            return mejor;
        }

        private double Cruce(Feed.Cadena c, double S, double r, double masCerca, double envejecer, bool porVolumen)
        {
            double amp = 0.03 * Math.Max(1.0, c.Apalancamiento); double lo = S * (1 - amp), hi = S * (1 + amp); const int pasos = 60;
            double ant = double.NaN, xAnt = 0;
            for (int i = 0; i <= pasos; i++)
            {
                double x = lo + (hi - lo) * i / pasos, t = 0;
                foreach (var f in c.Filas)
                {
                    if (f.V < 0 || f.V >= c.Dias.Length) continue;
                    double dias = c.Dias[f.V] - envejecer;
                    if (!PasaHorizonte(dias, masCerca)) continue;
                    t += Gex(f, x, Math.Max(dias, PISO_DIAS) / 365.0, r, porVolumen, c.EsFuturo);
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

            // la base: medida > medida reciente > de la rueda > cruda > TEORICA, y TODAS acotadas
            // con el carry teorico del contrato del grafico (1.5). Medido el 2026-09-09: la cruda de
            // la nube salto de 28,7 a 322,2 (NQ) cuando su cotizacion rolo a diciembre, y todos
            // los niveles quedaron ~294 pts arriba. Una base que no se parece al carry no se usa.
            var iv0 = CultureInfo.InvariantCulture;
            double carry = double.NaN;
            if (A.ExpiracionFuturoUtc != default(DateTime) && A.ExpiracionFuturoUtc > ahoraUtc)
                carry = futuro * (A.Tasa - A.Dividendo) * (A.ExpiracionFuturoUtc - ahoraUtc).TotalDays / 365.0;
            L.Carry = carry;
            double carryAlt = double.NaN;
            if (A.ExpiracionFuturoAltUtc != default(DateTime) && A.ExpiracionFuturoAltUtc > ahoraUtc)
                carryAlt = futuro * (A.Tasa - A.Dividendo) * (A.ExpiracionFuturoAltUtc - ahoraUtc).TotalDays / 365.0;
            bool Cerca(double b, double k) => Math.Abs(b - k) <= Math.Max(Math.Abs(k) * 0.6, futuro * 0.0006);
            // la medida (parity de opciones) puede parecerse al trimestre siguiente si el grafico
            // ya rolo; la CRUDA no tiene ese permiso: la rota de esta noche era justo "de diciembre"
            bool RazonableMedida(double b) => double.IsNaN(carry) || Cerca(b, carry) || (!double.IsNaN(carryAlt) && Cerca(b, carryAlt));
            bool Razonable(double b) => double.IsNaN(carry) || Cerca(b, carry);
            string Cota(string o, double b) => double.IsNaN(carry) ? o : o + (Razonable(b) ? "" : " FUERA DE COTA");
            double baseUsada; string origen;
            if (c.PorRazon) { baseUsada = 0; origen = "libro " + c.Fuente + " x razon " + c.Escala.ToString("0.0000", iv0) + (string.IsNullOrEmpty(c.EscalaOrigen) ? "" : " (" + c.EscalaOrigen + ")") + (c.Apalancamiento != 1.0 ? " apal " + c.Apalancamiento.ToString("0.###", iv0) + "x" : ""); }
            else if (c.EsFuturo) { baseUsada = 0; origen = "libro " + (string.IsNullOrEmpty(c.Fuente) ? "del futuro" : c.Fuente) + ", sin base"; }
            else if (c.BaseConfiable && c.Base != 0 && RazonableMedida(c.Base)) { baseUsada = c.Base; origen = "medida"; }
            else if (c.BaseUltimaBuena != 0 && c.BaseUltimaBuenaEdad <= 360 && RazonableMedida(c.BaseUltimaBuena)) { baseUsada = c.BaseUltimaBuena; origen = "medida hace " + c.BaseUltimaBuenaEdad.ToString("0", iv0) + " min"; }
            else if (!double.IsNaN(A.BaseRueda) && A.BaseRuedaEdadMin <= 24 * 60 && Razonable(A.BaseRueda)) { baseUsada = A.BaseRueda; origen = "de la rueda hace " + A.BaseRuedaEdadMin.ToString("0", iv0) + " min"; }
            else if (c.BaseCruda != 0 && Razonable(c.BaseCruda)) { baseUsada = c.BaseCruda; origen = "CRUDA " + c.BaseErrorTicks.ToString("0", iv0) + " ticks"; }
            else if (!double.IsNaN(carry)) { baseUsada = carry; origen = "TEORICA carry " + carry.ToString("0.0", iv0) + (c.BaseCruda != 0 ? " (cruda " + c.BaseCruda.ToString("0.0", iv0) + " descartada)" : ""); }
            else if (c.BaseCruda != 0) { baseUsada = c.BaseCruda; origen = "CRUDA " + c.BaseErrorTicks.ToString("0", iv0) + " ticks (sin cota)"; }
            else { L.SinBase = true; L.BaseOrigen = "sin base"; return L; }

            double S = c.PorRazon && c.Escala > 0 ? c.AlLibro(futuro) : futuro - baseUsada;
            if (S <= 0) return null;
            double r = A.Tasa, Sup = S * 1.01;

            // los dias de la cadena son de cuando se genero: se envejecen a la hora de la cuenta
            double envejecer = Envejecer(c, ahoraUtc);
            double masCerca = double.MaxValue;
            foreach (var d in c.Dias) { double dd = d - envejecer; if (dd >= 0 && dd < masCerca) masCerca = dd; }
            if (masCerca == double.MaxValue) masCerca = 0;

            var por = new Dictionary<double, Strike>();
            _convOiTmp.Clear();
            double mult = c.Multiplicador > 0 ? c.Multiplicador : MULT_INDICE;   // 3.7.0 C4: NQ 20, ES 50, CBOE 100
            foreach (var f in c.Filas)
            {
                if (f.V < 0 || f.V >= c.Dias.Length) continue;
                double dias = c.Dias[f.V] - envejecer;
                if (!PasaHorizonte(dias, masCerca)) continue;
                double T = Math.Max(dias, PISO_DIAS) / 365.0;
                double gOi = Gex(f, S, T, r, false, c.EsFuturo, mult), gVol = Gex(f, S, T, r, true, c.EsFuturo, mult);
                double gOiUp = Gex(f, Sup, T, r, false, c.EsFuturo, mult), gVolUp = Gex(f, Sup, T, r, true, c.EsFuturo, mult);
                if (gOi == 0 && gVol == 0) continue;
                if (!por.TryGetValue(f.K, out var s)) { s = new Strike { K = f.K, Clave = f.K0 > 0 ? -f.K0 : f.K, Fut = c.PorRazon ? c.AlFuturo(f.K) : f.K + baseUsada }; por[f.K] = s; }
                s.GexOi += gOi; s.GexVol += gVol;
                s.GexVolC += GexLado(f, S, T, r, true, c.EsFuturo, true, mult); s.GexVolP += GexLado(f, S, T, r, true, c.EsFuturo, false, mult);   // 3.5.7
                s.GexOiC += GexLado(f, S, T, r, false, c.EsFuturo, true, mult); s.GexOiP += GexLado(f, S, T, r, false, c.EsFuturo, false, mult);
                s.GexFlujo += GexFlujo(f, S, T, r, c.EsFuturo, mult);
                s.Oi += f.OiC + f.OiP; s.VolHoy += f.VolC + f.VolP;
                { double wc = f.OiC + f.VolC, wp = f.OiP + f.VolP; if (f.IvC > 0) { s.IvSum += f.IvC * wc; s.IvW += wc; } if (f.IvP > 0) { s.IvSum += f.IvP * wp; s.IvW += wp; } }
                if (dias < s.Dte) s.Dte = dias;
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
            double maxAbsFlujo = perfil.Count > 0 ? perfil.Max(x => Math.Abs(x.GexFlujo)) : 0;

            // zero gamma de cada libro. F5 (2.0.1): por defecto el de la referencia (cambio de signo del perfil por strike,
            // interpolado entre vecinos, el cruce mas cercano al precio); si no hay cambio de signo por strike, o si el
            // ajuste lo pide, el cruce REPRECIADO en la grilla de +-3 % (Cruce()). Se dice cual se uso (ZeroModo).
            double zeroVol, zeroOi; string zeroModo; int zeroCrucesVol = 0, zeroCrucesOi = 0; var listaCruces = new List<double>(); var listaV = new List<double>(); var listaO = new List<double>();
            if (A.ZeroInterpolado)
            {
                zeroVol = ZeroPorSigno(perfil, S, true, out zeroCrucesVol, listaV); zeroOi = ZeroPorSigno(perfil, S, false, out zeroCrucesOi, listaO); listaCruces.AddRange(listaV); listaCruces.AddRange(listaO); zeroModo = "interp";
                if (double.IsNaN(zeroVol)) { zeroVol = Cruce(c, S, r, masCerca, envejecer, true); zeroModo = "cruce (sin cambio de signo por strike)"; }
                if (double.IsNaN(zeroOi)) zeroOi = Cruce(c, S, r, masCerca, envejecer, false);
            }
            else { zeroVol = Cruce(c, S, r, masCerca, envejecer, true); zeroOi = Cruce(c, S, r, masCerca, envejecer, false); zeroModo = "cruce"; }
            if (!double.IsNaN(zeroVol)) zeroVol = c.PorRazon ? c.AlFuturo(zeroVol) : zeroVol + baseUsada;
            if (!double.IsNaN(zeroOi)) zeroOi = c.PorRazon ? c.AlFuturo(zeroOi) : zeroOi + baseUsada;
            List<double> AlFut(List<double> l0) => l0.Select(z => c.PorRazon ? c.AlFuturo(z) : z + baseUsada).Where(z => !double.IsNaN(z) && z > 0).Distinct().OrderBy(z => z).ToList();   // 3.5.1
            var listaFut = listaCruces.Select(z => c.PorRazon ? c.AlFuturo(z) : z + baseUsada).Where(z => !double.IsNaN(z) && z > 0).Distinct().OrderBy(z => z).ToList();   // 3.3.1

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
            if (A.RadioDominantesMaxPts > 0) radio = Math.Min(radio, A.RadioDominantesMaxPts);   // tope en puntos (16-09)
            string libroDom = "vol";
            int cuantas = Math.Max(1, A.CuantasDominantes);
            var candDom = perfil.Where(x => Math.Abs(x.Fut - futuro) <= radio && Math.Abs(x.GexVol) > 0)
                                .OrderByDescending(x => Math.Abs(x.GexVol)).Take(cuantas)
                                .Select(x => (x.Fut, x.GexVol)).ToList();
            bool nocheOi = A.DominantesDeNoche == NocheDominantes.InteresAbierto && FueraDeRueda(ahoraUtc)
                           && perfil.Any(x => Math.Abs(x.Fut - futuro) <= radio && Math.Abs(x.GexOi) > 0);
            if (nocheOi) candDom.Clear();   // de noche manda el interes abierto (16-09)
            if (candDom.Count == 0)
            {
                libroDom = "OI";
                candDom = perfil.Where(x => Math.Abs(x.Fut - futuro) <= radio && Math.Abs(x.GexOi) > 0)
                                .OrderByDescending(x => Math.Abs(x.GexOi)).Take(cuantas)
                                .Select(x => (x.Fut, x.GexOi)).ToList();
            }

            if (A.UnaPorLado && perfil.Count > 0)
            {
                Func<Strike, double> peso = x => Math.Abs(libroDom == "vol" ? x.GexVol : x.GexOi);
                var enRadio = perfil.Where(x => Math.Abs(x.Fut - futuro) <= radio && peso(x) > 0).ToList();
                // la mas fuerte de cada lado; con empate tecnico, la mas cercana entre las comparables
                Strike Elegir(IEnumerable<Strike> lado)
                {
                    var lista = lado.ToList();
                    if (lista.Count == 0) return null;
                    double pmax = lista.Max(peso);
                    double pisoEmpate = pmax * (1.0 - Math.Max(0.0, Math.Min(90.0, A.EmpatePct)) / 100.0);
                    return lista.Where(x => peso(x) >= pisoEmpate).OrderBy(x => Math.Abs(x.Fut - futuro)).ThenByDescending(peso).First();
                }
                var arriba = Elegir(enRadio.Where(x => x.Fut > futuro));
                var abajo = Elegir(enRadio.Where(x => x.Fut <= futuro));
                var lados = new List<(double Fut, double Gex)>();
                if (arriba != null) lados.Add((arriba.Fut, libroDom == "vol" ? arriba.GexVol : arriba.GexOi));
                if (abajo != null) lados.Add((abajo.Fut, libroDom == "vol" ? abajo.GexVol : abajo.GexOi));
                foreach (var x in enRadio.OrderByDescending(peso))
                {
                    if (lados.Count >= cuantas) break;
                    if (lados.Any(l => l.Fut == x.Fut)) continue;
                    lados.Add((x.Fut, libroDom == "vol" ? x.GexVol : x.GexOi));
                }
                if (lados.Count > 0) candDom = lados;
            }

            if (A.Centroide && candDom.Count > 0)
            {
                var conCentro = new List<(double Fut, double Gex)>();
                foreach (var dcand in candDom)
                {
                    double sw = 0, sx = 0;
                    foreach (var x in perfil)
                    {
                        if (Math.Abs(x.Fut - dcand.Fut) > A.RadioCentroidePts) continue;
                        double w = Math.Abs(libroDom == "vol" ? x.GexVol : x.GexOi);
                        if (w <= 0) continue;
                        sw += w; sx += w * x.Fut;
                    }
                    conCentro.Add((sw > 0 ? sx / sw : dcand.Fut, dcand.Item2));
                }
                candDom = conCentro;
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
            foto.GexVol.Clear(); foto.Conv.Clear();
            foreach (var x in perfil) { foto.GexVol[x.Clave] = x.GexVol; foto.Conv[x.Clave] = x.Conv; }
            var mc = new (double Fut, double Delta)[Ventanas.Length];
            for (int i = 0; i < Ventanas.Length; i++)
            {
                var vieja = _fotos.Where(s0 => s0.Minuto <= minuto - Ventanas[i]).LastOrDefault();
                double mejor = 0, futM = double.NaN;
                if (vieja != null)
                    foreach (var x in perfil)
                    {
                        // solo dentro del radio de las dominantes y con un piso del 5 % del maximo: un +5M a 255 pts
                        // del precio ocupaba una fila de la escalera (16-09)
                        if (Math.Abs(x.Fut - futuro) > radio) continue;
                        double antes = vieja.GexVol.TryGetValue(x.Clave, out var a0) ? a0 : 0;
                        double d = x.GexVol - antes;
                        if (Math.Abs(d) < 0.05 * maxAbsVol) continue;
                        if (Math.Abs(d) > Math.Abs(mejor)) { mejor = d; futM = x.Fut; }
                    }
                mc[i] = (futM, mejor);
            }

            // estela: la dominante de esta vela
            var est = new double[cuantas];
            for (int i = 0; i < est.Length; i++) est[i] = i < candDom.Count ? candDom[i].Item1 : double.NaN;

            L.Perfil = perfil; L.S = S; L.Base = baseUsada; L.BaseOrigen = origen; L.MasCerca = masCerca; L.Multiplicador = mult;
            L.ZeroVol = zeroVol; L.ZeroOi = zeroOi; L.NetVol = netVol; L.NetOi = netOi; L.ZeroModo = zeroModo; L.ZeroCrucesVol = zeroCrucesVol; L.ZeroCrucesOi = zeroCrucesOi; L.ZeroCrucesLista = listaFut; L.ZeroCrucesVolLista = AlFut(listaV); L.ZeroCrucesOiLista = AlFut(listaO);
            L.MpVol = mpVol; L.MnVol = mnVol; L.MpOi = mpOi; L.MnOi = mnOi;
            L.MaxAbsVol = maxAbsVol; L.MaxAbsOi = maxAbsOi; L.MaxAbsConv = maxAbsConv;
            L.MaxAbsFlujo = maxAbsFlujo; L.TieneFlujo = maxAbsFlujo > 0;
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
            return string.Format(inv, "AUDIT fut={0:F2} S={1:F2} base={2:F2} origen={3} strikes={4} netVol={5:F3}B netOi={6:F3}B zeroVol={7:F2} zeroOi={8:F2} mpVol={9:F2} mnVol={10:F2} doms={11} libroDom={12} conv={13} q={14} pico={15:F2} picoGex={16:F0}M mucho={17} convPrecio={18:F0}M mc30={19:F2}:{20:F0}M edadFeed={21:F1}min vivaActiva={22} flujo={23} zeroModo={24} zeroCruces={25}/{26} mult={27:0}",
                L.Futuro, L.S, L.Base, L.BaseOrigen.Replace(' ', '_'), L.Perfil.Count, L.NetVol / 1e9, L.NetOi / 1e9, L.ZeroVol, L.ZeroOi, L.MpVol, L.MnVol,
                string.Join("/", L.Doms.Select(d => d.Fut.ToString("F2", inv) + "=" + (d.Gex / 1e6).ToString("F0", inv) + "M")),
                L.LibroDom, L.LibroConv, L.CuadranteN, L.PicoFut, L.PicoGex / 1e6, L.Mucho, L.ConvEnPrecio / 1e6, L.MaxChange[4].Fut, L.MaxChange[4].Delta / 1e6, c.EdadMin, vivaActiva,
                L.TieneFlujo ? string.Join("/", L.Perfil.OrderByDescending(x => Math.Abs(x.GexFlujo)).Take(2).Select(x => x.Fut.ToString("F2", inv) + "=" + (x.GexFlujo / 1e6).ToString("F0", inv) + "M")) : "no",
                (L.ZeroModo ?? "").Replace(' ', '_'), L.ZeroCrucesVol, L.ZeroCrucesOi, L.Multiplicador);
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
            if (L.TieneFlujo)
            {
                // el perfil derecho por flujo firmado, para que el laboratorio lo juzgue contra placebo (1.9)
                var fp = L.Perfil.Where(x => x.GexFlujo > 0).OrderByDescending(x => x.GexFlujo).FirstOrDefault();
                var fn = L.Perfil.Where(x => x.GexFlujo < 0).OrderBy(x => x.GexFlujo).FirstOrDefault();
                if (fp != null) Add("flu_pos", fp.Fut); if (fn != null) Add("flu_neg", fn.Fut);
            }
            Add("pico", L.PicoFut);
            Add("q_cuadrante", L.CuadranteN);
            return niv;
        }
    }
}
