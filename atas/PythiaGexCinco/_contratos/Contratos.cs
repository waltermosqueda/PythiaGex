// Contratos.cs — PythiaGex 4.1 (08-10-2026). Tipos e interfaces COMPARTIDOS por los constructores en paralelo (ver PLAN_PARALELO.md).
// Lo escribio el agente principal a partir de la especificacion de la vista previa (preview_niveles.py + backtest_familia.py), de tqqq_vivo.py
// y del mapa de la 3.0. NADIE lo cambia sin avisar en su informe: si un modulo necesita algo mas, lo agrega en SU carpeta y lo propone.
//
// Convenciones (todas obligatorias):
//  * Tiempo: DateTime en UTC (Kind=Utc al construir). Claves de minuto = ms UTC / 60000 (long) — en Python key = t_ns // 60e9.
//    Claves de vela m2 = floor(ms / 120000) * 120000 (ms de la APERTURA). Ticks de ATAS: Kind Unspecified = UTC -> usar Ticks crudos.
//  * Precios de nivel SIEMPRE en precio de ES/MES (el grafico). Strikes en la unidad de su libro.
//  * NaN = "no hay". Nunca 0 para "no hay".
//  * Nada de E/S en el hilo de dibujo. Los calculos corren en el timer/hilo de fondo y publican fotos INMUTABLES.
// 5.0 (27-10-2026): sin TQQQ. Fuera TqqqVela, ICalculoTqqq, IVersionSTqqq y el eje "TQQQ x3" de FotoFamilia (TqS/TqC/TqDatoUtc);
// IMotorFamilia.Configurar ya no recibe el calculo de TQQQ. Lo demas, igual que en la 4.1.
using System;
using System.Collections.Generic;

