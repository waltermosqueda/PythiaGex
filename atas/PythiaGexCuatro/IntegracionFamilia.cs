// IntegracionFamilia.cs — PythiaGex 4.1 (08-10-2026), integracion del agente principal. Une los modulos de los constructores con el clon:
//   descargador propio de CBOE (B2) + minuteros NQ/NDX/QQQ (B3a/b/c) + TQQQ (B4) + motor de la familia (B3d) + pantalla (B5),
//   sobre los adaptadores del clon (B1: ICinta = cinta del MNQ, ILibroNq = libro NQ por Rithmic).
// UN host por proceso y contrato (dos graficos con la 4.0 comparten el mismo calculo y la misma bajada). El motor corre en SU hilo
// ("PythiaGex4 familia", BelowNormal): nada de calculo en el hilo de ATAS ni en el render. Cero programas externos.
// 4.1.2 (08-10-2026, B-pos): despues de cada Avanzar, en el MISMO hilo, CambiosFamilia (_modulos/familia/posiciones) anota los cambios por nivel
// (volumen de 5/15/30 min y OI contra la publicacion anterior, valuados con la gamma de ahora) sobre una COPIA de la foto del motor; se publica
// esa copia. PublicarConAviso usa FotoFamilia.Copia() (antes copiaba campo por campo y perdia los campos nuevos).
// 4.1.3 (09-10-2026): el motor recibe la Replica20 (OpcionesMotorFamilia.Extras): las dominantes como la 2.0 (su formula y su conversion) y su
// seleccion sobre los libros QQQ/NDX de la 4.1, sobre la misma descarga de CBOE y la misma cinta. Log: pythiagex4-integracion.log ("replica20").
// 4.1.5 (09-10-2026): ademas la replica de la clasica (ClasicaNdx: R10_NDX_zero "Clasica NDX 0Γ", el zero de NDX con la base de la rueda anterior),
// sobre la misma descarga y la misma cinta; el motor recibe las dos juntas (ExtrasCompuestos: primero la Replica20, despues la clasica). Log "clasica".
// 4.1.5d (09-10-2026): la clasica guarda las muestras de su base de la rueda en PythiaGex4\familia\muestras-clasica-NDX-<dia>.jsonl (CarpetaDatos) y
// recibe de la cinta si la vela de cada muestra esta completa (CintaFamilia.VelaM2Completa): la rueda cerrada no cambia segun como arranque la cinta.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using PythiaGexCuatro.Cboe;
using PythiaGexCuatro.Familia;
using PythiaGexCuatro.Familia.Ndx;
using PythiaGexCuatro.Familia.Qqq;

namespace PythiaGexCuatro
{
    public partial class FamiliaCuatro
    {
        // ------------------------------------------------------------------ ajustes de datos (nombres NUEVOS: no hay valores guardados)
        [Display(Name = "Sesion y ventanas en hora de Nueva York", GroupName = "7. Datos", Order = 1010,
                 Description = "Corregida (recomendado): la sesion va de 18:00 NY a 17:00 NY y no se rompe con el cambio de hora del 01-11. Apagado = paridad exacta con la vista previa (22:00-21:00 UTC fijo).")]
        public bool Familia41Corregida { get; set; } = true;

        [Display(Name = "Bajar las cadenas de CBOE (NDX, QQQ, TQQQ)", GroupName = "7. Datos", Order = 1020,
                 Description = "El propio indicador baja la cadena publica de CBOE (15 min de atraso), comprimida, de a un ticker, sin rafagas: cada 75 s en la rueda y cada 300 s fuera. Apagado = usa solo lo ya guardado en PythiaGex4\\cboe.")]
        public bool Cboe41Bajar { get; set; } = true;

        [Display(Name = "Tope de la bajada (KB/s)", GroupName = "7. Datos", Order = 1030,
                 Description = "Limite de velocidad de la bajada de CBOE para no competir con Rithmic (la pausa entre trozos se calcula con esto).")]
        [Range(16, 20000)]
        public int Cboe41TopeKBps { get; set; } = 1250;

        [Display(Name = "TQQQ de noche (anclaje al cierre, SIN VALIDAR)", GroupName = "7. Datos", Order = 1040,
                 Description = "De noche la cadena de TQQQ esta congelada: con esto se dibuja reanclando la conversion x3 al cierre de hoy. Sin validar: apagado por defecto.")]
        public bool Tqqq41AnclaNoche { get; set; } = false;

