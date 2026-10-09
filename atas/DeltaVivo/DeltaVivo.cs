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

namespace DeltaVivo
{
    /// <summary>
    /// DELTA VIVO 1.0 - EN PRUEBA, NO VALIDADO.
    ///
    /// QUE ES. Lo mismo que Absorcion Viva -una marca apoyada en el PRECIO EXACTO, encima de las
    /// velas, del tamano de lo que paso- pero para el DELTA: la agresion firmada, compras
    /// agresoras menos ventas agresoras. Marca LA OLA: el momento en que en pocos segundos el
    /// neto se fue para un lado y no volvio.
    ///   * ROMBO LLENO  = una ola.
    ///   * ROMBO HUECO  = una ola PURA: casi todo el volumen de esos segundos fue del mismo lado,
    ///                    o sea que del otro lado no habia nadie.
    ///   * AZUL    = empujaron las COMPRAS.   NARANJA = empujaron las VENTAS.
    ///
    /// POR QUE NO USA VERDE NI ROJO, y esto no es capricho: en la pantalla del operador ya conviven
    /// dos verdes con la lectura OPUESTA. En el Big Trades oficial, verde = compro y GANO. En
    /// Absorcion Viva, verde = compro y PERDIO (se la comieron). Un tercer verde volveria el color
    /// inutilizable. Azul y naranja ademas se distinguen con daltonismo. Hay un ajuste para dar
    /// vuelta los colores, y otro para elegir cualquier par.
    ///
    /// DE DONDE SALE EL DATO. De OnCumulativeTrade: la misma via por la que el Big Trades oficial
    /// recibe las operaciones, la misma que usa Absorcion Viva y la misma con la que la sonda grabo
    /// las sesiones que se midieron. NO se suscribe a profundidad ni a MarketByOrder.
    ///
    /// NO LEE HISTORIA AL ARRANCAR, a proposito (ATAS devuelve la sesion entera aunque se le pida
    /// media hora: pedir eso al abrir el grafico es colgar la plataforma). Arranca vacio y se llena
    /// hacia adelante; el rotulo dice "calentando" los primeros segundos.
    ///
    /// QUE NO ES. No es una senal y no dice para donde va el precio. MEDIDO sobre 22 ruedas de MNQ
    /// con el juez de la ronda 9 (entrada el segundo siguiente cruzando el spread real, costo
    /// verificado 0,948 puntos la vuelta): perseguir la ola pierde en las 36 de 36 celdas, de -1,17
    /// a -9,27 puntos al tacto (r11_f3_04_juez.csv, 1.568 vueltas). Operar en contra tampoco paga:
    /// la mejor celda da +0,932 con t +1,85 contra un techo del azar de 3,93, y se cae con el
    /// control apareado por calibre. Esto DESCRIBE lo que esta pasando en la cinta.
    ///
    /// EL ROMBO NO ES UN NIVEL. Marca el precio exacto de la operacion que completo la cuenta y ese
    /// precio no se mueve nunca, pero la ventana entera recorrio 73 ticks (18,25 puntos) de mediana
    /// en sus 10 segundos y la propia agresion que la enciende barrio el libro 5 ticks de mediana
    /// (p90 51). Dice CUANDO paso y a que precio se completo. No es soporte, no es resistencia y
    /// no es una flecha: por eso la marca no tiene punta.
    ///
    /// COMO DECIDE. En DeltaNucleo.cs, que no conoce ATAS. Ese mismo archivo se compila en arnes/ y
    /// se corre sobre las cintas grabadas para compararlo evento por evento contra el Python que
    /// produjo las mediciones (laboratorio/dom/ronda11_delta/paridad/).
    ///
    /// LOS AJUSTES LLEVAN EL PREFIJO "Dlt" A PROPOSITO: ATAS guarda los ajustes POR NOMBRE de
    /// propiedad en el workspace (.ws). Un nombre repetido se come el valor guardado de otro
    /// indicador, y para pisar un valor ya guardado no alcanza con cambiar el default en el codigo:
    /// hay que RENOMBRAR la propiedad (la trampa del 16-09). Ninguna propiedad de aca se llama como
    /// una "Abv" de Absorcion Viva ni como una "Fc" de Flujo Claro.
    ///
    /// LOS UMBRALES SON DE MNQ. En MES no hay NADA medido de esta familia. Puesto en un grafico de
    /// MES sin recalibrar va a sacar una cantidad de marcas que nadie conto.
    /// </summary>
    [DisplayName("Delta Vivo (EN PRUEBA)")]
    [Category("PythiaGex 2.0")]
    public class DeltaVivoIndicador : Indicator
    {
        public const string Version = "1.0";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public enum QueLado { Los_dos, Solo_compras, Solo_ventas }
        public enum DondeEnLaVela { En_el_instante_exacto, En_el_centro_de_la_vela }
        public enum CuandoElNumero { Nunca, Solo_las_grandes, Siempre }

        // =================================================================================
        // 1. QUE SE MARCA
        // =================================================================================
        [Display(Name = "Rombo: la ola (empujon de delta)", GroupName = "1. Que se marca", Order = 10,
            Description = "En los ultimos segundos el neto de agresion se fue para un lado. MEDIDO con esta misma definicion sobre 23 ruedas de MNQ (r11_dib_05_salida.txt, bloque A): 241 olas, mediana 10 por sesion (min 2, max 22), 1,06 por hora en la rueda y 0,25 fuera.")]
        public bool DltVerOla { get; set; } = true;

