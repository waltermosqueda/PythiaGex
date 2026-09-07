using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using MColor = System.Windows.Media.Color;

namespace PythiaVwap
{
    /// <summary>Donde se dibuja la caja de control.</summary>
    public enum EsquinaCaja { ArribaIzquierda, AbajoIzquierda }

    /// <summary>Como se dibuja cada nodo: una muesca corta en el borde izquierdo
    /// (elegido por el operador el 2026-09-07 para no confundirlos con las
    /// lineas de gamma) o una raya punteada que cruza el grafico.</summary>
    public enum EstiloNodo { Muesca, Raya }

    /// <summary>
    /// PythiaFlow - Nodos de Volumen.
    ///
    /// DE DONDE SALE
    /// De dos capturas de un "Luz Flow" corriendo sobre MNQ con barras
    /// `500/500 Order Flow Delta`. En la primera hay lineas amarillas
    /// horizontales sobre el grafico y, al lado, la escalera de volumen por
    /// precio con varias filas subrayadas. Las filas subrayadas son las que
    /// tienen los numeros grandes -- 220 y 237 en 29250, 176 y 161 en 29300,
    /// 226 en 29400, 317 y 408 en 29500 -- y las lineas amarillas caen sobre
    /// esos mismos precios.
    ///
    /// O sea: las lineas no son un calculo escondido. Son los precios donde el
    /// volumen se apelmaso. Eso es un perfil de volumen movil, y se puede
    /// reconstruir entero con lo que ATAS ya trae en cada vela.
    ///
    /// EL COLOREADO NO ESTA ACA
    /// Las velas verdes y rojas de esa captura son otro indicador de este mismo
    /// proyecto, `PythiaFlow - Tendencia de Order Flow`. Este dibuja solamente
    /// los niveles y las marcas de absorcion. Se usan juntos.
    ///
    /// POR QUE LOS UMBRALES SON RELATIVOS Y NO EN CONTRATOS
    /// La captura original tiene 500 y 800 metidos como numeros fijos. Un
    /// umbral fijo sirve para UN instrumento: NQ y ES no mueven el mismo
    /// volumen, y MNQ y MES menos todavia. Aca todo se mide contra la MEDIANA
    /// del propio instrumento en la ventana, asi que el indicador se calibra
    /// solo en ES, NQ, MES y MNQ sin tocar nada. La caja muestra a cuantos
    /// contratos equivale el umbral en cada momento, para poder auditarlo.
    ///
    /// LO QUE MARCA Y LO QUE NO
    /// Un nodo es un precio donde ya se opero mucho. Eso dice donde hay
    /// interes, no para donde va a ir el precio. Sirve como referencia de
    /// donde puede frenar o acelerar, nunca como direccion.
    ///
    /// UN DATO MEDIDO, POR HONESTIDAD
    /// Midiendo SPX el 2026-09-03 salio que la cinta corre 1,102 veces lo
    /// normal sobre los multiplos de 50, 1,012 sobre los de 10 y 0,993 sobre
    /// los de 5 -- sin mirar volumen ni gamma, solo por ser redondos. Por eso
    /// cada nodo avisa si ademas es un numero redondo: si lo es, parte de su
    /// fuerza puede ser eso y no el volumen.
    /// </summary>
    [DisplayName("PythiaFlow - Nodos de Volumen")]
    [Category("PythiaGex")]
    public class NodosDeVolumen : Indicator
    {
        private sealed class Nodo
        {
            public decimal Precio;
            public decimal Volumen;
            public decimal Delta;
            public double Fuerza;      // cuantas veces la mediana
            public int Redondez;       // 0 = comun, 10, 25, 50, 100
            public bool Flojo;         // no llego al umbral: se muestra igual, apagado
        }

        private readonly ValueDataSeries _absUp;
        private readonly ValueDataSeries _absDn;

        private readonly object _llave = new object();
        private List<Nodo> _nodos = new List<Nodo>();
        private int _barraPerfil = -1;
        private decimal _medianaNivel;      // volumen tipico de un precio
        private decimal _umbralNodo;
        private int _barrasUsadas;
        private decimal _volMedioPorPrecioBarra;
        private string _firma = "";
        private Rectangle _cajaRect = Rectangle.Empty;
        private DateTime _ultimoLogNodos = DateTime.MinValue;
        private int _candidatos;      // cuantos precios pasaron el umbral antes de fusionar
        private int _velasVentana;    // a cuantas velas equivale la ventana en este grafico

        public NodosDeVolumen() : base(true)
        {
            Panel = IndicatorDataProvider.CandlesPanel;
            DenyToChangePanel = true;

            var cero = (ValueDataSeries)DataSeries[0];
            cero.VisualType = VisualMode.Hide;
            cero.IsHidden = true;
            cero.IgnoredByAlerts = true;

            _absUp = new ValueDataSeries("absup", "Absorcion en el piso")
            {
                VisualType = VisualMode.UpArrow,
                Color = MColor.FromArgb(255, 60, 210, 110),
                Width = 2,
                ShowZeroValue = false,
                ShowCurrentValue = false,
            };
            _absDn = new ValueDataSeries("absdn", "Absorcion en el techo")
            {
                VisualType = VisualMode.DownArrow,
                Color = MColor.FromArgb(255, 235, 60, 90),
                Width = 2,
                ShowZeroValue = false,
                ShowCurrentValue = false,
            };
            DataSeries.Add(_absUp);
            DataSeries.Add(_absDn);

            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
        }

        // ==================================================================
        // Ajustes
        // ==================================================================

        [Display(Name = "Ventana en horas de mercado (en vez de velas)", GroupName = "1. Nodos", Order = 5,
                 Description = "Con la ventana en VELAS el mismo indicador contaba cosas distintas en "
                             + "cada pestaña: 300 velas de 1 minuto son 5 horas y 300 de 5 minutos son "
                             + "25. Medido el 2026-09-06: el precio 7721,25 decia 26,7K con delta -479 "
                             + "en el grafico de 1 minuto y 69,2K con delta +248 en el de 5, al mismo "
                             + "tiempo. Con la ventana en horas los dos graficos miran el mismo tramo. "
                             + "Son horas DE MERCADO: un fin de semana no cuenta. La primera version "
                             + "usaba horas de reloj y el domingo a la noche la ventana de 24 h tenia "
                             + "37 velas de 5 minutos, porque el sabado no existe.")]
        public bool VentanaEnHoras { get; set; } = true;

        [Display(Name = "Horas de mercado hacia atras", GroupName = "1. Nodos", Order = 6,
                 Description = "Cuantas horas de velas cerradas entran en el perfil, contando solo "
                             + "velas que existen. 24 cubre la rueda anterior entera. Se convierte a "
                             + "velas con la duracion mediana de las ultimas 60 velas del grafico.")]
        [Range(1.0, 240.0)]
        public double Horas { get; set; } = 24.0;

