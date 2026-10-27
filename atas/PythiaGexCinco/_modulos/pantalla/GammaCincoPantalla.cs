// GammaCincoPantalla.cs — PythiaGex 4.1, modulo pantalla (B5), 08-10-2026. Parte de la clase del indicador (partial GammaCinco).
// Las propiedades de la pantalla con los MISMOS nombres, tipos y defaults que el visor 4.0.6 (atas/PythiaGexCinco/_visor_4_0_6/GammaCinco.cs):
// ATAS guarda cada ajuste POR NOMBRE en el workspace, asi el grafico donde estaba el visor conserva lo que eligio el operador
// (29 casillas S_<id>, Eje4Libro, Estela4, Rotulos4, Cabecera4, Letra4, Recuadro4Abierto, MargenSup4). Para cambiar un default ya
// guardado hay que RENOMBRAR la propiedad (regla del proyecto).
// Carpeta4 (la carpeta de los json del visor) queda OCULTA y sin efecto: todo se calcula adentro. Se conserva solo para que el valor
// guardado en el .ws tenga donde caer (que ATAS ignore una propiedad desconocida esta SIN VERIFICAR).
// 4.1.2: tres ajustes NUEVOS en "6. Pantalla" (Monto41Rotulos, Cambio41Rotulos, Cambio41Ventana): nombres que ningun .ws tiene.
// 4.1.3: cuatro casillas NUEVAS en "4b. Dominantes como la 2.0" (S_R20_SPY_vol, S_R20_SPX_vol, S_DOMS_SPY_vol prendidas; S_DOMS_SPX_vol apagada).
// 4.1.4 (09-10-2026): ajuste NUEVO Tramos41Rotulos ("Rotulos de los tramos de historia", prendido) en "6. Pantalla". DEFAULTS apagados por pedido
// del operador ("suman ruido al ser ya superadas por la formula de la 2.0 spy"): S_MAJORS_SPY_oi, S_MUROS_SPY_oi, S_FAM_MUROS_oi y S_DOMS_SPY_vol
// (S_MUROS_SPY_vol ya lo estaba). SIN renombrar: su workspace ya las tiene guardadas en false; el default nuevo solo vale para graficos nuevos.
// 4.1.5 (09-10-2026): casilla NUEVA S_R10_SPX_zero ("Clasica SPX 0Γ", PRENDIDA por pedido del operador) en el grupo NUEVO "4c. Como la clasica".
// 4.1.5b: casilla NUEVA S_R10_SPX_dom ("Clasica SPX D1-D3", prendida). 4.1.5c: todas las casillas de dibujo en el grupo "0. PRENDER / APAGAR"
// (herramientas/grupo_arriba_415c.py; solo [Display]). 4.1.5d (revision 4.1.5b/c): solo cambian las DESCRIPCIONES de S_R10_SPX_zero y S_R10_SPX_dom
// (la salvedad medida el 09-10: si la clasica pierde Rithmic re-mide su base y la replica deja de coincidir); nombres, tipos y defaults iguales.
// Revision 4.1.4: solo cambia el texto de la descripcion de Tramos41Rotulos (largo minimo tambien en tiempo, la franja de arriba); nombre, tipo y
// default iguales.
// 5.0 (27-10-2026): sin TQQQ. Fuera las 6 casillas S_T_* (Order 18-23 del grupo "0. PRENDER / APAGAR"); las de abajo corren 5 lugares para que
// el Order quede sin huecos (mismo orden relativo). Eje4Libro: Ninguno/SPX/SPY, default SPX (el TQQQ de la 4.1 ya no existe). Descripciones:
// "PythiaGex 5.0" y "la 5.0" donde decian 4.0 / 4.1 como nombre del producto.
// 5.0 (27-10-2026, P5): el grupo de arriba para ES. Solo cambia el [Display] (nombre, Order, descripcion) y el default de 4 casillas; NINGUNA
// propiedad se renombra ni cambia de tipo (la 5.0 es un tipo nuevo en ATAS: ningun .ws tiene sus ajustes, los defaults son lo que se ve).
//   * ORDER 0..41 SIN HUECOS NI REPETIDOS (en la 4.1 las 5 de top-N/carga repetian 7-11 con las de NDX): ES 0-9, SPX 10-16, SPY 17-22, familia
//     23-25, clasica 26-27, 2.0 28-31, 3.0 32-34, doble eje 35, 'Estela▸' 36 (+ '↳ rot. tramos' 37), 'Etiquetas▸' 38 (+ '↳ montos' 39 y
//     '↳ cambios ▲▼' 40), la pestaña 41. Lo de la 3.0 (GammaHoyTres*.cs, paquete P4) tiene que empezar en 42 (hoy empieza en 37: +5).
//   * NOMBRES cortos con lo que distingue al principio, medidos con el metodo de grupo_arriba_415e.py --anchos (Segoe UI 12; entran hasta 78,9):
//     los 3 de carga pasan a "ES/SPX/SPY carga OI" (eran 27-28 letras), "Clas SPX D1-3" (82,8 -> 74,4), "SPY/SPX dom (2.0)" (80,5/81,0 -> 73,5/
//     74,0), "Pestaña 5.0" (era "Pestaña de detalle", 96,7 -> 60,0). El resto mide <= 78,1 salvo "FAM muros vol" (80,1, igual que en la 4.1).
//   * DESCRIPCIONES: la primera frase dice que llave necesitan (estilo 4.1.5e); despues el nombre tecnico de los tableros, el criollo, como llega
//     a ES (base o razon) y el estado: TODAS "SIN MEDIR EN ES"; lo medido en NQ va entre parentesis como dato de OTRO producto.
//   * DEFAULTS (decision del coordinador 27-10): prendidas MUROS_ES_oi, MUROS_SPX_vol, TRES_SPX, R20_SPX_vol, R10_SPX_zero y R10_SPX_dom.
//     Cambian contra el clon: S_R20_SPY_vol -> false (la 2.0 no dibuja SPY en un grafico de ES), S_TOPN_ES_vol y S_CARGA_ES -> false (en la 4.1
//     venian prendidas; el operador las tiene apagadas en su .ws del 27-10). Eje4Libro sigue en SPX (ver ArmadoPantalla y _diseno/P5_PANTALLA.md).
// Ademas implementa IGraficoPantalla (explicito) porque ChartArea, GetCandle, FirstVisibleBarNumber, InstrumentInfo y RedrawChart son
// protegidos en Indicator. Ganchos para el integrador (B1 ya dejo las marcas GANCHO-4.1 pantalla):
//   OnRender, al final y siempre:   _pantalla.Pintar(g, <motor>.Foto);          (Pintar nunca tira)
//   ProcessMouseClick, lo primero:  if (_pantalla.Clic(e.X, e.Y)) return true;  (Clic ya pide RedrawChart)
//   al configurar el motor:         _pantalla.Catalogo = <motor>.Catalogo;      (opcional: sin eso usa el respaldo, mismos valores)
//   OnInitialize (log de arranque): AjustesPantalla4Texto()
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Threading;

