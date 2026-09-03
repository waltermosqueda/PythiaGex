using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Control;
using OFT.Rendering.Tools;

using MColor = System.Windows.Media.Color;
using MColors = System.Windows.Media.Colors;

namespace PythiaVwap
{
    // ======================================================================
    // Opciones
    // ======================================================================

    /// <summary>Desde donde arranca a contar el VWAP.</summary>
    public enum ModoDeAncla
    {
        [Description("Sesion (se reinicia cada dia)")] Sesion,
        [Description("Semana")] Semana,
        [Description("Mes")] Mes,
        [Description("Fecha y hora fijas (o tecla + mouse)")] FechaFija,
        [Description("Maximo mas alto del rango")] MaximoDelRango,
        [Description("Minimo mas bajo del rango")] MinimoDelRango,
        [Description("Vela de mayor volumen del rango")] MayorVolumen,
        [Description("N barras hacia atras")] BarrasAtras,
        [Description("Todo el historico cargado")] Todo,
    }

    /// <summary>Que precio se usa para ponderar cada vela.</summary>
    public enum FuenteDePrecio
    {
        [Description("VWAP real de cada vela (exacto, recomendado)")] VwapDeVela,
        [Description("Volumen por precio / footprint (exacto + sigma exacta)")] Cluster,
        [Description("Precio tipico (H+L+C)/3 - igual que NinjaTrader")] Tipico,
        [Description("Cierre")] Cierre,
        [Description("Ponderado (H+L+C+C)/4")] Ponderado,
    }

    public enum TipoDeVolumen
    {
        [Description("Total")] Total,
        [Description("Solo bid (vendedor agresivo)")] Bid,
        [Description("Solo ask (comprador agresivo)")] Ask,
    }

    /// <summary>Como se separan las bandas de la linea central.</summary>
    public enum TipoDeBanda
    {
        [Description("Desviacion estandar (sigma)")] Sigma,
        [Description("Porcentaje del VWAP")] Porcentaje,
        [Description("Puntos fijos")] Puntos,
    }

    public enum EsquinaDelControl
    {
        [Description("Arriba a la izquierda")] ArribaIzquierda,
        [Description("Arriba a la derecha")] ArribaDerecha,
        [Description("Abajo a la izquierda")] AbajoIzquierda,
        [Description("Abajo a la derecha")] AbajoDerecha,
    }

    public enum LadoDeEtiqueta
    {
        [Description("Derecha")] Derecha,
        [Description("Izquierda")] Izquierda,
        [Description("Sin etiquetas")] Ninguna,
    }

    // ======================================================================
    // El indicador
    // ======================================================================

    /// <summary>
    /// PythiaVWAP - VWAP Anclado.
    ///
    /// El VWAP nativo de ATAS ancla por sesion, semana, mes o por una tecla,
    /// y trae tres desviaciones. Lo que no hace, y es lo que se agrega aca:
    ///
    ///   - anclar solo al maximo, al minimo o a la vela de mayor volumen de
    ///     un rango, que es como se usa el VWAP anclado en la practica;
    ///   - pintar el canal entre bandas;
    ///   - rotular cada linea con su nombre y su precio sobre el grafico;
    ///   - arrastrar los VWAP de las sesiones anteriores como niveles;
    ///   - calcular la sigma sobre el volumen por precio real de cada vela
    ///     en lugar de sobre un unico precio representativo.
    ///
    /// Sobre la precision: casi todas las plataformas ponderan cada vela por
    /// un solo precio, el tipico (H+L+C)/3. Eso es una aproximacion. ATAS
    /// guarda el volumen negociado en cada precio adentro de cada vela, asi
    /// que aca el VWAP puede salir exacto, identico al que daria contar tick
    /// por tick. Se deja igual el modo "Tipico" para poder comparar contra
    /// NinjaTrader o TradingView y ver cuanto se separan.
    /// </summary>
    [DisplayName("PythiaVWAP - VWAP Anclado")]
    [Category("PythiaGex")]
    public class VwapAnclado : Indicator
    {
        private const int MaxBandas = 4;
        private const int MaxPrevias = 5;

        // ==================================================================
        // Series
        // ==================================================================
        private readonly RangeDataSeries[] _relleno = new RangeDataSeries[MaxBandas];
        private readonly ValueDataSeries[] _arriba = new ValueDataSeries[MaxBandas];
        private readonly ValueDataSeries[] _abajo = new ValueDataSeries[MaxBandas];
        private readonly ValueDataSeries[] _previa = new ValueDataSeries[MaxPrevias];
        private readonly ValueDataSeries[] _previaSup = new ValueDataSeries[MaxBandas];
        private readonly ValueDataSeries[] _previaInf = new ValueDataSeries[MaxBandas];
        private readonly ValueDataSeries _linea;

        // ==================================================================
        // Estado del calculo
        // ==================================================================
        // Sumas acumuladas desde la barra 0. Restando dos posiciones sale
        // cualquier tramo en una sola operacion, sin importar donde este el
        // ancla, y por eso mover el ancla no cuesta nada.
        private decimal[] _sPv = new decimal[0];
        private decimal[] _sV = new decimal[0];
        private decimal[] _sP2v = new decimal[0];
        private int[] _anclaExtremo = new int[0];

        private readonly List<int> _sesiones = new List<int>();
        private readonly List<int> _semanas = new List<int>();
        private readonly List<int> _meses = new List<int>();

        private int _extIdx = -1;
        private decimal _extVal;
        private int _extDesde = -1;

        // Lo que quedo calculado en la ultima barra, para el rotulo.
        private int _anclaVigente = -1;
        private decimal _vwapVigente, _sigmaVigente;
        private DateTime _horaAncla;

        // Control de exactitud: el mismo tramo medido por los tres caminos.
        private int _ctrlBarra = -1;
        private decimal _ctrlCluster, _ctrlVela, _ctrlTipico;

