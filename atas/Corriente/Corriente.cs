using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace Corriente
{
    /// <summary>
    /// CORRIENTE 1.0 (29-10-2026). Pedido del operador: "me cuesta identificar cuando esta en tendencia... me bloqueo, pienso que
    /// va a cambiar y no; algo tipo tuneles que marquen la tendencia; sin carteles: minimalista, con datos clave, una flecha o un
    /// dibujo". Eligio dos maquetas sobre sus velas del 13-08: A (tunel) + B (flecha y cinta), en un indicador aparte.
    ///
    /// QUE DIBUJA (sin texto):
    ///   * TUNEL: la franja entre el cierre de cada vela y su SuperTrend (ATR 10 x 3, Wilder), verde con la corriente para arriba
    ///     (la linea queda abajo del precio) y roja para abajo; la linea del SuperTrend encima, del mismo color.
    ///   * FLECHA: a la derecha de la vela en curso, a la altura de su cierre, apunta para donde va la corriente. LLENA cuando el
    ///     precio esta del mismo lado del VWAP de la sesion (las dos lecturas coinciden); HUECA cuando no. Si hay una posicion
    ///     abierta EN CONTRA de la corriente, se agranda y titila (el momento medido de su error: promediar en contra).
    ///   * CINTA: una franja fina abajo con un color por vela (la historia de la corriente), fuerte si coincide con el VWAP.
    ///
    /// LO MEDIDO (laboratorio/tendencia/INFORME.md, sus datos): el SuperTrend NO anticipa la direccion (46-50 % a 15-30 min, igual
    /// que el azar) y llega tarde (MES 20+ pts: se da vuelta a los 6,5 min con 13 pts hechos). Es el que menos serrucha en 1 min
    /// (tramos de 28-30 min). En sus 593 agregados en contra marcaba la corriente en contra el 70,2 % (azar 54,1 %). DESCRIBE, no
    /// anticipa: es un espejo de lo que ya esta pasando, no una señal de entrada.
    ///
    /// AUDITORIA: cada 60 s, %APPDATA%\ATAS\PythiaGex\corriente\auditoria-AAAA-MM-DD.log con las ultimas velas (o h l c, ATR,
    /// SuperTrend, direccion, VWAP) para recalcularlas por fuera, y donde quedo la flecha. Ajustes con prefijo "Cor".
    ///
    /// 1.1 (29-10-2026) VUELTA RAPIDA. Pedido: "avisa demasiado tarde y te perdes las tendencias, mas en dias volatiles; sin sumar
    /// ruido; llevalo a los limites". Medido en laboratorio/tendencia (PREREGISTRO_CALIBRACION y _2, CALIBRACION_INFORME.md):
    ///   * Ronda 1 (80 versiones de un solo dial: ATR 5-60, multiplicador, cierre, eficiencia, Chandelier): todas caen en UNA curva
    ///     retraso/ruido; avisar antes = mas cambios en falso en proporcion fija. El ATR(10) se infla con las velas del propio arranque.
    ///   * Ronda 2 (114 de tres mecanismos): solo la VUELTA RAPIDA rompe la curva fuera de muestra (44 de 54 versiones en el examen de
    ///     MES, 54 de 54 en MNQ). La elegida por las reglas (linea ATR 60 x 4 + vuelta si el precio retrocede 2,5 ATR desde el extremo
    ///     del tramo en 3 velas o menos) tiene el mismo ruido que la 10 x 3 en la sesion entera y llega antes: MES diseño 57 contra 33
    ///     movimientos (p 0,007), MNQ 115 contra 81 (p 0,009), MES examen 24 contra 16 (p 0,13: NO alcanzo la prueba fijada). En dias
    ///     volatiles la mediana de puntos tarde baja de 15,4 a 11,5-11,9. En la rueda suma ~1 cambio por dia (10 -> 11). SIN VALIDAR del
    ///     todo: el registro anota cada vuelta y su causa (linea o rapida) para seguir midiendo en vivo.
    /// Ajustes NUEVOS (CorLinea*, CorRapida*): los viejos CorAtr / CorMult quedaron guardados en el .ws y lo pisarian (ATAS persiste por nombre).
    ///
    /// 1.2 (29-10-2026) RETROCESO EN PUNTOS. "Sigue avisando muy tarde; calibrala hasta los limites reales de prediccion estadistica".
    /// La 1.1 llego 22,25 pts tarde en la suba gradual de las 11:53-12:10 AR (la vuelta rapida no la ve; la linea 60x4 queda lejos).
    /// Medido (PREREGISTRO_PREDICCION.md, pred_01/02): si la corriente va para un lado y el precio retrocedio r pts desde el extremo, la
    /// probabilidad de que siga A pts mas antes de volver al extremo es la del azar puro r/(r+A) en MES y MNQ (126.000 casos; auditado
    /// por separado); 20 datos en vivo (delta, volumen, VWAP, velocidad, hora...) con regresion y con arboles NO le ganan a esa formula fuera de
    /// muestra. Sin prediccion, la decision optima usa solo r: darse vuelta cuando el retroceso desde el extremo del tramo llega a X pts.
    /// X = 10 elegido en DISEÑO (mismo ruido en la rueda que la 10x3) y examinado en los 17 dias nuevos: antes en 24 movimientos, despues
    /// en 10 (p 0,012); peor 10 % de atraso 22,0 -> 16,4 pts; cambios en la rueda 10,1 -> 10,5 y falsos 3,6 -> 3,3. Le gana a la 1.1 en
    /// todo. En MNQ el mismo procedimiento NO paso (eligio 70 pts): no usar este X en MNQ (equivalente medido ~55-60, sin validar).
    /// La LINEA del tunel es el punto exacto donde se da vuelta: X pts debajo del maximo del tramo (o encima del minimo).
    /// </summary>
    [DisplayName("PythiaGex - Corriente")]
    [Category("PythiaGex")]
    public class CorrienteIndicador : Indicator
    {
        public const string Version = "1.2";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public enum ModoCorriente
        {
            [Display(Name = "Retroceso en puntos (1.2)")] Retroceso,
            [Display(Name = "Linea ATR + vuelta rapida (1.1)")] LineaAtr
        }

        // ------------------------------------------------------------------ ajustes
        [Display(Name = "Modo", GroupName = "Corriente", Order = 5)]
        public ModoCorriente CorModo { get => _modo; set { _modo = value; RecalculateValues(); } }
        private ModoCorriente _modo = ModoCorriente.Retroceso;

        [Display(Name = "Retroceso para darse vuelta (pts)", GroupName = "Corriente", Order = 6)]
        [Range(0.25, 1000)]
        public double CorRetroceso { get => _retro; set { _retro = Math.Max(0.25, value); RecalculateValues(); } }
        private double _retro = 10.0;

        [Display(Name = "Linea: ATR (velas)", GroupName = "Corriente", Order = 10)]
        [Range(2, 500)]
        public int CorLineaAtr { get => _atrN; set { _atrN = Math.Max(2, value); RecalculateValues(); } }
        private int _atrN = 60;

        [Display(Name = "Linea: multiplicador del ATR", GroupName = "Corriente", Order = 20)]
        [Range(0.5, 20)]
        public double CorLineaMult { get => _mult; set { _mult = Math.Max(0.1, value); RecalculateValues(); } }
        private double _mult = 4.0;

        [Display(Name = "Vuelta rapida", GroupName = "Corriente", Order = 22)]
        public bool CorRapida { get => _rapida; set { _rapida = value; RecalculateValues(); } }
        private bool _rapida = true;

        [Display(Name = "Vuelta rapida: retroceso (ATR)", GroupName = "Corriente", Order = 24)]
        [Range(0.5, 20)]
        public double CorRapidaAtr { get => _rapV; set { _rapV = Math.Max(0.1, value); RecalculateValues(); } }
        private double _rapV = 2.5;

        [Display(Name = "Vuelta rapida: en cuantas velas", GroupName = "Corriente", Order = 26)]
        [Range(1, 60)]
        public int CorRapidaVelas { get => _rapJ; set { _rapJ = Math.Max(1, value); RecalculateValues(); } }
        private int _rapJ = 3;

        [Display(Name = "Tunel", GroupName = "Vista", Order = 30)]
        public bool CorTunel { get; set; } = true;

        [Display(Name = "Flecha", GroupName = "Vista", Order = 40)]
        public bool CorFlecha { get; set; } = true;

        [Display(Name = "Cinta", GroupName = "Vista", Order = 50)]
        public bool CorCinta { get; set; } = true;

        [Display(Name = "Opacidad del tunel (%)", GroupName = "Vista", Order = 60)]
        [Range(3, 60)]
        public int CorOpacidad { get; set; } = 16;

        [Display(Name = "Cinta: alto (px)", GroupName = "Vista", Order = 70)]
        [Range(2, 20)]
        public int CorCintaAlto { get; set; } = 5;

        // nombre nuevo en la 1.0b (el .ws guardo CorCintaAbajo = 2): 18 px deja la cinta arriba de la barra de Lots de su grafico
        [Display(Name = "Cinta: distancia al borde de abajo (px)", GroupName = "Vista", Order = 80)]
        [Range(0, 200)]
        public int CorCintaSobreBorde { get; set; } = 18;

        [Display(Name = "Color: corriente para arriba", GroupName = "Colores", Order = 90)]
        public System.Windows.Media.Color CorColorSube { get; set; } = System.Windows.Media.Color.FromRgb(38, 166, 154);

        [Display(Name = "Color: corriente para abajo", GroupName = "Colores", Order = 100)]
        public System.Windows.Media.Color CorColorBaja { get; set; } = System.Windows.Media.Color.FromRgb(239, 83, 80);

        [Display(Name = "Registro de auditoria", GroupName = "Auditoria", Order = 110)]
        public bool CorRegistrar { get; set; } = true;

        // ------------------------------------------------------------------ datos por vela (bajo llave)
        private readonly object _llave = new object();
        private readonly List<double> _o = new List<double>(), _h = new List<double>(), _l = new List<double>(), _c = new List<double>();
        private readonly List<double> _atr = new List<double>(), _fu = new List<double>(), _fd = new List<double>(), _st = new List<double>();
        private readonly List<int> _dir = new List<int>();
        // 1.1: extremo del tramo actual (maximo si la corriente va para arriba, minimo si va para abajo), la vela donde se hizo y
        // si la vela dio vuelta la corriente por la via rapida
        private readonly List<double> _ext = new List<double>();
        private readonly List<int> _iext = new List<int>();
        private readonly List<bool> _rap = new List<bool>();
        private readonly List<double> _pv = new List<double>(), _vv = new List<double>(), _vwap = new List<double>();
        private readonly List<DateTime> _hora = new List<DateTime>();
        private DateTime _ultAudit = DateTime.MinValue;
        private bool _volcar = true;   // al primer registro despues de calcular: todas las velas, para la paridad con Python
        private bool _enContra;
        private readonly TimeSpan _periodo = TimeSpan.FromMilliseconds(500);
        private Action _latido;

        public CorrienteIndicador() : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
            // 1.0b: ENCIMA. Debajo, la flecha quedaba tapada por la burbuja de Adaptive Big Trades de la ultima vela (captura 02:12
            // del 29-10) y la cinta por la barra de Lots. El tunel es transparente (16 %): las velas se siguen viendo.
            DrawAbovePrice = true;
            if (DataSeries.Count > 0 && DataSeries[0] is ValueDataSeries v)
            {
                v.IsHidden = true;
                v.VisualType = VisualMode.Hide;
                v.ShowCurrentValue = false;
            }
        }

        protected override void OnInitialize()
        {
            try
            {
                var ps = DataProvider != null ? DataProvider.Panels : null;
                if (ps != null && ps.Count > 0) Panel = ps.Contains("Chart") ? "Chart" : ps[0];
            }
            catch (Exception e) { Anotar(e); }
            DrawAbovePrice = true;   // ATAS guarda este valor en el .ws (la 1.0 lo guardo en false): se fuerza al iniciar
            // titilar la flecha solo cuando hay una posicion en contra
            _latido = () => { try { if (_enContra) RedrawChart(new RedrawArg(Container.Region)); } catch { } };
            SubscribeToTimer(_periodo, _latido);
        }

        protected override void OnDispose()
        {
            try { if (_latido != null) UnsubscribeFromTimer(_periodo, _latido); } catch { }
            base.OnDispose();
        }

        protected override void OnRecalculate()
        {
            lock (_llave)
            {
                foreach (var x in new List<double>[] { _o, _h, _l, _c, _atr, _fu, _fd, _st, _pv, _vv, _vwap, _ext }) x.Clear();
                _dir.Clear(); _hora.Clear(); _iext.Clear(); _rap.Clear();
                _volcar = true;
            }
        }

        /// <summary>SuperTrend estandar (Wilder): la banda de abajo solo sube mientras el cierre anterior este arriba de ella; la de
        /// arriba solo baja mientras el cierre anterior este abajo; la direccion cambia cuando el cierre cruza la banda vigente.
        /// Misma cuenta que maquetas/datos_dia.py (la que se le mostro al operador).</summary>
        protected override void OnCalculate(int bar, decimal value)
        {
            try
            {
                var k = GetCandle(bar); if (k == null) return;
                double o = (double)k.Open, h = (double)k.High, l = (double)k.Low, c = (double)k.Close, vol = (double)k.Volume;
                bool nueva = bar == 0;
                if (!nueva) { try { nueva = IsNewSession(bar); } catch { } }
                lock (_llave)
                {
                    while (_c.Count <= bar)
                    {
                        _o.Add(0); _h.Add(0); _l.Add(0); _c.Add(0); _atr.Add(0); _fu.Add(0); _fd.Add(0); _st.Add(0); _dir.Add(1);
                        _pv.Add(0); _vv.Add(0); _vwap.Add(0); _hora.Add(DateTime.MinValue);
                        _ext.Add(0); _iext.Add(0); _rap.Add(false);
                    }
                    _o[bar] = o; _h[bar] = h; _l[bar] = l; _c[bar] = c; _hora[bar] = k.Time;
                    double cp = bar > 0 ? _c[bar - 1] : c;
                    double tr = Math.Max(h - l, Math.Max(Math.Abs(h - cp), Math.Abs(l - cp)));
                    _atr[bar] = bar == 0 ? tr : (_atr[bar - 1] * (_atrN - 1) + tr) / _atrN;
                    double hl2 = (h + l) / 2, up = hl2 - _mult * _atr[bar], dn = hl2 + _mult * _atr[bar];
                    _rap[bar] = false;
                    bool retro = _modo == ModoCorriente.Retroceso;
                    if (bar == 0) { _fu[bar] = up; _fd[bar] = dn; _dir[bar] = 1; _ext[bar] = h; _iext[bar] = 0; }
                    else
                    {
                        _fu[bar] = _c[bar - 1] > _fu[bar - 1] ? Math.Max(up, _fu[bar - 1]) : up;
                        _fd[bar] = _c[bar - 1] < _fd[bar - 1] ? Math.Min(dn, _fd[bar - 1]) : dn;
                        int d0 = _dir[bar - 1];
                        // extremo del tramo, incluida esta vela (todo sale de bar - 1: se puede recalcular la vela en curso tick a tick)
                        double ext = _ext[bar - 1]; int iext = _iext[bar - 1];
                        if (d0 == 1 && h > ext) { ext = h; iext = bar; }
                        else if (d0 == -1 && l < ext) { ext = l; iext = bar; }
                        int d;
                        if (retro)
                            // RETROCESO (1.2): se da vuelta cuando el cierre retrocede CorRetroceso pts desde el extremo del tramo
                            d = d0 * (ext - c) >= _retro ? -d0 : d0;
                        else
                        {
                            d = d0 == 1 && c < _fu[bar] ? -1 : d0 == -1 && c > _fd[bar] ? 1 : d0;
                            // VUELTA RAPIDA (1.1): el precio retrocede CorRapidaAtr ATR desde el extremo del tramo en CorRapidaVelas velas o menos
                            if (d == d0 && _rapida && bar - iext <= _rapJ)
                            {
                                if (d0 == 1 && ext - c >= _rapV * _atr[bar]) { d = -1; _rap[bar] = true; }
                                else if (d0 == -1 && c - ext >= _rapV * _atr[bar]) { d = 1; _rap[bar] = true; }
                            }
                        }
                        if (d != d0) { ext = d == 1 ? h : l; iext = bar; }
                        _dir[bar] = d; _ext[bar] = ext; _iext[bar] = iext;
                    }
                    // la linea del tunel: en RETROCESO es el punto exacto donde se da vuelta (X pts del extremo del tramo)
                    _st[bar] = retro ? _ext[bar] - _dir[bar] * _retro : _dir[bar] == 1 ? _fu[bar] : _fd[bar];
                    double tp = (h + l + c) / 3;
                    _pv[bar] = (nueva ? 0 : _pv[bar - 1]) + tp * vol;
                    _vv[bar] = (nueva ? 0 : _vv[bar - 1]) + vol;
                    _vwap[bar] = _vv[bar] > 0 ? _pv[bar] / _vv[bar] : tp;
                }
            }
            catch (Exception e) { Anotar(e); }
        }

        private static Color De(System.Windows.Media.Color c) => Color.FromArgb(c.A, c.R, c.G, c.B);
        private static Color Alfa(Color c, int a) => Color.FromArgb(Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B);

        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            try { Pintar(g); } catch (Exception e) { Anotar(e); }
        }

        private void Pintar(RenderContext g)
        {
            var cont = ChartInfo?.PriceChartContainer; var mio = Container;
            if (cont == null || mio == null) return;
            var reg = mio.Region; if (reg.Width < 40 || reg.Height < 40) return;
            int ult = CurrentBar - 1; if (ult < 1) return;
            int desde = Math.Max(1, FirstVisibleBarNumber), hasta = Math.Min(ult, LastVisibleBarNumber);
            if (hasta < desde) return;
            int n = hasta - desde + 1;
            double[] c = new double[n], st = new double[n], vw = new double[n]; int[] dir = new int[n];
            lock (_llave)
            {
                if (_c.Count <= hasta) return;
                for (int i = 0; i < n; i++) { c[i] = _c[desde + i]; st[i] = _st[desde + i]; vw[i] = _vwap[desde + i]; dir[i] = _dir[desde + i]; }
            }
            int[] x0 = new int[n + 1];
            for (int i = 0; i <= n; i++) { try { x0[i] = cont.GetXByBar(desde + i, true); } catch { x0[i] = i > 0 ? x0[i - 1] + 6 : reg.Left; } }
            if (n >= 2 && x0[n] <= x0[n - 1]) x0[n] = x0[n - 1] + (x0[n - 1] - x0[n - 2]);
            int Y(double p) { try { return cont.GetYByPrice((decimal)p, false); } catch { return reg.Top; } }
            int xMax = reg.Right; try { var cb = g.ClipBounds; if (cb.Width > 0) xMax = Math.Min(xMax, cb.Right); } catch { }
            Color cSube = De(CorColorSube), cBaja = De(CorColorBaja);
            bool Acuerdo(int i) => (c[i] > vw[i]) == (dir[i] > 0);

            // TUNEL: franja cierre <-> SuperTrend por vela, y la linea del SuperTrend (cortada donde cambia de lado)
            if (CorTunel)
            {
                int alfa = (int)(255 * Math.Max(3, Math.Min(60, CorOpacidad)) / 100.0);
                for (int i = 0; i < n; i++)
                {
                    int ya = Y(c[i]), yb = Y(st[i]);
                    var r = new Rectangle(x0[i], Math.Min(ya, yb), Math.Max(1, x0[i + 1] - x0[i]), Math.Max(1, Math.Abs(ya - yb)));
                    if (r.Right > reg.Left && r.Left < xMax) g.FillRectangle(Alfa(dir[i] > 0 ? cSube : cBaja, alfa), r);
                }
                for (int i = 1; i < n; i++)
                {
                    if (dir[i] != dir[i - 1]) continue;
                    var pen = new RenderPen(dir[i] > 0 ? cSube : cBaja, 2);
                    g.DrawLine(pen, (x0[i - 1] + x0[i]) / 2, Y(st[i - 1]), (x0[i] + x0[i + 1]) / 2, Y(st[i]));
                }
            }

            // CINTA: un color por vela abajo; fuerte si coincide con el lado del VWAP, palida si no
            int yCinta = reg.Bottom - Math.Max(0, CorCintaSobreBorde) - Math.Max(2, CorCintaAlto);
            if (CorCinta)
                for (int i = 0; i < n; i++)
                {
                    int a = Math.Max(x0[i], reg.Left), b = Math.Min(x0[i + 1], xMax);
                    if (b <= a) continue;
                    g.FillRectangle(Alfa(dir[i] > 0 ? cSube : cBaja, Acuerdo(i) ? 235 : 80), new Rectangle(a, yCinta, b - a, Math.Max(2, CorCintaAlto)));
                }

            // FLECHA: a la derecha de la vela en curso, a la altura de su cierre
            string flechaAudit = "no (grafico corrido hacia atras o apagada)";
            _enContra = false;
            if (CorFlecha && hasta == ult)
            {
                int j = n - 1, d = dir[j];
                decimal volPos = 0;
                try { var pos = TradingManager?.Position; if (pos != null && pos.IsInPosition) volPos = pos.Volume; } catch { }
                _enContra = volPos != 0 && Math.Sign(volPos) != d;
                bool visible = !_enContra || DateTime.UtcNow.Millisecond < 600;   // en contra: titila (encendida 60 % del ciclo)
                // 1.0c: chica, entra entre la ultima vela y la columna de etiquetas de la 5.0 (con 22 px pisaba "SPY P -3,49B")
                int tam = _enContra ? 16 : 12;
                int xc = Math.Min(xMax - tam - 4, x0[n] + tam / 2 + 4), yc = Y(c[j]);   // pegada a la ultima vela: el hueco antes de las etiquetas es de ~24 px
                Color col = d > 0 ? cSube : cBaja;
                if (visible) Flecha(g, xc, yc, tam, d, col, Acuerdo(j));
                flechaAudit = string.Format(Inv, "{0} {1}, x {2} y {3}, tam {4}, posicion {5}{6}", d > 0 ? "ARRIBA" : "ABAJO",
                    Acuerdo(j) ? "llena (coincide con el VWAP)" : "hueca (no coincide con el VWAP)", xc, yc, tam, volPos, _enContra ? " EN CONTRA: titila" : "");
            }

            if (CorRegistrar && (DateTime.UtcNow - _ultAudit).TotalSeconds >= 60)
            {
                _ultAudit = DateTime.UtcNow;
                Auditar(flechaAudit, yCinta, desde, hasta);
            }
        }

        /// <summary>Flecha: punta triangular + tallo, centrada en (xc, yc), apuntando para arriba (d &gt; 0) o para abajo. Llena o
        /// hueca (solo el contorno, 2 px).</summary>
        private static void Flecha(RenderContext g, int xc, int yc, int tam, int d, Color col, bool llena)
        {
            int mitad = tam / 2, punta = (int)(tam * 0.55), tallo = Math.Max(3, tam / 3);
            int yPunta = yc - d * mitad, yBase = yPunta + d * punta, yFin = yc + d * mitad;
            var tri = new[] { new Point(xc, yPunta), new Point(xc - mitad, yBase), new Point(xc + mitad, yBase) };
            var palo = new Rectangle(xc - tallo / 2, Math.Min(yBase, yFin), tallo, Math.Max(1, Math.Abs(yFin - yBase)));
            if (llena)
            {
                g.FillPolygon(col, tri);
                g.FillRectangle(col, palo);
            }
            else
            {
                var pen = new RenderPen(col, 2);
                g.DrawPolygon(pen, tri);
                g.DrawRectangle(pen, palo);
            }
        }

        // ------------------------------------------------------------------ registro
        private static string Carpeta() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex", "corriente");

        private void Auditar(string flecha, int yCinta, int desde, int hasta)
        {
            try
            {
                var sb = new StringBuilder();
                string inst = "?"; try { inst = InstrumentInfo?.Instrument ?? "?"; } catch { }
                string marco = "?"; try { marco = ChartInfo?.TimeFrame ?? "?"; } catch { }
                sb.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", Inv)).Append(" UTC v").Append(Version).Append(' ').Append(inst).Append(' ').Append(marco)
                  .Append(_modo == ModoCorriente.Retroceso ? string.Format(Inv, " RETROCESO {0:0.##} pts", _retro)
                      : string.Format(Inv, " ATR {0} x {1:0.##}", _atrN, _mult) + (_rapida ? string.Format(Inv, " | rapida {0:0.##} ATR en {1} velas", _rapV, _rapJ) : " | rapida APAGADA"))
                  .Append('\n');
                sb.Append("  flecha: ").Append(flecha).Append(" | cinta y ").Append(yCinta).Append(" | velas visibles ").Append(desde).Append('-').Append(hasta).Append('\n');
                StringBuilder volcado = null;
                lock (_llave)
                {
                    int ult = _c.Count - 1;
                    for (int i = Math.Max(0, ult - 14); i <= ult; i++)
                        sb.Append(string.Format(Inv, "  vela {0:HH:mm} UTC o {1} h {2} l {3} c {4} | atr {5:0.0000} st {6:0.00} dir {7} vwap {8:0.00} | ext {9} hace {10}{11}\n",
                            _hora[i], _o[i], _h[i], _l[i], _c[i], _atr[i], _st[i], _dir[i], _vwap[i], _ext[i], i - _iext[i], _rap[i] ? " RAPIDA" : ""));
                    // vueltas de las ultimas 3 horas, con su causa: lo que se sigue midiendo en vivo
                    sb.Append("  vueltas (3 h):");
                    for (int i = Math.Max(1, ult - 180); i <= ult; i++)
                        if (_dir[i] != _dir[i - 1])
                            sb.Append(string.Format(Inv, " {0:HH:mm}{1}{2}@{3}", _hora[i], _dir[i] > 0 ? "+" : "-", _rap[i] ? "R" : "L", _c[i]));
                    sb.Append('\n');
                    if (_volcar && ult > 0)
                    {
                        // VOLCADO (1.1; la 1.2 agrega la linea): todas las velas cargadas con lo calculado, para recalcularlas en Python
                        _volcar = false;
                        volcado = new StringBuilder("hora_utc,o,h,l,c,atr,fu,fd,dir,ext,iext,rapida,linea\n");
                        for (int i = 0; i <= ult; i++)
                            volcado.Append(string.Format(Inv, "{0:yyyy-MM-dd HH:mm:ss},{1},{2},{3},{4},{5:R},{6:R},{7:R},{8},{9},{10},{11},{12:R}\n",
                                _hora[i], _o[i], _h[i], _l[i], _c[i], _atr[i], _fu[i], _fd[i], _dir[i], _ext[i], _iext[i], _rap[i] ? 1 : 0, _st[i]));
                    }
                }
                Directory.CreateDirectory(Carpeta());
                File.AppendAllText(Path.Combine(Carpeta(), "auditoria-" + DateTime.UtcNow.ToString("yyyy-MM-dd", Inv) + ".log"), sb.ToString());
                if (volcado != null)
                    File.WriteAllText(Path.Combine(Carpeta(), _modo == ModoCorriente.Retroceso
                        ? string.Format(Inv, "volcado-{0:yyyy-MM-dd_HHmmss}-{1}-retro{2:0.##}.csv", DateTime.UtcNow, inst, _retro)
                        : string.Format(Inv, "volcado-{0:yyyy-MM-dd_HHmmss}-{1}-atr{2}x{3:0.##}-r{4}.csv",
                            DateTime.UtcNow, inst, _atrN, _mult, _rapida ? _rapV.ToString("0.##", Inv) + "en" + _rapJ : "no")), volcado.ToString());
            }
            catch { }
        }

        private void Anotar(Exception e)
        {
            try
            {
                Directory.CreateDirectory(Carpeta());
                File.AppendAllText(Path.Combine(Carpeta(), "errores.log"), DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", Inv) + " " + e + "\n");
            }
            catch { }
        }
    }
}
