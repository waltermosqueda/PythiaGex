using System;
using System.Collections.Generic;

namespace DeltaVivo
{
    // =====================================================================================
    // EL NUCLEO DE DELTA VIVO. Codigo PURO: no conoce ATAS, no dibuja, no escribe archivos.
    //
    // Come la cinta orden por orden -las mismas 14 columnas que graba FlujoClaroSonda.cs- y
    // escupe EVENTOS {hora, precio, tamano, lado, tipo}. Nada mas.
    //
    // POR QUE ESTE ARCHIVO ES UNA COPIA Y NO UN ENLACE A AbsorcionNucleo.cs (decidido, no por
    // comodidad):
    //   1. AbsorcionViva YA ESTA INSTALADO Y CORRIENDO en la pantalla del operador. Un archivo
    //      enlazado invita a "arreglar" ahi adentro, y eso esta prohibido en esta ronda.
    //   2. Enlazado, DeltaVivo.dll exportaria los tipos con el nombre completo
    //      AbsorcionViva.Evento / AbsorcionViva.Franja, iguales a los de la DLL ya instalada.
    //      Dos ensamblados con los mismos nombres completos cargados en el mismo proceso de ATAS
    //      es legal en .NET, pero nadie lo probo contra el escaner de indicadores.
    //   EL PRECIO DE LA COPIA, dicho de frente: Reloj y Franja quedan en DOS archivos. Un arreglo
    //   del horario de verano hay que hacerlo en los dos, y si se arregla uno solo el indicador y
    //   su arnes dejan de coincidir sin que nadie se entere. Por eso el arnes vuelve a correr la
    //   prueba de la franja contra America/New_York, aparte, en vez de darla por heredada.
    //
    // NADA DE ESTO PREDICE NADA. Las rondas 1 a 10 llevan unos 13.000 ensayos sin una regla que le
    // gane a los 0,948 puntos que cuesta la vuelta, y en esta ronda la familia del empujon dio 0
    // de 36 celdas con neto positivo al tacto (mejor |t| 1,85 contra un techo del azar de 3,93).
    // Esto DESCRIBE lo que esta pasando en la cinta. No anticipa.
    // =====================================================================================

    /// <summary>Una agresion ya cerrada, con la mejor punta ANTES y DESPUES. Son las 14 columnas
    /// de la cinta de la sonda y, en vivo, los campos de un CumulativeTrade de ATAS.
    /// COPIA EXACTA de AbsorcionViva.Orden: si se le cambia un campo, la cinta grabada deja de
    /// encajar y se pierde la paridad.</summary>
    public struct Orden
    {
        /// <summary>Nanosegundos desde 1970-01-01 UTC.</summary>
        public long Ns;
        /// <summary>Precio del primer print del grupo (FirstPrice).</summary>
        public double Primero;
        /// <summary>Precio del ultimo print del grupo (Lastprice).</summary>
        public double Ultimo;
        /// <summary>Contratos del grupo (Volume).</summary>
        public double Vol;
        /// <summary>+1 agresor comprador, -1 agresor vendedor (Direction).</summary>
        public int Lado;
        /// <summary>Cuantas operaciones agrupo (Ticks.Count). Solo informativo.</summary>
        public int Prints;
        /// <summary>Mejor bid ANTES (PreviousBid): precio y tamano. NaN si no vino.</summary>
        public double ABid, ABidV;
        /// <summary>Mejor ask ANTES (PreviousAsk).</summary>
        public double AAsk, AAskV;
        /// <summary>Mejor bid DESPUES (NewBid).</summary>
        public double DBid, DBidV;
        /// <summary>Mejor ask DESPUES (NewAsk).</summary>
        public double DAsk, DAskV;
    }

    /// <summary>Los tipos de marca del delta. Los numeros 0, 1 y 2 son de Absorcion Viva y no se
    /// reusan: asi un CSV de los dos indicadores nunca se confunde.</summary>
    public static class Tipos
    {
        /// <summary>LA OLA: delta neto de la ventana corta. Es la unica que se DIBUJA de fabrica.</summary>
        public const int Ola = 10;
        /// <summary>CONTRAPIE: la ola y el precio yendo para el otro lado. No se dibuja.</summary>
        public const int Contrapie = 11;
        /// <summary>TRABA: mucho delta neto y el precio quieto (RANGO chico, no neto chico).</summary>
        public const int Traba = 12;
        /// <summary>NIDO: todo el delta de la ventana concentrado en un solo precio.</summary>
        public const int Nido = 13;

