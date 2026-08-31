using System;
using System.Collections.Generic;
using System.Linq;

using ATAS.Indicators;

namespace PythiaGex
{
    /// <summary>
    /// El contexto que aporta ATAS y que ninguna web de GEX puede dar.
    ///
    /// Cada vela de ATAS trae adentro el footprint completo: cuanto se opero en
    /// cada precio, cuanto contra el bid y cuanto contra el ask. Con eso se
    /// arma, sin pedirle nada a nadie:
    ///
    ///   - el perfil de volumen compuesto de la sesion (POC, VAH, VAL)
    ///   - el VWAP acumulado con sus bandas de desvio
    ///   - cuanto volumen y cuanto delta hay parado EN cada nivel de gamma
    ///
    /// Ese ultimo es el que decide. Un Call Wall con precio pegado y delta
    /// comprador fuerte se esta rompiendo. El mismo Call Wall con delta
    /// vendedor y volumen alto se esta defendiendo. La cadena de opciones dice
    /// donde esta el nivel; el order flow dice quien esta ganando ahi.
    /// </summary>
    public sealed class Contexto
    {
        public sealed class Nodo
        {
            public decimal Precio;
            public decimal Volumen;
            public decimal Delta;      // ask - bid: agresion neta
        }

        public sealed class Zona
        {
            public decimal Volumen, Delta, Bid, Ask;
            public decimal PctVolumenSesion;
            public bool Absorcion;     // mucho volumen, poco avance
        }

        // Perfil de volumen
        public decimal Poc, Vah, Val;
        public decimal VolumenTotal;
        public List<Nodo> Nodos = new();
        public List<decimal> NodosAltos = new();   // high volume nodes
        public List<decimal> NodosBajos = new();   // low volume nodes: por ahi pasa rapido

        // VWAP acumulado y bandas
        public decimal Vwap, Sigma;
        public decimal VwapMas1, VwapMenos1, VwapMas2, VwapMenos2;

        // Sesion
        public decimal Apertura, Maximo, Minimo, Cierre;
        public decimal IbAlto, IbBajo;
        public decimal DeltaAcumulado, DeltaMaximo, DeltaMinimo;
        public decimal VolumenSesion;
        public int BarrasUsadas;
        public DateTime Desde, Hasta;
        public bool Listo;

        /// <summary>Umbrales. Ninguno es una ley: son cortes elegidos, y por eso
        /// se pueden cambiar desde los ajustes y quedan a la vista en el tablero.</summary>
        public decimal PctValueArea = 0.70m;      // convencion de Market Profile
        public decimal FactorNodoAlto = 2.0m;     // veces el volumen promedio del perfil
        public decimal FactorNodoBajo = 0.3m;
        public decimal MinPctAbsorcion = 4.0m;    // % del volumen de la sesion
        public decimal MaxRatioAbsorcion = 0.12m; // |delta| / volumen
        public int MinutosIb = 60;                // Initial Balance

