// SeriesPantalla.cs — PythiaGex 4.1, modulo pantalla (B5), 08-10-2026. Sin referencias a ATAS.
// Las 29 casillas de la pantalla (una por serie), con los MISMOS nombres de propiedad que el visor 4.0.6 (S_<id>): ATAS guarda cada ajuste
// POR NOMBRE en el workspace, asi el grafico donde estaba el visor conserva lo que eligio el operador. Defaults = los 8 del operador
// (captura 08-10 17:5x). Para cambiar un default ya guardado hay que RENOMBRAR la propiedad (regla del proyecto).
// 4.1.3 (09-10-2026): + 4 casillas NUEVAS (S_R20_SPY_vol, S_R20_SPX_vol, S_DOMS_SPY_vol prendidas; S_DOMS_SPX_vol apagada) en el grupo
// "4b. Dominantes como la 2.0": 33 en total.
// 4.1.4 (09-10-2026): defaults apagados MAJORS_SPY_oi, MUROS_SPY_oi, FAM_MUROS_oi y DOMS_SPY_vol (pedido del operador; sin renombrar: su .ws ya los
// tiene en false); color de "2.0 SPY" #cc8800 (ambar oscuro, entre la D1 #E8C83C y la D2 #E8A838 de la 2.0): el #e8c83c de la 4.1.3 no se
// distinguia del "SPY 0G" #fcd34d (CIEDE2000 3,8; ahora 20,5 y >= 12 contra cualquier serie del catalogo).
// 4.1.5 (09-10-2026): + 1 casilla NUEVA (S_R10_SPX_zero, PRENDIDA: pedido del operador "copiarla ahora para nuestro 4.0") en el grupo NUEVO
// "4c. Como la clasica": la estela 'SPX 0Γ' de la clasica (ClasicaSpx.cs). 34 en total; 7 prendidas por defecto.
// 4.1.5b (09-10-2026): + S_R10_SPX_dom (las D1-D3 de la clasica, PRENDIDA): 35 en total; 8 prendidas por defecto.
// 4.1.5c (09-10-2026): el Display de las S_* (nombre corto, grupo "0. PRENDER / APAGAR" y Order) lo pone herramientas/grupo_arriba_415c.py en
// GammaCincoPantalla.cs; Casilla.Nombre/Grupo/Orden de esta lista quedaron con los de antes (el producto solo usa Id y Default).
// 4.1.5d (09-10-2026): color de R10_SPX_dom #b8f8d8 (el #9dffd2 quedaba a CIEDE2000 11,4 del "2.0 SPX" #3ddc97; ahora 14,3).
// 4.1.5e (09-10-2026): la fuente de verdad del Display pasa a herramientas/grupo_arriba_415e.py (jerarquia de llaves; las S_* siguen arriba, en el
// orden y con el nombre de siempre, y su descripcion dice que no necesitan ninguna llave). Esta lista no cambia.
// 5.0 (27-10-2026): sin TQQQ: fuera las 6 casillas del grupo "5. TQQQ x3", sus 6 series del respaldo y T_MUROS_oi de PrendidasPorDefecto.
// El orden relativo de lo que queda no cambia.
// 5.0 (27-10-2026, P5): esta lista pasa a ser el ESPEJO del dialogo de la 5.0 (GammaCincoPantalla.cs): Nombre = el nombre corto que se ve, Grupo =
// "0. PRENDER / APAGAR (todo lo que se dibuja)" y Orden = su Display.Order (0..34, sin huecos ni repetidos: por libro ES, SPX, SPY, despues la
// familia, la clasica, la 2.0 y la 3.0). Default = el de la propiedad: las 6 que decidio el coordinador (el analogo de las que eligio el operador en
// NQ, sin R20_SPY_vol: la 2.0 no dibuja SPY en un grafico de ES). TOPN_ES_vol y CARGA_ES pasan a APAGADAS (en la 4.1 venian prendidas de fabrica;
// el operador las tiene apagadas en su .ws del 27-10 y no estan en la lista del coordinador). La 5.0 es un tipo NUEVO en ATAS (ningun .ws tiene
// sus S_*): estos defaults son lo que se ve al ponerla. Respaldo: banda de SPY +-5 (era +-20), igual que CatalogoFamilia.
// Ademas: un catalogo de RESPALDO (id, corto, libro, fuente, tipo, grupo, color, banda) por si el motor todavia no publico el suyo
// (IMotorFamilia.Catalogo manda; el arnes compara este respaldo campo por campo contra CatalogoFamilia de B3d).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

