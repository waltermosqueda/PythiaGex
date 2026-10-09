using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ATAS.Indicators;

namespace PythiaGexTres
{
    /// <summary>
    /// LA CAJA NEGRA (3.1.0, 06-10-2026, pedido del operador: "si, arma"). En cada TOQUE de un nivel graba, en ese instante, todo lo que
    /// hay para contestar despues con numero "que lo hace reaccionar": la foto de las opciones del strike (OI, volumen, salto de los
    /// ultimos 5 min, IV, gex, vanna) y sus vecinos, la cinta de los ultimos 60 s (delta, volumen, prints grandes) y el libro de ordenes
    /// Level 2 (10 niveles por lado; NO MBO, que ya colgo ATAS por memoria). A los 5, 15 y 30 min graba el resultado crudo (alto, bajo,
    /// cierre, a favor / en contra). Se graban TAMBIEN niveles PLACEBO (las dominantes de NQ corridas +-12,5 y +-25): sin control no hay
    /// medicion. Formato y preguntas pre-registradas: laboratorio/tres/CAJA_NEGRA_PRE.md; lector: laboratorio/tres/caja_negra.py.
    /// Archivo: %APPDATA%\ATAS\PythiaGex3\caja\caja-&lt;raiz&gt;-&lt;yyyy-MM-dd&gt;.jsonl (dia UTC), una linea JSON por evento.
    /// Toque = la primera vela (la viva, mirada cada 5 s) que entra en [nivel - zona, nivel + zona] cuando la cerrada anterior no estaba;
    /// un toque por nivel y lado hasta que un cierre se aleje LEJOS (PRE_REGISTRO seccion 3, la misma regla del indicador).
    /// No dibuja nada: solo graba.
    /// </summary>
    public partial class GammaHoyTres
    {
        [Display(Name = "Caja negra: grabar toques", GroupName = "4. Caja negra", Order = 10,
                 Description = "3.1.0. En cada toque de una dominante (y de sus placebos) graba opciones, cinta y libro en PythiaGex3\\caja\\caja-<raiz>-<dia>.jsonl, y el resultado a 5/15/30 min. No dibuja nada.")]
        public bool Caja3Activa { get; set; } = true;

        [Display(Name = "Caja negra: niveles placebo (+-12,5 y +-25)", GroupName = "4. Caja negra", Order = 20,
                 Description = "Graba tambien los toques a las dominantes de NQ corridas +-12,5 y +-25 pts. Es el control: sin el, el rebote medido no se puede comparar con nada.")]
        public bool Caja3Placebos { get; set; } = true;

        [Display(Name = "Caja negra: capas NDX/QQQ y zero", GroupName = "4. Caja negra", Order = 30,
                 Description = "Graba tambien los toques a las dominantes de las capas (con la edad del dato) y al zero gamma de NQ.")]
        public bool Caja3Capas { get; set; } = true;

        [Display(Name = "Caja negra: niveles del libro por lado", GroupName = "4. Caja negra", Order = 40,
                 Description = "Cuantos niveles de bid y de ask se guardan de la foto Level 2 en el instante del toque.")]
        [Range(1, 20)]
        public int Caja3Profundidad { get; set; } = 10;

        [Display(Name = "Caja negra: lote grande (contratos)", GroupName = "4. Caja negra", Order = 50,
                 Description = "Una operacion agregada (CumulativeTrade) de este tamaño o mas cuenta como print grande en la cinta de los 60 s previos.")]
        [Range(1, 1000)]
        public int Caja3LoteGrande { get; set; } = 20;

