// IntegracionFamilia.cs — PythiaGex 5.0 (Gamma ES), clon de la 4.1 (08-10-2026, integracion del agente principal). Une los modulos de los constructores con el clon:
//   descargador propio de CBOE (B2) + minuteros ES/SPX/SPY (B3a/b/c) + motor de la familia (B3d) + pantalla (B5),
//   sobre los adaptadores del clon (B1: ICinta = cinta del MES, ILibroEs = libro ES por Rithmic).
// UN host por proceso y contrato (dos graficos con la 4.0 comparten el mismo calculo y la misma bajada). El motor corre en SU hilo
// ("PythiaGex5 familia", BelowNormal): nada de calculo en el hilo de ATAS ni en el render. Cero programas externos.
// 4.1.2 (08-10-2026, B-pos): despues de cada Avanzar, en el MISMO hilo, CambiosFamilia (_modulos/familia/posiciones) anota los cambios por nivel
// (volumen de 5/15/30 min y OI contra la publicacion anterior, valuados con la gamma de ahora) sobre una COPIA de la foto del motor; se publica
// esa copia. PublicarConAviso usa FotoFamilia.Copia() (antes copiaba campo por campo y perdia los campos nuevos).
// 4.1.3 (09-10-2026): el motor recibe la Replica20 (OpcionesMotorFamilia.Extras): las dominantes como la 2.0 (su formula y su conversion) y su
// seleccion sobre los libros SPY/SPX de la 4.1, sobre la misma descarga de CBOE y la misma cinta. Log: pythiagex5-integracion.log ("replica20").
// 4.1.5 (09-10-2026): ademas la replica de la clasica (ClasicaSpx: R10_SPX_zero "Clasica SPX 0Γ", el zero de SPX con la base de la rueda anterior),
// sobre la misma descarga y la misma cinta; el motor recibe las dos juntas (ExtrasCompuestos: primero la Replica20, despues la clasica). Log "clasica".
// 4.1.5d (09-10-2026): la clasica guarda las muestras de su base de la rueda en PythiaGex5\familia\muestras-clasica-SPX-<dia>.jsonl (CarpetaDatos) y
// recibe de la cinta si la vela de cada muestra esta completa (CintaFamilia.VelaM2Completa): la rueda cerrada no cambia segun como arranque la cinta.
// 5.0 (27-10-2026): sin TQQQ (era el B4): fuera CalculoTqqq, el ajuste Tqqq41AnclaNoche ("TQQQ de noche") y su paso al motor y a los cambios;
// la descarga de CBOE baja SPX y SPY solamente.
// 5.0 (27-10-2026, P5): solo los textos de los ajustes de "7. Datos" (para ES: SPX = _SPX de CBOE con SPXW y SPX mensual, SPY; el tamaño de la
// bajada de SPX; sin "la vista previa" de la 4.1 como unica referencia). Nombres de propiedad, tipos, defaults y la logica del host NO cambian.
// Las muestras de la historia de este encabezado (4.1.x) se refieren a la 4.1 sobre NQ/NDX/QQQ (el clon cambio los nombres de los libros).
// 5.0.1 (27-10-2026): ajuste NUEVO "Replicas con la conversion de la 5.0" (Replicas50Conversion, "7. Datos", default true): R20_SPX_vol, R10_SPX_zero y
// R10_SPX_dom llevan sus strikes a ES con la base SPX del minuto de la 5.0 (la de MUROS_SPX) en vez de su cascada (medido 27-10: 1,17 pts abajo y
// saltos de 1,1-1,7 pts en la 2.0). Uno por proceso y contrato como la bajada (prendido si cualquier instancia lo pide); el host lo pasa al crear las
// replicas y en cada latido (AjustarConversion50: si cambia, el motor rehace la sesion entera de esas series).
// 5.0.3 (27-10-2026): el motor recibe tambien GammalitoFam (las series estilo Gammalito: amarillos de SPX y SPY y el zero de SPX), al final del compuesto.
// 5.0.4 (27-10-2026): el mismo GammalitoFam arma tambien las del libro ES de Rithmic (GL_ES_dom, GL_ES_zero y las barras ES): aca no cambia nada.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using PythiaGexCinco.Cboe;
using PythiaGexCinco.Familia;
using PythiaGexCinco.Familia.Spx;
using PythiaGexCinco.Familia.Spy;

