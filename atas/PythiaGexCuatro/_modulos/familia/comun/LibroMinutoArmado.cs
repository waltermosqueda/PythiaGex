// LibroMinutoArmado.cs — PythiaGex 4.1, modulo Familia, CODIGO COMUN (B3a, 08-10-2026).
//  * ArmadoLibroMinuto.Armar = la clase BM de backtest_familia: lleva el perfil por lado al precio de NQ (NQ Fut = K + corr; NDX Fut = K + base;
//    QQQ Fut = K * razon) y conserva SOLO los strikes con |Fut - fut| <= 0.03 * fut, en orden de K (= Fut ascendente). Un libro con el
//    arreglo vacio despues del filtro SIGUE presente en el minuto (cuenta para CONF y para "los tres libros" de FAM), igual que en Python.
//  * SeriesLibroFam.Calcular = el bloque por libro de backtest_familia.conjuntos_minuto: MUROS, MAJORS, ZTP y ZEST por volumen y por OI, con
//    los montos (C1) y los pools de CONF (MUROS + MAJORS + ZTP de cada fuente, en ese orden). OI: en NQ solo si OiOk (C2 suprime); en NDX/QQQ
//    siempre (oi_fresco solo MARCA, preview "oi_viejo"). Las claves son "<TIPO>_<LIBRO>_<vol|oi>" (ej. "MUROS_NQ_vol").
using System;
using System.Collections.Generic;

namespace PythiaGexCuatro.Familia
{
    public static class ArmadoLibroMinuto
    {
        /// <summary>BM(libro, fut, ...): r en el eje del libro; zvNq/zoNq YA en precio de NQ (NaN si no hay). null si r es null.</summary>
        public static LibroMinuto Armar(string libro, long clave, double futMnq, double S, double conv, bool porRazon, PerfilLado r,
                                        double zvNq, double zoNq, DateTime datoUtc, bool oiOk, double mult, string convTexto, bool congelada)
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
                Gv = new double[m], Go = new double[m], ZeroVol = zvNq, ZeroOi = zoNq, DatoUtc = datoUtc, OiOk = oiOk, Mult = mult,
                ConvTexto = convTexto ?? "", Congelada = congelada,
            };
            for (int j = 0; j < m; j++)
            {
                int i = keep[j];
                b.Fut[j] = futAll[i]; b.K[j] = r.K[i];
                b.GvC[j] = r.GvC[i]; b.GvP[j] = r.GvP[i]; b.GoC[j] = r.GoC[i]; b.GoP[j] = r.GoP[i]; b.Gv[j] = r.Gv[i]; b.Go[j] = r.Go[i];
            }
            return b;
        }
    }

    /// <summary>Las series de UN libro en un minuto (ver cabecera) y sus pools para CONF.</summary>
    public sealed class SeriesLibro
    {
        public string Libro;
        /// <summary>"MUROS_NQ_vol" -> niveles (solo las no vacias, como Python: `if v:`).</summary>
        public Dictionary<string, List<NivelFam>> Series = new Dictionary<string, List<NivelFam>>();
        /// <summary>backtest_familia pools[fu][libro]: precios de MUROS + MAJORS + ZTP de esa fuente (solo si la fuente vale).</summary>
        public List<double> PoolVol, PoolOi;
    }

    public static class SeriesLibroFam
    {
        public static readonly string[] TIPOS = { "MUROS", "MAJORS", "ZTP", "ZEST" };

        /// <summary>oi_vale(b): NQ -> OiOk (C2); NDX/QQQ -> siempre (la marca oi_fresco no apaga).</summary>
        public static bool OiVale(LibroMinuto b) => b != null && (b.Libro != "NQ" || b.OiOk);

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
                var zt = SeleccionFam.Ztp(b, oi);
                var ze = SeleccionFam.Zest(b, oi);
                if (mu.Count > 0) s.Series["MUROS_" + b.Libro + "_" + fu] = mu;
                if (ma.Count > 0) s.Series["MAJORS_" + b.Libro + "_" + fu] = ma;
                if (zt.Count > 0) s.Series["ZTP_" + b.Libro + "_" + fu] = zt;
                if (ze.Count > 0) s.Series["ZEST_" + b.Libro + "_" + fu] = ze;
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
