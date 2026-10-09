using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using MColor = System.Windows.Media.Color;

namespace Pauta
{
    /// <summary>Escala de los parametros: NQ/MNQ = D 60, stop 10, objetivo 40 (lo pre-registrado para MNQ);
    /// ES/MES = D 15, stop 2,5, objetivo 10. Automatica: por el nombre del instrumento.</summary>
    public enum EscalaPauta
    {
        Automatica = 0,
        NQ_MNQ = 1,
        ES_MES = 2,
    }

    /// <summary>
    /// PAUTA 1.0 - pone en el grafico lo que usa "la pauta" (el canal de reels que trajo el operador el 24-09), con la
    /// misma pinta: las tres medias (roja gruesa la lenta, naranja y verde las rapidas), los niveles que la formalizacion
    /// P1 ("las marcaciones") calcula sola a las 09:15 NY con su etiqueta de origen, la ventana de la apertura sombreada,
    /// y la marca del disparo de P1 (y de P2 cuando es distinta) con el stop y el objetivo de la regla como lineas finas.
    /// Solo dibuja y anota: NO opera, NO toca ordenes. EN PRUEBA: no validado.
    ///
    /// LO MEDIDO (laboratorio/pauta, pre-registro P_00 del 24-09, criterio fijado antes de correr): P1 en MNQ 42
    /// operaciones en 36 sesiones, 9,5 % ganadoras cuando hacen falta 15,7 %, -3,1 pts por operacion con costo, t por
    /// sesion -1,4; en MES 41 operaciones, 7,3 %, -1,2 pts, t -2,4. No supera ninguno de los tres controles de azar.
    /// P2 (extremos de 08:15-09:15): MNQ -4,2 pts/op, MES -1,7. 0 de 7 condiciones en los dos instrumentos. Por eso
    /// el rotulo dice EN PRUEBA y no hay flechas de direccion.
    ///
    /// El calculo vive en PautaNucleo.cs, comparado nivel por nivel y operacion por operacion contra el Python de la
    /// medicion (p_paridad_pauta.py: 36 sesiones de MNQ y 32 de MES, 0 diferencias). Solo velas CERRADAS, de a una.
    /// </summary>
    [DisplayName("Pauta (replica de la pauta) - EN PRUEBA")]
    [Category("PythiaGex 2.0")]
    public class PautaIndicador : Indicator
    {
        public const string Version = "1.0";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-AR");

        // ==================================================================
        // Estado
        // ==================================================================
        private PautaNucleo _n;
        private readonly List<long> _utc = new List<long>();
        private readonly List<int> _ses = new List<int>();
        private readonly List<byte> _flag = new List<byte>();       // 0 nada, 1 ordenes activas 09:15-09:45, 2 hasta el cierre 10:30
        private readonly List<double> _cierres = new List<double>(), _emaL = new List<double>(), _sma1 = new List<double>(), _sma2 = new List<double>();
        private double _sum1, _sum2;
        private int _proc;                // velas cerradas ya procesadas (0.._proc-1)
        private int _barAlCargar = -1;    // primera vela que cerro DESPUES de terminar de cargar la historia
        private int _paso = 60;
        private double _tick = 0.25;
        private string _escalaUsada = "";
        private int _registrosHoy;
        private readonly object _llave = new object();

        // ==================================================================
        // 1. Medias
        // ==================================================================
        private int _emaLenta = 200;
        [Display(Name = "EMA lenta (roja gruesa)", GroupName = "1. Medias (las que se ven en el canal)", Order = 10,
            Description = "Estimada 200 en 7 de 9 videos (ajustada contra datos reales en v7). En el canal NO gatilla nada; aparece como objetivo y como nivel.")]
        [Range(2, 2000)]
        public int PtaEmaLenta { get => _emaLenta; set { _emaLenta = Math.Max(2, value); RecalculateValues(); } }

        private int _sma1n = 20;
        [Display(Name = "SMA rapida 1 (verde)", GroupName = "1. Medias (las que se ven en el canal)", Order = 20,
            Description = "La VERDE del canal, la mas rapida: estimada entre 8 y 21 (v7 contra la cinta de MNQ: SMA 21 rms 1,35, SMA 20 rms 2,1). Solo se dibuja.")]
        [Range(2, 2000)]
        public int PtaSmaRapida1 { get => _sma1n; set { _sma1n = Math.Max(2, value); RecalculateValues(); } }

        private int _sma2n = 26;
        [Display(Name = "SMA rapida 2 (naranja)", GroupName = "1. Medias (las que se ven en el canal)", Order = 30,
            Description = "La NARANJA del canal, apenas mas lenta que la verde: estimada entre 20 y 35 (v7: SMA 26 rms 1,0). Solo se dibuja.")]
        [Range(2, 2000)]
        public int PtaSmaRapida2 { get => _sma2n; set { _sma2n = Math.Max(2, value); RecalculateValues(); } }

        [Display(Name = "Dibujar las medias", GroupName = "1. Medias (las que se ven en el canal)", Order = 40)]
        public bool PtaVerMedias { get; set; } = true;

        [Display(Name = "Grosor de la lenta", GroupName = "1. Medias (las que se ven en el canal)", Order = 50)]
        [Range(1, 10)]
        public int PtaGrosorLenta { get; set; } = 3;

        // ==================================================================
        // 2. Regla
        // ==================================================================
        private EscalaPauta _escala = EscalaPauta.Automatica;
        [Display(Name = "Escala del instrumento", GroupName = "2. Regla P1 (defaults = lo pre-registrado)", Order = 10,
            Description = "NQ/MNQ: D 60, stop 10, objetivo 40, tope P2 50. ES/MES: D 15, stop 2,5, objetivo 10, tope 20. Automatica: por el nombre del instrumento.")]
        public EscalaPauta PtaEscala { get => _escala; set { _escala = value; RecalculateValues(); } }

        private decimal _d = 0m;
        [Display(Name = "D: distancia maxima de un nivel a P0 (0 = segun escala)", GroupName = "2. Regla P1 (defaults = lo pre-registrado)", Order = 20,
            Description = "Solo los niveles a menos de D puntos del precio de las 09:15 reciben limitada. Medido con 60 (MNQ) / 15 (MES).")]
        [Range(0, 100000)]
        public decimal PtaD { get => _d; set { _d = Math.Max(0m, value); RecalculateValues(); } }

        private decimal _stop = 0m;
        [Display(Name = "Stop en puntos (0 = segun escala)", GroupName = "2. Regla P1 (defaults = lo pre-registrado)", Order = 30,
            Description = "Medido con 10 (MNQ) / 2,5 (MES). El canal dice 2-2,5 de ES; medido en pantalla 1,75 a 4, y dos casos de 4,5 o mas (METODO.md seccion 7).")]
        [Range(0, 100000)]
        public decimal PtaStop { get => _stop; set { _stop = Math.Max(0m, value); RecalculateValues(); } }

        private decimal _obj = 0m;
        [Display(Name = "Objetivo en puntos (0 = segun escala)", GroupName = "2. Regla P1 (defaults = lo pre-registrado)", Order = 40,
            Description = "Medido con 40 (MNQ) / 10 (MES). El canal dice 10-15 de ES (medido en pantalla 6,75 a 15), 34-45 de NQ.")]
        [Range(0, 100000)]
        public decimal PtaObjetivo { get => _obj; set { _obj = Math.Max(0m, value); RecalculateValues(); } }

