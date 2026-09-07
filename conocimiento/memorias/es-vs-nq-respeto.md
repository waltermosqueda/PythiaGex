---
name: es-vs-nq-respeto
description: "Medido el 2026-09-07: el NQ 'parece' respetar mas los niveles porque su grilla de strikes es el doble de densa y se mueve 1,7 veces mas; contra placebo las dominantes de NQ NO ganan (62 % contra 65 %, 29 toques, 10 strikes). En ES no hubo ni muestra: un solo strike visitado en el dia."
metadata:
  type: project
---

Pregunta del operador (2026-09-07, 05:10): "los numeros se respetan mucho mas
como soportes y resistencias en NQ que en ES; toman ES de contexto y atacan
NQ". Se midio con `laboratorio/es_vs_nq.py` sobre lo que el indicador grabo
en ATAS (centinela M5 de MES y MNQ, misma ventana; y el AUDIT por minuto de
4 dias), mismo metodo que el laboratorio, cada instrumento contra su placebo
en otro strike y con tolerancias a escala del rango de cada uno.

Lo que dio (sesion de Labor Day, sin RTH, mas 4 dias de log):
- ES: en todo el dia el precio visito UN solo strike dominante (7706). Sus
  niveles estaban a 30-110 puntos en un dia de 25 puntos de recorrido.
  Sin muestra: 0 a 1 toques por tipo de nivel.
- NQ: visito 5 strikes dominantes (29570-29630) y en 4 dias junto 29 toques
  sobre 10 strikes. Freno 62,1 % contra placebo 65,0 %: ventaja -2,9 pp.
  No le gana a una linea cualquiera.
- Por que se VE mas respeto en NQ: la grilla de NDX cerca del dinero es de
  10 puntos (0,034 % del precio) contra 5 puntos de SPX (0,065 %): el doble
  de densa. Y el NQ se movio 0,51 % en el dia contra 0,32 % del ES (rango de
  vela de 5 min 0,049 % contra 0,029 %). Mas lineas y mas movimiento = el
  precio esta tocando algo todo el tiempo, y el va-y-viene normal parece
  rebote. El placebo lo desenmascara.
- "ES toca, NQ se da vuelta": un solo momento coincidente; no se puede
  contestar hasta tener dias con rango en RTH.

**Why:** el operador lo dio por hecho ("no es una sensacion"). Sin placebo
cualquier linea parece respetada; con placebo, en NQ no hay ventaja y en ES
no hay ni muestra.

**How to apply:** volver a correr `python laboratorio/es_vs_nq.py` cuando
haya dias de RTH; la vara es 15 strikes distintos por instrumento. Mientras
tanto, tratar los niveles de MNQ como mas flojos (base cruda +-6 pts y libro
NDX solo, ver [[nq-libro-propio-pesa]]) y no como mas respetados. Ver
[[laboratorio-formulas]], [[la-muestra-son-niveles-no-minutos]].
