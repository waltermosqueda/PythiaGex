using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PythiaLaOla;

// =========================================================================================================================
// 27-11-2026 (2.0a del Superindicador; desde La Ola 1.0 vive en atas/_test_pythiaola) EL FLUJO DE LA OLA DEL INDICADOR, simulado
// sobre cintas REALES con el MISMO codigo que corre en ATAS (atas/PythiaOla/OlaNucleo.cs: GestorOla, CargaOla, TrabajoOla). Lo que hace ATAS se imita asi:
//   - el grafico arranca con las velas de lo que ya paso (la ultima a medias) y las velas nuevas aparecen cuando llega su primera orden
//     (OnCalculate antes de la orden; en los escenarios "ticks" y "cambio", despues);
//   - cada orden en vivo llega como OnCumulativeTrade con su primera version y, si tuvo varios prints, crece con OnUpdateCumulativeTrade
//     (mismo objeto) hasta la orden completa de la cinta; en el bufer, a proposito, algunas crecen DESPUES de que empezo la siguiente;
//   - el latido pide la cinta (RequestForCumulativeTrades); ATAS contesta mas tarde con lo que tenia hasta ese momento (+ unos segundos:
//     el historial se solapa con el bufer) y el Task corre mientras siguen llegando ordenes y velas; despues se integra.
// Escribe los ola-*.csv con el mismo InstantaneaOla del indicador. Los compara herramientas/auditar_ola.py contra la referencia en
// Python (test_ola.py) con las mismas horas de corte. Ademas controla aca: X, cortes tarde, viejas, costura y la API (AlMomento, BarDeHora).
// Escenarios (MES 25-11 salvo "cambio", que cruza del 24 al 25-11):
//   vivo     el indicador arranca en el medio de un milisegundo con una orden creciendo (la costura), historial con solape, bufer con
//            ordenes que crecen fuera de orden, una vela nueva mientras corre el Task; csv a las 13:00 NY y al final
//   vacio    el historial llega antes que cualquier orden en vivo (bufer vacio): tope en la ultima vela; llegan repetidas en vivo
//   sincinta ATAS devuelve el historial vacio: solo en vivo desde el bufer (arranca en el medio de un milisegundo)
//   ticks    velas que no son de tiempo (~1500 ordenes, el corte es la apertura de la siguiente) y la vela llega despues de su orden
//   pasada   la jornada entera como pasada (la viva es la del 26-11, sin cinta), pedida por la vela del momento
//   cambio   la viva del 24-11 desde las 16:30 NY, el cambio de jornada a las 18:00 NY (por la orden, antes de la vela) y la viva del
//            25-11 con su propio pedido; csv de las dos
//   control  NEGATIVO: como vivo pero en vivo las ordenes crecen fuera de orden (viejas): el auditor TIENE que encontrar diferencias
// =========================================================================================================================
partial class Program
{
    struct Barra { public DateTime Ap, Fin; public decimal Hi, Lo; }

    static readonly TimeZoneInfo ZonaNy = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
    static DateTime NyDe(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ZonaNy);
    static readonly TimeSpan Rth0 = new TimeSpan(9, 30, 0), Rth1 = new TimeSpan(16, 0, 0);

    static void Sumar(ref Barra b, in OrdenCinta o)
    {
        decimal mn = o.Primero < o.Ultimo ? o.Primero : o.Ultimo, mx = o.Primero < o.Ultimo ? o.Ultimo : o.Primero;
        if (mx > b.Hi) b.Hi = mx;
        if (mn < b.Lo) b.Lo = mn;
        if (o.T > b.Fin) b.Fin = o.T;
    }

    static Barra Abrir(DateTime ap, in OrdenCinta o)
    {
        var b = new Barra { Ap = ap, Fin = o.T, Hi = decimal.MinValue, Lo = decimal.MaxValue };
        Sumar(ref b, o);
        return b;
    }

    /// <summary>Velas de 1 minuto (la apertura es el minuto de su primera orden). bdo: la vela de cada orden.</summary>
    static List<Barra> VelasMinuto(List<OrdenCinta> os, out int[] bdo, out int desordenadas)
    {
        var l = new List<Barra>(); bdo = new int[os.Count]; desordenadas = 0;
        long act = long.MinValue;
        for (int i = 0; i < os.Count; i++)
        {
            var o = os[i];
            if (i > 0 && o.T < os[i - 1].T) desordenadas++;
            long m = o.T.Ticks / TimeSpan.TicksPerMinute;
            if (m > act) { act = m; l.Add(Abrir(new DateTime(m * TimeSpan.TicksPerMinute), o)); }
            else { var b = l[l.Count - 1]; Sumar(ref b, o); l[l.Count - 1] = b; }
            bdo[i] = l.Count - 1;
        }
        return l;
    }

