// MotorFamilia.cs — PythiaGex 4.1, modulo fam (B3d), 08-10-2026. Implementa IMotorFamilia (Contratos.cs).
// Port de preview_niveles.Motor (ciclo / historico / calcular / salida) sobre los modulos por libro (ILibroMinutero nq/ndx/qqq), la cinta
// (ICinta), TQQQ (ICalculoTqqq) y lo que dibuja el motor 3.0 del clon (AlmacenTres). Una sesion por vez; cambia sola a las 18:00 NY
// (corregida) o a las 22:00 UTC (paridad).
//
// Grilla por minuto (preview_niveles.calcular): fut = ICinta.CierreConocido(t); por libro LibroMinuto (null = no vale); ReglasFam.Minuto.
// Tandas: como la vista previa, que reinicia el corrimiento de NQ y la cache del zero estandar en CADA llamada a calcular:
//   * al ponerse al dia (arranque o hueco): tandas de 240 minutos alineadas al inicio de la sesion (historico: range(ini, k1) de a 240);
//   * en vivo: una tanda por minuto nuevo (ciclo: k0..k_ahora).
//   Un minutero que tenga caches por tanda implementa ITandaMinutero y recibe NuevaTanda antes de cada una (opcional; propuesto).
// Un minuto se calcula a los RetrasoMinutoS (6 s) de empezar, como el bucle de la vista previa (segundo ~6 del minuto).
// Los minutos ya calculados no se recalculan (persistidos en PythiaGex4\familia\niv-<sesion>-<contrato>.jsonl).
// Arranque: se espera a que las fuentes tengan datos (cboe.Version > 0 y nq.Version > 0) o EsperaArranqueS, para no anotar minutos
// pasados "sin libros" solo porque el historico de las fuentes todavia no termino de cargar.
// Hilos: Avanzar corre en UN hilo de fondo (el timer de la Familia); Estela() puede llamarse desde el hilo de ATAS; Foto se lee desde el
// render con Volatile.Read (FotoFamilia inmutable; HistoriaM2 es un ImmutableDictionary que se actualiza por vela).
// Sin referencias a ATAS. Todo con try/catch y log propio (pythiagex4-familia.log, una linea por minuto y lugar como maximo).
//
// Conexion (la hace el integrador; nombres de B1/B2/B3a-c tal como estan hoy):
//   var motor = new MotorFamilia(new OpcionesMotorFamilia {
//         Carpeta = AdaptadoresFamilia.Carpeta,                      // %APPDATA%\ATAS\PythiaGex4 (familia\ y estela\ debajo)
//         Corregida = !AdaptadoresFamilia.SesionUtcFija,             // mismo modo que los minuteros (OpcionesNdx.ParidadPython, OpcionesQqq.Corregida)
//         TqqqDeNoche = Tqqq41AnclaNoche,
//         FuentesListas = () => bajador.HistoriaCargada && libroNq.Sembrado && cinta.Version > 0 });
//   motor.Configurar(cinta, libroNq, cboe, new ILibroMinutero[] { minNq, minNdx, minQqq }, tqqq, "MNQZ6" /* codigo del grafico */);
//   AdaptadoresFamilia.AlEstela = motor.Estela;                    // TRES_* al instante (sin el gancho igual se leen de PythiaGex4\estela)
//   hilo "PythiaGex4 familia" (BelowNormal): motor.Avanzar(DateTime.UtcNow) cada 5 s; si motor.Pendiente, otra vez a los 300 ms.
//   render: motor.Foto (inmutable) y motor.Catalogo (CatalogoFamilia.Series).
// TRES_* = las estelas que escribe el MISMO clon (GuardarEstela de GammaHoyTres para NQ y de GammaHoyTresCapas para NDX/QQQ, con
// Guardar3Estela = true y Capa3QQQ/Capa3NDX = Propia, sus defaults) en PythiaGex4\estela: nada de PythiaGex3.
// 4.1.3 (09-10-2026): OpcionesMotorFamilia.Extras (IExtrasMinuto, la Replica20) agrega las series "como la 2.0" a cada minuto DESPUES de
// ReglasFam.Minuto (las 21 no cambian); los minutos del archivo sin extras se completan en memoria al recorrerlos. Sin Extras = 4.1.2 exacto.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace PythiaGexCuatro.Familia
{
    /// <summary>Opcional (PROPUESTA de B3d, no esta en Contratos.cs): un ILibroMinutero con caches "por llamada" (corrimiento de NQ, cache del
    /// zero estandar) la reinicia aca para reproducir la vista previa. primera..ultima = las claves de la tanda; ahoraUtc = el reloj.</summary>
    public interface ITandaMinutero
    {
        void NuevaTanda(DateTime ahoraUtc, long primeraClave, long ultimaClave);
    }

    /// <summary>4.1.3 (09-10-2026): series EXTRA de un minuto (CatalogoFamilia.Extra: las dominantes como la 2.0), calculadas DESPUES de la cuenta
    /// de la vista previa y guardadas aparte (RegistroMinuto.Extra / ExtraStrikes / MetaExtra). No pueden tocar r.Series ni los libros bk (solo
    /// leerlos): las 21 series, CONF y FAM quedan identicas con o sin extras. Las llama el hilo del motor.</summary>
    public interface IExtrasMinuto
    {
        /// <summary>El minuto recien calculado: r = ReglasFam.Minuto(clave, bk); fut = el del minuto (ICinta.CierreConocido).</summary>
        void Calcular(RegistroMinuto r, DateTime tUtc, double fut, IReadOnlyList<LibroMinuto> bk);
        /// <summary>Un minuto cargado del archivo SIN extras (guardado por una version anterior o sin poder calcularlas): completar lo que se pueda
        /// sin los libros del minuto (que ya no estan). true si agrego algo. No se persiste: un reinicio lo vuelve a completar igual.</summary>
        bool Completar(RegistroMinuto r, DateTime tUtc);
    }

    /// <summary>4.1.4 (revision 4.1.3, opcional): un IExtrasMinuto cuyo resultado para minutos YA calculados puede cambiar despues (la razon de la 2.0
    /// de una cadena que se escalo sin la vela de la cinta y la vela llego despues). Cuando Generacion sube, el motor llama Rehacer en los minutos
    /// calculados de la sesion desde RehacerDesdeUtc, vuelve a guardar los que cambiaron (una linea nueva por minuto: al leer gana la ultima) y
    /// rehace su historia. Las 21 series no se tocan.</summary>
    public interface IExtrasRehacer
    {
        long Generacion { get; }
        DateTime RehacerDesdeUtc { get; }
        /// <summary>Rehace en r solo lo que depende de lo que cambio. true si cambio algo.</summary>
        bool Rehacer(RegistroMinuto r, DateTime tUtc);
    }

    public sealed class OpcionesMotorFamilia
    {
        /// <summary>%APPDATA%\ATAS\PythiaGex4 (adentro: familia\ y estela\). null/"" = sin persistencia ni lectura de estelas de archivo.</summary>
        public string Carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex4");
        public bool Corregida = true;              // sesion en hora NY (false = paridad con la vista previa: 22:00-21:00 UTC fijo)
        public bool Persistir = true;
        public int TandaMinutos = 240;             // preview_niveles.historico
        public int RetrasoMinutoS = 6;             // el minuto T se calcula en T + 6 s
        /// <summary>4.1.0: en vivo, si a T + 6 s todavia no llego ningun tick desde la apertura de la vela m2 en curso, la vela anterior no esta
        /// "cerrada" y el fut del minuto saldria de la vela de 2 min antes (distinto del historico). Se espera ese tick hasta T + EsperaCierreS;
        /// pasado ese tiempo se calcula igual (mercado quieto o cinta caida: como antes). 0 = no esperar (la regla de la vista previa en vivo).</summary>
        public int EsperaCierreS = 60;
        public int PresupuestoMs = 2500;           // tope de trabajo por llamada a Avanzar (despues sigue en la proxima)
        public int EsperaArranqueS = 90;           // espera maxima a que las fuentes carguen su historico antes de calcular el pasado
        /// <summary>Opcional (lo pone el integrador): true cuando las fuentes ya cargaron su historico, p. ej.
        /// () => bajador.HistoriaCargada &amp;&amp; libroNq.Sembrado &amp;&amp; cinta.Version > 0. Sin esto: CBOE con Version > 0 y sin "leyendo historia"
        /// en su Estado, libro NQ con Version > 0 y cinta con algun tick; o EsperaArranqueS.</summary>
        public Func<bool> FuentesListas;
        public bool TqqqDeNoche = false;           // Tqqq41AnclaNoche: pedir velas de TQQQ desde el inicio de la sesion (si no, desde la rueda)
        public int RetencionDias = 14;
        public string RutaLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "pythiagex4-familia.log");
        /// <summary>4.1.3: las series extra (las dominantes como la 2.0, Replica20). null = sin extras: el motor queda EXACTO como en la 4.1.2
        /// (los arneses de paridad lo construyen asi). El indicador (HostFamilia) pasa la Replica20.</summary>
        public IExtrasMinuto Extras;
    }

    public sealed class MotorFamilia : IMotorFamilia
    {
        public const string VERSION = "fam 4.1.4 (09-10-2026)";
        private readonly OpcionesMotorFamilia _op;
        private readonly object _paso = new object();

        private ICinta _cinta; private ILibroNq _nq; private IFuenteCboe _cboe; private ICalculoTqqq _tqqq;
        private ILibroMinutero[] _libros = Array.Empty<ILibroMinutero>();
        private string _contrato = "";
        private DateTime _configuradoUtc = DateTime.MinValue;
        private bool _configurado;

        private SesionFamilia _ses;
        private AlmacenNiveles _alm;
        private AlmacenTres _tres;
        private long _siguiente, _finTanda;
        private bool _listoFuentes;
        private readonly Dictionary<long, TqqqVela> _tq = new Dictionary<long, TqqqVela>();
        private long _tqVersionCboe = -1; private DateTime _tqPasadaUtc = DateTime.MinValue;
        private long _tqVersionS = long.MinValue;
        private long _ultimoEsperado = long.MinValue;
        private ImmutableDictionary<long, IReadOnlyDictionary<string, double[]>> _hist = ImmutableDictionary<long, IReadOnlyDictionary<string, double[]>>.Empty;
        private long _tresVersion;
        private FotoFamilia _foto = new FotoFamilia { CalculadoUtc = DateTime.MinValue, HistoriaM2 = ImmutableDictionary<long, IReadOnlyDictionary<string, double[]>>.Empty,
                                                       Actuales = Array.Empty<NivelActual>(), Fuentes = Array.Empty<FuenteEstado>(), Aviso = "sin configurar" };

        // estadistica (pestaña y arnes)
        public long MinutosCalculados { get; private set; }
        public long LlamadasMinutero { get; private set; }
        public int TandasIniciadas { get; private set; }
        public bool Pendiente { get; private set; }
        /// <summary>4.1.0: el minuto en vivo espera el primer tick de la vela m2 en curso (ver EsperaCierreS). El hilo puede mirar cada 1 s.</summary>
        public bool EsperandoCierre { get; private set; }
        public long MinutosEsperados { get; private set; }
        public int PasadasTqqqPorS { get; private set; }
        /// <summary>4.1.3: minutos del archivo a los que se les completaron las series extra en esta corrida.</summary>
        public long MinutosCompletados { get; private set; }
        /// <summary>4.1.4: minutos ya calculados cuyas series extra se rehicieron (IExtrasRehacer: la razon de la 2.0 cambio) y veces que paso.</summary>
        public long MinutosRehechos { get; private set; }
        public int Rehechuras { get; private set; }
        private long _genExtras;
        /// <summary>4.1.0: lo pone el host al parar. Avanzar deja de calcular minutos (y velas de TQQQ) en el proximo paso, sin esperar la tanda:
        /// Parar ya no espera 2,5 s de puesta al dia. Lo ya calculado se guarda igual (las fuentes siguen vivas hasta que el hilo termina).</summary>
        public volatile bool Detener;
        public double MsUltimoAvanzar { get; private set; }
        public SesionFamilia Sesion => _ses;
        public AlmacenNiveles Almacen => _alm;
        public AlmacenTres Tres => _tres;
        public IReadOnlyDictionary<long, TqqqVela> VelasTqqq => _tq;

        public MotorFamilia() : this(new OpcionesMotorFamilia()) { }
        public MotorFamilia(OpcionesMotorFamilia op) { _op = op ?? new OpcionesMotorFamilia(); }

        public FotoFamilia Foto => Volatile.Read(ref _foto);
        public IReadOnlyList<SerieInfo> Catalogo => CatalogoFamilia.Series;

        private string CarpetaFamilia => string.IsNullOrEmpty(_op.Carpeta) ? null : Path.Combine(_op.Carpeta, "familia");
        private string CarpetaEstela => string.IsNullOrEmpty(_op.Carpeta) ? null : Path.Combine(_op.Carpeta, "estela");

        public void Configurar(ICinta cinta, ILibroNq nq, IFuenteCboe cboe, IReadOnlyList<ILibroMinutero> libros, ICalculoTqqq tqqq, string contrato)
        {
            lock (_paso)
            {
                _cinta = cinta; _nq = nq; _cboe = cboe; _tqqq = tqqq;
                var orden = new List<ILibroMinutero>();
                if (libros != null)
                {   // el orden de bk de la vista previa: NQ, NDX, QQQ (y cualquier otro al final)
                    foreach (var lb in CatalogoFamilia.Libros) foreach (var m in libros) if (m != null && m.Libro == lb) orden.Add(m);
                    foreach (var m in libros) if (m != null && !orden.Contains(m)) orden.Add(m);
                }
                _libros = orden.ToArray();
                bool otroContrato = (contrato ?? "") != _contrato;
                _contrato = contrato ?? "";
                _configuradoUtc = DateTime.UtcNow;
                _configurado = true;
                if (otroContrato) _ses = null;          // roll o primer arranque: se abre la sesion de nuevo con el archivo de este contrato
                BitacoraFam.Linea(_op.RutaLog, "configurar", VERSION + ": contrato " + _contrato + ", libros " + string.Join(",", _libros.Select(x => x.Libro)) +
                          (_tqqq != null ? ", TQQQ" : "") + (_cboe != null ? ", CBOE" : "") + ", modo " + (_op.Corregida ? "corregido (NY)" : "paridad (UTC fijo)")
                          + (_op.Extras != null ? ", series extra " + string.Join("/", CatalogoFamilia.Extra) : ""));
            }
        }

        /// <summary>Gancho del clon (GuardarEstela): una linea de estela con el MISMO texto que va al archivo. Cualquier hilo.</summary>
        public void Estela(string capa, string linea)
        {
            try { Volatile.Read(ref _tres)?.Agregar(capa, linea); } catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "Estela", e); }
        }

        // ------------------------------------------------------------------ sesion
        private void AbrirSesion(SesionFamilia s, DateTime ahoraUtc)
        {
            _ses = s;
            _alm = new AlmacenNiveles(_op.Persistir ? CarpetaFamilia : null, s.Dia, _contrato, s.Corregida);
            int cargados = 0;
            try { cargados = _alm.Cargar(); } catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "Cargar", e); }
            if (_op.Persistir && CarpetaFamilia != null) AlmacenNiveles.Podar(CarpetaFamilia, _op.RetencionDias);
            Volatile.Write(ref _tres, new AlmacenTres(s.IniUtc, s.FinUtc));
            int lt = 0;
            try { lt = _tres.LeerArchivos(CarpetaEstela, s.Dia); } catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "Estelas", e); }
            _tresVersion = -1;
            _siguiente = s.IniClave; _finTanda = long.MinValue;
            _tq.Clear(); _tqVersionCboe = -1; _tqPasadaUtc = DateTime.MinValue; _tqVersionS = long.MinValue;
            _hist = ImmutableDictionary<long, IReadOnlyDictionary<string, double[]>>.Empty;
            _listoFuentes = false;
            _genExtras = (_op.Extras as IExtrasRehacer)?.Generacion ?? 0;          // 4.1.4: lo que cambie desde ahora se rehace
            BitacoraFam.Linea(_op.RutaLog, "sesion", "sesion " + s + ": " + cargados + " minutos de " + (_alm.Ruta ?? "(sin archivo)") + ", " + lt + " lineas de estela");
        }

        // ------------------------------------------------------------------ el paso
        public void Avanzar(DateTime ahoraUtc)
        {
            if (!Monitor.TryEnter(_paso)) return;                       // el paso anterior sigue: no se encima
            var sw = Stopwatch.StartNew();
            try
            {
                if (!_configurado) return;
                ahoraUtc = DateTime.SpecifyKind(ahoraUtc, DateTimeKind.Utc);
                var s = SesionFamilia.De(ahoraUtc, _op.Corregida);
                bool todo = false;
                if (_ses == null || s.Dia != _ses.Dia) { AbrirSesion(s, ahoraUtc); todo = true; }

                // estelas del motor 3.0 (cola del archivo; el gancho ya las pudo haber puesto)
                try { _tres.LeerArchivos(CarpetaEstela, _ses.Dia); } catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "Estelas", e); }
                long vt = 0; foreach (var c in CatalogoFamilia.CapasTres) vt += _tres.Version(c);
                long dTres = _tresVersion < 0 ? long.MaxValue : vt - _tresVersion;
                _tresVersion = vt;

                // esperar a que las fuentes carguen su historico (para no anotar el pasado sin libros)
                if (!_listoFuentes) _listoFuentes = FuentesListas();

                // ---- minutos
                var nuevos = new List<RegistroMinuto>();
                long primeraNueva = long.MaxValue, ultimaNueva = long.MinValue;
                long limite = Math.Min(Clave(ahoraUtc.AddSeconds(-_op.RetrasoMinutoS)), _ses.FinClave - 1);
                bool esperando = false;
                if (_listoFuentes)
                {
                    while (!Detener && _siguiente <= limite && sw.ElapsedMilliseconds < _op.PresupuestoMs)
                    {
                        long k = _siguiente;
                        if (_alm.Calculado(k))
                        {   // 4.1.3: un minuto del archivo sin las series extra (guardado por la 4.1.2): se completan en memoria (sin persistir)
                            if (CompletarExtras(k)) { primeraNueva = Math.Min(primeraNueva, k); ultimaNueva = Math.Max(ultimaNueva, k); MinutosCompletados++; }
                            _siguiente++; continue;
                        }
                        if (EsperarCierre(k, ahoraUtc)) { esperando = true; break; }   // 4.1.0: el minuto en vivo espera el cierre de la vela m2 anterior
                        if (k >= _finTanda)
                        {
                            long pend = limite - k + 1;
                            long alineado = _ses.IniClave + _op.TandaMinutos * ((k - _ses.IniClave) / _op.TandaMinutos + 1);
                            _finTanda = pend <= 2 ? limite + 1 : Math.Min(limite + 1, alineado);
                            TandasIniciadas++;
                            foreach (var m in _libros)
                                if (m is ITandaMinutero tm)
                                    try { tm.NuevaTanda(ahoraUtc, k, _finTanda - 1); } catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "NuevaTanda " + m.Libro, e); }
                        }
                        var r = CalcularMinuto(k);
                        _alm.Poner(k, r);
                        if (r != null && r.TieneLibros) nuevos.Add(r);
                        primeraNueva = Math.Min(primeraNueva, k); ultimaNueva = Math.Max(ultimaNueva, k);
                        MinutosCalculados++;
                        _siguiente++;
                    }
                    if (nuevos.Count > 0 && _op.Persistir)
                        try { _alm.Anexar(nuevos); } catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "Anexar", e); }
                    // 4.1.4 (revision 4.1.3): las series extra de minutos YA calculados que cambiaron (la razon de la 2.0 de una cadena escalada sin la
                    // vela de la cinta, que aparecio despues): se rehacen, se vuelven a guardar y su historia se rehace (las 21 series no se tocan)
                    if (!Detener && _op.Extras is IExtrasRehacer rh)
                    {
                        long gen = 0; DateTime desdeUtc = DateTime.MaxValue;
                        try { gen = rh.Generacion; desdeUtc = rh.RehacerDesdeUtc; } catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "Extras.Generacion", e); gen = _genExtras; }
                        if (gen != _genExtras)
                        {
                            _genExtras = gen;
                            long k0 = desdeUtc == DateTime.MaxValue ? long.MaxValue : Math.Max(_ses.IniClave, Clave(desdeUtc));
                            var rehechos = new List<RegistroMinuto>();
                            foreach (var r in _alm.Todos.Where(x => x.Clave >= k0 && (x.ExtraIntentado || x.TieneExtra)).OrderBy(x => x.Clave).ToList())
                            {
                                bool cambio = false;
                                try { cambio = rh.Rehacer(r, SesionFamilia.DeClave(r.Clave)); } catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "Extras.Rehacer", e); }
                                if (!cambio) continue;
                                rehechos.Add(r); primeraNueva = Math.Min(primeraNueva, r.Clave); ultimaNueva = Math.Max(ultimaNueva, r.Clave);
                            }
                            Rehechuras++; MinutosRehechos += rehechos.Count;
                            if (rehechos.Count > 0 && _op.Persistir)
                                try { _alm.Anexar(rehechos); } catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "Anexar (rehechos)", e); }
                            BitacoraFam.Linea(_op.RutaLog, "extras", "generacion " + gen + " de las series extra: " + rehechos.Count + " minutos rehechos desde "
                                              + (k0 == long.MaxValue ? "-" : SesionFamilia.DeClave(k0).ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) + "Z") + " (y vueltos a guardar)");
                        }
                    }
                }
                Pendiente = _siguiente <= limite && !esperando && !Detener;
                EsperandoCierre = esperando;

                // ---- TQQQ por vela m2
                long formando = Ms(ahoraUtc) / 120000L * 120000L;
                long tqIni = Ms(_op.TqqqDeNoche ? _ses.IniUtc : _ses.RuedaIniUtc) / 120000L * 120000L;
                long tqFin = Math.Min(formando, Ms(_ses.SiguienteIniUtc) - 120000L);
                var sucias = new HashSet<long>();
                if (_tqqq != null && _listoFuentes && !Detener)
                {
                    // una foto nueva de CBOE cambia solo las velas desde su 'generado' (vale 1500 s): se rehacen las ultimas 15 velas (30 min);
                    // cada 10 min (o al abrir la sesion) una pasada completa por si el historico del descargador llego tarde
                    long vc = _cboe?.Version ?? 0;
                    bool total = todo || (vc != _tqVersionCboe && (DateTime.UtcNow - _tqPasadaUtc).TotalSeconds >= 600);
                    bool cambio = vc != _tqVersionCboe;
                    _tqVersionCboe = vc;
                    // 4.1.0: la s del dia es UNA para toda la rueda. Con la s de respaldo (recta libre, sin cierres oficiales: AVISO) se re-estima
                    // en cada refresco; las velas ya hechas quedaban con la s anterior hasta la pasada de 10 min (la historia se repintaba y no
                    // coincidia con un recalculo). Si la s cambio (IVersionSTqqq), se rehacen TODAS las velas con la s nueva; y si cambia durante
                    // la pasada, otra vez (tope 3).
                    var verS = _tqqq as IVersionSTqqq;
                    for (int pasada = 0; pasada < 3 && !Detener; pasada++)
                    {
                        long vs0 = verS?.VersionS ?? 0;
                        if (vs0 != _tqVersionS && !total) { total = true; if (_tqVersionS != long.MinValue) PasadasTqqqPorS++; }
                        long desde = total ? tqIni : Math.Max(tqIni, tqFin - (cambio ? 15 : 2) * 120000L);
                        if (total) _tqPasadaUtc = DateTime.UtcNow;
                        for (long T = desde; T <= tqFin && !Detener; T += 120000L)
                        {
                            TqqqVela v = null;
                            try { v = _tqqq.Vela(T, _cinta, _cboe); } catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "TQQQ", e); }
                            bool tenia = _tq.TryGetValue(T, out var vieja);
                            if (v == null) { if (tenia) { _tq.Remove(T); sucias.Add(T); } continue; }
                            if (!tenia || !MismaVela(vieja, v)) { _tq[T] = v; sucias.Add(T); }
                        }
                        _tqVersionS = vs0;
                        if ((verS?.VersionS ?? 0) == vs0) break;   // la s no cambio durante la pasada: todas las velas tienen la misma
                        total = false;                               // cambio adentro de la pasada: la proxima vuelta la ve distinta y rehace todo
                    }
                }

                // ---- historia por vela m2 (solo las velas que cambiaron)
                long histIni = Ms(_ses.IniUtc) / 120000L * 120000L;
                long histFin = Math.Min(formando, Ms(_ses.SiguienteIniUtc) - 120000L);
                if (todo || dTres > 20)
                    for (long T = histIni; T <= histFin; T += 120000L) sucias.Add(T);
                else
                {
                    for (long T = Math.Max(histIni, histFin - 2 * 120000L); T <= histFin; T += 120000L) sucias.Add(T);   // TRES y la vela en curso
                    if (ultimaNueva != long.MinValue)
                        for (long T = primeraNueva * 60000L / 120000L * 120000L; T <= Math.Min(histFin, (ultimaNueva + 1) * 60000L); T += 120000L) sucias.Add(T);
                }
                if (sucias.Count > 0)
                {
                    var b = _hist.ToBuilder();
                    foreach (var T in sucias)
                    {
                        if (T < histIni || T > histFin) continue;
                        _tq.TryGetValue(T, out var tv);
                        var e = SalidaFamilia.Entrada(T, _alm.Get, _tres, tv);
                        if (e == null) b.Remove(T); else b[T] = e;
                    }
                    _hist = b.ToImmutable();
                }

                Publicar(ahoraUtc);
            }
            catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "Avanzar", e); }
            finally { MsUltimoAvanzar = sw.Elapsed.TotalMilliseconds; Monitor.Exit(_paso); }
        }

        /// <summary>Si ya se puede calcular el pasado: un minuto calculado sin libros no se reintenta en esta corrida (los minuteros se llaman en
        /// orden creciente), asi que no se empieza hasta que el historico de las fuentes este en memoria.</summary>
        private bool FuentesListas()
        {
            if ((DateTime.UtcNow - _configuradoUtc).TotalSeconds >= _op.EsperaArranqueS) return true;
            try
            {
                if (_op.FuentesListas != null) return _op.FuentesListas();
                bool cboe = _cboe == null || (_cboe.Version > 0 && (_cboe.Estado ?? "").IndexOf("leyendo historia", StringComparison.OrdinalIgnoreCase) < 0);
                bool nq = _nq == null || _nq.Version > 0;
                bool cinta = _cinta == null || _cinta.UltimoTickUtc > DateTime.MinValue;
                return cboe && nq && cinta;
            }
            catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "FuentesListas", e); return false; }
        }

        /// <summary>4.1.0: true = el minuto k (en vivo) espera el primer tick de la vela m2 en curso. CierreConocido(t) toma la ultima vela m2
        /// CERRADA, y "cerrada" quiere decir que llego un tick despues de su fin: sin ese tick, a t + 6 s el fut saldria de la vela de 2 min antes
        /// y no coincidiria con el historico (33 de 1378 minutos en el vivo simulado del 08-10). Solo mira minutos con menos de EsperaCierreS de
        /// edad (la puesta al dia nunca espera) y nunca espera si la cinta no tiene ningun tick.</summary>
        private bool EsperarCierre(long k, DateTime ahoraUtc)
        {
            if (_op.EsperaCierreS <= 0 || _cinta == null) return false;
            var t = SesionFamilia.DeClave(k);
            if ((ahoraUtc - t).TotalSeconds >= _op.EsperaCierreS) return false;
            DateTime tick;
            try { tick = _cinta.UltimoTickUtc; } catch { return false; }
            if (tick <= DateTime.MinValue.AddDays(1)) return false;
            long piso = Ms(t) / 120000L * 120000L;            // apertura de la vela m2 en curso = fin de la anterior
            if (Ms(tick) >= piso) return false;
            if (k != _ultimoEsperado) { _ultimoEsperado = k; MinutosEsperados++; }
            return true;
        }

        private static bool MismaVela(TqqqVela a, TqqqVela b)
        {
            if (a == null || b == null) return a == b;
            if (!(a.S.Equals(b.S) && a.C.Equals(b.C) && a.DatoUtc == b.DatoUtc && a.Congelada == b.Congelada)) return false;
            if ((a.Series?.Count ?? 0) != (b.Series?.Count ?? 0)) return false;
            if (a.Series == null) return true;
            foreach (var kv in a.Series)
            {
                if (!b.Series.TryGetValue(kv.Key, out var y)) return false;
                var x = kv.Value;
                if ((x?.Length ?? 0) != (y?.Length ?? 0)) return false;
                for (int i = 0; x != null && i < x.Length; i++) if (!x[i].Precio.Equals(y[i].Precio) || x[i].Etq != y[i].Etq || !x[i].GexM.Equals(y[i].GexM)) return false;
            }
            return true;
        }

        /// <summary>Un minuto (preview_niveles.calcular para un key). null = sin libros.</summary>
        private RegistroMinuto CalcularMinuto(long clave)
        {
            var t = SesionFamilia.DeClave(clave);
            double fut = double.NaN;
            try { fut = _cinta != null ? _cinta.CierreConocido(t) : double.NaN; } catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "CierreConocido", e); }
            var bk = new List<LibroMinuto>(3);
            foreach (var m in _libros)
            {
                LibroMinuto lm = null;
                try { lm = m.Minuto(clave, t, fut); LlamadasMinutero++; } catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "Minuto " + m.Libro, e); }
                if (lm == null) continue;
                if (!Normalizar(lm)) { BitacoraFam.Error(_op.RutaLog, "LibroMinuto " + m.Libro, new InvalidDataException("arreglos nulos o de distinto largo en " + lm.Libro)); continue; }
                bk.Add(lm);
            }
            RegistroMinuto r;
            try { r = ReglasFam.Minuto(clave, bk); }
            catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "ReglasFam", e); return null; }
            // 4.1.3: las series extra DESPUES de la cuenta y en su propio try: si fallan, el minuto queda como en la 4.1.2
            if (r != null && _op.Extras != null)
            {
                r.ExtraIntentado = true;
                try { _op.Extras.Calcular(r, t, fut, bk); }
                catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "Extras", e); r.Extra = null; r.ExtraStrikes = null; r.MetaExtra = Array.Empty<MetaLibro>(); }
            }
            return r;
        }

        /// <summary>4.1.3: completa en memoria las series extra de un minuto cargado del archivo que no las tiene (una vez por minuto y corrida).</summary>
        private bool CompletarExtras(long k)
        {
            var ex = _op.Extras; if (ex == null) return false;
            var r = _alm.Get(k);
            if (r == null || r.ExtraIntentado || r.TieneExtra) return false;
            r.ExtraIntentado = true;
            try { return ex.Completar(r, SesionFamilia.DeClave(k)); }
            catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "Extras.Completar", e); r.Extra = null; r.ExtraStrikes = null; r.MetaExtra = Array.Empty<MetaLibro>(); return false; }
        }

        /// <summary>Defensa contra un LibroMinuto incompleto: rehace Gv/Go si faltan; descarta si los largos no coinciden o fut es NaN.</summary>
        private static bool Normalizar(LibroMinuto b)
        {
            if (b.Fut == null || b.GvC == null || b.GvP == null || b.GoC == null || b.GoP == null || double.IsNaN(b.FutMnq)) return false;
            int n = b.Fut.Length;
            if (b.GvC.Length != n || b.GvP.Length != n || b.GoC.Length != n || b.GoP.Length != n) return false;
            if (b.Gv == null || b.Gv.Length != n) { b.Gv = new double[n]; for (int i = 0; i < n; i++) b.Gv[i] = b.GvC[i] + b.GvP[i]; }
            if (b.Go == null || b.Go.Length != n) { b.Go = new double[n]; for (int i = 0; i < n; i++) b.Go[i] = b.GoC[i] + b.GoP[i]; }
            if (string.IsNullOrEmpty(b.Libro)) return false;
            return true;
        }

        // ------------------------------------------------------------------ la foto
        private void Publicar(DateTime ahoraUtc)
        {
            var ultimo = _alm.Ultimo;
            // tqqq_vivo: 'ultimo' = la ultima vela con foto vigente y c (ICalculoTqqq.Vela != null), tenga o no niveles
            TqqqVela tqU = null; long tqT = long.MinValue;
            foreach (var kv in _tq) if (kv.Value != null && kv.Key > tqT) { tqT = kv.Key; tqU = kv.Value; }
            string estCboe = null; DateTime tick = default;
            try { estCboe = _cboe?.Estado; } catch { }
            try { tick = _cinta?.UltimoTickUtc ?? default; } catch { }
            var avisos = new List<string>();
            if (Pendiente) avisos.Add("calculando la sesion: " + _alm.Cuantos + " minutos con libros, sigue en el proximo paso");
            if (!_listoFuentes) avisos.Add("esperando el historico de las fuentes (Rithmic / CBOE)");
            bool abierta = ahoraUtc >= _ses.IniUtc && ahoraUtc < _ses.FinUtc;
            if (ultimo == null) { if (abierta && _listoFuentes && !Pendiente) avisos.Add("sin libros todavia en esta sesion"); }
            else
            {
                double sin = (ahoraUtc - SesionFamilia.DeClave(ultimo.Clave)).TotalSeconds;
                if (abierta && sin > 600) avisos.Add("sin libros vigentes desde las " + SesionFamilia.DeClave(ultimo.Clave).ToString("HH:mm", CultureInfo.InvariantCulture) + " UTC (hace " + SalidaFamilia.Edad(sin) + ")");
                else if (abierta && !Pendiente)
                {   // 4.1.0: decir QUE libro falta (antes desaparecian series sin ningun cartel)
                    var faltan = CatalogoFamilia.Libros.Where(lb => ultimo.Libros == null || Array.IndexOf(ultimo.Libros, lb) < 0).ToList();
                    if (faltan.Count > 0)
                        avisos.Add("el ultimo minuto no trae " + string.Join(" ni ", faltan) + " ("
                                   + string.Join("; ", faltan.Select(lb => lb == "NQ" ? "NQ: sin foto vigente del libro de opciones de Rithmic" : lb + ": sin cadena vigente de CBOE o sin conversion")) + ")");
                }
            }
            // 4.1.0: la cinta del MNQ (Rithmic) con la causa probable; antes, si nunca llego un tick, no habia ningun aviso
            if (abierta && _cinta != null && (tick == default || tick <= DateTime.MinValue.AddDays(1)))
                avisos.Add("sin ticks del MNQ todavia: Rithmic desconectado? (o pestaña oculta)");
            else if (abierta && tick != default && (ahoraUtc - tick).TotalSeconds > 120)
                avisos.Add("sin ticks del MNQ hace " + SalidaFamilia.Edad((ahoraUtc - tick).TotalSeconds) + ": Rithmic desconectado? (o pestaña oculta)");
            var f = new FotoFamilia
            {
                Sesion = _ses.Dia, CalculadoUtc = ahoraUtc, HistoriaM2 = _hist,
                Actuales = SalidaFamilia.Actuales(ultimo, _tres, tqU).AsReadOnly(),
                Fuentes = SalidaFamilia.Fuentes(ultimo, tqU, estCboe, tick, ahoraUtc).AsReadOnly(),
                TqS = tqU?.S ?? double.NaN, TqC = tqU?.C ?? double.NaN, TqDatoUtc = tqU?.DatoUtc ?? default,
                Instrumento = _contrato, Aviso = string.Join(" · ", avisos)
            };
            Volatile.Write(ref _foto, f);
        }

        private static long Ms(DateTime utc) => SesionFamilia.Ms(utc);
        private static long Clave(DateTime utc) => (long)Math.Floor(SesionFamilia.Ms(utc) / 60000.0);
    }

    /// <summary>Log propio con freno: una linea por minuto y lugar como maximo (ATAS se traga las excepciones).</summary>
    internal static class BitacoraFam
    {
        private static readonly object _l = new object();
        private static readonly Dictionary<string, DateTime> _ult = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        public static void Linea(string ruta, string lugar, string msg)
        {
            try
            {
                if (string.IsNullOrEmpty(ruta)) return;
                lock (_l)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(ruta));
                    var fi = new FileInfo(ruta);
                    if (fi.Exists && fi.Length > 4_000_000) { try { File.Copy(ruta, ruta + ".1", true); File.Delete(ruta); } catch { } }
                    File.AppendAllText(ruta, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC [" + lugar + "] " + msg + "\n", new UTF8Encoding(false));
                }
            }
            catch { }
        }

        public static void Error(string ruta, string lugar, Exception e)
        {
            lock (_l)
            {
                if (_ult.TryGetValue(lugar, out var t) && (DateTime.UtcNow - t).TotalSeconds < 60) return;
                _ult[lugar] = DateTime.UtcNow;
            }
            Linea(ruta, lugar, "ERROR " + e.GetType().Name + ": " + e.Message + " | " + (e.StackTrace ?? "").Replace("\r", "").Replace("\n", " | "));
        }
    }
}