        // ------------------------------------------------------------------ host de la familia
        private HostFamilia _famHost;
        private string _famHostClave;
        private DateTime _famHostIntentoUtc = DateTime.MinValue;

        /// <summary>Desde Tick (despues de AsegurarAdaptadoresFamilia): crea o toma el host de la familia cuando ya hay cinta y libro NQ.</summary>
        private void FamiliaAsegurarHost(string raiz)
        {
            try
            {
                if (raiz != "NQ") return;
                var cinta = _famCinta; var libro = _famLibro;
                if (cinta == null || libro == null) return;
                string codigo = CodigoGrafico();
                string clave = (codigo ?? "") + "|" + (Familia41Corregida ? "ny" : "utc");
                PedidoBajada(this, Cboe41Bajar, Cboe41TopeKBps);
                if (_famHost != null && _famHostClave == clave) { _famHost.AjustarBajada(); return; }
                if ((DateTime.UtcNow - _famHostIntentoUtc).TotalSeconds < 30) return;     // si fallo, reintenta cada 30 s (sin inundar el log)
                _famHostIntentoUtc = DateTime.UtcNow;
                if (_famHost != null) { HostFamilia.Soltar(_famHost); _famHost = null; }
                _famHost = HostFamilia.Tomar(clave, () => new HostFamilia(cinta, libro, codigo, Familia41Corregida, Cboe41Bajar, Cboe41TopeKBps, Tqqq41AnclaNoche));
                _famHostClave = clave;
                try { _pantalla.Catalogo = _famHost.Catalogo; } catch { }
                Log("familia: host " + clave + " tomado (" + _famHost.Descripcion + ")");
            }
            catch (Exception e) { Registro.Excepcion(LOG, "FamiliaAsegurarHost", e); }
        }

        private void FamiliaSoltarHost()
        {
            try { QuitarPedidoBajada(this); var h = _famHost; _famHost = null; if (h != null) HostFamilia.Soltar(h); }
            catch (Exception e) { Registro.Excepcion(LOG, "FamiliaSoltarHost", e); }
        }

        // ---- M1: el descargador es UNO por proceso: la bajada va prendida si CUALQUIER instancia la pide, al tope mas alto pedido.
        //      Antes cada instancia empujaba SU ajuste en cada Tick y dos graficos con ajustes distintos lo prendian y apagaban (rafagas).
        private static readonly object _llaveBajada = new object();
        private static readonly Dictionary<FamiliaCuatro, (bool Bajar, int Tope)> _pedidosBajada = new Dictionary<FamiliaCuatro, (bool, int)>();
        private static void PedidoBajada(FamiliaCuatro quien, bool bajar, int tope) { lock (_llaveBajada) _pedidosBajada[quien] = (bajar, tope); }
        private static void QuitarPedidoBajada(FamiliaCuatro quien) { lock (_llaveBajada) _pedidosBajada.Remove(quien); }
        internal static (bool Bajar, int Tope) BajadaEfectiva()
        {
            lock (_llaveBajada)
            {
                if (_pedidosBajada.Count == 0) return (true, 1250);
                bool b = false; int t = 16;
                foreach (var v in _pedidosBajada.Values) { b |= v.Bajar; t = Math.Max(t, v.Tope); }
                return (b, t);
            }
        }

        /// <summary>La foto que dibuja la pantalla (inmutable; null hasta la primera cuenta).</summary>
        internal FotoFamilia FamiliaFoto => _famHost?.Foto;

        // ==================================================================
        /// <summary>Un calculo de la familia por proceso y contrato: descargador + minuteros + TQQQ + motor, con su hilo.</summary>
        internal sealed class HostFamilia
        {
            private static readonly object _llaveHosts = new object();
            private static readonly Dictionary<string, HostFamilia> _hosts = new Dictionary<string, HostFamilia>();

            public static HostFamilia Tomar(string clave, Func<HostFamilia> crear)
            {
                lock (_llaveHosts)
                {
                    if (!_hosts.TryGetValue(clave, out var h))
                    {
                        h = crear(); h._clave = clave;
                        h.Arrancar();                 // si tira, NO queda registrado a medias (el reintento de 30 s lo vuelve a crear)
                        _hosts[clave] = h;
                    }
                    h._duenos++;
                    return h;
                }
            }

            public static void Soltar(HostFamilia h)
            {
                if (h == null) return;
                bool parar = false;
                lock (_llaveHosts)
                {
                    h._duenos--;
                    if (h._duenos <= 0) { _hosts.Remove(h._clave); parar = true; }
                }
                if (parar) h.Parar();
            }

