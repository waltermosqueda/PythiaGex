using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using MColor = System.Windows.Media.Color;

namespace PythiaLaOla
{
    /// <summary>
    /// LA OLA 1.0: LO QUE SE DIBUJA. Todo sale de UNA copia tomada bajo la llave al empezar cada dibujo (Instante): la ola, la marea y el
    /// clima de la vela del MOMENTO (la ultima a la vista), el estado de la marea de cada vela visible (fondo) y las piernas y giros de las
    /// jornadas a la vista. Despues se dibuja sin la llave (el hilo de datos no espera al dibujo).
    /// Orden: fondo de marea, linea de la ola, rombos de la marea, rayas de giro y, arriba de todo, el panel.
    /// </summary>
    public partial class PythiaOla
    {
        // ================================================================== la copia para dibujar
        private sealed class Tramo { public int B1, B2; public decimal P1, P2; public int Dir; public bool Con, EnCurso; }

        private sealed class Instante
        {
            public int Momento = -1, Ultima = -1, PrimVis, UltVis;
            public bool Hay; public string Estado = ""; public decimal X; public int Clave = -1;
            public FotoOla Ola, Marea;
            public bool HayClima; public MinutoClima Clima; public string MotivoClima = "";
            public bool Live, Atrasada, RecibeVivo, HayCarga;
            public string FuenteX = "", EstadoCarga = "", Espera = ""; public int Usadas; public decimal Mediana; public double MedClima;
            public int BarExtOla = -1, BarExtMarea = -1;
            public sbyte[] Fondo = Array.Empty<sbyte>();   // por vela visible: 0 sin pierna, 1 sube con agresores, 2 baja con agresores, 3 sin agresores
            public readonly List<Tramo> Tramos = new List<Tramo>();
            public readonly List<(int Bar, decimal Precio, int Tipo)> Rombos = new List<(int, decimal, int)>();
        }

        private static sbyte CodigoMarea(in FotoOla f) => !f.HayPierna ? (sbyte)0 : !f.ConAgresores ? (sbyte)3 : f.Dir > 0 ? (sbyte)1 : (sbyte)2;

        private Instante TomarInstante(int m, int ultima, int primVis, int ultVis)
        {
            var s = new Instante { Momento = m, Ultima = ultima, PrimVis = primVis, UltVis = ultVis };
            lock (_llaveOla)
            {
                var g = OlaG();
                s.Hay = g.AlMomento(m, out var fo, out var fm, out var po, out var pm, out decimal x, out string est, out var carga);
                s.Estado = est ?? ""; s.X = x; s.Espera = _olaEspera ?? "";
                if (carga != null)
                {
                    s.HayCarga = true; s.Clave = carga.Clave; s.FuenteX = carga.FuenteX; s.Usadas = carga.Usadas; s.Mediana = carga.Mediana; s.MedClima = carga.MedClima;
                    s.EstadoCarga = carga.EstadoTexto(); s.RecibeVivo = carga.RecibeVivo;
                    s.Atrasada = !s.Hay && s.Estado.StartsWith("atrasada", StringComparison.Ordinal);
                }
                if (s.Hay)
                {
                    s.Ola = fo; s.Marea = fm;
                    if (fo.HayPierna) s.BarExtOla = g.BarDeHora(fo.Extremo.T);
                    if (fm.HayPierna) s.BarExtMarea = g.BarDeHora(fm.Extremo.T);
                }
                s.HayClima = g.ClimaAlMomento(m, out var mc, out string motC, out _);
                s.Clima = mc; s.MotivoClima = motC ?? "";
                // LIVE: la vela del momento es la ultima del grafico y su jornada sigue en vivo (no atrasada)
                s.Live = s.Hay && m >= ultima && carga != null && carga.RecibeVivo;
                // el fondo: la marea al cierre de cada vela visible
                if (_fondo && ultVis >= primVis)
                {
                    s.Fondo = new sbyte[ultVis - primVis + 1];
                    for (int b = primVis; b <= ultVis; b++) if (g.FotosDe(b, out _, out var mb)) s.Fondo[b - primVis] = CodigoMarea(mb);
                }
                // la linea y los rombos: cada jornada a la vista, como estaba en el momento (o al final, si termino antes)
                if ((_linea || _rombo) && ultVis >= primVis)
                {
                    for (int b = primVis; b <= ultVis;)
                    {
                        int clave = g.ClaveDe(b);
                        if (clave < 0 || !g.BarrasDe(clave, out int p, out int u)) { b++; continue; }
                        int mj = Math.Min(u, m);
                        bool deMomento = mj == m;
                        if (mj >= p && g.AlMomento(mj, out var fo2, out var fm2, out var po2, out var pm2, out _, out _, out _))
                        {
                            if (_linea) AgregarTramos(g, s, po2, fo2, deMomento);
                            if (_rombo) foreach (var q in pm2) { int bq = g.BarDeHora(q.T); if (bq >= primVis && bq <= ultVis) s.Rombos.Add((bq, q.Precio, q.Tipo)); }
                        }
                        b = Math.Max(b + 1, u + 1);
                    }
                }
            }
            return s;
        }

