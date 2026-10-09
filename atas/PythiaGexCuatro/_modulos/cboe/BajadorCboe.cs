// BajadorCboe.cs — PythiaGex 4.1, modulo cboe (B2). Sin referencias a ATAS. Implementa IFuenteCboe (Contratos.cs).
// El descargador propio de las cadenas de NDX (_NDX), QQQ y TQQQ de CBOE (lo que Rithmic no transmite), adentro del indicador:
//   * UN hilo propio "PythiaGex4 cboe" (IsBackground, BelowNormal); nunca el pool de ATAS ni sus timers.
//   * Un ticker por vez (NDX -> QQQ -> TQQQ) con 3 s entre tickers; cadencia de cboe_local: 75 s de lunes a viernes 09:20-16:10 NY
//     (13:20-20:10 UTC en verano, que es lo que tenia cboe_local clavado en UTC), 300 s el resto. Proxima vuelta = max(5, espera - duracion).
//   * Gzip y de a trozos (HttpCboe). Sin reintento dentro de la vuelta; con 3 fallos seguidos un ticker pasa a 300 s; 403/429 = 15 min.
//   * Se acepta una cadena solo si: timestamp legible, current_price > 0, mas de 100 contratos, timestamp NO anterior al ultimo aceptado
//     y al menos una fila. Si el timestamp es igual al ultimo, no se anota (como archivar_cadena: gana la bajada mas temprana).
//   * Cada cadena aceptada: construir + medir + linea flaca -> cadena-<X>-<dia>.jsonl.gz, cadena-<X>.ultimo, ultima-<X>.json; y la foto
//     se arma RELEYENDO esa linea con el mismo lector del archivo (CboeCadena.DeLinea): lo que se ve en vivo es lo que se relee al reiniciar.
//   * Al arrancar lee su propio archivo (DiasHistoria dias UTC). Las fotos anteriores al CORTE quedan "livianas" (Filas vacio; spot, sellos,
//     hora del dato, OI total, base y cierre siguen): 8 dias con filas son ~170 MB en la memoria de ATAS (medido). Corte = el mas viejo entre
//     ahora - HorasConFilas y las 09:00 NY de la rueda habil ANTERIOR a hoy (asi C7/C8 tienen con filas la rueda previa aunque sea lunes).
//     La ultima foto anterior al corte conserva sus filas (es la vigente en el primer minuto despues del corte). HorasConFilas = 0: todo con filas.
//   * Un solo descargador por proceso (FuenteCboe.Obtener, con contador) y entre procesos (Mutex Local\PythiaGex4.BajadorCboe): el que no
//     es dueno no baja nada y sigue ultima-*.json cada 30 s.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using PythiaGexCuatro.Familia;

