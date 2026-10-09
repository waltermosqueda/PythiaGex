using System;
using System.Collections.Generic;

namespace AbsorcionViva
{
    // =====================================================================================
    // EL NUCLEO DE ABSORCION VIVA. Codigo PURO: no conoce ATAS, no dibuja, no escribe archivos.
    //
    // Come la cinta orden por orden -los mismos 14 campos que graba FlujoClaroSonda.cs- y escupe
    // EVENTOS {hora, precio, tamano, lado, tipo}. Nada mas.
    //
    // POR QUE ESTA SEPARADO, y por que la capa de dibujo solo conoce Evento:
    //   1. El operador pidio despues LO MISMO CON EL DELTA. Para eso alcanza con escribir otro
    //      nucleo que implemente IFuenteDeEventos y devuelva eventos con otro Tipo. El dibujo no
    //      se toca ni una linea. (El delta es una ronda aparte: NO se hace ahora.)
    //   2. PARIDAD: este mismo archivo .cs se compila tambien en arnes/ (consola, sin ATAS) y se
    //      corre sobre las cintas grabadas, para comparar evento por evento contra el Python que
    //      produjo las mediciones. Lo que se mide es el codigo que corre adentro del grafico.
    //
    // NADA DE ESTO PREDICE NADA. Las rondas 1 a 9 llevan unos 13.000 ensayos sin una regla que le
    // gane a los 0,948 puntos que cuesta la vuelta, y la ronda 10 no encontro ninguna tampoco: el
    // mejor t de la familia (+2,59) esta por debajo del techo que regala el azar con 143 ensayos
    // (2,65) y se cae a +0,29 en el contrato de hoy. Esto DESCRIBE. No anticipa.
    // =====================================================================================

    /// <summary>Una agresion ya cerrada, con la mejor punta ANTES y DESPUES. Son las 14 columnas
    /// de la cinta de la sonda y, en vivo, los campos de un CumulativeTrade de ATAS.</summary>
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

    /// <summary>Los tipos de marca. El delta agregara 10, 11, ... sin tocar el dibujo.</summary>
    public static class Tipos
    {
        public const int AbsorcionNivel = 0;
        public const int Iceberg = 1;
        public const int AgresorAtrapado = 2;

        public static string Nombre(int t)
        {
            switch (t)
            {
                case AbsorcionNivel: return "absorcion_nivel";
                case Iceberg: return "iceberg_aguante";
                case AgresorAtrapado: return "agresor_atrapado";
            }
            return "tipo_" + t;
        }
    }

    /// <summary>
    /// Lo unico que la capa de dibujo sabe de un nucleo. Un evento que sigue VIVO se actualiza
    /// solo (el mismo objeto): la pelotita crece en pantalla sin que nadie la vuelva a crear.
    /// </summary>
    public sealed class Evento
    {
        /// <summary>Que clase de marca es. Ver <see cref="Tipos"/>.</summary>
        public int Tipo;
        /// <summary>Instante en que se ENCENDIO, en nanosegundos UTC. Es la x de la marca.</summary>
        public long Ns;
        /// <summary>Instante en que arranco (la primera agresion contra ese precio).</summary>
        public long NsInicio;
        /// <summary>El precio de la marca, en TICKS enteros. Es la y, exacta.</summary>
        public long PrecioTk;
        /// <summary>El mismo precio en puntos.</summary>
        public double Precio;
        /// <summary>+1 se comieron COMPRAS agresoras (alguien vende en limite y repone) = VERDE.
        /// -1 se comieron VENTAS agresoras = ROJO.</summary>
        public int Lado;
        /// <summary>Contratos. Es el diametro de la pelotita. SIGUE CRECIENDO mientras el nivel
        /// aguante: por eso el objeto no se copia.</summary>
        public double Tamano;
        /// <summary>Contratos que tenia cuando se encendio (el resto es lo que crecio despues).</summary>
        public double TamanoAlEncender;
        /// <summary>Contratos que el nivel MOSTRABA cuando arranco el episodio.</summary>
        public double VistoInicial;
        /// <summary>Tamano / VistoInicial: cuantas veces se repuso. El "x46" del iceberg.</summary>
        public double Aguante;
        /// <summary>Cuantas agresiones lo formaron.</summary>
        public int Filas;
        /// <summary>Solo para el agresor atrapado: ticks que corrio el barrido.</summary>
        public double Ticks;
        /// <summary>Solo para el agresor atrapado: segundos que tardo en volver.</summary>
        public double EsperaSeg;
        /// <summary>Que episodio lo produjo. Dos eventos con el mismo numero son el MISMO nivel
        /// (la pelotita y su aro de iceberg): la capa de dibujo los junta en una sola marca.</summary>
        public long IdEpisodio;
        /// <summary>false mientras el nivel sigue aguantando.</summary>
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
        /// <see cref="Nuevos"/>, que se pisa en cada llamada (no se guarda la lista, se guardan
        /// los Evento).</summary>
        void Agregar(in Orden o);