    /// <summary>Velas que no son de tiempo: una cada ~porVela ordenes, cortando donde cambia el milisegundo (la apertura es la hora de su
    /// primera orden).</summary>
    static List<Barra> VelasPorOrdenes(List<OrdenCinta> os, int porVela, out int[] bdo)
    {
        var l = new List<Barra>(); bdo = new int[os.Count];
        int n = 0;
        for (int i = 0; i < os.Count; i++)
        {
            var o = os[i];
            if (l.Count == 0 || (n >= porVela && o.T > os[i - 1].T)) { l.Add(Abrir(o.T, o)); n = 0; }
            else { var b = l[l.Count - 1]; Sumar(ref b, o); l[l.Count - 1] = b; }
            n++; bdo[i] = l.Count - 1;
        }
        return l;
    }

    /// <summary>Las velas RTH (9:30-16:00 NY) de 1 minuto de una cinta, sin guardar las ordenes (para la X de las ruedas previas).</summary>
    static List<Barra> VelasRthDeArchivo(string ruta)
    {
        var l = new List<Barra>();
        long act = long.MinValue; bool entra = false;
        foreach (var o in LeerCinta(ruta))
        {
            long m = o.T.Ticks / TimeSpan.TicksPerMinute;
            if (m > act)
            {
                act = m;
                var ap = new DateTime(m * TimeSpan.TicksPerMinute);
                var tny = NyDe(ap).TimeOfDay;
                entra = tny >= Rth0 && tny < Rth1;
                if (entra) l.Add(Abrir(ap, o));
            }
            else if (entra) { var b = l[l.Count - 1]; Sumar(ref b, o); l[l.Count - 1] = b; }
        }
        return l;
    }

    sealed class Escenario
    {
        public string Nombre;
        public string Inst = "MES";         // para el nombre del csv (La Ola 1.0: el modo r21 corre ES)
        public List<OrdenCinta> O;
        public List<Barra> Velas; public int[] Bdo;
        public bool Tiempo = true;
        public int Grafico;                 // el grafico ya vio las ordenes [0, Grafico)
        public int Desde, Hasta;            // el flujo en vivo: ordenes [Desde, Hasta)
        public HashSet<int> ParcialEnHistorial = new HashSet<int>();   // el historial las trae a medio crecer (version 1)
        public HashSet<int> SoloActualizaciones = new HashSet<int>();  // su primera version llego antes del arranque: en vivo solo crecen
        public DateTime RespuestaEn = DateTime.MinValue, IntegrarEn = DateTime.MinValue;   // primer pedido (MinValue: enseguida)
        public int RespDemora = 40, IntDemora = 25;                                        // pedidos siguientes, en ordenes
        public double SnapSeg = 4;          // el historial de la viva trae hasta la hora de la respuesta + esto
        public bool HistorialVacio;         // ATAS devuelve vacio el historial de la viva
        public bool IntercalarBufer = true, IntercalarVivo;
        public bool VelaDespues;            // la vela nueva llega despues de su primera orden
        public List<DateTime> Exportar = new List<DateTime>();
        public bool Negativo;               // control: tiene que dar diferencias
    }

    sealed class Simulador
    {
        readonly Escenario E;
        public readonly GestorOla G;
        DateTime _ahora;
        readonly int _base;
        int _ult = -1; Barra _vela;
        CargaOla _enVuelo; TrabajoOla _trab; int _respEn = int.MaxValue, _intEn = int.MaxValue; bool _primero = true;
        (int I, int K, object R)? _demorada;
        public readonly List<string> Log = new List<string>();
        public readonly List<string> Csvs = new List<string>();
        public int Pedidos, Respuestas, Integradas;
        readonly string _dir, _marco;

