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
        /// Depende del instrumento: en ES 50 es tamano; en MES no.</summary>
        public decimal MinBarrido = 50m;

        /// <summary>Cuantos minutos se recuerdan los barridos.</summary>
        public int MemoriaMin = 30;

        /// <summary>Cuanto tiene que caer el tamano parado en el libro para
        /// llamarlo consumido o retirado, como fraccion de lo que habia.</summary>
        public decimal MinCaidaLibro = 0.5m;

        public sealed class Barrido
        {
            public DateTime Hora;
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
                if (t.Volume < MinBarrido) return;
                var b = new Barrido
                {
                    Hora = t.Time,
                    Precio = t.Lastprice,
                    Desde = t.FirstPrice,
                    Volumen = t.Volume,
                    Lado = t.Direction == ATAS.Indicators.TradeDirection.Buy ? 1 : -1,
                    TicksBarridos = (int)Math.Round(Math.Abs(t.Lastprice - t.FirstPrice) / tickSize),
                };
                lock (_llave)
                {
                    _barridos.Add(b);
                    var corte = b.Hora.AddMinutes(-Math.Max(1, MemoriaMin));
                    _barridos.RemoveAll(x => x.Hora < corte);
                    while (_barridos.Count > 400) _barridos.RemoveAt(0);
                }
            }
            catch { /* nunca romper el feed */ }
        }

        /// <summary>Los barridos recientes dentro de la zona de un nivel.</summary>
        public List<Barrido> EnNivel(decimal precio, decimal tickSize, int ticks,
                                     DateTime ahora, int minutos)
        {
            var tol = tickSize * Math.Max(1, ticks);
            var corte = ahora.AddMinutes(-Math.Max(1, minutos));
            lock (_llave)
            {
                return _barridos.Where(x => x.Hora >= corte
                                            && Math.Abs(x.Precio - precio) <= tol)
                                .ToList();
            }
        }

        public int Cantidad { get { lock (_llave) { return _barridos.Count; } } }

        public List<Barrido> Todos(DateTime ahora, int minutos)
        {
            var corte = ahora.AddMinutes(-Math.Max(1, minutos));
            lock (_llave) { return _barridos.Where(x => x.Hora >= corte).ToList(); }
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
