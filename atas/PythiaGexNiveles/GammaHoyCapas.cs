using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGex
{
    /// <summary>La razon futuro/ETF de un libro por razon (vela alineada + mediana de la rueda). Una por libro:
    /// la del indicador (Libro = CBOE_ETF) y una por cada capa extra (15-09).</summary>
    public sealed class RazonEtf
    {
        public readonly List<double> Obs = new();
        public double Rueda = double.NaN;
    }

    /// <summary>Una capa extra (15-09): un libro mas, calculado con el MISMO nucleo y dibujado con su color
    /// encima del grafico de NQ/MNQ. Solo baja su cadena, corre Calcular() y se dibuja; nada de la lectura
    /// primaria (centinela, gatillos, AUDIT, archivo, pelotitas) la lee. Ver conocimiento/traspasos/2026-09-15-capas-nq.md.</summary>
    public sealed class CapaLibro
    {
        public enum TipoCapa { EtfPorRazon, IndiceConBase, RithmicViva }

        public readonly string Nombre;        // "QQQ", "TQQQ", "NDX", "RITHMIC"
        public readonly string Ticker;        // lo que se baja: ultima-<Ticker>.json (ETF) / "NQ" (radar -> NDX) / "" (viva)
        public readonly TipoCapa Tipo;
        public readonly double Apalancamiento; // 1 (QQQ), 3 (TQQQ): solo cambia a que precio del futuro va cada strike
        public readonly Color Color;

        public Feed.Cadena C;                                   // la ultima cadena bajada
        public readonly GammaHoyNucleo Nucleo = new();          // su propio nucleo (fotos del Max Change propias, sin uso)
        public GammaHoyNucleo.Lectura L;                        // la ultima lectura (bajo el candado del indicador)
        public readonly RazonEtf Razon = new();
        public string Error = "";
        public DateTime UltimaBajada = DateTime.MinValue, UltimoCalculo = DateTime.MinValue;
        public Feed.Cadena CCalculada;                          // con que cadena se calculo L (referencia)

        public CapaLibro(string nombre, string ticker, TipoCapa tipo, double apalancamiento, Color color)
        { Nombre = nombre; Ticker = ticker; Tipo = tipo; Apalancamiento = apalancamiento; Color = color; }

        /// <summary>Edad del dato, dicha ANTES del numero (regla 4 del protocolo): "vivo", "N min" (ya con los 902 s de CBOE) o "sin dato".</summary>
        public string Edad(CultureInfo es)
        {
            var c = C;
            if (c == null) return "sin dato";
            if (c.EsFuturo) return "vivo";
            return (c.EdadMin + 902.0 / 60.0).ToString("0", es) + " min";
        }
    }

    public partial class GammaHoy
    {
        // ==================================================================
        // Capas extra (15-09): QQQ + TQQQ + NDX + Rithmic a la vez, en NQ/MNQ
        // ==================================================================
        [Display(Name = "Capa QQQ (celeste)", GroupName = "5. Capas extra (NQ)", Order = 1,
                 Description = "Libro de QQQ 0DTE por volumen (CBOE, 902 s tarde) llevado a NQ por razon, como la referencia. Solo en graficos de NQ/MNQ. Apagada no cambia nada.")]
        public bool CapaQqq { get; set; } = false;

        [Display(Name = "Capa TQQQ (magenta)", GroupName = "5. Capas extra (NQ)", Order = 2,
                 Description = "Libro de TQQQ (3x) 0DTE por volumen. Cada strike va a NQ con el apalancamiento (un strike a +3 % de TQQQ es NQ a +1 %). Las MAGNITUDES no son comparables con QQQ ni NDX: cada capa se normaliza a su propio maximo.")]
        public bool CapaTqqq { get; set; } = false;

        [Display(Name = "Capa NDX (gris)", GroupName = "5. Capas extra (NQ)", Order = 3,
                 Description = "Libro de NDX (CBOE, el mismo que 'Libro en vivo = CBOE_SPX' en NQ) con la base de la rueda de la lectura primaria; si la primaria es un ETF, cae a la base medida o teorica y lo dice.")]
        public bool CapaNdx { get; set; } = false;

        [Display(Name = "Capa Rithmic (lima)", GroupName = "5. Capas extra (NQ)", Order = 4,
                 Description = "Las opciones de NQ desde tu ATAS (la cadena viva). Necesita 'Cadena viva de Rithmic' prendida; no abre una segunda suscripcion.")]
        public bool CapaRithmic { get; set; } = false;

        [Display(Name = "Capas: dibujar barras", GroupName = "5. Capas extra (NQ)", Order = 10)]
        public bool CapasBarras { get; set; } = true;

        [Display(Name = "Capas: dibujar dominantes", GroupName = "5. Capas extra (NQ)", Order = 11)]
        public bool CapasDominantes { get; set; } = true;

        [Display(Name = "Capas: dibujar el zero gamma de cada una", GroupName = "5. Capas extra (NQ)", Order = 12)]
        public bool CapasZero { get; set; } = false;

        private readonly CapaLibro[] _capas =
        {
            new CapaLibro("QQQ", "QQQ", CapaLibro.TipoCapa.EtfPorRazon, 1, Color.FromArgb(80, 180, 255)),
            new CapaLibro("TQQQ", "TQQQ", CapaLibro.TipoCapa.EtfPorRazon, 3, Color.FromArgb(255, 90, 200)),
            new CapaLibro("NDX", "NQ", CapaLibro.TipoCapa.IndiceConBase, 1, Color.FromArgb(200, 200, 210)),
            new CapaLibro("RITHMIC", "", CapaLibro.TipoCapa.RithmicViva, 1, Color.FromArgb(170, 255, 90)),
        };
        private DateTime _ultimoAuditCapas = DateTime.MinValue;

        /// <summary>Solo en NQ/MNQ (no inventar capas de ES) y solo si su llave esta prendida.</summary>
        private bool CapaActiva(CapaLibro k)
        {
            if (Fuente == FuenteDatos.Archivo) return false;
            string raiz; try { raiz = Raiz(); } catch { return false; }
            if (raiz != "NQ") return false;
            switch (k.Nombre)
            {
                case "QQQ": return CapaQqq;
                case "TQQQ": return CapaTqqq;
                case "NDX": return CapaNdx;
                case "RITHMIC": return CapaRithmic;
            }
            return false;
        }

        /// <summary>La capa Rithmic se refresca desde el temporizador (cada SegundosLibroRithmic), reusando la viva.
        /// Si la primaria ya es Rithmic, comparte su cadena.</summary>
        private void RefrescarCapaRithmic(DateTime ahora)
        {
            var k = _capas.First(z => z.Tipo == CapaLibro.TipoCapa.RithmicViva);
            if (!CapaActiva(k)) return;
            if (Libro == LibroEnVivo.Rithmic_ES) { var c0 = _c; if (c0 != null && c0.EsFuturo) { k.C = c0; k.Error = ""; k.UltimaBajada = ahora; } return; }
            if (!_viva.Activa) { k.Error = UsarCadenaViva ? "viva: " + _viva.Estado : "prender 'Cadena viva de Rithmic'"; return; }
            if ((ahora - k.UltimaBajada).TotalSeconds < Math.Max(5, SegundosLibroRithmic)) return;
            k.UltimaBajada = ahora;
            try { var cv = DesdeViva(); if (cv != null) { k.C = cv; k.Error = ""; } }
            catch (Exception e) { k.Error = e.Message; Registrar(e); }
        }

        /// <summary>Baja las cadenas de las capas de CBOE, en serie y cada una en su try: una que falle no tumba
        /// a las otras ni a la primaria. Se llama al final de BajarFeed (misma cadencia que el feed).</summary>
        private async Task BajarCapas()
        {
            foreach (var k in _capas)
            {
                if (!CapaActiva(k) || k.Tipo == CapaLibro.TipoCapa.RithmicViva) continue;
                try
                {
                    Feed.Cadena c = null;
                    if (k.Tipo == CapaLibro.TipoCapa.EtfPorRazon)
                    {
                        c = await Feed.BajarUltima(UrlArchivo, k.Ticker, m => k.Error = m).ConfigureAwait(false);
                        if (c != null)
                        {
                            EscalarCon(c, k.Ticker, k.Razon);
                            c.Apalancamiento = k.Apalancamiento;
                            if (k.Apalancamiento != 1) c.Fuente += " x" + k.Apalancamiento.ToString("0", CultureInfo.InvariantCulture);
                        }
                    }
                    else // NDX: si la primaria ya es la cadena de NDX (CBOE_SPX en NQ), es la misma
                    {
                        var c0 = _c;
                        if (Libro == LibroEnVivo.CBOE_SPX && c0 != null && !c0.EsFuturo && !c0.PorRazon) c = c0;
                        else
                        {
                            c = await Feed.Bajar(Url, k.Ticker, m => k.Error = m).ConfigureAwait(false);
                            if (FeedMinuto)
                            {
                                var u = await Feed.BajarUltima(UrlArchivo, k.Ticker, null).ConfigureAwait(false);
                                if (u != null && (c == null || u.GeneradoUtc > c.GeneradoUtc)) c = u;
                            }
                        }
                    }
                    if (c != null) { k.C = c; k.Error = ""; k.UltimaBajada = DateTime.UtcNow; }
                }
                catch (Exception e) { k.Error = e.Message; Registrar(e); }
            }
        }

        /// <summary>Corre Calcular() de cada capa con los MISMOS ajustes que la primaria (copiados por reflexion,
        /// incluida la base de la rueda y el vencimiento del futuro). Solo cuando cambio su cadena o cada 5 s:
        /// cada Calcular son ~211 strikes x 61 pasos y ya se midio (10-09) que repreciar por tick funde un nucleo.
        /// Se llama al final de RepreciarCon.</summary>
        private void RepreciarCapas(double futuro, DateTime ahoraUtc)
        {
            bool audit = (ahoraUtc - _ultimoAuditCapas).TotalSeconds >= 60;
            if (audit) _ultimoAuditCapas = ahoraUtc;
            foreach (var k in _capas)
            {
                if (!CapaActiva(k)) { if (k.L != null) lock (_candado) k.L = null; continue; }
                var c = k.C;
                if (c == null) continue;
                if (ReferenceEquals(c, k.CCalculada) && (ahoraUtc - k.UltimoCalculo).TotalSeconds < 5) continue;
                var a = k.Nucleo.A; var de = _nucleo.A;
                var t = typeof(GammaHoyNucleo.Ajustes);
                foreach (var fi in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) fi.SetValue(a, fi.GetValue(de));
                foreach (var pi in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) if (pi.CanRead && pi.CanWrite) pi.SetValue(a, pi.GetValue(de));
                GammaHoyNucleo.Lectura L = null;
                try { L = k.Nucleo.Calcular(c, futuro, ahoraUtc); }
                catch (Exception e) { k.Error = e.Message; Registrar(e); }
                lock (_candado) { k.L = L; }
                k.UltimoCalculo = ahoraUtc; k.CCalculada = c;
                if (audit && L != null && !L.SinBase)
                    Log("AUDIT capa=" + k.Nombre + " " + GammaHoyNucleo.Audit(L, c, k.Tipo == CapaLibro.TipoCapa.RithmicViva).Substring(6));
            }
        }

        /// <summary>Dibujo de las capas: una columna de barras por capa a la derecha de la columna primaria (cada
        /// una normalizada a SU maximo; las negativas llevan un borde rojo), las dominantes como raya discontinua
        /// del color de la capa con rotulo "D1 QQQ 29.150" en su propia columna a la derecha, y una leyenda abajo a
        /// la izquierda con la edad de cada dato. La primaria se dibuja despues (encima). Se llama desde Pintar
        /// justo antes de las rayas de la primaria.</summary>
        private void PintarCapas(RenderContext g, IChartContainer cont, Rectangle area, int piso, int x0, int ancho, int alto,
                                 int xl0, int xl1, int altoRot, RenderFont fRot, CultureInfo es,
                                 Action<double, Color, float, System.Drawing.Drawing2D.DashStyle, int> raya)
        {
            var activas = _capas.Where(CapaActiva).ToList();
            if (activas.Count == 0) return;
            int anchoCapa = Math.Max(12, (int)(ancho * 0.45));
            int fila = 0;
            for (int i = 0; i < activas.Count; i++)
            {
                var k = activas[i];
                GammaHoyNucleo.Lectura L; lock (_candado) L = k.L;
                int xk = x0 + ancho + 4 + i * (anchoCapa + 4);
                var col = k.Color;

                // leyenda abajo a la izquierda, una linea por capa (el numero en su propia linea, nunca al lado de un control)
                string doms = L == null || L.Doms.Count == 0 ? "" : " · " + string.Join(" ", L.Doms.Select((d, j) => "D" + (j + 1) + " " + d.Fut.ToString("N0", es)));
                string zero = L == null || double.IsNaN(L.ZeroVol) ? "" : " · 0Γ " + L.ZeroVol.ToString("N0", es);
                string estado = L == null ? (k.C == null ? "sin dato" : "calculando") : k.Edad(es) + (L.SinBase ? " SIN BASE" : "");
                string ley = "■ " + k.Nombre + " " + estado + doms + zero + (string.IsNullOrEmpty(k.Error) ? "" : " · " + k.Error);
                int yl = piso - 4 - altoRot * (activas.Count - fila); fila++;
                var ml = g.MeasureString(ley, fRot);
                g.FillRectangle(Color.FromArgb(160, ColFondo), new Rectangle(x0 + 1, yl, ml.Width + 4, altoRot));
                g.DrawString(ley, fRot, Color.FromArgb(230, col), x0 + 3, yl);
                if (L == null || L.SinBase || L.Perfil.Count == 0) continue;

                if (CapasBarras)
                {
                    bool porOi = L.MaxAbsVol <= 0;                 // de noche no hay volumen: OI, y la leyenda lo dice
                    double maxK = porOi ? L.MaxAbsOi : L.MaxAbsVol;
                    if (maxK > 0)
                    {
                        foreach (var s in L.Perfil)
                        {
                            double v = porOi ? s.GexOi : s.GexVol;
                            if (v == 0) continue;
                            bool fijo = L.Doms.Any(d => d.Fut == s.Fut);
                            if (UmbralBarraPct > 0 && Math.Abs(v) < maxK * UmbralBarraPct / 100.0 && !fijo) continue;
                            int y; try { y = cont.GetYByPrice((decimal)s.Fut, false); } catch { continue; }
                            if (y < area.Top || y > piso) continue;
                            double fr = Math.Sqrt(Math.Abs(v) / maxK);
                            int w = Math.Max(1, (int)(fr * anchoCapa));
                            g.FillRectangle(Color.FromArgb((int)(80 + 120 * fr), col), new Rectangle(xk, y - alto / 2, w, alto));
                            if (v < 0) g.DrawLine(new RenderPen(Color.FromArgb(220, ColNeg), 1f), xk, y + alto / 2, xk + w, y + alto / 2);
                        }
                    }
                    g.DrawString(k.Nombre + (porOi ? " OI" : ""), fRot, Color.FromArgb(220, col), xk, area.Top + 8 + altoRot + 2);
                }

                if (CapasDominantes)
                {
                    for (int d = 0; d < L.Doms.Count; d++)
                    {
                        double p = L.Doms[d].Fut;
                        raya(p, col, d == 0 ? 1.4f : 1.0f, System.Drawing.Drawing2D.DashStyle.Dash, d == 0 ? 190 : 140);
                        int y; try { y = cont.GetYByPrice((decimal)p, false); } catch { continue; }
                        if (y < area.Top || y + altoRot > piso) continue;
                        string t = "D" + (d + 1) + " " + k.Nombre + " " + p.ToString("N0", es);
                        var m = g.MeasureString(t, fRot);
                        int xt = xl1 - m.Width - 2 - i * (m.Width + 8);   // cada capa en su propia columna, de derecha a izquierda
                        if (xt < xl0) xt = xl0;
                        g.FillRectangle(Color.FromArgb(150, ColFondo), new Rectangle(xt - 1, y + 1, m.Width + 2, altoRot));
                        g.DrawString(t, fRot, Color.FromArgb(230, col), xt, y + 1);
                    }
                    if (CapasZero) raya(L.ZeroVol, col, 1f, System.Drawing.Drawing2D.DashStyle.Dot, 120);
                }
            }
        }
    }
}
