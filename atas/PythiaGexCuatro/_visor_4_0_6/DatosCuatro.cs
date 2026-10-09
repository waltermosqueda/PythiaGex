using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace PythiaGexCuatro
{
    /// <summary>
    /// Lo que dibuja la 4.0, leido de los mismos json que la vista previa (profundidad/pagina/preview_datos/):
    ///   preview_niveles.json  lo escribe laboratorio/tres/auditoria_0810/preview_niveles.py cada minuto (NQ, NDX, QQQ, familia, zero, 3.0)
    ///   tqqq_vivo.json        lo escribe laboratorio/tres/tqqq/tqqq_vivo.py cada minuto (TQQQ x3 bien convertido)
    /// La 4.0 NO calcula: dibuja exactamente lo de la pagina (misma cuenta, mismos numeros). Una foto inmutable por lectura.
    /// </summary>
    internal sealed class Foto
    {
        public sealed class Serie
        {
            public string Id, Corto, Libro, Fuente, Tipo, Grupo;
            public Color Color;
            public double Banda;
            public readonly Dictionary<long, double[]> Hist = new Dictionary<long, double[]>();   // t_ms (vela m2 UTC) -> precios en NQ
        }
        public sealed class Actual
        {
            public string Serie, Libro, Fuente, Rol;
            public double Precio, Strike = double.NaN, Gex = double.NaN, Banda;
            public DateTime DatoUtc;
            public bool OiViejo;                 // la pagina marca "(OI 2s)": OI de dos sesiones atras (generador, oi_ok)
        }
        public sealed class Fuente
        {
            public string Lb, Conv = "", OiTxt = "";
            public DateTime DatoUtc = DateTime.MinValue;
            public bool Congelada;
        }
        public readonly List<Fuente> Fuentes = new List<Fuente>();
        public string Instrumento = "", TqSesion = "";
        public double NdxBase = double.NaN, QqqRazon = double.NaN;          // conv_valor de fuentes.NDX / fuentes.QQQ (lo que usan sus rayas)
        public DateTime NdxDatoUtc = DateTime.MinValue, QqqDatoUtc = DateTime.MinValue;
        public readonly Dictionary<string, Serie> Series = new Dictionary<string, Serie>();
        public readonly List<Actual> Actuales = new List<Actual>();
        public DateTime GeneradoUtc = DateTime.MinValue, GeneradoTqqqUtc = DateTime.MinValue;
        public string Sesion = "", Modo = "";
        public double TqS = double.NaN, TqC = double.NaN;
        public DateTime TqDatoUtc = DateTime.MinValue;
        public double Precio = double.NaN;
        public string Error = "";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static Foto Leer(string carpeta)
        {
            var f = new Foto();
            var rP = Path.Combine(carpeta, "preview_niveles.json");
            var rT = Path.Combine(carpeta, "tqqq_vivo.json");
            try
            {
                using (var d = JsonDocument.Parse(LeerCompartido(rP)))
                {
                    var r = d.RootElement;
                    f.GeneradoUtc = Fecha(r, "generado_utc");
                    f.Sesion = Str(r, "sesion"); f.Modo = Str(r, "modo");
                    if (r.TryGetProperty("precio", out var px) && px.ValueKind == JsonValueKind.Object) { f.Precio = Num(px, "ultimo"); f.Instrumento = Str(px, "instrumento"); }
                    if (r.TryGetProperty("fuentes", out var fs) && fs.ValueKind == JsonValueKind.Object)
                        foreach (var lb in new[] { "NQ", "NDX", "QQQ" })
                            if (fs.TryGetProperty(lb, out var fe) && fe.ValueKind == JsonValueKind.Object)
                            {
                                f.Fuentes.Add(LeerFuente(lb, fe));
                                if (lb == "NDX") { f.NdxBase = Num(fe, "conv_valor"); f.NdxDatoUtc = Fecha(fe, "dato_utc"); }
                                if (lb == "QQQ") { f.QqqRazon = Num(fe, "conv_valor"); f.QqqDatoUtc = Fecha(fe, "dato_utc"); }
                            }
                    Cargar(f, r);
                }
            }
            catch (Exception e)
            {   // una lectura a medias cuenta como falla: no se dibuja nada parcial
                f.Series.Clear(); f.Actuales.Clear(); f.Fuentes.Clear();
                f.Error = "no pude leer " + rP + ": " + e.Message; return f;
            }
            try
            {
                if (File.Exists(rT))
                    using (var d = JsonDocument.Parse(LeerCompartido(rT)))
                    {
                        var r = d.RootElement;
                        f.TqSesion = Str(r, "sesion");
                        if (f.TqSesion == f.Sesion)
                        {
                            f.GeneradoTqqqUtc = Fecha(r, "generado_utc");
                            if (r.TryGetProperty("conv", out var cv) && cv.ValueKind == JsonValueKind.Object) { f.TqS = Num(cv, "s"); f.TqC = Num(cv, "c"); }
                            if (r.TryGetProperty("fuentes", out var fu) && fu.TryGetProperty("TQQQ", out var ft)) { f.TqDatoUtc = Fecha(ft, "dato_utc"); f.Fuentes.Add(LeerFuente("TQQQ", ft)); }
                            Cargar(f, r);
                        }
                    }
            }
            catch (Exception e) { f.Error = "TQQQ: " + e.Message; }
            return f;
        }

        private static void Cargar(Foto f, JsonElement r)
        {
            if (r.TryGetProperty("series", out var ss) && ss.ValueKind == JsonValueKind.Array)
                foreach (var s in ss.EnumerateArray())
                {
                    var x = new Serie { Id = Str(s, "id"), Corto = Str(s, "corto"), Libro = Str(s, "libro"), Fuente = Str(s, "fuente"), Tipo = Str(s, "tipo"), Grupo = Str(s, "grupo"),
                                        Color = Hex(Str(s, "color")), Banda = Num(s, "banda") };
                    if (x.Id != "") f.Series[x.Id] = x;
                }
            if (r.TryGetProperty("historia", out var h) && h.ValueKind == JsonValueKind.Object)
                foreach (var p in h.EnumerateObject())
                {
                    if (!f.Series.TryGetValue(p.Name, out var se) || p.Value.ValueKind != JsonValueKind.Array) continue;
                    foreach (var fila in p.Value.EnumerateArray())
                    {
                        if (fila.ValueKind != JsonValueKind.Array || fila.GetArrayLength() < 2 || fila[0].ValueKind != JsonValueKind.Number) continue;
                        long t = (long)fila[0].GetDouble();
                        var lv = fila[1]; if (lv.ValueKind != JsonValueKind.Array) continue;
                        var ps = new List<double>();
                        foreach (var n in lv.EnumerateArray())
                        {
                            var v = n.ValueKind == JsonValueKind.Array && n.GetArrayLength() > 0 ? n[0] : n;
                            if (v.ValueKind == JsonValueKind.Number) ps.Add(v.GetDouble());
                        }
                        if (ps.Count > 0) se.Hist[t] = ps.ToArray();
                    }
                }
            if (r.TryGetProperty("actuales", out var ac) && ac.ValueKind == JsonValueKind.Array)
                foreach (var a in ac.EnumerateArray())
                {
                    var x = new Actual { Serie = Str(a, "serie"), Libro = Str(a, "libro"), Fuente = Str(a, "fuente"), Rol = Str(a, "rol"), Precio = Num(a, "precio"),
                                         Strike = Num(a, "strike"), Gex = Num(a, "gex_musd"), Banda = Num(a, "banda"), DatoUtc = Fecha(a, "dato_utc"),
                                         OiViejo = a.TryGetProperty("oi_viejo", out var ov) && ov.ValueKind == JsonValueKind.True };
                    if (x.Serie != "" && !double.IsNaN(x.Precio)) f.Actuales.Add(x);
                }
        }

        private static Fuente LeerFuente(string lb, JsonElement e) => new Fuente
        {
            Lb = lb, DatoUtc = Fecha(e, "dato_utc"), Conv = Str(e, "conv"), OiTxt = Str(e, "oi_txt"),
            Congelada = e.TryGetProperty("congelada", out var c) && c.ValueKind == JsonValueKind.True
        };

        /// <summary>el generador reemplaza el archivo con os.replace: se lee entero con FileShare amplio y se reintenta una vez.</summary>
        private static byte[] LeerCompartido(string ruta)
        {
            for (int i = 0; ; i++)
            {
                try
                {
                    using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var ms = new MemoryStream()) { fs.CopyTo(ms); return ms.ToArray(); }
                }
                catch (IOException) when (i < 2) { System.Threading.Thread.Sleep(150); }
            }
        }

        private static string Str(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
        private static double Num(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN;
        private static DateTime Fecha(JsonElement e, string k)
        {
            var s = Str(e, k);
            return DateTime.TryParse(s, Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) ? DateTime.SpecifyKind(t, DateTimeKind.Utc) : DateTime.MinValue;
        }
        private static Color Hex(string s)
        {
            try { if (s.StartsWith("#") && s.Length == 7) return Color.FromArgb(int.Parse(s.Substring(1, 2), NumberStyles.HexNumber), int.Parse(s.Substring(3, 2), NumberStyles.HexNumber), int.Parse(s.Substring(5, 2), NumberStyles.HexNumber)); }
            catch { }
            return Color.FromArgb(200, 200, 200);
        }
    }

    internal static class Bitacora
    {
        private static readonly object _l = new object();
        public static string Carpeta => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex4");
        public static void Linea(string m)
        {
            try { Directory.CreateDirectory(Carpeta); lock (_l) File.AppendAllText(Path.Combine(Carpeta, "pythiagex4.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + m + "\n", new UTF8Encoding(false)); }
            catch { }
        }
    }
}
