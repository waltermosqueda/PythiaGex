using System;
using System.Drawing;

using ATAS.Indicators;
using OFT.Rendering;
using OFT.Rendering.Control;

namespace PythiaLaOla
{
    /// <summary>
    /// LA OLA 1.0c (27-11-2026): EL MOUSE DEL CUADRO. Pedido del operador: "el cuadro tapa, molesta: deberia ser minimizable y que se pueda
    /// correr solo arrastrando el mouse; eso es simplicidad y amigable al usuario".
    ///  - Apretar en cualquier parte del cuadro (o de la pastilla) y arrastrar lo mueve: al soltar queda en PoPanelX / PoPanelY (entero
    ///    adentro del grafico).
    ///  - Si el mouse se movio menos de 3 px es un CLIC: en el "–" lo minimiza (PoPanelMinimizado); en la pastilla lo abre. En el resto del
    ///    cuadro el clic no hace nada (pero tampoco va al grafico).
    ///  - FUERA del cuadro no se toma NINGUN evento (return base...): el operador opera desde el grafico. Con "Mover y minimizar con el mouse"
    ///    apagado (PoPanelConMouse), tampoco adentro: como la 1.0b.
    ///  - Cursor: mano sobre el "–", "mover" sobre el cuadro y la pastilla.
    ///  - Por si ATAS no avisa: si durante la presion no llega ningun ProcessMouseMove, el latido sigue al cursor de Windows; si el boton ya
    ///    se solto (dos latidos) sin ProcessMouseUp, la presion se termina igual. El cartelito del mouse se borra aunque ATAS no avise que el
    ///    mouse se fue (el cursor real contra la ultima posicion que dio ATAS, como la lupa del Superindicador 1.3).
    /// </summary>
    public partial class PythiaOla
    {
        private sealed class PresionPanel
        {
            public string Zona = ""; public Point Ini, IniPantalla; public bool HayPantalla; public int PX0, PY0;
            public volatile bool Movio; public int Movimientos, Terminada; public DateTime SueltoDesde = DateTime.MinValue;
        }

        private const int UmbralArrastre = 3;
        private readonly object _llaveMouse = new object();
        private PresionPanel _presionPanel;
        private volatile int _arrPX, _arrPY;   // lugar en vivo mientras se arrastra (desde la esquina del grafico)
        // del ultimo dibujo del cuadro: su caja, el "–", la region y si era la pastilla
        private Rectangle _panelCaja = Rectangle.Empty, _panelMenos = Rectangle.Empty, _regPanel = Rectangle.Empty;
        private bool _panelPastilla;
        private volatile string _zonaUlt = "";
        private DateTime _finPresion = DateTime.MinValue; private Point _finPresionEn;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct PuntoW { public int X, Y; }
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out PuntoW p);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
        private Point _posAtasUlt = new Point(int.MinValue, int.MinValue), _cursorAncla;
        private bool _hayAncla;

        // ------------------------------------------------------------------ lugar del cuadro (lo deja el dibujo)
        /// <summary>Desde el dibujo: guarda donde quedo el cuadro (para el mouse) y devuelve la zona bajo el mouse ("" si ninguna).</summary>
        private string PanelDibujado(Rectangle reg, Rectangle caja, Rectangle menos, bool pastilla, Point m)
        {
            lock (_llaveMouse) { _panelCaja = _panelMouse ? caja : Rectangle.Empty; _panelMenos = menos; _regPanel = reg; _panelPastilla = pastilla; }
            string z = m.X >= 0 ? ZonaPanel(m.X, m.Y) : "";
            _zonaUlt = z;
            return z;
        }

        /// <summary>El cuadro no se dibujo (apagado o grafico sin lugar): no toma ningun clic.</summary>
        private void PanelOculto() { lock (_llaveMouse) { _panelCaja = Rectangle.Empty; _panelMenos = Rectangle.Empty; } }

