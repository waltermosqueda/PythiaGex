using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Linq;
using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGexCinco
{
    /// <summary>
    /// MAJORS POR INTERES ABIERTO COMO DOMINANTES (3.6.1-3.6.2, 08-10-2026). Pedido del operador (01:35 ART) sobre 31.350, el strike con mas
    /// gamma positiva por OI (+688 M a las 01:23) donde el precio freno: "dibujado en tiempo real, marcado en el cuadro y marcado como dominante".
    /// 3.6.2 (01:55): "el NQ M+ OI debe estar SIEMPRE y su negativo tambien, y lo mismo para los otros derivados, asi juzgo si sirven".
    /// M+ OI = el strike con mayor GEX neto positivo por OI de cada libro (NQ vivo por Rithmic; NDX y QQQ de CBOE); M− OI = el mas negativo
    /// (GammaHoyNucleo: MpOi / MnOi, todo el perfil). El OI es de ayer: intradia solo se mueven por precio y volatilidad. Sin limite de distancia
    /// (se ven aunque esten lejos: rotulo con flecha en el borde). Estela por vela: NQ verde / roja; NDX y QQQ en el color de su capa con un filo
    /// verde (M+) o rojo (M−). La estela se guarda en estela-*.jsonl ("m":[M+,M−]) y vuelve al reiniciar.
    /// Que el precio haya frenado ahi una vez no prueba nada: se mide en laboratorio/tres (extremos_dibujado.py) como cualquier otra raya.
    /// 5.0 (27-10-2026, P4): en la 5.0 los libros son ES (primario, Rithmic) y las capas SPX y SPY (CBOE): Raya3Ndx*Oi -> Raya3Spx*Oi y
    /// Raya3Qqq*Oi -> Raya3Spy*Oi (DLL nueva, nada guardado); las Raya3Nq*Oi gobiernan el primario (rotulo "ES M+ OI"). SIN MEDIR EN ES.
    /// </summary>
    public partial class GammaCinco
    {
        [Display(Name = "↳ ES M+ OI", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 41, Description = "Necesita la llave '3.0 ES dom▸' prendida. 3.6.1. Raya de dominante en el strike del libro ES con mas GEX positivo por OI: estela por vela y rotulo 'ES M+ OI'. Tambien decide si entra (con su punto) en el '3.0 libro▸'.")]
        public bool Raya3NqMasOi { get; set; } = true;

        [Display(Name = "↳ ES M− OI", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 42, Description = "Necesita la llave '3.0 ES dom▸' prendida. 3.6.1. Idem del lado negativo (puts): el strike del libro ES con mas GEX negativo por OI. Tambien decide si entra (con su punto) en el '3.0 libro▸'.")]
        public bool Raya3NqMenosOi { get; set; } = true;

        [Display(Name = "↳ SPX M+ OI", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 48, Description = "Necesita la llave '3.0 ES dom▸' prendida. 3.6.2. Major positivo por OI del libro de SPX (CBOE, llevado a ES con la base de la capa), estela en el color de la capa con filo verde; la edad del dato va en el rotulo. Va con la llave '3.0 ES dom▸' y NO con '3.0 capas▸' (asi esta en el codigo); con 'Capa SPX' en No (grupo 9.3) no hay dato.")]
        public bool Raya3SpxMasOi { get; set; } = true;
        [Display(Name = "↳ SPX M− OI", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 49, Description = "Necesita la llave '3.0 ES dom▸' prendida. 3.6.2. Major negativo por OI del libro de SPX (CBOE), filo rojo. Va con la llave '3.0 ES dom▸' y NO con '3.0 capas▸' (asi esta en el codigo); con 'Capa SPX' en No (grupo 9.3) no hay dato.")]
        public bool Raya3SpxMenosOi { get; set; } = true;
        [Display(Name = "↳ SPY M+ OI", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 50, Description = "Necesita la llave '3.0 ES dom▸' prendida. 3.6.2. Major positivo por OI del libro de SPY (CBOE, llevado a ES por la razon ES/SPY), estela en el color de la capa con filo verde; la edad del dato va en el rotulo. Va con la llave '3.0 ES dom▸' y NO con '3.0 capas▸' (asi esta en el codigo); con 'Capa SPY' en No (grupo 9.3) no hay dato.")]
        public bool Raya3SpyMasOi { get; set; } = true;
        [Display(Name = "↳ SPY M− OI", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 51, Description = "Necesita la llave '3.0 ES dom▸' prendida. 3.6.2. Major negativo por OI del libro de SPY (CBOE, por razon), filo rojo. Va con la llave '3.0 ES dom▸' y NO con '3.0 capas▸' (asi esta en el codigo); con 'Capa SPY' en No (grupo 9.3) no hay dato.")]
        public bool Raya3SpyMenosOi { get; set; } = true;

        [Display(Name = "Majors por OI: hasta cuantos pts del precio", GroupName = "9.7 3.0 · Rayas una por una", Order = 1008,
                 Description = "3.6.2: 3000 = siempre (pedido 08-10; en ES tambien es 'siempre': el libro ES llega a +-2 % del precio y los de CBOE a +-5 %). Fuera de pantalla queda el rotulo con flecha en el borde.")]
        [Range(20, 5000)]
        public int MajorOi3RadioMaxPts { get; set; } = 3000;

        private readonly Dictionary<DateTime, double[]> _estelaMajOi = new Dictionary<DateTime, double[]>();   // libro primario (5.0: ES): {M+ OI, M− OI} por hora de vela
        private readonly Dictionary<string, Dictionary<DateTime, double[]>> _estelaMajOiCapas = new Dictionary<string, Dictionary<DateTime, double[]>>(StringComparer.OrdinalIgnoreCase);
        private const string MAJ_OI_PRIMARIO = "primario";   // 5.0: clave interna del libro primario (la 4.1 decia "NQ"); el rotulo lo arma Candidatos con la raiz

        private bool MajOiVisible(string libro, bool mas)
        {
            if (string.Equals(libro, "SPX", StringComparison.OrdinalIgnoreCase)) return mas ? Raya3SpxMasOi : Raya3SpxMenosOi;
            if (string.Equals(libro, "SPY", StringComparison.OrdinalIgnoreCase)) return mas ? Raya3SpyMasOi : Raya3SpyMenosOi;
            return mas ? Raya3NqMasOi : Raya3NqMenosOi;
        }

        private void RegistrarMajorOiVela(DateTime tVela, GammaHoyNucleo.Lectura L)
        {
            try
            {
                if (L == null) return;
                lock (_candado)
                {
                    _estelaMajOi[tVela] = new[] { L.MpOi, L.MnOi };
                    if (_estelaMajOi.Count > 6000) foreach (var k in _estelaMajOi.Keys.OrderBy(x => x).Take(_estelaMajOi.Count - 5000).ToList()) _estelaMajOi.Remove(k);
                }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "RegistrarMajorOiVela", e); }
        }

        /// <summary>Llamar con _candado tomado (desde RegistrarCapasVela / SembrarMemoriaCapas).</summary>
        private void AnotarMajorOiCapa(string capa, DateTime tVela, double mp, double mn, bool pisar)
        {
            if ((double.IsNaN(mp) || mp <= 0) && (double.IsNaN(mn) || mn <= 0)) return;
            if (!_estelaMajOiCapas.TryGetValue(capa, out var d)) { d = new Dictionary<DateTime, double[]>(); _estelaMajOiCapas[capa] = d; }
            if (!pisar && d.ContainsKey(tVela)) return;
            d[tVela] = new[] { mp, mn };
            if (d.Count > 6000) foreach (var k in d.Keys.OrderBy(x => x).Take(d.Count - 5000).ToList()) d.Remove(k);
        }

        private void SembrarMajorOiArchivo(List<PuntoMem> pts0, List<VelaMem> velas)
        {
            try
            {
                var pts = pts0.Where(p => p.MpOi > 0 || p.MnOi > 0).OrderBy(p => p.T).ToList();
                if (pts.Count == 0 || velas == null) return;
                var ts = pts.Select(p => p.T).ToList(); var dM = new Dictionary<DateTime, double[]>();
                foreach (var v in velas)
                {
                    int i = ts.BinarySearch(v.TClose); if (i < 0) i = ~i; i--;
                    if (i < 0 || (v.TClose - ts[i]).TotalSeconds > VIDA_MEMORIA_S) continue;
                    dM[v.TOpen] = new[] { pts[i].MpOi, pts[i].MnOi };
                }
                SembrarMajorOi(dM);
                Log("memoria: majors OI del libro " + Raiz() + " desde la estela guardada en " + dM.Count + " velas");
            }
            catch (Exception e) { Registro.Excepcion(LOG, "SembrarMajorOiArchivo", e); }
        }

        private void SembrarMajorOi(Dictionary<DateTime, double[]> d) { lock (_candado) foreach (var kv in d) if (!_estelaMajOi.ContainsKey(kv.Key)) _estelaMajOi[kv.Key] = kv.Value; }

        /// <summary>Estela por vela de M+ OI / M− OI del libro primario (5.0: ES) y de las capas SPX/SPY, y los rotulos de las capas (los del
        /// primario van por Candidatos).</summary>
        private void PintarMajorOi(RenderContext g, Func<int, int> XBar, int desde, int hasta, List<(int B, DateTime T)> horas, double fut,
                                   GammaHoyNucleo.Lectura L, Func<double, int> Y, Func<int, bool> EnPantalla, ExtraRotulos extra)
        {
            try
            {
                int ultima = CurrentBar - 1;
                int inicio = Estela3VelasAtras > 0 ? Math.Max(0, ultima - Estela3VelasAtras) : _barInicioSesion;
                int bw = 5;
                { int xa = XBar(desde), xb = XBar(hasta); if (hasta > desde && xa != int.MinValue && xb != int.MinValue) bw = Math.Max(3, (xb - xa) / Math.Max(1, hasta - desde)); }
                int ancho = Math.Max(3, bw - 1), alto = Math.Max(2, Math.Min(4, bw / 3 + 1));
                double radio = Math.Max(20, MajorOi3RadioMaxPts);
                var ahora = DateTime.UtcNow;

                // (libro, color, estela por barra, valor de ahora, edad del dato)
                var libros = new List<(string Nombre, Color C, Dictionary<int, double[]> Est, double[] Ahora, string Edad, bool Capa)>();
                {
                    var est = new Dictionary<int, double[]>();
                    lock (_candado) foreach (var (b, t) in horas) if (_estelaMajOi.TryGetValue(t, out var e0)) est[b] = e0;
                    libros.Add((MAJ_OI_PRIMARIO, Color.Empty, est, L != null ? new[] { L.MpOi, L.MnOi } : null, "", false));
                }
                foreach (var k in _capas)
                {
                    if (!MajOiVisible(k.Nombre, true) && !MajOiVisible(k.Nombre, false)) continue;
                    var est = new Dictionary<int, double[]>(); GammaHoyNucleo.Lectura Lk; DateTime ult, gen;
                    lock (_candado)
                    {
                        Lk = k.L; ult = k.UltimoTradeUtc; gen = k.GeneradoUtc;
                        if (_estelaMajOiCapas.TryGetValue(k.Nombre, out var d)) foreach (var (b, t) in horas) if (d.TryGetValue(t, out var e0)) est[b] = e0;
                    }
                    bool viva = Lk != null;   // 3.6.3: como el resto de la capa: se dibuja aunque la cadena sea vieja, con su edad en el rotulo (·9h)
                    string edad = ult == DateTime.MinValue ? "" : " ·" + ((int)(ahora - ult).TotalMinutes).ToString("0", CultureInfo.InvariantCulture) + "m";
                    libros.Add((k.Nombre, k.Color, est, viva ? new[] { Lk.MpOi, Lk.MnOi } : null, edad, true));
                }

                foreach (var lb in libros)
                {
                    if (lb.Ahora != null && ultima >= desde && ultima <= hasta) lb.Est[ultima] = lb.Ahora;   // la vela viva con el valor de ahora
                    foreach (var kv in lb.Est)
                    {
                        int b = kv.Key; if (b < desde || b > hasta) continue;
                        bool previa = b < inicio; if (previa && !Estela3SesionAnterior) continue;
                        int x = XBar(b); if (x == int.MinValue) continue;
                        for (int i = 0; i < 2; i++)
                        {
                            if (!MajOiVisible(lb.Nombre, i == 0)) continue;
                            double p = kv.Value.Length > i ? kv.Value[i] : double.NaN;
                            if (double.IsNaN(p) || p <= 0 || Math.Abs(p - fut) > radio) continue;
                            int y = Y(p); if (!EnPantalla(y)) continue;
                            int alfa = b >= ultima - 30 ? 235 : 160; if (previa) alfa = alfa * 45 / 100;
                            alfa = AlfaAtravesada(alfa, p, b);   // 3.6.4
                            Color signo = i == 0 ? ColPos : ColNeg;
                            if (!lb.Capa) g.FillRectangle(Color.FromArgb(alfa, signo), new Rectangle(x - ancho / 2, y - alto / 2, ancho, alto));
                            else
                            {
                                g.FillRectangle(Color.FromArgb(alfa, lb.C), new Rectangle(x - ancho / 2, y - alto / 2, ancho, alto));
                                g.FillRectangle(Color.FromArgb(alfa, signo), new Rectangle(x - ancho / 2, i == 0 ? y - alto / 2 - 1 : y + alto - alto / 2, ancho, 1));   // filo: verde arriba (M+), rojo abajo (M−)
                            }
                        }
                    }
                    if (lb.Capa && lb.Ahora != null && extra != null)
                        for (int i = 0; i < 2; i++)
                        {
                            if (!MajOiVisible(lb.Nombre, i == 0)) continue;
                            double p = lb.Ahora[i];
                            if (double.IsNaN(p) || p <= 0 || Math.Abs(p - fut) > radio) continue;
                            extra.Rotulos.Add((lb.Nombre + (i == 0 ? " M+ OI" : " M− OI"), p, lb.C, lb.Edad));
                        }
                }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "PintarMajorOi", e); }
        }
    }
}
