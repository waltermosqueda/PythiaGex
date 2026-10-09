using System;
using System.Collections.Generic;

namespace PatronOperador
{
    /// <summary>
    /// EL NUCLEO DEL "PATRON OPERADOR": las reglas de la ronda 9, en C# puro y sin una sola
    /// referencia a ATAS, para que el MISMO codigo que dibuja en vivo se pueda correr sobre las
    /// cintas grabadas y compararse contra el Python (arnes/Program.cs + r9_p_02_comparar.py).
    ///
    /// QUE ES CADA RASGO (todo causal: en el instante t solo entran eventos con T &lt;= t)
    ///   mid           (bid + ask) / 2 del ULTIMO evento de la cinta con T &lt;= t, con la punta
    ///                 limpiada igual que el Python (r9_lib.preparar): una punta es mala si el bid
    ///                 no es positivo, o el ask no es mayor que el bid, o estan a mas de 20 puntos;
    ///                 la mala se reemplaza por la ultima buena (ffill).
    ///   ret_Ws        mid(t) - mid(ultimo evento con T &lt;= t - W s). Medio a medio, sin signo.
    ///   desbal_punta  (volumen del bid - volumen del ask) / (la suma), del mismo evento.
    ///   pos_rango_60s donde esta el medio dentro del rango de los ultimos 60 s (0 = en el minimo,
    ///                 1 = en el maximo). El rango se arma con los segundos [se-60, se) mas el
    ///                 tramo del segundo en curso; los segundos sin operaciones valen el ultimo
    ///                 precio impreso (igual que el ffill del Python).
    ///   desbal_10     (volumen comprador - vendedor) / (la suma) en la ventana (t-10 s, t].
    ///   m2p_cuerpo    cierre - apertura de la vela de 2 min ANTERIOR ya cerrada. Las velas se
    ///                 cortan por reloj UTC absoluto (t // 120 s), igual que en el Python; si en
    ///                 esos 2 minutos no hubo ninguna operacion, el rasgo no existe.
    ///
    /// LAS REGLAS (r9_j_lib.masks_regla, copiadas al pie de la letra)
    ///   AGREGA_EN_CONTRA (G5)  compra si ret_120s &lt;= -3 ; venta si ret_120s &gt;= +3
    ///   NO_PERSIGUE      (R2)  compra si ret_20s &lt;= 0 ; venta si ret_20s &gt;= 0
    ///   NO_PERSIGUE_PUNTA(R3)  compra si ret_20s &lt;= 1 y desbal_punta &lt;= 0,429
    ///                          venta  si ret_20s &gt;= -1 y desbal_punta &gt;= -0,429
    ///   PERSIGUE      (G1/G2)  compra si ret_30s &gt;= +2 ; venta si ret_30s &lt;= -2   [ANTI-SENAL]
    ///   PERSIGUE_EXTREMO (G4)  lo anterior + pegado al extremo del minuto (pos_rango_60s &gt;= 0,75)
    ///                          + cinta agrediendo a ese lado (|desbal_10| &gt;= 0,10)  [ANTI-SENAL]
    ///
    /// LO QUE MIDIO EL JUEZ (24 sesiones que el operador NO opero, 31-07 a 04-09; archivo
    /// r9_j_03_salida.txt): NINGUNA de las 17 reglas da neto positivo, ni una sola sesion LOSO
    /// positiva. Las tres primeras son las que le ganan al AZAR antes de costos (entre +0,12 y
    /// +0,26 puntos) y las dos anti-senal son PEORES que el azar (-0,34 a -0,50). La vuelta cuesta
    /// 0,948 puntos: la mejor produce 0,26. Por eso todo sale rotulado "EN PRUEBA: no validado" y
    /// por eso este indicador no opera nada.
    /// </summary>
    public sealed class PatronNucleo
    {
        public const double Tick = 0.25;
        public const long NS = 1_000_000_000L;
        private const long BarraNs = 120L * NS;   // la vela de 2 min de su grafico

