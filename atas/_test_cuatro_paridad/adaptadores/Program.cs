// Arnes de B1 (08-10-2026): AdaptadoresFamilia.cs (ICinta, ILibroNq, RedondeoPy) contra la vista previa en Python (ref_adaptadores.json).
// Sin ATAS. Lee (solo lectura) la cinta de la 3.0 (profundidad/estado/cinta) y viva3 de PythiaGex3 que usa la referencia; escribe solo en
// ./tmp (prueba de persistencia), que borra al final. Uso: correr.ps1 (compila y corre a prioridad BelowNormal).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using PythiaGexCuatro.Familia;

static class Programa
{
    static int _fallas, _pruebas;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static void Ok(bool c, string m) { _pruebas++; if (!c) { _fallas++; if (_fallas <= 40) Console.WriteLine("  FALLA: " + m); } }
    static double Dr(string s) => s == "nan" ? double.NaN : s == "inf" ? double.PositiveInfinity : s == "-inf" ? double.NegativeInfinity : double.Parse(s, NumberStyles.Float, Inv);
    static double Dr(JsonElement e) => Dr(e.GetString());
    static bool Igual(double a, double b) => (double.IsNaN(a) && double.IsNaN(b)) || a == b;
    static string F(double x) => double.IsNaN(x) ? "NaN" : x.ToString("R", Inv);

