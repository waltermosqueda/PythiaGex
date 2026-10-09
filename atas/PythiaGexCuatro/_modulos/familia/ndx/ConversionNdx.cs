// ConversionNdx.cs — PythiaGex 4.1, modulo Familia NDX (B3b). C7 (base NDX SINCRONIZADA), C9 (vigencia de la cadena congelada) y
// oi_fresco, port EXACTO de backtest_familia.conversion() + base_ndx() + vigencia_cboe() (lo que usa preview_niveles.py).
//
// OJO: NO es la regla de ConversionCboe37 (propuesta 3.7.1). Esa cambio dos cosas respecto de backtest_familia (A2: de dia la mediana
// ACUMULADA de toda la rueda en vez de las ultimas 24; y el modo dia ya no exige que la foto sea viva). La vista previa (la referencia de
// paridad) usa backtest_familia, asi que aca va esa. De la 3.7 se tomo la idea de persistir las muestras con su contrato.
//
// C7, por cada foto en orden de 'generado':
//   t_spot = cadena.ts - 900 s; precio = MNQ en t_spot (ICinta.Precio: tick o vela m2 interpolada); vivo = el spot cambio contra alguna
//   foto de los 600 s previos; muestra = precio - spot (si vivo); en_hora = NY 09:35-15:59 de lunes a viernes.
//   Vivo y en hora: la muestra va a ruedas[fecha NY] y al roll de 24 (se reinicia al cambiar el dia NY).
//   modo 'dia' = vivo, en hora y roll con >= 5 (robusta 3 MAD, piso 0,5): base = mediana robusta de las ultimas 24.
//   modo 'noche': ref = la rueda mas reciente (contando la de hoy) con >= 20 muestras; base = robusta(rueda) x (EXP - t) / (EXP - cierre de
//   esa rueda); nada si el contrato de la rueda no es el de t (roll).
// C9: vigencia = generado + 1500 s; si la cadena esta congelada (t_dato NY >= 15:59) vale hasta max(eso, proxima apertura 13:30 UTC).
// oi_fresco: el OI de CBOE es 'de dos sesiones' desde la noche hasta que la suma de OI salta > 5 % de madrugada (solo rotula).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PythiaGexCuatro.Familia.Ndx
{
    public sealed class OpcionesNdx
    {
        /// <summary>true = horas fijas en UTC y tabla de contratos del Python (solo el arnes). false = hora de NY y contrato del grafico (indicador).</summary>
        public bool ParidadPython = false;
        /// <summary>Contrato del grafico ("Z6", o el codigo entero "MNQZ6"); en modo corregido es el contrato de toda muestra nueva y de todo minuto.
        /// Si el codigo no trae mes (continua "MNQ"), el trimestral se saca por fecha (CalendarioNdx.ContratoPorFecha).</summary>
        public string ContratoGrafico = "";
        /// <summary>Opcional: contrato en un instante (si se da, pisa a ContratoGrafico y a la tabla de paridad).</summary>
        public Func<DateTime, string> ContratoDe;
        /// <summary>Opcional: vencimiento (UTC) de un contrato para el carry de la noche. Default: tabla del Python en paridad, tercer viernes 09:30 NY si no.</summary>
        public Func<string, DateTime> VencimientoDe;
        /// <summary>Opcional (modo corregido): dia habil en NY para 'en_hora' (feriados). Default: lunes a viernes.</summary>
        public Func<DateTime, bool> EsHabilNy;
        /// <summary>Carpeta de datos propia (%APPDATA%/ATAS/PythiaGex4). null o "" = no persiste nada.</summary>
        public string CarpetaDatos;
        /// <summary>Dias de calendario previos de cadenas (preview_niveles.DIAS_CBOE).</summary>
        public int DiasHistoria = 6;
        /// <summary>Minutos por tanda de la cache del zero (preview_niveles.historico calcula de a 240 claves desde el inicio de la sesion y
        /// cada tanda arranca la cache de cero). 0 = una sola cache por sesion.</summary>
        public int TandaZeroMin = 240;
        /// <summary>Si el motor arranca a mitad de una tanda (reinicio), rehacer la cache del zero con los minutos previos de la tanda (paridad
        /// con la corrida historica tambien despues de un reinicio). Cuesta como mucho 240 minutos de cuenta, una vez.</summary>
        public bool CalentarCacheZero = true;
        /// <summary>false (paridad): la clave de la cache del zero es la de la vista previa (foto, tramo de 900 s, round(S/50)). true: ademas
        /// cuantas filas entran en el horizonte, asi el 0DTE que vence a las 16:00 NY no queda adentro del zero por la cache (en la vista
        /// previa historica pasa hasta 15 min: medido 07-10 20:00-20:01 UTC, ZEST_NDX_vol 58 pts corrido). Decide el integrador.</summary>
        public bool ZeroCacheConHorizonte = false;
        public Action<string> Log;

        public string Contrato(DateTime utc)
        {
            if (ContratoDe != null) return ContratoDe(utc) ?? "";
            if (ParidadPython) return CalendarioNdx.ContratoParidad(utc);
            return CalendarioNdx.CodigoMes(ContratoGrafico) ?? CalendarioNdx.ContratoPorFecha(utc);   // continua sin mes: por fecha
        }

        public DateTime Vencimiento(string contrato)
        {
            if (VencimientoDe != null) return VencimientoDe(contrato);
            return ParidadPython ? CalendarioNdx.ExpiraParidad(contrato) : CalendarioNdx.ExpiraContrato(contrato);
        }
    }

    /// <summary>Lo que se sabe de una foto de NDX con lo observado hasta su 'generado' (los campos que backtest_familia.conversion anota en f).</summary>
    public sealed class EstadoFotoNdx
    {
        public FotoCadena Foto;
        public int Indice;
        public DateTime TSpotUtc;
        public double Precio = double.NaN;
        public bool PrecioDeTick, PrecioGuardado;
        public bool Vivo, EnHora;
        public double Muestra = double.NaN;
        public string Contrato = "";
        public bool ModoDia;
        public double BaseDia = double.NaN;
        public int NDia;
        public bool TieneRef;
        public DateTime RefDia;                       // fecha NY de la rueda de referencia
        public double RefBa = double.NaN, RefBu = double.NaN;
        public string RefContrato = "";
        public int RefN;
        public bool OiViejo, Congelada;
        public DateTime Vigencia;
        public string Modo => ModoDia ? "dia" : "noche";
    }

    /// <summary>Una muestra guardada en familia/muestras-NDX-&lt;sesion&gt;.jsonl (para rehacer C7 al reiniciar sin la cinta de los dias previos).</summary>
    public sealed class MuestraNdxGuardada
    {
        public DateTime TsUtc, GeneradoUtc;
        public double Spot, Precio;
        public bool Tick;
        public string Contrato = "";
    }

    public sealed class ConversionNdx
    {
        public const double RETRASO_S = 900, VIVO_S = 600, VIDA_CBOE_S = 1500;
        public const int MIN_DIA = 5, MIN_RUEDA = 20, VENTANA = 24;
        public const double PISO = 0.5;

        private readonly OpcionesNdx _op;
        private List<EstadoFotoNdx> _estados = new List<EstadoFotoNdx>();

        public ConversionNdx(OpcionesNdx op) { _op = op ?? new OpcionesNdx(); }

        public IReadOnlyList<EstadoFotoNdx> Estados => _estados;

        /// <summary>backtest_familia.robusta (codigo comun de B3a: NumFam.Robusta).</summary>
        public static (double V, int N) Robusta(IEnumerable<double> x, double piso) => NumFam.Robusta(x, piso);

        /// <summary>Rehace el estado de TODAS las fotos (en orden de generado; causal: el estado de una foto solo mira las anteriores).
        /// precioEn(t) = MNQ en t (NaN si no hay) y si fue un tick; guardadas = muestras persistidas por ts (pueden ser null).</summary>
        public void Recalcular(IReadOnlyList<FotoCadena> fotos, Func<DateTime, (double Precio, bool Tick)> precioEn, IDictionary<DateTime, MuestraNdxGuardada> guardadas)
        {
            bool par = _op.ParidadPython;
            var est = new List<EstadoFotoNdx>(fotos.Count);
            var ruedas = new SortedDictionary<DateTime, (List<double> M, string Contrato)>();
            var roll24 = new List<double>();
            DateTime rollDia = DateTime.MinValue;
            bool oiViejo = false; double oiPrev = double.NaN;
            // cache de la rueda de referencia (robusta de toda la rueda): cambia solo si la rueda crece
            DateTime cacheDia = DateTime.MinValue; int cacheN = -1; double cacheBa = double.NaN, cacheBu = double.NaN;

            for (int i = 0; i < fotos.Count; i++)
            {
                var f = fotos[i];
                var e = new EstadoFotoNdx { Foto = f, Indice = i };
                e.TSpotUtc = f.TsUtc.AddSeconds(-RETRASO_S);
                // vivo: el spot cambio contra alguna foto de los 600 s previos (por generado)
                for (int j = i - 1; j >= 0 && (f.GeneradoUtc - fotos[j].GeneradoUtc).TotalSeconds <= VIVO_S; j--)
                    if (Math.Abs(fotos[j].Spot - f.Spot) > 1e-9) { e.Vivo = true; break; }
                // precio del MNQ en t_spot: la muestra guardada (si fue tick, o si la cinta no tiene) o la cinta
                MuestraNdxGuardada g = null;
                if (guardadas != null) guardadas.TryGetValue(f.TsUtc, out g);
                (double Precio, bool Tick) pc = precioEn != null ? precioEn(e.TSpotUtc) : (double.NaN, false);
                if (g != null && !double.IsNaN(g.Precio) && (g.Tick || double.IsNaN(pc.Precio)))
                { e.Precio = g.Precio; e.PrecioDeTick = g.Tick; e.PrecioGuardado = true; e.Contrato = g.Contrato ?? ""; }
                else
                { e.Precio = pc.Precio; e.PrecioDeTick = pc.Tick; e.Contrato = _op.Contrato(e.TSpotUtc); }
                if (e.Vivo && !double.IsNaN(e.Precio) && f.Spot > 0) e.Muestra = e.Precio - f.Spot;
                var ny = CalendarioNdx.ANy(e.TSpotUtc);
                var diaNy = ny.Date;
                e.EnHora = CalendarioNdx.EnHora(e.TSpotUtc, par ? null : _op.EsHabilNy);
                if (e.Vivo && e.EnHora && !double.IsNaN(e.Muestra))
                {
                    if (!ruedas.TryGetValue(diaNy, out var r)) { r = (new List<double>(), e.Contrato); ruedas[diaNy] = r; }
                    r.M.Add(e.Muestra);
                    if (rollDia != diaNy) { roll24.Clear(); rollDia = diaNy; }
                    roll24.Add(e.Muestra);
                    if (roll24.Count > VENTANA) roll24.RemoveRange(0, roll24.Count - VENTANA);
                }
                double b24 = double.NaN; int nb = 0;
                if (rollDia == diaNy && e.Vivo && e.EnHora) (b24, nb) = Robusta(roll24, PISO);
                e.ModoDia = e.Vivo && e.EnHora && nb >= MIN_DIA;
                e.BaseDia = e.ModoDia ? b24 : double.NaN;
                e.NDia = nb;
                foreach (var kv in ruedas.Reverse())
                {
                    if (kv.Value.M.Count < MIN_RUEDA) continue;
                    if (kv.Key != cacheDia || kv.Value.M.Count != cacheN)
                    {
                        cacheDia = kv.Key; cacheN = kv.Value.M.Count;
                        cacheBa = Robusta(kv.Value.M, PISO).V;
                        cacheBu = Robusta(kv.Value.M.Skip(Math.Max(0, kv.Value.M.Count - VENTANA)), PISO).V;
                    }
                    e.TieneRef = true; e.RefDia = kv.Key; e.RefBa = cacheBa; e.RefBu = cacheBu; e.RefContrato = kv.Value.Contrato; e.RefN = kv.Value.M.Count;
                    break;
                }
                // oi_fresco (C9 de la sensibilidad 'fresco'): horas del reloj (UTC en paridad, NY+4 h en corregido)
                int hmu = CalendarioNdx.MinutoDelDia(CalendarioNdx.Reloj(f.GeneradoUtc, par));
                int hprev = i > 0 ? CalendarioNdx.MinutoDelDia(CalendarioNdx.Reloj(fotos[i - 1].GeneradoUtc, par)) : 12 * 60;
                const int H1330 = 13 * 60 + 30, H2200 = 22 * 60, H0300 = 3 * 60;
                if (hmu >= H1330 && hmu < H2200) oiViejo = false;
                else if ((hmu >= H2200 || hmu < H0300) && !oiViejo && ((hprev >= H1330 && hprev < H2200) || (i > 0 && (f.GeneradoUtc - fotos[i - 1].GeneradoUtc).TotalSeconds > 6 * 3600)))
                    oiViejo = true;
                else if (hmu >= H0300 && hmu < H1330 && oiViejo && !double.IsNaN(oiPrev) && oiPrev != 0 && Math.Abs(f.OiTotal - oiPrev) / oiPrev > 0.05)
                    oiViejo = false;
                e.OiViejo = oiViejo;
                if (f.OiTotal > 0) oiPrev = f.OiTotal;
                // C9
                e.Congelada = CalendarioNdx.Congelada(f.DatoUtc);
                var h1 = f.GeneradoUtc.AddSeconds(VIDA_CBOE_S);
                if (e.Congelada)
                {
                    var prox = CalendarioNdx.ProximaApertura(f.GeneradoUtc, par);
                    if (prox > h1) h1 = prox;
                }
                e.Vigencia = h1;
                est.Add(e);
            }
            _estados = est;
        }

        /// <summary>backtest_familia.base_ndx(f, t, 'sync'): la base vigente en el minuto t con la foto e. NaN si no hay. texto = para la pestaña.</summary>
        public double Base(EstadoFotoNdx e, DateTime tUtc, out string texto)
        {
            var inv = CultureInfo.InvariantCulture;
            if (e.ModoDia)
            {
                texto = "base " + e.BaseDia.ToString("0.00", inv) + " (mediana de las ultimas 24 muestras sincronizadas de esta rueda)";
                return e.BaseDia;
            }
            if (!e.TieneRef) { texto = "sin rueda sincronizada con >= " + MIN_RUEDA + " muestras: NDX no se dibuja"; return double.NaN; }
            string k = _op.Contrato(tUtc);
            if (!string.Equals(e.RefContrato, k, StringComparison.OrdinalIgnoreCase))
            {
                texto = "la rueda " + e.RefDia.ToString("yyyy-MM-dd", inv) + " es de " + e.RefContrato + " y el grafico es " + k + ": se descarta (roll)";
                return double.NaN;
            }
            var exp = _op.Vencimiento(e.RefContrato);
            var tClose = CalendarioNdx.CierreRueda(e.RefDia, _op.ParidadPython);
            double fac = 1.0;
            if (exp != default(DateTime))
            {
                double den = (exp - tClose).TotalSeconds;
                fac = den > 0 ? (exp - tUtc).TotalSeconds / den : 1.0;
            }
            double b = e.RefBa * fac;
            texto = "base " + b.ToString("0.00", inv) + " (mediana de la rueda " + e.RefDia.ToString("yyyy-MM-dd", inv) + " sincronizada, " + e.RefN + " muestras: "
                    + e.RefBa.ToString("0.00", inv) + ", menos el carry)";
            return b;
        }
    }

    /// <summary>Persistencia de las muestras de C7 en &lt;CarpetaDatos&gt;/familia/muestras-NDX-&lt;sesion&gt;.jsonl (una linea por foto con precio;
    /// append). Al reiniciar de noche se rehace C7 de los dias previos sin la cinta vieja.</summary>
    public sealed class MuestrasNdx
    {
        private readonly string _carpeta;
        private readonly Dictionary<DateTime, MuestraNdxGuardada> _mapa = new Dictionary<DateTime, MuestraNdxGuardada>();
        private readonly Action<string> _log;

        public MuestrasNdx(string carpetaDatos, Action<string> log)
        {
            _carpeta = string.IsNullOrEmpty(carpetaDatos) ? null : Path.Combine(carpetaDatos, "familia");
            _log = log;
        }

        public IDictionary<DateTime, MuestraNdxGuardada> Mapa => _mapa;
        public bool Activa => _carpeta != null;

        private static string Ruta(string carpeta, DateTime sesion) => Path.Combine(carpeta, "muestras-NDX-" + sesion.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jsonl");

        /// <summary>Lee las sesiones [desde, hasta] (fechas de sesion). Tolera lineas rotas.</summary>
        public void Cargar(DateTime sesionDesde, DateTime sesionHasta)
        {
            if (_carpeta == null) return;
            var inv = CultureInfo.InvariantCulture;
            for (var d = sesionDesde.Date; d <= sesionHasta.Date; d = d.AddDays(1))
            {
                var p = Ruta(_carpeta, d);
                if (!File.Exists(p)) continue;
                try
                {
                    using var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var sr = new StreamReader(fs, Encoding.UTF8);
                    string l;
                    while ((l = sr.ReadLine()) != null)
                    {
                        var m = Parsear(l);
                        if (m == null) continue;
                        if (_mapa.TryGetValue(m.TsUtc, out var v) && v.Tick && !m.Tick) continue;
                        _mapa[m.TsUtc] = m;
                    }
                }
                catch (Exception ex) { _log?.Invoke("muestras NDX: no pude leer " + p + ": " + ex.Message); }
            }
        }

        /// <summary>Guarda las muestras nuevas (o que pasaron de vela a tick). Solo fotos con precio y con t_spot ya asentado en la cinta.</summary>
        public int Guardar(IReadOnlyList<EstadoFotoNdx> estados, DateTime asentadoHastaUtc, bool paridad)
        {
            if (_carpeta == null) return 0;
            var inv = CultureInfo.InvariantCulture;
            var porSesion = new Dictionary<DateTime, StringBuilder>();
            int n = 0;
            foreach (var e in estados)
            {
                if (e.PrecioGuardado || double.IsNaN(e.Precio) || e.TSpotUtc > asentadoHastaUtc) continue;
                if (_mapa.TryGetValue(e.Foto.TsUtc, out var v) && (v.Tick || !e.PrecioDeTick)) continue;
                var m = new MuestraNdxGuardada { TsUtc = e.Foto.TsUtc, GeneradoUtc = e.Foto.GeneradoUtc, Spot = e.Foto.Spot, Precio = e.Precio, Tick = e.PrecioDeTick, Contrato = e.Contrato };
                _mapa[m.TsUtc] = m;
                var ses = CalendarioNdx.SesionDe(e.Foto.GeneradoUtc, paridad);
                if (!porSesion.TryGetValue(ses, out var sb)) { sb = new StringBuilder(); porSesion[ses] = sb; }
                sb.Append("{\"ts\":\"").Append(m.TsUtc.ToString("yyyy-MM-dd HH:mm:ss", inv)).Append("\",\"generado\":\"").Append(m.GeneradoUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", inv))
                  .Append("\",\"spot\":").Append(m.Spot.ToString("R", inv)).Append(",\"precio\":").Append(m.Precio.ToString("R", inv))
                  .Append(",\"tick\":").Append(m.Tick ? "true" : "false").Append(",\"muestra\":").Append(double.IsNaN(e.Muestra) ? "null" : e.Muestra.ToString("R", inv))
                  .Append(",\"vivo\":").Append(e.Vivo ? "true" : "false").Append(",\"en_hora\":").Append(e.EnHora ? "true" : "false")
                  .Append(",\"contrato\":\"").Append(m.Contrato).Append("\"}\n");
                n++;
            }
            if (n == 0) return 0;
            try
            {
                Directory.CreateDirectory(_carpeta);
                foreach (var kv in porSesion)
                    File.AppendAllText(Ruta(_carpeta, kv.Key), kv.Value.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex) { _log?.Invoke("muestras NDX: no pude escribir: " + ex.Message); }
            return n;
        }

        /// <summary>Borra los archivos de muestras de sesiones anteriores a 'antesDe' (retencion).</summary>
        public void Podar(DateTime antesDe)
        {
            if (_carpeta == null || !Directory.Exists(_carpeta)) return;
            try
            {
                foreach (var p in Directory.GetFiles(_carpeta, "muestras-NDX-*.jsonl"))
                {
                    var nom = Path.GetFileNameWithoutExtension(p);
                    if (nom.Length < 23) continue;
                    if (DateTime.TryParseExact(nom.Substring(nom.Length - 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && d < antesDe.Date)
                        File.Delete(p);
                }
            }
            catch (Exception ex) { _log?.Invoke("muestras NDX: poda: " + ex.Message); }
        }

        private static MuestraNdxGuardada Parsear(string l)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(l) || !l.TrimEnd().EndsWith("}")) return null;
                using var doc = System.Text.Json.JsonDocument.Parse(l);
                var r = doc.RootElement;
                var inv = CultureInfo.InvariantCulture;
                var m = new MuestraNdxGuardada();
                if (!DateTime.TryParseExact(r.GetProperty("ts").GetString(), "yyyy-MM-dd HH:mm:ss", inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var ts)) return null;
                m.TsUtc = DateTime.SpecifyKind(ts, DateTimeKind.Utc);
                if (r.TryGetProperty("generado", out var g) && DateTime.TryParse(g.GetString(), inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var gd)) m.GeneradoUtc = DateTime.SpecifyKind(gd, DateTimeKind.Utc);
                m.Spot = r.TryGetProperty("spot", out var s) && s.ValueKind == System.Text.Json.JsonValueKind.Number ? s.GetDouble() : double.NaN;
                m.Precio = r.TryGetProperty("precio", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.Number ? p.GetDouble() : double.NaN;
                m.Tick = r.TryGetProperty("tick", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.True;
                m.Contrato = r.TryGetProperty("contrato", out var c) ? c.GetString() ?? "" : "";
                return double.IsNaN(m.Precio) ? null : m;
            }
            catch { return null; }
        }
    }
}
