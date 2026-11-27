# Ronda 25 — La llave del día de tendencia — PRE-REGISTRO (diseño, partición y vara)

Escrito el **27-11-2026 a las 09:46 AR (UTC−3, reloj del sistema)**, ANTES de calcular cualquier detector o cualquier asociación con un
resultado. Lo único mirado hasta acá: la integridad de los datos (ruedas completas, barras de noche, saltos de contrato) y las fechas
oficiales del calendario (`calendario_oficial.json`). Correcciones y enmiendas: siempre como nota nueva al final, con hora.
Nada de esto promete rentabilidad. **Toda la data de esta ronda ya fue vista por alguna ronda anterior** (R19-R24): lo único limpio es
el vivo desde el 30-11-2026 y, si el operador lo compra, ES de 2023.

## 0. La pregunta
A3 (ronda 23, congelada) desvanece hacia el VWAP de 13:00 a 15:30. Pierde en los días que terminan en tendencia y gana en los de balance
(R23 y R24, post hoc). Se busca un **detector del día de tendencia que use solo lo sabido a las 13:00 NY** (las velas cerradas hasta
las 13:00, la sesión de noche, el día anterior y el calendario), armado desde la teoría, y se mira si como filtro de A3 ("A3F") mejora
fuera de donde se diseñó.

## 1. Datos y partición (fijada ahora)
Todas las cuentas del detector se hacen con **velas de 30 minutos** de la rueda (A = 9:30-10:00 … G = 12:30-13:00; tarde H = 13:00 … M =
15:30-16:00), nativas o armadas desde velas de 5 min o desde la cinta. El VWAP del detector es el de las velas de 30 min ((máx + mín)/2 ×
volumen, acumulado desde las 9:30). X = 0,08 × mediana del rango de la rueda de las 10 ruedas completas previas, al tick (igual que A3).
- **Exploración a mano (regla del operador "de menor a mayor"):** las **15 primeras ruedas de la cinta de ES** (10-09 a 30-09-2026).
  Se miran una por una antes de tocar el diseño. **Quedan fuera** de la validación en la cinta.
- **DISEÑO del detector:** MES 30 min nativas (`velas-MES-m30.csv`), **337 ruedas del 29-04-2024 al 08-09-2025** (las de la R24; antes
  las usó la R22 para el semáforo D1). Ninguna ronda diseñó ahí un detector de tipo de día.
- **VALIDACIÓN 1:** MES 5 min (`velas-MES-m5.csv`) agregadas a 30 min, **242 ruedas del 09-09-2025 al 31-08-2026** (diseño y chequeo de A3).
- **VALIDACIÓN 2:** cinta de ES agregada a 30 min, **las 40 ruedas del 01-10 al 25-11-2026** (las 55 de la R23 menos las 15 de exploración).
- **A3F (descriptivo, porque A3 ya se eligió mirando estas muestras):** diseño de A3 (116 ruedas, MES 5 min), chequeo (126), cinta (55 y
  las 40 de validación), y A3-30 en 2024-25 (R24; ahí el detector está dentro de muestra).
- **Días de cambio de contrato** (lunes de la semana del vencimiento trimestral: 17-06, 16-09, 16-12-2024; 17-03, 16-06, 15-09, 15-12-2025;
  16-03, 15-06, 14-09-2026): los detectores que usan el día anterior quedan **sin dato** (no marcan).

## 2. El objetivo independiente de A3
**Principal — TA, "la tarde se aleja del VWAP":** s13 = lado del cierre de las 13:00 respecto del VWAP de las 13:00; **TA = 1 si
s13 × (cierre de las 16:00 − cierre de las 13:00) > 0** (la tarde siguió alejándose del VWAP), 0 si no. Se informa también la versión en
puntos, **D = s13 × (C16 − C13) / X**. Por qué este y no otro:
1. **No usa nada de A3** (ni sus entradas, ni su stop, ni su objetivo).
2. **No tiene trampa de geometría.** Si el precio de la tarde fuera un paseo al azar (cada vela reflejada al azar, como `r23_sim.py`),
   TA valdría 50 % y D valdría 0 **estuviera donde estuviera el precio a las 13:00**. La R22 se cayó justamente por eso: su objetivo
   dependía de la ubicación. Acá, cualquier detector armado con el pasado tiene AUC 0,5 contra el azar por construcción (se comprueba).