        // ---- umbrales: los de la ronda 9. No se exponen como ajustes a proposito: cambiarlos
        //      rompe la equivalencia con lo medido y con el Python.
        public const double UmbralPersigue = 2.0;        // ret_30s de G1/G2/G4
        public const double UmbralAgrega = 3.0;          // ret_120s de G5
        public const double UmbralPunta = 0.429;         // desbal_punta de R1/R3
        public const double UmbralNoPersigue = 1.0;      // ret_20s de R3
        public const double ExtremoMin = 0.75;           // pos_rango_60s de G4
        public const double AgresionMin = 0.10;          // desbal_10 de G4

        public const double CostoVuelta = 0.948;         // puntos, ida y vuelta (ronda 8)

        // ================================================================== reglas
        public enum Regla { AgregaEnContra, NoPersiguePunta, NoPersigue, Persigue, PersigueExtremo }

        public sealed class FichaRegla
        {
            public Regla Id;
            public string Clave;      // el nombre que usa el Python
            public string Corto;      // el que va en el grafico
            public bool AntiSenal;
            public string Medido;     // la tasa medida, tal cual, para el rotulo
        }

        public static readonly FichaRegla[] Fichas =
        {
            new FichaRegla { Id = Regla.AgregaEnContra, Clave = "G5_agrega_en_contra", Corto = "CONTRA",
                AntiSenal = false,
                Medido = "juez 24 ses: neto -0,779 pts/op (n 22.447); contra el azar +0,260 (t +4,67)" },
            new FichaRegla { Id = Regla.NoPersiguePunta, Clave = "L2_R3_no_persigue_y_punta", Corto = "QUIETO+P",
                AntiSenal = false,
                Medido = "juez 24 ses: neto -0,757 pts/op (n 22.397); contra el azar +0,206 (t +3,16)" },
            new FichaRegla { Id = Regla.NoPersigue, Clave = "L2_R2_no_persigas", Corto = "QUIETO",
                AntiSenal = false,
                Medido = "juez 24 ses: neto -0,765 pts/op (n 32.259); contra el azar +0,217 (t +4,56)" },
            new FichaRegla { Id = Regla.Persigue, Clave = "G1_G2_persigue", Corto = "PERSIGO",
                AntiSenal = true,
                Medido = "juez 24 ses: neto -1,41 / -1,30 pts/op; PEOR que el azar (-0,54 y -0,34)" },
            new FichaRegla { Id = Regla.PersigueExtremo, Clave = "G4_persigue_extremo_y_agresion", Corto = "PERSIGO!!",
                AntiSenal = true,
                Medido = "juez 24 ses: neto -1,437 pts/op (n 8.555), la peor de 17; PEOR que el azar (-0,502)" },
        };

        public static FichaRegla Ficha(Regla r)
        {
            foreach (var f in Fichas) if (f.Id == r) return f;
            return null;
        }

        // ================================================================== rasgos
        public struct Rasgos
        {
            public bool Valido;
            public long TNs;
            public double Mid, Bid, Ask, BidVol, AskVol, SpreadTicks, DesbalPunta;
            public double Ret20, Ret30, Ret120;
            public double PosRango60, Desbal10, M2pCuerpo;
            public int Eventos;
            public double SegHistoria;
        }

        // ================================================================== estado
        private struct Ev
        {
            public long T;
            public double P1, P2, Vol, Mid, Bid, Ask, BidV, AskV;
            public int Lado;
        }

        private struct Vela
        {
            public double Apertura, Cierre;
        }

        private readonly List<Ev> _ev = new List<Ev>(1 << 16);
        private readonly Dictionary<long, Vela> _velas = new Dictionary<long, Vela>();

        // grilla de 1 segundo: maximo y minimo de cada segundo; los segundos sin operaciones se
        // rellenan con el ultimo precio impreso, igual que el ffill del Python.
        private const int CapSeg = 4096;
        private readonly double[] _secHi = new double[CapSeg];
        private readonly double[] _secLo = new double[CapSeg];
        private long _segUlt = long.MinValue, _segPrimero = long.MaxValue;
        private double _pxUlt = double.NaN;

