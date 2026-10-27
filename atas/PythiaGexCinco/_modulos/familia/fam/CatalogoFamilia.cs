// CatalogoFamilia.cs — PythiaGex 5.0 (Gamma ES), clon de la 4.1 (modulo fam B3d, 08-10-2026).
// Las 29 series: las 24 de laboratorio/tres/auditoria_0810/preview_niveles.py (SERIES, en el MISMO orden) + las 5 de
// laboratorio/tres/tqqq/tqqq_vivo.py. Textos copiados literal (tecnico, criollo, estado). Roles por etiqueta = preview_niveles.ROLES.
// 4.1.3 (09-10-2026): + 4 series "como la 2.0" AL FINAL (R20_SPY_vol, R20_SPX_vol, DOMS_SPY_vol, DOMS_SPX_vol; ver
// _modulos/familia/replica20/Replica20.cs). NO entran en Calc (las 21 de la vista previa siguen iguales, CONF y FAM no las ven): van en
// Extra, se calculan aparte (OpcionesMotorFamilia.Extras) y se guardan en RegistroMinuto.Extra.
// 4.1.4 (09-10-2026): PrendidasPorDefecto sin MAJORS_SPY_oi, MUROS_SPY_oi, FAM_MUROS_oi ni DOMS_SPY_vol (pedido del operador); color de R20_SPY_vol
// #cc8800 (el #e8c83c no se distinguia del SPY 0G #fcd34d). Las series, su orden y su cuenta no cambian.
// 4.1.5 (09-10-2026): + R10_SPX_zero "Clasica SPX 0Γ" AL FINAL de Extra (la estela 'SPX 0Γ' de la clasica: su zero de SPX por volumen con SU base de
// la rueda anterior; _modulos/familia/clasica/ClasicaSpx.cs), PRENDIDA por defecto (pedido del operador: "copiarla ahora para nuestro 4.0").
// Color #ffffff (los puntitos de la clasica son #EBEBEB; el blanco queda a CIEDE2000 12,0 del ES 0G #c9d4e3, el mas cercano del catalogo).
// 4.1.5b (09-10-2026): + R10_SPX_dom "Clasica SPX" (las D1-D3 de la capa SPX de la clasica), PRENDIDA. 35 series; 8 prendidas por defecto.
// 4.1.5d (09-10-2026, revision 4.1.5b/c): color de R10_SPX_dom #b8f8d8 (menta palido): el #9dffd2 de la 4.1.5b quedaba a CIEDE2000 11,4 del "2.0 SPX"
// #3ddc97 (criterio de la 4.1.4: >= 12); ahora 14,3 del 2.0 SPX, 14,7 del SPX major V y 21,2 del blanco del 0Γ. Descripciones de las dos R10 con la
// salvedad medida el 09-10 (si la clasica pierde Rithmic re-mide su base en la rueda y la replica deja de coincidir: ~13 pts de 16:48 a 17:11 ART).
// 5.0 (27-10-2026): sin TQQQ. Fuera las 6 series T_* (estaban DESPUES de las 24 de la vista previa: Calc y Extra, que son indices de
// RegistroMinuto.Series/Extra, no cambian), SeriesTqqq, los presets "TQQQ x3" y "TQQQ vs Gammalito" y T_MUROS_oi de PrendidasPorDefecto.
// Quedan 35 series: 24 de la vista previa + 4 como la 2.0 + 2 de la clasica + 2 TOPN (4.1.7) + 3 CARGA (4.1.8).
// 5.0 (27-10-2026, P5): TEXTOS DE ES. Tecnico (nombre de los tableros, en ingles), criollo (que hace) y estado reescritos para ES/SPX/SPY:
//   * el ESTADO de TODAS dice "SIN MEDIR EN ES"; lo que la 4.1 midio en NQ va entre parentesis como "en NQ, otro producto: ..." (los porcentajes
//     de rebote, Holm y "candidata principal" eran de NQ/NDX/QQQ: el clon solo cambio los nombres de los tickers en el texto);
//   * conversion explicada: SPX -> ES sumando la base ES - SPX sincronizada; SPY -> ES multiplicando por la razon ES/SPY sincronizada
//     (MEDIDO en Z6, MEDICIONES_ES.md §3.7: 10,08-10,13; un strike de SPY = 1 USD ~ 10,08 pts de ES, §2);
//   * BANDA de SPY +-5 (era +-20): en NQ un strike de QQQ son ~41 pts de NQ y la banda era medio strike; un strike de SPY son ~10,08 pts de ES
//     (MEDICIONES_ES.md §2): medio strike = +-5 (confirmado por el coordinador 27-10). Solo cambia el texto "±5" y el redondeo del precio de la
//     etiqueta (ArmadoPantalla): no cambia ninguna cuenta;
//   * las replicas de la 2.0 y de la clasica en ES: en un grafico de ES ninguna de las dos arma capas, usan UN libro, SPX, con radio
//     min(2 %, 100) = 100 pts y retraso 960 s (MEDICIONES_ES.md §6). R20_SPX_vol = el libro SPX de la 2.0 en ES; R10_* = el libro SPX de la
//     clasica en ES; R20_SPY_vol NO tiene equivalente (la 2.0 no dibuja SPY en ES): es una seleccion estilo 2.0 sobre SPY, APAGADA por defecto;
//   * los radios y las distancias de la seleccion dichos en el texto (los fija P1 en comun/Seleccion.cs y fam/ReglasFam.cs; aca solo se nombran,
//     RADIO_ES_TXT/RADIO_CBOE_TXT): ES 25 pts, SPX y SPY 20, familia (FAM y CONF) 20; confluencia a <= 3 pts (ReglasFam.CONF_PTS) y baldes de la
//     familia de 5 pts (ReglasFam.FAM_GRILLA), atados al paso de strike de ES y SPX (5, como NQ); zero estandar en grilla de 0,5 pts, a <= 75;
//   * PrendidasPorDefecto = las 6 del coordinador (el analogo de las que eligio el operador en NQ, sin R20_SPY_vol);
//   * presets sin "Recomendadas" (era la recomendacion del verificador en NQ); "2.0/Clasica contra 5.0".
// Ids, orden, colores, Calc y Extra NO cambian (verificado volcando el catalogo antes y despues, _diseno/P5_PANTALLA.md).
// Sin referencias a ATAS.
using System;
using System.Collections.Generic;
using System.Linq;

