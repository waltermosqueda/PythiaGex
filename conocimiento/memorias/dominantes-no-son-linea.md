---
name: dominantes-no-son-linea
description: "GAMMAlito NO dibuja puntos cruzando el gráfico: los puntitos son círculos SOBRE las barras del perfil, el rastro de cada strike. Verificado en 1920x1080 sin comprimir."
metadata: 
  node_type: memory
  type: reference
  originSessionId: 15e03ce1-51d8-43ea-8c35-a2fa1a4b8145
  modified: 2026-09-03T20:47:16.056Z
---

**Corregido el 2026-09-03.** Una versión anterior de esta memoria afirmaba lo
contrario, con mediciones que resultaron estar contaminadas. Queda registrado el
error porque explica cómo no volver a cometerlo.

## Lo que dibuja el producto, verificado

Cuadros de **1920x1080 en PNG sin comprimir** del video horizontal *"Te explico
en Vivo el Max Change"*, donde la barra de título dice
`GAMMAlito - Gexbot, OrderLineDecorator` sobre **MES de 30 segundos**.

**El cuerpo del gráfico tiene sólo velas y tres líneas de nivel. Cero puntos.**

Los puntitos están en los **bordes**: son **círculos grises de distinto tamaño
sobre cada barra del perfil**, a distintas posiciones a lo largo de la barra.
Algunos caen **fuera** de la barra, o sea más allá de donde termina: ese strike
antes era más grande y se encogió.

O sea que son **el rastro de cada strike sobre su propia barra**. Adentro de la
barra = encogió. Afuera = creció. En el proyecto eso es `Estela()`.

Las **dominantes** propiamente dichas son **zonas**, no puntos — lo dice su
propio blog: *"concentraciones clave de gamma entre los Major levels"*.

## El error, y la trampa que lo causó

La versión anterior sostenía que los puntos formaban bandas que ondulaban 47 a
196 px con 59–89 % de alturas únicas. **Esas mediciones estaban contaminadas.**

Se hicieron con detección de color ámbar sobre **shorts verticales comprimidos**,
y en esos videos **las velas son NARANJAS**. El detector estaba contando cuerpos
de velas como si fueran marcas del indicador. Separando por forma: de 43
componentes ámbar en un cuadro, **14 eran velas**, y de los 29 restantes varios
también.

**La regla que sale de acá:** antes de medir un color en una captura, verificar
qué MÁS en esa pantalla tiene ese color. Y preferir siempre material sin
comprimir y a resolución completa: los shorts verticales están escalados y con
artefactos de JPEG que inventan píxeles en los bordes.

## Consecuencia

La banda de puntos por vela que se llegó a implementar **no viene del original**.
Queda apagada por defecto, con la explicación en su propia descripción. No está
mal calculada: simplemente no es lo que hace el producto.

Y lo más caro del error: en el rediseño visual se había **apagado la estela** por
considerarla ruido — justo la única cosa que sí era el rastro de puntos que el
operador venía pidiendo desde el principio.

**Why:** se construyó una feature entera sobre una medición mal hecha, y se
guardó en memoria como si fuera verdad. Una memoria equivocada es peor que
ninguna.

**How to apply:** `VerEstela` encendido es lo correcto; `VerPuntosDominantes`
apagado. Ver [[radar-dominantes-bigtrades]] y [[ensenar-con-capturas-reales]].