        /// <summary>Los eventos que nacieron en la ultima llamada a <see cref="Agregar"/>.</summary>
        List<Evento> Nuevos { get; }

        /// <summary>Se llama al cambiar de instrumento o de sesion.</summary>
        void Reiniciar();
    }

    /// <summary>Varios detectores como si fueran uno. Para el delta: se agrega el nucleo nuevo
    /// aca y no se toca nada mas.</summary>
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
    // NUCLEO 1: EPISODIO DE NIVEL  ->  marcas "absorcion_nivel" e "iceberg_aguante"
    // =====================================================================================
    /// <summary>
    /// ABSORCION MEDIDA POR NIVEL DE PRECIO, acumulando por EPISODIO.
    ///
    /// POR QUE NO ALCANZA CON MIRAR UNA FILA (medido, r10_02b_salida.txt): adentro de una misma
    /// agresion el tamano de la punta DESPUES es exactamente el de ANTES menos el volumen, en el
    /// 100,0 % de 129.896 filas de MNQ y el 99,8 % de 55.278 de MES. La foto "despues" no agrega
    /// nada. Y una agresion sola mas grande que la punta casi nunca deja el precio quieto: el
    /// 0,2 % de 84.105 casos en MNQ, el 0,0 % de 10.789 en MES. La absorcion solo aparece
    /// ENCADENANDO filas.
    ///
    /// POR QUE NO ALCANZA CON LA "POSTA ESTRICTA" (cortar el episodio apenas la punta se mueve):
    /// medido, da 1.432.489 postas por sesion pero solo 20 con 100 contratos comidos o mas y 2 con
    /// 200 o mas. Con esa definicion la pelotita NO PUEDE CRECER NUNCA, y crecer en vivo es medio
    /// pedido. El episodio de nivel sobrevive a que la punta se vaya y vuelva: 110 episodios por
    /// sesion con 100 o mas, contra 20.
    ///
    /// COMO MUERE UN NIVEL (declarado, no barrido, para no inflar el conteo de ensayos):
    ///   - el mercado lo ATRAVIESA por mas de <see cref="MuerteTicks"/> ticks a favor del agresor;
    ///   - o pasan <see cref="MuerteSeg"/> segundos sin que la punta vuelva a pararse ahi.
    /// Que la punta se vaya y vuelva NO lo mata.
    ///
    /// ORDEN DE CADA FILA, y esto es lo que lo hace CAUSAL (no mira el futuro):
    ///   1. llega la foto de la punta ANTES: mueren los niveles atravesados; se abre o continua el
    ///      nivel de ese precio; se suma la liquidez que de verdad es nueva;
    ///   2. SE CHEQUEA EL ENCENDIDO, con todo lo anterior y NADA de esta fila;
    ///   3. recien ahi se suma el consumo de esta fila.
    /// Auditado: cortar la cinta y rehacerla devuelve los mismos eventos (24 cortes en 8 sesiones,
    /// 35.586 eventos, 0 diferencias - r10_f4_02_salida.txt).
    ///
    /// Es la misma definicion, paso por paso, que r10_f4_lib.py:_niveles(), que es el Python con el
    /// que se midio todo. El arnes de paridad compara las dos implementaciones fila por fila.
    /// </summary>
    public sealed class NucleoNivel : IFuenteDeEventos
    {
        // ---------------- ajustes ----------------
        /// <summary>Tamano del tick del instrumento.</summary>
        public double Tick = 0.25;
        /// <summary>G: el mercado atraveso el nivel por mas de estos ticks -> murio.</summary>
        public int MuerteTicks = 4;
        /// <summary>H: segundos sin que la punta vuelva al nivel -> murio.</summary>
        public double MuerteSeg = 60;

