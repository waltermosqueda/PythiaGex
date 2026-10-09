using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGexTres
{
    /// <summary>
    /// PythiaGex 3.0 - Sonda API.
    ///
    /// Prueba la cadena viva por la API publica de ATAS 8.0.15 (CadenaApi) y
    /// deja constancia de todo. Casi no dibuja: un renglon de estado arriba a la
    /// izquierda. Lo que importa queda en dos archivos:
    ///
    ///   %APPDATA%\ATAS\pythiagex3-sonda.log
    ///       cada paso: series listadas (tipo y vencimiento), contratos por
    ///       serie, suscripciones aceptadas/rechazadas con el mensaje de la
    ///       excepcion, el primer Summary recibido campo por campo, y los
    ///       conteos por minuto.
    ///
    ///   %APPDATA%\ATAS\PythiaGex3\viva\viva3-&lt;NQ|ES&gt;-&lt;dia&gt;.jsonl
    ///       una linea por minuto con EXACTAMENTE el formato de la viva vieja
    ///       (viva-NQ-&lt;dia&gt;.jsonl de PythiaGex), para que el laboratorio la
    ///       lea con los mismos cargadores y pueda comparar 3.0 contra 1.x/2.0:
    ///       {ts, futuro, grandes, campos:"strike,dias,es_call,oi,iv,bid,ask,
    ///        vol_hoy,vol_cinta,vol_compra,vol_venta,strike0", filas:[...]}
    ///       vol_cinta/vol_compra/vol_venta van en 0 (la API no trae cinta);
    ///       strike0 = strike crudo si fue corrido por el roll, 0 si no (misma
    ///       convencion que VivaJson de Gamma Hoy).
    ///
    /// TODO va envuelto en try/catch que loguea: ATAS se traga las excepciones.
    /// </summary>
    [DisplayName("PythiaGex 3.0 - Sonda API")]
    [Category("PythiaGex 3.0")]
    public class SondaApi : Indicator
    {
        private const string LOG = "sonda";
        private const string VERSION = "3.0.0";

        private readonly CadenaApi _cadena = new CadenaApi();
        private readonly TimeSpan _periodo = TimeSpan.FromSeconds(5);
        private Action _tick;
        private bool _arrancada;
        private DateTime _ultimoMinuto = DateTime.MinValue, _ultimoIntentoArranque = DateTime.MinValue, _ultimoRearme = DateTime.MinValue;
        private string _renglon = "PythiaGex 3.0 - Sonda API: arrancando...";
        private int _renders;

        // ------------------------------------------------------------------
        // ajustes
        // ------------------------------------------------------------------

        [Display(Name = "Raiz manual (vacio = del grafico)", GroupName = "1. Cadena", Order = 10,
                 Description = "NQ o ES. Vacio: se deduce del instrumento del grafico (MNQ -> NQ, MES -> ES).")]
        public string Sonda3RaizManual { get; set; } = "";

        [Display(Name = "Tope de contratos", GroupName = "1. Cadena", Order = 20,
                 Description = "Suscripciones vivas por instancia. El proveedor de ATAS corta en 512; aca 320 por defecto.")]
        [Range(20, 480)]
        public int Sonda3TopeContratos { get; set; } = 320;

        [Display(Name = "Ventana densa (%)", GroupName = "1. Cadena", Order = 30, Description = "Todos los strikes hasta este % del precio.")]
        public decimal Sonda3VentanaDensaPct { get; set; } = 1.0m;

        [Display(Name = "Ventana rala (%)", GroupName = "1. Cadena", Order = 40, Description = "Uno de cada dos strikes hasta este % del precio.")]
        public decimal Sonda3VentanaRalaPct { get; set; } = 2.0m;

        [Display(Name = "Vencimientos (fechas)", GroupName = "1. Cadena", Order = 50, Description = "Las fechas mas cercanas. El viernes de la trimestral una fecha trae dos series.")]
        [Range(1, 4)]
        public int Sonda3Vencimientos { get; set; } = 2;

        [Display(Name = "Recentrar al alejarse (%)", GroupName = "1. Cadena", Order = 60, Description = "Rearma la ventana cuando el precio se fue mas que esto del centro (minimo 60 s entre rearmes).")]
        public decimal Sonda3RecentrarPct { get; set; } = 0.5m;

        [Display(Name = "Guardar viva3 (jsonl por minuto)", GroupName = "2. Archivo", Order = 10,
                 Description = "viva3-<raiz>-<dia>.jsonl en %APPDATA%\\ATAS\\PythiaGex3\\viva, mismo formato que la viva vieja.")]
        public bool Sonda3GuardarViva { get; set; } = true;

        [Display(Name = "Renglon de estado en el grafico", GroupName = "3. Pantalla", Order = 10)]
        public bool Sonda3VerEnGrafico { get; set; } = true;

        [Display(Name = "Rearmar ahora", GroupName = "4. Accion", Order = 10, Description = "Cambialo a mano para forzar un rearme de la ventana.")]
        public bool Sonda3RearmarAhora
        {
            get => false;
            set { if (value) { try { Log("rearme pedido a mano"); _ = Task.Run(() => _cadena.Rearmar(OptionsDataProvider, TradingManager, DataProvider)); } catch (Exception e) { Registro.Excepcion(LOG, "Sonda3RearmarAhora", e); } } }
        }

        // ------------------------------------------------------------------
        // ciclo de vida
        // ------------------------------------------------------------------

        /// <summary>Sin esto ATAS no llama a OnRender nunca (verificado en Gamma Hoy). La serie por defecto se esconde.</summary>
        public SondaApi() : base(true)
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

        protected override void OnInitialize()
        {
            try
            {
                Log("Sonda API " + VERSION + " arranca. instrumento=" + (InstrumentInfo?.Instrument ?? "?") + " security=" + (TradingManager?.Security?.Code ?? "?")
                    + " raiz=" + Raiz() + " tope=" + Sonda3TopeContratos + " ventana=" + Sonda3VentanaDensaPct + "/" + Sonda3VentanaRalaPct + " % vencimientos=" + Sonda3Vencimientos
                    + " datos=" + Registro.CarpetaDatos);
                _tick = Tick;
                SubscribeToTimer(_periodo, _tick);
            }
            catch (Exception e) { Registro.Excepcion(LOG, "OnInitialize", e); }
        }

        protected override void OnCalculate(int bar, decimal value) { }

        protected override void OnDispose()
        {
            try { if (_tick != null) UnsubscribeFromTimer(_periodo, _tick); } catch { }
            try { _cadena.Parar(); } catch (Exception e) { Registro.Excepcion(LOG, "OnDispose", e); }
            Log("sonda quitada del grafico");
        }

        // ------------------------------------------------------------------
        // el latido: cada 5 s
        // ------------------------------------------------------------------

        private void Tick()
        {
            try
            {
                var ahora = DateTime.UtcNow;
                AplicarAjustes();

                if (!_arrancada || (!_cadena.Activa && (ahora - _ultimoIntentoArranque).TotalSeconds >= 120))
                {
                    _arrancada = true; _ultimoIntentoArranque = ahora;
                    _ = Task.Run(() => _cadena.Arrancar(OptionsDataProvider, TradingManager, DataProvider, CodigoGrafico(), PrecioGrafico, Log));
                }
                else
                {
                    _cadena.Latido();
                    if (_cadena.HayQueRecentrar() && (ahora - _ultimoRearme).TotalSeconds >= 60)
                    {
                        _ultimoRearme = ahora;
                        Log("el precio se alejo del centro (" + _cadena.Futuro.ToString("0.##", CultureInfo.InvariantCulture) + " vs " + _cadena.CentroVentana.ToString("0.##", CultureInfo.InvariantCulture) + "): rearmo");
                        _ = Task.Run(() => _cadena.Rearmar(OptionsDataProvider, TradingManager, DataProvider));
                    }
                }

                var foto = _cadena.Foto();
                _renglon = Renglon(foto);

                if ((ahora - _ultimoMinuto).TotalSeconds >= 60)
                {
                    _ultimoMinuto = ahora;
                    var (enRes, enSec) = _cadena.OrigenDePuntas();
                    Log("minuto: via=" + foto.Via + " series=" + foto.Series + " contratos=" + foto.Contratos + " suscritos=" + foto.Suscritos + " conResumen=" + foto.ConResumen
                        + " conPuntas=" + foto.ConPuntas + " (resumen " + enRes + ", soloSecurity " + enSec + ") conOI=" + foto.ConOI + " rechazadas=" + foto.Rechazadas + " errores=" + foto.Errores
                        + " lookups=" + foto.Lookups + " cambios=" + _cadena.Cambios + " futuro=" + _cadena.Futuro.ToString("0.##", CultureInfo.InvariantCulture)
                        + " edad=" + (double.IsNaN(foto.EdadSegundos) ? "-" : foto.EdadSegundos.ToString("0") + " s") + " | " + foto.Texto);
                    if (Sonda3GuardarViva) GuardarVivaAhora();
                }
                try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "Tick", e); }
        }

        private void AplicarAjustes()
        {
            _cadena.TopeContratos = Math.Max(20, Math.Min(480, Sonda3TopeContratos));
            _cadena.VentanaDensaPct = (double)Math.Max(0.1m, Sonda3VentanaDensaPct);
            _cadena.VentanaRalaPct = (double)Math.Max(Sonda3VentanaDensaPct, Sonda3VentanaRalaPct);
            _cadena.Vencimientos = Math.Max(1, Math.Min(4, Sonda3Vencimientos));
            _cadena.RecentrarPct = (double)Math.Max(0.1m, Sonda3RecentrarPct);
        }

        private string Raiz()
        {
            if (!string.IsNullOrWhiteSpace(Sonda3RaizManual)) return Sonda3RaizManual.Trim().ToUpperInvariant();
            return CadenaApi.RaizGrande(TradingManager?.Security?.Code ?? InstrumentInfo?.Instrument ?? "");
        }

        /// <summary>El codigo del contrato del grafico (MNQZ6) si lo hay; si no el instrumento (#MNQ). Con raiz manual, la raiz.</summary>
        private string CodigoGrafico()
        {
            if (!string.IsNullOrWhiteSpace(Sonda3RaizManual)) return Sonda3RaizManual.Trim().ToUpperInvariant();
            var c = TradingManager?.Security?.Code;
            if (!string.IsNullOrEmpty(c)) return c;
            return InstrumentInfo?.Instrument ?? "";
        }

        private double PrecioGrafico()
        {
            try { return CurrentBar > 0 ? (double)GetCandle(Math.Max(0, CurrentBar - 1)).Close : 0; } catch { return 0; }
        }

        // ------------------------------------------------------------------
        // el archivo viva3
        // ------------------------------------------------------------------

        /// <summary>Factorizado a Viva3 (06-10): la sonda y Gamma Hoy 3.0 escriben la misma linea; un solo escritor por raiz.</summary>
        private void GuardarVivaAhora()
        {
            try
            {
                var raiz = _cadena.Raiz; if (string.IsNullOrEmpty(raiz)) raiz = Raiz();
                Viva3.Guardar(_cadena, raiz, this, LOG);
            }
            catch (Exception e) { Registro.Excepcion(LOG, "GuardarVivaAhora", e); }
        }

        // ------------------------------------------------------------------
        // pantalla: un renglon
        // ------------------------------------------------------------------

        private static string Renglon(CadenaApi.Estado f)
        {
            var sb = new StringBuilder("PythiaGex 3.0 Sonda API: ");
            sb.Append(f.ApiDisponible || f.Via == "servicio" ? "API ok" : "API no").Append(string.IsNullOrEmpty(f.Via) ? "" : " (" + f.Via + ")");
            if (!string.IsNullOrEmpty(f.Futuro)) sb.Append(" | ").Append(f.Futuro);
            sb.Append(" | ").Append(f.Series).Append(" series | ").Append(f.Suscritos).Append('/').Append(f.Contratos).Append(" contratos | ")
              .Append(f.ConPuntas).Append(" con puntas | ").Append(f.ConOI).Append(" con OI");
            if (f.Rechazadas > 0) sb.Append(" | ").Append(f.Rechazadas).Append(" rechazadas");
            sb.Append(" | edad ").Append(double.IsNaN(f.EdadSegundos) ? "-" : f.EdadSegundos < 90 ? f.EdadSegundos.ToString("0") + " s" : (f.EdadSegundos / 60).ToString("0") + " min");
            if (f.Suscritos == 0 && !string.IsNullOrEmpty(f.Texto)) sb.Append(" | ").Append(f.Texto);
            return sb.ToString();
        }

        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            if (!Sonda3VerEnGrafico) return;
            try
            {
                var f = new RenderFont("Consolas", 10f);
                var m = g.MeasureString(_renglon, f);
                var x = ChartArea.Left + 8; var y = ChartArea.Top + 6;
                g.FillRectangle(Color.FromArgb(225, 8, 12, 18), new Rectangle(x, y, m.Width + 12, m.Height + 6));
                g.DrawString(_renglon, f, Color.FromArgb(220, 228, 236), x + 6, y + 3);
                if (_renders++ == 0) Log("primer render: area=" + ChartArea);
            }
            catch (Exception e) { Registro.Excepcion(LOG, "OnRender", e); }
        }

        private static void Log(string msg) => Registro.Linea(LOG, msg);
    }
}
