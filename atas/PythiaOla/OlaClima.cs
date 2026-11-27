using System;
using System.Collections.Generic;
using System.Globalization;

namespace PythiaLaOla
{
    // =========================================================================================================================
    // EL CLIMA DE LA RUEDA (27-11-2026), sin nada de ATAS: el semaforo D1 "cuentakilometros" de la ronda 21
    // (laboratorio/ronda21_regimen: RESULTADOS.md seccion 7, r21_01_regimen.py y regla_congelada_comb_t7_v4.py), con LAS MISMAS
    // definiciones, una por una:
    //   - rrn = (maximo - minimo de la rueda hasta el cierre del minuto m) / (mediana del rango RTH de las ruedas previas x f(m));
    //   - f(m) = la fraccion tipica del rango que ya hizo la rueda a esa hora: la curva FIJA de curva_rango_por_minuto.json (256 sesiones
    //     de MES 5 min anteriores al 08-09-2026), copiada abajo tal cual (390 valores, de las 9:30 a las 15:59 NY);
    //   - VWAP de la rueda = suma(precio medio de cada orden x volumen) / suma(volumen) desde las 9:30 NY, con precio medio =
    //     (primero + ultimo) / 2 de cada orden agresora (r19_01_rasgos.py, "VWAP (precio medio de la orden)");
    //   - apertura = el ULTIMO precio de la primera orden de la rueda; precio del minuto = el ultimo precio de la ultima orden antes de su
    //     cierre;
    //   - TENDENCIA si rrn >= 1,6 y el precio esta del mismo lado del VWAP y de la apertura (ese es el lado); ROTACION si 1,0 < rrn < 1,6;
    //     ESTRECHO si rrn <= 1,0; con rrn >= 1,6 y los lados en desacuerdo (o sin VWAP): DUDOSO, esperar (estado_minuto de la regla
    //     congelada, que lo llama 'N': no se opera).
    // Cada minuto se evalua AL CIERRE (la R21: "una senal en el segundo t usa el estado del ultimo minuto cerrado"). Las cuentas se
    // hacen en double como numpy, y como los precios son multiplos de 0,25 todas las sumas son exactas: el resultado es el MISMO numero,
    // no uno parecido (lo controla herramientas/auditar_pythiaola.py contra la referencia de la R21, minuto por minuto).
    // Detalle de la grilla por segundo de la R19 que se respeta: un segundo sin ordenes toma el ultimo precio; si a las 9:30:00 no hubo
    // ninguna orden, el maximo y el minimo de la rueda arrancan con el ultimo precio de la noche.
    // LO QUE ES: una LECTURA del clima del dia, con su numero. Medido en ES (R21): con TENDENCIA el extremo nuevo llega antes que el toque
    // del VWAP el 60-66 % de las veces (base 37-40 %). En MNQ de julio a septiembre leyo mal cuando las 10 ruedas previas eran de otro
    // clima. No es una senal de entrada.
    // =========================================================================================================================

    /// <summary>La curva f(m) de la ronda 21, copiada de laboratorio/ronda21_regimen/curva_rango_por_minuto.json.</summary>
    public static class CurvaRango
    {
        /// <summary>SHA-256 del json del que se copio la curva (para la auditoria).</summary>
        public const string Huella = "72589182d1533ccab0903e70469e8654dc7a201197d88064238dd1a2d7a6bc33";
        public const int Minutos = 390;