        /// <summary>Contratos comidos para encender la marca, en la rueda (09:30-16:00 NY).</summary>
        public double UmbralRueda = 150;
        /// <summary>Contratos comidos para encender la marca fuera de la rueda.</summary>
        public double UmbralFuera = 90;
        /// <summary>false: se usa siempre <see cref="UmbralRueda"/> (es el modo con el que corre
        /// la paridad, para que el Python y el C# comparen exactamente la misma regla).</summary>
        public bool PartirPorFranja = true;

        /// <summary>Veces que el nivel se comio lo que mostraba, para el aro de iceberg.</summary>
        public double IcebergAguante = 40;
        /// <summary>Contratos que el nivel tiene que mostrar al empezar para que el aro cuente.
        /// Con 1 contrato visible el cociente se dispara por nada.</summary>
        public double IcebergVisibleMin = 3;

        public bool PrenderAbsorcion = true;
        public bool PrenderIceberg = true;

        /// <summary>Si es >= 0, cada episodio que cierra con al menos estos contratos comidos se
        /// guarda en <see cref="Censo"/>. Es SOLO para el arnes de paridad: adentro de ATAS queda
        /// en -1 y no se guarda nada.</summary>
        public double PisoCenso = -1;
        public readonly List<Episodio> Censo = new List<Episodio>();

        /// <summary>Una foto completa de un episodio cerrado. Tiene exactamente las columnas que
        /// emite r10_f4_lib.py:_niveles(), para poder compararlas una por una.</summary>
        public struct Episodio
        {
            public int LadoAbs;
            public long TIni, TFin;
            public long IIni, IFin;
            public long PxTk;
            public double TamIni, Comido, RepCont, RepVuelta, RepOtro;
            public long NRepCont, NVuelta, NFilas;
            public long Fire0, Fire1;
        }

        // ---------------- estado ----------------
        private sealed class Nivel
        {
            public long Id;
            public long T0, TUlt, I0;
            public double TamIni, Ult, Comido, RepC, RepV, CrepOtro0;
            public int NRepC, NVu, NFilas;
            public long Fire0 = -1, Fire1 = -1;
            public Evento Ev0, Ev1;
        }

        private sealed class Costado
        {
            public int Signo;                                   // +1 ASK, -1 BID
            public readonly Dictionary<long, Nivel> Act = new Dictionary<long, Nivel>();
            public long ULim = long.MinValue;                   // hasta donde barrio la muerte
            public long PrevPxTk = -1;                          // punta DESPUES de la fila anterior
            public double PrevSz = -1;
            public double Crep;                                 // reposicion acumulada de ESTE lado

