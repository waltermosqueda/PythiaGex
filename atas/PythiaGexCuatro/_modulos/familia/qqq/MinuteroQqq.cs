// MinuteroQqq.cs — PythiaGex 4.1, modulo Familia QQQ (B3c). 08-10-2026.
// ILibroMinutero de QQQ = backtest_familia.minutos_cboe(libro="QQQ", variante="sync") minuto a minuto:
//   foto k = la ultima con generado <= t (de las procesadas por ConversionQqq, sin las de la pausa de CME); vale si t < vigencia (C9);
//   razon = la C8 de ESA foto (NaN -> no hay libro); se descarta si razon_contrato != contrato(t) (roll);
//   S = fut / razon; filas_hoy (C10) -> perfil_cp (Black-Scholes r 0.0375) -> Fut = K * razon -> solo |Fut - fut| <= 3 % de fut;
//   zero estandar C5 (paso 0.05 / semi 7.5 / centro 1.0) con la cache de la vista previa: clave (foto, floor((t - generado)/900 s), round(S)),
//   dentro de la tanda de 240 minutos (CacheZero de comun); zv, zo -> NQ multiplicando por la razon.
//   OiOk = oi_fresco (solo marca "(OI 2s)", no apaga nada: oi_vale(QQQ) = True en la primaria).
// Usa el codigo comun de B3a (FilasHoy, PerfilLado, ZeroEstandarFam, CacheZero, NumFam, TiempoFam). Sin ATAS, sin E/S.
// Hilo: uno solo (el del motor). Minuto se llama en orden creciente de minuto (contrato ILibroMinutero).
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PythiaGexCuatro.Familia.Qqq
{
    public sealed class MinuteroQqq : ILibroMinutero
    {
        public const string LIBRO = "QQQ";
        public const double MULT = 100.0;                 // C1: USD por punto de QQQ x 100 acciones
        public const double FILTRO = 0.03;                // BM: strikes a <= 3 % de fut
        public const double PODA_H = 36;                  // se suelta la cadena de las fotos de mas de 36 h (queda su estado C8)
        private const long TICKS_M2 = 120L * TimeSpan.TicksPerSecond;   // vela m2 (las velas de la cinta van alineadas a 0 UTC)

        private readonly IFuenteCboe _cboe;
        private readonly ICinta _cinta;
        private readonly OpcionesQqq _op;
        private readonly ConversionQqq _conv;
        private readonly List<FotoQqq> _fotos = new List<FotoQqq>();
        private readonly List<long> _genMs = new List<long>();
        private readonly HashSet<DateTime> _tsVistos = new HashSet<DateTime>();
        private readonly Queue<DateTime> _tsOrden = new Queue<DateTime>();
        private readonly CacheZero _cache = new CacheZero();
        private DateTime _desde;
        private DateTime _ultimoGen = DateTime.MinValue;
        private long _verVista = long.MinValue;
        private int _podadas;

        /// <summary>Se llama con cada foto procesada (para que el motor anote muestras-QQQ-&lt;sesion&gt;.jsonl). Opcional.</summary>
        public Action<FotoQqq> AlProcesar;
        /// <summary>Si la cinta todavia no llego a ts - 900 s, la foto espera (para no perder la muestra). Pasado este tiempo desde su
        /// 'generado' se procesa igual (cinta caida: sin muestra, la razon se hereda de las ultimas 24, como el Python).</summary>
        public double EsperaCintaMaxS = 1200;

        /// <param name="desdeUtc">desde cuando pedir fotos a la fuente (la vista previa: 6 dias de calendario antes de la sesion, 22:00 UTC).</param>
        public MinuteroQqq(IFuenteCboe cboe, ICinta cinta, OpcionesQqq op = null, DateTime? desdeUtc = null)
        {
            _cboe = cboe ?? throw new ArgumentNullException(nameof(cboe));
            _cinta = cinta ?? throw new ArgumentNullException(nameof(cinta));
            _op = op ?? new OpcionesQqq();
            _conv = new ConversionQqq(_op);
            _desde = desdeUtc ?? DateTime.MinValue;
        }

        public string Libro => LIBRO;
        public ConversionQqq Conversion => _conv;
        public IReadOnlyList<FotoQqq> Fotos => _fotos;
        public FotoQqq Ultima => _fotos.Count > 0 ? _fotos[_fotos.Count - 1] : null;
        public int CalculosZero => _cache.Calculos;
        public int AciertosZero => _cache.Aciertos;

        /// <summary>Lee de la fuente las fotos nuevas (generado creciente) y les corre C8/oi_fresco/C9. ahoraUtc = el minuto que se esta
        /// calculando (para la espera de la cinta).</summary>
        public int Sincronizar(DateTime ahoraUtc)
        {
            long ver = _cboe.Version;
            if (ver == _verVista && _pendientes.Count == 0) return 0;
            int n = 0;
            if (ver != _verVista)
            {
                var desde = _ultimoGen > _desde ? _ultimoGen : _desde;
                var lista = _cboe.Fotos(LIBRO, desde, DateTime.MaxValue);
                _verVista = ver;
                if (lista != null)
                    foreach (var f in lista)
                    {
                        if (f == null || f.Filas == null) continue;   // 4.1 integracion: las fotos 'livianas' de B2 (sin filas, > 30 h) igual cuentan para C8
                        var gen = HoraQqq.Seg(f.GeneradoUtc);
                        if (gen <= _ultimoGenEncolado) continue;                  // vista previa en vivo: generado <= el ultimo -> se ignora
                        if (_conv.FueraDeSesion(gen)) continue;                   // pausa de CME (21:00-22:00 UTC | 17:00-18:00 NY)
                        _pendientes.Enqueue(f);
                        _ultimoGenEncolado = gen;
                    }
            }
            while (_pendientes.Count > 0)
            {
                var f = _pendientes.Peek();
                var ts = HoraQqq.Seg(f.TsUtc);
                var tSpot = ts.AddSeconds(-ConversionQqq.RETRASO_S);
                // 4.1.0: la foto espera mientras su precio (MNQ en ts - 900 s) todavia se puede completar: sin tick en los 120 s previos, el precio
                // sale de la vela m2 que contiene tSpot, y esa vela recien vale cuando CIERRA (llega un tick despues de su fin). Antes se procesaba
                // apenas la cinta pasaba tSpot y, en un hueco de la cinta, la muestra quedaba NaN para siempre (el historico si la tiene: razon
                // distinta ~35 min en el vivo simulado del 08-10). Con precio por tick (lo normal) no espera nada; tope: EsperaCintaMaxS.
                var cierreVela = new DateTime((tSpot.Ticks / TICKS_M2) * TICKS_M2 + TICKS_M2, DateTimeKind.Utc);
                double precio = _cinta.Precio(tSpot);
                if (double.IsNaN(precio) && _cinta.UltimoTickUtc < cierreVela && (ahoraUtc - HoraQqq.Seg(f.GeneradoUtc)).TotalSeconds < EsperaCintaMaxS) break;
                _pendientes.Dequeue();
                if (_tsVistos.Contains(ts)) continue;                              // misma cadena (ts) ya vista: gana la mas temprana
                if (f.GeneradoUtc.Ticks % TimeSpan.TicksPerSecond != 0)            // la cuenta envejece desde 'generado' en segundos, como el archivo
                    f = new FotoCadena { Libro = f.Libro, GeneradoUtc = HoraQqq.Seg(f.GeneradoUtc), TsUtc = f.TsUtc, DatoUtc = f.DatoUtc, Spot = f.Spot, Dias = f.Dias, Filas = f.Filas,
                                         EsFuturo = f.EsFuturo, OiTotal = f.OiTotal, BaseCruda = f.BaseCruda, BaseErrorTicks = f.BaseErrorTicks, CierreAnterior = f.CierreAnterior, FuturoFoto = f.FuturoFoto };
                var r = _conv.Observar(f, precio);
                _fotos.Add(r);
                _genMs.Add(TiempoFam.Ms(r.GeneradoUtc));
                _ultimoGen = r.GeneradoUtc;
                _tsVistos.Add(ts); _tsOrden.Enqueue(ts);
                while (_tsOrden.Count > 2000) _tsVistos.Remove(_tsOrden.Dequeue());
                n++;
                try { AlProcesar?.Invoke(r); } catch { }
            }
            return n;
        }
        private readonly Queue<FotoCadena> _pendientes = new Queue<FotoCadena>();
        private DateTime _ultimoGenEncolado = DateTime.MinValue;

        /// <summary>La foto vigente (con su estado de conversion) para el minuto t: la ultima procesada con generado &lt;= t. null si no hay.</summary>
        public FotoQqq FotoEn(DateTime tUtc)
        {
            int k = NumFam.UltimoMenorIgual(_genMs, TiempoFam.Ms(tUtc));
            return k >= 0 ? _fotos[k] : null;
        }

        public LibroMinuto Minuto(long clave, DateTime tUtc, double futMnq)
        {
            var t = HoraQqq.Utc(tUtc);
            Sincronizar(t);
            if (double.IsNaN(futMnq)) return null;
            int k = NumFam.UltimoMenorIgual(_genMs, TiempoFam.Ms(t));
            if (k < 0) return null;
            var f = _fotos[k];
            Podar(k, t);
            if (f.Foto == null) return null;
            if (t >= f.VigenteHastaUtc) return null;                               // C9
            double rz = f.Razon;
            if (double.IsNaN(rz) || !string.Equals(f.RazonContrato, _op.ContratoDe(t) ?? "", StringComparison.Ordinal)) return null;   // C8 + roll
            double fut = futMnq;
            double S = fut / rz;
            var fl = FilasHoy.Armar(f.Foto, t);
            var r = PerfilLado.Armar(fl, S, false);
            if (r == null) return null;

            // C5 con la cache de la vista previa (tanda de 240 minutos desde el inicio de la sesion)
            long claveIni = TiempoFam.Clave(TiempoFam.IniSesion(TiempoFam.Sesion(t)));
            _cache.Tanda(CacheZero.TandaDe(clave, claveIni));
            long envB = (long)Math.Floor((t - f.GeneradoUtc).TotalSeconds / 900.0);
            long bS = (long)Math.Round(S / 1.0, MidpointRounding.ToEven);
            var (zv, zo) = _cache.Obtener(f, envB, bS, () => ZeroEstandarFam.Calcular(fl, S, false, ZeroEstandarFam.QQQ));

            // BM: Fut = K * razon; solo |Fut - fut| <= fut * 0.03 (orden por K = orden por Fut)
            int n = 0;
            var fu = new double[r.N];
            for (int i = 0; i < r.N; i++) { fu[i] = r.K[i] * rz; if (Math.Abs(fu[i] - fut) <= fut * FILTRO) n++; }
            var b = new LibroMinuto
            {
                Libro = LIBRO, Clave = clave, FutMnq = fut, S = S, Conv = rz, PorRazon = true,
                Fut = new double[n], K = new double[n], GvC = new double[n], GvP = new double[n], GoC = new double[n], GoP = new double[n], Gv = new double[n], Go = new double[n],
                ZeroVol = double.IsNaN(zv) ? double.NaN : zv * rz, ZeroOi = double.IsNaN(zo) ? double.NaN : zo * rz,
                DatoUtc = f.DatoUtc, OiOk = !f.OiViejo, Mult = MULT, Congelada = f.Congelada,
                ConvTexto = "razon " + rz.ToString("0.0000", CultureInfo.InvariantCulture) + " (mediana de " + f.RazonN.ToString(CultureInfo.InvariantCulture) + " muestras sincronizadas)",
            };
            int j = 0;
            for (int i = 0; i < r.N; i++)
            {
                if (!(Math.Abs(fu[i] - fut) <= fut * FILTRO)) continue;
                b.Fut[j] = fu[i]; b.K[j] = r.K[i]; b.GvC[j] = r.GvC[i]; b.GvP[j] = r.GvP[i]; b.GoC[j] = r.GoC[i]; b.GoP[j] = r.GoP[i]; b.Gv[j] = r.Gv[i]; b.Go[j] = r.Go[i];
                j++;
            }
            return b;
        }

        /// <summary>Estado de la fuente para la pestaña (FuenteEstado del contrato) a partir del libro del minuto y su foto.</summary>
        public FuenteEstado Fuente(LibroMinuto b, DateTime tUtc)
        {
            var f = FotoEn(tUtc);
            if (b == null || f == null) return new FuenteEstado { Libro = LIBRO, Texto = "QQQ: sin cadena vigente", DatoUtc = f?.DatoUtc ?? DateTime.MinValue };
            return new FuenteEstado { Libro = LIBRO, Texto = b.ConvTexto, ConvValor = b.Conv, DatoUtc = f.DatoUtc, Congelada = f.Congelada, OiOk = !f.OiViejo };
        }

        /// <summary>Suelta la cadena de las fotos de mas de 36 h antes de t (salvo la vigente y la anterior): su estado C8 queda.</summary>
        private void Podar(int k, DateTime t)
        {
            var lim = t.AddHours(-PODA_H);
            while (_podadas < k - 1 && _fotos[_podadas].GeneradoUtc < lim) { _fotos[_podadas].Foto = null; _podadas++; }
        }
    }
}
