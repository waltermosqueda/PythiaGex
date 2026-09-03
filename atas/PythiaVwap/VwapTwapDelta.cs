using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Windows.Input;

using ATAS.Indicators;

using MColor = System.Windows.Media.Color;

namespace PythiaVwap
{
    /// <summary>En que unidad se entrega la diferencia.</summary>
    public enum UnidadDelta
    {
        [Description("Puntos")] Puntos,
        [Description("Ticks")] Ticks,
        [Description("Porcentaje del TWAP")] Porcentaje,
    }

    /// <summary>
    /// PythiaVWAP - Delta VWAP/TWAP.
    ///
    /// Resta dos promedios del mismo tramo que solo se diferencian en como
    /// pesan cada vela:
    ///
    ///   VWAP  pesa cada precio por el VOLUMEN que se opero ahi.
    ///   TWAP  pesa cada vela por TIEMPO: todas valen lo mismo.
    ///
    /// La resta VWAP - TWAP dice entonces UNA sola cosa, pero que no se ve en
    /// ningun indicador de precio: **donde estuvo el volumen respecto del
    /// recorrido**.
    ///
    ///   Positivo: el volumen se hizo en la parte ALTA del recorrido. El
    ///             dinero entro arriba.
    ///   Negativo: el volumen se hizo ABAJO.
    ///
    /// Un tramo puede subir y tener la resta negativa: el precio sube pero el
    /// volumen quedo atras, abajo. Eso es una divergencia entre volumen y
    /// tiempo, y es informacion que el VWAP solo no da.
    ///
    /// IMPORTANTE: los dos promedios usan **el mismo precio** de cada vela. Si
    /// se usaran precios distintos la resta mezclaria dos efectos y no se
    /// sabria cual es cual. Lo unico que cambia entre ellos es el peso.
    ///
    /// Lo que NO dice: direccion. Igual que el resto del proyecto, describe
    /// como se comporto el flujo, no para donde va el precio.
    /// </summary>
    [DisplayName("PythiaVWAP - Delta VWAP/TWAP")]
    [Category("PythiaGex")]
    public class VwapTwapDelta : Indicator
    {
        private readonly ValueDataSeries _area;
        private readonly ValueDataSeries _borde;
        private readonly LineSeries _cero;

        // Acumulados desde la barra 0. Con dos restas sale cualquier tramo,
        // asi que mover el ancla no cuesta nada.
        private decimal[] _sPv = new decimal[0];   // precio x volumen
        private decimal[] _sV = new decimal[0];    // volumen
        private decimal[] _sP = new decimal[0];    // precio (peso 1: el TWAP)
        private decimal[] _crudo = new decimal[0]; // la resta sin suavizar
        private int[] _anclaExtremo = new int[0];

        private readonly List<int> _sesiones = new List<int>();
        private readonly List<int> _semanas = new List<int>();
        private readonly List<int> _meses = new List<int>();

        private int _extIdx = -1;
        private decimal _extVal;
        private int _extDesde = -1;

        public VwapTwapDelta() : base(true)
        {
            // Panel propio: la resta se mide en puntos y el precio en decenas
            // de miles. En el mismo panel que las velas, la escala se rompe y
            // no se ve nada.
            Panel = IndicatorDataProvider.NewPanel;
            DenyToChangePanel = false;

            var cero = (ValueDataSeries)DataSeries[0];
            cero.VisualType = VisualMode.Hide;
            cero.IsHidden = true;
            cero.IgnoredByAlerts = true;

            _area = new ValueDataSeries("area", "Delta VWAP-TWAP")
            {
                VisualType = VisualMode.Histogram,
                ShowZeroValue = false,
                ShowCurrentValue = true,
                Width = 1,
            };
            DataSeries.Add(_area);

            _borde = new ValueDataSeries("borde", "Contorno")
            {
                VisualType = VisualMode.Line,
                Color = MColor.FromArgb(255, 235, 200, 60),
                Width = 1,
                ShowZeroValue = false,
                ShowCurrentValue = false,
                IgnoredByAlerts = true,
            };
            DataSeries.Add(_borde);

            _cero = new LineSeries("cero", "Linea de cero")
            {
                Value = 0,
                Color = MColor.FromArgb(140, 150, 155, 165),
                Width = 1,
                LineDashStyle = OFT.Rendering.Settings.LineDashStyle.Dot,
                UseScale = true,
            };
            LineSeries.Add(_cero);
        }

        // ==================================================================
        // Ajustes: ancla
        // ==================================================================
        private ModoDeAncla _modo = ModoDeAncla.Sesion;
        [Display(Name = "Anclar en", GroupName = "1. Ancla", Order = 10,
            Description = "Desde donde se empiezan a acumular los dos promedios.")]
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

