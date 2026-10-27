// Seleccion.cs — PythiaGex 4.1, modulo Familia, CODIGO COMUN (B3a, 08-10-2026).
// La seleccion de niveles de la vista previa sobre un perfil YA en precio de ES (LibroMinuto o el perfil sumado de la familia):
//   MUROS  (extremos_rebote.f_muros, C6 strike exacto por lado): R = min(0.02*fut, radio del libro); D1 "muro C" = Fut[argmax C] entre
//          |Fut-fut|<=R y C>0; D2 "muro P" = Fut[argmin P] entre |Fut-fut|<=R y P<0. Las dos pueden caer del mismo lado del precio.
//   MAJORS (extremos_rebote.f_majors): igual con g = C + P neto: D1 "M+" argmax g>0, D2 "M-" argmin g<0.
//   ZTP    (backtest_familia.ztp + cruces, C3 sin islas, C4 sin histeresis): cruces de signo entre strikes vecinos con g != 0, sacando las
//          ISLAS (signo distinto a sus dos vecinos y |g| < 0.10 * min(|vecinos|), evaluado sobre la secuencia ORIGINAL, todas juntas);
//          z interpolado; solo |z - fut| <= radio del libro; D1 = min z > fut ("cruce arriba"), D2 = max z < fut ("cruce abajo").
//   ZEST   (backtest_familia.zest): una raya "Z" = el zero estandar (ZeroEstandarFam) si no es NaN y |z - fut| <= ZEST_MAX (75).
//   argmax/argmin: la PRIMERA ocurrencia en orden de Fut ascendente (np.argmax).
//   Monto (preview_niveles._monto): en el strike con |Fut - p| < 1e-6: MUROS D1 -> C, D2 -> P; MAJORS -> g; round(v * mult / 1e6, 1) en M USD
//          por 1 % (mult = Mult/100: ES 0.5, SPX y SPY 1; la familia ya viene escalada: 1). ZEST/ZTP/CONF: NaN.
// Las etiquetas crudas son las de Python ("D1", "D2", "Z"); RolesFam las traduce a "muro C", "M+", "0G est.", etc.
// 5.0 (27-10-2026): las distancias en puntos de NQ de la 4.1 pasadas a ES (DISENO_5_0 §4 + MEDICIONES_ES §1.3, P1_LIBRO_ES.md):
//   radio POR LIBRO (en la 4.1, 100 para todos): ES 25, SPX 20, SPY 20, familia (FAM y CONF) 20. El radio de NQ con R = 100 cubre la
//   misma fraccion de la gamma 0DTE que ES con ~24-26 y que SPX con 17 (volumen) / 20 (OI): el 0DTE del libro ES de Rithmic esta ~32 %
//   mas abierto que el de SPX (medido). La familia sumada pesa casi toda en dolares en SPX/SPY: va con el de ellos. ZEST_MAX 300 -> 75 (/4).
//   El 2 % del precio (154 pts con ES en 7.700) nunca manda: R es la constante del libro, como en la 4.1. Las ISLAS (0,10) son relativas:
//   no dependen de la escala ni del volumen absoluto (quedan). SIN VALIDAR en ES como nivel operable: esto solo fija la escala.
using System;
using System.Collections.Generic;

namespace PythiaGexCinco.Familia
{
    /// <summary>Un nivel crudo de la seleccion: precio en ES SIN redondear (CONF promedia sin redondear), etiqueta cruda y monto.</summary>
    public sealed class NivelFam
    {
        public double P; public string E; public double GexM = double.NaN;
        public NivelFam(double p, string e, double gexM = double.NaN) { P = p; E = e; GexM = gexM; }
        /// <summary>El Nivel del contrato: precio round(p, 2) de Python y la etiqueta cruda (o el rol si se pasa tipo).</summary>
        public Nivel ANivel(string tipo = null) => new Nivel(NumFam.PyRound(P, 2), tipo == null ? E : RolesFam.Rol(tipo, E), GexM);
        public override string ToString() => NumFam.F(P) + " " + E + (double.IsNaN(GexM) ? "" : " " + NumFam.F(GexM, "0.0"));
    }