        private double _bidOk = double.NaN, _askOk = double.NaN, _bidvOk = double.NaN, _askvOk = double.NaN;
        private long _tUlt = long.MinValue;

        public int Eventos { get; private set; }
        public long PrimerT { get; private set; } = long.MinValue;
        public long UltimoT => _tUlt;

        /// <summary>Segundos de cinta que hacen falta antes de creerle a los rasgos (ret_120s + margen).</summary>
        public const int SegCalentamiento = 130;

        public double SegundosDeCinta => (_tUlt > long.MinValue && PrimerT > long.MinValue)
            ? (_tUlt - PrimerT) / (double)NS : 0.0;

        public void Reiniciar()
        {
            _ev.Clear();
            _velas.Clear();
            _segUlt = long.MinValue; _segPrimero = long.MaxValue; _pxUlt = double.NaN;
            _bidOk = _askOk = _bidvOk = _askvOk = double.NaN;
            _tUlt = long.MinValue; PrimerT = long.MinValue; Eventos = 0;
        }

        /// <summary>
        /// Un evento de la cinta, ya terminado (un CumulativeTrade cerrado en vivo, o una fila del
        /// CSV grabado). primero/ultimo son el precio del primer y del ultimo print del evento;
        /// bid/ask son la punta DESPUES del evento (NewBid/NewAsk), que es lo que guarda la cinta.
        /// Los eventos tienen que llegar en orden de tiempo.
        /// </summary>
        public void Agregar(long tNs, double primero, double ultimo, double vol, int lado,
                            double bid, double bidVol, double ask, double askVol)
        {
            if (_tUlt > long.MinValue && tNs < _tUlt) return;   // fuera de orden: se descarta
            if (PrimerT == long.MinValue) PrimerT = tNs;
            _tUlt = tNs;
            Eventos++;

            // ---- limpieza de la punta, identica a r9_lib.preparar
            bool malo = !(bid > 0 && ask > bid && (ask - bid) < 20);
            if (!malo) { _bidOk = bid; _askOk = ask; _bidvOk = bidVol; _askvOk = askVol; }

            _ev.Add(new Ev
            {
                T = tNs, P1 = primero, P2 = ultimo, Vol = vol, Lado = lado,
                Mid = (_bidOk + _askOk) / 2.0,
                Bid = _bidOk, Ask = _askOk, BidV = _bidvOk, AskV = _askvOk
            });
            Podar();

            // ---- grilla de 1 segundo
            long sec = FloorDiv(tNs, NS);
            double hi = Math.Max(primero, ultimo), lo = Math.Min(primero, ultimo);
            if (_segUlt == long.MinValue)
            {
                _segPrimero = sec; _segUlt = sec;
                int k0 = Ranura(sec); _secHi[k0] = hi; _secLo[k0] = lo;
            }
            else if (sec > _segUlt)
            {
                for (long s = _segUlt + 1; s < sec && s <= _segUlt + CapSeg; s++)
                {
                    int kk = Ranura(s); _secHi[kk] = _pxUlt; _secLo[kk] = _pxUlt;
                }
                if (sec - _segUlt >= CapSeg) _segPrimero = sec;
                _segUlt = sec;
                int k1 = Ranura(sec); _secHi[k1] = hi; _secLo[k1] = lo;
            }
            else
            {
                int k2 = Ranura(sec);
                if (hi > _secHi[k2]) _secHi[k2] = hi;
                if (lo < _secLo[k2]) _secLo[k2] = lo;
            }
            if (_segUlt - _segPrimero >= CapSeg) _segPrimero = _segUlt - CapSeg + 1;
            _pxUlt = ultimo;

            // ---- velas de 2 min por reloj UTC absoluto
            long b = FloorDiv(tNs, BarraNs);
            if (_velas.TryGetValue(b, out var v)) { v.Cierre = ultimo; _velas[b] = v; }
            else
            {
                _velas[b] = new Vela { Apertura = primero, Cierre = ultimo };
                if (_velas.Count > 8)
                {
                    var viejas = new List<long>();
                    foreach (var kv in _velas) if (kv.Key < b - 5) viejas.Add(kv.Key);
                    foreach (var k3 in viejas) _velas.Remove(k3);
                }
            }
        }