        /// <summary>f(m), m = minutos desde las 9:30 NY (0 = 9:30, 389 = 15:59): fraccion mediana del rango del dia ya hecha al cierre del
        /// minuto m. Los literales son los que escribio Python (redondeo de ida y vuelta): C# los lee al mismo double.</summary>
        public static readonly double[] F =
        {
            0.04173280423280423, 0.08346560846560847, 0.1251984126984127, 0.16693121693121693, 0.20866402116402116, 0.2213150553150553,
            0.23396608946608946, 0.2466171236171236, 0.25926815776815776, 0.2719191919191919, 0.28101218190488575, 0.2901051718905796,
            0.29919816187627346, 0.3082911518619673, 0.31738414184766117, 0.3259857396320092, 0.33458733741635727, 0.34318893520070526,
            0.3517905329850533, 0.36039213076940135, 0.36825931456113103, 0.3761264983528607, 0.38399368214459034, 0.39186086593632,
            0.3997280497280497, 0.4064687996497698, 0.41320954957148986, 0.41995029949320994, 0.42669104941493, 0.4334317993366501,
            0.4408381613038166, 0.4482445232709831, 0.45565088523814956, 0.46305724720531605, 0.47046360917248253, 0.4760767696909272,
            0.4816899302093719, 0.4873030907278165, 0.4929162512462612, 0.4985294117647059, 0.5022830036537228, 0.5060365955427397,
            0.5097901874317566, 0.5135437793207736, 0.5172973712097905, 0.5186710449099903, 0.5200447186101902, 0.52141839231039,
            0.5227920660105899, 0.5241657397107897, 0.5293565152136078, 0.5345472907164259, 0.5397380662192441, 0.5449288417220622,
            0.5501196172248803, 0.5524710499052604, 0.5548224825856405, 0.5571739152660204, 0.5595253479464005, 0.5618767806267806,
            0.5702434577985238, 0.5786101349702671, 0.5869768121420104, 0.5953434893137537, 0.6037101664854969, 0.6080717658297187,
            0.6124333651739403, 0.6167949645181621, 0.6211565638623837, 0.6255181632066055, 0.6274256292489289, 0.6293330952912523,
            0.6312405613335756, 0.633148027375899, 0.6350554934182224, 0.6366934881548959, 0.6383314828915694, 0.6399694776282429,
            0.6416074723649163, 0.6432454671015898, 0.6475149257174709, 0.6517843843333521, 0.6560538429492332, 0.6603233015651143,
            0.6645927601809954, 0.6663118893042166, 0.6680310184274378, 0.669750147550659, 0.6714692766738802, 0.6731884057971014,
            0.6769804413849759, 0.6807724769728505, 0.684564512560725, 0.6883565481485996, 0.6921485837364741, 0.6947686407448355,
            0.6973886977531967, 0.700008754761558, 0.7026288117699192, 0.7052488687782805, 0.7071951918219999, 0.7091415148657193,
            0.7110878379094386, 0.7130341609531581, 0.7149804839968774, 0.7192384582281126, 0.7234964324593479, 0.7277544066905831,
            0.7320123809218184, 0.7362703551530536, 0.7391183249387694, 0.7419662947244853, 0.744814264510201, 0.7476622342959169,
            0.7505102040816327, 0.7522766036000877, 0.7540430031185428, 0.7558094026369978, 0.7575758021554528, 0.7593422016739079,
            0.7604233930094862, 0.7615045843450646, 0.7625857756806429, 0.7636669670162213, 0.7647481583517997, 0.7668036097041439,
            0.7688590610564882, 0.7709145124088324, 0.7729699637611767, 0.7750254151135209, 0.7783113484013964, 0.7815972816892718,
            0.7848832149771473, 0.7881691482650227, 0.7914550815528982, 0.7921421439012483, 0.7928292062495984, 0.7935162685979484,
            0.7942033309462986, 0.7948903932946486, 0.7964201428171721, 0.7979498923396956, 0.7994796418622192, 0.8010093913847427,
            0.8025391409072662, 0.8051915964137561, 0.8078440519202462, 0.8104965074267362, 0.8131489629332262, 0.8158014184397162,
            0.8170528994576554, 0.8183043804755944, 0.8195558614935335, 0.8208073425114726, 0.8220588235294117, 0.8225278810408921,
            0.8229969385523725, 0.8234659960638531, 0.8239350535753335, 0.8244041110868139, 0.8249240980871388, 0.8254440850874639,
            0.8259640720877889, 0.826484059088114, 0.8270040460884389, 0.82780949838088, 0.8286149506733211, 0.8294204029657624,
            0.8302258552582035, 0.8310313075506446, 0.8330421934764131, 0.8350530794021817, 0.8370639653279501, 0.8390748512537187,
            0.8410857371794872, 0.8430936429550584, 0.8451015487306296, 0.8471094545062008, 0.849117360281772, 0.8511252660573432,
            0.8536807110362088, 0.8562361560150744, 0.85879160099394, 0.8613470459728056, 0.8639024909516713, 0.8662343826192762,
            0.8685662742868812, 0.8708981659544862, 0.8732300576220912, 0.8755619492896961, 0.8763751236486008, 0.8771882980075055,
            0.8780014723664102, 0.878814646725315, 0.8796278210842197, 0.8797827916932464, 0.879937762302273, 0.8800927329112999,
            0.8802477035203266, 0.8804026741293532, 0.8804026741293532, 0.8804026741293532, 0.8804026741293532, 0.8804026741293532,
            0.8804026741293532, 0.8823268135658255, 0.8842509530022978, 0.8861750924387702, 0.8880992318752425, 0.8900233713117148,
            0.8915289419269331, 0.8930345125421514, 0.8945400831573695, 0.8960456537725878, 0.8975512243878061, 0.8980694623786373,
            0.8985877003694684, 0.8991059383602996, 0.8996241763511308, 0.900142414341962, 0.9009059851371403, 0.9016695559323186,
            0.9024331267274969, 0.9031966975226752, 0.9039602683178535, 0.9069761242588026, 0.9099919801997517, 0.9130078361407007,
            0.9160236920816498, 0.9190395480225989, 0.9217910803646541, 0.9245426127067093, 0.9272941450487645, 0.9300456773908198,
            0.9327972097328749, 0.9346796431877129, 0.9365620766425509, 0.9384445100973889, 0.9403269435522269, 0.9422093770070649,
            0.9435641787743961, 0.9449189805417274, 0.9462737823090587, 0.94762858407639, 0.9489833858437212, 0.9491586643685004,
            0.9493339428932794, 0.9495092214180586, 0.9496844999428377, 0.9498597784676168, 0.9535890713260307, 0.9573183641844445,
            0.9610476570428583, 0.9647769499012722, 0.968506242759686, 0.9708199319876771, 0.9731336212156682, 0.9754473104436593,
            0.9777609996716503, 0.9800746888996414, 0.9812376998738601, 0.9824007108480788, 0.9835637218222975, 0.9847267327965161,
            0.9858897437707348, 0.9887117950165878, 0.9915338462624409, 0.9943558975082939, 0.997177948754147, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            1.0, 1.0, 1.0, 1.0, 1.0, 1.0
        };
    }

