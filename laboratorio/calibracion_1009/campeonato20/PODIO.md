# PODIO — campeonato de los 20 puntos (calibración 09-10)

Para el agente principal. Escrito el 09-10 a las 07:16Z (04:16 hora argentina). Fechas en día-mes. Horas en Z (UTC), con la hora argentina entre paréntesis (ART = Z − 3).

**Edad de los datos.** Lo juzgado llega, de esta noche, hasta las 06:01Z en NDX, 06:06Z en QQQ y 06:10Z en NQ. En las combinaciones hay niveles hasta las 03:53Z y velas hasta las 06:13Z. Son estadísticas de 33 noches y 34 ruedas hacia atrás. **No son niveles para operar.**

**De dónde sale cada dato.**
- [familia]: lo midió el agente de esa familia (ndx, nq, qqq o combos).
- [esc1] / [esc2]: lo midió o lo reprodujo un escéptico.
- [acá]: lo medí al armar este podio, el 09-10 a las 07:1xZ.
- [supuesto] / [memoria]: no está medido en esta sesión.

---

## 0. Veredicto

**No sobrevive nada.** Ninguna serie de gamma le gana a una raya puesta al azar con la vara de 20 puntos fuera de muestra. Vale para NDX, NQ, QQQ, TQQQ, la 2.0 y las combinaciones.

- **Holm sobre el campeonato entero:**
  - MIRAR: 105 pruebas. Hay 6 con p ≤ 0,05, y el azar solo ya da unas 5,2. La mejor p de Holm es 0,419.
  - PRUEBA: 104 a 105 pruebas. Hay 1 con p ≤ 0,05 contra unas 5,2 esperadas. La p de Holm da 1,0 en todas.
  - Fuentes: [esc1][esc2]. Lo releí [acá] en esceptico2/datos/e03_holm.txt y esceptico1/datos/holm.txt.
- **Los conteos son correctos.** Los dos escépticos escribieron un juez propio desde cero. Con él reprodujeron exactos, evento por evento, los 10 casos con p < 0,06. El juez no es la fuente de ningún error. Tampoco hay fuga de futuro. [esc1][esc2]
- **Lo que asomó en MIRAR se explica sin gamma.** En R20_NDX fue ruido de la conversión. En NQ fueron los números redondos. En CONF, el parpadeo de un minuto. [esc1][esc2]
- **Esto no dice que el GEX no sirva para nada.** Dice que, con esta vara y esta muestra, las rayas no aguantan 20 puntos más seguido que una raya cualquiera. La muestra alcanza para ver ventajas de unos 8 a 10 puntos porcentuales, no de 4 o 5 (ver la sección 7).

---

## 1. La vara (lo que se juzgó)

- **Llegada:** la mecha toca la raya ±2 pts viniendo del otro lado: el techo desde abajo, el piso desde arriba. La raya vigente es la dibujada con datos hasta el minuto anterior.
- **Rota** (modo tolerante, el del operador). Cualquiera de estas tres:
  - un cierre de 1 min más allá de raya + 5, con cuerpo ≥ 4;
  - 3 cierres seguidos afuera;
  - más de 10 min afuera.
  - Las mechas que pinchan y vuelven no la rompen: eso es un falso rompimiento.
- **Resultado de cada llegada:**
  - ACIERTO20: recorre ≥ 20 pts a favor antes de romperse.
  - FALSA: se rompe antes.
  - Indefinida: pasan 120 min sin decidirse. No hubo ninguna en todo el campeonato, y las censuradas son casi 0.
- **Ventanas:** noche de 22:00Z a 13:30Z (19:00 a 10:30 ART); rueda de 13:30Z a 20:00Z (10:30 a 17:00 ART).
- **Partición:** los 2/3 primeros de las noches para MIRAR y el último 1/3 como PRUEBA, sin elegir nada mirándola.
  - **Ojo:** el corte no es el mismo en todas las familias. La PRUEBA de NDX arranca el 25-09, la de NQ el 30-09, la de QQQ el 01-10 y las combinaciones cortan serie por serie.

---

## 2. Tasa base del azar (punto 5)

Lo midieron las familias y lo confirmó esc1 con otro placebo.

- **El azar oficial.** Una raya al azar, con la misma cantidad de rayas por minuto y la misma distancia al precio que la serie real, cumple ACIERTO20 en el **36-41 % de las llegadas de noche** y en el **41-47 % de la rueda**.
- **Las corridas.** Las mismas rayas corridas ±11/±19/±31 pts dan casi lo mismo: 31-46 % de noche y 40-52 % de día.
- **El placebo de esc1.** Corre la serie entera cada noche entre 8 y 40 pts. Da 36-41 % de noche y 40-44 % de día.
- **En criollo:** una raya cualquiera que el precio toca aguanta 20 puntos **unas 4 de cada 10 veces de noche**, y algo más de día, sin saber nada de gamma. Por eso cualquier raya muestra más entradas falsas que aciertos: unas 1,6 falsas por cada acierto de noche.
- **Por qué es tan alta.** La vara cuenta el objetivo con la MECHA y la rotura con CIERRES. Además, si las dos cosas pasan en la misma vela, gana el acierto. [esc2]
- **La "expectativa" no es ventaja.** El +20 / −rotura da positivo en todas las series, de −1,3 a +6 pts por llegada, y **también en el azar** (de +0,8 a +2,9). Ese positivo lo fabrica la asimetría de la vara: 20 a favor contra una rotura mediana de 5 a 10. No sale del GEX. No incluye costos, deslizamiento ni la decisión de entrar. **No promete nada.** [familias][esc1][esc2]

