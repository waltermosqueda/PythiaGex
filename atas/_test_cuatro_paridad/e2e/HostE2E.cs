// HostE2E.cs — prueba de punta a punta de la 4.1 (08-10-2026). Replica LINEA POR LINEA de FamiliaCuatro.HostFamilia (IntegracionFamilia.cs).
// Por que una replica y no la clase misma: HostFamilia esta anidada en el indicador (partial de un Indicator de ATAS) y no compila sin ATAS; y
// sus modulos usan las rutas POR DEFECTO de produccion (%APPDATA%\ATAS\PythiaGex4\cboe, PythiaGex4\familia, el Mutex Local\PythiaGex4.BajadorCboe
// y los logs de %APPDATA%\ATAS), que esta prueba NO puede tocar. Todo lo demas es igual; cada diferencia va marcada [E2E-n]:
//   [E2E-1] OpcionesCboe: Carpeta = <tmp>\cboe, Log = <tmp>\logs, NombreMutex propio, AhoraUtc = reloj inyectado, Http = guardia sin red.
//   [E2E-2] OpcionesNq.Reloj = reloj inyectado (HostFamilia usa el default DateTime.UtcNow).
//   [E2E-3] OpcionesTqqq: Carpeta = <tmp>\familia (HostFamilia NO la pasa: usa el default PythiaGex4\familia), RutaLog = <tmp>\logs.
//           SForzada solo en la variante de diagnostico (nunca en el escenario principal).
//   [E2E-4] OpcionesMotorFamilia.RutaLog = <tmp>\logs (HostFamilia usa el default %APPDATA%\ATAS\pythiagex4-familia.log).
//   [E2E-5] Escribir (log de la integracion) a <tmp>\logs.
//   [E2E-6] Bucle: Avanzar(reloj()) en vez de Avanzar(DateTime.UtcNow). FuenteCapaEnMemoria (miembro del clon de la 3.0) no existe aca.
//   [E2E-8] 4.1.3: HostFamilia le pasa al motor la Replica20 (OpcionesMotorFamilia.Extras: las dominantes como la 2.0). Aca solo con extras = true
//           (4.1.4, escenario ny-extras: la configuracion de produccion; sus comparaciones miran las 21 series sin las claves R20_/DOMS_ ni las
//           fuentes "2.0 ..." y la foto tiene que dar IDENTICA a la de 'ny'). Los demas escenarios comparan contra la vista previa, que no las tiene.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using PythiaGexCuatro.Cboe;
using PythiaGexCuatro.Familia;
using PythiaGexCuatro.Familia.Ndx;
using PythiaGexCuatro.Familia.Qqq;

namespace E2E
{
    /// <summary>[E2E-1] Guardia: si alguien intenta bajar, se cuenta y se responde "sin red" (nunca sale un pedido).</summary>
    public sealed class HttpProhibido : IHttpCboe
    {
        public int Llamadas;
        public RespuestaHttp Bajar(string simbolo, int trozoBytes, int pausaMs, CancellationToken ct)
        {
            Interlocked.Increment(ref Llamadas);
            return new RespuestaHttp { Ok = false, Http = 0, Error = "e2e: sin red (guardia)" };
        }
    }

    public sealed class HostE2E
    {
        // ---- lo que en HostFamilia son campos privados (aca publicos para medir y comparar)
        public readonly ICinta Cinta; public readonly ILibroNq Libro;
        public readonly ManijaCboe Cboe;
        public readonly MinuteroNq MinNq; public readonly LibroMinuteroNdx MinNdx; public readonly MinuteroQqq MinQqq;
        public readonly CalculoTqqq Tqqq;
        public readonly MotorFamilia Motor;
        public readonly Replica20 Replica;                                                                                                      // [E2E-8] null sin extras
        public readonly HttpProhibido Http = new HttpProhibido();
        private readonly string _carpetaFam, _logs;
        private readonly Func<DateTime> _reloj;
        private Thread _hilo; private volatile bool _parar;
        private readonly AutoResetEvent _despertar = new AutoResetEvent(false);
        private DateTime _ultimoLogError = DateTime.MinValue;
        public string Descripcion { get; }
        public int LlamadasAvanzar, ErroresAvanzar;
        public double MsAvanzarTotal, MsAvanzarMax;
        public DateTime PrimerListoUtc = DateTime.MinValue;   // reloj real cuando el motor empezo a calcular (fuentes listas)

