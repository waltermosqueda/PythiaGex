// AlmacenNiveles.cs — PythiaGex 4.1, modulo fam (B3d), 08-10-2026.
// preview_niveles.Motor.niv (minuto -> {serie: [(precio, etq, monto)]}) mas lo que hace falta para los actuales, con persistencia propia:
//   %APPDATA%\ATAS\PythiaGex4\familia\niv-<sesion>-<contrato>.jsonl   (4.1.0: en modo paridad, niv-<sesion>-<contrato>-paridad.jsonl;
//   un archivo cuya cabecera es de OTRO modo se aparta como .otro-modo-<hora> y no se carga ni se mezcla)
//   linea 1: {"v":"fam-4.1","sesion":"2026-10-08","contrato":"MNQZ6","modo":"corregida"}
//   despues una por minuto calculado CON libros: {"k":29856842,"f":31403.75,"b":["NQ","NDX","QQQ"],
//      "m":[{"l":"NDX","c":241.62,"s":31162.1,"d":"2026-10-08T13:41:00Z","o":1,"z":0,"t":"base 241.62 (...)"}, ...],
//      "s":{"MUROS_NDX_vol":[[31444.03,"D1",252.9,31202.41],[31384.03,"D2",-62.7,31142.41]], ...}}
//   (precio NQ round 2, etiqueta, monto M USD round 1 o null, strike de su libro round 2 o null)
//   4.1.3: si el motor calcula las series extra (CatalogoFamilia.Extra), van en el mismo "s" con claves nuevas (R20_QQQ_vol, R20_NDX_vol,
//   DOMS_QQQ_vol, DOMS_NDX_vol) despues de las 21, y la conversion de las replicas de la 2.0 en "mx" (mismo formato que "m") al final de la
//   linea. Un minuto sin extras queda byte a byte como en la 4.1.2.
// Los minutos ya calculados NO se recalculan (el pasado no se mueve). Un archivo por sesion y contrato: con el roll del grafico no se
// mezclan contratos. Los minutos sin libros no se guardan (al reiniciar se vuelven a intentar). Retencion: 14 dias.
// Sin referencias a ATAS.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace PythiaGexCuatro.Familia
{
    public sealed class AlmacenNiveles
    {
        private readonly Dictionary<long, RegistroMinuto> _reg = new Dictionary<long, RegistroMinuto>();
        private readonly HashSet<long> _sinLibros = new HashSet<long>();
        private readonly string _ruta;                 // null = sin persistencia
        private readonly string _sesion, _contrato, _modo;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public long UltimaConLibros { get; private set; } = long.MinValue;
        public int Cargados { get; private set; }
        public int LineasRotas { get; private set; }
        public string Ruta => _ruta;

        public AlmacenNiveles(string carpetaFamilia, string sesion, string contrato, bool corregida)
        {
            _sesion = sesion; _contrato = string.IsNullOrEmpty(contrato) ? "sin-contrato" : contrato; _modo = corregida ? "corregida" : "paridad";
            // 4.1.0: el modo va en el nombre (el de paridad lleva "-paridad"; el corregido, que es el de fabrica, queda como estaba): apagar y
            // prender "Sesion y ventanas en hora de Nueva York" ya no mezcla minutos de los dos modos en el mismo archivo.
            if (!string.IsNullOrEmpty(carpetaFamilia))
                _ruta = Path.Combine(carpetaFamilia, "niv-" + sesion + "-" + Seguro(_contrato) + (corregida ? "" : "-paridad") + ".jsonl");
        }

        /// <summary>4.1.0: si el archivo tenia la cabecera de OTRO modo, se aparto (no se cargo ni se mezcla).</summary>
        public string Apartado { get; private set; }
        private bool _noEscribir;

        private static string Seguro(string s) { var sb = new StringBuilder(); foreach (var ch in s) sb.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_'); return sb.ToString(); }

        public bool Calculado(long clave) => _reg.ContainsKey(clave) || _sinLibros.Contains(clave);
        public RegistroMinuto Get(long clave) => _reg.TryGetValue(clave, out var r) ? r : null;
        public IEnumerable<RegistroMinuto> Todos => _reg.Values;
        public int Cuantos => _reg.Count;
        public RegistroMinuto Ultimo => UltimaConLibros == long.MinValue ? null : Get(UltimaConLibros);

        /// <summary>Anota un minuto calculado. r null = el minuto no tuvo libros (no entra en niv, como la vista previa).</summary>
        public void Poner(long clave, RegistroMinuto r)
        {
            if (r == null || !r.TieneLibros) { _sinLibros.Add(clave); return; }
            _reg[clave] = r;
            if (clave > UltimaConLibros) UltimaConLibros = clave;
        }

        // ------------------------------------------------------------------ persistencia
        /// <summary>Lee el archivo de la sesion (si existe). Tolera la ultima linea cortada. Devuelve cuantos minutos cargo.</summary>
        public int Cargar()
        {
            if (_ruta == null || !File.Exists(_ruta)) return 0;
            string[] lineas;
            try
            {
                using (var fs = new FileStream(_ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                    lineas = sr.ReadToEnd().Split('\n');
            }
            catch { return 0; }
            // 4.1.0: la cabecera tiene que ser del MISMO modo; si no, el archivo se aparta (.otro-modo-<hora>) y se empieza uno nuevo
            var cab = lineas.Length > 0 ? lineas[0].TrimEnd('\r') : "";
            if (cab.StartsWith("{\"v\"", StringComparison.Ordinal) && cab.IndexOf("\"modo\":\"" + _modo + "\"", StringComparison.Ordinal) < 0)
            {
                try
                {
                    var dest = _ruta + ".otro-modo-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", Inv);
                    File.Move(_ruta, dest);
                    Apartado = Path.GetFileName(dest);
                }
                catch { Apartado = "(no se pudo apartar: no se escribe)"; _noEscribir = true; }
                return 0;
            }
            int n = 0;
            foreach (var l0 in lineas)
            {
                var l = l0.TrimEnd('\r');
                if (l.Length < 5) continue;
                var r = Parsear(l);
                if (r == null) { if (!l.StartsWith("{\"v\"", StringComparison.Ordinal)) LineasRotas++; continue; }
                _reg[r.Clave] = r; n++;
                if (r.Clave > UltimaConLibros) UltimaConLibros = r.Clave;
            }
            Cargados = n;
            return n;
        }

        /// <summary>Agrega al final del archivo los minutos nuevos (con libros). Escribe la cabecera si el archivo es nuevo.</summary>
        public void Anexar(IEnumerable<RegistroMinuto> nuevos)
        {
            if (_ruta == null || _noEscribir) return;
            var sb = new StringBuilder();
            foreach (var r in nuevos) if (r != null && r.TieneLibros) sb.Append(Linea(r)).Append('\n');
            if (sb.Length == 0) return;
            Directory.CreateDirectory(Path.GetDirectoryName(_ruta));
            bool nuevo = !File.Exists(_ruta);
            using (var fs = new FileStream(_ruta, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                var enc = new UTF8Encoding(false);
                if (nuevo)
                {
                    var cab = "{\"v\":\"fam-4.1\",\"sesion\":\"" + _sesion + "\",\"contrato\":\"" + Esc(_contrato) + "\",\"modo\":\"" + _modo + "\"}\n";
                    var b0 = enc.GetBytes(cab); fs.Write(b0, 0, b0.Length);
                }
                var b = enc.GetBytes(sb.ToString()); fs.Write(b, 0, b.Length);
            }
        }

        /// <summary>Borra los niv-*.jsonl de mas de 'dias' dias (por fecha de modificacion).</summary>
        public static int Podar(string carpetaFamilia, int dias)
        {
            int n = 0;
            try
            {
                if (!Directory.Exists(carpetaFamilia)) return 0;
                var corte = DateTime.UtcNow.AddDays(-dias);
                foreach (var f in Directory.GetFiles(carpetaFamilia, "niv-*.jsonl").Concat(Directory.GetFiles(carpetaFamilia, "niv-*.jsonl.otro-modo-*")))
                    try { if (File.GetLastWriteTimeUtc(f) < corte) { File.Delete(f); n++; } } catch { }
            }
            catch { }
            return n;
        }

        private static string Num(double x) => double.IsNaN(x) || double.IsInfinity(x) ? "null" : x.ToString("R", Inv);
        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (var ch in s)
            {
                if (ch == '"' || ch == '\\') sb.Append('\\').Append(ch);
                else if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4", Inv));
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        public static string Linea(RegistroMinuto r)
        {
            var sb = new StringBuilder(1024);
            sb.Append("{\"k\":").Append(r.Clave.ToString(Inv)).Append(",\"f\":").Append(Num(r.FutMnq)).Append(",\"b\":[");
            for (int i = 0; i < r.Libros.Length; i++) { if (i > 0) sb.Append(','); sb.Append('"').Append(Esc(r.Libros[i])).Append('"'); }
            sb.Append("],\"m\":[");
            for (int i = 0; i < r.Meta.Length; i++) { if (i > 0) sb.Append(','); MetaJson(sb, r.Meta[i]); }
            sb.Append("],\"s\":{");
            bool primera = true;
            var calc = CatalogoFamilia.Calc;
            for (int s = 0; s < calc.Length; s++)
                SerieJson(sb, calc[s], r.Series?[s], r.Strikes?[s], ref primera);
            // 4.1.3: las series extra (R20_*, DOMS_*) van en el mismo objeto "s", con claves nuevas, despues de las 21 de siempre
            if (r.Extra != null)
                for (int s = 0; s < CatalogoFamilia.Extra.Length && s < r.Extra.Length; s++)
                    SerieJson(sb, CatalogoFamilia.Extra[s], r.Extra[s], r.ExtraStrikes != null && s < r.ExtraStrikes.Length ? r.ExtraStrikes[s] : null, ref primera);
            sb.Append('}');
            // 4.1.3: la conversion y la edad de las replicas de la 2.0 ("mx", mismo formato que "m"; solo si hay)
            if (r.MetaExtra != null && r.MetaExtra.Length > 0)
            {
                sb.Append(",\"mx\":[");
                for (int i = 0; i < r.MetaExtra.Length; i++) { if (i > 0) sb.Append(','); MetaJson(sb, r.MetaExtra[i]); }
                sb.Append(']');
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static void SerieJson(StringBuilder sb, string id, Nivel[] lv, double[] stk, ref bool primera)
        {
            if (lv == null) return;
            if (!primera) sb.Append(','); primera = false;
            sb.Append('"').Append(id).Append("\":[");
            for (int k = 0; k < lv.Length; k++)
            {
                if (k > 0) sb.Append(',');
                double st = stk != null && k < stk.Length ? stk[k] : double.NaN;
                sb.Append('[').Append(Num(lv[k].Precio)).Append(",\"").Append(Esc(lv[k].Etq)).Append("\",").Append(Num(lv[k].GexM)).Append(',').Append(Num(st)).Append(']');
            }
            sb.Append(']');
        }

        private static void MetaJson(StringBuilder sb, MetaLibro m)
        {
            sb.Append("{\"l\":\"").Append(Esc(m.Libro)).Append("\",\"c\":").Append(Num(m.Conv)).Append(",\"s\":").Append(Num(m.S))
              .Append(",\"d\":\"").Append(DateTime.SpecifyKind(m.DatoUtc, DateTimeKind.Utc).ToString("o", Inv)).Append("\",\"o\":").Append(m.OiOk ? '1' : '0')
              .Append(",\"z\":").Append(m.Congelada ? '1' : '0').Append(",\"t\":\"").Append(Esc(m.ConvTexto)).Append("\"}");
        }

        public static RegistroMinuto Parsear(string linea)
        {
            try
            {
                using (var doc = JsonDocument.Parse(linea))
                {
                    var j = doc.RootElement;
                    if (!j.TryGetProperty("k", out var ke) || ke.ValueKind != JsonValueKind.Number) return null;
                    var calc = CatalogoFamilia.Calc;
                    var r = new RegistroMinuto { Clave = ke.GetInt64(), Series = new Nivel[calc.Length][], Strikes = new double[calc.Length][] };
                    r.FutMnq = D(j, "f");
                    if (j.TryGetProperty("b", out var be) && be.ValueKind == JsonValueKind.Array) r.Libros = be.EnumerateArray().Select(x => x.GetString()).ToArray();
                    if (j.TryGetProperty("m", out var me) && me.ValueKind == JsonValueKind.Array)
                        r.Meta = me.EnumerateArray().Select(LeerMeta).ToArray();
                    if (j.TryGetProperty("mx", out var mx) && mx.ValueKind == JsonValueKind.Array)      // 4.1.3
                        r.MetaExtra = mx.EnumerateArray().Select(LeerMeta).ToArray();
                    if (j.TryGetProperty("s", out var se) && se.ValueKind == JsonValueKind.Object)
                        foreach (var p in se.EnumerateObject())
                        {
                            int i = CatalogoFamilia.IndiceCalc(p.Name);
                            int ix = i < 0 ? CatalogoFamilia.IndiceExtra(p.Name) : -1;     // 4.1.3: series extra (R20_*, DOMS_*)
                            if ((i < 0 && ix < 0) || p.Value.ValueKind != JsonValueKind.Array) continue;
                            var lv = new List<Nivel>(); var st = new List<double>();
                            foreach (var a in p.Value.EnumerateArray())
                            {
                                if (a.ValueKind != JsonValueKind.Array || a.GetArrayLength() < 2) continue;
                                double pr = a[0].ValueKind == JsonValueKind.Number ? a[0].GetDouble() : double.NaN;
                                string e = a[1].ValueKind == JsonValueKind.String ? a[1].GetString() : "";
                                double g = a.GetArrayLength() > 2 && a[2].ValueKind == JsonValueKind.Number ? a[2].GetDouble() : double.NaN;
                                double k = a.GetArrayLength() > 3 && a[3].ValueKind == JsonValueKind.Number ? a[3].GetDouble() : double.NaN;
                                lv.Add(new Nivel(pr, e, g)); st.Add(k);
                            }
                            if (lv.Count == 0) continue;
                            if (i >= 0) { r.Series[i] = lv.ToArray(); r.Strikes[i] = st.ToArray(); }
                            else
                            {
                                if (r.Extra == null) { r.Extra = new Nivel[CatalogoFamilia.Extra.Length][]; r.ExtraStrikes = new double[CatalogoFamilia.Extra.Length][]; }
                                r.Extra[ix] = lv.ToArray(); r.ExtraStrikes[ix] = st.ToArray();
                            }
                        }
                    return r.Libros.Length > 0 ? r : null;
                }
            }
            catch { return null; }
        }

        private static MetaLibro LeerMeta(JsonElement x) => new MetaLibro
        {
            Libro = S(x, "l"), Conv = D(x, "c"), S = D(x, "s"), ConvTexto = S(x, "t"),
            DatoUtc = DateTime.TryParse(S(x, "d"), Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) ? DateTime.SpecifyKind(t, DateTimeKind.Utc) : default,
            OiOk = D(x, "o") != 0, Congelada = D(x, "z") == 1
        };

        private static double D(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN;
        private static string S(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
    }
}
