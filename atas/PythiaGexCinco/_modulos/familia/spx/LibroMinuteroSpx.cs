// LibroMinuteroSpx.cs — PythiaGex 4.1, modulo Familia SPX (B3b). ILibroMinutero del libro SPX (cadena _SPX de CBOE, bajada por el propio
// indicador; archivo 'ES' en cboe-local), port exacto de backtest_familia.minutos_cboe(libro='SPX', variante='sync') tal como lo llama
// preview_niveles.Motor.calcular:
//   foto = la ultima con generado <= t; vale si t < vigencia (C9); base = C7 (ConversionSpx); S = fut - base;
//   filas_hoy (C10) -> perfil_cp (Black-Scholes r 3,75 %) -> zero estandar C5 -> LibroMinuto con Fut = K + base y el filtro +-3 %.
// La cuenta usa el codigo comun de B3a (FilasHoy, PerfilLado, ZeroEstandarFam, CacheZero, ArmadoLibroMinuto, NumFam).
// Las series (MUROS/MAJORS/ZTP/ZEST, montos) las elige SeriesLibroFam (comun) en el motor sobre el LibroMinuto.
// 5.0 (27-10-2026, P2), lo que cambia contra la 4.1 en NDX (P2_SPX.md):
//   * MULT 100 (SPX, igual que NDX: USD por punto del indice, seguro). Black-Scholes r 3,75 % sin dividendo (comun/Gamma.cs) como la 4.1.
//   * zero estandar con ZeroEstandarFam.SPX = paso 0,5 / +-75 / centro 12,5 (la 4.1 en NDX 2,5 / 300 / 50: distancias / 4, el paso a 2
//     ticks; lo dejo P1 en comun/ZeroEstandar). La clave de la cache usa round(S / 12,5) (solo pesa con ZeroCadaMinuto = false).
//   * radio de seleccion del libro: SeleccionFam.RadioDe("SPX") = 20 (comun/Seleccion; se aplica en SeriesLibroFam por b.Libro = "SPX").
//   * la base del minuto pasa por las COTAS de ConversionSpx (sanidad 0-150, carry 3,4 %, forwards): fuera de cota SPX no se dibuja.
//   * la cadena ya trae la SPX AM aparte (vence 09:30 NY, nunca en el horizonte Hoy) y sin series vencidas en la base (CboeCadena).
//   * lo que queda igual a NDX: TANDA 240, tramo de 900 s, reintentos 300/120/120 s, poda de muestras 14 dias, filtro +-3 % (relativo).
//
// CACHE DEL ZERO (C5). La vista previa calcula el zero con el S del PRIMER minuto de cada clave (foto, tramo de 900 s, round(S/cp)) y lo
// reutiliza dentro de cada llamada a minutos_cboe:
//   * historico (la referencia de paridad): tandas de 240 minutos alineadas al inicio de la sesion;
//   * en vivo (ciclo): una llamada por minuto nuevo, o sea cache nueva cada minuto.
// Si el motor implementa el protocolo de tandas (ITandaMinutero de fam/MotorFamilia.cs: NuevaTanda antes de cada tanda) manda el motor
// (vivo = vista previa en vivo; puesta al dia = historico). Si nadie llama a NuevaTanda, el modulo usa tandas de 240 alineadas a la sesion
// (vivo = historico). En los dos casos, si una tanda de puesta al dia arranca a mitad de una tanda historica (reinicio de ATAS), la cache se
// rehace con los minutos previos de esa tanda (fut = ICinta.CierreConocido, el mismo del motor) y el reinicio da lo mismo que el historico.
// Sin ATAS. Todo en UTC. Llamar en orden creciente de minuto (como dice el contrato), desde un solo hilo (el del motor).
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PythiaGexCinco.Familia.Spx
{
    /// <summary>Diagnostico del ultimo minuto (para la pestaña y para el arnes).</summary>
    public sealed class DiagMinutoSpx
    {
        public long Clave;
        public DateTime TUtc;
        public double Fut = double.NaN, Base = double.NaN, S = double.NaN, ZeroVol = double.NaN, ZeroOi = double.NaN;
        public EstadoFotoSpx Estado;
        public string Motivo = "";        // por que no hay libro (si no hay)
        public string TextoBase = "";
    }

    public sealed class LibroMinuteroSpx : ILibroMinutero, ITandaMinutero
    {
        public const string LIBRO = "SPX";
        public const double MULT = 100.0;                       // C1
        public const int TANDA_MIN = CacheZero.MINUTOS_TANDA;   // 240 (preview_niveles.historico)

        private readonly IFuenteCboe _cboe;
        private readonly ICinta _cinta;
        private readonly OpcionesSpx _op;
        private readonly ConversionSpx _conv;
        private readonly MuestrasSpx _muestras;
        private readonly object _llave = new object();

        private DateTime _dia = DateTime.MinValue;
        private long _ver = long.MinValue;
        private int _nCrudas = -1;
        private DateTime _ultTs, _ultGen;
        private List<FotoCadena> _fotos = new List<FotoCadena>();
        private long[] _genMs = new long[0];
        private DateTime _tickVisto = DateTime.MinValue;
        private DateTime _ultimoReintento = DateTime.MinValue;
        private readonly CacheZero _cache = new CacheZero();
        private long _tandaInterna = long.MinValue;
        private bool _tandaExterna;                             // el motor manda las tandas (NuevaTanda)
        private long _claveZ = long.MinValue;                   // ultimo minuto ya contado en la cache del zero
        private DateTime _ultimoLogError = DateTime.MinValue;

        public LibroMinuteroSpx(IFuenteCboe cboe, ICinta cinta, OpcionesSpx opciones)
        {
            _cboe = cboe ?? throw new ArgumentNullException(nameof(cboe));
            _cinta = cinta;
            _op = opciones ?? new OpcionesSpx();
            _conv = new ConversionSpx(_op);
            _muestras = new MuestrasSpx(_op.CarpetaDatos, _op.Log);
        }

        public string Libro => LIBRO;
        public OpcionesSpx Opciones => _op;
        public ConversionSpx Conversion => _conv;
        public DiagMinutoSpx Ultimo { get; private set; }
        public int Recalculos { get; private set; }
        public int Calentamientos { get; private set; }
        public int CalentadosUltimo { get; private set; }
        public int CalculosZero => _cache.Calculos;
        public int AciertosZero => _cache.Aciertos;

        // ------------------------------------------------------------------ ILibroMinutero
        /// <summary>Contrato: el libro SPX del minuto (null si no vale: sin foto vigente, sin base, fut NaN).</summary>
        public LibroMinuto Minuto(long clave, DateTime tUtc, double futMnq)
        {
            lock (_llave)
            {
                var d = new DiagMinutoSpx { Clave = clave, TUtc = tUtc, Fut = futMnq };
                Ultimo = d;
                try
                {
                    var t = CalendarioSpx.Utc(tUtc);
                    Preparar(clave, t);
                    return Calcular(clave, t, futMnq, d, true);
                }
                catch (Exception ex)
                {
                    d.Motivo = "error: " + ex.GetType().Name + ": " + ex.Message;
                    if ((DateTime.UtcNow - _ultimoLogError).TotalSeconds >= 60) { _ultimoLogError = DateTime.UtcNow; _op.Log?.Invoke("SPX minuto " + clave + ": " + ex); }
                    return null;
                }
                finally { if (clave > _claveZ) _claveZ = clave; }
            }
        }

        // ------------------------------------------------------------------ ITandaMinutero (protocolo del motor fam, propuesta de B3d)
        /// <summary>El motor arranca una tanda [primera, ultima]: cache del zero nueva (la vista previa la arma de cero en cada llamada a
        /// minutos_cboe). Una tanda de puesta al dia (3 minutos o mas) que no arranca alineada a 240 desde el inicio de la sesion se
        /// calienta con los minutos previos de su tanda historica (reinicio = historico).</summary>
        public void NuevaTanda(DateTime ahoraUtc, long primeraClave, long ultimaClave)
        {
            lock (_llave)
            {
                try
                {
                    _tandaExterna = true;
                    _cache.Vaciar();
                    _claveZ = primeraClave - 1;
                    if (_op.CalentarCacheZero && _cinta != null && ultimaClave - primeraClave + 1 >= 3)
                    {
                        var t = DeClave(primeraClave);
                        var dia = CalendarioSpx.SesionDe(t, _op.ParidadPython);
                        Refrescar(dia, t);
                        long ini = InicioTanda(dia, t);
                        if (ini < primeraClave) Calentar(ini, primeraClave - 1);
                    }
                }
                catch (Exception ex) { _op.Log?.Invoke("SPX NuevaTanda: " + ex); }
            }
        }

        // ------------------------------------------------------------------ preparacion y cuenta
        private static DateTime DeClave(long clave) => CalendarioSpx.Utc(DateTime.UnixEpoch.AddTicks(clave * 60_000L * TimeSpan.TicksPerMillisecond));

        /// <summary>Sesion, fotos y C7 al dia; y, sin protocolo del motor, la cache del zero por tanda de 240 alineada a la sesion (con
        /// calentamiento si se arranca a mitad de una tanda).</summary>
        private void Preparar(long clave, DateTime t)
        {
            bool par = _op.ParidadPython;
            var dia = CalendarioSpx.SesionDe(t, par);
            Refrescar(dia, t);
            if (_tandaExterna) return;
            long tanda = TandaDe(dia, t);
            if (tanda != _tandaInterna)
            {
                _cache.Vaciar(); _tandaInterna = tanda;
                _claveZ = InicioTanda(dia, t) - 1;
            }
            if (_op.CalentarCacheZero && _cinta != null && _claveZ < clave - 1) Calentar(_claveZ + 1, clave - 1);
            _claveZ = clave - 1;
        }

        private void Calentar(long desde, long hasta)
        {
            CalentadosUltimo = 0;
            var dummy = new DiagMinutoSpx();
            for (long kk = Math.Max(desde, hasta - TANDA_MIN + 1); kk <= hasta; kk++)
            {
                var tk = DeClave(kk);
                Calcular(kk, tk, _cinta.CierreConocido(tk), dummy, false);
                CalentadosUltimo++;
            }
            Calentamientos++;
        }

        /// <summary>La cuenta de un minuto. salida=false: solo deja el zero en la cache (calentamiento) y no arma el LibroMinuto.</summary>
        private LibroMinuto Calcular(long clave, DateTime t, double fut, DiagMinutoSpx d, bool salida)
        {
            if (double.IsNaN(fut)) { d.Motivo = "sin precio del MES (vela m2 cerrada)"; return null; }
            // foto = la ultima con generado <= t (np.searchsorted side='right' - 1)
            int k = NumFam.UltimoMenorIgual(_genMs, TiempoFam.Ms(t));
            if (k < 0) { d.Motivo = "sin cadena de SPX anterior a este minuto"; return null; }
            var e = _conv.Estados[k];
            d.Estado = e;
            if (t >= e.Vigencia)
            {
                d.Motivo = "cadena de SPX vencida (generada " + e.Foto.GeneradoUtc.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "Z, vigente hasta "
                           + e.Vigencia.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) + "Z)";
                return null;
            }
            double b = _conv.Base(e, t, fut, out string texto);      // 5.0: con las cotas (sanidad, carry 3,4 %, forwards)
            d.Base = b; d.TextoBase = texto;
            if (double.IsNaN(b)) { d.Motivo = texto; return null; }
            double S = fut - b;
            d.S = S;
            var fl = FilasHoy.Armar(e.Foto, t);
            PerfilLado.CargaPorStrike(e.Foto, t, S, false, out var kCg, out var oiCg, out var oiCgC, out var oiCgP, out var gxCg);   // 4.1.7: TODOS los vencimientos
            var r = PerfilLado.Armar(fl, S, false);
            if (r == null) { d.Motivo = "la cadena de SPX no aporta gamma en el horizonte Hoy"; return null; }
            // C5: clave (foto, tramo de 900 s desde generado, round(S/12,5)); se calcula con el S del PRIMER minuto de la clave (4.1 NDX: S/50)
            long envB = (long)Math.Floor((t - e.Foto.GeneradoUtc).TotalSeconds / 900.0);
            long sr = (long)Math.Round(S / ZeroEstandarFam.SPX.CentroPaso, MidpointRounding.ToEven);
            object fotoKey = _op.ZeroCacheConHorizonte ? (object)(e.Foto.GeneradoUtc.Ticks, e.Foto.TsUtc.Ticks, fl.N) : (e.Foto.GeneradoUtc.Ticks, e.Foto.TsUtc.Ticks);
            var z = _op.ZeroCadaMinuto
                ? ZeroEstandarFam.Calcular(fl, S, false, ZeroEstandarFam.SPX)
                : _cache.Obtener(fotoKey, envB, sr, () => ZeroEstandarFam.Calcular(fl, S, false, ZeroEstandarFam.SPX));
            if (!salida) return null;
            double zv = double.IsNaN(z.Zv) ? double.NaN : z.Zv + b, zo = double.IsNaN(z.Zo) ? double.NaN : z.Zo + b;
            d.ZeroVol = zv; d.ZeroOi = zo;
            // BM: Fut = K + base, solo strikes a <= 3 % de fut (en orden de K = orden de Fut)
            return ArmadoLibroMinuto.Armar(LIBRO, clave, fut, S, b, false, r, zv, zo, e.Foto.DatoUtc, !e.OiViejo, MULT, texto, e.Congelada, kCg, oiCg, oiCgC, oiCgP, gxCg);
        }

        /// <summary>Tanda interna de la cache del zero: (dia de sesion, minutos desde el inicio de la sesion / 240).</summary>
        private long TandaDe(DateTime dia, DateTime t)
        {
            var ini = CalendarioSpx.InicioSesion(dia, _op.ParidadPython);
            long min = (long)Math.Floor((t - ini).TotalMinutes);
            long tanda = _op.TandaZeroMin > 0 ? TiempoFam.PisoDiv(min, _op.TandaZeroMin) : 0;
            return (dia.Ticks / TimeSpan.TicksPerDay) * 100_000L + tanda;
        }

        /// <summary>Clave del primer minuto de la tanda (de 240, alineada al inicio de la sesion) que contiene t.</summary>
        private long InicioTanda(DateTime dia, DateTime t)
        {
            var ini = CalendarioSpx.InicioSesion(dia, _op.ParidadPython);
            long iniClave = TiempoFam.Clave(ini);
            long min = (long)Math.Floor((t - ini).TotalMinutes);
            if (_op.TandaZeroMin <= 0) return min >= 0 ? iniClave : iniClave + min;
            return iniClave + TiempoFam.PisoDiv(min, _op.TandaZeroMin) * _op.TandaZeroMin;
        }

        // ------------------------------------------------------------------ fotos y C7
        /// <summary>Trae las fotos de SPX si cambio la sesion o la version de la fuente, y rehace C7 (causal: el estado de cada foto solo mira
        /// las anteriores). Tambien reintenta si la cinta trajo datos para fotos que quedaron sin precio.</summary>
        private void Refrescar(DateTime dia, DateTime t)
        {
            bool par = _op.ParidadPython;
            bool cambioDia = dia != _dia;
            long ver = _cboe.Version;
            bool reintento = false;
            if (!cambioDia && ver == _ver && _cinta != null)
            {
                var ut = _cinta.UltimoTickUtc;
                if (ut > _tickVisto.AddSeconds(60) && (t - _ultimoReintento).TotalSeconds >= 60)
                {
                    var est = _conv.Estados;
                    for (int i = est.Count - 1; i >= 0 && est[i].TSpotUtc > _tickVisto.AddSeconds(-300); i--)
                        if (double.IsNaN(est[i].Precio)) { reintento = true; break; }
                    if (reintento) _ultimoReintento = t;
                }
            }
            if (!cambioDia && ver == _ver && !reintento) return;
            if (cambioDia)
            {
                _dia = dia;
                _cache.Vaciar(); _tandaInterna = long.MinValue;
                if (_muestras.Activa)
                {
                    _muestras.Cargar(dia.AddDays(-(_op.DiasHistoria + 1)), dia);
                    _muestras.Podar(dia.AddDays(-14));
                }
            }
            _ver = ver;
            var desde = CalendarioSpx.InicioSesion(dia.AddDays(-_op.DiasHistoria), par);
            var crudas = _cboe.Fotos(LIBRO, desde, DateTime.MaxValue) ?? Array.Empty<FotoCadena>();
            // la Version de la fuente sube con cualquier libro (SPY; en la 4.1 tambien TQQQ): si las fotos de SPX son las mismas, C7 no cambia
            if (!cambioDia && !reintento && crudas.Count == _nCrudas && crudas.Count > 0 && crudas[crudas.Count - 1] != null
                && crudas[crudas.Count - 1].TsUtc == _ultTs && crudas[crudas.Count - 1].GeneradoUtc == _ultGen) return;
            _nCrudas = crudas.Count;
            if (crudas.Count > 0 && crudas[crudas.Count - 1] != null) { _ultTs = crudas[crudas.Count - 1].TsUtc; _ultGen = crudas[crudas.Count - 1].GeneradoUtc; }
            var fotos = new List<FotoCadena>(crudas.Count);
            bool ordenada = true;
            foreach (var f0 in crudas)
            {
                // las fotos "livianas" de la fuente (B2 les saca las filas despues de 30 h) SIGUEN en C7: spot, sellos y OI total alcanzan;
                // una foto sin filas no puede ser la vigente de un minuto (FilasHoy da null: el libro no vale)
                if (f0 == null) continue;
                var f = ASegundos(f0);
                if (f.GeneradoUtc < desde || CalendarioSpx.EntreSesiones(f.GeneradoUtc, par)) continue;     // pausa de CME (21-22 UTC | 17-18 NY)
                if (fotos.Count > 0 && f.GeneradoUtc < fotos[fotos.Count - 1].GeneradoUtc) ordenada = false;
                fotos.Add(f);
            }
            if (!ordenada)
            {
                _op.Log?.Invoke("SPX: la fuente no dio las fotos ordenadas por generado (se reordenan, orden estable)");
                fotos = new List<FotoCadena>(System.Linq.Enumerable.OrderBy(fotos, f => f.GeneradoUtc));
            }
            _fotos = fotos;
            _genMs = new long[_fotos.Count];
            for (int i = 0; i < _fotos.Count; i++) _genMs[i] = TiempoFam.Ms(_fotos[i].GeneradoUtc);
            _tickVisto = _cinta != null ? _cinta.UltimoTickUtc : DateTime.MinValue;
            _conv.Recalcular(_fotos, PrecioEn, _muestras.Activa ? _muestras.Mapa : null);
            Recalculos++;
            if (_muestras.Activa && _cinta != null) _muestras.Guardar(_conv.Estados, _tickVisto.AddSeconds(-120), par);
        }

        /// <summary>'generado' al segundo (como el archivo de cadenas y el ultima-*.json que lee la vista previa): la cuenta envejece desde ahi y
        /// la foto vale desde ese segundo. Copia solo si hace falta (FotoCadena es inmutable).</summary>
        private static FotoCadena ASegundos(FotoCadena f)
        {
            long sobra = f.GeneradoUtc.Ticks % TimeSpan.TicksPerSecond;
            if (sobra == 0 && f.GeneradoUtc.Kind == DateTimeKind.Utc) return f;
            return new FotoCadena
            {
                Libro = f.Libro, GeneradoUtc = new DateTime(f.GeneradoUtc.Ticks - sobra, DateTimeKind.Utc), TsUtc = f.TsUtc, DatoUtc = f.DatoUtc, Spot = f.Spot,
                Dias = f.Dias, Filas = f.Filas, EsFuturo = f.EsFuturo, OiTotal = f.OiTotal, BaseCruda = f.BaseCruda, BaseErrorTicks = f.BaseErrorTicks,
                CierreAnterior = f.CierreAnterior, FuturoFoto = f.FuturoFoto,
            };
        }

        private (double, bool) PrecioEn(DateTime t)
        {
            if (_cinta == null) return (double.NaN, false);
            double p = _cinta.Precio(t);
            if (double.IsNaN(p)) return (double.NaN, false);
            double pt = _cinta.PrecioSoloTick(t, 120);
            return (p, !double.IsNaN(pt) && pt == p);
        }

        // ------------------------------------------------------------------ pestaña
        /// <summary>La fuente SPX para la pestaña (base y su origen, hora del dato, congelada, OI de dos sesiones) del ultimo minuto pedido.
        /// Protocolo: el que la muestra pone la edad ANTES del numero si el dato tiene mas de 30 min (DatoUtc).</summary>
        public FuenteEstado Estado()
        {
            lock (_llave)
            {
                var d = Ultimo;
                var fe = new FuenteEstado { Libro = LIBRO };
                if (d == null) { fe.Texto = "SPX: sin calcular"; return fe; }
                if (d.Estado == null) { fe.Texto = "SPX: " + d.Motivo; return fe; }
                var e = d.Estado;
                fe.DatoUtc = e.Foto.DatoUtc; fe.Congelada = e.Congelada; fe.OiOk = !e.OiViejo; fe.ConvValor = d.Base;
                fe.Texto = !string.IsNullOrEmpty(d.Motivo) ? "SPX: " + d.Motivo : d.TextoBase + (e.OiViejo ? " · OI de 2 sesiones" : "");
                return fe;
            }
        }
    }
}
