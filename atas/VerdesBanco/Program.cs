using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using PythiaGex;

// VERDES · BANCO: corre el MISMO nucleo del indicador (VerdesNucleo.cs) sobre la cinta grabada, operacion por operacion, con las rayas de un archivo tipo estela.
// Uso: VerdesBanco --cinta cinta.csv --rayas rayas.jsonl --salida eventos.jsonl [--sin-delta] [--objetivo 20] [--corrimientos 37.5,-37.5,62.5,-62.5]
static class Programa
{
    static int Main(string[] a)
    {
        var inv = CultureInfo.InvariantCulture; string cinta = null, rayas = null, salida = null; var n = new VerdesNucleo();
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] == "--cinta") cinta = a[++i]; else if (a[i] == "--rayas") rayas = a[++i]; else if (a[i] == "--salida") salida = a[++i];
            else if (a[i] == "--sin-delta") n.A.ExigirDelta = false; else if (a[i] == "--objetivo") n.A.Objetivo = double.Parse(a[++i], inv);
            else if (a[i] == "--corrimientos") n.A.Corrimientos = a[++i].Split(',').Where(x => x.Length > 0).Select(x => double.Parse(x, inv)).ToArray();
            else if (a[i] == "--tol") n.A.Tol = double.Parse(a[++i], inv); else if (a[i] == "--lejos") n.A.Lejos = double.Parse(a[++i], inv);
        }
        if (cinta == null || rayas == null || salida == null) { Console.Error.WriteLine("faltan --cinta --rayas --salida"); return 2; }
        var lineas = new List<(DateTime T, List<double> D, List<double> G, bool Oi, double N, double F)>();
        foreach (var l in File.ReadLines(rayas))
        {
            if (l.Length < 10) continue;
            try
            {
                using var doc = JsonDocument.Parse(l); var r = doc.RootElement;
                var t = DateTime.Parse(r.GetProperty("t").GetString(), inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                lineas.Add((t, r.GetProperty("d").EnumerateArray().Select(x => x.GetDouble()).Take(2).ToList(), r.TryGetProperty("g", out var g) ? g.EnumerateArray().Select(x => x.GetDouble()).ToList() : new List<double>(),
                            r.TryGetProperty("b", out var b) && b.GetString() == "OI", r.TryGetProperty("n", out var nn) ? nn.GetDouble() : 0, r.TryGetProperty("f", out var f) ? f.GetDouble() : 0));
            }
            catch { }
        }
        lineas.Sort((x, y) => x.T.CompareTo(y.T));
        n.Arrancar(DateTime.MinValue); int k = 0, ops = 0, ev = 0;
        using var w = new StreamWriter(salida, false);
        n.Evento = e => { w.WriteLine(VerdesNucleo.AJson(e, "banco", "NQ")); ev++; };
        using var rd = new StreamReader(cinta); string cab = rd.ReadLine(); string s;
        while ((s = rd.ReadLine()) != null)
        {
            var c = s.Split(','); if (c.Length < 5) continue;
            if (!DateTime.TryParse(c[0], inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)) continue;
            while (k < lineas.Count && lineas[k].T <= t) { var q = lineas[k++]; n.Rayas(q.T, q.D, q.G, q.Oi, q.N, q.F); }
            double pri = double.Parse(c[1], inv), ult = double.Parse(c[2], inv), vol = double.Parse(c[3], inv), lado = double.Parse(c[4], inv);
            if (pri != ult) n.Operacion(t, pri, 0);          // la orden barrio varios precios: primero el de arranque, despues el final con todo el volumen
            n.Operacion(t, ult, vol * lado); ops++;
        }
        Console.WriteLine(ops + " ordenes, " + lineas.Count + " lineas de rayas, " + ev + " eventos -> " + salida);
        return 0;
    }
}