        public Simulador(Escenario e, List<Barra> previas, string dir)
        {
            E = e; _dir = dir; _marco = e.Tiempo ? "M1" : "T1500";
            G = new GestorOla(ZonaNy)
            {
                Tick = 0.25m, Declarado = e.Tiempo ? TimeSpan.FromMinutes(1) : TimeSpan.Zero, NoEsTiempo = !e.Tiempo, Reloj = () => _ahora
            };
            G.Anotar = s => Log.Add(s);
            int b = 0;
            foreach (var v in previas) G.Vela(b++, v.Ap, v.Fin, v.Hi, v.Lo);
            _base = b;
            if (e.Grafico > 0)
            {
                int ultima = e.Bdo[e.Grafico - 1];
                for (int k = 0; k < ultima; k++) VelaAlGrafico(k, e.Velas[k]);
                int i0 = e.Grafico - 1;
                while (i0 > 0 && e.Bdo[i0 - 1] == ultima) i0--;
                var p = Abrir(e.Velas[ultima].Ap, e.O[i0]);
                for (int i = i0 + 1; i < e.Grafico; i++) Sumar(ref p, e.O[i]);
                _vela = p; _ult = ultima; VelaAlGrafico(ultima, p);
                _ahora = e.O[e.Grafico - 1].T;
            }
        }

        public int Base => _base;
        void VelaAlGrafico(int k, in Barra v) => G.Vela(_base + k, v.Ap, v.Fin, v.Hi, v.Lo);

        public void Latido(int pos)
        {
            if (G.Viva == null) G.CrearViva();
            if (_enVuelo != null || _trab != null) return;
            var c = G.SiguientePedido();
            if (c == null) return;
            G.MarcarPedida(c, null); _enVuelo = c; Pedidos++;
            _respEn = pos + E.RespDemora; _intEn = _respEn + E.IntDemora;
            Log.Add("pedida la jornada " + c.Clave + (c.RecibeVivo ? " (viva)" : " (pasada)") + " en la posicion " + pos + " " + _ahora.ToString("HH:mm:ss.fff", Inv));
        }

        bool TocaResponder(int pos) => _enVuelo != null && (_primero ? _ahora >= E.RespuestaEn : pos >= _respEn);
        bool TocaIntegrar(int pos) => _trab != null && (_primero ? _ahora >= E.IntegrarEn : pos >= _intEn);

        public void Responder()
        {
            var c = _enVuelo; _enVuelo = null; Respuestas++;
            var t = G.Preparar(c);
            if (t == null) { Log.Add("respuesta descartada"); return; }
            t.Empezar();
            if (!(E.HistorialVacio && c.RecibeVivo))
            {
                DateTime hasta = c.RecibeVivo ? _ahora.AddSeconds(E.SnapSeg) : DateTime.MaxValue;
                for (int j = 0; j < E.O.Count; j++)
                {
                    var o = E.O[j];
                    if (o.T < c.Desde || o.T >= c.Hasta || o.T > hasta) continue;   // ATAS devuelve la jornada pedida, hasta ese momento
                    if (E.ParcialEnHistorial.Contains(j))
                    {
                        int kk = Math.Min(Math.Max(o.Prints, 1), 6);
                        Version(o, 1, kk, out decimal u, out decimal v);
                        t.Orden(new object(), new OrdenOla(o.T, o.Primero, u, v, o.Lado));
                    }
                    else t.Orden(new object(), new OrdenOla(o.T, o.Primero, o.Ultimo, o.Vol, o.Lado));
                }
            }
            t.Terminar();
            _trab = t;
            Log.Add("respuesta de la jornada " + c.Clave + " " + _ahora.ToString("HH:mm:ss.fff", Inv) + ": " + t.Recibidas + " ordenes, costura " + (t.ConBufer ? "bufer " : "tope ")
                    + (t.Tope == DateTime.MaxValue ? "-" : t.Tope.ToString("HH:mm:ss.fff", Inv)));
        }

        public void Integrar()
        {
            var r = G.Integrar(_trab); _trab = null; Integradas++;
            _primero = false;
            Log.Add(r != null ? "integrada " + r : "integracion descartada");
        }

        void CorregirDemorada((int I, int K, object R) d)
        {
            var o = E.O[d.I];
            for (int j = 2; j <= d.K; j++)
            {
                Version(o, j, d.K, out decimal u, out decimal v);
                G.Actualizada(d.R, new OrdenOla(o.T, o.Primero, u, v, o.Lado));
            }
        }

