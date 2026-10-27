// CatalogoFamilia.cs — PythiaGex 4.1, modulo fam (B3d), 08-10-2026.
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
// Sin referencias a ATAS.
using System;
using System.Collections.Generic;
using System.Linq;

namespace PythiaGexCinco.Familia
{
    public static class CatalogoFamilia
    {
        /// <summary>Las 35 series: las 24 de la vista previa en su orden, las 5 de TQQQ, (4.1.3) las 4 "como la 2.0" y (4.1.5/4.1.5b) las 2 de la clasica.</summary>
        public static readonly IReadOnlyList<SerieInfo> Series;
        /// <summary>id -> serie.</summary>
        public static readonly IReadOnlyDictionary<string, SerieInfo> PorId;
        /// <summary>id -> texto 'estado' de la vista previa (SIN VALIDAR, etc.). SerieInfo no tiene ese campo: va aparte.</summary>
        public static readonly IReadOnlyDictionary<string, string> Estado;
        /// <summary>preview_niveles.CALC: las 21 series que calcula la cuenta (todas menos TRES y TQQQ), en orden. El indice es el de RegistroMinuto.Series.</summary>
        public static readonly string[] Calc;
        /// <summary>id -> indice en Calc (o -1).</summary>
        public static int IndiceCalc(string id) => _idxCalc.TryGetValue(id ?? "", out var i) ? i : -1;
        private static readonly Dictionary<string, int> _idxCalc;

