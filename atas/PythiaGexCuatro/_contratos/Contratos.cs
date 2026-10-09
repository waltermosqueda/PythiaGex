// Contratos.cs — PythiaGex 4.1 (08-10-2026). Tipos e interfaces COMPARTIDOS por los constructores en paralelo (ver PLAN_PARALELO.md).
// Lo escribio el agente principal a partir de la especificacion de la vista previa (preview_niveles.py + backtest_familia.py), de tqqq_vivo.py
// y del mapa de la 3.0. NADIE lo cambia sin avisar en su informe: si un modulo necesita algo mas, lo agrega en SU carpeta y lo propone.
//
// Convenciones (todas obligatorias):
//  * Tiempo: DateTime en UTC (Kind=Utc al construir). Claves de minuto = ms UTC / 60000 (long) — en Python key = t_ns // 60e9.
//    Claves de vela m2 = floor(ms / 120000) * 120000 (ms de la APERTURA). Ticks de ATAS: Kind Unspecified = UTC -> usar Ticks crudos.
//  * Precios de nivel SIEMPRE en precio de NQ/MNQ (el grafico). Strikes en la unidad de su libro.
//  * NaN = "no hay". Nunca 0 para "no hay".
//  * Nada de E/S en el hilo de dibujo. Los calculos corren en el timer/hilo de fondo y publican fotos INMUTABLES.
using System;
using System.Collections.Generic;

namespace PythiaGexCuatro.Familia
{
    // ------------------------------------------------------------------ cadenas (NQ por Rithmic, NDX/QQQ/TQQQ por la descarga propia de CBOE)

    /// <summary>Una fila por (strike, vencimiento): [K, V, oi_c, oi_p, iv_c, iv_p, vol_c, vol_p] como pythiagex/cadena_atas.py y viva3.</summary>
    public readonly struct FilaCadena
    {
        public readonly double K; public readonly int V;
        public readonly double OiC, OiP, IvC, IvP, VolC, VolP;
        public FilaCadena(double k, int v, double oiC, double oiP, double ivC, double ivP, double volC, double volP)
        { K = k; V = v; OiC = oiC; OiP = oiP; IvC = ivC; IvP = ivP; VolC = volC; VolP = volP; }
    }

    /// <summary>Una foto de una cadena. Inmutable despues de construida.</summary>
    public sealed class FotoCadena
    {
        public string Libro;                 // "NQ" (Rithmic, futuro) | "NDX" (CBOE _NDX) | "QQQ" | "TQQQ"
        public DateTime GeneradoUtc;         // cuando la obtuvo el indicador (bajada) o el minuto de la foto (NQ)
        public DateTime TsUtc;               // sello de la fuente (CBOE "timestamp"; NQ = minuto de la foto)
        public DateTime DatoUtc;             // hora del DATO: CBOE = ultimo_trade NY -> UTC (si falta, Ts - 902 s); NQ = Ts
        public double Spot = double.NaN;     // CBOE current_price (spot_idx); NQ = precio del futuro de la foto
        public double[] Dias;                // dias a cada vencimiento, relativos a GeneradoUtc, round 4 (indice = FilaCadena.V)
        public FilaCadena[] Filas;           // ya filtradas como la fuente (CBOE: +-5 %, <= 14 dias; flaca <= 8 dias)
        public bool EsFuturo;                // true solo NQ (Black-76 sin descuento); CBOE = Black-Scholes r = 0.0375
        public double OiTotal;               // suma oi_c + oi_p (para oi_fresco)
        public double BaseCruda = double.NaN, BaseErrorTicks = double.NaN;   // solo NDX: base por forwards (pythiagex/base.py medir)
        public double CierreAnterior = double.NaN;  // CBOE prev_day_close si viene en el json (para TQQQ s y verificacion)
        public double FuturoFoto = double.NaN;      // solo NQ: precio del futuro en la foto (para el corrimiento corr)
    }