namespace PythiaGexCinco.Familia
{
    public static class CatalogoFamilia
    {
        /// <summary>Las 35 series: las 24 de la vista previa en su orden, (4.1.3) las 4 "como la 2.0", (4.1.5/4.1.5b) las 2 de la clasica y (4.1.7/4.1.8)
        /// las 2 TOPN y las 3 CARGA. 5.0: sin las de TQQQ (iban despues de las 24).</summary>
        public static readonly IReadOnlyList<SerieInfo> Series;
        /// <summary>id -> serie.</summary>
        public static readonly IReadOnlyDictionary<string, SerieInfo> PorId;
        /// <summary>id -> texto 'estado'. 5.0 (P5): TODAS empiezan con "SIN MEDIR EN ES" (lo medido en NQ va entre parentesis, como dato de otro
        /// producto). SerieInfo no tiene ese campo: va aparte.</summary>
        public static readonly IReadOnlyDictionary<string, string> Estado;
        /// <summary>preview_niveles.CALC: las 21 series que calcula la cuenta (las 24 de la vista previa menos TRES; en la 4.1 tampoco entraba TQQQ)
        /// + (4.1.7/4.1.8) TOPN y CARGA al final, en orden. El indice es el de RegistroMinuto.Series.</summary>
        public static readonly string[] Calc;
        /// <summary>id -> indice en Calc (o -1).</summary>
        public static int IndiceCalc(string id) => _idxCalc.TryGetValue(id ?? "", out var i) ? i : -1;
        private static readonly Dictionary<string, int> _idxCalc;