        public static string Nombre(int t)
        {
            switch (t)
            {
                case Ola: return "ola";
                case Contrapie: return "contrapie";
                case Traba: return "traba";
                case Nido: return "nido";
            }
            return "tipo_" + t;
        }
    }

    /// <summary>
    /// Lo unico que la capa de dibujo sabe de un nucleo. Un evento que sigue VIVO se actualiza
    /// solo (el mismo objeto): la marca crece en pantalla sin que nadie la vuelva a crear.
    ///
    /// TRAMPA SEMANTICA, y va escrita aca porque en seis meses nadie se acuerda: el campo Lado
    /// NO significa lo mismo que en Absorcion Viva. Alla Lado=+1 es "se comieron COMPRAS
    /// agresoras", o sea LA COMPRA PERDIO. Aca Lado=+1 es "delta comprador neto", o sea LA COMPRA
    /// GANO. Es el mismo campo con la lectura invertida, y por eso este indicador no usa el mismo
    /// par de colores.
    /// </summary>
    public sealed class Evento
    {
        /// <summary>Que clase de marca es. Ver <see cref="Tipos"/>.</summary>
        public int Tipo;
        /// <summary>Instante en que se ENCENDIO, en nanosegundos UTC. Es la x de la marca.</summary>
        public long Ns;
        /// <summary>Instante de la primera fila que estaba adentro de la ventana al encender.</summary>
        public long NsInicio;
        /// <summary>Instante en que se rearmo (0 mientras sigue vivo).</summary>
        public long NsCierre;
        /// <summary>El precio de la marca, en TICKS enteros. Es la y, exacta.</summary>
        public long PrecioTk;
        /// <summary>El mismo precio en puntos.</summary>
        public double Precio;
        /// <summary>+1 empujaron las COMPRAS (delta comprador neto) = AZUL.
        /// -1 empujaron las VENTAS = NARANJA.</summary>
        public int Lado;
        /// <summary>Contratos NETOS. Es el diametro de la marca. SIGUE CRECIENDO mientras el
        /// empujon no afloje: por eso el objeto no se copia.</summary>
        public double Tamano;
        /// <summary>Contratos netos que tenia cuando se encendio.</summary>
        public double TamanoAlEncender;
        /// <summary>Contratos TOTALES operados en la ventana al encender (el caudal).</summary>
        public double Volumen;
        /// <summary>Para la ola: |D| / V, cuan de un solo lado fue. Para el nido: la
        /// concentracion |dmax| / A.</summary>
        public double Razon;
        /// <summary>Lo que se movio el medio del libro en la ventana, en ticks (con signo).</summary>
        public double Avance;
        /// <summary>Rango (maximo menos minimo) del precio operado en la ventana, en ticks.</summary>
        public double Rango;
        /// <summary>El umbral que estaba vigente (cambia de dia a noche).</summary>
        public double Vara;
        /// <summary>Cuantas filas de cinta habia adentro de la ventana al encender.</summary>
        public int Filas;
        /// <summary>Solo la ola: la razon supero el corte, o sea "no habia nadie enfrente".
        /// Se dibuja HUECA. No es una marca aparte: es la misma ola con otro relleno.</summary>
        public bool Pura;
        /// <summary>Encendio adentro de la rueda de Nueva York.</summary>
        public bool EnRueda;
        /// <summary>Numero corrido del evento adentro de su familia.</summary>
        public long IdEpisodio;
        /// <summary>false mientras el empujon sigue vivo (no se rearmo todavia).</summary>
        public bool Cerrado;
        /// <summary>Sube cada vez que el evento cambia. La capa de dibujo la usa para saber si
        /// tiene que repintar sin comparar campo por campo.</summary>
        public int Revision;
    }

    /// <summary>Un detector. La capa de dibujo no sabe nada mas que esto.</summary>
    public interface IFuenteDeEventos
    {
        /// <summary>Nombre corto, para el registro y el rotulo.</summary>
        string Nombre { get; }