        void Entregar(int i, int pos)
        {
            var o = E.O[i];
            if (o.T > _ahora) _ahora = o.T;   // el reloj no vuelve atras (las repetidas del escenario "vacio" son viejas)
            int k = E.Bdo[i];
            bool nueva = k > _ult;
            if (nueva && !E.VelaDespues) VelaNueva(k, o);
            int kk = o.Prints <= 1 ? 1 : Math.Min(o.Prints, 6);
            object r = new object();
            bool soloAct = E.SoloActualizaciones.Contains(i);
            if (!soloAct)
            {
                Version(o, 1, kk, out decimal u1, out decimal v1);
                G.Nueva(r, new OrdenOla(o.T, o.Primero, u1, v1, o.Lado));
            }
            if (_demorada != null) { CorregirDemorada(_demorada.Value); _demorada = null; }   // crece la anterior DESPUES de que empezo esta
            if (kk > 1)
            {
                bool enBufer = G.Viva != null && G.Viva.Bufereando;
                bool intercalar = !soloAct && (enBufer ? E.IntercalarBufer && pos + 1 < _intEn && i + 1 < E.Hasta : E.IntercalarVivo);
                if (intercalar) _demorada = (i, kk, r);
                else CorregirDemorada((i, kk, r));
            }
            if (nueva && E.VelaDespues) VelaNueva(k, o);
            else if (!nueva && k == _ult) { Sumar(ref _vela, o); VelaAlGrafico(_ult, _vela); }
        }

        void VelaNueva(int k, in OrdenCinta o)
        {
            for (int q = _ult + 1; q < k; q++) VelaAlGrafico(q, E.Velas[q]);   // no deberia pasar: cada vela tiene ordenes
            _vela = Abrir(E.Velas[k].Ap, o); _ult = k; VelaAlGrafico(k, _vela);
        }

        public void Correr()
        {
            var exp = new Queue<DateTime>(E.Exportar.OrderBy(t => t));
            int pos = 0;
            for (int i = E.Desde; i < E.Hasta; i++, pos++)
            {
                if (E.O[i].T > _ahora) _ahora = E.O[i].T;
                if (pos % 16 == 0 || G.Viva == null || G.Viva.Estado == EstadoOla.EnCola) Latido(pos);
                if (TocaResponder(pos)) Responder();
                if (TocaIntegrar(pos)) Integrar();
                while (exp.Count > 0 && _ahora >= exp.Peek()) { Exportar(G.Viva, "-" + NyDe(exp.Peek()).ToString("HHmm", Inv)); exp.Dequeue(); }
                Entregar(i, pos);
            }
            if (_demorada != null) { CorregirDemorada(_demorada.Value); _demorada = null; }
            if (_enVuelo != null) Responder();
            if (_trab != null) Integrar();
        }

        public void Exportar(CargaOla c, string sufijo)
        {
            if (c == null) return;
            var s = G.Instantanea(c);
            s.Inst = E.Inst; s.Marco = _marco; s.VersionIndicador = "1.0 arnes " + E.Nombre;
            string ruta = Path.Combine(_dir, "ola-" + E.Inst + "-" + _marco + "-" + c.Clave + "-" + E.Nombre + sufijo + ".csv");
            using (var w = new StreamWriter(ruta, false, new UTF8Encoding(false), 1 << 16)) s.Escribir(w);
            Csvs.Add(ruta);
        }
    }

