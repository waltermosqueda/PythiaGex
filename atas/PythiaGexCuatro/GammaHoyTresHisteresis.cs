using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

namespace PythiaGexCuatro
{
    /// <summary>
    /// Regla "Tres" (06-10-2026): la clasica (una por lado, empate 20 %) con HISTERESIS y centroide de 6 pts. Es un calco de
    /// laboratorio/tres/familias/F3_estabilidad.py, hacer_regla(modo=lado, radio=100, empate=20, hist_x=25, hist_n=5, perm_min=0,
    /// pos=c6) = variante V22, la unica direccion que mejoro en las DOS muestras del juez pre-registrado (NQ vivo, MNQ m2, rueda):
    ///   diseno (9 dias)        rebote 56,2 % vs placebo 43,2 (+12,9 pp, z 2,53), penetracion 9,4 pts, 3,63 cambios/h
    ///   confirmacion (10 dias) rebote 51,1 % vs placebo 44,0 (+7,1 pp, z 1,47: pide 1,5 -> NO VALIDADA), penetracion 12,9, 2,78/h
    ///   contra la clasica en confirmacion: +1,2 pp (z 0,27), penetracion 22,7, 10,0 cambios/h; la 2.0: +2,4 pp, 16,5, 7,95/h.
    /// Semantica (igual que el laboratorio):
    ///   - un PASO por minuto de reloj (el laboratorio tenia una foto por minuto); entre pasos solo se reubican las vigentes.
    ///   - seleccion de la clasica: candidatas a menos del radio con fuerza &gt; 0 (|gexVol|; |gexOi| si el nucleo eligio OI), la mas
    ///     fuerte de cada lado con empate (gana la mas cercana entre las comparables), lado vacio se completa con la mas fuerte que
    ///     queda, 2 plazas, ordenadas por fuerza.
    ///   - una vigente sigue mientras su strike este en el perfil, a menos del radio y con fuerza &gt; 0; si no, muere en el acto.
    ///   - plaza libre: entra la retadora mas fuerte ya. Plaza ocupada: la retadora compite con la vigente MAS DEBIL y la reemplaza
    ///     solo si la supera por X % durante N pasos seguidos (un paso que falla pone el contador en cero).
    ///   - salida: vigentes por fuerza actual descendente (D1, D2), cada una en el centroide (strikes a menos de R pts, pesados por
    ///     fuerza) del perfil de AHORA. R = 0 -> strike exacto.
    ///   - el estado arranca vacio y se vacia al cambiar de tramo: 09:30 NY (rueda, como corrio el juez) y 18:00 NY (Globex nuevo).
    /// La clave de cada strike es la ESTABLE del nucleo (Strike.Clave: el strike crudo del contrato), para que en la semana del roll
    /// una vigente no "muera" porque el spread le corrio el strike mostrado.
    /// </summary>
    public sealed class Histeresis3
    {
        public sealed class Vigente { public double Clave; public DateTime T0; public int Lado0; public double Fut; }

        public double HistX = 25.0;             // la retadora tiene que superar a la vigente mas debil por este %
        public int HistN = 5;                   // ... durante estos pasos (minutos) seguidos
        public int Plazas = 2;
        public double RadioCentroidePts = 6.0;  // 0 = strike exacto

        private readonly object _llave = new object();
        private List<Vigente> _vig = new List<Vigente>();
        private Dictionary<double, int> _cont = new Dictionary<double, int>();
        private DateTime _ultimoPasoMin = DateTime.MinValue;
        private DateTime _tramoDia = DateTime.MinValue;
        private int _tramo = -1;
        private readonly Action<string> _log;
        public int Pasos { get; private set; }