    /// <summary>El clima al cierre de un minuto de la rueda.</summary>
    public struct MinutoClima
    {
        public int M;                  // minuto de la rueda: 0 = 9:30 NY ... 389 = 15:59 NY
        public DateTime Fin;           // UTC: cierre del minuto (9:30 + m + 1 min NY)
        public long N;                 // ordenes que habia visto el motor al cerrar el minuto
        public bool HayRth;            // ya hubo alguna orden de la rueda
        public bool HayPrecio;         // ya hubo alguna orden de la jornada
        public decimal H, L;           // maximo y minimo de la rueda hasta el cierre del minuto
        public decimal C;              // precio del minuto: el ultimo de la ultima orden antes del cierre
        public decimal O;              // apertura de la rueda (0 si todavia no hubo orden de la rueda)
        public double Vw;              // VWAP de la rueda al cierre (NaN si todavia no hubo orden de la rueda)
        public double Med;             // mediana del rango RTH de las ruedas previas (0: no hay ruedas)
        public double Fm;              // f(m)
        public double Rrn;             // (H - L) / (Med x f(m)): veces lo normal a esta hora
        public char Tipo;              // 'T' tendencia, 'R' rotacion, 'E' estrecho, 'N' dudoso (esperar)
        public int LadoVwap;           // signo de (precio - VWAP): en TENDENCIA es el lado (+1 arriba, -1 abajo)
        public int LadoApertura;       // signo de (precio - apertura)

        public bool Tendencia => Tipo == 'T';

        /// <summary>El nombre para el panel: "TENDENCIA ▲", "TENDENCIA ▼", "ROTACIÓN", "ESTRECHO", "DUDOSO".</summary>
        public string Nombre()
        {
            switch (Tipo)
            {
                case 'T': return LadoVwap > 0 ? "TENDENCIA ▲" : "TENDENCIA ▼";
                case 'R': return "ROTACIÓN";
                case 'E': return "ESTRECHO";
                default: return "DUDOSO";
            }
        }
    }