    // ------------------------------------------------------------------------------------------------------------- controles
    static int Controlar(Simulador s, CargaOla c, Escenario e, decimal xEsperada, List<string> difs)
    {
        int n0 = difs.Count;
        string et = e.Nombre + " " + c.Clave;
        if (xEsperada > 0 && c.X != xEsperada) difs.Add(et + ": X " + c.X + " (" + c.FuenteX + "), esperada " + xEsperada);
        if (c.CortesTarde != 0) difs.Add(et + ": cortes tarde " + c.CortesTarde);
        if (c.CortesForzados != 0) difs.Add(et + ": cortes forzados " + c.CortesForzados);
        if (s.G.CortesCorridos != 0) difs.Add(et + ": cortes corridos " + s.G.CortesCorridos);
        if (c.CosturaDudosa) difs.Add(et + ": costura dudosa");
        if (!e.Negativo && c.Viejas != 0) difs.Add(et + ": " + c.Viejas + " viejas");
        if (!c.TieneMotor) { difs.Add(et + ": sin motor (" + c.EstadoTexto() + ")"); return difs.Count - n0; }
        // la API: AlMomento de cada vela de la jornada contra la foto k, y BarDeHora de cada pivote
        s.G.BarrasDe(c.Clave, out int p, out int u);
        int malas = 0, malasHora = 0;
        for (int b = p; b <= u; b++)
        {
            bool ok = s.G.AlMomento(b, out var fo, out var fm, out var po, out var pm, out decimal x, out string est, out var cc);
            var ko = c.Ola.Foto(b - c.PrimerBar); var km = c.Marea.Foto(b - c.PrimerBar);
            if (!ok || !ReferenceEquals(cc, c) || x != c.X || fo.N != ko.N || fo.Cum != ko.Cum || fo.Dir != ko.Dir || fo.Pivotes != ko.Pivotes || fm.N != km.N
                || fm.Pivotes != km.Pivotes || po.Count != fo.Pivotes || pm.Count != fm.Pivotes) malas++;
            else
                for (int q = 0; q < po.Count; q++) if (!po[q].Equals(c.Ola.Ola.Pivotes[q])) { malas++; break; }
        }
        foreach (var piv in c.Ola.Ola.Pivotes.Concat(c.Marea.Ola.Pivotes))
        {
            int b = s.G.BarDeHora(piv.T);
            if (b < 0 || s.G.Apertura(b) > piv.T || (b + 1 < s.G.Velas && s.G.Apertura(b + 1) <= piv.T)) malasHora++;
        }
        if (malas > 0) difs.Add(et + ": AlMomento distinto de la foto en " + malas + " velas");
        if (malasHora > 0) difs.Add(et + ": BarDeHora mal en " + malasHora + " pivotes");
        // la seccion "ola" de la auditoria tiene que ser JSON valido (en la primera vela, en el medio, en la ultima y sin vela)
        foreach (int m in new[] { p, (p + u) / 2, u, -1 })
        {
            var sb = new StringBuilder("{\"utc\":\"x\",\"ola\":{\"on\":true,\"x_fija\":0");
            s.G.AuditoriaJson(sb, m);
            sb.Append(",\"pedido\":null}}");
            try
            {
                using var d = System.Text.Json.JsonDocument.Parse(sb.ToString());
                var o = d.RootElement.GetProperty("ola");
                if (m == u && o.GetProperty("hay").GetBoolean() && o.GetProperty("foto").GetProperty("ola").GetProperty("n").GetInt64() != c.Ola.Foto(u - c.PrimerBar).N)
                    difs.Add(et + ": la foto del JSON no es la de la vela " + m);
            }
            catch (Exception ex) { difs.Add(et + ": el JSON de la auditoria no es valido en la vela " + m + ": " + ex.Message); }
        }
        Console.WriteLine("  " + et.PadRight(22) + " X " + c.X.ToString(Inv) + " (" + c.FuenteX + ", mediana " + c.Mediana.ToString(Inv) + ") | " + c.EstadoTexto()
                          + " | costura " + c.CosturaTipo + (c.Costura > DateTime.MinValue ? " " + c.Costura.ToString("HH:mm:ss.fff", Inv) : "")
                          + (c.SinSolape ? " SIN SOLAPE" : "") + " | historial " + c.HistUsadas + " usadas, " + c.HistDescartadas + " despues, " + c.HistEnCostura + " en la costura"
                          + " | vivo " + c.VivoNuevas + " nuevas, " + c.Correcciones + " correcciones (" + c.CorreccionesCostura + " de la costura), "
                          + c.VivoDuplicadas + " repetidas, viejas " + c.Viejas + " | bufer max " + c.BuferMax + ", " + c.CorreccionesBufer + " crecieron adentro ("
                          + c.ViejasBufer + " fuera de orden), " + c.HuerfanasBufer + " sueltas | cortes " + c.Cortes + ", tarde " + c.CortesTarde
                          + " | pivotes " + c.Ola.Ola.Pivotes.Count + " / " + c.Marea.Ola.Pivotes.Count + " | API " + (malas + malasHora == 0 ? "ok" : "MAL"));
        return difs.Count - n0;
    }

    static int Buscar(List<OrdenCinta> os, DateTime t)
    {
        int lo = 0, hi = os.Count;
        while (lo < hi) { int m = (lo + hi) >> 1; if (os[m].T < t) lo = m + 1; else hi = m; }
        return lo;
    }