using ATAS.Indicators;
using PythiaGexCinco.Familia;

namespace PythiaGexCinco
{
    public partial class GammaCinco : IGraficoPantalla
    {
        // ------------------------------------------------------------------ una casilla por serie (nombres = los de la pagina y del visor 4.0.6)
        // OJO: ATAS guarda cada ajuste POR NOMBRE en el workspace. Para cambiar un default ya guardado hay que RENOMBRAR la propiedad.
        // 5.0 (P5): las frases de siempre de las series (estilo 4.1.5e) y las conversiones a ES (las mismas del catalogo: CatalogoFamilia.CONV_*).
        private const string G0 = "0. PRENDER / APAGAR (todo lo que se dibuja)";
        private const string SERIE = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). ";
        private const string SPX_A_ES = CatalogoFamilia.CONV_SPX;
        private const string SPY_A_ES = CatalogoFamilia.CONV_SPY;

        // ---- libro ES: opciones sobre el futuro ES de CME, por Rithmic, en vivo (Order 0-9)
        [Display(Name = "ES muros vol", GroupName = G0, Order = 0, Description = SERIE + "Call Wall / Put Wall del 0DTE de ES por volumen (opciones sobre el futuro ES de CME, por Rithmic, en vivo): el strike donde HOY se negocio mas gamma de calls (muro C) y el de mas gamma de puts (muro P), dentro del radio de la Familia; los dos pueden caer del mismo lado del precio. Estado: SIN MEDIR EN ES (en NQ, otro producto: candidata principal de noche, sin validar: punta de mecha 60,6 % contra 50,9 % del azar en 20 sesiones, Holm 0,135).")]
        public bool S_MUROS_ES_vol { get; set; } = false;
        [Display(Name = "ES muros OI", GroupName = G0, Order = 1, Description = SERIE + "Call Wall / Put Wall del 0DTE de ES por interes abierto: donde estan las posiciones abiertas de ayer (el strike con mas gamma de calls y el de mas gamma de puts). El OI lo publica CME una vez por noche; con el OI de anteayer no se dibuja. Estado: SIN MEDIR EN ES (en NQ, otro producto: sin ventaja medida, rebote 68,1 % contra 64,0 % del azar, Holm 1,0).")]
        public bool S_MUROS_ES_oi { get; set; } = true;
        [Display(Name = "ES majors vol", GroupName = G0, Order = 2, Description = SERIE + "Major Positive / Major Negative del 0DTE de ES por volumen: el strike con mas gamma neta de hoy, positiva (M+) y negativa (M-), dentro del radio de la Familia. Estado: SIN MEDIR EN ES (en NQ, otro producto: de noche, sin validar: punta de mecha 60,4 % contra 50,9 % del azar, Holm 0,117).")]
        public bool S_MAJORS_ES_vol { get; set; } = false;
        [Display(Name = "ES majors OI", GroupName = G0, Order = 3, Description = SERIE + "Major Positive / Major Negative del 0DTE de ES por interes abierto: la gamma neta de las posiciones de ayer (con el OI de anteayer no se dibuja). Estado: SIN MEDIR EN ES (en NQ, otro producto: sin ventaja medida).")]
        public bool S_MAJORS_ES_oi { get; set; } = false;
        [Display(Name = "ES 0Γ est vol", GroupName = G0, Order = 4, Description = SERIE + "Zero Gamma (Gamma Flip) estandar de ES por volumen: el precio donde la gamma total del libro, reevaluada a cada precio, pasa de positiva a negativa. Describe el regimen (de un lado el movimiento se amortigua, del otro se acelera), nunca la direccion. No es el '0Γ' de la 3.0 (ese es un cruce por strike). Estado: SIN MEDIR EN ES (en NQ, otro producto: quedaba lejos, casi no saltaba y rebotaba igual que el azar).")]
        public bool S_ZEST_ES_vol { get; set; } = false;
        [Display(Name = "ES 0Γ est OI", GroupName = G0, Order = 5, Description = SERIE + "Zero Gamma estandar de ES por interes abierto: lo mismo con las posiciones de ayer (con el OI de anteayer no se dibuja). Estado: SIN MEDIR EN ES (en NQ, otro producto: 53 toques en 20 sesiones, no alcanzaba para medir).")]
        public bool S_ZEST_ES_oi { get; set; } = false;
        [Display(Name = "ES cruces vol", GroupName = G0, Order = 6, Description = SERIE + "Cruces de signo de la gamma por strike, el mas cercano arriba y abajo del precio, ES por volumen, sin los strikes 'isla' (signo distinto a sus dos vecinos y menos del 10 % del menor de ellos) y sin sostener cruces muertos: el tunel de la 3.0, limpio. Estado: SIN MEDIR EN ES (en NQ, otro producto: NO es muro, el tunel no retenia mas que uno puesto al azar: vuelve 41,3 % contra 43,6 %).")]
        public bool S_ZTP_ES_vol { get; set; } = false;
        [Display(Name = "ES top-N vol", GroupName = G0, Order = 7, Description = SERIE + "Los 6 strikes de ES con mas |gamma neta| por VOLUMEN dentro del radio de la Familia, de mayor a menor (D1..D6): existe porque muros y majors marcan UN strike por lado. Se recalculan cada minuto. Apagada por defecto (en la 4.1 venia prendida). Estado: SIN MEDIR EN ES (en NQ, otro producto: sin validar como nivel; es cobertura, no seleccion).")]
        public bool S_TOPN_ES_vol { get; set; } = false;   // 5.0 (P5): apagada (en la 4.1 venia prendida)
        [Display(Name = "ES top-N OI", GroupName = G0, Order = 8, Description = SERIE + "Lo mismo que 'ES top-N vol' con el interes abierto de ayer (lo publica CME de noche; con el OI de anteayer no se dibuja). Estado: SIN MEDIR EN ES (en NQ, otro producto: sin validar como nivel).")]
        public bool S_TOPN_ES_oi { get; set; } = false;
        [Display(Name = "ES carga OI", GroupName = G0, Order = 9, Description = SERIE + "Carga: los strikes de ES con mas INTERES ABIERTO dentro del radio, sumando los vencimientos que se ven, con 'C x P y' (contratos de calls y de puts). OJO, horizonte REAL: el libro ES suscribe solo los 2 vencimientos mas cercanos (CadenaApi.Vencimientos = 2) y no entra el contrato sin punta (iv <= 0): el 'C x P y' es un PISO, no el total de CME. Existe porque el resto de la Familia mira solo el vencimiento de hoy. Apagada por defecto (en la 4.1 venia prendida). Estado: SIN MEDIR EN ES (en NQ, otro producto: sin validar como nivel; es cobertura).")]
        public bool S_CARGA_ES { get; set; } = false;   // 5.0 (P5): apagada (en la 4.1 venia prendida)

