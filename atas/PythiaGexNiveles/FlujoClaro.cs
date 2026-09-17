using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGex
{
    /// <summary>
    /// FLUJO CLARO (17-09-2026). El reemplazo a medida del "CVD - Cumulative Volume Delta" de fabrica, pedido por el
    /// operador: "es chiquito, apenas se entiende, y siento que me avisa tarde". Es la FUSION 1 de las maquetas
    /// (conocimiento/cvd-superador): cinco cintas pegadas al precio (confluencia, presion, grandes, cinta, absorcion),
    /// abajo el CVD ANCLADO con las barras de presion, marcas de absorcion y divergencia SOBRE las velas, y el bloque
    /// AHORA con el flujo de los ultimos 5 s, 15 s, 60 s y 5 min calculado con cada operacion.
    ///
    /// LO QUE ESTA MEDIDO (laboratorio/cvd_superador.py y cvd_fusion.py, velas propias del 08 al 17-09):
    ///   - Legibilidad: con el CVD del dia entero la ultima hora ocupa el 27-35 % del alto del panel. Anclado, el 100 %.
    ///   - El delta y el precio se mueven JUNTOS: correlacion +0,69 a +0,82 en la misma vela, -0,03 a -0,09 con la
    ///     siguiente. Ninguna lectura de flujo adelanta: dice bien lo que pasa AHORA.
    ///   - Ninguna señal de vela (presion, z, mecha, confluencia) le gano al azar a 5 velas. Con las 4 lecturas del mismo
    ///     lado el precio fue a favor solo el 40 % de las veces (96 casos, z -1,4; con 3 o mas, 48 %): por eso el bloque
    ///     dice "EXTREMO: no perseguir" y NO "compra". Es una tendencia debil, no una prueba.
    ///   - Absorcion (mucho delta sin avance): 46 % en contra del delta con 24 casos (z +0,3): SIN evidencia todavia.
    ///     (Con un percentil global que miraba adelante habia dado 57 % con 21 casos: era un artefacto.)
    /// GRAMATICA DE LAS CINTAS (1.4, pedido del operador: "que el color corresponda con la vela, sin negros, sin llegar tarde"):
    ///   verde / rojo = el flujo ACOMPAÑA a la vela que tiene encima (brillo = cuanto)   violeta = el flujo le lleva LA CONTRA
    ///   gris = vela de indecision (doji / martillo) o flujo parejo                       negro = no hubo nada que medir
    /// Coincidir con la vela es por construccion, NO es prediccion: la vela siguiente sale del color de la celda el 50 % de las veces.
    /// Lo que la cinta agrega a la vela es CUANTO flujo la respalda y CUANDO el flujo la contradice.
    /// Por eso todo lo que se marca es LECTURA. Cada marca en vivo queda en
    /// %APPDATA%\ATAS\PythiaGex\flujo\marcas-<instrumento>-<dia>.jsonl para medirla contra placebo.
    ///
    /// Los nombres de los ajustes llevan el prefijo Fc a proposito: ATAS guarda los ajustes POR NOMBRE en el workspace.
    /// </summary>
    [DisplayName("PythiaGex - Flujo Claro")]
    [Category("PythiaGex")]
    public class FlujoClaro : Indicator
    {
        public enum ModoAnclaCvd { UltimasVelas, Visible, Sesion }
        public enum DibujoDelCvd { Linea, VelasConMecha }
        public enum LugarDelAhora { Auto, PanelIzquierda, PanelDerecha, Oculto }
        public enum FormaDelAhora { FilaArriba, FilaAbajo, Bloque, Oculto }
        public enum FormaDeMarca { Rombo, Punto, Cuadrado }

        // ------------------------------------------------------------------ 1. Panel
        [Display(Name = "CVD: desde donde se acumula", GroupName = "1. Panel CVD", Order = 1,
                 Description = "UltimasVelas = el cero es el CVD de hace N velas (la forma de la curva no cambia, solo la escala: se lee 3-4 veces mas alto). Visible = el cero es la primera vela visible. Sesion = como el de fabrica.")]
        public ModoAnclaCvd FcAncla { get; set; } = ModoAnclaCvd.UltimasVelas;

        [Display(Name = "CVD: cuantas velas (modo UltimasVelas)", GroupName = "1. Panel CVD", Order = 2)]
        [Range(10, 1000)]
        public int FcAnclaVelas { get; set; } = 60;

        [Display(Name = "CVD: dibujo", GroupName = "1. Panel CVD", Order = 3, Description = "Linea gruesa (verde sube, roja baja) o velas de delta con mecha (maximo y minimo del delta dentro de la vela).")]
        public DibujoDelCvd FcDibujo { get; set; } = DibujoDelCvd.Linea;

        [Display(Name = "CVD: grosor de la linea (px)", GroupName = "1. Panel CVD", Order = 4)]
        [Range(1, 6)]
        public int FcGrosor { get; set; } = 2;

        [Display(Name = "Presion: dibujar las barras", GroupName = "1. Panel CVD", Order = 5,
                 Description = "Presion = delta de las ultimas velas comparado con lo normal de la ultima hora, en desvios (sigmas). Es el 'chorro de la canilla': se mueve antes que el nivel de la bañera (el CVD).")]
        public bool FcVerPresion { get; set; } = true;

        [Display(Name = "Presion: velas que suma", GroupName = "1. Panel CVD", Order = 6)]
        [Range(1, 30)]
        public int FcPresionVelas { get => _fcVentanaPresion; set { if (_fcVentanaPresion == value) return; _fcVentanaPresion = value; try { RecalculateValues(); } catch { } } }
        private int _fcVentanaPresion = 2;

        [Display(Name = "Vela de indecision: cuerpo menor a (% del rango) = celda gris", GroupName = "1. Panel CVD", Order = 8,
                 Description = "Doji o martillo: el cuerpo es chico contra el recorrido de la vela. La celda va GRIS (estado con nombre, no un hueco). Si ademas hubo mucho delta, la fila absorcion lo marca.")]
        [Range(0, 60)]
        public int FcIndecisionPct { get => _fcIndecision; set { if (_fcIndecision == value) return; _fcIndecision = value; try { RecalculateValues(); } catch { } } }
        private int _fcIndecision = 15;

        [Display(Name = "Delta que cuenta: desde que percentil de la ultima hora", GroupName = "1. Panel CVD", Order = 9,
                 Description = "El tamaño del delta se mide por su lugar entre los de las ultimas velas (0 = el mas chico, 100 = el mas grande). Por debajo de este piso el delta es ruido: la celda toma el color de la vela a media luz.")]
        [Range(0, 90)]
        public int FcPisoDelta { get => _fcPisoDelta; set { if (_fcPisoDelta == value) return; _fcPisoDelta = value; try { RecalculateValues(); } catch { } } }
        private int _fcPisoDelta = 20;

        [Display(Name = "Celda VIOLETA (vela contra el delta): desde que percentil", GroupName = "1. Panel CVD", Order = 10,
                 Description = "Vela verde con delta vendedor (o roja con delta comprador) de este tamaño o mas: el precio le gano al flujo. Es un estado con nombre, no un error: pasa en ~3 de cada 100 velas con cuerpo.")]
        [Range(0, 95)]
        public int FcContraDesde { get => _fcContraDesde; set { if (_fcContraDesde == value) return; _fcContraDesde = value; try { RecalculateValues(); } catch { } } }
        private int _fcContraDesde = 35;

        [Display(Name = "Presion: velas de referencia (lo normal)", GroupName = "1. Panel CVD", Order = 7)]
        [Range(20, 600)]
        public int FcVentanaNormal { get => _fcVentanaNormal; set { if (_fcVentanaNormal == value) return; _fcVentanaNormal = value; try { RecalculateValues(); } catch { } } }
        private int _fcVentanaNormal = 60;

        // ------------------------------------------------------------------ 2. Cintas
        [Display(Name = "Cinta CONFLUENCIA", GroupName = "2. Cintas", Order = 1, Description = "Cuantas de las 4 lecturas de flujo (delta de la vela, donde cerro el delta, grandes, CVD de 20 velas) ACOMPAÑAN a la vela, menos las que van en contra. Color de la vela = la acompañan (intensa = las 4; medido: intensa suele ser TARDE, no entrada). Violeta = el flujo le lleva la contra. Gris = parejo o vela de indecision.")]
        public bool FcCintaConfluencia { get; set; } = true;
        [Display(Name = "Cinta PRESION", GroupName = "2. Cintas", Order = 2)]
        public bool FcCintaPresion { get; set; } = true;
        [Display(Name = "Cinta GRANDES", GroupName = "2. Cintas", Order = 3, Description = "Compras menos ventas de las ordenes agresoras de tamaño mayor o igual al umbral, por vela.")]
        public bool FcCintaGrandes { get; set; } = true;
        [Display(Name = "Grandes: violeta cuando van contra la vela", GroupName = "2. Cintas", Order = 3,
                 Description = "Misma gramatica que las otras cintas: verde/rojo = los grandes acompañan a la vela; violeta = operaron en contra. Apagado: verde = compraron, rojo = vendieron, sin mirar la vela.")]
        public bool FcGrandesContraVela { get; set; } = true;
        [Display(Name = "Cinta CINTA (velocidad)", GroupName = "2. Cintas", Order = 4, Description = "Cantidad de operaciones de la vela contra lo normal: hay apuro AHORA. No dice direccion.")]
        public bool FcCintaVelocidad { get; set; } = true;
        [Display(Name = "Cinta ABSORCION", GroupName = "2. Cintas", Order = 5)]
        public bool FcCintaAbsorcion { get; set; } = true;
        [Display(Name = "Alto de cada cinta (px, 0 = automatico)", GroupName = "2. Cintas", Order = 6)]
        [Range(0, 30)]
        public int FcAltoFila { get; set; } = 0;
        [Display(Name = "Rotulos de las cintas", GroupName = "2. Cintas", Order = 7)]
        public bool FcRotulos { get; set; } = true;

        // ------------------------------------------------------------------ 3. Marcas
        [Display(Name = "Marcar ABSORCION sobre la vela", GroupName = "3. Marcas en el precio", Order = 1,
                 Description = "Mucho delta y el precio avanza menos de lo que ese delta suele mover (esfuerzo sin resultado). Rombo lleno = vela cerrada; hueco = vela en curso. Marca de LECTURA.")]
        public bool FcMarcaAbsorcion { get; set; } = true;
        [Display(Name = "Marcar DIVERGENCIA sobre la vela", GroupName = "3. Marcas en el precio", Order = 2,
                 Description = "El precio hace un maximo nuevo de N velas y el CVD queda por debajo del maximo anterior (triangulo rojo), o el espejo (verde). Marca de LECTURA, no validada como señal.")]
        public bool FcMarcaDivergencia { get; set; } = true;
        [Display(Name = "Tamaño de las marcas (px)", GroupName = "3. Marcas en el precio", Order = 3)]
        [Range(2, 16)]
        public int FcTamMarca { get; set; } = 5;
        [Display(Name = "Forma de la absorcion", GroupName = "3. Marcas en el precio", Order = 4)]
        public FormaDeMarca FcFormaAbsorcion { get; set; } = FormaDeMarca.Rombo;
        [Display(Name = "Divergencia: velas del maximo/minimo", GroupName = "3. Marcas en el precio", Order = 5)]
        [Range(5, 200)]
        public int FcDivVelas { get => _fcDivVelas; set { if (_fcDivVelas == value) return; _fcDivVelas = value; try { RecalculateValues(); } catch { } } }
        private int _fcDivVelas = 20;
        [Display(Name = "Divergencia: separacion minima entre extremos (velas)", GroupName = "3. Marcas en el precio", Order = 6)]
        [Range(1, 60)]
        public int FcDivSeparacion { get => _fcDivSeparacion; set { if (_fcDivSeparacion == value) return; _fcDivSeparacion = value; try { RecalculateValues(); } catch { } } }
        private int _fcDivSeparacion = 5;

        // ------------------------------------------------------------------ 4. Reglas
        [Display(Name = "Operacion GRANDE: contratos", GroupName = "4. Reglas", Order = 1)]
        [Range(1, 5000)]
        public int FcUmbralGrande { get => _fcUmbralGrande; set { if (_fcUmbralGrande == value) return; _fcUmbralGrande = value; try { RecalculateValues(); } catch { } } }
        private int _fcUmbralGrande = 50;
        [Display(Name = "Grandes: dias de historia a pedirle a ATAS (0 = solo en vivo)", GroupName = "4. Reglas", Order = 2)]
        [Range(0, 5)]
        public int FcDiasGrandes { get => _fcDiasGrandes; set { if (_fcDiasGrandes == value) return; _fcDiasGrandes = value; try { RecalculateValues(); } catch { } } }
        private int _fcDiasGrandes = 2;
        [Display(Name = "Absorcion: percentil del delta (mucho delta)", GroupName = "4. Reglas", Order = 3)]
        [Range(50, 99)]
        public int FcAbsPercentil { get => _fcAbsPercentil; set { if (_fcAbsPercentil == value) return; _fcAbsPercentil = value; try { RecalculateValues(); } catch { } } }
        private int _fcAbsPercentil = 80;
        [Display(Name = "Absorcion: avance maximo (% de lo habitual)", GroupName = "4. Reglas", Order = 4)]
        [Range(0, 100)]
        public int FcAbsAvancePct { get => _fcAbsAvancePct; set { if (_fcAbsAvancePct == value) return; _fcAbsAvancePct = value; try { RecalculateValues(); } catch { } } }
        private int _fcAbsAvancePct = 25;
        [Display(Name = "Absorcion: velas para medir cuanto es mucho delta", GroupName = "4. Reglas", Order = 6,
                 Description = "El percentil se mide contra las ultimas N velas. Con 300 entraban velas de madrugada y en la apertura de Nueva York pasaba el 70 % de las velas (revision 17-09).")]
        [Range(30, 600)]
        public int FcAbsVentana { get => _fcAbsVentana; set { if (_fcAbsVentana == value) return; _fcAbsVentana = value; try { RecalculateValues(); } catch { } } }
        private int _fcAbsVentana = 60;

        [Display(Name = "Extremo: lecturas del mismo lado", GroupName = "4. Reglas", Order = 5, Description = "Con 3 o 4 lecturas del mismo lado el bloque AHORA avisa EXTREMO: no perseguir (medido: a favor solo el 42-45 % de las veces).")]
        [Range(2, 4)]
        public int FcExtremoDesde { get => _fcExtremoDesde; set { if (_fcExtremoDesde == value) return; _fcExtremoDesde = value; try { RecalculateValues(); } catch { } } }
        private int _fcExtremoDesde = 3;

        // ------------------------------------------------------------------ 5. Ahora
        [Display(Name = "AHORA: como se muestra", GroupName = "5. Bloque AHORA", Order = 0,
                 Description = "FilaArriba = una fila en el renglon del titulo del panel (no ocupa espacio extra). FilaAbajo = una fila al pie del panel, con mas datos. Bloque = el cuadro grande. Oculto = nada.")]
        public FormaDelAhora FcAhoraComo { get; set; } = FormaDelAhora.FilaArriba;

        [Display(Name = "AHORA en fila arriba: ancho reservado al titulo de ATAS (px)", GroupName = "5. Bloque AHORA", Order = 0)]
        [Range(0, 600)]
        public int FcAnchoTitulo { get; set; } = 250;

        [Display(Name = "Bloque AHORA: donde", GroupName = "5. Bloque AHORA", Order = 1,
                 Description = "Auto = a la derecha del panel si hay margen libre despues de la vela en curso; si no, a la izquierda (tapa las velas mas viejas del panel, nunca la actual).")]
        public LugarDelAhora FcBloqueAhora { get; set; } = LugarDelAhora.Auto;
        [Display(Name = "Bloque AHORA: ancho (px)", GroupName = "5. Bloque AHORA", Order = 2)]
        [Range(120, 400)]
        public int FcAnchoAhora { get; set; } = 210;
        [Display(Name = "Margen del eje de precios (px)", GroupName = "5. Bloque AHORA", Order = 3, Description = "El eje de precios se dibuja encima del borde derecho: lo que cae ahi no se ve.")]
        [Range(0, 200)]
        public int FcMargenEje { get; set; } = 0;
        [Display(Name = "Tamaño de letra", GroupName = "5. Bloque AHORA", Order = 4)]
        [Range(6, 16)]
        public int FcLetra { get; set; } = 9;

        // ------------------------------------------------------------------ 6. Colores
        [Display(Name = "Color compra", GroupName = "6. Colores", Order = 1)]
        public System.Windows.Media.Color FcColorCompra { get; set; } = System.Windows.Media.Color.FromRgb(38, 166, 154);
        [Display(Name = "Color venta", GroupName = "6. Colores", Order = 2)]
        public System.Windows.Media.Color FcColorVenta { get; set; } = System.Windows.Media.Color.FromRgb(239, 83, 80);
        [Display(Name = "Color aviso (absorcion, cinta rapida, extremo)", GroupName = "6. Colores", Order = 3)]
        public System.Windows.Media.Color FcColorAviso { get; set; } = System.Windows.Media.Color.FromRgb(255, 200, 87);
        [Display(Name = "Color CONTRA (la vela va contra el flujo)", GroupName = "6. Colores", Order = 3)]
        public System.Windows.Media.Color FcColorContra { get; set; } = System.Windows.Media.Color.FromRgb(179, 136, 255);
        [Display(Name = "Color INDECISION (doji, flujo parejo)", GroupName = "6. Colores", Order = 3)]
        public System.Windows.Media.Color FcColorIndecision { get; set; } = System.Windows.Media.Color.FromRgb(125, 133, 150);
        [Display(Name = "Color texto", GroupName = "6. Colores", Order = 4)]
        public System.Windows.Media.Color FcColorTexto { get; set; } = System.Windows.Media.Color.FromRgb(184, 192, 208);
        [Display(Name = "Color fondo de los rotulos", GroupName = "6. Colores", Order = 5)]
        public System.Windows.Media.Color FcColorFondo { get; set; } = System.Windows.Media.Color.FromRgb(14, 17, 24);

        // ------------------------------------------------------------------ 7. Alertas y registro
        [Display(Name = "Alerta: absorcion (vela cerrada)", GroupName = "7. Alertas y registro", Order = 1)]
        public bool FcAlertaAbsorcion { get; set; } = false;
        [Display(Name = "Alerta: divergencia", GroupName = "7. Alertas y registro", Order = 2)]
        public bool FcAlertaDivergencia { get; set; } = false;
        [Display(Name = "Alerta: extremo (no perseguir)", GroupName = "7. Alertas y registro", Order = 3)]
        public bool FcAlertaExtremo { get; set; } = false;
        [Display(Name = "Sonido de las alertas", GroupName = "7. Alertas y registro", Order = 4)]
        public string FcSonido { get; set; } = "alert1";
        [Display(Name = "Grabar cada marca en vivo (para medirla contra placebo)", GroupName = "7. Alertas y registro", Order = 5)]
        public bool FcGuardarMarcas { get; set; } = true;

        // ------------------------------------------------------------------ estado
        private readonly object _llave = new object();
        private readonly List<double> _d = new(), _vol = new(), _tk = new(), _dmax = new(), _dmin = new(), _o = new(), _h = new(), _l = new(), _c = new();
        private readonly List<double> _cvdC = new(), _cvdS = new(), _z = new(), _vel = new(), _big = new();
        private readonly List<bool> _abs = new();
        private readonly List<int> _conf = new(), _div = new(), _tono = new(), _tonoC = new();
        private readonly List<double> _bri = new(), _briC = new(), _lugar = new();
        private const int Contra = 2, Gris = 3, Aviso = 4;   // tonos de celda, ademas de +1 (compra) y -1 (venta)
        private Color _colContra, _colGris;
        private readonly List<DateTime> _t = new(), _tf = new();
        private int _cerradaHasta = -1, _vivoDesde = int.MaxValue;
        private bool _cargado;
        private (int Bar, double Cvd, double Precio) _ultMax = (-1, 0, 0), _ultMin = (-1, 0, 0);
        private DateTime _ultimoProvisorio = DateTime.MinValue;
        private int _barraProvisoria = -1;
        private double _proyTicks = 1;
        // operaciones en vivo: cubetas por segundo para el bloque AHORA
        private readonly Dictionary<long, (double S, double V)> _cubetas = new();
        private long _ultSeg; private DateTime _relojUltTrade = DateTime.MinValue;
        // grandes en vivo
        private CumulativeTrade _bigActual; private bool _histPedido; private int _histTrades, _histFuera;
        private bool _volcadoEntero;
        private static readonly CultureInfo Es = new CultureInfo("es-AR"), Inv = CultureInfo.InvariantCulture;
        private DateTime _ultimoError = DateTime.MinValue;

        public FlujoClaro() : base(true)
        {
            Panel = IndicatorDataProvider.NewPanel;
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
            DrawAbovePrice = true;
            if (DataSeries.Count > 0 && DataSeries[0] is ValueDataSeries v)
            {
                v.IsHidden = true; v.VisualType = VisualMode.Hide; v.ShowCurrentValue = false; v.ShowZeroValue = false;
            }
        }

        protected override void OnInitialize()
        {
            try { SubscribeToTimer(TimeSpan.FromSeconds(1), () => { try { RedrawChart(new RedrawArg(ChartArea)); } catch { } }); } catch { }
            Log("Flujo Claro 1.4 (la celda habla de su vela) arranca en " + (InstrumentInfo?.Instrument ?? "?"));
        }

        // ------------------------------------------------------------------ calculo
        private void Reiniciar()
        {
            lock (_llave)
            {
                foreach (var ls in new[] { _d, _vol, _tk, _dmax, _dmin, _o, _h, _l, _c, _cvdC, _cvdS, _z, _vel, _big }) ls.Clear();
                _abs.Clear(); _conf.Clear(); _div.Clear(); _t.Clear(); _tf.Clear(); _tono.Clear(); _tonoC.Clear(); _bri.Clear(); _briC.Clear(); _lugar.Clear();
                _cerradaHasta = -1; _vivoDesde = int.MaxValue; _cargado = false; _volcadoEntero = false; _ultMax = (-1, 0, 0); _ultMin = (-1, 0, 0); _histPedido = false;
            }
        }

        private void Asegurar(int bar)
        {
            while (_d.Count <= bar)
            {
                _d.Add(0); _vol.Add(0); _tk.Add(0); _dmax.Add(0); _dmin.Add(0); _o.Add(0); _h.Add(0); _l.Add(0); _c.Add(0);
                _cvdC.Add(0); _cvdS.Add(0); _z.Add(0); _vel.Add(0); _big.Add(0); _abs.Add(false); _conf.Add(0); _div.Add(0); _t.Add(DateTime.MinValue); _tf.Add(DateTime.MinValue); _tono.Add(0); _tonoC.Add(0); _bri.Add(0); _briC.Add(0); _lugar.Add(0);
            }
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            try
            {
                if (bar == 0) Reiniciar();
                var cn = GetCandle(bar); if (cn == null) return;
                lock (_llave)
                {
                    Asegurar(bar);
                    _d[bar] = (double)cn.Delta; _vol[bar] = (double)cn.Volume; _tk[bar] = (double)cn.Ticks; _dmax[bar] = (double)cn.MaxDelta; _dmin[bar] = (double)cn.MinDelta;
                    _o[bar] = (double)cn.Open; _h[bar] = (double)cn.High; _l[bar] = (double)cn.Low; _c[bar] = (double)cn.Close; _t[bar] = cn.Time; _tf[bar] = cn.LastTime;
                    bool nueva = false; try { nueva = bar > 0 && IsNewSession(bar); } catch { }
                    _cvdC[bar] = (bar > 0 ? _cvdC[bar - 1] : 0) + _d[bar];
                    _cvdS[bar] = (bar > 0 && !nueva ? _cvdS[bar - 1] : 0) + _d[bar];
                    for (int k = _cerradaHasta + 1; k < bar; k++) Cerrar(k);
                    bool viva = bar == CurrentBar - 1;
                    if (viva && !_cargado) { _cargado = true; _vivoDesde = bar; }
                    var ahora = DateTime.UtcNow;
                    // las velas historicas se derivan una sola vez, al cerrar (Cerrar); la viva, cada 300 ms como mucho
                    if (viva && (bar != _barraProvisoria || (ahora - _ultimoProvisorio).TotalMilliseconds >= 300))
                    {
                        _barraProvisoria = bar; _ultimoProvisorio = ahora;
                        _proyTicks = 1;
                        if (bar > 2)
                        {
                            double dur = Math.Min((_t[bar] - _t[bar - 1]).TotalSeconds, (_t[bar - 1] - _t[bar - 2]).TotalSeconds), tr = (cn.LastTime - cn.Time).TotalSeconds;
                            if (dur > 0 && dur <= 3600 && tr >= 5 && tr < dur) _proyTicks = dur / tr;   // solo velas de tiempo parejo
                        }
                        Derivar(bar);
                    }
                }
                if (_cargado && !_histPedido) { _histPedido = true; PedirGrandesHistoricos(); }
            }
            catch (Exception e) { Registrar(e); }
        }

        /// <summary>Todo lo que sale de las velas hasta 'bar' inclusive, sin mirar adelante.</summary>
        private void Derivar(int bar)
        {
            int w = Math.Max(1, FcPresionVelas), n = Math.Max(20, FcVentanaNormal);
            // presion: suma de w velas contra el desvio del delta por vela de las ultimas n
            double s = 0; for (int k = Math.Max(0, bar - w + 1); k <= bar; k++) s += _d[k];
            int a0 = Math.Max(0, bar - n + 1), m = bar - a0 + 1;
            double med = 0; for (int k = a0; k <= bar; k++) med += _d[k]; med /= m;
            double var = 0; for (int k = a0; k <= bar; k++) var += (_d[k] - med) * (_d[k] - med);
            double sd = m > 5 ? Math.Sqrt(var / m) : 0;
            _z[bar] = sd > 0 ? Math.Max(-3, Math.Min(3, s / (sd * Math.Sqrt(w)))) : 0;
            // velocidad de la cinta: operaciones de la vela contra lo normal
            double mt = 0; for (int k = a0; k <= bar; k++) mt += _tk[k]; mt /= m;
            double vt = 0; for (int k = a0; k <= bar; k++) vt += (_tk[k] - mt) * (_tk[k] - mt);
            double sdt = m > 5 ? Math.Sqrt(vt / m) : 0;
            double tkAhora = _tk[bar] * (bar == CurrentBar - 1 ? _proyTicks : 1);
            _vel[bar] = sdt > 0 ? Math.Max(0, Math.Min(3, (tkAhora - mt) / sdt)) : 0;
            // absorcion: mucho delta (percentil) y el precio avanzo menos de lo que ese delta suele mover
            int b0 = Math.Max(0, bar - Math.Max(30, FcAbsVentana) + 1);
            var ab = new List<double>(bar - b0 + 1); for (int k = b0; k <= bar; k++) ab.Add(Math.Abs(_d[k])); ab.Sort();
            double pAlto = ab[(int)Math.Min(ab.Count - 1, ab.Count * Math.Max(50, FcAbsPercentil) / 100.0)];
            double medAbs = ab[ab.Count / 2];
            var rs = new List<double>();
            for (int k = Math.Max(0, bar - 120); k < bar; k++) if (Math.Abs(_d[k]) >= Math.Max(1, medAbs)) rs.Add(Math.Abs(_c[k] - _o[k]) / Math.Abs(_d[k]));
            bool abs = false; double beta = 0;
            if (rs.Count >= 20) { rs.Sort(); beta = rs[rs.Count / 2]; }
            if (beta > 0 && Math.Abs(_d[bar]) >= pAlto && pAlto > 0)
            {
                double avance = (_c[bar] - _o[bar]) * (_d[bar] > 0 ? 1 : -1);
                abs = avance <= FcAbsAvancePct / 100.0 * beta * Math.Abs(_d[bar]);
            }
            _abs[bar] = abs;
            // ---- 1.4: LA CELDA HABLA DE LA VELA QUE TIENE ENCIMA (pedido 17-09: "el color no corresponde con la vela, avisa tarde,
            // muchos negros"). Medido (laboratorio/cvd_coherencia.py): la regla anterior dejaba apagado el 44 % de las velas con cuerpo,
            // porque el desvio estandar de 60 velas la cegaba despues de una vela enorme. El tamaño del delta ahora es su LUGAR entre los
            // de la ultima hora (0 = el mas chico, 1 = el mas grande). En la vela en curso el |delta| crece como la raiz del tiempo.
            double factor = bar == CurrentBar - 1 ? Math.Min(3, Math.Sqrt(Math.Max(1, _proyTicks))) : 1;
            double lugar = Lugar(bar, n, factor); _lugar[bar] = lugar;
            double cuerpo = _c[bar] - _o[bar], rango = _h[bar] - _l[bar];
            int sVela = rango > 0 && Math.Abs(cuerpo) >= FcIndecisionPct / 100.0 * rango ? Math.Sign(cuerpo) : 0, sDelta = Math.Sign(_d[bar]);
            double piso = FcPisoDelta / 100.0, contraDesde = FcContraDesde / 100.0;
            if (sVela == 0) { _tono[bar] = Gris; _bri[bar] = 0.45; }                                                      // doji / martillo
            else if (sDelta == sVela && lugar >= piso) { _tono[bar] = sVela; _bri[bar] = 0.40 + 0.60 * lugar; }           // el flujo acompaña: brillo = cuanto
            else if (sDelta == -sVela && lugar >= contraDesde) { _tono[bar] = Contra; _bri[bar] = 0.45 + 0.55 * lugar; }  // el precio le gano al flujo
            else { _tono[bar] = sVela; _bri[bar] = 0.40; }                                                                // se movio casi sin flujo
            // confluencia: las cuatro lecturas firmadas; la cinta las muestra RESPECTO de la vela
            int g0 = Math.Max(0, bar - 299);
            var bg = new List<double>(bar - g0 + 1); for (int k = g0; k <= bar; k++) bg.Add(Math.Abs(_big[k])); bg.Sort();
            double bmax = bg[(int)Math.Min(bg.Count - 1, bg.Count * 0.95)];
            int ca = lugar >= piso ? sDelta : 0;                                                                          // el delta de la vela
            int cb = bmax > 0 ? (_big[bar] > 0.3 * bmax ? 1 : _big[bar] < -0.3 * bmax ? -1 : 0) : 0;                      // las operaciones grandes
            int hc = Math.Max(20, 4 * w); int cc = bar >= hc ? Math.Sign(_cvdC[bar] - _cvdC[bar - hc]) : 0;               // el CVD de 20 velas (el fondo)
            // donde cerro el delta dentro de su recorrido (-1 en su minimo, +1 en su maximo): capta el martillo (venta temprana, compra al final)
            double rec = _dmax[bar] - _dmin[bar], cierreD = rec > 0 ? (_d[bar] - _dmin[bar]) / rec * 2 - 1 : 0; int ce = cierreD > 0.3 ? 1 : cierreD < -0.3 ? -1 : 0;
            int conf = ca + cb + cc + ce; _conf[bar] = conf;
            int neto = sVela * conf;                                                                                       // lecturas que acompañan a la vela menos las que van en contra
            if (sVela == 0) { _tonoC[bar] = Gris; _briC[bar] = 0.45; }
            else if (neto > 0) { _tonoC[bar] = sVela; _briC[bar] = neto >= 4 ? 1.0 : neto == 3 ? 0.80 : neto == 2 ? 0.58 : 0.40; }
            else if (neto < 0) { _tonoC[bar] = Contra; _briC[bar] = neto <= -4 ? 1.0 : neto == -3 ? 0.82 : neto == -2 ? 0.62 : 0.45; }
            else { _tonoC[bar] = Gris; _briC[bar] = 0.30; }
        }

        /// <summary>Lugar del |delta| de la vela entre los de las ultimas n velas (0 = el mas chico, 1 = el mas grande). Robusto a rafagas.</summary>
        private double Lugar(int bar, int n, double factor)
        {
            int a0 = Math.Max(0, bar - n + 1), m = bar - a0 + 1; if (m < 10) return 0.5;
            double a = Math.Abs(_d[bar]) * factor; int cnt = 1;
            for (int k = a0; k < bar; k++) if (Math.Abs(_d[k]) <= a) cnt++;
            return Math.Min(1.0, (cnt - 0.5) / m);
        }

        /// <summary>La vela k ya cerro: valores finales, divergencia, registro y alertas (solo de las cerradas en vivo).</summary>
        private void Cerrar(int k)
        {
            Derivar(k);
            int n = Math.Max(5, FcDivVelas);
            if (k >= n)
            {
                double hi = double.MinValue, lo = double.MaxValue;
                for (int j = k - n; j < k; j++) { if (_h[j] > hi) hi = _h[j]; if (_l[j] < lo) lo = _l[j]; }
                // divergencia de verdad (revision 17-09): el precio SUPERA al extremo anterior, el CVD no, y ese extremo no es viejo
                const int edadMax = 90;
                if (_h[k] >= hi)
                {
                    int edad = k - _ultMax.Bar;
                    if (_ultMax.Bar >= 0 && edad >= FcDivSeparacion && edad <= edadMax && _h[k] > _ultMax.Precio && _cvdC[k] < _ultMax.Cvd) _div[k] = -1;
                    _ultMax = (k, _cvdC[k], _h[k]);
                }
                if (_l[k] <= lo)
                {
                    int edad = k - _ultMin.Bar;
                    if (_ultMin.Bar >= 0 && edad >= FcDivSeparacion && edad <= edadMax && _l[k] < _ultMin.Precio && _cvdC[k] > _ultMin.Cvd) _div[k] = +1;
                    _ultMin = (k, _cvdC[k], _l[k]);
                }
            }
            _cerradaHasta = k;
            if (k >= _vivoDesde)
            {
                Volcar(k);
                if (_abs[k]) { Marca("absorcion", k, _d[k] > 0 ? -1 : +1); if (FcAlertaAbsorcion) Alertar("ABSORCION: delta " + _d[k].ToString("+0;-0", Es) + " y la vela no avanzo"); }
                bool ext = Math.Abs(_conf[k]) >= FcExtremoDesde, extAntes = k > 0 && Math.Abs(_conf[k - 1]) >= FcExtremoDesde;
                if (ext && !extAntes) { Marca("extremo", k, _conf[k] > 0 ? +1 : -1); if (FcAlertaExtremo) Alertar("EXTREMO " + (_conf[k] > 0 ? "comprador" : "vendedor") + ": no perseguir"); }
                if (_div[k] != 0) { Marca("divergencia", k, _div[k]); if (FcAlertaDivergencia) Alertar("DIVERGENCIA " + (_div[k] < 0 ? "bajista: maximo nuevo sin CVD" : "alcista: minimo nuevo sin CVD")); }
            }
        }

        // ------------------------------------------------------------------ operaciones en vivo
        protected override void OnNewTrade(MarketDataArg trade)
        {
            try
            {
                if (trade == null || trade.Volume <= 0) return;
                double v = (double)trade.Volume, sg = trade.Direction == TradeDirection.Buy ? v : trade.Direction == TradeDirection.Sell ? -v : 0;
                long seg = trade.Time.Ticks / TimeSpan.TicksPerSecond;
                lock (_cubetas)
                {
                    _cubetas.TryGetValue(seg, out var q); _cubetas[seg] = (q.S + sg, q.V + v);
                    if (seg > _ultSeg) { _ultSeg = seg; _relojUltTrade = DateTime.UtcNow; }
                    if (_cubetas.Count > 420) foreach (var viejo in _cubetas.Keys.Where(x => x < seg - 330).ToList()) _cubetas.Remove(viejo);
                }
            }
            catch (Exception e) { Registrar(e); }
        }

        /// <summary>Delta y volumen de los ultimos 'seg' segundos (el reloj sigue corriendo aunque no haya operaciones).</summary>
        private (double S, double V) Ventana(int seg)
        {
            lock (_cubetas)
            {
                if (_ultSeg == 0) return (0, 0);
                long ahora = _ultSeg + (long)Math.Max(0, (DateTime.UtcNow - _relojUltTrade).TotalSeconds);
                double s = 0, v = 0;
                foreach (var kv in _cubetas) if (kv.Key > ahora - seg && kv.Key <= ahora) { s += kv.Value.S; v += kv.Value.V; }
                return (s, v);
            }
        }

        protected override void OnCumulativeTrade(CumulativeTrade trade)
        {
            try
            {
                if (trade == null) return;
                lock (_llave)
                {
                    if (_bigActual != null && !ReferenceEquals(_bigActual, trade)) ContarGrande(_bigActual);
                    _bigActual = trade;
                }
            }
            catch (Exception e) { Registrar(e); }
        }

        protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
        {
            try { if (trade != null) lock (_llave) _bigActual = trade; } catch { }
        }

        private void ContarGrande(CumulativeTrade t)
        {
            if ((double)t.Volume < FcUmbralGrande) return;
            int bar = BarraDe(t.Time); if (bar < 0 || bar >= _big.Count) bar = Math.Min(CurrentBar - 1, _big.Count - 1); if (bar < 0) return;
            _big[bar] += t.Direction == TradeDirection.Buy ? (double)t.Volume : t.Direction == TradeDirection.Sell ? -(double)t.Volume : 0;
        }

        /// <summary>Las operaciones grandes de los dias anteriores salen del historial de ATAS (patron oficial de Market Power).</summary>
        private void PedirGrandesHistoricos()
        {
            try
            {
                if (FcDiasGrandes <= 0) return;
                DateTime fin, ini;
                lock (_llave) { if (_t.Count == 0) return; fin = _t[_t.Count - 1].AddDays(1); ini = _t[_t.Count - 1].AddDays(-FcDiasGrandes); }
                RequestForCumulativeTrades(new CumulativeTradesRequest(ini, fin, Math.Max(1, FcUmbralGrande), 0));
                Log("grandes: pedido el historial de operaciones >= " + FcUmbralGrande + " desde " + ini.ToString("yyyy-MM-dd HH:mm", Inv));
            }
            catch (Exception e) { Registrar(e); }
        }

        protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
        {
            try
            {
                if (cumulativeTrades == null) return;
                int puestos = 0, fuera = 0;
                lock (_llave)
                {
                    if (!_cargado) { _histPedido = false; Log("grandes: la respuesta llego en medio de un recalculo; se vuelve a pedir"); return; }
                    if (request != null && (int)request.MinVolume > Math.Max(1, FcUmbralGrande)) { _histPedido = false; return; }   // el umbral bajo con el pedido en vuelo
                    int tope = Math.Min(_vivoDesde, _big.Count);
                    var suma = new Dictionary<int, double>();
                    foreach (var t in cumulativeTrades)
                    {
                        if (t == null || (double)t.Volume < FcUmbralGrande) continue;
                        int bar = BarraDe(t.Time);
                        if (bar < 0 || bar >= tope) { fuera++; continue; }
                        double sg = t.Direction == TradeDirection.Buy ? (double)t.Volume : t.Direction == TradeDirection.Sell ? -(double)t.Volume : 0;
                        suma[bar] = (suma.TryGetValue(bar, out var q) ? q : 0) + sg; puestos++;
                    }
                    foreach (var kv in suma) _big[kv.Key] = kv.Value;
                    // la confluencia de esas velas usa los grandes: se rehace
                    foreach (var bar in suma.Keys.OrderBy(x => x)) if (bar <= _cerradaHasta) Derivar(bar);
                    _histTrades = puestos; _histFuera = fuera;
                }
                Log("grandes: " + puestos + " operaciones historicas ubicadas en su vela (" + fuera + " fuera del grafico o de la parte en vivo)");
                Volcar();
                try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            }
            catch (Exception e) { Registrar(e); }
        }

        /// <summary>La vela que contiene esa hora (misma base de tiempo que las velas de ATAS). -1 si cae antes de la primera.</summary>
        private int BarraDe(DateTime t)
        {
            int lo = 0, hi = _t.Count - 1, r = -1;
            while (lo <= hi) { int mid = (lo + hi) / 2; if (_t[mid] <= t) { r = mid; lo = mid + 1; } else hi = mid - 1; }
            if (r >= 0 && _tf[r] != DateTime.MinValue && t > _tf[r].AddSeconds(1)) return -1;   // hueco entre velas, o despues de la ultima cargada
            return r;
        }

        // ------------------------------------------------------------------ dibujo
        private static Color De(System.Windows.Media.Color c) => Color.FromArgb(c.A, c.R, c.G, c.B);
        private static Color Alfa(Color c, double a) => Color.FromArgb((int)Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B);

        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            try { Pintar(g); } catch (Exception e) { Registrar(e); }
        }

        private void Pintar(RenderContext g)
        {
            var cont = ChartInfo?.PriceChartContainer; var mio = Container;
            if (cont == null || mio == null) return;
            var reg = mio.Region; if (reg.Width < 80 || reg.Height < 40) return;
            int ult = CurrentBar - 1; if (ult < 1) return;
            int desde = Math.Max(0, FirstVisibleBarNumber), hasta = Math.Min(ult, LastVisibleBarNumber);
            if (hasta <= desde) return;
            Color cCompra = De(FcColorCompra), cVenta = De(FcColorVenta), cAviso = De(FcColorAviso), cTxt = De(FcColorTexto), cFondo = De(FcColorFondo);
            _colContra = De(FcColorContra); _colGris = De(FcColorIndecision);
            var f = new RenderFont("Consolas", FcLetra); var fCh = new RenderFont("Consolas", Math.Max(6, FcLetra - 1));
            int altoTxt = g.MeasureString("0", f).Height;
            int xMax = reg.Right;   // la Region de un panel propio ya viene SIN el eje de precios (revision 17-09)
            try { var cb = g.ClipBounds; if (cb.Width > 0) xMax = Math.Min(xMax, cb.Right); } catch { }
            xMax -= Math.Max(0, FcMargenEje);

            // copia de lo visible bajo llave
            int n = hasta - desde + 1;
            double[] d = new double[n], z = new double[n], vel = new double[n], big = new double[n], cv = new double[n], cs = new double[n], dmx = new double[n], dmn = new double[n], hi = new double[n], lo = new double[n];
            bool[] ab = new bool[n]; int[] cf = new int[n], dv = new int[n], tono = new int[n], tonoC = new int[n], sv = new int[n]; double[] bri = new double[n], briC = new double[n];
            double bmax = 0, cvdAncla = 0, sesion = 0; int anclaBar; int cerrada;
            double zA = 0, bigA = 0, velA = 0; bool absA = false; int cfA = 0, tonoA = 0;
            lock (_llave)
            {
                if (_d.Count <= ult) return;
                for (int i = 0; i < n; i++)
                {
                    int b = desde + i;
                    d[i] = _d[b]; z[i] = _z[b]; vel[i] = _vel[b]; big[i] = _big[b]; cv[i] = _cvdC[b]; cs[i] = _cvdS[b]; dmx[i] = _dmax[b]; dmn[i] = _dmin[b]; hi[i] = _h[b]; lo[i] = _l[b];
                    ab[i] = _abs[b]; cf[i] = _conf[b]; dv[i] = _div[b]; tono[i] = _tono[b]; tonoC[i] = _tonoC[b]; bri[i] = _bri[b]; briC[i] = _briC[b];
                    double cu = _c[b] - _o[b], rg = _h[b] - _l[b]; sv[i] = rg > 0 && Math.Abs(cu) >= FcIndecisionPct / 100.0 * rg ? Math.Sign(cu) : 0;
                }
                var bg = new List<double>(); for (int b = Math.Max(0, ult - 299); b <= ult; b++) if (_big[b] != 0) bg.Add(Math.Abs(_big[b])); bg.Sort();
                bmax = bg.Count >= 5 ? bg[(int)Math.Min(bg.Count - 1, bg.Count * 0.95)] : (bg.Count > 0 ? bg[bg.Count - 1] : 0);
                anclaBar = FcAncla == ModoAnclaCvd.Visible ? desde : Math.Max(0, hasta - Math.Max(10, FcAnclaVelas));
                cvdAncla = _cvdC[Math.Min(anclaBar, _cvdC.Count - 1)]; sesion = _cvdS[ult]; cerrada = _cerradaHasta;
                zA = _z[ult]; bigA = _big[ult]; velA = _vel[ult]; absA = _abs[ult]; cfA = _conf[ult]; tonoA = _tono[ult] == Contra || _tonoC[ult] == Contra ? Contra : _tono[ult];
            }
            // x de cada vela
            int[] x0 = new int[n + 1];
            for (int i = 0; i <= n; i++) { try { x0[i] = cont.GetXByBar(desde + i, true); } catch { x0[i] = i > 0 ? x0[i - 1] + 6 : reg.Left; } }

            // ---- bloque AHORA (reserva su ancho)
            int xIzq = reg.Left + 2;
            Rectangle rAhora = Rectangle.Empty;
            const int altoTitulo = 15;   // ATAS escribe el nombre del indicador arriba a la izquierda del panel
            Rectangle rFila = Rectangle.Empty; bool filaAbajo = false;
            if (FcAhoraComo == FormaDelAhora.FilaArriba || FcAhoraComo == FormaDelAhora.FilaAbajo)
            {
                int xTit = reg.Left + Math.Max(0, FcAnchoTitulo);
                filaAbajo = FcAhoraComo == FormaDelAhora.FilaAbajo || xMax - xTit < 330;   // sin lugar al lado del titulo: va al pie
                rFila = filaAbajo ? new Rectangle(reg.Left + 2, reg.Bottom - altoTxt - 5, xMax - reg.Left - 4, altoTxt + 4)
                                  : new Rectangle(xTit, reg.Top, xMax - xTit, altoTitulo);
            }
            else if (FcAhoraComo == FormaDelAhora.Bloque && FcBloqueAhora != LugarDelAhora.Oculto)
            {
                int w = Math.Min(FcAnchoAhora, reg.Width / 2);
                bool cabeDerecha = !(hasta == ult && x0[n] > xMax - w - 8);   // a la derecha solo si no tapa la vela en curso
                bool aLaDerecha = FcBloqueAhora != LugarDelAhora.PanelIzquierda && cabeDerecha;
                rAhora = aLaDerecha ? new Rectangle(xMax - w, reg.Top + 1, w, reg.Height - 2) : new Rectangle(reg.Left + 2, reg.Top + altoTitulo, w, reg.Height - altoTitulo - 1);
                if (aLaDerecha) xMax = rAhora.Left - 4; else xIzq = rAhora.Right + 4;
            }

            var clipPrevio = g.ClipBounds;
            try { g.SetClip(reg); } catch { }
            try
            {
            // ---- cintas
            var filas = new List<(string Nombre, int Tipo)>();
            if (FcCintaConfluencia) filas.Add(("confluencia", 0));
            if (FcCintaPresion) filas.Add(("presion", 1));
            if (FcCintaGrandes) filas.Add(("grandes", 2));
            if (FcCintaVelocidad) filas.Add(("cinta", 3));
            if (FcCintaAbsorcion) filas.Add(("absorcion", 4));
            int altoFila = FcAltoFila > 0 ? FcAltoFila : Math.Max(7, Math.Min(14, (int)(reg.Height * 0.085)));
            if (filas.Count * altoFila > reg.Height * 0.55) altoFila = Math.Max(5, (int)(reg.Height * 0.55 / Math.Max(1, filas.Count)));
            int yC = reg.Top + 1 + (xIzq <= reg.Left + 4 ? altoTitulo : 0);
            for (int r = 0; r < filas.Count; r++)
            {
                int y = yC + r * altoFila, tipo = filas[r].Tipo;
                for (int i = 0; i < n; i++)
                {
                    int xa = Math.Max(x0[i], xIzq), xb = Math.Min(x0[i + 1], xMax); if (xb <= xa) continue;
                    double v = 0; int tn = 0;
                    switch (tipo)
                    {
                        case 0: tn = tonoC[i]; v = briC[i]; break;
                        case 1: tn = tono[i]; v = bri[i]; break;
                        case 2:
                            if (big[i] != 0 && bmax > 0)
                            {
                                v = Math.Max(0.35, Math.Min(1, Math.Abs(big[i]) / bmax)); tn = big[i] > 0 ? 1 : -1;
                                if (FcGrandesContraVela && sv[i] != 0 && tn == -sv[i]) tn = Contra;   // los grandes operaron contra la vela
                            }
                            break;
                        case 3: v = vel[i] / 3.0; tn = Aviso; break;
                        case 4: v = ab[i] ? 1 : 0; tn = Aviso; break;
                    }
                    if (v <= 0 || tn == 0) continue;
                    Color col = tn == 1 ? cCompra : tn == -1 ? cVenta : tn == Contra ? _colContra : tn == Gris ? _colGris : cAviso;
                    var rect = new Rectangle(xa, y + 1, Math.Max(1, xb - xa), Math.Max(2, altoFila - 2));
                    g.FillRectangle(Alfa(col, 35 + 215 * Math.Min(1, v)), rect);
                    if (desde + i > cerrada) g.DrawRectangle(new RenderPen(Alfa(cTxt, 150), 1f), rect);   // vela en curso: llena y con borde (se mueve con la vela; el color firme es el del cierre)
                }
                if (FcRotulos && altoFila >= 7)
                {
                    var fRot = new RenderFont("Consolas", Math.Max(6f, Math.Min(FcLetra - 1, altoFila * 0.62f)));
                    var m = g.MeasureString(filas[r].Nombre, fRot);
                    bool junto = hasta == ult && x0[n] + 10 + m.Width < xMax;   // despues de la vela en curso, en el margen libre
                    int xr = junto ? x0[n] + 6 : xIzq;
                    if (!junto) g.FillRectangle(Alfa(cFondo, 190), new Rectangle(xr, y, m.Width + 4, altoFila));
                    g.DrawString(filas[r].Nombre, fRot, tipo >= 3 ? cAviso : cTxt, xr + 2, y + (altoFila - m.Height) / 2);
                }
            }

            // ---- zona del CVD y la presion
            int yTop = yC + filas.Count * altoFila + 3, yBot = (filaAbajo ? rFila.Top : reg.Bottom) - 2;
            if (yBot - yTop >= 24)
            {
                int yMid = (yTop + yBot) / 2, medio = (yBot - yTop) / 2;
                if (FcVerPresion)
                {
                    foreach (var lvl in new[] { 1.0, 2.0 })
                    {
                        int dy = (int)(lvl / 3.2 * medio);
                        var pen = new RenderPen(Alfa(cTxt, lvl > 1.5 ? 90 : 55), 1f, System.Drawing.Drawing2D.DashStyle.Dot);
                        g.DrawLine(pen, xIzq, yMid - dy, xMax, yMid - dy); g.DrawLine(pen, xIzq, yMid + dy, xMax, yMid + dy);
                        if (lvl > 1.5 || medio / 3.2 >= altoTxt)
                        {
                            g.DrawString("+" + lvl.ToString("0", Inv) + "σ", fCh, Alfa(cTxt, 150), xMax - 22, yMid - dy - altoTxt + 2);
                            g.DrawString("-" + lvl.ToString("0", Inv) + "σ", fCh, Alfa(cTxt, 150), xMax - 22, yMid + dy - 1);
                        }
                    }
                    for (int i = 0; i < n; i++)
                    {
                        int xa = Math.Max(x0[i] + 1, xIzq), xb = Math.Min(x0[i + 1] - 1, xMax); if (xb <= xa || z[i] == 0) continue;
                        int hBar = (int)(Math.Abs(z[i]) / 3.2 * medio); var col = z[i] >= 0 ? cCompra : cVenta;
                        bool ultima = desde + i == ult;
                        var rect = new Rectangle(xa, z[i] >= 0 ? yMid - hBar : yMid, Math.Max(1, xb - xa), Math.Max(1, hBar));
                        g.FillRectangle(Alfa(col, ultima ? 235 : 115), rect);
                        if (ultima) g.DrawRectangle(new RenderPen(Color.White, 1f), rect);
                    }
                }
                g.DrawLine(new RenderPen(Alfa(cTxt, 120), 1f), xIzq, yMid, xMax, yMid);

                // CVD anclado: la forma es la del CVD de siempre, el cero es el de hace N velas (o el borde visible, o la sesion)
                double[] v = new double[n];
                for (int i = 0; i < n; i++) v[i] = FcAncla == ModoAnclaCvd.Sesion ? cs[i] : cv[i] - cvdAncla;
                int iEsc = Math.Max(0, Math.Min(n - 1, (FcAncla == ModoAnclaCvd.UltimasVelas ? anclaBar : desde) - desde));
                double vMin = double.MaxValue, vMax = double.MinValue;
                for (int i = iEsc; i < n; i++)
                {
                    double a = v[i], b = v[i];
                    if (FcDibujo == DibujoDelCvd.VelasConMecha) { double ant = v[i] - d[i]; a = ant + dmn[i]; b = ant + dmx[i]; }
                    if (a < vMin) vMin = a; if (b > vMax) vMax = b; if (v[i] < vMin) vMin = v[i]; if (v[i] > vMax) vMax = v[i];
                }
                if (vMax - vMin < 1) { vMax += 1; vMin -= 1; }
                double margen = (vMax - vMin) * 0.08; vMax += margen; vMin -= margen;
                int Y(double val) => (int)Math.Max(yTop, Math.Min(yBot, yBot - (val - vMin) / (vMax - vMin) * (yBot - yTop)));
                if (vMin < 0 && vMax > 0 && FcAncla != ModoAnclaCvd.Sesion)
                    g.DrawLine(new RenderPen(Alfa(cTxt, 70), 1f, System.Drawing.Drawing2D.DashStyle.Dash), xIzq, Y(0), xMax, Y(0));
                var plumas = new[] { new RenderPen(Alfa(cCompra, 255), Math.Max(1, FcGrosor)), new RenderPen(Alfa(cVenta, 255), Math.Max(1, FcGrosor)), new RenderPen(Alfa(cCompra, 90), Math.Max(1, FcGrosor)), new RenderPen(Alfa(cVenta, 90), Math.Max(1, FcGrosor)) };
                for (int i = 1; i < n; i++)
                {
                    int xa = (x0[i - 1] + x0[i]) / 2, xb = (x0[i] + x0[i + 1]) / 2; if (xb < xIzq || xa > xMax) continue;
                    if ((v[i - 1] > vMax && v[i] > vMax) || (v[i - 1] < vMin && v[i] < vMin)) continue;   // entero fuera de escala: sin rayas falsas en el borde
                    bool tenue = i < iEsc; var col = v[i] >= v[i - 1] ? cCompra : cVenta;
                    if (tenue && (v[i] > vMax || v[i] < vMin || v[i - 1] > vMax || v[i - 1] < vMin)) continue;   // fuera de escala: no se aplasta contra el borde
                    if (FcDibujo == DibujoDelCvd.Linea)
                        g.DrawLine(plumas[(v[i] >= v[i - 1] ? 0 : 1) + (tenue ? 2 : 0)], xa, Y(v[i - 1]), Math.Min(xb, xMax), Y(v[i]));
                    else
                    {
                        double ant = v[i] - d[i]; int xc = Math.Min(xb, xMax), wv = Math.Max(1, (x0[i + 1] - x0[i]) * 6 / 10);
                        g.DrawLine(new RenderPen(Alfa(col, tenue ? 90 : 230), 1f), xc, Y(ant + dmx[i]), xc, Y(ant + dmn[i]));
                        int ya = Y(Math.Max(ant, v[i])), yb = Y(Math.Min(ant, v[i]));
                        var rect = new Rectangle(xc - wv / 2, ya, wv, Math.Max(1, yb - ya));
                        if (desde + i == ult) g.DrawRectangle(new RenderPen(Alfa(col, 255), 1f), rect); else g.FillRectangle(Alfa(col, tenue ? 90 : 230), rect);
                    }
                }
                // el numero que cambia, en su propia linea
                string modo = FcAncla == ModoAnclaCvd.Sesion ? "sesion" : FcAncla == ModoAnclaCvd.Visible ? "desde el borde visible" : "ultimas " + Math.Max(10, FcAnclaVelas) + " velas";
                string txt = "CVD sesion " + sesion.ToString("+#,0;-#,0", Es) + "  ·  " + modo + " " + v[n - 1].ToString("+#,0;-#,0", Es);
                var mt = g.MeasureString(txt, fCh);
                g.FillRectangle(Alfa(cFondo, 170), new Rectangle(xIzq, yTop, mt.Width + 6, mt.Height));
                g.DrawString(txt, fCh, cTxt, xIzq + 3, yTop);
                g.DrawString(vMax.ToString("#,0", Es), fCh, Alfa(cTxt, 150), xIzq + 3, yTop + mt.Height);
                g.DrawString(vMin.ToString("#,0", Es), fCh, Alfa(cTxt, 150), xIzq + 3, yBot - altoTxt);
            }

            }
            finally { try { g.SetClip(clipPrevio); } catch { } }

            // ---- marcas sobre las velas del precio
            if (FcMarcaAbsorcion || FcMarcaDivergencia)
            {
                int tm = Math.Max(2, FcTamMarca); var rp = cont.Region;
                for (int i = 0; i < n; i++)
                {
                    if (!ab[i] && dv[i] == 0) continue;
                    int xc; try { xc = cont.GetXByBar(desde + i, false); } catch { continue; }   // false = centro de la vela
                    if (xc < rp.Left || xc > Math.Min(xMax, rp.Right)) continue;
                    int yH, yL; try { yH = cont.GetYByPrice((decimal)hi[i], false); yL = cont.GetYByPrice((decimal)lo[i], false); } catch { continue; }
                    if (FcMarcaAbsorcion && ab[i])
                    {
                        int yc = d[i] > 0 ? yH - tm - 4 : yL + tm + 4; bool llena = desde + i <= cerrada;
                        if (yc - tm - 1 >= rp.Top && yc + tm + 1 <= rp.Bottom) MarcaForma(g, FcFormaAbsorcion, cAviso, xc, yc, tm, llena);
                    }
                    if (FcMarcaDivergencia && dv[i] != 0)
                    {
                        int t2 = tm + 2;
                        if (dv[i] < 0) { int yc = yH - 3 * tm - 8; if (yc - t2 >= rp.Top && yc + t2 <= rp.Bottom) g.FillPolygon(cVenta, new[] { new Point(xc - t2, yc - t2), new Point(xc + t2, yc - t2), new Point(xc, yc + t2) }); }
                        else { int yc = yL + 3 * tm + 8; if (yc - t2 >= rp.Top && yc + t2 <= rp.Bottom) g.FillPolygon(cCompra, new[] { new Point(xc - t2, yc + t2), new Point(xc + t2, yc + t2), new Point(xc, yc - t2) }); }
                    }
                }
            }

            // ---- bloque AHORA
            if (rFila.Width > 100) { try { g.SetClip(reg); } catch { } try { PintarFilaAhora(g, rFila, fCh, cCompra, cVenta, cAviso, cTxt, cFondo, zA, bigA, velA, absA, cfA, hasta == ult, filaAbajo, tonoA); } finally { try { g.SetClip(clipPrevio); } catch { } } }
            if (FcAhoraComo == FormaDelAhora.Bloque && FcBloqueAhora != LugarDelAhora.Oculto && rAhora.Width > 60) { try { g.SetClip(reg); } catch { } try { PintarAhora(g, rAhora, f, fCh, cCompra, cVenta, cAviso, cTxt, cFondo, zA, bigA, velA, absA, cfA, hasta == ult, tonoA); } finally { try { g.SetClip(clipPrevio); } catch { } } }
        }

        private static void MarcaForma(RenderContext g, FormaDeMarca forma, Color col, int xc, int yc, int r, bool llena)
        {
            switch (forma)
            {
                case FormaDeMarca.Punto:
                    if (llena) g.FillEllipse(col, new Rectangle(xc - r, yc - r, 2 * r, 2 * r)); else g.DrawEllipse(new RenderPen(col, 1.5f), new Rectangle(xc - r, yc - r, 2 * r, 2 * r));
                    break;
                case FormaDeMarca.Cuadrado:
                    if (llena) g.FillRectangle(col, new Rectangle(xc - r, yc - r, 2 * r, 2 * r)); else g.DrawRectangle(new RenderPen(col, 1.5f), new Rectangle(xc - r, yc - r, 2 * r, 2 * r));
                    break;
                default:
                    var p = new[] { new Point(xc, yc - r - 1), new Point(xc + r + 1, yc), new Point(xc, yc + r + 1), new Point(xc - r - 1, yc) };
                    if (llena) g.FillPolygon(col, p);
                    else { var pen = new RenderPen(col, 1.5f); for (int i = 0; i < 4; i++) g.DrawLine(pen, p[i].X, p[i].Y, p[(i + 1) % 4].X, p[(i + 1) % 4].Y); }
                    break;
            }
        }

        /// <summary>El AHORA en una sola fila: cuatro celdas (5 s, 15 s, 60 s, 5 min) y, si entran, FLUJO n/4, el aviso de
        /// extremo, PRESION, GRANDES, CINTA rapida y ABSORCION. Lo que no entra se saca de derecha a izquierda por prioridad.</summary>
        private void PintarFilaAhora(RenderContext g, Rectangle r, RenderFont fCh, Color cCompra, Color cVenta, Color cAviso, Color cTxt, Color cFondo,
                                     double z, double big, double vel, bool abs, int conf, bool alDia, bool conFondo, int tonoVela)
        {
            if (conFondo) g.FillRectangle(Alfa(cFondo, 215), r);
            int alto = g.MeasureString("0", fCh).Height, y = r.Top + Math.Max(0, (r.Height - alto) / 2), x = r.Left + 2;
            var vents = new[] { (5, "5s"), (15, "15s"), (60, "60s"), (300, "5m") };
            int wCel = g.MeasureString("60s +100%", fCh).Width + 8;
            foreach (var (seg, nombre) in vents)
            {
                if (x + wCel > r.Right) return;
                var (sv, vv) = Ventana(seg); double pct = vv > 0 ? sv / vv : 0, fuerza = Math.Min(1, Math.Abs(pct) / 0.30);
                var rc = new Rectangle(x, r.Top + 1, wCel - 3, r.Height - 2);
                g.FillRectangle(vv <= 0 ? Alfa(cTxt, 30) : Alfa(pct >= 0 ? cCompra : cVenta, 45 + 195 * fuerza), rc);
                string t = nombre + " " + (vv <= 0 ? "—" : (pct * 100).ToString("+0;-0", Es) + "%");
                g.DrawString(t, fCh, Color.White, rc.Left + 3, y);
                x += wCel;
            }
            x += 6;
            // el resto, por prioridad: lo que no entra no se dibuja
            var items = new List<(string Texto, Color Col, bool Caja)>();
            string lado = conf > 0 ? "COMPR." : conf < 0 ? "VEND." : "PAREJO";
            items.Add(("FLUJO " + lado + " " + Math.Abs(conf) + "/4", conf > 0 ? cCompra : conf < 0 ? cVenta : cTxt, false));
            if (Math.Abs(conf) >= FcExtremoDesde) items.Add(("NO PERSEGUIR", cAviso, true));
            if (tonoVela == Contra) items.Add(("VELA CONTRA EL FLUJO", _colContra, true));
            else if (tonoVela == Gris) items.Add(("INDECISION", _colGris, false));
            if (abs) items.Add(("ABSORCION", cAviso, true));
            items.Add(("DELTA " + Math.Max(1, FcPresionVelas) + "v " + Math.Round(z, 1).ToString("+0.0;-0.0;0.0", Es) + "σ", z > 0.5 ? cCompra : z < -0.5 ? cVenta : cTxt, false));
            items.Add(("GRANDES " + big.ToString("+#,0;-#,0;0", Es), big > 0 ? cCompra : big < 0 ? cVenta : cTxt, false));
            if (vel > 1) items.Add(("CINTA rapida", cAviso, false));
            if (!alDia) items.Add(("(grafico corrido)", Alfa(cTxt, 160), false));
            foreach (var it in items)
            {
                var m = g.MeasureString(it.Texto, fCh); int w = m.Width + (it.Caja ? 10 : 0);
                if (x + w > r.Right - 2) break;
                if (it.Caja)
                {
                    var rc = new Rectangle(x, r.Top + 1, w, r.Height - 2);
                    g.FillRectangle(Alfa(it.Col, 45), rc); g.DrawRectangle(new RenderPen(it.Col, 1f), rc); g.DrawString(it.Texto, fCh, it.Col, x + 5, y);
                }
                else g.DrawString(it.Texto, fCh, it.Col, x, y);
                x += w + 12;
            }
        }

        private void PintarAhora(RenderContext g, Rectangle r, RenderFont f, RenderFont fCh, Color cCompra, Color cVenta, Color cAviso, Color cTxt, Color cFondo,
                                 double z, double big, double vel, bool abs, int conf, bool alDia, int tonoVela)
        {
            g.FillRectangle(Alfa(cFondo, 225), r);
            g.DrawRectangle(new RenderPen(Alfa(cTxt, 60), 1f), r);
            int alto = g.MeasureString("0", f).Height, pad = 4;
            // columna izquierda: las cuatro ventanas de tiempo, calculadas con cada operacion
            int wCel = Math.Max(58, r.Width * 36 / 100), hCel = Math.Max(alto + 2, (r.Height - 2 * pad - 3 * 2) / 4);
            var vents = new[] { (5, "5 s"), (15, "15 s"), (60, "60 s"), (300, "5 min") };
            for (int i = 0; i < 4; i++)
            {
                var (s, v) = Ventana(vents[i].Item1);
                double pct = v > 0 ? s / v : 0; double fuerza = Math.Min(1, Math.Abs(pct) / 0.30);
                var rc = new Rectangle(r.Left + pad, r.Top + pad + i * (hCel + 2), wCel, hCel);
                g.FillRectangle(v <= 0 ? Alfa(cTxt, 30) : Alfa(pct >= 0 ? cCompra : cVenta, 40 + 200 * fuerza), rc);
                g.DrawString(vents[i].Item2, fCh, Color.White, rc.Left + 3, rc.Top + (hCel - alto) / 2 + 1);
                string t = v <= 0 ? "—" : (pct * 100).ToString("+0;-0", Es) + "%";
                var m = g.MeasureString(t, fCh); g.DrawString(t, fCh, Color.White, rc.Right - m.Width - 3, rc.Top + (hCel - alto) / 2 + 1);
            }
            // columna derecha: las lecturas de la vela en curso y el resumen
            int altoCh = g.MeasureString("0", fCh).Height;
            int x = r.Left + pad + wCel + 8, y = r.Top + pad, xR = r.Right - pad, paso = Math.Max(alto - 3, (r.Height - 2 * pad - altoCh - 4) / 5);
            void Linea(string nombre, string valor, Color col)
            {
                g.DrawString(nombre, fCh, cTxt, x, y); var m = g.MeasureString(valor, f); g.DrawString(valor, f, col, xR - m.Width, y - 1); y += paso;
            }
            Linea("DELTA " + Math.Max(1, FcPresionVelas) + "v", Math.Round(z, 1).ToString("+0.0;-0.0;0.0", Es) + "σ", z > 0.5 ? cCompra : z < -0.5 ? cVenta : cTxt);
            Linea("GRANDES", big.ToString("+#,0;-#,0;0", Es), big > 0 ? cCompra : big < 0 ? cVenta : cTxt);
            Linea("CINTA", vel > 1 ? "rapida" : "normal", vel > 1 ? cAviso : cTxt);
            Linea("ABSORCION", abs ? "SI" : "no", abs ? cAviso : cTxt);
            if (tonoVela == Contra) Linea("VELA", "contra el flujo", _colContra); else if (tonoVela == Gris) Linea("VELA", "indecision", _colGris);
            string lado = conf > 0 ? "COMPR. " : conf < 0 ? "VEND. " : "PAREJO ";
            Linea("FLUJO", lado + Math.Abs(conf) + "/4", conf > 0 ? cCompra : conf < 0 ? cVenta : cTxt);
            if (Math.Abs(conf) >= FcExtremoDesde)
            {
                string av = "EXTREMO: no perseguir"; var m = g.MeasureString(av, fCh);
                if (m.Width + 8 > xR - x + 2) { av = "NO PERSEGUIR"; m = g.MeasureString(av, fCh); }
                var rc = new Rectangle(x - 2, Math.Min(y, r.Bottom - m.Height - 3), Math.Min(xR - x + 2, m.Width + 8), m.Height + 1);
                g.FillRectangle(Alfa(cAviso, 45), rc); g.DrawRectangle(new RenderPen(cAviso, 1f), rc); g.DrawString(av, fCh, cAviso, rc.Left + 4, rc.Top);
            }
            if (!alDia) g.DrawString("(grafico corrido)", fCh, Alfa(cTxt, 150), r.Left + pad, r.Bottom - altoCh - 1);
        }

        // ------------------------------------------------------------------ registro y alertas
        private void Alertar(string texto)
        {
            try { AddAlert(string.IsNullOrWhiteSpace(FcSonido) ? "alert1" : FcSonido, "Flujo Claro " + (InstrumentInfo?.Instrument ?? "") + ": " + texto); } catch (Exception e) { Registrar(e); }
        }

        /// <summary>Todas las velas cerradas del grafico (crudas y estados) a un CSV: la materia prima del banco de pruebas.</summary>
        private void Volcar(int soloVela = -1)
        {
            if (!FcVolcarVelas) return;
            try
            {
                var sb = new System.Text.StringBuilder(soloVela >= 0 ? 256 : 1 << 20);
                bool entero = soloVela < 0 || !_volcadoEntero;
                lock (_llave)
                {
                    int hasta = Math.Min(_cerradaHasta, _d.Count - 1), desde = entero ? 0 : soloVela;
                    if (entero) sb.Append("t,tf,o,h,l,c,vol,ticks,delta,dmax,dmin,big,tono,brillo,tonoc,brilloc,lugar,conf,abs,div,vel\n");
                    for (int k = desde; k <= hasta; k++)
                        sb.Append(_t[k].ToString("yyyy-MM-ddTHH:mm:ss", Inv)).Append(',').Append(_tf[k].ToString("yyyy-MM-ddTHH:mm:ss", Inv)).Append(',')
                          .Append(_o[k].ToString("0.####", Inv)).Append(',').Append(_h[k].ToString("0.####", Inv)).Append(',').Append(_l[k].ToString("0.####", Inv)).Append(',').Append(_c[k].ToString("0.####", Inv)).Append(',')
                          .Append(_vol[k].ToString("0", Inv)).Append(',').Append(_tk[k].ToString("0", Inv)).Append(',').Append(_d[k].ToString("0", Inv)).Append(',').Append(_dmax[k].ToString("0", Inv)).Append(',').Append(_dmin[k].ToString("0", Inv)).Append(',')
                          .Append(_big[k].ToString("0", Inv)).Append(',').Append(_tono[k]).Append(',').Append(_bri[k].ToString("0.000", Inv)).Append(',').Append(_tonoC[k]).Append(',').Append(_briC[k].ToString("0.000", Inv)).Append(',').Append(_lugar[k].ToString("0.0000", Inv)).Append(',').Append(_conf[k]).Append(',').Append(_abs[k] ? 1 : 0).Append(',').Append(_div[k]).Append(',').Append(_vel[k].ToString("0.00", Inv)).Append('\n');
                    _volcadoEntero = true;
                }
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex", "flujo");
                Directory.CreateDirectory(dir);
                string inst = (InstrumentInfo?.Instrument ?? "x").Replace('#', ' ').Trim();
                string marco = ((ChartInfo?.ChartType.ToString() ?? "") + "-" + (ChartInfo?.TimeFrame ?? "")).Replace(' ', '_');
                var p = Path.Combine(dir, "velas-" + inst + "-" + marco + ".csv"); var txt = sb.ToString();
                // el archivo entero una sola vez por carga; despues, una linea por vela cerrada (antes reescribia 3 MB por minuto y por grafico)
                System.Threading.Tasks.Task.Run(() => { try { lock (_llaveArchivo) { if (entero) File.WriteAllText(p, txt); else File.AppendAllText(p, txt); } } catch { } });
            }
            catch (Exception e) { Registrar(e); }
        }

        /// <summary>Cada marca EN VIVO a un jsonl: hora, precio, tipo, sentido de la lectura y los numeros de la vela. Se mide despues contra placebo.</summary>
        private void Marca(string tipo, int bar, int sentido)
        {
            if (!FcGuardarMarcas) return;
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex", "flujo");
                Directory.CreateDirectory(dir);
                string inst = (InstrumentInfo?.Instrument ?? "x").Replace('#', ' ').Trim();
                var p = Path.Combine(dir, "marcas-" + inst + "-" + DateTime.UtcNow.ToString("yyyy-MM-dd", Inv) + ".jsonl");
                string l = "{\"utc\":\"" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", Inv) + "\",\"tipo\":\"" + tipo + "\",\"sentido\":" + sentido
                    + ",\"vela\":\"" + _t[bar].ToString("yyyy-MM-ddTHH:mm:ss", Inv) + "\",\"marco\":\"" + (ChartInfo?.ChartType.ToString() ?? "") + " " + (ChartInfo?.TimeFrame ?? "") + "\""
                    + ",\"c\":" + _c[bar].ToString("0.##", Inv) + ",\"delta\":" + _d[bar].ToString("0", Inv) + ",\"vol\":" + _vol[bar].ToString("0", Inv)
                    + ",\"z\":" + _z[bar].ToString("0.00", Inv) + ",\"grandes\":" + _big[bar].ToString("0", Inv) + ",\"cinta\":" + _vel[bar].ToString("0.00", Inv) + ",\"conf\":" + _conf[bar] + "}";
                lock (_llaveArchivo) File.AppendAllText(p, l + "\n");
            }
            catch (Exception e) { Registrar(e); }
        }

        private static readonly object _llaveArchivo = new object();
        [Display(Name = "Volcar las velas a un CSV (banco de pruebas)", GroupName = "7. Alertas y registro", Order = 9)]
        public bool FcVolcarVelas { get; set; } = true;
        private static readonly string RutaLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "pythiagex-flujoclaro.log");
        private static void Log(string m) { try { File.AppendAllText(RutaLog, DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", Inv) + "  " + m + "\n"); } catch { } }
        private void Registrar(Exception e)
        {
            if ((DateTime.UtcNow - _ultimoError).TotalSeconds < 10) return;
            _ultimoError = DateTime.UtcNow; Log("EXCEPCION " + e.GetType().Name + ": " + e.Message + " | " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim());
        }
    }
}
