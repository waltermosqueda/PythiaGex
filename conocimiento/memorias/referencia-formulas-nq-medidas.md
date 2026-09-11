---
name: referencia-formulas-nq-medidas
description: "Las formulas del grafico de NQ de la referencia, medidas contra sus capturas en vivo del 2026-09-11 10:55 ET con la cadena cruda de QQQ bajada al mismo minuto: barras = gamma x volumen NETO del dia de QQQ 0DTE (R2 0,99), strike x razon NQ/QQQ del momento, majors = extremos, zero = cambio de signo interpolado (no repreciado), dominantes = las 2 barras mas largas; NDX no suma; el perfil derecho sigue sin formula."
metadata: 
  node_type: memory
  type: project
  originSessionId: 65e7848c-844c-4b37-8346-096df7f51a44
  modified: 2026-09-11T15:38:14.822Z
---

Medido el 2026-09-11 (12:10-13:00 local) sobre cinco capturas del operador del
grafico NQ 1 min de la referencia (10:51-10:56 ET, rotulo "QQQ · 0DTE") y la
cadena cruda de QQQ y NDX de CBOE bajada a las 11:10 ET (retraso 15 min = dato
de 10:55, el mismo minuto de las capturas). Evidencia, scripts y resultados en
`Escritorio\ATAS nada\_referencia\2026-09-11-NQ-capturas\` (fuera de los repos).

## Lo que reproduce (dos capturas, 16 y 11 barras)

- **Grilla:** una barra cada 41,1 pts de NQ = 1 strike de QQQ. Posicion de cada
  barra = strike x (NQ/QQQ del momento), razon 41,09-41,10. No usan base ni NDX.
- **Largo de la barra izquierda** = |gamma x (vol calls - vol puts)| del 0DTE de
  QQQ, una sola escala para verdes y rojas (~19 M por pixel): R2 0,990 y 0,992,
  error mediano 5 %, color (signo) correcto en 27 de 27. Con gamma de CBOE o con
  Black-Scholes propio da igual. Descartado con el mismo ajuste: OI (R2 0,3),
  volumen sin gamma (0,75), bruto call+put (0,57), NDX solo (R2 ~0), y la SUMA
  QQQ+NDX (0,92, peor que QQQ solo). O sea: el grafico de NQ es QQQ solo, no un
  cruce de libros.
- **Lineas:** verde = strike con mayor GEX por volumen (717 -> 29.464), roja =
  menor (714 -> 29.340). Son nuestros mpVol/mnVol.
- **Zero gamma (linea amarilla fina, 29.408):** el cambio de signo del perfil por
  strike interpolado entre 715 (-3,7 B) y 716 (+2,2 B) = 715,62 -> 29.407. El
  cruce REPRECIADO (nuestro Cruce()) da 716,17 -> 29.429: no es ese.
- **Dominantes (guiones amarillos):** las DOS barras mas largas en valor absoluto
  sin importar el lado: 714 y 715 (ambas rojas, ambas debajo del precio). Nuestro
  "una por lado" habria puesto 717 arriba. Con el mismo libro, nuestro nucleo
  elige lo mismo si se desactiva UnaPorLado.
- **Rotulos grises del eje (29.611,5 / 29.399 / 29.307,5):** NO son niveles: son
  la herramienta de posicion larga del operador (objetivo / entrada / stop), el
  rectangulo verde y rojo del grafico. No perseguirlos.
- **Pelotitas:** en las puntas o cerca, como dice la definicion; sin serie
  temporal de QQQ no se puede verificar el 15/5/1.

## Lo que NO reproduce (abierto)

- **Perfil derecho (purpura/cyan):** 14 barras medidas; ninguna formula estatica
  da: convexidad dGEX/1 % por vol (R2 0,4) o por OI (0,7), GEX por OI 0DTE (0,8
  pero el color falla 8 de 14), GEX 90 d, delta/vanna/charm por vol u OI, libro
  del lunes o de toda la semana. La forma (715 >> 714, 719 grande, 720 vacio) no
  se parece a ningun libro del momento: hipotesis = un cambio en el tiempo (Max
  Change de N minutos). Hace falta archivar QQQ por minuto para probarlo.
- "Quant Signal gamma positive · 1,0K/1,6K" cambia en 16 s: no es el neto del OI.

## Por que ATAS no se parecia ese dia

Nuestro MNQ dibujaba el libro de opciones de **NQ por Rithmic** (otro mercado:
3.649 contratos operados vs 1.815.099 de QQQ 0DTE) y ademas **sin 0DTE** desde las
10:16 ET (ver [[nq-rithmic-pierde-0dte]]). La cadena ancha de NQ es NDX, con
strikes de 5-10 pts: otra grilla. Coincidir era casualidad (strikes redondos).

**Why:** el operador sentia que "algunas veces acertaba y otras estaba
lejisimos" y sospechaba un cruce NDX+NQ+QQQ. Medido: es QQQ solo, por volumen.

**How to apply:** si se quiere el mismo dibujo en MNQ, la fuente es QQQ 0DTE por
volumen con razon NQ/QQQ (como SPX para ES), zero por cambio de signo, dos
dominantes por tamaño. Es decision del operador (que libro manda: ver
[[dos-libros-distintos]]). Primero archivar QQQ cada minuto en `archivar_cadena.py`.
Ver [[anatomia-referencia]], [[nq-libro-propio-pesa]], [[laboratorio-formulas]].
