// CalculoTqqq.cs — PythiaGex 4.1, modulo TQQQ (B4), 08-10-2026. Implementa ICalculoTqqq (Contratos.cs).
// Port de laboratorio/tres/tqqq/tqqq_vivo.py 1.0 (lo que dibuja hoy la 4.0.6) con los arreglos del modo corregido:
//   Conversion NQ(K) = s K + c (banda +-4):
//     s = N0 / (3 P0), fija en el dia:
//         paridad   = tqqq_vivo.s_del_dia: spot de la ultima foto de la sesion anterior con t_dato <= 20:00 UTC (TQQQ y NDX); si falta,
//                     recta libre (np.polyfit) con >= 10 fotos de la sesion con precio. El arnes fuerza la s de conversion.json el 08-10
//                     (como hizo tqqq_vivo ese dia).
//         corregida = prev_day_close de TQQQ y de _NDX validados en la rueda/premarket de D (descargador: OpcionesTqqq.Cierres; si no, el
//                     prev_day_close de las fotos con dato del dia D). Defecto 1 del Python: s_del_dia toma el spot del after-hours.
//                     Si no hay: recta libre con >= 20 muestras de rueda y spot vivo, CON AVISO.
//     c por foto i (en orden de generado, sesion de la foto): px = MNQ en (sello CBOE - 900 s), solo tick (edad <= 120 s y sello <= ultimo
//       tick: tq_comun.precio_en); vivo = alguna de las 12 fotos previas a <= 600 s con spot distinto (> 1e-9); muestra px - s spot si vivo,
//       px valido, spot > 0 y el dato cae en la rueda; ventana de 24; c_i = robusta(muestras, piso 1,0) si quedan >= 5 (si no NaN).
//       Se calcula en todas las fotos: despues del cierre queda congelado en el ultimo de la rueda.
//   Velas m2 (las pide el motor, desde la rueda): foto = ultima con generado <= apertura + 120 s (mira el CIERRE de la vela, a diferencia
//     de NQ/NDX/QQQ); vale si apertura + 120 s - generado <= 1500 s y c_i no es NaN. Cada vela usa (s, c_i) de SU foto. Niveles de la foto
//     en strikes (TqqqPerfil, cache por foto) -> round(s K + c_i, 2). T_DOMS_raz (solo comparar): nq = MNQ en (cierre de la vela - 1 s),
//     T_eq = (nq - c)/s, precio = round(K nq / T_eq, 2).
//   Ancla de noche (OpcionesTqqq.AnclaNoche, default false, SIN VALIDAR, solo modo corregido): ver TqqqNoche.cs. Con RegistrarAnclas
//     (default true, modo corregido) se miden y guardan los cierres y el error de B/C cada noche aunque no se dibuje nada de noche.
// Hilos: el motor llama Vela desde su hilo de fondo; todo bajo una llave. Sin referencias a ATAS. try/catch con log propio.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace PythiaGexCuatro.Familia
{
    /// <summary>Cierres observados por el descargador para una fecha NY (lo llena el integrador desde ManijaCboe.Cierre de B2).</summary>
    public sealed class CierresTqqqDia
    {
        public double Anterior = double.NaN; public DateTime AnteriorUtc;   // prev_day_close validado en la rueda/premarket de esa fecha (= cierre de la sesion previa)
        public double Close = double.NaN; public DateTime CloseUtc;         // data.close visto despues de las 15:59 NY de esa fecha
        public double PdcNuevo = double.NaN; public DateTime PdcNuevoUtc;   // prev_day_close visto DESPUES del cierre y ya distinto del primero (CBOE lo cambio)
    }

    public sealed class OpcionesTqqq
    {
        private static readonly string App = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public bool Corregida = true;              // Familia41Corregida (false = paridad exacta con tqqq_vivo.py)
        public bool AnclaNoche = false;            // Tqqq41AnclaNoche: dibujar de noche con las anclas B/C (SIN VALIDAR). Solo con Corregida.
        public bool RegistrarAnclas = true;        // medir y guardar NQ_1600, N0', P0' y el error de B/C cada noche (no dibuja nada). Solo con Corregida.
        public string Carpeta = Path.Combine(App, "ATAS", "PythiaGex4", "familia");   // null/"" = sin persistencia
        public string RutaLog = Path.Combine(App, "ATAS", "pythiagex4-tqqq.log");      // null/"" = sin log
        /// <summary>(libro "TQQQ" | "NDX", fecha NY) -> cierres del descargador. null = solo lo que traen las fotos (FotoCadena.CierreAnterior).</summary>
        public Func<string, DateTime, CierresTqqqDia> Cierres;
        /// <summary>Respaldo de NQ_1600: cierre de la vela del grafico que termina en t (UTC). null = sin respaldo.</summary>
        public Func<DateTime, double> CierreVelaGrafico;
        /// <summary>Arnes: s forzada por dia (yyyy-MM-dd). tqqq_vivo hizo esto el 08-10 con conversion.json.</summary>
        public Dictionary<string, double> SForzada;
        public int RefrescoS = 20;                 // relee fotos y rehace c como maximo cada tanto si no hay foto nueva (la cinta puede rellenarse tarde)
        public int RetencionDias = 14;
    }

    /// <summary>Lo que no entra en TqqqVela: modo, banda, textos. Se obtiene con CalculoTqqq.Detalle(vela).</summary>
    public sealed class TqqqDetalle
    {
        public string Modo = "rueda";              // "rueda" | "rueda, esperando NDX congelado" | "ancla B (cierre implicito)" | "ancla C (cierre oficial)"
        public double Banda = CalculoTqqq.BANDA;
        public double S = double.NaN, C = double.NaN;
        public double Spot = double.NaN, VencD = double.NaN;
        public int NMuestras;                      // muestras que quedaron en la robusta de esa foto
        public DateTime GeneradoUtc, TsUtc, DatoUtc;
        public string STexto = "";
        public string AnclaTexto = "";
        private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-AR");

        /// <summary>"x3: NQ = 124,21 x TQQQ + 21012,1 (+-4); s = ..." (pestaña).</summary>
        public string Texto => "x3: NQ = " + S.ToString("0.00", Es) + " x TQQQ + " + C.ToString("0.0", Es) + " (+-" + Banda.ToString("0.#", Es) + ")" +
                               (Modo.StartsWith("ancla", StringComparison.Ordinal) ? "; " + Modo + ": " + AnclaTexto + ", 1 noche medida, SIN VALIDAR" : "; " + STexto) +
                               (Modo.Contains("esperando") ? " (esperando NDX congelado)" : "");

        /// <summary>"TQQQ 82,0 x3: NQ = 124,21 x K + 21012,1 (+-4)" (rotulo de un nivel, como tqqq_vivo 'conv').</summary>
        public string TextoK(double k) => "TQQQ " + k.ToString("0.0", Es) + " x3: NQ = " + S.ToString("0.00", Es) + " x K + " + C.ToString("0.0", Es) +
                                          " (+-" + Banda.ToString("0.#", Es) + ")" + (Modo.StartsWith("ancla", StringComparison.Ordinal) ? " " + Modo : "");
    }

    /// <summary>Una fila de la tabla de muestras (arnes y pestaña).</summary>
    public readonly struct TqqqMuestra
    {
        public readonly DateTime Generado, Ts; public readonly double Spot, Px, Muestra, C; public readonly bool Vivo, Rueda; public readonly int N;
        public TqqqMuestra(DateTime g, DateTime ts, double spot, double px, bool vivo, bool rueda, double m, double c, int n)
        { Generado = g; Ts = ts; Spot = spot; Px = px; Vivo = vivo; Rueda = rueda; Muestra = m; C = c; N = n; }
    }

    public sealed class CalculoTqqq : ICalculoTqqq, IVersionSTqqq
    {
        private long _versionS;
        /// <summary>4.1.0: sube cada vez que cambia la s de alguna sesion (ver IVersionSTqqq en Contratos.cs).</summary>
        public long VersionS { get { lock (_llave) return _versionS; } }
        public const string VERSION = "tqqq 4.1.0 (08-10-2026)";
        public const int RETRASO_S = 900, VIDA_S = 1500, VIVO_S = 600, VIVO_FOTOS = 12, VENTANA = 24, MIN_N = 5, EDAD_TICK_S = 120;
        public const long NS2 = 120000L;
        public const double BANDA = 4.0, BANDA_B = 8.0, BANDA_C = 5.0, PISO = 1.0;
        public static readonly string[] Series = { "T_DOMS_vol", "T_MUROS_vol", "T_MUROS_oi", "T_ZERO_oi", "T_DOMS_raz" };

        private readonly OpcionesTqqq _o;
        private readonly object _llave = new object();
        private readonly TqqqLog _log;
        private readonly Dictionary<string, EstadoSesion> _estados = new Dictionary<string, EstadoSesion>(StringComparer.Ordinal);
        private readonly Dictionary<(long, long), TqqqNivelesFoto> _niveles = new Dictionary<(long, long), TqqqNivelesFoto>();
        private readonly ConditionalWeakTable<TqqqVela, TqqqDetalle> _detalles = new ConditionalWeakTable<TqqqVela, TqqqDetalle>();
        private readonly TqqqAnclas _anclas;
        private readonly Dictionary<string, long> _anclaWall = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _anclaVer = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _anclaT = new Dictionary<string, long>(StringComparer.Ordinal);
        private string _contrato = "";
        private bool _anclasCargadas;
        private long _maxT = long.MinValue;        // el mayor (apertura + 120 s) pedido: el "ahora" de los datos (sirve igual en el arnes)
        private DateTime _podado;

        public CalculoTqqq() : this(new OpcionesTqqq()) { }
        public CalculoTqqq(OpcionesTqqq o)
        {
            _o = o ?? new OpcionesTqqq();
            _log = new TqqqLog(_o.RutaLog);
            _anclas = new TqqqAnclas(TqqqArchivo.RutaCierres(_o.Carpeta));
            if (_o.AnclaNoche && !_o.Corregida) _log.Linea("aviso: ancla de noche pedida en modo paridad: se ignora (la paridad reproduce tqqq_vivo, que de noche no dibuja)");
        }

        public OpcionesTqqq Opciones => _o;
        public string RutaLog => _log.Ruta;

        /// <summary>Contrato del grafico (MNQZ6 -> "Z6" o el codigo entero). Si cambia (roll) se reinician las muestras: otra cinta, otro c.</summary>
        public string Contrato
        {
            get { lock (_llave) return _contrato; }
            set
            {
                lock (_llave)
                {
                    var v = value ?? "";
                    if (v == _contrato) return;
                    if (_contrato.Length > 0) _log.Linea("contrato " + _contrato + " -> " + v + ": se reinician las muestras de c");
                    _contrato = v; _estados.Clear(); _anclaWall.Clear(); _anclaVer.Clear(); _anclaT.Clear();
                }
            }
        }

        public TqqqDetalle Detalle(TqqqVela v)
        {
            if (v == null) return null;
            lock (_llave) return _detalles.TryGetValue(v, out var d) ? d : null;
        }

        /// <summary>El ancla guardada para una fecha NY (null si no hay). Copia de solo lectura para la pestaña y el arnes.</summary>
        public TqqqAnclaDia Ancla(DateTime fechaNy)
        {
            lock (_llave) { CargarAnclas(); return _anclas.Ver(TqqqHora.Fecha(fechaNy), _contrato); }
        }

        /// <summary>La tabla de muestras de la sesion (arnes y pestaña). Vacia si esa sesion no se calculo.</summary>
        public List<TqqqMuestra> Muestras(string dia)
        {
            lock (_llave)
            {
                var l = new List<TqqqMuestra>();
                foreach (var st in _estados.Values)
                {
                    if (st.Ses.Dia != dia) continue;
                    for (int i = 0; i < st.Fotos.Length; i++)
                        l.Add(new TqqqMuestra(st.Fotos[i].GeneradoUtc, st.Fotos[i].TsUtc, st.Fotos[i].Spot, st.Px[i], st.Vivo[i], st.Rueda[i], st.Muestra[i], st.Cv[i], st.N[i]));
                    break;
                }
                return l;
            }
        }

        /// <summary>s de una sesion ya calculada (NaN si no) y su texto.</summary>
        public (double S, string Texto) SDe(string dia)
        {
            lock (_llave) { foreach (var st in _estados.Values) if (st.Ses.Dia == dia) return (st.S, st.STexto); return (double.NaN, ""); }
        }

        /// <summary>Texto corto para la pestaña.</summary>
        public string Estado
        {
            get
            {
                lock (_llave)
                {
                    EstadoSesion ult = null;
                    foreach (var st in _estados.Values) if (ult == null || string.CompareOrdinal(st.Ses.Dia, ult.Ses.Dia) > 0) ult = st;
                    if (ult == null) return "TQQQ: sin calcular";
                    int u = -1; for (int i = ult.Cv.Length - 1; i >= 0; i--) if (!double.IsNaN(ult.Cv[i])) { u = i; break; }
                    return "TQQQ " + ult.Ses.Dia + ": " + (double.IsNaN(ult.S) ? "sin s" : ult.STexto) +
                           (u >= 0 ? " · c " + ult.Cv[u].ToString("0.0", CultureInfo.InvariantCulture) + " (" + ult.N[u] + " muestras)" : " · c sin muestras todavia") +
                           " · " + ult.Fotos.Length + " fotos";
                }
            }
        }

        // ================================================================== ICalculoTqqq

        public TqqqVela Vela(long aperturaM2Ms, ICinta cinta, IFuenteCboe cboe)
        {
            lock (_llave)
            {
                try { return VelaAdentro(aperturaM2Ms, cinta, cboe); }
                catch (Exception e) { _log.Error("Vela", e); return null; }
            }
        }

        private TqqqVela VelaAdentro(long k, ICinta cinta, IFuenteCboe cboe)
        {
            if (cinta == null || cboe == null) return null;
            k = Piso(k, NS2);
            long t = k + NS2;
            if (t > _maxT) _maxT = t;
            bool corr = _o.Corregida, noche = corr && _o.AnclaNoche;
            var ses = TqqqSesion.De(k, corr, noche);
            var st = EstadoDe(ses, cinta, cboe);
            TqqqDetalle dA = null;
            var vA = k >= TqqqHora.Ms(ses.RuedaIniUtc) ? VelaRueda(st, k, cinta, out dA) : null;
            if (corr && (_o.RegistrarAnclas || noche)) MedirAnclas(st, cinta, cboe);
            if (!noche) return vA;

            // ---- ancla de noche (SIN VALIDAR)
            bool habil = TqqqHora.EsHabil(ses.Fecha.Date);
            if (habil && t > TqqqHora.Ms(ses.CierreUtc))
            {   // despues del cierre de hoy: ancla al cierre de hoy (si ya hay NDX congelado); si no, sigue la rueda con su rotulo
                var vB = VelaAncla(_anclas.Ver(ses.Dia, _contrato), st, k, cinta, cboe, true);
                if (vB != null) return vB;
                if (vA != null) { dA.Modo = "rueda, esperando NDX congelado"; return vA; }
                return null;
            }
            if (vA != null) return vA;
            // noche, premarket o rueda sin 5 muestras todavia (o feriado): ancla al cierre del habil anterior
            var dPrev = TqqqHora.HabilAnterior(ses.Fecha.Date);
            var a = AnclaCalculada(dPrev, cinta, cboe);
            return VelaAncla(a, st, k, cinta, cboe, false);
        }

        // ================================================================== estado por sesion (fotos, s, c)

        private sealed class EstadoSesion
        {
            public TqqqSesion Ses;
            public FotoCadena[] Fotos = Array.Empty<FotoCadena>(); public long[] Gens = Array.Empty<long>();
            public double[] Px = Array.Empty<double>(), Muestra = Array.Empty<double>(), Cv = Array.Empty<double>();
            public int[] N = Array.Empty<int>(); public bool[] Vivo = Array.Empty<bool>(), Rueda = Array.Empty<bool>();
            public double S = double.NaN, P0 = double.NaN, N0 = double.NaN; public string STexto = "", SFuente = ""; public bool SFija;
            public int PrimerCv = -1, UltimaConMuestra = -1;
            public long VersionCboe = long.MinValue; public DateTime TickVisto; public long WallMs = long.MinValue;
            public Dictionary<long, double> PxGuardado; public readonly HashSet<long> PxAnotado = new HashSet<long>(); public string RutaMuestras;
            public string Huella = "";
        }

        private EstadoSesion EstadoDe(TqqqSesion ses, ICinta cinta, IFuenteCboe cboe)
        {
            if (!_estados.TryGetValue(ses.Clave, out var st))
            {
                st = new EstadoSesion { Ses = ses, RutaMuestras = TqqqArchivo.RutaMuestras(_o.Carpeta, ses.Dia, _contrato) };
                try { st.PxGuardado = TqqqArchivo.LeerMuestras(st.RutaMuestras); } catch (Exception e) { _log.Error("LeerMuestras", e); st.PxGuardado = new Dictionary<long, double>(); }
                _estados[ses.Clave] = st;
                while (_estados.Count > 5)
                {   // se descarta la sesion mas vieja
                    string vieja = null;
                    foreach (var kv in _estados) if (vieja == null || string.CompareOrdinal(kv.Value.Ses.Dia, _estados[vieja].Ses.Dia) < 0) vieja = kv.Key;
                    _estados.Remove(vieja);
                }
                _log.Linea("sesion " + ses + (st.PxGuardado.Count > 0 ? ": " + st.PxGuardado.Count + " precios del MNQ repuestos de " + Path.GetFileName(st.RutaMuestras) : ""));
                Podar();
            }
            Refrescar(st, cinta, cboe);
            return st;
        }

        private void Refrescar(EstadoSesion st, ICinta cinta, IFuenteCboe cboe)
        {
            long ver = cboe.Version;
            DateTime tick = cinta.UltimoTickUtc;
            long wall = Environment.TickCount64;
            if (st.WallMs != long.MinValue && ver == st.VersionCboe && Math.Abs((tick - st.TickVisto).TotalSeconds) < 30 && wall - st.WallMs < _o.RefrescoS * 1000L)
                return;
            st.VersionCboe = ver; st.TickVisto = tick; st.WallMs = wall;

            var l = cboe.Fotos("TQQQ", st.Ses.FotosIniUtc, st.Ses.FotosFinUtc) ?? Array.Empty<FotoCadena>();
            var fo = new List<FotoCadena>(l.Count);
            foreach (var f in l) if (f != null && f.Filas != null && f.Filas.Length > 0) fo.Add(f);   // libros.fotos_cboe: sin filas no hay foto
            Ordenar(fo);
            int n = fo.Count;
            st.Fotos = fo.ToArray();
            st.Gens = new long[n];
            for (int i = 0; i < n; i++) st.Gens[i] = TqqqHora.Ms(st.Fotos[i].GeneradoUtc);
            st.Px = new double[n];
            for (int i = 0; i < n; i++)
            {
                var f = st.Fotos[i];
                double px = PrecioTick(cinta, f.TsUtc.AddSeconds(-RETRASO_S));
                if (double.IsNaN(px) && st.PxGuardado != null && st.PxGuardado.TryGetValue(f.TsUtc.Ticks, out var g)) px = g;
                st.Px[i] = px;
            }
            double sAntes = st.S;
            CalcularS(st, cboe);
            if (!sAntes.Equals(st.S)) _versionS++;   // 4.1.0: el motor rehace todas las velas con la s nueva (IVersionSTqqq)
            CalcularC(st);
            Persistir(st);
            var h = st.Ses.Dia + " s=" + TqqqNum.F(st.S, "0.######") + " " + st.SFuente + " c=" + (st.PrimerCv >= 0);   // solo cambios de s o el primer c
            if (h != st.Huella) { st.Huella = h; _log.Linea("sesion " + st.Ses.Dia + ": " + n + " fotos, " + (double.IsNaN(st.S) ? "sin s todavia" : st.STexto) + (st.PrimerCv >= 0 ? ", c desde la foto de las " + st.Fotos[st.PrimerCv].GeneradoUtc.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " UTC" : ", c sin 5 muestras todavia")); }
        }

        private static void Ordenar(List<FotoCadena> fo)
        {   // por generado, estable (sort de Python); el descargador ya las da asi
            for (int i = 1; i < fo.Count; i++)
                if (fo[i].GeneradoUtc < fo[i - 1].GeneradoUtc)
                {
                    var idx = new int[fo.Count]; for (int j = 0; j < idx.Length; j++) idx[j] = j;
                    var copia = fo.ToArray();
                    Array.Sort(idx, (a, b) => { int c = copia[a].GeneradoUtc.CompareTo(copia[b].GeneradoUtc); return c != 0 ? c : a.CompareTo(b); });
                    fo.Clear(); foreach (var j in idx) fo.Add(copia[j]);
                    return;
                }
        }

        // ---------------------------------------------------------------- s del dia

        private void CalcularS(EstadoSesion st, IFuenteCboe cboe)
        {
            if (st.SFija) return;
            var ses = st.Ses;
            if (_o.SForzada != null && _o.SForzada.TryGetValue(ses.Dia, out var sf) && sf > 0)
            {
                st.S = sf; st.SFija = true; st.SFuente = "forzada";
                st.STexto = "s = " + sf.ToString("0.000", CultureInfo.InvariantCulture) + " (forzada por el arnes, como tqqq_vivo el 08-10 con conversion.json)";
                return;
            }
            if (!ses.Corregida)
            {   // tqqq_vivo.s_del_dia
                var prev = ses.Fecha.Date.AddDays(-1);
                while (prev.DayOfWeek == DayOfWeek.Saturday || prev.DayOfWeek == DayOfWeek.Sunday) prev = prev.AddDays(-1);
                var sp = TqqqSesion.DelDia(prev, false, false);
                var cierre = DateTime.SpecifyKind(prev, DateTimeKind.Utc).AddHours(20);
                double P0 = SpotAlCierre(cboe, "TQQQ", sp, cierre), N0 = SpotAlCierre(cboe, "NDX", sp, cierre);
                if (P0 > 0 && N0 > 0)
                {
                    st.S = N0 / (3.0 * P0); st.P0 = P0; st.N0 = N0; st.SFija = true; st.SFuente = "s_del_dia";
                    st.STexto = string.Format(CultureInfo.InvariantCulture, "s = {0:0.000} (NDX {1:0.00} / (3 x TQQQ {2:0.00}), cierres de la sesion anterior)", st.S, N0, P0);
                    return;
                }
                // recta libre con lo que haya (tqqq_vivo: todas las fotos de la sesion con precio, >= 10), y queda fija
                var xs = new List<double>(); var ys = new List<double>();
                for (int i = 0; i < st.Fotos.Length; i++) if (!double.IsNaN(st.Px[i])) { xs.Add(st.Fotos[i].Spot); ys.Add(st.Px[i]); }
                if (xs.Count >= 10)
                {
                    st.S = TqqqNum.Pendiente(xs, ys); st.SFija = !double.IsNaN(st.S); st.SFuente = "recta libre";
                    st.STexto = "sin cierres de la sesion anterior: s por recta libre (" + TqqqNum.F(st.S, "0.000") + ")";
                }
                return;
            }
            // corregida: prev_day_close de TQQQ y de _NDX de la rueda/premarket de D
            double p0 = double.NaN, n0 = double.NaN; string fuente = "";
            try
            {
                var ct = _o.Cierres?.Invoke("TQQQ", ses.Fecha.Date); var cn = _o.Cierres?.Invoke("NDX", ses.Fecha.Date);
                if (ct != null && cn != null && ct.Anterior > 0 && cn.Anterior > 0) { p0 = ct.Anterior; n0 = cn.Anterior; fuente = "prev_day_close validado por el descargador"; }
            }
            catch (Exception e) { _log.Error("Cierres", e); }
            if (!(p0 > 0 && n0 > 0))
            {
                p0 = PdcDelDia(st.Fotos, ses);
                n0 = PdcDelDia(Lista(cboe.Fotos("NDX", ses.FotosIniUtc, ses.FotosFinUtc)), ses);
                fuente = "prev_day_close de las fotos con dato del dia";
            }
            if (p0 > 0 && n0 > 0)
            {
                st.S = n0 / (3.0 * p0); st.P0 = p0; st.N0 = n0; st.SFija = true; st.SFuente = fuente;
                st.STexto = string.Format(CultureInfo.InvariantCulture, "s = {0:0.000} (NDX {1:0.00} / (3 x TQQQ {2:0.00}), cierre oficial del dia anterior: {3})", st.S, n0, p0, fuente);
                return;
            }
            // respaldo: recta libre solo con muestras de rueda y spot vivo (>= 20), con aviso; no queda fija
            var x2 = new List<double>(); var y2 = new List<double>();
            for (int i = 0; i < st.Fotos.Length; i++)
            {
                var f = st.Fotos[i];
                if (double.IsNaN(st.Px[i]) || !(f.Spot > 0) || !ses.EnRueda(f.TsUtc.AddSeconds(-RETRASO_S)) || !EsVivo(st, i)) continue;
                x2.Add(f.Spot); y2.Add(st.Px[i]);
            }
            if (x2.Count >= 20)
            {
                st.S = TqqqNum.Pendiente(x2, y2); st.SFuente = "recta libre";
                st.STexto = "AVISO: sin cierres oficiales del dia anterior: s por recta libre de la rueda (" + TqqqNum.F(st.S, "0.000") + ", " + x2.Count + " muestras)";
            }
            else { st.S = double.NaN; st.STexto = "sin cierres del dia anterior todavia (prev_day_close de TQQQ y NDX)"; }
        }

        private static IReadOnlyList<FotoCadena> Lista(IReadOnlyList<FotoCadena> l) => l ?? Array.Empty<FotoCadena>();

        /// <summary>tqqq_vivo.s_del_dia.al_cierre: spot de la ULTIMA foto (por generado) de la sesion con t_dato &lt;= cierre. No exige filas: el
        /// descargador deja livianas (sin filas) las fotos de mas de 30 h y el spot alcanza (el Python saltea solo cadenas vacias de verdad).</summary>
        private static double SpotAlCierre(IFuenteCboe cboe, string libro, TqqqSesion sp, DateTime cierre)
        {
            var l = Lista(cboe.Fotos(libro, sp.FotosIniUtc, sp.FotosFinUtc));
            FotoCadena u = null;
            foreach (var f in l) if (f != null && f.DatoUtc <= cierre && (u == null || f.GeneradoUtc >= u.GeneradoUtc)) u = f;
            return u != null && u.Spot > 0 ? u.Spot : double.NaN;
        }

        /// <summary>prev_day_close de la ULTIMA foto cuyo dato (ultimo trade de opciones) es del dia D antes del cierre: en la rueda de D vale el
        /// cierre oficial de D-1. NaN si no hay.</summary>
        private static double PdcDelDia(IReadOnlyList<FotoCadena> l, TqqqSesion ses)
        {
            double v = double.NaN;
            var cierreNy = TqqqHora.CierreNy(ses.Fecha.Date);
            foreach (var f in l)
            {
                if (f == null || !(f.CierreAnterior > 0)) continue;
                var ny = TqqqHora.UtcANy(f.DatoUtc);
                if (ny.Date != ses.Fecha.Date || ny.TimeOfDay >= cierreNy - TimeSpan.FromMinutes(1)) continue;
                v = f.CierreAnterior;
            }
            return v;
        }

        // ---------------------------------------------------------------- c por foto

        private static bool EsVivo(EstadoSesion st, int i)
        {
            double si = st.Fotos[i].Spot;
            for (int j = Math.Max(0, i - VIVO_FOTOS); j < i; j++)
                if (st.Gens[i] - st.Gens[j] <= VIVO_S * 1000L && Math.Abs(st.Fotos[j].Spot - si) > 1e-9) return true;
            return false;
        }

        private void CalcularC(EstadoSesion st)
        {
            int n = st.Fotos.Length;
            st.Muestra = new double[n]; st.Cv = new double[n]; st.N = new int[n]; st.Vivo = new bool[n]; st.Rueda = new bool[n];
            st.PrimerCv = -1; st.UltimaConMuestra = -1;
            var roles = new List<double>(VENTANA + 1);
            for (int i = 0; i < n; i++)
            {
                var f = st.Fotos[i];
                st.Rueda[i] = st.Ses.EnRueda(f.TsUtc.AddSeconds(-RETRASO_S));
                st.Vivo[i] = EsVivo(st, i);
                st.Muestra[i] = double.NaN;
                double px = st.Px[i];
                if (st.Vivo[i] && !double.IsNaN(px) && f.Spot > 0 && st.Rueda[i] && !double.IsNaN(st.S))
                {
                    double m = px - st.S * f.Spot;
                    st.Muestra[i] = m;
                    roles.Add(m);
                    if (roles.Count > VENTANA) roles.RemoveAt(0);
                    st.UltimaConMuestra = i;
                }
                var (v, nn) = TqqqNum.Robusta(roles, PISO);
                st.N[i] = nn;
                st.Cv[i] = nn >= MIN_N ? v : double.NaN;
                if (st.PrimerCv < 0 && !double.IsNaN(st.Cv[i])) st.PrimerCv = i;
            }
        }

        private void Persistir(EstadoSesion st)
        {
            if (string.IsNullOrEmpty(st.RutaMuestras)) return;
            List<string> nuevas = null;
            for (int i = 0; i < st.Fotos.Length; i++)
            {
                var f = st.Fotos[i];
                if (double.IsNaN(st.Px[i]) || st.PxAnotado.Contains(f.TsUtc.Ticks)) continue;
                st.PxAnotado.Add(f.TsUtc.Ticks);
                if (st.PxGuardado.ContainsKey(f.TsUtc.Ticks)) continue;     // ya estaba en el archivo
                st.PxGuardado[f.TsUtc.Ticks] = st.Px[i];
                (nuevas ?? (nuevas = new List<string>())).Add(TqqqArchivo.LineaMuestra(f.TsUtc, f.GeneradoUtc, f.Spot, st.Px[i], st.Muestra[i], st.Vivo[i], st.Rueda[i]));
            }
            if (nuevas != null) try { TqqqArchivo.AnexarMuestras(st.RutaMuestras, nuevas); } catch (Exception e) { _log.Error("AnexarMuestras", e); }
        }

        private void Podar()
        {
            if (string.IsNullOrEmpty(_o.Carpeta) || (DateTime.UtcNow - _podado).TotalHours < 12) return;
            _podado = DateTime.UtcNow;
            try { int n = TqqqArchivo.Podar(_o.Carpeta, _o.RetencionDias); if (n > 0) _log.Linea("poda: " + n + " archivos de muestras de mas de " + _o.RetencionDias + " dias"); }
            catch (Exception e) { _log.Error("Podar", e); }
        }

        /// <summary>tq_comun.precio_en: ultimo tick con t &lt;= ts, ts - t &lt;= 120 s y ts &lt;= ultimo tick; si no, NaN (sin el respaldo de la vela).</summary>
        private static double PrecioTick(ICinta cinta, DateTime tUtc)
        {
            if (cinta == null) return double.NaN;
            var ult = cinta.UltimoTickUtc;
            if (ult == default || tUtc > ult) return double.NaN;
            double p = cinta.PrecioSoloTick(tUtc, EDAD_TICK_S);
            return p > 0 ? p : double.NaN;
        }

        // ================================================================== velas

        private TqqqNivelesFoto NivelesDe(FotoCadena f)
        {
            var clave = (f.TsUtc.Ticks, f.GeneradoUtc.Ticks);
            if (_niveles.TryGetValue(clave, out var nv)) return nv;
            if (_niveles.Count > 4000) _niveles.Clear();
            nv = TqqqPerfil.Niveles(f);
            _niveles[clave] = nv;
            return nv;
        }

        /// <summary>Modo A (la rueda, tqqq_vivo): foto vigente al cierre de la vela con c valido.</summary>
        private TqqqVela VelaRueda(EstadoSesion st, long k, ICinta cinta, out TqqqDetalle det)
        {
            det = null;
            long t = k + NS2;
            int i = TqqqNum.UltimoMenorIgual(st.Gens, st.Gens.Length, t);
            if (i < 0 || t - st.Gens[i] > VIDA_S * 1000L) return null;
            double c = st.Cv[i];
            if (double.IsNaN(c) || double.IsNaN(st.S)) return null;
            var f = st.Fotos[i];
            var v = Armar(k, f, st.S, c, cinta, st.Ses.Congelada(f.GeneradoUtc));
            det = new TqqqDetalle { Modo = "rueda", Banda = BANDA, S = st.S, C = c, Spot = f.Spot, NMuestras = st.N[i], GeneradoUtc = f.GeneradoUtc,
                                    TsUtc = f.TsUtc, DatoUtc = f.DatoUtc, STexto = st.STexto, VencD = NivelesDe(f).VencD };
            _detalles.AddOrUpdate(v, det);
            return v;
        }

        private TqqqVela Armar(long k, FotoCadena f, double s, double c, ICinta cinta, bool congelada)
        {
            var nv = NivelesDe(f);
            var v = new TqqqVela { AperturaM2Ms = k, S = s, C = c, DatoUtc = f.DatoUtc, Congelada = congelada };
            foreach (var serie in TqqqNivelesFoto.Orden)
            {
                if (!nv.Series.TryGetValue(serie, out var lv)) continue;
                var ns = new Nivel[lv.Length]; var ks = new double[lv.Length];
                for (int j = 0; j < lv.Length; j++) { ns[j] = new Nivel(TqqqNum.PyRound(s * lv[j].K + c, 2), lv[j].Rol, lv[j].GexM); ks[j] = lv[j].K; }
                v.Series[serie] = ns; v.Strikes[serie] = ks;
            }
            // T_DOMS_raz (solo para comparar): razon simple al precio del cierre de la vela - 1 s
            if (nv.Series.TryGetValue("T_DOMS_vol", out var doms) && doms.Length > 0)
            {
                double nq = PrecioTick(cinta, TqqqHora.DeMs(k + NS2 - 1000));
                if (!double.IsNaN(nq))
                {
                    double tEq = (nq - c) / s;
                    var ns = new Nivel[doms.Length]; var ks = new double[doms.Length];
                    for (int j = 0; j < doms.Length; j++) { ns[j] = new Nivel(TqqqNum.PyRound(doms[j].K * nq / tEq, 2), doms[j].Rol, doms[j].GexM); ks[j] = doms[j].K; }
                    v.Series["T_DOMS_raz"] = ns; v.Strikes["T_DOMS_raz"] = ks;
                }
            }
            return v;
        }

        // ================================================================== anclas de noche (SIN VALIDAR)

        private void CargarAnclas()
        {
            if (_anclasCargadas) return;
            _anclasCargadas = true;
            try { _anclas.Cargar(); } catch (Exception e) { _log.Error("CargarAnclas", e); }
        }

        /// <summary>Despues del cierre de la sesion de st: completa el ancla de hoy. En la rueda: anota el error de la de ayer contra el primer c.</summary>
        private void MedirAnclas(EstadoSesion st, ICinta cinta, IFuenteCboe cboe)
        {
            CargarAnclas();
            var ses = st.Ses;
            bool cambio = false;
            if (TqqqHora.EsHabil(ses.Fecha.Date) && _maxT > TqqqHora.Ms(ses.CierreUtc)) cambio |= ActualizarAncla(ses.Fecha.Date, st, cinta, cboe);
            if (st.PrimerCv >= 0)
            {
                var dPrev = TqqqHora.HabilAnterior(ses.Fecha.Date);
                var a = _anclas.Ver(TqqqHora.Fecha(dPrev), _contrato);
                if (a == null || double.IsNaN(a.CSig))
                {   // el ancla de anoche (si el indicador no la vio, se intenta rehacer con las fotos y las muestras guardadas)
                    var stP = EstadoDe(TqqqSesion.DelDia(dPrev, true, _o.AnclaNoche), cinta, cboe);
                    cambio |= ActualizarAncla(dPrev, stP, cinta, cboe);
                    cambio |= ErrorAncla(dPrev, st);
                }
            }
            if (cambio) GuardarAnclas();
        }

        private void GuardarAnclas()
        {
            try
            {
                foreach (var k in _anclas.Guardar())
                {
                    var a = _anclas.Ver(k.Split('|')[0], k.Contains("|") ? k.Substring(k.IndexOf('|') + 1) : "");
                    if (a != null) _log.Linea("ancla " + k + ": " + a.Json());
                }
            }
            catch (Exception e) { _log.Error("GuardarAnclas", e); }
        }

        /// <summary>El ancla del cierre de d, completada con lo que haya (estado de la sesion d si se puede rehacer, fotos y cinta).</summary>
        private TqqqAnclaDia AnclaCalculada(DateTime d, ICinta cinta, IFuenteCboe cboe)
        {
            CargarAnclas();
            var sesD = TqqqSesion.DelDia(d, true, _o.AnclaNoche);
            var stD = EstadoDe(sesD, cinta, cboe);
            if (ActualizarAncla(d, stD, cinta, cboe)) GuardarAnclas();
            return _anclas.Ver(TqqqHora.Fecha(d), _contrato);
        }

        /// <summary>Completa el ancla del cierre de d con lo visto hasta "ahora" (= la mayor vela pedida: sin mirar adelante tambien en el
        /// arnes). Cada dato guarda la hora en que se pudo ver. Se rehace como maximo cada RefrescoS segundos salvo foto nueva o 60 s de datos.</summary>
        private bool ActualizarAncla(DateTime d, EstadoSesion stD, ICinta cinta, IFuenteCboe cboe)
        {
            string fecha = TqqqHora.Fecha(d);
            long wall = Environment.TickCount64, ver = cboe.Version;
            if (_anclaWall.TryGetValue(fecha, out var w) && wall - w < _o.RefrescoS * 1000L && _anclaVer.TryGetValue(fecha, out var vv) && vv == ver
                && _anclaT.TryGetValue(fecha, out var tt) && _maxT - tt < 60000L) return false;
            _anclaWall[fecha] = wall; _anclaVer[fecha] = ver; _anclaT[fecha] = _maxT;

            var cierre = TqqqHora.CierreUtc(d);
            var ahora = TqqqHora.DeMs(_maxT);
            if (ahora <= cierre) return false;
            var a = _anclas.Tomar(fecha, _contrato);

            // s y cierres de d-1 (los de la rueda d)
            if (stD.SFija || !double.IsNaN(stD.S))
            {   // la recta libre (sin cierres oficiales) tambien sirve para el P0' implicito, pero queda dicho en la fuente
                a.S = stD.S; a.SFuente = stD.SFuente == "recta libre" ? "recta libre (AVISO: sin cierres oficiales)" : stD.SFuente; a.P0 = stD.P0; a.N0 = stD.N0;
            }
            // c al cierre: el ultimo c valido de la sesion d (despues del cierre no entran muestras: queda congelado)
            for (int i = stD.Cv.Length - 1; i >= 0; i--)
                if (!double.IsNaN(stD.Cv[i]))
                {
                    a.CCierre = stD.Cv[i]; a.CCierreN = stD.N[i];
                    a.CCierreUtc = stD.Fotos[stD.UltimaConMuestra >= 0 ? stD.UltimaConMuestra : i].GeneradoUtc;
                    break;
                }
            // NQ_1600: ultimo tick <= cierre (solo si la cinta lo tiene); si no, la vela del grafico; si no, la vela m2 de la cinta
            if (double.IsNaN(a.Nq1600) || !a.Nq1600Fuente.StartsWith("cinta", StringComparison.Ordinal))
            {
                double px = PrecioTick(cinta, cierre);
                if (!double.IsNaN(px)) { a.Nq1600 = px; a.Nq1600Fuente = "cinta: ultimo tick <= cierre NY"; }
                else if (double.IsNaN(a.Nq1600))
                {
                    double g = double.NaN;
                    try { g = _o.CierreVelaGrafico?.Invoke(cierre) ?? double.NaN; } catch (Exception e) { _log.Error("CierreVelaGrafico", e); }
                    if (g > 0) { a.Nq1600 = g; a.Nq1600Fuente = "vela del grafico que termina en el cierre"; }
                    else
                    {
                        double cc = (TqqqHora.Ms(cierre) % NS2 == 0) ? cinta.CierreConocido(cierre) : double.NaN;
                        if (cc > 0) { a.Nq1600 = cc; a.Nq1600Fuente = "vela m2 de la cinta que termina en el cierre"; }
                    }
                }
            }
            // N0': spot de _NDX congelado (primera foto con sello >= cierre + 15 min cuyo spot se repite en la siguiente)
            var ndx = Lista(cboe.Fotos("NDX", cierre.AddMinutes(15), ahora.AddTicks(1)));
            if (double.IsNaN(a.N0p))
            {
                for (int i = 0; i + 1 < ndx.Count; i++)
                {
                    var f = ndx[i]; var g = ndx[i + 1];
                    if (f == null || g == null || f.TsUtc < cierre.AddMinutes(15) || !(f.Spot > 0)) continue;
                    if (Math.Abs(g.Spot - f.Spot) <= 1e-9) { a.N0p = f.Spot; a.N0pUtc = g.GeneradoUtc; a.N0pFuente = "_NDX congelado (2 fotos iguales con sello >= cierre + 15 min)"; break; }
                }
                if (double.IsNaN(a.N0p))
                    try
                    {
                        var cn = _o.Cierres?.Invoke("NDX", d);
                        if (cn != null && cn.Close > 0 && cn.CloseUtc != default) { a.N0p = cn.Close; a.N0pUtc = cn.CloseUtc; a.N0pFuente = "data.close de _NDX (descargador)"; }
                    }
                    catch (Exception e) { _log.Error("Cierres", e); }
            }
            if (double.IsNaN(a.N0pPdc) && a.N0 > 0)
                foreach (var f in ndx)
                    if (f != null && f.CierreAnterior > 0 && Math.Abs(f.CierreAnterior - a.N0) > 1e-9) { a.N0pPdc = f.CierreAnterior; a.N0pPdcUtc = f.GeneradoUtc; break; }
            // P0' oficial: prev_day_close de TQQQ una vez que CBOE lo cambio
            if (double.IsNaN(a.P0pOficial))
            {
                try
                {
                    var ct = _o.Cierres?.Invoke("TQQQ", d);
                    if (ct != null && ct.PdcNuevo > 0 && ct.PdcNuevoUtc != default) { a.P0pOficial = ct.PdcNuevo; a.P0pOficialUtc = ct.PdcNuevoUtc; a.P0pFuente = "prev_day_close nuevo (descargador)"; }
                    else
                    {
                        var cs = _o.Cierres?.Invoke("TQQQ", HabilSiguiente(d));
                        if (cs != null && cs.Anterior > 0 && cs.AnteriorUtc != default) { a.P0pOficial = cs.Anterior; a.P0pOficialUtc = cs.AnteriorUtc; a.P0pFuente = "prev_day_close validado del dia siguiente (descargador)"; }
                    }
                    if (ct != null && ct.Close > 0) { a.P0pClose = ct.Close; a.P0pCloseUtc = ct.CloseUtc; }
                }
                catch (Exception e) { _log.Error("Cierres", e); }
            }
            if (double.IsNaN(a.P0pOficial))
            {
                var tq = Lista(cboe.Fotos("TQQQ", cierre.AddMinutes(15), ahora.AddTicks(1)));
                if (a.P0 > 0)
                {
                    foreach (var f in tq)
                        if (f != null && f.CierreAnterior > 0 && Math.Abs(f.CierreAnterior - a.P0) > 1e-9)
                        { a.P0pOficial = f.CierreAnterior; a.P0pOficialUtc = f.GeneradoUtc; a.P0pFuente = "prev_day_close de TQQQ cambio (fotos)"; break; }
                }
                else if (a.N0p > 0)
                {   // SUPUESTO: TQQQ y NDX cambian el prev_day_close a la misma hora
                    DateTime tx = default;
                    foreach (var f in ndx) if (f != null && f.CierreAnterior > 0 && Math.Abs(f.CierreAnterior - a.N0p) <= 0.5) { tx = f.GeneradoUtc; break; }
                    if (tx != default)
                        foreach (var f in tq)
                            if (f != null && f.GeneradoUtc >= tx && f.CierreAnterior > 0)
                            { a.P0pOficial = f.CierreAnterior; a.P0pOficialUtc = f.GeneradoUtc; a.P0pFuente = "prev_day_close de TQQQ (supuesto: cambia junto con el de NDX)"; break; }
                }
            }
            return true;
        }

        private static DateTime HabilSiguiente(DateTime d)
        {
            var x = d.Date.AddDays(1);
            for (int i = 0; i < 10 && !TqqqHora.EsHabil(x); i++) x = x.AddDays(1);
            return x;
        }

        /// <summary>Error de B y de C (anclas del cierre de d) contra el primer c medido de la rueda siguiente, en K = P0' (oficial o implicito).</summary>
        private bool ErrorAncla(DateTime d, EstadoSesion stSig)
        {
            var a = _anclas.Ver(TqqqHora.Fecha(d), _contrato);
            if (a == null || !double.IsNaN(a.CSig) || stSig.PrimerCv < 0 || double.IsNaN(stSig.S)) return false;
            if (double.IsNaN(a.N0p) || double.IsNaN(a.Nq1600)) return false;
            double k = a.P0pOficial > 0 ? a.P0pOficial : a.P0pImpl;
            if (!(k > 0)) return false;
            int i = stSig.PrimerCv;
            a.FechaSig = stSig.Ses.Dia; a.SSig = stSig.S; a.CSig = stSig.Cv[i]; a.CSigUtc = stSig.Fotos[i].GeneradoUtc;
            a.KErr = k;
            double real = a.SSig * k + a.CSig;
            if (a.P0pImpl > 0) a.ErrB = a.SPrima(a.P0pImpl) * k + a.CPrima - real;
            if (a.P0pOficial > 0) a.ErrC = a.SPrima(a.P0pOficial) * k + a.CPrima - real;
            return true;
        }

        /// <summary>Una vela con el ancla del cierre (B o C). despuesDelCierre: velas de la misma sesion (c del cierre sin mirar adelante).</summary>
        private TqqqVela VelaAncla(TqqqAnclaDia a, EstadoSesion st, long k, ICinta cinta, IFuenteCboe cboe, bool despuesDelCierre)
        {
            if (a == null) return null;
            long t = k + NS2; var tU = TqqqHora.DeMs(t);
            if (!(a.N0p > 0) || a.N0pUtc > tU || !(a.Nq1600 > 0)) return null;
            bool modoC = a.P0pOficial > 0 && a.P0pOficialUtc <= tU;
            double cCierre = a.CCierre;
            if (despuesDelCierre)
            {   // el c del cierre visto hasta t (sin mirar adelante)
                int i = TqqqNum.UltimoMenorIgual(st.Gens, st.Gens.Length, t);
                for (; i >= 0; i--) if (!double.IsNaN(st.Cv[i])) break;
                if (i >= 0) cCierre = st.Cv[i];
            }
            double sD = a.S > 0 ? a.S : (despuesDelCierre ? st.S : double.NaN);   // nunca la s de OTRA sesion
            double p0p;
            if (modoC) p0p = a.P0pOficial;
            else
            {
                if (double.IsNaN(cCierre) || !(sD > 0)) return null;
                p0p = (a.Nq1600 - cCierre) / sD;
            }
            if (!(p0p > 0)) return null;
            double s2 = a.N0p / (3.0 * p0p), c2 = a.Nq1600 - a.N0p / 3.0;
            var f = UltimaFoto(cboe, st, tU);
            if (f == null) return null;
            var v = Armar(k, f, s2, c2, cinta, true);
            var det = new TqqqDetalle
            {
                Modo = modoC ? "ancla C (cierre oficial)" : "ancla B (cierre implicito)", Banda = modoC ? BANDA_C : BANDA_B, S = s2, C = c2, Spot = f.Spot,
                GeneradoUtc = f.GeneradoUtc, TsUtc = f.TsUtc, DatoUtc = f.DatoUtc, VencD = NivelesDe(f).VencD, STexto = st.STexto,
                AnclaTexto = string.Format(CultureInfo.InvariantCulture, "cierre del {0}: TQQQ {1:0.0000} ({2}), NDX {3:0.00}, MNQ {4:0.00} ({5})", a.Fecha, p0p,
                                           modoC ? a.P0pFuente : "implicito = (NQ_1600 - c " + cCierre.ToString("0.0", CultureInfo.InvariantCulture) + ") / s " + sD.ToString("0.000", CultureInfo.InvariantCulture),
                                           a.N0p, a.Nq1600, a.Nq1600Fuente),
            };
            _detalles.AddOrUpdate(v, det);
            return v;
        }

        /// <summary>La ultima foto de TQQQ con generado &lt;= t que sigue vigente: 1500 s, o si la cadena esta congelada (dato >= 15:59 NY)
        /// hasta las 09:30 NY siguientes (C9 de la familia, en hora NY).</summary>
        private FotoCadena UltimaFoto(IFuenteCboe cboe, EstadoSesion st, DateTime tU)
        {
            FotoCadena f = null;
            long t = TqqqHora.Ms(tU);
            int i = TqqqNum.UltimoMenorIgual(st.Gens, st.Gens.Length, t);
            if (i >= 0) f = st.Fotos[i];
            else
            {
                var l = Lista(cboe.Fotos("TQQQ", tU.AddHours(-20), tU.AddTicks(1)));
                for (int j = l.Count - 1; j >= 0; j--) if (l[j] != null && l[j].Filas != null && l[j].Filas.Length > 0) { f = l[j]; break; }
            }
            if (f == null) return null;
            double edad = (tU - f.GeneradoUtc).TotalSeconds;
            if (edad <= VIDA_S) return f;
            var nyDato = TqqqHora.UtcANy(f.DatoUtc);
            bool congelada = nyDato.TimeOfDay >= TqqqHora.CierreNy(nyDato.Date) - TimeSpan.FromMinutes(1);
            if (!congelada) return null;
            var nyGen = TqqqHora.UtcANy(f.GeneradoUtc);
            var prox = TqqqHora.NyAUtc(nyGen.Date.AddHours(9).AddMinutes(30));
            if (prox <= f.GeneradoUtc) prox = TqqqHora.NyAUtc(nyGen.Date.AddDays(1).AddHours(9).AddMinutes(30));
            return tU < prox ? f : null;
        }

        private static long Piso(long a, long b) { long q = a / b; if (a % b != 0 && (a < 0) != (b < 0)) q--; return q * b; }
    }
}
