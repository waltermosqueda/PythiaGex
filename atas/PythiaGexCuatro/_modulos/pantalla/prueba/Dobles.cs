// Dobles.cs — arnes de la pantalla (B5): un grafico de ATAS de mentira (IGraficoPantalla), un RenderContext que cuenta lo que se pinta
// y el armado de una FotoFamilia doble con valores del 08-10 (precios de NQ, strikes y conversiones plausibles).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OFT.Rendering;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;
using PythiaGexCuatro;
using PythiaGexCuatro.Familia;

namespace PruebaPantalla
{
    /// <summary>Grafico doble: 300 velas de 1 min que terminan en Ahora, area 1200x700, precio 30.900-31.100 lineal.</summary>
    sealed class GraficoDoble : IGraficoPantalla
    {
        /// <summary>4.1.4: las casillas con las que se escribieron las pruebas C (los defaults de la 4.1.3). Desde la 4.1.4 QQQ majors OI, QQQ muros OI,
        /// FAM muros OI y QQQ dom estan apagadas por defecto (pedido del operador): el doble arranca con este juego fijo para que las pruebas de siempre
        /// sigan midiendo lo mismo; los defaults nuevos los prueban A8-A10, A18 y B4.</summary>
        public static readonly string[] Visibles413 = { "MAJORS_QQQ_oi", "MUROS_NQ_oi", "MUROS_NDX_vol", "MUROS_QQQ_oi", "FAM_MUROS_oi", "ZEST_QQQ_vol", "TRES_NDX", "T_MUROS_oi",
                                                       "R20_QQQ_vol", "R20_NDX_vol", "DOMS_QQQ_vol" };
        public readonly AjustesPantalla A = new AjustesPantalla();
        public bool Abierto; public int Redibujos;
        public Rectangle AreaR = new Rectangle(0, 0, 1200, 700);
        public string Instr = "MNQZ6";
        public double Alto = 31100, Bajo = 30900;
        public int N = 300, Primera = 200, Ultima = 299;
        public DateTime Ahora;
        public double UltimoCierre = 31000.25;
        public int LlamadasVela;
        /// <summary>4.1.4: pixeles por vela (el caso real del operador: ~2 px por vela de 1 min en 781 px visibles).</summary>
        public int PxVela = 10;
        /// <summary>Revision 4.1.4: segundos por vela (60 = 1 min, lo de siempre; 5 = un grafico de 5 s para la prueba del largo minimo en velas m2).</summary>
        public int SegVela = 60;

        public GraficoDoble(DateTime ahora)
        {
            Ahora = ahora;
            foreach (var id in Visibles413) A.Visibles.Add(id);
        }

        public void Ajustes(AjustesPantalla d)
        {
            d.Visibles.Clear(); foreach (var x in A.Visibles) d.Visibles.Add(x);
            d.Eje = A.Eje; d.Estela = A.Estela; d.Rotulos = A.Rotulos; d.Cabecera = A.Cabecera; d.PanelAbierto = Abierto; d.Letra = A.Letra; d.MargenSup = A.MargenSup;
            d.Montos = A.Montos; d.Cambios = A.Cambios; d.Ventana = A.Ventana;      // 4.1.2
            d.Tramos = A.Tramos;                                                     // 4.1.4
        }
        public bool PanelAbierto { get => Abierto; set => Abierto = value; }
        public bool Contenedor(out Rectangle area) { area = AreaR; return true; }
        public string Instrumento => Instr;
        public int PrimeraVisible => Primera;
        public int UltimaVisible => Ultima;
        public int BarraActual => N;
        public double PrecioAlto => Alto;
        public double PrecioBajo => Bajo;
        public int YDePrecio(double p) => double.IsNaN(p) ? int.MinValue : AreaR.Top + (int)Math.Round((Alto - p) / (Alto - Bajo) * AreaR.Height);
        public int XDeBarra(int b) => AreaR.Left + 20 + (b - Primera) * PxVela;
        /// <summary>Vela b: abre en Ahora - (N - b) min, LastTime = apertura + 59 s (ms UTC crudos). Revision 4.1.4: con SegVela != 60, abre en
        /// Ahora - (N - b) x SegVela s y LastTime = apertura + SegVela - 1 s (con 60 es lo mismo de siempre).</summary>
        public bool Vela(int b, out long ms, out double cierre)
        {
            LlamadasVela++;
            ms = 0; cierre = double.NaN;
            if (b < 0 || b >= N) return false;
            var t = SegVela == 60 ? Ahora.AddMinutes(-(N - b)).AddSeconds(59) : Ahora.AddSeconds(-(long)(N - b) * SegVela + SegVela - 1);
            ms = Prueba.Ms(t);
            cierre = b == N - 1 ? UltimoCierre : 31000;
            return true;
        }
        public void Redibujar() { Redibujos++; }
    }

