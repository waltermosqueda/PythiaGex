using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;

using ATAS.Indicators;
using ATAS.DataFeedsCore;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PatronOperador
{
    /// <summary>
    /// PATRON OPERADOR - EN PRUEBA, NO VALIDADO.
    ///
    /// QUE ES. Las reglas que salieron de la ronda 9 (las que mas se parecen a lo que el operador
    /// hace y a lo que el mercado paga en esos momentos), dibujadas en vivo sobre su grafico de
    /// MNQ de 2 minutos. NO OPERA NADA y no manda ordenes: marca la situacion y la anota.
    ///
    /// LO QUE HAY QUE SABER ANTES DE MIRARLO (y por eso el rotulo grande dice EN PRUEBA):
    /// el juez de la ronda 9 corrio las 17 reglas candidatas sobre 24 sesiones que el operador NO
    /// opero (31-07 a 04-09, catorce de la reserva que nadie habia mirado) y NINGUNA gana al costo:
    /// las 34 corridas dan neto negativo, entre -0,76 y -1,44 puntos por operacion, y no hay una
    /// sola sesion LOSO positiva. La mejor produce +0,26 puntos de ventaja sobre el azar y cruzar
    /// el spread mas la comision cuesta 0,948. Le falta casi cuatro veces lo que produce.
    /// Lo unico que SI quedo medido, y es el motivo de que este indicador exista:
    ///   - la familia "NO perseguir / promediar en contra" (CONTRA, QUIETO+P, QUIETO) le gana al
    ///     azar antes de costos, con t entre 2,35 y 4,67 y a favor en 67-83 % de las sesiones;
    ///   - la familia "perseguir" (PERSIGO, PERSIGO!!) es PEOR que tirar una moneda en el mismo
    ///     minuto, la misma franja y el mismo lado: -0,34 a -0,50 puntos, t de -4,4 a -6,6.
    /// O sea: las flechas verdes/rojas describen el mercado, no dicen cuando entrar; las marcas
    /// naranjas avisan que la foto de ese momento es la que historicamente resta.
    ///
    /// CUANTAS VECES APARECE. Entre 250 y 1.350 veces por sesion segun la regla (el operador abre
    /// 36 ciclos). Por eso vienen prendidas solo tres y hay un tope de una flecha por vela.
    ///
    /// COMO CALCULA. Exactamente igual que el Python del juez: la cinta orden por orden
    /// (CumulativeTrade, con la punta despues de cada orden, que es lo mismo que graba la sonda de
    /// Flujo Claro) entra en PatronNucleo.cs, que es codigo puro sin ATAS. Ese mismo archivo se
    /// corre sobre 3 sesiones grabadas en arnes/Program.cs: 20.610 senales comparadas contra el
    /// Python, 100 % de coincidencia (r9_p_02_salida.txt).
    ///
    /// REGISTRO. Cada senal (las cinco reglas, se dibujen o no) va a
    /// %APPDATA%\ATAS\PythiaGex2\patron\senales-&lt;fecha UTC&gt;.csv con la hora, el precio y todos los
    /// rasgos, para medirla despues con la prueba hacia adelante.
    /// </summary>
    [DisplayName("Patron Operador (EN PRUEBA)")]
    [Category("PythiaGex 2.0")]
    public class PatronOperadorIndicador : Indicator
    {
        public const string Version = "1.0";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ==================================================================
        // Estado
        // ==================================================================
        private sealed class Marca
        {
            public DateTime Utc;
            public int Bar;
            public int Lado;               // +1 compra, -1 venta
            public PatronNucleo.Regla Regla;
            public double Precio;          // el medio en el momento de la senal
            public bool Dibujar;
        }

        private readonly PatronNucleo _nucleo = new PatronNucleo();
        private readonly object _llave = new object();
        private readonly List<Marca> _marcas = new List<Marca>();
        private readonly Dictionary<PatronNucleo.Regla, PatronNucleo.Motor> _motores =
            new Dictionary<PatronNucleo.Regla, PatronNucleo.Motor>();
        private readonly Dictionary<PatronNucleo.Regla, int> _cuenta = new Dictionary<PatronNucleo.Regla, int>();
        private readonly Dictionary<PatronNucleo.Regla, int> _ultLado = new Dictionary<PatronNucleo.Regla, int>();
        private readonly HashSet<string> _yaEnVela = new HashSet<string>();

        private CumulativeTrade _actual;
        private long _ultimaEvaluacion = long.MinValue;
        private string _diaRegistro = "";
        private string _diaContadores = "";
        private int _barActual = -1;
        private string _estado = "arrancando";
        private TimeSpan _periodo;
        private Action _latido;

        // ==================================================================
        // Ajustes - que se dibuja
        // ==================================================================
        [Display(Name = "CONTRA (G5: el precio corrio 3 pts en contra en 2 min)", GroupName = "Que se marca", Order = 10,
            Description = "La senal con mas ventaja medida sobre el azar (+0,260 pts, t +4,67). Dispara unas 900 veces por sesion.")]
        public bool VerAgregaEnContra { get; set; } = true;

        [Display(Name = "QUIETO+P (R3: no persigue y la punta no esta cargada)", GroupName = "Que se marca", Order = 20,
            Description = "La menos mala por neto en el juez (-0,757 pts/op). Dispara unas 890 veces por sesion y deja 740 flechas: apagada por defecto porque sola tapa el grafico. Igual se registra en el CSV.")]
        public bool VerNoPersiguePunta { get; set; } = false;

        [Display(Name = "QUIETO (R2: no persigue)", GroupName = "Que se marca", Order = 30,
            Description = "La mas consistente contra el azar (83 % de las sesiones) pero dispara 1.340 veces por sesion: tapa la pantalla. Apagada por defecto.")]
        public bool VerNoPersigue { get; set; } = false;

        [Display(Name = "PERSIGO (G1/G2: el precio ya corrio 2 pts en 30 s) - AVISO", GroupName = "Que se marca", Order = 40,
            Description = "ANTI-SENAL: peor que el azar (-0,54 y -0,34 pts). 675 veces por sesion. Apagada por defecto.")]
        public bool VerPersigue { get; set; } = false;

        [Display(Name = "PERSIGO!! (G4: persigue + pegado al extremo + cinta agrediendo) - AVISO", GroupName = "Que se marca", Order = 50,
            Description = "ANTI-SENAL: la foto mas parecida a su gatillo y la peor de las 17 (-1,437 pts/op). Unas 300 veces por sesion.")]
        public bool VerPersigueExtremo { get; set; } = true;

        [Display(Name = "Una marca por vela y por regla", GroupName = "Que se marca", Order = 60,
            Description = "Solo limita el DIBUJO. El registro en el CSV guarda todas.")]
        public bool UnaPorVela { get; set; } = true;

        [Display(Name = "Marcar solo cuando la regla CAMBIA de lado", GroupName = "Que se marca", Order = 65,
            Description = "Solo limita el DIBUJO, no la senal. Medido sobre 3 sesiones grabadas: CONTRA pasa de 603 a 380 marcas por sesion, PERSIGO!! de 313 a 292.")]
        public bool SoloCambios { get; set; } = true;

        [Display(Name = "Velas hacia atras que se dibujan", GroupName = "Que se marca", Order = 70)]
        public int VelasAtras { get; set; } = 400;

        // ==================================================================
        // Ajustes - aviso
        // ==================================================================
        [Display(Name = "Alerta sonora", GroupName = "Aviso", Order = 10,
            Description = "Apagada por defecto. Con las reglas prendidas suena cerca de una vez por minuto.")]
        public bool Sonido { get; set; } = false;

        [Display(Name = "Sonido", GroupName = "Aviso", Order = 20)]
        public string ArchivoSonido { get; set; } = "alert1";

        [Display(Name = "Solo avisar las ANTI-SENAL", GroupName = "Aviso", Order = 30,
            Description = "Si esta prendido, el sonido salta solo cuando la foto es la que historicamente resta.")]
        public bool SoloAvisarAnti { get; set; } = true;

        // ==================================================================
        // Ajustes - registro
        // ==================================================================
        [Display(Name = "Registrar las senales en CSV", GroupName = "Registro", Order = 10,
            Description = @"%APPDATA%\ATAS\PythiaGex2\patron\senales-<fecha>.csv")]
        public bool Registrar { get; set; } = true;

        [Display(Name = "Registrar tambien las reglas apagadas", GroupName = "Registro", Order = 20,
            Description = "Prendido: el CSV guarda las cinco reglas aunque no se dibujen, para que la prueba hacia adelante no dependa de lo que se ve.")]
        public bool RegistrarApagadas { get; set; } = true;

        [Display(Name = "Carpeta (vacio = la de PythiaGex 2.0)", GroupName = "Registro", Order = 30)]
        public string Carpeta { get; set; } = "";

        // ==================================================================
        // Ajustes - dibujo
        // ==================================================================
        [Display(Name = "Ver el rotulo", GroupName = "Dibujo", Order = 10)]
        public bool VerRotulo { get; set; } = true;

        [Display(Name = "Tamano de la flecha (px)", GroupName = "Dibujo", Order = 20)]
        public int TamanoFlecha { get; set; } = 7;

        [Display(Name = "Separacion de la vela (px)", GroupName = "Dibujo", Order = 30)]
        public int Separacion { get; set; } = 10;

        [Display(Name = "Letra", GroupName = "Dibujo", Order = 40)]
        public float Letra { get; set; } = 8f;

        [Display(Name = "Ver el nombre corto de la regla", GroupName = "Dibujo", Order = 50)]
        public bool VerNombres { get; set; } = true;

        [Display(Name = "Margen del eje de precios (px)", GroupName = "Dibujo", Order = 60)]
        public int MargenEje { get; set; } = 62;

        private static readonly Color ColCompra = Color.FromArgb(235, 60, 210, 120);
        private static readonly Color ColVenta = Color.FromArgb(235, 235, 80, 80);
        private static readonly Color ColAnti = Color.FromArgb(235, 250, 170, 40);
        private static readonly Color ColTexto = Color.FromArgb(225, 230, 236);
        private static readonly Color ColFondo = Color.FromArgb(11, 16, 23);

        // ==================================================================
        // Ciclo de vida
        // ==================================================================
        public PatronOperadorIndicador() : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
            DrawAbovePrice = true;
            if (DataSeries.Count > 0 && DataSeries[0] is ValueDataSeries v)
            {
                v.IsHidden = true;
                v.VisualType = VisualMode.Hide;
                v.ShowCurrentValue = false;
            }
            foreach (var f in PatronNucleo.Fichas)
            {
                _motores[f.Id] = new PatronNucleo.Motor { TopeS = 60 };
                _cuenta[f.Id] = 0;
                _ultLado[f.Id] = 0;
            }
        }

        protected override void OnInitialize()
        {
            try
            {
                var ps = DataProvider?.Panels;
                if (ps != null && ps.Count > 0) Panel = ps.Contains("Chart") ? "Chart" : ps[0];
            }
            catch (Exception e) { Anotar(e); }

            _periodo = TimeSpan.FromSeconds(1);
            _latido = Latido;
            SubscribeToTimer(_periodo, _latido);
            Log("patron: arrancado, version " + Version);
        }

        protected override void OnDispose()
        {
            try { if (_latido != null) UnsubscribeFromTimer(_periodo, _latido); } catch { }
            base.OnDispose();
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            _barActual = bar;
        }

        // ==================================================================
        // La cinta en vivo
        // ==================================================================
        // Un CumulativeTrade se sigue actualizando mientras la agresion barre precios: recien
        // cuando llega el SIGUIENTE se sabe que el anterior termino, con su NewBid/NewAsk final.
        // Es la misma convencion con la que la sonda grabo las 37 sesiones (FlujoClaroSonda.cs),
        // asi que la cinta en vivo y la grabada son la misma cosa.
        protected override void OnCumulativeTrade(CumulativeTrade trade)
        {
            try
            {
                if (trade == null) return;
                CumulativeTrade cerrado = null;
                lock (_llave)
                {
                    if (_actual != null && !ReferenceEquals(_actual, trade)) cerrado = _actual;
                    _actual = trade;
                }
                if (cerrado != null) Alimentar(cerrado);
            }
            catch (Exception e) { Anotar(e); }
        }

        protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
        {
            try { if (trade != null) lock (_llave) _actual = trade; } catch { }
        }

        private void Alimentar(CumulativeTrade t)
        {
            var nb = t.NewBid; var na = t.NewAsk;
            long ns = (t.Time.Ticks - 621355968000000000L) * 100L;     // ns desde 1970-01-01 UTC
            // 'TradeDirection' existe en ATAS.DataFeedsCore y en ATAS.Indicators: hay que decir cual
            int lado = t.Direction == ATAS.Indicators.TradeDirection.Buy ? 1
                     : t.Direction == ATAS.Indicators.TradeDirection.Sell ? -1 : 0;
            lock (_llave)
            {
                _nucleo.Agregar(ns, (double)t.FirstPrice, (double)t.Lastprice, (double)t.Volume, lado,
                                nb != null ? (double)nb.Price : double.NaN,
                                nb != null ? (double)nb.Volume : double.NaN,
                                na != null ? (double)na.Price : double.NaN,
                                na != null ? (double)na.Volume : double.NaN);
            }
        }

        // ==================================================================
        // El latido: un momento cada 60 s, como el juez
        // ==================================================================
        private void Latido()
        {
            try
            {
                var ahora = DateTime.UtcNow;
                long reloj = ahora.Ticks / TimeSpan.TicksPerSecond - 62135596800L;    // segundos UTC
                // La grilla del juez es UN MOMENTO CADA 60 s. Aca se ancla al minuto UTC redondo y
                // se evalua un segundo DESPUES, para que la cinta de ese ultimo segundo ya haya
                // llegado; el instante que se le pasa al nucleo sigue siendo el minuto exacto.
                long minuto = (reloj - 1) / 60;
                if (minuto <= _ultimaEvaluacion) return;
                _ultimaEvaluacion = minuto;
                long sec = minuto * 60;

                // los contadores y el tope de una marca por vela son del dia UTC, como el CSV
                string hoy = ahora.ToString("yyyy-MM-dd", Inv);
                if (_diaContadores != hoy)
                {
                    _diaContadores = hoy;
                    _yaEnVela.Clear();
                    foreach (var fi in PatronNucleo.Fichas)
                    { _cuenta[fi.Id] = 0; _ultLado[fi.Id] = 0; _motores[fi.Id].Reiniciar(); }
                }

                PatronNucleo.Rasgos x;
                double seg;
                int eventos;
                lock (_llave)
                {
                    seg = _nucleo.SegundosDeCinta;
                    eventos = _nucleo.Eventos;
                    if (eventos == 0)
                    {
                        _estado = "sin cinta: el conector todavia no mando ninguna orden (CumulativeTrade)";
                        return;
                    }
                    if (eventos < 50 || seg < PatronNucleo.SegCalentamiento)
                    {
                        _estado = string.Format(Inv, "calentando: {0:0} s de cinta de {1} (faltan {2:0} s)",
                            seg, PatronNucleo.SegCalentamiento, Math.Max(0, PatronNucleo.SegCalentamiento - seg));
                        return;
                    }
                    x = _nucleo.Evaluar(sec * PatronNucleo.NS);
                }
                if (!x.Valido) { _estado = "sin cinta"; return; }
                _estado = string.Format(Inv, "cinta viva: {0} ordenes, {1:0} min", eventos, seg / 60.0);

                int bar = Math.Max(0, _barActual);
                bool dibujoAlgo = false;
                foreach (var f in PatronNucleo.Fichas)
                {
                    int s = PatronNucleo.Dispara(f.Id, in x);
                    int antes = _ultLado[f.Id];
                    _ultLado[f.Id] = (s == 2) ? 0 : s;
                    if (s == 0 || s == 2) continue;                    // 2 = dispara de los dos lados: el juez lo saltea
                    if (!_motores[f.Id].Aceptar(sec)) continue;        // sin solapes, igual que el juez

                    bool visible = Prendida(f.Id);
                    _cuenta[f.Id] = _cuenta[f.Id] + 1;

                    bool dibujar = false;
                    if (visible && (!SoloCambios || s != antes))
                    {
                        string k = f.Id + "|" + s + "|" + bar;
                        dibujar = !UnaPorVela || _yaEnVela.Add(k);
                    }
                    if (dibujar)
                    {
                        lock (_llave)
                        {
                            _marcas.Add(new Marca { Utc = ahora, Bar = bar, Lado = s, Regla = f.Id, Precio = x.Mid, Dibujar = true });
                            if (_marcas.Count > 4000) _marcas.RemoveRange(0, 1000);
                        }
                        dibujoAlgo = true;
                        if (Sonido && (!SoloAvisarAnti || f.AntiSenal))
                            Avisar((f.AntiSenal ? "AVISO " : "") + f.Corto + " " + (s > 0 ? "compra" : "venta")
                                   + " " + x.Mid.ToString("0.00", Inv) + " (EN PRUEBA)");
                    }
                    if (Registrar && (visible || RegistrarApagadas)) Anotar(ahora, f, s, in x, bar, dibujar);
                }
                if (dibujoAlgo) try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            }
            catch (Exception e) { Anotar(e); }
        }

        private bool Prendida(PatronNucleo.Regla r)
        {
            switch (r)
            {
                case PatronNucleo.Regla.AgregaEnContra: return VerAgregaEnContra;
                case PatronNucleo.Regla.NoPersiguePunta: return VerNoPersiguePunta;
                case PatronNucleo.Regla.NoPersigue: return VerNoPersigue;
                case PatronNucleo.Regla.Persigue: return VerPersigue;
                case PatronNucleo.Regla.PersigueExtremo: return VerPersigueExtremo;
            }
            return false;
        }

        private void Avisar(string texto)
        {
            try
            {
                AddAlert(string.IsNullOrWhiteSpace(ArchivoSonido) ? "alert1" : ArchivoSonido,
                         "Patron Operador " + (InstrumentInfo != null ? InstrumentInfo.Instrument : "") + ": " + texto);
            }
            catch (Exception e) { Anotar(e); }
        }

        // ==================================================================
        // Registro de senales
        // ==================================================================
        private const string Cabecera = "utc,instrumento,marco,version,regla,clave,lado,anti,dibujada,"
            + "mid,bid,ask,spread_ticks,desbal_punta,ret_20s,ret_30s,ret_120s,pos_rango_60s,desbal_10,m2p_cuerpo,"
            + "eventos_cinta,seg_cinta,tope_s,barra";

        private string CarpetaEfectiva()
        {
            if (!string.IsNullOrWhiteSpace(Carpeta)) return Carpeta.Trim();
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                "ATAS", "PythiaGex2", "patron");
        }

        private void Anotar(DateTime utc, PatronNucleo.FichaRegla f, int lado, in PatronNucleo.Rasgos x,
                            int bar, bool dibujada)
        {
            try
            {
                string dir = CarpetaEfectiva();
                string dia = utc.ToString("yyyy-MM-dd", Inv);
                string ruta = Path.Combine(dir, "senales-" + dia + ".csv");
                var b = new System.Text.StringBuilder(512);
                b.Append(utc.ToString("yyyy-MM-ddTHH:mm:ss", Inv)).Append(',');
                b.Append(InstrumentInfo != null ? InstrumentInfo.Instrument : "").Append(',');
                b.Append(ChartInfo != null && ChartInfo.ChartType != null
                         ? ChartInfo.ChartType + "-" + ChartInfo.TimeFrame : "").Append(',');
                b.Append(Version).Append(',');
                b.Append(f.Id).Append(',').Append(f.Clave).Append(',');
                b.Append(lado.ToString(Inv)).Append(',').Append(f.AntiSenal ? 1 : 0).Append(',').Append(dibujada ? 1 : 0).Append(',');
                Num(b, x.Mid); Num(b, x.Bid); Num(b, x.Ask); Num(b, x.SpreadTicks); Num(b, x.DesbalPunta);
                Num(b, x.Ret20); Num(b, x.Ret30); Num(b, x.Ret120); Num(b, x.PosRango60); Num(b, x.Desbal10);
                Num(b, x.M2pCuerpo);
                b.Append(x.Eventos.ToString(Inv)).Append(',');
                b.Append(x.SegHistoria.ToString("0", Inv)).Append(',');
                b.Append("60,").Append(bar.ToString(Inv)).Append('\n');

                lock (_llave)
                {
                    Directory.CreateDirectory(dir);
                    if (_diaRegistro != dia || !File.Exists(ruta))
                    {
                        if (!File.Exists(ruta)) File.WriteAllText(ruta, Cabecera + "\n");
                        _diaRegistro = dia;
                    }
                    File.AppendAllText(ruta, b.ToString());
                }
            }
            catch (Exception e) { Anotar(e); }
        }

        private static void Num(System.Text.StringBuilder b, double v)
        {
            if (!double.IsNaN(v) && !double.IsInfinity(v)) b.Append(v.ToString("0.######", Inv));
            b.Append(',');
        }

        // ==================================================================
        // Dibujo
        // ==================================================================
        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            try { Pintar(g); }
            catch (Exception e) { Anotar(e); }
        }

        private void Pintar(RenderContext g)
        {
            var cont = ChartInfo?.PriceChartContainer;
            if (cont == null) return;
            var area = ChartArea;
            int ult = CurrentBar - 1;
            if (ult < 1) return;
            int desde = Math.Max(0, Math.Max(FirstVisibleBarNumber, ult - Math.Max(10, VelasAtras)));
            int hasta = Math.Min(ult, LastVisibleBarNumber);

            List<Marca> copia;
            lock (_llave) copia = new List<Marca>(_marcas);

            var f = new RenderFont("Consolas", Math.Max(6f, Letra));
            int r = Math.Max(3, TamanoFlecha);
            var usados = new Dictionary<long, int>();

            foreach (var m in copia)
            {
                if (!m.Dibujar || !Prendida(m.Regla)) continue;
                if (m.Bar < desde || m.Bar > hasta) continue;
                int x;
                try { x = cont.GetXByBar(m.Bar, true); } catch { continue; }
                if (x < area.Left || x > area.Right - Math.Max(0, MargenEje)) continue;

                var vela = GetCandle(m.Bar);
                if (vela == null) continue;
                double px = m.Lado > 0 ? (double)vela.Low : (double)vela.High;
                int y;
                try { y = cont.GetYByPrice((decimal)px, false); } catch { continue; }

                long clave = (long)m.Bar * 4 + (m.Lado > 0 ? 1 : 0);
                usados.TryGetValue(clave, out int piso);
                usados[clave] = piso + 1;

                int sep = Math.Max(4, Separacion) + piso * (r * 2 + (VerNombres ? 11 : 3));
                bool anti = PatronNucleo.Ficha(m.Regla).AntiSenal;
                var col = anti ? ColAnti : (m.Lado > 0 ? ColCompra : ColVenta);

                if (m.Lado > 0)
                {
                    int cy = y + sep;
                    if (anti) Cruz(g, col, x, cy, r);
                    else g.FillPolygon(col, new[] { new Point(x, cy - r), new Point(x + r, cy + r), new Point(x - r, cy + r) });
                    if (VerNombres) Etiqueta(g, f, col, PatronNucleo.Ficha(m.Regla).Corto, x, cy + r + 1);
                }
                else
                {
                    int cy = y - sep;
                    if (anti) Cruz(g, col, x, cy, r);
                    else g.FillPolygon(col, new[] { new Point(x, cy + r), new Point(x + r, cy - r), new Point(x - r, cy - r) });
                    if (VerNombres) Etiqueta(g, f, col, PatronNucleo.Ficha(m.Regla).Corto, x, cy - r - 11);
                }
            }

            if (VerRotulo) Rotulo(g, area);
        }

        private static void Cruz(RenderContext g, Color c, int x, int y, int r)
        {
            var p = new RenderPen(c, 2f);
            g.DrawLine(p, x - r, y - r, x + r, y + r);
            g.DrawLine(p, x - r, y + r, x + r, y - r);
        }

        private void Etiqueta(RenderContext g, RenderFont f, Color c, string s, int x, int y)
        {
            var m = g.MeasureString(s, f);
            g.FillRectangle(Color.FromArgb(150, ColFondo), new Rectangle(x - m.Width / 2 - 2, y - 1, m.Width + 4, m.Height + 1));
            g.DrawString(s, f, c, x - m.Width / 2, y);
        }

        /// <summary>El rotulo. No es decoracion: sin el, alguien puede confundir estas flechas con
        /// una estrategia. Dice que esta en prueba y con que numero medido.</summary>
        private void Rotulo(RenderContext g, Rectangle area)
        {
            var fT = new RenderFont("Consolas", Math.Max(7f, Letra + 1f));
            var f = new RenderFont("Consolas", Math.Max(6f, Letra - 0.5f));
            var lineas = new List<(string s, Color c, RenderFont f)>();
            lineas.Add(("PATRON OPERADOR " + Version + "  -  EN PRUEBA: no validado", ColAnti, fT));
            lineas.Add(("ninguna de las 17 reglas gana al costo (la vuelta cuesta "
                        + PatronNucleo.CostoVuelta.ToString("0.000", Inv) + " pts).", ColTexto, f));
            lineas.Add(("juez: 24 sesiones que no opero, 0 con neto positivo. NO opera nada.", ColTexto, f));
            lineas.Add((_estado, Color.FromArgb(170, ColTexto), f));
            foreach (var ficha in PatronNucleo.Fichas)
            {
                if (!Prendida(ficha.Id)) continue;
                var c = ficha.AntiSenal ? ColAnti : ColCompra;
                lineas.Add(("  " + ficha.Corto.PadRight(9) + " hoy " + _cuenta[ficha.Id].ToString(Inv).PadLeft(4)
                            + "   " + ficha.Medido, c, f));
            }

            int w = 0, h = 0;
            foreach (var l in lineas)
            {
                var m = g.MeasureString(l.s, l.f);
                if (m.Width > w) w = m.Width;
                h += m.Height + 1;
            }
            int x0 = area.Left + 6, y0 = area.Top + 6;
            g.FillRectangle(Color.FromArgb(205, ColFondo), new Rectangle(x0, y0, w + 14, h + 8));
            g.DrawRectangle(new RenderPen(Color.FromArgb(150, ColAnti), 1f), new Rectangle(x0, y0, w + 14, h + 8));
            int y = y0 + 4;
            foreach (var l in lineas)
            {
                g.DrawString(l.s, l.f, l.c, x0 + 7, y);
                y += g.MeasureString(l.s, l.f).Height + 1;
            }
        }

        // ==================================================================
        // Log propio (ATAS se traga las excepciones de los indicadores)
        // ==================================================================
        private static void Log(string msg)
        {
            try
            {
                var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                     "ATAS", "pythiagex2-patron.log");
                File.AppendAllText(p, DateTime.Now.ToString("s") + "  " + msg + "\n");
            }
            catch { }
        }

        private static void Anotar(Exception e)
        {
            var st = e.StackTrace ?? "";
            Log("EXCEPCION " + e.GetType().Name + ": " + e.Message + " | "
                + st.Replace("\n", " ").Substring(0, Math.Min(300, st.Length)));
        }
    }
}