        public static readonly string[] Libros = { "ES", "SPX", "SPY" };       // backtest_familia.LIBROS (orden de bk)
        public static readonly string[] CapasTres = { "ES", "SPX", "SPY" };
        public static readonly string[] SeriesTqqq = { "T_DOMS_vol", "T_MUROS_vol", "T_MUROS_oi", "T_ZERO_oi", "T_DOMS_raz", "T_ZERO_vol_pm" };
        /// <summary>4.1.3: las series "como la 2.0" (fuera de Calc) y (4.1.5) la de la clasica, AL FINAL (un archivo de la 4.1.4 se lee igual). El indice
        /// es el de RegistroMinuto.Extra / ExtraStrikes.</summary>
        public static readonly string[] Extra = { "R20_SPY_vol", "R20_SPX_vol", "DOMS_SPY_vol", "DOMS_SPX_vol", "R10_SPX_zero", "R10_SPX_dom" };   // 4.1.5b: + R10_SPX_dom al final
        /// <summary>id -> indice en Extra (o -1).</summary>
        public static int IndiceExtra(string id) { for (int i = 0; i < Extra.Length; i++) if (Extra[i] == id) return i; return -1; }
        /// <summary>4.1.3: el MetaLibro de donde sale la edad y la conversion de una serie extra: las replicas de la 2.0 tienen el suyo
        /// (RegistroMinuto.MetaExtra, "2.0 SPY" / "2.0 SPX"); las DOMS_* usan el de su libro de la 4.1 (RegistroMinuto.Meta). null = el del libro.
        /// 4.1.5: la replica de la clasica, "Clasica SPX" (su base y su origen).</summary>
        public static string MetaExtraDe(string id) => id == "R20_SPY_vol" ? "2.0 SPY" : id == "R20_SPX_vol" ? "2.0 SPX" : (id == "R10_SPX_zero" || id == "R10_SPX_dom") ? "Clasica SPX" : null;
        /// <summary>Default de las casillas: las 8 que eligio el operador (captura del 08-10 17:5x) + 4.1.3: las dominantes de la 2.0 (SPY y SPX) y
        /// la seleccion 2.0 sobre el libro SPY de la 4.1 (pedido del operador 09-10: verlas PRENDIDAS junto a las de la 4.1).
        /// 4.1.4 (pedido del operador 09-10: "suman ruido al ser ya superadas por la formula de la 2.0 spy"): fuera SPY majors OI, SPY muros OI,
        /// FAM muros OI y SPY dom. Quedan 6 (mismo orden que SeriesPantalla.PrendidasPorDefecto).
        /// 4.1.5 (pedido del operador 09-10: "copiarla ahora para nuestro 4.0"): + R10_SPX_zero, la estela 'SPX 0Γ' de la clasica. Quedaban 7;
        /// 4.1.5b: + R10_SPX_dom (las D1-D3 de la clasica). Quedan 8.</summary>
        public static readonly string[] PrendidasPorDefecto = { "MUROS_ES_oi", "MUROS_SPX_vol", "TRES_SPX", "T_MUROS_oi",
                                                                "R20_SPY_vol", "R20_SPX_vol", "R10_SPX_zero", "R10_SPX_dom" };   // 4.1.5b: + Clasica SPX D1-D3 (pedido del operador)
        /// <summary>preview_niveles.PRESETS (+ los de tqqq_vivo).</summary>
        public static readonly IReadOnlyDictionary<string, string[]> Presets = new Dictionary<string, string[]>
        {
            ["Recomendadas"] = new[] { "MUROS_ES_vol", "MAJORS_SPY_oi", "CONF_vol", "TRES_ES" },
            ["Familia vol"] = new[] { "MUROS_ES_vol", "MUROS_SPX_vol", "MUROS_SPY_vol", "FAM_MUROS_vol", "TRES_ES" },
            ["Familia OI"] = new[] { "MUROS_ES_oi", "MUROS_SPX_oi", "MUROS_SPY_oi", "MAJORS_SPY_oi", "FAM_MUROS_oi", "TRES_ES" },
            ["Zero y cruces"] = new[] { "ZEST_ES_vol", "ZEST_SPX_vol", "ZEST_SPY_vol", "ZTP_ES_vol", "TRES_ES" },
            ["3.0 actual"] = new[] { "TRES_ES", "TRES_SPX", "TRES_SPY" },
            ["TQQQ x3"] = new[] { "T_DOMS_vol", "T_MUROS_oi" },
            ["TQQQ vs Gammalito"] = new[] { "T_DOMS_vol", "T_DOMS_raz" },
            ["2.0 contra 4.1"] = new[] { "R20_SPY_vol", "R20_SPX_vol", "DOMS_SPY_vol", "DOMS_SPX_vol" },
            ["Clasica contra 4.1"] = new[] { "R10_SPX_zero", "R10_SPX_dom", "ZEST_SPX_vol", "ZTP_SPX_vol" },
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

        private static SerieInfo S(string id, string corto, string libro, string fuente, string tipo, string grupo, string color, double banda,
                                   string tecnico, string criollo, string estado, List<SerieInfo> l, Dictionary<string, string> est)
        {
            var s = new SerieInfo { Id = id, Corto = corto, Libro = libro, Fuente = fuente, Tipo = tipo, Grupo = grupo, ColorHex = color, Banda = banda, Tecnico = tecnico, Criollo = criollo };
            l.Add(s); est[id] = estado; return s;
        }

        static CatalogoFamilia()
        {
            var l = new List<SerieInfo>(); var est = new Dictionary<string, string>(StringComparer.Ordinal);
            // --- recomendadas por el verificador (candidatas SIN VALIDAR)
            S("MUROS_ES_vol", "ES", "ES", "vol", "MUROS", "Recomendadas", "#4db5e4", 0,
              "Call wall / put wall del 0DTE de ES por volumen",
              "el strike del 0DTE de ES donde HOY se negocio mas gamma de calls (muro C, arriba) y de puts (muro P, abajo), a +-100 pts",
              "candidata principal de noche, SIN VALIDAR: punta de mecha 60,6 % contra 50,9 % del azar (20 sesiones), Holm 0,135; contra la raya corrida 53,8 %", l, est);
            S("MAJORS_SPY_oi", "SPY", "SPY", "oi", "MAJORS", "Recomendadas", "#e2b872", 20,
              "Major positivo / negativo de SPY por interes abierto (banda +-20)",
              "el strike de SPY con mas gamma neta acumulada (posiciones de ayer); un strike de SPY son 41 pts de ES, por eso es una banda de +-20",
              "opcional de noche, SIN VALIDAR: rebote 68,2 % contra 63,9 % del azar y 62,9 % de la raya corrida, Holm 0,84", l, est);
            S("CONF_vol", "CONF", "familia", "vol", "CONF", "Recomendadas", "#f3d35a", 0,
              "Confluencia por volumen (muros, majors o cruces de 2 o mas libros a <= 3 pts)",
              "un precio donde dos libros distintos (ES, SPX, SPY) ponen un nivel casi en el mismo lugar",
              "de dia, SIN VALIDAR: rebote 66,1 % contra 62,3 % del azar, Holm 0,67", l, est);
            S("MAJORS_ES_vol", "ES", "ES", "vol", "MAJORS", "Recomendadas", "#6cc3ff", 0,
              "Major positivo / negativo del 0DTE de ES por volumen",
              "el strike de ES con mas gamma neta de hoy, positiva (M+) y negativa (M-)",
              "de noche, SIN VALIDAR: punta de mecha 60,4 % contra 50,9 % del azar, Holm 0,117", l, est);
            // --- la familia por libro, por volumen y por OI
            S("MUROS_ES_oi", "ES", "ES", "oi", "MUROS", "Familia", "#9a98d6", 0,
              "Call wall / put wall de ES por interes abierto",
              "donde estan las posiciones abiertas de ayer en el 0DTE de ES (calls arriba, puts abajo)",
              "sin ventaja medida (rebote 68,1 % contra 64,0 % del azar, Holm 1,0); sin dibujar mientras el OI es de anteayer", l, est);
            S("MUROS_SPX_vol", "SPX", "SPX", "vol", "MUROS", "Familia", "#2fb3a8", 0,
              "Call wall / put wall de SPX por volumen (a precio de ES por la base sincronizada)",
              "lo mismo en las opciones del indice SPX de CBOE, corrido a precio del futuro", "sin ventaja medida", l, est);
            S("MUROS_SPX_oi", "SPX", "SPX", "oi", "MUROS", "Familia", "#8de031", 0,
              "Call wall / put wall de SPX por interes abierto", "posiciones abiertas en SPX", "sin ventaja medida", l, est);
            S("MUROS_SPY_vol", "SPY", "SPY", "vol", "MUROS", "Familia", "#dabe6f", 20,
              "Call wall / put wall de SPY por volumen (banda +-20)", "lo mismo en el ETF SPY (strikes cada 41 pts de ES)",
              "sin ventaja medida", l, est);
            S("MUROS_SPY_oi", "SPY", "SPY", "oi", "MUROS", "Familia", "#ff9f43", 20,
              "Call wall / put wall de SPY por interes abierto (banda +-20)", "posiciones abiertas en SPY",
              "sin ventaja medida (rebote 68,0 % contra 63,9 % del azar, Holm 1,0)", l, est);
            S("MAJORS_ES_oi", "ES", "ES", "oi", "MAJORS", "Familia", "#b9a4ff", 0,
              "Major +/- de ES por interes abierto", "gamma neta de las posiciones de ayer en ES", "sin ventaja medida", l, est);
            S("MAJORS_SPX_vol", "SPX", "SPX", "vol", "MAJORS", "Familia", "#4fd1c5", 0,
              "Major +/- de SPX por volumen", "gamma neta de hoy en SPX", "sin ventaja medida", l, est);
            S("MAJORS_SPX_oi", "SPX", "SPX", "oi", "MAJORS", "Familia", "#a3e635", 0,
              "Major +/- de SPX por interes abierto", "gamma neta de las posiciones de ayer en SPX", "sin ventaja medida", l, est);
            S("MAJORS_SPY_vol", "SPY", "SPY", "vol", "MAJORS", "Familia", "#f6c177", 20,
              "Major +/- de SPY por volumen (banda +-20)", "gamma neta de hoy en SPY", "sin ventaja medida", l, est);
            S("FAM_MUROS_vol", "FAM", "familia", "vol", "MUROS", "Familia", "#d04ad0", 0,
              "Muros de la familia sumada en USD por 1 % (ES x20, SPX y SPY x100), grilla de 5 pts",
              "los tres libros sumados en dolares; los domina SPY",
              "NO dibujar como muro: su efecto es estar cerca del precio (contra la raya corrida: igual)", l, est);
            S("FAM_MUROS_oi", "FAM", "familia", "oi", "MUROS", "Familia", "#e879f9", 0,
              "Muros de la familia sumada por interes abierto", "posiciones abiertas de los tres libros sumadas en dolares",
              "NO dibujar como muro (extremo 59,1 % contra 58,6 % de la raya corrida)", l, est);
            // --- zero y cruces (informativo)
            S("ZEST_ES_vol", "ES", "ES", "vol", "ZEST", "Zero", "#c9d4e3", 0,
              "Zero gamma estandar de ES por volumen (la suma de todo el libro reevaluada a cada precio)",
              "el precio donde la gamma total del libro pasa de positiva a negativa; queda lejos y casi no salta",
              "no es la raya '0G' de la 3.0 (esa es un cruce por strike); rebota igual que el azar", l, est);
            S("ZEST_ES_oi", "ES", "ES", "oi", "ZEST", "Zero", "#94a3b8", 0,
              "Zero gamma estandar de ES por OI", "lo mismo con las posiciones de ayer", "53 toques en 20 sesiones: no alcanza", l, est);
            S("ZEST_SPX_vol", "SPX", "SPX", "vol", "ZEST", "Zero", "#7dd3fc", 0,
              "Zero gamma estandar de SPX por volumen", "zero del indice", "sin ventaja medida", l, est);
            S("ZEST_SPY_vol", "SPY", "SPY", "vol", "ZEST", "Zero", "#fcd34d", 0,
              "Zero gamma estandar de SPY por volumen", "zero del ETF", "sin ventaja medida", l, est);
            S("ZTP_ES_vol", "ES", "ES", "vol", "ZTP", "Zero", "#f472b6", 0,
              "Cruces de signo limpios (sin islas) mas cercanos arriba y abajo, ES por volumen",
              "el tunel de la 3.0 pero sin los strikes 'isla' de 2-4 contratos y sin sostener cruces muertos",
              "NO es muro: el tunel no retiene mas que uno puesto al azar (vuelve 41,3 % contra 43,6 %)", l, est);
            S("ZTP_SPX_vol", "SPX", "SPX", "vol", "ZTP", "Zero", "#fb7185", 0,
              "Cruces de signo limpios de SPX por volumen", "techo y piso del zero de SPX sin islas", "NO es muro", l, est);
            // --- lo que dibujo la 3.0 (en la 4.1: el motor 3.0 que corre adentro, sus estelas en PythiaGex5\estela)
            S("TRES_ES", "3.0", "ES", "3.0", "TRES", "3.0", "#8a96a8", 0,
              "D1/D2 que dibujo la 3.0 (estela-ES)", "las rayas de su grafico de ATAS, para comparar", "lo que hay hoy", l, est);
            S("TRES_SPX", "3.0 SPX", "SPX", "3.0", "TRES", "3.0", "#5f6b7d", 0,
              "D1/D2 de la capa SPX del motor 3.0 interno (aprox. a la 3.0: spot al sello - 900 s, base por forwards; sin arnes propio)", "la capa SPX de su grafico, calculada adentro", "lo que hay hoy", l, est);
            S("TRES_SPY", "3.0 SPY", "SPY", "3.0", "TRES", "3.0", "#7c6f57", 0,
              "D1/D2 de la capa SPY del motor 3.0 interno (aprox. a la 3.0: spot al sello - 900 s; sin arnes propio)", "la capa SPY de su grafico, calculada adentro", "lo que hay hoy", l, est);
            // --- TQQQ x3 (tqqq_vivo.py 1.0)
            S("T_DOMS_vol", "TQQQ", "TQQQ", "vol", "DOMS", "TQQQ x3", "#ffd54f", 4.0,
              "Dominantes de TQQQ por volumen (las dos barras de GEX mas grandes del vencimiento mas cercano), x3 bien convertido",
              "los dos strikes de TQQQ donde hoy se negocio mas gamma: las rayas amarillas de Gammalito, pero puestas donde caen de verdad en ES (124 pts por dolar)",
              "SIN VALIDAR: hoy 3 rebotes en 8 toques contra 48 % al azar (t04); una rueda", l, est);
            S("T_MUROS_vol", "TQQQ", "TQQQ", "vol", "MUROS", "TQQQ x3", "#26c6da", 4.0,
              "Call wall / put wall de TQQQ por volumen (vencimiento mas cercano), x3 bien convertido",
              "el strike de TQQQ con mas gamma de calls negociada hoy (muro C) y el de puts (muro P)", "SIN VALIDAR (t04: 4 de 10)", l, est);
            S("T_MUROS_oi", "TQQQ", "TQQQ", "oi", "MUROS", "TQQQ x3", "#ba68c8", 4.0,
              "Call wall / put wall de TQQQ por interes abierto (OI de la OCC al cierre de ayer), x3 bien convertido",
              "los strikes de TQQQ con mas posiciones abiertas de calls y de puts desde ayer", "SIN VALIDAR (t04: 3 de 7)", l, est);
            S("T_ZERO_oi", "TQQQ", "TQQQ", "oi", "ZTP", "TQQQ x3", "#b0bec5", 4.0,
              "Zero gamma de TQQQ por OI (cruce de signo del perfil mas cercano al spot), x3 bien convertido",
              "donde el libro de TQQQ pasa de frenar a empujar segun las posiciones de ayer", "SIN VALIDAR; el cruce mas cercano se pega al precio", l, est);
            S("T_ZERO_vol_pm", "TQQQ", "TQQQ", "vol", "ZTP", "TQQQ x3", "#e8eaed", 4.0,
              "Zero de TQQQ por VOLUMEN en el PUNTO MEDIO de los dos strikes que encierran el cruce (no interpolado)",
              "la raya blanca de Gammalito: el medio del hueco donde el libro de TQQQ pasa de frenar a empujar, porque entre strike y strike no se sabe donde cae exactamente",
              "SIN VALIDAR como nivel; lo que SI esta medido (12-10) es que reproduce la raya de Gammalito en 2 dias distintos: 79,50 hoy y 81,50 el 08-10, los dos exactos, mientras el cruce interpolado daba 79,13-79,28 y 81,20. La incertidumbre REAL es +-0,5 USD de TQQQ (~63 pts de ES): la banda de 4 es cosmetica, no honesta", l, est);
            S("T_DOMS_raz", "TQQQ", "TQQQ", "vol", "RAZON", "TQQQ x3", "#ff7043", 0,
              "COMPARACION: las mismas dominantes convertidas con RAZON SIMPLE (K x ES/TQQQ), como las integra Gammalito (medido en t08)",
              "asi las dibuja Gammalito sobre ES: 3 veces mas lejos del precio y corriendose al reves del precio (el abanico). NO usar",
              "SOLO PARA COMPARAR", l, est);
            // --- 4.1.3 (09-10-2026): como la 2.0 (Replica20.cs). Rol D1 = la de mayor |GEX|, D2 la segunda; monto con signo (escala 1).
            S("R20_SPY_vol", "2.0 SPY", "SPY", "vol", "DOMS", "2.0", "#cc8800", 0,   // 4.1.4: ambar oscuro (el #e8c83c no se distinguia del SPY 0G #fcd34d)
              "Dominantes de la 2.0 (SPY 0DTE por volumen: las 2 barras de mayor |GEX| a <= min(2 % del futuro, 100 pts)) con SU conversion: razon = cierre de la vela de 1 min del MES del ultimo trade de la cadena / spot de SPY",
              "la raya ambar de la 2.0, calculada adentro igual que ella; de noche junta el MES de las 16:14 NY con el SPY del after-hours y queda corrida unos puntos",
              "replica para comparar y calibrar; la conversion de la 2.0 tiene error medido (|err| mediano 24 pts de noche en 16 noches)", l, est);
            S("R20_SPX_vol", "2.0 SPX", "SPX", "vol", "DOMS", "2.0", "#3ddc97", 0,
              "Capa SPX de la 2.0: la misma seleccion sobre la cadena _SPX de CBOE con base = base cruda de forwards de la cadena (si pasa la cota del carry)",
              "las rayas verdes de SPX de la 2.0, calculadas adentro igual que ella",
              "replica para comparar; base cruda de la 2.0: |err| mediano 5,1 pts en 17 noches", l, est);
            S("DOMS_SPY_vol", "SPY dom", "SPY", "vol", "DOMS", "2.0", "#5b8cff", 0,
              "Seleccion de la 2.0 (2 de mayor |GEX| por volumen a <= min(2 %, 100 pts)) sobre el libro SPY de la 4.1 (razon sincronizada C8)",
              "las dominantes de la 2.0 pero puestas con la conversion de la 4.1",
              "medida en 18 noches como la mejor combinacion conocida (seleccion 2.0 + conversion sincronizada); describe, no anticipa", l, est);
            S("DOMS_SPX_vol", "SPX dom", "SPX", "vol", "DOMS", "2.0", "#a9c4ff", 0,
              "Seleccion de la 2.0 sobre el libro SPX de la 4.1 (base sincronizada C7)",
              "las dominantes de SPX de la 2.0 con la base de la 4.1",
              "SIN MEDIR: apagada por defecto", l, est);
            // --- 4.1.5 (09-10-2026): como la clasica (ClasicaSpx.cs). Rol "Z" (zero, sin monto); el strike guardado es el zero en el eje del indice.
            S("R10_SPX_zero", "Clasica SPX 0Γ", "SPX", "vol", "ZEST", "Clasica", "#ffffff", 0,
              "Zero gamma por volumen del 0DTE de SPX como la clasica (Gamma Hoy, capa SPX: grilla de 61 precios en +-3 %, el PRIMER cruce de abajo hacia arriba) llevado al futuro con SU base: la de la rueda ANTERIOR (su regla MedirBaseRueda sobre las cadenas y la cinta de la 4.1), congelada durante la rueda; con mas de 24 h, la cruda de forwards y despues el carry",
              "los puntitos blancos 'SPX 0Γ' de la clasica, calculados adentro; la formula es la del zero estandar de SPX (ZEST, a -0,14 pt con la misma base): lo que la corre 13-15 pts es la base de la rueda anterior",
              "replica para comparar y calibrar; SIN VALIDAR: el 09-10 la base vieja (243,06 contra ~228 real) la puso 13-15 pts arriba y el precio la toco 2 veces (mirado despues). SALVEDAD (medida el 09-10): copia a la clasica cuando dibuja con la base de la rueda anterior (su primaria en Rithmic); si la clasica la pierde (p. ej. al reiniciar ATAS) re-mide su base en la rueda y la replica deja de coincidir: de 16:48 a 17:11 ART quedo ~13 pts corrida", l, est);
            // --- 4.1.5b (09-10-2026, pedido del operador: "replicar la clasica su formula ... SPX D1 en 31141"): las dominantes de la capa SPX de la clasica
            S("R10_SPX_dom", "Clasica SPX", "SPX", "vol", "DOMS", "Clasica", "#b8f8d8", 0,   // 4.1.5d: menta palido (el #9dffd2 quedaba a 11,4 del 2.0 SPX)
              "Dominantes D1-D3 de la capa SPX de la clasica (Gamma Hoy): GEX por volumen del 0DTE de SPX, radio min(2 %, 100 pts), una por lado con empate 20 % (la mas cercana), D3 = la siguiente mas fuerte, en el centroide +-12 pts; llevadas al futuro con SU base (la de la rueda anterior, como el 0Γ)",
              "las rayas 'SPX D1/D2/D3' de la clasica, calculadas adentro con su formula (misma cadena, base y fut que la Clasica SPX 0Γ); a diferencia de la clasica, que solo deja estela de D1 y D2, aca D3 tambien deja estela",
              "replica para comparar y calibrar; SIN VALIDAR; sin histeresis (cada minuto elige de cero, como la clasica); sin cambio ▲▼ (su base no es la del libro de la 4.1). SALVEDAD (medida el 09-10): si la clasica pierde Rithmic (p. ej. al reiniciar ATAS) re-mide su base en la rueda y estas rayas dejan de coincidir con las suyas: de 16:48 a 17:11 ART quedaron ~13 pts corridas, en varios minutos con otro strike ganador", l, est);

            // --- 4.1.7 (12-10-2026): TOP-N del libro de ES. MUROS/MAJORS marcan UN strike por lado; medido hoy,
            // en la ventana de +-100 pts habia 24 strikes con gamma y se dibujaban 2.
            S("TOPN_ES_vol", "ES top", "ES", "vol", "TOPN", "Recomendadas", "#7cf5c4", 0,
              "Los N strikes de ES con mas |gamma neta| por volumen dentro del radio (min(2 %, 100 pts)), de mayor a menor (D1..DN)",
              "los strikes de ES donde HOY se negocio mas gamma, no solo el mayor: se recalculan cada minuto y se mueven con el libro",
              "SIN VALIDAR como nivel. Es cobertura, no seleccion: marca lo que MUROS y MAJORS dejan afuera (medido 12-10: 24 strikes con gamma, 2 dibujados)", l, est);
            S("TOPN_ES_oi", "ES top OI", "ES", "oi", "TOPN", "Recomendadas", "#4fb98a", 0,
              "Los N strikes de ES con mas |gamma neta| por interes abierto dentro del radio, de mayor a menor (D1..DN)",
              "los strikes de ES con mas posiciones abiertas de ayer, los N mas pesados y no solo el mayor",
              "SIN VALIDAR como nivel; misma logica que TOPN_ES_vol con el OI de la OCC", l, est);

            S("CARGA_ES", "ES", "ES", "oi", "TOPN", "Recomendadas", "#ffb74d", 0,
              "Los strikes de ES con mas INTERES ABIERTO dentro del radio. Horizonte REAL: ES solo los 2 vencimientos suscritos, CBOE hasta 14 dias en vivo y 8 del archivo; sin el contrato sin punta (iv <= 0). Es un piso, no el total de CME",
              "donde hay mas papel puesto cerca del precio: en CME los strikes redondos llevan su carga en 4, 11, 18 y 25 dias, y el calculo normal de la Familia solo mira el vencimiento de hoy",
              "SIN VALIDAR como nivel. Es COBERTURA: marca lo que el horizonte del frente deja afuera", l, est);
            S("CARGA_SPX", "SPX", "SPX", "oi", "TOPN", "Recomendadas", "#ce93d8", 0,
              "Los strikes de SPX con mas INTERES ABIERTO dentro del radio. Horizonte REAL: ES solo los 2 vencimientos suscritos, CBOE hasta 14 dias en vivo y 8 del archivo; sin el contrato sin punta (iv <= 0). Es un piso, no el total de CME",
              "donde hay mas papel puesto cerca del precio: en CME los strikes redondos llevan su carga en 4, 11, 18 y 25 dias, y el calculo normal de la Familia solo mira el vencimiento de hoy",
              "SIN VALIDAR como nivel. Es COBERTURA: marca lo que el horizonte del frente deja afuera", l, est);
            S("CARGA_SPY", "SPY", "SPY", "oi", "TOPN", "Recomendadas", "#80cbc4", 0,
              "Los strikes de SPY con mas INTERES ABIERTO dentro del radio. Horizonte REAL: ES solo los 2 vencimientos suscritos, CBOE hasta 14 dias en vivo y 8 del archivo; sin el contrato sin punta (iv <= 0). Es un piso, no el total de CME",
              "donde hay mas papel puesto cerca del precio: en CME los strikes redondos llevan su carga en 4, 11, 18 y 25 dias, y el calculo normal de la Familia solo mira el vencimiento de hoy",
              "SIN VALIDAR como nivel. Es COBERTURA: marca lo que el horizonte del frente deja afuera", l, est);

            Series = l.AsReadOnly();
            PorId = l.ToDictionary(x => x.Id, StringComparer.Ordinal);
            Estado = est;
            Calc = l.Take(24).Where(x => x.Tipo != "TRES").Select(x => x.Id).Concat(new[] { "TOPN_ES_vol", "TOPN_ES_oi", "CARGA_ES", "CARGA_SPX", "CARGA_SPY" }).ToArray();   // 4.1.7: anexadas AL FINAL (0-20 intactos)
            _idxCalc = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < Calc.Length; i++) _idxCalc[Calc[i]] = i;
        }
    }
}