            private string _clave; private int _duenos;
            private readonly ICinta _cinta; private readonly ILibroNq _libro;
            private readonly ManijaCboe _cboe;
            private readonly MinuteroNq _minNq; private readonly LibroMinuteroNdx _minNdx; private readonly MinuteroQqq _minQqq;
            private readonly CalculoTqqq _tqqq;
            private readonly MotorFamilia _motor;
            private readonly CambiosFamilia _cambios;          // 4.1.2: cambios por nivel (solo en el hilo del host)
            private readonly Replica20 _replica20;             // 4.1.3: las dominantes como la 2.0 (las calcula el motor, en su hilo)
            private readonly ClasicaNdx _clasica;              // 4.1.5: la estela 'NDX 0Γ' de la clasica (la calcula el motor, en su hilo)
            private readonly string _carpetaFam;
            private Thread _hilo; private volatile bool _parar;
            private readonly AutoResetEvent _despertar = new AutoResetEvent(false);
            private DateTime _ultimoLogError = DateTime.MinValue;
            public string Descripcion { get; }

            private volatile FotoFamilia _fotoPublicada;
            public FotoFamilia Foto => _fotoPublicada ?? _motor?.Foto;
            private Func<string, (string Json, DateTime EscritoUtc)?> _fuenteCapa;
            public IReadOnlyList<SerieInfo> Catalogo => _motor?.Catalogo;

            public HostFamilia(ICinta cinta, ILibroNq libro, string codigo, bool corregida, bool bajar, int topeKBps, bool tqqqNoche)
            {
                _cinta = cinta; _libro = libro;
                AdaptadoresFamilia.SesionUtcFija = !corregida;
                string carpeta = AdaptadoresFamilia.Carpeta;
                _carpetaFam = Path.Combine(carpeta, "familia");
                try { Directory.CreateDirectory(_carpetaFam); } catch { }
                string contrato = AdaptadoresFamilia.ContratoDeCodigo(codigo);       // MNQZ6 -> "Z6" ("" = continua sin mes)

                _cboe = FuenteCboe.Obtener(new OpcionesCboe { Bajar = bajar, TopeKBps = topeKBps, VentanaUtcPython = !corregida });

                _minNq = new MinuteroNq(libro, cinta, null, new OpcionesNq());
                _minNdx = new LibroMinuteroNdx(_cboe, cinta, new OpcionesNdx
                {
                    ParidadPython = !corregida, ContratoGrafico = codigo ?? "", CarpetaDatos = carpeta,
                    Log = s => Escribir("ndx", s)
                });
                _minQqq = new MinuteroQqq(_cboe, cinta, new OpcionesQqq
                {
                    Corregida = corregida,
                    ContratoDe = corregida ? OpcionesQqq.ContratoFijo(OpcionesQqq.ContratoDeCodigo(codigo)) : (Func<DateTime, string>)OpcionesQqq.ContratoTablaPython
                });
                SembrarMuestrasQqq();
                _minQqq.AlProcesar = r => AnotarMuestraQqq(r);

                var cboe = _cboe;
                _tqqq = new CalculoTqqq(new OpcionesTqqq
                {
                    Corregida = corregida, AnclaNoche = tqqqNoche,
                    Cierres = (lb, f) =>
                    {
                        var c = cboe.Cierre(lb, f);
                        return c == null ? null : new CierresTqqqDia
                        {
                            Anterior = c.Anterior, AnteriorUtc = c.AnteriorUtc, Close = c.Close, CloseUtc = c.CloseUtc,
                            PdcNuevo = c.PdcCambioUtc != default(DateTime) ? c.PdcTrasCierreFin : double.NaN, PdcNuevoUtc = c.PdcCambioUtc
                        };
                    }
                }) { Contrato = contrato };

                // 4.1.3: las dominantes como la 2.0 (R20_QQQ_vol, R20_NDX_vol, DOMS_QQQ_vol, DOMS_NDX_vol), calculadas adentro sobre la MISMA
                // descarga de CBOE y la MISMA cinta; el motor las agrega despues de la cuenta de la vista previa (las 21 series no cambian)
                _replica20 = new Replica20(_cboe, cinta, new OpcionesReplica20 { Contrato = codigo ?? "", Log = s => Escribir("replica20", s) });
                // 4.1.5: la replica de la clasica (zero de NDX por volumen con SU base de la rueda anterior, medida con su regla sobre las cadenas y la cinta
                // de la 4.1: nada de la clasica). Va DESPUES de la Replica20 (que reemplaza las extras al guardar; la clasica agrega la suya)
                // 4.1.5d: las muestras de la base de la rueda se guardan en PythiaGex4\familia (la rueda cerrada no cambia al reiniciar) y la cinta dice si
                // la vela de cada muestra esta completa (una vela de ticks con un hueco, sin la del grafico, queda provisional y no se guarda)
                var cintaF = cinta as CintaFamilia;
                _clasica = new ClasicaNdx(_cboe, cinta, new OpcionesClasicaNdx
                {
                    Contrato = codigo ?? "", Log = s => Escribir("clasica", s), CarpetaDatos = carpeta,
                    VelaCompleta = cintaF == null ? (Func<DateTime, bool>)null : t => cintaF.VelaM2Completa(t)
                });
                var extras = new ExtrasCompuestos(_replica20, _clasica) { Error = (donde, e) => Escribir("extras", "ERROR en " + donde + ": " + e.GetType().Name + ": " + e.Message) };
                _motor = new MotorFamilia(new OpcionesMotorFamilia
                {
                    Carpeta = carpeta, Corregida = corregida, TqqqDeNoche = tqqqNoche,
                    FuentesListas = () => (cboe.Motor?.HistoriaCargada ?? false) && LibroSembrado(libro) && CintaConDatos(cinta),
                    Extras = extras
                });
                _motor.Configurar(cinta, libro, _cboe, new ILibroMinutero[] { _minNq, _minNdx, _minQqq }, _tqqq, codigo ?? "");
                // 4.1.2: mismo modo que los minuteros; el OI del dia espera a que las fuentes tengan su historico en memoria
                _cambios = new CambiosFamilia(libro, _cboe, new OpcionesCambios
                {
                    Corregida = corregida, TqqqNoche = tqqqNoche,
                    HistoriaLista = () => (cboe.Motor?.HistoriaCargada ?? false) && LibroSembrado(libro)
                });
                Descripcion = "contrato " + (contrato == "" ? "(sin mes)" : contrato) + ", sesion " + (corregida ? "NY" : "UTC fija") + ", bajada " + (bajar ? "si" : "no") + " " + topeKBps + " KB/s";
            }