        private int _bePorc = 40;
        [Display(Name = "Stop a la entrada al % del objetivo (0 = nunca)", GroupName = "2. Regla P1 (defaults = lo pre-registrado)", Order = 50,
            Description = "El 'stop loss en cero' del canal. Medido con 40 % (v2 47 %, v6 42 %; v5 nunca; v8 80 %).")]
        [Range(0, 100)]
        public int PtaBePorcentaje { get => _bePorc; set { _bePorc = Math.Max(0, Math.Min(100, value)); RecalculateValues(); } }

        private bool _reingreso = true;
        [Display(Name = "Un reingreso si la primera salio exactamente en cero", GroupName = "2. Regla P1 (defaults = lo pre-registrado)", Order = 60)]
        public bool PtaReingreso { get => _reingreso; set { _reingreso = value; RecalculateValues(); } }

        private bool _toque = false;
        [Display(Name = "Llenado por toque (variante V1)", GroupName = "2. Regla P1 (defaults = lo pre-registrado)", Order = 70,
            Description = "Apagado = lo medido: la limitada se llena solo si la vela la atraviesa un tick (y el objetivo igual).")]
        public bool PtaToque { get => _toque; set { _toque = value; RecalculateValues(); } }

        private bool _p2 = true;
        [Display(Name = "Calcular tambien P2 (extremos 08:15-09:15)", GroupName = "2. Regla P1 (defaults = lo pre-registrado)", Order = 80,
            Description = "P2 = venta limitada en el maximo y compra en el minimo de la ultima hora, sin filtro D. Se marca solo cuando su disparo es distinto del de P1.")]
        public bool PtaCalcularP2 { get => _p2; set { _p2 = value; RecalculateValues(); } }

        [Display(Name = "Ventana (hora NY): niveles se congelan a las", GroupName = "2. Regla P1 (defaults = lo pre-registrado)", Order = 90,
            Description = "Lo medido es 09:15 (ordenes activas hasta las 09:45, cierre por tiempo 10:30). Cambiarlo ya no es lo medido. Hora de Nueva York, independiente de la zona de la PC.")]
        public string PtaHoraCongela { get => _hCong; set { _hCong = value; RecalculateValues(); } }
        private string _hCong = "09:15";

        [Display(Name = "Ventana (hora NY): ordenes activas hasta las", GroupName = "2. Regla P1 (defaults = lo pre-registrado)", Order = 91)]
        public string PtaHoraFinOrdenes { get => _hFin; set { _hFin = value; RecalculateValues(); } }
        private string _hFin = "09:45";

        [Display(Name = "Ventana (hora NY): cierre por tiempo a las", GroupName = "2. Regla P1 (defaults = lo pre-registrado)", Order = 92)]
        public string PtaHoraCierre { get => _hCie; set { _hCie = value; RecalculateValues(); } }
        private string _hCie = "10:30";

        // ==================================================================
        // 3. Que se dibuja
        // ==================================================================
        [Display(Name = "Niveles con limitada (dentro de D)", GroupName = "3. Que se dibuja", Order = 10)]
        public bool PtaVerNiveles { get; set; } = true;

        [Display(Name = "Niveles fuera de D (tenues)", GroupName = "3. Que se dibuja", Order = 20)]
        public bool PtaVerNivelesFuera { get; set; } = true;

        [Display(Name = "Banda P0 +/- D", GroupName = "3. Que se dibuja", Order = 30)]
        public bool PtaVerBandaD { get; set; } = true;

        [Display(Name = "Rectas de pivotes (N3) desde sus pivotes", GroupName = "3. Que se dibuja", Order = 35)]
        public bool PtaVerRectas { get; set; } = true;

        [Display(Name = "Ventana de la apertura sombreada", GroupName = "3. Que se dibuja", Order = 40)]
        public bool PtaVerVentana { get; set; } = true;

        [Display(Name = "Marcas de los disparos (stop y objetivo)", GroupName = "3. Que se dibuja", Order = 50)]
        public bool PtaVerMarcas { get; set; } = true;

        [Display(Name = "Marca de P2 cuando es distinta de P1", GroupName = "3. Que se dibuja", Order = 60)]
        public bool PtaVerP2 { get; set; } = true;

        [Display(Name = "Etiquetas de los niveles", GroupName = "3. Que se dibuja", Order = 70)]
        public bool PtaVerEtiquetas { get; set; } = true;

        [Display(Name = "Rotulo (lo medido y el dia)", GroupName = "3. Que se dibuja", Order = 80)]
        public bool PtaVerRotulo { get; set; } = true;

        [Display(Name = "Bajar el rotulo (px)", GroupName = "3. Que se dibuja", Order = 85,
            Description = "ATAS escribe el instrumento y el OHLC arriba a la izquierda: el rotulo va debajo.")]
        [Range(0, 800)]
        public int PtaBajarRotulo { get; set; } = 62;

        [Display(Name = "Letra", GroupName = "3. Que se dibuja", Order = 90)]
        [Range(6, 20)]
        public float PtaLetra { get; set; } = 8f;

        [Display(Name = "Margen del eje de precios (px)", GroupName = "3. Que se dibuja", Order = 95)]
        public int PtaMargenEje { get; set; } = 62;

        // ==================================================================
        // 4. Colores
        // ==================================================================
        [Display(Name = "EMA lenta", GroupName = "4. Colores", Order = 10)]
        public MColor PtaColorLenta { get; set; } = MColor.FromArgb(255, 242, 54, 69);

        [Display(Name = "SMA rapida 1 (verde)", GroupName = "4. Colores", Order = 20)]
        public MColor PtaColorRapida1 { get; set; } = MColor.FromArgb(255, 76, 175, 80);

        [Display(Name = "SMA rapida 2 (naranja)", GroupName = "4. Colores", Order = 30)]
        public MColor PtaColorRapida2 { get; set; } = MColor.FromArgb(255, 255, 152, 0);

        [Display(Name = "Nivel / disparo de COMPRA (celeste, como sus flechas)", GroupName = "4. Colores", Order = 40)]
        public MColor PtaColorCompra { get; set; } = MColor.FromArgb(255, 41, 182, 246);

        [Display(Name = "Nivel / disparo de VENTA (violeta, como sus flechas)", GroupName = "4. Colores", Order = 50)]
        public MColor PtaColorVenta { get; set; } = MColor.FromArgb(255, 186, 104, 200);

        [Display(Name = "Stop", GroupName = "4. Colores", Order = 60)]
        public MColor PtaColorStop { get; set; } = MColor.FromArgb(255, 239, 83, 80);

        [Display(Name = "Objetivo", GroupName = "4. Colores", Order = 70)]
        public MColor PtaColorObjetivo { get; set; } = MColor.FromArgb(255, 38, 166, 154);

        [Display(Name = "Ventana de la apertura", GroupName = "4. Colores", Order = 80)]
        public MColor PtaColorVentana { get; set; } = MColor.FromArgb(34, 33, 150, 243);