---

## 3. Ranking honesto (punto 1)

### 3.1 Lo que supera al placebo en PRUEBA y sobrevive a los escépticos

**Nada.** La lista está vacía.

### 3.2 Las que asomaron y por qué cayeron

1. **R20_NDX_vol de noche** (la capa NDX de la 2.0). [ndx][esc1][esc2]
   - Pasó de 42,3 % contra 37,3 % del azar en MIRAR (p 0,006) a 38,4 % contra 38,9 % en PRUEBA.
   - Elige los mismos strikes que DOMS_NDX_vol en el 93,9 % de los minutos. Lo único que cambia es la base: mediana +0,73 pts, p10 −4,3, p90 +7,3.
   - Juzgada con la base C7 da 40,1 %, lo mismo que DOMS.
   - Corriéndola 3 a 9 pts saca 43-45 %: el lugar exacto de la raya no importa.
   - El p 0,006 fue una semilla con suerte: con 2000 juegos da 0,018, y agrupado por noche, 0,038. Holm del campeonato: 0,62.
2. **MUROS_NQ_vol, MAJORS_NQ_vol y MUROS_NQ_vol.P de noche.** [nq][esc1][esc2]
   - En MIRAR dieron 47,7, 45,7 y 48,7 % (p 0,020, 0,042 y 0,044).
   - En PRUEBA cayeron a 33,3, 34,4 y 28,6 %, las tres **debajo** del azar: percentiles 11, 23 y 5. Con 5000 juegos, p 0,90, 0,77 y 0,95.
   - Dos rayas sin gamma, en los múltiplos de 100 que encierran el precio, sacan 45,6 % en MIRAR y 30,2 % en PRUEBA: van igual que MUROS. El 58 % de sus llegadas fueron a múltiplos de 100. Si el placebo se redondea a strikes de 50, el p sube a 0,11-0,24.
   - **El exceso era el número redondo, no el gamma.** MUROS_NQ_vol era la serie que la 4.1 recomendaba de noche.
3. **CONF_vol y CONF_NDX_NQ_vol de rueda.** [combos][esc1][esc2]
   - En MIRAR dieron 52,0 y 53,4 % (p 0,004). En PRUEBA, 42,1 y 41,7 %, debajo del azar.
   - Con un minuto de desfase de más o de menos dan 41-44 %. Solo 43 de las 175 llegadas coinciden entre desfase 0 y +1: las rayas viven 8-10 min de mediana, y 97 de 544 viven un solo minuto.
   - 13,6 de los 16,1 aciertos de más salen de la semana del roll (14-09 a 18-09).
   - Holm del campeonato: 0,42.
4. **ZTP_NQ_vol de noche en PRUEBA** (42,9 %, p 0,040). No era candidata: en MIRAR había dado 36,0 %, percentil 47. Juntando las dos mitades da 38,3 %. Es 1 p ≤ 0,05 en 105 pruebas, cuando el azar solo regala unas 5. [esc1][esc2]
5. **MUROS_NQ_vol.P de rueda en PRUEBA** (53,7 %, p 0,052). La grilla de 100 sin gamma saca 52,0 % en las mismas ruedas, y en MIRAR la serie había dado 38,5 %. [esc1][esc2]
6. **ZEST_NQ_oi de noche en PRUEBA** (64,3 %, p 0,058). Son 14 llegadas en 4 noches, y con +1 min baja a 46,2 %. Es una anécdota. [esc1][esc2]

### 3.3 Ranking descriptivo por % ACIERTO20 — PRUEBA de noche

Va ordenado por la diferencia contra el azar de cada serie, porque cada una tiene su propia tasa base. **Nada de esto es significativo:** con 50 a 100 llegadas, el azar solo ya mueve el porcentaje unos ±8 puntos (entre p5 y p95).

Cada renglón trae el % real contra el azar, la diferencia, las llegadas, los aciertos y falsas por noche, las rayas por hora y cómo le fue en MIRAR. Fuentes: [ndx][nq][qqq][combos].