    /// <summary>
    /// El clima de UNA rueda, orden por orden (como el motor de la ola: mismas ordenes, en el mismo orden, con la misma correccion de la
    /// ultima orden cuando crece en vivo). Cada minuto se cierra cuando llega la primera orden de un minuto posterior (o con TomarHasta:
    /// fin de la cinta). No es seguro entre hilos: el indicador lo usa adentro de su llave; el historial, con una instancia local.
    /// </summary>
    public sealed class ClimaRueda
    {
        public const double CorteTendencia = 1.6, CorteEstrecho = 1.0;
        public const int Minutos = CurvaRango.Minutos;

        public readonly DateTime Rth0, Rth1;   // UTC: 9:30 y 16:00 NY del dia de la rueda
        public readonly double Med;            // mediana del rango RTH de las ruedas previas completas (0: no hay)
        public readonly int Usadas;            // cuantas ruedas entraron en la mediana
        private bool _hayPrimera; private DateTime _primera;   // la primera orden que entro (no cambia con las correcciones)

        /// <summary>El motor tiene ordenes desde ANTES de las 9:30 (el historial de la jornada, o el vivo arrancado de noche): el rango y el
        /// VWAP son los de la rueda entera. false si la primera orden que vio es de la rueda (sin cinta: solo en vivo, arrancado despues de
        /// las 9:30): el clima de esa rueda no se puede medir y no se muestra.</summary>
        public bool Completo => _hayPrimera && _primera < Rth0;
        public DateTime PrimeraOrden => _hayPrimera ? _primera : DateTime.MinValue;

        private struct Estado
        {
            public long N;
            public bool Hay; public decimal Ultimo;          // la ultima orden (cualquiera)
            public bool HayAntes; public decimal UltAntes;   // la ultima orden antes de las 9:30
            public bool HayRth; public decimal O, H, L;      // la rueda
            public double Pv, V;                             // VWAP: suma de precio medio x volumen y de volumen (exactas en double)
        }

        private Estado _e, _antes;
        private bool _puede;
        private readonly List<MinutoClima> _min = new List<MinutoClima>(Minutos);

        public ClimaRueda(DateTime rth0Utc, DateTime rth1Utc, double mediana, int usadas)
        {
            Rth0 = rth0Utc; Rth1 = rth1Utc; Med = mediana; Usadas = usadas;
        }

        /// <summary>Cierre del minuto m (UTC).</summary>
        public DateTime Fin(int m) => Rth0.AddTicks((m + 1) * TimeSpan.TicksPerMinute);

        /// <summary>Minutos ya cerrados (desde el 0, sin huecos).</summary>
        public int Cerrados => _min.Count;
        public MinutoClima Minuto(int m) => _min[m];
        public long Ordenes => _e.N;

        /// <summary>Una orden nueva. Antes de pasarla se cierran los minutos que terminaron antes de ella.</summary>
        public void Orden(DateTime t, decimal primero, decimal ultimo, decimal vol)
        {
            if (!_hayPrimera) { _hayPrimera = true; _primera = t; }
            while (_min.Count < Minutos && t >= Fin(_min.Count)) _min.Add(Foto(_min.Count, in _e));
            _antes = _e; _puede = true;
            Paso(t, primero, ultimo, vol);
        }

        /// <summary>La ULTIMA orden crecio (OnUpdateCumulativeTrade): se vuelve al estado de antes, se pasa de nuevo y se rehacen los
        /// minutos que se habian cerrado DESPUES de ella (con TomarHasta). false si no hay orden para corregir.</summary>
        public bool CorregirUltima(DateTime t, decimal primero, decimal ultimo, decimal vol)
        {
            if (!_puede) return false;
            while (_min.Count < Minutos && t >= Fin(_min.Count)) _min.Add(Foto(_min.Count, in _antes));   // si cambiara su hora
            _e = _antes;
            Paso(t, primero, ultimo, vol);
            for (int k = _min.Count - 1; k >= 0 && _min[k].N == _e.N; k--) _min[k] = Foto(k, in _e);
            return true;
        }