namespace PythiaGexCinco
{
    public partial class GammaCinco
    {
        // ------------------------------------------------------------------ ajustes de datos (nombres NUEVOS: no hay valores guardados)
        [Display(Name = "Sesion y ventanas en hora de Nueva York", GroupName = "7. Datos", Order = 1010,
                 Description = "Corregida (recomendado): la sesion de CME del ES/MES va de 18:00 NY a 17:00 NY (pausa diaria 17:00-18:00) y no se rompe con el cambio de hora de EE.UU. del 01-11. Apagado = la regla vieja de la 4.1 (22:00-21:00 UTC fijo, la de su vista previa en Python; en ES no hay paridad medida).")]
        public bool Familia41Corregida { get; set; } = true;

        [Display(Name = "Bajar las cadenas de CBOE (SPX, SPY)", GroupName = "7. Datos", Order = 1020,
                 Description = "El propio indicador baja la cadena publica de CBOE de SPX (el indice: series SPXW diarias y SPX mensuales) y de SPY (el ETF), con 15 min de atraso, comprimida, de a un ticker, sin rafagas: cada 75 s en la rueda y cada 300 s fuera. Una foto de SPX pesa ~1,7 MB comprimida (MEDIDO 27-10: unas 2 veces lo que pesaba NDX en la 4.1). Rithmic no transmite estas opciones (son OPRA, no CME). Apagado = usa solo lo ya guardado en PythiaGex5\\cboe: SPX y SPY quedan congelados y la pestaña lo avisa.")]
        public bool Cboe41Bajar { get; set; } = true;

        [Display(Name = "Tope de la bajada (KB/s)", GroupName = "7. Datos", Order = 1030,
                 Description = "Limite de velocidad de la bajada de CBOE para no competir con Rithmic (la pausa entre trozos se calcula con esto). Si dos graficos piden topes distintos, manda el mas alto.")]
        [Range(16, 20000)]
        public int Cboe41TopeKBps { get; set; } = 1250;

        // 5.0 (27-10-2026): aca iba Tqqq41AnclaNoche ("TQQQ de noche", Order 1040): se saco con TQQQ.

        // 5.0.1 (27-10-2026): nombre NUEVO (no hay valor guardado en el .ws). Lo lee FamiliaAsegurarHost en cada latido y el host se lo pasa a la
        // Replica20 (R20_SPX_vol) y a la ClasicaSpx (R10_SPX_zero, R10_SPX_dom): OpcionesReplica20/OpcionesClasicaSpx.ConversionDeLa50.
        [Display(Name = "Replicas con la conversion de la 5.0", GroupName = "7. Datos", Order = 1040,
                 Description = "Prendido (recomendado): las replicas '2.0 SPX D1-D2', 'Clas SPX 0Γ' y 'Clas SPX D1-3' conservan la formula y la seleccion de la 2.0 y de la clasica (su gamma, su radio de 100 pts, su cadena de CBOE 960 s atras, la grilla del zero, y la base con que eligen: los mismos strikes que las originales), pero dibujan sus strikes de SPX en ES con la CONVERSION DE LA 5.0: la base ES - SPX sincronizada (C7) del minuto, la misma de los muros de SPX, el eje doble y la 3.0 SPX. MEDIDO el 27-10 (control multifuente): la base propia de esas replicas (forwards de la cadena con el corte de una recta de 11 vencimientos) quedo 1,17 pts DEBAJO de la real (mediana de 51 fotos contra Yahoo ESZ26 - ^GSPC; de -0,77 a -1,84), y la de la 2.0 salto 1,1-1,7 pts al alternar 'medida' y 'de la rueda' (19 saltos desde las 15:15Z sin que el mercado se moviera): el strike SPX 7730 aparecia en 7764,57 en vez de 7766,12. Apagado = la conversion EXACTA de la 2.0 y de la clasica originales (su cascada de base), para compararlas. Al cambiarlo se rehace la sesion entera de esas tres series. El calculo es uno por contrato: con dos graficos de MES con la 5.0, manda el que lo tenga prendido.")]
        public bool Replicas50Conversion { get; set; } = true;