        public static readonly string[] Libros = { "ES", "SPX", "SPY" };       // backtest_familia.LIBROS (orden de bk)
        public static readonly string[] CapasTres = { "ES", "SPX", "SPY" };
        // 5.0 (27-10-2026): aca iba SeriesTqqq (T_DOMS_vol, T_MUROS_vol, T_MUROS_oi, T_ZERO_oi, T_DOMS_raz, T_ZERO_vol_pm).
        /// <summary>4.1.3: las series "como la 2.0" (fuera de Calc) y (4.1.5) la de la clasica, AL FINAL (un archivo de la 4.1.4 se lee igual). El indice
        /// es el de RegistroMinuto.Extra / ExtraStrikes.</summary>
        public static readonly string[] Extra = { "R20_SPY_vol", "R20_SPX_vol", "DOMS_SPY_vol", "DOMS_SPX_vol", "R10_SPX_zero", "R10_SPX_dom" };   // 4.1.5b: + R10_SPX_dom al final
        /// <summary>id -> indice en Extra (o -1).</summary>
        public static int IndiceExtra(string id) { for (int i = 0; i < Extra.Length; i++) if (Extra[i] == id) return i; return -1; }
        /// <summary>4.1.3: el MetaLibro de donde sale la edad y la conversion de una serie extra: las replicas de la 2.0 tienen el suyo
        /// (RegistroMinuto.MetaExtra, "2.0 SPY" / "2.0 SPX"); las DOMS_* usan el de su libro de la 5.0 (RegistroMinuto.Meta). null = el del libro.
        /// 4.1.5: la replica de la clasica, "Clasica SPX" (su base y su origen).</summary>
        public static string MetaExtraDe(string id) => id == "R20_SPY_vol" ? "2.0 SPY" : id == "R20_SPX_vol" ? "2.0 SPX" : (id == "R10_SPX_zero" || id == "R10_SPX_dom") ? "Clasica SPX" : null;
        /// <summary>Default de las casillas (mismo orden que SeriesPantalla.PrendidasPorDefecto). Historia en NQ: las 8 que eligio el operador (captura del
        /// 08-10 17:5x), + 4.1.3 las dominantes de la 2.0, - 4.1.4 SPY/QQQ majors OI, muros OI, FAM muros OI y dom ("suman ruido"), + 4.1.5/4.1.5b la clasica.
        /// 5.0 (27-10-2026, P5, decision del coordinador): el analogo en ES de las que eligio el operador en NQ (MUROS_NQ_oi, MUROS_NDX_vol, TRES_NDX,
        /// R20_NDX_vol, R10_NDX_zero, R10_NDX_dom) SIN R20_SPY_vol (el analogo de R20_QQQ_vol): en un grafico de ES la 2.0 no dibuja SPY, su unico libro es
        /// SPX (MEDICIONES_ES.md §6). Quedan 6. Ninguna esta medida en ES: son las que se ven al poner la 5.0, nada mas.</summary>
        public static readonly string[] PrendidasPorDefecto = { "MUROS_ES_oi", "MUROS_SPX_vol", "TRES_SPX",
                                                                "R20_SPX_vol", "R10_SPX_zero", "R10_SPX_dom" };   // 5.0 (P5): sin R20_SPY_vol
        /// <summary>preview_niveles.PRESETS (5.0: sin los de tqqq_vivo). 5.0 (P5): sin "Recomendadas" (era la recomendacion del verificador en NQ: en ES no
        /// hay nada medido); "Por defecto" = las prendidas de fabrica; "2.0/Clasica contra 5.0" (antes "contra 4.1"). No los usa el codigo de la DLL.</summary>
        public static readonly IReadOnlyDictionary<string, string[]> Presets = new Dictionary<string, string[]>
        {
            ["Por defecto"] = PrendidasPorDefecto,
            ["Familia vol"] = new[] { "MUROS_ES_vol", "MUROS_SPX_vol", "MUROS_SPY_vol", "FAM_MUROS_vol", "TRES_ES" },
            ["Familia OI"] = new[] { "MUROS_ES_oi", "MUROS_SPX_oi", "MUROS_SPY_oi", "MAJORS_SPY_oi", "FAM_MUROS_oi", "TRES_ES" },
            ["Zero y cruces"] = new[] { "ZEST_ES_vol", "ZEST_SPX_vol", "ZEST_SPY_vol", "ZTP_ES_vol", "TRES_ES" },
            ["3.0 actual"] = new[] { "TRES_ES", "TRES_SPX", "TRES_SPY" },
            ["2.0 contra 5.0"] = new[] { "R20_SPX_vol", "DOMS_SPX_vol", "R20_SPY_vol", "DOMS_SPY_vol" },
            ["Clasica contra 5.0"] = new[] { "R10_SPX_zero", "R10_SPX_dom", "ZEST_SPX_vol", "ZTP_SPX_vol" },
        };

        /// <summary>preview_niveles.ROLES: lo que significa D1/D2/Z segun el tipo. CONF -> "conf"; sin rol -> la etiqueta.</summary>
        public static string Rol(string tipo, string etq)
        {
            switch (tipo)
            {
                case "MUROS": return etq == "D1" ? "muro C" : etq == "D2" ? "muro P" : etq;
                case "MAJORS": return etq == "D1" ? "M+" : etq == "D2" ? "M-" : etq;
                case "UNO": return etq == "D1" ? "techo" : etq == "D2" ? "piso" : etq;
                case "ZTP": return etq == "D1" ? "cruce arriba" : etq == "D2" ? "cruce abajo" : etq;
                case "ZEST": return etq == "Z" ? "0G est." : etq;
                case "TRES": return etq;
                case "CONF": return "conf";
                default: return etq;
            }
        }

        /// <summary>5.0 (P5): el estado de una serie en ES. Nada esta medido en ES todavia (regla del operador: lo que se midio en NQ NO se presenta
        /// como medido en ES); lo que decia la 4.1 va entre parentesis, como dato de OTRO producto.</summary>
        public const string SIN_MEDIR = "SIN MEDIR EN ES";
        private static string EnNq(string loQueDeciaLa41) => SIN_MEDIR + (string.IsNullOrEmpty(loQueDeciaLa41) ? "" : " (en NQ, otro producto: " + loQueDeciaLa41 + ")");