        // ---- estado
        private sealed class CajaNivel { public string Fam, Etq, Origen; public double Nivel; public double CapaEdadMin = double.NaN; public CapaCboe Capa; }
        /// <summary>Por nivel: la vela ya registrada, y por lado la hora del ultimo cierre a >= LEJOS (el "viene de lejos" del juez, 30 min) y si ese
        /// alejamiento ya se gasto en un toque. Un toque cuenta solo si hubo cierre lejos de su lado en los 30 min previos y no se consumio (3.1.1).</summary>
        private sealed class CajaEstado { public DateTime Vela = DateTime.MinValue; public DateTime LejosArriba = DateTime.MinValue, LejosAbajo = DateTime.MinValue; public bool GastadoArriba, GastadoAbajo; public DateTime Visto; }
        private sealed class CajaPendiente { public string Id; public DateTime T; public double Nivel; public int Lado; public int Barra; public bool H5, H15, H30; }
        private readonly Dictionary<string, CajaEstado> _cajaEstado = new Dictionary<string, CajaEstado>();
        private readonly Dictionary<string, (DateTime T, double Nivel)> _cajaUltimoToque = new Dictionary<string, (DateTime, double)>();   // 3.2.4
        private readonly List<CajaPendiente> _cajaPendientes = new List<CajaPendiente>();
        private readonly object _cajaLlave = new object();
        private readonly List<(DateTime T, double P, double V, int Lado)> _cajaTrades = new List<(DateTime, double, double, int)>();
        private CumulativeTrade _cajaActual;
        private readonly List<(DateTime T, Dictionary<double, double> Vol)> _cajaVolHist = new List<(DateTime, Dictionary<double, double>)>();
        private DateTime _cajaUltimaMuestraVol = DateTime.MinValue, _cajaUltimoResumen = DateTime.MinValue;
        private int _cajaToquesHoy, _cajaResultadosHoy, _cajaNiveles;
        private string _cajaDia = "";

        // ---- la cinta en vivo (liviano: sin LINQ ni archivos aca; llegan cientos por segundo)
        protected override void OnCumulativeTrade(CumulativeTrade trade)
        {
            CintaEvento(trade, true);   // 3.4.0: la cinta en vivo (GammaHoyTresCinta.cs): encola un struct y vuelve; nunca tira
            try
            {
                if (trade == null) return;
                CumulativeTrade cerrado = null;
                lock (_cajaLlave) { if (_cajaActual != null && !ReferenceEquals(_cajaActual, trade)) cerrado = _cajaActual; _cajaActual = trade; }
                if (cerrado != null) CajaAlimentar(cerrado);
            }
            catch { }
        }
        protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
        {
            CintaEvento(trade, false);  // 3.4.0: la cinta en vivo (GammaHoyTresCinta.cs): solo el volumen nuevo de esta operacion
            try { if (trade != null) lock (_cajaLlave) _cajaActual = trade; } catch { }
        }
        private void CajaAlimentar(CumulativeTrade t)
        {
            int lado = t.Direction == ATAS.Indicators.TradeDirection.Buy ? 1 : t.Direction == ATAS.Indicators.TradeDirection.Sell ? -1 : 0;
            var tt = t.Time.Kind == DateTimeKind.Utc ? t.Time : DateTime.SpecifyKind(t.Time, DateTimeKind.Utc);
            lock (_cajaLlave)
            {
                _cajaTrades.Add((tt, (double)t.Lastprice, (double)t.Volume, lado));
                var lim = tt.AddSeconds(-180);
                int quitar = 0; while (quitar < _cajaTrades.Count && _cajaTrades[quitar].T < lim) quitar++;
                if (quitar > 0) _cajaTrades.RemoveRange(0, quitar);
            }
        }