        /// <summary>Alimenta una agresion YA CERRADA. Los eventos nuevos quedan en
        /// <see cref="Nuevos"/>, que se pisa en cada llamada.</summary>
        void Agregar(in Orden o);

        /// <summary>Los eventos que nacieron en la ultima llamada a <see cref="Agregar"/>.</summary>
        List<Evento> Nuevos { get; }

        /// <summary>Se llama al cambiar de instrumento o de sesion.</summary>
        void Reiniciar();
    }

    /// <summary>Varios detectores como si fueran uno.</summary>
    public sealed class Bateria : IFuenteDeEventos
    {
        private readonly List<IFuenteDeEventos> _fuentes = new List<IFuenteDeEventos>();
        private readonly List<Evento> _nuevos = new List<Evento>();

        public string Nombre { get { return "bateria"; } }
        public List<Evento> Nuevos { get { return _nuevos; } }
        public IReadOnlyList<IFuenteDeEventos> Fuentes { get { return _fuentes; } }

        public void Agregar(IFuenteDeEventos f) { if (f != null) _fuentes.Add(f); }

        public void Agregar(in Orden o)
        {
            _nuevos.Clear();
            for (int i = 0; i < _fuentes.Count; i++)
            {
                var f = _fuentes[i];
                f.Agregar(in o);
                var n = f.Nuevos;
                for (int k = 0; k < n.Count; k++) _nuevos.Add(n[k]);
            }
        }

        public void Reiniciar()
        {
            _nuevos.Clear();
            for (int i = 0; i < _fuentes.Count; i++) _fuentes[i].Reiniciar();
        }
    }