    /// <summary>RenderContext doble: cuenta lo que se pinta y mide el texto con una letra monoespaciada aproximada.</summary>
    sealed class RenderDoble : RenderContext
    {
        public int Rellenos, Bordes, Lineas, Textos;
        public readonly List<string> Strings = new List<string>();
        public readonly List<(string S, Color C, int X, int Y, float F)> TextosDet = new List<(string, Color, int, int, float)>();   // 4.1.2: color, lugar y letra de cada texto
        protected override string Name => "doble";
        /// <summary>Revision 4.1.2: el clip es elegible (el grafico del operador: area 852 px y clip.derecha 781, pythiagex4-pantalla.log).</summary>
        public Rectangle Clip = new Rectangle(0, 0, 1180, 700);
        public override Rectangle ClipBounds => Clip;
        public override int SetInterpolationMode(RenderInterpolationModes mode) => 0;
        public override int SetSmoothingMode(RenderSmoothingModes mode) => 0;
        public override int DrawLine(RenderPen pen, int x1, int y1, int x2, int y2) { Lineas++; return 0; }
        public override int DrawLines(RenderPen pen, Point[] points) => 0;
        public override int DrawRectangle(RenderPen pen, Rectangle r) { Bordes++; return 0; }
        public override int DrawRectangle(RenderPen pen, Rectangle r, int radius) { Bordes++; return 0; }
        public override int DrawEllipse(RenderPen pen, Rectangle r) => 0;
        public override int DrawPolygon(RenderPen pen, Point[] points) => 0;
        public override int Clear(Color c) => 0;
        public override int FillRectangle(Color c, Rectangle r) { Rellenos++; return 0; }
        public override int FillRectangle(Color c, Rectangle r, int radius) { Rellenos++; return 0; }
        public override int FillRectangle(Color c1, Color c2, Rectangle r, bool vertical) { Rellenos++; return 0; }
        public override int FillPolygon(Color c, Point[] points) => 0;
        public override int FillPolygon(Color c1, Color c2, Point[] points, bool vertical) => 0;
        public override int FillEllipse(Color c, Rectangle r) => 0;
        public override int FillPie(Color c, Rectangle r, float a, float b) => 0;
        public override int DrawString(string s, RenderFont f, Color c, int x, int y) { Textos++; Strings.Add(s); TextosDet.Add((s, c, x, y, f?.Size ?? 0)); return 0; }
        public override int DrawString(string s, RenderFont f, Color c, int x, int y, RenderStringFormat fmt) { Textos++; Strings.Add(s); return 0; }
        public override int DrawString(string s, RenderFont f, Color c, Rectangle r) { Textos++; Strings.Add(s); return 0; }
        public override int DrawString(string s, RenderFont f, Color c, Rectangle r, RenderStringFormat fmt) { Textos++; Strings.Add(s); return 0; }
        public override Size MeasureString(string s, RenderFont f) => Prueba.Medir(s, f.Size);
        public override Rectangle MeasureStringWithOverhang(string s, RenderFont f) => new Rectangle(Point.Empty, Prueba.Medir(s, f.Size));
        public override int DrawStaticImage(Image image, Rectangle r) => 0;
        public override int DrawStaticImage(IBitmapImage image, Rectangle r) => 0;
        public override int DrawLayout(DrawingLayout layout, Point p) => 0;
        public override int SetClip(Rectangle r) => 0;
        public override int ResetClip() => 0;
        public override int TranslateTransform(int x, int y) => 0;
        public override RenderContext CreateLayoutRenderContext(DrawingLayout layout) => this;
        public override int DrawSmoothedLine(RenderPen pen, Point[] points) => 0;
        public override int DrawQuadraticSpline(RenderPen pen, Point[] points) => 0;
    }