namespace PythiaGexCinco.Familia
{
    // ------------------------------------------------------------------ cadenas (ES por Rithmic, SPX/SPY por la descarga propia de CBOE)

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
        public string Libro;                 // "ES" (Rithmic, futuro) | "SPX" (CBOE _SPX) | "SPY"
        public DateTime GeneradoUtc;         // cuando la obtuvo el indicador (bajada) o el minuto de la foto (ES)
        public DateTime TsUtc;               // sello de la fuente (CBOE "timestamp"; ES = minuto de la foto)
        public DateTime DatoUtc;             // hora del DATO: CBOE = ultimo_trade NY -> UTC (si falta, Ts - 902 s); ES = Ts
        public double Spot = double.NaN;     // CBOE current_price (spot_idx); ES = precio del futuro de la foto
        public double[] Dias;                // dias a cada vencimiento, relativos a GeneradoUtc, round 4 (indice = FilaCadena.V)
        public FilaCadena[] Filas;           // ya filtradas como la fuente (CBOE: +-5 %, <= 14 dias; flaca <= 8 dias)
        public bool EsFuturo;                // true solo ES (Black-76 sin descuento); CBOE = Black-Scholes r = 0.0375
        public double OiTotal;               // suma oi_c + oi_p (para oi_fresco)
        public double BaseCruda = double.NaN, BaseErrorTicks = double.NaN;   // solo SPX: base por forwards (pythiagex/base.py medir)
        public double CierreAnterior = double.NaN;  // CBOE prev_day_close si viene en el json (verificacion; 5.0: ningun calculo lo usa, lo usaba la s de TQQQ)
        public double FuturoFoto = double.NaN;      // solo ES: precio del futuro en la foto (para el corrimiento corr)
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
        string Estado { get; }                                 // texto corto para la pestaña: "SPX hace 1 min · SPY sin internet (reintento 60 s)"
    }

    /// <summary>El libro de ES por Rithmic (del clon de la 3.0): una foto por minuto como viva3 (ConPuntas && IV > 0), con historia de la sesion.</summary>
    public interface ILibroEs
    {
        IReadOnlyList<FotoCadena> Fotos(DateTime desdeUtc, DateTime hastaUtc);   // ordenadas por TsUtc
        FotoCadena Ultima();
        long Version { get; }
    }

    /// <summary>Precio del MES en un instante (cinta del indicador + velas m2 armadas de los ticks; historia de dias previos de su archivo o del grafico).</summary>
    public interface ICinta
    {
        /// <summary>Spec 1.2: ultimo tick con t <= ts, ts - t <= 120 s y ts <= ultimo tick; si no, la vela m2 que contiene ts interpolada o + (c-o)(ts-b)/120 s; si no, NaN.</summary>
        double Precio(DateTime tUtc);
        /// <summary>Solo tick (C7, la 2.0 y la clasica; en la 4.1 tambien TQQQ): ultimo tick con t <= ts y ts - t <= maxEdadS; NaN si no.</summary>
        double PrecioSoloTick(DateTime tUtc, int maxEdadS = 120);
        /// <summary>Cierre de la ultima vela m2 CERRADA con b + 120 s <= t; NaN si t - (b + 120 s) > 4 h.</summary>
        double CierreConocido(DateTime tUtc);
        /// <summary>Velas m2 cerradas de la sesion (apertura ms, o, h, l, c).</summary>
        IReadOnlyList<(long Ms, double O, double H, double L, double C)> VelasM2(DateTime desdeUtc, DateTime hastaUtc);
        DateTime UltimoTickUtc { get; }
    }

    // ------------------------------------------------------------------ el libro de un minuto (clase BM de backtest_familia)

    /// <summary>Un libro convertido a precio de ES en un minuto: strikes con |Fut - fut| <= 3 % de fut, ordenados por Fut ascendente.</summary>
    public sealed class LibroMinuto
    {
        public string Libro;                  // ES | SPX | SPY
        public long Clave;                    // minuto (ms/60000)
        public double FutMnq;                 // fut del minuto = ICinta.CierreConocido(t)
        public double S;                      // spot en el eje del libro: ES fut-corr; SPX fut-base; SPY fut/razon
        public double Conv;                   // ES corr | SPX base | SPY razon
        public bool PorRazon;                 // true solo SPY (Fut = K * Conv); si no Fut = K + Conv
        public double[] Fut, K, GvC, GvP, GoC, GoP, Gv, Go;   // por strike (mismo largo)
        public double ZeroVol = double.NaN, ZeroOi = double.NaN; // zero estandar C5 ya en precio de ES
        public DateTime DatoUtc;              // edad: ES ts de la foto; CBOE t_dato
        public bool OiOk = true;              // ES: C2 (OI con fecha); CBOE: oi_fresco (solo marca, no apaga)
        public double Mult;                   // C1: ES 20, SPX 100, SPY 100
        public string ConvTexto;              // "base 241.62 (mediana ...)" para la pestaña
        public bool Congelada;                // CBOE: t_dato NY >= 15:59
        public double[] FutCarga, Carga, CargaC, CargaP, CargaK, CargaGex;   // 4.1.8: CargaK = strike CRUDO del libro
        // (no se recalcula deshaciendo la conversion sobre el precio ya redondeado: eso daba 30599,99 / 30600,01);
        // CargaGex = gamma de ese strike con LOS MISMOS vencimientos que los contratos.   // 4.1.7/4.1.8: strikes (eje MES) y OI de TODOS los vencimientos: total, calls y puts
    }

    /// <summary>Un modulo por libro (es / spx / spy): arma el LibroMinuto con SU foto vigente, SU conversion y SUS reglas de vigencia.</summary>
    public interface ILibroMinutero
    {
        string Libro { get; }
        /// <summary>Se llama en orden creciente de minuto. null si el libro no vale en ese minuto (sin foto vigente, sin conversion, fut NaN).</summary>
        LibroMinuto Minuto(long clave, DateTime tUtc, double futMnq);
    }

    // ------------------------------------------------------------------ niveles y fotos para la pantalla

    public readonly struct Nivel
    {
        public readonly double Precio;        // ES, round 2
        public readonly string Etq;           // "muro C", "muro P", "M+", "M-", "0G est.", "cruce arriba", "cruce abajo", "conf", "D1", "D2" (5.0: sin "dom"/"zero", eran de TQQQ)
        public readonly double GexM;          // M USD por 1 % (C1), round 1; NaN si no aplica
        public Nivel(double precio, string etq, double gexM) { Precio = precio; Etq = etq; GexM = gexM; }
    }

    /// <summary>Un nivel vigente "ahora" con su fuente y edad (protocolo: edad antes del numero si > 30 min).</summary>
    public sealed class NivelActual
    {
        public string Serie, Libro, Fuente, Tipo, Rol;   // Fuente "vol" | "oi" | "3.0"; Tipo MUROS|MAJORS|ZEST|ZTP|CONF|TRES|DOMS|TOPN (5.0: sin RAZON, era de TQQQ)
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
        public string Libro, Texto;           // Texto: "base <X> (...)" / "razon <X> (...)" (5.0: sin la linea "x3: ES = s x TQQQ + c")
        public double ConvValor = double.NaN; // SPX base | SPY razon | ES corr
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
        public string Instrumento = "";                                       // contrato de la cinta (control de vencimiento)
        public string Aviso = "";                                             // "sin Rithmic", "sin internet: CBOE de hace 2 h", etc.
        /// <summary>4.1.2: estado del calculo de cambios por libro, para la pestaña ("ES: vol 15 min 01:59-02:14Z; OI 10-09 01:48Z vs 10-08 01:30Z").</summary>
        public IReadOnlyList<string> CambiosEstado = Array.Empty<string>();
        /// <summary>4.1.2: copia superficial (para publicar una foto anotada sin tocar la del motor). Usar SIEMPRE esto en vez de copiar campo por campo.</summary>
        public FotoFamilia Copia() => (FotoFamilia)MemberwiseClone();

        /// <summary>4.1.8: REBOBINADO. clave de minuto (ms UTC / 60000) -> lo que regia en ESE minuto, o null si no hay.
        /// Es una clausura que el motor arma sobre instantaneas YA CONGELADAS (clones de los RegistroMinuto, nunca las
        /// referencias vivas: los registros publicados se mutan en el hilo del host al completar las series extra).
        /// La pantalla solo hace foto.Rebobinar?.Invoke(clave): toda la regla de fidelidad vive en el motor, y aca no
        /// entra ningun tipo del modulo fam (Contratos.cs se compila en cuatro proyectos).</summary>
        public Func<long, RebobinadoFam> Rebobinar;
    }

    /// <summary>4.1.8: lo que regia en un minuto del pasado. Mismos campos que lee la pantalla del vivo, para que el
    /// rebobinado no sea un camino de dibujo aparte: si un dato no se puede reconstruir se deja vacio, NUNCA se rellena
    /// con el valor de ahora (seria justo lo que hace sacar conclusiones erroneas).</summary>
    public sealed class RebobinadoFam
    {
        public long ClavePedida, ClaveUsada;
        public DateTime MinutoUtc;
        public double FutMnq = double.NaN;
        public IReadOnlyList<NivelActual> Niveles = Array.Empty<NivelActual>();
        public IReadOnlyList<FuenteEstado> Fuentes = Array.Empty<FuenteEstado>();
        public string[] Libros = Array.Empty<string>();
        /// <summary>Por que el minuto que se muestra no es el pedido (minuto sin libros, respaldo a clave-1), o "".</summary>
        public string Nota = "";
    }

    /// <summary>Catalogo de las series (id, grupo, color, banda) — el de la vista previa y lo agregado en la 4.1 (5.0: sin TQQQ). Lo llena el modulo fam (B3d).</summary>
    public sealed class SerieInfo
    {
        public string Id, Corto, Libro, Fuente, Tipo, Grupo, ColorHex, Tecnico, Criollo;
        public double Banda;
    }

    // 5.0 (27-10-2026): aca iba la seccion TQQQ (TqqqVela, ICalculoTqqq, IVersionSTqqq). Se saco entera con el modulo _modulos\tqqq.

    // ------------------------------------------------------------------ motor de la familia (B3d) y pantalla (B5)

    public interface IMotorFamilia
    {
        void Configurar(ICinta cinta, ILibroEs es, IFuenteCboe cboe, IReadOnlyList<ILibroMinutero> libros, string contrato);   // 5.0: sin el ICalculoTqqq
        /// <summary>Calcula los minutos nuevos (de a tandas, sin bloquear) y publica la foto. Llamado desde el timer de fondo.</summary>
        void Avanzar(DateTime ahoraUtc);
        FotoFamilia Foto { get; }
        IReadOnlyList<SerieInfo> Catalogo { get; }
    }
}
