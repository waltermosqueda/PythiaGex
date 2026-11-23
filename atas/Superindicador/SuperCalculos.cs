using System;
using System.Collections.Generic;
using System.Globalization;

namespace PythiaSuper
{
    /// <summary>
    /// LA CUENTA, SIN NADA DE ATAS (23-11-2026). Vive aparte para poder probarla fuera de la plataforma con datos sinteticos y
    /// contra una implementacion independiente en Python (herramientas/auditar_superindicador.py).
    ///
    /// Convenciones (las de ATAS, verificadas por reflexion y en ATAS.Indicators.Technical 8.0.15.306):
    /// - Cada nivel de precio de una vela trae Ask (volumen que COMPRO a mercado, agresor comprador), Bid (volumen que VENDIO a
    ///   mercado, agresor vendedor) y Volume (total del nivel). Delta del nivel = Ask - Bid. Es la misma resta que hacen el
    ///   MaxLevels y el DynamicLevels de fabrica (priceVolumeInfo.Ask - priceVolumeInfo.Bid).
    /// - Una fila agrupa K ticks: fila = piso(tick / K), alineada a la grilla ABSOLUTA de precios (con K = 4 en MES, cada fila es
    ///   un punto entero: 6828,00-6828,75). La fila no depende del zoom ni de donde empieza la ventana.
    /// - Area de valor: el algoritmo de ATAS (DynamicLevels.GetValueArea) con los huecos adentro, como el area de valor de cada
    ///   vela de ATAS: desde el POC, de a "paso" filas hacia arriba y hacia abajo; se suma el lado con mas volumen (empate:
    ///   arriba) hasta llegar al porcentaje del volumen total. POC empatado: el precio mas alto (como ATAS).
    /// </summary>
    public sealed class BarraFp
    {
        public int Bar;
        public DateTime Hora, HoraFin;                 // apertura y ultimo trade de la vela (UTC, como los da ATAS)
        public int TickMin;                            // tick (precio / tamano del tick) del nivel mas bajo con datos
        public decimal[] Bid = new decimal[0], Ask = new decimal[0], Vol = new decimal[0];   // por tick desde TickMin
        public decimal SumBid, SumAsk, SumVol;
        public decimal DeltaAtas, VolAtas;             // lo que ATAS trae calculado en la vela: control cruzado
        public decimal O, H, L, C;
        public int Niveles;                            // niveles con datos
        public int NivelesRepetidos;                   // el mismo tick dos veces en GetAllPriceLevels (no deberia pasar)
    }

    public sealed class Celda
    {
        public decimal Bid, Ask, Vol;
    }

    /// <summary>Suma de velas por filas de K ticks.</summary>
    public sealed class Acum
    {
        public readonly int K;
        public readonly Dictionary<int, Celda> Celdas = new Dictionary<int, Celda>();
        public decimal SumBid, SumAsk, SumVol, SumDeltaAtas, SumVolAtas;
        public int Velas, VelasSinDatos;
        public DateTime T0 = DateTime.MaxValue, T1 = DateTime.MinValue;

        public Acum(int k) { K = Math.Max(1, k); }

        /// <summary>Division entera hacia abajo (piso), tambien para ticks negativos.</summary>
        public static int Piso(int t, int k) => t >= 0 ? t / k : -((-t + k - 1) / k);

        public void Sumar(BarraFp b)
        {
            if (b == null) return;
            Velas++;
            if (b.Hora < T0) T0 = b.Hora;
            var fin = b.HoraFin > b.Hora ? b.HoraFin : b.Hora;
            if (fin > T1) T1 = fin;
            SumDeltaAtas += b.DeltaAtas;
            SumVolAtas += b.VolAtas;
            if (b.Niveles == 0) { VelasSinDatos++; return; }
            for (int i = 0; i < b.Vol.Length; i++)
            {
                decimal bi = b.Bid[i], a = b.Ask[i], v = b.Vol[i];
                if (bi == 0m && a == 0m && v == 0m) continue;
                int f = Piso(b.TickMin + i, K);
                if (!Celdas.TryGetValue(f, out var c)) { c = new Celda(); Celdas[f] = c; }
                c.Bid += bi; c.Ask += a; c.Vol += v;
                SumBid += bi; SumAsk += a; SumVol += v;
            }
        }

