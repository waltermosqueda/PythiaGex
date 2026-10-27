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
        /// <summary>Una casilla: id de la serie, nombre visible (el de la 4.0.6), grupo, orden y default.</summary>
        public sealed class Casilla
        {
            public string Id, Nombre, Grupo; public int Orden; public bool Default;
            public string Propiedad => "S_" + Id;
        }

        public static readonly IReadOnlyList<Casilla> Casillas = new[]
        {
            C("MUROS_ES_vol",   "ES muros vol",                 "1. Recomendadas", 10, false),
            C("MAJORS_SPY_oi",  "SPY majors OI",                "1. Recomendadas", 20, false),   // 4.1.4: apagada
            C("CONF_vol",       "CONF vol (confluencia)",       "1. Recomendadas", 30, false),
            C("MAJORS_ES_vol",  "ES majors vol",                "1. Recomendadas", 40, false),
            C("MUROS_ES_oi",    "ES muros OI",                  "2. Familia", 10, true),
            C("MUROS_SPX_vol",  "SPX muros vol",                "2. Familia", 20, true),
            C("MUROS_SPX_oi",   "SPX muros OI",                 "2. Familia", 30, false),
            C("MUROS_SPY_vol",  "SPY muros vol",                "2. Familia", 40, false),
            C("MUROS_SPY_oi",   "SPY muros OI",                 "2. Familia", 50, false),   // 4.1.4: apagada
            C("MAJORS_ES_oi",   "ES majors OI",                 "2. Familia", 60, false),
            C("MAJORS_SPX_vol", "SPX majors vol",               "2. Familia", 70, false),
            C("MAJORS_SPX_oi",  "SPX majors OI",                "2. Familia", 80, false),
            C("MAJORS_SPY_vol", "SPY majors vol",               "2. Familia", 90, false),
            C("FAM_MUROS_vol",  "FAM muros vol (familia sumada)", "2. Familia", 100, false),
            C("FAM_MUROS_oi",   "FAM muros OI (familia sumada)",  "2. Familia", 110, false),   // 4.1.4: apagada
            C("ZEST_ES_vol",    "ES zero est. vol",             "3. Zero", 10, false),
            C("ZEST_ES_oi",     "ES zero est. OI",              "3. Zero", 20, false),
            C("ZEST_SPX_vol",   "SPX zero est. vol",            "3. Zero", 30, false),
            C("ZEST_SPY_vol",   "SPY zero est. vol",            "3. Zero", 40, false),
            C("ZTP_ES_vol",     "ES cruces vol",                "3. Zero", 50, false),
            C("ZTP_SPX_vol",    "SPX cruces vol",               "3. Zero", 60, false),
            C("TRES_ES",        "3.0 (ES)",                     "4. Lo que dibujo la 3.0", 10, false),
            C("TRES_SPX",       "3.0 SPX",                      "4. Lo que dibujo la 3.0", 20, true),
            C("TRES_SPY",       "3.0 SPY",                      "4. Lo que dibujo la 3.0", 30, false),
            C("T_DOMS_vol",     "TQQQ dominantes vol",          "5. TQQQ x3", 10, false),
            C("T_MUROS_vol",    "TQQQ muros vol",               "5. TQQQ x3", 20, false),
            C("T_MUROS_oi",     "TQQQ muros OI",                "5. TQQQ x3", 30, true),
            C("CARGA_ES",       "ES carga (strikes abiertos)",       "1. ES", 9, true),
            C("CARGA_SPX",      "SPX carga (strikes abiertos)",      "2. SPX", 9, false),
            C("CARGA_SPY",      "SPY carga (strikes abiertos)",      "3. SPY", 9, false),
            C("TOPN_ES_vol",    "ES top-N vol",                 "1. ES", 7, true),
            C("TOPN_ES_oi",     "ES top-N OI",                  "1. ES", 8, false),
            C("T_ZERO_oi",      "TQQQ zero OI",                 "5. TQQQ x3", 40, false),
            C("T_ZERO_vol_pm",  "TQQQ 0Γ vol punto medio",      "5. TQQQ x3", 41, true),
            C("T_DOMS_raz",     "TQQQ razon (como Gammalito, SOLO para comparar)", "5. TQQQ x3", 50, false),
            // 4.1.3 (09-10-2026): las dominantes como la 2.0. Propiedades NUEVAS (ningun .ws las tiene: toman estos defaults).
            C("R20_SPY_vol",    "2.0 SPY (dominantes y conversion de la 2.0)",   GRUPO_20, 10, true),
            C("R20_SPX_vol",    "2.0 SPX (capa SPX de la 2.0)",                  GRUPO_20, 20, true),
            C("DOMS_SPY_vol",   "SPY dom (seleccion 2.0, razon sincronizada)",   GRUPO_20, 30, false),   // 4.1.4: apagada
            C("DOMS_SPX_vol",   "SPX dom (seleccion 2.0, base sincronizada; sin medir)", GRUPO_20, 40, false),
            // 4.1.5 (09-10-2026): como la clasica. Propiedad NUEVA (ningun .ws la tiene: toma este default).
            C("R10_SPX_zero",   "Clasica SPX 0Γ (zero de SPX con la base de la rueda anterior, como la clasica)", GRUPO_CLASICA, 10, true),
            C("R10_SPX_dom",    "Clasica SPX D1-D3 (dominantes de la capa SPX con la base de la rueda anterior, como la clasica)", GRUPO_CLASICA, 20, true),   // 4.1.5b
        };

        /// <summary>4.1.3: grupo de las casillas "como la 2.0" (en ATAS va entre "4. Lo que dibujo la 3.0" y "5. TQQQ x3").</summary>
        public const string GRUPO_20 = "4b. Dominantes como la 2.0";
        /// <summary>4.1.5: grupo de la casilla "como la clasica" (en ATAS va entre "4b. Dominantes como la 2.0" y "5. TQQQ x3").</summary>
        public const string GRUPO_CLASICA = "4c. Como la clasica";

        /// <summary>Las prendidas por defecto (mismo orden que CatalogoFamilia.PrendidasPorDefecto): las 8 del operador del 08-10 + (4.1.3) 2.0 SPY,
        /// 2.0 SPX y SPY dom, MENOS (4.1.4, pedido del operador 09-10: "suman ruido al ser ya superadas por la formula de la 2.0 spy") SPY majors OI,
        /// SPY muros OI, FAM muros OI y SPY dom (SPY muros vol ya estaba apagada). Quedaban 6; 4.1.5: + Clasica SPX 0Γ (pedido del operador); 4.1.5b: + Clasica
        /// SPX D1-D3. Quedan 8.</summary>
        public static readonly string[] PrendidasPorDefecto = { "MUROS_ES_oi", "MUROS_SPX_vol", "TRES_SPX", "T_MUROS_oi",
                                                                "R20_SPY_vol", "R20_SPX_vol", "R10_SPX_zero", "R10_SPX_dom" };   // 4.1.5b: + Clasica SPX D1-D3

        private static Casilla C(string id, string nombre, string grupo, int orden, bool def) => new Casilla { Id = id, Nombre = nombre, Grupo = grupo, Orden = orden, Default = def };

        // ------------------------------------------------------------------ catalogo de respaldo (los mismos valores que CatalogoFamilia de B3d)
        /// <summary>Respaldo de dibujo si el motor no paso su catalogo. Mismo orden que la vista previa (24) + TQQQ (5) + como la 2.0 (4) + la clasica (1).</summary>
        public static readonly IReadOnlyList<SerieInfo> Respaldo = new[]
        {
            R("MUROS_ES_vol", "ES", "ES", "vol", "MUROS", "Recomendadas", "#4db5e4", 0),
            R("MAJORS_SPY_oi", "SPY", "SPY", "oi", "MAJORS", "Recomendadas", "#e2b872", 20),
            R("CONF_vol", "CONF", "familia", "vol", "CONF", "Recomendadas", "#f3d35a", 0),
            R("MAJORS_ES_vol", "ES", "ES", "vol", "MAJORS", "Recomendadas", "#6cc3ff", 0),
            R("MUROS_ES_oi", "ES", "ES", "oi", "MUROS", "Familia", "#9a98d6", 0),
            R("MUROS_SPX_vol", "SPX", "SPX", "vol", "MUROS", "Familia", "#2fb3a8", 0),
            R("MUROS_SPX_oi", "SPX", "SPX", "oi", "MUROS", "Familia", "#8de031", 0),
            R("MUROS_SPY_vol", "SPY", "SPY", "vol", "MUROS", "Familia", "#dabe6f", 20),
            R("MUROS_SPY_oi", "SPY", "SPY", "oi", "MUROS", "Familia", "#ff9f43", 20),
            R("MAJORS_ES_oi", "ES", "ES", "oi", "MAJORS", "Familia", "#b9a4ff", 0),
            R("MAJORS_SPX_vol", "SPX", "SPX", "vol", "MAJORS", "Familia", "#4fd1c5", 0),
            R("MAJORS_SPX_oi", "SPX", "SPX", "oi", "MAJORS", "Familia", "#a3e635", 0),
            R("MAJORS_SPY_vol", "SPY", "SPY", "vol", "MAJORS", "Familia", "#f6c177", 20),
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
            R("T_DOMS_vol", "TQQQ", "TQQQ", "vol", "DOMS", "TQQQ x3", "#ffd54f", 4.0),
            R("T_MUROS_vol", "TQQQ", "TQQQ", "vol", "MUROS", "TQQQ x3", "#26c6da", 4.0),
            R("T_MUROS_oi", "TQQQ", "TQQQ", "oi", "MUROS", "TQQQ x3", "#ba68c8", 4.0),
            R("CARGA_ES", "ES", "ES", "oi", "TOPN", "Recomendadas", "#ffb74d", 0),
            R("CARGA_SPX", "SPX", "SPX", "oi", "TOPN", "Recomendadas", "#ce93d8", 0),
            R("CARGA_SPY", "SPY", "SPY", "oi", "TOPN", "Recomendadas", "#80cbc4", 0),
            R("TOPN_ES_vol", "ES", "ES", "vol", "TOPN", "Recomendadas", "#7cf5c4", 0),
            R("TOPN_ES_oi", "ES", "ES", "oi", "TOPN", "Recomendadas", "#4fb98a", 0),
            R("T_ZERO_oi", "TQQQ", "TQQQ", "oi", "ZTP", "TQQQ x3", "#b0bec5", 4.0),
            R("T_ZERO_vol_pm", "TQQQ", "TQQQ", "vol", "ZTP", "TQQQ x3", "#e8eaed", 4.0),
            R("T_DOMS_raz", "TQQQ", "TQQQ", "vol", "RAZON", "TQQQ x3", "#ff7043", 0),
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