            /// <summary>Los mismos niveles de <see cref="Act"/>, pero nada mas que su u (= signo x
            /// tick), ordenados de MAYOR a MENOR. Existe por una sola razon de costo: el barrido de
            /// muerte se lleva siempre los u mas chicos, o sea la COLA de esta lista, y asi no hay
            /// que recorrer el diccionario entero para encontrarlos.
            ///
            /// POR QUE HIZO FALTA (medido, no de memoria): los niveles que quedan del otro lado del
            /// precio no se borran hasta que el mercado vuelve a pasarles por encima, asi que si
            /// ATAS queda abierto varios dias el diccionario se llena. Con 11 sesiones seguidas de
            /// MNQ sin reiniciar -7.374.727 filas de cinta- los niveles vivos pasaron de 377 a
            /// 6.660 y el barrido paso de mirar 87 entradas por fila de cinta a mirar 201, todas en
            /// el hilo de la cinta. Con la cola ordenada el barrido no mira NINGUNA de mas: corta y
            /// listo. El estado en memoria sigue creciendo igual (1,3 MB a los 11 dias) pero eso no
            /// le hace nada a nadie; lo que no podia crecer era el trabajo por operacion.
            ///
            /// No cambia ni un evento: se emiten exactamente los mismos niveles y en el mismo orden
            /// (u creciente). Verificado con la paridad entera, 34.326 episodios, byte por byte.</summary>
            public readonly List<long> Orden = new List<long>();

            /// <summary>Mete un u nuevo dejando la lista de mayor a menor.</summary>
            public void Meter(long u)
            {
                int lo = 0, hi = Orden.Count;
                while (lo < hi)
                {
                    int m = (lo + hi) >> 1;
                    if (Orden[m] > u) lo = m + 1; else hi = m;
                }
                Orden.Insert(lo, u);
            }
        }

        private readonly Costado _ask = new Costado { Signo = +1 };
        private readonly Costado _bid = new Costado { Signo = -1 };
        private readonly List<Evento> _nuevos = new List<Evento>();
        private long _fila = -1;
        private long _idEpisodio;

        public string Nombre { get { return "nivel"; } }
        public List<Evento> Nuevos { get { return _nuevos; } }
        /// <summary>Cuantas agresiones entraron. Sirve para el cartel de "calentando".</summary>
        public long Filas { get { return _fila + 1; } }

        public void Reiniciar()
        {
            _nuevos.Clear();
            Censo.Clear();
            _fila = -1;
            _idEpisodio = 0;
            foreach (var c in new[] { _ask, _bid })
            {
                foreach (var kv in c.Act)
                {
                    if (kv.Value.Ev0 != null) kv.Value.Ev0.Cerrado = true;
                    if (kv.Value.Ev1 != null) kv.Value.Ev1.Cerrado = true;
                }
                c.Act.Clear();
                c.Orden.Clear();
                c.ULim = long.MinValue;
                c.PrevPxTk = -1;
                c.PrevSz = -1;
                c.Crep = 0;
            }
        }

        /// <summary>Cierra todo lo que quedo vivo. En el arnes se llama al terminar la cinta, para
        /// que el censo tenga los mismos episodios que el Python (que tambien vacia al final).</summary>
        public void Vaciar()
        {
            Cerrar(_ask, _bid, _fila);
            Cerrar(_bid, _ask, _fila);
        }

        private void Cerrar(Costado c, Costado otro, long iAhora)
        {
            if (c.Act.Count == 0) return;
            var claves = new List<long>(c.Act.Keys);
            claves.Sort();
            for (int k = 0; k < claves.Count; k++) Emitir(c, otro, claves[k], c.Act[claves[k]], iAhora);
            c.Act.Clear();
            c.Orden.Clear();
        }

        // ------------------------------------------------------------------ el paso
        public void Agregar(in Orden o)
        {
            _nuevos.Clear();
            _fila++;

            long aAskTk = Tk(o.AAsk), aBidTk = Tk(o.ABid);
            long dAskTk = Tk(o.DAsk), dBidTk = Tk(o.DBid);
            double aAskV = Sz(o.AAskV), aBidV = Sz(o.ABidV);
            double dAskV = Sz(o.DAskV), dBidV = Sz(o.DBidV);

            // 0) la reposicion de LOS DOS lados se acumula ANTES de tocar los niveles: el control
            //    del lado pasivo que se guarda en el censo usa el acumulado que incluye esta fila,
            //    igual que el cumsum del Python.
            _ask.Crep += RepFila(_ask, aAskTk, aAskV);
            _bid.Crep += RepFila(_bid, aBidTk, aBidV);

            // 1..3) LOS DOS lados ven cada fila: sus niveles nacen, mueren y se reponen igual.
            //       Lo que cambia es que solo el lado AGREDIDO suma consumo. Es lo mismo que hace
            //       el Python, que corre el nucleo entero una vez por lado con su bandera agr_*.
            Paso(_ask, _bid, o.Ns, aAskTk, aAskV, dAskTk, dAskV, o.Vol, o.Lado > 0);
            Paso(_bid, _ask, o.Ns, aBidTk, aBidV, dBidTk, dBidV, o.Vol, o.Lado < 0);

            _ask.PrevPxTk = dAskTk; _ask.PrevSz = dAskV;
            _bid.PrevPxTk = dBidTk; _bid.PrevSz = dBidV;
        }