        [Display(Name = "Niveles fuera de D y P0", GroupName = "4. Colores", Order = 90)]
        public MColor PtaColorTenue { get; set; } = MColor.FromArgb(255, 170, 178, 190);

        // ==================================================================
        // 5. Aviso y registro
        // ==================================================================
        [Display(Name = "Alerta sonora en cada disparo (solo en vivo)", GroupName = "5. Aviso y registro", Order = 10)]
        public bool PtaSonido { get; set; } = true;

        [Display(Name = "Sonido", GroupName = "5. Aviso y registro", Order = 20)]
        public string PtaArchivoSonido { get; set; } = "alert1";

        [Display(Name = "Registrar en vivo (CSV)", GroupName = "5. Aviso y registro", Order = 30,
            Description = @"%APPDATA%\ATAS\PythiaGex2\pauta\pauta-<sesion>.csv: niveles congelados, cada entrada, cada BE y cada salida. Es el registro de la prueba hacia adelante (p_medir_adelante.py).")]
        public bool PtaRegistrar { get; set; } = true;

        [Display(Name = "Volcar la historia al cargar (auditoria)", GroupName = "5. Aviso y registro", Order = 40,
            Description = @"Una vez por carga escribe %APPDATA%\ATAS\PythiaGex2\pauta\auditoria-<instrumento>-<marco>-*.csv con las sesiones, ordenes y operaciones que calculo sobre la historia del grafico, para compararlas con el laboratorio.")]
        public bool PtaVolcar { get; set; } = true;

        // ==================================================================
        // Ciclo de vida
        // ==================================================================
        public PautaIndicador() : base(true)
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
            Reiniciar();
        }

        protected override void OnInitialize()
        {
            try
            {
                var ps = DataProvider?.Panels;
                if (ps != null && ps.Count > 0) Panel = ps.Contains("Chart") ? "Chart" : ps[0];
            }
            catch (Exception e) { Anotar(e); }
            Log("pauta: arrancado, version " + Version);
        }

        private bool EsEscalaEs()
        {
            if (_escala == EscalaPauta.ES_MES) return true;
            if (_escala == EscalaPauta.NQ_MNQ) return false;
            string inst = "";
            try { inst = InstrumentInfo != null ? (InstrumentInfo.Instrument ?? "") : ""; } catch { }
            inst = inst.ToUpperInvariant();
            return inst.StartsWith("MES") || inst.StartsWith("ES");
        }

        private void Reiniciar()
        {
            lock (_llave)
            {
                bool es = EsEscalaEs();
                _escalaUsada = es ? "ES/MES" : "NQ/MNQ";
                try { if (InstrumentInfo != null && InstrumentInfo.TickSize > 0) _tick = (double)InstrumentInfo.TickSize; } catch { }
                var p = new Parametros
                {
                    Tick = _tick,
                    D = _d > 0 ? (double)_d : (es ? 15.0 : 60.0),
                    Stop = _stop > 0 ? (double)_stop : (es ? 2.5 : 10.0),
                    Objetivo = _obj > 0 ? (double)_obj : (es ? 10.0 : 40.0),
                    BeFrac = _bePorc / 100.0,
                    Reingreso = _reingreso, Toque = _toque, P2Activa = _p2,
                    TopeP2 = es ? 20.0 : 50.0,
                    HCongela = Segundos(_hCong, 9 * 3600 + 15 * 60), HFinOrdenes = Segundos(_hFin, 9 * 3600 + 45 * 60), HCierre = Segundos(_hCie, 10 * 3600 + 30 * 60),
                };
                _n = new PautaNucleo(p);
                _utc.Clear(); _ses.Clear(); _flag.Clear(); _cierres.Clear(); _emaL.Clear(); _sma1.Clear(); _sma2.Clear();
                _sum1 = 0; _sum2 = 0;
                _proc = 0; _barAlCargar = -1; _registrosHoy = 0;
                _paso = DetectarPaso();
            }
        }

        /// <summary>Duracion de la barra en segundos. Primero el marco que declara ATAS (TimeFrame "M1"/"M2" en minutos,
        /// ChartType "Seconds" + "1" en segundos: verificado en GammaHoy y en los volcados de la directriz); si no se
        /// puede leer, la diferencia mas frecuente entre velas consecutivas. El nucleo la usa para la grilla de relleno.</summary>
        private int DetectarPaso()
        {
            try
            {
                string tf = "", ct = "";
                try { tf = ChartInfo != null ? (ChartInfo.TimeFrame ?? "") : ""; ct = ChartInfo != null && ChartInfo.ChartType != null ? ChartInfo.ChartType.ToString() : ""; } catch { }
                int pasoDeclarado = PasoDeclarado(tf, ct);
                if (pasoDeclarado > 0) return pasoDeclarado;
                int n = Math.Min(400, CurrentBar);
                if (n < 3) return 60;
                var cuenta = new Dictionary<long, int>();
                DateTime? ant = null;
                for (int i = Math.Max(0, CurrentBar - n); i < CurrentBar; i++)
                {
                    var c = GetCandle(i);
                    if (c == null) continue;
                    var t = Utc(c.Time);
                    if (ant.HasValue)
                    {
                        long d = (long)Math.Round((t - ant.Value).TotalSeconds);
                        if (d > 0) { cuenta.TryGetValue(d, out int k); cuenta[d] = k + 1; }
                    }
                    ant = t;
                }
                long mejor = 60; int mejorK = -1;
                foreach (var kv in cuenta) if (kv.Value > mejorK || (kv.Value == mejorK && kv.Key < mejor)) { mejorK = kv.Value; mejor = kv.Key; }
                return (int)Math.Max(1, Math.Min(86400, mejor));
            }
            catch (Exception e) { Anotar(e); return 60; }
        }

        /// <summary>"M1" -> 60, "M5" -> 300, "S5"/"Seconds"+"5" -> 5, "H1" -> 3600. 0 si no es un marco de tiempo (ticks, rango...).</summary>
        internal static int PasoDeclarado(string tf, string ct)
        {
            tf = (tf ?? "").Trim(); ct = (ct ?? "").Trim().ToLowerInvariant();
            if (tf.Length == 0) return 0;
            char u = char.ToUpperInvariant(tf[0]);
            string dig = char.IsLetter(tf[0]) ? tf.Substring(1) : tf;
            if (!int.TryParse(dig, NumberStyles.Integer, Inv, out int n) || n <= 0) return 0;
            int unidad;
            if (char.IsLetter(tf[0]))
                unidad = u == 'S' ? 1 : u == 'M' ? 60 : u == 'H' ? 3600 : u == 'D' ? 86400 : 0;
            else
                unidad = ct.Contains("second") ? 1 : ct.Contains("minute") ? 60 : ct.Contains("hour") ? 3600 : ct.Contains("dai") || ct.Contains("day") ? 86400 : 0;
            return unidad == 0 ? 0 : n * unidad;
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            try
            {
                if (bar == 0) Reiniciar();
                // solo velas CERRADAS: la vela `bar` es la que se esta formando
                while (_proc < bar) Procesar(_proc++);
                if (_barAlCargar < 0 && bar == CurrentBar - 1)
                {
                    _barAlCargar = bar;
                    if (PtaVolcar) VolcarAuditoria();
                    Log("pauta: historia cargada, " + _proc + " velas, paso " + _paso + " s, escala " + _escalaUsada + ", sesiones " + _n.Sesiones.Count);
                }
            }
            catch (Exception e) { Anotar(e); }
        }

