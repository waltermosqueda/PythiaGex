// LibroMinutoArmado.cs — PythiaGex 4.1, modulo Familia, CODIGO COMUN (B3a, 08-10-2026).
//  * ArmadoLibroMinuto.Armar = la clase BM de backtest_familia: lleva el perfil por lado al precio de ES (ES Fut = K + corr; SPX Fut = K + base;
//    SPY Fut = K * razon) y conserva SOLO los strikes con |Fut - fut| <= 0.03 * fut, en orden de K (= Fut ascendente). Un libro con el
//    arreglo vacio despues del filtro SIGUE presente en el minuto (cuenta para CONF y para "los tres libros" de FAM), igual que en Python.
//  * SeriesLibroFam.Calcular = el bloque por libro de backtest_familia.conjuntos_minuto: MUROS, MAJORS, ZTP y ZEST por volumen y por OI, con
//    los montos (C1) y los pools de CONF (MUROS + MAJORS + ZTP de cada fuente, en ese orden). OI: en ES solo si OiOk (C2 suprime); en SPX/SPY
//    siempre (oi_fresco solo MARCA, preview "oi_viejo"). Las claves son "<TIPO>_<LIBRO>_<vol|oi>" (ej. "MUROS_ES_vol").
using System;
using System.Collections.Generic;

namespace PythiaGexCinco.Familia
{
    public static class ArmadoLibroMinuto
    {
        /// <summary>BM(libro, fut, ...): r en el eje del libro; zvEs/zoEs YA en precio de ES (NaN si no hay). null si r es null.</summary>
        public static LibroMinuto Armar(string libro, long clave, double futMnq, double S, double conv, bool porRazon, PerfilLado r,
                                        double zvEs, double zoEs, DateTime datoUtc, bool oiOk, double mult, string convTexto, bool congelada,
                                        double[] kCarga = null, double[] oiCarga = null, double[] oiCargaC = null, double[] oiCargaP = null, double[] gexCarga = null)
        {
            if (r == null) return null;
            int n = r.N;
            var keep = new List<int>(n);
            var futAll = new double[n];
            for (int i = 0; i < n; i++)
            {
                futAll[i] = porRazon ? r.K[i] * conv : r.K[i] + conv;
                if (Math.Abs(futAll[i] - futMnq) <= futMnq * SeleccionFam.FILTRO) keep.Add(i);
            }
            int m = keep.Count;
            var b = new LibroMinuto
            {
                Libro = libro, Clave = clave, FutMnq = futMnq, S = S, Conv = conv, PorRazon = porRazon,
                Fut = new double[m], K = new double[m], GvC = new double[m], GvP = new double[m], GoC = new double[m], GoP = new double[m],
                Gv = new double[m], Go = new double[m], ZeroVol = zvEs, ZeroOi = zoEs, DatoUtc = datoUtc, OiOk = oiOk, Mult = mult,
                ConvTexto = convTexto ?? "", Congelada = congelada,
            };
            for (int j = 0; j < m; j++)
            {
                int i = keep[j];
                b.Fut[j] = futAll[i]; b.K[j] = r.K[i];
                b.GvC[j] = r.GvC[i]; b.GvP[j] = r.GvP[i]; b.GoC[j] = r.GoC[i]; b.GoP[j] = r.GoP[i]; b.Gv[j] = r.Gv[i]; b.Go[j] = r.Go[i];
            }
            if (kCarga != null && oiCarga != null && kCarga.Length == oiCarga.Length && kCarga.Length > 0)
            {
                var fc = new List<double>(kCarga.Length); var cg = new List<double>(kCarga.Length);
                var cc = new List<double>(kCarga.Length); var cp = new List<double>(kCarga.Length);
                var kk = new List<double>(kCarga.Length); var gx = new List<double>(kCarga.Length);
                for (int i = 0; i < kCarga.Length; i++)
                {
                    double F = porRazon ? kCarga[i] * conv : kCarga[i] + conv;
                    if (Math.Abs(F - futMnq) > futMnq * SeleccionFam.FILTRO) continue;
                    fc.Add(F); cg.Add(oiCarga[i]); kk.Add(kCarga[i]);
                    gx.Add(gexCarga != null && i < gexCarga.Length ? gexCarga[i] : double.NaN);
                    cc.Add(oiCargaC != null && i < oiCargaC.Length ? oiCargaC[i] : double.NaN);
                    cp.Add(oiCargaP != null && i < oiCargaP.Length ? oiCargaP[i] : double.NaN);
                }
                if (fc.Count > 0) { b.FutCarga = fc.ToArray(); b.Carga = cg.ToArray(); b.CargaC = cc.ToArray(); b.CargaP = cp.ToArray(); b.CargaK = kk.ToArray(); b.CargaGex = gx.ToArray(); }
            }
            return b;
        }
    }