        public VwapAnclado() : base(true)
        {
            Panel = IndicatorDataProvider.CandlesPanel;
            DenyToChangePanel = true;

            // La serie que ATAS crea sola no se usa: se oculta para poder
            // controlar el orden de dibujo (rellenos atras, lineas adelante).
            var cero = (ValueDataSeries)DataSeries[0];
            cero.VisualType = VisualMode.Hide;
            cero.IsHidden = true;
            cero.IgnoredByAlerts = true;

            for (var i = 0; i < MaxBandas; i++)
            {
                _relleno[i] = new RangeDataSeries("r2" + i, "Relleno banda " + (i + 1))
                {
                    RangeColor = MColor.FromArgb((byte)(30 - i * 5), 70, 130, 200),
                    DrawAbovePrice = false,
                    IgnoredByAlerts = true,
                };
            }
            for (var i = MaxBandas - 1; i >= 0; i--)
                DataSeries.Add(_relleno[i]);

            for (var i = 0; i < MaxBandas; i++)
            {
                var n = i + 1;
                _arriba[i] = new ValueDataSeries("s2" + i, "Banda +" + n)
                {
                    Color = MColor.FromArgb(255, 90, 150, 220),
                    Width = 1,
                    VisualType = VisualMode.Line,
                    ShowZeroValue = false,
                    ShowCurrentValue = false,
                };
                _abajo[i] = new ValueDataSeries("i2" + i, "Banda -" + n)
                {
                    Color = MColor.FromArgb(255, 90, 150, 220),
                    Width = 1,
                    VisualType = VisualMode.Line,
                    ShowZeroValue = false,
                    ShowCurrentValue = false,
                };
                DataSeries.Add(_arriba[i]);
                DataSeries.Add(_abajo[i]);
            }

            _linea = new ValueDataSeries("v2", "VWAP")
            {
                Color = MColors.Orange,
                Width = 2,
                VisualType = VisualMode.Line,
                ShowZeroValue = false,
                ShowCurrentValue = false,
            };
            DataSeries.Add(_linea);

            for (var i = 0; i < MaxPrevias; i++)
            {
                _previa[i] = new ValueDataSeries("p2" + i,
                    i == 0 ? "VWAP sesion anterior" : "VWAP " + (i + 1) + " sesiones atras")
                {
                    Color = MColor.FromArgb(255, 150, 150, 160),
                    Width = 1,
                    LineDashStyle = OFT.Rendering.Settings.LineDashStyle.Dash,
                    VisualType = VisualMode.Line,
                    ShowZeroValue = false,
                    ShowCurrentValue = false,
                    IgnoredByAlerts = true,
                };
                DataSeries.Add(_previa[i]);
            }

            // Las bandas del VWAP de ayer, como niveles horizontales. Es lo que
            // el VWAPBandsPro2 de NinjaTrader rotula "Prev VWAP +1 Day".
            for (var i = 0; i < MaxBandas; i++)
            {
                var n = i + 1;
                _previaSup[i] = new ValueDataSeries("ps2" + i, "Ayer banda +" + n)
                {
                    Color = MColor.FromArgb(255, 120, 125, 140),
                    Width = 1,
                    LineDashStyle = OFT.Rendering.Settings.LineDashStyle.Dot,
                    VisualType = VisualMode.Line,
                    ShowZeroValue = false,
                    ShowCurrentValue = false,
                    IgnoredByAlerts = true,
                };
                _previaInf[i] = new ValueDataSeries("pi2" + i, "Ayer banda -" + n)
                {
                    Color = MColor.FromArgb(255, 120, 125, 140),
                    Width = 1,
                    LineDashStyle = OFT.Rendering.Settings.LineDashStyle.Dot,
                    VisualType = VisualMode.Line,
                    ShowZeroValue = false,
                    ShowCurrentValue = false,
                    IgnoredByAlerts = true,
                };
                DataSeries.Add(_previaSup[i]);
                DataSeries.Add(_previaInf[i]);
            }

            AplicarVisibilidad();

            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
        }

        // ==================================================================
        // Ajustes: ancla
        // ==================================================================
        private ModoDeAncla _modo = ModoDeAncla.Sesion;
        [Display(Name = "Anclar en", GroupName = "1. Ancla", Order = 10,
            Description = "Desde donde empieza a contar el VWAP.")]
        public ModoDeAncla Modo
        {
            get => _modo;
            set { _modo = value; RecalculateValues(); }
        }

        private DateTime _fechaAncla = DateTime.Today;
        [Display(Name = "Fecha y hora del ancla", GroupName = "1. Ancla", Order = 20,
            Description = "Solo para el modo de fecha fija. Se llena sola al apretar la tecla de ancla con el mouse sobre una vela.")]
        public DateTime FechaAncla
        {
            get => _fechaAncla;
            set { _fechaAncla = value; RecalculateValues(); }
        }

        private Key _tecla = Key.A;
        [Display(Name = "Tecla para anclar con el mouse", GroupName = "1. Ancla", Order = 30,
            Description = "Poner el mouse sobre la vela y apretar esta tecla: el ancla salta ahi.")]
        public Key TeclaDeAncla
        {
            get => _tecla;
            set => _tecla = value;
        }

        private int _ventana = 0;
        [Display(Name = "Rango de busqueda (sesiones)", GroupName = "1. Ancla", Order = 40,
            Description = "Para los modos de maximo, minimo y mayor volumen. 0 = todo el historico cargado.")]
        [Range(0, 500)]
        public int VentanaSesiones
        {
            get => _ventana;
            set { _ventana = Math.Max(0, value); RecalculateValues(); }
        }

        private int _barrasAtras = 100;
        [Display(Name = "Barras hacia atras", GroupName = "1. Ancla", Order = 50,
            Description = "Solo para el modo de N barras.")]
        [Range(1, 100000)]
        public int BarrasAtras
        {
            get => _barrasAtras;
            set { _barrasAtras = Math.Max(1, value); RecalculateValues(); }
        }

