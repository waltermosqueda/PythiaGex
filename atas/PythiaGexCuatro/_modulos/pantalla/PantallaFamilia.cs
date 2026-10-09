// PantallaFamilia.cs — PythiaGex 4.1, modulo pantalla (B5), 08-10-2026. Implementa IPantallaFamilia (ContratosPantalla.cs).
// Arma la vista desde el grafico (IGraficoPantalla), llama al armado puro (ArmadoPantalla) y pinta las primitivas en el RenderContext de
// ATAS. Hilo de dibujo: lee la FotoFamilia publicada (inmutable) y nada mas; sin E/S (el log va por el ThreadPool, 1 linea por minuto
// y lugar como maximo); sin llaves del motor. Nunca tira: ATAS se traga las excepciones del render.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGexCuatro.Familia
{
    public sealed class PantallaFamilia : IPantallaFamilia
    {
        public const string VERSION = "pantalla 4.1.4 (09-10-2026, rotulos de los tramos de historia)";
        private const long M2 = 120_000;
        private const int TOPE_VELAS = 20000;      // con el grafico muy alejado no se recorren mas velas que esto (la 4.0.6 no tenia tope)

        private sealed class CajaRect { public readonly Rectangle R; public CajaRect(Rectangle r) { R = r; } }

        private readonly IGraficoPantalla _g;
        private readonly AjustesPantalla _aj = new AjustesPantalla();
        private readonly VistaPantalla _v = new VistaPantalla();
        private readonly DibujoPantalla _d = new DibujoPantalla();
        private readonly Dictionary<float, RenderFont> _fuentes = new Dictionary<float, RenderFont>();
        private readonly Dictionary<(int, float), RenderPen> _plumas = new Dictionary<(int, float), RenderPen>();
        private readonly Func<double, int> _y;
        private readonly Func<string, float, Size> _medir;
        private RenderContext _gActual;
        private volatile CajaRect _pestana;
        private volatile IReadOnlyList<SerieInfo> _catFuente;
        private IReadOnlyList<SerieInfo> _catDe;
        private CatalogoPantalla _cat;
        private int _renders;
        private DateTime _ultErrorUtc = DateTime.MinValue;
        private int _fallasPrim;

        public PantallaFamilia(IGraficoPantalla grafico)
        {
            _g = grafico ?? throw new ArgumentNullException(nameof(grafico));
            _y = p => _g.YDePrecio(p);
            _medir = Medir;
        }

        public IReadOnlyList<SerieInfo> Catalogo { get => _catFuente; set => _catFuente = value; }
        /// <summary>El ultimo armado. Solo para el hilo de dibujo o el arnes (se reusa en cada render).</summary>
        public DibujoPantalla UltimoDibujo => _d;

        private CatalogoPantalla Cat()
        {
            var fuente = _catFuente;
            if (_cat == null || !ReferenceEquals(fuente, _catDe)) { _cat = new CatalogoPantalla(fuente); _catDe = fuente; }
            return _cat;
        }

        private static long PisoM2(long ms) => ms >= 0 ? ms / M2 * M2 : ((ms - M2 + 1) / M2) * M2;

        public void Pintar(RenderContext g, FotoFamilia foto)
        {
            if (g == null) return;
            try
            {
                if (!_g.Contenedor(out var area)) return;
                _aj.Limpiar(); _g.Ajustes(_aj);
                var v = _v;
                v.Area = area;
                int xr = area.Right;
                try { var cb = g.ClipBounds; if (cb.Width > 0) xr = Math.Min(area.Right, cb.Right); } catch { }
                v.XDerecha = xr;
                v.Instrumento = _g.Instrumento ?? "";
                v.PrecioAlto = _g.PrecioAlto; v.PrecioBajo = _g.PrecioBajo; v.Y = _y;
                int cur = _g.BarraActual;
                int desde = Math.Max(0, _g.PrimeraVisible), hasta = Math.Min(cur - 1, _g.UltimaVisible);
                if (hasta - desde > TOPE_VELAS) desde = hasta - TOPE_VELAS;
                int bw = 6;
                if (hasta > desde)
                {
                    int xa = _g.XDeBarra(hasta), xb = _g.XDeBarra(desde);
                    if (xa != int.MinValue && xb != int.MinValue) bw = Math.Max(1, (xa - xb) / Math.Max(1, hasta - desde));
                }
                v.AnchoVela = bw;
                v.Velas.Clear();
                if (_aj.Estela && _aj.Visibles.Count > 0 && hasta >= desde && foto?.HistoriaM2 != null && foto.HistoriaM2.Count > 0)
                    for (int b = desde; b <= hasta; b++)
                    {
                        if (!_g.Vela(b, out long ms, out _)) continue;
                        int x = _g.XDeBarra(b); if (x == int.MinValue) continue;
                        v.Velas.Add((x, PisoM2(ms)));
                    }
                v.UltimaVisible = _g.UltimaVisible >= cur - 1;
                v.PrecioUltimo = cur > 0 && _g.Vela(cur - 1, out _, out double cierre) ? cierre : double.NaN;

                _gActual = g;
                ArmadoPantalla.Armar(_d, foto, Cat(), _aj, v, DateTime.UtcNow, _medir);
                PintarPrims(g, _d);
                _pestana = _d.Pestana.IsEmpty ? null : new CajaRect(_d.Pestana);

                if (_renders++ == 0)
                    BitacoraPantalla.Linea(VERSION + ": primer render area=" + area + " clip.derecha=" + xr + " instrumento=" + v.Instrumento
                        + " velas visibles=" + (hasta - desde + 1) + " catalogo=" + (_cat.DelMotor ? "motor" : "respaldo de la pantalla")
                        + " foto=" + (foto == null ? "null" : "sesion " + foto.Sesion + " contrato " + foto.Instrumento + " velas m2 " + (foto.HistoriaM2?.Count ?? 0) + " actuales " + (foto.Actuales?.Count ?? 0))
                        + " | " + Resumen(_aj) + (_d.Cartel != "" ? " | cartel: " + _d.Cartel : "")
                        + " | rotulos de tramos " + _d.RotulosTramos.Count + " (tramos " + _d.TramosLargos + ", vigentes " + _d.TramosVigentes + ", sin lugar o tope " + _d.TramosDescartados
                        + ", recortados " + _d.TramosRecortados + ")" + (_d.RotulosTramos.Count > 0 ? ": " + string.Join(" | ", _d.RotulosTramos.Select(r => r.Txt)) : ""));
            }
            catch (Exception e) { Error("Pintar", e); }
            finally { _gActual = null; }
        }

        public bool Clic(int x, int y)
        {
            var r = _pestana;
            if (r == null || !r.R.Contains(x, y)) return false;
            try { _g.PanelAbierto = !_g.PanelAbierto; _g.Redibujar(); }
            catch (Exception e) { Error("Clic", e); }
            return true;
        }

        /// <summary>Texto de los ajustes para el log de arranque ("el log lista los ajustes leidos").</summary>
        public static string Resumen(AjustesPantalla a)
        {
            if (a == null) return "";
            var on = SeriesPantalla.Casillas.Where(c => a.Visible(c.Id)).Select(c => c.Id).ToArray();
            return "casillas prendidas (" + on.Length + "): " + (on.Length == 0 ? "ninguna" : string.Join(",", on)) + " · eje=" + a.Eje + " estela=" + a.Estela
                 + " etiquetas=" + a.Rotulos + " pestaña=" + a.Cabecera + " abierta=" + a.PanelAbierto + " letra=" + a.Letra + " margen=" + a.MargenSup
                 + " montos=" + a.Montos + " cambios=" + a.Cambios + " ventana=" + a.VentanaMin + " min" + " tramos=" + a.Tramos;
        }

        // ------------------------------------------------------------------ pintar
        private Size Medir(string t, float tam)
        {
            var g = _gActual;
            if (g == null || string.IsNullOrEmpty(t)) return new Size(0, (int)Math.Ceiling(tam * 1.4));
            return g.MeasureString(t, Fuente(tam));
        }

        private RenderFont Fuente(float tam)
        {
            if (!_fuentes.TryGetValue(tam, out var f)) { f = new RenderFont("Consolas", tam); _fuentes[tam] = f; }
            return f;
        }

        private RenderPen Pluma(Color c, float w)
        {
            var k = (c.ToArgb(), w);
            if (!_plumas.TryGetValue(k, out var p)) { if (_plumas.Count > 512) _plumas.Clear(); p = new RenderPen(c, w); _plumas[k] = p; }
            return p;
        }

        private void PintarPrims(RenderContext g, DibujoPantalla d)
        {
            var ps = d.Prims;
            for (int i = 0; i < ps.Count; i++)
            {
                var p = ps[i];
                try
                {
                    switch (p.Tipo)
                    {
                        case TipoPrimPantalla.Relleno: g.FillRectangle(p.C, p.R); break;
                        case TipoPrimPantalla.Borde: g.DrawRectangle(Pluma(p.C, p.A), p.R); break;
                        case TipoPrimPantalla.Linea: g.DrawLine(Pluma(p.C, p.A), p.R.X, p.R.Y, p.X2, p.Y2); break;
                        case TipoPrimPantalla.Texto: g.DrawString(p.T, Fuente(p.A), p.C, p.R.X, p.R.Y); break;
                    }
                }
                catch (Exception e) { if (_fallasPrim++ < 3) Error("primitiva " + p.Tipo, e); }
            }
        }

        private void Error(string donde, Exception e)
        {
            var ahora = DateTime.UtcNow;
            if ((ahora - _ultErrorUtc).TotalSeconds < 60) return;
            _ultErrorUtc = ahora;
            BitacoraPantalla.Linea("ERROR " + donde + ": " + e);
        }
    }

    /// <summary>Log propio de la pantalla: %APPDATA%\ATAS\pythiagex4-pantalla.log. La escritura va por el ThreadPool (nunca en el hilo de dibujo).</summary>
    public static class BitacoraPantalla
    {
        private static readonly object _l = new object();
        public static string Ruta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "pythiagex4-pantalla.log");
        public static void Linea(string m)
        {
            string linea = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + m + Environment.NewLine;
            try
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { lock (_l) { Directory.CreateDirectory(Path.GetDirectoryName(Ruta)); File.AppendAllText(Ruta, linea, new UTF8Encoding(false)); } }
                    catch { }
                });
            }
            catch { }
        }
    }
}
