using System;
using System.Collections.Generic;
using System.Linq;

using ATAS.DataFeedsCore;
using ATAS.Indicators;

namespace PythiaGex
{
    /// <summary>
    /// Lo que estaba pasando por afuera de todo lo demas: el libro y los
    /// barridos de verdad.
    ///
    /// DOS COSAS DISTINTAS QUE NO HAY QUE MEZCLAR
    ///
    /// El footprint muestra lo YA OPERADO: ordenes agresivas que se comieron
    /// lo que habia. El heatmap muestra lo que esta ESPERANDO: ordenes limite
    /// puestas y todavia sin ejecutar. Son dos mundos y hasta ahora el
    /// indicador solo miraba el primero.
    ///
    /// EL BARRIDO (cumulative trade)
    ///
    /// Lo que veniamos llamando "print grande" era el volumen de un precio en
    /// una vela: un agregado, no una operacion. Podian ser mil ordenes de un
    /// contrato. ATAS junta aparte los ticks consecutivos de un mismo agresor
    /// en un "cumulative trade", y ESO si es un barrido: alguien entrando de
    /// una, comiendose varios precios. Es distinto y es lo que se buscaba.
    ///
    /// LA TRAMPA DEL HEATMAP, Y ES GRANDE
    ///
    /// Una pared enorme de ordenes en el libro NO es evidencia de nada. Las
    /// ordenes limite se retiran, y ponerlas para que las vean y sacarlas
    /// antes de que las toquen es una practica corriente. Mirar el tamano
    /// parado en el libro y creerle es el error clasico del que recien empieza
    /// a mirar el DOM.
    ///
    /// Lo que SI es evidencia es lo que le pasa a esa pared cuando el precio
    /// llega:
    ///
    ///   - se COMIO  -> el tamano bajo Y hubo volumen operado ahi. Alguien
    ///                  pago por atravesarla. Eso es real y no se puede fingir.
    ///   - se RETIRO -> el tamano bajo SIN volumen operado. La pared nunca
    ///                  estuvo dispuesta a defender. Tambien es informacion,
    ///                  pero la contraria.
    ///
    /// Distinguir esas dos cosas es todo el valor de mirar el libro, y es
    /// justamente lo que un heatmap a ojo no te dice.
    /// </summary>
    public sealed class Libro
    {
        // ------------------------------------------------------------------
        // Umbrales, arriba y con su razon, como todo en este proyecto
        // ------------------------------------------------------------------

        /// <summary>Contratos que tiene que tener un barrido para contarlo.
        ///
        /// Poner un numero fijo de contratos fue un error de raiz: 50 lotes es
        /// tamano en ES, es poquisimo en MES y no significa nada en NQ. El
        /// mismo indicador puesto en dos graficos se comportaba distinto sin
        /// que nada lo dijera.
        ///
        /// Por eso el modo normal es AUTOMATICO: se mide la mediana de los
        /// barridos de ESTE instrumento y se llama grande al que la supera por
        /// el factor elegido. Asi funciona igual en ES, en MES o en lo que
        /// sea, y el numero que sale queda a la vista para poder discutirlo.
        /// </summary>
        public decimal MinBarrido = 15m;

        /// <summary>Si es true, MinBarrido se recalcula de lo observado.</summary>
        public bool UmbralAutomatico = true;

        /// <summary>Cuantas veces la mediana del instrumento.</summary>
        public decimal FactorUmbral = 8m;

        /// <summary>Cuantos barridos hay que ver antes de confiar en la
        /// mediana. Con pocos, cualquier numero es ruido.</summary>
        public int MinMuestraUmbral = 60;

        /// <summary>El umbral que se esta usando de verdad, ya resuelto.</summary>
        public decimal UmbralVigente { get; private set; } = 15m;

        /// <summary>Recalcula el umbral automatico. Se llama seguido y es
        /// barato: la mediana sale de una lista ya ordenada en memoria.</summary>
        public void ResolverUmbral()
        {
            if (!UmbralAutomatico) { UmbralVigente = MinBarrido; return; }
            decimal med;
            int n;
            lock (_llave) { n = _tamanos.Count; }
            med = Mediana;
            if (n < Math.Max(10, MinMuestraUmbral) || med <= 0)
            {
                // todavia no hay con que medir: se usa lo que puso el operador
                UmbralVigente = MinBarrido;
                return;
            }
            var u = Math.Ceiling(med * Math.Max(1.5m, FactorUmbral));
            UmbralVigente = Math.Max(2m, u);
        }

        /// <summary>Cuantos minutos se recuerdan los barridos.</summary>
        public int MemoriaMin = 30;

        /// <summary>Cuanto tiene que caer el tamano parado en el libro para
        /// llamarlo consumido o retirado, como fraccion de lo que habia.</summary>
        public decimal MinCaidaLibro = 0.5m;