        /// <summary>5.0 (P5): como llega un nivel de cada libro al precio de ES (se repite en el criollo de cada serie).</summary>
        public const string CONV_SPX = "llevado a ES sumando la base ES - SPX sincronizada (la pestaña dice la base y su edad)";
        public const string CONV_SPY = "llevado a ES multiplicando por la razon ES/SPY sincronizada (~10,1; un strike de SPY = 1 USD ~ 10 pts de ES; la pestaña dice la razon y su edad)";
        /// <summary>5.0 (P5): el radio de seleccion de cada libro, en texto. Los valores los fija SeleccionFam.RadioDe (P1: ES 25, SPX y SPY 20, familia
        /// 20); aca se repiten como texto para no hacer depender el catalogo (que tambien compila la prueba de la pantalla) de la cuenta.</summary>
        public const string RADIO_ES_TXT = "a <= 25 pts del precio (radio del libro ES)";
        public const string RADIO_CBOE_TXT = "a <= 20 pts del precio, ya llevado a ES (radio de SPX y SPY)";

        private static SerieInfo S(string id, string corto, string libro, string fuente, string tipo, string grupo, string color, double banda,
                                   string tecnico, string criollo, string estado, List<SerieInfo> l, Dictionary<string, string> est)
        {
            var s = new SerieInfo { Id = id, Corto = corto, Libro = libro, Fuente = fuente, Tipo = tipo, Grupo = grupo, ColorHex = color, Banda = banda, Tecnico = tecnico, Criollo = criollo };
            l.Add(s); est[id] = estado; return s;
        }

