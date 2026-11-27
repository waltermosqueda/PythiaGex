# Ronda 22 — El clima de la R21 en velas de 5 min (un año de MES) — RESULTADOS

27-11-2026, 07:22 a 08:00 AR. Pre-registro con hora y huellas SHA-256 en `PREREGISTRO.md` (escrito a las 07:47; código congelado a las 07:46).
Corrida ÚNICA a las 07:47-07:48: `salidas/h1.txt`, `h2.txt`, `h1b.txt` y `robustez.txt`. La mecánica, post hoc, está en `salidas/mecanica.txt`.
El registro de variantes es `registro_variantes.csv` (38 filas). Nada de esto promete rentabilidad.

## Veredicto en cuatro líneas
1. **El semáforo de la R21 no pasó en un año de MES (242 ruedas).** Cuando marca "desatado", el precio hace un extremo nuevo antes de volver
   al promedio del día el 54 % de las veces, contra 41 % en cualquier momento. La vara pedía 55 % y 15 puntos de ventaja. Tampoco anticipa
   que el día siga hasta el cierre: 45 % contra 47 %.
2. **Casi toda esa ventaja es "dónde está parado el precio"**: cuando el semáforo se enciende, el precio ya está lejos del promedio y cerca
   del extremo. Comparado con otros momentos con esa misma ubicación, el semáforo agrega 2 puntos; en las ruedas de la R21 agregaba 6 a 10.
3. **Las cuatro reglas llevadas a velas de 5 min pierden plata**: aciertan entre 42 y 48 % y dejan entre −2,1 y −2,9 pts por operación con
   1 pt de costo. Ninguna pasa. En las velas de 30 min de 2024-25 el semáforo quedó a medio punto de la vara (55 % contra 41 %).
4. **El porqué:** el precio vuelve al promedio del día más que si se moviera al azar, y en este año eso siguió pasando aunque el día
   estuviera "desatado". Además, la regla mide contra las 10 ruedas previas: después de semanas calmas, un día normal parece desatado (falsa
   alarma), y después de semanas agitadas, el semáforo se queda mudo. Y en 5 min la entrada llega tarde, después del empujón.

## Para el operador, en una idea
El semáforo es un cuentakilómetros que compara el viaje de hoy con los últimos 10 viajes. Si los últimos 10 fueron vueltas a la manzana,
cualquier viaje normal a la ruta parece "desatado"; si fueron viajes largos, uno largo de verdad parece normal. En septiembre-noviembre de 2026
(las ruedas de la R21, con la volatilidad subiendo) el semáforo veía algo más que la pantalla. Pero en el año anterior, casi siempre te decía
algo que ya estaba a la vista: que el precio estaba lejos del VWAP y cerca del máximo o del mínimo del día. Y ahí, igual que en cualquier día,
el precio tendía a volver al VWAP.

## 1. Qué se hizo
- **Muestra nueva:** `velas-MES-m5.csv` (solo lectura). Son 265 jornadas en el período y 252 ruedas completas. El calentamiento fue del 22-08
  al 08-09-2025, y se evaluaron **242 ruedas, del 09-09-2025 al 31-08-2026**. Las 10 ruedas cortas (feriados y medias ruedas) quedaron fuera
  de todo; las 3 incompletas por hueco de datos (27-08-2025, 20-02 y 19-03-2026) entraron solo como historia. Para H1b se usó
  `velas-MES-m30.csv`: **325 ruedas, del 29-04-2024 al 20-08-2025**.
- **Traducción a 5 min** (sección 2 del PREREGISTRO): D1 al cierre de cada vela; VWAP de la rueda y de la jornada con (máx + mín)/2; X = 0,08 ×
  mediana de 10; ola y marea sobre el camino de 4 puntos de cada vela; delta de la vela; los 31 niveles de la R19 más el redondo de 50;
  barrida; banda de 2σ; entrada al cierre de la vela de la vuelta; regla conservadora dentro de la vela; cambio de contrato con spreads medidos.
