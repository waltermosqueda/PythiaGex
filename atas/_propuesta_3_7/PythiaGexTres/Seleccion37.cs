using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PythiaGexTres
{
    /// <summary>
    /// FORMULAS DE LAS DOMINANTES (3.6.0). 3.7.0: el enum vive aca (archivo sin ATAS) para que Seleccion37 compile afuera (Simulador37).
    /// </summary>
    public enum FormulaDoms3
    {
        [System.ComponentModel.Description("F1 Cruces de signo (3.3.3): el cruce mas cercano por lado (3.7.0: sin islas). Se pega al precio")] Cruces,
        [System.ComponentModel.Description("F2 Una por lado por volumen: el strike con mas |GEX vol| arriba y abajo del precio en el radio (como la clasica)")] UnoPorLadoVol,
        [System.ComponentModel.Description("F3 Una por lado por OI: el strike con mas |GEX OI| arriba y abajo del precio en el radio")] UnoPorLadoOi,
        [System.ComponentModel.Description("F4 Una por lado, volumen + OI: el strike con mas |GEX vol| + |GEX OI| arriba y abajo")] UnoPorLadoVolOi,
        [System.ComponentModel.Description("F5 Muros por volumen: el mayor GEX de calls y el mayor de puts a +-100 pts (como la pagina)")] MurosVol,
        [System.ComponentModel.Description("F6 Muros por OI: el mayor GEX de calls y el mayor de puts por interes abierto a +-100 pts")] MurosOi,
        [System.ComponentModel.Description("F7 Majors por OI: el mayor GEX neto positivo y el mayor negativo por interes abierto")] MajorsOi,
        [System.ComponentModel.Description("F8 Las dos mas grandes por volumen (la regla de la 2.0), sin importar el lado")] DosMasGrandesVol,
    }

    /// <summary>
    /// LA SELECCION DE NIVELES DE LA 3.7.0, SIN ATAS (08-10-2026, auditoria laboratorio/tres/auditoria_0810).
    ///
    /// Todo lo que ELIGE un nivel (cruces sin islas, cerco techo/piso, dos mas grandes, una por lado, muros, formulas F1..F8, tunel por
    /// cruces, que se dibuja segun el radio de cada vela, rotulos del borde, vigencia de una cadena de CBOE, vencimiento del trimestre)
    /// vive aca como funciones estaticas puras: entran perfiles y precios, salen niveles. El indicador solo llama y dibuja; el simulador
    /// (atas/_propuesta_3_7/Simulador37) compila ESTE archivo con el nucleo y Feed y corre lo mismo sobre los archivos de viva3 y de CBOE.
    /// La cuenta de cada formula es la de laboratorio/tres/auditoria_0810/backtest_familia.py (cruces(), ztp(), uno(), MUROS, MAJORS),
    /// citada en cada funcion. Nada mira adelante: cada llamada recibe solo lo conocido en ese instante.
    /// </summary>
    public static class Seleccion37
    {
        /// <summary>C3: un strike es ISLA si su signo es distinto al de sus dos vecinos (sin contar los de GEX cero) y |G| &lt; 10 % del menor
        /// de ellos. Umbral SUPUESTO (auditoria NDX, rev_02_isla_ndx.txt: 31040 con +1,21 M entre -27,93 M y -100 M crea DOS cruces pegados).</summary>
        public const double ISLA = 0.10;

        // ------------------------------------------------------------------ cruces de signo (C3)

        /// <summary>Un cruce de signo entre dos strikes vecinos (con GEX distinto de cero), interpolado. Fuerza = |G izq| + |G der| (nq_06_cruces.py).</summary>
        public readonly struct Cruce
        {
            public readonly double Z, XIzq, XDer, GIzq, GDer;
            public Cruce(double z, double xIzq, double xDer, double gIzq, double gDer) { Z = z; XIzq = xIzq; XDer = xDer; GIzq = gIzq; GDer = gDer; }
            public double Fuerza => Math.Abs(GIzq) + Math.Abs(GDer);
        }

        /// <summary>
        /// C3 (backtest_familia.cruces): xs ordenado; se descartan los G = 0 / NaN; si isla &gt; 0 y hay 3 o mas, se marca como isla (sobre la
        /// secuencia ORIGINAL, sin iterar) cada strike del medio con signo distinto a sus dos vecinos y |G| &lt; isla x min(|vecinos|); los
        /// cruces salen entre strikes consecutivos que quedan, interpolados: x0 + (x1 - x0) x (-g0) / (g1 - g0).
        /// </summary>
        public static List<Cruce> Cruces(IReadOnlyList<double> xs, IReadOnlyList<double> gs, double isla, out int islas)
        {
            islas = 0;
            var r = new List<Cruce>();
            if (xs == null || gs == null) return r;
            int n = Math.Min(xs.Count, gs.Count);
            var nz = new List<int>(n);
            for (int i = 0; i < n; i++) { double g = gs[i]; if (g != 0 && !double.IsNaN(g) && !double.IsInfinity(g) && !double.IsNaN(xs[i])) nz.Add(i); }
            if (nz.Count < 2) return r;
            var queda = new bool[nz.Count];
            for (int j = 0; j < queda.Length; j++) queda[j] = true;
            if (isla > 0 && nz.Count >= 3)
                for (int j = 1; j < nz.Count - 1; j++)
                {
                    double g = gs[nz[j]], ga = gs[nz[j - 1]], gb = gs[nz[j + 1]];
                    if (Math.Sign(g) != Math.Sign(ga) && Math.Sign(g) != Math.Sign(gb) && Math.Abs(g) < isla * Math.Min(Math.Abs(ga), Math.Abs(gb)))
                    { queda[j] = false; islas++; }
                }
            int prev = -1;
            for (int j = 0; j < nz.Count; j++)
            {
                if (!queda[j]) continue;
                if (prev >= 0)
                {
                    int i0 = nz[prev], i1 = nz[j];
                    double g0 = gs[i0], g1 = gs[i1];
                    if ((g0 < 0 && g1 > 0) || (g0 > 0 && g1 < 0))
                        r.Add(new Cruce(xs[i0] + (xs[i1] - xs[i0]) * (-g0) / (g1 - g0), xs[i0], xs[i1], g0, g1));
                }
                prev = j;
            }
            return r;
        }

        public static List<Cruce> Cruces(IReadOnlyList<double> xs, IReadOnlyList<double> gs, double isla = ISLA) => Cruces(xs, gs, isla, out _);

        /// <summary>Los cruces del perfil entero de una lectura (strike por strike), en precio del FUTURO (enFut) o en el eje del libro (K).
        /// porVol = GEX por volumen; si no, por OI. Con isla (C3).</summary>
        public static List<Cruce> CrucesPerfil(IEnumerable<GammaHoyNucleo.Strike> perfil, bool porVol, bool enFut, double isla, out int islas)
        {
            islas = 0;
            if (perfil == null) return new List<Cruce>();
            var p = perfil.Where(s => s != null && (enFut ? s.Fut > 0 : s.K > 0)).OrderBy(s => enFut ? s.Fut : s.K).ToList();
            var xs = p.Select(s => enFut ? s.Fut : s.K).ToList();
            var gs = p.Select(s => porVol ? s.GexVol : s.GexOi).ToList();
            return Cruces(xs, gs, isla, out islas);
        }

        public static List<Cruce> CrucesPerfil(IEnumerable<GammaHoyNucleo.Strike> perfil, bool porVol, bool enFut = true, double isla = ISLA) => CrucesPerfil(perfil, porVol, enFut, isla, out _);

        /// <summary>El nivel mas cercano ARRIBA (z &gt; ref) y el mas cercano ABAJO (z &lt; ref) de una lista (backtest_familia.ztp sin el radio).</summary>
        public static (double Techo, double Piso) Cerco(IEnumerable<double> zs, double ref0)
        {
            double up = double.NaN, dn = double.NaN;
            if (zs != null && ref0 > 0)
                foreach (var z in zs)
                {
                    if (double.IsNaN(z) || z <= 0) continue;
                    if (z > ref0 && (double.IsNaN(up) || z < up)) up = z;
                    if (z < ref0 && (double.IsNaN(dn) || z > dn)) dn = z;
                }
            return (up, dn);
        }

        // ------------------------------------------------------------------ dominantes

        /// <summary>El radio de las dominantes: min(2 % del futuro, tope en pts) (GammaHoyNucleo: RadioDominantesPct 2 y RadioDominantesMaxPts).</summary>
        public static double RadioDominantes(double fut, double topePts) => topePts > 0 ? Math.Min(fut * 0.02, topePts) : fut * 0.02;

        /// <summary>D2 (3.7.0) = la regla de la 2.0 (DominantesReferencia: UnaPorLado y Centroide apagados): las dos barras de mayor |GEX|
        /// a &lt;= radio de 'fut', strike EXACTO, sin centroide ni histeresis. Orden: por |GEX| descendente, desempate estable por strike
        /// (OrderByDescending de LINQ, como el nucleo). porVol = volumen de hoy; si no, OI.</summary>
        public static List<(double Fut, double Gex)> DosMasGrandes(IEnumerable<GammaHoyNucleo.Strike> perfil, double fut, double radio, bool porVol)
        {
            if (perfil == null || fut <= 0) return new List<(double, double)>();
            return perfil.Where(s => s != null && s.Fut > 0 && Math.Abs(s.Fut - fut) <= radio && Math.Abs(porVol ? s.GexVol : s.GexOi) > 0)
                         .OrderBy(s => s.K)
                         .OrderByDescending(s => Math.Abs(porVol ? s.GexVol : s.GexOi))
                         .Take(2).Select(s => (s.Fut, porVol ? s.GexVol : s.GexOi)).ToList();
        }

        /// <summary>D3 (3.7.0): una por lado como la clasica (la mas fuerte de cada lado; con empate tecnico gana la mas cercana entre las
        /// comparables; lado vacio = la mas fuerte que queda), en el STRIKE EXACTO (sin centroide 12) y con 2 plazas (sin tercera).
        /// Mismo codigo que GammaHoyNucleo.UnaPorLado. Devuelve [arriba, abajo].</summary>
        public static List<(double Fut, double Gex)> UnaPorLado(IEnumerable<GammaHoyNucleo.Strike> perfil, double fut, double radio, double empatePct, bool porVol, int plazas = 2)
        {
            var salida = new List<(double Fut, double Gex)>();
            if (perfil == null || fut <= 0) return salida;
            Func<GammaHoyNucleo.Strike, double> peso = x => Math.Abs(porVol ? x.GexVol : x.GexOi);
            var enRadio = perfil.Where(x => x != null && x.Fut > 0 && Math.Abs(x.Fut - fut) <= radio && peso(x) > 0).OrderBy(x => x.K).ToList();
            GammaHoyNucleo.Strike Elegir(IEnumerable<GammaHoyNucleo.Strike> lado)
            {
                var l = lado.ToList();
                if (l.Count == 0) return null;
                double pmax = l.Max(peso);
                double piso = pmax * (1.0 - Math.Max(0.0, Math.Min(90.0, empatePct)) / 100.0);
                return l.Where(x => peso(x) >= piso).OrderBy(x => Math.Abs(x.Fut - fut)).ThenByDescending(peso).First();
            }
            var arriba = Elegir(enRadio.Where(x => x.Fut > fut));
            var abajo = Elegir(enRadio.Where(x => x.Fut <= fut));
            if (arriba != null) salida.Add((arriba.Fut, porVol ? arriba.GexVol : arriba.GexOi));
            if (abajo != null) salida.Add((abajo.Fut, porVol ? abajo.GexVol : abajo.GexOi));
            foreach (var x in enRadio.OrderByDescending(peso))
            {
                if (salida.Count >= Math.Max(1, plazas)) break;
                if (salida.Any(s => s.Fut == x.Fut)) continue;
                salida.Add((x.Fut, porVol ? x.GexVol : x.GexOi));
            }
            return salida;
        }

        /// <summary>3.0.9: D1 = la mas CERCANA al precio, D2 la siguiente (los niveles no cambian, cambia el numero del rotulo).</summary>
        public static List<(double Fut, double Gex)> PorCercania(IEnumerable<(double Fut, double Gex)> doms, double fut)
            => doms == null ? new List<(double, double)>() : doms.OrderBy(d => Math.Abs(d.Fut - fut)).ToList();

        /// <summary>Muro de calls (mayor GEX de calls &gt; 0) y muro de puts (GEX de puts mas negativo) a &lt;= radio de ref0 (motor.py
        /// k_muro_calls / k_muro_puts; backtest_familia MUROS). porVol = volumen de hoy; si no, OI.</summary>
        public static (GammaHoyNucleo.Strike C, GammaHoyNucleo.Strike P) Muros(IEnumerable<GammaHoyNucleo.Strike> perfil, double ref0, double radio, bool porVol)
        {
            GammaHoyNucleo.Strike mc = null, mp = null; double bc = 0, bp = 0;
            if (perfil != null)
                foreach (var s in perfil)
                {
                    if (s == null || Math.Abs(s.Fut - ref0) > radio) continue;
                    double c = porVol ? s.GexVolC : s.GexOiC, p = porVol ? s.GexVolP : s.GexOiP;
                    if (c > bc) { bc = c; mc = s; }
                    if (p < bp) { bp = p; mp = s; }
                }
            return (mc, mp);
        }

        /// <summary>
        /// 3.7.1 (A4): M+ OI / M− OI de una CAPA de CBOE (NDX / QQQ) buscados SOLO a &lt;= radio de 'fut' (radio = min(2 %·F, tope), el de las
        /// dominantes) y SIN la serie que cierra en menos de 'minCierreMin' minutos. Antes eran los del perfil entero (GammaHoyNucleo.MpOi /
        /// MnOi): medido en la 3.7.0 (resumen37 / cara_a_cara) saltaban hasta 1.500 pts y quedaban en el borde a 1.148 pts. El horizonte es el
        /// del nucleo ('Hoy': dias &lt;= max(1, serie mas cercana + 0,01)) pero contando como 'mas cercana' la primera que NO cierra en &lt; 30 min;
        /// asi, cuando ninguna serie esta por cerrar, el resultado es el mismo strike que el perfil del nucleo filtrado al radio (control en
        /// Simulador37). GEX por OI de cada fila = GammaHoyNucleo.Gex (la misma cuenta, el mismo multiplicador, la misma tasa).
        /// Devuelve NaN del lado que no tenga ningun strike con GEX de ese signo en el radio.
        /// </summary>
        public static (double Mp, double Mn) MajorsOiCerca(Feed.Cadena c, GammaHoyNucleo.Lectura L, double fut, double radio, DateTime ahoraUtc, double tasa, double minCierreMin = 30)
        {
            if (c == null || L == null || c.Filas == null || c.Dias == null || c.Dias.Length == 0 || fut <= 0 || L.S <= 0) return (double.NaN, double.NaN);
            double env = GammaHoyNucleo.Envejecer(c, ahoraUtc);
            double piso = Math.Max(0, minCierreMin) / 1440.0;
            double masCerca = double.MaxValue;
            foreach (var d in c.Dias) { double dd = d - env; if (dd >= piso && dd < masCerca) masCerca = dd; }
            if (masCerca == double.MaxValue) return (double.NaN, double.NaN);
            double tope = Math.Max(1.0, masCerca + 0.01);
            double mult = c.Multiplicador > 0 ? c.Multiplicador : GammaHoyNucleo.MULT_INDICE;
            var por = new Dictionary<double, double>();
            foreach (var f in c.Filas)
            {
                if (f.V < 0 || f.V >= c.Dias.Length) continue;
                double dias = c.Dias[f.V] - env;
                if (dias < piso || dias > tope) continue;
                double g = GammaHoyNucleo.Gex(f, L.S, Math.Max(dias, GammaHoyNucleo.PISO_DIAS) / 365.0, tasa, false, c.EsFuturo, mult);
                if (g == 0 || double.IsNaN(g)) continue;
                por[f.K] = (por.TryGetValue(f.K, out var q) ? q : 0) + g;
            }
            double mp = double.NaN, mn = double.NaN, bp = 0, bn = 0;
            foreach (var kv in por.OrderBy(x => x.Key))
            {
                double pf = c.PorRazon ? c.AlFuturo(kv.Key) : kv.Key + L.Base;
                if (double.IsNaN(pf) || pf <= 0 || Math.Abs(pf - fut) > radio) continue;
                if (kv.Value > bp) { bp = kv.Value; mp = pf; }
                if (kv.Value < bn) { bn = kv.Value; mn = pf; }
            }
            return (mp, mn);
        }

        /// <summary>
        /// Los dos niveles de una formula F1..F8 (sin estado). C1: con el OI de NQ de ANTEAYER (oiFresco = false) las formulas por OI no
        /// existen (NaN) y F1 usa solo los cruces por volumen. C3: las listas de cruces del nucleo ya vienen sin islas.
        /// radioDom = tope de las dominantes en pts (100 en NQ); los muros van siempre a +-100 pts (como la pagina).
        /// </summary>
        public static (double A, double B) Formula(FormulaDoms3 f, GammaHoyNucleo.Lectura L, double fut, double ref0, double radioDom, bool oiFresco)
        {
            if (L?.Perfil == null || L.Perfil.Count == 0 || ref0 <= 0) return (double.NaN, double.NaN);
            bool porOi = f == FormulaDoms3.UnoPorLadoOi || f == FormulaDoms3.UnoPorLadoVolOi || f == FormulaDoms3.MurosOi || f == FormulaDoms3.MajorsOi;
            if (porOi && !oiFresco) return (double.NaN, double.NaN);
            if (f == FormulaDoms3.Cruces)
            {
                var zs = new List<double>();
                if (L.ZeroCrucesVolLista != null) zs.AddRange(L.ZeroCrucesVolLista);
                if (oiFresco && L.ZeroCrucesOiLista != null) zs.AddRange(L.ZeroCrucesOiLista);
                var c = Cerco(zs, ref0);
                return (c.Techo, c.Piso);
            }
            if (f == FormulaDoms3.MajorsOi) return (L.MpOi, L.MnOi);
            double radio = f == FormulaDoms3.MurosVol || f == FormulaDoms3.MurosOi ? 100.0 : RadioDominantes(fut, radioDom);
            switch (f)
            {
                case FormulaDoms3.MurosVol:
                case FormulaDoms3.MurosOi:
                {
                    var m = Muros(L.Perfil, ref0, radio, f == FormulaDoms3.MurosVol);
                    return (m.C?.Fut ?? double.NaN, m.P?.Fut ?? double.NaN);   // A = muro de calls, B = muro de puts (pueden estar del mismo lado)
                }
                case FormulaDoms3.DosMasGrandesVol:
                {
                    var dos = DosMasGrandes(L.Perfil, ref0, radio, true);
                    return (dos.Count > 0 ? dos[0].Fut : double.NaN, dos.Count > 1 ? dos[1].Fut : double.NaN);
                }
                default:
                {
                    Func<GammaHoyNucleo.Strike, double> F = f == FormulaDoms3.UnoPorLadoOi ? (s => Math.Abs(s.GexOi))
                        : f == FormulaDoms3.UnoPorLadoVolOi ? (s => Math.Abs(s.GexVol) + Math.Abs(s.GexOi)) : (Func<GammaHoyNucleo.Strike, double>)(s => Math.Abs(s.GexVol));
                    var cand = L.Perfil.Where(s => s.Fut > 0 && Math.Abs(s.Fut - ref0) <= radio).ToList();
                    var a = cand.Where(s => s.Fut > ref0 && F(s) > 0).OrderByDescending(F).FirstOrDefault();
                    var b = cand.Where(s => s.Fut < ref0 && F(s) > 0).OrderByDescending(F).FirstOrDefault();
                    return (a?.Fut ?? double.NaN, b?.Fut ?? double.NaN);
                }
            }
        }

        // ------------------------------------------------------------------ tunel por cruces (C2)

        /// <summary>Estado del tunel por cruces (techo y piso vigentes). Clonable: el pulso de 1 s evalua sobre una copia y no lo mueve.</summary>
        public sealed class EstadoTunel
        {
            public double Techo = double.NaN, Piso = double.NaN;
            public EstadoTunel Clon() => new EstadoTunel { Techo = Techo, Piso = Piso };
        }

        /// <summary>
        /// C2 (cod_02_tunel_rancio: 13,8 % de las rayas del tunel de la 3.0 NO eran ningun cruce vigente, rev_06: 31.287,11 sostenido
        /// 11 min sin cruce). Con histeresis y el cierre adentro: el techo (piso) SE MANTIENE solo si existe un cruce a &lt;= 1 pt de el, del
        /// mismo lado del cierre, y toma su valor ACTUAL; si no, se reelige ese lado (el cruce mas cercano arriba / abajo del cierre).
        /// Cierre afuera = se reeligen los dos. Gex de cada uno = la FUERZA real del cruce (|G izq| + |G der|), no +-1e6.
        /// Devuelve null si no hay cruces; 'reeligio' dice si algun lado se eligio de nuevo (para el log).
        /// </summary>
        public static List<(double Fut, double Gex)> TunelCruces(IReadOnlyList<Cruce> cruces, double ref0, bool histeresis, EstadoTunel st, out bool reeligio)
        {
            reeligio = false;
            if (cruces == null || cruces.Count == 0 || ref0 <= 0 || st == null) return null;
            Cruce? Cercano(double nivel, bool arriba)
            {
                Cruce? m = null; double d = 1.0 + 1e-9;
                foreach (var c in cruces)
                {
                    if (arriba ? c.Z <= ref0 : c.Z >= ref0) continue;
                    double x = Math.Abs(c.Z - nivel);
                    if (x <= d) { d = x; m = c; }
                }
                return m;
            }
            Cruce? MasCerca(bool arriba)
            {
                Cruce? m = null;
                foreach (var c in cruces)
                {
                    if (arriba ? c.Z <= ref0 : c.Z >= ref0) continue;
                    if (m == null || Math.Abs(c.Z - ref0) < Math.Abs(m.Value.Z - ref0)) m = c;
                }
                return m;
            }
            double t = double.NaN, p = double.NaN, ft = double.NaN, fp = double.NaN;
            bool adentro = histeresis && !double.IsNaN(st.Techo) && !double.IsNaN(st.Piso) && ref0 < st.Techo && ref0 > st.Piso;
            if (adentro)
            {
                var ct = Cercano(st.Techo, true); if (ct.HasValue) { t = ct.Value.Z; ft = ct.Value.Fuerza; }
                var cp = Cercano(st.Piso, false); if (cp.HasValue) { p = cp.Value.Z; fp = cp.Value.Fuerza; }
            }
            if (double.IsNaN(t)) { var c = MasCerca(true); if (c.HasValue) { t = c.Value.Z; ft = c.Value.Fuerza; } reeligio = true; }
            if (double.IsNaN(p)) { var c = MasCerca(false); if (c.HasValue) { p = c.Value.Z; fp = c.Value.Fuerza; } reeligio = true; }
            if (double.IsNaN(t) && double.IsNaN(p)) return null;
            st.Techo = t; st.Piso = p;
            var doms = new List<(double Fut, double Gex)>();
            if (!double.IsNaN(t)) doms.Add((t, ft));
            if (!double.IsNaN(p)) doms.Add((p, fp));
            return doms;
        }

        // ------------------------------------------------------------------ dibujo: radio por vela y rotulos del borde (D1)

        /// <summary>D1: una marca de estela en p se dibuja en su vela solo si |p - cierre de ESA vela| &lt;= radio (para la vela viva, el precio
        /// de ahora). Ya no hay atenuacion por la distancia al precio de AHORA (cod_12: 39,8 % de los guiones del 08-10 quedaban al 30 %).</summary>
        public static bool EnRadio(double p, double cierre, double radio)
            => !double.IsNaN(p) && p > 0 && !double.IsNaN(cierre) && cierre > 0 && Math.Abs(p - cierre) <= radio;

        /// <summary>Un nivel candidato a rotulo: libro (NQ / NDX / QQQ), tipo (D1, M+ OI, F5 muroC vol, cruce...), precio y si va SIEMPRE al borde.</summary>
        public struct Nivel37
        {
            public string Libro, Tipo; public double P; public bool Siempre; public int Indice;
        }

        /// <summary>D1: de los niveles a MAS del radio del precio de ahora, por libro el mas cercano arriba y el mas cercano abajo, mas los
        /// marcados 'Siempre' (M± OI de NQ, pedido 3.6.2 y D4). Sin repetidos. Orden: arriba primero (el mas cercano antes), despues abajo.</summary>
        public static List<Nivel37> FueraDeRadio(IEnumerable<Nivel37> niveles, double ref0, double radio)
        {
            var todos = (niveles ?? Enumerable.Empty<Nivel37>()).Where(n => !double.IsNaN(n.P) && n.P > 0 && Math.Abs(n.P - ref0) > radio).ToList();
            var elegidos = new List<Nivel37>();
            foreach (var g in todos.GroupBy(n => n.Libro ?? ""))
            {
                var arr = g.Where(n => n.P > ref0).OrderBy(n => n.P - ref0).Select(n => (Nivel37?)n).FirstOrDefault();
                var aba = g.Where(n => n.P < ref0).OrderBy(n => ref0 - n.P).Select(n => (Nivel37?)n).FirstOrDefault();
                if (arr.HasValue) elegidos.Add(arr.Value);
                if (aba.HasValue) elegidos.Add(aba.Value);
            }
            foreach (var n in todos.Where(n => n.Siempre)) if (!elegidos.Any(e => e.Indice == n.Indice)) elegidos.Add(n);
            return elegidos.Where(n => n.P > ref0).OrderBy(n => n.P - ref0).Concat(elegidos.Where(n => n.P < ref0).OrderBy(n => ref0 - n.P)).ToList();
        }

        /// <summary>El texto del rotulo del borde: flecha, libro, tipo y distancia con signo ("↑ NQ M+ OI +87").</summary>
        public static string TextoBorde(Nivel37 n, double ref0, CultureInfo es)
        {
            string tipo = (n.Tipo ?? "").Replace("▲", "").Replace("▼", "").Trim();
            return (n.P > ref0 ? "↑ " : "↓ ") + (string.IsNullOrEmpty(n.Libro) ? "" : n.Libro + " ") + tipo + " " + (n.P - ref0).ToString("+0;-0", es ?? CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------ CBOE: vigencia de la cadena (C6) y hora de NY

        private static readonly TimeZoneInfo _ny = ZonaNy();
        private static TimeZoneInfo ZonaNy()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
            catch { try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); } catch { return null; } }
        }

        /// <summary>UTC -&gt; hora de Nueva York (sin zona instalada: UTC-4).</summary>
        public static DateTime ANy(DateTime utc)
        {
            var u = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            try { return _ny != null ? TimeZoneInfo.ConvertTimeFromUtc(u, _ny) : u.AddHours(-4); } catch { return u.AddHours(-4); }
        }

        /// <summary>Hora de Nueva York -&gt; UTC.</summary>
        public static DateTime AUtc(DateTime ny)
        {
            var n = DateTime.SpecifyKind(ny, DateTimeKind.Unspecified);
            try { return _ny != null ? TimeZoneInfo.ConvertTimeToUtc(n, _ny) : DateTime.SpecifyKind(n.AddHours(4), DateTimeKind.Utc); }
            catch { return DateTime.SpecifyKind(n.AddHours(4), DateTimeKind.Utc); }
        }

        /// <summary>C6: la cadena de CBOE esta CONGELADA si su ultimo trade de opciones es de las 15:59 NY o despues (cierre).</summary>
        public static bool Congelada(DateTime ultimoTradeUtc)
            => ultimoTradeUtc != DateTime.MinValue && ANy(ultimoTradeUtc).TimeOfDay >= new TimeSpan(15, 59, 0);

        /// <summary>C6 (backtest_familia.vigencia_cboe): una cadena vale 25 min desde 'generado'; congelada, hasta las 13:30 UTC siguientes
        /// (el contenido no cambia de noche: cod_05_ndx_congelada). Antes vencia a los 25 min y la estela de la noche quedaba sin capas.</summary>
        public static DateTime VigenciaCboe(DateTime generadoUtc, DateTime ultimoTradeUtc, double vidaS = 1500)
        {
            var h1 = generadoUtc.AddSeconds(vidaS);
            if (Congelada(ultimoTradeUtc))
            {
                var prox = generadoUtc.Date.AddHours(13.5);
                if (prox <= generadoUtc) prox = prox.AddDays(1);
                if (prox > h1) h1 = prox;
            }
            return h1;
        }

        /// <summary>Vencimiento del trimestre de un codigo de futuro ("MNQZ6", "NQZ6", "MESU6"): tercer viernes del mes a las 9:30 NY (SOQ),
        /// en UTC. default si el codigo no se entiende. refUtc fija la decada del año de un digito.</summary>
        public static DateTime VencimientoTrimestralUtc(string codigo, DateTime refUtc)
        {
            if (string.IsNullOrEmpty(codigo) || codigo.Length < 2) return default(DateTime);
            string c = codigo.Trim().ToUpperInvariant();
            char dy = c[c.Length - 1], cm = c[c.Length - 2];
            if (!char.IsDigit(dy)) return default(DateTime);
            int mes = cm == 'H' ? 3 : cm == 'M' ? 6 : cm == 'U' ? 9 : cm == 'Z' ? 12 : 0;
            if (mes == 0) return default(DateTime);
            int y0 = refUtc.Year, anio = y0 - y0 % 10 + (dy - '0');
            if (anio < y0 - 1) anio += 10;
            var d = new DateTime(anio, mes, 1);
            while (d.DayOfWeek != DayOfWeek.Friday) d = d.AddDays(1);
            d = d.AddDays(14).AddHours(9.5);
            return AUtc(d);
        }

        /// <summary>Dia habil anterior (lunes -&gt; viernes; sin feriados: SUPUESTO).</summary>
        public static DateTime HabilAnterior(DateTime d)
        {
            var x = d.Date.AddDays(-1);
            while (x.DayOfWeek == DayOfWeek.Saturday || x.DayOfWeek == DayOfWeek.Sunday) x = x.AddDays(-1);
            return x;
        }

        // ------------------------------------------------------------------ estadistica

        /// <summary>Mediana como numpy (promedio de los dos del medio con cantidad par). NaN si vacio.</summary>
        public static double Mediana(IEnumerable<double> xs)
        {
            var o = (xs ?? Enumerable.Empty<double>()).Where(v => !double.IsNaN(v)).OrderBy(v => v).ToList();
            if (o.Count == 0) return double.NaN;
            int m = o.Count / 2;
            return o.Count % 2 == 1 ? o[m] : 0.5 * (o[m - 1] + o[m]);
        }

        /// <summary>backtest_familia.robusta: mediana de los que estan a &lt;= max(3 MAD, piso) de la mediana; (valor, cuantos quedaron).</summary>
        public static (double V, int N) Robusta(IEnumerable<double> xs, double piso)
        {
            var x = (xs ?? Enumerable.Empty<double>()).Where(v => !double.IsNaN(v)).ToList();
            if (x.Count == 0) return (double.NaN, 0);
            double med = Mediana(x);
            double mad = Mediana(x.Select(v => Math.Abs(v - med)));
            double tope = Math.Max(3.0 * mad, piso);
            var b = x.Where(v => Math.Abs(v - med) <= tope).ToList();
            return b.Count > 0 ? (Mediana(b), b.Count) : (med, x.Count);
        }
    }
}
