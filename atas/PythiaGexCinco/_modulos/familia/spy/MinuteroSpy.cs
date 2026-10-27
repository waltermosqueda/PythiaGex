// MinuteroSpy.cs — PythiaGex 4.1, modulo Familia SPY (B3c). 08-10-2026.
// ILibroMinutero de SPY = backtest_familia.minutos_cboe(libro="SPY", variante="sync") minuto a minuto:
//   foto k = la ultima con generado <= t (de las procesadas por ConversionSpy, sin las de la pausa de CME); vale si t < vigencia (C9);
//   razon = la C8 de ESA foto (NaN -> no hay libro); se descarta si razon_contrato != contrato(t) (roll);
//   S = fut / razon; filas_hoy (C10) -> perfil_cp (Black-Scholes r 0.0375) -> Fut = K * razon -> solo |Fut - fut| <= 3 % de fut;
//   zero estandar C5 (paso 0.05 / semi 7.5 / centro 1.0) con la cache de la vista previa: clave (foto, floor((t - generado)/900 s), round(S)),
//   dentro de la tanda de 240 minutos (CacheZero de comun); zv, zo -> ES multiplicando por la razon.
//   OiOk = oi_fresco (solo marca "(OI 2s)", no apaga nada: oi_vale(SPY) = True en la primaria).
// Usa el codigo comun de B3a (FilasHoy, PerfilLado, ZeroEstandarFam, CacheZero, NumFam, TiempoFam). Sin ATAS, sin E/S.
// Hilo: uno solo (el del motor). Minuto se llama en orden creciente de minuto (contrato ILibroMinutero).
//
// 5.0 (27-10-2026, P3): SPY en un grafico de MES. Lo que cambia y por que (MEDICIONES_ES.md §2-3):
//   * MULT 100 (opciones de SPY: 100 acciones; en la familia monto x 100/100 = 1, igual que QQQ). Strikes de SPY cada 1 USD cerca del dinero
//     (hasta +46/-44 USD en los diarios), o sea ~10,08 pts de ES por strike con la razon de 10,08: un nivel de SPY cae en K x razon, que NO
//     es un multiplo de 5 de ES (la confluencia con ES/SPX la decide ReglasFam.CONF_PTS = 3).
//   * zero estandar con ZeroEstandarFam.SPY (paso 0,05 USD = 0,5 pt de ES; +-7,5 USD = +-75 pts; centro de la clave de la cache 1 USD = su
//     CentroPaso, ya no escrito a mano). El radio de seleccion (20 pts de ES) lo pone SeleccionFam.RadioDe("SPY") en las series.
//   * conversion: la C8 de ConversionSpy con las horas [04:00, 20:00) NY, la cota [9,95, 10,25] y el reinicio del ex-dividendo de SPY. Si el
//     corte del ex-dividendo (04:00 NY de ese dia) ya paso a la hora del minuto pero la foto vigente es de antes, su razon todavia trae el
//     dividendo adentro: no hay libro SPY ese minuto. Hasta juntar 5 muestras nuevas tampoco. MotivoSinLibro dice por que, con numeros.
//   * el texto de la conversion dice la razon, cuantas muestras, la ventana horaria y, si la ventana se reinicio por el ex-dividendo, desde cuando.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PythiaGexCinco.Familia.Spy
{
    public sealed class MinuteroSpy : ILibroMinutero
    {
        public const string LIBRO = "SPY";
        public const double MULT = 100.0;                 // C1: USD por punto de SPY x 100 acciones
        public const double FILTRO = 0.03;                // BM: strikes a <= 3 % de fut
        public const double PODA_H = 36;                  // se suelta la cadena de las fotos de mas de 36 h (queda su estado C8)
        private const long TICKS_M2 = 120L * TimeSpan.TicksPerSecond;   // vela m2 (las velas de la cinta van alineadas a 0 UTC)

        private readonly IFuenteCboe _cboe;
        private readonly ICinta _cinta;
        private readonly OpcionesSpy _op;
        private readonly ConversionSpy _conv;
        private readonly List<FotoSpy> _fotos = new List<FotoSpy>();
        private readonly List<long> _genMs = new List<long>();
        private readonly HashSet<DateTime> _tsVistos = new HashSet<DateTime>();
        private readonly Queue<DateTime> _tsOrden = new Queue<DateTime>();
        private readonly CacheZero _cache = new CacheZero();
        private DateTime _desde;
        private DateTime _ultimoGen = DateTime.MinValue;
        private long _verVista = long.MinValue;
        private int _podadas;

        /// <summary>Se llama con cada foto procesada (para que el motor anote muestras-SPY-&lt;sesion&gt;.jsonl). Opcional.</summary>
        public Action<FotoSpy> AlProcesar;
        /// <summary>Si la cinta todavia no llego a ts - 900 s, la foto espera (para no perder la muestra). Pasado este tiempo desde su
        /// 'generado' se procesa igual (cinta caida: sin muestra, la razon se hereda de las ultimas 24, como el Python).</summary>
        public double EsperaCintaMaxS = 1200;

        /// <summary>5.0: por que el ultimo Minuto devolvio null ("" si devolvio libro). Para la pestaña y el log.</summary>
        public string UltimoMotivo { get; private set; } = "";

        /// <param name="desdeUtc">desde cuando pedir fotos a la fuente (la vista previa: 6 dias de calendario antes de la sesion, 22:00 UTC).</param>
        public MinuteroSpy(IFuenteCboe cboe, ICinta cinta, OpcionesSpy op = null, DateTime? desdeUtc = null)
        {
            _cboe = cboe ?? throw new ArgumentNullException(nameof(cboe));
            _cinta = cinta ?? throw new ArgumentNullException(nameof(cinta));
            _op = op ?? new OpcionesSpy();
            _conv = new ConversionSpy(_op);
            _desde = desdeUtc ?? DateTime.MinValue;
        }

        public string Libro => LIBRO;
        public ConversionSpy Conversion => _conv;
        public IReadOnlyList<FotoSpy> Fotos => _fotos;
        public FotoSpy Ultima => _fotos.Count > 0 ? _fotos[_fotos.Count - 1] : null;
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
                        var gen = HoraSpy.Seg(f.GeneradoUtc);
                        if (gen <= _ultimoGenEncolado) continue;                  // vista previa en vivo: generado <= el ultimo -> se ignora
                        if (_conv.FueraDeSesion(gen)) continue;                   // pausa de CME (21:00-22:00 UTC | 17:00-18:00 NY)
                        _pendientes.Enqueue(f);
                        _ultimoGenEncolado = gen;
                    }
            }
            while (_pendientes.Count > 0)
            {
                var f = _pendientes.Peek();
                var ts = HoraSpy.Seg(f.TsUtc);
                var tSpot = ts.AddSeconds(-ConversionSpy.RETRASO_S);
                // 4.1.0: la foto espera mientras su precio (MES en ts - 900 s) todavia se puede completar: sin tick en los 120 s previos, el precio
                // sale de la vela m2 que contiene tSpot, y esa vela recien vale cuando CIERRA (llega un tick despues de su fin). Antes se procesaba
                // apenas la cinta pasaba tSpot y, en un hueco de la cinta, la muestra quedaba NaN para siempre (el historico si la tiene: razon
                // distinta ~35 min en el vivo simulado del 08-10). Con precio por tick (lo normal) no espera nada; tope: EsperaCintaMaxS.
                var cierreVela = new DateTime((tSpot.Ticks / TICKS_M2) * TICKS_M2 + TICKS_M2, DateTimeKind.Utc);
                double precio = _cinta.Precio(tSpot);
                if (double.IsNaN(precio) && _cinta.UltimoTickUtc < cierreVela && (ahoraUtc - HoraSpy.Seg(f.GeneradoUtc)).TotalSeconds < EsperaCintaMaxS) break;
                _pendientes.Dequeue();
                if (_tsVistos.Contains(ts)) continue;                              // misma cadena (ts) ya vista: gana la mas temprana
                if (f.GeneradoUtc.Ticks % TimeSpan.TicksPerSecond != 0)            // la cuenta envejece desde 'generado' en segundos, como el archivo
                    f = new FotoCadena { Libro = f.Libro, GeneradoUtc = HoraSpy.Seg(f.GeneradoUtc), TsUtc = f.TsUtc, DatoUtc = f.DatoUtc, Spot = f.Spot, Dias = f.Dias, Filas = f.Filas,
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
        public FotoSpy FotoEn(DateTime tUtc)
        {
            int k = NumFam.UltimoMenorIgual(_genMs, TiempoFam.Ms(tUtc));
            return k >= 0 ? _fotos[k] : null;
        }

        /// <summary>5.0: el ex-dividendo que la foto todavia no tiene en cuenta a la hora t (su corte ya paso a t, la razon de la foto es de antes);
        /// MinValue si ninguno.</summary>
        private DateTime CortePendiente(FotoSpy f, DateTime tUtc)
        {
            if (!_op.ReinicioExDividendo || f == null) return DateTime.MinValue;
            var c = ExDividendoSpy.UltimoCorte(tUtc);
            return c > f.ExDivUtc ? c : DateTime.MinValue;
        }

        /// <summary>5.0: el reinicio del ex-dividendo pesa en esta razon (corte de hace menos de 24 h y la ventana todavia sin llenar de muestras nuevas).</summary>
        private static bool ExDivReciente(FotoSpy f, DateTime tUtc) =>
            f != null && f.ExDivUtc != DateTime.MinValue && (tUtc - f.ExDivUtc).TotalHours < 24 && f.MuestrasDesdeExDiv < ConversionSpy.VENTANA;

        /// <summary>5.0: por que no hay libro SPY en el minuto t (con numeros); "" si lo hay o si no se sabe. No arma nada: solo mira el estado.</summary>
        public string MotivoSinLibro(DateTime tUtc)
        {
            var inv = CultureInfo.InvariantCulture;
            var t = HoraSpy.Utc(tUtc);
            var f = FotoEn(t);
            if (f == null) return "SPY: sin cadena de CBOE todavia";
            if (f.Foto == null) return "SPY: la cadena vigente ya no tiene filas en memoria (de mas de " + PODA_H.ToString("0", inv) + " h)";
            if (t >= f.VigenteHastaUtc) return "SPY: la ultima cadena (" + f.GeneradoUtc.ToString("MM-dd HH:mm", inv) + "Z) vencio a las " + f.VigenteHastaUtc.ToString("HH:mm", inv) + "Z (C9: 25 min)";
            var cp = CortePendiente(f, t);
            if (cp != DateTime.MinValue)
                return "SPY: ex-dividendo de SPY del " + ExDividendoSpy.Dia(cp) + ": la razon se reinicia a las 04:00 NY y la cadena vigente es de antes (" + f.GeneradoUtc.ToString("HH:mm", inv) + "Z): SPY no se dibuja hasta juntar " + ConversionSpy.MIN_N + " muestras nuevas";
            if (double.IsNaN(f.Razon))
            {
                if (f.ExDivUtc != DateTime.MinValue && (t - f.ExDivUtc).TotalHours < 24 && f.MuestrasDesdeExDiv < ConversionSpy.MIN_N)
                    return "SPY: ex-dividendo de SPY del " + ExDividendoSpy.Dia(f.ExDivUtc) + ": razon recien reiniciada a las 04:00 NY, " + f.MuestrasDesdeExDiv + " de " + ConversionSpy.MIN_N
                           + " muestras nuevas: SPY no se dibuja hasta juntar " + ConversionSpy.MIN_N;
                return "SPY: sin razon ES/SPY: " + f.RazonN + " de " + ConversionSpy.MIN_N + " muestras sincronizadas (MES de ts - 900 s / spot de SPY, 04:00-20:00 NY)"
                       + (string.IsNullOrEmpty(f.Descarte) ? "" : "; la ultima foto no dio muestra: " + f.Descarte);
            }
            if (!string.Equals(f.RazonContrato, _op.ContratoDe(t) ?? "", StringComparison.Ordinal))
                return "SPY: la razon es del contrato " + f.RazonContrato + " y el grafico esta en " + (_op.ContratoDe(t) ?? "?") + " (roll): se rehace con muestras nuevas";
            return "";
        }

        public LibroMinuto Minuto(long clave, DateTime tUtc, double futMnq)
        {
            var t = HoraSpy.Utc(tUtc);
            Sincronizar(t);
            if (double.IsNaN(futMnq)) { UltimoMotivo = "SPY: sin precio del MES en el minuto"; return null; }
            int k = NumFam.UltimoMenorIgual(_genMs, TiempoFam.Ms(t));
            if (k < 0) { UltimoMotivo = "SPY: sin cadena de CBOE todavia"; return null; }
            var f = _fotos[k];
            Podar(k, t);
            if (f.Foto == null || t >= f.VigenteHastaUtc) { UltimoMotivo = MotivoSinLibro(t); return null; }   // C9
            if (CortePendiente(f, t) != DateTime.MinValue) { UltimoMotivo = MotivoSinLibro(t); return null; }   // 5.0: ex-dividendo
            double rz = f.Razon;
            if (double.IsNaN(rz) || !string.Equals(f.RazonContrato, _op.ContratoDe(t) ?? "", StringComparison.Ordinal)) { UltimoMotivo = MotivoSinLibro(t); return null; }   // C8 + roll
            UltimoMotivo = "";
            double fut = futMnq;
            double S = fut / rz;
            var fl = FilasHoy.Armar(f.Foto, t);
            PerfilLado.CargaPorStrike(f.Foto, t, S, false, out var kCg, out var oiCg, out var oiCgC, out var oiCgP, out var gxCg);   // 4.1.7: TODOS los vencimientos
            var r = PerfilLado.Armar(fl, S, false);
            if (r == null) { UltimoMotivo = "SPY: la cadena vigente no tiene filas del vencimiento de hoy"; return null; }

            // C5 con la cache de la vista previa (tanda de 240 minutos desde el inicio de la sesion)
            long claveIni = TiempoFam.Clave(TiempoFam.IniSesion(TiempoFam.Sesion(t)));
            _cache.Tanda(CacheZero.TandaDe(clave, claveIni));
            long envB = (long)Math.Floor((t - f.GeneradoUtc).TotalSeconds / 900.0);
            long bS = (long)Math.Round(S / ZeroEstandarFam.SPY.CentroPaso, MidpointRounding.ToEven);   // 5.0: el centro de SPY (1 USD), ya no a mano
            var (zv, zo) = _op.ZeroCadaMinuto
                ? ZeroEstandarFam.Calcular(fl, S, false, ZeroEstandarFam.SPY)
                : _cache.Obtener(f, envB, bS, () => ZeroEstandarFam.Calcular(fl, S, false, ZeroEstandarFam.SPY));

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
                ConvTexto = TextoConversion(f, t),
            };
            int j = 0;
            for (int i = 0; i < r.N; i++)
            {
                if (!(Math.Abs(fu[i] - fut) <= fut * FILTRO)) continue;
                b.Fut[j] = fu[i]; b.K[j] = r.K[i]; b.GvC[j] = r.GvC[i]; b.GvP[j] = r.GvP[i]; b.GoC[j] = r.GoC[i]; b.GoP[j] = r.GoP[i]; b.Gv[j] = r.Gv[i]; b.Go[j] = r.Go[i];
                j++;
            }
            // 4.1.8: la carga (OI de TODOS los vencimientos) convertida al eje MES. Este minutero arma el LibroMinuto a
            // mano y NO pasa por ArmadoLibroMinuto.Armar, asi que la carga se engancha aca o no llega nunca a pantalla:
            // hasta la 4.1.8 CargaPorStrike se calculaba y se tiraba, y CARGA_SPY no dibujaba una sola raya.
            if (kCg != null && oiCg != null)
            {
                var fcQ = new List<double>(kCg.Length); var cgQ = new List<double>(kCg.Length);
                var ccQ = new List<double>(kCg.Length); var cpQ = new List<double>(kCg.Length);
                var kkQ = new List<double>(kCg.Length); var gxQ = new List<double>(kCg.Length);
                for (int i = 0; i < kCg.Length; i++)
                {
                    double F = kCg[i] * rz;
                    if (Math.Abs(F - fut) > fut * FILTRO) continue;
                    fcQ.Add(F); cgQ.Add(oiCg[i]); kkQ.Add(kCg[i]);
                    gxQ.Add(gxCg != null && i < gxCg.Length ? gxCg[i] : double.NaN);
                    ccQ.Add(oiCgC != null && i < oiCgC.Length ? oiCgC[i] : double.NaN);
                    cpQ.Add(oiCgP != null && i < oiCgP.Length ? oiCgP[i] : double.NaN);
                }
                if (fcQ.Count > 0) { b.FutCarga = fcQ.ToArray(); b.Carga = cgQ.ToArray(); b.CargaC = ccQ.ToArray(); b.CargaP = cpQ.ToArray(); b.CargaK = kkQ.ToArray(); b.CargaGex = gxQ.ToArray(); }
            }
            return b;
        }

        /// <summary>5.0: el texto de la conversion para la pestaña: "razon X (mediana de N muestras sincronizadas MES/SPY de 04:00-20:00 NY[; ex-dividendo ...])".
        /// Empieza con "razon " como en la 4.1 (lo que lee la pantalla).</summary>
        private string TextoConversion(FotoSpy f, DateTime t)
        {
            var inv = CultureInfo.InvariantCulture;
            string s = "razon " + f.Razon.ToString("0.0000", inv) + " (mediana de " + f.RazonN.ToString(inv) + " muestras sincronizadas MES(ts - 900 s)/spot SPY"
                       + (_op.VentanaHorariaNy ? " de 04:00-20:00 NY" : "");
            if (ExDivReciente(f, t))
                s += "; ex-dividendo de SPY del " + ExDividendoSpy.Dia(f.ExDivUtc) + ": ventana reiniciada a las 04:00 NY, " + f.MuestrasDesdeExDiv.ToString(inv) + " muestras nuevas";
            return s + ")";
        }

        /// <summary>Estado de la fuente para la pestaña (FuenteEstado del contrato) a partir del libro del minuto y su foto. 5.0: sin libro, el motivo.</summary>
        public FuenteEstado Fuente(LibroMinuto b, DateTime tUtc)
        {
            var f = FotoEn(tUtc);
            if (b == null || f == null)
            {
                string m = MotivoSinLibro(tUtc);
                return new FuenteEstado { Libro = LIBRO, Texto = string.IsNullOrEmpty(m) ? "SPY: sin cadena vigente" : m, DatoUtc = f?.DatoUtc ?? DateTime.MinValue };
            }
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