        public FotoFamilia Foto => Motor?.Foto;
        public IReadOnlyList<SerieInfo> Catalogo => Motor?.Catalogo;

        public HostE2E(ICinta cinta, ILibroNq libro, string codigo, bool corregida, bool bajar, int topeKBps, bool tqqqNoche,
                       Func<DateTime> reloj, string logs, string nombreMutex, Dictionary<string, double> sForzada = null, Func<DateTime> relojCboe = null, bool extras = false)
        {
            _reloj = reloj; _logs = logs;
            Directory.CreateDirectory(_logs);
            Cinta = cinta; Libro = libro;
            AdaptadoresFamilia.SesionUtcFija = !corregida;
            string carpeta = AdaptadoresFamilia.Carpeta;
            _carpetaFam = Path.Combine(carpeta, "familia");
            try { Directory.CreateDirectory(_carpetaFam); } catch { }
            string contrato = AdaptadoresFamilia.ContratoDeCodigo(codigo);       // MNQZ6 -> "Z6"

            Cboe = FuenteCboe.Obtener(new OpcionesCboe
            {
                Bajar = bajar, TopeKBps = topeKBps, VentanaUtcPython = !corregida,
                Carpeta = Path.Combine(carpeta, "cboe"), Log = Path.Combine(_logs, "pythiagex4-cboe.log"), NombreMutex = nombreMutex,   // [E2E-1]
                AhoraUtc = relojCboe ?? reloj, Http = Http,                                                                             // [E2E-1]
            });

            MinNq = new MinuteroNq(libro, cinta, null, new OpcionesNq { Reloj = reloj });                                              // [E2E-2]
            MinNdx = new LibroMinuteroNdx(Cboe, cinta, new OpcionesNdx
            {
                ParidadPython = !corregida, ContratoGrafico = codigo ?? "", CarpetaDatos = carpeta,
                Log = s => Escribir("ndx", s)
            });
            MinQqq = new MinuteroQqq(Cboe, cinta, new OpcionesQqq
            {
                Corregida = corregida,
                ContratoDe = corregida ? OpcionesQqq.ContratoFijo(OpcionesQqq.ContratoDeCodigo(codigo)) : (Func<DateTime, string>)OpcionesQqq.ContratoTablaPython
            });
            SembrarMuestrasQqq();
            MinQqq.AlProcesar = r => AnotarMuestraQqq(r);

            var cboe = Cboe;
            Tqqq = new CalculoTqqq(new OpcionesTqqq
            {
                Corregida = corregida, AnclaNoche = tqqqNoche,
                Carpeta = _carpetaFam, RutaLog = Path.Combine(_logs, "pythiagex4-tqqq.log"), SForzada = sForzada,                     // [E2E-3]
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

            // [E2E-8] como HostFamilia: la Replica20 sobre la MISMA descarga de CBOE y la MISMA cinta (solo en el escenario ny-extras)
            if (extras) Replica = new Replica20(Cboe, cinta, new OpcionesReplica20 { Contrato = codigo ?? "", Log = s => Escribir("replica20", s) });
            Motor = new MotorFamilia(new OpcionesMotorFamilia
            {
                Carpeta = carpeta, Corregida = corregida, TqqqDeNoche = tqqqNoche,
                FuentesListas = () => (cboe.Motor?.HistoriaCargada ?? false) && LibroSembrado(libro) && CintaConDatos(cinta),
                RutaLog = Path.Combine(_logs, "pythiagex4-familia.log"),                                                               // [E2E-4]
                Extras = Replica                                                                                                         // [E2E-8]
            });
            Motor.Configurar(cinta, libro, Cboe, new ILibroMinutero[] { MinNq, MinNdx, MinQqq }, Tqqq, codigo ?? "");
            Descripcion = "contrato " + (contrato == "" ? "(sin mes)" : contrato) + ", sesion " + (corregida ? "NY" : "UTC fija") + ", bajada " + (bajar ? "si" : "no") + " " + topeKBps + " KB/s";
        }

        private static bool LibroSembrado(ILibroNq l) { try { return l is LibroNqFamilia f ? f.Sembrado : l.Version > 0; } catch { return true; } }
        private static bool CintaConDatos(ICinta c) { try { return c.UltimoTickUtc > DateTime.MinValue.AddDays(1) || (c is CintaFamilia cf && cf.Version > 0); } catch { return true; } }

        public void Arrancar()
        {
            Cboe.Arrancar();
            AdaptadoresFamilia.AlEstela = Motor.Estela;
            // [E2E-6] FamiliaCuatro.FuenteCapaEnMemoria = ... (capas TRES_NDX/TRES_QQQ del clon): no existe fuera de ATAS
            _hilo = new Thread(Bucle) { IsBackground = true, Name = "PythiaGex4 familia", Priority = ThreadPriority.BelowNormal };
            _hilo.Start();
        }

        /// <summary>Vivo simulado: lo mismo que Arrancar pero sin el hilo (el arnes llama Motor.Avanzar minuto a minuto con su reloj).</summary>
        public void ArrancarSinHilo()
        {
            Cboe.Arrancar();
            AdaptadoresFamilia.AlEstela = Motor.Estela;
        }

        private void Bucle()
        {
            while (!_parar)
            {
                var t0 = DateTime.UtcNow;
                try
                {
                    Motor.Avanzar(_reloj());                                                                                               // [E2E-6]
                    if (PrimerListoUtc == DateTime.MinValue && Motor.MinutosCalculados > 0) PrimerListoUtc = t0;
                }
                catch (Exception e)
                {
                    ErroresAvanzar++;
                    if ((DateTime.UtcNow - _ultimoLogError).TotalSeconds > 60) { _ultimoLogError = DateTime.UtcNow; Escribir("motor", "ERROR " + e); }
                }
                double ms = (DateTime.UtcNow - t0).TotalMilliseconds;
                LlamadasAvanzar++; MsAvanzarTotal += ms; if (ms > MsAvanzarMax) MsAvanzarMax = ms;
                int espera = Motor.Pendiente ? 300 : 5000;
                _despertar.WaitOne(espera);
            }
        }

        /// <summary>Para la prueba: despierta el hilo para que haga otra vuelta ya (en ATAS lo hace el timer de 5 s).</summary>
        public void Despertar() => _despertar.Set();

        /// <summary>Para la prueba: frena SOLO el hilo del motor (para leer sus diccionarios sin que otra vuelta los toque); la fuente CBOE sigue viva.</summary>
        public void PararMotor()
        {
            _parar = true; _despertar.Set();
            try { _hilo?.Join(10000); } catch { }
        }

        public void Parar()
        {
            _parar = true; _despertar.Set();
            try { _hilo?.Join(2000); } catch { }
            try { if (AdaptadoresFamilia.AlEstela == (Action<string, string>)Motor.Estela) AdaptadoresFamilia.AlEstela = null; } catch { }
            try { Cboe.Parar(); } catch { }
        }

        // ---- muestras de C8 (QQQ): igual que HostFamilia (SesionHoy mira el reloj REAL, como en el indicador)
        private string RutaMuestrasQqq(string sesion) => Path.Combine(_carpetaFam, "muestras-QQQ-" + sesion + ".jsonl");
        private static string SesionHoy() => AdaptadoresFamilia.Sesion((DateTime.UtcNow.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks) / TimeSpan.TicksPerMillisecond).Dia;
        public int MuestrasQqqAnotadas, MuestrasQqqSembradas;

        private void AnotarMuestraQqq(FotoQqq r)
        {
            try
            {
                string linea = ConversionQqq.LineaMuestra(r);
                if (string.IsNullOrEmpty(linea)) return;
                File.AppendAllText(RutaMuestrasQqq(SesionHoy()), linea + "\n", new UTF8Encoding(false));
                MuestrasQqqAnotadas++;
            }
            catch (Exception e) { Escribir("qqq", "muestra: " + e.Message); }
        }

        private void SembrarMuestrasQqq()
        {
            try
            {
                var archivos = Directory.GetFiles(_carpetaFam, "muestras-QQQ-*.jsonl").OrderBy(x => x, StringComparer.Ordinal).ToList();
                foreach (var a in archivos.Skip(Math.Max(0, archivos.Count - 2)))
                    foreach (var l in File.ReadAllLines(a))
                        if (ConversionQqq.LeerLineaMuestra(l, out var ts, out var p, out var k)) { MinQqq.Conversion.SembrarPrecio(ts, p, k); MuestrasQqqSembradas++; }
            }
            catch (Exception e) { Escribir("qqq", "siembra: " + e.Message); }
        }

        private readonly object _llaveLog = new object();
        private void Escribir(string donde, string msg)                                                                                     // [E2E-5]
        {
            try
            {
                var ruta = Path.Combine(_logs, "pythiagex4-integracion.log");
                lock (_llaveLog) File.AppendAllText(ruta, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + donde + ": " + msg + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