        private bool _marcaAncla = true;
        [Display(Name = "Marcar la vela del ancla", GroupName = "1. Ancla", Order = 60)]
        public bool MarcarAncla
        {
            get => _marcaAncla;
            set { _marcaAncla = value; }
        }

        // ==================================================================
        // Ajustes: calculo
        // ==================================================================
        private FuenteDePrecio _fuente = FuenteDePrecio.VwapDeVela;
        [Display(Name = "Precio de cada vela", GroupName = "2. Calculo", Order = 110,
            Description = "Como se pondera cada vela. El modo footprint es el unico que da tambien la sigma exacta, pero es el mas pesado.")]
        public FuenteDePrecio Fuente
        {
            get => _fuente;
            set { _fuente = value; RecalculateValues(); }
        }

        private TipoDeVolumen _volumen = TipoDeVolumen.Total;
        [Display(Name = "Volumen", GroupName = "2. Calculo", Order = 120,
            Description = "Total, o solo el que se ejecuto contra el bid o contra el ask.")]
        public TipoDeVolumen Volumen
        {
            get => _volumen;
            set { _volumen = value; RecalculateValues(); }
        }

        private bool _verControl = false;
        [Display(Name = "Control de exactitud", GroupName = "2. Calculo", Order = 130,
            Description = "Dibuja una caja con el VWAP calculado por los tres metodos al mismo tiempo y la diferencia en ticks. El del footprint es el exacto; el tipico es el que usan NinjaTrader y TradingView. Sirve para saber cuanto se pierde por la aproximacion.")]
        public bool VerControl
        {
            get => _verControl;
            set { _verControl = value; _ctrlBarra = -1; }
        }

        private EsquinaDelControl _esquina = EsquinaDelControl.ArribaIzquierda;
        [Display(Name = "Esquina del control", GroupName = "2. Calculo", Order = 140,
            Description = "Donde va la caja. Ninguna esquina esta siempre libre: ATAS dibuja su encabezado arriba a la izquierda y el reloj de la vela abajo a la derecha.")]
        public EsquinaDelControl Esquina
        {
            get => _esquina;
            set => _esquina = value;
        }

        private int _bajarControl = 36;
        [Display(Name = "Bajar el control (px)", GroupName = "2. Calculo", Order = 150,
            Description = "ATAS escribe el nombre del instrumento y el OHLC arriba del area del grafico. Este corrimiento evita que la caja quede encima.")]
        [Range(0, 600)]
        public int BajarControl
        {
            get => _bajarControl;
            set => _bajarControl = Math.Max(0, value);
        }

        // ==================================================================
        // Ajustes: bandas
        // ==================================================================
        private readonly decimal[] _mult = { 1m, 2m, 3m, 0m };

        private TipoDeBanda _tipoBanda = TipoDeBanda.Sigma;
        [Display(Name = "Las bandas se miden en", GroupName = "3. Bandas", Order = 2205)]
        public TipoDeBanda TipoBanda
        {
            get => _tipoBanda;
            set { _tipoBanda = value; RecalculateValues(); }
        }

        [Display(Name = "Banda 1", GroupName = "3. Bandas", Order = 210, Description = "0 la apaga.")]
        public decimal Mult1
        {
            get => _mult[0];
            set { _mult[0] = Math.Max(0, value); AplicarVisibilidad(); RecalculateValues(); }
        }

        [Display(Name = "Banda 2", GroupName = "3. Bandas", Order = 20, Description = "0 la apaga.")]
        public decimal Mult2
        {
            get => _mult[1];
            set { _mult[1] = Math.Max(0, value); AplicarVisibilidad(); RecalculateValues(); }
        }

        [Display(Name = "Banda 3", GroupName = "3. Bandas", Order = 230, Description = "0 la apaga.")]
        public decimal Mult3
        {
            get => _mult[2];
            set { _mult[2] = Math.Max(0, value); AplicarVisibilidad(); RecalculateValues(); }
        }

        [Display(Name = "Banda 4", GroupName = "3. Bandas", Order = 240, Description = "0 la apaga.")]
        public decimal Mult4
        {
            get => _mult[3];
            set { _mult[3] = Math.Max(0, value); AplicarVisibilidad(); RecalculateValues(); }
        }

        private bool _rellenar = true;
        [Display(Name = "Pintar el canal entre bandas", GroupName = "3. Bandas", Order = 250)]
        public bool Rellenar
        {
            get => _rellenar;
            set { _rellenar = value; AplicarVisibilidad(); RecalculateValues(); }
        }

        // ==================================================================
        // Ajustes: sesiones previas
        // ==================================================================
        private int _previas = 0;
        [Display(Name = "VWAP de sesiones anteriores", GroupName = "4. Sesiones anteriores", Order = 310,
            Description = "Cuantos VWAP de cierre de dias anteriores arrastrar como nivel. 0 los apaga.")]
        [Range(0, MaxPrevias)]
        public int Previas
        {
            get => _previas;
            set { _previas = Math.Min(MaxPrevias, Math.Max(0, value)); AplicarVisibilidad(); RecalculateValues(); }
        }

        private bool _bandasPrevia = false;
        [Display(Name = "Tambien las bandas de ayer", GroupName = "4. Sesiones anteriores", Order = 320,
            Description = "Dibuja las bandas del VWAP con que cerro la sesion anterior, no solo su linea. Usa los mismos multiplicadores que las bandas de arriba.")]
        public bool BandasDeLaPrevia
        {
            get => _bandasPrevia;
            set { _bandasPrevia = value; AplicarVisibilidad(); RecalculateValues(); }
        }

        // ==================================================================
        // Ajustes: etiquetas
        // ==================================================================
        private LadoDeEtiqueta _lado = LadoDeEtiqueta.Derecha;
        [Display(Name = "Etiquetas", GroupName = "5. Etiquetas", Order = 410)]
        public LadoDeEtiqueta Lado
        {
            get => _lado;
            set => _lado = value;
        }

