using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PythiaGexTres
{
    /// <summary>
    /// QUE SE ANOTA Y QUE SE ROTULA EN LA 3.7.0, SIN ATAS (08-10-2026, extraido para Simulador37).
    ///
    /// Hasta la primera version de la propuesta estas decisiones vivian dentro de los pintores del indicador (GammaHoyTres.cs Candidatos,
    /// GammaHoyTresCapas.cs RegistrarCapasVela / PintarCapas, GammaHoyTresFormula.cs NivelesTodas / PintarFormulas, GammaHoyTresMajorOi.cs,
    /// GammaHoyTres37.cs SeleccionarDoms y GammaHoyTresRayas.cs RotuloCorto), atadas a ATAS: el simulador tenia que copiarlas y podian
    /// bifurcarse. Aca quedan como funciones puras (entran lecturas del nucleo, precios y las casillas; salen niveles y textos), sin
    /// colores ni pixeles: el indicador las llama y pone el color y la posicion; Simulador37 compila ESTE archivo y llama lo mismo.
    /// Comportamiento identico al de la propuesta anterior (mismo orden, mismas condiciones), solo movido.
    /// 3.7.1 (08-10): D1 de NQ = la mayor (A3); PrepararCapa (A6 regla 2.0 + A4 M± OI cercanos); Rango / AgruparMarcas / VistaRotulos (A5:
    /// una marca y un rotulo por (libro, strike), tope por jerarquia despues de agrupar, renglon por capa con la edad y la fecha del OI);
    /// RotulosCapa sin la alarma (A1); la estela de capas no se corre (A2, CORRER_ESTELA_NDX = false).
    /// 3.7.2 (08-10): ElegirVela = la regla de dibujo de UNA vela (agrupado, radio 35, 'juntas' 3 pts, tope por jerarquia), la misma en la
    /// estela, la vela viva y los rotulos; Rango nuevo (cruces de las capas antes que sus M± OI); VistaRotulos con las flechas del borde solo
    /// en los lugares que sobran y la edad / fecha del OI en cada rotulo.
    /// 3.7.3 (08-10): radio 50 y 'juntas' 8 pts (revisor visual de la vuelta 2: las rayas de libros distintos a 3-8 pts se leian como una raya
    /// doble; con 8 no pueden existir); tope de RAYAS por vela aparte del tope de rotulos (VistaRotulos topeRayas); la edad del dato y la
    /// fecha del OI vuelven a un RENGLON por libro arriba de la columna (Cabeceras: 'NDX · QQQ · dato de hace 14 h · OI 07-10', 'NQ · OI 07-10'),
    /// antes de todos los numeros, y salen de cada rotulo (eran los rotulos de 54 caracteres que tapaban las ultimas velas).
    /// </summary>
    public static class Dibujo37
    {
        // ------------------------------------------------------------------ NQ: D1/D2 (D2 de la spec)

        /// <summary>D2: la regla de la 2.0 por volumen (Seleccion37.DosMasGrandes, radio min(2 %·F, tope)); solo si no hay volumen en el radio
        /// y el OI es de ayer (C1), por OI. libro = "vol" | "OI". 3.7.1 (A3): D1 = la de MAYOR |GEX| y D2 la segunda, como la 2.0 (la 3.7.0
        /// las ordenaba por cercania al precio: el mismo par de strikes, pero el rotulo 'D1' no decia la mas fuerte).</summary>
        public static List<(double Fut, double Gex)> DominantesNq(IEnumerable<GammaHoyNucleo.Strike> perfil, double fut, double radioTopePts, bool oiFresco, out string libro)
        {
            double radio = Seleccion37.RadioDominantes(fut, radioTopePts);
            var d = Seleccion37.DosMasGrandes(perfil, fut, radio, true);
            libro = "vol";
            if (d.Count == 0 && oiFresco) { d = Seleccion37.DosMasGrandes(perfil, fut, radio, false); libro = "OI"; }
            return d;
        }

        /// <summary>
        /// 3.7.1: lo que se elige en una CAPA de CBOE (NDX / QQQ) despues de la cuenta del nucleo, igual en el indicador (CargarCapa) y en
        /// Simulador37 (CalcularCapa).
        ///   A6: D1/D2 = la regla de la 2.0 (Seleccion37.DosMasGrandes: las dos barras de mayor |GEX vol| a &lt;= min(2 %·F, tope), strike
        ///       exacto; D1 = la mayor). La 3.7.0 usaba una por lado con empate 20 %: la de cada lado era la mas cercana entre las comparables,
        ///       se pegaba al precio y saltaba de lado (cara_a_cara: NDX D1/D2 15,0 rayas en el cuerpo cada 100 velas contra 8,1 de la 2.0).
        ///   A4: M+ OI / M− OI solo a &lt;= el mismo radio y sin la serie que cierra en &lt; 30 min (Seleccion37.MajorsOiCerca); se guardan en
        ///       L.MpOi / L.MnOi para que la estela, el archivo y los rotulos usen lo mismo.
        /// </summary>
        public static void PrepararCapa(GammaHoyNucleo.Lectura L, Feed.Cadena c, double fut, DateTime ahoraUtc, double radioTopePts, double tasa)
        {
            if (L == null || L.SinBase || L.Perfil == null || fut <= 0) return;
            double radio = Seleccion37.RadioDominantes(fut, radioTopePts);
            L.Doms = Seleccion37.DosMasGrandes(L.Perfil, fut, radio, true); L.LibroDom = "vol";
            var m = Seleccion37.MajorsOiCerca(c, L, fut, radio, ahoraUtc, tasa, 30);
            L.MpOi = m.Mp; L.MnOi = m.Mn;
        }

        // ------------------------------------------------------------------ 3.7.1 (A5): jerarquia, marcas agrupadas y rotulos agrupados

        /// <summary>
        /// La jerarquia de un nivel (menor = mas importante). Decide (1) que estilo lleva la UNICA marca de un grupo (libro, strike), (2) cual
        /// manda cuando dos rayas quedan a &lt;= 'juntas' pts (3.7.2) y (3) que entra con el tope (Tope373Rayas en la estela, Tope3Rotulos en los rotulos).
        /// Orden 3.7.2: NQ D1, NQ D2 (el libro vivo), NDX D1, QQQ D1 (la dominante de cada capa), M± OI de NQ (pedido 3.6.2: 'siempre'), muros F5
        /// de NQ, NDX D2, QQQ D2, techo/piso (cruces) de NDX, cruce de QQQ, M± OI de NDX, M± OI de QQQ, las otras formulas, cruce de NQ, M± por
        /// volumen. 3.7.1 tenia los cruces de las capas al final (11): con el tope por vela se cortaban primero y son los que cubren los giros
        /// (eleccion_372.md); los M± OI de las capas quedan detras (eran los bloques mas gruesos y no cubren giros: ablacion_371.md).
        /// </summary>
        public static int Rango(string libro, string tipo)
        {
            string t = (tipo ?? "").Replace("▲", "").Replace("▼", "").Trim();
            bool nq = !(string.Equals(libro, "NDX", StringComparison.OrdinalIgnoreCase) || string.Equals(libro, "QQQ", StringComparison.OrdinalIgnoreCase));
            bool ndx = string.Equals(libro, "NDX", StringComparison.OrdinalIgnoreCase);
            if (t.Contains("cruce") && !(t.Length > 1 && t[0] == 'F' && char.IsDigit(t[1]))) return nq ? 13 : ndx ? 8 : 9;
            if (t == "D1") return nq ? 0 : ndx ? 2 : 3;
            if (t == "D2") return nq ? 1 : ndx ? 6 : 7;
            if (t.EndsWith(" OI") && t.StartsWith("M")) return nq ? 4 : ndx ? 10 : 11;
            if (t.Length > 1 && t[0] == 'F' && char.IsDigit(t[1])) return t.StartsWith("F5") ? 5 : 12;
            if (t.EndsWith(" vol") && t.StartsWith("M")) return 14;
            return 20;
        }

        // ------------------------------------------------------------------ 3.7.2: que se DIBUJA en una vela (la estela y la vela viva, igual)

        /// <summary>Radio de dibujo por defecto (pts contra el cierre de cada vela; la viva contra el precio de ahora). 3.7.1: 50; 3.7.2: 35;
        /// 3.7.3: 50 (configuracion pedida por el orquestador; el radio 35 de la 3.7.2 salio de una grilla sobre la misma muestra).</summary>
        public const double RADIO_DIBUJO_DEFECTO = 50.0;
        /// <summary>Dos rayas de la misma vela a &lt;= esto (pts) se dibujan como UNA (la de mayor jerarquia); la otra va en su rotulo. 3.7.2: 3;
        /// 3.7.3: 8 = el borde de arriba de la 'raya doble' del revisor visual (dos libros a 3-8 pts: 47 % de las velas m1 en la 3.7.2, 20 % en
        /// la 2.0). Fijado antes de medir (eleccion_373.py), no por las metricas.</summary>
        public const double JUNTAS_DEFECTO = 8.0;
        /// <summary>3.7.3: tope de rayas dibujadas por vela por defecto (propiedad Tope373Rayas; en la 3.7.2 era Tope3Rotulos, que cambiaba de
        /// significado sin renombrarse).</summary>
        public const int TOPE_RAYAS_DEFECTO = 5;

        /// <summary>Un grupo (libro, strike) de una vela y lo que se decidio con el: Dib = se dibuja; Absorbido = quedo a &lt;= 'juntas' pts de
        /// un grupo de mayor jerarquia que si se dibuja (Dueno): no se dibuja aparte y va nombrado en el rotulo del Dueno; Cortado = dentro del
        /// radio pero pasado el tope; Dentro = a &lt;= radio de la referencia.</summary>
        public sealed class GrupoVela37
        {
            public string Libro; public double P; public int Rango, Orden;
            public List<Marca37> Miembros = new List<Marca37>();
            public bool Dentro, Dib, Absorbido, Cortado;
            public GrupoVela37 Dueno;
            public List<GrupoVela37> Junto = new List<GrupoVela37>();
            public Marca37 Primero => Miembros[0];
            public string Clave => Dibujo37.Clave(Libro, P);
        }

        /// <summary>
        /// 3.7.2: LA regla de dibujo de una vela, la misma en la estela (ref0 = cierre de esa vela), en la vela viva (ref0 = precio de ahora) y en
        /// los rotulos (VistaRotulos), en el indicador (PintarMarcasAgrupadas) y en Simulador37:
        ///   (1) una marca por (libro, strike) con el estilo del de mayor jerarquia (AgruparMarcas, 3.7.1);
        ///   (2) solo los grupos a &lt;= radio de ref0;
        ///   (3) en orden de jerarquia (Rango; a igual jerarquia el mas cercano a ref0): un grupo a &lt;= 'juntas' pts de uno ya elegido NO se
        ///       dibuja aparte (lo absorbe el de mayor jerarquia: pedido del revisor visual 08-10, rayas dobles NQ/NDX a 4-8 pts que cruzaban
        ///       juntas los cuerpos; con 3 pts la cobertura de giros no cambia, eleccion_372.md);
        ///   (4) a lo sumo 'tope' grupos dibujados (3.7.3: Tope373Rayas; en la 3.7.2 era Tope3Rotulos): en la vela viva el menor entre ese y el
        ///       tope de rotulos, asi cada raya dibujada de la vela viva tiene su rotulo.
        /// Devuelve TODOS los grupos (con Dib / Absorbido / Cortado), en el orden en que aparecio su primer miembro.
        /// </summary>
        public static List<GrupoVela37> ElegirVela(IEnumerable<Marca37> marcas, double ref0, double radio, double juntas, int tope)
        {
            var grupos = AgruparMarcas(marcas).Select((g, i) => new GrupoVela37 { Libro = g[0].Libro, P = g[0].P, Rango = Rango(g[0].Libro, g[0].Tipo), Miembros = g, Orden = i }).ToList();
            foreach (var q in grupos) q.Dentro = !double.IsNaN(ref0) && ref0 > 0 && Math.Abs(q.P - ref0) <= radio;
            var dentro = grupos.Where(q => q.Dentro).OrderBy(q => q.Rango).ThenBy(q => Math.Abs(q.P - ref0)).ThenBy(q => q.Orden).ToList();
            var dib = new List<GrupoVela37>();
            int t = Math.Max(1, tope);
            foreach (var q in dentro)
            {
                var dueno = juntas > 0 ? dib.FirstOrDefault(z => Math.Abs(z.P - q.P) <= juntas + 1e-9) : null;
                if (dueno != null) { q.Absorbido = true; q.Dueno = dueno; dueno.Junto.Add(q); continue; }
                if (dib.Count >= t) { q.Cortado = true; continue; }
                q.Dib = true; dib.Add(q);
            }
            return grupos;
        }

        /// <summary>El tipo corto para el rotulo agrupado: 'F5 muroP vol' -&gt; 'muroP', 'M− OI' -&gt; 'M−OI', 'cruce techo' -&gt; 'techo', 'F1 cruce' -&gt; 'F1'.</summary>
        public static string TipoCorto(string tipo)
        {
            string t = (tipo ?? "").Replace("▲", "").Replace("▼", "").Trim();
            if (t.Length > 1 && t[0] == 'F' && char.IsDigit(t[1]))
            {
                if (t.Contains("muroC")) return "muroC";
                if (t.Contains("muroP")) return "muroP";
                int sp = t.IndexOf(' '); return sp > 0 ? t.Substring(0, sp) : t;
            }
            if (t == "cruce techo") return "techo";
            if (t == "cruce piso") return "piso";
            if (t.StartsWith("M") && (t.EndsWith(" OI") || t.EndsWith(" vol"))) return t.Replace(" ", "");
            return t;
        }

        /// <summary>3.7.3: los tipos de un grupo en orden de jerarquia, cortos y sin repetir; 'muroC' y 'muroP' en el mismo strike van como
        /// 'muros' (rotulos mas angostos: el revisor visual de la vuelta 2 midio la columna tapando las ultimas velas).</summary>
        public static string TiposCortos(IEnumerable<string> tipos)
        {
            var l = (tipos ?? Enumerable.Empty<string>()).Select(TipoCorto).Distinct().ToList();
            int ic = l.IndexOf("muroC"), ip = l.IndexOf("muroP");
            if (ic >= 0 && ip >= 0) { l[Math.Min(ic, ip)] = "muros"; l.RemoveAt(Math.Max(ic, ip)); }
            return string.Join("·", l);
        }

        /// <summary>3.7.3: cuantos 'junto' se nombran con su tipo y distancia en un rotulo; el resto va como 'y N más'.</summary>
        public const int JUNTOS_EN_ROTULO = 1;

        /// <summary>La clave de agrupado: mismo libro y mismo strike (a 0,01 pt: los niveles de un libro salen del mismo strike convertido).</summary>
        public static string Clave(string libro, double p) => (libro ?? "") + "|" + Math.Round(p * 100.0).ToString("0", CultureInfo.InvariantCulture);

        /// <summary>Una marca de estela de una vela (sin color): libro, tipo, precio e Id del que llama (para volver a su estilo).</summary>
        public struct Marca37
        {
            public string Libro, Tipo; public double P; public int Id;
            public Marca37(string libro, string tipo, double p, int id = 0) { Libro = libro; Tipo = tipo; P = p; Id = id; }
        }

        /// <summary>3.7.1 (A5): las marcas de UNA vela agrupadas por (libro, strike): una sola marca por grupo, con el estilo del de mayor
        /// jerarquia (el primero de cada lista). Los grupos salen en el orden en que aparecio su primer miembro.</summary>
        public static List<List<Marca37>> AgruparMarcas(IEnumerable<Marca37> marcas)
        {
            var orden = new List<string>(); var g = new Dictionary<string, List<Marca37>>();
            foreach (var m in marcas ?? Enumerable.Empty<Marca37>())
            {
                if (double.IsNaN(m.P) || m.P <= 0) continue;
                var k = Clave(m.Libro, m.P);
                if (!g.TryGetValue(k, out var l)) { l = new List<Marca37>(); g[k] = l; orden.Add(k); }
                if (!l.Any(x => x.Tipo == m.Tipo)) l.Add(m);
            }
            return orden.Select(k => g[k].Select((m, i) => (m, i)).OrderBy(x => Rango(x.m.Libro, x.m.Tipo)).ThenBy(x => x.i).Select(x => x.m).ToList()).ToList();
        }

        /// <summary>Un rotulo agrupado (libro, strike) con sus miembros ordenados por jerarquia. 3.7.2: Edad = la edad del dato de la capa si pasa
        /// de 30 min ('14 h', va ANTES del numero); FechaOi = 'OI 07-10' si el grupo tiene un M± OI (NQ incluido); Junto = los grupos de la vela
        /// viva que quedaron a &lt;= 'juntas' pts y no se dibujan aparte (van nombrados con su distancia).</summary>
        public sealed class Grupo37
        {
            public string Libro; public double P; public int Rango; public bool Siempre, Dentro; public List<Rotulo37> Miembros = new List<Rotulo37>();
            public string Edad = "", FechaOi = "";
            public List<Grupo37> Junto = new List<Grupo37>();
            public Rotulo37 Primero => Miembros[0];
            public string Tipos => TiposCortos(Miembros.Select(m => m.Tipo));
            public bool TieneOi => Miembros.Any(m => m.Tipo != null && m.Tipo.StartsWith("M") && m.Tipo.EndsWith(" OI"));
            public string Clave => Dibujo37.Clave(Libro, P);
        }

        /// <summary>Lo que se rotula en la vela viva: los grupos de adentro (en orden de precio, de arriba abajo) y los del borde (arriba primero).
        /// 3.7.3: Cabeceras = un renglon por libro (o por libros con el mismo texto) ARRIBA de la columna con la edad del dato (&gt; 30 min) y la
        /// fecha del OI: 'NDX · QQQ · dato de hace 14 h · OI 07-10', 'NQ · OI 07-10'. (3.7.2: vacia, la edad y la fecha iban en cada rotulo.)</summary>
        public sealed class Vista37
        {
            public List<Grupo37> Dentro = new List<Grupo37>(), Borde = new List<Grupo37>();
            public List<(string Libro, string Texto)> Cabeceras = new List<(string, string)>();
            public int Candidatos, Grupos, Cortados;
        }

        /// <summary>"45 min" / "14 h" (la edad del dato de una capa, para su renglon).</summary>
        public static string Hace(double minutos)
        {
            if (double.IsNaN(minutos) || minutos < 0) return "?";
            if (minutos < 90) return ((int)Math.Round(minutos)).ToString(CultureInfo.InvariantCulture) + " min";
            return ((int)Math.Round(minutos / 60.0)).ToString(CultureInfo.InvariantCulture) + " h";
        }

        /// <summary>
        /// 3.7.3 (antes 3.7.2 / 3.7.1, A5). (1) los candidatos (niveles de AHORA) pasan por la MISMA regla que la estela, ElegirVela contra el
        /// precio de ahora: agrupado por (libro, strike) en UN rotulo ('NQ 31.200 D1·muroP·M−OI'), radio, 'juntas' (lo que queda a &lt;= 8 pts de
        /// uno de mayor jerarquia va dentro de su rotulo: 'NQ 31.250 D2 · NDX D1 −2,6') y tope por jerarquia (el menor entre el de rotulos y el de
        /// rayas por vela); (2) cada grupo dibujado tiene rotulo: las flechas del borde (Seleccion37.FueraDeRadio: por libro el mas cercano arriba
        /// y abajo, mas los 'Siempre') solo usan los lugares que SOBRAN del tope de rotulos; (3) 3.7.3: la edad del dato (&gt; 30 min) y la fecha
        /// del OI van en un RENGLON por libro arriba de la columna (Cabeceras), antes de todos los numeros, y no en cada rotulo. capas = libro -&gt;
        /// (edad del dato en min, 'OI dd-MM'); NQ entra con edad NaN y la fecha de su OI.
        /// </summary>
        public static Vista37 VistaRotulos(IList<Rotulo37> cand, double fut, double radio, int tope, IDictionary<string, (double EdadMin, string FechaOi)> capas, double juntas = JUNTAS_DEFECTO, int topeRayas = 0)
        {
            var v = new Vista37();
            if (cand == null || cand.Count == 0) return v;
            v.Candidatos = cand.Count;
            int t = Math.Max(1, tope);
            int tv = topeRayas > 0 ? Math.Min(t, topeRayas) : t;   // 3.7.3: en la vela viva se dibuja solo lo rotulado: el menor de los dos topes
            // la MISMA regla que la estela (ElegirVela: agrupado, radio, juntas, tope) sobre los niveles de ahora contra el precio de ahora
            var gv = ElegirVela(cand.Select((q, i) => new Marca37(q.Libro, q.Tipo, q.P, i)), fut, radio, juntas, tv);
            v.Grupos = gv.Count;
            var mapa = new Dictionary<GrupoVela37, Grupo37>();
            foreach (var x in gv)
            {
                var gr = new Grupo37 { Libro = x.Libro, P = x.P, Rango = x.Rango, Dentro = x.Dentro, Miembros = x.Miembros.Select(m => cand[m.Id]).ToList() };
                gr.Siempre = x.Miembros.Any(m => cand[m.Id].Siempre);
                if (capas != null && capas.TryGetValue(gr.Libro ?? "", out var info))
                {
                    if (!double.IsNaN(info.EdadMin) && info.EdadMin > 30) gr.Edad = Hace(info.EdadMin);
                    if (gr.TieneOi && !string.IsNullOrEmpty(info.FechaOi)) gr.FechaOi = info.FechaOi;
                }
                mapa[x] = gr;
            }
            foreach (var x in gv) if (x.Dib) mapa[x].Junto = x.Junto.Select(j => mapa[j]).ToList();
            var dentro = gv.Where(x => x.Dib).Select(x => mapa[x]).ToList();
            // las flechas del borde van DESPUES de las rayas dibujadas: solo ocupan los lugares que sobran del tope de rotulos
            var fuera = gv.Where(x => !x.Dentro).Select(x => mapa[x]).ToList();
            var niv = fuera.Select((x, i) => new Seleccion37.Nivel37 { Libro = x.Libro, Tipo = x.Tipos, P = x.P, Siempre = x.Siempre, Indice = i + 1 }).ToList();
            var borde = Seleccion37.FueraDeRadio(niv, fut, -1).Select(n => fuera[n.Indice - 1]).OrderBy(x => x.Rango).ThenBy(x => Math.Abs(x.P - fut)).ToList();
            int libres = Math.Max(0, t - dentro.Count);
            var quedan = new HashSet<Grupo37>(borde.Take(libres));
            v.Cortados = gv.Count(x => x.Cortado) + Math.Max(0, borde.Count - libres);
            v.Dentro = dentro.OrderByDescending(x => x.P).ToList();
            v.Borde = borde.Where(quedan.Contains).OrderByDescending(x => x.P > fut).ThenBy(x => Math.Abs(x.P - fut)).ToList();
            v.Cabeceras = Cabeceras(v.Dentro.Concat(v.Borde).ToList(), capas);   // 3.7.3: la edad y la fecha del OI, antes de los numeros
            return v;
        }

        /// <summary>
        /// 3.7.3: los renglones de arriba de la columna (A5, 'un renglon por capa'; revisor visual de la vuelta 2: la fecha del OI iba DESPUES
        /// del numero y los rotulos llegaban a 54 caracteres). Un libro entra si tiene algo rotulado (propio o 'junto') y: su dato tiene mas de
        /// 30 min ('dato de hace 14 h') o rotula un M± OI ('OI 07-10'). Libros con el MISMO texto van en un solo renglon ('NDX · QQQ · ...').
        /// Orden fijo: NQ (el libro vivo), NDX, QQQ (no salta cuando se mueve el precio). Sin nada que decir, ningun renglon.
        /// </summary>
        public static List<(string Libro, string Texto)> Cabeceras(List<Grupo37> rotulados, IDictionary<string, (double EdadMin, string FechaOi)> capas)
        {
            var r = new List<(string Libro, string Texto)>();
            if (rotulados == null || rotulados.Count == 0 || capas == null) return r;
            var orden = new List<string>(); var conOi = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Ver(Grupo37 g)
            {
                if (g == null || string.IsNullOrEmpty(g.Libro)) return;
                if (!orden.Contains(g.Libro, StringComparer.OrdinalIgnoreCase)) orden.Add(g.Libro);
                if (g.TieneOi) conOi.Add(g.Libro);
            }
            foreach (var g in rotulados) { Ver(g); foreach (var j in g.Junto) Ver(j); }
            orden = orden.OrderBy(OrdenLibro).ToList();   // orden FIJO (no salta cuando se mueve el precio): NQ, NDX, QQQ
            var textos = new List<(string Libro, string Resto)>();
            foreach (var l in orden)
            {
                if (!capas.TryGetValue(l, out var info)) continue;
                string edad = !double.IsNaN(info.EdadMin) && info.EdadMin > 30 ? "dato de hace " + Hace(info.EdadMin) : "";
                string oi = conOi.Contains(l) && !string.IsNullOrEmpty(info.FechaOi) ? info.FechaOi : "";
                string resto = string.Join(" · ", new[] { edad, oi }.Where(x => x != ""));
                if (resto != "") textos.Add((l, resto));
            }
            foreach (var grupo in textos.GroupBy(x => x.Resto))
                r.Add((grupo.First().Libro, string.Join(" · ", grupo.Select(x => x.Libro)) + " · " + grupo.Key));
            return r;
        }

        private static int OrdenLibro(string libro) => string.Equals(libro, "NDX", StringComparison.OrdinalIgnoreCase) ? 1 : string.Equals(libro, "QQQ", StringComparison.OrdinalIgnoreCase) ? 2 : 0;

        /// <summary>Las claves (libro, strike) que tienen rotulo en la vela viva: lo unico que se dibuja en ella (3.7.2: cada raya con su numero).</summary>
        public static HashSet<string> ClavesRotuladas(Vista37 v) => new HashSet<string>((v?.Dentro ?? new List<Grupo37>()).Select(x => x.Clave));

        /// <summary>'+2,6' / '-2,6' / '+0,0'. Se redondea ANTES de formatear: paridad_372 encontro '-+0,0' (.NET formatea -0,04 con la seccion
        /// positiva y le antepone el signo).</summary>
        private static string Distancia(double d, CultureInfo es)
        {
            double r = Math.Round(d, 1, MidpointRounding.AwayFromZero);
            if (r == 0) r = 0;   // tambien -0,0
            return r.ToString("+0.0;-0.0", es ?? CultureInfo.InvariantCulture);
        }

        /// <summary>3.7.3: el texto de un grupo de la columna: libro, precio al tick, tipos por jerarquia ('muros' = muroC y muroP) y lo que quedo
        /// junto (a &lt;= 8 pts): el de mayor jerarquia con su distancia y el resto contado ('NQ 31.400 D1·M+OI·muros · NDX D1 −5,7 · y 2 más',
        /// 'NDX 31.192,25 D1·M−OI'; un junto del mismo libro va sin repetir el libro: 'NQ 31.250 D1 · D2 +5,0'). La edad del dato y la fecha del OI
        /// van en el renglon de su libro arriba de la columna (Cabeceras), antes de todos los numeros (3.7.2 las ponia en cada rotulo: hasta 54
        /// caracteres; la 3.7.3 con todos los junto llegaba a 76).</summary>
        public static string TextoGrupo(Grupo37 gr, double tick, CultureInfo es)
        {
            var s = gr.Libro + " " + Precio(gr.P, tick, es) + " " + gr.Tipos;
            foreach (var j in gr.Junto.Take(JUNTOS_EN_ROTULO)) s += " · " + (j.Libro != gr.Libro ? j.Libro + " " : "") + j.Tipos + " " + Distancia(j.P - gr.P, es);
            if (gr.Junto.Count > JUNTOS_EN_ROTULO) s += " · y " + (gr.Junto.Count - JUNTOS_EN_ROTULO).ToString(CultureInfo.InvariantCulture) + " más";   // 3.7.3: rotulo angosto
            return s;
        }

        /// <summary>3.7.3: el texto de un grupo del borde: flecha, libro, tipos y distancia ('↑ NQ M+OI +87', '↓ NDX D1 −54'); la edad y la fecha
        /// del OI van en el renglon de su libro (Cabeceras).</summary>
        public static string TextoBordeGrupo(Grupo37 gr, double fut, CultureInfo es)
            => (gr.P > fut ? "↑ " : "↓ ") + gr.Libro + " " + gr.Tipos + " " + (gr.P - fut).ToString("+0;-0", es ?? CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ formulas F1..F8 (grupo '9. Formulas a juzgar')

        /// <summary>El orden de las formulas que se anotan por vela (2 niveles cada una). Indice 0 = F1 ... 4 = F5 ... 6 = F8.</summary>
        public static readonly FormulaDoms3[] FormulasJuzgar = { FormulaDoms3.Cruces, FormulaDoms3.UnoPorLadoVol, FormulaDoms3.UnoPorLadoOi, FormulaDoms3.UnoPorLadoVolOi, FormulaDoms3.MurosVol, FormulaDoms3.MurosOi, FormulaDoms3.DosMasGrandesVol };
        public static readonly string[][] NomFormula = { new[] { "F1 cruce", "F1 cruce" }, new[] { "F2 vol", "F2 vol" }, new[] { "F3 OI", "F3 OI" }, new[] { "F4 v+OI", "F4 v+OI" }, new[] { "F5 muroC vol", "F5 muroP vol" }, new[] { "F6 muroC OI", "F6 muroP OI" }, new[] { "F8 2.0", "F8 2.0" } };

        /// <summary>Todas las formulas aplanadas (2 por formula: [2i] y [2i+1]) con Seleccion37.Formula. ref0 = cierre de la vela (al anotar)
        /// o el ultimo cierre (vela viva). Una formula que tira queda en NaN (como NivelesTodas de la 3.6.1).</summary>
        public static double[] NivelesFormulas(GammaHoyNucleo.Lectura L, double fut, double ref0, double radioDomPts, bool oiFresco)
        {
            var r = new double[FormulasJuzgar.Length * 2];
            for (int i = 0; i < FormulasJuzgar.Length; i++)
            {
                (double a, double b) = (double.NaN, double.NaN);
                try { (a, b) = Seleccion37.Formula(FormulasJuzgar[i], L, fut, ref0, radioDomPts, oiFresco); } catch { }
                r[2 * i] = a; r[2 * i + 1] = b;
            }
            return r;
        }

        // ------------------------------------------------------------------ capas de CBOE (NDX / QQQ)

        /// <summary>Lo que se anota de una capa en la vela que cierra (RegistrarCapasVela): [D1, D2, cruce mas cercano, base usada (NaN en
        /// QQQ), techo, piso]; techo/piso = Seleccion37.Cerco de los cruces por volumen SIN ISLAS del perfil entero contra el CIERRE de esa vela.</summary>
        public static double[] RegistroCapa(GammaHoyNucleo.Lectura L, double cierre, bool porRazon)
        {
            var cer = Seleccion37.Cerco(Seleccion37.CrucesPerfil(L.Perfil, true, true).Select(c => c.Z), cierre > 0 ? cierre : L.Futuro);
            return new[] { L.Doms.Count > 0 ? L.Doms[0].Fut : double.NaN, L.Doms.Count > 1 ? L.Doms[1].Fut : double.NaN, L.ZeroVol, porRazon ? double.NaN : L.Base, cer.Techo, cer.Piso };
        }

        /// <summary>3.6.6: la estela de NDX se corria a la base de AHORA (base de ahora - base con que se anoto el punto). 3.7.1 (A2): YA NO SE
        /// CORRE NINGUNA marca de capa (ni D1/D2 ni M± OI): cada marca queda donde se anoto con la base de ese momento, que es exactamente lo
        /// que juzga la auditoria (cara_a_cara: 'NDX va en la posicion que tenia en ese momento'). Con la base de dia acumulada (A2) la base
        /// ya no salta en la rueda. Se deja la funcion para el registro; el indicador pasa 0.</summary>
        public static double Corrimiento(double[] e, double baseAhora)
            => e != null && e.Length > 3 && !double.IsNaN(e[3]) && !double.IsNaN(baseAhora) ? baseAhora - e[3] : 0;
        public const bool CORRER_ESTELA_NDX = false;

        /// <summary>
        /// Los puntos de la estela de una capa que pasan las casillas (PintarCapas): (indice en e, tipo 0 = D1 / 1 = D2 / 2 = cruce, precio
        /// ya corrido). Con el cerco (NDX con 'techo y piso' prendido y el registro de 6) el cruce mas cercano [2] no se dibuja si hay techo o
        /// piso; techo [4] y piso [5] se dibujan como el cruce. El radio de la vela lo aplica quien llama (Seleccion37.EnRadio).
        /// </summary>
        public static List<(int Indice, int Tipo, double P)> PuntosCapa(double[] e, bool cercoCapa, bool zeroRombos, bool rayaD1, bool rayaD2, bool rayaZero, double corr)
        {
            var r = new List<(int, int, double)>();
            if (e == null) return r;
            bool cerco = cercoCapa && e.Length > 5;   // 3.6.8
            for (int i0 = 0; i0 < 6 && i0 < e.Length; i0++)
            {
                if (i0 == 3) continue;                                     // [3] = base, no es un nivel
                if (i0 == 2 && (!zeroRombos || (cerco && (!double.IsNaN(e[4]) || !double.IsNaN(e[5]))))) continue;   // con techo/piso no hace falta el mas cercano
                if (i0 >= 4 && (!cerco || !zeroRombos)) continue;
                int i = i0 >= 4 ? 2 : i0;                                  // techo y piso se dibujan como el cruce
                double p = e[i0]; if (double.IsNaN(p) || p <= 0) continue;
                p += corr;   // 3.6.6
                if (i == 0 ? !rayaD1 : i == 1 ? !rayaD2 : !rayaZero) continue;   // 3.5.0: una por una
                r.Add((i0, i, p));
            }
            return r;
        }

        // ------------------------------------------------------------------ rotulos (candidatos neutros: sin color ni posicion)

        /// <summary>Un rotulo candidato. Clase dice de que color va (la pone el indicador): "D" (dominante NQ), "cruce" (zero NQ), "M+" / "M-"
        /// (majors NQ por volumen u OI), "capa" (D1/D2/cruces de NDX o QQQ, color de la capa), "F0".."F6" (formula i), "capaM+" / "capaM-"
        /// (majors por OI de una capa). Libro y Tipo son los del rotulo del borde (D1); Siempre = va al borde aunque haya uno mas cercano.</summary>
        public struct Rotulo37
        {
            public string Nombre, Libro, Tipo, Sufijo, Extra, Clase; public double P; public bool Chico, Siempre;
            /// <summary>3.7.1: lo pone quien llama (el indicador: el indice de su lista con colores) para volver del grupo a su estilo.</summary>
            public int Id;
            public Rotulo37(string nombre, double p, string clase, bool chico, string sufijo, string libro, string tipo, string extra = "", bool siempre = false)
            { Nombre = nombre; P = p; Clase = clase; Chico = chico; Sufijo = sufijo ?? ""; Libro = libro; Tipo = tipo; Extra = extra ?? ""; Siempre = siempre; }
        }

        private static string Flecha(double p, double fut) => p >= fut ? "▲" : "▼";

        /// <summary>Los rotulos de NQ en orden de jerarquia (Candidatos de GammaHoyTres.cs): D1, D2, cruce (si prendido), M+/M- vol (si hay
        /// majors), M+/M- OI solo con el OI de ayer (C1) y siempre al borde (D4).</summary>
        public static List<Rotulo37> CandidatosNq(string raiz, GammaHoyNucleo.Lectura L, double fut, bool rayaD1, bool rayaD2, bool rayaCruce, bool majors,
                                                  bool rayaMas, bool rayaMenos, bool oiFresco, bool rayaMasOi, bool rayaMenosOi)
        {
            var c = new List<Rotulo37>();
            if (L == null) return c;
            if (rayaD1 && L.Doms.Count > 0 && L.Doms[0].Fut > 0) c.Add(new Rotulo37(raiz + " D1 " + Flecha(L.Doms[0].Fut, fut), L.Doms[0].Fut, "D", false, "", raiz, "D1"));
            if (rayaD2 && L.Doms.Count > 1 && L.Doms[1].Fut > 0) c.Add(new Rotulo37(raiz + " D2 " + Flecha(L.Doms[1].Fut, fut), L.Doms[1].Fut, "D", false, "", raiz, "D2"));
            if (rayaCruce && !double.IsNaN(L.ZeroVol) && L.ZeroVol > 0) c.Add(new Rotulo37(raiz + " cruce", L.ZeroVol, "cruce", false, L.ZeroCrucesVol > 1 ? " ·" + L.ZeroCrucesVol + " cruces" : "", raiz, "cruce"));   // 3.7.0 C8
            if (majors)
            {
                if (rayaMas && !double.IsNaN(L.MpVol) && L.MpVol > 0) c.Add(new Rotulo37(raiz + " M+ vol", L.MpVol, "M+", false, "", raiz, "M+ vol"));
                if (rayaMenos && !double.IsNaN(L.MnVol) && L.MnVol > 0) c.Add(new Rotulo37(raiz + " M− vol", L.MnVol, "M-", false, "", raiz, "M− vol"));
            }
            if (oiFresco)   // 3.7.0 C1
            {
                if (rayaMasOi && !double.IsNaN(L.MpOi) && L.MpOi > 0) c.Add(new Rotulo37(raiz + " M+ OI", L.MpOi, "M+", false, "", raiz, "M+ OI", "", true));
                if (rayaMenosOi && !double.IsNaN(L.MnOi) && L.MnOi > 0) c.Add(new Rotulo37(raiz + " M− OI", L.MnOi, "M-", false, "", raiz, "M− OI", "", true));
            }
            return c;
        }

        /// <summary>Los rotulos de una capa (PintarCapas): D1/D2 (si prendidas; 'ocultarD' = coincide con D1/D2 de NQ y no son independientes),
        /// y el cruce: techo y piso contra el precio de AHORA (NDX con cerco) o el mas cercano (QQQ). edad = ' ·17m'. 3.7.1 (A1): la alarma de
        /// la familia ya no va en los rotulos (solo log y recuadro, y sostenida 10 min: AlarmaFamilia37).</summary>
        public static List<Rotulo37> RotulosCapa(string nombre, GammaHoyNucleo.Lectura L, double fut, string edad, bool rayaD1, bool rayaD2,
                                                 bool zeroRombos, bool rayaZero, bool cerco, Func<double, bool> ocultarD = null)
        {
            var r = new List<Rotulo37>();
            if (L == null) return r;
            for (int i = 0; i < Math.Min(2, L.Doms.Count); i++)
            {
                double p = L.Doms[i].Fut; if (p <= 0) continue;
                if (i == 0 ? !rayaD1 : !rayaD2) continue;
                if (ocultarD != null && ocultarD(p)) continue;
                r.Add(new Rotulo37(nombre + " D" + (i + 1) + " " + Flecha(p, fut), p, "capa", true, edad, nombre, "D" + (i + 1)));
            }
            if (zeroRombos && rayaZero && cerco)   // 3.6.8: techo y piso del cruce (3.7.0: sin islas, 'cruce')
            {
                var cer = Seleccion37.Cerco(Seleccion37.CrucesPerfil(L.Perfil, true, true).Select(c => c.Z), fut);
                if (!double.IsNaN(cer.Techo)) r.Add(new Rotulo37(nombre + " cruce techo", cer.Techo, "capa", true, edad, nombre, "cruce techo"));
                if (!double.IsNaN(cer.Piso)) r.Add(new Rotulo37(nombre + " cruce piso", cer.Piso, "capa", true, edad, nombre, "cruce piso"));
            }
            else if (zeroRombos && rayaZero && !double.IsNaN(L.ZeroVol) && L.ZeroVol > 0)
                r.Add(new Rotulo37(nombre + " cruce", L.ZeroVol, "capa", true, edad, nombre, "cruce"));   // 3.7.0 C8
            return r;
        }

        /// <summary>Los rotulos de las formulas visibles con los niveles de ahora (PintarFormulas): el segundo nivel de una formula con los dos
        /// nombres iguales no se repite si coincide con el primero.</summary>
        public static List<Rotulo37> RotulosFormulas(double[] ahora, Func<int, bool> visible, string raiz)
        {
            var r = new List<Rotulo37>();
            if (ahora == null) return r;
            for (int i = 0; i < FormulasJuzgar.Length; i++)
            {
                if (visible != null && !visible(i)) continue;
                for (int k = 0; k < 2; k++)
                {
                    int j = 2 * i + k; if (j >= ahora.Length) continue;
                    double p = ahora[j];
                    if (double.IsNaN(p) || p <= 0) continue;
                    if (k == 1 && NomFormula[i][0] == NomFormula[i][1] && Math.Abs(p - ahora[2 * i]) < 0.01) continue;
                    r.Add(new Rotulo37(NomFormula[i][k], p, "F" + i, true, "", raiz, NomFormula[i][k]));
                }
            }
            return r;
        }

        /// <summary>Los rotulos M+ OI / M− OI de una capa (PintarMajorOi) con su edad y la fecha de su OI ('OI 07-10', '?' si es por reloj).</summary>
        public static List<Rotulo37> RotulosMajorOiCapa(string nombre, double[] ahora, string edad, string fechaOi, bool mas, bool menos)
        {
            var r = new List<Rotulo37>();
            if (ahora == null) return r;
            for (int i = 0; i < 2 && i < ahora.Length; i++)
            {
                if (i == 0 ? !mas : !menos) continue;
                double p = ahora[i];
                if (double.IsNaN(p) || p <= 0) continue;
                r.Add(new Rotulo37(nombre + (i == 0 ? " M+ OI" : " M− OI"), p, i == 0 ? "capaM+" : "capaM-", true, edad, nombre, i == 0 ? "M+ OI" : "M− OI", fechaOi));
            }
            return r;
        }

        /// <summary>D1: a &lt;= radio del precio de ahora van como rotulo normal; los demas al borde (Seleccion37.FueraDeRadio: por libro el mas
        /// cercano arriba y abajo, mas los 'Siempre'). Devuelve los de adentro (en el orden de entrada) y los del borde (arriba primero).</summary>
        public static (List<Rotulo37> Dentro, List<Rotulo37> Borde) Repartir(IList<Rotulo37> cand, double fut, double radio)
        {
            var dentro = cand.Where(q => Math.Abs(q.P - fut) <= radio).ToList();
            var fuera = cand.Where(q => Math.Abs(q.P - fut) > radio).ToList();
            var niv = fuera.Select((q, i) => new Seleccion37.Nivel37 { Libro = q.Libro, Tipo = q.Tipo, P = q.P, Siempre = q.Siempre, Indice = i + 1 }).ToList();
            var borde = Seleccion37.FueraDeRadio(niv, fut, -1).Select(n => fuera[n.Indice - 1]).ToList();
            return (dentro, borde);
        }

        // ------------------------------------------------------------------ textos

        /// <summary>3.5.5: el rotulo corto. 'NQ D2 ▼' -&gt; 'NQ D2'; edad ' ·422m' -&gt; ' ·7h' solo si &gt; 30 min; ' ·21 ctos' -&gt; ' ·21'.</summary>
        public static string RotuloCorto(string nombre, string precio, string sufijo)
        {
            string n = (nombre ?? "").Replace("▲", "").Replace("▼", "").Trim();
            while (n.Contains("  ")) n = n.Replace("  ", " ");
            string s = "";
            var m = System.Text.RegularExpressions.Regex.Match(sufijo ?? "", @"·\s*(\d+)\s*m\b");
            if (m.Success) { int min = int.Parse(m.Groups[1].Value); if (min > 30) s = " ·" + (min >= 90 ? (min / 60.0).ToString("0", CultureInfo.InvariantCulture) + "h" : min + "m"); }
            var c = System.Text.RegularExpressions.Regex.Match(sufijo ?? "", @"·\s*([\d.,]+)\s*ctos");
            if (c.Success) s = " ·" + c.Groups[1].Value;
            return n + " " + precio + s;
        }

        /// <summary>Precio redondeado al tick, con separador de miles: 31.545 / 6.745,25.</summary>
        public static string Precio(double p, double tick, CultureInfo es)
        {
            if (tick > 0) p = Math.Round(p / tick) * tick;
            return Math.Abs(p - Math.Round(p)) < 1e-9 || tick >= 1 ? p.ToString("N0", es) : p.ToString("N2", es);
        }

        /// <summary>El texto de un rotulo de adentro con 'Rotulos cortos' (Columna): 'NQ D2 31.381,50' + edad + ' ·' extra.</summary>
        public static string TextoCorto(Rotulo37 q, double tick, CultureInfo es)
        {
            string t = RotuloCorto(q.Nombre, Precio(q.P, tick, es), q.Sufijo);
            return string.IsNullOrEmpty(q.Extra) ? t : t + " ·" + q.Extra;
        }

        /// <summary>El texto de un rotulo del borde (PintarBorde37): flecha, libro, tipo y distancia ('↑ NQ M+ OI +87'), la edad si pasa de
        /// 30 min y el extra (fecha del OI / alarma de base).</summary>
        public static string TextoBorde(Rotulo37 q, double fut, CultureInfo es)
        {
            var n = new Seleccion37.Nivel37 { Libro = q.Libro, Tipo = q.Tipo, P = q.P, Siempre = q.Siempre };
            string edad = RotuloCorto("", "", q.Sufijo).Trim();
            return Seleccion37.TextoBorde(n, fut, es) + (edad == "" ? "" : " " + edad) + (string.IsNullOrEmpty(q.Extra) ? "" : " ·" + q.Extra);
        }
    }
}
