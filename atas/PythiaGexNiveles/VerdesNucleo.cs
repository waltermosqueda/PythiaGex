using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PythiaGex
{
    /// <summary>
    /// VERDES · NUCLEO (17-09-2026). La maquina de estados del RECLAMO en las verdes, SIN ninguna dependencia de ATAS: la usan, con el MISMO codigo,
    /// el indicador en vivo (FlujoClaroVerdes.cs) y el banco de pruebas (atas/VerdesBanco, que la corre sobre la cinta operacion por operacion).
    /// Nace de la revision del 17-09: el detector 1.6 y el banco en Python eran dos logicas distintas (paridad 82 %) y el numero del banco no era el del vivo.
    ///
    /// Entradas: Rayas(t, niveles, fuerzas, libro, futuroDelEscritor) cada vez que hay una linea nueva de estela; Operacion(t, precio, volumenFirmado) con cada
    /// operacion de la cinta. Salidas: eventos (toque, rota, sin_gatillo, gatillo, resultado) por el callback Evento.
    ///
    /// Reglas (todas fijadas ANTES de volver a medir):
    ///  - una raya es la MISMA si su nivel esta a <= MismaRaya pts de una que ya se sigue; si deja de figurar se la sigue GraciaSeg segundos mas
    ///    (el selector 'una por lado' la saca de la estela justo cuando el precio la cruza);
    ///  - una raya solo sirve si su ultima linea de estela tiene <= FrescuraSeg y fue escrita DESPUES de arrancar el nucleo, y si el futuro del escritor
    ///    esta a <= EscalaTol pts del ultimo precio de esta cinta (misma escala de contrato);
    ///  - TOQUE: el precio estuvo a >= Lejos pts del lado bueno con la raya ya existente (hace <= MemoriaLejosSeg) y ENTRA a la zona (<= Tol) en una
    ///    TRANSICION desde ese lado; una sola visita abierta por raya;
    ///  - durante la visita se sigue el extremo con cada operacion; si traspasa mas de TraspasoMax es ROTA; a los VisitaSeg sin reclamo, SIN GATILLO;
    ///  - la DECISION se toma una vez por segundo, al cerrar cada segundo posterior al de entrada, con el cierre y el extremo HASTA ese cierre:
    ///    cruce >= CruceMin y cierre >= Reclamo pts del lado bueno, y (opcional) delta de los 10 s que terminan en ese segundo a favor;
    ///  - entrada al precio de la primera operacion siguiente; stop StopTrasMecha pts detras del extremo; objetivo fijo; vence a los VenceSeg;
    ///    el stop se anota al PEOR entre el stop y el precio que lo disparo;
    ///  - ademas de las rayas verdaderas se siguen, igual, rayas PLACEBO corridas (Corrimientos): el juez en vivo.
    /// </summary>
    public sealed class VerdesNucleo
    {
        public sealed class Ajustes
        {
            public double Tol = 1.5, Lejos = 10, TraspasoMax = 10, CruceMin = 0.5, Reclamo = 1.5, Objetivo = 20, StopTrasMecha = 1.0, MismaRaya = 2.5, EscalaTol = 8;
            public int VisitaSeg = 90, VenceSeg = 900, MemoriaLejosSeg = 1800, FrescuraSeg = 150, GraciaSeg = 180;
            public bool ExigirDelta = true;
            public double[] Corrimientos = { 37.5, -37.5, 62.5, -62.5 };
            public double Escala = 1.0;   // 1 = NQ/MNQ
        }

        public sealed class Suceso
        {
            public string Tipo; public DateTime T; public string Id; public double Raya, Corrimiento, Precio, Traspaso, SegEnZona, Vel60, Delta10, Fuerza, Neto, Stop, Objetivo, Entrada, Puntos;
            public int Lado, Resultado; public bool PorOi, Rueda;
        }

        private sealed class Raya
        {
            public double Nivel, Fuerza, Corr; public bool PorOi; public DateTime Vista, Nacio;
            public DateTime LejosArriba = DateTime.MinValue, LejosAbajo = DateTime.MinValue;
            public Visita V;
        }
        private sealed class Visita { public int Lado; public DateTime T0; public long Seg0; public double L, Extremo, ExtremoAlCierre, Vel60; public string Id; }
        public sealed class Sombra
        {
            public string Id; public DateTime T; public int Lado, Resultado; public double Entrada, Stop, Objetivo, L, Corr, Traspaso, Fuerza, Puntos; public bool PorOi, Rueda;
        }

        public readonly Ajustes A = new Ajustes();
        public Action<Suceso> Evento;
        private readonly List<Raya> _rayas = new List<Raya>();
        public readonly List<Sombra> Sombras = new List<Sombra>();
        private readonly Queue<(long Seg, double Delta)> _delta = new Queue<(long, double)>();
        private readonly Queue<(long Seg, double Precio)> _precios = new Queue<(long, double)>();
        private DateTime _arranque = DateTime.MinValue, _ultimaLinea = DateTime.MinValue; private double _neto, _futEscritor;
        private long _segActual = -1; private double _cierreSeg, _deltaSeg, _precioPrevio; private bool _pendienteDecidir;
        private readonly List<(Raya R, double Pen)> _porEntrar = new List<(Raya, double)>();
        public string Estado = ""; public DateTime EstadoHasta = DateTime.MinValue;

        public static bool EsRueda(DateTime tUtc) { int m = tUtc.Hour * 60 + tUtc.Minute; return m >= 13 * 60 + 30 && m < 20 * 60 && tUtc.DayOfWeek != DayOfWeek.Saturday && tUtc.DayOfWeek != DayOfWeek.Sunday; }

        /// <summary>El nucleo arranca: solo valen las lineas de estela escritas de aca en adelante.</summary>
        public void Arrancar(DateTime tUtc) { _arranque = tUtc; }

        /// <summary>Una linea nueva de la estela: las dos dominantes (0 = no hay), su fuerza, el libro y el futuro que veia quien la escribio.</summary>
        public void Rayas(DateTime tUtc, IList<double> niveles, IList<double> fuerzas, bool porOi, double neto, double futEscritor)
        {
            if (tUtc < _arranque) return;
            _ultimaLinea = tUtc; _neto = neto; _futEscritor = futEscritor;
            for (int i = 0; i < niveles.Count && i < 2; i++)
            {
                double nv = niveles[i]; if (nv <= 0) continue;
                foreach (double c in new[] { 0.0 }.Concat(A.Corrimientos.Select(x => x * A.Escala)))
                {
                    double L = nv + c; var r = _rayas.FirstOrDefault(x => x.Corr == c && Math.Abs(x.Nivel - L) <= A.MismaRaya * A.Escala);
                    if (r == null) { r = new Raya { Corr = c, Nacio = tUtc }; _rayas.Add(r); }
                    r.Nivel = L; r.Fuerza = i < fuerzas.Count ? fuerzas[i] : 0; r.PorOi = porOi; r.Vista = tUtc;
                }
            }
            _rayas.RemoveAll(x => x.V == null && (tUtc - x.Vista).TotalSeconds > A.GraciaSeg);
        }

        public bool HayRayas(DateTime tUtc) => (tUtc - _ultimaLinea).TotalSeconds <= A.FrescuraSeg;
        public DateTime UltimaLinea => _ultimaLinea;

        /// <summary>Cada operacion de la cinta, en orden. vol firmado: + compra agresora, - venta.</summary>
        public void Operacion(DateTime tUtc, double p, double volFirmado)
        {
            if (p <= 0) return;
            long seg = tUtc.Ticks / TimeSpan.TicksPerSecond;
            if (_segActual < 0) { _segActual = seg; _precioPrevio = p; }
            if (seg != _segActual)
            {
                // cerro el segundo anterior: se decide con SU cierre y con el extremo hasta ese cierre; la entrada es ESTA operacion
                CerrarSegundo(_segActual);
                Decidir(tUtc, p);
                _segActual = seg; _deltaSeg = 0;
            }
            _deltaSeg += volFirmado; _cierreSeg = p;
            double esc = A.Escala, tol = A.Tol * esc, lejos = A.Lejos * esc, penMax = A.TraspasoMax * esc;
            // sombras abiertas: objetivo / stop (al peor precio) / vencimiento
            foreach (var s in Sombras)
            {
                if (s.Resultado != 0) continue;
                if ((p - s.Stop) * s.Lado <= 0) Cerrar(s, -1, (Math.Min(p * s.Lado, s.Stop * s.Lado) - s.Entrada * s.Lado), tUtc);
                else if ((p - s.Objetivo) * s.Lado >= 0) Cerrar(s, +1, (s.Objetivo - s.Entrada) * s.Lado, tUtc);
                else if ((tUtc - s.T).TotalSeconds > A.VenceSeg) Cerrar(s, 2, (p - s.Entrada) * s.Lado, tUtc);
            }
            bool frescas = HayRayas(tUtc) && (_futEscritor <= 0 || Math.Abs(_futEscritor - p) <= Math.Max(A.EscalaTol * esc, 0.004 * p));
            foreach (var r in _rayas)
            {
                double L = r.Nivel; var v = r.V;
                if (v == null)
                {
                    if (!frescas || (tUtc - r.Vista).TotalSeconds > A.GraciaSeg) continue;
                    if (p >= L + lejos) r.LejosArriba = tUtc; else if (p <= L - lejos) r.LejosAbajo = tUtc;
                    int lado = 0;
                    // TRANSICION: la operacion anterior estaba fuera de la zona del lado bueno y esta entra
                    if (_precioPrevio > L + tol && p <= L + tol && p > L - penMax && (tUtc - r.LejosArriba).TotalSeconds <= A.MemoriaLejosSeg) lado = +1;
                    else if (_precioPrevio < L - tol && p >= L - tol && p < L + penMax && (tUtc - r.LejosAbajo).TotalSeconds <= A.MemoriaLejosSeg) lado = -1;
                    if (lado == 0) continue;
                    if (lado > 0) r.LejosArriba = DateTime.MinValue; else r.LejosAbajo = DateTime.MinValue;
                    double hace60 = 0; foreach (var q in _precios) { if (q.Seg <= seg - 60) hace60 = q.Precio; else break; }
                    r.V = v = new Visita { Lado = lado, T0 = tUtc, Seg0 = seg, L = L, Extremo = p, ExtremoAlCierre = p, Vel60 = hace60 > 0 ? Math.Abs(p - hace60) : 0,
                                           Id = tUtc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + (lado > 0 ? "S" : "T") + Math.Round(L * 4).ToString(CultureInfo.InvariantCulture) };
                    Emitir("toque", tUtc, r, v, p, 0, null);
                    if (r.Corr == 0) { Estado = "VERDE " + L.ToString("0.00", CultureInfo.InvariantCulture) + (lado > 0 ? " soporte" : " techo") + ": en zona"; EstadoHasta = tUtc.AddSeconds(A.VisitaSeg); }
                    continue;
                }
                v.Extremo = v.Lado > 0 ? Math.Min(v.Extremo, p) : Math.Max(v.Extremo, p);
                double pen = (v.L - v.Extremo) * v.Lado;
                if (pen > penMax) { Emitir("rota", tUtc, r, v, p, pen, null); r.V = null; if (r.Corr == 0) { Estado = "VERDE " + v.L.ToString("0.00", CultureInfo.InvariantCulture) + " ROTA"; EstadoHasta = tUtc.AddSeconds(45); } }
                else if ((tUtc - v.T0).TotalSeconds > A.VisitaSeg) { Emitir("sin_gatillo", tUtc, r, v, p, pen, null); r.V = null; }
            }
            _precioPrevio = p;
            if (_precios.Count == 0 || _precios.Last().Seg != seg) _precios.Enqueue((seg, p));
            while (_precios.Count > 0 && _precios.Peek().Seg < seg - 130) _precios.Dequeue();
        }

        private void CerrarSegundo(long seg)
        {
            _delta.Enqueue((seg, _deltaSeg));
            while (_delta.Count > 0 && _delta.Peek().Seg <= seg - 10) _delta.Dequeue();
            _porEntrar.Clear();
            double esc = A.Escala; double d10 = _delta.Sum(x => x.Delta);
            foreach (var r in _rayas)
            {
                var v = r.V; if (v == null || seg <= v.Seg0) continue;                       // en el segundo de entrada no se decide
                double pen = (v.L - v.Extremo) * v.Lado;                                      // extremo hasta este cierre (la operacion que abre el segundo nuevo todavia no entro)
                bool gat = pen >= A.CruceMin * esc && (_cierreSeg - v.L) * v.Lado >= A.Reclamo * esc && (!A.ExigirDelta || d10 * v.Lado > 0);
                if (gat) _porEntrar.Add((r, Math.Max(0, pen)));
            }
            _ultimoDelta10 = d10;
        }
        private double _ultimoDelta10;

        private void Decidir(DateTime tUtc, double pEntrada)
        {
            foreach (var (r, pen) in _porEntrar)
            {
                var v = r.V; if (v == null) continue;
                double esc = A.Escala;
                var s = new Sombra { Id = v.Id, T = tUtc, Lado = v.Lado, Entrada = pEntrada, Stop = v.Extremo - v.Lado * A.StopTrasMecha * esc, Objetivo = pEntrada + v.Lado * A.Objetivo * esc,
                                     L = v.L, Corr = r.Corr, Traspaso = pen, Fuerza = r.Fuerza, PorOi = r.PorOi, Rueda = EsRueda(tUtc) };
                if ((pEntrada - s.Stop) * s.Lado <= 0) { r.V = null; continue; }               // la operacion de entrada ya esta detras del stop: no hay operacion
                Sombras.Add(s); if (Sombras.Count > 2000) Sombras.RemoveAt(0);
                Emitir("gatillo", tUtc, r, v, pEntrada, pen, s); r.V = null;
                if (r.Corr == 0) { Estado = "RECLAMO " + (v.Lado > 0 ? "▲ " : "▼ ") + v.L.ToString("0.00", CultureInfo.InvariantCulture); EstadoHasta = tUtc.AddSeconds(90); }
            }
            _porEntrar.Clear();
        }

        private void Cerrar(Sombra s, int resultado, double puntos, DateTime t)
        {
            s.Resultado = resultado; s.Puntos = puntos;
            Evento?.Invoke(new Suceso { Tipo = "resultado", T = t, Id = s.Id, Raya = s.L, Corrimiento = s.Corr, Lado = s.Lado, Entrada = s.Entrada, Resultado = resultado, Puntos = puntos, Rueda = s.Rueda, PorOi = s.PorOi, Fuerza = s.Fuerza });
        }

        private void Emitir(string tipo, DateTime t, Raya r, Visita v, double p, double pen, Sombra s)
        {
            Evento?.Invoke(new Suceso { Tipo = tipo, T = t, Id = v.Id, Raya = v.L, Corrimiento = r.Corr, Lado = v.Lado, Precio = p, Traspaso = Math.Max(0, pen), SegEnZona = (t - v.T0).TotalSeconds, Vel60 = v.Vel60,
                                        Delta10 = _ultimoDelta10, Fuerza = r.Fuerza, Neto = _neto, PorOi = r.PorOi, Rueda = EsRueda(t), Stop = s?.Stop ?? 0, Objetivo = s?.Objetivo ?? 0, Entrada = s?.Entrada ?? 0 });
        }

        public static string AJson(Suceso e, string ver, string inst)
        {
            var c = CultureInfo.InvariantCulture;
            return "{\"ver\":\"" + ver + "\",\"inst\":\"" + inst + "\",\"ev\":\"" + e.Tipo + "\",\"id\":\"" + e.Id + "\",\"utc\":\"" + e.T.ToString("yyyy-MM-ddTHH:mm:ss.fff", c) + "\",\"corr\":" + e.Corrimiento.ToString("0.##", c)
                + ",\"raya\":" + e.Raya.ToString("0.00", c) + ",\"lado\":" + e.Lado + ",\"rueda\":" + (e.Rueda ? "true" : "false") + ",\"libro\":\"" + (e.PorOi ? "OI" : "vol") + "\",\"fuerza\":" + e.Fuerza.ToString("0", c)
                + (e.Tipo == "resultado" ? ",\"entrada\":" + e.Entrada.ToString("0.00", c) + ",\"resultado\":" + e.Resultado + ",\"puntos\":" + e.Puntos.ToString("0.00", c)
                                         : ",\"precio\":" + e.Precio.ToString("0.00", c) + ",\"traspaso\":" + e.Traspaso.ToString("0.00", c) + ",\"seg_en_zona\":" + e.SegEnZona.ToString("0", c) + ",\"vel60\":" + e.Vel60.ToString("0.00", c)
                                           + ",\"delta10\":" + e.Delta10.ToString("0", c) + ",\"neto\":" + e.Neto.ToString("0", c) + (e.Tipo == "gatillo" ? ",\"entrada\":" + e.Entrada.ToString("0.00", c) + ",\"stop\":" + e.Stop.ToString("0.00", c) + ",\"objetivo\":" + e.Objetivo.ToString("0.00", c) : ""))
                + "}";
        }
    }
}
