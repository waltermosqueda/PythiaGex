using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using AbsorcionViva;

namespace AbsorcionArnes
{
    /// <summary>
    /// EL ARNES. Corre el MISMO AbsorcionNucleo.cs que va adentro de ATAS, pero alimentado con la
    /// cinta grabada en CSV, para poder compararlo evento por evento contra el Python que produjo
    /// las mediciones (r10_f4_lib.py y r10_f6_lib.py).
    ///
    ///   AbsorcionArnes censo    &lt;cinta.csv&gt; &lt;salida.csv&gt; [--umbral 100] [--aguante 40]
    ///                           [--visible 3] [--piso 25] [--g 4] [--h 60]
    ///   AbsorcionArnes atrapado &lt;cinta.csv&gt; &lt;salida.csv&gt; [--vs 300] [--m 16] [--d 30] [--w 2]
    ///   AbsorcionArnes eventos  &lt;cinta.csv&gt; &lt;salida.csv&gt;   (los defaults de fabrica del indicador)
    ///   AbsorcionArnes franja   &lt;desde-epoch-seg&gt; &lt;hasta&gt; &lt;paso&gt; &lt;salida.csv&gt;
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
                    case "censo": return Censo(args);
                    case "atrapado": return Atrapado(args);
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
            Console.Error.WriteLine("uso: AbsorcionArnes censo|atrapado|eventos <cinta.csv> <salida.csv> [opciones]");
            Console.Error.WriteLine("     AbsorcionArnes franja <desde_seg> <hasta_seg> <paso_seg> <salida.csv>");
        }

        private static double Opt(string[] a, string nombre, double porDefecto)
        {
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == "--" + nombre) return double.Parse(a[i + 1], Inv);
            return porDefecto;
        }

        // ================================================================== censo de episodios
        private static int Censo(string[] args)
        {
            var nu = new NucleoNivel
            {
                Tick = 0.25,
                MuerteTicks = (int)Opt(args, "g", 4),
                MuerteSeg = Opt(args, "h", 60),
                UmbralRueda = Opt(args, "umbral", 100),
                PartirPorFranja = false,                 // el Python compara con UN umbral fijo
                IcebergAguante = Opt(args, "aguante", 40),
                IcebergVisibleMin = Opt(args, "visible", 3),
                PisoCenso = Opt(args, "piso", 25),
                PrenderAbsorcion = true,
                PrenderIceberg = true
            };
            nu.UmbralFuera = nu.UmbralRueda;

            long filas = Recorrer(args[1], nu);
            nu.Vaciar();

            var ep = new List<NucleoNivel.Episodio>(nu.Censo);
            ep.Sort(delegate (NucleoNivel.Episodio x, NucleoNivel.Episodio y)
            {
                int c = x.TIni.CompareTo(y.TIni); if (c != 0) return c;
                c = x.LadoAbs.CompareTo(y.LadoAbs); if (c != 0) return c;
                return x.PxTk.CompareTo(y.PxTk);
            });

            var sb = new StringBuilder(1 << 22);
            sb.Append("lado_abs,t_ini,t_fin,i_ini,i_fin,px_tk,tam_ini,comido,rep_cont,rep_vuelta,")
              .Append("rep_otro,n_rep_cont,n_vuelta,n_filas,f_abs,f_ice\n");
            for (int i = 0; i < ep.Count; i++)
            {
                var e = ep[i];
                sb.Append(e.LadoAbs).Append(',').Append(e.TIni).Append(',').Append(e.TFin).Append(',')
                  .Append(e.IIni).Append(',').Append(e.IFin).Append(',').Append(e.PxTk).Append(',');
                D(sb, e.TamIni); D(sb, e.Comido); D(sb, e.RepCont); D(sb, e.RepVuelta); D(sb, e.RepOtro);
                sb.Append(e.NRepCont).Append(',').Append(e.NVuelta).Append(',').Append(e.NFilas).Append(',')
                  .Append(e.Fire0).Append(',').Append(e.Fire1).Append('\n');
            }
            File.WriteAllText(args[2], sb.ToString());
            Console.WriteLine(string.Format(Inv, "censo: {0} filas de cinta -> {1} episodios (piso {2}) en {3}",
                filas, ep.Count, nu.PisoCenso, Path.GetFileName(args[2])));
            return 0;
        }

        // ================================================================== agresor atrapado
        private static int Atrapado(string[] args)
        {
            var nu = new NucleoAtrapado
            {
                Tick = 0.25,
                VolMin = Opt(args, "vs", 300),
                TicksMin = Opt(args, "m", 16),
                EsperaSeg = (int)Opt(args, "d", 30),
                HuecoSeg = Opt(args, "w", 2),
                Prendido = true
            };
            var sal = new List<Evento>();
            long filas = Recorrer(args[1], nu, sal);
            sal.Sort(Por);

            var sb = new StringBuilder(1 << 18);
            sb.Append("t_d_seg,lado,px_tk,precio,vol_tot,ticks_mov,espera_s,t_ini\n");
            for (int i = 0; i < sal.Count; i++)
            {
                var e = sal[i];
                sb.Append(e.Ns / 1000000000L).Append(',').Append(e.Lado).Append(',').Append(e.PrecioTk).Append(',');
                D(sb, e.Precio); D(sb, e.Tamano); D(sb, e.Ticks);
                sb.Append((long)e.EsperaSeg).Append(',').Append(e.NsInicio).Append('\n');
            }
            File.WriteAllText(args[2], sb.ToString());
            Console.WriteLine(string.Format(Inv, "atrapado: {0} filas de cinta -> {1} eventos en {2}",
                filas, sal.Count, Path.GetFileName(args[2])));
            return 0;
        }

        // ================================================================== lo que ve el operador
        private static int Eventos(string[] args)
        {
            var nivel = new NucleoNivel();                       // defaults de fabrica
            var atrap = new NucleoAtrapado();
            atrap.Prendido = Opt(args, "atrapado", 0) > 0;
            var bat = new Bateria();
            bat.Agregar(nivel);
            bat.Agregar(atrap);

            var sal = new List<Evento>();
            long filas = Recorrer(args[1], bat, sal);
            sal.Sort(Por);

            var sb = new StringBuilder(1 << 20);
            sb.Append("tipo,nombre,t_ns,t_utc,px_tk,precio,lado,tam_encendio,tam_final,visto_inicial,")
              .Append("aguante,filas,ticks,espera_s,en_rueda\n");
            for (int i = 0; i < sal.Count; i++)
            {
                var e = sal[i];
                sb.Append(e.Tipo).Append(',').Append(Tipos.Nombre(e.Tipo)).Append(',').Append(e.Ns).Append(',')
                  .Append(Reloj.De(e.Ns).ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv)).Append(',')
                  .Append(e.PrecioTk).Append(',');
                D(sb, e.Precio);
                sb.Append(e.Lado).Append(',');
                D(sb, e.TamanoAlEncender); D(sb, e.Tamano); D(sb, e.VistoInicial); D(sb, e.Aguante);
                sb.Append(e.Filas).Append(',');
                D(sb, e.Ticks);
                sb.Append((long)e.EsperaSeg).Append(',').Append(Franja.EnRueda(e.Ns) ? 1 : 0).Append('\n');
            }
            File.WriteAllText(args[2], sb.ToString());
            int n0 = 0, n1 = 0, n2 = 0;
            for (int i = 0; i < sal.Count; i++)
            {
                if (sal[i].Tipo == Tipos.AbsorcionNivel) n0++;
                else if (sal[i].Tipo == Tipos.Iceberg) n1++;
                else n2++;
            }
            Console.WriteLine(string.Format(Inv,
                "eventos: {0} filas -> absorcion_nivel {1}, iceberg {2}, atrapado {3} en {4}",
                filas, n0, n1, n2, Path.GetFileName(args[2])));
            return 0;
        }

        private static int Por(Evento a, Evento b)
        {
            int c = a.Ns.CompareTo(b.Ns); if (c != 0) return c;
            c = a.Tipo.CompareTo(b.Tipo); if (c != 0) return c;
            c = a.PrecioTk.CompareTo(b.PrecioTk); if (c != 0) return c;
            return a.Lado.CompareTo(b.Lado);
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

        /// <summary>yyyy-MM-ddTHH:mm:ss.fff -> nanosegundos UTC. Parseo a mano: son 30 millones de
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
