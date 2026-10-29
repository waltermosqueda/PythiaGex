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
    /// ESTADISTICA VISIBLE 1.0 (28-10-2026). Pedido del operador: "tengo que hacer recontra zoom para que se vean los numeros
    /// del Cluster Statistic y ahi pierdo contexto del grafico".
    ///
    /// EL PROBLEMA, MEDIDO. El Cluster Statistic de fabrica escribe el numero solo si entra en el ancho de la columna de la vela.
    /// Con su zoom de siempre cada columna mide 17 px y "-245" pide ~24: ATAS lo oculta. De sus 72 ajustes ninguno lo fuerza.
    ///
    /// LA SOLUCION. Las mismas filas (Delta, Delta de la sesion, Volumen), pero si el numero no entra en una columna, las velas
    /// se turnan en RENGLONES ALTERNADOS: las pares escriben arriba y las impares abajo (o en tres renglones si hace falta), asi
    /// cada numero usa el ancho de dos o tres columnas y se lee entero con letra normal, sin tocar el zoom. El turno sale del
    /// numero ABSOLUTO de la vela: al correr el grafico un numero no salta de renglon. Si ni asi entra, achica la letra hasta 7;
    /// si tampoco, deja las celdas pintadas sin numero (como ATAS) y lo anota en el registro.
    ///
    /// LOS DATOS. Los mismos que el de fabrica: GetCandle(bar).Delta y .Volume; la sesion se reinicia con IsNewSession (la
    /// sesion del grafico, el modo por defecto del Cluster Statistic). Los miles van con una decimal ("-1,4k"); el de fabrica
    /// redondea a "-1K".
    ///
    /// AUDITORIA. Cada 60 s (y al arrancar) anota en %APPDATA%\ATAS\PythiaGex\estadistica\auditoria-AAAA-MM-DD.log el ancho de
    /// columna, la letra y los renglones de cada fila, y los valores crudos de las ultimas 15 velas, para compararlos contra la
    /// pantalla y contra el Cluster Statistic de fabrica.
    ///
    /// LOS AJUSTES LLEVAN EL PREFIJO "Est": ATAS guarda los ajustes POR NOMBRE en el workspace (.ws).
    /// </summary>
    [DisplayName("PythiaGex - Estadistica Visible")]
    [Category("PythiaGex")]
    public class EstadisticaVisibleIndicador : Indicator
    {
        public const string Version = "1.0";
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

        [Display(Name = "Nombres de las filas", GroupName = "Vista", Order = 40)]
        public bool EstNombres { get; set; } = true;

        [Display(Name = "Tamano de letra", GroupName = "Vista", Order = 50)]
        [Range(7, 14)]
        public int EstLetra { get; set; } = 9;

        [Display(Name = "Renglones como maximo", GroupName = "Vista", Order = 60)]
        [Range(1, 3)]
        public int EstRenglones { get; set; } = 3;

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

        private void Pintar(RenderContext g)
        {
            var cont = ChartInfo?.PriceChartContainer; var mio = Container;
            if (cont == null || mio == null) return;
            var reg = mio.Region; if (reg.Width < 40 || reg.Height < 12) return;
            var filas = new List<Tipo>();
            if (_fDelta) filas.Add(Tipo.Delta); if (_fSesion) filas.Add(Tipo.Sesion); if (_fVolumen) filas.Add(Tipo.Volumen);
            if (filas.Count == 0) return;

            int ult = CurrentBar - 1; if (ult < 0) return;
            int desde = Math.Max(0, FirstVisibleBarNumber), hasta = Math.Min(ult, LastVisibleBarNumber);
            if (hasta < desde) return;
            int n = hasta - desde + 1;

            double[][] val = new double[filas.Count][];
            DateTime[] hora = new DateTime[n];
            lock (_llave)
            {
                if (_delta.Count <= hasta) return;
                for (int k = 0; k < filas.Count; k++)
                {
                    var src = filas[k] == Tipo.Delta ? _delta : filas[k] == Tipo.Sesion ? _ses : _vol;
                    val[k] = new double[n];
                    for (int i = 0; i < n; i++) val[k][i] = src[desde + i];
                }
                for (int i = 0; i < n; i++) hora[i] = _hora[desde + i];
            }

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
            int altoFila = reg.Height / filas.Count;
            var penGrilla = new RenderPen(cGrilla, 1);
            var audit = new StringBuilder();
            audit.Append("columna ").Append(wCol).Append(" px, velas ").Append(desde).Append("-").Append(hasta).Append(", fila ").Append(altoFila).Append(" px");

            // nombres: se miden antes para no escribir numeros debajo
            var fNom = new RenderFont("Segoe UI", Math.Max(7, EstLetra - 1));
            int anchoNom = 0;
            string[] nombres = filas.Select(t => t == Tipo.Delta ? "Delta" : t == Tipo.Sesion ? "Delta sesion" : "Volumen").ToArray();
            if (EstNombres) foreach (var s in nombres) anchoNom = Math.Max(anchoNom, g.MeasureString(s, fNom).Width + 8);

            for (int k = 0; k < filas.Count; k++)
            {
                var r = new Rectangle(reg.Left, reg.Top + k * altoFila, reg.Width, altoFila);
                double mx = 0; foreach (var v in val[k]) mx = Math.Max(mx, Math.Abs(v));
                if (mx <= 0) mx = 1;

                // celdas pintadas: intensidad por la raiz del tamaño relativo a lo visible
                for (int i = 0; i < n; i++)
                {
                    int xa = x0[i], xb = x0[i + 1]; if (xb <= reg.Left || xa >= xMax) continue;
                    double v = val[k][i];
                    Color baseC = filas[k] == Tipo.Volumen ? cVol : (v >= 0 ? cMas : cMenos);
                    int alfa = (int)(30 + 190 * Math.Sqrt(Math.Min(1.0, Math.Abs(v) / mx)));
                    int gap = (xb - xa) >= 5 ? 1 : 0;
                    var celda = new Rectangle(Math.Max(xa, reg.Left), r.Top + 1, Math.Min(xb, xMax) - Math.Max(xa, reg.Left) - gap, altoFila - 2);
                    if (celda.Width > 0 && celda.Height > 0) g.FillRectangle(Color.FromArgb(alfa, baseC.R, baseC.G, baseC.B), celda);
                }
                if (k > 0) g.DrawLine(penGrilla, reg.Left, r.Top, xMax, r.Top);

                // numeros. 1) busca la letra mas grande con la que entran TODOS turnando renglones (la vela par arriba, la impar
                // abajo: el turno sale del numero absoluto de la vela y no salta al correr el grafico). 2) Si no entran todos ni
                // con letra 7, llena los renglones que caben por importancia: primero la vela en curso, despues las de numero
                // mas grande, cada una en el primer renglon donde no toca a otra. Nunca se pisan dos numeros.
                string[] txt = new string[n]; for (int i = 0; i < n; i++) txt[i] = Formato(val[k][i]);
                int renglones = 0, letra = 0; RenderFont f = null; int altoTxt = 0, anchoMax = 0;
                for (int tam = Math.Max(7, EstLetra); tam >= 7 && renglones == 0; tam--)
                {
                    var ft = new RenderFont("Segoe UI", tam);
                    int aMax = 0; foreach (var s in txt) aMax = Math.Max(aMax, g.MeasureString(s, ft).Width);
                    int hTxt = g.MeasureString("0", ft).Height;
                    int posibles = Math.Max(0, Math.Min(Math.Max(1, EstRenglones), altoFila / Math.Max(1, hTxt)));
                    int hacen = (int)Math.Ceiling((aMax + 4.0) / Math.Max(1, wCol));
                    if (hacen >= 1 && hacen <= posibles) { renglones = hacen; f = ft; altoTxt = hTxt; anchoMax = aMax; letra = tam; }
                }
                bool todos = renglones > 0;
                if (!todos)   // no entran todos: la letra (8 o 7) que deje mas renglones en el alto de la fila
                    for (int tam = Math.Max(7, Math.Min(EstLetra, 8)); tam >= 7; tam--)
                    {
                        var ft = new RenderFont("Segoe UI", tam); int hTxt = g.MeasureString("0", ft).Height;
                        int p = Math.Min(Math.Max(1, EstRenglones), altoFila / Math.Max(1, hTxt));
                        if (p > renglones) { renglones = p; f = ft; altoTxt = hTxt; letra = tam; }
                    }
                audit.Append(" | ").Append(nombres[k]).Append(": ");
                if (renglones == 0) { audit.Append("SIN NUMEROS (la fila es mas baja que la letra 7: ").Append(g.MeasureString("0", new RenderFont("Segoe UI", 7)).Height).Append(" px)"); continue; }

                int yBase = r.Top + (altoFila - renglones * altoTxt) / 2;
                int xMinTxt = reg.Left + (EstNombres ? anchoNom : 0);
                var orden = Enumerable.Range(0, n).ToList();
                if (!todos) orden = orden.OrderByDescending(i => desde + i == ult ? double.MaxValue : Math.Abs(val[k][i])).ToList();
                var ocupado = new List<(int a, int b)>[renglones]; for (int j = 0; j < renglones; j++) ocupado[j] = new List<(int, int)>();
                int puestos = 0;
                foreach (int i in orden)
                {
                    int b = desde + i;
                    int xc = (x0[i] + x0[i + 1]) / 2;
                    var sz = g.MeasureString(txt[i], f);
                    int xt = xc - sz.Width / 2;
                    if (xt < xMinTxt || xt + sz.Width > xMax) continue;   // ni debajo de los nombres ni cortado por el borde
                    int linea = -1;
                    if (todos) linea = b % renglones;
                    else
                        for (int j = 0; j < renglones && linea < 0; j++)
                            if (!ocupado[j].Any(o => xt < o.b + 3 && xt + sz.Width + 3 > o.a)) linea = j;
                    if (linea < 0) continue;
                    ocupado[linea].Add((xt, xt + sz.Width));
                    g.DrawString(txt[i], f, cTxt, xt, yBase + linea * altoTxt);
                    puestos++;
                }
                audit.Append(todos ? "TODOS" : "por importancia").Append(", letra ").Append(letra).Append(" (alto ").Append(altoTxt).Append(" px)").Append(", ").Append(renglones)
                     .Append(" renglon(es), ").Append(puestos).Append(" de ").Append(n).Append(" numeros");
            }

            // nombres de las filas, encima de todo, con fondo propio (como el de fabrica)
            if (EstNombres)
                for (int k = 0; k < filas.Count; k++)
                {
                    var r = new Rectangle(reg.Left, reg.Top + k * altoFila, anchoNom, altoFila);
                    g.FillRectangle(Color.FromArgb(235, cFondo.R, cFondo.G, cFondo.B), r);
                    int hN = g.MeasureString(nombres[k], fNom).Height;
                    g.DrawString(nombres[k], fNom, Color.FromArgb(200, cTxt.R, cTxt.G, cTxt.B), r.Left + 4, r.Top + (altoFila - hN) / 2);
                    if (k > 0) g.DrawLine(penGrilla, r.Left, r.Top, r.Right, r.Top);
                }

            if (EstRegistrar && (DateTime.UtcNow - _ultAudit).TotalSeconds >= 60)
            {
                _ultAudit = DateTime.UtcNow;
                Auditar(audit.ToString(), filas, val, hora, n);
            }
        }

        // ------------------------------------------------------------------ registro
        private static string Carpeta() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex", "estadistica");

        private void Auditar(string pantalla, List<Tipo> filas, double[][] val, DateTime[] hora, int n)
        {
            try
            {
                var sb = new StringBuilder();
                string inst = "?"; try { inst = InstrumentInfo?.Instrument ?? "?"; } catch { }
                string marco = "?"; try { marco = ChartInfo?.TimeFrame ?? "?"; } catch { }
                sb.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", Inv)).Append(" UTC v").Append(Version).Append(' ').Append(inst).Append(' ').Append(marco).Append('\n');
                sb.Append("  pantalla: ").Append(pantalla).Append('\n');
                for (int i = Math.Max(0, n - 15); i < n; i++)
                {
                    sb.Append("  vela ").Append(hora[i].ToString("HH:mm", Inv)).Append(" UTC");
                    for (int k = 0; k < filas.Count; k++) sb.Append("  ").Append(filas[k]).Append(' ').Append(val[k][i].ToString("0", Inv)).Append(" (").Append(Formato(val[k][i])).Append(')');
                    sb.Append('\n');
                }
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
