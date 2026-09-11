---
name: pelotitas-max-change-medidas
description: "Las pelotitas de la referencia, resueltas y medidas (2026-09-10): son la punta de cada barra hace 15/5/1 min (Max Change), en los dos perfiles; las nuestras estaban rotas por la base; el efecto 'adelantado' existe pero es chico (x2 sobre quieto, y decrecer tambien cuenta); las del modo BIGTRADE de la web son otra cosa."
metadata: 
  node_type: memory
  type: project
  originSessionId: 9345174c-7f69-4fe5-a793-976e41f2dc7c
  modified: 2026-09-10T07:23:46.002Z
---

**Que son (audio literal del creador, "Te explico en vivo el Max Change", 5:59):** tres
circulos por barra: grande = donde estaba la punta hace 15 min, mediana = hace 5, chica =
hace 1. Adentro de la barra = ese strike crece; afuera = decrece; juntas en la punta =
quieto; alineadas = crecimiento sostenido; desparramadas = mucho momento; desordenadas
= indeciso. "Lo mas importante no es la dominante sino la pelotita: es adelantado". Las
dibuja en el perfil de GEX y en el de convexidad (cuadros del video, 1080p). Las barras
son el VOLUMEN de opciones del dia por strike (0DTE a la izquierda, 90 DTE a la derecha).

**Por que "se mueven" cuando el precio sube o baja:** la barra es gamma x volumen, y la
gamma es maxima en el strike donde esta el precio. Al acercarse el precio a un strike su
barra crece (pelotitas quedan adentro); al alejarse, se achica (quedan afuera). Ademas
crecen por operaciones nuevas. Y el sube-y-baja vertical de 1,5 pts de las barras de
la referencia es la razon QQQ->NQ cambiando (lo mismo que nuestra base): no es informacion.

**Las nuestras (Gamma Hoy):** existian (VerPelotitas) pero buscaban la foto vieja por
precio del futuro; con la base cambiando no encontraban nada. Desde 1.6: por strike,
sembradas con el archivo al arrancar, en los dos perfiles, con el mouse sobre el pasado,
y un log "PELOTITAS" cada 5 min con ahora/hace1/5/15 y el veredicto. Verificado en
pantalla y en el log (K7700 de ES "DECRECE, las tres afuera" mientras el precio se alejaba).

**Medido ("es adelantado"), laboratorio/pelotitas_predicen.py, 4 dias por minuto:** un
strike con las tres pelotitas adentro (crecio 20 %+ en 15 min) pasa a ser dominante en
los 45 min siguientes el 9,5 % de las veces en ES (NQ 5,5 %), contra 4,4 % (2,8 %) si esta
quieto; pero uno que DECRECE tambien: 8,2 % (4,4 %). El vecino con la misma etiqueta
sube menos (7,4 %). Conclusion: la pelotita muestra DONDE cambia la exposicion y eso
duplica la chance de que ese strike sea la proxima dominante, pero nueve de cada diez
no llegan y la direccion del cambio importa poco. Es contexto, no gatillo.

**El otro tipo de pelotitas:** en la web, con el modo BIGTRADE, los circulos azules
dentro del histograma son operaciones grandes de opciones (varias por barra, aparecen y
desaparecen). No son el Max Change. Ver [[pelotitas-son-eventos]] (esa medicion era de
ESE modo) y [[anatomia-referencia]].

**Why:** el operador dijo "el creador le da mucha importancia y nosotros lo desestimamos
por no saber comprender como estan construidas". Ahora estan construidas igual y medidas.

**How to apply:** mirar las pelotitas como velocimetro de cada barra, no como nivel. Un
CV del video no fue confiable (zoom, scroll y dibujos del presentador): la definicion
sale del audio. Videos nuevos en Escritorio/ATAS nada/_referencia/nuevos-2026-09-10/.

**13 dias completos (2026-09-10 04:30):** ES CRECE 10,6 % / QUIETO 4,9 % / DECRECE 7,6 %
(vecino 8,0 / 4,0 / 6,1); NQ 6,0 / 2,7 / 4,2 (vecino 3,0 / 1,4 / 2,1). Misma lectura: x2,
del strike (le gana al vecino), y 9 de 10 no llegan.
