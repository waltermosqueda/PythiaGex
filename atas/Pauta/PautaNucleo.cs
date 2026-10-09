using System;
using System.Collections.Generic;
using System.Globalization;

namespace Pauta
{
    /// <summary>
    /// EL NUCLEO de la pauta: codigo puro, sin nada de ATAS, sin dibujar, sin archivos. Es el MISMO calculo que
    /// laboratorio/pauta/p_P1_lib.py (formalizacion P1 "las marcaciones") y p_P2_lib.py (P2 "extremos del pre-mercado"),
    /// tal como quedaron pre-registrados y medidos el 24-09. El arnes (carpeta arnes/) compila este archivo sobre las
    /// mismas velas que uso el Python y se compara nivel por nivel y operacion por operacion (p_paridad_pauta.py).
    ///
    /// Que hace, barra por barra (1 s o 1 min, la resolucion la pone quien lo alimenta):
    ///  - corta las sesiones de CME (18:00 NY -> 17:00 NY del dia siguiente; la sesion se llama por su fecha de cierre),
    ///  - agrega velas de 1 min y de 5 min alineadas al reloj, solo con minutos que tuvieron operaciones (como ATAS),
    ///  - acumula N1 (max/min de la sesion electronica hasta las 09:15), N2 (max/min/cierre de la rueda 09:30-16:00 de la
    ///    sesion anterior), N4 (max/min de 1 min de 08:15 a 09:14:59), la EMA 200 de los cierres de 1 min (N5, semilla el
    ///    primer minuto de la sesion) y P0 (ultimo cierre antes de las 09:15:00),
    ///  - a las 09:15:00 NY CONGELA: pivotes de 5 min (5 a cada lado, convencion de TradingView), rectas N3 por los dos
    ///    ultimos pivotes, y arma las limitadas de P1 (compra debajo de P0, venta encima, dentro de D; fijos con el mismo
    ///    precio se funden) y las dos limitadas de P2 (N4 max/min, sin filtro),
    ///  - de 09:15 a 09:45 llena las limitadas (atravesadas un tick), simula el bracket (stop fijo, objetivo limitado, stop a
    ///    la entrada al 40 % del objetivo), el reingreso unico tras salida en cero y el cierre por tiempo de las 10:30.
    ///
    /// Segundos sin operaciones: la grilla del Python (p_P1_lib.grilla) rellena de 09:13:59 a 10:30:00 cada segundo vacio
    /// con o=h=l=c=ultimo cierre y la ultima punta conocida. El nucleo hace exactamente eso (barras "de relleno", que NO
    /// entran en los agregados ni en los niveles, solo en la ejecucion). En un grafico de 1 min no hay minutos vacios.
    ///
    /// Barras sin punta (velas de 1 min de ATAS, o la cache de MES): bid = apertura - medio tick, ask = apertura + medio
    /// tick, que es la misma convencion que uso el Python para las 24 sesiones de MES en 1 min.
    ///
    /// Lo que NO afirma: nada. P1 y P2 NO PASARON el criterio pre-registrado en ningun instrumento (0 de 7 condiciones).
    /// Este nucleo existe para poner en el grafico lo que el canal usa y anotar cada disparo para la prueba hacia
    /// adelante, que no esta habilitada. No manda ordenes.
    /// </summary>
    public static class RelojNy
    {
        private static TimeZoneInfo _ny;
        private static readonly DateTime Epoca = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static TimeZoneInfo Zona
        {
            get
            {
                if (_ny == null)
                {
                    try { _ny = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
                    catch { _ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
                }
                return _ny;
            }
        }

        public static long UtcSeg(DateTime utc) => (long)Math.Floor((utc - Epoca).TotalSeconds);
        public static DateTime DesdeUtcSeg(long s) => Epoca.AddSeconds(s);

        /// <summary>sod = segundos desde la medianoche de NY; fechaNy = yyyymmdd de NY; sesion = yyyymmdd de (NY + 6 h),
        /// que es la sesion de CME (lo que arranca a las 18:00 pertenece al dia siguiente).</summary>
        public static void Descomponer(long utcSeg, out int sod, out int fechaNy, out int sesion)
        {
            var ny = TimeZoneInfo.ConvertTimeFromUtc(DesdeUtcSeg(utcSeg), Zona);
            sod = ny.Hour * 3600 + ny.Minute * 60 + ny.Second;
            fechaNy = ny.Year * 10000 + ny.Month * 100 + ny.Day;
            var s = ny.AddHours(6);
            sesion = s.Year * 10000 + s.Month * 100 + s.Day;
        }

        public static string Hms(int sod) => string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", sod / 3600, (sod / 60) % 60, sod % 60);
        public static string Hm(int sod) => string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", sod / 3600, (sod / 60) % 60);
        public static string FechaIso(int ymd) => string.Format(CultureInfo.InvariantCulture, "{0:0000}-{1:00}-{2:00}", ymd / 10000, (ymd / 100) % 100, ymd % 100);
        public static DateTime Fecha(int ymd) => new DateTime(ymd / 10000, (ymd / 100) % 100, ymd % 100);
        public static int DiasEntre(int a, int b) => (Fecha(b) - Fecha(a)).Days;
        public static string HmsUtc(long utcSeg) { Descomponer(utcSeg, out int sod, out _, out _); return Hms(sod); }
    }

    /// <summary>Una barra de entrada al nucleo. Bid/Ask = double.NaN si no hay punta (velas de 1 min).</summary>
    public struct Barra
    {
        public long Utc; public int Sod, FechaNy, Sesion, Paso;
        public double O, H, L, C, Bid, Ask;
        public string Contrato;
        public bool Relleno;

        public static Barra Crear(long utcSeg, double o, double h, double l, double c, double bid, double ask, string contrato, int paso)
        {
            var b = new Barra { Utc = utcSeg, O = o, H = h, L = l, C = c, Bid = bid, Ask = ask, Contrato = contrato, Paso = paso };
            RelojNy.Descomponer(utcSeg, out b.Sod, out b.FechaNy, out b.Sesion);
            return b;
        }
    }

    public sealed class Parametros
    {
        public double Tick = 0.25;
        /// <summary>Filtro de distancia a P0 de las ordenes de P1 (MNQ 60 / MES 15).</summary>
        public double D = 60.0;
        public double Stop = 10.0;
        public double Objetivo = 40.0;
        /// <summary>Fraccion del objetivo a la que el stop pasa a la entrada (0 = nunca).</summary>
        public double BeFrac = 0.40;
        public int EmaN = 200;
        public int PivIzq = 5, PivDer = 5;
        public bool Reingreso = true;
        /// <summary>Llenado por toque (V1) en vez de atravesar un tick.</summary>
        public bool Toque = false;
        public int HCongela = 9 * 3600 + 15 * 60;
        public int HFinOrdenes = 9 * 3600 + 45 * 60;
        public int HCierre = 10 * 3600 + 30 * 60;
        /// <summary>Ajuste que se suma a N2 cuando la sesion anterior era de OTRO contrato (solo el laboratorio; en ATAS
        /// el grafico es de un solo contrato y nunca se aplica). MNQ +292,50 medido, MES +66 SUPUESTO.</summary>
        public double RollSpread = 0.0;
        public bool RollMedido = true;
        /// <summary>Tope de 100 USD de P2 en puntos (MNQ 50 / MES 20).</summary>
        public double TopeP2 = 50.0;
        public bool P1Activa = true;
        public bool P2Activa = true;
    }

    public sealed class Recta
    {
        public long UtcPivote1, UtcPivote2; public double X1, Y1, X2, Y2;
        public double Valor(double x) { return Y2 + (Y2 - Y1) / (X2 - X1) * (x - X2); }
    }

    /// <summary>Un nivel candidato de P1 (antes o despues del filtro D); Orden = quedo como limitada.</summary>
    public sealed class Nivel
    {
        public double Precio; public string Familia, Tipo; public int Lado; public bool Orden; public double Dist;
    }

    public sealed class Orden
    {
        public double Precio;          // precio actual (las de linea y EMA se reposicionan por minuto)
        public double PrecioInicial;
        public int Lado; public string Familia, Tipo; public Recta Recta;
    }

    public sealed class Operacion
    {
        public int Regla;              // 1 = P1, 2 = P2
        public int NOp;                // 1 o 2 (reingreso)
        public int Sesion, Lado;
        public string Familia = "", Tipo = "", EntradaPor = "limite", Motivo = "", NivelP2 = "";
        public double Nivel, Entrada, Salida = double.NaN, Stop, Objetivo, Bruto = double.NaN, P0 = double.NaN;
        public long UtcEnt, UtcSal = -1, UtcBe = -1;
        public int SodEnt, SodSal = -1, Paso = 1;
        public bool BeArmado, Abierta = true, EntradaEnRelleno;
        public double StopVigente;     // para dibujar: el stop original o la entrada si el BE ya rige
        public int DurS => UtcSal < 0 ? 0 : (int)(UtcSal - UtcEnt);
    }

    public sealed class SesionInfo
    {
        public int Sesion; public string Contrato = ""; public int Paso = 1;
        public long T0Utc, UtcCongela = -1, UtcFinOrd = -1, UtcCierre = -1, UtcPrimera = -1, UtcUltima = -1;
        public bool Congelada;
        public double P0 = double.NaN;
        public readonly List<Nivel> Candidatos = new List<Nivel>();
        public readonly List<Orden> Ordenes = new List<Orden>();
        public Recta LnVenta, LnCompra;
        public readonly List<KeyValuePair<long, double>> EmaRepos = new List<KeyValuePair<long, double>>();
        public double N4Hi = double.NaN, N4Lo = double.NaN;
        public string Notas = "";
        public int NMinEma;
        public readonly List<Operacion> OpsP1 = new List<Operacion>();
        public readonly List<Operacion> OpsP2 = new List<Operacion>();
        public string SesionIso => RelojNy.FechaIso(Sesion);
    }

    public enum TipoEvento { Congelado = 0, Entrada = 1, BeArmado = 2, Salida = 3 }

    public sealed class Evento
    {
        public TipoEvento Tipo; public int Regla; public SesionInfo Sesion; public Operacion Op; public long Utc;
    }

    public sealed class PautaNucleo
    {
        public readonly Parametros P;
        public readonly List<SesionInfo> Sesiones = new List<SesionInfo>();
        public readonly List<Evento> Eventos = new List<Evento>();
        public SesionInfo Actual { get; private set; }
        public long Barras { get; private set; }
        public long Rellenos { get; private set; }

        // ---- estado de la sesion en curso
        private double _n1Max, _n1Min, _n4Max, _n4Min; private bool _hayN4;
        private double _rthMax, _rthMin, _rthCierre; private int _rthUltSod; private bool _hayRth;
        private double _p0; private bool _hayP0;
        private double _ultCierre = double.NaN, _ultBid = double.NaN, _ultAsk = double.NaN;
        private long _ultUtcReal = -1;
        private int _ultGridSod = -1;
        private double _ema; private bool _hayEma; private int _nMin;
        // agregados abiertos
        private long _minKey = -1; private double _minO, _minH, _minL, _minC;
        private long _k5 = -1; private double _h5, _l5; private long _t5;
        private readonly List<long> _v5t = new List<long>();
        private readonly List<double> _v5h = new List<double>(), _v5l = new List<double>();
        // la sesion anterior (para N2)
        private bool _hayPrev; private int _prevSesion; private string _prevContrato; private bool _prevHayRth;
        private double _prevRthMax, _prevRthMin, _prevRthCierre; private int _prevRthUltSod;

        private MotorP1 _m1; private MotorP2 _m2;

        public PautaNucleo(Parametros p) { P = p; }

        public double Redondear(double x) { return Math.Round(x / P.Tick, MidpointRounding.ToEven) * P.Tick; }

        // ==================================================================================== entrada
        public void Agregar(in Barra b)
        {
            Barras++;
            if (Actual == null || b.Sesion != Actual.Sesion) NuevaSesion(b);
            var s = Actual;
            // relleno de la grilla (solo el dia de la sesion, de gridStart a 10:30:00)
            if (b.FechaNy == s.Sesion && b.Sod > GridStart(s.Paso) && !b.Relleno)
            {
                int paso = s.Paso;
                int desde = _ultGridSod < 0 ? GridStart(paso) : _ultGridSod + paso;
                int hasta = Math.Min(b.Sod - paso, P.HCierre);
                for (int sod = desde; sod <= hasta; sod += paso)
                {
                    var r = new Barra
                    {
                        Utc = b.Utc - (b.Sod - sod), Sod = sod, FechaNy = b.FechaNy, Sesion = b.Sesion, Paso = paso,
                        O = _ultCierre, H = _ultCierre, L = _ultCierre, C = _ultCierre, Bid = _ultBid, Ask = _ultAsk,
                        Contrato = b.Contrato, Relleno = true,
                    };
                    Rellenos++;
                    Procesar(r);
                }
            }
            Procesar(b);
        }

        private static int GridStart(int paso) { int g = 9 * 3600 + 15 * 60 - 61; return paso <= 1 ? g : (g / paso) * paso; }

        private void NuevaSesion(in Barra b)
        {
            if (Actual != null)
            {
                _hayPrev = true; _prevSesion = Actual.Sesion; _prevContrato = Actual.Contrato; _prevHayRth = _hayRth;
                _prevRthMax = _rthMax; _prevRthMin = _rthMin; _prevRthCierre = _rthCierre; _prevRthUltSod = _rthUltSod;
            }
            var s = new SesionInfo { Sesion = b.Sesion, Contrato = b.Contrato ?? "", Paso = Math.Max(1, b.Paso) };
            s.T0Utc = b.Utc - b.Sod + 18 * 3600;           // 18:00 NY del dia de NY de la primera barra
            s.UtcPrimera = b.Utc;
            long mediaNocheSesion = b.Utc - b.Sod + (b.FechaNy == b.Sesion ? 0 : 24 * 3600);
            s.UtcCongela = mediaNocheSesion + P.HCongela; s.UtcFinOrd = mediaNocheSesion + P.HFinOrdenes; s.UtcCierre = mediaNocheSesion + P.HCierre;
            Sesiones.Add(s); Actual = s;
            _n1Max = double.MinValue; _n1Min = double.MaxValue; _n4Max = double.MinValue; _n4Min = double.MaxValue; _hayN4 = false;
            _rthMax = double.MinValue; _rthMin = double.MaxValue; _rthCierre = double.NaN; _rthUltSod = -1; _hayRth = false;
            _hayP0 = false; _ultGridSod = -1;
            _hayEma = false; _nMin = 0; _minKey = -1; _k5 = -1;
            _v5t.Clear(); _v5h.Clear(); _v5l.Clear();
            _m1 = null; _m2 = null;
        }

        private void Procesar(in Barra b)
        {
            var s = Actual;
            if (!b.Relleno) s.UtcUltima = b.Utc;
            // 1) cerrar los agregados que terminaron antes de esta barra
            CerrarAgregados(b.Utc);
            // 2) congelar a las 09:15:00 del dia de la sesion
            bool diaSesion = b.FechaNy == s.Sesion;
            if (diaSesion && b.Sod >= P.HCongela && !s.Congelada) Congelar(b);
            // 3) ejecucion (grilla)
            if (s.Congelada && diaSesion && b.Sod >= P.HCongela && b.Sod <= P.HCierre)
            {
                if (_m1 != null) _m1.Paso(b, _ema, _hayEma);
                if (_m2 != null) _m2.Paso(b);
            }
            if (diaSesion && b.Sod >= GridStart(s.Paso) && b.Sod <= P.HCierre) _ultGridSod = b.Sod;
            if (b.Relleno) return;
            // 4) acumular la barra real
            bool antes = !diaSesion || b.Sod < P.HCongela;
            if (antes)
            {
                if (b.H > _n1Max) _n1Max = b.H; if (b.L < _n1Min) _n1Min = b.L;
                _p0 = b.C; _hayP0 = true;
                if (diaSesion && b.Sod >= 8 * 3600 + 15 * 60)
                {
                    if (b.H > _n4Max) _n4Max = b.H; if (b.L < _n4Min) _n4Min = b.L; _hayN4 = true;
                }
            }
            if (diaSesion && b.Sod >= 9 * 3600 + 30 * 60 && b.Sod < 16 * 3600)
            {
                if (b.H > _rthMax) _rthMax = b.H; if (b.L < _rthMin) _rthMin = b.L; _rthCierre = b.C; _rthUltSod = b.Sod; _hayRth = true;
            }
            _ultCierre = b.C; _ultBid = b.Bid; _ultAsk = b.Ask; _ultUtcReal = b.Utc;
            // agregados de 1 y 5 min (solo barras reales)
            long km = b.Utc / 60;
            if (km != _minKey) { _minKey = km; _minO = b.O; _minH = b.H; _minL = b.L; _minC = b.C; }
            else { if (b.H > _minH) _minH = b.H; if (b.L < _minL) _minL = b.L; _minC = b.C; }
            if (!s.Congelada)
            {
                long k5 = b.Utc / 300;
                if (k5 != _k5) { _k5 = k5; _t5 = k5 * 300; _h5 = b.H; _l5 = b.L; }
                else { if (b.H > _h5) _h5 = b.H; if (b.L < _l5) _l5 = b.L; }
            }
        }

        private void CerrarAgregados(long utc)
        {
            if (_minKey >= 0 && (_minKey + 1) * 60 <= utc)
            {
                double a = 2.0 / (P.EmaN + 1);
                _ema = _hayEma ? a * _minC + (1 - a) * _ema : _minC;
                _hayEma = true; _nMin++;
                _minKey = -1;
            }
            if (_k5 >= 0 && (_k5 + 1) * 300 <= utc)
            {
                _v5t.Add(_t5); _v5h.Add(_h5); _v5l.Add(_l5);
                _k5 = -1;
            }
        }

        // ==================================================================================== congelado (09:15:00)
        private void Congelar(in Barra b)
        {
            var s = Actual;
            s.Congelada = true; s.NMinEma = _nMin;
            var notas = new List<string>();
            if (s.UtcPrimera - s.T0Utc > 600) { RelojNy.Descomponer(s.UtcPrimera, out int sodA, out _, out _); notas.Add("arranque " + RelojNy.Hm(sodA)); }
            s.P0 = _hayP0 ? _p0 : double.NaN;
            var est = new List<KeyValuePair<double, string>>();
            if (_n1Max > double.MinValue)
            {
                est.Add(new KeyValuePair<double, string>(_n1Max, "N1_max"));
                est.Add(new KeyValuePair<double, string>(_n1Min, "N1_min"));
            }
            if (_hayPrev)
            {
                if (_prevHayRth)
                {
                    double aj = 0.0;
                    if (_prevContrato != s.Contrato && P.RollSpread != 0.0)
                    {
                        aj = P.RollSpread;
                        notas.Add("N2_roll+" + aj.ToString("0.00", CultureInfo.InvariantCulture) + (P.RollMedido ? "" : "_SUPUESTO"));
                    }
                    if (RelojNy.DiasEntre(_prevSesion, s.Sesion) > 3) notas.Add("N2_lejano(" + RelojNy.FechaIso(_prevSesion) + ")");
                    if (_prevRthUltSod / 3600 < 15) notas.Add("N2_rueda_corta");
                    est.Add(new KeyValuePair<double, string>(_prevRthMax + aj, "N2_max"));
                    est.Add(new KeyValuePair<double, string>(_prevRthMin + aj, "N2_min"));
                    est.Add(new KeyValuePair<double, string>(_prevRthCierre + aj, "N2_cierre"));
                }
                else notas.Add("N2_sin_rth");
            }
            else notas.Add("N2_sin_previa");
            if (_hayN4)
            {
                est.Add(new KeyValuePair<double, string>(_n4Max, "N4_max"));
                est.Add(new KeyValuePair<double, string>(_n4Min, "N4_min"));
                s.N4Hi = _n4Max; s.N4Lo = _n4Min;
            }
            else notas.Add("N4_sin_datos");
            // N3: pivotes de 5 min con datos anteriores al congelado
            long tCong = s.UtcCongela;
            var ph = new List<int>(); var pl = new List<int>();
            Pivotes(ph, pl);
            s.LnVenta = Linea(ph, _v5h, tCong, s.T0Utc);
            s.LnCompra = Linea(pl, _v5l, tCong, s.T0Utc);
            if (s.LnVenta == null) notas.Add("N3_venta_sin_2_pivotes");
            if (s.LnCompra == null) notas.Add("N3_compra_sin_2_pivotes");
            s.Notas = string.Join(";", notas);

            // ordenes iniciales de P1 (p_P1_lib.ordenes_iniciales con dyn=True)
            double xCong = (tCong - s.T0Utc) / 60.0 + 0.5;
            var cand = new List<Nivel>();
            foreach (var e in est) cand.Add(new Nivel { Precio = Redondear(e.Key), Familia = e.Value, Tipo = "fijo" });
            if (s.LnVenta != null) cand.Add(new Nivel { Precio = Redondear(s.LnVenta.Valor(xCong)), Familia = "N3_pivotes_altos", Tipo = "linea" });
            if (s.LnCompra != null) cand.Add(new Nivel { Precio = Redondear(s.LnCompra.Valor(xCong)), Familia = "N3_pivotes_bajos", Tipo = "linea" });
            if (_hayEma) cand.Add(new Nivel { Precio = Redondear(_ema), Familia = "N5_ema200", Tipo = "ema" });
            var fijos = new List<Orden>(); var otros = new List<Orden>();
            foreach (var n in cand)
            {
                n.Dist = n.Precio - s.P0;
                s.Candidatos.Add(n);
                if (double.IsNaN(s.P0) || n.Precio == s.P0 || Math.Abs(n.Precio - s.P0) > P.D) continue;
                n.Lado = n.Precio < s.P0 ? 1 : -1;
                n.Orden = true;
                if (n.Tipo == "fijo")
                {
                    Orden ya = null;
                    foreach (var f in fijos) if (f.Precio == n.Precio) { ya = f; break; }
                    if (ya != null) { ya.Familia += "+" + n.Familia; continue; }
                    fijos.Add(new Orden { Precio = n.Precio, PrecioInicial = n.Precio, Lado = n.Lado, Familia = n.Familia, Tipo = "fijo" });
                }
                else
                {
                    otros.Add(new Orden
                    {
                        Precio = n.Precio, PrecioInicial = n.Precio, Lado = n.Lado, Familia = n.Familia, Tipo = n.Tipo,
                        Recta = n.Tipo == "linea" ? (n.Familia == "N3_pivotes_altos" ? s.LnVenta : s.LnCompra) : null,
                    });
                }
            }
            s.Ordenes.AddRange(fijos); s.Ordenes.AddRange(otros);
            if (P.P1Activa && s.Ordenes.Count > 0) _m1 = new MotorP1(this, s);
            if (P.P2Activa && _hayN4) _m2 = new MotorP2(this, s);
            Eventos.Add(new Evento { Tipo = TipoEvento.Congelado, Regla = 0, Sesion = s, Utc = b.Utc });
        }

        /// <summary>Pivotes de 5 min, convencion de TradingView: un igual a la IZQUIERDA no veta, un igual a la DERECHA si
        /// (p_P1_lib.pivotes, reusado de directriz/replica.py).</summary>
        private void Pivotes(List<int> ph, List<int> pl)
        {
            int n = _v5t.Count, izq = P.PivIzq, der = P.PivDer;
            for (int i = izq; i < n - der; i++)
            {
                bool esH = true, esL = true;
                double h = _v5h[i], l = _v5l[i];
                for (int k = i - izq; k < i && (esH || esL); k++) { if (!(h >= _v5h[k])) esH = false; if (!(l <= _v5l[k])) esL = false; }
                for (int k = i + 1; k <= i + der && (esH || esL); k++) { if (!(h > _v5h[k])) esH = false; if (!(l < _v5l[k])) esL = false; }
                if (esH) ph.Add(i);
                if (esL) pl.Add(i);
            }
        }

        private Recta Linea(List<int> idx, List<double> campo, long tCong, long t0)
        {
            var ok = new List<int>();
            foreach (int i in idx) if (_v5t[i] + 300L * (P.PivDer + 1) <= tCong) ok.Add(i);
            if (ok.Count < 2) return null;
            int i1 = ok[ok.Count - 2], i2 = ok[ok.Count - 1];
            return new Recta
            {
                UtcPivote1 = _v5t[i1], UtcPivote2 = _v5t[i2],
                X1 = (_v5t[i1] - t0) / 60.0 + 2.5, Y1 = campo[i1],
                X2 = (_v5t[i2] - t0) / 60.0 + 2.5, Y2 = campo[i2],
            };
        }

        private static double BidDe(in Barra b, double tick) => double.IsNaN(b.Bid) ? b.O - tick / 2 : b.Bid;
        private static double AskDe(in Barra b, double tick) => double.IsNaN(b.Ask) ? b.O + tick / 2 : b.Ask;

        // ==================================================================================== motor P1
        /// <summary>p_P1_lib.correr_p1 + sim_pos, barra por barra y causal. Orden dentro de la barra: (1) stop vigente,
        /// (2) 40 % alcanzado y toque de la entrada en la misma barra -> cero (no en la barra de entrada de una limitada),
        /// (3) objetivo atravesado un tick, (4) 40 % -> stop a la entrada desde la barra siguiente.</summary>
        private sealed class MotorP1
        {
            private readonly PautaNucleo _n; private readonly SesionInfo _s; private readonly Parametros _p;
            private readonly List<Orden> _ords;
            private int _minutoAct = -1, _nOps; private bool _ultCero, _terminado;
            private double _ultC; private readonly double _xMin0; private readonly int _gridStart;
            private Operacion _pos; private double _sl, _tp, _beLvl; private bool _be, _limite, _entrada;

            public MotorP1(PautaNucleo n, SesionInfo s)
            {
                _n = n; _s = s; _p = n.P; _ords = s.Ordenes;
                _ultC = s.P0;
                _gridStart = GridStart(s.Paso);
                long gridUtc = s.UtcCongela - (_p.HCongela - _gridStart);
                _xMin0 = (gridUtc - s.T0Utc) / 60.0;
            }

            public void Paso(in Barra b, double ema, bool hayEma)
            {
                if (_pos != null) { PasoPosicion(b); return; }
                if (_terminado) return;
                if (!(b.Sod < _p.HFinOrdenes && (_nOps == 0 || (_p.Reingreso && _nOps == 1 && _ultCero)))) { _terminado = true; return; }
                // reposicionamiento de N3/N5 al cierre de cada minuto (ANTES de mirar la barra)
                int minK = b.Sod / 60;
                Orden reposMkt = null;
                if (minK != _minutoAct)
                {
                    _minutoAct = minK;
                    double x = _xMin0 + (minK * 60 - _gridStart) / 60.0 + 0.5;
                    foreach (var od in _ords)
                    {
                        double nuevo;
                        if (od.Tipo == "linea") nuevo = _n.Redondear(od.Recta.Valor(x));
                        else if (od.Tipo == "ema") nuevo = hayEma ? _n.Redondear(ema) : od.Precio;
                        else continue;
                        if (nuevo != od.Precio)
                        {
                            od.Precio = nuevo;
                            if (od.Tipo == "ema") _s.EmaRepos.Add(new KeyValuePair<long, double>(b.Utc - (b.Sod - minK * 60), nuevo));
                            if (b.Sod > _p.HCongela && ((od.Lado > 0 && nuevo >= _ultC) || (od.Lado < 0 && nuevo <= _ultC)) && reposMkt == null)
                                reposMkt = od;
                        }
                    }
                }
                // llenados en esta barra
                Orden lleno = null; double pxFill = double.NaN; string motivoEnt = "limite";
                if (reposMkt != null) { lleno = reposMkt; pxFill = b.O; motivoEnt = "repos_mercado"; }
                else
                {
                    double mejorDist = double.MaxValue; int iMejor = -1;
                    var cruzan = new List<int>();
                    for (int i = 0; i < _ords.Count; i++)
                    {
                        var od = _ords[i]; double Lp = od.Precio;
                        bool ok = _p.Toque ? (od.Lado > 0 ? b.L <= Lp : b.H >= Lp)
                                           : (od.Lado > 0 ? b.L <= Lp - _p.Tick : b.H >= Lp + _p.Tick);
                        if (!ok) continue;
                        cruzan.Add(i);
                        double d = Math.Abs(Lp - b.O);
                        if (d < mejorDist) { mejorDist = d; iMejor = i; }     // el primero entre los empatados (orden estable)
                    }
                    if (iMejor >= 0)
                    {
                        lleno = _ords[iMejor]; pxFill = lleno.Precio;
                        var otras = new List<string>();
                        foreach (int i in cruzan) if (i != iMejor && _ords[i].Precio == pxFill) otras.Add(_ords[i].Familia);
                        if (otras.Count > 0) motivoEnt = "limite(+" + string.Join(",", otras) + ")";
                    }
                }
                if (lleno == null) { _ultC = b.C; return; }
                Abrir(b, lleno, pxFill, motivoEnt);
                PasoPosicion(b);
            }

            private void Abrir(in Barra b, Orden od, double L, string motivoEnt)
            {
                int lado = od.Lado;
                _pos = new Operacion
                {
                    Regla = 1, NOp = _nOps + 1, Sesion = _s.Sesion, Lado = lado, Familia = od.Familia, Tipo = od.Tipo, EntradaPor = motivoEnt,
                    Nivel = od.Precio, Entrada = L, Stop = L - lado * _p.Stop, Objetivo = L + lado * _p.Objetivo, P0 = _s.P0,
                    UtcEnt = b.Utc, SodEnt = b.Sod, Paso = b.Paso, EntradaEnRelleno = b.Relleno,
                };
                _pos.StopVigente = _pos.Stop;
                _sl = _pos.Stop; _tp = _pos.Objetivo; _beLvl = L + lado * _p.BeFrac * _p.Objetivo;
                _be = false; _limite = motivoEnt != "repos_mercado"; _entrada = true;
                _s.OpsP1.Add(_pos);
                _n.Eventos.Add(new Evento { Tipo = TipoEvento.Entrada, Regla = 1, Sesion = _s, Op = _pos, Utc = b.Utc });
            }

            private void PasoPosicion(in Barra b)
            {
                var op = _pos; int lado = op.Lado; double L = op.Entrada; double tick = _p.Tick;
                bool entrada = _entrada; _entrada = false;
                if (b.Sod >= _p.HCierre) { Cerrar(b, lado > 0 ? BidDe(b, tick) : AskDe(b, tick), "tiempo"); return; }
                double fav = lado > 0 ? b.H : b.L, adv = lado > 0 ? b.L : b.H;
                if (!entrada)
                {
                    if (lado * (b.O - _sl) <= 0.0) { Cerrar(b, b.O, _be ? "be_gap" : "stop_gap"); return; }
                    if (lado * (b.O - _tp) >= 0.0) { Cerrar(b, b.O, "objetivo_gap"); return; }
                }
                if (lado * (adv - _sl) <= 0.0) { Cerrar(b, _sl, _be ? "be" : "stop"); return; }
                bool arma = _p.BeFrac > 0.0 && !_be && lado * (fav - _beLvl) >= 0.0;
                if (arma && (!entrada || !_limite) && lado * (adv - L) <= 0.0)
                {
                    op.BeArmado = true; op.UtcBe = b.Utc;
                    Cerrar(b, L, "be_misma_barra"); return;
                }
                bool hitTp = _p.Toque ? lado * (fav - _tp) >= 0.0 : lado * (fav - _tp) >= tick;
                if (hitTp) { Cerrar(b, _tp, "objetivo"); return; }
                if (arma)
                {
                    _be = true; _sl = L; op.BeArmado = true; op.UtcBe = b.Utc; op.StopVigente = L;
                    _n.Eventos.Add(new Evento { Tipo = TipoEvento.BeArmado, Regla = 1, Sesion = _s, Op = op, Utc = b.Utc });
                }
            }

            private void Cerrar(in Barra b, double px, string motivo)
            {
                var op = _pos;
                op.Salida = px; op.Motivo = motivo; op.UtcSal = b.Utc; op.SodSal = b.Sod; op.Abierta = false;
                op.Bruto = op.Lado * (px - op.Entrada);
                _pos = null; _nOps++;
                _ultCero = op.Bruto == 0.0; _ultC = b.C;
                _n.Eventos.Add(new Evento { Tipo = TipoEvento.Salida, Regla = 1, Sesion = _s, Op = op, Utc = b.Utc });
            }
        }

        // ==================================================================================== motor P2
        /// <summary>p_P2_lib.correr_sesion + buscar_llenado + salir, barra por barra. Vela de entrada: solo el stop
        /// original; despues, en la misma barra: stop &gt; objetivo &gt; BE (y si la barra del BE toca la entrada, cero);
        /// con el BE armado: entrada (o apertura si hubo hueco) &gt; objetivo; cierre por tiempo al cierre de la ultima
        /// barra anterior a las 10:30:00. Las dos limitadas en la misma barra = stop de la compra ("doble").</summary>
        private sealed class MotorP2
        {
            private readonly PautaNucleo _n; private readonly SesionInfo _s; private readonly Parametros _p;
            private readonly double _hi, _lo;
            private int _reingresos; private bool _terminado;
            private Operacion _pos; private double _sl, _tp, _beLvl; private bool _be, _entrada;

            public MotorP2(PautaNucleo n, SesionInfo s) { _n = n; _s = s; _p = n.P; _hi = s.N4Hi; _lo = s.N4Lo; }

            public void Paso(in Barra b)
            {
                if (_pos != null) { PasoPosicion(b); return; }
                if (_terminado) return;
                if (b.Sod >= _p.HFinOrdenes) { _terminado = true; return; }
                double tick = _p.Tick;
                bool mc = _p.Toque ? b.L <= _lo : b.L <= _lo - tick;
                bool mv = _p.Toque ? b.H >= _hi : b.H >= _hi + tick;
                if (!mc && !mv) return;
                if (mc && mv)
                {
                    var op = Nueva(b, 1, _lo);
                    op.Salida = _lo - _p.Stop; op.Motivo = "doble_stop"; op.UtcSal = b.Utc; op.SodSal = b.Sod; op.Abierta = false;
                    op.Bruto = op.Lado * (op.Salida - op.Entrada);
                    _s.OpsP2.Add(op); _terminado = true;
                    _n.Eventos.Add(new Evento { Tipo = TipoEvento.Entrada, Regla = 2, Sesion = _s, Op = op, Utc = b.Utc });
                    _n.Eventos.Add(new Evento { Tipo = TipoEvento.Salida, Regla = 2, Sesion = _s, Op = op, Utc = b.Utc });
                    return;
                }
                _pos = mc ? Nueva(b, 1, _lo) : Nueva(b, -1, _hi);
                _sl = _pos.Stop; _tp = _pos.Objetivo; _beLvl = _pos.Entrada + _pos.Lado * _p.BeFrac * _p.Objetivo; _be = false; _entrada = true;
                _s.OpsP2.Add(_pos);
                _n.Eventos.Add(new Evento { Tipo = TipoEvento.Entrada, Regla = 2, Sesion = _s, Op = _pos, Utc = b.Utc });
                PasoPosicion(b);
            }

            private Operacion Nueva(in Barra b, int lado, double L)
            {
                var op = new Operacion
                {
                    Regla = 2, NOp = _reingresos + 1, Sesion = _s.Sesion, Lado = lado, Familia = lado > 0 ? "N4_min" : "N4_max", Tipo = "fijo",
                    NivelP2 = lado > 0 ? "lo" : "hi", Nivel = L, Entrada = L, Stop = L - lado * _p.Stop, Objetivo = L + lado * _p.Objetivo,
                    P0 = _s.P0, UtcEnt = b.Utc, SodEnt = b.Sod, Paso = b.Paso, EntradaEnRelleno = b.Relleno,
                };
                op.StopVigente = op.Stop;
                return op;
            }

            private double PxStop(in Barra b, double nivel)
            {
                int lado = _pos.Lado; double L = _pos.Entrada;
                double p = (lado > 0 && b.O < nivel) || (lado < 0 && b.O > nivel) ? b.O : nivel;
                double peor = L - lado * _p.TopeP2;
                return lado > 0 ? Math.Max(p, peor) : Math.Min(p, peor);
            }

            private void PasoPosicion(in Barra b)
            {
                var op = _pos; int lado = op.Lado; double L = op.Entrada; double tick = _p.Tick;
                bool entrada = _entrada; _entrada = false;
                bool ultima = b.Sod >= _p.HCierre - b.Paso;          // ultima barra anterior a las 10:30:00
                bool hitSl = lado > 0 ? b.L <= _sl : b.H >= _sl;
                bool hitTp = _p.Toque ? (lado > 0 ? b.H >= _tp : b.L <= _tp) : (lado > 0 ? b.H >= _tp + tick : b.L <= _tp - tick);
                bool hitEnt = lado > 0 ? b.L <= L : b.H >= L;
                bool beAct = _p.BeFrac > 0.0 && (lado > 0 ? b.H >= _beLvl : b.L <= _beLvl);
                if (entrada)
                {
                    if (hitSl) { Cerrar(b, _sl, "stop_vela_entrada"); return; }
                    if (ultima) { Cerrar(b, b.C, "tiempo"); return; }
                    return;
                }
                if (!_be)
                {
                    if (hitSl) { Cerrar(b, PxStop(b, _sl), "stop"); return; }
                    if (hitTp) { Cerrar(b, _tp, "objetivo"); return; }
                    if (beAct)
                    {
                        op.BeArmado = true; op.UtcBe = b.Utc;
                        if (hitEnt) { Cerrar(b, L, "be_misma_vela"); return; }
                        _be = true; op.StopVigente = L;
                        _n.Eventos.Add(new Evento { Tipo = TipoEvento.BeArmado, Regla = 2, Sesion = _s, Op = op, Utc = b.Utc });
                    }
                    if (ultima) { Cerrar(b, b.C, "tiempo"); return; }
                    return;
                }
                if (hitEnt) { double p = PxStop(b, L); Cerrar(b, p, p == L ? "be" : "be_hueco"); return; }
                if (hitTp) { Cerrar(b, _tp, "objetivo"); return; }
                if (ultima) { Cerrar(b, b.C, "tiempo"); return; }
            }

            private void Cerrar(in Barra b, double px, string motivo)
            {
                var op = _pos;
                op.Salida = px; op.Motivo = motivo; op.UtcSal = b.Utc; op.SodSal = b.Sod; op.Abierta = false;
                op.Bruto = op.Lado * (px - op.Entrada);
                _pos = null;
                _n.Eventos.Add(new Evento { Tipo = TipoEvento.Salida, Regla = 2, Sesion = _s, Op = op, Utc = b.Utc });
                // reingreso unico: salida exacta en cero por BE y queda al menos una barra en la ventana
                if (_p.Reingreso && _reingresos == 0 && op.Bruto == 0.0 && motivo.StartsWith("be") && b.Sod + b.Paso <= _p.HFinOrdenes - b.Paso)
                    _reingresos++;
                else
                    _terminado = true;
            }
        }
    }
}
