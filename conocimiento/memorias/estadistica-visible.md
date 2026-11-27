---
name: estadistica-visible
description: "28-10/29-10: 'PythiaGex - Estadistica Visible' 2.0 (atas/EstadisticaVisible): UN cuadro por vela (EstVelas=1, eligio el operador), numero en negrita con sombra acostado/parado o solo colores si no entra, LUPA del mouse con numeros exactos; nunca depende del zoom ni se sale del cuadro"
metadata:
  node_type: memory
  type: project
  originSessionId: 404dab2f-b5f6-48fa-ba08-3ab764cbbdd8
  modified: 2026-10-30T01:22:04.275Z
---

Pedido 28-10 ~23:10 AR: "tengo que hacer recontra zoom para ver los numeros del cluster [Statistic] y pierdo el contexto; no me obligues al
modo footprint". OJO: el operador le dice "cluster search" al **Cluster Statistic** (panel Delta / Session Delta / Volume). Las burbujas
rojas/verdes del MES 1m son **Adaptive Big Trades** (Size 20); tambien hay un Cluster Search agregado. Me desvie a las burbujas y se enojo.

**Medido:** el de fabrica escribe el numero solo si entra en la columna (17 px a su zoom; "-245" pide ~24) y de sus 72 ajustes ninguno lo
fuerza. **Hecho:** indicador propio `atas/EstadisticaVisible/EstadisticaVisible.cs` (v1.0, panel propio, ajustes con prefijo Est): las velas
se turnan en 2-3 renglones (par arriba / impar abajo por numero absoluto de vela); si no entran todos, por importancia (vela en curso y las
de numero mas grande, sin pisarse). Miles "-1,4k". Sesion = IsNewSession. Registro `%APPDATA%\ATAS\PythiaGex\estadistica\auditoria-<dia>.log`
(columna, letra, renglones, cuantos numeros, ultimas 15 velas). Instalado 23:28 en MES 1m; alto del panel 92 (= el de fabrica) con
`herramientas/ws_alto_estadistica_visible.py` via `instalar_5_0.ps1 -SinDll -AntesDeLanzar` (ATAS lo crea con 60: filas de 16 px, sin numeros).
A ese zoom (columna 10 px, fila 27 px, letra 7): 47/63 delta, 32/63 sesion, 51/63 volumen. Verificado: 20 velas 02:10-02:29 UTC, delta y
volumen y sesion = registro crudo; el Cluster Statistic de fabrica muestra lo mismo redondeado ("-2K" para -1.653).
Dato: al recargar la historia ATAS cambio la vela 02:23 de 111/471 (vivo) a 107/467 (historia): es de ATAS, igual para el de fabrica.
**Iteraciones 23:30-23:55 (el operador saco el Cluster Statistic de fabrica):** su zoom real es 6-9 px por vela ("este zoom o mas").
- 1.0 renglones alternados: con 6-10 px no entran todos. 1.1 tablero en el margen vacio: 5 cuadros -> RECHAZADO ("no en el espacio que
  quedaba"). 1.2 numeros alineados de las ultimas 15 en 2-3 renglones + letra angosta: solo 2 velas (con 6 px "-245" pide 19 px; Bahnschrift
  y Arial Narrow MIDEN IGUAL en ATAS: el RenderFont no cambia de ancho). 
- **1.3b (la que quedo):** izquierda tira pintada alineada; derecha CUADROS de las ultimas velas (75 % del ancho, tope EstCuantas 15; con su
  pantalla 13 de 43 px, letra 11), HORA local arriba de cada cuadro (sin ella el eje de ATAS debajo engaña), la en curso con recuadro.
  Panel 120 (`ws_alto_estadistica_visible.py`, ALTO=120). Verificado 23:54: 39 numeros = crudo y la sesion = suma acumulada vela a vela.
- 1.3b RECHAZADA ("esta mal partido, saca eso que esta vacio, se profesional, que persiga a las velas correctas"). Pregunta con 2
  maquetas: eligio **"cuadros bajo sus velas"** -> **1.4 (la vigente):** todo el panel son cuadros alineados exactamente bajo las velas que
  resumen (GetXByBar), velas por cuadro = la menor de 1,2,3,5,6,10,15... con la que entra el numero (a su zoom 7 px: 6 velas = 6 min), cortes
  a hora redonda (segundos/ (k x duracion)), Delta y Volumen = suma, Sesion = valor al cierre del cuadro; el cuadro que se esta llenando, si
  queda angosto, se junta con el anterior; la vela en curso va sola con recuadro pegada a su derecha; "cuadro = 6 velas" bajo "Delta".
  Sin fila de horas (el eje del grafico vale). Verificado 00:02 del 29-10: 5 cuadros sumados a mano = pantalla; 23:00/23:20/23:40 del eje
  caen en los bordes esperados. Registro: cada cuadro con sus velas sueltas y sus x.
- 1.4 ERROR (29-10 00:20, el operador): "cuando estiras o contraes los numeros cambian, desaparecen o se fusionan; debe ser integro
  siempre y corresponder a su vela siempre, sino despues malas decisiones". Causa: velas por cuadro elegidas SEGUN EL ZOOM y el bloque
  angosto pegado al anterior. **1.5 (vigente):** bloque FIJO `EstBloque` = 5 velas (nombre nuevo: el .ws guardaba EstVelasPorCuadro=0),
  cortes a hora redonda, nada se junta, numeros EXACTOS hasta 99.999 ("-2483"), el zoom solo cambia la letra; el bloque en curso crece y
  se estira a la derecha con recuadro. Verificado 00:40: 3 zooms con la rueda sobre el panel, los 10 cuadros identicos; 5 sumas a mano = pantalla.
  REGLA: un numero en pantalla NUNCA puede depender del zoom.