    // =====================================================================================
    // EL NUCLEO DEL DELTA: una sola ventana movil, cuatro detectores colgados de ella.
    // =====================================================================================
    /// <summary>
    /// UNA VENTANA MOVIL DE T SEGUNDOS sobre la cinta, y cuatro familias colgadas de ella. Todas
    /// comparten el mismo barrido -por eso esto cuesta un solo recorrido por fila- pero cada una
    /// lleva su propio refractario, asi que una no apaga a la otra.
    ///
    /// LO QUE SE CALCULA EN CADA FILA, siempre con datos de t &lt;= t de esa fila:
    ///   D      delta neto de la ventana (t-T, t]  = suma de lado*volumen
    ///   V      contratos totales de la ventana
    ///   mid    medio del libro DESPUES de la fila (NewBid/NewAsk), arrastrado hacia adelante
    ///          cuando la punta no es sana; antes de la primera punta sana se usa el precio operado
    ///   av     (mid de esta fila - mid de la ultima fila ANTERIOR a la ventana) / tick
    ///   rango  maximo menos minimo del precio OPERADO adentro de la ventana, en ticks
    ///   dmax   el delta neto del precio que se llevo mas delta (en valor absoluto) de la ventana
    ///   conc   |dmax| / (suma de |delta| de cada precio) = la concentracion, entre 0 y 1
    ///
    /// LAS CUATRO FAMILIAS:
    ///   OLA        |D| &gt;= vara. Es la unica que se dibuja. Marca en el ULTIMO print de la fila
    ///              que completo la cuenta. Si ademas |D|/V &gt;= RazonPura, se marca Pura (hueca).
    ///   CONTRAPIE  lo mismo pero con el precio yendo para el OTRO lado (signo de av contrario y
    ///              por lo menos N ticks). MEDIDO: 8 en 23 ruedas. No se dibuja.
    ///   TRABA      |D| &gt;= vara y el RANGO de la ventana chico. MEDIDO: no existe (0 en 23 ruedas
    ///              con 800/400 y rango &lt;= 8). Se deja calculando para que el CSV lo pruebe.
    ///   NIDO       |dmax| &gt;= vara y conc &gt;= corte. No se dibuja porque NO SE SABE EN QUE PRECIO
    ///              ponerlo: cambiando la convencion de a que precio se le carga una agresion que
    ///              camino el libro, el precio dominante se corre en el 95,9 % de los casos
    ///              (12 ticks de mediana).
    ///
    /// EL REFRACTARIO es el mismo para las cuatro: al encender queda desarmado y se rearma cuando
    /// su medida baja de la mitad de la vara. Mientras esta desarmado el evento SIGUE VIVO y su
    /// Tamano es el MAXIMO alcanzado hasta ahora: por eso la marca crece en pantalla sin mirar el
    /// futuro ni una sola vez.
    ///
    /// Es la misma definicion, paso por paso, que el Python con el que se midio todo
    /// (r11_f3_lib.preparar/_ventanas/_refractario, r11_f6_lib._barrer y
    /// r11_dib_04_anclaje.rango_movil). El arnes de paridad compara las dos implementaciones
    /// evento por evento.
    /// </summary>
    public sealed class NucleoDelta : IFuenteDeEventos
    {
        // ---------------------------------------------------------------- ajustes
        /// <summary>Tamano del tick del instrumento. 0,25 en MNQ y en MES.</summary>
        public double Tick = 0.25;
        /// <summary>Largo de la ventana movil, en segundos.</summary>
        public double VentanaSeg = 10.0;
        /// <summary>Contratos netos para encender la ola adentro de la rueda de Nueva York.</summary>
        public double OlaVaraRueda = 1500;
        /// <summary>Contratos netos para encender la ola fuera de la rueda.</summary>
        public double OlaVaraFuera = 800;
        /// <summary>false: se usa siempre la vara de la rueda.</summary>
        public bool PartirPorFranja = true;
        /// <summary>|D| / V a partir del cual la ola se considera PURA (se dibuja hueca).</summary>
        public double RazonPura = 0.70;
        /// <summary>Ticks que el precio tiene que haber ido EN CONTRA para el contrapie.</summary>
        public double ContrapieTicks = 2;
        public double TrabaVaraRueda = 800;
        public double TrabaVaraFuera = 400;
        /// <summary>Rango maximo de la ventana, en ticks, para la traba.</summary>
        public double TrabaRangoTicks = 8;
        public double NidoVaraRueda = 400;
        public double NidoVaraFuera = 250;
        /// <summary>Concentracion minima del nido.</summary>
        public double NidoConcMin = 0.50;
        /// <summary>Rango maximo de la ventana para el nido. 0 o menos = sin filtro.</summary>
        public double NidoRangoTicks = 0;
        /// <summary>true: el detector se rearma en la fila siguiente en vez de esperar a que su
        /// medida baje de la mitad de la vara. NO es con lo que se midio: con el refractario
        /// apagado el mismo empujon enciende una y otra vez y las marcas se multiplican. Existe
        /// para que el operador pueda VER la diferencia, y para el arnes.</summary>
        public bool RearmeInmediato = false;
        /// <summary>Las cuatro familias calculan SIEMPRE. Estas banderas existen para el arnes,
        /// no para la pantalla: lo que apaga el operador es el DIBUJO, nunca el calculo, porque
        /// el CSV tiene que guardar todo para que la prueba hacia adelante no dependa de lo que
        /// tenia prendido ese dia.</summary>
        public bool PrenderOla = true, PrenderContrapie = true, PrenderTraba = true, PrenderNido = true;

        public string Nombre { get { return "delta"; } }

        // ---------------------------------------------------------------- salida
        private readonly List<Evento> _nuevos = new List<Evento>();
        public List<Evento> Nuevos { get { return _nuevos; } }

        // ---------------------------------------------------------------- anillo de filas
        // Se guardan las filas desde (inicio de la ventana - 1) hasta la actual. El -1 es porque
        // el avance compara contra el mid de la ultima fila ANTERIOR a la ventana.
        private long[] _rNs = new long[1024];
        private long[] _rTk = new long[1024];
        private double[] _rDd = new double[1024];
        private double[] _rVol = new double[1024];
        private double[] _rMid = new double[1024];
        private int _mask = 1023;

        private long _n;            // cuantas filas entraron (indice absoluto de la proxima)
        private long _a;            // indice absoluto de la primera fila DE la ventana
        private double _D, _V, _A;  // delta neto, volumen y delta absoluto repartido por precio
        private double _midUlt;
        private bool _haySana;