        // ---- el latido (desde Tick, cada 5 s, despues de la cuenta)
        private void CajaLatido(string raiz, DateTime ahora)
        {
            if (!Caja3Activa || string.IsNullOrEmpty(raiz)) return;
            try
            {
                GammaHoyNucleo.Lectura L; Feed.Cadena c; double fut;
                lock (_candado) { L = _L; c = _c; fut = _futuro; }
                if (L == null || c == null || fut <= 0) { CajaResultados(ahora); return; }
                var inv = CultureInfo.InvariantCulture;
                string dia = ahora.ToString("yyyy-MM-dd", inv);
                if (dia != _cajaDia) { _cajaDia = dia; _cajaToquesHoy = 0; _cajaResultadosHoy = 0; }

                // muestra de volumen por strike cada 60 s (para salto5)
                if ((ahora - _cajaUltimaMuestraVol).TotalSeconds >= 60)
                {
                    _cajaUltimaMuestraVol = ahora;
                    var d = new Dictionary<double, double>();
                    foreach (var s in L.Perfil) d[s.Fut] = s.VolHoy;
                    _cajaVolHist.Add((ahora, d));
                    while (_cajaVolHist.Count > 0 && (ahora - _cajaVolHist[0].T).TotalMinutes > 7) _cajaVolHist.RemoveAt(0);
                }

                // los niveles vigilados ahora
                var niveles = new List<CajaNivel>();
                for (int i = 0; i < Math.Min(2, L.Doms.Count); i++)
                {
                    double p = L.Doms[i].Fut; if (p <= 0) continue;
                    string etq = "D" + (i + 1);
                    niveles.Add(new CajaNivel { Fam = "NQ", Etq = etq, Origen = "", Nivel = p });
                    if (Caja3Placebos)
                        foreach (double corr in new[] { 12.5, -12.5, 25.0, -25.0 })
                            niveles.Add(new CajaNivel { Fam = "PLACEBO", Etq = etq, Origen = "NQ " + etq + " " + (corr > 0 ? "+" : "") + corr.ToString("0.#", inv), Nivel = p + corr });
                }
                if (Caja3Capas)
                {
                    if (!double.IsNaN(L.ZeroVol) && L.ZeroVol > 0) niveles.Add(new CajaNivel { Fam = "ZERO", Etq = "0G", Origen = "", Nivel = L.ZeroVol });
                    if (!double.IsNaN(L.ZeroOi) && L.ZeroOi > 0) niveles.Add(new CajaNivel { Fam = "ZEROOI", Etq = "0GOI", Origen = "", Nivel = L.ZeroOi });
                    { int ia = 0; foreach (var a in ApoyoActual()) { ia++; niveles.Add(new CajaNivel { Fam = "APOYO", Etq = "A" + ia, Origen = a.Ctos.ToString("0", inv) + " ctos", Nivel = a.Fut }); } }   // 3.2.3
                    int cuantas = Math.Max(2, Math.Min(3, Capa3Cuantas));
                    foreach (var k in _capas)
                    {
                        GammaHoyNucleo.Lectura Lk; DateTime ult;
                        lock (_candado) { Lk = k.L; ult = k.UltimoTradeUtc; }
                        if (Lk == null || ModoDe(k) == CapaModo3.No) continue;
                        for (int i = 0; i < Math.Min(cuantas, Lk.Doms.Count); i++)
                        {
                            double p = Lk.Doms[i].Fut; if (p <= 0) continue;
                            niveles.Add(new CajaNivel { Fam = k.Nombre, Etq = "D" + (i + 1), Origen = "", Nivel = p, Capa = k, CapaEdadMin = ult == DateTime.MinValue ? double.NaN : (ahora - ult).TotalMinutes });
                        }
                    }
                }
                _cajaNiveles = niveles.Count;

                // la vela viva y la cerrada anterior
                int bViva = CurrentBar - 1; if (bViva < 1) { CajaResultados(ahora); return; }
                IndicatorCandle viva, prev;
                try { viva = GetCandle(bViva); prev = GetCandle(bViva - 1); } catch { return; }
                if (viva == null || prev == null) return;
                DateTime tViva = Utc(viva.Time);
                double hi = (double)viva.High, lo = (double)viva.Low, pc = (double)prev.Close, ph = (double)prev.High, pl = (double)prev.Low;
                double zona = ZonaToque(raiz), lejos = Lejos(raiz);
                var vivos = new HashSet<string>();
                foreach (var n in niveles)
                {
                    // 3.2.4: los niveles continuos (zero por volumen / por OI, capas) se mueven centesimas tick a tick: la clave va redondeada al punto,
                    // y un toque de la misma familia a <= 3 pts en los ultimos 5 min no se repite (a las 02:41 ZEROOI anoto 3 toques en 15 s).
                    bool continuo = n.Fam != "NQ" && n.Fam != "PLACEBO";
                    string key = n.Fam + "|" + n.Origen + "|" + (continuo ? Math.Round(n.Nivel).ToString("0", inv) : n.Nivel.ToString("0.00", inv));
                    vivos.Add(key);
                    if (!_cajaEstado.TryGetValue(key, out var est))
                    {
                        est = new CajaEstado(); _cajaEstado[key] = est;
                        // nivel nuevo: los cierres lejos de los ultimos 30 min, para no esperar media hora a que el nivel "exista"
                        for (int b = bViva - 1, k = 0; b >= 0 && k < 400; b--, k++)
                        {
                            IndicatorCandle cb; try { cb = GetCandle(b); } catch { break; }
                            if (cb == null) continue;
                            var tb = Utc(cb.Time); if ((ahora - tb).TotalMinutes > 30) break;
                            double cc = (double)cb.Close;
                            if (cc >= n.Nivel + lejos && tb > est.LejosArriba) est.LejosArriba = tb;
                            if (cc <= n.Nivel - lejos && tb > est.LejosAbajo) est.LejosAbajo = tb;
                        }
                    }
                    est.Visto = ahora;
                    // "viene de lejos": el cierre de la vela cerrada anterior a >= LEJOS del nivel renueva el permiso de ese lado
                    DateTime tPrev = Utc(prev.Time);
                    if (pc >= n.Nivel + lejos && tPrev > est.LejosArriba) { est.LejosArriba = tPrev; est.GastadoArriba = false; }
                    if (pc <= n.Nivel - lejos && tPrev > est.LejosAbajo) { est.LejosAbajo = tPrev; est.GastadoAbajo = false; }
                    bool enZona = lo <= n.Nivel + zona && hi >= n.Nivel - zona;
                    bool prevEnZona = pl <= n.Nivel + zona && ph >= n.Nivel - zona;
                    if (!enZona || prevEnZona || est.Vela == tViva) continue;
                    int lado = pc > n.Nivel ? +1 : -1;
                    DateTime lejosT = lado == +1 ? est.LejosArriba : est.LejosAbajo;
                    bool gastado = lado == +1 ? est.GastadoArriba : est.GastadoAbajo;
                    if (lejosT == DateTime.MinValue || (ahora - lejosT).TotalMinutes > 30 || gastado) continue;   // no viene de lejos (o ya conto): es un cruce, no un toque
                    if (_cajaUltimoToque.TryGetValue(n.Fam, out var ut) && (ahora - ut.T).TotalSeconds <= 300 && Math.Abs(ut.Nivel - n.Nivel) <= 3) continue;   // 3.2.4
                    _cajaUltimoToque[n.Fam] = (ahora, n.Nivel);
                    est.Vela = tViva;
                    if (lado == +1) est.GastadoArriba = true; else est.GastadoAbajo = true;
                    CajaRegistrarToque(raiz, n, lado, ahora, tViva, bViva, viva, fut, zona, lejos, L, c, niveles, (ahora - lejosT).TotalMinutes);
                }
                // estados viejos (niveles que ya no existen hace mas de un dia)
                if (_cajaEstado.Count > 400)
                    foreach (var k in _cajaEstado.Where(kv => (ahora - kv.Value.Visto).TotalHours > 24).Select(kv => kv.Key).ToList()) _cajaEstado.Remove(k);

                CajaResultados(ahora);
                if ((ahora - _cajaUltimoResumen).TotalMinutes >= 5)
                {
                    _cajaUltimoResumen = ahora;
                    int nT; lock (_cajaLlave) nT = _cajaTrades.Count;
                    Log("caja: " + _cajaToquesHoy + " toques y " + _cajaResultadosHoy + " resultados hoy (UTC), " + _cajaNiveles + " niveles vigilados, " + _cajaPendientes.Count + " pendientes, cinta " + nT + " operaciones en 3 min");
                }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "CajaLatido", e); }
        }

