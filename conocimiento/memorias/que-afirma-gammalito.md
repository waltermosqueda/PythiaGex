---
name: que-afirma-gammalito
description: "GAMMAlito explica sus lineas en video: NO dicen que el precio rebote. Dicen que ahi se acelera el tape. Estabamos midiendo la afirmacion equivocada."
metadata:
  type: reference
---

Del video **"¿Que significan las lineas de GAMMAlito?"** (1:08, NinjaTrader,
2026-09-04). Reconstruido de los subtitulos quemados, cuadro por cuadro.

## Lo que declaran, textualmente

Las barras marcan **"los nodos donde esta mayormente expuesto el market
maker"**. Ahi el creador de mercado **"va a cubrir su cartera"**, y lo que va a
pasar es que **"va a aumentar la velocidad del tape"**.

Y sobre como leerlas: lo que manda es **cual de las lineas es mas LARGA**. El
largo codifica la concentracion de GEX.

## POR QUE ESTO IMPORTA MAS QUE CUALQUIER FORMULA

**No afirman que el precio rebote en el nivel.** Afirman que ahi se ACELERA LA
ACTIVIDAD, porque es donde la mesa tiene que rehedgear.

Nosotros veniamos juzgando 19 formulas con la pregunta "se da vuelta el
precio?", y todas perdieron o empataron contra el placebo. **Esa no es la
afirmacion que ellos hacen.** Es posible que las formulas estuvieran bien y la
vara estuviera mal.

## Lo que se midio y por que no alcanza

Se probo con el volumen de opciones por minuto (`vc` + `vp` de las velas
archivadas), normalizado por la mediana de su entorno de +-20 min para sacarle
la forma horaria, y contra placebo.

Resultado: la mejor formula dio **1,046 contra 1,000** del placebo. Un 4,6 % de
exceso. Con minutos consecutivos autocorrelacionados y 13 formulas probadas,
**no es un hallazgo**.

**Pero se midio un sustituto, no la afirmacion.** Ellos hablan del tape de
FUTUROS -- transacciones en MNQ/ES -- y se midio VOLUMEN DE OPCIONES, que es lo
unico archivado. Son cosas distintas.

## El experimento que si probaria la afirmacion

El operador **tiene el tape de futuros en ATAS**, a 157 ms, con footprint y
delta. Hay que instrumentar el indicador para que anote la actividad del tape
cada vez que el precio entra en un nivel, y acumular sesiones. Es el
[[centinela-que-mide]] pero midiendo ACTIVIDAD en vez de rebote.

Eso no se puede backtestear con lo archivado: hay que registrarlo hacia
adelante.

## Detalles visuales confirmados

- Las etiquetas de los niveles son **"Major Positive"** y **"Zero Gamma"**.
- Cada barra lleva **tres puntos redondos de tamano decreciente** adentro,
  grande a la izquierda. No se establecio que codifican.
- El panel tiene selectores de temporalidad **H1** y **M15**.

**Why:** doce horas de laboratorio midiendo rebote pueden haber respondido bien
una pregunta que nadie hizo.

**How to apply:** ver [[laboratorio-formulas]] y [[pelotitas-son-eventos]].
