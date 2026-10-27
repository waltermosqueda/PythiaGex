// Replica20.cs — PythiaGex 4.1.3 (09-10-2026), modulo replica20. Pedido del operador (09-10): "copia la formula de la 2.0 y agregala a lo que ya
// tenemos, que tambien funciona, asi vamos refinando y calibrando". Cuatro series NUEVAS, todas calculadas ADENTRO (no se lee nada de la 2.0:
// ni su log, ni su razon-ES-SPY.txt, ni su base-rueda-ES.json, ni sus estelas; independencia total). Fuera de la cuenta de la vista previa: CONF
// y FAM no las ven.
//
// 5.0 (27-10-2026, P3): LA 2.0 EN UN GRAFICO DE ES (MESZ6). Verificado en su codigo (PythiaGexDos 2.0.6) con la raiz "ES" (GammaHoy.cs:966-973):
//   * en ES la 2.0 NO arma capas (CapaActiva solo con raiz NQ, GammaHoyCapas.cs:437-438) y con 'Libro automatico' (default true, GammaHoy.cs:81)
//     su UNICO libro es CBOE_SPX (GammaHoy.cs:88): la cadena de SPX por BASE ADITIVA, Fut = K + base (GammaHoyNucleo.cs:322, 343). Nada de 'por
//     beta' (eso es la capa SPX dibujada en un grafico de NQ). MEDIDO en su log del 05-10 13:11 ART: "raiz=ES ... libro efectivo=CBOE_SPX/ES".
//   * R20_SPX_vol = ESE libro. Horizonte Hoy; Black-Scholes r 0,0375 SIN dividendo; GEX x 100 (MULT_INDICE); dominantes con los defaults del .ws
//     (CuantasDominantes 2, RadioDominantesPct 2, RadioDominantesMaxPts 100, DominantesReferencia true => sin una por lado ni centroide,
//     GammaHoy.cs:1508; DominantesDeNoche Volumen): las 2 de mayor |GEX vol| a <= min(2 % del futuro, 100 pts) = 100 pts en ES (2 % de 7.700
//     son 154); si ninguna del radio tiene volumen, por OI. NO se reescala nada: es lo que la 2.0 hace en ES (MEDICIONES_ES.md §1.5 punto 7).
//   * SU base, la cascada de CalcularAdentro (GammaHoyNucleo.cs:298-321), cada escalon acotado por el carry del contrato del grafico:
//       carry = fut x (0,0375 - dividendo) x dias al vencimiento del futuro / 365 (vencimiento: Security/codigo, tercer viernes 13:30 UTC,
//       GammaHoy.cs:1587-1647); dividendo = DividendoUsado() = 1,2 % en ES (GammaHoy.cs:1649; la 4.1 tenia el 0,8 % de NQ: DIVIDENDO_ES);
//       cota |b - carry| <= max(0,6 |carry|, 0,0006 fut); la "medida" (y la reciente) tambien vale contra el carry del trimestre siguiente
//       cuando el vencimiento es SUPUESTO (codigo sin mes, GammaHoyNucleo.cs:308):
//       1. "medida": la base de forwards de la cadena cuando es CONFIABLE. La 2.0 lee base/base_confiable del json (pythiagex/base.py:250-255:
//          residuo <= 8 ticks, dispersion <= 12 ticks, >= 5 vencimientos, >= 8 strikes y 0 <= q <= 0,04 con la tasa del Tesoro). Aca sale de la
//          tabla de la descarga (Cboe.IMedidaCboe de P2: la "base"/"base_confiable" de cada linea, la de Python en las de cboe-local y la de
//          CboeCadena.Medir con TasaConfianza 4,1 % en las propias). Si la fuente no tiene esa tabla (un arnes), el respaldo es el residuo
//          <= 8 ticks (OpcionesReplica20.MedidaErrTicks): medido contra el base_confiable de cboe-local, 98,9 % de acuerdo en la rueda y 99,5 %
//          de noche (1.979 cadenas de SPX del 20 al 27-10: ningun confiable perdido, 20 marcadas de mas). Detalle en _diseno/P3_SPY.md.
//       2. "medida reciente": la 2.0 la lee del radar de la nube (base_ultima_buena, si tiene <= 360 min). Con la cadena LOCAL de cboe_local.py
//          (la que la 2.0 usa casi siempre: FeedMinuto y gana la de 'generado' mas nuevo, GammaHoy.cs:950-953) ese campo no viene y la 2.0 salta
//          directo a 3: por eso queda APAGADO por defecto (OpcionesReplica20.MedidaReciente; prendido = la ultima cadena "medida" de <= 360 min).
//       3. "de la rueda" (<= 24 h desde que se adopto): MedirBaseRueda (GammaHoy.cs:1656-1723), port 1:1 en RuedaSpx20/BaseEs20: una muestra por
//          sello NUEVO de CBOE con el sello entre 13:50 y 20:10 UTC (fijo, como la 2.0) y la cadena de <= 30 min; muestra = cierre de la vela de
//          1 min del MES que contiene (sello - 960 s) - spot de SPX; las ultimas 24; mediana alta; MAD; tope max(3 MAD, 0,0002 x precio);
//          con >= 3 buenas la mediana de las buenas; con >= 5 buenas (o sin base todavia) se adopta.
//       4. CRUDA (la de forwards, con la cota); 5. TEORICA (el carry); 6. la CRUDA sin cota.
//   * R20_SPY_vol NO tiene original en ES: la 2.0 solo dibuja SPY con 'Libro automatico' APAGADO y CBOE_ETF a mano (RaizLibro "SPY",
//     GammaHoy.cs:999-1004; MEDIDO: no existe PythiaGex2\razon-ES-SPY.txt). Queda como "seleccion estilo 2.0 sobre SPY con la conversion estilo
//     2.0" (EscalarCon tal cual: vela de 1 min del MES del ultimo trade de la cadena / spot de SPY, guardia del 0,6 %, cordura 5 %, mediana de
//     24); el catalogo la rotula asi y la deja apagada (P5).
//   * DOMS_SPX_vol / DOMS_SPY_vol: la SELECCION de la 2.0 (2 de mayor |GEX vol| a <= min(2 %, 100)) sobre los libros SPX (base C7) y SPY (razon
//     C8) de la 5.0, con la conversion de la 5.0.
//   Diferencias con la 2.0 en ES que NO se pueden evitar (dichas, no escondidas), ademas de las de la 4.1.3 de abajo:
//     * "medida": en las cadenas propias el q se mide con TasaConfianza (4,1 % fijo) en vez de la curva del Tesoro de Python (P2); sin la
//       tabla de la descarga, solo el residuo;
//     * "medida reciente": apagada (la 2.0 la tiene solo cuando la cadena que gano es la del radar de la nube: el log de la clasica, que baja
//       igual, muestra 49-102 AUDIT "medida hace N" por dia el 04-10 y el 17-09 y ninguno el 05-10);
//     * la lista de 24 de la rueda arranca vacia cada dia UTC (la 2.0 la arrastra mientras ATAS siga abierto; ATAS arranca cada mañana) y el
//       "archivo" (base-rueda-ES.json de la 2.0) es la ultima adopcion de los dias anteriores recorridos (DiasHistoria);
//     * la vela de la muestra de la rueda: el ultimo tick de ese minuto en la cinta (la 2.0: la vela del grafico de 1 min); sin ticks de ese
//       minuto, la vela m2 interpolada, marcada provisional y vuelta a mirar (como las razones de SPY);
//     * VENTANA DE LA RUEDA EN UTC FIJO = la de la 2.0 (13:50-20:10 UTC = 09:50-16:10 NY en horario de verano). DESDE EL 01-11-2026 (invierno)
//       queda 08:50-15:10 NY: las muestras de 08:50-09:30 NY comparan el MES con el spot de SPX de la vispera (el indice abre 09:30) y la base
//       de la rueda sale corrida lo que se movio la noche. La 2.0 haria lo mismo (y la replica de la clasica, que usa la misma regla).
//       OpcionesReplica20.RuedaVentanaNy = true la corrige (09:50-16:10 NY); 5.0: el coordinador la dejo en true por defecto (27-10).
//   SIN VERIFICAR contra la 2.0 corriendo en ES: no hay salidas de la 2.0 en ES desde el 05-10 (5 AUDIT ese dia, "base=53.42 origen=medida").
//
// 5.0.1 (27-10-2026): R20_SPX_vol CON LA CONVERSION DE LA 5.0 (OpcionesReplica20.ConversionDeLa50, default true; ajuste publico
//   Replicas50Conversion de "7. Datos"). MEDIDO hoy por el control multifuente (_diseno/CONTROL_MULTIFUENTE_2710.md §4): (1) la "medida" de la
//   cascada sale de los forwards con el corte de una RECTA ajustada a 11 vencimientos (CboeCadena.Medir) y quedo 1,17 pts DEBAJO de la base real
//   (mediana de 51 fotos contra Yahoo ESZ26 - ^GSPC; rango -0,77 a -1,84): el strike SPX 7730 aparecia en 7764,57 y con la base de la 5.0 en
//   7766,12; (2) la base salta 1,1-1,7 pts sin que el mercado se mueva al alternar "medida" (~34,5) y "de la rueda" (~36,0) segun la ultima foto
//   traiga base confiable o no (19 saltos de la raya con el mismo strike desde las 15:15Z). Con la opcion prendida la RAYA va con la base de la
//   5.0 del minuto (RegistroMinuto.MetaDe("SPX").Conv: la sincronizada C7 del libro SPX, la MISMA de MUROS_SPX, el eje doble y TRES_SPX;
//   Base50De): Fut(K) = K + esa base. TODO lo demas queda como en la 2.0, tambien lo que depende de su base: la cascada se sigue calculando y con
//   ella S = fut del instante - base de la 2.0, el radio |K + base de la 2.0 - fut| <= min(2 %, 100), la gamma (Black-Scholes r 0,0375 sin
//   dividendo, x100, horizonte Hoy envejecido), el orden por |GEX| y por eso los MISMOS strikes y montos que la 2.0; la cadena (la ultima bajada
//   con generado <= t) y el fut del instante tampoco cambian. Solo se mueve la raya: K + base de la 5.0 en lugar de K + base de la 2.0 (el texto
//   dice las dos: "seleccion y gamma con la base de la 2.0 X (origen)"). Si la cascada no da base, S y el radio van con la de la 5.0. Sin base de
//   la 5.0 en el minuto (sin libro SPX) la serie no se dibuja, como MUROS_SPX: no se completa con la cascada (mezclar bases es el salto de arriba).
//   MetaLibro "2.0 SPX": Conv = la base de la 5.0 (la de la raya), S = el S de la gamma (fut - base de la 2.0).
//   Apagada = la cascada original EXACTA (mismo numero y mismo texto que la 5.0.0). Calcular, Completar y Rehacer usan la misma regla; al cambiar
//   la opcion (PonerConversionDeLa50) sube la generacion desde el principio y el motor rehace la sesion entera; un minuto del archivo calculado con
//   la otra regla (su texto empieza o no con BaseSpx20.TEXTO_50) se completa con la vigente al cargarlo (IExtrasParciales.Falta + Completar ->
//   Rehacer, que solo toca R20_*: DOMS_* y la clasica quedan como estan).
//
// 4.1.3/4.1.4 (texto original, con los nombres ya de ES por el clon):
//   R20_SPY_vol  "2.0 SPY"  replica de las dominantes de la 2.0 sobre la cadena SPY de CBOE con SU conversion (EscalarCon, GammaHoy.cs:1040-1131):
//                razon = cierre de la vela de 1 min del MES que contiene el ultimo trade de la cadena / spot_idx de la cadena; con la cadena fresca y
//                |pAl/pAhora - 1| > 0,006, la cruda pAhora/spot; sin vela, sus respaldos (ultima valida <= 4 dias, mediana de la rueda, cruda).
//                Fut(K) = K x razon, S = fut / razon.
//   R20_SPX_vol  "2.0 SPX"  la misma seleccion sobre la cadena _SPX de CBOE con la base de la 2.0 (5.0: la cascada completa de arriba). Fut(K) = K + base.
//   DOMS_SPY_vol "SPY dom"  la SELECCION de la 2.0 sobre el libro SPY de la 5.0 (LibroMinuto del minutero SPY: razon sincronizada C8).
//   DOMS_SPX_vol "SPX dom"  la seleccion de la 2.0 sobre el libro SPX de la 5.0 (base C7). Apagada por defecto: sin medir.
// Rol "D1" (la de mayor |GEX|) y "D2"; monto = GEX con signo / 1e6 (M USD por 1 %, escala 1 como SPX/SPY), round 1. Strike en su libro.
// Diferencias con la 2.0 que NO se pueden evitar (4.1.3):
//   * la cuenta se hace una vez por minuto (4.1.4: con el ultimo precio del MES al empezar el minuto); la 2.0 reprecia cada segundo con el precio
//     de ese instante;
//   * la cadena es la que bajo la 5.0 (su descarga y la de cboe-local: de noche el dato es el mismo);
//   * la vela de 1 min sale de la cinta (el ultimo tick antes de que termine ese minuto); si la cinta no tiene los ticks de ese minuto
//     (ATAS cerrado), la vela m2 interpolada, y lo dice el texto de la conversion;
//   * la 5.0 no guarda si la cadena traia ultimo_trade: se toma FotoCadena.DatoUtc (CBOE siempre lo trae; sin el, ts - 902 s).
// 4.1.4 (09-10-2026, arreglos de la revision de la 4.1.3):
//   * la razon de una cadena ya no se escala "una vez y para siempre": las cadenas cuya razon salio SIN la vela de 1 min de la cinta (la cinta
//     todavia cargaba su historia al arrancar) o sin el precio del MES de cuando se bajaron, se vuelven a mirar cada SondeoCadaS s; si la vela
//     aparecio, se rehace el estado entero en orden (lo mismo que daria un arranque con la cinta completa) y, si cambio la razon de alguna cadena,
//     sube Generacion: el motor rehace los minutos ya calculados desde esa cadena (IExtrasRehacer.Rehacer) y los vuelve a guardar. 5.0: lo mismo
//     con las muestras de la rueda de SPX (la base de la 2.0 en ES tiene estado) y Rehacer rehace R20_SPY_vol Y R20_SPX_vol.
//   * la razon de la 2.0 depende del MARCO del grafico donde corre la 2.0 (su "vela del ultimo trade"): esta replica es la de la 2.0 en un grafico
//     de 1 min.
//   * el fut de las R20 (OpcionesReplica20.FutDelInstante, PRENDIDO): el ultimo precio del MES al empezar el minuto en vez del cierre de la ultima
//     vela m2 cerrada (el de todas las series). Es lo mas parecido al "cierre de la vela actual" con que reprecia la 2.0.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace PythiaGexCinco.Familia
{
    /// <summary>La cuenta de la 2.0 que hace falta para sus dominantes, port 1:1 de PythiaGexDos/GammaHoyNucleo.cs (mismas operaciones y mismo orden).</summary>
    public static class Nucleo20
    {
        public const double TASA = 0.0375;               // ajuste "Tasa" del .ws (0,0375)
        public const double MULT = 100.0;                // GammaHoyNucleo.MULT_INDICE (SPX y SPY x 100)
        public const double PISO_DIAS = 1.0 / 1440.0;    // GammaHoyNucleo.PISO_DIAS
        public const double RADIO_PCT = 2.0, RADIO_MAX_PTS = 100.0;   // 5.0: los de la 2.0 en ES (no se reescalan: 100 pts de ES)
        public const int CUANTAS = 2;

        private static double Fi(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);

        /// <summary>GammaHoyNucleo.GammaBs: Black-Scholes SIN dividendo (la r solo entra en d1).</summary>
        public static double GammaBs(double S, double K, double T, double iv, double r)
        {
            if (S <= 0 || K <= 0 || T <= 0 || iv <= 0) return 0;
            var v = iv * Math.Sqrt(T);
            if (v <= 0) return 0;
            var d1 = (Math.Log(S / K) + (r + 0.5 * iv * iv) * T) / v;
            return Fi(d1) / (S * v);
        }

        /// <summary>Feed.Parsear de la 2.0 lee un null del json como 0 (la 5.0 lo guarda NaN).</summary>
        private static double Z(double x) => double.IsNaN(x) ? 0.0 : x;

        /// <summary>GammaHoyNucleo.Gex (libro de CBOE): (gC wC - gP wP) x 100 x S x S x 0,01.</summary>
        public static double Gex(in FilaCadena f, double S, double T, double r, bool porVolumen)
        {
            var gC = GammaBs(S, Z(f.K), T, Z(f.IvC), r);
            var gP = GammaBs(S, Z(f.K), T, Z(f.IvP), r);
            double wC = porVolumen ? Z(f.VolC) : Z(f.OiC), wP = porVolumen ? Z(f.VolP) : Z(f.OiP);
            return (gC * wC - gP * wP) * MULT * S * S * 0.01;
        }

        /// <summary>GammaHoyNucleo.Envejecer: dias desde 'generado', entre 0 y 2 (0 si no hay generado o si ahora &lt;= generado).</summary>
        public static double Envejecer(DateTime generadoUtc, DateTime ahoraUtc)
        {
            if (generadoUtc == default(DateTime) || ahoraUtc <= generadoUtc) return 0;
            return Math.Min(2.0, (ahoraUtc - generadoUtc).TotalDays);
        }

        /// <summary>Una barra del perfil de la 2.0: strike, su precio en el futuro, GEX por volumen y por OI (sin escalar: USD por 1 %).</summary>
        public sealed class Barra { public double K, Fut, Gv, Go; }

        /// <summary>El perfil por K (ascendente) de CalcularAdentro: horizonte Hoy, envejecido desde generadoUtc hasta ahoraUtc, con S en el eje del
        /// libro. porRazon: Fut = K x conv (SPY); si no, Fut = K + conv (SPX). Vacio si la cadena no sirve o S &lt;= 0.</summary>
        public static List<Barra> Perfil(FotoCadena c, DateTime generadoUtc, double S, DateTime ahoraUtc, double conv, bool porRazon)
        {
            var orden = new List<Barra>();
            if (c?.Filas == null || c.Filas.Length == 0 || c.Dias == null || c.Dias.Length == 0) return orden;
            if (!(S > 0)) return orden;
            double envejecer = Envejecer(generadoUtc, ahoraUtc);
            double masCerca = double.MaxValue;
            foreach (var d in c.Dias) { double dd = Z(d) - envejecer; if (dd >= 0 && dd < masCerca) masCerca = dd; }
            if (masCerca == double.MaxValue) masCerca = 0;
            var por = new Dictionary<double, Barra>();
            foreach (var f in c.Filas)
            {
                if (f.V < 0 || f.V >= c.Dias.Length) continue;
                double dias = Z(c.Dias[f.V]) - envejecer;
                if (!(dias >= 0 && dias <= Math.Max(1.0, masCerca + 0.01))) continue;          // PasaHorizonte(Hoy)
                double T = Math.Max(dias, PISO_DIAS) / 365.0;
                double gOi = Gex(f, S, T, TASA, false), gVol = Gex(f, S, T, TASA, true);
                if (gOi == 0 && gVol == 0) continue;
                double k = Z(f.K);
                if (!por.TryGetValue(k, out var s)) { s = new Barra { K = k, Fut = porRazon ? k * conv : k + conv }; por[k] = s; orden.Add(s); }
                s.Go += gOi; s.Gv += gVol;
            }
            orden.Sort((a, b) => a.K.CompareTo(b.K));                                            // por.Values.OrderBy(x => x.K) (K unicos)
            return orden;
        }

        /// <summary>Las dominantes de la 2.0 con DominantesReferencia (sin una por lado, sin centroide): las CUANTAS barras de mayor |gv| con
        /// |Fut - fut| &lt;= min(fut x 2 %, 100) y |gv| &gt; 0, en orden estable; si no hay ninguna, lo mismo con el OI (libroDom = "OI").</summary>
        public static List<(double Fut, double G, double K)> Dominantes(IReadOnlyList<Barra> perfil, double fut, out string libroDom)
        {
            double radio = fut * RADIO_PCT / 100.0;
            if (RADIO_MAX_PTS > 0) radio = Math.Min(radio, RADIO_MAX_PTS);
            libroDom = "vol";
            var cand = perfil.Where(x => Math.Abs(x.Fut - fut) <= radio && Math.Abs(x.Gv) > 0).OrderByDescending(x => Math.Abs(x.Gv)).Take(CUANTAS)
                             .Select(x => (x.Fut, x.Gv, x.K)).ToList();
            if (cand.Count == 0)
            {
                libroDom = "OI";
                cand = perfil.Where(x => Math.Abs(x.Fut - fut) <= radio && Math.Abs(x.Go) > 0).OrderByDescending(x => Math.Abs(x.Go)).Take(CUANTAS)
                             .Select(x => (x.Fut, x.Go, x.K)).ToList();
            }
            return cand;
        }

        /// <summary>La seleccion de la 2.0 sobre un libro de la 5.0 (LibroMinuto ya convertido a ES por la 5.0: Fut, K, Gv, Go en orden de Fut).</summary>
        public static List<(double Fut, double G, double K)> Dominantes(LibroMinuto b, out string libroDom)
        {
            libroDom = "";
            if (b?.Fut == null || b.Gv == null || b.Go == null || b.K == null) return new List<(double, double, double)>();
            var p = new List<Barra>(b.Fut.Length);
            for (int i = 0; i < b.Fut.Length; i++) p.Add(new Barra { K = b.K[i], Fut = b.Fut[i], Gv = b.Gv[i], Go = b.Go[i] });
            return Dominantes(p, b.FutMnq, out libroDom);
        }
    }

    /// <summary>La razon de una cadena de SPY como la 2.0 (EscalarCon) y de donde salio.</summary>
    public sealed class Razon20
    {
        public double Razon = double.NaN;
        public string Origen = "";
        public bool Valida, Congelada, Vieja, PAlDeTick;
        public double PAl = double.NaN, PAhora = double.NaN, Spot = double.NaN, AtrasoUtMin = double.NaN;
        public DateTime UtUtc;                // ultimo trade de la cadena (FotoCadena.DatoUtc)
        public DateTime GenUtc, TsUtc;
    }

    /// <summary>EscalarCon + RazonEtf de la 2.0 para UNA serie de cadenas en orden de llegada (el estado: la ultima razon valida, la mediana de las
    /// ultimas 24 frescas). Sin archivo (la 2.0 guarda razon-ES-SPY.txt): el estado se arma con las cadenas que tiene la 5.0.
    /// 5.0: sin cambios de numero (la 2.0 no tiene nada de ES/SPY aca adentro; la guardia del 0,6 % y la cordura del 5 % son las suyas).</summary>
    public sealed class RazonSpy20
    {
        public const double VIEJA_MIN = 20;              // GammaHoy.RazonCadenaViejaMin
        public int RetrasoSeg = 960;                     // ajuste RetrasoCboeSeg del .ws
        private readonly List<double> _obs = new List<double>();
        public double Rueda = double.NaN;
        public double Ultima = double.NaN;
        public DateTime UltimaUtc = DateTime.MinValue;
        public int Procesadas { get; private set; }
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Una cadena (en orden de 'generado'). ahora = su 'generado' (cuando la bajo la 5.0 = cuando la 2.0 la habria escalado).
        /// cierreVela(t) = cierre de la vela de 1 min del MES abierta a t o antes (NaN si no hay; deTick = si salio de los ticks);
        /// precioEn(t) = el ultimo precio del MES en t (NaN si no hay).</summary>
        public Razon20 Escalar(FotoCadena c, DateTime ahora, Func<DateTime, (double P, bool DeTick)> cierreVela, Func<DateTime, double> precioEn)
        {
            Procesadas++;
            var r = new Razon20 { Spot = c?.Spot ?? double.NaN, GenUtc = ahora, TsUtc = c?.TsUtc ?? default, UtUtc = c?.DatoUtc ?? default };
            if (c == null || !(c.Spot > 0)) { r.Origen = "sin spot en la cadena"; return r; }
            double spot = c.Spot;
            double pAhora = precioEn != null ? precioEn(ahora) : double.NaN;
            if (!(pAhora > 0)) pAhora = 0;
            r.PAhora = pAhora > 0 ? pAhora : double.NaN;
            DateTime ts = c.TsUtc, ut = c.DatoUtc;
            bool hayTs = ts != default(DateTime), hayUt = ut != default(DateTime);
            double edadExtraMin = hayTs ? (ahora - ts).TotalMinutes - RetrasoSeg / 60.0 : double.NaN;
            bool vieja = !hayTs || edadExtraMin > VIEJA_MIN;
            double atrasoUtMin = hayUt ? ((hayTs ? ts : ahora) - ut).TotalMinutes - RetrasoSeg / 60.0 : double.NaN;
            bool congelada = hayUt && atrasoUtMin > VIEJA_MIN;
            r.Vieja = vieja; r.Congelada = congelada; r.AtrasoUtMin = atrasoUtMin;
            string utNy = hayUt ? TiempoFam.ANy(ut).ToString("HH:mm", Inv) : "";
            double pAl = 0;
            if (hayUt || hayTs)
            {
                var (p, deTick) = cierreVela != null ? cierreVela(hayUt ? ut : ts.AddSeconds(-RetrasoSeg)) : (double.NaN, false);
                if (p > 0) { pAl = p; r.PAlDeTick = deTick; }
            }
            r.PAl = pAl > 0 ? pAl : double.NaN;
            double razon = double.NaN; string origen = ""; bool valida = false; DateTime validaUtc = ahora;
            if (hayUt && pAl > 0)
            {
                razon = pAl / spot; valida = true; validaUtc = ut;
                origen = congelada ? "vela alineada al ultimo trade " + utNy + " NY (cadena congelada " + atrasoUtMin.ToString("0", Inv) + " min)"
                                   : "vela alineada (ultimo trade " + utNy + " NY)";
                if (!congelada && !vieja && pAhora > 0 && Math.Abs(pAl / pAhora - 1) > 0.006)
                { razon = pAhora / spot; origen = "CRUDA: la vela alineada es de otro contrato (" + pAl.ToString("0", Inv) + " vs " + pAhora.ToString("0", Inv) + ")"; valida = false; }
            }
            else if (!hayUt && !vieja && pAl > 0)
            {
                razon = pAl / spot; origen = "vela alineada"; valida = true;
                if (pAhora > 0 && Math.Abs(pAl / pAhora - 1) > 0.006)
                { razon = pAhora / spot; origen = "CRUDA: la vela alineada es de otro contrato (" + pAl.ToString("0", Inv) + " vs " + pAhora.ToString("0", Inv) + ")"; valida = false; }
            }
            else
            {
                string porque = hayUt && congelada ? "cadena congelada (ultimo trade " + utNy + " NY) sin vela alineada"
                              : hayUt ? "sin vela alineada al ultimo trade " + utNy + " NY"
                              : !hayTs ? "cadena sin ts" : vieja ? "cadena vieja (" + edadExtraMin.ToString("0", Inv) + " min mas que el retraso normal)" : "sin vela alineada";
                double ult = Ultima; DateTime ultUtc = UltimaUtc;
                if (!double.IsNaN(ult) && ult > 0 && pAhora > 0 && Math.Abs(ult * spot / pAhora - 1) > 0.05)
                {   // cordura: puesta sobre este spot tiene que dar el precio del grafico a menos de 5 %
                    if (Ultima == ult) { Ultima = double.NaN; UltimaUtc = DateTime.MinValue; }
                    ult = double.NaN; ultUtc = DateTime.MinValue;
                }
                bool alineadaMasNueva = pAl > 0 && hayTs && (double.IsNaN(ult) || ult <= 0 || ts > ultUtc);
                if (alineadaMasNueva) { razon = pAl / spot; origen = "vela alineada de una " + porque; valida = true; validaUtc = ts; }
                else if (!double.IsNaN(ult) && ult > 0 && (ahora - ultUtc).TotalDays <= 4) { razon = ult; origen = "ultima valida hace " + (ahora - ultUtc).TotalMinutes.ToString("0", Inv) + " min; " + porque; }
                else if (pAl > 0) { razon = pAl / spot; origen = "vela alineada de una " + porque; valida = true; validaUtc = ts; }
                else if (!double.IsNaN(Rueda) && Rueda > 0) { razon = Rueda; origen = "mediana de la rueda; " + porque; }
                else if (pAhora > 0) { razon = pAhora / spot; origen = "CRUDA sin alinear (sin razon valida previa); " + porque; }
            }
            if (double.IsNaN(razon) || razon <= 0) { r.Origen = "sin razon"; return r; }
            r.Razon = razon; r.Origen = origen; r.Valida = valida;
            if (valida)
            {
                // la mediana de la rueda solo con fotos frescas: ni congeladas ni viejas
                if (!vieja && !congelada)
                {
                    _obs.Add(razon); if (_obs.Count > 24) _obs.RemoveAt(0);
                    var ord = _obs.OrderBy(x => x).ToList(); Rueda = ord[ord.Count / 2];
                }
                if (!(validaUtc < UltimaUtc)) { Ultima = razon; UltimaUtc = validaUtc; }   // RazonEtf.AnotarValida: nunca hacia atras
            }
            return r;
        }
    }

    /// <summary>La base del libro SPX de la 2.0 en un grafico de ES (CalcularAdentro, rama indice con base: GammaHoyNucleo.cs:298-321).</summary>
    public static class BaseSpx20
    {
        /// <summary>DividendoUsado() de la 2.0 en ES: el ajuste 'Dividendo' en 0 -> 1,2 % para ES (0,8 % solo para NQ). GammaHoy.cs:1649.
        /// 5.0: era 0,008 (copiado de NQ). (La replica de la clasica ya no lo usa: tiene su propio 1,2 %, PythiaGexNiveles/GammaHoy.cs:1411.)</summary>
        public const double DIVIDENDO_ES = 0.012;
        /// <summary>5.0: el residuo de la recta de forwards (base_error_ticks) hasta el que la base de la cadena cuenta como "medida" (base.py: 8 ticks).</summary>
        public const double MEDIDA_ERR_TICKS = 8.0;
        /// <summary>"medida reciente": base_ultima_buena con edad &lt;= 360 min (GammaHoyNucleo.cs:315).</summary>
        public const double MEDIDA_RECIENTE_MIN = 360.0;
        /// <summary>"de la rueda": BaseRuedaEdadMin &lt;= 24 h (GammaHoyNucleo.cs:316).</summary>
        public const double RUEDA_VIDA_MIN = 24 * 60;
        /// <summary>5.0: como empieza el texto de cada origen (despues de "base 2.0 X (" en Resultado20.Texto): lo lee la pantalla. Chequear
        /// ORIGEN_MEDIDA_HACE antes que ORIGEN_MEDIDA y "(sin cota)" antes que ORIGEN_CRUDA.</summary>
        public const string ORIGEN_MEDIDA = "medida", ORIGEN_MEDIDA_HACE = "medida hace", ORIGEN_RUEDA = "de la rueda", ORIGEN_CRUDA = "CRUDA",
                            ORIGEN_TEORICA = "TEORICA", MARCA_SIN_COTA = "(sin cota)";
        /// <summary>5.0.1: el origen "conversion de la 5.0" (OpcionesReplica20.ConversionDeLa50) y como EMPIEZA el texto de la replica con esa
        /// conversion ("conversion de la 5.0: base 36.12 (C7, ...) · seleccion y gamma con la base de la 2.0 34.57 (medida: ...)"). Lo leen la
        /// pantalla (ArmadoPantalla) y Falta (un minuto del archivo calculado con la otra regla). El texto con la cascada sigue empezando con "base 2.0 ".</summary>
        public const string ORIGEN_50 = "conversion de la 5.0", TEXTO_50 = "conversion de la 5.0: base ";
        /// <summary>5.0.1: lo que va entre parentesis despues de la base de la 5.0 en el texto, y como empieza la base con que se eligio.</summary>
        public const string DETALLE_50 = "C7, la del libro SPX de la 5.0: la misma de MUROS_SPX, el eje y TRES_SPX";
        public const string SELECCION_50 = "seleccion y gamma con la base de la 2.0 ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>carry = fut x (tasa - dividendo) x dias al vencimiento / 365 (NaN si el vencimiento no es posterior a ahora).</summary>
        public static double Carry(double futuro, DateTime ahoraUtc, DateTime expUtc, double tasa = Nucleo20.TASA, double dividendo = DIVIDENDO_ES)
            => expUtc != default(DateTime) && expUtc > ahoraUtc ? futuro * (tasa - dividendo) * (expUtc - ahoraUtc).TotalDays / 365.0 : double.NaN;

        /// <summary>La cota de la 2.0: |b - k| &lt;= max(0,6 |k|, 0,0006 fut).</summary>
        public static bool Cerca(double b, double k, double futuro) => Math.Abs(b - k) <= Math.Max(Math.Abs(k) * 0.6, futuro * 0.0006);

        /// <summary>La cascada SIN los escalones con estado (la de la 4.1, sin "medida" ni rueda): CRUDA si pasa la cota del carry; si no, el carry
        /// (TEORICA); si no hay carry, la cruda sin cota; si nada, NaN. Queda para los arneses; la replica usa Cascada.</summary>
        public static double Base(FotoCadena c, double futuro, DateTime ahoraUtc, DateTime expUtc, out string origen, out double carry,
                                  double tasa = Nucleo20.TASA, double dividendo = DIVIDENDO_ES)
            => Cascada(c?.BaseCruda ?? double.NaN, c?.BaseErrorTicks ?? double.NaN, double.NaN, "", double.NaN, double.NaN, double.NaN, double.NaN, 0,
                       futuro, ahoraUtc, expUtc, default(DateTime), tasa, dividendo, out origen, out carry);

        /// <summary>5.0: la cascada de la 2.0 en ES (GammaHoyNucleo.cs:314-319), escalon por escalon:
        /// medida (la base medida de la cadena, NaN si no es confiable; pasa la cota, tambien contra el carry alternativo) &gt; medida reciente (ultBuena
        /// con edad &lt;= 360 min, misma cota) &gt; de la rueda (edad &lt;= 24 h, cota del carry) &gt; CRUDA (cota) &gt; TEORICA (el carry) &gt; CRUDA sin cota.
        /// NaN = sin base. medidaDe = de donde salio la medida, para el texto. expAltUtc = el vencimiento del trimestre siguiente cuando el del
        /// grafico es SUPUESTO (default = no hay).</summary>
        public static double Cascada(double cruda, double errTicks, double medida, string medidaDe, double ultBuena, double ultBuenaEdadMin, double rueda, double ruedaEdadMin, int ruedaBuenas,
                                     double futuro, DateTime ahoraUtc, DateTime expUtc, DateTime expAltUtc, double tasa, double dividendo, out string origen, out double carry)
        {
            carry = Carry(futuro, ahoraUtc, expUtc, tasa, dividendo);
            double k0 = carry, k1 = Carry(futuro, ahoraUtc, expAltUtc, tasa, dividendo);
            bool Razonable(double b) => double.IsNaN(k0) || Cerca(b, k0, futuro);
            bool RazonableMedida(double b) => double.IsNaN(k0) || Cerca(b, k0, futuro) || (!double.IsNaN(k1) && Cerca(b, k1, futuro));
            double cr = double.IsNaN(cruda) ? 0 : cruda, er = double.IsNaN(errTicks) ? 0 : errTicks;
            if (!double.IsNaN(medida) && medida != 0 && RazonableMedida(medida))
            { origen = ORIGEN_MEDIDA + ": forwards de la cadena, confiable (" + (string.IsNullOrEmpty(medidaDe) ? "" : medidaDe + "; ") + "residuo " + er.ToString("0.0", Inv) + " ticks)"; return medida; }
            if (!double.IsNaN(ultBuena) && ultBuena != 0 && ultBuenaEdadMin <= MEDIDA_RECIENTE_MIN && RazonableMedida(ultBuena))
            { origen = ORIGEN_MEDIDA_HACE + " " + ultBuenaEdadMin.ToString("0", Inv) + " min (la ultima cadena con la base confiable)"; return ultBuena; }
            if (!double.IsNaN(rueda) && ruedaEdadMin <= RUEDA_VIDA_MIN && Razonable(rueda))
            { origen = ORIGEN_RUEDA + " hace " + ruedaEdadMin.ToString("0", Inv) + " min (mediana de " + ruedaBuenas.ToString(Inv) + " muestras MES - SPX de su regla)"; return rueda; }
            if (cr != 0 && Razonable(cr)) { origen = ORIGEN_CRUDA + " " + er.ToString("0", Inv) + " ticks"; return cr; }
            if (!double.IsNaN(carry)) { origen = ORIGEN_TEORICA + " carry " + carry.ToString("0.0", Inv) + (cr != 0 ? " (cruda " + cr.ToString("0.0", Inv) + " descartada)" : ""); return carry; }
            if (cr != 0) { origen = ORIGEN_CRUDA + " " + er.ToString("0", Inv) + " ticks " + MARCA_SIN_COTA; return cr; }
            origen = "sin base"; return double.NaN;
        }

        /// <summary>GammaHoy.ExpiracionDeCodigo de la 2.0: tercer viernes del mes del codigo, 13:30 UTC; default si el codigo no trae mes.
        /// ahora reemplaza al DateTime.UtcNow de la 2.0 (decada del año de una cifra).</summary>
        public static DateTime ExpiracionDeCodigo(string codigo, DateTime ahoraUtc)
        {
            if (string.IsNullOrEmpty(codigo)) return default(DateTime);
            var limpio = System.Text.RegularExpressions.Regex.Replace(codigo.ToUpperInvariant(), "[^A-Z0-9]", "");
            var m = System.Text.RegularExpressions.Regex.Match(limpio, @"^[A-Z0-9]*?([FGHJKMNQUVXZ])(\d{1,2})$");
            if (!m.Success) return default(DateTime);
            int mes = "FGHJKMNQUVXZ".IndexOf(m.Groups[1].Value[0]) + 1;
            int y = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            int anio = m.Groups[2].Value.Length == 2 ? 2000 + y : (ahoraUtc.Year / 10) * 10 + y;
            if (m.Groups[2].Value.Length == 1 && anio < ahoraUtc.Year - 1) anio += 10;
            var d1 = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
            int haciaViernes = ((int)DayOfWeek.Friday - (int)d1.DayOfWeek + 7) % 7;
            return d1.AddDays(haciaViernes + 14).AddHours(13.5);
        }

        /// <summary>GammaHoy.TrimestralDesde: el tercer viernes trimestral (13:30 UTC) que sigue a la fecha (sin mes en el codigo).</summary>
        public static DateTime TrimestralDesde(DateTime desdeUtc)
        {
            for (int k = 0; k < 8; k++)
            {
                int mes = ((desdeUtc.Month - 1) / 3 + 1 + k) * 3;
                int anio = desdeUtc.Year + (mes - 1) / 12; mes = (mes - 1) % 12 + 1;
                var d1 = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
                int haciaViernes = ((int)DayOfWeek.Friday - (int)d1.DayOfWeek + 7) % 7;
                var venc = d1.AddDays(haciaViernes + 14).AddHours(13.5);
                if (venc > desdeUtc) return venc;
            }
            return default(DateTime);
        }
    }

    /// <summary>5.0: MedirBaseRueda de la 2.0 (PythiaGexDos/GammaHoy.cs:1656-1723): la ventana y la cuenta de UNA muestra, port 1:1.
    /// Propia de esta replica (no usa la de la clasica, que vive en otro paquete y puede cambiar): la regla es la misma letra por letra.</summary>
    public static class RuedaSpx20
    {
        public const int VENTANA = 24, MIN_BUENAS = 5;
        /// <summary>La ventana de la 2.0: "9:50-16:10 de Nueva York" escrito en UTC fijo (13:50-20:10 UTC, el minuto 20:10 entero).</summary>
        public const int INI_MIN_UTC = 13 * 60 + 50, FIN_MIN_UTC = 20 * 60 + 10;
        /// <summary>La misma ventana en hora de NY (RuedaVentanaNy: la que la 2.0 queria; en horario de verano da lo mismo).</summary>
        public const int INI_MIN_NY = 9 * 60 + 50, FIN_MIN_NY = 16 * 60 + 10;
        /// <summary>La cadena tiene que tener &lt;= 30 min ("(ahora - tsCboe).TotalMinutes &lt;= 30").</summary>
        public const double EDAD_CADENA_MAX_MIN = 30;

        /// <summary>El sello de CBOE entra en la ventana de la 2.0 (UTC fijo) o en la de NY.</summary>
        public static bool EnVentana(DateTime tsUtc, bool enNy)
        {
            if (enNy) { var ny = TiempoFam.ANy(tsUtc); int mn = ny.Hour * 60 + ny.Minute; return mn >= INI_MIN_NY && mn <= FIN_MIN_NY; }
            int m = tsUtc.Hour * 60 + tsUtc.Minute;
            return m >= INI_MIN_UTC && m <= FIN_MIN_UTC;
        }

        /// <summary>Una muestra: la agrega a obs (las ultimas 24), saca la mediana alta, el MAD y el tope max(3 MAD, 0,0002 x precio); con &gt;= 3 buenas
        /// la mediana de las buenas; con &gt;= 5 buenas (o sin base) la adopta con la hora 'ahora'. precio = el cierre de la vela.</summary>
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

    /// <summary>5.0: el estado de la base de la 2.0 en ES despues de procesar UNA cadena de SPX (en orden de generado).</summary>
    public sealed class EstadoSpx20
    {
        public DateTime GenUtc, TsUtc;
        public double Spot = double.NaN, Cruda = double.NaN, ErrTicks = double.NaN;
        /// <summary>"medida": la base de forwards de la cadena es confiable (tabla de la descarga, IMedidaCboe; sin tabla, residuo &lt;= MedidaErrTicks).</summary>
        public bool Medida;
        /// <summary>La base medida (NaN si la cadena no es "medida") y de donde salio el "confiable" ("tabla de la descarga" | "aprox.: residuo &lt;= 8").</summary>
        public double MedidaBase = double.NaN; public string MedidaDe = "";
        /// <summary>La ultima "medida" hasta esta cadena (incluida) y su generado: la "medida reciente".</summary>
        public double UltBuena = double.NaN; public DateTime UltBuenaUtc = DateTime.MinValue;
        // ---- la muestra de la rueda de ESTA cadena (MedirBaseRueda)
        /// <summary>La cadena es una muestra de la rueda: sello nuevo en la ventana y cadena de &lt;= 30 min.</summary>
        public bool MuestraRueda;
        public DateTime HoraVelaUtc;
        public double Precio = double.NaN, Muestra = double.NaN;
        /// <summary>false = la cinta no tiene un tick de ese minuto: vela m2 interpolada (o nada); provisional, se vuelve a mirar.</summary>
        public bool PrecioDeTick;
        public double Mediana = double.NaN, Mad = double.NaN; public int Buenas, N; public bool Adopto;
        // ---- la base de la rueda vigente despues de esta cadena
        public double Rueda = double.NaN; public DateTime RuedaUtc = DateTime.MinValue; public int RuedaBuenas;
    }

    /// <summary>5.0: lo que la 2.0 en ES arrastra de cadena en cadena para su base (MedirBaseRueda y la ultima "medida"). Uno por serie de cadenas;
    /// se recorre en orden de generado. Determinista: el mismo orden de cadenas y la misma cinta dan el mismo estado.</summary>
    public sealed class BaseEs20
    {
        private readonly OpcionesReplica20 _op;
        private readonly List<double> _obs = new List<double>();
        private DateTime _obsDia = DateTime.MinValue, _ultTs = DateTime.MinValue;
        private double _rueda = double.NaN; private DateTime _ruedaUtc = DateTime.MinValue; private int _ruedaBuenas;
        private double _ultBuena = double.NaN; private DateTime _ultBuenaUtc = DateTime.MinValue;
        public int Procesadas { get; private set; }
        public int Muestras { get; private set; }

        public BaseEs20(OpcionesReplica20 op) { _op = op ?? new OpcionesReplica20(); }

        /// <summary>Una cadena de SPX. cierreVela(t) = cierre de la vela de 1 min del MES que contiene t (deTick = de un tick de ese minuto).
        /// medidaDe(f) = la base medida de la tabla de la descarga (Hay = la fuente tiene tabla; Base NaN = no confiable); null = sin tabla:
        /// respaldo con el residuo de la recta (base_error_ticks &lt;= MedidaErrTicks).</summary>
        public EstadoSpx20 Procesar(FotoCadena f, DateTime genUtc, Func<DateTime, (double P, bool DeTick)> cierreVela, Func<FotoCadena, (bool Hay, double Base)> medidaDe = null)
        {
            Procesadas++;
            var e = new EstadoSpx20 { GenUtc = genUtc, TsUtc = f?.TsUtc ?? default, Spot = f?.Spot ?? double.NaN, Cruda = f?.BaseCruda ?? double.NaN, ErrTicks = f?.BaseErrorTicks ?? double.NaN };
            if (f != null)
            {
                (bool Hay, double Base) tm = (false, double.NaN);
                if (medidaDe != null) try { tm = medidaDe(f); } catch { tm = (false, double.NaN); }
                if (tm.Hay)
                {
                    e.MedidaDe = "tabla de la descarga";
                    e.Medida = !double.IsNaN(tm.Base) && tm.Base != 0;
                    e.MedidaBase = e.Medida ? tm.Base : double.NaN;
                }
                else
                {
                    e.MedidaDe = "aprox.: residuo <= " + _op.MedidaErrTicks.ToString("0.#", CultureInfo.InvariantCulture) + " ticks";
                    e.Medida = !double.IsNaN(e.Cruda) && e.Cruda != 0 && !double.IsNaN(e.ErrTicks) && e.ErrTicks <= _op.MedidaErrTicks;
                    e.MedidaBase = e.Medida ? e.Cruda : double.NaN;
                }
                if (e.Medida) { _ultBuena = e.MedidaBase; _ultBuenaUtc = genUtc; }
                // MedirBaseRueda: UNA muestra por sello nuevo, en la ventana, con la cadena fresca y el spot del indice
                if (f.TsUtc != default(DateTime) && f.TsUtc != _ultTs && f.Spot > 0 && RuedaSpx20.EnVentana(f.TsUtc, _op.RuedaVentanaNy)
                    && (genUtc - f.TsUtc).TotalMinutes <= RuedaSpx20.EDAD_CADENA_MAX_MIN)
                {
                    _ultTs = f.TsUtc;                                       // la 2.0 marca el sello antes de buscar la vela
                    e.MuestraRueda = true;
                    e.HoraVelaUtc = f.TsUtc.AddSeconds(-Math.Max(0, _op.RetrasoCboeSeg));
                    var (p, deTick) = cierreVela != null ? cierreVela(e.HoraVelaUtc) : (double.NaN, false);
                    e.Precio = p; e.PrecioDeTick = deTick && p > 0;
                    if (p > 0)
                    {
                        var dia = f.TsUtc.Date;                              // la lista de 24 arranca vacia cada dia UTC (ATAS arranca cada mañana)
                        if (dia != _obsDia) { _obs.Clear(); _obsDia = dia; }
                        e.Muestra = p - f.Spot;
                        RuedaSpx20.Paso(_obs, e.Muestra, p, genUtc, ref _rueda, ref _ruedaUtc, out var med, out var mad, out var buenas, out var adopto);
                        e.Mediana = med; e.Mad = mad; e.Buenas = buenas; e.N = _obs.Count; e.Adopto = adopto;
                        if (adopto) _ruedaBuenas = buenas;
                        Muestras++;
                    }
                }
            }
            e.UltBuena = _ultBuena; e.UltBuenaUtc = _ultBuenaUtc;
            e.Rueda = _rueda; e.RuedaUtc = _ruedaUtc; e.RuedaBuenas = _ruedaBuenas;
            return e;
        }
    }

    public sealed class OpcionesReplica20
    {
        /// <summary>Codigo del grafico (MESZ6): el vencimiento del futuro para la cota de la base SPX (como la 2.0 con el codigo del instrumento).</summary>
        public string Contrato = "";
        public double Tasa = Nucleo20.TASA, Dividendo = BaseSpx20.DIVIDENDO_ES;
        public int RetrasoCboeSeg = 960;
        /// <summary>Dias de cadenas previos al inicio de la sesion que se recorren (estado de la razon de SPY y de la base de SPX: ultima valida,
        /// medianas de la rueda; el lunes, la del viernes).</summary>
        public int DiasHistoria = 6;
        public Action<string> Log;
        /// <summary>4.1.4 (revision 4.1.3): cada cuantos segundos (reloj de la PC) se vuelven a mirar en la cinta las cadenas cuya razon (SPY) o muestra
        /// de la rueda (SPX, 5.0) salio SIN la vela de 1 min de sus ticks (cinta cargando al arrancar, ATAS cerrado a esa hora): si la vela aparecio,
        /// se rehace el estado entero en orden (como un arranque con la cinta completa) y el motor rehace esos minutos. 0 = en cada llamada (arnes).</summary>
        public int SondeoCadaS = 20;
        /// <summary>4.1.4 (revision 4.1.3, hallazgo del precio): true (default) = las R20 usan el ultimo precio del MES al empezar el minuto (lo mas parecido
        /// a la 2.0, que reprecia con el precio de cada instante: "cierre de la vela actual"); false = el fut del minuto de la 5.0 (el cierre de la ultima
        /// vela m2 cerrada, el de todas las series). Las rayas no se mueven (son strike x razon / strike + base): cambia el monto y, en el borde de
        /// los 100 pts, cual strike entra.</summary>
        public bool FutDelInstante = true;
        /// <summary>5.0: true (default) = la "medida" sale de la tabla de la descarga (Cboe.IMedidaCboe, si la fuente la tiene: el base_confiable de
        /// cada linea); false o fuente sin tabla = el respaldo por el residuo (MedidaErrTicks).</summary>
        public bool UsarTablaMedida = true;
        /// <summary>5.0: residuo (base_error_ticks) hasta el que la base de forwards de una cadena de SPX cuenta como "medida" cuando la fuente no tiene
        /// la tabla (pythiagex/base.py: 8).</summary>
        public double MedidaErrTicks = BaseSpx20.MEDIDA_ERR_TICKS;
        /// <summary>5.0: el escalon "medida reciente" (la ultima cadena medida de &lt;= 360 min, reconstruida). false (default) = como la 2.0 con la cadena
        /// LOCAL de cboe_local.py, la que usa casi siempre (FeedMinuto y gana la de generado mas nuevo; ese json no trae base_ultima_buena): de
        /// "medida" pasa directo a "de la rueda". true = como la 2.0 cuando gana la cadena del radar de la nube. Medido el 26-10 con true: 355 de
        /// 390 minutos de la rueda salian de "medida hace N" y quedaban 1,46 pts por debajo de la rueda (mediana; |max| 3,2).</summary>
        public bool MedidaReciente = false;
        /// <summary>5.0: false (default) = la ventana de la rueda de la 2.0 en UTC fijo (13:50-20:10); true = 09:50-16:10 hora de NY (la que la 2.0
        /// queria). Iguales hasta el 31-10; desde el 01-11 la de UTC empieza 08:50 NY, con el SPX todavia cerrado.</summary>
        public bool RuedaVentanaNy = true;   // 5.0 (27-10-2026, agente principal): en hora de NY por defecto (igual hasta el 31-10; desde el 01-11 la de UTC
                                             // empezaria 8:50 NY con el SPX cerrado). La 2.0 original queda con su UTC fijo.
        /// <summary>5.0.1 (27-10-2026): true (default) = R20_SPX_vol lleva sus strikes a ES con la CONVERSION DE LA 5.0: la raya en Fut = K + la base
        /// SPX del minuto del libro SPX de la 5.0 (sincronizada C7; RegistroMinuto.MetaDe("SPX").Conv, la de MUROS_SPX, el eje doble y TRES_SPX). La
        /// seleccion queda la de la 2.0: S, el radio de 100 y el orden por |GEX| con SU base (su cascada), la formula, T/r, la cadena y el fut del
        /// instante: los mismos strikes y montos, otra altura. Medido 27-10 (control multifuente): la base de la cascada de la 2.0 quedo 1,17 pts debajo
        /// de la real (mediana de 51 fotos contra Yahoo; -0,77 a -1,84) y salto 1,1-1,7 pts al alternar "medida" y "de la rueda" (19 saltos desde las
        /// 15:15Z con el strike quieto). Sin base de la 5.0 en el minuto no se dibuja. false = la cascada de la 2.0 EXACTA (la de la 5.0.0). La cambia
        /// el host con Replica20.PonerConversionDeLa50.</summary>
        public bool ConversionDeLa50 = true;
    }

    /// <summary>Lo que dio una replica de la 2.0 en un instante (para el motor y el arnes).</summary>
    public sealed class Resultado20
    {
        public string Libro;                                   // "2.0 SPY" | "2.0 SPX"
        public FotoCadena Foto; public DateTime GeneradoUtc;
        public double Conv = double.NaN, S = double.NaN, Fut = double.NaN;
        public string LibroDom = "", Texto = "";
        public bool Congelada;
        public int Strikes;
        public List<(double Fut, double G, double K)> Doms = new List<(double, double, double)>();
        public Razon20 Razon;                                  // solo SPY
        public string OrigenBase = ""; public double Carry = double.NaN;   // solo SPX
        public EstadoSpx20 Base20;                             // 5.0, solo SPX: el estado de la base de la 2.0 (rueda, ultima medida) en esa cadena
        /// <summary>5.0.1, solo SPX: Conv salio de la conversion de la 5.0 (OpcionesReplica20.ConversionDeLa50; S sigue siendo el de la gamma: fut -
        /// BaseOriginal); BaseOriginal / OrigenOriginal = lo que daba la cascada de la 2.0 en ese instante (NaN / "sin base" si no tenia), con la que se
        /// eligio. Con la cascada, BaseOriginal = Conv.</summary>
        public bool Con50;
        public double BaseOriginal = double.NaN; public string OrigenOriginal = "";
    }

    /// <summary>Las series "como la 2.0" de cada minuto (IExtrasMinuto del motor). Un solo hilo (el del motor); lock por si acaso.
    /// 4.1.4 (revision 4.1.3): IExtrasRehacer: si una cadena se escalo sin la vela de la cinta (la cinta todavia cargaba) y la vela llega despues, se
    /// rehace el estado en orden y el motor rehace los minutos ya calculados desde esa cadena. 5.0: tambien la base de SPX (su rueda).
    /// 5.0.1: IExtrasParciales: un minuto del archivo cuyo R20_SPX_vol se calculo con la otra conversion (5.0 / cascada) se completa con la vigente.</summary>
    public sealed class Replica20 : IExtrasMinuto, IExtrasRehacer, IExtrasParciales
    {
        public const string VERSION = "replica20 5.0.1 (27-10-2026, R20_SPX_vol con la conversion de la 5.0 por defecto)";
        public const string LIBRO_SPY = "2.0 SPY", LIBRO_SPX = "2.0 SPX";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly IFuenteCboe _cboe;
        private readonly ICinta _cinta;
        private readonly OpcionesReplica20 _op;
        private readonly object _llave = new object();

        // SPY: solo las CLAVES (generado, sello) y la razon de cada cadena; la cadena misma se le pide a la fuente cuando hace falta (la fuente le
        // saca las filas a las de mas de 30 h: guardar aca las fotos completas las retendria en memoria)
        private readonly List<(DateTime Gen, DateTime Ts)> _vistas = new List<(DateTime, DateTime)>();   // tal como las dio la fuente (si solo crecio)
        private readonly List<DateTime> _tsq = new List<DateTime>();          // sello de cada cadena aceptada (en orden de generado)
        private readonly List<long> _gq = new List<long>();                    // generado al segundo (ms)
        private readonly List<Razon20> _rq = new List<Razon20>();
        private RazonSpy20 _estado;
        private long _ver = long.MinValue;
        private DateTime _desde = DateTime.MinValue;
        // 4.1.4: por cadena aceptada (mismo indice que _rq): su razon salio sin la vela de la cinta (o sin el precio de la cinta al bajarla)
        private readonly List<bool> _provVela = new List<bool>(), _provAhora = new List<bool>();
        private int _nProv;
        private DateTime _ultSondeoUtc = DateTime.MinValue;

        // SPX (5.0): la base de la 2.0 en ES tiene estado (su rueda y la ultima "medida"): por cadena aceptada, el estado despues de procesarla
        private readonly List<(DateTime Gen, DateTime Ts)> _vistasSpx = new List<(DateTime, DateTime)>();
        private readonly List<FotoCadena> _fn = new List<FotoCadena>();
        private readonly List<long> _gn = new List<long>();
        private readonly List<EstadoSpx20> _en = new List<EstadoSpx20>();
        private readonly List<int> _provSpx = new List<int>();                // indices de _en cuya muestra de la rueda salio sin un tick de su minuto
        private BaseEs20 _baseEs;
        private readonly Cboe.IMedidaCboe _tabla;                             // 5.0: la base medida de cada cadena (la tabla de la descarga de P2); null = sin tabla

        // IExtrasRehacer: la generacion y desde cuando rehacer (el minimo de lo que cambio desde la ultima vez que el motor leyo la generacion)
        private long _gen, _genLeida;
        private DateTime _rehacerDesde = DateTime.MaxValue;

        public int Reprocesos { get; private set; }
        /// <summary>4.1.4: veces que el sondeo encontro en la cinta la vela (o el precio) que faltaba al escalar alguna cadena y rehizo el estado.</summary>
        public int Resondeos { get; private set; }
        /// <summary>5.0: lo mismo para la base de SPX (la serie de cadenas se rehizo entera / por el sondeo de la rueda).</summary>
        public int ReprocesosSpx { get; private set; }
        public int ResondeosSpx { get; private set; }
        /// <summary>4.1.4 (IExtrasRehacer): sube cada vez que cambia la razon (o su origen) de una cadena de SPY ya escalada, o (5.0) la base de la rueda
        /// de una cadena de SPX ya procesada.</summary>
        public long Generacion { get { lock (_llave) { AplicarPedido50(); _genLeida = _gen; return _gen; } } }   // 5.0.1: con el pedido de conversion aplicado
        /// <summary>4.1.4 (IExtrasRehacer): el 'generado' de la primera cadena que cambio desde la ultima lectura de Generacion (desde ahi se rehacen).</summary>
        public DateTime RehacerDesdeUtc { get { lock (_llave) return _rehacerDesde; } }
        /// <summary>4.1.4: cadenas cuya razon todavia espera la vela de la cinta.</summary>
        public int Provisionales { get { lock (_llave) return _nProv; } }
        /// <summary>5.0: muestras de la rueda de SPX que todavia esperan un tick de su minuto en la cinta.</summary>
        public int ProvisionalesSpx { get { lock (_llave) return _provSpx.Count; } }
        public int FotosSpy { get { lock (_llave) return _gq.Count; } }
        public int FotosSpx { get { lock (_llave) return _fn.Count; } }

        public Replica20(IFuenteCboe cboe, ICinta cinta, OpcionesReplica20 op = null)
        {
            _cboe = cboe ?? throw new ArgumentNullException(nameof(cboe));
            _cinta = cinta;
            _op = op ?? new OpcionesReplica20();
            _estado = new RazonSpy20 { RetrasoSeg = _op.RetrasoCboeSeg };
            _baseEs = new BaseEs20(_op);
            _tabla = _op.UsarTablaMedida ? cboe as Cboe.IMedidaCboe : null;
        }

        /// <summary>5.0.1: la conversion de R20_SPX_vol: true = la de la 5.0 (OpcionesReplica20.ConversionDeLa50; con un pedido pendiente, ya aplicado).</summary>
        public bool ConversionDeLa50 { get { lock (_llave) { AplicarPedido50(); return _op.ConversionDeLa50; } } }

        // 5.0.1: el valor pedido por el ajuste (-1 = ninguno, 0 = cascada, 1 = la de la 5.0). Lo escribe el hilo de ATAS sin llave; lo aplica el del motor.
        private int _pedido50 = -1;

        /// <summary>5.0.1: pide la conversion de R20_SPX_vol (el ajuste Replicas50Conversion). La llama el host en CADA latido desde el hilo de ATAS:
        /// solo anota el valor (sin llave: el hilo del motor puede tener la llave segundos, rehaciendo el estado al arrancar). Lo aplica el hilo del
        /// motor en la proxima cuenta o lectura de la generacion (AplicarPedido50): si cambia, sube la generacion desde el principio y el motor rehace
        /// TODOS los minutos calculados de la sesion con la regla nueva (Rehacer) y los vuelve a guardar, asi la estela no mezcla las dos conversiones.</summary>
        public void PonerConversionDeLa50(bool v) => Volatile.Write(ref _pedido50, v ? 1 : 0);

        /// <summary>5.0.1: aplica el pedido de PonerConversionDeLa50 si cambia la regla (bajo _llave).</summary>
        private void AplicarPedido50()
        {
            int p = Volatile.Read(ref _pedido50);
            if (p < 0 || (p == 1) == _op.ConversionDeLa50) return;
            _op.ConversionDeLa50 = p == 1;
            Subir(DateTime.MinValue);
            _op.Log?.Invoke("SPX: conversion de R20_SPX_vol -> " + (p == 1 ? "la de la 5.0 (la base SPX del minuto del libro SPX de la 5.0)" : "la cascada de la 2.0 (la original)")
                            + "; generacion " + _gen + ": se rehace la sesion entera");
        }

        /// <summary>5.0.1: la base SPX -&gt; ES de la 5.0 en un minuto: la del libro SPX de la 5.0 (RegistroMinuto.Meta, la sincronizada C7: la misma
        /// que usan MUROS_SPX, el eje doble y TRES_SPX; en el archivo, el "c" del "m" de SPX). NaN si el minuto no tiene libro SPX.</summary>
        public static double Base50De(RegistroMinuto r)
        {
            var m = r?.MetaDe("SPX");
            return m != null && EsBase(m.Conv) ? m.Conv : double.NaN;
        }

        private static bool EsBase(double b) => !double.IsNaN(b) && !double.IsInfinity(b);

        /// <summary>5.0.1 (IExtrasParciales): el R20_SPX_vol del minuto (archivo) se calculo con la OTRA conversion que la vigente (su texto empieza o
        /// no con BaseSpx20.TEXTO_50). Un minuto sin "2.0 SPX" no se toca.</summary>
        public bool Falta(RegistroMinuto r)
        {
            var m = r?.MetaExtraDe(LIBRO_SPX);
            if (m == null) return false;
            bool es50 = (m.ConvTexto ?? "").StartsWith(BaseSpx20.TEXTO_50, StringComparison.Ordinal);
            lock (_llave) { AplicarPedido50(); return es50 != _op.ConversionDeLa50; }
        }

        /// <summary>5.0: la base medida de una cadena segun la tabla de la descarga: (true, base) si es confiable, (true, NaN) si no (o no esta).</summary>
        private (bool Hay, double Base) MedidaDeTabla(FotoCadena f)
        {
            var m = _tabla?.Medida(f);
            return (true, m != null && m.Confiable && !double.IsNaN(m.Base) && m.Base != 0 ? m.Base : double.NaN);
        }

        private static DateTime Seg(DateTime d) => new DateTime(d.Ticks - d.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);

        /// <summary>Sube la generacion: desde = el minimo de lo cambiado desde que el motor leyo la ultima (si ya la leyo, arranca de nuevo).</summary>
        private void Subir(DateTime desdeUtc)
        {
            if (_genLeida == _gen) _rehacerDesde = desdeUtc;
            else if (desdeUtc < _rehacerDesde) _rehacerDesde = desdeUtc;
            _gen++;
        }

        // ------------------------------------------------------------------ la cinta (lo que en la 2.0 es el grafico de 1 min)
        /// <summary>GammaHoy.BarraDeExacta + GetCandle(b).Close: el cierre de la vela de 1 min abierta a t o antes (a lo sumo 4 h) = el ultimo tick
        /// antes de que termine el minuto de t. Sin los ticks de ese tramo en la cinta: la vela m2 interpolada (deTick = false).</summary>
        public (double P, bool DeTick) CierreVela1m(DateTime tUtc)
        {
            if (_cinta == null) return (double.NaN, false);
            var fin = new DateTime((tUtc.Ticks / TimeSpan.TicksPerMinute + 1) * TimeSpan.TicksPerMinute - TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
            double p = double.NaN;
            try { p = _cinta.PrecioSoloTick(fin, 4 * 3600); } catch { }
            if (p > 0) return (p, true);
            try { p = _cinta.Precio(fin); } catch { p = double.NaN; }
            return (p > 0 ? p : double.NaN, false);
        }

        /// <summary>5.0: la vela de la muestra de la rueda de la 2.0 (BarraDe(sello - 960 s) + GetCandle(b).Close en el grafico de 1 min): el ultimo tick
        /// DE ESE MINUTO en la cinta (deTick = true). No se acepta un tick de antes (con un hueco de la cinta, PrecioSoloTick a 4 h daria el de antes
        /// del hueco y la muestra quedaria corrida para siempre): sin un tick de ese minuto, la vela m2 interpolada (deTick = false: provisional,
        /// se vuelve a mirar). NaN si nada.</summary>
        public (double P, bool DeTick) CierreVela1mRueda(DateTime tUtc)
        {
            if (_cinta == null) return (double.NaN, false);
            var ini = new DateTime((tUtc.Ticks / TimeSpan.TicksPerMinute) * TimeSpan.TicksPerMinute, DateTimeKind.Utc);
            var fin = ini.AddMinutes(1).AddTicks(-TimeSpan.TicksPerMillisecond);
            double p = double.NaN;
            try { p = _cinta.PrecioSoloTick(fin, 60); } catch { }
            if (p > 0) return (p, true);
            try { p = _cinta.Precio(fin); } catch { p = double.NaN; }
            return (p > 0 ? p : double.NaN, false);
        }

        /// <summary>GetCandle(CurrentBar - 1).Close en el momento de escalar: el ultimo precio del MES en t.</summary>
        public double PrecioEn(DateTime tUtc)
        {
            if (_cinta == null) return double.NaN;
            double p = double.NaN;
            try { p = _cinta.PrecioSoloTick(tUtc, 4 * 3600); } catch { }
            if (p > 0) return p;
            try { p = _cinta.Precio(tUtc); } catch { p = double.NaN; }
            return p > 0 ? p : double.NaN;
        }

        // ------------------------------------------------------------------ las cadenas
        /// <summary>Trae las cadenas de SPY y SPX si cambio la fuente o la sesion, y corre la razon de la 2.0 sobre las SPY nuevas y (5.0) la base de
        /// la 2.0 sobre las SPX nuevas, en orden. Si la fuente cambio el principio de la lista (historia que llego tarde) o el sondeo encontro la vela
        /// que faltaba, rehace el estado de ese libro desde el principio (causal y determinista).</summary>
        private void Refrescar(DateTime tUtc)
        {
            AplicarPedido50();                                                     // 5.0.1: el pedido de conversion del ajuste, en el hilo del motor
            var desde = TiempoFam.IniSesion(TiempoFam.Sesion(tUtc)).AddDays(-_op.DiasHistoria);
            long ver = _cboe.Version;
            // 4.1.4 (revision 4.1.3): las cadenas "provisionales" se vuelven a mirar cada SondeoCadaS s: si la vela (o el precio) aparecio, se rehace
            // todo el estado de ese libro en orden, igual que un arranque con la cinta completa.
            var (porQueSpy, porQueSpx) = SondeoProvisionales();
            if (porQueSpy == null && porQueSpx == null && ver == _ver && desde == _desde) return;
            bool otraVentana = desde != _desde;
            _ver = ver; _desde = desde;
            RefrescarSpy(desde, otraVentana, porQueSpy);
            RefrescarSpx(desde, otraVentana, porQueSpx);
        }

        private void RefrescarSpy(DateTime desde, bool otraVentana, string porQue)
        {
            // SPY: la razon se escala UNA vez por cadena, en orden (como la 2.0 al bajar cada una); si la fuente solo agrego al final, se siguen
            var lq = _cboe.Fotos("SPY", desde, DateTime.MaxValue) ?? Array.Empty<FotoCadena>();
            var nuevas = new List<FotoCadena>(lq.Count);
            foreach (var f in lq) if (f != null && f.TsUtc != default(DateTime)) nuevas.Add(f);
            int nv = _vistas.Count;
            bool prefijo = porQue == null && !otraVentana && nv <= nuevas.Count && (nv == 0 || (Clave(nuevas[0]) == _vistas[0] && Clave(nuevas[nv - 1]) == _vistas[nv - 1]));
            Dictionary<(long, DateTime), (double, string)> antes = null;
            if (!prefijo)
            {
                if (_gq.Count > 0) { Reprocesos++; if (porQue != null) Resondeos++; }
                antes = new Dictionary<(long, DateTime), (double, string)>(_gq.Count);
                for (int i = 0; i < _gq.Count; i++) antes[(_gq[i], _tsq[i])] = (_rq[i].Razon, _rq[i].Origen);
                _vistas.Clear(); _tsq.Clear(); _gq.Clear(); _rq.Clear(); _provVela.Clear(); _provAhora.Clear(); _nProv = 0;
                _estado = new RazonSpy20 { RetrasoSeg = _op.RetrasoCboeSeg };
            }
            int n0 = _vistas.Count, a0 = _gq.Count;
            for (int i = n0; i < nuevas.Count; i++)
            {
                var f = nuevas[i]; var gen = Seg(f.GeneradoUtc);
                _vistas.Add(Clave(f));
                if (_gq.Count > 0 && TiempoFam.Ms(gen) < _gq[_gq.Count - 1]) continue;        // fuera de orden (la fuente ya las da ordenadas)
                _tsq.Add(f.TsUtc); _gq.Add(TiempoFam.Ms(gen));
                var rz = _estado.Escalar(f, gen, CierreVela1m, PrecioEn);
                _rq.Add(rz);
                bool pv = !(rz.PAl > 0 && rz.PAlDeTick) && (rz.UtUtc != default(DateTime) || rz.TsUtc != default(DateTime)), pa = !SinPrecioAhora(rz) && !TickEn(gen);
                _provVela.Add(pv); _provAhora.Add(pa); if (pv || pa) _nProv++;
            }
            if (a0 == 0 || !prefijo) _ultSondeoUtc = DateTime.UtcNow;          // recien escaladas: el proximo sondeo despues de SondeoCadaS
            // 4.1.4: que cadenas ya escaladas cambiaron de razon (o de origen) con el estado rehecho: desde la primera, el motor rehace los minutos
            if (antes != null && antes.Count > 0)
            {
                int primera = -1;
                for (int i = 0; i < _gq.Count && primera < 0; i++)
                    if (antes.TryGetValue((_gq[i], _tsq[i]), out var vj) && (!vj.Item1.Equals(_rq[i].Razon) || vj.Item2 != _rq[i].Origen)) primera = i;
                if (primera >= 0)
                {
                    var desdeCambio = TiempoFam.DeMs(_gq[primera]);
                    Subir(desdeCambio);
                    _op.Log?.Invoke("SPY: razon de la 2.0 REHECHA (" + (porQue ?? (otraVentana ? "otra ventana de cadenas" : "la fuente cambio el principio de la lista")) + "): la primera cadena que cambio es la de "
                                    + desdeCambio.ToString("MM-dd HH:mm:ss", Inv) + "Z, " + NumFam.F(antes[(_gq[primera], _tsq[primera])].Item1, "0.000000") + " -> " + NumFam.F(_rq[primera].Razon, "0.000000")
                                    + " (" + _rq[primera].Origen + "); generacion " + _gen + ", " + _nProv + " cadenas siguen sin la vela");
                }
            }
            if (_gq.Count > a0 && _op.Log != null)
            {
                var u = _rq[_rq.Count - 1];
                _op.Log("SPY: " + (_gq.Count - a0) + " cadenas nuevas" + (prefijo ? "" : " (estado rehecho desde " + desde.ToString("MM-dd HH:mm", Inv) + "Z)") + "; ultima "
                        + u.GenUtc.ToString("MM-dd HH:mm:ss", Inv) + "Z razon 2.0 " + NumFam.F(u.Razon, "0.0000") + " (" + u.Origen + "), MES vela " + NumFam.F(u.PAl, "0.00")
                        + (u.PAlDeTick ? "" : " (m2 interpolada)") + " / spot " + NumFam.F(u.Spot, "0.00") + (_nProv > 0 ? "; " + _nProv + " cadenas sin la vela de la cinta (se vuelven a mirar)" : ""));
            }
        }

        /// <summary>5.0: las cadenas de SPX en orden, con el estado de la base de la 2.0 (su rueda y la ultima "medida") despues de cada una.</summary>
        private void RefrescarSpx(DateTime desde, bool otraVentana, string porQue)
        {
            var ln = _cboe.Fotos("SPX", desde, DateTime.MaxValue) ?? Array.Empty<FotoCadena>();
            var nuevas = new List<FotoCadena>(ln.Count);
            foreach (var f in ln) if (f != null) nuevas.Add(f);
            int nv = _vistasSpx.Count;
            bool prefijo = porQue == null && !otraVentana && nv <= nuevas.Count && (nv == 0 || (Clave(nuevas[0]) == _vistasSpx[0] && Clave(nuevas[nv - 1]) == _vistasSpx[nv - 1]));
            Dictionary<(long, DateTime), (double, DateTime)> antes = null;
            if (!prefijo)
            {
                if (_gn.Count > 0) { ReprocesosSpx++; if (porQue != null) ResondeosSpx++; }
                antes = new Dictionary<(long, DateTime), (double, DateTime)>(_gn.Count);
                for (int i = 0; i < _gn.Count; i++) antes[(_gn[i], _fn[i].TsUtc)] = (_en[i].Rueda, _en[i].RuedaUtc);
                _vistasSpx.Clear(); _fn.Clear(); _gn.Clear(); _en.Clear(); _provSpx.Clear();
                _baseEs = new BaseEs20(_op);
            }
            int n0 = _vistasSpx.Count, a0 = _gn.Count;
            for (int i = n0; i < nuevas.Count; i++)
            {
                var f = nuevas[i]; var gen = Seg(f.GeneradoUtc);
                _vistasSpx.Add(Clave(f));
                long g = TiempoFam.Ms(gen);
                if (_gn.Count > 0 && g < _gn[_gn.Count - 1]) continue;              // fuera de orden (la fuente ya las da ordenadas)
                var e = _baseEs.Procesar(f, gen, CierreVela1mRueda, _tabla == null ? null : (Func<FotoCadena, (bool, double)>)MedidaDeTabla);
                _fn.Add(f); _gn.Add(g); _en.Add(e);
                if (e.MuestraRueda && !e.PrecioDeTick) _provSpx.Add(_en.Count - 1);
            }
            if (a0 == 0 || !prefijo) _ultSondeoUtc = DateTime.UtcNow;
            if (antes != null && antes.Count > 0)
            {
                int primera = -1;
                for (int i = 0; i < _gn.Count && primera < 0; i++)
                    if (antes.TryGetValue((_gn[i], _fn[i].TsUtc), out var vj) && (!vj.Item1.Equals(_en[i].Rueda) || vj.Item2 != _en[i].RuedaUtc)) primera = i;
                if (primera >= 0)
                {
                    var desdeCambio = TiempoFam.DeMs(_gn[primera]);
                    Subir(desdeCambio);
                    _op.Log?.Invoke("SPX: base de la rueda de la 2.0 REHECHA (" + (porQue ?? (otraVentana ? "otra ventana de cadenas" : "la fuente cambio el principio de la lista")) + "): la primera cadena que cambio es la de "
                                    + desdeCambio.ToString("MM-dd HH:mm:ss", Inv) + "Z, " + NumFam.F(antes[(_gn[primera], _fn[primera].TsUtc)].Item1, "0.00") + " -> " + NumFam.F(_en[primera].Rueda, "0.00")
                                    + "; generacion " + _gen + ", " + _provSpx.Count + " muestras siguen sin un tick de su minuto");
                }
            }
            if (_gn.Count > a0 && _op.Log != null)
            {
                var u = _en[_en.Count - 1];
                _op.Log("SPX: " + (_gn.Count - a0) + " cadenas nuevas" + (prefijo ? "" : " (estado rehecho desde " + desde.ToString("MM-dd HH:mm", Inv) + "Z)") + "; ultima "
                        + u.GenUtc.ToString("MM-dd HH:mm:ss", Inv) + "Z: cruda " + NumFam.F(u.Cruda, "0.00") + " residuo " + NumFam.F(u.ErrTicks, "0.0") + " tk (" + (u.Medida ? "medida " + NumFam.F(u.MedidaBase, "0.00") : "no medida") + ", " + u.MedidaDe
                        + "), rueda de la 2.0 " + NumFam.F(u.Rueda, "0.00") + (u.RuedaUtc > DateTime.MinValue ? " adoptada " + u.RuedaUtc.ToString("MM-dd HH:mm", Inv) + "Z con " + u.RuedaBuenas + " buenas" : "")
                        + (u.MuestraRueda ? "; muestra " + NumFam.F(u.Muestra, "0.00") + " (vela " + u.HoraVelaUtc.ToString("HH:mm", Inv) + "Z " + NumFam.F(u.Precio, "0.00") + (u.PrecioDeTick ? "" : " m2 interpolada") + ")" : "")
                        + (_provSpx.Count > 0 ? "; " + _provSpx.Count + " muestras sin un tick de su minuto (se vuelven a mirar)" : ""));
            }
        }

        private static (DateTime, DateTime) Clave(FotoCadena f) => (Seg(f.GeneradoUtc), f.TsUtc);

        /// <summary>4.1.4: la cinta tiene un tick del MES en t (lo que PrecioEn le da a la guardia del 0,6 % y a los respaldos).</summary>
        private bool TickEn(DateTime t)
        {
            if (_cinta == null) return false;
            try { return _cinta.PrecioSoloTick(t, 4 * 3600) > 0; } catch { return false; }
        }

        /// <summary>4.1.4: el precio "de ahora" no pesa en la razon de esta cadena (salio de la vela alineada con la cadena congelada o vieja).</summary>
        private static bool SinPrecioAhora(Razon20 r) => r.UtUtc != default(DateTime) && r.PAl > 0 && (r.Congelada || r.Vieja);

        /// <summary>4.1.4 (revision 4.1.3): vuelve a mirar en la cinta las cadenas provisionales (cada SondeoCadaS s de reloj). (null, null) = nada nuevo;
        /// si no, el motivo de cada libro (para el log): SPY = la vela de 1 min del ultimo trade aparecio (o cambio), o aparecio el precio del MES de
        /// cuando se bajo la cadena; SPX (5.0) = la vela de una muestra de la rueda aparecio con sus ticks (o cambio).</summary>
        private (string Spy, string Spx) SondeoProvisionales()
        {
            if ((_nProv == 0 || _rq.Count == 0) && _provSpx.Count == 0) return (null, null);
            if (_op.SondeoCadaS > 0 && (DateTime.UtcNow - _ultSondeoUtc).TotalSeconds < _op.SondeoCadaS) return (null, null);
            _ultSondeoUtc = DateTime.UtcNow;
            string spy = null, spx = null;
            for (int i = 0; i < _rq.Count && spy == null && _nProv > 0; i++)
            {
                var r = _rq[i];
                if (_provVela[i])
                {
                    var t = r.UtUtc != default(DateTime) ? r.UtUtc : r.TsUtc.AddSeconds(-_op.RetrasoCboeSeg);
                    var (p, deTick) = CierreVela1m(t);
                    bool habia = r.PAl > 0;
                    if ((p > 0 && deTick) || (p > 0) != habia || (p > 0 && habia && Math.Abs(p - r.PAl) > 1e-9))
                        spy = "la cinta ahora tiene la vela de las " + TiempoFam.ANy(t).ToString("HH:mm", Inv) + " NY de la cadena del " + r.GenUtc.ToString("MM-dd HH:mm", Inv) + "Z: "
                              + NumFam.F(p, "0.00") + (deTick ? "" : " (m2 interpolada)") + ", antes " + NumFam.F(r.PAl, "0.00");
                }
                if (spy == null && _provAhora[i] && TickEn(r.GenUtc))
                    spy = "la cinta ahora tiene el MES de las " + r.GenUtc.ToString("MM-dd HH:mm:ss", Inv) + "Z (cuando se bajo la cadena)";
            }
            foreach (int i in _provSpx)
            {
                if (i < 0 || i >= _en.Count) continue;
                var e = _en[i];
                var (p, deTick) = CierreVela1mRueda(e.HoraVelaUtc);
                bool habia = e.Precio > 0;
                if ((p > 0 && deTick) || (p > 0) != habia || (p > 0 && habia && Math.Abs(p - e.Precio) > 1e-9))
                {
                    spx = "la cinta ahora tiene la vela de las " + e.HoraVelaUtc.ToString("HH:mm", Inv) + "Z de la muestra de la rueda de la cadena del " + e.GenUtc.ToString("MM-dd HH:mm", Inv) + "Z: "
                          + NumFam.F(p, "0.00") + (deTick ? "" : " (m2 interpolada)") + ", antes " + NumFam.F(e.Precio, "0.00");
                    break;
                }
            }
            return (spy, spx);
        }

        /// <summary>La cadena de la fuente con ese generado (al segundo) y ese sello (null si la fuente ya no la tiene).</summary>
        private FotoCadena FotoSpy(int k)
        {
            var g = TiempoFam.DeMs(_gq[k]);
            var l = _cboe.Fotos("SPY", g, g.AddSeconds(1));
            if (l != null) for (int i = l.Count - 1; i >= 0; i--) if (l[i] != null && l[i].TsUtc == _tsq[k]) return l[i];
            return null;
        }

        // ------------------------------------------------------------------ las dos replicas (publicas para el arnes)
        /// <summary>Las dominantes estilo 2.0 en SPY en el instante t con el futuro fut (la cadena = la ultima con generado &lt;= t). null si no hay
        /// cadena con filas o no hay razon. 5.0: sin original en ES (ver la cabecera).</summary>
        public Resultado20 Spy(DateTime tUtc, double fut)
        {
            lock (_llave)
            {
                Refrescar(tUtc);
                int k = NumFam.UltimoMenorIgual(_gq, TiempoFam.Ms(tUtc));
                if (k < 0 || !(fut > 0)) return null;
                var rz = _rq[k];
                if (!(rz.Razon > 0)) return null;
                var f = FotoSpy(k);
                if (f?.Filas == null || f.Filas.Length == 0) return null;
                double S = fut / rz.Razon;
                var gen = TiempoFam.DeMs(_gq[k]);
                var perfil = Nucleo20.Perfil(f, gen, S, tUtc, rz.Razon, true);
                var doms = Nucleo20.Dominantes(perfil, fut, out var lib);
                var r = new Resultado20 { Libro = LIBRO_SPY, Foto = f, GeneradoUtc = gen, Conv = rz.Razon, S = S, Fut = fut, LibroDom = lib, Strikes = perfil.Count,
                                          Doms = doms, Razon = rz, Congelada = rz.Congelada };
                r.Texto = TextoRazon(rz) + (lib == "OI" && doms.Count > 0 ? "; dominantes por OI (sin volumen en el radio)" : "");
                return r;
            }
        }

        /// <summary>Las dominantes del libro SPX de la 2.0 en ES en el instante t con el futuro fut. null si no hay cadena con filas o no hay base.
        /// 5.0: con la cascada completa de la 2.0 en ES (medida &gt; medida reciente &gt; de la rueda &gt; cruda &gt; teorica &gt; cruda sin cota).
        /// 5.0.1: base50 = la base SPX -&gt; ES de la 5.0 en ese minuto (Base50De). Con OpcionesReplica20.ConversionDeLa50 (default) la raya va a
        /// Fut = K + base50 (null si base50 es NaN: sin libro SPX de la 5.0 no se dibuja); S, el radio y la seleccion siguen con la base de la cascada
        /// (si la cascada no da base, con base50). Apagada, la cascada exacta de antes (base50 no se mira).</summary>
        public Resultado20 Spx(DateTime tUtc, double fut, double base50 = double.NaN)
        {
            lock (_llave)
            {
                Refrescar(tUtc);
                int k = NumFam.UltimoMenorIgual(_gn, TiempoFam.Ms(tUtc));
                if (k < 0 || !(fut > 0)) return null;
                var f = _fn[k]; var e = _en[k];
                if (f.Filas == null || f.Filas.Length == 0) return null;
                var (exp, expAlt) = Expiraciones(tUtc);
                double edadUlt = _op.MedidaReciente && e.UltBuenaUtc > DateTime.MinValue ? (e.GenUtc - e.UltBuenaUtc).TotalMinutes : double.NaN;
                double edadRueda = !double.IsNaN(e.Rueda) && e.RuedaUtc > DateTime.MinValue ? (tUtc - e.RuedaUtc).TotalMinutes : double.NaN;
                double b20 = BaseSpx20.Cascada(f.BaseCruda, f.BaseErrorTicks, e.Medida ? e.MedidaBase : double.NaN, e.MedidaDe, _op.MedidaReciente ? e.UltBuena : double.NaN, edadUlt,
                                               e.Rueda, edadRueda, e.RuedaBuenas, fut, tUtc, exp, expAlt, _op.Tasa, _op.Dividendo, out var origen20, out var carry);
                // 5.0.1: la conversion de la 5.0 (default) o la cascada de la 2.0 (la original, exacta). Con la de la 5.0 la SELECCION no cambia: S, el
                // radio y el orden por |GEX| siguen con la base de la 2.0 (bSel; sin base de la 2.0, la de la 5.0) y solo la raya va a K + base50
                bool con50 = _op.ConversionDeLa50;
                if (con50 ? !EsBase(base50) : double.IsNaN(b20)) return null;
                double bSel = con50 && double.IsNaN(b20) ? base50 : b20;
                double b = con50 ? base50 : b20;
                string origen = con50 ? BaseSpx20.ORIGEN_50 : origen20;
                double S = fut - bSel;
                if (S <= 0) return null;
                var gen = TiempoFam.DeMs(_gn[k]);
                var perfil = Nucleo20.Perfil(f, gen, S, tUtc, bSel, false);
                var doms = Nucleo20.Dominantes(perfil, fut, out var lib);
                if (con50) doms = doms.Select(x => (x.K + b, x.G, x.K)).ToList();          // la misma seleccion, puesta con la base de la 5.0
                bool cong = f.DatoUtc.Year >= 2000 && TiempoFam.ANy(f.DatoUtc).TimeOfDay >= new TimeSpan(15, 59, 0);
                var r = new Resultado20 { Libro = LIBRO_SPX, Foto = f, GeneradoUtc = gen, Conv = b, S = S, Fut = fut, LibroDom = lib, Strikes = perfil.Count, Doms = doms,
                                          OrigenBase = origen, Carry = carry, Congelada = cong, Base20 = e, Con50 = con50, BaseOriginal = b20, OrigenOriginal = origen20 };
                string porOi = lib == "OI" && doms.Count > 0 ? "; dominantes por OI (sin volumen en el radio)" : "";
                if (con50)
                    r.Texto = BaseSpx20.TEXTO_50 + b.ToString("0.00", Inv) + " (" + BaseSpx20.DETALLE_50 + ") · " + BaseSpx20.SELECCION_50
                              + (double.IsNaN(b20) ? "ninguna (" + origen20 + "): van con la de la 5.0"
                                                   : b20.ToString("0.00", Inv) + " (" + origen20 + (origen20.StartsWith(BaseSpx20.ORIGEN_CRUDA, StringComparison.Ordinal) ? " de forwards de la cadena" : "") + ")")
                              + porOi;
                else
                    r.Texto = "base 2.0 " + b.ToString("0.00", Inv) + " (" + origen + (origen.StartsWith(BaseSpx20.ORIGEN_CRUDA, StringComparison.Ordinal) ? " de forwards de la cadena" : "")
                              + (double.IsNaN(carry) ? "" : "; cota: carry " + carry.ToString("0.0", Inv) + " +-60 % (dividendo " + (_op.Dividendo * 100).ToString("0.0", Inv) + " %)") + ")"
                              + porOi;
                return r;
            }
        }

        /// <summary>Vencimiento del futuro del grafico (13:30 UTC del tercer viernes), como la 2.0: del codigo; si no trae mes, el trimestral mas cercano.</summary>
        public DateTime Expiracion(DateTime tUtc) => Expiraciones(tUtc).Exp;

        /// <summary>5.0: el vencimiento del grafico y, si es SUPUESTO (codigo sin mes), el del trimestre siguiente, que la 2.0 tambien acepta para la
        /// "medida" (ExpiracionFuturoAltUtc, GammaHoy.cs:1606-1611; GammaHoyNucleo.cs:302-308). Con el codigo con mes, Alt = default.</summary>
        public (DateTime Exp, DateTime Alt) Expiraciones(DateTime tUtc)
        {
            var e = BaseSpx20.ExpiracionDeCodigo(_op.Contrato, tUtc);
            if (e != default(DateTime)) return (e, default(DateTime));
            var sup = BaseSpx20.TrimestralDesde(tUtc.AddDays(1));
            return (sup, sup != default(DateTime) ? BaseSpx20.TrimestralDesde(sup.AddDays(1)) : default(DateTime));
        }

        /// <summary>5.0: el estado de la base de la 2.0 en la cadena de SPX vigente a t (null si no hay): para el arnes y los diagnosticos.</summary>
        public EstadoSpx20 EstadoBaseEn(DateTime tUtc)
        {
            lock (_llave)
            {
                Refrescar(tUtc);
                int k = NumFam.UltimoMenorIgual(_gn, TiempoFam.Ms(tUtc));
                return k >= 0 ? _en[k] : null;
            }
        }

        private static string TextoRazon(Razon20 r)
        {
            string s = "razon 2.0 " + r.Razon.ToString("0.0000", Inv);
            if (r.Valida && r.Origen.StartsWith("vela alineada", StringComparison.Ordinal) && !double.IsNaN(r.PAl))
                s += " = MES " + r.PAl.ToString("0.00", Inv) + " (vela de 1 min de las " + TiempoFam.ANy(r.UtUtc).ToString("HH:mm", Inv) + " NY, la del ultimo trade de la cadena"
                     + (r.PAlDeTick ? "" : "; sin sus ticks en la cinta: vela m2 interpolada") + ") / spot SPY " + r.Spot.ToString("0.00", Inv);
            s += " · " + r.Origen;
            if (r.Congelada) s += " · de noche junta el MES de esa vela con el spot de SPY de ahora (falla conocida de la 2.0)";
            return s;
        }

        // ------------------------------------------------------------------ IExtrasMinuto
        /// <summary>4.1.4: el fut con el que se ponen las R20 del minuto t. Con FutDelInstante (default): el ultimo precio del MES al empezar el minuto
        /// (el ultimo tick con hora &lt;= t, a lo sumo 120 s antes; lo mas parecido al "cierre de la vela actual" con que reprecia la 2.0 y lo mismo en vivo
        /// que al recalcular: si a la hora de calcular todavia no llego un tick posterior a t, el ultimo tick ES el de antes de t). Sin tick en esos
        /// 120 s, o con FutDelInstante apagado: el fut del minuto de la 5.0 (cierre de la ultima vela m2 cerrada, el de todas las series).</summary>
        public double FutR20(DateTime tUtc, double futMinuto)
        {
            if (!_op.FutDelInstante || _cinta == null) return futMinuto;
            double p = double.NaN;
            try
            {
                var q = tUtc; var ut = _cinta.UltimoTickUtc;
                if (ut > DateTime.MinValue.AddDays(1) && ut < tUtc)
                {
                    if ((tUtc - ut).TotalSeconds > 120) return futMinuto;
                    q = ut;                                               // todavia ningun tick despues de t: el ultimo es el de antes de t
                }
                p = _cinta.PrecioSoloTick(q, 120);
            }
            catch { p = double.NaN; }
            return p > 0 ? p : futMinuto;
        }

        public void Calcular(RegistroMinuto r, DateTime tUtc, double fut, IReadOnlyList<LibroMinuto> bk)
        {
            if (r == null) return;
            int n = CatalogoFamilia.Extra.Length;
            var ex = new Nivel[n][]; var ks = new double[n][]; var metas = new List<MetaLibro>(2);
            PonerReplicas(ex, ks, metas, tUtc, FutR20(tUtc, fut), Base50De(r));   // 5.0.1: la base del libro SPX de la 5.0 de este minuto (r.Meta)
            if (bk != null)
                foreach (var b in bk)
                {
                    if (b == null) continue;
                    int i = b.Libro == "SPY" ? CatalogoFamilia.IndiceExtra("DOMS_SPY_vol") : b.Libro == "SPX" ? CatalogoFamilia.IndiceExtra("DOMS_SPX_vol") : -1;
                    if (i < 0) continue;
                    var doms = Nucleo20.Dominantes(b, out _);
                    Poner(ex, ks, i, doms, ReglasFam.Escala(b));
                }
            Guardar(r, ex, ks, metas);
        }

        public bool Completar(RegistroMinuto r, DateTime tUtc)
        {
            if (r == null || double.IsNaN(r.FutMnq)) return false;
            // 5.0.1: un minuto que YA trae series extra llega aca solo por Falta (su R20_SPX_vol es de la otra conversion): se rehacen solo las R20_*
            // (Rehacer las reemplaza sin tocar DOMS_* ni la clasica; Guardar de abajo pisaria r.Extra entero)
            if (r.TieneExtra) return Rehacer(r, tUtc);
            int n = CatalogoFamilia.Extra.Length;
            var ex = new Nivel[n][]; var ks = new double[n][]; var metas = new List<MetaLibro>(2);
            PonerReplicas(ex, ks, metas, tUtc, FutR20(tUtc, r.FutMnq), Base50De(r));
            return Guardar(r, ex, ks, metas);
        }

        /// <summary>4.1.4 (IExtrasRehacer): rehace en un minuto YA calculado lo que depende del estado de la 2.0 (5.0: R20_SPY_vol con su razon "2.0 SPY"
        /// Y R20_SPX_vol con su base "2.0 SPX", que ahora tiene rueda), con el estado de ahora; el resto (DOMS_*, la clasica) queda igual. true si
        /// cambio algo (el motor lo vuelve a guardar). 5.0.1: con la misma conversion que Calcular (la de la 5.0 del minuto, r.Meta, o la cascada).</summary>
        public bool Rehacer(RegistroMinuto r, DateTime tUtc)
        {
            if (r == null || double.IsNaN(r.FutMnq)) return false;
            int iq = CatalogoFamilia.IndiceExtra("R20_SPY_vol"), ix = CatalogoFamilia.IndiceExtra("R20_SPX_vol"), n = CatalogoFamilia.Extra.Length;
            if (iq < 0 && ix < 0) return false;
            double fut = FutR20(tUtc, r.FutMnq);
            var q = iq >= 0 ? Spy(tUtc, fut) : null;
            var d = ix >= 0 ? Spx(tUtc, fut, Base50De(r)) : null;
            var ex1 = new Nivel[n][]; var ks1 = new double[n][];
            if (q != null) Poner(ex1, ks1, iq, q.Doms, 1.0);
            if (d != null) Poner(ex1, ks1, ix, d.Doms, 1.0);
            var mq = q == null ? null : MetaDe(q);
            var md = d == null ? null : MetaDe(d);
            bool igual = MismaMeta(r.MetaExtraDe(LIBRO_SPY), mq) && MismaMeta(r.MetaExtraDe(LIBRO_SPX), md);
            foreach (int i in new[] { iq, ix })
            {
                if (!igual || i < 0) continue;
                var lv = r.Extra != null && i < r.Extra.Length ? r.Extra[i] : null;
                var kv = r.ExtraStrikes != null && i < r.ExtraStrikes.Length ? r.ExtraStrikes[i] : null;
                if (!MismosNiveles(lv, kv, ex1[i], ks1[i])) igual = false;
            }
            if (igual) return false;
            var ex = new Nivel[n][]; var ks = new double[n][];
            if (r.Extra != null) Array.Copy(r.Extra, ex, Math.Min(n, r.Extra.Length));
            if (r.ExtraStrikes != null) Array.Copy(r.ExtraStrikes, ks, Math.Min(n, r.ExtraStrikes.Length));
            if (iq >= 0) { ex[iq] = ex1[iq]; ks[iq] = ks1[iq]; }
            if (ix >= 0) { ex[ix] = ex1[ix]; ks[ix] = ks1[ix]; }
            var metas = new List<MetaLibro>(3);
            if (mq != null) metas.Add(mq);                                       // el mismo orden que PonerReplicas: "2.0 SPY" y despues "2.0 SPX"
            if (md != null) metas.Add(md);
            if (r.MetaExtra != null) foreach (var m in r.MetaExtra) if (m != null && m.Libro != LIBRO_SPY && m.Libro != LIBRO_SPX) metas.Add(m);
            r.Extra = ex; r.ExtraStrikes = ks; r.MetaExtra = metas.ToArray();
            return true;
        }

        private static MetaLibro MetaDe(Resultado20 x) =>
            new MetaLibro { Libro = x.Libro, Conv = x.Conv, S = x.S, DatoUtc = x.Foto.DatoUtc, OiOk = true, Congelada = x.Congelada, ConvTexto = x.Texto };

        private static bool MismosNiveles(Nivel[] a, double[] ka, Nivel[] b, double[] kb)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].Precio.Equals(b[i].Precio) || a[i].Etq != b[i].Etq || !a[i].GexM.Equals(b[i].GexM)) return false;
                double x = ka != null && i < ka.Length ? ka[i] : double.NaN, y = kb != null && i < kb.Length ? kb[i] : double.NaN;
                if (!x.Equals(y)) return false;
            }
            return true;
        }

        private static bool MismaMeta(MetaLibro a, MetaLibro b)
        {
            if (a == null || b == null) return a == null && b == null;
            return a.Conv.Equals(b.Conv) && a.S.Equals(b.S) && a.DatoUtc == b.DatoUtc && a.Congelada == b.Congelada && (a.ConvTexto ?? "") == (b.ConvTexto ?? "");
        }

        private void PonerReplicas(Nivel[][] ex, double[][] ks, List<MetaLibro> metas, DateTime tUtc, double fut, double base50)
        {
            var q = Spy(tUtc, fut);
            if (q != null)
            {
                Poner(ex, ks, CatalogoFamilia.IndiceExtra("R20_SPY_vol"), q.Doms, 1.0);
                metas.Add(MetaDe(q));
            }
            var d = Spx(tUtc, fut, base50);
            if (d != null)
            {
                Poner(ex, ks, CatalogoFamilia.IndiceExtra("R20_SPX_vol"), d.Doms, 1.0);
                metas.Add(MetaDe(d));
            }
        }

        /// <summary>Niveles D1/D2: precio round(Fut, 2), monto round(G x escala / 1e6, 1) con signo, strike round(K, 2).</summary>
        private static void Poner(Nivel[][] ex, double[][] ks, int i, List<(double Fut, double G, double K)> doms, double escala)
        {
            if (i < 0 || doms == null || doms.Count == 0) return;
            var lv = new Nivel[doms.Count]; var st = new double[doms.Count];
            for (int j = 0; j < doms.Count; j++)
            {
                lv[j] = new Nivel(NumPy.RoundPy(doms[j].Fut, 2), "D" + (j + 1), NumPy.RoundPy(doms[j].G * escala / 1e6, 1));
                st[j] = NumPy.RoundPy(doms[j].K, 2);
            }
            ex[i] = lv; ks[i] = st;
        }

        private static bool Guardar(RegistroMinuto r, Nivel[][] ex, double[][] ks, List<MetaLibro> metas)
        {
            bool alguna = false; foreach (var x in ex) if (x != null) { alguna = true; break; }
            if (!alguna && metas.Count == 0) return false;
            r.Extra = ex; r.ExtraStrikes = ks; r.MetaExtra = metas.ToArray();
            return true;
        }
    }
}