        private Key _tecla = Key.D;
        [Display(Name = "Tecla para anclar con el mouse", GroupName = "1. Ancla", Order = 30,
            Description = "Poner el mouse sobre la vela y apretar esta tecla.")]
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
        [Display(Name = "Barras hacia atras", GroupName = "1. Ancla", Order = 50)]
        [Range(1, 100000)]
        public int BarrasAtras
        {
            get => _barrasAtras;
            set { _barrasAtras = Math.Max(1, value); RecalculateValues(); }
        }

        // ==================================================================
        // Ajustes: calculo
        // ==================================================================
        private FuenteDePrecio _fuente = FuenteDePrecio.VwapDeVela;
        [Display(Name = "Precio de cada vela", GroupName = "2. Calculo", Order = 110,
            Description = "El MISMO precio alimenta los dos promedios: lo unico que cambia entre ellos es el peso. Si se usaran precios distintos la resta mezclaria dos efectos.")]
        public FuenteDePrecio Fuente
        {
            get => _fuente;
            set { _fuente = value; RecalculateValues(); }
        }

        private TipoDeVolumen _volumen = TipoDeVolumen.Total;
        [Display(Name = "Volumen (solo pesa al VWAP)", GroupName = "2. Calculo", Order = 120,
            Description = "El TWAP no mira el volumen, asi que esto solo mueve el lado VWAP de la resta.")]
        public TipoDeVolumen Volumen
        {
            get => _volumen;
            set { _volumen = value; RecalculateValues(); }
        }

        private UnidadDelta _unidad = UnidadDelta.Puntos;
        [Display(Name = "Unidad", GroupName = "2. Calculo", Order = 130)]
        public UnidadDelta Unidad
        {
            get => _unidad;
            set { _unidad = value; RecalculateValues(); }
        }

        private int _suavizado = 9;
        [Display(Name = "Suavizado (velas)", GroupName = "2. Calculo", Order = 140,
            Description = "Promedio movil sobre la resta. 1 la deja cruda. Nunca toma velas anteriores al ancla, para no arrastrar el tramo pasado.")]
        [Range(1, 500)]
        public int Suavizado
        {
            get => _suavizado;
            set { _suavizado = Math.Max(1, value); RecalculateValues(); }
        }

        // ==================================================================
        // Ajustes: colores
        // ==================================================================
        private MColor _colPos = MColor.FromArgb(230, 60, 190, 90);
        [Display(Name = "Volumen arriba (positivo)", GroupName = "3. Colores", Order = 210)]
        public MColor ColorPositivo
        {
            get => _colPos;
            set { _colPos = value; RecalculateValues(); }
        }

        private MColor _colNeg = MColor.FromArgb(230, 225, 45, 75);
        [Display(Name = "Volumen abajo (negativo)", GroupName = "3. Colores", Order = 220)]
        public MColor ColorNegativo
        {
            get => _colNeg;
            set { _colNeg = value; RecalculateValues(); }
        }

        private bool _verArea = true;
        [Display(Name = "Pintar el area", GroupName = "3. Colores", Order = 230)]
        public bool VerArea
        {
            get => _verArea;
            set { _verArea = value; _area.VisualType = value ? VisualMode.Histogram : VisualMode.Hide; }
        }

        private bool _verBorde = true;
        [Display(Name = "Dibujar el contorno", GroupName = "3. Colores", Order = 240)]
        public bool VerBorde
        {
            get => _verBorde;
            set { _verBorde = value; _borde.VisualType = value ? VisualMode.Line : VisualMode.Hide; }
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
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            try { Calcular(bar); }
            catch (Exception e) { Rezongar("Delta.OnCalculate", e); }
        }

        private void Calcular(int bar)
        {
            Redimensionar(bar);
            MarcarPeriodos(bar);
            Acumular(bar);

            var a = ResolverAncla(bar);
            if (a < 0 || a > bar) return;

            var v = _sV[bar] - (a > 0 ? _sV[a - 1] : 0m);
            if (v <= 0) return;

            var vwap = (_sPv[bar] - (a > 0 ? _sPv[a - 1] : 0m)) / v;
            var twap = (_sP[bar] - (a > 0 ? _sP[a - 1] : 0m)) / (bar - a + 1);

            var d = vwap - twap;

            switch (Unidad)
            {
                case UnidadDelta.Ticks:
                    var ts = InstrumentInfo?.TickSize ?? 0m;
                    if (ts > 0) d /= ts;
                    break;
                case UnidadDelta.Porcentaje:
                    if (twap != 0) d = d / twap * 100m;
                    break;
            }

            _crudo[bar] = d;

            // El promedio movil nunca cruza el ancla: si lo hiciera, arrastraria
            // el tramo anterior adentro del actual y el primer tramo de cada
            // sesion vendria contaminado por la sesion de ayer.
            var val = d;
            if (Suavizado > 1)
            {
                decimal s = 0;
                var n = 0;
                for (var k = bar; k > bar - Suavizado && k >= a; k--) { s += _crudo[k]; n++; }
                if (n > 0) val = s / n;
            }

            _area[bar] = val;
            _area.Colors[bar] = val >= 0 ? ADrawing(ColorPositivo) : ADrawing(ColorNegativo);
            _borde[bar] = val;

            // Al reanclar se corta la linea: unir el valor viejo con el nuevo
            // dibujaria una transicion que nunca existio.
            if (a == bar && bar > 0)
            {
                _borde.SetPointOfEndLine(bar - 1);
                _area.SetPointOfEndLine(bar - 1);
            }
        }