- **Calibración solo con cinta** (sección 2), **pre-registro, corrida única, mecánica post hoc** (sección 7) y **comprobación desde el
  crudo con código propio** (sección 8).

## 2. La traducción y su fidelidad (calibración, hecha antes de mirar)
- **Estado D1 cada 5 min:** coincide **100,0 %** con el de la cinta en el mismo instante (3.819 puntos, 57 ruedas de ES con velas armadas
  desde la cinta) y **99,6 %** con las velas de MES reales (268 puntos, 4 ruedas: 08, 17, 18 y 21-09-2026; el archivo no tiene del 09 al
  16-09). Contra el minuto siguiente, que es el punto de control de la R21, coincide 98,5 %.
- **VWAP:** el error mediano es 0,13 pts con (máx + mín)/2 contra la cinta, y 0,28 pts con MES. El desvío, con la varianza dentro de la vela,
  queda a 0,6 % (sin ella, 2,8 %). **Primer paso (L2):** coincide 99,5 %. **Orden dentro de la vela** (alcista: mínimo primero): acierta 87,9 %.
- **Delta:** el de 5 min del MES tiene el mismo signo que el del ES en el 72,4 % de las velas.
- **Lo que no se traduce:** las velas de 5 min ven **solo el 53 % de las vueltas de la ola** (las que duran más de una vela). Y la entrada a
  stop dentro de la vela tiene un sesgo grande. En las ruedas de la R21, T7-S6 dio **86,6 %** con el orden supuesto y **29,2 %** con la regla
  conservadora, contra **62,8 %** de la cinta en las mismas ruedas. El mecanismo: la vela esconde las vueltas que fallan dentro de ella (el
  mínimo nuevo pisa al extremo viejo y la operación perdedora desaparece), y la regla conservadora castiga cualquier vela ancha. Por eso las
  variantes se probaron **entrando al cierre de la vela de la vuelta**. Con esa entrada, en las mismas ruedas de la R21, dieron 54,4 / 56,0 /
  57,4 / 64,1 % (T7 / T6-P1 / COMB / V4e), contra 62,8 / 64,9 / 63,1 / 69,2 % de la cinta: un esqueleto más grueso y con menos filo.

## 3. H1 — el clima (5 min, 242 ruedas): NO PASA
Vara: con TENDENCIA, ≥ 55 %, ≥ base + 15 puntos y un IC por rueda de la diferencia mayor que 0.
| Corte | Puntos en TENDENCIA (ruedas) | Extremo antes que el VWAP: TENDENCIA / base | Dif. [IC 95 %] | Sigue hasta el cierre: TEND. / base |
|---|---|---|---|---|
| **Todas** | 1.436 = 8,9 % (69 de 242) | **53,6 / 41,3 %** | **+12,2 [+2,6 a +21,3]** | 45,2 / 46,9 % |
| 2025T3 (sep) | 26 (3 de 16) | 23,1 / 47,4 | −24,3 | 0,0 / 49,2 |
| 2025T4 | 502 (20 de 62) | 54,0 / 41,5 | +12,5 [−2,9 a +28,4] | 52,2 / 45,4 |
| 2026T1 | 420 (21 de 59) | 54,3 / 42,5 | +11,8 [−4,0 a +27,6] | 37,4 / 42,3 |
| 2026T2 | 335 (19 de 62) | 59,7 / 40,6 | +19,1 [−10,5 a +36,1] | 58,5 / 53,0 |
| 2026T3 (jul-ago) | 153 (6 de 43) | 41,8 / 38,4 | +3,4 | 22,2 / 45,7 |
- **Por volatilidad** (tercil de la mediana de 10): con volatilidad baja (≤ 51,9 pts), 50,0 contra 44,0 %; media, **55,2 contra 38,6 %
  (pasaría: +16,6 con IC +2,6 a +30,3)**; alta (> 73,5 pts), 64,0 contra 41,4 %, pero con solo 3,3 % de los puntos en TENDENCIA y un IC de
  15 a 87 %.