        [Display(Name = "Velas hacia atras (si la ventana no es en horas)", GroupName = "1. Nodos", Order = 10,
                 Description = "Cuantas velas cerradas entran en el perfil.")]
        [Range(20, 5000)]
        public int Barras { get; set; } = 300;

        [Display(Name = "Fuerza minima (veces la mediana)", GroupName = "1. Nodos", Order = 20,
                 Description = "Un precio es nodo si acumulo esta cantidad de veces "
                             + "el volumen del precio tipico de la ventana. Al ser "
                             + "relativo, no hay que retocarlo entre ES y NQ.")]
        [Range(1.5, 50.0)]
        public double Fuerza { get; set; } = 2.5;

        // RENOMBRADA (era MaxNodos = 8): el operador vio "puras rayas amarillas
        // iguales" el 2026-09-06. Ocho lineas del mismo color en 8 puntos de
        // rango no jerarquizan nada. Cuatro, con grosor y brillo por ranking, y
        // etiqueta solo en las tres primeras. ATAS guarda el valor viejo en el
        // workspace, asi que hay que cambiar el nombre para que llegue el 4.
        [Display(Name = "Cuantos nodos como maximo", GroupName = "1. Nodos", Order = 30,
                 Description = "Cuatro: el primero grueso y brillante, el cuarto fino y apagado. "
                             + "Mas que eso y la pantalla vuelve a ser una pila de rayas iguales.")]
        [Range(1, 40)]
        public int MaxNodosDibujados { get; set; } = 4;
        private int MaxNodos => MaxNodosDibujados;

        [Display(Name = "Etiquetas solo en los primeros N", GroupName = "1. Nodos", Order = 32,
                 Description = "Los demas nodos llevan la linea pero no el texto: se ve la "
                             + "jerarquia sin leer ocho numeros.")]
        [Range(0, 40)]
        public int EtiquetasMax { get; set; } = 3;

        [Display(Name = "Minimo que se muestra igual", GroupName = "1. Nodos", Order = 35,
                 Description = "Si ningun precio llega al umbral, igual se dibujan "
                             + "los mas cargados, mas apagados. Una pantalla vacia no "
                             + "se distingue de un indicador roto.")]
        [Range(0, 10)]
        public int MinimoNodos { get; set; } = 3;

        [Display(Name = "Fusionar precios pegados (ticks)", GroupName = "1. Nodos", Order = 40,
                 Description = "Dos precios a menos de esta distancia son el mismo "
                             + "nodo. Sin esto un solo apelmazamiento sale como tres "
                             + "lineas juntas.")]
        [Range(0, 20)]
        public int FusionTicks { get; set; } = 2;

        [Display(Name = "Ver nodos", GroupName = "1. Nodos", Order = 50)]
        public bool VerNodos { get; set; } = true;

        [Display(Name = "Avisar si el precio es redondo", GroupName = "1. Nodos", Order = 60,
                 Description = "Medido: la cinta corre mas rapido sobre los multiplos "
                             + "de 50 sin importar el volumen. Si un nodo ademas es "
                             + "redondo, parte de su fuerza puede venir de ahi.")]
        public bool AvisarRedondos { get; set; } = true;

        [Display(Name = "Marcar absorcion", GroupName = "2. Absorcion", Order = 10,
                 Description = "Flecha donde entro agresion contra el extremo y el "
                             + "precio no la siguio.")]
        public bool VerAbsorcion { get; set; } = true;

        [Display(Name = "Velas a cada lado del pivote", GroupName = "2. Absorcion", Order = 20,
                 Description = "Cuantas velas tienen que quedar por encima (o por "
                             + "debajo) para que el extremo cuente como pivote. Mas "
                             + "alto = menos señales y mas tarde.")]
        [Range(1, 20)]
        public int Pivote { get; set; } = 3;

        [Display(Name = "Ticks del extremo que se miran", GroupName = "2. Absorcion", Order = 30)]
        [Range(0, 20)]
        public int TicksExtremo { get; set; } = 2;

        [Display(Name = "Agresion minima (veces lo normal)", GroupName = "2. Absorcion", Order = 40,
                 Description = "Cuanto volumen tiene que haber en el extremo, medido "
                             + "contra el volumen tipico de un precio en esa vela.")]
        [Range(1.0, 30.0)]
        public double FuerzaAbsorcion { get; set; } = 3.0;

        [Display(Name = "Donde tiene que cerrar (0 a 1)", GroupName = "2. Absorcion", Order = 50,
                 Description = "0,6 = la vela tiene que cerrar en el 40% de arriba de "
                             + "su rango para que el piso cuente como absorbido.")]
        [Range(0.0, 1.0)]
        public double CierreMinimo { get; set; } = 0.6;

        [Display(Name = "Rango minimo de la vela (ticks)", GroupName = "2. Absorcion", Order = 60,
                 Description = "Si la vela mide menos que esto, el 'extremo' es la vela entera y no "
                             + "hay resto contra que comparar. En MES de 1 minuto en Asia las velas "
                             + "miden 2 o 3 ticks y la regla vieja disparaba en todas.")]
        [Range(2, 60)]
        public int RangoMinimoTicks { get; set; } = 6;

        [Display(Name = "Velas para la mediana de volumen", GroupName = "2. Absorcion", Order = 70,
                 Description = "Una vela con menos volumen que la mediana de las ultimas N no absorbe "
                             + "nada, por concentrado que tenga el extremo.")]
        [Range(3, 200)]
        public int VelasMediana { get; set; } = 20;

        [Display(Name = "Registrar cada flecha en un archivo", GroupName = "2. Absorcion", Order = 80,
                 Description = "Escribe en %APPDATA%\\ATAS\\pythiaflow-absorcion.log los numeros de cada "
                             + "flecha (y de las que estuvieron cerca): rango, volumen, concentracion, "
                             + "delta del extremo, cierre. Una flecha sin sus numeros no se puede discutir.")]
        public bool RegistrarAbsorcion { get; set; } = true;

        [Display(Name = "Ver la caja de control", GroupName = "3. Pantalla", Order = 10)]
        public bool VerCaja { get; set; } = true;

        [Display(Name = "Margen de la escala de precios", GroupName = "3. Pantalla", Order = 12,
                 Description = "ChartArea.Right incluye la escala de precios, asi que "
                             + "la etiqueta pegada al borde queda cortada por los "
                             + "numeros del eje. Medido: hacen falta unos 70 pixeles.")]
        [Range(0, 400)]
        public int MargenEje { get; set; } = 70;