**Arriba**
1. MUROS_NDX_oi:C_techos: 43,0 contra 37,3 (+5,7). 79 llegadas, 3,1 aciertos y 4,1 falsas por noche, 1,7 rayas/h. En MIRAR dio 35,4 (percentil 49): no se repitió. p 0,166.
2. FAM_MUROS_oi_TOP: 48,1 contra 42,6 (+5,5). 52 llegadas, 4,2 y 4,5 por noche, 1,0 rayas/h. En MIRAR, 31,6 (p 0,84): se dio vuelta. p 0,256.
3. ZTP_NQ_vol: 42,9 contra 37,4 (+5,5). 266 llegadas, 14,2 y 19,0 por noche, 4,3 rayas/h. Percentil 47 en MIRAR. Cae (ver 3.2).
4. MUROS_NDX_vol:P_pisos: 43,9 contra 38,5 (+5,4). 107 llegadas, 4,3 y 5,5 por noche, 1,8 rayas/h. Percentil 67 en MIRAR. p 0,170.
5. MAJORS_QQQ_vol: 41,2 contra 37,9 (+3,3). 102 llegadas, 6,0 y 8,6 por noche, 2,8 rayas/h. Percentil 72 en MIRAR. p 0,26.
6. MUROS_NQ_oi: 43,3 contra 40,1 (+3,2). 60 llegadas, 3,2 y 4,2 por noche (6,8 y 8,9 por noche completa), 2,5 rayas/h. En MIRAR, 43,1 (percentil 83,6). p 0,335. **Es la más pareja entre las dos mitades.**
7. MUROS_NDX_vol: 41,3 contra 38,6 (+2,7). 264 llegadas, 9,9 y 14,1 por noche, 3,1 rayas/h. Percentil 66 en MIRAR. p 0,209.
8. DOMS_NDX_vol: 40,4 contra 38,1 (+2,3). 312 llegadas, 11,5 y 16,9 por noche, 3,1 rayas/h. Percentil 88 en MIRAR. p 0,223.
9. ZTP_NDX_vol: 40,6 contra 38,3 (+2,3). 504 llegadas, 18,5 y 27,2 por noche, 2,9 rayas/h. Percentil 56 en MIRAR. p 0,202.
10. MAJORS_NDX_vol: 40,9 contra 38,8 (+2,1). 259 llegadas, 9,6 y 13,9 por noche, 3,1 rayas/h. Percentil 68 en MIRAR. p 0,26.

**El control sin gamma.** GRILLA_QQQ (los 2 strikes de QQQ más cercanos al precio) da 42,1 contra 38,9 (+3,2), percentil 87, p 0,13. **Le gana a todas las series de QQQ con gamma** en esa ventana. [qqq]

**Abajo**
- FAM_MUROS_vol: 30,9 contra 41,5 (−10,6).
- MUROS_NQ_vol.P: 28,6 contra 39,0 (−10,4).
- FAM_MUROS_vol_TOP: 30,2 contra 40,5 (−10,3).
- ZEST_QQQ_vol: 31,7 contra 39,0 (−7,3). Queda debajo del azar en las 4 celdas.
- MUROS_NQ_vol: 33,3 contra 40,4 (−7,1).
- CONF_NDX_NQ_vol: 31,5 contra 37,0 (−5,5).
- MAJORS_NQ_vol: 34,4 contra 38,6 (−4,2).
- R20_QQQ (la 2.0 QQQ): 36,2 contra 38,7 (−2,5).
- Las tres candidatas de NQ que había dado MIRAR terminaron en este grupo.

### 3.4 Ranking por ruido: falsas por noche y rayas por hora (PRUEBA de noche)

Menos ruido no quiere decir más acierto. El porcentaje de aciertos queda en el del azar; lo único que cambia es cuántas llegadas hay.

**Las que más entradas falsas meten por noche**
- CONF_todo: 30,3.
- ZTP_NDX_vol: 27,2.
- MUROS_oi_DOI: 23,0.
- R20_QQQ (2.0 QQQ): 21,4.
- ZTP_NQ_vol: 19,0 (31,8 por noche completa).
- DOMS_QQQ: 18,3 a 18,6.
- R20_NDX (2.0 NDX): 17,4.
- DOMS_NDX: 16,9.
- MUROS_NDX_vol: 14,1.
- CONF_vol: 14,0.
- MAJORS_NDX_vol: 13,9.
- MUROS_NDX_oi: 13,7.

**Las que menos meten**
- ZEST (zero estándar): 0,6 a 4,0.
- Un solo lado de un muro: 2,2 a 9,2.
- FAM_MUROS_oi_TOP: 4,5.
- MUROS_NQ_oi: 4,2 (8,9 por noche completa).

**Rayas distintas por hora**
- De noche:
  - ~1: ZEST_NDX, ZEST_QQQ, FAM_MUROS_oi_TOP y CONF_NDX_NQ.
  - 1,5 a 1,9: un solo lado de un muro.
  - 2 a 4: muros, majors y DOMS completos.
  - 4,1 a 4,3: DOMS_QQQ_esc, ZTP_NQ y CONF_todo.
  - 5,9: MUROS_oi_DOI.
  - 8,1: la 2.0 QQQ.
  - 8,9: ZEST_NQ_vol, porque salta.
- De día:
  - T_DOMS_raz: ~50.
  - La 2.0 QQQ: 14 a 20.
  - La 2.0 NDX: 14,1.
  - CONF_todo: 9,7.
  - MUROS_oi_DOI: 8,9.
  - ZEST_NQ: 8 a 11.

