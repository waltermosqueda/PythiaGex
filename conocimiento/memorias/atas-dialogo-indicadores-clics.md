---
name: atas-dialogo-indicadores-clics
description: "Como operar el dialogo Indicators de ATAS 8.0.15 con computer-use sin perder tiempo (06-10-2026): coordenadas SIEMPRE del cuadro a escala 1 (1456x819), los botones Add to chart/Apply/Close solo por UI Automation (InvokePattern / WindowPattern.Close), los items de la lista si aceptan clic, el tacho de la fila 'Added' solo tras hover previo, y Ctrl+I abre el dialogo del grafico con FOCO DE TECLADO (puede ser otro panel): abrirlo desde el boton 'Indicators' de la barra del grafico."
metadata:
  node_type: memory
  type: feedback
  originSessionId: f5879819-e2a4-405b-87b0-959ace090d7b
  modified: 2026-10-06T19:16:42.068Z
---

**LA CAUSA DE FONDO (captura del operador, 06-10 16:16):** la ventana del chat de Claude queda ENCIMA del lado derecho de los dialogos de ATAS
(botones Apply / Add to chart / Cancel, panel de ajustes): los clics ahi caen en el chat, no en ATAS. Las capturas de computer-use enmascaran
la ventana del chat (no esta en la lista permitida), asi que NO SE VE el solapamiento. Antes de clickear en ATAS: `powershell -File
herramientas/atas_al_frente.ps1` (SetForegroundWindow con el truco Alt) y recien despues los clics; o directamente UI Automation para los botones.

**Lo que costo 20 minutos el 06-10-2026** (instalando la Sonda API de la 3.0 y abriendo un grafico nuevo MNQZ6 2m):

1. **Escala de la captura.** `screenshot(scale 0.6)` devuelve una imagen de 874x491 pero el cuadro de coordenadas es 1456x819: leer los
   pixeles de la imagen chica y pasarlos como coordenadas clickea a 0,6x del lugar (un clic en "Indicators" del grafico cayo en
   "Layouts settings" de la barra principal). Regla: para clickear, capturar a `scale 1` y leer ahi; la 0.6 solo para mirar.
2. **Ctrl+I abre el dialogo del grafico que tiene el FOCO de teclado**, que puede ser el otro panel (abrio el de la 2.0 estando
   seleccionada la pestaña nueva de la izquierda). Abrirlo con el boton "Indicators" de la barra del propio grafico (hover 1 s y clic).
   Si el dialogo muestra la lista "Added" de otro grafico, cerrarlo SIN Apply (WindowPattern.Close) y no tocar nada.
3. **Botones del dialogo (Add to chart, Apply, Cancel, la X): el clic del mouse no les llega.** Si por UI Automation:
   `FindAll(Descendants, Name="Add to chart")` -> `InvokePattern.Invoke()`; idem "Apply". Cerrar con `WindowPattern.Close()` sobre
   el Window "Indicators" (ancho > 300). Cancel despues de Apply REVIERTE (memoria gamma-hoy-1-9).
4. **Items de la lista de la biblioteca y de 'Added' SI aceptan clic** (seleccion). El buscador acepta clic + `type`.
5. **El tacho de una fila 'Added'** (iconos copiar/ojo/tacho/estrella a la derecha de la fila, x≈761/779/798, fila 1 y≈421, fila 2
   y≈445 a escala 1) solo respondio una vez: hover sobre la fila, hover sobre el icono 1 s, clic (el tooltip 'Delete' aparecio).
   Las otras tres veces no respondio (ni la tecla Suprimir). UIA no expone esos iconos (0 candidatos). Si no sale en dos intentos,
   dejarlo y seguir: un Depth Of Market de la plantilla por defecto no ensucia la comparacion de gamma.
6. **Grafico nuevo**: "+" de la barra de pestañas -> menu -> "Chart" -> "Manage instruments" (doble clic en el instrumento) -> nace
   con la plantilla por defecto (5 min, Depth Of Market, perfil de volumen); temporalidad con el desplegable "▾" de la barra del
   grafico -> M2 -> Apply (hover antes de cada clic).
7. **Connect del login**: el clic en el boton no funciona; clic en un campo del cuadro y Enter si. El `Invocar "Connect"` por UIA del
   instalador da "candidatos 0" en 8.0.15 (la ventana nueva con publicidad): dejarlo y hacer clic+Enter desde computer-use.

**Why:** cada reinicio + agregado de indicador se hace varias veces por sesion; sin esto se pierden 15-20 minutos cada vez.
**How to apply:** seguir estos pasos tal cual; actualizar `herramientas/instalar_3_0.ps1` para que el login use clic en campo + Enter
cuando `Invocar "Connect"` no encuentra candidatos. Ver [[compilar-indicadores-atas]], [[clics-que-no-llegan-y-loops]],
[[verificar-yo-no-el-usuario]].