        // RENOMBRADA (era MargenSuperior = 34). ATAS guarda el valor de cada
        // propiedad en el workspace y ese guardado le gana a cualquier default
        // nuevo: para que el 90 llegue a la pantalla del operador hay que
        // cambiar el nombre. Medido el 2026-09-06: la marca de agua, el aviso
        // de Gamma Vivo y la leyenda de dos indicadores ocupan ~82 px.
        [Display(Name = "Margen de arriba de la caja", GroupName = "3. Pantalla", Order = 15,
                 Description = "ATAS pone arriba a la izquierda su marca de agua y la leyenda de "
                             + "indicadores; Gamma Vivo pone ahi sus avisos. Con 90 la caja queda "
                             + "debajo de todo eso y no se pisan (medido con dos indicadores).")]
        [Range(0, 400)]
        public int MargenArriba { get; set; } = 90;

        // RENOMBRADA (era Esquina = AbajoIzquierda), por el mismo motivo.
        [Display(Name = "Donde va la caja", GroupName = "3. Pantalla", Order = 14,
                 Description = "Abajo a la izquierda pisaba el panel de cuenta que el operador tiene "
                             + "en el grafico de MNQ (visto el 2026-09-06). Arriba a la izquierda, "
                             + "debajo de la leyenda, no hay nada mas.")]
        public EsquinaCaja EsquinaCajaControl { get; set; } = EsquinaCaja.ArribaIzquierda;

        [Display(Name = "Margen de abajo", GroupName = "3. Pantalla", Order = 16,
                 Description = "ChartArea es mas alto que lo visible: lo pegado al fondo cae detras "
                             + "del eje de tiempo. Medido en Gamma Vivo: hacen falta unos 48 pixeles.")]
        [Range(0, 400)]
        public int MargenInferior { get; set; } = 48;

        [Display(Name = "Corrimiento de las etiquetas hacia la izquierda", GroupName = "3. Pantalla", Order = 18,
                 Description = "Las etiquetas de los nodos se corren esta cantidad de pixeles a la izquierda "
                             + "para no compartir columna con los chips de Gamma Vivo, que viven pegados al "
                             + "eje. Medido el 2026-09-06: los chips de dos lineas miden hasta 300 px.")]
        [Range(0, 900)]
        public int DesplazarEtiquetas { get; set; } = 330;

        [Display(Name = "Panel de cuenta abajo a la izquierda: alto (px)", GroupName = "3. Pantalla", Order = 18,
                 Description = "El panel de cuenta de ATAS (Account, Balance, Open PnL) vive abajo a la izquierda "
                             + "del grafico de MNQ y tapaba la etiqueta del nodo #2 (visto el 2026-09-07 04:00). "
                             + "Las etiquetas que caen en esa zona se corren a la derecha del panel. 0 = no hay panel.")]
        [Range(0, 600)]
        public int AltoPanelCuenta { get; set; } = 160;

        [Display(Name = "Panel de cuenta abajo a la izquierda: ancho (px)", GroupName = "3. Pantalla", Order = 18)]
        [Range(0, 900)]
        public int AnchoPanelCuenta { get; set; } = 260;

        [Display(Name = "Estilo del nodo", GroupName = "3. Pantalla", Order = 19,
                 Description = "Muesca: una marca corta en el borde izquierdo, a la derecha de las barras " +
                               "de Gamma Vivo, con su etiqueta al lado. Raya: la linea punteada de antes.")]
        public EstiloNodo Estilo { get; set; } = EstiloNodo.Muesca;

        [Display(Name = "Donde arranca la muesca (px desde el borde)", GroupName = "3. Pantalla", Order = 19,
                 Description = "Las barras de Gamma Vivo ocupan unos 78 px a la izquierda; la muesca va despues.")]
        [Range(0, 400)]
        public int MargenMuesca { get; set; } = 84;

        [Display(Name = "Color de los nodos", GroupName = "3. Pantalla", Order = 20)]
        public MColor ColorNodo { get; set; } = MColor.FromArgb(255, 235, 200, 60);

        // ==================================================================
        // Calculo
        // ==================================================================

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0)
            {
                lock (_llave) { _nodos = new List<Nodo>(); _barraPerfil = -1; }
                return;
            }
            try { MarcarAbsorcion(bar); } catch { }

            // El perfil se rehace una vez por vela cerrada. Recorrer 300 velas
            // con su footprint en cada tick tiraria abajo el grafico.
            //
            // PERO tambien hay que rehacerlo cuando cambian los ajustes. Sin
            // esto, con el mercado cerrado no cierra ninguna vela nueva, el
            // perfil no se recalcula nunca y mover "fuerza minima" no hace
            // nada: la caja sigue mostrando el umbral viejo y parece roto.
            // Paso exactamente eso la primera vez que se probo.
            // SOLO EN EL BORDE VIVO. Esto no es una optimizacion: sin esto el
            // indicador CUELGA la plataforma.
            //
            // Cuando ATAS calcula el historico llama a OnCalculate una vez por
            // cada vela del grafico. Un MNQ de un minuto tiene miles. Si el
            // perfil se rehace en cada una, son miles de recorridos de 300
            // velas con su footprint: millones de operaciones antes de dibujar
            // el primer pixel. Paso exactamente eso: en MES de 5 minutos, con
            // pocas velas, no se noto; al ponerlo en MNQ de 1 minuto la
            // plataforma quedo clavada en "Loading..." y hubo que matarla.
            //
            // El perfil solo tiene sentido para el momento actual, asi que se
            // calcula una sola vez, cuando el recorrido llega al final.
            if (bar < CurrentBar - 1) return;