3. **Es el mecanismo por el que pierde A3:** A3 va hacia el VWAP; si la tarde se aleja del VWAP, sus entradas pierden. Se comprueba (sin
   elegir nada con eso) con las operaciones de A3-30 en el diseño.
**Secundarios (se informan, no eligen):** TIPO = día de tendencia ex post (|cierre − apertura| ≥ 0,6 × rango de 9:30-16:00, la definición
de la R23) y NV = la tarde no vuelve al VWAP (ninguna vela de 30 min de 13:00 a 16:00 toca el VWAP de la vela previa). Los dos dependen de
la ubicación a las 13:00, así que se informan con su **AUC del azar** (200 tardes reflejadas por rueda) y lo que importa es la diferencia.
Subconjuntos: todas las ruedas con s13 ≠ 0 (principal) y "tarde con tensión" |C13 − VWAP13| ≥ 1 X (secundario).

## 3. Los seis detectores (teoría; parámetro fijado ahora, grilla solo para robustez)
Dirección d = +1 alcista, −1 bajista; un detector direccional **solo marca si d = s13** (el precio del lado del VWAP que dice el detector).
1. **APE — apertura en un extremo (open-drive / open-test-drive de Dalton).** Mecánica: el que opera a plazo largo entra en la apertura con
   convicción y el precio no vuelve a la apertura: la apertura queda como extremo del día. q = min(O − L13, H13 − O) / (H13 − L13), con
   H13/L13 el rango de 9:30 a 13:00; d = +1 si la apertura está más cerca del mínimo. Marca si **q ≤ 0,15**, d = s13 y el cierre de las
   13:00 está del lado d de la apertura. Grilla 0,10 / 0,15 / 0,20. Puntaje: 0,5 − q en los días coherentes, 0 en el resto.
2. **OTF — una sola dirección en la mañana (one-timeframing).** Mecánica: en un día de tendencia cada media hora subasta más arriba sin
   perforar el mínimo de la media hora anterior: el comprador de plazo largo manda y el que responde no consigue volver. En balance las
   medias horas se pisan. n_otf = cuántas de las 6 comparaciones B-A … G-F cumplen (si s13 = +1) mín_k ≥ mín_(k−1) (si s13 = −1, máx_k ≤
   máx_(k−1)). Marca si **n_otf ≥ 5**. Grilla 4 / 5 / 6. Puntaje: n_otf.
3. **VWA — el VWAP quedó atrás (lado sostenido).** Mecánica: el VWAP es el precio promedio de todos los que operaron hoy; si el precio no
   lo toca en toda la mañana, un lado entero está perdiendo plata y el que desvanece hacia el VWAP queda atropellado; en balance el precio
   gira alrededor del VWAP. n_vwa = cuántas velas de B a G (10:00-13:00) no tocaron el VWAP de la vela previa, del lado s13. Marca si
   **n_vwa ≥ 5**. Grilla 4 / 5 / 6. Puntaje: n_vwa.
4. **ACE — aceptación fuera del rango de ayer.** Mecánica (Dalton): abrir fuera del rango del día anterior es desequilibrio; si en toda la
   mañana el precio no vuelve a entrar, los precios nuevos se están aceptando y el mercado busca valor nuevo en esa dirección; si vuelve,
   es un hueco que se rellena (balance). Marca si la apertura está fuera del rango de 9:30-16:00 de ayer, del lado s13, y de 9:30 a 13:00
   el precio no volvió a entrar más de **ε = 0** (grilla 0 / X/2). Sin dato en los días de cambio de contrato. Puntaje: marca (0/1).
5. **EXT — el rango de la mañana contra lo normal (la llave vieja, como control).** r = (H13 − L13) / mediana de 10. Marca si **r ≥ 1,0**
   (la mañana ya hizo un día normal entero). Grilla 0,8 / 1,0 / 1,2. Puntaje: r. Es el D1 de las rondas 21-22 a las 13:00: se espera que
   anticipe expansión y no continuación (R20/R22); va como vara de comparación.
