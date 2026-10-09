---
name: estrategia-ronda-4-5
description: "22-09: ronda 4 (25 reglas intradia en 117 sesiones de MES 5 min con delta: order flow, estructura, valor, tendencia, ML) no paso ni el diseno; ronda 5 cambia a replicar anomalias publicadas (momentum de la ultima media hora por gamma de dealers, fin de mes, pre-FOMC, deriva nocturna europea)."
metadata: 
  node_type: memory
  type: project
  originSessionId: 6b882b35-31bc-4131-8cd4-4b099f670c25
  modified: 2026-09-22T04:12:31.464Z
---

Ronda 4 (`PythiaGex\laboratorio\dom\ronda4`, dataset auditado MES_m5/m30/m60 + MNQ con rolls, VWAP, IB, noche,
cargador con candado de tramos r4_datos.py): tramos DISENO 2025-08-22..2026-02-27 (117 utiles), CONFIRMAR
2026-03..06 (79), FINAL 2026-07..09-21, EXTRA 2024-04..2025-08 en 30/60 min. Resultado: 0 de 25 reglas llegan a t>=2
ni en DISENO (footprint A1-A4 negativas, el filtro de delta apenas mejora; valor C1-C4 negativas; tendencia D1-D4 ~0;
ML con 43 variables walk-forward: mejor t 1,63 y un solo dia puso 43 % del total). CONFIRMAR/FINAL/EXTRA siguen SIN
ABRIR (sirven para la ronda que venga).

**Why:** con ~70 reglas intradia medidas en 4 rondas (MNQ con cinta y MES con 13 meses de delta), la conclusion es que
las lecturas intradia de order flow/niveles/horario no tienen ventaja despues de costos para un minorista. Coincide con
busqueda-gatillo-2026-09-17.
**How to apply:** no volver a minar reglas intradia de order flow sin una hipotesis nueva con mecanismo; el DOM queda como
herramienta de EJECUCION (limitada ahorra 0,15-0,36 pts, ronda 1 C1). Ronda 5 (`laboratorio\dom\ronda5`): anomalias
publicadas con parametros de los papers (Baltussen et al. 2021 momentum de la ultima media hora por gamma; Etula et al.
2020 fin de mes; Lucca-Moench 2015 pre-FOMC; Boyarchenko et al. deriva nocturna), historia larga de Yahoo + MES.
Ver [[estrategia-dom-ronda-1]], [[estrategia-ronda-2]], [[busqueda-estrategia-sin-parar]].