    static class FotosDoble
    {
        /// <summary>4.1.2: OI vigente (R0) y anterior (R1) de los libros de CBOE en el doble (la OCC: QQQ salta 07:31-09:12 UTC una vez por noche).</summary>
        public static readonly DateTime OiR0 = new DateTime(2026, 10, 8, 7, 40, 0, DateTimeKind.Utc), OiR1 = new DateTime(2026, 10, 7, 7, 31, 0, DateTimeKind.Utc);
        /// <summary>NQ: saltos de OI medidos 10-07 ~01:24Z y 10-08 ~01:30Z.</summary>
        public static readonly DateTime OiNqR0 = new DateTime(2026, 10, 8, 1, 30, 0, DateTimeKind.Utc), OiNqR1 = new DateTime(2026, 10, 7, 1, 24, 0, DateTimeKind.Utc);

        /// <summary>La foto "normal": libros con 40 s (NQ) y 16 min (NDX/QQQ, CBOE llega 15 min tarde), TQQQ 15 min, sin avisos.
        /// 4.1.2: montos realistas (magnitudes de niv-2026-10-09-MNQZ6.jsonl: MUROS_NQ_oi +89,7/−81,8; MUROS_NDX_vol −249,6; MAJORS_QQQ_oi +108,5;
        /// FAM_MUROS_oi −510,8) y NaN donde la familia no trae monto (ZEST, ZTP, CONF, TRES). Cambios como los anotaria CambiosFamilia:
        /// MAJORS_QQQ_oi ▲12 (OI), FAM_MUROS_oi ▼20 (OI, se achica), MUROS_NDX_vol P ▲3,1 (vol 15 min, cobertura 90 %), MUROS_NQ_oi C ▲5,2 pero
        /// con OI viejo (no va en la etiqueta), MUROS_NDX_vol C ventana real 16 min, T_MUROS_oi C sin la sesion anterior (nota).</summary>
        public static FotoFamilia Normal(DateTime ahora, double edadCboeMin = 16, bool conHist = true)
        {
            var hist = new Dictionary<long, IReadOnlyDictionary<string, double[]>>();
            if (conHist)
            {
                long fin = Prueba.Ms(ahora) / 120000 * 120000;
                for (long T = fin - 200 * 60000L; T <= fin; T += 120000)
                    hist[T] = new Dictionary<string, double[]>
                    {
                        ["MUROS_NQ_oi"] = new[] { 31060.0, 30950.0 },
                        ["MAJORS_QQQ_oi"] = new[] { 31073.31 },
                        ["TRES_NDX"] = new[] { 31020.0 },
                        ["MUROS_NQ_vol"] = new[] { 31040.0 },                 // apagada por defecto: no se dibuja
                        ["T_MUROS_oi"] = new[] { 31073.31, 30949.10 },
                    };
            }
            DateTime nq = ahora.AddSeconds(-40), cb = ahora.AddMinutes(-edadCboeMin), tq = ahora.AddMinutes(-15);
            var act = new List<NivelActual>
            {
                Oi(A("MAJORS_QQQ_oi", "QQQ", "oi", "MAJORS", "M+", 31073.31, 750, 20, cb, gexM: 108.6), 12.4, OiR1, OiR0, cob: 1.0),
                Oi(A("T_MUROS_oi", "TQQQ", "oi", "MUROS", "muro C", 31073.31, 81, 4, tq, gexM: 3.4), double.NaN, DateTime.MinValue, DateTime.MinValue,
                   nota: "OI sin la sesion anterior en memoria"),
                Oi(A("MUROS_NQ_oi", "NQ", "oi", "MUROS", "muro C", 31060, 31060, 0, nq, oiViejo: true, gexM: 89.7), 5.2, OiNqR1, OiNqR0),   // OI viejo: no va
                A("MUROS_NQ_oi", "NQ", "oi", "MUROS", "muro P", 30950, 30950, 0, nq, oiViejo: true, gexM: -81.8),
                Vol(A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro C", 31060.5, 30818.88, 0, cb, gexM: 0.4), cb,
                    new[] { 0.1, 0.2, double.NaN }, new[] { 300.0, 960.0, double.NaN }, new[] { 1.0, 1.0, double.NaN }),       // ventana real 16 min
                A("TRES_NDX", "NDX", "3.0", "TRES", "D1", 31020, double.NaN, 0, cb),                                           // sin monto (sin "gm")
                A("MUROS_NQ_vol", "NQ", "vol", "MUROS", "muro C", 31040, 31040, 0, nq, gexM: 47.1),                            // apagada: no va
                Oi(A("FAM_MUROS_oi", "familia", "oi", "MUROS", "muro P", 30990, double.NaN, 0, cb, gexM: -510.8), 20.3, OiR1, OiR0, cob: 0.97),  // ▼ se achica
                Vol(A("MUROS_NDX_vol", "NDX", "vol", "MUROS", "muro P", 30981.62, 30740, 0, cb, gexM: -249.6), cb,
                    new[] { -1.2, -3.1, -5.0 }, new[] { 300.0, 885.0, 1800.0 }, new[] { 1.0, 0.9, 0.8 }),                      // ▲ crece (puts mas negativos)
                Oi(A("T_MUROS_oi", "TQQQ", "oi", "MUROS", "muro P", 30949.10, 80, 4, tq, gexM: -2.1), -0.6, OiR1, OiR0),
                A("ZEST_QQQ_vol", "QQQ", "vol", "ZEST", "0G est.", 31500, 760.2, 0, cb),        // fuera de pantalla (arriba): flecha; zero sin monto
            };
            foreach (var n in act) ComoCambios(n);      // revision 4.1.2: TODOS anotados como CambiosFamilia (los zeros y la 3.0 con arreglos NaN)
            var fu = new List<FuenteEstado>
            {
                new FuenteEstado { Libro = "NQ", Texto = "mismo contrato", ConvValor = 0, DatoUtc = nq },
                new FuenteEstado { Libro = "NDX", Texto = "base 241.62", ConvValor = 241.62, DatoUtc = cb },
                new FuenteEstado { Libro = "QQQ", Texto = "razon 41.4331", ConvValor = 41.4331, DatoUtc = cb },
                new FuenteEstado { Libro = "TQQQ", Texto = "x3: NQ = 124.21 x TQQQ + 21012.1 (+-4)", DatoUtc = tq },
                new FuenteEstado { Libro = "CBOE", Texto = "QQQ hace 40 s · NDX hace 1 min · TQQQ hace 1 min" },
                new FuenteEstado { Libro = "cinta", Texto = "ultimo tick hace 2 s", DatoUtc = ahora.AddSeconds(-2) },
            };
            return new FotoFamilia
            {
                Sesion = "2026-10-08", CalculadoUtc = ahora.AddSeconds(-3), HistoriaM2 = hist, Actuales = act, Fuentes = fu,
                TqS = 124.21, TqC = 21012.06, TqDatoUtc = tq, Instrumento = "Z6", Aviso = "",
                CambiosEstado = new[] { "NQ: vol 15 min 18:44-18:59Z; OI 10-08 01:30Z vs 10-07 01:24Z (OI de 2 sesiones)",
                                        "NDX: vol 15 min 18:29-18:44Z; OI 10-08 07:40Z vs 10-07 07:31Z" }
            };
        }