        private bool _verPrecio = true;
        [Display(Name = "Mostrar el precio en la etiqueta", GroupName = "5. Etiquetas", Order = 420)]
        public bool VerPrecio
        {
            get => _verPrecio;
            set => _verPrecio = value;
        }

        private string _prefijo = "";
        [Display(Name = "Prefijo", GroupName = "5. Etiquetas", Order = 430,
            Description = "Texto delante de cada etiqueta, para distinguir dos instancias del indicador.")]
        public string Prefijo
        {
            get => _prefijo;
            set => _prefijo = value ?? "";
        }

        private string _tipografia = "Consolas";
        [Display(Name = "Tipografia", GroupName = "5. Etiquetas", Order = 440)]
        public string Tipografia
        {
            get => _tipografia;
            set => _tipografia = value;
        }

        private float _tamFuente = 9f;
        [Display(Name = "Tamano de letra", GroupName = "5. Etiquetas", Order = 450)]
        [Range(5, 30)]
        public float TamFuente
        {
            get => _tamFuente;
            set => _tamFuente = Math.Max(5f, value);
        }

        private int _margenEje = 62;
        [Display(Name = "Separacion del eje de precios (px)", GroupName = "5. Etiquetas", Order = 470,
            Description = "El eje de precios se dibuja ENCIMA del area del grafico, asi que el borde derecho del area no es el borde visible. Si la etiqueta queda cortada, subir este numero.")]
        [Range(0, 400)]
        public int MargenEje
        {
            get => _margenEje;
            set => _margenEje = Math.Max(0, value);
        }

        private bool _verSigma = false;
        [Display(Name = "Mostrar el ancho de sigma", GroupName = "5. Etiquetas", Order = 460,
            Description = "Agrega en la etiqueta del VWAP cuantos puntos mide una desviacion. Sirve para ver si el dia esta comprimido o expandido.")]
        public bool VerSigma
        {
            get => _verSigma;
            set => _verSigma = value;
        }

        // ==================================================================
        // Calculo
        // ==================================================================

        protected override void OnRecalculate()
        {
            _sesiones.Clear();
            _semanas.Clear();
            _meses.Clear();
            _extIdx = -1;
            _extDesde = -1;
            _extVal = 0;
            _anclaVigente = -1;
            _vwapVigente = 0;
            _sigmaVigente = 0;
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            try { Calcular(bar); }
            catch (Exception e) { Rezongar("OnCalculate", e); }
        }

        private void Calcular(int bar)
        {
            Redimensionar(bar);
            MarcarPeriodos(bar);
            Acumular(bar);

            var ancla = ResolverAncla(bar);
            if (ancla < 0 || ancla > bar)
                return;

            decimal vwap, sigma;
            if (!Tramo(ancla, bar, out vwap, out sigma))
                return;

            _linea[bar] = vwap;

            // Cortar la linea en la vela anterior al ancla para que no quede
            // una diagonal uniendo el VWAP viejo con el nuevo.
            if (ancla == bar && bar > 0)
            {
                _linea.SetPointOfEndLine(bar - 1);
                for (var i = 0; i < MaxBandas; i++)
                {
                    _arriba[i].SetPointOfEndLine(bar - 1);
                    _abajo[i].SetPointOfEndLine(bar - 1);
                }
            }

            for (var i = 0; i < MaxBandas; i++)
            {
                var m = _mult[i];
                if (m <= 0)
                {
                    _arriba[i][bar] = 0;
                    _abajo[i][bar] = 0;
                    _relleno[i][bar] = new RangeValue { Upper = 0, Lower = 0 };
                    continue;
                }

                var d = Separacion(m, vwap, sigma);
                var sup = vwap + d;
                var inf = vwap - d;
                _arriba[i][bar] = sup;
                _abajo[i][bar] = inf;

                // Cada banda pinta su rango completo con poca opacidad. Al
                // superponerse, el color se suma hacia el centro y queda el
                // degradado: mas denso cerca del VWAP, mas tenue en los bordes.
                _relleno[i][bar] = Rellenar
                    ? new RangeValue { Upper = sup, Lower = inf }
                    : new RangeValue { Upper = 0, Lower = 0 };
            }

            CalcularPrevias(bar);

            if (bar == CurrentBar - 1)
            {
                _anclaVigente = ancla;
                _vwapVigente = vwap;
                _sigmaVigente = sigma;
                _horaAncla = GetCandle(ancla).Time;

                // El control es caro: se rehace al cambiar de vela, no en cada
                // tick. Los tres metodos se miden en el mismo instante, que es
                // la unica forma de que la comparacion signifique algo.
                if (VerControl && bar != _ctrlBarra)
                {
                    CalcularControl(ancla, bar);
                    _ctrlBarra = bar;
                }
            }
        }

        /// <summary>
        /// Mide el mismo tramo por los tres caminos a la vez. El del footprint
        /// suma el volumen negociado en cada precio adentro de cada vela, asi
        /// que es el VWAP exacto; los otros dos representan la vela por un
        /// unico precio y son aproximaciones.
        /// </summary>
        private void CalcularControl(int desde, int hasta)
        {
            decimal pvC = 0, vC = 0, pvV = 0, pvT = 0, vTot = 0;

            for (var b = desde; b <= hasta; b++)
            {
                var c = GetCandle(b);

                foreach (var l in c.GetAllPriceLevels())
                {
                    if (l.Volume <= 0) continue;
                    pvC += l.Price * l.Volume;
                    vC += l.Volume;
                }

                var vol = c.Volume;
                if (vol <= 0) continue;
                var tip = (c.High + c.Low + c.Close) / 3m;
                pvV += (c.VWAP > 0 ? c.VWAP : tip) * vol;
                pvT += tip * vol;
                vTot += vol;
            }

            _ctrlCluster = vC > 0 ? pvC / vC : 0;
            _ctrlVela = vTot > 0 ? pvV / vTot : 0;
            _ctrlTipico = vTot > 0 ? pvT / vTot : 0;
        }