        public Acum Copia()
        {
            var r = new Acum(K)
            {
                SumBid = SumBid, SumAsk = SumAsk, SumVol = SumVol, SumDeltaAtas = SumDeltaAtas, SumVolAtas = SumVolAtas,
                Velas = Velas, VelasSinDatos = VelasSinDatos, T0 = T0, T1 = T1
            };
            foreach (var kv in Celdas) r.Celdas[kv.Key] = new Celda { Bid = kv.Value.Bid, Ask = kv.Value.Ask, Vol = kv.Value.Vol };
            return r;
        }
    }

    /// <summary>Filas contiguas (de abajo hacia arriba) listas para dibujar, con POC y area de valor.</summary>
    public sealed class Perfil
    {
        public int K, FilaMin;
        public decimal[] Bid = new decimal[0], Ask = new decimal[0], Vol = new decimal[0];
        public decimal SumBid, SumAsk, SumVol, SumDeltaAtas, SumVolAtas;
        public int Velas, VelasSinDatos, Desde = -1, Hasta = -1;
        public DateTime T0, T1;
        public int Poc = -1, Vah = -1, Val = -1;      // indices de fila
        public decimal VolAv;                          // volumen que quedo adentro del area de valor
        public int Pct, Paso;
        public BarraFp Viva;                           // la vela en curso EXACTA que entro en esta suma (para la auditoria)
        public DateTime HoraDesde, HoraHasta;          // apertura de la primera y de la ultima vela de la ventana
        public bool Recortado;                         // el rango era absurdo (un tick erroneo): se recorto alrededor del volumen

        public int Filas => Vol.Length;
        public decimal Delta(int i) => Ask[i] - Bid[i];
        public int TickBajo(int i) => (FilaMin + i) * K;
        public int TickAlto(int i) => (FilaMin + i) * K + K - 1;

        public const int MaxFilas = 60000;

        public static Perfil DeAcum(Acum a, int desde, int hasta)
        {
            var p = new Perfil
            {
                K = a.K, SumBid = a.SumBid, SumAsk = a.SumAsk, SumVol = a.SumVol, SumDeltaAtas = a.SumDeltaAtas,
                SumVolAtas = a.SumVolAtas, Velas = a.Velas, VelasSinDatos = a.VelasSinDatos, Desde = desde, Hasta = hasta,
                T0 = a.T0, T1 = a.T1
            };
            if (a.Celdas.Count == 0) return p;
            int min = int.MaxValue, max = int.MinValue;
            foreach (var f in a.Celdas.Keys) { if (f < min) min = f; if (f > max) max = f; }
            long n = (long)max - min + 1;
            if (n > MaxFilas)
            {
                // un precio erroneo lejisimo dejaria millones de filas vacias: se conserva la franja de MaxFilas alrededor de la
                // fila de mas volumen y se avisa (Recortado). No pasa con datos sanos de futuros.
                int mejor = min; decimal vmax = -1;
                foreach (var kv in a.Celdas) if (kv.Value.Vol > vmax) { vmax = kv.Value.Vol; mejor = kv.Key; }
                min = mejor - MaxFilas / 2; max = min + MaxFilas - 1; n = MaxFilas; p.Recortado = true;
            }
            p.FilaMin = min;
            p.Bid = new decimal[n]; p.Ask = new decimal[n]; p.Vol = new decimal[n];
            foreach (var kv in a.Celdas)
            {
                long i = (long)kv.Key - min;
                if (i < 0 || i >= n) continue;
                p.Bid[i] = kv.Value.Bid; p.Ask[i] = kv.Value.Ask; p.Vol[i] = kv.Value.Vol;
            }
            return p;
        }
    }