        [Display(Name = "Contratos NETOS para marcar EN LA RUEDA (09:30-16:00 NY)", GroupName = "1. Que se marca", Order = 20,
            Description = "1500 es el percentil 98,908 de la rueda: el 1,0923 % de las filas de cinta lo pasan (r11_dib_01_salida.txt, bloque B, n = 11.675.318 filas). Para comparar: 900 deja mediana 34 marcas por sesion, 1200 deja 18 y 1500 deja 10. La vara se eligio por DENSIDAD, no por resultado: en esta familia no hay resultado en ninguna celda medida.")]
        [Range(1, 1000000)]
        public int DltVaraRueda { get; set; } = 1500;

        [Display(Name = "Contratos NETOS para marcar FUERA de la rueda", GroupName = "1. Que se marca", Order = 30,
            Description = "800 es el percentil 99,490 de fuera de la rueda (0,5100 % de 6.257.614 filas). POR QUE PARTIDO: con 800 para las 23 horas salen 6,64 marcas por hora de dia, que es una sopa; con 1500 para las 23 horas queda una marca cada 33 horas de noche, que es un desierto.")]
        [Range(1, 1000000)]
        public int DltVaraFuera { get; set; } = 800;

        [Display(Name = "Usar las dos varas (dia y noche)", GroupName = "1. Que se marca", Order = 40,
            Description = "Apagado: se usa siempre la de la rueda. La proporcion noche/dia (800/1500 = 0,53) es la misma que eligio Absorcion Viva (90/150 = 0,60).")]
        public bool DltPartirDiaNoche { get; set; } = true;

        [Display(Name = "Segundos de la ventana", GroupName = "1. Que se marca", Order = 50,
            Description = "La ola es el delta neto acumulado en los ultimos N segundos. MEDIDO: 5, 10 y 30 s dan la misma conclusion; 10 s es la mas pareja en densidad. Si lo cambias, la vara de arriba deja de significar lo que dice su descripcion.")]
        [Range(1, 600)]
        public int DltVentanaSegundos { get; set; } = 10;

        [Display(Name = "Rombo HUECO: la ola pura (razon |delta| / volumen)", GroupName = "1. Que se marca", Order = 60,
            Description = "Con 0,70: de cada 100 contratos operados en esos segundos, 70 o mas fueron del mismo lado. No agrega marcas: son olas que ya estaban dibujadas y cambian de relleno. MEDIDO: 47 de 241 olas (19,5 %), mediana 1 por sesion, max 6. Con 0,60 salen 4 por sesion (demasiado seguido) y con 0,80 quedan 25 en 23 ruedas. NO ESTA MEDIDO que pase nada distinto despues de una ola pura: se dibuja porque es una situacion distinta y rara, igual que el aro del iceberg.")]
        [Range(0.10, 1.00)]
        public double DltRazonPura { get; set; } = 0.70;

        [Display(Name = "Rearmar recien cuando el delta baja a la mitad de la vara", GroupName = "1. Que se marca", Order = 70,
            Description = "Es el refractario con el que se midio TODO. Apagado, la misma ola enciende una y otra vez y la cantidad de marcas se multiplica. Se deja como ajuste para poder verlo, no porque convenga.")]
        public bool DltRearmarALaMitad { get; set; } = true;

        [Display(Name = "Que lado se marca", GroupName = "1. Que se marca", Order = 80,
            Description = "Solo limita el DIBUJO. El registro en CSV guarda los dos lados igual.")]
        public QueLado DltLado { get; set; } = QueLado.Los_dos;

        [Display(Name = "Contratos netos minimos para dibujar (0 = la vara de arriba)", GroupName = "1. Que se marca", Order = 90,
            Description = "Para endurecer la pantalla sin tocar la vara ni el registro: las olas mas chicas que esto se calculan y se anotan igual, pero no se dibujan.")]
        [Range(0, 1000000)]
        public int DltContratosMinimos { get; set; } = 0;

        // =================================================================================
        // 2. LAS OTRAS FAMILIAS (se calculan y se anotan siempre; de fabrica no se dibujan)
        // =================================================================================
        [Display(Name = "Rombo punteado: contrapie (la ola y el precio al reves)", GroupName = "2. Las otras familias", Order = 10,
            Description = "APAGADO de fabrica, pero SE SIGUE CALCULANDO Y ANOTANDO en el CSV. MEDIDO: 8 marcas en 23 ruedas con 2 ticks en contra (15 ruedas sin ninguna). Es una rareza de una vez cada tres ruedas: no es una marca, es una anecdota. Sobre las 241 olas el signo del avance coincide con el del delta en el 100,00 % de los casos.")]
        public bool DltVerContrapie { get; set; } = false;

        [Display(Name = "Ticks que el precio fue EN CONTRA (contrapie)", GroupName = "2. Las otras familias", Order = 20,
            Description = "MEDIDO en 23 ruedas: 8 marcas con 2 ticks, 5 con 4 y 3 con 8.")]
        [Range(1, 400)]
        public int DltContrapieTicks { get; set; } = 2;

        [Display(Name = "Cuadrado: la traba (mucho delta y el precio quieto)", GroupName = "2. Las otras familias", Order = 30,
            Description = "APAGADO de fabrica y casi seguro que no vas a ver ninguna: MEDIDO, 0 marcas en 23 ruedas con 800/400 y rango <= 8 ticks. OJO CON LA TRAMPA: si en vez del RANGO se mide el movimiento NETO parece que existe (7 por sesion), pero esas mismas ventanas tienen 52 ticks de rango, o sea 13 puntos de ida y vuelta. El precio no estuvo quieto: se fue y volvio. La absorcion medida AL NIVEL si existe y ya la tenes en pantalla: es la pelotita de Absorcion Viva.")]
        public bool DltVerTraba { get; set; } = false;