6. **CAL — calendario: día de la Fed.** Mecánica: el comunicado de las 14:00 y la conferencia de las 14:30 meten información nueva justo en
   la ventana de A3. Marca los días de comunicado del FOMC (federalreserve.gov). Variante: FOMC o dato de las 8:30 (CPI o empleo, bls.gov).
   Puntaje: marca. Ya se sabe (R23, post hoc) que sacar los días de la Fed no mejoró A3: va igual porque es la única noticia programada
   dentro de la tarde.
No entran como detectores (se miran solo en la exploración y como descripción): el inventario de la noche (su mecánica actúa en la
primera hora) y el lugar del cierre de la mañana en su rango (es ubicación; A3 igual exige que el precio ya haya vuelto del extremo).

## 4. Cómo se mide la clasificación
- **AUC** del puntaje contra TA, con **p de una cola por permutación** (2.000 permutaciones de TA entre las ruedas de la muestra) y su
  error de Hanley-McNeil; precisión, recall, base y lift de la marca; D medio con y sin marca (t de Welch por rueda).
- **AUC del azar** (TIPO y NV): el mismo puntaje contra el resultado de 200 tardes reflejadas por rueda; "memoria" = AUC real − AUC del azar.
  Para TA el AUC del azar debe dar ~0,50 (control de la herramienta).
- **Corrección por la búsqueda:** la prueba principal son 6 AUC (un puntaje por detector, TA, todas las ruedas, diseño): **α por detector
  = 0,05 / 6 = 0,0083, de una cola**. Todo lo demás (grillas, subconjuntos, objetivos secundarios, combinaciones) es descriptivo y se anota
  en `registro_variantes.csv` con su número.

## 5. Regla de elección (fijada ahora)
El detector que va a A3F es **el de mayor AUC en el diseño entre los que pasan su α (0,0083)**, con su parámetro de teoría (no se elige el
umbral). Si ninguno pasa, se informan todos como descriptivos, el de mayor AUC se mira igual en validación y en A3F, y **no se congela nada**.
Combinaciones (O / Y de dos detectores): solo descriptivas.

## 6. A3F
- **A3F-día (principal):** A3 tal cual (cand_A3_tarde_giro_amplia.py), pero si a las 13:00 el detector marca, ese día no se opera.
- **A3F-dir (secundaria):** en los días marcados no se toman las entradas que desvanecen contra la dirección del detector (precio del
  lado d del VWAP); las del otro lado sí. Para EXT y CAL (sin dirección) es igual a A3F-día.
- **Medidas por muestra:** operaciones (y por rueda), acierto, acierto del azar con la misma ubicación (los 200 caminos reflejados que ya
  tiene cada operación de A3), neto con costo 1,00 por operación y por rueda, ventaja sobre el azar (pts reales − pts del azar, t con error
  agrupado por rueda), cada mitad de la muestra, y A3 mirando solo los cierres de :00 y :30.
- **Placebo de días salteados:** saltear al azar la misma cantidad de ruedas en cada muestra (2.000 sorteos).

## 7. Cuándo se congela (las cinco; si falla una, no se congela nada)
- **F1:** el detector pasó la regla de elección (sección 5) y en la VALIDACIÓN 1 su AUC contra TA es > 0,5 con p < 0,05 de una cola; en la
  VALIDACIÓN 2, AUC > 0,5.
- **F2 (vara doble del pedido):** A3F-día con acierto ≥ 60 %, neto > 0 con costo 1,00, ≤ 5 operaciones por rueda y ventaja sobre el azar
  > 0 en **cada una** de las tres muestras de A3 (diseño, chequeo, cinta).
- **F3:** A3F mejora el neto por operación de A3 en al menos 2 de las 3 muestras, y en las tres juntas el neto por rueda (las ruedas sin
  operación cuentan 0) de A3F es ≥ el de A3.
- **F4:** la mejora del neto por operación (tres muestras juntas) supera el percentil 90 del placebo de días salteados.
- **F5 (robustez, A3 resultó frágil):** con solo los cierres de :00/:30, A3F deja más neto que A3 en el diseño y en el chequeo; y A3F ≥ A3
  (neto por operación) en al menos 4 de las 6 mitades.