        private static string J(double x, string f = "0.####") => double.IsNaN(x) || double.IsInfinity(x) ? "null" : x.ToString(f, CultureInfo.InvariantCulture);
        private static string JS(string s) => s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private void CajaRegistrarToque(string raiz, CajaNivel n, int lado, DateTime ahora, DateTime tViva, int bViva, IndicatorCandle viva, double fut,
                                        double zona, double lejos, GammaHoyNucleo.Lectura L, Feed.Cadena c, List<CajaNivel> todos, double minDesdeLejos)
        {
            var inv = CultureInfo.InvariantCulture;
            string id = ahora.ToString("yyyyMMddHHmmss", inv) + "-" + n.Fam + "-" + n.Nivel.ToString("0.00", inv) + "-" + (lado > 0 ? "1" : "m1");
            var sb = new StringBuilder(2048);
            sb.Append("{\"e\":\"toque\",\"id\":").Append(JS(id)).Append(",\"t\":\"").Append(ahora.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", inv)).Append("\",\"vela\":\"").Append(tViva.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", inv))
              .Append("\",\"fam\":").Append(JS(n.Fam)).Append(",\"etq\":").Append(JS(n.Etq)).Append(",\"nivel\":").Append(J(n.Nivel)).Append(",\"origen\":").Append(JS(n.Origen))
              .Append(",\"lado\":").Append(lado).Append(",\"fut\":").Append(J(fut)).Append(",\"zona\":").Append(J(zona)).Append(",\"lejos\":").Append(J(lejos)).Append(",\"minDesdeLejos\":").Append(J(minDesdeLejos, "0.#"))
              .Append(",\"velaOHLC\":[").Append(J((double)viva.Open)).Append(',').Append(J((double)viva.High)).Append(',').Append(J((double)viva.Low)).Append(',').Append(J((double)viva.Close)).Append(']')
              .Append(",\"velaVol\":").Append(J((double)viva.Volume, "0")).Append(",\"velaDelta\":").Append(J((double)viva.Delta, "0"));
            // opciones: el strike del nivel (o el mas cercano) en el libro que corresponde
            try { sb.Append(",\"op\":").Append(CajaOpciones(n, fut, ahora, L, c)); } catch (Exception e) { sb.Append(",\"op\":null"); Registro.Excepcion(LOG, "CajaOpciones", e); }
            try { sb.Append(",\"cinta\":").Append(CajaCinta(ahora)); } catch { sb.Append(",\"cinta\":null"); }
            try { sb.Append(",\"libro\":").Append(CajaLibro()); } catch (Exception e) { sb.Append(",\"libro\":null"); Registro.Excepcion(LOG, "CajaLibro", e); }
            string vigDesde = null;
            if (n.Fam == "NQ" && ReglaEfectiva() == ReglaDominantes3.Tres) { var t0 = Hist.DesdeCuando(n.Nivel); if (t0.HasValue) vigDesde = t0.Value.ToString("HH:mm'Z'", inv); }
            sb.Append(",\"hist\":{\"regla\":").Append(JS(ReglaEfectiva().ToString())).Append(",\"vigDesde\":").Append(JS(vigDesde)).Append('}');
            // 3.1.1: los niveles vigentes de las demas familias en este instante (para la pregunta del ping-pong), sin los placebos
            sb.Append(",\"otras\":[");
            bool pri = true;
            foreach (var o in todos)
            {
                if (o.Fam == "PLACEBO" || (o.Fam == n.Fam && Math.Abs(o.Nivel - n.Nivel) < 0.01)) continue;
                if (!pri) sb.Append(','); pri = false;
                sb.Append('[').Append(JS(o.Fam)).Append(',').Append(JS(o.Etq)).Append(',').Append(J(o.Nivel)).Append(']');
            }
            sb.Append(']');
            var foto = _cadena.Foto();
            sb.Append(",\"dato\":{\"edadLibroS\":").Append(J(foto.EdadSegundos, "0")).Append(",\"conPuntas\":").Append(foto.ConPuntas).Append(",\"suscritos\":").Append(foto.Suscritos)
              .Append(",\"capaEdadMin\":").Append(J(n.CapaEdadMin, "0")).Append("}}");
            CajaEscribir(raiz, ahora, sb.ToString());
            _cajaToquesHoy++;
            _cajaPendientes.Add(new CajaPendiente { Id = id, T = ahora, Nivel = n.Nivel, Lado = lado, Barra = bViva });
            Log("caja: toque " + n.Fam + " " + n.Etq + (n.Origen == "" ? "" : " (" + n.Origen + ")") + " " + n.Nivel.ToString("0.00", inv) + " lado " + (lado > 0 ? "desde arriba" : "desde abajo") + " fut " + fut.ToString("0.00", inv));
        }

        /// <summary>Vanna Black-76 por contrato: -e^(-rT) phi(d1) d2 / sigma (derivada de delta respecto de sigma). Sin escalar.</summary>
        private static double Vanna76(double F, double K, double T, double sigma, double r)
        {
            if (F <= 0 || K <= 0 || T <= 0 || sigma <= 0) return double.NaN;
            double sq = sigma * Math.Sqrt(T);
            double d1 = (Math.Log(F / K) + 0.5 * sigma * sigma * T) / sq, d2 = d1 - sq;
            double phi = Math.Exp(-0.5 * d1 * d1) / Math.Sqrt(2 * Math.PI);
            return -Math.Exp(-r * T) * phi * d2 / sigma;
        }

        private string CajaOpciones(CajaNivel n, double fut, DateTime ahora, GammaHoyNucleo.Lectura Lnq, Feed.Cadena cnq)
        {
            var inv = CultureInfo.InvariantCulture;
            GammaHoyNucleo.Lectura L = Lnq; Feed.Cadena c = cnq; bool capa = false;
            if (n.Capa != null) { lock (_candado) { L = n.Capa.L; c = n.Capa.Cadena; } capa = true; }
            if (L == null || c == null || L.Perfil.Count == 0) return "null";
            // el strike del nivel: el mas cercano en Fut
            var perfil = L.Perfil.OrderBy(s => s.Fut).ToList();
            int idx = 0; double mejor = double.MaxValue;
            for (int i = 0; i < perfil.Count; i++) { double d = Math.Abs(perfil[i].Fut - n.Nivel); if (d < mejor) { mejor = d; idx = i; } }
            var s0 = perfil[idx];
            // las filas de ese strike del vencimiento mas cercano
            double S = capa ? (c.PorRazon && c.Escala > 0 ? c.AlLibro(fut) : fut - c.Base) : fut;
            Feed.Fila fila = null; int vMin = int.MaxValue;
            foreach (var f in c.Filas) if (f.K == s0.K && f.V < vMin && f.V >= 0 && f.V < c.Dias.Length) { vMin = f.V; fila = f; }
            double dias = fila != null ? c.Dias[fila.V] : double.NaN;
            double T = Math.Max(double.IsNaN(dias) ? 0 : dias, 1.0 / 1440.0) / 365.0;
            double r = _nucleo.A.Tasa;
            double vC = fila != null && fila.IvC > 0 ? Vanna76(S, s0.K, T, fila.IvC, r) : double.NaN;
            double vP = fila != null && fila.IvP > 0 ? Vanna76(S, s0.K, T, fila.IvP, r) : double.NaN;
            double vannaVol = double.NaN, vannaOi = double.NaN;
            if (fila != null) { vannaVol = (double.IsNaN(vC) ? 0 : vC * fila.VolC) - (double.IsNaN(vP) ? 0 : vP * fila.VolP); vannaOi = (double.IsNaN(vC) ? 0 : vC * fila.OiC) - (double.IsNaN(vP) ? 0 : vP * fila.OiP); }
            // salto de volumen del strike en 5 min (solo libro vivo de NQ)
            double salto5 = double.NaN;
            if (!capa && _cajaVolHist.Count > 0)
            {
                var ref5 = _cajaVolHist.Where(h => (ahora - h.T).TotalMinutes >= 4.5).OrderByDescending(h => h.T).FirstOrDefault();
                if (ref5.Vol != null && ref5.Vol.TryGetValue(s0.Fut, out double v5)) salto5 = s0.VolHoy - v5;
            }
            var sb = new StringBuilder(512);
            sb.Append("{\"K\":").Append(J(s0.K)).Append(",\"Fut\":").Append(J(s0.Fut)).Append(",\"dist\":").Append(J(mejor)).Append(",\"dias\":").Append(J(dias))
              .Append(",\"oiC\":").Append(fila != null ? J(fila.OiC, "0") : "null").Append(",\"oiP\":").Append(fila != null ? J(fila.OiP, "0") : "null")
              .Append(",\"volC\":").Append(fila != null ? J(fila.VolC, "0") : "null").Append(",\"volP\":").Append(fila != null ? J(fila.VolP, "0") : "null")
              .Append(",\"salto5\":").Append(J(salto5, "0")).Append(",\"ivC\":").Append(fila != null ? J(fila.IvC) : "null").Append(",\"ivP\":").Append(fila != null ? J(fila.IvP) : "null")
              .Append(",\"gexVol\":").Append(J(s0.GexVol, "0")).Append(",\"gexOi\":").Append(J(s0.GexOi, "0")).Append(",\"vannaVol\":").Append(J(vannaVol)).Append(",\"vannaOi\":").Append(J(vannaOi))
              .Append(",\"vec\":[");
            bool primero = true;
            for (int i = Math.Max(0, idx - 2); i <= Math.Min(perfil.Count - 1, idx + 2); i++)
            {
                var s = perfil[i]; double volC = 0, volP = 0; int vm = int.MaxValue;
                foreach (var f in c.Filas) if (f.K == s.K && f.V < vm) { vm = f.V; volC = f.VolC; volP = f.VolP; }
                if (!primero) sb.Append(','); primero = false;
                sb.Append('[').Append(J(s.Fut)).Append(',').Append(J(volC, "0")).Append(',').Append(J(volP, "0")).Append(',').Append(J(s.GexVol, "0")).Append(']');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private string CajaCinta(DateTime ahora)
        {
            double d60 = 0, v60 = 0, bigMax = 0, bigBuy = 0, bigSell = 0; int n60 = 0, bigN = 0; DateTime ultimo = DateTime.MinValue;
            lock (_cajaLlave)
            {
                var lim = ahora.AddSeconds(-60);
                for (int i = _cajaTrades.Count - 1; i >= 0; i--)
                {
                    var t = _cajaTrades[i];
                    if (t.T > ultimo) ultimo = t.T;
                    if (t.T < lim) continue;
                    n60++; v60 += t.V; d60 += t.Lado * t.V;
                    if (t.V >= Caja3LoteGrande) { bigN++; if (t.V > bigMax) bigMax = t.V; if (t.Lado > 0) bigBuy += t.V; else if (t.Lado < 0) bigSell += t.V; }
                }
            }
            if (n60 == 0 && ultimo == DateTime.MinValue) return "null";
            return "{\"d60\":" + J(d60, "0") + ",\"v60\":" + J(v60, "0") + ",\"n60\":" + n60 + ",\"bigN\":" + bigN + ",\"bigMax\":" + J(bigMax, "0") + ",\"bigBuy\":" + J(bigBuy, "0") + ",\"bigSell\":" + J(bigSell, "0")
                 + ",\"edadS\":" + (ultimo == DateTime.MinValue ? "null" : J((ahora - ultimo).TotalSeconds, "0.#")) + "}";
        }

        private string CajaLibro()
        {
            var dom = MarketDepthInfo?.GetMarketDepthSnapshot()?.ToList();
            if (dom == null || dom.Count == 0) return "null";
            int N = Math.Max(1, Math.Min(20, Caja3Profundidad));
            var bids = dom.Where(x => x.IsBid && x.Volume > 0).OrderByDescending(x => x.Price).Take(N).ToList();
            var asks = dom.Where(x => x.IsAsk && x.Volume > 0).OrderBy(x => x.Price).Take(N).ToList();
            if (bids.Count == 0 && asks.Count == 0) return "null";
            double bidSum = (double)bids.Sum(x => x.Volume), askSum = (double)asks.Sum(x => x.Volume);
            double imb = bidSum + askSum > 0 ? (bidSum - askSum) / (bidSum + askSum) : double.NaN;
            var mayorB = bids.OrderByDescending(x => x.Volume).FirstOrDefault(); var mayorA = asks.OrderByDescending(x => x.Volume).FirstOrDefault();
            string mayor = "null";
            if (mayorB != null && (mayorA == null || mayorB.Volume >= mayorA.Volume)) mayor = "[" + J((double)mayorB.Price) + "," + J((double)mayorB.Volume, "0") + ",\"bid\"]";
            else if (mayorA != null) mayor = "[" + J((double)mayorA.Price) + "," + J((double)mayorA.Volume, "0") + ",\"ask\"]";
            double spread = bids.Count > 0 && asks.Count > 0 ? (double)(asks[0].Price - bids[0].Price) : double.NaN;
            string Lista(List<MarketDataArg> l) => "[" + string.Join(",", l.Select(x => "[" + J((double)x.Price) + "," + J((double)x.Volume, "0") + "]")) + "]";
            return "{\"bid\":" + Lista(bids) + ",\"ask\":" + Lista(asks) + ",\"bidSum\":" + J(bidSum, "0") + ",\"askSum\":" + J(askSum, "0") + ",\"imb\":" + J(imb) + ",\"mayor\":" + mayor + ",\"spread\":" + J(spread) + "}";
        }

        /// <summary>Los resultados a 5, 15 y 30 min: alto, bajo y cierre desde la vela del toque hasta ahora; a favor / en contra segun el lado.</summary>
        private void CajaResultados(DateTime ahora)
        {
            if (_cajaPendientes.Count == 0) return;
            var inv = CultureInfo.InvariantCulture;
            string raiz = Raiz();
            for (int i = _cajaPendientes.Count - 1; i >= 0; i--)
            {
                var p = _cajaPendientes[i];
                double min = (ahora - p.T).TotalMinutes;
                foreach (int h in new[] { 5, 15, 30 })
                {
                    if (min < h) continue;
                    if ((h == 5 && p.H5) || (h == 15 && p.H15) || (h == 30 && p.H30)) continue;
                    // la vela del toque INCLUIDA; a favor / en contra segun el lado; y el ORDEN: segundos desde el toque hasta la primera vela que
                    // alcanzo a favor 12 / 20 y en contra 6 / 8 (regla chica y grande del PRE_REGISTRO para NQ; en ES se escalan /4), con "primero"
                    double alto = double.MinValue, bajo = double.MaxValue, cierre = double.NaN; int velas = 0;
                    double esc = raiz == "ES" ? 0.25 : 1.0;
                    double g12 = double.NaN, s6 = double.NaN, g20 = double.NaN, s8 = double.NaN;
                    double sV6 = double.NaN, sC2 = double.NaN;   // 3.2.5: criterio VISUAL del operador (ventana 6 min = 3 velas m2)
                    for (int b = Math.Max(0, p.Barra); b <= CurrentBar - 1; b++)
                    {
                        IndicatorCandle cb; try { cb = GetCandle(b); } catch { break; }
                        if (cb == null) continue;
                        if (Utc(cb.Time) > p.T.AddMinutes(h)) break;
                        alto = Math.Max(alto, (double)cb.High); bajo = Math.Min(bajo, (double)cb.Low); cierre = (double)cb.Close; velas++;
                        double af = p.Lado > 0 ? alto - p.Nivel : p.Nivel - bajo, ec = p.Lado > 0 ? p.Nivel - bajo : alto - p.Nivel;
                        var tFin = Utc(cb.LastTime != default(DateTime) ? cb.LastTime : cb.Time);
                        double seg = Math.Max(0, (tFin - p.T).TotalSeconds);
                        if (double.IsNaN(g12) && af >= 12 * esc) g12 = seg;
                        if (double.IsNaN(s6) && ec >= 6 * esc) s6 = seg;
                        if (double.IsNaN(g20) && af >= 20 * esc) g20 = seg;
                        if (double.IsNaN(s8) && ec >= 8 * esc) s8 = seg;
                        if (Utc(cb.Time) <= p.T.AddMinutes(6))
                        {
                            double cierreContra = p.Lado > 0 ? p.Nivel - (double)cb.Close : (double)cb.Close - p.Nivel;
                            if (double.IsNaN(sV6) && af >= 6 * esc) sV6 = seg;
                            if (double.IsNaN(sC2) && cierreContra >= 2 * esc) sC2 = seg;
                        }
                    }
                    if (velas == 0) continue;
                    double aFavor = p.Lado > 0 ? alto - p.Nivel : p.Nivel - bajo, enContra = p.Lado > 0 ? p.Nivel - bajo : alto - p.Nivel;
                    string Primero(double g, double s) => double.IsNaN(g) && double.IsNaN(s) ? "null" : double.IsNaN(s) ? "\"G\"" : double.IsNaN(g) ? "\"S\"" : g < s ? "\"G\"" : s < g ? "\"S\"" : "\"=\"";
                    string linea = "{\"e\":\"resultado\",\"id\":" + JS(p.Id) + ",\"min\":" + h + ",\"t\":\"" + ahora.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", inv) + "\",\"nivel\":" + J(p.Nivel) + ",\"lado\":" + p.Lado
                                 + ",\"alto\":" + J(alto) + ",\"bajo\":" + J(bajo) + ",\"cierre\":" + J(cierre) + ",\"aFavor\":" + J(Math.Max(0, aFavor)) + ",\"enContra\":" + J(Math.Max(0, enContra)) + ",\"velas\":" + velas
                                 + ",\"sG12\":" + J(g12, "0") + ",\"sS6\":" + J(s6, "0") + ",\"sG20\":" + J(g20, "0") + ",\"sS8\":" + J(s8, "0") + ",\"primero\":" + Primero(g12, s6) + ",\"primero20_8\":" + Primero(g20, s8) + ",\"sV6\":" + J(sV6, "0") + ",\"sC2\":" + J(sC2, "0") + ",\"vis6\":" + Primero(sV6, sC2) + "}";
                    CajaEscribir(raiz, ahora, linea);
                    _cajaResultadosHoy++;
                    if (h == 5) p.H5 = true; else if (h == 15) p.H15 = true; else p.H30 = true;
                }
                if (p.H30 || min > 40) _cajaPendientes.RemoveAt(i);
            }
        }

        private void CajaEscribir(string raiz, DateTime ahora, string linea)
        {
            var ruta = Path.Combine(Registro.CarpetaDatos, "caja", "caja-" + raiz + "-" + ahora.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jsonl");
            Registro.Anexar(ruta, linea);
        }
    }
}