    /// <summary>La descarga propia de CBOE (modulo cboe). Un hilo de fondo, un ticker por vez, gzip, trozos de 256 KB con 50 ms de pausa.</summary>
    public interface IFuenteCboe
    {
        void Arrancar();                                       // idempotente; lee del disco propio el historico (6 dias) y arranca el hilo
        void Parar();                                          // OnDispose: cancela y espera <= 2 s
        /// <summary>Fotos de un libro con GeneradoUtc en [desde, hasta), ordenadas por GeneradoUtc, deduplicadas por TsUtc (gana la mas temprana).</summary>
        IReadOnlyList<FotoCadena> Fotos(string libro, DateTime desdeUtc, DateTime hastaUtc);
        FotoCadena Ultima(string libro);                       // null si no hay
        long Version { get; }                                  // sube con cada foto nueva (para saber si recalcular)
        string Estado { get; }                                 // texto corto para la pestaña: "QQQ hace 40 s · NDX hace 1 min · TQQQ sin internet (reintento 60 s)"
    }

    /// <summary>El libro de NQ por Rithmic (del clon de la 3.0): una foto por minuto como viva3 (ConPuntas && IV > 0), con historia de la sesion.</summary>
    public interface ILibroNq
    {
        IReadOnlyList<FotoCadena> Fotos(DateTime desdeUtc, DateTime hastaUtc);   // ordenadas por TsUtc
        FotoCadena Ultima();
        long Version { get; }
    }

    /// <summary>Precio del MNQ en un instante (cinta del indicador + velas m2 armadas de los ticks; historia de dias previos de su archivo o del grafico).</summary>
    public interface ICinta
    {
        /// <summary>Spec 1.2: ultimo tick con t <= ts, ts - t <= 120 s y ts <= ultimo tick; si no, la vela m2 que contiene ts interpolada o + (c-o)(ts-b)/120 s; si no, NaN.</summary>
        double Precio(DateTime tUtc);
        /// <summary>Solo tick (TQQQ): ultimo tick con t <= ts y ts - t <= maxEdadS; NaN si no.</summary>
        double PrecioSoloTick(DateTime tUtc, int maxEdadS = 120);
        /// <summary>Cierre de la ultima vela m2 CERRADA con b + 120 s <= t; NaN si t - (b + 120 s) > 4 h.</summary>
        double CierreConocido(DateTime tUtc);
        /// <summary>Velas m2 cerradas de la sesion (apertura ms, o, h, l, c).</summary>
        IReadOnlyList<(long Ms, double O, double H, double L, double C)> VelasM2(DateTime desdeUtc, DateTime hastaUtc);
        DateTime UltimoTickUtc { get; }
    }

    // ------------------------------------------------------------------ el libro de un minuto (clase BM de backtest_familia)

    /// <summary>Un libro convertido a precio de NQ en un minuto: strikes con |Fut - fut| <= 3 % de fut, ordenados por Fut ascendente.</summary>
    public sealed class LibroMinuto
    {
        public string Libro;                  // NQ | NDX | QQQ
        public long Clave;                    // minuto (ms/60000)
        public double FutMnq;                 // fut del minuto = ICinta.CierreConocido(t)
        public double S;                      // spot en el eje del libro: NQ fut-corr; NDX fut-base; QQQ fut/razon
        public double Conv;                   // NQ corr | NDX base | QQQ razon
        public bool PorRazon;                 // true solo QQQ (Fut = K * Conv); si no Fut = K + Conv
        public double[] Fut, K, GvC, GvP, GoC, GoP, Gv, Go;   // por strike (mismo largo)
        public double ZeroVol = double.NaN, ZeroOi = double.NaN; // zero estandar C5 ya en precio de NQ
        public DateTime DatoUtc;              // edad: NQ ts de la foto; CBOE t_dato
        public bool OiOk = true;              // NQ: C2 (OI con fecha); CBOE: oi_fresco (solo marca, no apaga)
        public double Mult;                   // C1: NQ 20, NDX 100, QQQ 100
        public string ConvTexto;              // "base 241.62 (mediana ...)" para la pestaña
        public bool Congelada;                // CBOE: t_dato NY >= 15:59
    }

    /// <summary>Un modulo por libro (nq / ndx / qqq): arma el LibroMinuto con SU foto vigente, SU conversion y SUS reglas de vigencia.</summary>
    public interface ILibroMinutero
    {
        string Libro { get; }
        /// <summary>Se llama en orden creciente de minuto. null si el libro no vale en ese minuto (sin foto vigente, sin conversion, fut NaN).</summary>
        LibroMinuto Minuto(long clave, DateTime tUtc, double futMnq);
    }