        private static int Ranura(long sec) => (int)(((sec % CapSeg) + CapSeg) % CapSeg);

        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }

        private void Podar()
        {
            // se guardan 900 s de cinta: alcanza de sobra para ret_120s y el rango de 60 s
            if (_ev.Count < 16384) return;
            long corte = _tUlt - 900L * NS;
            int i = 0;
            while (i < _ev.Count && _ev[i].T < corte) i++;
            if (i > 4096) _ev.RemoveRange(0, i);
        }

        /// <summary>Ultimo evento con T &lt;= t; -1 si no hay.</summary>
        private int IdxDe(long t)
        {
            int lo = 0, hi = _ev.Count - 1, r = -1;
            while (lo <= hi)
            {
                int m = (lo + hi) >> 1;
                if (_ev[m].T <= t) { r = m; lo = m + 1; }
                else hi = m - 1;
            }
            return r;
        }

        public Rasgos Evaluar(long tNs)
        {
            var r = new Rasgos { TNs = tNs, Eventos = Eventos, SegHistoria = SegundosDeCinta };
            r.Mid = r.Bid = r.Ask = r.BidVol = r.AskVol = r.SpreadTicks = r.DesbalPunta = double.NaN;
            r.Ret20 = r.Ret30 = r.Ret120 = r.PosRango60 = r.Desbal10 = r.M2pCuerpo = double.NaN;

            int i = IdxDe(tNs);
            if (i < 0) return r;
            r.Valido = true;
            var e = _ev[i];
            r.Mid = e.Mid; r.Bid = e.Bid; r.Ask = e.Ask; r.BidVol = e.BidV; r.AskVol = e.AskV;

            double tot = r.BidVol + r.AskVol;
            if (tot > 0) r.DesbalPunta = (r.BidVol - r.AskVol) / tot;
            if (r.Ask > r.Bid) r.SpreadTicks = (r.Ask - r.Bid) / Tick;

            r.Ret20 = Retorno(tNs, 20, r.Mid);
            r.Ret30 = Retorno(tNs, 30, r.Mid);
            r.Ret120 = Retorno(tNs, 120, r.Mid);

            // ---- desbal_10: ventana (t - 10 s, t]
            int j = IdxDe(tNs - 10L * NS) + 1;
            double vc = 0, vv = 0;
            for (int k = Math.Max(0, j); k <= i; k++)
            {
                if (_ev[k].Lado > 0) vc += _ev[k].Vol;
                else if (_ev[k].Lado < 0) vv += _ev[k].Vol;
            }
            if (vc + vv > 0) r.Desbal10 = (vc - vv) / (vc + vv);

            r.PosRango60 = PosRango(tNs, i, r.Mid, 60);

            long bar = FloorDiv(tNs, BarraNs);
            if (_velas.TryGetValue(bar - 1, out var vp)) r.M2pCuerpo = vp.Cierre - vp.Apertura;

            return r;
        }

        private double Retorno(long t, int w, double mid)
        {
            int j = IdxDe(t - (long)w * NS);
            if (j < 0) return double.NaN;
            return mid - _ev[j].Mid;
        }

