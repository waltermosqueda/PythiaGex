// Program.cs — arnes 'ws_compat' de PythiaGex 4.1.5c (09-10-2026). Salida 0 = todo verde. Resultados en .\resultados\ws_compat.txt.
// ATAS guarda cada ajuste del indicador POR NOMBRE en el workspace (.ws): si una propiedad se renombra, cambia de tipo o de default, el grafico del
// operador pierde lo que eligio (o toma otro valor sin avisar). Esta prueba compara, por reflexion, la clase PythiaGexCuatro.FamiliaCuatro de:
//   NUEVA   atas/PythiaGexCuatro/bin/Release/PythiaGexCuatro.dll (la que compila correr_todo.ps1 en su primer paso)
//   4.1.5   referencia/PythiaGexCuatro-4.1.5.dll (copia de la 4.1.5 que se instalo el 09-10 ~16:3x ART; sha256 fijo, se verifica)
// Que mide:
//   W1 la referencia es la 4.1.5 (sha256 y la constante VERSION de la clase).
//   W2 la lista COMPLETA de propiedades publicas de instancia (propias y heredadas del Indicator de ATAS): la nueva = la 4.1.5 + S_R10_NDX_dom (4.1.5b).
//   W3 el mismo tipo, lectura/escritura y atributos (salvo [Display]) en cada propiedad comun.
//   W4 el mismo default: el valor de cada propiedad en una instancia recien creada de cada DLL (las que cambian entre dos instancias de la MISMA DLL,
//      p. ej. un id por instancia, no se comparan y se listan).
//   W5 S_R10_NDX_dom: bool de lectura y escritura, default True.
//   W6 los cambios de [Display] entre la 4.1.5 y la nueva son EXACTAMENTE los de herramientas/grupo_arriba_415c.py: las 70 casillas de su LISTA en
//      "0. PRENDER / APAGAR (todo lo que se dibuja)" con Order = su indice y su nombre corto; las demas, el mismo nombre y grupo con el Order + 1000
//      (si era menor que 1000). Las descripciones que cambiaron se listan (informativo).
// Las dos DLL se cargan desde la memoria (no se bloquea ningun archivo) en contextos separados; las DLL de ATAS se resuelven de su carpeta (solo lectura).
// 4.1.5d (09-10-2026): la LISTA del script paso a 101 (+20 bool de dibujo de la 3.0 y +11 enum: perfil/ver de la 3.0, barras y doble eje): W6 la lee
// igual (las movidas son las de la LISTA); la 4.1.5d no agrega propiedades (W2) ni cambia tipos o defaults (W3/W4). Descripciones nuevas o
// cambiadas (informativo): S_R10_NDX_zero, S_R10_NDX_dom (la salvedad), Tres41Tunel, Tunel3Sombreado, Raya3NqMas/Menos (dependencias).
// 4.1.5e (09-10-2026): la fuente de verdad del grupo pasa a herramientas/grupo_arriba_415e.py (las mismas 101, con la JERARQUIA de llaves: cada llave
// '...▸' antes de sus dependientes '↳ ...', y la primera frase de cada descripcion dice que llave necesita). W6 lee su LISTA igual (nombres y Order
// nuevos; tipos y defaults NO cambian: W2-W4). W7 corre el script con --verificar: el codigo coincide con la LISTA y una segunda pasada no cambia nada
// (idempotente). W8: la primera frase de la descripcion de cada una de las 101 es una de las del script (Llave / Necesita la llave / alguna de las llaves
// / No necesita ninguna llave). La jerarquia en si (que cada '↳' nombre la llave de arriba y que el codigo de dibujo la pida) la mide el arnes de la
// pantalla (A23).
// Se crean instancias de FamiliaCuatro: su constructor solo arma la serie oculta y se suscribe al dibujo (sin E/S ni hilos); OnInitialize NO se llama.
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WsCompat
{
    static class Program
    {
        const string ATAS = @"C:\Program Files (x86)\ATAS Platform";
        const string SHA_415 = "88023542542e250e0766f3144d35a5954d9c85d3e930f8b5376faac9f4dd83b0";   // scratchpad/dll415/PythiaGexCuatro.dll (09-10 16:31:44)
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static StreamWriter _out;
        static int _ok, _mal;
        static readonly List<string> _fallas = new List<string>();
        static void P(string s = "") { Console.WriteLine(s); _out?.WriteLine(s); _out?.Flush(); }
        static void Ok(bool c, string que, string det = "")
        {
            if (c) { _ok++; P("  OK   " + que); }
            else { _mal++; _fallas.Add(que + (det == "" ? "" : " -> " + det)); P("  MAL  " + que + (det == "" ? "" : " -> " + det)); }
        }

        sealed class Prop
        {
            public string Nombre, Tipo, Declara, Atributos = "", Default, DispNombre, DispGrupo, DispDesc;
            public int? DispOrden; public bool Lee, Escribe, Propia, TieneDisplay;
        }

        static int Main(string[] args)
        {
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            Console.OutputEncoding = Encoding.UTF8;
            AssemblyLoadContext.Default.Resolving += (ctx, an) =>
            {   // las DLL de ATAS se LEEN de su carpeta (no se copian ni se tocan)
                var p = Path.Combine(ATAS, an.Name + ".dll");
                return File.Exists(p) ? ctx.LoadFromAssemblyPath(p) : null;
            };
            var d0 = new DirectoryInfo(AppContext.BaseDirectory);
            while (d0 != null && !File.Exists(Path.Combine(d0.FullName, "ws_compat.csproj"))) d0 = d0.Parent;
            string aqui = d0?.FullName ?? Directory.GetCurrentDirectory();
            string raiz = Path.GetFullPath(Path.Combine(aqui, "..", "..", ".."));                      // ...\PythiaGex
            string nueva = args.Length > 0 ? args[0] : Path.Combine(raiz, "atas", "PythiaGexCuatro", "bin", "Release", "PythiaGexCuatro.dll");
            string vieja = args.Length > 1 ? args[1] : Path.Combine(aqui, "referencia", "PythiaGexCuatro-4.1.5.dll");
            string script = Path.Combine(raiz, "herramientas", "grupo_arriba_415e.py");   // 4.1.5e (antes grupo_arriba_415c.py)
            string res = Path.Combine(aqui, "resultados"); Directory.CreateDirectory(res);
            var sw = Stopwatch.StartNew();
            using (_out = new StreamWriter(Path.Combine(res, "ws_compat.txt"), false, new UTF8Encoding(false)))
            {
                P("=== arnes ws_compat (PythiaGex 4.1.5c; 4.1.5e) " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv));
                try { Correr(nueva, vieja, script); }
                catch (Exception e) { Ok(false, "excepcion", e.ToString()); }
                P();
                P("=== " + _ok + " OK, " + _mal + " MAL en " + sw.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s");
                foreach (var f in _fallas) P("  MAL " + f);
            }
            return _mal == 0 ? 0 : 1;
        }

        static string Sha(string ruta) { using var s = SHA256.Create(); return Convert.ToHexString(s.ComputeHash(File.ReadAllBytes(ruta))).ToLowerInvariant(); }

        static Type Cargar(string ruta, string nombre)
        {
            var alc = new AssemblyLoadContext(nombre, isCollectible: true);
            var asm = alc.LoadFromStream(new MemoryStream(File.ReadAllBytes(ruta)));
            return asm.GetType("PythiaGexCuatro.FamiliaCuatro", true);
        }

        static string Version(Type t) => t.GetField("VERSION", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue() as string ?? "?";

        static object Instancia(Type t, out string porQue)
        {
            porQue = "";
            try { return Activator.CreateInstance(t); }
            catch (Exception e) { var x = e.InnerException ?? e; porQue = x.GetType().Name + ": " + x.Message; return null; }
        }

        /// <summary>El valor como texto comparable (profundidad 1 para objetos: sus propiedades simples; mas adentro, solo el tipo).</summary>
        static string Desc(object v, int prof = 0)
        {
            if (v == null) return "null";
            var t = v.GetType();
            if (v is string s) return "\"" + s + "\"";
            if (v is bool b) return b ? "True" : "False";
            if (v is double dd) return dd.ToString("R", Inv);
            if (v is float ff) return ff.ToString("R", Inv);
            if (t.IsPrimitive || v is decimal) return Convert.ToString(v, Inv);
            if (t.IsEnum) return t.FullName + "." + v;
            if (v is DateTime dt) return dt.ToString("o", Inv);
            if (v is TimeSpan ts) return ts.ToString("c", Inv);
            if (v is System.Drawing.Color c) return "Color(" + c.A + "," + c.R + "," + c.G + "," + c.B + ")";
            if (v is System.Windows.Media.Color mc) return "MediaColor(" + mc.A + "," + mc.R + "," + mc.G + "," + mc.B + ")";
            if (prof >= 1) return t.FullName;
            if (v is IEnumerable e)
            {
                var l = new List<string>(); int n = 0;
                try { foreach (var x in e) { n++; if (l.Count < 16) l.Add(Desc(x, prof + 1)); } } catch (Exception ex) { l.Add("<excepcion " + ex.GetType().Name + ">"); }
                return t.FullName + "[" + n + ": " + string.Join(",", l) + "]";
            }
            var sb = new StringBuilder(t.FullName).Append('{');
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                string val;
                try { val = Desc(p.GetValue(v), prof + 1); } catch (Exception ex) { val = "<excepcion " + (ex.InnerException ?? ex).GetType().Name + ">"; }
                sb.Append(p.Name).Append('=').Append(val).Append(';');
            }
            return sb.Append('}').ToString();
        }

        static string Atributos(PropertyInfo p)
        {
            var l = new List<string>();
            foreach (var a in p.GetCustomAttributesData())
            {
                if (a.AttributeType.FullName == typeof(DisplayAttribute).FullName) continue;
                var args = a.ConstructorArguments.Select(x => Convert.ToString(x.Value, Inv)).Concat(a.NamedArguments.Select(x => x.MemberName + "=" + Convert.ToString(x.TypedValue.Value, Inv)));
                l.Add(a.AttributeType.FullName + "(" + string.Join(",", args) + ")");
            }
            l.Sort(StringComparer.Ordinal);
            return string.Join(" ", l);
        }

        static Dictionary<string, Prop> Leer(Type t, object inst)
        {
            var d = new Dictionary<string, Prop>(StringComparer.Ordinal);
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0 || d.ContainsKey(p.Name)) continue;     // si se oculta por nombre, manda la mas derivada (la primera)
                var x = new Prop
                {
                    Nombre = p.Name, Tipo = p.PropertyType.FullName, Declara = p.DeclaringType?.FullName, Propia = p.DeclaringType == t,
                    Lee = p.GetMethod != null && p.GetMethod.IsPublic, Escribe = p.SetMethod != null && p.SetMethod.IsPublic, Atributos = Atributos(p)
                };
                var da = p.GetCustomAttribute<DisplayAttribute>();
                if (da != null) { x.TieneDisplay = true; x.DispNombre = da.Name; x.DispGrupo = da.GroupName; x.DispOrden = da.GetOrder(); x.DispDesc = da.Description; }
                if (inst != null && x.Lee)
                {
                    try { x.Default = Desc(p.GetValue(inst)); }
                    catch (Exception e) { x.Default = "<excepcion " + (e.InnerException ?? e).GetType().Name + ">"; }
                }
                d[p.Name] = x;
            }
            return d;
        }

        static (string Grupo, List<(string Prop, string Nombre)> Lista) GrupoArriba(string ruta)
        {
            var s = File.ReadAllText(ruta, Encoding.UTF8);
            string grupo = Regex.Match(s, "^GRUPO = \"([^\"]+)\"", RegexOptions.Multiline).Groups[1].Value;
            int i = s.IndexOf("LISTA = [", StringComparison.Ordinal), j = i < 0 ? -1 : s.IndexOf("\n]", i, StringComparison.Ordinal);
            var lista = new List<(string, string)>();
            if (i >= 0 && j > i)
                foreach (Match m in Regex.Matches(s.Substring(i, j - i), "\\(\"(\\w+)\",\\s*\"([^\"]*)\"\\)")) lista.Add((m.Groups[1].Value, m.Groups[2].Value));
            return (grupo, lista);
        }

        static string D(Prop p) => p == null || !p.TieneDisplay ? "sin Display" : p.DispNombre + "|" + p.DispGrupo + "|" + (p.DispOrden?.ToString(Inv) ?? "sin Order");

        static void Correr(string nueva, string vieja, string script)
        {
            P("== 0. Las dos DLL");
            Ok(File.Exists(nueva) && File.Exists(vieja), "W0 existen la nueva (" + nueva + ") y la 4.1.5 (" + vieja + ")");
            if (!File.Exists(nueva) || !File.Exists(vieja)) return;
            string shaN = Sha(nueva), shaV = Sha(vieja);
            P("  nueva: " + new FileInfo(nueva).Length + " bytes, " + File.GetLastWriteTime(nueva).ToString("dd-MM HH:mm:ss", Inv) + ", sha256 " + shaN);
            P("  4.1.5: " + new FileInfo(vieja).Length + " bytes, sha256 " + shaV);
            var tN = Cargar(nueva, "nueva"); var tV = Cargar(vieja, "4.1.5");
            string vN = Version(tN), vV = Version(tV);
            P("  VERSION de la clase: nueva " + vN + ", referencia " + vV + "; FileVersion nueva " + FileVersionInfo.GetVersionInfo(nueva).FileVersion + ", referencia " + FileVersionInfo.GetVersionInfo(vieja).FileVersion);
            Ok(shaV == SHA_415 && vV == "4.1.5", "W1 la referencia es la 4.1.5 instalada (sha256 " + shaV.Substring(0, 12) + "..., VERSION " + vV + ")");
            P("  (informativo) la nueva es VERSION " + vN + (shaN == shaV ? " (IGUAL a la referencia: no hay nada que comparar)" : ""));

            var iN = Instancia(tN, out var pqN); var iV = Instancia(tV, out var pqV);
            var iN2 = Instancia(tN, out _); var iV2 = Instancia(tV, out _);
            Ok(iN != null && iV != null && iN2 != null && iV2 != null, "W0b una instancia recien creada de cada DLL fuera de ATAS (constructor sin E/S; OnInitialize no se llama)", pqN + " " + pqV);
            var pN = Leer(tN, iN); var pV = Leer(tV, iV); var pN2 = Leer(tN, iN2); var pV2 = Leer(tV, iV2);
            int propN = pN.Values.Count(x => x.Propia), propV = pV.Values.Count(x => x.Propia);
            P("  propiedades publicas: nueva " + pN.Count + " (" + propN + " de FamiliaCuatro, " + (pN.Count - propN) + " heredadas de ATAS); 4.1.5 " + pV.Count + " (" + propV + " propias)");

            P(); P("== 1. Nombres, tipos y atributos (lo que el .ws reconoce por nombre)");
            var nuevas = pN.Keys.Except(pV.Keys).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var faltan = pV.Keys.Except(pN.Keys).OrderBy(x => x, StringComparer.Ordinal).ToList();
            Ok(nuevas.SequenceEqual(new[] { "S_R10_NDX_dom" }) && faltan.Count == 0,
               "W2 la lista completa de propiedades publicas es la de la 4.1.5 mas S_R10_NDX_dom (nuevas: " + (nuevas.Count == 0 ? "ninguna" : string.Join(", ", nuevas)) + "; faltan: " + (faltan.Count == 0 ? "ninguna" : string.Join(", ", faltan)) + ")");
            var comunes = pV.Keys.Intersect(pN.Keys).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var malTipo = comunes.Where(k => pN[k].Tipo != pV[k].Tipo || pN[k].Lee != pV[k].Lee || pN[k].Escribe != pV[k].Escribe || pN[k].Propia != pV[k].Propia).ToList();
            Ok(malTipo.Count == 0, "W3a el mismo tipo, la misma lectura/escritura y el mismo dueño (FamiliaCuatro o el Indicator de ATAS) en las " + comunes.Count + " comunes",
               string.Join("; ", malTipo.Take(8).Select(k => k + ": " + pV[k].Tipo + (pV[k].Escribe ? " rw" : " r") + " -> " + pN[k].Tipo + (pN[k].Escribe ? " rw" : " r"))));
            var malAttr = comunes.Where(k => pN[k].Atributos != pV[k].Atributos).ToList();
            Ok(malAttr.Count == 0, "W3b los mismos atributos (salvo [Display]: Browsable, Range, ...) en las " + comunes.Count + " comunes",
               string.Join("; ", malAttr.Take(6).Select(k => k + ": '" + pV[k].Atributos + "' -> '" + pN[k].Atributos + "'")));
            int ocultas = pN.Values.Count(x => x.Propia && x.Atributos.Contains("System.ComponentModel.BrowsableAttribute(False)"));
            P("  (informativo) propias con [Browsable(false)] en la nueva: " + ocultas + " (" + string.Join(", ", pN.Values.Where(x => x.Propia && x.Atributos.Contains("BrowsableAttribute(False)")).Select(x => x.Nombre).Take(12)) + ")");

            P(); P("== 2. Defaults (una instancia recien creada de cada DLL)");
            var volatiles = comunes.Where(k => pN[k].Default != pN2[k].Default || pV[k].Default != pV2[k].Default).ToList();
            P("  propiedades que cambian entre dos instancias de la MISMA DLL (no se comparan): " + (volatiles.Count == 0 ? "ninguna" : string.Join(", ", volatiles)));
            var compar = comunes.Except(volatiles).ToList();
            var malDef = compar.Where(k => pN[k].Default != pV[k].Default).ToList();
            int propias = compar.Count(k => pN[k].Propia);
            Ok(malDef.Count == 0 && compar.Count >= comunes.Count - 3, "W4 el mismo default en " + (compar.Count - malDef.Count) + " de " + compar.Count + " propiedades comparables (" + propias + " de FamiliaCuatro, "
               + (compar.Count - propias) + " heredadas)", string.Join("; ", malDef.Take(8).Select(k => k + ": " + Corto(pV[k].Default) + " -> " + Corto(pN[k].Default))));
            var r = pN.TryGetValue("S_R10_NDX_dom", out var x5) ? x5 : null;
            Ok(r != null && r.Propia && r.Tipo == "System.Boolean" && r.Lee && r.Escribe && r.Default == "True",
               "W5 S_R10_NDX_dom (4.1.5b): bool de lectura y escritura de FamiliaCuatro, default True (ningun .ws la tiene: toma este default)", r == null ? "no existe" : r.Tipo + " " + r.Default);
            // los valores de algunas casillas, para leer (los del operador quedan en su .ws; estos son los de un grafico nuevo)
            P("  (informativo) defaults de la nueva: " + string.Join(", ", new[] { "S_R10_NDX_zero", "S_R10_NDX_dom", "S_TRES_NDX", "S_MUROS_NDX_vol", "Estela4", "Rotulos4", "Cabecera4", "Letra4", "MargenSup4", "Eje4Libro", "Carpeta4" }
                .Where(pN.ContainsKey).Select(k => k + "=" + Corto(pN[k].Default))));

            P(); P("== 3. [Display]: solo lo que cambio grupo_arriba_415e.py (4.1.5c: grupo_arriba_415c.py)");
            var (grupo, lista) = GrupoArriba(script);
            var idx = new Dictionary<string, int>(); for (int i = 0; i < lista.Count; i++) idx[lista[i].Prop] = i;
            P("  LISTA de " + script + ": " + lista.Count + " casillas al grupo '" + grupo + "'");
            int igualesD = 0, movidas = 0, corridas = 0, nuevasL = 0; var malD = new List<string>(); var descs = new List<string>();
            foreach (var k in pN.Keys.Where(k => pN[k].Propia).OrderBy(k => k, StringComparer.Ordinal))
            {
                var n = pN[k]; pV.TryGetValue(k, out var v);
                (string N, string G, int? O) esp;
                if (idx.TryGetValue(k, out int o)) esp = (lista[o].Nombre, grupo, o);
                else if (v == null || !v.TieneDisplay) { if (n.TieneDisplay && v != null) malD.Add(k + ": la 4.1.5 no tenia Display y la nueva " + D(n)); else if (v == null && n.TieneDisplay) malD.Add(k + ": nueva, fuera de la LISTA " + D(n)); continue; }
                else esp = (v.DispNombre, v.DispGrupo, v.DispOrden.HasValue && v.DispOrden.Value < 1000 ? v.DispOrden + 1000 : v.DispOrden);
                bool ok = n.TieneDisplay && n.DispNombre == esp.N && n.DispGrupo == esp.G && n.DispOrden == esp.O;
                if (!ok) { malD.Add(k + ": " + D(v) + " -> " + D(n) + " (esperado " + esp.N + "|" + esp.G + "|" + esp.O + ")"); continue; }
                if (v != null && D(v) == D(n)) igualesD++; else if (v == null) nuevasL++; else if (idx.ContainsKey(k)) movidas++; else corridas++;
                if (v != null && v.DispDesc != n.DispDesc) descs.Add(k);
            }
            Ok(malD.Count == 0 && movidas + nuevasL == lista.Count && nuevasL == 1 && igualesD == 0 && lista.All(x => pN.ContainsKey(x.Prop)),
               "W6 los cambios de [Display] de la 4.1.5 a la nueva son exactamente los del script: " + movidas + " casillas movidas al grupo de arriba y " + nuevasL + " nueva ya en el (nombre corto, Order = indice), " + corridas
               + " ajustes con el Order + 1000, " + igualesD + " iguales; distintos de lo esperado " + malD.Count, string.Join("; ", malD.Take(8)));
            P("  (informativo) descripciones que cambiaron: " + (descs.Count == 0 ? "ninguna" : descs.Count + " (" + string.Join(", ", descs.Take(12)) + (descs.Count > 12 ? ", ..." : "") + ")"));

            // 4.1.5e: W7 el script es la fuente de verdad (el codigo coincide y es idempotente) y W8 la primera frase de cada descripcion del grupo
            P(); P("== 4. 4.1.5e: el script manda y cada casilla dice que llave necesita");
            try
            {
                var psi = new ProcessStartInfo("python", "-I \"" + script + "\" --verificar")
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
                using var pr = Process.Start(psi);
                string so = pr.StandardOutput.ReadToEnd(), se = pr.StandardError.ReadToEnd();
                pr.WaitForExit(120000);
                string ult = (so ?? "").Trim().Split('\n').LastOrDefault()?.Trim() ?? "";
                Ok(pr.ExitCode == 0 && ult.StartsWith("sin cambios", StringComparison.Ordinal), "W7 python -I grupo_arriba_415e.py --verificar: '" + ult + "' (el codigo ya es el de la LISTA; una segunda pasada no cambia nada)",
                   "exit " + pr.ExitCode + " " + (so + se).Trim().Replace("\r", "").Replace("\n", " | "));
            }
            catch (Exception e) { Ok(false, "W7 correr python -I grupo_arriba_415e.py --verificar", e.GetType().Name + ": " + e.Message); }
            var aperturas = new[] { "Llave: ", "Necesita la llave '", "Necesita alguna de las llaves '", "No necesita ninguna llave" };
            var sinFrase = lista.Where(x => !pN.TryGetValue(x.Prop, out var n) || n.DispDesc == null || !aperturas.Any(a => n.DispDesc.StartsWith(a, StringComparison.Ordinal))).Select(x => x.Prop).ToList();
            int nLlave = lista.Count(x => pN.TryGetValue(x.Prop, out var n) && (n.DispDesc ?? "").StartsWith("Llave: ", StringComparison.Ordinal));
            int nDep = lista.Count(x => pN.TryGetValue(x.Prop, out var n) && (n.DispDesc ?? "").StartsWith("Necesita ", StringComparison.Ordinal));
            Ok(sinFrase.Count == 0 && nLlave == 9 && lista.Count(x => x.Nombre.StartsWith("↳ ", StringComparison.Ordinal)) == nDep,
               "W8 las " + lista.Count + " del grupo empiezan su descripcion diciendo que llave necesitan: " + nLlave + " llaves ('Llave:'), " + nDep + " '↳' ('Necesita ...'), "
               + (lista.Count - nLlave - nDep) + " sin llave ('No necesita ninguna llave')", string.Join(", ", sinFrase.Take(10)));
        }

        static string Corto(string s) => s == null ? "null" : s.Length <= 90 ? s : s.Substring(0, 90) + "...";
    }
}