        private string ZonaPanel(int x, int y)
        {
            lock (_llaveMouse)
            {
                if (_panelCaja.IsEmpty || !_panelCaja.Contains(x, y)) return "";
                if (_panelPastilla) return "pastilla";
                if (!_panelMenos.IsEmpty && _panelMenos.Contains(x, y)) return "menos";
                return "panel";
            }
        }

        /// <summary>El mouse para el dibujo del cuadro: (-1, -1) si se esta arrastrando, si no esta sobre el grafico o si el cuadro no usa el mouse.</summary>
        private Point MousePanel()
        {
            if (!_panelMouse) return new Point(-1, -1);
            lock (_llaveMouse) { if (_presionPanel != null) return new Point(-1, -1); }
            return PosMouse();
        }

        /// <summary>Posicion del mouse sobre el grafico, o (-1, -1) si esta afuera. ATAS NO siempre avisa IsMouseLeave: se ancla el cursor REAL
        /// de Windows cada vez que ATAS da una posicion nueva; si el cursor real se aleja mas de 4 px del ancla y ATAS sigue con la misma, el
        /// mouse se fue (la regla de la lupa del Superindicador 1.3, medida el 23-11).</summary>
        private Point PosMouse()
        {
            try
            {
                var mi = ChartInfo?.MouseLocationInfo ?? MouseLocationInfo;
                if (mi == null) return new Point(-1, -1);
                if (mi.IsMouseLeave || mi.IsMovingChartUsingMouse) return new Point(-1, -1);
                var pg = mi.LastPosition;
                if (GetCursorPos(out var w))
                {
                    var pr = new Point(w.X, w.Y);
                    lock (_llaveMouse)
                    {
                        if (pg != _posAtasUlt) { _posAtasUlt = pg; _cursorAncla = pr; _hayAncla = true; }
                        else if (_hayAncla && (Math.Abs(pr.X - _cursorAncla.X) > 4 || Math.Abs(pr.Y - _cursorAncla.Y) > 4)) return new Point(-1, -1);
                    }
                }
                var reg = ChartInfo?.PriceChartContainer?.Region ?? Rectangle.Empty;
                if (!reg.IsEmpty && !reg.Contains(pg)) return new Point(-1, -1);
                return pg;
            }
            catch { }
            return new Point(-1, -1);
        }

        /// <summary>El boton izquierdo LOGICO esta apretado (con los botones cambiados de mano, es el fisico derecho: SM_SWAPBUTTON).</summary>
        private static bool BotonIzquierdoApretado()
        {
            try { int vk = GetSystemMetrics(23) != 0 ? 0x02 : 0x01; return (GetAsyncKeyState(vk) & 0x8000) != 0; }
            catch { return false; }
        }

        // ------------------------------------------------------------------ eventos de ATAS
        public override bool ProcessMouseDown(RenderControlMouseEventArgs e)
        {
            try
            {
                if (_panelMouse && _panel && e.Button == RenderControlMouseButtons.Left)
                {
                    string z = ZonaPanel(e.X, e.Y);
                    if (z.Length > 0)
                    {
                        Rectangle caja, reg; lock (_llaveMouse) { caja = _panelCaja; reg = _regPanel; }
                        // el arrastre arranca desde donde esta DIBUJADO (si el grafico se achico, PoPanelX / Y pueden quedar afuera)
                        var pr = new PresionPanel { Zona = z, Ini = new Point(e.X, e.Y), PX0 = caja.X - reg.Left, PY0 = caja.Y - reg.Top };
                        if (GetCursorPos(out var w)) { pr.IniPantalla = new Point(w.X, w.Y); pr.HayPantalla = true; }
                        _arrPX = pr.PX0; _arrPY = pr.PY0;
                        lock (_llaveMouse) _presionPanel = pr;
                        Redibujar();
                        return true;
                    }
                }
            }
            catch (Exception ex) { Anotar(ex); }
            return base.ProcessMouseDown(e);
        }