        private static double RepFila(Costado c, long preTk, double preSz)
        {
            if (preTk < 0 || c.PrevPxTk < 0 || preTk != c.PrevPxTk || preSz < 0 || c.PrevSz < 0) return 0;
            double d = preSz - c.PrevSz;
            return d > 0 ? d : 0;
        }

        private void Paso(Costado c, Costado otro, long ns, long preTk, double preSz,
                          long postTk, double postSz, double vol, bool agredido)
        {
            if (preTk < 0 || preSz < 0) return;                        // sin punta valida no se hace nada
            int sg = c.Signo;
            long u = sg * preTk;

            // --- 1a) mueren los niveles que el mercado atraveso por mas de G ticks ---
            //     Son siempre los u mas chicos, o sea la COLA de c.Orden (que va de mayor a menor).
            //     Se emiten en u CRECIENTE, igual que antes: por eso se recorre la cola al reves.
            long uLim = u - MuerteTicks;
            if (uLim > c.ULim)
            {
                var od = c.Orden;
                int cnt = od.Count, muertos = 0;
                while (muertos < cnt && od[cnt - 1 - muertos] < uLim) muertos++;
                for (int k = 0; k < muertos; k++)
                {
                    long tk = sg * od[cnt - 1 - k];
                    Emitir(c, otro, tk, c.Act[tk], _fila);
                    c.Act.Remove(tk);
                }
                if (muertos > 0) od.RemoveRange(cnt - muertos, muertos);
                c.ULim = uLim;
            }

            // --- 1b) se abre o continua el nivel de la punta ---
            Nivel n;
            bool hay = c.Act.TryGetValue(preTk, out n);
            bool sigue = hay && (ns - n.TUlt <= (long)(MuerteSeg * 1e9));
            if (!sigue)
            {
                // Si ya habia nivel en este tick, la clave NO se va del diccionario (se cierra uno
                // y se abre otro en el mismo precio): c.Orden queda igual. Si no habia, entra.
                if (hay) { Emitir(c, otro, preTk, n, _fila); c.Act.Remove(preTk); }
                else c.Meter(u);
                n = new Nivel
                {
                    Id = ++_idEpisodio,
                    T0 = ns,
                    TUlt = ns,
                    I0 = _fila,
                    TamIni = preSz,
                    Ult = 0,
                    CrepOtro0 = otro.Crep
                };
                c.Act[preTk] = n;
                if (u < c.ULim) c.ULim = u;                            // el nivel nuevo queda por
            }                                                          // debajo del barrido: se corre
            else
            {
                double d = preSz - n.Ult;
                if (c.PrevPxTk == preTk && c.PrevSz >= 0)
                {
                    if (d > 0) { n.RepC += d; n.NRepC++; }             // la punta no se movio
                }
                else
                {
                    if (d > 0) { n.RepV += d; n.NVu++; }               // se fue y VOLVIO con tamano nuevo
                }
            }
            n.TUlt = ns;
            n.NFilas++;

            // --- 2) CHEQUEO DE ENCENDIDO, sin una sola cifra de esta fila ---
            if (PrenderAbsorcion && n.Fire0 < 0)
            {
                double umbral = Umbral(ns);
                if (n.Comido >= umbral)
                {
                    n.Fire0 = ns;
                    n.Ev0 = Nacer(Tipos.AbsorcionNivel, ns, n, preTk, sg);
                    _nuevos.Add(n.Ev0);
                }
            }
            if (PrenderIceberg && n.Fire1 < 0)
            {
                if (n.TamIni >= IcebergVisibleMin && n.TamIni >= 1.0 && n.Comido >= IcebergAguante * n.TamIni)
                {
                    n.Fire1 = ns;
                    n.Ev1 = Nacer(Tipos.Iceberg, ns, n, preTk, sg);
                    _nuevos.Add(n.Ev1);
                }
            }

            // --- 3) recien ahora, el consumo de esta fila ---
            double cons = 0;
            if (agredido && vol > 0)
            {
                cons = vol;
                if (postTk != preTk && cons > preSz) cons = preSz;     // se llevo varios precios:
                n.Comido += cons;                                      // solo lo que habia aca
            }
            if (postTk == preTk && postSz >= 0) n.Ult = postSz;        // exacto
            else if (sg * postTk > u) n.Ult = 0;                       // se lo llevaron puesto
            else { double q = preSz - cons; n.Ult = q > 0 ? q : 0; }   // alguien se paro adentro

            Crecer(n);
        }