        // ---- SPX: opciones del indice SPX de CBOE (15 min tarde), a ES por la base (Order 10-16)
        [Display(Name = "SPX muros vol", GroupName = G0, Order = 10, Description = SERIE + "Call Wall / Put Wall de SPX por volumen (opciones del indice SPX de CBOE, 15 min tarde): el strike de SPX con mas gamma de calls de hoy y el de mas gamma de puts, " + SPX_A_ES + ". Estado: SIN MEDIR EN ES (en NQ, con NDX: sin ventaja medida).")]
        public bool S_MUROS_SPX_vol { get; set; } = true;
        [Display(Name = "SPX muros OI", GroupName = G0, Order = 11, Description = SERIE + "Call Wall / Put Wall de SPX por interes abierto: las posiciones abiertas de ayer en SPX (las publica la OCC antes de la apertura), " + SPX_A_ES + ". Estado: SIN MEDIR EN ES (en NQ, con NDX: sin ventaja medida).")]
        public bool S_MUROS_SPX_oi { get; set; } = false;
        [Display(Name = "SPX majors vol", GroupName = G0, Order = 12, Description = SERIE + "Major Positive / Major Negative de SPX por volumen: la gamma neta de hoy en SPX, " + SPX_A_ES + ". Estado: SIN MEDIR EN ES (en NQ, con NDX: sin ventaja medida).")]
        public bool S_MAJORS_SPX_vol { get; set; } = false;
        [Display(Name = "SPX majors OI", GroupName = G0, Order = 13, Description = SERIE + "Major Positive / Major Negative de SPX por interes abierto: la gamma neta de las posiciones de ayer en SPX, " + SPX_A_ES + ". Estado: SIN MEDIR EN ES (en NQ, con NDX: sin ventaja medida).")]
        public bool S_MAJORS_SPX_oi { get; set; } = false;
        [Display(Name = "SPX 0Γ est vol", GroupName = G0, Order = 14, Description = SERIE + "Zero Gamma estandar de SPX por volumen: el zero del indice (la gamma de toda la cadena reevaluada a cada precio), " + SPX_A_ES + ". Estado: SIN MEDIR EN ES (en NQ, con NDX: sin ventaja medida).")]
        public bool S_ZEST_SPX_vol { get; set; } = false;
        [Display(Name = "SPX cruces vol", GroupName = G0, Order = 15, Description = SERIE + "Cruces de signo limpios de SPX por volumen: el cruce de SPX mas cercano arriba y abajo, sin islas, " + SPX_A_ES + ". Estado: SIN MEDIR EN ES (en NQ, con NDX: NO es muro).")]
        public bool S_ZTP_SPX_vol { get; set; } = false;
        [Display(Name = "SPX carga OI", GroupName = G0, Order = 16, Description = SERIE + "Carga: los strikes de SPX con mas INTERES ABIERTO dentro del radio; la etiqueta lleva el strike de SPX (el que se cruza contra CBOE) y la raya va " + SPX_A_ES + ". OJO, horizonte REAL: CBOE hasta 14 dias en vivo y 8 desde el archivo, strikes a +-5 % del spot, sin el contrato sin punta (iv <= 0): es un PISO, no el total de la cadena. Estado: SIN MEDIR EN ES (en NQ, con NDX: sin validar como nivel; es cobertura).")]
        public bool S_CARGA_SPX { get; set; } = false;