        // ---------------------------------------------------------------- acumulador por precio
        private sealed class Celda { public double Acc; public int Cnt; }
        private readonly Dictionary<long, Celda> _cel = new Dictionary<long, Celda>();
        private readonly List<long> _act = new List<long>();
        /// <summary>Cuantos precios distintos se toleran en el diccionario antes de sacar los que
        /// ya no tienen ninguna fila adentro de la ventana. En MNQ se visitan unos 3.828 precios
        /// por sesion; sin esto el diccionario crece mientras ATAS quede abierto. Sacar una celda
        /// con Cnt=0 no cambia ni un resultado: su Acc vale exactamente 0 (son enteros).</summary>
        public int TopePrecios = 8000;

        // ---------------------------------------------------------------- colas del rango
        private long[] _qmx = new long[1024];
        private long[] _qmn = new long[1024];
        private int _hx, _tx, _hn, _tn;

        // ---------------------------------------------------------------- estado de cada familia
        private sealed class Detector
        {
            public bool Armado = true;
            public Evento Vivo;
            public long Id;
        }
        private readonly Detector _ola = new Detector();
        private readonly Detector _cpie = new Detector();
        private readonly Detector _trb = new Detector();
        private readonly Detector _nid = new Detector();

        public void Reiniciar()
        {
            _nuevos.Clear();
            _n = 0; _a = 0; _D = 0; _V = 0; _A = 0;
            _midUlt = 0; _haySana = false;
            _cel.Clear(); _act.Clear();
            _hx = _tx = _hn = _tn = 0;
            Reset(_ola); Reset(_cpie); Reset(_trb); Reset(_nid);
        }

        private static void Reset(Detector d) { d.Armado = true; d.Vivo = null; d.Id = 0; }

