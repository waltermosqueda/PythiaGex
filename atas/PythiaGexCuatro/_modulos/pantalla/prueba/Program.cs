// Program.cs — arnes de la pantalla (B5) de PythiaGex 4.1. dotnet run -c Release (desde esta carpeta). Salida 0 = todo verde.
// Que prueba:
//   A. Propiedades: los MISMOS nombres, tipos, Display (nombre, grupo, orden) y defaults que el visor 4.0.6 (lee su .cs del respaldo).
//   B. Catalogo de respaldo de la pantalla = CatalogoFamilia de B3d (id, corto, libro, fuente, tipo, grupo, color, banda) y casillas.
//   C. Armado sobre una FotoFamilia doble: rayitas, etiquetas (4.1.2: LIBRO ROL MONTO [CAMBIO] PRECIO FUENTE, sin "+N"), edad arriba de la
//      columna (DATO DE HACE si > 30 min), pestaña, detalle, doble eje (cada marca en GetYByPrice de su conversion), control de vencimiento,
//      carteles, motor parado, robustez. Desde C53: formato del monto, cabeza por |monto|, cambio (solo vol/oi, nunca con OI viejo), colores
//      y posicion de los tramos, ancho, flechas fuera de pantalla con monto, valores enormes, ajustes apagados, leyenda y detalle.
//      Desde C76 (revision 4.1.2): ▲/▼ por magnitud y ♦ cuando el neto da vuelta (dato real MAJORS_NQ_oi 31.000 10-09 01:49Z), niveles sin lado
//      anotados como CambiosFamilia (C72), la pestaña partida en el ancho visible del grafico del operador (852 px, clip 781), Partir, prefijo.
//   D. PantallaFamilia + RenderContext doble: lo armado se pinta 1 a 1, el clic abre/cierra la pestaña, catalogo del motor = respaldo.
//   E. Rendimiento del armado y del pintado (velas visibles x series).
//   T. (4.1.4) Rotulos de los tramos de historia: deteccion (mismo precio, huecos de hasta 3 velas, >= 15 velas), el vigente no, juntar, absorber
//      (la misma raya que sigue), sin choques ni sobre la columna/pestaña, tope 14, apagado, nombres; y la historia REAL de esta noche (T12).
//   R. (4.1.4) Arreglos de la revision de la 4.1.3: series 2.0 tapadas en un grupo, origen real de la conversion de las replicas, color de "2.0 QQQ".
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using PythiaGexCuatro;
using PythiaGexCuatro.Familia;

namespace PruebaPantalla
{
    static class Prueba
    {
        const string ATAS = @"C:\Program Files (x86)\ATAS Platform";
        static readonly long EPOCA = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        public static long Ms(DateTime t) => (t.Ticks - EPOCA) / TimeSpan.TicksPerMillisecond;
        /// <summary>4.1.2: ancho por letra calibrado. Consolas avanza 0,5498 em en TODAS sus letras (medido con GlyphTypeface, ver C60) y ATAS
        /// toma el tamaño en puntos (96 ppp: 1 pt = 4/3 px): 0,5498 x 4/3 = 0,733 x tam. La captura del 08-10 22:16 dio ~5,9 px por letra a
        /// tamC 8 (0,74 x tam). Antes 0,62 (subestimaba ~16 %).</summary>
        public const double ANCHO_LETRA = 0.74;
        public static Size Medir(string s, float tam) => new Size((int)Math.Ceiling((s?.Length ?? 0) * tam * ANCHO_LETRA), (int)Math.Ceiling(tam * 1.45));
        static int _ok, _mal;
        static readonly List<string> _fallas = new List<string>();

        static void Ok(bool c, string que, string detalle = "")
        {
            if (c) { _ok++; Console.WriteLine("  ok   " + que); }
            else { _mal++; _fallas.Add(que + (detalle == "" ? "" : " -> " + detalle)); Console.WriteLine("  MAL  " + que + (detalle == "" ? "" : " -> " + detalle)); }
        }

        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] static extern bool SetPriorityClass(IntPtr h, uint prio);

        static int Main(string[] args)
        {
            try { SetPriorityClass(GetCurrentProcess(), 0x4000); } catch { }        // BELOW_NORMAL: el mercado esta abierto y ATAS corre
            AssemblyLoadContext.Default.Resolving += (ctx, an) =>
            {   // las DLL de ATAS se LEEN de su carpeta (no se copian ni se tocan)
                var p = Path.Combine(ATAS, an.Name + ".dll");
                return File.Exists(p) ? ctx.LoadFromAssemblyPath(p) : null;
            };
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            // 4.1.0: el log de la pantalla del arnes va a una carpeta temporal (antes escribia en el REAL, %APPDATA%\ATAS\pythiagex4-pantalla.log)
            BitacoraPantalla.Ruta = Path.Combine(Path.GetTempPath(), "pythiagex4_prueba_pantalla", "pythiagex4-pantalla.log");
            Console.WriteLine("Arnes pantalla B5 — " + PantallaFamilia.VERSION + " (log de prueba: " + BitacoraPantalla.Ruta + ")");
            if (args.Length > 0 && args[0] == "--solo")      // 4.1.4: para medir una seccion sola (T, R o E)
            {
                foreach (var a in args.Skip(1))
                    try { if (a == "T") PruebaTramos(); else if (a == "R") PruebaRevision413(); else if (a == "E") PruebaRendimiento(); } catch (Exception e) { Ok(false, a + ": excepcion", e.ToString()); }
                Console.WriteLine("RESULTADO (parcial): " + _ok + " ok, " + _mal + " mal");
                return _mal == 0 ? 0 : 1;
            }
            try { PruebaPropiedades(); } catch (Exception e) { Ok(false, "A. propiedades: excepcion", e.ToString()); }
            try { PruebaCatalogo(); } catch (Exception e) { Ok(false, "B. catalogo: excepcion", e.ToString()); }
            try { PruebaArmado(); } catch (Exception e) { Ok(false, "C. armado: excepcion", e.ToString()); }
            try { PruebaMontos(); } catch (Exception e) { Ok(false, "C53+. montos y cambios: excepcion", e.ToString()); }
            try { PruebaFueraDePantalla(); } catch (Exception e) { Ok(false, "C80. fuera de pantalla: excepcion", e.ToString()); }
            try { PruebaReplica20(); } catch (Exception e) { Ok(false, "C90. dominantes como la 2.0: excepcion", e.ToString()); }
            try { PruebaTramos(); } catch (Exception e) { Ok(false, "T. rotulos de los tramos de historia: excepcion", e.ToString()); }
            try { PruebaRevision413(); } catch (Exception e) { Ok(false, "R. revision 4.1.3: excepcion", e.ToString()); }
            try { PruebaPintado(); } catch (Exception e) { Ok(false, "D. pintado: excepcion", e.ToString()); }
            try { PruebaRendimiento(); } catch (Exception e) { Ok(false, "E. rendimiento: excepcion", e.ToString()); }
            Console.WriteLine();
            Console.WriteLine("RESULTADO: " + _ok + " ok, " + _mal + " mal");
            foreach (var f in _fallas) Console.WriteLine("  MAL " + f);
            return _mal == 0 ? 0 : 1;
        }

