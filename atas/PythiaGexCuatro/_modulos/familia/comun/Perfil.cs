// Perfil.cs — PythiaGex 4.1, modulo Familia, CODIGO COMUN (B3a, 08-10-2026).
// Port exacto de backtest_familia.filas_hoy (C10: vencido excluido + horizonte Hoy con envejecimiento, = GammaHoyNucleo.Envejecer +
// PasaHorizonte Hoy) y de backtest_familia.perfil_cp (GEX por strike POR LADO: calls +, puts -; por volumen y por OI).
//
//   env      = clamp((ahora - generado) / 86400, 0, 2)                  dias_env = dias - env
//   mas      = min(dias_env >= 0) (0 si no hay)                         tope = max(1.0, mas + 0.01)
//   entra    = 0 <= V < len(dias) y 0 <= dias_env[V] <= tope            T = max(dias_env[V], 1/1440) / 365
//   esc      = 100 * S * S * 0.01
//   a_vc = g(S,K,T,iv_c)*vol_c*esc   a_vp = -g(S,K,T,iv_p)*vol_p*esc   a_oc = g_c*oi_c*esc   a_op = -g_p*oi_p*esc
//   aporta   = (a_vc + a_vp != 0) o (a_oc + a_op != 0)
//   por K (ascendente, suma en el orden de las filas como np.bincount): GvC GvP GoC GoP; Gv = GvC + GvP; Go = GoC + GoP
// Las filas que entran son las de TODO el libro (sin el filtro de +-3 %): ese filtro lo aplica ArmadoLibroMinuto despues de convertir a NQ.
using System;
using System.Collections.Generic;

namespace PythiaGexCuatro.Familia
{
    /// <summary>Las filas del horizonte Hoy de una foto en un instante, con T ya envejecido (backtest_familia.filas_hoy). Inmutable.</summary>
    public sealed class FilasHoy
    {
        public int N;
        public double[] K, T, OiC, OiP, IvC, IvP, VolC, VolP;
        public double Env, MasCerca, Tope;

        /// <summary>Envejecimiento de la cadena (GammaHoyNucleo.Envejecer / nucleo.envejecer): entre 0 y 2 dias. Sin generado: 0.</summary>
        public static double Envejecer(DateTime generadoUtc, DateTime ahoraUtc)
        {
            if (generadoUtc == default) return 0.0;
            double seg = (ahoraUtc - generadoUtc).TotalSeconds;
            return Math.Max(0.0, Math.Min(2.0, seg / 86400.0));
        }

        /// <summary>null si la foto no tiene filas o ninguna entra en el horizonte (como Python: None).</summary>
        public static FilasHoy Armar(FotoCadena f, DateTime ahoraUtc) => Armar(f, ahoraUtc, false);

        /// <summary>4.1.7 (12-10-2026): todos=true NO aplica el tope de horizonte: entran TODOS los vencimientos de la cadena.
        /// Hace falta para la carga: en CME los strikes redondos llevan su interes abierto en 4, 11, 18 y 25 dias, no en el 0DTE.</summary>
        public static FilasHoy Armar(FotoCadena f, DateTime ahoraUtc, bool todos)
        {
            if (f?.Filas == null || f.Filas.Length == 0) return null;
            var dias = f.Dias ?? Array.Empty<double>();
            double env = Envejecer(f.GeneradoUtc, ahoraUtc);
            int nd = dias.Length;
            var de = new double[nd];
            double mas = double.PositiveInfinity; bool hay = false;
            for (int i = 0; i < nd; i++)
            {
                de[i] = dias[i] - env;
                if (de[i] >= 0) { hay = true; if (de[i] < mas) mas = de[i]; }
            }
            if (!hay) mas = 0.0;
            double tope = Math.Max(1.0, mas + 0.01);
            var F = f.Filas;
            int n = 0;
            var ok = new bool[F.Length];
            var d = new double[F.Length];
            for (int i = 0; i < F.Length; i++)
            {
                int v = F[i].V;
                if (v < 0 || v >= nd) continue;
                double x = de[v];
                if (x >= 0 && (todos || x <= tope)) { ok[i] = true; d[i] = x; n++; }
            }
            if (n == 0) return null;
            var r = new FilasHoy
            {
                N = n, Env = env, MasCerca = mas, Tope = tope,
                K = new double[n], T = new double[n], OiC = new double[n], OiP = new double[n],
                IvC = new double[n], IvP = new double[n], VolC = new double[n], VolP = new double[n],
            };
            int j = 0;
            for (int i = 0; i < F.Length; i++)
            {
                if (!ok[i]) continue;
                var x = F[i];
                r.K[j] = x.K; r.T[j] = Math.Max(d[i], GammaFam.PISO_DIAS) / 365.0;
                r.OiC[j] = x.OiC; r.OiP[j] = x.OiP; r.IvC[j] = x.IvC; r.IvP[j] = x.IvP; r.VolC[j] = x.VolC; r.VolP[j] = x.VolP;
                j++;
            }
            return r;
        }
    }