        /// <summary>Una banda en cero no deja ni linea ni relleno colgados.</summary>
        private void AplicarVisibilidad()
        {
            for (var i = 0; i < MaxBandas; i++)
            {
                var viva = _mult[i] > 0;
                _arriba[i].VisualType = viva ? VisualMode.Line : VisualMode.Hide;
                _abajo[i].VisualType = viva ? VisualMode.Line : VisualMode.Hide;
                _relleno[i].Visible = viva && _rellenar;
            }
            for (var k = 0; k < MaxPrevias; k++)
                _previa[k].VisualType = k < _previas ? VisualMode.Line : VisualMode.Hide;

            var conBandas = _bandasPrevia && _previas > 0;
            for (var i = 0; i < MaxBandas; i++)
            {
                var v = conBandas && _mult[i] > 0 ? VisualMode.Line : VisualMode.Hide;
                _previaSup[i].VisualType = v;
                _previaInf[i].VisualType = v;
            }
        }

        /// <summary>Separacion de la banda respecto de la linea central.</summary>
        private decimal Separacion(decimal m, decimal vwap, decimal sigma)
        {
            switch (TipoBanda)
            {
                case TipoDeBanda.Porcentaje: return vwap * m / 100m;
                case TipoDeBanda.Puntos: return m;
                default: return sigma * m;
            }
        }

        private void Redimensionar(int bar)
        {
            if (bar < _sPv.Length) return;
            var n = Math.Max(bar + 1024, (CurrentBar > 0 ? CurrentBar : bar) + 16);
            Array.Resize(ref _sPv, n);
            Array.Resize(ref _sV, n);
            Array.Resize(ref _sP2v, n);
            Array.Resize(ref _anclaExtremo, n);
        }

        /// <summary>Anota donde arranca cada sesion, semana y mes.</summary>
        private void MarcarPeriodos(int bar)
        {
            Anotar(_sesiones, bar, bar == 0 || IsNewSession(bar));
            Anotar(_semanas, bar, bar == 0 || IsNewWeek(bar));
            Anotar(_meses, bar, bar == 0 || IsNewMonth(bar));
        }

        private static void Anotar(List<int> lista, int bar, bool arranca)
        {
            // La barra en curso se recalcula en cada tick: hay que sacar todo
            // lo que quedo anotado desde ella en adelante antes de anotar.
            while (lista.Count > 0 && lista[lista.Count - 1] >= bar)
                lista.RemoveAt(lista.Count - 1);
            if (arranca) lista.Add(bar);
        }

        /// <summary>
        /// Suma el aporte de una vela a los acumulados. Es idempotente: la
        /// barra en curso se puede recalcular todas las veces que haga falta.
        /// </summary>
        private void Acumular(int bar)
        {
            var c = GetCandle(bar);
            decimal pv = 0, vv = 0, p2v = 0;

            if (Fuente == FuenteDePrecio.Cluster)
            {
                foreach (var l in c.GetAllPriceLevels())
                {
                    decimal v;
                    switch (Volumen)
                    {
                        case TipoDeVolumen.Bid: v = l.Bid; break;
                        case TipoDeVolumen.Ask: v = l.Ask; break;
                        default: v = l.Volume; break;
                    }
                    if (v <= 0) continue;
                    pv += l.Price * v;
                    vv += v;
                    p2v += l.Price * l.Price * v;
                }
            }
            else
            {
                decimal p;
                switch (Fuente)
                {
                    case FuenteDePrecio.Tipico: p = (c.High + c.Low + c.Close) / 3m; break;
                    case FuenteDePrecio.Cierre: p = c.Close; break;
                    case FuenteDePrecio.Ponderado: p = (c.High + c.Low + c.Close + c.Close) / 4m; break;
                    default:
                        p = c.VWAP > 0 ? c.VWAP : (c.High + c.Low + c.Close) / 3m;
                        break;
                }

                decimal v;
                switch (Volumen)
                {
                    case TipoDeVolumen.Bid: v = c.Bid; break;
                    case TipoDeVolumen.Ask: v = c.Ask; break;
                    default: v = c.Volume; break;
                }
                if (v <= 0) v = c.Volume;

                pv = p * v;
                vv = v;
                p2v = p * p * v;
            }

            var basePv = bar > 0 ? _sPv[bar - 1] : 0m;
            var baseV = bar > 0 ? _sV[bar - 1] : 0m;
            var baseP2v = bar > 0 ? _sP2v[bar - 1] : 0m;

            _sPv[bar] = basePv + pv;
            _sV[bar] = baseV + vv;
            _sP2v[bar] = baseP2v + p2v;

            if (Modo == ModoDeAncla.MaximoDelRango || Modo == ModoDeAncla.MinimoDelRango
                || Modo == ModoDeAncla.MayorVolumen)
                SeguirExtremo(bar, c);
        }

        /// <summary>
        /// Mantiene la vela mas alta, la mas baja o la de mas volumen dentro
        /// de la ventana. Se rehace entera solo cuando la ventana se corre,
        /// una vez por sesion; el resto del tiempo es una comparacion.
        /// </summary>
        private void SeguirExtremo(int bar, IndicatorCandle c)
        {
            var desde = InicioDeVentana();

            if (desde != _extDesde || _extIdx < 0 || _extIdx < desde || _extIdx > bar)
            {
                _extDesde = desde;
                _extIdx = -1;
                for (var b = desde; b <= bar; b++)
                {
                    var cc = GetCandle(b);
                    var v = ValorExtremo(cc);
                    if (_extIdx < 0 || Mejor(v, _extVal)) { _extVal = v; _extIdx = b; }
                }
            }
            else
            {
                var v = ValorExtremo(c);
                if (Mejor(v, _extVal)) { _extVal = v; _extIdx = bar; }
            }

            _anclaExtremo[bar] = _extIdx;
        }

        private decimal ValorExtremo(IndicatorCandle c)
        {
            switch (Modo)
            {
                case ModoDeAncla.MinimoDelRango: return c.Low;
                case ModoDeAncla.MayorVolumen: return c.Volume;
                default: return c.High;
            }
        }