**Falsas contra aciertos.** En casi todas las series de noche hay más falsas que aciertos. La única excepción es ZEST_NQ_oi, con 14 y 31 llegadas. Con una tasa base de ~38 %, eso es lo esperable para cualquier raya.

### 3.5 Rueda (13:30Z a 20:00Z)

- La tasa base es de 41-47 %, y nada pasa Holm.
- **Lo mejor de PRUEBA:**
  - MUROS_NQ_vol.P: 53,7 %. La grilla de 100 sin gamma da casi lo mismo: 52,0.
  - MUROS_NQ_vol: 49,6 contra 45,5.
  - MAJORS_QQQ_vol: 49,5 contra 45,6.
  - FAM_MUROS_oi_TOP: 49,3 contra 43,7.
  - DOMS_QQQ_vol: 49,2 contra 45,1.
  - MAJORS_NDX_oi: 48,5 contra 44,7.
  - Todas tienen p ≥ 0,12, y casi todas habían quedado del otro lado en MIRAR.
- **De día las series NDX completas quedan debajo de la mediana del azar en las dos mitades.** Por ejemplo, MUROS_NDX_vol cae en los percentiles 20 y 33.

---

## 4. Qué instrumento rinde mejor (punto 2)

**Ninguno se despega del azar.** Las diferencias entre instrumentos, de 1 a 2 puntos, son más chicas que el ruido.

**Todas las series de cada instrumento juntas, PRUEBA de noche** [acá, desde las tablas de las familias]. No son independientes, porque comparten strikes: sirven solo para orientar.
- NDX: 39,9 % contra 38,6 % del azar. 5 de 8 series quedan arriba de su azar.
- NQ: 39,9 % contra 38,8 %. 4 de 8.
- QQQ: 37,6 % contra 38,6 %. 2 de 8.
- Combinaciones: 37,1 % contra 38,0 %. 5 de 9.

**Instrumento por instrumento**
- **NDX es "la menos mala", por un pelo.** [ndx][esc1]
  - De noche, los muros, majors, DOMS y ZTP completos quedaron arriba de la mediana del azar en las dos mitades: percentiles 56 a 88 en MIRAR y 74 a 80 en PRUEBA. Pero todos con p ≥ 0,2.
  - Con solo las 12 noches CBOE de MIRAR quedaron debajo: percentiles 28 a 46.
  - Una grilla NDX en múltiplos de 50 o de 100 da 39,6 y 36,9 % en PRUEBA: algo parecido.
  - De día, NDX queda debajo del azar.
- **NQ tuvo el mejor MIRAR y la peor caída.** [nq][esc1][esc2]
  - Lo que tenía era el número redondo.
  - Lo único parejo fueron los muros por OI de noche: 43,1 y 43,3 %, sin significancia.
  - Hay un problema práctico: solo 5 de 23 noches tienen el libro de NQ completo. Además, las series por OI no existen hasta el salto de OI (~01:48Z, 22:48 ART) o hasta las 03:30Z.
- **QQQ queda a la par o debajo del azar.** La grilla sin gamma le ganó a todas sus series con gamma en PRUEBA de noche. [qqq]
- **La 2.0** (R20_NDX y R20_QQQ). [ndx][qqq][juez20]
  - Fuera de muestra no le gana al azar en ninguna mitad.
  - La demo del juez con las dominantes de la 2.0 da 36,4 % contra 37,8 % del azar en 13 noches.
  - Es la que más dibuja. La 2.0 QQQ hace 6 a 8 rayas/h de noche, 14 a 20 de día y 17 a 21 falsas por noche.
  - La 2.0 QQQ, además, queda corrida unos 20,7 pts de mediana contra DOMS, por su conversión.
- **TQQQ** solo se juzgó en la rueda, con muestras chicas: 21 a 85 llegadas en MIRAR y 0 a 38 en PRUEBA. Queda neutral. T_DOMS_raz es ruido puro: unas 50 rayas por hora. [qqq]
- **Combinaciones** (familia sumada, confluencias y filtros TOP/dOI). [combos]
  - No mejoran el porcentaje de forma consistente; los filtros solo dibujan menos.
  - Lo que la 4.1 dibujó de verdad esta noche (n = 1, solo descriptivo): FAM_MUROS_vol 1 acierto en 9 llegadas, FAM_MUROS_oi 1 en 9 y CONF_vol 2 en 14.

---

## 5. Los ejemplos que marcó el operador (punto 3)

Los juzgó juez20 contra las series reales que dibujó la 4.1 esta noche, con desfase 0. Con desfase 1 da lo mismo.

- **E1 — NDX 30.800 = 31.040,74, piso.** Es la "raya doble o triple": NDX muro V/OI más NDX major V. **ACIERTO.**
  - Llegó a las 03:11Z (00:11 ART), con la mecha 0,74 por debajo de la raya.
  - Hizo los 20 pts a las 03:18Z, en 7 min.
  - Recorrió 127 pts hasta 31.167,75 (05:49Z) sin romperse, y sigue abierta.
  - Los ~93 que vio el operador coinciden con el máximo de las 05:04Z: 31.137,25, o sea +96,5.
