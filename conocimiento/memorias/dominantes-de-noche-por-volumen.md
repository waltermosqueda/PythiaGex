---
name: dominantes-de-noche-por-volumen
description: "REGLA del operador (17-09-2026): las dominantes de noche van por VOLUMEN, no por interes abierto; el default OI que puse en 1.10r (16-09, sin avisarle ni comparar en pantalla) mando las verdes a 130 pts la vispera del vencimiento trimestral y el lo tuvo que arreglar a mano. Ningun cambio de default que toque lo que ve en el grafico sin avisarle en el mismo mensaje y sin captura antes/despues."
metadata:
  type: feedback
---

**Lo que paso (17-09 noche):** con `DominantesDeNoche = InteresAbierto` (default que YO puse en Gamma Hoy 1.10r, 16-09 02:02, a partir de mi
lectura de [[dominantes-de-noche-resto-de-ayer]]) las verdes de NQ quedaron a +33/-92 del precio en la vispera del vencimiento trimestral: la
trimestral tiene 4.800-8.800 de OI en strikes redondos cada 50-100 pts y pesa 17x cualquier weekly (`verdes_52_por_vencimiento.py`, foto 23:11 UTC).
El operador cambio el ajuste a **Volumen** y las verdes volvieron a +33/-22 (estela 00:43 UTC, `"b":"vol"`, fuerzas 63 M / 29 M): por volumen las
rayas marcan donde se opero HOY (cerca del precio), por OI marcan las posiciones acumuladas (lejos esa noche). Sus palabras: "ojo, esto es vital
para el scalping; autocritica con lo que tocas la proxima sin revisar bien".

**Why:** el cambio de default lo hice de madrugada, con una hipotesis mia sobre "el resto de ayer", sin decirselo en el mensaje, sin captura
antes/despues y sin mirar varias noches. El lo detecto a ojo (cuarta y quinta vez que tiene razon sobre las rayas de noche, ver
[[regla-roja-roll-libro-vivo]]). Lo que el mira cada noche no es un parametro mio: es su herramienta de trabajo.

**How to apply:** (1) default en codigo = Volumen (commit del 17-09; ATAS ademas guarda su eleccion por nombre en el .ws). (2) Cualquier cambio
de default, regla de seleccion, radio, empate o color que cambie lo que se DIBUJA: avisarlo en el mismo mensaje con el nombre del ajuste, dejar el
valor anterior disponible en el menu, y mostrar captura antes/despues de la misma noche. (3) Si una noche las rayas se van lejos: primero
`verdes_51` + `verdes_52` (con `--vol` y sin) y leer que libro eligio el ("b" en la estela), despues opinar. (4) El OI sigue siendo informacion
valida (muro real de la trimestral en 29744 esa noche): ofrecerlo como capa o rotulo aparte, nunca reemplazando lo que el usa para scalpear.
