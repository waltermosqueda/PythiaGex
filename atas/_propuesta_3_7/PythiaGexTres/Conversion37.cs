using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PythiaGexTres
{
    /// <summary>
    /// C5 (3.7.0): COMO SE LLEVA UNA CADENA DE CBOE AL FUTURO, SINCRONIZADA. Sin ATAS (lo corre tambien Simulador37).
    ///
    /// Misma regla que laboratorio/tres/auditoria_0810/backtest_familia.py (conversion() + base_ndx(), correcciones C7/C8 de su
    /// pre-registro), que es la verificada:
    ///   muestra = precio del MNQ en el SEGUNDO (cadena.ts - 900 s) [menos | dividido] el spot de la cadena, SOLO con el spot VIVO
    ///             (distinto del de alguna foto de los 10 min previos, por 'generado').
    ///   NDX (base aditiva): solo muestras con la hora NY del spot entre 09:35 y 15:59 (dia habil). De dia (en hora y &gt;= 5 muestras de la
    ///       rueda en curso): 3.7.1 (A2) mediana robusta (3 MAD, piso 0,5) ACUMULADA de la rueda en curso (la 3.7.0 usaba las ultimas 24 y se
    ///       movia 6 pts en la rueda). Fuera de eso: mediana robusta de TODA
    ///       la ultima rueda con &gt;= 20 muestras, por el decaimiento del carry (T_vto - t) / (T_vto - 16:00 NY de esa rueda), y nada si esa
    ///       rueda es de otro contrato (roll). Medido: noche del 08-10 = 243,07 (backtest_familia.md); la 3.0 dibujaba 248,15 (rueda con
    ///       16 min de atraso) y despues 238,36 (forwards).  rev_05: con decaimiento el error contra la base viva del dia siguiente es 3,67
    ///       pts (mediana) contra 5,32 de los forwards y 5,08 sin decaimiento.
    ///   QQQ (razon): 3.7.1 (A1) solo muestras EN HORA (09:35-15:59 NY, como NDX: el after-hours y el pre-market movian la razon y
    ///       prendian la alarma '⚠base' falsa) y la misma regla que NDX: de dia la mediana robusta (piso 0,02 % de la mediana) ACUMULADA de la
    ///       rueda en curso (&gt;= 5); fuera de hora la de la ultima rueda (&gt;= 20), que es la ultima calculada en hora; sin tope de 24 h; nada si
    ///       cambio el contrato.
    /// El estado se persiste (Serializar/Cargar) con el contrato; no vence por tiempo.
    /// </summary>
    public sealed class ConversionCboe37
    {
        public readonly bool PorRazon;
        public const double RETRASO_S = 900, VIVO_S = 600;
        public const int MIN_DIA = 5, MIN_RUEDA = 20, VENTANA = 24;

        private readonly object _llave = new object();
        private readonly List<(DateTime Gen, double Spot)> _hist = new List<(DateTime, double)>();
        private readonly SortedDictionary<DateTime, (List<double> M, string Contrato)> _ruedas = new SortedDictionary<DateTime, (List<double>, string)>();
        private readonly List<double> _roll = new List<double>();
        private DateTime _rollDia = DateTime.MinValue;
        private string _rollContrato = "";
        private bool _modoDia;
        private double _baseDia = double.NaN, _razon = double.NaN;
        private int _nDia, _nRazon;

        public DateTime UltimoTsUtc { get; private set; } = DateTime.MinValue;
        public bool UltimaViva { get; private set; }
        public bool UltimaEnHora { get; private set; }
        public double UltimaMuestra { get; private set; } = double.NaN;
        /// <summary>Hora (UTC) del SPOT de la ultima muestra viva aceptada (cadena.ts - 900 s).</summary>
        public DateTime UltimaMuestraSpotUtc { get; private set; } = DateTime.MinValue;
        public int MuestrasVivas { get; private set; }

        public ConversionCboe37(bool porRazon) { PorRazon = porRazon; }

        /// <summary>3.7.1 (A1): la hora del SPOT de una muestra esta en la rueda (09:35-15:59 NY de un dia habil; feriados: SUPUESTO, no estan).
        /// La misma para NDX, QQQ y los pares de la alarma (ParesFamilia37).</summary>
        public static bool EnHora(DateTime tSpotUtc)
        {
            var ny = Seleccion37.ANy(tSpotUtc);
            var hm = ny.TimeOfDay;
            return ny.DayOfWeek != DayOfWeek.Saturday && ny.DayOfWeek != DayOfWeek.Sunday && hm >= new TimeSpan(9, 35, 0) && hm < new TimeSpan(16, 0, 0);
        }

        /// <summary>Una foto nueva de la cadena (en orden de cadena.ts; las repetidas o viejas se ignoran). precio = MNQ en (ts - 900 s)
        /// o NaN. Devuelve true si la proceso.</summary>
        public bool Observar(DateTime generadoUtc, DateTime cadenaTsUtc, double spot, double precio, string contrato)
        {
            lock (_llave)
            {
                if (cadenaTsUtc <= UltimoTsUtc || spot <= 0 || double.IsNaN(spot)) return false;
                UltimoTsUtc = cadenaTsUtc;
                bool vivo = false;
                foreach (var h in _hist)
                    if (h.Gen < generadoUtc && (generadoUtc - h.Gen).TotalSeconds <= VIVO_S && Math.Abs(h.Spot - spot) > 1e-9) { vivo = true; break; }
                _hist.Add((generadoUtc, spot));
                _hist.RemoveAll(h => (generadoUtc - h.Gen).TotalSeconds > 1800);
                var tSpot = cadenaTsUtc.AddSeconds(-RETRASO_S);
                double val = vivo && !double.IsNaN(precio) && precio > 0 ? (PorRazon ? precio / spot : precio - spot) : double.NaN;
                UltimaViva = vivo; UltimaMuestra = val;
                string k = contrato ?? "";
                // 3.7.1 (A1): la MISMA ventana de hora para NDX y QQQ: el spot de la muestra entre 09:35 y 15:59 NY de un dia habil.
                var ny = Seleccion37.ANy(tSpot);
                bool enHora = EnHora(tSpot);
                UltimaEnHora = enHora;
                if (!PorRazon)
                {
                    var dia = ny.Date;
                    if (vivo && enHora && !double.IsNaN(val))
                    {
                        if (!_ruedas.TryGetValue(dia, out var r)) { r = (new List<double>(), k); _ruedas[dia] = r; }
                        r.M.Add(val);
                        if (_rollDia != dia) { _roll.Clear(); _rollDia = dia; }
                        _roll.Add(val); while (_roll.Count > VENTANA) _roll.RemoveAt(0);
                        UltimaMuestraSpotUtc = tSpot; MuestrasVivas++;
                        while (_ruedas.Count > 4) _ruedas.Remove(_ruedas.Keys.First());
                    }
                    // 3.7.1 (A2): de dia la base es la mediana robusta ACUMULADA de la rueda en curso (todas sus muestras vivas), no la de las
                    // ultimas 24: medido en la 3.7.0 la de 24 se movia 6 pts en la rueda y la estela de NDX se corria con ella. La acumulada converge
                    // a la mediana de la rueda entera, que es justo la que usa la noche (factor del carry = 1 a las 16:00 NY): sin salto a las 16:15.
                    // El modo dia ya no depende de que ESTA foto sea viva (un spot repetido no la manda a la noche por un minuto): alcanza con que la
                    // hora este en la rueda y la rueda de hoy tenga >= 5 muestras.
                    var rb = enHora && _ruedas.TryGetValue(dia, out var rh) ? Seleccion37.Robusta(rh.M, 0.5) : (double.NaN, 0);
                    _modoDia = enHora && rb.Item2 >= MIN_DIA;
                    _baseDia = _modoDia ? rb.Item1 : double.NaN; _nDia = rb.Item2;
                }
                else
                {
                    if (_rollContrato != k) { _roll.Clear(); _rollContrato = k; }
                    // 3.7.1 (A1): QQQ cotiza after-hours y pre-market con el ultimo trade de opciones clavado en las 16:14 (CLAUDE.md): esas muestras
                    // movian la razon (41,4407 -> 41,4505 en 10 min) y prendian una alarma '⚠base' falsa. Solo entran muestras EN HORA, y con la MISMA
                    // regla que NDX (A2): de dia la mediana robusta ACUMULADA de la rueda en curso (>= 5), fuera de hora la de la ultima rueda (>= 20)
                    // = la ultima calculada en hora. Medido en Simulador37: con 'las ultimas 24 en hora' (primer intento) la razon de la noche era la
                    // del final de la rueda y la base de NDX la de la rueda entera: la alarma comparaba ventanas distintas (-2,65 toda la noche del 08-10).
                    var dia = ny.Date;
                    if (!double.IsNaN(val) && enHora)
                    {
                        if (!_ruedas.TryGetValue(dia, out var r)) { r = (new List<double>(), k); _ruedas[dia] = r; }
                        r.M.Add(val);
                        _roll.Add(val); while (_roll.Count > VENTANA) _roll.RemoveAt(0);
                        UltimaMuestraSpotUtc = tSpot; MuestrasVivas++;
                        while (_ruedas.Count > 4) _ruedas.Remove(_ruedas.Keys.First());
                    }
                    RecalcularRazon(enHora ? dia : (DateTime?)null);
                }
                return true;
            }
        }

        /// <summary>3.7.1 (A1): la razon de QQQ = mediana robusta (piso 0,02 % de la mediana) de la rueda en curso si 'diaEnHora' tiene &gt;= 5
        /// muestras; si no, la de la ultima rueda del contrato con &gt;= 20 (la de hoy al terminar la rueda: la ultima en hora); si ninguna llega
        /// a 20, la ultima con &gt;= 5. Llamar con la llave tomada.</summary>
        private void RecalcularRazon(DateTime? diaEnHora)
        {
            List<double> m = null; DateTime d0 = DateTime.MinValue;
            if (diaEnHora != null && _ruedas.TryGetValue(diaEnHora.Value, out var hoy) && hoy.M.Count >= MIN_DIA && hoy.Contrato == _rollContrato) { m = hoy.M; d0 = diaEnHora.Value; }
            if (m == null) foreach (var kv in _ruedas.Reverse()) if (kv.Value.Contrato == _rollContrato && kv.Value.M.Count >= MIN_RUEDA) { m = kv.Value.M; d0 = kv.Key; break; }
            if (m == null) foreach (var kv in _ruedas.Reverse()) if (kv.Value.Contrato == _rollContrato && kv.Value.M.Count >= MIN_DIA) { m = kv.Value.M; d0 = kv.Key; break; }
            if (m == null) { _razon = double.NaN; _nRazon = 0; _razonDia = DateTime.MinValue; return; }
            double med = Seleccion37.Mediana(m);
            var rr = Seleccion37.Robusta(m, 0.0002 * med);
            _razon = rr.V; _nRazon = rr.N; _razonDia = d0;
        }
        private DateTime _razonDia = DateTime.MinValue;

        public struct Valor
        {
            public double V; public string Modo; public string Texto; public int N; public DateTime RefDia; public double Factor;
            /// <summary>El valor SIN el decaimiento del carry (de noche: la mediana de la rueda; de dia o QQQ: igual a V). Para la alarma de la familia.</summary>
            public double VSinCarry;
        }

        /// <summary>La base (NDX) o la razon (QQQ) vigente en 'ahora' con lo observado. contratoAhora = el del grafico; vencUtc = vencimiento
        /// del contrato del grafico (para el carry de la noche; default = sin decaimiento).</summary>
        public Valor Calcular(DateTime ahoraUtc, string contratoAhora, DateTime vencUtc)
        {
            var inv = CultureInfo.InvariantCulture;
            lock (_llave)
            {
                if (PorRazon)
                {
                    if (!string.Equals(_rollContrato, contratoAhora ?? "", StringComparison.OrdinalIgnoreCase))
                        return new Valor { V = double.NaN, Modo = "sin", Texto = "razon de otro contrato (" + _rollContrato + "): se descarta (roll)", N = 0, Factor = 1 };
                    if (double.IsNaN(_razon))
                        return new Valor { V = double.NaN, Modo = "sin", Texto = "razon sincronizada: " + _nRazon + " muestras vivas (faltan " + Math.Max(0, MIN_DIA - _nRazon) + ")", N = _nRazon, Factor = 1 };
                    return new Valor { V = _razon, VSinCarry = _razon, Modo = "razon", N = _nRazon, Factor = 1,
                        Texto = "razon sincronizada " + _razon.ToString("0.0000", inv) + " (mediana robusta ACUMULADA de " + _nRazon + " muestras EN HORA 09:35-15:59 NY de la rueda " + (_razonDia == DateTime.MinValue ? "?" : _razonDia.ToString("dd-MM", inv)) + ": MNQ en cadena.ts-900 s / spot; ultima " + (UltimaMuestraSpotUtc == DateTime.MinValue ? "?" : UltimaMuestraSpotUtc.ToString("MM-dd HH:mm", inv) + "Z") + ")" };
                }
                if (_modoDia)
                    return new Valor { V = _baseDia, VSinCarry = _baseDia, Modo = "dia", N = _nDia, Factor = 1, RefDia = _rollDia,
                        Texto = "base sincronizada de dia " + _baseDia.ToString("0.00", inv) + " (mediana robusta ACUMULADA de las " + _nDia + " muestras de la rueda en curso: MNQ en cadena.ts-900 s - spot)" };
                DateTime refDia = DateTime.MinValue; (List<double> M, string Contrato) r = (null, "");
                foreach (var kv in _ruedas.Reverse()) if (kv.Value.M.Count >= MIN_RUEDA) { refDia = kv.Key; r = kv.Value; break; }
                if (r.M == null) return new Valor { V = double.NaN, Modo = "sin", Texto = "sin rueda sincronizada con >= " + MIN_RUEDA + " muestras", N = 0, Factor = 1 };
                if (!string.Equals(r.Contrato, contratoAhora ?? "", StringComparison.OrdinalIgnoreCase))
                    return new Valor { V = double.NaN, Modo = "sin", Texto = "la rueda " + refDia.ToString("dd-MM", inv) + " es de " + r.Contrato + ": se descarta (roll)", N = r.M.Count, Factor = 1, RefDia = refDia };
                var ba = Seleccion37.Robusta(r.M, 0.5);
                var tCierre = Seleccion37.AUtc(refDia.AddHours(16));
                double fac = 1.0;
                if (vencUtc != default(DateTime) && vencUtc > tCierre) fac = (vencUtc - ahoraUtc).TotalSeconds / (vencUtc - tCierre).TotalSeconds;
                double v = ba.V * fac;
                return new Valor { V = v, VSinCarry = ba.V, Modo = "noche", N = r.M.Count, Factor = fac, RefDia = refDia,
                    Texto = "base de noche " + v.ToString("0.00", inv) + " = rueda " + refDia.ToString("dd-MM", inv) + " sincronizada " + ba.V.ToString("0.00", inv) + " (" + r.M.Count + " muestras) x carry " + fac.ToString("0.0000", inv)
                            + (vencUtc == default(DateTime) ? " (sin vencimiento: sin decaimiento)" : " hasta " + vencUtc.ToString("dd-MM HH:mm", inv) + "Z") };
            }
        }

        /// <summary>Cuantas muestras tiene la ultima rueda (NDX) o la ventana (QQQ): para decidir si hace falta sembrar desde el archivo.</summary>
        public bool TieneValor(string contratoAhora)
        {
            lock (_llave)
            {
                if (PorRazon) return !double.IsNaN(_razon) && string.Equals(_rollContrato, contratoAhora ?? "", StringComparison.OrdinalIgnoreCase);   // 3.7.1: de las ruedas en hora
                return _ruedas.Any(kv => kv.Value.M.Count >= MIN_RUEDA && string.Equals(kv.Value.Contrato, contratoAhora ?? "", StringComparison.OrdinalIgnoreCase)) || _modoDia;
            }
        }

        public string Resumen()
        {
            var inv = CultureInfo.InvariantCulture;
            lock (_llave)
            {
                if (PorRazon) return "razon " + (double.IsNaN(_razon) ? "-" : _razon.ToString("0.0000", inv)) + " n=" + _nRazon + " rueda=" + (_razonDia == DateTime.MinValue ? "-" : _razonDia.ToString("MM-dd", inv)) + " ruedas=" + string.Join(",", _ruedas.Select(kv => kv.Key.ToString("MM-dd", inv) + ":" + kv.Value.M.Count)) + " contrato=" + _rollContrato + " vivas=" + MuestrasVivas;
                return "ruedas=" + string.Join(",", _ruedas.Select(kv => kv.Key.ToString("MM-dd", inv) + ":" + kv.Value.M.Count + "/" + kv.Value.Contrato)) + " dia=" + (_modoDia ? _baseDia.ToString("0.00", inv) : "-") + " vivas=" + MuestrasVivas;
            }
        }

        // ------------------------------------------------------------------ persistencia (con el contrato; sin vencimiento por tiempo)

        public string Serializar(DateTime ahoraUtc)
        {
            var inv = CultureInfo.InvariantCulture;
            string L(IEnumerable<double> xs) => "[" + string.Join(",", xs.Select(x => x.ToString("0.######", inv))) + "]";
            lock (_llave)
            {
                var sb = new StringBuilder();
                sb.Append("{\"tipo\":\"").Append(PorRazon ? "razon" : "base").Append("\",\"version\":\"3.7.1\",\"utc\":\"").Append(ahoraUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", inv))
                  .Append("\",\"ultimo_ts\":\"").Append(UltimoTsUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", inv)).Append("\",\"contrato\":\"").Append(_rollContrato)
                  .Append("\",\"roll_dia\":\"").Append(_rollDia.ToString("yyyy-MM-dd", inv)).Append("\",\"roll\":").Append(L(_roll))
                  .Append(",\"ultima_spot\":\"").Append(UltimaMuestraSpotUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", inv)).Append("\",\"ruedas\":[");
                bool pri = true;
                foreach (var kv in _ruedas)
                {
                    if (!pri) sb.Append(','); pri = false;
                    sb.Append("{\"dia\":\"").Append(kv.Key.ToString("yyyy-MM-dd", inv)).Append("\",\"contrato\":\"").Append(kv.Value.Contrato).Append("\",\"m\":").Append(L(kv.Value.M)).Append('}');
                }
                sb.Append("]}");
                return sb.ToString();
            }
        }

        public bool Cargar(string json, out string msg)
        {
            msg = "";
            var inv = CultureInfo.InvariantCulture;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                string tipo = r.TryGetProperty("tipo", out var t) ? t.GetString() : "";
                if ((tipo == "razon") != PorRazon) { msg = "tipo " + tipo + " no corresponde"; return false; }
                DateTime F(string s) => DateTime.TryParseExact(s ?? "", "yyyy-MM-dd'T'HH:mm:ss'Z'", inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : DateTime.MinValue;
                DateTime D(string s) => DateTime.TryParseExact(s ?? "", "yyyy-MM-dd", inv, DateTimeStyles.None, out var d) ? d : DateTime.MinValue;
                List<double> Arr(JsonElement e) { var l = new List<double>(); if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) if (x.ValueKind == JsonValueKind.Number) l.Add(x.GetDouble()); return l; }
                lock (_llave)
                {
                    UltimoTsUtc = F(r.TryGetProperty("ultimo_ts", out var u) ? u.GetString() : null);
                    _rollContrato = r.TryGetProperty("contrato", out var c) ? c.GetString() ?? "" : "";
                    _rollDia = D(r.TryGetProperty("roll_dia", out var rd) ? rd.GetString() : null);
                    UltimaMuestraSpotUtc = F(r.TryGetProperty("ultima_spot", out var us) ? us.GetString() : null);
                    _roll.Clear(); if (r.TryGetProperty("roll", out var ro)) _roll.AddRange(Arr(ro));
                    _ruedas.Clear();
                    if (r.TryGetProperty("ruedas", out var rs) && rs.ValueKind == JsonValueKind.Array)
                        foreach (var x in rs.EnumerateArray())
                        {
                            var dia = D(x.TryGetProperty("dia", out var dd) ? dd.GetString() : null);
                            if (dia == DateTime.MinValue) continue;
                            _ruedas[dia] = (Arr(x.TryGetProperty("m", out var mm) ? mm : default), x.TryGetProperty("contrato", out var cc) ? cc.GetString() ?? "" : "");
                        }
                    if (PorRazon) RecalcularRazon(null);   // 3.7.1 (A1): desde las ruedas guardadas (en hora)
                    _modoDia = false; _baseDia = double.NaN;
                    MuestrasVivas = _ruedas.Values.Sum(v => v.M.Count) + (PorRazon ? _roll.Count : 0);
                }
                msg = Resumen();
                return true;
            }
            catch (Exception e) { msg = e.GetType().Name + ": " + e.Message; return false; }
        }
    }

    /// <summary>
    /// C1 (3.7.0): EL OI DE NQ TIENE FECHA. Rithmic publica el OI nuevo de noche: medido (cruce_1b, rev_01, backtest_familia.md) entre
    /// las 21:24 y las 21:52 NY en 11 noches; hasta ese salto el OI de las series de NQ es el de DOS sesiones atras (anteayer). Salto =
    /// dos fotos seguidas con &gt;= 20 contratos en comun (OI &gt; 0 en alguna) y &gt;= 50 % de ellos con OI distinto, buscado entre las 18:00 y
    /// las 23:30 NY de la sesion (backtest_familia.oi_nq_viejo_hasta). Sin ATAS.
    /// Reglas sin salto visto: sesion del lunes = OI ya actualizado (el del viernes); primera foto de la noche a las 21:55 NY o despues =
    /// OI supuesto actualizado; antes de las 23:30 NY = 'OI de anteayer, esperando el salto'; despues = supuesto actualizado.
    /// Los feriados de CME no estan (SUPUESTO: el dia posterior a un feriado se trata como uno normal).
    /// </summary>
    public sealed class SaltoOi37
    {
        public static readonly TimeSpan Inicio = new TimeSpan(18, 0, 0), FinBusqueda = new TimeSpan(23, 30, 0), PrimeraTarde = new TimeSpan(21, 55, 0);
        private readonly object _llave = new object();
        private Dictionary<string, double> _prev;
        public DateTime Sesion { get; private set; } = DateTime.MinValue;
        public DateTime? SaltoUtc { get; private set; }
        public DateTime? PrimeraUtc { get; private set; }
        public DateTime UltimaUtc { get; private set; } = DateTime.MinValue;
        public int UltimoComunes { get; private set; }
        public int UltimoCambiados { get; private set; }

        /// <summary>Sesion de CME de un instante: desde las 18:00 NY es la del dia siguiente.</summary>
        public static DateTime SesionDe(DateTime utc) { var ny = Seleccion37.ANy(utc); return ny.TimeOfDay >= Inicio ? ny.Date.AddDays(1) : ny.Date; }

        /// <summary>3.7.2: el rotulo de la fecha del OI de NQ cuando es 'de ayer' (despues del salto): el cierre de la sesion habil anterior a la
        /// de ahora ('OI 07-10' durante la sesion del 08-10; el lunes, el viernes). Feriados de CME: no estan (SUPUESTO, como C1).</summary>
        public static string RotuloFechaOi(DateTime ahoraUtc)
        {
            var d = SesionDe(ahoraUtc).AddDays(-1);
            while (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(-1);
            return "OI " + d.ToString("dd-MM", CultureInfo.InvariantCulture);
        }
        private static bool EnVentana(DateTime utc) { var tod = Seleccion37.ANy(utc).TimeOfDay; return tod >= Inicio && tod <= FinBusqueda; }

        /// <summary>Una foto del libro (clave del contrato -&gt; OI), en orden de tiempo (las viejas se ignoran). true si en ESTA foto se midio el salto.</summary>
        public bool Observar(DateTime tsUtc, IDictionary<string, double> oi)
        {
            lock (_llave)
            {
                if (oi == null || tsUtc <= UltimaUtc) return false;
                UltimaUtc = tsUtc;
                var ses = SesionDe(tsUtc);
                if (ses != Sesion) { Sesion = ses; SaltoUtc = null; PrimeraUtc = null; _prev = null; }
                if (SaltoUtc != null || !EnVentana(tsUtc)) return false;
                var cur = new Dictionary<string, double>(oi);
                if (PrimeraUtc == null) { PrimeraUtc = tsUtc; _prev = cur; return false; }
                int com = 0, cambian = 0;
                foreach (var kv in cur)
                {
                    if (!_prev.TryGetValue(kv.Key, out double p)) continue;
                    if (kv.Value <= 0 && p <= 0) continue;
                    com++;
                    if (kv.Value != p) cambian++;
                }
                UltimoComunes = com; UltimoCambiados = cambian;
                _prev = cur;
                if (com >= 20 && cambian >= 0.5 * com) { SaltoUtc = tsUtc; _prev = null; return true; }
                return false;
            }
        }

        /// <summary>El OI de NQ en 'ahora' es el de ANTEAYER? (motivo para el log y el rotulo).</summary>
        public bool OiDeAnteayer(DateTime ahoraUtc, out string motivo)
        {
            var inv = CultureInfo.InvariantCulture;
            lock (_llave)
            {
                var ses = SesionDe(ahoraUtc);
                var tod = Seleccion37.ANy(ahoraUtc).TimeOfDay;
                bool ventana = tod >= Inicio && tod < FinBusqueda;
                if (ses.DayOfWeek == DayOfWeek.Monday) { motivo = "sesion del lunes: OI del viernes, ya actualizado"; return false; }
                if (ses != Sesion || PrimeraUtc == null) { motivo = ventana ? "OI de anteayer: todavia sin fotos del libro esta noche" : "sin fotos de la noche: OI supuesto actualizado"; return ventana; }
                if (SaltoUtc != null)
                {
                    motivo = "salto de OI de Rithmic medido a las " + Seleccion37.ANy(SaltoUtc.Value).ToString("HH:mm:ss", inv) + " NY";
                    return ahoraUtc < SaltoUtc.Value;
                }
                if (Seleccion37.ANy(PrimeraUtc.Value).TimeOfDay >= PrimeraTarde) { motivo = "primera foto " + Seleccion37.ANy(PrimeraUtc.Value).ToString("HH:mm", inv) + " NY (despues del salto habitual): OI supuesto actualizado"; return false; }
                if (ventana) { motivo = "OI de anteayer: esperando el salto de Rithmic (medido 21:24-21:52 NY en 11 noches)"; return true; }
                motivo = "sin salto visible hasta las 23:30 NY: OI supuesto actualizado";
                return false;
            }
        }

        /// <summary>Toma el salto que encontro otro detector (el del rebobinado de viva3) para la MISMA sesion, y la primera foto mas temprana.</summary>
        public bool AdoptarDe(SaltoOi37 otro)
        {
            if (otro == null || ReferenceEquals(otro, this)) return false;
            DateTime ses; DateTime? salto, primera;
            lock (otro._llave) { ses = otro.Sesion; salto = otro.SaltoUtc; primera = otro.PrimeraUtc; }
            lock (_llave)
            {
                if (ses == DateTime.MinValue) return false;
                if (Sesion != DateTime.MinValue && Sesion != ses) return false;
                bool cambio = false;
                if (Sesion == DateTime.MinValue) { Sesion = ses; cambio = true; }
                if (primera != null && (PrimeraUtc == null || primera < PrimeraUtc)) { PrimeraUtc = primera; cambio = true; }
                if (salto != null && SaltoUtc == null) { SaltoUtc = salto; _prev = null; cambio = true; }
                return cambio;
            }
        }

        public string Serializar()
        {
            var inv = CultureInfo.InvariantCulture;
            lock (_llave)
                return "{\"sesion\":\"" + Sesion.ToString("yyyy-MM-dd", inv) + "\",\"salto\":" + (SaltoUtc == null ? "null" : "\"" + SaltoUtc.Value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", inv) + "\"")
                     + ",\"primera\":" + (PrimeraUtc == null ? "null" : "\"" + PrimeraUtc.Value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", inv) + "\"") + "}";
        }

        /// <summary>Carga lo persistido solo si es de la sesion de 'ahora'.</summary>
        public bool Cargar(string json, DateTime ahoraUtc)
        {
            var inv = CultureInfo.InvariantCulture;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                if (!DateTime.TryParseExact(r.GetProperty("sesion").GetString() ?? "", "yyyy-MM-dd", inv, DateTimeStyles.None, out var ses) || ses != SesionDe(ahoraUtc)) return false;
                DateTime? T(string n) => r.TryGetProperty(n, out var e) && e.ValueKind == JsonValueKind.String && DateTime.TryParseExact(e.GetString(), "yyyy-MM-dd'T'HH:mm:ss'Z'", inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : (DateTime?)null;
                lock (_llave)
                {
                    if (Sesion != DateTime.MinValue && Sesion != ses) return false;
                    Sesion = ses;
                    var s = T("salto"); var p = T("primera");
                    if (s != null && SaltoUtc == null) SaltoUtc = s;
                    if (p != null && (PrimeraUtc == null || p < PrimeraUtc)) PrimeraUtc = p;
                }
                return true;
            }
            catch { return false; }
        }
    }

    /// <summary>
    /// C1 para NDX/QQQ (3.7.0): la FECHA del interes abierto de una cadena de CBOE, para el rotulo. El OCC lo publica de madrugada:
    /// medido (cruce_9b_hora_oi_cboe.txt) NDX 02:31-02:34 NY en 3 dias, QQQ 03:31 y 05:12 NY. Si se ve el cambio (la suma de OI de la
    /// cadena cambia mas de 5 % entre dos fotos antes de las 09:30 NY) la fecha es el dia habil anterior al del cambio (MEDIDO); si no,
    /// por reloj con la hora tipica (SUPUESTO, se dice).
    /// </summary>
    public sealed class FechaOiCboe37
    {
        public readonly TimeSpan HoraTipica;
        private double _oiPrev = double.NaN;
        public DateTime? CambioUtc { get; private set; }
        public FechaOiCboe37(TimeSpan horaTipicaNy) { HoraTipica = horaTipicaNy; }

        public void Observar(DateTime generadoUtc, double oiTotal)
        {
            if (oiTotal <= 0 || double.IsNaN(oiTotal)) return;
            if (!double.IsNaN(_oiPrev) && _oiPrev > 0 && Math.Abs(oiTotal - _oiPrev) / _oiPrev > 0.05)
            {
                var ny = Seleccion37.ANy(generadoUtc);
                if (ny.TimeOfDay < new TimeSpan(9, 30, 0)) CambioUtc = generadoUtc;
            }
            _oiPrev = oiTotal;
        }

        /// <summary>(fecha del OI, medido o por reloj).</summary>
        public (DateTime Fecha, bool Medido) FechaOi(DateTime ahoraUtc)
        {
            var ny = Seleccion37.ANy(ahoraUtc);
            if (CambioUtc != null && (ahoraUtc - CambioUtc.Value).TotalHours < 30) return (Seleccion37.HabilAnterior(Seleccion37.ANy(CambioUtc.Value).Date), true);
            bool finde = ny.DayOfWeek == DayOfWeek.Saturday || ny.DayOfWeek == DayOfWeek.Sunday;
            var d = ny.Date;
            return (finde || ny.TimeOfDay >= HoraTipica ? Seleccion37.HabilAnterior(d) : Seleccion37.HabilAnterior(Seleccion37.HabilAnterior(d)), false);
        }

        public string Rotulo(DateTime ahoraUtc)
        {
            var f = FechaOi(ahoraUtc);
            return "OI " + f.Fecha.ToString("dd-MM", CultureInfo.InvariantCulture) + (f.Medido ? "" : "?");
        }
    }

    /// <summary>
    /// C5, la ALARMA de la familia (3.7.0): la base equivalente de NDX que sale de QQQ, base_qqq = NDX x (razon_QQQ / R - 1), con R = NDX/QQQ
    /// pareados en la MISMA bajada (generado a &lt;= 45 s, los dos spots vivos, 09:35-15:59 NY), mediana robusta de la ultima rueda con
    /// &gt;= 10 pares (qqq_base_viva.py: en 2026-10-07 dio 245,23 contra la base viva 245,10). Se compara contra la base de NDX SIN el
    /// decaimiento de la noche (Valor.VSinCarry: la mediana de la rueda), porque la razon de QQQ tampoco decae: medido en Simulador37 la
    /// noche del 08-10, la razon quedo en 41,4339-41,4343 de 20:51Z a 08:54Z, y contra la base decaida la diferencia crecia de -1,4 a
    /// -3,3 pts solo por el reloj. Si difieren mas de 2 pts: alarma en el log y en el rotulo. Sin ATAS.
    /// </summary>
    public sealed class ParesFamilia37
    {
        private readonly object _llave = new object();
        private readonly List<(DateTime Gen, DateTime TSpot, double Spot, bool Vivo)> _ndx = new List<(DateTime, DateTime, double, bool)>(), _qqq = new List<(DateTime, DateTime, double, bool)>();
        private readonly SortedDictionary<DateTime, List<double>> _r = new SortedDictionary<DateTime, List<double>>();
        private readonly HashSet<(DateTime, DateTime)> _usados = new HashSet<(DateTime, DateTime)>();
        public const double TOLERANCIA_S = 45;
        public const int MIN_PARES = 10;

        public void Ndx(DateTime gen, DateTime tSpot, double spot, bool vivo) => Agregar(true, gen, tSpot, spot, vivo);
        public void Qqq(DateTime gen, DateTime tSpot, double spot, bool vivo) => Agregar(false, gen, tSpot, spot, vivo);

        private void Agregar(bool esNdx, DateTime gen, DateTime tSpot, double spot, bool vivo)
        {
            if (spot <= 0) return;
            lock (_llave)
            {
                var mia = esNdx ? _ndx : _qqq; var otra = esNdx ? _qqq : _ndx;
                mia.Add((gen, tSpot, spot, vivo));
                mia.RemoveAll(x => (gen - x.Gen).TotalMinutes > 30);
                if (!vivo) return;
                (DateTime Gen, DateTime TSpot, double Spot, bool Vivo)? mejor = null;
                foreach (var o in otra) if (o.Vivo && Math.Abs((o.Gen - gen).TotalSeconds) <= TOLERANCIA_S && (mejor == null || Math.Abs((o.Gen - gen).TotalSeconds) < Math.Abs((mejor.Value.Gen - gen).TotalSeconds))) mejor = o;
                if (mejor == null) return;
                var n = esNdx ? (gen, tSpot, spot) : (mejor.Value.Gen, mejor.Value.TSpot, mejor.Value.Spot);
                var q = esNdx ? (mejor.Value.Gen, mejor.Value.TSpot, mejor.Value.Spot) : (gen, tSpot, spot);
                if (!_usados.Add((n.Item1, q.Item1))) return;
                var ny = Seleccion37.ANy(n.Item2);
                var hm = ny.TimeOfDay;
                if (ny.DayOfWeek == DayOfWeek.Saturday || ny.DayOfWeek == DayOfWeek.Sunday || hm < new TimeSpan(9, 35, 0) || hm >= new TimeSpan(16, 0, 0)) return;
                if (!_r.TryGetValue(ny.Date, out var l)) { l = new List<double>(); _r[ny.Date] = l; }
                l.Add(n.Item3 / q.Item3);
                while (_r.Count > 4) _r.Remove(_r.Keys.First());
                if (_usados.Count > 5000) _usados.Clear();
            }
        }

        /// <summary>(R = NDX/QQQ pareado, pares, dia) de la ultima rueda con &gt;= 10 pares; NaN si no hay.</summary>
        public (double R, int N, DateTime Dia) RPar()
        {
            lock (_llave)
            {
                foreach (var kv in _r.Reverse())
                    if (kv.Value.Count >= MIN_PARES)
                    {
                        double med = Seleccion37.Mediana(kv.Value);
                        var rb = Seleccion37.Robusta(kv.Value, 0.0002 * med);
                        return (rb.V, kv.Value.Count, kv.Key);
                    }
                return (double.NaN, 0, DateTime.MinValue);
            }
        }

        /// <summary>Base equivalente de NDX desde QQQ: NDX x (razon / R - 1). Sin carry: se compara contra Valor.VSinCarry de NDX.</summary>
        public static double BaseEquivalente(double ndxSpot, double razon, double rPar)
        {
            if (ndxSpot <= 0 || razon <= 0 || rPar <= 0 || double.IsNaN(razon) || double.IsNaN(rPar)) return double.NaN;
            return ndxSpot * (razon / rPar - 1.0);
        }

        public string Serializar()
        {
            var inv = CultureInfo.InvariantCulture;
            lock (_llave)
                return "{\"pares\":[" + string.Join(",", _r.Select(kv => "{\"dia\":\"" + kv.Key.ToString("yyyy-MM-dd", inv) + "\",\"r\":[" + string.Join(",", kv.Value.Select(x => x.ToString("0.######", inv))) + "]}")) + "]}";
        }

        public bool Cargar(string json)
        {
            var inv = CultureInfo.InvariantCulture;
            try
            {
                using var doc = JsonDocument.Parse(json);
                lock (_llave)
                {
                    foreach (var x in doc.RootElement.GetProperty("pares").EnumerateArray())
                    {
                        if (!DateTime.TryParseExact(x.GetProperty("dia").GetString() ?? "", "yyyy-MM-dd", inv, DateTimeStyles.None, out var d)) continue;
                        if (_r.ContainsKey(d)) continue;
                        var l = new List<double>(); foreach (var v in x.GetProperty("r").EnumerateArray()) l.Add(v.GetDouble());
                        _r[d] = l;
                    }
                    while (_r.Count > 4) _r.Remove(_r.Keys.First());
                }
                return true;
            }
            catch { return false; }
        }
    }

    /// <summary>
    /// 3.7.1 (A1): la alarma de la familia SOSTENIDA. La diferencia base NDX (sin carry) - base equivalente de QQQ tiene que pasar de 2 pts
    /// durante 10 minutos SEGUIDOS para que la alarma exista; vuelve a 0 apenas la diferencia baja de 2 o falta el dato. Ya no va en los
    /// rotulos de las capas ('·⚠base' de la 3.7.0, que se prendia 731 min el 10-07 por las muestras de QQQ fuera de hora): solo en el log
    /// y en el recuadro del libro. Sin ATAS (la usa Simulador37).
    /// </summary>
    public sealed class AlarmaFamilia37
    {
        public const double UMBRAL_PTS = 2.0, MINUTOS = 10.0;
        public DateTime DesdeUtc { get; private set; } = DateTime.MinValue;
        public double UltimaDif { get; private set; } = double.NaN;

        /// <summary>Una evaluacion (una por minuto o por cadena). Devuelve el texto de la alarma ('⚠base +2,4 hace 12 min') o "".</summary>
        public string Evaluar(DateTime ahoraUtc, double dif)
        {
            UltimaDif = dif;
            if (double.IsNaN(dif) || Math.Abs(dif) <= UMBRAL_PTS) { DesdeUtc = DateTime.MinValue; return ""; }
            if (DesdeUtc == DateTime.MinValue || ahoraUtc < DesdeUtc) DesdeUtc = ahoraUtc;
            double min = (ahoraUtc - DesdeUtc).TotalMinutes;
            if (min < MINUTOS) return "";
            return "⚠base " + dif.ToString("+0.0;-0.0", CultureInfo.GetCultureInfo("es-AR")) + " hace " + ((int)min).ToString(CultureInfo.InvariantCulture) + " min";
        }
    }

    /// <summary>
    /// El archivo de cboe-local (cadena-&lt;NQ|QQQ&gt;-&lt;dia UTC&gt;.jsonl.gz, lo escribe cboe_local.py): solo las CABECERAS de cada linea
    /// (generado, cadena_ts, spot = cadena.spot_idx como el vivo; si no esta, la cabecera 'spot'), leyendo los primeros 400 caracteres sin parsear las filas. Lo usan la siembra del indicador y el
    /// simulador: la misma lectura en los dos. Solo lectura (FileShare.ReadWrite: el bajador puede estar escribiendo).
    /// </summary>
    public static class ArchivoCboe37
    {
        private static readonly Regex RxGen = new Regex(@"""generado""\s*:\s*""([^""]+)""", RegexOptions.Compiled);
        private static readonly Regex RxTs = new Regex(@"""cadena_ts""\s*:\s*""([^""]+)""", RegexOptions.Compiled);
        private static readonly Regex RxSpot = new Regex(@"""spot""\s*:\s*([-0-9.eE]+)", RegexOptions.Compiled);
        // 3.7.0 (paridad 08-10, resultados/paridad_3_7.md): el spot de la muestra es cadena.spot_idx, como el vivo (CargarCapa) y
        // backtest_familia (libros.fotos_cboe); la cabecera 'spot' de NDX trae 4 decimales y spot_idx 2, y con la cabecera la siembra
        // y el simulador daban otra base que el vivo (hasta 0,13 pts el 07-10). Sin spot_idx en los primeros 400 caracteres: la cabecera.
        private static readonly Regex RxSpotIdx = new Regex(@"""spot_idx""\s*:\s*([-0-9.eE]+)", RegexOptions.Compiled);

        public static List<(DateTime Gen, DateTime Ts, double Spot)> Cabeceras(string carpeta, string archivo, IEnumerable<DateTime> diasUtc)
        {
            var inv = CultureInfo.InvariantCulture;
            var r = new List<(DateTime, DateTime, double)>();
            foreach (var d in diasUtc)
            {
                var ruta = Path.Combine(carpeta, "cadena-" + archivo + "-" + d.ToString("yyyy-MM-dd", inv) + ".jsonl.gz");
                if (!File.Exists(ruta)) continue;
                try
                {
                    using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var gz = new GZipStream(fs, CompressionMode.Decompress);
                    var buf = new byte[1 << 16]; var cab = new StringBuilder(512); bool enCab = true; int n;
                    void Cerrar()
                    {
                        string s = cab.ToString(); cab.Clear();
                        var mg = RxGen.Match(s); var mt = RxTs.Match(s); var ms = RxSpotIdx.Match(s);
                        if (!ms.Success) ms = RxSpot.Match(s);
                        if (!mg.Success || !mt.Success || !ms.Success) return;
                        if (!DateTimeOffset.TryParse(mg.Groups[1].Value, inv, DateTimeStyles.AssumeUniversal, out var g)) return;
                        if (!DateTime.TryParseExact(mt.Groups[1].Value, "yyyy-MM-dd HH:mm:ss", inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)) return;
                        if (!double.TryParse(ms.Groups[1].Value, NumberStyles.Float, inv, out var sp) || sp <= 0) return;
                        r.Add((g.UtcDateTime, DateTime.SpecifyKind(t, DateTimeKind.Utc), sp));
                    }
                    try
                    {
                        while ((n = gz.Read(buf, 0, buf.Length)) > 0)
                            for (int i = 0; i < n; i++)
                            {
                                byte b = buf[i];
                                if (b == 10) { Cerrar(); enCab = true; continue; }   // '\n'
                                if (enCab) { cab.Append((char)b); if (cab.Length >= 400) enCab = false; }
                            }
                    }
                    catch (InvalidDataException) { }   // el bajador puede estar escribiendo el final: lo leido hasta ahi vale
                    if (cab.Length > 0) Cerrar();
                }
                catch { }
            }
            return r;
        }
    }
}
