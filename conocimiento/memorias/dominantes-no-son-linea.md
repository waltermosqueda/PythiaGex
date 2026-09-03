---
name: dominantes-no-son-linea
description: "Los puntos de las dominantes de GAMMAlito ondulan, tienen huecos y no cruzan el grafico: son una marca POR VELA, no una linea recta al nivel de ahora."
metadata: 
  node_type: memory
  type: reference
  originSessionId: 15e03ce1-51d8-43ea-8c35-a2fa1a4b8145
  modified: 2026-09-03T17:40:16.589Z
---

Medido el 2026-09-03 con deteccion de color sobre tres cuadros de GAMMAlito
sacados de los videos del propio operador, agrupando los guiones ambar en
bandas por altura. El lo dijo primero mirando la pantalla: *"no son una
secuencia siempre lineal, suelen ser random y si hay un patron lo decide el
mercado"*. Tenia razon.

```
banda de 17 guiones -- ondula  47 px =  9,4 alturas de guion
banda de 18 guiones -- ondula  80 px = 13,3 alturas
banda de 22 guiones -- ondula  88 px = 11,7 alturas
banda de 28 guiones -- ondula 196 px = 28,1 alturas
```

**Ninguna es recta.** Todas tienen huecos — el mayor de 352 px — y **ninguna
llega de punta a punta del lienzo**. El guion mide 7 a 13 px de ancho: **una
vela**.

## Lo que eso implica

Cada guion es **la dominante de ESA vela**, registrada en ese momento. Cuando el
nivel se sostiene la banda se hace densa; cuando se mueve, ondula; cuando en esa
vela ninguna zona califica, queda el hueco.

Dibujar en cambio una linea recta al nivel de AHORA cruzando todo el grafico
**miente**: pinta el nivel actual sobre velas de hace horas, donde ese nivel no
se habia medido. Es dibujar una hipotesis como si fuera un registro, que es
justo lo que este proyecto existe para no hacer. Ver [[auditoria-punta-a-punta]].

## El nucleo no es el punto medio

Al corregirlo hay una segunda trampa: el nucleo de la zona viaja en el campo
`fut`, **no** es `(desde + hasta) / 2`. En la zona del dia el nucleo estaba en
7759,07 y el punto medio daba 7749,07: **diez puntos de corrimiento**, la banda
dibujada donde no hay nada.

## Consecuencia práctica

Los puntos **se acumulan desde que arranca el indicador**. Al reiniciar ATAS la
pantalla queda casi vacia y eso es honesto, no un bug. Para llenar el pasado sin
mentir habria que **reproducir las fotos historicas de la cadena** (hay ~440 por
rueda en `datos/cache`), porque cada una es un registro real de ese momento.

**Why:** se estaba dibujando otra cosa con el nombre de dominantes, y encima de
una forma que afirmaba mas de lo medido.

**How to apply:** vive en `PuntosDominantes()` de
`atas/PythiaGexNiveles/GammaVivo.cs`; cada `Marca` guarda `Doms` e `Incs` del
momento de su vela. Ver [[radar-dominantes-bigtrades]].
