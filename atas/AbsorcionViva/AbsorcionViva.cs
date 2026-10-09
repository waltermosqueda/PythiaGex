using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace AbsorcionViva
{
    /// <summary>
    /// ABSORCION VIVA 1.0 - EN PRUEBA, NO VALIDADO.
    ///
    /// QUE ES. Lo mismo que el Big Trades de ATAS -pelotita llena y semitransparente apoyada en el
    /// PRECIO EXACTO, encima de las velas, del tamano de lo que paso- pero para ABSORCION: marca
    /// el precio donde el mercado se esta comiendo agresion SIN moverse.
    ///   * VERDE  = se comieron COMPRAS agresoras. Hay alguien vendiendo en limite y reponiendo.
    ///   * ROJO   = se comieron VENTAS agresoras.
    /// OJO CON ESTO: en el Big Trades que el operador ya tiene puesto, verde tambien es "hubo
    /// compra", pero ahi la compra se salio con la suya y aca la compra PERDIO. Son dos pelotitas
    /// del mismo color con la lectura al reves. Hay un ajuste para dar vuelta los colores.
    ///
    /// DE DONDE SALE EL DATO. De OnCumulativeTrade: la misma via por la que el Big Trades oficial
    /// recibe las operaciones y la misma con la que la sonda grabo las 37 sesiones de MNQ. NO se
    /// suscribe a profundidad ni a MarketByOrder -la punta del libro viene adentro del propio
    /// CumulativeTrade-, asi que no puede repetir el corte de Rithmic del 16-09.
    ///
    /// NO LEE HISTORIA AL ARRANCAR, a proposito. Para absorcion hace falta la cinta entera
    /// (MinVolume=1): son 743.535 ordenes en una rueda de MNQ, y esta medido que ATAS, para una
    /// fecha pasada, devuelve la SESION COMPLETA aunque se le pida media hora (414.184 ordenes por
    /// tramo, FlujoClaroSonda.cs:164). Pedir eso al abrir el grafico es colgar la plataforma. El
    /// indicador arranca vacio y se llena hacia adelante; el rotulo dice "calentando" mientras
    /// tanto.
    ///
    /// QUE NO ES. No es una senal y no dice para donde va el precio. La ronda 10 midio tres
    /// familias con el juez de la ronda 9 (entrada el segundo siguiente cruzando el spread real,
    /// costo verificado 0,948 puntos la vuelta) y dio 0 de 24 combinaciones con neto positivo, la
    /// mejor -0,154. Y el nivel tampoco aguanta: 0 de 18 variantes cruzan menos que un nivel al
    /// azar a la misma distancia a 5 minutos. Esto DESCRIBE lo que esta pasando en el libro.
    ///
    /// COMO DECIDE. En AbsorcionNucleo.cs, que no conoce ATAS. Ese mismo archivo se compila en
    /// arnes/ y se corre sobre la cinta grabada: 34.326 episodios comparados contra el Python que
    /// produjo las mediciones, en 3 sesiones (una de la reserva), 100,000 % de coincidencia en las
    /// 16 columnas (laboratorio/dom/ronda10_absorcion/paridad/r10_p_01_salida.txt).
    ///
    /// PARA EL DELTA (lo que el operador pidio despues): esta clase solo sabe de Evento {hora,
    /// precio, tamano, lado, tipo}. Para hacer lo mismo con el delta alcanza con otro nucleo que
    /// implemente IFuenteDeEventos y devuelva Tipo 10, 11...; el dibujo no se toca. Es una ronda
    /// aparte y NO se hace ahora.
    ///
    /// LOS AJUSTES LLEVAN EL PREFIJO "Abv" A PROPOSITO: ATAS guarda los ajustes POR NOMBRE de
    /// propiedad en el workspace (.ws). Un nombre repetido se come el valor guardado de otro
    /// indicador, y para pisar un valor ya guardado no alcanza con cambiar el default en el
    /// codigo: hay que RENOMBRAR la propiedad (la trampa del 16-09).
    /// </summary>
    [DisplayName("Absorcion Viva (EN PRUEBA)")]
    [Category("PythiaGex 2.0")]
    public class AbsorcionVivaIndicador : Indicator
    {
        public const string Version = "1.0";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public enum QueLado { Los_dos, Solo_compras_comidas, Solo_ventas_comidas }
        public enum DondeEnLaVela { En_el_instante_exacto, En_el_centro_de_la_vela }
        public enum CuandoElNumero { Nunca, Solo_las_grandes, Siempre }

        // =================================================================================
        // 1. QUE SE MARCA
        // =================================================================================
        [Display(Name = "Pelotita: absorcion en el nivel", GroupName = "1. Que se marca", Order = 10,
            Description = "El precio se comio una pila de contratos agresores sin dejar pasar al mercado. MEDIDO con este mismo codigo sobre 21 cintas enteras de MNQ (medicion/r10_p_03_salida.txt): mediana 21 pelotitas por sesion, 24 marcas contando los aros; 2,15 marcas por hora en la rueda y 0,67 fuera.")]
        public bool AbvVerAbsorcion { get; set; } = true;

        [Display(Name = "Contratos para marcar EN LA RUEDA (09:30-16:00 NY)", GroupName = "1. Que se marca", Order = 20,
            Description = "Para comparar umbrales, censo de MNQ (r10_dib_01_salida.txt): 150 deja 2,46 marcas por hora en la rueda, 175 deja 1,38 y 200 deja 0,46. OJO: ese censo cuenta por el tamano FINAL del nivel y sale ~10 % mas alto que lo que el indicador puede encender de verdad; el numero que se ve en pantalla con 150 es 2,15 por hora. En MES el mismo numero saca 10 a 20 veces mas marcas: hay que subirlo.")]
        [Range(1, 100000)]
        public int AbvUmbralRueda { get; set; } = 150;

        [Display(Name = "Contratos para marcar FUERA de la rueda", GroupName = "1. Que se marca", Order = 30,
            Description = "De noche el libro es flaco y el mismo numero no significa lo mismo. Para comparar umbrales, censo de MNQ (r10_dib_01_salida.txt): 90 deja 1,15 marcas por hora fuera de la rueda, 80 deja 1,76 y 100 deja 0,73. Lo que se ve en pantalla con 90 es 0,67 por hora (medicion/r10_p_03_salida.txt).")]
        [Range(1, 100000)]
        public int AbvUmbralFuera { get; set; } = 90;

        [Display(Name = "Usar los dos umbrales (dia y noche)", GroupName = "1. Que se marca", Order = 40,
            Description = "Apagado: se usa siempre el de la rueda. Con UN solo numero (100) salen 17,85 marcas por hora de dia y 0,73 de noche: una sopa y un desierto.")]
        public bool AbvPartirDiaNoche { get; set; } = true;

        [Display(Name = "Aro: iceberg (mostraba poco y se comio mucho)", GroupName = "1. Que se marca", Order = 50,
            Description = "El nivel mostraba tres o cuatro contratos y termino comiendose cuarenta veces eso. Mediana medida: 4 por sesion. El 54,7 % cae en el mismo nivel que una pelotita, y ahi se dibuja el aro ALREDEDOR de la pelotita, no aparte.")]
        public bool AbvVerIceberg { get; set; } = true;

        [Display(Name = "Veces que se repuso, para el aro", GroupName = "1. Que se marca", Order = 60,
            Description = "MEDIDO: con 20 salen 107 marcas por sesion (ilegible), con 30 salen 19, con 40 salen 4.")]
        [Range(2, 10000)]
        public int AbvIcebergAguante { get; set; } = 40;

        [Display(Name = "Contratos que el nivel tiene que MOSTRAR para el aro", GroupName = "1. Que se marca", Order = 70,
            Description = "Con 1 contrato visible el cociente se dispara por nada. Con 5 en vez de 3 queda 1 marca por sesion, que es demasiado poco.")]
        [Range(1, 1000)]
        public int AbvIcebergVisible { get; set; } = 3;

        [Display(Name = "Circulo punteado: agresor atrapado (SALE TARDE)", GroupName = "1. Que se marca", Order = 80,
            Description = "APAGADO de fabrica, pero SE SIGUE CALCULANDO Y ANOTANDO en el CSV: esto solo apaga el dibujo. La marca no sale cuando pasa el barrido sino cuando el precio VUELVE: mediana 3 s, p90 20 s, p99 29 s. Ademas duplica la densidad y lo unico que tiene medido es descriptivo: el 83,1 % de estos barridos vuelven en menos de 60 s, contra el 74,4 % desde un momento cualquiera.")]
        public bool AbvVerAtrapado { get; set; } = false;

        [Display(Name = "Contratos del barrido (atrapado)", GroupName = "1. Que se marca", Order = 90)]
        [Range(1, 100000)]
        public int AbvAtrapadoContratos { get; set; } = 300;

        [Display(Name = "Ticks que corrio el barrido (atrapado)", GroupName = "1. Que se marca", Order = 100)]
        [Range(1, 1000)]
        public int AbvAtrapadoTicks { get; set; } = 16;

        [Display(Name = "Segundos como maximo para que vuelva (atrapado)", GroupName = "1. Que se marca", Order = 110)]
        [Range(1, 300)]
        public int AbvAtrapadoVuelta { get; set; } = 30;

        [Display(Name = "Que lado se marca", GroupName = "1. Que se marca", Order = 120,
            Description = "Solo limita el DIBUJO. El registro en CSV guarda los dos lados igual.")]
        public QueLado AbvLado { get; set; } = QueLado.Los_dos;

        // =================================================================================
        // 2. LA PELOTITA
        // =================================================================================
        [Display(Name = "VERDE: se comieron COMPRAS agresoras (alguien vende en limite)", GroupName = "2. La pelotita", Order = 10,
            Description = "El nombre es largo a proposito: en el Big Trades que ya tenes puesto el verde quiere decir lo contrario (una compra que gano). Si te resulta mas comodo al reves, dalo vuelta con el ajuste de abajo.")]
        public System.Windows.Media.Color AbvColorComprasComidas { get; set; }
            = System.Windows.Media.Color.FromRgb(42, 190, 130);

        [Display(Name = "ROJO: se comieron VENTAS agresoras (alguien compra en limite)", GroupName = "2. La pelotita", Order = 20)]
        public System.Windows.Media.Color AbvColorVentasComidas { get; set; }
            = System.Windows.Media.Color.FromRgb(228, 72, 72);

        [Display(Name = "Dar vuelta los colores", GroupName = "2. La pelotita", Order = 30,
            Description = "Buena parte del mundo pinta la absorcion por el lado PASIVO, que es el que gana. Con esto la pelotita queda de ese color sin tocar nada mas.")]
        public bool AbvInvertirColores { get; set; } = false;

        [Display(Name = "Opacidad (%)", GroupName = "2. La pelotita", Order = 40,
            Description = "60 % es lo que se ve en la captura de referencia: la vela se ve por abajo.")]
        [Range(5, 100)]
        public int AbvOpacidad { get; set; } = 60;

        [Display(Name = "Diametro minimo (px)", GroupName = "2. La pelotita", Order = 50)]
        [Range(2, 200)]
        public int AbvDiametroMin { get; set; } = 10;

        [Display(Name = "Diametro maximo (px)", GroupName = "2. La pelotita", Order = 60,
            Description = "MEDIDO sobre la captura del operador: sus pelotitas del Big Trades miden entre 18 y 22 px. Con esta escala la marca mediana (153 contratos, medicion/r10_p_03_salida.txt) cae en 17 px.")]
        [Range(2, 200)]
        public int AbvDiametroMax { get; set; } = 26;

        [Display(Name = "Contratos que valen el diametro minimo", GroupName = "2. La pelotita", Order = 70)]
        [Range(1, 100000)]
        public int AbvContratosDiametroMin { get; set; } = 75;

        [Display(Name = "Contratos que valen el diametro maximo", GroupName = "2. La pelotita", Order = 80,
            Description = "Entre los dos crece derecho (lineal), NO con la raiz del area: el rango real dibujado son 3,3 veces en contratos, y con raiz cuadrada quedarian 1,8 veces de diametro, que a ojo no se distingue.")]
        [Range(1, 100000)]
        public int AbvContratosDiametroMax { get; set; } = 250;

        [Display(Name = "Todas del mismo tamano", GroupName = "2. La pelotita", Order = 90)]
        public bool AbvTodasIgual { get; set; } = false;

        [Display(Name = "Borde alrededor de la pelotita", GroupName = "2. La pelotita", Order = 100)]
        public bool AbvBorde { get; set; } = false;

        [Display(Name = "Grosor del borde (px)", GroupName = "2. La pelotita", Order = 110)]
        [Range(0.5, 6.0)]
        public double AbvGrosorBorde { get; set; } = 1.2;

        [Display(Name = "Grosor del aro de iceberg (px)", GroupName = "2. La pelotita", Order = 120)]
        [Range(0.5, 6.0)]
        public double AbvGrosorAro { get; set; } = 2.0;

        [Display(Name = "Contratos del diametro minimo del atrapado", GroupName = "2. La pelotita", Order = 130,
            Description = "El barrido es mucho mas grande que la absorcion (mediana 416 contratos contra 154), asi que lleva su propia escala o se clavan todos en el maximo.")]
        [Range(1, 100000)]
        public int AbvAtrapadoDiametroDesde { get; set; } = 250;

        [Display(Name = "Contratos del diametro maximo del atrapado", GroupName = "2. La pelotita", Order = 140)]
        [Range(1, 100000)]
        public int AbvAtrapadoDiametroHasta { get; set; } = 800;

        // =================================================================================
        // 3. EL NUMERO AL LADO
        // =================================================================================
        [Display(Name = "Cuando se muestra el numero de contratos", GroupName = "3. El numero", Order = 10,
            Description = "De fabrica solo en las grandes: son el 12,8 %, unas 4 o 5 por sesion. Justo cuando la pelotita ya se clavo en el diametro maximo y el tamano dejo de informar. El aro de iceberg muestra su 'x46' salvo que aca pongas 'Nunca', que apaga todos los numeros.")]
        public CuandoElNumero AbvVerNumero { get; set; } = CuandoElNumero.Solo_las_grandes;

        [Display(Name = "A partir de cuantos contratos es 'grande'", GroupName = "3. El numero", Order = 20)]
        [Range(1, 100000)]
        public int AbvNumeroDesde { get; set; } = 200;

        [Display(Name = "Tamano de la letra", GroupName = "3. El numero", Order = 30)]
        [Range(5.0, 20.0)]
        public double AbvLetra { get; set; } = 9.0;

        // =================================================================================
        // 4. DONDE SE UBICA
        // =================================================================================
        [Display(Name = "Donde cae dentro de la vela", GroupName = "4. Donde se ubica", Order = 10,
            Description = "La altura SIEMPRE es el precio exacto del nivel. Esto es solo el corrimiento horizontal: en el instante exacto dentro de la vela, o al medio. En 1 minuto casi no se nota; en 30 minutos es la diferencia entre ver el momento y no verlo.")]
        public DondeEnLaVela AbvDondeEnLaVela { get; set; } = DondeEnLaVela.En_el_instante_exacto;

        [Display(Name = "Juntar las marcas a N ticks o menos (0 = no juntar)", GroupName = "4. Donde se ubica", Order = 20,
            Description = "Dos marcas del mismo tipo y lado, en la misma vela y a 2 ticks o menos, se dibujan como UNA sumando los contratos. NUNCA se corre una marca de su precio: eso romperia lo unico que no se negocia. MEDIDO con este codigo sobre 21 cintas (medicion/r10_p_03_salida.txt): de 725 marcas, la fusion se come 33 en M1 (4,6 %), 45 en M5 (6,2 %) y 60 en M30 (8,3 %).")]
        [Range(0, 100)]
        public int AbvJuntarTicks { get; set; } = 2;

        [Display(Name = "Maximo de marcas por vela", GroupName = "4. Donde se ubica", Order = 30,
            Description = "Se quedan las MAS GRANDES y el rotulo avisa cuantas se escondieron. MEDIDO con este codigo sobre 21 cintas (medicion/r10_p_03_salida.txt): el maximo en una vela es 5 en M1, 8 en M5 y 13 en M30, y con el tope en 6 quedan tapadas el 0,0 % de las marcas en M1, el 0,3 % en M5 y el 6,0 % en M30.")]
        [Range(1, 200)]
        public int AbvTopePorVela { get; set; } = 6;

        [Display(Name = "Dibujar encima de las velas", GroupName = "4. Donde se ubica", Order = 40,
            Description = "Apagado: las marcas quedan detras de las velas.")]
        public bool AbvEncimaDeLasVelas
        {
            get { return DrawAbovePrice; }
            set { DrawAbovePrice = value; }
        }

        [Display(Name = "Margen del eje de precios (px)", GroupName = "4. Donde se ubica", Order = 50,
            Description = "El eje de precios se dibuja ENCIMA del area del grafico: sin este margen la marca mas nueva queda cortada por el eje.")]
        [Range(0, 200)]
        public int AbvMargenEje { get; set; } = 62;

        [Display(Name = "Velas hacia atras que se dibujan", GroupName = "4. Donde se ubica", Order = 60)]
        [Range(10, 20000)]
        public int AbvVelasAtras { get; set; } = 1000;

        // =================================================================================
        // 5. EL ROTULO
        // =================================================================================
        [Display(Name = "Ver el rotulo entero", GroupName = "5. El rotulo", Order = 10,
            Description = "La primera linea (EN PRUEBA: no validado) no se puede apagar.")]
        public bool AbvVerRotulo { get; set; } = true;

        // =================================================================================
        // 6. EL REGISTRO
        // =================================================================================
        [Display(Name = "Anotar todos los eventos en un CSV", GroupName = "6. El registro", Order = 10,
            Description = @"%APPDATA%\ATAS\PythiaGex2\absorcion\absorcion-<fecha UTC>.csv. Guarda TODOS los eventos, se dibujen o no, con una fila cuando nace y otra cuando el nivel muere. Es lo que despues se mide con el juez.")]
        public bool AbvRegistrar { get; set; } = true;

        [Display(Name = "Carpeta (vacio = la de PythiaGex 2.0)", GroupName = "6. El registro", Order = 20)]
        public string AbvCarpeta { get; set; } = "";

        // =================================================================================
        // Estado
        // =================================================================================
        private sealed class Ficha
        {
            public Evento Ev;
            public bool Anotado, AnotadoCierre;
        }

        /// <summary>Lo que el dibujo necesita de un evento, copiado bajo llave para no leer campos
        /// que el hilo de la cinta esta escribiendo.</summary>
        private struct Foto
        {
            public int Tipo, Lado;
            public long Ns, PrecioTk, IdEpisodio;
            public double Precio, Tamano, Aguante;
        }

        private readonly NucleoNivel _nivel = new NucleoNivel();
        private readonly NucleoAtrapado _atrap = new NucleoAtrapado();
        private readonly Bateria _bateria = new Bateria();
        private readonly object _llave = new object();
        private readonly List<Ficha> _fichas = new List<Ficha>();
        private readonly List<Foto> _fotos = new List<Foto>();

        private CumulativeTrade _actual;
        private TimeSpan _periodo;
        private Action _latido;
        private bool _sucio;
        private double _tick;
        private long _ordenes;
        private long _nsPrimera, _nsUltima;
        private int _hoy0, _hoy1, _hoy2;
        private string _diaContadores = "";
        private string _diaRegistro = "";
        private int _tapadas;
        private string _estado = "arrancando: todavia no llego ninguna operacion";

        /// <summary>Segundos de cinta antes de dejar de decir "calentando". Son dos veces la vida
        /// maxima de un nivel: recien ahi cualquier episodio que se vea nacio entero adentro de
        /// esta corrida. Las marcas de antes igual son validas (nunca sobrestiman: solo cuentan lo
        /// que este indicador vio), lo que puede faltar es alguna.</summary>
        private const double SegCalentando = 120;

        private static readonly Color ColTexto = Color.FromArgb(226, 232, 240);
        private static readonly Color ColFondo = Color.FromArgb(11, 16, 23);
        private static readonly Color ColAviso = Color.FromArgb(250, 176, 60);

        // =================================================================================
        // Ciclo de vida
        // =================================================================================
        public AbsorcionVivaIndicador() : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
            DrawAbovePrice = true;
            if (DataSeries.Count > 0 && DataSeries[0] is ValueDataSeries v)
            {
                v.IsHidden = true;
                v.VisualType = VisualMode.Hide;
                v.ShowCurrentValue = false;
            }
            _bateria.Agregar(_nivel);
            _bateria.Agregar(_atrap);
        }

        protected override void OnInitialize()
        {
            try
            {
                var ps = DataProvider != null ? DataProvider.Panels : null;
                if (ps != null && ps.Count > 0) Panel = ps.Contains("Chart") ? "Chart" : ps[0];
            }
            catch (Exception e) { Anotar(e); }

            _periodo = TimeSpan.FromMilliseconds(250);
            _latido = Latido;
            SubscribeToTimer(_periodo, _latido);
            Log("absorcion: arrancado, version " + Version);
        }

        protected override void OnDispose()
        {
            try { if (_latido != null) UnsubscribeFromTimer(_periodo, _latido); } catch { }
            base.OnDispose();
        }

        /// <summary>Una sola linea a proposito: ATAS llama a OnCalculate por CADA vela en cada
        /// recalculo, y ahi adentro se cuelga la plataforma (memoria indicador-que-cuelga-atas).
        /// La vela de cada marca se resuelve en el dibujo, buscando por hora entre las visibles:
        /// asi anda igual en M1, M5, M30, ticks, rango y footprint, y sobrevive a que ATAS cargue
        /// mas historia y corra los indices.</summary>
        protected override void OnCalculate(int bar, decimal value) { }

        // =================================================================================
        // La cinta en vivo
        // =================================================================================
        // Un CumulativeTrade se sigue actualizando mientras la agresion barre precios: recien
        // cuando llega el SIGUIENTE se sabe que el anterior termino, con su NewBid/NewAsk final.
        // Es la misma convencion con la que la sonda grabo las 37 sesiones (FlujoClaroSonda.cs),
        // asi que la cinta viva y la grabada son la misma cosa.
        protected override void OnCumulativeTrade(CumulativeTrade trade)
        {
            try
            {
                if (trade == null) return;
                CumulativeTrade cerrado = null;
                lock (_llave)
                {
                    if (_actual != null && !ReferenceEquals(_actual, trade)) cerrado = _actual;
                    _actual = trade;
                }
                if (cerrado != null) Alimentar(cerrado);
            }
            catch (Exception e) { Anotar(e); }
        }

        protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
        {
            try { if (trade != null) lock (_llave) _actual = trade; } catch { }
        }

        /// <summary>Aca adentro NO puede haber archivos, ni LINQ, ni repintados: en MNQ llegan
        /// hasta 426 ordenes por segundo. El repintado va desacoplado, en el latido de 250 ms.</summary>
        private void Alimentar(CumulativeTrade t)
        {
            var o = new Orden();
            o.Ns = Reloj.Ns(t.Time);                  // CumulativeTrade.Time YA es UTC: no convertir
            o.Primero = (double)t.FirstPrice;
            o.Ultimo = (double)t.Lastprice;
            o.Vol = (double)t.Volume;
            // 'TradeDirection' existe en ATAS.Indicators Y en ATAS.DataFeedsCore: hay que decir cual
            o.Lado = t.Direction == ATAS.Indicators.TradeDirection.Buy ? 1
                   : t.Direction == ATAS.Indicators.TradeDirection.Sell ? -1 : 0;
            o.Prints = t.Ticks != null ? t.Ticks.Count : 0;
            Punta(t.PreviousBid, ref o.ABid, ref o.ABidV);
            Punta(t.PreviousAsk, ref o.AAsk, ref o.AAskV);
            Punta(t.NewBid, ref o.DBid, ref o.DBidV);
            Punta(t.NewAsk, ref o.DAsk, ref o.DAskV);

            lock (_llave)
            {
                if (_ordenes == 0) _nsPrimera = o.Ns;
                _nsUltima = o.Ns;
                _ordenes++;
                _bateria.Agregar(in o);
                var nv = _bateria.Nuevos;
                for (int i = 0; i < nv.Count; i++)
                {
                    _fichas.Add(new Ficha { Ev = nv[i] });
                    if (nv[i].Tipo == Tipos.AbsorcionNivel) _hoy0++;
                    else if (nv[i].Tipo == Tipos.Iceberg) _hoy1++;
                    else _hoy2++;
                    _sucio = true;
                }
                if (_fichas.Count > 6000) _fichas.RemoveRange(0, 2000);
            }
        }

        private static void Punta(MarketDataArg q, ref double px, ref double vol)
        {
            if (q == null) { px = double.NaN; vol = double.NaN; return; }
            px = (double)q.Price;
            vol = (double)q.Volume;
        }

        // =================================================================================
        // El latido: 250 ms. Repinta UNA vez y solo si hay algo nuevo.
        // =================================================================================
        private void Latido()
        {
            try
            {
                // El tick SOLO se cambia con un valor que la plataforma dio de verdad. Si
                // InstrumentInfo parpadea en null un latido, antes se caia al 0,25 de fabrica y en
                // un instrumento de otro tick eso vaciaba el nucleo entero por nada.
                double tk = 0;
                try { if (InstrumentInfo != null) tk = (double)InstrumentInfo.TickSize; } catch { }
                if (tk <= 0) tk = _tick > 0 ? _tick : 0.25;

                bool reiniciar = false;
                lock (_llave)
                {
                    if (_tick != tk)
                    {
                        _tick = tk;
                        _nivel.Tick = tk;
                        _atrap.Tick = tk;
                        reiniciar = _ordenes > 0;
                    }
                    Ajustes();
                    if (reiniciar)
                    {
                        _bateria.Reiniciar();
                        _fichas.Clear();
                        _actual = null;
                        _ordenes = 0; _hoy0 = _hoy1 = _hoy2 = 0;
                        _sucio = true;
                        Log("absorcion: cambio el tick a " + tk.ToString(Inv) + ", se reinicio el nucleo");
                    }
                    Estado();
                }
                if (AbvRegistrar) Registrar();
                bool pintar;
                lock (_llave) { pintar = _sucio; _sucio = false; }
                if (pintar) try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            }
            catch (Exception e) { Anotar(e); }
        }

        /// <summary>Los ajustes de la pantalla se copian al nucleo. Se llama con la llave tomada.</summary>
        private void Ajustes()
        {
            _nivel.UmbralRueda = AbvUmbralRueda;
            _nivel.UmbralFuera = AbvPartirDiaNoche ? AbvUmbralFuera : AbvUmbralRueda;
            _nivel.PartirPorFranja = AbvPartirDiaNoche;
            _nivel.IcebergAguante = AbvIcebergAguante;
            _nivel.IcebergVisibleMin = AbvIcebergVisible;
            _nivel.PrenderIceberg = true;                 // se registra siempre; el dibujo se filtra
            _nivel.PrenderAbsorcion = true;
            _atrap.VolMin = AbvAtrapadoContratos;
            _atrap.TicksMin = AbvAtrapadoTicks;
            _atrap.EsperaSeg = AbvAtrapadoVuelta;
            // Los TRES detectores calculan SIEMPRE, este apagado o no lo que se dibuja: el CSV
            // tiene que guardar todos los eventos para que la prueba hacia adelante no dependa de
            // lo que el operador tenia prendido ese dia. Lo que apagan los ajustes es el DIBUJO.
            _atrap.Prendido = true;
        }

        private void Estado()
        {
            string hoy = DateTime.UtcNow.ToString("yyyy-MM-dd", Inv);
            if (_diaContadores != hoy) { _diaContadores = hoy; _hoy0 = _hoy1 = _hoy2 = 0; }

            if (_ordenes == 0)
            {
                _estado = "sin cinta: el conector todavia no mando ninguna operacion";
                return;
            }
            double seg = (_nsUltima - _nsPrimera) / 1e9;
            if (seg < SegCalentando)
                _estado = string.Format(Inv,
                    "CALENTANDO: {0:0} s de cinta de {1:0} ({2:n0} ordenes). Lo que se ve ya es valido; puede faltar alguna marca.",
                    seg, SegCalentando, _ordenes);
            else
                _estado = string.Format(Inv, "cinta viva: {0:n0} ordenes, {1:0} min. Arranco vacio: no lee historia.",
                    _ordenes, seg / 60.0);
        }

        // =================================================================================
        // El registro: TODOS los eventos, se dibujen o no
        // =================================================================================
        private const string Cabecera =
            "utc,instrumento,marco,version,momento,tipo,nombre,lado,precio,precio_tk,contratos,"
            + "contratos_al_encender,visto_inicial,aguante,filas,ticks_barrido,espera_s,"
            + "umbral_vigente,en_rueda,inicio_utc,se_dibuja";

        private string CarpetaEfectiva()
        {
            if (!string.IsNullOrWhiteSpace(AbvCarpeta)) return AbvCarpeta.Trim();
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                "ATAS", "PythiaGex2", "absorcion");
        }

        private void Registrar()
        {
            var sb = new System.Text.StringBuilder(512);
            lock (_llave)
            {
                for (int i = 0; i < _fichas.Count; i++)
                {
                    var f = _fichas[i];
                    if (!f.Anotado) { Fila(sb, f.Ev, "nace"); f.Anotado = true; }
                    else if (f.Ev.Cerrado && !f.AnotadoCierre) { Fila(sb, f.Ev, "cierra"); f.AnotadoCierre = true; }
                }
            }
            if (sb.Length == 0) return;
            try
            {
                string dir = CarpetaEfectiva();
                string dia = DateTime.UtcNow.ToString("yyyy-MM-dd", Inv);
                string ruta = Path.Combine(dir, "absorcion-" + dia + ".csv");
                Directory.CreateDirectory(dir);
                if (_diaRegistro != dia || !File.Exists(ruta))
                {
                    if (!File.Exists(ruta)) File.WriteAllText(ruta, Cabecera + "\n");
                    _diaRegistro = dia;
                }
                File.AppendAllText(ruta, sb.ToString());
            }
            catch (Exception e) { Anotar(e); }
        }

        private void Fila(System.Text.StringBuilder b, Evento e, string momento)
        {
            b.Append(Reloj.De(e.Ns).ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv)).Append(',');
            b.Append(InstrumentInfo != null ? InstrumentInfo.Instrument : "").Append(',');
            b.Append(ChartInfo != null && ChartInfo.ChartType != null
                     ? ChartInfo.ChartType + "-" + ChartInfo.TimeFrame : "").Append(',');
            b.Append(Version).Append(',').Append(momento).Append(',');
            b.Append(e.Tipo).Append(',').Append(Tipos.Nombre(e.Tipo)).Append(',').Append(e.Lado).Append(',');
            Num(b, e.Precio); b.Append(e.PrecioTk).Append(',');
            Num(b, e.Tamano); Num(b, e.TamanoAlEncender); Num(b, e.VistoInicial); Num(b, e.Aguante);
            b.Append(e.Filas).Append(',');
            Num(b, e.Ticks); Num(b, e.EsperaSeg);
            bool rueda = Franja.EnRueda(e.Ns);
            b.Append(e.Tipo == Tipos.AgresorAtrapado
                     ? AbvAtrapadoContratos
                     : (AbvPartirDiaNoche && !rueda ? AbvUmbralFuera : AbvUmbralRueda)).Append(',');
            b.Append(rueda ? 1 : 0).Append(',');
            b.Append(Reloj.De(e.NsInicio).ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv)).Append(',');
            b.Append(Prendido(e.Tipo) && LadoVisible(e.Lado) ? 1 : 0).Append('\n');
        }

        private static void Num(System.Text.StringBuilder b, double v)
        {
            if (!double.IsNaN(v) && !double.IsInfinity(v)) b.Append(v.ToString("0.######", Inv));
            b.Append(',');
        }

        // =================================================================================
        // EL DIBUJO
        // =================================================================================
        private struct Marca
        {
            public int Tipo, Lado, Bar;
            public long Tk, IdEpisodio;
            public double Precio, Contratos, Aguante;
            public int X, Y, D;
        }

        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            try { Pintar(g); }
            catch (Exception e) { Anotar(e); }
        }

        private void Pintar(RenderContext g)
        {
            _tapadas = 0;
            var area = ChartArea;
            var cont = ChartInfo != null ? ChartInfo.PriceChartContainer : null;
            if (cont == null) { Rotulo(g, area); return; }

            int ult = CurrentBar - 1;
            if (ult < 1) { Rotulo(g, area); return; }
            int desde = Math.Max(0, Math.Max(FirstVisibleBarNumber, ult - Math.Max(10, AbvVelasAtras)));
            int hasta = Math.Min(ult, LastVisibleBarNumber);
            if (hasta < desde) { Rotulo(g, area); return; }

            var vDesde = GetCandle(desde);
            if (vDesde == null) { Rotulo(g, area); return; }
            long nsDesde = Reloj.Ns(vDesde.Time);          // IndicatorCandle.Time tambien es UTC

            // 1) foto de los eventos, bajo llave
            _fotos.Clear();
            lock (_llave)
            {
                for (int i = 0; i < _fichas.Count; i++)
                {
                    var e = _fichas[i].Ev;
                    if (e.Ns < nsDesde) continue;
                    if (!Prendido(e.Tipo) || !LadoVisible(e.Lado)) continue;
                    _fotos.Add(new Foto
                    {
                        Tipo = e.Tipo,
                        Lado = e.Lado,
                        Ns = e.Ns,
                        PrecioTk = e.PrecioTk,
                        IdEpisodio = e.IdEpisodio,
                        Precio = e.Precio,
                        Tamano = e.Tamano,
                        Aguante = e.Aguante
                    });
                }
            }
            if (_fotos.Count == 0) { Rotulo(g, area); return; }

            // 2) cada evento a su vela y a su pixel
            double durEst = DuracionTipica(desde, hasta, ult);
            var marcas = new List<Marca>(_fotos.Count);
            int recorteEje = Math.Max(0, AbvMargenEje);
            for (int i = 0; i < _fotos.Count; i++)
            {
                var f = _fotos[i];
                int bar = BarraDe(f.Ns, desde, hasta);
                if (bar < 0) continue;
                var vela = GetCandle(bar);
                if (vela == null) continue;

                long a = Reloj.Ns(vela.Time);
                long b;
                if (bar < ult)
                {
                    var sig = GetCandle(bar + 1);
                    b = sig != null ? Reloj.Ns(sig.Time) : a + (long)durEst;
                }
                else b = a + (long)durEst;                 // la vela en curso: se estima el ancho
                double frac = 0.5;
                if (AbvDondeEnLaVela == DondeEnLaVela.En_el_instante_exacto && b > a)
                {
                    frac = (double)(f.Ns - a) / (b - a);
                    if (frac < 0) frac = 0; else if (frac > 1) frac = 1;
                }

                int xIzq, ancho;
                try
                {
                    int x1 = cont.GetXByBar(bar, true);
                    int x2 = cont.GetXByBar(bar, false);
                    xIzq = Math.Min(x1, x2);               // sea cual sea el sentido de la bandera
                    ancho = Math.Max(1, (int)Math.Round((double)cont.BarsWidth));
                }
                catch { continue; }
                int x = xIzq + (int)Math.Round(frac * ancho);
                if (x < area.Left - 40 || x > area.Right - recorteEje) continue;

                int y;
                try { y = cont.GetYByPrice((decimal)f.Precio, false); } catch { continue; }
                if (y < area.Top - 40 || y > area.Bottom + 40) continue;

                marcas.Add(new Marca
                {
                    Tipo = f.Tipo,
                    Lado = f.Lado,
                    Bar = bar,
                    Tk = f.PrecioTk,
                    IdEpisodio = f.IdEpisodio,
                    Precio = f.Precio,
                    Contratos = f.Tamano,
                    Aguante = f.Aguante,
                    X = x,
                    Y = y,
                    D = 0
                });
            }
            if (marcas.Count == 0) { Rotulo(g, area); return; }

            // 3) juntar las que estan a pocos ticks: se SUMAN, nunca se corren de precio
            if (AbvJuntarTicks > 0) marcas = Juntar(marcas);

            // 4) el diametro, y el tope por vela con las mas grandes
            for (int i = 0; i < marcas.Count; i++)
            {
                var m = marcas[i];
                m.D = Diametro(m.Tipo, m.Contratos);
                marcas[i] = m;
            }
            marcas = Tope(marcas, out _tapadas);

            // 5) a pintar: primero las pelotitas, despues los aros y los punteados
            var fuente = new RenderFont("Consolas", (float)Math.Max(5.0, AbvLetra));
            try { g.SetSmoothingMode(OFT.Rendering.Context.RenderSmoothingModes.AntiAlias); } catch { }
            for (int paso = 0; paso < 3; paso++)
            {
                int tipo = paso == 0 ? Tipos.AbsorcionNivel : (paso == 1 ? Tipos.AgresorAtrapado : Tipos.Iceberg);
                for (int i = 0; i < marcas.Count; i++)
                {
                    if (marcas[i].Tipo != tipo) continue;
                    Dibujar(g, fuente, marcas, i);
                }
            }
            try { g.SetSmoothingMode(OFT.Rendering.Context.RenderSmoothingModes.Default); } catch { }
            Rotulo(g, area);
        }

        private void Dibujar(RenderContext g, RenderFont fuente, List<Marca> marcas, int i)
        {
            var m = marcas[i];
            var col = Pintura(m.Lado);
            int alfa = (int)Math.Round(255.0 * Math.Max(5, Math.Min(100, AbvOpacidad)) / 100.0);
            int d = m.D, r = d / 2;
            var caja = new Rectangle(m.X - r, m.Y - r, Math.Max(2, d), Math.Max(2, d));

            if (m.Tipo == Tipos.AbsorcionNivel)
            {
                g.FillEllipse(Color.FromArgb(alfa, col), caja);
                if (AbvBorde)
                    g.DrawEllipse(new RenderPen(Color.FromArgb(Math.Min(255, alfa + 70), col),
                                                (float)AbvGrosorBorde), caja);
                Numero(g, fuente, col, m, d, false);
            }
            else if (m.Tipo == Tipos.Iceberg)
            {
                // Si el MISMO nivel ya tiene su pelotita, el aro va alrededor de ELLA y no como
                // marca aparte (pasa en el 54,7 % de los aros). Se busca por vela, lado y precio;
                // se tolera la distancia de la fusion porque, si dos niveles vecinos se fundieron,
                // la pelotita puede haber quedado en el tick del mas grande.
                int dd = d;
                int cerca = Math.Max(1, AbvJuntarTicks);
                for (int k = 0; k < marcas.Count; k++)
                    if (marcas[k].Tipo == Tipos.AbsorcionNivel && marcas[k].Bar == m.Bar
                        && marcas[k].Lado == m.Lado && Math.Abs(marcas[k].Tk - m.Tk) <= cerca)
                    { dd = Math.Max(d, marcas[k].D); break; }
                int rr = dd / 2 + 3;
                g.DrawEllipse(new RenderPen(Color.FromArgb(Math.Min(255, alfa + 70), col), (float)AbvGrosorAro),
                              new Rectangle(m.X - rr, m.Y - rr, 2 * rr, 2 * rr));
                Numero(g, fuente, col, m, 2 * rr, true);
            }
            else
            {
                var pluma = new RenderPen(Color.FromArgb(Math.Min(255, alfa + 40), col), (float)AbvGrosorAro);
                try { pluma.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot; } catch { }
                g.DrawEllipse(pluma, caja);
            }
        }

        private void Numero(RenderContext g, RenderFont f, Color col, Marca m, int d, bool esAro)
        {
            // "Nunca" es nunca, tambien para el aro: el operador pidio que TODO se pueda apagar.
            if (AbvVerNumero == CuandoElNumero.Nunca) return;
            string s;
            if (esAro) s = "x" + ((long)Math.Round(m.Aguante)).ToString(Inv);
            else
            {
                if (AbvVerNumero == CuandoElNumero.Solo_las_grandes && m.Contratos < AbvNumeroDesde) return;
                s = ((long)Math.Round(m.Contratos)).ToString(Inv);
            }
            var t = g.MeasureString(s, f);
            int x = m.X + d / 2 + 3;
            int y = m.Y - t.Height / 2;
            g.FillRectangle(Color.FromArgb(160, ColFondo), new Rectangle(x - 2, y, t.Width + 4, t.Height));
            g.DrawString(s, f, Color.FromArgb(235, col), x, y);
        }

        private Color Pintura(int lado)
        {
            bool verde = AbvInvertirColores ? lado < 0 : lado > 0;
            var c = verde ? AbvColorComprasComidas : AbvColorVentasComidas;
            return Color.FromArgb(255, c.R, c.G, c.B);
        }

        private int Diametro(int tipo, double contratos)
        {
            int dmin = Math.Min(AbvDiametroMin, AbvDiametroMax);
            int dmax = Math.Max(AbvDiametroMin, AbvDiametroMax);
            if (AbvTodasIgual) return (dmin + dmax) / 2;
            double cmin = tipo == Tipos.AgresorAtrapado ? AbvAtrapadoDiametroDesde : AbvContratosDiametroMin;
            double cmax = tipo == Tipos.AgresorAtrapado ? AbvAtrapadoDiametroHasta : AbvContratosDiametroMax;
            if (cmax <= cmin) return dmax;
            double u = (contratos - cmin) / (cmax - cmin);
            if (u < 0) u = 0; else if (u > 1) u = 1;
            return (int)Math.Round(dmin + (dmax - dmin) * u);
        }

        /// <summary>Junta las marcas del mismo tipo, mismo lado, misma vela y a pocos ticks en UNA
        /// sola que suma los contratos y se queda EN EL PRECIO de la mas grande. Nunca se corre una
        /// marca de su precio: eso romperia el requisito numero uno.</summary>
        private List<Marca> Juntar(List<Marca> ms)
        {
            ms.Sort(delegate (Marca a, Marca b)
            {
                int c = a.Bar.CompareTo(b.Bar); if (c != 0) return c;
                c = a.Tipo.CompareTo(b.Tipo); if (c != 0) return c;
                c = a.Lado.CompareTo(b.Lado); if (c != 0) return c;
                return a.Tk.CompareTo(b.Tk);
            });
            var salida = new List<Marca>(ms.Count);
            int i = 0;
            while (i < ms.Count)
            {
                int j = i;
                var mejor = ms[i];
                double suma = ms[i].Contratos;
                while (j + 1 < ms.Count
                       && ms[j + 1].Bar == ms[i].Bar && ms[j + 1].Tipo == ms[i].Tipo
                       && ms[j + 1].Lado == ms[i].Lado
                       && Math.Abs(ms[j + 1].Tk - ms[j].Tk) <= AbvJuntarTicks)
                {
                    j++;
                    suma += ms[j].Contratos;
                    if (ms[j].Contratos > mejor.Contratos) mejor = ms[j];
                }
                mejor.Contratos = suma;
                salida.Add(mejor);
                i = j + 1;
            }
            return salida;
        }

        /// <summary>Tope por vela: se quedan las mas grandes. Devuelve cuantas quedaron afuera para
        /// que el rotulo lo diga (que no crea que no paso nada).</summary>
        private List<Marca> Tope(List<Marca> ms, out int tapadas)
        {
            tapadas = 0;
            int tope = Math.Max(1, AbvTopePorVela);
            var cuenta = new Dictionary<int, int>();
            ms.Sort(delegate (Marca a, Marca b) { return b.Contratos.CompareTo(a.Contratos); });
            var salida = new List<Marca>(ms.Count);
            for (int i = 0; i < ms.Count; i++)
            {
                int n;
                cuenta.TryGetValue(ms[i].Bar, out n);
                if (n >= tope) { tapadas++; continue; }
                cuenta[ms[i].Bar] = n + 1;
                salida.Add(ms[i]);
            }
            return salida;
        }

        /// <summary>La vela de un instante, por busqueda binaria entre las visibles. No se guarda
        /// el indice de vela en ningun lado: guardar el indice es un bug latente porque ATAS corre
        /// los indices cuando carga mas historia.</summary>
        private int BarraDe(long ns, int desde, int hasta)
        {
            var v0 = GetCandle(desde);
            if (v0 == null || ns < Reloj.Ns(v0.Time)) return -1;
            int lo = desde, hi = hasta;
            while (lo < hi)
            {
                int m = lo + (hi - lo + 1) / 2;
                var v = GetCandle(m);
                if (v != null && Reloj.Ns(v.Time) <= ns) lo = m; else hi = m - 1;
            }
            return lo;
        }

        /// <summary>Ancho tipico de una vela EN TIEMPO, para poder ubicar el instante adentro de la
        /// vela que todavia se esta formando. Se saca de las velas cerradas, asi anda igual en
        /// graficos de tiempo, de ticks y de rango.</summary>
        private double DuracionTipica(int desde, int hasta, int ult)
        {
            int n = 0;
            double suma = 0;
            for (int b = Math.Max(desde, hasta - 10); b < hasta && b < ult; b++)
            {
                var a = GetCandle(b);
                var c = GetCandle(b + 1);
                if (a == null || c == null) continue;
                double d = Reloj.Ns(c.Time) - Reloj.Ns(a.Time);
                if (d > 0) { suma += d; n++; }
            }
            if (n > 0) return suma / n;
            var u = GetCandle(hasta);
            if (u != null)
            {
                double d = Reloj.Ns(u.LastTime) - Reloj.Ns(u.Time);
                if (d > 0) return d;
            }
            return 60e9;
        }

        private bool Prendido(int tipo)
        {
            if (tipo == Tipos.AbsorcionNivel) return AbvVerAbsorcion;
            if (tipo == Tipos.Iceberg) return AbvVerIceberg;
            if (tipo == Tipos.AgresorAtrapado) return AbvVerAtrapado;
            return false;
        }

        private bool LadoVisible(int lado)
        {
            if (AbvLado == QueLado.Solo_compras_comidas) return lado > 0;
            if (AbvLado == QueLado.Solo_ventas_comidas) return lado < 0;
            return true;
        }

        // =================================================================================
        // El rotulo. La primera linea NO se apaga: sin ella, alguien puede confundir estas
        // pelotitas con una senal, y no lo son.
        // =================================================================================
        private void Rotulo(RenderContext g, Rectangle area)
        {
            var fT = new RenderFont("Consolas", (float)Math.Max(6.0, AbvLetra));
            var f = new RenderFont("Consolas", (float)Math.Max(5.5, AbvLetra - 1.0));
            var lineas = new List<Tuple<string, Color, RenderFont>>();
            lineas.Add(Tuple.Create("ABSORCION VIVA " + Version + " - EN PRUEBA: no validado", ColAviso, fT));
            if (AbvVerRotulo)
            {
                int h0, h1, h2;
                long ords;
                lock (_llave) { h0 = _hoy0; h1 = _hoy1; h2 = _hoy2; ords = _ordenes; }
                bool rueda = Franja.EnRueda(Reloj.Ns(DateTime.UtcNow));
                int umbral = AbvPartirDiaNoche && !rueda ? AbvUmbralFuera : AbvUmbralRueda;
                lineas.Add(Tuple.Create("describe lo que pasa en el libro. NO dice para donde va.",
                                        Color.FromArgb(190, ColTexto), f));
                lineas.Add(Tuple.Create(_estado, Color.FromArgb(190, ColTexto), f));
                lineas.Add(Tuple.Create(string.Format(Inv,
                    "umbral ahora {0} contratos ({1}){2}", umbral,
                    rueda ? "rueda 09:30-16:00 NY" : "fuera de la rueda",
                    AbvPartirDiaNoche ? "" : "  [un solo umbral]"), ColTexto, f));
                lineas.Add(Tuple.Create(string.Format(Inv,
                    "eventos hoy (se dibujen o no):  pelotita {0}   aro {1}   punteado {2}   (de {3:n0} ordenes)",
                    h0, h1, h2, ords), ColTexto, f));
                if (_tapadas > 0)
                    lineas.Add(Tuple.Create(string.Format(Inv,
                        "{0} marcas tapadas por el tope de {1} por vela", _tapadas, AbvTopePorVela),
                        ColAviso, f));
            }

            int w = 0, h = 0;
            for (int i = 0; i < lineas.Count; i++)
            {
                var m = g.MeasureString(lineas[i].Item1, lineas[i].Item3);
                if (m.Width > w) w = m.Width;
                h += m.Height + 1;
            }
            int x0 = area.Left + 6, y0 = area.Top + 6;
            g.FillRectangle(Color.FromArgb(205, ColFondo), new Rectangle(x0, y0, w + 14, h + 8));
            g.DrawRectangle(new RenderPen(Color.FromArgb(150, ColAviso), 1f), new Rectangle(x0, y0, w + 14, h + 8));
            int y = y0 + 4;
            for (int i = 0; i < lineas.Count; i++)
            {
                g.DrawString(lineas[i].Item1, lineas[i].Item3, lineas[i].Item2, x0 + 7, y);
                y += g.MeasureString(lineas[i].Item1, lineas[i].Item3).Height + 1;
            }
        }

        // =================================================================================
        // Log propio: ATAS se traga las excepciones de los indicadores y un fallo se ve solo
        // como un indicador que no dibuja.
        // =================================================================================
        private static void Log(string msg)
        {
            try
            {
                var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                     "ATAS", "pythiagex2-absorcion.log");
                File.AppendAllText(p, DateTime.Now.ToString("s") + "  " + msg + "\n");
            }
            catch { }
        }

        // El freno del log. ATAS llama a OnRender cada vez que repinta el grafico, no cada 250 ms:
        // si algo adentro del dibujo tira excepcion, sin freno esto escribiria en disco DECENAS DE
        // VECES POR SEGUNDO desde el hilo de la pantalla, que es justo como se cuelga la
        // plataforma. Se anota la primera vez, despues una por minuto, y al final se dice cuantas
        // se callaron. El log de Gamma Hoy ya pesa 41 MB: no hace falta otro igual.
        private static readonly object _llaveLog = new object();
        private static string _ultimaFalla = "";
        private static DateTime _ultimaVez = DateTime.MinValue;
        private static long _calladas;

        private static void Anotar(Exception e)
        {
            var st = e.StackTrace ?? "";
            string msg = "EXCEPCION " + e.GetType().Name + ": " + e.Message + " | "
                       + st.Replace("\n", " ").Substring(0, Math.Min(300, st.Length));
            bool escribir;
            long calladas = 0;
            lock (_llaveLog)
            {
                var ahora = DateTime.UtcNow;
                escribir = msg != _ultimaFalla || (ahora - _ultimaVez).TotalSeconds >= 60;
                if (escribir)
                {
                    calladas = _calladas;
                    _calladas = 0;
                    _ultimaFalla = msg;
                    _ultimaVez = ahora;
                }
                else _calladas++;
            }
            if (escribir) Log(msg + (calladas > 0 ? "  [y " + calladas + " iguales que no se anotaron]" : ""));
        }
    }
}
