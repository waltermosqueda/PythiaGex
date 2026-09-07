using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGex
{
    /// <summary>
    /// GAMMA HOY. Construido de cero el 2026-09-07 a pedido del operador, para
    /// contrastarlo al lado de Gamma Vivo y de GAMMAlito.
    ///
    /// LO QUE APRENDIMOS DE LOS 30 VIDEOS Y DEL LABORATORIO, Y QUE ACA MANDA:
    ///
    ///   1. El mapa del dia es el de VOLUMEN. GAMMAlito tiene dos libros en su
    ///      panel, "Volume" y "Open Interest", y sus barras "respiran" porque
    ///      salen del volumen de hoy. En nuestro laboratorio las formulas de
    ///      volumen del dia le ganan al placebo por 22 y 42 puntos; la de gamma
    ///      por OI, que Gamma Vivo dibuja, pierde por 4. Aca el libro de
    ///      VOLUMEN es el principal y el de OI es la sombra de ayer.
    ///   2. La dominante es la barra mas larga del libro de volumen. Dos por
    ///      defecto (primaria y secundaria), como en su web.
    ///   3. El Max Change: donde estaba la punta de cada barra hace 15, 5 y 1
    ///      minutos (las tres pelotitas), y el strike con mayor cambio de GEX
    ///      en 1/5/10/15/30 minutos. "Lo mas importante no es la dominante
    ///      sino la pelotita: es adelantado".
    ///   4. La Convexity Ladder: la aceleracion por strike, aguamarina positiva
    ///      (colchon) y purpura negativa (tobogan), y el cuadrante de regimen:
    ///      mucho o poco GEX cerca del precio por convexidad positiva o
    ///      negativa = iman colchon / nivel explosivo / mercado estable /
    ///      salvese quien pueda. Y la alerta de transicion: perder el maximo
    ///      GEX con convexidad negativa.
    ///   5. Los Big Trades son bloques de OPCIONES con tamaño. Aca salen del
    ///      tape de Rithmic, contrato por contrato y sin retraso.
    ///   6. Nada se publica sin fuente y edad. Y todo se anota por vela para
    ///      que el laboratorio lo juzgue contra placebo: un indicador que
    ///      decide si tiene razon siempre tiene razon.
    ///
    /// LO QUE SE REUTILIZA: la cañeria. Feed (la cadena de CBOE que arma
    /// radar.py), CadenaViva (opciones de ES por Rithmic), Black76, Reloj y
    /// Centinela. Todo lo demas es nuevo.
    /// </summary>
    [DisplayName("PythiaGex - Gamma Hoy")]
    [Category("PythiaGex")]
    public class GammaHoy : Indicator
    {
        // ==================================================================
        // Ajustes (pocos, y cada uno con su porque)
        // ==================================================================
        public enum HorizonteVenc { Hoy, Semana, Todo }
        public enum LibroConv { Auto, Volumen, OI }

        [Display(Name = "Feed (url base)", GroupName = "1. Datos", Order = 1)]
        public string Url { get; set; } = "https://waltermosqueda.github.io/PythiaGex/datos/atas/";

        [Display(Name = "Raiz manual (ES, NQ, RTY; vacio = del grafico)", GroupName = "1. Datos", Order = 2)]
        public string RaizManual { get; set; } = "";

        [Display(Name = "Refresco del feed (s)", GroupName = "1. Datos", Order = 3)]
        [Range(60, 3600)]
        public int SegundosRefresco { get; set; } = 120;

        [Display(Name = "Tasa libre de riesgo", GroupName = "1. Datos", Order = 4)]
        public decimal Tasa { get; set; } = 0.0375m;

        [Display(Name = "Vencimientos del mapa", GroupName = "1. Datos", Order = 5,
                 Description = "Hoy: solo el 0DTE (si no hay, el mas cercano). Semana: hasta 7 dias. Todo: la cadena entera. GAMMAlito: 'nosotros trabajamos en 0DTE'.")]
        public HorizonteVenc Horizonte { get; set; } = HorizonteVenc.Hoy;

        [Display(Name = "Cadena viva de Rithmic (volumen y bloques de opciones de ES)", GroupName = "1. Datos", Order = 6)]
        public bool UsarCadenaViva { get; set; } = true;

        [Display(Name = "Viva: strikes por lado", GroupName = "1. Datos", Order = 7)]
        [Range(5, 60)]
        public int StrikesEnVivo { get; set; } = 25;

        [Display(Name = "Viva: vencimientos", GroupName = "1. Datos", Order = 8)]
        [Range(1, 6)]
        public int VencimientosEnVivo { get; set; } = 2;

        [Display(Name = "Viva: tope de contratos suscritos", GroupName = "1. Datos", Order = 9)]
        [Range(20, 600)]
        public int TopeContratos { get; set; } = 200;

        [Display(Name = "Dominantes (barras mas largas del volumen)", GroupName = "2. Lectura", Order = 1)]
        [Range(1, 5)]
        public int CuantasDominantes { get; set; } = 2;

        [Display(Name = "Dominantes: radio alrededor del precio (%)", GroupName = "2. Lectura", Order = 2)]
        public decimal RadioDominantesPct { get; set; } = 2.0m;

        [Display(Name = "Pico de GEX 'cerca del precio': radio (%)", GroupName = "2. Lectura", Order = 3,
                 Description = "Para el cuadrante. El pico del volumen dentro de este radio se compara con el maximo de todo el libro.")]
        public decimal PicoRadioPct { get; set; } = 0.35m;

        [Display(Name = "'Mucho GEX' = pico cercano >= % del maximo", GroupName = "2. Lectura", Order = 4)]
        [Range(10, 100)]
        public int MuchoPct { get; set; } = 50;

        [Display(Name = "Convexidad: de que libro", GroupName = "2. Lectura", Order = 5,
                 Description = "Auto: volumen si ya hay volumen (>= 20 % del OI en gamma), si no OI. Se rotula cual se uso.")]
        public LibroConv Convexidad { get; set; } = LibroConv.Auto;

        [Display(Name = "Big Trade: contratos minimos (opciones de ES)", GroupName = "2. Lectura", Order = 6,
                 Description = "GAMMAlito usa ~180 en QQQ. Las opciones de ES son menos liquidas: 50 de arranque, se calibra midiendo.")]
        [Range(5, 5000)]
        public int UmbralBigTrade { get; set; } = 50;

        [Display(Name = "Ancho de las barras (px)", GroupName = "3. Pantalla", Order = 1)]
        [Range(30, 300)]
        public int AnchoBarras { get; set; } = 90;

        [Display(Name = "Sombra del libro de OI (ayer) detras de las barras", GroupName = "3. Pantalla", Order = 2)]
        public bool VerSombraOI { get; set; } = true;

        [Display(Name = "Convexity Ladder (derecha)", GroupName = "3. Pantalla", Order = 3)]
        public bool VerConvexidad { get; set; } = true;

        [Display(Name = "Escalera pegada al eje", GroupName = "3. Pantalla", Order = 4)]
        public bool VerEscalera { get; set; } = true;

        [Display(Name = "Escalera: ancho (px)", GroupName = "3. Pantalla", Order = 5)]
        [Range(80, 300)]
        public int AnchoEscalera { get; set; } = 118;

        [Display(Name = "Estela de las dominantes por vela", GroupName = "3. Pantalla", Order = 6)]
        public bool VerEstela { get; set; } = true;

        [Display(Name = "Pelotitas del Max Change (15, 5 y 1 min)", GroupName = "3. Pantalla", Order = 7)]
        public bool VerPelotitas { get; set; } = true;

        [Display(Name = "Big Trades sobre las velas", GroupName = "3. Pantalla", Order = 8)]
        public bool VerBigTrades { get; set; } = true;

        [Display(Name = "Tamaño de letra", GroupName = "3. Pantalla", Order = 9)]
        [Range(6, 14)]
        public decimal TamLetra { get; set; } = 8m;

        [Display(Name = "Margen de abajo (px)", GroupName = "3. Pantalla", Order = 10,
                 Description = "ChartArea es mas alto que lo visible: lo pegado al fondo cae detras del eje de tiempo.")]
        [Range(0, 200)]
        public int MargenInferior { get; set; } = 48;

        [Display(Name = "Anotar cada vela para el laboratorio", GroupName = "4. Auditoria", Order = 1)]
        public bool AnotarCentinela { get; set; } = true;

        // ==================================================================
        // Estado
        // ==================================================================
        private sealed class Strike
        {
            public double K, Fut;
            public double GexOi, GexVol, Conv;
            public double Oi, VolHoy;
        }

        private sealed class Snap
        {
            public long Minuto;
            public Dictionary<double, double> GexVol = new();
        }

        private readonly object _candado = new();
        private Feed.Cadena _c;
        private string _error = "";
        private DateTime _ultimaBajada = DateTime.MinValue;
        private int _bajando;
        private TimeSpan _periodo;
        private Action _tick;

        private readonly CadenaViva _viva = new();
        private bool _vivaCorriendo;
        private DateTime _ultimoIntentoViva = DateTime.MinValue;

        // resultado del ultimo repricing (se copia bajo llave para dibujar)
        private List<Strike> _perfil = new();
        private double _S = double.NaN, _futuro = double.NaN, _base = double.NaN;
        private string _baseOrigen = "sin base";
        private double _zeroVol = double.NaN, _zeroOi = double.NaN, _netVol, _netOi;
        private double _mpVol = double.NaN, _mnVol = double.NaN, _mpOi = double.NaN, _mnOi = double.NaN;
        private double _maxAbsVol, _maxAbsOi, _maxAbsConv;
        private List<(double Fut, double Gex)> _doms = new();
        private string _libroConvUsado = "";
        private string _libroDomUsado = "vol";
        // cuadrante
        private string _cuadrante = "", _cuadranteCorto = "";
        private int _cuadranteN;
        private double _picoFut = double.NaN, _picoGex, _convEnPrecio;
        private bool _mucho;
        private int _ladoPico;             // +1 precio arriba del pico, -1 abajo
        private DateTime _alertaHasta = DateTime.MinValue;
        private string _alerta = "";
        // max change
        private readonly List<Snap> _fotos = new();
        private readonly int[] _ventanas = { 1, 5, 10, 15, 30 };
        private (double Fut, double Delta)[] _maxChange = new (double, double)[5];
        // estela de dominantes por vela
        private readonly Dictionary<int, double[]> _estela = new();
        // centinela
        private Centinela _cent;
        private int _barraCent = -1;
        private DateTime _ultimoAudit = DateTime.MinValue;
        private int _renders;

        private const double MULT_INDICE = 100.0;
        private const double PISO_DIAS = 1.0 / 1440.0;

        private static readonly Color ColPos = Color.FromArgb(45, 220, 130);
        private static readonly Color ColNeg = Color.FromArgb(235, 60, 60);
        private static readonly Color ColConvPos = Color.FromArgb(93, 217, 208);
        private static readonly Color ColConvNeg = Color.FromArgb(168, 107, 255);
        private static readonly Color ColDom = Color.FromArgb(232, 200, 60);
        private static readonly Color ColZero = Color.FromArgb(235, 235, 235);
        private static readonly Color ColTexto = Color.FromArgb(225, 230, 236);
        private static readonly Color ColFondo = Color.FromArgb(11, 16, 23);
        private static readonly Color ColAviso = Color.FromArgb(240, 160, 88);

        // ==================================================================
        // Ciclo de vida
        // ==================================================================
        protected override void OnInitialize()
        {
            try
            {
                var ps = DataProvider?.Panels;
                if (ps != null && ps.Count > 0) Panel = ps.Contains("Chart") ? "Chart" : ps[0];
            }
            catch (Exception e) { Registrar(e); }

            _periodo = TimeSpan.FromSeconds(30);
            _tick = () =>
            {
                var ahora = DateTime.UtcNow;
                if (Reloj.UltimaMedicion == DateTime.MinValue || (ahora - Reloj.UltimaMedicion).TotalMinutes >= 30)
                    _ = Reloj.Medir(Log);
                if ((ahora - _ultimaBajada).TotalSeconds >= Math.Max(60, SegundosRefresco))
                {
                    _ultimaBajada = ahora;
                    _ = BajarFeed();
                }
                if (UsarCadenaViva && !_viva.Activa && !_vivaCorriendo && (ahora - _ultimoIntentoViva).TotalSeconds >= 180)
                {
                    _ultimoIntentoViva = ahora;
                    ArrancarViva();
                }
                _viva.UmbralGrande = UmbralBigTrade;
            };
            SubscribeToTimer(_periodo, _tick);
            _ = Reloj.Medir(Log);
            _ultimaBajada = DateTime.UtcNow;
            _ultimoIntentoViva = DateTime.UtcNow;
            _ = BajarFeed();
            if (UsarCadenaViva) ArrancarViva();
            Log("Gamma Hoy 0.1 arranca. raiz=" + Raiz() + " horizonte=" + Horizonte);
        }

        protected override void OnDispose()
        {
            try { if (_tick != null) UnsubscribeFromTimer(_periodo, _tick); } catch { }
            try { _cent?.Volcar(true); } catch { }
            try { _viva.Dispose(); } catch { }
        }

        private void ArrancarViva()
        {
            _vivaCorriendo = true;
            _viva.UmbralGrande = UmbralBigTrade;
            _ = Task.Run(async () =>
            {
                try
                {
                    await _viva.Arrancar(DataProvider, TradingManager, TradingManager?.Security, Raiz(),
                                         7, Math.Max(5, StrikesEnVivo), Math.Max(1, VencimientosEnVivo),
                                         Math.Max(20, TopeContratos), m => Log("[viva] " + m)).ConfigureAwait(false);
                }
                catch (Exception e) { Registrar(e); }
                finally { _vivaCorriendo = false; }
            });
        }

        private async Task BajarFeed()
        {
            if (Interlocked.Exchange(ref _bajando, 1) == 1) return;
            try
            {
                var c = await Feed.Bajar(Url, Raiz(), m => _error = m).ConfigureAwait(false);
                if (c != null) { _c = c; _error = ""; }
            }
            finally
            {
                Interlocked.Exchange(ref _bajando, 0);
                try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            }
        }

        private string Raiz()
        {
            if (!string.IsNullOrWhiteSpace(RaizManual)) return RaizManual.Trim().ToUpperInvariant();
            var s = (InstrumentInfo?.Instrument ?? "").ToUpperInvariant().TrimStart('#');
            if (s.StartsWith("MNQ") || s.StartsWith("NQ")) return "NQ";
            if (s.StartsWith("M2K") || s.StartsWith("RTY")) return "RTY";
            return "ES";
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar != CurrentBar - 1) return;
            try { Repreciar(); } catch (Exception e) { Registrar(e); }
            try { Anotar(bar); } catch (Exception e) { Registrar(e); }
        }

        // ==================================================================
        // La cuenta
        // ==================================================================
        private static double Fi(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);

        private static double GammaBs(double S, double K, double T, double iv, double r)
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
        private static double Gex(Feed.Fila f, double S, double T, double r, bool porVolumen)
        {
            var gC = GammaBs(S, f.K, T, f.IvC, r);
            var gP = GammaBs(S, f.K, T, f.IvP, r);
            double wC = porVolumen ? f.VolC : f.OiC, wP = porVolumen ? f.VolP : f.OiP;
            return (gC * wC - gP * wP) * MULT_INDICE * S * S * 0.01;
        }

        private bool PasaHorizonte(double dias, double masCerca)
        {
            switch (Horizonte)
            {
                case HorizonteVenc.Hoy: return dias >= 0 && dias <= Math.Max(1.0, masCerca + 0.01);
                case HorizonteVenc.Semana: return dias >= 0 && dias <= Math.Max(7.0, masCerca + 0.01);
                default: return dias >= 0;
            }
        }

        private void Repreciar()
        {
            var c = _c;
            if (c == null || c.Filas.Count == 0 || c.Dias == null || c.Dias.Length == 0) return;

            decimal cierre;
            try { cierre = GetCandle(Math.Max(0, CurrentBar - 1)).Close; } catch { return; }
            if (cierre <= 0) return;
            double futuro = (double)cierre;

            // la base: medida > ultima buena reciente > cruda; nunca inventada
            double baseUsada; string origen;
            if (c.BaseConfiable && c.Base != 0) { baseUsada = c.Base; origen = "medida"; }
            else if (c.BaseUltimaBuena != 0 && c.BaseUltimaBuenaEdad <= 360) { baseUsada = c.BaseUltimaBuena; origen = "medida hace " + c.BaseUltimaBuenaEdad.ToString("0", CultureInfo.InvariantCulture) + " min"; }
            else if (c.BaseCruda != 0) { baseUsada = c.BaseCruda; origen = "CRUDA " + c.BaseErrorTicks.ToString("0", CultureInfo.InvariantCulture) + " ticks"; }
            else { lock (_candado) { _baseOrigen = "sin base"; } return; }

            double S = futuro - baseUsada;
            if (S <= 0) return;
            double r = (double)Tasa, Sup = S * 1.01;

            double masCerca = double.MaxValue;
            foreach (var d in c.Dias) if (d >= 0 && d < masCerca) masCerca = d;
            if (masCerca == double.MaxValue) masCerca = 0;

            var por = new Dictionary<double, Strike>();
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
                s.GexOi += 0; // (el de OI se suma abajo con su propio acumulador)
                _convOiTmp[f.K] = (_convOiTmp.TryGetValue(f.K, out var q) ? q : 0) + (gOiUp - gOi);
            }
            var perfil = por.Values.OrderBy(x => x.K).ToList();

            double sumVol = perfil.Sum(x => Math.Abs(x.GexVol)), sumOi = perfil.Sum(x => Math.Abs(x.GexOi));
            bool convPorVol = Convexidad == LibroConv.Volumen || (Convexidad == LibroConv.Auto && sumVol >= 0.2 * sumOi && sumVol > 0);
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
            double radio = futuro * (double)RadioDominantesPct / 100.0;
            string libroDom = "vol";
            var candDom = perfil.Where(x => Math.Abs(x.Fut - futuro) <= radio && Math.Abs(x.GexVol) > 0)
                                .OrderByDescending(x => Math.Abs(x.GexVol)).Take(Math.Max(1, CuantasDominantes))
                                .Select(x => (x.Fut, x.GexVol)).ToList();
            if (candDom.Count == 0)
            {
                libroDom = "OI";
                candDom = perfil.Where(x => Math.Abs(x.Fut - futuro) <= radio && Math.Abs(x.GexOi) > 0)
                                .OrderByDescending(x => Math.Abs(x.GexOi)).Take(Math.Max(1, CuantasDominantes))
                                .Select(x => (x.Fut, x.GexOi)).ToList();
            }

            // el cuadrante: pico de GEX cerca del precio? convexidad ahi?
            double rPico = futuro * (double)PicoRadioPct / 100.0;
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
            bool mucho = maxLibro > 0 && Math.Abs(picoGex) >= maxLibro * MuchoPct / 100.0;
            bool convPos = convPrecio >= 0;
            int cuadN; string nombre, corto;
            if (mucho && convPos) { cuadN = 1; nombre = "iman colchon: rango, reversion"; corto = "IMAN"; }
            else if (mucho && !convPos) { cuadN = 2; nombre = "nivel explosivo: ruptura, momentum"; corto = "EXPLOSIVO"; }
            else if (!mucho && convPos) { cuadN = 3; nombre = "mercado estable: rangos amplios"; corto = "ESTABLE"; }
            else { cuadN = 4; nombre = "salvese quien pueda: tendencia, tamaño chico"; corto = "RIESGO"; }

            // transicion: perder el maximo GEX del libro con convexidad negativa
            double maxFut = double.NaN; double maxG = 0;
            foreach (var x in perfil) { double gg = porVolCuad ? x.GexVol : x.GexOi; if (Math.Abs(gg) > maxG) { maxG = Math.Abs(gg); maxFut = x.Fut; } }
            int lado = double.IsNaN(maxFut) ? 0 : (futuro >= maxFut ? 1 : -1);
            string alerta = ""; DateTime alertaHasta;
            lock (_candado) { alertaHasta = _alertaHasta; alerta = _alerta; }
            if (lado != 0 && _ladoPico != 0 && lado != _ladoPico && !convPos)
            {
                alerta = (lado < 0 ? "perdio" : "recupero") + " el maximo GEX " + maxFut.ToString("N0", CultureInfo.GetCultureInfo("es-AR")) + " con convexidad negativa: pensar en TENDENCIA";
                alertaHasta = DateTime.UtcNow.AddMinutes(10);
                Log("TRANSICION " + alerta);
            }
            if (lado != 0) _ladoPico = lado;

            // max change: fotos por minuto del libro de volumen
            long minuto = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
            var foto = _fotos.LastOrDefault();
            if (foto == null || foto.Minuto != minuto) { foto = new Snap { Minuto = minuto }; _fotos.Add(foto); while (_fotos.Count > 40) _fotos.RemoveAt(0); }
            foto.GexVol.Clear();
            foreach (var x in perfil) foto.GexVol[x.Fut] = x.GexVol;
            var mc = new (double Fut, double Delta)[_ventanas.Length];
            for (int i = 0; i < _ventanas.Length; i++)
            {
                var vieja = _fotos.Where(s0 => s0.Minuto <= minuto - _ventanas[i]).LastOrDefault();
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
            int barra = Math.Max(0, CurrentBar - 1);
            var est = new double[Math.Max(1, CuantasDominantes)];
            for (int i = 0; i < est.Length; i++) est[i] = i < candDom.Count ? candDom[i].Item1 : double.NaN;

            lock (_candado)
            {
                _perfil = perfil; _S = S; _futuro = futuro; _base = baseUsada; _baseOrigen = origen;
                _zeroVol = zeroVol; _zeroOi = zeroOi; _netVol = netVol; _netOi = netOi;
                _mpVol = mpVol; _mnVol = mnVol; _mpOi = mpOi; _mnOi = mnOi;
                _maxAbsVol = maxAbsVol; _maxAbsOi = maxAbsOi; _maxAbsConv = maxAbsConv;
                _doms = candDom; _libroConvUsado = convPorVol ? "vol" : "OI"; _libroDomUsado = libroDom;
                _cuadrante = nombre; _cuadranteCorto = corto; _cuadranteN = cuadN;
                _picoFut = picoFut; _picoGex = picoGex; _convEnPrecio = convPrecio; _mucho = mucho;
                _alerta = alerta; _alertaHasta = alertaHasta;
                _maxChange = mc;
                _estela[barra] = est;
                if (_estela.Count > 6000) foreach (var k in _estela.Keys.Where(k => k < barra - 5000).ToList()) _estela.Remove(k);
            }

            if ((DateTime.UtcNow - _ultimoAudit).TotalSeconds >= 60)
            {
                _ultimoAudit = DateTime.UtcNow;
                var inv = CultureInfo.InvariantCulture;
                Log(string.Format(inv, "AUDIT fut={0:F2} S={1:F2} base={2:F2} origen={3} strikes={4} netVol={5:F3}B netOi={6:F3}B zeroVol={7:F2} zeroOi={8:F2} mpVol={9:F2} mnVol={10:F2} doms={11} libroDom={12} conv={13} q={14} pico={15:F2} picoGex={16:F0}M mucho={17} convPrecio={18:F0}M mc30={19:F2}:{20:F0}M edadFeed={21:F1}min vivaActiva={22}",
                    futuro, S, baseUsada, origen.Replace(' ', '_'), perfil.Count, netVol / 1e9, netOi / 1e9, zeroVol, zeroOi, mpVol, mnVol,
                    string.Join("/", candDom.Select(d => d.Item1.ToString("F2", inv) + "=" + (d.Item2 / 1e6).ToString("F0", inv) + "M")),
                    libroDom, convPorVol ? "vol" : "OI", cuadN, picoFut, picoGex / 1e6, mucho, convPrecio / 1e6, mc[4].Fut, mc[4].Delta / 1e6, c.EdadMin, _viva.Activa));
            }
        }

        private readonly Dictionary<double, double> _convOiTmp = new();

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

        // ==================================================================
        // Lo que se anota para el laboratorio
        // ==================================================================
        private void Anotar(int bar)
        {
            if (!AnotarCentinela || bar <= _barraCent) return;
            int cerrada = bar - 1;
            if (cerrada < 1) { _barraCent = bar; return; }
            _barraCent = bar;
            if (_cent == null)
            {
                var instr = InstrumentInfo != null ? InstrumentInfo.Instrument : "x";
                var marco = ChartInfo != null && ChartInfo.ChartType != null ? ChartInfo.ChartType + "-" + ChartInfo.TimeFrame : "x";
                _cent = new Centinela("hoy-" + instr, marco);
            }
            IndicatorCandle c;
            try { c = GetCandle(cerrada); } catch { return; }
            if (c == null) return;
            var niv = new List<KeyValuePair<string, double>>();
            double sp;
            lock (_candado)
            {
                sp = _S;
                void Add(string k, double v) { if (!double.IsNaN(v) && v > 0) niv.Add(new KeyValuePair<string, double>(k, v)); }
                Add("zero_vol", _zeroVol); Add("zero_oi", _zeroOi);
                Add("mp_vol", _mpVol); Add("mn_vol", _mnVol); Add("mp_oi", _mpOi); Add("mn_oi", _mnOi);
                for (int i = 0; i < _doms.Count; i++) Add("dom" + i, _doms[i].Fut);
                for (int i = 0; i < _ventanas.Length; i++) Add("mc" + _ventanas[i], _maxChange[i].Fut);
                Add("pico", _picoFut);
                Add("q_cuadrante", _cuadranteN);
            }
            _cent.Anotar(cerrada, c.LastTime != default(DateTime) ? c.LastTime : c.Time,
                (double)c.Open, (double)c.High, (double)c.Low, (double)c.Close,
                (double)c.Volume, (double)c.Ticks, (double)c.Delta, sp, niv);
        }

        // ==================================================================
        // Dibujo
        // ==================================================================
        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            try { Pintar(g); }
            catch (Exception e) { Registrar(e); }
        }

        private void Pintar(RenderContext g)
        {
            if (ChartInfo == null) return;
            var cont = ChartInfo.PriceChartContainer;
            if (cont == null) return;
            try { g.SetSmoothingMode(OFT.Rendering.Context.RenderSmoothingModes.AntiAlias); } catch { }
            var area = ChartArea;
            int xr = area.Right;
            try { var cb = g.ClipBounds; if (cb.Width > 0) xr = Math.Min(area.Right, cb.Right); } catch { }
            int piso = area.Bottom - Math.Max(0, MargenInferior);
            var es = CultureInfo.GetCultureInfo("es-AR");
            var f = new RenderFont("Consolas", (float)Math.Max(6m, Math.Min(14m, TamLetra)));
            var fChica = new RenderFont("Consolas", (float)Math.Max(6m, Math.Min(12m, TamLetra - 1m)));

            if (_renders++ == 0) Log("primer render: area=" + area + " clip=" + g.ClipBounds);

            // copia del estado bajo llave
            List<Strike> perfil; double S, futuro, zeroVol, zeroOi, mpVol, mnVol, mpOi, mnOi, maxV, maxO, maxC, netVol, netOi;
            List<(double Fut, double Gex)> doms; string cuad, corto, origenBase, libroConv, libroDom, alerta; DateTime alertaHasta;
            (double Fut, double Delta)[] mc; bool mucho; double convPrecio, picoFut;
            lock (_candado)
            {
                perfil = _perfil; S = _S; futuro = _futuro; zeroVol = _zeroVol; zeroOi = _zeroOi;
                mpVol = _mpVol; mnVol = _mnVol; mpOi = _mpOi; mnOi = _mnOi; maxV = _maxAbsVol; maxO = _maxAbsOi; maxC = _maxAbsConv;
                netVol = _netVol; netOi = _netOi; doms = _doms; cuad = _cuadrante; corto = _cuadranteCorto; origenBase = _baseOrigen;
                libroConv = _libroConvUsado; libroDom = _libroDomUsado; alerta = _alerta; alertaHasta = _alertaHasta;
                mc = _maxChange; mucho = _mucho; convPrecio = _convEnPrecio; picoFut = _picoFut;
            }

            var c = _c;
            // ---- cabecera: siempre, aunque no haya datos, para que se sepa por que
            string edad = c == null ? "sin feed" : (c.EdadMin + 902.0 / 60.0).ToString("0", es) + " min tarde";
            string l1 = perfil.Count == 0
                ? "GAMMA HOY  esperando cadena" + (string.IsNullOrEmpty(_error) ? "" : " (" + _error + ")")
                : "GAMMA HOY  " + corto + "  " + cuad + "   conv " + (convPrecio >= 0 ? "+" : "-") + " (" + libroConv + ")  pico " + (double.IsNaN(picoFut) ? "--" : picoFut.ToString("N0", es)) + (mucho ? " mucho" : " poco");
            string l2 = "vol CBOE " + edad + " · OI de ayer · base " + origenBase + " · dominantes por " + libroDom
                      + (_viva.Activa ? " · vivo Rithmic " + ((int)_viva.VolumenTotalHoy()).ToString("N0", es) + " contr" : " · vivo: " + _viva.Estado);
            int yc = area.Top + 26;
            var m1 = g.MeasureString(l1, f); var m2 = g.MeasureString(l2, fChica);
            int wc = Math.Max(m1.Width, m2.Width) + 12;
            g.FillRectangle(Color.FromArgb(200, ColFondo), new Rectangle(area.Left + 6, yc - 3, wc, m1.Height + m2.Height + 8));
            g.DrawString(l1, f, perfil.Count == 0 ? ColAviso : (convPrecio >= 0 ? ColConvPos : ColConvNeg), area.Left + 12, yc);
            g.DrawString(l2, fChica, Color.FromArgb(170, ColTexto), area.Left + 12, yc + m1.Height + 2);
            if (DateTime.UtcNow < alertaHasta && alerta.Length > 0)
            {
                var ma = g.MeasureString("TRANSICION: " + alerta, f);
                int ya = yc + m1.Height + m2.Height + 10;
                g.FillRectangle(Color.FromArgb(215, 60, 35, 10), new Rectangle(area.Left + 6, ya - 2, ma.Width + 12, ma.Height + 4));
                g.DrawString("TRANSICION: " + alerta, f, ColAviso, area.Left + 12, ya);
            }
            if (perfil.Count == 0 || double.IsNaN(futuro)) return;

            int x0 = area.Left;
            int ancho = Math.Max(30, AnchoBarras);
            int xLad = VerEscalera ? xr - Math.Max(80, AnchoEscalera) : xr;
            int xConv = xLad - 6;                       // borde derecho de la convexidad
            int xl0 = x0 + ancho + 8, xl1 = (VerConvexidad ? xConv - (int)(ancho * 0.7) - 8 : xLad - 8);
            if (xl1 - xl0 < 60) { xl0 = x0 + 2; xl1 = xLad - 2; }

            // ---- barras: sombra de OI, volumen encima, convexidad a la derecha
            int alto = 5;
            try { int y1 = cont.GetYByPrice((decimal)perfil[0].Fut, false); if (perfil.Count > 1) { int y2 = cont.GetYByPrice((decimal)perfil[1].Fut, false); alto = Math.Max(2, Math.Min(9, Math.Abs(y2 - y1) - 2)); } } catch { }
            var fotos = _fotos.ToList();
            foreach (var s in perfil)
            {
                int y; try { y = cont.GetYByPrice((decimal)s.Fut, false); } catch { continue; }
                if (y < area.Top || y > piso) continue;
                if (VerSombraOI && maxO > 0 && Math.Abs(s.GexOi) > 0)
                {
                    int w = Math.Max(1, (int)(Math.Sqrt(Math.Abs(s.GexOi) / maxO) * ancho));
                    g.FillRectangle(Color.FromArgb(55, s.GexOi >= 0 ? ColPos : ColNeg), new Rectangle(x0, y - alto / 2 - 1, w, alto + 2));
                }
                if (maxV > 0 && Math.Abs(s.GexVol) > 0)
                {
                    double fr = Math.Sqrt(Math.Abs(s.GexVol) / maxV);
                    int w = Math.Max(1, (int)(fr * ancho));
                    var col = s.GexVol >= 0 ? ColPos : ColNeg;
                    g.FillRectangle(Color.FromArgb((int)(120 + 120 * fr), col), new Rectangle(x0, y - alto / 2, w, alto));
                    if (VerPelotitas)
                    {
                        // donde estaba la punta hace 15, 5 y 1 minutos: adentro = crece, afuera = decrece
                        long ahora = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
                        int[] vent = { 15, 5, 1 }; int[] rad = { 4, 3, 2 };
                        for (int i = 0; i < 3; i++)
                        {
                            var vieja = fotos.Where(z => z.Minuto <= ahora - vent[i]).LastOrDefault();
                            if (vieja == null || !vieja.GexVol.TryGetValue(s.Fut, out var gAntes)) continue;
                            int wa = Math.Max(0, (int)(Math.Sqrt(Math.Abs(gAntes) / maxV) * ancho));
                            int rr = rad[i];
                            g.FillEllipse(Color.FromArgb(235, 250, 250, 250), new Rectangle(x0 + wa - rr, y - rr, 2 * rr, 2 * rr));
                            g.DrawEllipse(new RenderPen(Color.FromArgb(200, col), 1f), new Rectangle(x0 + wa - rr, y - rr, 2 * rr, 2 * rr));
                        }
                    }
                }
                if (VerConvexidad && maxC > 0 && Math.Abs(s.Conv) > 0)
                {
                    double fr = Math.Sqrt(Math.Abs(s.Conv) / maxC);
                    int w = Math.Max(1, (int)(fr * ancho * 0.7));
                    var col = s.Conv >= 0 ? ColConvPos : ColConvNeg;
                    g.FillRectangle(Color.FromArgb((int)(110 + 120 * fr), col), new Rectangle(xConv - w, y - alto / 2, w, alto));
                }
            }
            g.DrawString("volumen hoy · sombra OI ayer", fChica, Color.FromArgb(110, ColTexto), x0 + 4, area.Top + 8);
            if (VerConvexidad) { var mcx = g.MeasureString("convexity ladder (" + libroConv + ")", fChica); g.DrawString("convexity ladder (" + libroConv + ")", fChica, Color.FromArgb(110, ColTexto), xConv - mcx.Width, area.Top + 8); }

            // ---- rayas: zero de cada libro, majors del volumen, dominantes
            void Raya(double p, Color col, float w, System.Drawing.Drawing2D.DashStyle ds, int alfa)
            {
                if (double.IsNaN(p) || p <= 0) return;
                int y; try { y = cont.GetYByPrice((decimal)p, false); } catch { return; }
                if (y < area.Top || y > piso) return;
                g.DrawLine(new RenderPen(Color.FromArgb(alfa, col), w, ds), xl0, y, xl1, y);
            }
            Raya(zeroOi, Color.FromArgb(160, 160, 170), 1f, System.Drawing.Drawing2D.DashStyle.Dot, 120);
            Raya(zeroVol, ColZero, 1.4f, System.Drawing.Drawing2D.DashStyle.Dash, 200);
            Raya(mpVol, ColPos, 1.6f, System.Drawing.Drawing2D.DashStyle.Solid, 190);
            Raya(mnVol, ColNeg, 1.6f, System.Drawing.Drawing2D.DashStyle.Solid, 190);
            for (int i = 0; i < doms.Count; i++) Raya(doms[i].Fut, ColDom, i == 0 ? 1.6f : 1.1f, System.Drawing.Drawing2D.DashStyle.Solid, i == 0 ? 220 : 160);

            // ---- estela: la dominante que regia en cada vela
            if (VerEstela)
            {
                Dictionary<int, double[]> est; lock (_candado) est = new Dictionary<int, double[]>(_estela);
                int desde = Math.Max(0, FirstVisibleBarNumber), hasta = Math.Min(CurrentBar - 1, LastVisibleBarNumber);
                for (int b = desde; b <= hasta; b++)
                {
                    if (!est.TryGetValue(b, out var d)) continue;
                    int x; try { x = cont.GetXByBar(b, false); } catch { continue; }
                    for (int i = 0; i < d.Length; i++)
                    {
                        if (double.IsNaN(d[i])) continue;
                        int y; try { y = cont.GetYByPrice((decimal)d[i], false); } catch { continue; }
                        if (y < area.Top || y > piso) continue;
                        g.FillRectangle(Color.FromArgb(i == 0 ? 210 : 130, ColDom), new Rectangle(x - 1, y - 1, 3, 2));
                    }
                }
            }

            // ---- big trades de opciones, sobre la vela del momento
            if (VerBigTrades && _viva.Activa)
            {
                foreach (var t in _viva.Grandes())
                {
                    int b = BarraDe(t.Hora); if (b < 0) continue;
                    int x; try { x = cont.GetXByBar(b, false); } catch { continue; }
                    double pr; try { pr = (double)GetCandle(b).Close; } catch { continue; }
                    int y; try { y = cont.GetYByPrice((decimal)pr, false); } catch { continue; }
                    if (y < area.Top || y > piso || x < xl0 || x > xl1) continue;
                    var col = t.EsCall ? ColPos : ColNeg;
                    int rr = 9;
                    g.FillEllipse(Color.FromArgb(150, col), new Rectangle(x - rr, y - rr, 2 * rr, 2 * rr));
                    var tx = ((int)t.Contratos).ToString(es); var mt = g.MeasureString(tx, fChica);
                    g.DrawString(tx, fChica, Color.White, x - mt.Width / 2, y - mt.Height / 2);
                }
            }

            // ---- la escalera pegada al eje
            if (VerEscalera) Escalera(g, cont, area, xLad, xr, piso, f, fChica, futuro, zeroVol, zeroOi, mpVol, mnVol, doms, mc, netVol, netOi);
        }

        private int BarraDe(DateTime horaUtc)
        {
            try
            {
                for (int b = CurrentBar - 1; b >= Math.Max(0, CurrentBar - 600); b--)
                {
                    var c = GetCandle(b);
                    var t0 = c.Time.Kind == DateTimeKind.Utc ? c.Time : c.Time.ToUniversalTime();
                    if (t0 <= horaUtc) return b;
                }
            }
            catch { }
            return -1;
        }

        private void Escalera(RenderContext g, IChartContainer cont, Rectangle area, int xLad, int xr, int piso,
                              RenderFont f, RenderFont fChica, double futuro, double zeroVol, double zeroOi, double mpVol, double mnVol,
                              List<(double Fut, double Gex)> doms, (double Fut, double Delta)[] mc, double netVol, double netOi)
        {
            var es = CultureInfo.GetCultureInfo("es-AR");
            var rect = new Rectangle(xLad, area.Top, xr - xLad, Math.Max(40, piso - area.Top));
            g.FillRectangle(Color.FromArgb(235, ColFondo), rect);
            g.DrawLine(new RenderPen(Color.FromArgb(90, ColTexto), 1f), rect.Left, rect.Top, rect.Left, rect.Bottom);

            int hf = g.MeasureString("X", f).Height;
            int yc = rect.Top + 26;
            string Bm(double v) => Math.Abs(v) >= 1e9 ? (v / 1e9).ToString("+0.0;-0.0", es) + "B" : (v / 1e6).ToString("+0;-0", es) + "M";
            g.DrawString("net vol " + Bm(netVol), f, netVol >= 0 ? ColPos : ColNeg, rect.Left + 6, yc); yc += hf + 1;
            g.DrawString("net OI  " + Bm(netOi), fChica, Color.FromArgb(160, ColTexto), rect.Left + 6, yc); yc += hf + 1;
            // max change: strike con mayor cambio de GEX por volumen
            for (int i = 0; i < mc.Length; i++)
            {
                if (double.IsNaN(mc[i].Fut) || mc[i].Delta == 0) continue;
                string t = "Δ" + _ventanas[i].ToString(es).PadLeft(2) + "' " + mc[i].Fut.ToString("N0", es) + " " + Bm(mc[i].Delta);
                g.DrawString(t, fChica, mc[i].Delta >= 0 ? ColPos : ColNeg, rect.Left + 6, yc); yc += hf;
            }
            yc += 4;
            g.DrawLine(new RenderPen(Color.FromArgb(60, ColTexto), 1f), rect.Left + 4, yc, rect.Right - 4, yc); yc += 4;

            // filas a la altura de su precio, como un DOM, sin pisarse
            var filas = new List<(string N, double P, Color C, bool esPrecio)>();
            void Add(string n, double p, Color c) { if (!double.IsNaN(p) && p > 0) filas.Add((n, p, c, false)); }
            Add("0Γ vol", zeroVol, ColZero); Add("0Γ ayer", zeroOi, Color.FromArgb(160, 160, 170));
            Add("+Γ", mpVol, ColPos); Add("−Γ", mnVol, ColNeg);
            for (int i = 0; i < doms.Count; i++) Add("D" + (i + 1), doms[i].Fut, ColDom);
            filas.Add(("", futuro, Color.FromArgb(31, 143, 124), true));
            filas.Sort((a, b) => b.P.CompareTo(a.P));
            int n = filas.Count; var y = new int[n]; var alt = new int[n]; var enPant = new bool[n];
            int yTop = yc, yBot = rect.Bottom - 4;
            for (int i = 0; i < n; i++)
            {
                alt[i] = hf + 4;
                int yp = int.MinValue; try { yp = cont.GetYByPrice((decimal)filas[i].P, false); } catch { }
                enPant[i] = yp != int.MinValue && yp >= area.Top && yp <= area.Bottom;
                y[i] = enPant[i] ? yp - alt[i] / 2 : (yp != int.MinValue && yp < area.Top ? yTop : yBot - alt[i]);
            }
            int minY = yTop; for (int i = 0; i < n; i++) { if (y[i] < minY) y[i] = minY; minY = y[i] + alt[i] + 1; }
            int maxY = yBot; for (int i = n - 1; i >= 0; i--) { if (y[i] + alt[i] > maxY) y[i] = maxY - alt[i]; maxY = y[i] - 1; }
            for (int i = 0; i < n; i++)
            {
                if (y[i] < yTop) continue;
                var q = filas[i];
                if (q.esPrecio)
                {
                    g.FillRectangle(q.C, new Rectangle(rect.Left + 2, y[i], rect.Width - 4, alt[i] - 1));
                    g.DrawString("▶ " + futuro.ToString("N2", es), f, Color.White, rect.Left + 6, y[i] + 1);
                    continue;
                }
                double dist = q.P - futuro;
                string izq = q.N + " " + q.P.ToString("N0", es) + " " + (dist >= 0 ? "+" : "") + dist.ToString("0", es);
                g.FillRectangle(Color.FromArgb(230, q.C), new Rectangle(rect.Left + 3, y[i] + 2, 3, hf - 2));
                g.DrawString(izq, f, Color.FromArgb(240, ColTexto), rect.Left + 9, y[i] + 1);
                if (enPant[i]) g.FillRectangle(Color.FromArgb(230, q.C), new Rectangle(rect.Right - 5, y[i] + hf / 2, 5, 2));
            }
        }

        // ==================================================================
        // Registro
        // ==================================================================
        private static void Log(string msg)
        {
            try
            {
                var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "pythiagex-gammahoy.log");
                File.AppendAllText(p, DateTime.Now.ToString("s") + "  " + msg + "\n");
            }
            catch { }
        }

        private static void Registrar(Exception e)
        {
            Log("EXCEPCION " + e.GetType().Name + ": " + e.Message + " | " + (e.StackTrace ?? "").Replace("\n", " ").Substring(0, Math.Min(300, (e.StackTrace ?? "").Length)));
        }
    }
}