        static string RaizProyecto()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "PythiaGexCuatro.csproj"))) d = d.Parent;
            return d?.FullName;
        }

        // ------------------------------------------------------------------ A
        static void PruebaPropiedades()
        {
            Console.WriteLine("A. propiedades contra el visor 4.0.6");
            var raiz = RaizProyecto();
            Ok(raiz != null, "A0 encuentro la raiz del proyecto (PythiaGexCuatro.csproj)");
            var src = File.ReadAllText(Path.Combine(raiz, "_visor_4_0_6", "FamiliaCuatro.cs"));
            // [Display(Name = "...", GroupName = "...", Order = N...)] public <tipo> <Nombre> { get ...} = <default>;
            var re = new Regex(@"\[Display\(Name = ""(?<n>[^""]*)"", GroupName = ""(?<g>[^""]*)"", Order = (?<o>\d+)[^\]]*\]\s*(?:\[[^\]]*\]\s*)*public (?<t>\w+) (?<p>\w+) \{(?<cuerpo>[^}]*)\}(?:\s*=\s*(?<d>[^;]+);)?", RegexOptions.Singleline);
            var vis = re.Matches(src).Cast<Match>().ToList();
            var tipo = typeof(FamiliaCuatro);
            Ok(vis.Count >= 36, "A1 el visor 4.0.6 declara " + vis.Count + " ajustes con Display (29 casillas + 7 de pantalla + Carpeta4)");
            object inst = null; string porQue = "";
            try { inst = Activator.CreateInstance(tipo); } catch (Exception e) { porQue = (e.InnerException ?? e).GetType().Name + ": " + (e.InnerException ?? e).Message; }
            Console.WriteLine("     instancia de FamiliaCuatro fuera de ATAS: " + (inst != null ? "si (se comparan los defaults en vivo)" : "no (" + porQue + "): defaults por el codigo fuente"));
            var mio = File.ReadAllText(Path.Combine(raiz, "_modulos", "pantalla", "FamiliaCuatroPantalla.cs"));
            var misDef = re.Matches(mio).Cast<Match>().ToDictionary(m => m.Groups["p"].Value, m => m.Groups["d"].Value.Trim());
            // 4.1.4 (pedido del operador 09-10): estos defaults del visor 4.0.6 (true) pasan a false SIN renombrar (su .ws ya los tiene en false)
            var cambiados414 = new[] { "S_MAJORS_QQQ_oi", "S_MUROS_QQQ_oi", "S_FAM_MUROS_oi", "S_ZEST_QQQ_vol" };   // + QQQ 0G (pedido del operador 09-10 ~03:05)
            int iguales = 0;
            foreach (var m in vis)
            {
                string p = m.Groups["p"].Value, t = m.Groups["t"].Value, dflt = m.Groups["d"].Value.Trim();
                var pi = tipo.GetProperty(p, BindingFlags.Public | BindingFlags.Instance);
                if (pi == null) { Ok(false, "A2 existe la propiedad " + p); continue; }
                string tn = pi.PropertyType == typeof(bool) ? "bool" : pi.PropertyType == typeof(int) ? "int" : pi.PropertyType == typeof(string) ? "string" : pi.PropertyType.Name;
                bool okTipo = tn == t && pi.CanRead && pi.CanWrite;
                if (p == "EjeFamilia4" || t == "EjeFamilia4") okTipo = pi.PropertyType.FullName == "PythiaGexCuatro.EjeFamilia4" && pi.CanWrite;
                if (!okTipo) { Ok(false, "A2 tipo de " + p, tn + " vs " + t); continue; }
                if (p == "Carpeta4")
                {   // oculta y sin efecto en la 4.1 (todo adentro); solo tiene que existir con el mismo tipo
                    var br = pi.GetCustomAttribute<BrowsableAttribute>();
                    Ok(br != null && !br.Browsable, "A3 Carpeta4 existe (string) y queda oculta: el valor guardado en el .ws tiene donde caer");
                    continue;
                }
                var da = pi.GetCustomAttribute<DisplayAttribute>();
                bool okDisp = da != null && da.Name == m.Groups["n"].Value && da.GroupName == m.Groups["g"].Value && da.Order.ToString() == m.Groups["o"].Value;
                if (!okDisp) { Ok(false, "A4 Display de " + p, (da == null ? "sin Display" : da.Name + "|" + da.GroupName + "|" + da.Order) + " vs " + m.Groups["n"].Value + "|" + m.Groups["g"].Value + "|" + m.Groups["o"].Value); continue; }
                if (p == "Recuadro4Abierto") { iguales++; continue; }            // respaldado por el campo de estado (arranca cerrado), igual que la 4.0.6
                string mioD = misDef.TryGetValue(p, out var x) ? x : "?";
                string esperado = cambiados414.Contains(p) ? "false" : dflt;           // 4.1.4: los tres apagados a pedido
                bool okDef = mioD == esperado;
                if (inst != null) { var vv = pi.GetValue(inst); okDef &= string.Equals(Convert.ToString(vv, CultureInfo.InvariantCulture), esperado.Replace("EjeFamilia4.", ""), StringComparison.OrdinalIgnoreCase); }
                if (!okDef) { Ok(false, "A5 default de " + p, mioD + " vs " + esperado + (esperado != dflt ? " (4.0.6: " + dflt + ")" : "")); continue; }
                iguales++;
            }
            Ok(iguales == vis.Count - 1, "A6 " + iguales + " de " + (vis.Count - 1) + " ajustes con el mismo nombre, tipo, Display y default que la 4.0.6 (4.1.4: salvo los defaults de "
               + string.Join(", ", cambiados414) + ", apagados a pedido sin renombrar)");
            var esperadas = new HashSet<string>(vis.Select(m => m.Groups["p"].Value));
            var blanca412 = new[] { "Monto41Rotulos", "Cambio41Rotulos", "Cambio41Ventana" };     // 4.1.2: los unicos ajustes nuevos (nombres que ningun .ws tiene)
            var blanca413 = new[] { "S_R20_QQQ_vol", "S_R20_NDX_vol", "S_DOMS_QQQ_vol", "S_DOMS_NDX_vol" };   // 4.1.3: las 4 casillas de las dominantes como la 2.0
            var blanca414 = new[] { "Tramos41Rotulos" };                                                      // 4.1.4: los rotulos de los tramos de historia
            var nuevas = tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                             .Where(p => p.CanWrite && p.GetCustomAttribute<DisplayAttribute>() != null && !esperadas.Contains(p.Name)).Select(p => p.Name).ToList();
            Ok(nuevas.OrderBy(x => x).SequenceEqual(blanca412.Concat(blanca413).Concat(blanca414).OrderBy(x => x)), "A7 la pantalla agrega a ATAS solo los 3 ajustes de la 4.1.2 (" + string.Join(", ", blanca412)
               + "), las 4 casillas de la 4.1.3 (" + string.Join(", ", blanca413) + ") y el ajuste de la 4.1.4 (" + string.Join(", ", blanca414) + ")", string.Join(",", nuevas));
            // A17 (4.1.4) el ajuste nuevo: grupo "6. Pantalla", orden 28 (entre Cambio41Ventana y Cabecera4), bool, prendido, con descripcion
            {
                var pi = tipo.GetProperty("Tramos41Rotulos", BindingFlags.Public | BindingFlags.Instance); var da = pi?.GetCustomAttribute<DisplayAttribute>();
                bool ok = pi != null && pi.PropertyType == typeof(bool) && pi.CanRead && pi.CanWrite && da != null && da.Name == "Rotulos de los tramos de historia" && da.GroupName == "6. Pantalla"
                          && da.Order == 28 && !string.IsNullOrEmpty(da.Description) && misDef.TryGetValue("Tramos41Rotulos", out var df) && df == "true";
                if (ok && inst != null) ok &= Convert.ToString(pi.GetValue(inst), CultureInfo.InvariantCulture) == "True";
                Ok(ok, "A17 Tramos41Rotulos (bool, true, 'Rotulos de los tramos de historia', '6. Pantalla', orden 28, con descripcion): nombre NUEVO, ningun .ws lo tiene");
            }
            // A18 (4.1.4) las 5 casillas que pidio apagar el operador ("suman ruido al ser ya superadas por la formula de la 2.0 qqq"): default false en el codigo
            {
                var apagar = new[] { "S_MUROS_QQQ_vol", "S_MUROS_QQQ_oi", "S_MAJORS_QQQ_oi", "S_DOMS_QQQ_vol", "S_FAM_MUROS_oi", "S_ZEST_QQQ_vol" };
                var mal = apagar.Where(pn => !(misDef.TryGetValue(pn, out var df) && df == "false") || (inst != null && Convert.ToString(tipo.GetProperty(pn).GetValue(inst), CultureInfo.InvariantCulture) != "False")).ToList();
                Ok(mal.Count == 0 && apagar.All(pn => SeriesPantalla.Casillas.Any(c => c.Propiedad == pn && !c.Default)),
                   "A18 apagadas por defecto (codigo, instancia y SeriesPantalla): " + string.Join(", ", apagar) + " (S_MUROS_QQQ_vol ya lo estaba)", string.Join(",", mal));
            }
            // A13 los tres ajustes nuevos: grupo "6. Pantalla", orden 22/24/26 (entre Rotulos4 y Cabecera4), tipo y default
            {
                var esp = new (string P, Type T, int O, string Def)[] { ("Monto41Rotulos", typeof(bool), 22, "true"), ("Cambio41Rotulos", typeof(bool), 24, "true"),
                                                                        ("Cambio41Ventana", typeof(VentanaCambio41), 26, "VentanaCambio41.M15") };
                int okN = 0; var mal = new List<string>();
                foreach (var (pn, tt, oo, dd) in esp)
                {
                    var pi = tipo.GetProperty(pn, BindingFlags.Public | BindingFlags.Instance); var da = pi?.GetCustomAttribute<DisplayAttribute>();
                    bool ok = pi != null && pi.PropertyType == tt && pi.CanRead && pi.CanWrite && da != null && da.GroupName == "6. Pantalla" && da.Order == oo
                              && !string.IsNullOrEmpty(da.Name) && !string.IsNullOrEmpty(da.Description) && misDef.TryGetValue(pn, out var df) && df == dd;
                    if (ok && inst != null) ok &= Convert.ToString(pi.GetValue(inst), CultureInfo.InvariantCulture) == dd.Replace("VentanaCambio41.", "").Replace("true", "True");
                    if (ok) okN++; else mal.Add(pn);
                }
                Ok(okN == 3, "A13 Monto41Rotulos (bool, true, 22), Cambio41Rotulos (bool, true, 24), Cambio41Ventana (VentanaCambio41, M15, 26) en '6. Pantalla'",
                   string.Join(",", mal));
                Ok(Enum.GetNames(typeof(VentanaCambio41)).SequenceEqual(new[] { "M5", "M15", "M30" }) && CambiosVentanas.Min.SequenceEqual(new[] { 5, 15, 30 }),
                   "A14 VentanaCambio41 { M5, M15, M30 } = CambiosVentanas.Min { 5, 15, 30 } del contrato");
            }
            // casillas de SeriesPantalla = propiedades S_*
            int okC = 0;
            foreach (var c in SeriesPantalla.Casillas)
            {
                var pi = tipo.GetProperty(c.Propiedad); var da = pi?.GetCustomAttribute<DisplayAttribute>();
                var mm = vis.FirstOrDefault(v => v.Groups["p"].Value == c.Propiedad);
                // 4.1.3: las casillas nuevas no estan en el visor 4.0.6: su default es el del codigo de la 4.1.3 (y el de la instancia, si se pudo crear)
                string dflt = mm != null ? mm.Groups["d"].Value.Trim() : (blanca413.Contains(c.Propiedad) && misDef.TryGetValue(c.Propiedad, out var d413) ? d413 : "?");
                if (cambiados414.Contains(c.Propiedad)) dflt = "false";                    // 4.1.4: apagadas a pedido
                bool okVivo = inst == null || pi == null || Convert.ToString(pi.GetValue(inst), CultureInfo.InvariantCulture) == (c.Default ? "True" : "False");
                if (pi != null && da != null && da.Name == c.Nombre && da.GroupName == c.Grupo && da.Order == c.Orden && dflt == (c.Default ? "true" : "false") && okVivo) okC++;
                else Ok(false, "A8 casilla " + c.Id + " = su propiedad");
            }
            Ok(okC == 33 && SeriesPantalla.Casillas.Count == 33, "A8 las 33 casillas de SeriesPantalla (29 + 4 de la 4.1.3) coinciden con sus propiedades S_* (nombre, grupo, orden, default)");
            Ok(SeriesPantalla.Casillas.Where(c => c.Default).Select(c => c.Id).OrderBy(x => x).SequenceEqual(SeriesPantalla.PrendidasPorDefecto.OrderBy(x => x)) && SeriesPantalla.PrendidasPorDefecto.Length == 6
               && SeriesPantalla.PrendidasPorDefecto.OrderBy(x => x).SequenceEqual(new[] { "MUROS_NQ_oi", "MUROS_NDX_vol", "TRES_NDX", "T_MUROS_oi", "R20_QQQ_vol", "R20_NDX_vol" }.OrderBy(x => x)),
               "A9 prendidas por defecto (4.1.4: sin QQQ majors OI, QQQ muros OI, FAM muros OI, QQQ dom ni QQQ 0G): " + string.Join(", ", SeriesPantalla.PrendidasPorDefecto));
            // A16 (4.1.3) las 4 casillas nuevas: grupo "4b. Dominantes como la 2.0", orden 10/20/30/40, con descripcion; NDX dom apagada (sin medir)
            {
                var esp = new (string P, int O, bool Def)[] { ("S_R20_QQQ_vol", 10, true), ("S_R20_NDX_vol", 20, true), ("S_DOMS_QQQ_vol", 30, false), ("S_DOMS_NDX_vol", 40, false) };
                int okN = 0; var mal = new List<string>();
                foreach (var (pn, oo, dd) in esp)
                {
                    var pi = tipo.GetProperty(pn, BindingFlags.Public | BindingFlags.Instance); var da = pi?.GetCustomAttribute<DisplayAttribute>();
                    bool ok = pi != null && pi.PropertyType == typeof(bool) && da != null && da.GroupName == SeriesPantalla.GRUPO_20 && da.GroupName == "4b. Dominantes como la 2.0" && da.Order == oo
                              && !string.IsNullOrEmpty(da.Description) && misDef.TryGetValue(pn, out var df) && df == (dd ? "true" : "false");
                    if (ok) okN++; else mal.Add(pn);
                }
                Ok(okN == 4, "A16 las 4 casillas de la 4.1.3 en '4b. Dominantes como la 2.0' (orden 10/20/30/40, con descripcion; 2.0 QQQ y 2.0 NDX prendidas; QQQ dom apagada desde la 4.1.4; NDX dom apagada)", string.Join(",", mal));
            }
            if (inst is FamiliaCuatro fc)
            {
                var a = new AjustesPantalla(); ((IGraficoPantalla)fc).Ajustes(a);
                Ok(a.Visibles.OrderBy(x => x).SequenceEqual(SeriesPantalla.PrendidasPorDefecto.OrderBy(x => x)) && a.Eje == EjeFamilia4.TQQQ && a.Estela && a.Rotulos && a.Cabecera && !a.PanelAbierto && a.Letra == 9 && a.MargenSup == 56
                   && a.Montos && a.Cambios && a.Ventana == VentanaCambio41.M15 && a.VentanaMin == 15 && a.Tramos,
                   "A10 IGraficoPantalla.Ajustes de una instancia recien creada = defaults del operador (4.1.2: montos y cambios prendidos, ventana 15 min; 4.1.4: rotulos de tramos prendidos)");
                fc.Tramos41Rotulos = false; ((IGraficoPantalla)fc).Ajustes(a);
                Ok(!a.Tramos && PantallaFamilia.Resumen(a).EndsWith(" tramos=False"), "A19 Tramos41Rotulos llega a AjustesPantalla y al resumen del log ('tramos=False')");
                fc.Tramos41Rotulos = true;
                fc.S_CONF_vol = true; fc.S_TRES_NDX = false; ((IGraficoPantalla)fc).Ajustes(a);
                Ok(a.Visible("CONF_vol") && !a.Visible("TRES_NDX"), "A11 SerieVisible4 sigue a las casillas (CONF prendida, TRES_NDX apagada)");
                fc.Monto41Rotulos = false; fc.Cambio41Rotulos = false; fc.Cambio41Ventana = VentanaCambio41.M30; ((IGraficoPantalla)fc).Ajustes(a);
                string res = PantallaFamilia.Resumen(a);
                Ok(!a.Montos && !a.Cambios && a.VentanaMin == 30 && res.Contains("montos=False cambios=False ventana=30 min"),
                   "A15 los ajustes 4.1.2 llegan a AjustesPantalla y al resumen del log: '" + res.Substring(Math.Max(0, res.Length - 44)) + "'");
                fc.Monto41Rotulos = true; fc.Cambio41Rotulos = true; fc.Cambio41Ventana = VentanaCambio41.M15;
                Console.WriteLine("     log de arranque: " + fc.AjustesPantalla4Texto());
                Ok(fc._pantalla != null && ReferenceEquals(fc._pantalla, fc._pantalla), "A12 _pantalla se crea una vez (gancho de B1)");
            }
        }

        // ------------------------------------------------------------------ B
        static void PruebaCatalogo()
        {
            Console.WriteLine("B. catalogo de respaldo contra CatalogoFamilia (B3d)");
            var mot = CatalogoFamilia.Series; var res = SeriesPantalla.Respaldo;
            Ok(mot.Count == 33 && res.Count == 33, "B1 33 series en los dos (29 + 4 de la 4.1.3) (" + mot.Count + "/" + res.Count + ")");
            int igual = 0;
            for (int i = 0; i < Math.Min(mot.Count, res.Count); i++)
            {
                var a = mot[i]; var b = res[i];
                bool ok = a.Id == b.Id && a.Corto == b.Corto && a.Libro == b.Libro && a.Fuente == b.Fuente && a.Tipo == b.Tipo && a.Grupo == b.Grupo && a.ColorHex == b.ColorHex && a.Banda == b.Banda;
                if (ok) igual++; else Ok(false, "B2 serie " + i + " " + a.Id + " vs " + b.Id);
            }
            Ok(igual == 33, "B2 mismo orden y mismos id/corto/libro/fuente/tipo/grupo/color/banda en las 33");
            Ok(SeriesPantalla.Casillas.Select(c => c.Id).OrderBy(x => x).SequenceEqual(mot.Select(s => s.Id).OrderBy(x => x)), "B3 una casilla por serie del catalogo del motor");
            Ok(CatalogoFamilia.PrendidasPorDefecto.OrderBy(x => x).SequenceEqual(SeriesPantalla.PrendidasPorDefecto.OrderBy(x => x)), "B4 mismos defaults que CatalogoFamilia.PrendidasPorDefecto");
            var cp = new CatalogoPantalla(new[] { new SerieInfo { Id = "MUROS_NQ_vol", ColorHex = "#010203", Corto = "NQ*", Libro = "NQ", Tipo = "MUROS" } });
            Ok(cp.Lista.Count == 33 && cp.PorId["MUROS_NQ_vol"].Corto == "NQ*" && cp.Colores["MUROS_NQ_vol"] == Color.FromArgb(1, 2, 3) && cp.DelMotor,
               "B5 el catalogo del motor manda y el respaldo completa lo que falte");
            Ok(SeriesPantalla.ColorDe("zz") == Color.FromArgb(200, 200, 200) && SeriesPantalla.ColorDe(null) == Color.FromArgb(200, 200, 200), "B6 color ilegible -> gris, sin tirar");
        }

        // ------------------------------------------------------------------ C
        static DibujoPantalla Armar(FotoFamilia f, GraficoDoble gr, IReadOnlyList<SerieInfo> cat = null, int xDerecha = 1180)
        {
            var d = new DibujoPantalla(); var aj = new AjustesPantalla(); gr.Ajustes(aj);
            var v = new VistaPantalla { Area = gr.AreaR, XDerecha = xDerecha, Instrumento = gr.Instr, PrecioAlto = gr.Alto, PrecioBajo = gr.Bajo, Y = gr.YDePrecio };
            int desde = Math.Max(0, gr.PrimeraVisible), hasta = Math.Min(gr.BarraActual - 1, gr.UltimaVisible);
            v.AnchoVela = hasta > desde ? Math.Max(1, (gr.XDeBarra(hasta) - gr.XDeBarra(desde)) / (hasta - desde)) : 6;
            for (int b = desde; b <= hasta; b++) if (gr.Vela(b, out long ms, out _)) v.Velas.Add((gr.XDeBarra(b), ms / 120000 * 120000));
            v.UltimaVisible = gr.UltimaVisible >= gr.BarraActual - 1;
            v.PrecioUltimo = gr.Vela(gr.BarraActual - 1, out _, out double c) ? c : double.NaN;
            ArmadoPantalla.Armar(d, f, new CatalogoPantalla(cat), aj, v, gr.Ahora, Medir);
            return d;
        }

        static void PruebaArmado()
        {
            Console.WriteLine("C. armado sobre una FotoFamilia doble");
            var ahora = new DateTime(2026, 10, 8, 19, 0, 0, DateTimeKind.Utc);
            var f = FotosDoble.Normal(ahora);
            var gr = new GraficoDoble(ahora);

            // C1 vencimiento
            Ok(ArmadoPantalla.Venc("MNQZ6") == "Z6" && ArmadoPantalla.Venc("Z6") == "Z6" && ArmadoPantalla.Venc("NQZ26") == "Z6" && ArmadoPantalla.Venc("MNQ Z6") == "Z6"
               && ArmadoPantalla.Venc("MNQZ6.CME") == "Z6" && ArmadoPantalla.Venc("H7") == "H7" && ArmadoPantalla.Venc("MESZ6") == "" && ArmadoPantalla.Venc("") == "" && ArmadoPantalla.Venc(null) == "",
               "C1 Venc: MNQZ6/Z6/NQZ26/'MNQ Z6' -> Z6; H7 -> H7; MESZ6 y vacio -> sin dato");

            var d = Armar(f, gr);
            Ok(d.Cartel == "", "C2 foto normal: sin cartel", d.Cartel);
            Ok(d.Rayitas == 600, "C3 rayitas = 100 velas x 6 niveles de las series prendidas (MUROS_NQ_vol apagada no se dibuja): " + d.Rayitas);
            var rel = d.Prims.Where(p => p.Tipo == TipoPrimPantalla.Relleno && p.R.Height <= 2 && p.R.Width == 9).ToList();
            Ok(rel.All(p => p.R.Y + 1 == gr.YDePrecio(31060) || p.R.Y + 1 == gr.YDePrecio(30950) || p.R.Y + 1 == gr.YDePrecio(31073.31) || p.R.Y + 1 == gr.YDePrecio(31020) || p.R.Y + 1 == gr.YDePrecio(30949.10)),
               "C4 cada rayita cae en el GetYByPrice de su nivel (mapeo lineal = exacto en el doble)");
            Ok(!d.Prims.Any(p => p.Tipo == TipoPrimPantalla.Relleno && p.R.Y + 1 == gr.YDePrecio(31040) && p.R.Height <= 2), "C5 la serie apagada (NQ muros vol 31.040) no deja rayitas");
            var txt = d.Etiquetas.Select(e => e.Txt).ToList();
            Console.WriteLine("     etiquetas: " + string.Join(" | ", txt));
            // 4.1.2: LIBRO ROL MONTO [CAMBIO] PRECIO FUENTE, sin "+N"; en un grupo la etiqueta del de mayor |GexM|
            Ok(txt.Contains("QQQ M+ +109M ▲12M 31.073 OI"), "C6 QQQ majors OI (+108,6, OI ▲12,4) y TQQQ muro C (+3,4) al mismo precio: 'QQQ M+ +109M ▲12M 31.073 OI' (banda -> precio redondeado)");
            Ok(txt.Contains("NQ C +90M 31.060 OI*"), "C7 NDX muro C vol (+0,4, 31.060,50) y NQ muro C OI (+89,7, 31.060) a 0,5 pt: la del mayor monto 'NQ C +90M 31.060 OI*' (OI viejo: sin cambio)");
            Ok(txt.Contains("3.0 NDX D1 31.020"), "C8 TRES_NDX sin monto (sin 'gm'): '3.0 NDX D1 31.020' (la 3.0 no lleva fuente)");
            Ok(txt.Contains("FAM P −511M ▼20M 30.990 OI"), "C9 familia: 'FAM P −511M ▼20M 30.990 OI' (OI +20,3 sobre un muro P de −510,8: se achica)");
            Ok(txt.Contains("NQ P −82M 30.950 OI*"), "C10 NQ muro P OI viejo (*, −81,8) y TQQQ muro P (−2,1) a 0,9 pt: 'NQ P −82M 30.950 OI*'");
            Ok(txt.Contains("↑ QQQ 0G 31.500 V"), "C11 nivel fuera de pantalla (QQQ zero est. 31.500, sin monto): '↑ QQQ 0G 31.500 V'");
            Ok(txt.Contains("NDX P −250M ▲3,1M 30.981,62 V"), "C11b NDX muro P vol (−249,6; vol 15 min −3,1: crece): 'NDX P −250M ▲3,1M 30.981,62 V'");
            Ok(!txt.Any(t => t.Contains("31.040")), "C12 la serie apagada no tiene etiqueta");
            Ok(d.EdadColumna == "dato de hace 16 min", "C13 edad arriba de la columna = la del dato mas viejo (CBOE 16 min): '" + d.EdadColumna + "'");
            int yEdad = d.Prims.Where(p => p.Tipo == TipoPrimPantalla.Texto && p.T == d.EdadColumna).Select(p => p.R.Y).DefaultIfEmpty(int.MaxValue).Min();
            Ok(d.Etiquetas.Where(e => !e.Txt.StartsWith("↑") && !e.Txt.StartsWith("↓")).All(e => e.Y > yEdad), "C14 la edad va ARRIBA de todas las etiquetas (antes de los numeros)");
            var ys = d.Etiquetas.Where(e => !e.Txt.StartsWith("↑") && !e.Txt.StartsWith("↓")).Select(e => e.Y).ToList();
            Ok(ys.Zip(ys.Skip(1), (a, b) => b - a).All(x => x >= Medir("X", 8).Height + 3), "C15 etiquetas sin pisarse (separacion >= alto de letra)");
            Ok(d.Titulo == "PythiaGex 4.0 ▸ · dato de hace 16 min · eje TQQQ x3", "C16 pestaña cerrada: '" + d.Titulo + "'");
            Ok(d.Pestana.X == 3 && d.Pestana.Y == 56, "C17 pestaña arriba a la izquierda con margen 56: " + d.Pestana);
            Ok(d.Panel.Count == 0, "C18 la pestaña arranca cerrada (sin detalle)");
            Ok(d.EjeTitulo == "eje TQQQ x3: NQ = 124,21 x TQQQ + 21.012,1 (no regla de tres) · dato 15 min", "C19 titulo del eje TQQQ: '" + d.EjeTitulo + "'");
            Ok(d.Marcas.Count > 5 && d.Marcas.All(m => m.Y == gr.YDePrecio(124.21 * m.V + 21012.06)), "C20 cada marca del eje TQQQ en GetYByPrice(s*K + c): " + d.Marcas.Count + " marcas, paso "
               + (d.Marcas.Count > 1 ? (d.Marcas[1].V - d.Marcas[0].V).ToString("0.##", CultureInfo.InvariantCulture) : "?"));
            Ok(d.Prims.Count(p => p.Tipo == TipoPrimPantalla.Relleno && p.R.Width > 300 && p.R.Height > 300) == 0, "C21 sin sombreado (ningun relleno grande)");

            // C22 panel abierto
            gr.Abierto = true; var dp = Armar(f, gr); gr.Abierto = false;
            Console.WriteLine("     detalle:"); foreach (var l in dp.Panel) Console.WriteLine("       " + l);
            Ok(dp.Titulo.StartsWith("PythiaGex 4.0 ▾"), "C22 abierta: '▾'");
            Ok(dp.Panel.Contains("NQ Rithmic (40 s) · mismo contrato") && dp.Panel.Contains("NDX CBOE (16 min) · base 241.62") && dp.Panel.Contains("QQQ CBOE (16 min) · razon 41.4331")
               && dp.Panel.Any(l => l.StartsWith("TQQQ CBOE (15 min) · x3")) && dp.Panel.Any(l => l.StartsWith("descarga CBOE: ")) && dp.Panel.Contains("cinta MNQ: ultimo tick hace 2 s"),
               "C23 fuentes con su edad y conversion (NQ Rithmic, NDX/QQQ/TQQQ CBOE, descarga, cinta)");
            int iSep = dp.Panel.IndexOf("— precio MNQ 31.000,25 —");
            Ok(iSep > 0 && dp.Panel[iSep - 1].StartsWith("31.020  3.0 NDX D1") && dp.Panel[iSep + 1].StartsWith("30.990  FAM muro P·OI"), "C24 el precio MNQ separa lo de arriba (31.020) de lo de abajo (30.990)");
            Ok(dp.Panel.Any(l => l.StartsWith("31.500  QQQ 0G est.·vol (strike QQQ 760,2) · 16 min")), "C25 el detalle trae precio (primero), fuente, strike y edad (un zero no tiene monto): '31.500  QQQ 0G est.·vol (strike QQQ 760,2) · 16 min'");
            Ok(dp.Panel.Contains("30.981,62  NDX muro P·vol −250M (strike NDX 30.740) · 16 min"), "C25b el detalle trae el monto pegado al nombre: '30.981,62  NDX muro P·vol −250M (strike NDX 30.740) · 16 min'");
            Ok(dp.Panel.Any(l => l.Contains("NQ muro P·OI (OI 2s)")), "C26 OI de 2 sesiones marcado '(OI 2s)' en el detalle");
            Ok(dp.Panel.Last().StartsWith("sin validar: describe, no anticipa"), "C27 cierra con 'sin validar: describe, no anticipa'");
            Ok(dp.Panel[0].StartsWith("calculado adentro: libro NQ por Rithmic + NDX/QQQ/TQQQ bajados de CBOE"), "C28 dice de donde sale todo (adentro, sin programas externos)");

            // C29 dato viejo (> 30 min): la edad va ANTES de los numeros
            var fv = FotosDoble.Normal(ahora, edadCboeMin: 45);
            var dv = Armar(fv, gr);
            Ok(dv.EdadColumna == "DATO DE HACE 45 min" && dv.Titulo.Contains("· DATO DE HACE 45 min") && dv.ColorTitulo == ArmadoPantalla.ColNaranja, "C29 > 30 min: 'DATO DE HACE 45 min' arriba de la columna y en la pestaña (naranja)");
            gr.Abierto = true; var dvp = Armar(fv, gr); gr.Abierto = false;
            Ok(dvp.Panel.Any(l => l.StartsWith("DATO DE HACE 45 min · NDX CBOE")) && dvp.Panel.Any(l => l.StartsWith("[45 min] 31.073  QQQ M+·OI")), "C30 detalle viejo: la edad adelante ('DATO DE HACE 45 min · NDX CBOE', '[45 min] 31.073  QQQ ...')");

            // C31 motor parado
            var fp = FotosDoble.Copia(f, x => x.CalculadoUtc = ahora.AddMinutes(-10));
            var dpp = Armar(fp, gr);
            Ok(dpp.Titulo.StartsWith("PythiaGex 4.0 ▸ · EL MOTOR NO CALCULA HACE 10 min") && dpp.ColorTitulo == ArmadoPantalla.ColRojo, "C31 motor parado: '" + dpp.Titulo + "' en rojo");

            // C32 aviso del motor
            var fa = FotosDoble.Copia(f, x => x.Aviso = "sin cinta hace 5 min (pestaña oculta?)");
            var da = Armar(fa, gr);
            Ok(da.Titulo.Contains("· sin cinta hace 5 min (pestaña oculta?)") && da.ColorTitulo == ArmadoPantalla.ColNaranja && da.Cartel == "", "C32 el aviso del motor va en la pestaña (naranja), sin tapar el grafico");

            // C33 control de vencimiento
            var fh = FotosDoble.Copia(f, x => x.Instrumento = "H7");
            var dh = Armar(fh, gr);
            Ok(dh.Cartel.StartsWith("PythiaGex 4.0: los niveles son de H7 y este grafico es MNQZ6 (otro vencimiento): no dibujo") && dh.ColorCartel == ArmadoPantalla.ColRojo
               && dh.Rayitas == 0 && dh.Etiquetas.Count == 0 && dh.Pestana.IsEmpty, "C33 otro vencimiento: cartel rojo y nada mas");
            var fz = FotosDoble.Copia(f, x => x.Instrumento = "MNQZ6");
            Ok(Armar(fz, gr).Rayitas == 600, "C34 mismo vencimiento escrito como MNQZ6: dibuja");
            var fs = FotosDoble.Copia(f, x => x.Instrumento = "");
            Ok(Armar(fs, gr).Rayitas == 600, "C35 contrato desconocido en la foto: no bloquea");

            // C36 grafico que no es NQ
            gr.Instr = "MESZ6"; var dm = Armar(f, gr); gr.Instr = "MNQZ6";
            Ok(dm.Cartel.StartsWith("PythiaGex 4.0: los niveles estan en precio de NQ/MNQ; este grafico es MESZ6") && dm.Prims.Count == 2, "C36 grafico de ES: cartel naranja y nada mas");

            // C37 foto null y foto sin niveles
            var dn = Armar(null, gr);
            Ok(dn.Cartel.StartsWith("PythiaGex 4.0: esperando el primer calculo") && dn.Prims.Count == 2, "C37 sin foto: cartel de espera");
            var fe = new FotoFamilia { Sesion = "2026-10-08", CalculadoUtc = ahora.AddSeconds(-2), HistoriaM2 = new Dictionary<long, IReadOnlyDictionary<string, double[]>>(),
                                       Actuales = Array.Empty<NivelActual>(), Fuentes = Array.Empty<FuenteEstado>(), Aviso = "esperando el historico de las fuentes (Rithmic / CBOE)", Instrumento = "Z6" };
            var de = Armar(fe, gr);
            Ok(de.Cartel == "PythiaGex 4.0: esperando el historico de las fuentes (Rithmic / CBOE)" && !de.Pestana.IsEmpty && de.Titulo.Contains("esperando el historico"),
               "C38 sin niveles: cartel con el aviso del motor y la pestaña igual (para abrirla)");
            var fi = new FotoFamilia { CalculadoUtc = DateTime.MinValue, Aviso = "sin configurar", Instrumento = "" };
            var di = Armar(fi, gr);
            Ok(di.Titulo.Contains("sin calcular todavia") && di.Cartel.Contains("sin configurar"), "C39 foto inicial del motor (sin calcular): 'sin calcular todavia'");

            // C40 mirando velas viejas: sin etiquetas de AHORA (las rayitas si)
            gr.Ultima = 250; var dvj = Armar(f, gr); gr.Ultima = 299;
            Ok(dvj.Etiquetas.Count == 0 && dvj.Rayitas == 51 * 6, "C40 mirando velas viejas: sin etiquetas, con rayitas (" + dvj.Rayitas + ")");

            // C41 ejes
            gr.A.Eje = EjeFamilia4.QQQ; var dq = Armar(f, gr);
            Ok(dq.EjeTitulo == "eje QQQ: NQ = QQQ x 41,4331 (razon sincronizada) · dato 16 min" && dq.Marcas.Count > 5 && dq.Marcas.All(m => m.Y == gr.YDePrecio(m.V * 41.4331)) && dq.Titulo.EndsWith("· eje QQQ"),
               "C41 eje QQQ: NQ = QQQ x razon; cada marca en su precio (" + dq.Marcas.Count + " marcas)");
            gr.A.Eje = EjeFamilia4.NDX; var dx = Armar(f, gr);
            Ok(dx.EjeTitulo == "eje NDX: NQ = NDX + 241,62 (base sincronizada) · dato 16 min" && dx.Marcas.Count > 5 && dx.Marcas.All(m => m.Y == gr.YDePrecio(m.V + 241.62)),
               "C42 eje NDX: NQ = NDX + base; cada marca en su precio (" + dx.Marcas.Count + " marcas)");
            var fr = FotosDoble.Copia(f, x => x.Fuentes = f.Fuentes.Select(u => u.Libro == "QQQ" ? new FuenteEstado { Libro = "QQQ", ConvValor = 25, DatoUtc = u.DatoUtc, Texto = u.Texto } : u).ToList());
            gr.A.Eje = EjeFamilia4.QQQ; var dr = Armar(fr, gr);
            Ok(dr.Marcas.Count == 0 && dr.Titulo.EndsWith("· eje QQQ SIN DATO") && dr.EjeTitulo.StartsWith("eje QQQ: sin conversion vigente"), "C43 razon fuera de 30-60: no dibujo el eje y lo digo");
            var ft = FotosDoble.Copia(f, x => { x.TqS = double.NaN; x.TqC = double.NaN; });
            gr.A.Eje = EjeFamilia4.TQQQ; gr.Abierto = true; var dtq = Armar(ft, gr); gr.Abierto = false;
            Ok(dtq.Marcas.Count == 0 && dtq.Panel.Any(l => l.StartsWith("TQQQ sin dato en la sesion 2026-10-08")), "C44 TQQQ sin conversion: sin eje y el detalle lo dice");
            gr.A.Eje = EjeFamilia4.Ninguno; var d0 = Armar(f, gr);
            Ok(d0.Marcas.Count == 0 && d0.EjeTitulo == "" && d0.Titulo == "PythiaGex 4.0 ▸ · dato de hace 16 min", "C45 eje Ninguno: sin marcas ni titulo");
            gr.A.Eje = EjeFamilia4.TQQQ;

            // C46 casillas: estela apagada, etiquetas apagadas, pestaña apagada, letra
            gr.A.Estela = false; Ok(Armar(f, gr).Rayitas == 0, "C46 Estela4 = false: sin rayitas"); gr.A.Estela = true;
            gr.A.Rotulos = false; Ok(Armar(f, gr).Etiquetas.Count == 0, "C47 Rotulos4 = false: sin etiquetas"); gr.A.Rotulos = true;
            gr.A.Cabecera = false; var dc = Armar(f, gr); Ok(dc.Pestana.IsEmpty && dc.Titulo == "", "C48 Cabecera4 = false: sin pestaña (y el clic no se toma)"); gr.A.Cabecera = true;
            gr.A.Visibles.Clear(); gr.A.Visibles.Add("T_DOMS_raz");
            var dsolo = Armar(f, gr);
            Ok(dsolo.Rayitas == 0 && dsolo.Etiquetas.Count == 0, "C49 serie prendida sin datos: nada");
            foreach (var c in SeriesPantalla.Casillas) gr.A.Visibles.Add(c.Id);
            var dtodo = Armar(f, gr);
            Ok(dtodo.Rayitas == 700 && dtodo.Etiquetas.Any(e => e.Txt == "NQ C +47M 31.040 V"), "C50 todas prendidas: aparece la serie que estaba apagada ('NQ C +47M 31.040 V', " + dtodo.Rayitas + " rayitas)");
            gr.A.Visibles.Clear(); foreach (var id in GraficoDoble.Visibles413) gr.A.Visibles.Add(id);

            // C51 robustez: nulos, NaN, catalogo raro
            var fx = new FotoFamilia
            {
                CalculadoUtc = ahora, Instrumento = "Z6", HistoriaM2 = new Dictionary<long, IReadOnlyDictionary<string, double[]>> { [gr.Ahora.Ticks] = null, [0] = new Dictionary<string, double[]> { ["MUROS_NQ_oi"] = null } },
                Actuales = new List<NivelActual> { null, new NivelActual { Serie = "MUROS_NQ_oi", Precio = double.NaN }, new NivelActual { Serie = "MUROS_NQ_oi", Precio = 31000, Libro = "NQ", Rol = null, Fuente = null, Strike = double.NaN } },
                Fuentes = new List<FuenteEstado> { null, new FuenteEstado { Libro = null }, new FuenteEstado { Libro = "NDX", ConvValor = double.NaN } }, TqS = 1e9, TqC = double.NaN
            };
            gr.Abierto = true;
            Exception ex = null; DibujoPantalla dx2 = null;
            try { dx2 = Armar(fx, gr, new List<SerieInfo> { null, new SerieInfo { Id = null }, new SerieInfo { Id = "X", ColorHex = "#zzzzzz" } }); } catch (Exception e) { ex = e; }
            gr.Abierto = false;
            Ok(ex == null && dx2 != null && dx2.Etiquetas.Count == 1, "C51 nulos, NaN y catalogo raro: no tira y dibuja lo valido" + (ex == null ? "" : " -> " + ex.Message));
            var dmx = new DibujoPantalla();
            ArmadoPantalla.Armar(dmx, f, null, new AjustesPantalla(), new VistaPantalla { Area = gr.AreaR, Instrumento = "MNQZ6", Y = null }, ahora, Medir);
            Ok(dmx.Rayitas == 0 && dmx.Etiquetas.Count == 0 && !dmx.Pestana.IsEmpty, "C52 sin mapeo de precio (Y null): no tira, solo la pestaña");
        }

        // ------------------------------------------------------------------ C53+ (4.1.2): montos y cambios en las etiquetas
        static NivelActual A(string serie, string libro, string fuente, string tipo, string rol, double precio, double strike, double banda, DateTime dato, double gexM = double.NaN, bool oiViejo = false)
            => FotosDoble.A(serie, libro, fuente, tipo, rol, precio, strike, banda, dato, oiViejo, gexM);

        /// <summary>C80 (principal, 09-10): las etiquetas fuera de pantalla (↑/↓) tienen renglones propios y no tapan ni son tapadas por las visibles.
        /// Caso real del operador (09-10 00:11): "NDX M+ +30M 31.110,74 V" encima de "NDX C +116M ..." en el renglon de arriba.</summary>
        static void PruebaFueraDePantalla()
        {
            Console.WriteLine("C80. etiquetas fuera de pantalla con renglon propio (sin tapar la primera visible)");
            var ahora = new DateTime(2026, 10, 8, 19, 0, 0, DateTimeKind.Utc);
            DateTime cb = ahora.AddMinutes(-16);
            var f0 = FotosDoble.Normal(ahora);
            var gr = new GraficoDoble(ahora); foreach (var c in SeriesPantalla.Casillas) gr.A.Visibles.Add(c.Id);
            int hC = Medir("X", 8).Height + 3;
            var d0 = Armar(f0, gr); int baseArr = d0.Rotulos.Count(r => r.Fuera > 0), baseAba = d0.Rotulos.Count(r => r.Fuera < 0);   // la foto normal ya trae alguna
            foreach (var (nArr, nAba) in new[] { (1, 0), (2, 0), (0, 1), (2, 2), (3, 3) })
            {
                var extra = new List<NivelActual>();
                for (int i = 0; i < nArr; i++) extra.Add(A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro C", gr.Alto + 20 + 15 * i, 30900 + 10 * i, 0, cb, gexM: 116 + i));
                for (int i = 0; i < nAba; i++) extra.Add(A("MUROS_NDX_oi", "NDX", "oi", "MUROS", "muro P", gr.Bajo - 20 - 15 * i, 30500 - 10 * i, 0, cb, gexM: -80 - i));
                extra.Add(A("MAJORS_NDX_vol", "NDX", "vol", "MAJORS", "M+", gr.Alto - 0.5, 30870, 0, cb, gexM: 30));     // pegada al borde de arriba
                extra.Add(A("MAJORS_NQ_oi", "NQ", "oi", "MAJORS", "M-", gr.Bajo + 0.5, 30800, 0, cb, gexM: -29));      // pegada al borde de abajo
                var d = Armar(FotosDoble.Mas(f0, extra.ToArray()), gr);
                var filas = d.Rotulos.Select(r => r.Fuera == 0 ? (Ini: r.Y - hC / 2, Fin: r.Y - hC / 2 + hC, R: r) : (Ini: r.Y, Fin: r.Y + hC, R: r)).ToList();
                var choques = new List<string>();
                for (int i = 0; i < filas.Count; i++)
                    for (int j = i + 1; j < filas.Count; j++)
                        if (filas[i].Ini < filas[j].Fin && filas[j].Ini < filas[i].Fin && (filas[i].R.Fuera != 0 || filas[j].R.Fuera != 0))
                            choques.Add("'" + filas[i].R.Txt + "' [" + filas[i].Ini + "," + filas[i].Fin + ") con '" + filas[j].R.Txt + "' [" + filas[j].Ini + "," + filas[j].Fin + ")");
                int arr = d.Rotulos.Count(r => r.Fuera > 0), aba = d.Rotulos.Count(r => r.Fuera < 0);
                Ok(choques.Count == 0 && arr == Math.Min(2, nArr + baseArr) && aba == Math.Min(2, nAba + baseAba) && d.Rotulos.Any(r => r.Fuera == 0 && r.Serie == "MAJORS_NDX_vol"),
                   "C80 " + nArr + " arriba / " + nAba + " abajo fuera de pantalla: " + arr + " ↑ y " + aba + " ↓ en renglones propios, ninguna se pisa con una visible", string.Join("; ", choques));
            }
        }

        // ------------------------------------------------------------------ C90+ (4.1.3): las dominantes como la 2.0
        /// <summary>4.1.3: R20_QQQ_vol "2.0 QQQ", R20_NDX_vol "2.0 NDX", DOMS_QQQ_vol "QQQ dom", DOMS_NDX_vol "NDX dom" (rol D1/D2) con los numeros reales del
        /// 09-10 03:00 UTC (la raya de la 2.0 en 31.083,50 = QQQ 750 x 41,4447; el mismo 750 con la razon sincronizada en 31.076,37).</summary>
        static void PruebaReplica20()
        {
            Console.WriteLine("C90+. 4.1.3: las dominantes como la 2.0 (pedido del operador 09-10: 'copia la formula de la 2.0 y agregala')");
            var ahora = new DateTime(2026, 10, 9, 3, 0, 0, DateTimeKind.Utc);
            DateTime cb = new DateTime(2026, 10, 8, 20, 14, 59, DateTimeKind.Utc);       // cadena congelada (ultimo trade 16:14:59 NY)
            const string M = "−";
            var f0 = FotosDoble.Normal(ahora);
            var r20q1 = FotosDoble.ComoCambios(A("R20_QQQ_vol", "QQQ", "vol", "DOMS", "D1", 31083.5, 750, 0, cb, gexM: 417.3));
            r20q1.CambioNota = "replica de la 2.0: sin cambio por nivel (su conversion y su S no son las del libro de la 4.1)";
            r20q1.CambioVolM = new[] { 5.0, 5.0, 5.0 };                                   // aunque viniera un cambio, la replica no lo muestra
            var extra = new[]
            {
                r20q1,
                FotosDoble.ComoCambios(A("R20_QQQ_vol", "QQQ", "vol", "DOMS", "D2", 31042.06, 749, 0, cb, gexM: 266.4)),
                FotosDoble.ComoCambios(A("R20_NDX_vol", "NDX", "vol", "DOMS", "D1", 31041.83, 30800, 0, cb, gexM: -146.2)),
                FotosDoble.ComoCambios(FotosDoble.Vol(A("DOMS_QQQ_vol", "QQQ", "vol", "DOMS", "D1", 31076.37, 750, 0, cb, gexM: 402.1), cb,
                                                      new[] { 0.0, 3.1, 3.1 }, new[] { 300.0, 900.0, 1800.0 }, new[] { 1.0, 1.0, 1.0 })),
                FotosDoble.ComoCambios(A("DOMS_NDX_vol", "NDX", "vol", "DOMS", "D1", 31030.98, 30790, 0, cb, gexM: -140.0)),
            };
            var fu = f0.Fuentes.Concat(new[]
            {
                new FuenteEstado { Libro = "2.0 QQQ", ConvValor = 41.44466733533935, DatoUtc = cb, Congelada = true,
                                   Texto = "razon 2.0 41.4447 = MNQ 30990.25 (vela de 1 min de las 16:14 NY, la del ultimo trade de la cadena) / spot QQQ 747.75" },
                new FuenteEstado { Libro = "2.0 NDX", ConvValor = 241.83, DatoUtc = cb, Congelada = true, Texto = "base 2.0 241.83 (CRUDA 90 ticks de forwards de la cadena; cota: carry 176.4 +-60 %)" },
            }).ToList();
            var f = FotosDoble.Copia(FotosDoble.Mas(f0, extra), x => x.Fuentes = fu);
            var gr = new GraficoDoble(ahora) { Alto = 31200, Bajo = 30900 };
            gr.A.Visibles.Clear(); foreach (var id in GraficoDoble.Visibles413) gr.A.Visibles.Add(id);   // 4.1.4: QQQ dom ya no es default; aca se prueba prendida
            var d = Armar(f, gr);
            var txt = d.Etiquetas.Select(e => e.Txt).ToList();
            Console.WriteLine("     etiquetas: " + string.Join(" | ", txt));
            Ok(txt.Contains("2.0 QQQ D1 +417M 31.083,50 V"), "C90 la raya de la 2.0: '2.0 QQQ D1 +417M 31.083,50 V' (libro, rol D1, monto, precio, V)");
            Ok(txt.Contains("2.0 QQQ D2 +266M 31.042,06 V · 2.0 NDX D1"), "C91 en un grupo (31.042,06 y 2.0 NDX 31.041,83 a 0,23 pt) va la de mayor monto y (4.1.4) la otra serie 2.0 nombrada DESPUES del precio y la fuente de la cabeza: '2.0 QQQ D2 +266M 31.042,06 V · 2.0 NDX D1'");
            Ok(txt.Contains("QQQ dom D1 +402M ▲3,1M 31.076,37 V"), "C92 la seleccion 2.0 sobre la 4.1 lleva el cambio neto (vol 15 min +3,1 sobre +402: crece): 'QQQ dom D1 +402M ▲3,1M 31.076,37 V'");
            Ok(!txt.Any(t => t.Contains("NDX dom")), "C93 NDX dom apagada por defecto (sin medir): sin etiqueta");
            Ok(!txt.Any(t => t.StartsWith("2.0 QQQ D1") && t.Contains("▲")), "C94 la replica de la 2.0 nunca lleva cambio (TieneLado false aunque venga un valor)");
            Ok(ArmadoPantalla.TieneLado(extra[3]) && !ArmadoPantalla.TieneLado(extra[0]) && !ArmadoPantalla.TieneLado(A("TRES_NDX", "NDX", "3.0", "TRES", "D1", 1, 1, 0, cb)),
               "C95 TieneLado: DOMS D1 si; R20 D1 no; TRES D1 no");
            gr.A.Visibles.Add("DOMS_NDX_vol");
            var dn = Armar(f, gr);
            Ok(dn.Etiquetas.Any(e => e.Txt == "NDX dom D1 " + M + "140M 31.030,98 V"), "C96 con la casilla prendida: 'NDX dom D1 −140M 31.030,98 V'");
            gr.Abierto = true; var dp = Armar(f, gr); gr.Abierto = false;
            Console.WriteLine("     detalle:"); foreach (var l in dp.Panel) Console.WriteLine("       " + l);
            Ok(dp.Panel.Any(l => l.StartsWith("DATO DE HACE 6,8 h · 2.0 QQQ (replica, CBOE) · cadena congelada · razon 2.0 41.4447 = MNQ 30990.25")),
               "C97 fuente de la replica: edad ANTES del numero (dato viejo), congelada y la razon de la 2.0 con su vela y su spot");
            Ok(dp.Panel.Any(l => l.StartsWith("DATO DE HACE 6,8 h · 2.0 NDX (replica, CBOE) · cadena congelada · base 2.0 241.83")), "C98 fuente de la replica NDX con su base");
            Ok(dp.Panel.Any(l => l.Contains("2.0 QQQ D1·vol +417M (razon 2.0 41,4447 = MNQ del ultimo trade / QQQ spot)")), "C99 el detalle de la 2.0 dice su conversion (razon 2.0 = MNQ del ultimo trade / QQQ spot)");
            Ok(dp.Panel.Any(l => l.Contains("QQQ dom D1·vol +402M (razon sincronizada 41,4331)")), "C100 el detalle de QQQ dom dice 'razon sincronizada'");
            Ok(dp.Panel.Any(l => l.Contains("NDX dom D1·vol " + M + "140M (base sincronizada 241,62)")), "C101 el detalle de NDX dom dice 'base sincronizada'");
            int iDom = dp.Panel.FindIndex(l => l.Contains("QQQ dom D1·vol"));
            Ok(iDom >= 0 && iDom + 1 < dp.Panel.Count && dp.Panel[iDom + 1].StartsWith("    vol 15 min") && dp.Panel[iDom + 1].Contains("▲3,1M"), "C102 debajo de QQQ dom, su cambio por volumen: '" + (iDom >= 0 && iDom + 1 < dp.Panel.Count ? dp.Panel[iDom + 1] : "-") + "'");
            int iR = dp.Panel.FindIndex(l => l.Contains("2.0 QQQ D1·vol"));
            Ok(iR >= 0 && !(iR + 1 < dp.Panel.Count && dp.Panel[iR + 1].StartsWith("    ")), "C103 debajo de la 2.0 QQQ no va renglon de cambio (no aplica)");
            gr.A.Visibles.Remove("R20_QQQ_vol"); gr.A.Visibles.Remove("R20_NDX_vol"); gr.Abierto = true; var ds = Armar(f, gr); gr.Abierto = false;
            Ok(!ds.Panel.Any(l => l.Contains("(replica, CBOE)")) && !ds.Etiquetas.Any(e => e.Txt.StartsWith("2.0 ")), "C104 con las replicas apagadas: ni sus etiquetas ni sus fuentes");
        }

        static void PruebaMontos()
        {
            Console.WriteLine("C53+. 4.1.2: montos y cambios en las etiquetas (pedido del operador: 'ndx M+ P 558m 31500 oi', sin los +1 +2)");
            var ahora = new DateTime(2026, 10, 8, 19, 0, 0, DateTimeKind.Utc);
            DateTime cb = ahora.AddMinutes(-16), nq = ahora.AddSeconds(-40);
            var f = FotosDoble.Normal(ahora);
            var gr = new GraficoDoble(ahora);
            var grTodo = new GraficoDoble(ahora); foreach (var c in SeriesPantalla.Casillas) grTodo.A.Visibles.Add(c.Id);
            const string M = "−";

            // C53 formato del monto
            var casos = new (double V, string E)[]
            {
                (102.3, "+102M"), (2296.4, "+2,30B"), (-845.6, M + "846M"), (0.4, "+0,4M"), (-2.1, M + "2,1M"), (45, "+45M"), (-558, M + "558M"),
                (999.4, "+999M"), (999.6, "+1,00B"), (9.94, "+9,9M"), (9.96, "+10M"), (0, "0M"), (-0.04, "0M"), (0.049, "0M"), (3.4, "+3,4M"),
                (12345, "+12,3B"), (99940, "+99,9B"), (99960, "+100B"), (123456, "+123B"), (-1234.5, M + "1,23B"), (1e12, "+1.000.000.000B"),
                (double.NaN, ""), (double.PositiveInfinity, ""), (double.NegativeInfinity, ""),
            };
            var malM = casos.Where(c => ArmadoPantalla.Monto(c.V) != c.E).Select(c => c.V.ToString(CultureInfo.InvariantCulture) + " -> '" + ArmadoPantalla.Monto(c.V) + "' (esperaba '" + c.E + "')").ToList();
            Ok(malM.Count == 0, "C53 Monto: " + casos.Length + " casos (M: 1 decimal < 9,95, entero < 999,5; B: 2/1/0 decimales; '0M' si < 0,05; NaN/Inf -> ''; coma es-AR)", string.Join("; ", malM));
            Ok(casos.All(c => !ArmadoPantalla.Monto(c.V).Contains("-")) && ArmadoPantalla.Monto(-845.6)[0] == '−', "C53b el signo negativo es '−' (U+2212), nunca el guion");

            // C54 sin "+N" (diagnostico estructurado, no regex sobre el texto entero: "+102M" tambien lleva "+")
            var d = Armar(f, gr);
            Console.WriteLine("     etiquetas 4.1.2: " + string.Join(" | ", d.Etiquetas.Select(e => e.Txt)));
            Ok(d.Rotulos.Count == d.Etiquetas.Count && d.Rotulos.Count == 7 && d.Rotulos.All(r => !r.Txt.Split(' ').Any(tk => Regex.IsMatch(tk, @"^\+\d+$"))),
               "C54 ninguna de las " + d.Rotulos.Count + " etiquetas tiene un '+N' (token '+<entero>' sin unidad)");
            var soloQ = FotosDoble.Copia(f, x => x.Actuales = f.Actuales.Where(n => n.Serie == "MAJORS_QQQ_oi").ToList());
            var dsQ = Armar(soloQ, gr); var rq = d.Rotulos.FirstOrDefault(r => r.Serie == "MAJORS_QQQ_oi");
            Ok(dsQ.Rotulos.Count == 1 && rq != null && rq.EnGrupo == 2 && rq.Txt == dsQ.Rotulos[0].Txt,
               "C54b el grupo QQQ M+ / TQQQ muro C (2 niveles) muestra EXACTAMENTE la etiqueta de QQQ M+ sola: '" + rq?.Txt + "'");

            // C55 cabeza por |monto| (NaN al final; empate: precio mas alto)
            var r60 = d.Rotulos.FirstOrDefault(r => r.Txt.Contains("31.060"));
            Ok(r60 != null && r60.Serie == "MUROS_NQ_oi" && r60.EnGrupo == 2 && r60.Col == SeriesPantalla.ColorDe("#9a98d6") && r60.YNivel == gr.YDePrecio(31060),
               "C55 grupo NDX C 31.060,50 (+0,4) / NQ C 31.060 (+89,7): manda el de mayor |monto| aunque este mas abajo: nombre, color (#9a98d6) y tic de NQ");
            var fN = FotosDoble.Mas(f, A("MUROS_NDX_oi", "NDX", "oi", "MUROS", "muro C", 31019.5, 30778, 0, cb, gexM: 94.2));
            var dN = Armar(fN, grTodo);
            Ok(dN.Etiquetas.Any(e => e.Txt == "NDX C +94M 31.019,50 OI") && !dN.Etiquetas.Any(e => e.Txt.StartsWith("3.0 NDX")),
               "C55b sin monto va al final: TRES (31.020, NaN) y NDX muro C OI (31.019,50, +94,2) -> 'NDX C +94M 31.019,50 OI'");
            var fE = FotosDoble.Mas(f, A("MAJORS_NDX_vol", "NDX", "vol", "MAJORS", "M+", 31030.5, 30789, 0, cb, gexM: 50), A("MAJORS_NDX_oi", "NDX", "oi", "MAJORS", "M-", 31030, 30788, 0, cb, gexM: -50));
            var dE = Armar(fE, grTodo);
            Ok(dE.Etiquetas.Any(e => e.Txt == "NDX M+ +50M 31.030,50 V") && !dE.Etiquetas.Any(e => e.Txt.Contains("31.030 OI")), "C55c empate de |monto| (+50 / −50 a 0,5 pt): el de precio mas alto, como hoy");

            // C56 el cambio: solo series vol (ventana elegida) y oi (del dia); nada en la 3.0, en zeros ni en C/P
            gr.A.Ventana = VentanaCambio41.M5; var d5 = Armar(f, gr);
            gr.A.Ventana = VentanaCambio41.M30; var d30 = Armar(f, gr);
            gr.A.Ventana = VentanaCambio41.M15;
            Ok(d5.Etiquetas.Any(e => e.Txt == "NDX P −250M ▲1,2M 30.981,62 V") && d30.Etiquetas.Any(e => e.Txt == "NDX P −250M ▲5,0M 30.981,62 V"),
               "C56 serie V: el cambio es CambioVolM de la ventana elegida (5 min ▲1,2M · 15 min ▲3,1M · 30 min ▲5,0M)");
            Ok(d5.Etiquetas.Any(e => e.Txt == "QQQ M+ +109M ▲12M 31.073 OI") && d30.Etiquetas.Any(e => e.Txt == "QQQ M+ +109M ▲12M 31.073 OI"),
               "C56b serie OI: el cambio del dia, el mismo con cualquier ventana");
            var fx = FotosDoble.ConNiveles(f, n => n.Serie == "MAJORS_QQQ_oi" || (n.Serie == "MUROS_NDX_vol" && n.Rol == "muro P") || n.Serie == "TRES_NDX" || n.Serie == "ZEST_QQQ_vol",
                n =>
                {
                    if (n.Serie == "MAJORS_QQQ_oi") n.CambioVolM = new[] { 99.0, 99.0, 99.0 };
                    else if (n.Serie == "MUROS_NDX_vol") n.CambioOiDiaM = 77;
                    else if (n.Serie == "TRES_NDX") { n.CambioVolM = new[] { 5.0, 5.0, 5.0 }; n.CambioOiDiaM = 5; n.GexM = 143; }
                    else { n.CambioOiDiaM = 8; }
                });
            var dx = Armar(fx, gr);
            Ok(dx.Etiquetas.Any(e => e.Txt == "QQQ M+ +109M ▲12M 31.073 OI") && dx.Etiquetas.Any(e => e.Txt == "NDX P −250M ▲3,1M 30.981,62 V")
               && dx.Etiquetas.Any(e => e.Txt == "3.0 NDX D1 +143M 31.020") && dx.Etiquetas.Any(e => e.Txt == "↑ QQQ 0G 31.500 V"),
               "C56c cruzados: OI no usa CambioVolM, V no usa CambioOiDiaM, la 3.0 no lleva cambio ('3.0 NDX D1 +143M 31.020'), un zero V no usa CambioOiDiaM");
            var fcp = FotosDoble.Mas(f, FotosDoble.Oi(A("MUROS_QQQ_oi", "QQQ", "oi", "MUROS", "muro P", 31076.37, 750, 20, cb, gexM: -503.6), -30, FotosDoble.OiR1, FotosDoble.OiR0),
                                        FotosDoble.Oi(A("MUROS_QQQ_oi", "QQQ", "oi", "MUROS", "muro C", 31076.37, 750, 20, cb, gexM: 338.9), 10, FotosDoble.OiR1, FotosDoble.OiR0));
            var dcp = Armar(fcp, gr);
            Ok(dcp.Etiquetas.Any(e => e.Txt == "QQQ C/P +339M/−504M 31.076 OI"), "C56d muro C y muro P de la misma serie en el mismo precio: 'QQQ C/P +339M/−504M 31.076 OI' (C primero aunque llegue despues; sin cambio)");
            var f0 = FotosDoble.ConNiveles(f, n => n.Serie == "MUROS_NDX_vol" && n.Rol == "muro P", n => n.CambioVolM = new[] { 0.0, 0.04, 0.0 });
            Ok(Armar(f0, gr).Etiquetas.Any(e => e.Txt == "NDX P −250M 30.981,62 V"), "C56e |cambio| < 0,05 (redondea a '0M'): no va en la etiqueta");
            var fv = FotosDoble.Mas(f,
                FotosDoble.Vol(A("MAJORS_NQ_vol", "NQ", "vol", "MAJORS", "M+", 31090, 31090, 0, nq, gexM: 42.6), nq, new[] { -1.3, -1.3, -1.3 }, new[] { 300.0, 900.0, 1800.0 }, new[] { 1.0, 1.0, 1.0 }),
                FotosDoble.Vol(A("MUROS_NQ_vol", "NQ", "vol", "MUROS", "muro C", 30930, 30930, 0, nq), nq, new[] { 2.0, 2.0, 2.0 }, new[] { 300.0, 900.0, 1800.0 }, new[] { 1.0, 1.0, 1.0 }),
                FotosDoble.Vol(A("MAJORS_NDX_vol", "NDX", "vol", "MAJORS", "M-", 30915, 30673.38, 0, cb), cb, new[] { -2.0, -2.0, -2.0 }, new[] { 300.0, 900.0, 1800.0 }, new[] { 1.0, 1.0, 1.0 }));
            var dv = Armar(fv, grTodo);
            Ok(dv.Etiquetas.Any(e => e.Txt == "NQ M+ +43M ▼1,3M 31.090 V") && dv.Etiquetas.Any(e => e.Txt == "NQ C ▲2,0M 30.930 V") && dv.Etiquetas.Any(e => e.Txt == "NDX M- ▼2,0M 30.915 V"),
               "C56f ▼ en un neto V que se achica (M+ +42,6 con −1,3); sin monto: ▲ si el cambio es + y ▼ si es −");

            // C57 OI viejo oculta el cambio (C7); con OI vigente el mismo nivel lo muestra
            var fo = FotosDoble.ConNiveles(f, n => n.Serie == "MUROS_NQ_oi" && n.Rol == "muro C", n => n.OiViejo = false);
            Ok(r60 != null && r60.Cambio == "" && Armar(fo, gr).Etiquetas.Any(e => e.Txt == "NQ C +90M ▲5,2M 31.060 OI"),
               "C57 OI viejo: sin cambio en la etiqueta aunque CambioOiDiaM = +5,2; con OI vigente 'NQ C +90M ▲5,2M 31.060 OI'");

            // C58 colores y posicion de los tramos
            var tx = d.Prims.Where(p => p.Tipo == TipoPrimPantalla.Texto).ToList();
            int iF = tx.FindIndex(p => p.T == "FAM P " + M + "511M"), iD = tx.FindIndex(p => p.T == "NDX P " + M + "250M");
            Ok(ArmadoPantalla.ColSube == Color.FromArgb(0x08, 0x99, 0x81) && ArmadoPantalla.ColBaja == Color.FromArgb(0xf2, 0x36, 0x45), "C58 colores: ▲ #089981, ▼ #f23645");
            Ok(iF >= 0 && tx[iF + 1].T == " ▼20M" && tx[iF + 1].C == ArmadoPantalla.ColBaja && tx[iF + 2].T == " 30.990 OI" && tx[iF].C == Color.FromArgb(240, ArmadoPantalla.ColTexto) && tx[iF + 2].C == tx[iF].C
               && iD >= 0 && tx[iD + 1].T == " ▲3,1M" && tx[iD + 1].C == ArmadoPantalla.ColSube && tx[iD + 2].T == " 30.981,62 V",
               "C58b el tramo del cambio va aparte en su color (▼20M rojo, ▲3,1M verde); el resto del texto como hoy");
            double cw = Medir(new string('0', 20), 8).Width / 20.0;
            Ok(iD >= 0 && tx[iD + 1].R.X == tx[iD].R.X + (int)Math.Round(cw * tx[iD].T.Length) && tx[iD + 2].R.X == tx[iD].R.X + (int)Math.Round(cw * (tx[iD].T.Length + tx[iD + 1].T.Length))
               && tx[iD + 1].R.Y == tx[iD].R.Y && tx[iD + 2].R.Y == tx[iD].R.Y,
               "C58c los tramos siguen en la misma linea, por columna (x = columnas x ancho de '0' medido con medir())");

            // C59 ancho (medir calibrado a 0,74 x tam)
            var cajas = d.Rotulos.Where(r => r.Fuera == 0).ToList();
            bool dentro = true;
            foreach (var r in cajas)
            {
                var ts = tx.Where(p => p.R.Y == r.Y - (Medir("X", 8).Height + 3) / 2 + 1 && p.R.X >= r.X0 && p.C != Color.FromArgb(200, ArmadoPantalla.ColFondo)).ToList();
                int der = ts.Count == 0 ? int.MaxValue : ts.Max(p => p.R.X + Medir(p.T, 8).Width);
                dentro &= ts.Count > 0 && der <= r.X0 + r.Ancho - 2 && ts.Min(p => p.R.X) == r.X0 + 6;
            }
            Ok(dentro, "C59 cada caja encierra todos sus tramos (texto desde x0+6 hasta antes del borde derecho)");
            var gOff = new GraficoDoble(ahora); gOff.A.Montos = false; gOff.A.Cambios = false;
            int wViejo = Armar(f, gOff).Rotulos.Where(r => r.Fuera == 0).Max(r => r.Ancho), wNuevo = cajas.Max(r => r.Ancho);
            Console.WriteLine("     anchos a letra 9 (tamC 8, 0,74 x tam = " + (0.74 * 8).ToString("0.00", CultureInfo.InvariantCulture) + " px por letra): "
                + string.Join(" | ", cajas.Select(r => r.Txt.Length + " letras " + r.Ancho + " px")) + " · la mas ancha 4.1.1 sin +N: " + wViejo + " px; 4.1.2: " + wNuevo
                + " px (" + (100.0 * wNuevo / 852).ToString("0", CultureInfo.InvariantCulture) + " % de un grafico de 852 px como el del operador)");
            Ok(wNuevo <= 200, "C59b la etiqueta realista mas ancha mide " + wNuevo + " px a letra 9 (tope de prueba 200 px; 4.1.1 sin +N: " + wViejo + " px)");

            // C60 Consolas tiene los glifos nuevos con el mismo avance que '0' (los tramos por columna caen exactos; dibujarlos en ATAS: sin verificar)
            try
            {
                var gt = new System.Windows.Media.GlyphTypeface(new Uri(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "consola.ttf")));
                double w0 = gt.AdvanceWidths[gt.CharacterToGlyphMap['0']];
                var faltan = "▲▼♦−↑↓·→±".Where(ch => !gt.CharacterToGlyphMap.ContainsKey(ch) || Math.Abs(gt.AdvanceWidths[gt.CharacterToGlyphMap[ch]] - w0) > 1e-9).ToList();
                Ok(faltan.Count == 0, "C60 Consolas tiene ▲ ▼ ♦ − ↑ ↓ · → ± con el mismo avance que '0' (" + w0.ToString("0.####", CultureInfo.InvariantCulture) + " em)", string.Join("", faltan));
            }
            catch (Exception e) { Ok(false, "C60 Consolas: no pude leer la fuente", e.Message); }

            // C61 fuera de pantalla con monto y cambio (sin caja, con sombra, el cambio en su color)
            var grQ = new GraficoDoble(ahora); grQ.A.Visibles.Add("MUROS_QQQ_vol");
            var ff = FotosDoble.Mas(f,
                FotosDoble.Vol(A("MUROS_QQQ_vol", "QQQ", "vol", "MUROS", "muro C", 31200, 753, 20, cb, gexM: 2585.2), cb, new[] { 5.0, 15.3, 30.0 }, new[] { 300.0, 885.0, 1800.0 }, new[] { 1.0, 1.0, 1.0 }),
                FotosDoble.Vol(A("MUROS_QQQ_vol", "QQQ", "vol", "MUROS", "muro P", 30800, 743, 20, cb, gexM: -2172), cb, new[] { -1.0, -4.4, -9.0 }, new[] { 300.0, 885.0, 1800.0 }, new[] { 1.0, 1.0, 1.0 }));
            var df = Armar(ff, grQ);
            var tf = df.Prims.Where(p => p.Tipo == TipoPrimPantalla.Texto).ToList();
            int iA = tf.FindIndex(p => p.T == "↑ QQQ C +2,59B"); var colQ = SeriesPantalla.ColorDe("#dabe6f");
            Ok(df.Etiquetas.Any(e => e.Txt == "↑ QQQ C +2,59B ▲15M 31.200 V") && df.Etiquetas.Any(e => e.Txt == "↓ QQQ P " + M + "2,17B ▲4,4M 30.800 V")
               && iA > 0 && tf[iA - 1].T == "↑ QQQ C +2,59B ▲15M 31.200 V" && tf[iA - 1].C == Color.FromArgb(200, ArmadoPantalla.ColFondo)
               && tf[iA].C == colQ && tf[iA + 1].T == " ▲15M" && tf[iA + 1].C == ArmadoPantalla.ColSube && tf[iA + 2].T == " 31.200 V" && tf[iA + 2].C == colQ,
               "C61 fuera de pantalla con monto y cambio: '↑ QQQ C +2,59B ▲15M 31.200 V' (sombra entera, texto en el color de la serie, cambio en verde)");

            // C62 valores enormes, infinitos y arreglos raros: no tira
            var raros = new List<NivelActual>
            {
                FotosDoble.Vol(A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro C", 31010, 30770, 0, cb, gexM: 1e12), cb,
                               new[] { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity }, new[] { double.NaN, 1e9, -5 }, new[] { double.PositiveInfinity, double.NaN, -1 }),
                new NivelActual { Serie = "MUROS_NQ_oi", Libro = "NQ", Fuente = "oi", Tipo = "MUROS", Rol = "muro P", Precio = 30965, Strike = 30965, GexM = -1e15, DatoUtc = nq,
                                  CambioOiDiaM = double.NegativeInfinity, CambioCobertura = new double[0], CambioOiDesdeUtc = DateTime.MaxValue },
                new NivelActual { Serie = "MAJORS_QQQ_oi", Libro = "QQQ", Fuente = "oi", Tipo = "MAJORS", Rol = "M-", Precio = 31045, Strike = 749, Banda = 20, GexM = double.PositiveInfinity, DatoUtc = cb,
                                  CambioVolM = new[] { 5.0 }, CambioOiDiaM = 1e15, CambioNota = null },
                new NivelActual { Serie = "FAM_MUROS_oi", Libro = "familia", Fuente = "oi", Tipo = "MUROS", Rol = "muro C", Precio = 31035, Strike = double.NaN, GexM = double.NegativeInfinity, DatoUtc = cb,
                                  CambioVolM = null, CambioVolSegReal = new[] { 1.0 }, CambioVolDesdeUtc = null, CambioCobertura = null, CambioNota = null, CambioOiDiaM = double.NaN },
                new NivelActual { Serie = "T_MUROS_oi", Libro = "TQQQ", Fuente = "oi", Tipo = "MUROS", Rol = "muro C", Precio = 31025, Strike = 82, Banda = 4, GexM = double.MaxValue, DatoUtc = cb,
                                  CambioOiDiaM = double.MinValue, CambioOiDesdeUtc = DateTime.MinValue, CambioOiHastaUtc = DateTime.MaxValue },
                new NivelActual { Serie = "MUROS_NDX_vol", Libro = "NDX", Fuente = "vol", Tipo = "MUROS", Rol = "muro P", Precio = 30960, Strike = 30720, GexM = -1, DatoUtc = cb,
                                  CambioVolM = new double[0], CambioVolSegReal = null, CambioVolDesdeUtc = new DateTime[0], CambioVolHastaUtc = null },
            };
            var fr = FotosDoble.Copia(f, x => x.Actuales = raros);
            Exception ex = null; DibujoPantalla dr = null, drp = null;
            try { dr = Armar(fr, gr); gr.Abierto = true; drp = Armar(fr, gr); gr.Abierto = false; var pr = new PantallaFamilia(gr); pr.Pintar(new RenderDoble(), fr); pr.Pintar(new RenderDoble(), FotosDoble.Copia(fr, x => x.CambiosEstado = null)); }
            catch (Exception e) { ex = e; gr.Abierto = false; }
            Ok(ex == null && dr != null && dr.Etiquetas.Any(e => e.Txt == "NDX C +1.000.000.000B 31.010 V") && dr.Etiquetas.Any(e => e.Txt == "QQQ M- ▲1.000.000.000.000B 31.045 OI") && drp.Panel.Count > 10,
               "C62 montos 1e12 / −1e15 / ±Inf / MaxValue, cambios Inf, arreglos nulos, vacios o cortos y CambiosEstado null: no tira; lo infinito no se muestra" + (ex == null ? "" : " -> " + ex.Message));

            // C63 ajustes apagados: la etiqueta de la 4.1.1 SIN "+N" (cabeza = la de mayor precio, como hoy)
            var dOff = Armar(f, gOff);
            var viejas = new[] { "↑ QQQ 0G v  31.500", "QQQ M+ OI  31.073", "NDX C v  31.060,50", "3.0 NDX D1  31.020", "FAM P OI  30.990", "NDX P v  30.981,62", "NQ P OI*  30.950" };
            Ok(dOff.Etiquetas.Select(e => e.Txt).OrderBy(x => x).SequenceEqual(viejas.OrderBy(x => x), StringComparer.Ordinal)
               && !dOff.Prims.Any(p => p.Tipo == TipoPrimPantalla.Texto && (p.C == ArmadoPantalla.ColSube || p.C == ArmadoPantalla.ColBaja)),
               "C63 Monto41Rotulos y Cambio41Rotulos apagados: las etiquetas de la 4.1.1 sin '+N' (" + string.Join(" | ", dOff.Etiquetas.Select(e => e.Txt)) + ")");
            var gM = new GraficoDoble(ahora); gM.A.Cambios = false; var dM = Armar(f, gM);
            var gC = new GraficoDoble(ahora); gC.A.Montos = false; var dC = Armar(f, gC);
            Ok(dM.Etiquetas.Any(e => e.Txt == "QQQ M+ +109M 31.073 OI") && dM.Etiquetas.Any(e => e.Txt == "FAM P " + M + "511M 30.990 OI")
               && dC.Etiquetas.Any(e => e.Txt == "QQQ M+ ▲12M 31.073 OI") && dC.Etiquetas.Any(e => e.Txt == "NQ C 31.060 OI*") && dC.Etiquetas.Any(e => e.Txt == "FAM P ▼20M 30.990 OI"),
               "C63b solo monto: 'QQQ M+ +109M 31.073 OI'; solo cambio: 'QQQ M+ ▲12M 31.073 OI' (la cabeza sigue siendo la de mayor |monto|)");

            // C64+ la pestaña: leyenda, fuentes de cambios, detalle por nivel
            gr.Abierto = true; var dp = Armar(f, gr); gr.Abierto = false;      // (el detalle completo lo imprime C22)
            int iBl = dp.Panel.IndexOf("");
            Ok(iBl > 0 && dp.Panel[iBl + 1] == "monto = M USD de cobertura por 1 % (M millones, B mil millones) · gamma de ahora: el precio no lo mueve"
               && dp.Panel[iBl + 2] == "▲ crece · ▼ se achica · ♦ dio vuelta (cambió de signo)"
               && dp.Panel[iBl + 3] == "V: volumen nuevo en 15 min (no dice si abren o cierran) · OI: posiciones vs la publicación anterior"
               && dp.Panel.Skip(iBl + 1).Take(3).All(l => l.Length <= 110),
               "C64 leyenda corta arriba del detalle (tres renglones de <= 110 letras: el grafico del operador muestra ~115), con la ventana elegida (15 min) y la ♦");
            int iCe = dp.Panel.IndexOf("cambios NQ: vol 15 min 18:44-18:59Z; OI 10-08 01:30Z vs 10-07 01:24Z (OI de 2 sesiones)");
            Ok(iCe > 0 && iCe < iBl && dp.Panel[iCe + 1] == "cambios NDX: vol 15 min 18:29-18:44Z; OI 10-08 07:40Z vs 10-07 07:31Z" && dp.Panel.IndexOf("cinta MNQ: ultimo tick hace 2 s") < iCe,
               "C65 las lineas de FotoFamilia.CambiosEstado al final de las fuentes");
            int iP = dp.Panel.IndexOf("30.981,62  NDX muro P·vol " + M + "250M (strike NDX 30.740) · 16 min");
            Ok(iP > 0 && dp.Panel[iP + 1] == "    vol 15 min 18:29→18:44 UTC: ▲3,1M · cobertura 90 %", "C66 vol: ventana real con sus horas UTC, el cambio y la cobertura (< 95 %)");
            Ok(dp.Panel.Contains("    QQQ M+·OI: OI 10-08 07:40Z vs 10-07 07:31Z: ▲12M") && dp.Panel.Contains("    OI 10-08 07:40Z vs 10-07 07:31Z: ▼20M"),
               "C67 OI: publicacion vigente vs anterior con fecha y hora; cobertura 97 % no se dice");
            Ok(dp.Panel.Any(l => l.Contains("TQQQ 81 muro C·OI +3,4M por 1 % de TQQQ")) && dp.Panel.Any(l => l.Contains("TQQQ 80 muro P·OI " + M + "2,1M por 1 % de TQQQ")), "C68 TQQQ: el monto es 'por 1 % de TQQQ'");
            Ok(dp.Panel.Contains("    TQQQ 81 muro C·OI: OI sin la sesion anterior en memoria"), "C69 la nota del cambio (CambioNota) en el detalle");
            Ok(dp.Panel.Contains("    NDX muro C·vol: vol 16 min 18:28→18:44 UTC: ▲0,2M"), "C70 ventana real distinta de la pedida: dice 16 min (no 15)");
            Ok(dp.Panel.Contains("    NQ muro C·OI (OI 2s): OI 10-08 01:30Z vs 10-07 01:24Z: ▲5,2M (OI de 2 sesiones: no va en la etiqueta)"),
               "C71 OI viejo: el detalle lo muestra con sus fechas y aclara que no va en la etiqueta");
            // C72 (revision 4.1.2): los niveles SIN lado (zeros, cruces, CONF, la 3.0, el zero de TQQQ) anotados COMO CambiosFamilia (arreglos NaN en
            // todos, nota "") no tienen renglon de cambio; uno CON lado y sin dato lo conserva. Mira el renglon que sigue al de cada nivel (antes el
            // predicado buscaba "0G" dentro del sub-renglon, que en un grupo de un nivel nunca lleva el nombre, y el doble dejaba null en los zeros).
            {
                DateTime tqz = ahora.AddMinutes(-15);
                var sinLado = new List<NivelActual>
                {
                    FotosDoble.ComoCambios(A("ZEST_QQQ_vol", "QQQ", "vol", "ZEST", "0G est.", 31055.57, 749.5, 0, cb)),
                    FotosDoble.ComoCambios(A("ZTP_NDX_vol", "NDX", "vol", "ZTP", "cruce arriba", 31049.62, 30808.89, 0, cb)),
                    FotosDoble.ComoCambios(A("ZTP_NQ_vol", "NQ", "vol", "ZTP", "cruce abajo", 31013.98, 31013.98, 0, nq)),
                    FotosDoble.ComoCambios(A("CONF_vol", "familia", "vol", "CONF", "conf", 31000, double.NaN, 0, cb)),
                    FotosDoble.ComoCambios(A("TRES_NDX", "NDX", "3.0", "TRES", "D1", 31030, double.NaN, 0, cb, gexM: 143)),
                    FotosDoble.ComoCambios(A("ZEST_NQ_oi", "NQ", "oi", "ZEST", "0G est.", 30990, 30990, 0, nq)),
                    FotosDoble.ComoCambios(A("T_ZERO_oi", "TQQQ", "oi", "ZTP", "zero", 30970, 79.5, 4, tqz)),
                };
                var conLado = FotosDoble.ComoCambios(A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro P", 30940, 30700, 0, cb, gexM: -402));
                conLado.CambioNota = "sin libro NDX en el minuto";
                var fz = FotosDoble.Copia(f, x => x.Actuales = sinLado.Concat(new[] { conLado }).ToList());
                grTodo.Abierto = true; var dz = Armar(fz, grTodo); grTodo.Abierto = false;
                var malZ = new List<string>();
                foreach (var n in sinLado)
                {
                    string pz = ArmadoPantalla.P(n.Banda > 0 ? Math.Round(n.Precio) : n.Precio);
                    int iz = dz.Panel.FindIndex(l => !l.StartsWith(" ") && !l.StartsWith("—") && l.Contains(pz));     // su renglon, en cualquier formato
                    if (iz < 0) { malZ.Add(n.Serie + " sin renglon"); continue; }
                    if (iz + 1 < dz.Panel.Count && dz.Panel[iz + 1].StartsWith("    ")) malZ.Add(n.Serie + " " + n.Rol + ": '" + dz.Panel[iz + 1].Trim() + "'");
                }
                int iCl = dz.Panel.FindIndex(l => !l.StartsWith(" ") && l.Contains("NDX muro P·vol") && l.Contains("30.940"));
                Ok(malZ.Count == 0 && iCl >= 0 && dz.Panel[iCl + 1] == "    vol 15 min: sin dato · sin libro NDX en el minuto",
                   "C72 sin renglon de cambio en los " + sinLado.Count + " niveles sin lado anotados como CambiosFamilia (ZEST, ZTP, CONF, TRES, T_ZERO); un muro sin dato lo conserva ('vol 15 min: sin dato · sin libro NDX en el minuto')",
                   string.Join("; ", malZ) + (iCl >= 0 && iCl + 1 < dz.Panel.Count ? " | muro: '" + dz.Panel[iCl + 1] + "'" : " | muro sin renglon"));
                var fz2 = FotosDoble.ConNiveles(fz, n => n.Serie == "ZEST_QQQ_vol" || n.Serie == "CONF_vol" || n.Serie == "ZEST_NQ_oi",
                                                n => { n.CambioVolM = new[] { 4.0, 4.0, 4.0 }; n.CambioOiDiaM = 4; n.CambioOiHastaUtc = FotosDoble.OiNqR0; });
                grTodo.Abierto = true; var dz2 = Armar(fz2, grTodo); grTodo.Abierto = false;
                Ok(dz2.Etiquetas.Any(e => e.Txt == "QQQ 0G 31.055,57 V") && dz2.Etiquetas.Any(e => e.Txt == "CONF 31.000 V") && dz2.Etiquetas.Any(e => e.Txt == "NQ 0G 30.990 OI")
                   && !dz2.Panel.Any(l => l.StartsWith("    ") && l.Contains("4,0M")),
                   "C72b aunque a un nivel sin lado le lleguen valores de cambio (no deberia), ni la etiqueta ni el detalle los muestran (regla de CambiosFamilia.LadoDe)");
            }
            var fv45 = FotosDoble.Normal(ahora, edadCboeMin: 45);
            gr.Abierto = true; var dp45 = Armar(fv45, gr); gr.Abierto = false;
            Ok(dp45.Panel.Any(l => l.StartsWith("[45 min] 31.073  QQQ M+·OI +109M")) && dp45.Panel.Any(l => l.StartsWith("[45 min] 30.981,62  NDX muro P·vol " + M + "250M")),
               "C73 dato de mas de 30 min: la edad va ANTES del precio y del monto ('[45 min] 31.073  QQQ M+·OI +109M ...')");
            var fcg = FotosDoble.ConNiveles(f, n => n.Serie == "MUROS_NDX_vol" && n.Rol == "muro P", n => { n.CambioVolM = new[] { 0.0, 0.0, 0.0 }; n.CambioNota = "cadena congelada"; });
            gr.Abierto = true; var dcg = Armar(fcg, gr); gr.Abierto = false;
            Ok(dcg.Etiquetas.Any(e => e.Txt == "NDX P " + M + "250M 30.981,62 V") && dcg.Panel.Contains("    vol 15 min 18:29→18:44 UTC: 0M · cobertura 90 % · cadena congelada"),
               "C74 cadena congelada (cambio 0 exacto): sin cambio en la etiqueta; el detalle dice '0M · ... · cadena congelada'");

            // C75 los ejemplos del diseño 4.1.2, uno por foto (sin grupos): el formato que pidio el operador
            var grEj = new GraficoDoble(ahora) { Alto = 31600, Bajo = 30900 }; foreach (var c in SeriesPantalla.Casillas) grEj.A.Visibles.Add(c.Id);
            var ejemplos = new (NivelActual N, string E)[]
            {
                (A("MUROS_NDX_oi", "NDX", "oi", "MUROS", "muro P", 31500, 31258.38, 0, cb, gexM: -558), "NDX P " + M + "558M 31.500 OI"),
                (FotosDoble.Vol(A("MUROS_NQ_vol", "NQ", "vol", "MUROS", "muro C", 31100, 31100, 0, nq, gexM: 45), nq, new[] { 1.0, 3.1, 6.0 }, new[] { 300.0, 900.0, 1800.0 }, new[] { 1.0, 1.0, 1.0 }),
                 "NQ C +45M ▲3,1M 31.100 V"),
                (A("ZEST_QQQ_vol", "QQQ", "vol", "ZEST", "0G est.", 31054, 749.49, 0, cb), "QQQ 0G 31.054 V"),
                (A("TRES_NDX", "NDX", "3.0", "TRES", "D1", 31036, double.NaN, 0, cb, gexM: 143), "3.0 NDX D1 +143M 31.036"),
                (A("T_MUROS_oi", "TQQQ", "oi", "MUROS", "muro C", 31073.31, 81, 4, ahora.AddMinutes(-15), gexM: 3.4), "TQQQ 81 C +3,4M 31.073 OI"),
                (A("CONF_vol", "familia", "vol", "CONF", "conf", 31000, double.NaN, 0, cb), "CONF 31.000 V"),
                (A("ZTP_NDX_vol", "NDX", "vol", "ZTP", "cruce arriba", 31041.01, 30799.39, 0, cb), "NDX cruce↑ 31.041,01 V"),
                (A("ZTP_NQ_vol", "NQ", "vol", "ZTP", "cruce abajo", 31030.5, 31030.5, 0, nq), "NQ cruce↓ 31.030,50 V"),
                (A("MAJORS_NQ_oi", "NQ", "oi", "MAJORS", "M-", 31050, 31050, 0, nq, gexM: -28.1, oiViejo: true), "NQ M- " + M + "28M 31.050 OI*"),
            };
            var malEj = new List<string>();
            foreach (var (n, e) in ejemplos)
            {
                var de = Armar(FotosDoble.Copia(f, x => x.Actuales = new List<NivelActual> { n }), grEj);
                string got = de.Etiquetas.Count == 1 ? de.Etiquetas[0].Txt : "(" + de.Etiquetas.Count + " etiquetas)";
                if (got != e) malEj.Add("'" + got + "' en vez de '" + e + "'");
            }
            Ok(malEj.Count == 0, "C75 los " + ejemplos.Length + " ejemplos del diseño salen tal cual ('NDX P −558M 31.500 OI', 'NQ C +45M ▲3,1M 31.100 V', 'QQQ 0G 31.054 V', '3.0 NDX D1 +143M 31.036', "
               + "'TQQQ 81 C +3,4M 31.073 OI', 'CONF 31.000 V', 'NDX cruce↑ ...', 'NQ M- −28M 31.050 OI*')", string.Join("; ", malEj));

            PruebaRevision412(ahora, f, cb, nq, grTodo, grEj);
        }

        // ------------------------------------------------------------------ C76+ (revision 4.1.2): vueltas de signo, ancho visible, prefijo de cambios
        static void PruebaRevision412(DateTime ahora, FotoFamilia f, DateTime cb, DateTime nq, GraficoDoble grTodo, GraficoDoble grEj)
        {
            const string M = "−";
            Console.WriteLine("C76+. revision 4.1.2: ▲/▼ por magnitud y ♦ si el neto dio vuelta; renglones partidos en el ancho visible");

            // C76 un neto que dio vuelta. Dato real: MAJORS_NQ_oi M+ 31.000 el 10-09 01:49Z (viva3-NQ-2026-10-09, recalculado por la revision y por el
            // arnes de B-pos): GexM +8,0 (7,97) con ΔOI +68,499 -> antes −60,5. La magnitud BAJO de 60,5 a 8,0 y cambio el signo: la regla vieja
            // (signo de Δ == signo de GexM) lo pintaba "▲68M" en verde (crece).
            {
                var r0 = new DateTime(2026, 10, 9, 1, 48, 56, DateTimeKind.Utc); var r1 = new DateTime(2026, 10, 8, 1, 30, 26, DateTimeKind.Utc);
                var mm = FotosDoble.ComoCambios(FotosDoble.Oi(A("MAJORS_NQ_oi", "NQ", "oi", "MAJORS", "M+", 31000, 31000, 0, nq, gexM: 8.0), 68.499, r1, r0, cob: 1.0));
                var fm = FotosDoble.Copia(f, x => x.Actuales = new List<NivelActual> { mm });
                grTodo.Abierto = true; var dm = Armar(fm, grTodo); grTodo.Abierto = false;
                var rm = dm.Rotulos.FirstOrDefault(r => r.Serie == "MAJORS_NQ_oi");
                var tm = dm.Prims.Where(p => p.Tipo == TipoPrimPantalla.Texto).ToList();
                int im = tm.FindIndex(p => p.T == " ♦68M");
                Ok(rm != null && rm.Txt == "NQ M+ +8,0M ♦68M 31.000 OI" && !rm.CambioCrece && rm.CambioVuelta && rm.ColCambio == ArmadoPantalla.ColVuelta
                   && im > 0 && tm[im].C == ArmadoPantalla.ColVuelta && tm[im - 1].T == "NQ M+ +8,0M" && tm[im + 1].T == " 31.000 OI",
                   "C76 neto que dio vuelta (MAJORS_NQ_oi M+ 31.000, 10-09 01:49Z: +8,0 con ΔOI +68,5, antes −60,5): 'NQ M+ +8,0M ♦68M 31.000 OI' con la ♦ en blanco, no '▲68M' verde",
                   "'" + rm?.Txt + "' crece=" + rm?.CambioCrece + " vuelta=" + rm?.CambioVuelta);
                Ok(dm.Panel.Contains("    OI 10-09 01:48Z vs 10-08 01:30Z: ♦ dio vuelta +68M (antes " + M + "60M)"),
                   "C76b el detalle lo dice en palabras, con el cambio con signo y el valor de antes: 'OI 10-09 01:48Z vs 10-08 01:30Z: ♦ dio vuelta +68M (antes −60M)'",
                   string.Join(" | ", dm.Panel.Where(l => l.StartsWith("    "))));
            }

            // C77 la regla por magnitud, caso por caso (antes = GexM − Δ; |x| < 0,05 no tiene signo)
            {
                var ws = new[] { 300.0, 900.0, 1800.0 }; var cob1 = new[] { 1.0, 1.0, 1.0 };
                NivelActual V(string serie, string libro, string rol, double precio, double k, double g, double dv)
                    => FotosDoble.ComoCambios(FotosDoble.Vol(A(serie, libro, "vol", serie.StartsWith("MUROS") ? "MUROS" : "MAJORS", rol, precio, k, 0, libro == "NQ" ? nq : cb, gexM: g),
                                                             libro == "NQ" ? nq : cb, new[] { dv, dv, dv }, ws, cob1));
                var casos = new (NivelActual N, string E, int S)[]
                {
                    (V("MAJORS_NDX_vol", "NDX", "M-", 31030, 30788, -30, -10), "NDX M- " + M + "30M ▲10M 31.030 V", 1),          // M- mas negativo: crece
                    (V("MAJORS_NQ_vol", "NQ", "M+", 31090, 31090, 42.6, -1.3), "NQ M+ +43M ▼1,3M 31.090 V", -1),                  // M+ que baja sin dar vuelta
                    (V("MAJORS_NDX_vol", "NDX", "M+", 31030, 30788, 5.0, 5.02), "NDX M+ +5,0M ▲5,0M 31.030 V", 1),               // de ~0 (−0,02) a +5: crece
                    (V("MAJORS_NDX_vol", "NDX", "M-", 31030, 30788, 0.0, 3.0), "NDX M- 0M ▼3,0M 31.030 V", -1),                  // de −3 a 0: se achica
                    (V("MUROS_NQ_vol", "NQ", "muro C", 31040, 31040, 0.4, 0.43), "NQ C +0,4M ▲0,4M 31.040 V", 1),                // redondeo de GexM: no es vuelta
                    (V("MAJORS_NDX_vol", "NDX", "M+", 31030, 30788, 3.0, 10.0), "NDX M+ +3,0M ♦10M 31.030 V", 0),                // −7 -> +3: dio vuelta (y bajo)
                    (V("MAJORS_NDX_vol", "NDX", "M+", 31030, 30788, 5.0, 9.0), "NDX M+ +5,0M ♦9,0M 31.030 V", 0),                // −4 -> +5: dio vuelta (y crecio)
                    (V("MAJORS_NDX_vol", "NDX", "M-", 31030, 30788, -2.0, -12.0), "NDX M- " + M + "2,0M ♦12M 31.030 V", 0),      // +10 -> −2
                    (V("MUROS_NDX_vol", "NDX", "muro P", 31030, 30788, -249.6, 20.0), "NDX P " + M + "250M ▼20M 31.030 V", -1),  // puts que se van
                };
                var mal = new List<string>();
                foreach (var (n, e, s) in casos)
                {
                    var de = Armar(FotosDoble.Copia(f, x => x.Actuales = new List<NivelActual> { n }), grEj);
                    var r = de.Rotulos.Count == 1 ? de.Rotulos[0] : null;
                    var colE = s > 0 ? ArmadoPantalla.ColSube : s < 0 ? ArmadoPantalla.ColBaja : ArmadoPantalla.ColVuelta;
                    if (r == null || r.Txt != e || r.CambioCrece != (s > 0) || r.CambioVuelta != (s == 0) || r.ColCambio != colE)
                        mal.Add("'" + (r?.Txt ?? "(sin etiqueta)") + "' en vez de '" + e + "' (crece=" + r?.CambioCrece + " vuelta=" + r?.CambioVuelta + ")");
                }
                Ok(mal.Count == 0, "C77 " + casos.Length + " casos: ▲ si |GexM| > |GexM − Δ|, ▼ si no, ♦ si el neto cambio de signo (de ~0 o a ~0 no es vuelta; un muro C con GexM redondeado tampoco)",
                   string.Join("; ", mal));
                var fv = FotosDoble.Copia(f, x => x.Actuales = new List<NivelActual> { casos[5].N });
                var gA = new GraficoDoble(ahora) { Alto = 31600, Bajo = 30900, Abierto = true }; foreach (var c in SeriesPantalla.Casillas) gA.A.Visibles.Add(c.Id);
                var dv = Armar(fv, gA);
                string hh = cb.AddSeconds(-900).ToString("HH:mm", CultureInfo.InvariantCulture) + "→" + cb.ToString("HH:mm", CultureInfo.InvariantCulture);
                Ok(dv.Panel.Contains("    vol 15 min " + hh + " UTC: ♦ dio vuelta +10M (antes " + M + "7,0M)"),
                   "C77b serie por volumen que dio vuelta: 'vol 15 min " + hh + " UTC: ♦ dio vuelta +10M (antes −7,0M)'", string.Join(" | ", dv.Panel.Where(l => l.StartsWith("    "))));
            }

            // C78 el grafico del operador (pythiagex4-pantalla.log, render 4.1.2 de las 00:08: area 852 px, clip.derecha 781). La pestaña se parte en el
            // ancho VISIBLE: ningun texto pasa de x = 781, la caja tampoco, cada grupo lleva su precio en el PRIMER renglon y no se pierde ninguna palabra
            // (leyenda, notas de OI de la FAM, estado de cambios). Antes: la leyenda (127 letras = 846 px) se cortaba en "(no dice si abre" y el
            // precio de "[6,9 h] QQQ M-·OI (OI 2s) −167M ±20 / QQQ muro C/muro P·vol ... 31.076" (131 letras) quedaba afuera.
            {
                const int CLIP = 781;
                DateTime viejo = ahora.AddHours(-6.9);
                var oiR1 = new DateTime(2026, 10, 7, 12, 49, 0, DateTimeKind.Utc); var oiR0 = new DateTime(2026, 10, 8, 8, 49, 0, DateTimeKind.Utc);
                const string notaQ = "OI anterior visto desde 10-07 12:49Z (su salto quedo fuera de memoria)";
                const string notaF = "OI por libro: NQ 10-09 01:48Z vs 10-08 01:30Z; NDX 10-08 06:31Z vs 10-07 12:50Z o antes; QQQ 10-08 08:49Z vs 10-07 12:49Z o antes";
                const string estadoNq = "NQ: vol 5 min (sin foto de hace 5 min (la anterior es de hace 53 min)) · 15 min (sin foto de hace 15 min (la anterior es de hace 53 min)) · "
                                      + "30 min (sin foto de hace 30 min (la anterior es de hace 53 min)); OI leyendo la historia de OI (160 fotos hasta 10-08 01:28Z)";
                var c5 = new[] { 300.0, 900.0, 1800.0 }; var cob1 = new[] { 1.0, 1.0, 1.0 };
                var largos = new List<NivelActual>
                {
                    FotosDoble.ComoCambios(FotosDoble.Oi(A("MAJORS_QQQ_oi", "QQQ", "oi", "MAJORS", "M-", 31076.37, 750, 20, viejo, gexM: -167, oiViejo: true), 77, oiR1, oiR0, nota: notaQ)),
                    FotosDoble.ComoCambios(FotosDoble.Vol(A("MUROS_QQQ_vol", "QQQ", "vol", "MUROS", "muro C", 31076.37, 750, 20, viejo, gexM: 2620), viejo, new[] { 0.0, 0.0, 0.0 }, c5, cob1, "cadena congelada")),
                    FotosDoble.ComoCambios(FotosDoble.Vol(A("MUROS_QQQ_vol", "QQQ", "vol", "MUROS", "muro P", 31076.37, 750, 20, viejo, gexM: -2200), viejo, new[] { 0.0, 0.0, 0.0 }, c5, cob1, "cadena congelada")),
                    FotosDoble.ComoCambios(FotosDoble.Oi(A("MUROS_QQQ_oi", "QQQ", "oi", "MUROS", "muro C", 31076.37, 750, 20, viejo, gexM: 344, oiViejo: true), -7.0, oiR1, oiR0, nota: notaQ)),
                    FotosDoble.ComoCambios(FotosDoble.Oi(A("MUROS_QQQ_oi", "QQQ", "oi", "MUROS", "muro P", 31076.37, 750, 20, viejo, gexM: -511, oiViejo: true), -70, oiR1, oiR0, nota: notaQ)),
                    FotosDoble.ComoCambios(FotosDoble.Oi(A("FAM_MUROS_oi", "familia", "oi", "MUROS", "muro C", 31045, double.NaN, 0, viejo, gexM: 350, oiViejo: true), -5.7, oiR1, oiR0, nota: notaF)),
                };
                var fOp = FotosDoble.Copia(f, x => { x.Actuales = f.Actuales.Concat(largos).ToList(); x.CambiosEstado = f.CambiosEstado.Concat(new[] { estadoNq }).ToArray(); });
                var gOp = new GraficoDoble(ahora) { AreaR = new Rectangle(0, 0, 852, 581), Abierto = true }; gOp.A.Visibles.Add("MUROS_QQQ_vol");
                var dOp = Armar(fOp, gOp, null, CLIP);
                var txOp = dOp.Prims.Where(p => p.Tipo == TipoPrimPantalla.Texto).ToList();
                var pasan = txOp.Where(p => p.R.X + Medir(p.T, p.A).Width > CLIP).Select(p => "'" + p.T + "' hasta x=" + (p.R.X + Medir(p.T, p.A).Width)).ToList();
                var caja = dOp.Prims.Where(p => p.Tipo == TipoPrimPantalla.Relleno && p.C == Color.FromArgb(235, ArmadoPantalla.ColFondo)).ToList();
                Console.WriteLine("     detalle a 852 px con clip 781 (" + dOp.Panel.Count + " renglones):"); foreach (var l in dOp.Panel) Console.WriteLine("       |" + l);
                Ok(pasan.Count == 0 && caja.Count == 1 && caja[0].R.Right <= CLIP,
                   "C78 area 852 px y clip 781 (el grafico del operador): ningun texto ni la caja de la pestaña pasan del borde visible (" + txOp.Count + " textos; caja hasta x=" + (caja.Count > 0 ? caja[0].R.Right : -1) + ")",
                   string.Join("; ", pasan.Take(5)));
                string todo = Regex.Replace(string.Join(" ", dOp.Panel), @"\s+", " ");
                var faltan = new[] { "(no dice si abren o cierran) · OI: posiciones vs la publicación anterior", notaF, notaQ, estadoNq, "gamma de ahora: el precio no lo mueve",
                                     "sin validar: describe, no anticipa · clic en la pestaña para cerrar" }.Where(s => !todo.Contains(Regex.Replace(s, @"\s+", " "))).ToList();
                Ok(faltan.Count == 0, "C78b partido en renglones no se pierde ninguna palabra (leyenda, nota de OI por libro de la FAM, estado de cambios de 270 letras)", string.Join(" | ", faltan));
                int iG = dOp.Panel.FindIndex(l => l.StartsWith("[6,9 h] 31.076  QQQ M-·OI (OI 2s) " + M + "167M ±20 / "));
                Ok(iG >= 0 && dOp.Panel[iG + 1].StartsWith("  ") && !dOp.Panel[iG + 1].StartsWith("    "),
                   "C78c el grupo largo de 31.076 (M- OI + C/P vol + C/P OI) lleva el precio en su PRIMER renglon ('[6,9 h] 31.076  QQQ M-·OI ...') y sigue con sangria",
                   iG < 0 ? "no esta" : "'" + dOp.Panel[iG + 1] + "'");
                int iFam = dOp.Panel.FindIndex(l => l.StartsWith("[6,9 h] 31.045  FAM muro C·OI (OI 2s) +350M"));
                Ok(iFam >= 0 && dOp.Panel[iFam + 1].StartsWith("    OI 10-08 08:49Z vs 10-07 12:49Z: ▼5,7M") && dOp.Panel[iFam + 2].StartsWith("      "),
                   "C78d el renglon de cambio de la FAM con su nota larga sigue debajo con sangria de 6 (continuacion del sub-renglon)",
                   iFam < 0 ? "no esta" : string.Join(" | ", dOp.Panel.Skip(iFam).Take(4)));
                // el mismo detalle en un grafico ancho (area y clip 4000 px): los renglones son los de siempre (no se parte nada)
                var gAncho = new GraficoDoble(ahora) { AreaR = new Rectangle(0, 0, 4000, 581), Abierto = true }; gAncho.A.Visibles.Add("MUROS_QQQ_vol");
                var dAncho = Armar(fOp, gAncho, null, 4000);
                Ok(dAncho.Panel.Any(l => l == "    " + "QQQ muro C/muro P·OI (OI 2s) muro P: OI 10-08 08:49Z vs 10-07 12:49Z: ▲70M (OI de 2 sesiones: no va en la etiqueta) · " + notaQ)
                   && dAncho.Panel.Count < dOp.Panel.Count,
                   "C78e con lugar de sobra (clip 4000) no se parte nada: " + dAncho.Panel.Count + " renglones contra " + dOp.Panel.Count + " a 781 px");
            }

            // C79 Partir: pedazos <= n letras, sangria en la continuacion, corte duro sin espacios, nada perdido
            {
                var sal = new List<(string T, Color C)>();
                string largo = "cambios NQ: vol 5 min (sin foto de hace 5 min (la anterior es de hace 53 min)) · 15 min (sin foto de hace 15 min (la anterior es de hace 53 min)) · 30 min (sin foto de hace 30 min); OI 10-08 01:30Z vs 10-07 22:41Z o antes (49 de 246 claves)";
                ArmadoPantalla.Partir(largo, Color.Red, 60, sal);
                bool ok1 = sal.Count >= 4 && sal.All(x => x.T.Length <= 60 && x.C == Color.Red) && sal.Skip(1).All(x => x.T.StartsWith("  ") && !x.T.StartsWith("   "))
                           && string.Join(" ", sal.Select(x => x.T.Trim())) == largo;
                var sal2 = new List<(string T, Color C)>(); string sinEsp = "    " + new string('x', 150);
                ArmadoPantalla.Partir(sinEsp, Color.Red, 50, sal2);
                bool ok2 = sal2.All(x => x.T.Length <= 50) && string.Concat(sal2.Select(x => x.T.Trim())) == sinEsp.Trim() && sal2.Skip(1).All(x => x.T.StartsWith("      "));
                var sal3 = new List<(string T, Color C)>(); ArmadoPantalla.Partir("corto", Color.Red, 60, sal3); ArmadoPantalla.Partir("", Color.Red, 60, sal3); ArmadoPantalla.Partir(null, Color.Red, 60, sal3);
                bool ok3 = sal3.Count == 3 && sal3[0].T == "corto" && sal3[1].T == "" && sal3[2].T == "";
                Ok(ok1 && ok2 && ok3, "C79 Partir: pedazos <= n letras, continuacion con la sangria + 2, corte duro sin espacios, sin perder letras; corto/vacio/null quedan igual",
                   "ok1=" + ok1 + " ok2=" + ok2 + " ok3=" + ok3 + " | " + string.Join(" | ", sal.Select(x => x.T)));
            }

            // C80 CambiosEstado que ya empieza con "cambios" no se repite ("cambios cambios: sin minuto ...", visto en la revision)
            {
                var fc = FotosDoble.Copia(f, x => x.CambiosEstado = new[] { "cambios: sin minuto con libros todavia", "cambios de OI: leyendo la historia de las fuentes (sigue en el proximo paso)",
                                                                            "NQ: vol 15 min 18:44-18:59Z" });
                var gc = new GraficoDoble(ahora) { Abierto = true };
                var dc = Armar(fc, gc);
                Ok(dc.Panel.Contains("cambios: sin minuto con libros todavia") && dc.Panel.Contains("cambios de OI: leyendo la historia de las fuentes (sigue en el proximo paso)")
                   && dc.Panel.Contains("cambios NQ: vol 15 min 18:44-18:59Z") && !dc.Panel.Any(l => l.StartsWith("cambios cambios")),
                   "C80 el estado de CambiosFamilia que ya dice 'cambios' no sale 'cambios cambios'");
            }
        }

        // ------------------------------------------------------------------ T (4.1.4): rotulos de los tramos de historia
        /// <summary>Historia por vela m2 desde una funcion de la vela del grafico doble (la primera vela de cada m2 manda; m2 = piso de LastTime).</summary>
        static Dictionary<long, IReadOnlyDictionary<string, double[]>> HistT(GraficoDoble gr, Func<int, Dictionary<string, double[]>> f)
        {
            var h = new Dictionary<long, IReadOnlyDictionary<string, double[]>>();
            for (int b = 0; b < gr.N; b++)
            {
                gr.Vela(b, out long ms, out _); long m2 = ms / 120000 * 120000;
                if (h.ContainsKey(m2)) continue;
                var e = f(b); if (e != null && e.Count > 0) h[m2] = e;
            }
            return h;
        }

        static Dictionary<string, double[]> S(params (string Id, double[] P)[] x) { var d = new Dictionary<string, double[]>(); foreach (var (id, p) in x) if (p != null && p.Length > 0) d[id] = p; return d; }
        static double[] Pr(params double[] p) => p;

        /// <summary>La foto normal con otra historia y otros niveles actuales (anotados como CambiosFamilia).</summary>
        static FotoFamilia FotoT(DateTime ahora, Dictionary<long, IReadOnlyDictionary<string, double[]>> hist, params NivelActual[] act)
            => FotosDoble.Copia(FotosDoble.Normal(ahora), x => { x.HistoriaM2 = hist; x.Actuales = act.Select(FotosDoble.ComoCambios).ToList(); });

        static GraficoDoble GrT(DateTime ahora, params string[] visibles)
        {
            var gr = new GraficoDoble(ahora); gr.A.Visibles.Clear(); foreach (var v in visibles) gr.A.Visibles.Add(v); gr.A.Eje = EjeFamilia4.Ninguno; return gr;
        }

        /// <summary>Las cajas de las etiquetas de la columna de la derecha (las de adentro: caja; las de afuera: renglon) con el tic de 6 px.</summary>
        static List<Rectangle> CajasColumna(DibujoPantalla d)
        {
            int hC = Medir("X", 8).Height + 3;
            return d.Rotulos.Select(r => r.Fuera == 0 ? new Rectangle(r.X0 - 7, r.Y - hC / 2, r.Ancho + 7, hC) : new Rectangle(r.X0 - 1, r.Y, r.Ancho + 2, hC)).ToList();
        }

        static string Rts(DibujoPantalla d) => d.RotulosTramos.Count == 0 ? "(ninguno)" : string.Join(" | ", d.RotulosTramos.Select(r => "'" + r.Txt + "' fin vela " + r.VelaFin + " largo " + r.Largo + " caja " + r.Caja));

        static void PruebaTramos()
        {
            Console.WriteLine("T. 4.1.4: rotulos de los tramos de historia (pedido del operador 09-10: 'la doble o triple raya no tiene rotulo/etiqueta y es importante')");
            var ahora = new DateTime(2026, 10, 8, 19, 0, 0, DateTimeKind.Utc);
            DateTime cb = ahora.AddMinutes(-16), nq = ahora.AddSeconds(-40);
            const string M = "−";
            int hR = Medir("X", 7).Height;

            // T1 deteccion: un tramo de 60 velas en 31.040,74 que deja de ser el vigente (la serie pasa a 31.090, su nivel de ahora)
            {
                var gr = GrT(ahora, "MUROS_NDX_vol");
                var h = HistT(gr, b => S(("MUROS_NDX_vol", b < 260 ? Pr(31040.74) : Pr(31090.0))));
                var f = FotoT(ahora, h, A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro P", 31090, 30850, 0, cb, gexM: -250));
                var d = Armar(f, gr);
                Console.WriteLine("     T1 rotulos: " + Rts(d));
                var r = d.RotulosTramos.FirstOrDefault();
                int xFinEsp = gr.XDeBarra(259) + Math.Max(2, (int)Math.Round(10 * 0.9)) / 2 + 1, yLinea = gr.YDePrecio(31040.74);
                Ok(d.RotulosTramos.Count == 1 && r.Txt == "NDX muro V 31.040,74" && r.VelaFin == 59 && r.VelaIni == 0 && r.Largo == 60 && r.XFin == xFinEsp
                   && r.Caja.Right == xFinEsp + 2 && r.Caja.Bottom <= yLinea - 1 && r.Caja.Bottom >= yLinea - 3 && d.TramosLargos == 2 && d.TramosVigentes == 1,
                   "T1 un tramo de 60 velas que ya no es el vigente: 'NDX muro V 31.040,74' al FINAL del tramo (termina en su x), arriba de la raya; el vigente (31.090) no lleva rotulo", Rts(d));
                var tx = d.Prims.Where(p => p.Tipo == TipoPrimPantalla.Texto && p.T == "NDX muro V").ToList();
                Ok(tx.Count == 1 && tx[0].C == SeriesPantalla.ColorDe("#2fb3a8") && tx[0].A == 7f && d.Prims.Any(p => p.Tipo == TipoPrimPantalla.Texto && p.T == "NDX muro V 31.040,74" && p.C == Color.FromArgb(200, ArmadoPantalla.ColFondo))
                   && d.Prims.Any(p => p.Tipo == TipoPrimPantalla.Relleno && p.R == r.Caja && p.C.A == 120),
                   "T1b letra tamC - 1 (7 a letra 9), en el color de la serie (#2fb3a8), con sombra y caja tenue");
            }

            // T2 huecos: hasta 3 velas sin dato el tramo sigue; con 4, son dos tramos
            {
                var gr = GrT(ahora, "MUROS_NDX_vol");
                var act = A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro P", 31090, 30850, 0, cb, gexM: -250);
                var d2 = Armar(FotoT(ahora, HistT(gr, b => b >= 230 && b <= 231 ? S() : S(("MUROS_NDX_vol", b < 260 ? Pr(31040.0) : Pr(31090.0)))), act), gr);
                var d4 = Armar(FotoT(ahora, HistT(gr, b => b >= 230 && b <= 233 ? S() : S(("MUROS_NDX_vol", b < 260 ? Pr(31040.0) : Pr(31090.0)))), act), gr);
                Ok(d2.RotulosTramos.Count == 1 && d2.RotulosTramos[0].Largo == 60 && d4.RotulosTramos.Count == 2 && d4.RotulosTramos.All(x => x.Txt == "NDX muro V 31.040")
                   && d4.RotulosTramos.Select(x => x.VelaFin).OrderBy(x => x).SequenceEqual(new[] { 29, 59 }),
                   "T2 hueco de 2 velas (una m2 sin dato): un tramo de 60; hueco de 4 velas: dos tramos (terminan en las velas 29 y 59), cada uno con su rotulo", Rts(d2) + " || " + Rts(d4));
            }

            // T3 largo minimo: 14 velas no se rotula, 16 si
            {
                var gr = GrT(ahora, "MUROS_NDX_vol");
                var act = A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro P", 31090, 30850, 0, cb, gexM: -250);
                var d14 = Armar(FotoT(ahora, HistT(gr, b => b < 246 ? S() : S(("MUROS_NDX_vol", b < 260 ? Pr(31040.0) : Pr(31090.0)))), act), gr);
                var d16 = Armar(FotoT(ahora, HistT(gr, b => b < 244 ? S() : S(("MUROS_NDX_vol", b < 260 ? Pr(31040.0) : Pr(31090.0)))), act), gr);
                Ok(d14.RotulosTramos.Count == 0 && d16.RotulosTramos.Count == 1 && d16.RotulosTramos[0].Largo == 16,
                   "T3 tramo de 14 velas: sin rotulo; de 16 (>= " + ArmadoPantalla.TRAMO_MIN + "): con rotulo", Rts(d14) + " || " + Rts(d16));
            }

            // T4 el vigente no se rotula; si la serie ya se movio (su nivel de ahora es otro) o el grafico mira velas viejas, si
            {
                var gr = GrT(ahora, "MUROS_NDX_vol");
                var h = HistT(gr, b => S(("MUROS_NDX_vol", Pr(31040.74))));
                var dv = Armar(FotoT(ahora, h, A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro P", 31040.74, 30800, 0, cb, gexM: -250)), gr);
                var dm = Armar(FotoT(ahora, h, A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro P", 31045.0, 30805, 0, cb, gexM: -250)), gr);
                gr.Ultima = 280; var dvj = Armar(FotoT(ahora, h, A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro P", 31040.74, 30800, 0, cb, gexM: -250)), gr); gr.Ultima = 299;
                Ok(dv.RotulosTramos.Count == 0 && dv.TramosVigentes == 1 && dv.Etiquetas.Any(e => e.Txt == "NDX P " + M + "250M 31.040,74 V")
                   && dm.RotulosTramos.Count == 1 && dm.RotulosTramos[0].VelaFin == 99
                   && dvj.RotulosTramos.Count == 1 && dvj.RotulosTramos[0].VelaFin == 80 && dvj.Etiquetas.Count == 0,
                   "T4 el tramo vigente (llega a la ultima vela con su nivel de ahora) no se rotula: lo nombra la columna; con el nivel de ahora en otro precio, si; mirando velas viejas (sin columna), si, al borde",
                   Rts(dv) + " || " + Rts(dm) + " || " + Rts(dvj));
            }

            // T5 juntar: rayas de series distintas a <= 1,5 pt con el final a <= 40 px -> un solo rotulo, cada nombre en su color, precio redondeado
            {
                var gr = GrT(ahora, "MUROS_NDX_vol", "MUROS_NDX_oi", "R20_NDX_vol");
                NivelActual[] act =
                {
                    A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro P", 31090, 30850, 0, cb, gexM: -250), A("MUROS_NDX_oi", "NDX", "oi", "MUROS", "muro P", 31091, 30851, 0, cb, gexM: -90),
                    A("R20_NDX_vol", "NDX", "vol", "DOMS", "D1", 31092, 30850, 0, cb, gexM: 140),
                };
                Func<int, int, Dictionary<string, double[]>> hist = (b, finR20) => S(("MUROS_NDX_vol", b < 260 ? Pr(31040.43) : Pr(31090.0)), ("MUROS_NDX_oi", b < 260 ? Pr(31040.43) : Pr(31091.0)),
                                                                                      ("R20_NDX_vol", b < finR20 ? Pr(31041.83) : Pr(31092.0)));
                var d = Armar(FotoT(ahora, HistT(gr, b => hist(b, 260)), act), gr);
                var r = d.RotulosTramos.FirstOrDefault();
                var tx = d.Prims.Where(p => p.Tipo == TipoPrimPantalla.Texto && p.A == 7f).ToList();
                int i1 = tx.FindIndex(p => p.T == "NDX muro V/OI");
                Ok(d.RotulosTramos.Count == 1 && r.Txt == "NDX muro V/OI · 2.0 NDX 31.040" && r.Series.SequenceEqual(new[] { "MUROS_NDX_vol", "MUROS_NDX_oi", "R20_NDX_vol" }) && r.Tramos == 3
                   && i1 > 0 && tx[i1].C == SeriesPantalla.ColorDe("#2fb3a8") && tx[i1 + 1].T == " · " && tx[i1 + 2].T == "2.0 NDX" && tx[i1 + 2].C == SeriesPantalla.ColorDe("#3ddc97") && tx[i1 + 3].T == " 31.040",
                   "T5 la doble/triple raya (NDX muro V y OI en 31.040,43, 2.0 NDX en 31.041,83) termina junta: UN rotulo 'NDX muro V/OI · 2.0 NDX 31.040', cada nombre en el color de su serie", Rts(d));
                var d40 = Armar(FotoT(ahora, HistT(gr, b => hist(b, 264)), act), gr);       // el 2.0 NDX termina 4 velas (40 px) despues: misma raya
                var gr2 = GrT(ahora, "MUROS_NDX_vol", "MUROS_NDX_oi", "R20_NDX_vol");
                var dSep = Armar(FotoT(ahora, HistT(gr2, b => S(("MUROS_NDX_vol", b < 260 ? Pr(31040.43) : Pr(31090.0)), ("MUROS_NDX_oi", b < 260 ? Pr(31040.43) : Pr(31091.0)),
                                                              ("R20_NDX_vol", b < 260 ? Pr(31045.0) : Pr(31092.0)))), act), gr2);   // 4,6 pt: rayas distintas
                Ok(d40.RotulosTramos.Count == 1 && d40.RotulosTramos[0].Txt == "NDX muro V/OI · 2.0 NDX 31.042" && d40.RotulosTramos[0].VelaFin == 63
                   && dSep.RotulosTramos.Count == 2 && dSep.RotulosTramos.Any(x => x.Txt == "NDX muro V/OI 31.040") && dSep.RotulosTramos.Any(x => x.Txt == "2.0 NDX 31.045"),
                   "T5b si la raya sigue 4 velas mas con otra serie, un solo rotulo al final de la raya; a 4,6 pt son rayas distintas (dos rotulos)", Rts(d40) + " || " + Rts(dSep));
            }

            // T6 la MISMA serie en la misma raya (muro C y muro P en el mismo strike): el C se va y el P sigue -> sin rotulo a mitad de la raya
            {
                var gr = GrT(ahora, "MUROS_NDX_oi");
                var h = HistT(gr, b => S(("MUROS_NDX_oi", b < 240 ? Pr(31041.19, 31041.19) : b < 280 ? Pr(31041.19) : Pr(31100.0, 31060.0))));
                var d = Armar(FotoT(ahora, h, A("MUROS_NDX_oi", "NDX", "oi", "MUROS", "muro C", 31100, 30860, 0, cb, gexM: 90), A("MUROS_NDX_oi", "NDX", "oi", "MUROS", "muro P", 31060, 30820, 0, cb, gexM: -80)), gr);
                Ok(d.RotulosTramos.Count == 1 && d.RotulosTramos[0].Txt == "NDX muro OI 31.041,19" && d.RotulosTramos[0].VelaFin == 79 && d.RotulosTramos[0].Tramos == 2,
                   "T6 muro C y P de NDX OI en el mismo strike: el C se va en la vela 39 y el P sigue hasta la 79: un solo rotulo, al final de la raya", Rts(d));
            }

            // T7 sin choques: 6 rayas a 3 pt (10,5 px) que terminan juntas + una que termina debajo de la columna de etiquetas
            {
                string[] ss = { "MUROS_NDX_vol", "MUROS_NDX_oi", "MAJORS_NDX_vol", "MAJORS_NDX_oi", "ZTP_NDX_vol", "MUROS_NQ_oi", "MAJORS_NQ_oi" };
                var gr = GrT(ahora, ss); gr.PxVela = 11;          // 11 px por vela: la ultima vela cae debajo de la columna de etiquetas
                var h = HistT(gr, b =>
                {
                    var e = new Dictionary<string, double[]>();
                    for (int k = 0; k < 6; k++) e[ss[k]] = b < 260 ? Pr(31040.0 + 3 * k) : Pr(31100.0 + 2 * k);
                    e[ss[6]] = b < 296 ? Pr(31070.0) : Pr(30960.0);
                    return e;
                });
                var act = new List<NivelActual>();
                for (int k = 0; k < 6; k++) act.Add(A(ss[k], k == 5 ? "NQ" : "NDX", k == 1 || k == 3 || k == 5 ? "oi" : "vol", k == 2 || k == 3 ? "MAJORS" : k == 4 ? "ZTP" : "MUROS",
                                                      k == 4 ? "cruce arriba" : k == 2 || k == 3 ? "M+" : "muro C", 31100.0 + 2 * k, 30860 + 2 * k, 0, k == 5 ? nq : cb, gexM: k == 4 ? double.NaN : 50 + k));
                act.Add(A("MAJORS_NQ_oi", "NQ", "oi", "MAJORS", "M-", 30960, 30960, 0, nq, gexM: -30));
                var d = Armar(FotoT(ahora, h, act.ToArray()), gr);
                Console.WriteLine("     T7 rotulos: " + Rts(d) + " | descartados " + d.TramosDescartados);
                var cajas = d.RotulosTramos.Select(x => x.Caja).ToList();
                var col = CajasColumna(d);
                int colIzq = col.Count == 0 ? int.MaxValue : col.Min(c => c.Left);
                bool sinChoque = true;
                for (int i = 0; i < cajas.Count; i++) for (int j = i + 1; j < cajas.Count; j++) if (cajas[i].IntersectsWith(cajas[j])) sinChoque = false;
                bool sinColumna = cajas.All(c => c.Right <= colIzq - 2 && !col.Any(o => o.IntersectsWith(c))) && cajas.All(c => !c.IntersectsWith(d.Pestana));
                var r7 = d.RotulosTramos.FirstOrDefault(x => x.Series.Contains("MAJORS_NQ_oi"));
                Ok(sinChoque && sinColumna && d.RotulosTramos.Count + d.TramosDescartados == 7 && d.RotulosTramos.Count >= 3 && r7 != null && r7.Caja.Right <= colIzq - 2,
                   "T7 siete rotulos que caen juntos: " + d.RotulosTramos.Count + " puestos sin pisarse (arriba, abajo o un renglon mas), " + d.TramosDescartados + " descartados; ninguno sobre la columna de "
                   + "etiquetas ni sobre la pestaña; el que termina debajo de la columna ('" + r7?.Txt + "') se corre a su izquierda", Rts(d));
            }

            // T8 tope: 20 tramos -> 14 rotulos, los de los tramos mas largos
            {
                string[] ss = { "MUROS_NDX_vol", "MUROS_NDX_oi", "MAJORS_NDX_vol", "ZTP_NDX_vol" };
                var gr = GrT(ahora, ss);
                var h = HistT(gr, b =>
                {
                    var e = new Dictionary<string, double[]>();
                    if (b < 200) return e;
                    int i = (b - 200) / 20, o = (b - 200) % 20;
                    for (int j = 0; j < 4; j++) { int L = 16 + 2 * ((i + j) % 2); if (o < L) e[ss[j]] = Pr(30920.0 + 45 * j + 2 * i); }
                    return e;
                });
                var d = Armar(FotoT(ahora, h, A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro P", 31095, 30855, 0, cb, gexM: -250)), gr);
                Ok(d.RotulosTramos.Count == ArmadoPantalla.TRAMO_TOPE && d.TramosDescartados == 6 && d.RotulosTramos.Count(x => x.Largo == 18) == 10 && d.RotulosTramos.Count(x => x.Largo == 16) == 4,
                   "T8 20 tramos (10 de 18 velas, 10 de 16): tope " + ArmadoPantalla.TRAMO_TOPE + " rotulos = los 10 de 18 + 4 de 16; 6 descartados", Rts(d));
            }

            // T9 apagado (Tramos41Rotulos = false) y sin estela: ningun rotulo y el resto igual
            {
                var gr = GrT(ahora, "MUROS_NDX_vol");
                var f = FotoT(ahora, HistT(gr, b => S(("MUROS_NDX_vol", b < 260 ? Pr(31040.74) : Pr(31090.0)))), A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro P", 31090, 30850, 0, cb, gexM: -250));
                var dOn = Armar(f, gr);
                gr.A.Tramos = false; var dOff = Armar(f, gr); gr.A.Tramos = true;
                gr.A.Estela = false; var dSin = Armar(f, gr); gr.A.Estela = true;
                var tOn = dOn.Prims.Where(p => p.Tipo == TipoPrimPantalla.Texto && p.A != 7f).Select(p => p.T).ToList();
                var tOff = dOff.Prims.Where(p => p.Tipo == TipoPrimPantalla.Texto).Select(p => p.T).ToList();
                Ok(dOn.RotulosTramos.Count == 1 && dOff.RotulosTramos.Count == 0 && dSin.RotulosTramos.Count == 0 && dOff.Rayitas == dOn.Rayitas && tOn.SequenceEqual(tOff)
                   && dOff.Prims.Count == dOn.Prims.Count - 1 - 1 - 2,
                   "T9 Tramos41Rotulos apagado: ningun rotulo y el resto del dibujo identico (mismas rayitas y textos; solo faltan la caja, la sombra y los 2 tramos del rotulo); sin estela tampoco");
            }

            // T10 nombres cortos de las 33 series
            {
                var cp = new CatalogoPantalla(CatalogoFamilia.Series);
                Console.WriteLine("     T10 nombres: " + string.Join(" | ", cp.Lista.Select(s => s.Id + "=" + (cp.NombreTramo[s.Id].Base + " " + cp.NombreTramo[s.Id].Suf).Trim())));
                var esp = new Dictionary<string, string>
                {
                    ["MUROS_NDX_vol"] = "NDX muro V", ["MUROS_NDX_oi"] = "NDX muro OI", ["ZTP_NDX_vol"] = "NDX cruce", ["R20_NDX_vol"] = "2.0 NDX", ["R20_QQQ_vol"] = "2.0 QQQ", ["DOMS_QQQ_vol"] = "QQQ dom",
                    ["FAM_MUROS_oi"] = "FAM muro OI", ["TRES_NDX"] = "3.0 NDX", ["T_MUROS_oi"] = "TQQQ muro OI", ["ZEST_NQ_oi"] = "NQ 0G OI", ["ZEST_QQQ_vol"] = "QQQ 0G", ["CONF_vol"] = "CONF",
                    ["T_ZERO_oi"] = "TQQQ 0G", ["T_DOMS_raz"] = "TQQQ razon", ["MAJORS_QQQ_vol"] = "QQQ major V", ["MAJORS_NQ_oi"] = "NQ major OI", ["T_DOMS_vol"] = "TQQQ dom",
                };
                var mal = esp.Where(kv => (cp.NombreTramo[kv.Key].Base + " " + cp.NombreTramo[kv.Key].Suf).Trim() != kv.Value).Select(kv => kv.Key).ToList();
                Ok(mal.Count == 0 && cp.Lista.All(s => cp.NombreTramo[s.Id].Base != ""), "T10 nombre corto de cada serie (V/OI solo si la serie existe por volumen y por OI)", string.Join(",", mal));
            }

            // T11 PantallaFamilia los pinta (llegan al RenderContext)
            {
                var gr = GrT(ahora, "MUROS_NDX_vol");
                var f = FotoT(ahora, HistT(gr, b => S(("MUROS_NDX_vol", b < 260 ? Pr(31040.74) : Pr(31090.0)))), A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro P", 31090, 30850, 0, cb, gexM: -250));
                var p = new PantallaFamilia(gr); var g = new RenderDoble(); p.Pintar(g, f);
                Ok(p.UltimoDibujo.RotulosTramos.Count == 1 && g.TextosDet.Count(t => t.S == "NDX muro V 31.040,74") == 1 && g.TextosDet.Any(t => t.S == "NDX muro V" && t.F == 7f),
                   "T11 PantallaFamilia: el rotulo llega al RenderContext (sombra con el texto entero y los tramos de color)");
            }

            // T13 una raya con 6 series: como mucho 4 nombres (los de las series que mas velas dibujaron en ella), en el orden del catalogo, sin "+N"
            {
                var ss = new (string Id, double P, int Desde)[] { ("MUROS_NDX_vol", 31040.0, 200), ("R20_NDX_vol", 31040.3, 206), ("MAJORS_NDX_vol", 31040.6, 212), ("ZTP_NDX_vol", 31040.9, 218),
                                                                   ("MUROS_NQ_oi", 31041.2, 224), ("TRES_NDX", 31041.5, 230) };
                var gr = GrT(ahora, ss.Select(x => x.Id).ToArray());
                var h = HistT(gr, b => { var e = new Dictionary<string, double[]>(); foreach (var x in ss) if (b >= x.Desde && b < 260) e[x.Id] = Pr(x.P); else if (b >= 260) e[x.Id] = Pr(x.P + 50); return e; });
                var act = ss.Select(x => A(x.Id, x.Id.Contains("NQ_") ? "NQ" : "NDX", x.Id == "TRES_NDX" ? "3.0" : x.Id.EndsWith("_oi") ? "oi" : "vol", CatalogoFamilia.PorId[x.Id].Tipo,
                                           x.Id.StartsWith("MUROS") ? "muro C" : x.Id.StartsWith("MAJORS") ? "M+" : x.Id.StartsWith("ZTP") ? "cruce arriba" : "D1", x.P + 50, double.NaN, 0, cb, gexM: 10)).ToArray();
                var d = Armar(FotoT(ahora, h, act), gr);
                var r = d.RotulosTramos.FirstOrDefault();
                Ok(d.RotulosTramos.Count == 1 && r.Txt == "NDX muro V · NDX major V · NDX cruce · 2.0 NDX 31.040" && r.Series.SequenceEqual(new[] { "MUROS_NDX_vol", "MAJORS_NDX_vol", "ZTP_NDX_vol", "R20_NDX_vol" }) && r.Tramos == 6,
                   "T13 una raya con 6 series (de 60 a 30 velas): un rotulo con los " + ArmadoPantalla.TRAMO_PIEZAS + " nombres de las que mas velas dibujaron, en el orden del catalogo: '" + r?.Txt + "'", Rts(d));
            }

            PruebaTramosRevision();
            PruebaTramosReal();
        }

        /// <summary>T14-T20 (revision 4.1.4, hallazgos del revisor sobre los rotulos de tramos): absorber solo la misma serie en el mismo precio, costo
        /// acotado, largo minimo tambien en velas m2, la franja de MargenSup.</summary>
        static void PruebaTramosRevision()
        {
            Console.WriteLine("T14+. revision 4.1.4 de los rotulos de tramos");
            var ahora = new DateTime(2026, 10, 8, 19, 0, 0, DateTimeKind.Utc);
            DateTime cb = ahora.AddMinutes(-16);
            const string M = "−";
            int w9 = Math.Max(2, (int)Math.Round(10 * 0.9)) / 2 + 1;           // medio ancho de la rayita + 1 (PxVela 10)

            // T14 (hallazgo 1a, caso S1 del revisor): el 2.0 NDX (31.041,83) termina en la vela 59 ADENTRO de la raya vigente del muro NDX V (31.040,74,
            //     a 1,09 pt): antes se absorbia en la vigente y quedaba sin nombre en toda la pantalla (la columna nombra solo el muro)
            {
                var gr = GrT(ahora, "MUROS_NDX_vol", "R20_NDX_vol");
                NivelActual[] act = { A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro P", 31040.74, 30800, 0, cb, gexM: -250), A("R20_NDX_vol", "NDX", "vol", "DOMS", "D1", 31092, 30850, 0, cb, gexM: 140) };
                Func<int, Func<int, Dictionary<string, double[]>>> h = fin => b => S(("MUROS_NDX_vol", Pr(31040.74)), ("R20_NDX_vol", b < fin ? Pr(31041.83) : Pr(31092.0)));
                var d = Armar(FotoT(ahora, HistT(gr, h(260)), act), gr);
                var r = d.RotulosTramos.FirstOrDefault(); int xFin = gr.XDeBarra(259) + w9;
                Ok(d.RotulosTramos.Count == 1 && r.Txt == "2.0 NDX 31.041,83" && r.VelaFin == 59 && r.XFin == xFin && r.Caja.Right == xFin + 2 && d.TramosVigentes == 2
                   && d.Etiquetas.Any(e => e.Txt == "NDX P " + M + "250M 31.040,74 V"),
                   "T14 el 2.0 NDX que termina en la vela 59 adentro de la raya VIGENTE del muro NDX V (a 1,09 pt) lleva SU rotulo '2.0 NDX 31.041,83' en SU final (la columna sigue nombrando el muro; vigentes: el muro y el 2.0 NDX de ahora en 31.092)", Rts(d));
                var d22 = Armar(FotoT(ahora, HistT(gr, h(222)), act), gr);
                var r22 = d22.RotulosTramos.FirstOrDefault(); int xFin22 = gr.XDeBarra(221) + w9;
                Ok(d22.RotulosTramos.Count == 1 && r22.Txt == "2.0 NDX 31.041,83" && r22.VelaFin == 21 && r22.Caja.Right == xFin22 + 2,
                   "T14b (caso S1b) el 2.0 NDX de las velas 0-21: rotulo en la vela 21 (x " + xFin22 + "), no al final de la otra raya", Rts(d22));
            }

            // T15 (hallazgo 1b, caso S2): un cruce de NDX (31.050,90) de las velas 0-21 adentro de un major NQ OI (31.050) que sigue hasta la vela 89 y ya
            //     no es vigente: antes el nombre del cruce iba al final del major (550 px despues); ahora cada uno en su final
            {
                var gr = GrT(ahora, "MAJORS_NQ_oi", "ZTP_NDX_vol");
                var hh = HistT(gr, b => S(("MAJORS_NQ_oi", b < 290 ? Pr(31050.0) : Pr(31000.0)), ("ZTP_NDX_vol", b < 222 ? Pr(31050.9) : Pr(31080.0 + (b / 2 % 7) * 0.9))));
                var d = Armar(FotoT(ahora, hh, A("MAJORS_NQ_oi", "NQ", "oi", "MAJORS", "M+", 31000, 31000, 0, ahora.AddSeconds(-40), gexM: 30),
                                    A("ZTP_NDX_vol", "NDX", "vol", "ZTP", "cruce abajo", 31080.0 + (299 / 2 % 7) * 0.9, double.NaN, 0, cb)), gr);
                var rc = d.RotulosTramos.FirstOrDefault(x => x.Series.Contains("ZTP_NDX_vol")); var rm = d.RotulosTramos.FirstOrDefault(x => x.Series.Contains("MAJORS_NQ_oi"));
                Ok(d.RotulosTramos.Count == 2 && rc != null && rc.Txt == "NDX cruce 31.050,90" && rc.VelaFin == 21 && rc.Caja.Right == gr.XDeBarra(221) + w9 + 2
                   && rm != null && rm.Txt == "NQ major OI 31.050" && rm.VelaFin == 89 && rm.Series.Count == 1,
                   "T15 un cruce que termina adentro de un major que sigue (y no es vigente): cada uno con su rotulo en su final ('NDX cruce 31.050,90' en la vela 21, 'NQ major OI 31.050' en la 89)", Rts(d));
            }

            // T16 la MISMA serie a mas de 0,5 pt (un escalon de 0,76 pt de la base) es otro tramo: cada uno con su rotulo (definicion del pedido: mismo precio
            //     a <= 0,5 pt); solo se absorbe la misma serie en el MISMO precio (T6, T20)
            {
                var gr = GrT(ahora, "MUROS_NDX_vol");
                var d = Armar(FotoT(ahora, HistT(gr, b => S(("MUROS_NDX_vol", b < 240 ? Pr(31040.43) : b < 260 ? Pr(31041.19) : Pr(31090.0)))),
                                    A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro P", 31090, 30850, 0, cb, gexM: -250)), gr);
                Ok(d.RotulosTramos.Count == 2 && d.RotulosTramos.Any(x => x.Txt == "NDX muro V 31.040,43" && x.VelaFin == 39) && d.RotulosTramos.Any(x => x.Txt == "NDX muro V 31.041,19" && x.VelaFin == 59),
                   "T16 la misma serie con un escalon de 0,76 pt: dos tramos, dos rotulos ('NDX muro V 31.040,43' en la vela 39 y 'NDX muro V 31.041,19' en la 59)", Rts(d));
            }

            // T20 muro C y P de NDX OI en el mismo strike que DERIVA con la base (0,02 pt por vela m2, como esta noche: 31.041,47 a las 22:02Z y 31.040,42
            //     a las 05:23Z); el C se va en la vela 39 y el P sigue y es el VIGENTE: el C no lleva rotulo (es la misma raya, la nombra la columna). Se
            //     compara con el precio del P cuando el C termina, no con el final (31.040,79 contra 31.040,19: 0,6 pt)
            {
                var gr = GrT(ahora, "MUROS_NDX_oi");
                Func<int, double> pP = b => Math.Round(31041.19 - 0.02 * ((b - 200) / 2), 2);
                var h = HistT(gr, b => S(("MUROS_NDX_oi", b < 240 ? Pr(pP(b), pP(b)) : Pr(pP(b)))));
                double pFin = pP(298);
                var d = Armar(FotoT(ahora, h, A("MUROS_NDX_oi", "NDX", "oi", "MUROS", "muro C", 31100, 30860, 0, cb, gexM: 90), A("MUROS_NDX_oi", "NDX", "oi", "MUROS", "muro P", pFin, 30800, 0, cb, gexM: -80)), gr);
                Ok(d.RotulosTramos.Count == 0 && d.TramosLargos == 2 && d.TramosVigentes == 1 && d.Etiquetas.Any(e => e.Txt == "NDX P " + M + "80M " + ArmadoPantalla.P(pFin) + " OI"),
                   "T20 muro C/P en el mismo strike que deriva 1 pt: el C (velas 0-39) es la misma raya que el P vigente: sin rotulo; la columna dice 'NDX P −80M " + ArmadoPantalla.P(pFin) + " OI'", Rts(d) + " largos " + d.TramosLargos + " vig " + d.TramosVigentes);
                var d2 = Armar(FotoT(ahora, h, A("MUROS_NDX_oi", "NDX", "oi", "MUROS", "muro C", 31100, 30860, 0, cb, gexM: 90), A("MUROS_NDX_oi", "NDX", "oi", "MUROS", "muro P", 31060, 30820, 0, cb, gexM: -80)), gr);
                Ok(d2.RotulosTramos.Count == 1 && d2.RotulosTramos[0].Txt == "NDX muro OI " + ArmadoPantalla.P(pFin) && d2.RotulosTramos[0].VelaFin == 99 && d2.RotulosTramos[0].Tramos == 2,
                   "T20b lo mismo con el P ya no vigente (el nivel de ahora en otro precio): UN rotulo al final de la raya con su precio de ahi", Rts(d2));
            }

            // T17 (hallazgo 3): grafico de 5 s (24 velas por vela m2). Un cruce que cambia cada 2 min: antes 8 rotulos de 'tramos' de 24 velas (120 s); ahora
            //     ninguno. Un tramo de 8 velas m2 (176 velas, 18:42Z a 18:56Z) si; uno de 7 (152 velas) no, aunque pasa las 15 velas del grafico.
            {
                GraficoDoble Gr5()
                {
                    var g = GrT(ahora, "ZTP_NDX_vol"); g.N = 1440; g.Primera = 1240; g.Ultima = 1439; g.PxVela = 5; g.SegVela = 5; return g;
                }
                var gr = Gr5();
                var dEsc = Armar(FotoT(ahora, HistT(gr, b => S(("ZTP_NDX_vol", Pr(31000.0 + (b / 24 % 9) * 7.3)))), A("ZTP_NDX_vol", "NDX", "vol", "ZTP", "cruce abajo", 31000.0 + (1439 / 24 % 9) * 7.3, double.NaN, 0, cb)), gr);
                var act = A("ZTP_NDX_vol", "NDX", "vol", "ZTP", "cruce abajo", 31090, double.NaN, 0, cb);
                var d8 = Armar(FotoT(ahora, HistT(gr, b => S(("ZTP_NDX_vol", b < 1416 ? Pr(31040.0) : Pr(31090.0)))), act), gr);
                var d7 = Armar(FotoT(ahora, HistT(gr, b => S(("ZTP_NDX_vol", b < 1392 ? Pr(31040.0) : Pr(31090.0)))), act), gr);
                Ok(dEsc.RotulosTramos.Count == 0 && dEsc.TramosLargos == 0, "T17 grafico de 5 s con un cruce que cambia cada 2 min (escalones de 24 velas): ningun rotulo (antes 8)", Rts(dEsc));
                Ok(d8.RotulosTramos.Count == 1 && d8.RotulosTramos[0].Txt == "NDX cruce 31.040" && d8.RotulosTramos[0].VelaFin == 175 && d7.RotulosTramos.Count == 0 && d7.TramosLargos == 0,
                   "T17b en 5 s: un tramo de 8 velas m2 (176 velas, ~15 min) lleva rotulo; uno de 7 velas m2 (152 velas) no", Rts(d8) + " || " + Rts(d7));
            }

            // T18 (hallazgo 4): la franja de arriba que reserva MargenSup4 (56 px: la leyenda y el cartel rojo de ATAS) no lleva rotulos
            {
                var gr = GrT(ahora, "MUROS_NDX_vol", "MUROS_NDX_oi");
                var h = HistT(gr, b => S(("MUROS_NDX_vol", b < 260 ? Pr(31082.0) : Pr(31000.0)), ("MUROS_NDX_oi", b < 260 ? Pr(31094.0) : Pr(31010.0))));
                var f = FotoT(ahora, h, A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro P", 31000, 30760, 0, cb, gexM: -250), A("MUROS_NDX_oi", "NDX", "oi", "MUROS", "muro P", 31010, 30770, 0, cb, gexM: -90));
                var d = Armar(f, gr);
                gr.A.MargenSup = 0; var d0 = Armar(f, gr); gr.A.MargenSup = 56;
                var rA = d.RotulosTramos.FirstOrDefault(x => x.Series.Contains("MUROS_NDX_vol"));
                Ok(d.RotulosTramos.Count == 1 && rA != null && rA.Caja.Top == gr.YDePrecio(31082.0) + 3 && d.RotulosTramos.All(x => x.Caja.Top >= 56)
                   && d.TramosNoPuestos.Count == 1 && d.TramosNoPuestos[0].Series.Contains("MUROS_NDX_oi") && d.TramosNoPuestos[0].Txt == "sin lugar"
                   && d0.RotulosTramos.Count == 2 && d0.RotulosTramos.Any(x => x.Caja.Top < 56),
                   "T18 la raya de y=" + gr.YDePrecio(31082.0) + " lleva el rotulo ABAJO (arriba caia en la franja de 56 px); la de y=" + gr.YDePrecio(31094.0) + " no tiene lugar fuera de la franja (descartado); con MargenSup4 = 0 los dos van arriba",
                   Rts(d) + " || " + Rts(d0));
            }

            // T19 (hallazgo 2): 33 series x 2 niveles que saltan cada 16 velas en 2.000 velas (8.000+ tramos): entran los 200 no vigentes mas largos; 14 rotulos sin choques
            {
                var gr = GrT(ahora, CatalogoFamilia.Series.Select(s => s.Id).ToArray());
                gr.N = 3000; gr.Primera = 1000; gr.Ultima = 2999; gr.PxVela = 1; gr.AreaR = new Rectangle(0, 0, 2100, 700); gr.Alto = 31300; gr.Bajo = 30700;
                var f = FotoPeorCaso(ahora, gr);
                var d = Armar(f, gr, CatalogoFamilia.Series, 2080);
                var cajas = d.RotulosTramos.Select(x => x.Caja).ToList(); bool sinChoque = true;
                for (int i = 0; i < cajas.Count; i++) for (int j = i + 1; j < cajas.Count; j++) if (cajas[i].IntersectsWith(cajas[j])) sinChoque = false;
                Console.WriteLine("     T19 tramos " + d.TramosLargos + " (vigentes " + d.TramosVigentes + ", recortados " + d.TramosRecortados + "), rayas " + d.TramosGrupos + ", rotulos " + d.RotulosTramos.Count + ", descartados " + d.TramosDescartados);
                Ok(d.TramosLargos >= 8000 && d.TramosRecortados == d.TramosLargos - d.TramosVigentes - ArmadoPantalla.TRAMO_CAND && d.RotulosTramos.Count == ArmadoPantalla.TRAMO_TOPE && sinChoque,
                   "T19 peor caso (" + d.TramosLargos + " tramos): juntar/agrupar con los " + ArmadoPantalla.TRAMO_CAND + " no vigentes mas largos (" + d.TramosRecortados + " recortados); " + d.RotulosTramos.Count + " rotulos sin choques");
            }
        }

        /// <summary>T19/E2 (revision 4.1.4): el peor caso de los rotulos: todas las series (33) con 2 niveles que saltan a otro precio cada 16 velas (2 pt),
        /// en todas las velas del grafico (como el P1 del revisor). Los niveles de ahora: un muro C en 31.000 por serie.</summary>
        static FotoFamilia FotoPeorCaso(DateTime ahora, GraficoDoble gr)
        {
            var ids = CatalogoFamilia.Series.Select(s => s.Id).ToArray();
            var h = HistT(gr, b =>
            {
                var e = new Dictionary<string, double[]>();
                for (int i = 0; i < ids.Length; i++) { int blk = (b + i * 3) / 16; e[ids[i]] = new[] { 30800.0 + i * 12 + (blk % 5) * 2.0, 31200.0 - i * 11 - (blk % 7) * 2.0 }; }
                return e;
            });
            var cb = ahora.AddMinutes(-16);
            return FotoT(ahora, h, ids.Select(id => { var si = CatalogoFamilia.PorId[id]; return A(id, si.Libro, si.Fuente, si.Tipo, "muro C", 31000, double.NaN, 0, cb, gexM: 10); }).ToArray());
        }

        /// <summary>T12 (4.1.4): la historia REAL de esta noche (copia de solo lectura de %APPDATA%\ATAS\PythiaGex4\familia\niv-2026-10-09-MNQZ6.jsonl hasta las 05:44Z,
        /// en prueba\datos) con las casillas del operador (MNQ liviano.ws, 02:53) en su grafico (852 px, 781 visibles, ~2 px por vela, 00:20Z a 05:44Z).
        /// Revision 4.1.4: T12b a las 03:00Z (la raya de 31.040 todavia VIGENTE) y T21 la cobertura cada 2 min de 00:20Z a 05:44Z.</summary>
        static void PruebaTramosReal()
        {
            string ruta = Path.Combine(RaizProyecto() ?? ".", "_modulos", "pantalla", "prueba", "datos", "niv-2026-10-09-MNQZ6-hasta-0544Z.jsonl");
            if (!File.Exists(ruta)) { Ok(false, "T12 historia real: no esta " + ruta); return; }
            var niv = new SortedDictionary<long, Dictionary<string, (double P, string E, double G, double K)[]>>();
            foreach (var l in File.ReadLines(ruta))
            {
                if (!l.StartsWith("{\"k\"", StringComparison.Ordinal)) continue;
                using var doc = System.Text.Json.JsonDocument.Parse(l);
                var j = doc.RootElement; long k = j.GetProperty("k").GetInt64();
                var s = new Dictionary<string, (double, string, double, double)[]>();
                foreach (var p in j.GetProperty("s").EnumerateObject())
                    s[p.Name] = p.Value.EnumerateArray().Select(a => (a[0].GetDouble(), a[1].GetString(), a[2].ValueKind == System.Text.Json.JsonValueKind.Number ? a[2].GetDouble() : double.NaN,
                                                                      a[3].ValueKind == System.Text.Json.JsonValueKind.Number ? a[3].GetDouble() : double.NaN)).ToArray();
                niv[k] = s;
            }
            // la historia por vela m2 como SalidaFamilia.Entrada: el minuto de la apertura o, si no esta, el anterior (para las velas que se ven es la misma
            // que la de una foto anterior: la ultima vela visible es la del minuto de la foto)
            var hist = new Dictionary<long, IReadOnlyDictionary<string, double[]>>();
            long kIni = niv.Keys.First(), kFin = niv.Keys.Last();
            for (long T = kIni / 2 * 2 * 60000; T <= kFin * 60000; T += 120000)
            {
                long key = T / 60000;
                if (!niv.TryGetValue(key, out var s) && !niv.TryGetValue(key - 1, out s)) continue;
                hist[T] = s.ToDictionary(kv => kv.Key, kv => kv.Value.Select(x => x.P).ToArray());
            }
            string Hm(DateTime t) => t.ToString("HH:mm", CultureInfo.InvariantCulture) + "Z";

            // la foto y el grafico del operador a la hora del minuto k (la ultima vela es la del minuto k; los actuales, los de ese minuto)
            (FotoFamilia F, GraficoDoble G, DateTime Ahora) Estado(long k)
            {
                var ahora = DateTime.UnixEpoch.AddMinutes(k + 1);
                var act = new List<NivelActual>();
                foreach (var kv in niv[k])
                {
                    if (!CatalogoFamilia.PorId.TryGetValue(kv.Key, out var si)) continue;
                    foreach (var x in kv.Value)
                        act.Add(FotosDoble.ComoCambios(new NivelActual { Serie = kv.Key, Libro = si.Libro, Fuente = si.Fuente, Tipo = si.Tipo, Rol = CatalogoFamilia.Rol(si.Tipo, x.E), Precio = x.P, Strike = x.K,
                                                                         GexM = x.G, Banda = si.Banda, DatoUtc = si.Libro == "NQ" ? ahora.AddSeconds(-50) : new DateTime(2026, 10, 8, 20, 14, 59, DateTimeKind.Utc) }));
                }
                var f = FotosDoble.Copia(FotosDoble.Normal(ahora), x => { x.HistoriaM2 = hist; x.Actuales = act; x.Sesion = "2026-10-09"; x.Instrumento = "MNQZ6"; });
                // el grafico del operador: area 852 x 581, clip 781; 325 velas de 1 min a 2 px; precio 30.995 - 31.185 (captura 02:44 local)
                var gr = new GraficoDoble(ahora) { AreaR = new Rectangle(0, 0, 852, 581), N = 400, Primera = 75, Ultima = 399, PxVela = 2, Alto = 31185, Bajo = 30995, UltimoCierre = 31164.25 };
                gr.A.Visibles.Clear();
                foreach (var id in new[] { "MAJORS_NDX_vol", "MAJORS_NQ_oi", "MUROS_NDX_oi", "MUROS_NDX_vol", "MUROS_NQ_oi", "R20_NDX_vol", "R20_QQQ_vol", "T_MUROS_oi", "ZEST_QQQ_vol", "ZTP_NDX_vol" })
                    gr.A.Visibles.Add(id);
                gr.A.Eje = EjeFamilia4.Ninguno;
                return (f, gr, ahora);
            }
            void Listar(string tag, DibujoPantalla d, GraficoDoble gr, DateTime ahora)
            {
                Console.WriteLine("     " + tag + " historia real (" + niv.Count + " minutos, " + hist.Count + " velas m2) a las " + Hm(ahora.AddMinutes(-1)) + ": tramos de >= 15 velas en pantalla " + d.TramosLargos + " (vigentes "
                                  + d.TramosVigentes + "), rayas " + d.TramosGrupos + ", rotulos " + d.RotulosTramos.Count + ", descartados " + d.TramosDescartados);
                Console.WriteLine("     " + tag + " etiquetas de la columna: " + string.Join(" | ", d.Etiquetas.Select(e => e.Txt)));
                foreach (var r in d.RotulosTramos)
                {
                    var t0 = ahora.AddMinutes(-(gr.N - (gr.Primera + r.VelaIni))); var t1 = ahora.AddMinutes(-(gr.N - (gr.Primera + r.VelaFin)));
                    Console.WriteLine("     " + tag + " rotulo '" + r.Txt + "' (" + string.Join("+", r.Series) + ", " + r.Tramos + " tramos) raya " + Hm(t0) + " -> " + Hm(t1) + " (" + r.Largo + " velas) en x " + r.Caja.X + "-" + r.Caja.Right + ", y " + r.Caja.Y);
                }
            }

            // T12: 05:44Z
            {
                var (f, gr, ahora) = Estado(kFin);
                var d = Armar(f, gr, CatalogoFamilia.Series, 781);
                Listar("T12", d, gr, ahora);
                var r40 = d.RotulosTramos.FirstOrDefault(r => r.Series.Contains("MUROS_NDX_vol") && r.Precio >= 31040 && r.Precio <= 31042);
                var r64 = d.RotulosTramos.Where(r => r.Series.Contains("ZTP_NDX_vol") && r.Precio >= 31063.5 && r.Precio <= 31064.6).ToList();
                var col = CajasColumna(d); int colIzq = col.Count == 0 ? int.MaxValue : col.Min(c => c.Left);
                var cajas = d.RotulosTramos.Select(x => x.Caja).ToList(); bool sinChoque = true;
                for (int i = 0; i < cajas.Count; i++) for (int j = i + 1; j < cajas.Count; j++) if (cajas[i].IntersectsWith(cajas[j])) sinChoque = false;
                Ok(r40 != null && r40.Txt.Contains("NDX muro V/OI") && r40.Txt.EndsWith(" 31.040", StringComparison.Ordinal),
                   "T12 la doble/triple raya de 31.040/31.041 de esta noche (NDX muros V y OI, mas el major) lleva rotulo: '" + r40?.Txt + "'", Rts(d));
                Ok(r64.Count >= 1, "T12 la raya roja de 31.064 (NDX cruce, 01:52-03:53Z) lleva rotulo: " + string.Join(" | ", r64.Select(x => "'" + x.Txt + "'")), Rts(d));
                Ok(sinChoque && cajas.All(c => c.Right <= Math.Min(781 - 2, colIzq - 2) && !c.IntersectsWith(d.Pestana) && c.Top >= 56) && d.RotulosTramos.Count <= ArmadoPantalla.TRAMO_TOPE,
                   "T12 los " + d.RotulosTramos.Count + " rotulos de esta noche: sin pisarse, a la izquierda de la columna de etiquetas y del borde visible (781), fuera de la pestaña y de la franja de 56 px, tope " + ArmadoPantalla.TRAMO_TOPE);
            }

            // T12b (revision 4.1.4, hallazgo 1): a las 03:00Z la raya de 31.040 (muros NDX V y OI) sigue VIGENTE. El major NDX V de 31.041,36 (22:16Z -> 22:47Z,
            //      32 velas, a ~0,9 pt de esa raya) antes se absorbia en ella y no lo nombraba nadie; ahora lleva su rotulo en su final (22:47Z)
            {
                long k3 = (long)(new DateTime(2026, 10, 9, 3, 0, 0, DateTimeKind.Utc) - DateTime.UnixEpoch).TotalMinutes;
                var (f, gr, ahora) = Estado(k3);
                var d = Armar(f, gr, CatalogoFamilia.Series, 781);
                Listar("T12b", d, gr, ahora);
                int b2247 = gr.N - (int)(ahora - new DateTime(2026, 10, 8, 22, 47, 0, DateTimeKind.Utc)).TotalMinutes;
                int xFin = gr.XDeBarra(b2247) + Math.Max(2, (int)Math.Round(2 * 0.9)) / 2 + 1;
                var rMaj = d.RotulosTramos.FirstOrDefault(r => r.Series.Contains("MAJORS_NDX_vol") && r.Precio >= 31041 && r.Precio <= 31042);
                bool vig40 = !d.RotulosTramos.Any(r => r.Series.Contains("MUROS_NDX_vol") && r.Precio >= 31039.5 && r.Precio <= 31042) && d.Etiquetas.Any(e => e.Txt.Contains(" 31.040,"));
                Ok(rMaj != null && rMaj.Txt.StartsWith("NDX major V", StringComparison.Ordinal) && Math.Abs(rMaj.XFin - xFin) <= 2 && vig40,
                   "T12b 03:00Z: la raya de 31.040 es la vigente (la nombra la columna, sin rotulo) y el major NDX V que termino a las 22:47Z adentro de ella lleva SU rotulo en su final (x " + xFin + "): '" + rMaj?.Txt + "'", Rts(d));
            }

            // T21 (revision 4.1.4): cobertura sobre la noche real, cada 2 min de 00:20Z a 05:44Z: cada tramo no vigente de >= 15 velas (y 8 velas m2) en pantalla,
            //     detectado POR FUERA del armado, tiene su rotulo en su final (<= 40 px, <= 1,5 pt), o es la misma serie que sigue en el mismo precio (C/P),
            //     o quedo sin lugar / fuera del tope (TramosNoPuestos). Ninguno nombrado lejos ni sin nombre (antes: 352 casos sin nombre por absorcion en una
            //     raya vigente y 1.036 nombrados a mas de 40 px, barrido del revisor con las R20).
            {
                long k0 = (long)(new DateTime(2026, 10, 9, 0, 20, 0, DateTimeKind.Utc) - DateTime.UnixEpoch).TotalMinutes;
                int tot = 0, ok = 0, misma = 0, noPuestos = 0, lejos = 0, sin = 0, piezas = 0, choques = 0, sobre = 0, franja = 0, estados = 0, rot = 0;
                var ej = new List<string>();
                for (long k = k0; k <= kFin; k += 2)
                {
                    if (!niv.ContainsKey(k)) continue;
                    var (f, gr, ahora) = Estado(k);
                    var d = Armar(f, gr, CatalogoFamilia.Series, 781);
                    estados++; rot += d.RotulosTramos.Count;
                    var col = CajasColumna(d);
                    var cajas = d.RotulosTramos.Select(x => x.Caja).ToList();
                    for (int i = 0; i < cajas.Count; i++) for (int j = i + 1; j < cajas.Count; j++) if (cajas[i].IntersectsWith(cajas[j])) choques++;
                    foreach (var c in cajas) { if (col.Any(o => o.IntersectsWith(c)) || c.IntersectsWith(d.Pestana) || c.Right > 781) sobre++; if (c.Top < 56) franja++; }
                    foreach (var t in TramosPorFuera(f, gr))
                    {
                        tot++;
                        bool Cerca(RotuloTramo r) => r.Series.Contains(t.Id) && Math.Abs(r.Precio - t.P) <= 1.5 + 1e-9 && Math.Abs(r.XFin - t.XFin) <= 40;
                        if (d.RotulosTramos.Any(Cerca)) { ok++; continue; }
                        if (t.SigueFin > t.Fin + 3) { misma++; continue; }
                        if (d.TramosNoPuestos.Any(Cerca)) { noPuestos++; continue; }
                        if (d.RotulosTramos.Any(r => Math.Abs(r.Precio - t.P) <= 1.5 + 1e-9 && Math.Abs(r.XFin - t.XFin) <= 40 && r.Txt.Split(new[] { " · " }, StringSplitOptions.None).Length >= ArmadoPantalla.TRAMO_PIEZAS)) { piezas++; continue; }
                        string que = t.Id + " " + t.P.ToString("0.00", CultureInfo.InvariantCulture) + " velas " + t.Ini + "-" + t.Fin + " a las " + Hm(ahora.AddMinutes(-1));
                        if (d.RotulosTramos.Any(r => r.Series.Contains(t.Id) && Math.Abs(r.Precio - t.P) <= 1.5 + 1e-9)) { lejos++; if (ej.Count < 6) ej.Add("LEJOS " + que); }
                        else { sin++; if (ej.Count < 6) ej.Add("SIN NOMBRE " + que); }
                    }
                }
                Console.WriteLine("     T21 cobertura: " + estados + " fotos, " + rot + " rotulos; tramos no vigentes " + tot + ": con rotulo en su final " + ok + ", misma serie que sigue en el mismo precio " + misma
                                  + ", sin lugar o fuera del tope " + noPuestos + ", en un rotulo de " + ArmadoPantalla.TRAMO_PIEZAS + " nombres " + piezas + ", nombrados lejos " + lejos + ", sin nombre " + sin
                                  + "; choques " + choques + ", sobre la columna/pestaña/borde " + sobre + ", en la franja de 56 px " + franja);
                Ok(estados >= 150 && tot > 1000 && lejos == 0 && sin == 0 && choques == 0 && sobre == 0 && franja == 0,
                   "T21 la noche real cada 2 min (" + estados + " fotos, " + tot + " tramos no vigentes): ninguno nombrado lejos de su final ni sin nombre (salvo sin lugar/tope: " + noPuestos + "); sin choques, nada sobre la columna, la pestaña ni la franja de arriba",
                   string.Join(" || ", ej));
            }
        }

        /// <summary>T21: un tramo detectado por fuera del armado (la regla del pedido: mismo precio a <= 0,5 pt, huecos de hasta 3 velas, >= 15 velas y 8 velas m2).</summary>
        sealed class TramoFuera { public string Id; public int Ini, Fin, Hueco, XFin; public double P; public int SigueFin = -1; public TramoFuera Sigue; public int Largo => Fin - Ini + 1; }

        /// <summary>T21: los tramos NO vigentes y en pantalla de cada serie visible, con su XFin como lo calcula el armado (x de la ultima vela + medio ancho + 1) y,
        /// si al cerrarse otro tramo de la misma serie seguia en su precio, el final de ese otro (SigueFin).</summary>
        static List<TramoFuera> TramosPorFuera(FotoFamilia f, GraficoDoble gr)
        {
            var velas = new List<(int X, long M2)>();
            int desde = Math.Max(0, gr.Primera), hasta = Math.Min(gr.N - 1, gr.Ultima);
            for (int b = desde; b <= hasta; b++) if (gr.Vela(b, out long ms, out _)) velas.Add((gr.XDeBarra(b), ms / 120000 * 120000));
            int ancho = hasta > desde ? Math.Max(1, (gr.XDeBarra(hasta) - gr.XDeBarra(desde)) / (hasta - desde)) : 6;
            int w = Math.Max(2, (int)Math.Round(ancho * 0.9));
            bool Largo(TramoFuera t) => t.Largo >= 15 && velas[t.Fin].M2 - velas[t.Ini].M2 >= 7 * 120000L;
            var largos = new List<TramoFuera>();
            foreach (var id in gr.A.Visibles)
            {
                var ab = new List<TramoFuera>();
                for (int i = 0; i < velas.Count; i++)
                {
                    double[] ps = null;
                    if (f.HistoriaM2.TryGetValue(velas[i].M2, out var e) && e != null && e.TryGetValue(id, out var p0)) ps = p0;
                    var usados = new bool[ps?.Length ?? 0];
                    foreach (var t in ab)
                    {
                        int mejor = -1; double dm = 1e9;
                        for (int a = 0; a < usados.Length; a++) { if (usados[a]) continue; double dd = Math.Abs(ps[a] - t.P); if (dd <= 0.5 + 1e-9 && dd < dm) { dm = dd; mejor = a; } }
                        if (mejor >= 0) { usados[mejor] = true; t.P = ps[mejor]; t.Fin = i; t.XFin = velas[i].X; t.Hueco = 0; } else t.Hueco++;
                    }
                    for (int q = ab.Count - 1; q >= 0; q--)
                        if (ab[q].Hueco > 3)
                        {
                            var t = ab[q];
                            if (Largo(t)) { t.Sigue = ab.FirstOrDefault(o => o != t && o.Hueco <= 3 && Math.Abs(o.P - t.P) <= 0.5 + 1e-9); largos.Add(t); }
                            ab.RemoveAt(q);
                        }
                    for (int a = 0; a < usados.Length; a++) if (!usados[a]) ab.Add(new TramoFuera { Id = id, Ini = i, Fin = i, P = ps[a], XFin = velas[i].X });
                }
                foreach (var t in ab) if (Largo(t)) largos.Add(t);
            }
            foreach (var t in largos) if (t.Sigue != null) t.SigueFin = t.Sigue.Fin;
            int nV = velas.Count;
            bool ultima = gr.Ultima >= gr.N - 1;
            var sal = new List<TramoFuera>();
            foreach (var t in largos)
            {
                int y = gr.YDePrecio(t.P); if (y < gr.AreaR.Top || y > gr.AreaR.Bottom) continue;
                bool vig = ultima && t.Fin >= nV - 4 && f.Actuales.Any(a => a.Serie == t.Id && Math.Abs(a.Precio - t.P) <= 0.5 + 1e-9);
                if (vig) continue;
                t.XFin = t.XFin + w / 2 + 1;
                sal.Add(t);
            }
            return sal;
        }

        // ------------------------------------------------------------------ R (4.1.4): arreglos de la revision de la 4.1.3
        static void PruebaRevision413()
        {
            Console.WriteLine("R. 4.1.4: arreglos de la revision de la 4.1.3 (etiquetas tapadas, origen de la conversion, color)");
            var ahora = new DateTime(2026, 10, 9, 5, 40, 0, DateTimeKind.Utc);
            DateTime cb = new DateTime(2026, 10, 8, 20, 14, 59, DateTimeKind.Utc);
            const string M = "−";
            var f0 = FotosDoble.Normal(ahora);
            // R1 el caso real de la revision: QQQ dom D1 en el MISMO strike que el muro de QQQ por OI (31.076,37): antes la etiqueta decia solo 'QQQ P −2,19B'
            {
                var gr = new GraficoDoble(ahora) { Alto = 31200, Bajo = 30900 }; gr.A.Visibles.Clear(); gr.A.Visibles.Add("MUROS_QQQ_oi"); gr.A.Visibles.Add("DOMS_QQQ_vol");
                var f = FotosDoble.Copia(f0, x => x.Actuales = new List<NivelActual>
                {
                    FotosDoble.ComoCambios(A("MUROS_QQQ_oi", "QQQ", "oi", "MUROS", "muro P", 31076.37, 750, 20, cb, gexM: -2194.6)),
                    FotosDoble.ComoCambios(A("DOMS_QQQ_vol", "QQQ", "vol", "DOMS", "D1", 31076.37, 750, 0, cb, gexM: 427.3)),
                });
                var d = Armar(f, gr);
                var r = d.Rotulos.FirstOrDefault(x => x.Fuera == 0);
                Ok(d.Etiquetas.Count == 1 && r != null && r.Txt == "QQQ P " + M + "2,19B 31.076 OI · QQQ dom D1" && r.Serie == "MUROS_QQQ_oi" && r.Acompanan == "QQQ dom D1",
                   "R1 QQQ dom D1 (+427M) en el strike del muro P de QQQ OI (−2,19B): la etiqueta la nombra al final (revision 4.1.4: el precio y el 'OI' quedan pegados a la cabeza): 'QQQ P −2,19B 31.076 OI · QQQ dom D1'", r?.Txt ?? "-");
                gr.A.Montos = false; gr.A.Cambios = false; var dv = Armar(f, gr);
                Ok(dv.Etiquetas.Any(e => e.Txt == "QQQ P OI  31.076 · QQQ dom D1"), "R1b con la etiqueta de la 4.1.1 (montos y cambios apagados) tambien: 'QQQ P OI  31.076 · QQQ dom D1'", string.Join(" | ", dv.Etiquetas.Select(e => e.Txt)));
            }
            // R2 2.0 NDX D1 a 0,36 pt del muro P de NDX por volumen (222 de 345 minutos del 09-10): nombrada; una serie de la 4.1 tapada NO se agrega (como siempre)
            {
                var gr = new GraficoDoble(ahora) { Alto = 31100, Bajo = 30800 }; gr.A.Visibles.Clear(); foreach (var s in new[] { "MUROS_NDX_vol", "R20_NDX_vol", "MAJORS_NDX_vol", "DOMS_NDX_vol", "R20_QQQ_vol" }) gr.A.Visibles.Add(s);
                var f = FotosDoble.Copia(f0, x => x.Actuales = new List<NivelActual>
                {
                    FotosDoble.ComoCambios(A("R20_NDX_vol", "NDX", "vol", "DOMS", "D1", 30941.83, 30700, 0, cb, gexM: 146.2)),
                    FotosDoble.ComoCambios(A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro P", 30941.47, 30700, 0, cb, gexM: -249.6)),
                    FotosDoble.ComoCambios(A("MAJORS_NDX_vol", "NDX", "vol", "MAJORS", "M-", 30941.47, 30700, 0, cb, gexM: -40)),
                    FotosDoble.ComoCambios(A("DOMS_NDX_vol", "NDX", "vol", "DOMS", "D2", 30941.47, 30700, 0, cb, gexM: -60)),
                    FotosDoble.ComoCambios(A("R20_QQQ_vol", "QQQ", "vol", "DOMS", "D2", 30941.2, 746.5, 0, cb, gexM: 80)),
                });
                var d = Armar(f, gr);
                var r = d.Rotulos.FirstOrDefault(x => x.Fuera == 0);
                Ok(d.Etiquetas.Count == 1 && r != null && r.Txt == "NDX P " + M + "250M 30.941,47 V · 2.0 NDX D1 · NDX dom D2",
                   "R2 grupo de 5 niveles a <= 1 pt: cabeza NDX P (mayor |monto|) con SU precio y fuente + hasta DOS series 2.0 nombradas al final ('· 2.0 NDX D1 · NDX dom D2'); el major de la 4.1 y la tercera 2.0 no se agregan", r?.Txt ?? "-");
                // R2b (revision 4.1.4): con cambio, los tres tramos de color: T1 'LIBRO ROL MONTO', T2 el cambio, T3 ' PRECIO FUENTE · acompañantes'
                var f2 = FotosDoble.Copia(f, x => x.Actuales = x.Actuales.Select(n => n.Serie != "MUROS_NDX_vol" ? n
                                                    : FotosDoble.Vol(n.Copia(), cb, new[] { -1.2, -3.1, -5.0 }, new[] { 300.0, 900.0, 1800.0 }, new[] { 1.0, 1.0, 1.0 })).ToList());
                var p2 = new PantallaFamilia(gr); var g2 = new RenderDoble(); p2.Pintar(g2, f2);
                int i2 = g2.TextosDet.FindIndex(t => t.S == "NDX P " + M + "250M");
                Ok(i2 >= 0 && i2 + 2 < g2.TextosDet.Count && g2.TextosDet[i2 + 1].S == " ▲3,1M" && g2.TextosDet[i2 + 2].S == " 30.941,47 V · 2.0 NDX D1 · NDX dom D2",
                   "R2b con cambio: 'NDX P −250M' | ' ▲3,1M' | ' 30.941,47 V · 2.0 NDX D1 · NDX dom D2' (el precio y la fuente de la cabeza antes de los acompañantes)",
                   i2 < 0 ? string.Join(" | ", g2.TextosDet.Select(t => t.S)) : string.Join(" | ", g2.TextosDet.Skip(i2).Take(3).Select(t => "'" + t.S + "'")));
            }
            // R3 el detalle dice el ORIGEN real de la conversion de la replica (antes, siempre 'MNQ del ultimo trade / QQQ spot' y 'cruda de forwards')
            {
                const string vela = "razon 2.0 41.4447 = MNQ 30990.25 (vela de 1 min de las 16:14 NY, la del ultimo trade de la cadena) / spot QQQ 747.75 · vela alineada al ultimo trade 16:14 NY (cadena congelada 508 min) · de noche junta el MNQ de esa vela con el spot de QQQ de ahora (falla conocida de la 2.0)";
                var casos = new (string Serie, string Libro, double Conv, string Texto, string Esperado, bool Normal)[]
                {
                    ("R20_QQQ_vol", "2.0 QQQ", 41.44466733533935, vela, " (razon 2.0 41,4447 = MNQ del ultimo trade / QQQ spot)", true),
                    ("R20_QQQ_vol", "2.0 QQQ", 41.4447, "razon 2.0 41.4447 = MNQ 30990.25 (vela de 1 min de las 16:14 NY, la del ultimo trade de la cadena; sin sus ticks en la cinta: vela m2 interpolada) / spot QQQ 747.75 · vela alineada (ultimo trade 16:14 NY)",
                     " (razon 2.0 41,4447 = MNQ del ultimo trade (vela m2 interpolada: la cinta no tiene esos ticks) / QQQ spot)", false),
                    ("R20_QQQ_vol", "2.0 QQQ", 41.1727, "razon 2.0 41.1727 · CRUDA: la vela alineada es de otro contrato (31242 vs 31049)",
                     " (razon 2.0 41,1727 = CRUDA: MNQ de ahora / QQQ spot (la vela del ultimo trade era de otro contrato: guardia del 0,6 %))", false),
                    ("R20_QQQ_vol", "2.0 QQQ", 41.4447, "razon 2.0 41.4447 · ultima valida hace 35 min; sin vela alineada al ultimo trade 16:14 NY",
                     " (razon 2.0 41,4447 = la ultima razon valida (hace 35 min), sin vela alineada)", false),
                    ("R20_QQQ_vol", "2.0 QQQ", 41.43, "razon 2.0 41.4300 · mediana de la rueda; cadena congelada (ultimo trade 16:14 NY) sin vela alineada", " (razon 2.0 41,4300 = la mediana de la rueda, sin vela alineada)", false),
                    ("R20_QQQ_vol", "2.0 QQQ", 41.609495, "razon 2.0 41.6095 · CRUDA sin alinear (sin razon valida previa); cadena congelada (ultimo trade 16:14 NY) sin vela alineada · de noche junta el MNQ de esa vela con el spot de QQQ de ahora (falla conocida de la 2.0)",
                     " (razon 2.0 41,6095 = CRUDA: MNQ de ahora / QQQ spot, sin vela alineada ni razon previa)", false),
                    ("R20_NDX_vol", "2.0 NDX", 241.83, "base 2.0 241.83 (CRUDA 90 ticks de forwards de la cadena; cota: carry 176.4 +-60 %)", " (base 2.0 241,83, cruda de forwards)", true),
                    ("R20_NDX_vol", "2.0 NDX", 177.0, "base 2.0 177.00 (TEORICA carry 177.0 (cruda 300.0 descartada); cota: carry 177.0 +-60 %)", " (base 2.0 177,00, TEORICA: el carry (la cruda de forwards no paso la cota))", false),
                    ("R20_NDX_vol", "2.0 NDX", 241.83, "base 2.0 241.83 (CRUDA 90 ticks (sin cota) de forwards de la cadena)", " (base 2.0 241,83, cruda de forwards SIN cota (no hay carry para comparar))", false),
                };
                var mal = new List<string>();
                foreach (var c in casos)
                {
                    var fu = new List<FuenteEstado> { new FuenteEstado { Libro = c.Libro, ConvValor = c.Conv, Texto = c.Texto, DatoUtc = cb } };
                    string got = ArmadoPantalla.ConvDe(c.Serie, fu); var (_, normal) = ArmadoPantalla.OrigenConv20(c.Serie, c.Texto);
                    if (got != c.Esperado || normal != c.Normal) mal.Add("'" + got + "' normal=" + normal + " en vez de '" + c.Esperado + "' normal=" + c.Normal);
                }
                Ok(mal.Count == 0, "R3 ConvDe segun el origen real de la conversion (" + casos.Length + " casos: vela, m2 interpolada, CRUDA guardia 0,6 %, ultima valida, mediana, CRUDA sin alinear; NDX cruda, TEORICA, sin cota)", string.Join(" || ", mal));
                // en la pestaña: el renglon del nivel y, si es un respaldo, uno naranja debajo
                var gr = new GraficoDoble(ahora) { Alto = 31200, Bajo = 30900, Abierto = true }; gr.A.Visibles.Clear(); gr.A.Visibles.Add("R20_QQQ_vol");
                var f = FotosDoble.Copia(f0, x =>
                {
                    x.Actuales = new List<NivelActual> { FotosDoble.ComoCambios(A("R20_QQQ_vol", "QQQ", "vol", "DOMS", "D1", 30879.0, 750, 0, cb, gexM: 417.3)) };
                    x.Fuentes = f0.Fuentes.Concat(new[] { new FuenteEstado { Libro = "2.0 QQQ", ConvValor = 41.1727, DatoUtc = cb, Texto = casos[2].Texto } }).ToList();
                });
                var d = Armar(f, gr);
                int i = d.Panel.FindIndex(l => l.Contains("2.0 QQQ D1·vol +417M (razon 2.0 41,1727 = CRUDA: MNQ de ahora / QQQ spot"));
                int iR = d.Panel.FindIndex(l => l.StartsWith("    2.0 QQQ: conversion de RESPALDO de la 2.0 (CRUDA: MNQ de ahora / QQQ spot", StringComparison.Ordinal));
                var tn = d.Prims.FirstOrDefault(p => p.Tipo == TipoPrimPantalla.Texto && p.T.StartsWith("    2.0 QQQ: conversion de RESPALDO", StringComparison.Ordinal));
                Ok(i >= 0 && iR > i && iR <= i + 2 && tn.C == ArmadoPantalla.ColNaranja,
                   "R3b la pestaña: el nivel con su origen real y, debajo, en naranja: '2.0 QQQ: conversion de RESPALDO de la 2.0 (...): la raya puede quedar corrida'",
                   i < 0 ? string.Join(" | ", d.Panel) : d.Panel[i] + " || " + (i + 1 < d.Panel.Count ? d.Panel[i + 1] : "-"));
                string todo = Regex.Replace(string.Join(" ", d.Panel), @"\s+", " ");
                Ok(todo.Contains("2.0 QQQ (replica, CBOE)") && todo.Contains("replica a la 2.0 en un grafico de 1 min (en otro marco su vela, y su razon, cambian)"),
                   "R3c la fuente '2.0 QQQ' dice que replica a la 2.0 en un grafico de 1 min (en otro marco su vela, y su razon, cambian)");
            }
            // R4 color: '2.0 QQQ' distinguible del 'QQQ 0G' (CIEDE2000; antes 3,8 = el mismo amarillo a simple vista)
            {
                var cat = CatalogoFamilia.Series;
                string c20 = CatalogoFamilia.PorId["R20_QQQ_vol"].ColorHex, c0g = CatalogoFamilia.PorId["ZEST_QQQ_vol"].ColorHex;
                double dAntes = De2000("#e8c83c", c0g), dAhora = De2000(c20, c0g);
                var cerca = cat.Where(s => s.Id != "R20_QQQ_vol").Select(s => (s.Id, D: De2000(c20, s.ColorHex))).OrderBy(x => x.D).ToList();
                Console.WriteLine("     R4 '2.0 QQQ' " + c20 + ": CIEDE2000 contra QQQ 0G " + dAhora.ToString("0.0", CultureInfo.InvariantCulture) + " (4.1.3 #e8c83c: " + dAntes.ToString("0.0", CultureInfo.InvariantCulture)
                                  + "); las mas cercanas: " + string.Join(", ", cerca.Take(4).Select(x => x.Id + " " + x.D.ToString("0.0", CultureInfo.InvariantCulture))));
                Ok(dAntes < 5 && dAhora >= 15 && cerca[0].D >= 10 && SeriesPantalla.Respaldo.First(s => s.Id == "R20_QQQ_vol").ColorHex == c20,
                   "R4 '2.0 QQQ' " + c20 + " (ambar oscuro) se distingue del 'QQQ 0G' #fcd34d (CIEDE2000 " + dAhora.ToString("0.0", CultureInfo.InvariantCulture) + " >= 15; antes "
                   + dAntes.ToString("0.0", CultureInfo.InvariantCulture) + ") y de cualquier otra serie (>= 10: la mas cercana " + cerca[0].Id + " " + cerca[0].D.ToString("0.0", CultureInfo.InvariantCulture) + ")");
            }
        }

        /// <summary>CIEDE2000 entre dos colores "#rrggbb" (sRGB D65).</summary>
        static double De2000(string h1, string h2)
        {
            (double L, double A, double B) Lab(string h)
            {
                double Lin(int c) { double x = c / 255.0; return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
                var col = SeriesPantalla.ColorDe(h); double r = Lin(col.R), g = Lin(col.G), b = Lin(col.B);
                double X = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047, Y = 0.2126 * r + 0.7152 * g + 0.0722 * b, Z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883;
                double Fx(double t) => t > 216.0 / 24389 ? Math.Pow(t, 1.0 / 3) : (24389.0 / 27 * t + 16) / 116;
                return (116 * Fx(Y) - 16, 500 * (Fx(X) - Fx(Y)), 200 * (Fx(Y) - Fx(Z)));
            }
            double Rad(double g) => g * Math.PI / 180;
            double Grad(double r) => r * 180 / Math.PI;
            var (L1, a1, b1) = Lab(h1); var (L2, a2, b2) = Lab(h2);
            double C1 = Math.Sqrt(a1 * a1 + b1 * b1), C2 = Math.Sqrt(a2 * a2 + b2 * b2), Cm = (C1 + C2) / 2;
            double G = 0.5 * (1 - Math.Sqrt(Math.Pow(Cm, 7) / (Math.Pow(Cm, 7) + Math.Pow(25, 7))));
            double a1p = (1 + G) * a1, a2p = (1 + G) * a2, C1p = Math.Sqrt(a1p * a1p + b1 * b1), C2p = Math.Sqrt(a2p * a2p + b2 * b2);
            double h1p = (Grad(Math.Atan2(b1, a1p)) + 360) % 360, h2p = (Grad(Math.Atan2(b2, a2p)) + 360) % 360;
            double dLp = L2 - L1, dCp = C2p - C1p;
            double dhp = C1p * C2p == 0 ? 0 : Math.Abs(h2p - h1p) <= 180 ? h2p - h1p : h2p - h1p > 180 ? h2p - h1p - 360 : h2p - h1p + 360;
            double dHp = 2 * Math.Sqrt(C1p * C2p) * Math.Sin(Rad(dhp / 2));
            double Lm = (L1 + L2) / 2, Cmp = (C1p + C2p) / 2;
            double hm = C1p * C2p == 0 ? h1p + h2p : Math.Abs(h1p - h2p) <= 180 ? (h1p + h2p) / 2 : (h1p + h2p < 360 ? (h1p + h2p + 360) / 2 : (h1p + h2p - 360) / 2);
            double T = 1 - 0.17 * Math.Cos(Rad(hm - 30)) + 0.24 * Math.Cos(Rad(2 * hm)) + 0.32 * Math.Cos(Rad(3 * hm + 6)) - 0.20 * Math.Cos(Rad(4 * hm - 63));
            double dth = 30 * Math.Exp(-Math.Pow((hm - 275) / 25, 2)), Rc = 2 * Math.Sqrt(Math.Pow(Cmp, 7) / (Math.Pow(Cmp, 7) + Math.Pow(25, 7)));
            double Sl = 1 + 0.015 * Math.Pow(Lm - 50, 2) / Math.Sqrt(20 + Math.Pow(Lm - 50, 2)), Sc = 1 + 0.045 * Cmp, Sh = 1 + 0.015 * Cmp * T, Rt = -Math.Sin(Rad(2 * dth)) * Rc;
            return Math.Sqrt(Math.Pow(dLp / Sl, 2) + Math.Pow(dCp / Sc, 2) + Math.Pow(dHp / Sh, 2) + Rt * (dCp / Sc) * (dHp / Sh));
        }

        // ------------------------------------------------------------------ D
        static void PruebaPintado()
        {
            Console.WriteLine("D. PantallaFamilia + RenderContext doble (OFT.Rendering cargado de la carpeta de ATAS)");
            var ahora = DateTime.UtcNow;
            var gr = new GraficoDoble(ahora);
            var f = FotosDoble.Normal(ahora);
            var p = new PantallaFamilia(gr);
            var g = new RenderDoble();
            p.Pintar(g, f);
            var d = p.UltimoDibujo;
            int nRel = d.Prims.Count(x => x.Tipo == TipoPrimPantalla.Relleno), nBor = d.Prims.Count(x => x.Tipo == TipoPrimPantalla.Borde),
                nLin = d.Prims.Count(x => x.Tipo == TipoPrimPantalla.Linea), nTxt = d.Prims.Count(x => x.Tipo == TipoPrimPantalla.Texto);
            Ok(d.Rayitas == 600 && g.Rellenos == nRel && g.Bordes == nBor && g.Lineas == nLin && g.Textos == nTxt,
               "D1 se pinta exactamente lo armado: " + g.Rellenos + " rellenos, " + g.Bordes + " bordes, " + g.Lineas + " lineas, " + g.Textos + " textos");
            Ok(g.Strings.Contains("NQ C +90M 31.060 OI*") && g.Strings.Contains(d.Titulo), "D2 los textos llegan al RenderContext (etiqueta sin cambio: una sola primitiva; y la pestaña)");
            int iq = g.TextosDet.FindIndex(t => t.S == "QQQ M+ +109M");
            Ok(iq >= 0 && iq + 2 < g.TextosDet.Count && g.TextosDet[iq + 1].S == " ▲12M" && g.TextosDet[iq + 1].C == ArmadoPantalla.ColSube && g.TextosDet[iq + 2].S == " 31.073 OI"
               && g.TextosDet[iq].C == Color.FromArgb(240, ArmadoPantalla.ColTexto) && g.TextosDet[iq + 2].C == g.TextosDet[iq].C && g.TextosDet[iq + 1].X > g.TextosDet[iq].X && g.TextosDet[iq + 2].X > g.TextosDet[iq + 1].X,
               "D2b la etiqueta con cambio llega en tres tramos al RenderContext ('QQQ M+ +109M' | ' ▲12M' en #089981 | ' 31.073 OI'), de izquierda a derecha");
            var r = d.Pestana;
            Ok(!p.Clic(r.Right + 5, r.Bottom + 5) && !gr.Abierto && gr.Redibujos == 0, "D3 clic afuera de la pestaña: no se toma");
            Ok(p.Clic(r.X + 4, r.Y + 4) && gr.Abierto && gr.Redibujos == 1, "D4 clic en la pestaña: la abre y pide redibujar");
            p.Pintar(g, f);
            Ok(p.UltimoDibujo.Panel.Count > 10 && p.UltimoDibujo.Titulo.StartsWith("PythiaGex 4.0 ▾"), "D5 el render siguiente muestra el detalle (" + p.UltimoDibujo.Panel.Count + " renglones)");
            Ok(p.Clic(r.X + 4, r.Y + 4) && !gr.Abierto && gr.Redibujos == 2, "D6 otro clic la cierra");
            p.Catalogo = CatalogoFamilia.Series;
            var g2 = new RenderDoble(); p.Pintar(g2, f);
            var g3 = new RenderDoble(); p.Catalogo = null; p.Pintar(g3, f);
            Ok(g2.Strings.SequenceEqual(g3.Strings) && g2.Rellenos == g3.Rellenos, "D7 con el catalogo del motor dibuja lo mismo que con el respaldo");
            Exception ex = null; try { p.Pintar(g, null); p.Pintar(null, f); } catch (Exception e) { ex = e; }
            Ok(ex == null, "D8 Pintar con foto null o sin RenderContext no tira");
            var gm = new GraficoDoble(ahora) { Instr = "MNQH7" };
            var pm = new PantallaFamilia(gm); var gg = new RenderDoble(); pm.Pintar(gg, f);
            Ok(gg.Textos == 2 && gg.Rellenos == 0 && !pm.Clic(5, 60), "D9 grafico de otro vencimiento (MNQH7 contra Z6): solo el cartel; el clic no se toma");
            // D10 (revision 4.1.2): por PantallaFamilia, el borde visible sale del ClipBounds del RenderContext (area 852, clip 781 como el operador)
            var gOp = new GraficoDoble(ahora) { AreaR = new Rectangle(0, 0, 852, 581), Abierto = true };
            var pOp = new PantallaFamilia(gOp); var gO = new RenderDoble { Clip = new Rectangle(0, 0, 781, 581) };
            pOp.Pintar(gO, FotosDoble.Normal(ahora));
            var pasan = gO.TextosDet.Where(t => t.X + Medir(t.S, t.F).Width > 781).Select(t => "'" + t.S + "'").ToList();
            Ok(pasan.Count == 0 && gO.TextosDet.Count > 20 && pOp.UltimoDibujo.Panel.Count > 10,
               "D10 PantallaFamilia con clip 781 en un area de 852 (el grafico del operador): ningun texto llega al RenderContext pasando el borde visible ("
               + gO.TextosDet.Count + " textos)", string.Join("; ", pasan.Take(5)));
        }

        // ------------------------------------------------------------------ E
        static void PruebaRendimiento()
        {
            Console.WriteLine("E. rendimiento (hilo de dibujo)");
            var ahora = DateTime.UtcNow;
            var gr = new GraficoDoble(ahora) { N = 3000, Primera = 1000, Ultima = 2999 };
            foreach (var c in SeriesPantalla.Casillas) gr.A.Visibles.Add(c.Id);
            var hist = new Dictionary<long, IReadOnlyDictionary<string, double[]>>();
            long fin = Ms(ahora) / 120000 * 120000;
            for (long T = fin - 3000 * 60000L; T <= fin; T += 120000)
            {
                var porSerie = new Dictionary<string, double[]>();
                int i = 0; foreach (var c in SeriesPantalla.Casillas) { porSerie[c.Id] = new[] { 30950.0 + i * 4, 31050.0 - i * 3 }; i++; }   // 4.1.3: paso 4 (con 33 series el paso 5 dejaba la ultima en 31.110, fuera del grafico)
                hist[T] = porSerie;
            }
            var f = FotosDoble.Copia(FotosDoble.Normal(ahora), x => x.HistoriaM2 = hist);
            var p = new PantallaFamilia(gr); var g = new RenderDoble();
            for (int k = 0; k < 5; k++) p.Pintar(g, f);       // calentar
            var sw = Stopwatch.StartNew(); int n = 40;
            for (int k = 0; k < n; k++) p.Pintar(g, f);
            double ms = sw.Elapsed.TotalMilliseconds / n;
            Console.WriteLine("     2000 velas visibles x " + SeriesPantalla.Casillas.Count + " series x 2 niveles = " + p.UltimoDibujo.Rayitas + " rayitas: " + ms.ToString("0.00", CultureInfo.InvariantCulture) + " ms por render (armado + pintado al doble)");
            Ok(p.UltimoDibujo.Rayitas == 2000 * 2 * SeriesPantalla.Casillas.Count, "E1 " + (2000 * 2 * SeriesPantalla.Casillas.Count).ToString("#,##0", ArmadoPantalla.Es) + " rayitas armadas (4.1.3: 33 series)");
            // 4.1.4: lo mismo sin los rotulos de tramos. OJO (revision 4.1.4): con niveles QUIETOS los tramos siguen por el atajo y hay 66: NO es el peor caso
            // de los rotulos (ese es E2)
            gr.A.Tramos = false;
            for (int k = 0; k < 5; k++) p.Pintar(g, f);
            sw.Restart(); for (int k = 0; k < n; k++) p.Pintar(g, f);
            double msSin = sw.Elapsed.TotalMilliseconds / n;
            gr.A.Tramos = true;
            Console.WriteLine("     niveles quietos: sin rotulos de tramos " + msSin.ToString("0.00", CultureInfo.InvariantCulture) + " ms por render; con rotulos " + ms.ToString("0.00", CultureInfo.InvariantCulture)
                              + " ms (" + p.UltimoDibujo.RotulosTramos.Count + " rotulos en el ultimo render sin ellos: debe ser 0)");
            Ok(ms < 60, "E1b niveles quietos, todas las series, 2000 velas: < 60 ms por render: " + ms.ToString("0.0", CultureInfo.InvariantCulture) + " ms");
            // E2 (revision 4.1.4): el PEOR caso de los rotulos: las 33 series con 2 niveles que saltan cada 16 velas (8.000+ tramos largos; en la 4.1.4
            // del constructor 220 ms por render: juntar/agrupar comparaba todos contra todos). Se mide armado + pintado, con y sin rotulos.
            {
                var gp = new GraficoDoble(ahora) { N = 3000, Primera = 1000, Ultima = 2999, PxVela = 1, AreaR = new Rectangle(0, 0, 1200, 700), Alto = 31300, Bajo = 30700 };
                gp.A.Visibles.Clear(); foreach (var s0 in CatalogoFamilia.Series) gp.A.Visibles.Add(s0.Id); gp.A.Eje = EjeFamilia4.Ninguno;
                var fp = FotoPeorCaso(ahora, gp);
                var pp = new PantallaFamilia(gp); var gg = new RenderDoble { Clip = new Rectangle(0, 0, 1200, 700) };
                for (int k = 0; k < 5; k++) pp.Pintar(gg, fp);
                sw.Restart(); for (int k = 0; k < n; k++) pp.Pintar(gg, fp);
                double msP = sw.Elapsed.TotalMilliseconds / n;
                var dp = pp.UltimoDibujo; int tl = dp.TramosLargos, rec = dp.TramosRecortados, rot = dp.RotulosTramos.Count;
                gp.A.Tramos = false;
                for (int k = 0; k < 5; k++) pp.Pintar(gg, fp);
                sw.Restart(); for (int k = 0; k < n; k++) pp.Pintar(gg, fp);
                double msPs = sw.Elapsed.TotalMilliseconds / n;
                gp.A.Tramos = true;
                Console.WriteLine("     E2 peor caso de los rotulos (33 series x 2 niveles que saltan cada 16 velas, 2000 velas): " + tl + " tramos largos (" + rec + " recortados), " + rot + " rotulos: con rotulos "
                                  + msP.ToString("0.00", CultureInfo.InvariantCulture) + " ms, sin " + msPs.ToString("0.00", CultureInfo.InvariantCulture) + " ms por render");
                Ok(tl >= 8000 && rot == ArmadoPantalla.TRAMO_TOPE && msP < 60, "E2 peor caso de los rotulos (" + tl + " tramos): < 60 ms por render: " + msP.ToString("0.0", CultureInfo.InvariantCulture) + " ms (sin rotulos "
                   + msPs.ToString("0.0", CultureInfo.InvariantCulture) + ")");
            }
            gr.A.Visibles.Clear(); foreach (var c in SeriesPantalla.Casillas) if (c.Default) gr.A.Visibles.Add(c.Id);
            gr.Primera = 2800;
            for (int k = 0; k < 5; k++) p.Pintar(g, f);
            sw.Restart(); for (int k = 0; k < n; k++) p.Pintar(g, f);
            double ms2 = sw.Elapsed.TotalMilliseconds / n;
            Console.WriteLine("     caso normal (200 velas, " + SeriesPantalla.PrendidasPorDefecto.Length + " series): " + ms2.ToString("0.000", CultureInfo.InvariantCulture) + " ms por render");
            Ok(ms2 < 5, "E3 caso normal < 5 ms por render: " + ms2.ToString("0.00", CultureInfo.InvariantCulture) + " ms");
        }
    }
}
