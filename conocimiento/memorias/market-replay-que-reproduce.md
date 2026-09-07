---
name: market-replay-que-reproduce
description: El Replay de ATAS reproduce la cinta del futuro pero NO la cadena de opciones; y el modo por defecto inventa las operaciones.
metadata: 
  node_type: memory
  type: project
  originSessionId: 15e03ce1-51d8-43ea-8c35-a2fa1a4b8145
  modified: 2026-09-04T22:06:29.344Z
---

Probado el 2026-09-04 con una muestra chica de Market Replay sobre MES.

**Lo que el Replay SI reproduce:** las velas y la cinta del futuro. Escribe
timestamps en **UTC** en el centinela (puesto 23:30 con el huso en UTC-3, las
filas salieron 02:28 UTC del dia siguiente). El campo del dialogo cambia de
huso solo entre UTC+0 y UTC-3 segun el momento; no discutir con la pantalla,
verificar contra el sello del archivo.

**Lo que el Replay NO reproduce: la cadena de opciones.** El indicador sigue
leyendo la cadena de HOY mientras las velas son de otro dia. Resultado: el
mapa de hoy dibujado sobre el precio de ayer. El campo `niv` del centinela
queda inservible en replay, y usarlo seria medir un placebo creyendo que es el
nivel. Para analizar un dia pasado hay que tomar los niveles de las fotos
archivadas de `datos/cache` de ESE dia. Ver [[laboratorio-formulas]].

**Los tres modos de "Market data type", y cual importa:**

1. `Candles and the generated best prices` — el que viene por defecto. ATAS
   **genera** la cinta a partir de velas. El `Ticks` de la vela (o sea `ops`,
   la velocidad del tape) es **sintetico**: no sirve para medir actividad.
2. `Ticks and the generated best prices` — cinta **real**, operacion por
   operacion, con puntas generadas. **Es el que hay que usar** para medir
   velocidad de cinta.
3. `Ticks and the DOM (Level2)` — agrega el libro completo. Es el mas pesado y
   el sospechoso de haber tildado ATAS la vez anterior. No hace falta.

**Costo medido:** 30 minutos de MES en modo 1, a 10x, tardaron ~2 minutos y
escribieron 29 filas de un minuto sin trabar nada.

Conectar el replay **desconecta el dato online**. De noche o fin de semana no
cuesta nada; con el mercado abierto si. Ver [[cambios-atas-de-a-uno]].
