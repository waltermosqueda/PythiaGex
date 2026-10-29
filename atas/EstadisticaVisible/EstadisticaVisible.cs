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
    /// ESTADISTICA VISIBLE 1.8 (29-10-2026). Pedido del operador: "tengo que hacer recontra zoom para que se vean los numeros
    /// del Cluster Statistic y ahi pierdo contexto del grafico"; despues: "que se vean siempre los ultimos sin zoom"; con la 1.3:
    /// "que persiga a las velas correctas" (eligio "cuadros bajo sus velas"); y con la 1.4: "cuando estiras o contraes los numeros
    /// cambian, desaparecen o se fusionan; debe ser integro siempre y corresponder a su vela siempre, sino despues malas decisiones".
    ///
    /// EL PROBLEMA, MEDIDO. Con su zoom cada vela mide 6-9 px y un numero legible (letra 9-11) pide 33-41 px. Un numero por vela
    /// debajo de su vela no entra (ni turnando renglones: 1.0-1.2), y cuadros de una vela no quedan debajo de su vela (1.3). La 1.4
    /// elegia cuantas velas juntar segun el zoom: al estirar el grafico cambiaban los cortes y los numeros. ERROR, corregido aca.
    ///
    /// LA SOLUCION (1.8). Todo el panel son CUADROS ALINEADOS, cada uno exactamente debajo de las velas que resume, con su numero
    /// SIEMPRE ADENTRO (1.6-1.7 escalonaba y los numeros se salian del cuadro: "imaginate mañana con mas volumen"). El bloque es
    /// FIJO (EstVelasCuadro, 4 velas, cortado a la hora redonda): el zoom no cambia ni los cortes ni los numeros, nada se junta,
    /// solo cambia el tamaño de la letra. Delta y Volumen = suma de sus velas; Delta de la sesion = el valor al cierre del bloque.
    /// El bloque EN CURSO crece vela a vela, lleva recuadro y se estira hacia la derecha (el futuro, vacio) hasta que entra su numero.
    ///
    /// LOS DATOS. Los mismos que el de fabrica: GetCandle(bar).Delta y .Volume; la sesion se reinicia con IsNewSession (la
    /// sesion del grafico, el modo por defecto del Cluster Statistic). Delta exacto hasta 999 y despues "-1,2k" (siempre igual,
    /// no segun el zoom); Delta de la sesion en miles ("-2,3"); Volumen exacto hasta 999 y despues "1,2k".
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
        public const string Version = "1.8b";
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

        // nombre nuevo en la 1.8b (EstVelasCuadro, y queda este): el .ws guarda EstVelasPorBloque = 2 de la 1.6-1.7 y le ganaria al
        // default. 4 velas: con su zoom mas alejado (7 px por vela) el cuadro da 28 px y entra un numero de 4-5 caracteres adentro.
        [Display(Name = "Velas por cuadro (fijo: no cambia con el zoom)", GroupName = "Numeros", Order = 35)]
        [Range(1, 240)]
        public int EstVelasCuadro { get; set; } = 4;

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

        /// <summary>Delta EXACTO (Formato); Delta de la sesion en MILES con una decimal ("-2,3" = -2.300; 100 mil o mas, sin decimal);
        /// Volumen exacto hasta 999 y despues abreviado ("1,2k"; desde 100k, "123k"). Coma decimal. No dependen del zoom.</summary>
        private static string FormatoFila(Tipo t, double v)
        {
            if (t == Tipo.Delta) { if (Math.Abs(v) < 999.5) return Math.Round(v).ToString("0", Inv); double kd = v / 1000.0; return kd.ToString(Math.Abs(kd) < 99.95 ? "0.0" : "0", Inv).Replace('.', ',') + "k"; }
            if (t == Tipo.Sesion) { double m = v / 1000.0; return m.ToString(Math.Abs(m) < 99.95 ? "0.0" : "0", Inv).Replace('.', ','); }
            if (Math.Abs(v) < 999.5) return Math.Round(v).ToString("0", Inv);
            double kk = v / 1000.0; return kk.ToString(Math.Abs(kk) < 99.95 ? "0.0" : "0", Inv).Replace('.', ',') + "k";
        }

        private static Color De(System.Windows.Media.Color c) => Color.FromArgb(c.A, c.R, c.G, c.B);
        private static string HoraLocal(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm", Inv);

        /// <summary>Un cuadro: velas [Ini, Fin] de lo visible (indices locales) y sus tres numeros.</summary>
        private sealed class Cuadro
        {
            public int Ini, Fin; public long Clave; public double Delta, Volumen, Sesion; public bool EnCurso;
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
                if (res.Count == 0 || clave != clavePrev) res.Add(new Cuadro { Ini = i, Fin = i, Clave = clave });
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
            // FILAS (1.7, pedido 29-10: "el delta sesion y volumen no me importa mucho, podes abreviarlo; el delta si es el importante").
            // El Delta va EXACTO, en la fila mas alta (la mitad del panel) y con la letra mas grande. El Delta de la sesion va en MILES
            // con una decimal ("-2,3" = -2.300) y el Volumen abreviado desde mil ("1,2k"), en filas bajas con letra chica.
            int nFil = tipos.Count;
            int[] altoF = new int[nFil], topF = new int[nFil];
            {
                double pesoTot = tipos.Sum(t => t == Tipo.Delta ? 2.0 : 1.0); int y = reg.Top;
                for (int r = 0; r < nFil; r++)
                {
                    altoF[r] = r == nFil - 1 ? reg.Bottom - y : (int)Math.Round(reg.Height * (tipos[r] == Tipo.Delta ? 2.0 : 1.0) / pesoTot);
                    topF[r] = y; y += altoF[r];
                }
            }

            // CUADROS FIJOS (1.8, pedido 29-10: "se salen los numeros de los cuadros, imaginate mañana con mas volumen"). Cada numero va
            // SIEMPRE ADENTRO de su cuadro, en una sola linea y centrado (la 1.6-1.7 los escalonaba y un numero usaba el ancho de dos
            // cuadros: quedaba encima del vecino). El bloque es FIJO (EstVelasCuadro, 4 velas cortadas a la hora redonda: con su
            // zoom de 7-11 px por vela da 28-44 px, lo justo para 4-5 caracteres legibles); el zoom no cambia ni los cortes ni los
            // numeros, nada se junta. La letra de cada fila es la mas grande con la que TODOS sus numeros entran en su cuadro; si con
            // letra 7 alguno no entra, ese cuadro queda pintado sin numero (nunca uno que se salga). El bloque EN CURSO lleva recuadro
            // y se estira hacia la derecha (el futuro, vacio) hasta que sus numeros entran en el.
            int k = Math.Max(1, EstVelasCuadro);
            var cuadros = Agrupar(k, n, hora, d, v, s, segVela);
            bool enCurso = hasta == ult;
            if (enCurso && cuadros.Count > 0) cuadros[cuadros.Count - 1].EnCurso = true;
            int anchoBloque = k * wCol;

            var fuente = new RenderFont[nFil]; var altoTxt = new int[nFil]; var letra = new int[nFil]; var hay = new bool[nFil];
            int anchoCaja = Math.Max(1, anchoBloque - 1 - 4);   // lo que queda adentro del cuadro: 1 px de separacion y 2 de aire por lado
            for (int r = 0; r < nFil; r++)
            {
                var t = tipos[r];
                // (se miden solo los textos mas largos de la fila: ATAS redibuja en cada operacion y medir todos seria caro)
                var txts = cuadros.Where(c => !c.EnCurso).Select(c => FormatoFila(t, c.Valor(t))).ToList();
                int lmax = txts.Count > 0 ? txts.Max(x => x.Length) : 0;
                var largos = txts.Where(x => x.Length >= lmax - 1).Distinct().ToList();
                int tope = t == Tipo.Delta ? Math.Max(7, EstLetraCajas) : Math.Max(7, Math.Min(EstLetraCajas, 9));
                for (int tam = tope; tam >= 7 && !hay[r]; tam--)
                {
                    var ft = new RenderFont("Segoe UI", tam); int h = g.MeasureString("0", ft).Height;
                    if (h + 2 > altoF[r]) continue;
                    int mas = 0; foreach (var x in largos) mas = Math.Max(mas, g.MeasureString(x, ft).Width);
                    if (mas <= anchoCaja || tam == 7) { fuente[r] = ft; altoTxt[r] = h; letra[r] = tam; hay[r] = true; }
                }
                if (!hay[r]) { fuente[r] = new RenderFont("Segoe UI", 7); altoTxt[r] = g.MeasureString("0", fuente[r]).Height; letra[r] = 7; hay[r] = true; }
            }

            var fNom = new RenderFont("Segoe UI", Math.Max(7, EstLetra - 1));
            var fNota = new RenderFont("Segoe UI", 7);
            string[] nombres = tipos.Select(t => t == Tipo.Delta ? "Delta" : t == Tipo.Sesion ? "Delta sesion (miles)" : "Volumen").ToArray();
            string nota = k > 1 ? "cuadro = " + k + " velas" : "cuadro = 1 vela";
            int anchoNom = 0;
            if (EstNombres)
            {
                foreach (var nm in nombres) anchoNom = Math.Max(anchoNom, g.MeasureString(nm, fNom).Width + 8);
                anchoNom = Math.Max(anchoNom, g.MeasureString(nota, fNota).Width + 8);
            }

            // el bloque en curso se estira hacia la derecha hasta que sus numeros entran adentro
            int xbCurso = 0;
            if (enCurso && cuadros.Count > 0)
            {
                var cc = cuadros[cuadros.Count - 1]; int nec = 0;
                for (int r = 0; r < nFil; r++) nec = Math.Max(nec, g.MeasureString(FormatoFila(tipos[r], cc.Valor(tipos[r])), fuente[r]).Width);
                xbCurso = Math.Min(xMax, Math.Max(x0[cc.Fin + 1], x0[cc.Ini] + nec + 8));
            }

            var audit = new StringBuilder();
            audit.Append("vela ").Append(wCol).Append(" px (").Append(segVela.ToString("0", Inv)).Append(" s), bloque fijo de ").Append(k)
                 .Append(" velas = ").Append(anchoBloque).Append(" px, ").Append(cuadros.Count).Append(" cuadros")
                 .Append(enCurso ? ", el ultimo en curso" : " (corrido hacia atras)");
            for (int r = 0; r < nFil; r++)
                audit.Append(" | ").Append(tipos[r]).Append(": fila ").Append(altoF[r]).Append(" px, ")
                     .Append("letra ").Append(letra[r]).Append(" (adentro: ").Append(anchoCaja).Append(" px)");

            for (int r = 0; r < nFil; r++)
            {
                var t = tipos[r]; int yF = topF[r], hF = altoF[r]; var f = fuente[r];
                double mx = 1; foreach (var c in cuadros) if (!c.EnCurso) mx = Math.Max(mx, Math.Abs(c.Valor(t)));
                if (r > 0) g.DrawLine(penGrilla, reg.Left, yF, xMax, yF);
                foreach (var c in cuadros)
                {
                    int a = Math.Max(x0[c.Ini], reg.Left), b = Math.Min(c.EnCurso ? xbCurso : x0[c.Fin + 1], xMax);
                    if (b - a < 1) continue;
                    double val = c.Valor(t);
                    Color baseC = t == Tipo.Volumen ? cVol : (val >= 0 ? cMas : cMenos);
                    int alfa = (int)(40 + 180 * Math.Sqrt(Math.Min(1.0, Math.Abs(val) / mx)));
                    int gap = b - a >= 4 ? 1 : 0;
                    g.FillRectangle(Color.FromArgb(alfa, baseC.R, baseC.G, baseC.B), new Rectangle(a, yF + 1, b - a - gap, hF - 2));
                }
                int sinNumero = 0, chicos = 0; var fChica = new RenderFont("Segoe UI", 6);
                foreach (var c in cuadros)
                {
                    int a = x0[c.Ini], b = c.EnCurso ? xbCurso : x0[c.Fin + 1];
                    string txt = FormatoFila(t, c.Valor(t)); var sz = g.MeasureString(txt, f);
                    var fc = f; int xt = a + (b - 1 - a - sz.Width) / 2;
                    if (xt < a + 2 || xt + sz.Width > b - 3)
                    {
                        // no entra con la letra de la fila: ese cuadro prueba con letra 6 antes de quedar sin numero (nunca afuera)
                        fc = fChica; sz = g.MeasureString(txt, fc); xt = a + (b - 1 - a - sz.Width) / 2;
                        if (xt < a + 2 || xt + sz.Width > b - 3) { if (a >= reg.Left + anchoNom) sinNumero++; continue; }
                        chicos++;
                    }
                    if (xt < reg.Left + anchoNom || xt + sz.Width > xMax) continue;   // ni debajo de los nombres ni cortado por el borde
                    g.DrawString(txt, fc, cTxt, xt, yF + (hF - g.MeasureString("0", fc).Height) / 2);
                }
                audit.Append(" | ").Append(t).Append(": ").Append(chicos).Append(" con letra 6, sin numero (no entra ni con letra 6): ").Append(sinNumero);
            }
            if (enCurso && cuadros.Count > 0)
            {
                var c = cuadros[cuadros.Count - 1];
                int a = x0[c.Ini], b = Math.Max(xbCurso, a + 3);
                g.DrawRectangle(new RenderPen(Color.FromArgb(220, cTxt.R, cTxt.G, cTxt.B), 1), new Rectangle(a, reg.Top + 1, b - a - 1, reg.Height - 3));
            }

            // nombres de las filas, encima de todo, con fondo propio; debajo de "Delta", cuantas velas junta cada cuadro
            if (EstNombres)
                for (int r = 0; r < nFil; r++)
                {
                    var rr = new Rectangle(reg.Left, topF[r], anchoNom, altoF[r]);
                    g.FillRectangle(Color.FromArgb(240, cFondo.R, cFondo.G, cFondo.B), rr);
                    int hN = g.MeasureString(nombres[r], fNom).Height;
                    bool conNota = tipos[r] == Tipo.Delta && nota.Length > 0;
                    int hNota = conNota ? g.MeasureString(nota, fNota).Height : 0;
                    int y0 = rr.Top + (altoF[r] - hN - hNota) / 2;
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
                        FormatoFila(Tipo.Delta, c.Delta), c.Delta, FormatoFila(Tipo.Sesion, c.Sesion), c.Sesion, FormatoFila(Tipo.Volumen, c.Volumen), c.Volumen));
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