- **E2 — NQ muro P OI 31.100** (MUROS_NQ_oi). **ACIERTO**, pero el toque no fue cuando él lo marcó.
  - Fue a las 04:50-04:51Z (01:50-01:51 ART), no a las 05:0x-05:1xZ: a las 05:05Z el mínimo estaba 20 pts arriba de la raya.
  - Tuvo un falso rompimiento a 31.097,5 (2,5 de mecha). Hizo los 20 pts a las 04:55Z y ya lleva 67,75 de recorrido.
- **E3 — NDX cruce del zero (ZTP_NDX_vol) 31.063,8-31.064,0.** Es la "línea roja" de la que el operador dijo que "se comportó como lo que buscamos". De los cinco pisos que marcó, 3 cuentan como acierto y 2 como falsa:
  - 02:18Z y 02:26Z (23:18 y 23:26 ART) son UNA misma llegada. Acierto a las 02:28Z, con recorrido de 25,3 y un falso rompimiento de 4,5.
  - 02:40Z (23:40 ART): acierto por **0,06 pts**. El máximo de las 03:01Z, 31.084,00, pasó los 20 por casi nada.
  - 03:04Z y 03:07Z (00:04 y 00:07 ART): **FALSA**.
    - Hubo tres cierres seguidos debajo (03:06, 03:07 y 03:08Z) con solo 7,6 a favor.
    - La serie fue corriendo la raya hacia abajo detrás del precio.
    - El precio bajó hasta 31.040 y ahí lo frenó la raya del E1.
  - 03:27Z (00:27 ART), la misma raya como piso: acierto, con recorrido de 103,9. Sigue abierta.
- **E4 — ZTP_NDX_vol D2 31.112,6: NO CUENTA.**
  - No hubo llegada en 05:0xZ: lo más cerca que estuvo el precio fue 5,18 pts arriba (05:13Z), y la banda es de ±2.
  - Si se contara, fuera del pre-registro, sería un acierto de 55.
  - Con esa misma raya hubo dos llegadas reales, y **las dos fueron falsas**: 04:47Z como piso y 04:53Z como techo.
- **E5 — NQ M− OI 31.050** (MAJORS_NQ_oi). **ACIERTO en las dos llegadas.**
  - 01:53Z (22:53 ART): acierto a las 02:00Z, recorrido 39,25.
  - 02:15Z (23:15 ART): acierto a las 02:18Z, con 4 de falso rompimiento.
  - Lo de 01:4xZ no se puede juzgar: la raya nació a las 01:49Z, cuando cargó el OI de NQ.
  - Después se rompió, entre las 03:09 y las 03:11Z.

**La cuenta.** De los pisos que marcó, 6 acertaron, 2 fueron falsos (por la regla de los 3 cierres) y 1 no tuvo llegada. Uno de los aciertos fue por 0,06.

**La lección, para explicarle.** Lo que el operador marcó es real, y el juez lo reconoce. Pero el juez cuenta TODAS las llegadas, no solo las que uno recuerda. Esas mismas rayas tuvieron llegadas falsas esta misma noche (E3 a las 03:04, E4 a las 04:47 y 04:53). Y en 11 a 23 noches, esas series quedan a la par de una raya cualquiera: unas 4 de cada 10. [familias]

---

## 6. Recomendación para la 4.1 (punto 4)

**Prender: nada.** Ninguna serie tiene un acierto respaldado por la medición. Las que estaban apagadas tampoco se ganaron el lugar.

**Lo que tiene prendido hoy** [acá]. Sale de "MNQ liviano.ws", guardado el 09-10 a las 03:04 ART (06:04Z); lo que haya cambiado después no está acá.
- Prendidas: MUROS_NQ_oi, MAJORS_NQ_oi, MUROS_NDX_vol, MUROS_NDX_oi, MAJORS_NDX_vol, ZTP_NDX_vol, R20_NDX_vol (2.0 NDX), R20_QQQ_vol (2.0 QQQ) y T_MUROS_oi (TQQQ, solo de día).
- Apagado todo lo demás, incluidas la 3.0 NDX (S_TRES_NDX) y el zero estándar de QQQ (ZEST_QQQ_vol).

**Criterio.** La vara solo distingue dos cosas: **cuánto ensucia** cada serie y **si quedó del mismo lado del azar en las dos mitades**.
- Dejar como mapa: percentil ≥ 65 contra el azar en las dos mitades de noche, con densidad baja.
- Proponer apagar: no cumple eso y además mete muchas falsas o queda debajo del azar.

Abajo, serie por serie, de noche, en MIRAR → PRUEBA:

**Proponer apagar por ruido** (no tienen ventaja y meten muchas falsas)
- **2.0 QQQ (R20_QQQ).**
  - Dio 36,2 → 36,2 % contra 37,5 → 38,7 del azar. Percentiles 33 → 25: debajo en las dos mitades.
  - Mete **16,8 → 21,4 falsas por noche**, con 5,9 → 8,1 rayas/h de noche y 14 a 20 de día. Es la que más ensucia de todo lo que tiene prendido.
  - De día queda arriba del azar (percentiles 78 → 72), pero con 14 a 20 rayas por hora.
  - Ojo: el operador la quiere porque le acertó el 08-10 [memoria]. Una noche buena no pesa más que 19 noches medidas, pero la decisión es suya.
- **NDX cruce (ZTP_NDX_vol).**
  - Dio 37,1 → 40,6 % (percentiles 56 → 80), sin significancia.
  - Mete **25,9 → 27,2 falsas por noche**: es la de NDX que más entradas falsas genera, porque la raya va detrás del precio.
  - Es la "línea roja" que le gustó esta noche (en el E3: 3 aciertos y 2 falsas).
- **NDX muro OI (MUROS_NDX_oi).**
  - Dio 35,7 → 37,1 % contra 37,4 → 39,0 del azar: percentil 29 en las dos mitades, o sea debajo.
  - Mete 15,4 → 13,7 falsas por noche.
  - Muchas veces cae en el mismo strike que el NDX muro V. Si se apaga, la "doble raya" del E1 queda simple, pero la raya sigue.
- **2.0 NDX (R20_NDX_vol).**
  - Dio 42,3 → 38,4 % (percentiles 99,6 → 43,6). Lo de MIRAR fue ruido de la conversión.
  - Mete 14,1 → 17,4 falsas por noche.
  - De día dibuja 14 rayas/h y queda debajo del azar (percentiles 39 → 26).
  - Elige los mismos strikes que NDX dom en el 94 % de los minutos.
- **NQ M± OI (MAJORS_NQ_oi).**
  - Dio 40,9 → 39,0 % (percentiles 67 → 49).
  - De día queda debajo del azar en las dos mitades (percentiles 16 → 24).
  - Mete unas 13 a 14 falsas por noche completa; por noche cruda, unas 6.

**Dejar como mapa, no como señal.** Ninguna le gana al azar. Son las que quedaron arriba de la mediana del azar en las dos mitades de noche y con densidad baja.
- **NQ muro OI (MUROS_NQ_oi).**
  - Dio 43,1 → 43,3 % contra 38,2 → 40,1 del azar (percentiles 84 → 67), con 2,5 a 2,9 rayas/h. Es la más pareja de todo el campeonato.
  - De día queda a la par del azar.
  - Ojo: no se contrastó contra la grilla de múltiplos de 100, que en NQ empata a las series por volumen.
- **NDX muro V (MUROS_NDX_vol).**
  - Dio 38,1 → 41,3 % (percentiles 66 → 79), con 2,8 a 3,1 rayas/h.
  - Mete 14 a 15 falsas por noche, por las dos caras del muro.
  - De día queda debajo del azar (percentiles 20 → 33).
  - Para menos ruido con lo mismo: solo el muro P da 1,6 a 1,8 rayas/h y 8,5 a 9,2 falsas por noche, con el mismo porcentaje (MUROS_NDX_vol:P, 38,8 → 41,3).
- **NDX major V (MAJORS_NDX_vol).** Dio 38,3 → 40,9 % (percentiles 68 → 74), con 3,0 a 3,1 rayas/h y 11 a 14 falsas por noche.

**Neutral**
- **TQQQ muro OI (T_MUROS_oi).** Es solo de día: 47,4 → 45,5 % (percentiles 55 → 74), con 76 y 33 llegadas y 2,0 a 2,5 rayas/h. Ensucia poco. No hay base ni para apagarla ni para destacarla.

**Lo que no se midió en este campeonato** (no opinar sobre esto)
- La capa 3.0 NDX (TRES_*). Figura entre los defaults de la 4.1.4, que no está instalada.

**Un default de la 4.1.4 que conviene revisar.** El zero estándar de QQQ (ZEST_QQQ_vol) también es default de la 4.1.4 sin instalar.
- Quedó debajo de la mediana del azar en las 4 celdas: percentiles 18, 20, 26 y 24.
- De día salta: 5,8 a 7,1 rayas por hora.
- Con lo medido no tiene respaldo para ser default. En su .ws ya está apagado.

**Cómo hacerlo** (son reglas suyas, no se negocian)
- **Es una propuesta.** Apagar una serie cambia lo que se dibuja: hay que avisarle en el mismo mensaje, con **captura antes y después**, y esperar su OK. [CLAUDE.md][memoria: dominantes-de-noche-por-volumen]
- **De a una casilla por vez**, y nada pesado con el mercado abierto. [memoria: cambios-atas-de-a-uno]
- **Son casillas de SU workspace.** ATAS las guarda por nombre en el .ws, así que cambiar un default en el código no las toca. [CLAUDE.md]
- **Apagar no hace ganar nada.** Solo saca entradas falsas de la vista; las que quedan siguen a la par del azar.

---

## 7. Cuántas noches más hacen falta y qué dejar grabando

### Cuántas

