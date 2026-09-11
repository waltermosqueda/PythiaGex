---
name: la-muestra-son-niveles-no-minutos
description: "En una prueba de niveles la muestra util son los strikes distintos que el precio visita, no la cantidad de minutos."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 15e03ce1-51d8-43ea-8c35-a2fa1a4b8145
  modified: 2026-09-04T22:49:05.637Z
---

Medido el 2026-09-04 sobre 105 minutos de cinta real de MES del 2026-09-03.

**Why:** parecia una muestra decente -- 105 minutos, 94 observaciones, 26 contra
26 en los dos grupos. Pero en esos 105 minutos el SPX se movio **17,9 puntos** y
piso **4 strikes**: 7740, 7745, 7750 y 7755. Toda la comparacion "mucha gamma
contra poca gamma" se reducia a "el precio parado en 7750 contra parado en
7745". Un numero, una tarde. Juntar mas minutos del mismo tramo no agrega ni un
nivel.

**How to apply:** antes de reportar cualquier resultado sobre niveles, contar
**cuantos strikes distintos toco el precio** y decirlo junto al numero. Si son
menos de ~15, no hay prueba, hay anecdota. Para conseguir niveles distintos hace
falta rango o dias, no minutos.

Dos trampas que salieron en la misma auditoria y hay que controlar siempre:

1. **Placebo entre strikes.** Los strikes de SPX estan todos en multiplos de 5.
   Un placebo de 11, 19 o 31 puntos cae SIEMPRE entre strikes, asi que el nivel
   real tiene gamma *y* redondez y el placebo no tiene ninguna de las dos. Al
   cambiar a placebos de 15/25/35 -- que caen sobre otro strike -- se cayo casi
   todo: la concentracion de gamma paso de 20/6 fotos a 10/6, y la formula base
   de 21/7 a 9/7.
2. **Redondez de segundo orden.** Entre strikes tampoco es parejo: la cinta
   corre 1,102 sobre los multiplos de 50, 1,012 sobre los de 10 y 0,993 sobre
   los de 5, sin mirar gamma. Los strikes con mas interes abierto son justo los
   mas redondos, asi que hay que comparar dentro de cada clase de redondez.

**Control barato que hay que correr siempre:** partir la muestra al medio. Acá
el efecto dio -0,121 en la primera mitad y +0,392 en la segunda. Cambiar de
signo dentro del mismo dia es firma de ruido.

Ver [[laboratorio-formulas]] y [[que-afirma-referencia]].
