// MinuteroEs.cs — PythiaGex 4.1, modulo Familia, libro ES (B3a, 08-10-2026).
// ILibroMinutero del libro ES: port exacto de backtest_familia.minutos_es tal como lo corre preview_niveles (historico, de a tandas de 240).
// Por minuto t (clave = ms/60000) con fut = ICinta.CierreConocido(t) (cierre de la ultima m2 CERRADA):
//   1. fut NaN -> null.   foto = la ultima con TsUtc <= t; null si no hay o si t - ts >= 300 s (VIDA_VIVA).
//   2. Corrimiento de CONTRATO (libros.construir rama viva): al entrar una foto nueva, si la m2 cerrada con fin <= ts + 120 s tiene fin a
//      <= 240 s de ts, se agrega (cierre - futuro de la foto) a un historial; med = mediana si hay >= 5; corr = med si |med| > CorrMinimo
//      (5.0: 7,5; la 4.1: 30 de NQ), si no 0. Con el libro sacado del mismo grafico (MESZ6 / opciones de ESZ6: MES y ES cotizan igual)
//      corr = 0. El historial es por TANDA (como Python) salvo OpcionesEs.CorrPorTanda=false.
//   3. S = fut - corr; filas del horizonte Hoy envejecidas a t; perfil por lado con Black-76 SIN descuento; null si no aporta nada.
//   4. Zero estandar C5 (5.0, ES: paso 0.5, semi 75, cp 12.5; la 4.1: 2.5 / 300 / 50) con la cache (foto, round(S/cp)) de la tanda; z + corr.
//   5. C2: oi_ok segun OiEs (salto de OI de Rithmic).  6. LibroMinuto: Fut = K + corr, +-3 % de fut, Mult 50 (ES; la 4.1: NQ 20), DatoUtc = ts.
// No hace E/S. No es seguro entre hilos: lo usa el hilo del motor. Si cambia la sesion de CME, se reinicia solo.
// 5.0 (27-10-2026): la sesion sigue el modo del motor (OpcionesEs.Corregida; por defecto el de AdaptadoresFamilia.SesionUtcFija, que el
// integrador pone antes de construir): corregida = 18:00-17:00 NY. La 4.1 usaba SIEMPRE la de UTC fijo (22:00-21:00): en horario de verano
// es la misma, pero desde el 01-11 el libro ES faltaria de 16:05 a 17:00 NY (las fotos de despues de las 21:00 UTC quedaban afuera).
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PythiaGexCinco.Familia
{
    public sealed class OpcionesEs
    {
        /// <summary>"ahora" para C2 en vivo (esperando el salto). Default DateTime.UtcNow. El arnes historico pasa el fin de la sesion.</summary>
        public Func<DateTime> Reloj;
        /// <summary>true (default, paridad con la referencia): el historial del corrimiento se vacia en cada tanda de 240 minutos como en
        /// preview_niveles.historico. false: acumulado en toda la sesion (lo que dice la especificacion en palabras). Solo importa si |corr| > 30.</summary>
        public bool CorrPorTanda = true;
        /// <summary>null: calendario de feriados. Lista explicita de posferiados "yyyy-MM-dd" para reproducir la vista previa al pie de la letra.</summary>
        public IReadOnlyCollection<string> Posferiados;
        /// <summary>false (paridad con la referencia HISTORICA): cache del zero por (foto, round(S/cp)) dentro de la tanda de 240 min.
        /// true: se recalcula cada minuto, que es lo que hacia la vista previa EN VIVO (su cache vivia dentro de cada llamada de un minuto).
        /// Medido en la 4.1 (NQ) el 07/08-10: recalcular cada minuto cambia ZEST_NQ_vol en 61/76 minutos (hasta 0,30/2,67 pts) y ZEST_NQ_oi
        /// en 81/48 (hasta 29,08/0,69 pts: el zero salta de cruce cuando S se mueve dentro del mismo balde de 50).</summary>
        /// <summary>4.1.8 (12-10-2026): calcular el zero FRESCO cada minuto en vez de reusar el de la cache.
        /// La cache memoriza el resultado del PRIMER minuto que cae en cada clave (ZeroEstandar.cs:103) y el valor
        /// NO es funcion de la clave: dentro de una misma clave el envejecimiento mueve T minuto a minuto y S puede
        /// enganchar otro cruce. Medido en la 4.1 sobre la sesion 2026-10-09 (1.378 minutos, codigo real compilado):
        /// ZEST_QQQ_vol p50 0,014 / p95 0,137 / MAX 115,6 pts en el eje MNQ, y el valor dibujado de QQQ salia de la
        /// cache en el 99,8 % de los minutos. Costo de no cachear: ~5,0 ms por minuto los tres libros (~7 s por
        /// sesion; en la 5.0 la grilla del zero tiene 301 nodos contra 241: ~1,25 veces). Sin esto el pasado no es
        /// reproducible y el operador leeria un zero que no rigio.</summary>
        public bool ZeroCadaMinuto = true;
        public int VidaVivaS = 300;
        /// <summary>5.0 (27-10-2026): 7,5 pts de ES (la 4.1: 30 de NQ, /4). Separa "mismo contrato" (la muestra es la deriva de 0-2 min: medido
        /// MES m1 contra el futuro de Rithmic, mediana -0,01, p95 0,73, MAD 0,20) de "otro trimestre" (ES U6 -> Z6: ~65-70 pts en septiembre;
        /// el carry de un trimestre de ES es ~50-70 pts, el de NQ ~240).</summary>
        public double CorrMinimo = 7.5;
        public int CorrMuestrasMin = 5;
        /// <summary>5.0 (27-10-2026): true = sesion de CME en hora de NY (18:00 NY de la vispera a 17:00 NY, SesionFamilia corregida, la del motor
        /// por defecto); false = la de la vista previa (22:00-21:00 UTC fijo, la de la 4.1). null (default) = !AdaptadoresFamilia.SesionUtcFija al
        /// construir. En horario de verano de EE.UU. dan lo mismo minuto a minuto.</summary>
        public bool? Corregida;
    }

    public sealed class MinuteroEs : ILibroMinutero
    {
        public string Libro => "ES";
        /// <summary>5.0 (27-10-2026): USD por punto de las opciones sobre el futuro ES (CME: 50; la 4.1 tenia NQ 20). C1: toda suma entre libros en
        /// USD usa ES x50, SPX x100, SPY x100 (ReglasFam.Escala = MULT / 100 = 0,5).</summary>
        public const double MULT = 50.0;

        private readonly ILibroEs _libro;
        private readonly ICinta _cinta;
        private readonly OpcionesEs _op;
        private readonly bool _corregida;
        private string _sesion;
        private DateTime _ini, _fin;
        private long _claveIni;
        private OiEs _oi;
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
        /// <summary>5.0: true = sesion en hora de NY (ver OpcionesEs.Corregida).</summary>
        public bool Corregida => _corregida;

        public MinuteroEs(ILibroEs libro, ICinta cinta, string sesion = null, OpcionesEs opciones = null)
        {
            _libro = libro ?? throw new ArgumentNullException(nameof(libro));
            _cinta = cinta ?? throw new ArgumentNullException(nameof(cinta));
            _op = opciones ?? new OpcionesEs();
            if (_op.Reloj == null) _op.Reloj = () => DateTime.UtcNow;
            _corregida = _op.Corregida ?? !AdaptadoresFamilia.SesionUtcFija;
            if (sesion != null) Reiniciar(sesion);
        }

        /// <summary>5.0: la sesion de t en el modo del minutero (corregida: SesionFamilia en hora de NY; si no: fecha(t + 2 h), la de la 4.1).</summary>
        private string SesionDe(DateTime tUtc) => _corregida ? SesionFamilia.De(tUtc, true).Dia : TiempoFam.Sesion(tUtc);

        private void Reiniciar(string sesion)
        {
            _sesion = sesion;
            if (_corregida) { var s = SesionFamilia.DelDia(sesion, true); _ini = s.IniUtc; _fin = s.FinUtc; }
            else { _ini = TiempoFam.IniSesion(sesion); _fin = TiempoFam.FinSesion(sesion); }
            _claveIni = TiempoFam.Clave(_ini);
            _oi = new OiEs(sesion, _op.Posferiados, _corregida ? _ini : (DateTime?)null);
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
            string ses = SesionDe(tUtc);
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
            PerfilLado.CargaPorStrike(f, tUtc, S, true, out var kCg, out var oiCg, out var oiCgC, out var oiCgP, out var gxCg);   // 4.1.7: TODOS los vencimientos
            var r = PerfilLado.Armar(fl, S, true);
            if (r == null) return null;
            FotoUsada = f;
            // 5.0: el balde de la cache es el mismo centro de la grilla (cp 12,5; la 4.1: round(S/50) con cp 50), como en LibroMinuteroSpx
            var (zv, zo) = _op.ZeroCadaMinuto
                ? ZeroEstandarFam.Calcular(fl, S, true, ZeroEstandarFam.ES)
                : _cacheZ.Obtener(f, (long)NumFam.PyRound0(S / ZeroEstandarFam.ES.CentroPaso), 0L, () => ZeroEstandarFam.Calcular(fl, S, true, ZeroEstandarFam.ES));
            bool oiOk = !(hasta != null && _ini <= tUtc && tUtc < hasta.Value);
            string conv = Math.Abs(corr) < 1e-9
                ? "mismo contrato (corrimiento 0)"
                : "corrimiento " + corr.ToString("0.00", CultureInfo.InvariantCulture) + " (mediana de " + _corrHist.Count + " fotos)";
            return ArmadoLibroMinuto.Armar("ES", clave, futMnq, S, corr, false, r, zv + corr, zo + corr, f.TsUtc, oiOk, MULT, conv, false, kCg, oiCg, oiCgC, oiCgP, gxCg);
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