        public override bool ProcessMouseMove(RenderControlMouseEventArgs e)
        {
            try
            {
                PresionPanel pr; lock (_llaveMouse) pr = _presionPanel;
                if (pr != null)
                {
                    pr.Movimientos++;
                    int mx = e.X - pr.Ini.X, my = e.Y - pr.Ini.Y;
                    if (!pr.Movio && (Math.Abs(mx) >= UmbralArrastre || Math.Abs(my) >= UmbralArrastre)) pr.Movio = true;
                    if (pr.Movio) { _arrPX = pr.PX0 + mx; _arrPY = pr.PY0 + my; Redibujar(); }
                    return true;   // mientras se arrastra el cuadro, el grafico no se mueve
                }
                if (_panelMouse && _panel)
                {
                    string z = ZonaPanel(e.X, e.Y);
                    if (z != _zonaUlt) { _zonaUlt = z; Redibujar(); }
                }
            }
            catch { }
            return base.ProcessMouseMove(e);
        }

        public override bool ProcessMouseUp(RenderControlMouseEventArgs e)
        {
            try
            {
                PresionPanel pr; lock (_llaveMouse) pr = _presionPanel;
                if (pr != null)
                {
                    int mx = e.X - pr.Ini.X, my = e.Y - pr.Ini.Y;
                    if (!pr.Movio && (Math.Abs(mx) >= UmbralArrastre || Math.Abs(my) >= UmbralArrastre)) pr.Movio = true;
                    TerminarPresionPanel(pr, pr.PX0 + mx, pr.PY0 + my, new Point(e.X, e.Y), "suelta");
                    return true;
                }
                if (_panelMouse && ZonaPanel(e.X, e.Y).Length > 0) return true;
            }
            catch (Exception ex) { Anotar(ex); }
            return base.ProcessMouseUp(e);
        }

        /// <summary>Un clic que ATAS arma sobre el cuadro (o donde se acaba de soltar el arrastre) no llega al grafico.</summary>
        private bool ClicDelPanel(int x, int y)
        {
            if (_panelMouse && ZonaPanel(x, y).Length > 0) return true;
            return (DateTime.UtcNow - _finPresion).TotalMilliseconds < 700 && Math.Abs(x - _finPresionEn.X) <= 12 && Math.Abs(y - _finPresionEn.Y) <= 12;
        }

        public override bool ProcessMouseClick(RenderControlMouseEventArgs e)
        {
            try { if (ClicDelPanel(e.X, e.Y)) return true; } catch { }
            return base.ProcessMouseClick(e);
        }

        public override bool ProcessMouseDoubleClick(RenderControlMouseEventArgs e)
        {
            try { if (ClicDelPanel(e.X, e.Y)) return true; } catch { }
            return base.ProcessMouseDoubleClick(e);
        }

        public override StdCursor GetCursor(RenderControlMouseEventArgs e)
        {
            try
            {
                PresionPanel pr; lock (_llaveMouse) pr = _presionPanel;
                if (pr != null) return StdCursor.SizeAll;   // mover
                if (_panelMouse && _panel)
                {
                    string z = ZonaPanel(e.X, e.Y);
                    if (z == "menos") return StdCursor.Hand;
                    if (z.Length > 0) return StdCursor.SizeAll;
                }
            }
            catch { }
            return base.GetCursor(e);
        }

