using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using PatronOperador;

namespace PatronArnes
{
    /// <summary>
    /// EL ARNES: corre el MISMO PatronNucleo.cs que va adentro de ATAS, pero alimentado con la
    /// cinta grabada en CSV, para poder comparar senal por senal contra el Python.
    ///
    ///   dotnet run -c Release -- &lt;carpeta&gt; &lt;sesion&gt; [&lt;sesion&gt; ...]
    ///
    /// Lee cinta_&lt;sesion&gt;.csv y grilla_&lt;sesion&gt;.csv (los escribe r9_p_01_exportar.py) y deja
    /// cs_&lt;sesion&gt;.csv con los rasgos y la senal de cada regla en cada momento de la grilla.
    /// No toca ATAS ni nada de %APPDATA%.
    /// </summary>
    public static class Program
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("uso: PatronArnes <carpeta> <sesion> [<sesion> ...]");
                return 2;
            }
            bool vivo = args[0] == "--vivo";
            int i0 = vivo ? 1 : 0;
            if (args.Length < i0 + 2) { Console.Error.WriteLine("uso: PatronArnes [--vivo] <carpeta> <sesion> ..."); return 2; }
            string dir = args[i0];
            for (int a = i0 + 1; a < args.Length; a++)
            {
                try { if (vivo) Vivo(dir, args[a]); else Una(dir, args[a]); }
                catch (Exception e) { Console.Error.WriteLine(args[a] + ": " + e); return 1; }
            }
            return 0;
        }

        /// <summary>
        /// El CAMINO EN VIVO, tal cual corre adentro de ATAS: la grilla anclada al minuto UTC
        /// redondo, el motor sin solapes de 60 s y el tope de una marca por vela de 2 min y por
        /// regla. Sirve para saber, con numeros y no de memoria, cuantas flechas va a ver el
        /// operador por sesion y para dejar un CSV con la misma forma que el que graba el indicador.
        /// </summary>
        private static void Vivo(string dir, string sesion)
        {
            string fCinta = Path.Combine(dir, "cinta_" + sesion + ".csv");
            string fSal = Path.Combine(dir, "senales_" + sesion + ".csv");
            var nucleo = new PatronNucleo();
            var reglas = new[]
            {
                PatronNucleo.Regla.AgregaEnContra, PatronNucleo.Regla.NoPersiguePunta,
                PatronNucleo.Regla.NoPersigue, PatronNucleo.Regla.Persigue,
                PatronNucleo.Regla.PersigueExtremo
            };
            var motores = new Dictionary<PatronNucleo.Regla, PatronNucleo.Motor>();
            var cuenta = new Dictionary<PatronNucleo.Regla, int>();
            var dibujadas = new Dictionary<PatronNucleo.Regla, int>();
            var flancos = new Dictionary<PatronNucleo.Regla, int>();
            var ultLado = new Dictionary<PatronNucleo.Regla, int>();
            foreach (var r in reglas)
            {
                motores[r] = new PatronNucleo.Motor { TopeS = 60 };
                cuenta[r] = 0; dibujadas[r] = 0; flancos[r] = 0; ultLado[r] = 0;
            }
            var enVela = new HashSet<string>();

            var sb = new StringBuilder(1 << 20);
            sb.Append("utc,regla,lado,anti,dibujada,mid,bid,ask,spread_ticks,desbal_punta,"
                    + "ret_20s,ret_30s,ret_120s,pos_rango_60s,desbal_10,m2p_cuerpo,eventos_cinta,seg_cinta\n");

            long minuto = long.MinValue;
            int eventos = 0;
            using (var sr = new StreamReader(fCinta, Encoding.ASCII, false, 1 << 20))
            {
                string linea = sr.ReadLine();
                var campos = new string[16];
                while ((linea = sr.ReadLine()) != null)
                {
                    if (linea.Length == 0) continue;
                    if (Partir(linea, campos) < 9) continue;
                    long t = long.Parse(campos[0], Inv);
                    long m = (t / PatronNucleo.NS - 1) / 60;              // un segundo de gracia, igual que el indicador
                    while (minuto != long.MinValue && m > minuto)
                    {
                        minuto++;
                        Momento(sb, nucleo, reglas, motores, cuenta, dibujadas, flancos, ultLado, enVela, minuto * 60);
                    }
                    if (minuto == long.MinValue) minuto = m;
                    nucleo.Agregar(t, D(campos[1]), D(campos[2]), D(campos[3]), (int)D(campos[4]),
                                   D(campos[5]), D(campos[6]), D(campos[7]), D(campos[8]));
                    eventos++;
                }
            }
            File.WriteAllText(fSal, sb.ToString());
            Console.WriteLine(sesion + ": " + eventos + " eventos -> " + Path.GetFileName(fSal));
            foreach (var r in reglas)
                Console.WriteLine(string.Format(Inv,
                    "    {0,-17} senales {1,5}   una por vela {2,5}   solo cuando CAMBIA {3,5}",
                    r, cuenta[r], dibujadas[r], flancos[r]));
        }

        private static void Momento(StringBuilder sb, PatronNucleo nucleo, PatronNucleo.Regla[] reglas,
                                    Dictionary<PatronNucleo.Regla, PatronNucleo.Motor> motores,
                                    Dictionary<PatronNucleo.Regla, int> cuenta,
                                    Dictionary<PatronNucleo.Regla, int> dibujadas,
                                    Dictionary<PatronNucleo.Regla, int> flancos,
                                    Dictionary<PatronNucleo.Regla, int> ultLado,
                                    HashSet<string> enVela, long sec)
        {
            if (nucleo.Eventos < 50 || nucleo.SegundosDeCinta < PatronNucleo.SegCalentamiento) return;
            var x = nucleo.Evaluar(sec * PatronNucleo.NS);
            if (!x.Valido) return;
            long vela = sec / 120;
            foreach (var r in reglas)
            {
                int s = PatronNucleo.Dispara(r, in x);
                int antes = ultLado[r];
                ultLado[r] = (s == 2) ? 0 : s;
                if (s == 0 || s == 2) continue;
                if (!motores[r].Aceptar(sec)) continue;
                cuenta[r] = cuenta[r] + 1;
                bool cambio = s != antes;
                if (cambio) flancos[r] = flancos[r] + 1;
                bool dib = enVela.Add(r + "|" + s + "|" + vela);
                if (dib) dibujadas[r] = dibujadas[r] + 1;
                var ficha = PatronNucleo.Ficha(r);
                sb.Append(DateTimeOffset.FromUnixTimeSeconds(sec).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", Inv)).Append(',');
                sb.Append(r).Append(',').Append(s.ToString(Inv)).Append(',').Append(ficha.AntiSenal ? 1 : 0)
                  .Append(',').Append(dib ? 1 : 0).Append(',');
                C(sb, x.Mid); C(sb, x.Bid); C(sb, x.Ask); C(sb, x.SpreadTicks); C(sb, x.DesbalPunta);
                C(sb, x.Ret20); C(sb, x.Ret30); C(sb, x.Ret120); C(sb, x.PosRango60); C(sb, x.Desbal10);
                C(sb, x.M2pCuerpo);
                sb.Append(x.Eventos.ToString(Inv)).Append(',').Append(x.SegHistoria.ToString("0", Inv)).Append('\n');
            }
        }

        private static void C(StringBuilder sb, double v)
        {
            if (!double.IsNaN(v) && !double.IsInfinity(v)) sb.Append(v.ToString("0.######", Inv));
            sb.Append(',');
        }

        private static void Una(string dir, string sesion)
        {
            string fCinta = Path.Combine(dir, "cinta_" + sesion + ".csv");
            string fGrilla = Path.Combine(dir, "grilla_" + sesion + ".csv");
            string fSal = Path.Combine(dir, "cs_" + sesion + ".csv");

            var grilla = LeerGrilla(fGrilla);
            var nucleo = new PatronNucleo();
            var reglas = new[]
            {
                PatronNucleo.Regla.AgregaEnContra, PatronNucleo.Regla.NoPersiguePunta,
                PatronNucleo.Regla.NoPersigue, PatronNucleo.Regla.Persigue,
                PatronNucleo.Regla.PersigueExtremo
            };

            var sb = new StringBuilder(1 << 22);
            sb.Append("t,mid,ret20,ret30,ret120,dpunta,pos60,desbal10,m2pc");
            foreach (var r in reglas) sb.Append(',').Append(r.ToString());
            sb.Append('\n');

            int gi = 0, eventos = 0, escritas = 0;
            var t0 = DateTime.UtcNow;

            using (var sr = new StreamReader(fCinta, Encoding.ASCII, false, 1 << 20))
            {
                string linea = sr.ReadLine();          // cabecera
                var campos = new string[16];
                while ((linea = sr.ReadLine()) != null)
                {
                    if (linea.Length == 0) continue;
                    int n = Partir(linea, campos);
                    if (n < 9) continue;
                    long t = long.Parse(campos[0], Inv);

                    // todo momento de la grilla anterior o igual a este evento se evalua ANTES de
                    // meterlo: asi el nucleo tiene exactamente los eventos con T <= t_ref
                    while (gi < grilla.Count && grilla[gi] < t)
                    {
                        Fila(sb, nucleo, reglas, grilla[gi]); gi++; escritas++;
                    }

                    nucleo.Agregar(t, D(campos[1]), D(campos[2]), D(campos[3]), (int)D(campos[4]),
                                   D(campos[5]), D(campos[6]), D(campos[7]), D(campos[8]));
                    eventos++;

                    while (gi < grilla.Count && grilla[gi] == t)
                    {
                        Fila(sb, nucleo, reglas, grilla[gi]); gi++; escritas++;
                    }
                }
            }
            while (gi < grilla.Count) { Fila(sb, nucleo, reglas, grilla[gi]); gi++; escritas++; }

            File.WriteAllText(fSal, sb.ToString());
            Console.WriteLine(string.Format(Inv, "{0}: {1} eventos, {2} momentos -> {3} ({4:0.0} s)",
                sesion, eventos, escritas, Path.GetFileName(fSal), (DateTime.UtcNow - t0).TotalSeconds));
        }

        private static void Fila(StringBuilder sb, PatronNucleo nucleo, PatronNucleo.Regla[] reglas, long t)
        {
            var x = nucleo.Evaluar(t);
            sb.Append(t.ToString(Inv)).Append(',');
            N(sb, x.Mid); sb.Append(',');
            N(sb, x.Ret20); sb.Append(',');
            N(sb, x.Ret30); sb.Append(',');
            N(sb, x.Ret120); sb.Append(',');
            N(sb, x.DesbalPunta); sb.Append(',');
            N(sb, x.PosRango60); sb.Append(',');
            N(sb, x.Desbal10); sb.Append(',');
            N(sb, x.M2pCuerpo);
            foreach (var r in reglas) sb.Append(',').Append(PatronNucleo.Dispara(r, in x).ToString(Inv));
            sb.Append('\n');
        }

        private static void N(StringBuilder sb, double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return;
            sb.Append(v.ToString("R", Inv));
        }

        private static double D(string s)
            => (s == null || s.Length == 0) ? double.NaN : double.Parse(s, NumberStyles.Float, Inv);

        private static int Partir(string s, string[] campos)
        {
            int n = 0, ini = 0;
            for (int i = 0; i <= s.Length; i++)
            {
                if (i == s.Length || s[i] == ',')
                {
                    if (n < campos.Length) campos[n] = s.Substring(ini, i - ini);
                    n++; ini = i + 1;
                }
            }
            return n;
        }

        private static List<long> LeerGrilla(string ruta)
        {
            var l = new List<long>(2048);
            foreach (var s in File.ReadLines(ruta))
            {
                if (s.Length == 0 || s[0] == 't') continue;
                l.Add(long.Parse(s, Inv));
            }
            l.Sort();
            return l;
        }
    }
}
