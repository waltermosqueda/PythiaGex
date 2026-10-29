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
    /// ESTADISTICA VISIBLE 1.4 (28-10-2026). Pedido del operador: "tengo que hacer recontra zoom para que se vean los numeros
    /// del Cluster Statistic y ahi pierdo contexto del grafico"; despues: "que se vean siempre los ultimos sin zoom" y, con la 1.3
    /// en pantalla: "esta mal partido, saca eso que esta vacio, se profesional y que persiga a las velas correctas". Eligio
    /// (pregunta del 28-10 23:58) "cuadros bajo sus velas".
    ///
    /// EL PROBLEMA, MEDIDO. Con su zoom cada vela mide 6-9 px y un numero legible (letra 10-11) pide ~40 px. Un numero por vela
    /// debajo de su vela no entra (ni turnando renglones: 1.0-1.2), y cuadros de una vela no quedan debajo de su vela (1.3).
    ///
    /// LA SOLUCION. Todo el panel son CUADROS ALINEADOS: cada uno ocupa exactamente el ancho de las velas que resume y queda debajo
    /// de ellas, asi el eje de tiempo del grafico vale para los cuadros. Cuantas velas por cuadro: las minimas para que el numero
    /// entre (1, 2, 3, 5, 6, 10, 15...), y los cortes van a la hora redonda (con 6 velas de 1 min: 23:30, 23:36, 23:42...), asi no
    /// saltan cuando entra una vela nueva. Delta y Volumen = suma de sus velas; Delta de la sesion = el valor al cierre del cuadro.
    /// La vela EN CURSO va sola, en su propio cuadro con recuadro, pegada a su derecha (en el lugar vacio del futuro). Si el cuadro
    /// que se esta llenando todavia es angosto, se junta con el anterior. Con zoom grande vuelve solo a una vela por cuadro.
    ///
    /// LOS DATOS. Los mismos que el de fabrica: GetCandle(bar).Delta y .Volume; la sesion se reinicia con IsNewSession (la
    /// sesion del grafico, el modo por defecto del Cluster Statistic). Los miles van con una decimal ("-1,4k").
    ///
    /// AUDITORIA. Cada 60 s anota en %APPDATA%\ATAS\PythiaGex\estadistica\auditoria-AAAA-MM-DD.log: ancho de vela, velas por cuadro,
    /// letra, cada cuadro (hora, cuantas velas, texto y suma cruda) y las velas sueltas de los ultimos cuadros para rehacer la suma.
    ///
    /// LOS AJUSTES LLEVAN EL PREFIJO "Est": ATAS guarda los ajustes POR NOMBRE en el workspace (.ws).
    /// </summary>
    [DisplayName("PythiaGex - Estadistica Visible")]
    [Category("PythiaGex")]
    public class EstadisticaVisibleIndicador : Indicator
    {
        public const string Version = "1.4";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly int[] Agrupaciones = { 1, 2, 3, 5, 6, 10, 15, 20, 30, 60, 120, 240 };

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

        [Display(Name = "Velas por cuadro (0 = automatico)", GroupName = "Numeros", Order = 35)]
        [Range(0, 240)]
        public int EstVelasPorCuadro { get; set; } = 0;

        [Display(Name = "Tamano de letra de los cuadros", GroupName = "Numeros", Order = 36)]
        [Range(7, 16)]
        public int EstLetraCajas { get; set; } = 11;

        [Display(Name = "Nombres de las filas", GroupName = "Vista", Order = 40)]
        public bool EstNombres { get; set; } = true;

        [Display(Name = "Tamano de letra de los nombres", GroupName = "Vista", Order = 50)]
        [Range(7, 14)]
        public int EstLetra { get; set; } = 9;

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
        private static string HoraLocal(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm", Inv);

        /// <summary>Un cuadro: velas [Ini, Fin] de lo visible (indices locales) y sus tres numeros.</summary>
        private sealed class Cuadro
        {
            public int Ini, Fin; public double Delta, Volumen, Sesion; public bool EnCurso;
            public double Valor(Tipo t) => t == Tipo.Delta ? Delta : t == Tipo.Volumen ? Volumen : Sesion;
        }

        /// <summary>Junta las velas cerradas visibles [0, nCerradas) en cuadros de k velas cortados a la hora redonda (clave = segundos
        /// desde 1970 / (k x duracion de la vela)). El ultimo cuadro, si es el que se esta llenando y queda mas angosto que
        /// <paramref name="anchoMin"/>, se junta con el anterior.</summary>
        private static List<Cuadro> Agrupar(int k, int nCerradas, DateTime[] hora, double[] d, double[] v, double[] s, int[] x0, double segVela, int anchoMin, bool llenandose)
        {
            var res = new List<Cuadro>();
            long clavePrev = long.MinValue;
            for (int i = 0; i < nCerradas; i++)
            {
                long clave = (long)Math.Floor((hora[i] - DateTime.UnixEpoch).TotalSeconds / Math.Max(1.0, k * segVela));
                if (res.Count == 0 || clave != clavePrev) res.Add(new Cuadro { Ini = i, Fin = i });
                var c = res[res.Count - 1];
                c.Fin = i; c.Delta += d[i]; c.Volumen += v[i]; c.Sesion = s[i];
                clavePrev = clave;
            }
            if (llenandose && res.Count >= 2)
            {
                var u = res[res.Count - 1];
                if (x0[u.Fin + 1] - x0[u.Ini] < anchoMin)
                {
                    var p = res[res.Count - 2];
                    p.Fin = u.Fin; p.Delta += u.Delta; p.Volumen += u.Volumen; p.Sesion = u.Sesion;
                    res.RemoveAt(res.Count - 1);
                }
            }
            return res;
        }

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
            var tipos = new List<Tipo>();
            if (_fDelta) tipos.Add(Tipo.Delta); if (_fSesion) tipos.Add(Tipo.Sesion); if (_fVolumen) tipos.Add(Tipo.Volumen);
            if (tipos.Count == 0) return;

            int ult = CurrentBar - 1; if (ult < 0) return;
            int desde = Math.Max(0, FirstVisibleBarNumber), hasta = Math.Min(ult, LastVisibleBarNumber);
            if (hasta < desde) return;
            int n = hasta - desde + 1;
            double[] d = new double[n], v = new double[n], s = new double[n]; DateTime[] hora = new DateTime[n];
            lock (_llave)
            {
                if (_delta.Count <= hasta) return;
                for (int i = 0; i < n; i++) { d[i] = _delta[desde + i]; v[i] = _vol[desde + i]; s[i] = _ses[desde + i]; hora[i] = _hora[desde + i]; }
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
            var difs = new List<double>(); for (int i = 1; i < n; i++) { double t = (hora[i] - hora[i - 1]).TotalSeconds; if (t > 0) difs.Add(t); }
            difs.Sort(); double segVela = difs.Count > 0 ? difs[difs.Count / 2] : 60;

            int xMax = reg.Right;
            try { var cb = g.ClipBounds; if (cb.Width > 0) xMax = Math.Min(xMax, cb.Right); } catch { }

            Color cMas = De(EstColorMas), cMenos = De(EstColorMenos), cVol = De(EstColorVolumen), cTxt = De(EstColorTexto);
            Color cFondo = Color.FromArgb(255, 19, 23, 34), cGrilla = Color.FromArgb(255, 42, 46, 57);
            var penGrilla = new RenderPen(cGrilla, 1);
            int altoFila = reg.Height / tipos.Count;

            // letra de los cuadros: la mas grande que entra en el alto de la fila
            RenderFont f = null; int altoTxt = 0, letra = 0;
            for (int tam = Math.Max(7, EstLetraCajas); tam >= 7 && f == null; tam--)
            {
                var ft = new RenderFont("Segoe UI", tam); int h = g.MeasureString("0", ft).Height;
                if (h + 2 <= altoFila) { f = ft; altoTxt = h; letra = tam; }
            }
            if (f == null) { f = new RenderFont("Segoe UI", 7); altoTxt = g.MeasureString("0", f).Height; letra = 7; }

            bool enCurso = hasta == ult;
            int nCerradas = enCurso ? n - 1 : n;
            int Ancho(Cuadro c) { int a = 0; foreach (var t in tipos) a = Math.Max(a, g.MeasureString(Formato(c.Valor(t)), f).Width); return a + 8; }

            // velas por cuadro: la menor agrupacion con la que el numero entra en el ancho de un cuadro entero
            int k = 1; List<Cuadro> cuadros = null; int anchoNec = 0;
            var opciones = EstVelasPorCuadro > 0 ? new[] { EstVelasPorCuadro } : Agrupaciones;
            foreach (int kk in opciones)
            {
                var prueba = Agrupar(kk, nCerradas, hora, d, v, s, x0, segVela, 0, false);
                int nec = 0; foreach (var c in prueba) nec = Math.Max(nec, Ancho(c));
                k = kk; cuadros = prueba; anchoNec = nec;
                if (kk * wCol - 1 >= nec) break;
            }
            cuadros = Agrupar(k, nCerradas, hora, d, v, s, x0, segVela, anchoNec, enCurso);

            // la vela en curso: su propio cuadro, desde su borde izquierdo hacia el futuro (a la derecha)
            Cuadro actual = null; int xAct = xMax, anchoAct = 0;
            if (enCurso)
            {
                actual = new Cuadro { Ini = n - 1, Fin = n - 1, Delta = d[n - 1], Volumen = v[n - 1], Sesion = s[n - 1], EnCurso = true };
                anchoAct = Math.Max(Ancho(actual) + 2, x0[n] - x0[n - 1]);
                xAct = Math.Min(x0[n - 1], xMax - anchoAct);
            }

            var fNom = new RenderFont("Segoe UI", Math.Max(7, EstLetra - 1));
            var fNota = new RenderFont("Segoe UI", 7);
            string[] nombres = tipos.Select(t => t == Tipo.Delta ? "Delta" : t == Tipo.Sesion ? "Delta sesion" : "Volumen").ToArray();
            string nota = k > 1 ? "cuadro = " + k + " velas" : "";
            int anchoNom = 0;
            if (EstNombres)
            {
                foreach (var nm in nombres) anchoNom = Math.Max(anchoNom, g.MeasureString(nm, fNom).Width + 8);
                if (nota.Length > 0) anchoNom = Math.Max(anchoNom, g.MeasureString(nota, fNota).Width + 8);
            }

            // maximos por fila para la intensidad del color: los cuadros contra los cuadros; la vela en curso contra las velas sueltas
            var audit = new StringBuilder();
            audit.Append("vela ").Append(wCol).Append(" px (").Append(segVela.ToString("0", Inv)).Append(" s), fila ").Append(altoFila)
                 .Append(" px, letra ").Append(letra).Append(", ").Append(k).Append(" velas por cuadro (necesita ").Append(anchoNec).Append(" px), ")
                 .Append(cuadros.Count).Append(" cuadros").Append(enCurso ? " + la vela en curso desde x " + xAct : " (corrido hacia atras)");

            for (int r = 0; r < tipos.Count; r++)
            {
                var t = tipos[r]; int yF = reg.Top + r * altoFila;
                double mxC = 1, mxV = 1;
                foreach (var c in cuadros) mxC = Math.Max(mxC, Math.Abs(c.Valor(t)));
                for (int i = 0; i < nCerradas; i++) mxV = Math.Max(mxV, Math.Abs(t == Tipo.Delta ? d[i] : t == Tipo.Volumen ? v[i] : s[i]));
                if (t == Tipo.Sesion) mxV = mxC;
                if (r > 0) g.DrawLine(penGrilla, reg.Left, yF, xMax, yF);

                var todos = new List<Cuadro>(cuadros); if (actual != null) todos.Add(actual);
                foreach (var c in todos)
                {
                    int xa = c.EnCurso ? xAct : x0[c.Ini], xb = c.EnCurso ? xAct + anchoAct : x0[c.Fin + 1];
                    if (!c.EnCurso && actual != null) xb = Math.Min(xb, xAct - 1);
                    xa = Math.Max(xa, reg.Left); xb = Math.Min(xb, xMax);
                    if (xb - xa < 2) continue;
                    double val = c.Valor(t);
                    Color baseC = t == Tipo.Volumen ? cVol : (val >= 0 ? cMas : cMenos);
                    int alfa = (int)(40 + 180 * Math.Sqrt(Math.Min(1.0, Math.Abs(val) / (c.EnCurso ? mxV : mxC))));
                    var caja = new Rectangle(xa, yF + 1, xb - xa - 1, altoFila - 2);
                    g.FillRectangle(Color.FromArgb(alfa, baseC.R, baseC.G, baseC.B), caja);
                    string txt = Formato(val); var sz = g.MeasureString(txt, f);
                    if (sz.Width + 4 <= caja.Width && caja.Left + (caja.Width - sz.Width) / 2 >= reg.Left + anchoNom)
                        g.DrawString(txt, f, cTxt, caja.Left + (caja.Width - sz.Width) / 2, yF + (altoFila - altoTxt) / 2);
                }
            }
            if (actual != null)
                g.DrawRectangle(new RenderPen(Color.FromArgb(220, cTxt.R, cTxt.G, cTxt.B), 1), new Rectangle(xAct, reg.Top + 1, anchoAct - 2, tipos.Count * altoFila - 3));

            // nombres de las filas, encima de todo, con fondo propio; debajo de "Delta", cuantas velas junta cada cuadro
            if (EstNombres)
                for (int r = 0; r < tipos.Count; r++)
                {
                    var rr = new Rectangle(reg.Left, reg.Top + r * altoFila, anchoNom, altoFila);
                    g.FillRectangle(Color.FromArgb(240, cFondo.R, cFondo.G, cFondo.B), rr);
                    int hN = g.MeasureString(nombres[r], fNom).Height;
                    bool conNota = r == 0 && nota.Length > 0;
                    int hNota = conNota ? g.MeasureString(nota, fNota).Height : 0;
                    int y0 = rr.Top + (altoFila - hN - hNota) / 2;
                    g.DrawString(nombres[r], fNom, Color.FromArgb(210, cTxt.R, cTxt.G, cTxt.B), rr.Left + 4, y0);
                    if (conNota) g.DrawString(nota, fNota, Color.FromArgb(150, cTxt.R, cTxt.G, cTxt.B), rr.Left + 4, y0 + hN);
                    if (r > 0) g.DrawLine(penGrilla, rr.Left, rr.Top, rr.Right, rr.Top);
                }

            if (EstRegistrar && (DateTime.UtcNow - _ultAudit).TotalSeconds >= 60)
            {
                _ultAudit = DateTime.UtcNow;
                var lineas = new List<string>();
                var todos = new List<Cuadro>(cuadros); if (actual != null) todos.Add(actual);
                foreach (var c in todos.Skip(Math.Max(0, todos.Count - 6)))
                {
                    lineas.Add(string.Format(Inv, "cuadro {0} ({1} velas{2}) x {3}-{4}: Delta '{5}' crudo {6:0} | Sesion '{7}' crudo {8:0} | Volumen '{9}' crudo {10:0}",
                        HoraLocal(hora[c.Ini]), c.Fin - c.Ini + 1, c.EnCurso ? ", EN CURSO" : "", c.EnCurso ? xAct : x0[c.Ini], c.EnCurso ? xAct + anchoAct : x0[c.Fin + 1],
                        Formato(c.Delta), c.Delta, Formato(c.Sesion), c.Sesion, Formato(c.Volumen), c.Volumen));
                    for (int i = c.Ini; i <= c.Fin; i++)
                        lineas.Add(string.Format(Inv, "    vela {0} x {1}: delta {2:0} sesion {3:0} volumen {4:0}", HoraLocal(hora[i]), x0[i], d[i], s[i], v[i]));
                }
                Auditar(audit.ToString(), lineas);
            }
        }

        // ------------------------------------------------------------------ registro
        private static string Carpeta() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex", "estadistica");

        private void Auditar(string pantalla, List<string> lineas)
        {
            try
            {
                var sb = new StringBuilder();
                string inst = "?"; try { inst = InstrumentInfo?.Instrument ?? "?"; } catch { }
                string marco = "?"; try { marco = ChartInfo?.TimeFrame ?? "?"; } catch { }
                sb.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", Inv)).Append(" UTC v").Append(Version).Append(' ').Append(inst).Append(' ').Append(marco).Append('\n');
                sb.Append("  pantalla: ").Append(pantalla).Append('\n');
                foreach (var e in lineas) sb.Append("  ").Append(e).Append('\n');
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