        private bool Mejor(decimal candidato, decimal actual)
            => Modo == ModoDeAncla.MinimoDelRango ? candidato < actual : candidato > actual;

        private int InicioDeVentana()
        {
            if (VentanaSesiones <= 0 || _sesiones.Count == 0) return 0;
            var i = _sesiones.Count - VentanaSesiones;
            return i <= 0 ? 0 : _sesiones[i];
        }

        private int ResolverAncla(int bar)
        {
            switch (Modo)
            {
                case ModoDeAncla.Sesion: return Ultimo(_sesiones, bar);
                case ModoDeAncla.Semana: return Ultimo(_semanas, bar);
                case ModoDeAncla.Mes: return Ultimo(_meses, bar);
                case ModoDeAncla.Todo: return 0;
                case ModoDeAncla.BarrasAtras: return Math.Max(0, bar - BarrasAtras + 1);
                case ModoDeAncla.FechaFija: return PorFecha(bar);
                case ModoDeAncla.MaximoDelRango:
                case ModoDeAncla.MinimoDelRango:
                case ModoDeAncla.MayorVolumen:
                    return bar < _anclaExtremo.Length ? _anclaExtremo[bar] : 0;
                default: return 0;
            }
        }

        private static int Ultimo(List<int> lista, int bar)
        {
            for (var i = lista.Count - 1; i >= 0; i--)
                if (lista[i] <= bar) return lista[i];
            return 0;
        }

        /// <summary>Primera vela cuya hora ya alcanzo la fecha del ancla.</summary>
        private int PorFecha(int bar)
        {
            if (GetCandle(bar).Time < FechaAncla) return -1;

            // Busqueda binaria: las velas estan ordenadas por hora.
            int lo = 0, hi = bar, r = bar;
            while (lo <= hi)
            {
                var m = (lo + hi) / 2;
                if (GetCandle(m).Time >= FechaAncla) { r = m; hi = m - 1; }
                else lo = m + 1;
            }
            return r;
        }

        /// <summary>VWAP y sigma de un tramo, con dos restas.</summary>
        private bool Tramo(int desde, int hasta, out decimal vwap, out decimal sigma)
        {
            vwap = 0;
            sigma = 0;
            if (desde < 0 || hasta < desde || hasta >= _sV.Length) return false;

            var v = _sV[hasta] - (desde > 0 ? _sV[desde - 1] : 0m);
            if (v <= 0) return false;

            var pv = _sPv[hasta] - (desde > 0 ? _sPv[desde - 1] : 0m);
            var p2v = _sP2v[hasta] - (desde > 0 ? _sP2v[desde - 1] : 0m);

            vwap = pv / v;

            var varianza = p2v / v - vwap * vwap;
            if (varianza > 0)
                sigma = (decimal)Math.Sqrt((double)varianza);

            return true;
        }

        /// <summary>
        /// El VWAP con el que cerro cada una de las sesiones anteriores, que
        /// queda como nivel horizontal sobre la sesion actual.
        /// </summary>
        private void CalcularPrevias(int bar)
        {
            // Al abrir una sesion nueva todos los niveles previos se corren un
            // lugar. Sin cortar la linea, ATAS une el valor viejo con el nuevo
            // con una diagonal que no es ningun nivel.
            var abre = bar > 0 && _sesiones.Count > 0 && _sesiones[_sesiones.Count - 1] == bar;

            for (var k = 0; k < MaxPrevias; k++)
            {
                if (abre) _previa[k].SetPointOfEndLine(bar - 1);
                if (k >= Previas) { _previa[k][bar] = 0; continue; }

                var j = _sesiones.Count - 2 - k;
                if (j < 0) { _previa[k][bar] = 0; continue; }

                var ini = _sesiones[j];
                var fin = _sesiones[j + 1] - 1;
                decimal v, sg;
                var hay = Tramo(ini, fin, out v, out sg);
                _previa[k][bar] = hay ? v : 0;

                // Solo la sesion anterior inmediata lleva bandas: mas atras se
                // vuelve una maraña que tapa el precio.
                if (k != 0) continue;
                for (var i = 0; i < MaxBandas; i++)
                {
                    if (abre)
                    {
                        _previaSup[i].SetPointOfEndLine(bar - 1);
                        _previaInf[i].SetPointOfEndLine(bar - 1);
                    }
                    var m = _mult[i];
                    if (!hay || !BandasDeLaPrevia || m <= 0)
                    {
                        _previaSup[i][bar] = 0;
                        _previaInf[i][bar] = 0;
                        continue;
                    }
                    var d = Separacion(m, v, sg);
                    _previaSup[i][bar] = v + d;
                    _previaInf[i][bar] = v - d;
                }
            }
        }

        // ==================================================================
        // Anclar con el mouse
        // ==================================================================
        public override bool ProcessKeyDown(KeyEventArgs e)
        {
            if (e == null || e.Key != TeclaDeAncla) return false;

            var b = MouseLocationInfo?.BarBelowMouse ?? -1;
            if (b < 0 || b >= CurrentBar) return false;

            _fechaAncla = GetCandle(b).Time;
            _modo = ModoDeAncla.FechaFija;
            RaisePropertyChanged(nameof(FechaAncla));
            RaisePropertyChanged(nameof(Modo));
            RecalculateValues();
            return true;
        }

        // ==================================================================
        // Etiquetas
        // ==================================================================
        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            try { Rotular(g); }
            catch (Exception e) { Rezongar("OnRender", e); }
        }

