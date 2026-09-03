using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Control;
using OFT.Rendering.Tools;

using MColor = System.Windows.Media.Color;

namespace PythiaVwap
{
    /// <summary>Como se decide el color de cada vela.</summary>
    public enum ModoDeFlujo
    {
        [Description("Tendencia del delta acumulado (como las barras 500/500)")] Tendencia,
        [Description("Delta de cada vela por separado")] PorVela,
        [Description("Delta promediado de las ultimas N velas")] Suavizado,
    }

    public enum EsquinaFlujo
    {
        [Description("Arriba a la izquierda")] ArribaIzquierda,
        [Description("Arriba a la derecha")] ArribaDerecha,
        [Description("Abajo a la izquierda")] AbajoIzquierda,
        [Description("Abajo a la derecha")] AbajoDerecha,
        [Description("Sin caja")] Ninguna,
    }

    /// <summary>
    /// PythiaFlow - Tendencia de Order Flow.
    ///
    /// Replica del "Luz Flow" de NinjaTrader, que corre sobre un grafico de
    /// barras `500/500 Order Flow Delta`.
    ///
    /// Conviene separar dos cosas que en esa captura vienen juntas:
    ///
    ///   1. El TIPO DE BARRA. En NinjaTrader, "500/500 Order Flow Delta"
    ///      significa que una vela nueva se abre cuando el delta acumulado
    ///      avanza 500 en la direccion de su tendencia (trend delta) o cuando
    ///      revierte 500 (trend reversal). Eso no es un indicador: es como se
    ///      arman las velas. ATAS trae barras de Delta nativas (500 / 1000 /
    ///      1500 en el selector de periodo), asi que esa mitad ya existe.
    ///
    ///   2. El COLOR. Es lo que hace este indicador.
    ///
    /// El modo principal, "Tendencia", es el que produce los tramos limpios de
    /// la captura: acumula el delta y mantiene el color mientras el flujo siga
    /// empujando para el mismo lado. Solo lo cambia cuando el delta acumulado
    /// **revierte** mas que el umbral desde su extremo. Un retroceso chico no
    /// cambia nada; por eso no alterna vela a vela.
    ///
    /// La flecha marca la vela exacta donde giro el flujo, que es el punto que
    /// el operador circulo en su captura.
    ///
    /// Lo que NO dice: si el giro va a funcionar. Marca que el flujo cambio de
    /// mano, nada mas. La caja de control muestra cuanto delta falta para el
    /// proximo giro, para que la marca no aparezca como un veredicto sino como
    /// una cuenta que se puede seguir.
    /// </summary>
    [DisplayName("PythiaFlow - Tendencia de Order Flow")]
    [Category("PythiaGex")]
    public class FlujoTendencia : Indicator
    {
        private readonly PaintbarsDataSeries _paint;
        private readonly ValueDataSeries _giroUp;
        private readonly ValueDataSeries _giroDn;

        // Estado por barra. Todo depende solo de la barra anterior, asi que el
        // recalculo de la vela en curso es idempotente.
        private decimal[] _acum = new decimal[0];
        private decimal[] _ext = new decimal[0];
        private sbyte[] _tend = new sbyte[0];
        private bool[] _giro = new bool[0];

        // Lo ultimo calculado, para la caja.
        private decimal _acumHoy, _extHoy, _faltaGiro;
        private sbyte _tendHoy;
        private int _velasEnTramo;

        public FlujoTendencia() : base(true)
        {
            Panel = IndicatorDataProvider.CandlesPanel;
            DenyToChangePanel = true;

            var cero = (ValueDataSeries)DataSeries[0];
            cero.VisualType = VisualMode.Hide;
            cero.IsHidden = true;
            cero.IgnoredByAlerts = true;

            _paint = new PaintbarsDataSeries("pb", "Color de la vela") { IsHidden = false };
            DataSeries.Add(_paint);

            _giroUp = new ValueDataSeries("gup", "Giro a comprador")
            {
                VisualType = VisualMode.UpArrow,
                Color = MColor.FromArgb(255, 60, 210, 110),
                Width = 2,
                ShowZeroValue = false,
                ShowCurrentValue = false,
            };
            _giroDn = new ValueDataSeries("gdn", "Giro a vendedor")
            {
                VisualType = VisualMode.DownArrow,
                Color = MColor.FromArgb(255, 235, 60, 90),
                Width = 2,
                ShowZeroValue = false,
                ShowCurrentValue = false,
            };
            DataSeries.Add(_giroUp);
            DataSeries.Add(_giroDn);

            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
        }