        [Display(Name = "Contratos netos de la traba EN LA RUEDA", GroupName = "2. Las otras familias", Order = 40)]
        [Range(1, 1000000)]
        public int DltTrabaVaraRueda { get; set; } = 800;

        [Display(Name = "Contratos netos de la traba FUERA de la rueda", GroupName = "2. Las otras familias", Order = 50)]
        [Range(1, 1000000)]
        public int DltTrabaVaraFuera { get; set; } = 400;

        [Display(Name = "Rango maximo de la ventana para la traba (ticks)", GroupName = "2. Las otras familias", Order = 60,
            Description = "RANGO = maximo menos minimo del precio operado en la ventana. No es el movimiento neto: esa es justo la trampa.")]
        [Range(0, 400)]
        public int DltTrabaRangoTicks { get; set; } = 8;

        [Display(Name = "El nido: todo el delta en un solo precio (SOLO SE ANOTA)", GroupName = "2. Las otras familias", Order = 70,
            Description = "NO SE PUEDE DIBUJAR y por eso no hay ajuste para prenderlo. La cinta no dice a que precio se opero cada contrato de una agresion que camino el libro: dice el primero, el ultimo y el total. Cambiando esa convencion, el precio dominante del nido cambia en el 95,9 % de los casos y se corre 12 ticks de mediana, p90 22 (r11_dib_05_salida.txt, bloque E, n = 363). Dibujarlo romperia el requisito numero uno, que es el precio exacto. Se calcula y se anota en el CSV para poder medirlo despues.")]
        [Range(1, 1000000)]
        public int DltNidoVaraRueda { get; set; } = 400;

        [Display(Name = "Contratos netos del nido FUERA de la rueda (solo se anota)", GroupName = "2. Las otras familias", Order = 80)]
        [Range(1, 1000000)]
        public int DltNidoVaraFuera { get; set; } = 250;

        [Display(Name = "Concentracion minima del nido (solo se anota)", GroupName = "2. Las otras familias", Order = 90,
            Description = "Que fraccion del delta repartido entre precios se la lleva el precio dominante. MEDIDO: p50 0,648.")]
        [Range(0.05, 1.00)]
        public double DltNidoConcentracion { get; set; } = 0.50;

        // =================================================================================
        // 3. EL ROMBO
        // =================================================================================
        [Display(Name = "AZUL: empujaron las COMPRAS (delta comprador neto)", GroupName = "3. El rombo", Order = 10,
            Description = "El nombre es largo a proposito. En Absorcion Viva el verde quiere decir que la compra PERDIO; aca el azul quiere decir que la compra GANO. Son lecturas opuestas y por eso los colores son distintos.")]
        public System.Windows.Media.Color DltColorCompras { get; set; }
            = System.Windows.Media.Color.FromRgb(64, 148, 255);

        [Display(Name = "NARANJA: empujaron las VENTAS (delta vendedor neto)", GroupName = "3. El rombo", Order = 20)]
        public System.Windows.Media.Color DltColorVentas { get; set; }
            = System.Windows.Media.Color.FromRgb(245, 130, 32);

        [Display(Name = "Dar vuelta los colores", GroupName = "3. El rombo", Order = 30)]
        public bool DltInvertirColores { get; set; } = false;

        [Display(Name = "Opacidad (%)", GroupName = "3. El rombo", Order = 40,
            Description = "55 % deja ver la vela por abajo. Absorcion Viva usa 60 %: el rombo va un poco mas transparente para que la pelotita de la absorcion mande en la pantalla.")]
        [Range(5, 100)]
        public int DltOpacidad { get; set; } = 55;

        [Display(Name = "Diametro minimo (px)", GroupName = "3. El rombo", Order = 50)]
        [Range(2, 200)]
        public int DltDiametroMin { get; set; } = 12;

        [Display(Name = "Diametro maximo (px)", GroupName = "3. El rombo", Order = 60,
            Description = "Con esta escala la ola tipica queda en 15 px, un poco mas chica que la pelotita tipica de Absorcion Viva (17 px), que es lo que se busca.")]
        [Range(2, 200)]
        public int DltDiametroMax { get; set; } = 26;

        [Display(Name = "Veces la vara que valen el diametro MINIMO (x10)", GroupName = "3. El rombo", Order = 70,
            Description = "LA ESCALA VA EN VECES LA VARA, no en contratos sueltos, porque la vara cambia de dia a noche. 10 = una vez la vara. RIESGO QUE HAY QUE SABER: con esto una ola de noche de 1.000 contratos se ve igual de grande que una de dia de 1.950. Es a proposito, pero es una decision, no una medicion.")]
        [Range(1, 1000)]
        public int DltVecesDiametroMin { get; set; } = 10;

        [Display(Name = "Veces la vara que valen el diametro MAXIMO (x10)", GroupName = "3. El rombo", Order = 80,
            Description = "25 = dos veces y media la vara. MEDIDO: la ola crece hasta 1,30 veces la vara de mediana (p90 1,97, p99 3,10 en la rueda; 1,24 / 1,92 / 2,69 fuera - r11_dib_04_salida.txt). Entre el minimo y el maximo crece derecho, no con la raiz del area.")]
        [Range(1, 1000)]
        public int DltVecesDiametroMax { get; set; } = 25;

        [Display(Name = "Todas del mismo tamano", GroupName = "3. El rombo", Order = 90)]
        public bool DltTodasIgual { get; set; } = false;

        [Display(Name = "Grosor del contorno de la ola pura (px)", GroupName = "3. El rombo", Order = 100)]
        [Range(0.5, 6.0)]
        public double DltGrosorContorno { get; set; } = 2.0;