        // ------------------------------------------------------------------ host de la familia
        private HostFamilia _famHost;
        private string _famHostClave;
        private DateTime _famHostIntentoUtc = DateTime.MinValue;

        /// <summary>Desde Tick (despues de AsegurarAdaptadoresFamilia): crea o toma el host de la familia cuando ya hay cinta y libro ES.</summary>
        private void FamiliaAsegurarHost(string raiz)
        {
            try
            {
                if (raiz != "ES") return;
                var cinta = _famCinta; var libro = _famLibro;
                if (cinta == null || libro == null) return;
                string codigo = CodigoGrafico();
                string clave = (codigo ?? "") + "|" + (Familia41Corregida ? "ny" : "utc");
                PedidoBajada(this, Cboe41Bajar, Cboe41TopeKBps);
                PedidoConversion50(this, Replicas50Conversion);                         // 5.0.1: antes de crear el host (el host nace con el valor efectivo)
                if (_famHost != null && _famHostClave == clave) { _famHost.AjustarBajada(); _famHost.AjustarConversion50(); return; }
                if ((DateTime.UtcNow - _famHostIntentoUtc).TotalSeconds < 30) return;     // si fallo, reintenta cada 30 s (sin inundar el log)
                _famHostIntentoUtc = DateTime.UtcNow;
                if (_famHost != null) { HostFamilia.Soltar(_famHost); _famHost = null; }
                _famHost = HostFamilia.Tomar(clave, () => new HostFamilia(cinta, libro, codigo, Familia41Corregida, Cboe41Bajar, Cboe41TopeKBps));
                _famHostClave = clave;
                try { _pantalla.Catalogo = _famHost.Catalogo; } catch { }
                Log("familia: host " + clave + " tomado (" + _famHost.Descripcion + ")");
            }
            catch (Exception e) { Registro.Excepcion(LOG, "FamiliaAsegurarHost", e); }
        }

        private void FamiliaSoltarHost()
        {
            try { QuitarPedidoBajada(this); QuitarPedidoConversion50(this); var h = _famHost; _famHost = null; if (h != null) HostFamilia.Soltar(h); }
            catch (Exception e) { Registro.Excepcion(LOG, "FamiliaSoltarHost", e); }
        }

        // ---- M1: el descargador es UNO por proceso: la bajada va prendida si CUALQUIER instancia la pide, al tope mas alto pedido.
        //      Antes cada instancia empujaba SU ajuste en cada Tick y dos graficos con ajustes distintos lo prendian y apagaban (rafagas).
        private static readonly object _llaveBajada = new object();
        private static readonly Dictionary<GammaCinco, (bool Bajar, int Tope)> _pedidosBajada = new Dictionary<GammaCinco, (bool, int)>();
        private static void PedidoBajada(GammaCinco quien, bool bajar, int tope) { lock (_llaveBajada) _pedidosBajada[quien] = (bajar, tope); }
        private static void QuitarPedidoBajada(GammaCinco quien) { lock (_llaveBajada) _pedidosBajada.Remove(quien); }
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

