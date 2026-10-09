using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using ATAS.Indicators;

namespace PythiaGexTres
{
    /// <summary>
    /// LA MEMORIA DE GAMMA HOY 3.0 (06-10-2026, pedido del operador: "cuando reinicio o recargo el grafico o lo cambio de
    /// temporalidad se borra todo lo anterior dibujado y los anteriores si tenian memoria").
    ///
    /// La clasica y la 2.0 vuelven a poner su estela al arrancar (GammaHoyCapas.CargarEstelaGuardada) y rebobinan el archivo de
    /// la nube. La 3.0 escribia su estela en disco pero no la releia, y ATAS crea el indicador de cero en cada cambio de marco.
    /// Aca, una vez que el grafico tiene velas, en un hilo aparte:
    ///   1. se leen los puntos de la estela guardada (hoy y ayer, UTC): es EXACTAMENTE lo que se dibujo;
    ///   2. se REBOBINAN las fotos viva3 (hoy y ayer) con la MISMA cuenta (GammaHoyNucleo, mismos ajustes) para rellenar los
    ///      minutos en que la 3.0 no estaba en pantalla pero la sonda o el otro grafico seguian grabando el libro;
    ///   3. a cada vela cerrada sin guion se le asigna el ultimo punto conocido ANTES de su cierre (vida 5 min, como el juez
    ///      del laboratorio: sin foto fresca el nivel no existe) y se rehacen las marcas de toque con la misma regla del vivo.
    /// Nada mira adelante: la vela recibe solo puntos anteriores a su cierre. Lo que no tiene dato queda sin guion.
    /// </summary>
    public partial class GammaHoyTres
    {
        private const double VIDA_MEMORIA_S = 300;        // un punto vale hasta 5 min despues (libro vivo), como VIDA_S del laboratorio
        private int _memoriaIntentos;                      // dos pasadas: al arrancar y 2 min despues (ATAS termina de cargar historia)
        private bool _memoriaEnCurso;
        private DateTime _memoriaUltimoIntento = DateTime.MinValue;
        private readonly GammaHoyNucleo _nucleoMemoria = new GammaHoyNucleo();

        private struct VelaMem { public int B; public DateTime TOpen, TClose; public double O, H, L, C; }
        private struct PuntoMem { public DateTime T; public double D1, D2, Zero; public bool DeArchivo; public double[] CrucesOi; public double MpOi, MnOi; public double[] Formulas; public double Bs; }

        /// <summary>Desde el temporizador: lanza la siembra cuando hay velas y no se hizo (o para la segunda pasada).</summary>
        private void SembrarMemoriaSiHaceFalta(string raiz)
        {
            try
            {
                if (_memoriaEnCurso || _memoriaIntentos >= 2 || string.IsNullOrEmpty(raiz)) return;
                int ult = CurrentBar - 2;
                if (ult < 5) return;
                var ahora = DateTime.UtcNow;
                if (_memoriaIntentos == 1 && (ahora - _memoriaUltimoIntento).TotalSeconds < 120) return;
                _memoriaIntentos++; _memoriaUltimoIntento = ahora; _memoriaEnCurso = true;

                // la foto de las velas se toma aca (hilo del temporizador, como el resto del indicador); el trabajo pesado va aparte
                var velas = new List<VelaMem>(Math.Min(ult + 1, 6000));
                int desde = Math.Max(0, ult - 6000 + 1);
                for (int b = desde; b <= ult; b++)
                {
                    IndicatorCandle c; try { c = GetCandle(b); } catch { continue; }
                    if (c == null) continue;
                    DateTime tOpen = Utc(c.Time), tClose;
                    try { var sig = GetCandle(b + 1); tClose = sig != null ? Utc(sig.Time) : tOpen.AddMinutes(1); } catch { tClose = tOpen.AddMinutes(1); }
                    if (tClose <= tOpen) tClose = tOpen.AddSeconds(1);
                    velas.Add(new VelaMem { B = b, TOpen = tOpen, TClose = tClose, O = (double)c.Open, H = (double)c.High, L = (double)c.Low, C = (double)c.Close });
                }
                var a = _nucleoMemoria.A; var src = _nucleo.A;
                a.Horizonte = src.Horizonte; a.CuantasDominantes = src.CuantasDominantes; a.RadioDominantesPct = src.RadioDominantesPct;
                a.RadioDominantesMaxPts = src.RadioDominantesMaxPts; a.EmpatePct = src.EmpatePct; a.DominantesDeNoche = src.DominantesDeNoche;
                a.ZeroInterpolado = src.ZeroInterpolado; a.UnaPorLado = src.UnaPorLado; a.Centroide = src.Centroide; a.RadioCentroidePts = src.RadioCentroidePts;
                double zona = ZonaToque(raiz), lejos = Lejos(raiz);
                int intento = _memoriaIntentos;
                _ = Task.Run(() => SembrarMemoria(raiz, velas, zona, lejos, intento));
            }
            catch (Exception e) { _memoriaEnCurso = false; Registro.Excepcion(LOG, "SembrarMemoriaSiHaceFalta", e); }
        }