        private double Umbral(long ns)
        {
            if (!PartirPorFranja) return UmbralRueda;
            return Franja.EnRueda(ns) ? UmbralRueda : UmbralFuera;
        }

        private Evento Nacer(int tipo, long ns, Nivel n, long tk, int sg)
        {
            var e = new Evento
            {
                Tipo = tipo,
                Ns = ns,
                NsInicio = n.T0,
                PrecioTk = tk,
                Precio = tk * Tick,
                Lado = sg,                       // +1 ASK agredido = se comieron COMPRAS = verde
                Tamano = n.Comido,
                TamanoAlEncender = n.Comido,
                VistoInicial = n.TamIni,
                Aguante = n.TamIni > 0 ? n.Comido / n.TamIni : 0,
                Filas = n.NFilas,
                IdEpisodio = n.Id,
                Cerrado = false,
                Revision = 1
            };
            return e;
        }

        private static void Crecer(Nivel n)
        {
            if (n.Ev0 != null && n.Ev0.Tamano != n.Comido) Actualizar(n.Ev0, n);
            if (n.Ev1 != null && n.Ev1.Tamano != n.Comido) Actualizar(n.Ev1, n);
        }

        private static void Actualizar(Evento e, Nivel n)
        {
            e.Tamano = n.Comido;
            e.Aguante = n.TamIni > 0 ? n.Comido / n.TamIni : 0;
            e.Filas = n.NFilas;
            e.Revision++;
        }

        private void Emitir(Costado c, Costado otro, long tk, Nivel n, long iAhora)
        {
            if (n.Ev0 != null) { n.Ev0.Cerrado = true; n.Ev0.Revision++; }
            if (n.Ev1 != null) { n.Ev1.Cerrado = true; n.Ev1.Revision++; }
            if (PisoCenso < 0 || n.Comido < PisoCenso) return;
            long filas = iAhora - n.I0 + 1; if (filas < 1) filas = 1;
            Censo.Add(new Episodio
            {
                LadoAbs = c.Signo,
                TIni = n.T0,
                TFin = n.TUlt,
                IIni = n.I0,
                IFin = iAhora,
                PxTk = tk,
                TamIni = n.TamIni,
                Comido = n.Comido,
                RepCont = n.RepC,
                RepVuelta = n.RepV,
                RepOtro = (otro.Crep - n.CrepOtro0) * n.NFilas / filas,
                NRepCont = n.NRepC,
                NVuelta = n.NVu,
                NFilas = n.NFilas,
                Fire0 = n.Fire0,
                Fire1 = n.Fire1
            });
        }

        private long Tk(double p)
        {
            if (double.IsNaN(p) || double.IsInfinity(p) || p <= 0) return -1;
            return (long)Math.Round(p / Tick, MidpointRounding.ToEven);
        }