        /// <summary>Las piernas de la ola que tocan la vista: de giro a giro (con agresores si el delta acumulado entre los dos extremos va
        /// a favor de la pierna) y la pierna en curso, del ultimo giro al extremo de ahora.</summary>
        private static void AgregarTramos(GestorOla g, Instante s, IReadOnlyList<PivoteOla> piv, in FotoOla f, bool deMomento)
        {
            int n = piv.Count;
            for (int i = 0; i + 1 < n; i++)
            {
                var a = piv[i]; var z = piv[i + 1];
                int b1 = g.BarDeHora(a.T), b2 = g.BarDeHora(z.T);
                if (b2 < s.PrimVis - 1 || b1 > s.UltVis + 1) continue;
                s.Tramos.Add(new Tramo { B1 = b1, P1 = a.Precio, B2 = b2, P2 = z.Precio, Dir = z.Tipo, Con = Math.Sign(z.Cum - a.Cum) == z.Tipo });
            }
            if (f.HayPierna && n > 0)
            {
                var a = piv[n - 1];
                int b1 = g.BarDeHora(a.T), b2 = g.BarDeHora(f.Extremo.T);
                if (!(b2 < s.PrimVis - 1 || b1 > s.UltVis + 1))
                    s.Tramos.Add(new Tramo { B1 = b1, P1 = a.Precio, B2 = b2, P2 = f.Extremo.Precio, Dir = f.Dir, Con = f.ConAgresores, EnCurso = deMomento });
            }
        }

