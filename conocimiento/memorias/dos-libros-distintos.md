---
name: dos-libros-distintos
description: "El indicador alterna entre DOS mercados de opciones distintos -- SPX y ES -- y cada vez que cambia, todos los niveles saltan 23 a 33 puntos sin que el mercado se mueva."
metadata:
  type: project
---

Medido el 2026-09-04 sobre 1140 lecturas de una rueda americana. **Es el
problema de integridad mas grande encontrado hasta ahora.**

## Son dos libros, no dos mediciones del mismo

La cadena "ancha" de CBOE son opciones de **SPX**. La cadena "viva" de Rithmic
son opciones sobre el futuro de **ES**. El indicador usa la viva cuando esta y
cae a la de CBOE cuando no, **alternando entre los dos varias veces por hora**.

En la MISMA ventana de strikes y el MISMO momento:

```
                     libro de ES      libro de SPX     diferencia
  call wall            7740.00          7832.51         +92.51
  put  wall            7710.00          7707.51          -2.49
  neto                -0.620 mil M     +5.865 mil M   SIGNOS OPUESTOS
  interes abierto      87.855          684.984          7,8 veces
  |GEX| total          5,361 mil M     43,033 mil M     8,0 veces
```

**De los 6 strikes mas fuertes de cada libro, coinciden CERO.**

## Lo que cuesta el alternar

Salto de una lectura a la siguiente, con el PRECIO como control:

```
  nivel                sin cambiar de libro    AL CAMBIAR DE LIBRO
  el precio (control)      0.25 pts                0.25 pts
  zero gamma               0.18 pts               28.22 pts  (max 278)
  call wall                0.00 pts               33.10 pts  (max  92)
  put  wall                0.00 pts               23.75 pts  (max 117)
```

El precio no se mueve en esos instantes: **el salto es puro cambio de libro.**
El regimen (signo del neto) se dio vuelta en 3 de 88 cambios.

## NO es la ventana de strikes

Se probó recortando la cadena ancha a exactamente la misma ventana que la
angosta: el cruce se movio **0,38 puntos** y la brecha con la viva quedo en
**9,78**. La ventana no explica nada; el libro explica todo.

Esto **refuto mi propia hipotesis** del dia, que decia que la ventana angosta
corria el zero gamma. Falso.

## El error que introduje y ya revertí

Creyendo lo de la ventana, puse el zero gamma a salir de la cadena ancha
mientras los muros seguian saliendo de la viva. Resultado: **el zero de un libro
al lado de los muros del otro**, un mapa que no describe ningun mercado. El
operador lo vio en pantalla al toque ("para mi el gamma zero quedo defasado").
Revertido con el ajuste `ZeroDeCadenaAncha` en apagado.

Regla que queda: **todos los niveles del MISMO libro, siempre. Mezclar es peor
que cualquiera de las dos opciones.**

## Lo que falta decidir (es del operador)

Que libro manda, y no volver a alternar:

- **SPX**: 8 veces mas grande, es el que mira la industria para el mapa de gamma
  del S&P, y sus mesas cubren con futuros de ES. Pero llega 15 min tarde.
- **ES**: directamente sobre el instrumento que opera, en vivo, pero 8 veces mas
  chico y con solo 47 strikes.

Lo que NO se puede seguir haciendo es cambiar de uno a otro en el medio.

**Why:** el proyecto existe porque los tableros ajenos mienten por omision; esto
es una omision propia y de las gordas.

**How to apply:** ver [[gex-formula-auditada]], [[cadena-es-en-vivo-rithmic]] y
[[costo-real-del-retraso]].