    /// <summary>GEX por strike por lado, en el eje del libro (backtest_familia.perfil_cp). K ascendente. Inmutable.</summary>
    public sealed class PerfilLado
    {
        public int N;
        public double[] K, GvC, GvP, GoC, GoP, Gv, Go;
        public double S;                       // el S con que se calculo (eje del libro)

        /// <summary>null si fl es null, S &lt;= 0 o ninguna fila aporta (Python: None -> el libro no vale en ese minuto).
        /// porLado (4.1.4, solo para los CAMBIOS de posiciones/CambiosFamilia): una fila aporta si CUALQUIER lado es distinto de 0, no el neto. Las
        /// series (vista previa, paridad) usan el neto (false, el default): una fila con calls y puts que se anulan no entra. En una fotoΔ eso perdia
        /// el cambio de cada lado cuando los dos cambian igual con la misma IV (NQ OI 10-09 05:59:55Z, K 31.160: +9 calls y +9 puts, IV 0,206258
        /// las dos: el cambio del muro C y del muro P de ese strike salia 0).</summary>
        /// <summary>4.1.7: interes abierto TOTAL por strike sumando TODOS los vencimientos (lo que muestra el mapa de calor de CME).
        /// No usa gamma: es "cuanto papel hay puesto ahi", que es lo que el operador ve cargado y donde el precio reacciona.</summary>
        /// <summary>4.1.8: ademas del OI por strike devuelve la GAMMA de ese strike sumando LOS MISMOS vencimientos.
        /// Hasta la 4.1.8 el rotulo mezclaba horizontes: los contratos eran de todos los vencimientos y el monto salia
        /// del perfil del FRENTE (ReglasFam.Monto -> MontoEn(b.Fut, b.Go, ...)), que ademas exige coincidencia exacta,
        /// asi que un strike cargado sin gamma del frente salia sin monto. Ahora los dos numeros del rotulo miden lo mismo.</summary>
        public static void CargaPorStrike(FotoCadena f, DateTime ahoraUtc, double S, bool esFuturo,
                                          out double[] K, out double[] Oi, out double[] OiC, out double[] OiP, out double[] Gex)
        {
            Gex = null;
            CargaPorStrike(f, ahoraUtc, out K, out Oi, out OiC, out OiP);
            if (K == null || S <= 0) return;
            var pf = Armar(FilasHoy.Armar(f, ahoraUtc, true), S, esFuturo);
            if (pf == null || pf.K == null) return;
            var g = new Dictionary<double, double>(pf.K.Length);
            for (int i = 0; i < pf.K.Length; i++) g[pf.K[i]] = pf.Go[i];
            Gex = new double[K.Length];
            for (int i = 0; i < K.Length; i++) Gex[i] = g.TryGetValue(K[i], out var x) ? x : double.NaN;
        }