        static CatalogoFamilia()
        {
            var l = new List<SerieInfo>(); var est = new Dictionary<string, string>(StringComparer.Ordinal);
            // --- las "recomendadas" de la 4.1 (en NQ: candidatas SIN VALIDAR del verificador; en ES no hay nada medido). El grupo queda con su nombre de
            //     siempre (es un identificador de la vista previa: no se muestra)
            S("MUROS_ES_vol", "ES", "ES", "vol", "MUROS", "Recomendadas", "#4db5e4", 0,
              "Call Wall / Put Wall del 0DTE de ES por volumen (opciones sobre el futuro ES de CME, por Rithmic, en vivo; 50 USD por punto)",
              "el strike del 0DTE de ES donde HOY se negocio mas gamma de calls (muro C) y el de mas gamma de puts (muro P), " + RADIO_ES_TXT + "; los dos pueden caer del mismo lado del precio",
              EnNq("candidata principal de noche, sin validar: punta de mecha 60,6 % contra 50,9 % del azar en 20 sesiones, Holm 0,135; contra la raya corrida 53,8 %"), l, est);
            S("MAJORS_SPY_oi", "SPY", "SPY", "oi", "MAJORS", "Recomendadas", "#e2b872", 5,   // 5.0 (P5): banda +-5 (era +-20, medio strike de QQQ en NQ)
              "Major Positive / Major Negative de SPY por interes abierto (banda +-5)",
              "el strike de SPY con mas gamma neta de las posiciones de ayer, positiva (M+) y negativa (M-), " + RADIO_CBOE_TXT + ", " + CONV_SPY + "; la banda +-5 es medio strike de SPY",
              EnNq("con QQQ: opcional de noche, sin validar: rebote 68,2 % contra 63,9 % del azar y 62,9 % de la raya corrida, Holm 0,84"), l, est);
            S("CONF_vol", "CONF", "familia", "vol", "CONF", "Recomendadas", "#f3d35a", 0,
              "Confluencia por volumen (muros, majors o cruces de 2 o mas libros a <= 3 pts uno del otro, a <= 20 pts del precio)",
              "un precio donde dos libros distintos (ES, SPX y SPY, ya llevados a ES) ponen un nivel casi en el mismo lugar (a <= 3 pts: un strike de ES o de SPX son 5)",
              EnNq("de dia, sin validar: rebote 66,1 % contra 62,3 % del azar, Holm 0,67"), l, est);
            S("MAJORS_ES_vol", "ES", "ES", "vol", "MAJORS", "Recomendadas", "#6cc3ff", 0,
              "Major Positive / Major Negative del 0DTE de ES por volumen",
              "el strike de ES con mas gamma neta de hoy, positiva (M+) y negativa (M-), " + RADIO_ES_TXT,
              EnNq("de noche, sin validar: punta de mecha 60,4 % contra 50,9 % del azar, Holm 0,117"), l, est);
            // --- la familia por libro, por volumen y por OI
            S("MUROS_ES_oi", "ES", "ES", "oi", "MUROS", "Familia", "#9a98d6", 0,
              "Call Wall / Put Wall del 0DTE de ES por interes abierto",
              "donde estan las posiciones abiertas de ayer en el 0DTE de ES (el strike con mas gamma de calls y el de mas gamma de puts), " + RADIO_ES_TXT + "; el OI lo publica CME una vez por noche; con el OI de anteayer no se dibuja",
              EnNq("sin ventaja medida: rebote 68,1 % contra 64,0 % del azar, Holm 1,0"), l, est);
            S("MUROS_SPX_vol", "SPX", "SPX", "vol", "MUROS", "Familia", "#2fb3a8", 0,
              "Call Wall / Put Wall de SPX por volumen (opciones del indice SPX de CBOE, 15 min tarde; 100 USD por punto)",
              "lo mismo en las opciones del indice SPX, " + RADIO_CBOE_TXT + ", " + CONV_SPX, EnNq("con NDX: sin ventaja medida"), l, est);
            S("MUROS_SPX_oi", "SPX", "SPX", "oi", "MUROS", "Familia", "#8de031", 0,
              "Call Wall / Put Wall de SPX por interes abierto", "posiciones abiertas de ayer en SPX, " + RADIO_CBOE_TXT + ", " + CONV_SPX, EnNq("con NDX: sin ventaja medida"), l, est);
            S("MUROS_SPY_vol", "SPY", "SPY", "vol", "MUROS", "Familia", "#dabe6f", 5,       // 5.0 (P5): banda +-5
              "Call Wall / Put Wall de SPY por volumen (banda +-5)", "lo mismo en el ETF SPY (CBOE, 15 min tarde), " + RADIO_CBOE_TXT + ", " + CONV_SPY,
              EnNq("con QQQ: sin ventaja medida"), l, est);
            S("MUROS_SPY_oi", "SPY", "SPY", "oi", "MUROS", "Familia", "#ff9f43", 5,         // 5.0 (P5): banda +-5
              "Call Wall / Put Wall de SPY por interes abierto (banda +-5)", "posiciones abiertas de ayer en SPY, " + RADIO_CBOE_TXT + ", " + CONV_SPY,
              EnNq("con QQQ: sin ventaja medida, rebote 68,0 % contra 63,9 % del azar, Holm 1,0"), l, est);
            S("MAJORS_ES_oi", "ES", "ES", "oi", "MAJORS", "Familia", "#b9a4ff", 0,
              "Major Positive / Major Negative del 0DTE de ES por interes abierto", "gamma neta de las posiciones de ayer en ES, " + RADIO_ES_TXT + " (con el OI de anteayer no se dibuja)",
              EnNq("sin ventaja medida"), l, est);
            S("MAJORS_SPX_vol", "SPX", "SPX", "vol", "MAJORS", "Familia", "#4fd1c5", 0,
              "Major Positive / Major Negative de SPX por volumen", "gamma neta de hoy en SPX, " + RADIO_CBOE_TXT + ", " + CONV_SPX, EnNq("con NDX: sin ventaja medida"), l, est);
            S("MAJORS_SPX_oi", "SPX", "SPX", "oi", "MAJORS", "Familia", "#a3e635", 0,
              "Major Positive / Major Negative de SPX por interes abierto", "gamma neta de las posiciones de ayer en SPX, " + RADIO_CBOE_TXT + ", " + CONV_SPX, EnNq("con NDX: sin ventaja medida"), l, est);
            S("MAJORS_SPY_vol", "SPY", "SPY", "vol", "MAJORS", "Familia", "#f6c177", 5,      // 5.0 (P5): banda +-5
              "Major Positive / Major Negative de SPY por volumen (banda +-5)", "gamma neta de hoy en SPY, " + RADIO_CBOE_TXT + ", " + CONV_SPY, EnNq("con QQQ: sin ventaja medida"), l, est);
            S("FAM_MUROS_vol", "FAM", "familia", "vol", "MUROS", "Familia", "#d04ad0", 0,
              "Call Wall / Put Wall de la familia sumada por volumen (USD de cobertura por 1 %: ES x50, SPX y SPY x100), en baldes de 5 pts (un strike de ES y de SPX), a <= 20 pts del precio",
              "los tres libros (ES, SPX, SPY) sumados en dolares en el precio de ES; en ES no esta medido cual libro pesa mas (en NQ los dominaba QQQ)",
              EnNq("NO dibujar como muro: su efecto era estar cerca del precio (contra la raya corrida: igual)"), l, est);
            S("FAM_MUROS_oi", "FAM", "familia", "oi", "MUROS", "Familia", "#e879f9", 0,
              "Call Wall / Put Wall de la familia sumada por interes abierto", "posiciones abiertas de ayer de los tres libros sumadas en dolares (solo si los tres tienen el OI de la misma noche)",
              EnNq("NO dibujar como muro: extremo 59,1 % contra 58,6 % de la raya corrida"), l, est);
            // --- zero y cruces (informativo)
            S("ZEST_ES_vol", "ES", "ES", "vol", "ZEST", "Zero", "#c9d4e3", 0,
              "Zero Gamma (Gamma Flip) estandar de ES por volumen: la gamma de todo el libro reevaluada a cada precio (grilla de 0,5 pts; a mas de 75 pts no se dibuja)",
              "el precio donde la gamma total del libro pasa de positiva a negativa; describe el regimen (de un lado se amortigua, del otro se acelera), no la direccion",
              EnNq("no es la raya '0G' de la 3.0 (esa es un cruce por strike); quedaba lejos, casi no saltaba y rebotaba igual que el azar"), l, est);
            S("ZEST_ES_oi", "ES", "ES", "oi", "ZEST", "Zero", "#94a3b8", 0,
              "Zero Gamma estandar de ES por interes abierto", "lo mismo con las posiciones de ayer (con el OI de anteayer no se dibuja)",
              EnNq("53 toques en 20 sesiones: no alcanzaba para medir"), l, est);
            S("ZEST_SPX_vol", "SPX", "SPX", "vol", "ZEST", "Zero", "#7dd3fc", 0,
              "Zero Gamma estandar de SPX por volumen (grilla de 0,5 pts; a mas de 75 pts no se dibuja)", "el zero del indice SPX, " + CONV_SPX, EnNq("con NDX: sin ventaja medida"), l, est);
            S("ZEST_SPY_vol", "SPY", "SPY", "vol", "ZEST", "Zero", "#fcd34d", 0,
              "Zero Gamma estandar de SPY por volumen (grilla de 0,05 USD ~ 0,5 pts de ES; a mas de 75 pts no se dibuja)", "el zero del ETF SPY, " + CONV_SPY, EnNq("con QQQ: sin ventaja medida"), l, est);
            S("ZTP_ES_vol", "ES", "ES", "vol", "ZTP", "Zero", "#f472b6", 0,
              "Cruces de signo de la gamma por strike, limpios (sin islas), el mas cercano arriba y abajo del precio, ES por volumen, " + RADIO_ES_TXT,
              "el tunel de la 3.0 sin los strikes 'isla' (signo distinto a sus dos vecinos y menos del 10 % del menor de ellos) y sin sostener cruces muertos",
              EnNq("NO es muro: el tunel no retenia mas que uno puesto al azar (vuelve 41,3 % contra 43,6 %)"), l, est);
            S("ZTP_SPX_vol", "SPX", "SPX", "vol", "ZTP", "Zero", "#fb7185", 0,
              "Cruces de signo limpios de SPX por volumen", "el cruce de SPX mas cercano arriba y abajo, sin islas, " + RADIO_CBOE_TXT + ", " + CONV_SPX, EnNq("con NDX: NO es muro"), l, est);
            // --- lo que dibuja la 3.0 (en la 5.0: el motor 3.0 que corre adentro, sus estelas en PythiaGex5\estela)
            S("TRES_ES", "3.0", "ES", "3.0", "TRES", "3.0", "#8a96a8", 0,
              "D1/D2 del motor 3.0 que corre adentro de la 5.0 (estela-ES: libro ES por Rithmic)", "las rayas que dibujaria la 3.0 en este grafico, para comparar",
              EnNq("lo que habia; nunca validado"), l, est);
            S("TRES_SPX", "3.0 SPX", "SPX", "3.0", "TRES", "3.0", "#5f6b7d", 0,
              "D1/D2 de la capa SPX del motor 3.0 interno (CBOE, 15 min tarde; aproximada a la 3.0, sin arnes propio en ES)",
              "la capa SPX calculada adentro y llevada a ES con la conversion de ESA capa (puede no ser la base de la Familia: la pestaña no la muestra)",
              EnNq("con NDX: lo que habia; nunca validado"), l, est);
            S("TRES_SPY", "3.0 SPY", "SPY", "3.0", "TRES", "3.0", "#7c6f57", 0,
              "D1/D2 de la capa SPY del motor 3.0 interno (CBOE, 15 min tarde; aproximada a la 3.0, sin arnes propio en ES)",
              "la capa SPY calculada adentro y llevada a ES con la razon de ESA capa (puede no ser la de la Familia: la pestaña no la muestra)",
              EnNq("con QQQ: lo que habia; nunca validado"), l, est);
            // --- 5.0 (27-10-2026): aca iban las 6 de TQQQ x3 (tqqq_vivo.py 1.0). Se sacaron: la 5.0 es del S&P y no tiene un equivalente pedido.
            // --- 4.1.3 (09-10-2026): como la 2.0 (Replica20.cs). Rol D1 = la de mayor |GEX|, D2 la segunda; monto con signo (escala 1).
            //     5.0 (P5): en un grafico de ES la 2.0 no arma capas: dibuja UN libro, SPX ('Libro automatico'), con radio min(2 %, 100) = 100 pts y
            //     retraso 960 s (MEDICIONES_ES.md §6). R20_SPY_vol no tiene equivalente en la 2.0 en ES.
            S("R20_SPY_vol", "2.0 SPY", "SPY", "vol", "DOMS", "2.0", "#cc8800", 0,   // 4.1.4: ambar oscuro (el #e8c83c no se distinguia del SPY 0G #fcd34d)
              "Seleccion estilo 2.0 sobre SPY: las 2 barras de mayor |GEX| por volumen del 0DTE de SPY a <= min(2 % del futuro, 100 pts), con la conversion de la 2.0 para un ETF (razon = MES de la vela de 1 min del ultimo trade de la cadena / spot de SPY). La 2.0 NO dibuja SPY en un grafico de ES (alli su unico libro es SPX)",
              "la formula de la 2.0 aplicada a SPY, para comparar; no replica nada que la 2.0 muestre en ES; de noche la razon junta el MES de las 16:14 NY con el SPY del after-hours",
              EnNq("con QQQ era la raya ambar de la 2.0; su razon erro 24 pts de mediana de noche en 16 noches"), l, est);
            S("R20_SPX_vol", "2.0 SPX", "SPX", "vol", "DOMS", "2.0", "#3ddc97", 0,
              "Dominantes del libro SPX de la 2.0 en un grafico de ES (con la cadena de CBOE 960 s atras): las 2 de mayor |GEX| por volumen del 0DTE de SPX a <= min(2 % del futuro, 100 pts) (= 100 pts en ES), sin lado ni centroide, con la base de la 2.0",
              "lo que la 2.0 dibuja en ES (su unico libro alli es SPX), calculado adentro con su formula; la pestaña dice que base uso y de donde salio",
              EnNq("con NDX, la base cruda de forwards de la 2.0 erro 5,1 pts de mediana en 17 noches") + "; SIN VERIFICAR contra la 2.0 corriendo en ES (de la 2.0 en ES solo hay 5 salidas, del 05-10)", l, est);
            S("DOMS_SPY_vol", "SPY dom", "SPY", "vol", "DOMS", "2.0", "#5b8cff", 0,
              "Seleccion de la 2.0 (2 de mayor |GEX| por volumen a <= min(2 %, 100 pts)) sobre el libro SPY de la 5.0 (razon sincronizada C8)",
              "las dominantes estilo 2.0 de SPY pero puestas con la conversion de la 5.0",
              EnNq("con QQQ, medida en 18 noches como la mejor combinacion conocida (seleccion 2.0 + conversion sincronizada); describe, no anticipa"), l, est);
            S("DOMS_SPX_vol", "SPX dom", "SPX", "vol", "DOMS", "2.0", "#a9c4ff", 0,
              "Seleccion de la 2.0 sobre el libro SPX de la 5.0 (base sincronizada C7)",
              "las dominantes de SPX estilo 2.0 con la base de la 5.0 (no la de la 2.0)",
              EnNq("con NDX tampoco se midio: apagada por defecto"), l, est);
            // --- 4.1.5 (09-10-2026): como la clasica (ClasicaSpx.cs). Rol "Z" (zero, sin monto); el strike guardado es el zero en el eje del indice.
            //     5.0 (P5): en ES la clasica tampoco arma capas: su libro primario es CBOE_SPX (MEDICIONES_ES.md §6). R10_* = ese libro.
            S("R10_SPX_zero", "Clasica SPX 0Γ", "SPX", "vol", "ZEST", "Clasica", "#ffffff", 0,
              "Zero Gamma por volumen del 0DTE de SPX como lo calcula la clasica (Gamma Hoy) en un grafico de ES, donde su libro es SPX (con la cadena de CBOE 960 s atras): grilla de 61 precios en +-3 %, el PRIMER cruce de abajo hacia arriba, llevado al futuro con SU base y su cascada (medida > medida hace <= 360 min > de la rueda <= 24 h > cruda > teorica; la pestaña dice cual, de donde salio y de cuando es)",
              "el zero de SPX de la clasica en ES, calculado adentro; la cuenta es la del zero estandar con la grilla de la clasica: lo que lo separa de 'SPX 0Γ est' es la grilla y, sobre todo, la base",
              SIN_MEDIR + " como nivel; la replica, comparada con la clasica en ES solo el 05-10 (131 salidas, _diseno/P2_SPX.md): de dia la misma base y el mismo zero (mediana 0,00), esa noche +1,27 pts (ella en la de la rueda, la replica en la medida de hace 185 min)", l, est);
            // --- 4.1.5b (09-10-2026, pedido del operador: "replicar la clasica su formula ... SPX D1 en 31141"): las dominantes de la capa SPX de la clasica
            //     5.0 (P5): en ES son las dominantes del libro primario (SPX) de la clasica.
            S("R10_SPX_dom", "Clasica SPX", "SPX", "vol", "DOMS", "Clasica", "#b8f8d8", 0,   // 4.1.5d: menta palido (el #9dffd2 quedaba a 11,4 del 2.0 SPX)
              "Dominantes D1-D3 del libro SPX de la clasica en un grafico de ES (con la cadena de CBOE 960 s atras): GEX por volumen del 0DTE de SPX, radio min(2 %, 100 pts) (= 100 en ES), una por lado con empate 20 % (la mas cercana), D3 = la siguiente mas fuerte, en el centroide +-12 pts; con la misma base que la Clasica SPX 0Γ",
              "las rayas D1/D2/D3 de la clasica en ES, calculadas adentro con su formula (misma cadena, base y fut que la Clasica SPX 0Γ); a diferencia de la clasica, que solo deja estela de D1 y D2, aca D3 tambien deja estela; sin histeresis y sin cambio ▲▼",
              SIN_MEDIR + " como nivel; la replica, comparada con la clasica en ES solo el 05-10 (131 salidas, _diseno/P2_SPX.md): con la base y el precio de la clasica, las mismas D1-D3 en 102 (en el resto la clasica leia otra cadena)", l, est);

            // --- 4.1.7 (12-10-2026): TOP-N del libro de ES. MUROS/MAJORS marcan UN strike por lado (en NQ, el 12-10: 24 strikes con gamma en la ventana
            //     y 2 dibujados).
            S("TOPN_ES_vol", "ES top", "ES", "vol", "TOPN", "Recomendadas", "#7cf5c4", 0,
              "Los 6 strikes de ES con mas |gamma neta| por volumen " + RADIO_ES_TXT + " (ahi entran ~10 strikes, cada 5 pts), de mayor a menor (D1..D6)",
              "los strikes de ES donde HOY se negocio mas gamma, no solo el mayor: se recalculan cada minuto y se mueven con el libro",
              EnNq("sin validar como nivel; es cobertura, no seleccion (el 12-10: 24 strikes con gamma en la ventana, 2 dibujados)"), l, est);
            S("TOPN_ES_oi", "ES top OI", "ES", "oi", "TOPN", "Recomendadas", "#4fb98a", 0,
              "Los 6 strikes de ES con mas |gamma neta| por interes abierto " + RADIO_ES_TXT + ", de mayor a menor (D1..D6)",
              "los strikes de ES con mas posiciones abiertas de ayer, los N mas pesados y no solo el mayor (con el OI de anteayer no se dibuja)",
              EnNq("sin validar como nivel; misma logica que TOPN_ES_vol con el OI de la OCC"), l, est);

            S("CARGA_ES", "ES", "ES", "oi", "TOPN", "Recomendadas", "#ffb74d", 0,
              "Los strikes de ES con mas INTERES ABIERTO " + RADIO_ES_TXT + " (umbral y tope: ajustes '↳ carga' del grupo 3.0; en ES casi todos pasan 50 contratos, medido 27-10). Horizonte REAL: el libro ES suscribe solo los 2 vencimientos mas cercanos; sin el contrato sin punta (iv <= 0). Es un piso, no el total de CME",
              "donde hay mas papel puesto cerca del precio, sumando los vencimientos que se ven (el calculo normal de la Familia solo mira el vencimiento de hoy)",
              EnNq("sin validar como nivel; es cobertura: marca lo que el horizonte del frente deja afuera"), l, est);
            S("CARGA_SPX", "SPX", "SPX", "oi", "TOPN", "Recomendadas", "#ce93d8", 0,
              "Los strikes de SPX con mas INTERES ABIERTO " + RADIO_CBOE_TXT + " (umbral y tope: ajustes '↳ carga' del grupo 3.0). Horizonte REAL: CBOE hasta 14 dias en vivo y 8 desde el archivo, strikes a +-5 % del spot; sin el contrato sin punta (iv <= 0). Es un piso, no el total de la cadena",
              "donde hay mas papel puesto cerca del precio en SPX; la etiqueta lleva el strike de SPX (el que se cruza contra CBOE) y la raya va " + CONV_SPX,
              EnNq("con NDX: sin validar como nivel; es cobertura"), l, est);
            S("CARGA_SPY", "SPY", "SPY", "oi", "TOPN", "Recomendadas", "#80cbc4", 0,
              "Los strikes de SPY con mas INTERES ABIERTO " + RADIO_CBOE_TXT + " (umbral y tope: ajustes '↳ carga' del grupo 3.0). Horizonte REAL: CBOE hasta 14 dias en vivo y 8 desde el archivo, strikes a +-5 % del spot; sin el contrato sin punta (iv <= 0). Es un piso, no el total de la cadena",
              "donde hay mas papel puesto cerca del precio en SPY; la etiqueta lleva el strike de SPY y la raya va " + CONV_SPY,
              EnNq("con QQQ: sin validar como nivel; es cobertura"), l, est);

            Series = l.AsReadOnly();
            PorId = l.ToDictionary(x => x.Id, StringComparer.Ordinal);
            Estado = est;
            Calc = l.Take(24).Where(x => x.Tipo != "TRES").Select(x => x.Id).Concat(new[] { "TOPN_ES_vol", "TOPN_ES_oi", "CARGA_ES", "CARGA_SPX", "CARGA_SPY" }).ToArray();   // 4.1.7: anexadas AL FINAL (0-20 intactos)
            // 5.0 (27-10-2026): las T_* estaban en las posiciones 24-29 de 'l', despues del Take(24): Calc sale identico al del clon (los mismos
            // 26 ids en las mismas posiciones; verificado volcando el catalogo antes y despues, ver _diseno/QUITAR_TQQQ.md).
            _idxCalc = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < Calc.Length; i++) _idxCalc[Calc[i]] = i;
        }
    }
}
