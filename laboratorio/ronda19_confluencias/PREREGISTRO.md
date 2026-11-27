# Ronda 19 — Entrada de calidad por confluencias (ola + marea + niveles + order flow) — PRE-REGISTRO

Escrito el **27-11-2026 a las 03:00 AR (UTC−3)**, ANTES de simular una sola operación. Lo único que se corrió antes es el
inventario de cintas (`r19_00_inventario.py`): conteos, horarios, huecos y la base contra el SPX para ubicar el roll. Nada de
resultados de entradas ni de salidas.

Pedido del operador (textual, resumido): cruzar VWAP, imbalance, absorción, big trades, icebergs y niveles automáticos
(día anterior, Asia, Londres, balance inicial, VWAP semanal y mensual, área de valor de la semana anterior) para subir el
porcentaje de aciertos con **pocas operaciones de calidad por rueda que se desplacen bastante**. Reglas de método: teoría dura y
confluencia (contexto → lugar → momento), de menor a mayor, explicar el movimiento (prohibido cerrar con "nada le gana al
azar"), baúl de pistas, pre-registro con muestra nueva y prueba en vivo. Nada de esto promete rentabilidad.

## 0. Datos (medidos en el inventario, 27-11 02:50 AR)
- Cintas orden por orden de `%APPDATA%\ATAS\PythiaGex\flujo`: **ES 49 ruedas (08-09 a 13-11, sin días faltantes)**, MES 08-09 a
  17-09 (7, sin el 10-09) y **MES 16 a 25-11 (8)**. Cada una va de las 18:00 NY de la víspera a las 17:00 NY. Huecos de la rueda:
  ninguno mayor a 60 s salvo ES 24-09 12:17 NY (143 s). Duplicados exactos: 0-251 por jornada (se sacan).
- **El roll está medido:** la base futuro − SPX (con el SPX de `datos/historico`, 902 s de atraso descontados) da 3,9-6,4 pts en
  las cintas de ES y MES del 08 al 11-09 (contrato **U6**) y 66-71 pts desde el 14-09 (contrato **Z6**), bajando hasta ~21 a mediados
  de noviembre. El salto entre jornadas del 11-09 al 14-09 (+19,25) es el spread menos la caída del fin de semana.
  **Spread usado para ajustar niveles de varios días: +67,00 pts** (entre +66,63 medido en vivo el 15-09 21:40 y +67,43 de la base
  11-09 → 14-09). Se suma a todo precio U6 que entre en un nivel usado en una rueda Z6 (día anterior del 14-09, semana del 08-11-09,
  VWAP mensual de septiembre).
- Historia previa al 08-09 (solo para X, VWAP mensual de septiembre y área de valor de la semana 31-08 a 04-09): velas de 5 min de
  ES continuo (U6) de la caché de ATAS decodificada (`laboratorio/dom/ronda3/velas/velas-ES-m5.csv`). Ahí el VWAP usa el precio
  típico de la vela y el perfil reparte el volumen de cada vela parejo en su rango (aproximación declarada).

## 1. Muestras (por tiempo, fijas)
- **Diseño:** ES 08-09 a 15-10. Las dos primeras (08 y 09-09) son calentamiento de los umbrales del order flow (percentiles de
  ruedas previas): **se opera del 10-09 al 15-10 = 26 ruedas**.
- **Confirmación:** ES 16-10 a 13-11 = 21 ruedas (OJO: 02 a 13-11 ya las miraron las rondas 17 y 18, con otras reglas).
- **Guardada:** MES 16 a 25-11 = 8 ruedas. No se arma nada de ella hasta el final (ni rasgos). OJO: fue el diseño de la ronda 18 y
  la tanda 0 de la 17 (23-25-11), con otras reglas; para esta ronda es intocada.
- **Tanda 0 (de menor a mayor), elegida por regla con la eficiencia de la rueda e = |cierre − apertura| / rango (RTH), entre las 26
  de diseño:** las 2 de mayor e (tendencia) **21-09 (0,82) y 05-10 (0,76)**; las 2 de menor e (rango) **10-09 (0,01) y 18-09
  (0,05)**; la más cercana a la mediana (0,383; empate, la anterior) **14-09 (0,33, mixta; primera rueda Z6 después del roll)**.
  Se miran operación por operación; después la etapa 1 congelada corre sobre las 21 restantes del diseño.

## 2. Máquinas, escala y niveles (todo con lo anterior a cada instante)
- **X por rueda = 0,08 × mediana del rango RTH (9:30-16:00) de las 10 ruedas previas**, redondeado al tick. Ola = zigzag de X, marea =
  4X, micro = X/2, orden por orden desde las 18:00 NY, con la clase `Maquina` de `ronda18_senal_ola/r18_01_senal.py` (idéntica).
  **Marea con agresores** = el delta acumulado desde el último pivote de la marea tiene el signo de la marea (igual que la R18).
- **VWAP de la jornada** (desde las 18:00) y **VWAP RTH** (desde las 9:30), con bandas de ±1 y ±2 desvíos (desvío ponderado por
  volumen del precio contra el VWAP, acumulado). **VWAP semanal** (desde la primera jornada de la semana) y **mensual** (desde la
  primera jornada del mes).
- **Día anterior** (RTH): máximo, mínimo, cierre (último antes de las 16:00) y área de valor 70 % (POC, VAH, VAL; regla de ATAS de
  `r17_comun.area_valor`, perfil por precio con los barridos repartidos parejo).
- **Asia** (18:00-03:00 NY) y **Londres** (03:00-09:30 NY): máximo y mínimo.
- **Balance inicial** (9:30-10:30): máximo y mínimo, y extensiones a ±0,5 y ±1 vez su rango; existen desde las 10:30.
- **Semana anterior:** área de valor 70 % (POC, VAH, VAL) de todas las jornadas de la semana calendario anterior.
- **Redondos de 50.**
- **Tipo de día (registrado, no filtra en la etapa 1):** extensión del balance inicial (arriba, abajo, las dos, ninguna), apertura y
  precio dentro o fuera del área de valor de ayer, y "fuera del ruido" (|precio − apertura| contra el promedio de ese mismo minuto en
  las 10 ruedas previas, a la Zarattini-Aziz-Barbon 2024). **Hora** desde las 9:30.

## 3. Order flow (definiciones de práctica, causales; umbrales = percentiles por franja de 30 min de la rueda en las hasta 10 ruedas previas del mismo instrumento, mínimo 2)
Para un LARGO (el corto es el espejo), sobre la ventana de los 60 s que terminan en el segundo de decisión t:
- **F1 absorción en la punta:** volumen de ventas agresivas que NO movió el bid (bid después ≥ bid antes) ≥ p90 de esa suma de 60 s.
- **F2 iceberg:** al menos una venta que se comió MÁS de lo que mostraba el bid (vol > tamaño visible) y el bid quedó en el mismo precio
  con tamaño ≥ 1 (se recargó), a ≤ X/2 del extremo del retroceso.
- **F3 divergencia de delta:** el mínimo del retroceso está por debajo del mínimo micro (X/2) anterior del mismo retroceso, pero el delta
  acumulado en el mínimo nuevo es MAYOR que en el anterior.
- **F4 agotamiento:** el volumen por tick del último tramo de X/4 de la bajada hasta el mínimo es ≤ la mitad del volumen por tick de
  todo el retroceso.
- **F5 imbalance apilado a favor:** en una vela de 1 min ya cerrada dentro de la ventana, 3+ precios seguidos con desequilibrio
  diagonal de compra ≥ 300 % (ask en p ≥ 3 × bid en p − 1 tick, con ask ≥ 30 contratos en ES / 18 en MES, el umbral de la R17), a ≤ X/2
  del mínimo.
- **F6 big trades netos a favor:** volumen neto de órdenes ≥ p99 de tamaño (franja, ruedas previas) en la ventana ≥ p80 de ese neto de 60 s.
- **Veto C (caudal anómalo, baúl #13):** volumen vendedor de los 30 s anteriores al mínimo ≥ p99,5 de esa suma de 30 s → no se entra.
- **Registrados para la anatomía (no puntúan en la etapa 1):** retroceso con o sin agresores (signo del delta del retroceso), delta
  del retroceso, profundidad del retroceso en X, velocidad de llegada, cantidad de niveles en la zona, hora, tipo de día.

## 4. ETAPA 1 — el modelo de entrada (CONTEXTO → LUGAR → MOMENTO; ESPERAR por defecto)
Se evalúa al cierre de cada segundo t entre **9:45:00 y 15:29:59 NY**, sin posición abierta, con menos de 5 entradas en la rueda.
- **CONTEXTO (todas):** C1 marea a favor CON agresores; C2 precio del lado correcto del VWAP RTH (largo arriba, corto abajo).
- **Retroceso en curso:** la ola está en contra de la marea; su extremo corriente es el "extremo del retroceso" (ext).
- **LUGAR:** ext está en la zona de al menos un nivel de la lista (sección 2, más VWAP RTH ±1/±2 desvíos, VWAP de la jornada,
  semanal y mensual): para un largo, L − X/4 ≤ ext ≤ L + T, con T = X/8 redondeado al tick (mínimo 2 ticks). Puede perforar hasta X/4.
- **MOMENTO:** el mínimo es fresco (t − hora del mínimo ≤ 60 s), el precio ya rebotó al menos 2 ticks y está a ≤ X/2 del mínimo, y el
  **puntaje de confluencia F1+…+F6 ≥ 2** (pesos iguales, Timmermann 2006), sin el veto C.
- **Entrada:** al último precio del segundo t. Una por retroceso (pierna de la ola); una posición por vez; máximo 5 por rueda.
- **Stop:** detrás del extremo: min(ext − 2 ticks, entrada − X/2) para el largo (R mínimo X/2 para que el ruido y el costo no se
  coman el riesgo: costo ≤ 20 % de R). R = entrada − stop.
- **Salidas (familia chica, todas se reportan):** S1 objetivo 2R; S2 objetivo 3R; S3 objetivo en el nivel siguiente a favor entre 1R y
  6R (si no hay, 3R); S4 seguimiento con la raya de la MAREA (sale cuando la marea se da vuelta en contra, en su raya; el stop inicial
  vale hasta entonces). Los objetivos exigen que el precio los pase por 1 tick (orden limitada). Toda posición se cierra a las 15:59:59.
- **Costo:** 0,50 pts de ES por operación completa (comisión + deslizamiento) y 1,00 como prueba de esfuerzo. Entradas y salidas al
  precio negociado (el costo cubre el spread: no se cuenta dos veces).

## 5. Cómo se juzga (igual para cada etapa)
- Por regla × salida: operaciones por rueda, % de acierto (operaciones con resultado bruto > 0), ganancia media y pérdida media,
  esperanza neta por operación con costo 0,50 y 1,00, **t por RUEDA** (resultado neto diario, las ruedas sin operaciones cuentan 0),
  ruedas positivas, máxima racha perdedora, máxima caída (pts netos).
- **Placebo A (dirección al azar):** mismas entradas (hora y precio) con lado al azar, mismo R, mismos objetivos en R (S3: nivel
  siguiente en el lado sorteado; S4 se compara con su versión simétrica S4b: stop que persigue a 4X del mejor precio); 200 sorteos.
- **Placebo B (niveles al azar):** todos los niveles del día corridos un δ ~ U(−2X, +2X) con |δ| ≥ X/2; el resto de la regla igual;
  40 sorteos.
- **Búsqueda múltiple:** se cuenta TODA combinación regla × salida corrida en diseño (incluidas las ablaciones). Se reporta el t de
  Bonferroni (una cola, 5 % / N, gl = ruedas − 1) y el Deflated Sharpe (Bailey y López de Prado 2014) de cada candidata.
- **Finalistas:** como mucho 2, las de mayor t por rueda en diseño (costo 0,50) entre las que operan 1 a 5 veces por rueda con
  n ≥ 30. Se juzgan UNA vez en confirmación y UNA vez en la guardada, sin tocar nada.
- **PASA** solo si: neto > 0 (costo 0,50) en diseño, en confirmación y en la guardada, cada una por separado; **y** el t por rueda de
  confirmación + guardada (29 ruedas) ≥ el t de Bonferroni para 2 finalistas (una cola, 2,5 %); **y** le gana al placebo A en esas 29
  ruedas (p < 0,05). Se informa además el t contra la corrección por TODAS las variantes probadas. Aun si pasa, es "en prueba" hasta
  la prueba en vivo (bitácora).
- **Evoluciones:** hasta 4 (etapa 1 + 3 más), cada una pre-registrada acá abajo ANTES de correrla, diciendo qué cambia y por qué.
- **Anatomía:** para la finalista, qué tenían las ganadoras que no tenían las perdedoras (cada factor, cociente con IC, por tipo de día,
  hora y nivel), con operaciones reales (fecha, hora, precio). Lo que sobreviva en parte va al baúl. Prohibido cerrar con "nada le
  gana al azar": se explica la mecánica con números.

---
## Resultado de la ETAPA 1 (para entender por qué cambia la 2) — 27-11 03:18 AR
- Tanda 0 (5 ruedas, `narraciones/E1-tanda0.md`): 22 operaciones, 22,7 % de acierto con 2R. Diseño completo (26 ruedas): 126
  operaciones (4,85 por rueda: el tope de 5 se llena casi siempre ANTES de las 10:30), acierto 25,4 % con 2R (un paseo al azar
  da 33 %), bruto −0,46 pts (−0,24 R) por operación, neto −0,96 con costo 0,50, t por rueda −4,6. Las 4 salidas pierden.
  Placebo A (mismas entradas, lado al azar): −0,42 → la dirección no suma; placebo B (niveles corridos): −0,37 → los niveles no
  suman. Ablaciones: sin lugar −0,29, sin momento −0,51 (tampoco cambian). Control de simetría del motor (entradas al azar):
  +0,03 R ± 0,04 → el motor no tiene sesgo; el −0,24 R es de los MOMENTOS que elige la etapa 1.
- Mecánica (anatomía, `salidas/anatomia-E1-diseno-S1.txt`): entra a 2 ticks de un extremo FRESCO con el stop a X/2: el stop queda
  adentro del ruido del extremo y lo tocan los dos lados (por eso también pierde el lado al azar). El puntaje no filtra: el iceberg
  a ≤ X/2 aparece en 120 de 126 y el agotamiento en 108 de 126 (cerca de cualquier mínimo fresco hay recargas y el último tramo
  siempre tiene menos volumen por tick que el empujón inicial). Pistas sin significancia: 2+ niveles en la zona 36 % vs 23 %
  (cociente 1,58, IC 0,84-2,98); absorción 32 vs 24 %; puntaje 3-4 29-40 % vs 23 %.

## ETAPA 2 — pre-registrada el 27-11 a las 03:18 AR, ANTES de correrla
Qué cambia y por qué (teoría + lo medido en la etapa 1):
1. **Régimen direccional (CONTEXTO C3, nuevo):** solo se opera a favor si el precio está FUERA del área de ruido de la apertura en
   ese lado: (precio − apertura RTH) × lado > σ del minuto, con σ = promedio de |precio − apertura| en ese mismo minuto de las 10
   ruedas previas (Zarattini, Aziz y Barbon 2024, "Beat the market", momentum intradía del SPY; Gao, Han, Li y Zhou 2018; Baltussen
   et al. 2021). En la etapa 1 el tope se llenaba en cualquier tipo de día; la tendencia intradía que documenta la literatura vive
   fuera del ruido.
2. **Entrada CONFIRMADA por la ola en vez de a 2 ticks:** el setup de la etapa 1 (C1 + C2 + lugar + puntaje ≥ 2 sin veto, con el
   mínimo fresco y el rebote de 2 ticks a X/2) solo ARMA el retroceso; se entra cuando la ola se da vuelta a favor de la marea (el
   precio ya volvió X desde el extremo), si el extremo final de ese retroceso es el armado (a ≤ 2 ticks). Geometría (R17: la chance
   de giro crece con lo que ya volvió) y ruido (stop afuera del ruido del extremo).
3. **Stop detrás del extremo final** (extremo − 2 ticks): R ≈ X + 2 ticks (el costo de 0,50 pasa a ser ~10 % de R).
4. **Horario de entradas 10:00-15:30** (antes de las 10:00 las bandas del VWAP RTH todavía son angostas y el ruido no está definido).
- En la entrada (el segundo en que la ola se da vuelta) se exigen otra vez C1, C2 y C3. Se mantienen: una por retroceso, una
  posición por vez, máximo 5 por rueda, salidas S1-S4, costos, placebos y jueces de la sección 5.

---
## Resultado de la ETAPA 2 y lo que mostró el contexto — 27-11 03:23 AR
- E2 en diseño: 33 operaciones (1,27 por rueda), acierto 21 % con 2R (R ≈ 4,3 pts), bruto −1,60 pts (−0,34 R); S4 (marea) −0,02 R
  con 18 % de acierto y ganadoras de +17,5. Placebo A (lado al azar) −0,48 y placebo B (niveles corridos) −0,16: la dirección
  "a favor de la ruptura" fue PEOR que el azar.
- Medición exploratoria del contexto en diseño (`r19_06_contexto_deriva.py`, `salidas/contexto_deriva_diseno.txt`, chequeos cada
  30 min de 10:00 a 15:30): con el precio fuera del área de ruido, el lado de la ruptura dio **−3,12 pts al cierre (−0,58 X), 40 %
  a favor, t por rueda −2,45**; si además la marea ya se había dado vuelta hacia el valor, **−6,71 pts (−1,49 X), t −2,54**; a las
  15:30 el lado de la ruptura perdió en 10 de 10. Por la regla del baúl (#19, eficiencia mediana de las 10 ruedas previas < 0,465),
  21 de 26 ruedas de diseño y 21 de 21 de confirmación son "va y vuelve": el período entero es de reversión, no de tendencia.
  Mecánica: en este régimen la extensión fuera del ruido la arma la agresión de un lado y la deshace el libro (responsiva, Dalton):
  el precio vuelve al valor.

## Enmienda por pedido del operador (vía coordinador, recibida 27-11 ~03:21 AR) — antes de elegir finalistas
- Prioridad: **la fórmula final más chica** (no todas las variables) con el **acierto más alto posible**, pocas operaciones por rueda
  y **esperanza neta > 0**. Se informan acierto y esperanza neta lado a lado en diseño y confirmación.
- **Criterio de finalistas (reemplaza al de la sección 5):** entre las variantes con esperanza neta > 0 (costo 0,50) en diseño,
  1 a 5 operaciones por rueda y n ≥ 30, la de MAYOR ACIERTO; la segunda, la siguiente en acierto (otra regla o salida). Si hay menos
  de 2 con esperanza > 0, se completan con la de mayor t por rueda, que igual se juzga afuera pero no puede "pasar". La vara de
  PASA no cambia.
- Entregable extra: `scratchpad\r19\eventos_todos.parquet` con TODAS las vueltas de la ola en RTH a favor de la marea y las que
  vuelven en un nivel aunque la marea no acompañe, con hora UTC al ms, precio, rasgos de ese instante y el resultado de cada salida
  (se arma al final, con la columna de muestra; la guardada se rotula).
- Si al final existe `scratchpad\r20\gex_minuto.parquet`, se puede cruzar la finalista con el régimen de gamma como UNA evolución
  más, pre-registrada.

## ETAPA 3 — "vuelta al valor" — pre-registrada el 27-11 a las 03:23 AR, ANTES de correrla
Fórmula chica, desde la teoría (reversión al valor en régimen de balance: Dalton, actividad responsiva; Grossman-Miller 1988 y
Campbell-Grossman-Wang 1993: la presión de liquidez se revierte; el VWAP como precio justo del día) y lo medido en E2 y en el contexto:
1. **Extensión:** el pivote que inició la pierna actual de la MAREA (su extremo) quedó FUERA del área de ruido de la apertura, del
   otro lado: para comprar, ese mínimo < apertura RTH − σ del minuto en que se hizo (con σ de las 10 ruedas previas, como en E2);
   espejo para vender. Solo cuentan extremos de la rueda (desde las 9:30).
2. **La marea vuelve hacia el valor con agresores:** marea a favor de la operación y delta de su pierna con ese signo (igual que C1).
3. **Entrada:** cuando la OLA se da vuelta a favor de la marea (precio X desde el extremo del retroceso), con el precio todavía del
   lado extendido del VWAP RTH y a ≥ 1R de él (que el objetivo pague al menos 1 a 1). 10:00-15:30, una por retroceso, una posición
   por vez, máximo 5 por rueda.
4. **Stop:** extremo del retroceso ∓ 2 ticks (R ≈ X + 2 ticks).
5. **Salidas:** S1-S4 de siempre y **S5 = el VWAP RTH** (sale cuando el precio lo pasa por 1 tick, al valor del VWAP de ese segundo;
   si stop y VWAP caen en el mismo segundo, cuenta el stop). Para el placebo A, S5 se compara con su versión simétrica S5b (objetivo
   fijo a la distancia del VWAP en la entrada).
- Sin nivel ni puntaje de order flow (fórmula chica): niveles y flujo se miden en la anatomía y, si suman acierto sin matar la
  esperanza, entran en la etapa 4.

## Resultado de la ETAPA 3 — 27-11 03:26 AR
- Diseño (26 ruedas): 49-59 operaciones (1,9-2,3 por rueda). S1 2R: acierto 35,1 %, bruto +0,15 pts (+0,05 R), neto −0,36;
  **S2 3R: acierto 29,6 %, bruto +0,58 (+0,15 R), neto +0,08 (t +0,09)**; S3 −0,52; S4 marea +0,13; S5 VWAP: acierto 24 % (el VWAP
  quedaba lejos, ~3 R), bruto −0,11. Placebo A (lado al azar): la dirección real le gana al azar en todas las salidas (S4b +0,12 vs
  −1,63, p 0,000; S2 +0,58 vs −0,57, p 0,065; S5b p 0,095): **la dirección "hacia el valor" tiene información**, pero la esperanza
  queda cerca de 0 antes de costos y el acierto es bajo. (Placebo B no aplica: la etapa 3 no usa niveles para entrar.)
- Anatomía (`salidas/anatomia-E3-diseno-S1.txt`, 57 operaciones, sin corrección por comparaciones múltiples): absorción en la punta
  en el último minuto del rebote 66,7 % vs 29,2 % (n 9, cociente 2,29, IC 1,21-4,33); big trades netos a favor 56,2 % vs 26,8 % (n 16,
  2,10, IC 1,08-4,08); en la etapa 1 los dos tenían el mismo signo, más débil (32 vs 24 %, 30 vs 24 %). Otras pistas: día con el
  balance inicial roto de los dos lados 61 vs 23 %; 13:30-15:30 56 vs 27 %; retroceso profundo (≈ 3-4 X) 58 vs 24 %.

## ETAPA 4 — "vuelta al valor con flujo responsivo" — pre-registrada el 27-11 a las 03:26 AR, ANTES de correrla
- **E4 = E3 + una sola capa de order flow:** se entra solo si en los 60 s previos a la vuelta de la ola hay ABSORCIÓN en la punta
  del lado de la operación (F1) **o** BIG TRADES netos a favor (F6) — puntaje F1 + F6 ≥ 1, pesos iguales. Teoría: en un mercado en
  balance el que deshace la extensión es el participante RESPONSIVO (Dalton): el libro pasivo que absorbe la agresión contraria y las
  órdenes grandes que entran a favor; es lo que pidió medir el operador (absorción, big trades). Elegida mirando la anatomía de la
  etapa 3 en diseño (sesgo de selección declarado): por eso decide la confirmación.
- **Salidas:** S1-S5 de la etapa 3 **más S6 = objetivo 1R** (la salida de acierto alto que pide el operador; en un paseo al azar da
  50 % de acierto y esperanza −costo).
- Todo lo demás igual a la etapa 3. Variantes probadas en diseño hasta acá: E1 (4), E1 sin lugar (4), E1 sin momento (4), E2 (4),
  E3 (5) = 21; con E4 (6) = 27.

---
## PRE-REGISTRO GENERAL DE LA BÚSQUEDA POR FAMILIAS — 27-11 03:32 AR (pedido del operador vía coordinador: "darle con todo")
- **Base común:** `scratchpad\r19\eventos_todos.parquet` (diccionario en `DICCIONARIO_EVENTOS.md`): las 4.822 vueltas de la ola de
  la rueda en las 47 ruedas de ES con rasgos causales y el resultado de las salidas pre-registradas S1, S2, S3, S4, S4b, S5, S6.
  Explorar SOLO con `muestra == "diseno"` (26 ruedas). Ninguna salida nueva sin pre-registrarla y calcularla desde la cinta.
- **Familias:** (a) rechazo en una zona (el retroceso llega a un nivel y aparece absorción o iceberg del lado pasivo); (b) ruptura con
  iniciativa (atraviesa un nivel con apilados y grandes a favor; necesita eventos de cruce, se agregan aparte si se pide);
  (c) retroceso al VWAP o su banda a favor de la marea con el delta dándose vuelta; (d) subasta fallida / barrida de un nivel que
  vuelve adentro con absorción; (e) divergencia de delta en un nivel; (f) vuelta al valor (etapas 3-4 de este agente).
- **Cada familia se pre-registra ACÁ, con hora, antes de mirar sus resultados**, y CADA variante probada (regla × salida × muestra) se
  anota en `registro_variantes.csv` (fecha y hora, agente, familia, parámetros, resultado en diseño), con `r19_09_registro.anotar()`.
- **Vara doble:** acierto Y esperanza neta con costo 0,50 y 1,00, lado a lado, con t POR RUEDA, operaciones por rueda (1 a 5), racha y
  caída. **Corrección por búsqueda múltiple sobre el TOTAL de variantes de diseño del registro** (Bonferroni sobre t y Deflated Sharpe
  con la varianza de los Sharpe de todas: `python -I r19_09_registro.py resumen`).
- **Finalistas:** se eligen al FINAL de todas las familias (queda en suspenso la elección que surgía de las etapas 1-4: E4-S6 y
  E4-S1), con el criterio de la enmienda (mayor acierto con esperanza > 0, n ≥ 30, 1 a 5 por rueda), como mucho 2. Van UNA vez a
  confirmación y después a la guardada. PASA igual que antes (neto > 0 en las tres muestras, t OOS ≥ Bonferroni para las
  finalistas, mejor que el placebo A). Prohibido cerrar con "nada le gana al azar": se explica qué separa a las ganadoras.

## Base de CRUCES para la familia (b) — definida el 27-11 a las 03:34 AR, antes de calcularla
- Evento = el precio CRUZA un nivel de la lista (los 31 de la matriz + cada redondo de 50) con histéresis: hay cruce hacia arriba en el
  segundo t si el último precio queda ≥ L + T habiendo estado ≤ L − T desde el cruce anterior de ese nivel (espejo hacia abajo);
  T = X/8 al tick (mínimo 2 ticks). Solo en la rueda (9:30-16:00); `lado` = dirección del cruce.
- Entrada = último precio del segundo del cruce; **stop = L ∓ X/2** (el nivel falló: el precio volvió X/2 del otro lado); R = |entrada − stop|.
- Salidas pre-registradas: S1 2R, S2 3R, S3 nivel siguiente 1R-6R, S4 raya de la marea (solo a favor de la marea), S4b persigue 4X,
  S5 VWAP RTH (solo si queda del lado de la ganancia), S6 1R; mfe_R y mae_R. Rasgos de la ruptura: apilados e imbalances a favor y en
  contra (1 y 3 velas), big trades netos 60 s, delta 60/300 s, absorción del lado que resiste, icebergs, marea y ola, VWAP, régimen.

## FAMILIA (b) "ruptura con iniciativa" — pre-registrada el 27-11 a las 03:38 AR, ANTES de mirar (base `eventos_cruces.parquet`, diseño)
Teoría: actividad de INICIATIVA (Dalton): el que rompe un nivel compromete agresión (imbalances diagonales apilados) y tamaño (big
trades netos a favor); si la ruptura es genuina, el precio no vuelve al nivel. Simulación con `r19_11_evaluar_base.py` (una posición
por vez, máx. 5 por rueda, entradas 9:45-15:30), salidas S1 2R, S3 nivel siguiente, S4 marea, S6 1R; placebo C (eventos al azar de
la misma rueda); todo al registro.
- **B0** (línea de base): todos los cruces.  **B1:** cruce con `of_apilados_favor_3v ≥ 1` Y `of_big_favor_fuerte == 1`.
- **B2:** B1 + a favor de la marea con agresores.  **B3:** B1 solo en niveles estructurales (sin las bandas ±1/±2 de los VWAP).
- 4 reglas × 4 salidas = 16 variantes. Advertencia escrita antes de mirar: en este período "va y vuelve" lo esperable es que las
  rupturas fallen (contexto medido: el lado de la ruptura fuera del ruido perdió al cierre).

## FAMILIA (f) — vecindario y ablaciones de E4 — pre-registrado el 27-11 a las 03:38 AR, ANTES de mirar (base `eventos_todos`, diseño)
Para saber si E4 es un filo de cuchillo (elegido mirando la anatomía de E3) o una zona estable, y cuál es la fórmula MÁS CHICA:
- Control (no cuenta): E4 reproducido sobre la base (tiene que dar lo mismo que el motor: S1 30 ops, 50 %, +1,61 neto).
- **Vecindario:** flujo ∈ {solo F1, solo F6, F1 o F6} × distancia mínima al VWAP ∈ {0,5R, 1R, 1,5R} (sin repetir E4) × salidas {S1, S6}.
- **Ablaciones:** E4 sin la extensión fuera del ruido; E4 sin "marea con agresores"; E4 sin flujo (= E3); × {S1, S6}.
- 8 × 2 + 3 × 2 = 22 variantes.

## Resultado de (b) y del vecindario de E4 — 27-11 03:41 AR (diseño; detalle en `salidas/familia_b_diseno.txt` y `salidas/familia_f_vecindario_diseno.txt`)
- (b) ruptura con iniciativa: con objetivos fijos pierde o empata (B1 S1 −0,89 neto, S6 −0,49); con la raya de la marea gana poco y
  acierta poco (B2 S4: 44 ops, 20,5 % de acierto, +1,94 neto, t 0,95, placebo C p 0,005). En "va y vuelve" la ruptura casi siempre
  vuelve; cuando no vuelve, corre mucho (ganadoras de +23).
- (f) vecindario de E4: TODAS las variantes con flujo dan positivo con S1 (+0,38 a +2,54 neto); sin flujo (= E3) da −0,38. Sacar la
  extensión fuera del ruido (+1,16) o "marea con agresores" (+1,40) casi no cambia: la pieza que separa es el FLUJO (absorción o big
  trades del lado que defiende) con el VWAP lejos del lado de la ganancia. Absorción sola: 13-18 ops, 53-67 % de acierto.

## ETAPA 5 "fórmula mínima" (familia f) — pre-registrada el 27-11 a las 03:41 AR, ANTES de mirar
Pedido del operador: la fórmula más chica. Sobre la base de vueltas, diseño, salidas {S1, S6}:
- **E5a:** vuelta de la ola con el VWAP RTH a ≥ 1R del lado de la ganancia + (F1 absorción o F6 big trades) — sin marea ni extensión.
- **E5b:** E5a + a favor de la marea (sin extensión ni agresores).
- 2 reglas × 2 salidas = 4 variantes (al registro).

## Resultado de E5 — 27-11 03:42 AR
- E5a (sin marea): S1 −0,19 bruto, 31,6 % → sin la marea hacia el valor NO hay nada. E5b (con marea, sin extensión ni agresores):
  S1 36 ops, 47,2 %, +1,19 neto (t 1,09, placebo C p 0,015); S6 54,8 %, −0,20 neto. **La fórmula mínima que conserva lo de E4 tiene
  tres condiciones: la ola vuelve a favor de la marea + el VWAP RTH queda a ≥ 1R del lado de la ganancia + absorción o big trades
  del lado que defiende en el último minuto.**

## REGLA DEL ARBITRAJE DE FINALISTAS — escrita el 27-11 a las 03:42 AR, ANTES de recibir las finalistas de los exploradores
- Pozo = las finalistas que proponga cada explorador + las de la familia (f). Elegibles: neto > 0 con costo 0,50 en diseño, n ≥ 30,
  1 a 5 operaciones por rueda, sin usar la confirmación.
- Orden: por ACIERTO en diseño (pedido del operador). Diferencias menores a una operación se desempatan por la variante
  pre-registrada antes (menos búsqueda) y después por esperanza neta. Máximo 2 por familia y nada de casi duplicados (misma
  entrada y misma salida con un parámetro movido: queda la mejor).
- Máximo 4 finalistas. Se abre la confirmación UNA vez para todas juntas (ES 16-10 a 13-11) y después la guardada (MES 16-25-11)
  con el mismo código, sin tocar nada. PASA como en la sección 5 y la enmienda: neto > 0 en las tres muestras, t por rueda de
  confirmación + guardada ≥ Bonferroni para las finalistas juzgadas, y mejor que el placebo de la misma rueda.
- Candidatas de la familia (f) para el pozo: **E4-S6** (58,3 %, +0,15, n 36; su vecina d1.5 da 58,8 % con 2 operaciones menos: queda la
  pre-registrada) y **E4-S1** (50,0 %, +1,61, n 30). Alternativa más chica si hace falta un lugar: E5b-S1 (47,2 %, +1,19, n 36).

## Enmienda: la GUARDADA pasa a ser ES 16-25-11 (pedido del operador vía coordinador) — 27-11 04:04 AR, sin haber abierto nada
- Motivo (medido por el coordinador en 3 ruedas de septiembre con ES y MES, RTH): el precio es el mismo (igual en 60-62 % de los
  segundos, a ≤ 1 tick en 99,7 %), pero el ES mueve 12-15 veces más dinero, el p99 de tamaño equivale a 18-19 M USD contra 1,5-1,8 en
  MES y el delta por minuto de MES coincide en signo con el de ES solo 72-77 % de los minutos (correlación 0,69-0,78): el order flow se
  lee del ES.
- **Juicio final = ES 16, 17, 18, 19, 20, 23, 24 y 25-11** (8 ruedas), cuando el coordinador avise que las cintas están bajadas (se
  bajan con la sonda desde un gráfico de ES). Mismo código, sin tocar nada; el X, los niveles y los VWAP de noviembre quedan todos en ES
  (no hace falta escalar volumen). **MES 16-25-11 queda como chequeo secundario** (no decide). Ninguna de las dos se abre hasta el aviso.
- La confirmación (ES 16-10 a 13-11) no cambia: se abre UNA vez para las finalistas cuando lleguen.

## Nota de transparencia sobre las horas (agregada, sin reescribir nada) — 27-11 ~03:57 AR
- La enmienda de la guardada dice "04:04 AR" pero el reloj del sistema marcaba **03:55** cuando se escribió (escribí una hora de más).
- Antes de esta nota corregí en el mismo archivo, reemplazándolas, varias horas de encabezados que había escrito a ojo (E1 resultado,
  E2, E2 resultado, enmienda del operador, E3, E3 resultado, E4, base de cruces, familia b/f, E5 y arbitraje) para que coincidan con
  el reloj del sistema (`Get-Date`), siempre ANTES de correr lo que pre-registraban. De acá en adelante toda corrección se agrega como
  nota nueva, sin tocar lo ya escrito.

---
## ENMIENDA — CANDIDATAS DE ZONAS Y DELTA (definiciones exactas) y aplicación preliminar del arbitraje — 27-11 04:04 AR
Escrita ANTES de abrir la confirmación (sigue cerrada: falta MODELO). Código ejecutable: `finalistas.py` (copiado de
`explorador_zonas/z09_candidatas.py` + `zonas_comun.py` y de `explorador_delta/d11_juicio.py` + `d_lib.py`). Común a todas: evento =
vuelta de la ola de la base; entradas 10:00-15:30 NY (`seg_rueda` 1800-21599); una posición por vez por rueda; máximo 5 por rueda;
entrada = último precio del segundo de la vuelta.
- **ZONAS (familia a, reversión en la banda de 2 desvíos):** banda = VWAP RTH ±2σ o VWAP de jornada ±2σ del lado OPUESTO al
  retroceso (compra: vwr−2 / vwj−2; venta: vwr+2 / vwj+2); pen = (banda − extremo) × lado.
  - **C1** = banda respetada (pen en (−X/2, 0]) + F2 (iceberg del lado pasivo a ≤ X/2 del extremo en 60 s). Salida S6 (1R) o S1 (2R).
  - **C2** = toque de la banda (|pen| ≤ T, T = `zona_T`), sin flujo. S6.
  - **C3** = C1 + VWAP RTH a ≥ 1,5R del lado de la ganancia. S6.
  - **C4** = C1 con entradas desde las 10:30 (corte POST HOC). S6 o S1.
- **DELTA (familia e, FADE de la vuelta falsa):** se opera AL REVÉS de la vuelta, a la misma entrada; salida **S6mc** calculada DESDE
  LA CINTA (`r19_16_fade.py`): objetivo = extremo del retroceso ∓ 1 tick (se llena al pasar 1 tick más), stop = entrada ± (R + 1 tick).
  "Rápida" = la vuelta llegó ≤ 107 s después del segundo del extremo.
  - **w6** = rápida Y (`of_apilados_favor_3v` ≥ 1 O (F3 Y `n_niveles_en_zona` ≥ 1)).  **w1** = rápida Y (apilados ≥ 1 O
    `ctx_ret_con_agresores` = 0).  **a2** = rápida Y retroceso sin agresores.  **a3** = rápida Y F3 en nivel.
- **Familia f (este agente):** E4 (S6 / S1) y E5b (S1), definiciones en `finalistas.py`.

**Verificación con MI evaluador (`r19_17_verificar_candidatas.py`, `salidas/verificacion_candidatas_diseno.txt`):** reproduce EXACTO n,
acierto, neto y t de las 13 candidatas en diseño (p. ej. C1-S6 70 ops 70,0 % +1,364 t 2,34; C4-S6 44 / 75,0 % / +1,847 / 2,74;
w6 78 / 70,5 % / +1,215 / 2,27). La operación contraria recalculada desde la cinta coincide con la derivada de S6 en 220 de 220 eventos
(puntos y segundo de salida). **Solapamiento de eventos:** entre familias casi nada (f contra ZONAS 3 %, f contra DELTA 6-10 %, ZONAS
contra DELTA 2-9 %): son tres confirmaciones distintas. Dentro de cada familia: C1/C3/C4 89-100 %, C2 contra C1 47 %, w6 contra w1 58 %,
a2 ⊂ w1 (85 %), a3 ⊂ w6 (84 %).

**Aplicación preliminar de la regla del arbitraje (a completar con MODELO):**
- Pedido del coordinador para cuidar el sobreajuste, incorporado como criterio (más conservador que "la de mayor acierto"): **entre casi
  duplicados queda la de MENOS ajustes elegidos mirando resultados**; los subconjuntos ajustados (C3, C4, a2, a3) se leen solo como
  descripción dentro de las operaciones fuera de muestra de su madre, no como pruebas aparte. Al menos una finalista por familia.
- ZONAS → **C1-S6** (contiene a C3 y C4; 70 ops, 70,0 %, +1,36). DELTA → **w6-S6m** (empata en acierto con w1 por menos de una
  operación, pre-registradas juntas en D7; desempata la esperanza: +1,22 contra +1,03). Familia f → **E4-S6** (58,3 %, +0,15).
- Cuarto lugar: la finalista de MODELO si es elegible (neto > 0, n ≥ 30, 1-5 por rueda) y no es casi duplicada de las otras tres; si no,
  **C2-S6** (la versión de ZONAS sin flujo y con la zona estándar de la base: control de sobreajuste de la ventana y del iceberg).
- Bonferroni fuera de muestra para k = 4: una cola 1,25 % con 29 ruedas (21 de confirmación + 8 de la guardada ES) → t ≈ 2,4.

---
## ARBITRAJE FINAL — 27-11 04:25 AR, ANTES de abrir la confirmación (con la regla tal como estaba escrita a las 03:42 y 04:04)
Candidata de MODELO (definición exacta en `explorador_modelo/candidatas_modelo.py`, copiada a `finalistas.py` como `m_a`):
**A "vuelta al valor con grandes a favor"** = `vwap_jornada_dist_sd`·lado ≤ −1,076 Y grandes netos de 60 s a favor > 1,339 × la escala
de su franja ((p80 − p20)/2) Y pierna de la marea ≤ 8,75 X; salida S6 (1R); stop extremo ∓ 2 ticks; horario: toda la rueda (como la
definió MODELO). Números honestos de diseño (fuera de pliegue, purgado por rueda con embargo ±1): n 38 (1,46 por rueda), 68,4 %,
+1,16 / +0,66, t 1,87; dentro de muestra mi evaluador da 32 ops y 90,6 % (inflado, no se usa). B no es elegible (n < 30): queda como pista.

Aplicación de la regla escrita:
1. Elegibles (neto > 0 con 0,50 en diseño, n ≥ 30, 1-5 por rueda): las madres de cada familia y A (con sus números fuera de pliegue).
2. Casi duplicados = "misma entrada y misma salida con un parámetro movido": C3 y C4 de C1; w1 de w6 (y a2, a3 subconjuntos). A NO es casi
   duplicada de E4: otras condiciones de entrada (VWAP de jornada en desvíos, umbral de grandes relativo, pierna de marea), comparte 10 de
   33 eventos (23-28 % en mi matriz).
3. Enmienda de las 04:04: ZONAS → C1-S6; DELTA → w6-S6m; familia f → E4-S6; "cuarto lugar: la de MODELO si es elegible y no casi
   duplicada de las otras tres". A cumple → cuarto lugar. Máximo 2 por familia: la familia f queda con E4 y A (2), dentro del tope.
**FINALISTAS (k = 4): Z-C1-S6, D-w6-S6m, E4-S6, M-A-S6.** C2-S6 no entra (era el reemplazo si MODELO no tenía elegible).
Umbral fuera de muestra: Bonferroni una cola 5 %/4 con 29 ruedas (21 de confirmación + 8 de la guardada ES) → t ≥ 2,37 (gl 28).
Se abre la confirmación UNA vez para las cuatro, y después la guardada de ES, con `r19_13_juez.py`, sin tocar nada. Se informan por
separado (n, operaciones por rueda, acierto, neto con 0,50 y 1,00, t por rueda, ruedas positivas, placebo C) y después juntas.

## RESULTADO DEL JUICIO (confirmación y guardada abiertas UNA vez, 27-11 ~04:27-04:30 AR) — detalle en `RESULTADOS.md` y `salidas/juez/`
- **Ninguna de las 4 finalistas pasa.** Fuera de muestra las cuatro caen a ~47-51 % de acierto con objetivo 1R (lo que da una moneda) y
  neto negativo: confirmación C1 −0,75, w6 −0,57, A −0,57, E4 −0,49; guardada ES C1 +0,40 (t 0,26, 3/8 ruedas +), A −0,92, w6 −2,86,
  E4 −4,21; juntas (29 ruedas) t por rueda entre −2,17 y −0,56 contra un umbral de +2,37. Ningún placebo C con p < 0,05 fuera de muestra.
- Mecánica medida: las cuatro eran apuestas a la REVERSIÓN. El diseño fue un período de reversión (la ruptura del ruido volvió −3,1 pts al
  cierre, t −2,45); en la confirmación la reversión se apagó (+0,03 pts, t −1,42) y en la guardada se dio vuelta (+32 pts al cierre a
  favor de la ruptura, 71 %; rango RTH mediano 138 pts contra 57 en diseño). La regla de régimen del baúl (#19) siguió diciendo "va y
  vuelve" en las 8 ruedas de la guardada: llega tarde al cambio.