        // =================================================================================
        // 4. EL NUMERO AL LADO
        // =================================================================================
        [Display(Name = "Cuando se muestra el numero de contratos", GroupName = "4. El numero", Order = 10,
            Description = "El numero son los contratos NETOS; el signo lo dice el color. De fabrica aparece recien cuando la marca ya se clavo en el diametro maximo y el tamano dejo de informar: pasa en el 1 al 3 % de las olas.")]
        public CuandoElNumero DltVerNumero { get; set; } = CuandoElNumero.Solo_las_grandes;

        [Display(Name = "Veces la vara a partir de las cuales es 'grande' (x10)", GroupName = "4. El numero", Order = 20)]
        [Range(1, 1000)]
        public int DltNumeroDesdeVeces { get; set; } = 25;

        [Display(Name = "Tamano de la letra", GroupName = "4. El numero", Order = 30)]
        [Range(5.0, 20.0)]
        public double DltLetra { get; set; } = 9.0;

        // =================================================================================
        // 5. DONDE SE UBICA
        // =================================================================================
        [Display(Name = "Donde cae dentro de la vela", GroupName = "5. Donde se ubica", Order = 10,
            Description = "La altura SIEMPRE es el precio exacto del evento. Esto es solo el corrimiento horizontal: en el instante exacto dentro de la vela, o al medio. En 1 minuto casi no se nota; en 30 minutos es la diferencia entre ver el momento y no verlo.")]
        public DondeEnLaVela DltDondeEnLaVela { get; set; } = DondeEnLaVela.En_el_instante_exacto;

        [Display(Name = "Juntar las marcas a N ticks o menos (0 = no juntar)", GroupName = "5. Donde se ubica", Order = 20,
            Description = "Dos marcas del mismo tipo y lado, en la misma vela y a 2 ticks o menos, se dibujan como UNA sumando los contratos. NUNCA se corre una marca de su precio: eso romperia lo unico que no se negocia. OJO, heredado de Absorcion Viva y sin arreglar: la fusion se ENCADENA (si A y B estan a 2 ticks y B y C tambien, se juntan las tres), asi que una marca fusionada puede abarcar mas de los 2 ticks que promete el nombre.")]
        [Range(0, 100)]
        public int DltJuntarTicks { get; set; } = 2;

        [Display(Name = "Maximo de marcas por vela", GroupName = "5. Donde se ubica", Order = 30,
            Description = "Se quedan las MAS GRANDES y el rotulo avisa cuantas se escondieron. MEDIDO: el maximo son 2 olas en una vela de M1 y de M5, y 6 en M30, asi que con 6 no tapa nada. OJO: este tope es INDEPENDIENTE del de Absorcion Viva; los dos indicadores no se conocen.")]
        [Range(1, 200)]
        public int DltTopePorVela { get; set; } = 6;

        [Display(Name = "Dibujar encima de las velas", GroupName = "5. Donde se ubica", Order = 40)]
        public bool DltEncimaDeLasVelas
        {
            get { return DrawAbovePrice; }
            set { DrawAbovePrice = value; }
        }

        [Display(Name = "Margen del eje de precios (px)", GroupName = "5. Donde se ubica", Order = 50,
            Description = "El eje de precios se dibuja ENCIMA del area del grafico: sin este margen la marca mas nueva queda cortada por el eje.")]
        [Range(0, 200)]
        public int DltMargenEje { get; set; } = 62;

        [Display(Name = "Velas hacia atras que se dibujan", GroupName = "5. Donde se ubica", Order = 60)]
        [Range(10, 20000)]
        public int DltVelasAtras { get; set; } = 1000;

        // =================================================================================
        // 6. EL ROTULO
        // =================================================================================
        [Display(Name = "Ver el rotulo entero", GroupName = "6. El rotulo", Order = 10,
            Description = "La primera linea (EN PRUEBA: no validado) no se puede apagar.")]
        public bool DltVerRotulo { get; set; } = true;

        // =================================================================================
        // 7. EL REGISTRO
        // =================================================================================
        [Display(Name = "Anotar todos los eventos en un CSV", GroupName = "7. El registro", Order = 10,
            Description = @"%APPDATA%\ATAS\PythiaGex2\delta\delta-<fecha UTC>.csv. Guarda TODOS los eventos de las CUATRO familias, se dibujen o no, con una fila cuando nace y otra cuando se rearma (con el maximo alcanzado). Es a proposito que el registro no dependa de lo que este prendido: si dependiera, arruinaria la prueba hacia adelante.")]
        public bool DltRegistrar { get; set; } = true;

        [Display(Name = "Carpeta (vacio = la de PythiaGex 2.0)", GroupName = "7. El registro", Order = 20)]
        public string DltCarpeta { get; set; } = "";

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
            public long Ns, PrecioTk;
            public double Precio, Tamano, Vara;
            public bool Pura;
        }

        private readonly NucleoDelta _nucleo = new NucleoDelta();
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
        private int _hoyOla, _hoyPura, _hoyCpie, _hoyTrb, _hoyNido;
        private string _diaContadores = "";
        private string _diaRegistro = "";
        private int _tapadas;
        private string _estado = "arrancando: todavia no llego ninguna operacion";

        /// <summary>Segundos de cinta antes de dejar de decir "calentando". Es lo que tarda en
        /// llenarse la ventana movil, con un margen: recien ahi cualquier ola que se vea nacio con
        /// la ventana entera adentro de esta corrida.</summary>
        private double SegCalentando { get { return Math.Max(10, DltVentanaSegundos) * 2.0; } }