        // ==================================================================
        // Ajustes
        // ==================================================================
        private ModoDeFlujo _modo = ModoDeFlujo.Tendencia;
        [Display(Name = "Como se decide el color", GroupName = "1. Flujo", Order = 10)]
        public ModoDeFlujo Modo
        {
            get => _modo;
            set { _modo = value; RecalculateValues(); }
        }

        private decimal _reversion = 500m;
        [Display(Name = "Reversion para girar (contratos)", GroupName = "1. Flujo", Order = 20,
            Description = "Cuanto tiene que retroceder el delta acumulado desde su extremo para que el color cambie. Es el segundo numero del 500/500 de NinjaTrader. Mas alto = menos giros y tramos mas largos.")]
        [Range(1, 1000000)]
        public decimal Reversion
        {
            get => _reversion;
            set { _reversion = Math.Max(1, value); RecalculateValues(); }
        }

        private decimal _umbralVela = 0m;
        [Display(Name = "Delta minimo por vela", GroupName = "1. Flujo", Order = 30,
            Description = "Solo para el modo 'delta de cada vela'. Debajo de este valor la vela queda neutra en vez de pintarse. 0 = pintar siempre.")]
        public decimal UmbralVela
        {
            get => _umbralVela;
            set { _umbralVela = Math.Max(0, value); RecalculateValues(); }
        }

        private int _suavizado = 8;
        [Display(Name = "Velas del promedio", GroupName = "1. Flujo", Order = 40,
            Description = "Solo para el modo suavizado.")]
        [Range(1, 500)]
        public int Suavizado
        {
            get => _suavizado;
            set { _suavizado = Math.Max(1, value); RecalculateValues(); }
        }

        private bool _reiniciar = true;
        [Display(Name = "Reiniciar el conteo en cada sesion", GroupName = "1. Flujo", Order = 50,
            Description = "El delta acumulado vuelve a cero al abrir la sesion. Sin esto, el conteo arrastra dias enteros y el umbral deja de significar lo mismo.")]
        public bool Reiniciar
        {
            get => _reiniciar;
            set { _reiniciar = value; RecalculateValues(); }
        }

        // ==================================================================
        private MColor _colComp = MColor.FromArgb(255, 40, 200, 95);
        [Display(Name = "Flujo comprador", GroupName = "2. Colores", Order = 110)]
        public MColor ColorComprador
        {
            get => _colComp;
            set { _colComp = value; RecalculateValues(); }
        }

        private MColor _colVend = MColor.FromArgb(255, 230, 45, 75);
        [Display(Name = "Flujo vendedor", GroupName = "2. Colores", Order = 120)]
        public MColor ColorVendedor
        {
            get => _colVend;
            set { _colVend = value; RecalculateValues(); }
        }

        private MColor _colNeutro = MColor.FromArgb(255, 130, 135, 145);
        [Display(Name = "Neutro (solo modo por vela)", GroupName = "2. Colores", Order = 130)]
        public MColor ColorNeutro
        {
            get => _colNeutro;
            set { _colNeutro = value; RecalculateValues(); }
        }

        private bool _verFlechas = true;
        [Display(Name = "Marcar la vela del giro", GroupName = "2. Colores", Order = 140)]
        public bool VerFlechas
        {
            get => _verFlechas;
            set
            {
                _verFlechas = value;
                _giroUp.VisualType = value ? VisualMode.UpArrow : VisualMode.Hide;
                _giroDn.VisualType = value ? VisualMode.DownArrow : VisualMode.Hide;
            }
        }

        private int _offsetFlecha = 4;
        [Display(Name = "Separacion de la flecha (ticks)", GroupName = "2. Colores", Order = 150)]
        [Range(0, 200)]
        public int OffsetFlecha
        {
            get => _offsetFlecha;
            set { _offsetFlecha = Math.Max(0, value); RecalculateValues(); }
        }