        /// <summary>
        /// Recalcula todo desde las velas. Se llama en barra nueva, no en cada
        /// tick: recorrer el footprint de cientos de barras es caro y el perfil
        /// no cambia de manera util entre dos ticks.
        /// </summary>
        public void Calcular(Func<int, IndicatorCandle> vela, int desde, int hasta,
                             decimal tickSize)
        {
            Listo = false;
            if (desde < 0 || hasta < desde) return;

            var acum = new Dictionary<decimal, Nodo>();
            decimal sumaPV = 0, sumaV = 0;
            decimal hi = decimal.MinValue, lo = decimal.MaxValue;
            decimal delta = 0, dMax = decimal.MinValue, dMin = decimal.MaxValue;
            int n = 0;

            for (int b = desde; b <= hasta; b++)
            {
                IndicatorCandle c;
                try { c = vela(b); } catch { continue; }
                if (c == null) continue;

                if (n == 0) { Apertura = c.Open; Desde = c.Time; }
                Cierre = c.Close; Hasta = c.LastTime;
                if (c.High > hi) hi = c.High;
                if (c.Low < lo) lo = c.Low;

                delta += c.Delta;
                if (delta > dMax) dMax = delta;
                if (delta < dMin) dMin = delta;

                if (c.Volume > 0 && c.VWAP > 0)
                {
                    sumaPV += c.VWAP * c.Volume;
                    sumaV += c.Volume;
                }

                try
                {
                    foreach (var l in c.GetAllPriceLevels())
                    {
                        if (l == null || l.Volume <= 0) continue;
                        if (!acum.TryGetValue(l.Price, out var nd))
                            acum[l.Price] = nd = new Nodo { Precio = l.Price };
                        nd.Volumen += l.Volume;
                        nd.Delta += l.Ask - l.Bid;
                    }
                }
                catch { /* una barra sin footprint no invalida el resto */ }
                n++;
            }

            if (n == 0 || acum.Count == 0) return;

            BarrasUsadas = n;
            Maximo = hi; Minimo = lo;
            VolumenSesion = sumaV;
            DeltaAcumulado = delta;
            DeltaMaximo = dMax == decimal.MinValue ? 0 : dMax;
            DeltaMinimo = dMin == decimal.MaxValue ? 0 : dMin;

            // Initial Balance: primera hora. Se aproxima por tiempo real de las
            // velas, asi sirve igual en graficos de volumen o de rango.
            var limite = Desde.AddMinutes(Math.Max(1, MinutosIb));
            decimal ibh = decimal.MinValue, ibl = decimal.MaxValue;
            for (int b = desde; b <= hasta; b++)
            {
                IndicatorCandle c;
                try { c = vela(b); } catch { continue; }
                if (c == null || c.Time > limite) break;
                if (c.High > ibh) ibh = c.High;
                if (c.Low < ibl) ibl = c.Low;
            }
            IbAlto = ibh == decimal.MinValue ? hi : ibh;
            IbBajo = ibl == decimal.MaxValue ? lo : ibl;

            // VWAP acumulado
            Vwap = sumaV > 0 ? sumaPV / sumaV : 0;

            Nodos = acum.Values.OrderBy(x => x.Precio).ToList();
            VolumenTotal = Nodos.Sum(x => x.Volumen);

            // desvio del volumen alrededor del VWAP, que es lo que da las bandas
            if (Vwap > 0 && VolumenTotal > 0)
            {
                double acu = 0;
                foreach (var nd in Nodos)
                {
                    var d = (double)(nd.Precio - Vwap);
                    acu += d * d * (double)nd.Volumen;
                }
                Sigma = (decimal)Math.Sqrt(acu / (double)VolumenTotal);
                VwapMas1 = Vwap + Sigma; VwapMenos1 = Vwap - Sigma;
                VwapMas2 = Vwap + 2 * Sigma; VwapMenos2 = Vwap - 2 * Sigma;
            }

            // POC y area de valor por expansion desde el POC
            var poc = Nodos.OrderByDescending(x => x.Volumen).First();
            Poc = poc.Precio;
            int iPoc = Nodos.FindIndex(x => x.Precio == Poc);
            decimal objetivo = VolumenTotal * PctValueArea, dentro = poc.Volumen;
            int a = iPoc, z = iPoc;
            while (dentro < objetivo && (a > 0 || z < Nodos.Count - 1))
            {
                decimal arriba = z < Nodos.Count - 1 ? Nodos[z + 1].Volumen : -1;
                decimal abajo = a > 0 ? Nodos[a - 1].Volumen : -1;
                if (arriba >= abajo && arriba >= 0) { z++; dentro += arriba; }
                else if (abajo >= 0) { a--; dentro += abajo; }
                else break;
            }
            Val = Nodos[a].Precio; Vah = Nodos[z].Precio;

            // Nodos de alto y bajo volumen, relativos al promedio del perfil.
            // Los altos frenan, los bajos se cruzan rapido.
            var prom = VolumenTotal / Nodos.Count;
            NodosAltos = Nodos.Where(x => x.Volumen >= prom * FactorNodoAlto)
                              .OrderByDescending(x => x.Volumen).Take(8)
                              .Select(x => x.Precio).ToList();
            NodosBajos = Nodos.Where(x => x.Volumen <= prom * FactorNodoBajo)
                              .Select(x => x.Precio).ToList();

            Listo = true;
        }

        /// <summary>Volumen y delta operados dentro de una banda de precio.</summary>
        public Zona EnNivel(decimal precio, decimal tickSize, int ticks)
        {
            var z = new Zona();
            if (!Listo || tickSize <= 0) return z;
            var tol = tickSize * ticks;
            foreach (var nd in Nodos)
            {
                if (Math.Abs(nd.Precio - precio) > tol) continue;
                z.Volumen += nd.Volumen;
                z.Delta += nd.Delta;
            }
            z.PctVolumenSesion = VolumenTotal > 0 ? z.Volumen / VolumenTotal * 100m : 0;
            // Absorcion: mucho volumen concentrado y delta chico en relacion.
            // Alguien esta comiendo todo lo que le tiran sin mover el precio.
            z.Absorcion = z.PctVolumenSesion >= MinPctAbsorcion
                          && z.Volumen > 0
                          && Math.Abs(z.Delta) / z.Volumen < MaxRatioAbsorcion;
            return z;
        }

        /// <summary>Distancia en ticks entre dos precios.</summary>
        public static int Ticks(decimal a, decimal b, decimal tickSize)
            => tickSize <= 0 ? 0 : (int)Math.Round((a - b) / tickSize);
    }
}