        private void Rotular(RenderContext g)
        {
            if (ChartInfo == null || Lado == LadoDeEtiqueta.Ninguna) return;
            if (_anclaVigente < 0 || _vwapVigente <= 0) return;

            var cont = ChartInfo.PriceChartContainer;
            if (cont == null) return;

            var f = new RenderFont(string.IsNullOrWhiteSpace(Tipografia) ? "Consolas" : Tipografia,
                                   TamFuente);
            var area = ChartArea;

            var filas = new List<Tuple<decimal, string, Color>>();

            var texto = Prefijo.Length > 0 ? Prefijo + " VWAP" : "VWAP";
            if (VerSigma && _sigmaVigente > 0)
                texto += "  s=" + Redondear(_sigmaVigente).ToString("0.##", CultureInfo.InvariantCulture);
            filas.Add(Tuple.Create(_vwapVigente, texto, ADrawing(_linea.Color)));

            for (var i = 0; i < MaxBandas; i++)
            {
                var m = _mult[i];
                if (m <= 0) continue;
                var d = Separacion(m, _vwapVigente, _sigmaVigente);
                if (d <= 0) continue;

                var etq = Etiqueta(m);
                var pre = (Prefijo.Length > 0 ? Prefijo + " " : "") + "VWAP ";
                filas.Add(Tuple.Create(_vwapVigente + d, pre + "+" + etq, ADrawing(_arriba[i].Color)));
                filas.Add(Tuple.Create(_vwapVigente - d, pre + "-" + etq, ADrawing(_abajo[i].Color)));
            }

            var ult = CurrentBar - 1;
            if (ult >= 0)
            {
                for (var k = 0; k < Previas && k < MaxPrevias; k++)
                {
                    var v = _previa[k][ult];
                    if (v <= 0) continue;
                    filas.Add(Tuple.Create(v,
                        k == 0 ? "VWAP ayer" : "VWAP -" + (k + 1) + "d",
                        ADrawing(_previa[k].Color)));
                }

                if (BandasDeLaPrevia && Previas > 0)
                {
                    for (var i = 0; i < MaxBandas; i++)
                    {
                        if (_mult[i] <= 0) continue;
                        var etq = Etiqueta(_mult[i]);
                        var sup = _previaSup[i][ult];
                        var inf = _previaInf[i][ult];
                        if (sup > 0)
                            filas.Add(Tuple.Create(sup, "Ayer +" + etq, ADrawing(_previaSup[i].Color)));
                        if (inf > 0)
                            filas.Add(Tuple.Create(inf, "Ayer -" + etq, ADrawing(_previaInf[i].Color)));
                    }
                }
            }

            // Las etiquetas van en una columna propia al costado, todas
            // arrancando en la misma x y con el precio alineado a la derecha.
            // Amontonadas contra el eje, cada una con su ancho, no se pueden
            // leer de un vistazo, que es justo para lo que sirven.
            var alto = (int)g.MeasureString("Xy", f).Height + 3;

            var vivas = new List<Etq>();
            foreach (var fila in filas)
            {
                int y;
                try { y = cont.GetYByPrice(fila.Item1, false); }
                catch { continue; }
                if (y < area.Top - alto * 2 || y > area.Bottom + alto * 2) continue;

                vivas.Add(new Etq
                {
                    Y0 = y,
                    Y = y,
                    Nombre = fila.Item2,
                    Col = fila.Item3,
                    Precio = VerPrecio
                        ? Redondear(fila.Item1).ToString("0.##", CultureInfo.InvariantCulture)
                        : "",
                });
            }

            if (vivas.Count > 0)
            {
                vivas.Sort((a, b) => a.Y0.CompareTo(b.Y0));
                Desplegar(vivas, alto, area);

                var wCol = 0;
                foreach (var e in vivas)
                {
                    e.Texto = e.Precio.Length > 0 ? e.Nombre + "  " + e.Precio : e.Nombre;
                    wCol = Math.Max(wCol, (int)g.MeasureString(e.Texto, f).Width);
                }

                // La etiqueta va donde TERMINA la linea, no contra el eje. Si
                // se pega al eje queda un tramo largo de linea sola en el
                // medio, que es ruido: no marca ningun nivel nuevo.
                var tope = area.Right - MargenEje - wCol - 6;
                var xCol = tope;

                if (Lado == LadoDeEtiqueta.Derecha)
                {
                    var xUlt = int.MinValue;
                    if (CurrentBar > 0)
                    {
                        try { xUlt = cont.GetXByBar(CurrentBar - 1, false); }
                        catch { xUlt = int.MinValue; }
                    }
                    if (xUlt > area.Left) xCol = Math.Min(tope, xUlt + 12);
                }
                else
                {
                    xCol = area.Left + 14;
                }

                foreach (var e in vivas)
                {
                    var cy = e.Y - alto / 2;

                    g.FillRectangle(Color.FromArgb(185, 12, 15, 20),
                        new Rectangle(xCol - 5, cy, wCol + 10, alto));

                    // Solo cuando la etiqueta se tuvo que correr para no pisar
                    // a otra: sin este tironcito, una etiqueta desplazada
                    // miente sobre a que altura esta su nivel.
                    if (Math.Abs(e.Y - e.Y0) > 2)
                        g.DrawLine(new RenderPen(Color.FromArgb(140, e.Col), 1, DashStyle.Dot),
                                   xCol - 10, e.Y0, xCol - 5, e.Y);

                    g.DrawString(e.Texto, f, e.Col, xCol, cy + 1);
                }
            }

            if (MarcarAncla) DibujarAncla(g, cont, area, f);
            if (VerControl) DibujarControl(g, area, f);
        }

