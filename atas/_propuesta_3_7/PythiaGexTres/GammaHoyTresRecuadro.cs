using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Linq;
using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGexTres
{
    /// <summary>
    /// RECUADRO DEL LIBRO (3.5.7, 08-10-2026). Pedido del operador sobre la maqueta 10: "la 1 pero sacale el precio y agregale este recuadro ... con
    /// los datos cruzados con el libro profundo: muro de calls y puts, los majors, la gamma o gex, lo que miran los expertos de un libro cuando se
    /// acerca el precio" y "separalo por volumen y por OI: ven cual baja o sube en tiempo real".
    /// Arriba: los N niveles dibujados mas cercanos por encima y por debajo del precio, cada uno con el GEX del libro de NQ en su strike mas
    /// cercano, por volumen de hoy y por interes abierto (de ayer), con una flecha de cambio en los ultimos M minutos.
    /// Abajo: muro de calls y de puts (el mayor GEX de UN lado a +-radio del precio, como k_muro_calls/k_muro_puts de motor.py), majors y neto,
    /// cada uno por volumen y por OI. Describe el libro; no dice para donde va el precio.
    /// </summary>
    public enum EstiloRotulos3
    {
        [System.ComponentModel.Description("Minimal (maqueta 1, 08-10): texto chico del color del nivel al lado de la ultima vela, sin caja, sin raya y sin precio")] Minimal,
        [System.ComponentModel.Description("Columna (3.5.5): chips con fondo pegados al eje, con precio")] Columna,
    }

    public enum EsquinaRecuadro3
    {
        [System.ComponentModel.Description("Arriba a la izquierda")] ArribaIzquierda,
        [System.ComponentModel.Description("Arriba a la derecha")] ArribaDerecha,
        [System.ComponentModel.Description("Abajo a la izquierda")] AbajoIzquierda,
        [System.ComponentModel.Description("Abajo a la derecha")] AbajoDerecha,
    }

    public partial class GammaHoyTres
    {
        [Display(Name = "Rotulos: estilo", GroupName = "7. Rayas una por una", Order = 49,
                 Description = "3.7.0 (D5): Columna por defecto (chips con fondo pegados al eje, con precio), como lo dejo el operador en el .ws. Minimal (maqueta 1): el nombre del nivel en su color, al lado de la ultima vela, sin caja y sin precio.")]
        public EstiloRotulos3 Rotulos3Estilo { get; set; } = EstiloRotulos3.Columna;

        [Display(Name = "Ver el recuadro del libro (plegable)", GroupName = "8. Recuadro del libro", Order = 10,
                 Description = "3.6.2. Plegado por defecto: una pestañita 'LIBRO NQ ▸'; un clic lo abre y un clic en su titulo lo cierra (pedido 08-10: 'me molestaba para la lectura, algo mas chico o desplegable'). Adentro: niveles cercanos con GEX por volumen y por OI, muros, majors y neto.")]
        public bool RecuadroLibro3Ver { get; set; } = true;

        private bool _recuadroAbierto;
        private Rectangle _rectRecuadroCab = Rectangle.Empty;

        /// <summary>3.6.2: un clic en la pestañita (o en el titulo del recuadro abierto) lo abre o lo cierra. true = el clic no sigue al grafico.</summary>
        public override bool ProcessMouseClick(OFT.Rendering.Control.RenderControlMouseEventArgs e)
        {
            if (!RecuadroLibro3Ver || _rectRecuadroCab == Rectangle.Empty || !_rectRecuadroCab.Contains(e.X, e.Y)) return base.ProcessMouseClick(e);
            _recuadroAbierto = !_recuadroAbierto;
            try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            return true;
        }

        [Display(Name = "Esquina", GroupName = "8. Recuadro del libro", Order = 11)]
        public EsquinaRecuadro3 Recuadro3Esquina { get; set; } = EsquinaRecuadro3.ArribaIzquierda;

        [Display(Name = "Separacion del borde de arriba (px)", GroupName = "8. Recuadro del libro", Order = 12, Description = "Para no tapar el renglon de precios de ATAS (O: H: L: C:).")]
        [Range(0, 400)]
        public int Recuadro3MargenPx { get; set; } = 56;

        [Display(Name = "Niveles por lado", GroupName = "8. Recuadro del libro", Order = 13)]
        [Range(1, 6)]
        public int Recuadro3Niveles { get; set; } = 3;

        [Display(Name = "Flechas: cambio en (min)", GroupName = "8. Recuadro del libro", Order = 14,
                 Description = "↑ = el GEX se hizo mas positivo en estos minutos, ↓ = mas negativo, → = quieto (menos del 2 % del mayor del libro). Por OI solo se mueve por precio y volatilidad: el OI es de ayer.")]
        [Range(1, 30)]
        public int Recuadro3CambioMin { get; set; } = 5;

        [Display(Name = "Muros: radio (pts)", GroupName = "8. Recuadro del libro", Order = 15, Description = "El muro de calls (puts) es el strike con mas GEX de calls (puts) a esta distancia del precio, como en la pagina Profundidad.")]
        [Range(20, 400)]
        public int Recuadro3RadioMuros { get; set; } = 100;

        [Display(Name = "Tamaño (%)", GroupName = "8. Recuadro del libro", Order = 16)]
        [Range(50, 130)]
        public int Recuadro3TamPct { get; set; } = 85;

        // ---- fotos por minuto para las flechas: por strike (clave estable) {vol, oi, volC, volP, oiC, oiP}, neto y donde estaban los muros
        private sealed class FotoRec
        {
            public long Minuto;
            public Dictionary<double, double[]> Por = new Dictionary<double, double[]>();
            public double NetV = double.NaN, NetO = double.NaN;
            public double MuroCV = double.NaN, MuroPV = double.NaN, MuroCO = double.NaN, MuroPO = double.NaN;
        }
        private readonly List<FotoRec> _fotosRec = new List<FotoRec>();
        private readonly object _candadoRec = new object();
        private GammaHoyNucleo.Lectura _ultLRec;

        /// <summary>3.5.7: el rotulo Minimal: solo el nombre (sin flecha ni precio) y la edad si pasa de 30 min (·7h) o los contratos del APOYO.</summary>
        private string RotuloMinimal(string nombre, string sufijo)
        {
            string t = RotuloCorto(nombre, "", sufijo);
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            return t.Trim();
        }

        private static GammaHoyNucleo.Strike StrikeCerca(List<GammaHoyNucleo.Strike> perfil, double p, double max)
        {
            GammaHoyNucleo.Strike m = null; double d = max;
            foreach (var s in perfil) { double x = Math.Abs(s.Fut - p); if (x <= d) { d = x; m = s; } }
            return m;
        }

        private static (GammaHoyNucleo.Strike C, GammaHoyNucleo.Strike P) Muros(List<GammaHoyNucleo.Strike> perfil, double fut, double radio, bool porVol)
            => Seleccion37.Muros(perfil, fut, radio, porVol);   // 3.7.0: la misma cuenta que F5/F6 (Seleccion37, sin ATAS)

        /// <summary>+129M / −1,2B / +5K / 0 (signo matematico; coma decimal).</summary>
        private static string Gx(double v, CultureInfo es)
        {
            if (double.IsNaN(v)) return "—";
            double a = Math.Abs(v); string sg = v > 0 ? "+" : v < 0 ? "−" : "";
            if (a >= 1e9) return sg + (a / 1e9).ToString(a >= 1e10 ? "0" : "0.0", es) + "B";
            if (a >= 1e6) return sg + (a / 1e6).ToString(a >= 1e8 ? "0" : "0.0", es) + "M";
            if (a >= 1e3) return sg + (a / 1e3).ToString("0", es) + "K";
            return "0";
        }

        private static string Flecha(double ahora, double antes, double umbral)
        {
            if (double.IsNaN(ahora) || double.IsNaN(antes)) return "";
            if (ahora - antes >= umbral) return "↑";
            if (antes - ahora >= umbral) return "↓";
            return "→";
        }

        private void PintarRecuadro(RenderContext g, RenderFont f0, CultureInfo es, Rectangle area, int xr, int piso, string raiz,
                                    GammaHoyNucleo.Lectura L, double fut, double tick, bool majors, ExtraRotulos extra, DateTime cuenta)
        {
            if (L == null || L.Perfil == null || L.Perfil.Count == 0 || fut <= 0) return;
            long minuto = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
            int nMin = Math.Max(1, Math.Min(30, Recuadro3CambioMin));
            double radioM = Math.Max(20, Recuadro3RadioMuros);
            var mV = Muros(L.Perfil, fut, radioM, true);
            bool oiOk = _oiNqFresco;   // 3.7.0 C1: con el OI de anteayer no hay muros ni majors por OI de NQ
            (GammaHoyNucleo.Strike C, GammaHoyNucleo.Strike P) mO = oiOk ? Muros(L.Perfil, fut, radioM, false) : (null, null);
            FotoRec viejo;
            lock (_candadoRec)
            {
                var ult = _fotosRec.Count > 0 ? _fotosRec[_fotosRec.Count - 1] : null;
                bool nueva = ult == null || ult.Minuto != minuto;
                if (nueva) { ult = new FotoRec { Minuto = minuto }; _fotosRec.Add(ult); while (_fotosRec.Count > 45) _fotosRec.RemoveAt(0); }
                if (nueva || !ReferenceEquals(L, _ultLRec))
                {
                    _ultLRec = L;
                    ult.Por.Clear();
                    foreach (var s in L.Perfil) ult.Por[s.Clave] = new[] { s.GexVol, s.GexOi, s.GexVolC, s.GexVolP, s.GexOiC, s.GexOiP };
                    ult.NetV = L.NetVol; ult.NetO = L.NetOi;
                    ult.MuroCV = mV.C?.Fut ?? double.NaN; ult.MuroPV = mV.P?.Fut ?? double.NaN;
                    ult.MuroCO = mO.C?.Fut ?? double.NaN; ult.MuroPO = mO.P?.Fut ?? double.NaN;
                }
                viejo = _fotosRec.LastOrDefault(x => x.Minuto <= minuto - nMin);
            }
            double Antes(GammaHoyNucleo.Strike s, int i) => viejo != null && s != null && viejo.Por.TryGetValue(s.Clave, out var a) ? a[i] : double.NaN;
            double uV = Math.Max(0.02 * L.MaxAbsVol, 1e3), uO = Math.Max(0.02 * L.MaxAbsOi, 1e3);

            if (!_recuadroAbierto)
            {
                var fp = new RenderFont("Consolas", Math.Max(5f, f0.Size * Math.Max(50, Math.Min(130, Recuadro3TamPct)) / 100f));
                string tp = "LIBRO " + raiz + " ▸" + (string.IsNullOrEmpty(_alarmaFamilia) ? "" : " · " + _alarmaFamilia);   // 3.7.1 (A1): la alarma sostenida va aca (no en los rotulos)
                var mp = g.MeasureString(tp, fp);
                bool der = Recuadro3Esquina == EsquinaRecuadro3.ArribaDerecha || Recuadro3Esquina == EsquinaRecuadro3.AbajoDerecha;
                bool aba = Recuadro3Esquina == EsquinaRecuadro3.AbajoIzquierda || Recuadro3Esquina == EsquinaRecuadro3.AbajoDerecha;
                int xp = der ? xr - mp.Width - 20 : area.Left + 8, yp = aba ? piso - mp.Height - 14 : area.Top + Recuadro3MargenPx + 22;   // 3.6.3: debajo del titulo de la cabecera
                _rectRecuadroCab = new Rectangle(xp, yp, mp.Width + 12, mp.Height + 6);
                g.FillRectangle(Color.FromArgb(215, ColFondo), _rectRecuadroCab);
                g.DrawString(tp, fp, Color.FromArgb(230, 232, 197, 71), xp + 6, yp + 3);
                return;
            }
            var f = new RenderFont("Consolas", Math.Max(5f, f0.Size * Math.Max(50, Math.Min(130, Recuadro3TamPct)) / 100f));
            var fN = new RenderFont("Consolas", f.Size, FontStyle.Bold);
            double cw = g.MeasureString(new string('0', 20), f).Width / 20.0;
            int hf = g.MeasureString("Xg", f).Height + 1;
            int X(double col) => (int)Math.Round(col * cw);
            Color cTxt = Color.FromArgb(235, ColTexto), cGris = Color.FromArgb(150, 160, 172), cOro = Color.FromArgb(232, 197, 71);
            Color CSigno(double v) => double.IsNaN(v) || v == 0 ? cGris : v > 0 ? ColPos : ColNeg;
            Color CFlecha(string fl) => fl == "↑" ? ColPos : fl == "↓" ? ColNeg : cGris;

            // ---- filas (se arman primero para medir el alto)
            var cand = Candidatos(raiz, L, fut, majors, extra).Where(q => !(q.Nombre.Length > 1 && q.Nombre[0] == 'F' && char.IsDigit(q.Nombre[1]))).ToList();   // 3.6.1: las formulas a juzgar no entran
            int nv = Math.Max(1, Math.Min(6, Recuadro3Niveles));
            var arriba = cand.Where(q => q.P > fut).OrderBy(q => q.P - fut).Take(nv).OrderByDescending(q => q.P).ToList();
            var abajo = cand.Where(q => q.P <= fut).OrderBy(q => fut - q.P).Take(nv).ToList();
            bool hayAlarma = !string.IsNullOrEmpty(_alarmaFamilia);   // 3.7.1 (A1)
            int filas = 2 + (hayAlarma ? 1 : 0) + arriba.Count + 1 + abajo.Count + 1 + 1 + 6 + 2;
            int w = X(58) + 14, h = filas * hf + 14;
            int x0, y0;
            switch (Recuadro3Esquina)
            {
                case EsquinaRecuadro3.ArribaDerecha: x0 = xr - w - 8; y0 = area.Top + Recuadro3MargenPx; break;
                case EsquinaRecuadro3.AbajoIzquierda: x0 = area.Left + 8; y0 = piso - h - 8; break;
                case EsquinaRecuadro3.AbajoDerecha: x0 = xr - w - 8; y0 = piso - h - 8; break;
                default: x0 = area.Left + 8; y0 = area.Top + Recuadro3MargenPx; break;
            }
            x0 = Math.Max(area.Left + 2, x0); y0 = Math.Max(area.Top + 2, Math.Min(y0, piso - h - 2));
            var borde = new RenderPen(Color.FromArgb(150, 70, 82, 98), 1f);
            g.FillRectangle(Color.FromArgb(228, ColFondo), new Rectangle(x0, y0, w, h));
            g.DrawLine(borde, x0, y0, x0 + w, y0); g.DrawLine(borde, x0, y0 + h, x0 + w, y0 + h);
            g.DrawLine(borde, x0, y0, x0, y0 + h); g.DrawLine(borde, x0 + w, y0, x0 + w, y0 + h);
            int xi = x0 + 7, y = y0 + 6;
            _rectRecuadroCab = new Rectangle(x0, y0, w, hf + 8);   // 3.6.2: el titulo cierra el recuadro

            void Izq(string t, double col, Color c, RenderFont ff = null) { if (!string.IsNullOrEmpty(t)) g.DrawString(t, ff ?? f, c, xi + X(col), y); }
            void Der(string t, double colDer, Color c) { if (string.IsNullOrEmpty(t)) return; int wt = g.MeasureString(t, f).Width; g.DrawString(t, f, c, xi + X(colDer) - wt, y); }
            void Raya() { g.DrawLine(new RenderPen(Color.FromArgb(90, 120, 132, 148), 1f), x0 + 4, y + hf / 2, x0 + w - 4, y + hf / 2); y += hf; }

            // ---- cabecera: edad del dato ANTES que nada si pasa de 30 min (protocolo)
            double edadS = cuenta == DateTime.MinValue ? double.NaN : (DateTime.UtcNow - cuenta).TotalSeconds;
            string edad = double.IsNaN(edadS) ? "sin hora" : edadS < 90 ? ((int)edadS) + " s" : edadS < 5400 ? ((int)(edadS / 60)) + " min" : (edadS / 3600).ToString("0.0", es) + " h";
            if (!double.IsNaN(edadS) && edadS > 1800) { Izq("DATO VIEJO · cuenta hace " + edad, 0, ColNeg, fN); }
            else Izq("LIBRO " + raiz + " ▾ · cuenta hace " + edad + " · " + OiNqTexto + " · x" + L.Multiplicador.ToString("0", es) + " USD/pt", 0, oiOk ? cTxt : ColNaranja, fN);   // 3.7.0 C1/C4
            y += hf;
            if (hayAlarma) { Izq("ALARMA familia NDX/QQQ: " + _alarmaFamilia + " (base NDX - base de QQQ, > 2 pts 10 min seguidos)", 0, ColNaranja, fN); y += hf; }   // 3.7.1 (A1)
            Izq("nivel", 0, cGris); Der("precio", 24, cGris); Der("dist", 30, cGris); Der("strike", 38, cGris);
            Der("GEX vol", 47, cGris); Der("GEX OI", 57, cGris);
            y += hf;

            void FilaNivel(Rot37 q, bool sube)
            {
                var s = StrikeCerca(L.Perfil, q.P, 7.5);
                double gv = s?.GexVol ?? double.NaN, go = oiOk ? (s?.GexOi ?? double.NaN) : double.NaN;
                string fv = Flecha(gv, Antes(s, 0), uV), fo = Flecha(go, Antes(s, 1), uO);
                Izq((sube ? "▲ " : "▼ ") + RotuloMinimal(q.Nombre, q.Sufijo), 0, q.C);
                Der(Precio(q.P, tick, es), 24, cTxt);
                Der((q.P - fut).ToString("+0;-0;0", es), 30, cGris);
                Der(s != null ? Precio(s.Fut, tick, es) : "—", 38, cGris);
                Der(Gx(gv, es), 46, CSigno(gv)); Izq(fv, 46.2, CFlecha(fv));
                Der(Gx(go, es), 56, CSigno(go)); Izq(fo, 56.2, CFlecha(fo));
                y += hf;
            }
            foreach (var q in arriba) FilaNivel(q, true);
            Izq("— precio " + Precio(fut, tick, es) + " —", 4, cOro, fN); y += hf;
            foreach (var q in abajo) FilaNivel(q, false);

            // ---- muros, majors y neto: por volumen y por OI
            Raya();
            Izq("muros ±" + radioM.ToString("0", es) + " / majors", 0, cGris); Der("VOL (hoy)", 33, cGris); Der(oiOk ? "OI (ayer)" : "OI ANTEAYER", 54, oiOk ? cGris : ColNaranja);
            y += hf;
            void FilaPar(string nombre, Color cn, GammaHoyNucleo.Strike sv, double gv, int iv, double movV,
                                                   GammaHoyNucleo.Strike so, double go, int io, double movO)
            {
                Izq(nombre, 0, cn);
                if (sv != null)
                {
                    string fl = Flecha(gv, Antes(sv, iv), uV);
                    Der(Precio(sv.Fut, tick, es), 22, cTxt); Der(Gx(gv, es), 30, CSigno(gv)); Izq(fl, 30.2, CFlecha(fl));
                    if (!double.IsNaN(movV) && Math.Abs(movV - sv.Fut) > 0.01) Izq("*", 31.4, cOro);
                }
                else Der("—", 30, cGris);
                if (so != null)
                {
                    string fl = Flecha(go, Antes(so, io), uO);
                    Der(Precio(so.Fut, tick, es), 43, cTxt); Der(Gx(go, es), 51, CSigno(go)); Izq(fl, 51.2, CFlecha(fl));
                    if (!double.IsNaN(movO) && Math.Abs(movO - so.Fut) > 0.01) Izq("*", 52.4, cOro);
                }
                else Der("—", 51, cGris);
                y += hf;
            }
            FilaPar("muro calls", ColPos, mV.C, mV.C?.GexVolC ?? double.NaN, 2, viejo?.MuroCV ?? double.NaN, mO.C, mO.C?.GexOiC ?? double.NaN, 4, viejo?.MuroCO ?? double.NaN);
            FilaPar("muro puts", ColNeg, mV.P, mV.P?.GexVolP ?? double.NaN, 3, viejo?.MuroPV ?? double.NaN, mO.P, mO.P?.GexOiP ?? double.NaN, 5, viejo?.MuroPO ?? double.NaN);
            var smpV = !double.IsNaN(L.MpVol) ? StrikeCerca(L.Perfil, L.MpVol, 2.5) : null; var smpO = oiOk && !double.IsNaN(L.MpOi) ? StrikeCerca(L.Perfil, L.MpOi, 2.5) : null;
            var smnV = !double.IsNaN(L.MnVol) ? StrikeCerca(L.Perfil, L.MnVol, 2.5) : null; var smnO = oiOk && !double.IsNaN(L.MnOi) ? StrikeCerca(L.Perfil, L.MnOi, 2.5) : null;
            if (Raya3NqMasOi && smpO != null) Izq("●", 56.4, cOro);   // 3.6.1: dibujada como dominante
            FilaPar("major +", ColPos, smpV, smpV?.GexVol ?? double.NaN, 0, double.NaN, smpO, smpO?.GexOi ?? double.NaN, 1, double.NaN);
            if (Raya3NqMenosOi && smnO != null) Izq("●", 56.4, cOro);
            FilaPar("major −", ColNeg, smnV, smnV?.GexVol ?? double.NaN, 0, double.NaN, smnO, smnO?.GexOi ?? double.NaN, 1, double.NaN);
            {
                double netO = oiOk ? L.NetOi : double.NaN;
                string fv = Flecha(L.NetVol, viejo?.NetV ?? double.NaN, uV), fo = Flecha(netO, viejo?.NetO ?? double.NaN, uO);
                Izq("neto", 0, cTxt);
                Der(Gx(L.NetVol, es), 30, CSigno(L.NetVol)); Izq(fv, 30.2, CFlecha(fv));
                Der(Gx(netO, es), 51, CSigno(netO)); Izq(fo, 51.2, CFlecha(fo));
                y += hf;
                Izq("régimen", 0, cGris);
                Der(L.NetVol > 0 ? "colchón" : L.NetVol < 0 ? "tobogán" : "—", 30, CSigno(L.NetVol));
                Der(netO > 0 ? "colchón" : netO < 0 ? "tobogán" : "—", 51, CSigno(netO));
                y += hf;
            }
            Izq(viejo == null ? "flechas: juntando " + nMin + " min de fotos" : "↑↓ = cambio en " + nMin + " min · * = el muro cambió de strike", 0, cGris);
            y += hf; Izq("● = dibujado en el gráfico como dominante (M± OI)", 0, cGris);
        }
    }
}