    static int Main(string[] args)
    {
        var reloj = Stopwatch.StartNew();
        string aqui = args.Length > 0 ? args[0] : BuscarAqui();
        string raiz = Path.GetFullPath(Path.Combine(aqui, "..", "..", ".."));
        var refJ = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(aqui, "ref_adaptadores.json"))).RootElement;
        AdaptadoresFamilia.Log = m => Console.WriteLine("  log: " + m);
        Console.WriteLine("arnes adaptadores B1 · referencia " + refJ.GetProperty("generado").GetString());

        // ---------------------------------------------------------------- 1. RedondeoPy == round(x, 4) de Python
        {
            int n = 0, malMathRound = 0, mal = 0;
            foreach (var par in refJ.GetProperty("redondeo").EnumerateArray())
            {
                double x = Dr(par[0]), esp = Dr(par[1]);
                double r = AdaptadoresFamilia.RedondeoPy(x, 4);
                if (!Igual(r, esp)) { mal++; if (mal <= 5) Console.WriteLine("  redondeo " + F(x) + " -> " + F(r) + " (Python " + F(esp) + ")"); }
                if (!Igual(Math.Round(x, 4), esp)) malMathRound++;
                n++;
            }
            Ok(mal == 0, "RedondeoPy: " + mal + " de " + n + " distintos de Python");
            Console.WriteLine("1. RedondeoPy: " + (n - mal) + "/" + n + " iguales a round(x,4) de Python (Math.Round de .NET erra " + malMathRound + ")");
        }

        // ---------------------------------------------------------------- 2. ParsearViva3 == preview_niveles.foto_viva3
        string rutaViva = refJ.GetProperty("viva").GetString();
        var lineas = File.ReadAllLines(rutaViva);
        {
            var cin = refJ.GetProperty("cinta");
            long ini = cin.GetProperty("ini").GetInt64(), fin = cin.GetProperty("fin").GetInt64();
            int n = 0, nulos = 0, filasTot = 0;
            foreach (var fo in refJ.GetProperty("fotos").EnumerateArray())
            {
                int i = fo.GetProperty("i").GetInt32();
                var f = LibroNqFamilia.ParsearViva3(lineas[i], "NQ", out int crudas);
                if (fo.TryGetProperty("nada", out _))
                {
                    nulos++;
                    bool fuera = f == null || AdaptadoresFamilia.Ms(f.TsUtc) < ini || AdaptadoresFamilia.Ms(f.TsUtc) > fin;
                    Ok(fuera, "foto " + i + ": Python None y C# la arma dentro de la sesion");
                    continue;
                }
                n++;
                if (f == null) { Ok(false, "foto " + i + ": C# null y Python no"); continue; }
                Ok(f.TsUtc.ToString("yyyy-MM-dd HH:mm:ss", Inv) == fo.GetProperty("ts").GetString(), "foto " + i + ": ts");
                Ok(Igual(f.FuturoFoto, Dr(fo.GetProperty("futuro"))) && Igual(f.Spot, f.FuturoFoto), "foto " + i + ": futuro");
                Ok(crudas == fo.GetProperty("n_filas").GetInt32(), "foto " + i + ": filas crudas " + crudas);
                var dias = fo.GetProperty("dias").EnumerateArray().Select(Dr).ToArray();
                Ok(dias.Length == f.Dias.Length && dias.Zip(f.Dias, Igual).All(b => b), "foto " + i + ": dias " + string.Join("/", f.Dias.Select(F)) + " vs " + string.Join("/", dias.Select(F)));
                var filas = fo.GetProperty("filas").EnumerateArray().Select(r => r.EnumerateArray().Select(Dr).ToArray()).ToArray();
                bool igualFilas = filas.Length == f.Filas.Length;
                for (int k = 0; igualFilas && k < filas.Length; k++)
                {
                    var a = f.Filas[k]; var b = filas[k];
                    igualFilas = Igual(a.K, b[0]) && a.V == (int)b[1] && Igual(a.OiC, b[2]) && Igual(a.OiP, b[3]) && Igual(a.IvC, b[4]) && Igual(a.IvP, b[5]) && Igual(a.VolC, b[6]) && Igual(a.VolP, b[7]);
                }
                Ok(igualFilas, "foto " + i + ": filas (" + f.Filas.Length + " vs " + filas.Length + ")");
                Ok(f.EsFuturo && f.Libro == "NQ" && f.GeneradoUtc == f.TsUtc && f.DatoUtc == f.TsUtc && f.TsUtc.Kind == DateTimeKind.Utc, "foto " + i + ": campos fijos");
                filasTot += f.Filas.Length;
            }
            Console.WriteLine("2. ParsearViva3: " + n + " fotos comparadas campo por campo (" + filasTot + " filas) + " + nulos + " lineas fuera de la sesion");
        }

        // ---------------------------------------------------------------- 3. ICinta == velas_de_ticks / cierre_conocido / Precio / precio_en
        var c = refJ.GetProperty("cinta");
        string dia = c.GetProperty("dia").GetString();
        string carpetaCinta = Path.Combine(raiz, "profundidad", "estado", "cinta");
        var tLeer = Stopwatch.StartNew();
        var viv = CintaFamilia.LeerCsv(Path.Combine(carpetaCinta, "cinta-NQ-" + dia + ".csv"));
        var rel = CintaFamilia.LeerCsv(Path.Combine(carpetaCinta, "cinta-NQ-" + dia + "-relleno.csv"));
        (long, long)? listo = c.GetProperty("listo").ValueKind == JsonValueKind.Array ? (c.GetProperty("listo")[0].GetInt64(), c.GetProperty("listo")[1].GetInt64()) : null;
        Console.WriteLine("3. cinta " + dia + ": " + viv.Count + " vivos + " + rel.Count + " relleno leidos en " + tLeer.ElapsedMilliseconds + " ms (Python: " + c.GetProperty("n_vivo").GetInt32() + " vivos, " + c.GetProperty("n_relleno").GetInt32() + " relleno antes del primer vivo)");

        CintaFamilia ArmarB()
        {
            var b = new CintaFamilia("MNQZ6");
            var t0 = Stopwatch.StartNew();
            foreach (var (t, p) in viv) b.Tick(t, p);
            double usTick = t0.Elapsed.TotalMilliseconds * 1000.0 / Math.Max(1, viv.Count);
            int entraron = b.Relleno(rel, listo?.Item1 ?? rel[0].T, listo?.Item2 ?? rel[rel.Count - 1].T);
            Console.WriteLine("   B (camino vivo: Tick + Relleno): " + usTick.ToString("0.000", Inv) + " us por Tick; relleno entraron " + entraron);
            return b;
        }
        var A = new CintaFamilia("MNQZ6"); A.CargarCsv(viv, rel, listo);
        var B = ArmarB();
        foreach (bool fija in new[] { true, false })
        {
            AdaptadoresFamilia.SesionUtcFija = fija;
            Comparar("A (CargarCsv)" + (fija ? " sesion UTC fija" : " sesion NY"), A, c);
            Comparar("B (Tick+Relleno)" + (fija ? " sesion UTC fija" : " sesion NY"), B, c);
        }
        AdaptadoresFamilia.SesionUtcFija = false;
        Console.WriteLine("   " + B.Estado);

        // ---------------------------------------------------------------- 3b. otras sesiones (ref_adaptadores_<dia>.json, solo cinta)
        foreach (var extra in Directory.GetFiles(aqui, "ref_adaptadores_*.json").OrderBy(x => x))
        {
            var c2 = JsonDocument.Parse(File.ReadAllBytes(extra)).RootElement.GetProperty("cinta");
            string d2 = c2.GetProperty("dia").GetString();
            var viv2 = CintaFamilia.LeerCsv(Path.Combine(carpetaCinta, "cinta-NQ-" + d2 + ".csv"));
            var rel2 = CintaFamilia.LeerCsv(Path.Combine(carpetaCinta, "cinta-NQ-" + d2 + "-relleno.csv"));
            (long, long)? listo2 = c2.GetProperty("listo").ValueKind == JsonValueKind.Array ? (c2.GetProperty("listo")[0].GetInt64(), c2.GetProperty("listo")[1].GetInt64()) : null;
            Console.WriteLine("3b. cinta " + d2 + ": " + viv2.Count + " vivos + " + rel2.Count + " relleno (Python: " + c2.GetProperty("n_vivo").GetInt32() + " vivos, " + c2.GetProperty("n_relleno").GetInt32() + " relleno antes del primer vivo)");
            var A2 = new CintaFamilia("MNQZ6"); A2.CargarCsv(viv2, rel2, listo2);
            var B2 = new CintaFamilia("MNQZ6");
            foreach (var (t, p) in viv2) B2.Tick(t, p);
            B2.Relleno(rel2, listo2?.Item1 ?? (rel2.Count > 0 ? rel2[0].T : 0), listo2?.Item2 ?? (rel2.Count > 0 ? rel2[rel2.Count - 1].T : 0));
            Comparar("A " + d2 + " (CargarCsv)", A2, c2);
            Comparar("B " + d2 + " (Tick+Relleno)", B2, c2);
            Console.WriteLine("   " + B2.Estado);
        }

        // ---------------------------------------------------------------- 4. persistencia: B -> disco -> C, mismas respuestas
        {
            string tmp = Path.Combine(aqui, "tmp");
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            AdaptadoresFamilia.Carpeta = tmp;
            var t0 = Stopwatch.StartNew();
            B.Persistir();
            var archivos = Directory.GetFiles(Path.Combine(tmp, "cinta"), "seg-*.bin");
            long bytes = archivos.Sum(f => new FileInfo(f).Length);
            var C = new CintaFamilia("MNQZ6");
            foreach (var f in archivos) Ok(C.CargarArchivoDia(f), "cargar " + Path.GetFileName(f));
            Console.WriteLine("4. persistencia: " + archivos.Length + " archivos, " + (bytes / 1048576.0).ToString("0.00", Inv) + " MB, escribir+leer " + t0.ElapsedMilliseconds + " ms");
            Comparar("C (de disco)", C, c);
            var D = new CintaFamilia("OTROZ6");
            Ok(!D.CargarArchivoDia(archivos[0]), "un archivo de otro instrumento no se carga");
            // una cinta nueva que NO cargo la historia y recibe un tick de ese dia: al persistir funde el archivo, no lo pisa
            var E = new CintaFamilia("MNQZ6");
            long tUlt = AdaptadoresFamilia.Ms(B.UltimoTickUtc);
            E.Tick(tUlt + 500, 12345.25);
            E.Persistir();
            var F2 = new CintaFamilia("MNQZ6");
            foreach (var f in Directory.GetFiles(Path.Combine(tmp, "cinta"), "seg-*.bin")) F2.CargarArchivoDia(f);
            Ok(F2.VelasM2(AdaptadoresFamilia.DeMs(c.GetProperty("ini").GetInt64()), AdaptadoresFamilia.DeMs(c.GetProperty("fin").GetInt64())).Count >= 686, "persistir sin haber cargado no pisa la historia del dia");
            Ok(Igual(F2.PrecioSoloTick(AdaptadoresFamilia.DeMs(tUlt + 500)), 12345.25), "y suma el tick nuevo");
            Directory.Delete(tmp, true);
        }

        // ---------------------------------------------------------------- 5. velas del grafico como respaldo
        {
            var G = new CintaFamilia("MNQZ6");
            long t0 = 1791400000000L - 1791400000000L % 120000;   // un balde m2
            var velas = new List<(long, long, double, double, double, double)>();
            for (int k = 0; k < 10; k++) { long ap = t0 + k * 60000L; velas.Add((ap, ap + 59000, 100 + k, 101 + k, 99 + k, 100.5 + k)); }
            int rech = G.VelasGrafico(velas);
            Ok(rech == 0, "velas de 1 min: ninguna rechazada");
            var m2 = G.VelasM2(AdaptadoresFamilia.DeMs(t0), AdaptadoresFamilia.DeMs(t0 + 3600000));
            Ok(m2.Count == 4, "10 velas de 1 min -> 4 m2 cerradas (la quinta cierra con la vela siguiente): " + m2.Count);
            if (m2.Count > 0) Ok(m2[0].Ms == t0 && m2[0].O == 100 && m2[0].H == 102 && m2[0].L == 99 && m2[0].C == 101.5, "m2 armada de dos de 1 min: o/h/l/c");
            Ok(Math.Abs(G.Precio(AdaptadoresFamilia.DeMs(t0 + 60000)) - (100 + (101.5 - 100) * 0.5)) < 1e-12, "Precio sin ticks: interpola la vela del grafico");
            Ok(double.IsNaN(G.PrecioSoloTick(AdaptadoresFamilia.DeMs(t0 + 60000))), "PrecioSoloTick sin ticks: NaN");
            int r3 = G.VelasGrafico(new List<(long, long, double, double, double, double)> { (t0 + 3600000, t0 + 3600000 + 170000, 1, 2, 0.5, 1.5) });
            Ok(r3 == 1, "una vela de 3 min se rechaza");
            // ticks que cubren un balde ENTERO (escucha continua) mandan sobre el grafico
            var H = new CintaFamilia("MNQZ6");
            H.VelasGrafico(velas);
            for (long t = t0 - 30000; t <= t0 + 150000; t += 1000) H.Tick(t, 500 + (t - t0) / 1000.0);
            var h2 = H.VelasM2(AdaptadoresFamilia.DeMs(t0), AdaptadoresFamilia.DeMs(t0 + 120000));
            Ok(h2.Count == 1 && h2[0].O == 500 && h2[0].C == 619, "balde escuchado entero: vela de ticks (" + (h2.Count > 0 ? F(h2[0].O) + "/" + F(h2[0].C) : "-") + ")");
            var h3 = H.VelasM2(AdaptadoresFamilia.DeMs(t0 - 120000), AdaptadoresFamilia.DeMs(t0));
            Ok(h3.Count == 1 && h3[0].O == 470, "balde anterior escuchado a medias y sin vela del grafico: la de ticks incompleta, como ultimo recurso (" + h3.Count + ")");
            Ok(Igual(H.PrecioSoloTick(AdaptadoresFamilia.DeMs(t0 - 25000)), 475), "PrecioSoloTick dentro de lo escuchado");
            Ok(double.IsNaN(H.PrecioSoloTick(AdaptadoresFamilia.DeMs(t0 - 31000))), "PrecioSoloTick antes de escuchar: NaN (no hay tick)");
            H.VelasGrafico(new List<(long, long, double, double, double, double)> { (t0 - 120000, t0 - 61000, 1000, 1001, 999, 1000.5), (t0 - 60000, t0 - 1000, 1000.5, 1002, 1000, 1001) });
            var h4 = H.VelasM2(AdaptadoresFamilia.DeMs(t0 - 120000), AdaptadoresFamilia.DeMs(t0));
            Ok(h4.Count == 1 && h4[0].O == 1000 && h4[0].C == 1001, "balde escuchado a medias CON vela del grafico: manda la del grafico");
            Console.WriteLine("5. velas del grafico: respaldo m2, interpolacion, rechazo de marcos > 2 min y prioridad de los ticks escuchados");
        }

        // ---------------------------------------------------------------- 6. ILibroNq: dedup por ts, orden, rango
        {
            var L = new LibroNqFamilia("NQ");
            int buenas = 0; var tsVistos = new HashSet<string>();
            foreach (var l in lineas) { if (L.AgregarLinea(l, "archivo")) buenas++; var f = LibroNqFamilia.ParsearViva3(l, "NQ", out _); if (f != null) tsVistos.Add(f.TsUtc.ToString("s")); }
            var todas = L.Fotos(DateTime.MinValue.AddYears(1), DateTime.MaxValue.AddYears(-1));
            Ok(todas.Count == tsVistos.Count, "una foto por ts: " + todas.Count + " vs " + tsVistos.Count);
            Ok(todas.Zip(todas.Skip(1), (a, b) => a.TsUtc < b.TsUtc).All(x => x), "ordenadas por ts");
            Ok(!L.AgregarLinea(lineas[lineas.Length / 2], "vivo"), "la misma linea otra vez no entra (dedup: gana la de mas filas)");
            var u = L.Ultima();
            Ok(u != null && u.TsUtc == todas[todas.Count - 1].TsUtc, "Ultima = la de ts mayor");
            var medio = todas[todas.Count / 2].TsUtc;
            var rango = L.Fotos(medio, medio.AddMinutes(10));
            Ok(rango.Count > 0 && rango[0].TsUtc == medio && rango.All(f => f.TsUtc >= medio && f.TsUtc < medio.AddMinutes(10)), "Fotos(desde, hasta) = [desde, hasta)");
            Console.WriteLine("6. ILibroNq: " + todas.Count + " fotos de " + lineas.Length + " lineas (" + buenas + " aceptadas), dedup, orden y rango; " + L.Estado);
        }

        // ---------------------------------------------------------------- 7. ciclo de vida: Registrar (hilo, siembra) / Soltar (persiste)
        {
            string tmp = Path.Combine(aqui, "tmp");
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            AdaptadoresFamilia.Carpeta = tmp;
            Directory.CreateDirectory(Path.Combine(tmp, "viva"));
            // una linea viva3 real con el ts corrido a hace 1 h (la siembra guarda 30 h)
            var ahora = DateTime.UtcNow;
            string ts0 = ahora.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss", Inv), ts1 = ahora.AddMinutes(-59).ToString("yyyy-MM-dd HH:mm:ss", Inv);
            string l0 = lineas[lineas.Length / 2];
            int p0 = l0.IndexOf("\"ts\":\"", StringComparison.Ordinal) + 6;
            string conTs(string ts) => l0.Substring(0, p0) + ts + l0.Substring(p0 + 19);
            File.WriteAllLines(Path.Combine(tmp, "viva", "viva3-NQ-" + ahora.AddHours(-1).ToString("yyyy-MM-dd", Inv) + ".jsonl"), new[] { conTs(ts0) });
            File.AppendAllLines(Path.Combine(tmp, "viva", "viva3-NQ-" + ahora.AddMinutes(-59).ToString("yyyy-MM-dd", Inv) + ".jsonl"), new[] { conTs(ts1) });
            var duenio = new object();
            var L = LibroNqFamilia.Para("NQ");
            L.Registrar(duenio);
            for (int i = 0; i < 100 && !L.Sembrado; i++) System.Threading.Thread.Sleep(20);
            Ok(L.Sembrado && L.Fotos(ahora.AddHours(-2), ahora).Count == 2, "siembra del ILibroNq desde PythiaGex4/viva: " + L.Estado);
            Ok(L.AgregarLinea(conTs(ahora.ToString("yyyy-MM-dd HH:mm:ss", Inv)), "vivo") && L.Ultima().TsUtc > ahora.AddSeconds(-2), "la linea del minuto entra despues de la siembra");
            L.Soltar(duenio);

            var K = CintaFamilia.Para("TESTZ6");
            Ok(ReferenceEquals(K, CintaFamilia.Para("TESTZ6")), "una cinta por instrumento");
            K.Registrar(duenio);
            long t0 = AdaptadoresFamilia.Ms(ahora) - 60000;
            for (int i = 0; i < 50; i++) K.Tick(t0 + i * 1000, 20000 + i);
            for (int i = 0; i < 100 && K.Estado.Contains("cargando"); i++) System.Threading.Thread.Sleep(20);
            K.Soltar(duenio);
            var arch = Directory.GetFiles(Path.Combine(tmp, "cinta"), "seg-TESTZ6-*.bin");
            Ok(arch.Length >= 1, "al soltar la ultima instancia se persiste (" + arch.Length + " archivo/s)");
            var K2 = new CintaFamilia("TESTZ6");
            foreach (var f in arch) K2.CargarArchivoDia(f);
            Ok(Igual(K2.PrecioSoloTick(AdaptadoresFamilia.DeMs(t0 + 49000)), 20049), "lo persistido se lee igual (" + K2.Estado + ")");
            K.Registrar(duenio);   // vuelve a arrancar (nuevo tramo de escucha)
            K.Soltar(duenio);
            Directory.Delete(tmp, true);
            Console.WriteLine("7. ciclo de vida: siembra del libro, una cinta por instrumento, persistencia al soltar, re-arranque");
        }

        Console.WriteLine((_fallas == 0 ? "TODO OK" : "FALLAS: " + _fallas) + " · " + _pruebas + " comprobaciones en " + reloj.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s");
        return _fallas == 0 ? 0 : 1;
    }

    static void Comparar(string nombre, CintaFamilia X, JsonElement c)
    {
        long ini = c.GetProperty("ini").GetInt64(), fin = c.GetProperty("fin").GetInt64();
        // velas m2 cerradas de la sesion
        var esp = c.GetProperty("velas").EnumerateArray().Select(v => (Ms: v[0].GetInt64(), O: Dr(v[1]), H: Dr(v[2]), L: Dr(v[3]), C: Dr(v[4]))).ToList();
        var mias = X.VelasM2(AdaptadoresFamilia.DeMs(ini), AdaptadoresFamilia.DeMs(fin));
        int malV = 0;
        if (mias.Count != esp.Count) malV = Math.Abs(mias.Count - esp.Count);
        for (int i = 0; i < Math.Min(mias.Count, esp.Count); i++)
        {
            var a = mias[i]; var b = esp[i];
            if (a.Ms != b.Ms || a.O != b.O || a.H != b.H || a.L != b.L || a.C != b.C) { malV++; if (malV <= 3) Console.WriteLine("   vela " + a.Ms + " " + F(a.O) + "/" + F(a.H) + "/" + F(a.L) + "/" + F(a.C) + " vs " + b.Ms + " " + F(b.O) + "/" + F(b.H) + "/" + F(b.L) + "/" + F(b.C)); }
        }
        Ok(malV == 0, nombre + ": velas m2 " + malV + " distintas (" + mias.Count + " vs " + esp.Count + ")");
        // cierre conocido en la grilla de minutos
        var grilla = c.GetProperty("grilla").EnumerateArray().Select(x => x.GetInt64()).ToArray();
        var cierres = c.GetProperty("cierres").EnumerateArray().Select(Dr).ToArray();
        int malC = 0, nanC = 0;
        for (int i = 0; i < grilla.Length; i++)
        {
            double v = X.CierreConocido(AdaptadoresFamilia.DeMs(grilla[i]));
            if (double.IsNaN(cierres[i])) nanC++;
            if (!Igual(v, cierres[i])) { malC++; if (malC <= 3) Console.WriteLine("   cierre " + AdaptadoresFamilia.DeMs(grilla[i]).ToString("HH:mm", Inv) + " " + F(v) + " vs " + F(cierres[i])); }
        }
        Ok(malC == 0, nombre + ": CierreConocido " + malC + " distintos de " + grilla.Length);
        // Precio y PrecioSoloTick: exacto en segundos enteros; los fraccionarios se cuentan aparte (diseño: casilla por segundo)
        var inst = c.GetProperty("instantes").EnumerateArray().Select(x => x.GetInt64()).ToArray();
        var pr = c.GetProperty("precio").EnumerateArray().Select(Dr).ToArray();
        var pt = c.GetProperty("precio_tick").EnumerateArray().Select(Dr).ToArray();
        int malP = 0, malT = 0, fracP = 0, fracT = 0, nEnt = 0, nFrac = 0, porVela = 0;
        double maxFrac = 0; long maxFracT = 0; double maxFracMio = 0, maxFracPy = 0;
        for (int i = 0; i < inst.Length; i++)
        {
            var t = AdaptadoresFamilia.DeMs(inst[i]);
            double p = X.Precio(t), q = X.PrecioSoloTick(t, 120);
            bool entero = inst[i] % 1000 == 0;
            if (entero) nEnt++; else nFrac++;
            if (!double.IsNaN(pr[i]) && double.IsNaN(pt[i])) porVela++;
            if (!Igual(p, pr[i])) { if (entero) { malP++; if (malP <= 3) Console.WriteLine("   precio " + t.ToString("HH:mm:ss", Inv) + " " + F(p) + " vs " + F(pr[i])); } else { fracP++; if (!double.IsNaN(p) && !double.IsNaN(pr[i]) && Math.Abs(p - pr[i]) > maxFrac) { maxFrac = Math.Abs(p - pr[i]); maxFracT = inst[i]; maxFracMio = p; maxFracPy = pr[i]; } } }
            if (!Igual(q, pt[i])) { if (entero) { malT++; if (malT <= 3) Console.WriteLine("   precio tick " + t.ToString("HH:mm:ss", Inv) + " " + F(q) + " vs " + F(pt[i])); } else fracT++; }
        }
        Ok(malP == 0, nombre + ": Precio (segundos enteros) " + malP + " distintos de " + nEnt);
        Ok(malT == 0, nombre + ": PrecioSoloTick (segundos enteros) " + malT + " distintos de " + nEnt);
        Console.WriteLine("   " + nombre + ": velas " + mias.Count + "/" + esp.Count + " (dif " + malV + "), cierres " + grilla.Length + " (dif " + malC + ", NaN en Python " + nanC + "), Precio " + nEnt + " enteros (dif " + malP + ", " + porVela + " por vela), PrecioSoloTick (dif " + malT
            + "); fraccionarios " + nFrac + ": dif Precio " + fracP + " (max " + maxFrac.ToString("0.##", Inv) + " pts" + (maxFracT > 0 ? " a las " + AdaptadoresFamilia.DeMs(maxFracT).ToString("HH:mm:ss.fff", Inv) + ": " + F(maxFracMio) + " vs " + F(maxFracPy) + ", tick del segundo anterior " + F(X.PrecioSoloTick(AdaptadoresFamilia.DeMs(maxFracT - maxFracT % 1000))) : "") + "), dif tick " + fracT + " [por diseño: casilla por segundo]");
    }

    static string BuscarAqui()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "ref_adaptadores.json"))) d = d.Parent;
        if (d == null) throw new Exception("no encuentro ref_adaptadores.json hacia arriba de " + AppContext.BaseDirectory);
        return d.FullName;
    }
}