        public sealed class Barrido
        {
            /// <summary>La hora que informa el exchange. Se guarda para el
            /// registro, NO para medir ventanas.</summary>
            public DateTime Hora;

            /// <summary>Cuando lo recibimos, en UTC de esta maquina.
            ///
            /// Las ventanas se miden con ESTE reloj y no con el del exchange.
            /// Los barridos venian con la hora de CumulativeTrade y el "ahora"
            /// salia de la ultima vela: dos relojes distintos, posiblemente en
            /// husos distintos. Si se corren, la ventana de cinco minutos no
            /// encuentra nada aunque el barrido acabe de pasar — y eso es
            /// exactamente lo que se vio en pantalla: un barrido dibujado y la
            /// cinta diciendo "sin barridos".
            ///
            /// OnCumulativeTrade llega en tiempo casi real, asi que sellarlo al
            /// recibirlo da un reloj unico, monotono y sin husos de por medio.</summary>
            public DateTime RecibidoUtc;
            public decimal Precio;       // donde termino
            public decimal Desde;        // donde arranco
            public decimal Volumen;
            public int Lado;             // +1 compra agresiva, -1 venta agresiva
            public int TicksBarridos;    // cuantos precios se llevo puestos
            /// <summary>La barra donde cae, resuelta la primera vez que se
            /// dibuja. Anotar() corre en el hilo de datos y ahi no se puede
            /// preguntar por el grafico.</summary>
            public int Barra = -1;
        }

        /// <summary>Como esta el libro parado alrededor de un nivel.</summary>
        public sealed class Muro
        {
            public decimal Bids, Asks;          // contratos esperando en la zona
            public decimal DesbalanceZona;      // (bids-asks)/(bids+asks)
            public bool Hay;
        }

        /// <summary>Que le paso a ese muro desde la ultima mirada.</summary>
        public enum Suerte { SinDato, Igual, Comido, Retirado, Crecio }

        private readonly List<Barrido> _barridos = new();
        private readonly object _llave = new();

        // memoria del libro por nivel, para poder comparar
        private sealed class Memoria
        {
            public decimal Tamano;
            public decimal VolAcum;
            public DateTime Cuando;
        }
        private readonly Dictionary<string, Memoria> _antes = new();

        public decimal DomBids, DomAsks;
        public bool LibroVivo;

        // Diagnostico. Sin esto, "no veo barridos" es indistinguible entre
        // "el feed no manda nada" y "tu umbral esta demasiado alto", que son
        // dos problemas opuestos. Se cuenta TODO lo que llega, antes del
        // filtro, y se guardan los tamanos para poder sugerir un umbral.
        public long VistosTotal;
        public decimal MayorVisto;
        private readonly List<decimal> _tamanos = new();

        /// <summary>El tamano que solo superan uno de cada diez barridos. Es
        /// el umbral que deja pasar lo destacado de este instrumento sin que
        /// haya que adivinarlo.</summary>
        public decimal P90
        {
            get
            {
                lock (_llave)
                {
                    if (_tamanos.Count < 10) return 0m;
                    var o = _tamanos.OrderBy(x => x).ToList();
                    return o[(int)(o.Count * 0.90)];
                }
            }
        }

        public decimal Mediana
        {
            get
            {
                lock (_llave)
                {
                    if (_tamanos.Count == 0) return 0m;
                    var o = _tamanos.OrderBy(x => x).ToList();
                    return o[o.Count / 2];
                }
            }
        }

        /// <summary>Desbalance del libro entero. Positivo = mas ordenes
        /// esperando del lado comprador.</summary>
        public decimal DesbalanceDom
            => (DomBids + DomAsks) > 0 ? (DomBids - DomAsks) / (DomBids + DomAsks) : 0m;

        // ------------------------------------------------------------------
        // Barridos
        // ------------------------------------------------------------------

        /// <summary>Se llama desde el hilo de datos: tiene que ser barato y no
        /// puede tirar excepciones hacia la plataforma.</summary>
        public void Anotar(CumulativeTrade t, decimal tickSize)
        {
            if (t == null || tickSize <= 0) return;
            try
            {
                lock (_llave)
                {
                    VistosTotal++;
                    if (t.Volume > MayorVisto) MayorVisto = t.Volume;
                    _tamanos.Add(t.Volume);
                    while (_tamanos.Count > 3000) _tamanos.RemoveAt(0);
                }
                if (t.Volume < UmbralVigente) return;
                var b = new Barrido
                {
                    Hora = t.Time,
                    RecibidoUtc = DateTime.UtcNow,
                    Precio = t.Lastprice,
                    Desde = t.FirstPrice,
                    Volumen = t.Volume,
                    Lado = t.Direction == ATAS.Indicators.TradeDirection.Buy ? 1 : -1,
                    TicksBarridos = (int)Math.Round(Math.Abs(t.Lastprice - t.FirstPrice) / tickSize),
                };
                lock (_llave)
                {
                    _barridos.Add(b);
                    var corte = b.RecibidoUtc.AddMinutes(-Math.Max(1, MemoriaMin));
                    _barridos.RemoveAll(x => x.RecibidoUtc < corte);
                    while (_barridos.Count > 400) _barridos.RemoveAt(0);
                }
            }
            catch { /* nunca romper el feed */ }
        }

