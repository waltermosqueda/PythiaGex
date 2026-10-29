using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace EstadisticaVisible
{
    /// <summary>
    /// ESTADISTICA VISIBLE 1.3 (28-10-2026). Pedido del operador: "tengo que hacer recontra zoom para que se vean los numeros
    /// del Cluster Statistic y ahi pierdo contexto del grafico"; y despues: "que se vean SIEMPRE los ultimos 10 o 15 cuadros,
    /// en el cuadro mismo, sin tener que hacer zoom; vos defini cuantos por los pixeles".
    ///
    /// EL PROBLEMA, MEDIDO. El Cluster Statistic de fabrica escribe el numero solo si entra en el ancho de la columna de la vela.
    /// Con su zoom de siempre cada columna mide 6-9 px y "-245" pide ~19 aun con letra 7: ATAS lo oculta, y de sus 72 ajustes
    /// ninguno lo fuerza. Probado y descartado: turnar 2-3 renglones debajo de cada vela (1.0-1.2) solo alcanza con columnas de
    /// 10 px o mas; con 6 px quedaban 2 velas con numero; las letras angostas (Bahnschrift, Arial Narrow) miden igual en ATAS.
    ///
    /// LA SOLUCION. Las mismas filas (Delta, Delta de la sesion, Volumen). A la izquierda, la tira pintada debajo de cada vela
    /// (el color dice tamaño y signo). A la derecha, CUADROS de las ultimas velas, uno por vela y por fila, con letra grande; la
    /// mas nueva pegada al borde y con recuadro; una raya arriba marca que velas de la tira estan en los cuadros. Cuantos: los
    /// que entren en el 75 % del ancho, con tope EstCuantas (15).
    ///
    /// LOS DATOS. Los mismos que el de fabrica: GetCandle(bar).Delta y .Volume; la sesion se reinicia con IsNewSession (la
    /// sesion del grafico, el modo por defecto del Cluster Statistic). Los miles van con una decimal ("-1,4k"); el de fabrica
    /// redondea a "-1K".
    ///
    /// AUDITORIA. Cada 60 s anota en %APPDATA%\ATAS\PythiaGex\estadistica\auditoria-AAAA-MM-DD.log el ancho de columna, el alto
    /// de fila, cuantos cuadros, su ancho y su letra, y cada numero escrito (hora de la vela, cuadro, texto) junto al valor
    /// crudo, para compararlo contra la pantalla.
    ///
    /// LOS AJUSTES LLEVAN EL PREFIJO "Est": ATAS guarda los ajustes POR NOMBRE en el workspace (.ws).
    /// </summary>
    [DisplayName("PythiaGex - Estadistica Visible")]
    [Category("PythiaGex")]
    public class EstadisticaVisibleIndicador : Indicator
    {
        public const string Version = "1.3b";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ ajustes
        [Display(Name = "Delta", GroupName = "Filas", Order = 10)]
        public bool EstDelta { get => _fDelta; set { _fDelta = value; RecalculateValues(); } }
        private bool _fDelta = true;

        [Display(Name = "Delta de la sesion", GroupName = "Filas", Order = 20)]
        public bool EstSesion { get => _fSesion; set { _fSesion = value; RecalculateValues(); } }
        private bool _fSesion = true;

        [Display(Name = "Volumen", GroupName = "Filas", Order = 30)]
        public bool EstVolumen { get => _fVolumen; set { _fVolumen = value; RecalculateValues(); } }
        private bool _fVolumen = true;

        [Display(Name = "Cuadros de las ultimas velas (tope)", GroupName = "Numeros", Order = 35)]
        [Range(3, 40)]
        public int EstCuantas { get; set; } = 15;

        [Display(Name = "Nombres de las filas", GroupName = "Vista", Order = 40)]
        public bool EstNombres { get; set; } = true;

        [Display(Name = "Tamano de letra de los nombres", GroupName = "Vista", Order = 50)]
        [Range(7, 14)]
        public int EstLetra { get; set; } = 9;

        [Display(Name = "Tamano de letra de los cuadros", GroupName = "Numeros", Order = 36)]
        [Range(7, 16)]
        public int EstLetraCajas { get; set; } = 11;

        [Display(Name = "Color positivo", GroupName = "Colores", Order = 70)]
        public System.Windows.Media.Color EstColorMas { get; set; } = System.Windows.Media.Color.FromRgb(38, 166, 91);

        [Display(Name = "Color negativo", GroupName = "Colores", Order = 80)]
        public System.Windows.Media.Color EstColorMenos { get; set; } = System.Windows.Media.Color.FromRgb(214, 48, 49);

        [Display(Name = "Color volumen", GroupName = "Colores", Order = 90)]
        public System.Windows.Media.Color EstColorVolumen { get; set; } = System.Windows.Media.Color.FromRgb(52, 120, 200);

        [Display(Name = "Color del texto", GroupName = "Colores", Order = 100)]
        public System.Windows.Media.Color EstColorTexto { get; set; } = System.Windows.Media.Color.FromRgb(236, 239, 244);

        [Display(Name = "Registro de auditoria", GroupName = "Auditoria", Order = 110)]
        public bool EstRegistrar { get; set; } = true;

        // ------------------------------------------------------------------ datos (por vela, bajo llave)
        private readonly object _llave = new object();
        private readonly List<double> _delta = new List<double>(), _ses = new List<double>(), _vol = new List<double>();
        private readonly List<DateTime> _hora = new List<DateTime>();
        private DateTime _ultAudit = DateTime.MinValue;

        private enum Tipo { Delta, Sesion, Volumen }

        public EstadisticaVisibleIndicador() : base(true)
        {
            Panel = IndicatorDataProvider.NewPanel;
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
            DrawAbovePrice = true;
            try { ShowDescription = false; } catch { }
            if (DataSeries.Count > 0 && DataSeries[0] is ValueDataSeries v)
            {
                v.IsHidden = true;
                v.VisualType = VisualMode.Hide;
                v.ShowCurrentValue = false;
            }
        }

        protected override void OnRecalculate()
        {
            lock (_llave) { _delta.Clear(); _ses.Clear(); _vol.Clear(); _hora.Clear(); }
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            try
            {
                var c = GetCandle(bar); if (c == null) return;
                double d = (double)c.Delta, v = (double)c.Volume;
                bool nueva = bar == 0;
                if (!nueva) { try { nueva = IsNewSession(bar); } catch { } }
                lock (_llave)
                {
                    while (_delta.Count <= bar) { _delta.Add(0); _ses.Add(0); _vol.Add(0); _hora.Add(DateTime.MinValue); }
                    _delta[bar] = d; _vol[bar] = v; _hora[bar] = c.Time;
                    _ses[bar] = nueva || bar == 0 ? d : _ses[bar - 1] + d;
                }
            }
            catch (Exception e) { Anotar(e); }
        }

        // ------------------------------------------------------------------ formato
        /// <summary>Hasta 999 entero; 1.000-9.999 con una decimal y "k"; 10k-999k entero con "k"; despues "M". Coma decimal.</summary>
        public static string Formato(double v)
        {
            double a = Math.Abs(v);
            string s;
            if (a < 999.5) s = Math.Round(v).ToString("0", Inv);
            else if (a < 9999.5) s = (v / 1000.0).ToString("0.0", Inv) + "k";
            else if (a < 999500) s = Math.Round(v / 1000.0).ToString("0", Inv) + "k";
            else s = (v / 1e6).ToString("0.0", Inv) + "M";
            return s.Replace('.', ',');
        }

        private static Color De(System.Windows.Media.Color c) => Color.FromArgb(c.A, c.R, c.G, c.B);

        // ------------------------------------------------------------------ dibujo
        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            try { Pintar(g); } catch (Exception e) { Anotar(e); }
        }

        /// <summary>Lo que hace falta para ubicar los numeros de una fila (se arma una vez por cuadro).</summary>
        private sealed class Fila
        {
            public Tipo Tipo; public Rectangle R; public double[] Val; public string[] Txt;
        }

        private void Pintar(RenderContext g)
        {
            var cont = ChartInfo?.PriceChartContainer; var mio = Container;
            if (cont == null || mio == null) return;
            var reg = mio.Region; if (reg.Width < 40 || reg.Height < 12) return;
            var tipos = new List<Tipo>();
            if (_fDelta) tipos.Add(Tipo.Delta); if (_fSesion) tipos.Add(Tipo.Sesion); if (_fVolumen) tipos.Add(Tipo.Volumen);
            if (tipos.Count == 0) return;

            int ult = CurrentBar - 1; if (ult < 0) return;
            int desde = Math.Max(0, FirstVisibleBarNumber), hasta = Math.Min(ult, LastVisibleBarNumber);
            if (hasta < desde) return;
            int n = hasta - desde + 1;

            // renglon de arriba con la HORA de cada cuadro (1.3b): sin ella, el eje de tiempo de ATAS que queda debajo de los cuadros
            // hace creer que cada cuadro es la vela de arriba (los cuadros son las ultimas velas, mas anchos que ellas)
            var fHora = new RenderFont("Segoe UI", 7);
            int altoCab = g.MeasureString("00:00", fHora).Height + 1;
            int altoFila = (reg.Height - altoCab) / tipos.Count;
            var filas = new List<Fila>();
            DateTime[] hora = new DateTime[n];
            lock (_llave)
            {
                if (_delta.Count <= hasta) return;
                for (int k = 0; k < tipos.Count; k++)
                {
                    var src = tipos[k] == Tipo.Delta ? _delta : tipos[k] == Tipo.Sesion ? _ses : _vol;
                    var f = new Fila { Tipo = tipos[k], R = new Rectangle(reg.Left, reg.Top + altoCab + k * altoFila, reg.Width, altoFila), Val = new double[n] };
                    for (int i = 0; i < n; i++) f.Val[i] = src[desde + i];
                    filas.Add(f);
                }
                for (int i = 0; i < n; i++) hora[i] = _hora[desde + i];
            }
            foreach (var f in filas) f.Txt = f.Val.Select(Formato).ToArray();

            // x de cada vela: borde izquierdo de la vela y de la siguiente
            int[] x0 = new int[n + 1];
            for (int i = 0; i <= n; i++)
            {
                try { x0[i] = cont.GetXByBar(desde + i, true); }
                catch { x0[i] = i > 0 ? x0[i - 1] + 6 : reg.Left; }
            }
            if (n >= 2 && x0[n] <= x0[n - 1]) x0[n] = x0[n - 1] + (x0[n - 1] - x0[n - 2]);
            var anchos = new List<int>(); for (int i = 0; i < n; i++) anchos.Add(Math.Max(1, x0[i + 1] - x0[i]));
            anchos.Sort(); int wCol = anchos[anchos.Count / 2];

            int xMax = reg.Right;
            try { var cb = g.ClipBounds; if (cb.Width > 0) xMax = Math.Min(xMax, cb.Right); } catch { }

            Color cMas = De(EstColorMas), cMenos = De(EstColorMenos), cVol = De(EstColorVolumen), cTxt = De(EstColorTexto);
            Color cFondo = Color.FromArgb(255, 19, 23, 34), cGrilla = Color.FromArgb(255, 42, 46, 57);
            var penGrilla = new RenderPen(cGrilla, 1);

            var fNom = new RenderFont("Segoe UI", Math.Max(7, EstLetra - 1));
            int anchoNom = 0;
            string[] nombres = tipos.Select(t => t == Tipo.Delta ? "Delta" : t == Tipo.Sesion ? "Delta sesion" : "Volumen").ToArray();
            if (EstNombres) foreach (var s in nombres) anchoNom = Math.Max(anchoNom, g.MeasureString(s, fNom).Width + 8);
            int xMinTxt = reg.Left + (EstNombres ? anchoNom : 0);

            // CUADROS DE LAS ULTIMAS VELAS (1.3). Con su zoom (6-9 px por vela) un numero (~19 px medido) no entra debajo de su vela
            // ni turnando tres renglones (la 1.2 llego a 2 velas con numero): las ultimas velas van en CUADROS anchos a la derecha
            // del panel, la mas nueva pegada al borde, uno por vela y por fila, con letra grande. Cuantos: los que entren en el 75 %
            // del ancho con esa letra, con tope EstCuantas (15). A la izquierda sigue la tira pintada alineada con las velas (el color
            // dice tamaño y signo) para el contexto; una raya arriba de la tira marca que velas estan en los cuadros.
            int cuantas = Math.Min(Math.Max(1, EstCuantas), n);
            RenderFont fCaja = null; int altoCaja = 0, letraCaja = 0;
            for (int tam = Math.Max(7, EstLetraCajas); tam >= 7 && fCaja == null; tam--)
            {
                var ft = new RenderFont("Segoe UI", tam); int h = g.MeasureString("0", ft).Height;
                if (h + 2 <= altoFila) { fCaja = ft; altoCaja = h; letraCaja = tam; }
            }
            int anchoCaja = 0, nCajas = 0, xCajas = xMax;
            if (fCaja != null)
            {
                int aMax = 0;
                foreach (var f in filas) for (int i = n - cuantas; i < n; i++) aMax = Math.Max(aMax, g.MeasureString(f.Txt[i], fCaja).Width);
                anchoCaja = aMax + 10;
                nCajas = Math.Min(cuantas, (int)((xMax - xMinTxt) * 0.75) / Math.Max(1, anchoCaja));
                xCajas = xMax - nCajas * anchoCaja;
            }
            int xFinTira = nCajas > 0 ? xCajas - 3 : xMax;

            // la tira: celdas pintadas debajo de las velas, intensidad por la raiz del tamaño relativo a lo visible
            var maximo = new double[filas.Count];
            for (int k = 0; k < filas.Count; k++)
            {
                var f = filas[k];
                double mx = 0; foreach (var v in f.Val) mx = Math.Max(mx, Math.Abs(v));
                maximo[k] = mx <= 0 ? 1 : mx;
                for (int i = 0; i < n; i++)
                {
                    int xa = x0[i], xb = Math.Min(x0[i + 1], xFinTira); if (xb <= reg.Left || xa >= xFinTira) continue;
                    double v = f.Val[i];
                    Color baseC = f.Tipo == Tipo.Volumen ? cVol : (v >= 0 ? cMas : cMenos);
                    int alfa = (int)(30 + 190 * Math.Sqrt(Math.Min(1.0, Math.Abs(v) / maximo[k])));
                    int gap = (x0[i + 1] - xa) >= 5 ? 1 : 0;
                    var celda = new Rectangle(Math.Max(xa, reg.Left), f.R.Top + 1, xb - Math.Max(xa, reg.Left) - gap, altoFila - 2);
                    if (celda.Width > 0 && celda.Height > 0) g.FillRectangle(Color.FromArgb(alfa, baseC.R, baseC.G, baseC.B), celda);
                }
                if (f.R.Top > reg.Top) g.DrawLine(penGrilla, reg.Left, f.R.Top, xMax, f.R.Top);
            }

            var audit = new StringBuilder();
            audit.Append("columna ").Append(wCol).Append(" px, fila ").Append(altoFila).Append(" px, velas visibles ").Append(n)
                 .Append(hasta == ult ? " (hasta la vela en curso)" : " (corrido hacia atras)");
            var escritos = new List<string>();
            if (nCajas == 0) audit.Append(" | SIN CUADROS: ").Append(fCaja == null ? "la fila es mas baja que la letra 7" : "no entra ni uno");
            else
            {
                g.FillRectangle(cFondo, new Rectangle(xCajas - 3, reg.Top, xMax - xCajas + 3, reg.Height));
                for (int k = 0; k < filas.Count; k++)
                {
                    var f = filas[k];
                    for (int j = 0; j < nCajas; j++)
                    {
                        int i = n - nCajas + j; double v = f.Val[i];
                        Color baseC = f.Tipo == Tipo.Volumen ? cVol : (v >= 0 ? cMas : cMenos);
                        int alfa = (int)(45 + 175 * Math.Sqrt(Math.Min(1.0, Math.Abs(v) / maximo[k])));
                        var caja = new Rectangle(xCajas + j * anchoCaja, f.R.Top + 1, anchoCaja - 2, altoFila - 2);
                        g.FillRectangle(Color.FromArgb(alfa, baseC.R, baseC.G, baseC.B), caja);
                        var sz = g.MeasureString(f.Txt[i], fCaja);
                        g.DrawString(f.Txt[i], fCaja, cTxt, caja.Left + (caja.Width - sz.Width) / 2, f.R.Top + (altoFila - altoCaja) / 2);
                        escritos.Add(string.Format(Inv, "{0} {1:HH:mm} cuadro {2} '{3}' crudo {4:0}", f.Tipo, hora[i], j + 1, f.Txt[i], v));
                    }
                }
                // la hora de cada cuadro (la del eje del grafico: hora de la PC) y la vela en curso con recuadro
                var cHora = Color.FromArgb(175, cTxt.R, cTxt.G, cTxt.B);
                for (int j = 0; j < nCajas; j++)
                {
                    int i = n - nCajas + j;
                    string hh = DateTime.SpecifyKind(hora[i], DateTimeKind.Utc).ToLocalTime().ToString("HH:mm", Inv);
                    var sz = g.MeasureString(hh, fHora);
                    g.DrawString(hh, fHora, cHora, xCajas + j * anchoCaja + (anchoCaja - 2 - sz.Width) / 2, reg.Top);
                }
                if (hasta == ult)
                    g.DrawRectangle(new RenderPen(Color.FromArgb(220, cTxt.R, cTxt.G, cTxt.B), 1), new Rectangle(xCajas + (nCajas - 1) * anchoCaja, reg.Top, anchoCaja - 3, reg.Height - 2));
                audit.Append(" | cuadros: ").Append(nCajas).Append(" de ").Append(Math.Max(1, EstCuantas)).Append(", ancho ").Append(anchoCaja)
                     .Append(" px, letra ").Append(letraCaja).Append(" (alto ").Append(altoCaja).Append(" px), desde x ").Append(xCajas)
                     .Append(" | sus velas en la tira desde x ").Append(x0[n - nCajas]).Append(" hasta ").Append(x0[n]);
            }

            // nombres de las filas, encima de todo, con fondo propio (como el de fabrica)
            if (EstNombres) g.FillRectangle(Color.FromArgb(235, cFondo.R, cFondo.G, cFondo.B), new Rectangle(reg.Left, reg.Top, anchoNom, altoCab));
            if (EstNombres)
                for (int k = 0; k < filas.Count; k++)
                {
                    var r = new Rectangle(reg.Left, filas[k].R.Top, anchoNom, altoFila);
                    g.FillRectangle(Color.FromArgb(235, cFondo.R, cFondo.G, cFondo.B), r);
                    int hN = g.MeasureString(nombres[k], fNom).Height;
                    g.DrawString(nombres[k], fNom, Color.FromArgb(200, cTxt.R, cTxt.G, cTxt.B), r.Left + 4, r.Top + (altoFila - hN) / 2);
                    if (k > 0) g.DrawLine(penGrilla, r.Left, r.Top, r.Right, r.Top);
                }

            if (EstRegistrar && (DateTime.UtcNow - _ultAudit).TotalSeconds >= 60)
            {
                _ultAudit = DateTime.UtcNow;
                Auditar(audit.ToString(), escritos);
            }
        }

        // ------------------------------------------------------------------ registro
        private static string Carpeta() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex", "estadistica");

        private void Auditar(string pantalla, List<string> escritos)
        {
            try
            {
                var sb = new StringBuilder();
                string inst = "?"; try { inst = InstrumentInfo?.Instrument ?? "?"; } catch { }
                string marco = "?"; try { marco = ChartInfo?.TimeFrame ?? "?"; } catch { }
                sb.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", Inv)).Append(" UTC v").Append(Version).Append(' ').Append(inst).Append(' ').Append(marco).Append('\n');
                sb.Append("  pantalla: ").Append(pantalla).Append('\n');
                foreach (var e in escritos) sb.Append("  escrito ").Append(e).Append('\n');
                Directory.CreateDirectory(Carpeta());
                File.AppendAllText(Path.Combine(Carpeta(), "auditoria-" + DateTime.UtcNow.ToString("yyyy-MM-dd", Inv) + ".log"), sb.ToString());
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
