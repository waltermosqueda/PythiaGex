using System;
using System.Collections.Generic;
using System.Linq;

namespace PythiaGex
{
    /// <summary>
    /// GATILLOS DE ORDER FLOW EN LOS EXTREMOS DE LAS BANDAS DOMINANTES (puro, sin ATAS).
    ///
    /// Es la misma logica que laboratorio/gatillos.py, para que lo que se dibuja en
    /// pantalla sea EXACTAMENTE lo que el laboratorio juzga contra placebo. Si se
    /// cambia algo aca hay que cambiarlo alla, y al reves.
    ///
    /// QUE ES UN EVENTO: el precio entra por primera vez en la banda de la dominante
    /// de un lado (la de arriba o la de abajo del precio), viniendo de mas lejos
    /// (2,5 rangos tipicos + la banda) hace menos de 30 minutos, y con la dominante
    /// QUIETA (se movio menos de QuietaPct en las 5 velas previas: el precio fue a la
    /// banda, no la banda al precio; una dominante centroide que persigue al volumen
    /// "entra" sola en el precio y eso no es un toque).
    ///
    /// QUE ES UN GATILLO: en la vela de entrada o en las 4 siguientes, con el precio
    /// todavia a menos de 2 bandas de la dominante:
    ///   - RECHAZO·TREN (la principal, la unica que dio senal en el laboratorio del
    ///     2026-09-08: 27 casos, 70 % contra 37 % del placebo, en ES minuto a minuto,
    ///     rueda americana; POCOS casos, es una pista y no una prueba): tres deltas
    ///     seguidos del mismo signo EN CONTRA de la llegada (llego subiendo y venden
    ///     tres velas seguidas, o al reves). Lado: hacia adentro del canal.
    ///   - rechazo·delta: delta de la vela fuerte en contra (z <= -1) y cierre del
    ///     lado de adentro. Se registra, no se dibuja por defecto (13 pp sobre el
    ///     placebo con 23 casos).
    ///   - rechazo·divergencia: nuevo extremo de 5 velas en la banda con delta
    ///     contrario. Se registra (17 casos, 70 %).
    ///   - ruptura·delta: cierre del otro lado de la dominante con delta a favor. En
    ///     el laboratorio salio AL REVES (25 % de 20): se registra como contraseña
    ///     de que la ruptura con delta suele fallar, y se dibuja apagada.
    /// Cada tipo dispara a lo sumo una vez por evento.
    ///
    /// NADA DE ESTO ESTA PROBADO. Cada disparo se registra con su hora, precio y
    /// lado para que laboratorio/gatillos.py lo juzgue contra placebo cuando haya
    /// muestra (hacen falta 60+ casos por tipo). Un indicador que decide si tuvo
    /// razon es un indicador que siempre tiene razon.
    /// </summary>
    public sealed class GatilloBanda
    {
        public sealed class Marca
        {
            public int Bar; public DateTime Hora; public string Tipo = ""; public int Lado;   // +1 largo, -1 corto
            public double Precio, Dom, Dz; public bool Arriba, Principal;
        }

        public double BandaPct = 0.08;      // la franja hacia adentro, % del precio (la misma del dibujo)
        public double QuietaPct = 0.026;    // dominante quieta: max movimiento en 5 velas (% del precio: 2 pts en ES 7700)
        public double LejosMult = 2.5;      // "venia de lejos": 2,5 rangos tipicos + la banda
        public int EsperaMin = 30;          // ... hace menos de 30 minutos
        public int VelasGatillo = 5;        // velas desde la entrada en las que puede disparar
        public int Ventana = 60;            // velas para el z del delta y el rango tipico
        public bool SoloRueda = true;       // entradas solo en la rueda americana (13:30-20:00 UTC): donde se midio la pista

        private sealed class Entrada { public int Bar; public double Dom; public bool Arriba; public HashSet<string> Hechos = new(); }

        private readonly List<double> _deltas = new(), _rangos = new(), _altos = new(), _bajos = new();
        private readonly List<double>[] _hist = { new(), new() };          // dominante por lado, ultimas 6 velas
        private readonly DateTime?[] _lejosDesde = new DateTime?[2];
        private readonly bool[] _dentroYa = new bool[2];
        private readonly List<Entrada> _entradas = new();
        private int _ultimoBar = -1;

        public int Entradas { get; private set; }
        public int Disparos { get; private set; }

