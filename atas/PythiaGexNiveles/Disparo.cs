using System;
using System.Collections.Generic;

namespace PythiaGex
{
    /// <summary>
    /// El gatillo compuesto: la marca en el grafico que dice "aca, ahora".
    ///
    /// POR QUE NO ALCANZA CON UN PRINT GRANDE
    ///
    /// Un print grande solo es ambiguo y no sirve de gatillo: puede ser
    /// alguien defendiendo el nivel o alguien rindiendose y saliendo. Lo mismo
    /// una divergencia suelta. Marcar cada uno de esos eventos llenaria la
    /// pantalla de flechas que aciertan la mitad de las veces, que es
    /// exactamente lo mismo que no tener nada.
    ///
    /// LAS TRES CONDICIONES, Y LA PRIMERA NO SUMA PUNTOS: HABILITA
    ///
    ///   1. UBICACION. El precio tiene que estar DENTRO de la zona de un nivel
    ///      publicado. Sin esto no hay disparo, por mucho flujo que haya. Este
    ///      es el filtro que mata la mayor parte del ruido, y por eso no da
    ///      puntos: es la puerta, no un merito.
    ///
    ///   2. DIRECCION. Alguien tiene que estar ganando, y hay que poder decir
    ///      quien. Solo tres cosas votan: los imbalances apilados, la
    ///      divergencia de delta, y el delta agredido en el nivel durante la
    ///      ventana reciente (no el acumulado del dia, que es contexto). Si
    ///      las tres se anulan entre si, no hay disparo: un nivel donde nadie
    ///      esta ganando claramente no es un gatillo, es una duda.
    ///
    ///   3. PESO. Recien ahi suman los amplificadores: el ESFUERZO (que parte
    ///      del flujo reciente se amontono en este precio), la absorcion, el
    ///      print grande, cuantas razones distintas coinciden ahi, y si el
    ///      delta acumulado del dia acompana. Son amplificadores y no
    ///      direccion: sin el punto 2 no valen nada.
    ///
    ///      El esfuerzo esta para cubrir el caso que el print grande no ve:
    ///      mil ordenes chicas amontonadas en el mismo precio no dejan ningun
    ///      print visible y pesan lo mismo que una orden grande.
    ///
    /// CONTRA EL FALSO POSITIVO
    ///
    ///   - Un umbral que el operador elige. Mas alto = menos flechas y mejores.
    ///   - Enfriamiento: el mismo nivel no vuelve a disparar por N minutos.
    ///   - Rearme: ademas tiene que ALEJARSE del nivel antes de poder volver a
    ///     disparar ahi. Sin esto, un precio pegado al muro dispara sin parar.
    ///
    /// Y LO MAS IMPORTANTE
    ///
    /// Cada disparo se anota con su puntaje y sus componentes. El centinela
    /// mide despues cuantos acertaron. O sea que la pregunta "esto es ruido o
    /// sirve?" no la contesta mi opinion ni la intuicion del operador: la
    /// contesta la cuenta, y con eso se sube o se baja el umbral.
    /// </summary>
    public sealed class Disparo
    {
        public sealed class Evento
        {
            public int Barra;
            public DateTime Hora;
            public decimal Precio;        // el precio del nivel
            public decimal PrecioBarra;   // donde estaba el mercado
            public int Lado;              // +1 largo, -1 corto
            public int Puntaje;
            public string Nivel = "";
            public string Razones = "";
            public bool Es0dte;
        }

        /// <summary>Puntaje minimo para dibujar la flecha.</summary>
        public int Umbral = 3;

        /// <summary>Minutos que el mismo nivel queda callado despues de disparar.</summary>
        public int EnfriamientoMin = 15;

        /// <summary>Ticks que el precio tiene que alejarse del nivel para que
        /// ese nivel vuelva a estar armado.</summary>
        public int TicksRearme = 12;

        /// <summary>Cuantos disparos se guardan dibujados.</summary>
        public int MaxEventos = 60;