- **Por clima heredado** (mediana de 10 / mediana de 60): si las 10 previas fueron más calmas que las 60, hubo TENDENCIA en el 13,3 % de los
  puntos y acertó 52,9 %; con 10 normales, 9,3 % de los puntos y 52,8 %; con 10 más agitadas, solo 4,5 % de los puntos y 59,9 %. Ninguno pasa.
- **Los otros estados:** ROTACIÓN (34 % de los puntos) da 44,0 % de extiende y 44,1 % de vuelve; ESTRECHO (56 % de los puntos) da 38,3 % y
  51,7 %. Lo mismo que en la R21: el estado "chico" no anticipa la vuelta mejor que la base, que es 47,7 %.

## 4. H1b — velas de 30 min, 2024-25 (325 ruedas): NO PASA (le faltan 0,6 puntos)
TENDENCIA dio 55,3 % [IC 48,2-62,1] contra 40,9 % de base: **+14,4 [IC +7,8 a +20,8]**. Cumple el 55 % y el IC, pero no el +15. Sigue hasta
el cierre: 47,3 contra 51,5 % (en contra). Por trimestre solo pasa 2025T1 (64,1 %, +22,7); 2024T2 da 43,8 %. Con la geometría controlada,
en 30 min el semáforo agrega −8,1 puntos en 2024 y +0,7 en 2025 (sección 7).

## 5. H2 — el esqueleto operable: NO PASA NINGUNA (todas pierden)
La vara pedía acierto ≥ 60 %, neto > 0 con costo 1,00 y t por rueda ≥ 2,26 (Bonferroni, k = 4). Todas con entrada al cierre de la vela de la vuelta.
| Variante | Ops (por rueda) | Acierto | R medio | Neto c0,50 | Neto c1,00 (t) | Placebo de lado p |
|---|---|---|---|---|---|---|
| T7-S6 | 145 (0,60) | 46,2 % | 10,99 | −1,62 | −2,12 (−1,99) | 0,84 |
| T6-P1 | 127 (0,52) | 48,0 % | 11,79 | −2,37 | −2,87 (−2,53) | 0,72 |
| COMB-T7+V4e-S6 | 333 (1,38) | 43,2 % | 8,91 | −1,62 | −2,12 (−3,61) | 0,95 |
| V4e-S6 | 202 (0,83) | 41,6 % | 7,25 | −1,70 | −2,20 (−3,67) | 0,93 |
- **Por trimestre**, T7-S6 da 0 / 49,0 / 40,4 / 56,2 / 40,0 % (2025T3 a 2026T3) y T6-P1 da 0 / 51,2 / 38,6 / 63,0 / 46,2 %. **En la COMB**,
  la parte T7 da 46,6 % (133 ops) y la V4e 41,0 % (200). Por trimestre, la V4e da 42,9 / 36,6 / 53,1 / 34,7 / 35,3 %.
- **Por volatilidad y por clima heredado:** ningún tercil llega al 60 % ni deja neto positivo con costo 0,50. El mejor es T7 con 10 previas
  normales (55,1 %, −0,18 con costo 0,50).
- **El placebo de lado** (las mismas entradas con el lado sorteado) le gana a la regla real en 72-95 % de los sorteos: el lado elegido no
  aporta. En la banda, el lado contrario anduvo mejor (sección 7).

## 6. Robustez (no decide)
- **f(m) sin el trimestre que se prueba:** H1 da 53,3 % contra 41,3 % (+12,0), casi igual, y H2 da 46,3 / 46,9 / 44,0 / 42,5 %. Control: la
  curva rehecha con el procedimiento de la R21 reproduce la congelada exacto (256 ruedas, diferencia 0).