        // ==================================================================
        private EsquinaFlujo _esquina = EsquinaFlujo.ArribaIzquierda;
        [Display(Name = "Caja de control", GroupName = "3. Control", Order = 210,
            Description = "Muestra el delta acumulado, el extremo del tramo y cuanto falta para el proximo giro.")]
        public EsquinaFlujo Esquina
        {
            get => _esquina;
            set => _esquina = value;
        }

        private int _bajarCaja = 36;
        [Display(Name = "Bajar la caja (px)", GroupName = "3. Control", Order = 220,
            Description = "ATAS escribe el instrumento y el OHLC arriba del area del grafico.")]
        [Range(0, 600)]
        public int BajarCaja
        {
            get => _bajarCaja;
            set => _bajarCaja = Math.Max(0, value);
        }

        private string _tipografia = "Consolas";
        [Display(Name = "Tipografia", GroupName = "3. Control", Order = 230)]
        public string Tipografia
        {
            get => _tipografia;
            set => _tipografia = value;
        }

        private float _tamFuente = 9f;
        [Display(Name = "Tamano de letra", GroupName = "3. Control", Order = 240)]
        [Range(5, 30)]
        public float TamFuente
        {
            get => _tamFuente;
            set => _tamFuente = Math.Max(5f, value);
        }

        // ==================================================================
        // Calculo
        // ==================================================================
        protected override void OnCalculate(int bar, decimal value)
        {
            try { Calcular(bar); }
            catch (Exception e) { Rezongar("Flujo.OnCalculate", e); }
        }

        private void Calcular(int bar)
        {
            Redimensionar(bar);

            var c = GetCandle(bar);
            var abre = Reiniciar && (bar == 0 || IsNewSession(bar));

            switch (Modo)
            {
                case ModoDeFlujo.PorVela: PintarPorVela(bar, c); return;
                case ModoDeFlujo.Suavizado: PintarSuavizado(bar, c); return;
            }

            // ---- Modo tendencia: el delta acumulado y su extremo.
            var accPrev = (bar > 0 && !abre) ? _acum[bar - 1] : 0m;
            var acc = accPrev + c.Delta;

            var tPrev = (bar > 0 && !abre) ? _tend[bar - 1] : (sbyte)(c.Delta >= 0 ? 1 : -1);
            var ePrev = (bar > 0 && !abre) ? _ext[bar - 1] : acc;

            sbyte t;
            decimal ext;
            var giro = false;

            if (tPrev > 0)
            {
                ext = acc > ePrev ? acc : ePrev;
                if (ext - acc >= Reversion) { t = -1; ext = acc; giro = true; }
                else t = 1;
            }
            else
            {
                ext = acc < ePrev ? acc : ePrev;
                if (acc - ext >= Reversion) { t = 1; ext = acc; giro = true; }
                else t = -1;
            }

            // Al abrir sesion el tramo arranca de cero: no hay giro que marcar.
            if (abre) { giro = false; ext = acc; }

            _acum[bar] = acc;
            _ext[bar] = ext;
            _tend[bar] = t;
            _giro[bar] = giro;

            _paint[bar] = t > 0 ? ColorComprador : ColorVendedor;

            var off = OffsetFlecha * (InstrumentInfo?.TickSize ?? 0.25m);
            _giroUp[bar] = giro && t > 0 ? c.Low - off : 0m;
            _giroDn[bar] = giro && t < 0 ? c.High + off : 0m;

            if (bar == CurrentBar - 1)
            {
                _acumHoy = acc;
                _extHoy = ext;
                _tendHoy = t;
                _faltaGiro = Reversion - Math.Abs(ext - acc);
                if (_faltaGiro < 0) _faltaGiro = 0;

                var n = 0;
                for (var k = bar; k >= 0 && n < 5000; k--)
                {
                    if (_tend[k] != t) break;
                    n++;
                    if (k > 0 && Reiniciar && IsNewSession(k)) break;
                }
                _velasEnTramo = n;
            }
        }