        public static void CargaPorStrike(FotoCadena f, DateTime ahoraUtc, out double[] K, out double[] Oi, out double[] OiC, out double[] OiP)
        {
            K = null; Oi = null; OiC = null; OiP = null;
            var fl = FilasHoy.Armar(f, ahoraUtc, true);
            if (fl == null || fl.N == 0) return;
            var ac = new Dictionary<double, double[]>();   // [0]=calls [1]=puts
            for (int i = 0; i < fl.N; i++)
            {
                double c = fl.OiC[i] > 0 ? fl.OiC[i] : 0, q = fl.OiP[i] > 0 ? fl.OiP[i] : 0;
                if (c + q <= 0) continue;
                if (!ac.TryGetValue(fl.K[i], out var a)) { a = new double[2]; ac[fl.K[i]] = a; }
                a[0] += c; a[1] += q;
            }
            if (ac.Count == 0) return;
            var ks = new List<double>(ac.Keys); ks.Sort();
            K = ks.ToArray(); Oi = new double[ks.Count]; OiC = new double[ks.Count]; OiP = new double[ks.Count];
            for (int i = 0; i < ks.Count; i++) { var a = ac[ks[i]]; OiC[i] = a[0]; OiP[i] = a[1]; Oi[i] = a[0] + a[1]; }
        }

        public static PerfilLado Armar(FilasHoy fl, double S, bool esFuturo, bool porLado = false)
        {
            if (fl == null || S <= 0) return null;
            int n = fl.N;
            double esc = 100.0 * S * S * 0.01;
            var avc = new double[n]; var avp = new double[n]; var aoc = new double[n]; var aop = new double[n];
            var aporta = new bool[n];
            int na = 0;
            for (int i = 0; i < n; i++)
            {
                double gc = GammaFam.De(esFuturo, S, fl.K[i], fl.T[i], fl.IvC[i]);
                double gp = GammaFam.De(esFuturo, S, fl.K[i], fl.T[i], fl.IvP[i]);
                avc[i] = gc * fl.VolC[i] * esc; avp[i] = -gp * fl.VolP[i] * esc;
                aoc[i] = gc * fl.OiC[i] * esc; aop[i] = -gp * fl.OiP[i] * esc;
                bool ap = porLado ? (avc[i] != 0 || avp[i] != 0 || aoc[i] != 0 || aop[i] != 0) : ((avc[i] + avp[i] != 0) || (aoc[i] + aop[i] != 0));
                if (ap) { aporta[i] = true; na++; }
            }
            if (na == 0) return null;
            // np.unique(K[aporta]) + np.bincount(inv, x[aporta]): K distintos ascendentes; suma en el orden de las filas
            var ks = new List<double>(na);
            for (int i = 0; i < n; i++) if (aporta[i]) ks.Add(fl.K[i]);
            ks.Sort();
            var uniq = new List<double>(ks.Count);
            foreach (var k in ks) if (uniq.Count == 0 || uniq[uniq.Count - 1] != k) uniq.Add(k);
            int m = uniq.Count;
            var idx = new Dictionary<double, int>(m);
            for (int i = 0; i < m; i++) idx[uniq[i]] = i;
            var r = new PerfilLado
            {
                N = m, S = S, K = uniq.ToArray(),
                GvC = new double[m], GvP = new double[m], GoC = new double[m], GoP = new double[m], Gv = new double[m], Go = new double[m],
            };
            for (int i = 0; i < n; i++)
            {
                if (!aporta[i]) continue;
                int j = idx[fl.K[i]];
                r.GvC[j] += avc[i]; r.GvP[j] += avp[i]; r.GoC[j] += aoc[i]; r.GoP[j] += aop[i];
            }
            for (int j = 0; j < m; j++) { r.Gv[j] = r.GvC[j] + r.GvP[j]; r.Go[j] = r.GoC[j] + r.GoP[j]; }
            return r;
        }
    }
}
