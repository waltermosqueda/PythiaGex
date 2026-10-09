// TqqqNoche.cs — PythiaGex 4.1, modulo TQQQ (B4), 08-10-2026. FUNCION NUEVA, SIN VALIDAR (1 noche medida: 07-10 -> 08-10).
// Reanclaje de noche al cierre de la rueda d (especificacion TQQQ §6, DISENO_4_1 anexo B.5):
//   TQQQ se rebalancea al cierre: desde las 16:00 NY rige TQQQ(t) = P0'(1 + 3(NDX/N0' - 1)). Despejando, NQ(K) = s' K + c' con
//     s' = N0' / (3 P0')      c' = NQ_1600 - N0'/3      (c' no depende de P0'; NQ(K) si: 1 centavo de P0' corre todo ~1,24 pts)
//   NQ_1600 = MNQ en el cierre (ultimo tick <= 16:00:00 NY; si no, la vela del grafico que termina ahi); N0' = spot de _NDX congelado
//   (primera foto con sello >= cierre + 15 min cuyo spot se repite en la siguiente); P0' oficial = prev_day_close de TQQQ una vez que CBOE
//   lo cambio; P0' implicito = (NQ_1600 - c_cierre) / s_d (el cierre de TQQQ que se deduce de la conversion de la rueda).
//   Modo B (P0' implicito, banda +-8) desde que hay N0' hasta que aparece el oficial; modo C (oficial, banda +-5) hasta que la rueda
//   siguiente junta 5 muestras. Cada noche se guarda el error de B y de C contra el primer c medido de la rueda siguiente (para juntar muestra).
// Este archivo: el registro por fecha NY y contrato (tqqq-cierres.json). La cuenta vive en CalculoTqqq.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PythiaGexCuatro.Familia
{
    public sealed class TqqqAnclaDia
    {
        public string Fecha = "", Contrato = "";
        public double S = double.NaN; public string SFuente = "";            // s de la rueda d (N0/(3 P0) con los cierres de d-1)
        public double P0 = double.NaN, N0 = double.NaN;                        // los cierres de d-1 que dieron s
        public double CCierre = double.NaN; public DateTime CCierreUtc; public int CCierreN;   // c de la rueda d al cierre (congelado)
        public double Nq1600 = double.NaN; public string Nq1600Fuente = "";
        public double N0p = double.NaN; public DateTime N0pUtc; public string N0pFuente = "";  // NDX al cierre de d (N0')
        public double N0pPdc = double.NaN; public DateTime N0pPdcUtc;          // prev_day_close de NDX ya cambiado (informativo)
        public double P0pOficial = double.NaN; public DateTime P0pOficialUtc; public string P0pFuente = "";
        public double P0pClose = double.NaN; public DateTime P0pCloseUtc;      // data.close de TQQQ despues del cierre (SIN VERIFICAR que sea el oficial)
        public string FechaSig = ""; public double SSig = double.NaN, CSig = double.NaN; public DateTime CSigUtc;
        public double KErr = double.NaN, ErrB = double.NaN, ErrC = double.NaN;

        public double P0pImpl => (Nq1600 - CCierre) / S;
        public double CPrima => Nq1600 - N0p / 3.0;
        public double SPrima(double p0p) => N0p / (3.0 * p0p);

        public string Clave => Clv(Fecha, Contrato);
        public static string Clv(string fecha, string contrato) => fecha + "|" + (contrato ?? "");

        internal string Huella() => string.Join(";", new[] { N(S), N(CCierre), N(Nq1600), N(N0p), N(P0pOficial), N(P0pClose), N(CSig), N(ErrB), N(ErrC), N(N0pPdc), N(KErr) });

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static string N(double v) => double.IsNaN(v) ? "null" : v.ToString("R", Inv);
        private static string T(DateTime t) => t == default ? "null" : "\"" + TqqqArchivo.Iso(t) + "\"";
        private static string Q(string s) => JsonSerializer.Serialize(s ?? "");

        public string Json()
        {
            var sb = new StringBuilder(800);
            sb.Append("{\"fecha\":").Append(Q(Fecha)).Append(",\"contrato\":").Append(Q(Contrato));
            sb.Append(",\"s\":").Append(N(S)).Append(",\"s_fuente\":").Append(Q(SFuente)).Append(",\"p0\":").Append(N(P0)).Append(",\"n0\":").Append(N(N0));
            sb.Append(",\"c_cierre\":").Append(N(CCierre)).Append(",\"c_cierre_utc\":").Append(T(CCierreUtc)).Append(",\"c_cierre_n\":").Append(CCierreN);
            sb.Append(",\"nq_1600\":").Append(N(Nq1600)).Append(",\"nq_1600_fuente\":").Append(Q(Nq1600Fuente));
            sb.Append(",\"n0p\":").Append(N(N0p)).Append(",\"n0p_utc\":").Append(T(N0pUtc)).Append(",\"n0p_fuente\":").Append(Q(N0pFuente));
            sb.Append(",\"n0p_pdc\":").Append(N(N0pPdc)).Append(",\"n0p_pdc_utc\":").Append(T(N0pPdcUtc));
            sb.Append(",\"p0p_oficial\":").Append(N(P0pOficial)).Append(",\"p0p_oficial_utc\":").Append(T(P0pOficialUtc)).Append(",\"p0p_fuente\":").Append(Q(P0pFuente));
            sb.Append(",\"p0p_close\":").Append(N(P0pClose)).Append(",\"p0p_close_utc\":").Append(T(P0pCloseUtc));
            sb.Append(",\"p0p_impl\":").Append(N(P0pImpl)).Append(",\"c_prima\":").Append(N(CPrima));
            sb.Append(",\"ancla_b\":{\"s\":").Append(N(SPrima(P0pImpl))).Append(",\"c\":").Append(N(CPrima)).Append('}');
            sb.Append(",\"ancla_c\":{\"s\":").Append(N(SPrima(P0pOficial))).Append(",\"c\":").Append(N(CPrima)).Append('}');
            sb.Append(",\"fecha_sig\":").Append(Q(FechaSig)).Append(",\"s_sig\":").Append(N(SSig)).Append(",\"c_sig\":").Append(N(CSig)).Append(",\"c_sig_utc\":").Append(T(CSigUtc));
            sb.Append(",\"k_err\":").Append(N(KErr)).Append(",\"err_b\":").Append(N(ErrB)).Append(",\"err_c\":").Append(N(ErrC));
            sb.Append('}');
            return sb.ToString();
        }

        public static TqqqAnclaDia DeJson(JsonElement e)
        {
            var a = new TqqqAnclaDia
            {
                Fecha = S_(e, "fecha"), Contrato = S_(e, "contrato"), S = D(e, "s"), SFuente = S_(e, "s_fuente"), P0 = D(e, "p0"), N0 = D(e, "n0"),
                CCierre = D(e, "c_cierre"), CCierreUtc = U(e, "c_cierre_utc"), Nq1600 = D(e, "nq_1600"), Nq1600Fuente = S_(e, "nq_1600_fuente"),
                N0p = D(e, "n0p"), N0pUtc = U(e, "n0p_utc"), N0pFuente = S_(e, "n0p_fuente"), N0pPdc = D(e, "n0p_pdc"), N0pPdcUtc = U(e, "n0p_pdc_utc"),
                P0pOficial = D(e, "p0p_oficial"), P0pOficialUtc = U(e, "p0p_oficial_utc"), P0pFuente = S_(e, "p0p_fuente"),
                P0pClose = D(e, "p0p_close"), P0pCloseUtc = U(e, "p0p_close_utc"),
                FechaSig = S_(e, "fecha_sig"), SSig = D(e, "s_sig"), CSig = D(e, "c_sig"), CSigUtc = U(e, "c_sig_utc"),
                KErr = D(e, "k_err"), ErrB = D(e, "err_b"), ErrC = D(e, "err_c"),
            };
            if (e.TryGetProperty("c_cierre_n", out var n) && n.ValueKind == JsonValueKind.Number) a.CCierreN = n.GetInt32();
            return a;
        }

        private static double D(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN;
        private static string S_(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
        private static DateTime U(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? TqqqArchivo.DeIso(v.GetString()) : default;
    }

    /// <summary>tqqq-cierres.json: fecha|contrato -> TqqqAnclaDia (las ultimas 40). Lo usa solo el hilo de la Familia.</summary>
    public sealed class TqqqAnclas
    {
        private readonly SortedDictionary<string, TqqqAnclaDia> _d = new SortedDictionary<string, TqqqAnclaDia>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _huella = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly string _ruta;
        public TqqqAnclas(string ruta) { _ruta = ruta; }

        public void Cargar()
        {
            try
            {
                if (string.IsNullOrEmpty(_ruta) || !System.IO.File.Exists(_ruta)) return;
                using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(_ruta));
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    if (p.Value.ValueKind != JsonValueKind.Object) continue;
                    var a = TqqqAnclaDia.DeJson(p.Value);
                    _d[a.Clave] = a; _huella[a.Clave] = a.Huella();
                }
            }
            catch { }
        }

        public TqqqAnclaDia Ver(string fecha, string contrato) => _d.TryGetValue(TqqqAnclaDia.Clv(fecha, contrato), out var a) ? a : null;

        public TqqqAnclaDia Tomar(string fecha, string contrato)
        {
            var k = TqqqAnclaDia.Clv(fecha, contrato);
            if (!_d.TryGetValue(k, out var a)) { a = new TqqqAnclaDia { Fecha = fecha, Contrato = contrato ?? "" }; _d[k] = a; }
            return a;
        }

        /// <summary>Escribe el archivo si alguna ancla cambio (atomico). Devuelve las claves que cambiaron.</summary>
        public List<string> Guardar()
        {
            var cambiadas = new List<string>();
            foreach (var kv in _d)
            {
                var h = kv.Value.Huella();
                if (!_huella.TryGetValue(kv.Key, out var v) || v != h) { cambiadas.Add(kv.Key); _huella[kv.Key] = h; }
            }
            if (cambiadas.Count == 0) return cambiadas;
            while (_d.Count > 40) { var e = _d.GetEnumerator(); e.MoveNext(); _huella.Remove(e.Current.Key); _d.Remove(e.Current.Key); }
            if (string.IsNullOrEmpty(_ruta)) return cambiadas;
            var sb = new StringBuilder(8192);
            sb.Append("{\n");
            bool primero = true;
            foreach (var kv in _d)
            {
                if (!primero) sb.Append(",\n"); primero = false;
                sb.Append(JsonSerializer.Serialize(kv.Key)).Append(':').Append(kv.Value.Json());
            }
            sb.Append("\n}\n");
            TqqqArchivo.EscribirAtomico(_ruta, sb.ToString());
            return cambiadas;
        }
    }
}