        // ---- 5.0.1: Replicas50Conversion. El host es uno por proceso y contrato: el valor efectivo es fijo para todas las instancias (prendido si
        //      CUALQUIERA lo pide, como la bajada): si cada instancia empujara el suyo, dos graficos distintos lo prenderian y apagarian en cada latido y
        //      cada cambio rehace la sesion entera de las replicas.
        private static readonly Dictionary<GammaCinco, bool> _pedidosConv50 = new Dictionary<GammaCinco, bool>();
        private static void PedidoConversion50(GammaCinco quien, bool v) { lock (_llaveBajada) _pedidosConv50[quien] = v; }
        private static void QuitarPedidoConversion50(GammaCinco quien) { lock (_llaveBajada) _pedidosConv50.Remove(quien); }
        internal static bool Conversion50Efectiva()
        {
            lock (_llaveBajada)
            {
                if (_pedidosConv50.Count == 0) return true;
                foreach (var v in _pedidosConv50.Values) if (v) return true;
                return false;
            }
        }

        /// <summary>La foto que dibuja la pantalla (inmutable; null hasta la primera cuenta).</summary>
        internal FotoFamilia FamiliaFoto => _famHost?.Foto;

        // ==================================================================
        /// <summary>Un calculo de la familia por proceso y contrato: descargador + minuteros + motor, con su hilo.</summary>
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
            private readonly ICinta _cinta; private readonly ILibroEs _libro;
            private readonly ManijaCboe _cboe;
            private readonly MinuteroEs _minEs; private readonly LibroMinuteroSpx _minSpx; private readonly MinuteroSpy _minSpy;
            private readonly MotorFamilia _motor;
            private readonly CambiosFamilia _cambios;          // 4.1.2: cambios por nivel (solo en el hilo del host)
            private readonly Replica20 _replica20;             // 4.1.3: las dominantes como la 2.0 (las calcula el motor, en su hilo)
            private readonly ClasicaSpx _clasica;              // 4.1.5: la estela 'SPX 0Γ' de la clasica (la calcula el motor, en su hilo)
            private readonly string _carpetaFam;
            private Thread _hilo; private volatile bool _parar;
            private readonly AutoResetEvent _despertar = new AutoResetEvent(false);
            private DateTime _ultimoLogError = DateTime.MinValue;
            public string Descripcion { get; }

            private volatile FotoFamilia _fotoPublicada;
            public FotoFamilia Foto => _fotoPublicada ?? _motor?.Foto;
            private Func<string, (string Json, DateTime EscritoUtc)?> _fuenteCapa;
            public IReadOnlyList<SerieInfo> Catalogo => _motor?.Catalogo;