    static DateTime U(string s) => DateTime.ParseExact(s, "yyyy-MM-ddTHH:mm:ss", Inv);

    // ------------------------------------------------------------------------------------------------------------- flujo
    static int Flujo_(string[] a)
    {
        BajaPrioridad();
        if (a.Length < 6) throw new ArgumentException("uso: flujo <dir_salida> <cinta_vispera.csv> <cinta_jornada.csv> <x_esperada> <cinta_previa.csv> [...]");
        string dir = a[1];
        Directory.CreateDirectory(dir);
        decimal xEsp = decimal.Parse(a[4], Inv);
        var sw = Stopwatch.StartNew();
        var previas = new List<Barra>();
        for (int i = 5; i < a.Length; i++) previas.AddRange(VelasRthDeArchivo(a[i]));
        previas.Sort((p, q) => p.Ap.CompareTo(q.Ap));
        var t24 = LeerCinta(a[2]).ToList();
        var t25 = LeerCinta(a[3]).ToList();
        Console.WriteLine("flujo: " + (a.Length - 5) + " cintas previas (" + previas.Count + " velas RTH), vispera " + t24.Count + " ordenes, jornada " + t25.Count + " ordenes, lectura "
                          + sw.ElapsedMilliseconds + " ms");
        var v25 = VelasMinuto(t25, out int[] b25, out int des25);
        int ver24 = 0;
        var rth24 = new List<Barra>();
        foreach (var b in VelasMinuto(t24, out _, out ver24)) { var tny = NyDe(b.Ap).TimeOfDay; if (tny >= Rth0 && tny < Rth1) rth24.Add(b); }
        Console.WriteLine("  ordenes fuera de orden en las cintas: 24-11 " + ver24 + ", 25-11 " + des25);
        var previasMas24 = previas.Concat(rth24).ToList();
        var escs = new List<(Escenario E, List<Barra> Previas)>();

        // ---- vivo: arranca en el medio de un milisegundo, con la orden O1 creciendo (sus actualizaciones llegan sueltas)
        {
            int i1 = Buscar(t25, U("2026-11-25T14:59:40"));
            while (!(t25[i1].T == t25[i1 + 1].T && t25[i1].Prints >= 2 && t25[i1 - 1].T < t25[i1].T)) i1++;
            var e = new Escenario
            {
                Nombre = "vivo", O = t25, Velas = v25, Bdo = b25, Grafico = i1 + 1, Desde = i1, Hasta = t25.Count,
                RespuestaEn = U("2026-11-25T14:59:57"), IntegrarEn = U("2026-11-25T15:00:03"), Exportar = { U("2026-11-25T18:00:00") }
            };
            e.ParcialEnHistorial.Add(i1); e.SoloActualizaciones.Add(i1);
            escs.Add((e, previasMas24));
        }
        // ---- vacio: el historial llega antes que cualquier orden en vivo; despues llegan en vivo 3 que el historial ya traia
        {
            int i0 = Buscar(t25, U("2026-11-25T08:00:00"));
            while (!(t25[i0 - 1].Prints >= 2 && t25[i0 - 1].T < t25[i0].T && t25[i0 - 2].T == t25[i0 - 1].T)) i0++;
            var e = new Escenario
            {
                Nombre = "vacio", O = t25, Velas = v25, Bdo = b25, Grafico = i0, Desde = i0 - 3, Hasta = t25.Count, SnapSeg = 3
            };
            e.ParcialEnHistorial.Add(i0 - 1);
            escs.Add((e, previasMas24));
        }
        // ---- sincinta: historial vacio; arranca en el medio de un milisegundo
        {
            int i0 = Buscar(t25, U("2026-11-25T15:30:00"));
            while (!(t25[i0 - 1].T == t25[i0].T && t25[i0 - 2].T < t25[i0 - 1].T)) i0++;
            var e = new Escenario
            {
                Nombre = "sincinta", O = t25, Velas = v25, Bdo = b25, Grafico = i0, Desde = i0, Hasta = t25.Count, HistorialVacio = true,
                RespuestaEn = U("2026-11-25T15:30:20"), IntegrarEn = U("2026-11-25T15:30:22")
            };
            escs.Add((e, previasMas24));
        }
        // ---- ticks: velas de ~1500 ordenes, la vela despues de su primera orden
        {
            var vt = VelasPorOrdenes(t25, 1500, out int[] bt);
            int i1 = Buscar(t25, U("2026-11-25T16:10:00"));
            while (!(t25[i1].T == t25[i1 + 1].T && t25[i1].Prints >= 2 && t25[i1 - 1].T < t25[i1].T)) i1++;
            var e = new Escenario
            {
                Nombre = "ticks", O = t25, Velas = vt, Bdo = bt, Tiempo = false, Grafico = i1 + 1, Desde = i1, Hasta = t25.Count, VelaDespues = true,
                RespuestaEn = U("2026-11-25T16:10:15"), IntegrarEn = U("2026-11-25T16:10:30")
            };
            e.ParcialEnHistorial.Add(i1); e.SoloActualizaciones.Add(i1);
            escs.Add((e, previasMas24));
        }
        // ---- control NEGATIVO: como vivo, pero en vivo las ordenes crecen despues de que empezo la siguiente
        {
            var bas = escs[0].E;
            var e = new Escenario
            {
                Nombre = "control", O = t25, Velas = v25, Bdo = b25, Grafico = bas.Grafico, Desde = bas.Desde, Hasta = t25.Count,
                RespuestaEn = bas.RespuestaEn, IntegrarEn = bas.IntegrarEn, IntercalarVivo = true, Negativo = true
            };
            e.ParcialEnHistorial.UnionWith(bas.ParcialEnHistorial); e.SoloActualizaciones.UnionWith(bas.SoloActualizaciones);
            escs.Add((e, previasMas24));
        }

        int total = 0, negativos = 0;
        var difs = new List<string>();
        var csvs = new List<(string Csv, string Cinta, bool Negativo)>();
        foreach (var (e, prev) in escs)
        {
            var t0 = Stopwatch.StartNew();
            var s = new Simulador(e, prev, dir);
            s.Correr();
            var c = s.G.Viva;
            s.Exportar(c, "");
            int d = Controlar(s, c, e, xEsp, difs);
            if (e.Negativo) { negativos += c.Viejas > 0 ? 0 : 1; if (c.Viejas == 0) difs.Add("control: no hubo viejas"); }
            else total += d;
            foreach (var f in s.Csvs) csvs.Add((f, a[3], e.Negativo));
            Console.WriteLine("    " + e.Nombre + ": " + s.Pedidos + " pedido(s), " + (e.Hasta - e.Desde) + " ordenes en vivo, " + t0.ElapsedMilliseconds + " ms; " + string.Join(" | ", s.Log.Where(x => x.StartsWith("pedida") || x.StartsWith("respuesta"))));
        }

        // ---- pasada: la jornada entera pedida por la vela del momento; la viva es la del 26-11 (una vela) y su historial vuelve vacio
        {
            var e = new Escenario { Nombre = "pasada", O = t25, Velas = v25, Bdo = b25, Grafico = t25.Count, Desde = 0, Hasta = 0, HistorialVacio = true };
            var s = new Simulador(e, previasMas24, dir);
            var sig = new DateTime(2026, 11, 25, 23, 0, 0);
            s.G.Vela(s.Base + v25.Count, sig, sig.AddSeconds(1), 7200m, 7199m);   // la primera vela de la jornada del 26-11
            s.Latido(0); s.Responder(); s.Integrar();                              // la viva (26-11): sin cinta
            s.G.QuererMomento(s.Base + 700);                                       // el momento cae en el 25-11
            s.Latido(1); s.Responder(); s.Integrar();
            var c = s.G.Buscar(20261125);
            if (c == null) difs.Add("pasada: no se creo la carga del 25-11");
            else
            {
                s.Exportar(c, "");
                total += Controlar(s, c, e, xEsp, difs);
                if (c.Estado != EstadoOla.Completa) difs.Add("pasada: estado " + c.EstadoTexto());
                var v = s.G.Viva;
                if (v == null || v.Clave != 20261126 || v.Estado != EstadoOla.SinCinta) difs.Add("pasada: la viva del 26-11 tendria que estar sin cinta (" + (v?.EstadoTexto() ?? "null") + ")");
            }
            foreach (var f in s.Csvs) csvs.Add((f, a[3], false));
            Console.WriteLine("    pasada: " + string.Join(" | ", s.Log));
        }

        // ---- replay en modo velas: despues de la costura siguen apareciendo velas enteras pero no llega ninguna orden en vivo. Las velas
        //      con foto tomada siguen valiendo; las de despues tienen que dar false ("atrasada"), nunca la foto vieja como si fuera la suya
        {
            int i0 = Buscar(t25, U("2026-11-25T15:00:00"));
            var e = new Escenario { Nombre = "replay", O = t25, Velas = v25, Bdo = b25, Grafico = i0, Desde = i0, Hasta = i0 };
            var s = new Simulador(e, previasMas24, dir);
            s.Latido(0); s.Responder(); s.Integrar();
            int ult = b25[i0 - 1];
            for (int k = ult + 1; k <= ult + 30; k++) { var v = v25[k]; s.G.Vela(s.Base + k, v.Ap, v.Fin, v.Hi, v.Lo); }
            var c = s.G.Viva;
            bool antes = s.G.AlMomento(s.Base + ult - 5, out _, out _, out _, out _, out _, out string e1, out _);
            bool despues = s.G.AlMomento(s.Base + ult + 10, out _, out _, out _, out _, out _, out string e2, out _);
            bool ultima = s.G.AlMomento(s.Base + ult + 30, out _, out _, out _, out _, out _, out string e3, out _);
            if (!antes) difs.Add("replay: la vela anterior a la costura tendria que tener foto (" + e1 + ")");
            if (despues || !e2.StartsWith("atrasada")) difs.Add("replay: una vela posterior sin ordenes tendria que dar atrasada (" + e2 + ")");
            if (ultima || !e3.StartsWith("atrasada")) difs.Add("replay: la ultima vela tendria que dar atrasada (" + e3 + ")");
            if (c == null || c.Cortes != ult + 31 || c.PrimerBar != s.Base) difs.Add("replay: faltan cortes de las velas nuevas (" + c?.Cortes + ")");
            Console.WriteLine("    replay: antes de la costura " + (antes ? "con foto" : "SIN foto") + " | despues: " + e2);
        }

        // ---- cambio: viva del 24-11 desde las 16:30 NY, el cambio de jornada por la primera orden y la viva del 25-11
        {
            var o = t24.Concat(t25).ToList();
            var vo = VelasMinuto(o, out int[] bo, out _);
            int g0 = Buscar(o, U("2026-11-24T21:30:00"));
            var e = new Escenario
            {
                Nombre = "cambio", O = o, Velas = vo, Bdo = bo, Grafico = g0, Desde = g0, Hasta = Buscar(o, U("2026-11-25T01:00:00")), VelaDespues = true,
                RespuestaEn = U("2026-11-24T21:30:20"), IntegrarEn = U("2026-11-24T21:30:25")
            };
            var s = new Simulador(e, previas, dir);
            s.Correr();
            var viva = s.G.Viva; var pasada = s.G.Buscar(20261124);
            if (viva == null || viva.Clave != 20261125) difs.Add("cambio: la viva no paso al 25-11");
            if (pasada == null) difs.Add("cambio: el 24-11 no quedo como pasada");
            if (s.G.CambiosDeJornada != 1) difs.Add("cambio: " + s.G.CambiosDeJornada + " cambios de jornada");
            if (pasada != null) { s.Exportar(pasada, ""); total += Controlar(s, pasada, e, 0m, difs); csvs.Add((s.Csvs[s.Csvs.Count - 1], a[2], false)); }
            if (viva != null) { s.Exportar(viva, ""); total += Controlar(s, viva, e, xEsp, difs); csvs.Add((s.Csvs[s.Csvs.Count - 1], a[3], false)); }
            Console.WriteLine("    cambio: " + string.Join(" | ", s.Log.Where(x => !x.StartsWith("integrada"))));
        }

        using (var w = new StreamWriter(Path.Combine(dir, "flujo-csvs.txt"), false, new UTF8Encoding(false)))
            foreach (var (csv, cinta, neg) in csvs) w.Write(csv + "\t" + cinta + "\t" + (neg ? "negativo" : "positivo") + "\n");
        Console.WriteLine("flujo: " + csvs.Count + " csv en " + dir + " (lista en flujo-csvs.txt), " + sw.ElapsedMilliseconds + " ms");
        foreach (var x in difs.Take(40)) Console.WriteLine("  DIF " + x);
        Console.WriteLine("flujo: controles internos " + (difs.Count == 0 ? "OK" : difs.Count + " problema(s)"));
        return difs.Count == 0 ? 0 : 1;
    }
}