        private static double Sz(double v)
        {
            return (double.IsNaN(v) || double.IsInfinity(v)) ? -1.0 : v;
        }
    }

    // =====================================================================================
    // NUCLEO 2: AGRESOR ATRAPADO  ->  marca "agresor_atrapado" (APAGADA de fabrica)
    // =====================================================================================
    /// <summary>
    /// EL BARRIDO QUE SALIO MAL. Una racha de agresion de un solo lado se lleva varios niveles por
    /// delante y corre el precio a su favor; y enseguida el precio vuelve al punto de partida.
    ///
    /// ARRANCA APAGADA, por tres razones medidas y ninguna de gusto:
    ///   1. La marca no sale cuando pasa el barrido sino cuando el precio VUELVE: demora mediana
    ///      3 s, p90 20 s, p99 29 s. En vivo aparece tarde y eso hay que verlo antes de confiar.
    ///   2. El informe que la proponia publicaba "t 2,30, 70 % de sesiones" y esos numeros eran del
    ///      BRUTO: el neto tiene t 0,77 y 47,8 % en su propio archivo (r10_f6_04_salida.txt). En el
    ///      contrato que se opera hoy (MNQZ6, 6 sesiones) el bruto es -0,606.
    ///   3. Duplica la densidad en pantalla: de 36 a 50 marcas por sesion.
    /// Lo unico que aguanta de ella es descriptivo: el 83,1 % de los barridos que califican vuelven
    /// al punto de partida en menos de 60 s, contra el 74,4 % desde un momento cualquiera con el
    /// mismo movimiento previo. O sea que es casi la regla, no una rareza.
    ///
    /// Misma definicion, paso por paso, que r10_f6_lib.py:barridos() + disparar().
    /// </summary>
    public sealed class NucleoAtrapado : IFuenteDeEventos
    {
        public double Tick = 0.25;
        /// <summary>W: hueco maximo entre agresiones para que la racha siga siendo una sola.</summary>
        public double HuecoSeg = 2.0;
        /// <summary>Vs: contratos que tiene que juntar la racha.</summary>
        public double VolMin = 300;
        /// <summary>M: ticks que tiene que correr el precio a favor del agresor.</summary>
        public double TicksMin = 16;
        /// <summary>D: segundos como maximo para que el precio vuelva.</summary>
        public int EsperaSeg = 30;
        public bool Prendido = false;

        private const long NS = 1000000000L;
        private const int RING = 128;

        private struct Pendiente
        {
            public long SecFin; public int Lado; public double MidAntes, Extremo, Vol, Ticks; public long TIni;
        }

        // racha en curso
        private bool _hay;
        private int _lado;
        private long _tIni, _tFin;
        private double _vol, _midAntes, _ext;
        private bool _sana;

        // serie de 1 segundo (el mismo medio que arma r10_f6_lib.segundos_de_cinta)
        private double _midUlt = double.NaN;
        private long _secUlt = long.MinValue;
        private readonly double[] _mid = new double[RING];
        private readonly long[] _secDe = new long[RING];
        private bool _huboBueno;

        private readonly List<Pendiente> _pend = new List<Pendiente>();
        private readonly List<Evento> _nuevos = new List<Evento>();

        public string Nombre { get { return "atrapado"; } }
        public List<Evento> Nuevos { get { return _nuevos; } }

        public void Reiniciar()
        {
            _nuevos.Clear(); _pend.Clear();
            _hay = false; _lado = 0; _vol = 0; _ext = 0; _midAntes = double.NaN; _sana = false;
            _midUlt = double.NaN; _secUlt = long.MinValue; _huboBueno = false;
            for (int i = 0; i < RING; i++) { _mid[i] = double.NaN; _secDe[i] = long.MinValue; }
        }