            public HostFamilia(ICinta cinta, ILibroEs libro, string codigo, bool corregida, bool bajar, int topeKBps)
            {
                _cinta = cinta; _libro = libro;
                AdaptadoresFamilia.SesionUtcFija = !corregida;
                string carpeta = AdaptadoresFamilia.Carpeta;
                _carpetaFam = Path.Combine(carpeta, "familia");
                try { Directory.CreateDirectory(_carpetaFam); } catch { }
                string contrato = AdaptadoresFamilia.ContratoDeCodigo(codigo);       // MESZ6 -> "Z6" ("" = continua sin mes)

                _cboe = FuenteCboe.Obtener(new OpcionesCboe { Bajar = bajar, TopeKBps = topeKBps, VentanaUtcPython = !corregida });

                _minEs = new MinuteroEs(libro, cinta, null, new OpcionesEs());
                _minSpx = new LibroMinuteroSpx(_cboe, cinta, new OpcionesSpx
                {
                    ParidadPython = !corregida, ContratoGrafico = codigo ?? "", CarpetaDatos = carpeta,
                    Log = s => Escribir("spx", s)
                });
                _minSpy = new MinuteroSpy(_cboe, cinta, new OpcionesSpy
                {
                    Corregida = corregida,
                    ContratoDe = corregida ? OpcionesSpy.ContratoFijo(OpcionesSpy.ContratoDeCodigo(codigo)) : (Func<DateTime, string>)OpcionesSpy.ContratoTablaPython
                });
                SembrarMuestrasSpy();
                _minSpy.AlProcesar = r => AnotarMuestraSpy(r);

                var cboe = _cboe;
                // 5.0 (27-10-2026): aca se creaba el CalculoTqqq (con los cierres de cboe.Cierre); se saco con TQQQ.

                // 4.1.3: las dominantes como la 2.0 (R20_SPY_vol, R20_SPX_vol, DOMS_SPY_vol, DOMS_SPX_vol), calculadas adentro sobre la MISMA
                // descarga de CBOE y la MISMA cinta; el motor las agrega despues de la cuenta de la vista previa (las 21 series no cambian)
                // 5.0.1: ConversionDeLa50 = Replicas50Conversion efectivo (R20_SPX_vol con la base SPX del minuto de la 5.0; apagado = la cascada de la 2.0)
                bool conv50 = Conversion50Efectiva();
                _replica20 = new Replica20(_cboe, cinta, new OpcionesReplica20 { Contrato = codigo ?? "", Log = s => Escribir("replica20", s), ConversionDeLa50 = conv50 });
                // 4.1.5: la replica de la clasica (zero de SPX por volumen con SU base de la rueda anterior, medida con su regla sobre las cadenas y la cinta
                // de la 4.1: nada de la clasica). Va DESPUES de la Replica20 (que reemplaza las extras al guardar; la clasica agrega la suya)
                // 4.1.5d: las muestras de la base de la rueda se guardan en PythiaGex5\familia (la rueda cerrada no cambia al reiniciar) y la cinta dice si
                // la vela de cada muestra esta completa (una vela de ticks con un hueco, sin la del grafico, queda provisional y no se guarda)
                var cintaF = cinta as CintaFamilia;
                _clasica = new ClasicaSpx(_cboe, cinta, new OpcionesClasicaSpx
                {
                    Contrato = codigo ?? "", Log = s => Escribir("clasica", s), CarpetaDatos = carpeta,
                    VelaCompleta = cintaF == null ? (Func<DateTime, bool>)null : t => cintaF.VelaM2Completa(t),
                    ConversionDeLa50 = conv50                                               // 5.0.1: el zero y las D1-D3 con la base SPX del minuto de la 5.0
                });
                Escribir("replicas", "conversion de R20_SPX_vol / R10_SPX_zero / R10_SPX_dom: " + (conv50 ? "la de la 5.0 (base SPX del minuto del libro SPX)" : "la cascada original de la 2.0 y de la clasica"));
                // 5.0.3: + las series estilo Gammalito (GL_SPX_dom, GL_SPY_dom, GL_SPX_zero) sobre los libros SPX/SPY del minuto de la 5.0. Va AL FINAL:
                // agrega sus series sin tocar las de la Replica20 (que reemplaza las extras al guardar) ni las de la clasica
                var extras = new ExtrasCompuestos(_replica20, _clasica, new GammalitoFam()) { Error = (donde, e) => Escribir("extras", "ERROR en " + donde + ": " + e.GetType().Name + ": " + e.Message) };
                _motor = new MotorFamilia(new OpcionesMotorFamilia
                {
                    Carpeta = carpeta, Corregida = corregida,
                    FuentesListas = () => (cboe.Motor?.HistoriaCargada ?? false) && LibroSembrado(libro) && CintaConDatos(cinta),
                    Extras = extras
                });
                _motor.Configurar(cinta, libro, _cboe, new ILibroMinutero[] { _minEs, _minSpx, _minSpy }, codigo ?? "");
                // 4.1.2: mismo modo que los minuteros; el OI del dia espera a que las fuentes tengan su historico en memoria
                _cambios = new CambiosFamilia(libro, _cboe, new OpcionesCambios
                {
                    Corregida = corregida,
                    HistoriaLista = () => (cboe.Motor?.HistoriaCargada ?? false) && LibroSembrado(libro)
                });
                Descripcion = "contrato " + (contrato == "" ? "(sin mes)" : contrato) + ", sesion " + (corregida ? "NY" : "UTC fija") + ", bajada " + (bajar ? "si" : "no") + " " + topeKBps + " KB/s";
            }