Si pasan las cinco: se congela A3F-día (y A3F-dir si también cumple F2-F5) con SHA-256, su `PREREGISTRO_VIVO.md` (lecturas decisivas a las 60
y 120 jornadas con α = 0,0125 por lectura; futilidad a las 20 y 40; descriptiva a las 15) y su juez `r25_vivo.py`, sin tocar nada de las
rondas 21-24.

## 8. Lo que no se hace
No se toca ATAS ni su ventana, no se mandan teclas, no se compra nada, no se usa la red fuera de las tres páginas oficiales ya bajadas, no
se modifican archivos con huella de las rondas 21-24, no se elige el umbral mirando resultados, no se usa el GEX (solo 57 ruedas de 2026:
queda como hipótesis, R20).

## Enmienda 1 — 27-11-2026, 09:54 AR (después de la exploración a mano, ANTES de calcular cualquier asociación en el diseño o la validación)
Lo que se vio en las 15 ruedas de exploración (`salidas/exploracion_a_mano.txt`):
1. Las 3 ruedas que terminaron en tendencia (15, 21 y 23-09) **ya eran tendencia a las 13:00** (apertura en el extremo, 6 de 6 medias
   horas en una dirección, rango de la mañana 1,0-1,6 veces lo normal). A3 no operó el 21-09 (la llave del IB la frenó), ganó el 15-09
   (+8,5) y perdió poco el 23-09 (−5,5). Dos de esas tres tardes volvieron algo hacia el VWAP.
2. Las tres pérdidas grandes de A3 (18-09 −18,5; 25-09 −17,75; 30-09 −13,5) fueron en ruedas de balance o mixtas: **la tarde arrancó
   un movimiento nuevo** (el 18-09 desde abajo del VWAP, cruzándolo y siguiendo; el 25-09 y el 30-09 con el empujón final de 15:00-16:00).
   Una mañana con tendencia no las anticipa; TA tampoco ve el caso del 18-09 (la tarde cruzó el VWAP: para TA cuenta como "volvió").
3. La apertura en un extremo (APE, q ≤ 0,15) marcó 7 de 15 ruedas: por la ley del arcoseno, el punto de partida de un paseo al azar
   queda seguido cerca de un extremo de su rango. Se deja como está (la prueba principal usa el puntaje, no el umbral).
**Cambio (solo agrega un objetivo secundario; los detectores, el objetivo principal, la regla de elección y la vara NO cambian):**
- **ET — tarde de tendencia, en cualquier dirección:** eficiencia de la tarde |C16 − C13| / (máx − mín de 13:00 a 16:00) ≥ 0,6 **y**
  |C16 − C13| ≥ 2 X. Depende del tamaño de las velas de la tarde, así que se informa con su AUC del azar (las mismas 200 tardes reflejadas)
  y lo que cuenta es la diferencia. Se agrega además un análisis descriptivo de **dónde se hace el movimiento de los días de tendencia**
  (antes o después de las 13:00), porque de eso depende que un detector de la mañana pueda servir.

## Nota de resultado — 27-11-2026, 10:09 AR (no cambia nada de lo escrito arriba)
- Regla de elección (09:56, `elegido_r25.json`): **ningún detector pasó** su α de 0,0083 contra TA en el diseño (AUC 0,459-0,523; el
  mejor, OTF, p 0,22). Según la sección 5 no se congela nada; OTF se miró igual en validación (2025-26: 0,497; oct-nov 2026: 0,783) y
  los seis en A3F, como descriptivos.
- Sección 7: **F1 falla**; F3 y F5 fallan para todos los A3F (ninguno mejora el neto por rueda de las tres muestras juntas sin empeorar
  en otra, ni arregla los cierres :00/:30 en las dos mitades de MES). **No hay candidata, ni PREREGISTRO_VIVO, ni juez r25_vivo.py.**
- El juez de A3 (`ronda23_diseno/r23_vivo.py`) sigue tal cual desde el 30-11. Detalle en `RESULTADOS.md`.
