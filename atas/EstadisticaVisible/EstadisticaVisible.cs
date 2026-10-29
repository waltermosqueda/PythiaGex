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
    /// ESTADISTICA VISIBLE 1.5 (29-10-2026). Pedido del operador: "tengo que hacer recontra zoom para que se vean los numeros
    /// del Cluster Statistic y ahi pierdo contexto del grafico"; despues: "que se vean siempre los ultimos sin zoom"; con la 1.3:
    /// "que persiga a las velas correctas" (eligio "cuadros bajo sus velas"); y con la 1.4: "cuando estiras o contraes los numeros
    /// cambian, desaparecen o se fusionan; debe ser integro siempre y corresponder a su vela siempre, sino despues malas decisiones".
    ///
    /// EL PROBLEMA, MEDIDO. Con su zoom cada vela mide 6-9 px y un numero legible (letra 9-11) pide 33-41 px. Un numero por vela
    /// debajo de su vela no entra (ni turnando renglones: 1.0-1.2), y cuadros de una vela no quedan debajo de su vela (1.3). La 1.4
    /// elegia cuantas velas juntar segun el zoom: al estirar el grafico cambiaban los cortes y los numeros. ERROR, corregido aca.
    ///
    /// LA SOLUCION. Todo el panel son CUADROS ALINEADOS, cada uno exactamente debajo de las velas que resume. El bloque es FIJO
    /// (EstBloque, 5 velas: con 1 min, :00-:04, :05-:09...) y se corta a la hora redonda: el zoom no cambia ni los cortes ni los
    /// numeros, y nada se junta. Delta y Volumen = suma exacta de sus velas; Delta de la sesion = el valor al cierre del bloque.
    /// El zoom solo cambia el tamaño de la letra; si un bloque queda mas angosto que su numero con letra 7, queda pintado sin
    /// numero (nunca con el de otro bloque). El bloque EN CURSO crece vela a vela, lleva recuadro y se estira hacia la derecha
    /// (el futuro, vacio) para que su numero siempre se lea.
    ///
    /// LOS DATOS. Los mismos que el de fabrica: GetCandle(bar).Delta y .Volume; la sesion se reinicia con IsNewSession (la
    /// sesion del grafico, el modo por defecto del Cluster Statistic). Numeros EXACTOS hasta 99.999 ("-2483", sin redondear).
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
        public const string Version = "1.5";
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

        [Display(Name = "Velas por cuadro (fijo: no cambia con el zoom)", GroupName = "Numeros", Order = 35)]
        [Range(1, 240)]
        public int EstBloque { get; set; } = 5;

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
        /// <summary>EXACTO hasta 99.999 (sin separador de miles: "-2483" ocupa lo mismo que "-2,5k" y no redondea); despues "k"
        /// entero y "M" con una decimal. Coma decimal.</summary>
        public static string Formato(double v)
        {
            double a = Math.Abs(v);
            string s;
            if (a < 99999.5) s = Math.Round(v).ToString("0", Inv);
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

        /// <summary>Junta las velas visibles [0, n) en bloques de k velas cortados a la hora redonda (clave = segundos desde 1970 /
        /// (k x duracion de la vela)). Los cortes dependen solo de la hora de cada vela: el zoom no los mueve y nada se junta.</summary>
        private static List<Cuadro> Agrupar(int k, int n, DateTime[] hora, double[] d, double[] v, double[] s, double segVela)
        {
            var res = new List<Cuadro>();
            long clavePrev = long.MinValue;
            for (int i = 0; i < n; i++)
            {
                long clave = (long)Math.Floor((hora[i] - DateTime.UnixEpoch).TotalSeconds / Math.Max(1.0, k * segVela));
                if (res.Count == 0 || clave != clavePrev) res.Add(new Cuadro { Ini = i, Fin = i });
                var c = res[res.Count - 1];
                c.Fin = i; c.Delta += d[i]; c.Volumen += v[i]; c.Sesion = s[i];
                clavePrev = clave;
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

            // CUADROS FIJOS (1.5, pedido 29-10: "cuando estiras o contraes los numeros cambian, desaparecen o se fusionan; debe ser
            // integro siempre y corresponder a su vela siempre"). Cada cuadro es SIEMPRE el mismo bloque de EstBloque velas cortado a
            // la hora redonda (con 5 velas de 1 min: :00-:04, :05-:09...): el zoom no cambia ni los cortes ni los numeros, nada se
            // junta. El zoom solo cambia el tamaño de la letra; si un cuadro queda mas angosto que su numero con letra 7, ese cuadro
            // queda pintado sin numero (nunca un numero de otro bloque). El bloque EN CURSO (el que contiene la vela que se esta
            // formando) crece vela a vela, lleva recuadro y se estira hacia la derecha (el futuro, vacio) para que su numero se lea.
            int k = Math.Max(1, EstBloque);
            var cuadros = Agrupar(k, n, hora, d, v, s, segVela);
            bool enCurso = hasta == ult;
            if (enCurso && cuadros.Count > 0) cuadros[cuadros.Count - 1].EnCurso = true;

            // letra: la mas grande (hasta EstLetraCajas) que entra en el alto de la fila y en el ancho de un bloque entero
            RenderFont f = null; int altoTxt = 0, letra = 0, anchoBloque = k * wCol - 1, anchoTexto = 0;
            for (int tam = Math.Max(7, EstLetraCajas); tam >= 7 && f == null; tam--)
            {
                var ft = new RenderFont("Segoe UI", tam); int h = g.MeasureString("0", ft).Height;
                if (h + 2 > altoFila) continue;
                int aMax = 0;
                foreach (var c in cuadros) if (!c.EnCurso) foreach (var t in tipos) aMax = Math.Max(aMax, g.MeasureString(Formato(c.Valor(t)), ft).Width);
                if (aMax + 6 <= anchoBloque || tam == 7) { f = ft; altoTxt = h; letra = tam; anchoTexto = aMax; }
            }
            if (f == null) { f = new RenderFont("Segoe UI", 7); altoTxt = g.MeasureString("0", f).Height; letra = 7; }

            // extension de cada cuadro en pantalla: el en curso se estira a la derecha hasta que entren sus numeros
            int[] xa = new int[cuadros.Count], xb = new int[cuadros.Count];
            for (int j = 0; j < cuadros.Count; j++)
            {
                var c = cuadros[j];
                xa[j] = x0[c.Ini]; xb[j] = x0[c.Fin + 1];
                if (c.EnCurso)
                {
                    int nec = 0; foreach (var t in tipos) nec = Math.Max(nec, g.MeasureString(Formato(c.Valor(t)), f).Width);
                    xb[j] = Math.Min(xMax, Math.Max(xb[j], xa[j] + nec + 10));
                }
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

            var audit = new StringBuilder();
            audit.Append("vela ").Append(wCol).Append(" px (").Append(segVela.ToString("0", Inv)).Append(" s), fila ").Append(altoFila)
                 .Append(" px, bloque fijo de ").Append(k).Append(" velas = ").Append(anchoBloque).Append(" px, letra ").Append(letra)
                 .Append(" (texto mas ancho ").Append(anchoTexto).Append(" px), ").Append(cuadros.Count).Append(" cuadros")
                 .Append(enCurso ? ", el ultimo en curso" : " (corrido hacia atras)");
            int sinNumero = 0;

            for (int r = 0; r < tipos.Count; r++)
            {
                var t = tipos[r]; int yF = reg.Top + r * altoFila;
                double mx = 1; foreach (var c in cuadros) if (!c.EnCurso) mx = Math.Max(mx, Math.Abs(c.Valor(t)));
                if (r > 0) g.DrawLine(penGrilla, reg.Left, yF, xMax, yF);
                for (int j = 0; j < cuadros.Count; j++)
                {
                    var c = cuadros[j];
                    int a = Math.Max(xa[j], reg.Left), b = Math.Min(xb[j], xMax);
                    if (b - a < 2) continue;
                    double val = c.Valor(t);
                    Color baseC = t == Tipo.Volumen ? cVol : (val >= 0 ? cMas : cMenos);
                    int alfa = (int)(40 + 180 * Math.Sqrt(Math.Min(1.0, Math.Abs(val) / mx)));
                    var caja = new Rectangle(a, yF + 1, b - a - 1, altoFila - 2);
                    g.FillRectangle(Color.FromArgb(alfa, baseC.R, baseC.G, baseC.B), caja);
                    string txt = Formato(val); var sz = g.MeasureString(txt, f);
                    bool entra = sz.Width + 4 <= caja.Width && caja.Left + (caja.Width - sz.Width) / 2 >= reg.Left + anchoNom;
                    if (entra) g.DrawString(txt, f, cTxt, caja.Left + (caja.Width - sz.Width) / 2, yF + (altoFila - altoTxt) / 2);
                    else if (caja.Left >= reg.Left + anchoNom && x0[c.Ini] >= reg.Left) sinNumero++;
                }
            }
            if (enCurso && cuadros.Count > 0)
            {
                int j = cuadros.Count - 1;
                g.DrawRectangle(new RenderPen(Color.FromArgb(220, cTxt.R, cTxt.G, cTxt.B), 1), new Rectangle(xa[j], reg.Top + 1, xb[j] - xa[j] - 2, tipos.Count * altoFila - 3));
            }
            audit.Append(", cuadros enteros sin numero (no entra ni con letra 7): ").Append(sinNumero);

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
                foreach (var c in cuadros.Skip(Math.Max(0, cuadros.Count - 6)))
                {
                    lineas.Add(string.Format(Inv, "cuadro {0} ({1} velas{2}) x {3}-{4}: Delta '{5}' crudo {6:0} | Sesion '{7}' crudo {8:0} | Volumen '{9}' crudo {10:0}",
                        HoraLocal(hora[c.Ini]), c.Fin - c.Ini + 1, c.EnCurso ? ", EN CURSO" : "", x0[c.Ini], x0[c.Fin + 1],
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