        private static readonly Color ColTexto = Color.FromArgb(226, 232, 240);
        private static readonly Color ColFondo = Color.FromArgb(11, 16, 23);
        private static readonly Color ColAviso = Color.FromArgb(250, 176, 60);

        // =================================================================================
        // Ciclo de vida
        // =================================================================================
        public DeltaVivoIndicador() : base(true)
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
            _bateria.Agregar(_nucleo);
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
            Log("delta: arrancado, version " + Version);
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
        // Es la misma convencion con la que la sonda grabo las sesiones que se midieron, asi que la
        // cinta viva y la grabada son la misma cosa.
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
                    switch (nv[i].Tipo)
                    {
                        case Tipos.Ola: _hoyOla++; if (nv[i].Pura) _hoyPura++; break;
                        case Tipos.Contrapie: _hoyCpie++; break;
                        case Tipos.Traba: _hoyTrb++; break;
                        default: _hoyNido++; break;
                    }
                    _sucio = true;
                }
                if (_fichas.Count > _topeFichas) Podar();
            }
        }

        /// <summary>Cuando se vuelve a barrer <see cref="_fichas"/>. NO es una constante a
        /// proposito: si una poda no rinde (porque todo lo que hay todavia le debe una fila al
        /// registro), el tope sube y el barrido no se repite en la fila siguiente. Sin esto, con la
        /// lista llena, cada operacion de la cinta pagaria un recorrido entero CON LA LLAVE TOMADA,
        /// y en MNQ llegan hasta 426 por segundo: eso es exactamente como se cuelga la plataforma.</summary>
        private int _topeFichas = 6000;
        /// <summary>Techo que no se pasa NUNCA, aunque la poda no tenga nada para sacar. Es la
        /// diferencia entre "el indicador pierde una fila del CSV en un caso absurdo" y "ATAS se
        /// queda sin memoria": se elige perder la fila, y se anota en el log.</summary>
        private const int TechoFichas = 40000;
        private bool _avisoTecho;

        /// <summary>Saca fichas viejas SIN perder ninguna fila del registro. Absorcion Viva hace
        /// aca un RemoveRange(0, 2000) a ciegas: con sus 24 marcas por sesion eso no muerde en 150
        /// sesiones, pero en el delta muerde antes si alguien baja la vara (con 150 contratos una
        /// sola sesion saco 519 marcas), y lo que se perderia es la fila de "cierra" de eventos
        /// todavia vivos, justo la que hace falta para medir. Se conserva TODO lo que no tenga las
        /// dos filas escritas y todo lo de las ultimas seis horas de cinta.</summary>
        private void Podar()
        {
            long corte = _nsUltima - 6L * 3600L * 1000000000L;
            // CON EL REGISTRO APAGADO NADIE VA A ESCRIBIR ESAS FILAS NUNCA. Sin esta linea,
            // 'completa' era false para todas las fichas, la poda no sacaba ni una, y la lista
            // crecia sin techo mientras ATAS quedara abierto: con una vara baja (una sola sesion
            // saco 519 marcas) eso llega a las 6.000 en un par de semanas y, de ahi en adelante,
            // CADA operacion de la cinta barre la lista entera con la llave tomada.
            bool guardar = DltRegistrar;
            int w = 0;
            for (int i = 0; i < _fichas.Count; i++)
            {
                var f = _fichas[i];
                bool completa = !guardar || (f.Anotado && f.AnotadoCierre);
                if (!completa || f.Ev.Ns >= corte) _fichas[w++] = f;
            }
            if (w < _fichas.Count) _fichas.RemoveRange(w, _fichas.Count - w);

            // El techo duro: si aun asi quedaron mas de 40.000 (miles de eventos todavia VIVOS,
            // que es absurdo pero no imposible con la vara al minimo), se tiran los MAS VIEJOS.
            // La lista esta en orden de llegada, asi que los de adelante son los mas viejos.
            if (_fichas.Count > TechoFichas)
            {
                int sobran = _fichas.Count - TechoFichas;
                _fichas.RemoveRange(0, sobran);
                if (!_avisoTecho)
                {
                    _avisoTecho = true;
                    Log("delta: se paso el techo de " + TechoFichas + " fichas y se tiraron las "
                        + sobran + " mas viejas. Si esto aparece, la vara esta demasiado baja: "
                        + "esas marcas pierden su fila 'cierra' en el CSV.");
                }
            }

            // Y no volver a barrer hasta que entren otras mil: la poda es O(n) y va con la llave
            // tomada en el hilo de la cinta.
            _topeFichas = Math.Max(6000, _fichas.Count + 1000);
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
                // InstrumentInfo parpadea en null un latido, caerse al 0,25 de fabrica vaciaria el
                // nucleo entero por nada en un instrumento de otro tick.
                double tk = 0;
                try { if (InstrumentInfo != null) tk = (double)InstrumentInfo.TickSize; } catch { }
                if (tk <= 0) tk = _tick > 0 ? _tick : 0.25;

                bool reiniciar = false;
                lock (_llave)
                {
                    if (_tick != tk)
                    {
                        _tick = tk;
                        _nucleo.Tick = tk;
                        reiniciar = _ordenes > 0;
                    }
                    Ajustes();
                    if (reiniciar)
                    {
                        _bateria.Reiniciar();
                        _fichas.Clear();
                        _actual = null;
                        _ordenes = 0;
                        _hoyOla = _hoyPura = _hoyCpie = _hoyTrb = _hoyNido = 0;
                        _sucio = true;
                        Log("delta: cambio el tick a " + tk.ToString(Inv) + ", se reinicio el nucleo");
                    }
                    Estado();
                }
                if (DltRegistrar) Registrar();
                bool pintar;
                lock (_llave) { pintar = _sucio; _sucio = false; }
                if (pintar) try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            }
            catch (Exception e) { Anotar(e); }
        }

        /// <summary>Los ajustes de la pantalla se copian al nucleo. Se llama con la llave tomada.
        /// Las CUATRO familias calculan siempre: lo que apagan los ajustes es el DIBUJO.</summary>
        private void Ajustes()
        {
            _nucleo.VentanaSeg = Math.Max(1, DltVentanaSegundos);
            _nucleo.OlaVaraRueda = DltVaraRueda;
            _nucleo.OlaVaraFuera = DltPartirDiaNoche ? DltVaraFuera : DltVaraRueda;
            _nucleo.PartirPorFranja = DltPartirDiaNoche;
            _nucleo.RazonPura = DltRazonPura;
            _nucleo.ContrapieTicks = DltContrapieTicks;
            _nucleo.TrabaVaraRueda = DltTrabaVaraRueda;
            _nucleo.TrabaVaraFuera = DltPartirDiaNoche ? DltTrabaVaraFuera : DltTrabaVaraRueda;
            _nucleo.TrabaRangoTicks = DltTrabaRangoTicks;
            _nucleo.NidoVaraRueda = DltNidoVaraRueda;
            _nucleo.NidoVaraFuera = DltPartirDiaNoche ? DltNidoVaraFuera : DltNidoVaraRueda;
            _nucleo.NidoConcMin = DltNidoConcentracion;
            // El refractario apagado se hace con una vara de rearme imposible de bajar: el nucleo
            // rearma cuando la medida cae por debajo de la MITAD de la vara, asi que con esto
            // rearma en la fila siguiente y cada fila que pase el umbral vuelve a encender.
            _nucleo.PrenderOla = true;
            _nucleo.PrenderContrapie = true;
            _nucleo.PrenderTraba = true;
            _nucleo.PrenderNido = true;
            _nucleo.RearmeInmediato = !DltRearmarALaMitad;
        }

        private void Estado()
        {
            string hoy = DateTime.UtcNow.ToString("yyyy-MM-dd", Inv);
            if (_diaContadores != hoy)
            {
                _diaContadores = hoy;
                _hoyOla = _hoyPura = _hoyCpie = _hoyTrb = _hoyNido = 0;
            }

            if (_ordenes == 0)
            {
                _estado = "sin cinta: el conector todavia no mando ninguna operacion";
                return;
            }
            double seg = (_nsUltima - _nsPrimera) / 1e9;
            if (seg < SegCalentando)
                _estado = string.Format(Inv,
                    "CALENTANDO: {0:0} s de cinta de {1:0} ({2:n0} ordenes). La ventana todavia no se lleno entera.",
                    seg, SegCalentando, _ordenes);
            else
                _estado = string.Format(Inv, "cinta viva: {0:n0} ordenes, {1:0} min. Arranco vacio: no lee historia.",
                    _ordenes, seg / 60.0);
        }

        // =================================================================================
        // El registro: TODOS los eventos de las cuatro familias, se dibujen o no
        // =================================================================================
        private const string Cabecera =
            "utc,instrumento,marco,version,momento,tipo,familia,lado,precio,precio_tk,"
            + "d_al_encender,d_maximo,volumen,razon,pura,avance_tk,rango_tk,vara,filas,"
            + "ventana_s,en_rueda,inicio_utc,cierre_utc,id,se_dibuja";

        private string CarpetaEfectiva()
        {
            if (!string.IsNullOrWhiteSpace(DltCarpeta)) return DltCarpeta.Trim();
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                "ATAS", "PythiaGex2", "delta");
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
                string ruta = Path.Combine(dir, "delta-" + dia + ".csv");
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
            Num(b, e.TamanoAlEncender); Num(b, e.Tamano); Num(b, e.Volumen); Num(b, e.Razon);
            b.Append(e.Pura ? 1 : 0).Append(',');
            Num(b, e.Avance); Num(b, e.Rango); Num(b, e.Vara);
            b.Append(e.Filas).Append(',').Append(DltVentanaSegundos).Append(',');
            b.Append(e.EnRueda ? 1 : 0).Append(',');
            b.Append(Reloj.De(e.NsInicio).ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv)).Append(',');
            b.Append(e.NsCierre > 0 ? Reloj.De(e.NsCierre).ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv) : "").Append(',');
            b.Append(e.IdEpisodio).Append(',');
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
            public long Tk;
            public double Precio, Contratos, Vara;
            public bool Pura;
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
            int desde = Math.Max(0, Math.Max(FirstVisibleBarNumber, ult - Math.Max(10, DltVelasAtras)));
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
                    if (e.Tamano < DltContratosMinimos) continue;
                    _fotos.Add(new Foto
                    {
                        Tipo = e.Tipo,
                        Lado = e.Lado,
                        Ns = e.Ns,
                        PrecioTk = e.PrecioTk,
                        Precio = e.Precio,
                        Tamano = e.Tamano,
                        Vara = e.Vara,
                        Pura = e.Pura
                    });
                }
            }
            if (_fotos.Count == 0) { Rotulo(g, area); return; }

            // 2) cada evento a su vela y a su pixel
            double durEst = DuracionTipica(desde, hasta, ult);
            var marcas = new List<Marca>(_fotos.Count);
            int recorteEje = Math.Max(0, DltMargenEje);
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

                // REQUISITO 1: la marca va en la vela que CONTIENE el instante, nunca corrida.
                // BarraDe() busca solo entre las visibles y devuelve la ultima cuando el instante
                // cae despues, asi que con el grafico corrido hacia atras (LastVisibleBarNumber
                // menor que la ultima vela) un evento POSTERIOR a la ultima vela visible quedaba
                // pegado a esa vela. Casi siempre lo tapaba el recorte del eje; casi siempre no es
                // nunca, y si se cuela es una marca en la vela equivocada.
                if (bar < ult && f.Ns >= b) continue;

                double frac = 0.5;
                if (DltDondeEnLaVela == DondeEnLaVela.En_el_instante_exacto && b > a)
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
                    Precio = f.Precio,
                    Contratos = f.Tamano,
                    Vara = f.Vara,
                    Pura = f.Pura,
                    X = x,
                    Y = y,
                    D = 0
                });
            }
            if (marcas.Count == 0) { Rotulo(g, area); return; }

            // 3) juntar las que estan a pocos ticks: se SUMAN, nunca se corren de precio
            if (DltJuntarTicks > 0) marcas = Juntar(marcas);

            // 4) el diametro, y el tope por vela con las mas grandes
            for (int i = 0; i < marcas.Count; i++)
            {
                var m = marcas[i];
                m.D = Diametro(m.Contratos, m.Vara);
                marcas[i] = m;
            }
            marcas = Tope(marcas, out _tapadas);

            // 5) a pintar: primero las olas, despues las otras familias
            var fuente = new RenderFont("Consolas", (float)Math.Max(5.0, DltLetra));
            try { g.SetSmoothingMode(OFT.Rendering.Context.RenderSmoothingModes.AntiAlias); } catch { }
            for (int paso = 0; paso < 3; paso++)
            {
                int tipo = paso == 0 ? Tipos.Ola : (paso == 1 ? Tipos.Contrapie : Tipos.Traba);
                for (int i = 0; i < marcas.Count; i++)
                {
                    if (marcas[i].Tipo != tipo) continue;
                    Dibujar(g, fuente, marcas[i]);
                }
            }
            try { g.SetSmoothingMode(OFT.Rendering.Context.RenderSmoothingModes.Default); } catch { }
            Rotulo(g, area);
        }

        /// <summary>El ROMBO. No es un circulo a proposito: en la pantalla ya hay dos pelotitas
        /// (Big Trades y Absorcion Viva) y la forma es lo que se lee de lejos, antes que el color.
        /// Y no tiene punta: este indicador no dice para donde va el precio.</summary>
        private void Dibujar(RenderContext g, RenderFont fuente, Marca m)
        {
            var col = Pintura(m.Lado);
            int alfa = (int)Math.Round(255.0 * Math.Max(5, Math.Min(100, DltOpacidad)) / 100.0);
            int r = Math.Max(2, m.D / 2);
            var pts = new[]
            {
                new Point(m.X, m.Y - r),
                new Point(m.X + r, m.Y),
                new Point(m.X, m.Y + r),
                new Point(m.X - r, m.Y)
            };

            if (m.Tipo == Tipos.Traba)
            {
                // la traba es un CUADRADO, no un rombo: si alguna vez aparece, que no se confunda
                g.DrawRectangle(new RenderPen(Color.FromArgb(Math.Min(255, alfa + 70), col),
                                              (float)DltGrosorContorno),
                                new Rectangle(m.X - r, m.Y - r, 2 * r, 2 * r));
            }
            else if (m.Tipo == Tipos.Contrapie)
            {
                var pluma = new RenderPen(Color.FromArgb(Math.Min(255, alfa + 40), col),
                                          (float)DltGrosorContorno);
                try { pluma.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot; } catch { }
                g.DrawPolygon(pluma, pts);
            }
            else if (m.Pura)
            {
                // la ola PURA va hueca: mismo tamano, mismo color, solo el contorno
                g.DrawPolygon(new RenderPen(Color.FromArgb(Math.Min(255, alfa + 90), col),
                                            (float)DltGrosorContorno), pts);
            }
            else
            {
                g.FillPolygon(Color.FromArgb(alfa, col), pts);
            }
            Numero(g, fuente, col, m, m.D);
        }

        private void Numero(RenderContext g, RenderFont f, Color col, Marca m, int d)
        {
            if (DltVerNumero == CuandoElNumero.Nunca) return;
            if (DltVerNumero == CuandoElNumero.Solo_las_grandes
                && m.Contratos < m.Vara * (DltNumeroDesdeVeces / 10.0)) return;
            string s = ((long)Math.Round(m.Contratos)).ToString(Inv);
            var t = g.MeasureString(s, f);
            int x = m.X + d / 2 + 3;
            int y = m.Y - t.Height / 2;
            g.FillRectangle(Color.FromArgb(160, ColFondo), new Rectangle(x - 2, y, t.Width + 4, t.Height));
            g.DrawString(s, f, Color.FromArgb(235, col), x, y);
        }

        private Color Pintura(int lado)
        {
            bool compras = DltInvertirColores ? lado < 0 : lado > 0;
            var c = compras ? DltColorCompras : DltColorVentas;
            return Color.FromArgb(255, c.R, c.G, c.B);
        }

        /// <summary>El diametro va en VECES LA VARA y no en contratos sueltos, porque la vara
        /// cambia de dia a noche. Crece derecho (lineal) entre los dos extremos.</summary>
        private int Diametro(double contratos, double vara)
        {
            int dmin = Math.Min(DltDiametroMin, DltDiametroMax);
            int dmax = Math.Max(DltDiametroMin, DltDiametroMax);
            if (DltTodasIgual) return (dmin + dmax) / 2;
            if (!(vara > 0)) return dmax;
            double veces = contratos / vara;
            double vmin = DltVecesDiametroMin / 10.0;
            double vmax = DltVecesDiametroMax / 10.0;
            if (vmax <= vmin) return dmax;
            double u = (veces - vmin) / (vmax - vmin);
            if (u < 0) u = 0; else if (u > 1) u = 1;
            return (int)Math.Round(dmin + (dmax - dmin) * u);
        }

        /// <summary>Junta las marcas del mismo tipo, mismo lado, misma vela y a pocos ticks en UNA
        /// sola que suma los contratos y se queda EN EL PRECIO de la mas grande. Nunca se corre una
        /// marca de su precio: eso romperia el requisito numero uno. Una fusionada queda llena aunque
        /// alguna de las juntadas fuera pura: el hueco dice "esta ola fue de un solo lado" y de una
        /// suma de olas eso no se sabe.</summary>
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
                int cuantas = 1;
                while (j + 1 < ms.Count
                       && ms[j + 1].Bar == ms[i].Bar && ms[j + 1].Tipo == ms[i].Tipo
                       && ms[j + 1].Lado == ms[i].Lado
                       && Math.Abs(ms[j + 1].Tk - ms[j].Tk) <= DltJuntarTicks)
                {
                    j++;
                    suma += ms[j].Contratos;
                    cuantas++;
                    if (ms[j].Contratos > mejor.Contratos) mejor = ms[j];
                }
                mejor.Contratos = suma;
                if (cuantas > 1) mejor.Pura = false;
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
            int tope = Math.Max(1, DltTopePorVela);
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
            if (tipo == Tipos.Ola) return DltVerOla;
            if (tipo == Tipos.Contrapie) return DltVerContrapie;
            if (tipo == Tipos.Traba) return DltVerTraba;
            return false;                 // el nido NO se dibuja: su precio es de la convencion
        }

        private bool LadoVisible(int lado)
        {
            if (DltLado == QueLado.Solo_compras) return lado > 0;
            if (DltLado == QueLado.Solo_ventas) return lado < 0;
            return true;
        }

        // =================================================================================
        // El rotulo. La primera linea NO se apaga: sin ella, alguien puede confundir estos
        // rombos con una senal, y no lo son.
        // =================================================================================
        private void Rotulo(RenderContext g, Rectangle area)
        {
            var fT = new RenderFont("Consolas", (float)Math.Max(6.0, DltLetra));
            var f = new RenderFont("Consolas", (float)Math.Max(5.5, DltLetra - 1.0));
            var lineas = new List<Tuple<string, Color, RenderFont>>();
            lineas.Add(Tuple.Create("DELTA VIVO " + Version + " - EN PRUEBA: no validado", ColAviso, fT));
            if (DltVerRotulo)
            {
                int o, p, c, t, nd;
                long ords;
                lock (_llave) { o = _hoyOla; p = _hoyPura; c = _hoyCpie; t = _hoyTrb; nd = _hoyNido; ords = _ordenes; }
                bool rueda = Franja.EnRueda(Reloj.Ns(DateTime.UtcNow));
                int vara = DltPartirDiaNoche && !rueda ? DltVaraFuera : DltVaraRueda;
                lineas.Add(Tuple.Create("describe el empujon de la cinta. NO dice para donde va.",
                                        Color.FromArgb(190, ColTexto), f));
                lineas.Add(Tuple.Create(_estado, Color.FromArgb(190, ColTexto), f));
                lineas.Add(Tuple.Create(string.Format(Inv,
                    "vara ahora {0} contratos netos en {1} s ({2}){3}", vara, DltVentanaSegundos,
                    rueda ? "rueda 09:30-16:00 NY" : "fuera de la rueda",
                    DltPartirDiaNoche ? "" : "  [una sola vara]"), ColTexto, f));
                lineas.Add(Tuple.Create(string.Format(Inv,
                    "eventos hoy (se dibujen o no):  ola {0} (puras {1})   contrapie {2}   traba {3}   nido {4}   (de {5:n0} ordenes)",
                    o, p, c, t, nd, ords), ColTexto, f));
                lineas.Add(Tuple.Create("azul = empujaron las compras   naranja = las ventas   hueco = de un solo lado",
                                        Color.FromArgb(190, ColTexto), f));
                if (_tapadas > 0)
                    lineas.Add(Tuple.Create(string.Format(Inv,
                        "{0} marcas tapadas por el tope de {1} por vela", _tapadas, DltTopePorVela),
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
                                     "ATAS", "pythiagex2-delta.log");
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
        /// <summary>Cuando se escribio la ULTIMA linea, sea cual sea el mensaje. El freno de
        /// arriba mira si el texto cambio, y con eso solo no alcanza: si fallan DOS cosas
        /// distintas a la vez (una en el dibujo y otra en el latido), cada una llega con un texto
        /// distinto del anterior, las dos pasan el filtro y el log vuelve a escribirse decenas de
        /// veces por segundo. Este es el piso que no depende del texto.</summary>
        private static DateTime _ultimaLinea = DateTime.MinValue;
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
                escribir = (msg != _ultimaFalla || (ahora - _ultimaVez).TotalSeconds >= 60)
                           && (ahora - _ultimaLinea).TotalSeconds >= 2.0;
                if (escribir)
                {
                    calladas = _calladas;
                    _calladas = 0;
                    _ultimaFalla = msg;
                    _ultimaVez = ahora;
                    _ultimaLinea = ahora;
                }
                else _calladas++;
            }
            if (escribir) Log(msg + (calladas > 0 ? "  [y " + calladas + " iguales que no se anotaron]" : ""));
        }
    }
}
