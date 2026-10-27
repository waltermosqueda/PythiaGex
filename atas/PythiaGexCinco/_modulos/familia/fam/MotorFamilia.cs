// MotorFamilia.cs — PythiaGex 4.1, modulo fam (B3d), 08-10-2026. Implementa IMotorFamilia (Contratos.cs).
// Port de preview_niveles.Motor (ciclo / historico / calcular / salida) sobre los modulos por libro (ILibroMinutero es/spx/spy), la cinta
// (ICinta) y lo que dibuja el motor 3.0 del clon (AlmacenTres). Una sesion por vez; cambia sola a las 18:00 NY
// (corregida) o a las 22:00 UTC (paridad).
// 5.0 (27-10-2026): sin TQQQ. Fuera ICalculoTqqq, las velas m2 de TQQQ (_tq, VelasTqqq, PasadasTqqqPorS, MismaVela), la opcion
// TqqqDeNoche y el eje TqS/TqC de la foto. Los minutos, su historia por vela m2 y los actuales de las demas series no cambian: TQQQ
// solo se AGREGABA al final (SalidaFamilia) y nunca entraba en la cuenta de un minuto (RegistroMinuto).
//
// Grilla por minuto (preview_niveles.calcular): fut = ICinta.CierreConocido(t); por libro LibroMinuto (null = no vale); ReglasFam.Minuto.
// Tandas: como la vista previa, que reinicia el corrimiento de ES y la cache del zero estandar en CADA llamada a calcular:
//   * al ponerse al dia (arranque o hueco): tandas de 240 minutos alineadas al inicio de la sesion (historico: range(ini, k1) de a 240);
//   * en vivo: una tanda por minuto nuevo (ciclo: k0..k_ahora).
//   Un minutero que tenga caches por tanda implementa ITandaMinutero y recibe NuevaTanda antes de cada una (opcional; propuesto).
// Un minuto se calcula a los RetrasoMinutoS (6 s) de empezar, como el bucle de la vista previa (segundo ~6 del minuto).
// Los minutos ya calculados no se recalculan (persistidos en PythiaGex5\familia\niv-<sesion>-<contrato>.jsonl).
// Arranque: se espera a que las fuentes tengan datos (cboe.Version > 0 y es.Version > 0) o EsperaArranqueS, para no anotar minutos
// pasados "sin libros" solo porque el historico de las fuentes todavia no termino de cargar.
// Hilos: Avanzar corre en UN hilo de fondo (el timer de la Familia); Estela() puede llamarse desde el hilo de ATAS; Foto se lee desde el
// render con Volatile.Read (FotoFamilia inmutable; HistoriaM2 es un ImmutableDictionary que se actualiza por vela).
// Sin referencias a ATAS. Todo con try/catch y log propio (pythiagex5-familia.log, una linea por minuto y lugar como maximo).
//
// Conexion (la hace el integrador; nombres de B1/B2/B3a-c tal como estan hoy):
//   var motor = new MotorFamilia(new OpcionesMotorFamilia {
//         Carpeta = AdaptadoresFamilia.Carpeta,                      // %APPDATA%\ATAS\PythiaGex5 (familia\ y estela\ debajo)
//         Corregida = !AdaptadoresFamilia.SesionUtcFija,             // mismo modo que los minuteros (OpcionesSpx.ParidadPython, OpcionesSpy.Corregida)
//         FuentesListas = () => bajador.HistoriaCargada && libroEs.Sembrado && cinta.Version > 0 });
//   motor.Configurar(cinta, libroEs, cboe, new ILibroMinutero[] { minEs, minSpx, minSpy }, "MESZ6" /* codigo del grafico */);
//   AdaptadoresFamilia.AlEstela = motor.Estela;                    // TRES_* al instante (sin el gancho igual se leen de PythiaGex5\estela)
//   hilo "PythiaGex5 familia" (BelowNormal): motor.Avanzar(DateTime.UtcNow) cada 5 s; si motor.Pendiente, otra vez a los 300 ms.
//   render: motor.Foto (inmutable) y motor.Catalogo (CatalogoFamilia.Series).
// TRES_* = las estelas que escribe el MISMO clon (GuardarEstela de GammaHoyTres para ES y de GammaHoyTresCapas para SPX/SPY, con
// Guardar3Estela = true y Capa3SPY/Capa3SPX = Propia, sus defaults) en PythiaGex5\estela: nada de PythiaGex3.
// 4.1.3 (09-10-2026): OpcionesMotorFamilia.Extras (IExtrasMinuto, la Replica20) agrega las series "como la 2.0" a cada minuto DESPUES de
// ReglasFam.Minuto (las 21 no cambian); los minutos del archivo sin extras se completan en memoria al recorrerlos. Sin Extras = 4.1.2 exacto.
// 4.1.5 (09-10-2026): el indicador pasa ExtrasCompuestos (Replica20 + ClasicaSpx, la R10 "Clasica SPX 0Γ"); IExtrasParciales (opcional) deja
// completar en memoria un minuto del archivo que ya trae las R20 pero no la R10. Con la Replica20 sola, el motor es el de la 4.1.4.
// 5.0.3 (27-10-2026): el perfil estilo Gammalito (GammalitoFam.Perfil: barras por strike de SPX y SPY ya en ES) del ULTIMO minuto calculado con libros
// va en la foto (FotoFamilia.PerfilGl) para que la pantalla lo dibuje sin calcular. Se arma con los libros del minuto (bk) DESPUES de la cuenta y de las
// extras, en su propio try: no toca ni la cuenta ni las series. Un minuto del archivo no lo trae (sus libros ya no estan): hasta el primer minuto
// calculado en esta corrida no hay barras. Cambia de sesion -> null.
// 5.0.4 (27-10-2026): el perfil trae tambien las barras del libro ES de Rithmic (PerfilGl.BarrasEs); un minuto con SOLO el libro ES ya publica perfil.
// El motor no cambia: GammalitoFam.Perfil recibe los mismos libros del minuto.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace PythiaGexCinco.Familia
{
    /// <summary>Opcional (PROPUESTA de B3d, no esta en Contratos.cs): un ILibroMinutero con caches "por llamada" (corrimiento de ES, cache del
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

    /// <summary>4.1.5 (09-10-2026, opcional): un IExtrasMinuto que puede completar un minuto del archivo que YA trae algunas series extra (p. ej. uno
    /// guardado por la 4.1.4 con las R20 pero sin la R10 "Clasica SPX 0Γ" de la 4.1.5). Sin esta interfaz el motor completa solo los minutos SIN
    /// ninguna extra (la regla de la 4.1.3): con la Replica20 sola el motor queda exacto como en la 4.1.4.</summary>
    public interface IExtrasParciales
    {
        /// <summary>true si al minuto le falta alguna serie que este proveedor sabe completar.</summary>
        bool Falta(RegistroMinuto r);
    }

    public sealed class OpcionesMotorFamilia
    {
        /// <summary>%APPDATA%\ATAS\PythiaGex5 (adentro: familia\ y estela\). null/"" = sin persistencia ni lectura de estelas de archivo.</summary>
        public string Carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex5");
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
        /// () => bajador.HistoriaCargada &amp;&amp; libroEs.Sembrado &amp;&amp; cinta.Version > 0. Sin esto: CBOE con Version > 0 y sin "leyendo historia"
        /// en su Estado, libro ES con Version > 0 y cinta con algun tick; o EsperaArranqueS.</summary>
        public Func<bool> FuentesListas;
        public int RetencionDias = 14;
        public string RutaLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "pythiagex5-familia.log");
        /// <summary>4.1.3: las series extra (las dominantes como la 2.0, Replica20). null = sin extras: el motor queda EXACTO como en la 4.1.2
        /// (los arneses de paridad lo construyen asi). El indicador (HostFamilia) pasa la Replica20.</summary>
        public IExtrasMinuto Extras;
    }

    public sealed class MotorFamilia : IMotorFamilia
    {
        public const string VERSION = "fam 5.0 (27-10-2026, la 4.1.5d sin TQQQ; escala ES: radios ES 25 / SPX-SPY-familia 20, zero 0,5 +-75, CONF 3, FAM 5 (paso de strike 5 como NQ), ES x50)";
        private readonly OpcionesMotorFamilia _op;
        private readonly object _paso = new object();

        private ICinta _cinta; private ILibroEs _es; private IFuenteCboe _cboe;
        private ILibroMinutero[] _libros = Array.Empty<ILibroMinutero>();
        private string _contrato = "";
        private DateTime _configuradoUtc = DateTime.MinValue;
        private bool _configurado;

        private SesionFamilia _ses;
        private AlmacenNiveles _alm;
        private AlmacenTres _tres;
        private long _siguiente, _finTanda;
        private bool _listoFuentes;
        private long _ultimoEsperado = long.MinValue;
        private ImmutableDictionary<long, IReadOnlyDictionary<string, double[]>> _hist = ImmutableDictionary<long, IReadOnlyDictionary<string, double[]>>.Empty;
        /// <summary>4.1.8 REBOBINADO: minuto -> su registro CLONADO y la estela de SU sesion. Clonado porque los
        /// registros publicados se mutan despues en el hilo del host (Replica20/ClasicaSpx reemplazan Extra en tres
        /// escrituras no atomicas): congelar el diccionario no alcanza, hay que congelar el registro. El apareo con
        /// la sesion se hace ACA, en el hilo del host, para que el de dibujo no tenga que resolver que sesion es.</summary>
        private sealed class RegRebob { public RegistroMinuto R; public AlmacenTres Tres; }
        private ImmutableDictionary<long, RegRebob> _rebob = ImmutableDictionary<long, RegRebob>.Empty;
        private ImmutableDictionary<long, RegRebob> _rebobPrevias = ImmutableDictionary<long, RegRebob>.Empty;
        private long _tresVersion;
        /// <summary>5.0.3: el perfil estilo Gammalito del ultimo minuto calculado con libros (solo lo toca el hilo del motor; se publica en la foto).</summary>
        private PerfilGl _perfilGl;
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
        /// <summary>4.1.3: minutos del archivo a los que se les completaron las series extra en esta corrida.</summary>
        public long MinutosCompletados { get; private set; }
        /// <summary>4.1.4: minutos ya calculados cuyas series extra se rehicieron (IExtrasRehacer: la razon de la 2.0 cambio) y veces que paso.</summary>
        public long MinutosRehechos { get; private set; }
        public int Rehechuras { get; private set; }
        private long _genExtras;
        /// <summary>4.1.0: lo pone el host al parar. Avanzar deja de calcular minutos en el proximo paso, sin esperar la tanda:
        /// Parar ya no espera 2,5 s de puesta al dia. Lo ya calculado se guarda igual (las fuentes siguen vivas hasta que el hilo termina).</summary>
        public volatile bool Detener;
        public double MsUltimoAvanzar { get; private set; }
        public SesionFamilia Sesion => _ses;
        public AlmacenNiveles Almacen => _alm;
        public AlmacenTres Tres => _tres;

        public MotorFamilia() : this(new OpcionesMotorFamilia()) { }
        public MotorFamilia(OpcionesMotorFamilia op) { _op = op ?? new OpcionesMotorFamilia(); }

        public FotoFamilia Foto => Volatile.Read(ref _foto);
        public IReadOnlyList<SerieInfo> Catalogo => CatalogoFamilia.Series;

        private string CarpetaFamilia => string.IsNullOrEmpty(_op.Carpeta) ? null : Path.Combine(_op.Carpeta, "familia");
        private string CarpetaEstela => string.IsNullOrEmpty(_op.Carpeta) ? null : Path.Combine(_op.Carpeta, "estela");

        public void Configurar(ICinta cinta, ILibroEs es, IFuenteCboe cboe, IReadOnlyList<ILibroMinutero> libros, string contrato)
        {
            lock (_paso)
            {
                _cinta = cinta; _es = es; _cboe = cboe;
                var orden = new List<ILibroMinutero>();
                if (libros != null)
                {   // el orden de bk de la vista previa: ES, SPX, SPY (y cualquier otro al final)
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
                          (_cboe != null ? ", CBOE" : "") + ", modo " + (_op.Corregida ? "corregido (NY)" : "paridad (UTC fijo)")
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
            _perfilGl = null;                                                       // 5.0.3: el perfil de la otra sesion no se muestra
            _siguiente = s.IniClave; _finTanda = long.MinValue;
            _hist = ImmutableDictionary<long, IReadOnlyDictionary<string, double[]>>.Empty;
            string previas = "";
            try { _hist = HistoriaPrevia(s, out previas); } catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "HistoriaPrevia", e); }   // 4.1.6g: memoria de 2 sesiones
            // 4.1.8: el indice del rebobinado = lo previo (lo llena HistoriaPrevia) + lo ya cargado de esta sesion
            try
            {
                var br = _rebobPrevias.ToBuilder();
                foreach (var r in _alm.Todos) if (r != null) br[r.Clave] = new RegRebob { R = r.Copia(), Tres = _tres };
                _rebob = br.ToImmutable();
            }
            catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "RebobinadoSiembra", e); }
            _listoFuentes = false;
            _genExtras = (_op.Extras as IExtrasRehacer)?.Generacion ?? 0;          // 4.1.4: lo que cambie desde ahora se rehace
            BitacoraFam.Linea(_op.RutaLog, "sesion", "sesion " + s + ": " + cargados + " minutos de " + (_alm.Ruta ?? "(sin archivo)") + ", " + lt + " lineas de estela"
                              + " | historia previa: " + (previas == "" ? "ninguna" : previas));
        }

        /// <summary>4.1.6g (pedido del operador 11-10: "por que no cargo las dominantes del jueves y viernes, solo las de ahora ... quiero que minimo
        /// siempre cargue hasta dos dias antes"): la historia por vela m2 de las SESIONES_PREVIAS sesiones anteriores que tengan archivo (se buscan hasta
        /// 10 dias atras: el fin de semana no tiene archivo), leida del disco (niv + estelas de la 3.0), sin recalcular nada. Va debajo de la
        /// de la sesion en curso (sus velas son anteriores a su apertura): la pantalla la dibuja sobre las velas viejas del grafico.</summary>
        public const int SESIONES_PREVIAS = 2;
        private ImmutableDictionary<long, IReadOnlyDictionary<string, double[]>> HistoriaPrevia(SesionFamilia s, out string txt)
        {
            txt = "";
            var b = ImmutableDictionary<long, IReadOnlyDictionary<string, double[]>>.Empty.ToBuilder();
            var rb = ImmutableDictionary<long, RegRebob>.Empty.ToBuilder();
            _rebobPrevias = ImmutableDictionary<long, RegRebob>.Empty;
            if (!_op.Persistir || CarpetaFamilia == null) return b.ToImmutable();
            var d0 = DateTime.ParseExact(s.Dia, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            int n = 0; var partes = new List<string>();
            for (int k = 1; k <= 10 && n < SESIONES_PREVIAS; k++)
            {
                string dia = d0.AddDays(-k).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var alm = new AlmacenNiveles(CarpetaFamilia, dia, _contrato, s.Corregida);
                if (alm.Ruta == null || !File.Exists(alm.Ruta)) continue;
                int c = 0; try { c = alm.Cargar(); } catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "HistoriaPrevia.Cargar " + dia, e); continue; }
                if (c <= 0) continue;
                var sp = SesionFamilia.DelDia(dia, s.Corregida);
                var tres = new AlmacenTres(sp.IniUtc, sp.FinUtc);
                try { tres.LeerArchivos(CarpetaEstela, dia); } catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "HistoriaPrevia.Estelas " + dia, e); }
                long ini = Ms(sp.IniUtc) / 120000L * 120000L, fin = Math.Min(Ms(sp.SiguienteIniUtc), Ms(s.IniUtc)) - 120000L;
                int velas = 0;
                for (long T = ini; T <= fin; T += 120000L)
                {
                    var e = SalidaFamilia.Entrada(T, alm.Get, tres);
                    if (e != null) { b[T] = e; velas++; }
                }
                // 4.1.8: hasta la 4.1.7 'alm' y 'tres' se tiraban aca y por eso, al retroceder el grafico a estos dias,
                // las rayas se dibujaban pero no habia con que leerlas. Quedarselos no cuesta nada: ya estan parseados.
                foreach (var rr in alm.Todos) if (rr != null) rb[rr.Clave] = new RegRebob { R = rr.Copia(), Tres = tres };
                n++; partes.Add(dia + " (" + c + " min, " + velas + " velas m2)");
            }
            txt = string.Join(", ", partes);
            _rebobPrevias = rb.ToImmutable();
            return b.ToImmutable();
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

                // ---- la vela m2 que se esta formando y las velas de la historia a rehacer
                //      5.0 (27-10-2026): aca iba el calculo de TQQQ por vela m2 (sus velas cambiadas tambien entraban en 'sucias'). Sin TQQQ,
                //      'sucias' se llena solo con lo de abajo (minutos nuevos/rehechos, TRES y las ultimas velas), como para las demas series.
                long formando = Ms(ahoraUtc) / 120000L * 120000L;
                var sucias = new HashSet<long>();

                // ---- 4.1.8: el indice del rebobinado sigue a los minutos nuevos y a los re-completados. Se vuelve a
                //      clonar porque CompletarExtras muta el registro en el lugar: lo publicado tiene que ser la version completa.
                if (ultimaNueva != long.MinValue)
                {
                    var rb = _rebob.ToBuilder();
                    for (long k = primeraNueva; k <= ultimaNueva; k++)
                    {
                        var rr = _alm.Get(k);
                        if (rr != null) rb[k] = new RegRebob { R = rr.Copia(), Tres = _tres };
                    }
                    _rebob = rb.ToImmutable();
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
                        var e = SalidaFamilia.Entrada(T, _alm.Get, _tres);
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
                bool es = _es == null || _es.Version > 0;
                bool cinta = _cinta == null || _cinta.UltimoTickUtc > DateTime.MinValue;
                return cboe && es && cinta;
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
            // 5.0.3: el perfil estilo Gammalito del minuto (solo lee bk); el del ultimo minuto con libros es el que se publica
            if (r != null && r.TieneLibros)
            {
                try { _perfilGl = GammalitoFam.Perfil(clave, bk); }
                catch (Exception e) { BitacoraFam.Error(_op.RutaLog, "PerfilGl", e); _perfilGl = null; }
            }
            return r;
        }

        /// <summary>4.1.3: completa en memoria las series extra de un minuto cargado del archivo que no las tiene (una vez por minuto y corrida).
        /// 4.1.5: tambien uno que trae ALGUNAS (las R20 de la 4.1.4) si el proveedor dice que le falta otra (IExtrasParciales: la R10 de la clasica).</summary>
        private bool CompletarExtras(long k)
        {
            var ex = _op.Extras; if (ex == null) return false;
            var r = _alm.Get(k);
            if (r == null || r.ExtraIntentado || (r.TieneExtra && !(ex is IExtrasParciales pa && pa.Falta(r)))) return false;
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
            string estCboe = null; DateTime tick = default;
            try { estCboe = _cboe?.Estado; } catch { }
            try { tick = _cinta?.UltimoTickUtc ?? default; } catch { }
            var avisos = new List<string>();
            if (Pendiente) avisos.Add("calculando la sesion: " + _alm.Cuantos + " minutos con libros, sigue en el proximo paso");
            if (!_listoFuentes) avisos.Add("esperando el historico de las fuentes (Rithmic / CBOE)");
            bool abierta = ahoraUtc >= _ses.IniUtc && ahoraUtc < _ses.FinUtc;
            // 4.1.5d: fuera de la sesion (fin de semana: se muestra la del viernes) el dato es VIEJO y se dice antes que nada
            if (ahoraUtc >= _ses.FinUtc)
                avisos.Insert(0, "mercado cerrado: se muestra la sesion del " + _ses.Dia + " (cerro hace " + SalidaFamilia.Edad((ahoraUtc - _ses.FinUtc).TotalSeconds) + ")");
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
                                   + string.Join("; ", faltan.Select(lb => lb == "ES" ? "ES: sin foto vigente del libro de opciones de Rithmic" : lb + ": sin cadena vigente de CBOE o sin conversion")) + ")");
                }
            }
            // 4.1.0: la cinta del MES (Rithmic) con la causa probable; antes, si nunca llego un tick, no habia ningun aviso
            if (abierta && _cinta != null && (tick == default || tick <= DateTime.MinValue.AddDays(1)))
                avisos.Add("sin ticks del MES todavia: Rithmic desconectado? (o pestaña oculta)");
            else if (abierta && tick != default && (ahoraUtc - tick).TotalSeconds > 120)
                avisos.Add("sin ticks del MES hace " + SalidaFamilia.Edad((ahoraUtc - tick).TotalSeconds) + ": Rithmic desconectado? (o pestaña oculta)");
            var f = new FotoFamilia
            {
                Sesion = _ses.Dia, CalculadoUtc = ahoraUtc, HistoriaM2 = _hist,
                Actuales = SalidaFamilia.Actuales(ultimo, _tres).AsReadOnly(),
                Fuentes = SalidaFamilia.Fuentes(ultimo, estCboe, tick, ahoraUtc).AsReadOnly(),
                Instrumento = _contrato, Aviso = string.Join(" · ", avisos),
                PerfilGl = _perfilGl                                                // 5.0.3: inmutable (se arma uno nuevo por minuto)
            };
            // 4.1.8 REBOBINADO: clausura sobre la instantanea INMUTABLE de este instante. El hilo de dibujo no toca
            // estado vivo del motor: solo consulta este diccionario congelado de clones. Si el minuto no esta, devuelve
            // null y la pantalla sigue mostrando el vivo; nunca rellena con el valor de ahora.
            var rebob = _rebob;
            f.Rebobinar = clave =>
            {
                if (rebob == null || rebob.Count == 0) return null;
                string nota = "";
                long usada = clave;
                if (!rebob.TryGetValue(usada, out var e) || e?.R == null || e.R.Vacio)
                {
                    usada = clave - 1; nota = "el minuto pedido no tiene niveles: se muestra el anterior";
                    if (!rebob.TryGetValue(usada, out e) || e?.R == null || e.R.Vacio) return null;
                }
                var min = DateTime.UnixEpoch.AddMinutes(usada);
                return new RebobinadoFam
                {
                    ClavePedida = clave, ClaveUsada = usada, MinutoUtc = min, FutMnq = e.R.FutMnq,
                    Libros = e.R.Libros ?? Array.Empty<string>(),
                    Niveles = SalidaFamilia.Actuales(e.R, e.Tres).AsReadOnly(),
                    Fuentes = SalidaFamilia.Fuentes(e.R, "", default, min).AsReadOnly(),
                    Nota = nota,
                };
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