        private void Procesar(int k)
        {
            var c = GetCandle(k);
            if (c == null) return;
            long utc = RelojNy.UtcSeg(Utc(c.Time));
            string inst = "";
            try { inst = InstrumentInfo != null ? (InstrumentInfo.Instrument ?? "") : ""; } catch { }
            double o = (double)c.Open, h = (double)c.High, l = (double)c.Low, cl = (double)c.Close;
            List<Evento> evs = null;
            lock (_llave)
            {
                var b = Barra.Crear(utc, o, h, l, cl, double.NaN, double.NaN, inst, _paso);
                _utc.Add(utc); _ses.Add(b.Sesion);
                byte f = 0;
                if (b.FechaNy == b.Sesion)
                {
                    if (b.Sod >= _n.P.HCongela && b.Sod < _n.P.HFinOrdenes) f = 1;
                    else if (b.Sod >= _n.P.HFinOrdenes && b.Sod < _n.P.HCierre) f = 2;
                }
                _flag.Add(f);
                // medias de la vista (sobre las velas del grafico, cualquiera sea su marco)
                _cierres.Add(cl);
                double aL = 2.0 / (_emaLenta + 1.0);
                _emaL.Add(_emaL.Count == 0 ? cl : aL * cl + (1 - aL) * _emaL[_emaL.Count - 1]);
                _sum1 += cl; if (_cierres.Count > _sma1n) _sum1 -= _cierres[_cierres.Count - 1 - _sma1n];
                _sum2 += cl; if (_cierres.Count > _sma2n) _sum2 -= _cierres[_cierres.Count - 1 - _sma2n];
                _sma1.Add(_sum1 / Math.Min(_cierres.Count, _sma1n));
                _sma2.Add(_sum2 / Math.Min(_cierres.Count, _sma2n));
                _n.Agregar(b);
                if (_n.Eventos.Count > 0) { evs = new List<Evento>(_n.Eventos); _n.Eventos.Clear(); }
            }
            if (evs != null && _barAlCargar >= 0 && k >= _barAlCargar)
                foreach (var ev in evs) EnVivo(ev, inst);
        }

        // ==================================================================
        // Tiempo
        // ==================================================================
        private static DateTime Utc(DateTime t) => t.Kind == DateTimeKind.Utc ? t : (t.Kind == DateTimeKind.Local ? t.ToUniversalTime() : DateTime.SpecifyKind(t, DateTimeKind.Utc));

        private static int Segundos(string s, int def)
        {
            if (string.IsNullOrWhiteSpace(s)) return def;
            var p = s.Trim().Replace(".", ":").Split(':');
            if (p.Length == 1 && p[0].Length == 4) p = new[] { p[0].Substring(0, 2), p[0].Substring(2) };
            if (p.Length < 2 || !int.TryParse(p[0], out int h) || !int.TryParse(p[1], out int m)) return def;
            return Math.Max(0, Math.Min(86399, h * 3600 + m * 60));
        }

        /// <summary>Primera vela cerrada cuya hora es &gt;= utc (busqueda binaria). -1 si no hay; Count-1 si se pasa del final.</summary>
        private int BarDe(long utc)
        {
            int lo = 0, hi = _utc.Count - 1;
            if (hi < 0) return -1;
            if (_utc[hi] < utc) return hi;
            while (lo < hi)
            {
                int m = (lo + hi) / 2;
                if (_utc[m] >= utc) hi = m; else lo = m + 1;
            }
            return lo;
        }

        // ==================================================================
        // En vivo: aviso y registro
        // ==================================================================
        private static string Etiqueta(string familia)
        {
            var partes = familia.Split('+');
            for (int i = 0; i < partes.Length; i++)
            {
                switch (partes[i])
                {
                    case "N1_max": partes[i] = "max noche"; break;
                    case "N1_min": partes[i] = "min noche"; break;
                    case "N2_max": partes[i] = "max ayer"; break;
                    case "N2_min": partes[i] = "min ayer"; break;
                    case "N2_cierre": partes[i] = "cierre ayer"; break;
                    case "N4_max": partes[i] = "max 8:15-9:15"; break;
                    case "N4_min": partes[i] = "min 8:15-9:15"; break;
                    case "N3_pivotes_altos": partes[i] = "recta pivotes altos"; break;
                    case "N3_pivotes_bajos": partes[i] = "recta pivotes bajos"; break;
                    case "N5_ema200": partes[i] = "EMA 200 de 1 min"; break;
                }
            }
            return string.Join(" + ", partes);
        }

        private void EnVivo(Evento ev, string inst)
        {
            try
            {
                if (ev.Tipo == TipoEvento.Entrada && PtaSonido)
                {
                    string lado = ev.Op.Lado > 0 ? "COMPRA" : "VENTA";
                    AddAlert(string.IsNullOrWhiteSpace(PtaArchivoSonido) ? "alert1" : PtaArchivoSonido,
                             "Pauta " + inst + ": P" + ev.Regla + " " + lado + " limitada en " + ev.Op.Entrada.ToString("0.00", Inv)
                             + " (" + Etiqueta(ev.Op.Familia) + ") - EN PRUEBA, no validado; no opera");
                }
                if (PtaRegistrar) Registrar(ev, inst);
            }
            catch (Exception e) { Anotar(e); }
        }

        private const string Cabecera = "utc,sesion,instrumento,marco,paso_s,version,regla,evento,n_op,lado,familia,tipo,precio,nivel,stop,objetivo,motivo,bruto,be_armado,entrada_por,P0,D,stop_pts,objetivo_pts,be_porc,notas";

        private string Marco()
        {
            try { return ChartInfo != null && ChartInfo.ChartType != null ? ChartInfo.ChartType + "-" + ChartInfo.TimeFrame : ""; } catch { return ""; }
        }

