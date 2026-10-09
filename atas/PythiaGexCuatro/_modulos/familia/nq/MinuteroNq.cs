// MinuteroNq.cs — PythiaGex 4.1, modulo Familia, libro NQ (B3a, 08-10-2026).
// ILibroMinutero del libro NQ: port exacto de backtest_familia.minutos_nq tal como lo corre preview_niveles (historico, de a tandas de 240).
// Por minuto t (clave = ms/60000) con fut = ICinta.CierreConocido(t) (cierre de la ultima m2 CERRADA):
//   1. fut NaN -> null.   foto = la ultima con TsUtc <= t; null si no hay o si t - ts >= 300 s (VIDA_VIVA).
//   2. Corrimiento de CONTRATO (libros.construir rama viva): al entrar una foto nueva, si la m2 cerrada con fin <= ts + 120 s tiene fin a
//      <= 240 s de ts, se agrega (cierre - futuro de la foto) a un historial; med = mediana si hay >= 5; corr = med si |med| > 30, si no 0.
//      Con el libro sacado del mismo grafico (MNQZ6 / NQZ6) corr = 0. El historial es por TANDA (como Python) salvo OpcionesNq.CorrPorTanda=false.
//   3. S = fut - corr; filas del horizonte Hoy envejecidas a t; perfil por lado con Black-76 SIN descuento; null si no aporta nada.
//   4. Zero estandar C5 (NQ: paso 2.5, semi 300, cp 50) con la cache (foto, round(S/50)) de la tanda; en NQ: z + corr.
//   5. C2: oi_ok segun OiNq (salto de OI de Rithmic).  6. LibroMinuto: Fut = K + corr, +-3 % de fut, Mult 20, DatoUtc = ts de la foto.
// No hace E/S. No es seguro entre hilos: lo usa el hilo del motor. Si cambia la sesion de CME, se reinicia solo.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PythiaGexCuatro.Familia
{
    public sealed class OpcionesNq
    {
        /// <summary>"ahora" para C2 en vivo (esperando el salto). Default DateTime.UtcNow. El arnes historico pasa el fin de la sesion.</summary>
        public Func<DateTime> Reloj;
        /// <summary>true (default, paridad con la referencia): el historial del corrimiento se vacia en cada tanda de 240 minutos como en
        /// preview_niveles.historico. false: acumulado en toda la sesion (lo que dice la especificacion en palabras). Solo importa si |corr| > 30.</summary>
        public bool CorrPorTanda = true;
        /// <summary>null: calendario de feriados. Lista explicita de posferiados "yyyy-MM-dd" para reproducir la vista previa al pie de la letra.</summary>
        public IReadOnlyCollection<string> Posferiados;
        /// <summary>false (default, paridad con la referencia HISTORICA): cache del zero por (foto, round(S/50)) dentro de la tanda de 240 min.
        /// true: se recalcula cada minuto, que es lo que hacia la vista previa EN VIVO (su cache vivia dentro de cada llamada de un minuto).
        /// Medido 07/08-10: recalcular cada minuto cambia ZEST_NQ_vol en 61/76 minutos (hasta 0,30/2,67 pts) y ZEST_NQ_oi en 81/48 (hasta
        /// 29,08/0,69 pts: el zero salta de cruce cuando S se mueve dentro del mismo balde de 50).</summary>
        public bool ZeroCadaMinuto = false;
        public int VidaVivaS = 300;
        public double CorrMinimo = 30.0;
        public int CorrMuestrasMin = 5;
    }

    public sealed class MinuteroNq : ILibroMinutero
    {
        public string Libro => "NQ";
        public const double MULT = 20.0;

        private readonly ILibroNq _libro;
        private readonly ICinta _cinta;
        private readonly OpcionesNq _op;
        private string _sesion;
        private DateTime _ini, _fin;
        private long _claveIni;
        private OiNq _oi;
        private readonly CacheZero _cacheZ = new CacheZero();
        private readonly List<(FotoCadena F, double V)> _corrHist = new List<(FotoCadena, double)>();
        private long _tanda = long.MinValue;
        private long _ver = long.MinValue;
        private IReadOnlyList<FotoCadena> _fotos = Array.Empty<FotoCadena>();
        private long[] _tsMs = Array.Empty<long>();

        // ---- estado para la pestaña / el motor (solo lectura)
        public string Sesion => _sesion;
        public DateTime? OiViejoHasta { get; private set; }
        public string OiComo { get; private set; } = "";
        public double Corr { get; private set; }
        public int CorrMuestras => _corrHist.Count;
        public FotoCadena FotoUsada { get; private set; }
        public int NFotos => _fotos.Count;
        public CacheZero CacheZ => _cacheZ;

        public MinuteroNq(ILibroNq libro, ICinta cinta, string sesion = null, OpcionesNq opciones = null)
        {
            _libro = libro ?? throw new ArgumentNullException(nameof(libro));
            _cinta = cinta ?? throw new ArgumentNullException(nameof(cinta));
            _op = opciones ?? new OpcionesNq();
            if (_op.Reloj == null) _op.Reloj = () => DateTime.UtcNow;
            if (sesion != null) Reiniciar(sesion);
        }

        private void Reiniciar(string sesion)
        {
            _sesion = sesion;
            _ini = TiempoFam.IniSesion(sesion); _fin = TiempoFam.FinSesion(sesion);
            _claveIni = TiempoFam.Clave(_ini);
            _oi = new OiNq(sesion, _op.Posferiados);
            _cacheZ.Vaciar(); _corrHist.Clear(); _tanda = long.MinValue; _ver = long.MinValue;
            _fotos = Array.Empty<FotoCadena>(); _tsMs = Array.Empty<long>();
            OiViejoHasta = null; OiComo = ""; Corr = 0; FotoUsada = null;
        }

        private void RefrescarFotos()
        {
            long v = _libro.Version;
            if (v == _ver) return;
            _ver = v;
            var l = _libro.Fotos(_ini, _fin.AddSeconds(1)) ?? Array.Empty<FotoCadena>();
            var ts = new long[l.Count];
            for (int i = 0; i < l.Count; i++) ts[i] = TiempoFam.Ms(l[i].TsUtc);
            _fotos = l; _tsMs = ts;
        }

        public LibroMinuto Minuto(long clave, DateTime tUtc, double futMnq)
        {
            string ses = TiempoFam.Sesion(tUtc);
            if (ses != _sesion) Reiniciar(ses);
            // tanda de la vista previa (de a 240 minutos desde el inicio de la sesion): cache del zero y, en paridad, el corrimiento
            long tanda = CacheZero.TandaDe(clave, _claveIni);
            if (tanda != _tanda)
            {
                _tanda = tanda;
                _cacheZ.Tanda(tanda);
                if (_op.CorrPorTanda) _corrHist.Clear();
            }
            RefrescarFotos();
            // C2 (con lo visto hasta ahora)
            _oi.Avanzar(_fotos);
            var (hasta, como) = _oi.Hasta(_op.Reloj());
            OiViejoHasta = hasta; OiComo = como;

            if (double.IsNaN(futMnq)) return null;
            long tMs = TiempoFam.Ms(tUtc);
            int k = NumFam.UltimoMenorIgual(_tsMs, tMs);
            if (k < 0) return null;
            var f = _fotos[k];
            if ((tUtc - f.TsUtc).TotalSeconds >= _op.VidaVivaS) return null;

            // corrimiento de contrato (una muestra por foto nueva)
            if (_corrHist.Count == 0 || !ReferenceEquals(_corrHist[_corrHist.Count - 1].F, f))
            {
                double muestra = MuestraCorr(f);
                if (!double.IsNaN(muestra)) _corrHist.Add((f, muestra));
            }
            double med = 0.0;
            if (_corrHist.Count >= _op.CorrMuestrasMin)
            {
                var vs = new double[_corrHist.Count];
                for (int i = 0; i < vs.Length; i++) vs[i] = _corrHist[i].V;
                med = NumFam.Mediana(vs);
            }
            double corr = Math.Abs(med) > _op.CorrMinimo ? med : 0.0;
            Corr = corr;

            double S = futMnq - corr;
            var fl = FilasHoy.Armar(f, tUtc);
            var r = PerfilLado.Armar(fl, S, true);
            if (r == null) return null;
            FotoUsada = f;
            var (zv, zo) = _op.ZeroCadaMinuto
                ? ZeroEstandarFam.Calcular(fl, S, true, ZeroEstandarFam.NQ)
                : _cacheZ.Obtener(f, (long)NumFam.PyRound0(S / 50.0), 0L, () => ZeroEstandarFam.Calcular(fl, S, true, ZeroEstandarFam.NQ));
            bool oiOk = !(hasta != null && _ini <= tUtc && tUtc < hasta.Value);
            string conv = Math.Abs(corr) < 1e-9
                ? "mismo contrato (corrimiento 0)"
                : "corrimiento " + corr.ToString("0.00", CultureInfo.InvariantCulture) + " (mediana de " + _corrHist.Count + " fotos)";
            return ArmadoLibroMinuto.Armar("NQ", clave, futMnq, S, corr, false, r, zv + corr, zo + corr, f.TsUtc, oiOk, MULT, conv, false);
        }

        /// <summary>V.cierre_conocido(vs, ts + 120 s): la ultima m2 cerrada con fin &lt;= ts + 120 s (= apertura &lt;= ts); vale si ts - fin &lt;= 240 s.
        /// Devuelve cierre - futuro de la foto, o NaN.</summary>
        private double MuestraCorr(FotoCadena f)
        {
            double fut = !double.IsNaN(f.FuturoFoto) ? f.FuturoFoto : f.Spot;
            if (double.IsNaN(fut)) return double.NaN;
            long ts = TiempoFam.Ms(f.TsUtc);
            var velas = _cinta.VelasM2(f.TsUtc.AddMinutes(-10), f.TsUtc.AddMinutes(2));
            if (velas == null || velas.Count == 0) return double.NaN;
            long mejor = long.MinValue; double c = double.NaN;
            foreach (var v in velas)
                if (v.Ms <= ts && v.Ms > mejor) { mejor = v.Ms; c = v.C; }
            if (mejor == long.MinValue || double.IsNaN(c)) return double.NaN;
            long finVela = mejor + TiempoFam.MS_M2;
            if (ts - finVela > 240_000L) return double.NaN;
            return c - fut;
        }
    }
}