    public static class Calculos
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>POC: la fila de mas volumen. Empate: la de PRECIO MAS ALTO, la regla de ATAS (medida el 23-11-2026: en 28 de 28
        /// velas de MES con empate, IndicatorCandle.MaxVolumePriceInfo era el nivel mas alto de los empatados).</summary>
        public static int Poc(Perfil p)
        {
            int n = p.Filas; if (n == 0) return -1;
            decimal vmax = 0; int mejor = -1;
            for (int i = 0; i < n; i++)
                if (p.Vol[i] > 0 && p.Vol[i] >= vmax) { vmax = p.Vol[i]; mejor = i; }   // >= : ante empate gana la de arriba
            return mejor;
        }

        /// <summary>
        /// Area de valor con la regla de ATAS (la de DynamicLevels.GetValueArea, 8.0.15.306) y con los HUECOS adentro, como hace
        /// ATAS en el area de valor de cada vela (IndicatorCandle.ValueArea): verificado el 23-11-2026 contra ATAS en 1.163 de 1.163
        /// velas de MES (sin huecos salian 1.154). Arranca en el POC con su volumen; mientras lo acumulado sea &lt;= objetivo (pct
        /// del total) y no se hayan tocado los dos extremos: suma las proximas "paso" filas de arriba y las proximas "paso" de abajo
        /// (las vacias cuentan como fila con 0); agrega el lado mayor (empate: arriba); si los dos lados dan 0 (dos huecos) avanza
        /// por los dos y sigue; corta en cuanto lo acumulado llega al objetivo.
        /// </summary>
        public static void AreaDeValor(Perfil p, int pct, int paso)
        {
            p.Pct = pct; p.Paso = paso = Math.Max(1, paso);
            p.Poc = Poc(p); p.Vah = p.Val = p.Poc; p.VolAv = 0;
            if (p.Poc < 0) return;
            int bajo = -1, alto = -1;
            decimal total = 0;
            for (int i = 0; i < p.Filas; i++) if (p.Vol[i] > 0) { if (bajo < 0) bajo = i; alto = i; total += p.Vol[i]; }
            decimal objetivo = total * pct / 100m;
            decimal acum = p.Vol[p.Poc];
            int hi = p.Poc, lo = p.Poc;
            while (acum <= objetivo && !(hi >= alto && lo <= bajo))
            {
                decimal arriba = 0, abajo = 0;
                int hiN = hi, loN = lo;
                for (int s = 0; s < paso && hiN + 1 <= alto; s++) { hiN++; arriba += p.Vol[hiN]; }
                for (int s = 0; s < paso && loN - 1 >= bajo; s++) { loN--; abajo += p.Vol[loN]; }
                if (arriba == 0 && abajo == 0) { hi = hiN; lo = loN; }
                else if (arriba >= abajo) { hi = hiN; acum += arriba; }
                else { lo = loN; acum += abajo; }
                if (acum >= objetivo) break;
            }
            p.Vah = hi; p.Val = lo; p.VolAv = acum;
        }

        /// <summary>Numero entero exacto hasta 99.999; desde 100.000, en miles ("123k"). Regla fija: nunca depende del zoom.</summary>
        public static string Numero(decimal v, bool conSigno)
        {
            decimal a = Math.Abs(v);
            string s;
            if (a >= 100000m) s = Math.Round(a / 1000m, 0, MidpointRounding.AwayFromZero).ToString("0", Inv) + "k";
            else if (a == decimal.Truncate(a)) s = a.ToString("0", Inv);
            else s = a.ToString("0.##", Inv);
            if (v < 0) return "-" + s;
            if (conSigno && v > 0) return "+" + s;
            return s;
        }

        /// <summary>Miles con punto, sin decimales si es entero (para carteles: "12.345").</summary>
        public static string Miles(decimal v)
        {
            var ar = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            ar.NumberGroupSeparator = "."; ar.NumberDecimalSeparator = ",";
            return v == decimal.Truncate(v) ? v.ToString("#,0", ar) : v.ToString("#,0.##", ar);
        }
    }
}