        private void SembrarMemoria(string raiz, List<VelaMem> velas, double zona, double lejos, int intento)
        {
            try
            {
                if (velas.Count == 0) return;
                var inv = CultureInfo.InvariantCulture;
                DateTime primera = velas[0].TOpen, ultima = velas[velas.Count - 1].TClose;
                var dias = new List<DateTime>();
                for (var d = primera.Date.AddDays(-1); d <= ultima.Date; d = d.AddDays(1)) dias.Add(d);

                // 1) la estela guardada (lo dibujado)
                var puntos = LeerEstelaGuardada(raiz, dias);
                int deArchivo = puntos.Count;
                SembrarMemoriaCapas(velas, dias);   // las capas QQQ/NDX vuelven desde su propia estela guardada

                // 2) el rebobinado de viva3 (la misma cuenta sobre las fotos del libro), solo donde la estela no llega
                int fotos = 0, rebobinados = 0;
                var viva = RebobinarViva3(raiz, dias, velas, ref fotos);
                // 3.7.0 C1: el salto de OI que vio el rebobinado (fotos de viva3 de esta noche) lo toma el vivo si es de la misma sesion
                if (_saltoOi.AdoptarDe(_saltoOiMemoria))
                {
                    Log("memoria: OI NQ: el vivo adopta del rebobinado " + _saltoOi.Serializar());
                    try { if (_saltoOi.SaltoUtc != null) File.WriteAllText(RutaSaltoOi(raiz), _saltoOi.Serializar()); } catch { }
                }
                SembrarFormulasDesde(viva, velas);   // 3.6.1: formulas, majors OI y cruces OI desde TODAS las fotos
                SembrarMajorOiArchivo(puntos, velas);   // 3.6.2: donde no hubo foto, los majors guardados en la estela
                if (ReglaEfectiva() == ReglaDominantes3.Tres)
                {
                    // el estado de la histeresis sigue desde donde lo dejo el rebobinado (si el vivo casi no camino y es el mismo tramo)
                    if (Hist.AdoptarDe(HistMemoria, DateTime.UtcNow)) Log("memoria: histeresis adoptada del rebobinado: " + Hist.Resumen());
                    else Log("memoria: histeresis NO adoptada (vivo con pasos, rebobinado vacio, viejo o de otro tramo): vivo " + Hist.Resumen() + " | rebobinado " + HistMemoria.Resumen());
                }
                if (viva.Count > 0)
                {
                    var tEst = puntos.Select(p => p.T).OrderBy(t => t).ToList();
                    foreach (var p in viva)
                    {
                        // hay un punto de archivo a menos de 90 s? entonces el archivo manda
                        int i = tEst.BinarySearch(p.T); if (i < 0) i = ~i;
                        bool cerca = (i < tEst.Count && (tEst[i] - p.T).TotalSeconds <= 90) || (i > 0 && (p.T - tEst[i - 1]).TotalSeconds <= 90);
                        if (!cerca) { puntos.Add(p); rebobinados++; }
                    }
                }
                puntos.Sort((x, y) => x.T.CompareTo(y.T));
                if (puntos.Count == 0) { Log("memoria (pasada " + intento + "): sin estela guardada ni fotos viva3 de " + string.Join(",", dias.Select(d => d.ToString("MM-dd", inv))) + ": nada que sembrar"); return; }

                // 3) cada vela cerrada sin guion recibe el ultimo punto ANTERIOR a su cierre (vida 5 min)
                var tiempos = puntos.Select(p => p.T).ToList();
                var sembradas = new Dictionary<DateTime, double[]>();
                var sembradasOi = new Dictionary<DateTime, double[]>();   // 3.5.2
                var toques = new Dictionary<DateTime, List<(double Nivel, bool Arriba)>>();
                var armado = new Dictionary<(int Dom, int Lado), bool>();
                HashSet<DateTime> yaHay; lock (_candado) { yaHay = new HashSet<DateTime>(_estela.Keys); }
                VelaMem? prev = null; DateTime primeraSembrada = DateTime.MaxValue, ultimaSembrada = DateTime.MinValue; int nToques = 0;
                foreach (var v in velas)
                {
                    int i = tiempos.BinarySearch(v.TClose); if (i < 0) i = ~i; i--;   // ultimo punto con T <= TClose
                    if (i >= 0 && (v.TClose - tiempos[i]).TotalSeconds <= VIDA_MEMORIA_S && !yaHay.Contains(v.TOpen))
                    {
                        var p = puntos[i];
                        sembradas[v.TOpen] = new[] { p.D1, p.D2, p.Zero };
                        if (p.CrucesOi != null) sembradasOi[v.TOpen] = p.CrucesOi;   // 3.5.2
                        if (v.TOpen < primeraSembrada) primeraSembrada = v.TOpen;
                        if (v.TOpen > ultimaSembrada) ultimaSembrada = v.TOpen;
                        // los toques, con la MISMA regla que RegistrarVelaCerrada (primera vela que entra en la banda; un toque por
                        // dominante y lado hasta que un cierre se aleje LEJOS)
                        if (prev.HasValue)
                        {
                            var pv = prev.Value; List<(double, bool)> lista = null;
                            var doms = new[] { p.D1, p.D2 };
                            for (int k = 0; k < doms.Length; k++)
                            {
                                double d = doms[k];
                                if (double.IsNaN(d) || d <= 0) continue;
                                bool enZona = v.L <= d + zona && v.H >= d - zona;
                                bool prevEnZona = pv.L <= d + zona && pv.H >= d - zona;
                                if (enZona && !prevEnZona)
                                {
                                    int lado = pv.C > d ? +1 : -1;
                                    if (!armado.TryGetValue((k, lado), out bool arm) || arm) { lista ??= new List<(double, bool)>(); lista.Add((d, lado == -1)); armado[(k, lado)] = false; }
                                }
                                if (v.C >= d + lejos) armado[(k, +1)] = true;
                                if (v.C <= d - lejos) armado[(k, -1)] = true;
                            }
                            if (lista != null) { toques[v.TOpen] = lista; nToques += lista.Count; }
                        }
                    }
                    prev = v;
                }

                lock (_candado)
                {
                    foreach (var kv in sembradas) if (!_estela.ContainsKey(kv.Key)) _estela[kv.Key] = kv.Value;
                    foreach (var kv in sembradasOi) if (!_estelaCrucesOi.ContainsKey(kv.Key)) _estelaCrucesOi[kv.Key] = kv.Value;   // 3.5.2
                    foreach (var kv in toques) if (!_toques.ContainsKey(kv.Key)) _toques[kv.Key] = kv.Value;
                }
                lock (_llaveVelas)
                {
                    // si el vivo todavia no anoto nada, el estado de rearme sigue desde la memoria y la proxima vela cerrada es la siguiente
                    if (_ultimaVelaUtc == DateTime.MinValue && ultimaSembrada != DateTime.MinValue)
                    {
                        foreach (var kv in armado) _toqueArmado[kv.Key] = kv.Value;
                        _ultimaVelaUtc = ultimaSembrada;
                    }
                }
                Log("memoria (pasada " + intento + "): " + deArchivo + " puntos de la estela guardada + " + rebobinados + " rebobinados de " + fotos + " fotos viva3"
                    + " -> " + sembradas.Count + " velas sembradas" + (sembradas.Count > 0 ? " (" + primeraSembrada.ToString("MM-dd HH:mm", inv) + "Z .. " + ultimaSembrada.ToString("MM-dd HH:mm", inv) + "Z)" : "")
                    + ", " + nToques + " toques, " + (velas.Count - sembradas.Count - yaHay.Count) + " velas sin dato de las " + velas.Count);
                try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            }
            catch (Exception e) { Registro.Excepcion(LOG, "SembrarMemoria", e); }
            finally { _memoriaEnCurso = false; }
        }