        public void Agregar(in Orden o)
        {
            _nuevos.Clear();

            // a) la racha se corta con esta fila? (cambio de lado o hueco mayor que W)
            bool corta = !_hay || o.Lado != _lado || (o.Ns - _tFin) > (long)(HuecoSeg * NS);
            if (corta && _hay) Armar();

            // b) se cierran los segundos que ya no pueden cambiar, y se miran los pendientes
            long sec = Piso(o.Ns);
            long limite = (o.Ns % NS == 0) ? sec - 1 : sec;
            if (_secUlt == long.MinValue) _secUlt = limite - 1;
            while (_secUlt < limite)
            {
                _secUlt++;
                Guardar(_secUlt, _midUlt);
                Revisar(_secUlt, _midUlt);
            }

            // c) el medio de esta fila entra en la serie
            double db = o.DBid, da = o.DAsk;
            if (db > 0 && da > db && (da - db) < 20 && !double.IsNaN(db) && !double.IsNaN(da))
            {
                _midUlt = (db + da) / 2.0;
                if (!_huboBueno) { _huboBueno = true; Rellenar(_midUlt); }
            }

            // d) la fila se suma a la racha
            if (corta)
            {
                _hay = true; _lado = o.Lado; _tIni = o.Ns; _vol = 0; _ext = double.NaN;
                _midAntes = (o.ABid + o.AAsk) / 2.0;
                _sana = o.AAsk > o.ABid && o.ABid > 0 && (o.AAsk - o.ABid) < 20;
            }
            _vol += o.Vol;
            if (double.IsNaN(_ext)) _ext = o.Ultimo;
            else if (_lado > 0) { if (o.Ultimo > _ext) _ext = o.Ultimo; }
            else { if (o.Ultimo < _ext) _ext = o.Ultimo; }
            _tFin = o.Ns;
        }

        private void Armar()
        {
            _hay = false;
            if (!_sana || double.IsNaN(_midAntes) || double.IsNaN(_ext)) return;
            double ticks = _lado * (_ext - _midAntes) / Tick;
            if (_vol < VolMin || ticks < TicksMin) return;
            if (!Prendido) return;
            _pend.Add(new Pendiente
            {
                SecFin = Piso(_tFin),
                Lado = _lado,
                MidAntes = _midAntes,
                Extremo = _ext,
                Vol = _vol,
                Ticks = ticks,
                TIni = _tIni
            });
        }

        private void Revisar(long sec, double mid)
        {
            if (_pend.Count == 0) return;
            for (int i = _pend.Count - 1; i >= 0; i--)
            {
                var p = _pend[i];
                if (sec <= p.SecFin) continue;
                if (sec > p.SecFin + EsperaSeg) { _pend.RemoveAt(i); continue; }
                if (double.IsNaN(mid)) continue;
                if (p.Lado * (mid - p.MidAntes) > 0) continue;
                long tk = (long)Math.Round(p.Extremo / Tick, MidpointRounding.ToEven);
                _nuevos.Add(new Evento
                {
                    Tipo = Tipos.AgresorAtrapado,
                    Ns = sec * NS,
                    NsInicio = p.TIni,
                    PrecioTk = tk,
                    Precio = tk * Tick,
                    Lado = p.Lado,                     // barrio comprando -> se comieron compras -> verde
                    Tamano = p.Vol,
                    TamanoAlEncender = p.Vol,
                    VistoInicial = 0,
                    Aguante = 0,
                    Filas = 0,
                    Ticks = p.Ticks,
                    EsperaSeg = sec - p.SecFin,
                    Cerrado = true,
                    Revision = 1
                });
                _pend.RemoveAt(i);
            }
        }

        private void Guardar(long sec, double mid)
        {
            int k = (int)(((sec % RING) + RING) % RING);
            _mid[k] = mid; _secDe[k] = sec;
        }

        private void Rellenar(double v)
        {
            for (int i = 0; i < RING; i++) if (double.IsNaN(_mid[i]) && _secDe[i] != long.MinValue) _mid[i] = v;
        }

        private static long Piso(long ns)
        {
            long s = ns / NS;
            if (ns < 0 && ns % NS != 0) s--;
            return s;
        }
    }

    // =====================================================================================
    // Utilidades compartidas por el indicador y por el arnes
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
        /// r10_lib.segmento().</summary>
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