        // ---- SPY: el ETF SPY de CBOE (15 min tarde), a ES por la razon; banda +-5 = medio strike de SPY (Order 17-22)
        [Display(Name = "SPY muros vol", GroupName = G0, Order = 17, Description = SERIE + "Call Wall / Put Wall de SPY por volumen (el ETF SPY de CBOE, 15 min tarde; banda +-5): el strike de SPY con mas gamma de calls de hoy y el de mas gamma de puts, " + SPY_A_ES + "; la banda +-5 es medio strike de SPY. Estado: SIN MEDIR EN ES (en NQ, con QQQ: sin ventaja medida).")]
        public bool S_MUROS_SPY_vol { get; set; } = false;
        [Display(Name = "SPY muros OI", GroupName = G0, Order = 18, Description = SERIE + "Call Wall / Put Wall de SPY por interes abierto (banda +-5): las posiciones abiertas de ayer en SPY, " + SPY_A_ES + ". Estado: SIN MEDIR EN ES (en NQ, con QQQ: sin ventaja medida, rebote 68,0 % contra 63,9 % del azar, Holm 1,0).")]
        public bool S_MUROS_SPY_oi { get; set; } = false;   // 4.1.4: apagada
        [Display(Name = "SPY majors vol", GroupName = G0, Order = 19, Description = SERIE + "Major Positive / Major Negative de SPY por volumen (banda +-5): la gamma neta de hoy en SPY, " + SPY_A_ES + ". Estado: SIN MEDIR EN ES (en NQ, con QQQ: sin ventaja medida).")]
        public bool S_MAJORS_SPY_vol { get; set; } = false;
        [Display(Name = "SPY majors OI", GroupName = G0, Order = 20, Description = SERIE + "Major Positive / Major Negative de SPY por interes abierto (banda +-5): la gamma neta de las posiciones de ayer en SPY, " + SPY_A_ES + ". Estado: SIN MEDIR EN ES (en NQ, con QQQ: opcional de noche, sin validar: rebote 68,2 % contra 63,9 % del azar y 62,9 % de la raya corrida, Holm 0,84).")]
        public bool S_MAJORS_SPY_oi { get; set; } = false;   // 4.1.4: apagada
        [Display(Name = "SPY 0Γ est vol", GroupName = G0, Order = 21, Description = SERIE + "Zero Gamma estandar de SPY por volumen: el zero del ETF, " + SPY_A_ES + ". Estado: SIN MEDIR EN ES (en NQ, con QQQ: sin ventaja medida).")]
        public bool S_ZEST_SPY_vol { get; set; } = false;   // 4.1.4: apagada de la vista por defecto (pedido del operador 09-10; se sigue calculando)
        [Display(Name = "SPY carga OI", GroupName = G0, Order = 22, Description = SERIE + "Carga: los strikes de SPY con mas INTERES ABIERTO dentro del radio; la etiqueta lleva el strike de SPY y la raya va " + SPY_A_ES + ". OJO, horizonte REAL: CBOE hasta 14 dias en vivo y 8 desde el archivo, strikes a +-5 % del spot, sin el contrato sin punta (iv <= 0): es un PISO, no el total de la cadena. Estado: SIN MEDIR EN ES (en NQ, con QQQ: sin validar como nivel; es cobertura).")]
        public bool S_CARGA_SPY { get; set; } = false;

        // ---- la familia sumada y la confluencia (Order 23-25)
        [Display(Name = "FAM muros vol", GroupName = G0, Order = 23, Description = SERIE + "Call Wall / Put Wall de la familia sumada por volumen: los tres libros (ES, SPX y SPY, ya en precio de ES) sumados en USD de cobertura por 1 % (ES x50, SPX y SPY x100), en baldes de la grilla de la Familia. En ES no esta medido cual libro pesa mas. Estado: SIN MEDIR EN ES (en NQ, otro producto: NO dibujar como muro, su efecto era estar cerca del precio).")]
        public bool S_FAM_MUROS_vol { get; set; } = false;
        [Display(Name = "FAM muros OI", GroupName = G0, Order = 24, Description = SERIE + "Lo mismo que 'FAM muros vol' por interes abierto (solo si los tres libros tienen el OI de la misma noche). Estado: SIN MEDIR EN ES (en NQ, otro producto: NO dibujar como muro, extremo 59,1 % contra 58,6 % de la raya corrida).")]
        public bool S_FAM_MUROS_oi { get; set; } = false;   // 4.1.4: apagada
        [Display(Name = "CONF vol", GroupName = G0, Order = 25, Description = SERIE + "Confluencia por volumen: un precio donde dos libros distintos (ES, SPX y SPY, ya llevados a ES) ponen un muro, un major o un cruce casi en el mismo lugar. Estado: SIN MEDIR EN ES (en NQ, otro producto: de dia, sin validar: rebote 66,1 % contra 62,3 % del azar, Holm 0,67).")]
        public bool S_CONF_vol { get; set; } = false;
        // 5.0 (27-10-2026): aca iban las 6 casillas de TQQQ x3 (S_T_DOMS_vol, S_T_MUROS_vol, S_T_MUROS_oi, S_T_ZERO_oi, S_T_ZERO_vol_pm, S_T_DOMS_raz).

