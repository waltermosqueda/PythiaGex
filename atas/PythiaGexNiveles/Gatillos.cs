using System;
using System.Collections.Generic;
using System.Linq;

using ATAS.Indicators;

namespace PythiaGex
{
    /// <summary>
    /// El order flow como GATILLO, no como adorno.
    ///
    /// La regla de disenio es una sola y es la que evita que la pantalla se
    /// llene de flechitas: <b>el order flow solo habla parado sobre un nivel
    /// publicado</b>. En el resto del grafico es ruido. La cadena de opciones
    /// dice DONDE puede pasar algo; el flujo dice CUANDO esta pasando. Por
    /// separado no sirve ninguno de los dos.
    ///
    /// Tres cosas se miden, y ninguna es una opinion:
    ///
    ///   1. IMBALANCES APILADOS (stacked imbalances). En el footprint se
    ///      compara en diagonal: lo que se compro a un precio contra lo que se
    ///      vendio al precio de abajo. Si comprando gana por goleada varias
    ///      veces seguidas, hay alguien barriendo hacia arriba. Apilados de a
    ///      tres o mas es la senal clasica de footprint.
    ///
    ///   2. PRINTS GRANDES (block prints). Una sola operacion, en un solo
    ///      precio y una sola barra, muy por encima de lo normal de ese rato.
    ///      Es el unico rastro visible de que entro tamano de verdad y no
    ///      cientos de contratos chicos que suman lo mismo.
    ///
    ///   3. DIVERGENCIA DE DELTA. El precio hace un extremo nuevo y la
    ///      agresion acumulada no lo acompana. Es agotamiento: quedan pocos
    ///      dispuestos a seguir empujando.
    ///
    /// Todo lo que sale de aca es medido sobre el footprint que ya trae cada
    /// vela de ATAS. No hay ningun numero inventado ni ninguna constante
    /// magica escondida: los tres umbrales estan arriba de todo, con su razon
    /// al lado, y se pueden cambiar desde los ajustes.
    /// </summary>
    public sealed class Gatillos
    {
        /// <summary>Cuantas veces tiene que ganar una diagonal para llamarla
        /// desbalanceada. 3.0 es la convencion mas usada en footprint; abajo de
        /// 2 se dispara con cualquier cosa.</summary>
        public decimal FactorImbalance = 3.0m;

        /// <summary>Cuantos imbalances seguidos hacen falta para que cuente.
        /// Uno solo es ruido; tres apilados es alguien barriendo.</summary>
        public int MinApilados = 3;

        /// <summary>Cuantas veces el volumen tipico de un precio tiene que
        /// tener un print para llamarlo grande.</summary>
        public decimal FactorPrint = 8.0m;

        /// <summary>Cuantas barras para atras se mira. Es la ventana del
        /// gatillo: si es muy larga deja de ser un gatillo y pasa a ser
        /// contexto, que ya lo da el perfil.</summary>
        public int Ventana = 20;

        public sealed class Senal
        {
            /// <summary>Imbalances apilados encontrados en la zona.</summary>
            public int Apilados;
            /// <summary>+1 si el apilamiento es comprador, -1 si es vendedor.</summary>
            public int Lado;
            /// <summary>El print mas grande visto en la zona, en contratos.</summary>
            public decimal PrintMayor;
            /// <summary>Cuantas veces el volumen tipico fue ese print.</summary>
            public decimal PrintVeces;
            public bool PrintGrande;
            /// <summary>El precio hizo extremo nuevo y el delta no acompano.</summary>
            public bool Divergencia;
            /// <summary>+1 divergencia alcista (piso), -1 bajista (techo).</summary>
            public int LadoDivergencia;
            /// <summary>Delta agredido dentro de la zona en la ventana.</summary>
            public decimal DeltaVentana;
            public decimal VolumenVentana;
            public int BarrasVistas;
            public bool Listo;

            /// <summary>Resumen corto para la etiqueta. Vacio si no hay nada
            /// que decir, que es lo normal la mayor parte del tiempo.</summary>
            public string Corto()
            {
                var p = new List<string>();
                if (Apilados > 0)
                    p.Add((Lado > 0 ? "+" : "-") + Apilados + "imb");
                if (PrintGrande)
                    p.Add("print x" + PrintVeces.ToString("0"));
                if (Divergencia)
                    p.Add(LadoDivergencia > 0 ? "div+" : "div-");
                return string.Join(" ", p);
            }
        }