        private void PintarPorVela(int bar, IndicatorCandle c)
        {
            var d = c.Delta;
            _paint[bar] = d > UmbralVela ? ColorComprador
                        : d < -UmbralVela ? ColorVendedor
                        : ColorNeutro;
            _giroUp[bar] = 0m;
            _giroDn[bar] = 0m;

            if (bar == CurrentBar - 1)
            {
                _acumHoy = d;
                _extHoy = 0;
                _faltaGiro = 0;
                _tendHoy = (sbyte)(d >= 0 ? 1 : -1);
                _velasEnTramo = 0;
            }
        }

        private void PintarSuavizado(int bar, IndicatorCandle c)
        {
            decimal s = 0;
            var n = 0;
            for (var k = bar; k > bar - Suavizado && k >= 0; k--)
            {
                s += GetCandle(k).Delta;
                n++;
            }
            var prom = n > 0 ? s / n : 0m;

            _paint[bar] = prom > UmbralVela ? ColorComprador
                        : prom < -UmbralVela ? ColorVendedor
                        : ColorNeutro;
            _giroUp[bar] = 0m;
            _giroDn[bar] = 0m;

            if (bar == CurrentBar - 1)
            {
                _acumHoy = prom;
                _extHoy = 0;
                _faltaGiro = 0;
                _tendHoy = (sbyte)(prom >= 0 ? 1 : -1);
                _velasEnTramo = 0;
            }
        }

        private void Redimensionar(int bar)
        {
            if (bar < _acum.Length) return;
            var n = Math.Max(bar + 1024, (CurrentBar > 0 ? CurrentBar : bar) + 16);
            Array.Resize(ref _acum, n);
            Array.Resize(ref _ext, n);
            Array.Resize(ref _tend, n);
            Array.Resize(ref _giro, n);
        }

        // ==================================================================
        // La caja
        // ==================================================================
        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            try { Caja(g); }
            catch (Exception e) { Rezongar("Flujo.OnRender", e); }
        }

        private void Caja(RenderContext g)
        {
            if (ChartInfo == null || Esquina == EsquinaFlujo.Ninguna) return;
            if (Modo != ModoDeFlujo.Tendencia) return;

            var area = ChartArea;
            var f = new RenderFont(string.IsNullOrWhiteSpace(Tipografia) ? "Consolas" : Tipografia,
                                   TamFuente);

            var lado = _tendHoy > 0 ? "COMPRADOR" : "VENDEDOR";
            var filas = new List<string>
            {
                "FLUJO: " + lado,
                "delta acumulado   " + N(_acumHoy),
                "extremo del tramo " + N(_extHoy),
                "falta para girar  " + N(_faltaGiro),
                "velas en el tramo " + _velasEnTramo.ToString(CultureInfo.InvariantCulture),
            };

            var alto = (int)g.MeasureString("Xy", f).Height + 2;
            var ancho = 0;
            foreach (var t in filas)
                ancho = Math.Max(ancho, (int)g.MeasureString(t, f).Width);

            var w = ancho + 14;
            var h = alto * filas.Count + 10;
            var arriba = Esquina == EsquinaFlujo.ArribaIzquierda || Esquina == EsquinaFlujo.ArribaDerecha;
            var izq = Esquina == EsquinaFlujo.ArribaIzquierda || Esquina == EsquinaFlujo.AbajoIzquierda;

            var cx = izq ? area.Left + 8 : area.Right - w - 70;
            var cy = arriba ? area.Top + 8 + BajarCaja : area.Bottom - h - 8;
            var caja = new Rectangle(cx, cy, w, h);

            g.FillRectangle(Color.FromArgb(215, 12, 15, 20), caja);
            g.DrawRectangle(new RenderPen(Color.FromArgb(90, 120, 130, 145), 1), caja);

            var y = caja.Top + 5;
            for (var i = 0; i < filas.Count; i++)
            {
                var col = i == 0
                    ? (_tendHoy > 0 ? ADrawing(ColorComprador) : ADrawing(ColorVendedor))
                    : Color.FromArgb(205, 210, 220);
                g.DrawString(filas[i], f, col, caja.Left + 7, y);
                y += alto;
            }
        }

        private static string N(decimal v)
            => Math.Round(v, 0).ToString("#,##0", CultureInfo.InvariantCulture);

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