        // ---- 4.1.5b / 4.1.5 (09-10-2026): como la clasica. 5.0 (P5): en un grafico de ES la clasica no arma capas: su unico libro es SPX (Order 26-27)
        [Display(Name = "Clas SPX D1-3", GroupName = G0, Order = 26, Description = SERIE + "Las dominantes D1-D3 de la clasica en un grafico de ES (alli no arma capas: su unico libro es SPX), calculadas adentro con su formula: GEX por volumen del 0DTE de SPX, radio min(2 %, 100 pts) (= 100 pts en ES), una por lado con empate 20 % (la mas cercana al precio), D3 = la siguiente mas fuerte, en el centroide de +-12 pts; con la misma base que 'Clas SPX 0Γ'. Sin histeresis y sin cambio ▲▼. SIN VERIFICAR contra la clasica corriendo en ES (no hay salidas de la clasica en ES desde el 05-10). Estado: SIN MEDIR EN ES (en NQ, con NDX: replica para comparar, sin validar).")]
        public bool S_R10_SPX_dom { get; set; } = true;
        [Display(Name = "Clas SPX 0Γ", GroupName = G0, Order = 27, Description = SERIE + "El zero gamma de SPX de la clasica en un grafico de ES (su libro alli es SPX), calculado adentro con su formula: zero por volumen del 0DTE de SPX (grilla de 61 precios en +-3 %, el primer cruce de abajo hacia arriba), llevado a ES con la base de la clasica (su regla de la rueda sobre las cadenas y la cinta de la 5.0; con mas de 24 h, la cruda de forwards y despues el carry). La pestaña dice la base usada, de cuando es y la de ahora. La formula es la del zero estandar de SPX: lo que lo separa de 'SPX 0Γ est vol' es la base. SIN VERIFICAR contra la clasica corriendo en ES. Estado: SIN MEDIR EN ES (en NQ, con NDX: el 09-10 la base vieja lo puso 13-15 pts arriba; si la clasica pierde Rithmic re-mide su base y la replica deja de coincidir, ~13 pts).")]
        public bool S_R10_SPX_zero { get; set; } = true;

        // ---- 4.1.3 (09-10-2026): como la 2.0. 5.0 (P5): en un grafico de ES la 2.0 no arma capas: su unico libro es SPX; SPY no lo dibuja (Order 28-31)
        [Display(Name = "2.0 SPY D1-D2", GroupName = G0, Order = 28, Description = SERIE + "Seleccion estilo 2.0 sobre SPY. La 2.0 NO dibuja SPY en un grafico de ES (alli su unico libro es SPX): esto no replica nada que se vea en la 2.0, aplica su formula (las 2 de mayor |GEX| por volumen del 0DTE de SPY a <= min(2 % del futuro, 100 pts)) con su conversion para un ETF (razon = MES de la vela de 1 min del ultimo trade de la cadena / spot de SPY; la pestaña dice el origen). De noche esa razon junta el MES de las 16:14 NY con el SPY del after-hours. Apagada por defecto. Estado: SIN MEDIR EN ES (en NQ, con QQQ: su razon erro 24 pts de mediana de noche en 16 noches).")]
        public bool S_R20_SPY_vol { get; set; } = false;   // 5.0 (P5): apagada (la 2.0 no dibuja SPY en ES; decision del coordinador 27-10)
        [Display(Name = "2.0 SPX D1-D2", GroupName = G0, Order = 29, Description = SERIE + "Las dominantes de la 2.0 en un grafico de ES (alli no arma capas: su unico libro es SPX), calculadas adentro con su formula: las 2 de mayor |GEX| por volumen del 0DTE de SPX a <= min(2 % del futuro, 100 pts) (= 100 pts en ES), sin lado ni centroide, llevadas a ES con la base de la 2.0 (la pestaña dice cual uso y de donde salio, en naranja si es un respaldo). SIN VERIFICAR contra la 2.0 corriendo en ES (no hay salidas de la 2.0 en ES desde el 05-10). Estado: SIN MEDIR EN ES (en NQ, con NDX: la base cruda de la 2.0 erro 5,1 pts de mediana en 17 noches).")]
        public bool S_R20_SPX_vol { get; set; } = true;
        [Display(Name = "SPY dom (2.0)", GroupName = G0, Order = 30, Description = SERIE + "La seleccion de la 2.0 (las 2 de mayor |GEX| por volumen a <= min(2 %, 100 pts)) sobre el libro SPY de la 5.0, con la razon sincronizada (C8) en vez de la de la 2.0. Estado: SIN MEDIR EN ES (en NQ, con QQQ: medida en 18 noches como la mejor combinacion conocida; describe, no anticipa).")]
        public bool S_DOMS_SPY_vol { get; set; } = false;   // 4.1.4: apagada por defecto (pedido del operador 09-10)
        [Display(Name = "SPX dom (2.0)", GroupName = G0, Order = 31, Description = SERIE + "La seleccion de la 2.0 sobre el libro SPX de la 5.0, con la base sincronizada (C7) en vez de la de la 2.0. Estado: SIN MEDIR EN ES (en NQ, con NDX: tampoco se midio).")]
        public bool S_DOMS_SPX_vol { get; set; } = false;

