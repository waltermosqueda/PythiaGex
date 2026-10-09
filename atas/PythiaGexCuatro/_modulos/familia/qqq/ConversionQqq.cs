// ConversionQqq.cs — PythiaGex 4.1, modulo Familia QQQ (B3c). 08-10-2026.
// Port EXACTO de laboratorio/tres/auditoria_0810/backtest_familia.py:
//   * conversion(), rama QQQ: C8 razon SINCRONIZADA (muestra = precio del MNQ en cadena.ts - 900 s / spot, solo con el spot VIVO =
//     distinto del de alguna foto de los 600 s previos por 'generado'; cualquier hora; ultimas 24 validas; mediana robusta 3 MAD con piso
//     0,0002 x mediana; >= 5 o NaN; se reinicia si cambia el contrato; una foto sin muestra hereda la razon de las ultimas 24, sin tope de tiempo);
//   * conversion(), bloque oi_fresco ("OI de 2 sesiones": solo MARCA, no apaga nada);
//   * conversion(), 'congelada' (ultimo_trade NY >= 15:59);
//   * vigencia_cboe() = C9 (25 min desde 'generado'; congelada: hasta las 13:30 UTC siguientes).
// OJO: NO es la regla de atas/_propuesta_3_7/.../Conversion37.cs 3.7.1 (A1: solo muestras EN HORA + mediana ACUMULADA de la rueda): esa
// cambia los numeros y la referencia de paridad (preview_niveles -> backtest_familia) usa la C8 original. Se porto la C8 original.
// Modo Corregida (default del indicador): las horas fijas en UTC del Python (13:30, 22:00, 03:00) pasan a hora de Nueva York (09:30, 18:00,
// 23:00), para que no se corran el 01-11. En horario de verano da IDENTICO. ParidadPython = las horas en UTC como el Python.
// Sin ATAS, sin E/S: lo compila el arnes atas/_test_cuatro_paridad/qqq.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PythiaGexCuatro.Familia.Qqq
{
    /// <summary>Atajos de hora del modulo QQQ sobre TiempoFam (comun de B3a).</summary>
    internal static class HoraQqq
    {
        public static DateTime ANy(DateTime utc) => TiempoFam.ANy(utc);
        public static DateTime AUtc(DateTime ny) => TiempoFam.NyAUtc(ny);
        /// <summary>Minuto del dia "HH:MM" como entero (el Python compara textos "HH:MM": se truncan los segundos).</summary>
        public static int Hm(DateTime d) => d.Hour * 60 + d.Minute;
        public static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);
        /// <summary>UTC truncado al segundo (los archivos de CBOE y la referencia guardan 'generado' en segundos; B2 puede traer UtcNow con fraccion:
        /// sin truncar, la vigencia de 1500 s y el tramo de 900 s de la cache del zero podian caer distinto en vivo que al reiniciar desde el archivo).</summary>
        public static DateTime Seg(DateTime d) => new DateTime(d.Ticks - d.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }

    /// <summary>Opciones del modulo QQQ. Contrato: de una funcion (ParidadPython = tabla fija de velas.contrato; Corregida = el del grafico).</summary>
    public sealed class OpcionesQqq
    {
        /// <summary>true (default del indicador): horas de C9 / oi_fresco / ventana de sesion en hora de NY. false = como el Python (UTC fijo).</summary>
        public bool Corregida = true;
        /// <summary>Contrato del grafico vigente en un instante UTC. Default = tabla del Python (U6 hasta la sesion del 14-09, Z6 hasta la del 17-12, H7).</summary>
        public Func<DateTime, string> ContratoDe = ContratoTablaPython;

        /// <summary>velas.contrato(sesion_de(t)) del laboratorio: tabla fija (paridad). En el indicador se reemplaza por el del grafico.</summary>
        public static string ContratoTablaPython(DateTime tUtc)
        {
            var ses = tUtc.AddHours(2).Date;
            if (ses <= new DateTime(2026, 9, 14)) return "U6";
            if (ses <= new DateTime(2026, 12, 17)) return "Z6";
            return "H7";
        }
        /// <summary>Contrato fijo (el del codigo del grafico: MNQZ6 -> "Z6").</summary>
        public static Func<DateTime, string> ContratoFijo(string k) { var c = k ?? ""; return _ => c; }
        /// <summary>"MNQZ6" / "NQZ6" / "MNQ Z6" -> "Z6" ("" si no se reconoce).</summary>
        public static string ContratoDeCodigo(string codigo)
        {
            if (string.IsNullOrEmpty(codigo)) return "";
            var s = codigo.Trim().ToUpperInvariant();
            int i = s.Length - 1;
            while (i >= 0 && char.IsDigit(s[i])) i--;
            if (i < 0 || i == s.Length - 1) return "";
            char mes = s[i];
            if ("FGHJKMNQUVXZ".IndexOf(mes) < 0) return "";
            return s.Substring(i);
        }
    }

    /// <summary>Lo que la conversion sabe de UNA foto de QQQ a su 'generado' (inmutable despues de Observar).</summary>
    public sealed class FotoQqq
    {
        public FotoCadena Foto;               // se libera (null) al podar las viejas
        public int Indice;                    // orden de llegada (el 'k' del Python: clave de la cache del zero)
        public DateTime GeneradoUtc, TsUtc, DatoUtc;
        public double Spot = double.NaN;
        public DateTime TSpotUtc;             // ts - 900 s
        public string ContratoMuestra = "";   // contrato_de(t_spot)
        public bool Vivo;
        public double Precio = double.NaN;    // MNQ en ts - 900 s (cinta, o sembrado del archivo de muestras)
        public double Muestra = double.NaN;   // precio / spot (solo vivo)
        public double Razon = double.NaN;     // C8 vigente a esta foto (NaN si < 5 muestras)
        public string RazonContrato = "";
        public int RazonN;
        public bool OiViejo;                  // "OI de 2 sesiones" (solo marca)
        public double OiTotal;
        public bool Congelada;                // ultimo_trade NY >= 15:59
        public DateTime VigenteHastaUtc;      // C9
    }

    /// <summary>C8 + oi_fresco + congelada + C9 de QQQ, foto por foto EN ORDEN de 'generado' (backtest_familia.conversion es causal: el estado de
    /// la foto i solo depende de las fotos 0..i, asi que procesarlas de a una apenas llegan da lo mismo que recalcular todo).</summary>
    public sealed class ConversionQqq
    {
        public const double RETRASO_S = 900, VIVO_S = 600, VIDA_CBOE_S = 1500, PISO_REL = 0.0002;
        public const int VENTANA = 24, MIN_N = 5;

        private readonly OpcionesQqq _op;
        private readonly List<(DateTime Gen, double Spot)> _recientes = new List<(DateTime, double)>();   // fotos de los ultimos 600 s
        private readonly List<double> _roll = new List<double>();
        private string _rollContrato;          // null = "None" del Python (la primera foto siempre "cambia" de contrato)
        private bool _oiViejo;
        private double _oiPrev = double.NaN;   // None
        private DateTime _genPrev = DateTime.MinValue;
        private int _n;
        private readonly Dictionary<DateTime, (double Precio, string Contrato)> _sembrados = new Dictionary<DateTime, (double, string)>();

        public ConversionQqq(OpcionesQqq op) { _op = op ?? new OpcionesQqq(); }
        public int Procesadas => _n;

        /// <summary>Precio del MNQ en ts-900 s guardado en una corrida anterior (archivo de muestras): se usa si la cinta no lo tiene, y solo si
        /// es del MISMO contrato que hoy le toca a esa muestra (despues de un roll, un precio de Z6 no puede entrar en la razon de H7).</summary>
        public void SembrarPrecio(DateTime tsUtc, double precio, string contrato = null)
        {
            if (double.IsNaN(precio)) return;
            _sembrados[HoraQqq.Seg(tsUtc)] = (precio, contrato);
            if (_sembrados.Count > 20000) _sembrados.Clear();          // tope de memoria (6 dias de QQQ son ~7000 fotos)
        }

        /// <summary>Procesa la foto siguiente (generado creciente). precioCinta = ICinta.Precio(ts - 900 s) (NaN si no hay).</summary>
        public FotoQqq Observar(FotoCadena f, double precioCinta)
        {
            var gen = HoraQqq.Seg(f.GeneradoUtc);
            var ts = HoraQqq.Seg(f.TsUtc);
            var r = new FotoQqq { Foto = f, Indice = _n, GeneradoUtc = gen, TsUtc = ts, DatoUtc = HoraQqq.Utc(f.DatoUtc), Spot = f.Spot, OiTotal = f.OiTotal };
            r.TSpotUtc = ts.AddSeconds(-RETRASO_S);
            r.ContratoMuestra = _op.ContratoDe(r.TSpotUtc) ?? "";
            double precio = precioCinta;
            if (double.IsNaN(precio) && _sembrados.TryGetValue(ts, out var ps) && (string.IsNullOrEmpty(ps.Contrato) || ps.Contrato == r.ContratoMuestra)) precio = ps.Precio;
            r.Precio = precio;

            // vivo: alguna foto de los 600 s previos (por generado) con otro spot
            bool vivo = false;
            for (int j = _recientes.Count - 1; j >= 0; j--)
            {
                if ((gen - _recientes[j].Gen).TotalSeconds > VIVO_S) break;
                if (Math.Abs(_recientes[j].Spot - r.Spot) > 1e-9) { vivo = true; break; }
            }
            r.Vivo = vivo;
            _recientes.Add((gen, r.Spot));
            int corte = 0;
            while (corte < _recientes.Count - 1 && (gen - _recientes[corte].Gen).TotalSeconds > VIVO_S) corte++;
            if (corte > 0) _recientes.RemoveRange(0, corte);

            double val = double.NaN;
            if (vivo && !double.IsNaN(precio) && r.Spot > 0) val = precio / r.Spot;
            r.Muestra = val;

            // C8
            if (_rollContrato == null || _rollContrato != r.ContratoMuestra) { _roll.Clear(); _rollContrato = r.ContratoMuestra; }
            if (!double.IsNaN(val)) { _roll.Add(val); if (_roll.Count > VENTANA) _roll.RemoveRange(0, _roll.Count - VENTANA); }
            var (v, n) = Robusta(_roll, PISO_REL * (_roll.Count > 0 ? Mediana(_roll) : 0.0));
            r.Razon = n >= MIN_N ? v : double.NaN;
            r.RazonContrato = _rollContrato;
            r.RazonN = n;

            // oi_fresco (solo marca)
            int hmu = Hm(gen);
            int hprev = _n > 0 ? Hm(_genPrev) : -1;   // Python: "12:00" UTC para la primera = fuera de la franja de dia (en NY 12:00 seria dia: -1 lo evita)
            if (EnDia(hmu)) _oiViejo = false;
            else if (EnNoche(hmu) && !_oiViejo && (EnDia(hprev) || (_n > 0 && (gen - _genPrev).TotalSeconds > 6 * 3600))) _oiViejo = true;
            else if (EnMadrugada(hmu) && _oiViejo && !double.IsNaN(_oiPrev) && _oiPrev != 0 && Math.Abs(r.OiTotal - _oiPrev) / _oiPrev > 0.05) _oiViejo = false;
            r.OiViejo = _oiViejo;
            if (r.OiTotal > 0) _oiPrev = r.OiTotal;

            // congelada + C9
            r.Congelada = r.DatoUtc.Year >= 2000 && HoraQqq.Hm(HoraQqq.ANy(r.DatoUtc)) >= 15 * 60 + 59;   // sin hora del dato: no congelada
            r.VigenteHastaUtc = Vigencia(gen, r.Congelada);

            _genPrev = gen;
            _n++;
            return r;
        }

        /// <summary>C9: 25 min desde generado; congelada: hasta la proxima apertura (13:30 UTC del Python | 09:30 NY corregida) si es mas tarde.</summary>
        public DateTime Vigencia(DateTime genUtc, bool congelada)
        {
            var h1 = genUtc.AddSeconds(VIDA_CBOE_S);
            if (!congelada) return h1;
            DateTime prox;
            if (_op.Corregida)
            {
                var ny = HoraQqq.ANy(genUtc);
                prox = HoraQqq.AUtc(ny.Date.AddHours(9).AddMinutes(30));
                if (prox <= genUtc) prox = HoraQqq.AUtc(ny.Date.AddDays(1).AddHours(9).AddMinutes(30));
            }
            else
            {
                prox = HoraQqq.Utc(genUtc.Date.AddHours(13).AddMinutes(30));
                if (prox <= genUtc) prox = prox.AddDays(1);
            }
            return prox > h1 ? prox : h1;
        }

        // franjas de oi_fresco: dia [13:30, 22:00) | noche [22:00, 03:00) | madrugada [03:00, 13:30) en UTC (Python);
        // corregida: dia [09:30, 18:00) | noche [18:00, 23:00) | madrugada [23:00, 09:30) en NY.
        private int Hm(DateTime genUtc) => _op.Corregida ? HoraQqq.Hm(HoraQqq.ANy(genUtc)) : HoraQqq.Hm(genUtc);
        private bool EnDia(int hm) => _op.Corregida ? (hm >= 9 * 60 + 30 && hm < 18 * 60) : (hm >= 13 * 60 + 30 && hm < 22 * 60);
        private bool EnNoche(int hm) => _op.Corregida ? (hm >= 18 * 60 && hm < 23 * 60) : (hm >= 22 * 60 || hm < 3 * 60);
        private bool EnMadrugada(int hm) => _op.Corregida ? (hm >= 23 * 60 || hm < 9 * 60 + 30) : (hm >= 3 * 60 && hm < 13 * 60 + 30);

        /// <summary>Ventana de la sesion de CBOE (libros.fotos_cboe: generado en [dia-2h, dia+21h], o sea se descartan las de (21:00, 22:00) UTC
        /// exclusivo; corregida: (17:00, 18:00) NY).</summary>
        public bool FueraDeSesion(DateTime genUtc)
        {
            var d = _op.Corregida ? HoraQqq.ANy(genUtc) : genUtc;
            var tod = d.TimeOfDay;
            var a = _op.Corregida ? new TimeSpan(17, 0, 0) : new TimeSpan(21, 0, 0);
            return tod > a && tod < a.Add(TimeSpan.FromHours(1));
        }

        public static double Mediana(IReadOnlyList<double> x) => NumFam.Mediana(x);
        public static (double V, int N) Robusta(IReadOnlyList<double> x, double piso) => NumFam.Robusta(x, piso);

        // ------------------------------------------------------------------ archivo de muestras (lo escribe el motor en PythiaGex4/familia)
        /// <summary>Una linea de muestras-QQQ-&lt;sesion&gt;.jsonl: {ts, generado, spot, precio, muestra, vivo, contrato, razon, n}.</summary>
        public static string LineaMuestra(FotoQqq r)
        {
            var inv = CultureInfo.InvariantCulture;
            string N(double d) => double.IsNaN(d) ? "null" : d.ToString("R", inv);
            var sb = new StringBuilder(200);
            sb.Append("{\"ts\":\"").Append(r.TsUtc.ToString("yyyy-MM-dd HH:mm:ss", inv)).Append("\",\"generado\":\"").Append(r.GeneradoUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", inv))
              .Append("\",\"spot\":").Append(N(r.Spot)).Append(",\"precio\":").Append(N(r.Precio)).Append(",\"muestra\":").Append(N(r.Muestra))
              .Append(",\"vivo\":").Append(r.Vivo ? "true" : "false").Append(",\"contrato\":\"").Append(r.ContratoMuestra).Append("\",\"razon\":").Append(N(r.Razon))
              .Append(",\"n\":").Append(r.RazonN.ToString(inv)).Append('}');
            return sb.ToString();
        }

        /// <summary>Lee (ts, precio, contrato) de una linea de muestras (para SembrarPrecio al arrancar sin la cinta vieja).</summary>
        public static bool LeerLineaMuestra(string linea, out DateTime tsUtc, out double precio, out string contrato)
        {
            tsUtc = DateTime.MinValue; precio = double.NaN; contrato = null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(linea);
                var r = doc.RootElement;
                if (!DateTime.TryParseExact(r.GetProperty("ts").GetString(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return false;
                tsUtc = HoraQqq.Utc(t);
                if (r.TryGetProperty("contrato", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.String) contrato = c.GetString();
                var p = r.GetProperty("precio");
                if (p.ValueKind != System.Text.Json.JsonValueKind.Number) return false;
                precio = p.GetDouble();
                return true;
            }
            catch { return false; }
        }
    }
}