            private static bool LibroSembrado(ILibroNq l) { try { return l is LibroNqFamilia f ? f.Sembrado : l.Version > 0; } catch { return true; } }
            private static bool CintaConDatos(ICinta c) { try { return c.UltimoTickUtc > DateTime.MinValue.AddDays(1) || (c is CintaFamilia cf && cf.Version > 0); } catch { return true; } }

            public void AjustarBajada()
            {
                try
                {
                    var (bajar, tope) = BajadaEfectiva();
                    if (_cboe.Bajar != bajar) _cboe.Bajar = bajar;
                    if (_cboe.TopeKBps != tope) _cboe.TopeKBps = tope;
                }
                catch { }
            }

            private void Arrancar()
            {
                _cboe.Arrancar();
                AdaptadoresFamilia.AlEstela = _motor.Estela;
                // las capas de la 3.0 (TRES_NDX / TRES_QQQ) leen la misma cadena en memoria: archivo "NQ" = libro NDX
                var cboe = _cboe;
                _fuenteCapa = arch =>
                {
                    var r = cboe.UltimoRegistro(arch == "NQ" ? "NDX" : arch);
                    if (r == null || string.IsNullOrEmpty(r.Texto) || r.Foto == null) return null;
                    return (r.Texto, r.Foto.GeneradoUtc);
                };
                FamiliaCuatro.FuenteCapaEnMemoria = _fuenteCapa;
                _hilo = new Thread(Bucle) { IsBackground = true, Name = "PythiaGex4 familia", Priority = ThreadPriority.BelowNormal };
                _hilo.Start();
            }

            private void Bucle()
            {
                while (!_parar)
                {
                    try { _motor.Avanzar(DateTime.UtcNow); PublicarConAviso(Anotar()); }
                    catch (Exception e)
                    {
                        if ((DateTime.UtcNow - _ultimoLogError).TotalSeconds > 60) { _ultimoLogError = DateTime.UtcNow; Escribir("motor", "ERROR " + e); }
                    }
                    int espera = _motor.Pendiente ? 300 : 5000;
                    _despertar.WaitOne(espera);
                }
            }

