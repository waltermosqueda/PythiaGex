using System;
using System.Collections.Generic;
using System.Linq;

namespace PythiaGex
{
    /// <summary>
    /// GATILLO REBOTE (puro, sin ATAS): la entrada que el operador toma a ojo en las rayas
    /// amarillas, escrita como regla para que se dibuje, se registre y se juzgue. Es la misma
    /// logica que laboratorio/gatillo_rebote.py: si se cambia algo aca hay que cambiarlo alla.
    ///
    /// NIVELES: todas las dominantes que hubo en la rueda hasta ahora (cada una queda como fila
    /// de guiones amarillos aunque la dominante ya se haya ido), el zero gamma y los majors. La
    /// identidad de una fila es su strike (Paso: 10 en NQ, 5 en ES); el valor es el exacto.
    ///
    /// ENTRADA LARGA (la corta es espejo):
    ///   1. TOQUE: el minimo de la vela llega a L + Tol o lo traspasa hasta L - Pen como maximo.
    ///   2. VENIA DE ARRIBA: el maximo de las Atras velas previas estuvo >= L + Ret (es un
    ///      retroceso al nivel, no una ruptura desde abajo).
    ///   3. CIERRA BIEN: la vela cierra por encima de L (el nivel aguanto al cierre).
    ///   4. PRIMERA: la vela anterior no cumplia 1+3, y no hubo disparo en ese nivel y lado en
    ///      las ultimas Enfriamiento velas. Si varios niveles cumplen, el mas cercano al extremo.
    /// Entrada = cierre de esa vela. Dz lleva el numero de toque del nivel en el dia.
    ///
    /// MEDIDO el 2026-09-10 (MNQ 1 min, 16 dias, contra los mismos niveles corridos +-85 y
    /// +-145 pts; acierto = +20 antes que -20 en 20 min desde el cierre):
    ///   - Todos: 102 disparos por dia, 45 % (placebo 46 %). Solo a favor del zero (largo con el
    ///     precio sobre el zero, corto debajo): 51 por dia, 44 % (42 %). Con +10/-10: 47 % (47 %).
    ///   - Primer toque de la dominante ACTUAL a favor del zero: 3,2 por dia, 52 casos, 50 %
    ///     (placebo 42 %), con +10/-10 60 % (39 %); las dos mitades de los dias 50 / 50; pero en
    ///     MES 36 % (40 %): es el unico bolsillo que asomo entre 20 tablas, hipotesis y no señal.
    ///   - En velas de 2 y 5 min no mejora (43 % y 40 %, igual al placebo).
    ///   - Equivalencia con el laboratorio (rebote_equivalencia.py): 28 de 30 disparos iguales al
    ///     minuto el 10-09; el resto difiere por una vela.
    /// Los 7 ejemplos del operador de ese dia disparan todos con Todos; 5 de 7 con SoloTendencia.
    /// Lo que gana en esos ejemplos es el dia alcista (comprar cada retroceso), no la raya: el
    /// placebo rinde lo mismo. Se dibuja igual porque el lo pidio, y cada disparo se registra en
    /// pythiagex-gatillos-&lt;inst&gt;.jsonl para juzgarlo con dias nuevos.
    /// </summary>
    public sealed class GatilloRebote
    {
        public enum Modo { SoloTendencia, Todos, PrimerToqueActual, Ninguno }

        public Modo ModoUso = Modo.SoloTendencia;
        public double TolPct = 0.035, PenPct = 0.085, RetPct = 0.05;   // % del precio: NQ 10 / 25 / 15 pts; ES 2,3 / 5,5 / 3,3
        public double Paso = 10;                                         // strike: identidad de la fila (NQ 10, ES 5)
        public int Atras = 10, Enfriamiento = 5;
        public bool ConZero = true, ConMajors = true, SoloRueda = true;
        public int Disparos, Candidatos;

        private readonly Dictionary<double, (double Fut, int Bar)> _filas = new();          // strike -> (valor exacto, vela en que nacio)
        private readonly Dictionary<(double K, int Lado), int> _ultimo = new(), _toques = new();
        private readonly List<(double H, double L, double C)> _velas = new();
        private DateTime _dia = DateTime.MinValue;
        private bool _enRueda;
        private int _ultimoBar = -1;

        public IReadOnlyDictionary<double, (double Fut, int Bar)> Filas => _filas;

        private double Strike(double x) => Math.Round(x / Math.Max(0.01, Paso)) * Math.Max(0.01, Paso);

        private static bool EnRueda(DateTime utc)
        {
            int m = utc.Hour * 60 + utc.Minute;
            return m >= 13 * 60 + 30 && m < 20 * 60;
        }

        /// <summary>Filas de dominantes ya dibujadas (los guiones de la rueda de hoy), para que el
        /// vivo arranque con las mismas rayas que ve el operador y no solo con las de ahora.</summary>
        public void Sembrar(IEnumerable<(double Fut, int Bar)> filas)
        {
            foreach (var f in filas)
            {
                if (f.Fut <= 0) continue;
                double k = Strike(f.Fut);
                if (!_filas.TryGetValue(k, out var v) || v.Bar > f.Bar) _filas[k] = (f.Fut, f.Bar);
            }
        }

