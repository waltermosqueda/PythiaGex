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
    /// APOYO (3.2.3, 07-10-2026): el libro PROFUNDO de las opciones del futuro. Por strike del 0DTE, los contratos apoyados en la punta
    /// (bid + ask del call y del put, tamaños de las puntas que trae el Summary de Rithmic). Los N strikes con mas contratos apoyados se
    /// dibujan como rombos por vela (como la "PROFUNDIDAD" de la 2.0, GammaHoyCapas.ApoyoPorStrikeLados) y van a la caja negra como
    /// fam APOYO para medirlos contra placebo. NO es una dominante por gamma: es donde los creadores de mercado tienen mas ordenes paradas.
    /// Tambien la estela del zero gamma por INTERES ABIERTO (la 2.0 la dibuja y el operador la ve como "zona").
    /// Lo medido hasta hoy sobre estos niveles: nada (ni el laboratorio ni la caja): se dibujan a pedido del operador y se miden.
    /// </summary>
    public partial class GammaHoyTres
    {
        [Display(Name = "Apoyo: strikes con mas contratos apoyados (rombos)", GroupName = "3. Pantalla", Order = 32,
                 Description = "3.2.3. Los N strikes del 0DTE con mas contratos en las puntas (bid + ask de call y put, tamaños del Summary de Rithmic), como rombos verdes por vela y rotulo con los contratos. Es el libro profundo de las opciones, no una dominante por gamma. Sin medir: la caja negra lo graba como APOYO.")]
        public bool Ver3Apoyo { get; set; } = false;   // 3.6.2: ruido (pedido 08-10)

        [Display(Name = "Apoyo: cuantos strikes", GroupName = "3. Pantalla", Order = 33)]
        [Range(1, 6)]
        public int Apoyo3Cuantos { get; set; } = 3;

        [Display(Name = "Zero gamma por OI: estela (puntos)", GroupName = "3. Pantalla", Order = 34,
                 Description = "3.2.3. Ademas del zero por volumen (gris), el cruce por cero de la gamma por INTERES ABIERTO como puntos finos gris oscuro por vela. La 2.0 lo dibuja como rombos; aca es apagable y se mide.")]
        public bool Ver3EstelaZeroOi { get; set; } = false;   // 3.6.2: ruido (pedido 08-10)

        [Display(Name = "Zero gamma: todos los cruces cercanos (rombos chicos)", GroupName = "3. Pantalla", Order = 35,
                 Description = "3.3.1 (pedido 07-10: 'que toque aun mas las mechas cercanas'). Ademas del cruce mas cercano al precio (la fila de la 2.0), TODOS los cruces de signo de la gamma por volumen y por OI a +-0,1 % del precio, como rombos chicos por vela. Mas filas = mas toques; lo medido dice que el % de rebote por toque no sube (criterio_operador.py).")]
        public bool Zero3TodosLosCruces { get; set; } = false;   // 3.6.2: ruido (pedido 08-10)

        [Display(Name = "Zero gamma: radio de los cruces (pts)", GroupName = "3. Pantalla", Order = 36)]
        [Range(5, 40)]
        public int Zero3CrucesRadioPts { get; set; } = 20;

        private readonly Dictionary<DateTime, double[]> _estelaCruces = new Dictionary<DateTime, double[]>();
        private readonly Dictionary<DateTime, double[]> _estelaCrucesOi = new Dictionary<DateTime, double[]>();   // 3.5.1: solo los de interes abierto
        /// <summary>3.5.2: desde el rebobinado (memoria del arranque): siembra la estela de cruces por OI sin pisar lo que ya hay.</summary>
        private void SembrarCrucesOi(Dictionary<DateTime, double[]> d) { lock (_candado) foreach (var kv in d) if (!_estelaCrucesOi.ContainsKey(kv.Key)) _estelaCrucesOi[kv.Key] = kv.Value; }
        private static readonly Color ColCruceOi = Color.FromArgb(38, 198, 190);    // turquesa: cruces por INTERES ABIERTO (y el zero por OI)
        private static readonly Color ColCruceVol = Color.FromArgb(176, 186, 200);  // gris claro: cruces por VOLUMEN
        private List<double> _crucesOiAhora = new List<double>();

        [Display(Name = "Cruces por interés abierto: radio (pts)", GroupName = "3. Pantalla", Order = 37,
                 Description = "3.5.2. Hasta cuantos puntos del precio se dibujan los cruces de signo por interes abierto (turquesa, con rotulo 'cruce OI', 3.7.0 C8; solo con el OI de ayer, C1). Son los que casi no se mueven en la sesion (el OI es de ayer).")]
        [Range(10, 80)]
        public int CrucesOiRadio3Pts { get; set; } = 60;   // 3.5.3: nombre nuevo (el .ws no lo pisa) y 60 pts

        private static readonly Color ColApoyo = Color.FromArgb(76, 175, 80);      // verde (como los rombos de la 2.0)
        private static readonly Color ColZeroOi = Color.FromArgb(130, 130, 130);
        private List<(double Fut, double Ctos)> _apoyo = new List<(double, double)>();
        private readonly Dictionary<DateTime, double[]> _estelaApoyo = new Dictionary<DateTime, double[]>();
        private readonly Dictionary<DateTime, double> _estelaZeroOi = new Dictionary<DateTime, double>();

        /// <summary>Desde el Tick, con la cadena y la cuenta frescas: los N strikes del vencimiento mas cercano con mas contratos apoyados, a <= radio.</summary>
        private void ActualizarApoyo(Feed.Cadena c, GammaHoyNucleo.Lectura L, double fut, double radio)
        {
            try
            {
                if (c == null || c.Filas == null || c.Dias == null || c.Dias.Length == 0 || fut <= 0) return;
                int v0 = 0; double dmin = double.MaxValue;
                for (int i = 0; i < c.Dias.Length; i++) if (c.Dias[i] >= 0 && c.Dias[i] < dmin) { dmin = c.Dias[i]; v0 = i; }
                var porK = new Dictionary<double, double>();
                foreach (var f in c.Filas)
                {
                    if (f.V != v0) continue;
                    double ctos = f.BidVolC + f.AskVolC + f.BidVolP + f.AskVolP;
                    if (ctos <= 0) continue;
                    double futK = c.PorRazon ? c.AlFuturo(f.K) : f.K + c.Base;
                    if (Math.Abs(futK - fut) > radio) continue;
                    porK[futK] = porK.TryGetValue(futK, out var x) ? x + ctos : ctos;
                }
                var top = porK.OrderByDescending(kv => kv.Value).Take(Math.Max(1, Math.Min(6, Apoyo3Cuantos))).OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)).ToList();
                lock (_candado) { _apoyo = top; }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "ActualizarApoyo", e); }
        }

        private List<(double Fut, double Ctos)> ApoyoActual() { lock (_candado) return _apoyo.ToList(); }

        /// <summary>Desde RegistrarVelaCerrada: la estela en memoria del apoyo y del zero por OI (no se persisten: arrancan vacias al reiniciar).</summary>
        private void RegistrarApoyoVela(DateTime tVela, GammaHoyNucleo.Lectura L)
        {
            try
            {
                lock (_candado)
                {
                    if (_apoyo.Count > 0) _estelaApoyo[tVela] = _apoyo.Select(a => a.Fut).ToArray();
                    bool oiOk = _oiNqFresco;   // 3.7.0 C1: con el OI de anteayer no hay zero ni cruces por OI de NQ
                    if (oiOk && L != null && !double.IsNaN(L.ZeroOi) && L.ZeroOi > 0) _estelaZeroOi[tVela] = L.ZeroOi;
                    if (L != null && L.ZeroCrucesVolLista != null && L.ZeroCrucesVolLista.Count > 0) _estelaCruces[tVela] = L.ZeroCrucesVolLista.ToArray();   // 3.5.1: solo VOLUMEN (antes todos juntos)
                    if (oiOk && L != null && L.ZeroCrucesOiLista != null && L.ZeroCrucesOiLista.Count > 0) _estelaCrucesOi[tVela] = L.ZeroCrucesOiLista.ToArray();
                    if (L != null) _crucesOiAhora = oiOk && L.ZeroCrucesOiLista != null ? L.ZeroCrucesOiLista.ToList() : new List<double>();
                    if (_estelaCrucesOi.Count > 6000) foreach (var k in _estelaCrucesOi.Keys.OrderBy(x => x).Take(_estelaCrucesOi.Count - 5000).ToList()) _estelaCrucesOi.Remove(k);
                    if (_estelaCruces.Count > 6000) foreach (var k in _estelaCruces.Keys.OrderBy(x => x).Take(_estelaCruces.Count - 5000).ToList()) _estelaCruces.Remove(k);
                    if (_estelaApoyo.Count > 6000) foreach (var k in _estelaApoyo.Keys.OrderBy(x => x).Take(_estelaApoyo.Count - 5000).ToList()) _estelaApoyo.Remove(k);
                    if (_estelaZeroOi.Count > 6000) foreach (var k in _estelaZeroOi.Keys.OrderBy(x => x).Take(_estelaZeroOi.Count - 5000).ToList()) _estelaZeroOi.Remove(k);
                }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "RegistrarApoyoVela", e); }
        }

        /// <summary>Desde Pintar, despues de las capas: rombos del apoyo por vela, puntos del zero por OI, linea del apoyo actual y sus rotulos (chicos).</summary>
        private void PintarApoyo(RenderContext g, Func<int, int> XBar, Rectangle area, int xr, int piso, int desde, int hasta,
                                 List<(int B, DateTime T)> horas, double fut, double radioDib, CultureInfo es, Func<double, int> Y, Func<int, bool> EnPantalla, ExtraRotulos extra, Func<int, double, bool> enRadio)
        {
            try
            {
                if (!Ver3Apoyo && !Ver3EstelaZeroOi) return;
                Dictionary<int, double[]> est = new Dictionary<int, double[]>(); Dictionary<int, double> zoi = new Dictionary<int, double>(); Dictionary<int, double[]> cru = new Dictionary<int, double[]>(); Dictionary<int, double[]> cruO = new Dictionary<int, double[]>();
                List<(double Fut, double Ctos)> ap;
                lock (_candado)
                {
                    ap = _apoyo.ToList();
                    foreach (var (b, t) in horas)
                    {
                        if (Ver3Apoyo && _estelaApoyo.TryGetValue(t, out var e0)) est[b] = e0;
                        if (Ver3EstelaZeroOi && _estelaZeroOi.TryGetValue(t, out var z0)) zoi[b] = z0;
                        if (Zero3TodosLosCruces && _estelaCruces.TryGetValue(t, out var c0)) cru[b] = c0;
                        if (Zero3TodosLosCruces && _estelaCrucesOi.TryGetValue(t, out var c1)) cruO[b] = c1;   // 3.5.1
                    }
                }
                int ultima = CurrentBar - 1;
                int inicio = Estela3VelasAtras > 0 ? Math.Max(0, ultima - Estela3VelasAtras) : _barInicioSesion;
                int bw = 5;
                { int xa = XBar(desde), xb = XBar(hasta); if (hasta > desde && xa != int.MinValue && xb != int.MinValue) bw = Math.Max(3, (xb - xa) / Math.Max(1, hasta - desde)); }
                int r = Math.Max(2, Math.Min(4, bw / 2));
                foreach (var kv in est)
                {
                    int b = kv.Key; if (b < desde || b > hasta) continue;
                    bool previa = b < inicio; if (previa && !Estela3SesionAnterior) continue;
                    int x = XBar(b); if (x == int.MinValue) continue;
                    foreach (var p in kv.Value)
                    {
                        if (double.IsNaN(p) || p <= 0 || !enRadio(b, p)) continue;   // 3.7.0 D1
                        int y = Y(p); if (!EnPantalla(y)) continue;
                        int alfa = b >= ultima - 30 ? 220 : 150;
                        if (previa) alfa = alfa * 45 / 100;
                        g.FillPolygon(Color.FromArgb(alfa, ColApoyo), new[] { new Point(x, y - r), new Point(x + r, y), new Point(x, y + r), new Point(x - r, y) });
                    }
                }
                foreach (var kv in zoi)
                {
                    int b = kv.Key; if (b < desde || b > hasta) continue;
                    bool previa = b < inicio; if (previa && !Estela3SesionAnterior) continue;
                    int x = XBar(b); if (x == int.MinValue) continue;
                    if (!enRadio(b, kv.Value)) continue;   // 3.7.0 D1
                    int y = Y(kv.Value); if (!EnPantalla(y)) continue;
                    int alfa = b >= ultima - 30 ? 200 : 130; if (previa) alfa = alfa * 45 / 100;
                    if (Zero3Estilo == ZeroEstilo3.RomboVerde)
                    {
                        int rz = Math.Max(2, Math.Min(4, bw / 2 + 1));   // 3.3.1: mismo rombo que el zero por volumen (la fila de la 2.0)
                        g.FillPolygon(Color.FromArgb(alfa, ColCruceOi), new[] { new Point(x, y - rz), new Point(x + rz, y), new Point(x, y + rz), new Point(x - rz, y) });   // 3.5.1: turquesa (OI)
                    }
                    else g.FillRectangle(Color.FromArgb(alfa, ColZeroOi), new Rectangle(x - 1, y, 2, 1));
                }
                foreach (var dic in new[] { cru, cruO })
                if (Cruces3SinCortes && dic.Count > 2)   // 3.5.0: un cruce que estaba y vuelve dentro de 3 velas se dibuja tambien en el hueco (3.5.1: VOL y OI)
                {
                    var cru0 = dic;
                    var bs = cru0.Keys.OrderBy(x => x).ToList(); var huecos = new Dictionary<int, List<double>>();
                    for (int a = 0; a < bs.Count; a++)
                        foreach (var p in cru0[bs[a]])
                        {
                            if (double.IsNaN(p) || p <= 0) continue;
                            for (int s = a + 1; s < bs.Count && bs[s] - bs[a] <= 4; s++)
                            {
                                if (!cru0[bs[s]].Any(q => Math.Abs(q - p) <= 1.0)) continue;
                                for (int bb = bs[a] + 1; bb < bs[s]; bb++)
                                {
                                    if (cru0.TryGetValue(bb, out var ya) && ya.Any(q => Math.Abs(q - p) <= 1.0)) continue;
                                    if (!huecos.TryGetValue(bb, out var le)) huecos[bb] = le = new List<double>();
                                    if (!le.Any(q => Math.Abs(q - p) <= 0.5)) le.Add(p);
                                }
                                break;
                            }
                        }
                    foreach (var kv in huecos) cru0[kv.Key] = cru0.TryGetValue(kv.Key, out var v0) ? v0.Concat(kv.Value).ToArray() : kv.Value.ToArray();
                }
                foreach (var kv in cru)   // 3.3.1: todos los cruces cercanos, rombos chicos
                {
                    int b = kv.Key; if (b < desde || b > hasta) continue;
                    bool previa = b < inicio; if (previa && !Estela3SesionAnterior) continue;
                    int x = XBar(b); if (x == int.MinValue) continue;
                    int rz = Math.Max(1, Math.Min(3, bw / 2));
                    foreach (var p in kv.Value)
                    {
                        if (double.IsNaN(p) || p <= 0 || Math.Abs(p - fut) > Zero3CrucesRadioPts || !enRadio(b, p)) continue;
                        int y = Y(p); if (!EnPantalla(y)) continue;
                        int alfa = b >= ultima - 30 ? 150 : 95; if (previa) alfa = alfa * 45 / 100;
                        g.FillPolygon(Color.FromArgb(alfa, ColCruceVol), new[] { new Point(x, y - rz), new Point(x + rz, y), new Point(x, y + rz), new Point(x - rz, y) });   // 3.5.1: VOL gris
                    }
                }
                foreach (var kv in cruO)   // 3.5.1: cruces por INTERES ABIERTO, turquesa, un poco mas grandes (son los que no se mueven: OI de ayer)
                {
                    int b = kv.Key; if (b < desde || b > hasta) continue;
                    bool previa = b < inicio; if (previa && !Estela3SesionAnterior) continue;
                    int x = XBar(b); if (x == int.MinValue) continue;
                    int rz = Math.Max(2, Math.Min(4, bw / 2 + 1));
                    foreach (var p in kv.Value)
                    {
                        if (double.IsNaN(p) || p <= 0 || Math.Abs(p - fut) > CrucesOiRadio3Pts || !enRadio(b, p)) continue;
                        int y = Y(p); if (!EnPantalla(y)) continue;
                        int alfa = b >= ultima - 30 ? 220 : 140; if (previa) alfa = alfa * 45 / 100;
                        g.FillPolygon(Color.FromArgb(alfa, ColCruceOi), new[] { new Point(x, y - rz), new Point(x + rz, y), new Point(x, y + rz), new Point(x - rz, y) });
                    }
                }
                if (Zero3TodosLosCruces)
                {
                    List<double> oiAhora; lock (_candado) oiAhora = _crucesOiAhora.ToList();
                    int xU = XBar(Math.Max(0, CurrentBar - 1));
                    foreach (var p in oiAhora)
                    {
                        if (double.IsNaN(p) || p <= 0 || Math.Abs(p - fut) > CrucesOiRadio3Pts) continue;
                        int y = Y(p);
                        if (Rayas3Largas && EnPantalla(y) && xU != int.MinValue && xr > xU) g.DrawLine(new RenderPen(Color.FromArgb(200, ColCruceOi), 1f), Math.Max(area.Left, xU), y, xr, y);
                        extra?.Rotulos.Add(new Rot37("cruce OI " + (p >= fut ? "▲" : "▼"), p, ColCruceOi, true, "", Raiz(), "cruce OI"));   // 3.7.0 C8
                    }
                }
                if (Ver3Apoyo && ap.Count > 0)
                {
                    int xUlt = XBar(Math.Max(0, CurrentBar - 1));
                    foreach (var a in ap)
                    {
                        if (Math.Abs(a.Fut - fut) > radioDib) continue;
                        int y = Y(a.Fut);
                        if (Rayas3Largas && EnPantalla(y) && xUlt != int.MinValue && xr > xUlt) g.DrawLine(new RenderPen(Color.FromArgb(150, ColApoyo), 1f), Math.Max(area.Left, xUlt), y, xr, y);
                        extra?.Rotulos.Add(new Rot37("APOYO " + (a.Fut >= fut ? "▲" : "▼"), a.Fut, ColApoyo, true, " ·" + a.Ctos.ToString("N0", es) + " ctos", Raiz(), "APOYO"));
                    }
                }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "PintarApoyo", e); }
        }
    }
}
