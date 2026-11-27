using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using PythiaSuper;

// Genera casos sinteticos (con semillas fijas), los suma con SuperCalculos y escribe entrada + salida en JSON.
// La salida la recalcula por su cuenta herramientas/test_superindicador.py.
// 27-11-2026, modos con cintas REALES para el motor de la ola (los compara herramientas/test_ola.py):
//   ola <cinta.csv> <salida.txt> <X> <paso_s> <nombre> <desde_utc> <hasta_utc> [<nombre> <desde_utc> <hasta_utc> ...]
//   x <salida.json> <tick> <cinta.csv> [<cinta.csv> ...]
//   velocidad <cinta.csv> [X] [repeticiones]
// 27-11-2026 (2.0a) el FLUJO del indicador con el mismo codigo de ATAS (SuperOlaNucleo.cs), en FlujoOla.cs:
//   flujo <dir_salida> <cinta_vispera.csv> <cinta_jornada.csv> <x_esperada> <cinta_previa.csv> [...]
partial class Program
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(decimal v) => v.ToString(Inv);

    static BarraFp Vela(Random r, int bar, int tickCentro, int rango, bool huecos, bool empates)
    {
        var b = new BarraFp { Bar = bar, Hora = new DateTime(2026, 11, 23, 14, 0, 0).AddMinutes(bar) };
        b.HoraFin = b.Hora.AddSeconds(59);
        int n = 1 + r.Next(rango);
        b.TickMin = tickCentro - r.Next(n);
        b.Bid = new decimal[n]; b.Ask = new decimal[n]; b.Vol = new decimal[n];
        for (int i = 0; i < n; i++)
        {
            if (huecos && r.NextDouble() < 0.15) continue;   // nivel sin operaciones adentro de la vela
            decimal bi = empates ? 5 * r.Next(0, 4) : r.Next(0, 300);
            decimal a = empates ? 5 * r.Next(0, 4) : r.Next(0, 300);
            decimal e = r.NextDouble() < 0.05 ? r.Next(1, 3) : 0;   // volumen sin agresor (Between)
            b.Bid[i] = bi; b.Ask[i] = a; b.Vol[i] = bi + a + e;
            b.SumBid += bi; b.SumAsk += a; b.SumVol += bi + a + e;
            if (bi + a + e > 0) b.Niveles++;
        }
        b.DeltaAtas = b.SumAsk - b.SumBid; b.VolAtas = b.SumVol;
        return b;
    }

    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "ola") { Ola_(args); return; }
        if (args.Length > 0 && args[0] == "x") { EscalaX_(args); return; }
        if (args.Length > 0 && args[0] == "velocidad") { Velocidad_(args); return; }
        if (args.Length > 0 && args[0] == "flujo") { Environment.ExitCode = Flujo_(args); return; }
        string salida = args.Length > 0 ? args[0] : "casos.json";
        var sb = new StringBuilder();
        sb.Append("[");
        int caso = 0;
        int[] ks = { 1, 2, 3, 4, 8 };
        int[] pcts = { 70, 68, 50, 100, 1 };
        int[] pasos = { 1, 2, 3 };
        for (int semilla = 1; semilla <= 400; semilla++)
        {
            var r = new Random(semilla);
            int velas = 1 + r.Next(60);
            bool huecos = semilla % 3 == 0, empates = semilla % 4 == 0;
            int centro = 27300 + r.Next(-200, 200);
            var lista = new List<BarraFp>();
            for (int v = 0; v < velas; v++) { centro += r.Next(-6, 7); lista.Add(Vela(r, v, centro, empates ? 6 : 25, huecos, empates)); }
            int k = ks[semilla % ks.Length], pct = pcts[(semilla / 5) % pcts.Length], paso = pasos[semilla % pasos.Length];
            var a = new Acum(k);
            foreach (var b in lista) a.Sumar(b);
            var p = Perfil.DeAcum(a, 0, velas - 1);
            Calculos.AreaDeValor(p, pct, paso);
            if (caso++ > 0) sb.Append(",\n");
            sb.Append("{\"semilla\":").Append(semilla).Append(",\"k\":").Append(k).Append(",\"pct\":").Append(pct).Append(",\"paso\":").Append(paso).Append(",\"velas\":[");
            for (int v = 0; v < lista.Count; v++)
            {
                var b = lista[v];
                if (v > 0) sb.Append(',');
                sb.Append("{\"tick_min\":").Append(b.TickMin).Append(",\"niveles\":[");
                for (int i = 0, q = 0; i < b.Vol.Length; i++)
                {
                    if (b.Vol[i] == 0 && b.Bid[i] == 0 && b.Ask[i] == 0) continue;
                    if (q++ > 0) sb.Append(',');
                    sb.Append('[').Append(b.TickMin + i).Append(',').Append(F(b.Bid[i])).Append(',').Append(F(b.Ask[i])).Append(',').Append(F(b.Vol[i])).Append(']');
                }
                sb.Append("]}");
            }
            sb.Append("],\"salida\":{\"fila_min\":").Append(p.FilaMin).Append(",\"filas\":").Append(p.Filas)
              .Append(",\"sum_bid\":").Append(F(p.SumBid)).Append(",\"sum_ask\":").Append(F(p.SumAsk)).Append(",\"sum_vol\":").Append(F(p.SumVol))
              .Append(",\"poc\":").Append(p.Poc).Append(",\"vah\":").Append(p.Vah).Append(",\"val\":").Append(p.Val).Append(",\"vol_av\":").Append(F(p.VolAv)).Append(",\"bid\":[");
            for (int i = 0; i < p.Filas; i++) { if (i > 0) sb.Append(','); sb.Append(F(p.Bid[i])); }
            sb.Append("],\"ask\":[");
            for (int i = 0; i < p.Filas; i++) { if (i > 0) sb.Append(','); sb.Append(F(p.Ask[i])); }
            sb.Append("],\"vol\":[");
            for (int i = 0; i < p.Filas; i++) { if (i > 0) sb.Append(','); sb.Append(F(p.Vol[i])); }
            sb.Append("]}}");
        }
        sb.Append("]");
        File.WriteAllText(salida, sb.ToString());
        // formato de numeros
        foreach (var v in new decimal[] { 0, 7, -7, 1019, -501, 99999, -99999, 100000, 123456, -123499, -123500, 2.5m, -0.25m })
            Console.WriteLine("NUM " + F(v) + " -> '" + Calculos.Numero(v, true) + "' / '" + Calculos.Numero(v, false) + "' / miles '" + Calculos.Miles(v) + "'");
        Console.WriteLine("casos " + caso + " -> " + salida);
        // piso con negativos
        foreach (var (t, k) in new[] { (0, 4), (3, 4), (4, 4), (-1, 4), (-4, 4), (-5, 4), (27315, 4) })
            Console.WriteLine("PISO " + t + "/" + k + " = " + Acum.Piso(t, k));
        Jornadas_(args.Length > 1 ? args[1] : "jornadas.json");
    }

    // 1.5: casos sinteticos de JORNADAS: velas en UTC (como las da ATAS) -> hora NY con la zona de Windows (igual que el
    // indicador) -> lista de jornadas, rangos por parte, "a la vista" y "N atras". Python lo rehace con su propia base de husos.
    static void Jornadas_(string salida)
    {
        var ny = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var sb = new StringBuilder("[");
        int casos = 0;
        int[] marcos = { 1, 1, 1, 5, 30, 60 };
        var inicios = new[] { new DateTime(2026, 3, 5), new DateTime(2026, 10, 28), new DateTime(2026, 11, 23), new DateTime(2026, 11, 18), new DateTime(2026, 7, 1), new DateTime(2026, 12, 30) };
        for (int semilla = 1; semilla <= 120; semilla++)
        {
            var r = new Random(1000 + semilla);
            int marco = marcos[semilla % marcos.Length];
            // arranque en UTC a cualquier minuto (multiplo del marco) de un dia de los de la lista + desplazamiento al azar
            var t = inicios[semilla % inicios.Length].AddMinutes(r.Next(0, 3 * 24 * 60) / marco * marco);
            int dias = 2 + r.Next(5);
            var fin = t.AddDays(dias);
            var horas = new List<DateTime>();
            while (t < fin)
            {
                var h = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(t, DateTimeKind.Utc), ny);
                bool cerrado = h.DayOfWeek == DayOfWeek.Saturday || (h.DayOfWeek == DayOfWeek.Friday && h.TimeOfDay >= new TimeSpan(17, 0, 0))
                            || (h.DayOfWeek == DayOfWeek.Sunday && h.TimeOfDay < new TimeSpan(18, 0, 0))
                            || (h.TimeOfDay >= new TimeSpan(17, 0, 0) && h.TimeOfDay < new TimeSpan(18, 0, 0));
                // a veces una vela "imposible" (en el corte o en fin de semana) para probar las reglas de borde
                bool rara = r.NextDouble() < 0.002;
                if ((!cerrado || rara) && r.NextDouble() > 0.03) horas.Add(t);   // 3 % de minutos sin operaciones
                t = t.AddMinutes(marco);
            }
            var js = new List<Jornada>();
            for (int b = 0; b < horas.Count; b++)
                Jornadas.Agregar(js, b, TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(horas[b], DateTimeKind.Utc), ny));
            if (casos++ > 0) sb.Append(",\n");
            sb.Append("{\"semilla\":").Append(semilla).Append(",\"marco\":").Append(marco).Append(",\"utc\":[");
            for (int b = 0; b < horas.Count; b++) { if (b > 0) sb.Append(','); sb.Append('"').Append(horas[b].ToString("yyyy-MM-ddTHH:mm", Inv)).Append('"'); }
            sb.Append("],\"lista\":[");
            for (int k = 0; k < js.Count; k++)
            {
                var j = js[k];
                if (k > 0) sb.Append(',');
                sb.Append('[').Append(j.Clave).Append(',').Append(j.Primera).Append(',').Append(j.Ultima).Append(',').Append(j.NochePrimera).Append(',')
                  .Append(j.NocheUltima).Append(',').Append(j.RthPrimera).Append(',').Append(j.RthUltima).Append(",\"").Append(Jornadas.Rotulo(j.Clave)).Append("\"")
                  .Append(",\"").Append(Jornadas.InicioNominal(j.Clave, Jornadas.ParteCompleta).ToString("yyyy-MM-ddTHH:mm", Inv)).Append("\"")
                  .Append(",\"").Append(Jornadas.InicioNominal(j.Clave, Jornadas.ParteRth).ToString("yyyy-MM-ddTHH:mm", Inv)).Append("\"]");
            }
            sb.Append("],\"vista\":[");
            for (int q = 0; q < 40; q++)
            {
                int n = horas.Count; if (n == 0) break;
                int a = r.Next(n), w = 1 + r.Next(Math.Max(1, n / 2)), parte = r.Next(3);
                int b0 = Math.Max(0, a - w / 2), b1 = Math.Min(n - 1, b0 + w);
                if (q > 0) sb.Append(',');
                sb.Append('[').Append(b0).Append(',').Append(b1).Append(',').Append(parte).Append(',').Append(Jornadas.AlaVista(js, b0, b1, parte)).Append(']');
            }
            // 1.6: "a la vista" con la regla del borde (la mitad de las consultas con la ultima vela a la vista)
            sb.Append("],\"borde\":[");
            for (int q = 0; q < 30; q++)
            {
                int n = horas.Count; if (n == 0) break;
                int ult = n - 1, w = 1 + r.Next(Math.Max(1, n / 2)), parte = r.Next(3);
                int b1 = q % 2 == 0 ? ult : r.Next(n), b0 = Math.Max(0, b1 - w);
                if (q > 0) sb.Append(',');
                sb.Append('[').Append(b0).Append(',').Append(b1).Append(',').Append(ult).Append(',').Append(parte).Append(',')
                  .Append(Jornadas.AlaVistaConBorde(js, b0, b1, ult, parte)).Append(']');
            }
            // 1.7: "al momento": la parte de jornada como estaba a la altura de una vela m cualquiera
            sb.Append("],\"momento\":[");
            for (int q = 0; q < 60; q++)
            {
                int n = horas.Count; if (n == 0) break;
                int m = r.Next(n), parte = r.Next(3);
                bool ok = Jornadas.AlMomento(js, m, parte, out int im, out int dm, out int hm);
                if (q > 0) sb.Append(',');
                sb.Append('[').Append(m).Append(',').Append(parte).Append(',').Append(ok ? im : -1).Append(',').Append(dm).Append(',').Append(hm).Append(']');
            }
            sb.Append("],\"atras\":[");
            for (int q = 0; q <= js.Count + 1; q++) { if (q > 0) sb.Append(','); sb.Append(Jornadas.PorAtras(js, q)); }
            sb.Append("]}");
        }
        sb.Append("]");
        File.WriteAllText(salida, sb.ToString());
        Console.WriteLine("jornadas: " + casos + " casos -> " + salida);
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