        private int _estelaDescartada;

        /// <summary>3.7.2: true si la linea de estela la escribio una version con las reglas de dibujo 3.7.2 o posterior ('v' &gt;= "3.7.2").</summary>
        private static bool EstelaDeReglasNuevas(JsonElement r)
        {
            if (!r.TryGetProperty("v", out var v) || v.ValueKind != JsonValueKind.String) return false;
            var p = (v.GetString() ?? "").Split('.');
            if (p.Length < 3 || !int.TryParse(p[0], out int a) || !int.TryParse(p[1], out int b) || !int.TryParse(p[2], out int c)) return false;
            return a > 3 || (a == 3 && (b > 7 || (b == 7 && c >= 2)));
        }

        /// <summary>Los puntos de estela-&lt;raiz&gt;-&lt;dia&gt;.jsonl: {"t":"...Z","d":[D1,D2,zero],...}. 0 = no habia. 3.7.2: solo las lineas con 'v' &gt;= 3.7.2.</summary>
        private List<PuntoMem> LeerEstelaGuardada(string raiz, List<DateTime> dias)
        {
            _estelaDescartada = 0;
            var out_ = new List<PuntoMem>();
            var inv = CultureInfo.InvariantCulture;
            foreach (var dia in dias)
            {
                var ruta = Path.Combine(Registro.CarpetaDatos, "estela", "estela-" + raiz + "-" + dia.ToString("yyyy-MM-dd", inv) + ".jsonl");
                if (!File.Exists(ruta)) continue;
                string[] lineas; try { lineas = File.ReadAllLines(ruta); } catch (Exception e) { Log("memoria: no pude leer " + ruta + ": " + e.Message); continue; }
                foreach (var l in lineas)
                {
                    if (l.Length < 20) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(l);
                        var r = doc.RootElement;
                        if (!DateTime.TryParseExact(r.GetProperty("t").GetString(), "yyyy-MM-dd'T'HH:mm:ss'Z'", inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)) continue;
                        // 3.7.2 (escéptico 08-10): la estela de PythiaGex3 la escribio la 3.6.9 (otras reglas: tunel por cruces, una por lado en las
                        // capas, M± OI a 265 pts). Solo se siembra lo escrito con reglas 3.7.2 o posteriores ('v'); el resto lo completa el rebobinado
                        // de viva3 con las reglas nuevas (NQ) y, en las capas, queda vacio hasta que la capa vuelva a anotar.
                        if (!EstelaDeReglasNuevas(r)) { _estelaDescartada++; continue; }
                        var d = r.GetProperty("d"); var v = new List<double>();
                        foreach (var x in d.EnumerateArray()) v.Add(x.GetDouble());
                        while (v.Count < 3) v.Add(0);
                        double mp = 0, mn = 0;   // 3.6.2: majors por OI guardados ('m')
                        double bs = r.TryGetProperty("bs", out var bsv) && bsv.ValueKind == JsonValueKind.Number ? bsv.GetDouble() : double.NaN;   // 3.6.6
                        if (r.TryGetProperty("m", out var mm) && mm.ValueKind == JsonValueKind.Array) { int q = 0; foreach (var x in mm.EnumerateArray()) { if (q == 0) mp = x.GetDouble(); else if (q == 1) mn = x.GetDouble(); q++; } }
                        out_.Add(new PuntoMem { T = t, D1 = v[0] > 0 ? v[0] : double.NaN, D2 = v[1] > 0 ? v[1] : double.NaN, Zero = v[2] > 0 ? v[2] : double.NaN, DeArchivo = true, MpOi = mp, MnOi = mn, Bs = bs });
                    }
                    catch { }
                }
            }
            if (_estelaDescartada > 0) Log("memoria " + raiz + ": " + _estelaDescartada + " lineas de estela de una version anterior a la 3.7.2 (sin 'v') NO se siembran; quedan " + out_.Count);
            return out_;
        }