- **f(m) externa** (30 min de 2024-25, que la R21 no vio): es más chica a la mañana (10:00 0,366 contra 0,433), así que marca más TENDENCIA
  (12,0 % de los puntos). H1 da 52,0 contra 41,3 % (+10,7) y H2 da 45,3 / 45,1 / 45,0 / 44,3 %. **La limitación de f(m) no cambia nada.**
- **Empates** del primer paso: 33 de 16.214 puntos, ninguno en TENDENCIA.
- **Cotas de la entrada a stop dentro de la vela** (sesgadas, no deciden): con el orden supuesto, T7-S6 da 66,7 %, +1,06 con costo 0,50 y
  +0,56 con 1,00 (t 1,43, no llega a Bonferroni). En calibración esa versión inflaba +24 puntos (86,6 contra 62,8 %). La conservadora da 17,6 %.

## 7. La mecánica: qué pesó, en qué clima y por qué
**7.1 El factor que más pesó es el "elástico" del VWAP.** Supongamos que el precio se moviera al azar entre dos metas fijas: el extremo + X/2
de un lado y el VWAP del otro. Entonces llegaría primero a la más cercana, en proporción a las distancias. Con la ubicación real de cada punto,
eso daría 48,9 % de "extremo primero"; el mercado dio 41,3 %. En TENDENCIA, el azar daría 63,8 % y el mercado dio 53,6 %. Es decir, el precio
vuelve al promedio del día 7-10 puntos más que el azar, **y en este año eso pasó igual en los días "desatados"**. En las ruedas de la R21,
TENDENCIA casi apagaba el elástico: 62,7 % contra 66,6 % del azar (en el resto de los momentos, 37,6 contra 47,4). Eso es lo que la R21 vio y
este año no repitió.

**7.2 Geometría contra clima.** Comparemos TENDENCIA con el resto dentro del mismo casillero: misma distancia al extremo, misma distancia al
VWAP y misma franja horaria. En el año de MES, el semáforo agrega −1,9 / −3,1 / +1,0 / **+16,2** / −3,9 puntos por trimestre (+2,4 ponderado).
En 30 min, −8,1 en 2024 y +0,7 en 2025. En la R21, por período, +8,4 / +10,2 / +6,1. El +23-27 sin controlar de la R21 era sobre todo ubicación:
la R21 ya lo avisaba ("parte del primer paso es geometría"), y ahora está medido. **Solo 2026T2 se parece a la R21.**

**7.3 La vara de 10 ruedas (el mecanismo de MNQ, ahora en un año entero).**
- Con 10 previas más calmas que las 60, el semáforo marca TENDENCIA en el 13,3 % de los puntos, pero esas ruedas solo eran 1,25 veces la
  mediana de 60, y 8 de 26 terminaron en balance.
- Con 10 previas más agitadas, marca TENDENCIA en el 4,5 % de los puntos, y esas ruedas eran 1,79 veces la mediana de 60.
- **Post hoc:** con la TENDENCIA confirmada por las dos varas (10 y 60), el extremo llega antes en el 57,5 % de los casos y sigue hasta el
  cierre en el 48,3 % (n 1.006; +5,4 sobre la misma geometría). Con TENDENCIA solo por la vara de 10, cae a 44,4 % y 37,9 % (n 430), peor que
  la base: esas son las falsas alarmas después de semanas calmas.

**7.4 "Desatado" no separa el día que sigue del que vuelve.**
- De las 69 ruedas que pasaron por TENDENCIA, 35 % terminaron en tendencia (la base es 33 %) y 29 % en balance (base 33 %).
- De las 81 ruedas de tendencia, solo 24 pasaron por TENDENCIA.
- El primer TENDENCIA mediano llega a las **10:10**: casi siempre es el empujón de la apertura, que este año volvió tanto como siguió.
- Ex post: en los días que terminaron en tendencia, la TENDENCIA acertó el primer paso 65 % y siguió al cierre 74 %; en los que terminaron en
  balance, 31 % y 10 %. La pieza que falta es la que ya pedía la R21: distinguir "expandido que sigue" de "expandido que vuelve".