namespace PythiaGexCuatro.Cboe
{
    public sealed class OpcionesCboe
    {
        private static string App => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS");
        public string Carpeta = Path.Combine(App, "PythiaGex4", "cboe");
        public string Log = Path.Combine(App, "pythiagex4-cboe.log");
        public bool Bajar = true;                 // Cboe41Bajar
        public int TopeKBps = 1250;               // Cboe41TopeKBps: pausa = trozo / tope
        public int TrozoBytes = 64 * 1024;        // fuentes.py usa 256 KB; aca 64 KB (mismo proceso que el socket de Rithmic)
        public int BufferSocketBytes = 64 * 1024; // SO_RCVBUF: la ventana de TCP no deja pasar mas que esto sin que la app lea (0 = sistema)
        public bool VentanaUtcPython = false;     // true: la ventana de cboe_local (13:20-20:10 UTC, lun-vie) en vez de 09:20-16:10 NY
        public int CadenciaRuedaS = 75, CadenciaFueraS = 300;
        public int PausaEntreTickersMs = 3000;
        public int FallosParaEsperar = 3, EsperaTrasFallosS = 300;   // 3 fallos seguidos de un ticker -> ese ticker cada 300 s hasta que vuelva
        public int EsperaCloudflareS = 900;       // HTTP 403/429: todo espera 15 min
        public int RetrasoInicialS = 15;          // la primera vuelta, cuando ya paso la carga de ATAS
        public int DiasHistoria = 7;              // dias UTC previos que se leen al arrancar (+ hoy)
        public double HorasConFilas = 30;         // ver Corte(): fotos anteriores quedan sin filas (la ultima anterior al corte las conserva); 0 = todas con filas
        public int RetencionDias = 14;            // poda diaria de cadena-*-dia.jsonl.gz PROPIOS (0 = no podar)
        public string NombreMutex = @"Local\PythiaGex4.BajadorCboe";   // null/"" = sin mutex (siempre dueno)
        public int SeguirCadaS = 30;              // seguidor: cada cuanto mira ultima-*.json
        public string[] Libros = { "NDX", "QQQ", "TQQQ" };
        public Func<DateTime> AhoraUtc = () => DateTime.UtcNow;      // inyectable (arnes)
        public IHttpCboe Http;                                        // null = HttpCboe real
        public Action<string, byte[], int, DateTime> AlBajarCrudo;    // arnes: (simbolo, bytes comprimidos, largo, generado) de cada bajada OK
        public Func<DateTime, DateTime> HoyLocal = u => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(u, DateTimeKind.Utc), TimeZoneInfo.Local).Date;  // base.medir usa dt.date.today()
    }

    /// <summary>La ultima linea aceptada de un libro: el texto flaco (lo que lee el clon de la 3.0 en sus capas) y su foto.</summary>
    public sealed class RegistroCboe
    {
        public string Libro, Archivo, Texto;      // Texto puede ser null si la foto vino del archivo y no hay ultima-*.json que coincida
        public FotoCadena Foto;
        public SubCboe Sub;
        public DateTime GeneradoUtc;
        public long Version;
    }

    public sealed class BajadorCboe : IFuenteCboe, IDisposable
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Instantanea
        {
            public static readonly Instantanea Vacia = new Instantanea { Fotos = Array.Empty<FotoCadena>() };
            public FotoCadena[] Fotos;   // por GeneradoUtc ascendente; nunca se modifica despues de publicada
        }

        private sealed class LibroCboe
        {
            public string Nombre, Archivo, Simbolo;
            public Instantanea Inst = Instantanea.Vacia;
            public RegistroCboe Ultimo;
            public readonly HashSet<string> Sellos = new HashSet<string>(StringComparer.Ordinal);
            public string UltimoTs = "";
            public DateTime UltimoTsUtc;
            public int PodadoHasta;
            // estado de la bajada (solo lo toca el hilo)
            public int Fallos; public DateTime ProximoIntentoUtc; public string UltimoError = ""; public DateTime UltimoLogFalloUtc;
            public DateTime ProximaBajadaUtc;       // 4.1.0: inicio del ultimo intento + cadencia (75 s rueda / 300 s fuera): nada la adelanta
            public DateTime FallandoDesdeUtc;       // 4.1.0: primer fallo de la racha actual (default = responde bien)
            public DateTime UltimaBajadaOkUtc; public long UltimoMs, UltimoGz; public int UltimoHttp;
            public DateTime UltimaEscrituraVista;   // seguidor: LastWriteTimeUtc de ultima-*.json
        }

        private readonly OpcionesCboe _o;
        private readonly LogCboe _log;
        private readonly LibroCboe[] _libros;
        private readonly Dictionary<string, LibroCboe> _porNombre = new Dictionary<string, LibroCboe>(StringComparer.OrdinalIgnoreCase);
        private readonly ConditionalWeakTable<FotoCadena, SubCboe> _subs = new ConditionalWeakTable<FotoCadena, SubCboe>();
        private readonly CierresCboe _cierres = new CierresCboe();
        private readonly object _llaveArranque = new object();
        private readonly ManualResetEventSlim _despertar = new ManualResetEventSlim(false);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private IHttpCboe _http;
        private Thread _hilo;
        private volatile bool _parar, _terminado, _esLider, _historiaCargada;
        private volatile bool _bajar;
        private volatile int _topeKBps;
        private long _version;
        private Mutex _mutex;
        private bool _avisoSeguidor;
        private DateTime _pausaGlobalHasta;
        private string _modo = "arrancando";

        public BajadorCboe(OpcionesCboe opciones = null)
        {
            _o = opciones ?? new OpcionesCboe();
            _log = new LogCboe(_o.Log);
            _bajar = _o.Bajar;
            _topeKBps = Math.Max(16, _o.TopeKBps);
            var l = new List<LibroCboe>();
            foreach (var n in _o.Libros)
            {
                var x = Describir(n);
                if (x == null || _porNombre.ContainsKey(x.Nombre)) continue;
                l.Add(x);
                _porNombre[x.Nombre] = x; _porNombre[x.Archivo] = x; _porNombre[x.Simbolo] = x;
            }
            _libros = l.ToArray();
        }

        private static LibroCboe Describir(string n)
        {
            switch ((n ?? "").ToUpperInvariant().TrimStart('_', '^'))
            {
                case "NDX": case "NQ": return new LibroCboe { Nombre = "NDX", Archivo = "NQ", Simbolo = "_NDX" };
                case "QQQ": return new LibroCboe { Nombre = "QQQ", Archivo = "QQQ", Simbolo = "QQQ" };
                case "TQQQ": return new LibroCboe { Nombre = "TQQQ", Archivo = "TQQQ", Simbolo = "TQQQ" };
                default: return null;
            }
        }

        // ================================================================== IFuenteCboe

        public void Arrancar()
        {
            lock (_llaveArranque)
            {
                if (_hilo != null || _terminado) return;
                _hilo = new Thread(Bucle) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "PythiaGex4 cboe" };
                _hilo.Start();
            }
        }

        public void Parar()
        {
            Thread h;
            lock (_llaveArranque) { h = _hilo; _parar = true; _terminado = true; }
            try { _cts.Cancel(); } catch { }
            _despertar.Set();
            if (h != null && h != Thread.CurrentThread)
            {
                try { if (!h.Join(2000)) _log.Linea("parar: el hilo no termino en 2 s (sigue en segundo plano y se cierra solo)"); } catch { }
            }
        }

        public void Dispose() => Parar();

        public IReadOnlyList<FotoCadena> Fotos(string libro, DateTime desdeUtc, DateTime hastaUtc)
        {
            if (!_porNombre.TryGetValue(libro ?? "", out var L)) return Array.Empty<FotoCadena>();
            var arr = Volatile.Read(ref L.Inst).Fotos;
            int a = Cota(arr, desdeUtc), b = Cota(arr, hastaUtc);
            return b > a ? new ArraySegment<FotoCadena>(arr, a, b - a) : (IReadOnlyList<FotoCadena>)Array.Empty<FotoCadena>();
        }

        public FotoCadena Ultima(string libro)
        {
            if (!_porNombre.TryGetValue(libro ?? "", out var L)) return null;
            var arr = Volatile.Read(ref L.Inst).Fotos;
            return arr.Length > 0 ? arr[arr.Length - 1] : null;
        }

        public long Version => Interlocked.Read(ref _version);

        public string Estado
        {
            get
            {
                try { return ArmarEstado(_o.AhoraUtc()); } catch (Exception e) { return "cboe: " + e.Message; }
            }
        }

        // ================================================================== extras (no estan en el contrato)

        /// <summary>Cboe41Bajar: false = no baja (las fotos del archivo siguen). 4.1.0: solo el paso de false a true despierta al hilo, y aun asi
        /// cada ticker respeta su cadencia (ProximaBajadaUtc): prender y apagar no adelanta ninguna bajada (antes: una vuelta entera por cambio).</summary>
        public bool Bajar { get => _bajar; set { bool subir = value && !_bajar; _bajar = value; if (subir) _despertar.Set(); } }
        /// <summary>Cboe41TopeKBps: tope de la bajada (la pausa entre trozos se recalcula).</summary>
        public int TopeKBps { get => _topeKBps; set => _topeKBps = Math.Max(16, value); }
        public bool EsDueno => _esLider;

        /// <summary>4.1.0 (cartel "sin internet"): desde cuando falla la bajada (el primer fallo de la racha mas vieja entre los tickers que
        /// estan fallando), cuales y el ultimo error. Desde = default si todos responden (o si la descarga esta apagada o la baja otro proceso).</summary>
        public (DateTime Desde, string Libros, string Error) Falla()
        {
            DateTime d = default; var nombres = new List<string>(3); string err = "";
            if (!_bajar || !_esLider) return (d, "", "");
            foreach (var L in _libros)
            {
                if (L.Fallos <= 0 || L.FallandoDesdeUtc == default) continue;
                nombres.Add(L.Nombre);
                if (d == default || L.FallandoDesdeUtc < d) { d = L.FallandoDesdeUtc; err = L.UltimoError ?? ""; }
            }
            return (d, string.Join("/", nombres), err);
        }
        public bool HistoriaCargada => _historiaCargada;
        public string RutaLog => _log.Ruta;
        public string Carpeta => _o.Carpeta;

        /// <summary>La ultima linea aceptada (texto flaco + foto). Para las capas del clon de la 3.0 (TRES_NDX / TRES_QQQ).</summary>
        public RegistroCboe UltimoRegistro(string libro) => _porNombre.TryGetValue(libro ?? "", out var L) ? Volatile.Read(ref L.Ultimo) : null;

        /// <summary>data.* de la foto (null si vino de una linea sin bloque "sub", p. ej. cboe-local).</summary>
        public SubCboe Sub(FotoCadena f) => f != null && _subs.TryGetValue(f, out var s) ? s : null;

        /// <summary>Lo observado del cierre de un subyacente para una fecha NY (cierres.json). null si no hay nada.</summary>
        public CierreCboe Cierre(string libro, DateTime fechaNy)
        {
            if (!_porNombre.TryGetValue(libro ?? "", out var L)) return null;
            return _cierres.Ver(L.Nombre, fechaNy.ToString("yyyy-MM-dd", Inv));
        }

        /// <summary>Si ahora corre la cadencia de rueda (75 s).</summary>
        public bool EnRueda(DateTime utc)
        {
            if (_o.VentanaUtcPython)
            {
                if (utc.DayOfWeek == DayOfWeek.Saturday || utc.DayOfWeek == DayOfWeek.Sunday) return false;
                int hm = utc.Hour * 60 + utc.Minute;
                return 13 * 60 + 20 <= hm && hm <= 20 * 60 + 10;
            }
            var ny = HoraNyCboe.DeUtc(utc);
            if (ny.DayOfWeek == DayOfWeek.Saturday || ny.DayOfWeek == DayOfWeek.Sunday) return false;
            int m = ny.Hour * 60 + ny.Minute;
            return 9 * 60 + 20 <= m && m <= 16 * 60 + 10;
        }

        // ================================================================== el hilo

        private void Bucle()
        {
            try
            {
                Directory.CreateDirectory(_o.Carpeta);
                _log.Linea("arranca: carpeta " + _o.Carpeta + ", bajar " + _bajar + ", tope " + _topeKBps + " KB/s, trozo " + (_o.TrozoBytes >> 10) + " KB, socket "
                           + (_o.BufferSocketBytes >> 10) + " KB, ventana " + (_o.VentanaUtcPython ? "13:20-20:10 UTC (paridad cboe_local)" : "09:20-16:10 NY") + ", libros " + string.Join("/", Array.ConvertAll(_libros, x => x.Nombre)));
                _cierres.Cargar(CboeArchivo.LeerTexto(CboeArchivo.RutaCierres(_o.Carpeta)));
                try { CargarHistoria(); } catch (Exception e) { _log.Linea("historia: fallo " + e.GetType().Name + ": " + e.Message); }
                _historiaCargada = true;
                Interlocked.Increment(ref _version);
                Esperar(TimeSpan.FromSeconds(Math.Max(0, _o.RetrasoInicialS)));
                DateTime ultimaPoda = default;
                while (!_parar)
                {
                    try
                    {
                        DateTime ahora = _o.AhoraUtc();
                        if (!_bajar)
                        {
                            // 4.1.0: con la descarga apagada NO se toma (o se suelta) el mutex: otro proceso con la descarga prendida puede bajar,
                            // y este sigue sus ultima-*.json (antes: el apagado se quedaba con el mutex y nadie bajaba)
                            SoltarDueno();
                            if (_modo != "apagada") _log.Linea("modo: descarga apagada (Cboe41Bajar = false): no tomo el mutex; sigo ultima-*.json si baja otro proceso");
                            _modo = "apagada";
                            Seguir();
                            Esperar(TimeSpan.FromSeconds(Math.Max(1, _o.SeguirCadaS)));
                            continue;
                        }
                        bool lider = AsegurarDueno();
                        if (!lider)
                        {
                            _modo = "seguidor";
                            Seguir();
                            Esperar(TimeSpan.FromSeconds(Math.Max(1, _o.SeguirCadaS)));
                            continue;
                        }
                        if (_o.RetencionDias > 0 && ahora.Date != ultimaPoda)
                        {
                            ultimaPoda = ahora.Date;
                            CboeArchivo.Podar(_o.Carpeta, _o.RetencionDias, ahora, _log);
                        }
                        if (_modo != "dueno") _log.Linea("modo: dueno de la descarga");
                        _modo = "dueno";
                        if (ahora < _pausaGlobalHasta)
                        {
                            Esperar(Min(_pausaGlobalHasta - ahora, TimeSpan.FromSeconds(30)));
                            continue;
                        }
                        // 4.1.0: la cadencia es POR TICKER (ProximaBajadaUtc = inicio de su ultimo intento + 75/300 s). Un despertar (prender la
                        // descarga, Parar) solo reevalua: nunca adelanta una bajada. Antes la espera entre vueltas se cortaba con cualquier
                        // despertar y la vuelta siguiente bajaba los tres de nuevo (rafagas, medido con HTTP falso: +33 pedidos en 11 s).
                        bool primero = true;
                        foreach (var L in _libros)
                        {
                            if (_parar || !_bajar) break;
                            var ya = _o.AhoraUtc();
                            if (ya < L.ProximoIntentoUtc || ya < L.ProximaBajadaUtc) continue;
                            if (!primero) { PausaFija(_o.PausaEntreTickersMs); if (_parar || !_bajar) break; }
                            primero = false;
                            var inicio = _o.AhoraUtc();
                            L.ProximaBajadaUtc = inicio.AddSeconds(EnRueda(inicio) ? _o.CadenciaRuedaS : _o.CadenciaFueraS);
                            BajarYProcesar(L);
                            if (_o.AhoraUtc() < _pausaGlobalHasta) break;
                        }
                        var ahora2 = _o.AhoraUtc();
                        DateTime prox = DateTime.MaxValue;
                        foreach (var L in _libros)
                        {
                            var p = L.ProximaBajadaUtc > L.ProximoIntentoUtc ? L.ProximaBajadaUtc : L.ProximoIntentoUtc;
                            if (p < prox) prox = p;
                        }
                        var espera = prox == DateTime.MaxValue ? TimeSpan.FromSeconds(30) : prox - ahora2;
                        double minS = Math.Min(1.0, Math.Max(0.05, Math.Min(_o.CadenciaRuedaS, _o.CadenciaFueraS)));
                        if (espera < TimeSpan.FromSeconds(minS)) espera = TimeSpan.FromSeconds(minS);
                        Esperar(Min(espera, TimeSpan.FromSeconds(30)));
                    }
                    catch (Exception e)
                    {
                        _log.Linea("vuelta fallo: " + e.GetType().Name + ": " + e.Message);
                        Esperar(TimeSpan.FromSeconds(30));
                    }
                }
            }
            catch (Exception e)
            {
                _log.Linea("hilo fallo: " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                try { if (_esLider && _mutex != null) _mutex.ReleaseMutex(); } catch { }
                try { _mutex?.Dispose(); } catch { }
                try { (_http as IDisposable)?.Dispose(); } catch { }
                _log.Linea("se detiene");
            }
        }

        private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

        /// <summary>Pausa entre tickers que un despertar NO corta (solo Parar): asi prender la descarga no junta dos bajadas.</summary>
        private void PausaFija(int ms)
        {
            if (_parar || ms <= 0) return;
            try { _cts.Token.WaitHandle.WaitOne(ms); } catch { }
        }

        /// <summary>4.1.0: suelta el mutex (descarga apagada). Lo llama el hilo del bucle, que es el que lo tomo.</summary>
        private void SoltarDueno()
        {
            if (!_esLider) return;
            _esLider = false;
            if (_mutex != null)
            {
                try { _mutex.ReleaseMutex(); _log.Linea("modo: suelto el mutex " + _o.NombreMutex + " (descarga apagada): si otro proceso la tiene prendida, baja el"); } catch { }
            }
            _avisoSeguidor = false;
        }

        private void Esperar(TimeSpan t)
        {
            if (_parar || t <= TimeSpan.Zero) return;
            try { _despertar.Wait(t, _cts.Token); } catch (OperationCanceledException) { }
            if (!_parar) _despertar.Reset();
        }

        private bool AsegurarDueno()
        {
            if (_esLider) return true;
            if (string.IsNullOrEmpty(_o.NombreMutex)) { _esLider = true; return true; }
            try
            {
                _mutex ??= new Mutex(false, _o.NombreMutex);
                bool ok;
                try { ok = _mutex.WaitOne(0); }
                catch (AbandonedMutexException) { ok = true; }
                if (ok)
                {
                    _esLider = true;
                    // si antes baja otro, su ultimo sello manda
                    foreach (var L in _libros)
                    {
                        var s = CboeArchivo.LeerSello(_o.Carpeta, L.Archivo);
                        if (s.Length > 0 && string.CompareOrdinal(s, L.UltimoTs) > 0) Sello(L, s);
                    }
                    if (_avisoSeguidor) _log.Linea("modo: este proceso pasa a ser el dueno de la descarga");
                    return true;
                }
                if (!_avisoSeguidor) { _avisoSeguidor = true; _log.Linea("modo: seguidor (otro proceso tiene " + _o.NombreMutex + "; leo ultima-*.json cada " + _o.SeguirCadaS + " s)"); }
                return false;
            }
            catch (Exception e)
            {
                _log.Linea("mutex: " + e.Message + " (bajo igual)");
                _esLider = true;
                return true;
            }
        }

        private static void Sello(LibroCboe L, string ts)
        {
            L.UltimoTs = ts ?? "";
            L.UltimoTsUtc = CboeCadena.ParsearTs(L.UltimoTs, out var u) ? u : default;
        }

        // ================================================================== una bajada

        private void BajarYProcesar(LibroCboe L)
        {
            _http ??= _o.Http ?? new HttpCboe(_o.BufferSocketBytes);
            int trozo = Math.Max(4096, _o.TrozoBytes);
            int pausa = (int)Math.Round(trozo * 1000.0 / (_topeKBps * 1024.0));
            var r = _http.Bajar(L.Simbolo, trozo, pausa, _cts.Token);
            DateTime gen = Micro(_o.AhoraUtc());                     // generado = al terminar la bajada (como bajar_y_archivar)
            L.UltimoHttp = r.Http; L.UltimoMs = r.Ms; L.UltimoGz = r.GzLargo;
            if (!r.Ok) { Fallo(L, r.Http, r.Error, gen); return; }
            try { _o.AlBajarCrudo?.Invoke(L.Simbolo, r.Gz, r.GzLargo, gen); } catch { }
            string cab = L.Nombre + " http " + r.Http + (r.UsoRespaldo ? " (host de respaldo)" : "") + " gz " + r.GzLargo + " B json " + r.JsonLargo + " B " + r.Ms + " ms" + (r.SinGzip ? " SIN GZIP" : "");
            CrudoCboe cr;
            try { cr = CrudoCboe.Leer(new ReadOnlySpan<byte>(r.Json, 0, r.JsonLargo)); }
            catch (Exception e) { Fallo(L, r.Http, "JSON ilegible: " + e.Message, gen); return; }
            if (!CboeCadena.ParsearTs(cr.Timestamp, out var tsUtc)) { Fallo(L, r.Http, "timestamp ilegible '" + cr.Timestamp + "'", gen); return; }
            if (!(cr.CurrentPrice.V > 0)) { Fallo(L, r.Http, "current_price " + cr.CurrentPrice, gen); return; }
            if (cr.N <= 100) { Fallo(L, r.Http, "solo " + cr.N + " contratos", gen); return; }
            if (L.Fallos > 0) _log.Linea(L.Nombre + ": vuelve a responder tras " + L.Fallos + " fallo(s)");
            L.Fallos = 0; L.ProximoIntentoUtc = default; L.UltimaBajadaOkUtc = gen; L.UltimoError = ""; L.FallandoDesdeUtc = default;
            if (string.Equals(cr.Timestamp, L.UltimoTs, StringComparison.Ordinal))
            {
                _log.Linea(cab + " | ts " + cr.Timestamp + " | mismo sello de CBOE, no se repite");
                return;
            }
            if (L.UltimoTsUtc != default && tsUtc < L.UltimoTsUtc)
            {
                _log.Linea(cab + " | ts " + cr.Timestamp + " ANTERIOR al ultimo aceptado (" + L.UltimoTs + "): servidor atrasado, se descarta");
                return;
            }
            var cad = CboeCadena.Construir(cr, gen);
            BaseCboe b = null;
            try { b = CboeCadena.Medir(cr, _o.HoyLocal(gen)); } catch (Exception e) { _log.Linea(L.Nombre + ": medir fallo (" + e.Message + "), base_cruda = null"); }
            var info = new InfoBajada { Url = r.Url, Http = r.Http, BytesGz = r.GzLargo, Bytes = r.JsonLargo, Ms = r.Ms };
            string texto = CboeCadena.LineaFlaca(cad, cr, b, gen, info);
            var linea = CboeCadena.DeLinea(texto);
            if (linea == null)
            {
                _log.Linea(cab + " | ts " + cr.Timestamp + " | cadena sin filas a <= 8 dias (" + cad.Filas.Length + " en 14 dias), no se anota");
                return;
            }
            string disco = "";
            try
            {
                CboeArchivo.AgregarLinea(CboeArchivo.RutaDia(_o.Carpeta, L.Archivo, gen), texto);
                CboeArchivo.AnotarSello(_o.Carpeta, L.Archivo, cr.Timestamp);
                CboeArchivo.EscribirAtomico(CboeArchivo.RutaUltima(_o.Carpeta, L.Archivo), texto);
            }
            catch (Exception e) { disco = " | DISCO: " + e.Message; }
            Sello(L, cr.Timestamp);
            Insertar(L, new List<LineaCboe> { linea }, texto);
            Observar(L, linea, gen);
            var edad = gen - linea.DatoUtc;
            _log.Linea(cab + " | ts " + cr.Timestamp + " | dato " + Edad(edad) + " (ultimo_trade " + (cad.UltimoTrade.Length > 0 ? cad.UltimoTrade : "?") + " NY) | spot "
                       + cad.SpotIdx + " | filas " + linea.NFilas + " (<= 8 d) de " + cad.Filas.Length + " | base_cruda " + (b != null ? PyCboe.Repr(b.Base) + " err " + PyCboe.Repr(b.ResiduoTicks) + " tk" : "null")
                       + " | aceptada" + disco);
        }

        private void Fallo(LibroCboe L, int http, string error, DateTime ahora)
        {
            L.Fallos++;
            if (L.Fallos == 1) L.FallandoDesdeUtc = ahora;
            L.UltimoError = error ?? "";
            string extra = "";
            if (http == 403 || http == 429)
            {
                _pausaGlobalHasta = ahora.AddSeconds(_o.EsperaCloudflareS);
                extra = " -> todo espera " + _o.EsperaCloudflareS + " s (Cloudflare)";
            }
            else if (L.Fallos >= _o.FallosParaEsperar)
            {
                L.ProximoIntentoUtc = ahora.AddSeconds(_o.EsperaTrasFallosS);
                extra = " -> " + L.Nombre + " se reintenta cada " + _o.EsperaTrasFallosS + " s hasta que vuelva";
            }
            if (L.Fallos == 1 || (ahora - L.UltimoLogFalloUtc).TotalSeconds >= 60 || extra.Length > 0)
            {
                L.UltimoLogFalloUtc = ahora;
                _log.Linea(L.Nombre + ": fallo " + L.Fallos + " seguido: " + L.UltimoError + (http > 0 ? " (http " + http + ")" : "") + extra + "; sigue la ultima cadena buena");
            }
        }

        private void Observar(LibroCboe L, LineaCboe x, DateTime vistoUtc)
        {
            if (x.Sub == null) return;
            _cierres.Observar(L.Nombre, x.Sub, x.TsUtc, vistoUtc);
            if (_cierres.Cambiado && _esLider)
            {
                _cierres.Cambiado = false;
                try { CboeArchivo.EscribirAtomico(CboeArchivo.RutaCierres(_o.Carpeta), _cierres.Json()); } catch (Exception e) { _log.Linea("cierres.json: " + e.Message); }
            }
        }

        private static DateTime Micro(DateTime t) => new DateTime(t.Ticks - t.Ticks % 10, DateTimeKind.Utc);

        // ================================================================== el almacen

        private static int Cota(FotoCadena[] a, DateTime t)
        {
            int lo = 0, hi = a.Length;
            while (lo < hi) { int m = (lo + hi) >> 1; if (a[m].GeneradoUtc < t) lo = m + 1; else hi = m; }
            return lo;
        }

        /// <summary>Agrega fotos (ya leidas) al libro. Solo el hilo del descargador escribe. Dedup por sello: gana la mas temprana.</summary>
        private void Insertar(LibroCboe L, List<LineaCboe> nuevas, string textoUltima)
        {
            if (nuevas.Count == 0) return;
            var viejo = Volatile.Read(ref L.Inst).Fotos;
            var lista = new List<FotoCadena>(viejo.Length + nuevas.Count);
            lista.AddRange(viejo);
            bool desordenado = false;
            DateTime ultimoGen = viejo.Length > 0 ? viejo[viejo.Length - 1].GeneradoUtc : DateTime.MinValue;
            FotoCadena ultimaNueva = null; LineaCboe ultimaLinea = null;
            foreach (var x in nuevas)
            {
                if (!L.Sellos.Add(x.CadenaTs)) continue;
                var f = x.AFoto(L.Nombre);
                if (x.Sub != null) _subs.AddOrUpdate(f, x.Sub);
                // la noche repite el contenido con otro sello: se comparte el arreglo de filas (misma memoria, mismo dato)
                var prev = lista.Count > 0 ? lista[lista.Count - 1] : null;
                if (prev != null && prev.Filas.Length > 0 && MismasFilas(prev.Filas, f.Filas)) f.Filas = prev.Filas;
                if (f.GeneradoUtc < ultimoGen) desordenado = true;
                ultimoGen = f.GeneradoUtc > ultimoGen ? f.GeneradoUtc : ultimoGen;
                lista.Add(f);
                if (ultimaNueva == null || f.GeneradoUtc >= ultimaNueva.GeneradoUtc) { ultimaNueva = f; ultimaLinea = x; }
            }
            if (ultimaNueva == null) return;
            if (desordenado) { lista.Sort((a, b) => a.GeneradoUtc.CompareTo(b.GeneradoUtc)); L.PodadoHasta = 0; }
            var arr = lista.ToArray();
            PodarFilas(L, arr);
            Volatile.Write(ref L.Inst, new Instantanea { Fotos = arr });
            var ult = arr[arr.Length - 1];
            if (ReferenceEquals(ult, ultimaNueva))
            {
                Volatile.Write(ref L.Ultimo, new RegistroCboe
                {
                    Libro = L.Nombre, Archivo = L.Archivo, Texto = textoUltima, Foto = ult, Sub = ultimaLinea.Sub, GeneradoUtc = ult.GeneradoUtc,
                    Version = Interlocked.Increment(ref _version),
                });
            }
            else Interlocked.Increment(ref _version);
        }

        private static bool MismasFilas(FilaCadena[] a, FilaCadena[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                var x = a[i]; var y = b[i];
                if (x.K != y.K || x.V != y.V || x.OiC != y.OiC || x.OiP != y.OiP || x.IvC != y.IvC || x.IvP != y.IvP || x.VolC != y.VolC || x.VolP != y.VolP) return false;
            }
            return true;
        }

        /// <summary>Las fotos con GeneradoUtc anterior al corte (ahora - HorasConFilas) pasan a livianas, salvo la ultima anterior al corte
        /// (es la que vale en el primer minuto despues del corte). Se reemplazan por copias: lo ya publicado no se toca.</summary>
        private void PodarFilas(LibroCboe L, FotoCadena[] arr)
        {
            if (_o.HorasConFilas <= 0) return;
            var corte = Corte(_o.AhoraUtc());
            int j = Cota(arr, corte) - 1;            // ultima anterior al corte: conserva filas
            for (int i = Math.Max(0, L.PodadoHasta); i < j; i++)
            {
                var f = arr[i];
                if (f.Filas.Length == 0) continue;
                var liv = Liviana(f);
                if (_subs.TryGetValue(f, out var s)) _subs.AddOrUpdate(liv, s);
                arr[i] = liv;
            }
            if (j > L.PodadoHasta) L.PodadoHasta = j;
        }

        /// <summary>Desde cuando las fotos conservan filas: el mas viejo entre ahora - HorasConFilas y las 09:00 NY del dia habil anterior
        /// (lun-vie; sin calendario de feriados) a la fecha NY de ahora. Un lunes a la noche guarda con filas desde el viernes 09:00 NY.</summary>
        public DateTime Corte(DateTime ahoraUtc)
        {
            var porHoras = ahoraUtc.AddHours(-_o.HorasConFilas);
            var d = HoraNyCboe.DeUtc(ahoraUtc).Date.AddDays(-1);
            while (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(-1);
            var rueda = HoraNyCboe.AUtc(d.AddHours(9));
            return rueda < porHoras ? rueda : porHoras;
        }

        private static FotoCadena Liviana(FotoCadena f) => new FotoCadena
        {
            Libro = f.Libro, GeneradoUtc = f.GeneradoUtc, TsUtc = f.TsUtc, DatoUtc = f.DatoUtc, Spot = f.Spot, Dias = f.Dias,
            Filas = Array.Empty<FilaCadena>(), EsFuturo = f.EsFuturo, OiTotal = f.OiTotal, BaseCruda = f.BaseCruda, BaseErrorTicks = f.BaseErrorTicks,
            CierreAnterior = f.CierreAnterior, FuturoFoto = f.FuturoFoto,
        };

        // ================================================================== historia y seguidor

        private void CargarHistoria()
        {
            var ahora = _o.AhoraUtc();
            var corte = _o.HorasConFilas > 0 ? Corte(ahora) : DateTime.MinValue;
            foreach (var L in _libros)
            {
                if (_parar) return;
                var todas = new List<(LineaCboe X, string Ruta)>();
                int ileg = 0, archivos = 0;
                for (int d = Math.Max(0, _o.DiasHistoria); d >= 0; d--)
                {
                    var ruta = CboeArchivo.RutaDia(_o.Carpeta, L.Archivo, ahora.Date.AddDays(-d));
                    if (!File.Exists(ruta)) continue;
                    archivos++;
                    try
                    {
                        var leidas = CboeArchivo.LeerDia(ruta, g => g >= corte, out int il);
                        ileg += il;
                        foreach (var x in leidas) todas.Add((x, ruta));
                    }
                    catch (Exception e) { _log.Linea("historia " + Path.GetFileName(ruta) + ": " + e.Message); }
                    Thread.Sleep(20);
                    if (_parar) return;
                }
                // dedup por sello: gana el generado mas temprano (libros.fotos_cboe)
                todas.Sort((a, b) => a.X.GeneradoUtc.CompareTo(b.X.GeneradoUtc));
                var vistos = new HashSet<string>(StringComparer.Ordinal);
                var lineas = new List<LineaCboe>(todas.Count);
                var rutas = new List<string>(todas.Count);
                foreach (var t in todas) if (vistos.Add(t.X.CadenaTs)) { lineas.Add(t.X); rutas.Add(t.Ruta); }
                // la ultima anterior al corte necesita sus filas: se relee solo esa linea
                int j = -1;
                for (int i = 0; i < lineas.Count; i++) if (lineas[i].GeneradoUtc < corte) j = i;
                if (j >= 0 && lineas[j].Filas == null)
                {
                    var g = lineas[j].GeneradoUtc; var ts = lineas[j].CadenaTs;
                    try
                    {
                        foreach (var x in CboeArchivo.LeerDia(rutas[j], gg => gg == g, out _))
                            if (x.GeneradoUtc == g && x.CadenaTs == ts && x.Filas != null) { lineas[j] = x; break; }
                    }
                    catch { }
                }
                // la ultima-*.json da el texto de la ultima linea (y, si el archivo del dia fallo, la foto misma)
                string textoUlt = CboeArchivo.LeerTexto(CboeArchivo.RutaUltima(_o.Carpeta, L.Archivo));
                var xu = textoUlt != null ? CboeCadena.DeLinea(textoUlt) : null;
                if (xu != null && !vistos.Contains(xu.CadenaTs) && xu.GeneradoUtc >= ahora.Date.AddDays(-Math.Max(0, _o.DiasHistoria))) lineas.Add(xu);
                Insertar(L, lineas, null);
                var u = Volatile.Read(ref L.Ultimo);
                if (u != null && xu != null && u.Foto.TsUtc == xu.TsUtc && u.GeneradoUtc == xu.GeneradoUtc)
                    Volatile.Write(ref L.Ultimo, new RegistroCboe { Libro = u.Libro, Archivo = u.Archivo, Texto = textoUlt, Foto = u.Foto, Sub = u.Sub, GeneradoUtc = u.GeneradoUtc, Version = u.Version });
                foreach (var x in lineas) if (x.Sub != null) _cierres.Observar(L.Nombre, x.Sub, x.TsUtc, x.GeneradoUtc);
                string sello = CboeArchivo.LeerSello(_o.Carpeta, L.Archivo);
                var arr = Volatile.Read(ref L.Inst).Fotos;
                string tsUltFoto = lineas.Count > 0 ? lineas[lineas.Count - 1].CadenaTs : "";
                Sello(L, string.CompareOrdinal(sello, tsUltFoto) >= 0 ? sello : tsUltFoto);
                int conFilas = 0; foreach (var f in arr) if (f.Filas.Length > 0) conFilas++;
                _log.Linea("historia " + L.Nombre + ": " + archivos + " archivo(s), " + arr.Length + " fotos (" + conFilas + " con filas; " + ileg + " lineas sin filas o ilegibles salteadas, como foto_cboe_de_json)" + (arr.Length > 0 ? ", de "
                           + CboeCadena.IsoGenerado(arr[0].GeneradoUtc) + " a " + CboeCadena.IsoGenerado(arr[arr.Length - 1].GeneradoUtc) : "") + ", ultimo sello " + (L.UltimoTs.Length > 0 ? L.UltimoTs : "-"));
            }
            if (_cierres.Cambiado) _cierres.Cambiado = false;   // lo de la historia ya esta en el archivo (o se reescribe con la proxima foto)
        }

        /// <summary>Seguidor: otro proceso baja; se leen sus ultima-*.json cuando cambian.</summary>
        private void Seguir()
        {
            foreach (var L in _libros)
            {
                try
                {
                    var ruta = CboeArchivo.RutaUltima(_o.Carpeta, L.Archivo);
                    var fi = new FileInfo(ruta);
                    if (!fi.Exists || fi.LastWriteTimeUtc == L.UltimaEscrituraVista) continue;
                    L.UltimaEscrituraVista = fi.LastWriteTimeUtc;
                    var texto = CboeArchivo.LeerTexto(ruta);
                    var x = texto != null ? CboeCadena.DeLinea(texto) : null;
                    if (x == null || L.Sellos.Contains(x.CadenaTs)) continue;
                    Sello(L, x.CadenaTs);
                    Insertar(L, new List<LineaCboe> { x }, texto);
                    if (x.Sub != null) _cierres.Observar(L.Nombre, x.Sub, x.TsUtc, x.GeneradoUtc);
                }
                catch (Exception e) { _log.Linea("seguidor " + L.Nombre + ": " + e.Message); }
            }
        }

        // ================================================================== estado para la pestaña

        public static string Edad(TimeSpan t)
        {
            double s = Math.Max(0, t.TotalSeconds);
            if (s < 120) return "hace " + ((int)s).ToString(Inv) + " s";
            if (s < 120 * 60) return "hace " + ((int)(s / 60)).ToString(Inv) + " min";
            int h = (int)(s / 3600), m = (int)((s - h * 3600) / 60);
            return "hace " + h.ToString(Inv) + " h " + m.ToString(Inv) + " min";
        }

        private string ArmarEstado(DateTime ahora)
        {
            var sb = new StringBuilder(256);
            if (!_historiaCargada) sb.Append("leyendo historia · ");
            if (_modo == "seguidor") sb.Append("(baja otro proceso) ");
            for (int i = 0; i < _libros.Length; i++)
            {
                var L = _libros[i];
                if (i > 0) sb.Append(" · ");
                sb.Append(L.Nombre).Append(' ');
                var arr = Volatile.Read(ref L.Inst).Fotos;
                var u = arr.Length > 0 ? arr[arr.Length - 1] : null;
                if (u == null) sb.Append("sin datos");
                else
                {
                    var edad = ahora - u.DatoUtc;
                    sb.Append("dato ").Append(Edad(edad));
                    if (edad.TotalMinutes > 30) sb.Append(" (VIEJO)");
                }
                if (!_bajar) sb.Append(", descarga apagada");
                else if (L.Fallos > 0)
                {
                    sb.Append(", falla: ").Append(L.UltimoError.Length > 40 ? L.UltimoError.Substring(0, 40) : L.UltimoError);
                    var prox = L.ProximoIntentoUtc > ahora ? L.ProximoIntentoUtc : (ahora < _pausaGlobalHasta ? _pausaGlobalHasta : DateTime.MinValue);
                    if (prox > ahora) sb.Append(" (reintento en ").Append(((int)(prox - ahora).TotalSeconds).ToString(Inv)).Append(" s)");
                }
                else if (L.UltimaBajadaOkUtc != default) sb.Append(", bajado ").Append(Edad(ahora - L.UltimaBajadaOkUtc));
            }
            return sb.ToString();
        }
    }

    // ================================================================== un descargador por proceso

    /// <summary>Un solo BajadorCboe por proceso, compartido por todas las instancias del indicador (contador de referencias).</summary>
    public static class FuenteCboe
    {
        private static readonly object _llave = new object();
        private static BajadorCboe _motor;
        private static int _refs;

        /// <summary>Devuelve una manija: Arrancar() suma una referencia (arranca el motor si es el primero), Parar() la suelta (el ultimo lo detiene).
        /// Las opciones valen solo si es la primera manija del proceso.</summary>
        public static ManijaCboe Obtener(OpcionesCboe opciones = null) => new ManijaCboe(opciones);

        /// <summary>4.1.0: la ultima linea aceptada del descargador VIGENTE del proceso (null si no hay ninguno). Las capas del clon de la 3.0
        /// la leen por aca: se asigna una vez y no hay que anularla al parar un host (antes un Parar tardio la borraba al host nuevo).</summary>
        public static RegistroCboe UltimoRegistroVigente(string libro) { var m = Volatile.Read(ref _motor); return m?.UltimoRegistro(libro); }

        internal static BajadorCboe Tomar(OpcionesCboe o)
        {
            lock (_llave)
            {
                if (_motor == null) { _motor = new BajadorCboe(o); _refs = 0; }
                _refs++;
                _motor.Arrancar();
                return _motor;
            }
        }

        internal static void Soltar(BajadorCboe m)
        {
            BajadorCboe parar = null;
            lock (_llave)
            {
                if (m == null || !ReferenceEquals(m, _motor)) return;
                if (--_refs <= 0) { parar = _motor; _motor = null; _refs = 0; }
            }
            parar?.Parar();
        }
    }

    /// <summary>La IFuenteCboe que usa cada instancia del indicador.</summary>
    public sealed class ManijaCboe : IFuenteCboe
    {
        private readonly OpcionesCboe _o;
        private BajadorCboe _m;
        private readonly object _llave = new object();
        internal ManijaCboe(OpcionesCboe o) { _o = o; }

        public BajadorCboe Motor => _m;
        public void Arrancar() { lock (_llave) { if (_m == null) _m = FuenteCboe.Tomar(_o); } }
        public void Parar() { BajadorCboe m; lock (_llave) { m = _m; _m = null; } FuenteCboe.Soltar(m); }
        public IReadOnlyList<FotoCadena> Fotos(string libro, DateTime desdeUtc, DateTime hastaUtc) => _m?.Fotos(libro, desdeUtc, hastaUtc) ?? Array.Empty<FotoCadena>();
        public FotoCadena Ultima(string libro) => _m?.Ultima(libro);
        public long Version => _m?.Version ?? 0;
        public string Estado => _m?.Estado ?? "cboe: sin arrancar";
        public RegistroCboe UltimoRegistro(string libro) => _m?.UltimoRegistro(libro);
        public SubCboe Sub(FotoCadena f) => _m?.Sub(f);
        public CierreCboe Cierre(string libro, DateTime fechaNy) => _m?.Cierre(libro, fechaNy);
        public bool Bajar { get => _m?.Bajar ?? false; set { var m = _m; if (m != null) m.Bajar = value; } }
        public int TopeKBps { get => _m?.TopeKBps ?? 0; set { var m = _m; if (m != null) m.TopeKBps = value; } }
    }
}