        // ---- lo que dibuja el motor 3.0 que corre adentro (Order 32-34)
        [Display(Name = "3.0 ES rayas", GroupName = G0, Order = 32, Description = SERIE + "D1/D2 del motor 3.0 que corre adentro de la 5.0 en este grafico (estela-ES: libro ES por Rithmic): las rayas que dibujaria la 3.0, para comparar. No es la llave '3.0 ES dom▸': es la serie de la Familia que sale de la estela que el motor 3.0 calcula siempre. Estado: SIN MEDIR EN ES (lo que calcula el motor; nunca validado).")]
        public bool S_TRES_ES { get; set; } = false;
        [Display(Name = "3.0 SPX rayas", GroupName = G0, Order = 33, Description = SERIE + "D1/D2 de la capa SPX del motor 3.0 interno (CBOE, 15 min tarde; aproximada a la 3.0, sin arnes propio en ES), llevadas a ES con la conversion de ESA capa: puede no ser la base de la Familia y la pestaña no la muestra. No es la llave '3.0 capas▸': es la serie de la Familia que sale de la capa SPX que el motor 3.0 calcula siempre. Estado: SIN MEDIR EN ES (en NQ, con NDX: nunca validado).")]
        public bool S_TRES_SPX { get; set; } = true;
        [Display(Name = "3.0 SPY rayas", GroupName = G0, Order = 34, Description = SERIE + "D1/D2 de la capa SPY del motor 3.0 interno (CBOE, 15 min tarde; aproximada a la 3.0, sin arnes propio en ES), llevadas a ES con la razon de ESA capa: puede no ser la de la Familia y la pestaña no la muestra. No es la llave '3.0 capas▸': es la serie de la Familia que sale de la capa SPY que el motor 3.0 calcula siempre. Estado: SIN MEDIR EN ES (en NQ, con QQQ: nunca validado).")]
        public bool S_TRES_SPY { get; set; } = false;

        // ------------------------------------------------------------------ pantalla (mismos nombres que la 4.0.6; Order 35-41)
        [Display(Name = "Doble eje izq.", GroupName = G0, Order = 35, Description = "No necesita ninguna llave. Marcas a la izquierda con el precio del libro elegido en cada altura: SPX = ES - base sincronizada; SPY = ES / razon sincronizada (ES/SPY). Es la MISMA conversion que usan las rayas de SPX y SPY de la Familia. Ninguno = sin marcas. Default SPX (el indice que publican los tableros de GEX; con SPY cada dolar entero es un strike). Si la conversion no esta vigente o sale de su cota (base de SPX entre 0 y 150 pts; razon ES/SPY entre 9,95 y 10,25: MEDICIONES_ES.md) no se dibuja y la pestaña lo dice.")]
        public EjeFamilia4 Eje4Libro { get; set; } = EjeFamilia4.SPX;   // 5.0: Ninguno/SPX/SPY; default SPX (en la 4.1 era TQQQ, que ya no existe; 5.0 P5: confirmado)

        [Display(Name = "Estela▸", GroupName = G0, Order = 36, Description = "Llave: sin ella no se dibuja ninguno de los renglones con ↳ que la siguen. Las rayitas por vela de todas las series de la Familia de arriba (la historia de cada nivel). Apagada: sin rayitas ni rotulos de tramos; las etiquetas siguen.")] public bool Estela4 { get; set; } = true;
        [Display(Name = "Etiquetas▸", GroupName = G0, Order = 38, Description = "Llave: sin ella no se dibuja ninguno de los renglones con ↳ que la siguen. Las etiquetas chicas pegadas al eje de cada nivel de ahora de las series de la Familia de arriba: libro, rol y monto (la carga, ademas, el strike); los niveles a <= 0,25 pt van en una sola etiqueta. La edad del dato va arriba, al lado del recuadro (en naranja si pasa de 30 min). Apagada: sin etiquetas, sin montos y sin cambios (las rayitas siguen).")] public bool Rotulos4 { get; set; } = true;

        // 4.1.2 (pedido del operador: los millones en las etiquetas, sin los +1 +2). Nombres NUEVOS: ningun .ws los tiene, toman este default.
        [Display(Name = "↳ montos", GroupName = G0, Order = 39, Description = "Necesita la llave 'Etiquetas▸' prendida. GEX del strike en millones (M) o miles de millones (B) de USD de cobertura por 1 % del subyacente; calls +, puts - (quien esta largo no esta medido). Con este y el cambio apagados la etiqueta pasa a nombre corto + precio.")]
        public bool Monto41Rotulos { get; set; } = true;
        [Display(Name = "↳ cambios ▲▼", GroupName = G0, Order = 40, Description = "Necesita la llave 'Etiquetas▸' prendida. Series V: volumen operado en la ventana en ese strike, con la gamma de ahora (no dice si abren o cierran). Series OI: la publicacion de OI vigente contra la anterior (nunca con OI viejo). Verde = el muro crece en magnitud; rojo = se achica.")]
        public bool Cambio41Rotulos { get; set; } = true;
        [Display(Name = "Ventana del cambio por volumen", GroupName = "6. Pantalla", Order = 1026,
                 Description = "Cuantos minutos de volumen mira el cambio de las series V. El detalle de la pestaña dice la ventana REAL entre las dos fotos usadas.")]
        public VentanaCambio41 Cambio41Ventana { get; set; } = VentanaCambio41.M15;