        private void Registrar(Evento ev, string inst)
        {
            var s = ev.Sesion; var p = _n.P;
            string comun = string.Join(",", inst, Marco(), _paso.ToString(Inv), Version);
            string cola = string.Join(",", R(s.P0), R(p.D), R(p.Stop), R(p.Objetivo), _bePorc.ToString(Inv), (s.Notas ?? "").Replace(",", ";"));
            var filas = new List<string>();
            string utc = RelojNy.DesdeUtcSeg(ev.Utc).ToString("yyyy-MM-ddTHH:mm:ss", Inv);
            if (ev.Tipo == TipoEvento.Congelado)
            {
                foreach (var od in s.Ordenes)
                    filas.Add(string.Join(",", utc, s.SesionIso, comun, "P1", "nivel", "", od.Lado.ToString(Inv), od.Familia, od.Tipo, R(od.PrecioInicial), R(od.PrecioInicial), "", "", "", "", "", "", cola));
                if (!double.IsNaN(s.N4Hi))
                {
                    filas.Add(string.Join(",", utc, s.SesionIso, comun, "P2", "nivel", "", "-1", "N4_max", "fijo", R(s.N4Hi), R(s.N4Hi), "", "", "", "", "", "", cola));
                    filas.Add(string.Join(",", utc, s.SesionIso, comun, "P2", "nivel", "", "1", "N4_min", "fijo", R(s.N4Lo), R(s.N4Lo), "", "", "", "", "", "", cola));
                }
                filas.Add(string.Join(",", utc, s.SesionIso, comun, "P1", "congelado", "", "0", "", "", R(s.P0), "", "", "", "", "", "", "", cola));
            }
            else
            {
                var op = ev.Op;
                string evento = ev.Tipo == TipoEvento.Entrada ? "entrada" : ev.Tipo == TipoEvento.BeArmado ? "be_armado" : "salida";
                string precio = ev.Tipo == TipoEvento.Salida ? R(op.Salida) : R(op.Entrada);
                filas.Add(string.Join(",", utc, s.SesionIso, comun, "P" + op.Regla, evento, op.NOp.ToString(Inv), op.Lado.ToString(Inv), op.Familia, op.Tipo, precio, R(op.Nivel),
                    R(op.Stop), R(op.Objetivo), ev.Tipo == TipoEvento.Salida ? op.Motivo : "", ev.Tipo == TipoEvento.Salida ? R(op.Bruto) : "",
                    op.BeArmado ? "1" : "0", op.EntradaPor.Replace(",", ";"), cola));
            }
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex2", "pauta");
            string ruta = Path.Combine(dir, "pauta-" + s.SesionIso + ".csv");
            lock (_llave)
            {
                Directory.CreateDirectory(dir);
                if (!File.Exists(ruta)) File.WriteAllText(ruta, Cabecera + "\n");
                File.AppendAllText(ruta, string.Join("\n", filas) + "\n");
                _registrosHoy += filas.Count;
            }
        }

        private static string R(double x) => double.IsNaN(x) ? "" : x.ToString("R", Inv);

