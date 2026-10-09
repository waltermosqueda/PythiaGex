using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGexTres
{
    /// <summary>
    /// LO NUEVO DE LA 3.7.0 DEL LADO DEL INDICADOR (08-10-2026; 3.7.2: radio 35, rayas juntas, regla de la vela en PintarMarcasAgrupadas; 3.7.3:
    /// radio 50, juntas 8, tope de rayas por vela propio, renglones de edad / fecha del OI arriba de la columna). La seleccion (que nivel) esta en Seleccion37.cs / Conversion37.cs, sin ATAS;
    /// aca solo se arma lo que necesitan (precio por segundo, OI con fecha, dominantes del vivo / del rebobinado / del pulso) y se dibuja
    /// (radio por vela, rotulos del borde).
    /// </summary>
    public partial class GammaHoyTres
    {
        // ------------------------------------------------------------------ D1: el radio de dibujo por vela

        [Display(Name = "Dominantes: radio de dibujo (pts)", GroupName = "3. Pantalla", Order = 89,
                 Description = "3.7.0 (D1). Cada marca de estela (NQ, capas, formulas, majors) se dibuja en su vela solo si esta a esta distancia o menos del CIERRE DE ESA VELA. En la vela viva, lo que queda mas lejos va como rotulo en el borde con flecha, libro, tipo y distancia (el mas cercano arriba y abajo de cada libro; los M± OI de NQ siempre), si sobra lugar en el tope de rotulos. 3.7.3: 50 pts (propiedad renombrada Radio37DibujoPtsC; la 3.7.2 tenia 35, elegido con una grilla sobre la misma muestra que juzgaba el criterio).")]
        [Range(5, 1000)]
        public decimal Radio37DibujoPtsC { get; set; } = 50m;   // 3.7.3: renombrada (3.7.2: Radio37DibujoPtsB = 35; 3.7.1: Radio37DibujoPts = 50)

        [Display(Name = "Rayas juntas: una sola si estan a menos de (pts)", GroupName = "3. Pantalla", Order = 90,
                 Description = "3.7.2. Dos rayas de la misma vela (de cualquier libro) a esta distancia o menos se dibujan como UNA: la de mayor jerarquia (NQ D1, NQ D2, NDX D1, QQQ D1, NQ M± OI, F5, NDX D2, QQQ D2, M± OI de NDX, M± OI de QQQ). La otra va nombrada en su rotulo con la distancia ('NQ 31.250 D2 · NDX D1 −2,6'). 0 = apagado. 3.7.3: 8 pts (propiedad renombrada Juntas373Pts; 3.7.2: 3): el revisor visual medio rayas de libros distintos a 3-8 pts en el 47 % de las velas, que se leen como una raya doble.")]
        [Range(0, 20)]
        public decimal Juntas373Pts { get; set; } = 8m;   // 3.7.3: renombrada (3.7.2: Juntas372Pts = 3)

        [Display(Name = "Tope de rayas por vela", GroupName = "3. Pantalla", Order = 91,
                 Description = "3.7.3. Cuantas rayas (grupos libro-strike, despues de juntar las que estan a <= 'Rayas juntas') se dibujan como maximo en cada vela, por jerarquia. En la 3.7.2 lo decidia 'Tope de rotulos' (Tope3Rotulos), que cambiaba de significado sin renombrarse; ahora cada uno es lo suyo. En la vela viva se dibuja solo lo que tiene rotulo (el menor de los dos topes).")]
        [Range(1, 7)]
        public int Tope373Rayas { get; set; } = Dibujo37.TOPE_RAYAS_DEFECTO;   // 3.7.3: nueva

        private double RadioDibujo37 => (double)Math.Max(5m, Math.Min(1000m, Radio37DibujoPtsC));
        private double Juntas37 => (double)Math.Max(0m, Math.Min(20m, Juntas373Pts));
        private int TopeVela37 => Math.Max(1, Math.Min(7, Tope373Rayas));        // 3.7.3: rayas por vela (estela y vela viva)
        private int TopeRotulos37 => Math.Max(1, Math.Min(7, Tope3Rotulos));     // 3.7.3: rotulos de la columna (adentro + borde)

        /// <summary>Un rotulo: lo que se escribe (Nombre/Sufijo/Extra), su precio, su color, si es de capa (letra chica), y para el borde
        /// el LIBRO y el TIPO. Siempre = va al borde aunque haya otro mas cercano (M± OI de NQ).</summary>
        private struct Rot37
        {
            public string Nombre; public double P; public Color C; public bool Chico; public string Sufijo; public string Extra;
            public string Libro, Tipo; public bool Siempre;
            public Rot37(string nombre, double p, Color c, bool chico, string sufijo, string libro, string tipo, string extra = "", bool siempre = false)
            { Nombre = nombre; P = p; C = c; Chico = chico; Sufijo = sufijo ?? ""; Libro = libro; Tipo = tipo; Extra = extra ?? ""; Siempre = siempre; }
            /// <summary>Desde el candidato neutro de Dibujo37 (sin ATAS), con el color que le pone el indicador.</summary>
            public Rot37(Dibujo37.Rotulo37 r, Color c) : this(r.Nombre, r.P, c, r.Chico, r.Sufijo, r.Libro, r.Tipo, r.Extra, r.Siempre) { }
            public Dibujo37.Rotulo37 Neutro() => new Dibujo37.Rotulo37(Nombre, P, "", Chico, Sufijo, Libro, Tipo, Extra, Siempre);
        }

        // ------------------------------------------------------------------ C5: el precio del MNQ en un SEGUNDO (anillo de 2 h de la cinta)

        private const int ANILLO_S = 7200;
        private readonly long[] _anilloSeg = new long[ANILLO_S];
        private readonly double[] _anilloPx = new double[ANILLO_S];
        private static readonly long EPOCA = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        /// <summary>Desde OnCumulativeTrade (cientos por segundo): el ultimo precio de cada segundo UTC. Sin LINQ, sin locks, nunca tira.</summary>
        private void PrecioSegundoAnotar(CumulativeTrade t)
        {
            try
            {
                if (t == null) return;
                var tt = t.Time.Kind == DateTimeKind.Utc ? t.Time : DateTime.SpecifyKind(t.Time, DateTimeKind.Utc);
                long s = (tt.Ticks - EPOCA) / TimeSpan.TicksPerSecond;
                if (s <= 0) return;
                int i = (int)(s % ANILLO_S);
                _anilloPx[i] = (double)t.Lastprice;
                _anilloSeg[i] = s;
            }
            catch { }
        }

        /// <summary>El precio del grafico en el segundo tUtc: el ultimo tick de los 120 s previos (como backtest_familia.Precio); si no hay,
        /// la vela que contiene el instante interpolada apertura -&gt; cierre; NaN si tampoco.</summary>
        private double PrecioEn(DateTime tUtc)
        {
            long s = (tUtc.Ticks - EPOCA) / TimeSpan.TicksPerSecond;
            for (int k = 0; k <= 120; k++)
            {
                long q = s - k; if (q <= 0) break;
                int i = (int)(q % ANILLO_S);
                if (_anilloSeg[i] == q) { double p = _anilloPx[i]; if (p > 0) return p; }
            }
            return PrecioVelaEn(tUtc, null);
        }

        private struct VelaPx { public DateTime T; public double O, C; }

        /// <summary>La vela del grafico que contiene tUtc, interpolada apertura -&gt; cierre por la fraccion de tiempo transcurrida (la vela
        /// termina donde empieza la siguiente; huecos de mas de 10 min no cuentan). Con 'velas' usa esa foto (hilo aparte).</summary>
        private double PrecioVelaEn(DateTime tUtc, List<VelaPx> velas)
        {
            try
            {
                if (velas != null)
                {
                    int lo = 0, hi = velas.Count - 1, j = -1;
                    while (lo <= hi) { int m = (lo + hi) / 2; if (velas[m].T <= tUtc) { j = m; lo = m + 1; } else hi = m - 1; }
                    if (j < 0 || j + 1 >= velas.Count) return double.NaN;
                    var a = velas[j]; var fin = velas[j + 1].T;
                    double span = (fin - a.T).TotalSeconds;
                    if (span <= 0 || span > 600) return double.NaN;
                    double fr = Math.Max(0, Math.Min(1, (tUtc - a.T).TotalSeconds / span));
                    return a.O + (a.C - a.O) * fr;
                }
                for (int b = CurrentBar - 1, n = 0; b >= 1 && n < 20000; b--, n++)
                {
                    IndicatorCandle c; try { c = GetCandle(b); } catch { break; }
                    if (c == null) continue;
                    var t0 = Utc(c.Time);
                    if (t0 > tUtc) continue;
                    DateTime fin;
                    if (b + 1 <= CurrentBar - 1) { var sig = GetCandle(b + 1); fin = sig != null ? Utc(sig.Time) : t0.AddMinutes(1); }
                    else fin = DateTime.UtcNow;
                    double span = (fin - t0).TotalSeconds;
                    if (span <= 0 || span > 600 || tUtc >= fin) return double.NaN;
                    double fr = Math.Max(0, Math.Min(1, (tUtc - t0).TotalSeconds / span));
                    return (double)c.Open + ((double)c.Close - (double)c.Open) * fr;
                }
            }
            catch { }
            return double.NaN;
        }

        /// <summary>Foto de las velas (apertura y cierre por hora) para la siembra desde el archivo en otro hilo. Llamar desde el temporizador.</summary>
        private List<VelaPx> FotoVelas(int maximo)
        {
            var l = new List<VelaPx>();
            try
            {
                int ult = CurrentBar - 1;
                for (int b = Math.Max(0, ult - maximo + 1); b <= ult; b++)
                {
                    IndicatorCandle c; try { c = GetCandle(b); } catch { continue; }
                    if (c == null) continue;
                    l.Add(new VelaPx { T = Utc(c.Time), O = (double)c.Open, C = (double)c.Close });
                }
                l.Sort((x, y) => x.T.CompareTo(y.T));
            }
            catch { }
            return l;
        }

        /// <summary>El contrato del grafico como trimestre ("Z6"): del codigo del grafico, o del futuro grande de la API. "" si no se sabe.</summary>
        private string Trimestre37()
        {
            string T(string c)
            {
                c = (c ?? "").Trim().ToUpperInvariant();
                if (c.Length >= 2 && "HMUZ".IndexOf(c[c.Length - 2]) >= 0 && char.IsDigit(c[c.Length - 1])) return c.Substring(c.Length - 2);
                return "";
            }
            var x = T(CodigoGrafico());
            return x != "" ? x : T(_cadena.CodigoFuturo);
        }

        /// <summary>Vencimiento (UTC) del trimestre del grafico: tercer viernes 9:30 NY (Seleccion37); si no se entiende el codigo, la fecha de
        /// la API a las 9:30 NY.</summary>
        private DateTime VencimientoGrafico37()
        {
            var tri = Trimestre37();
            var v = tri == "" ? default(DateTime) : Seleccion37.VencimientoTrimestralUtc(tri, DateTime.UtcNow);
            if (v == default(DateTime) && _cadena.FechaVencimientoFuturo != default(DateTime)) v = Seleccion37.AUtc(_cadena.FechaVencimientoFuturo.Date.AddHours(9.5));
            return v;
        }

        // ------------------------------------------------------------------ C1: el OI de NQ con fecha

        private readonly SaltoOi37 _saltoOi = new SaltoOi37();
        private SaltoOi37 _saltoOiMemoria = new SaltoOi37();
        private bool _oiNqFresco = true;
        private string _oiNqMotivo = "";
        private DateTime _oiUltimaFotoMin = DateTime.MinValue;
        private bool _oiCargado, _oiAvisadoAnteayer;
        private string RutaSaltoOi(string raiz) => Path.Combine(Registro.CarpetaDatos, "oi-salto-" + raiz + ".json");

        /// <summary>Desde el Tick con las filas de la API: una foto por minuto al detector del salto (contrato -&gt; OI, solo con Summary) y el
        /// estado de ahora. Persiste el salto para que un reinicio no lo pierda.</summary>
        private void ActualizarOiNq(string raiz, List<CadenaApi.Fila> fs, DateTime ahora)
        {
            try
            {
                if (!_oiCargado)
                {
                    _oiCargado = true;
                    try { var r = RutaSaltoOi(raiz); if (File.Exists(r) && _saltoOi.Cargar(File.ReadAllText(r), ahora)) Log("OI NQ: salto de esta sesion cargado de " + r + ": " + _saltoOi.Serializar()); } catch { }
                }
                var minuto = new DateTime(ahora.Year, ahora.Month, ahora.Day, ahora.Hour, ahora.Minute, 0, DateTimeKind.Utc);
                if (fs != null && fs.Count > 0 && minuto > _oiUltimaFotoMin)
                {
                    _oiUltimaFotoMin = minuto;
                    var mapa = new Dictionary<string, double>();
                    foreach (var f in fs)
                    {
                        if (!f.ConResumen) continue;
                        string k = !string.IsNullOrEmpty(f.Codigo) ? f.Codigo : f.K.ToString("0.##", CultureInfo.InvariantCulture) + "|" + f.Dias.ToString("0.0", CultureInfo.InvariantCulture) + "|" + (f.EsCall ? "C" : "P");
                        mapa[k] = f.OI;
                    }
                    if (_saltoOi.Observar(ahora, mapa))
                    {
                        Log("OI NQ: SALTO de Rithmic medido a las " + Seleccion37.ANy(ahora).ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " NY (" + _saltoOi.UltimoCambiados + " de " + _saltoOi.UltimoComunes + " contratos cambiaron de OI): desde ahora el OI es el de ayer");
                        try { Directory.CreateDirectory(Registro.CarpetaDatos); File.WriteAllText(RutaSaltoOi(raiz), _saltoOi.Serializar()); } catch (Exception e) { Registro.Excepcion(LOG, "guardar oi-salto", e); }
                    }
                }
                bool viejo = _saltoOi.OiDeAnteayer(ahora, out var motivo);
                if (viejo != !_oiNqFresco || (viejo && !_oiAvisadoAnteayer)) Log("OI NQ: " + (viejo ? "DE ANTEAYER (sin M± OI, muros ni cruces por OI de NQ)" : "de ayer") + " · " + motivo);
                if (viejo) _oiAvisadoAnteayer = true; else _oiAvisadoAnteayer = false;
                _oiNqFresco = !viejo; _oiNqMotivo = motivo;
            }
            catch (Exception e) { Registro.Excepcion(LOG, "ActualizarOiNq", e); }
        }

        private string OiNqTexto => _oiNqFresco ? "OI de ayer" : "OI de anteayer";

        // ------------------------------------------------------------------ D2: las dominantes, igual en el vivo, el rebobinado y el pulso

        /// <summary>
        /// Elige D1/D2 sobre la lectura del nucleo. DosMasGrandes (default 3.7.0, D2) = la regla de la 2.0 por volumen, de dia y de noche,
        /// strike exacto, sin centroide ni histeresis (Seleccion37.DosMasGrandes); solo si no hay volumen en el radio y el OI es de ayer, por
        /// OI; 3.7.1 (A3): D1 = la de MAYOR |GEX| (como la 2.0), D2 la segunda. Las otras reglas (Clasica, Tres, Tunel) quedan como estaban,
        /// con el tunel C2 y el OI con fecha (C1) y D1 = la mas cercana (3.0.9).
        /// pulso = true: no mueve ningun estado (histeresis sin decidir, tunel sobre una copia).
        /// </summary>
        private void SeleccionarDoms(GammaHoyNucleo.Lectura L, double fut, double cierre, DateTime ahora, string raiz, bool memoria, bool decidir, bool oiFresco, bool pulso = false)
        {
            if (L == null || L.Perfil == null || fut <= 0) return;
            var regla = ReglaEfectiva();
            if (regla == ReglaDominantes3.DosMasGrandes)
            {
                L.Doms = Dibujo37.DominantesNq(L.Perfil, fut, RadioDominantes(raiz), oiFresco, out var libro); L.LibroDom = libro;   // sin ATAS (Simulador37 llama lo mismo)
                return;
            }
            if (pulso)
            {
                if (regla == ReglaDominantes3.Tres && Histeresis3.EsRueda(ahora))
                {
                    var h = Hist;
                    h.HistX = (double)Math.Max(0m, Histeresis3Pct); h.HistN = Math.Max(1, Histeresis3Min); h.RadioCentroidePts = Rayas3Independientes ? 0.0 : (double)Math.Max(0m, Histeresis3CentroidePts);
                    h.Aplicar(L, RadioDominantesEfectivo(L.Futuro), _nucleo.A.EmpatePct, ahora, false);
                }
            }
            else AplicarRegla(L, ahora, memoria, decidir);
            AplicarTunel(L, fut, cierre, raiz, memoria, ahora, oiFresco, pulso);
            if (regla != ReglaDominantes3.Tres && L.Doms.Count > 1) L.Doms = Seleccion37.PorCercania(L.Doms, fut);
            if (!oiFresco && L.LibroDom == "OI") L.Doms = new List<(double Fut, double Gex)>();   // C1: nada por OI de anteayer
        }

        // ------------------------------------------------------------------ 3.7.1 (A5): UNA marca por (libro, strike) en cada vela

        /// <summary>Una marca pendiente de una vela: el nivel (libro, tipo, precio) y como se pinta. Los pintores (estela de NQ, capas, formulas,
        /// majors por OI) ya no pintan directo: encolan, y PintarMarcasAgrupadas pinta UNA por grupo (Dibujo37.AgruparMarcas, la misma funcion
        /// que corre Simulador37) con el estilo del de mayor jerarquia. Todo en el hilo de OnRender.</summary>
        private struct MarcaPend37 { public int B; public Dibujo37.Marca37 M; public Action Pintar; public bool Visible; }
        private readonly List<MarcaPend37> _marcasPend = new List<MarcaPend37>();
        private int _marcasPintadas, _marcasEncoladas, _marcasJuntas, _marcasCortadas, _marcasVivaSinRotulo;

        /// <summary>3.7.2: se encola TODA marca a &lt;= radio de su vela, este o no en pantalla (visible = su y cae en el lienzo), para que la regla
        /// de la vela (Dibujo37.ElegirVela: juntas y tope) decida sobre lo mismo que el simulador aunque haya zoom; solo se pinta si es visible.</summary>
        private void EncolarMarca(int b, string libro, string tipo, double p, bool visible, Action pintar)
        {
            if (double.IsNaN(p) || p <= 0 || pintar == null) return;
            _marcasPend.Add(new MarcaPend37 { B = b, M = new Dibujo37.Marca37(libro, tipo, p), Pintar = pintar, Visible = visible });
        }

        /// <summary>3.7.2: por cada vela, Dibujo37.ElegirVela (la misma que Simulador37): una marca por (libro, strike) con el estilo del de mayor
        /// jerarquia, solo a &lt;= radio de la referencia (cierre de la vela; la viva: el precio de ahora), lo que queda a &lt;= 'juntas' pts de una
        /// de mayor jerarquia no se pinta aparte, y a lo sumo Tope373Rayas por vela (3.7.3). En la vela viva ademas solo lo que tiene rotulo (clavesViva =
        /// Dibujo37.ClavesRotuladas de la vista de los rotulos): cada raya de la vela viva con su numero.</summary>
        private void PintarMarcasAgrupadas(Func<int, double> refDe, int ultimaBar, HashSet<string> clavesViva)
        {
            try
            {
                _marcasEncoladas = _marcasPend.Count; _marcasPintadas = 0; _marcasJuntas = 0; _marcasCortadas = 0; _marcasVivaSinRotulo = 0;
                double radio = RadioDibujo37, juntas = Juntas37; int tope = TopeVela37;
                foreach (var porVela in _marcasPend.GroupBy(x => x.B))
                {
                    var lista = porVela.ToList();
                    var marcas = lista.Select((x, i) => { var m = x.M; m.Id = i; return m; }).ToList();
                    bool viva = porVela.Key >= ultimaBar;
                    foreach (var grupo in Dibujo37.ElegirVela(marcas, refDe(porVela.Key), radio, juntas, tope))
                    {
                        if (grupo.Absorbido) { _marcasJuntas++; continue; }
                        if (grupo.Cortado) { _marcasCortadas++; continue; }
                        if (!grupo.Dib) continue;
                        if (viva && clavesViva != null && !clavesViva.Contains(grupo.Clave)) { _marcasVivaSinRotulo++; continue; }
                        var x = lista[grupo.Primero.Id];
                        if (!x.Visible) continue;
                        try { x.Pintar(); _marcasPintadas++; } catch { }
                    }
                }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "PintarMarcasAgrupadas", e); }
            finally { _marcasPend.Clear(); }
        }

        /// <summary>La edad del dato (min) y la fecha del OI de cada capa con cuenta, para los renglones de las capas (Dibujo37.VistaRotulos).</summary>
        private Dictionary<string, (double EdadMin, string FechaOi)> InfoCapasRotulo()
        {
            var d = new Dictionary<string, (double, string)>(StringComparer.OrdinalIgnoreCase);
            var ahora = DateTime.UtcNow;
            if (_oiNqFresco) { var r = Raiz(); d[string.IsNullOrEmpty(r) ? "NQ" : r] = (double.NaN, SaltoOi37.RotuloFechaOi(ahora)); }   // 3.7.2: la fecha del OI de NQ en su rotulo
            foreach (var k in _capas)
            {
                DateTime ult; bool hay;
                lock (_candado) { ult = k.UltimoTradeUtc; hay = k.L != null; }
                if (!hay) continue;
                d[k.Nombre] = (ult == DateTime.MinValue ? double.NaN : (ahora - ult).TotalMinutes, k.FechaOi.Rotulo(ahora));
            }
            return d;
        }

        // ------------------------------------------------------------------ D1: rotulos del borde para la vela viva

        /// <summary>3.7.1 (A5): los grupos del borde (ya elegidos y con el tope aplicado por Dibujo37.VistaRotulos), apilados en los bordes con
        /// flecha, libro, tipos y distancia ('↑ NQ M+OI +87'). Devuelve cuanto ocupo arriba y abajo (para que la escalera no se encime).</summary>
        private (int YTop, int YBot) PintarBorde37(RenderContext g, RenderFont f, RenderFont fC, CultureInfo es, int xCol, int wCol, int yTop, int yBot,
                                                   List<Dibujo37.Grupo37> borde, List<Rot37> cand, double fut, bool minimal)
        {
            if (borde == null || borde.Count == 0) return (yTop, yBot);
            foreach (var gr in borde)
            {
                var q = cand[gr.Primero.Id];
                var ff = q.Chico ? fC : f;
                int h = g.MeasureString("X", ff).Height + 2;
                string txt = Dibujo37.TextoBordeGrupo(gr, fut, es);   // sin ATAS (Simulador37 escribe el mismo texto)
                bool arriba = gr.P > fut;
                int y = arriba ? yTop : yBot - h;
                if (minimal)
                {
                    g.DrawString(txt, ff, Color.FromArgb(210, ColFondo), xCol + 1, y + 2);
                    g.DrawString(txt, ff, Color.FromArgb(235, q.C), xCol, y + 1);
                }
                else Chip(g, ff, xCol, y, Math.Max(wCol, g.MeasureString(txt, ff).Width + 12), h, txt, q.C);
                if (arriba) yTop += h + 1; else yBot -= h + 1;
            }
            return (yTop, yBot);
        }
    }
}
