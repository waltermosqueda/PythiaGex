using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using ATAS.DataFeedsCore;
using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGex
{
    /// <summary>
    /// PythiaGex - Sonda de opciones.
    ///
    /// Contesta UNA pregunta, la que decide todo el proyecto:
    ///
    ///     ¿puede un indicador leer la cadena de opciones de CME en vivo,
    ///      la que ATAS ya recibe por Rithmic?
    ///
    /// Ya esta confirmado por reflexion, sin abrir la plataforma, que
    /// OFT.Rithmic.RithmicConnector implementa ATAS.DataFeedsCore.IOptionsDataFeed.
    /// O sea que el dato ENTRA. Lo que falta saber es si el contenedor de
    /// servicios se lo entrega a un indicador, y con que frescura.
    ///
    /// Importa porque el CDN de CBOE llega 902 segundos tarde -- medido, 14 de
    /// 14 corridas, sin dispersion. Con ese dato no se puede construir un
    /// gatillo de entrada. Con la cadena de Rithmic, si.
    ///
    /// La sonda prueba varios caminos, escribe TODO a un archivo de texto y
    /// ademas lo muestra en pantalla. No dibuja nada sobre el grafico y no
    /// toca ninguna otra cosa.
    ///
    /// El informe queda en:
    ///     %APPDATA%\ATAS\pythiagex-sonda.txt
    /// </summary>
    [DisplayName("PythiaGex - Sonda de opciones")]
    [Category("PythiaGex")]
    public class SondaOpciones : Indicator
    {
        private readonly List<string> _lineas = new();
        private bool _corrida;
        private string _archivo = "";

        [Display(Name = "Volver a probar", GroupName = "Sonda", Order = 10,
                 Description = "Cambialo a mano para forzar otra corrida.")]
        public bool Reintentar
        {
            get => false;
            set { if (value) { _corrida = false; _lineas.Clear(); } }
        }

        [Display(Name = "Cuantos contratos listar", GroupName = "Sonda", Order = 20)]
        public int MuestraContratos { get; set; } = 12;

        public SondaOpciones() : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
            if (DataSeries.Count > 0 && DataSeries[0] is ValueDataSeries v)
            {
                v.IsHidden = true;
                v.VisualType = VisualMode.Hide;
                v.ShowCurrentValue = false;
            }
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            if (_corrida) return;
            _corrida = true;
            _ = Task.Run(Probar);
        }

        private void L(string s)
        {
            lock (_lineas) _lineas.Add(s);
        }

        private async Task Probar()
        {
            try
            {
                L("SONDA DE OPCIONES - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                L("instrumento del grafico: " + (InstrumentInfo?.Instrument ?? "?"));
                L("");

                // ---------- 1. el tipo existe ----------
                var tOpt = Type.GetType("ATAS.DataFeedsCore.IOptionsDataFeed, ATAS.DataFeedsCore");
                L("1) tipo IOptionsDataFeed .......... " + (tOpt != null ? "existe" : "NO EXISTE"));
                if (tOpt == null) { Volcar(); return; }

                // ---------- 2. GetService del proveedor del indicador ----------
                object feed = null;
                try
                {
                    var mi = typeof(IIndicatorDataProvider).GetMethod("GetService");
                    feed = mi?.MakeGenericMethod(tOpt).Invoke(DataProvider, null);
                    L("2) DataProvider.GetService ........ " +
                      (feed != null ? "SI -> " + feed.GetType().FullName
                                    : "devolvio null (no registrado para indicadores)"));
                }
                catch (TargetInvocationException e)
                {
                    // Invoke envuelve la excepcion real; sin desenvolverla el
                    // mensaje no dice nada util.
                    var i = e.InnerException;
                    L("2) DataProvider.GetService ........ NO: " +
                      (i != null ? i.GetType().Name + " - " + i.Message : e.Message));
                }
                catch (Exception e)
                {
                    L("2) DataProvider.GetService ........ NO: " + e.GetType().Name + " - " + e.Message);
                }

                // ---------- 3. por el TradingManager y su Security ----------
                Security sec = null;
                try
                {
                    sec = TradingManager?.Security;
                    L("3) TradingManager.Security ........ " +
                      (sec != null ? sec.Code + "  (" + sec.Exchange + ", conector " + sec.ConnectorId + ")"
                                   : "null"));
                }
                catch (Exception e) { L("3) TradingManager.Security ........ error: " + e.Message); }

                // ---------- 4. rastrear el conector por reflexion ----------
                if (feed == null)
                {
                    L("");
                    L("   GetService no lo dio. Se busca el conector por reflexion,");
                    L("   recorriendo los campos privados del proveedor y del manager.");
                    feed = Rastrear(DataProvider, tOpt, "DataProvider", 0)
                        ?? Rastrear(TradingManager, tOpt, "TradingManager", 0)
                        ?? Rastrear(sec, tOpt, "Security", 0);
                    L("4) rastreo por reflexion .......... " +
                      (feed != null ? "SI -> " + feed.GetType().FullName : "no se encontro"));
                }

                if (feed == null)
                {
                    L("");
                    L("RESULTADO: la cadena de opciones NO es alcanzable desde un indicador.");
                    L("El conector de Rithmic la implementa, pero el contenedor no la entrega.");
                    Volcar();
                    return;
                }

                // ---------- 5. el conector, el futuro y las opciones ----------
                var conn = feed as IDataFeedConnector;
                L("");
                L("5) el conector como IDataFeedConnector ... " + (conn != null ? "si" : "NO"));
                if (conn == null) { Volcar(); return; }
                L("   conectado: " + conn.IsConnected + "   licencia completa: " + conn.IsFullLicense);

                var raizB = (InstrumentInfo?.Instrument ?? "").ToUpperInvariant().TrimStart('#');
                raizB = new string(raizB.TakeWhile(char.IsLetter).ToArray());
                if (raizB.Length == 0) raizB = "MNQ";

                // EL CATALOGO LOCAL PRIMERO. Securities es lo que el conector ya
                // tiene cargado; suele ser poquito, pero de ahi sale el futuro.
                List<Security> todas = new();
                try { todas = (conn.Securities ?? Enumerable.Empty<Security>()).ToList(); }
                catch (Exception e) { L("   no se pudo leer Securities: " + e.Message); }
                L("   Securities en el catalogo local ... " + todas.Count);
                foreach (var x in todas.Take(10))
                    L("     " + (x.Code ?? "?").PadRight(16) + x.Type + "   " + x.Exchange);

                if (sec == null)
                    sec = todas.Where(x => x.Type == SecType.Future
                                      && (x.Code ?? "").ToUpperInvariant().StartsWith(raizB))
                               .OrderBy(x => x.Expiration).FirstOrDefault()
                          ?? todas.FirstOrDefault(x => x.Type == SecType.Future);
                L("   futuro elegido .................... " + (sec?.Code ?? "ninguno"));

                // BUSCAR EN EL SERVIDOR. Cada variante del filtro va en su propio
                // try: la version anterior pasaba un filtro incompleto y
                // SearchSecuritiesAsync tiraba NullReference sin decir por que.
                List<Security> ops = new();
                async Task Probar(string etiqueta, SecurityFilter f)
                {
                    // Solo cuenta como cadena si trae STRIKES. Una busqueda
                    // que devuelve el futuro mismo daba Count=1 y bloqueaba el
                    // camino por serie, que es el unico que trajo los 454.
                    if (ops.Count(z => z.StrikePrice.HasValue) > 0) return;
                    try
                    {
                        var r = await conn.SearchSecuritiesAsync(f).ConfigureAwait(false);
                        var l = (r ?? Enumerable.Empty<Security>()).ToList();
                        L("   buscar [" + etiqueta + "] -> " + l.Count);
                        if (l.Count(z => z.StrikePrice.HasValue) > 0) ops = l;
                    }
                    catch (Exception e)
                    {
                        L("   buscar [" + etiqueta + "] fallo: " +
                          e.GetType().Name + " - " + Recortar(e.Message, 70));
                    }
                }

                L("");
                await Probar("Option + ContractCode=" + raizB,
                    new SecurityFilter { Type = SecType.Option, ContractCode = raizB });
                await Probar("Option + Code=" + raizB,
                    new SecurityFilter { Type = SecType.Option, Code = raizB });
                await Probar("Option + Code + Exchange=CME",
                    new SecurityFilter { Type = SecType.Option, Code = raizB, Exchange = "CME" });
                if (sec != null)
                    await Probar("Option + ContractCode=" + sec.Code,
                        new SecurityFilter { Type = SecType.Option, ContractCode = sec.Code });

                // CAMINO DE RESPALDO: la serie de opciones del futuro.
                if (ops.Count(z => z.StrikePrice.HasValue) == 0 && sec != null)
                {
                    try
                    {
                        var ss = await ((dynamic)feed).GetOptionSeriesAsync(sec);
                        var series = ((IEnumerable<OptionSeries>)ss).OrderBy(z => z.Expiration).ToList();
                        L("   por serie del futuro -> " + series.Count + " vencimientos");
                        foreach (var serie in series)
                        {
                            var cc = await ((dynamic)feed).GetOptionsAsync(serie);
                            var l = ((IEnumerable<Security>)cc).ToList();
                            ops.AddRange(l);
                            L("     " + serie.Expiration.ToString("yyyy-MM-dd") + " (" + serie.Type + ") -> " + l.Count);
                        }
                    }
                    catch (Exception e)
                    { L("   camino por serie fallo: " + Recortar(e.Message, 70)); }
                }

                if (ops.Count == 0) { L("   sin contratos, no se puede seguir"); Volcar(); return; }

                var vencs = ops.Select(o => o.Expiration.Date).Distinct().OrderBy(d => d).ToList();
                L("");
                L("6) contratos totales ............. " + ops.Count);
                L("   vencimientos distintos ........ " + vencs.Count);
                foreach (var v in vencs.Take(12))
                    L("     " + v.ToString("yyyy-MM-dd") + "   " + ops.Count(o => o.Expiration.Date == v));

                // ---------- 7. SUSCRIBIRSE, que es lo que faltaba ----------
                //
                // Las opciones volvieron con interes abierto y precios en CERO.
                // No estan vacias: el feed no manda datos de un instrumento al
                // que nadie se suscribio. Se piden la cinta (Prints), las puntas
                // (Best) y el resumen (Summary, donde viaja el interes abierto).
                // Se toma una ventana alrededor del dinero y no las 454: pedir
                // todo de golpe es maltratar el feed sin necesidad.
                var px = (decimal)Precio();
                var cerca = ops.Where(o => o.StrikePrice.HasValue)
                               .OrderBy(o => Math.Abs((o.StrikePrice ?? 0) - px))
                               .Take(60).ToList();
                L("");
                L("7) suscribiendo " + cerca.Count + " contratos alrededor de " + px.ToString("0.##"));
                try
                {
                    conn.SubscribeToMarketData(cerca,
                        SubscriptionType.Prints | SubscriptionType.Best | SubscriptionType.Summary);
                    L("   suscripcion enviada (Prints | Best | Summary)");
                }
                catch (Exception e) { L("   la suscripcion fallo: " + Recortar(e.Message, 70)); }

                for (int intento = 1; intento <= 4; intento++)
                {
                    Volcar();
                    await Task.Delay(15000).ConfigureAwait(false);
                    L("   t+" + (intento * 15) + "s  con OI: " + cerca.Count(x => (x.OpenInterest ?? 0) > 0) +
                      "   con ultimo: " + cerca.Count(x => (x.LastTradePrice ?? 0) > 0) +
                      "   con bid: " + cerca.Count(x => x.BestBidPrice > 0) +
                      "   con ask: " + cerca.Count(x => x.BestAskPrice > 0));
                }

                L("");
                L("   los ocho mas cercanos al dinero:");
                L("   " + "codigo".PadRight(22) + "tipo  strike     OI    ultimo    bid    ask");
                foreach (var c in cerca.Take(8))
                    L("   " + (c.Code ?? "").PadRight(22) +
                      (c.OptionType?.ToString() ?? "?").PadRight(6) +
                      F(c.StrikePrice).PadLeft(8) + F(c.OpenInterest).PadLeft(8) +
                      F(c.LastTradePrice).PadLeft(9) + F(c.BestBidPrice).PadLeft(7) +
                      F(c.BestAskPrice).PadLeft(7));

                bool hayDato = cerca.Any(x => (x.OpenInterest ?? 0) > 0 || x.BestBidPrice > 0
                                              || (x.LastTradePrice ?? 0) > 0);
                L("");
                L(hayDato
                  ? "RESULTADO: HAY CADENA DE OPCIONES EN VIVO POR RITHMIC."
                  : "RESULTADO: la cadena se lista pero NO llega dato de mercado.");
                Volcar();
            }
            catch (Exception e)
            {
                L("");
                L("EXPLOTO: " + e);
                Volcar();
            }
        }

        private double Precio()
        {
            try { return (double)GetCandle(Math.Max(0, CurrentBar - 1)).Close; }
            catch { return 0; }
        }

        private static string Recortar(string s, int n)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "...");

        private static string F(decimal? v)
            => v.HasValue ? v.Value.ToString("0.##", CultureInfo.InvariantCulture) : "-";

        /// <summary>Busca un objeto que implemente la interfaz pedida, bajando por
        /// los campos privados. Profundidad corta a proposito: mas hondo se
        /// entra en grafos ciclicos y no aporta.</summary>
        private object Rastrear(object raiz, Type buscada, string camino, int nivel)
        {
            if (raiz == null || nivel > 3) return null;
            try
            {
                if (buscada.IsInstanceOfType(raiz)) return raiz;
                var t = raiz.GetType();
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic |
                                              BindingFlags.Public))
                {
                    if (f.FieldType.IsPrimitive || f.FieldType == typeof(string)) continue;
                    object v;
                    try { v = f.GetValue(raiz); } catch { continue; }
                    if (v == null) continue;
                    if (buscada.IsInstanceOfType(v))
                    {
                        L("     encontrado en " + camino + "." + f.Name);
                        return v;
                    }
                    var r = Rastrear(v, buscada, camino + "." + f.Name, nivel + 1);
                    if (r != null) return r;
                }
            }
            catch { }
            return null;
        }

        private void Volcar()
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS");
                Directory.CreateDirectory(dir);
                _archivo = Path.Combine(dir, "pythiagex-sonda.txt");
                string txt;
                lock (_lineas) txt = string.Join(Environment.NewLine, _lineas);
                File.WriteAllText(_archivo, txt, Encoding.UTF8);
            }
            catch (Exception e)
            {
                lock (_lineas) _lineas.Add("no se pudo escribir el archivo: " + e.Message);
            }
            try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
        }

        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            try
            {
                List<string> ls;
                lock (_lineas) ls = new List<string>(_lineas);
                if (ls.Count == 0) ls.Add("probando...");
                if (!string.IsNullOrEmpty(_archivo))
                    ls.Add("--- informe escrito en " + _archivo);

                var f = new RenderFont("Consolas", 11f);
                int w = 0, h = 0;
                var med = ls.Select(s => g.MeasureString(s, f)).ToList();
                foreach (var m in med) { w = Math.Max(w, m.Width); h += m.Height + 1; }
                var x = ChartArea.Left + 10;
                var y = ChartArea.Top + 10;
                g.FillRectangle(Color.FromArgb(235, 8, 12, 18),
                    new Rectangle(x, y, w + 20, h + 14));
                g.DrawRectangle(new RenderPen(Color.FromArgb(120, 200, 210, 220), 1f),
                    new Rectangle(x, y, w + 20, h + 14));
                var yy = y + 7;
                for (int i = 0; i < ls.Count; i++)
                {
                    var c = ls[i].StartsWith("RESULTADO") ? Color.FromArgb(120, 230, 170)
                          : ls[i].StartsWith("EXPLOTO") ? Color.FromArgb(240, 120, 100)
                          : Color.FromArgb(220, 228, 236);
                    g.DrawString(ls[i], f, c, x + 10, yy);
                    yy += med[i].Height + 1;
                }
            }
            catch { }
        }
    }
}