    // ------------------------------------------------------------------ niveles y fotos para la pantalla

    public readonly struct Nivel
    {
        public readonly double Precio;        // NQ, round 2
        public readonly string Etq;           // "muro C", "muro P", "M+", "M-", "0G est.", "cruce arriba", "cruce abajo", "conf", "D1", "D2", "dom", "zero"
        public readonly double GexM;          // M USD por 1 % (C1), round 1; NaN si no aplica
        public Nivel(double precio, string etq, double gexM) { Precio = precio; Etq = etq; GexM = gexM; }
    }

    /// <summary>Un nivel vigente "ahora" con su fuente y edad (protocolo: edad antes del numero si > 30 min).</summary>
    public sealed class NivelActual
    {
        public string Serie, Libro, Fuente, Tipo, Rol;   // Fuente "vol" | "oi" | "3.0"; Tipo MUROS|MAJORS|ZEST|ZTP|CONF|TRES|DOMS|RAZON
        public double Precio, Strike = double.NaN, GexM = double.NaN, Banda;
        public DateTime DatoUtc;
        public bool OiViejo;

        // ---- 4.1.2 (08-10-2026): cambios por nivel. El MOTOR los deja vacios (paridad intacta); los llena CambiosFamilia en el host, sobre una
        //      COPIA del nivel (nunca se modifica el objeto que publico el motor). Mismas unidades y signo que GexM (M USD por 1 %, calls +, puts -).
        /// <summary>Solo series por VOLUMEN: volumen operado en la ventana en el MISMO strike (y lado: muro C = calls, muro P = puts, M+/M- = neto),
        /// valuado con la gamma de AHORA (el efecto del precio, el tiempo y la IV queda afuera). Indice = CambiosVentanas.Min. NaN = sin dato.
        /// No dice si abren o cierran: cerrar una posicion tambien suma volumen.</summary>
        public double[] CambioVolM;
        /// <summary>Segundos reales entre las dos fotos usadas para cada ventana (NaN si no hay). Mismo indice que CambioVolM.</summary>
        public double[] CambioVolSegReal;
        /// <summary>Hora (UTC) de la foto de "antes" y de "ahora" de cada ventana (MinValue si no hay). Mismo indice que CambioVolM.</summary>
        public DateTime[] CambioVolDesdeUtc, CambioVolHastaUtc;
        /// <summary>Solo series por OI: OI vigente menos el OI de la publicacion anterior en el MISMO strike y lado, valuado con la gamma de AHORA.
        /// Es cambio de POSICIONES (lo que la OCC consolido entre dos noches), no actividad de hoy. NaN = sin dato.</summary>
        public double CambioOiDiaM = double.NaN;
        /// <summary>Desde cuando rige el OI de "antes" (R1) y el de "ahora" (R0): la hora de la foto en que se vio el salto de cada uno.</summary>
        public DateTime CambioOiDesdeUtc, CambioOiHastaUtc;
        /// <summary>0..1: parte del monto actual del strike que tiene dato en las dos fotos comparadas (ventana elegida o dia). NaN = no aplica.</summary>
        public double[] CambioCobertura;   // indice: CambiosVentanas.Min y, en la ultima posicion, el cambio de OI del dia
        /// <summary>Por que no hay cambio o con que salvedad ("cadena congelada", "volumen reiniciado", "OI sin la sesion anterior en memoria"...).</summary>
        public string CambioNota = "";

        /// <summary>Copia superficial (los arreglos se comparten: quien anota crea arreglos nuevos).</summary>
        public NivelActual Copia() => (NivelActual)MemberwiseClone();
    }

    /// <summary>4.1.2: ventanas del cambio por volumen, en minutos. La pantalla elige una por grafico (Cambio41Ventana); el host calcula las tres.</summary>
    public static class CambiosVentanas
    {
        public static readonly int[] Min = { 5, 15, 30 };
        public static int Indice(int minutos) { for (int i = 0; i < Min.Length; i++) if (Min[i] == minutos) return i; return 1; }
        /// <summary>Posicion del cambio de OI del dia en CambioCobertura.</summary>
        public static int IndiceOiDia => Min.Length;
    }

