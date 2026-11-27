# Ronda 21 — El RÉGIMEN del día como llave (detector en tiempo real + una familia por régimen) — PRE-REGISTRO

Escrito el **27-11-2026 a las 04:40 AR (UTC−3)**, ANTES de calcular un solo rasgo de régimen o una sola etiqueta. Lo único hecho
antes: leer la R19 (RESULTADOS, PREREGISTRO, DICCIONARIO, `finalistas.py`, motor y juez), el baúl (#19, #42-46) y listar archivos
(inventario de la base de eventos: 5.895 vueltas, diseño 2.250 / 26 ruedas, confirmación 2.572 / 21, guardada 1.073 / 8; y los rangos
RTH diarios de `hist/resumen.json`, que ya figuraban en la R19). Correcciones: SIEMPRE como nota nueva al final, nunca reemplazando.

Pedido (coordinador, 27-11): un detector de régimen en TIEMPO REAL ("va y vuelve" contra "tendencia", teoría de subasta de Dalton) como
llave para elegir qué operar en ES; meta del operador: acierto 60-70 % con ganancia neta positiva y 1 a 5 operaciones de calidad por
rueda. Prohibido cerrar con "nada le gana al azar"; si nada llega, explicar mecánicamente qué separa ganadoras de perdedoras. Nada de
esto promete rentabilidad.

## 0. Honestidad de muestra (escrito antes de todo)
- **No hay muestra virgen.** Las 55 ruedas de ES (diseño 10-09..15-10 = 26, confirmación 16-10..13-11 = 21, guardada 16..25-11 = 8)
  ya las vio la R19 con otras reglas, y la R19 ya midió que el diseño fue de reversión, la confirmación neutra y la guardada de tendencia
  (rango RTH mediano 57 / 73 / 138). Por eso la validación es por **período dejado afuera** (entrenar en dos, probar en el tercero, las
  3 combinaciones) y por **rueda dejada afuera con embargo** (±2 ruedas), y se dice así en el informe. Nada de lo que salga es "fuera de
  muestra" en sentido estricto: el juez final es la prueba EN VIVO.
- Todo umbral o elección que use resultados va DENTRO del pliegue (se elige con las ruedas de entrenamiento y se aplica tal cual a las
  de prueba). Lo que se fija de antemano (teoría) se dice acá.

## 1. ETAPA 1 — el detector de régimen (solo con lo anterior a cada instante)
**Unidad:** cada minuto m de la rueda (m = 0..389 desde las 9:30), al cierre del minuto (segundo 60m+59). Una operación en el segundo t
usa el estado del último minuto CERRADO antes de t. Puntos de control para medir el detector: m = 30, 35, …, 360 (10:00 a 15:30, cada 5 min).

**Rasgos (causales; fuente: los rasgos por segundo de la R19, `scratchpad\r19\rasgos`, y `hist/resumen.json`):**
- `rrn` = rango RTH hasta ahora / (mediana del rango RTH de las 10 ruedas previas × f(m)); f(m) = mediana de la fracción del rango del
  día ya hecha al minuto m, curva FIJA medida en las sesiones de MES 5 min ANTERIORES al 08-09-2026 (fuera de las 3 muestras).
- `vel30n` = (máximo − mínimo de los últimos 30 min) / (mediana de 10 × g(m)); g(m) = mediana del rango de 30 min a esa hora / rango del
  día, misma curva previa. Es la "velocidad" del rango.
- `z` = (precio − apertura RTH) / σ del minuto (área de ruido de Zarattini-Aziz-Barbon, σ = media de |cierre del minuto − apertura| de
  las 10 ruedas previas); `sost` = fracción de los últimos 30 cierres de 1 min con |z| > 1 del mismo lado que ahora.
- Balance inicial (desde las 10:30): extensión arriba = max(0, máximo − IBH)/rango IB, abajo = max(0, IBL − mínimo)/rango IB;
  un lado / dos lados / ninguno.
- Apertura contra ayer: dentro del área de valor de ayer / fuera del área pero dentro del rango / fuera del rango.
- VWAP RTH: pendiente de 60 min en X (o desde las 9:30 si m < 60), fracción de los últimos 60 cierres de 1 min del mismo lado que
  ahora, distancia en desvíos, cruces del VWAP en los últimos 60 min (histéresis 1 tick) y minutos desde el último toque (±1 tick).
- Marea (zigzag 4X): largo de la pierna en curso en X, largo de las 2 últimas piernas completas, pivotes desde las 9:30.
- Delta acumulado desde las 9:30 contra el precio: mismo signo que (precio − apertura) y correlación de 60 min de los cierres de 1 min.
- De la víspera y la apertura: eficiencia mediana de 10 ruedas (#19), rango de ayer / mediana de 10, rango de la noche (Asia + Londres)
  / mediana de 10, hueco (apertura − cierre de ayer) / mediana de 10.
- GEX (ronda 20): si `scratchpad\r20\gex_minuto.parquet` existe al cerrar la etapa 1, entra como rasgo pre-registrado en una nota
  nueva ANTES de usarlo; si no, queda el lugar (columna vacía) para enchufarlo.

**Etiquetas (ex post; SOLO para medir, nunca entran al detector):**
- **L1 (principal) "¿sigue o vuelve?":** s = signo(precio − VWAP RTH) en el punto de control; y = (cierre RTH − precio) × s / X.
  Tendencia si y > 0 (el desplazamiento desde el valor siguió hasta el cierre), balance si y < 0 (volvió).
- **L1z (la de la R19):** solo con |z| > 1, s = signo(precio − apertura); y = (cierre − precio) × s / X.
- **L2 "primer paso":** con s del VWAP, ¿qué llega primero después del punto de control: un toque del VWAP (que se mueve) o un extremo
  nuevo del día del lado s pasado por X/2? (va y vuelve / se extiende / ninguno al cierre).
- **L3 tipo de día:** e = |cierre − apertura| / rango RTH; tendencia e ≥ 0,6; balance e < 0,3; mixto el resto.

**Detectores pre-registrados (estado TENDENCIA con lado / BALANCE / DUDOSO):**
- **D0 (línea de base, #19):** BALANCE si la eficiencia mediana de las 10 previas < 0,465; si no, TENDENCIA con lado = signo(precio − VWAP).
- **D1 (rango):** TENDENCIA si rrn ≥ a; BALANCE si rrn ≤ b; DUDOSO entre. Grilla a ∈ {1,2; 1,4; 1,6}, b ∈ {0,8; 1,0}. Default (teoría y
  #43): a = 1,4, b = 1,0.
- **D2 (ruido sostenido):** TENDENCIA si |z| > 1 y sost ≥ k; BALANCE si |z| ≤ 1; DUDOSO si |z| > 1 y sost < k. Grilla k ∈ {0,6; 0,8; 1,0}.
- **D3 (puntos de Dalton, pesos iguales, umbrales de teoría fijados acá):** +1/−1 por cada pieza:
  rango (rrn ≥ 1,3 / ≤ 0,8); velocidad (vel30n ≥ 1,5 / ≤ 0,7); ruido (|z| > 1 con sost ≥ 0,8 / |z| ≤ 1); balance inicial desde las 10:30
  (un solo lado extendido ≥ 0,5 IB del lado del desplazamiento / dos lados, o ninguno después de las 12:00); VWAP (≥ 90 % de los últimos
  60 min del mismo lado y pendiente ≥ 1 X a favor / ≥ 2 cruces en 60 min); marea (pierna en curso ≥ 6 X a favor del desplazamiento /
  las 2 últimas piernas < 5 X); apertura (fuera del rango de ayer / dentro del área de valor); delta (mismo signo que el desplazamiento
  y correlación de 60 min ≥ 0,5 / correlación ≤ 0). TENDENCIA si puntaje ≥ k_hi; BALANCE si ≤ k_lo; grilla k_hi ∈ {2, 3, 4},
  k_lo ∈ {−1, −2, −3}. Default 3 / −2.
- **D4 (logística):** regresión logística con penalización L2 (C = 1) sobre los rasgos estandarizados con las ruedas de entrenamiento
  (rrn, vel30n, |z|·sost, extensión del IB de un lado, lado del VWAP 60, |pendiente| a favor, minutos desde el toque, pierna de la marea,
  delta a favor, noche, eficiencia 10), objetivo L1 (y > 0), en los puntos de control de entrenamiento. TENDENCIA si p ≥ q, BALANCE si
  p ≤ 1 − q; grilla q ∈ {0,55; 0,60; 0,65}.
- En todos: el lado de TENDENCIA = signo(precio − VWAP); si signo(precio − apertura) es otro, DUDOSO.

**Cómo se elige y cómo se juzga el detector (vara del régimen):**
- Métrica q por punto de control: +y si TENDENCIA (seguir el desplazamiento hasta el cierre), −y si BALANCE (desvanecerlo), 0 si DUDOSO,
  en X. Se promedia por rueda y se calcula **t por rueda**. Además: % de aciertos de TENDENCIA (y > 0), de BALANCE (y < 0), L2 (va y
  vuelve primero en BALANCE / se extiende primero en TENDENCIA), y el estado a las 11:00 y 12:30 contra el tipo de día L3.
- Dentro de cada pliegue: se elige el corte de la grilla con mayor t por rueda de q en ENTRENAMIENTO, con DUDOSO ≤ 60 % de los puntos.
- **Validación:** LOPO (3 pliegues: cada período afuera) y LODO con embargo ±2 ruedas (todas las ruedas, informado por período).
- **Placebo del detector:** las líneas de estado se sortean entre ruedas del mismo período de prueba (500 sorteos); p = fracción de
  sorteos con q ≥ el real.
- **El detector que pasa a la etapa 2** = el de mayor t de q sumando los 3 pliegues LOPO de prueba, siempre que le gane a D0 y a su
  placebo (p < 0,05) en al menos 2 de los 3 pliegues. Si ninguno cumple, pasa el mejor igual, rotulado "no separa", y se explica.
  5 detectores × sus grillas cuentan como variantes en `registro_variantes.csv`.

## 2. ETAPA 2 (se pre-registra en detalle ANTES de correrla, al cerrar la etapa 1)
Esquema ya fijado: BALANCE → vuelta al valor (núcleo de las 4 finalistas de la R19); TENDENCIA → continuación (retroceso de la ola a
favor de la marea con el VWAP detrás, ruptura con iniciativa, objetivos que dejan correr); DUDOSO → esperar. 1 a 5 operaciones por rueda,
una posición por vez. Salidas: 1R, 2R, VWAP, nivel siguiente, parcial + resto con stop en la entrada; nada de objetivo diminuto con stop
enorme. Vara doble: acierto Y neto con costo 0,50 y 1,00 pts de ES, t por RUEDA, ruedas positivas, placebo (régimen sorteado entre
ruedas; dirección al azar con el mismo R), corrección por la cantidad de variantes. **Meta con honestidad: acierto ≥ 60 % con neto > 0
en el período dejado afuera, en los 3 cortes.**

---
## Resultado de la ETAPA 1 (detector) — 27-11 04:47 AR (detalle en `salidas/detector.txt`, `salidas/detector.json`)
- Base (todos los puntos de control): el desplazamiento desde el VWAP siguió hasta el cierre en el 43,8 % (diseño 41,7, confirmación
  43,1, guardada 52,4): en las tres muestras el precio tiende a volver al VWAP al cierre, menos en la guardada.
- **D1 (rango hasta ahora contra lo normal a esa hora) eligió el mismo corte en los 3 pliegues: TENDENCIA si rrn ≥ 1,6, BALANCE si ≤ 1,0.**
  LOPO juntas (55 ruedas): t de q +1,89. Cuando dice TENDENCIA el desplazamiento sigue al cierre el 56,3 % (y +2,51 X) contra 43,8 % de base;
  en la guardada 74,2 % (y +5,72 X). Cuando dice BALANCE vuelve el 55,5 % (y −0,51 X): igual a la base (56,2 %). El primer paso (L2) sí
  separa: en TENDENCIA llega antes el extremo nuevo 60-67 %; en BALANCE el toque del VWAP 55-74 %.
- D3 (puntos de Dalton) t +1,50; D2 (ruido sostenido) +1,07; D4 (logística) +1,02 en LOPO pero +2,08 en LODO (su BALANCE vuelve 62,9 %);
  D0 (#19) −0,53 (confirmado: no sirve). **Ninguno cumplió la condición pre-registrada** (ganarle a D0 y a su placebo p < 0,05 en 2 de
  3 pliegues; los p van de 0,02 a 0,30). Por la regla escrita pasa D1, **rotulado "no separa" con la vara del placebo**: su TENDENCIA tiene
  información y su BALANCE no agrega sobre la base.
- GEX: a las 04:46 `scratchpad\r20\gex_minuto.parquet` NO existe (la ronda 20 está armando la tabla). Queda el lugar.

## ETAPA 2 — una familia por régimen — pre-registrada el 27-11 a las 04:52 AR, ANTES de mirar una sola operación por régimen
**Estados:** los de D1 (1,6 / 1,0). Para cada pliegue LOPO, el estado de cada minuto sale del corte elegido SIN el período de prueba
(columna `estado` de `estados_minuto.parquet`); para LODO, el corte elegido sin la rueda y su embargo ±2 (`estado_lodo`). Un evento en el
segundo t usa el estado del último minuto cerrado (m = t // 60 − 1). TENDENCIA trae su lado (s = signo(precio − VWAP) del minuto).
**Robustez (rotulada, no decide):** lo mismo con los estados de D3 y D4 en LOPO.

**Entradas (todas 10:00-15:30, una posición por vez entre las dos familias, máximo 5 por rueda):**
- BALANCE (solo en estado BALANCE) — vuelta al valor, núcleo de la R19, definiciones EXACTAS de `finalistas.py`:
  V1 = E4; V2 = E5b (a favor de la marea + VWAP RTH a ≥ 1R del lado de la ganancia + F1 o F6); V3 = E5b sin flujo; V4 = Z-C1 (banda 2σ
  respetada + iceberg); V5 = M-A (a más de 1,076 σ del VWAP de jornada + grandes a favor + pierna de marea ≤ 8,75 X); V6 = D-w6 (fade de
  la vuelta rápida; su salida es la de la R19, S6mc, y no lleva otras).
- TENDENCIA (solo en estado TENDENCIA y con lado = s) — continuación:
  T1 = vuelta de la ola a favor de la marea con el VWAP RTH DETRÁS (entrada del lado del desplazamiento, alejándose del valor);
  T2 = T1 + marea con agresores; T3 = T1 + iniciativa en el último minuto (F6 big trades netos a favor o `of_apilados_favor_3v` ≥ 1);
  T4 = ruptura con iniciativa (base de cruces, B2 de la R19: apilados a favor en 3 velas ≥ 1 + big trades fuertes a favor + a favor de la
  marea con agresores), con lado = s. La base de cruces de la guardada se arma con el MISMO código (`r19_10_cruces.cruces_dia`) en el
  scratchpad de la r21.
- DUDOSO → no se opera.

**Salidas (todas computadas desde la cinta con el motor de la R19; stop detrás del extremo, R ≈ X + 2 ticks; en cruces, stop = nivel ∓ X/2):**
- S6 = 1R; S1 = 2R (de la base).
- BALANCE: S5 = VWAP RTH; **P3 = mitad en 1R y el resto al VWAP con el stop pasado a la entrada.**
- TENDENCIA: S4 = raya de la marea; **P1 = mitad en 1R y el resto en 2R con stop en la entrada; P2 = mitad en 1R y el resto con la raya de la
  marea y stop en la entrada (deja correr).**
- Parciales: tras llenar 1R (pasarlo por 1 tick), el resto se busca desde el segundo siguiente (aproximación declarada: no se miran las
  órdenes del mismo segundo después del llenado). Acierto = operación con bruto > 0 (en las parciales equivale a que llegue 1R antes del stop).
- Costo 0,50 y 1,00 pts de ES por operación completa (por contrato).
Menú: BALANCE 5 entradas × 4 salidas + V6 = 21 variantes; TENDENCIA 4 × 5 = 20. Con las 22 del detector: 63 variantes en la ronda.

**Procedimiento dentro de cada pliegue (todo con las ruedas de entrenamiento):**
1. Para cada familia, entre sus variantes con neto > 0 (costo 0,50), n ≥ 20 y 1-5 por rueda en entrenamiento (cada una simulada sola, en su
   estado), se elige la de **mayor acierto**; empate (< 1 operación de diferencia) → mayor t por rueda. Si ninguna es elegible, esa familia
   no opera en el pliegue (esperar).
2. La estrategia = la elegida de BALANCE en estado BALANCE + la elegida de TENDENCIA en estado TENDENCIA, con una sola posición entre las dos.
3. Se aplica tal cual al período (LOPO) o a la rueda (LODO, embargo ±2) de prueba.

**Juicio (en el período dejado afuera, por separado y juntos):** n, operaciones por rueda, acierto, ganancia y pérdida media, neto con 0,50 y
1,00, t por RUEDA (ruedas sin operaciones = 0), ruedas positivas, racha y caída. **Placebos (200 sorteos):** PR = los tipos de estado de
cada rueda de prueba se sortean entre las ruedas del mismo período (el lado de TENDENCIA sigue siendo el del propio día); PD = mismas
entradas con lado al azar y el mismo R (salidas en R; S4/P2 → persigue 4X con stop en la entrada; S5/P3 → objetivo fijo a la distancia del
VWAP de la entrada). p = fracción de sorteos con neto ≥ el real. Corrección: t de Bonferroni con las 63 variantes (una cola 5 %, gl =
ruedas − 1) y por separado el t de Bonferroni de k = 3 pliegues para el procedimiento.
**META (pre-registrada): acierto ≥ 60 % y neto > 0 (costo 0,50) en el período dejado afuera en los 3 cortes LOPO.** Además se informa la
comparación clave: cada familia CON la llave del régimen contra la misma familia en TODOS los estados (¿la llave abre la puerta?).
**Mecánica:** 3 a 5 operaciones ejemplares por régimen (fecha, hora NY, precio), y la anatomía de ganadoras contra perdedoras por régimen
(cociente con IC) para lo que no llegue.

> Nota de hora (27-11 04:48 AR, agregada sin tocar lo escrito): el encabezado de la ETAPA 2 dice "04:52 AR" pero el reloj del sistema
> (`date`) marcaba **04:47** al guardarlo; la hora real del pre-registro de la etapa 2 es 04:47, antes de correr nada de la etapa 2.

---
## Resultado de la ETAPA 2 — 27-11 05:08 AR (detalle en `salidas/estrategia-D1.txt`, `-D3.txt`, `-D4.txt`, `salidas/mecanica.txt`)
- **D1 (el elegido), LOPO:** diseño afuera → BALANCE no opera (ninguna variante elegible) y TENDENCIA T1-S6: 30 ops, 56,7 %, neto +0,01
  (t 0,01); confirmación afuera → V4-S5 + T3-P2: 48 ops, 45,8 %, −1,10 (t −1,32); guardada afuera → V5-P3 + T1-S6: 26 ops, 65,4 %, +2,25
  (t 1,13; PD p 0,03; PR p 0,13). Juntas 104 ops 53,8 % +0,05 (t 0,08). **META: solo en la guardada (1 de 3). NO se cumple.**
- LODO (embargo ±2): 123 ops 58,5 % +0,41 (t 0,71); familia TENDENCIA 88 ops 60,2 % +0,76 (diseño 55,2 / confirmación 61,1 / guardada
  65,2 %); familia BALANCE 35 ops 54,3 % −0,48. La elección más frecuente: T1-S6 (40 de 55 pliegues).
- Robustez rotulada: con D3 juntas 41,7 % −1,30; con D4 52,7 % +0,05. D1 es la mejor llave de las tres.
- Mecánica (evento por evento, 55 ruedas, descriptiva): T1-S6 en TENDENCIA 52,8 / 53,8 / 54,9 % por período contra 44-47 % en BALANCE
  y DUDOSO (la llave separa en los 3 períodos, pero el piso queda en ~54 %); la banda de 2σ (V4) anda mejor en DUDOSO (74 / 58 / 67 %)
  que en BALANCE (67 / 44 / 33 %); "nivel en la zona" sube la continuación en TENDENCIA (63 contra 52 %, signo +++) y la baja en BALANCE
  (42 contra 53 %, −−−).

> Nota de hora (27-11 04:55 AR, agregada sin tocar lo escrito): el encabezado "Resultado de la ETAPA 2 — 05:08 AR" está mal; el reloj del
> sistema marcaba **04:55** al escribirlo. De acá en adelante cada hora se toma del reloj al guardar (`date`), no a ojo.

---
## ETAPA 3 — "evolución mecánica" — pre-registrada el 27-11 a las 04:56 AR (reloj del sistema), ANTES de simularla
**Advertencia escrita antes de correr:** esta etapa se diseña DESPUÉS de ver los resultados LOPO de la etapa 2 y la mecánica descriptiva
de las 55 ruedas. Sus números por período NO son validación (ya miré todos los períodos); sirven para decidir qué se lleva al juez en vivo.
**Mapa de régimen (corte de D1 fijo en 1,6 / 1,0, el mismo que se eligió en los 3 pliegues y con las 55 ruedas):**
- TENDENCIA (rrn ≥ 1,6, lado s): continuación.
- ROTACIÓN (1,0 < rrn < 1,6; el viejo DUDOSO): vuelta al valor en la banda de 2σ (V4 = Z-C1 de la R19).
- ESTRECHO (rrn ≤ 1,0; el viejo BALANCE): esperar.
Teoría (Dalton): un día que llega a la banda de 2σ con el rango dentro de lo normal es de rotación, donde el responsivo desvanece los
extremos; un día estrecho (rango por debajo de lo normal para la hora) tiene σ chico: tocar la banda no es una extensión real y el balance
puede romperse; un día expandido (≥ 1,6 veces lo normal) es de desequilibrio: manda la iniciativa.
**Variantes (reglas FIJAS, sin elección adentro; una posición por vez, máximo 5 por rueda, 10:00-15:30):**
- TENDENCIA (salidas S6 = 1R y P1 = mitad 1R + resto 2R con stop en la entrada): T5 = T1 + el extremo del retroceso en un nivel
  (`n_niveles_en_zona` ≥ 1); T6 = T1 + delta de 5 min a favor (`of_delta_300` > 0); T7 = T1 + al menos una de {nivel en la zona,
  barrida y vuelta, delta de 5 min a favor}. (T1 queda de control: ya está medida.)
- ROTACIÓN: V4-S6 en estado ROTACIÓN.
- Combinada: T7-S6 en TENDENCIA + V4-S6 en ROTACIÓN.
- GEX (tabla de la ronda 20, dato válido si `edad_s` ≤ 3.600; la guardada solo tiene 16-19-11): T7-S6 solo con gamma negativa
  (`regimen` = neg) y V4-S6 solo con gamma positiva. Teoría: con gamma negativa la cobertura de los dealers empuja a favor del movimiento
  (continuación); con positiva lo frena (reversión). Se informa también la división de T1-S6 y V4-S6 por signo de gamma.
- Cuentan 10 variantes nuevas → 73 en la ronda.
**Juicio:** por período (diseño / confirmación / guardada): n, acierto, neto 0,50 y 1,00, t por rueda, ruedas positivas, placebos PR y PD.
META igual que la etapa 2 (≥ 60 % y neto > 0 en los 3 períodos). Además, la elección anidada LOPO entre las 8 sin GEX (mayor acierto
con neto > 0 y n ≥ 20 en dos períodos, prueba en el tercero), y la versión con los estados LODO de D1.
**Mecánica descriptiva extra:** acierto de T1-S6 en TENDENCIA por orden de la operación en la rueda y por hora.

---
## Resultado de la ETAPA 3 (post hoc) — 27-11 04:58 AR (detalle en `salidas/etapa3.txt`)
- **Cumplen la META en los 3 períodos (elegidas MIRANDO, no validadas):**
  - **COMB-T7+V4** (TENDENCIA: T7-S6; ROTACIÓN: V4-S6): diseño 48 ops 60,4 % +0,43 (t 0,67) | confirmación 47 ops 63,8 % +1,15 (t 1,12) |
    guardada 27 ops 66,7 % +2,67 (t 1,97); juntas 122 ops 63,1 % +1,20 (t 2,12, 25/55 ruedas +). Con costo 1,00: −0,07 / +0,65 / +2,17.
    PR p 0,41 / 0,075 / 0,015; PD p 0,04 / 0,02 / 0,015.
  - **T6-P1** (T1 + delta de 5 min a favor; mitad 1R + resto 2R): 20 ops 60,0 % +0,42 | 32 ops 62,5 % +1,75 | 22 ops 72,7 % +3,38; juntas
    74 ops 64,9 % +1,88 (t 2,28). PR p 0,02 / 0,05 / 0,175; PD p 0,205 / 0,01 / 0,00.
- No cumplen: T1-S6 56,7 / 75,0 / 68,0; T7-S6 50,0 / 69,4 / 66,7; T5 50 / 69 / 55; V4-ROT 75,0 / 56,2 / 75,0.
- **Elección anidada LOPO entre las 8:** diseño afuera → T6-P1 (60,0 %, +0,42); confirmación afuera → V4-ROT (56,2 %, +0,50); guardada
  afuera → V4-ROT (75 %, 8 ops). Juntas 44 ops 61,4 % +1,06 (t 1,09). **El procedimiento anidado NO cumple en la confirmación.**
- t de las dos que cumplen (2,12 y 2,28) por debajo del Bonferroni de la ronda (~3,4 con 73 variantes).

## ETAPA 4 — vecindario del corte (descriptivo) — pre-registrado el 27-11 a las 04:58 AR, ANTES de correrlo
Para saber si COMB-T7+V4 y T6-P1 son zona estable o filo de cuchillo: mismas reglas con el corte de D1 movido a a ∈ {1,4; 1,6; 1,8} ×
b ∈ {0,8; 1,0; 1,2} (9 cortes × 2 reglas = 18 evaluaciones, se suman al registro como variantes: 91 en la ronda). Se informa acierto y
neto por período en cada corte; "estable" = cumple la META en ≥ 6 de los 9 cortes.

## Resultado de la ETAPA 4 (vecindario) — 27-11 04:59 AR (`salidas/vecindario.txt`)
- COMB-T7+V4 cumple la META en 2 de 9 cortes (1,6/1,0 y 1,8/1,0); T6-P1 en 3 de 9 (todos con a = 1,6; b no la afecta). **Filo de cuchillo
  para el 60 % en los 3 períodos.** Pero el SIGNO es una zona: juntas, COMB da 55,6-63,7 % con neto +0,20 a +1,20 en los 9 cortes, y T6-P1
  58,8-64,9 % con +0,90 a +1,88. El período flojo es siempre el diseño (49-64 %).

## Cierre — 27-11 05:06 AR
Resultados en `RESULTADOS.md`. La etapa 2 (la única con elección dentro del pliegue) no cumplió la meta. La candidata para el juez en vivo
(COMB-T7+V4, especificación en RESULTADOS §7) sale de la etapa 3, post hoc: se mide en 15 sesiones de bitácora sin tocar parámetros.
Comprobación desde la cinta cruda: 10 de 10. Baúl actualizado (#47-#53). Sin commits.

---
## ENMIENDA 5 — CONGELAMIENTO de COMB-T7+V4 y prueba en muestra nueva (MNQ) + prueba en vivo — 27-11 05:15 AR, ANTES de mirar MNQ
Pedido del coordinador (27-11 ~05:08 AR): probar la candidata CONGELADA, sin cambiarle nada, en otro instrumento y otro período, y dejar
armada la prueba en vivo. Lo único hecho antes de esta enmienda con MNQ: el inventario y la compactación de las cintas (`r21_09_mnq_inventario.py`,
`salidas/mnq_inventario.csv`: conteos, horarios, huecos y la base contra las velas de 5 min). Ningún rasgo ni resultado de regla de MNQ.

**La regla congelada (copia de RESULTADOS §7, sin cambios de parámetros):**
- Escala: med10 = mediana del rango RTH (9:30-16:00) de las 10 ruedas previas del MISMO instrumento; X = 0,08 × med10 al tick; ola = zigzag
  de X; marea = zigzag de 4X, orden por orden desde las 18:00 NY.
- Semáforo al cierre de cada minuto m: rrn = (máximo − mínimo RTH hasta ese minuto) / (med10 × f(m)), f(m) de `curva_rango_por_minuto.json`
  (10:00 0,433; 10:30 0,562; 11:00 0,673; 11:30 0,759; 12:00 0,816; 12:30 0,851; 13:00 0,890; 13:30 0,942; desde 14:00 1,000).
  **TENDENCIA** si rrn ≥ 1,6 y el precio del minuto está del mismo lado del VWAP RTH y de la apertura (ese es el lado); **ROTACIÓN** si
  1,0 < rrn < 1,6; **ESTRECHO** si rrn ≤ 1,0 (no se opera). Si rrn ≥ 1,6 y los lados no coinciden: no se opera. *Aclaración escrita antes de
  mirar:* en el código de la etapa 3 esos minutos (1,2 % de los de 10:00-15:30 en ES) caían en ROTACIÓN; no cambiaron ninguna operación
  (0 de las 49 de V4 tenían rrn ≥ 1,6). Se congela la versión escrita: no se opera. Una señal en el segundo t usa el último minuto cerrado.
- **T7 (TENDENCIA):** vuelta de la ola a favor de la marea, del lado de la TENDENCIA, con el VWAP RTH detrás, y al menos una de: extremo del
  retroceso en la zona de un nivel (de L − X/4 a L + X/8 del lado del retroceso; la lista de 31 niveles de la R19 + redondo de 50), barrida de
  un nivel hasta X/2 y vuelta, delta de los 300 s previos a favor.
- **V4 (ROTACIÓN, = Z-C1 de la R19):** extremo del retroceso entre la banda de 2σ (VWAP RTH o de jornada) del lado opuesto y X/2 antes, sin
  perforarla, + iceberg del lado que defiende a ≤ X/2 del extremo en los 60 s previos.
- **Gestión:** entrada = último precio del segundo de la vuelta; stop = extremo del retroceso ∓ 2 ticks; objetivo 1R pasado por 1 tick;
  10:00:00-15:29:59 NY; una posición por vez; máximo 5 por rueda; todo cerrado a las 15:59:59.
- Archivo de la regla: `regla_congelada_comb_t7_v4.py`. **Verificación antes de congelar:** sobre las 55 ruedas de ES reproduce EXACTO las
  122 operaciones de la etapa 3 (mismo día, segundo, lado, familia y resultado; 60,4 / 63,8 / 66,7 %).

**SHA-256 (también en `regla_congelada_huellas.json`). Después de esto no se toca ningún parámetro ni ninguno de estos archivos:**
- regla_congelada_comb_t7_v4.py dd8e18ad534b4fcefbb61dd2826358bda318f8dcd1763c475b62f5791795cb8a
- curva_rango_por_minuto.json 72589182d1533ccab0903e70469e8654dc7a201197d88064238dd1a2d7a6bc33
- r21_01_regimen.py 28f081cc901f9b7a9ea049994a671709fbcb968321a9d36f693491a7de135f3b
- r21_comun.py bf0b10824ba648f1dc28d54aecc75c4b466d3e57c8d08a27f1f1d43b313d88e7
- ronda19: r19_comun.py a7d2056070d0eca89b26fea7aff0a73857c9ba52368d9d4bb7de44aec807e19d; r19_00_inventario.py
  8dac9c518b5ab712192feebcc81506b07b34cc930ebe8355955c1d7c9c9065ea; r19_01_rasgos.py 006e4b6828d8419d0989778d80fca56098c2706bff59be71eac0c0302a1d164f;
  r19_02_motor.py f23ef1f834adec95c05b620c2f003c3593d1698d810690a591b58c9fb84e12a0; r19_08_eventos.py
  6ea788fa598c313a3ff7e5e113473878a53dadf9c215232f6cf7b9ef983418ae

**Prueba en MNQ (muestra nueva; nunca usada en las rondas 19 ni 21):**
- Cintas: `cinta-MNQ-*` del 31-07 al 14-09 (contrato U6: coinciden con el MNQ continuo de 5 min y quedan 289-298 pts debajo de Z6; las del
  29 y 30-07 están vacías) y `cinta-MNQZ6-*` del 15 al 21-09 (Z6; la del 22-09 es solo noche, no es rueda). 36 ruedas con cinta.
- **Calentamiento:** las 10 primeras ruedas con cinta (31-07 a 13-08), solo para X, umbrales y ruido. La historia previa de esas 10 (rango, ayer,
  ruido) sale de las velas de 5 min de NQ del 27 al 30-07, como la R19 usó las de ES.
- **Evaluadas:** 14-08 a 21-09 = 25 ruedas. **Excluida por regla: 17-09** (la cinta termina a las 15:41 NY: rueda incompleta; queda como
  historia). Se informa además el subconjunto **estrictamente anterior al 10-09** (14-08 a 09-09, 18 ruedas: ninguna coincide en fecha con
  una rueda de ES evaluada) y el resto (10-09 a 21-09, 7 ruedas, que coinciden en fecha con ruedas del diseño de ES). Ojo: 14-09 es U6 en su
  última semana (la mitad de órdenes que una rueda normal: la liquidez ya se iba a Z6).
- **Roll U6 → Z6:** spread medido el 14-09 (MNQ U6 contra las velas de MNQZ6, 76 velas de la rueda): **+298,00**. Los niveles de varios días
  (ayer, VWAP semanal y mensual, área de valor de la semana anterior) que cruzan el roll se ajustan con +298,00, como el +67,00 de ES.
- **Pipeline idéntico** (los archivos de arriba, sin tocar): X, umbrales (percentiles por franja de 30 min) y ruido con las 10 ruedas previas
  de MNQ; la misma curva f(m). El orquestador para MNQ (rutas, roll, lista de ruedas) es plomería nueva y se registra con su SHA-256 antes de
  correrlo. Único agregado: el mínimo de volumen de los imbalances apilados para MNQ = 30 (el de ES); los apilados no entran en T7 ni en V4.
- **Costo:** 0,50 y 1,00 punto de NQ por operación.
- **Vara:** n, operaciones por rueda, acierto, neto 0,50 y 1,00, t por rueda, ruedas positivas; placebos PD (mismas entradas, lado al azar,
  mismo R, 1R simétrico desde la cinta) y PR (estados sorteados entre las ruedas evaluadas); las dos familias por separado y juntas.
  **"Pasa en MNQ"** = acierto ≥ 60 % y neto > 0 con costo 0,50 en las 25 ruedas juntas; se anota como "pasó en MNQ, falta en vivo con ES".
  Si no pasa, NO se ajusta ni se vuelve a correr.
- **Lectura del flujo (pedida, descriptiva, no son candidatas):** como el flujo de MNQ es en buena parte copia del NQ grande, se informa
  también, sin elegir nada, T1 sin la confluencia (régimen + estructura sola) y la banda respetada sin iceberg (estructura sin flujo), para
  decir si se cae la parte de flujo y se sostiene la de régimen + estructura.

**Prueba en vivo (ES, muestra virgen desde el 30-11):** `r21_11_vivo.py` (SHA-256 al crearlo) toma cada cinta nueva de ES posterior al 25-11,
corre el pipeline y la regla congelados (verificando las huellas antes) y agrega sus operaciones a `bitacora_en_vivo.csv` (fecha, hora, régimen,
regla, lado, entrada, stop, objetivo, resultado, motivo, rrn, R). **Exclusiones por regla:** (1) rueda corta (27-11-2026 por Acción de
Gracias, 24-12-2026): no se opera y no entra en la historia (ni en la mediana de 10, ni en umbrales, ni como día anterior, ni en los VWAP
semanal y mensual); (2) cinta incompleta (última orden de la rueda antes de las 15:59:00 o un hueco > 300 s en la rueda): no se evalúa;
(3) roll de diciembre (Z6 → H7): si la cinta cambia de contrato, los niveles de varios días se ajustan con el spread MEDIDO; sin spread
medido, la rueda queda afuera (no se inventa). **Empieza con la rueda del lunes 30-11-2026.** Juez: 15 sesiones; acierto ≥ 60 % y neto > 0
con costo 0,50, sin tocar nada.

> Nota (27-11 05:18 AR, antes de correr la prueba en MNQ): plomería registrada. `r21_09_mnq_inventario.py`
> d96f420ab1924d332d50b861231bff7d5c3cc982369ef1903d31d4cfc11227fc; `r21_10_mnq_prueba.py` c11436230b805a7f5671465fdff6a4697a3909168d1eb2cf816c8c70cff91f40.
> El orquestador verifica las huellas de la regla congelada antes de correr y se niega si alguna cambió. Si tuviera que corregir un error de
> plomería (un cuelgue), se anota acá con su nueva huella ANTES de mirar resultados; ningún parámetro de la regla se toca.

## Resultado de la prueba CONGELADA en MNQ — 27-11 05:23 AR (`salidas/mnq_prueba.txt`, `salidas/ops-MNQ-COMB-T7+V4.csv`)
Corrida UNA vez, con las huellas verificadas (9 de 9) y la plomería registrada arriba sin cambios. Comprobación desde la cinta cruda de MNQ:
6 de 6 operaciones coinciden (entrada, rango relativo, lado del VWAP y resultado).
- **25 ruedas evaluadas (14-08 a 21-09, sin el 17-09):** 46 ops (1,84 por rueda), acierto **52,2 %**, bruto +0,38 pts de NQ, neto c0,50 **−0,13**
  (t −0,04), c1,00 −0,63 (t −0,20), 7 de 25 ruedas positivas. Por familia: T7 29 ops 58,6 % +2,47; V4 17 ops 41,2 % −4,56. **NO PASA** (meta:
  ≥ 60 % y neto > 0). No se ajusta ni se vuelve a correr.
- **Estrictamente antes del 10-09 (18 ruedas, sin coincidencia de fecha con ES):** 27 ops, 40,7 %, −4,94 (t −1,47); T7 43,8 %, V4 36,4 %.
- 10-09 a 21-09 (7 ruedas, mismas fechas que el diseño de ES): 19 ops, 68,4 %, +6,72 (t 1,28); T7 76,9 %, V4 50 %.
- Placebos: PD (lado al azar) p 0,45 juntas (T7 p 0,21, V4 p 0,79); PR (estado sorteado entre ruedas) p 0,35.
- Lectura del flujo (descriptiva): la banda sin iceberg da 47,7 % (con iceberg 41,2 %): en MNQ el iceberg no suma; T1 en TENDENCIA sin la
  confluencia da 57,6 % en las 25 y 44,4 % antes del 10-09: **régimen + estructura tampoco se sostiene en la parte limpia.**
- Distribución del semáforo en MNQ (10:00-15:30): ESTRECHO 46,5 %, ROTACIÓN 42,0 %, TENDENCIA 9,7 % (en ES ~18 %): la mediana de 10 de
  mediados de agosto venía inflada por las ruedas de 650-750 pts de fines de julio, y las ruedas del 14 y 17-08 fueron ESTRECHO todo el día.

## Prueba en vivo armada — 27-11 05:23 AR
- `r21_11_vivo.py` SHA-256 0bad8775e2af2b604101dfc65a9dab7efda6e41fd9a135a734784ab2dc286809. Estado durable en %LOCALAPPDATA%\PythiaGex_r21_vivo
  (historia de ES copiada de la R19; si falta, se arma de nuevo).
- **Verificado de punta a punta desde la cinta cruda** (compactación → rasgos → vueltas → semáforo → regla) en dos jornadas YA conocidas, con
  la historia hasta la víspera: 25-11 (5 de 5 operaciones T7 idénticas) y 20-11 (4 V4 + 1 T7 idénticas). No escribió la bitácora.
- `bitacora_en_vivo.csv` (vacía, con encabezado) y `bitacora_jornadas.csv` con el 26-11 (feriado) y el **27-11 excluido por regla (rueda corta)**.
  Primera jornada a correr: **lunes 30-11-2026**. Desde el 07-12 (semana del roll de diciembre) cada cinta necesita su contrato confirmado en
  `vivo_contratos.json` y, si cambia de Z6 a H7, el spread medido; si no, la jornada queda afuera.