- 29-10 00:45 "mas chicos los cuadros, que coincidan con las velas si o si" -> **1.6**: bloque fijo de 2 velas (`EstVelasPorBloque`, nombre
  nuevo), numeros ESCALONADOS en 2-3 renglones por la CLAVE del bloque (hora, no pantalla), bloque en curso estirado a la derecha.
  00:50 "el delta sesion y volumen no me importan, abrevialos; el delta si es el importante" -> **1.7 (vigente)**: Delta exacto en la fila
  alta (mitad del panel, letra 11), Sesion en MILES con una decimal ("-2,3") y Volumen abreviado desde mil ("1,2k"), filas bajas letra 7-9.
  Verificado 00:54: 2 zooms (11 y 9 px por vela) con deltas identicos, 5 bloques sumados a mano = pantalla.
- 01:00 "se salen los numeros de los cuadros, imaginate mañana con mas volumen" (los escalones de 1.6-1.7 usaban el ancho de dos
  cuadros) -> **1.8b (vigente)**: cada numero SIEMPRE ADENTRO de su cuadro, una linea, centrado; bloque fijo de 4 velas (`EstVelasCuadro`,
  nombre definitivo): a 7 px por vela da 28 px (adentro 23) y entra "-557"/"1,4k"; si uno no entra con la letra de la fila, ese cuadro
  usa letra 6; delta exacto hasta 999 y despues "-1,2k" (regla fija). Con 3 velas a 7 px quedaban 7 deltas y 5 volumenes sin numero.
  Verificado 01:07: 0 sin numero, 2 zooms identicos, 5 bloques sumados a mano = pantalla. REGLA: un numero NUNCA afuera de su cuadro.
- 01:10 "depende del zoom desaparecen valores" -> 1.9: si la fila no entra acostada, numeros PARADOS (texto girado: ATAS no gira,
  se dibuja un Bitmap GDI+ con DrawStaticImage, cache por texto). 1.9c: llega sin faltantes hasta ~5 px por vela con bloques de 4.
- 01:22 "que coincidan las velas SI O SI con sus cuadros, sino confusion al analizar una vela y su delta en el pasado; mas legible,
  enfasis" + pregunta con 2 maquetas: eligio **"1 cuadro por vela"** -> **2.0 (vigente)**: EstVelas=1 (nombre nuevo), numero en
  NEGRITA con SOMBRA; la fila entera acostada si todos entran, si no parada (letra >= 7), si no "solo colores"; LUPA: mouse sobre una
  vela = columna amarilla + cartel "HH:mm Delta +116 / Vol 379 Sesion -2086" exactos. Con su zoom (6-8 px) las filas quedan solo colores
  y se lee con la lupa; los numeros por vela aparecen desde ~16 px por vela. Verificado: lupa 01:00 = -13 / 379 / -2086 = registro y
  = V:379 que ATAS muestra arriba. NO verificado en pantalla el modo con numeros por vela (no pude acercar el zoom: ver abajo).
- 29-10 22:00 "cuando te posas se ve la etiqueta y no el color de las barras; a veces queda frizada; de ultima con clic o
  despues de 10 s" -> **2.1c (vigente, instalada 22:19, sha e65f19eac19a)**: el detalle va en el espacio VACIO a la derecha de
  la vela en curso, 25 px abajo (ATAS pone su barrita ojo/engranaje/X arriba a la derecha del panel al posar el mouse: ~23 px
  de alto, ultimos ~140 px; la 2.1 la tenia debajo), sobre los nombres si no entra; la vela se marca con 2 lineas amarillas
  AFUERA (rendija o pixel de la vecina) + pestañas de 4 px. Congelamiento: IsMouseLeave de ATAS NO siempre llega (medido: mouse
  saltando al heatmap de la derecha y quieto = lupa 10 s en la vela en curso con 2.1b); ahora se borra si el cursor real
  (GetCursorPos) se mueve y la posicion de ATAS no, si la posicion cae fuera de los bordes, y con 10 s de mouse quieto
  (EstLupaQuieto); temporizador 250 ms redibuja. Modos EstLupa posar/clic/apagada, EstLupaDemora 0. Registro
  `lupa-<dia>.log` con "crudo GetCandle" (sesion = suma vela a vela). Verificado: 4 velas = crudo, V:706 = cabecera de ATAS,
  marca en pixeles fisicos (captura_fisica.ps1 del scratchpad) afuera de la vela, panel de ordenes/heatmap/quieto la borran.
  NO probado el modo clic (no clickeo su lienzo). Coordenadas: panel x 0-844 = pantalla fisica +27/28 px; el panel termina
  donde empieza el eje de precios. Cada reinicio con instalar_5_0 le cambio el ancho de vela (4,4 -> 3 -> 1,5 px).
- **Trampas del zoom de ATAS (29-10):** la rueda sobre el panel cambia el ancho de vela a saltos irregulares; rueda sobre el EJE DE TIEMPO
  corrio la escala de PRECIOS; Ctrl+rueda cambio la escala vertical; doble clic en el eje de precios vuelve a escala automatica. Para
  devolverle la vista: cerrar ATAS con "Close without saving" (UIA) y relanzar con instalar_5_0.ps1 -SinDll (carga el .ws del ultimo guardado).
**Regla del operador (otra vez, 28-10):** "controla visualmente lo que haces, no supongas, que sea legible y armonico, la tarea final en
pantalla siempre": captura a resolucion completa (1600x900 fisico) recortada y ampliada, no la de 0,6.
Ver [[compilar-indicadores-atas]], [[atas-dialogo-indicadores-clics]], [[flujo-claro-cvd-superador]].