            private static bool LibroSembrado(ILibroEs l) { try { return l is LibroEsFamilia f ? f.Sembrado : l.Version > 0; } catch { return true; } }
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

            /// <summary>5.0.1: el ajuste Replicas50Conversion efectivo a las dos replicas, en cada latido desde el hilo de ATAS. Solo anota el pedido (sin
            /// llave: no frena al hilo de ATAS aunque el motor este rehaciendo el estado); lo aplica el hilo del motor: si cambia, cada replica sube su
            /// generacion desde el principio y el motor rehace todos los minutos de la sesion de esas series con la regla nueva; si no cambia, nada.</summary>
            public void AjustarConversion50()
            {
                try
                {
                    bool v = Conversion50Efectiva();
                    _replica20?.PonerConversionDeLa50(v);
                    _clasica?.PonerConversionDeLa50(v);
                }
                catch (Exception e) { Escribir("replicas", "ERROR al cambiar la conversion: " + e.GetType().Name + ": " + e.Message); }
            }

            private void Arrancar()
            {
                _cboe.Arrancar();
                AdaptadoresFamilia.AlEstela = _motor.Estela;
                // las capas de la 3.0 (TRES_SPX / TRES_SPY) leen la misma cadena en memoria: archivo "ES" = libro SPX
                var cboe = _cboe;
                _fuenteCapa = arch =>
                {
                    var r = cboe.UltimoRegistro(arch == "ES" ? "SPX" : arch);
                    if (r == null || string.IsNullOrEmpty(r.Texto) || r.Foto == null) return null;
                    return (r.Texto, r.Foto.GeneradoUtc);
                };
                GammaCinco.FuenteCapaEnMemoria = _fuenteCapa;
                _hilo = new Thread(Bucle) { IsBackground = true, Name = "PythiaGex5 familia", Priority = ThreadPriority.BelowNormal };
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
                try { _motor.Detener = true; } catch { }      // el motor corta su paso en curso (puesta al dia) sin esperar el presupuesto
                bool termino = true;
                try { termino = _hilo == null || _hilo.Join(2000); } catch { }
                try { if (AdaptadoresFamilia.AlEstela == (Action<string, string>)_motor.Estela) AdaptadoresFamilia.AlEstela = null; } catch { }
                try { if (ReferenceEquals(GammaCinco.FuenteCapaEnMemoria, _fuenteCapa)) GammaCinco.FuenteCapaEnMemoria = null; } catch { }
                // M2: la CBOE no se suelta con el motor a mitad de un minuto (quedaria guardado un minuto sin SPX/SPY que el proximo arranque
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
            /// <summary>CME abierto (dom 18:00 - vie 17:00 NY, sin la pausa diaria 17:00-18:00): Rithmic deberia mandar el libro de ES.</summary>
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
                    foreach (var lb in new[] { "SPX", "SPY" }) { var u = _cboe.Ultima(lb); if (u != null && u.GeneradoUtc > gen) gen = u.GeneradoUtc; }   // 5.0: sin TQQQ
                    if (!_cboe.Bajar) { if (EnRueda(ahora)) partes.Add("descarga de CBOE apagada: SPX/SPY no se actualizan"); }
                    else if (est.Contains("falla")) partes.Add("CBOE con fallas (" + (est.Length > 70 ? est.Substring(0, 70) + "…" : est) + ")");
                    else if (EnRueda(ahora) && gen > DateTime.MinValue && (ahora - gen).TotalMinutes > 10)
                        partes.Add("CBOE sin bajar hace " + Math.Round((ahora - gen).TotalMinutes) + " min: SPX/SPY congelados");
                }
                catch { }
                try
                {
                    var u = _libro.Ultima();
                    if (CmeAbierto(ahora) && (u == null || (ahora - u.TsUtc).TotalMinutes > 5))
                        partes.Add(u == null ? "sin libro de opciones de ES (Rithmic)" : "libro de ES (Rithmic) sin foto nueva hace " + Math.Round((ahora - u.TsUtc).TotalMinutes) + " min");
                }
                catch { }
                return string.Join(" · ", partes);
            }

