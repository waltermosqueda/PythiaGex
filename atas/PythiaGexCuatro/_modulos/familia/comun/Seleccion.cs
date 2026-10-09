// Seleccion.cs — PythiaGex 4.1, modulo Familia, CODIGO COMUN (B3a, 08-10-2026).
// La seleccion de niveles de la vista previa sobre un perfil YA en precio de NQ (LibroMinuto o el perfil sumado de la familia):
//   MUROS  (extremos_rebote.f_muros, C6 strike exacto por lado): R = min(0.02*fut, 100); D1 "muro C" = Fut[argmax C] entre |Fut-fut|<=R y C>0;
//          D2 "muro P" = Fut[argmin P] entre |Fut-fut|<=R y P<0. Las dos pueden caer del mismo lado del precio.
//   MAJORS (extremos_rebote.f_majors): igual con g = C + P neto: D1 "M+" argmax g>0, D2 "M-" argmin g<0.
//   ZTP    (backtest_familia.ztp + cruces, C3 sin islas, C4 sin histeresis): cruces de signo entre strikes vecinos con g != 0, sacando las
//          ISLAS (signo distinto a sus dos vecinos y |g| < 0.10 * min(|vecinos|), evaluado sobre la secuencia ORIGINAL, todas juntas);
//          z interpolado; solo |z - fut| <= 100; D1 = min z > fut ("cruce arriba"), D2 = max z < fut ("cruce abajo").
//   ZEST   (backtest_familia.zest): una raya "Z" = el zero estandar (ZeroEstandarFam) si no es NaN y |z - fut| <= 300.
//   argmax/argmin: la PRIMERA ocurrencia en orden de Fut ascendente (np.argmax).
//   Monto (preview_niveles._monto): en el strike con |Fut - p| < 1e-6: MUROS D1 -> C, D2 -> P; MAJORS -> g; round(v * mult / 1e6, 1) en M USD
//          por 1 % (mult = Mult/100: NQ 0.2, NDX y QQQ 1; la familia ya viene escalada: 1). ZEST/ZTP/CONF: NaN.
// Las etiquetas crudas son las de Python ("D1", "D2", "Z"); RolesFam las traduce a "muro C", "M+", "0G est.", etc.
using System;
using System.Collections.Generic;

namespace PythiaGexCuatro.Familia
{
    /// <summary>Un nivel crudo de la seleccion: precio en NQ SIN redondear (CONF promedia sin redondear), etiqueta cruda y monto.</summary>
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
        public const double RADIO = 100.0;        // backtest_familia.RADIO (ZTP y CONF)
        public const double ISLA = 0.10;          // C3
        public const double ZEST_MAX = 300.0;     // ZEST se descarta a mas de 300 pts
        public const double FILTRO = 0.03;        // BM: strikes a <= 3 % de fut

        /// <summary>R = min(fut * 0.02, 100) (extremos_rebote._R).</summary>
        public static double R(double fut) => Math.Min(fut * 0.02, 100.0);

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

        /// <summary>MUROS: D1 = argmax C (C &gt; 0), D2 = argmin P (P &lt; 0), en R.</summary>
        public static List<NivelFam> Muros(double[] Fut, double[] C, double[] P, double fut)
        {
            var o = new List<NivelFam>(2);
            if (Fut == null || Fut.Length == 0) return o;
            double r = R(fut);
            int i = Arg(Fut, C, fut, r, +1, true);
            if (i >= 0) o.Add(new NivelFam(Fut[i], "D1"));
            int j = Arg(Fut, P, fut, r, -1, false);
            if (j >= 0) o.Add(new NivelFam(Fut[j], "D2"));
            return o;
        }

        /// <summary>MAJORS: D1 = argmax g (g &gt; 0), D2 = argmin g (g &lt; 0), en R.</summary>
        public static List<NivelFam> Majors(double[] Fut, double[] g, double fut)
        {
            var o = new List<NivelFam>(2);
            if (Fut == null || Fut.Length == 0) return o;
            double r = R(fut);
            int i = Arg(Fut, g, fut, r, +1, true);
            if (i >= 0) o.Add(new NivelFam(Fut[i], "D1"));
            int j = Arg(Fut, g, fut, r, -1, false);
            if (j >= 0) o.Add(new NivelFam(Fut[j], "D2"));
            return o;
        }

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

        /// <summary>ZEST: [(z, "Z")] si z no es NaN y |z - fut| &lt;= 300.</summary>
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

        // ------------------------------------------------------------------ atajos sobre un LibroMinuto
        public static List<NivelFam> Muros(LibroMinuto b, bool oi) => b == null ? new List<NivelFam>() : Muros(b.Fut, oi ? b.GoC : b.GvC, oi ? b.GoP : b.GvP, b.FutMnq);
        public static List<NivelFam> Majors(LibroMinuto b, bool oi) => b == null ? new List<NivelFam>() : Majors(b.Fut, oi ? b.Go : b.Gv, b.FutMnq);
        public static List<NivelFam> Ztp(LibroMinuto b, bool oi) => b == null ? new List<NivelFam>() : Ztp(b.Fut, oi ? b.Go : b.Gv, b.FutMnq);
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
                case "CONF": return "conf";
                default: return e;      // TRES: D1/D2
            }
        }
    }
}
