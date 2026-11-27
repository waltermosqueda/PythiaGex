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

    /// <summary>
    /// 1.5 (26-11-2026) JORNADA DE CME, sin nada de ATAS. Pedido del operador: "ver el indicador con los valores del dia anterior o
    /// de la sesion que yo elija, con la data limpia SOLO de esa jornada" (el rango visible juntaba dos dias) y que ande en el replay.
    /// Una jornada de los futuros de CME va de las 18:00 NY de la vispera a las 17:00 NY (el domingo 18:00 abre la del lunes). La
    /// jornada de una vela sale de la HORA NY DE SU APERTURA: desde las 18:00 cuenta para el dia siguiente. Partes: NOCHE = 18:00 a
    /// 9:30, RTH = 9:30 a 16:00, y DESPUES DEL CIERRE = 16:00 a 17:00 (va en la completa, no en la RTH ni en la noche).
    /// No depende de la plantilla de sesion del grafico ni del reloj de la PC: en el replay vale lo mismo.
    /// Limite dicho: en velas que no abren a horario (rango, ticks, volumen, o de 1 hora) la vela va entera a la parte de su apertura.
    /// </summary>
    public sealed class Jornada
    {
        public int Clave;                                    // aaaammdd del dia de la rueda
        public int Primera = -1, Ultima = -1;                // toda la jornada (velas, numeradas como en el grafico)
        public int NochePrimera = -1, NocheUltima = -1;      // 18:00 a 9:30 NY
        public int RthPrimera = -1, RthUltima = -1;          // 9:30 a 16:00 NY

        public void Agregar(int bar, int tipo)
        {
            if (Primera < 0 || bar < Primera) Primera = bar;
            if (bar > Ultima) Ultima = bar;
            if (tipo == Jornadas.TipoNoche) { if (NochePrimera < 0 || bar < NochePrimera) NochePrimera = bar; if (bar > NocheUltima) NocheUltima = bar; }
            else if (tipo == Jornadas.TipoRth) { if (RthPrimera < 0 || bar < RthPrimera) RthPrimera = bar; if (bar > RthUltima) RthUltima = bar; }
        }

        /// <summary>Velas de la parte pedida (Jornadas.ParteCompleta / ParteRth / ParteNoche); false si esa parte no tiene velas.</summary>
        public bool Rango(int parte, out int desde, out int hasta)
        {
            if (parte == Jornadas.ParteRth) { desde = RthPrimera; hasta = RthUltima; }
            else if (parte == Jornadas.ParteNoche) { desde = NochePrimera; hasta = NocheUltima; }
            else { desde = Primera; hasta = Ultima; }
            return desde >= 0 && hasta >= desde;
        }
    }

    public static class Jornadas
    {
        public const int TipoNoche = 0, TipoRth = 1, TipoDespues = 2;           // de cada vela
        public const int ParteCompleta = 0, ParteRth = 1, ParteNoche = 2;       // lo que se elige
        public static readonly TimeSpan Rth0 = new TimeSpan(9, 30, 0), Rth1 = new TimeSpan(16, 0, 0), Corte = new TimeSpan(18, 0, 0);
        private static readonly string[] Dias = { "dom", "lun", "mar", "mie", "jue", "vie", "sab" };

        /// <summary>aaaammdd de la jornada de una vela por la hora NY de su apertura. Sabado o domingo (no deberia haber velas antes del
        /// domingo 18:00) van al lunes.</summary>
        public static int Clave(DateTime ny)
        {
            var d = ny.TimeOfDay >= Corte ? ny.Date.AddDays(1) : ny.Date;
            if (d.DayOfWeek == DayOfWeek.Saturday) d = d.AddDays(2);
            else if (d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(1);
            return d.Year * 10000 + d.Month * 100 + d.Day;
        }

        public static int Tipo(DateTime ny)
        {
            var t = ny.TimeOfDay;
            if (t >= Rth0 && t < Rth1) return TipoRth;
            if (t >= Rth1 && t < Corte) return TipoDespues;
            return TipoNoche;
        }

        public static DateTime Fecha(int clave) => new DateTime(clave / 10000, clave / 100 % 100, clave % 100);

        /// <summary>Hora NY en la que empieza la parte: la completa y la noche a las 18:00 de la vispera (el lunes: domingo), la RTH a las 9:30.</summary>
        public static DateTime InicioNominal(int clave, int parte)
            => parte == ParteRth ? Fecha(clave).Add(Rth0) : Fecha(clave).AddDays(-1).Add(Corte);

        public static string Rotulo(int clave)
        {
            var d = Fecha(clave);
            return Dias[(int)d.DayOfWeek] + " " + d.ToString("dd-MM", CultureInfo.InvariantCulture);
        }

        public static string NombreParte(int parte) => parte == ParteRth ? "RTH" : parte == ParteNoche ? "noche" : "completa";

        /// <summary>Agrega una vela a la lista ordenada (las velas llegan en orden; una vela ya agregada no cambia de jornada).</summary>
        public static void Agregar(List<Jornada> js, int bar, DateTime ny)
        {
            int clave = Clave(ny), tipo = Tipo(ny);
            Jornada j = null;
            for (int i = js.Count - 1; i >= 0 && i >= js.Count - 3; i--) if (js[i].Clave == clave) { j = js[i]; break; }
            if (j == null)
            {
                j = new Jornada { Clave = clave };
                int pos = js.Count;
                while (pos > 0 && js[pos - 1].Clave > clave) pos--;   // por si una vela llega fuera de orden
                js.Insert(pos, j);
            }
            j.Agregar(bar, tipo);
        }

        /// <summary>Indice de la jornada N lugares antes de la ultima (0 = la ultima); -1 si no hay.</summary>
        public static int PorAtras(IList<Jornada> js, int atras)
        {
            if (js.Count == 0) return -1;
            return Math.Max(0, js.Count - 1 - Math.Max(0, atras));
        }

        /// <summary>La jornada que se esta mirando: la que tiene MAS velas a la vista de la parte pedida (empate: la mas nueva); si
        /// ninguna tiene esa parte a la vista, la que tiene mas velas a la vista en total. -1 si no hay nada a la vista.</summary>
        public static int AlaVista(IList<Jornada> js, int primVis, int ultVis, int parte)
        {
            int mejor = -1, mejorN = 0, mejorTot = -1, mejorTotN = 0;
            if (ultVis < primVis) return -1;
            for (int i = 0; i < js.Count; i++)
            {
                var j = js[i];
                int tot = Solape(j.Primera, j.Ultima, primVis, ultVis);
                if (tot > 0 && tot >= mejorTotN) { mejorTotN = tot; mejorTot = i; }
                if (j.Rango(parte, out int d, out int h))
                {
                    int n = Solape(d, h, primVis, ultVis);
                    if (n > 0 && n >= mejorN) { mejorN = n; mejor = i; }
                }
            }
            return mejor >= 0 ? mejor : mejorTot;
        }

        /// <summary>1.6, pedido del operador ("si voy corriendo el grafico el perfil queda fijo; tiene que ser dinamico, sincronizado
        /// segun la vista"): "a la vista" con la regla del BORDE. Si la ultima vela del grafico esta a la vista (grafico al dia, o el
        /// replay corriendo), la jornada es la ULTIMA que tiene la parte pedida (la que se opera; antes de las 9:30 la RTH es la de
        /// ayer); si el grafico esta corrido hacia atras, la que mas velas tiene a la vista (AlaVista).</summary>
        public static int AlaVistaConBorde(IList<Jornada> js, int primVis, int ultVis, int ultima, int parte)
        {
            if (js.Count == 0) return -1;
            if (ultima >= 0 && ultVis >= ultima)
            {
                for (int i = js.Count - 1; i >= 0; i--) if (js[i].Rango(parte, out _, out _)) return i;
                return js.Count - 1;
            }
            return AlaVista(js, primVis, ultVis, parte);
        }

        /// <summary>1.7, pedido del operador ("al ir moviendo el grafico, el delta y el perfil tienen que mostrar como estaban EN ESE
        /// MOMENTO, como un footprint real; si todo queda estatico no sirve para analizar retrocediendo"): la parte de jornada tal como
        /// existia a la altura de la vela m. La jornada es la de la vela m; si esa parte ya habia empezado, va desde su comienzo hasta
        /// m (o hasta su final si ya habia terminado); si todavia no habia empezado (por ejemplo la RTH a las 8:00), es la misma parte
        /// de la jornada anterior mas reciente que la tenga, COMPLETA (la que se tenia a la vista en ese momento). false si no hay.</summary>
        public static bool AlMomento(IList<Jornada> js, int m, int parte, out int indice, out int desde, out int hasta)
        {
            indice = desde = hasta = -1;
            int j = -1;
            for (int i = js.Count - 1; i >= 0; i--) if (js[i].Primera >= 0 && js[i].Primera <= m) { j = i; break; }
            for (int i = j; i >= 0; i--)
            {
                if (!js[i].Rango(parte, out int d, out int h) || d > m) continue;
                indice = i; desde = d; hasta = Math.Min(h, m);
                return true;
            }
            return false;
        }

        private static int Solape(int a0, int a1, int b0, int b1)
        {
            if (a0 < 0 || a1 < a0) return 0;
            int x0 = Math.Max(a0, b0), x1 = Math.Min(a1, b1);
            return x1 >= x0 ? x1 - x0 + 1 : 0;
        }
    }

    // 1.9b (27-11-2026): la seccion de LA OLA Y LA MAREA (PivoteOla, FotoOla, Ola, OlaFotos, EscalaOla, RangosRth) que se habia agregado
    // para la 2.0a se MUDO, sin cambios, a La Ola 1.0 (atas/PythiaOla/OlaMotor.cs): el Superindicador vuelve a velas + delta + perfil.
}