namespace PythiaGexCinco.Familia
{
    public static class SeriesPantalla
    {
        /// <summary>Una casilla: id de la serie, nombre visible (el del dialogo de la 5.0), grupo, orden (su Display.Order) y default.</summary>
        public sealed class Casilla
        {
            public string Id, Nombre, Grupo; public int Orden; public bool Default;
            public string Propiedad => "S_" + Id;
        }

        /// <summary>5.0 (P5): el grupo de arriba del dialogo (el mismo de GammaCincoPantalla.cs y GammaHoyTresCasillas.GRUPO_CASILLAS).</summary>
        public const string GRUPO_DIALOGO = "0. PRENDER / APAGAR (todo lo que se dibuja)";

        /// <summary>5.0 (P5): en el orden del dialogo (Orden = Display.Order de la propiedad S_ID). Las propiedades de pantalla (doble eje, llaves,
        /// pestaña) siguen en 35..41 y las de la 3.0 (GammaHoyTres*.cs, paquete P4) desde 42.</summary>
        public static readonly IReadOnlyList<Casilla> Casillas = new[]
        {
            // ---- libro ES (opciones sobre el futuro, Rithmic)
            C("MUROS_ES_vol",   "ES muros vol",   0, false),
            C("MUROS_ES_oi",    "ES muros OI",    1, true),
            C("MAJORS_ES_vol",  "ES majors vol",  2, false),
            C("MAJORS_ES_oi",   "ES majors OI",   3, false),
            C("ZEST_ES_vol",    "ES 0Γ est vol",  4, false),
            C("ZEST_ES_oi",     "ES 0Γ est OI",   5, false),
            C("ZTP_ES_vol",     "ES cruces vol",  6, false),
            C("TOPN_ES_vol",    "ES top-N vol",   7, false),   // 5.0 (P5): apagada (en la 4.1 venia prendida)
            C("TOPN_ES_oi",     "ES top-N OI",    8, false),
            C("CARGA_ES",       "ES carga OI",    9, false),   // 5.0 (P5): apagada (en la 4.1 venia prendida)
            // ---- SPX (CBOE, a ES por la base)
            C("MUROS_SPX_vol",  "SPX muros vol", 10, true),
            C("MUROS_SPX_oi",   "SPX muros OI",  11, false),
            C("MAJORS_SPX_vol", "SPX majors vol", 12, false),
            C("MAJORS_SPX_oi",  "SPX majors OI", 13, false),
            C("ZEST_SPX_vol",   "SPX 0Γ est vol", 14, false),
            C("ZTP_SPX_vol",    "SPX cruces vol", 15, false),
            C("CARGA_SPX",      "SPX carga OI",  16, false),
            // ---- SPY (CBOE, a ES por la razon)
            C("MUROS_SPY_vol",  "SPY muros vol", 17, false),
            C("MUROS_SPY_oi",   "SPY muros OI",  18, false),   // 4.1.4: apagada
            C("MAJORS_SPY_vol", "SPY majors vol", 19, false),
            C("MAJORS_SPY_oi",  "SPY majors OI", 20, false),   // 4.1.4: apagada
            C("ZEST_SPY_vol",   "SPY 0Γ est vol", 21, false),
            C("CARGA_SPY",      "SPY carga OI",  22, false),
            // ---- la familia sumada y la confluencia
            C("FAM_MUROS_vol",  "FAM muros vol", 23, false),
            C("FAM_MUROS_oi",   "FAM muros OI",  24, false),   // 4.1.4: apagada
            C("CONF_vol",       "CONF vol",      25, false),
            // ---- 4.1.5/4.1.5b: como la clasica (en ES: su libro SPX). Propiedades NUEVAS (ningun .ws las tiene: toman estos defaults).
            C("R10_SPX_dom",    "Clas SPX D1-3", 26, true),
            C("R10_SPX_zero",   "Clas SPX 0Γ",   27, true),
            // ---- 4.1.3: como la 2.0 (en ES: su libro SPX; R20_SPY_vol no tiene equivalente en la 2.0 en ES)
            C("R20_SPY_vol",    "2.0 SPY D1-D2", 28, false),   // 5.0 (P5): apagada (la 2.0 no dibuja SPY en ES)
            C("R20_SPX_vol",    "2.0 SPX D1-D2", 29, true),
            C("DOMS_SPY_vol",   "SPY dom (2.0)", 30, false),   // 4.1.4: apagada
            C("DOMS_SPX_vol",   "SPX dom (2.0)", 31, false),
            // ---- lo que dibuja el motor 3.0 que corre adentro
            C("TRES_ES",        "3.0 ES rayas",  32, false),
            C("TRES_SPX",       "3.0 SPX rayas", 33, true),
            C("TRES_SPY",       "3.0 SPY rayas", 34, false),
        };

