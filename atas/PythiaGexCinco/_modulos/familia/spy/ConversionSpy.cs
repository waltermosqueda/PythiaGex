// ConversionSpy.cs — PythiaGex 4.1, modulo Familia SPY (B3c). 08-10-2026.
// Port EXACTO de laboratorio/tres/auditoria_0810/backtest_familia.py:
//   * conversion(), rama SPY: C8 razon SINCRONIZADA (muestra = precio del MES en cadena.ts - 900 s / spot, solo con el spot VIVO =
//     distinto del de alguna foto de los 600 s previos por 'generado'; cualquier hora; ultimas 24 validas; mediana robusta 3 MAD con piso
//     0,0002 x mediana; >= 5 o NaN; se reinicia si cambia el contrato; una foto sin muestra hereda la razon de las ultimas 24, sin tope de tiempo);
//   * conversion(), bloque oi_fresco ("OI de 2 sesiones": solo MARCA, no apaga nada);
//   * conversion(), 'congelada' (ultimo_trade NY >= 15:59);
//   * vigencia_cboe() = C9 (25 min desde 'generado'; congelada: hasta las 13:30 UTC siguientes).
// OJO: NO es la regla de atas/_propuesta_3_7/.../Conversion37.cs 3.7.1 (A1: solo muestras EN HORA + mediana ACUMULADA de la rueda): esa
// cambia los numeros y la referencia de paridad (preview_niveles -> backtest_familia) usa la C8 original. Se porto la C8 original.
// Modo Corregida (default del indicador): las horas fijas en UTC del Python (13:30, 22:00, 03:00) pasan a hora de Nueva York (09:30, 18:00,
// 23:00), para que no se corran el 01-11. En horario de verano da IDENTICO. ParidadPython = las horas en UTC como el Python.
// Sin ATAS, sin E/S: lo compila el arnes atas/_test_cuatro_paridad/spy.
//
// 5.0 (27-10-2026, P3): razon ES/SPY (MES / SPY) en lugar de MNQ/QQQ. La C8 es la MISMA regla, con tres agregados de ES, todos medidos
// (_diseno/MEDICIONES_ES.md §3.7, 28 dias de Z6, 15-09..26-10; razon 10,0806-10,1269, mediana 10,1035):
//   * HORAS: solo entran muestras con t_spot (ts - 900 s) en [04:00, 20:00) hora de NY (VentanaHorariaNy). A las 20:00 NY el spot de SPY
//     vuelve al cierre oficial: las 23 muestras de 20:00-20:06 NY erraban hasta 36 pts de ES (|p95| 35,4) y el spot "cambia" (pasa la prueba
//     de vivo). De 04:00 a 19:59 NY el |p95| contra la razon de la rueda del mismo dia es 1,4-2,4 pts. De 20:00 a 04:00 no hay muestras: la
//     razon es la de las ultimas 24 (la de la tarde; medido: sostenida hasta las 10:30 NY erra mediana -1,0 pt de ES, |max| 4,2).
//   * COTA: una muestra fuera de [9,95, 10,25] se descarta, y la razon tambien (CotaRazon). Medido 10,081-10,127 (SPX/SPY 10,015-10,040);
//     ES/SPY = (SPX/SPY) x (1 + base/SPX) va de ~10,015 (vencimiento) a ~10,14 (recien rolado): margen de 0,6 % abajo y 1 % arriba.
//     La de QQQ (en las capas de la 3.0) era 30-60.
//   * EX-DIVIDENDO DE SPY (ExDividendoSpy, tabla EDITABLE; el proximo es el 18-12-2026, el mismo dia que vence ESZ6): a las 04:00 NY de ese
//     dia (abre el pre-mercado: desde ahi el spot de SPY ya viene sin el dividendo) la ventana se REINICIA (ReinicioExDividendo). MEDIDO el
//     18-09: SPX/SPY 10,0155 -> 10,0401 (+0,024, un dividendo de ~1,85 USD) y ES/SPY 10,1065 -> 10,1269 (+0,0204 = +15,6 pts de ES); sin
//     reinicio la mediana trato como atipicas las muestras nuevas (aparecieron desde las 05:47 NY) hasta las 11:58-12:50 NY y los niveles de
//     SPY quedaron ~16 pts corridos toda esa mañana. Hasta juntar 5 muestras nuevas NO hay razon: MinuteroSpy no arma el libro SPY y dice por
//     que (MotivoSinLibro). Es lo mas seguro: NO se usa "la base de SPX x 10,032", porque SPX/SPY es justo lo que salta el dia del
//     ex-dividendo (10,032 es el valor del 22-10, mitad del ciclo; el 18-12 SPX/SPY andaria por 10,040-10,045 y esa conversion dejaria los
//     niveles ~6-10 pts corridos, sin medir) y ademas necesitaria la base C7 del modulo SPX (otro paquete). El hueco esperado: de las 04:00 NY
//     hasta la 5a muestra nueva (pre-mercado: una cadena cada 300 s con el spot vivo en el 25-75 % de las fotos; el 18-09, desde las 05:47).
//   Ruido medido de esta C8 en ES: MAD intradia 0,00059 de razon = 0,45 pts de ES (p95 0,70); after-hours - rueda +0,24 pts (p05 -0,58,
//   p95 +0,86); error contra la muestra siguiente: rueda p05/p95 -1,42/+1,29, after -0,90/+1,32, pre-mercado -3,12/+1,15 pts.
//   El piso 0,0002 x mediana (~0,0020 = ~1,5 pts de ES) queda casi igual a 3 MAD medidos (0,0018): no se cambia.
//   SIN VALIDAR en ES como nivel operable: esto solo fija la conversion.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PythiaGexCinco.Familia.Spy
{
    /// <summary>Atajos de hora del modulo SPY sobre TiempoFam (comun de B3a).</summary>
    internal static class HoraSpy
    {
        public static DateTime ANy(DateTime utc) => TiempoFam.ANy(utc);
        public static DateTime AUtc(DateTime ny) => TiempoFam.NyAUtc(ny);
        /// <summary>Minuto del dia "HH:MM" como entero (el Python compara textos "HH:MM": se truncan los segundos).</summary>
        public static int Hm(DateTime d) => d.Hour * 60 + d.Minute;
        public static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);
        /// <summary>UTC truncado al segundo (los archivos de CBOE y la referencia guardan 'generado' en segundos; B2 puede traer UtcNow con fraccion:
        /// sin truncar, la vigencia de 1500 s y el tramo de 900 s de la cache del zero podian caer distinto en vivo que al reiniciar desde el archivo).</summary>
        public static DateTime Seg(DateTime d) => new DateTime(d.Ticks - d.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }

    /// <summary>5.0 (27-10-2026): los ex-dividendos de SPY y el corte de las 04:00 NY de cada uno (el reinicio de la C8).
    /// TABLA EDITABLE: si SPY anuncia otra fecha (o un feriado la corre), se cambia aca (o se asigna otro arreglo a Fechas en vivo: la
    /// asignacion es atomica). Despues de la ultima fecha de la tabla rige la regla (tercer viernes de mar/jun/sep/dic; si la bolsa esta
    /// cerrada ese dia, el dia habil anterior).</summary>
    public static class ExDividendoSpy
    {
        /// <summary>Hora de NY del reinicio: 04:00, cuando abre el pre-mercado de SPY (desde ahi su spot ya viene sin el dividendo).</summary>
        public static readonly TimeSpan HORA_NY = new TimeSpan(4, 0, 0);

        /// <summary>Fechas (dia de NY) del ex-dividendo de SPY. SPY lo fija el tercer viernes de marzo, junio, septiembre y diciembre.</summary>
        public static DateTime[] Fechas =
        {
            new DateTime(2026, 9, 18),    // MEDIDO: SPX/SPY 10,0155 (17-09) -> 10,0401 (18-09); ES/SPY +0,0204 = +15,6 pts de ES
            new DateTime(2026, 12, 18),   // regla; el MISMO dia vence ESZ6 (09:30 NY) y esa semana se rola a H7 (la C8 tambien se reinicia en el roll)
            new DateTime(2027, 3, 19),    // regla
            new DateTime(2027, 6, 17),    // regla: el viernes 18-06-2027 es feriado (Juneteenth, sabado 19, se corre al viernes) -> jueves 17. SIN VERIFICAR la fecha de SPY
            new DateTime(2027, 9, 17),    // regla
            new DateTime(2027, 12, 17),   // regla
            new DateTime(2028, 3, 17),    // regla
            new DateTime(2028, 6, 16),    // regla
            new DateTime(2028, 9, 15),    // regla
            new DateTime(2028, 12, 15),   // regla
        };

        /// <summary>El tercer viernes de un mes trimestral; si la bolsa de EE. UU. esta cerrada ese dia (TiempoFam.EsFeriadoBolsa), el dia habil anterior.</summary>
        public static DateTime PorRegla(int anio, int mes)
        {
            var d1 = new DateTime(anio, mes, 1);
            var d = d1.AddDays(((int)DayOfWeek.Friday - (int)d1.DayOfWeek + 7) % 7 + 14);
            while (TiempoFam.EsFeriadoBolsa(d) || d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(-1);
            return d;
        }

        /// <summary>04:00 NY de esa fecha, en UTC.</summary>
        public static DateTime CorteUtc(DateTime fechaNy) => TiempoFam.NyAUtc(fechaNy.Date + HORA_NY);

        /// <summary>El corte (04:00 NY de un ex-dividendo, en UTC) mas reciente que sea &lt;= tUtc; DateTime.MinValue si no hay ninguno.</summary>
        public static DateTime UltimoCorte(DateTime tUtc)
        {
            var tabla = Fechas ?? Array.Empty<DateTime>();
            DateTime mejor = DateTime.MinValue, ultima = DateTime.MinValue;
            foreach (var f in tabla)
            {
                if (f > ultima) ultima = f;
                var c = CorteUtc(f);
                if (c <= tUtc && c > mejor) mejor = c;
            }
            // despues de la tabla: la regla
            int anioT = TiempoFam.ANy(tUtc).Year;
            for (int y = Math.Max(ultima == DateTime.MinValue ? anioT - 1 : ultima.Year, anioT - 1); y <= anioT; y++)
                for (int m = 3; m <= 12; m += 3)
                {
                    var d = PorRegla(y, m);
                    if (d <= ultima) continue;
                    var c = CorteUtc(d);
                    if (c <= tUtc && c > mejor) mejor = c;
                }
            return mejor;
        }

        /// <summary>"dd-MM" de la fecha de NY de un corte (para los textos).</summary>
        public static string Dia(DateTime corteUtc) => corteUtc == DateTime.MinValue ? "?" : TiempoFam.ANy(corteUtc).ToString("dd-MM", CultureInfo.InvariantCulture);
    }

    /// <summary>Opciones del modulo SPY. Contrato: de una funcion (ParidadPython = tabla fija de velas.contrato; Corregida = el del grafico).</summary>
    public sealed class OpcionesSpy
    {
        /// <summary>true (default del indicador): horas de C9 / oi_fresco / ventana de sesion en hora de NY. false = como el Python (UTC fijo).</summary>
        public bool Corregida = true;

        /// <summary>4.1.8 (12-10-2026): calcular el zero FRESCO cada minuto en vez de reusar el de la cache.
        /// La cache memoriza el resultado del PRIMER minuto que cae en cada clave (ZeroEstandar.cs:103) y el valor
        /// NO es funcion de la clave: dentro de una misma clave el envejecimiento mueve T minuto a minuto y S puede
        /// enganchar otro cruce. Medido sobre la sesion 2026-10-09 (1.378 minutos, codigo real compilado):
        /// ZEST_SPY_vol p50 0,014 / p95 0,137 / MAX 115,6 pts en el eje MES, y el valor dibujado de SPY sale de la
        /// cache en el 99,8 % de los minutos. Costo de no cachear: ~5,0 ms por minuto los tres libros (~7 s por
        /// sesion). Sin esto el pasado no es reproducible y el operador leeria un zero que no rigio.</summary>
        public bool ZeroCadaMinuto = true;
        /// <summary>Contrato del grafico vigente en un instante UTC. Default = tabla del Python (U6 hasta la sesion del 14-09, Z6 hasta la del 17-12, H7).</summary>
        public Func<DateTime, string> ContratoDe = ContratoTablaPython;

        /// <summary>5.0 (27-10-2026): solo muestras con t_spot NY en [04:00, 20:00) (a las 20:00 NY el spot de SPY vuelve al cierre oficial:
        /// muestras de hasta 36 pts de error, medido). false = a cualquier hora, como la C8 de la 4.1 (QQQ).</summary>
        public bool VentanaHorariaNy = true;
        /// <summary>5.0: descartar muestras (y la razon) fuera de [RAZON_MIN, RAZON_MAX] = [9,95, 10,25]. false = sin cota (4.1).</summary>
        public bool CotaRazon = true;
        /// <summary>5.0: reiniciar la ventana a las 04:00 NY de cada ex-dividendo de SPY (ExDividendoSpy). false = como la 4.1 (QQQ no lo necesitaba).</summary>
        public bool ReinicioExDividendo = true;

        /// <summary>velas.contrato(sesion_de(t)) del laboratorio: tabla fija (paridad). En el indicador se reemplaza por el del grafico.</summary>
        public static string ContratoTablaPython(DateTime tUtc)
        {
            var ses = tUtc.AddHours(2).Date;
            if (ses <= new DateTime(2026, 9, 14)) return "U6";
            if (ses <= new DateTime(2026, 12, 17)) return "Z6";
            return "H7";
        }
        /// <summary>Contrato fijo (el del codigo del grafico: MESZ6 -> "Z6").</summary>
        public static Func<DateTime, string> ContratoFijo(string k) { var c = k ?? ""; return _ => c; }
        /// <summary>"MESZ6" / "ESZ6" / "MES Z6" -> "Z6" ("" si no se reconoce).</summary>
        public static string ContratoDeCodigo(string codigo)
        {
            if (string.IsNullOrEmpty(codigo)) return "";
            var s = codigo.Trim().ToUpperInvariant();
            int i = s.Length - 1;
            while (i >= 0 && char.IsDigit(s[i])) i--;
            if (i < 0 || i == s.Length - 1) return "";
            char mes = s[i];
            if ("FGHJKMNQUVXZ".IndexOf(mes) < 0) return "";
            return s.Substring(i);
        }
    }

    /// <summary>Lo que la conversion sabe de UNA foto de SPY a su 'generado' (inmutable despues de Observar).</summary>
    public sealed class FotoSpy
    {
        public FotoCadena Foto;               // se libera (null) al podar las viejas
        public int Indice;                    // orden de llegada (el 'k' del Python: clave de la cache del zero)
        public DateTime GeneradoUtc, TsUtc, DatoUtc;
        public double Spot = double.NaN;
        public DateTime TSpotUtc;             // ts - 900 s
        public string ContratoMuestra = "";   // contrato_de(t_spot)
        public bool Vivo;
        public double Precio = double.NaN;    // MES en ts - 900 s (cinta, o sembrado del archivo de muestras)
        public double Muestra = double.NaN;   // precio / spot (solo vivo, en hora, en la cota y despues del ultimo ex-dividendo)
        public double Razon = double.NaN;     // C8 vigente a esta foto (NaN si < 5 muestras)
        public string RazonContrato = "";
        public int RazonN;
        public bool OiViejo;                  // "OI de 2 sesiones" (solo marca)
        public double OiTotal;
        public bool Congelada;                // ultimo_trade NY >= 15:59
        public DateTime VigenteHastaUtc;      // C9
        // ---- 5.0 (27-10-2026)
        /// <summary>Por que esta foto NO dio muestra ("" = dio): spot quieto, sin precio del MES, fuera de hora, fuera de cota, anterior al ex-dividendo.</summary>
        public string Descarte = "";
        /// <summary>El corte de ex-dividendo vigente cuando se calculo la razon (04:00 NY del ultimo ex-dividendo &lt;= generado, UTC; MinValue = ninguno).</summary>
        public DateTime ExDivUtc = DateTime.MinValue;
        /// <summary>Muestras aceptadas desde ese corte (para "N de 5 muestras nuevas").</summary>
        public int MuestrasDesdeExDiv;
        /// <summary>Esta foto reinicio la ventana por el ex-dividendo (cruzo el corte con la serie andando).</summary>
        public bool ReinicioExDiv;
    }

    /// <summary>C8 + oi_fresco + congelada + C9 de SPY, foto por foto EN ORDEN de 'generado' (backtest_familia.conversion es causal: el estado de
    /// la foto i solo depende de las fotos 0..i, asi que procesarlas de a una apenas llegan da lo mismo que recalcular todo).</summary>
    public sealed class ConversionSpy
    {
        public const double RETRASO_S = 900, VIVO_S = 600, VIDA_CBOE_S = 1500, PISO_REL = 0.0002;
        public const int VENTANA = 24, MIN_N = 5;
        /// <summary>5.0: cota de la razon ES/SPY (medida 10,0806-10,1269 en Z6, 15-09..26-10). La de QQQ era 30-60.</summary>
        public const double RAZON_MIN = 9.95, RAZON_MAX = 10.25;
        /// <summary>5.0: t_spot NY en [04:00, 20:00) (minutos del dia).</summary>
        public const int HORA_INI_NY = 4 * 60, HORA_FIN_NY = 20 * 60;

        private readonly OpcionesSpy _op;
        private readonly List<(DateTime Gen, double Spot)> _recientes = new List<(DateTime, double)>();   // fotos de los ultimos 600 s
        private readonly List<double> _roll = new List<double>();
        private string _rollContrato;          // null = "None" del Python (la primera foto siempre "cambia" de contrato)
        private bool _oiViejo;
        private double _oiPrev = double.NaN;   // None
        private DateTime _genPrev = DateTime.MinValue;
        private int _n;
        private readonly Dictionary<DateTime, (double Precio, string Contrato)> _sembrados = new Dictionary<DateTime, (double, string)>();
        private DateTime _corteExDiv = DateTime.MinValue;   // 5.0: corte de ex-dividendo ya aplicado
        private int _nDesdeCorte;                           // 5.0: muestras aceptadas desde ese corte

        public ConversionSpy(OpcionesSpy op) { _op = op ?? new OpcionesSpy(); }
        public int Procesadas => _n;

        /// <summary>5.0: la muestra con t_spot en esta hora entra? (t_spot NY en [04:00, 20:00); siempre si VentanaHorariaNy esta apagada).</summary>
        public bool EnHoraMuestra(DateTime tSpotUtc)
        {
            if (!_op.VentanaHorariaNy) return true;
            int hm = HoraSpy.Hm(HoraSpy.ANy(tSpotUtc));
            return hm >= HORA_INI_NY && hm < HORA_FIN_NY;
        }

        /// <summary>5.0: la razon (o una muestra) esta en la cota? (siempre si CotaRazon esta apagada; NaN nunca).</summary>
        public bool EnCota(double razon) => !double.IsNaN(razon) && (!_op.CotaRazon || (razon >= RAZON_MIN && razon <= RAZON_MAX));

        /// <summary>Precio del MES en ts-900 s guardado en una corrida anterior (archivo de muestras): se usa si la cinta no lo tiene, y solo si
        /// es del MISMO contrato que hoy le toca a esa muestra (despues de un roll, un precio de Z6 no puede entrar en la razon de H7).</summary>
        public void SembrarPrecio(DateTime tsUtc, double precio, string contrato = null)
        {
            if (double.IsNaN(precio)) return;
            _sembrados[HoraSpy.Seg(tsUtc)] = (precio, contrato);
            if (_sembrados.Count > 20000) _sembrados.Clear();          // tope de memoria (6 dias de SPY son ~7000 fotos)
        }

        /// <summary>Procesa la foto siguiente (generado creciente). precioCinta = ICinta.Precio(ts - 900 s) (NaN si no hay).</summary>
        public FotoSpy Observar(FotoCadena f, double precioCinta)
        {
            var inv = CultureInfo.InvariantCulture;
            var gen = HoraSpy.Seg(f.GeneradoUtc);
            var ts = HoraSpy.Seg(f.TsUtc);
            var r = new FotoSpy { Foto = f, Indice = _n, GeneradoUtc = gen, TsUtc = ts, DatoUtc = HoraSpy.Utc(f.DatoUtc), Spot = f.Spot, OiTotal = f.OiTotal };
            r.TSpotUtc = ts.AddSeconds(-RETRASO_S);
            r.ContratoMuestra = _op.ContratoDe(r.TSpotUtc) ?? "";
            double precio = precioCinta;
            if (double.IsNaN(precio) && _sembrados.TryGetValue(ts, out var ps) && (string.IsNullOrEmpty(ps.Contrato) || ps.Contrato == r.ContratoMuestra)) precio = ps.Precio;
            r.Precio = precio;

            // 5.0: ex-dividendo de SPY. Desde las 04:00 NY de ese dia (por 'generado': la foto rige desde que se bajo) las muestras de antes
            // traen el dividendo adentro: la ventana arranca de cero y la razon queda NaN hasta juntar MIN_N muestras nuevas.
            var corte = _op.ReinicioExDividendo ? ExDividendoSpy.UltimoCorte(gen) : DateTime.MinValue;
            if (corte != _corteExDiv)
            {
                r.ReinicioExDiv = _n > 0 && corte > _corteExDiv;
                _roll.Clear(); _nDesdeCorte = 0; _corteExDiv = corte;
            }

            // vivo: alguna foto de los 600 s previos (por generado) con otro spot
            bool vivo = false;
            for (int j = _recientes.Count - 1; j >= 0; j--)
            {
                if ((gen - _recientes[j].Gen).TotalSeconds > VIVO_S) break;
                if (Math.Abs(_recientes[j].Spot - r.Spot) > 1e-9) { vivo = true; break; }
            }
            r.Vivo = vivo;
            _recientes.Add((gen, r.Spot));
            int corteRec = 0;
            while (corteRec < _recientes.Count - 1 && (gen - _recientes[corteRec].Gen).TotalSeconds > VIVO_S) corteRec++;
            if (corteRec > 0) _recientes.RemoveRange(0, corteRec);

            // la muestra (5.0: con hora, cota y ex-dividendo)
            double val = double.NaN;
            if (!vivo) r.Descarte = "spot quieto";
            else if (double.IsNaN(precio)) r.Descarte = "sin precio del MES en ts - 900 s";
            else if (!(r.Spot > 0)) r.Descarte = "sin spot";
            else if (!EnHoraMuestra(r.TSpotUtc)) r.Descarte = "fuera de hora (spot de las " + HoraSpy.ANy(r.TSpotUtc).ToString("HH:mm", inv) + " NY; solo 04:00-20:00)";
            else if (r.TSpotUtc < _corteExDiv) r.Descarte = "spot anterior al ex-dividendo del " + ExDividendoSpy.Dia(_corteExDiv);
            else
            {
                double q = precio / r.Spot;
                if (!EnCota(q)) r.Descarte = "fuera de cota (" + q.ToString("0.0000", inv) + "; vale " + RAZON_MIN.ToString("0.00", inv) + "-" + RAZON_MAX.ToString("0.00", inv) + ")";
                else val = q;
            }
            r.Muestra = val;

            // C8
            if (_rollContrato == null || _rollContrato != r.ContratoMuestra) { _roll.Clear(); _rollContrato = r.ContratoMuestra; }
            if (!double.IsNaN(val)) { _roll.Add(val); if (_roll.Count > VENTANA) _roll.RemoveRange(0, _roll.Count - VENTANA); _nDesdeCorte++; }
            var (v, n) = Robusta(_roll, PISO_REL * (_roll.Count > 0 ? Mediana(_roll) : 0.0));
            r.Razon = n >= MIN_N && EnCota(v) ? v : double.NaN;
            r.RazonContrato = _rollContrato;
            r.RazonN = n;
            r.ExDivUtc = _corteExDiv;
            r.MuestrasDesdeExDiv = _nDesdeCorte;

            // oi_fresco (solo marca)
            int hmu = Hm(gen);
            int hprev = _n > 0 ? Hm(_genPrev) : -1;   // Python: "12:00" UTC para la primera = fuera de la franja de dia (en NY 12:00 seria dia: -1 lo evita)
            if (EnDia(hmu)) _oiViejo = false;
            else if (EnNoche(hmu) && !_oiViejo && (EnDia(hprev) || (_n > 0 && (gen - _genPrev).TotalSeconds > 6 * 3600))) _oiViejo = true;
            else if (EnMadrugada(hmu) && _oiViejo && !double.IsNaN(_oiPrev) && _oiPrev != 0 && Math.Abs(r.OiTotal - _oiPrev) / _oiPrev > 0.05) _oiViejo = false;
            r.OiViejo = _oiViejo;
            if (r.OiTotal > 0) _oiPrev = r.OiTotal;

            // congelada + C9
            r.Congelada = r.DatoUtc.Year >= 2000 && HoraSpy.Hm(HoraSpy.ANy(r.DatoUtc)) >= 15 * 60 + 59;   // sin hora del dato: no congelada
            r.VigenteHastaUtc = Vigencia(gen, r.Congelada);

            _genPrev = gen;
            _n++;
            return r;
        }

        /// <summary>C9: 25 min desde generado; congelada: hasta la proxima apertura (13:30 UTC del Python | 09:30 NY corregida) si es mas tarde.</summary>
        public DateTime Vigencia(DateTime genUtc, bool congelada)
        {
            var h1 = genUtc.AddSeconds(VIDA_CBOE_S);
            if (!congelada) return h1;
            DateTime prox;
            if (_op.Corregida)
            {
                var ny = HoraSpy.ANy(genUtc);
                prox = HoraSpy.AUtc(ny.Date.AddHours(9).AddMinutes(30));
                if (prox <= genUtc) prox = HoraSpy.AUtc(ny.Date.AddDays(1).AddHours(9).AddMinutes(30));
            }
            else
            {
                prox = HoraSpy.Utc(genUtc.Date.AddHours(13).AddMinutes(30));
                if (prox <= genUtc) prox = prox.AddDays(1);
            }
            return prox > h1 ? prox : h1;
        }

        // franjas de oi_fresco: dia [13:30, 22:00) | noche [22:00, 03:00) | madrugada [03:00, 13:30) en UTC (Python);
        // corregida: dia [09:30, 18:00) | noche [18:00, 23:00) | madrugada [23:00, 09:30) en NY.
        private int Hm(DateTime genUtc) => _op.Corregida ? HoraSpy.Hm(HoraSpy.ANy(genUtc)) : HoraSpy.Hm(genUtc);
        private bool EnDia(int hm) => _op.Corregida ? (hm >= 9 * 60 + 30 && hm < 18 * 60) : (hm >= 13 * 60 + 30 && hm < 22 * 60);
        private bool EnNoche(int hm) => _op.Corregida ? (hm >= 18 * 60 && hm < 23 * 60) : (hm >= 22 * 60 || hm < 3 * 60);
        private bool EnMadrugada(int hm) => _op.Corregida ? (hm >= 23 * 60 || hm < 9 * 60 + 30) : (hm >= 3 * 60 && hm < 13 * 60 + 30);

        /// <summary>Ventana de la sesion de CBOE (libros.fotos_cboe: generado en [dia-2h, dia+21h], o sea se descartan las de (21:00, 22:00) UTC
        /// exclusivo; corregida: (17:00, 18:00) NY).</summary>
        public bool FueraDeSesion(DateTime genUtc)
        {
            var d = _op.Corregida ? HoraSpy.ANy(genUtc) : genUtc;
            var tod = d.TimeOfDay;
            var a = _op.Corregida ? new TimeSpan(17, 0, 0) : new TimeSpan(21, 0, 0);
            return tod > a && tod < a.Add(TimeSpan.FromHours(1));
        }

        public static double Mediana(IReadOnlyList<double> x) => NumFam.Mediana(x);
        public static (double V, int N) Robusta(IReadOnlyList<double> x, double piso) => NumFam.Robusta(x, piso);

        // ------------------------------------------------------------------ archivo de muestras (lo escribe el motor en PythiaGex5/familia)
        /// <summary>Una linea de muestras-SPY-&lt;sesion&gt;.jsonl: {ts, generado, spot, precio, muestra, vivo, contrato, razon, n} y, desde la 5.0,
        /// "descarte" (por que no dio muestra; "" si dio) y "exdiv" (fecha NY del ultimo ex-dividendo aplicado, "" si ninguno). Los lectores
        /// viejos (LeerLineaMuestra, la sembradora) ignoran los campos nuevos.</summary>
        public static string LineaMuestra(FotoSpy r)
        {
            var inv = CultureInfo.InvariantCulture;
            string N(double d) => double.IsNaN(d) ? "null" : d.ToString("R", inv);
            var sb = new StringBuilder(260);
            sb.Append("{\"ts\":\"").Append(r.TsUtc.ToString("yyyy-MM-dd HH:mm:ss", inv)).Append("\",\"generado\":\"").Append(r.GeneradoUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", inv))
              .Append("\",\"spot\":").Append(N(r.Spot)).Append(",\"precio\":").Append(N(r.Precio)).Append(",\"muestra\":").Append(N(r.Muestra))
              .Append(",\"vivo\":").Append(r.Vivo ? "true" : "false").Append(",\"contrato\":\"").Append(r.ContratoMuestra).Append("\",\"razon\":").Append(N(r.Razon))
              .Append(",\"n\":").Append(r.RazonN.ToString(inv))
              .Append(",\"descarte\":\"").Append((r.Descarte ?? "").Replace("\\", "\\\\").Replace("\"", "'")).Append('"')
              .Append(",\"exdiv\":\"").Append(r.ExDivUtc == DateTime.MinValue ? "" : TiempoFam.ANy(r.ExDivUtc).ToString("yyyy-MM-dd", inv)).Append("\"}");
            return sb.ToString();
        }

        /// <summary>Lee (ts, precio, contrato) de una linea de muestras (para SembrarPrecio al arrancar sin la cinta vieja).</summary>
        public static bool LeerLineaMuestra(string linea, out DateTime tsUtc, out double precio, out string contrato)
        {
            tsUtc = DateTime.MinValue; precio = double.NaN; contrato = null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(linea);
                var r = doc.RootElement;
                if (!DateTime.TryParseExact(r.GetProperty("ts").GetString(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return false;
                tsUtc = HoraSpy.Utc(t);
                if (r.TryGetProperty("contrato", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.String) contrato = c.GetString();
                var p = r.GetProperty("precio");
                if (p.ValueKind != System.Text.Json.JsonValueKind.Number) return false;
                precio = p.GetDouble();
                return true;
            }
            catch { return false; }
        }
    }
}