        /// <summary>
        /// Mira el footprint de las ultimas barras dentro de la zona del nivel.
        /// </summary>
        /// <param name="vela">acceso a la vela por indice</param>
        /// <param name="ultima">indice de la ultima barra cerrada o en curso</param>
        /// <param name="precio">el nivel, en precio del instrumento</param>
        /// <param name="tickSize">tamano del tick</param>
        /// <param name="ticks">semiancho de la zona, en ticks</param>
        public Senal Mirar(Func<int, IndicatorCandle> vela, int ultima,
                           decimal precio, decimal tickSize, int ticks)
        {
            var s = new Senal();
            if (tickSize <= 0 || ultima < 1) return s;

            var tol = tickSize * Math.Max(1, ticks);
            int desde = Math.Max(0, ultima - Math.Max(2, Ventana) + 1);

            // volumen por precio dentro de la zona, y el print mas grande
            var porPrecio = new Dictionary<decimal, decimal>();
            var ask = new Dictionary<decimal, decimal>();
            var bid = new Dictionary<decimal, decimal>();
            decimal printMayor = 0;
            // La ventana se parte al medio para poder comparar la mitad vieja
            // con la nueva. Es la unica forma de decir "hizo minimo nuevo" sin
            // meter una definicion arbitraria de que es un minimo.
            int mitad = desde + (ultima - desde) / 2;
            decimal hiA = decimal.MinValue, loA = decimal.MaxValue, dA = 0;
            decimal hiB = decimal.MinValue, loB = decimal.MaxValue, dB = 0;
            int n = 0;

            for (int b = desde; b <= ultima; b++)
            {
                IndicatorCandle c;
                try { c = vela(b); } catch { continue; }
                if (c == null) continue;
                n++;

                if (b <= mitad)
                {
                    if (c.High > hiA) hiA = c.High;
                    if (c.Low < loA) loA = c.Low;
                    dA += c.Delta;
                }
                else
                {
                    if (c.High > hiB) hiB = c.High;
                    if (c.Low < loB) loB = c.Low;
                    dB += c.Delta;
                }

                try
                {
                    foreach (var l in c.GetAllPriceLevels())
                    {
                        if (l == null || l.Volume <= 0) continue;
                        if (Math.Abs(l.Price - precio) > tol) continue;
                        porPrecio.TryGetValue(l.Price, out var v);
                        porPrecio[l.Price] = v + l.Volume;
                        ask.TryGetValue(l.Price, out var a); ask[l.Price] = a + l.Ask;
                        bid.TryGetValue(l.Price, out var d); bid[l.Price] = d + l.Bid;
                        if (l.Volume > printMayor) printMayor = l.Volume;
                        s.VolumenVentana += l.Volume;
                        s.DeltaVentana += l.Ask - l.Bid;
                    }
                }
                catch { /* una barra sin footprint no invalida la ventana */ }
            }

            s.BarrasVistas = n;
            if (porPrecio.Count == 0) return s;

            // --- print grande: contra el volumen tipico de un precio en la zona
            var tipico = Mediana(porPrecio.Values.ToList());
            // el tipico es de toda la ventana; el print es de una sola barra,
            // asi que se compara contra el tipico repartido entre las barras
            var tipicoPorBarra = n > 0 ? tipico / n : tipico;
            s.PrintMayor = printMayor;
            s.PrintVeces = tipicoPorBarra > 0 ? printMayor / tipicoPorBarra : 0;
            s.PrintGrande = tipicoPorBarra > 0 && s.PrintVeces >= FactorPrint;

            // --- imbalances apilados: diagonal ask(P) contra bid(P - 1 tick)
            var precios = porPrecio.Keys.OrderBy(x => x).ToList();
            int seguidasC = 0, seguidasV = 0, mejorC = 0, mejorV = 0;
            for (int i = 1; i < precios.Count; i++)
            {
                var arriba = precios[i];
                var abajo = precios[i - 1];
                // solo cuenta si son precios contiguos de verdad
                if (Math.Abs((arriba - abajo) - tickSize) > tickSize / 2)
                { seguidasC = seguidasV = 0; continue; }

                ask.TryGetValue(arriba, out var aArriba);
                bid.TryGetValue(abajo, out var bAbajo);
                ask.TryGetValue(abajo, out var aAbajo);
                bid.TryGetValue(arriba, out var bArriba);

                bool compra = bAbajo > 0 && aArriba >= bAbajo * FactorImbalance;
                bool venta = aAbajo > 0 && bArriba >= aAbajo * FactorImbalance;

                seguidasC = compra ? seguidasC + 1 : 0;
                seguidasV = venta ? seguidasV + 1 : 0;
                if (seguidasC > mejorC) mejorC = seguidasC;
                if (seguidasV > mejorV) mejorV = seguidasV;
            }
            if (mejorC >= MinApilados || mejorV >= MinApilados)
            {
                if (mejorC >= mejorV) { s.Apilados = mejorC; s.Lado = 1; }
                else { s.Apilados = mejorV; s.Lado = -1; }
            }

            // --- divergencia, dicho en una frase: la mitad nueva de la ventana
            // llego mas lejos que la vieja, pero con menos agresion detras.
            //
            //   alcista  -> hizo minimo mas bajo y sin embargo se vendio MENOS
            //               que antes. La venta empujo sin nafta: agotamiento.
            //   bajista  -> hizo maximo mas alto y se compro MENOS que antes.
            //
            // Se pide que las dos mitades tengan al menos dos barras cada una,
            // porque con una sola barra cualquier cosa "diverge".
            if (n >= 4 && loA != decimal.MaxValue && loB != decimal.MaxValue)
            {
                if (loB < loA && dB > dA) { s.Divergencia = true; s.LadoDivergencia = 1; }
                else if (hiB > hiA && dB < dA) { s.Divergencia = true; s.LadoDivergencia = -1; }
            }

            s.Listo = true;
            return s;
        }

        private static decimal Mediana(List<decimal> xs)
        {
            if (xs == null || xs.Count == 0) return 0;
            var o = xs.OrderBy(x => x).ToList();
            int m = o.Count / 2;
            return o.Count % 2 == 1 ? o[m] : (o[m - 1] + o[m]) / 2m;
        }
    }
}