    public sealed class FuenteEstado
    {
        public string Libro, Texto;           // Texto: "base 241.62 (...)" / "razon 41.4331 (...)" / "x3: NQ = 124.21 x TQQQ + 21012.1"
        public double ConvValor = double.NaN; // NDX base | QQQ razon | NQ corr
        public DateTime DatoUtc;
        public bool Congelada, OiOk = true;
    }

    /// <summary>Lo que dibuja la pantalla. INMUTABLE: el motor arma una nueva y la publica con Volatile.Write.</summary>
    public sealed class FotoFamilia
    {
        public string Sesion = "";                                             // yyyy-MM-dd (sesion de 22:00 UTC a 21:00 UTC)
        public DateTime CalculadoUtc;
        public IReadOnlyDictionary<long, IReadOnlyDictionary<string, double[]>> HistoriaM2;  // apertura m2 (ms) -> serie -> precios
        public IReadOnlyList<NivelActual> Actuales;
        public IReadOnlyList<FuenteEstado> Fuentes;
        public double TqS = double.NaN, TqC = double.NaN; public DateTime TqDatoUtc;      // eje TQQQ x3
        public string Instrumento = "";                                       // contrato de la cinta (control de vencimiento)
        public string Aviso = "";                                             // "sin Rithmic", "sin internet: CBOE de hace 2 h", etc.
        /// <summary>4.1.2: estado del calculo de cambios por libro, para la pestaña ("NQ: vol 15 min 01:59-02:14Z; OI 10-09 01:48Z vs 10-08 01:30Z").</summary>
        public IReadOnlyList<string> CambiosEstado = Array.Empty<string>();
        /// <summary>4.1.2: copia superficial (para publicar una foto anotada sin tocar la del motor). Usar SIEMPRE esto en vez de copiar campo por campo.</summary>
        public FotoFamilia Copia() => (FotoFamilia)MemberwiseClone();
    }

    /// <summary>Catalogo de las 29 series (id, grupo, color, banda) — el mismo de la vista previa + TQQQ. Lo llena el modulo fam (B3d).</summary>
    public sealed class SerieInfo
    {
        public string Id, Corto, Libro, Fuente, Tipo, Grupo, ColorHex, Tecnico, Criollo;
        public double Banda;
    }

    // ------------------------------------------------------------------ TQQQ

    public sealed class TqqqVela
    {
        public long AperturaM2Ms;
        public double S = double.NaN, C = double.NaN;          // NQ = S*K + C
        public Dictionary<string, Nivel[]> Series = new Dictionary<string, Nivel[]>();   // T_DOMS_vol, T_MUROS_vol, T_MUROS_oi, T_ZERO_oi, T_DOMS_raz
        public Dictionary<string, double[]> Strikes = new Dictionary<string, double[]>();
        public DateTime DatoUtc; public bool Congelada;
    }

    public interface ICalculoTqqq
    {
        /// <summary>Niveles de TQQQ para la vela m2 que abre en aperturaMs (foto = ultima con Generado <= apertura + 120 s, vida 1500 s). null si no hay.</summary>
        TqqqVela Vela(long aperturaM2Ms, ICinta cinta, IFuenteCboe cboe);
    }

    /// <summary>4.1.0 (integracion, OPCIONAL): un ICalculoTqqq cuya s del dia puede cambiar dentro de la sesion (s de respaldo por recta libre,
    /// o la s que aparece cuando llegan los cierres) sube VersionS cada vez que cambia; el motor rehace entonces TODAS las velas de la sesion
    /// con la s nueva (antes quedaban velas viejas con la s anterior hasta la pasada de 10 min).</summary>
    public interface IVersionSTqqq
    {
        long VersionS { get; }
    }

    // ------------------------------------------------------------------ motor de la familia (B3d) y pantalla (B5)

    public interface IMotorFamilia
    {
        void Configurar(ICinta cinta, ILibroNq nq, IFuenteCboe cboe, IReadOnlyList<ILibroMinutero> libros, ICalculoTqqq tqqq, string contrato);
        /// <summary>Calcula los minutos nuevos (de a tandas, sin bloquear) y publica la foto. Llamado desde el timer de fondo.</summary>
        void Avanzar(DateTime ahoraUtc);
        FotoFamilia Foto { get; }
        IReadOnlyList<SerieInfo> Catalogo { get; }
    }
}