        /// <summary>Cierra los minutos que terminan a esa hora o antes (fin de la cinta; despues no puede entrar una orden anterior).</summary>
        public void TomarHasta(DateTime hasta)
        {
            while (_min.Count < Minutos && Fin(_min.Count) <= hasta) _min.Add(Foto(_min.Count, in _e));
        }

        /// <summary>El ultimo minuto CERRADO cuyo cierre es a esa hora o antes (el clima "al momento" de una vela que cierra a esa hora);
        /// -1 si ninguno.</summary>
        public int UltimoHasta(DateTime corte)
        {
            if (_min.Count == 0 || corte < Fin(0)) return -1;
            long n = (corte - Rth0).Ticks / TimeSpan.TicksPerMinute;   // minutos con cierre <= corte
            return (int)Math.Min(n, _min.Count) - 1;
        }

        private void Paso(DateTime t, decimal primero, decimal ultimo, decimal vol)
        {
            _e.N++; _e.Hay = true; _e.Ultimo = ultimo;
            if (t < Rth0) { _e.HayAntes = true; _e.UltAntes = ultimo; return; }
            if (t >= Rth1) return;
            decimal mn, mx;
            if (ultimo < primero) { mn = ultimo; mx = primero; } else { mn = primero; mx = ultimo; }
            if (!_e.HayRth)
            {
                _e.HayRth = true; _e.O = ultimo; _e.H = mx; _e.L = mn;
                // la grilla por segundo de la R19: los segundos de la rueda sin ordenes valen el ultimo precio; si la primera orden no es
                // del segundo 9:30:00, los anteriores arrastran el ultimo precio de la noche y entran en el maximo y el minimo
                if ((t - Rth0).Ticks >= TimeSpan.TicksPerSecond && _e.HayAntes)
                {
                    if (_e.UltAntes > _e.H) _e.H = _e.UltAntes;
                    if (_e.UltAntes < _e.L) _e.L = _e.UltAntes;
                }
            }
            else
            {
                if (mx > _e.H) _e.H = mx;
                if (mn < _e.L) _e.L = mn;
            }
            double pm = ((double)primero + (double)ultimo) / 2.0, v = (double)vol;
            _e.Pv += pm * v; _e.V += v;
        }

        private MinutoClima Foto(int m, in Estado e)
        {
            var x = new MinutoClima { M = m, Fin = Fin(m), N = e.N, HayRth = e.HayRth, HayPrecio = e.Hay, Med = Med, Fm = CurvaRango.F[m] };
            if (e.HayRth) { x.H = e.H; x.L = e.L; x.O = e.O; x.Vw = e.V > 0 ? e.Pv / e.V : double.NaN; }
            else { x.H = x.L = e.HayAntes ? e.UltAntes : (e.Hay ? e.Ultimo : 0m); x.Vw = double.NaN; }
            x.C = e.Hay ? e.Ultimo : 0m;
            Evaluar(ref x);
            return x;
        }

        /// <summary>estado_minuto de la regla congelada de la R21, con las mismas comparaciones.</summary>
        public static void Evaluar(ref MinutoClima x)
        {
            double hl = (double)x.H - (double)x.L;
            double den = x.Med * x.Fm;
            x.Rrn = hl / den;
            bool fin = !(double.IsNaN(x.Rrn) || double.IsInfinity(x.Rrn));
            bool hayVw = !double.IsNaN(x.Vw);
            double c = (double)x.C;
            x.LadoVwap = hayVw ? Math.Sign(c - x.Vw) : 0;
            x.LadoApertura = x.HayRth ? Math.Sign(c - (double)x.O) : 0;
            if (fin && x.Rrn >= CorteTendencia && hayVw && x.HayRth && x.LadoVwap == x.LadoApertura && x.LadoVwap != 0) x.Tipo = 'T';
            else if (fin && x.Rrn > CorteEstrecho && x.Rrn < CorteTendencia) x.Tipo = 'R';
            else if (fin && x.Rrn <= CorteEstrecho) x.Tipo = 'E';
            else x.Tipo = 'N';
        }

        /// <summary>Un double como lo escribe la auditoria: el mas corto que se lee igual (Python lo parsea al mismo numero).</summary>
        public static string D(double v) => double.IsNaN(v) ? "nan" : v.ToString("R", CultureInfo.InvariantCulture);
    }
}
