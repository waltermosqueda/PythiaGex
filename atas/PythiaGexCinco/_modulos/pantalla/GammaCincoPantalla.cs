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
        [Display(Name = "ES muros vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 0, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Call wall / put wall del 0DTE de ES por volumen: el strike del 0DTE de ES donde HOY se negocio mas gamma de calls (muro C, arriba) y de puts (muro P, abajo), a +-100 pts. Estado: candidata principal de noche, SIN VALIDAR: punta de mecha 60,6 % contra 50,9 % del azar (20 sesiones), Holm 0,135; contra la raya corrida 53,8 %.")] public bool S_MUROS_ES_vol { get; set; } = false;
        [Display(Name = "SPY majors OI", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 16, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Major positivo / negativo de SPY por interes abierto (banda +-20): el strike de SPY con mas gamma neta acumulada (posiciones de ayer); un strike de SPY son 41 pts de ES, por eso es una banda de +-20. Estado: opcional de noche, SIN VALIDAR: rebote 68,2 % contra 63,9 % del azar y 62,9 % de la raya corrida, Holm 0,84.")] public bool S_MAJORS_SPY_oi { get; set; } = false;   // 4.1.4: apagada
        [Display(Name = "CONF vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 20, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Confluencia por volumen (muros, majors o cruces de 2 o mas libros a <= 3 pts): un precio donde dos libros distintos (ES, SPX, SPY) ponen un nivel casi en el mismo lugar. Estado: de dia, SIN VALIDAR: rebote 66,1 % contra 62,3 % del azar, Holm 0,67.")] public bool S_CONF_vol { get; set; } = false;
        [Display(Name = "ES majors vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 2, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Major positivo / negativo del 0DTE de ES por volumen: el strike de ES con mas gamma neta de hoy, positiva (M+) y negativa (M-). Estado: de noche, SIN VALIDAR: punta de mecha 60,4 % contra 50,9 % del azar, Holm 0,117.")] public bool S_MAJORS_ES_vol { get; set; } = false;

        [Display(Name = "ES muros OI", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 1, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Call wall / put wall de ES por interes abierto: donde estan las posiciones abiertas de ayer en el 0DTE de ES (calls arriba, puts abajo). Estado: sin ventaja medida (rebote 68,1 % contra 64,0 % del azar, Holm 1,0); sin dibujar mientras el OI es de anteayer.")] public bool S_MUROS_ES_oi { get; set; } = true;
        [Display(Name = "SPX muros vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 7, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Call wall / put wall de SPX por volumen (a precio de ES por la base sincronizada): lo mismo en las opciones del indice SPX de CBOE, corrido a precio del futuro. Estado: sin ventaja medida.")] public bool S_MUROS_SPX_vol { get; set; } = true;
        [Display(Name = "SPX muros OI", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 8, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Call wall / put wall de SPX por interes abierto: posiciones abiertas en SPX. Estado: sin ventaja medida.")] public bool S_MUROS_SPX_oi { get; set; } = false;
        [Display(Name = "SPY muros vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 13, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Call wall / put wall de SPY por volumen (banda +-20): lo mismo en el ETF SPY (strikes cada 41 pts de ES). Estado: sin ventaja medida.")] public bool S_MUROS_SPY_vol { get; set; } = false;
        [Display(Name = "SPY muros OI", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 14, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Call wall / put wall de SPY por interes abierto (banda +-20): posiciones abiertas en SPY. Estado: sin ventaja medida (rebote 68,0 % contra 63,9 % del azar, Holm 1,0).")] public bool S_MUROS_SPY_oi { get; set; } = false;   // 4.1.4: apagada
        [Display(Name = "ES majors OI", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 3, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Major +/- de ES por interes abierto: gamma neta de las posiciones de ayer en ES. Estado: sin ventaja medida.")] public bool S_MAJORS_ES_oi { get; set; } = false;
        [Display(Name = "SPX majors vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 9, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Major +/- de SPX por volumen: gamma neta de hoy en SPX. Estado: sin ventaja medida.")] public bool S_MAJORS_SPX_vol { get; set; } = false;
        [Display(Name = "SPX majors OI", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 10, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Major +/- de SPX por interes abierto: gamma neta de las posiciones de ayer en SPX. Estado: sin ventaja medida.")] public bool S_MAJORS_SPX_oi { get; set; } = false;
        [Display(Name = "SPY majors vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 15, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Major +/- de SPY por volumen (banda +-20): gamma neta de hoy en SPY. Estado: sin ventaja medida.")] public bool S_MAJORS_SPY_vol { get; set; } = false;
        [Display(Name = "FAM muros vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 18, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Muros de la familia sumada en USD por 1 % (ES x20, SPX y SPY x100), grilla de 5 pts: los tres libros sumados en dolares; los domina SPY. Estado: NO dibujar como muro: su efecto es estar cerca del precio (contra la raya corrida: igual).")] public bool S_FAM_MUROS_vol { get; set; } = false;
        [Display(Name = "FAM muros OI", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 19, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Muros de la familia sumada por interes abierto: posiciones abiertas de los tres libros sumadas en dolares. Estado: NO dibujar como muro (extremo 59,1 % contra 58,6 % de la raya corrida).")] public bool S_FAM_MUROS_oi { get; set; } = false;   // 4.1.4: apagada

        [Display(Name = "ES 0Γ est vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 4, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Zero gamma estandar de ES por volumen (la suma de todo el libro reevaluada a cada precio): el precio donde la gamma total del libro pasa de positiva a negativa; queda lejos y casi no salta. Estado: no es la raya '0G' de la 3.0 (esa es un cruce por strike); rebota igual que el azar.")] public bool S_ZEST_ES_vol { get; set; } = false;
        [Display(Name = "ES 0Γ est OI", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 5, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Zero gamma estandar de ES por OI: lo mismo con las posiciones de ayer. Estado: 53 toques en 20 sesiones: no alcanza.")] public bool S_ZEST_ES_oi { get; set; } = false;
        [Display(Name = "SPX 0Γ est vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 11, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Zero gamma estandar de SPX por volumen: zero del indice. Estado: sin ventaja medida.")] public bool S_ZEST_SPX_vol { get; set; } = false;
        [Display(Name = "SPY 0Γ est vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 17, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Zero gamma estandar de SPY por volumen: zero del ETF. Estado: sin ventaja medida.")] public bool S_ZEST_SPY_vol { get; set; } = false;   // 4.1.4: apagada de la vista por defecto (pedido del operador 09-10; se sigue calculando)
        [Display(Name = "ES cruces vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 6, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Cruces de signo limpios (sin islas) mas cercanos arriba y abajo, ES por volumen: el tunel de la 3.0 pero sin los strikes 'isla' de 2-4 contratos y sin sostener cruces muertos. Estado: NO es muro: el tunel no retiene mas que uno puesto al azar (vuelve 41,3 % contra 43,6 %).")] public bool S_ZTP_ES_vol { get; set; } = false;
        [Display(Name = "SPX cruces vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 12, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Cruces de signo limpios de SPX por volumen: techo y piso del zero de SPX sin islas. Estado: NO es muro.")] public bool S_ZTP_SPX_vol { get; set; } = false;

        [Display(Name = "3.0 ES rayas", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 27, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). D1/D2 que dibujo la 3.0 (estela-ES): las rayas de su grafico de ATAS, para comparar. Estado: lo que hay hoy. No es la llave '3.0 ES dom▸': es la serie de la Familia que sale de la estela que la 3.0 calcula siempre.")] public bool S_TRES_ES { get; set; } = false;
        [Display(Name = "3.0 SPX rayas", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 28, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). D1/D2 de la capa SPX del motor 3.0 interno (aprox. a la 3.0: spot al sello - 900 s, base por forwards; sin arnes propio): la capa SPX de su grafico, calculada adentro. Estado: lo que hay hoy. No es la llave '3.0 capas▸': es la serie de la Familia que sale de la capa SPX que la 3.0 calcula siempre.")] public bool S_TRES_SPX { get; set; } = true;
        [Display(Name = "3.0 SPY rayas", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 29, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). D1/D2 de la capa SPY del motor 3.0 interno (aprox. a la 3.0: spot al sello - 900 s; sin arnes propio): la capa SPY de su grafico, calculada adentro. Estado: lo que hay hoy. No es la llave '3.0 capas▸': es la serie de la Familia que sale de la capa SPY que la 3.0 calcula siempre.")] public bool S_TRES_SPY { get; set; } = false;

        [Display(Name = "ES top-N vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 7, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Los N strikes de ES con mas |gamma neta| por VOLUMEN dentro del radio (min(2 %, 100 pts)), de mayor a menor. Existe porque MUROS y MAJORS marcan UN strike por lado: medido el 12-10, en la ventana de +-100 pts habia 24 strikes con gamma y se dibujaban 2 (30800, 30860, 30870, 30840, 30875, 30890, 30920, 30950 quedaban sin marcar). Estado: SIN VALIDAR como nivel; es cobertura, no seleccion.")] public bool S_TOPN_ES_vol { get; set; } = true;
        [Display(Name = "ES top-N OI", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 8, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Lo mismo que TOPN_ES_vol pero con el INTERES ABIERTO de la OCC al cierre de ayer. Estado: SIN VALIDAR como nivel.")] public bool S_TOPN_ES_oi { get; set; } = false;
        [Display(Name = "ES carga (strikes abiertos)", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 9, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Los strikes con mas INTERES ABIERTO del libro, dentro del radio. OJO, horizonte REAL (no son todos los vencimientos de CME): el libro ES suscribe solo los 2 vencimientos mas cercanos (CadenaApi.Vencimientos = 2) y CBOE recorta a 14 dias en vivo y 8 desde el archivo, con strikes a +-5 % del spot; ademas no entra el contrato sin punta (iv <= 0). O sea el 'C x P y' es un PISO, no el total de CME. Existe porque el resto del calculo mira solo el vencimiento del frente. Estado: SIN VALIDAR como nivel; es cobertura.")] public bool S_CARGA_ES { get; set; } = true;
        [Display(Name = "SPX carga (strikes abiertos)", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 10, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Los strikes con mas INTERES ABIERTO del libro, dentro del radio. OJO, horizonte REAL (no son todos los vencimientos de CME): el libro ES suscribe solo los 2 vencimientos mas cercanos (CadenaApi.Vencimientos = 2) y CBOE recorta a 14 dias en vivo y 8 desde el archivo, con strikes a +-5 % del spot; ademas no entra el contrato sin punta (iv <= 0). O sea el 'C x P y' es un PISO, no el total de CME. Existe porque el resto del calculo mira solo el vencimiento del frente. Estado: SIN VALIDAR como nivel; es cobertura.")] public bool S_CARGA_SPX { get; set; } = false;
        [Display(Name = "SPY carga (strikes abiertos)", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 11, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Los strikes con mas INTERES ABIERTO del libro, dentro del radio. OJO, horizonte REAL (no son todos los vencimientos de CME): el libro ES suscribe solo los 2 vencimientos mas cercanos (CadenaApi.Vencimientos = 2) y CBOE recorta a 14 dias en vivo y 8 desde el archivo, con strikes a +-5 % del spot; ademas no entra el contrato sin punta (iv <= 0). O sea el 'C x P y' es un PISO, no el total de CME. Existe porque el resto del calculo mira solo el vencimiento del frente. Estado: SIN VALIDAR como nivel; es cobertura.")] public bool S_CARGA_SPY { get; set; } = false;
        // 5.0 (27-10-2026): aca iban las 6 casillas de TQQQ x3 (S_T_DOMS_vol, S_T_MUROS_vol, S_T_MUROS_oi, S_T_ZERO_oi, S_T_ZERO_vol_pm, S_T_DOMS_raz).

        // 4.1.3 (09-10-2026, pedido del operador: "copia la formula de la 2.0 y agregala a lo que ya tenemos"). Nombres NUEVOS: ningun .ws los tiene.
        [Display(Name = "2.0 SPY D1-D2", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 23, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Las rayas ambar de la 2.0 calculadas adentro igual que ella: libro SPY 0DTE por volumen, las 2 de mayor GEX a menos de 2 % (tope 100 pts), con SU razon (MES de la vela de 1 min del ultimo trade de la cadena / spot de SPY). De noche esa razon junta el MES de las 16:14 NY con el SPY del after-hours: queda corrida unos puntos. Replica a la 2.0 corriendo en un grafico de 1 min: con la 2.0 en otro marco (p. ej. 2 min) su vela, y su razon, cambian (de noche ~3 pts).")]
        public bool S_R20_SPY_vol { get; set; } = true;
        [Display(Name = "2.0 SPX D1-D2", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 24, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Las rayas verdes de SPX de la 2.0: la misma seleccion sobre la cadena de SPX con la base cruda de forwards de la cadena (si pasa la cota del carry).")]
        public bool S_R20_SPX_vol { get; set; } = true;
        [Display(Name = "SPY dom sel2.0", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 25, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). La seleccion de la 2.0 (2 de mayor GEX por volumen cerca del precio) sobre el libro SPY de la 5.0, con la razon sincronizada. Medida en 18 noches como la mejor combinacion conocida; describe, no anticipa.")]
        public bool S_DOMS_SPY_vol { get; set; } = false;   // 4.1.4: apagada por defecto (pedido del operador 09-10)
        [Display(Name = "SPX dom sel2.0", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 26, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). La seleccion de la 2.0 sobre el libro SPX de la 5.0 (base sincronizada). Sin medir: apagada.")]
        public bool S_DOMS_SPX_vol { get; set; } = false;

        // 4.1.5 (09-10-2026, pedido del operador: "analiza como la version clasica hizo esta estela ... y copiarla ahora para nuestro 4.0"). Nombre NUEVO:
        // ningun .ws lo tiene, toma este default (prendida).
        [Display(Name = "Clas SPX 0Γ", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 22, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Los puntitos blancos 'SPX 0Γ' de la clasica, calculados adentro con su formula: el zero gamma por volumen del 0DTE de SPX (grilla de 61 precios en +-3 %, el primer cruce de abajo hacia arriba) mas SU base: la de la rueda ANTERIOR, medida con su regla sobre las cadenas y la cinta de la 5.0 y congelada durante la rueda (con mas de 24 h, la cruda de forwards y despues el carry). La formula es la del zero estandar de SPX: lo que la corre 13-15 pts (09-10) es la base vieja. La pestaña dice la base usada, de cuando es y la de ahora. SALVEDAD (medida el 09-10): copia a la clasica cuando dibuja con la base de la rueda anterior; si la clasica pierde Rithmic (p. ej. al reiniciar ATAS) re-mide su base en la rueda y esta replica deja de coincidir (de 16:48 a 17:11 ART quedo ~13 pts corrida). Sin validar.")]
        public bool S_R10_SPX_zero { get; set; } = true;

        // 4.1.5b (09-10-2026, pedido del operador: "replicar la clasica su formula ... todo se pueda activar desactivar"). Nombre NUEVO: toma este default.
        [Display(Name = "Clas SPX D1-D3", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 21, Description = "No necesita ninguna llave: es una serie de la Familia y se dibuja sola. Se ve como rayitas con 'Estela▸' y como etiqueta con 'Etiquetas▸' (las dos prendidas por defecto). Las rayas 'SPX D1/D2/D3' de la clasica, calculadas adentro con su formula: GEX por volumen del 0DTE de SPX, radio min(2 %, 100 pts), una por lado con empate 20 % (la mas cercana al precio), D3 = la siguiente mas fuerte, en el centroide de +-12 pts; con la misma base que la Clasica SPX 0Γ (la de la rueda anterior). Sin histeresis y sin cambio ▲▼. SALVEDAD (medida el 09-10): si la clasica pierde Rithmic (p. ej. al reiniciar ATAS) re-mide su base en la rueda y estas rayas dejan de coincidir con las suyas (de 16:48 a 17:11 ART quedaron ~13 pts corridas, en varios minutos con otro strike ganador). Sin validar.")]
        public bool S_R10_SPX_dom { get; set; } = true;

        // ------------------------------------------------------------------ pantalla (mismos nombres que la 4.0.6)
        [Display(Name = "Doble eje izq.", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 30, Description = "No necesita ninguna llave. SPX = ES - base sincronizada; SPY = ES / razon sincronizada. La misma conversion que usan las rayas.")]
        public EjeFamilia4 Eje4Libro { get; set; } = EjeFamilia4.SPX;   // 5.0: Ninguno/SPX/SPY; default SPX (en la 4.1 era TQQQ, que ya no existe)

        [Display(Name = "Estela▸", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 31, Description = "Llave: sin ella no se dibuja ninguno de los renglones con ↳ que la siguen. Las rayitas por vela de todas las series de la Familia de arriba (la historia de cada nivel). Apagada: sin rayitas ni rotulos de tramos; las etiquetas siguen.")] public bool Estela4 { get; set; } = true;
        [Display(Name = "Etiquetas▸", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 33, Description = "Llave: sin ella no se dibuja ninguno de los renglones con ↳ que la siguen. Las etiquetas chicas pegadas al eje con el nombre, el precio y la edad del dato de cada nivel de ahora de las series de la Familia de arriba. Apagada: sin etiquetas, sin montos y sin cambios (las rayitas siguen).")] public bool Rotulos4 { get; set; } = true;

        // 4.1.2 (pedido del operador: los millones en las etiquetas, sin los +1 +2). Nombres NUEVOS: ningun .ws los tiene, toman este default.
        [Display(Name = "↳ montos", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 34, Description = "Necesita la llave 'Etiquetas▸' prendida. GEX del strike en millones (M) o miles de millones (B) de USD de cobertura por 1 % del subyacente; calls +, puts - (quien esta largo no esta medido). Con este y el cambio apagados la etiqueta vuelve a la de la 4.1.1, sin el +N.")]
        public bool Monto41Rotulos { get; set; } = true;
        [Display(Name = "↳ cambios ▲▼", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 35, Description = "Necesita la llave 'Etiquetas▸' prendida. Series V: volumen operado en la ventana en ese strike, con la gamma de ahora (no dice si abren o cierran). Series OI: la publicacion de OI vigente contra la anterior (nunca con OI viejo). Verde = el muro crece en magnitud; rojo = se achica.")]
        public bool Cambio41Rotulos { get; set; } = true;
        [Display(Name = "Ventana del cambio por volumen", GroupName = "6. Pantalla", Order = 1026,
                 Description = "Cuantos minutos de volumen mira el cambio de las series V. El detalle de la pestaña dice la ventana REAL entre las dos fotos usadas.")]
        public VentanaCambio41 Cambio41Ventana { get; set; } = VentanaCambio41.M15;

        // 4.1.4 (pedido del operador 09-10: "la doble o triple raya no tiene rotulo/etiqueta y es importante"). Nombre NUEVO: ningun .ws lo tiene.
        [Display(Name = "↳ rot. tramos", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 32, Description = "Necesita la llave 'Estela▸' prendida. Cada raya de la estela que ya no es la vigente (15 velas o mas, y unos 15 min o mas, al mismo precio) lleva al final un rotulo chico con su nombre y su precio (\"SPX muro V 31.040,74\"); varias rayas de la misma altura que terminan juntas van en uno solo (\"SPX muro V/OI · 2.0 SPX 31.041\"). Nunca sobre las etiquetas de la derecha, la pestaña ni la franja de arriba (Separacion del borde de arriba); como mucho 14 (los tramos mas largos).")]
        public bool Tramos41Rotulos { get; set; } = false;   // 4.1.6d: apagado de fabrica (pedido del operador 09-10: "las etiquetas del medio sacalas, solo a la derecha")

        [Display(Name = "Pestaña de detalle", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 36, Description = "No necesita ninguna llave. La pestaña 'PythiaGex 5.0 ▸' arriba a la izquierda: la edad del dato, los avisos (p. ej. 'mercado cerrado: se muestra la sesion del ...') y, con un clic, las fuentes, las conversiones y el detalle de cada nivel.")] public bool Cabecera4 { get; set; } = true;
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
