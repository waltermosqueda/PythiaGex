using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using DeltaVivo;

namespace DeltaArnes
{
    /// <summary>
    /// EL ARNES. Corre el MISMO DeltaNucleo.cs que va adentro de ATAS, pero alimentado con la
    /// cinta grabada en CSV, para poder compararlo evento por evento contra el Python que produjo
    /// las mediciones (r11_f3_lib, r11_f6_lib y r11_dib_04_anclaje.rango_movil).
    ///
    ///   DeltaArnes eventos &lt;cinta.csv&gt; &lt;salida.csv&gt; [--t 10] [--ur 1500] [--uf 800]
    ///                      [--pura 0.7] [--cpie 2] [--tr 800] [--tf 400] [--trango 8]
    ///                      [--nr 400] [--nf 250] [--conc 0.5] [--nrango 0] [--tick 0.25]
    ///   DeltaArnes franja  &lt;desde-epoch-seg&gt; &lt;hasta&gt; &lt;paso&gt; &lt;salida.csv&gt;
    ///
    /// La prueba de la franja se vuelve a correr AUNQUE ya se corrio en la ronda 10: Franja y
    /// Reloj estan COPIADOS de AbsorcionNucleo.cs, y una copia que nadie vuelve a probar es una
    /// copia que un dia se separa del original sin que nadie se entere.
    ///
    /// NO toca ATAS, no escribe en %APPDATA% y no usa red. Lee el CSV crudo (NO un export
    /// intermedio) a proposito: en la ronda 9 la unica diferencia de paridad que aparecio la
    /// producia el exportador, que escribia 6 cifras significativas con precios de 7.
    /// </summary>
    public static class Program
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static int Main(string[] args)
        {
            if (args.Length < 3) { Uso(); return 2; }
            try
            {
                switch (args[0])
                {
                    case "eventos": return Eventos(args);
                    case "franja": return FranjaCsv(args);
                }
            }
            catch (Exception e) { Console.Error.WriteLine("ERROR: " + e); return 1; }
            Uso();
            return 2;
        }

        private static void Uso()
        {
            Console.Error.WriteLine("uso: DeltaArnes eventos <cinta.csv> <salida.csv> [opciones]");
            Console.Error.WriteLine("     DeltaArnes franja <desde_seg> <hasta_seg> <paso_seg> <salida.csv>");
        }