        /// <summary>Disparos ya hechos hoy (por el recorrido del archivo) para no repetir el
        /// "primer toque" ni saltarse el enfriamiento al pasar al vivo.</summary>
        public void SembrarDisparo(double nivel, int lado, int bar)
        {
            var k = (Strike(nivel), lado);
            _toques[k] = (_toques.TryGetValue(k, out int n) ? n : 0) + 1;
            if (!_ultimo.TryGetValue(k, out int b) || b < bar) _ultimo[k] = bar;
        }

        public List<GatilloBanda.Marca> Procesar(int bar, DateTime horaUtc, double o, double h, double l, double c, double delta,
                                                 List<(double Fut, double Gex)> doms, double zero, double mp, double mn)
        {
            var res = new List<GatilloBanda.Marca>();
            if (bar <= _ultimoBar || c <= 0 || h < l) return res;
            _ultimoBar = bar;
            bool rueda = EnRueda(horaUtc);
            // el dia de las filas: la rueda americana (como en el laboratorio); sin SoloRueda, el dia UTC
            bool nuevoDia = SoloRueda ? (rueda && !_enRueda) : horaUtc.Date != _dia;
            _enRueda = rueda; _dia = horaUtc.Date;
            if (nuevoDia) { _filas.Clear(); _ultimo.Clear(); _toques.Clear(); _velas.Clear(); }
            var actuales = new HashSet<double>();
            if (doms != null)
                foreach (var d in doms)
                {
                    if (d.Fut <= 0) continue;
                    double k = Strike(d.Fut); actuales.Add(k);
                    _filas[k] = (d.Fut, _filas.TryGetValue(k, out var f) ? f.Bar : bar);
                }
            (double H, double L, double C)? prev = _velas.Count > 0 ? _velas[_velas.Count - 1] : null;
            var ventana = _velas.Count > Atras ? _velas.GetRange(_velas.Count - Atras, Atras) : new List<(double H, double L, double C)>();
            _velas.Add((h, l, c));
            if (_velas.Count > Atras + 2) _velas.RemoveAt(0);
            if (ModoUso == Modo.Ninguno || prev == null || ventana.Count < Atras) return res;
            if (SoloRueda && !rueda) return res;
            double tol = c * TolPct / 100.0, pen = c * PenPct / 100.0, ret = c * RetPct / 100.0;
            double maxh = ventana.Max(v => v.H), minl = ventana.Min(v => v.L);
            var niveles = new List<(double L, double K, string Tipo)>();
            foreach (var kv in _filas) niveles.Add((kv.Value.Fut, kv.Key, "dom"));
            if (ConZero && zero > 0 && !double.IsNaN(zero) && niveles.All(n => n.K != Strike(zero))) niveles.Add((zero, Strike(zero), "zero"));
            if (ConMajors)
                foreach (var m in new[] { mp, mn })
                    if (m > 0 && !double.IsNaN(m) && niveles.All(n => n.K != Strike(m))) niveles.Add((m, Strike(m), "major"));
            var (ph, pl, pc) = prev.Value;
            foreach (int lado in new[] { 1, -1 })
            {
                (double Dist, double L, double K, string Tipo)? mejor = null;
                foreach (var n in niveles)
                {
                    bool toca, bien, venia, antes; double dist;
                    if (lado > 0)
                    {
                        toca = l <= n.L + tol && l >= n.L - pen; bien = c >= n.L; venia = maxh >= n.L + ret;
                        antes = pl <= n.L + tol && pl >= n.L - pen && pc >= n.L; dist = Math.Abs(l - n.L);
                    }
                    else
                    {
                        toca = h >= n.L - tol && h <= n.L + pen; bien = c <= n.L; venia = minl <= n.L - ret;
                        antes = ph >= n.L - tol && ph <= n.L + pen && pc <= n.L; dist = Math.Abs(h - n.L);
                    }
                    if (!(toca && bien && venia) || antes) continue;
                    if (_ultimo.TryGetValue((n.K, lado), out int ub) && bar - ub <= Enfriamiento) continue;
                    if (mejor == null || dist < mejor.Value.Dist) mejor = (dist, n.L, n.K, n.Tipo);
                }
                if (mejor == null) continue;
                var mv = mejor.Value;
                Candidatos++;
                _ultimo[(mv.K, lado)] = bar;
                int nToque = (_toques.TryGetValue((mv.K, lado), out int nt) ? nt : 0) + 1;
                _toques[(mv.K, lado)] = nToque;
                bool aFavor = zero > 0 && !double.IsNaN(zero) && ((lado > 0) == (c > zero));
                if (ModoUso == Modo.SoloTendencia && !aFavor) continue;
                if (ModoUso == Modo.PrimerToqueActual && !(aFavor && nToque == 1 && mv.Tipo == "dom" && actuales.Contains(mv.K))) continue;
                Disparos++;
                res.Add(new GatilloBanda.Marca
                {
                    Bar = bar, Hora = horaUtc, Tipo = "rebote·" + mv.Tipo, Lado = lado, Precio = c, Dom = mv.L, Dz = nToque,
                    Arriba = lado < 0, Principal = true,
                });
            }
            return res;
        }
    }
}