        /// <summary>Un nivel. 4.1.2: GexM por defecto NaN (antes 1,5 en todas: irreal; ZEST/ZTP/CONF/TRES no traen monto).</summary>
        public static NivelActual A(string serie, string libro, string fuente, string tipo, string rol, double precio, double strike, double banda, DateTime dato,
                                    bool oiViejo = false, double gexM = double.NaN)
            => new NivelActual { Serie = serie, Libro = libro, Fuente = fuente, Tipo = tipo, Rol = rol, Precio = precio, Strike = strike, GexM = gexM, Banda = banda, DatoUtc = dato, OiViejo = oiViejo };

        /// <summary>Anota el cambio por volumen de las tres ventanas (5/15/30 min) como CambiosFamilia: la foto de "ahora" = hasta, la de antes = hasta - seg.</summary>
        public static NivelActual Vol(NivelActual n, DateTime hasta, double[] dm, double[] seg, double[] cob, string nota = "")
        {
            n.CambioVolM = dm; n.CambioVolSegReal = seg;
            n.CambioVolHastaUtc = new DateTime[dm.Length]; n.CambioVolDesdeUtc = new DateTime[dm.Length];
            for (int i = 0; i < dm.Length; i++)
            {
                bool hay = !double.IsNaN(seg[i]);
                n.CambioVolHastaUtc[i] = hay ? hasta : DateTime.MinValue;
                n.CambioVolDesdeUtc[i] = hay ? hasta.AddSeconds(-seg[i]) : DateTime.MinValue;
            }
            n.CambioCobertura = new[] { cob[0], cob[1], cob[2], double.NaN };
            n.CambioNota = nota;
            return n;
        }

