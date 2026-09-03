using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

// Vuelca la superficie publica de los ensamblados de ATAS para poder escribir
// el indicador contra la API real y no contra una suposicion.
class Program
{
    const string Dir = @"C:\Program Files (x86)\ATAS Platform";
    static AssemblyLoadContext _ctx;

    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--v") { _v = true; args = args.Skip(1).ToArray(); }
        _ctx = new AssemblyLoadContext("atas", true);
        _ctx.Resolving += (c, n) =>
        {
            var p = Path.Combine(Dir, n.Name + ".dll");
            return File.Exists(p) ? c.LoadFromAssemblyPath(p) : null;
        };

        foreach (var f in new[] { "ATAS.Indicators", "ATAS.Types", "OFT.Rendering",
                                  "OFT.Attributes", "OFT.Localization", "ATAS.DataFeedsCore",
                                  "ATAS.Indicators.Technical", "ATAS.Indicators.Other" })
            try { _ctx.LoadFromAssemblyPath(Path.Combine(Dir, f + ".dll")); } catch { }

        // Quien IMPLEMENTA una interfaz. Sirve para contestar si el conector
        // de Rithmic sabe servir opciones sin tener que abrir la plataforma.
        if (args.Length > 1 && args[0] == "--impl")
        {
            foreach (var f in new[] { "OFT.Rithmic", "OFT.IQFeed", "OFT.DxFeed",
                                      "ADataFeeder8", "ATAS.Strategies" })
                try { _ctx.LoadFromAssemblyPath(Path.Combine(Dir, f + ".dll")); } catch { }
            var pat = args[1];
            foreach (var a in _ctx.Assemblies)
            {
                Type[] ts;
                try { ts = a.GetTypes(); }
                catch (ReflectionTypeLoadException e) { ts = e.Types.Where(t => t != null).ToArray(); }
                catch { continue; }
                foreach (var t in ts)
                {
                    if (t == null) continue;
                    try
                    {
                        foreach (var i in t.GetInterfaces())
                            if (i.Name.Contains(pat, StringComparison.OrdinalIgnoreCase))
                            { Console.WriteLine($"{a.GetName().Name,-22} {t.FullName}  ->  {i.Name}"); break; }
                    }
                    catch { }
                }
            }
            return;
        }

        if (args.Length == 0 || args[0] == "--tipos")
        {
            var pat = args.Length > 1 ? args[1] : "";
            foreach (var a in _ctx.Assemblies)
                foreach (var t in Seguro(a).Where(t => t.FullName.Contains(pat, StringComparison.OrdinalIgnoreCase))
                                           .OrderBy(t => t.FullName))
                    Console.WriteLine(t.FullName);
            return;
        }

        foreach (var nombre in args) Volcar(nombre);
    }

    static Type[] Seguro(Assembly a)
    {
        try { return a.GetExportedTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); }
        catch { return Array.Empty<Type>(); }
    }

    static void Volcar(string nombre)
    {
        Type t = _ctx.Assemblies.SelectMany(Seguro)
                     .FirstOrDefault(x => x.FullName == nombre || x.Name == nombre);
        if (t == null) { Console.WriteLine($"!! no encontrado: {nombre}"); return; }

        if (t.IsEnum)
        {
            Console.WriteLine($"### ENUM {t.FullName}");
            foreach (var v in Enum.GetValues(t))
                Console.WriteLine($"  {Convert.ToInt64(v),4} = {v}");
            return;
        }
        Console.WriteLine($"### {t.FullName}  : {t.BaseType?.Name}");
        foreach (var i in t.GetInterfaces()) Console.WriteLine($"    impl {i.Name}");

        const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic
                             | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var tt = t;
        int nivel = 0;
        while (tt != null && tt != typeof(object) && nivel < 4)
        {
            if (nivel > 0) Console.WriteLine($"  --- heredado de {tt.Name} ---");
            foreach (var p in tt.GetProperties(F).Where(p => !p.Name.StartsWith("_")).OrderBy(p => p.Name))
                Console.WriteLine($"  prop {Corto(p.PropertyType)} {p.Name}");
            foreach (var m in tt.GetMethods(F)
                         .Where(m => !m.IsSpecialName && (m.IsPublic || m.IsFamily))
                         .OrderBy(m => m.Name))
                Console.WriteLine($"  metodo {Corto(m.ReturnType)} {m.Name}("
                    + string.Join(", ", m.GetParameters().Select(x => Corto(x.ParameterType) + " " + x.Name)) + ")");
            foreach (var f2 in tt.GetFields(F).Where(x => x.IsPublic))
                Console.WriteLine($"  campo {Corto(f2.FieldType)} {f2.Name}"
                    + (f2.IsLiteral ? " = " + f2.GetRawConstantValue() : ""));
            foreach (var c in tt.GetConstructors(F).Where(c => c.IsPublic || c.IsFamily))
                Console.WriteLine($"  ctor ({string.Join(", ", c.GetParameters().Select(x => Corto(x.ParameterType) + " " + x.Name))})");
            tt = tt.BaseType; nivel++;
        }
    }

    static bool _v = false;
    static string Corto(Type t)
    {
        if (t == null) return "?";
        if (_v && !t.IsGenericType) return t.FullName ?? t.Name;
        if (!t.IsGenericType) return t.Name;
        return t.Name.Split('`')[0] + "<" + string.Join(",", t.GetGenericArguments().Select(Corto)) + ">";
    }
}