        /// <summary>Rebobina viva3-&lt;raiz&gt;-&lt;dia&gt;.jsonl con la misma cuenta del vivo: una cadena por foto, el precio del grafico de ese
        /// minuto como futuro (si no hay vela, el futuro de la foto), Calcular(c, fut, ts). Devuelve un punto por foto con cuenta.</summary>
        private List<PuntoMem> RebobinarViva3(string raiz, List<DateTime> dias, List<VelaMem> velas, ref int fotos)
        {
            var out_ = new List<PuntoMem>();
            var inv = CultureInfo.InvariantCulture;
            var tOpen = velas.Select(v => v.TOpen).ToList();
            _nucleoMemoria.Reiniciar();
            HistMemoria.Reiniciar("rebobinado");   // regla Tres: la histeresis se rebobina foto a foto (3.0.4)
            _saltoOiMemoria = new SaltoOi37();     // 3.7.0 C1: el salto de OI se busca tambien en las fotos rebobinadas (en orden de tiempo)
            _tunelStM.Techo = double.NaN; _tunelStM.Piso = double.NaN;
            // paridad C#/Python (3.0.4): cada foto rebobinada con la regla Tres queda escrita en PythiaGex3\rebobinado\hist-<raiz>-<dia>-<sello>.jsonl
            string selloReb = DateTime.UtcNow.ToString("HHmmss", inv);
            foreach (var dia in dias.OrderBy(d => d))
            {
                var ruta = Path.Combine(Viva3.Carpeta, "viva3-" + raiz + "-" + dia.ToString("yyyy-MM-dd", inv) + ".jsonl");
                if (!File.Exists(ruta)) continue;
                string[] lineas; try { lineas = File.ReadAllLines(ruta); } catch (Exception e) { Log("memoria: no pude leer " + ruta + ": " + e.Message); continue; }
                foreach (var l in lineas)
                {
                    if (l.Length < 60) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(l);
                        var r = doc.RootElement;
                        if (!DateTime.TryParseExact(r.GetProperty("ts").GetString(), "yyyy-MM-dd HH:mm:ss", inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var ts)) continue;
                        double futFoto = r.GetProperty("futuro").GetDouble();
                        fotos++;
                        // la cadena, como DesdeApi: una Fila por (K, vencimiento), call y put juntos, solo filas con IV y con OI o volumen
                        var filas = new List<double[]>();
                        foreach (var x in r.GetProperty("filas").EnumerateArray())
                        {
                            var f = new double[12]; int i = 0;
                            foreach (var y in x.EnumerateArray()) { if (i < 12) f[i] = y.GetDouble(); i++; }
                            if (i >= 8) filas.Add(f);
                        }
                        // 3.7.0 C1: la foto al detector del salto de OI (clave = strike|vencimiento|lado, como backtest_familia.oi_nq_viejo_hasta)
                        var mapaOi = new Dictionary<string, double>();
                        foreach (var f in filas) mapaOi[f[0].ToString("0.##", inv) + "|" + ts.AddDays(f[1]).ToString("MM-dd", inv) + "|" + (f[2] >= 0.5 ? "C" : "P")] = f[3];
                        _saltoOiMemoria.Observar(ts, mapaOi);
                        bool oiOk = !_saltoOiMemoria.OiDeAnteayer(ts, out _);
                        var diasV = filas.Where(f => f[4] > 0).Select(f => Math.Round(f[1], 4)).Distinct().OrderBy(x => x).ToList();
                        if (diasV.Count == 0) continue;
                        var idx = new Dictionary<double, int>(); for (int i = 0; i < diasV.Count; i++) idx[diasV[i]] = i;
                        var porClave = new Dictionary<(double, int), Feed.Fila>();
                        foreach (var f in filas)
                        {
                            double K = f[0], iv = f[4], oi = f[3], vol = f[7]; bool call = f[2] >= 0.5;
                            if (iv <= 0 || (oi <= 0 && vol <= 0)) continue;
                            if (!idx.TryGetValue(Math.Round(f[1], 4), out int v)) continue;
                            if (!porClave.TryGetValue((K, v), out var fila)) { fila = new Feed.Fila { K = K, K0 = f.Length > 11 ? f[11] : 0, V = v }; porClave[(K, v)] = fila; }
                            if (call) { fila.OiC = oi; fila.IvC = iv; fila.VolC = vol; } else { fila.OiP = oi; fila.IvP = iv; fila.VolP = vol; }
                        }
                        int utiles = porClave.Values.Where(x => x.IvC > 0 && x.IvP > 0).Select(x => x.K).Distinct().Count();
                        if (utiles < MIN_STRIKES_UTILES) continue;
                        var c = new Feed.Cadena
                        {
                            Ts = ts.ToString("yyyy-MM-dd HH:mm:ss", inv), SpotIdx = futFoto, Dias = diasV.ToArray(),
                            Filas = porClave.Values.OrderBy(x => x.K).ThenBy(x => x.V).ToList(),
                            Base = 0, BaseConfiable = true, EdadMin = 0, UltimoTrade = "", HorizonteCadena = diasV[diasV.Count - 1],
                            RecibidoUtc = ts, GeneradoUtc = ts, EsFuturo = true, Fuente = raiz + " vivo (API, rebobinado)",
                            Multiplicador = Feed.MultiplicadorDe(raiz),   // 3.7.0 C4
                        };
                        // el precio del GRAFICO en ese minuto (la vela abierta a ts), como en el vivo; si no hay vela, el futuro de la foto
                        double fut = futFoto;
                        int j = tOpen.BinarySearch(ts); if (j < 0) j = ~j - 1;
                        if (j >= 0 && j < velas.Count && (ts - velas[j].TOpen).TotalMinutes <= 30) fut = velas[j].C;
                        if (fut <= 0) continue;
                        var L = _nucleoMemoria.Calcular(c, fut, ts);
                        if (L == null || L.SinBase) continue;
                        SeleccionarDoms(L, fut, fut, ts, raiz, true, true, oiOk);   // 3.7.0: la MISMA seleccion que el vivo (D2), con el OI de esa hora (C1)
                        if (ReglaEfectiva() == ReglaDominantes3.Tres)
                        {
                            string D(int i) => L.Doms.Count > i && L.Doms[i].Fut > 0 ? L.Doms[i].Fut.ToString("0.00", inv) : "0";
                            Registro.Anexar(Path.Combine(Registro.CarpetaDatos, "rebobinado", "hist-" + raiz + "-" + ts.ToString("yyyy-MM-dd", inv) + "-" + selloReb + ".jsonl"),
                                "{\"t\":\"" + ts.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", inv) + "\",\"f\":" + fut.ToString("0.00", inv) + ",\"d\":[" + D(0) + "," + D(1) + "],\"b\":\"" + (L.LibroDom ?? "vol") + "\",\"n\":" + L.Perfil.Count + "}");
                        }
                        out_.Add(new PuntoMem
                        {
                            T = ts,
                            D1 = L.Doms.Count > 0 && L.Doms[0].Fut > 0 ? L.Doms[0].Fut : double.NaN,
                            D2 = L.Doms.Count > 1 && L.Doms[1].Fut > 0 ? L.Doms[1].Fut : double.NaN,
                            Zero = !double.IsNaN(L.ZeroVol) && L.ZeroVol > 0 ? L.ZeroVol : double.NaN,
                            DeArchivo = false,
                            CrucesOi = oiOk && L.ZeroCrucesOiLista != null && L.ZeroCrucesOiLista.Count > 0 ? L.ZeroCrucesOiLista.ToArray() : null,   // 3.5.2; 3.7.0 C1
                            MpOi = !oiOk || double.IsNaN(L.MpOi) ? 0 : L.MpOi, MnOi = !oiOk || double.IsNaN(L.MnOi) ? 0 : L.MnOi,   // 3.6.1; 3.7.0 C1
                            Formulas = NivelesTodas(L, fut, fut, raiz, oiOk),   // 3.6.1; 3.7.0 C1
                        });
                    }
                    catch (Exception e) { Registro.Excepcion(LOG, "RebobinarViva3 linea", e); }
                }
            }
            return out_;
        }
    }
}