**7.5 La hora.** La TENDENCIA de 11:00 a 12:25 acertó el primer paso 67,3 % contra 49,7 % de la franja (n 367). La de 10:00 a 10:55, 54,5 contra
43,9; la de 14:00 a 15:30, 39,7 contra 30,6. Hasta el cierre, solo la de 11:00 a 12:25 sigue apenas más que la base (50,7 contra 47,5 %);
las otras, igual o menos.

**7.6 Por qué pierde la T7 en 5 min: persigue.**
- La entrada al cierre de la vela queda a 2,0 X del extremo (mediana), cuando la R21 entraba a ~1 X. El R medio es de 11 pts (2,5 X).
- Cuanto más lejos del extremo, peor: a menos de 1,5 X acierta 52,4 % (−0,52 por operación con costo 1,00); a 3 X o más, 40,5 % (−4,41).
- Las piezas de la T7 no separan: con nivel 46,9 % contra 45,8 % sin él; con barrida 43,1 % contra 48,8 %; con delta a favor 46,2 % contra 46,7 %.
- Después de la vela del empujón lo más común es el retroceso: el lado contrario con el mismo R gana 52,4 %. Es el mismo mecanismo del baúl
  (#11 y #12, y "persigue" de la ronda 9), ahora a escala de 5 min.

**7.7 La banda de 2σ en ROTACIÓN (sin iceberg): este año fue puerta, no elástico.**
- Desvanecerla ganó 41,6 % y seguir la ruptura con el mismo R, 53,0 %.
- De 10 a 12 h dio 47,3 contra 47,3 %; de 12 a 14 h, 37,3 contra 57,6 %; de 14 a 16 h, 29,0 contra 64,5 % (n 31).
- En los días que terminaron en tendencia o mixtos, desvanecer acertó 39 %; en los de balance, 46 %.
- En las ruedas de la R21 (velas de la cinta), la misma banda daba 64,1 %. O lo que la sostenía era el iceberg, que no se ve en 5 min, o fue
  cosa de esos meses.

**7.8 En una línea:** el semáforo funciona cuando el mercado cambia de marcha (sep-nov 2026: el rango relativo mediano por período fue 1,04 →
1,09 → 1,44, y 2026T2); en un año "normal" (rango relativo mediano 1,01) solo describe la ubicación del precio, y el elástico del VWAP manda.

## 8. Comprobación desde los datos crudos (10 de 10)
`r22_04_verificar.py` lee el CSV crudo con el módulo `csv` y `zoneinfo`, sin el motor. Rehace la mediana de 10, X, el VWAP, el rango relativo,
el estado, el pivote, la banda (V4e), el resultado con la regla conservadora y la vela de salida. **Coinciden 10 de 10 operaciones** (8
distintas; dos aparecen en T7 y en T6-P1 con salidas distintas). Horas NY de cierre de la vela de entrada:
1. **10-10-2025, 11:25 — VENTA (T7) a 6.693,50** (extremo 6.726,50, R 33,50, rango 3,34 veces lo normal): **+33,50** a las 11:40. La misma
   operación en T6-P1: **+16,75** (1R y el resto en la entrada).
2. **10-10-2025, 13:55 — VENTA (T7) a 6.653,25** (R 9,50): **stop −9,50** a las 14:15.
3. **10-10-2025, 14:30 — VENTA (T6-P1) a 6.654,75** (R 10,75): **+16,12** (1R + 2R) a las 14:50.
4. **09-01-2026, 10:25 — COMPRA (T7) a 6.989,25** (extremo 6.976,50, R 13,25, rango 1,63): **+13,25** a las 11:50.
5. **01-10-2025, 12:05 — VENTA (V4e) a 6.743,50** en la banda de 2σ (rango 1,00): **+4,00** a las 12:55.
6. **09-10-2025, 10:05 — COMPRA (V4e) a 6.789,00** (rango 1,19): **stop −5,75** a las 10:20.
7. **17-04-2026, 10:55 — VENTA (V4e, COMB) a 7.169,75** (rango 1,32): **+5,50** en la vela siguiente.
8. **05-06-2026, 10:00 — VENTA (T7, COMB) a 7.511,50** (extremo 7.525,75, R 14,75, rango 1,99): **stop −14,75** a las 10:10.

## 9. Pistas post hoc (al baúl #58-#64) y candidatos para la bitácora en vivo
Todo esto salió **después** de mirar: son pistas, no resultados probados. La bitácora en vivo de la R21 (`r21_11_vivo.py`, congelada) no se
toca. Lo que sigue son observaciones para anotar al lado:
- anotar **rrn contra la mediana de 60** junto al de 10, y marcar si la TENDENCIA viene "con las dos varas" (57,5 % contra 44,4 %);
- anotar la **hora** de cada TENDENCIA (la franja de 11:00 a 12:25 fue la mejor);
- en la banda de 2σ, anotar **si la ruptura sigue**, sobre todo después de las 12;
- anotar **a cuántas X del extremo** queda la entrada (entrar lejos persigue).

## 10. Limitaciones y notas de honestidad
1. **f(m) se midió con estas mismas velas** (256 sesiones de MES de 5 min, del 22-08-2025 al 04-09-2026). Los dos controles (f sin el
   trimestre y f externa de 2024-25) no cambian ninguna conclusión.
2. **El diseño de H2 cambió antes del pre-registro** (de la entrada a stop a la entrada al cierre) porque la calibración midió el sesgo de la
   entrada a stop. Para eso miré resultados de operaciones en las ruedas de la R21, que ya estaban vistas; ninguna del período independiente.
   Está declarado en el PREREGISTRO, sección 0.
3. **Lo que esta ronda NO juzga:** la COMB congelada de la R21 a su escala nativa (segundo de la vuelta, R ≈ X + 2 ticks, iceberg). Las velas de
   5 min no pueden. La prueba en vivo con ES sigue siendo el juez de esa regla. Este año sí dice que el clima D1, solo, no es una llave estable.
4. El delta del MES es solo un proxy del delta del ES (72 % de mismo signo). La V4e no tiene iceberg. El spread del roll de septiembre de 2025
   es estimado (±5 pts). Hubo una sola corrida y ningún parámetro cambió después.
5. **Corrección a la R21:** el "60-67 % contra 37-40 %" del primer paso era en buena parte ubicación del precio. Por período, el clima
   agregaba +6 a +10 puntos sobre la misma geometría, no los +23-27 del número crudo.

## 11. Entregables
- `PREREGISTRO.md`, `RESULTADOS.md`, `registro_variantes.csv` y `huellas_r22.json`.
- Scripts:
  - `r22_comun.py` y `r22_motor.py` (congelados);
  - `r22_00_inventario.py`;
  - `r22_01_calibracion.py` y `r22_01b_calibracion_resultados.py`;
  - `r22_02_corrida.py` (congelado);
  - `r22_03_mecanica.py` (post hoc);
  - `r22_04_verificar.py` (sin el motor).
- `salidas/`:
  - inventario: `inventario_m5.csv`, `inventario_m30.csv` e `inventario_log.txt`;
  - calibración: `calibracion.txt/json` y `calibracion_b.txt`;
  - corrida: `h1.txt`, `h1b.txt`, `h2.txt`, `robustez.txt` y `corrida.json`;
  - mecánica y verificación: `mecanica.txt` y `verificacion.txt`;
  - operaciones y ruedas: `ops-r22-*.csv` (4 variantes y 4 cotas), `ruedas_5min.csv` y `ruedas_30min.csv`.
- Intermedios en `scratchpad\r22`: `control_5min.parquet`, `eventos_5min.parquet`, `control_30min.parquet` y `corrida_log.txt`.
