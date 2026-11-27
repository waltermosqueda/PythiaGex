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
            public string FuenteX = "", EstadoCarga = "", Espera = "", Corte = ""; public int Usadas; public decimal Mediana; public double MedClima;
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
                s.Estado = est ?? ""; s.X = x; s.Espera = _olaEspera ?? ""; s.Corte = _cortes.Aviso ?? "";
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
            bool panelDibujado = false;   // 1.0c: si el cuadro no se dibuja, no toma clics
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
                if (_panel) { DibujarPanel(g, reg, s, p); panelDibujado = true; } else RetirarCuadro();
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
                if (!panelDibujado) PanelOculto();
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

        /// <summary>1.0c: los textos del cuadro (las mismas cuentas de la 1.0b), para los tres dibujos: compacto, clasico y la pastilla.</summary>
        private sealed class TextosPanel
        {
            public string Hora = "", Dia = "";
            public string CVal = "", CDet = "", CDetCorto = ""; public Color CCol;
            public string MVal = "", MDet = ""; public Color MCol;
            public string OVal = "", ODet = ""; public Color OCol;
            public string Pie = "", Senal = ""; public bool AvisoPie;
        }

        private TextosPanel LeerPanel(Instante s)
        {
            Color cGris = Color.FromArgb(255, 154, 163, 174), cAmbar = Color.FromArgb(245, 255, 214, 0);
            Color cSube = D(_cSube), cBaja = D(_cBaja), cSin = D(_cSin);
            var t = new TextosPanel();
            // LIVE, hora y dia (hora de la PC de la ultima orden que entro hasta el momento)
            DateTime tDato = s.Hay && s.Ola.N > 0 ? s.Ola.T : DateTime.MinValue;
            if (tDato > DateTime.MinValue)
            {
                var l = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(tDato, DateTimeKind.Utc), TimeZoneInfo.Local);
                t.Dia = DiasCortos[(int)l.DayOfWeek].ToUpperInvariant() + " " + l.ToString("dd/MM", CultureInfo.InvariantCulture);
                t.Hora = l.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            }
            // CLIMA
            if (s.HayClima)
            {
                var mc = s.Clima;
                t.CVal = mc.Tipo == 'N' ? "DUDOSO · esperar" : mc.Nombre();
                t.CCol = mc.Tipo == 'T' ? (mc.LadoVwap > 0 ? cSube : cBaja) : mc.Tipo == 'R' ? Color.FromArgb(255, 100, 181, 246)
                       : mc.Tipo == 'E' ? Color.FromArgb(255, 176, 184, 194) : cAmbar;
                var lm = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(mc.Fin.AddMinutes(-1), DateTimeKind.Utc), TimeZoneInfo.Local);
                string hm = lm.ToString("HH:mm", CultureInfo.InvariantCulture);
                t.CDet = Formato.Uno(mc.Rrn) + "× lo normal a esta hora (" + hm + ")";
                t.CDetCorto = Formato.Uno(mc.Rrn) + "× lo normal (" + hm + ")";
            }
            else { t.CVal = "—"; t.CCol = cGris; t.CDet = t.CDetCorto = s.MotivoClima.Length > 0 ? s.MotivoClima : "sin dato"; }
            // MAREA y OLA
            Lectura(s, s.Marea, "4X", cSube, cBaja, cSin, cGris, out t.MVal, out t.MCol, out t.MDet);
            Lectura(s, s.Ola, "X", cSube, cBaja, cSin, cGris, out t.OVal, out t.OCol, out t.ODet);
            // pie: X y de donde sale el dato
            if (s.HayCarga)
            {
                string fx = s.FuenteX == "auto" ? "auto, " + s.Usadas + " ruedas" : s.FuenteX;
                string est = s.Atrasada ? "cinta atrasada"
                           : s.EstadoCarga.StartsWith("sin ", StringComparison.Ordinal) ? s.EstadoCarga          // "sin cinta: solo en vivo"
                           : s.EstadoCarga == "en cola" && s.Espera.Length > 0 ? "cinta en cola: " + s.Espera
                           : "cinta " + s.EstadoCarga;
                t.Pie = "X " + Formato.Puntos(s.X) + " (" + fx + ") · " + est;
                t.AvisoPie = s.Atrasada || !(s.EstadoCarga == "en vivo" || s.EstadoCarga == "completa") || s.FuenteX != "auto";
            }
            else { t.Pie = "cinta: " + (s.Espera.Length > 0 ? s.Espera : s.Estado.Length > 0 ? s.Estado : "esperando las velas"); t.AvisoPie = true; }
            if (s.Corte.Length > 0) { t.Pie = s.Corte; t.AvisoPie = true; }   // 1.0b: tras un corte de datos el pie avisa hasta la recarga del grafico
            t.Senal = _senales ? "señales: en prueba, pendientes de validación (no dibuja)" : "";
            return t;
        }

        /// <summary>
        /// 1.0c: el cuadro segun su estado (pedido del operador: "tapa, molesta; deberia ser minimizable y que se pueda correr arrastrando
        /// el mouse"): la PASTILLA si esta minimizado, el COMPACTO (de fabrica) o el CLASICO de la 1.0b. Deja su lugar para el mouse
        /// (PythiaOlaMouse.cs) y lo que se vio para la auditoria; con el mouse encima, el cartelito con la X y de donde sale el dato.
        /// </summary>
        private void DibujarPanel(RenderContext g, Rectangle reg, Instante s, Pantalla p)
        {
            var t = LeerPanel(s);
            var m = MousePanel();   // (-1, -1) si el mouse no esta sobre el grafico o se esta arrastrando
            Rectangle caja, menos = Rectangle.Empty;
            if (_panelMin) caja = DibujarPastilla(g, reg, s, p, t, m);
            else if (_panelEstilo == EstiloPanel.Clasico) caja = DibujarPanelClasico(g, reg, s, p, t);
            else caja = DibujarPanelCompacto(g, reg, s, p, t, m, out menos);
            string zona = PanelDibujado(reg, caja, menos, _panelMin, m);
            if (zona.Length > 0) CartelPanel(g, reg, caja, zona, t);
            p.Panel = caja; p.Live = s.Live; p.Hora = t.Hora; p.Dia = t.Dia; p.Pie = t.Pie;
            p.Minimizado = _panelMin; p.Estilo = _panelEstilo.ToString();
            if (!_panelMin && _panelEstilo == EstiloPanel.Compacto) p.PieVisible = t.AvisoPie; else if (!_panelMin) p.PieVisible = true;
        }

        /// <summary>El lugar del cuadro: PoPanelX / PoPanelY desde la esquina de arriba a la izquierda del grafico (o el del arrastre en curso),
        /// siempre entero adentro del grafico.</summary>
        private void LugarPanel(Rectangle reg, int w, int h, out int x0, out int y0)
        {
            PresionPanel pr; lock (_llaveMouse) pr = _presionPanel;
            bool arr = pr != null && pr.Movio;
            // 1.0d: sin lugar elegido, debajo de la franja de arriba donde escribe ATAS (hasta ~112 px con el aviso de datos viejos)
            int lx = _lugarX >= 0 ? _lugarX : 8, ly = _lugarY >= 0 ? _lugarY : 118;
            x0 = reg.Left + (arr ? _arrPX : lx); y0 = reg.Top + (arr ? _arrPY : ly);
            if (x0 + w > reg.Right - 4) x0 = reg.Right - 4 - w;
            if (y0 + h > reg.Bottom - 4) y0 = reg.Bottom - 4 - h;
            x0 = Math.Max(reg.Left, x0); y0 = Math.Max(reg.Top, y0);
            PublicarCuadro(x0 - reg.Left, y0 - reg.Top, w, h);   // 1.0e: la manija del Superindicador se pone debajo
        }

        /// <summary>1.0e (27-11, pedido del operador: "por defecto arriba a la izquierda, debajo de la lista de indicadores de ATAS, y que no
        /// compitan entre si, desplegados o minimizados"): el lugar del cuadro (o de la pastilla) queda en los datos del proceso, con una clave
        /// por instrumento y marco, para que el Superindicador 1.9d ponga su manija justo debajo. Lo retira OnDispose o el panel apagado.</summary>
        internal static string ClaveCuadro(string instrumento, string marco) => "PythiaGex.LaOla.cuadro|" + (instrumento ?? "").Replace("#", "").Trim() + "|" + (marco ?? "");
        private string ClaveCuadroPropia() { try { return ClaveCuadro(InstrumentInfo?.Instrument, ChartInfo?.TimeFrame); } catch { return ClaveCuadro("", ""); } }
        private void PublicarCuadro(int x, int y, int w, int h) { try { AppDomain.CurrentDomain.SetData(ClaveCuadroPropia(), new int[] { x, y, w, h, Environment.TickCount }); } catch { } }
        private void RetirarCuadro() { try { AppDomain.CurrentDomain.SetData(ClaveCuadroPropia(), null); } catch { } }

        /// <summary>
        /// 1.0c COMPACTO: un renglon de cabecera ("● LIVE  VIE 27/11  14:51:47" y el "–" para minimizar) y un renglon por dato: CLIMA, MAREA y
        /// OLA con la lectura en su color y el dato fino en gris a la derecha. El pie (en ambar apagado) solo aparece cuando hay algo que avisar
        /// (cinta atrasada o cargando, corte de datos, X que no es la automatica); la X de siempre va en el cartelito del mouse. Las columnas
        /// tienen un ancho minimo medido con los textos mas largos: el cuadro no cambia de ancho con cada orden.
        /// </summary>
        private Rectangle DibujarPanelCompacto(RenderContext g, Rectangle reg, Instante s, Pantalla p, TextosPanel t, Point m, out Rectangle rMenos)
        {
            var fLive = Fuente(7, true); var fCab = Fuente(7.5f, false);
            var fEt = Fuente(7, true); var fVal = Fuente(8, true); var fDet = Fuente(7, false); var fPie = Fuente(6.5f, false);
            Color cGris = Color.FromArgb(255, 154, 163, 174), cEt = Color.FromArgb(255, 128, 135, 147);
            Color cAviso = Color.FromArgb(230, 222, 184, 82);   // ambar apagado: se lee, no grita
            var filas = new[] { ("CLIMA", t.CVal, t.CCol, t.CDetCorto), ("MAREA", t.MVal, t.MCol, t.MDet), ("OLA", t.OVal, t.OCol, t.ODet) };
            int wEt = 0;
            int wVal = Math.Max(g.MeasureString("▲ con compradores", fVal).Width, g.MeasureString("DUDOSO · esperar", fVal).Width);
            int wDet = Math.Max(g.MeasureString("Δ +88.888 · faltan 88,88", fDet).Width, g.MeasureString("8,8× lo normal (88:88)", fDet).Width);
            foreach (var (et, val, _, det) in filas)
            {
                wEt = Math.Max(wEt, g.MeasureString(et, fEt).Width);
                wVal = Math.Max(wVal, g.MeasureString(val, fVal).Width);
                wDet = Math.Max(wDet, g.MeasureString(det, fDet).Width);
            }
            string cab = (t.Dia + "  " + t.Hora).Trim();
            var szL = g.MeasureString("LIVE", fLive);
            var szC = g.MeasureString(cab.Length > 0 ? cab : "VIE 00/00  00:00:00", fCab);
            const int pad = 7, gap = 8, gap2 = 12, wMenos = 16;
            int hVal = g.MeasureString("TENDENCIA", fVal).Height, hEt = g.MeasureString("OLA", fEt).Height;
            int hDet = g.MeasureString("lo", fDet).Height, hPie = g.MeasureString("X", fPie).Height;
            int hCab = Math.Max(szL.Height, szC.Height);
            bool conPie = t.AvisoPie, conSenal = t.Senal.Length > 0;
            int w = Math.Max(7 + 4 + szL.Width + 8 + szC.Width + 8 + wMenos - pad + 4, wEt + gap + wVal + gap2 + wDet);
            if (conPie) w = Math.Max(w, g.MeasureString(t.Pie, fPie).Width);
            if (conSenal) w = Math.Max(w, g.MeasureString(t.Senal, fPie).Width);
            w += 2 * pad;
            int hFila = hVal + 1;
            int h = 5 + hCab + 6 + 3 * hFila + (conPie ? 5 + hPie : 0) + (conSenal ? hPie + 1 : 0) + 5;
            LugarPanel(reg, w, h, out int x0, out int y0);
            var caja = new Rectangle(x0, y0, w, h);
            g.FillRectangle(Color.FromArgb(236, 34, 38, 46), caja, 4);
            var linea = new RenderPen(Color.FromArgb(255, 52, 58, 68), 1);
            int xi = x0 + pad, y = y0 + 5;

            // (1) cabecera: punto + LIVE (verde = la vela en curso con la jornada en vivo; rojo apagado = un momento pasado), dia y hora en gris
            Color cEstado = s.Live ? Color.FromArgb(255, 0, 230, 118) : Color.FromArgb(255, 142, 59, 63);
            g.FillEllipse(cEstado, new Rectangle(xi, y + (hCab - 7) / 2, 7, 7));
            g.DrawString("LIVE", fLive, cEstado, xi + 11, y + (hCab - szL.Height) / 2);
            if (cab.Length > 0) g.DrawString(cab, fCab, cGris, xi + 11 + szL.Width + 8, y + (hCab - szC.Height) / 2);
            // el "–" (solo si el mouse puede usarlo)
            rMenos = Rectangle.Empty;
            if (_panelMouse)
            {
                rMenos = new Rectangle(x0 + w - 4 - wMenos, y0 + 2, wMenos, hCab + 6);
                bool hv = m.X >= 0 && rMenos.Contains(m);
                if (hv) g.FillRectangle(Color.FromArgb(46, 255, 255, 255), rMenos, 3);
                int cx = rMenos.X + rMenos.Width / 2, cy = rMenos.Y + rMenos.Height / 2;
                g.FillRectangle(hv ? Color.FromArgb(255, 236, 239, 244) : Color.FromArgb(200, 140, 148, 160), new Rectangle(cx - 4, cy, 9, 2));
            }
            y += hCab + 3;
            g.DrawLine(linea, x0 + 4, y, x0 + w - 4, y);
            y += 3;

            // (2) CLIMA, MAREA, OLA: etiqueta gris, lectura en su color y el dato fino en gris, en un renglon
            int xv = xi + wEt + gap, xd = xv + wVal + gap2;
            foreach (var (et, val, col, det) in filas)
            {
                g.DrawString(et, fEt, cEt, xi, y + (hVal - hEt + 1) / 2);
                g.DrawString(val, fVal, col, xv, y);
                g.DrawString(det, fDet, cGris, xd, y + (hVal - hDet + 1) / 2);
                y += hFila;
            }

            // (3) el pie, solo con algo que avisar; y la linea de las senales si esta prendida
            if (conPie)
            {
                y += 2;
                g.DrawLine(linea, x0 + 4, y, x0 + w - 4, y);
                y += 3;
                g.DrawString(t.Pie, fPie, cAviso, xi, y);
                y += hPie;
            }
            if (conSenal) { y += 1; g.DrawString(t.Senal, fPie, cAviso, xi, y); }

            p.Textos = new[] { "CLIMA " + t.CVal + " | " + t.CDetCorto, "MAREA " + t.MVal + " | " + t.MDet, "OLA " + t.OVal + " | " + t.ODet };
            return caja;
        }

        /// <summary>
        /// 1.0c PASTILLA (cuadro minimizado): un renglon con el punto LIVE, la letra del clima en su color (T▲ / T▼ tendencia, R rotacion,
        /// E estrecho, D dudoso; — sin dato), "MAREA ▲" y "OLA ▼" con los colores de siempre (verde con compradores, rojo con vendedores,
        /// ambar sin agresores; · todavia arrancando) y, si hay algo que avisar, un punto ambar con una palabra. Un clic la abre.
        /// </summary>
        private Rectangle DibujarPastilla(RenderContext g, Rectangle reg, Instante s, Pantalla p, TextosPanel t, Point m)
        {
            var fLet = Fuente(8, true); var fEt = Fuente(7, true); var fFl = Fuente(8, true); var fTag = Fuente(7, false);
            Color cGris = Color.FromArgb(255, 154, 163, 174), cEt = Color.FromArgb(255, 128, 135, 147);
            Color cAviso = Color.FromArgb(240, 222, 184, 82);
            string letra = "—"; Color cLet = cGris;
            if (s.HayClima)
            {
                var mc = s.Clima;
                letra = mc.Tipo == 'T' ? (mc.LadoVwap > 0 ? "T▲" : "T▼") : mc.Tipo == 'R' ? "R" : mc.Tipo == 'E' ? "E" : "D";
                cLet = t.CCol;
            }
            string fm = FlechaPastilla(s, s.Marea), fo = FlechaPastilla(s, s.Ola), tag = AvisoCorto(s);
            var szLet = g.MeasureString(letra, fLet); var szM = g.MeasureString("MAREA", fEt); var szO = g.MeasureString("OLA", fEt);
            var szFm = g.MeasureString(fm, fFl); var szFo = g.MeasureString(fo, fFl);
            var szTag = tag.Length > 0 ? g.MeasureString(tag, fTag) : Size.Empty;
            const int pad = 7;
            int H = Math.Max(18, Math.Max(szLet.Height, szFm.Height) + 4);
            int w = pad + 7 + 6 + szLet.Width + 6 + 1 + 7 + szM.Width + 4 + szFm.Width + 10 + szO.Width + 4 + szFo.Width
                  + (tag.Length > 0 ? 10 + 5 + 4 + szTag.Width : 0) + pad;
            LugarPanel(reg, w, H, out int x0, out int y0);
            var caja = new Rectangle(x0, y0, w, H);
            bool hv = m.X >= 0 && caja.Contains(m);
            g.FillRectangle(hv ? Color.FromArgb(244, 44, 49, 59) : Color.FromArgb(236, 34, 38, 46), caja, 4);
            int x = x0 + pad, cy = y0 + H / 2;
            Color cEstado = s.Live ? Color.FromArgb(255, 0, 230, 118) : Color.FromArgb(255, 142, 59, 63);
            g.FillEllipse(cEstado, new Rectangle(x, cy - 3, 7, 7)); x += 7 + 6;
            g.DrawString(letra, fLet, cLet, x, cy - szLet.Height / 2); x += szLet.Width + 6;
            g.DrawLine(new RenderPen(Color.FromArgb(255, 62, 68, 80), 1), x, y0 + 5, x, y0 + H - 5); x += 1 + 7;
            g.DrawString("MAREA", fEt, cEt, x, cy - szM.Height / 2); x += szM.Width + 4;
            g.DrawString(fm, fFl, t.MCol, x, cy - szFm.Height / 2); x += szFm.Width + 10;
            g.DrawString("OLA", fEt, cEt, x, cy - szO.Height / 2); x += szO.Width + 4;
            g.DrawString(fo, fFl, t.OCol, x, cy - szFo.Height / 2); x += szFo.Width;
            if (tag.Length > 0)
            {
                x += 10;
                g.FillEllipse(cAviso, new Rectangle(x, cy - 2, 5, 5)); x += 5 + 4;
                g.DrawString(tag, fTag, cAviso, x, cy - szTag.Height / 2);
            }
            p.Pastilla = "LIVE " + (s.Live ? "verde" : "rojo") + " | clima " + letra + " | MAREA " + fm + " | OLA " + fo + (tag.Length > 0 ? " | " + tag : "");
            p.Textos = new[] { "CLIMA " + t.CVal + " | " + t.CDetCorto, "MAREA " + t.MVal + " | " + t.MDet, "OLA " + t.OVal + " | " + t.ODet };
            return caja;
        }

        /// <summary>La flecha de la pastilla: ▲ / ▼ con pierna, · todavia arrancando, — sin dato (el color es el de Lectura).</summary>
        private static string FlechaPastilla(Instante s, in FotoOla f) => !s.Hay ? "—" : !f.HayPierna ? "·" : f.Dir > 0 ? "▲" : "▼";

        /// <summary>La palabra de aviso de la pastilla (la mas importante): corte de datos, cinta atrasada o cargando, X que no es la automatica.</summary>
        private static string AvisoCorto(Instante s)
        {
            if (s.Corte.Length > 0) return "corte de datos";
            if (s.Atrasada) return "cinta atrasada";
            if (!s.HayCarga) return "esperando la cinta";
            if (!(s.EstadoCarga == "en vivo" || s.EstadoCarga == "completa"))
                return s.EstadoCarga.StartsWith("sin ", StringComparison.Ordinal) ? "sin cinta" : "cargando la cinta";
            if (s.FuenteX != "auto") return s.FuenteX == "fija" ? "X fija" : "X " + s.FuenteX;
            return "";
        }

        /// <summary>El cartelito con el mouse encima: en el cuadro, la X y de donde sale el dato (el pie de la 1.0b) y como se mueve; en el "–",
        /// que minimiza; en la pastilla, que un clic la abre. Abajo del cuadro (arriba si no entra).</summary>
        private void CartelPanel(RenderContext g, Rectangle reg, Rectangle caja, string zona, TextosPanel t)
        {
            var f = Fuente(7, false);
            Color c1 = Color.FromArgb(255, 214, 219, 226), cTenue = Color.FromArgb(255, 140, 148, 160), cAviso = Color.FromArgb(240, 222, 184, 82);
            string l1, l2 = ""; Color k1 = c1, k2 = cTenue;
            switch (zona)
            {
                case "menos": l1 = "Minimizar el cuadro"; break;
                case "pastilla": l1 = "Clic: abrir el cuadro  ·  arrastrar: mover"; l2 = t.Pie; k2 = t.AvisoPie ? cAviso : cTenue; break;
                default:
                    if (_panelEstilo == EstiloPanel.Clasico) { l1 = "Arrastrar: mover el cuadro"; break; }
                    l1 = t.Pie; k1 = t.AvisoPie ? cAviso : c1;
                    l2 = "Arrastrar: mover  ·  –: minimizar";
                    break;
            }
            var s1 = g.MeasureString(l1, f); var s2 = l2.Length > 0 ? g.MeasureString(l2, f) : Size.Empty;
            int wt = Math.Max(s1.Width, s2.Width) + 12, ht = s1.Height + (l2.Length > 0 ? s2.Height + 1 : 0) + 6;
            int xt = Math.Max(reg.Left + 2, Math.Min(reg.Right - 2 - wt, caja.X));
            int yt = caja.Bottom + 4;
            if (yt + ht > reg.Bottom - 2) yt = caja.Top - 4 - ht;
            yt = Math.Max(reg.Top + 2, yt);
            var r = new Rectangle(xt, yt, wt, ht);
            g.FillRectangle(Color.FromArgb(242, 14, 16, 21), r, 3);
            g.DrawRectangle(new RenderPen(Color.FromArgb(255, 58, 64, 76), 1), r, 3);
            g.DrawString(l1, f, k1, xt + 6, yt + 3);
            if (l2.Length > 0) g.DrawString(l2, f, k2, xt + 6, yt + 3 + s1.Height + 1);
        }

        /// <summary>
        /// El cuadro CLASICO de la 1.0b, sin cambios (con el estilo del encabezado del Superindicador, variante E): renglon 1 "● LIVE" (verde
        /// si es la vela en curso y la jornada sigue en vivo; rojo apagado si es un momento pasado) y la hora de la PC (la de la ultima orden
        /// que entro hasta el momento) a la derecha; renglon 2 el dia; una linea; despues CLIMA, MAREA y OLA, cada uno con su lectura y el
        /// dato fino abajo; al pie la X y de donde sale el dato. Sin sombras; fondo gris oscuro como la columna del Superindicador.
        /// </summary>
        private Rectangle DibujarPanelClasico(RenderContext g, Rectangle reg, Instante s, Pantalla p, TextosPanel t)
        {
            var fLive = Fuente(7, true); var fHora = Fuente(7.5f, false); var fDia = Fuente(8.5f, true);
            var fEt = Fuente(7, true); var fVal = Fuente(8.5f, true); var fDet = Fuente(7, false); var fPie = Fuente(6.5f, false);
            Color cGris = Color.FromArgb(255, 154, 163, 174), cEt = Color.FromArgb(255, 138, 145, 156), cBlanco = Color.FromArgb(255, 236, 239, 244);
            Color cAmbar = Color.FromArgb(245, 255, 214, 0);
            string hora = t.Hora, dia = t.Dia, pie = t.Pie, senal = t.Senal; bool avisoPie = t.AvisoPie;

            // medidas
            var filas = new[] { ("CLIMA", t.CVal, t.CCol, t.CDet), ("MAREA", t.MVal, t.MCol, t.MDet), ("OLA", t.OVal, t.OCol, t.ODet) };
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
            LugarPanel(reg, w, h, out int x0, out int y0);
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

            p.Textos = new[] { "CLIMA " + t.CVal + " | " + t.CDet, "MAREA " + t.MVal + " | " + t.MDet, "OLA " + t.OVal + " | " + t.ODet };
            return caja;
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