        // ================================================================== dibujo
        private double _msRender, _msRenderMax; private int _nRender;   // cuanto le cuesta a ATAS cada dibujo (a la auditoria)
        private DateTime _ultAudit = DateTime.MinValue;

        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                var pc = ChartInfo?.PriceChartContainer;
                if (pc == null || CurrentBar <= 0) return;
                var reg = pc.Region;
                if (reg.Width < 80 || reg.Height < 60) return;
                int ultima = CurrentBar - 1, m = Momento();
                int primVis = Math.Max(0, FirstVisibleBarNumber), ultVis = Math.Min(ultima, LastVisibleBarNumber);
                if (ultVis - primVis > 6000) primVis = ultVis - 6000;   // tope de seguridad con el grafico muy alejado
                var s = TomarInstante(m, ultima, primVis, ultVis);
                var p = new Pantalla { Momento = m, Ultima = ultima, PrimVis = primVis, UltVis = ultVis };
                if (_fondo) DibujarFondo(g, pc, reg, s, p);
                if (_linea) DibujarLinea(g, pc, reg, s, p);
                if (_rombo) DibujarRombos(g, pc, reg, s, p);
                if (_rayas) DibujarRayas(g, pc, reg, s, p);
                if (_panel) DibujarPanel(g, reg, s, p);
                if (_registro && (DateTime.UtcNow - _ultAudit).TotalSeconds >= 60)
                {
                    _ultAudit = DateTime.UtcNow;
                    Auditar(s, p, reg);
                    _msRender = _msRenderMax = 0; _nRender = 0;
                }
            }
            catch (Exception e) { Anotar(e); }
            finally
            {
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                _msRender += ms; _nRender++; if (ms > _msRenderMax) _msRenderMax = ms;
            }
        }

        private static readonly Dictionary<string, RenderFont> _fuentes = new Dictionary<string, RenderFont>();
        private static RenderFont Fuente(float tam, bool negrita)
        {
            string k = tam.ToString("0.0", CultureInfo.InvariantCulture) + (negrita ? "b" : "");
            lock (_fuentes)
            {
                if (!_fuentes.TryGetValue(k, out var f))
                {
                    f = negrita ? new RenderFont("Segoe UI", tam, FontStyle.Bold) : new RenderFont("Segoe UI", tam);
                    _fuentes[k] = f;
                }
                return f;
            }
        }

        private static Color Alfa(MColor c, int a) => Color.FromArgb(Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B);

        private int Y(IChartContainer pc, decimal precio) => pc.GetYByPrice(precio, false);
        private int X(IChartContainer pc, int bar) => pc.GetXByBar(bar, false);

        // ------------------------------------------------------------------ fondo de marea
        // La geometria de ATAS 8.0.15 (descompilada en la revision del 27-11): las velas van a paso FRACCIONARIO (ancho + ancho/3), cada una
        // empieza en GetXByBar(b, true), el cuerpo ocupa [inicio, inicio + ancho] y la mecha va en inicio + ancho/2 (truncado); el centro que
        // da GetXByBar(b, false) esta redondeado. Por eso: (1) el borde entre dos velas vecinas es el punto medio entre sus centros, el MISMO
        // para las dos (sin columnas pintadas dos veces ni huecos de 1 px); (2) el recorte sale del inicio real de cada vela, con 1 px de aire,
        // y se arma columna de pixeles por columna mirando TODAS las velas que tocan esa columna (la vecina puede meterse 1 px en la ranura).
        private const int FondoMaxVelasConRecorte = 800;   // mas velas a la vista (grafico muy alejado): sin recorte (ATAS ni dibuja el cuerpo)

        private struct GeoVela { public bool Hay; public int X0, X1, M0, M1, YH, YL, YT, YB; }   // cuerpo [X0, X1] y mecha [M0, M1] con aire; y

        /// <summary>
        /// El color de la marea de cada vela, de borde a borde de su lugar. "Detras de las velas": como ATAS deja arriba o abajo de las velas
        /// al indicador ENTERO (y el panel y las rayas tienen que verse siempre), el fondo se pinta ALREDEDOR de cada vela: en cada columna de
        /// pixeles, todo lo que no es cuerpo ni mecha (con 1 px de aire). La vela queda con su color. En barras o clusters se recorta la caja
        /// entera de la vela; en linea, montana u otro modo sin velas, sin recorte; con mas de 800 velas a la vista o velas de menos de 2 px,
        /// tambien sin recorte (no se ve y costaria decenas de miles de rectangulos por cuadro).
        /// A la derecha de la ultima vela (si se ve) el color de ahora sigue hasta el borde: ahi no hay velas.
        /// </summary>
        private void DibujarFondo(RenderContext g, IChartContainer pc, Rectangle reg, Instante s, Pantalla p)
        {
            int n = s.Fondo.Length;
            if (n == 0) return;
            int a = (int)Math.Round(255 * _fondoOpac / 100.0);
            var cols = new[] { Color.Empty, Alfa(_cSube, a), Alfa(_cBaja, a), Alfa(_cSin, a) };
            string modo = "";
            try { modo = ChartInfo?.ChartVisualMode.ToString() ?? ""; } catch { }
            bool velas = modo == "Candles" || modo == "TransparentCandles", caja = modo == "Bars" || modo == "Clusters";
            double bw = (double)pc.BarsWidth;
            bool recorte = _capa == CapaFondo.DetrasDeLasVelas && (velas || caja) && bw >= 2 && n <= FondoMaxVelasConRecorte;
            int top = reg.Top, bot = reg.Bottom;
            p.FondoModo = modo; p.FondoRecorte = recorte;
            // centros de las velas PrimVis-1 .. UltVis+1 y bordes compartidos (punto medio): la vela PrimVis+i va de Borde(i) a Borde(i+1)
            var cx = new int[n + 2];
            for (int k = 1; k <= n; k++) cx[k] = X(pc, s.PrimVis - 1 + k);
            int pasoAprox = n > 1 ? Math.Max(1, (cx[n] - cx[1]) / (n - 1)) : Math.Max(1, (int)Math.Round(pc.BarsWidth + pc.BarSpacing));
            // la vela anterior a la primera visible y la siguiente a la ultima pueden no existir: si ATAS no da su lugar, se extrapola
            try { cx[0] = X(pc, s.PrimVis - 1); } catch { cx[0] = cx[1] - pasoAprox; }
            try { cx[n + 1] = X(pc, s.UltVis + 1); } catch { cx[n + 1] = cx[n] + pasoAprox; }
            for (int k = 1; k < n + 2; k++) if (cx[k] <= cx[k - 1]) cx[k] = cx[k - 1] + 1;   // por las dudas: siempre crecientes
            int Borde(int i) => (cx[i] + cx[i + 1] + 1) >> 1;
            // geometria real de cada vela (con 1 px de aire); indice k = vela PrimVis-1+k
            GeoVela[] geo = null;
            if (recorte)
            {
                int pad = Math.Max(1, (int)Math.Ceiling((double)pc.PriceRowHeight / 2.0));
                int bwi = (int)bw;
                geo = new GeoVela[n + 2];
                for (int k = 0; k < n + 2; k++)
                {
                    int b = s.PrimVis - 1 + k;
                    if (b < 0 || b > s.Ultima) continue;
                    var c = GetCandle(b);
                    if (c == null) continue;
                    int x0 = pc.GetXByBar(b, true), mx = x0 + (int)(bw / 2.0);
                    decimal arriba = c.Open > c.Close ? c.Open : c.Close, abajo = c.Open > c.Close ? c.Close : c.Open;
                    geo[k] = new GeoVela
                    {
                        Hay = true, X0 = x0 - 1, X1 = x0 + bwi + 1, M0 = mx - 1, M1 = mx + 1,
                        YH = Y(pc, c.High) - pad, YL = Y(pc, c.Low) + pad, YT = Y(pc, arriba) - pad, YB = Y(pc, abajo) + pad
                    };
                }
            }
            // columna por columna de pixeles; las columnas seguidas con el mismo color y los mismos huecos se juntan en un rectangulo
            var libres = new int[8]; var prev = new int[8]; int nLib = 0, nPrev = -1, xPrev = 0, codPrev = 0;
            int iv = 0;
            int x = Borde(0), xFin = Borde(n);
            for (; x < xFin; x++)
            {
                while (iv < n - 1 && x >= Borde(iv + 1)) iv++;
                int cod = s.Fondo[iv];
                nLib = 0;
                if (cod != 0)
                {
                    if (!recorte) { libres[0] = top; libres[1] = bot; nLib = 2; }
                    else LibresEn(geo, iv + 1, x, caja, top, bot, libres, out nLib);
                }
                bool igual = cod == codPrev && nLib == nPrev;
                for (int q = 0; igual && q < nLib; q++) igual = libres[q] == prev[q];
                if (!igual)
                {
                    VolcarFondo(g, cols, codPrev, prev, nPrev, xPrev, x);
                    Array.Copy(libres, prev, nLib); nPrev = nLib; codPrev = cod; xPrev = x;
                }
            }
            VolcarFondo(g, cols, codPrev, prev, nPrev, xPrev, x);
            // a la derecha de la ultima vela: el color de ahora hasta el borde (no hay velas)
            if (s.UltVis >= s.Ultima && s.Fondo[n - 1] != 0)
            {
                int xl = xFin;
                if (geo != null && geo[n].Hay && xl <= geo[n].X1) xl = geo[n].X1 + 1;
                Llenar(g, cols[s.Fondo[n - 1]], xl, top, reg.Right - xl, bot - top);
            }
            p.FondoMomento = s.Momento >= s.PrimVis && s.Momento - s.PrimVis < n ? s.Fondo[s.Momento - s.PrimVis] : (sbyte)-1;
        }

        /// <summary>Los tramos verticales LIBRES (sin cuerpo ni mecha de ninguna vela) de la columna de pixeles x, como pares [y0, y1) en
        /// 'libres'. Se miran la vela de la ranura (k) y sus dos vecinas: la vecina puede meterse un pixel en esta ranura.</summary>
        private static void LibresEn(GeoVela[] geo, int k, int x, bool caja, int top, int bot, int[] libres, out int nLib)
        {
            // hasta 3 intervalos tapados [a, b] (inclusive), ordenados por a
            int t0a = 0, t0b = -1, t1a = 0, t1b = -1, t2a = 0, t2b = -1, nt = 0;
            for (int d = -1; d <= 1; d++)
            {
                int q = k + d;
                if (q < 0 || q >= geo.Length || !geo[q].Hay) continue;
                var v = geo[q];
                if (x < v.X0 || x > v.X1) continue;
                int ya, yb;
                if (caja || (x >= v.M0 && x <= v.M1)) { ya = v.YH; yb = v.YL; } else { ya = v.YT; yb = v.YB; }
                if (nt == 0) { t0a = ya; t0b = yb; } else if (nt == 1) { t1a = ya; t1b = yb; } else { t2a = ya; t2b = yb; }
                nt++;
            }
            // ordenar (son a lo sumo 3) y restar de [top, bot)
            if (nt > 1 && t1a < t0a) { (t0a, t1a) = (t1a, t0a); (t0b, t1b) = (t1b, t0b); }
            if (nt > 2 && t2a < t1a) { (t1a, t2a) = (t2a, t1a); (t1b, t2b) = (t2b, t1b); if (t1a < t0a) { (t0a, t1a) = (t1a, t0a); (t0b, t1b) = (t1b, t0b); } }
            nLib = 0;
            int y = top;
            for (int i = 0; i < nt; i++)
            {
                int a = i == 0 ? t0a : i == 1 ? t1a : t2a, b = i == 0 ? t0b : i == 1 ? t1b : t2b;
                if (b < y) continue;
                if (a > y) { libres[nLib++] = y; libres[nLib++] = Math.Min(a, bot); }
                y = Math.Max(y, b + 1);
                if (y >= bot) break;
            }
            if (y < bot) { libres[nLib++] = y; libres[nLib++] = bot; }
        }

        private static void VolcarFondo(RenderContext g, Color[] cols, int cod, int[] libres, int nLib, int x0, int x1)
        {
            if (cod <= 0 || nLib <= 0 || x1 <= x0) return;
            for (int i = 0; i + 1 < nLib; i += 2) Llenar(g, cols[cod], x0, libres[i], x1 - x0, libres[i + 1] - libres[i]);
        }

        private static void Llenar(RenderContext g, Color c, int x, int y, int w, int h)
        {
            if (w > 0 && h > 0) g.FillRectangle(c, new Rectangle(x, y, w, h));
        }

        // ------------------------------------------------------------------ linea de la ola
        /// <summary>Cada pierna de giro a giro: llena (verde si sube, roja si baja) si fue con agresores; punteada ambar si fue sin; la pierna
        /// en curso del momento, mas gruesa.</summary>
        private void DibujarLinea(RenderContext g, IChartContainer pc, Rectangle reg, Instante s, Pantalla p)
        {
            foreach (var t in s.Tramos)
            {
                if (t.B2 < 0) continue;
                // un giro anterior a la primera vela del grafico (la jornada empezo antes de lo cargado): la pierna arranca en la vela 0
                int b1 = Math.Max(0, t.B1);
                int x1 = X(pc, b1), y1 = Y(pc, t.P1), x2 = X(pc, t.B2), y2 = Y(pc, t.P2);
                float ancho = t.EnCurso ? 3f : 1.6f;
                RenderPen pen = t.Con
                    ? new RenderPen(D(t.Dir > 0 ? _cSube : _cBaja), ancho)
                    : new RenderPen(D(_cSin), ancho, System.Drawing.Drawing2D.DashStyle.Dot);
                g.DrawLine(pen, x1, y1, x2, y2);
                p.Tramos++;
                if (t.EnCurso) { p.TramoEnCurso = t.Con ? (t.Dir > 0 ? "sube con agresores" : "baja con agresores") : "sin agresores"; }
            }
        }

        // ------------------------------------------------------------------ rombos de la marea
        private void DibujarRombos(RenderContext g, IChartContainer pc, Rectangle reg, Instante s, Pantalla p)
        {
            var col = D(_cRayaMarea);
            var borde = new RenderPen(Color.FromArgb(230, 20, 22, 28), 1);
            const int r = 5;
            foreach (var (bar, precio, tipo) in s.Rombos)
            {
                int x = X(pc, bar), y = Y(pc, precio) + (tipo > 0 ? -(r + 5) : r + 5);   // arriba de un maximo, abajo de un minimo
                if (y < reg.Top - r || y > reg.Bottom + r) continue;
                var pts = new[] { new Point(x, y - r), new Point(x + r, y), new Point(x, y + r), new Point(x - r, y) };
                g.FillPolygon(col, pts);
                g.DrawPolygon(borde, pts);
                p.Rombos++;
            }
        }

        // ------------------------------------------------------------------ rayas de giro
        /// <summary>Donde la ola y la marea se dan vuelta (extremo -/+ X o 4X), desde la vela del extremo hasta el borde, con su etiqueta corta
        /// a la derecha ("ola gira 7.047,75"). Si la etiqueta de la marea pisaria la de la ola, va pegada del lado de AFUERA de la de la ola
        /// (arriba si su raya esta arriba, abajo si esta abajo), nunca encima de la otra raya.</summary>
        private void DibujarRayas(RenderContext g, IChartContainer pc, Rectangle reg, Instante s, Pantalla p)
        {
            if (!s.Hay) return;
            var f = Fuente(8, true);
            int xDer = reg.Right - _margen;
            Rectangle etOla = Rectangle.Empty; int yOla = 0;
            if (s.Ola.HayPierna)
            {
                decimal precio = s.Ola.Raya;
                int y = Y(pc, precio), x0 = s.BarExtOla >= 0 ? Math.Max(reg.Left, X(pc, s.BarExtOla)) : reg.Left;
                g.DrawLine(new RenderPen(D(_cRayaOla), 1.2f, System.Drawing.Drawing2D.DashStyle.Dot), x0, y, reg.Right, y);
                string txt = "ola gira " + Formato.Precio(precio);
                etOla = RectEtiqueta(g, f, txt, xDer, y - EtiquetaAlto(g, f, txt) - 1, reg);
                PintarEtiqueta(g, f, txt, D(_cRayaOla), etOla);
                yOla = y;
                p.RayaOla = precio; p.RayaOlaY = y; p.RayaOlaTexto = txt;
            }
            if (s.Marea.HayPierna)
            {
                decimal precio = s.Marea.Raya;
                int y = Y(pc, precio), x0 = s.BarExtMarea >= 0 ? Math.Max(reg.Left, X(pc, s.BarExtMarea)) : reg.Left;
                g.DrawLine(new RenderPen(D(_cRayaMarea), 1.5f), x0, y, reg.Right, y);
                string txt = "marea gira " + Formato.Precio(precio);
                int h = EtiquetaAlto(g, f, txt);
                var r = RectEtiqueta(g, f, txt, xDer, y - h - 1, reg);
                if (!etOla.IsEmpty && r.IntersectsWith(etOla))
                    r = RectEtiqueta(g, f, txt, xDer, y <= yOla ? etOla.Top - h - 2 : etOla.Bottom + 2, reg);
                PintarEtiqueta(g, f, txt, D(_cRayaMarea), r);
                p.RayaMarea = precio; p.RayaMareaY = y; p.RayaMareaTexto = txt;
            }
        }

        private static int EtiquetaAlto(RenderContext g, RenderFont f, string txt) => g.MeasureString(txt, f).Height + 2;

        /// <summary>El lugar de una etiqueta alineada a la derecha en xDer con su borde de arriba en yArriba, adentro del grafico.</summary>
        private static Rectangle RectEtiqueta(RenderContext g, RenderFont f, string txt, int xDer, int yArriba, Rectangle reg)
        {
            var sz = g.MeasureString(txt, f);
            int w = sz.Width + 6, h = sz.Height + 2;
            int yy = Math.Max(reg.Top + 1, Math.Min(reg.Bottom - h - 3, yArriba));
            return new Rectangle(xDer - w, yy, w, h);
        }

        /// <summary>Etiqueta con fondo oscuro (para leerse arriba de las velas).</summary>
        private static void PintarEtiqueta(RenderContext g, RenderFont f, string txt, Color c, Rectangle r)
        {
            g.FillRectangle(Color.FromArgb(200, 20, 22, 28), r, 2);
            g.DrawString(txt, f, c, r.X + 3, r.Y + 1);
        }

        // ------------------------------------------------------------------ panel
        private static readonly string[] DiasCortos = { "dom", "lun", "mar", "mie", "jue", "vie", "sab" };

        /// <summary>
        /// El panel, con el estilo del encabezado del Superindicador (variante E, la que eligio el operador): renglon 1 "● LIVE" (verde si es
        /// la vela en curso y la jornada sigue en vivo; rojo apagado si es un momento pasado) y la hora de la PC (la de la ultima orden que
        /// entro hasta el momento) a la derecha; renglon 2 el dia; una linea; despues CLIMA, MAREA y OLA, cada uno con su lectura y el dato
        /// fino abajo; al pie la X y de donde sale el dato. Sin sombras; fondo gris oscuro como la columna del Superindicador.
        /// </summary>
        private void DibujarPanel(RenderContext g, Rectangle reg, Instante s, Pantalla p)
        {
            var fLive = Fuente(7, true); var fHora = Fuente(7.5f, false); var fDia = Fuente(8.5f, true);
            var fEt = Fuente(7, true); var fVal = Fuente(8.5f, true); var fDet = Fuente(7, false); var fPie = Fuente(6.5f, false);
            Color cGris = Color.FromArgb(255, 154, 163, 174), cEt = Color.FromArgb(255, 138, 145, 156), cBlanco = Color.FromArgb(255, 236, 239, 244);
            Color cAmbar = Color.FromArgb(245, 255, 214, 0);
            Color cSube = D(_cSube), cBaja = D(_cBaja), cSin = D(_cSin);

            // renglones 1 y 2: LIVE, hora y dia (hora de la PC de la ultima orden que entro hasta el momento)
            DateTime tDato = s.Hay && s.Ola.N > 0 ? s.Ola.T : DateTime.MinValue;
            string hora = "", dia = "";
            if (tDato > DateTime.MinValue)
            {
                var l = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(tDato, DateTimeKind.Utc), TimeZoneInfo.Local);
                dia = DiasCortos[(int)l.DayOfWeek].ToUpperInvariant() + " " + l.ToString("dd/MM", CultureInfo.InvariantCulture);
                hora = l.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            }
            // CLIMA
            string cVal, cDet; Color cCol;
            if (s.HayClima)
            {
                var mc = s.Clima;
                cVal = mc.Tipo == 'N' ? "DUDOSO · esperar" : mc.Nombre();
                cCol = mc.Tipo == 'T' ? (mc.LadoVwap > 0 ? cSube : cBaja) : mc.Tipo == 'R' ? Color.FromArgb(255, 100, 181, 246)
                     : mc.Tipo == 'E' ? Color.FromArgb(255, 176, 184, 194) : cAmbar;
                var lm = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(mc.Fin.AddMinutes(-1), DateTimeKind.Utc), TimeZoneInfo.Local);
                cDet = Formato.Uno(mc.Rrn) + "× lo normal a esta hora (" + lm.ToString("HH:mm", CultureInfo.InvariantCulture) + ")";
            }
            else { cVal = "—"; cCol = cGris; cDet = s.MotivoClima.Length > 0 ? s.MotivoClima : "sin dato"; }
            // MAREA y OLA
            Lectura(s, s.Marea, "4X", cSube, cBaja, cSin, cGris, out string mVal, out Color mCol, out string mDet);
            Lectura(s, s.Ola, "X", cSube, cBaja, cSin, cGris, out string oVal, out Color oCol, out string oDet);
            // pie: X y de donde sale el dato
            string pie; bool avisoPie;
            if (s.HayCarga)
            {
                string fx = s.FuenteX == "auto" ? "auto, " + s.Usadas + " ruedas" : s.FuenteX;
                string est = s.Atrasada ? "cinta atrasada"
                           : s.EstadoCarga.StartsWith("sin ", StringComparison.Ordinal) ? s.EstadoCarga          // "sin cinta: solo en vivo"
                           : s.EstadoCarga == "en cola" && s.Espera.Length > 0 ? "cinta en cola: " + s.Espera
                           : "cinta " + s.EstadoCarga;
                pie = "X " + Formato.Puntos(s.X) + " (" + fx + ") · " + est;
                avisoPie = s.Atrasada || !(s.EstadoCarga == "en vivo" || s.EstadoCarga == "completa") || s.FuenteX != "auto";
            }
            else { pie = "cinta: " + (s.Espera.Length > 0 ? s.Espera : s.Estado.Length > 0 ? s.Estado : "esperando las velas"); avisoPie = true; }
            string senal = _senales ? "señales: en prueba, pendientes de validación (no dibuja)" : "";

            // medidas
            var filas = new[] { ("CLIMA", cVal, cCol, cDet), ("MAREA", mVal, mCol, mDet), ("OLA", oVal, oCol, oDet) };
            int wEt = 0, wTxt = 0;
            foreach (var (et, val, _, det) in filas)
            {
                wEt = Math.Max(wEt, g.MeasureString(et, fEt).Width);
                wTxt = Math.Max(wTxt, Math.Max(g.MeasureString(val, fVal).Width, g.MeasureString(det, fDet).Width));
            }
            var szL = g.MeasureString("LIVE", fLive);
            var szH = g.MeasureString(hora.Length > 0 ? hora : "00:00:00", fHora);
            int pad = 7, gap = 8;
            int w = Math.Max(wEt + gap + wTxt, Math.Max(7 + 4 + szL.Width + 12 + szH.Width, g.MeasureString(pie, fPie).Width));
            if (senal.Length > 0) w = Math.Max(w, g.MeasureString(senal, fPie).Width);
            w = Math.Max(196, w) + 2 * pad;
            int hVal = g.MeasureString("TENDENCIA", fVal).Height, hDet = g.MeasureString("lo", fDet).Height, hPie = g.MeasureString("X", fPie).Height;
            int hFila = hVal + hDet + 3;
            // los renglones de arriba se MIDEN (con la escala de Windows arriba de 100 % las letras crecen)
            int h1 = Math.Max(szL.Height, szH.Height) + 1, h2 = g.MeasureString("JUE 27/11", fDia).Height + 1;
            int h = pad + h1 + h2 + 6 + 3 * hFila + 5 + hPie + (senal.Length > 0 ? hPie + 1 : 0) + pad;
            int x0 = reg.Left + _panelX, y0 = reg.Top + _panelY;
            if (x0 + w > reg.Right - 4) x0 = Math.Max(reg.Left, reg.Right - 4 - w);
            if (y0 + h > reg.Bottom - 4) y0 = Math.Max(reg.Top, reg.Bottom - 4 - h);
            var caja = new Rectangle(x0, y0, w, h);
            g.FillRectangle(Color.FromArgb(236, 34, 38, 46), caja, 4);
            int xi = x0 + pad, xd = x0 + w - pad, y = y0 + pad;

            // (1) punto + LIVE y la hora
            Color cEstado = s.Live ? Color.FromArgb(255, 0, 230, 118) : Color.FromArgb(255, 142, 59, 63);
            g.FillEllipse(cEstado, new Rectangle(xi, y + (szL.Height - 7) / 2, 7, 7));
            g.DrawString("LIVE", fLive, cEstado, xi + 11, y);
            if (hora.Length > 0) g.DrawString(hora, fHora, cGris, xd - szH.Width, y + Math.Max(0, (szL.Height - szH.Height) / 2));
            y += h1;
            // (2) el dia
            if (dia.Length > 0) g.DrawString(dia, fDia, cBlanco, xi, y);
            y += h2;
            g.DrawLine(new RenderPen(Color.FromArgb(255, 62, 68, 78), 1), x0 + 4, y + 2, x0 + w - 4, y + 2);
            y += 6;
            // (3) CLIMA, MAREA, OLA
            int xv = xi + wEt + gap;
            foreach (var (et, val, col, det) in filas)
            {
                g.DrawString(et, fEt, cEt, xi, y + Math.Max(0, (hVal - g.MeasureString(et, fEt).Height) / 2));
                g.DrawString(val, fVal, col, xv, y);
                g.DrawString(det, fDet, cGris, xv, y + hVal);
                y += hFila;
            }
            // (4) pie
            g.DrawLine(new RenderPen(Color.FromArgb(255, 62, 68, 78), 1), x0 + 4, y + 1, x0 + w - 4, y + 1);
            y += 4;
            g.DrawString(pie, fPie, avisoPie ? cAmbar : cEt, xi, y);
            y += hPie + 1;
            if (senal.Length > 0) g.DrawString(senal, fPie, cAmbar, xi, y);

            p.Panel = caja; p.Live = s.Live; p.Hora = hora; p.Dia = dia; p.Pie = pie;
            p.Textos = new[] { "CLIMA " + cVal + " | " + cDet, "MAREA " + mVal + " | " + mDet, "OLA " + oVal + " | " + oDet };
        }

        /// <summary>Una fila del panel para la marea (4X) o la ola (X): "▲ con compradores", "▼ con vendedores", "▲ sin agresores"... y abajo el
        /// delta de la pierna y cuanto le falta al precio para tocar la raya de giro.</summary>
        private static void Lectura(Instante s, in FotoOla f, string esc, Color cSube, Color cBaja, Color cSin, Color cGris, out string val, out Color col, out string det)
        {
            if (!s.Hay) { val = "—"; col = cGris; det = s.Estado.Length > 0 ? s.Estado : "sin dato"; return; }
            if (!f.HayPierna)
            {
                val = "arrancando"; col = cGris;
                det = "todavía no recorrió " + esc + " (" + Formato.Puntos(f.X) + " pts)";
                return;
            }
            string flecha = f.Dir > 0 ? "▲ " : "▼ ";
            if (f.ConAgresores) { val = flecha + (f.Dir > 0 ? "con compradores" : "con vendedores"); col = f.Dir > 0 ? cSube : cBaja; }
            else { val = flecha + "sin agresores"; col = cSin; }
            det = "Δ " + Formato.ConSigno(f.DeltaPierna) + " · " + (f.Faltan > 0 ? "faltan " + Formato.Puntos(f.Faltan) : "en la raya");
        }
    }
}