    public static class SeleccionFam
    {
        // 5.0 (27-10-2026): backtest_familia.RADIO = 100 pts de NQ para todo (muros, majors, ZTP, CONF, top-N). En ES va POR LIBRO:
        public const double RADIO_ES = 25.0;      // libro ES de Rithmic: 100 / 4 (precio NQ/ES 3,82-3,86; cobertura medida k = 3,9-4,2); = radioDom ES de la 3.0
        public const double RADIO_CBOE = 20.0;    // SPX y SPY: cobertura medida SPX<->NDX k = 5,8 (vol, R 17) / 5,0 (OI, R 20): un solo 20 para las dos fuentes
        public const double RADIO_FAMILIA = 20.0; // FAM_MUROS y CONF: la suma en USD la mueven SPX/SPY (x100 y mucho mas OI que el libro ES)
        /// <summary>El radio de los argumentos sin libro (arneses y llamadas viejas): el del libro ES, el mas ancho. Por libro: RadioDe.</summary>
        public const double RADIO = RADIO_ES;
        public const double ISLA = 0.10;          // C3 (relativo: queda igual)
        public const double ZEST_MAX = 75.0;      // 5.0: ZEST se descarta a mas de 75 pts (4.1: 300 pts de NQ, /4)
        public const double FILTRO = 0.03;        // BM: strikes a <= 3 % de fut (relativo: queda igual)

        /// <summary>5.0 (27-10-2026): el radio de seleccion de cada libro, en puntos de ES. "familia" (FAM, CONF) y cualquier otro: ver arriba.</summary>
        public static double RadioDe(string libro)
        {
            switch (libro)
            {
                case "ES": return RADIO_ES;
                case "SPX": case "SPY": return RADIO_CBOE;
                case "familia": return RADIO_FAMILIA;
                default: return RADIO;
            }
        }

        /// <summary>R = min(fut * 0.02, radio) (extremos_rebote._R; 5.0: radio del libro, default el de ES).</summary>
        public static double R(double fut, double radio = RADIO) => Math.Min(fut * 0.02, radio);

        private static int Arg(double[] Fut, double[] v, double fut, double radio, int signo, bool mayor)
        {
            int best = -1; double bv = 0;
            for (int i = 0; i < Fut.Length; i++)
            {
                if (!(Math.Abs(Fut[i] - fut) <= radio)) continue;
                double x = v[i];
                if (signo > 0 ? !(x > 0) : !(x < 0)) continue;
                if (best < 0 || (mayor ? x > bv : x < bv)) { best = i; bv = x; }
            }
            return best;
        }

        /// <summary>MUROS: D1 = argmax C (C &gt; 0), D2 = argmin P (P &lt; 0), en R (5.0: radio del libro).</summary>
        public static List<NivelFam> Muros(double[] Fut, double[] C, double[] P, double fut, double radio = RADIO)
        {
            var o = new List<NivelFam>(2);
            if (Fut == null || Fut.Length == 0) return o;
            double r = R(fut, radio);
            int i = Arg(Fut, C, fut, r, +1, true);
            if (i >= 0) o.Add(new NivelFam(Fut[i], "D1"));
            int j = Arg(Fut, P, fut, r, -1, false);
            if (j >= 0) o.Add(new NivelFam(Fut[j], "D2"));
            return o;
        }

        /// <summary>MAJORS: D1 = argmax g (g &gt; 0), D2 = argmin g (g &lt; 0), en R (5.0: radio del libro).</summary>
        public static List<NivelFam> Majors(double[] Fut, double[] g, double fut, double radio = RADIO)
        {
            var o = new List<NivelFam>(2);
            if (Fut == null || Fut.Length == 0) return o;
            double r = R(fut, radio);
            int i = Arg(Fut, g, fut, r, +1, true);
            if (i >= 0) o.Add(new NivelFam(Fut[i], "D1"));
            int j = Arg(Fut, g, fut, r, -1, false);
            if (j >= 0) o.Add(new NivelFam(Fut[j], "D2"));
            return o;
        }