        private void Redimensionar(int bar)
        {
            if (bar < _sPv.Length) return;
            var n = Math.Max(bar + 1024, (CurrentBar > 0 ? CurrentBar : bar) + 16);
            Array.Resize(ref _sPv, n);
            Array.Resize(ref _sV, n);
            Array.Resize(ref _sP, n);
            Array.Resize(ref _crudo, n);
            Array.Resize(ref _anclaExtremo, n);
        }

        private void MarcarPeriodos(int bar)
        {
            Anotar(_sesiones, bar, bar == 0 || IsNewSession(bar));
            Anotar(_semanas, bar, bar == 0 || IsNewWeek(bar));
            Anotar(_meses, bar, bar == 0 || IsNewMonth(bar));
        }

        private static void Anotar(List<int> lista, int bar, bool arranca)
        {
            while (lista.Count > 0 && lista[lista.Count - 1] >= bar)
                lista.RemoveAt(lista.Count - 1);
            if (arranca) lista.Add(bar);
        }

        /// <summary>
        /// Suma el aporte de una vela. Idempotente: la barra en curso se
        /// recalcula en cada tick sin duplicar nada.
        /// </summary>
        private void Acumular(int bar)
        {
            var c = GetCandle(bar);
            decimal pv = 0, vv = 0, p = 0;

            if (Fuente == FuenteDePrecio.Cluster)
            {
                foreach (var l in c.GetAllPriceLevels())
                {
                    decimal x;
                    switch (Volumen)
                    {
                        case TipoDeVolumen.Bid: x = l.Bid; break;
                        case TipoDeVolumen.Ask: x = l.Ask; break;
                        default: x = l.Volume; break;
                    }
                    if (x <= 0) continue;
                    pv += l.Price * x;
                    vv += x;
                }
                // El precio representativo de la vela para el lado TWAP: el
                // mismo VWAP interno de la vela, para que los dos lados de la
                // resta partan del mismo numero.
                p = vv > 0 ? pv / vv : (c.High + c.Low + c.Close) / 3m;
            }
            else
            {
                switch (Fuente)
                {
                    case FuenteDePrecio.Tipico: p = (c.High + c.Low + c.Close) / 3m; break;
                    case FuenteDePrecio.Cierre: p = c.Close; break;
                    case FuenteDePrecio.Ponderado: p = (c.High + c.Low + c.Close + c.Close) / 4m; break;
                    default: p = c.VWAP > 0 ? c.VWAP : (c.High + c.Low + c.Close) / 3m; break;
                }

                decimal x;
                switch (Volumen)
                {
                    case TipoDeVolumen.Bid: x = c.Bid; break;
                    case TipoDeVolumen.Ask: x = c.Ask; break;
                    default: x = c.Volume; break;
                }
                if (x <= 0) x = c.Volume;

                pv = p * x;
                vv = x;
            }

            _sPv[bar] = (bar > 0 ? _sPv[bar - 1] : 0m) + pv;
            _sV[bar] = (bar > 0 ? _sV[bar - 1] : 0m) + vv;
            _sP[bar] = (bar > 0 ? _sP[bar - 1] : 0m) + p;

            if (Modo == ModoDeAncla.MaximoDelRango || Modo == ModoDeAncla.MinimoDelRango
                || Modo == ModoDeAncla.MayorVolumen)
                SeguirExtremo(bar, c);
        }

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
                    var x = ValorExtremo(cc);
                    if (_extIdx < 0 || Mejor(x, _extVal)) { _extVal = x; _extIdx = b; }
                }
            }
            else
            {
                var x = ValorExtremo(c);
                if (Mejor(x, _extVal)) { _extVal = x; _extIdx = bar; }
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

        private int PorFecha(int bar)
        {
            if (GetCandle(bar).Time < FechaAncla) return -1;

            int lo = 0, hi = bar, r = bar;
            while (lo <= hi)
            {
                var m = (lo + hi) / 2;
                if (GetCandle(m).Time >= FechaAncla) { r = m; hi = m - 1; }
                else lo = m + 1;
            }
            return r;
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

        private static Color ADrawing(MColor c) => Color.FromArgb(c.A, c.R, c.G, c.B);

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
