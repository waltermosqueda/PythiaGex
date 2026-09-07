using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PythiaGex
{
    /// <summary>
    /// REBOBINA: Gamma Hoy sobre dias pasados, afuera de ATAS.
    ///
    /// Lee velas del futuro (CSV de Databento) y cadenas por minuto (el mismo
    /// formato que archiva la nube y que baja el indicador), y para cada vela
    /// cerrada hace exactamente lo que hace el indicador en vivo: toma la
    /// ultima cadena que se tenia al cierre de la vela, el cierre como precio
    /// del futuro, y llama a GammaHoyNucleo.Calcular. Lo que sale se anota con
    /// Centinela, en el mismo archivo y formato que usa ATAS, con el prefijo
    /// "rebobinado-": los scripts del laboratorio corren sin cambiar una linea.
    ///
    /// Uso:
    ///   Rebobina --cadenas datos/simulador/cadenas/sim-ES-2026-09-03-r902.jsonl.gz
    ///            --velas datos/simulador/velas/ESU6-1m.csv
    ///            [--instrumento MES] [--marco M1] [--horizonte Hoy|Semana|Todo]
    ///            [--nombre rebobinado] [--desde 2026-09-03T13:30Z] [--hasta 2026-09-03T21:00Z]
    ///            [--marco-min 1] [--audit 30]
    /// </summary>
    internal static class Program
    {
        private sealed class Vela { public DateTime T; public double O, H, L, C, V; }

        private static string Arg(string[] a, string k, string def = null)
        {
            for (int i = 0; i + 1 < a.Length; i++) if (a[i] == k) return a[i + 1];
            return def;
        }

        private static int Main(string[] args)
        {
            var inv = CultureInfo.InvariantCulture;
            // --prueba <radar.json> --precio 7709: una sola cuenta sobre una cadena
            // del feed, para comparar con la linea AUDIT que dejo el indicador en
            // ATAS con la misma cadena y el mismo precio (equivalencia del nucleo).
            if (Arg(args, "--prueba") != null)
            {
                var c = Feed.Parsear(File.ReadAllText(Arg(args, "--prueba")));
                if (c == null) { Console.Error.WriteLine("no pude parsear la cadena"); return 2; }
                var n = new GammaHoyNucleo();
                n.A.Horizonte = Enum.Parse<GammaHoyNucleo.HorizonteVenc>(Arg(args, "--horizonte", "Hoy"), true);
                var L = n.Calcular(c, double.Parse(Arg(args, "--precio"), inv), DateTime.UtcNow);
                Console.WriteLine(L == null ? "sin lectura" : GammaHoyNucleo.Audit(L, c, false));
                return 0;
            }
            var rutasCadenas = (Arg(args, "--cadenas") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
            var rutaVelas = Arg(args, "--velas");
            if (rutasCadenas.Length == 0 || rutaVelas == null)
            {
                Console.Error.WriteLine("falta --cadenas y/o --velas");
                return 2;
            }
            string instrumento = Arg(args, "--instrumento", "MES");
            int marcoMin = int.Parse(Arg(args, "--marco-min", "1"), inv);
            string marco = Arg(args, "--marco", "M" + marcoMin);
            string nombre = Arg(args, "--nombre", "rebobinado");
            int auditCada = int.Parse(Arg(args, "--audit", "30"), inv);
            var nucleo = new GammaHoyNucleo();
            nucleo.A.Horizonte = Enum.Parse<GammaHoyNucleo.HorizonteVenc>(Arg(args, "--horizonte", "Hoy"), true);
            nucleo.A.CuantasDominantes = int.Parse(Arg(args, "--dominantes", "2"), inv);
            nucleo.A.Tasa = double.Parse(Arg(args, "--tasa", "0.0375"), inv);
            nucleo.A.Convexidad = Enum.Parse<GammaHoyNucleo.LibroConv>(Arg(args, "--convexidad", "Auto"), true);

            // ---- cadenas
            var cadenas = new List<Feed.Cadena>();
            foreach (var r in rutasCadenas)
            {
                if (!File.Exists(r)) { Console.Error.WriteLine("no existe " + r); return 2; }
                var ls = Feed.Archivo.Leer(r);
                Console.WriteLine("cadenas: " + Path.GetFileName(r) + " -> " + ls.Count + " (" + (ls.Count > 0 ? ls[0].GeneradoUtc.ToString("yyyy-MM-dd HH:mm") + " a " + ls[^1].GeneradoUtc.ToString("HH:mm") + " UTC" : "vacio") + ")");
                cadenas.AddRange(ls);
            }
            cadenas.Sort((x, y) => x.GeneradoUtc.CompareTo(y.GeneradoUtc));
            if (cadenas.Count == 0) { Console.Error.WriteLine("sin cadenas"); return 2; }

            // ---- velas (CSV: ts_event,open,high,low,close,volume) agrupadas al marco pedido
            var velas1 = new List<Vela>();
            foreach (var l in File.ReadLines(rutaVelas).Skip(1))
            {
                var p = l.Split(',');
                if (p.Length < 6) continue;
                if (!DateTime.TryParse(p[0], inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)) continue;
                velas1.Add(new Vela { T = t, O = double.Parse(p[1], inv), H = double.Parse(p[2], inv), L = double.Parse(p[3], inv), C = double.Parse(p[4], inv), V = double.Parse(p[5], inv) });
            }
            velas1.Sort((x, y) => x.T.CompareTo(y.T));
            var desde = Arg(args, "--desde") != null ? DateTime.Parse(Arg(args, "--desde"), inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal) : cadenas[0].GeneradoUtc;
            var hasta = Arg(args, "--hasta") != null ? DateTime.Parse(Arg(args, "--hasta"), inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal) : cadenas[^1].GeneradoUtc.AddMinutes(marcoMin);
            var velas = Agrupar(velas1.Where(v => v.T >= desde.AddMinutes(-marcoMin) && v.T < hasta).ToList(), marcoMin);
            Console.WriteLine("velas: " + velas.Count + " de " + marcoMin + " min entre " + desde.ToString("yyyy-MM-dd HH:mm") + " y " + hasta.ToString("yyyy-MM-dd HH:mm") + " UTC");
            if (velas.Count == 0) return 2;

            // ---- el centinela, limpio: este archivo es del simulador
            var cent = new Centinela(nombre + "-" + instrumento, "TimeFrame-" + marco);
            var rutaCent = RutaCentinela(nombre + "-" + instrumento, "TimeFrame-" + marco);
            try { if (File.Exists(rutaCent)) File.Delete(rutaCent); } catch { }

            int iC = 0, conCadena = 0, sinCadena = 0;
            DateTime diaAnterior = DateTime.MinValue;
            for (int k = 0; k < velas.Count; k++)
            {
                var v = velas[k];
                var cierreT = v.T.AddMinutes(marcoMin);          // la vela cierra aca
                if (v.T.Date != diaAnterior) { nucleo.Reiniciar(); diaAnterior = v.T.Date; }
                while (iC + 1 < cadenas.Count && cadenas[iC + 1].GeneradoUtc <= cierreT) iC++;
                var cad = cadenas[iC];
                if (cad.GeneradoUtc > cierreT || (cierreT - cad.GeneradoUtc).TotalHours > 6)
                {
                    sinCadena++;
                    continue;
                }
                var L = nucleo.Calcular(cad, v.C, cierreT);
                if (L == null || L.SinBase) { sinCadena++; continue; }
                conCadena++;
                if (L.TransicionNueva) Console.WriteLine("  " + cierreT.ToString("HH:mm") + " TRANSICION " + L.Alerta);
                cent.Anotar(k, cierreT, v.O, v.H, v.L, v.C, v.V, 0, 0, L.S, GammaHoyNucleo.Niveles(L));
                if (auditCada > 0 && k % auditCada == 0)
                    Console.WriteLine("  " + cierreT.ToString("HH:mm") + " " + GammaHoyNucleo.Audit(L, cad, false));
            }
            cent.Volcar(true);
            Console.WriteLine("listo: " + conCadena + " velas con cadena, " + sinCadena + " sin. Centinela: " + rutaCent);
            return 0;
        }

        private static List<Vela> Agrupar(List<Vela> v1, int min)
        {
            if (min <= 1) return v1;
            var salida = new List<Vela>();
            Vela cur = null; DateTime cubo = DateTime.MinValue;
            foreach (var v in v1)
            {
                var c = new DateTime(v.T.Year, v.T.Month, v.T.Day, v.T.Hour, v.T.Minute - v.T.Minute % min, 0, DateTimeKind.Utc);
                if (cur == null || c != cubo)
                {
                    cur = new Vela { T = c, O = v.O, H = v.H, L = v.L, C = v.C, V = v.V };
                    salida.Add(cur); cubo = c;
                }
                else { cur.H = Math.Max(cur.H, v.H); cur.L = Math.Min(cur.L, v.L); cur.C = v.C; cur.V += v.V; }
            }
            return salida;
        }

        /// <summary>La misma ruta que arma Centinela (copiada de su Limpiar), para
        /// borrar el archivo antes de correr y no acumular corridas viejas.</summary>
        private static string RutaCentinela(string instrumento, string marco)
        {
            string Limpiar(string s)
            {
                if (string.IsNullOrEmpty(s)) return "x";
                var sb = new System.Text.StringBuilder();
                foreach (var ch in s) sb.Append(char.IsLetterOrDigit(ch) ? ch : '-');
                return sb.ToString();
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS",
                                "pythiagex-centinela-" + Limpiar(instrumento) + "-" + Limpiar(marco) + ".jsonl");
        }
    }
}