    /// <summary>Las series de UN libro en un minuto (ver cabecera) y sus pools para CONF.</summary>
    public sealed class SeriesLibro
    {
        public string Libro;
        /// <summary>"MUROS_ES_vol" -> niveles (solo las no vacias, como Python: `if v:`).</summary>
        public Dictionary<string, List<NivelFam>> Series = new Dictionary<string, List<NivelFam>>();
        /// <summary>backtest_familia pools[fu][libro]: precios de MUROS + MAJORS + ZTP de esa fuente (solo si la fuente vale).</summary>
        public List<double> PoolVol, PoolOi;
    }

    public static class SeriesLibroFam
    {
        public static readonly string[] TIPOS = { "MUROS", "MAJORS", "ZTP", "ZEST", "TOPN" };

        /// <summary>oi_vale(b): ES -> OiOk (C2); SPX/SPY -> siempre (la marca oi_fresco no apaga).</summary>
        public static bool OiVale(LibroMinuto b) => b != null && (b.Libro != "ES" || b.OiOk);

        public static SeriesLibro Calcular(LibroMinuto b)
        {
            var s = new SeriesLibro { Libro = b?.Libro };
            if (b == null) return s;
            double mult = b.Mult / 100.0;
            foreach (var fu in new[] { "vol", "oi" })
            {
                bool oi = fu == "oi";
                if (oi && !OiVale(b)) continue;
                var mu = SeleccionFam.Muros(b, oi);
                foreach (var n in mu) n.GexM = SeleccionFam.Monto(b.Fut, n.E == "D1" ? (oi ? b.GoC : b.GvC) : (oi ? b.GoP : b.GvP), n.P, mult);
                var ma = SeleccionFam.Majors(b, oi);
                foreach (var n in ma) n.GexM = SeleccionFam.Monto(b.Fut, oi ? b.Go : b.Gv, n.P, mult);
                var tn = SeleccionFam.TopN(b, oi);
                foreach (var n in tn) n.GexM = SeleccionFam.Monto(b.Fut, oi ? b.Go : b.Gv, n.P, mult);
                var zt = SeleccionFam.Ztp(b, oi);
                var ze = SeleccionFam.Zest(b, oi);
                if (mu.Count > 0) s.Series["MUROS_" + b.Libro + "_" + fu] = mu;
                if (ma.Count > 0) s.Series["MAJORS_" + b.Libro + "_" + fu] = ma;
                if (zt.Count > 0) s.Series["ZTP_" + b.Libro + "_" + fu] = zt;
                if (ze.Count > 0) s.Series["ZEST_" + b.Libro + "_" + fu] = ze;
                if (tn.Count > 0) s.Series["TOPN_" + b.Libro + "_" + fu] = tn;
                var pool = new List<double>(6);
                foreach (var n in mu) pool.Add(n.P);
                foreach (var n in ma) pool.Add(n.P);
                foreach (var n in zt) pool.Add(n.P);
                if (oi) s.PoolOi = pool; else s.PoolVol = pool;
            }
            return s;
        }
    }
}
