using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using PythiaLaOla;

// ARNES DE LA OLA 1.0 (27-11-2026), SIN ATAS: corre el MISMO codigo del indicador (atas/PythiaOla/OlaMotor.cs, OlaNucleo.cs y OlaClima.cs)
// sobre cintas REALES de la sonda. Los modos de la ola se mudaron aca desde atas/_test_super (el Superindicador volvio a 1.9b y ya no
// tiene la ola); los compara herramientas/test_ola.py y herramientas/auditar_ola.py igual que antes:
//   ola <cinta.csv> <salida.txt> <X> <paso_s> <nombre> <desde_utc> <hasta_utc> [<nombre> <desde_utc> <hasta_utc> ...]
//   x <salida.json> <tick> <cinta.csv> [<cinta.csv> ...]
//   velocidad <cinta.csv> [X] [repeticiones]
//   flujo <dir_salida> <cinta_vispera.csv> <cinta_jornada.csv> <x_esperada> <cinta_previa.csv> [...]       (FlujoOla.cs)
// y el modo nuevo de La Ola, contra la referencia de la ronda 21 (ArnesR21.cs; lo compara herramientas/auditar_pythiaola.py):
//   r21 <dir_salida> <AAAA-MM-DD> <cinta_jornada.csv> <cinta_previa.csv> [...]
partial class Program
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(decimal v) => v.ToString(Inv);

    static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "ola") { Ola_(args); return 0; }
            if (args.Length > 0 && args[0] == "x") { EscalaX_(args); return 0; }
            if (args.Length > 0 && args[0] == "velocidad") { Velocidad_(args); return 0; }
            if (args.Length > 0 && args[0] == "flujo") return Flujo_(args);
            if (args.Length > 0 && args[0] == "r21") return R21_(args);
            if (args.Length > 0 && args[0] == "cortes") return Cortes_(args);   // La Ola 1.0b: el detector de cortes de datos (ArnesCortes.cs)
            Console.WriteLine("modos: ola | x | velocidad | flujo | r21 (ver el encabezado de Program.cs)");
            return 2;
        }
        catch (Exception e) { Console.WriteLine("ERROR " + e); return 3; }
    }

    // =====================================================================================================================
    // 27-11-2026 EL MOTOR DE LA OLA sobre cintas reales de la sonda (t,primero,ultimo,vol,lado,prints,...; t en UTC).
    // =====================================================================================================================

    struct OrdenCinta
    {
        public DateTime T;
        public decimal Primero, Ultimo, Vol;
        public int Lado, Prints;
    }

    static string H(DateTime t) => t == DateTime.MinValue ? "" : t.ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv);

    static DateTime Utc(string s) => DateTime.ParseExact(s, "yyyy-MM-ddTHH:mm:ss", Inv);

    static void BajaPrioridad()
    {
        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
    }

    static ReadOnlySpan<char> Campo(ref ReadOnlySpan<char> resto)
    {
        int i = resto.IndexOf(',');
        ReadOnlySpan<char> c;
        if (i < 0) { c = resto; resto = ReadOnlySpan<char>.Empty; }
        else { c = resto.Slice(0, i); resto = resto.Slice(i + 1); }
        return c;
    }

    static int Digitos(ReadOnlySpan<char> s, int i, int n)
    {
        int v = 0;
        for (int k = 0; k < n; k++)
        {
            int d = s[i + k] - '0';
            if ((uint)d > 9) throw new FormatException("hora mal escrita: " + s.ToString());
            v = v * 10 + d;
        }
        return v;
    }

    /// <summary>yyyy-MM-ddTHH:mm:ss.fff, los 23 caracteres que escribe la sonda (la referencia lee x[0][:23]).</summary>
    static DateTime Hora(ReadOnlySpan<char> s)
    {
        if (s.Length != 23 || s[4] != '-' || s[7] != '-' || s[10] != 'T' || s[13] != ':' || s[16] != ':' || s[19] != '.')
            throw new FormatException("hora mal escrita: " + s.ToString());
        return new DateTime(Digitos(s, 0, 4), Digitos(s, 5, 2), Digitos(s, 8, 2), Digitos(s, 11, 2), Digitos(s, 14, 2), Digitos(s, 17, 2), Digitos(s, 20, 3));
    }

    /// <summary>Las ordenes de la cinta EN EL ORDEN DEL ARCHIVO y sin sacar duplicados, igual que la referencia. Una fila rota corta todo
    /// (en Python tambien reventaria).</summary>
    static IEnumerable<OrdenCinta> LeerCinta(string ruta)
    {
        using var sr = new StreamReader(ruta, Encoding.ASCII, false, 1 << 20);
        string linea = sr.ReadLine();
        if (linea == null || !linea.StartsWith("t,primero,ultimo,vol,lado", StringComparison.Ordinal))
            throw new InvalidDataException("encabezado inesperado en " + ruta + ": " + linea);
        long nl = 1;
        while ((linea = sr.ReadLine()) != null)
        {
            nl++;
            OrdenCinta o;
            try
            {
                ReadOnlySpan<char> r = linea;
                o.T = Hora(Campo(ref r));
                o.Primero = decimal.Parse(Campo(ref r), NumberStyles.Float, Inv);
                o.Ultimo = decimal.Parse(Campo(ref r), NumberStyles.Float, Inv);
                o.Vol = decimal.Parse(Campo(ref r), NumberStyles.Float, Inv);
                o.Lado = int.Parse(Campo(ref r), NumberStyles.AllowLeadingSign, Inv);
                var p = Campo(ref r);
                o.Prints = p.Length > 0 ? int.Parse(p, NumberStyles.None, Inv) : 1;
            }
            catch (Exception e) { throw new InvalidDataException(Path.GetFileName(ruta) + " linea " + nl + ": '" + linea + "': " + e.Message); }
            yield return o;
        }
    }

    sealed class VentanaOla
    {
        public string Nombre;
        public DateTime Desde, Hasta;
        public bool Vivo;                    // las ordenes de varios prints entran creciendo (Orden + CorregirUltima)
        public OlaFotos Ola, Marea;
        public long Ordenes, Correcciones;
    }

    /// <summary>Version j de k de una orden acumulada que crece, como la ve ATAS en vivo: la barrida se estira de a ticks (0,25: MES y ES)
    /// desde 'primero' y el volumen sube; la version k es la orden completa, la de la cinta.</summary>
    static void Version(in OrdenCinta o, int j, int k, out decimal ultimo, out decimal vol)
    {
        if (j >= k) { ultimo = o.Ultimo; vol = o.Vol; return; }
        long ticks = (long)decimal.Truncate((o.Ultimo - o.Primero) / 0.25m);
        ultimo = o.Primero + (ticks * j / k) * 0.25m;
        vol = Math.Max(1m, decimal.Floor(o.Vol * j / k));
    }

    static void Pasar(List<VentanaOla> vs, in OrdenCinta o, DateTime siguiente)
    {
        foreach (var v in vs)
        {
            if (o.T < v.Desde || o.T >= v.Hasta) continue;
            v.Ordenes++;
            if (!v.Vivo || o.Prints <= 1)
            {
                v.Ola.Orden(o.T, o.Primero, o.Ultimo, o.Vol, o.Lado);
                v.Marea.Orden(o.T, o.Primero, o.Ultimo, o.Vol, o.Lado);
                continue;
            }
            int k = Math.Min(o.Prints, 6);
            for (int j = 1; j <= k; j++)
            {
                Version(o, j, k, out decimal ult, out decimal vol);
                if (j == 1)
                {
                    v.Ola.Orden(o.T, o.Primero, ult, vol, o.Lado);
                    v.Marea.Orden(o.T, o.Primero, ult, vol, o.Lado);
                    // el reloj sigue mientras la orden crece: se toman ya las fotos de los cortes que caen antes de la orden siguiente
                    // (con la version chica adentro) para que CorregirUltima tenga que rehacerlas
                    DateTime h = siguiente == DateTime.MaxValue ? v.Hasta : siguiente.AddMilliseconds(-1);
                    if (h > v.Hasta) h = v.Hasta;
                    v.Ola.TomarHasta(h); v.Marea.TomarHasta(h);
                }
                else
                {
                    v.Ola.CorregirUltima(o.T, o.Primero, ult, vol, o.Lado);
                    v.Marea.CorregirUltima(o.T, o.Primero, ult, vol, o.Lado);
                    v.Correcciones++;
                }
            }
        }
    }

    static void EscribirPivote(StreamWriter w, string ven, string esc, int k, in PivoteOla p)
    {
        w.Write("P;"); w.Write(ven); w.Write(';'); w.Write(esc); w.Write(';'); w.Write(k.ToString(Inv)); w.Write(';');
        w.Write(H(p.T)); w.Write(';'); w.Write(F(p.Precio)); w.Write(';'); w.Write(p.Tipo.ToString(Inv)); w.Write(';');
        w.Write(p.Confirmado ? "1" : "0"); w.Write(';'); w.Write(H(p.TConf)); w.Write(';'); w.Write(p.NConf.ToString(Inv)); w.Write(';');
        w.Write(F(p.Cum)); w.Write('\n');
    }

    static void EscribirFoto(StreamWriter w, string ven, string esc, int k, DateTime corte, in FotoOla f)
    {
        var sb = new StringBuilder(256);
        sb.Append("F;").Append(ven).Append(';').Append(esc).Append(';').Append(k).Append(';').Append(H(corte)).Append(';')
          .Append(f.N).Append(';').Append(H(f.T)).Append(';').Append(f.N > 0 ? F(f.Ultimo) : "").Append(';').Append(F(f.Cum)).Append(';')
          .Append(f.Dir).Append(';').Append(f.Pivotes);
        if (f.HayPierna)
        {
            sb.Append(';').Append(H(f.Origen.T)).Append(';').Append(F(f.Origen.Precio)).Append(';').Append(f.Origen.Tipo).Append(';').Append(F(f.Origen.Cum))
              .Append(';').Append(H(f.Extremo.T)).Append(';').Append(F(f.Extremo.Precio)).Append(';').Append(F(f.Extremo.Cum))
              .Append(';').Append(F(f.DeltaPierna)).Append(';').Append(f.ConAgresores ? 1 : 0).Append(';').Append(F(f.Retroceso))
              .Append(';').Append(F(f.Faltan)).Append(';').Append(F(f.Raya)).Append(';').Append(F(f.Recorrido));
        }
        else sb.Append(";;;;;;;;;;;;;");
        w.Write(sb.ToString()); w.Write('\n');
    }

    /// <summary>
    /// ola: la ola (X) y la marea (4X) de SuperCalculos sobre una cinta, en una o mas ventanas [desde, hasta) UTC, cada una con su motor
    /// arrancado de cero y fotos cada 'paso' segundos (cortes desde + paso, desde + 2 paso, ... &lt;= hasta). Cada ventana corre dos
    /// veces: normal y VIVO (las ordenes de varios prints entran creciendo); las dos tienen que dar lo mismo que la referencia.
    /// </summary>
    static void Ola_(string[] a)
    {
        BajaPrioridad();
        if (a.Length < 8 || (a.Length - 5) % 3 != 0) throw new ArgumentException("uso: ola <cinta.csv> <salida.txt> <X> <paso_s> <nombre> <desde_utc> <hasta_utc> [...]");
        string cinta = a[1], salida = a[2];
        decimal x = decimal.Parse(a[3], Inv);
        int paso = int.Parse(a[4], Inv);
        var vs = new List<VentanaOla>();
        for (int i = 5; i + 2 < a.Length; i += 3)
            foreach (bool vivo in new[] { false, true })
            {
                var v = new VentanaOla { Nombre = a[i] + (vivo ? "_vivo" : ""), Desde = Utc(a[i + 1]), Hasta = Utc(a[i + 2]), Vivo = vivo, Ola = new OlaFotos(x), Marea = new OlaFotos(4 * x) };
                for (var c = v.Desde.AddSeconds(paso); c <= v.Hasta; c = c.AddSeconds(paso)) { v.Ola.AgregarCorte(c); v.Marea.AgregarCorte(c); }
                vs.Add(v);
            }
        var sw = Stopwatch.StartNew();
        long filas = 0;
        OrdenCinta prev = default;
        foreach (var o in LeerCinta(cinta))   // con una orden de adelanto: el modo vivo necesita saber cuando llega la siguiente
        {
            if (filas++ > 0) Pasar(vs, prev, o.T);
            prev = o;
        }
        if (filas > 0) Pasar(vs, prev, DateTime.MaxValue);
        foreach (var v in vs) { v.Ola.TomarHasta(v.Hasta); v.Marea.TomarHasta(v.Hasta); }
        double ms = sw.Elapsed.TotalMilliseconds;
        using (var w = new StreamWriter(salida, false, new UTF8Encoding(false), 1 << 20))
        {
            w.Write("# ola " + Path.GetFileName(cinta) + " X=" + F(x) + " paso=" + paso + " filas=" + filas + "\n");
            foreach (var v in vs)
            {
                w.Write("V;" + v.Nombre + ";" + H(v.Desde) + ";" + H(v.Hasta) + ";" + v.Ordenes + ";" + v.Correcciones + ";" + v.Ola.CortesTarde + ";" + v.Marea.CortesTarde + "\n");
                foreach (var (esc, of) in new[] { ("X", v.Ola), ("4X", v.Marea) })
                {
                    var piv = of.Ola.Pivotes;
                    for (int k = 0; k < piv.Count; k++) EscribirPivote(w, v.Nombre, esc, k, piv[k]);
                    var act = of.Actual;
                    if (act.HayPierna) EscribirPivote(w, v.Nombre, esc, piv.Count, act.Extremo);   // el extremo final, sin confirmar
                    for (int k = 0; k < of.Tomadas; k++) EscribirFoto(w, v.Nombre, esc, k, of.Corte(k), of.Foto(k));
                }
            }
        }
        Console.WriteLine("ola: " + Path.GetFileName(cinta) + " " + filas + " filas en " + ms.ToString("0", Inv) + " ms (lectura incluida)");
        foreach (var v in vs)
            Console.WriteLine("  " + v.Nombre.PadRight(14) + " ordenes " + v.Ordenes + ", correcciones " + v.Correcciones + ", pivotes X " + v.Ola.Ola.Pivotes.Count
                              + " / 4X " + v.Marea.Ola.Pivotes.Count + ", fotos " + v.Ola.Tomadas + ", cortes tarde " + (v.Ola.CortesTarde + v.Marea.CortesTarde));
    }

    /// <summary>x: rango RTH de cada rueda de las cintas (RangosRth, hora NY con la zona de Windows) y la X de cada rueda con las 10
    /// anteriores de la lista (EscalaOla).</summary>
    static void EscalaX_(string[] a)
    {
        BajaPrioridad();
        string salida = a[1];
        decimal tick = decimal.Parse(a[2], Inv);
        var zona = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var rr = new RangosRth();
        var sw = Stopwatch.StartNew();
        long filas = 0, horaUtc = long.MinValue;
        TimeSpan corr = TimeSpan.Zero;
        for (int i = 3; i < a.Length; i++)
            foreach (var o in LeerCinta(a[i]))
            {
                filas++;
                long h = o.T.Ticks / TimeSpan.TicksPerHour;   // el corrimiento de NY cambia en horas enteras: se calcula una vez por hora UTC
                if (h != horaUtc) { horaUtc = h; corr = zona.GetUtcOffset(new DateTime(h * TimeSpan.TicksPerHour, DateTimeKind.Utc)); }
                rr.Agregar(o.T + corr, o.Primero, o.Ultimo);
            }
        var sb = new StringBuilder("[");
        int n = 0;
        foreach (var r in rr.Lista)
        {
            decimal x = rr.XDe(r.Clave, tick, out decimal med, out int usadas);
            if (n++ > 0) sb.Append(",\n");
            sb.Append("{\"dia\":\"").Append(Jornadas.Fecha(r.Clave).ToString("yyyy-MM-dd", Inv)).Append("\",\"alto\":").Append(F(r.Alto)).Append(",\"bajo\":")
              .Append(F(r.Bajo)).Append(",\"rango\":").Append(F(r.Rango)).Append(",\"ordenes_rth\":").Append(r.Ordenes).Append(",\"x\":").Append(F(x))
              .Append(",\"mediana\":").Append(F(med)).Append(",\"usadas\":").Append(usadas).Append('}');
        }
        sb.Append("]");
        File.WriteAllText(salida, sb.ToString());
        Console.WriteLine("x: " + (a.Length - 3) + " cintas, " + filas + " filas, " + n + " ruedas en " + sw.ElapsedMilliseconds + " ms -> " + salida);
    }

    static void Medir(string nombre, int reps, int n, Func<long> f)
    {
        f();   // calienta el JIT
        var ms = new List<double>();
        long control = 0;
        for (int r = 0; r < reps; r++)
        {
            var sw = Stopwatch.StartNew();
            control = f();
            ms.Add(sw.Elapsed.TotalMilliseconds);
        }
        ms.Sort();
        double med = ms[ms.Count / 2];
        Console.WriteLine(nombre.PadRight(58) + " mediana " + med.ToString("0.0", Inv).PadLeft(7) + " ms, mejor " + ms[0].ToString("0.0", Inv).PadLeft(7)
                          + " ms | " + (n / med / 1000.0).ToString("0.00", Inv) + " M ordenes/s | " + (med * 800000.0 / n).ToString("0.0", Inv)
                          + " ms por 800 mil (control " + control + ")");
    }

    /// <summary>velocidad: la cinta entera en memoria y el motor solo, sin lectura ni escritura.</summary>
    static void Velocidad_(string[] a)
    {
        BajaPrioridad();
        string cinta = a[1];
        decimal x = a.Length > 2 ? decimal.Parse(a[2], Inv) : 9.75m;
        int reps = a.Length > 3 ? int.Parse(a[3], Inv) : 7;
        var sw = Stopwatch.StartNew();
        var lista = new List<OrdenCinta>(1 << 20);
        foreach (var o in LeerCinta(cinta)) lista.Add(o);
        double msLeer = sw.Elapsed.TotalMilliseconds;
        var os = lista.ToArray();
        int n = os.Length;
        Console.WriteLine("cinta " + Path.GetFileName(cinta) + ": " + n + " ordenes; lectura y parseo del CSV " + msLeer.ToString("0", Inv) + " ms (" + (n / msLeer / 1000.0).ToString("0.00", Inv) + " M filas/s); X = " + F(x));
        var t0 = new DateTime(os[0].T.Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute);
        Medir("1 motor (X)", reps, n, () =>
        {
            var m = new Ola(x);
            for (int i = 0; i < n; i++) { ref var q = ref os[i]; m.Orden(q.T, q.Primero, q.Ultimo, q.Vol, q.Lado); }
            return m.Pivotes.Count;
        });
        Medir("ola + marea (X y 4X)", reps, n, () =>
        {
            var m1 = new Ola(x); var m4 = new Ola(4 * x);
            for (int i = 0; i < n; i++) { ref var q = ref os[i]; m1.Orden(q.T, q.Primero, q.Ultimo, q.Vol, q.Lado); m4.Orden(q.T, q.Primero, q.Ultimo, q.Vol, q.Lado); }
            return m1.Pivotes.Count + m4.Pivotes.Count;
        });
        foreach (int seg in new[] { 60, 30 })
            Medir("ola + marea con fotos cada " + seg + " s (velas de " + seg + " s)", reps, n, () =>
            {
                var f1 = new OlaFotos(x); var f4 = new OlaFotos(4 * x);
                for (var c = t0.AddSeconds(seg); c <= os[n - 1].T.AddSeconds(seg); c = c.AddSeconds(seg)) { f1.AgregarCorte(c); f4.AgregarCorte(c); }
                for (int i = 0; i < n; i++) { ref var q = ref os[i]; f1.Orden(q.T, q.Primero, q.Ultimo, q.Vol, q.Lado); f4.Orden(q.T, q.Primero, q.Ultimo, q.Vol, q.Lado); }
                return f1.Tomadas + f4.Tomadas;
            });
        Medir("ola + marea VIVO (una CorregirUltima por print, hasta 6)", reps, n, () =>
        {
            var f1 = new OlaFotos(x); var f4 = new OlaFotos(4 * x);
            long correcciones = 0;
            for (int i = 0; i < n; i++)
            {
                ref var q = ref os[i];
                int k = q.Prints <= 1 ? 1 : Math.Min(q.Prints, 6);
                for (int j = 1; j <= k; j++)
                {
                    Version(q, j, k, out decimal ult, out decimal vol);
                    if (j == 1) { f1.Orden(q.T, q.Primero, ult, vol, q.Lado); f4.Orden(q.T, q.Primero, ult, vol, q.Lado); }
                    else { f1.CorregirUltima(q.T, q.Primero, ult, vol, q.Lado); f4.CorregirUltima(q.T, q.Primero, ult, vol, q.Lado); correcciones++; }
                }
            }
            return correcciones;
        });
    }
}