            private void Parar()
            {
                _parar = true; _despertar.Set();
                try { _motor.Detener = true; } catch { }      // el motor corta su paso en curso (puesta al dia, TQQQ) sin esperar el presupuesto
                bool termino = true;
                try { termino = _hilo == null || _hilo.Join(2000); } catch { }
                try { if (AdaptadoresFamilia.AlEstela == (Action<string, string>)_motor.Estela) AdaptadoresFamilia.AlEstela = null; } catch { }
                try { if (ReferenceEquals(FamiliaCuatro.FuenteCapaEnMemoria, _fuenteCapa)) FamiliaCuatro.FuenteCapaEnMemoria = null; } catch { }
                // M2: la CBOE no se suelta con el motor a mitad de un minuto (quedaria guardado un minuto sin NDX/QQQ que el proximo arranque
                // da por calculado): si el hilo no termino en 2 s, se suelta en segundo plano cuando termine (sin trabar OnDispose).
                if (termino) { try { _cboe.Parar(); } catch { } }
                else
                {
                    var h = _hilo; var c = _cboe;
                    ThreadPool.QueueUserWorkItem(_ => { try { h.Join(60000); } catch { } try { c.Parar(); } catch { } });
                }
            }

            // ---- M3: avisos de fuentes caidas (la pestaña los muestra en naranja aunque este cerrada)
            private static readonly TimeZoneInfo _zonaNy = ZonaNy();
            private static TimeZoneInfo ZonaNy() { try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); } catch { return null; } }
            private static DateTime Ny(DateTime utc) => _zonaNy == null ? utc.AddHours(-4) : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zonaNy);
            /// <summary>Rueda de opciones de NY (lun-vie 09:30-16:00): CBOE deberia estar bajando.</summary>
            private static bool EnRueda(DateTime utc) { var n = Ny(utc); var h = n.TimeOfDay.TotalHours; return n.DayOfWeek >= DayOfWeek.Monday && n.DayOfWeek <= DayOfWeek.Friday && h >= 9.5 && h < 16.0; }
            /// <summary>CME abierto (dom 18:00 - vie 17:00 NY, sin la pausa diaria 17:00-18:00): Rithmic deberia mandar el libro de NQ.</summary>
            private static bool CmeAbierto(DateTime utc)
            {
                var n = Ny(utc); var h = n.TimeOfDay.TotalHours;
                if (n.DayOfWeek == DayOfWeek.Saturday) return false;
                if (n.DayOfWeek == DayOfWeek.Sunday) return h >= 18.0;
                if (n.DayOfWeek == DayOfWeek.Friday) return h < 17.0;
                return h < 17.0 || h >= 18.0;
            }

            private string AvisoFuentes(DateTime ahora)
            {
                var partes = new List<string>();
                try
                {
                    string est = _cboe.Estado ?? "";
                    DateTime gen = DateTime.MinValue;
                    foreach (var lb in new[] { "NDX", "QQQ", "TQQQ" }) { var u = _cboe.Ultima(lb); if (u != null && u.GeneradoUtc > gen) gen = u.GeneradoUtc; }
                    if (!_cboe.Bajar) { if (EnRueda(ahora)) partes.Add("descarga de CBOE apagada: NDX/QQQ/TQQQ no se actualizan"); }
                    else if (est.Contains("falla")) partes.Add("CBOE con fallas (" + (est.Length > 70 ? est.Substring(0, 70) + "…" : est) + ")");
                    else if (EnRueda(ahora) && gen > DateTime.MinValue && (ahora - gen).TotalMinutes > 10)
                        partes.Add("CBOE sin bajar hace " + Math.Round((ahora - gen).TotalMinutes) + " min: NDX/QQQ/TQQQ congelados");
                }
                catch { }
                try
                {
                    var u = _libro.Ultima();
                    if (CmeAbierto(ahora) && (u == null || (ahora - u.TsUtc).TotalMinutes > 5))
                        partes.Add(u == null ? "sin libro de opciones de NQ (Rithmic)" : "libro de NQ (Rithmic) sin foto nueva hace " + Math.Round((ahora - u.TsUtc).TotalMinutes) + " min");
                }
                catch { }
                return string.Join(" · ", partes);
            }

            /// <summary>4.1.2: la foto del motor con los cambios por nivel (copia; la del motor no se toca). En el hilo del host, despues de
            /// Avanzar: el almacen de minutos y las velas de TQQQ solo los toca este hilo. Si algo falla, la foto del motor tal cual.</summary>
            private FotoFamilia Anotar()
            {
                var f = _motor.Foto;
                if (f == null) return null;
                try
                {
                    TqqqVela tq = null; long tqT = long.MinValue;            // la misma vela que usa MotorFamilia.Publicar para los actuales de TQQQ
                    var velas = _motor.VelasTqqq;
                    if (velas != null) foreach (var kv in velas) if (kv.Value != null && kv.Key > tqT) { tqT = kv.Key; tq = kv.Value; }
                    return _cambios.Anotar(f, _motor.Almacen?.Ultimo, tq) ?? f;
                }
                catch (Exception e)
                {
                    if ((DateTime.UtcNow - _ultimoLogError).TotalSeconds > 60) { _ultimoLogError = DateTime.UtcNow; Escribir("cambios", "ERROR " + e); }
                    return f;
                }
            }

            /// <summary>Diagnostico (pestaña/arnes): el calculo de cambios por nivel de este host.</summary>
            public CambiosFamilia Cambios => _cambios;

            private void PublicarConAviso(FotoFamilia f)
            {
                if (f == null) return;
                string extra = AvisoFuentes(DateTime.UtcNow);
                if (extra == "") { _fotoPublicada = f; return; }
                var c = f.Copia();                                           // 4.1.2: Copia() conserva Actuales anotados y CambiosEstado
                c.Aviso = string.IsNullOrEmpty(f.Aviso) ? extra : f.Aviso + " · " + extra;
                _fotoPublicada = c;
            }

            // ---- muestras de C8 (QQQ): persistidas para que un reinicio no pierda la razon (B3c: "el motor tiene que persistir las muestras")
            private string RutaMuestrasQqq(string sesion) => Path.Combine(_carpetaFam, "muestras-QQQ-" + sesion + ".jsonl");

            // Solo se anotan las fotos MAS NUEVAS que la ultima ya guardada (al arrancar se reprocesa todo el historico y antes se volvia a
            // anotar entero: ~390 KB por arranque) y van al archivo de la sesion de la FOTO, no de la hora del reloj.
            private DateTime _ultimaMuestraQqq = DateTime.MinValue;
            private void AnotarMuestraQqq(FotoQqq r)
            {
                try
                {
                    string linea = ConversionQqq.LineaMuestra(r);
                    if (string.IsNullOrEmpty(linea)) return;
                    if (!ConversionQqq.LeerLineaMuestra(linea, out var ts, out _, out _)) return;
                    if (ts <= _ultimaMuestraQqq) return;
                    _ultimaMuestraQqq = ts;
                    long ms = (ts.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks) / TimeSpan.TicksPerMillisecond;
                    File.AppendAllText(RutaMuestrasQqq(AdaptadoresFamilia.Sesion(ms).Dia), linea + "\n", new UTF8Encoding(false));
                }
                catch (Exception e) { Escribir("qqq", "muestra: " + e.Message); }
            }

            private void SembrarMuestrasQqq()
            {
                try
                {
                    var archivos = Directory.GetFiles(_carpetaFam, "muestras-QQQ-*.jsonl").OrderBy(x => x, StringComparer.Ordinal).ToList();
                    // poda: se conservan las ultimas 14 sesiones (como las cadenas y los niveles)
                    foreach (var viejo in archivos.Take(Math.Max(0, archivos.Count - 14)).ToList()) { try { File.Delete(viejo); archivos.Remove(viejo); } catch { } }
                    foreach (var a in archivos.Skip(Math.Max(0, archivos.Count - 2)))
                        foreach (var l in File.ReadAllLines(a))
                            if (ConversionQqq.LeerLineaMuestra(l, out var ts, out var p, out var k))
                            {
                                _minQqq.Conversion.SembrarPrecio(ts, p, k);
                                if (ts > _ultimaMuestraQqq) _ultimaMuestraQqq = ts;
                            }
                }
                catch (Exception e) { Escribir("qqq", "siembra: " + e.Message); }
            }

            private static readonly object _llaveLog = new object();
            private static void Escribir(string donde, string msg)
            {
                try
                {
                    var ruta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "pythiagex4-integracion.log");
                    lock (_llaveLog) File.AppendAllText(ruta, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + donde + ": " + msg + "\n", new UTF8Encoding(false));
                }
                catch { }
            }
        }
    }
}