        /// <summary>
        /// La caja que dice cuanto se separan los tres metodos. Si el footprint
        /// y el vwap de vela no coinciden, algo esta mal en el calculo. Si el
        /// tipico se separa mucho, es la medida de lo que pierde cualquier
        /// plataforma que pondere la vela por un solo precio.
        /// </summary>
        private void DibujarControl(RenderContext g, Rectangle area, RenderFont f)
        {
            if (_ctrlCluster <= 0) return;

            var ts = InstrumentInfo?.TickSize ?? 0m;
            var filas = new List<string>
            {
                "CONTROL DE EXACTITUD",
                "footprint (exacto)   " + Fmt(_ctrlCluster),
                "vwap de vela         " + Fmt(_ctrlVela) + Dif(_ctrlVela, ts),
                "precio tipico        " + Fmt(_ctrlTipico) + Dif(_ctrlTipico, ts),
            };

            var alto = (int)g.MeasureString("Xy", f).Height + 2;
            var ancho = 0;
            foreach (var t in filas)
                ancho = Math.Max(ancho, (int)g.MeasureString(t, f).Width);

            var w = ancho + 14;
            var h = alto * filas.Count + 10;
            var arriba = Esquina == EsquinaDelControl.ArribaIzquierda
                      || Esquina == EsquinaDelControl.ArribaDerecha;
            var izq = Esquina == EsquinaDelControl.ArribaIzquierda
                   || Esquina == EsquinaDelControl.AbajoIzquierda;

            var cx = izq ? area.Left + 8 : area.Right - w - MargenEje - 6;
            var cy = arriba ? area.Top + 8 + BajarControl : area.Bottom - h - 8;
            var caja = new Rectangle(cx, cy, w, h);
            g.FillRectangle(Color.FromArgb(215, 12, 15, 20), caja);
            g.DrawRectangle(new RenderPen(Color.FromArgb(90, 120, 130, 145), 1), caja);

            var y = caja.Top + 5;
            for (var i = 0; i < filas.Count; i++)
            {
                var col = i == 0 ? Color.FromArgb(150, 160, 175)
                        : i == 1 ? Color.FromArgb(120, 220, 150)
                        : Color.FromArgb(210, 215, 225);
                g.DrawString(filas[i], f, col, caja.Left + 7, y);
                y += alto;
            }
        }

        private string Fmt(decimal p)
            => Redondear(p).ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>Cuanto se aparta del exacto, en ticks.</summary>
        private string Dif(decimal v, decimal ts)
        {
            if (ts <= 0) return "";
            var tk = (v - _ctrlCluster) / ts;
            return "   " + (tk >= 0 ? "+" : "") + tk.ToString("0.0", CultureInfo.InvariantCulture) + " tk";
        }

        private string Etiqueta(decimal m)
        {
            var n = m.ToString("0.##", CultureInfo.InvariantCulture);
            switch (TipoBanda)
            {
                case TipoDeBanda.Porcentaje: return n + "%";
                case TipoDeBanda.Puntos: return n + "pt";
                default: return n + "s";
            }
        }

        private sealed class Etq
        {
            public int Y0, Y;
            public string Nombre, Precio, Texto;
            public Color Col;
        }

        /// <summary>
        /// Separa las etiquetas que se pisan, manteniendo siempre el orden de
        /// precio. Empuja hacia abajo y despues corrige si el bloque se paso
        /// de algun borde. El orden no se toca nunca: si una etiqueta quedara
        /// arriba de otra que vale mas, la columna mentiria sobre cual nivel
        /// esta primero, que es peor que una superposicion.
        /// </summary>
        private static void Desplegar(List<Etq> es, int alto, Rectangle area)
        {
            for (var i = 1; i < es.Count; i++)
                if (es[i].Y - es[i - 1].Y < alto)
                    es[i].Y = es[i - 1].Y + alto;

            var sobra = es[es.Count - 1].Y + alto / 2 - area.Bottom;
            if (sobra > 0)
                for (var i = es.Count - 1; i >= 0; i--)
                {
                    es[i].Y -= sobra;
                    if (i > 0 && es[i].Y - es[i - 1].Y >= alto) break;
                }

            var falta = area.Top - (es[0].Y - alto / 2);
            if (falta > 0)
                for (var i = 0; i < es.Count; i++)
                {
                    es[i].Y += falta;
                    if (i + 1 < es.Count && es[i + 1].Y - es[i].Y >= alto) break;
                }
        }

        private void DibujarAncla(RenderContext g, IChartContainer cont, Rectangle area, RenderFont f)
        {
            if (_anclaVigente < 0) return;
            int x;
            try { x = cont.GetXByBar(_anclaVigente, false); }
            catch { return; }
            if (x < area.Left || x > area.Right) return;

            var col = ADrawing(_linea.Color);
            g.DrawLine(new RenderPen(Color.FromArgb(120, col), 1, DashStyle.Dot),
                       x, area.Top, x, area.Bottom);

            var t = _horaAncla.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
            var w = (int)g.MeasureString(t, f).Width + 6;
            var h = (int)g.MeasureString(t, f).Height + 2;
            var caja = new Rectangle(x + 2, area.Top + 2, w, h);
            g.FillRectangle(Color.FromArgb(190, 12, 15, 20), caja);
            g.DrawString(t, f, col, caja.Left + 3, caja.Top + 1);
        }

        private decimal Redondear(decimal p)
        {
            var ts = InstrumentInfo?.TickSize ?? 0m;
            if (ts <= 0) return Math.Round(p, 2);
            return Math.Round(p / ts, MidpointRounding.AwayFromZero) * ts;
        }

        private static Color ADrawing(MColor c) => Color.FromArgb(c.A, c.R, c.G, c.B);

        // ==================================================================
        // Diagnostico
        // ==================================================================
        // ATAS se traga las excepciones de los indicadores sin dejar rastro en
        // su log. Sin esto, un fallo se ve solo como un indicador que no
        // dibuja, y no hay con que averiguar por que.
        private static readonly HashSet<string> _yaAvisado = new HashSet<string>();

        private static void Rezongar(string donde, Exception e)
        {
            try
            {
                var clave = donde + "|" + e.GetType().Name + "|" + e.Message;
                lock (_yaAvisado)
                {
                    if (!_yaAvisado.Add(clave)) return;
                }
                var ruta = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ATAS", "pythiavwap-errores.txt");
                System.IO.File.AppendAllText(ruta,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [" + donde + "]  "
                    + e.GetType().Name + ": " + e.Message + Environment.NewLine
                    + e.StackTrace + Environment.NewLine + Environment.NewLine);
            }
            catch { }
        }
    }
}
