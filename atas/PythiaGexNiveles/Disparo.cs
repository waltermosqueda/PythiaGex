using System;
using System.Collections.Generic;
using System.Linq;

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
        public int MaxEventos = 400;

        /// <summary>Que parte del flujo reciente tiene que concentrarse en el
        /// nivel para llamarlo esfuerzo. Es el gatillo de "muchos chicos":
        /// detecta el amontonamiento que no deja ningun print grande.</summary>
        public decimal MinPctEsfuerzo = 20m;

        /// <summary>Desde que relacion delta/volumen se considera que alguien
        /// esta empujando de verdad y no solo pasando por ahi.</summary>
        public decimal MinRatioDelta = 0.06m;

        public readonly List<Evento> Eventos = new();

        /// <summary>Cuantos disparos se guardan dibujados. Se subio de 60 a
        /// 400: un disparo que se borra de la pantalla no se puede revisar
        /// despues, y revisarlos es el unico modo de saber si sirven.</summary>
        public string Carpeta = "";

        // por nivel: cuando disparo, y si esta armado
        private readonly Dictionary<string, DateTime> _ultimo = new();
        private readonly Dictionary<string, bool> _armado = new();

        /// <summary>
        /// Deja el disparo en disco apenas ocurre.
        ///
        /// Antes vivian solo en memoria: al cerrar ATAS se perdian, y sin
        /// registro no hay forma de decir despues "este acerto y este no".
        /// Medir el gatillo es justamente lo que decide si sirve o es ruido,
        /// asi que el registro no es un extra: es la razon de que exista.
        /// </summary>
        private void Persistir(Evento e)
        {
            try
            {
                var dir = string.IsNullOrWhiteSpace(Carpeta)
                    ? System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "ATAS", "PythiaGex", "contexto")
                    : Carpeta.Trim();
                System.IO.Directory.CreateDirectory(dir);
                var ruta = System.IO.Path.Combine(
                    dir, "disparos-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".jsonl");
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var l = "{\"t\":\"" + e.Hora.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss", inv)
                      + "Z\",\"nivel\":\"" + (e.Nivel ?? "").Replace("\"", "'")
                      + "\",\"lado\":" + e.Lado
                      + ",\"puntaje\":" + e.Puntaje
                      + ",\"precio\":" + Math.Round(e.Precio, 2).ToString(inv)
                      + ",\"precio_barra\":" + Math.Round(e.PrecioBarra, 2).ToString(inv)
                      + ",\"es0dte\":" + (e.Es0dte ? "true" : "false")
                      + ",\"razones\":\"" + (e.Razones ?? "").Replace("\"", "'") + "\"}";
                System.IO.File.AppendAllText(ruta, l + "\n",
                    new System.Text.UTF8Encoding(false));
            }
            catch { /* nunca romper el indicador por no poder escribir */ }
        }

        /// <summary>Vuelve a cargar los disparos del dia para que sigan
        /// dibujados despues de reiniciar ATAS.</summary>
        public int Recuperar(int maxEventos)
        {
            try
            {
                var dir = string.IsNullOrWhiteSpace(Carpeta)
                    ? System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "ATAS", "PythiaGex", "contexto")
                    : Carpeta.Trim();
                var ruta = System.IO.Path.Combine(
                    dir, "disparos-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".jsonl");
                if (!System.IO.File.Exists(ruta)) return 0;
                int n = 0;
                foreach (var l in System.IO.File.ReadAllLines(ruta))
                {
                    var e = Leer(l);
                    if (e == null) continue;
                    if (Eventos.Any(x => x.Hora == e.Hora && x.Nivel == e.Nivel
                                         && x.Precio == e.Precio)) continue;
                    Eventos.Add(e);
                    n++;
                }
                Eventos.Sort((x, y) => x.Hora.CompareTo(y.Hora));
                while (Eventos.Count > Math.Max(5, maxEventos)) Eventos.RemoveAt(0);
                return n;
            }
            catch { return 0; }
        }

        private static Evento Leer(string l)
        {
            if (string.IsNullOrWhiteSpace(l) || l[0] != '{') return null;
            try
            {
                string Tx(string k)
                {
                    var i = l.IndexOf("\"" + k + "\":\"", StringComparison.Ordinal);
                    if (i < 0) return "";
                    i += k.Length + 4;
                    var j = l.IndexOf('"', i);
                    return j < 0 ? "" : l.Substring(i, j - i);
                }
                double Nm(string k)
                {
                    var i = l.IndexOf("\"" + k + "\":", StringComparison.Ordinal);
                    if (i < 0) return 0;
                    i += k.Length + 3;
                    var j = i;
                    while (j < l.Length && (char.IsDigit(l[j]) || l[j] == '-'
                           || l[j] == '.' || l[j] == '+')) j++;
                    return double.TryParse(l.Substring(i, j - i), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
                }
                var t = Tx("t");
                if (t.Length < 19) return null;
                return new Evento
                {
                    Hora = DateTime.Parse(t, System.Globalization.CultureInfo.InvariantCulture,
                                          System.Globalization.DateTimeStyles.AdjustToUniversal
                                          | System.Globalization.DateTimeStyles.AssumeUniversal),
                    Nivel = Tx("nivel"), Razones = Tx("razones"),
                    Lado = (int)Nm("lado"), Puntaje = (int)Nm("puntaje"),
                    Precio = (decimal)Nm("precio"), PrecioBarra = (decimal)Nm("precio_barra"),
                    Barra = -1,
                };
            }
            catch { return null; }
        }

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
            // EL DELTA DE LA VENTANA, no el acumulado del dia: para un
            // gatillo importa quien esta ganando ahora.
            //
            // LO QUE SE INTENTO MEDIR Y NO SE PUDO, QUE HAY QUE DEJAR ESCRITO
            //
            // El 2026-09-01 se probo condicionar este voto a que hubiera
            // absorcion. Parecia respaldado: 203 casos, 67 % con absorcion
            // contra 40 % sin. Pero al auditarlo se cayo entero, por dos
            // motivos que ya conociamos y volvimos a pisar:
            //
            //   - EL SESGO DEL MERCADO. En esas dos ruedas el precio bajo casi
            //     sin parar: los cortos acertaban 93 % y los largos 5 %. De los
            //     66 casos "con absorcion", 43 eran cortos. El 67 % era la
            //     tendencia, no la senal.
            //   - LA CORRELACION SERIAL. El indicador anota cada cinco minutos,
            //     asi que la misma escena aparecia una y otra vez. Los 203
            //     casos eran 15 episodios independientes.
            //
            // Normalizando por la tasa base de cada direccion y colapsando a
            // episodios: 15 episodios, 53 % contra una base de 50 %. Nada.
            //
            // Asi que el voto queda como estaba. NO porque este validado, sino
            // porque con dos ruedas de un mercado que fue para un solo lado no
            // se puede validar ni descartar nada. La herramienta que lo mide
            // quedo en herramientas/probar_gatillos.py y hay que volver a
            // correrla cuando el centinela junte ruedas con las dos
            // direcciones representadas.
            if (senal != null && senal.Listo && Math.Abs(senal.RatioDelta) >= MinRatioDelta)
            {
                voto += senal.RatioDelta > 0 ? 1 : -1;
                razones.Add((senal.RatioDelta > 0 ? "delta comprador" : "delta vendedor")
                            + " " + Math.Abs(Math.Round(senal.RatioDelta * 100, 0))
                            + "% del flujo del nivel");
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
            Persistir(e);
            _ultimo[clave] = hora;
            _armado[clave] = false;   // hasta que se aleje, este nivel queda callado
            return e;
        }
    }
}