        // =================================================================================
        // UNA FILA DE CINTA
        // =================================================================================
        public void Agregar(in Orden o)
        {
            _nuevos.Clear();
            // NO se filtra ninguna fila, ni siquiera una con lado 0 o volumen 0. El Python con el
            // que se compara tampoco filtra, y una fila descartada correria la ventana y el orden
            // de la lista de precios activos: la paridad dejaria de significar nada.
            double tick = Tick > 0 ? Tick : 0.25;

            // ---- 1) el medio del libro DESPUES de esta fila
            double mid;
            bool sana = o.DBid > 0 && o.DAsk > o.DBid && (o.DAsk - o.DBid) < 20
                        && !double.IsNaN(o.DBid) && !double.IsInfinity(o.DBid)
                        && !double.IsNaN(o.DAsk) && !double.IsInfinity(o.DAsk);
            if (sana) { mid = (o.DBid + o.DAsk) / 2.0; _midUlt = mid; _haySana = true; }
            else mid = _haySana ? _midUlt : o.Ultimo;

            // ---- 2) la fila entra al anillo
            long i = _n;
            Capacidad(i);
            int s = (int)(i & _mask);
            long tk = (long)Math.Round(o.Ultimo / tick, MidpointRounding.ToEven);
            double dd = o.Lado * o.Vol;
            _rNs[s] = o.Ns; _rTk[s] = tk; _rDd[s] = dd; _rVol[s] = o.Vol; _rMid[s] = mid;
            _n++;

            // ---- 3) suma a los acumuladores (primero entra, despues se echa a los viejos: es el
            //         mismo orden del Python, y con el mismo orden el residuo de coma flotante es
            //         el mismo hasta el ultimo bit)
            Celda c;
            if (!_cel.TryGetValue(tk, out c)) { c = new Celda(); _cel[tk] = c; }
            if (c.Cnt == 0) _act.Add(tk);
            c.Cnt++;
            double viejo = c.Acc;
            c.Acc = viejo + dd;
            _A += Math.Abs(c.Acc) - Math.Abs(viejo);
            _D += dd; _V += o.Vol;

            // ---- 4) se van las filas que quedaron fuera de la ventana
            //
            // DOS CANDADOS, y ninguno cambia un solo resultado con la ventana bien puesta:
            //   - la ventana nunca vale cero. Con VentanaSeg = 0 el limite queda IGUAL al instante
            //     de esta fila, la propia fila se echa a si misma y el indice de la ventana se va
            //     mas alla de lo escrito: a partir de ahi lee posiciones viejas del anillo y el
            //     bucle no termina. La capa de ATAS ya recorta el ajuste a 1 segundo como minimo,
            //     pero el nucleo no puede depender de que quien lo llame se acuerde.
            //   - la fila que acaba de entrar NO se echa nunca (_a < i).
            double vent = VentanaSeg > 0 ? VentanaSeg : 10.0;
            long lim = o.Ns - (long)(vent * 1e9);
            while (_a < i && _rNs[(int)(_a & _mask)] <= lim)
            {
                int sa = (int)(_a & _mask);
                long kk = _rTk[sa];
                var cc = _cel[kk];
                double v0 = cc.Acc;
                cc.Acc = v0 - _rDd[sa];
                _A += Math.Abs(cc.Acc) - Math.Abs(v0);
                cc.Cnt--;
                _D -= _rDd[sa]; _V -= _rVol[sa];
                _a++;
            }

            // ---- 5) el precio que se llevo mas delta, compactando la lista de activos.
            //         Se recorre EN ORDEN DE LLEGADA y se toma el primero estrictamente mayor:
            //         con eso los empates los gana el precio que entro antes, igual que el Python.
            double mejor = -1.0;
            long bk = long.MinValue;
            bool hayBk = false;
            int w = 0;
            for (int u = 0; u < _act.Count; u++)
            {
                long kk = _act[u];
                var cc = _cel[kk];
                if (cc.Cnt == 0) continue;
                _act[w++] = kk;
                double av2 = Math.Abs(cc.Acc);
                if (av2 > mejor) { mejor = av2; bk = kk; hayBk = true; }
            }
            if (w < _act.Count) _act.RemoveRange(w, _act.Count - w);
            double dmax = hayBk ? _cel[bk].Acc : 0.0;
            long tkNido = hayBk ? bk : 0;
            if (_cel.Count > TopePrecios) Limpiar();

            // ---- 6) el rango de la ventana, con colas monotonas
            Empujar(ref _qmx, ref _hx, ref _tx, i, tk, true);
            Empujar(ref _qmn, ref _hn, ref _tn, i, tk, false);
            while (_qmx[_hx] < _a) _hx++;
            while (_qmn[_hn] < _a) _hn++;
            double rango = _rTk[(int)(_qmx[_hx] & _mask)] - _rTk[(int)(_qmn[_hn] & _mask)];

            // ---- 7) el avance del medio del libro adentro de la ventana
            long j = _a - 1;
            bool hay = j >= 0;
            double av = hay ? (mid - _rMid[(int)(j & _mask)]) / tick : 0.0;

            // ---- 8) los cuatro detectores, cada uno con su refractario
            bool rueda = Franja.EnRueda(o.Ns);
            double dabs = Math.Abs(_D);
            int sgD = _D > 0 ? 1 : (_D < 0 ? -1 : 0);
            double nabs = Math.Abs(dmax);
            double conc = _A > 0 ? nabs / _A : 0.0;
            int filas = (int)(i - _a + 1);
            long nsIni = _rNs[(int)(_a & _mask)];

            double uOla = Vara(OlaVaraRueda, OlaVaraFuera, rueda);
            double uTrb = Vara(TrabaVaraRueda, TrabaVaraFuera, rueda);
            double uNid = Vara(NidoVaraRueda, NidoVaraFuera, rueda);

            double razonOla = dabs / Math.Max(_V, 1.0);

            if (PrenderOla)
            {
                bool cond = dabs >= uOla && hay && sgD != 0;
                if (Paso(_ola, dabs, uOla, cond, o.Ns))
                    Encender(_ola, Tipos.Ola, o.Ns, nsIni, tk, tk * tick, sgD, dabs, _V,
                             razonOla, av, rango, uOla, filas, razonOla >= RazonPura, rueda);
            }
            if (PrenderContrapie)
            {
                int sgA = av > 0 ? 1 : (av < 0 ? -1 : 0);
                bool cond = dabs >= uOla && hay && sgD != 0 && sgA != sgD
                            && Math.Abs(av) >= ContrapieTicks;
                if (Paso(_cpie, dabs, uOla, cond, o.Ns))
                    Encender(_cpie, Tipos.Contrapie, o.Ns, nsIni, tk, tk * tick, sgD, dabs, _V,
                             razonOla, av, rango, uOla, filas, false, rueda);
            }
            if (PrenderTraba)
            {
                bool cond = dabs >= uTrb && hay && rango <= TrabaRangoTicks;
                if (Paso(_trb, dabs, uTrb, cond, o.Ns))
                    Encender(_trb, Tipos.Traba, o.Ns, nsIni, tk, tk * tick, sgD, dabs, _V,
                             razonOla, av, rango, uTrb, filas, false, rueda);
            }
            if (PrenderNido)
            {
                int sgN = dmax > 0 ? 1 : (dmax < 0 ? -1 : 0);
                bool cond = nabs >= uNid && conc >= NidoConcMin
                            && (NidoRangoTicks <= 0 || rango <= NidoRangoTicks);
                if (Paso(_nid, nabs, uNid, cond, o.Ns))
                    Encender(_nid, Tipos.Nido, o.Ns, nsIni, tkNido, tkNido * tick, sgN, nabs, _V,
                             conc, av, rango, uNid, filas, false, rueda);
            }
        }