        // 4.1.4 (pedido del operador 09-10: "la doble o triple raya no tiene rotulo/etiqueta y es importante"). Nombre NUEVO: ningun .ws lo tiene.
        [Display(Name = "↳ rot. tramos", GroupName = G0, Order = 37, Description = "Necesita la llave 'Estela▸' prendida. Cada raya de la estela que ya no es la vigente (15 velas o mas, y unos 15 min o mas, al mismo precio) lleva al final un rotulo chico con su nombre y su precio (\"SPX muro V 6.886,38\"); varias rayas a <= 0,25 pt que terminan juntas van en uno solo (\"SPX muro V/OI · 2.0 SPX 6.886\"). Nunca sobre las etiquetas de la derecha, la pestaña ni la franja de arriba (Separacion del borde de arriba); como mucho 14 (los tramos mas largos).")]
        public bool Tramos41Rotulos { get; set; } = false;   // 4.1.6d: apagado de fabrica (pedido del operador 09-10: "las etiquetas del medio sacalas, solo a la derecha")

        [Display(Name = "Pestaña 5.0", GroupName = G0, Order = 41, Description = "No necesita ninguna llave. La pestaña 'PythiaGex 5.0 ▸' arriba a la izquierda: los avisos (motor parado, fuentes caidas, 'mercado cerrado: se muestra la sesion del ...') y, con un clic, las fuentes con su edad, las conversiones (base de SPX, razon de SPY) y el detalle de cada nivel con su strike. Es donde se ve de donde sale cada numero y de cuando es: conviene dejarla prendida.")] public bool Cabecera4 { get; set; } = true;
        [Display(Name = "Recuadro abierto", GroupName = "6. Pantalla", Order = 1035, Description = "Lo mismo que hacer clic en la pestaña PythiaGex 5.0.")]
        public bool Recuadro4Abierto { get => _pantallaAbierta4; set => _pantallaAbierta4 = value; }
        [Display(Name = "Separacion del borde de arriba (px)", GroupName = "6. Pantalla", Order = 1036, Description = "Para no tapar la leyenda ni el cartel rojo de ATAS (la 3.0 usa 56).")]
        [Range(0, 400)]
        public int MargenSup4 { get; set; } = 56;
        [Display(Name = "Tamano de letra", GroupName = "6. Pantalla", Order = 1040)]
        [Range(6, 14)]
        public int Letra4 { get; set; } = 9;

        /// <summary>4.0.6: carpeta de los json de la vista previa. En la 4.1 NO se lee (todo adentro): oculta, solo para que el valor guardado
        /// en el .ws tenga donde caer.</summary>
        [Browsable(false)]
        public string Carpeta4 { get; set; } = "";

        // ------------------------------------------------------------------ estado y gancho
        private volatile bool _pantallaAbierta4;                 // el recuadro desplegable arranca CERRADO
        private PantallaFamilia _pantallaObj4;
        private static readonly long EPOCA_PANTALLA4 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        /// <summary>GANCHO-4.1 pantalla: el dibujo de la 4.0 (B5). Se crea en el primer uso.</summary>
        internal IPantallaFamilia _pantalla
        {
            get
            {
                var p = Volatile.Read(ref _pantallaObj4);
                if (p != null) return p;
                Interlocked.CompareExchange(ref _pantallaObj4, new PantallaFamilia(this), null);
                return Volatile.Read(ref _pantallaObj4);
            }
        }

        /// <summary>La casilla de una serie (sin reflexion).</summary>
        internal bool SerieVisible4(string id)
        {
            switch (id)
            {
                case "MUROS_ES_vol": return S_MUROS_ES_vol;
                case "MAJORS_SPY_oi": return S_MAJORS_SPY_oi;
                case "CONF_vol": return S_CONF_vol;
                case "MAJORS_ES_vol": return S_MAJORS_ES_vol;
                case "MUROS_ES_oi": return S_MUROS_ES_oi;
                case "MUROS_SPX_vol": return S_MUROS_SPX_vol;
                case "MUROS_SPX_oi": return S_MUROS_SPX_oi;
                case "MUROS_SPY_vol": return S_MUROS_SPY_vol;
                case "MUROS_SPY_oi": return S_MUROS_SPY_oi;
                case "MAJORS_ES_oi": return S_MAJORS_ES_oi;
                case "MAJORS_SPX_vol": return S_MAJORS_SPX_vol;
                case "MAJORS_SPX_oi": return S_MAJORS_SPX_oi;
                case "MAJORS_SPY_vol": return S_MAJORS_SPY_vol;
                case "FAM_MUROS_vol": return S_FAM_MUROS_vol;
                case "FAM_MUROS_oi": return S_FAM_MUROS_oi;
                case "ZEST_ES_vol": return S_ZEST_ES_vol;
                case "ZEST_ES_oi": return S_ZEST_ES_oi;
                case "ZEST_SPX_vol": return S_ZEST_SPX_vol;
                case "ZEST_SPY_vol": return S_ZEST_SPY_vol;
                case "ZTP_ES_vol": return S_ZTP_ES_vol;
                case "ZTP_SPX_vol": return S_ZTP_SPX_vol;
                case "TRES_ES": return S_TRES_ES;
                case "TRES_SPX": return S_TRES_SPX;
                case "TRES_SPY": return S_TRES_SPY;
                case "CARGA_ES": return S_CARGA_ES;
                case "CARGA_SPX": return S_CARGA_SPX;
                case "CARGA_SPY": return S_CARGA_SPY;
                case "TOPN_ES_vol": return S_TOPN_ES_vol;
                case "TOPN_ES_oi": return S_TOPN_ES_oi;
                // 5.0: sin las T_* de TQQQ
                case "R20_SPY_vol": return S_R20_SPY_vol;       // 4.1.3
                case "R20_SPX_vol": return S_R20_SPX_vol;
                case "DOMS_SPY_vol": return S_DOMS_SPY_vol;
                case "DOMS_SPX_vol": return S_DOMS_SPX_vol;
                case "R10_SPX_zero": return S_R10_SPX_zero;     // 4.1.5
                case "R10_SPX_dom": return S_R10_SPX_dom;       // 4.1.5b
                default: return false;
            }
        }

