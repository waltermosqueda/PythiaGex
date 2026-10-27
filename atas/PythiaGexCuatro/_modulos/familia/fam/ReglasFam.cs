// ReglasFam.cs — PythiaGex 4.1, modulo fam (B3d), 08-10-2026.
// La cuenta de UN minuto a partir de los libros del minuto (LibroMinuto de nq/ndx/qqq), port EXACTO de:
//   laboratorio/tres/auditoria_0810/backtest_familia.py   conjuntos_minuto (solo lo que usa la vista previa), confluencia, fusion (C1), ztp (C3),
//                                                          zest, oi_vale, cruces
//   laboratorio/tres/extremos_rebote.py                    f_muros, f_majors, _R, _arg (primera ocurrencia del maximo/minimo)
//   laboratorio/tres/auditoria_0810/preview_niveles.py     Motor.calcular (reg = round(p, 2), etiqueta, monto) y Motor._monto
// Redondeos como Python: round() de Python (correcto sobre el valor binario exacto, empate a par), np.round (empate a par), np.mean
// (suma por pares de numpy, empezando en 0.0, dividida por n: medido contra numpy 2.4.3 en 114.000 arreglos, 0 diferencias).
// Sin referencias a ATAS. Sin estado: se puede llamar desde cualquier hilo.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace PythiaGexCuatro.Familia
{
    /// <summary>Lo calculado en un minuto (preview_niveles.Motor.niv[key] + lo que hace falta para los actuales): por serie de CatalogoFamilia.Calc.</summary>
    public sealed class RegistroMinuto
    {
        public long Clave;                               // ms UTC / 60000
        public string[] Libros = Array.Empty<string>();  // los libros vigentes en el minuto (orden NQ, NDX, QQQ); vacio = minuto calculado sin libros
        public Nivel[][] Series;                          // indice = CatalogoFamilia.IndiceCalc(id); null = la serie no tiene niveles en el minuto
        public double[][] Strikes;                        // strike en la unidad de su libro (NQ/NDX p - conv, QQQ p / conv, round 2); NaN en familia/CONF
        public MetaLibro[] Meta = Array.Empty<MetaLibro>(); // una por libro vigente (fuente, edad, conversion) -> pestaña y actuales
        public double[][] Crudo;                          // precios SIN redondear (diagnostico del arnes; no se persiste)
        public double FutMnq = double.NaN;
        public bool Vacio => Series == null || Array.TrueForAll(Series, x => x == null);   // Python: niv[key] == {} (falsy)
        public bool TieneLibros => Libros != null && Libros.Length > 0;
        public MetaLibro MetaDe(string libro) { foreach (var m in Meta) if (m.Libro == libro) return m; return null; }

        // ---- 4.1.3 (09-10-2026): las series "como la 2.0" (CatalogoFamilia.Extra). Fuera de Series/Meta a proposito: CONF, FAM, la paridad con la
        //      vista previa y la edad de la familia (Meta) no las ven. null = no se calcularon (motor sin OpcionesMotorFamilia.Extras, o minuto viejo).
        /// <summary>indice = CatalogoFamilia.IndiceExtra(id); null por serie = sin niveles en el minuto.</summary>
        public Nivel[][] Extra;
        /// <summary>strike en la unidad de su libro (QQQ / NDX), round 2; mismo indice que Extra.</summary>
        public double[][] ExtraStrikes;
        /// <summary>conversion y edad de las replicas de la 2.0 ("2.0 QQQ": su razon; "2.0 NDX": su base). Las DOMS_* usan Meta de su libro.</summary>
        public MetaLibro[] MetaExtra = Array.Empty<MetaLibro>();
        /// <summary>uso interno del motor: ya se intento completar las series extra de un minuto cargado del archivo (no se reintenta).</summary>
        public bool ExtraIntentado;
        public bool TieneExtra => Extra != null && !Array.TrueForAll(Extra, x => x == null);
        public MetaLibro MetaExtraDe(string libro) { if (MetaExtra != null) foreach (var m in MetaExtra) if (m.Libro == libro) return m; return null; }

        /// <summary>4.1.8: clon superficial para PUBLICAR al rebobinado. Hace falta porque los registros ya publicados
        /// se mutan despues en el hilo del host al completar las series extra (Replica20 y ClasicaNdx reemplazan
        /// Extra/ExtraStrikes/MetaExtra en tres escrituras NO atomicas). Congelar el diccionario no alcanza: hay que
        /// congelar el registro. Es seguro porque TODAS esas mutaciones reemplazan el arreglo entero, nunca un elemento.</summary>
        public RegistroMinuto Copia() => (RegistroMinuto)MemberwiseClone();
    }

    /// <summary>preview_niveles.Motor._meta_libro reducido: lo que necesitan los actuales y la pestaña.</summary>
    public sealed class MetaLibro
    {
        public string Libro, ConvTexto = "";
        public double Conv = double.NaN, S = double.NaN;
        public DateTime DatoUtc;
        public bool OiOk = true, Congelada;
        public static MetaLibro De(LibroMinuto b) => new MetaLibro
        {
            Libro = b.Libro, ConvTexto = b.ConvTexto ?? "", Conv = b.Conv, S = b.S, DatoUtc = b.DatoUtc, OiOk = b.OiOk, Congelada = b.Congelada
        };
    }

    /// <summary>El perfil de la familia sumada (backtest_familia.Fus).</summary>
    public sealed class PerfilFamilia
    {
        public double Fut;                                // el precio del minuto
        public double[] K, GvC, GvP, GoC, GoP, Gv, Go;    // K = grilla de 5 pts en precio de NQ, ascendente
    }

    public static class ReglasFam
    {
        public const double RADIO = 100.0;                // backtest_familia.RADIO (ZTP y CONF)
        public const double ISLA = 0.10;                  // C3
        public const double CONF_PTS = 3.0;
        public const double FAM_GRILLA = 5.0;
        public const double ZEST_MAX = 300.0;

        /// <summary>backtest_familia.MULT / 100 (C1): NQ 0,2; NDX y QQQ 1.</summary>
        public static double Escala(LibroMinuto b)
        {
            if (b.Mult > 0) return b.Mult / 100.0;
            return b.Libro == "NQ" ? 20.0 / 100.0 : 100.0 / 100.0;
        }

        /// <summary>backtest_familia.oi_vale(b) con los defaults (fecha=True, fresco=False): NQ -> oi_ok (C2); CBOE -> siempre.</summary>
        public static bool OiVale(LibroMinuto b) => b.Libro != "NQ" || b.OiOk;

        // ------------------------------------------------------------------ formulas por libro (extremos_rebote.f_muros / f_majors)
        private static double R(double fut) => Math.Min(fut * 0.02, 100.0);

        /// <summary>f_muros: D1 = argmax C (C > 0) y D2 = argmin P (P < 0) entre |Fut - fut| <= R; primera ocurrencia.</summary>
        public static void Muros(double[] Fut, double fut, double[] C, double[] P, List<(double P, string E)> sal)
        {
            double r = R(fut); int i = -1, j = -1;
            for (int k = 0; k < Fut.Length; k++)
            {
                if (!(Math.Abs(Fut[k] - fut) <= r)) continue;
                if (C[k] > 0 && (i < 0 || C[k] > C[i])) i = k;
                if (P[k] < 0 && (j < 0 || P[k] < P[j])) j = k;
            }
            if (i >= 0) sal.Add((Fut[i], "D1"));
            if (j >= 0) sal.Add((Fut[j], "D2"));
        }

        /// <summary>f_majors: D1 = argmax g (g > 0), D2 = argmin g (g < 0) entre |Fut - fut| <= R.</summary>
        public static void Majors(double[] Fut, double fut, double[] g, List<(double P, string E)> sal) => Muros(Fut, fut, g, g, sal);

        /// <summary>backtest_familia.ztp(b, oi, isla=0.10, fuerza=0): cruces sin islas (C3) a <= 100 pts; D1 el menor por encima, D2 el mayor por debajo.</summary>
        /// <summary>4.1.7: los N strikes con mas |valor| dentro del radio R, de mayor a menor (D1..DN).
        /// MUROS y MAJORS marcan UNO por lado; medido el 12-10 habia 24 strikes con gamma en +-100 pts y se dibujaban 2.</summary>
        public static int TopNCantidad = 6;
        private static double Radio(double fut) => Math.Min(fut * 0.02, 100.0);

        /// <summary>4.1.8 (12-10-2026): TODOS los strikes con carga >= umbral dentro del radio, de mayor a menor.
        /// Pedido del operador: "si hay mas de 50 contratos tiene MINIMO que estar marcado, pase lo que pase".
        /// Un top-N fijo deja afuera los dias con muchos strikes cargados; el umbral no.
        /// El tope solo evita que un dia raro llene la pantalla.</summary>
        public static double CargaMinima = 50.0;
        public static int CargaTope = 12;
        /// <summary>4.1.8: radio propio de la carga, en puntos. 0 = usar Radio(fut), que con MNQ en 31.000 es la
        /// CONSTANTE 100 (el termino del 2 % son 620 y nunca manda). Un strike cargado no deja de existir porque el
        /// precio se corrio 4 puntos: medido el 12-10, el 30800 se caia con el futuro en 30904 y volvia en 30890.
        /// Techo 300: pasando los +-309 pts la cadena de NQ llega con un strike de cada dos (CadenaApi.cs:687) y el
        /// numero dejaria de ser trazable.</summary>
        public static int CargaRadioPts = 0;
        /// <summary>4.1.8: cuantos strikes cargados se marcan SIEMPRE, aunque no lleguen al umbral de contratos.
        /// Medido el 12-10-2026: el umbral absoluto de 50 borra medio libro en cuanto vence el 0DTE, porque TODOS los
        /// strikes pierden sus contratos del dia a la vez. Caso del operador: el strike NDX 30675 (30.904 en el eje
        /// MNQ) venia marcado con 51 contratos y a las 20:01Z desaparecio con 32 — pero 25 de esos 32 estaban en el
        /// vencimiento a 4 dias, o sea una posicion abierta de verdad, y el precio siguio respetando ese nivel. Un
        /// numero fijo no puede decidir que es significativo cuando el libro entero se achica tres veces.</summary>
        public static int CargaMinimaRayas = 10;  // MEDIDO sobre el caso del operador (12-10): ordenando por carga los
                                                 // 22 strikes de NDX con OI>0 en el radio, el 30675 (30.904 en el eje
                                                 // MNQ, 32 contratos, 25 de ellos a 4 dias) queda NOVENO. Con 8 entraba
                                                 // el 30625 y no el suyo.

        public static void CargaPorUmbral(double[] Fut, double fut, double[] v, List<(double P, string E)> sal, double umbral, int tope, double[] vC = null, double[] vP = null)
        {
            if (Fut == null || v == null || Fut.Length == 0) return;
            double r = CargaRadioPts > 0 ? Math.Min(CargaRadioPts, 300) : Radio(fut);
            // Todos los del radio, ordenados por carga: primero se aplica el umbral y, si quedan menos de
            // CargaMinimaRayas, se completa con los que siguen en carga aunque no lleguen. Asi el libro nunca queda
            // vacio ni se borra de golpe al vencer el 0DTE: lo que cambia es CUANTOS contratos dice la etiqueta.
            var todos = new List<int>();
            for (int i = 0; i < Fut.Length && i < v.Length; i++)
                if (Math.Abs(Fut[i] - fut) <= r && v[i] > 0) todos.Add(i);
            if (todos.Count == 0) return;
            todos.Sort((x, y) => { int c = v[y].CompareTo(v[x]); return c != 0 ? c : x.CompareTo(y); });
            var idx = new List<int>();
            foreach (var i in todos) if (v[i] >= umbral) idx.Add(i);
            for (int i = 0; i < todos.Count && idx.Count < CargaMinimaRayas; i++)
                if (v[todos[i]] < umbral) idx.Add(todos[i]);
            if (idx.Count == 0) return;
            idx.Sort((x, y) => { int c = v[y].CompareTo(v[x]); return c != 0 ? c : x.CompareTo(y); });
            int n = Math.Min(Math.Max(1, tope), idx.Count);
            for (int j = 0; j < n; j++)
            {
                // 4.1.8: los CONTRATOS van en la etiqueta del rol ("D1·301"), que es lo unico que viaja entero
                // hasta el rotulo. Asi la raya se lee sola: rol + contratos, y al lado el monto en millones de gamma
                // y la flecha de cambio que arma la pantalla. Pedido del operador: un nivel que sirva para decidir.
                int i2 = idx[j];
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                string N(double x) => x >= 1000 ? (x / 1000.0).ToString("0.#", inv) + "k" : x.ToString("0", inv);
                // 4.1.8: calls y puts POR SEPARADO, como los lee el operador ("C 120 P 180"): un strike con 300 calls
                // no es lo mismo que uno con 300 puts, y el total los confunde.
                string etq = (vC != null && vP != null && i2 < vC.Length && i2 < vP.Length && !double.IsNaN(vC[i2]) && !double.IsNaN(vP[i2]))
                    ? "C" + N(vC[i2]) + " P" + N(vP[i2])
                    : "D" + (j + 1).ToString(inv) + "·" + N(v[i2]);
                sal.Add((Fut[i2], etq));
            }
        }

        public static void TopNPeso(double[] Fut, double fut, double[] v, List<(double P, string E)> sal, int n)
        {
            if (Fut == null || v == null || Fut.Length == 0) return;
            double r = Radio(fut);
            var idx = new List<int>();
            for (int i = 0; i < Fut.Length && i < v.Length; i++)
                if (Math.Abs(Fut[i] - fut) <= r && v[i] != 0) idx.Add(i);
            if (idx.Count == 0) return;
            idx.Sort((x, y) => { int c = Math.Abs(v[y]).CompareTo(Math.Abs(v[x])); return c != 0 ? c : x.CompareTo(y); });
            int tope = Math.Min(Math.Max(1, n), idx.Count);
            for (int j = 0; j < tope; j++)
                sal.Add((Fut[idx[j]], "D" + (j + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        public static void Ztp(double[] Fut, double fut, double[] g, List<(double P, string E)> sal)
        {
            int n = g.Length;
            var nz = new List<int>(n);
            for (int k = 0; k < n; k++) if (g[k] != 0) nz.Add(k);   // NaN != 0 -> entra, como numpy
            if (nz.Count < 2) return;
            if (nz.Count >= 3)
            {   // islas sobre la secuencia ORIGINAL, quitadas todas juntas
                var keep = new bool[nz.Count];
                keep[0] = true; keep[nz.Count - 1] = true;
                for (int m = 1; m < nz.Count - 1; m++)
                {
                    double gl = g[nz[m - 1]], gm = g[nz[m]], gr = g[nz[m + 1]];
                    double sl = Signo(gl), sm = Signo(gm), sr = Signo(gr);
                    bool isla = sm != sl && sm != sr && Math.Abs(gm) < ISLA * Math.Min(Math.Abs(gl), Math.Abs(gr));
                    keep[m] = !isla;
                }
                var nz2 = new List<int>(nz.Count);
                for (int m = 0; m < nz.Count; m++) if (keep[m]) nz2.Add(nz[m]);
                nz = nz2;
            }
            double arriba = double.NaN, abajo = double.NaN;
            for (int m = 0; m + 1 < nz.Count; m++)
            {
                int i0 = nz[m], i1 = nz[m + 1];
                if (!(g[i0] * g[i1] < 0)) continue;
                double z = Fut[i0] + (Fut[i1] - Fut[i0]) * (-g[i0]) / (g[i1] - g[i0]);
                if (!(Math.Abs(z - fut) <= RADIO)) continue;
                if (z > fut && (double.IsNaN(arriba) || z < arriba)) arriba = z;
                if (z < fut && (double.IsNaN(abajo) || z > abajo)) abajo = z;
            }
            if (!double.IsNaN(arriba)) sal.Add((arriba, "D1"));
            if (!double.IsNaN(abajo)) sal.Add((abajo, "D2"));
        }

        /// <summary>np.sign: -1, 0, 1 (NaN -> NaN).</summary>
        private static double Signo(double x) => double.IsNaN(x) ? double.NaN : x > 0 ? 1.0 : x < 0 ? -1.0 : 0.0;

        /// <summary>backtest_familia.zest: una raya "Z" si el zero no es NaN y |z - fut| <= 300.</summary>
        public static void Zest(double z, double fut, List<(double P, string E)> sal)
        {
            if (!double.IsNaN(z) && Math.Abs(z - fut) <= ZEST_MAX) sal.Add((z, "Z"));
        }

        // ------------------------------------------------------------------ familia
        /// <summary>backtest_familia.confluencia: niveles de 2 o mas libros a <= 3 pts del primero del grupo; la raya es el promedio (np.mean).</summary>
        public static void Confluencia(List<(string Libro, List<double> Ps)> pools, double fut, List<(double P, string E)> sal)
        {
            var items = new List<(double P, string Lb)>();
            foreach (var (lb, ps) in pools) foreach (var p in ps) items.Add((p, lb));
            items.Sort((a, b) => { int c = a.P.CompareTo(b.P); return c != 0 ? c : string.CompareOrdinal(a.Lb, b.Lb); });
            int i = 0, n = 0;
            var libs = new HashSet<string>(StringComparer.Ordinal);
            while (i < items.Count)
            {
                int j = i;
                while (j + 1 < items.Count && items[j + 1].P - items[i].P <= CONF_PTS) j++;
                libs.Clear();
                for (int k = i; k <= j; k++) libs.Add(items[k].Lb);
                if (libs.Count >= 2)
                {
                    var xs = new double[j - i + 1];
                    for (int k = i; k <= j; k++) xs[k - i] = items[k].P;
                    double p = NumPy.Media(xs);
                    if (Math.Abs(p - fut) <= RADIO) { n++; sal.Add((p, "D" + n)); }
                    i = j + 1;
                }
                else i += 1;
            }
        }

        /// <summary>backtest_familia.fusion (C1): Fut redondeado a 5 pts (np.round, empate a par), escalado por MULT/100 y sumado (np.bincount, en orden).</summary>
        public static PerfilFamilia Fusion(IReadOnlyList<LibroMinuto> bms, double fut)
        {
            int tot = 0; foreach (var b in bms) tot += b.Fut.Length;
            var fr = new double[tot]; int q = 0;
            foreach (var b in bms) foreach (var x in b.Fut) fr[q++] = Math.Round(x / FAM_GRILLA, MidpointRounding.ToEven) * FAM_GRILLA;
            var ks = (double[])fr.Clone(); Array.Sort(ks);
            int nu = 0;
            for (int k = 0; k < ks.Length; k++) if (nu == 0 || ks[k] != ks[nu - 1]) ks[nu++] = ks[k];
            Array.Resize(ref ks, nu);
            var inv = new int[tot];
            for (int k = 0; k < tot; k++) inv[k] = Array.BinarySearch(ks, fr[k]);
            var f = new PerfilFamilia { Fut = fut, K = ks, GvC = new double[nu], GvP = new double[nu], GoC = new double[nu], GoP = new double[nu] };
            q = 0;
            foreach (var b in bms)
            {
                double e = Escala(b);
                for (int k = 0; k < b.Fut.Length; k++, q++)
                {
                    int u = inv[q];
                    f.GvC[u] += b.GvC[k] * e; f.GvP[u] += b.GvP[k] * e; f.GoC[u] += b.GoC[k] * e; f.GoP[u] += b.GoP[k] * e;
                }
            }
            f.Gv = new double[nu]; f.Go = new double[nu];
            for (int k = 0; k < nu; k++) { f.Gv[k] = f.GvC[k] + f.GvP[k]; f.Go[k] = f.GoC[k] + f.GoP[k]; }
            return f;
        }

        // ------------------------------------------------------------------ el minuto completo (preview_niveles.Motor.calcular, un key)
        /// <summary>Los niveles de las 21 series de CatalogoFamilia.Calc para un minuto. bk: los libros vigentes en orden NQ, NDX, QQQ (sin nulls).
        /// Devuelve null si bk esta vacio (Python: 'if not bk: continue', el minuto no entra en niv).</summary>
        public static RegistroMinuto Minuto(long clave, IReadOnlyList<LibroMinuto> bk)
        {
            if (bk == null || bk.Count == 0) return null;
            var calc = CatalogoFamilia.Calc;
            var crudo = new List<(double P, string E)>[calc.Length];
            var porLibro = new Dictionary<string, LibroMinuto>(StringComparer.Ordinal);
            var poolsVol = new List<(string, List<double>)>();
            foreach (var b in bk)
            {
                porLibro[b.Libro] = b;
                double fut = b.FutMnq;
                // 4.1.7: CARGA = los N strikes con mas interes abierto TOTAL de TODOS los vencimientos (el mapa de calor de CME).
                if (b.FutCarga != null && b.Carga != null)
                { var cg = new List<(double, string)>(); CargaPorUmbral(b.FutCarga, fut, b.Carga, cg, CargaMinima, CargaTope, b.CargaC, b.CargaP); Poner(crudo, "CARGA_" + b.Libro, cg); }
                foreach (var oi in new[] { false, true })
                {
                    if (oi && !OiVale(b)) continue;
                    string fu = oi ? "oi" : "vol";
                    var mu = new List<(double, string)>(); Muros(b.Fut, fut, oi ? b.GoC : b.GvC, oi ? b.GoP : b.GvP, mu);
                    var ma = new List<(double, string)>(); Majors(b.Fut, fut, oi ? b.Go : b.Gv, ma);
                    var zt = new List<(double, string)>(); Ztp(b.Fut, fut, oi ? b.Go : b.Gv, zt);
                    var ze = new List<(double, string)>(); Zest(oi ? b.ZeroOi : b.ZeroVol, fut, ze);
                    Poner(crudo, "MUROS_" + b.Libro + "_" + fu, mu);
                    Poner(crudo, "MAJORS_" + b.Libro + "_" + fu, ma);
                    Poner(crudo, "ZTP_" + b.Libro + "_" + fu, zt);
                    Poner(crudo, "ZEST_" + b.Libro + "_" + fu, ze);
                    var tn = new List<(double, string)>(); TopNPeso(b.Fut, fut, oi ? b.Go : b.Gv, tn, TopNCantidad);
                    Poner(crudo, "TOPN_" + b.Libro + "_" + fu, tn);
                    if (!oi)
                    {
                        var ps = new List<double>();
                        foreach (var x in mu) ps.Add(x.Item1);
                        foreach (var x in ma) ps.Add(x.Item1);
                        foreach (var x in zt) ps.Add(x.Item1);
                        poolsVol.Add((b.Libro, ps));
                    }
                }
            }
            double fut0 = bk[0].FutMnq;
            var conf = new List<(double, string)>(); Confluencia(poolsVol, fut0, conf);
            Poner(crudo, "CONF_vol", conf);
            PerfilFamilia fam = null;
            if (porLibro.ContainsKey("NQ") && porLibro.ContainsKey("NDX") && porLibro.ContainsKey("QQQ"))
            {
                var orden = new[] { porLibro["NQ"], porLibro["NDX"], porLibro["QQQ"] };
                fam = Fusion(orden, fut0);
                var fv = new List<(double, string)>(); Muros(fam.K, fam.Fut, fam.GvC, fam.GvP, fv); Poner(crudo, "FAM_MUROS_vol", fv);
                if (OiVale(orden[0]) && OiVale(orden[1]) && OiVale(orden[2]))
                { var fo = new List<(double, string)>(); Muros(fam.K, fam.Fut, fam.GoC, fam.GoP, fo); Poner(crudo, "FAM_MUROS_oi", fo); }
            }
            // reg[sid] = [(round(p, 2), e, monto)] en el orden de CALC
            var reg = new RegistroMinuto { Clave = clave, Series = new Nivel[calc.Length][], Strikes = new double[calc.Length][], Crudo = new double[calc.Length][], FutMnq = fut0 };
            var libs = new List<string>(); var meta = new List<MetaLibro>();
            foreach (var b in bk) { libs.Add(b.Libro); meta.Add(MetaLibro.De(b)); }
            reg.Libros = libs.ToArray(); reg.Meta = meta.ToArray();
            for (int s = 0; s < calc.Length; s++)
            {
                var lv = crudo[s];
                if (lv == null || lv.Count == 0) continue;
                var si = CatalogoFamilia.PorId[calc[s]];
                var arr = new Nivel[lv.Count]; var stk = new double[lv.Count]; var cru = new double[lv.Count];
                LibroMinuto b = null; bool esFam = si.Libro == "familia";
                if (!esFam) porLibro.TryGetValue(si.Libro, out b);
                for (int k = 0; k < lv.Count; k++)
                {
                    double p = lv[k].P; string e = lv[k].E;
                    double monto = esFam ? (si.Tipo != "CONF" ? MontoFam(fam, si, p, e) : double.NaN) : Monto(b, si, p, e);
                    double pr = NumPy.RoundPy(p, 2); cru[k] = p;
                    arr[k] = new Nivel(pr, e, monto);
                    // 4.1.8: para la carga el strike CRUDO del libro, no la inversa de la conversion sobre el precio ya
                    // redondeado (daba 30599,99 / 30600,01 en el 8 % de los renglones de NDX y el operador cruza ese
                    // numero contra CBOE/CME).
                    double stkCarga = double.NaN;
                    if (!esFam && b?.FutCarga != null && b.CargaK != null && si.Id != null && si.Id.StartsWith("CARGA", StringComparison.Ordinal))
                        for (int q = 0; q < b.FutCarga.Length && q < b.CargaK.Length; q++)
                            if (Math.Abs(b.FutCarga[q] - p) < 1e-6) { stkCarga = b.CargaK[q]; break; }
                    stk[k] = !double.IsNaN(stkCarga) ? stkCarga
                           : esFam || b == null ? double.NaN
                           : (b.PorRazon ? NumPy.RoundPy(pr / b.Conv, 2) : NumPy.RoundPy(pr - b.Conv, 2));
                }
                reg.Series[s] = arr; reg.Strikes[s] = stk; reg.Crudo[s] = cru;
            }
            return reg;
        }

        private static void Poner(List<(double, string)>[] crudo, string id, List<(double, string)> v)
        {
            if (v.Count == 0) return;
            int i = CatalogoFamilia.IndiceCalc(id);
            if (i >= 0) crudo[i] = v;
        }

        /// <summary>preview_niveles.Motor._monto para una serie de un libro: M USD por 1 % con el multiplicador del libro (C1), round 1. NaN si no aplica.</summary>
        public static double Monto(LibroMinuto b, SerieInfo s, double p, string e)
        {
            if (b == null) return double.NaN;
            // 4.1.8: TOPN (NQ top-N y las series de carga) tambien llevan monto en el rotulo, con la gamma en dolares
            // de ESE strike. Sin esto el rotulo salia pelado ("NQ carga D1") y no se veia ni cuanto pesa ni si crece:
            // el monto es ademas lo que alimenta la flecha de cambio del rotulo.
            if (s.Tipo == "TOPN")
            {
                // 4.1.8: la CARGA se mide en SU propio eje y con SU gamma (todos los vencimientos, los mismos que los
                // contratos del rotulo). Antes se buscaba en b.Fut/b.Go, el perfil del FRENTE: horizontes distintos en
                // el mismo rotulo, y un strike cargado sin gamma del frente salia sin monto.
                if (s.Id != null && s.Id.StartsWith("CARGA", StringComparison.Ordinal))
                    return b.FutCarga == null || b.CargaGex == null ? double.NaN : MontoEn(b.FutCarga, b.CargaGex, p, Escala(b));
                bool oiT = s.Fuente == "oi";
                return MontoEn(b.Fut, oiT ? b.Go : b.Gv, p, Escala(b));
            }
            if (s.Tipo != "MUROS" && s.Tipo != "MAJORS" && s.Tipo != "UNO") return double.NaN;
            bool oi = s.Fuente == "oi";
            double[] v = s.Tipo == "MUROS" ? (e == "D1" ? (oi ? b.GoC : b.GvC) : (oi ? b.GoP : b.GvP)) : (oi ? b.Go : b.Gv);
            return MontoEn(b.Fut, v, p, Escala(b));
        }

        private static double MontoFam(PerfilFamilia f, SerieInfo s, double p, string e)
        {
            if (f == null || (s.Tipo != "MUROS" && s.Tipo != "MAJORS" && s.Tipo != "UNO")) return double.NaN;
            bool oi = s.Fuente == "oi";
            double[] v = s.Tipo == "MUROS" ? (e == "D1" ? (oi ? f.GoC : f.GvC) : (oi ? f.GoP : f.GvP)) : (oi ? f.Go : f.Gv);
            return MontoEn(f.K, v, p, 1.0);
        }

        private static double MontoEn(double[] Fut, double[] v, double p, double mult)
        {
            for (int i = 0; i < Fut.Length; i++)
                if (Math.Abs(Fut[i] - p) < 1e-6) return NumPy.RoundPy(v[i] * mult / 1e6, 1);
            return double.NaN;
        }
    }

    /// <summary>Redondeos y promedios con la semantica exacta de Python/numpy.</summary>
    public static class NumPy
    {
        private static readonly double[] P10 = { 1, 10, 100, 1000, 1e4, 1e5, 1e6, 1e7, 1e8 };

        /// <summary>round(x, nd) de Python: el decimal correcto del valor binario EXACTO, empate a par; devuelve el double mas cercano a ese decimal.</summary>
        public static double RoundPy(double x, int nd)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || nd < 0 || nd > 8) return x;
            double p = P10[nd];
            double y = x * p;
            if (Math.Abs(y) < 1e9)                                // ulp(y) <= 1.2e-7: el error de x*p no llega al umbral de 1e-6
            {
                double fl = Math.Floor(y), fr = y - fl;
                if (Math.Abs(fr - 0.5) > 1e-6) return (fr < 0.5 ? fl : fl + 1.0) / p;   // lejos del empate: el error de x*p no cambia el entero
            }
            return Exacto(x, nd, p);
        }

        private static double Exacto(double x, int nd, double p)
        {
            long bits = BitConverter.DoubleToInt64Bits(x);
            bool neg = bits < 0;
            int exp = (int)((bits >> 52) & 0x7FF);
            long man = bits & 0xFFFFFFFFFFFFFL;
            if (exp == 0) exp = 1; else man |= 1L << 52;
            exp -= 1075;                                          // x = man * 2^exp
            if (exp >= 0) return x;                               // entero: round(x, nd) == x
            var num = new BigInteger(man) * BigInteger.Pow(10, nd);
            var den = BigInteger.One << (-exp);
            var q = BigInteger.DivRem(num, den, out var rem);
            int c = (rem * 2).CompareTo(den);
            if (c > 0 || (c == 0 && !q.IsEven)) q += 1;
            double r = (double)q / p;
            return neg ? -r : r;
        }

        /// <summary>np.mean(lista) de float64: suma por pares de numpy (bloques de 8, empieza en 0.0) dividida por n.</summary>
        public static double Media(double[] a) => a.Length == 0 ? double.NaN : SumaPares(a, 0, a.Length) / a.Length;

        private static double SumaPares(double[] a, int ini, int n)
        {
            if (n < 8)
            {
                double res = 0.0;
                for (int i = 0; i < n; i++) res += a[ini + i];
                return res;
            }
            if (n <= 128)
            {
                var r = new double[8];
                for (int j = 0; j < 8; j++) r[j] = a[ini + j];
                int i = 8;
                for (; i < n - (n % 8); i += 8) for (int j = 0; j < 8; j++) r[j] += a[ini + i + j];
                double res = ((r[0] + r[1]) + (r[2] + r[3])) + ((r[4] + r[5]) + (r[6] + r[7]));
                for (; i < n; i++) res += a[ini + i];
                return res;
            }
            int n2 = n / 2; n2 -= n2 % 8;
            return SumaPares(a, ini, n2) + SumaPares(a, ini + n2, n - n2);
        }
    }
}