        private double Vara(double rueda, double fuera, bool enRueda)
        {
            return PartirPorFranja && !enRueda ? fuera : rueda;
        }

        /// <summary>El refractario, en el mismo orden que el Python: primero se actualiza el
        /// maximo y se ve si hay que rearmar, y recien despues se mira si puede encender. Devuelve
        /// true si esta fila enciende.</summary>
        private bool Paso(Detector d, double x, double u, bool cond, long ns)
        {
            if (!d.Armado)
            {
                if (d.Vivo != null)
                {
                    if (x > d.Vivo.Tamano) { d.Vivo.Tamano = x; d.Vivo.Revision++; }
                }
                if (RearmeInmediato || x < u * 0.5)
                {
                    d.Armado = true;
                    if (d.Vivo != null)
                    {
                        d.Vivo.Cerrado = true;
                        d.Vivo.NsCierre = ns;
                        d.Vivo.Revision++;
                    }
                }
            }
            return d.Armado && cond;
        }

        private void Encender(Detector d, int tipo, long ns, long nsIni, long tk, double precio,
                              int lado, double tam, double vol, double razon, double av,
                              double rango, double vara, int filas, bool pura, bool rueda)
        {
            var e = new Evento
            {
                Tipo = tipo,
                Ns = ns,
                NsInicio = nsIni,
                NsCierre = 0,
                PrecioTk = tk,
                Precio = precio,
                Lado = lado,
                Tamano = tam,
                TamanoAlEncender = tam,
                Volumen = vol,
                Razon = razon,
                Avance = av,
                Rango = rango,
                Vara = vara,
                Filas = filas,
                Pura = pura,
                EnRueda = rueda,
                IdEpisodio = ++d.Id,
                Cerrado = false,
                Revision = 0
            };
            d.Vivo = e;
            d.Armado = false;
            _nuevos.Add(e);
        }

        // =================================================================================
        // Mantenimiento
        // =================================================================================
        /// <summary>Saca del diccionario los precios que ya no tienen ninguna fila adentro de la
        /// ventana. No cambia ningun resultado: con Cnt=0 el acumulador vale exactamente 0, porque
        /// se sumaron y restaron los mismos enteros.</summary>
        private void Limpiar()
        {
            var fuera = new List<long>(_cel.Count);
            foreach (var par in _cel) if (par.Value.Cnt == 0) fuera.Add(par.Key);
            for (int i = 0; i < fuera.Count; i++) _cel.Remove(fuera[i]);
        }

        /// <summary>El anillo tiene que cubrir desde (inicio de la ventana - 1) hasta la fila que
        /// entra. Cuando no alcanza se duplica y se recopian las filas vivas.</summary>
        private void Capacidad(long i)
        {
            long desde = _a > 0 ? _a - 1 : 0;
            long hacen = i - desde + 1;
            int cap = _mask + 1;
            if (hacen <= cap) return;
            int nuevo = cap;
            while (nuevo < hacen) nuevo <<= 1;
            var ns = new long[nuevo]; var tks = new long[nuevo];
            var dd = new double[nuevo]; var vol = new double[nuevo]; var mid = new double[nuevo];
            int nm = nuevo - 1;
            for (long k = desde; k < i; k++)
            {
                int vi = (int)(k & _mask), vo = (int)(k & nm);
                ns[vo] = _rNs[vi]; tks[vo] = _rTk[vi]; dd[vo] = _rDd[vi];
                vol[vo] = _rVol[vi]; mid[vo] = _rMid[vi];
            }
            _rNs = ns; _rTk = tks; _rDd = dd; _rVol = vol; _rMid = mid; _mask = nm;
        }