            /// <summary>4.1.2: la foto del motor con los cambios por nivel (copia; la del motor no se toca). En el hilo del host, despues de
            /// Avanzar: el almacen de minutos solo lo toca este hilo. Si algo falla, la foto del motor tal cual. 5.0: sin la vela de TQQQ.</summary>
            private FotoFamilia Anotar()
            {
                var f = _motor.Foto;
                if (f == null) return null;
                try
                {
                    return _cambios.Anotar(f, _motor.Almacen?.Ultimo) ?? f;
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

            // ---- muestras de C8 (SPY): persistidas para que un reinicio no pierda la razon (B3c: "el motor tiene que persistir las muestras")
            private string RutaMuestrasSpy(string sesion) => Path.Combine(_carpetaFam, "muestras-SPY-" + sesion + ".jsonl");

            // Solo se anotan las fotos MAS NUEVAS que la ultima ya guardada (al arrancar se reprocesa todo el historico y antes se volvia a
            // anotar entero: ~390 KB por arranque) y van al archivo de la sesion de la FOTO, no de la hora del reloj.
            private DateTime _ultimaMuestraSpy = DateTime.MinValue;
            private void AnotarMuestraSpy(FotoSpy r)
            {
                try
                {
                    string linea = ConversionSpy.LineaMuestra(r);
                    if (string.IsNullOrEmpty(linea)) return;
                    if (!ConversionSpy.LeerLineaMuestra(linea, out var ts, out _, out _)) return;
                    if (ts <= _ultimaMuestraSpy) return;
                    _ultimaMuestraSpy = ts;
                    long ms = (ts.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks) / TimeSpan.TicksPerMillisecond;
                    File.AppendAllText(RutaMuestrasSpy(AdaptadoresFamilia.Sesion(ms).Dia), linea + "\n", new UTF8Encoding(false));
                }
                catch (Exception e) { Escribir("spy", "muestra: " + e.Message); }
            }

            private void SembrarMuestrasSpy()
            {
                try
                {
                    var archivos = Directory.GetFiles(_carpetaFam, "muestras-SPY-*.jsonl").OrderBy(x => x, StringComparer.Ordinal).ToList();
                    // poda: se conservan las ultimas 14 sesiones (como las cadenas y los niveles)
                    foreach (var viejo in archivos.Take(Math.Max(0, archivos.Count - 14)).ToList()) { try { File.Delete(viejo); archivos.Remove(viejo); } catch { } }
                    foreach (var a in archivos.Skip(Math.Max(0, archivos.Count - 2)))
                        foreach (var l in File.ReadAllLines(a))
                            if (ConversionSpy.LeerLineaMuestra(l, out var ts, out var p, out var k))
                            {
                                _minSpy.Conversion.SembrarPrecio(ts, p, k);
                                if (ts > _ultimaMuestraSpy) _ultimaMuestraSpy = ts;
                            }
                }
                catch (Exception e) { Escribir("spy", "siembra: " + e.Message); }
            }

            private static readonly object _llaveLog = new object();
            private static void Escribir(string donde, string msg)
            {
                try
                {
                    var ruta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "pythiagex5-integracion.log");
                    lock (_llaveLog) File.AppendAllText(ruta, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + donde + ": " + msg + "\n", new UTF8Encoding(false));
                }
                catch { }
            }
        }
    }
}