            int ultima = bar - 1;
            var firma = Firma();
            if (ultima != _barraPerfil || firma != _firma)
            {
                _barraPerfil = ultima;
                _firma = firma;
                try { RehacerPerfil(ultima); } catch { }
            }
        }

        /// <summary>Los ajustes que cambian el perfil, en una sola cadena.</summary>
        private string Firma()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3}|{4}|{5}|{6}",
                                 Barras, Fuerza, MaxNodos, FusionTicks, MinimoNodos,
                                 VentanaEnHoras, Horas);
        }

        /// <summary>Suma el volumen por precio de las ultimas velas y saca los nodos.</summary>
        private void RehacerPerfil(int ultima)
        {
            // LA VENTANA EN HORAS, NO EN VELAS.
            //
            // Con la ventana en velas, el mismo indicador en dos pestañas
            // decia dos cosas distintas del mismo precio (ver el ajuste). Aca
            // se camina hacia atras desde la ultima vela cerrada hasta que la
            // distancia en horas supera la ventana. Con un tope de vueltas por
            // si el grafico tiene decenas de miles de velas.
            // HORAS DE MERCADO, NO DE RELOJ.
            //
            // La primera version caminaba hacia atras por la hora de cada vela.
            // El domingo 2026-09-06 a la noche eso dio "37 velas (24 h)" en MES
            // de 5 minutos: 24 horas de reloj hacia atras caen en el sabado, y
            // el sabado no tiene velas. Contar horas de mercado es contar velas:
            // la duracion mediana de las ultimas velas del grafico dice cuantas
            // velas son 24 horas. En 1 minuto son 1440, en 5 minutos 288, y las
            // dos pestañas miran el mismo tramo de mercado.
            int velasVentana = Barras;
            if (VentanaEnHoras)
            {
                double durMin = DuracionMedianaVela(ultima, 60);
                if (durMin > 0)
                    velasVentana = (int)Math.Max(20, Math.Min(20000, Math.Round(Horas * 60.0 / durMin)));
            }
            int desde = Math.Max(0, ultima - velasVentana + 1);
            lock (_llave) _velasVentana = velasVentana;
            var acum = new Dictionary<decimal, Nodo>();
            int n = 0;
            decimal volTotal = 0;
            long nivelesVistos = 0;

            for (int b = desde; b <= ultima; b++)
            {
                var c = GetCandle(b);
                if (c == null) continue;
                n++;
                try
                {
                    foreach (var l in c.GetAllPriceLevels())
                    {
                        if (l == null || l.Volume <= 0) continue;
                        if (!acum.TryGetValue(l.Price, out var nd))
                            acum[l.Price] = nd = new Nodo { Precio = l.Price };
                        nd.Volumen += l.Volume;
                        nd.Delta += l.Ask - l.Bid;
                        volTotal += l.Volume;
                        nivelesVistos++;
                    }
                }
                catch { /* una vela sin footprint no invalida la ventana */ }
            }

            if (acum.Count == 0)
            {
                lock (_llave) { _nodos = new List<Nodo>(); _barrasUsadas = n; }
                return;
            }

            // La MEDIANA, no el promedio. El promedio lo levanta el propio nodo
            // que estamos buscando y entonces el umbral se corre hacia arriba
            // justo cuando aparece lo que queremos detectar.
            var vols = new List<decimal>(acum.Count);
            foreach (var kv in acum) vols.Add(kv.Value.Volumen);
            vols.Sort();
            decimal mediana = vols[vols.Count / 2];
            if (mediana <= 0) mediana = 1;
            decimal umbral = mediana * (decimal)Fuerza;

            // Todos los precios, ordenados de mas a menos volumen. El umbral se
            // aplica despues, no acá, porque hace falta saber CUANTOS pasaron
            // para poder decirlo en la caja.
            var todos = new List<Nodo>(acum.Count);
            foreach (var kv in acum)
            {
                kv.Value.Fuerza = (double)(kv.Value.Volumen / mediana);
                todos.Add(kv.Value);
            }
            todos.Sort((a, b2) => b2.Volumen.CompareTo(a.Volumen));

            int pasaron = 0;
            foreach (var x in todos) if (x.Volumen >= umbral) pasaron++;

            // SI NINGUNO PASA, IGUAL SE MUESTRAN LOS MAS CARGADOS.
            //
            // Con 300 velas de 5 minutos el perfil abarca 25 horas: el volumen
            // se reparte entre cientos de precios y ninguno llega a 4 veces la
            // mediana. Con el filtro a secas el indicador dibujaba NADA y
            // parecia roto -- paso en la primera prueba. Mostrar los mas
            // cargados con un aviso es mas util y mas honesto que una pantalla
            // vacia: la caja dice cuantos pasaron de verdad y los que no
            // pasaron van mas apagados.
            var cand = new List<Nodo>();
            foreach (var x in todos)
            {
                bool paso = x.Volumen >= umbral;
                if (!paso && cand.Count >= MinimoNodos) break;
                x.Flojo = !paso;
                cand.Add(x);
                if (cand.Count >= MaxNodos * 3) break;   // margen para la fusion
            }

            // Fusion: dos precios pegados son un apelmazamiento, no dos.
            decimal tick = 0;
            try { tick = InstrumentInfo != null ? InstrumentInfo.TickSize : 0; } catch { }
            if (tick <= 0) tick = 0.25m;
            decimal luz = tick * FusionTicks;

            var salida = new List<Nodo>();
            foreach (var c2 in cand)
            {
                bool pegado = false;
                foreach (var y in salida)
                {
                    if (Math.Abs(y.Precio - c2.Precio) <= luz)
                    {
                        // el precio del nodo es el del pico, que ya esta en la
                        // lista por venir ordenada de mayor a menor
                        y.Volumen += c2.Volumen;
                        y.Delta += c2.Delta;
                        y.Fuerza = (double)(y.Volumen / mediana);
                        pegado = true;
                        break;
                    }
                }
                if (pegado) continue;
                c2.Redondez = Redondez(c2.Precio);
                salida.Add(c2);
                if (salida.Count >= MaxNodos) break;
            }

            lock (_llave)
            {
                _nodos = salida;
                _barrasUsadas = n;
                _medianaNivel = mediana;
                _umbralNodo = umbral;
                _candidatos = pasaron;
                _volMedioPorPrecioBarra = nivelesVistos > 0
                    ? volTotal / nivelesVistos : 0;
            }
            PublicarNodos(salida);
        }

        /// <summary>
        /// EL PUENTE HACIA GAMMA VIVO, SIN ARCHIVOS.
        ///
        /// Los dos indicadores corren en el mismo proceso de ATAS, asi que se
        /// pueden pasar datos por AppDomain.SetData, con una clave por
        /// instrumento. Gamma Vivo lo lee para armar la ESCALERA: los peldanos
        /// mas cercanos al precio, mezclando nodos de volumen y niveles de
        /// gamma en una sola lista ordenada. Pedido por el operador el
        /// 2026-09-06: "en que nivel estamos, cual es el siguiente".
        ///
        /// Formato: "unix;precio|vol|delta|rango|flojo;precio|...". Con el sello
        /// para que el lector descarte datos viejos si este indicador se saco
        /// del grafico.
        /// </summary>
        private void PublicarNodos(List<Nodo> nodos)
        {
            try
            {
                string inst = (InstrumentInfo != null ? InstrumentInfo.Instrument : "").ToUpperInvariant().TrimStart('#');
                if (inst.Length == 0) return;
                var inv = CultureInfo.InvariantCulture;
                var sb = new System.Text.StringBuilder();
                sb.Append(DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(inv));
                int r = 0;
                foreach (var nd in nodos)
                {
                    r++;
                    sb.Append(';').Append(nd.Precio.ToString(inv)).Append('|')
                      .Append(nd.Volumen.ToString("0", inv)).Append('|')
                      .Append(nd.Delta.ToString("0", inv)).Append('|')
                      .Append(r).Append('|').Append(nd.Flojo ? 1 : 0);
                }
                AppDomain.CurrentDomain.SetData("PythiaFlow.Nodos." + inst, sb.ToString());
            }
            catch { }
        }

        /// <summary>Duracion mediana de una vela, en minutos, sobre las ultimas n.
        /// Se descartan los saltos (noche, fin de semana) mayores a seis veces la
        /// mediana cruda antes de recalcular. Devuelve 0 si no se puede saber
        /// (velas que no son de tiempo).</summary>
        private double DuracionMedianaVela(int ultima, int n)
        {
            var d = new List<double>();
            for (int i = Math.Max(1, ultima - n + 1); i <= ultima; i++)
            {
                var a = GetCandle(i - 1); var b = GetCandle(i);
                if (a == null || b == null) continue;
                double m = (b.Time - a.Time).TotalMinutes;
                if (m > 0) d.Add(m);
            }
            if (d.Count < 3) return 0;
            d.Sort();
            double med = d[d.Count / 2];
            var sin = new List<double>();
            foreach (var x in d) if (x <= med * 6) sin.Add(x);
            if (sin.Count < 3) return med;
            sin.Sort();
            return sin[sin.Count / 2];
        }

        /// <summary>Que tan redondo es un precio. Devuelve 0 si no lo es.</summary>
        private static int Redondez(decimal p)
        {
            decimal e = Math.Round(p);
            if (e != p) return 0;
            long v = (long)e;
            if (v % 100 == 0) return 100;
            if (v % 50 == 0) return 50;
            if (v % 25 == 0) return 25;
            if (v % 10 == 0) return 10;
            return 0;
        }

        /// <summary>
        /// Absorcion: entro agresion contra el extremo y el precio no la siguio.
        ///
        /// Se pide un pivote CONFIRMADO, con velas a los dos lados. Eso llega
        /// tarde a proposito: un extremo sin confirmar todavia no es un
        /// extremo, y marcarlo seria dibujar una señal que se puede borrar
        /// sola.
        /// </summary>
        private void MarcarAbsorcion(int bar)
        {
            _absUp[bar] = 0;
            _absDn[bar] = 0;
            if (!VerAbsorcion) return;

            int b = bar - Pivote;                 // la vela candidata
            if (b - Pivote < 0) return;

            var c = GetCandle(b);
            if (c == null) return;
            decimal rango = c.High - c.Low;
            if (rango <= 0) return;

            bool esPiso = true, esTecho = true;
            for (int i = b - Pivote; i <= b + Pivote; i++)
            {
                if (i == b) continue;
                var o = GetCandle(i);
                if (o == null) continue;
                if (o.Low < c.Low) esPiso = false;
                if (o.High > c.High) esTecho = false;
            }
            if (!esPiso && !esTecho) return;

            decimal tick = 0;
            try { tick = InstrumentInfo != null ? InstrumentInfo.TickSize : 0; } catch { }
            if (tick <= 0) tick = 0.25m;

            // LA REGLA VIVE EN AbsorcionRegla.cs, SIN ATAS ADENTRO, Y TIENE
            // PRUEBAS (atas/_test_flow). La version anterior estaba aca mismo y
            // era degenerada: sumaba tres niveles y los dividia por el promedio
            // de un nivel, asi que una vela pareja daba exactamente 3, el
            // umbral. Trece flechas en 84 velas de un rango de 5 puntos, en la
            // noche del domingo 2026-09-06. La prueba que lo demuestra es la
            // primera del arnes.
            var niveles = new List<NivelPV>();
            try
            {
                foreach (var l in c.GetAllPriceLevels())
                {
                    if (l == null || l.Volume <= 0) continue;
                    niveles.Add(new NivelPV { Precio = l.Price, Volumen = l.Volume, Delta = l.Ask - l.Bid });
                }
            }
            catch { return; }
            if (niveles.Count == 0) return;

            // una vela con menos volumen que la mediana de las recientes no
            // absorbe nada, por concentrado que tenga el extremo
            decimal medVelas = MedianaVolumenVelas(b, Math.Max(3, VelasMediana));

            if (esPiso)
            {
                var r = AbsorcionRegla.Evaluar(niveles, c.Low, c.High, c.Close, true, tick,
                                               TicksExtremo, RangoMinimoTicks, FuerzaAbsorcion,
                                               CierreMinimo, medVelas);
                if (r.Dispara) _absUp[b] = c.Low - tick * 2;
                RegistrarAbs(b, c, r);
            }
            if (esTecho)
            {
                var r = AbsorcionRegla.Evaluar(niveles, c.Low, c.High, c.Close, false, tick,
                                               TicksExtremo, RangoMinimoTicks, FuerzaAbsorcion,
                                               CierreMinimo, medVelas);
                if (r.Dispara) _absDn[b] = c.High + tick * 2;
                RegistrarAbs(b, c, r);
            }
        }

        /// <summary>Mediana del volumen de las N velas anteriores a b.</summary>
        private decimal MedianaVolumenVelas(int b, int n)
        {
            var v = new List<decimal>();
            for (int i = Math.Max(0, b - n); i < b; i++)
            {
                var o = GetCandle(i);
                if (o != null && o.Volume > 0) v.Add(o.Volume);
            }
            if (v.Count == 0) return 0;
            v.Sort();
            return v[v.Count / 2];
        }

        /// <summary>
        /// CADA FLECHA CON SUS NUMEROS, EN UN ARCHIVO.
        ///
        /// Se escriben todas las que dispararon (para poder auditar lo que hay
        /// en pantalla) y, solo en el borde vivo, las que pasaron la
        /// concentracion y fallaron por otra condicion: son las que el operador
        /// va a ver "casi" como absorcion y conviene poder explicar por que no.
        /// </summary>
        private void RegistrarAbs(int b, IndicatorCandle c, ResultadoAbsorcion r)
        {
            if (!RegistrarAbsorcion) return;
            bool borde = b >= CurrentBar - 3;
            if (!r.Dispara && !(borde && r.Concentracion >= FuerzaAbsorcion)) return;
            try
            {
                var p = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ATAS", "pythiaflow-absorcion.log");
                string inst = "";
                try { inst = InstrumentInfo != null ? InstrumentInfo.Instrument : ""; } catch { }
                string marco = "";
                try { marco = ChartInfo != null ? ChartInfo.ChartType + "-" + ChartInfo.TimeFrame : ""; } catch { }
                var inv = CultureInfo.InvariantCulture;
                System.IO.File.AppendAllText(p, string.Format(inv,
                    "{0:s} {1} {2} bar={3} vela={4:s} L={5} H={6} C={7} {8}\n",
                    DateTime.Now, inst, marco, b, c.Time, c.Low, c.High, c.Close, r.Linea(inv)));
            }
            catch { }
        }

        // ==================================================================
        // Dibujo
        // ==================================================================

        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            if (ChartInfo == null) return;
            try
            {
                // RECALCULAR ACA TAMBIEN, Y NO SOLO EN OnCalculate.
                //
                // Con el mercado cerrado no entra un solo tick, asi que
                // OnCalculate NO se ejecuta: cambiar "fuerza minima" no hacia
                // absolutamente nada y la caja seguia mostrando el umbral
                // viejo. Parecia que el ajuste estaba roto. El render, en
                // cambio, corre siempre. Solo se rehace cuando la firma
                // cambio, o sea cuando el usuario movio un ajuste.
                var firma = Firma();
                if (firma != _firma && CurrentBar > 1)
                {
                    _firma = firma;
                    _barraPerfil = CurrentBar - 1;
                    try { RehacerPerfil(CurrentBar - 1); } catch { }
                }
                if (VerNodos) PintarNodos(g);
                if (VerCaja) PintarCaja(g);
            }
            catch { /* el render nunca puede tirar abajo el grafico */ }
        }

        private void PintarNodos(RenderContext g)
        {
            List<Nodo> nodos;
            lock (_llave) nodos = new List<Nodo>(_nodos);
            if (nodos.Count == 0) return;

            var cont = ChartInfo.PriceChartContainer;
            if (cont == null) return;
            var area = ChartArea;
            var f = new RenderFont("Arial", 8.5f);
            var cultura = CultureInfo.GetCultureInfo("es-AR");
            var col = Color.FromArgb(ColorNodo.A, ColorNodo.R, ColorNodo.G, ColorNodo.B);

            double maxF = 1;
            foreach (var n in nodos) if (n.Fuerza > maxF) maxF = n.Fuerza;

            // el precio y la volatilidad realizada que publica Gamma Vivo, para
            // la distancia y la chance de toque en las etiquetas
            double px = 0, sigH = 0;
            try
            {
                string inst0 = (InstrumentInfo != null ? InstrumentInfo.Instrument : "").ToUpperInvariant().TrimStart('#');
                var rawP = AppDomain.CurrentDomain.GetData("PythiaGex.Prob." + inst0) as string;
                if (!string.IsNullOrEmpty(rawP))
                {
                    var q = rawP.Split(';');
                    if (q.Length >= 3 && long.TryParse(q[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tsP)
                        && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - tsP < 120)
                    {
                        double.TryParse(q[1], NumberStyles.Float, CultureInfo.InvariantCulture, out px);
                        double.TryParse(q[2], NumberStyles.Float, CultureInfo.InvariantCulture, out sigH);
                    }
                }
            }
            catch { }

            // las rayas de gamma que publica Gamma Vivo (si esta en el grafico)
            var ysGamma = new List<int>();
            try
            {
                string inst = (InstrumentInfo != null ? InstrumentInfo.Instrument : "").ToUpperInvariant().TrimStart('#');
                var raw = AppDomain.CurrentDomain.GetData("PythiaGex.Niveles." + inst) as string;
                if (!string.IsNullOrEmpty(raw))
                {
                    var partes = raw.Split(';');
                    if (partes.Length > 1 && long.TryParse(partes[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ts)
                        && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts < 600)
                        for (int i = 1; i < partes.Length; i++)
                            if (double.TryParse(partes[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var p))
                                try { ysGamma.Add(cont.GetYByPrice((decimal)p, false)); } catch { }
                }
            }
            catch { }

            int fuera = 0;
            decimal precioArriba = 0, precioAbajo = 0;

            // JERARQUIA POR RANKING, NO SOLO POR FUERZA RELATIVA.
            //
            // Con la fuerza relativa sola, cuatro nodos de 60K, 55K, 51K y 40K
            // salian casi identicos: todos cerca del maximo. Para scalping lo
            // que hace falta leer de un vistazo es CUAL es el primero, cual el
            // segundo. El ranking lo da directo: la lista ya viene ordenada de
            // mayor a menor volumen.
            int rango = 0;
            var usados = new List<Rectangle>();
            var sbLog = new System.Text.StringBuilder();
            foreach (var n in nodos)
            {
                rango++;
                int y;
                try { y = cont.GetYByPrice(n.Precio, false); }
                catch { continue; }
                if (sbLog.Length > 0) sbLog.Append("]");
                sbLog.Append(string.Format(CultureInfo.InvariantCulture, " [#{0} {1} v{2} {3} y={4}",
                             rango, n.Precio, n.Volumen, n.Flojo ? "flojo" : "ok", y));

                // UN NODO FUERA DE PANTALLA NO SE CALLA.
                //
                // Con 300 velas de 5 minutos el perfil abarca 25 horas, y la
                // ventana de precio visible suele ser mucho mas angosta. Si el
                // indicador dibuja en silencio, parece roto. Ya paso.
                if (y < area.Top || y > area.Bottom)
                {
                    fuera++;
                    if (y < area.Top) { if (n.Precio > precioArriba) precioArriba = n.Precio; }
                    else { if (precioAbajo == 0 || n.Precio < precioAbajo) precioAbajo = n.Precio; }
                    continue;
                }

                // el grosor cuenta la historia: un nodo el doble de cargado se
                // ve el doble de firme, sin tener que leer el numero
                float ancho = rango == 1 ? 3.2f : rango == 2 ? 2.4f : rango == 3 ? 1.6f : 1.0f;
                int alfa = rango == 1 ? 235 : rango == 2 ? 185 : rango == 3 ? 140 : 100;
                // PUNTEADAS, TODAS. Pedido del operador el 2026-09-07: con los
                // nodos solidos se confundian con las lineas de gamma. El trazo
                // dice que es: punteado = volumen del futuro (esto), solido =
                // gamma de opciones (Gamma Vivo), rayado = zero gamma. La
                // jerarquia entre nodos la llevan el grosor y el brillo; el que
                // no llego al umbral va fino y apagado.
                var pluma = n.Flojo
                    ? new RenderPen(Color.FromArgb(alfa / 2, col), 1f,
                                    System.Drawing.Drawing2D.DashStyle.Dot)
                    : new RenderPen(Color.FromArgb(alfa, col), ancho,
                                    System.Drawing.Drawing2D.DashStyle.Dot);
                if (Estilo == EstiloNodo.Muesca)
                {
                    // muesca corta: largo por ranking, misma altura de trazo
                    int largo = Math.Max(8, 26 - rango * 4);
                    g.FillRectangle(Color.FromArgb(n.Flojo ? alfa / 2 : alfa, col),
                                    new Rectangle(area.Left + MargenMuesca, y - 2, largo, 4));
                }
                else
                    g.DrawLine(pluma, area.Left, y, Math.Max(area.Left + 10, area.Right - MargenEje + 40), y);

                // etiqueta solo en los primeros: los demas se leen por el grosor
                if (rango > EtiquetasMax) continue;

                string red = "";
                if (AvisarRedondos && n.Redondez > 0)
                    red = "  redondo " + n.Redondez.ToString(CultureInfo.InvariantCulture);
                string sig = n.Delta > 0 ? "+" : "";
                // LA MISMA DISTANCIA Y LA MISMA CHANCE QUE LOS CHIPS DE GAMMA.
                // Pedido del operador el 2026-09-07: "por que los nodos no
                // tienen la misma info". Son otra cosa (volumen del futuro, no
                // gamma de opciones), pero distancia y chance de toque valen
                // igual para los dos. El precio y la volatilidad realizada los
                // publica Gamma Vivo por AppDomain; si no esta, no se escribe.
                string cerca = "";
                if (px > 0)
                {
                    double d = (double)n.Precio - px;
                    cerca = "   " + (d >= 0 ? "+" : "") + d.ToString("0", cultura);
                    if (sigH > 0)
                    {
                        double z = Math.Abs(Math.Log((double)n.Precio / px)) / sigH;
                        double pr = Math.Min(1.0, Math.Max(0.0, 2.0 * Phi(-z)));
                        cerca += " " + (pr * 100).ToString("0", cultura) + "%";
                    }
                }
                // el ranking va escrito ("#1"): asi el nodo de la linea y el de
                // la escalera de Gamma Vivo se reconocen como el mismo
                var txt = string.Format(cultura, "#{5}  {0:N2}   {1}   d {2}{3}{4}{6}",
                                        n.Precio, Corto(n.Volumen), sig, Corto(n.Delta), red, rango, cerca);
                var m = g.MeasureString(txt, f);
                // con muesca la etiqueta va al lado de la muesca, a la izquierda;
                // con raya, corrida a la izquierda de los chips de Gamma Vivo
                int x = Estilo == EstiloNodo.Muesca
                    ? area.Left + MargenMuesca + 32
                    : area.Right - m.Width - MargenEje - DesplazarEtiquetas;
                if (x < area.Left + 4) x = area.Left + 4;
                // ARRIBA DE LA RAYA, O ABAJO SI ARRIBA TAPA OTRA. Otra raya es
                // cualquier otro nodo o cualquier nivel de gamma que Gamma
                // Vivo publica por AppDomain. Pedido del operador el
                // 2026-09-06: la etiqueta nunca sobre una raya, ni confundible
                // con la vecina.
                // ... y NUNCA sobre otra etiqueta: con dos nodos a un punto (7.721,25
                // y 7.720,25 en el MES de 5 min) las dos etiquetas caian en la misma
                // fila y se leian como una sola garabateada (visto el 2026-09-07
                // 04:00). Se prueba arriba, abajo, y despues alejandose, contra las
                // rayas y contra las etiquetas ya puestas. Si cae sobre la caja o
                // sobre el panel de cuenta, se corre a la derecha de ellos.
                int alto = m.Height + 2, anchoEt = m.Width + 8;
                int yy = int.MinValue, xx = x;
                for (int k = 0; k < 5 && yy == int.MinValue; k++)
                {
                    int ar = y - m.Height - 3 - k * (alto + 3);
                    int ab = y + 4 + k * (alto + 3);
                    xx = x;
                    if (EtiquetaLibre(ar, alto, anchoEt, y, nodos, ysGamma, cont, area, usados, ref xx)) { yy = ar; break; }
                    xx = x;
                    if (EtiquetaLibre(ab, alto, anchoEt, y, nodos, ysGamma, cont, area, usados, ref xx)) { yy = ab; break; }
                }
                if (yy == int.MinValue) { yy = y - m.Height - 3; xx = x; }
                var re = new Rectangle(xx - 4, yy, anchoEt, alto);
                usados.Add(re);
                sbLog.Append(string.Format(CultureInfo.InvariantCulture, " et={0},{1}", xx, yy));
                g.FillRectangle(Color.FromArgb(170, 12, 14, 18), re);
                g.DrawString(txt, f, Color.FromArgb(235, col), xx, yy + 1);
            }
            if (sbLog.Length > 0) sbLog.Append("]");
            if ((DateTime.UtcNow - _ultimoLogNodos).TotalSeconds >= 60)
            {
                _ultimoLogNodos = DateTime.UtcNow;
                RegistrarNodos(string.Format(CultureInfo.InvariantCulture,
                    "nodos={0} fuera={1} rayasGamma={2} area={3}..{4} caja={5}{6}",
                    nodos.Count, fuera, ysGamma.Count, area.Top, area.Bottom,
                    _cajaRect.IsEmpty ? "-" : _cajaRect.Bottom.ToString(CultureInfo.InvariantCulture),
                    sbLog.ToString()));
            }

            // los que quedaron afuera, avisados en el borde que corresponde
            if (fuera > 0)
            {
                if (precioArriba > 0)
                    Aviso(g, f, col, area, true,
                          string.Format(cultura, "▲ nodo {0:N2}", precioArriba));
                if (precioAbajo > 0)
                    Aviso(g, f, col, area, false,
                          string.Format(cultura, "▼ nodo {0:N2}", precioAbajo));
            }
        }

        /// <summary>true si la etiqueta cabe en [yy, yy+alto] sin salirse del area,
        /// sin tapar otra raya y sin pisar otra etiqueta. Si cae sobre la caja o
        /// sobre el panel de cuenta de ATAS, corre xx a la derecha de ellos y
        /// prueba ahi (la muesca sigue a la izquierda, a la misma altura).</summary>
        private bool EtiquetaLibre(int yy, int alto, int ancho, int yPropio, List<Nodo> nodos,
                                   List<int> ysGamma, IChartContainer cont, Rectangle area,
                                   List<Rectangle> usados, ref int xx)
        {
            if (yy < area.Top + 2 || yy + alto > area.Bottom - MargenInferior) return false;
            if (TapaOtraRaya(yy, alto, yPropio, nodos, ysGamma, cont)) return false;
            var r = new Rectangle(xx - 4, yy, ancho, alto);
            var zonas = new List<Rectangle>();
            if (VerCaja && !_cajaRect.IsEmpty) zonas.Add(_cajaRect);
            if (AltoPanelCuenta > 0 && AnchoPanelCuenta > 0)
                zonas.Add(new Rectangle(area.Left, area.Bottom - AltoPanelCuenta, AnchoPanelCuenta, AltoPanelCuenta));
            foreach (var z in zonas)
                if (z.IntersectsWith(r)) { xx = z.Right + 10; r = new Rectangle(xx - 4, yy, ancho, alto); }
            foreach (var u in usados) if (u.IntersectsWith(Rectangle.Inflate(r, 2, 2))) return false;
            return true;
        }

        private void RegistrarNodos(string linea)
        {
            try
            {
                var p = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ATAS", "pythiaflow-nodos.log");
                string inst = "";
                try { inst = InstrumentInfo != null ? InstrumentInfo.Instrument : ""; } catch { }
                string marco = "";
                try { marco = ChartInfo != null ? ChartInfo.ChartType + "-" + ChartInfo.TimeFrame : ""; } catch { }
                System.IO.File.AppendAllText(p, string.Format(CultureInfo.InvariantCulture,
                    "{0:s} {1} {2} {3}\n", DateTime.Now, inst, marco, linea));
            }
            catch { }
        }

        /// <summary>true si una etiqueta que ocupa [yy, yy+alto] cubre la raya de
        /// otro nodo o de un nivel de gamma (no la propia, en yPropio). Con dos
        /// pixeles de aire, porque a un pixel la raya vecina tocaba el borde.</summary>
        private static bool TapaOtraRaya(int yy, int alto, int yPropio, List<Nodo> nodos,
                                         List<int> ysGamma, IChartContainer cont)
        {
            int top = yy - 2, bot = yy + alto + 2;
            foreach (var yg in ysGamma) if (yg >= top && yg <= bot) return true;
            foreach (var n in nodos)
            {
                int yn;
                try { yn = cont.GetYByPrice(n.Precio, false); } catch { continue; }
                if (yn == yPropio) continue;
                if (yn >= top && yn <= bot) return true;
            }
            return false;
        }

        private void Aviso(RenderContext g, RenderFont f, Color col,
                           Rectangle area, bool arriba, string txt)
        {
            var m = g.MeasureString(txt, f);
            int x = area.Right - m.Width - MargenEje;
            if (x < area.Left + 4) x = area.Left + 4;
            // 46 y no 4: en la primera fila de la derecha ATAS tiene su boton de
            // reproduccion y en la segunda Gamma Vivo ancla su chip de muro
            // fuera de pantalla. Abajo, el margen: lo pegado al fondo cae
            // detras del eje de tiempo.
            int y = arriba ? area.Top + 46 : area.Bottom - m.Height - 6 - MargenInferior;
            g.FillRectangle(Color.FromArgb(170, 12, 14, 18),
                            new Rectangle(x - 4, y, m.Width + 8, m.Height + 2));
            g.DrawString(txt, f, Color.FromArgb(210, col), x, y + 1);
        }

        private void PintarCaja(RenderContext g)
        {
            List<Nodo> nodos;
            int barras, cand, ventana;
            decimal med, umb;
            lock (_llave)
            {
                nodos = new List<Nodo>(_nodos);
                barras = _barrasUsadas;
                med = _medianaNivel;
                umb = _umbralNodo;
                cand = _candidatos;
                ventana = _velasVentana;
            }

            var cultura = CultureInfo.GetCultureInfo("es-AR");
            var filas = new List<string>
            {
                "NODOS DE VOLUMEN",
                VentanaEnHoras
                    ? string.Format(cultura, "{0} velas = {1} h de mercado   {2} pasaron   {3} dibujados",
                                    barras, Horas.ToString("0.#", cultura), cand, nodos.Count)
                      + (barras < ventana ? string.Format(cultura, "  (el grafico solo tiene {0})", barras) : "")
                    : string.Format(cultura, "{0} velas   {1} pasaron   {2} dibujados",
                                    barras, cand, nodos.Count),
                // Estos dos numeros son la auditoria: dicen a cuantos contratos
                // equivale el umbral relativo en ESTE instrumento ahora mismo.
                string.Format(cultura, "precio tipico {0}   umbral {1}",
                              Corto(med), Corto(umb)),
                // LA REGLA DE ABSORCION, TAL COMO SE APLICA. Antes la caja decia
                // "3 x N por precio" con N el promedio de la ventana, y el codigo
                // usaba OTRO numero (el promedio de la propia vela). La caja y el
                // codigo tienen que decir lo mismo o la caja no audita nada.
                // corta a proposito: la caja no puede llegar hasta donde arrancan
                // las etiquetas de los nodos (medido el 2026-09-07: pisaba "#3")
                string.Format(cultura, "absorcion {0}x · rango >= {1} tk · vela >= mediana",
                              FuerzaAbsorcion.ToString("0.#", cultura), RangoMinimoTicks),
            };

            var f = new RenderFont("Arial", 8.5f);
            int w = 0, h = 0;
            foreach (var s in filas)
            {
                var m = g.MeasureString(s, f);
                if (m.Width > w) w = m.Width;
                h += m.Height + 1;
            }
            var area = ChartArea;
            // ATAS pone su propio cartel ("Last Market Data Update...") pegado
            // arriba a la izquierda y tapaba justo la linea del conteo. Por eso
            // la caja arranca mas abajo, y el margen queda a mano por si en otra
            // pantalla el cartel ocupa distinto.
            int yCaja = EsquinaCajaControl == EsquinaCaja.AbajoIzquierda
                ? area.Bottom - (h + 8) - MargenInferior
                : area.Top + MargenArriba;
            var caja = new Rectangle(area.Left + 8, yCaja, w + 14, h + 8);
            _cajaRect = caja;   // para que las etiquetas de los nodos la esquiven
            g.FillRectangle(Color.FromArgb(180, 12, 14, 18), caja);
            g.DrawRectangle(new RenderPen(Color.FromArgb(90, 120, 130, 145), 1), caja);
            int y2 = caja.Top + 4;
            for (int i = 0; i < filas.Count; i++)
            {
                var m = g.MeasureString(filas[i], f);
                g.DrawString(filas[i], f,
                             i == 0 ? Color.FromArgb(235, 235, 200, 60)
                                    : Color.FromArgb(210, 200, 205, 215),
                             caja.Left + 7, y2);
                y2 += m.Height + 1;
            }
        }

        /// <summary>Normal acumulada (Abramowitz-Stegun 7.1.26), la misma que usa Gamma Vivo.</summary>
        private static double Phi(double x)
        {
            double t = 1.0 / (1.0 + 0.2316419 * Math.Abs(x));
            double d = 0.3989422804014327 * Math.Exp(-x * x / 2.0);
            double p = d * t * (0.319381530 + t * (-0.356563782 + t * (1.781477937 + t * (-1.821255978 + t * 1.330274429))));
            return x >= 0 ? 1.0 - p : p;
        }

        /// <summary>Volumen abreviado: 1,2M / 840K / 512.</summary>
        private static string Corto(decimal v)
        {
            var c = CultureInfo.GetCultureInfo("es-AR");
            decimal a = Math.Abs(v);
            if (a >= 1000000m) return (v / 1000000m).ToString("0.#", c) + "M";
            if (a >= 1000m) return (v / 1000m).ToString("0.#", c) + "K";
            return v.ToString("0", c);
        }
    }
}