        /// <summary>Los barridos recientes dentro de la zona de un nivel.
        /// La ventana se mide contra el reloj de recepcion, en UTC.</summary>
        public List<Barrido> EnNivel(decimal precio, decimal tickSize, int ticks,
                                     int minutos)
        {
            var tol = tickSize * Math.Max(1, ticks);
            var corte = DateTime.UtcNow.AddMinutes(-Math.Max(1, minutos));
            lock (_llave)
            {
                return _barridos.Where(x => x.RecibidoUtc >= corte
                                            && Math.Abs(x.Precio - precio) <= tol)
                                .ToList();
            }
        }

        public int Cantidad { get { lock (_llave) { return _barridos.Count; } } }

        public List<Barrido> Todos(int minutos)
        {
            var corte = DateTime.UtcNow.AddMinutes(-Math.Max(1, minutos));
            lock (_llave) { return _barridos.Where(x => x.RecibidoUtc >= corte).ToList(); }
        }

        // ------------------------------------------------------------------
        // El contrato, leido del exchange y no de una tabla
        // ------------------------------------------------------------------

        /// <summary>Multiplicador en dolares por punto, segun lo que informa el
        /// feed. Cero si todavia no se pudo leer.</summary>
        public decimal MultiplicadorReal;
        public decimal TickReal;
        public string OrigenContrato = "sin leer";

        /// <summary>
        /// Saca el multiplicador de la especificacion que manda el exchange, en
        /// vez de la tabla escrita a mano.
        ///
        /// El multiplicador sale de dividir cuanto vale un tick por cuanto mide
        /// un tick: en ES son 12.50 dolares cada 0.25 puntos, o sea 50 dolares
        /// por punto. Eso lo publica el feed y no hay por que suponerlo.
        /// </summary>
        public void LeerContrato(decimal tickCost, decimal tickSize)
        {
            if (tickSize <= 0 || tickCost <= 0)
            {
                OrigenContrato = "el feed no informa el valor del tick";
                return;
            }
            TickReal = tickSize;
            MultiplicadorReal = tickCost / tickSize;
            OrigenContrato = "del feed";
        }

        // ------------------------------------------------------------------
        // El libro
        // ------------------------------------------------------------------

        /// <summary>Cuanto hay esperando alrededor de un nivel, por lado.</summary>
        public Muro Parado(IEnumerable<MarketDataArg> libro, decimal precio,
                           decimal tickSize, int ticks)
        {
            var m = new Muro();
            if (libro == null || tickSize <= 0) return m;
            var tol = tickSize * Math.Max(1, ticks);
            try
            {
                foreach (var d in libro)
                {
                    if (d == null) continue;
                    if (Math.Abs(d.Price - precio) > tol) continue;
                    if (d.IsBid) m.Bids += d.Volume;
                    else if (d.IsAsk) m.Asks += d.Volume;
                }
            }
            catch { return m; }
            var tot = m.Bids + m.Asks;
            m.Hay = tot > 0;
            m.DesbalanceZona = tot > 0 ? (m.Bids - m.Asks) / tot : 0m;
            return m;
        }

        /// <summary>
        /// Que le paso al muro desde la ultima vez que se miro.
        ///
        /// La distincion que importa: si el tamano bajo Y hubo volumen operado,
        /// se lo COMIERON —alguien pago por atravesarlo y eso no se finge—. Si
        /// bajo sin volumen, lo RETIRARON: nunca estuvo dispuesto a defender.
        /// </summary>
        public Suerte Comparar(string clave, decimal tamanoAhora, decimal volAcumAhora,
                               DateTime ahora)
        {
            if (!_antes.TryGetValue(clave, out var m))
            {
                _antes[clave] = new Memoria
                { Tamano = tamanoAhora, VolAcum = volAcumAhora, Cuando = ahora };
                return Suerte.SinDato;
            }

            var caida = m.Tamano > 0 ? (m.Tamano - tamanoAhora) / m.Tamano : 0m;
            var operado = volAcumAhora - m.VolAcum;
            var r = Suerte.Igual;

            if (tamanoAhora > m.Tamano * 1.5m && m.Tamano > 0) r = Suerte.Crecio;
            else if (caida >= MinCaidaLibro)
                r = operado > 0 ? Suerte.Comido : Suerte.Retirado;

            m.Tamano = tamanoAhora; m.VolAcum = volAcumAhora; m.Cuando = ahora;
            return r;
        }

        public void Limpiar()
        {
            lock (_llave) { _barridos.Clear(); }
            _antes.Clear();
        }
    }
}
