---
name: estrategia-ronda-6
description: "22-09: ronda 6 (herramientas propias: perfil, footprint fino, VWAP anclado, gamma+heatmap+DOM, confluencia): nada sobrevive; P3 single prints de ayer t 3,32 en diseno (n 17) y -15,6/op en CONFIRMAR MES (n 10, el espejo gano); S3_3 raya de gamma rota sin reposicion -0,80 fuera de muestra."
metadata: 
  node_type: memory
  type: project
  originSessionId: 6b882b35-31bc-4131-8cd4-4b099f670c25
  modified: 2026-09-22T06:08:12.390Z
---

`PythiaGex\laboratorio\dom\ronda6`. CONFIRMAR MES (2026-03..06) abierto SOLO para P3 y T2; FINAL sin abrir salvo el
intento de S3_3 (sin rayas de gamma antes del 19-08-2026: no medible).

- P3 (revisita de single prints del TPO de la rueda de ayer, limitada en el borde, cierre 15:55): diseno n 17 +19,68 t 3,32,
  88 % dias; CONFIRMAR n 10 -15,62 (2 de 10), espejo +14,62; revisita 12 % vs 28 % esperado. Muerta.
- S3_3 (gamma NEG, la raya se rompe <30 s y nadie repone en el libro; MNQ): diseno n 93 +1,62 t 2,03 (post-hoc, pico en
  3 pts); fuera de muestra con llenado exacto n 29 -0,80; depende de 1-2 ticks de llenado y de la fuente de rayas
  (rebobinada M2 da -1,93). Refutada por el esceptico. Queda solo la etapa B pre-registrada (S_10_PREREGISTRO_ADELANTE.md,
  desde el 23-09 con el grabador, sin tocar parametros), juzgarla una vez, no presentarla como estrategia.
- T2 (puntaje de confluencia de ruptura, percentil 90 walk-forward): CONFIRMAR -1,67 n 25, peor que rupturas al azar.
- Descartadas en diseno: POC virgen (no atrae: 26 % vs 29 %), maximo/minimo pobre, mudanza del POC, escalera de imbalances
  (mejor de 51 celdas t 0,55), subasta terminada, absorcion con footprint, delta contra el cierre, VWAP anclado a extremos
  (el ancla verdadera peor que una al azar).

**Why:** patron de todas las rondas: lo que da t 2-3,3 en diseno con n chico se da vuelta fuera de muestra. Es ruido.
**How to apply:** exigir n grande en diseno (>= 60) antes de gastar un tramo sellado; ver [[estrategia-ronda-4-5]],
[[busqueda-estrategia-sin-parar]].
