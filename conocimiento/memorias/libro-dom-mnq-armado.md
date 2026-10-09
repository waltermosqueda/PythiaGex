---
name: libro-dom-mnq-armado
description: "21-09: Smart DOM de MNQ acoplado a la derecha del heatmap con plantillas propias 'MNQ Libro Noche' (filtro 50) y 'MNQ Libro RTH' (filtro 70), trabado; y la trampa: cerrar una ventana flotante de ATAS con WM_CLOSE rompe el guardado del workspace y obliga a matar el proceso."
metadata: 
  node_type: memory
  type: project
  originSessionId: 6b882b35-31bc-4131-8cd4-4b099f670c25
  modified: 2026-09-21T22:34:13.521Z
---

Pedido del operador (21-09): pantalla aparte con el libro de MNQ en vivo al lado del heatmap, como la usan los profesionales, y despues que le enseñe.

**Lo armado y verificado en pantalla (8.0.14.399):**
- Home → Smart DOM → "MNQ Continuous" (#MNQZ6). Se acopla arrastrando la barra del DOM sobre el heatmap y soltando en la flecha DERECHA de la cruz: queda graficos | heatmap | DOM. Vista previa antes de soltar con left_mouse_down + mouse_move + captura.
- Plantillas en %APPDATA%\ATAS\SmartDOM\Templates: `MNQ Libro Noche.dts` (Bids/Asks Filter 50 = p90 Globex) y `MNQ Libro RTH.dts` (Filter 70 = p90 RTH). Base Limit Tracking sin Queue (es emulada), Depth changes de 55 px, filas 16, auto-centrado por 10 ticks, One-Click apagado, Simple Order placing apagado en Bids y Asks, Locked=true (leido del JSON).
- Link Price Axis: el menu del DOM se abre por UIA (ExpandCollapse sobre BarSubItemLinkPriceAxisLinkButton); "Link to Left" lo vincula con el heatmap. Con el vinculo el DOM toma la escala del heatmap (quedo 4x = 4 ticks por fila); zoom en el heatmap cambia las filas del DOM. Corrimiento de ~1 fila visto con el mercado en pausa: medir en rueda.
- Umbrales medidos con libro-vivo-*.csv (82.169 s): nivel mas cargado a 5 pts RTH p90 70 / p99 138; Globex p90 47 / p99 125; punta mediana 5-8. El RTH sale casi todo del 18-09 (vencimiento): provisorio. Scripts libro_umbrales.py y libro_dos_regimenes.py en el scratchpad de esa sesion.

**Estado final verificado en vivo (21-09 19:09, workspace guardado):** DOM desvinculado del heatmap. Con "Link to Left" el DOM toma la escala y la posicion del heatmap, pierde su auto-centrado y quedo mostrando 30785-30795 con el precio en 30801. Ahora va a 1 tick por fila (UseAutoScale=false), auto-centrado por 10 ticks, barras de tamaño en Bids/Asks (ShowHistogram) y Depth changes con UseAutoClear=true (sin eso acumulan para siempre). Las dos plantillas ya estan editadas asi (copias .bak al lado). El fondo gris (#A9A9A9, FilterColor) marca los niveles que PASAN el filtro: visto con 59 contratos a filtro 50.

**El modulo Heatmap de 8.0.14 NO sigue al precio solo** (llega en 8.0.15 beta): con un salto de 18 pts quedo mostrando 30795-30812 con el precio en 30826. Se arregla prendiendo la "A" (auto-escala) arriba a la derecha de su eje de precio: se reencuadra y acompaña (verificado 19:32, guardado 19:33). Ojo tambien: PrintWindow sobre paneles OpenGL (heatmap/DOM) puede devolver un cuadro viejo; para leer numeros usar la captura de computer-use.

**La ventana del chat del operador esta encima de la columna del DOM** (Claude en 1052..1588 x 20..748 fisico): los clics ahi se los lleva el chat. Correrla con MoveWindow y DEVOLVERLA enseguida. Mientras esta corrida, lo que el operador tipea va a ATAS: el 21-09 entraron "Cancel All" y "Sell market click" (sus atajos de una letra) y los freno "Trading is locked". Avisarle que no escriba antes de correrla. Para capturas sin moverla: PrintWindow(hwnd, hdc, 2) sobre la ventana principal de ATAS.

**La trampa que costo un reinicio:** cerre la ventana flotante de Replay (abierta por un clic mio errado) con `SendMessage WM_CLOSE`. El panel quedo en el layout sin ventana y TODO guardado de workspace fallo ("This Visual is not connected to a PresentationSource" en SaveLayout), incluso "Close without saving". Hubo que `Stop-Process` y se perdio todo lo hecho despues de las 16:45, incluido un grafico "MNQ 5m" del operador.

**Why:** el WM_CLOSE a dialogos modales (settings) anda; a paneles del DockLayoutManager (Replay, Smart DOM flotante, cualquier CustomFloatGroup) NO.
**How to apply:** paneles de ATAS se cierran con su propia X o por UIA (PART_CloseButton / toggle del boton del ribbon), nunca por mensaje. Despues de armar algo, guardar enseguida (Home → Workspaces → Save → Yes) y verificar que el .ws cambio de hora. El operador usa ATAS a la vez que yo: si aparece un menu contextual o una pestaña que no hice, frenar los clics. Ver [[clics-que-no-llegan-y-loops]], [[verificar-yo-no-el-usuario]], [[cambios-atas-de-a-uno]].
