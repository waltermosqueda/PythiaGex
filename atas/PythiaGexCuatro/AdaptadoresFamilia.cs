// AdaptadoresFamilia.cs — PythiaGex 4.1, constructor B1 (08-10-2026).
//
// ICinta e ILibroNq del contrato (_contratos/Contratos.cs) sobre el clon de la 3.0, mas utilidades comunes (redondeo identico al de Python,
// sesion de CME, contrato del grafico). SIN referencias a ATAS: el arnes atas/_test_cuatro_paridad/adaptadores compila este archivo +
// Contratos.cs y lo prueba contra la vista previa (Python) sin abrir ATAS.
//
// Quien alimenta (pegamento con ATAS, en el clon, marcas GANCHO-4.1):
//   - CintaFamilia.Tick          <- GammaHoyTresCinta.CintaEvento (cada evento que iria al CSV de la cinta: el mismo (t, precio)), hilo de ATAS, O(1)
//   - CintaFamilia.Relleno       <- GammaHoyTresCinta.CintaRellenoEscribir (si el relleno de la 3.0 esta prendido), en su Task
//   - CintaFamilia.VelasGrafico  <- GammaHoyTres.AlimentarVelasGraficoFamilia (velas CERRADAS del grafico, desde el temporizador de 5 s)
//   - LibroNqFamilia.AgregarLinea <- Viva3.Guardar (la linea viva3 del minuto, el mismo texto que el archivo PythiaGex4\viva)
//
// Lo que guardan (todo en %APPDATA%\ATAS\PythiaGex4, nada pisa a la 3.0):
//   - cinta\seg-<instrumento>-<yyyy-MM-dd UTC>.bin : precio por segundo (el ultimo tick de cada segundo), velas m2 de ticks, tramos de escucha
//     y sesiones. Se escribe cada 5 min (atomico) y al soltar la ultima instancia. ~1-2 MB por dia; se podan a los 9 dias.
//   - el ILibroNq NO escribe nada: se siembra de PythiaGex4\viva\viva3-<raiz>-<dia>.jsonl (lo escribe el clon) y sigue en memoria.
//
// Fidelidad con la vista previa (preview_niveles.py / backtest_familia.Precio / velas.cierre_conocido / tq_comun.precio_en):
//   - Velas m2 = velas_de_ticks: o = primer evento (en orden de llegada) entre los de t minimo; c = ultimo entre los de t maximo; cerrada si
//     b + 120 s <= ultimo tick de SU sesion. CierreConocido usa SOLO las velas de la sesion de t (como self.vs), con el tope de 4 h.
//   - Precio(ts): ultimo tick <= ts si ts - t <= 120 s y ts <= ultimo tick; si no, la vela m2 cerrada que contiene ts, interpolada.
//     El almacen es POR SEGUNDO (casilla ceil(t/1000), gana el t mayor; empate: el ultimo en llegar): EXACTO para ts en segundos enteros,
//     que es lo que piden C7/C8/TQQQ (sello de CBOE - 900 s; fin de vela - 1 s). Con ts fraccionario puede devolver un tick de hasta 1 s antes.
//   - Lo que la vista previa NO tiene: los TRAMOS DE ESCUCHA. Si el indicador no estaba corriendo (arranco a mitad de sesion, o entre un
//     Parar y el siguiente Arrancar), una vela m2 de ticks puede estar incompleta: ahi manda la vela del grafico (respaldo) y un tick solo
//     vale si se escucho de corrido desde el tick hasta ts. Un hueco de ticks MIENTRAS corre (mercado quieto o corte de datos) no corta el
//     tramo, igual que la vista previa sobre la cinta CSV. Con el indicador corriendo toda la sesion es identico a la vista previa.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace PythiaGexCuatro.Familia
{
    /// <summary>Utilidades comunes de los adaptadores (sin ATAS).</summary>
    public static class AdaptadoresFamilia
    {
        /// <summary>%APPDATA%\ATAS\PythiaGex4 (el arnes la cambia a una carpeta temporal).</summary>
        public static string Carpeta { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex4");

        /// <summary>Log de los adaptadores. Default: %APPDATA%\ATAS\pythiagex4-adaptadores.log. El integrador puede redirigirlo a Registro.</summary>
        public static Action<string> Log { get; set; } = LogPorDefecto;

        /// <summary>false (default, "corregido"): la sesion de CME en hora de Nueva York, 18:00 de la vispera a 17:00. true: la de la vista previa,
        /// 22:00 UTC de la vispera a 21:00 UTC fijo. En horario de verano de EE.UU. son identicas; desde el 01-11 la fija se corre una hora.</summary>
        public static bool SesionUtcFija { get; set; }

        /// <summary>GANCHO-4.1 familia (TRES_*): (raiz o capa "NQ"|"NDX"|"QQQ", la linea exacta de la estela). Lo invoca GuardarEstela del clon.</summary>
        public static Action<string, string> AlEstela { get; set; }

        internal static readonly long EPOCA = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        /// <summary>ms UTC desde 1970 con los ticks CRUDOS (Kind Unspecified = UTC, como da ATAS; nunca ToUniversalTime).</summary>
        public static long Ms(DateTime t) => (t.Ticks - EPOCA) / TimeSpan.TicksPerMillisecond;
        public static DateTime DeMs(long ms) => new DateTime(EPOCA + ms * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
        public static long PisoDiv(long a, long b) { long q = a / b; if (a % b != 0 && ((a < 0) != (b < 0))) q--; return q; }
        public static long TechoDiv(long a, long b) => -PisoDiv(-a, b);

        // ------------------------------------------------------------------ redondeo identico a round(x, n) de Python

        private static readonly double[] _pot10 = { 1, 10, 100, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15 };

        /// <summary>round(x, nd) de Python: el decimal mas cercano al valor BINARIO exacto de x (empate exacto: al par), devuelto como el double mas
        /// cercano a ese decimal. Math.Round(x, nd) de .NET multiplica en double y puede caer del otro lado en los "medios" (0,12345 -> 0,1234 en
        /// .NET y 0,1235 en Python: el binario de 0,12345 esta un pelo arriba). Los dias de viva3 traen 5 decimales: 1 de cada 10 es un medio.</summary>
        public static double RedondeoPy(double x, int nd)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || x == 0) return x;
            if (nd < 0 || nd > 15) throw new ArgumentOutOfRangeException(nameof(nd));
            double p = _pot10[nd];
            double y = x * p;
            if (Math.Abs(y) >= 1e15) return x;                       // ya no tiene decimales que redondear a esta escala
            double fl = Math.Floor(y), frac = y - fl;
            if (Math.Abs(frac - 0.5) > 1e-6) return (frac < 0.5 ? fl : fl + 1) / p;
            // cerca de un medio: comparacion EXACTA con aritmetica entera (x = man * 2^exp)
            long bits = BitConverter.DoubleToInt64Bits(x);
            bool neg = bits < 0;
            int exp = (int)((bits >> 52) & 0x7FF);
            long man = bits & 0xFFFFFFFFFFFFFL;
            if (exp == 0) exp = 1; else man |= 1L << 52;
            exp -= 1075;
            BigInteger n = new BigInteger(man) * BigInteger.Pow(10, nd);
            BigInteger q;
            if (exp >= 0) q = n << exp;
            else
            {
                int s = -exp;
                q = n >> s;
                BigInteger rem = n - (q << s);
                int cmp = (rem << 1).CompareTo(BigInteger.One << s);
                if (cmp > 0 || (cmp == 0 && !q.IsEven)) q += 1;
            }
            double r = (double)q / p;
            return neg ? -r : r;
        }

        // ------------------------------------------------------------------ contrato y sesion

        private static readonly Regex _reContrato = new Regex(@"([FGHJKMNQUVXZ])(\d{1,2})$", RegexOptions.CultureInvariant);

        /// <summary>"MNQZ6" -> "Z6"; "NQZ26" -> "Z26"; "" si el codigo no termina en mes + año.</summary>
        public static string ContratoDeCodigo(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return "";
            var m = _reContrato.Match(codigo.Trim().ToUpperInvariant());
            return m.Success ? m.Groups[1].Value + m.Groups[2].Value : "";
        }

        private static TimeZoneInfo _ny; private static bool _nyBuscada;
        internal static TimeZoneInfo ZonaNy()
        {
            if (_nyBuscada) return _ny;
            try { _ny = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
            catch { try { _ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); } catch { _ny = null; } }
            _nyBuscada = true;
            if (_ny == null) Log?.Invoke("adaptadores: sin zona horaria de Nueva York instalada: uso UTC-4 fijo (la sesion se corre en invierno)");
            return _ny;
        }

        internal static DateTime NyAUtc(DateTime ny)
        {
            var z = ZonaNy();
            if (z == null) return DateTime.SpecifyKind(ny.AddHours(4), DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(ny, DateTimeKind.Unspecified), z);
        }

        internal static DateTime UtcANy(DateTime utc)
        {
            var z = ZonaNy();
            if (z == null) return utc.AddHours(-4);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), z);
        }

        private sealed class SesionCache { public long Ini, Fin; public string Dia; public bool Fija; }
        private static SesionCache _sesCache;

        /// <summary>La sesion de CME que contiene tMs: [ini, fin) en ms UTC y su etiqueta yyyy-MM-dd (la fecha del dia en que CIERRA).
        /// Corregida (default): 18:00 NY de la vispera a 17:00 NY. Fija (SesionUtcFija): dia = fecha(t + 2 h), [dia - 2 h, dia + 21 h) UTC.
        /// En la pausa (fin <= t < ini de la siguiente) devuelve la sesion que acaba de cerrar.</summary>
        public static (long Ini, long Fin, string Dia) Sesion(long tMs)
        {
            var c = _sesCache;
            bool fija = SesionUtcFija;
            if (c != null && c.Fija == fija && tMs >= c.Ini && tMs < c.Fin) return (c.Ini, c.Fin, c.Dia);
            long ini, fin; DateTime dia;
            if (fija)
            {
                dia = DeMs(tMs).AddHours(2).Date;
                ini = Ms(dia.AddHours(-2)); fin = Ms(dia.AddHours(21));
            }
            else
            {
                var ny = UtcANy(DeMs(tMs));
                dia = ny.AddHours(6).Date;
                ini = Ms(NyAUtc(dia.AddDays(-1).AddHours(18)));
                fin = Ms(NyAUtc(dia.AddHours(17)));
            }
            string etq = dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (tMs >= ini && tMs < fin) _sesCache = new SesionCache { Ini = ini, Fin = fin, Dia = etq, Fija = fija };
            return (ini, fin, etq);
        }

        // ------------------------------------------------------------------ log

        private static readonly object _logLlave = new object();
        private static readonly Dictionary<string, DateTime> _logUltimo = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        private static void LogPorDefecto(string msg)
        {
            try
            {
                var ruta = Path.Combine(Path.GetDirectoryName(Carpeta.TrimEnd('\\', '/')) ?? Carpeta, "pythiagex4-adaptadores.log");
                lock (_logLlave) File.AppendAllText(ruta, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + msg + "\n", new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>Una linea por clave y por minuto como maximo (para errores que se repiten).</summary>
        internal static void LogLimitado(string clave, string msg)
        {
            var ahora = DateTime.UtcNow;
            lock (_logUltimo)
            {
                if (_logUltimo.TryGetValue(clave, out var u) && (ahora - u).TotalSeconds < 60) return;
                _logUltimo[clave] = ahora;
            }
            try { Log?.Invoke(msg); } catch { }
        }

        internal static void Excepcion(string donde, Exception e)
        {
            if (e == null) return;
            var pila = (e.StackTrace ?? "").Replace("\r", " ").Replace("\n", " ");
            if (pila.Length > 400) pila = pila.Substring(0, 400);
            LogLimitado("exc|" + donde, "adaptadores: EXCEPCION en " + donde + ": " + e.GetType().Name + ": " + e.Message + " | " + pila);
        }
    }

    // ======================================================================================================================================
    // ICinta: la cinta del MNQ del grafico
    // ======================================================================================================================================

    /// <summary>
    /// La cinta del MNQ en memoria para la Familia (ICinta). Una por instrumento en todo el proceso (CintaFamilia.Para): dos graficos del mismo
    /// MNQZ6 la alimentan los dos (es idempotente: casilla por segundo con "gana el t mayor", velas con o/c por t). Hilo propio BelowNormal
    /// ("PythiaGex4 cinta-familia") que carga la historia (8 dias) y persiste cada 5 min; el hilo de ATAS solo toca memoria con una llave corta.
    /// </summary>
    public sealed class CintaFamilia : ICinta
    {
        public const long VELA_MS = 120000;
        public const long EDAD_TICK_MS = 120000;           // backtest_familia.Precio: el tick vale 120 s
        public const long TOPE_CIERRE_MS = 4 * 3600000L;   // velas.cierre_conocido: mas de 4 h = hueco
        public const long HUECO_VIVO_MS = 120000;          // sin ticks mas de 2 min con la sesion abierta: se cuenta (log) pero NO corta el tramo (ver Tick)
        public const int DIAS_HISTORIA = 8;                // dias UTC que se cargan y se guardan (la vista previa usa 6 de calendario previos)
        public const int PERSISTIR_CADA_S = 300;

        // ---------------------------------------------------------------- registro por instrumento
        private static readonly object _regLlave = new object();
        private static readonly Dictionary<string, CintaFamilia> _registro = new Dictionary<string, CintaFamilia>(StringComparer.OrdinalIgnoreCase);

        /// <summary>La cinta de ese instrumento (MNQZ6). Se crea la primera vez; vive todo el proceso (con 0 dueños no tiene hilo).</summary>
        public static CintaFamilia Para(string instrumento, string raiz = "NQ")
        {
            if (string.IsNullOrWhiteSpace(instrumento)) throw new ArgumentException("instrumento vacio");
            lock (_regLlave)
            {
                if (!_registro.TryGetValue(instrumento.Trim(), out var c)) { c = new CintaFamilia(instrumento.Trim(), raiz); _registro[c.Instrumento] = c; }
                return c;
            }
        }

        public string Instrumento { get; }
        public string Raiz { get; }
        private readonly HashSet<object> _duenos = new HashSet<object>(ReferenceEqualityComparer.Instance);

        /// <summary>Para el arnes: una cinta suelta (sin registro, sin hilo, sin disco salvo que se pida).</summary>
        public CintaFamilia(string instrumento, string raiz = "NQ") { Instrumento = instrumento; Raiz = raiz ?? "NQ"; }

        // ---------------------------------------------------------------- datos (todo bajo _llave)
        private readonly object _llave = new object();

        private sealed class DiaSeg
        {
            public readonly long Num;                        // dia UTC = floor(segundo / 86400)
            public readonly long[] T = new long[86400];      // ms del ultimo tick de ese segundo (0 = vacio)
            public readonly double[] P = new double[86400];
            public int N; public bool Sucio;
            public DiaSeg(long n) { Num = n; }
        }
        private readonly Dictionary<long, DiaSeg> _dias = new Dictionary<long, DiaSeg>();
        private DiaSeg _diaCache;

        private sealed class Vela { public long B; public double O, H, L, C; public long TPri = long.MaxValue, TUlt = long.MinValue; public int N; }
        private readonly Dictionary<long, Vela> _velas = new Dictionary<long, Vela>();          // de ticks
        private Vela _velaCache;
        private readonly Dictionary<long, Vela> _velasGraf = new Dictionary<long, Vela>();      // del grafico (respaldo)
        private readonly HashSet<long> _grafInvalidas = new HashSet<long>();                    // baldes que una vela del grafico cruza
        private long _grafMaxAp = long.MinValue;

        private sealed class Ses { public string Dia; public long Ini, Fin; public long UltTick = long.MinValue; public long PrimerVivo = long.MaxValue; public bool Sucia; }
        private readonly Dictionary<string, Ses> _ses = new Dictionary<string, Ses>(StringComparer.Ordinal);
        private Ses _sesCache;

        // tramos en los que se escucho la cinta entera: [Desde, Hasta] en ms. El abierto es [_vivoDesde, _vivoUlt].
        private readonly List<(long Desde, long Hasta)> _tramos = new List<(long, long)>();
        private long _vivoDesde = long.MinValue, _vivoUlt = long.MinValue;
        private long _registroMs = long.MaxValue;            // reloj (ms) en que esta corrida empezo a escuchar (Registrar)

        private long _ultimoTick = long.MinValue;
        private long _version, _nVivo, _nRelleno, _nRellenoFuera, _nGraf, _nHuecos;
        private int _diasCargados, _csvImportados;
        private readonly HashSet<long> _diasLeidos = new HashSet<long>();   // dias cuyo archivo ya se fundio (o que no tenian): recien ahi se reescribe
        private DateTime _ultimaPersistencia = DateTime.MinValue;
        private volatile bool _cargado;

        public long Version { get { lock (_llave) return _version; } }

        // ---------------------------------------------------------------- entrada: ticks vivos (hilo de ATAS, O(1))

        /// <summary>Un evento de la cinta en vivo (el mismo (t, precio) de la linea del CSV de la 3.0). O(1), llave corta, sin I/O. Nunca tira.</summary>
        public void Tick(long tMs, double px)
        {
            if (tMs <= 0 || !(px > 0) || double.IsInfinity(px)) return;
            try
            {
                lock (_llave)
                {
                    var ses = SesionDe(tMs);
                    if (_vivoDesde == long.MinValue)
                    {
                        // el tramo arranca en el primer tick o, si llego despues, cuando esta corrida empezo a escuchar (+2 s de latencia)
                        _vivoDesde = _registroMs == long.MaxValue ? tMs : Math.Min(tMs, _registroMs + 2000);
                        _vivoUlt = tMs;
                    }
                    else
                    {
                        // Mientras el indicador corre, un hueco de mas de 2 min dentro de la sesion se CUENTA pero NO corta el tramo: puede ser
                        // mercado quieto o un corte de datos, y la vista previa (que lee la cinta CSV de la 3.0, con los mismos huecos) no los
                        // distingue: el ultimo tick vale 120 s igual. Medido 08-10: 2 huecos asi; cortarlos daba 7 precios distintos de Python.
                        // Lo que SI corta el tramo es que el indicador no estuviera corriendo (Parar / Arrancar): eso se sabe exacto.
                        if (tMs - _vivoUlt > HUECO_VIVO_MS && ReferenceEquals(SesionDe(_vivoUlt), ses)) _nHuecos++;
                        if (tMs > _vivoUlt) _vivoUlt = tMs;
                        ses = SesionDe(tMs);
                    }
                    if (tMs < ses.PrimerVivo) { ses.PrimerVivo = tMs; ses.Sucia = true; }
                    Poner(tMs, px, ses);
                    _nVivo++;
                }
            }
            catch (Exception e) { AdaptadoresFamilia.Excepcion("CintaFamilia.Tick", e); }
        }

        /// <summary>El relleno de la sesion (RequestForCumulativeTrades de la 3.0): entra solo lo que es ANTERIOR al primer tick vivo de su sesion
        /// (como preview_niveles: rt &lt; vt.min()). Declara escuchado [desde, min(hasta, primer vivo)]. Devuelve cuantos entraron.</summary>
        public int Relleno(IReadOnlyList<(long T, double P)> ticks, long desdeMs, long hastaMs)
        {
            if (ticks == null) return 0;
            int n = 0;
            try
            {
                lock (_llave)
                {
                    long minT = long.MaxValue, primerVivoDesde = long.MaxValue;
                    foreach (var (t, p) in ticks)
                    {
                        if (t <= 0 || !(p > 0)) continue;
                        var ses = SesionDe(t);
                        if (t >= ses.PrimerVivo) { _nRellenoFuera++; continue; }
                        Poner(t, p, ses);
                        n++; _nRelleno++;
                        if (t < minT) minT = t;
                    }
                    // tramo escuchado del relleno: desde lo pedido hasta el primer tick vivo de esa sesion (o hasta lo pedido)
                    if (hastaMs > desdeMs && desdeMs > 0)
                    {
                        var sesD = SesionDe(desdeMs);
                        primerVivoDesde = sesD.PrimerVivo;
                        long h = Math.Min(hastaMs, primerVivoDesde == long.MaxValue ? hastaMs : primerVivoDesde);
                        if (h > desdeMs) _tramos.Add((desdeMs, h));
                        NormalizarTramos();
                    }
                }
            }
            catch (Exception e) { AdaptadoresFamilia.Excepcion("CintaFamilia.Relleno", e); }
            return n;
        }

        /// <summary>Velas CERRADAS del grafico (apertura y ultimo trade en ms UTC crudos, o, h, l, c), en orden. Se agregan en baldes m2 y se
        /// usan SOLO donde la cinta no se escucho entera. Una vela que cruza un balde (marco de mas de 2 min, o una de rango/ticks larga) no sirve
        /// e invalida los baldes que toca. Devuelve cuantas se rechazaron.</summary>
        public int VelasGrafico(IReadOnlyList<(long Ap, long Ult, double O, double H, double L, double C)> velas)
        {
            if (velas == null) return 0;
            int rech = 0;
            try
            {
                lock (_llave)
                {
                    foreach (var v in velas)
                    {
                        if (v.Ap <= 0 || !(v.O > 0) || !(v.C > 0) || !(v.H > 0) || !(v.L > 0)) continue;
                        long ult = v.Ult >= v.Ap ? v.Ult : v.Ap;
                        long b = AdaptadoresFamilia.PisoDiv(v.Ap, VELA_MS) * VELA_MS, bu = AdaptadoresFamilia.PisoDiv(ult, VELA_MS) * VELA_MS;
                        if (v.Ap > _grafMaxAp) _grafMaxAp = v.Ap;
                        if (bu != b)
                        {
                            rech++;
                            for (long x = b; x <= bu && x - b <= 400 * VELA_MS; x += VELA_MS) { _grafInvalidas.Add(x); _velasGraf.Remove(x); }
                            continue;
                        }
                        if (_grafInvalidas.Contains(b)) continue;
                        if (!_velasGraf.TryGetValue(b, out var g)) { g = new Vela { B = b, O = v.O, H = v.H, L = v.L, C = v.C, TPri = v.Ap, TUlt = v.Ap, N = 1 }; _velasGraf[b] = g; _nGraf++; continue; }
                        if (v.Ap < g.TPri) { g.TPri = v.Ap; g.O = v.O; }
                        if (v.Ap >= g.TUlt) { g.TUlt = v.Ap; g.C = v.C; }
                        if (v.H > g.H) g.H = v.H;
                        if (v.L < g.L) g.L = v.L;
                        g.N++;
                    }
                    _version++;
                }
            }
            catch (Exception e) { AdaptadoresFamilia.Excepcion("CintaFamilia.VelasGrafico", e); }
            return rech;
        }

        // ---------------------------------------------------------------- ICinta (lecturas; cualquier hilo)

        public double Precio(DateTime tUtc)
        {
            long ts = AdaptadoresFamilia.Ms(tUtc);
            lock (_llave)
            {
                if (_ultimoTick != long.MinValue && ts <= _ultimoTick)
                {
                    var (t, p, ok) = UltimoTickHasta(ts, EDAD_TICK_MS);
                    if (ok && ts - t <= EDAD_TICK_MS && Escuchado(t, ts)) return p;
                }
                long b = AdaptadoresFamilia.PisoDiv(ts, VELA_MS) * VELA_MS;
                var (v, cerrada) = Elegir(b);
                if (v != null && cerrada) return v.O + (v.C - v.O) * (ts - b) / (double)VELA_MS;
                return double.NaN;
            }
        }

        public double PrecioSoloTick(DateTime tUtc, int maxEdadS = 120)
        {
            long ts = AdaptadoresFamilia.Ms(tUtc), max = Math.Max(0, maxEdadS) * 1000L;
            lock (_llave)
            {
                if (_ultimoTick == long.MinValue || ts > _ultimoTick) return double.NaN;
                var (t, p, ok) = UltimoTickHasta(ts, max);
                return ok && ts - t <= max && Escuchado(t, ts) ? p : double.NaN;
            }
        }

        public double CierreConocido(DateTime tUtc)
        {
            long ts = AdaptadoresFamilia.Ms(tUtc);
            lock (_llave)
            {
                var (ini, _, _) = AdaptadoresFamilia.Sesion(ts);
                for (long b = AdaptadoresFamilia.PisoDiv(ts - VELA_MS, VELA_MS) * VELA_MS; b >= ini; b -= VELA_MS)
                {
                    if (ts - (b + VELA_MS) > TOPE_CIERRE_MS) return double.NaN;
                    var (v, cerrada) = Elegir(b);
                    if (v != null && cerrada) return v.C;
                }
                return double.NaN;
            }
        }

        public IReadOnlyList<(long Ms, double O, double H, double L, double C)> VelasM2(DateTime desdeUtc, DateTime hastaUtc)
        {
            long d = AdaptadoresFamilia.Ms(desdeUtc), h = AdaptadoresFamilia.Ms(hastaUtc);
            var r = new List<(long, double, double, double, double)>();
            lock (_llave)
            {
                var claves = new SortedSet<long>();
                foreach (var b in _velas.Keys) if (b >= d && b < h) claves.Add(b);
                foreach (var b in _velasGraf.Keys) if (b >= d && b < h) claves.Add(b);
                foreach (var b in claves)
                {
                    var (v, cerrada) = Elegir(b);
                    if (v != null && cerrada) r.Add((b, v.O, v.H, v.L, v.C));
                }
            }
            return r;
        }

        public DateTime UltimoTickUtc { get { lock (_llave) return _ultimoTick == long.MinValue ? DateTime.MinValue : AdaptadoresFamilia.DeMs(_ultimoTick); } }

        /// <summary>Texto corto para el log y la pestaña.</summary>
        public string Estado
        {
            get
            {
                lock (_llave)
                {
                    var inv = CultureInfo.InvariantCulture;
                    string edad = _ultimoTick == long.MinValue ? "sin ticks" : "ultimo tick hace " + ((AdaptadoresFamilia.Ms(DateTime.UtcNow) - _ultimoTick) / 1000.0).ToString("0", inv) + " s";
                    return "cinta " + Instrumento + ": " + edad + ", vivos " + _nVivo + ", relleno " + _nRelleno + (_nRellenoFuera > 0 ? " (+" + _nRellenoFuera + " ya en vivo)" : "")
                        + ", velas ticks " + _velas.Count + " / grafico " + _velasGraf.Count + ", tramos " + (_tramos.Count + (_vivoDesde != long.MinValue ? 1 : 0))
                        + (_nHuecos > 0 ? ", huecos de mas de 2 min sin ticks " + _nHuecos : "") + ", dias " + _dias.Count + (_cargado ? " (historia: " + _diasCargados + " archivos" + (_csvImportados > 0 ? ", " + _csvImportados + " csv" : "") + ")" : _hilo != null ? " (cargando historia)" : " (sin hilo)");
                }
            }
        }

        // ---------------------------------------------------------------- internos (bajo _llave)

        private Ses SesionDe(long tMs)
        {
            var c = _sesCache;
            if (c != null && tMs >= c.Ini && tMs < c.Fin) return c;
            var (ini, fin, dia) = AdaptadoresFamilia.Sesion(tMs);
            if (!_ses.TryGetValue(dia, out var s)) { s = new Ses { Dia = dia, Ini = ini, Fin = fin }; _ses[dia] = s; }
            if (tMs >= s.Ini && tMs < s.Fin) _sesCache = s;
            return s;
        }

        private DiaSeg ObtenerDia(long num)
        {
            var d = _diaCache;
            if (d != null && d.Num == num) return d;
            if (!_dias.TryGetValue(num, out d)) { d = new DiaSeg(num); _dias[num] = d; }
            _diaCache = d;
            return d;
        }

        private void Poner(long tMs, double px, Ses ses)
        {
            long s = AdaptadoresFamilia.TechoDiv(tMs, 1000);
            long dn = AdaptadoresFamilia.PisoDiv(s, 86400);
            int i = (int)(s - dn * 86400);
            var d = ObtenerDia(dn);
            long tv = d.T[i];
            if (tv == 0) { d.N++; d.T[i] = tMs; d.P[i] = px; }
            else if (tMs >= tv) { d.T[i] = tMs; d.P[i] = px; }
            d.Sucio = true;

            long b = AdaptadoresFamilia.PisoDiv(tMs, VELA_MS) * VELA_MS;
            var v = _velaCache;
            if (v == null || v.B != b)
            {
                if (!_velas.TryGetValue(b, out v)) { v = new Vela { B = b }; _velas[b] = v; }
                _velaCache = v;
            }
            if (v.N == 0) { v.O = v.H = v.L = v.C = px; v.TPri = v.TUlt = tMs; }
            else
            {
                if (tMs < v.TPri) { v.TPri = tMs; v.O = px; }
                if (tMs >= v.TUlt) { v.TUlt = tMs; v.C = px; }
                if (px > v.H) v.H = px;
                if (px < v.L) v.L = px;
            }
            v.N++;
            if (tMs > ses.UltTick) { ses.UltTick = tMs; ses.Sucia = true; }
            if (tMs > _ultimoTick) _ultimoTick = tMs;
            _version++;
        }

        /// <summary>El ultimo tick con t &lt;= ts y t &gt;= ts - maxEdad, recorriendo las casillas por segundo hacia atras.</summary>
        private (long T, double P, bool Ok) UltimoTickHasta(long ts, long maxEdad)
        {
            long s = AdaptadoresFamilia.TechoDiv(ts, 1000), sMin = AdaptadoresFamilia.TechoDiv(ts - maxEdad, 1000);
            DiaSeg d = null;
            for (long k = s; k >= sMin; k--)
            {
                long dn = AdaptadoresFamilia.PisoDiv(k, 86400);
                if (d == null || d.Num != dn) { if (!_dias.TryGetValue(dn, out d)) { d = null; k = dn * 86400; continue; } }
                int i = (int)(k - dn * 86400);
                long t = d.T[i];
                if (t == 0 || t > ts) continue;          // t > ts solo en la casilla de ts fraccionario
                return (t, d.P[i], true);
            }
            return (0, double.NaN, false);
        }

        /// <summary>Se escucho la cinta entera de corrido en [desde, hasta]? (un tramo cerrado, el abierto, o un cerrado que empalma con el
        /// abierto: el relleno termina justo donde empieza el vivo). Los cerrados estan normalizados (ordenados, sin solaparse).</summary>
        private bool Escuchado(long desde, long hasta)
        {
            bool vivo = _vivoDesde != long.MinValue;
            if (vivo && _vivoDesde <= desde && _vivoUlt >= hasta) return true;
            for (int i = 0; i < _tramos.Count; i++)
            {
                var t = _tramos[i];
                if (t.Desde > desde) break;
                if (t.Hasta >= hasta) return true;
                if (vivo && t.Hasta >= _vivoDesde && _vivoUlt >= hasta) return true;
            }
            return false;
        }

        /// <summary>La vela del balde b y si esta cerrada. Escuchada entera -> la de ticks (o ninguna: no hubo operaciones). Si no -> la del
        /// grafico, y si no hay, la de ticks aunque este incompleta (ultimo recurso).</summary>
        private (Vela V, bool Cerrada) Elegir(long b)
        {
            _velas.TryGetValue(b, out var vt);
            bool entera = Escuchado(b, b + VELA_MS);
            if (entera || (vt != null && !_velasGraf.ContainsKey(b)))
            {
                if (vt == null) return (null, false);
                var s = SesionDe(b);
                return (vt, b + VELA_MS <= s.UltTick);
            }
            if (_velasGraf.TryGetValue(b, out var vg) && !_grafInvalidas.Contains(b))
                return (vg, b + VELA_MS <= Math.Max(_grafMaxAp, _ultimoTick));
            if (vt != null) { var s = SesionDe(b); return (vt, b + VELA_MS <= s.UltTick); }
            return (null, false);
        }

        private void NormalizarTramos()
        {
            if (_tramos.Count < 2) return;
            _tramos.Sort((a, c) => a.Desde.CompareTo(c.Desde));
            var r = new List<(long, long)>(_tramos.Count);
            var cur = _tramos[0];
            for (int i = 1; i < _tramos.Count; i++)
            {
                var x = _tramos[i];
                if (x.Desde <= cur.Hasta) { if (x.Hasta > cur.Hasta) cur.Hasta = x.Hasta; }
                else { r.Add(cur); cur = x; }
            }
            r.Add(cur);
            _tramos.Clear(); _tramos.AddRange(r);
        }

        // ---------------------------------------------------------------- vida: dueños, hilo, disco

        private readonly object _vidaLlave = new object();
        private Thread _hilo;
        private readonly ManualResetEventSlim _parar = new ManualResetEventSlim(false);

        /// <summary>Una instancia del indicador empieza a usar esta cinta. La primera arranca el hilo (carga la historia y persiste).</summary>
        public void Registrar(object dueno)
        {
            bool primero;
            lock (_regLlave) { primero = _duenos.Count == 0; _duenos.Add(dueno); }
            if (primero) Arrancar();
        }

        /// <summary>Una instancia la suelta. La ultima cierra el tramo escuchado, para el hilo y persiste (sincronico, en el hilo que llama).</summary>
        public void Soltar(object dueno)
        {
            bool ultimo;
            lock (_regLlave) { if (!_duenos.Remove(dueno)) return; ultimo = _duenos.Count == 0; }
            if (ultimo) Parar();
        }

        /// <summary>Arranca la escucha (tramo nuevo) y el hilo de carga/persistencia. Idempotente.</summary>
        public void Arrancar()
        {
            lock (_vidaLlave)
            {
                lock (_llave) { _registroMs = AdaptadoresFamilia.Ms(DateTime.UtcNow); }
                if (_hilo != null && _hilo.IsAlive) return;
                _parar.Reset();
                _hilo = new Thread(Bucle) { IsBackground = true, Name = "PythiaGex4 cinta-familia", Priority = ThreadPriority.BelowNormal };
                _hilo.Start();
            }
        }

        /// <summary>Corta la escucha (el tramo abierto se cierra), para el hilo (espera 2 s) y persiste lo sucio.</summary>
        public void Parar()
        {
            lock (_vidaLlave)
            {
                _parar.Set();
                try { if (_hilo != null && !_hilo.Join(2000)) AdaptadoresFamilia.Log?.Invoke("adaptadores: " + Instrumento + ": el hilo de la cinta no termino en 2 s"); } catch { }
                _hilo = null;
                CerrarEscucha(AdaptadoresFamilia.Ms(DateTime.UtcNow));
                try { Persistir(); } catch (Exception e) { AdaptadoresFamilia.Excepcion("CintaFamilia.Parar.Persistir", e); }
            }
        }

        /// <summary>Cierra el tramo vivo: se escucho hasta ahora (o hasta 2 min despues del ultimo tick, si la cinta se habia cortado).</summary>
        private void CerrarEscucha(long ahoraMs)
        {
            lock (_llave)
            {
                if (_vivoDesde != long.MinValue)
                {
                    long h = Math.Max(_vivoUlt, Math.Min(ahoraMs, _vivoUlt + HUECO_VIVO_MS));
                    _tramos.Add((_vivoDesde, h));
                    NormalizarTramos();
                    // los tramos van en el archivo de cada dia que tocan (+-1): esos se reescriben
                    long d0 = AdaptadoresFamilia.PisoDiv(_vivoDesde, 86400000L) - 1, d1 = AdaptadoresFamilia.PisoDiv(h, 86400000L) + 1;
                    foreach (var d in _dias.Values) if (d.Num >= d0 && d.Num <= d1) d.Sucio = true;
                }
                _vivoDesde = long.MinValue; _vivoUlt = long.MinValue; _registroMs = long.MaxValue;
            }
        }

        private void Bucle()
        {
            try
            {
                if (!_cargado) { CargarHistoria(); _cargado = true; AdaptadoresFamilia.Log?.Invoke("adaptadores: " + Estado); }
                while (!_parar.Wait(30000))
                {
                    try
                    {
                        if ((DateTime.UtcNow - _ultimaPersistencia).TotalSeconds >= PERSISTIR_CADA_S) Persistir();
                        Podar();
                    }
                    catch (Exception e) { AdaptadoresFamilia.Excepcion("CintaFamilia.Bucle", e); }
                }
            }
            catch (Exception e) { AdaptadoresFamilia.Excepcion("CintaFamilia.Bucle.fuera", e); _cargado = true; }
        }

        private string CarpetaCinta => Path.Combine(AdaptadoresFamilia.Carpeta, "cinta");
        private string RutaDia(long num) => Path.Combine(CarpetaCinta, "seg-" + Seguro(Instrumento) + "-" + AdaptadoresFamilia.DeMs(num * 86400000L).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".bin");
        private static string Seguro(string s) { var sb = new StringBuilder(); foreach (var ch in s) sb.Append(char.IsLetterOrDigit(ch) ? ch : '_'); return sb.ToString(); }

        private const string MAGIA = "PG4SEG1";

        /// <summary>Escribe cada dia sucio (atomico: .tmp y reemplazo). La copia se toma bajo la llave; el disco va afuera.</summary>
        public void Persistir()
        {
            // un dia que todavia no se leyo de disco (la carga no llego, o fallo) se funde ANTES de reescribirlo: nunca se pisa historia
            var porLeer = new List<long>();
            lock (_llave) foreach (var d in _dias.Values) if (d.Sucio && !_diasLeidos.Contains(d.Num)) porLeer.Add(d.Num);
            foreach (var num in porLeer)
            {
                var ruta = RutaDia(num);
                bool ok = !File.Exists(ruta) || CargarArchivoDia(ruta);
                if (!ok)
                {
                    // roto o ilegible: se aparta (no se borra) y se sigue con lo de memoria
                    try { File.Move(ruta, ruta + ".roto-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)); ok = true; AdaptadoresFamilia.Log?.Invoke("adaptadores: " + Path.GetFileName(ruta) + " no se pudo leer: apartado como .roto"); }
                    catch (Exception e) { AdaptadoresFamilia.Excepcion("CintaFamilia.Persistir.apartar", e); }
                }
                if (ok) lock (_llave) _diasLeidos.Add(num);
            }
            var tareas = new List<(string Ruta, byte[] Datos)>();
            var fotos = new List<FotoDiaSeg>();
            lock (_llave)
            {
                foreach (var d in _dias.Values)
                {
                    if (!d.Sucio || !_diasLeidos.Contains(d.Num)) continue;   // sin leer (archivo roto o ilegible): no se pisa
                    d.Sucio = false;
                    tareas.Add((RutaDia(d.Num), null));
                    fotos.Add(FotoDia(d));
                }
            }
            if (tareas.Count == 0) { _ultimaPersistencia = DateTime.UtcNow; return; }
            for (int i = 0; i < tareas.Count; i++) tareas[i] = (tareas[i].Ruta, SerializarDia(fotos[i]));   // afuera de la llave
            Directory.CreateDirectory(CarpetaCinta);
            foreach (var (ruta, datos) in tareas)
            {
                try
                {
                    var tmp = ruta + ".tmp";
                    File.WriteAllBytes(tmp, datos);
                    File.Move(tmp, ruta, true);
                }
                catch (Exception e) { AdaptadoresFamilia.Excepcion("CintaFamilia.Persistir " + Path.GetFileName(ruta), e); }
            }
            _ultimaPersistencia = DateTime.UtcNow;
        }

        /// <summary>Copia de un dia para escribirlo afuera de la llave (la llave se retiene solo lo que tarda copiar ~1,4 MB).</summary>
        private sealed class FotoDiaSeg
        {
            public long Num; public int N; public long[] T; public double[] P;
            public List<Vela> Velas = new List<Vela>(); public List<(long, long)> Tramos = new List<(long, long)>(); public List<Ses> Sesiones = new List<Ses>();
        }

        private FotoDiaSeg FotoDia(DiaSeg d)   // bajo _llave
        {
            var f = new FotoDiaSeg { Num = d.Num, N = d.N, T = (long[])d.T.Clone(), P = (double[])d.P.Clone() };
            long ini = d.Num * 86400000L, fin = ini + 86400000L;
            foreach (var v in _velas.Values) if (v.B >= ini && v.B < fin && v.N > 0) f.Velas.Add(new Vela { B = v.B, O = v.O, H = v.H, L = v.L, C = v.C, TPri = v.TPri, TUlt = v.TUlt, N = v.N });
            foreach (var t in _tramos) if (t.Hasta >= ini - 86400000L && t.Desde < fin + 86400000L) f.Tramos.Add(t);
            if (_vivoDesde != long.MinValue && _vivoUlt >= ini - 86400000L && _vivoDesde < fin + 86400000L) f.Tramos.Add((_vivoDesde, _vivoUlt));
            foreach (var s in _ses.Values) if (s.Fin > ini && s.Ini < fin) f.Sesiones.Add(new Ses { Dia = s.Dia, Ini = s.Ini, Fin = s.Fin, UltTick = s.UltTick, PrimerVivo = s.PrimerVivo });
            return f;
        }

        private byte[] SerializarDia(FotoDiaSeg d)
        {
            using var ms = new MemoryStream(1 << 20);
            using var w = new BinaryWriter(ms, Encoding.UTF8);
            w.Write(MAGIA); w.Write(1); w.Write(Instrumento); w.Write(d.Num);
            w.Write(d.N);
            for (int i = 0; i < 86400; i++) if (d.T[i] != 0) { w.Write(i); w.Write(d.T[i]); w.Write(d.P[i]); }
            w.Write(d.Velas.Count);
            foreach (var v in d.Velas) { w.Write(v.B); w.Write(v.O); w.Write(v.H); w.Write(v.L); w.Write(v.C); w.Write(v.TPri); w.Write(v.TUlt); w.Write(v.N); }
            w.Write(d.Tramos.Count);
            foreach (var (a, b) in d.Tramos) { w.Write(a); w.Write(b); }
            w.Write(d.Sesiones.Count);
            foreach (var s in d.Sesiones) { w.Write(s.Dia); w.Write(s.Ini); w.Write(s.Fin); w.Write(s.UltTick); w.Write(s.PrimerVivo); }
            w.Flush();
            return ms.ToArray();
        }

        /// <summary>Lee un archivo de dia y lo funde con lo que hay (idempotente). Devuelve false si no es de este instrumento o esta roto.</summary>
        public bool CargarArchivoDia(string ruta)
        {
            byte[] datos;
            try { datos = File.ReadAllBytes(ruta); } catch (Exception e) { AdaptadoresFamilia.Excepcion("CintaFamilia.CargarArchivoDia " + Path.GetFileName(ruta), e); return false; }
            try
            {
                using var ms = new MemoryStream(datos);
                using var r = new BinaryReader(ms, Encoding.UTF8);
                if (r.ReadString() != MAGIA || r.ReadInt32() != 1) return false;
                if (!string.Equals(r.ReadString(), Instrumento, StringComparison.OrdinalIgnoreCase)) return false;
                long num = r.ReadInt64();
                int n = r.ReadInt32();
                var idx = new int[n]; var ts = new long[n]; var ps = new double[n];
                for (int i = 0; i < n; i++) { idx[i] = r.ReadInt32(); ts[i] = r.ReadInt64(); ps[i] = r.ReadDouble(); }
                int nv = r.ReadInt32();
                var vs = new List<Vela>(nv);
                for (int i = 0; i < nv; i++) vs.Add(new Vela { B = r.ReadInt64(), O = r.ReadDouble(), H = r.ReadDouble(), L = r.ReadDouble(), C = r.ReadDouble(), TPri = r.ReadInt64(), TUlt = r.ReadInt64(), N = r.ReadInt32() });
                int nt = r.ReadInt32();
                var tr = new List<(long, long)>(nt);
                for (int i = 0; i < nt; i++) tr.Add((r.ReadInt64(), r.ReadInt64()));
                int ns = r.ReadInt32();
                var ss = new List<Ses>(ns);
                for (int i = 0; i < ns; i++) ss.Add(new Ses { Dia = r.ReadString(), Ini = r.ReadInt64(), Fin = r.ReadInt64(), UltTick = r.ReadInt64(), PrimerVivo = r.ReadInt64() });
                lock (_llave)
                {
                    var d = ObtenerDia(num);
                    _diasLeidos.Add(num);
                    for (int i = 0; i < n; i++)
                    {
                        int k = idx[i]; if (k < 0 || k >= 86400) continue;
                        long tv = d.T[k];
                        if (tv == 0) { d.N++; d.T[k] = ts[i]; d.P[k] = ps[i]; }
                        else if (ts[i] > tv) { d.T[k] = ts[i]; d.P[k] = ps[i]; }
                        if (ts[i] > _ultimoTick) _ultimoTick = ts[i];
                    }
                    foreach (var v in vs)
                    {
                        if (!_velas.TryGetValue(v.B, out var x)) { _velas[v.B] = v; continue; }
                        if (v.TPri < x.TPri) { x.TPri = v.TPri; x.O = v.O; }
                        if (v.TUlt > x.TUlt) { x.TUlt = v.TUlt; x.C = v.C; }
                        if (v.H > x.H) x.H = v.H;
                        if (v.L < x.L) x.L = v.L;
                        x.N = Math.Max(x.N, v.N);
                    }
                    foreach (var t in tr) if (t.Item2 >= t.Item1) _tramos.Add(t);
                    NormalizarTramos();
                    foreach (var s in ss)
                    {
                        if (!_ses.TryGetValue(s.Dia, out var x)) { _ses[s.Dia] = s; continue; }
                        if (s.UltTick > x.UltTick) x.UltTick = s.UltTick;
                        if (s.PrimerVivo < x.PrimerVivo) x.PrimerVivo = s.PrimerVivo;
                    }
                    _sesCache = null;
                    _version++;
                }
                return true;
            }
            catch (Exception e) { AdaptadoresFamilia.Excepcion("CintaFamilia.CargarArchivoDia " + Path.GetFileName(ruta), e); return false; }
        }

        /// <summary>Al arrancar (hilo propio, BelowNormal): los archivos propios de los ultimos DIAS_HISTORIA dias y, para las sesiones sin
        /// archivo propio, la cinta CSV exportada por esta misma 4.0 (PythiaGex4\cinta\cinta-&lt;raiz&gt;-&lt;sesion&gt;.csv, solo con Cinta4Exportar).</summary>
        private void CargarHistoria()
        {
            var hoy = DateTime.UtcNow.Date;
            for (int k = DIAS_HISTORIA; k >= 0; k--)
            {
                if (_parar.IsSet) return;
                long num = AdaptadoresFamilia.PisoDiv(AdaptadoresFamilia.Ms(hoy.AddDays(-k)), 86400000L);
                var ruta = RutaDia(num);
                if (File.Exists(ruta) && CargarArchivoDia(ruta)) _diasCargados++;
                Thread.Sleep(20);
            }
            for (int k = DIAS_HISTORIA; k >= -1; k--)
            {
                if (_parar.IsSet) return;
                try { if (ImportarCsvSesion(hoy.AddDays(-k).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))) _csvImportados++; }
                catch (Exception e) { AdaptadoresFamilia.Excepcion("CintaFamilia.ImportarCsv", e); }
            }
        }

        /// <summary>La cinta CSV de una sesion (formato de la 3.0: t,precio,dv,lado,id; vivo + -relleno.csv) si es de este instrumento y cambio
        /// desde la ultima importacion (marca seg-&lt;instr&gt;-csv-&lt;sesion&gt;.hecho con el tamaño). Mismas reglas que preview_niveles._leer_cinta_dia.</summary>
        public bool ImportarCsvSesion(string sesion)
        {
            string viv = Path.Combine(CarpetaCinta, "cinta-" + Raiz + "-" + sesion + ".csv"), rel = Path.Combine(CarpetaCinta, "cinta-" + Raiz + "-" + sesion + "-relleno.csv");
            if (!File.Exists(viv) && !File.Exists(rel)) return false;
            try
            {
                var ins = viv + ".instrumento";
                if (File.Exists(ins) && !string.Equals(File.ReadAllText(ins).Trim(), Instrumento, StringComparison.OrdinalIgnoreCase)) return false;
            }
            catch { return false; }
            long tam = (File.Exists(viv) ? new FileInfo(viv).Length : 0) + (File.Exists(rel) ? new FileInfo(rel).Length : 0);
            var marca = Path.Combine(CarpetaCinta, "seg-" + Seguro(Instrumento) + "-csv-" + sesion + ".hecho");
            try { if (File.Exists(marca) && File.ReadAllText(marca).Trim() == tam.ToString(CultureInfo.InvariantCulture)) return false; } catch { }
            var vivo = LeerCsv(viv, () => _parar.IsSet);
            if (_parar.IsSet) return false;
            var rell = LeerCsv(rel, () => _parar.IsSet);
            if (_parar.IsSet) return false;
            CargarCsv(vivo, rell, LeerListo(rel + ".listo"));
            try { Directory.CreateDirectory(CarpetaCinta); File.WriteAllText(marca, tam.ToString(CultureInfo.InvariantCulture)); } catch { }
            AdaptadoresFamilia.Log?.Invoke("adaptadores: " + Instrumento + ": cinta CSV de la sesion " + sesion + " importada (" + vivo.Count + " vivos, " + rell.Count + " relleno)");
            return true;
        }

        /// <summary>Funde una sesion leida de la cinta CSV: vivo (un tramo del primer al ultimo tick) y relleno
        /// (solo t &lt; primer vivo). 'listo' = (desde, hasta) del .listo del relleno, o null (se usa el primero y el ultimo del relleno).</summary>
        public void CargarCsv(IReadOnlyList<(long T, double P)> vivo, IReadOnlyList<(long T, double P)> relleno, (long Desde, long Hasta)? listo)
        {
            // el tramo del vivo: del primer al ultimo tick, sin cortar en los huecos (la vista previa lee este mismo archivo y no los distingue)
            var tramos = new List<(long, long)>();
            long tDesde = long.MaxValue, tUlt = long.MinValue;
            foreach (var (t, p) in vivo)
            {
                if (t <= 0 || !(p > 0)) continue;
                if (t < tDesde) tDesde = t;
                if (t > tUlt) tUlt = t;
            }
            if (tUlt != long.MinValue) tramos.Add((tDesde, tUlt));
            // los ticks, de a tandas (la llave no se retiene mas de una tanda: el hilo de ATAS tambien la usa)
            const int TANDA = 50000;
            for (int i0 = 0; i0 < vivo.Count; i0 += TANDA)
            {
                lock (_llave)
                {
                    int i1 = Math.Min(vivo.Count, i0 + TANDA);
                    for (int i = i0; i < i1; i++)
                    {
                        var (t, p) = vivo[i];
                        if (t <= 0 || !(p > 0)) continue;
                        var ses = SesionDe(t);
                        if (t < ses.PrimerVivo) { ses.PrimerVivo = t; ses.Sucia = true; }
                        Poner(t, p, ses);
                    }
                }
                if (i0 + TANDA < vivo.Count && _hilo != null) Thread.Sleep(1);
            }
            lock (_llave) { _tramos.AddRange(tramos); NormalizarTramos(); }
            if (relleno.Count > 0)
            {
                long d = listo?.Desde ?? relleno[0].T, h = listo?.Hasta ?? relleno[relleno.Count - 1].T;
                Relleno(relleno, d, h);
            }
        }

        /// <summary>t,precio de un CSV de la cinta (sin la cabecera ni lineas rotas: la 'x' de una linea cortada no parsea), en orden de archivo.</summary>
        public static List<(long T, double P)> LeerCsv(string ruta, Func<bool> cancelar = null)
        {
            var r = new List<(long, double)>();
            if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) return r;
            using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            string l; int n = 0;
            while ((l = sr.ReadLine()) != null)
            {
                if (++n % 200000 == 0) { if (cancelar != null && cancelar()) break; Thread.Sleep(5); }
                if (l.Length == 0 || l[0] == 't') continue;
                int c1 = l.IndexOf(','); if (c1 <= 0) continue;
                int c2 = l.IndexOf(',', c1 + 1); if (c2 < 0) c2 = l.Length;
                if (!long.TryParse(l.AsSpan(0, c1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long t)) continue;
                if (!double.TryParse(l.AsSpan(c1 + 1, c2 - c1 - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double p)) continue;
                r.Add((t, p));
            }
            return r;
        }

        private static (long, long)? LeerListo(string ruta)
        {
            try
            {
                if (!File.Exists(ruta)) return null;
                using var doc = JsonDocument.Parse(File.ReadAllText(ruta));
                var e = doc.RootElement;
                if (e.TryGetProperty("desde", out var d) && e.TryGetProperty("hasta", out var h) && d.ValueKind == JsonValueKind.Number && h.ValueKind == JsonValueKind.Number) return (d.GetInt64(), h.GetInt64());
            }
            catch { }
            return null;
        }

        /// <summary>Saca de memoria lo de mas de DIAS_HISTORIA + 1 dias y borra los archivos propios de mas de 9 dias.</summary>
        private void Podar()
        {
            long corteMs = AdaptadoresFamilia.Ms(DateTime.UtcNow.Date.AddDays(-(DIAS_HISTORIA + 1)));
            long corteDia = AdaptadoresFamilia.PisoDiv(corteMs, 86400000L);
            lock (_llave)
            {
                var viejos = new List<long>();
                foreach (var k in _dias.Keys) if (k < corteDia) viejos.Add(k);
                foreach (var k in viejos) { if (_dias[k].Sucio) continue; _dias.Remove(k); }
                if (_diaCache != null && _diaCache.Num < corteDia) _diaCache = null;
                var vv = new List<long>();
                foreach (var k in _velas.Keys) if (k < corteMs) vv.Add(k);
                foreach (var k in vv) _velas.Remove(k);
                vv.Clear();
                foreach (var k in _velasGraf.Keys) if (k < corteMs) vv.Add(k);
                foreach (var k in vv) _velasGraf.Remove(k);
                _grafInvalidas.RemoveWhere(k => k < corteMs);
                _tramos.RemoveAll(t => t.Hasta < corteMs);
                var sv = new List<string>();
                foreach (var kv in _ses) if (kv.Value.Fin < corteMs) sv.Add(kv.Key);
                foreach (var k in sv) _ses.Remove(k);
                _velaCache = null; _sesCache = null;
            }
            try
            {
                if (!Directory.Exists(CarpetaCinta)) return;
                var limite = DateTime.UtcNow.Date.AddDays(-(DIAS_HISTORIA + 1));
                foreach (var f in Directory.GetFiles(CarpetaCinta, "seg-" + Seguro(Instrumento) + "-????-??-??.bin"))
                {
                    var nom = Path.GetFileNameWithoutExtension(f);
                    if (nom.Length < 10) continue;
                    if (DateTime.TryParseExact(nom.Substring(nom.Length - 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia) && dia < limite)
                        try { File.Delete(f); } catch { }
                }
            }
            catch { }
        }
    }

    // ======================================================================================================================================
    // ILibroNq: el libro de NQ por Rithmic (viva3 del clon)
    // ======================================================================================================================================

    /// <summary>
    /// El libro de NQ de la 4.0 (ILibroNq): una foto por minuto, la MISMA linea que Viva3 escribe en PythiaGex4\viva, armada como
    /// preview_niveles.foto_viva3 (mismo filtro y mismo armado). Uno por raiz en el proceso. Al primer Registrar se siembra desde los archivos
    /// viva3 propios de las ultimas RetencionHoras (hilo BelowNormal); despues entra la del minuto por AgregarLinea.
    /// Dedup por ts: gana la foto con MAS filas crudas (como _nuevas_nq).
    /// </summary>
    public sealed class LibroNqFamilia : ILibroNq
    {
        private static readonly object _regLlave = new object();
        private static readonly Dictionary<string, LibroNqFamilia> _registro = new Dictionary<string, LibroNqFamilia>(StringComparer.OrdinalIgnoreCase);

        public static LibroNqFamilia Para(string raiz)
        {
            raiz = string.IsNullOrWhiteSpace(raiz) ? "NQ" : raiz.Trim().ToUpperInvariant();
            lock (_regLlave)
            {
                if (!_registro.TryGetValue(raiz, out var l)) { l = new LibroNqFamilia(raiz); _registro[raiz] = l; }
                return l;
            }
        }

        public string Raiz { get; }
        /// <summary>Cuantas horas de fotos se guardan en memoria (la sesion entera mas margen: la sesion dura 23 h).</summary>
        public int RetencionHoras { get; set; } = 30;
        private readonly HashSet<object> _duenos = new HashSet<object>(ReferenceEqualityComparer.Instance);

        /// <summary>Para el arnes: un libro suelto (sin registro ni siembra).</summary>
        public LibroNqFamilia(string raiz) { Raiz = raiz; }

        private readonly object _llave = new object();
        private readonly SortedDictionary<long, (FotoCadena F, int Crudas)> _porTs = new SortedDictionary<long, (FotoCadena, int)>();
        private FotoCadena[] _vista;                      // copia ordenada para leer sin llave larga (se rehace al cambiar)
        private long _version, _nVivas, _nArchivo, _nRechazadas;
        private volatile bool _sembrado;
        private Thread _hilo;

        public long Version { get { lock (_llave) return _version; } }

        public void Registrar(object dueno)
        {
            bool primero;
            lock (_regLlave) { primero = _duenos.Count == 0; _duenos.Add(dueno); }
            if (primero) Arrancar();
        }

        public void Soltar(object dueno) { lock (_regLlave) _duenos.Remove(dueno); }

        /// <summary>Siembra (una vez por proceso) desde PythiaGex4\viva, en un hilo BelowNormal. Idempotente.</summary>
        public void Arrancar()
        {
            lock (_llave)
            {
                if (_sembrado || (_hilo != null && _hilo.IsAlive)) return;
                _hilo = new Thread(Sembrar) { IsBackground = true, Name = "PythiaGex4 libro-familia", Priority = ThreadPriority.BelowNormal };
                _hilo.Start();
            }
        }

        private void Sembrar()
        {
            try
            {
                var ahora = DateTime.UtcNow;
                var desde = ahora.AddHours(-RetencionHoras);
                var carpeta = Path.Combine(AdaptadoresFamilia.Carpeta, "viva");
                int lineas = 0, buenas = 0;
                for (var d = desde.Date; d <= ahora.Date; d = d.AddDays(1))
                {
                    var ruta = Path.Combine(carpeta, "viva3-" + Raiz + "-" + d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jsonl");
                    if (!File.Exists(ruta)) continue;
                    using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
                    using var sr = new StreamReader(fs, Encoding.UTF8);
                    string l;
                    while ((l = sr.ReadLine()) != null)
                    {
                        lineas++;
                        if (lineas % 40 == 0) Thread.Sleep(2);
                        if (AgregarLinea(l, "archivo", desde)) buenas++;
                    }
                }
                AdaptadoresFamilia.Log?.Invoke("adaptadores: libro " + Raiz + " sembrado de " + carpeta + ": " + buenas + " fotos de " + lineas + " lineas (" + RetencionHoras + " h). " + Estado);
            }
            catch (Exception e) { AdaptadoresFamilia.Excepcion("LibroNqFamilia.Sembrar", e); }
            finally { _sembrado = true; }
        }

        /// <summary>Una linea viva3 (la del minuto, o una del archivo). false si no sirve (como foto_viva3: None) o si ya habia una foto con ese ts
        /// con tantas o mas filas crudas. 'desde' opcional: ignora fotos mas viejas.</summary>
        public bool AgregarLinea(string linea, string origen = "vivo", DateTime? desde = null)
        {
            FotoCadena f; int crudas;
            try { f = ParsearViva3(linea, Raiz, out crudas); }
            catch (Exception e) { AdaptadoresFamilia.Excepcion("LibroNqFamilia.Parsear", e); return false; }
            if (f == null) { if (origen == "vivo") _nRechazadas++; return false; }
            if (desde.HasValue && f.TsUtc < desde.Value) return false;
            long k = AdaptadoresFamilia.Ms(f.TsUtc);
            lock (_llave)
            {
                if (_porTs.TryGetValue(k, out var y) && y.Crudas >= crudas) return false;
                _porTs[k] = (f, crudas);
                if (origen == "vivo") _nVivas++; else _nArchivo++;
                // poda: mas viejas que la retencion contra la mas nueva
                long corte = k - RetencionHoras * 3600000L;
                if (_porTs.Count > 200)
                {
                    List<long> viejas = null;
                    foreach (var kv in _porTs) { if (kv.Key >= corte) break; (viejas ??= new List<long>()).Add(kv.Key); }
                    if (viejas != null) foreach (var x in viejas) _porTs.Remove(x);
                }
                _vista = null;
                _version++;
            }
            return true;
        }

        private FotoCadena[] Vista()
        {
            lock (_llave)
            {
                if (_vista != null) return _vista;
                var a = new FotoCadena[_porTs.Count]; int i = 0;
                foreach (var kv in _porTs) a[i++] = kv.Value.F;
                _vista = a;
                return a;
            }
        }

        /// <summary>Fotos con TsUtc en [desde, hasta), ordenadas por TsUtc. Las FotoCadena son inmutables (se comparten).</summary>
        public IReadOnlyList<FotoCadena> Fotos(DateTime desdeUtc, DateTime hastaUtc)
        {
            var v = Vista();
            long d = AdaptadoresFamilia.Ms(desdeUtc), h = AdaptadoresFamilia.Ms(hastaUtc);
            int lo = 0, hi = v.Length;
            while (lo < hi) { int m = (lo + hi) >> 1; if (AdaptadoresFamilia.Ms(v[m].TsUtc) < d) lo = m + 1; else hi = m; }
            var r = new List<FotoCadena>();
            for (int i = lo; i < v.Length; i++) { if (AdaptadoresFamilia.Ms(v[i].TsUtc) >= h) break; r.Add(v[i]); }
            return r;
        }

        public FotoCadena Ultima() { var v = Vista(); return v.Length == 0 ? null : v[v.Length - 1]; }

        public bool Sembrado => _sembrado;

        public string Estado
        {
            get
            {
                var u = Ultima();
                lock (_llave)
                    return "libro " + Raiz + ": " + _porTs.Count + " fotos (vivas " + _nVivas + ", archivo " + _nArchivo + (_nRechazadas > 0 ? ", rechazadas " + _nRechazadas : "") + ")"
                        + (u == null ? "" : ", ultima " + u.TsUtc.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "Z hace " + ((DateTime.UtcNow - u.TsUtc).TotalSeconds).ToString("0", CultureInfo.InvariantCulture) + " s, " + u.Filas.Length + " filas")
                        + (_sembrado ? "" : _hilo != null ? " (sembrando)" : " (sin sembrar)");
            }
        }

        /// <summary>
        /// preview_niveles.foto_viva3 en C#: una linea viva3 -> FotoCadena (Libro = raiz, EsFuturo, Generado = Ts = Dato = ts, Spot = FuturoFoto =
        /// futuro). null si la linea no sirve (corta, sin '}' final, json roto, ts mal, futuro &lt;= 0 o sin filas). Dias = los dias redondeados
        /// a 4 (round de Python) de TODAS las filas crudas, ordenados; filas [K, V, oi_c, oi_p, iv_c, iv_p, vol_c, vol_p] solo con iv &gt; 0 y
        /// (oi &gt; 0 o vol &gt; 0), en el orden de primera aparicion de (K, V); el call (es_call &gt;= 0,5) pisa 2/4/6 y el put 3/5/7.
        /// crudas = cantidad de filas crudas (para el dedup por ts).
        /// </summary>
        public static FotoCadena ParsearViva3(string linea, string libro, out int crudas)
        {
            crudas = 0;
            if (linea == null) return null;
            var l = linea.Trim();
            if (l.Length < 40 || !l.EndsWith("}", StringComparison.Ordinal)) return null;
            string ts = null; double fut = 0; var filas = new List<double[]>();
            try
            {
                var bytes = Encoding.UTF8.GetBytes(l);
                var r = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow });
                if (!r.Read() || r.TokenType != JsonTokenType.StartObject) return null;
                while (r.Read())
                {
                    if (r.TokenType == JsonTokenType.EndObject) break;
                    if (r.TokenType != JsonTokenType.PropertyName) return null;
                    string nom = r.GetString();
                    if (!r.Read()) return null;
                    if (nom == "ts") { if (r.TokenType != JsonTokenType.String) return null; ts = r.GetString(); }
                    else if (nom == "futuro") { fut = r.TokenType == JsonTokenType.Number ? r.GetDouble() : 0; }
                    else if (nom == "filas")
                    {
                        if (r.TokenType == JsonTokenType.Null) continue;
                        if (r.TokenType != JsonTokenType.StartArray) return null;
                        while (r.Read() && r.TokenType != JsonTokenType.EndArray)
                        {
                            if (r.TokenType != JsonTokenType.StartArray) return null;
                            var x = new double[12]; int i = 0;
                            while (r.Read() && r.TokenType != JsonTokenType.EndArray)
                            {
                                double v = r.TokenType == JsonTokenType.Number ? r.GetDouble() : double.NaN;
                                if (i < 12) x[i] = v;
                                i++;
                            }
                            if (i < 8) return null;   // en Python x[7] tiraria IndexError: la linea no sirve
                            filas.Add(x);
                        }
                    }
                    else r.Skip();
                }
            }
            catch { return null; }
            if (ts == null || !DateTime.TryParseExact(ts, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var tsUtc)) return null;
            tsUtc = DateTime.SpecifyKind(tsUtc, DateTimeKind.Utc);
            if (!(fut > 0) || filas.Count == 0) return null;
            crudas = filas.Count;

            // dias = sorted(set(round(float(x[1]), 4) for x in filas))
            var setDias = new SortedSet<double>();
            foreach (var x in filas) setDias.Add(AdaptadoresFamilia.RedondeoPy(x[1], 4));
            var dias = new double[setDias.Count]; { int i = 0; foreach (var d in setDias) dias[i++] = d; }
            var idxDia = new Dictionary<double, int>(dias.Length);
            for (int i = 0; i < dias.Length; i++) idxDia[dias[i]] = i;

            var orden = new List<(double K, int V)>();
            var por = new Dictionary<(double, int), double[]>();
            foreach (var x in filas)
            {
                double K = x[0], di = AdaptadoresFamilia.RedondeoPy(x[1], 4), oi = x[3], iv = x[4], vol = x[7];
                bool call = x[2] >= 0.5;
                if (iv <= 0 || (oi <= 0 && vol <= 0)) continue;   // literal de foto_viva3: iv <= 0 or (oi <= 0 and vol <= 0)
                int v = idxDia[di];
                if (!por.TryGetValue((K, v), out var e)) { e = new double[] { 0, 0, 0, 0, 0, 0 }; por[(K, v)] = e; orden.Add((K, v)); }
                if (call) { e[0] = oi; e[2] = iv; e[4] = vol; } else { e[1] = oi; e[3] = iv; e[5] = vol; }
            }
            var fs = new FilaCadena[orden.Count];
            double oiTot = 0;
            for (int i = 0; i < orden.Count; i++)
            {
                var (K, v) = orden[i]; var e = por[(K, v)];
                fs[i] = new FilaCadena(K, v, e[0], e[1], e[2], e[3], e[4], e[5]);
                oiTot += e[0] + e[1];
            }
            return new FotoCadena
            {
                Libro = string.IsNullOrEmpty(libro) ? "NQ" : libro, GeneradoUtc = tsUtc, TsUtc = tsUtc, DatoUtc = tsUtc, Spot = fut, FuturoFoto = fut,
                Dias = dias, Filas = fs, EsFuturo = true, OiTotal = oiTot,
            };
        }
    }
}