        /// <summary>Para el log de arranque: los ajustes de pantalla que ATAS leyo del workspace.</summary>
        internal string AjustesPantalla4Texto()
        {
            var a = new AjustesPantalla();
            ((IGraficoPantalla)this).Ajustes(a);
            return PantallaFamilia.VERSION + ": " + PantallaFamilia.Resumen(a)
                 + (string.IsNullOrEmpty(Carpeta4) ? "" : " · Carpeta4 guardada (" + Carpeta4 + ") ignorada: todo se calcula adentro");
        }

        // ------------------------------------------------------------------ IGraficoPantalla (explicito: no choca con nada de la 3.0)
        void IGraficoPantalla.Ajustes(AjustesPantalla a)
        {
            if (a == null) return;
            a.Visibles.Clear();
            foreach (var c in SeriesPantalla.Casillas) if (SerieVisible4(c.Id)) a.Visibles.Add(c.Id);
            a.Eje = Eje4Libro; a.Estela = Estela4; a.Rotulos = Rotulos4; a.Cabecera = Cabecera4; a.PanelAbierto = _pantallaAbierta4;
            a.Letra = Letra4; a.MargenSup = MargenSup4;
            a.Montos = Monto41Rotulos; a.Cambios = Cambio41Rotulos; a.Ventana = Cambio41Ventana;      // 4.1.2
            a.Tramos = Tramos41Rotulos;                                                               // 4.1.4
        }

        bool IGraficoPantalla.PanelAbierto { get => _pantallaAbierta4; set => _pantallaAbierta4 = value; }

        bool IGraficoPantalla.Contenedor(out Rectangle area)
        {
            area = Rectangle.Empty;
            try
            {
                if (ChartInfo?.PriceChartContainer == null) return false;
                area = ChartArea;
                return area.Width > 0 && area.Height > 0;
            }
            catch { return false; }
        }

        string IGraficoPantalla.Instrumento { get { try { return InstrumentInfo?.Instrument ?? ""; } catch { return ""; } } }
        int IGraficoPantalla.PrimeraVisible { get { try { return FirstVisibleBarNumber; } catch { return 0; } } }
        int IGraficoPantalla.UltimaVisible { get { try { return LastVisibleBarNumber; } catch { return -1; } } }
        int IGraficoPantalla.BarraActual { get { try { return CurrentBar; } catch { return 0; } } }

        /// <summary>4.1.8: la vela bajo el mouse, con las MISMAS guardas que la 2.0 (PythiaGexDos/GammaHoy.cs:1469),
        /// que ya estan probadas: sin IsMouseLeave el valor quedaba pegado en la ultima vela, y con
        /// IsMovingChartUsingMouse el rebobinado saltaba mientras se arrastra el grafico.</summary>
        int IGraficoPantalla.BarraBajoMouse
        {
            get
            {
                try
                {
                    var m = ChartInfo?.MouseLocationInfo;
                    if (m == null || m.IsMouseLeave || m.IsMovingChartUsingMouse) return -1;
                    int b = m.BarBelowMouse;
                    return b >= 0 && b < CurrentBar ? b : -1;
                }
                catch { return -1; }
            }
        }

        double IGraficoPantalla.PrecioAlto { get { try { var c = ChartInfo?.PriceChartContainer; return c == null ? double.NaN : (double)c.High; } catch { return double.NaN; } } }
        double IGraficoPantalla.PrecioBajo { get { try { var c = ChartInfo?.PriceChartContainer; return c == null ? double.NaN : (double)c.Low; } catch { return double.NaN; } } }

        int IGraficoPantalla.YDePrecio(double p)
        {
            if (double.IsNaN(p) || double.IsInfinity(p) || Math.Abs(p) > 1e12) return int.MinValue;
            try { var c = ChartInfo?.PriceChartContainer; return c == null ? int.MinValue : c.GetYByPrice((decimal)p, false); }
            catch { return int.MinValue; }
        }

        int IGraficoPantalla.XDeBarra(int barra)
        {
            try { var c = ChartInfo?.PriceChartContainer; return c == null ? int.MinValue : c.GetXByBar(barra, false); }
            catch { return int.MinValue; }
        }

        /// <summary>Hora de la vela por TICKS crudos (ATAS da UTC con Kind Unspecified; ToUniversalTime ya corrio todo 3 h una vez).</summary>
        bool IGraficoPantalla.Vela(int barra, out long msUtc, out double cierre)
        {
            msUtc = 0; cierre = double.NaN;
            try
            {
                if (barra < 0 || barra >= CurrentBar) return false;
                var c = GetCandle(barra);
                if (c == null) return false;
                var t = c.LastTime != default(DateTime) ? c.LastTime : c.Time;
                msUtc = (t.Ticks - EPOCA_PANTALLA4) / TimeSpan.TicksPerMillisecond;
                cierre = (double)c.Close;
                return true;
            }
            catch { return false; }
        }

        void IGraficoPantalla.Redibujar() { try { RedrawChart(new RedrawArg(ChartArea)); } catch { } }
    }
}
