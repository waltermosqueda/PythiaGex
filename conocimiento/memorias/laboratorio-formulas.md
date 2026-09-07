---
name: laboratorio-formulas
description: "El laboratorio que juzga cualquier formula de niveles contra un placebo. Diecinueve probadas: la que usamos hoy PIERDE, y solo ganan las basadas en el volumen del dia."
metadata:
  type: project
---

Construido el 2026-09-04. Vive en `laboratorio/` y es la herramienta permanente:
**ninguna idea nueva toca el indicador sin pasar por aca.**

## Como juzga

Dos preguntas, las dos hacen falta:

1. **Es un nivel o es un eco?** Beta contra el precio. Si da cerca de 1, el
   nivel sigue al precio y no puede avisar nada por adelantado.
2. **Lo respeto el precio?** Toques contra maximos y minimos de velas de un
   minuto, y **siempre contra un placebo**: la misma formula corrida 11, 19 o
   31 puntos. Sin placebo cualquier linea parece respetada, porque el precio va
   y vuelve solo.

## Resultados sobre 300 fotos del 2026-09-03

```
formula                        beta  toques  freno  placebo  ventaja
  D volumen puro, sin gamma     1,00     39   61,5%   19,1%   +42,4
  B gamma x volumen de hoy      0,78     78   57,7%   35,7%   +22,0
  G solo 0DTE                   1,01     81   67,9%   49,3%   +18,6
  K delta x OI                  1,02     63   58,7%   51,2%    +7,5
  A gamma x OI  (LA QUE USAMOS) 1,05     35   68,6%   72,6%    -4,0
  F lejos del precio            1,20     15   46,7%   62,2%   -15,5
```

**La formula que usamos PIERDE contra su placebo.** Su 68,6 % parece bueno
hasta que se ve que una linea cualquiera en esa zona saca 72,6 %.

Lo que gana es **donde esta la actividad de HOY**, no donde esta el interes
abierto de ayer.

## Lo que se probo y NO funciono

- **Flujo firmado** (Lee-Ready sobre bid/ask/ultimo precio). Se puede firmar el
  **87 %** del volumen nuevo, y el metodo es temporalmente valido (el ultimo
  trade esta a menos de 1 min del momento efectivo del dato). Pero como niveles
  **pierde por 20 y 29 puntos**. Medir el signo y producir buenos niveles son
  dos cosas distintas.
- **Charm como nivel**: pierde por 25, 11 y 25 puntos en sus tres variantes.
- **Centro de gravedad**: refutado antes, correlacion 0,995 con el precio.

## Trampas de medicion que me comi (y quedan documentadas)

- **Vencimientos a medianoche.** El cargador ponia el vencimiento a las 00:00 y
  el 0DTE verdadero daba tiempo negativo: quedaba excluido. Una tabla entera
  salio mal. Los SPXW liquidan a las **16:00 ET = 20:00 UTC**.
- **Test degenerado.** Para ver si el charm agregado anticipa direccion medi
  "acierto de signo", pero el charm fue negativo en TODAS las observaciones. El
  control barajado dio exactamente lo mismo, 41,9 %, las 200 veces. Un signo que
  nunca cambia no puede predecir un signo.
- **Muestra chica disfrazada.** Una variante del flujo firmado dio 92,3 % contra
  48,7 %... con **13 toques**. Con 19 formulas probadas, que una brille por azar
  es lo esperable.

## El cuello de botella: no hay segundo dia

El resultado del volumen se apoya en **una sola rueda**. El 31 de agosto y el 1
de septiembre tienen entre 0 y 4 toques por formula: no alcanzan.

La causa: `datos/cache` lo llena el **lado de Python**, no el indicador de ATAS.
El 2026-09-03 alguien lo dejo corriendo todo el dia (51 fotos por hora, de 08 a
22 UTC, sin cortes) y por eso ese dia sirve. Los demas dias son recoleccion
manual y salteada.

**Sin mas dias, nada de esto se puede confirmar ni descartar.**

## Otro hallazgo lateral

El retraso de CBOE quedo medido por **tres caminos independientes**: el reloj
(902 s), el cruce de precios contra velas historicas (15 min, el ajuste salta de
23 % a 79 %) y la antiguedad del ultimo trade (15,8 min, apretado entre 15,3 y
17,1). Los tres dan lo mismo.

Y el delta de CBOE se reproduce con **r = 0,0375 y dividendo q = 0,012**: el
error mediano baja de 0,0072 a 0,0036. En el GAMMA eso cambia menos del 1 % y
**no mueve ningun muro** -- medido en 5 fotos.

**Why:** el proyecto existe para no creerle a los dibujos; el laboratorio es lo
que permite no creerle tampoco a los propios.

**How to apply:** `laboratorio/puntuar.py` corre todo. Ver
[[dos-libros-distintos]] y [[gex-formula-auditada]].
