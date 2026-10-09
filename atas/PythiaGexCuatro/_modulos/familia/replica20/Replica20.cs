// Replica20.cs — PythiaGex 4.1.3 (09-10-2026), modulo replica20. Pedido del operador (09-10): "copia la formula de la 2.0 y agregala a lo que ya
// tenemos, que tambien funciona, asi vamos refinando y calibrando". Cuatro series NUEVAS, todas calculadas ADENTRO (no se lee nada de la 2.0:
// ni su log, ni su razon-NQ-QQQ.txt, ni sus estelas; independencia total). Fuera de la cuenta de la vista previa: CONF y FAM no las ven.
//
//   R20_QQQ_vol  "2.0 QQQ"  replica EXACTA de las dominantes de la 2.0 (PythiaGexDos 2.0.6 con los ajustes del .ws del operador:
//                Horizonte=Hoy, CuantasDominantes=2, RadioDominantesPct=2, RadioDominantesMaxPts=100, DominantesReferencia=true (=> sin una por lado
//                y sin centroide), DominantesDeNoche=volumen, Tasa=0,0375, RetrasoCboeSeg=960):
//                  * libro = la cadena QQQ de CBOE (la ultima con generado <= t), horizonte Hoy (0 <= dias - env <= max(1, mas cercano + 0,01));
//                  * gamma Black-Scholes r 0,0375 sin dividendo; por fila gv = (g(ivC) volC - g(ivP) volP) x 100 x S^2 x 0,01, go igual con el OI;
//                    sumado por K (GammaHoyNucleo.cs:189-196, 332-353);
//                  * dominantes = las 2 barras de mayor |gv| con |Fut - fut| <= min(2 % de fut, 100) (orden estable por K); si no hay, lo mismo con go
//                    (GammaHoyNucleo.cs:394-410);
//                  * SU conversion (EscalarCon, GammaHoy.cs:1040-1131): razon = cierre de la vela de 1 min del MNQ que contiene el ultimo trade de la
//                    cadena / spot_idx de la cadena; con la cadena fresca y |pAl/pAhora - 1| > 0,006, la cruda pAhora/spot; sin vela, sus respaldos
//                    (ultima valida <= 4 dias, mediana de la rueda, cruda). Fut(K) = K x razon, S = fut / razon.
//   R20_NDX_vol  "2.0 NDX"  la capa NDX de la 2.0 (GammaHoyCapas.cs RepreciarCapas, mismos ajustes): la misma seleccion sobre la cadena _NDX de CBOE
//                con base = la base cruda de forwards de la cadena si pasa la cota |b - carry| <= max(0,6 |carry|, 0,0006 fut) (carry = fut x
//                (0,0375 - 0,008) x dias al vencimiento del futuro / 365, GammaHoyNucleo.cs:298-321); si no, el carry; si no hay carry, la cruda.
//                Fut(K) = K + base.
//   DOMS_QQQ_vol "QQQ dom"  la SELECCION de la 2.0 sobre el libro QQQ de la 4.1 (LibroMinuto del minutero QQQ: razon sincronizada C8).
//   DOMS_NDX_vol "NDX dom"  la seleccion de la 2.0 sobre el libro NDX de la 4.1 (base C7). Apagada por defecto: sin medir.
// Rol "D1" (la de mayor |GEX|) y "D2"; monto = GEX con signo / 1e6 (M USD por 1 %, escala 1 como NDX/QQQ), round 1. Strike en su libro.
//
// Diferencias con la 2.0 que NO se pueden evitar (dichas, no escondidas):
//   * la cuenta se hace una vez por minuto (4.1.4: con el ultimo precio del MNQ al empezar el minuto; la 4.1.3 usaba el fut del minuto de la 4.1,
//     el cierre de la ultima vela m2 cerrada); la 2.0 reprecia cada segundo con el precio de ese instante (el arnes replica20 mide la cuenta con el
//     precio de cada AUDIT de la 2.0);
//   * la cadena es la que bajo la 4.1 (otra bajada que la de cboe-local: de noche el dato es el mismo);
//   * la vela de 1 min sale de la cinta de la 4.1 (el ultimo tick antes de que termine ese minuto); si la cinta no tiene los ticks de ese minuto
//     (ATAS cerrado), la vela m2 interpolada, y lo dice el texto de la conversion;
//   * la 4.1 no guarda si la cadena traia ultimo_trade: se toma FotoCadena.DatoUtc (CBOE siempre lo trae; sin el, la 4.1 pone ts - 902 s);
//   * la base NDX: los respaldos "medida" (base del radar de la nube), "ultima buena" y "de la rueda" (archivo base-rueda-NQ.json) de la 2.0 no
//     estan en las cadenas de la 4.1 (con la primaria en QQQ la 2.0 tampoco mide la rueda): queda cruda -> carry -> cruda sin cota.
// Nada de E/S. Lo llama el hilo del motor (IExtrasMinuto); el arnes lo llama directo (publico y puro donde se puede).
//
// 4.1.4 (09-10-2026, arreglos de la revision de la 4.1.3):
//   * la razon de una cadena ya no se escala "una vez y para siempre": las cadenas cuya razon salio SIN la vela de 1 min de la cinta (la cinta
//     todavia cargaba su historia al arrancar: la carga va de los dias viejos a los CSV, y el ultimo dia es el que mas tarda) o sin el precio del
//     MNQ de cuando se bajaron, se vuelven a mirar cada SondeoCadaS s; si la vela aparecio, se rehace el estado entero en orden (lo mismo que daria
//     un arranque con la cinta completa) y, si cambio la razon de alguna cadena, sube Generacion: el motor rehace los minutos ya calculados desde
//     esa cadena (IExtrasRehacer.Rehacer: solo R20_QQQ_vol y su "2.0 QQQ") y los vuelve a guardar. Medido por la revision: con la cinta sin el 10-08
//     la razon quedaba 41,6095 (CRUDA sin alinear) toda la noche en vez de 41,4447 = 124 pts en la raya de K750.
//   * la razon de la 2.0 depende del MARCO del grafico donde corre la 2.0 (su "vela del ultimo trade"): esta replica es la de la 2.0 en un grafico
//     de 1 min. En 2 min la vela de las 16:14 NY cierra 30.993,25 en vez de 30.990,25 (41,4487 contra 41,4447: ~3 pts de noche). La revision mostro
//     que los 3 minutos QQQ distintos del 09-10 (22:00, 22:02, 22:03 UTC) eran la 2.0 en un grafico de 2 min (y a las 22:00 otra bajada), NO "la
//     instancia previa / I1 recien arrancada" como dijo el informe de la 4.1.3.
//   * el fut de las R20 (OpcionesReplica20.FutDelInstante, PRENDIDO): el ultimo precio del MNQ al empezar el minuto en vez del cierre de la ultima
//     vela m2 cerrada (el de todas las series). Es lo mas parecido al "cierre de la vela actual" con que reprecia la 2.0 y da lo mismo en vivo que al
//     recalcular. Medido (arnes replica20, variante E contra la D, AUDIT de la 2.0 del 09-10): minutos iguales a la 2.0 QQQ 113/138 contra 97,
//     NDX 113/132 contra 101; |fut - fut de la 2.0| mediana 2,25 contra 3,25 pts. Las rayas no se mueven (strike x razon / strike + base): cambia
//     el monto y, en el borde de los 100 pts, cual strike entra.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PythiaGexCuatro.Familia
{
    /// <summary>La cuenta de la 2.0 que hace falta para sus dominantes, port 1:1 de PythiaGexDos/GammaHoyNucleo.cs (mismas operaciones y mismo orden).</summary>
    public static class Nucleo20
    {
        public const double TASA = 0.0375;               // ajuste "Tasa" del .ws (0,0375)
        public const double MULT = 100.0;                // GammaHoyNucleo.MULT_INDICE
        public const double PISO_DIAS = 1.0 / 1440.0;    // GammaHoyNucleo.PISO_DIAS
        public const double RADIO_PCT = 2.0, RADIO_MAX_PTS = 100.0;
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

        /// <summary>Feed.Parsear de la 2.0 lee un null del json como 0 (la 4.1 lo guarda NaN).</summary>
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
        /// libro. porRazon: Fut = K x conv (QQQ); si no, Fut = K + conv (NDX). Vacio si la cadena no sirve o S &lt;= 0.</summary>
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

        /// <summary>La seleccion de la 2.0 sobre un libro de la 4.1 (LibroMinuto ya convertido a NQ por la 4.1: Fut, K, Gv, Go en orden de Fut).</summary>
        public static List<(double Fut, double G, double K)> Dominantes(LibroMinuto b, out string libroDom)
        {
            libroDom = "";
            if (b?.Fut == null || b.Gv == null || b.Go == null || b.K == null) return new List<(double, double, double)>();
            var p = new List<Barra>(b.Fut.Length);
            for (int i = 0; i < b.Fut.Length; i++) p.Add(new Barra { K = b.K[i], Fut = b.Fut[i], Gv = b.Gv[i], Go = b.Go[i] });
            return Dominantes(p, b.FutMnq, out libroDom);
        }
    }

    /// <summary>La razon de una cadena de QQQ como la 2.0 (EscalarCon) y de donde salio.</summary>
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
    /// ultimas 24 frescas). Sin archivo (la 2.0 guarda razon-NQ-QQQ.txt): el estado se arma con las cadenas que tiene la 4.1.</summary>
    public sealed class RazonQqq20
    {
        public const double VIEJA_MIN = 20;              // GammaHoy.RazonCadenaViejaMin
        public int RetrasoSeg = 960;                     // ajuste RetrasoCboeSeg del .ws
        private readonly List<double> _obs = new List<double>();
        public double Rueda = double.NaN;
        public double Ultima = double.NaN;
        public DateTime UltimaUtc = DateTime.MinValue;
        public int Procesadas { get; private set; }
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Una cadena (en orden de 'generado'). ahora = su 'generado' (cuando la bajo la 4.1 = cuando la 2.0 la habria escalado).
        /// cierreVela(t) = cierre de la vela de 1 min del MNQ abierta a t o antes (NaN si no hay; deTick = si salio de los ticks);
        /// precioEn(t) = el ultimo precio del MNQ en t (NaN si no hay).</summary>
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

    /// <summary>La base de la capa NDX de la 2.0 (CalcularAdentro, rama indice con base), con lo que trae una cadena de la 4.1.</summary>
    public static class BaseNdx20
    {
        public const double DIVIDENDO_NQ = 0.008;        // DividendoUsado(): 0 en el .ws -> 0,8 % para NQ

        /// <summary>carry = fut x (tasa - dividendo) x dias al vencimiento / 365 (NaN si el vencimiento no es posterior a ahora).</summary>
        public static double Carry(double futuro, DateTime ahoraUtc, DateTime expUtc, double tasa = Nucleo20.TASA, double dividendo = DIVIDENDO_NQ)
            => expUtc != default(DateTime) && expUtc > ahoraUtc ? futuro * (tasa - dividendo) * (expUtc - ahoraUtc).TotalDays / 365.0 : double.NaN;

        /// <summary>CRUDA si pasa la cota del carry; si no, el carry (TEORICA); si no hay carry, la cruda sin cota; si nada, NaN.</summary>
        public static double Base(FotoCadena c, double futuro, DateTime ahoraUtc, DateTime expUtc, out string origen, out double carry,
                                  double tasa = Nucleo20.TASA, double dividendo = DIVIDENDO_NQ)
        {
            var iv0 = CultureInfo.InvariantCulture;
            carry = Carry(futuro, ahoraUtc, expUtc, tasa, dividendo);
            double k0 = carry;
            bool Cerca(double b, double k) => Math.Abs(b - k) <= Math.Max(Math.Abs(k) * 0.6, futuro * 0.0006);
            bool Razonable(double b) => double.IsNaN(k0) || Cerca(b, k0);
            double cruda = c == null || double.IsNaN(c.BaseCruda) ? 0 : c.BaseCruda;
            double err = c == null || double.IsNaN(c.BaseErrorTicks) ? 0 : c.BaseErrorTicks;
            if (cruda != 0 && Razonable(cruda)) { origen = "CRUDA " + err.ToString("0", iv0) + " ticks"; return cruda; }
            if (!double.IsNaN(carry)) { origen = "TEORICA carry " + carry.ToString("0.0", iv0) + (cruda != 0 ? " (cruda " + cruda.ToString("0.0", iv0) + " descartada)" : ""); return carry; }
            if (cruda != 0) { origen = "CRUDA " + err.ToString("0", iv0) + " ticks (sin cota)"; return cruda; }
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

    public sealed class OpcionesReplica20
    {
        /// <summary>Codigo del grafico (MNQZ6): el vencimiento del futuro para la cota de la base NDX (como la 2.0 con el codigo del instrumento).</summary>
        public string Contrato = "";
        public double Tasa = Nucleo20.TASA, Dividendo = BaseNdx20.DIVIDENDO_NQ;
        public int RetrasoCboeSeg = 960;
        /// <summary>Dias de cadenas previos al inicio de la sesion que se recorren (estado de la razon: ultima valida, mediana de la rueda).</summary>
        public int DiasHistoria = 6;
        public Action<string> Log;
        /// <summary>4.1.4 (revision 4.1.3): cada cuantos segundos (reloj de la PC) se vuelven a mirar en la cinta las cadenas cuya razon se escalo SIN la
        /// vela de 1 min de sus ticks (cinta cargando al arrancar, ATAS cerrado a esa hora): si la vela aparecio, se rehace el estado entero en orden
        /// (como un arranque con la cinta completa) y el motor rehace esos minutos. 0 = en cada llamada (arnes).</summary>
        public int SondeoCadaS = 20;
        /// <summary>4.1.4 (revision 4.1.3, hallazgo del precio): true (default) = las R20 usan el ultimo precio del MNQ al empezar el minuto (lo mas parecido
        /// a la 2.0, que reprecia con el precio de cada instante: "cierre de la vela actual"); false = el fut del minuto de la 4.1 (el cierre de la ultima
        /// vela m2 cerrada, el de todas las series, como la 4.1.3). Medido en el arnes replica20 (variante E contra la D, 09-10, AUDIT de la 2.0):
        /// minutos iguales a la 2.0 QQQ 113 de 138 contra 97, NDX 113 de 132 contra 101; |fut - fut de la 2.0| mediana 2,25 contra 3,25 pts,
        /// maximo 12,75 contra 22,25. Las rayas no se mueven (son strike x razon / strike + base): cambia el monto y, en el borde de los 100 pts,
        /// cual strike entra.</summary>
        public bool FutDelInstante = true;
    }

    /// <summary>Lo que dio una replica de la 2.0 en un instante (para el motor y el arnes).</summary>
    public sealed class Resultado20
    {
        public string Libro;                                   // "2.0 QQQ" | "2.0 NDX"
        public FotoCadena Foto; public DateTime GeneradoUtc;
        public double Conv = double.NaN, S = double.NaN, Fut = double.NaN;
        public string LibroDom = "", Texto = "";
        public bool Congelada;
        public int Strikes;
        public List<(double Fut, double G, double K)> Doms = new List<(double, double, double)>();
        public Razon20 Razon;                                  // solo QQQ
        public string OrigenBase = ""; public double Carry = double.NaN;   // solo NDX
    }

    /// <summary>Las series "como la 2.0" de cada minuto (IExtrasMinuto del motor). Un solo hilo (el del motor); lock por si acaso.
    /// 4.1.4 (revision 4.1.3): IExtrasRehacer: si una cadena se escalo sin la vela de la cinta (la cinta todavia cargaba) y la vela llega despues, se
    /// rehace el estado de la razon en orden y el motor rehace los minutos ya calculados desde esa cadena (antes quedaba la razon de respaldo toda la
    /// noche: 41,6095 en vez de 41,4447 = 124 pts en la raya de K750).</summary>
    public sealed class Replica20 : IExtrasMinuto, IExtrasRehacer
    {
        public const string VERSION = "replica20 4.1.4 (09-10-2026)";
        public const string LIBRO_QQQ = "2.0 QQQ", LIBRO_NDX = "2.0 NDX";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly IFuenteCboe _cboe;
        private readonly ICinta _cinta;
        private readonly OpcionesReplica20 _op;
        private readonly object _llave = new object();

        // QQQ: solo las CLAVES (generado, sello) y la razon de cada cadena; la cadena misma se le pide a la fuente cuando hace falta (la fuente le
        // saca las filas a las de mas de 30 h: guardar aca las fotos completas las retendria en memoria)
        private readonly List<(DateTime Gen, DateTime Ts)> _vistas = new List<(DateTime, DateTime)>();   // tal como las dio la fuente (si solo crecio)
        private readonly List<DateTime> _tsq = new List<DateTime>();          // sello de cada cadena aceptada (en orden de generado)
        private readonly List<long> _gq = new List<long>();                    // generado al segundo (ms)
        private readonly List<Razon20> _rq = new List<Razon20>();
        private RazonQqq20 _estado;
        private readonly List<FotoCadena> _fn = new List<FotoCadena>();
        private readonly List<long> _gn = new List<long>();
        private long _ver = long.MinValue;
        private DateTime _desde = DateTime.MinValue;
        // 4.1.4: por cadena aceptada (mismo indice que _rq): su razon salio sin la vela de la cinta (o sin el precio de la cinta al bajarla)
        private readonly List<bool> _provVela = new List<bool>(), _provAhora = new List<bool>();
        private int _nProv;
        private DateTime _ultSondeoUtc = DateTime.MinValue;

        public int Reprocesos { get; private set; }
        /// <summary>4.1.4: veces que el sondeo encontro en la cinta la vela (o el precio) que faltaba al escalar alguna cadena y rehizo el estado.</summary>
        public int Resondeos { get; private set; }
        /// <summary>4.1.4 (IExtrasRehacer): sube cada vez que cambia la razon (o su origen) de una cadena ya escalada.</summary>
        public long Generacion { get; private set; }
        /// <summary>4.1.4 (IExtrasRehacer): el 'generado' de la primera cadena que cambio en la ultima Generacion (desde ahi se rehacen los minutos).</summary>
        public DateTime RehacerDesdeUtc { get; private set; } = DateTime.MaxValue;
        /// <summary>4.1.4: cadenas cuya razon todavia espera la vela de la cinta.</summary>
        public int Provisionales { get { lock (_llave) return _nProv; } }
        public int FotosQqq { get { lock (_llave) return _gq.Count; } }
        public int FotosNdx { get { lock (_llave) return _fn.Count; } }

        public Replica20(IFuenteCboe cboe, ICinta cinta, OpcionesReplica20 op = null)
        {
            _cboe = cboe ?? throw new ArgumentNullException(nameof(cboe));
            _cinta = cinta;
            _op = op ?? new OpcionesReplica20();
            _estado = new RazonQqq20 { RetrasoSeg = _op.RetrasoCboeSeg };
        }

        private static DateTime Seg(DateTime d) => new DateTime(d.Ticks - d.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);

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

        /// <summary>GetCandle(CurrentBar - 1).Close en el momento de escalar: el ultimo precio del MNQ en t.</summary>
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
        /// <summary>Trae las cadenas de QQQ y NDX si cambio la fuente o la sesion, y corre la razon de la 2.0 sobre las QQQ nuevas, en orden. Si la
        /// fuente cambio el principio de la lista (historia que llego tarde), rehace el estado desde el principio (causal y determinista).</summary>
        private void Refrescar(DateTime tUtc)
        {
            var desde = TiempoFam.IniSesion(TiempoFam.Sesion(tUtc)).AddDays(-_op.DiasHistoria);
            long ver = _cboe.Version;
            // 4.1.4 (revision 4.1.3): antes la razon se escalaba UNA vez por cadena y nunca se revisaba; si al escalar la cinta no tenia la vela (cargaba
            // la historia), la cadena congelada quedaba con un respaldo toda la noche. Ahora las cadenas "provisionales" se vuelven a mirar cada
            // SondeoCadaS s: si la vela (o el precio) aparecio, se rehace todo el estado en orden, igual que un arranque con la cinta completa.
            string porQue = SondeoProvisionales();
            if (porQue == null && ver == _ver && desde == _desde) return;
            bool otraVentana = desde != _desde;
            _ver = ver; _desde = desde;
            // QQQ: la razon se escala UNA vez por cadena, en orden (como la 2.0 al bajar cada una); si la fuente solo agrego al final, se siguen
            var lq = _cboe.Fotos("QQQ", desde, DateTime.MaxValue) ?? Array.Empty<FotoCadena>();
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
                _estado = new RazonQqq20 { RetrasoSeg = _op.RetrasoCboeSeg };
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
                    Generacion++; RehacerDesdeUtc = TiempoFam.DeMs(_gq[primera]);
                    _op.Log?.Invoke("QQQ: razon de la 2.0 REHECHA (" + (porQue ?? (otraVentana ? "otra ventana de cadenas" : "la fuente cambio el principio de la lista")) + "): la primera cadena que cambio es la de "
                                    + RehacerDesdeUtc.ToString("MM-dd HH:mm:ss", Inv) + "Z, " + NumFam.F(antes[(_gq[primera], _tsq[primera])].Item1, "0.000000") + " -> " + NumFam.F(_rq[primera].Razon, "0.000000")
                                    + " (" + _rq[primera].Origen + "); generacion " + Generacion + ", " + _nProv + " cadenas siguen sin la vela");
                }
            }
            if (_gq.Count > a0 && _op.Log != null)
            {
                var u = _rq[_rq.Count - 1];
                _op.Log("QQQ: " + (_gq.Count - a0) + " cadenas nuevas" + (prefijo ? "" : " (estado rehecho desde " + desde.ToString("MM-dd HH:mm", Inv) + "Z)") + "; ultima "
                        + u.GenUtc.ToString("MM-dd HH:mm:ss", Inv) + "Z razon 2.0 " + NumFam.F(u.Razon, "0.0000") + " (" + u.Origen + "), MNQ vela " + NumFam.F(u.PAl, "0.00")
                        + (u.PAlDeTick ? "" : " (m2 interpolada)") + " / spot " + NumFam.F(u.Spot, "0.00") + (_nProv > 0 ? "; " + _nProv + " cadenas sin la vela de la cinta (se vuelven a mirar)" : ""));
            }
            // NDX (sin estado)
            var ln = _cboe.Fotos("NDX", desde, DateTime.MaxValue) ?? Array.Empty<FotoCadena>();
            _fn.Clear(); _gn.Clear();
            foreach (var f in ln)
            {
                if (f == null) continue;
                long g = TiempoFam.Ms(Seg(f.GeneradoUtc));
                if (_gn.Count > 0 && g < _gn[_gn.Count - 1]) continue;
                _fn.Add(f); _gn.Add(g);
            }
        }

        private static (DateTime, DateTime) Clave(FotoCadena f) => (Seg(f.GeneradoUtc), f.TsUtc);

        /// <summary>4.1.4: la cinta tiene un tick del MNQ en t (lo que PrecioEn le da a la guardia del 0,6 % y a los respaldos).</summary>
        private bool TickEn(DateTime t)
        {
            if (_cinta == null) return false;
            try { return _cinta.PrecioSoloTick(t, 4 * 3600) > 0; } catch { return false; }
        }

        /// <summary>4.1.4: el precio "de ahora" no pesa en la razon de esta cadena (salio de la vela alineada con la cadena congelada o vieja).</summary>
        private static bool SinPrecioAhora(Razon20 r) => r.UtUtc != default(DateTime) && r.PAl > 0 && (r.Congelada || r.Vieja);

        /// <summary>4.1.4 (revision 4.1.3): vuelve a mirar en la cinta las cadenas provisionales (cada SondeoCadaS s de reloj). null = nada nuevo; si no, el
        /// motivo (para el log): la vela de 1 min del ultimo trade aparecio (o cambio), o aparecio el precio del MNQ de cuando se bajo la cadena.</summary>
        private string SondeoProvisionales()
        {
            if (_nProv == 0 || _rq.Count == 0) return null;
            if (_op.SondeoCadaS > 0 && (DateTime.UtcNow - _ultSondeoUtc).TotalSeconds < _op.SondeoCadaS) return null;
            _ultSondeoUtc = DateTime.UtcNow;
            for (int i = 0; i < _rq.Count; i++)
            {
                var r = _rq[i];
                if (_provVela[i])
                {
                    var t = r.UtUtc != default(DateTime) ? r.UtUtc : r.TsUtc.AddSeconds(-_op.RetrasoCboeSeg);
                    var (p, deTick) = CierreVela1m(t);
                    bool habia = r.PAl > 0;
                    if ((p > 0 && deTick) || (p > 0) != habia || (p > 0 && habia && Math.Abs(p - r.PAl) > 1e-9))
                        return "la cinta ahora tiene la vela de las " + TiempoFam.ANy(t).ToString("HH:mm", Inv) + " NY de la cadena del " + r.GenUtc.ToString("MM-dd HH:mm", Inv) + "Z: "
                               + NumFam.F(p, "0.00") + (deTick ? "" : " (m2 interpolada)") + ", antes " + NumFam.F(r.PAl, "0.00");
                }
                if (_provAhora[i] && TickEn(r.GenUtc))
                    return "la cinta ahora tiene el MNQ de las " + r.GenUtc.ToString("MM-dd HH:mm:ss", Inv) + "Z (cuando se bajo la cadena)";
            }
            return null;
        }

        /// <summary>La cadena de la fuente con ese generado (al segundo) y ese sello (null si la fuente ya no la tiene).</summary>
        private FotoCadena FotoQqq(int k)
        {
            var g = TiempoFam.DeMs(_gq[k]);
            var l = _cboe.Fotos("QQQ", g, g.AddSeconds(1));
            if (l != null) for (int i = l.Count - 1; i >= 0; i--) if (l[i] != null && l[i].TsUtc == _tsq[k]) return l[i];
            return null;
        }

        // ------------------------------------------------------------------ las dos replicas (publicas para el arnes)
        /// <summary>Las dominantes de la 2.0 en QQQ en el instante t con el futuro fut (la cadena = la ultima con generado &lt;= t). null si no hay
        /// cadena con filas o no hay razon.</summary>
        public Resultado20 Qqq(DateTime tUtc, double fut)
        {
            lock (_llave)
            {
                Refrescar(tUtc);
                int k = NumFam.UltimoMenorIgual(_gq, TiempoFam.Ms(tUtc));
                if (k < 0 || !(fut > 0)) return null;
                var rz = _rq[k];
                if (!(rz.Razon > 0)) return null;
                var f = FotoQqq(k);
                if (f?.Filas == null || f.Filas.Length == 0) return null;
                double S = fut / rz.Razon;
                var gen = TiempoFam.DeMs(_gq[k]);
                var perfil = Nucleo20.Perfil(f, gen, S, tUtc, rz.Razon, true);
                var doms = Nucleo20.Dominantes(perfil, fut, out var lib);
                var r = new Resultado20 { Libro = LIBRO_QQQ, Foto = f, GeneradoUtc = gen, Conv = rz.Razon, S = S, Fut = fut, LibroDom = lib, Strikes = perfil.Count,
                                          Doms = doms, Razon = rz, Congelada = rz.Congelada };
                r.Texto = TextoRazon(rz) + (lib == "OI" && doms.Count > 0 ? "; dominantes por OI (sin volumen en el radio)" : "");
                return r;
            }
        }

        /// <summary>Las dominantes de la capa NDX de la 2.0 en el instante t con el futuro fut. null si no hay cadena con filas o no hay base.</summary>
        public Resultado20 Ndx(DateTime tUtc, double fut)
        {
            lock (_llave)
            {
                Refrescar(tUtc);
                int k = NumFam.UltimoMenorIgual(_gn, TiempoFam.Ms(tUtc));
                if (k < 0 || !(fut > 0)) return null;
                var f = _fn[k];
                if (f.Filas == null || f.Filas.Length == 0) return null;
                var exp = Expiracion(tUtc);
                double b = BaseNdx20.Base(f, fut, tUtc, exp, out var origen, out var carry, _op.Tasa, _op.Dividendo);
                if (double.IsNaN(b)) return null;
                double S = fut - b;
                if (S <= 0) return null;
                var gen = TiempoFam.DeMs(_gn[k]);
                var perfil = Nucleo20.Perfil(f, gen, S, tUtc, b, false);
                var doms = Nucleo20.Dominantes(perfil, fut, out var lib);
                bool cong = f.DatoUtc.Year >= 2000 && TiempoFam.ANy(f.DatoUtc).TimeOfDay >= new TimeSpan(15, 59, 0);
                var r = new Resultado20 { Libro = LIBRO_NDX, Foto = f, GeneradoUtc = gen, Conv = b, S = S, Fut = fut, LibroDom = lib, Strikes = perfil.Count, Doms = doms,
                                          OrigenBase = origen, Carry = carry, Congelada = cong };
                r.Texto = "base 2.0 " + b.ToString("0.00", Inv) + " (" + origen + (origen.StartsWith("CRUDA", StringComparison.Ordinal) ? " de forwards de la cadena" : "")
                          + (double.IsNaN(carry) ? "" : "; cota: carry " + carry.ToString("0.0", Inv) + " +-60 %") + ")"
                          + (lib == "OI" && doms.Count > 0 ? "; dominantes por OI (sin volumen en el radio)" : "");
                return r;
            }
        }

        /// <summary>Vencimiento del futuro del grafico (13:30 UTC del tercer viernes), como la 2.0: del codigo; si no trae mes, el trimestral mas cercano.</summary>
        public DateTime Expiracion(DateTime tUtc)
        {
            var e = BaseNdx20.ExpiracionDeCodigo(_op.Contrato, tUtc);
            return e != default(DateTime) ? e : BaseNdx20.TrimestralDesde(tUtc.AddDays(1));
        }

        private static string TextoRazon(Razon20 r)
        {
            string s = "razon 2.0 " + r.Razon.ToString("0.0000", Inv);
            if (r.Valida && r.Origen.StartsWith("vela alineada", StringComparison.Ordinal) && !double.IsNaN(r.PAl))
                s += " = MNQ " + r.PAl.ToString("0.00", Inv) + " (vela de 1 min de las " + TiempoFam.ANy(r.UtUtc).ToString("HH:mm", Inv) + " NY, la del ultimo trade de la cadena"
                     + (r.PAlDeTick ? "" : "; sin sus ticks en la cinta: vela m2 interpolada") + ") / spot QQQ " + r.Spot.ToString("0.00", Inv);
            s += " · " + r.Origen;
            if (r.Congelada) s += " · de noche junta el MNQ de esa vela con el spot de QQQ de ahora (falla conocida de la 2.0)";
            return s;
        }

        // ------------------------------------------------------------------ IExtrasMinuto
        /// <summary>4.1.4: el fut con el que se ponen las R20 del minuto t. Con FutDelInstante (default): el ultimo precio del MNQ al empezar el minuto
        /// (el ultimo tick con hora &lt;= t, a lo sumo 120 s antes; lo mas parecido al "cierre de la vela actual" con que reprecia la 2.0 y lo mismo en vivo
        /// que al recalcular: si a la hora de calcular todavia no llego un tick posterior a t, el ultimo tick ES el de antes de t). Sin tick en esos
        /// 120 s, o con FutDelInstante apagado: el fut del minuto de la 4.1 (cierre de la ultima vela m2 cerrada, el de todas las series).</summary>
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
            PonerReplicas(ex, ks, metas, tUtc, FutR20(tUtc, fut));
            if (bk != null)
                foreach (var b in bk)
                {
                    if (b == null) continue;
                    int i = b.Libro == "QQQ" ? CatalogoFamilia.IndiceExtra("DOMS_QQQ_vol") : b.Libro == "NDX" ? CatalogoFamilia.IndiceExtra("DOMS_NDX_vol") : -1;
                    if (i < 0) continue;
                    var doms = Nucleo20.Dominantes(b, out _);
                    Poner(ex, ks, i, doms, ReglasFam.Escala(b));
                }
            Guardar(r, ex, ks, metas);
        }

        public bool Completar(RegistroMinuto r, DateTime tUtc)
        {
            if (r == null || double.IsNaN(r.FutMnq)) return false;
            int n = CatalogoFamilia.Extra.Length;
            var ex = new Nivel[n][]; var ks = new double[n][]; var metas = new List<MetaLibro>(2);
            PonerReplicas(ex, ks, metas, tUtc, FutR20(tUtc, r.FutMnq));
            return Guardar(r, ex, ks, metas);
        }

        /// <summary>4.1.4 (IExtrasRehacer): rehace en un minuto YA calculado lo que depende de la razon de la 2.0 (R20_QQQ_vol y su conversion "2.0 QQQ"),
        /// con el estado de la razon de ahora; el resto (R20_NDX_vol, DOMS_*) queda igual. true si cambio algo (el motor lo vuelve a guardar).</summary>
        public bool Rehacer(RegistroMinuto r, DateTime tUtc)
        {
            if (r == null || double.IsNaN(r.FutMnq)) return false;
            int iq = CatalogoFamilia.IndiceExtra("R20_QQQ_vol"), n = CatalogoFamilia.Extra.Length;
            if (iq < 0) return false;
            var q = Qqq(tUtc, FutR20(tUtc, r.FutMnq));
            var ex1 = new Nivel[n][]; var ks1 = new double[n][];
            if (q != null) Poner(ex1, ks1, iq, q.Doms, 1.0);
            var meta = q == null ? null : new MetaLibro { Libro = LIBRO_QQQ, Conv = q.Conv, S = q.S, DatoUtc = q.Foto.DatoUtc, OiOk = true, Congelada = q.Congelada, ConvTexto = q.Texto };
            var lvViejo = r.Extra != null && iq < r.Extra.Length ? r.Extra[iq] : null;
            var ksViejo = r.ExtraStrikes != null && iq < r.ExtraStrikes.Length ? r.ExtraStrikes[iq] : null;
            if (MismosNiveles(lvViejo, ksViejo, ex1[iq], ks1[iq]) && MismaMeta(r.MetaExtraDe(LIBRO_QQQ), meta)) return false;
            var ex = new Nivel[n][]; var ks = new double[n][];
            if (r.Extra != null) Array.Copy(r.Extra, ex, Math.Min(n, r.Extra.Length));
            if (r.ExtraStrikes != null) Array.Copy(r.ExtraStrikes, ks, Math.Min(n, r.ExtraStrikes.Length));
            ex[iq] = ex1[iq]; ks[iq] = ks1[iq];
            var metas = new List<MetaLibro>(2);
            if (meta != null) metas.Add(meta);                                   // el mismo orden que PonerReplicas: "2.0 QQQ" y despues "2.0 NDX"
            if (r.MetaExtra != null) foreach (var m in r.MetaExtra) if (m != null && m.Libro != LIBRO_QQQ) metas.Add(m);
            r.Extra = ex; r.ExtraStrikes = ks; r.MetaExtra = metas.ToArray();
            return true;
        }

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

        private void PonerReplicas(Nivel[][] ex, double[][] ks, List<MetaLibro> metas, DateTime tUtc, double fut)
        {
            var q = Qqq(tUtc, fut);
            if (q != null)
            {
                Poner(ex, ks, CatalogoFamilia.IndiceExtra("R20_QQQ_vol"), q.Doms, 1.0);
                metas.Add(new MetaLibro { Libro = LIBRO_QQQ, Conv = q.Conv, S = q.S, DatoUtc = q.Foto.DatoUtc, OiOk = true, Congelada = q.Congelada, ConvTexto = q.Texto });
            }
            var d = Ndx(tUtc, fut);
            if (d != null)
            {
                Poner(ex, ks, CatalogoFamilia.IndiceExtra("R20_NDX_vol"), d.Doms, 1.0);
                metas.Add(new MetaLibro { Libro = LIBRO_NDX, Conv = d.Conv, S = d.S, DatoUtc = d.Foto.DatoUtc, OiOk = true, Congelada = d.Congelada, ConvTexto = d.Texto });
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
