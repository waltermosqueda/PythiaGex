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

    // =========================================================================================================================
    // 27-11-2026 LA OLA Y LA MAREA, sin nada de ATAS. El zigzag en vivo de la ronda 17 (laboratorio/ronda17_orderflow), orden
    // agresora por orden agresora. La OLA es el zigzag de X (EscalaOla: 0,08 x mediana del rango RTH de las 10 ruedas previas); la
    // MAREA es el mismo motor con 4X. Prueba: herramientas/test_ola.py, contra la referencia en Python sobre cintas reales.
    // =========================================================================================================================

    /// <summary>
    /// Un giro del zigzag. En <see cref="Ola.Pivotes"/> van solo los CONFIRMADOS, en orden; el extremo de la pierna en curso (que
    /// todavia se puede estirar) viaja en la foto, <see cref="FotoOla.Extremo"/>, con Confirmado = false.
    /// </summary>
    public struct PivoteOla
    {
        public DateTime T;          // hora de la orden que marco el extremo: la PRIMERA que toco ese precio (las que lo repiten no lo mueven)
        public decimal Precio;
        public int Tipo;            // +1 maximo, -1 minimo
        public bool Confirmado;     // el precio ya volvio X desde aca
        public DateTime TConf;      // hora de la orden que lo confirmo (DateTime.MinValue si no esta confirmado)
        public long NConf;          // ordenes que llevaba el motor al confirmarlo, contando la que confirmo (0 si no esta confirmado)
        public decimal Cum;         // delta acumulado del motor en el extremo, contando la orden del extremo
    }

    /// <summary>
    /// La ola en un instante: todo lo que se dibuja sale de aca. Con Dir = 0 (desde el arranque hasta que el precio recorre X por
    /// primera vez) todavia no hay pierna: Origen y Extremo vienen vacios y los derivados dan 0.
    /// </summary>
    public struct FotoOla
    {
        public decimal X;
        public long N;              // ordenes que entraron hasta la foto
        public DateTime T;          // hora de la ultima orden que entro (MinValue: ninguna)
        public decimal Ultimo;      // ultimo precio: el 'ultimo' de esa orden (en una barrida, donde termino)
        public decimal Cum;         // delta acumulado desde el arranque del motor (compras a mercado menos ventas a mercado)
        public int Dir;             // +1 la pierna en curso sube, -1 baja, 0 todavia no hay pierna
        public int Pivotes;         // giros confirmados hasta la foto: los primeros 'Pivotes' de Ola.Pivotes
        public PivoteOla Origen;    // el ultimo giro confirmado: de donde arranca la pierna en curso
        public PivoteOla Extremo;   // la punta de la pierna en curso (Confirmado = false; Tipo = Dir)

        public bool HayPierna => Dir != 0;

        /// <summary>Delta de la pierna: el acumulado de ahora menos el del origen.</summary>
        public decimal DeltaPierna => Dir != 0 ? Cum - Origen.Cum : 0m;

        /// <summary>CON agresores: el delta de la pierna tiene el signo de la direccion. Si no (o si da 0), la pierna va SIN agresores
        /// ("pasiva": el precio se mueve contra la agresion neta).</summary>
        public bool ConAgresores => Dir != 0 && Math.Sign(Cum - Origen.Cum) == Dir;

        /// <summary>Cuanto volvio el precio desde el extremo: |extremo - ultimo precio|.</summary>
        public decimal Retroceso => Dir != 0 ? Math.Abs(Extremo.Precio - Ultimo) : 0m;

        /// <summary>Cuanto le falta al precio para llegar a la raya y darse vuelta: max(0, X - retroceso). Da 0 si la misma orden que
        /// hizo el extremo ya barrio hasta el otro lado de la raya: el giro lo confirma la orden siguiente que no haga extremo nuevo.</summary>
        public decimal Faltan => Dir != 0 ? Math.Max(0m, X - Retroceso) : 0m;

        /// <summary>La raya de giro: extremo - X si la pierna sube, extremo + X si baja. Una orden que la toca confirma el giro.</summary>
        public decimal Raya => Dir != 0 ? Extremo.Precio - Dir * X : 0m;

        /// <summary>Lo que lleva recorrido la pierna: |extremo - origen|.</summary>
        public decimal Recorrido => Dir != 0 ? Math.Abs(Extremo.Precio - Origen.Precio) : 0m;
    }

    /// <summary>
    /// EL MOTOR DE LA OLA: copia AL PIE DE LA LETRA de la clase Maquina de la referencia en Python (ola_demo_datos.py, verificada
    /// contra la ronda 17). Por cada orden de la cinta:
    ///   1. mn = min(primero, ultimo), mx = max(primero, ultimo): una orden que barre pasa por todos los precios del medio.
    ///   2. cum += vol * lado ANTES del paso: el cum de un extremo incluye la orden que lo marco.
    ///   3. Sin direccion (Dir = 0, al arrancar) sigue el minimo y el maximo con su hora y su cum. Cuando maximo - minimo &gt;= X, el
    ///      primer pivote es el que vino PRIMERO por hora (con la misma hora, el maximo) y la direccion va hacia el otro, que pasa a
    ///      ser el extremo de la pierna.
    ///   4. Subiendo: si mx &gt; extremo, el extremo sube (con la hora y el cum de esta orden); si no, y extremo - mn &gt;= X, se confirma
    ///      un MAXIMO en el extremo (hora de confirmacion = esta orden) y arranca una pierna que baja con extremo = mn, la hora de esta
    ///      orden y el cum de ahora. Bajando, el espejo.
    /// Comparaciones exactas (&gt;=, &gt;, &lt;) en decimal, sin tolerancias: los precios son multiplos del tick.
    /// EN VIVO, la orden acumulada de ATAS crece (OnUpdateCumulativeTrade): <see cref="CorregirUltima"/> deshace la ultima orden y la
    /// vuelve a pasar con los valores nuevos, asi el estado queda IGUAL que si hubiera entrado una sola vez ya completa, como en la
    /// cinta. No es seguro entre hilos: el indicador lo usa adentro de su llave.
    /// </summary>
    public sealed class Ola
    {
        private struct Estado
        {
            public int Dir;
            public bool Arranco;                    // ya entro alguna orden (en la referencia: lo y hi distintos de None)
            public long N;
            public DateTime T, ExtT, LoT, HiT;
            public decimal Cum, Ultimo, Ext, ExtCum, Lo, LoCum, Hi, HiCum;
        }

        public readonly decimal X;
        private Estado _e, _antes;                  // el estado y el de justo antes de la ultima orden (para corregirla)
        private int _pivAntes = -1;                 // pivotes que habia antes de la ultima orden; -1: no hay orden para corregir
        private readonly List<PivoteOla> _piv = new List<PivoteOla>();

        public Ola(decimal x)
        {
            if (x <= 0m) throw new ArgumentOutOfRangeException(nameof(x), "X tiene que ser mayor que cero");
            X = x;
        }

        /// <summary>Los giros confirmados, en orden. Solo crece (salvo que CorregirUltima deshaga uno que confirmo la ultima orden).</summary>
        public IReadOnlyList<PivoteOla> Pivotes => _piv;
        public int Dir => _e.Dir;
        public long Ordenes => _e.N;
        public decimal Cum => _e.Cum;
        public DateTime UltimaHora => _e.T;
        /// <summary>Hora de la orden anterior a la ultima (MinValue si hubo una sola).</summary>
        public DateTime HoraAnterior => _antes.T;
        public bool PuedeCorregir => _pivAntes >= 0;

        public void Reiniciar()
        {
            _e = default; _antes = default; _pivAntes = -1; _piv.Clear();
        }

        /// <summary>Una orden nueva (en vivo: la primera version de una orden acumulada, que despues puede crecer).</summary>
        public void Orden(DateTime t, decimal primero, decimal ultimo, decimal vol, int lado)
        {
            _antes = _e; _pivAntes = _piv.Count;
            Paso(t, primero, ultimo, vol, lado);
        }

        /// <summary>La ULTIMA orden crecio (mas volumen o una barrida mas larga): vuelve al estado de antes de esa orden, saca el pivote
        /// que hubiera confirmado y la pasa de nuevo con los valores nuevos. false si no hay orden para corregir.</summary>
        public bool CorregirUltima(DateTime t, decimal primero, decimal ultimo, decimal vol, int lado)
        {
            if (_pivAntes < 0) return false;
            _e = _antes;
            if (_piv.Count > _pivAntes) _piv.RemoveRange(_pivAntes, _piv.Count - _pivAntes);
            Paso(t, primero, ultimo, vol, lado);
            return true;
        }

        /// <summary>El estado de ahora.</summary>
        public FotoOla Foto() => Armar(in _e, _piv.Count);

        /// <summary>El estado de justo ANTES de la ultima orden (para un corte que llego una orden tarde).</summary>
        public FotoOla FotoAntesDeLaUltima() => _pivAntes >= 0 ? Armar(in _antes, _pivAntes) : Foto();

        private FotoOla Armar(in Estado e, int npiv)
        {
            var f = new FotoOla { X = X, N = e.N, T = e.T, Ultimo = e.Ultimo, Cum = e.Cum, Dir = e.Dir, Pivotes = npiv };
            if (e.Dir != 0)
            {
                f.Origen = _piv[npiv - 1];
                f.Extremo = new PivoteOla { T = e.ExtT, Precio = e.Ext, Tipo = e.Dir, Cum = e.ExtCum };
            }
            return f;
        }

        private void Paso(DateTime t, decimal primero, decimal ultimo, decimal vol, int lado)
        {
            decimal mn, mx;
            if (ultimo < primero) { mn = ultimo; mx = primero; } else { mn = primero; mx = ultimo; }
            switch (lado)                           // cum += vol * lado, sin multiplicar en el caso comun
            {
                case 1: _e.Cum += vol; break;
                case -1: _e.Cum -= vol; break;
                case 0: break;
                default: _e.Cum += vol * lado; break;
            }
            _e.N++; _e.T = t; _e.Ultimo = ultimo;
            decimal cum = _e.Cum;
            if (_e.Dir == 0)
            {
                if (!_e.Arranco || mn < _e.Lo) { _e.Lo = mn; _e.LoT = t; _e.LoCum = cum; }
                if (!_e.Arranco || mx > _e.Hi) { _e.Hi = mx; _e.HiT = t; _e.HiCum = cum; }
                _e.Arranco = true;
                if (_e.Hi - _e.Lo >= X)
                {
                    if (_e.HiT > _e.LoT)
                    {
                        Confirmar(_e.LoT, _e.Lo, -1, t, _e.LoCum);
                        _e.Dir = 1; _e.Ext = _e.Hi; _e.ExtT = _e.HiT; _e.ExtCum = _e.HiCum;
                    }
                    else
                    {
                        Confirmar(_e.HiT, _e.Hi, 1, t, _e.HiCum);
                        _e.Dir = -1; _e.Ext = _e.Lo; _e.ExtT = _e.LoT; _e.ExtCum = _e.LoCum;
                    }
                }
                return;
            }
            if (_e.Dir == 1)
            {
                if (mx > _e.Ext) { _e.Ext = mx; _e.ExtT = t; _e.ExtCum = cum; }
                else if (_e.Ext - mn >= X)
                {
                    Confirmar(_e.ExtT, _e.Ext, 1, t, _e.ExtCum);
                    _e.Dir = -1; _e.Ext = mn; _e.ExtT = t; _e.ExtCum = cum;
                }
            }
            else
            {
                if (mn < _e.Ext) { _e.Ext = mn; _e.ExtT = t; _e.ExtCum = cum; }
                else if (mx - _e.Ext >= X)
                {
                    Confirmar(_e.ExtT, _e.Ext, -1, t, _e.ExtCum);
                    _e.Dir = 1; _e.Ext = mx; _e.ExtT = t; _e.ExtCum = cum;
                }
            }
        }

        private void Confirmar(DateTime tExt, decimal precio, int tipo, DateTime tConf, decimal cumExt)
            => _piv.Add(new PivoteOla { T = tExt, Precio = precio, Tipo = tipo, Confirmado = true, TConf = tConf, NConf = _e.N, Cum = cumExt });
    }

    /// <summary>
    /// La ola con FOTOS AL CIERRE DE CADA VELA, para dibujar "al momento" de cualquier vela (como el perfil de la 1.7). Cada corte es
    /// una hora, el cierre de una vela (la apertura de la siguiente; en velas de tiempo, apertura + periodo), y su foto es el estado con
    /// TODAS las ordenes de t &lt; corte y NINGUNA de t &gt;= corte: se toma justo antes de pasar la primera orden de t &gt;= corte.
    /// La foto k es la de la vela k contada desde la primera vela que tiene corte (el indicador lleva ese corrimiento). Para dibujar la
    /// vela k: los primeros Foto(k).Pivotes de Ola.Pivotes y, de ahi, la pierna en curso hasta Foto(k).Extremo.
    /// Costo: una comparacion por orden y ~220 bytes por foto (1.380 velas de 1 min por jornada: unos 300 kB).
    /// </summary>
    public sealed class OlaFotos
    {
        public readonly Ola Ola;
        private readonly List<DateTime> _cortes = new List<DateTime>();
        private readonly List<FotoOla> _fotos = new List<FotoOla>();

        public OlaFotos(decimal x) { Ola = new Ola(x); }

        public int Cortes => _cortes.Count;
        public int Tomadas => _fotos.Count;
        /// <summary>Cortes que llegaron tarde por mas de una orden (su foto incluye ordenes de despues del corte). Tiene que dar 0.</summary>
        public int CortesTarde { get; private set; }
        public DateTime Corte(int k) => _cortes[k];

        public void Reiniciar() { Ola.Reiniciar(); _cortes.Clear(); _fotos.Clear(); CortesTarde = 0; }

        /// <summary>
        /// Agrega el corte siguiente; tiene que ser posterior al anterior (si no, no lo agrega y devuelve false). Si ya entro una orden
        /// de t &gt;= corte (el corte llego TARDE: en vivo, la orden que abre la vela nueva entro antes de que el grafico la avise) y fue
        /// solo la ultima, la foto se arma con el estado de antes de esa orden y es exacta. Si fueron mas, se toma con el estado actual,
        /// se cuenta en CortesTarde y devuelve false. En velas de tiempo no pasa si el corte se agrega cuando ABRE la vela.
        /// </summary>
        public bool AgregarCorte(DateTime corte)
        {
            if (_cortes.Count > 0 && corte <= _cortes[_cortes.Count - 1]) return false;
            _cortes.Add(corte);
            if (Ola.Ordenes == 0 || Ola.UltimaHora < corte) return true;     // lo normal: la foto sale al pasar la primera orden de t >= corte
            bool exacta = Ola.PuedeCorregir && Ola.HoraAnterior < corte;
            while (_fotos.Count < _cortes.Count) _fotos.Add(exacta ? Ola.FotoAntesDeLaUltima() : Ola.Foto());
            if (!exacta) CortesTarde++;
            return exacta;
        }

        public void Orden(DateTime t, decimal primero, decimal ultimo, decimal vol, int lado)
        {
            while (_fotos.Count < _cortes.Count && t >= _cortes[_fotos.Count]) _fotos.Add(Ola.Foto());
            Ola.Orden(t, primero, ultimo, vol, lado);
        }

        /// <summary>La ultima orden crecio (OnUpdateCumulativeTrade). Corrige el motor y rehace las fotos que se habian tomado DESPUES
        /// de esa orden (con TomarHasta), que la incluian con los valores viejos.</summary>
        public bool CorregirUltima(DateTime t, decimal primero, decimal ultimo, decimal vol, int lado)
        {
            if (!Ola.PuedeCorregir) return false;
            while (_fotos.Count < _cortes.Count && t >= _cortes[_fotos.Count]) _fotos.Add(Ola.FotoAntesDeLaUltima());   // si cambiara su hora
            Ola.CorregirUltima(t, primero, ultimo, vol, lado);
            long n = Ola.Ordenes;
            for (int k = _fotos.Count - 1; k >= 0 && _fotos[k].N == n; k--) _fotos[k] = Ola.Foto();
            return true;
        }

        /// <summary>Toma las fotos de los cortes &lt;= hasta que esperaban una orden posterior: fin de la cinta, o mercado quieto con la
        /// hora de las ORDENES ya pasada (no la de la PC). Despues de esto no puede entrar una orden de t &lt; hasta.</summary>
        public void TomarHasta(DateTime hasta)
        {
            while (_fotos.Count < _cortes.Count && _cortes[_fotos.Count] <= hasta) _fotos.Add(Ola.Foto());
        }

        public bool Tomada(int k) => k >= 0 && k < _fotos.Count;

        /// <summary>La foto del corte k; si todavia no se tomo (la vela en curso), la de ahora; k &lt; 0, vacia.</summary>
        public FotoOla Foto(int k) => k < 0 ? new FotoOla { X = Ola.X } : k < _fotos.Count ? _fotos[k] : Ola.Foto();

        public FotoOla Actual => Ola.Foto();
    }

    /// <summary>
    /// LA ESCALA DE LA OLA, como la ronda 17 (r17_01_anatomia.escala_X): X = max(4, redondeo al tick de 0,08 x la mediana del RANGO
    /// RTH de las 10 ruedas previas). El redondeo es el round de Python (al par en el medio); con tick 0,25 y rangos multiplos del
    /// tick nunca cae en el medio. Ruedas previas = las ruedas con datos anteriores a la del dia (un feriado no cuenta como rueda).
    /// La mediana es la de pandas: con cantidad par, el promedio de los dos del medio. La marea usa 4X.
    /// </summary>
    public static class EscalaOla
    {
        public const decimal Factor = 0.08m, Piso = 4m;
        public const int Ruedas = 10;

        public static decimal Mediana(IList<decimal> valores)
        {
            int n = valores?.Count ?? 0;
            if (n == 0) return 0m;
            var v = new decimal[n];
            valores.CopyTo(v, 0);
            Array.Sort(v);
            return n % 2 == 1 ? v[n / 2] : (v[n / 2 - 1] + v[n / 2]) / 2m;
        }

        /// <summary>X con los rangos RTH de las ruedas ANTERIORES, en orden de fecha (se usan las ultimas 10; con menos, las que haya).
        /// 0 si no hay ninguna.</summary>
        public static decimal X(IList<decimal> rangosPrevios, decimal tick, out decimal mediana)
        {
            mediana = 0m;
            int n = rangosPrevios?.Count ?? 0;
            if (n == 0 || tick <= 0m) return 0m;
            var ult = new List<decimal>(Ruedas);
            for (int i = Math.Max(0, n - Ruedas); i < n; i++) ult.Add(rangosPrevios[i]);
            mediana = Mediana(ult);
            decimal x = Math.Round(Factor * mediana / tick, 0, MidpointRounding.ToEven) * tick;
            return x > Piso ? x : Piso;
        }
    }

    /// <summary>
    /// Rango RTH de cada rueda: maximo y minimo de lo operado con hora NY entre las 9:30 y las 16:00 de ese dia (la ronda 17 y la 16:
    /// primero y ultimo de cada orden de la cinta). Recibe ordenes (primero, ultimo) o velas (alto, bajo) con la hora NY; una vela va
    /// entera a la parte de su apertura, como en Jornadas (con velas de 1 o 5 min da lo mismo que la cinta).
    /// </summary>
    public sealed class RangosRth
    {
        public sealed class Rueda
        {
            public int Clave;                           // aaaammdd
            public decimal Alto, Bajo;
            public long Ordenes;
            public decimal Rango => Alto - Bajo;
        }

        private readonly SortedList<int, Rueda> _r = new SortedList<int, Rueda>();
        private Rueda _ult;

        public IList<Rueda> Lista => _r.Values;

        public void Agregar(DateTime ny, decimal p1, decimal p2)
        {
            if (Jornadas.Tipo(ny) != Jornadas.TipoRth) return;
            int clave = ny.Year * 10000 + ny.Month * 100 + ny.Day;
            var r = _ult;
            if (r == null || r.Clave != clave)
            {
                if (!_r.TryGetValue(clave, out r)) { r = new Rueda { Clave = clave, Alto = decimal.MinValue, Bajo = decimal.MaxValue }; _r.Add(clave, r); }
                _ult = r;
            }
            decimal mn = p1 < p2 ? p1 : p2, mx = p1 < p2 ? p2 : p1;
            if (mx > r.Alto) r.Alto = mx;
            if (mn < r.Bajo) r.Bajo = mn;
            r.Ordenes++;
        }

        /// <summary>X de la rueda 'clave' con los rangos de las ruedas ANTERIORES que haya en la lista (las ultimas 10).</summary>
        public decimal XDe(int clave, decimal tick, out decimal mediana, out int usadas)
        {
            var previos = new List<decimal>();
            foreach (var kv in _r) if (kv.Key < clave) previos.Add(kv.Value.Rango);
            usadas = Math.Min(EscalaOla.Ruedas, previos.Count);
            return EscalaOla.X(previos, tick, out mediana);
        }
    }
}