La cuenta de potencia la hice [acá] con estos supuestos:
- tasa base de 38,5 %, potencia del 80 % y una cola;
- un factor de dispersión entre noches de 1,0 a 1,4 (medido por esc2).

**Llegadas necesarias**
- Para ver una ventaja de **+5 puntos** (43,5 % contra 38,5 %) hacen falta unas 590 llegadas con una sola serie pre-registrada. Con tres series (Holm), unas 840.
- Con la dispersión entre noches: hasta 830 y 1.180.

**Noches nuevas.** Una serie completa (las dos rayas, muro C + P) junta unas 22 a 25 llegadas por noche **completa**.
- Con una candidata: **24 a 38 noches nuevas**.
- Con tres: **34 a 54 noches nuevas**.
- Son entre 5 y 11 semanas de 5 noches.
- Para ver +4 puntos: 37 a 59 noches con una, 53 a 84 con tres.
- Una serie de un solo lado (~8 llegadas por noche) necesita 75 a 148 noches para +5 puntos: no es práctico.

**Lo que ya hay no alcanza.** Con 7 a 12 noches de PRUEBA se ven ventajas de 8 a 10 puntos, no de 4 o 5. Las 15 sesiones de bitácora acordadas tampoco alcanzan para esta pregunta.

**Las noches tienen que ser NUEVAS:** desde la sesión del 10-10, que arranca hoy 09-10 a las 22:00Z (19:00 ART). Las candidatas se eligen después de haber visto las dos mitades, así que ninguna noche ya vista puede contar como prueba.

### Pre-registro propuesto

Hay que decidirlo con el operador ANTES de mirar noches nuevas, y hashearlo.
- **Regla de candidatas:** las series que hoy tiene prendidas y quedaron en percentil ≥ 65 contra el azar en las dos mitades de noche. Son tres: MUROS_NQ_oi, MUROS_NDX_vol y MAJORS_NDX_vol. Tres, para que Holm no se coma la potencia.
- **Controles obligatorios** [esc1][esc2]:
  - la grilla sin gamma: múltiplos de 100 en NQ, de 50 y de 100 en NDX, y GRILLA_QQQ;
  - el placebo redondeado a strikes;
  - una serie de NQ que no le gane a la grilla no cuenta.
- **Desfase:** juzgar con desfase 0 y con +1 min, y exigir que aguante en los dos. Es la lección de CONF. [esc1]
- **Un solo corte de calendario** para todas las series.
- **Placebo:** n_azar ≥ 2000, con piso de p en 0,0005, y Holm sobre las tres.
- **No cambiar la cuenta de las candidatas en la 4.1** mientras dure la muestra. Si cambia, anotar la versión y cortar la muestra ahí.

### Qué dejar grabando

- **ATAS con la 4.1 abierta toda la noche**, de 22:00Z a 13:30Z (19:00 a 10:30 ART). El libro de NQ solo existe con ATAS abierto: hasta hoy, solo 5 de 23 noches tienen el libro completo. [nq]
- **La 4.1 ya graba por minuto las 25 series, prendidas o no**, en %APPDATA%\ATAS\PythiaGex4\familia\niv-AAAA-MM-DD-MNQZ6.jsonl.
  - [acá] El de hoy tiene 554 líneas, la última de las 07:13Z, con las 25 series.
  - Hay un solo archivo, porque la 4.1 empezó el 08-10 a las 22:04Z.
  - **No graba TQQQ (T_*) ni la 3.0 (TRES_*).** Para juzgarlas hay que agregarlas al registro, que es un cambio de código y sigue el protocolo de la 4.1, o reconstruirlas.
- **Respaldar todos los días** %APPDATA%\ATAS\PythiaGex4\{cboe, viva, familia, cinta} fuera de esa carpeta.
  - Las cadenas de cboe\ se guardan 14 días [README de la 4.1]. Hoy hay 9 días por ticker, desde el 01-10 [acá].
  - Sin copia, las primeras se empiezan a perder alrededor del 15-10.
  - [supuesto] No verifiqué si el archivo de cadenas del laboratorio ya copia la carpeta PythiaGex4.
- **Velas M1 de MNQZ6** (velas_hasta_ahora.py). Falta la de las 05:35Z de hoy.

---

## 8. Cómo contárselo (una idea por vez, sin tablas)

Analogías sugeridas, en este orden:

