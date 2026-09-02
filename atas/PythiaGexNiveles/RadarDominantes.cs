using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGex
{
    /// <summary>
    /// PythiaGex - Radar de Dominantes.
    ///
    /// Dos capas y nada mas, para que se pueda leer de un vistazo mientras se
    /// opera:
    ///
    ///   DOMINANTES  las zonas donde la mesa tiene un incentivo real HOY. No es
    ///               "el strike mas grande": es el unico que pasa los tres
    ///               filtros a la vez -- que sea grande, que venza pronto y que
    ///               el precio pueda llegar. Un muro enorme que vence en tres
    ///               semanas y esta a doscientos puntos no empuja nada hoy, y
    ///               dibujarlo como si fuera una pared es lo que hacen los
    ///               tableros publicos.
    ///
    ///   BIGTRADES   cuando entra plata grande en las opciones, en que strike y
    ///               de que lado. Se calcula restando el volumen acumulado de
    ///               dos corridas de la cadena, asi que es la cinta agrupada en
    ///               ventanas en vez de tick a tick.
    ///
    /// Juntas contestan lo que ninguna de las dos sola puede: la pared que
    /// estoy mirando, ¿la estan reforzando o se la estan comiendo?
    ///
    /// EL RETRASO SE DIBUJA, NO SE ESCONDE. El CDN de CBOE sirve el dato 902
    /// segundos tarde -- medido, no estimado. Los BigTrades de este indicador
    /// describen lo que paso hace un cuarto de hora. La cinta de arriba lo dice
    /// siempre, en numeros, para que nadie los use de gatillo de entrada.
    ///
    /// Para el gatillo en vivo estan los barridos del futuro, que ya vienen por
    /// Rithmic y viven en el otro indicador.
    /// </summary>
    [DisplayName("PythiaGex - Radar de Dominantes")]
    [Category("PythiaGex")]
    public class RadarDominantes : Indicator
    {
        // ==============================================================
        // Modelo
        // ==============================================================
        private sealed class Zona
        {
            public double Idx, Desde, Hasta;
            public double? Fut, FutDesde, FutHasta;
            public string Caracter = "", Lado = "", Criollo = "";
            public double Incentivo, GexM, Tam, Inm, Alc;
            public bool Relevante;
            /// <summary>El rol cardinal, si es una de las cuatro. Vacio si es
            /// una zona secundaria.</summary>
            public string Rol = "";
        }

        private sealed class Punto
        {
            public double Idx, GexM, Incentivo;
            public double? Fut;
        }

        private sealed class Trade
        {
            public DateTime Hora;
            public double Idx;
            public double? Fut;
            public string Tipo = "", Lado = "", Efecto = "";
            public int Contratos;
            public double Prima;
            public bool Cero;
            public int Barra = -1;
        }

        private sealed class Datos
        {
            public string Contrato = "", CadenaTs = "", HoraMercado = "";
            public double EdadMin;
            public int RetrasoS;
            public double? Spot, Base;
            public bool BaseConfiable;
            public List<Zona> Zonas = new();
            public List<Punto> Perfil = new();
            public List<Trade> Trades = new();
            public List<string> Faltan = new();
        }

        // ==============================================================
        private static readonly HttpClient Http = CrearCliente();
        private static HttpClient CrearCliente()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            c.DefaultRequestHeaders.Add("User-Agent", "PythiaGex-Radar/1.0");
            return c;
        }

        private volatile Datos _d;
        private volatile string _error = "";
        private int _bajando;
        private TimeSpan _periodo;
        private Action _tick;
        private int _ultimoMapeo = -1;

        // ==============================================================
        // Ajustes - Fuente
        // ==============================================================
        [Display(Name = "Direccion del panel", GroupName = "Fuente", Order = 10)]
        public string Url { get; set; } = "https://waltermosqueda.github.io/PythiaGex/datos/atas/";

        [Display(Name = "Instrumento (vacio = automatico)", GroupName = "Fuente", Order = 20)]
        public string RaizManual { get; set; } = "";

        [Display(Name = "Refrescar cada (segundos)", GroupName = "Fuente", Order = 30)]
        public int SegundosRefresco { get; set; } = 60;

        // ==============================================================
        // Ajustes - Dominantes
        // ==============================================================
        [Display(Name = "Ver zonas dominantes", GroupName = "Dominantes", Order = 40)]
        public bool VerZonas { get; set; } = true;

        [Display(Name = "Solo las cuatro cardinales", GroupName = "Dominantes", Order = 41)]
        public bool SoloCardinales { get; set; } = true;

        [Display(Name = "Tambien las debiles (decorativas)", GroupName = "Dominantes", Order = 42)]
        public bool VerDebiles { get; set; } = true;

        [Display(Name = "Relleno de la banda (0-100)", GroupName = "Dominantes", Order = 43)]
        public int OpacidadBanda { get; set; } = 26;

        [Display(Name = "Etiqueta con la traduccion", GroupName = "Dominantes", Order = 44)]
        public bool VerCriollo { get; set; } = true;

        [Display(Name = "Margen del eje de precios (px)", GroupName = "Dominantes", Order = 45)]
        public int MargenEje { get; set; } = 62;

        // ==============================================================
        // Ajustes - Perfil
        // ==============================================================
        [Display(Name = "Ver perfil de gamma por strike", GroupName = "Perfil", Order = 50)]
        public bool VerPerfil { get; set; } = true;

        [Display(Name = "Ancho maximo de la barra (px)", GroupName = "Perfil", Order = 51)]
        public int AnchoPerfil { get; set; } = 120;

        [Display(Name = "Perfil a la derecha", GroupName = "Perfil", Order = 52)]
        public bool PerfilDerecha { get; set; } = false;

        // ==============================================================
        // Ajustes - BigTrades
        // ==============================================================
        [Display(Name = "Ver BigTrades", GroupName = "BigTrades", Order = 60)]
        public bool VerTrades { get; set; } = true;

        [Display(Name = "Prima minima a mostrar (USD)", GroupName = "BigTrades", Order = 61)]
        public int PrimaMinima { get; set; } = 250000;

        [Display(Name = "Solo los del vencimiento de hoy", GroupName = "BigTrades", Order = 62)]
        public bool Solo0dte { get; set; } = false;

        [Display(Name = "Ancho de la marca (px)", GroupName = "BigTrades", Order = 63)]
        public int AnchoMarca { get; set; } = 14;

        // ==============================================================
        // Ajustes - Cinta
        // ==============================================================
        [Display(Name = "Ver la cinta de procedencia", GroupName = "Cinta", Order = 70)]
        public bool VerCinta { get; set; } = true;

        [Display(Name = "Cinta abajo", GroupName = "Cinta", Order = 71)]
        public bool CintaAbajo { get; set; } = false;

        // ==============================================================
        // Ajustes - Colores
        // ==============================================================
        [Display(Name = "Freno (gamma positiva)", GroupName = "Colores", Order = 80)]
        public Color ColFreno { get; set; } = Color.FromArgb(63, 191, 169);

        [Display(Name = "Acelerador (gamma negativa)", GroupName = "Colores", Order = 81)]
        public Color ColAcel { get; set; } = Color.FromArgb(232, 115, 74);

        [Display(Name = "Sin agresor claro", GroupName = "Colores", Order = 82)]
        public Color ColNeutro { get; set; } = Color.FromArgb(140, 152, 164);

        [Display(Name = "Texto", GroupName = "Colores", Order = 83)]
        public Color ColTexto { get; set; } = Color.FromArgb(225, 230, 238);

        [Display(Name = "Fondo de la cinta", GroupName = "Colores", Order = 84)]
        public Color ColFondo { get; set; } = Color.FromArgb(10, 14, 20);

        [Display(Name = "Aviso", GroupName = "Colores", Order = 85)]
        public Color ColAviso { get; set; } = Color.FromArgb(224, 163, 46);

        // ==============================================================
        public RadarDominantes() : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
            DrawAbovePrice = false;
            if (DataSeries.Count > 0 && DataSeries[0] is ValueDataSeries v)
            {
                v.IsHidden = true;
                v.VisualType = VisualMode.Hide;
                v.ShowCurrentValue = false;
            }
        }

        protected override void OnInitialize()
        {
            _periodo = TimeSpan.FromSeconds(Math.Max(20, SegundosRefresco));
            _tick = () => _ = Bajar();
            SubscribeToTimer(_periodo, _tick);
            _ = Bajar();
        }

        protected override void OnDispose()
        {
            try { if (_tick != null) UnsubscribeFromTimer(_periodo, _tick); } catch { }
        }

        protected override void OnCalculate(int bar, decimal value) { }

        // ==============================================================
        // Descarga
        // ==============================================================
        private string Raiz()
        {
            if (!string.IsNullOrWhiteSpace(RaizManual)) return RaizManual.Trim().ToUpperInvariant();
            var s = (InstrumentInfo?.Instrument ?? "").ToUpperInvariant().TrimStart('#');
            if (s.StartsWith("MNQ") || s.StartsWith("NQ")) return "NQ";
            if (s.StartsWith("M2K") || s.StartsWith("RTY")) return "RTY";
            return "ES";
        }

        private async Task Bajar()
        {
            if (Interlocked.Exchange(ref _bajando, 1) == 1) return;
            try
            {
                var b = (Url ?? "").Trim();
                if (!b.EndsWith("/")) b += "/";
                var txt = await Http.GetStringAsync(
                    b + Raiz() + "_radar.json?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    .ConfigureAwait(false);
                var d = Parsear(txt);
                if (d != null) { _d = d; _error = ""; _ultimoMapeo = -1; }
                else _error = "el archivo no se pudo interpretar";
            }
            catch (Exception e) { _error = Recortar(e.Message, 70); }
            finally
            {
                Interlocked.Exchange(ref _bajando, 0);
                try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            }
        }

        private static double? Num(JsonElement e, string k)
            => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number
               && v.TryGetDouble(out var d) ? d : (double?)null;

        private static string Txt(JsonElement e, string k)
            => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";

        private static bool Bol(JsonElement e, string k)
            => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;

        private static Zona LeerZona(JsonElement z)
        {
            return new Zona
            {
                Idx = Num(z, "idx") ?? 0,
                Fut = Num(z, "fut"),
                Desde = Num(z, "idx_desde") ?? 0,
                Hasta = Num(z, "idx_hasta") ?? 0,
                FutDesde = Num(z, "desde"),
                FutHasta = Num(z, "hasta"),
                Caracter = Txt(z, "caracter"),
                Lado = Txt(z, "lado"),
                Criollo = Txt(z, "criollo"),
                Incentivo = Num(z, "incentivo") ?? 0,
                GexM = Num(z, "gex_M") ?? 0,
                Tam = Num(z, "tam") ?? 0,
                Inm = Num(z, "inm") ?? 0,
                Alc = Num(z, "alc") ?? 0,
                Relevante = Bol(z, "relevante"),
            };
        }

        private Datos Parsear(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                var d = new Datos
                {
                    Contrato = Txt(r, "contrato"),
                    CadenaTs = Txt(r, "cadena_ts"),
                    HoraMercado = Txt(r, "hora_mercado"),
                    EdadMin = Num(r, "edad_min") ?? 0,
                    RetrasoS = (int)(Num(r, "retraso_s") ?? 902),
                    Spot = Num(r, "spot"),
                    Base = Num(r, "base"),
                    BaseConfiable = Bol(r, "base_confiable"),
                };

                // Las cuatro cardinales vienen aparte y en orden: techo, piso,
                // trampolin, resbaladilla. El backend solo manda las que
                // existen, asi que el rol se deduce del caracter y del lado y
                // no de la posicion en el arreglo.
                if (r.TryGetProperty("dominantes", out var dom) && dom.ValueKind == JsonValueKind.Array)
                    foreach (var z in dom.EnumerateArray())
                    {
                        var zz = LeerZona(z);
                        zz.Rol = Rol(zz.Caracter, zz.Lado);
                        d.Zonas.Add(zz);
                    }

                if (r.TryGetProperty("zonas", out var zs) && zs.ValueKind == JsonValueKind.Array)
                    foreach (var z in zs.EnumerateArray())
                    {
                        var zz = LeerZona(z);
                        // no repetir las que ya entraron como cardinales
                        if (d.Zonas.Any(x => Math.Abs(x.Idx - zz.Idx) < 0.01)) continue;
                        d.Zonas.Add(zz);
                    }

                if (r.TryGetProperty("perfil", out var pf) && pf.ValueKind == JsonValueKind.Array)
                    foreach (var p in pf.EnumerateArray())
                        d.Perfil.Add(new Punto
                        {
                            Idx = Num(p, "idx") ?? 0,
                            Fut = Num(p, "fut"),
                            GexM = Num(p, "gex_M") ?? 0,
                            Incentivo = Num(p, "incentivo") ?? 0,
                        });

                if (r.TryGetProperty("bigtrades", out var bt) && bt.ValueKind == JsonValueKind.Array)
                    foreach (var t in bt.EnumerateArray())
                    {
                        var h = Txt(t, "h");
                        if (!DateTime.TryParse(h, CultureInfo.InvariantCulture,
                                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                                out var hora))
                            continue;
                        d.Trades.Add(new Trade
                        {
                            Hora = hora,
                            Idx = Num(t, "idx") ?? 0,
                            Fut = Num(t, "fut"),
                            Tipo = Txt(t, "t"),
                            Lado = Txt(t, "l"),
                            Efecto = Txt(t, "e"),
                            Contratos = (int)(Num(t, "c") ?? 0),
                            Prima = Num(t, "p") ?? 0,
                            Cero = (Num(t, "z") ?? 0) > 0.5,
                        });
                    }

                if (r.TryGetProperty("faltan", out var fa) && fa.ValueKind == JsonValueKind.Array)
                    foreach (var f in fa.EnumerateArray())
                        if (f.ValueKind == JsonValueKind.String) d.Faltan.Add(f.GetString());

                return d;
            }
            catch { return null; }
        }

        private static string Rol(string caracter, string lado)
        {
            if (caracter == "freno") return lado == "arriba" ? "TECHO" : "PISO";
            return lado == "arriba" ? "TRAMPOLIN" : "RESBALADILLA";
        }

        private static string Recortar(string s, int n)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "...");

        // ==============================================================
        // Dibujo
        // ==============================================================
        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            // ATAS se traga las excepciones de los indicadores sin dejar rastro
            // en su log: un fallo se ve solo como un indicador que no dibuja.
            // Por eso todo va envuelto y el error termina en un archivo propio.
            try { Pintar(g); }
            catch (Exception e) { Registrar(e); }
        }

        private void Pintar(RenderContext g)
        {
            if (ChartInfo == null) return;
            var d = _d;
            var area = ChartArea;
            var derecha = area.Right - Math.Max(0, MargenEje);
            var izquierda = area.Left;

            if (VerCinta) Cinta(g, d, izquierda, derecha, area);
            if (d == null) return;

            // LA REGLA MAS IMPORTANTE DE TODO EL PROYECTO.
            //
            // Si la base indice->futuro no se pudo medir con confianza, aca no
            // se dibuja NADA sobre el grafico. Un nivel de SPX puesto crudo
            // sobre el ES esta unos veinte puntos corrido, y veinte puntos de
            // ES es una perdida sistematica en cada operacion. Es mejor una
            // pantalla vacia con el aviso que una pantalla llena de mentiras.
            if (!d.BaseConfiable) return;

            var cont = ChartInfo.PriceChartContainer;
            var pen = new RenderPen(ColTexto, 1f);

            if (VerZonas) Zonas(g, d, izquierda, derecha, cont);
            if (VerPerfil) Perfil(g, d, izquierda, derecha, cont);
            if (VerTrades) Trades(g, d, izquierda, derecha, cont);
        }

        /// <summary>Las bandas y sus etiquetas.</summary>
        private void Zonas(RenderContext g, Datos d, int x0, int x1, IChartContainer cont)
        {
            var fuente = new RenderFont("Arial", 10f);
            var fuenteChica = new RenderFont("Arial", 8.5f);
            var ocupado = new List<int>();

            foreach (var z in d.Zonas.OrderBy(z => -z.Incentivo))
            {
                if (SoloCardinales && string.IsNullOrEmpty(z.Rol)) continue;
                if (!VerDebiles && !z.Relevante) continue;
                if (z.FutDesde == null || z.FutHasta == null || z.Fut == null) continue;

                var col = z.Caracter == "freno" ? ColFreno : ColAcel;
                int yA, yB, yC;
                try
                {
                    yA = cont.GetYByPrice((decimal)z.FutHasta.Value, false);
                    yB = cont.GetYByPrice((decimal)z.FutDesde.Value, false);
                    yC = cont.GetYByPrice((decimal)z.Fut.Value, false);
                }
                catch { continue; }
                if (yC < area_top() - 40 || yC > area_bottom() + 40) continue;

                var arriba = Math.Min(yA, yB);
                var alto = Math.Max(3, Math.Abs(yB - yA));

                // la banda
                var relleno = Color.FromArgb(
                    Math.Max(0, Math.Min(255, OpacidadBanda * 255 / 100)),
                    col.R, col.G, col.B);
                g.FillRectangle(relleno, new Rectangle(x0, arriba, Math.Max(1, x1 - x0), alto));

                // el nucleo, que es donde de verdad esta el incentivo
                var estilo = z.Relevante ? System.Drawing.Drawing2D.DashStyle.Solid
                                         : System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawLine(new RenderPen(col, z.Relevante ? 2f : 1f, estilo), x0, yC, x1, yC);

                // la etiqueta, corrida para que no se pisen entre si
                var rot = z.Rol;
                if (string.IsNullOrEmpty(rot)) rot = z.Caracter == "freno" ? "freno" : "acelera";
                var txt = rot + "  " + z.Fut.Value.ToString("0.00", CultureInfo.InvariantCulture);
                if (!z.Relevante) txt += "  (debil)";
                var m = g.MeasureString(txt, fuente);
                var yl = yC - m.Height - 2;
                while (ocupado.Any(u => Math.Abs(u - yl) < m.Height + 2)) yl -= (int)m.Height + 3;
                ocupado.Add(yl);

                g.FillRectangle(Color.FromArgb(190, ColFondo),
                    new Rectangle(x0 + 4, yl, m.Width + 10, m.Height + 2));
                g.DrawString(txt, fuente, col, x0 + 9, yl + 1);

                if (VerCriollo && !string.IsNullOrEmpty(z.Criollo))
                {
                    var c2 = z.Criollo;
                    var m2 = g.MeasureString(c2, fuenteChica);
                    if (m2.Width < (x1 - x0) - 30)
                    {
                        g.FillRectangle(Color.FromArgb(150, ColFondo),
                            new Rectangle(x0 + 4, yl + m.Height + 2, m2.Width + 10, m2.Height + 2));
                        g.DrawString(c2, fuenteChica, ColTexto, x0 + 9, yl + m.Height + 3);
                    }
                }
            }
        }

        /// <summary>El perfil de gamma por strike, contra un borde.</summary>
        private void Perfil(RenderContext g, Datos d, int x0, int x1, IChartContainer cont)
        {
            var vivos = d.Perfil.Where(p => p.Fut != null && Math.Abs(p.GexM) > 0).ToList();
            if (vivos.Count == 0) return;
            var mayor = vivos.Max(p => Math.Abs(p.GexM));
            if (mayor <= 0) return;
            var ancho = Math.Max(20, AnchoPerfil);

            // El alto de cada barra sale del espacio entre strikes vecinos, no
            // de un numero fijo: con el grafico muy alejado las barras se
            // superponen y el perfil se vuelve una mancha.
            var paso = SepStrikes(vivos);
            int altoBarra = 5;
            try
            {
                var ya = cont.GetYByPrice((decimal)(vivos[0].Fut.Value), false);
                var yb = cont.GetYByPrice((decimal)(vivos[0].Fut.Value + paso), false);
                altoBarra = Math.Max(2, Math.Min(14, (int)(Math.Abs(ya - yb) * 0.62)));
            }
            catch { }

            foreach (var p in vivos)
            {
                int y;
                try { y = cont.GetYByPrice((decimal)p.Fut.Value, false); }
                catch { continue; }
                if (y < area_top() - 6 || y > area_bottom() + 6) continue;

                var w = Math.Max(2, (int)(Math.Abs(p.GexM) / mayor * ancho));
                var col = p.GexM >= 0 ? ColFreno : ColAcel;
                // Un strike con incentivo se ve; uno sin incentivo queda
                // apagado pero presente, porque la forma del perfil tambien
                // informa aunque cada barra suelta no decida nada.
                var alfa = p.Incentivo >= 2 ? 230 : 80;
                var r = PerfilDerecha
                    ? new Rectangle(x1 - w, y - altoBarra / 2, w, altoBarra)
                    : new Rectangle(x0, y - altoBarra / 2, w, altoBarra);
                g.FillRectangle(Color.FromArgb(alfa, col), r);
            }
        }

        private static double SepStrikes(List<Punto> ps)
        {
            var ks = ps.Select(p => p.Fut.Value).OrderBy(v => v).ToList();
            var difs = new List<double>();
            for (int i = 1; i < ks.Count; i++)
                if (ks[i] - ks[i - 1] > 0) difs.Add(ks[i] - ks[i - 1]);
            if (difs.Count == 0) return 5;
            difs.Sort();
            return difs[difs.Count / 2];
        }

        /// <summary>Las operaciones grandes, en la barra en que ocurrieron.</summary>
        private void Trades(RenderContext g, Datos d, int x0, int x1, IChartContainer cont)
        {
            if (d.Trades.Count == 0) return;
            MapearBarras(d);
            var fuente = new RenderFont("Arial", 8f);
            var mayor = d.Trades.Max(t => t.Prima);
            if (mayor <= 0) return;

            foreach (var t in d.Trades)
            {
                if (t.Prima < PrimaMinima) continue;
                if (Solo0dte && !t.Cero) continue;
                if (t.Fut == null || t.Barra < 0) continue;

                int x, y;
                try
                {
                    x = cont.GetXByBar(t.Barra, false);
                    y = cont.GetYByPrice((decimal)t.Fut.Value, false);
                }
                catch { continue; }
                if (x < x0 - 20 || x > x1 + 20) continue;
                if (y < area_top() - 6 || y > area_bottom() + 6) continue;

                // El color dice que le hace a la gamma de la mesa, que es lo
                // unico que importa para operar: 'A' la deja corta y amplifica,
                // 'M' la deja larga y amortigua.
                var col = t.Efecto == "A" ? ColAcel : t.Efecto == "M" ? ColFreno : ColNeutro;
                var w = Math.Max(4, (int)(AnchoMarca * (0.45 + 0.55 * Math.Sqrt(t.Prima / mayor))));
                var h = t.Cero ? 3 : 2;
                g.FillRectangle(Color.FromArgb(t.Cero ? 235 : 130, col),
                    new Rectangle(x - w / 2, y - h / 2, w, h));
            }
        }

        /// <summary>A que barra del grafico corresponde cada operacion.
        ///
        /// Se recalcula solo cuando entra un archivo nuevo o cuando aparecen
        /// barras nuevas: hacerlo en cada repintado recorre todas las velas por
        /// cada operacion y con cuatrocientas operaciones el grafico se arrastra.
        /// </summary>
        private void MapearBarras(Datos d)
        {
            if (_ultimoMapeo == CurrentBar) return;
            _ultimoMapeo = CurrentBar;
            foreach (var t in d.Trades) t.Barra = -1;

            var pend = d.Trades.OrderByDescending(t => t.Hora).ToList();
            int i = Math.Max(0, CurrentBar - 1), k = 0;
            while (i >= 0 && k < pend.Count)
            {
                IndicatorCandle c;
                try { c = GetCandle(i); }
                catch { break; }
                // Las velas de ATAS vienen en la hora del servidor; el archivo
                // trae UTC. Se compara en UTC de los dos lados.
                var tc = c.Time.Kind == DateTimeKind.Utc ? c.Time : c.Time.ToUniversalTime();
                while (k < pend.Count && pend[k].Hora >= tc)
                {
                    pend[k].Barra = i;
                    k++;
                }
                i--;
            }
        }

        /// <summary>La cinta de procedencia. Ningun numero de este indicador se
        /// lee sin ella, asi que va siempre y no se puede apagar por accidente
        /// junto con las capas de dibujo.</summary>
        private void Cinta(RenderContext g, Datos d, int x0, int x1, Rectangle area)
        {
            var f = new RenderFont("Arial", 9.5f);
            var lineas = new List<Tuple<string, Color>>();

            if (d == null)
            {
                lineas.Add(Tuple.Create(
                    string.IsNullOrEmpty(_error) ? "bajando el mapa..." : "sin datos: " + _error,
                    ColAviso));
            }
            else
            {
                var edad = d.EdadMin;
                var colEdad = edad > 30 ? ColAcel : ColTexto;
                lineas.Add(Tuple.Create(
                    string.Format(CultureInfo.InvariantCulture,
                        "{0}  ·  cadena de hace {1:0.#} min  ·  mercado {2}",
                        d.Contrato,
                        edad,
                        d.HoraMercado.Length >= 19 ? d.HoraMercado.Substring(11, 8) : "?"),
                    colEdad));

                lineas.Add(Tuple.Create(
                    string.Format(CultureInfo.InvariantCulture,
                        "el dato de CBOE llega {0} min tarde (medido): los BigTrades no son gatillo en vivo",
                        (int)Math.Round(d.RetrasoS / 60.0)),
                    ColAviso));

                if (!d.BaseConfiable)
                    lineas.Add(Tuple.Create(
                        "la base indice->futuro no dio confiable: no se dibuja ningun nivel",
                        ColAcel));
                else
                    lineas.Add(Tuple.Create(
                        string.Format(CultureInfo.InvariantCulture,
                            "base {0:+0.00;-0.00}  ·  {1} operaciones grandes hoy",
                            d.Base ?? 0, d.Trades.Count),
                        ColTexto));

                foreach (var q in d.Faltan)
                    lineas.Add(Tuple.Create(q, ColAviso));
            }

            var alto = 0; var ancho = 0;
            var med = lineas.Select(l => g.MeasureString(l.Item1, f)).ToList();
            foreach (var m in med) { alto += m.Height + 2; ancho = Math.Max(ancho, m.Width); }
            alto += 8; ancho += 16;

            var y = CintaAbajo ? area.Bottom - alto - 6 : area.Top + 6;
            var x = x0 + 6;
            g.FillRectangle(Color.FromArgb(215, ColFondo), new Rectangle(x, y, ancho, alto));
            g.DrawRectangle(new RenderPen(Color.FromArgb(90, ColTexto), 1f),
                new Rectangle(x, y, ancho, alto));
            var yy = y + 4;
            for (int i = 0; i < lineas.Count; i++)
            {
                g.DrawString(lineas[i].Item1, f, lineas[i].Item2, x + 8, yy);
                yy += med[i].Height + 2;
            }
        }

        private int area_top() { return ChartArea.Top; }
        private int area_bottom() { return ChartArea.Bottom; }

        private static void Registrar(Exception e)
        {
            try
            {
                var p = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ATAS", "pythiagex-radar.log");
                File.AppendAllText(p, DateTime.Now.ToString("s") + "  " + e + "\n");
            }
            catch { }
        }
    }
}
