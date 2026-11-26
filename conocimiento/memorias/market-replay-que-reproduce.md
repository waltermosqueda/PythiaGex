---
name: market-replay-que-reproduce
description: "Replay de ATAS 8.0.15.306 (medido 26-11-2026): bar replay desde una vela; el footprint de cada vela cerrada sale IDENTICO a la historia en vivo en modo Ticks y en modo Candles; se maneja por UIA; desconecta el dato en vivo y al cerrarlo reconecta solo. NO reproduce la cadena de opciones."
metadata:
  node_type: memory
  type: project
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-26T22:12:56.106Z
---

**26-11-2026 (Thanksgiving, mercado parado), replay del 25-11 con el Superindicador 1.5 en el grafico limpio MES 1m:**
- Boton "Replay" del ribbon (UIA `RibbonReplayButton`, InvokePattern) -> modo replay ("[In replay mode]"): DESCONECTA las
  conexiones de trading/dato en vivo. Panel flotante "Replay - ...": Select module, **Select Bar** (bar replay: "Click the bar you
  want to start the replay from"; clic real en la vela), velocidad (0,1x a 1000x; la lista abre con ExpandCollapse.Expand),
  Reset to start, Play/Pausa, Step, Go to realtime, Settings, Close. Los clics de computer-use en ese panel NO llegan: todo por UIA.
- Settings: tipo de dato Candles (de fabrica) / Ticks / Ticks and DOM, y "Auto replay" (al llegar al final RECOMIENZA solo; me
  confundio una segunda pasada). Settings esta BLOQUEADO con el replay en marcha o en pausa: "Reset to start" lo libera.
- "Close" del panel sale del replay y ATAS RECONECTA solas las dos conexiones Rithmic en ~2 s.
- Medido vela por vela contra la historia en vivo del mismo dia: modo **Ticks 387 de 387 minutos identicos nivel por nivel** (y las
  dos pasadas iguales entre si); modo **Candles 188 de 188 identicos** (mismo delta y volumen). Candles trae cada vela entera de
  una; Ticks la arma operacion por operacion (para ver como se forma el delta). La cinta del replay (Ticks) calza exacta con el
  footprint. Velocidad real con Ticks ~100x en la primera pasada (9:15 a 17:00 NY en ~4,7 min).
- ATAS dejo mal SU area de valor de 1 vela de 387 en el replay (la del indicador sale del footprint y da igual).
- Error mio corregido con el dato: yo habia dicho que el modo Candles "inventaba" el footprint; medido, no (ver arriba).
- Quedo "Auto replay" APAGADO (estaba prendido): avisado al operador.

**Lo que NO reproduce (04-09, sigue valiendo): la cadena de opciones.** El indicador de gamma lee la cadena de HOY mientras las
velas son de otro dia: para analizar un dia pasado, niveles de las fotos archivadas de ESE dia. Ver [[laboratorio-formulas]].
La cinta grabada EN VIVO por el indicador pierde operaciones en rafagas (12 de 372 minutos del 25-11, siempre de menos; el
footprint y el replay estan completos). Ver [[superindicador-velas-delta-perfil]], [[cambios-atas-de-a-uno]].