        /// <summary>Revision 4.1.2: deja el nivel como lo deja CambiosFamilia.AnotarNivel (posiciones/CambiosFamilia.cs:400-410): TODOS los niveles,
        /// tambien ZEST/ZTP/CONF/TRES que no tienen cambio, salen con arreglos de NaN (3 ventanas; cobertura 3 + dia), fechas MinValue y nota "".
        /// Lo que el doble ya anoto (Vol/Oi) se conserva. Antes el doble dejaba null en los zeros y C72 pasaba por eso.</summary>
        public static NivelActual ComoCambios(NivelActual n)
        {
            if (n == null) return null;
            int nv = CambiosVentanas.Min.Length;
            if (n.CambioVolM == null) n.CambioVolM = Enumerable.Repeat(double.NaN, nv).ToArray();
            if (n.CambioVolSegReal == null) n.CambioVolSegReal = Enumerable.Repeat(double.NaN, nv).ToArray();
            if (n.CambioVolDesdeUtc == null) n.CambioVolDesdeUtc = Enumerable.Repeat(DateTime.MinValue, nv).ToArray();
            if (n.CambioVolHastaUtc == null) n.CambioVolHastaUtc = Enumerable.Repeat(DateTime.MinValue, nv).ToArray();
            if (n.CambioCobertura == null) n.CambioCobertura = Enumerable.Repeat(double.NaN, nv + 1).ToArray();
            if (n.CambioNota == null) n.CambioNota = "";
            return n;
        }

        /// <summary>Anota el cambio de OI del dia (publicacion vigente R0 contra la anterior R1).</summary>
        public static NivelActual Oi(NivelActual n, double dm, DateTime r1, DateTime r0, double cob = double.NaN, string nota = "")
        {
            n.CambioOiDiaM = dm; n.CambioOiDesdeUtc = r1; n.CambioOiHastaUtc = r0;
            n.CambioCobertura = new[] { double.NaN, double.NaN, double.NaN, cob };
            n.CambioNota = nota;
            return n;
        }

        /// <summary>4.1.2: copia con FotoFamilia.Copia() (la regla del contrato: nunca campo por campo, se perderian los campos nuevos).</summary>
        public static FotoFamilia Copia(FotoFamilia f, Action<FotoFamilia> cambio)
        {
            var c = f.Copia();
            cambio(c); return c;
        }

        /// <summary>La foto normal con los niveles cambiados (cada nivel es una COPIA: la foto original no se toca).</summary>
        public static FotoFamilia ConNiveles(FotoFamilia f, Func<NivelActual, bool> donde, Action<NivelActual> cambio)
            => Copia(f, x => x.Actuales = f.Actuales.Select(n => { if (n == null || !donde(n)) return n; var c = n.Copia(); cambio(c); return c; }).ToList());

        /// <summary>La foto normal mas niveles.</summary>
        public static FotoFamilia Mas(FotoFamilia f, params NivelActual[] extra) => Copia(f, x => x.Actuales = f.Actuales.Concat(extra).ToList());
    }
}