        // ------------------------------------------------------------------ fin de la presion
        /// <summary>Una sola vez aunque lleguen ProcessMouseUp y el latido: arrastre de 3 px o mas = el cuadro queda ahi (entero adentro del
        /// grafico, guardado en PoPanelX / PoPanelY); si no, clic: el "–" minimiza y la pastilla abre.</summary>
        private void TerminarPresionPanel(PresionPanel pr, int px, int py, Point en, string como)
        {
            if (System.Threading.Interlocked.Exchange(ref pr.Terminada, 1) == 1) return;
            try
            {
                if (pr.Movio)
                {
                    Rectangle caja, reg; lock (_llaveMouse) { caja = _panelCaja; reg = _regPanel; }
                    if (!reg.IsEmpty && !caja.IsEmpty)
                    {
                        px = Math.Max(0, Math.Min(reg.Width - 4 - caja.Width, px));
                        py = Math.Max(0, Math.Min(reg.Height - 4 - caja.Height, py));
                    }
                    PoCuadroX = px; PoCuadroY = py;   // 1.0d: el lugar nuevo (PoPanelX / PoPanelY quedan sin uso)
                    RaisePropertyChanged(nameof(PoCuadroX)); RaisePropertyChanged(nameof(PoCuadroY));
                    AnotarLinea("panel: movido (" + como + ") a " + _lugarX + ", " + _lugarY + " px de la esquina del grafico");
                }
                else if (pr.Zona == "menos")
                {
                    PoPanelMinimizado = true; RaisePropertyChanged(nameof(PoPanelMinimizado));
                    AnotarLinea("panel: minimizado (" + como + ")");
                }
                else if (pr.Zona == "pastilla")
                {
                    PoPanelMinimizado = false; RaisePropertyChanged(nameof(PoPanelMinimizado));
                    AnotarLinea("panel: abierto (" + como + ")");
                }
            }
            catch (Exception ex) { Anotar(ex); }
            finally
            {
                _finPresion = DateTime.UtcNow; _finPresionEn = en;
                lock (_llaveMouse) { if (ReferenceEquals(_presionPanel, pr)) _presionPanel = null; }
                Redibujar();
            }
        }

        /// <summary>Desde el latido (250 ms): la presion que ATAS no termino y el cartelito del mouse.</summary>
        private void PanelLatido()
        {
            RevisarPresionPanel();
            string z = "";
            if (_panelMouse && _panel)
            {
                bool presion; lock (_llaveMouse) presion = _presionPanel != null;
                if (!presion) { var m = PosMouse(); if (m.X >= 0) z = ZonaPanel(m.X, m.Y); }
            }
            if (z != _zonaUlt) { _zonaUlt = z; Redibujar(); }
        }

        /// <summary>(1) Si durante la presion no llego NINGUN ProcessMouseMove, el cuadro sigue al cursor de Windows; (2) si el boton ya se
        /// solto (dos latidos seguidos) y no llego ProcessMouseUp (por ejemplo, se solto afuera del grafico), la presion se termina igual con
        /// lo ultimo visto. Nunca queda un cuadro pegado al mouse.</summary>
        private void RevisarPresionPanel()
        {
            PresionPanel pr; lock (_llaveMouse) pr = _presionPanel;
            if (pr == null || pr.Terminada != 0) return;
            if (pr.Movimientos == 0 && pr.HayPantalla && GetCursorPos(out var w))
            {
                int mx = w.X - pr.IniPantalla.X, my = w.Y - pr.IniPantalla.Y;
                if (!pr.Movio && (Math.Abs(mx) >= UmbralArrastre || Math.Abs(my) >= UmbralArrastre)) pr.Movio = true;
                if (pr.Movio) { _arrPX = pr.PX0 + mx; _arrPY = pr.PY0 + my; Redibujar(); }
            }
            if (BotonIzquierdoApretado()) { pr.SueltoDesde = DateTime.MinValue; return; }
            var ahora = DateTime.UtcNow;
            if (pr.SueltoDesde == DateTime.MinValue) { pr.SueltoDesde = ahora; return; }
            if ((ahora - pr.SueltoDesde).TotalMilliseconds < 200) return;
            int fx = _arrPX, fy = _arrPY; var en = pr.Ini;
            var disp = System.Windows.Application.Current?.Dispatcher;
            if (disp != null) disp.BeginInvoke(new Action(() => TerminarPresionPanel(pr, fx, fy, en, "latido")));
            else TerminarPresionPanel(pr, fx, fy, en, "latido");
        }
    }
}