        // ==================================================================
        // Auditoria: lo que calculo sobre la historia del grafico
        // ==================================================================
        private void VolcarAuditoria()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex2", "pauta");
                Directory.CreateDirectory(dir);
                string inst = "inst"; try { inst = InstrumentInfo != null ? InstrumentInfo.Instrument : "inst"; } catch { }
                string marco = Marco(); if (string.IsNullOrEmpty(marco)) marco = "marco";
                foreach (var ch in Path.GetInvalidFileNameChars()) { inst = inst.Replace(ch, '_'); marco = marco.Replace(ch, '_'); }
                string pre = Path.Combine(dir, "auditoria-" + inst + "-" + marco);
                var b = new StringBuilder(1 << 18);
                lock (_llave)
                {
                    b.Append("sesion,contrato,paso,P0,n4_hi,n4_lo,n_min_ema,notas,n_ordenes,n_ops_p1,n_ops_p2\n");
                    foreach (var s in _n.Sesiones)
                        b.Append(s.SesionIso).Append(',').Append(s.Contrato).Append(',').Append(s.Paso).Append(',').Append(R(s.P0)).Append(',')
                         .Append(R(s.N4Hi)).Append(',').Append(R(s.N4Lo)).Append(',').Append(s.NMinEma).Append(',').Append(s.Notas).Append(',')
                         .Append(s.Ordenes.Count).Append(',').Append(s.OpsP1.Count).Append(',').Append(s.OpsP2.Count).Append('\n');
                    File.WriteAllText(pre + "-sesiones.csv", b.ToString());
                    b.Clear().Append("sesion,P0,precio,lado,familia,tipo,dist,notas\n");
                    foreach (var s in _n.Sesiones)
                        foreach (var od in s.Ordenes)
                            b.Append(s.SesionIso).Append(',').Append(R(s.P0)).Append(',').Append(R(od.PrecioInicial)).Append(',').Append(od.Lado).Append(',')
                             .Append(od.Familia).Append(',').Append(od.Tipo).Append(',').Append(R(od.PrecioInicial - s.P0)).Append(',').Append(s.Notas).Append('\n');
                    File.WriteAllText(pre + "-ordenes.csv", b.ToString());
                    b.Clear().Append("sesion,regla,n_op,t_ent,t_sal,lado,familia,tipo,nivel,entrada,salida,motivo,entrada_por,be_armado,bruto,dur_s,P0\n");
                    foreach (var s in _n.Sesiones)
                        foreach (var lista in new[] { s.OpsP1, s.OpsP2 })
                            foreach (var op in lista)
                                b.Append(s.SesionIso).Append(",P").Append(op.Regla).Append(',').Append(op.NOp).Append(',').Append(RelojNy.Hms(op.SodEnt)).Append(',')
                                 .Append(op.SodSal < 0 ? "" : RelojNy.Hms(op.SodSal)).Append(',').Append(op.Lado).Append(',').Append(op.Familia).Append(',').Append(op.Tipo).Append(',')
                                 .Append(R(op.Nivel)).Append(',').Append(R(op.Entrada)).Append(',').Append(R(op.Salida)).Append(',').Append(op.Motivo).Append(',')
                                 .Append(op.EntradaPor).Append(',').Append(op.BeArmado ? 1 : 0).Append(',').Append(R(op.Bruto)).Append(',').Append(op.DurS).Append(',').Append(R(op.P0)).Append('\n');
                    File.WriteAllText(pre + "-operaciones.csv", b.ToString());
                    File.WriteAllText(pre + "-ajustes.txt", string.Join("\n", "version=" + Version, "paso=" + _paso, "escala=" + _escalaUsada, "tick=" + R(_tick),
                        "D=" + R(_n.P.D), "stop=" + R(_n.P.Stop), "objetivo=" + R(_n.P.Objetivo), "be=" + R(_n.P.BeFrac), "reingreso=" + _reingreso, "toque=" + _toque,
                        "p2=" + _p2, "congela=" + _hCong, "fin_ordenes=" + _hFin, "cierre=" + _hCie, "velas=" + _utc.Count, "sesiones=" + _n.Sesiones.Count, "rellenos=" + _n.Rellenos) + "\n");
                }
                Log("pauta: volcado de auditoria en " + pre + "-*.csv (" + _utc.Count + " velas)");
            }
            catch (Exception e) { Anotar(e); }
        }

        // ==================================================================
        // Dibujo
        // ==================================================================
        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            try { lock (_llave) Pintar(g); }
            catch (Exception e) { Anotar(e); }
        }

        private static Color D(MColor c) => Color.FromArgb(c.A, c.R, c.G, c.B);
        private static Color Alfa(MColor c, int a) => Color.FromArgb(a, c.R, c.G, c.B);

        private void Pintar(RenderContext g)
        {
            var cont = ChartInfo?.PriceChartContainer;
            if (cont == null) return;
            Rectangle area = ChartArea;
            Rectangle reg;
            try { reg = cont.Region; } catch { reg = area; }
            if (reg.Width < 40 || reg.Height < 40) reg = area;
            var clip = new Rectangle(reg.Left, reg.Top, Math.Max(10, reg.Width - (reg == area ? Math.Max(0, PtaMargenEje) : 0)), reg.Height);

            int cuenta = _utc.Count;
            if (cuenta < 2) { if (PtaVerRotulo) Rotulo(g, clip); return; }
            int desde = Math.Max(0, FirstVisibleBarNumber);
            int hasta = Math.Min(cuenta - 1, LastVisibleBarNumber);
            if (hasta < desde) { if (PtaVerRotulo) Rotulo(g, clip); return; }
            Func<int, int> X = b => cont.GetXByBar(Math.Max(0, Math.Min(b, cuenta - 1)), false);
            Func<double, int> Y = p => cont.GetYByPrice((decimal)p, false);
            int anchoVela = Math.Max(1, Math.Abs(cont.GetXByBar(Math.Min(hasta, desde + 1), false) - cont.GetXByBar(desde, false)));
            var f = new RenderFont("Consolas", Math.Max(6f, PtaLetra));
            var fChica = new RenderFont("Consolas", Math.Max(5.5f, PtaLetra - 1f));

            g.SetClip(clip);
            try
            {
                // ventana de la apertura
                if (PtaVerVentana)
                {
                    int b = desde;
                    while (b <= hasta)
                    {
                        byte fl = _flag[b];
                        if (fl == 0) { b++; continue; }
                        int a = b; while (b + 1 <= hasta && _flag[b + 1] == fl) b++;
                        int x0 = X(a) - anchoVela / 2, x1 = X(b) + anchoVela / 2;
                        var col = fl == 1 ? D(PtaColorVentana) : Alfa(PtaColorVentana, Math.Max(6, PtaColorVentana.A / 3));
                        g.FillRectangle(col, new Rectangle(x0, clip.Top, Math.Max(1, x1 - x0), clip.Height));
                        b++;
                    }
                }

                // medias
                if (PtaVerMedias)
                {
                    var pL = new RenderPen(D(PtaColorLenta), PtaGrosorLenta);
                    var p1 = new RenderPen(D(PtaColorRapida1), 1f);
                    var p2 = new RenderPen(D(PtaColorRapida2), 1f);
                    for (int b = Math.Max(1, desde); b <= hasta; b++)
                    {
                        int xa = X(b - 1), xb = X(b);
                        g.DrawLine(p1, xa, Y(_sma1[b - 1]), xb, Y(_sma1[b]));
                        g.DrawLine(p2, xa, Y(_sma2[b - 1]), xb, Y(_sma2[b]));
                        g.DrawLine(pL, xa, Y(_emaL[b - 1]), xb, Y(_emaL[b]));
                    }
                }

                // sesiones visibles: niveles, banda, rectas, marcas
                long utcDesde = _utc[desde], utcHasta = _utc[hasta];
                foreach (var s in _n.Sesiones)
                {
                    if (!s.Congelada) continue;
                    if (s.UtcCierre < utcDesde || s.UtcCongela > utcHasta + 3600) continue;
                    int b0 = BarDe(s.UtcCongela), b1 = BarDe(s.UtcCierre), bFin = BarDe(s.UtcFinOrd);
                    if (b0 < 0) continue;
                    if (_utc[b1] < s.UtcCierre) b1 = cuenta - 1;                    // sesion en curso: hasta la ultima vela
                    int x0 = X(b0) - anchoVela / 2, x1 = X(b1) + anchoVela / 2, xFin = X(bFin) + anchoVela / 2;
                    if (x1 < clip.Left || x0 > clip.Right) continue;
                    int xEtq = Math.Min(x1, clip.Right - 4);
                    PintarNiveles(g, s, x0, x1, xFin, xEtq, Y, X, f, fChica);
                    if (PtaVerMarcas)
                    {
                        foreach (var op in s.OpsP1) PintarOperacion(g, op, false, X, Y, f, anchoVela, cuenta);
                        if (PtaVerP2)
                            foreach (var op in s.OpsP2)
                            {
                                bool igual = false;
                                foreach (var o1 in s.OpsP1) if (o1.UtcEnt == op.UtcEnt && o1.Lado == op.Lado && o1.Entrada == op.Entrada) { igual = true; break; }
                                if (!igual) PintarOperacion(g, op, true, X, Y, f, anchoVela, cuenta);
                            }
                    }
                }
            }
            finally { g.ResetClip(); }

            if (PtaVerRotulo) Rotulo(g, clip);
        }

        private void PintarNiveles(RenderContext g, SesionInfo s, int x0, int x1, int xFin, int xEtq, Func<double, int> Y, Func<int, int> X, RenderFont f, RenderFont fChica)
        {
            var p = _n.P;
            if (PtaVerBandaD && !double.IsNaN(s.P0))
            {
                int ya = Y(s.P0 + p.D), yb = Y(s.P0 - p.D);
                g.FillRectangle(Alfa(PtaColorTenue, 14), new Rectangle(x0, Math.Min(ya, yb), Math.Max(1, x1 - x0), Math.Max(1, Math.Abs(yb - ya))));
                var penP0 = new RenderPen(Alfa(PtaColorTenue, 150), 1f, System.Drawing.Drawing2D.DashStyle.Dot);
                int yp = Y(s.P0);
                g.DrawLine(penP0, x0, yp, x1, yp);
                if (PtaVerEtiquetas) TextoDerecha(g, fChica, "P0 " + RelojNy.Hm(p.HCongela) + " " + s.P0.ToString("0.00", Es), Alfa(PtaColorTenue, 200), xEtq, yp - 1, true);
            }
            if (PtaVerNiveles || PtaVerNivelesFuera)
            {
                foreach (var c in s.Candidatos)
                {
                    if (c.Orden && !PtaVerNiveles) continue;
                    if (!c.Orden && !PtaVerNivelesFuera) continue;
                    int y = Y(c.Precio);
                    Color col; RenderPen pen;
                    if (c.Orden)
                    {
                        col = D(c.Lado > 0 ? PtaColorCompra : PtaColorVenta);
                        pen = new RenderPen(Alfa(c.Lado > 0 ? PtaColorCompra : PtaColorVenta, 220), c.Tipo == "fijo" ? 1.6f : 1.2f,
                                            c.Tipo == "fijo" ? System.Drawing.Drawing2D.DashStyle.Solid : System.Drawing.Drawing2D.DashStyle.Dash);
                    }
                    else
                    {
                        col = Alfa(PtaColorTenue, 150);
                        pen = new RenderPen(Alfa(PtaColorTenue, 90), 1f, System.Drawing.Drawing2D.DashStyle.Dot);
                    }
                    // las de linea y EMA se dibujan por su historia de reposicionamiento; aca va el precio de las 09:15
                    int xh = c.Orden ? xFin : x1;
                    g.DrawLine(pen, x0, y, xh, y);
                    if (c.Orden && c.Tipo == "fijo")
                    {
                        // marquita corta a la derecha del nivel con limitada, sin punta
                        g.FillRectangle(col, new Rectangle(xh - 3, y - 2, 4, 4));
                    }
                    if (PtaVerEtiquetas)
                    {
                        string txt = (c.Orden ? (c.Lado > 0 ? "C " : "V ") : "") + Etiqueta(c.Familia) + " " + c.Precio.ToString("0.00", Es);
                        TextoDerecha(g, c.Orden ? f : fChica, txt, col, xEtq, y, c.Lado >= 0);
                    }
                }
                // reposicionamiento de la EMA (N5) dentro de la ventana: escalones
                foreach (var od in s.Ordenes)
                {
                    if (od.Tipo != "ema" || s.EmaRepos.Count == 0) continue;
                    var col = Alfa(od.Lado > 0 ? PtaColorCompra : PtaColorVenta, 200);
                    var pen = new RenderPen(col, 1.2f, System.Drawing.Drawing2D.DashStyle.Dash);
                    double precio = od.PrecioInicial; int xa = x0;
                    foreach (var kv in s.EmaRepos)
                    {
                        int bx = BarDe(kv.Key); int xb = X(bx) - 2;
                        if (xb > xFin) break;
                        int y = Y(precio);
                        g.DrawLine(pen, xa, y, xb, y);
                        g.DrawLine(pen, xb, y, xb, Y(kv.Value));
                        precio = kv.Value; xa = xb;
                    }
                    g.DrawLine(pen, xa, Y(precio), xFin, Y(precio));
                }
            }
            if (PtaVerRectas)
            {
                foreach (var od in s.Ordenes)
                {
                    if (od.Tipo != "linea" || od.Recta == null) continue;
                    var r = od.Recta;
                    var pen = new RenderPen(Alfa(od.Lado > 0 ? PtaColorCompra : PtaColorVenta, 170), 1f);
                    int bp1 = BarDe(r.UtcPivote1), bp2 = BarDe(r.UtcPivote2), bF = BarDe(s.UtcFinOrd);
                    // la recta vive en minutos desde las 18:00 de la vispera; el valor en una vela = su centro
                    Func<int, double> valor = b => r.Valor((_utc[b] - s.T0Utc) / 60.0 + 0.5);
                    int xa = X(bp1), xb = X(bF);
                    g.DrawLine(pen, xa, Y(r.Y1), xb, Y(valor(bF)));
                    g.FillEllipse(Alfa(od.Lado > 0 ? PtaColorCompra : PtaColorVenta, 200), new Rectangle(X(bp1) - 3, Y(r.Y1) - 3, 6, 6));
                    g.FillEllipse(Alfa(od.Lado > 0 ? PtaColorCompra : PtaColorVenta, 200), new Rectangle(X(bp2) - 3, Y(r.Y2) - 3, 6, 6));
                }
            }
        }

        private void TextoDerecha(RenderContext g, RenderFont f, string txt, Color col, int xDer, int y, bool arriba)
        {
            var m = g.MeasureString(txt, f);
            int x = Math.Max(0, xDer - m.Width - 2);
            int yy = arriba ? y - m.Height - 1 : y + 1;
            g.FillRectangle(Color.FromArgb(150, 11, 16, 23), new Rectangle(x - 1, yy, m.Width + 2, m.Height));
            g.DrawString(txt, f, col, x, yy);
        }

        /// <summary>La marca del disparo: un circulo (lleno P1, hueco P2) en el precio y la vela de la entrada, SIN punta
        /// de flecha; desde ahi el stop (fino, rojo) y el objetivo (fino, verde) hasta la salida; el stop en la entrada
        /// (punteado) desde que se arma; un cuadradito en la salida con el resultado bruto.</summary>
        private void PintarOperacion(RenderContext g, Operacion op, bool esP2, Func<int, int> X, Func<double, int> Y, RenderFont f, int anchoVela, int cuenta)
        {
            int bE = BarDe(op.UtcEnt); if (bE < 0) return;
            int bS = op.Abierta ? cuenta - 1 : BarDe(op.UtcSal);
            int xe = X(bE), xs = X(bS) + anchoVela / 2;
            int ye = Y(op.Entrada), yStop = Y(op.Stop), yObj = Y(op.Objetivo);
            var colLado = D(op.Lado > 0 ? PtaColorCompra : PtaColorVenta);
            int r = esP2 ? 6 : 5;
            var penStop = new RenderPen(Alfa(PtaColorStop, esP2 ? 120 : 200), 1f, esP2 ? System.Drawing.Drawing2D.DashStyle.Dot : System.Drawing.Drawing2D.DashStyle.Solid);
            var penObj = new RenderPen(Alfa(PtaColorObjetivo, esP2 ? 120 : 200), 1f, esP2 ? System.Drawing.Drawing2D.DashStyle.Dot : System.Drawing.Drawing2D.DashStyle.Solid);
            g.DrawLine(penStop, xe, yStop, xs, yStop);
            g.DrawLine(penObj, xe, yObj, xs, yObj);
            g.DrawLine(new RenderPen(Color.FromArgb(120, colLado), 1f), xe, ye, xs, ye);
            if (op.BeArmado && op.UtcBe >= 0)
            {
                int xb = X(BarDe(op.UtcBe));
                g.DrawLine(new RenderPen(Color.FromArgb(200, 230, 230, 230), 1f, System.Drawing.Drawing2D.DashStyle.Dot), xb, ye, xs, ye);
                g.DrawString("BE", f, Color.FromArgb(220, 230, 230, 230), xb + 2, ye - g.MeasureString("BE", f).Height);
            }
            var caja = new Rectangle(xe - r, ye - r, 2 * r, 2 * r);
            if (esP2) g.DrawEllipse(new RenderPen(colLado, 1.5f), caja);
            else { g.FillEllipse(colLado, caja); g.DrawEllipse(new RenderPen(Color.FromArgb(230, 250, 250, 250), 1f), caja); }
            string rot = (esP2 ? "P2 " : "P1 ") + (op.Lado > 0 ? "compra " : "venta ") + Etiqueta(op.Familia) + (op.EntradaPor == "repos_mercado" ? " (a mercado)" : "");
            int yRot = op.Lado > 0 ? ye + r + 1 : ye - r - 1 - g.MeasureString(rot, f).Height;
            g.DrawString(rot, f, colLado, xe + r + 3, yRot);
            if (!op.Abierta)
            {
                int ys = Y(op.Salida);
                g.DrawRectangle(new RenderPen(colLado, 1f), new Rectangle(xs - 3, ys - 3, 6, 6));
                string res = (op.Bruto > 0 ? "+" : "") + op.Bruto.ToString("0.00", Es) + " " + op.Motivo;
                var colR = op.Bruto > 0 ? D(PtaColorObjetivo) : op.Bruto < 0 ? D(PtaColorStop) : Color.FromArgb(230, 230, 230, 230);
                int yR = op.Lado > 0 ? ys - g.MeasureString(res, f).Height - 4 : ys + 4;
                g.DrawString(res, f, colR, xs + 4, yR);
            }
        }

        private string ResumenOp(Operacion op)
        {
            string s = string.Format(Es, "P{0} {1} {2:0.00} ({3}) {4}", op.Regla, op.Lado > 0 ? "COMPRA" : "VENTA", op.Entrada, Etiqueta(op.Familia), RelojNy.Hms(op.SodEnt));
            if (op.Abierta) s += string.Format(Es, " ABIERTA | stop {0:0.00} objetivo {1:0.00}{2}", op.StopVigente, op.Objetivo, op.BeArmado ? " (stop en la entrada)" : "");
            else s += string.Format(Es, " -> {0:0.00} {1} {2:+0.00;-0.00;0.00} pts ({3})", op.Salida, RelojNy.Hms(op.SodSal), op.Bruto, op.Motivo);
            return s;
        }

        /// <summary>El rotulo. No es decoracion: sin el, alguien puede confundir estas marcas con una estrategia probada.</summary>
        private void Rotulo(RenderContext g, Rectangle area)
        {
            var fT = new RenderFont("Consolas", Math.Max(7f, PtaLetra + 1f));
            var f = new RenderFont("Consolas", Math.Max(6f, PtaLetra - 0.5f));
            var amarillo = Color.FromArgb(250, 170, 40);
            var texto = Color.FromArgb(225, 230, 236);
            var lineas = new List<(string s, Color c, RenderFont f)>();
            lineas.Add(("PAUTA " + Version + " - EN PRUEBA: no validado | replica de 'la pauta' (P1 marcaciones) | NO opera, solo dibuja y anota", amarillo, fT));
            lineas.Add(("Medido 24-09 P1: MNQ 42 op/36 ses, 9,5 % gan (necesita 15,7), -3,1 pts/op neto, t -1,4 | MES 41 op, 7,3 %, -1,2 pts/op, t -2,4 | 0 de 7 criterios, no supera el azar", texto, f));
            lineas.Add(("Medido 24-09 P2 (extremos 8:15-9:15): MNQ 34 op, 8,8 %, -4,2 pts/op, t -1,7 | MES 27 op, 7,4 %, -1,7 pts/op, t -2,7 | percentiles 9-44 contra el azar", texto, f));

            var p = _n.P;
            string marco = _paso >= 60 ? (_paso / 60) + " min" : _paso + " s";
            string estado = string.Format(Es, "Marco {0} | escala {1}: D {2:0.##} / stop {3:0.##} / objetivo {4:0.##} / BE {5} % | ventana {6}-{7} NY, cierre {8} | registros hoy {9}",
                marco, _escalaUsada, p.D, p.Stop, p.Objetivo, _bePorc, RelojNy.Hm(p.HCongela), RelojNy.Hm(p.HFinOrdenes), RelojNy.Hm(p.HCierre), _registrosHoy);
            lineas.Add((estado, texto, f));
            if (_paso > 60) lineas.Add(("OJO: marco mayor a 1 min. Los niveles de 1 min (N4, EMA 200, P0) salen de estas velas y NO son los medidos.", amarillo, f));
            else if (_paso == 60 && _escalaUsada == "NQ/MNQ") lineas.Add(("Marco 1 min en NQ/MNQ: los niveles son los medidos; la ejecucion dentro del minuto se resuelve como en MES 1 min (lo medido en MNQ fue con velas de 1 s).", texto, f));

            int n = _n.Sesiones.Count;
            for (int i = Math.Max(0, n - 2); i < n; i++)
            {
                var s = _n.Sesiones[i];
                string rot = i == n - 1 ? "en curso" : "anterior";
                string cab = string.Format(Es, "Sesion {0:00}-{1:00} ({2}): ", s.Sesion % 100, (s.Sesion / 100) % 100, rot);
                if (!s.Congelada)
                {
                    lineas.Add((cab + "niveles se congelan a las " + RelojNy.Hm(p.HCongela) + " NY (con la primera vela cerrada despues)", texto, f));
                    continue;
                }
                var fams = new List<string>();
                foreach (var od in s.Ordenes) fams.Add((od.Lado > 0 ? "C " : "V ") + Etiqueta(od.Familia) + " " + od.Precio.ToString("0.00", Es));
                string n4 = double.IsNaN(s.N4Hi) ? "" : string.Format(Es, " | N4 {0:0.00}/{1:0.00}", s.N4Hi, s.N4Lo);
                lineas.Add((cab + string.Format(Es, "P0 {0:0.00} | {1} limitada(s) P1: {2}{3}{4}", s.P0, s.Ordenes.Count,
                    fams.Count > 0 ? string.Join("; ", fams) : "ninguna dentro de D", n4, string.IsNullOrEmpty(s.Notas) ? "" : " | " + s.Notas), texto, f));
                if (s.OpsP1.Count == 0 && s.OpsP2.Count == 0) lineas.Add(("   sin disparo" + (s.UtcUltima < s.UtcFinOrd ? " (todavia)" : ""), texto, f));
                foreach (var op in s.OpsP1) lineas.Add(("   " + ResumenOp(op), op.Abierta ? amarillo : texto, f));
                foreach (var op in s.OpsP2)
                {
                    bool igual = false;
                    foreach (var o1 in s.OpsP1) if (o1.UtcEnt == op.UtcEnt && o1.Lado == op.Lado && o1.Entrada == op.Entrada) { igual = true; break; }
                    lineas.Add(("   " + ResumenOp(op) + (igual ? " = misma entrada que P1" : ""), op.Abierta ? amarillo : texto, f));
                }
            }

            int w = 0, h = 0;
            foreach (var l in lineas) { var m = g.MeasureString(l.s, l.f); if (m.Width > w) w = m.Width; h += m.Height + 1; }
            int x0 = area.Left + 6, y0 = area.Top + Math.Max(0, PtaBajarRotulo);
            g.FillRectangle(Color.FromArgb(205, 11, 16, 23), new Rectangle(x0, y0, w + 14, h + 8));
            g.DrawRectangle(new RenderPen(Color.FromArgb(150, amarillo), 1f), new Rectangle(x0, y0, w + 14, h + 8));
            int y = y0 + 4;
            foreach (var l in lineas) { g.DrawString(l.s, l.f, l.c, x0 + 7, y); y += g.MeasureString(l.s, l.f).Height + 1; }
        }

        // ==================================================================
        // Log propio (ATAS se traga las excepciones de los indicadores)
        // ==================================================================
        private static void Log(string msg)
        {
            try
            {
                var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "pythiagex2-pauta.log");
                File.AppendAllText(p, DateTime.Now.ToString("s") + "  " + msg + "\n");
            }
            catch { }
        }

        // Freno del log de excepciones (revision adversarial 24-09): si algo falla en cada OnCalculate u OnRender, sin esto
        // se escribiria en disco por cada tick. La misma excepcion se anota una vez cada 30 s, con cuantas se callaron.
        private static readonly object _llaveLog = new object();
        private static string _ultErr = ""; private static long _ultErrTick; private static int _errCallados;
        private static void Anotar(Exception e)
        {
            try
            {
                string clave = e.GetType().Name + ": " + e.Message;
                long ahora = Environment.TickCount64;
                lock (_llaveLog)
                {
                    if (clave == _ultErr && ahora - _ultErrTick < 30000) { _errCallados++; return; }
                    int callados = _errCallados; _errCallados = 0; _ultErr = clave; _ultErrTick = ahora;
                    var st = e.StackTrace ?? "";
                    Log("EXCEPCION " + clave + (callados > 0 ? " (x" + (callados + 1) + " en 30 s)" : "") + " | " + st.Replace((char)10, ' ').Substring(0, Math.Min(300, st.Length)));
                }
            }
            catch { }
        }
    }
}