        /// <summary>Que parte del flujo reciente tiene que concentrarse en el
        /// nivel para llamarlo esfuerzo. Es el gatillo de "muchos chicos":
        /// detecta el amontonamiento que no deja ningun print grande.</summary>
        public decimal MinPctEsfuerzo = 20m;

        /// <summary>Desde que relacion delta/volumen se considera que alguien
        /// esta empujando de verdad y no solo pasando por ahi.</summary>
        public decimal MinRatioDelta = 0.06m;

        public readonly List<Evento> Eventos = new();

        // por nivel: cuando disparo, y si esta armado
        private readonly Dictionary<string, DateTime> _ultimo = new();
        private readonly Dictionary<string, bool> _armado = new();

        public void Limpiar()
        {
            Eventos.Clear();
            _ultimo.Clear();
            _armado.Clear();
        }

        /// <summary>
        /// Evalua un nivel y, si corresponde, deja el disparo anotado.
        /// Devuelve el evento nuevo o null.
        /// </summary>
        /// <param name="clave">identificador estable del nivel</param>
        /// <param name="precioNivel">el nivel</param>
        /// <param name="precioAhora">donde esta el mercado</param>
        /// <param name="tickSize">tamano del tick</param>
        /// <param name="ticksZona">semiancho de la zona del nivel</param>
        /// <param name="flujo">volumen y delta parados en el nivel</param>
        /// <param name="senal">los gatillos de order flow en ese nivel</param>
        /// <param name="confluencia">cuantas razones coinciden ahi</param>
        /// <param name="minConfluencia">desde cuantas razones suma punto</param>
        /// <param name="deltaSesion">delta acumulado de la sesion</param>
        public Evento Evaluar(string clave, decimal precioNivel, decimal precioAhora,
                              decimal tickSize, int ticksZona,
                              Contexto.Zona flujo, Gatillos.Senal senal,
                              int confluencia, int minConfluencia,
                              decimal deltaSesion, int barra, DateTime hora,
                              List<Libro.Barrido> barridos, Libro.Suerte suerteMuro,
                              decimal desbalanceDom)
        {
            if (tickSize <= 0 || precioNivel <= 0 || precioAhora <= 0) return null;

            var dist = Math.Abs(precioAhora - precioNivel) / tickSize;

            // --- rearme: alejarse vuelve a armar el nivel
            if (dist >= Math.Max(2, TicksRearme)) _armado[clave] = true;
            if (!_armado.TryGetValue(clave, out var armado)) { _armado[clave] = true; armado = true; }

            // --- 1. UBICACION. La puerta. No suma, habilita.
            if (dist > Math.Max(1, ticksZona)) return null;
            if (!armado) return null;
            if (_ultimo.TryGetValue(clave, out var cuando)
                && (hora - cuando).TotalMinutes < Math.Max(1, EnfriamientoMin)) return null;

            // --- 2. DIRECCION. Solo tres cosas votan, y tienen que coincidir.
            int voto = 0;
            var razones = new List<string>();

            if (senal != null && senal.Listo && senal.Apilados > 0)
            {
                voto += senal.Lado;
                razones.Add(senal.Apilados + " imbalances "
                            + (senal.Lado > 0 ? "compradores" : "vendedores"));
            }
            if (senal != null && senal.Listo && senal.Divergencia)
            {
                voto += senal.LadoDivergencia;
                razones.Add(senal.LadoDivergencia > 0
                            ? "divergencia alcista" : "divergencia bajista");
            }
            // El delta de la VENTANA, no el acumulado del dia. Para un gatillo
            // importa quien esta ganando ahora; el acumulado del dia es
            // contexto y entra mas abajo, como amplificador.
            if (senal != null && senal.Listo && Math.Abs(senal.RatioDelta) >= MinRatioDelta)
            {
                voto += senal.RatioDelta > 0 ? 1 : -1;
                razones.Add((senal.RatioDelta > 0 ? "delta comprador" : "delta vendedor")
                            + " " + Math.Abs(Math.Round(senal.RatioDelta * 100, 0))
                            + "% del flujo del nivel");
            }
            // BARRIDOS: agresores de verdad, uno por uno, no volumen agregado.
            // Este es el unico que ve "alguien entro de una", y por eso vota
            // con el peso de lo que efectivamente barrio.
            if (barridos != null && barridos.Count > 0)
            {
                decimal vc = 0, vv = 0;
                foreach (var x in barridos)
                    if (x.Lado > 0) vc += x.Volumen; else vv += x.Volumen;
                if (vc + vv > 0 && Math.Abs(vc - vv) / (vc + vv) >= 0.25m)
                {
                    voto += vc > vv ? 1 : -1;
                    razones.Add(barridos.Count + " barrido" + (barridos.Count > 1 ? "s" : "")
                                + " " + (vc > vv ? "comprador" : "vendedor") + " de "
                                + Math.Round(Math.Max(vc, vv)) + " lotes");
                }
            }

            // EL MURO DEL LIBRO. Que se lo hayan COMIDO es evidencia dura:
            // alguien pago por atravesarlo y eso no se puede fingir. Que lo
            // hayan RETIRADO dice lo contrario y por eso vota al reves: la
            // pared nunca estuvo dispuesta a defender ese precio.
            if (suerteMuro == Libro.Suerte.Comido)
            {
                voto += precioAhora >= precioNivel ? 1 : -1;
                razones.Add("se comieron el muro del libro");
            }
            else if (suerteMuro == Libro.Suerte.Retirado)
            {
                voto += precioAhora >= precioNivel ? -1 : 1;
                razones.Add("retiraron el muro sin defenderlo");
            }

            if (voto == 0) return null;
            int lado = voto > 0 ? 1 : -1;

            // Los votos en contra descuentan: si hay dos senales para un lado y
            // una para el otro, el caso es mas flojo que si fueran dos limpias.
            int puntaje = Math.Abs(voto);

            // --- 3. PESO. Amplificadores. Sin direccion no valdrian nada, y
            // por eso se suman recien aca.
            // ESFUERZO: muchos chicos amontonados. Es la otra mitad de los
            // casos, la que el print grande no ve.
            if (senal != null && senal.Listo && senal.PctVentana >= MinPctEsfuerzo)
            {
                puntaje++;
                razones.Add(Math.Round(senal.PctVentana, 0)
                            + "% del flujo reciente se concentro aca");
            }
            if (flujo != null && flujo.Absorcion) { puntaje++; razones.Add("absorcion"); }
            if (senal != null && senal.Listo && senal.PrintGrande)
            { puntaje++; razones.Add("print grande x" + senal.PrintVeces.ToString("0")); }
            if (confluencia >= Math.Max(1, minConfluencia))
            { puntaje++; razones.Add(confluencia + " confluencias"); }
            if (deltaSesion != 0 && Math.Sign(deltaSesion) == lado)
            { puntaje++; razones.Add("el delta de la sesion acompana"); }

            // El libro entero inclinado para el mismo lado. Amplificador y no
            // direccion a proposito: las ordenes limite se retiran, asi que el
            // tamano parado sugiere pero no prueba.
            if (Math.Abs(desbalanceDom) >= 0.20m && Math.Sign(desbalanceDom) == lado)
            {
                puntaje++;
                razones.Add("el libro esta " + Math.Round(Math.Abs(desbalanceDom) * 100)
                            + "% inclinado a favor");
            }
            if (suerteMuro == Libro.Suerte.Crecio)
            {
                // alguien esta REFORZANDO la pared: juega en contra de pasarla
                puntaje = Math.Max(1, puntaje - 1);
                razones.Add("ojo: estan reforzando el muro");
            }

            if (puntaje < Math.Max(1, Umbral)) return null;

            var e = new Evento
            {
                Barra = barra, Hora = hora,
                Precio = precioNivel, PrecioBarra = precioAhora,
                Lado = lado, Puntaje = puntaje,
                Nivel = clave, Razones = string.Join(" + ", razones),
            };
            Eventos.Add(e);
            while (Eventos.Count > Math.Max(5, MaxEventos)) Eventos.RemoveAt(0);
            _ultimo[clave] = hora;
            _armado[clave] = false;   // hasta que se aleje, este nivel queda callado
            return e;
        }
    }
}