        /// <summary>4.1.7 (12-10-2026): TOP-N por |gamma neta| dentro del radio R, de mayor a menor.
        /// MUROS y MAJORS marcan UN strike por lado (argmax de calls, argmin de puts): en la 4.1 (NQ) habia 24 strikes con gamma
        /// en la ventana de +-100 pts y quedaban 22 sin marcar. Esta serie marca los N mas pesados, dinamicos:
        /// se recalculan cada minuto y se mueven con el libro. Etiquetas D1..DN por orden de peso.
        /// 5.0 (27-10-2026): N queda en 6 (es una CANTIDAD, no una distancia). Ojo: en ES el radio de 25 pts con strikes cada 5 tiene ~10
        /// strikes (medido 27-10 04:00 UTC: 10 con OI en +-25), contra ~24 de NQ en +-100: 6 son 60 % del radio, no 25 %. Ver P1_LIBRO_ES.md.</summary>
        public static int TopNCantidad = 6;

        public static List<NivelFam> TopN(double[] Fut, double[] g, double fut, int n, double radio = RADIO)
        {
            var o = new List<NivelFam>();
            if (Fut == null || Fut.Length == 0 || g == null) return o;
            double r = R(fut, radio);
            var idx = new List<int>();
            for (int i = 0; i < Fut.Length; i++)
                if (Math.Abs(Fut[i] - fut) <= r && g[i] != 0) idx.Add(i);
            if (idx.Count == 0) return o;
            idx.Sort((x, y) => { int c = Math.Abs(g[y]).CompareTo(Math.Abs(g[x])); return c != 0 ? c : x.CompareTo(y); });
            int tope = Math.Min(Math.Max(1, n), idx.Count);
            for (int j = 0; j < tope; j++) o.Add(new NivelFam(Fut[idx[j]], "D" + (j + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            return o;
        }

        public static List<NivelFam> TopN(LibroMinuto b, bool oi) =>
            b == null ? new List<NivelFam>() : TopN(b.Fut, oi ? b.Go : b.Gv, b.FutMnq, TopNCantidad, RadioDe(b.Libro));

        /// <summary>backtest_familia.cruces: [(z, i0, i1)] entre strikes vecinos con g != 0; con isla &gt; 0 y 3 o mas, se sacan las islas.</summary>
        public static List<(double Z, int I0, int I1)> Cruces(double[] Fut, double[] g, double isla)
        {
            var o = new List<(double, int, int)>();
            if (Fut == null || g == null) return o;
            var nz = new List<int>(g.Length);
            for (int i = 0; i < g.Length; i++) if (g[i] != 0) nz.Add(i);     // NaN != 0 es true en numpy y en C#: igual
            if (nz.Count < 2) return o;
            if (isla > 0 && nz.Count >= 3)
            {
                int n = nz.Count;
                var keep = new bool[n];
                for (int k = 0; k < n; k++) keep[k] = true;
                for (int k = 1; k < n - 1; k++)
                {
                    double a = g[nz[k - 1]], b = g[nz[k]], c = g[nz[k + 1]];
                    double sa = SignoNp(a), sb = SignoNp(b), sc = SignoNp(c);
                    bool mid = (sb != sa) && (sb != sc) && (Math.Abs(b) < isla * Math.Min(Math.Abs(a), Math.Abs(c)));
                    if (mid) keep[k] = false;
                }
                var nz2 = new List<int>(n);
                for (int k = 0; k < n; k++) if (keep[k]) nz2.Add(nz[k]);
                nz = nz2;
            }
            for (int k = 0; k + 1 < nz.Count; k++)
            {
                int i0 = nz[k], i1 = nz[k + 1];
                if (g[i0] * g[i1] < 0)
                    o.Add((Fut[i0] + (Fut[i1] - Fut[i0]) * (-g[i0]) / (g[i1] - g[i0]), i0, i1));
            }
            return o;
        }

        /// <summary>np.sign (NaN -> NaN; NaN != NaN es true como en numpy).</summary>
        private static double SignoNp(double x) => double.IsNaN(x) ? double.NaN : (x > 0 ? 1.0 : x < 0 ? -1.0 : 0.0);

        /// <summary>ZTP (C3 + C4): cruces sin islas a &lt;= radio de fut; D1 = el menor arriba, D2 = el mayor abajo (z == fut no cuenta).</summary>
        public static List<NivelFam> Ztp(double[] Fut, double[] g, double fut, double isla = ISLA, double radio = RADIO)
        {
            var o = new List<NivelFam>(2);
            double ar = double.NaN, ab = double.NaN;
            foreach (var (z, _, _) in Cruces(Fut, g, isla))
            {
                if (!(Math.Abs(z - fut) <= radio)) continue;
                if (z > fut) { if (double.IsNaN(ar) || z < ar) ar = z; }
                else if (z < fut) { if (double.IsNaN(ab) || z > ab) ab = z; }
            }
            if (!double.IsNaN(ar)) o.Add(new NivelFam(ar, "D1"));
            if (!double.IsNaN(ab)) o.Add(new NivelFam(ab, "D2"));
            return o;
        }

        /// <summary>ZEST: [(z, "Z")] si z no es NaN y |z - fut| &lt;= ZEST_MAX (5.0: 75; la 4.1, 300 de NQ).</summary>
        public static List<NivelFam> Zest(double z, double fut)
        {
            var o = new List<NivelFam>(1);
            if (!double.IsNaN(z) && Math.Abs(z - fut) <= ZEST_MAX) o.Add(new NivelFam(z, "Z"));
            return o;
        }

        /// <summary>preview_niveles._monto: v en el PRIMER strike con |Fut - p| &lt; 1e-6, round(v * mult / 1e6, 1); NaN si no hay.</summary>
        public static double Monto(double[] Fut, double[] v, double p, double mult)
        {
            if (Fut == null || v == null) return double.NaN;
            for (int i = 0; i < Fut.Length; i++)
                if (Math.Abs(Fut[i] - p) < 1e-6) return NumFam.PyRound(v[i] * mult / 1e6, 1);
            return double.NaN;
        }

        // ------------------------------------------------------------------ atajos sobre un LibroMinuto (5.0: con el radio de SU libro)
        public static List<NivelFam> Muros(LibroMinuto b, bool oi) => b == null ? new List<NivelFam>() : Muros(b.Fut, oi ? b.GoC : b.GvC, oi ? b.GoP : b.GvP, b.FutMnq, RadioDe(b.Libro));
        public static List<NivelFam> Majors(LibroMinuto b, bool oi) => b == null ? new List<NivelFam>() : Majors(b.Fut, oi ? b.Go : b.Gv, b.FutMnq, RadioDe(b.Libro));
        public static List<NivelFam> Ztp(LibroMinuto b, bool oi) => b == null ? new List<NivelFam>() : Ztp(b.Fut, oi ? b.Go : b.Gv, b.FutMnq, ISLA, RadioDe(b.Libro));
        public static List<NivelFam> Zest(LibroMinuto b, bool oi) => b == null ? new List<NivelFam>() : Zest(oi ? b.ZeroOi : b.ZeroVol, b.FutMnq);
    }

    /// <summary>Etiquetas de la vista previa (preview_niveles.ROLES).</summary>
    public static class RolesFam
    {
        public static string Rol(string tipo, string e)
        {
            switch (tipo)
            {
                case "MUROS": return e == "D1" ? "muro C" : e == "D2" ? "muro P" : e;
                case "MAJORS": return e == "D1" ? "M+" : e == "D2" ? "M-" : e;
                case "UNO": return e == "D1" ? "techo" : e == "D2" ? "piso" : e;
                case "ZTP": return e == "D1" ? "cruce arriba" : e == "D2" ? "cruce abajo" : e;
                case "ZEST": return e == "Z" ? "0G est." : e;
                case "TOPN": return e;
                case "CONF": return "conf";
                default: return e;      // TRES: D1/D2
            }
        }
    }
}
