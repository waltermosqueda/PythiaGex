// MuestrasClasica.cs — PythiaGex 4.1.5d (09-10-2026), modulo clasica. Revision de la 4.1.5b/c (hallazgo: "la base de la rueda de un dia ya cerrado
// cambia segun como arranque el indicador": el 09-10 a las 17:35 ART la de la rueda del 09-10 paso de 229,32 a 229,98 con las MISMAS 296 muestras,
// porque la cinta arranco sin las velas del grafico y unos baldes de 2 min con un hueco de ticks dieron otro cierre).
// Las muestras de la regla de la base (ReglaBaseClasica) se guardan la PRIMERA vez que se miden con una vela COMPLETA, una linea por sello de CBOE, en
//   <CarpetaDatos>\familia\muestras-clasica-NDX-<dia UTC del sello>.jsonl
//   {"ts":"2026-10-09 19:40:37","generado":"2026-10-09T19:41:02.123Z","vela":"2026-10-09 19:24:37","vela_min":2,"retraso_s":960,"spot":30900.62,
//    "precio":31130.5,"muestra":229.88,"version":"clasica 4.1.5d (09-10-2026)"}
// y al reiniciar ClasicaNdx usa el precio guardado en vez de la cinta de ese momento (como C7 con muestras-NDX y C8 con muestras-QQQ). Clave: el sello,
// el marco de la vela y el retraso (otro marco u otro retraso es otra medicion). Append; tolera lineas rotas; gana la primera de cada clave.
// Nunca tira (los errores van al log). Un solo escritor: el host de la Familia del instrumento (un hilo).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PythiaGexCuatro.Familia
{
    /// <summary>Una muestra guardada de la regla de la base de la clasica.</summary>
    public sealed class MuestraClasicaGuardada
    {
        public DateTime TsUtc, GeneradoUtc, VelaUtc;
        public double Spot = double.NaN, Precio = double.NaN, Muestra = double.NaN;
        public int VelaMin, RetrasoS;
        public string Version = "";
    }

    public sealed class MuestrasClasicaArchivo
    {
        public const string PREFIJO = "muestras-clasica-NDX-";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly string _carpeta;
        private readonly Action<string> _log;
        private readonly Dictionary<(DateTime Ts, int VelaMin, int RetrasoS), MuestraClasicaGuardada> _mapa = new Dictionary<(DateTime, int, int), MuestraClasicaGuardada>();
        private readonly HashSet<DateTime> _leidos = new HashSet<DateTime>();
        private int _rotas;

        /// <summary>carpetaDatos = %APPDATA%\ATAS\PythiaGex4 (adentro: familia\). null/"" = inactiva (no lee ni escribe).</summary>
        public MuestrasClasicaArchivo(string carpetaDatos, Action<string> log)
        {
            _carpeta = string.IsNullOrEmpty(carpetaDatos) ? null : Path.Combine(carpetaDatos, "familia");
            _log = log;
        }

        public bool Activa => _carpeta != null;
        public int Cuantas => _mapa.Count;
        public int LineasRotas => _rotas;
        public string Carpeta => _carpeta;

        public static string Ruta(string carpetaFamilia, DateTime diaUtc) => Path.Combine(carpetaFamilia, PREFIJO + diaUtc.ToString("yyyy-MM-dd", Inv) + ".jsonl");

        /// <summary>Lee los dias UTC [desde, hasta] que no leyo todavia (los de hoy y adelante se vuelven a leer: puede haber escrito otro arranque).</summary>
        public void Cargar(DateTime desdeUtc, DateTime hastaUtc)
        {
            if (_carpeta == null) return;
            int n0 = _mapa.Count;
            var hoy = DateTime.UtcNow.Date;
            for (var d = desdeUtc.Date; d <= hastaUtc.Date; d = d.AddDays(1))
            {
                if (d < hoy && _leidos.Contains(d)) continue;
                _leidos.Add(d);
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
                        if (m == null) { if (!string.IsNullOrWhiteSpace(l)) _rotas++; continue; }
                        var k = (m.TsUtc, m.VelaMin, m.RetrasoS);
                        if (!_mapa.ContainsKey(k)) _mapa[k] = m;          // gana la primera medicion
                    }
                }
                catch (Exception ex) { _log?.Invoke("muestras clasica: no pude leer " + p + ": " + ex.Message); }
            }
            if (_mapa.Count != n0) _log?.Invoke("muestras clasica: " + (_mapa.Count - n0) + " guardadas leidas de " + _carpeta + " (dias " + desdeUtc.ToString("MM-dd", Inv) + " a "
                                                 + hastaUtc.ToString("MM-dd", Inv) + "; " + _mapa.Count + " en memoria" + (_rotas > 0 ? ", " + _rotas + " lineas rotas ignoradas" : "") + ")");
        }

        public bool Buscar(DateTime tsUtc, int velaMin, int retrasoS, out MuestraClasicaGuardada m) => _mapa.TryGetValue((tsUtc, velaMin, retrasoS), out m);

        /// <summary>Guarda las muestras nuevas (precio &gt; 0; las que ya estan no se repiten). Primero en memoria (la sesion queda consistente aunque el disco
        /// falle) y despues una escritura por dia. Devuelve cuantas escribio.</summary>
        public int Guardar(IEnumerable<MuestraClasica> nuevas, int velaMin, int retrasoS, string version)
        {
            if (_carpeta == null || nuevas == null) return 0;
            var porDia = new Dictionary<DateTime, StringBuilder>();
            int n = 0;
            foreach (var s in nuevas)
            {
                if (s == null || !(s.Precio > 0) || s.TsUtc == default(DateTime)) continue;
                var k = (s.TsUtc, velaMin, retrasoS);
                if (_mapa.ContainsKey(k)) continue;
                var m = new MuestraClasicaGuardada { TsUtc = s.TsUtc, GeneradoUtc = s.GenUtc, VelaUtc = s.VelaUtc, Spot = s.Spot, Precio = s.Precio, Muestra = s.Muestra,
                                                     VelaMin = velaMin, RetrasoS = retrasoS, Version = version ?? "" };
                _mapa[k] = m;
                var d = s.TsUtc.Date;
                if (!porDia.TryGetValue(d, out var sb)) { sb = new StringBuilder(); porDia[d] = sb; }
                sb.Append(Linea(m)).Append('\n');
                n++;
            }
            if (n == 0) return 0;
            try
            {
                Directory.CreateDirectory(_carpeta);
                foreach (var kv in porDia) File.AppendAllText(Ruta(_carpeta, kv.Key), kv.Value.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex) { _log?.Invoke("muestras clasica: no pude escribir: " + ex.Message); return 0; }
            return n;
        }

        /// <summary>Borra los archivos de dias anteriores a 'antesDe' (retencion). Nunca tira.</summary>
        public void Podar(DateTime antesDeUtc)
        {
            if (_carpeta == null || !Directory.Exists(_carpeta)) return;
            try
            {
                foreach (var p in Directory.GetFiles(_carpeta, PREFIJO + "*.jsonl"))
                {
                    var nom = Path.GetFileNameWithoutExtension(p);
                    if (nom.Length < PREFIJO.Length + 10) continue;
                    if (DateTime.TryParseExact(nom.Substring(nom.Length - 10), "yyyy-MM-dd", Inv, DateTimeStyles.None, out var d) && d < antesDeUtc.Date)
                        File.Delete(p);
                }
            }
            catch (Exception ex) { _log?.Invoke("muestras clasica: poda: " + ex.Message); }
        }

        public static string Linea(MuestraClasicaGuardada m)
        {
            var sb = new StringBuilder(260);
            sb.Append("{\"ts\":\"").Append(m.TsUtc.ToString("yyyy-MM-dd HH:mm:ss", Inv)).Append("\",\"generado\":\"").Append(m.GeneradoUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", Inv))
              .Append("\",\"vela\":\"").Append(m.VelaUtc.ToString("yyyy-MM-dd HH:mm:ss", Inv)).Append("\",\"vela_min\":").Append(m.VelaMin.ToString(Inv))
              .Append(",\"retraso_s\":").Append(m.RetrasoS.ToString(Inv)).Append(",\"spot\":").Append(Num(m.Spot)).Append(",\"precio\":").Append(Num(m.Precio))
              .Append(",\"muestra\":").Append(Num(m.Muestra)).Append(",\"version\":\"").Append((m.Version ?? "").Replace("\\", "/").Replace("\"", "'")).Append("\"}");
            return sb.ToString();
        }

        private static string Num(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "null" : v.ToString("R", Inv);

        public static MuestraClasicaGuardada Parsear(string l)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(l) || !l.TrimEnd().EndsWith("}", StringComparison.Ordinal)) return null;
                using var doc = System.Text.Json.JsonDocument.Parse(l);
                var r = doc.RootElement;
                var m = new MuestraClasicaGuardada();
                if (!DateTime.TryParseExact(r.GetProperty("ts").GetString(), "yyyy-MM-dd HH:mm:ss", Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var ts)) return null;
                m.TsUtc = DateTime.SpecifyKind(ts, DateTimeKind.Utc);
                if (r.TryGetProperty("generado", out var g) && DateTime.TryParse(g.GetString(), Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var gd))
                    m.GeneradoUtc = DateTime.SpecifyKind(gd, DateTimeKind.Utc);
                if (r.TryGetProperty("vela", out var v) && DateTime.TryParseExact(v.GetString(), "yyyy-MM-dd HH:mm:ss", Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var vd))
                    m.VelaUtc = DateTime.SpecifyKind(vd, DateTimeKind.Utc);
                if (!r.TryGetProperty("vela_min", out var vm) || vm.ValueKind != System.Text.Json.JsonValueKind.Number) return null;
                if (!r.TryGetProperty("retraso_s", out var rs) || rs.ValueKind != System.Text.Json.JsonValueKind.Number) return null;
                m.VelaMin = vm.GetInt32(); m.RetrasoS = rs.GetInt32();
                m.Spot = r.TryGetProperty("spot", out var s) && s.ValueKind == System.Text.Json.JsonValueKind.Number ? s.GetDouble() : double.NaN;
                m.Precio = r.TryGetProperty("precio", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.Number ? p.GetDouble() : double.NaN;
                m.Muestra = r.TryGetProperty("muestra", out var mu) && mu.ValueKind == System.Text.Json.JsonValueKind.Number ? mu.GetDouble() : double.NaN;
                m.Version = r.TryGetProperty("version", out var ve) && ve.ValueKind == System.Text.Json.JsonValueKind.String ? ve.GetString() : "";
                return m.Precio > 0 ? m : null;
            }
            catch { return null; }
        }
    }
}
