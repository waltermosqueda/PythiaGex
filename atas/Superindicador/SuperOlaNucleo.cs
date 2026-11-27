using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PythiaSuper
{
    // =========================================================================================================================
    // 2.0a (27-11-2026) LA OLA CONECTADA A LA CINTA DE ATAS, LA PARTE SIN NADA DE ATAS. El motor (Ola, OlaFotos, EscalaOla, RangosRth
    // en SuperCalculos.cs) no se toca: esto es lo que lo alimenta. Una CARGA por jornada de CME (18:00 NY de la vispera a 17:00 NY):
    // su X, sus cortes (uno por vela) y dos OlaFotos, la ola (X) y la marea (4X). La de la ultima vela es la VIVA: junta en un bufer las
    // ordenes en vivo desde que arranca, pide el historial de la jornada a ATAS, lo pasa por motores LOCALES (en un Task, sin llave) y lo
    // cose con el bufer en la COSTURA; desde ahi sigue orden por orden. Las pasadas se piden enteras cuando la vela del momento cae en
    // ellas, de a una. Vive aparte de SuperOla.cs (la parte de ATAS) para que el arnes atas/_test_super corra EXACTAMENTE este codigo
    // sobre cintas reales y herramientas/auditar_ola.py lo compare contra la referencia en Python (test_ola.py).
    // Nada de esto es seguro entre hilos: el indicador lo usa adentro de su llave (_llaveOla); el Task del historial solo toca su
    // TrabajoOla, que no comparte nada con la carga hasta que se integra bajo la llave.
    // =========================================================================================================================

    /// <summary>Una orden agresora como la entrega ATAS (CumulativeTrade): hora UTC de su primera operacion, primer y ultimo precio (en
    /// una barrida, donde arranco y donde termino), volumen y lado (+1 compra a mercado, -1 venta a mercado, 0 sin agresor).</summary>
    public readonly struct OrdenOla
    {
        public readonly DateTime T;
        public readonly decimal Primero, Ultimo, Vol;
        public readonly int Lado;

        public OrdenOla(DateTime t, decimal primero, decimal ultimo, decimal vol, int lado)
        {
            T = t; Primero = primero; Ultimo = ultimo; Vol = vol; Lado = lado;
        }

        /// <summary>La misma orden aunque haya crecido: una orden acumulada que crece conserva su hora, su primer precio y su lado (lo que
        /// cambia es el ultimo precio y el volumen).</summary>
        public bool MismaClave(in OrdenOla o) => T == o.T && Primero == o.Primero && Lado == o.Lado;
    }

    /// <summary>
    /// EL PERIODO DE LAS VELAS, para saber cuando cierra cada una. Regla del pedido: la diferencia minima positiva entre aperturas
    /// consecutivas, si al menos el 90 % de las diferencias positivas son multiplos de ella; si no, el grafico no se toma como de tiempo y
    /// el corte de una vela es la apertura de la siguiente. Dos resguardos: el periodo tiene que ser de segundos enteros (en un grafico de
    /// ticks la diferencia minima puede ser 1 ms y todo seria "multiplo") y, si el marco que declara ATAS ("M1" = 60 s, "Seconds" + "45" =
    /// 45 s) es multiplo de esa minima y tambien pasa el 90 %, manda el declarado: una vela suelta mas corta (una sesion que reabre fuera de
    /// la grilla) no achica el periodo de todas. Un ChartType que no es de tiempo (Tick, Range, Volume, Renko...) no se toma como de tiempo.
    /// </summary>
    public static class PeriodoVelas
    {
        public const double Minimo = 0.90;

        /// <summary>El marco que declara ATAS: ChartType "TimeFrame" con TimeFrame "M1"/"M5"/"H1"/"D1"/"W1", o ChartType "Seconds" con
        /// TimeFrame "45". Devuelve TimeSpan.Zero si no se puede leer; noEsTiempo = el ChartType dice que no es de tiempo.</summary>
        public static TimeSpan Declarado(string tipo, string marco, out bool noEsTiempo)
        {
            tipo = (tipo ?? "").Trim(); marco = (marco ?? "").Trim();
            string t = tipo.ToLowerInvariant();
            bool esTiempo = t == "timeframe" || t == "seconds" || t == "minutes" || t == "daily" || t == "weekly";
            noEsTiempo = t.Length > 0 && !esTiempo;
            if (!esTiempo || marco.Length == 0) return TimeSpan.Zero;
            char u = char.ToUpperInvariant(marco[0]);
            string dig = char.IsLetter(marco[0]) ? marco.Substring(1) : marco;
            if (!int.TryParse(dig, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n <= 0) n = dig.Length == 0 ? 1 : 0;
            if (n <= 0) return TimeSpan.Zero;
            if (t == "seconds") return TimeSpan.FromSeconds(n);
            if (t == "daily") return TimeSpan.FromDays(n);
            if (t == "weekly") return TimeSpan.FromDays(7 * n);
            switch (u)
            {
                case 'S': return TimeSpan.FromSeconds(n);
                case 'M': return TimeSpan.FromMinutes(n);
                case 'H': return TimeSpan.FromHours(n);
                case 'D': return TimeSpan.FromDays(n);
                case 'W': return TimeSpan.FromDays(7 * n);
            }
            return char.IsDigit(marco[0]) ? TimeSpan.FromMinutes(n) : TimeSpan.Zero;
        }

        /// <summary>El periodo con la regla del pedido (ver la clase). TimeSpan.Zero: el corte de cada vela es la apertura de la siguiente.</summary>
        public static TimeSpan Detectar(IList<DateTime> aperturas, TimeSpan declarado, bool noEsTiempo, out double pctMultiplos, out int diferencias, out string fuente)
        {
            pctMultiplos = 0; diferencias = 0; fuente = "";
            if (noEsTiempo) { fuente = "no es de tiempo (tipo de grafico)"; return TimeSpan.Zero; }
            int n = aperturas?.Count ?? 0;
            long min = long.MaxValue;
            for (int i = 1; i < n; i++)
            {
                long d = aperturas[i].Ticks - aperturas[i - 1].Ticks;
                if (d <= 0) continue;
                diferencias++;
                if (d < min) min = d;
            }
            if (diferencias < 3)
            {
                if (declarado > TimeSpan.Zero) { fuente = "declarado (pocas velas)"; pctMultiplos = 100; return declarado; }
                fuente = "sin velas suficientes"; return TimeSpan.Zero;
            }
            double pct = Pct(aperturas, min);
            pctMultiplos = pct * 100;
            if (pct < Minimo) { fuente = "las aperturas no son multiplos de " + Seg(min) + " (" + (pct * 100).ToString("0.0", CultureInfo.InvariantCulture) + " %)"; return TimeSpan.Zero; }
            if (min % TimeSpan.TicksPerSecond != 0) { fuente = "diferencia minima de " + Seg(min) + ": no es de segundos enteros"; return TimeSpan.Zero; }
            long dec = declarado.Ticks;
            if (dec > min && dec % min == 0)
            {
                double pd = Pct(aperturas, dec);
                if (pd >= Minimo) { pctMultiplos = pd * 100; fuente = "declarado (multiplo de la minima " + Seg(min) + ")"; return declarado; }
            }
            fuente = "aperturas";
            return new TimeSpan(min);
        }

        private static double Pct(IList<DateTime> a, long p)
        {
            int tot = 0, mul = 0;
            for (int i = 1; i < a.Count; i++)
            {
                long d = a[i].Ticks - a[i - 1].Ticks;
                if (d <= 0) continue;
                tot++;
                if (d % p == 0) mul++;
            }
            return tot > 0 ? (double)mul / tot : 0;
        }

        private static string Seg(long ticks) => (ticks / (double)TimeSpan.TicksPerSecond).ToString("0.###", CultureInfo.InvariantCulture) + " s";
    }

    /// <summary>
    /// Los rangos RTH de cada rueda (RangosRth, de las velas del grafico) y si la rueda esta COMPLETA: sus velas RTH cubren al menos 6 h
    /// (de la primera apertura RTH al cierre de la ultima vela RTH: su apertura + el periodo, o su ultima operacion si es mas tarde). Asi
    /// quedan afuera las medias ruedas de feriado (26 y 27-11: 3,5 h) y la rueda que el grafico cargo por la mitad.
    /// </summary>
    public sealed class RuedasOla
    {
        public static readonly TimeSpan Minimo = TimeSpan.FromHours(6);

        private sealed class Cobertura { public DateTime Primera = DateTime.MaxValue, UltimaApertura = DateTime.MinValue, UltimaFin = DateTime.MinValue; public int Velas; }

        private readonly RangosRth _rangos = new RangosRth();
        private readonly Dictionary<int, Cobertura> _cob = new Dictionary<int, Cobertura>();
        private Cobertura _ult; private int _ultClave = -1; private DateTime _ultApertura = DateTime.MinValue;

        public IList<RangosRth.Rueda> Lista => _rangos.Lista;

        /// <summary>Una vela (o la vela en curso otra vez, con su alto, bajo y ultima operacion nuevos): horas NY de su apertura y de su ultima
        /// operacion. Una vela va entera a la parte de su apertura, como en Jornadas.</summary>
        public void Agregar(DateTime nyApertura, DateTime nyFin, decimal alto, decimal bajo)
        {
            if (Jornadas.Tipo(nyApertura) != Jornadas.TipoRth) return;
            _rangos.Agregar(nyApertura, alto, bajo);
            int clave = nyApertura.Year * 10000 + nyApertura.Month * 100 + nyApertura.Day;
            var c = _ult;
            if (c == null || _ultClave != clave)
            {
                if (!_cob.TryGetValue(clave, out c)) { c = new Cobertura(); _cob.Add(clave, c); }
                _ult = c; _ultClave = clave; _ultApertura = DateTime.MinValue;
            }
            if (nyApertura < c.Primera) c.Primera = nyApertura;
            if (nyApertura > c.UltimaApertura) c.UltimaApertura = nyApertura;
            if (nyFin > c.UltimaFin) c.UltimaFin = nyFin;
            if (nyApertura != _ultApertura) { c.Velas++; _ultApertura = nyApertura; }
        }

        /// <summary>Lo que cubren las velas RTH de la rueda.</summary>
        public TimeSpan Cubierto(int clave, TimeSpan periodo)
        {
            if (!_cob.TryGetValue(clave, out var c) || c.Primera == DateTime.MaxValue) return TimeSpan.Zero;
            var fin = c.UltimaApertura + periodo;
            if (c.UltimaFin > fin) fin = c.UltimaFin;
            return fin - c.Primera;
        }

        public bool Completa(int clave, TimeSpan periodo) => Cubierto(clave, periodo) >= Minimo;

        /// <summary>X de la jornada 'clave': EscalaOla.X con los rangos RTH de las ultimas 10 ruedas COMPLETAS anteriores (con menos, las que
        /// haya). Sin ninguna: el piso de 4 (sinRuedas = true). excluidas: las ruedas anteriores incompletas (las ultimas 5, para el registro).</summary>
        public decimal X(int clave, decimal tick, TimeSpan periodo, out decimal mediana, out int usadas, out bool sinRuedas, out List<int> excluidas)
        {
            var previos = new List<decimal>();
            excluidas = new List<int>();
            foreach (var r in _rangos.Lista)
            {
                if (r.Clave >= clave) continue;
                if (Completa(r.Clave, periodo)) previos.Add(r.Rango);
                else { excluidas.Add(r.Clave); if (excluidas.Count > 5) excluidas.RemoveAt(0); }
            }
            usadas = Math.Min(EscalaOla.Ruedas, previos.Count);
            decimal x = EscalaOla.X(previos, tick, out mediana);
            sinRuedas = x <= 0m;
            return sinRuedas ? EscalaOla.Piso : x;
        }
    }

    public enum EstadoOla { EnCola, Pedida, Procesando, EnVivo, Completa, SinCinta, SinRespuesta }

    /// <summary>
    /// UNA JORNADA de la ola: X, cortes (uno por vela, desde PrimerBar), los dos motores y la costura con el vivo.
    /// Ciclo: EnCola (creada, esperando turno) -> Pedida (RequestForCumulativeTrades en vuelo) -> Procesando (el historial corre en un Task
    /// con motores locales) -> EnVivo (la viva, orden por orden) o Completa (una pasada). Mientras no se integro el historial, lo que llega
    /// en vivo va al BUFER: las ordenes nuevas en su orden de llegada y, si una orden crece (OnUpdateCumulativeTrade, misma referencia), se
    /// reemplaza adentro del bufer (todavia no entro al motor: queda exacto aunque no sea la ultima). Una actualizacion de una orden que no
    /// esta en el bufer (la que venia creciendo cuando arranco el indicador) se guarda suelta y, al coser, corrige la ultima orden del
    /// historial si es la misma (misma hora, primer precio y lado).
    /// LA COSTURA. Con ordenes nuevas en el bufer, manda el bufer desde la primera (T_costura): del historial entran las de t &lt; T_costura y,
    /// del mismo milisegundo, solo las que el bufer no trae (las primeras: pasaron antes de que arrancara el indicador). Con el bufer vacio,
    /// entra todo el historial hasta la ultima operacion de la ultima vela del grafico (en el replay ATAS puede devolver la rueda entera, con
    /// el "futuro"); desde ahi, lo que llega en vivo con hora anterior a la costura es repetido y se descarta.
    /// EN VIVO: OnCumulativeTrade -> Orden; OnUpdateCumulativeTrade de la ULTIMA orden (misma referencia y clave) -> CorregirUltima; de otra
    /// orden: "vieja" (se cuenta y el indicador la anota; el motor no puede corregir una orden que no es la ultima).
    /// SIN CINTA (el historial volvio vacio, puede pasar en el replay) o SIN RESPUESTA (180 s): la viva sigue solo con lo que llega en vivo
    /// desde el bufer (InicioMotor = la primera orden que entro).
    /// </summary>
    public sealed class CargaOla
    {
        public const int MaxBufer = 600000;   // ~50 MB: 180 s de rueda rapida son menos de 100 mil
        public const int BuscarEnBufer = 64;   // cuantas ordenes hacia atras se busca la que crecio

        public readonly int Clave;
        public readonly DateTime Desde, Hasta;          // UTC
        public readonly decimal X, Mediana;
        public readonly int Usadas;                     // ruedas completas en la mediana
        public readonly string FuenteX;
        public readonly bool XSinRuedas;
        public readonly string Excluidas;               // ruedas anteriores incompletas (las ultimas 5)
        public readonly TimeSpan Periodo;               // TimeSpan.Zero: el corte es la apertura de la vela siguiente
        public int PrimerBar { get; internal set; } = -1;
        public bool RecibeVivo { get; internal set; }   // la viva (la de la ultima vela)
        public EstadoOla Estado { get; internal set; } = EstadoOla.EnCola;
        public string Motivo { get; internal set; } = "";   // por que quedo sin cinta / sin respuesta
        public OlaFotos Ola { get; private set; }
        public OlaFotos Marea { get; private set; }

        private readonly List<DateTime> _cortes = new List<DateTime>();
        public int Cortes => _cortes.Count;
        public DateTime Corte(int k) => _cortes[k];
        public int CortesForzados { get; private set; }   // corte que no era posterior al anterior: se corrio un tick para no perder el indice k
        public int CortesTarde => (Ola?.CortesTarde ?? 0) + (Marea?.CortesTarde ?? 0);

        // la costura
        public DateTime Costura { get; private set; } = DateTime.MinValue;
        public string CosturaTipo { get; private set; } = "";
        public DateTime InicioMotor { get; private set; } = DateTime.MinValue;   // desde donde el motor tiene ordenes
        public long NInicio { get; private set; }                                   // ordenes del motor con t == InicioMotor (para el auditor)
        public bool CosturaDudosa { get; private set; }   // en el milisegundo de la costura, el historial y el bufer no traen las mismas ordenes
        public bool SinSolape { get; private set; }       // el historial termino antes de la primera del bufer: mercado quieto o un hueco
        public DateTime UltimaHist { get; private set; } = DateTime.MinValue;
        public DateTime PrimeraBufer { get; private set; } = DateTime.MinValue;

        // contadores
        public long HistRecibidas, HistNulas, HistFuera, HistUsadas, HistDescartadas, HistEnCostura, HistDesordenadas;
        public long VivoNuevas, VivoDuplicadas, VivoRepetidas, VivoDesordenadas, Correcciones, CorreccionesCostura, Viejas, ViejasBufer, CorreccionesBufer, HuerfanasBufer, Fuera;
        public long ActDuplicadas;   // actualizaciones de ordenes repetidas (el historial ya las traia)
        public int BuferMax;
        public DateTime CreadaEn, PedidaEn, RespondidaEn, IntegradaEn;
        public double MsProceso;
        public OrdenOla UltimaVieja;
        public long Version { get; private set; }        // sube con cada cambio del motor (para no reescribir lo que no cambio)
        public long VersionPiv { get; private set; }     // sube cuando puede haber cambiado algun pivote
        public object Pedido;                            // el pedido de ATAS en vuelo (lo maneja el indicador)
        public long Acceso;                              // para la cache de pasadas (la menos usada se va)
        public long VersionEscrita = -1;                 // la Version que ya se escribio en el csv de la auditoria (lo maneja el indicador)

        // la ultima orden del motor (para corregirla)
        private object _ultRef; private OrdenOla _ult; private bool _hayUlt, _ultDeHistorial;
        // con el bufer vacio: lo que el historial ya trajo en el milisegundo de la costura (para descartar repetidos en vivo)
        private bool _dedup; private List<OrdenOla> _dedupClaves; private HashSet<object> _dupRefs;

        private struct Evento { public object Ref; public OrdenOla O; public bool Nueva; }
        private readonly List<Evento> _bufer = new List<Evento>();
        public int Bufer => _bufer.Count;

        private PivoteOla[] _snapO = Array.Empty<PivoteOla>(), _snapM = Array.Empty<PivoteOla>();
        private long _snapVer = -1;

        public CargaOla(int clave, DateTime desde, DateTime hasta, decimal x, decimal mediana, int usadas, string fuenteX, bool sinRuedas,
                        string excluidas, TimeSpan periodo, bool viva, DateTime ahora)
        {
            Clave = clave; Desde = desde; Hasta = hasta; X = x; Mediana = mediana; Usadas = usadas; FuenteX = fuenteX ?? ""; XSinRuedas = sinRuedas;
            Excluidas = excluidas ?? ""; Periodo = periodo; RecibeVivo = viva; CreadaEn = ahora;
        }

        public bool Bufereando => Estado == EstadoOla.EnCola || Estado == EstadoOla.Pedida || Estado == EstadoOla.Procesando;
        public bool TieneMotor => Ola != null;

        public string EstadoTexto()
        {
            switch (Estado)
            {
                case EstadoOla.EnCola: return "en cola";
                case EstadoOla.Pedida: return "pedida";
                case EstadoOla.Procesando: return "procesando";
                case EstadoOla.EnVivo: return "en vivo";
                case EstadoOla.Completa: return "completa";
                case EstadoOla.SinCinta: return RecibeVivo ? "sin cinta: solo en vivo" : "sin cinta";
                case EstadoOla.SinRespuesta: return RecibeVivo ? "sin respuesta: solo en vivo" : "sin respuesta";
            }
            return Estado.ToString();
        }

        // ------------------------------------------------------------------ cortes
        /// <summary>El corte de la vela siguiente de la jornada (en orden: la vela PrimerBar + Cortes). Si no es posterior al anterior se corre
        /// un tick (100 ns) para que el indice k de cada vela no se pierda, y se cuenta.</summary>
        public void AgregarCorte(DateTime corte)
        {
            if (_cortes.Count > 0 && corte <= _cortes[_cortes.Count - 1]) { corte = _cortes[_cortes.Count - 1].AddTicks(1); CortesForzados++; }
            _cortes.Add(corte);
            if (Ola != null && !Bufereando) SincronizarCortes();
        }

        private void SincronizarCortes()
        {
            while (Ola.Cortes < _cortes.Count)
            {
                int antes = Ola.Cortes;
                var c = _cortes[antes];
                Ola.AgregarCorte(c); Marea.AgregarCorte(c);
                if (Ola.Cortes == antes) { CortesForzados++; break; }   // no deberia pasar: los cortes son crecientes
            }
            Version++;
        }

        // ------------------------------------------------------------------ en vivo
        /// <summary>OnCumulativeTrade. false si la orden es de la jornada siguiente (t &gt;= Hasta): la rutea el gestor.</summary>
        public bool Nueva(object r, in OrdenOla o)
        {
            if (o.T >= Hasta) return false;
            if (o.T < Desde) { Fuera++; return true; }
            if (Bufereando)
            {
                if (_bufer.Count >= MaxBufer) { SoloEnVivo(EstadoOla.SinCinta, "el bufer se lleno (" + MaxBufer + " eventos) esperando el historial"); }
                else
                {
                    if (PrimeraBufer == DateTime.MinValue) PrimeraBufer = o.T;
                    _bufer.Add(new Evento { Ref = r, O = o, Nueva = true });
                    if (_bufer.Count > BuferMax) BuferMax = _bufer.Count;
                    return true;
                }
            }
            if (Ola == null) return true;
            AplicarNueva(r, o);
            return true;
        }

        /// <summary>OnUpdateCumulativeTrade: la orden crecio. false si es de la jornada siguiente.</summary>
        public bool Actualizada(object r, in OrdenOla o)
        {
            if (o.T >= Hasta) return false;
            if (o.T < Desde) { Fuera++; return true; }
            if (Bufereando && _bufer.Count < MaxBufer) { CorregirEnBufer(r, o); return true; }
            if (Bufereando) SoloEnVivo(EstadoOla.SinCinta, "el bufer se lleno (" + MaxBufer + " eventos) esperando el historial");
            if (Ola == null) return true;
            AplicarCorreccion(r, o);
            return true;
        }

        private void CorregirEnBufer(object r, in OrdenOla o)
        {
            // la orden que crecio, por referencia, entre las ultimas del bufer: se reemplaza (todavia no entro al motor: queda exacto)
            for (int i = _bufer.Count - 1, n = 0; i >= 0 && n < BuscarEnBufer; i--, n++)
            {
                var e = _bufer[i];
                if (!ReferenceEquals(e.Ref, r)) continue;
                if (!e.O.MismaClave(o)) break;   // el mismo objeto con otra orden (ATAS lo reuso): no se toca
                e.O = o; _bufer[i] = e;
                CorreccionesBufer++;
                if (i != _bufer.Count - 1) ViejasBufer++;
                return;
            }
            // no esta: la orden que venia creciendo cuando arranco el bufer; al coser corrige la ultima del historial si es la misma
            _bufer.Add(new Evento { Ref = r, O = o, Nueva = false });
            HuerfanasBufer++;
            if (_bufer.Count > BuferMax) BuferMax = _bufer.Count;
        }

        private void AplicarNueva(object r, in OrdenOla o)
        {
            // ATAS mando otra vez como nueva la misma orden (mismo objeto y clave): es la misma que crecio
            if (_hayUlt && ReferenceEquals(r, _ultRef) && o.MismaClave(_ult)) { VivoRepetidas++; Corregir(o); return; }
            if (_dedup)
            {
                if (o.T < Costura) { VivoDuplicadas++; Repetida(r); return; }   // el historial ya la trajo
                if (o.T == Costura)
                {
                    int i = -1;
                    for (int j = 0; j < _dedupClaves.Count; j++) if (_dedupClaves[j].MismaClave(o)) { i = j; break; }
                    if (i >= 0)
                    {
                        _dedupClaves.RemoveAt(i); VivoDuplicadas++;
                        if (_hayUlt && _ultDeHistorial && o.MismaClave(_ult)) _ultRef = r;   // sus actualizaciones corrigen la ultima del historial
                        else Repetida(r);
                        return;
                    }
                }
                else _dedup = false;   // ya paso la costura
            }
            if (_hayUlt && o.T < _ult.T) VivoDesordenadas++;
            int p1 = Ola.Ola.Pivotes.Count, p4 = Marea.Ola.Pivotes.Count;
            Ola.Orden(o.T, o.Primero, o.Ultimo, o.Vol, o.Lado);
            Marea.Orden(o.T, o.Primero, o.Ultimo, o.Vol, o.Lado);
            if (Ola.Ola.Pivotes.Count != p1 || Marea.Ola.Pivotes.Count != p4) VersionPiv++;
            if (InicioMotor == DateTime.MinValue) InicioMotor = o.T;
            if (o.T == InicioMotor) NInicio++;
            _hayUlt = true; _ult = o; _ultRef = r; _ultDeHistorial = false;
            VivoNuevas++; Version++;
        }

        private void AplicarCorreccion(object r, in OrdenOla o)
        {
            if (_hayUlt && o.MismaClave(_ult))
            {
                if (ReferenceEquals(r, _ultRef)) { Corregir(o); return; }
                if (_ultDeHistorial) { CorreccionesCostura++; _ultRef = r; Corregir(o); return; }   // la que venia creciendo en la costura
            }
            if (_dupRefs != null && _dupRefs.Contains(r)) { ActDuplicadas++; return; }   // crecio una que el historial ya traia
            Viejas++; UltimaVieja = o;
        }

        /// <summary>Una orden en vivo que el historial ya traia (bufer vacio): sus actualizaciones tampoco cuentan.</summary>
        private void Repetida(object r)
        {
            if (r == null) return;
            if (_dupRefs == null) _dupRefs = new HashSet<object>(ReferenceEqualityComparer.Instance);
            if (_dupRefs.Count < 10000) _dupRefs.Add(r);
        }

        private void Corregir(in OrdenOla o)
        {
            Ola.CorregirUltima(o.T, o.Primero, o.Ultimo, o.Vol, o.Lado);
            Marea.CorregirUltima(o.T, o.Primero, o.Ultimo, o.Vol, o.Lado);
            _ult = o; Correcciones++; VersionPiv++; Version++;
        }

        // ------------------------------------------------------------------ el historial
        /// <summary>Llego el historial: decide la costura (bajo la llave) y arma el trabajo para el Task. topeVela = la ultima operacion de la
        /// ultima vela del grafico (DateTime.MinValue si no se sabe).</summary>
        public TrabajoOla Preparar(DateTime topeVela, int generacion, DateTime ahora)
        {
            Estado = EstadoOla.Procesando; RespondidaEn = ahora;
            int i0 = _bufer.FindIndex(e => e.Nueva);
            bool conBufer = i0 >= 0;
            DateTime tope;
            OrdenOla[] enTope = Array.Empty<OrdenOla>();
            if (conBufer)
            {
                tope = _bufer[i0].O.T;
                var l = new List<OrdenOla>();
                foreach (var e in _bufer) if (e.Nueva && e.O.T == tope) l.Add(e.O);
                enTope = l.ToArray();
            }
            else tope = RecibeVivo && topeVela > DateTime.MinValue ? topeVela : DateTime.MaxValue;
            return new TrabajoOla(this, generacion, Desde, Hasta, X, _cortes.ToArray(), conBufer, tope, enTope);
        }

        /// <summary>Bajo la llave, con el Task terminado: los motores del trabajo pasan a ser los de la carga, entran los cortes que
        /// aparecieron mientras tanto y despues el bufer en orden.</summary>
        public void Integrar(TrabajoOla t, DateTime ahora)
        {
            HistRecibidas = t.Recibidas; HistNulas = t.Nulas; HistFuera = t.FueraDeRango; HistUsadas = t.Usadas; HistDescartadas = t.Descartadas;
            HistEnCostura = t.EnTopeUsadas; HistDesordenadas = t.Desordenadas; CosturaDudosa = t.CosturaDudosa; SinSolape = t.SinSolape; MsProceso = t.Ms;
            IntegradaEn = ahora;
            if (t.Error != null || t.Ola == null) { SoloEnVivo(EstadoOla.SinCinta, "error al procesar el historial: " + (t.Error?.Message ?? "sin motores")); return; }
            bool vacio = t.EnRango == 0;
            if (vacio)
            {
                // puede pasar en el replay de ATAS: la viva sigue solo con lo que llega en vivo; una pasada queda sin motor
                SoloEnVivo(EstadoOla.SinCinta, t.Recibidas == 0 ? "el historial volvio vacio" : "el historial no trajo ordenes de la jornada (" + t.Recibidas + " fuera de rango)");
                return;
            }
            Ola = t.Ola; Marea = t.Marea;
            SincronizarCortes();   // los cortes de las velas que aparecieron mientras corria el Task
            InicioMotor = Desde;
            UltimaHist = t.UltimaT;
            if (t.ConBufer) { Costura = t.Tope; CosturaTipo = "bufer"; }
            else
            {
                Costura = t.UltimaT;
                CosturaTipo = !RecibeVivo ? "completa" : t.Tope < DateTime.MaxValue ? "fin del historial, con tope en la ultima vela" : "fin del historial";
                if (RecibeVivo) { _dedup = true; _dedupClaves = new List<OrdenOla>(t.EnLaUltimaHora); }
                // el historial termina mas de 2 s antes de lo que ya habia visto el grafico y no llego nada en vivo: puede faltar un tramo
                if (RecibeVivo && t.Tope < DateTime.MaxValue && t.Tope > t.UltimaT.AddSeconds(2)) SinSolape = true;
            }
            _hayUlt = t.HayUltima; _ult = t.Ultima; _ultRef = t.UltimaRef; _ultDeHistorial = t.HayUltima;
            Repasar();   // el bufer, en su orden
            if (!RecibeVivo) { Ola.TomarHasta(DateTime.MaxValue); Marea.TomarHasta(DateTime.MaxValue); }
            Estado = RecibeVivo ? EstadoOla.EnVivo : EstadoOla.Completa;
            VersionPiv++; Version++;
        }

        /// <summary>Sin historial (vacio, error, sin respuesta o bufer lleno): la viva arranca motores nuevos con sus cortes y sigue con el
        /// bufer y lo que llegue en vivo; una pasada queda sin motor.</summary>
        public void SoloEnVivo(EstadoOla estado, string motivo)
        {
            Motivo = motivo ?? "";
            CosturaTipo = estado == EstadoOla.SinRespuesta ? "sin respuesta" : "sin cinta";
            if (!RecibeVivo) { Estado = estado; _bufer.Clear(); _bufer.TrimExcess(); return; }
            Ola = new OlaFotos(X); Marea = new OlaFotos(4 * X);
            Estado = estado;   // ya no bufferea: SincronizarCortes y Repasar van al motor
            SincronizarCortes();
            _hayUlt = false; _ultDeHistorial = false; _dedup = false;
            Repasar();
            VersionPiv++; Version++;
        }

        private void Repasar()
        {
            foreach (var e in _bufer)
            {
                if (e.Nueva) AplicarNueva(e.Ref, e.O);
                else AplicarCorreccion(e.Ref, e.O);
            }
            _bufer.Clear(); _bufer.TrimExcess();
        }

        /// <summary>La jornada termino (la viva paso a pasada): las fotos que faltan son el estado final.</summary>
        public void Cerrar()
        {
            RecibeVivo = false;
            if (Ola != null && !Bufereando) { Ola.TomarHasta(DateTime.MaxValue); Marea.TomarHasta(DateTime.MaxValue); Version++; }
            if (Estado == EstadoOla.EnVivo) Estado = EstadoOla.Completa;
        }

        // ------------------------------------------------------------------ lectura
        /// <summary>Los primeros n pivotes confirmados, de una copia que no cambia (se rehace solo si cambio algun pivote).</summary>
        public IReadOnlyList<PivoteOla> Pivotes(bool marea, int n)
        {
            if (Ola == null) return Array.Empty<PivoteOla>();
            if (_snapVer != VersionPiv)
            {
                _snapO = Copiar(Ola.Ola.Pivotes); _snapM = Copiar(Marea.Ola.Pivotes); _snapVer = VersionPiv;
            }
            var a = marea ? _snapM : _snapO;
            if (n > a.Length) { _snapO = Copiar(Ola.Ola.Pivotes); _snapM = Copiar(Marea.Ola.Pivotes); a = marea ? _snapM : _snapO; }
            n = Math.Max(0, Math.Min(n, a.Length));
            return new ArraySegment<PivoteOla>(a, 0, n);
        }

        private static PivoteOla[] Copiar(IReadOnlyList<PivoteOla> l)
        {
            var a = new PivoteOla[l.Count];
            for (int i = 0; i < a.Length; i++) a[i] = l[i];
            return a;
        }
    }

    /// <summary>
    /// El historial de una carga, corriendo en un Task con motores LOCALES (no comparte nada con la carga hasta Integrar). Recibe las
    /// ordenes en el orden en que las devolvio ATAS (el mismo de las cintas de la sonda) y las filtra: [Desde, Hasta) y la costura.
    /// </summary>
    public sealed class TrabajoOla
    {
        public readonly CargaOla Carga;
        public readonly int Generacion;
        private readonly DateTime _desde, _hasta;
        private readonly decimal _x;
        private readonly DateTime[] _cortes;
        public readonly bool ConBufer;
        public readonly DateTime Tope;
        private readonly OrdenOla[] _enTopeBufer;
        private readonly List<(object Ref, OrdenOla O)> _enTope = new List<(object, OrdenOla)>();

        public OlaFotos Ola, Marea;
        public long Recibidas, Nulas, FueraDeRango, EnRango, Usadas, Descartadas, Desordenadas, EnTopeUsadas;
        public bool CosturaDudosa, SinSolape, HayUltima;
        public OrdenOla Ultima; public object UltimaRef;
        public DateTime UltimaT = DateTime.MinValue;
        public readonly List<OrdenOla> EnLaUltimaHora = new List<OrdenOla>();
        public Exception Error;
        public double Ms;
        private System.Diagnostics.Stopwatch _sw;

        internal TrabajoOla(CargaOla c, int gen, DateTime desde, DateTime hasta, decimal x, DateTime[] cortes, bool conBufer, DateTime tope, OrdenOla[] enTope)
        {
            Carga = c; Generacion = gen; _desde = desde; _hasta = hasta; _x = x; _cortes = cortes; ConBufer = conBufer; Tope = tope; _enTopeBufer = enTope;
        }

        public void Empezar()
        {
            _sw = System.Diagnostics.Stopwatch.StartNew();
            Ola = new OlaFotos(_x); Marea = new OlaFotos(4 * _x);
            foreach (var c in _cortes) { Ola.AgregarCorte(c); Marea.AgregarCorte(c); }
        }

        public void Nula() { Recibidas++; Nulas++; }

        public void Orden(object r, in OrdenOla o)
        {
            Recibidas++;
            if (o.T < _desde || o.T >= _hasta) { FueraDeRango++; return; }
            EnRango++;
            if (o.T > Tope) { Descartadas++; return; }   // con bufer: la trae el bufer; sin bufer: despues de la ultima vela, llega en vivo
            if (ConBufer && o.T == Tope) { _enTope.Add((r, o)); return; }   // el milisegundo de la costura se decide al final
            Pasar(r, o);
        }

        private void Pasar(object r, in OrdenOla o)
        {
            if (HayUltima && o.T < Ultima.T) Desordenadas++;
            Ola.Orden(o.T, o.Primero, o.Ultimo, o.Vol, o.Lado);
            Marea.Orden(o.T, o.Primero, o.Ultimo, o.Vol, o.Lado);
            Usadas++; Ultima = o; UltimaRef = r; HayUltima = true;
            if (o.T > UltimaT) { UltimaT = o.T; EnLaUltimaHora.Clear(); }
            if (o.T == UltimaT) EnLaUltimaHora.Add(o);
        }

        /// <summary>El milisegundo de la costura: de las ordenes del historial con t == T_costura entran las primeras (las que pasaron antes de
        /// que arrancara el bufer); las ultimas tantas como trae el bufer en ese milisegundo son las del bufer. Control: tienen que ser las
        /// mismas (clave) y en el mismo orden; si no, la costura queda marcada como dudosa.</summary>
        public void Terminar()
        {
            if (ConBufer && _enTope.Count > 0)
            {
                int nb = _enTopeBufer.Length, solo = Math.Max(0, _enTope.Count - nb);
                for (int i = 0; i < solo; i++) { Pasar(_enTope[i].Ref, _enTope[i].O); EnTopeUsadas++; }
                for (int i = solo, j = 0; i < _enTope.Count && j < nb; i++, j++)
                    if (!_enTope[i].O.MismaClave(_enTopeBufer[j])) CosturaDudosa = true;
                Descartadas += _enTope.Count - solo;
            }
            // sin solape: el historial termino antes de la primera orden del bufer. Puede ser mercado quieto (nada en el medio) o un hueco: se avisa
            if (ConBufer && _enTope.Count == 0 && Descartadas == 0 && EnRango > 0) SinSolape = true;
            Ms = _sw != null ? _sw.Elapsed.TotalMilliseconds : 0;
        }
    }

    /// <summary>
    /// TODAS LAS CARGAS de un grafico: las velas (apertura, jornada de CME y rango RTH de cada una), la viva, hasta 4 pasadas en cache y lo
    /// que llega en vivo antes de que exista la viva. Las velas entran por Vela() (OnCalculate); la viva se crea con CrearViva() cuando el
    /// grafico tiene todas sus velas, y cambia sola al empezar otra jornada (por la vela nueva o por la primera orden de la jornada nueva).
    /// </summary>
    public sealed class GestorOla
    {
        public const int MaxPasadas = 4;
        public const int MaxPendientes = 200000;

        private readonly TimeZoneInfo _ny;
        private readonly List<DateTime> _aper = new List<DateTime>();       // apertura UTC de cada vela (indice = bar)
        private readonly List<int> _claveBar = new List<int>();             // jornada de CME de cada vela
        private readonly Dictionary<int, int[]> _jor = new Dictionary<int, int[]>();   // clave -> [primera, ultima]
        private RuedasOla _ruedas = new RuedasOla();
        private readonly List<CargaOla> _pasadas = new List<CargaOla>();
        private List<(object Ref, OrdenOla O, bool Nueva)> _pend = new List<(object, OrdenOla, bool)>();
        private long _acceso;
        private int _querida = -1;

        public decimal XFija, Tick = 0.25m;
        public TimeSpan Declarado; public bool NoEsTiempo;
        public TimeSpan Periodo { get; private set; }
        public bool PeriodoListo { get; private set; }
        public string PeriodoFuente { get; private set; } = "";
        public double PctMultiplos { get; private set; }
        public DateTime UltimaHoraVela { get; private set; } = DateTime.MinValue;   // ultima operacion de la ultima vela (UTC)
        public CargaOla Viva { get; private set; }
        public int Generacion { get; private set; }
        public long PendTirados, PendFuera, VelasSaltadas, CambiosDeJornada, CortesCorridos;
        public Func<DateTime> Reloj = () => DateTime.UtcNow;   // el arnes lo reemplaza
        public Action<string> Anotar;                           // registro (el indicador lo manda al log)

        public GestorOla(TimeZoneInfo ny) { _ny = ny ?? TimeZoneInfo.Utc; }

        public int Velas => _aper.Count;
        public IReadOnlyList<CargaOla> Pasadas => _pasadas;
        public int Pendientes => _pend.Count;

        public void ReiniciarCargas()
        {
            Viva = null; _pasadas.Clear(); _pend.Clear(); _querida = -1; Generacion++;
            PendTirados = PendFuera = CambiosDeJornada = CortesCorridos = 0;
        }

        public void ReiniciarTodo()
        {
            ReiniciarCargas();
            _aper.Clear(); _claveBar.Clear(); _jor.Clear();
            _ruedas = new RuedasOla();
            PeriodoListo = false; Periodo = TimeSpan.Zero; PeriodoFuente = ""; PctMultiplos = 0; UltimaHoraVela = DateTime.MinValue; VelasSaltadas = 0;
        }

        public DateTime Ny(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _ny);

        /// <summary>[Desde, Hasta) UTC de la jornada: 18:00 NY de la vispera (el lunes, el domingo) a 17:00 NY.</summary>
        public void Rango(int clave, out DateTime desde, out DateTime hasta)
        {
            var d = Jornadas.InicioNominal(clave, Jornadas.ParteCompleta);
            var h = Jornadas.Fecha(clave).AddHours(17);
            desde = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(d, DateTimeKind.Unspecified), _ny), DateTimeKind.Utc);
            hasta = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(h, DateTimeKind.Unspecified), _ny), DateTimeKind.Utc);
        }

        // ------------------------------------------------------------------ velas
        /// <summary>
        /// Una vela del grafico (OnCalculate): numero, apertura y ultima operacion (UTC), alto y bajo. Las velas llegan en orden; la ultima
        /// vuelve a llegar con cada operacion. Devuelve false si hay un salto de numeros (el indicador completa las que faltan antes).
        /// </summary>
        public bool Vela(int bar, DateTime aperUtc, DateTime finUtc, decimal alto, decimal bajo)
        {
            if (bar < 0) return true;
            if (bar == 0 && _aper.Count > 0) ReiniciarTodo();   // el grafico se recargo sin avisar
            if (bar > _aper.Count) { VelasSaltadas++; return false; }
            DateTime fin = finUtc > aperUtc ? finUtc : aperUtc;
            DateTime nyA = Ny(aperUtc), nyF = Ny(fin);
            _ruedas.Agregar(nyA, nyF, alto, bajo);
            if (bar >= _aper.Count - 1 && fin > UltimaHoraVela) UltimaHoraVela = fin;   // lo ultimo que vio el grafico
            if (bar < _aper.Count) return true;
            // vela nueva
            int clave = Jornadas.Clave(nyA);
            _aper.Add(aperUtc); _claveBar.Add(clave);
            if (_jor.TryGetValue(clave, out var r)) { if (bar > r[1]) r[1] = bar; }
            else _jor[clave] = new[] { bar, bar };
            var v = Viva;
            if (v != null)
            {
                if (clave > v.Clave)
                {
                    // sin periodo, el corte de la ultima vela de la jornada que termina es la apertura de esta
                    if (Periodo == TimeSpan.Zero && v.PrimerBar >= 0 && v.Cortes == bar - 1 - v.PrimerBar) v.AgregarCorte(aperUtc);
                    CambioDeJornada(clave, "vela nueva"); v = Viva;
                }
                if (clave == v.Clave) CortesDeVelaNueva(v, bar);
            }
            return true;
        }

        public DateTime Apertura(int bar) => _aper[bar];
        public int ClaveDe(int bar) => bar >= 0 && bar < _claveBar.Count ? _claveBar[bar] : -1;

        public bool BarrasDe(int clave, out int primera, out int ultima)
        {
            if (_jor.TryGetValue(clave, out var r)) { primera = r[0]; ultima = r[1]; return true; }
            primera = ultima = -1; return false;
        }

        /// <summary>La vela de un instante UTC: la ultima que abre a esa hora o antes (busqueda binaria). -1 si es anterior a la primera.</summary>
        public int BarDeHora(DateTime utc)
        {
            int lo = 0, hi = _aper.Count - 1, r = -1;
            while (lo <= hi)
            {
                int m = (lo + hi) >> 1;
                if (_aper[m] <= utc) { r = m; lo = m + 1; } else hi = m - 1;
            }
            return r;
        }

        /// <summary>El corte de la vela b: en velas de tiempo, apertura + periodo (o la apertura de la siguiente si llega antes); si no, la
        /// apertura de la siguiente. false si todavia no se sabe (la ultima vela de un grafico que no es de tiempo).</summary>
        private bool CorteDe(int b, out DateTime corte)
        {
            bool haySig = b + 1 < _aper.Count;
            if (Periodo > TimeSpan.Zero)
            {
                corte = _aper[b] + Periodo;
                if (haySig && _aper[b + 1] < corte) corte = _aper[b + 1];
                return true;
            }
            corte = haySig ? _aper[b + 1] : DateTime.MinValue;
            return haySig;
        }

        private void CortesDeVelaNueva(CargaOla c, int bar)
        {
            if (c.PrimerBar < 0) c.PrimerBar = bar;
            if (Periodo > TimeSpan.Zero)
            {
                // el corte de la anterior ya estaba (apertura + periodo); si esta vela abrio antes, ya no se puede arreglar: se cuenta
                if (bar > c.PrimerBar && c.Cortes > 0 && _aper[bar] < c.Corte(c.Cortes - 1)) CortesCorridos++;
                if (CorteDe(bar, out var cv) && c.Cortes == bar - c.PrimerBar) c.AgregarCorte(cv);
            }
            else if (bar - 1 >= c.PrimerBar && c.Cortes == bar - 1 - c.PrimerBar) c.AgregarCorte(_aper[bar]);   // cierra la anterior
        }

        private void DetectarPeriodo()
        {
            if (PeriodoListo) return;
            Periodo = PeriodoVelas.Detectar(_aper, Declarado, NoEsTiempo, out double pct, out _, out string fuente);
            PctMultiplos = pct; PeriodoFuente = fuente; PeriodoListo = true;
        }

        // ------------------------------------------------------------------ cargas
        private CargaOla NuevaCarga(int clave, bool viva)
        {
            DetectarPeriodo();
            decimal x, med = 0m; int usadas = 0; string fuente; bool sinRuedas = false; string excl = "";
            if (XFija > 0m) { x = XFija; fuente = "fija"; }
            else
            {
                x = _ruedas.X(clave, Tick, Periodo, out med, out usadas, out sinRuedas, out var ex);
                fuente = sinRuedas ? "piso: sin ruedas completas" : usadas >= EscalaOla.Ruedas ? "auto" : "auto con " + usadas + " ruedas";
                excl = string.Join(" ", ex);
            }
            Rango(clave, out var desde, out var hasta);
            var c = new CargaOla(clave, desde, hasta, x, med, usadas, fuente, sinRuedas, excl, Periodo, viva, Reloj()) { Acceso = ++_acceso };
            if (BarrasDe(clave, out int p, out int u))
            {
                c.PrimerBar = p;
                for (int b = p; b <= u; b++)
                {
                    if (!CorteDe(b, out var corte)) break;
                    c.AgregarCorte(corte);
                }
            }
            return c;
        }

        /// <summary>La viva, cuando el grafico ya tiene todas sus velas: la jornada de la ultima vela. Toma lo que llego en vivo antes.</summary>
        public CargaOla CrearViva()
        {
            if (Viva != null || _aper.Count == 0) return Viva;
            Viva = NuevaCarga(_claveBar[_aper.Count - 1], true);
            Anotar?.Invoke("ola: viva " + Viva.Clave + " creada, X " + Viva.X.ToString(CultureInfo.InvariantCulture) + " (" + Viva.FuenteX + "), " + Viva.Cortes + " cortes, periodo " + Periodo.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " s (" + PeriodoFuente + ")");
            TomarPendientes();
            return Viva;
        }

        private void CambioDeJornada(int clave, string porque)
        {
            var vieja = Viva;
            vieja.Cerrar();
            AgregarPasada(vieja);
            Viva = NuevaCarga(clave, true);
            CambiosDeJornada++;
            Anotar?.Invoke("ola: cambio de jornada (" + porque + "): " + vieja.Clave + " pasa a pasada (" + vieja.EstadoTexto() + "), viva " + clave + " X " + Viva.X.ToString(CultureInfo.InvariantCulture) + " (" + Viva.FuenteX + ")");
            TomarPendientes();
        }

        private void AgregarPasada(CargaOla c)
        {
            c.Acceso = ++_acceso;
            _pasadas.Add(c);
            while (_pasadas.Count > MaxPasadas)
            {
                int quitar = -1; long min = long.MaxValue;
                for (int i = 0; i < _pasadas.Count; i++)
                {
                    var p = _pasadas[i];
                    if (p.Estado == EstadoOla.Pedida || p.Estado == EstadoOla.Procesando) continue;   // en vuelo: no se tira
                    if (p.Acceso < min) { min = p.Acceso; quitar = i; }
                }
                if (quitar < 0) break;
                Anotar?.Invoke("ola: sale de la cache la jornada " + _pasadas[quitar].Clave);
                _pasadas.RemoveAt(quitar);
            }
        }

        public CargaOla Buscar(int clave)
        {
            if (Viva != null && Viva.Clave == clave) return Viva;
            foreach (var p in _pasadas) if (p.Clave == clave) return p;
            return null;
        }

        public bool Conoce(CargaOla c) => c != null && (ReferenceEquals(c, Viva) || _pasadas.Contains(c));

        /// <summary>La vela del momento (la ultima a la vista): si cae en una jornada sin carga, esa jornada se pide (la ultima que se quiso
        /// manda: si se corre el grafico antes de pedirla, se pide la nueva).</summary>
        public void QuererMomento(int bar)
        {
            int clave = ClaveDe(bar);
            if (clave < 0) return;
            var c = Buscar(clave);
            if (c != null) { c.Acceso = ++_acceso; return; }
            _querida = clave;
        }

        /// <summary>La siguiente carga a pedir: la viva primero; despues la pasada que quiere el momento (se crea aca); despues cualquier
        /// pasada en cola. null: nada.</summary>
        public CargaOla SiguientePedido()
        {
            if (Viva != null && Viva.Estado == EstadoOla.EnCola) return Viva;
            if (_querida > 0)
            {
                int q = _querida; _querida = -1;
                if (Buscar(q) == null && _jor.ContainsKey(q) && (Viva == null || q != Viva.Clave))
                {
                    var c = NuevaCarga(q, false);
                    AgregarPasada(c);
                    return c;
                }
            }
            foreach (var p in _pasadas) if (p.Estado == EstadoOla.EnCola) return p;
            return null;
        }

        public void MarcarPedida(CargaOla c, object pedido)
        {
            c.Estado = EstadoOla.Pedida; c.Pedido = pedido; c.PedidaEn = Reloj();
        }

        public void VolverACola(CargaOla c) { if (c.Estado == EstadoOla.Pedida) { c.Estado = EstadoOla.EnCola; c.Pedido = null; } }

        public TrabajoOla Preparar(CargaOla c)
        {
            if (!Conoce(c) || c.Estado != EstadoOla.Pedida) return null;
            c.Pedido = null;
            return c.Preparar(UltimaHoraVela, Generacion, Reloj());
        }

        /// <summary>Integra un trabajo terminado. Devuelve el resumen para el registro (null si la carga ya no existe: se reinicio).</summary>
        public string Integrar(TrabajoOla t)
        {
            var c = t.Carga;
            if (t.Generacion != Generacion || !Conoce(c) || c.Estado != EstadoOla.Procesando) return null;
            c.Integrar(t, Reloj());
            return Resumen(c);
        }

        public void SinRespuesta(CargaOla c, string motivo)
        {
            if (!Conoce(c)) return;
            c.Pedido = null;
            c.SoloEnVivo(EstadoOla.SinRespuesta, motivo);
        }

        public static string Resumen(CargaOla c)
        {
            var I = CultureInfo.InvariantCulture;
            return "jornada " + c.Clave + " " + c.EstadoTexto() + (c.Motivo.Length > 0 ? " (" + c.Motivo + ")" : "") + ", X " + c.X.ToString(I) + " (" + c.FuenteX + ")"
                 + ", historial " + c.HistRecibidas + " recibidas, " + c.HistUsadas + " usadas, " + c.HistDescartadas + " despues de la costura, " + c.HistFuera + " fuera de la jornada"
                 + ", costura " + (c.CosturaTipo.Length > 0 ? c.CosturaTipo : "-") + (c.Costura > DateTime.MinValue ? " " + c.Costura.ToString("HH:mm:ss.fff", I) + " UTC" : "")
                 + (c.CosturaDudosa ? " DUDOSA" : "") + ", bufer max " + c.BuferMax + ", vivo " + c.VivoNuevas + " nuevas, " + c.Correcciones + " correcciones"
                 + ", viejas " + c.Viejas + ", cortes " + c.Cortes + " (tarde " + c.CortesTarde + ", forzados " + c.CortesForzados + "), proceso " + c.MsProceso.ToString("0", I) + " ms";
        }

        // ------------------------------------------------------------------ en vivo
        public void Nueva(object r, in OrdenOla o) => Evento(r, o, true);
        public void Actualizada(object r, in OrdenOla o) => Evento(r, o, false);

        private void Evento(object r, in OrdenOla o, bool nueva)
        {
            var v = Viva;
            if (v == null) { Pendiente(r, o, nueva); return; }
            if (nueva ? v.Nueva(r, o) : v.Actualizada(r, o)) return;
            // t >= Hasta de la viva: la primera orden de la jornada siguiente (antes de que aparezca su vela) abre la jornada nueva
            int clave = Jornadas.Clave(Ny(o.T));
            if (clave > v.Clave) { CambioDeJornada(clave, "primera orden de la jornada"); v = Viva; if (nueva ? v.Nueva(r, o) : v.Actualizada(r, o)) return; }
            v.Fuera++;   // entre las 17:00 y las 18:00 NY: no es de ninguna jornada
        }

        private void Pendiente(object r, in OrdenOla o, bool nueva)
        {
            if (_pend.Count >= MaxPendientes) { PendTirados++; return; }
            _pend.Add((r, o, nueva));
        }

        private void TomarPendientes()
        {
            if (_pend.Count == 0) return;
            var l = _pend; _pend = new List<(object, OrdenOla, bool)>();
            foreach (var e in l)
            {
                if (Viva != null && e.O.T < Viva.Desde) { PendFuera++; continue; }
                Evento(e.Ref, e.O, e.Nueva);
            }
        }

        // ------------------------------------------------------------------ lectura
        /// <summary>La foto de la jornada de la vela 'bar' al cierre de esa vela; en la vela viva, la de ahora (OlaFotos.Foto).</summary>
        public bool AlMomento(int bar, out FotoOla ola, out FotoOla marea, out IReadOnlyList<PivoteOla> pivOla, out IReadOnlyList<PivoteOla> pivMarea,
                              out decimal x, out string estado, out CargaOla carga)
        {
            ola = marea = default; pivOla = pivMarea = Array.Empty<PivoteOla>(); x = 0m; carga = null;
            int clave = ClaveDe(bar);
            if (clave < 0) { estado = "sin vela"; return false; }
            var c = Buscar(clave);
            if (c == null) { estado = "sin carga"; return false; }
            carga = c; c.Acceso = ++_acceso; x = c.X;
            estado = c.EstadoTexto() + (c.FuenteX == "auto" ? "" : "; X " + c.FuenteX);
            if (!c.TieneMotor || c.PrimerBar < 0 || c.Bufereando) return false;
            int k = bar - c.PrimerBar;
            // el grafico va adelante de la cinta en vivo (replay en modo velas, o el vivo cortado): la foto de una vela que no se tomo
            // seria la de la ultima orden que llego, no la de esa vela
            if (k >= c.Ola.Tomadas && Atrasada(c, out var uo))
            {
                estado = "atrasada: la cinta en vivo no llega (ultima orden " + (uo == c.Desde ? "ninguna" : uo.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " UTC")
                       + ", el grafico va por las " + UltimaHoraVela.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + ")";
                return false;
            }
            ola = c.Ola.Foto(k); marea = c.Marea.Foto(k);
            pivOla = c.Pivotes(false, ola.Pivotes); pivMarea = c.Pivotes(true, marea.Pivotes);
            return true;
        }

        public const double ToleranciaAtrasoSeg = 5;

        /// <summary>La viva quedo atras del grafico: la ultima operacion de la ultima vela es mas de 5 s posterior a la ultima orden que entro
        /// al motor (en vivo normal van juntas; en el replay en modo velas OnCumulativeTrade puede no llegar). ultimaOrden: la del motor (o el
        /// comienzo de la jornada si no entro ninguna).</summary>
        public bool Atrasada(CargaOla c, out DateTime ultimaOrden)
        {
            ultimaOrden = DateTime.MinValue;
            if (c == null || !c.RecibeVivo || !c.TieneMotor || c.Bufereando) return false;
            ultimaOrden = c.Ola.Ola.Ordenes > 0 ? c.Ola.Ola.UltimaHora : c.Desde;
            return UltimaHoraVela > ultimaOrden.AddSeconds(ToleranciaAtrasoSeg);
        }

        /// <summary>Copia lo que hace falta para el CSV de la auditoria (bajo la llave; el formato se arma afuera).</summary>
        public InstantaneaOla Instantanea(CargaOla c)
        {
            var s = new InstantaneaOla
            {
                Clave = c.Clave, Estado = c.EstadoTexto(), Motivo = c.Motivo, X = c.X, Mediana = c.Mediana, Usadas = c.Usadas, FuenteX = c.FuenteX,
                Excluidas = c.Excluidas, Desde = c.Desde, Hasta = c.Hasta, InicioMotor = c.InicioMotor, NInicio = c.NInicio, Costura = c.Costura,
                CosturaTipo = c.CosturaTipo, CosturaDudosa = c.CosturaDudosa, SinSolape = c.SinSolape, PrimerBar = c.PrimerBar, Periodo = c.Periodo, PeriodoFuente = PeriodoFuente,
                Generado = Reloj(), RecibeVivo = c.RecibeVivo, Version = c.Version,
                HistRecibidas = c.HistRecibidas, HistFuera = c.HistFuera, HistUsadas = c.HistUsadas, HistDescartadas = c.HistDescartadas,
                HistEnCostura = c.HistEnCostura, VivoNuevas = c.VivoNuevas, VivoDuplicadas = c.VivoDuplicadas, VivoRepetidas = c.VivoRepetidas,
                VivoDesordenadas = c.VivoDesordenadas, Correcciones = c.Correcciones, CorreccionesCostura = c.CorreccionesCostura, Viejas = c.Viejas,
                ViejasBufer = c.ViejasBufer, CorreccionesBufer = c.CorreccionesBufer, HuerfanasBufer = c.HuerfanasBufer, Fuera = c.Fuera, BuferMax = c.BuferMax,
                ActDuplicadas = c.ActDuplicadas,
                CortesForzados = c.CortesForzados, CortesCorridos = CortesCorridos
            };
            BarrasDe(c.Clave, out _, out int ult);
            s.UltimoBar = ult;
            s.Cortes = new DateTime[c.Cortes];
            for (int k = 0; k < s.Cortes.Length; k++) s.Cortes[k] = c.Corte(k);
            if (c.TieneMotor && !c.Bufereando)
            {
                s.HayMotor = true;
                int filas = Math.Max(s.Cortes.Length, c.PrimerBar >= 0 && ult >= c.PrimerBar ? ult - c.PrimerBar + 1 : 0);
                s.FotosOla = new FotoOla[filas]; s.FotosMarea = new FotoOla[filas];
                for (int k = 0; k < filas; k++) { s.FotosOla[k] = c.Ola.Foto(k); s.FotosMarea[k] = c.Marea.Foto(k); }
                s.TomadasOla = c.Ola.Tomadas; s.TomadasMarea = c.Marea.Tomadas;
                s.CortesTardeOla = c.Ola.CortesTarde; s.CortesTardeMarea = c.Marea.CortesTarde;
                s.PivOla = Arreglo(c.Pivotes(false, int.MaxValue));
                s.PivMarea = Arreglo(c.Pivotes(true, int.MaxValue));
                s.ActualOla = c.Ola.Actual; s.ActualMarea = c.Marea.Actual;
                s.N = c.Ola.Ola.Ordenes; s.TUltima = c.Ola.Ola.UltimaHora;
            }
            return s;
        }

        private static PivoteOla[] Arreglo(IReadOnlyList<PivoteOla> l)
        {
            var a = new PivoteOla[l.Count];
            for (int i = 0; i < a.Length; i++) a[i] = l[i];
            return a;
        }

        // ------------------------------------------------------------------ auditoria (renglon JSON de cada minuto)
        private static readonly CultureInfo I = CultureInfo.InvariantCulture;
        private static string J(string s) => (s ?? "").Replace("\\", "/").Replace("\"", "'").Replace('\n', ' ').Replace('\r', ' ');
        private static string Hu(DateTime t) => t == DateTime.MinValue || t == DateTime.MaxValue ? "" : t.ToString("yyyy-MM-ddTHH:mm:ss.fff", I);
        private static string Fd(decimal v) => v.ToString(I);

        /// <summary>Los campos de la seccion "ola" de auditoria-*.jsonl (cada uno empieza con coma; el indicador abre y cierra el objeto):
        /// periodo, velas, la carga de la vela del momento (clave, X, mediana y ruedas usadas, estado, ordenes del historial y en vivo,
        /// correcciones, viejas, bufer, costura, cortes tarde), su foto de la ola y de la marea y la lista de cargas. Bajo la llave.</summary>
        public void AuditoriaJson(StringBuilder s, int momento)
        {
            s.Append(",\"periodo_s\":").Append(Periodo.TotalSeconds.ToString(I)).Append(",\"periodo_fuente\":\"").Append(J(PeriodoFuente))
             .Append("\",\"multiplos_pct\":").Append(PctMultiplos.ToString("0.0", I)).Append(",\"velas\":").Append(Velas)
             .Append(",\"ultima_vela\":\"").Append(Hu(UltimaHoraVela)).Append("\",\"pendientes\":").Append(Pendientes)
             .Append(",\"pend_tirados\":").Append(PendTirados).Append(",\"pend_fuera\":").Append(PendFuera).Append(",\"cambios_de_jornada\":").Append(CambiosDeJornada)
             .Append(",\"cortes_corridos\":").Append(CortesCorridos).Append(",\"velas_saltadas\":").Append(VelasSaltadas)
             .Append(",\"momento\":").Append(momento);
            bool hay = AlMomento(momento, out var fo, out var fm, out var po, out var pm, out _, out string estado, out var c);
            s.Append(",\"hay\":").Append(hay ? "true" : "false").Append(",\"estado\":\"").Append(J(estado)).Append('"');
            if (c != null)
            {
                s.Append(",\"clave\":").Append(c.Clave).Append(",\"x\":").Append(Fd(c.X)).Append(",\"mediana\":").Append(Fd(c.Mediana))
                 .Append(",\"ruedas_usadas\":").Append(c.Usadas).Append(",\"x_fuente\":\"").Append(J(c.FuenteX)).Append("\",\"excluidas\":\"").Append(J(c.Excluidas))
                 .Append("\",\"viva\":").Append(c.RecibeVivo ? "true" : "false").Append(",\"motivo\":\"").Append(J(c.Motivo))
                 .Append("\",\"desde\":\"").Append(Hu(c.Desde)).Append("\",\"hasta\":\"").Append(Hu(c.Hasta))
                 .Append("\",\"primer_bar\":").Append(c.PrimerBar).Append(",\"k\":").Append(c.PrimerBar >= 0 ? momento - c.PrimerBar : -1)
                 .Append(",\"historial\":{\"recibidas\":").Append(c.HistRecibidas).Append(",\"usadas\":").Append(c.HistUsadas)
                 .Append(",\"despues_de_la_costura\":").Append(c.HistDescartadas).Append(",\"en_la_costura\":").Append(c.HistEnCostura)
                 .Append(",\"fuera\":").Append(c.HistFuera).Append(",\"nulas\":").Append(c.HistNulas).Append(",\"desordenadas\":").Append(c.HistDesordenadas)
                 .Append(",\"ms\":").Append(c.MsProceso.ToString("0", I)).Append('}')
                 .Append(",\"vivo\":{\"nuevas\":").Append(c.VivoNuevas).Append(",\"correcciones\":").Append(c.Correcciones)
                 .Append(",\"correcciones_costura\":").Append(c.CorreccionesCostura).Append(",\"viejas\":").Append(c.Viejas)
                 .Append(",\"duplicadas\":").Append(c.VivoDuplicadas).Append(",\"act_duplicadas\":").Append(c.ActDuplicadas).Append(",\"repetidas\":").Append(c.VivoRepetidas)
                 .Append(",\"desordenadas\":").Append(c.VivoDesordenadas).Append(",\"fuera\":").Append(c.Fuera).Append('}')
                 .Append(",\"bufer\":{\"ahora\":").Append(c.Bufer).Append(",\"max\":").Append(c.BuferMax).Append(",\"corregidas\":").Append(c.CorreccionesBufer)
                 .Append(",\"viejas\":").Append(c.ViejasBufer).Append(",\"huerfanas\":").Append(c.HuerfanasBufer).Append('}')
                 .Append(",\"costura\":{\"t\":\"").Append(Hu(c.Costura)).Append("\",\"tipo\":\"").Append(J(c.CosturaTipo))
                 .Append("\",\"ultima_hist\":\"").Append(Hu(c.UltimaHist)).Append("\",\"primera_bufer\":\"").Append(Hu(c.PrimeraBufer))
                 .Append("\",\"inicio_motor\":\"").Append(Hu(c.InicioMotor)).Append("\",\"dudosa\":").Append(c.CosturaDudosa ? "true" : "false")
                 .Append(",\"sin_solape\":").Append(c.SinSolape ? "true" : "false").Append('}')
                 .Append(",\"cortes\":").Append(c.Cortes).Append(",\"cortes_tarde\":").Append(c.CortesTarde).Append(",\"cortes_forzados\":").Append(c.CortesForzados)
                 .Append(",\"ordenes_motor\":").Append(c.TieneMotor ? c.Ola.Ola.Ordenes : 0)
                 .Append(",\"t_motor\":\"").Append(c.TieneMotor ? Hu(c.Ola.Ola.UltimaHora) : "").Append('"');
                bool atr = Atrasada(c, out var uo);
                s.Append(",\"atrasada\":").Append(atr ? "true" : "false")
                 .Append(",\"atraso_s\":").Append(c.RecibeVivo && c.TieneMotor && !c.Bufereando ? (UltimaHoraVela - uo).TotalSeconds.ToString("0.000", I) : "null");
            }
            if (hay) { s.Append(",\"foto\":{"); FotoJson(s, "ola", fo, po.Count); s.Append(','); FotoJson(s, "marea", fm, pm.Count); s.Append('}'); }
            s.Append(",\"cargas\":[");
            var todas = new List<CargaOla>();
            if (Viva != null) todas.Add(Viva);
            todas.AddRange(_pasadas);
            for (int i = 0; i < todas.Count; i++)
            {
                var q = todas[i];
                if (i > 0) s.Append(',');
                s.Append('[').Append(q.Clave).Append(",\"").Append(J(q.EstadoTexto())).Append("\",").Append(Fd(q.X)).Append(',').Append(q.Usadas).Append(',')
                 .Append(q.Cortes).Append(',').Append(q.CortesTarde).Append(',').Append(q.Viejas).Append(',').Append(q.TieneMotor ? q.Ola.Ola.Pivotes.Count : 0).Append(',')
                 .Append(q.TieneMotor ? q.Marea.Ola.Pivotes.Count : 0).Append(']');
            }
            s.Append(']');
        }

        /// <summary>Una foto para la auditoria: dir, origen (hora y precio), extremo (hora y precio), delta de la pierna, con agresores,
        /// retroceso, faltan, raya, recorrido y cuantos pivotes confirmados.</summary>
        public static void FotoJson(StringBuilder s, string nombre, in FotoOla f, int pivotes)
        {
            s.Append('"').Append(nombre).Append("\":{\"x\":").Append(Fd(f.X)).Append(",\"n\":").Append(f.N).Append(",\"t\":\"").Append(Hu(f.T))
             .Append("\",\"ultimo\":").Append(Fd(f.Ultimo)).Append(",\"cum\":").Append(Fd(f.Cum)).Append(",\"dir\":").Append(f.Dir).Append(",\"pivotes\":").Append(pivotes);
            if (f.HayPierna)
                s.Append(",\"origen_t\":\"").Append(Hu(f.Origen.T)).Append("\",\"origen_precio\":").Append(Fd(f.Origen.Precio))
                 .Append(",\"extremo_t\":\"").Append(Hu(f.Extremo.T)).Append("\",\"extremo_precio\":").Append(Fd(f.Extremo.Precio))
                 .Append(",\"delta_pierna\":").Append(Fd(f.DeltaPierna)).Append(",\"con\":").Append(f.ConAgresores ? "true" : "false")
                 .Append(",\"retroceso\":").Append(Fd(f.Retroceso)).Append(",\"faltan\":").Append(Fd(f.Faltan)).Append(",\"raya\":").Append(Fd(f.Raya))
                 .Append(",\"recorrido\":").Append(Fd(f.Recorrido));
            s.Append('}');
        }
    }

    /// <summary>
    /// Lo que se escribe en ola-&lt;inst&gt;-&lt;tf&gt;-&lt;clave&gt;.csv (una copia hecha bajo la llave; Escribir no toca la carga). Filas, separadas por
    /// coma, con la primera columna de tipo:
    ///   M,clave,valor                      datos de la carga (X, costura, contadores...)
    ///   P,esc,k,t_extremo,precio,tipo,confirmado,t_conf,n_conf,cum       pivotes confirmados y, al final, el extremo en curso (confirmado 0)
    ///   F,esc,k,bar,corte,tomada,n,t_ultima,ultimo,cum,dir,pivotes,origen_t,origen_precio,origen_tipo,origen_cum,extremo_t,
    ///     extremo_precio,extremo_cum,delta_pierna,con_agresores,retroceso,faltan,raya,recorrido        la foto de cada vela k
    /// esc = X (la ola) o 4X (la marea). Horas UTC yyyy-MM-ddTHH:mm:ss.fff. tomada = 0: la foto todavia no se tomo (vela en curso o mercado
    /// quieto) y vale el estado de ahora. corte vacio: la vela en curso de un grafico que no es de tiempo.
    /// </summary>
    public sealed class InstantaneaOla
    {
        private static readonly CultureInfo I = CultureInfo.InvariantCulture;

        public string Inst = "", Marco = "", VersionIndicador = "";
        public int Clave, Usadas, PrimerBar, UltimoBar, BuferMax, CortesForzados, TomadasOla, TomadasMarea, CortesTardeOla, CortesTardeMarea;
        public string Estado = "", Motivo = "", FuenteX = "", Excluidas = "", CosturaTipo = "", PeriodoFuente = "";
        public decimal X, Mediana;
        public DateTime Desde, Hasta, InicioMotor, Costura, Generado, TUltima;
        public long NInicio, N, Version, HistRecibidas, HistFuera, HistUsadas, HistDescartadas, HistEnCostura, VivoNuevas, VivoDuplicadas, VivoRepetidas,
                    VivoDesordenadas, Correcciones, CorreccionesCostura, Viejas, ViejasBufer, CorreccionesBufer, HuerfanasBufer, Fuera, CortesCorridos, ActDuplicadas;
        public bool CosturaDudosa, SinSolape, RecibeVivo, HayMotor;
        public TimeSpan Periodo;
        public DateTime[] Cortes = Array.Empty<DateTime>();
        public FotoOla[] FotosOla = Array.Empty<FotoOla>(), FotosMarea = Array.Empty<FotoOla>();
        public PivoteOla[] PivOla = Array.Empty<PivoteOla>(), PivMarea = Array.Empty<PivoteOla>();
        public FotoOla ActualOla, ActualMarea;

        public static string H(DateTime t) => t == DateTime.MinValue || t == DateTime.MaxValue ? "" : t.ToString("yyyy-MM-ddTHH:mm:ss.fff", I);
        private static string F(decimal v) => v.ToString(I);
        private static string Txt(string s) => (s ?? "").Replace(',', ';').Replace('\n', ' ').Replace('\r', ' ');

        public void Escribir(TextWriter w)
        {
            w.Write("# ola del Superindicador " + Txt(VersionIndicador) + ": " + Txt(Inst) + " " + Txt(Marco) + " jornada " + Clave + ", generado " + H(Generado) + " UTC\n");
            void M(string k, string v) { w.Write("M,"); w.Write(k); w.Write(','); w.Write(Txt(v)); w.Write('\n'); }
            M("inst", Inst); M("marco", Marco); M("version", VersionIndicador); M("clave", Clave.ToString(I)); M("estado", Estado); M("motivo", Motivo);
            M("recibe_vivo", RecibeVivo ? "1" : "0"); M("hay_motor", HayMotor ? "1" : "0");
            M("x", F(X)); M("mediana", F(Mediana)); M("usadas", Usadas.ToString(I)); M("x_fuente", FuenteX); M("excluidas", Excluidas);
            M("desde", H(Desde)); M("hasta", H(Hasta)); M("inicio_motor", H(InicioMotor)); M("n_inicio", NInicio.ToString(I));
            M("costura", H(Costura)); M("costura_tipo", CosturaTipo); M("costura_dudosa", CosturaDudosa ? "1" : "0"); M("sin_solape", SinSolape ? "1" : "0");
            M("n", N.ToString(I)); M("t_ultima", H(TUltima)); M("version_motor", Version.ToString(I));
            M("periodo_s", Periodo.TotalSeconds.ToString(I)); M("periodo_fuente", PeriodoFuente); M("primer_bar", PrimerBar.ToString(I)); M("ultimo_bar", UltimoBar.ToString(I));
            M("cortes", Cortes.Length.ToString(I)); M("cortes_tarde_x", CortesTardeOla.ToString(I)); M("cortes_tarde_4x", CortesTardeMarea.ToString(I));
            M("cortes_forzados", CortesForzados.ToString(I)); M("cortes_corridos", CortesCorridos.ToString(I));
            M("tomadas_x", TomadasOla.ToString(I)); M("tomadas_4x", TomadasMarea.ToString(I));
            M("hist_recibidas", HistRecibidas.ToString(I)); M("hist_fuera", HistFuera.ToString(I)); M("hist_usadas", HistUsadas.ToString(I));
            M("hist_descartadas", HistDescartadas.ToString(I)); M("hist_en_costura", HistEnCostura.ToString(I));
            M("vivo_nuevas", VivoNuevas.ToString(I)); M("vivo_duplicadas", VivoDuplicadas.ToString(I)); M("vivo_repetidas", VivoRepetidas.ToString(I));
            M("vivo_desordenadas", VivoDesordenadas.ToString(I)); M("correcciones", Correcciones.ToString(I)); M("correcciones_costura", CorreccionesCostura.ToString(I));
            M("viejas", Viejas.ToString(I)); M("act_duplicadas", ActDuplicadas.ToString(I)); M("viejas_bufer", ViejasBufer.ToString(I)); M("correcciones_bufer", CorreccionesBufer.ToString(I));
            M("huerfanas_bufer", HuerfanasBufer.ToString(I)); M("fuera", Fuera.ToString(I)); M("bufer_max", BuferMax.ToString(I));
            w.Write("#P,esc,k,t_extremo,precio,tipo,confirmado,t_conf,n_conf,cum\n");
            foreach (var (esc, piv, act) in new[] { ("X", PivOla, ActualOla), ("4X", PivMarea, ActualMarea) })
            {
                for (int k = 0; k < piv.Length; k++) Pivote(w, esc, k, piv[k]);
                if (HayMotor && act.HayPierna) Pivote(w, esc, piv.Length, act.Extremo);   // el extremo en curso, sin confirmar
            }
            w.Write("#F,esc,k,bar,corte,tomada,n,t_ultima,ultimo,cum,dir,pivotes,origen_t,origen_precio,origen_tipo,origen_cum,extremo_t,extremo_precio,extremo_cum,delta_pierna,con_agresores,retroceso,faltan,raya,recorrido\n");
            foreach (var (esc, fotos, tomadas) in new[] { ("X", FotosOla, TomadasOla), ("4X", FotosMarea, TomadasMarea) })
                for (int k = 0; k < fotos.Length; k++) Foto(w, esc, k, PrimerBar + k, k < Cortes.Length ? Cortes[k] : DateTime.MinValue, k < tomadas, fotos[k]);
        }

        private static void Pivote(TextWriter w, string esc, int k, in PivoteOla p)
        {
            w.Write("P,"); w.Write(esc); w.Write(','); w.Write(k.ToString(I)); w.Write(','); w.Write(H(p.T)); w.Write(','); w.Write(F(p.Precio)); w.Write(',');
            w.Write(p.Tipo.ToString(I)); w.Write(','); w.Write(p.Confirmado ? "1" : "0"); w.Write(','); w.Write(H(p.TConf)); w.Write(',');
            w.Write(p.NConf.ToString(I)); w.Write(','); w.Write(F(p.Cum)); w.Write('\n');
        }

        private static void Foto(TextWriter w, string esc, int k, int bar, DateTime corte, bool tomada, in FotoOla f)
        {
            var sb = new System.Text.StringBuilder(256);
            sb.Append("F,").Append(esc).Append(',').Append(k).Append(',').Append(bar).Append(',').Append(H(corte)).Append(',').Append(tomada ? '1' : '0').Append(',')
              .Append(f.N).Append(',').Append(H(f.T)).Append(',').Append(f.N > 0 ? F(f.Ultimo) : "").Append(',').Append(F(f.Cum)).Append(',')
              .Append(f.Dir).Append(',').Append(f.Pivotes);
            if (f.HayPierna)
                sb.Append(',').Append(H(f.Origen.T)).Append(',').Append(F(f.Origen.Precio)).Append(',').Append(f.Origen.Tipo).Append(',').Append(F(f.Origen.Cum))
                  .Append(',').Append(H(f.Extremo.T)).Append(',').Append(F(f.Extremo.Precio)).Append(',').Append(F(f.Extremo.Cum))
                  .Append(',').Append(F(f.DeltaPierna)).Append(',').Append(f.ConAgresores ? 1 : 0).Append(',').Append(F(f.Retroceso))
                  .Append(',').Append(F(f.Faltan)).Append(',').Append(F(f.Raya)).Append(',').Append(F(f.Recorrido));
            else sb.Append(",,,,,,,,,,,,,");
            sb.Append('\n');
            w.Write(sb.ToString());
        }
    }
}