        private static readonly TimeZoneInfo NY = ZonaNy();
        private static TimeZoneInfo ZonaNy()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
            catch { try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); } catch { return null; } }
        }

        public Histeresis3(Action<string> log) { _log = log; }

        /// <summary>Tramo de sesion de un instante: (dia NY, 0 = madrugada hasta 09:30 / 1 = rueda hasta 18:00 / 2 = Globex nuevo).</summary>
        public static (DateTime Dia, int Tramo) TramoDe(DateTime utc)
        {
            DateTime ny;
            try { ny = NY != null ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), NY) : utc.AddHours(-4); }
            catch { ny = utc.AddHours(-4); }
            var t = ny.TimeOfDay;
            // 3.2.2: dos tramos por sesion, y la medianoche de NY NO corta nada. "dia" = 09:30-18:00 NY (fecha de ese dia); "noche" = desde las
            // 18:00 NY hasta las 09:30 del dia siguiente, con la fecha de la sesion que ARRANCA (la de las 18:00). El 07-10 a las 00:00 NY la
            // version anterior reinicio el estado por cambio de fecha y dejo 2 h una D2 debil (31450, -100M) con el precio 15-35 pts arriba.
            if (t >= new TimeSpan(9, 30, 0) && t < new TimeSpan(18, 0, 0)) return (ny.Date, 1);
            var sesion = t >= new TimeSpan(18, 0, 0) ? ny.Date : ny.Date.AddDays(-1);
            return (sesion, 2);
        }
        private static string NombreTramo(int t) => t == 1 ? "dia 09:30 NY" : "noche desde las 18:00 NY";

        /// <summary>Rueda de Nueva York: 09:30-16:00 NY. La histeresis se midio SOLO ahi (PRE_REGISTRO, ventana rueda 13:30-20:00 UTC).</summary>
        public static bool EsRueda(DateTime utc)
        {
            DateTime ny;
            try { ny = NY != null ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), NY) : utc.AddHours(-4); }
            catch { ny = utc.AddHours(-4); }
            var t = ny.TimeOfDay;
            return ny.DayOfWeek != DayOfWeek.Saturday && ny.DayOfWeek != DayOfWeek.Sunday && t >= new TimeSpan(9, 30, 0) && t < new TimeSpan(16, 0, 0);
        }

        public void Reiniciar(string motivo)
        {
            lock (_llave)
            {
                _vig = new List<Vigente>(); _cont = new Dictionary<double, int>();
                _ultimoPasoMin = DateTime.MinValue; _tramoDia = DateTime.MinValue; _tramo = -1; Pasos = 0;
            }
            _log?.Invoke("hist: estado vacio (" + motivo + ")");
        }

        /// <summary>Aplica la regla sobre la lectura del nucleo y REEMPLAZA L.Doms. radio y empate son los mismos que uso el nucleo
        /// (RadioDominantes / EmpatePct). Devuelve true si este llamado fue un paso (minuto nuevo).</summary>
        public bool Aplicar(GammaHoyNucleo.Lectura L, double radio, double empatePct, DateTime ahoraUtc, bool decidir = true)
        {
            if (L == null || L.Perfil == null || L.Perfil.Count == 0 || L.Futuro <= 0) return false;
            var inv = CultureInfo.InvariantCulture;
            double fut = L.Futuro;
            bool porVol = L.LibroDom != "OI";
            Func<GammaHoyNucleo.Strike, double> peso = x => Math.Abs(porVol ? x.GexVol : x.GexOi);
            var porK = new Dictionary<double, GammaHoyNucleo.Strike>();
            foreach (var x in L.Perfil) porK[x.Clave] = x;
            double Fuerza(double k) => porK.TryGetValue(k, out var s) ? peso(s) : 0.0;
            string F(double p) => p.ToString("0.##", inv);
            string M(double g) => (g / 1e6).ToString("0", inv) + "M";
            var eventos = new List<string>();
            bool paso = false;
            lock (_llave)
            {
                // reinicio por tramo de sesion (09:30 NY y 18:00 NY)
                var tr = TramoDe(ahoraUtc);
                if (_tramo >= 0 && (tr.Dia != _tramoDia || tr.Tramo != _tramo))
                {
                    _vig.Clear(); _cont.Clear();
                    eventos.Add("estado vacio: tramo nuevo (" + NombreTramo(tr.Tramo) + ", " + tr.Dia.ToString("MM-dd", inv) + ")");
                }
                _tramoDia = tr.Dia; _tramo = tr.Tramo;

                var minuto = new DateTime(ahoraUtc.Year, ahoraUtc.Month, ahoraUtc.Day, ahoraUtc.Hour, ahoraUtc.Minute, 0, DateTimeKind.Utc);
                // 3.0.7: con decidir = false (libro a medio armar tras un rearme) no hay paso: las vigentes solo se reubican. Evita la
                // muerte falsa de una vigente porque su strike todavia no tiene puntas (21:08 del 06-10: 31520 "no esta en el perfil").
                if (decidir && minuto > _ultimoPasoMin)
                {
                    paso = true; _ultimoPasoMin = minuto; Pasos++;

                    // (1) la seleccion de la clasica: una por lado + empate, Plazas plazas, por fuerza descendente
                    var cerca = L.Perfil.Where(x => Math.Abs(x.Fut - fut) <= radio && peso(x) > 0).ToList();
                    GammaHoyNucleo.Strike Elegir(IEnumerable<GammaHoyNucleo.Strike> lado)
                    {
                        var l = lado.ToList();
                        if (l.Count == 0) return null;
                        double pmax = l.Max(peso);
                        double piso = pmax * (1.0 - Math.Max(0.0, Math.Min(90.0, empatePct)) / 100.0);
                        return l.Where(x => peso(x) >= piso).OrderBy(x => Math.Abs(x.Fut - fut)).ThenByDescending(peso).First();
                    }
                    var lados = new List<GammaHoyNucleo.Strike>();
                    var arriba = Elegir(cerca.Where(x => x.Fut > fut));
                    var abajo = Elegir(cerca.Where(x => x.Fut <= fut));
                    if (arriba != null) lados.Add(arriba);
                    if (abajo != null) lados.Add(abajo);
                    foreach (var x in cerca.OrderByDescending(peso))
                    {
                        if (lados.Count >= Plazas) break;
                        if (lados.Any(l => l.Fut == x.Fut)) continue;
                        lados.Add(x);
                    }
                    var selK = lados.OrderByDescending(peso).Take(Plazas).Select(x => x.Clave).ToList();

                    // (2) las vigentes vivas siguen (con el Fut de ahora); las demas mueren ya
                    var final = new List<Vigente>();
                    var vivas = new List<Vigente>();
                    foreach (var v in _vig)
                    {
                        if (porK.TryGetValue(v.Clave, out var s) && Math.Abs(s.Fut - fut) <= radio && peso(s) > 0)
                        {
                            var nv = new Vigente { Clave = v.Clave, T0 = v.T0, Lado0 = v.Lado0, Fut = s.Fut };
                            vivas.Add(nv); final.Add(nv);
                        }
                        else
                        {
                            string por = !porK.ContainsKey(v.Clave) ? "no esta en el perfil" : Math.Abs(porK[v.Clave].Fut - fut) > radio ? "fuera del radio" : "sin fuerza";
                            eventos.Add("muere " + F(v.Fut) + " (" + por + ") tras " + ((int)(ahoraUtc - v.T0).TotalMinutes) + " min");
                        }
                    }

                    // (3) las retadoras: plaza libre -> entra ya; plaza ocupada -> contra la vigente mas debil, X % durante N pasos
                    var retadoras = selK.Where(k => !final.Any(v => v.Clave == k)).ToList();
                    int libres = Math.Max(0, Plazas - final.Count);
                    foreach (var k in retadoras.Take(libres))
                    {
                        var s = porK[k];
                        final.Add(new Vigente { Clave = k, T0 = ahoraUtc, Lado0 = fut >= s.Fut ? 1 : -1, Fut = s.Fut });
                        eventos.Add("entra " + F(s.Fut) + " (" + M(peso(s)) + ", plaza libre)");
                    }
                    var debiles = vivas.OrderBy(v => Fuerza(v.Clave)).ToList();
                    var nuevoCont = new Dictionary<double, int>();
                    foreach (var k in retadoras.Skip(libres))
                    {
                        if (debiles.Count == 0) break;
                        var w = debiles[0];
                        double fk = Fuerza(k), fw = Fuerza(w.Clave);
                        nuevoCont[k] = fk > fw * (1.0 + HistX / 100.0) ? (_cont.TryGetValue(k, out int c0) ? c0 : 0) + 1 : 0;
                        if (nuevoCont[k] >= HistN)
                        {
                            final.Remove(w); debiles.RemoveAt(0); nuevoCont.Remove(k);
                            var s = porK[k];
                            final.Add(new Vigente { Clave = k, T0 = ahoraUtc, Lado0 = fut >= s.Fut ? 1 : -1, Fut = s.Fut });
                            eventos.Add(F(s.Fut) + " (" + M(fk) + ") reemplaza a " + F(w.Fut) + " (" + M(fw) + ") tras " + HistN + " min con mas de " + HistX.ToString("0.#", inv) + " %");
                        }
                        else if (nuevoCont[k] > 0) eventos.Add("retadora " + F(porK[k].Fut) + " (" + M(fk) + ") " + nuevoCont[k] + "/" + HistN + " contra " + F(w.Fut) + " (" + M(fw) + ")");
                    }
                    _vig = final; _cont = nuevoCont;
                }

                // (4) la salida: vigentes por fuerza ACTUAL, en el centroide del perfil de ahora
                var doms = new List<(double Fut, double Gex)>();
                // 3.0.9: D1 = la vigente mas CERCANA al precio (antes la mas fuerte); los niveles son los mismos
                foreach (var v in _vig.OrderBy(v => Math.Abs((porK.TryGetValue(v.Clave, out var sv) ? sv.Fut : v.Fut) - fut)))
                {
                    porK.TryGetValue(v.Clave, out var s);
                    double Fut = s != null ? s.Fut : v.Fut;
                    double precio = Fut;
                    if (RadioCentroidePts > 0)
                    {
                        double sw = 0, sx = 0;
                        foreach (var q in L.Perfil)
                        {
                            if (Math.Abs(q.Fut - Fut) > RadioCentroidePts) continue;
                            double w = peso(q); if (w <= 0) continue;
                            sw += w; sx += w * q.Fut;
                        }
                        if (sw > 0) precio = sx / sw;
                    }
                    doms.Add((precio, s != null ? (porVol ? s.GexVol : s.GexOi) : 0.0));
                }
                L.Doms = doms;
            }
            if (_log != null) foreach (var e in eventos) _log("hist: " + e);
            return paso;
        }

        /// <summary>Resumen de una linea para el AUDIT3: vigentes con su hora de entrada, contadores de las retadoras, pasos.</summary>
        /// <summary>Desde cuando es vigente el nivel que esta (a 0,01) en ese precio; null si no es vigente. Para la caja negra (3.1.0).</summary>
        public DateTime? DesdeCuando(double fut)
        {
            lock (_llave) { foreach (var v in _vig) if (Math.Abs(v.Fut - fut) <= 0.01) return v.T0; }
            return null;
        }

        public string Resumen()
        {
            var inv = CultureInfo.InvariantCulture;
            lock (_llave)
            {
                string vig = _vig.Count == 0 ? "-" : string.Join(",", _vig.Select(v => v.Fut.ToString("0.##", inv) + "@" + v.T0.ToString("HH:mm", inv) + "Z"));
                string cont = _cont.Count == 0 ? "-" : string.Join(",", _cont.Select(kv => kv.Key.ToString("0.##", inv) + ":" + kv.Value));
                return "vig=" + vig + " cont=" + cont + " pasos=" + Pasos + " X=" + HistX.ToString("0.#", inv) + " N=" + HistN + " c=" + RadioCentroidePts.ToString("0.#", inv);
            }
        }

        /// <summary>Foto estructurada del estado (3.2.0, para indicador.json de Profundidad 3.0): vigentes con su hora de entrada,
        /// contadores de las retadoras y pasos. Solo lectura; copia bajo la llave.</summary>
        public (List<(double Fut, DateTime T0)> Vig, List<(double Clave, int N)> Cont, int Pasos) Foto()
        {
            lock (_llave)
            {
                return (_vig.Select(v => (v.Fut, v.T0)).ToList(), _cont.Select(kv => (kv.Key, kv.Value)).ToList(), Pasos);
            }
        }

        /// <summary>Toma el estado de otra instancia (la del rebobinado de la memoria) si esta casi no camino todavia (hasta 3 pasos:
        /// los primeros pasos desde vacio son la clasica a secas) y la otra termino hace menos de una hora en el MISMO tramo.</summary>
        public bool AdoptarDe(Histeresis3 otra, DateTime ahoraUtc)
        {
            if (otra == null || ReferenceEquals(otra, this)) return false;
            lock (otra._llave)
            {
                lock (_llave)
                {
                    if (Pasos > 3 || otra._vig.Count == 0 || otra._ultimoPasoMin == DateTime.MinValue) return false;
                    if ((ahoraUtc - otra._ultimoPasoMin).TotalMinutes > 60) return false;
                    var a = TramoDe(ahoraUtc); var b = TramoDe(otra._ultimoPasoMin);
                    if (a.Dia != b.Dia || a.Tramo != b.Tramo) return false;
                    _vig = otra._vig.Select(v => new Vigente { Clave = v.Clave, T0 = v.T0, Lado0 = v.Lado0, Fut = v.Fut }).ToList();
                    _cont = new Dictionary<double, int>(otra._cont);
                    _ultimoPasoMin = otra._ultimoPasoMin; Pasos = otra.Pasos; _tramoDia = otra._tramoDia; _tramo = otra._tramo;
                    return true;
                }
            }
        }
    }

    public partial class FamiliaCuatro
    {
        [Display(Name = "Histeresis: la retadora supera a la vigente por (%)", GroupName = "9.2 3.0 · Lectura", Order = 1012,
                 Description = "Solo con la regla Tres. 25 con 5 min = V22 del laboratorio (confirmacion +7,1 pp, z 1,47: sin validar). 50 con 10 min y centroide 0 = V14 (indistinguible en confirmacion).")]
        [Range(0, 500)]
        public decimal Histeresis3Pct { get; set; } = 25m;

        [Display(Name = "Histeresis: minutos seguidos", GroupName = "9.2 3.0 · Lectura", Order = 1013,
                 Description = "Solo con la regla Tres. Cuantos minutos seguidos tiene que ganar la retadora para reemplazar a la vigente mas debil.")]
        [Range(1, 120)]
        public int Histeresis3Min { get; set; } = 5;

        [Display(Name = "Histeresis: centroide (pts, 0 = strike exacto)", GroupName = "9.2 3.0 · Lectura", Order = 1014,
                 Description = "Solo con la regla Tres. La raya va al promedio de los strikes a menos de esta distancia del elegido, pesado por fuerza. 6 = V22.")]
        [Range(0, 50)]
        public decimal Histeresis3CentroidePts { get; set; } = 6m;

        private Histeresis3 _hist, _histMemoria;
        private Histeresis3 Hist => _hist ??= new Histeresis3(m => Log(m));
        private Histeresis3 HistMemoria => _histMemoria ??= new Histeresis3(null);

        private double RadioDominantesEfectivo(double futuro)
        {
            var a = _nucleo.A;
            double radio = futuro * a.RadioDominantesPct / 100.0;
            if (a.RadioDominantesMaxPts > 0) radio = Math.Min(radio, a.RadioDominantesMaxPts);
            return radio;
        }

        /// <summary>Con la regla Tres, pasa la lectura por la histeresis (la del vivo o la del rebobinado) y reemplaza L.Doms.</summary>
        private bool _avisoLibroIncompleto, _avisoFueraDeRueda;
        /// <summary>Con la regla Tres, la histeresis vale SOLO en la rueda de NY (donde se midio). Fuera de la rueda la cuenta del nucleo ya es la
        /// clasica (una por lado + centroide 12: ver AplicarAjustes) y se deja tal cual, ordenada por cercania.</summary>
        private bool HisteresisActiva(DateTime ahoraUtc) => Histeresis3.EsRueda(ahoraUtc);
        private void AplicarRegla(GammaHoyNucleo.Lectura L, DateTime ahoraUtc, bool memoria, bool decidir = true)
        {
            if (L == null || ReglaEfectiva() != ReglaDominantes3.Tres) return;
            if (!HisteresisActiva(ahoraUtc))
            {
                if (!memoria && !_avisoFueraDeRueda) { _avisoFueraDeRueda = true; Log("hist: fuera de la rueda de NY: regla Tres = clasica a secas (una por lado + centroide 12); la histeresis vuelve a las 09:30 NY"); }
                if (L.Doms.Count > 1) L.Doms = L.Doms.OrderBy(d => Math.Abs(d.Fut - L.Futuro)).ToList();
                return;
            }
            if (!memoria && _avisoFueraDeRueda) { _avisoFueraDeRueda = false; Log("hist: rueda de NY: la histeresis vuelve a decidir"); }
            var h = memoria ? HistMemoria : Hist;
            h.HistX = (double)Math.Max(0m, Histeresis3Pct); h.HistN = Math.Max(1, Histeresis3Min); h.RadioCentroidePts = Rayas3Independientes ? 0.0 : (double)Math.Max(0m, Histeresis3CentroidePts);   // 3.5.0
            if (!memoria && decidir != !_avisoLibroIncompleto) { _avisoLibroIncompleto = !decidir; Log(decidir ? "hist: libro completo: el portero vuelve a decidir" : "hist: libro a medio armar (menos del 60 % con puntas o rearmado hace < 90 s): el portero no decide, solo reubica"); }
            h.Aplicar(L, RadioDominantesEfectivo(L.Futuro), _nucleo.A.EmpatePct, ahoraUtc, decidir);
        }

        /// <summary>3.0.7: el libro vivo se considera completo 90 s despues del ultimo armado y con al menos 60 % de los contratos con puntas.</summary>
        private static bool LibroCompleto(CadenaApi.Estado f, DateTime ahora)
            => f != null && f.UltimoArmadoUtc != DateTime.MinValue && (ahora - f.UltimoArmadoUtc).TotalSeconds >= 90 && f.Suscritos > 0 && f.ConPuntas >= 0.6 * f.Suscritos;

        private string ReglaTexto() => ReglaEfectiva() == ReglaDominantes3.Tres ? (Tunel3DeNoche && !Histeresis3.EsRueda(DateTime.UtcNow) ? "Tres (tunel de noche)" : "Tres (histeresis) · SIN VALIDAR") : ReglaEfectiva().ToString();
    }
}