        /// <summary>Cola monotona: saca de la punta todo lo que ya no puede ser el maximo (o el
        /// minimo) y mete el indice nuevo. Cuando el arreglo se llena, primero se corre lo vivo al
        /// principio y solo se agranda si de verdad hace falta.</summary>
        private void Empujar(ref long[] q, ref int h, ref int t, long i, long tk, bool maximo)
        {
            while (t > h)
            {
                long v = _rTk[(int)(q[t - 1] & _mask)];
                if (maximo ? v <= tk : v >= tk) t--; else break;
            }
            if (t == q.Length)
            {
                int vivos = t - h;
                if (vivos * 2 <= q.Length) { Array.Copy(q, h, q, 0, vivos); h = 0; t = vivos; }
                else
                {
                    var nq = new long[q.Length * 2];
                    Array.Copy(q, h, nq, 0, vivos);
                    q = nq; h = 0; t = vivos;
                }
            }
            q[t++] = i;
        }
    }

    // =====================================================================================
    // Utilidades compartidas por el indicador y por el arnes.
    // OJO: esto es una COPIA de AbsorcionNucleo.cs. Un arreglo aca NO llega alla, y al reves
    // tampoco. Por eso el arnes vuelve a correr la prueba de la franja contra America/New_York
    // en vez de darla por heredada.
    // =====================================================================================
    public static class Reloj
    {
        private const long TicksAEpoca = 621355968000000000L;    // 1970-01-01 en ticks de DateTime

        /// <summary>Nanosegundos UTC desde un DateTime. OJO: tanto CumulativeTrade.Time como
        /// IndicatorCandle.Time YA SON UTC aunque vengan con Kind=Unspecified. Llamar a
        /// ToUniversalTime() sobre ellos corre todo tres horas: ya paso (GammaHoyCapas.cs:630).</summary>
        public static long Ns(DateTime t) { return (t.Ticks - TicksAEpoca) * 100L; }

        public static DateTime De(long ns) { return new DateTime(ns / 100L + TicksAEpoca, DateTimeKind.Utc); }
    }

    /// <summary>
    /// La franja horaria de Nueva York, SIN depender del sistema operativo (asi el arnes, el
    /// indicador y el Python dan lo mismo aunque la PC tenga otra zona). Horario de verano de
    /// Estados Unidos: del segundo domingo de marzo a las 02:00 locales al primer domingo de
    /// noviembre a las 02:00 locales.
    /// </summary>
    public static class Franja
    {
        /// <summary>true entre las 09:30 y las 16:00 de Nueva York. Es el mismo corte "rueda" de
        /// r10_lib.segmento() y de r11_f3_lib.en_rueda().</summary>
        public static bool EnRueda(long ns)
        {
            int mm = MinutoNy(ns);
            return mm >= 570 && mm < 960;
        }

        /// <summary>Minuto del dia en Nueva York (0 a 1439).</summary>
        public static int MinutoNy(long ns)
        {
            var utc = Reloj.De(ns);
            var local = utc.AddHours(EsVerano(utc) ? -4 : -5);
            return local.Hour * 60 + local.Minute;
        }

        private static bool EsVerano(DateTime utc)
        {
            int a = utc.Year;
            var ini = Domingo(a, 3, 2).AddHours(7);      // 02:00 EST = 07:00 UTC
            var fin = Domingo(a, 11, 1).AddHours(6);     // 02:00 EDT = 06:00 UTC
            return utc >= ini && utc < fin;
        }

        private static DateTime Domingo(int anio, int mes, int cual)
        {
            var d = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
            int salto = ((int)DayOfWeek.Sunday - (int)d.DayOfWeek + 7) % 7;
            return d.AddDays(salto + 7 * (cual - 1));
        }
    }
}