        private static double Opt(string[] a, string nombre, double porDefecto)
        {
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == "--" + nombre) return double.Parse(a[i + 1], Inv);
            return porDefecto;
        }

        // ================================================================== los eventos
        private static int Eventos(string[] args)
        {
            var nu = new NucleoDelta
            {
                Tick = Opt(args, "tick", 0.25),
                VentanaSeg = Opt(args, "t", 10),
                OlaVaraRueda = Opt(args, "ur", 1500),
                OlaVaraFuera = Opt(args, "uf", 800),
                PartirPorFranja = true,
                RazonPura = Opt(args, "pura", 0.70),
                ContrapieTicks = Opt(args, "cpie", 2),
                TrabaVaraRueda = Opt(args, "tr", 800),
                TrabaVaraFuera = Opt(args, "tf", 400),
                TrabaRangoTicks = Opt(args, "trango", 8),
                NidoVaraRueda = Opt(args, "nr", 400),
                NidoVaraFuera = Opt(args, "nf", 250),
                NidoConcMin = Opt(args, "conc", 0.50),
                NidoRangoTicks = Opt(args, "nrango", 0),
                PrenderOla = true,
                PrenderContrapie = true,
                PrenderTraba = true,
                PrenderNido = true
            };

            var sal = new List<Evento>();
            long filas = Recorrer(args[1], nu, sal);
            sal.Sort(Por);

            var sb = new StringBuilder(1 << 20);
            sb.Append("tipo,familia,t_ns,t_utc,sec,px_tk,precio,lado,tam_encendio,tam_final,")
              .Append("volumen,razon,pura,avance_tk,rango_tk,vara,filas,en_rueda,t_ini_ns,t_cierre_ns,id\n");
            for (int i = 0; i < sal.Count; i++)
            {
                var e = sal[i];
                sb.Append(e.Tipo).Append(',').Append(Tipos.Nombre(e.Tipo)).Append(',').Append(e.Ns).Append(',')
                  .Append(Reloj.De(e.Ns).ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv)).Append(',')
                  .Append(e.Ns / 1000000000L).Append(',')
                  .Append(e.PrecioTk).Append(',');
                D(sb, e.Precio);
                sb.Append(e.Lado).Append(',');
                D(sb, e.TamanoAlEncender); D(sb, e.Tamano); D(sb, e.Volumen); D(sb, e.Razon);
                sb.Append(e.Pura ? 1 : 0).Append(',');
                D(sb, e.Avance); D(sb, e.Rango); D(sb, e.Vara);
                sb.Append(e.Filas).Append(',').Append(e.EnRueda ? 1 : 0).Append(',')
                  .Append(e.NsInicio).Append(',').Append(e.NsCierre).Append(',')
                  .Append(e.IdEpisodio).Append('\n');
            }
            File.WriteAllText(args[2], sb.ToString());
            int nOla = 0, nPura = 0, nCp = 0, nTr = 0, nNi = 0;
            for (int i = 0; i < sal.Count; i++)
            {
                if (sal[i].Tipo == Tipos.Ola) { nOla++; if (sal[i].Pura) nPura++; }
                else if (sal[i].Tipo == Tipos.Contrapie) nCp++;
                else if (sal[i].Tipo == Tipos.Traba) nTr++;
                else nNi++;
            }
            Console.WriteLine(string.Format(Inv,
                "eventos: {0} filas -> ola {1} (puras {2}), contrapie {3}, traba {4}, nido {5} en {6}",
                filas, nOla, nPura, nCp, nTr, nNi, Path.GetFileName(args[2])));
            return 0;
        }

        /// <summary>Orden TOTAL, sin empates posibles: dos filas de cinta pueden compartir el
        /// milisegundo, asi que ordenar solo por hora dejaria el desempate en manos del algoritmo
        /// de List.Sort (que no es estable) y la paridad compararia filas distintas por nada. El
        /// tercer criterio es el numero corrido del evento adentro de su familia, que es unico.</summary>
        private static int Por(Evento a, Evento b)
        {
            int c = a.Ns.CompareTo(b.Ns); if (c != 0) return c;
            c = a.Tipo.CompareTo(b.Tipo); if (c != 0) return c;
            return a.IdEpisodio.CompareTo(b.IdEpisodio);
        }

        // ================================================================== la franja de Nueva York
        private static int FranjaCsv(string[] args)
        {
            long d = long.Parse(args[1], Inv), h = long.Parse(args[2], Inv), p = long.Parse(args[3], Inv);
            var sb = new StringBuilder(1 << 22);
            sb.Append("seg,minuto_ny,en_rueda\n");
            for (long s = d; s <= h; s += p)
            {
                long ns = s * 1000000000L;
                sb.Append(s).Append(',').Append(Franja.MinutoNy(ns)).Append(',')
                  .Append(Franja.EnRueda(ns) ? 1 : 0).Append('\n');
            }
            File.WriteAllText(args[4], sb.ToString());
            Console.WriteLine("franja: " + ((h - d) / p + 1) + " instantes -> " + Path.GetFileName(args[4]));
            return 0;
        }

        // ================================================================== el lector de la cinta
        /// <summary>Lee el CSV crudo de la sonda y se lo pasa al nucleo fila por fila, en el mismo
        /// orden en que llegaron. Verifica que la cinta este ordenada: si no lo estuviera, el
        /// Python (que hace un sort estable) y esto verian secuencias distintas y la paridad no
        /// significaria nada.</summary>
        private static long Recorrer(string ruta, IFuenteDeEventos fuente, List<Evento> sal = null)
        {
            long filas = 0, desorden = 0, tAnt = long.MinValue;
            var o = new Orden();
            var campos = new int[16];
            using (var sr = new StreamReader(ruta, Encoding.ASCII, false, 1 << 20))
            {
                string linea = sr.ReadLine();                     // cabecera
                if (linea == null || !linea.StartsWith("t,primero,ultimo,vol,lado,prints,abid"))
                    throw new Exception("cabecera inesperada en " + ruta + ": " + linea);
                while ((linea = sr.ReadLine()) != null)
                {
                    if (linea.Length < 20) continue;
                    int n = Partir(linea, campos);
                    if (n < 14) continue;
                    o.Ns = Hora(linea, campos[0], campos[1] - 1);
                    o.Primero = C(linea, campos, 1);
                    o.Ultimo = C(linea, campos, 2);
                    o.Vol = C(linea, campos, 3);
                    o.Lado = (int)C(linea, campos, 4);
                    o.Prints = (int)C(linea, campos, 5);
                    o.ABid = C(linea, campos, 6); o.ABidV = C(linea, campos, 7);
                    o.AAsk = C(linea, campos, 8); o.AAskV = C(linea, campos, 9);
                    o.DBid = C(linea, campos, 10); o.DBidV = C(linea, campos, 11);
                    o.DAsk = C(linea, campos, 12); o.DAskV = C(linea, campos, 13);
                    if (o.Ns < tAnt) desorden++;
                    tAnt = o.Ns;
                    fuente.Agregar(in o);
                    var nv = fuente.Nuevos;
                    if (sal != null) for (int k = 0; k < nv.Count; k++) sal.Add(nv[k]);
                    filas++;
                }
            }
            if (desorden > 0)
                Console.Error.WriteLine("AVISO: " + desorden + " filas fuera de orden en " + Path.GetFileName(ruta));
            return filas;
        }

        /// <summary>Guarda en `pos` el arranque de cada campo. Devuelve cuantos hay. `pos[n]` queda
        /// con el largo de la linea + 1, para poder calcular el largo del ultimo campo.</summary>
        private static int Partir(string s, int[] pos)
        {
            int n = 0;
            pos[n++] = 0;
            for (int i = 0; i < s.Length && n < pos.Length; i++)
                if (s[i] == ',') pos[n++] = i + 1;
            if (n < pos.Length) pos[n] = s.Length + 1;
            return n;
        }

        private static double C(string s, int[] pos, int k)
        {
            int a = pos[k];
            int b = (k + 1 < pos.Length ? pos[k + 1] : s.Length + 1) - 1;
            if (b > s.Length) b = s.Length;
            if (b <= a) return double.NaN;                        // campo vacio = punta que no vino
            return double.Parse(s.AsSpan(a, b - a), NumberStyles.Float, Inv);
        }

        /// <summary>yyyy-MM-ddTHH:mm:ss.fff -> nanosegundos UTC. Parseo a mano: son millones de
        /// filas y DateTime.Parse cuesta diez veces mas.</summary>
        private static long Hora(string s, int a, int b)
        {
            int anio = Ent(s, a, 4), mes = Ent(s, a + 5, 2), dia = Ent(s, a + 8, 2);
            int hh = Ent(s, a + 11, 2), mi = Ent(s, a + 14, 2), ss = Ent(s, a + 17, 2);
            long frac = 0;
            if (b > a + 19 && s[a + 19] == '.')
            {
                int d = b - (a + 20);
                if (d > 7) d = 7;
                long v = 0;
                for (int i = 0; i < d; i++) v = v * 10 + (s[a + 20 + i] - '0');
                for (int i = d; i < 7; i++) v *= 10;               // a decimas de microsegundo
                frac = v;
            }
            var t = new DateTime(anio, mes, dia, hh, mi, ss, DateTimeKind.Utc);
            return Reloj.Ns(t) + frac * 100L;
        }

        private static int Ent(string s, int a, int n)
        {
            int v = 0;
            for (int i = 0; i < n; i++) v = v * 10 + (s[a + i] - '0');
            return v;
        }

        /// <summary>Numeros con TODAS las cifras. La unica diferencia de paridad de la ronda 9 la
        /// producia un exportador que escribia 6 cifras significativas con precios de 7.</summary>
        private static void D(StringBuilder sb, double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) sb.Append(',');
            else sb.Append(v.ToString("R", Inv)).Append(',');
        }
    }
}