        private double PosRango(long t, int i, double mid, int w)
        {
            if (_segUlt == long.MinValue) return double.NaN;
            long se = FloorDiv(t, NS);

            // tramo parcial del segundo en curso: eventos desde el inicio de ese segundo hasta t
            double ph = double.NaN, pl = double.NaN;
            int jq = IdxDe(se * NS - 1) + 1;                  // primer evento con T >= se*NS
            if (jq <= i)
            {
                double a1 = double.NegativeInfinity, b1 = double.PositiveInfinity;
                for (int k = Math.Max(0, jq); k <= i; k++)
                {
                    double hi2 = Math.Max(_ev[k].P1, _ev[k].P2), lo2 = Math.Min(_ev[k].P1, _ev[k].P2);
                    if (hi2 > a1) a1 = hi2;
                    if (lo2 < b1) b1 = lo2;
                }
                if (!double.IsInfinity(a1)) { ph = a1; pl = b1; }
            }

            long a = se - w;
            if (a < _segPrimero) a = _segPrimero;
            double hw = double.NegativeInfinity, lw = double.PositiveInfinity;
            bool hay = false;
            for (long s = a; s < se; s++)
            {
                double h2, l2;
                if (s > _segUlt) { h2 = _pxUlt; l2 = _pxUlt; }        // hueco sin operaciones hasta t
                else { int k = Ranura(s); h2 = _secHi[k]; l2 = _secLo[k]; }
                if (double.IsNaN(h2) || double.IsNaN(l2)) continue;
                if (h2 > hw) hw = h2;
                if (l2 < lw) lw = l2;
                hay = true;
            }
            double H = hay ? hw : double.NaN, L = hay ? lw : double.NaN;
            if (!double.IsNaN(ph)) H = double.IsNaN(H) ? ph : Math.Max(H, ph);
            if (!double.IsNaN(pl)) L = double.IsNaN(L) ? pl : Math.Min(L, pl);
            if (double.IsNaN(H) || double.IsNaN(L)) return double.NaN;
            if (H > L) return (mid - L) / (H - L);
            return 0.5;
        }

        // ================================================================== las reglas
        /// <summary>+1 compra, -1 venta, 0 nada, 2 ambiguo (dispara de los dos lados: el motor del
        /// Python saltea esos momentos). En C# una comparacion contra NaN da false, igual que numpy.</summary>
        public static int Dispara(Regla r, in Rasgos x)
        {
            bool c = false, v = false;
            switch (r)
            {
                case Regla.AgregaEnContra:
                    c = x.Ret120 <= -UmbralAgrega;
                    v = x.Ret120 >= UmbralAgrega;
                    break;
                case Regla.NoPersigue:
                    c = x.Ret20 <= 0.0;
                    v = x.Ret20 >= 0.0;
                    break;
                case Regla.NoPersiguePunta:
                    c = x.Ret20 <= UmbralNoPersigue && x.DesbalPunta <= UmbralPunta;
                    v = x.Ret20 >= -UmbralNoPersigue && x.DesbalPunta >= -UmbralPunta;
                    break;
                case Regla.Persigue:
                    c = x.Ret30 >= UmbralPersigue;
                    v = x.Ret30 <= -UmbralPersigue;
                    break;
                case Regla.PersigueExtremo:
                    c = x.Ret30 >= UmbralPersigue && x.PosRango60 >= ExtremoMin && x.Desbal10 >= AgresionMin;
                    v = x.Ret30 <= -UmbralPersigue && (1.0 - x.PosRango60) >= ExtremoMin && -x.Desbal10 >= AgresionMin;
                    break;
            }
            if (c && v) return 2;
            return c ? 1 : (v ? -1 : 0);
        }

        // ================================================================== motor sin solapes
        /// <summary>
        /// El mismo motor del juez (r9_j_lib.elegir_entradas): se mira un momento cada 60 s, la
        /// entrada seria el SEGUNDO SIGUIENTE a la senal y mientras dura un ciclo no se toma otra.
        /// Aca no se opera nada: solo sirve para que la senal registrada sea la MISMA que se midio.
        /// </summary>
        public sealed class Motor
        {
            public int TopeS = 60;
            private long _libre = long.MinValue;

            public bool Aceptar(long tSec)
            {
                long ent = tSec + 1;
                if (_libre != long.MinValue && ent < _libre) return false;
                _libre = ent + TopeS;
                return true;
            }

            public void Reiniciar() { _libre = long.MinValue; }
        }
    }
}