        /// <summary>4.1.3: grupo de las casillas "como la 2.0" en la 4.1 (historico: desde la 4.1.5c todas van en el grupo de arriba).</summary>
        public const string GRUPO_20 = "4b. Dominantes como la 2.0";
        /// <summary>4.1.5: grupo de la casilla "como la clasica" en la 4.1 (historico; 5.0: sin TQQQ).</summary>
        public const string GRUPO_CLASICA = "4c. Como la clasica";

        /// <summary>Las prendidas por defecto (mismo orden que CatalogoFamilia.PrendidasPorDefecto). Historia en NQ: las 8 del operador del 08-10, + 4.1.3
        /// 2.0 SPY/SPX y SPY dom, - 4.1.4 ("suman ruido al ser ya superadas por la formula de la 2.0 spy") SPY majors OI, SPY muros OI, FAM muros OI y SPY
        /// dom, + 4.1.5 Clasica SPX 0Γ, + 4.1.5b Clasica SPX D1-D3; 5.0: sin TQQQ. 5.0 (P5, decision del coordinador 27-10): sin 2.0 SPY (la 2.0 no dibuja
        /// SPY en un grafico de ES). Quedan 6.</summary>
        public static readonly string[] PrendidasPorDefecto = { "MUROS_ES_oi", "MUROS_SPX_vol", "TRES_SPX",
                                                                "R20_SPX_vol", "R10_SPX_zero", "R10_SPX_dom" };   // 5.0 (P5): sin R20_SPY_vol

        private static Casilla C(string id, string nombre, int orden, bool def) => new Casilla { Id = id, Nombre = nombre, Grupo = GRUPO_DIALOGO, Orden = orden, Default = def };

