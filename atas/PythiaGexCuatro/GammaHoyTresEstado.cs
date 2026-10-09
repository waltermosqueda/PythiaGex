using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace PythiaGexCuatro
{
    /// <summary>
    /// EL ESTADO PARA "PROFUNDIDAD 3.0" (indicador.json). 3.2.0 (07-10-2026): el pulso de 5 s. 3.2.1 (07-10-2026, 01:35, pedido del
    /// operador: "independiente de ATAS y lo mas en tiempo real posible, de segundos"): el pulso POR CAMBIO.
    ///
    /// Dos caminos escriben el mismo archivo, de forma ATOMICA (archivo temporal + File.Move con pisado):
    ///   - el Tick de 5 s (el DUEÑO de la cuenta, la estela, la caja, las capas y la histeresis): despues de CajaLatido escribe el
    ///     estado con la cuenta compartida (_L/_c) de siempre. Origen "tick".
    ///   - un segundo temporizador LIVIANO de 1 s (3.2.1): si el contador CadenaApi.Cambios se movio desde la ultima escritura, o el
    ///     precio del grafico paso a otro tick, rehace SOLO la cuenta del nucleo (DesdeApi + Calcular, igual que el Tick) sobre un
    ///     GammaHoyNucleo PROPIO (Calcular guarda historia para el Max Change: no se mezcla con la del Tick), pasa la lectura por la
    ///     histeresis con decidir = false (3.0.7: no hay paso, las vigentes solo se reubican; ningun paso se adelanta) y escribe. Si no
    ///     cambio nada, no escribe. Tope: una escritura cada Estado3PulsoSeg segundos (1-5, default 1). NO toca _L ni el dibujo. Origen "pulso".
    /// El JSON: precio del grafico, estado de la cadena por la API (suscritos, con puntas, con OI, edad del dato, via), las zonas
    /// (dominantes con su gex, zero por volumen y por OI, histeresis estructurada si la regla es Tres) y la ESCALERA: una fila por strike
    /// del perfil (K, Fut, gexVol, gexOi, OI, volumen de hoy, IV media) mas los datos por lado (OI, IV, volumen de hoy, volumen de AYER,
    /// ultimo precio, bid y ask de call y de put), SOLO del vencimiento mas cercano ('Hoy', el 0DTE). Ademas "cambios" (el contador de
    /// la cadena), "pulso_s" (edad del ultimo cambio del libro), "origen" y los contadores de escrituras.
    /// La pagina local (profundidad/pagina) y su motor Python (profundidad/motor.py) lo leen; el indicador no lee nada.
    ///
    /// Convenciones: todo UTC en ISO; los numeros que no existen van null (J() ya lo hace con NaN). gexVol / gexOi son los del nucleo
    /// (gamma x (volC - volP | oiC - oiP) x 100 x F^2 x 1 %, calls +, puts -), en unidades crudas.
    /// Si la escritura falla se anota en el log UNA vez por minuto y nada sale hacia afuera: el dibujo y la cuenta no dependen de esto.
    /// No dibuja nada, no toca el grafico, no suscribe nada nuevo.
    /// </summary>
    public partial class FamiliaCuatro
    {
        [Display(Name = "Estado para Profundidad 3.0 (indicador.json)", GroupName = "9.5 3.0 · Profundidad", Order = 10,
                 Description = "3.2.0/3.2.1. Escribe indicador.json (atomico) con el libro vivo del 0DTE por strike y por lado, las zonas y la foto de la cadena, para la pagina local Profundidad 3.0: en cada latido de 5 s y, ademas, por CAMBIO del libro o del precio (temporizador de 1 s). No dibuja nada.")]
        public bool Estado4Pulso { get; set; } = false;   // 4.1: nombre nuevo y apagado: nadie de afuera lee la 4.0

        [Display(Name = "Pulso por cambio: segundos minimos entre escrituras (1-5)", GroupName = "9.5 3.0 · Profundidad", Order = 15,
                 Description = "3.2.1. El temporizador de 1 s escribe indicador.json solo si el libro (CadenaApi.Cambios) o el precio del grafico cambiaron desde la ultima escritura, y nunca mas seguido que esto. 1 = lo mas en tiempo real posible; 5 = solo el latido del Tick.")]
        [Range(1, 5)]
        public int Estado3PulsoSeg { get; set; } = 1;

        [Display(Name = "Ruta del estado (indicador.json)", GroupName = "9.5 3.0 · Profundidad", Order = 20,
                 Description = "Archivo que se escribe. 4.1: default %APPDATA%\\ATAS\\PythiaGex4\\estado\\indicador.json (propio: la 3.0 escribe el de profundidad\\estado). La carpeta se crea si no existe.")]
        public string Estado4Ruta { get; set; } = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex4", "estado", "indicador.json");

        private readonly object _estadoLlave = new object();          // una escritura por vez (Tick y pulso son dos temporizadores)
        private DateTime _estadoUltimoError = DateTime.MinValue, _estadoUltimaEscritura = DateTime.MinValue;
        private int _estadoEscritos, _estadoEscritosPulso;
        private long _estadoCambiosEscritos = -1;                     // CadenaApi.Cambios en la ultima escritura
        private double _estadoPrecioEscrito;                          // precio del grafico en la ultima escritura

        // el pulso por cambio (3.2.1): temporizador liviano de 1 s
        private readonly TimeSpan _pulsoPeriodo = TimeSpan.FromSeconds(1);
        private Action _pulso;
        private int _pulsoCorriendo;                                  // guarda de reentrada (Interlocked)
        private long _pulsoCambiosVistos = -1;
        private DateTime _pulsoUltimoCambioUtc = DateTime.MinValue;   // cuando se vio moverse el contador por ultima vez -> "pulso_s"
        private DateTime _pulsoUltimoError = DateTime.MinValue;
        private int _pulsoSaltadosPorTope;
        private readonly GammaHoyNucleo _nucleoPulso = new GammaHoyNucleo();

        /// <summary>Desde OnInitialize: suscribe el temporizador de 1 s (idempotente).</summary>
        private void EstadoPulsoArrancar()
        {
            if (_pulso != null) return;
            _pulso = EstadoPulsoCambio;
            SubscribeToTimer(_pulsoPeriodo, _pulso);
        }

        /// <summary>Desde OnDispose.</summary>
        private void EstadoPulsoParar()
        {
            try { if (_pulso != null) UnsubscribeFromTimer(_pulsoPeriodo, _pulso); } catch { }
            _pulso = null;
        }

        private void AnotarCambios(long cambios, DateTime ahora)
        {
            if (cambios != _pulsoCambiosVistos) { _pulsoCambiosVistos = cambios; _pulsoUltimoCambioUtc = ahora; }
        }

        private double TickGrafico()
        {
            double tick = 0.25; try { tick = (double)(InstrumentInfo?.TickSize ?? 0.25m); if (tick <= 0) tick = 0.25; } catch { }
            return tick;
        }

        /// <summary>Desde Tick, cada 5 s, despues de CajaLatido: el latido con la cuenta COMPARTIDA (la del dueño). Nunca tira.</summary>
        private void EstadoPulso(string raiz, DateTime ahora)
        {
            if (!Estado4Pulso || string.IsNullOrEmpty(raiz)) return;
            try
            {
                AnotarCambios(_cadena.Cambios, ahora);
                GammaHoyNucleo.Lectura L; Feed.Cadena c; double fut; int utiles; DateTime cuenta;
                lock (_candado) { L = _L; c = _c; fut = _futuro; utiles = _strikesUtiles; cuenta = _ultimaCuentaUtc; }
                EstadoEscribir(raiz, ahora, L, c, fut, utiles, cuenta, "tick", null);
            }
            catch (Exception e) { EstadoError(ahora, "EstadoPulso", e); }
        }

        /// <summary>
        /// El temporizador de 1 s (3.2.1). Escribe SOLO si el libro o el precio cambiaron desde la ultima escritura, con el tope de
        /// Estado3PulsoSeg. Rehace la cuenta del nucleo sobre _nucleoPulso (los ajustes se copian del nucleo del Tick) y pasa la lectura
        /// por la histeresis SIN decidir (ningun paso se adelanta). No toca _L, _c ni el dibujo: el Tick sigue siendo el dueño.
        /// </summary>
        private void EstadoPulsoCambio()
        {
            if (Interlocked.Exchange(ref _pulsoCorriendo, 1) == 1) return;
            var ahora = DateTime.UtcNow;
            try
            {
                if (!Estado4Pulso) return;
                var raiz = Raiz(); if (raiz == "") return;
                if (!_cadena.Activa) return;
                long cambios = _cadena.Cambios;
                AnotarCambios(cambios, ahora);
                double precio = PrecioGrafico();
                bool cambioLibro = cambios != _estadoCambiosEscritos;
                bool cambioPrecio = precio > 0 && _estadoPrecioEscrito > 0 && Math.Abs(precio - _estadoPrecioEscrito) >= TickGrafico() - 1e-9;
                if (!cambioLibro && !cambioPrecio) return;
                if (precio <= 0) return;
                int seg = Math.Max(1, Math.Min(5, Estado3PulsoSeg));
                if (_estadoUltimaEscritura != DateTime.MinValue && (ahora - _estadoUltimaEscritura).TotalSeconds < seg - 0.05) { _pulsoSaltadosPorTope++; return; }

                // SOLO la cuenta del nucleo, igual que el Tick: una sola llamada a Filas() (IV incluida) que sirve para la cuenta y para la escalera
                var foto = _cadena.Foto();
                var fs = _cadena.Filas();
                int utiles;
                var c = DesdeApi(raiz, foto, fs, out utiles);
                if (c == null) return;
                CopiarAjustesNucleo(_nucleo, _nucleoPulso);
                var L = _nucleoPulso.Calcular(c, precio, ahora);
                if (L == null || L.SinBase) return;
                if (ReglaEfectiva() == ReglaDominantes3.Tres)
                {
                    // la MISMA histeresis del vivo, con decidir = false: no hay paso (3.0.7), las vigentes solo se reubican en el perfil de ahora.
                    // Se llama directo a Hist.Aplicar (no a AplicarRegla) para no alternar el aviso 'libro a medio armar / completo' del Tick.
                    var h = Hist;
                    h.HistX = (double)Math.Max(0m, Histeresis3Pct); h.HistN = Math.Max(1, Histeresis3Min); h.RadioCentroidePts = (double)Math.Max(0m, Histeresis3CentroidePts);
                    h.Aplicar(L, RadioDominantesEfectivo(L.Futuro), _nucleoPulso.A.EmpatePct, ahora, false);
                }
                else if (L.Doms.Count > 1) L.Doms = L.Doms.OrderBy(d => Math.Abs(d.Fut - precio)).ToList();   // 3.0.9: D1 = la mas cercana
                EstadoEscribir(raiz, ahora, L, c, precio, utiles, ahora, "pulso", fs);
            }
            catch (Exception e) { EstadoError(ahora, "EstadoPulsoCambio", e); }
            finally { Interlocked.Exchange(ref _pulsoCorriendo, 0); }
        }

        private static void CopiarAjustesNucleo(GammaHoyNucleo desde, GammaHoyNucleo hacia)
        {
            var a = hacia.A; var src = desde.A;
            a.Horizonte = src.Horizonte; a.CuantasDominantes = src.CuantasDominantes; a.RadioDominantesPct = src.RadioDominantesPct;
            a.RadioDominantesMaxPts = src.RadioDominantesMaxPts; a.EmpatePct = src.EmpatePct; a.DominantesDeNoche = src.DominantesDeNoche;
            a.ZeroInterpolado = src.ZeroInterpolado; a.UnaPorLado = src.UnaPorLado; a.Centroide = src.Centroide; a.RadioCentroidePts = src.RadioCentroidePts;
        }

        private void EstadoError(DateTime ahora, string donde, Exception e)
        {
            if ((ahora - _estadoUltimoError).TotalSeconds >= 60)
            {
                _estadoUltimoError = ahora;
                Log("estado: " + donde + ": " + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>Arma y escribe el estado. Serializado: Tick y pulso nunca escriben a la vez. Nunca tira hacia afuera.</summary>
        private void EstadoEscribir(string raiz, DateTime ahora, GammaHoyNucleo.Lectura L, Feed.Cadena c, double fut, int utiles, DateTime cuenta, string origen, List<CadenaApi.Fila> fs)
        {
            string ruta = (Estado4Ruta ?? "").Trim();
            if (ruta == "") return;
            lock (_estadoLlave)
            {
                try
                {
                    long cambios = _cadena.Cambios;
                    AnotarCambios(cambios, ahora);
                    string json = EstadoArmar(raiz, ahora, L, c, fut, utiles, cuenta, origen, cambios, fs);
                    EstadoEscribirAtomico(ruta, json);
                    _estadoEscritos++;
                    if (origen == "pulso") _estadoEscritosPulso++;
                    _estadoUltimaEscritura = ahora;
                    _estadoCambiosEscritos = cambios;
                    double p = PrecioGrafico(); _estadoPrecioEscrito = p > 0 ? p : fut;
                }
                catch (Exception e)
                {
                    if ((ahora - _estadoUltimoError).TotalSeconds >= 60)
                    {
                        _estadoUltimoError = ahora;
                        Log("estado: no pude escribir " + ruta + ": " + e.GetType().Name + ": " + e.Message);
                    }
                }
            }
        }

        /// <summary>Archivo temporal al lado + File.Move con pisado: el lector nunca ve un archivo a medio escribir.</summary>
        private static void EstadoEscribirAtomico(string ruta, string contenido)
        {
            var dir = Path.GetDirectoryName(ruta);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = ruta + "." + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ".tmp";
            File.WriteAllText(tmp, contenido, new UTF8Encoding(false));
            try { File.Move(tmp, ruta, true); }
            catch
            {
                // por si el lector lo tiene abierto sin compartir: segundo intento con Replace (requiere que el destino exista)
                if (File.Exists(ruta)) File.Replace(tmp, ruta, null, true);
                else throw;
            }
        }

        private static string JT(DateTime t) => t == DateTime.MinValue ? "null" : "\"" + t.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) + "\"";

        private string EstadoArmar(string raiz, DateTime ahora, GammaHoyNucleo.Lectura L, Feed.Cadena c, double fut, int utiles, DateTime cuenta, string origen, long cambios, List<CadenaApi.Fila> fs)
        {
            var inv = CultureInfo.InvariantCulture;
            double precio = PrecioGrafico();
            var foto = _cadena.Foto();
            var regla = ReglaEfectiva();
            double pulsoS = _pulsoUltimoCambioUtc == DateTime.MinValue ? double.NaN : Math.Max(0, (ahora - _pulsoUltimoCambioUtc).TotalSeconds);

            var sb = new StringBuilder(64 * 1024);
            sb.Append("{\"generado\":").Append(JT(ahora)).Append(",\"version\":").Append(JS(VERSION)).Append(",\"raiz\":").Append(JS(raiz))
              .Append(",\"grafico\":").Append(JS(CodigoGrafico())).Append(",\"precio\":").Append(J(precio, "0.####"))
              .Append(",\"precio_cuenta\":").Append(J(fut, "0.####")).Append(",\"cuenta_utc\":").Append(JT(cuenta))
              .Append(",\"origen\":").Append(JS(origen)).Append(",\"cambios\":").Append(cambios).Append(",\"pulso_s\":").Append(J(pulsoS, "0.#"))
              .Append(",\"pulso_seg\":").Append(Math.Max(1, Math.Min(5, Estado3PulsoSeg)))
              .Append(",\"regla\":").Append(JS(regla.ToString())).Append(",\"horizonte\":").Append(JS(Horizonte3.ToString()))
              .Append(",\"strikes_utiles\":").Append(utiles);

            // la foto del libro por la API
            sb.Append(",\"libro\":{\"activo\":").Append(_cadena.Activa ? "true" : "false").Append(",\"via\":").Append(JS(foto.Via)).Append(",\"conector\":").Append(JS(foto.Conector))
              .Append(",\"futuro_api\":").Append(JS(foto.Futuro)).Append(",\"futuro_api_precio\":").Append(J(_cadena.Futuro, "0.####"))
              .Append(",\"series\":").Append(foto.Series).Append(",\"contratos\":").Append(foto.Contratos).Append(",\"suscritos\":").Append(foto.Suscritos)
              .Append(",\"con_resumen\":").Append(foto.ConResumen).Append(",\"con_puntas\":").Append(foto.ConPuntas).Append(",\"con_oi\":").Append(foto.ConOI)
              .Append(",\"rechazadas\":").Append(foto.Rechazadas).Append(",\"errores\":").Append(foto.Errores).Append(",\"vencidas_fuera\":").Append(foto.Vencidas)
              .Append(",\"edad_s\":").Append(J(foto.EdadSegundos, "0.#")).Append(",\"ultimo_resumen_utc\":").Append(JT(foto.UltimoResumenUtc))
              .Append(",\"ultimo_armado_utc\":").Append(JT(foto.UltimoArmadoUtc)).Append(",\"texto\":").Append(JS(foto.Texto)).Append('}');

            // las zonas
            sb.Append(",\"zonas\":");
            if (L == null) sb.Append("null");
            else
            {
                sb.Append("{\"doms\":[");
                for (int i = 0; i < L.Doms.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    string vig = null;
                    if (regla == ReglaDominantes3.Tres) { var t0 = Hist.DesdeCuando(L.Doms[i].Fut); if (t0.HasValue) vig = t0.Value.ToString("HH:mm'Z'", inv); }
                    sb.Append("{\"fut\":").Append(J(L.Doms[i].Fut, "0.####")).Append(",\"gex\":").Append(J(L.Doms[i].Gex, "0")).Append(",\"etq\":\"D").Append(i + 1).Append("\",\"vig_desde\":").Append(JS(vig)).Append('}');
                }
                sb.Append("],\"zero_vol\":").Append(J(L.ZeroVol, "0.####")).Append(",\"zero_oi\":").Append(J(L.ZeroOi, "0.####")).Append(",\"zero_modo\":").Append(JS(L.ZeroModo))
                  .Append(",\"net_vol\":").Append(J(L.NetVol, "0")).Append(",\"net_oi\":").Append(J(L.NetOi, "0"))
                  .Append(",\"max_abs_vol\":").Append(J(L.MaxAbsVol, "0")).Append(",\"max_abs_oi\":").Append(J(L.MaxAbsOi, "0"))
                  .Append(",\"libro_dom\":").Append(JS(L.LibroDom)).Append(",\"cuadrante\":").Append(JS(L.Cuadrante))
                  .Append(",\"mp_vol\":").Append(J(L.MpVol, "0.####")).Append(",\"mn_vol\":").Append(J(L.MnVol, "0.####"))
                  .Append(",\"mas_cerca_dias\":").Append(J(L.MasCerca, "0.####"));
                sb.Append(",\"hist\":");
                if (regla != ReglaDominantes3.Tres) sb.Append("null");
                else
                {
                    var h = Hist.Foto();
                    sb.Append("{\"resumen\":").Append(JS(Hist.Resumen())).Append(",\"vig\":[");
                    for (int i = 0; i < h.Vig.Count; i++) { if (i > 0) sb.Append(','); sb.Append('[').Append(J(h.Vig[i].Fut, "0.####")).Append(",\"").Append(h.Vig[i].T0.ToString("HH:mm", inv)).Append("Z\"]"); }
                    sb.Append("],\"cont\":[");
                    for (int i = 0; i < h.Cont.Count; i++) { if (i > 0) sb.Append(','); sb.Append('[').Append(J(h.Cont[i].Clave, "0.####")).Append(',').Append(h.Cont[i].N).Append(']'); }
                    sb.Append("],\"pasos\":").Append(h.Pasos).Append('}');
                }
                sb.Append('}');
            }

            // la escalera: solo el vencimiento mas cercano ('Hoy')
            sb.Append(",\"escalera\":");
            if (L == null || c == null || c.Dias.Length == 0) sb.Append("null");
            else
            {
                double diasHoy = c.Dias[0];
                sb.Append("{\"dias\":").Append(J(diasHoy, "0.######")).Append(",\"filas\":[");
                // las filas del nucleo del vencimiento 0 por strike (K redondeado a 1/100)
                var porK = new Dictionary<long, Feed.Fila>();
                foreach (var f in c.Filas) if (f.V == 0) porK[(long)Math.Round(f.K * 100)] = f;
                // las filas crudas de la API del mismo vencimiento (por lado): bid/ask, volumen de ayer, ultimo, codigo
                var apiC = new Dictionary<long, CadenaApi.Fila>(); var apiP = new Dictionary<long, CadenaApi.Fila>();
                try
                {
                    if (fs == null) fs = _cadena.Filas();
                    if (fs != null)
                        foreach (var f in fs)
                        {
                            if (Math.Abs(Math.Round(f.Dias, 4) - Math.Round(diasHoy, 4)) > 0.00051) continue;
                            long k = (long)Math.Round(f.K * 100);
                            if (f.EsCall) apiC[k] = f; else apiP[k] = f;
                        }
                }
                catch (Exception e) { Registro.Excepcion(LOG, "EstadoArmar.Filas", e); }

                bool primero = true;
                foreach (var s in L.Perfil.OrderBy(x => x.K))
                {
                    long k = (long)Math.Round(s.K * 100);
                    porK.TryGetValue(k, out var f); apiC.TryGetValue(k, out var ac); apiP.TryGetValue(k, out var ap);
                    if (f == null && ac == null && ap == null && s.Dte > diasHoy + 0.001) continue;   // strike que solo aporta otro vencimiento
                    if (!primero) sb.Append(','); primero = false;
                    sb.Append("{\"K\":").Append(J(s.K, "0.####")).Append(",\"fut\":").Append(J(s.Fut, "0.####")).Append(",\"K0\":").Append(f != null && f.K0 > 0 ? J(f.K0, "0.####") : "null")
                      .Append(",\"gexVol\":").Append(J(s.GexVol, "0")).Append(",\"gexOi\":").Append(J(s.GexOi, "0")).Append(",\"conv\":").Append(J(s.Conv, "0"))
                      .Append(",\"oi\":").Append(J(s.Oi, "0")).Append(",\"volHoy\":").Append(J(s.VolHoy, "0")).Append(",\"ivMedia\":").Append(J(s.IvMedia, "0.#####"))
                      .Append(",\"oiC\":").Append(f != null ? J(f.OiC, "0") : "null").Append(",\"oiP\":").Append(f != null ? J(f.OiP, "0") : "null")
                      .Append(",\"ivC\":").Append(f != null && f.IvC > 0 ? J(f.IvC, "0.#####") : "null").Append(",\"ivP\":").Append(f != null && f.IvP > 0 ? J(f.IvP, "0.#####") : "null")
                      .Append(",\"volC\":").Append(f != null ? J(f.VolC, "0") : "null").Append(",\"volP\":").Append(f != null ? J(f.VolP, "0") : "null")
                      .Append(",\"volAyerC\":").Append(ac != null ? J(ac.VolAyer, "0") : "null").Append(",\"volAyerP\":").Append(ap != null ? J(ap.VolAyer, "0") : "null")
                      .Append(",\"lastC\":").Append(ac != null && ac.Last > 0 ? J(ac.Last, "0.####") : "null").Append(",\"lastP\":").Append(ap != null && ap.Last > 0 ? J(ap.Last, "0.####") : "null")
                      .Append(",\"bidC\":").Append(ac != null && ac.ConPuntas ? J(ac.Bid, "0.####") : "null").Append(",\"askC\":").Append(ac != null && ac.ConPuntas ? J(ac.Ask, "0.####") : "null")
                      .Append(",\"bidVolC\":").Append(ac != null ? J(ac.BidVol, "0") : "null").Append(",\"askVolC\":").Append(ac != null ? J(ac.AskVol, "0") : "null").Append(",\"bidVolP\":").Append(ap != null ? J(ap.BidVol, "0") : "null").Append(",\"askVolP\":").Append(ap != null ? J(ap.AskVol, "0") : "null")
                      .Append(",\"bidP\":").Append(ap != null && ap.ConPuntas ? J(ap.Bid, "0.####") : "null").Append(",\"askP\":").Append(ap != null && ap.ConPuntas ? J(ap.Ask, "0.####") : "null")
                      .Append(",\"codC\":").Append(JS(ac?.Codigo)).Append(",\"codP\":").Append(JS(ap?.Codigo)).Append('}');
                }
                sb.Append("]}");
            }
            sb.Append(",\"escritos\":").Append(_estadoEscritos).Append(",\"escritos_pulso\":").Append(_estadoEscritosPulso).Append(",\"saltados_tope\":").Append(_pulsoSaltadosPorTope).Append('}');
            return sb.ToString();
        }
    }
}