1. **La tasa base.** "Es como el pronosticador que todos los días dice 'mañana no llueve' en Buenos Aires: acierta casi siempre sin saber nada." Una raya cualquiera, puesta a la misma distancia del precio, aguanta 20 puntos 4 de cada 10 veces de noche. La vara a superar no es 0: es 4 de 10.
2. **MIRAR y PRUEBA.** "Es estudiar con el examen del año pasado y después rendir uno nuevo." Varias rayas se sacaron un 9 en el examen viejo y un 4 en el nuevo.
3. **Muchas pruebas.** "Si tirás cien monedas, alguna sale seis caras seguidas. No es una moneda mágica: es que tiraste cien." Se probaron más de 100 combinaciones, y aparecieron las 5 o 6 "ganadoras" que el azar regala solo.
4. **Los números redondos.** "Son como las esquinas: la gente se para ahí aunque no haya semáforo." Lo que hacían bien las rayas de NQ en la primera mitad, lo hacía igual una raya en el 31.100 redondo, sin gamma.
5. **Sus ejemplos.** "Es acordarse de los goles y olvidarse de los penales errados." El juez le reconoce los 6 aciertos, pero también cuenta las 2 falsas y las llegadas que él no marcó.
6. **Por qué en el papel todas "ganan plata".** "La cancha está inclinada por cómo se cuenta: 20 a favor contra 5 a 10 en contra." Hasta una raya al azar da positivo en el papel. Y no tiene costos: no promete nada.
7. **Qué hacer.** "Sacar del gráfico lo que más ruido mete no te hace ganar: te saca entradas falsas de la vista." Para saber si alguna raya sirve de verdad, hacen falta entre 5 y 11 semanas más de noches completas grabadas.

Nada de esto anticipa dirección ni promete rentabilidad.

---

## 9. Dudas y límites

- **Particiones distintas por familia.** La PRUEBA de NDX arranca el 25-09, la de NQ el 30-09, la de QQQ el 01-10, y las combinaciones cortan serie por serie. Lo que es PRUEBA para una familia es MIRAR para otra, así que no se elige nada comparando entre familias. [esc2]
- **La noche de hoy entra en PRUEBA cortada.** NDX llega hasta las 06:01Z, NQ hasta las 06:10Z y QQQ hasta las 06:06Z; las combinaciones tienen niveles solo hasta las 03:53Z. Los promedios por noche de PRUEBA quedan un poco bajos.
- **Las noches del 20-08 al 04-09 de NDX** usan como cadena de noche la última foto de la rueda de Databento. Sin esas noches, R20 baja del percentil 99,6 al 93, y el resto de NDX queda debajo de la mediana del azar.
- **Casi todo es reconstrucción, no registro.** La 4.1 no existía antes del 08-10: lo anterior es lo que la 4.1 habría dibujado, no lo que dibujó en vivo.
  - La paridad con lo grabado se midió solo esta noche: NDX 480 de 480 minutos idénticos y NQ 315 de 315.
  - DOMS y R20 solo se pudieron comparar en 26 minutos.
- **El libro de NQ cambió de ancho según la época:** de 160 a 310 filas. Del 02-10 al 06-10 estuvo ahogado por el cupo de 200 de ATAS 8.0.15. Esto afecta sobre todo al zero estándar.
- **Las series que siguen al precio** (ZTP y ZEST) juntan muchas llegadas por construcción.
- **El escalón −0,0077 de DOMS_QQQ_esc** se estimó con noches que incluyen la PRUEBA. Igual no ganó.
- **Mi cuenta de potencia** trata las llegadas de una noche como independientes, corregidas por un factor de 1,0 a 1,4. Si las llegadas de una misma noche están más pegadas entre sí, hacen falta más noches.
- **Los totales por instrumento de la sección 4** juntan series que comparten strikes: son solo orientativos.
- **No toqué** ATAS, el indicador ni el .ws: solo los leí.

---

## 10. Archivos

Todo está en C:/Users/wmx_7/OneDrive/Escritorio/ATAS nada/PythiaGex/laboratorio/calibracion_1009/campeonato20/

- **El juez:** juez20/juez20.py, test_juez20.py y ejemplos_operador_0910.py. Las salidas están en juez20/datos/ (test_juez20_salida.txt, ejemplos_operador_0910.txt y .json, demo_api_20.json, velas_m1_juez20.csv).
- **NDX:** ndx/datos/resumen_ndx.txt y .json, tabla_compacta.txt, paridad_0910.txt y control_filtro.txt. Los scripts son ndx/ndx_00 a ndx_04.
- **NQ:** familia_nq/datos/juez_nq.txt, .json y juez_nq_tabla.csv, particion.json y paridad_41_0910.txt. Los scripts son familia_nq/nq01 a nq03.
- **QQQ y la 2.0:** qqq20/datos/resumen_qqq20.txt, .csv y .json, y q05_descriptivo.txt. Los scripts son qqq20/q01 a q05.
- **Combinaciones:** familia_conf/PREREGISTRO_familia.md y su .sha256, resultados/tabla.json, tabla.txt, comparar.json y hoy_41_real.json, y datos/paridad_*.json.
- **Escéptico 1:** esceptico1/datos/, con reproduccion, causalidad, desfase, desfase_placebo, r20doms, grilla, placebo_propio, holm y paridad_propia (.txt y .json).
- **Escéptico 2:** esceptico2/datos/, con e01_reproduccion, e02_resumen, e02_por_noche, e03_holm, e04_* y e05_grilla_nq.
- **Leídos [acá]:** %APPDATA%\ATAS\PythiaGex4\familia\niv-2026-10-09-MNQZ6.jsonl, %APPDATA%\ATAS\Workspaces_v3\MNQ liviano.ws (las casillas S_*), PythiaGex/atas/PythiaGexCuatro/README.md y CHANGELOG.md.