        // ------------------------------------------------------------------ catalogo de respaldo (los mismos valores que CatalogoFamilia de B3d)
        /// <summary>Respaldo de dibujo si el motor no paso su catalogo. Mismo orden que la vista previa (24) + carga y top-N + como la 2.0 (4) + la clasica (2)
        /// (5.0: sin las 6 de TQQQ; 5.0 P5: banda de SPY +-5).</summary>
        public static readonly IReadOnlyList<SerieInfo> Respaldo = new[]
        {
            R("MUROS_ES_vol", "ES", "ES", "vol", "MUROS", "Recomendadas", "#4db5e4", 0),
            R("MAJORS_SPY_oi", "SPY", "SPY", "oi", "MAJORS", "Recomendadas", "#e2b872", 5),    // 5.0 (P5): +-5 (era +-20)
            R("CONF_vol", "CONF", "familia", "vol", "CONF", "Recomendadas", "#f3d35a", 0),
            R("MAJORS_ES_vol", "ES", "ES", "vol", "MAJORS", "Recomendadas", "#6cc3ff", 0),
            R("MUROS_ES_oi", "ES", "ES", "oi", "MUROS", "Familia", "#9a98d6", 0),
            R("MUROS_SPX_vol", "SPX", "SPX", "vol", "MUROS", "Familia", "#2fb3a8", 0),
            R("MUROS_SPX_oi", "SPX", "SPX", "oi", "MUROS", "Familia", "#8de031", 0),
            R("MUROS_SPY_vol", "SPY", "SPY", "vol", "MUROS", "Familia", "#dabe6f", 5),        // 5.0 (P5): +-5
            R("MUROS_SPY_oi", "SPY", "SPY", "oi", "MUROS", "Familia", "#ff9f43", 5),          // 5.0 (P5): +-5
            R("MAJORS_ES_oi", "ES", "ES", "oi", "MAJORS", "Familia", "#b9a4ff", 0),
            R("MAJORS_SPX_vol", "SPX", "SPX", "vol", "MAJORS", "Familia", "#4fd1c5", 0),
            R("MAJORS_SPX_oi", "SPX", "SPX", "oi", "MAJORS", "Familia", "#a3e635", 0),
            R("MAJORS_SPY_vol", "SPY", "SPY", "vol", "MAJORS", "Familia", "#f6c177", 5),      // 5.0 (P5): +-5
            R("FAM_MUROS_vol", "FAM", "familia", "vol", "MUROS", "Familia", "#d04ad0", 0),
            R("FAM_MUROS_oi", "FAM", "familia", "oi", "MUROS", "Familia", "#e879f9", 0),
            R("ZEST_ES_vol", "ES", "ES", "vol", "ZEST", "Zero", "#c9d4e3", 0),
            R("ZEST_ES_oi", "ES", "ES", "oi", "ZEST", "Zero", "#94a3b8", 0),
            R("ZEST_SPX_vol", "SPX", "SPX", "vol", "ZEST", "Zero", "#7dd3fc", 0),
            R("ZEST_SPY_vol", "SPY", "SPY", "vol", "ZEST", "Zero", "#fcd34d", 0),
            R("ZTP_ES_vol", "ES", "ES", "vol", "ZTP", "Zero", "#f472b6", 0),
            R("ZTP_SPX_vol", "SPX", "SPX", "vol", "ZTP", "Zero", "#fb7185", 0),
            R("TRES_ES", "3.0", "ES", "3.0", "TRES", "3.0", "#8a96a8", 0),
            R("TRES_SPX", "3.0 SPX", "SPX", "3.0", "TRES", "3.0", "#5f6b7d", 0),
            R("TRES_SPY", "3.0 SPY", "SPY", "3.0", "TRES", "3.0", "#7c6f57", 0),
            R("CARGA_ES", "ES", "ES", "oi", "TOPN", "Recomendadas", "#ffb74d", 0),
            R("CARGA_SPX", "SPX", "SPX", "oi", "TOPN", "Recomendadas", "#ce93d8", 0),
            R("CARGA_SPY", "SPY", "SPY", "oi", "TOPN", "Recomendadas", "#80cbc4", 0),
            R("TOPN_ES_vol", "ES", "ES", "vol", "TOPN", "Recomendadas", "#7cf5c4", 0),
            R("TOPN_ES_oi", "ES", "ES", "oi", "TOPN", "Recomendadas", "#4fb98a", 0),
            R("R20_SPY_vol", "2.0 SPY", "SPY", "vol", "DOMS", "2.0", "#cc8800", 0),     // 4.1.4: ambar oscuro (antes #e8c83c, igual al SPY 0G)
            R("R20_SPX_vol", "2.0 SPX", "SPX", "vol", "DOMS", "2.0", "#3ddc97", 0),
            R("DOMS_SPY_vol", "SPY dom", "SPY", "vol", "DOMS", "2.0", "#5b8cff", 0),
            R("DOMS_SPX_vol", "SPX dom", "SPX", "vol", "DOMS", "2.0", "#a9c4ff", 0),
            R("R10_SPX_zero", "Clasica SPX 0Γ", "SPX", "vol", "ZEST", "Clasica", "#ffffff", 0),   // 4.1.5
            R("R10_SPX_dom", "Clasica SPX", "SPX", "vol", "DOMS", "Clasica", "#b8f8d8", 0),       // 4.1.5b; 4.1.5d: #b8f8d8 (antes #9dffd2, a 11,4 del 2.0 SPX)
        };

        private static SerieInfo R(string id, string corto, string libro, string fuente, string tipo, string grupo, string color, double banda)
            => new SerieInfo { Id = id, Corto = corto, Libro = libro, Fuente = fuente, Tipo = tipo, Grupo = grupo, ColorHex = color, Banda = banda, Tecnico = "", Criollo = "" };

        /// <summary>"#rrggbb" -> Color; gris si no se entiende (nunca tira).</summary>
        public static Color ColorDe(string hex)
        {
            try
            {
                if (!string.IsNullOrEmpty(hex) && hex.Length == 7 && hex[0] == '#')
                    return Color.FromArgb(int.Parse(hex.Substring(1, 2), NumberStyles.HexNumber), int.Parse(hex.Substring(3, 2), NumberStyles.HexNumber),
                                          int.Parse(hex.Substring(5, 2), NumberStyles.HexNumber));
            }
            catch { }
            return Color.FromArgb(200, 200, 200);
        }
    }
}