        /// <summary>Una vela cerrada (en orden). doms: las dominantes vigentes en esa vela
        /// (puede estar vacia: sin cadena no hay banda, pero el order flow sigue contando).</summary>
        public List<Marca> Procesar(int bar, DateTime hora, double o, double h, double l, double c, double delta,
                                    IList<(double Fut, double Gex)> doms)
        {
            var salida = new List<Marca>();
            if (bar <= _ultimoBar || c <= 0) return salida;
            _ultimoBar = bar;

            // ---- rasgos de order flow de ESTA vela contra las anteriores
            double dz = 0, rt = 0.25;
            if (_deltas.Count >= 20)
            {
                double m = _deltas.Average();
                double sd = Math.Sqrt(_deltas.Sum(x => (x - m) * (x - m)) / _deltas.Count);
                dz = delta / (sd > 0 ? sd : 1.0);
            }
            if (_rangos.Count >= 5) { var ord = _rangos.OrderBy(x => x).ToList(); rt = Math.Max(0.25, ord[ord.Count / 2]); }
            bool tren = _deltas.Count >= 2 && ((delta > 0 && _deltas[^1] > 0 && _deltas[^2] > 0) || (delta < 0 && _deltas[^1] < 0 && _deltas[^2] < 0));
            bool nuevoAlto = _altos.Count >= 5 && h > _altos.Skip(_altos.Count - 5).Max();
            bool nuevoBajo = _bajos.Count >= 5 && l < _bajos.Skip(_bajos.Count - 5).Min();

            double semi = c * BandaPct / 100.0;
            double lejos = LejosMult * rt + semi;

            // ---- entradas a la banda, por lado
            double? arr = null, aba = null;
            if (doms != null)
                foreach (var d in doms)
                {
                    if (d.Fut > c) { if (arr == null || d.Fut < arr) arr = d.Fut; }
                    else if (aba == null || d.Fut > aba) aba = d.Fut;
                }
            for (int lado = 0; lado < 2; lado++)
            {
                bool arriba = lado == 0;
                double? dn = arriba ? arr : aba;
                if (dn == null) { _dentroYa[lado] = false; _hist[lado].Clear(); continue; }
                double d = dn.Value;
                var hh = _hist[lado]; hh.Add(d); if (hh.Count > 6) hh.RemoveAt(0);
                bool dentro = arriba ? h >= d - semi : l <= d + semi;
                double dist = Math.Abs(c - d);
                bool quieta = hh.Count == 6 && Math.Abs(hh[5] - hh[0]) <= c * QuietaPct / 100.0;
                if (dist > lejos) { _lejosDesde[lado] = hora; _dentroYa[lado] = false; continue; }
                if (!dentro) { _dentroYa[lado] = false; continue; }
                if (_dentroYa[lado]) continue;
                _dentroYa[lado] = true;
                if (!quieta) continue;
                var ld = _lejosDesde[lado];
                if (ld == null || (hora - ld.Value).TotalMinutes > EsperaMin) continue;
                if (SoloRueda) { int mUtc = hora.Hour * 60 + hora.Minute; if (mUtc < 13 * 60 + 30 || mUtc >= 20 * 60) continue; }
                _entradas.Add(new Entrada { Bar = bar, Dom = d, Arriba = arriba });
                Entradas++;
            }

            // ---- gatillos sobre las entradas vivas
            for (int k = _entradas.Count - 1; k >= 0; k--)
            {
                var e = _entradas[k];
                if (bar - e.Bar >= VelasGatillo || Math.Abs(c - e.Dom) > 2 * semi) { _entradas.RemoveAt(k); continue; }
                int rech = e.Arriba ? -1 : 1, cont = -rech;
                void Tiro(string tipo, int ld, bool principal)
                {
                    if (e.Hechos.Contains(tipo)) return;
                    e.Hechos.Add(tipo);
                    salida.Add(new Marca { Bar = bar, Hora = hora, Tipo = tipo, Lado = ld, Precio = c, Dom = e.Dom, Dz = dz, Arriba = e.Arriba, Principal = principal });
                    Disparos++;
                }
                if (tren && (delta < 0) == e.Arriba) Tiro("rechazo·tren", rech, true);
                bool deltaContra = e.Arriba ? dz <= -1.0 : dz >= 1.0;
                bool cierraAdentro = e.Arriba ? c < e.Dom : c > e.Dom;
                if (deltaContra && cierraAdentro) Tiro("rechazo·delta", rech, false);
                if (e.Arriba ? (nuevoAlto && delta < 0) : (nuevoBajo && delta > 0)) Tiro("rechazo·divergencia", rech, false);
                bool deltaFavor = e.Arriba ? dz >= 1.0 : dz <= -1.0;
                if (deltaFavor && !cierraAdentro && c != e.Dom) Tiro("ruptura·delta", cont, false);
            }

            // ---- la vela pasa a la historia
            _deltas.Add(delta); _rangos.Add(h - l); _altos.Add(h); _bajos.Add(l);
            if (_deltas.Count > Ventana) { _deltas.RemoveAt(0); _rangos.RemoveAt(0); _altos.RemoveAt(0); _bajos.RemoveAt(0); }
            return salida;
        }
    }
}
