// ClasicaNdx.cs — PythiaGex 4.1.5 / 4.1.5b / 4.1.5d (09-10-2026), modulo clasica. Pedido del operador (09-10, ~15:50 ART): "analiza como la version clasica hizo esta
// estela (los puntitos 'NDX 0Γ') ... 14:10 y 14:18, 31067 31068 aprox acertaron mucho mejor que nosotros ... y copiarla ahora para nuestro 4.0".
// UNA serie NUEVA, R10_NDX_zero "Clasica NDX 0Γ", calculada ADENTRO (no se lee nada de la clasica: ni su log, ni base-rueda-NQ.json, ni sus cadenas
// ni sus estelas; independencia total). Fuera de la cuenta de la vista previa: CONF y FAM no la ven; va en RegistroMinuto.Extra como las R20.
//
// La receta (medida en laboratorio/calibracion_1009/receta_clasica: paridad 910/910 contra los AUDIT capa=NDX de la clasica del 09-10):
//   * el zero gamma POR VOLUMEN del horizonte Hoy de la cadena _NDX de CBOE (GammaHoyNucleo.cs Cruce, port 1:1 en CruceClasica):
//       S = fut - base;  x_i = S(1-0,03) + S x 0,06 x i / 60, i = 0..60 (61 precios, paso ~31 pts de NDX);
//       G(x) = sum_filas (g(x,K,T,ivC) volC - g(x,K,T,ivP) volP) x 100 x x^2 x 0,01   (Black-Scholes r 0,0375 sin dividendo, T = max(dias - env, 1/1440)/365)
//       el PRIMER cambio de signo recorriendo la grilla de abajo hacia arriba ((G_{i-1} < 0 y G_i >= 0) o (G_{i-1} > 0 y G_i <= 0)), interpolado;
//       NO es el cruce mas cercano al precio (eso es ZEST de la 4.1, grilla de 2,5 pts) ni ZeroPorSigno (2.0/3.0);
//   * se lleva al futuro sumando SU base (GammaHoyNucleo.CalcularAdentro, la cascada de la clasica, cada una acotada por el carry
//       |b - carry| <= max(0,6 |carry|, 0,0006 fut), carry = fut x (0,0375 - 0,008) x dias al vencimiento del futuro / 365):
//       1. "medida" y 2. "medida reciente": las cadenas de CBOE (las de la clasica y las de la 4.1) traen base = null y base_confiable = false
//          (medido: 910 de 910 AUDIT del 09-10; 477 + 369 cadenas de la 4.1 del 08 y 09-10): no se usan;
//       3. "de la rueda": la base que la clasica mide en la rueda (MedirBaseRueda, GammaHoy.cs:1418-1485) si tiene <= 24 h;
//       4. CRUDA (la de forwards de la cadena); 5. TEORICA (el carry); 6. la cruda sin cota.
//   * MedirBaseRueda (ReglaBaseClasica.Paso, port 1:1): una muestra por cada sello NUEVO de CBOE con 13:50 <= sello <= 20:10 UTC y la cadena de
//     <= 30 min: muestra = cierre de la vela DEL GRAFICO que contiene (sello - 960 s) - spot del indice; las ultimas 24; mediana alta; MAD;
//     tope = max(3 MAD, 0,0002 x precio); "buenas" = a <= tope de la mediana; con >= 3 buenas la mediana de las buenas; con >= 5 (o sin base) se
//     adopta y se anota la hora (la base vale 24 h desde la ultima adopcion).
//
// Lo que la clasica hizo el 09-10 (MEDIDO en su log, pythiagex-gammahoy.log; corregido en la 4.1.5d: antes aca decia que "no midio la rueda de hoy"):
// MedirBaseRueda SOLO mide con una primaria de indice de CBOE. Hasta las 19:32 UTC su primaria fue el libro de Rithmic: no midio (salvo una muestra
// suelta a las 16:48:30 UTC) y dibujo con la base de la rueda ANTERIOR (243,06, medida el 08-10 a las 20:12 UTC). Desde las 19:32 UTC (16:32 ART),
// despues de varios reinicios de ATAS, su primaria cayo a CBOE y SI midio la rueda de hoy, con velas de 1 min y la lista de 24 vacia en cada reinicio:
// adopto 229,88 (sello 19:47:37 UTC), 232,26 (20:02:37), 230,23 (20:04:37) y 230,16 (20:09:38; base-rueda-NQ.json 20:11:03Z). Esta replica supone
// la primaria en Rithmic (CongelarEnLaRueda: la base del dia anterior durante la rueda) y NO puede saber cuando la clasica cae a CBOE sin leer su
// estado (regla del proyecto: independencia): ese 09-10 entre las 19:48 y las 20:11 UTC el 0Γ y las D1-D3 de la replica quedaron ~13 pts corridos de
// los de la clasica (y en varios minutos con otro strike ganador). Lo dicen la casilla, el catalogo y la pestaña. Los 13-15 pts de diferencia con la
// ZEST de la 4.1 en la rueda salen ENTEROS de esa base vieja (con la de hoy, ~228, la formula da lo mismo que ZEST_NDX_vol: mediana -0,14 pt).
// Esta replica lo hace con regla fija y con los datos de la 4.1:
//   * la base "de la rueda" se calcula con la regla de la clasica sobre las cadenas _NDX que bajo la 4.1 (PythiaGex4\cboe) y la cinta del MNQ de la
//     4.1 (la vela del grafico: VelaMin, 2 min por defecto = el marco del grafico donde la clasica midio el 08-10, ver OpcionesClasicaNdx.VelaMin);
//   * DURANTE la ventana de la rueda de un dia (13:50-20:11 UTC) se usa CONGELADA la base con que ese dia empezo (la de la rueda anterior), como la
//     clasica con su primaria en Rithmic (el 09-10 hasta las 19:32 UTC); fuera de la ventana, el estado de la regla a esa hora (desde las 20:11 UTC la de la rueda que termino);
//   * con mas de 24 h (lunes: la del viernes) la clasica pasa a la CRUDA de cada cadena y despues al carry: igual aca.
// Diferencias con la clasica que NO se pueden evitar (dichas, no escondidas):
//   * una cuenta por minuto (al empezar el minuto, con el ultimo precio del MNQ y la ultima cadena bajada hasta ahi); la clasica recalcula cada 5 s
//     y pone hasta 24 puntitos por vela; la historia de la 4.1 es por vela m2;
//   * la cadena es la que bajo la 4.1 (otra bajada que cboe-local: de dia el sello puede ser otro; de noche el dato es el mismo);
//   * la clasica muestrea solo los sellos que le llegan en su bajada de cada 60 s (el 08-10 se salteo el de las 19:33:54); aca entran todos los de
//     la 4.1; y la vela es la de la cinta de la 4.1 (el grafico de la clasica es el de ATAS: con la cinta completa, el mismo cierre);
//   * la lista de 24 muestras arranca vacia cada dia (la clasica la arrastra mientras ATAS no se reinicie): con >= 24 muestras en el dia, igual;
//   * la vela tiene que estar en la cinta de la 4.1 (sin buscar la anterior): el 08-10 la cinta tiene un hueco de 17:49 a ~18:15 UTC (Rithmic caido) y
//     esas 15 muestras no entran (ninguna esta entre las ultimas 24);
//   * de noche: si la clasica NO midio la rueda (primaria Rithmic todo el dia), a las 24 h de su ultima adopcion pasa a la CRUDA de cada cadena y esta
//     replica, desde las 20:11 UTC, usa la base de la rueda de hoy de su regla: DE NOCHE NO COINCIDEN. Si la midio (el 09-10 desde las 19:32 UTC) usa la
//     suya (230,16, de las 11 muestras desde su ultimo reinicio) y esta replica la de su regla con TODAS las muestras del dia (229,32 con las velas del
//     grafico en la cinta; 229,98 con la cinta solo de ticks): ~0,2-0,8 pt (medido en la revision de la 4.1.5b/c: desde las 20:36 UTC del 09-10, -0,18 exacto contra su AUDIT);
//   * la vela: VelaMin = 2 (el grafico de 2 min de la clasica el 08-10); el 09-10 la clasica corrio en un grafico de 1 min (sus velas 19:27, 19:28, ...;
//     y el .ws "MNQ liviano" la tiene en M1). Con VelaMin = 1 la rueda del 09-10 da 230,23 (cinta solo de ticks) y la del 08-10 242,45 en vez de los
//     243,06 que la clasica dibujo todo el 09-10: un solo marco no replica los dos dias; queda 2 y la pestaña dice con que vela se mide;
//   * 4.1.5d: las muestras de la regla se GUARDAN (OpcionesClasicaNdx.CarpetaDatos, familia/muestras-clasica-NDX-<dia del sello>.jsonl, como las de C7)
//     la primera vez que se miden con una vela COMPLETA (balde de 2 min escuchado entero o la vela del grafico: OpcionesClasicaNdx.VelaCompleta); al
//     reiniciar se usan las guardadas y la rueda ya cerrada no cambia segun como arranque la cinta (el 09-10 a las 17:35 ART, con la cinta sin las velas
//     del grafico, la de la rueda del 09-10 paso de 229,32 a 229,98 con las mismas 296 muestras). Una muestra con una vela de ticks INCOMPLETA queda
//     provisional (no se guarda) y se vuelve a mirar cada SondeoCadaS como las que no tienen vela.
// Medido (arnes atas/_test_cuatro_paridad/clasica, 09-10): la regla con las 92 muestras del log de la clasica da sus 92 medianas y termina en 243,06;
// con las cadenas y la cinta de la 4.1 SOLAS la base de la rueda del 08-10 sale 243,06 (las mismas ultimas 24 muestras); el zero con la misma cadena,
// fut, base y hora que cada AUDIT: 910 de 910 a 0,05 pt; todo 4.1: base igual en 910 de 910, zero igual en 746, y los 164 restantes son la cadena
// (la 4.1 tenia otro sello de CBOE en ese instante: con la cadena de la clasica, iguales los 164).
// E/S: el Log y, solo con OpcionesClasicaNdx.CarpetaDatos (produccion), el archivo de las muestras (MuestrasClasica.cs). Lo llama el hilo del motor
// (IExtrasMinuto); el arnes lo llama directo (publico y puro donde se puede; sin CarpetaDatos no toca el disco).
// 4.1.5d (09-10-2026, revision de la 4.1.5b/c): las D1-D3 se calculan aunque no haya cruce del zero (la clasica las calcula igual); si ningun strike
// del radio tiene volumen y las dominantes salen del interes abierto, el texto de la fuente lo dice ("dominantes por OI") y la etiqueta lleva OI.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PythiaGexCuatro.Familia
{
    /// <summary>El zero de la clasica: GammaHoyNucleo.Cruce (PythiaGexNiveles) port 1:1, sobre una cadena de CBOE de la 4.1.</summary>
    public static class CruceClasica
    {
        public const int PASOS = 60;                     // 61 precios
        public const double AMP = 0.03;                  // +-3 % de S (x Apalancamiento, que en NDX es 1)

        /// <summary>Feed.Parsear de la clasica lee un null del json como 0 (la 4.1 lo guarda NaN).</summary>
        private static double Z(double x) => double.IsNaN(x) ? 0.0 : x;

        /// <summary>El zero por volumen (porVolumen) o por OI en el EJE DEL INDICE (NaN si no hay cruce en +-3 %). La cadena se envejece desde su
        /// 'generado' hasta ahoraUtc (como la clasica: el 0DTE vencido sale solo). filas = cuantas filas entraron en el horizonte Hoy.</summary>
        public static double Cruce(FotoCadena c, double S, DateTime ahoraUtc, out int filas, double r = Nucleo20.TASA, bool porVolumen = true)
        {
            filas = 0;
            if (c?.Filas == null || c.Filas.Length == 0 || c.Dias == null || c.Dias.Length == 0 || !(S > 0)) return double.NaN;
            // CalcularAdentro: los dias de la cadena son de cuando se genero: se envejecen a la hora de la cuenta
            double envejecer = Nucleo20.Envejecer(c.GeneradoUtc, ahoraUtc);
            double masCerca = double.MaxValue;
            foreach (var d in c.Dias) { double dd = Z(d) - envejecer; if (dd >= 0 && dd < masCerca) masCerca = dd; }
            if (masCerca == double.MaxValue) masCerca = 0;
            // las filas que pasan PasaHorizonte(Hoy) y su T (no dependen de x): en el mismo orden que c.Filas (la suma va en ese orden)
            var fs = new List<FilaCadena>(c.Filas.Length); var ts = new List<double>(c.Filas.Length);
            foreach (var f in c.Filas)
            {
                if (f.V < 0 || f.V >= c.Dias.Length) continue;
                double dias = Z(c.Dias[f.V]) - envejecer;
                if (!(dias >= 0 && dias <= Math.Max(1.0, masCerca + 0.01))) continue;
                fs.Add(f); ts.Add(Math.Max(dias, Nucleo20.PISO_DIAS) / 365.0);
            }
            filas = fs.Count;
            double amp = AMP; double lo = S * (1 - amp), hi = S * (1 + amp); const int pasos = PASOS;
            double ant = double.NaN, xAnt = 0;
            for (int i = 0; i <= pasos; i++)
            {
                double x = lo + (hi - lo) * i / pasos, t = 0;
                for (int k = 0; k < fs.Count; k++) t += Nucleo20.Gex(fs[k], x, ts[k], r, porVolumen);
                if (!double.IsNaN(ant) && ((ant < 0 && t >= 0) || (ant > 0 && t <= 0)))
                    return (t != ant) ? xAnt + (x - xAnt) * (-ant) / (t - ant) : x;
                ant = t; xAnt = x;
            }
            return double.NaN;
        }
    }

    /// <summary>4.1.5b: una dominante de la capa NDX de la clasica (strike ganador, su posicion = centroide, su monto con signo en USD).</summary>
    public sealed class DomClasica
    {
        public double K, Fut, Pos, Gex;
        public string Lado = "";                        // "arriba" | "abajo" | "relleno"
    }

    /// <summary>4.1.5b (09-10-2026, pedido del operador: "replicar la clasica, su formula"): las dominantes D1-D3 de la capa NDX de la clasica
    /// (GammaHoyNucleo.CalcularAdentro :300-414 de PythiaGexNiveles, receta en laboratorio/calibracion_1009/receta_clasica_dominantes) sobre el MISMO
    /// perfil que el zero (horizonte Hoy, Black-Scholes r 0,0375, x100, S = fut - base):
    ///   radio = min(2 % del fut; 100); libro = volumen (si ningun strike del radio tiene GEX por volumen, OI); peso = |GEX|;
    ///   elegir(lado) = de los que pesan >= 80 % del mayor del lado, el mas CERCANO al fut (empate: el de mas peso; despues el de menor K);
    ///   D1 = elegir(Fut &gt; fut), D2 = elegir(Fut &lt;= fut), D3 = la de mas peso del radio que no tenga el mismo Fut (relleno, sin mirar el lado);
    ///   posicion = centroide ponderado por |GEX| de los strikes del perfil a +-12 pts del Fut del ganador; monto = GEX del strike ganador.
    /// Sin histeresis ni memoria (como la clasica: cada minuto elige de cero).</summary>
    public static class DominantesClasica
    {
        public const double RADIO_PCT = 0.02, RADIO_MAX = 100, EMPATE = 0.20, CENTROIDE = 12;

        public static List<DomClasica> Calcular(FotoCadena c, double S, double fut, double b, DateTime ahoraUtc, double r, out string libroDom, int cuantas = 3)
        {
            libroDom = "";
            var res = new List<DomClasica>(3);
            if (c?.Filas == null || c.Filas.Length == 0 || c.Dias == null || c.Dias.Length == 0 || !(S > 0) || !(fut > 0) || double.IsNaN(b)) return res;
            double envejecer = Nucleo20.Envejecer(c.GeneradoUtc, ahoraUtc);
            double masCerca = double.MaxValue;
            foreach (var d in c.Dias) { double dd = (double.IsNaN(d) ? 0 : d) - envejecer; if (dd >= 0 && dd < masCerca) masCerca = dd; }
            if (masCerca == double.MaxValue) masCerca = 0;
            // el perfil por strike (CalcularAdentro :300-321): suma de las filas que pasan el horizonte, en el orden de c.Filas
            var gv = new SortedDictionary<double, double>(); var go = new SortedDictionary<double, double>();
            foreach (var f in c.Filas)
            {
                if (f.V < 0 || f.V >= c.Dias.Length) continue;
                double dias = (double.IsNaN(c.Dias[f.V]) ? 0 : c.Dias[f.V]) - envejecer;
                if (!(dias >= 0 && dias <= Math.Max(1.0, masCerca + 0.01))) continue;
                double T = Math.Max(dias, Nucleo20.PISO_DIAS) / 365.0;
                double v = Nucleo20.Gex(f, S, T, r, true), o = Nucleo20.Gex(f, S, T, r, false);
                if (v == 0 && o == 0) continue;
                double k = f.K;
                gv[k] = (gv.TryGetValue(k, out var a) ? a : 0) + v;
                go[k] = (go.TryGetValue(k, out var a2) ? a2 : 0) + o;
            }
            if (gv.Count == 0) return res;
            double radio = Math.Min(fut * RADIO_PCT, RADIO_MAX);
            var ks = gv.Keys.ToList();
            bool porVol = ks.Any(k => Math.Abs(k + b - fut) <= radio && gv[k] != 0);
            libroDom = porVol ? "vol" : "OI";
            var w = porVol ? gv : go;
            var enR = ks.Where(k => Math.Abs(k + b - fut) <= radio && w[k] != 0).ToList();       // en orden de K
            if (enR.Count == 0) return res;
            double? Elegir(IEnumerable<double> lado)
            {
                var l = lado.ToList(); if (l.Count == 0) return null;
                double pmax = l.Max(k => Math.Abs(w[k]));
                double? mejor = null; double dMejor = double.MaxValue, pMejor = -1;
                foreach (var k in l)          // orden de K: el primero gana el empate total (menor K)
                {
                    double p = Math.Abs(w[k]); if (p < (1 - EMPATE) * pmax) continue;
                    double dd = Math.Abs(k + b - fut);
                    if (dd < dMejor || (dd == dMejor && p > pMejor)) { mejor = k; dMejor = dd; pMejor = p; }
                }
                return mejor;
            }
            var arriba = Elegir(enR.Where(k => k + b > fut));
            var abajo = Elegir(enR.Where(k => k + b <= fut));
            var elegidos = new List<(double K, string Lado)>(3);
            if (arriba.HasValue) elegidos.Add((arriba.Value, "arriba"));
            if (abajo.HasValue) elegidos.Add((abajo.Value, "abajo"));
            foreach (var k in enR.OrderByDescending(k => Math.Abs(w[k])))     // OrderBy es estable: con el mismo peso, el de menor K
            {
                if (elegidos.Count >= cuantas) break;
                if (elegidos.Any(e => e.K + b == k + b)) continue;
                elegidos.Add((k, "relleno"));
            }
            foreach (var (k, lado) in elegidos.Take(cuantas))
            {
                double fk = k + b, sp = 0, sw = 0;
                foreach (var y in ks)
                {
                    double py = Math.Abs(w[y]); if (!(py > 0)) continue;
                    double fy = y + b; if (Math.Abs(fy - fk) > CENTROIDE) continue;
                    sp += py * fy; sw += py;
                }
                res.Add(new DomClasica { K = k, Fut = fk, Pos = sw > 0 ? sp / sw : fk, Gex = w[k], Lado = lado });
            }
            return res;
        }
    }

    /// <summary>MedirBaseRueda de la clasica (GammaHoy.cs:1418-1485), la cuenta de UNA muestra, port 1:1.</summary>
    public static class ReglaBaseClasica
    {
        public const int VENTANA = 24, MIN_BUENAS = 5;
        public const int INI_MIN_UTC = 13 * 60 + 50, FIN_MIN_UTC = 20 * 60 + 10;      // "9:50-16:10 de Nueva York" escrito en UTC fijo (como la clasica)
        public const double EDAD_CADENA_MAX_MIN = 30;

        /// <summary>El sello de CBOE entra en la ventana de la clasica (hora UTC entre 13:50 y 20:10, el minuto 20:10 entero).</summary>
        public static bool EnVentana(DateTime tsUtc) { int m = tsUtc.Hour * 60 + tsUtc.Minute; return m >= INI_MIN_UTC && m <= FIN_MIN_UTC; }

        /// <summary>Una muestra: la agrega a obs (las ultimas 24), saca la mediana robusta y, con >= 5 buenas (o sin base), la adopta con la hora 'ahora'.
        /// precio = el cierre de la vela (el tope usa ESE precio, como la clasica).</summary>
        public static void Paso(List<double> obs, double muestra, double precio, DateTime ahoraUtc, ref double baseRueda, ref DateTime baseUtc,
                                out double med, out double mad, out int buenas, out bool adopto)
        {
            obs.Add(muestra);
            if (obs.Count > VENTANA) obs.RemoveAt(0);
            var ord = obs.OrderBy(x => x).ToList();
            med = ord[ord.Count / 2];
            double m0 = med;
            mad = ord.Select(x => Math.Abs(x - m0)).OrderBy(x => x).ToList()[ord.Count / 2];
            double tope = Math.Max(3 * mad, precio * 0.0002);
            var b = obs.Where(x => Math.Abs(x - m0) <= tope).OrderBy(x => x).ToList();
            buenas = b.Count;
            if (b.Count >= 3) med = b[b.Count / 2];
            adopto = b.Count >= MIN_BUENAS || double.IsNaN(baseRueda);
            if (adopto) { baseRueda = med; baseUtc = ahoraUtc; }
        }
    }

    public sealed class OpcionesClasicaNdx
    {
        /// <summary>Codigo del grafico (MNQZ6): el vencimiento del futuro para la cota del carry (como la clasica con el codigo del instrumento).</summary>
        public string Contrato = "";
        public double Tasa = Nucleo20.TASA, Dividendo = BaseNdx20.DIVIDENDO_NQ;
        /// <summary>RetrasoCboeSeg de la clasica (960 en el .ws del operador): la vela de la muestra es la que contiene (sello - esto).</summary>
        public int RetrasoCboeSeg = 960;
        /// <summary>El marco (en minutos) de la vela "del grafico" con que se mide la base: 2 (default) = la vela m2 de la cinta de la 4.1 = el grafico de
        /// 2 min donde corria la clasica el 08-10 cuando midio la base de 243,06 que dibujo todo el 09-10 (medido en su log: las 92 muestras del 08-10
        /// caen en velas de minuto par); 1 = una vela de 1 min (el grafico de la clasica el 09-10 desde las 19:32 UTC y en el .ws "MNQ liviano"). El cierre
        /// de una vela es el ultimo tick hasta su fin. Medido (arnes clasica 2e, cinta solo de ticks): rueda del 08-10 243,06 con 2 / 242,45 con 1; rueda
        /// del 09-10 229,98 con 2 / 230,23 con 1 (la clasica: 230,16). Ningun marco fijo replica los dos dias: queda 2 y la pestaña lo dice.</summary>
        public int VelaMin = 2;
        /// <summary>La base de la rueda vale 24 h desde su ultima adopcion (BaseRuedaEdadMin &lt;= 24 x 60 de la clasica).</summary>
        public double EdadBaseRuedaMaxMin = 24 * 60;
        /// <summary>true (default) = durante la ventana de la rueda (13:50-20:11 UTC) la base queda CONGELADA en la que el dia tenia al empezar (la de la rueda
        /// anterior), como la clasica con su primaria en Rithmic (el 09-10 hasta las 19:32 UTC); false = la clasica con la primaria en CBOE (mide y cambia en
        /// la rueda; el 09-10 desde las 19:32 UTC, tras los reinicios de ATAS). Cual de las dos tiene la clasica no se sabe desde aca (independencia).</summary>
        public bool CongelarEnLaRueda = true;
        /// <summary>4.1.5d: carpeta de datos (%APPDATA%\ATAS\PythiaGex4). Con ella las muestras de la regla medidas con una vela completa se guardan en
        /// familia\muestras-clasica-NDX-&lt;dia UTC del sello&gt;.jsonl y al reiniciar se usan las guardadas (la rueda cerrada no cambia). null/"" = no
        /// guarda ni lee nada (el arnes).</summary>
        public string CarpetaDatos;
        /// <summary>4.1.5d: la vela m2 que abre en ese instante esta COMPLETA en la cinta (escuchada entera o con la vela del grafico)? La pasa el integrador
        /// (CintaFamilia.VelaM2Completa). null = se toma como completa (como antes de la 4.1.5d). Con VelaMin = 1 no se usa: el cierre de 1 min sale de un
        /// tick con la cinta escuchada hasta el fin del minuto (PrecioSoloTick) o no sale.</summary>
        public Func<DateTime, bool> VelaCompleta;
        /// <summary>4.1.5d: dias de muestras guardadas que se conservan (los archivos mas viejos se borran al arrancar).</summary>
        public int RetencionDias = 21;
        /// <summary>Dias de cadenas previos al inicio de la sesion que recorre la regla (para tener la rueda anterior: el lunes, la del viernes).</summary>
        public int DiasHistoria = 6;
        /// <summary>true (default) = el fut de la cuenta es el ultimo precio del MNQ al empezar el minuto (lo mas parecido al "cierre de la vela actual"
        /// con que recalcula la clasica); false = el fut del minuto de la 4.1 (cierre de la ultima vela m2 cerrada).</summary>
        public bool FutDelInstante = true;
        /// <summary>Cada cuantos segundos (reloj de la PC) se vuelven a mirar las muestras que no tenian vela en la cinta (cinta cargando al arrancar).
        /// 0 = en cada llamada (arnes).</summary>
        public int SondeoCadaS = 20;
        public Action<string> Log;
    }

    /// <summary>Una muestra de la regla (una cadena de CBOE en la ventana) y el estado despues de procesarla.</summary>
    public sealed class MuestraClasica
    {
        public DateTime GenUtc, TsUtc, VelaUtc, Dia;
        public double Spot = double.NaN, Precio = double.NaN, Muestra = double.NaN;
        /// <summary>La cinta no tenia la vela: la muestra no entra (provisional: se vuelve a mirar).</summary>
        public bool SinVela;
        /// <summary>4.1.5d: el precio salio del archivo de muestras guardadas (la primera medicion con vela completa).</summary>
        public bool Guardada;
        /// <summary>4.1.5d: la vela de la cinta estaba INCOMPLETA (ticks con un hueco y sin la vela del grafico): entra, pero no se guarda y se vuelve a mirar.</summary>
        public bool Provisional;
        // estado despues de esta muestra (solo si tiene vela)
        public double Mediana = double.NaN, Mad = double.NaN, Base = double.NaN;
        public DateTime BaseUtc;
        public int Buenas, N, MuestrasDia;
        public bool Adopto;
        /// <summary>El dia de la ultima adopcion de la base (la rueda de donde sale).</summary>
        public DateTime BaseDia;
    }

    /// <summary>La base de la rueda vigente en un instante.</summary>
    public sealed class EstadoBaseClasica
    {
        public double Base = double.NaN;
        public DateTime BaseUtc, BaseDia;                 // ultima adopcion y el dia (UTC) de su rueda
        public int Buenas, N, MuestrasDia;               // buenas/N de la ultima adopcion; muestras de ese dia
        public bool Congelada;                           // se uso la del inicio de la ventana de hoy
        public DateTime CorteUtc;                        // el estado es el de las muestras con generado <= esto
        public bool Hay => !double.IsNaN(Base);
        public double EdadMin(DateTime ahoraUtc) => Hay ? (ahoraUtc - BaseUtc).TotalMinutes : double.NaN;
    }

    /// <summary>Lo que dio la replica en un instante (para el motor y el arnes).</summary>
    public sealed class ResultadoClasica
    {
        public const string LIBRO = "Clasica NDX";
        public FotoCadena Foto; public DateTime GeneradoUtc;
        public double Fut = double.NaN, Base = double.NaN, S = double.NaN, ZeroIdx = double.NaN, Zero = double.NaN, Carry = double.NaN;
        public int Filas;
        /// <summary>"de la rueda" | "CRUDA" | "TEORICA" | "CRUDA sin cota".</summary>
        public string Origen = "";
        public EstadoBaseClasica Rueda;                  // la base de la rueda que mira la cascada (congelada en la rueda)
        public EstadoBaseClasica Hoy;                    // la regla de la clasica SIN congelar a esta hora (la rueda de hoy, para comparar)
        public double BaseAhora = double.NaN;            // la base sincronizada de la 4.1 (C7) del minuto, para comparar (NaN si no hay)
        public bool Congelada;
        public string Texto = "";
        /// <summary>4.1.5b: las dominantes D1-D3 de la capa NDX de la clasica con la misma cadena, base y fut (DominantesClasica).</summary>
        public List<DomClasica> Doms = new List<DomClasica>();
        /// <summary>"vol" | "OI" (ningun strike del radio con GEX por volumen) | "" (sin dominantes). 4.1.5d: va al texto ("dominantes por OI") y a la etiqueta.</summary>
        public string LibroDom = "";
        /// <summary>4.1.5d: el marco de la vela con que se mide la base (OpcionesClasicaNdx.VelaMin), para el texto.</summary>
        public int VelaMin = 2;
        /// <summary>4.1.5d: el marcador que pone Texto cuando las dominantes salen del interes abierto (ArmadoPantalla lo lee para la etiqueta).</summary>
        public const string MARCA_DOM_OI = "dominantes por OI";
    }

    /// <summary>La serie "Clasica NDX 0Γ" de cada minuto (IExtrasMinuto del motor, IExtrasRehacer si la base de la rueda cambia despues: la cinta
    /// cargaba su historia al arrancar). Un solo hilo (el del motor); lock por si acaso.</summary>
    public sealed class ClasicaNdx : IExtrasMinuto, IExtrasRehacer, IExtrasParciales
    {
        public const string VERSION = "clasica 4.1.5d (09-10-2026)";
        public const string SERIE = "R10_NDX_zero";
        public const string SERIE_DOM = "R10_NDX_dom";             // 4.1.5b: las dominantes D1-D3 de la capa NDX de la clasica
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly IFuenteCboe _cboe;
        private readonly ICinta _cinta;
        private readonly OpcionesClasicaNdx _op;
        private readonly object _llave = new object();
        private readonly MuestrasClasicaArchivo _mem;                              // 4.1.5d: muestras guardadas (null sin CarpetaDatos)

        private readonly List<FotoCadena> _fn = new List<FotoCadena>();          // cadenas NDX (orden de generado)
        private readonly List<long> _gn = new List<long>();                       // generado al segundo (ms)
        private List<MuestraClasica> _m = new List<MuestraClasica>();            // las muestras (con vela) en orden de generado
        private List<long> _mg = new List<long>();                                // su generado (ms) para buscar
        private List<MuestraClasica> _sinVela = new List<MuestraClasica>();      // sin vela: no entran (se vuelven a mirar)
        private List<MuestraClasica> _provisionales = new List<MuestraClasica>(); // 4.1.5d: con una vela incompleta: entran, no se guardan, se vuelven a mirar
        private long _ver = long.MinValue;
        private DateTime _desde = DateTime.MinValue;
        private DateTime _ultSondeoUtc = DateTime.MinValue;
        private int _usadasGuardadas, _distintasGuardadas, _guardadasAhora;

        public long Generacion { get; private set; }
        public DateTime RehacerDesdeUtc { get; private set; } = DateTime.MaxValue;
        public int Recalculos { get; private set; }
        public IReadOnlyList<MuestraClasica> Muestras { get { lock (_llave) return _m.ToArray(); } }
        public int SinVela { get { lock (_llave) return _sinVela.Count; } }
        /// <summary>4.1.5d: muestras con una vela incompleta (entran provisionales, no se guardan).</summary>
        public int Provisionales { get { lock (_llave) return _provisionales.Count; } }
        /// <summary>4.1.5d: en la ultima cuenta: muestras que usaron el precio guardado, de esas cuantas difieren de la cinta de ahora, y cuantas se guardaron.</summary>
        public (int Usadas, int Distintas, int Guardadas) Archivo { get { lock (_llave) return (_usadasGuardadas, _distintasGuardadas, _guardadasAhora); } }

        public ClasicaNdx(IFuenteCboe cboe, ICinta cinta, OpcionesClasicaNdx op = null)
        {
            _cboe = cboe ?? throw new ArgumentNullException(nameof(cboe));
            _cinta = cinta;
            _op = op ?? new OpcionesClasicaNdx();
            if (!string.IsNullOrEmpty(_op.CarpetaDatos)) _mem = new MuestrasClasicaArchivo(_op.CarpetaDatos, _op.Log);
        }

        /// <summary>4.1.5d: la vela de la muestra esta completa en la cinta? (VelaMin 2: la del balde m2 que contiene 'hora'; sin el dato, si.)</summary>
        private bool VelaCompleta(DateTime horaUtc)
        {
            if (_op.VelaMin != 2) return true;              // 1 min: PrecioSoloTick ya exige la cinta escuchada hasta el fin del minuto
            var q = _op.VelaCompleta; if (q == null) return true;
            try { return q(TiempoFam.DeMs(TiempoFam.AperturaM2(TiempoFam.Ms(horaUtc)))); } catch { return true; }
        }

        private static DateTime Seg(DateTime d) => new DateTime(d.Ticks - d.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);

        // ------------------------------------------------------------------ la vela "del grafico" (de la cinta de la 4.1)
        /// <summary>BarraDe(hora) + GetCandle(b).Close de la clasica: el cierre de la vela de VelaMin minutos que contiene 'hora'. 2 min: la vela m2 cerrada
        /// de la cinta de ESE balde; 1 min: el ultimo tick de ese minuto. NaN si la cinta no la tiene. Sin buscar la vela anterior: en la ventana de la
        /// regla (9:34-15:54 NY) el MNQ opera en cada minuto, asi que una vela que falta es un hueco de la cinta (ATAS cerrado o Rithmic caido: el 08-10 de
        /// 17:49 a ~18:15 UTC) y la anterior seria de ANTES del hueco (medido: 3 muestras del 08-10 salian 24 pts corridas); la clasica la tendria del
        /// historial del grafico. La muestra no entra y queda "sin vela" (se vuelve a mirar).</summary>
        public double CierreVela(DateTime horaUtc, IReadOnlyList<(long Ms, double O, double H, double L, double C)> velasM2 = null)
        {
            if (_cinta == null) return double.NaN;
            long ms = TiempoFam.Ms(horaUtc);
            try
            {
                if (_op.VelaMin == 2)
                {
                    long b = TiempoFam.AperturaM2(ms);
                    var vs = velasM2 ?? _cinta.VelasM2(TiempoFam.DeMs(b), TiempoFam.DeMs(b + 1));
                    return CierreEn(vs, horaUtc);
                }
                long min = Math.Max(1, _op.VelaMin) * 60000L;
                long fin = TiempoFam.PisoDiv(ms, min) * min + min - 1;
                double p = _cinta.PrecioSoloTick(TiempoFam.DeMs(fin), (int)(min / 1000));
                return p > 0 ? p : double.NaN;
            }
            catch { return double.NaN; }
        }

        /// <summary>Busca en una lista de velas m2 ordenada (VelasM2) la del balde de 2 min que contiene 'hora' (exacta). NaN si no esta.</summary>
        private static double CierreEn(IReadOnlyList<(long Ms, double O, double H, double L, double C)> vs, DateTime horaUtc)
        {
            if (vs == null) return double.NaN;
            long b = TiempoFam.AperturaM2(TiempoFam.Ms(horaUtc));
            int lo = 0, hi = vs.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (vs[mid].Ms == b) return vs[mid].C > 0 ? vs[mid].C : double.NaN;
                if (vs[mid].Ms < b) lo = mid + 1; else hi = mid - 1;
            }
            return double.NaN;
        }

        // ------------------------------------------------------------------ las cadenas y la regla de la base
        /// <summary>Trae las cadenas NDX si cambio la fuente o la ventana, y rehace las muestras de la regla en orden (causal y determinista). Si cambio
        /// el estado de una muestra ya vista (la cinta trajo una vela que faltaba, o la fuente cambio el principio de la lista), sube Generacion.</summary>
        private void Refrescar(DateTime tUtc)
        {
            var desde = TiempoFam.IniSesion(TiempoFam.Sesion(tUtc)).AddDays(-_op.DiasHistoria);
            long ver = _cboe.Version;
            bool sondeo = false;
            if ((_sinVela.Count > 0 || _provisionales.Count > 0) && (_op.SondeoCadaS <= 0 || (DateTime.UtcNow - _ultSondeoUtc).TotalSeconds >= _op.SondeoCadaS))
            {
                _ultSondeoUtc = DateTime.UtcNow;
                IReadOnlyList<(long Ms, double O, double H, double L, double C)> vs = null;
                if (_op.VelaMin == 2 && _cinta != null && _sinVela.Count > 0)
                    try { vs = _cinta.VelasM2(_sinVela[0].VelaUtc.AddHours(-1), DateTime.MaxValue); } catch { vs = null; }
                foreach (var s in _sinVela)
                    if ((_op.VelaMin == 2 ? (vs != null ? CierreEn(vs, s.VelaUtc) : double.NaN) : CierreVela(s.VelaUtc)) > 0) { sondeo = true; break; }
                // 4.1.5d: una provisional (vela de ticks incompleta) cuya vela ya esta completa (llego la del grafico o el tramo que faltaba): se rehace
                if (!sondeo) foreach (var s in _provisionales) if (VelaCompleta(s.VelaUtc)) { sondeo = true; break; }
            }
            if (!sondeo && ver == _ver && desde == _desde) return;
            if (_mem != null && desde != _desde)
            {   // 4.1.5d: las muestras guardadas de los dias de la ventana (los que todavia no se leyeron) y la poda de los viejos
                var hasta = (DateTime.UtcNow > tUtc ? DateTime.UtcNow : tUtc).Date.AddDays(1);
                try { _mem.Cargar(desde.Date.AddDays(-1), hasta); _mem.Podar(DateTime.UtcNow.Date.AddDays(-Math.Max(_op.DiasHistoria + 2, _op.RetencionDias))); }
                catch (Exception e) { _op.Log?.Invoke("muestras guardadas: " + e.GetType().Name + ": " + e.Message); }
            }
            _ver = ver; _desde = desde;
            var ln = _cboe.Fotos("NDX", desde, DateTime.MaxValue) ?? Array.Empty<FotoCadena>();
            _fn.Clear(); _gn.Clear();
            foreach (var f in ln)
            {
                if (f == null) continue;
                long g = TiempoFam.Ms(Seg(f.GeneradoUtc));
                if (_gn.Count > 0 && g < _gn[_gn.Count - 1]) continue;
                _fn.Add(f); _gn.Add(g);
            }
            var viejas = _m;
            Muestrear();
            Recalculos++;
            // que cambio: la primera muestra distinta (no solo agregadas al final) => el motor rehace los minutos desde ahi. Las listas se alinean en la
            // primera muestra comun (cuando la ventana de dias avanza, las del dia que sale no cuentan como cambio)
            DateTime piso = viejas.Count > 0 && _m.Count > 0 ? (viejas[0].GenUtc > _m[0].GenUtc ? viejas[0].GenUtc : _m[0].GenUtc) : DateTime.MinValue;
            var va = viejas.Where(x => x.GenUtc >= piso).ToList(); var vb = _m.Where(x => x.GenUtc >= piso).ToList();
            int n = Math.Min(va.Count, vb.Count), i0 = -1;
            for (int i = 0; i < n && i0 < 0; i++)
                if (va[i].GenUtc != vb[i].GenUtc || va[i].TsUtc != vb[i].TsUtc || !va[i].Base.Equals(vb[i].Base) || va[i].BaseUtc != vb[i].BaseUtc) i0 = i;
            if (i0 < 0 && vb.Count < va.Count) i0 = vb.Count;
            if (i0 >= 0 && viejas.Count > 0)
            {
                var g0 = i0 < vb.Count ? vb[i0].GenUtc : va[i0].GenUtc;
                if (i0 < va.Count && va[i0].GenUtc < g0) g0 = va[i0].GenUtc;
                Generacion++; RehacerDesdeUtc = g0;
                _op.Log?.Invoke("base de la rueda REHECHA (" + (sondeo ? "la cinta trajo velas que faltaban o completo velas provisionales" : "la fuente cambio") + "): la primera muestra distinta es la de "
                                + g0.ToString("MM-dd HH:mm:ss", Inv) + "Z; generacion " + Generacion + ", " + _sinVela.Count + " muestras siguen sin vela y " + _provisionales.Count + " provisionales");
            }
            if (_op.Log != null && (viejas.Count != _m.Count || i0 >= 0 || _guardadasAhora > 0))
            {
                var u = _m.Count > 0 ? _m[_m.Count - 1] : null;
                _op.Log(_m.Count + " muestras de la regla (" + _sinVela.Count + " sin vela en la cinta, " + _provisionales.Count + " provisionales con la vela incompleta"
                        + (_mem != null ? "; " + _usadasGuardadas + " del archivo, " + _distintasGuardadas + " distintas de la cinta de ahora, " + _guardadasAhora + " guardadas ahora" : "")
                        + ") en " + _fn.Count + " cadenas NDX desde " + desde.ToString("MM-dd HH:mm", Inv) + "Z"
                        + (u == null ? "" : "; ultima: sello " + u.TsUtc.ToString("MM-dd HH:mm:ss", Inv) + "Z spot " + NumFam.F(u.Spot, "0.00") + " vela " + u.VelaUtc.ToString("HH:mm", Inv)
                                            + "Z cierre " + NumFam.F(u.Precio, "0.00") + " => " + NumFam.F(u.Muestra, "0.00") + " | mediana " + NumFam.F(u.Mediana, "0.00") + " de " + u.Buenas + "/" + u.N
                                            + " | base " + NumFam.F(u.Base, "0.00") + " (rueda " + u.BaseDia.ToString("yyyy-MM-dd", Inv) + ")"));
            }
        }

        /// <summary>La regla de la clasica sobre TODAS las cadenas de la ventana, en orden de generado: una muestra por sello (la fuente ya deduplica por
        /// sello), la lista de 24 se vacia al cambiar el dia; la base sigue de un dia al otro (como el archivo de la clasica).
        /// 4.1.5d: el precio de una muestra GUARDADA (la primera medicion con vela completa) manda sobre la cinta de ahora; una medida con vela completa
        /// se guarda; una con vela incompleta entra provisional (no se guarda, se vuelve a mirar).</summary>
        private void Muestrear()
        {
            var m = new List<MuestraClasica>(); var mg = new List<long>(); var sv = new List<MuestraClasica>(); var pv = new List<MuestraClasica>();
            var nuevas = new List<MuestraClasica>();
            int usadas = 0, distintas = 0;
            int retraso = Math.Max(0, _op.RetrasoCboeSeg);
            // 1) las candidatas (una por sello nuevo de la ventana) y, ANTES de la foto de velas, si la vela de cada una esta completa: las velas de la
            //    cinta solo se completan (ticks que llegan, velas del grafico que se agregan), asi que una completa antes de la foto da en la foto su cierre
            //    completo; una que se completa en el medio queda provisional y la rehace el sondeo. Nunca se guarda el cierre de una vela incompleta.
            var cand = new List<(FotoCadena F, DateTime Ts, DateTime Hv, MuestraClasicaGuardada G, bool Completa)>(_fn.Count);
            DateTime ultimoTs = default;
            foreach (var f in _fn)
            {
                var ts = f.TsUtc;
                if (ts == default(DateTime) || ts == ultimoTs || !(f.Spot > 0)) continue;
                if (!ReglaBaseClasica.EnVentana(ts) || (f.GeneradoUtc - ts).TotalMinutes > ReglaBaseClasica.EDAD_CADENA_MAX_MIN) continue;
                ultimoTs = ts;
                var hv = ts.AddSeconds(-retraso);
                MuestraClasicaGuardada g = null;
                if (_mem != null && _mem.Buscar(ts, _op.VelaMin, retraso, out var g0) && g0.Precio > 0) g = g0;
                cand.Add((f, ts, hv, g, g != null || VelaCompleta(hv)));
            }
            // 2) la foto de velas de la cinta (DESPUES de mirar si estaban completas)
            IReadOnlyList<(long Ms, double O, double H, double L, double C)> velas = null;
            if (_op.VelaMin == 2 && _cinta != null && _fn.Count > 0)
                try { velas = _cinta.VelasM2(_fn[0].TsUtc.AddSeconds(-_op.RetrasoCboeSeg - 3600), DateTime.MaxValue); } catch { velas = null; }
            // 3) la regla
            var obs = new List<double>(ReglaBaseClasica.VENTANA + 1);
            double baseR = double.NaN; DateTime baseUtc = default, baseDia = default; DateTime dia = default; int nDia = 0;
            foreach (var (f, ts, hv, g, completa) in cand)
            {
                var s = new MuestraClasica { GenUtc = f.GeneradoUtc, TsUtc = ts, VelaUtc = hv, Spot = f.Spot, Dia = ts.Date };
                double cinta = _op.VelaMin == 2 ? (velas != null ? CierreEn(velas, hv) : double.NaN) : CierreVela(hv);
                double p;
                if (g != null)
                {
                    p = g.Precio; s.Guardada = true; usadas++;
                    if (cinta > 0 && Math.Abs(cinta - p) > 1e-9) distintas++;
                }
                else
                {
                    p = cinta;
                    if (!(p > 0)) { s.SinVela = true; sv.Add(s); continue; }
                    if (!completa) { s.Provisional = true; pv.Add(s); }
                    else if (_mem != null) nuevas.Add(s);
                }
                if (s.Dia != dia) { obs.Clear(); dia = s.Dia; nDia = 0; }
                s.Precio = p; s.Muestra = p - f.Spot; nDia++;
                ReglaBaseClasica.Paso(obs, s.Muestra, p, f.GeneradoUtc, ref baseR, ref baseUtc, out var med, out var mad, out var buenas, out var adopto);
                if (adopto) baseDia = s.Dia;
                s.Mediana = med; s.Mad = mad; s.Buenas = buenas; s.N = obs.Count; s.Adopto = adopto; s.Base = baseR; s.BaseUtc = baseUtc; s.BaseDia = baseDia; s.MuestrasDia = nDia;
                m.Add(s); mg.Add(TiempoFam.Ms(f.GeneradoUtc));
            }
            _m = m; _mg = mg; _sinVela = sv; _provisionales = pv;
            _usadasGuardadas = usadas; _distintasGuardadas = distintas; _guardadasAhora = 0;
            if (_mem != null && nuevas.Count > 0)
            {
                try { _guardadasAhora = _mem.Guardar(nuevas, _op.VelaMin, retraso, VERSION); }
                catch (Exception e) { _op.Log?.Invoke("muestras guardadas: no pude escribir: " + e.GetType().Name + ": " + e.Message); }
                foreach (var s in nuevas) s.Guardada = true;
            }
        }

        /// <summary>El estado de la regla con las muestras de generado &lt;= corte (null si no hay ninguna).</summary>
        private EstadoBaseClasica EstadoHasta(DateTime corteUtc, bool congelada)
        {
            int k = NumFam.UltimoMenorIgual(_mg, TiempoFam.Ms(corteUtc));
            if (k < 0) return new EstadoBaseClasica { CorteUtc = corteUtc, Congelada = congelada };
            var s = _m[k];
            // la ultima ADOPCION: buenas/N de esa muestra
            int j = k; while (j > 0 && !_m[j].Adopto) j--;
            var a = _m[j];
            // muestras del dia de la base hasta el corte (los dias van en orden: la ultima de ese dia antes de k tiene la cuenta)
            int nd = 0;
            for (int i = k; i >= 0; i--)
            {
                if (_m[i].Dia == s.BaseDia) { nd = _m[i].MuestrasDia; break; }
                if (_m[i].Dia < s.BaseDia) break;
            }
            return new EstadoBaseClasica { Base = s.Base, BaseUtc = s.BaseUtc, BaseDia = s.BaseDia, Buenas = a.Buenas, N = a.N, MuestrasDia = nd, CorteUtc = corteUtc, Congelada = congelada };
        }

        /// <summary>La base de la rueda que mira la cascada en el instante t: dentro de la ventana de la rueda de un dia (13:50-20:11 UTC) y con
        /// CongelarEnLaRueda, la del inicio de la ventana (las muestras de antes de las 13:50 de ese dia); si no, la de las muestras hasta t.</summary>
        public EstadoBaseClasica BaseRueda(DateTime tUtc)
        {
            lock (_llave)
            {
                Refrescar(tUtc);
                return BaseRuedaSinRefrescar(tUtc);
            }
        }

        private EstadoBaseClasica BaseRuedaSinRefrescar(DateTime tUtc)
        {
            if (_op.CongelarEnLaRueda && EnVentanaRueda(tUtc, out var ini)) return EstadoHasta(ini.AddMilliseconds(-1), true);
            return EstadoHasta(tUtc, false);
        }

        /// <summary>t cae en la ventana de la rueda de su dia UTC: [13:50, 20:11) (la ultima muestra posible tiene sello 20:10:59). ini = 13:50 de ese dia.</summary>
        public static bool EnVentanaRueda(DateTime tUtc, out DateTime iniUtc)
        {
            iniUtc = DateTime.SpecifyKind(tUtc.Date, DateTimeKind.Utc).AddMinutes(ReglaBaseClasica.INI_MIN_UTC);
            var fin = DateTime.SpecifyKind(tUtc.Date, DateTimeKind.Utc).AddMinutes(ReglaBaseClasica.FIN_MIN_UTC + 1);
            return tUtc >= iniUtc && tUtc < fin;
        }

        /// <summary>Vencimiento del futuro del grafico (13:30 UTC del tercer viernes), como la clasica: del codigo; si no trae mes, el trimestral mas cercano.</summary>
        public DateTime Expiracion(DateTime tUtc)
        {
            var e = BaseNdx20.ExpiracionDeCodigo(_op.Contrato, tUtc);
            return e != default(DateTime) ? e : BaseNdx20.TrimestralDesde(tUtc.AddDays(1));
        }

        /// <summary>La cascada de la clasica (sin "medida" ni "medida reciente": las cadenas no las traen). NaN = sin base (no se dibuja).</summary>
        public static double BaseCascada(FotoCadena c, double futuro, DateTime ahoraUtc, DateTime expUtc, EstadoBaseClasica rueda, double edadMaxMin,
                                         double tasa, double dividendo, out string origen, out double carry)
        {
            var iv0 = Inv;
            carry = BaseNdx20.Carry(futuro, ahoraUtc, expUtc, tasa, dividendo);
            double k0 = carry;
            bool Cerca(double b, double k) => Math.Abs(b - k) <= Math.Max(Math.Abs(k) * 0.6, futuro * 0.0006);
            bool Razonable(double b) => double.IsNaN(k0) || Cerca(b, k0);
            double cruda = c == null || double.IsNaN(c.BaseCruda) ? 0 : c.BaseCruda;
            double err = c == null || double.IsNaN(c.BaseErrorTicks) ? 0 : c.BaseErrorTicks;
            double edad = rueda != null && rueda.Hay ? rueda.EdadMin(ahoraUtc) : double.NaN;
            if (rueda != null && rueda.Hay && edad <= edadMaxMin && Razonable(rueda.Base)) { origen = "de la rueda"; return rueda.Base; }
            if (cruda != 0 && Razonable(cruda)) { origen = "CRUDA " + err.ToString("0", iv0) + " ticks"; return cruda; }
            if (!double.IsNaN(carry)) { origen = "TEORICA carry " + carry.ToString("0.0", iv0) + (cruda != 0 ? " (cruda " + cruda.ToString("0.0", iv0) + " descartada)" : ""); return carry; }
            if (cruda != 0) { origen = "CRUDA " + err.ToString("0", iv0) + " ticks (sin cota)"; return cruda; }
            origen = "sin base"; return double.NaN;
        }

        /// <summary>La replica en el instante t con el futuro fut (la cadena = la ultima con generado &lt;= t). null si no hay cadena con filas o base, o si no
        /// hay ni cruce ni dominantes (4.1.5d: sin cruce del zero las D1-D3 se calculan igual, como la clasica: Zero/ZeroIdx quedan NaN).
        /// baseAhora = la base sincronizada de la 4.1 de ese minuto (solo para el texto; NaN si no hay).</summary>
        public ResultadoClasica Zero(DateTime tUtc, double fut, double baseAhora = double.NaN)
        {
            lock (_llave)
            {
                Refrescar(tUtc);
                int k = NumFam.UltimoMenorIgual(_gn, TiempoFam.Ms(tUtc));
                if (k < 0 || !(fut > 0)) return null;
                var f = _fn[k];
                if (f.Filas == null || f.Filas.Length == 0) return null;
                var rueda = BaseRuedaSinRefrescar(tUtc);
                var hoy = _op.CongelarEnLaRueda && rueda.Congelada ? EstadoHasta(tUtc, false) : null;
                var exp = Expiracion(tUtc);
                double b = BaseCascada(f, fut, tUtc, exp, rueda, _op.EdadBaseRuedaMaxMin, _op.Tasa, _op.Dividendo, out var origen, out var carry);
                if (double.IsNaN(b)) return null;
                double S = fut - b;
                if (!(S > 0)) return null;
                double z = CruceClasica.Cruce(f, S, tUtc, out int filas, _op.Tasa, true);
                List<DomClasica> doms; string ld = "";
                try { doms = DominantesClasica.Calcular(f, S, fut, b, tUtc, _op.Tasa, out ld); } catch { doms = new List<DomClasica>(); ld = ""; }   // 4.1.5b
                if (double.IsNaN(z) && doms.Count == 0) return null;
                bool cong = f.DatoUtc.Year >= 2000 && TiempoFam.ANy(f.DatoUtc).TimeOfDay >= new TimeSpan(15, 59, 0);
                var r = new ResultadoClasica { Foto = f, GeneradoUtc = TiempoFam.DeMs(_gn[k]), Fut = fut, Base = b, S = S, ZeroIdx = z, Zero = double.IsNaN(z) ? double.NaN : z + b,
                                               Carry = carry, Filas = filas, Origen = origen, Rueda = rueda, Hoy = hoy, BaseAhora = baseAhora, Congelada = cong,
                                               Doms = doms, LibroDom = ld, VelaMin = _op.VelaMin };
                r.Texto = Texto(r, tUtc, _op.EdadBaseRuedaMaxMin);
                return r;
            }
        }

        /// <summary>El texto de la conversion (pestaña y archivo, numeros invariantes; ArmadoPantalla lo lee):
        /// "base clasica 243.06 = de la rueda 2026-10-08 (21 buenas de 24, 92 muestras, vela de 2 min, adoptada 20:10:52Z, hace 1080 min), congelada en la
        ///  rueda de hoy · ahora: 4.1 sincronizada 228.62; regla de la clasica con la rueda de hoy 227.30 (247 muestras) · zero en el indice 30825.15".
        /// 4.1.5d: ", vela de N min" (el marco con que se midio); sin cruce "· zero sin cruce en +-3 %"; con las dominantes del interes abierto
        /// "· dominantes por OI (ningun strike del radio con volumen)".</summary>
        public static string Texto(ResultadoClasica r, DateTime tUtc, double edadMaxMin = 24 * 60)
        {
            var sb = new System.Text.StringBuilder(220);
            sb.Append("base clasica ").Append(r.Base.ToString("0.00", Inv)).Append(" = ");
            var ru = r.Rueda;
            if (r.Origen == "de la rueda")
            {
                sb.Append("de la rueda ").Append(ru.BaseDia.ToString("yyyy-MM-dd", Inv)).Append(" (").Append(ru.Buenas).Append(" buenas de ").Append(ru.N)
                  .Append(", ").Append(ru.MuestrasDia).Append(" muestras, vela de ").Append(r.VelaMin.ToString(Inv)).Append(" min, adoptada ").Append(ru.BaseUtc.ToString("HH:mm:ss", Inv)).Append("Z, hace ")
                  .Append(ru.EdadMin(tUtc).ToString("0", Inv)).Append(" min)").Append(ru.Congelada ? ", congelada en la rueda de hoy" : "");
            }
            else
            {
                sb.Append(r.Origen);
                if (ru != null && ru.Hay)
                {
                    double edad = ru.EdadMin(tUtc);
                    sb.Append(" (la de la rueda ").Append(ru.BaseDia.ToString("yyyy-MM-dd", Inv)).Append(" ").Append(ru.Base.ToString("0.00", Inv)).Append(" tiene ")
                      .Append(edad.ToString("0", Inv)).Append(edad > edadMaxMin ? " min: vencio a las 24 h)" : " min: no paso la cota del carry)");
                }
                else sb.Append(" (sin base de la rueda en las cadenas de la 4.1)");
            }
            sb.Append(" · ahora:");
            bool alguna = false;
            if (!double.IsNaN(r.BaseAhora)) { sb.Append(" 4.1 sincronizada ").Append(r.BaseAhora.ToString("0.00", Inv)); alguna = true; }
            if (r.Hoy != null && r.Hoy.Hay && r.Hoy.BaseDia == tUtc.Date)
            { sb.Append(alguna ? ";" : "").Append(" regla de la clasica con la rueda de hoy ").Append(r.Hoy.Base.ToString("0.00", Inv)).Append(" (").Append(r.Hoy.MuestrasDia).Append(" muestras)"); alguna = true; }
            if (!alguna) sb.Append(" sin dato");
            if (double.IsNaN(r.ZeroIdx)) sb.Append(" · zero sin cruce en +-3 %");
            else sb.Append(" · zero en el indice ").Append(r.ZeroIdx.ToString("0.00", Inv));
            if (r.LibroDom == "OI") sb.Append(" · ").Append(ResultadoClasica.MARCA_DOM_OI).Append(" (ningun strike del radio con volumen)");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ IExtrasMinuto
        /// <summary>El fut con que se pone la serie del minuto t (la misma regla que Replica20.FutR20): con FutDelInstante, el ultimo tick del MNQ con hora
        /// &lt;= t (a lo sumo 120 s antes; si todavia no llego ningun tick despues de t, el ultimo); si no, el fut del minuto de la 4.1.</summary>
        public double FutClasica(DateTime tUtc, double futMinuto)
        {
            if (!_op.FutDelInstante || _cinta == null) return futMinuto;
            double p = double.NaN;
            try
            {
                var q = tUtc; var ut = _cinta.UltimoTickUtc;
                if (ut > DateTime.MinValue.AddDays(1) && ut < tUtc)
                {
                    if ((tUtc - ut).TotalSeconds > 120) return futMinuto;
                    q = ut;
                }
                p = _cinta.PrecioSoloTick(q, 120);
            }
            catch { p = double.NaN; }
            return p > 0 ? p : futMinuto;
        }

        public void Calcular(RegistroMinuto r, DateTime tUtc, double fut, IReadOnlyList<LibroMinuto> bk)
        {
            if (r == null) return;
            Poner(r, tUtc, FutClasica(tUtc, fut));
        }

        public bool Completar(RegistroMinuto r, DateTime tUtc)
        {
            if (r == null || double.IsNaN(r.FutMnq)) return false;
            return Poner(r, tUtc, FutClasica(tUtc, r.FutMnq));
        }

        /// <summary>IExtrasParciales: al minuto (guardado por la 4.1.4, con las R20) le falta esta serie. 4.1.5d: un minuto que ya tiene la meta de la
        /// clasica y las D1-D3 pero no el 0Γ (sin cruce: el zero NaN) no esta incompleto.</summary>
        public bool Falta(RegistroMinuto r)
        {
            int i = CatalogoFamilia.IndiceExtra(SERIE), j = CatalogoFamilia.IndiceExtra(SERIE_DOM);
            bool falta(int x) => x >= 0 && (r?.Extra == null || x >= r.Extra.Length || r.Extra[x] == null);
            bool conMeta = r?.MetaExtraDe(ResultadoClasica.LIBRO) != null;
            return (falta(i) && !(conMeta && !falta(j))) || falta(j);
        }

        /// <summary>IExtrasRehacer: rehace la serie en un minuto ya calculado con el estado de ahora. true si cambio algo.</summary>
        public bool Rehacer(RegistroMinuto r, DateTime tUtc)
        {
            if (r == null || double.IsNaN(r.FutMnq)) return false;
            int i = CatalogoFamilia.IndiceExtra(SERIE);
            if (i < 0) return false;
            int j = CatalogoFamilia.IndiceExtra(SERIE_DOM);
            var antes = r.Extra != null && i < r.Extra.Length ? r.Extra[i] : null;
            var kAntes = r.ExtraStrikes != null && i < r.ExtraStrikes.Length ? r.ExtraStrikes[i] : null;
            var dAntes = j >= 0 && r.Extra != null && j < r.Extra.Length ? r.Extra[j] : null;
            var kdAntes = j >= 0 && r.ExtraStrikes != null && j < r.ExtraStrikes.Length ? r.ExtraStrikes[j] : null;
            var mAntes = r.MetaExtraDe(ResultadoClasica.LIBRO);
            var res = Zero(tUtc, FutClasica(tUtc, r.FutMnq), BaseAhoraDe(r));
            Nivel[] lv = null, ld = null; double[] ks = null, kd = null; MetaLibro meta = null;
            if (res != null) { (lv, ks, meta) = Niveles(res); (ld, kd) = NivelesDom(res); }
            if (Mismo(antes, kAntes, mAntes, lv, ks, meta) && Mismo(dAntes, kdAntes, null, ld, kd, null)) return false;
            Guardar(r, i, lv, ks, meta, j, ld, kd);
            return true;
        }

        private static double BaseAhoraDe(RegistroMinuto r) { var m = r?.MetaDe("NDX"); return m != null ? m.Conv : double.NaN; }

        private bool Poner(RegistroMinuto r, DateTime tUtc, double fut)
        {
            int i = CatalogoFamilia.IndiceExtra(SERIE);
            if (i < 0) return false;
            var res = Zero(tUtc, fut, BaseAhoraDe(r));
            if (res == null) return false;
            var (lv, ks, meta) = Niveles(res);
            var (ld, kd) = NivelesDom(res);
            Guardar(r, i, lv, ks, meta, CatalogoFamilia.IndiceExtra(SERIE_DOM), ld, kd);
            return true;
        }

        /// <summary>Nivel "Z": precio round(zero + base, 2), sin monto; strike = el zero en el eje del indice, round 2. 4.1.5d: sin cruce (solo las
        /// dominantes) el 0Γ no lleva nivel (null) y la meta va igual (la base de las D1-D3 en la pestaña).</summary>
        public static (Nivel[] Lv, double[] Ks, MetaLibro Meta) Niveles(ResultadoClasica res)
        {
            var meta = new MetaLibro { Libro = ResultadoClasica.LIBRO, Conv = res.Base, S = res.S, DatoUtc = res.Foto.DatoUtc, OiOk = true, Congelada = res.Congelada, ConvTexto = res.Texto };
            if (double.IsNaN(res.Zero)) return (null, null, meta);
            var lv = new[] { new Nivel(NumPy.RoundPy(res.Zero, 2), "Z", double.NaN) };
            var ks = new[] { NumPy.RoundPy(res.ZeroIdx, 2) };
            return (lv, ks, meta);
        }

        /// <summary>4.1.5b: las dominantes de la capa NDX de la clasica: "D1".."D3" en su centroide (round 2) con el GEX del strike ganador en M USD por 1 %
        /// (x100 de NDX: los mismos M que el AUDIT capa=NDX de la clasica); strike = el del indice. null si no hay.</summary>
        public static (Nivel[] Lv, double[] Ks) NivelesDom(ResultadoClasica res)
        {
            if (res?.Doms == null || res.Doms.Count == 0) return (null, null);
            var lv = new Nivel[res.Doms.Count]; var ks = new double[res.Doms.Count];
            for (int q = 0; q < res.Doms.Count; q++)
            {
                var d = res.Doms[q];
                lv[q] = new Nivel(NumPy.RoundPy(d.Pos, 2), "D" + (q + 1), NumPy.RoundPy(d.Gex / 1e6, 1));
                ks[q] = d.K;
            }
            return (lv, ks);
        }

        /// <summary>Pone la serie en su lugar SIN tocar las demas extras (las R20/DOMS que puso la Replica20, o las del archivo).
        /// 4.1.5b: y, si j &gt;= 0, las dominantes de la clasica en su indice.</summary>
        private static void Guardar(RegistroMinuto r, int i, Nivel[] lv, double[] ks, MetaLibro meta, int j = -1, Nivel[] ld = null, double[] kd = null)
        {
            int n = CatalogoFamilia.Extra.Length;
            var ex = new Nivel[n][]; var kk = new double[n][];
            if (r.Extra != null) Array.Copy(r.Extra, ex, Math.Min(n, r.Extra.Length));
            if (r.ExtraStrikes != null) Array.Copy(r.ExtraStrikes, kk, Math.Min(n, r.ExtraStrikes.Length));
            ex[i] = lv; kk[i] = ks;
            if (j >= 0 && j < n) { ex[j] = ld; kk[j] = kd; }
            var metas = new List<MetaLibro>(3);
            if (r.MetaExtra != null) foreach (var m in r.MetaExtra) if (m != null && m.Libro != ResultadoClasica.LIBRO) metas.Add(m);
            if (meta != null) metas.Add(meta);
            r.Extra = ex; r.ExtraStrikes = kk; r.MetaExtra = metas.ToArray();
        }

        private static bool Mismo(Nivel[] a, double[] ka, MetaLibro ma, Nivel[] b, double[] kb, MetaLibro mb)
        {
            if ((a == null) != (b == null)) return false;
            if (a != null)
            {
                if (a.Length != b.Length) return false;
                for (int j = 0; j < a.Length; j++)
                {
                    if (!a[j].Precio.Equals(b[j].Precio) || a[j].Etq != b[j].Etq || !a[j].GexM.Equals(b[j].GexM)) return false;
                    double x = ka != null && j < ka.Length ? ka[j] : double.NaN, y = kb != null && j < kb.Length ? kb[j] : double.NaN;
                    if (!x.Equals(y)) return false;
                }
            }
            if ((ma == null) != (mb == null)) return false;
            return ma == null || (ma.Conv.Equals(mb.Conv) && ma.S.Equals(mb.S) && ma.DatoUtc == mb.DatoUtc && ma.Congelada == mb.Congelada && (ma.ConvTexto ?? "") == (mb.ConvTexto ?? ""));
        }
    }
}
