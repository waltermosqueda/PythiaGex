# Ronda 23 — Diseño: el elástico donde paga — RESULTADOS

27-11-2026, 08:09 a 09:10 AR. Etapa de DISEÑO: **todo el material ya estaba visto** por rondas anteriores (R19-R22), así que lo que sigue es
**selección sobre datos vistos**, con una partición interna (diseño → chequeo → cinta) para no elegir a ciegas. El juez de verdad es el vivo
(desde el 30-11, `PREREGISTRO_VIVO.md`) y, si se compra, ES de 2023. Nada de esto promete rentabilidad.

## Veredicto en cuatro líneas
1. **Quedó una sola candidata y está congelada: "el elástico de la tarde después del giro" (A3).** De 13:00 a 15:30, cuando el precio está
   lejos del promedio del día (el VWAP) pero ya empezó a volver desde su máximo o su mínimo, apostar a que llega al VWAP acertó **74 %, 68 % y
   74 %** en las tres muestras y dejó **+2,5, +0,7 y +3,2 puntos por operación** después de pagar 1 punto de costo, con **~1 operación por rueda**.
2. **Un precio que se moviera al azar, desde el mismo lugar, acertaría 63 % en las tres.** La regla le gana por 11, 5 y 12 puntos. Pero en la
   mitad de chequeo (marzo-agosto 2026) la ventaja fue chica y no alcanza para descartar la suerte: es una candidata, no una regla probada.
3. **Corrección a la R22:** el "elástico de 7-10 puntos" estaba mal medido. Contra un azar bien hecho (que cierra a las 16:00 y con el VWAP que
   se mueve), en el día entero el precio vuelve al VWAP **igual** que el azar (47,0 contra 47,1 %). Lo que sí hay es un efecto de horario.
4. **Lo que les faltó a las otras:** A1 (la misma idea, más exigente) quedó en neto 0,0 en el chequeo; A2 (stop más ancho) perdió en el
   chequeo y en la cinta; las reglas de "seguir la ruptura" (al mediodía y en la tarde de un día de tendencia) no pagan el costo.

## Para el operador, en una idea
Pensá en una pelota atada con un elástico a una estaca, y la estaca es el VWAP. Lo que medimos dice que **a la mañana el elástico casi no
tira**, y que a la tarde tira un poco más. Pero lo que más pesa es otra cosa: **que la pelota ya haya empezado a volver**. Si a la tarde el
precio llegó lejos, se dio vuelta y ya recorrió más o menos la mitad del camino de regreso, lo más común es que termine de volver a la
estaca. Ojo con la trampa de siempre: acá se gana seguido porque el stop está lejos (detrás del extremo del día, ~23 pts) y el objetivo cerca
(el VWAP, ~10 pts). Eso solo no sirve: lo que importa es que gana **más seguido que un precio al azar con esa misma ubicación**. Y pierde en
los días de tendencia, cuando la estaca se arrastra detrás del precio y la pelota nunca vuelve.

## 1. Qué se hizo
- **Datos (solo lectura):** `velas-MES-m5.csv` (ruedas completas de 9:30 a 16:00 NY), partido por fecha ANTES de mirar resultados:
  **DISEÑO** 116 ruedas del 09-09-2025 al 27-02-2026 y **CHEQUEO** 126 ruedas del 02-03 al 31-08-2026 (son las 242 de la R22). Después, la
  **CINTA de ES** orden por orden: 55 ruedas del 10-09 al 25-11-2026 (los CSV crudos de la sonda; historia de rangos de la R19).
- **Puntos de decisión:** el cierre de cada vela de 5 min (la entrada es siempre al cierre de la vela, como exigió la calibración de la R22).
  Rasgos causales por punto en `r23_01_base5.py` (VWAP con (máx + mín)/2, extremos del día, X = 0,08 × mediana de 10, D1, extensión del
  balance inicial, toques del VWAP, pendiente, cociente de varianzas de hoy y de las 10 previas, etc.).
- **Simulador de brackets con su referencia al azar** (`r23_sim.py`): stop y objetivo fijos o pegados al VWAP (la orden se corre en cada vela),
  regla conservadora (stop y objetivo en la misma vela = stop), cierre a las 16:00. **La referencia al azar con la misma ubicación** son 200
  caminos en los que cada vela posterior se refleja al azar alrededor del cierre previo: mismo tamaño y forma de velas, mismo volumen, mismo
  VWAP que se mueve, mismo cierre y misma regla conservadora. Ahí la esperanza bruta de cualquier bracket es ~0 (un poco negativa por la regla
  conservadora). Además se informa la fórmula geométrica P = d_stop / (d_stop + d_objetivo).
- **Registro:** `registro_variantes.csv` (1.046 filas): 272 variantes distintas en diseño (grilla de 72 + vecindario de 216, corridas dos veces:
  la segunda con todas las distancias en ticks enteros), ~465 casilleros de mapa mirados, 3 en el chequeo, 3 en la cinta (corrida tres veces:
  las dos últimas para arreglar una advertencia de zona horaria y el redondeo de la hora de salida, con resultados idénticos), 24 ablaciones y
  432 filas del vecindario mirado después en el chequeo y en la cinta.

## 2. La referencia al azar y la corrección a la R22 (`salidas/correccion_r22.txt`)
La R22 preguntó, en cada cierre de vela de 10:00 a 15:30: ¿qué llega antes, el extremo del día + X/2 o el VWAP? Y comparó contra la fórmula
de dos metas fijas. Esa fórmula ignora que la rueda se corta a las 16:00 (11 % de los puntos no llegan a ninguna meta) y que el VWAP se
mueve hacia el precio. Con la referencia bien hecha:
| Muestra | Extremo primero: real / azar / fórmula | VWAP primero: real / azar |
|---|---|---|
| Año de MES (las 242 de la R22) | 41,9 / 42,0 / 48,8 % | 47,0 / 47,1 % |
| Diseño | 42,4 / 43,5 / 50,1 % | 46,3 / 45,7 % |
| Chequeo | 41,3 / 40,5 / 47,7 % | 47,7 / 48,3 % |
| Cinta de ES (velas de la cinta) | 38,3 / 40,5 / 47,2 % | 48,5 / 48,6 % |
**El elástico de todo el día no existe: era la vara.** Queda un efecto de horario chico: de 10:00 a 11:30 el precio llega al extremo un poco
más que el azar (+2,6 puntos en el año) y de 13:05 a 15:30 vuelve al VWAP un poco más (+2,4 en el año; diseño +4,7, chequeo 0, cinta −1,1).

## 3. Diseño (MES 5 min, 116 ruedas)
- **Mapa** (`salidas/mapa_diseno.txt`): desvanecer hacia el VWAP con el stop detrás del extremo tiene borde contra el azar **negativo de 11:00
  a 12:30** (−3,9 puntos de acierto; sobre todo en sep-nov) y **positivo después de las 14:00** (+9,4; en los dos sub-períodos del diseño). La extensión del balance
  inicial (IB) más allá de 1 vez su ancho dio vuelta el signo (con 2 veces o más: −9 pts por punto).
- **Grilla** (72 variantes, tres familias pensadas desde la teoría de subasta): **A, elástico de la tarde** (desvanecer hacia el VWAP después de
  las 13:00/14:00, con y sin la llave del IB); **B, puerta de la tarde** (seguir la extensión en días con el IB extendido más de 1 vez);
  **C, puerta de media mañana** (seguir la extensión cerca del extremo de 11:00 a 12:25). Solo la A pasa la vara (22 de 36); B y C pierden
  todas con el costo (B: −0,2 a −3,3 pts por operación; C: −0,06 a −2,5).
- **Vecindario** de la A (216 variantes: hora de inicio, distancia, llave, stop): la geometría "después del giro" (stop natural detrás del
  extremo y P geométrica ≥ 0,5-0,6) es una meseta (98 de 108 pasan en los dos sub-períodos); la geometría "estirada" (stop ≥ 1,25-2 veces la
  distancia al VWAP) pasa en el total pero solo 39 de 108 en los dos sub-períodos.
- **Elección para el chequeo, antes de abrirlo** (`elegidas_para_chequeo.json`, 08:23:15): el centro de cada meseta — **A1** (13:30-15:30, ≥ 2 X,
  IB ≤ 1, P ≥ 0,55), **A2** (14:00-15:30, ≥ 2 X, IB ≤ 1,5, stop ≥ 1,5 veces la distancia) y **A3** (13:00-15:30, ≥ 1,5 X, IB ≤ 1, P ≥ 0,55).
- **Corrección por la búsqueda:** con 272 variantes, el t de Bonferroni (una cola, gl 115) es 3,67; el mejor t por rueda del diseño fue 2,71.
  **Nada del diseño es significativo por sí solo**; por eso el chequeo y la cinta.

## 4. Resultados de las tres elegidas (costo 1,00 pt por operación; azar = referencia con la misma ubicación)
| | Diseño (116 r) | Chequeo (126 r) | Cinta de ES (55 r, orden por orden) |
|---|---|---|---|
| **A3** ops (por rueda) | 109 (0,94) | 123 (0,98) | 62 (1,13; 36 ruedas con operación) |
| acierto / azar / fórmula | **74,3** / 62,9 / 67,6 % | **67,5** / 62,8 / 65,4 % | **74,2** / 62,5 / 66,6 % |
| neto por op. (t por rueda) | **+2,49** (2,30) | **+0,66** (0,52) | **+3,17** (2,17) |
| neto del azar en los mismos puntos | −1,22 | −0,89 | −0,96 |
| por sub-período | sep-nov 71,8 % +2,71; dic-feb 75,7 % +2,36 | mar-may 67,7 % +1,01; jun-ago 67,2 % +0,31 | R21: 70,8 % +0,44 / 72,7 % +3,04 / 100 % +17,15 |
| **A1** | 65 ops (0,56) 75,4 % (azar 60,7) +3,49 | 56 ops 62,5 % (61,2) **−0,01** | 33 ops 66,7 % (57,9) +3,82 |
| **A2** | 123 ops (1,06) 67,5 % (56,3) +2,65 | 122 ops 54,9 % (54,7) **−1,54** | 52 ops 59,6 % (55,5) **−0,45** |
- **Borde contra el azar en los puntos que operó A3** (pts por operación, error agrupado por rueda): diseño **+3,71 (t 3,9)**, chequeo **+1,55
  (t 1,2)**, cinta **+4,46 (t 3,1)**; en acierto +11,5 / +4,7 / +12,2 puntos.
- **A3 en la cinta:** 43 salidas en el VWAP, 13 al cierre de las 16:00 y 6 stops; stop medio 23,1 pts, objetivo medio 10,5 pts, X medio 5,0. Las
  mismas entradas resueltas con velas de 5 min dan 74,2 % y +3,20: a esta escala la vela de 5 min no engaña (las operaciones son lentas).
- **Vara del pedido (acierto ≥ 60 % y neto > 0 con costo 1,00, ≤ 5 por rueda, en las dos mitades y en la cinta): solo A3 la cumple.**
  A1 queda a 0,01 pts en el chequeo (acierto 62,5 %, apenas sobre su azar de 61,2 %). A2 no tiene borde afuera del diseño.
- **Lo que no es:** A3 no captura "piernas de 20-50 pts": su recorrido típico es de 10 pts (lo que hay hasta el VWAP), con un stop de ~22.

## 5. La candidata congelada
**A3 — `cand_A3_tarde_giro_amplia.py`** (SHA-256 7e4e8a0e…40c5; `huellas_r23.json`, 09:01:30). Script autocontenido (numpy y pandas) que lee
una cinta de ES en el formato de la sonda (t, primero, último, vol, lado) y la historia de rangos, y devuelve las operaciones del día:
- X = 0,08 × mediana del rango de la rueda de las 10 jornadas previas (al tick).
- Se mira en los cierres de vela de 13:00 a 15:30. Condiciones: precio a ≥ 1,5 X del VWAP de la rueda; del lado del precio, el IB extendido
  ≤ 1 vez su ancho; y P geométrica = d_stop / (d_stop + d_VWAP) ≥ 0,55 (el precio ya volvió desde el extremo).
- Hacia el VWAP al cierre de la vela; stop X/2 más allá del extremo del día; objetivo en el VWAP (orden limitada que se corre cada 5 min);
  lo abierto se cierra a las 16:00; una posición por vez, máximo 5 por rueda.
**En una línea para la pantalla:** "Después de la 1 de la tarde, si el precio se alejó del VWAP y ya volvió la mitad desde el máximo (o el
mínimo) del día, y el día no rompió el balance inicial más de una vez de ese lado: ir hacia el VWAP, stop detrás del extremo, salir en el VWAP
o a las 16:00".

## 6. La mecánica: qué pesa, dónde y por qué (`salidas/posthoc.txt`, `salidas/mecanica_a3.txt`; post hoc, descriptivo)
**6.1 Qué pieza pesa (ablaciones de A3; diseño | chequeo | cinta, neto por operación):**
- **La geometría "después del giro" es la que da el acierto.** Sin la condición P ≥ 0,55 (stop natural pero entrando cerca del extremo) el
  acierto cae a 46 / 40 / 53 %: sin ella el 60 % es imposible, y el borde contra el azar se achica.
- **La hora es la que da el borde.** La misma regla en todo el día da −0,01 / −1,13 / +1,17; solo a la mañana, −0,84 / −2,23 / +0,70. Dentro
  de la tarde, la franja de 13:00 a 13:30 es la más floja (chequeo −2,51); de 14:00 a 15:30 es positiva en las tres muestras (+1,8 a +4,2 por operación según franja y muestra).
- **La distancia es la que paga el costo.** Sin el corte de 1,5 X el acierto sube a 77-85 % pero el objetivo es tan chico que el neto se va a
  ~0 (+0,53 / −0,39 / +0,24). Pedir más distancia (≥ 3 X) funcionó en diseño (+6,23) y se cayó afuera (−2,69 / −1,73).
- **La llave del IB ayudó en diseño y afuera no se sostuvo:** sin la llave, +0,95 / +1,07 / +2,99 (contra +2,49 / +0,66 / +3,41 con la llave);
  solo con días de IB extendido más de 1 vez: −5,56 / +3,07 / −0,79, con 25 / 27 / 7 operaciones. Es la llave de "cuándo se apaga el elástico"
  medida en vivo dentro de la rueda (Dalton: un día que extiende el balance inicial de un solo lado es un día de iniciativa), pero con esta
  muestra no se puede afirmar que sume.
**6.2 Dónde pierde (ex post, para entender, no se puede usar en vivo):** en los días que terminan en **tendencia** (|cierre − apertura| ≥ 0,6
del rango) acierta 55,8 % y deja −2,84; en los de **balance**, 77,5 % y +3,24; en los mixtos, 71,2 % y +2,40. Es exactamente lo que dice la
teoría de subasta: en balance el precio vuelve al valor; en desequilibrio el valor se va con el precio. **La pieza que falta** es reconocer
antes de las 13:00 el día de tendencia; la extensión del IB lo hace solo en parte.
**6.3 Por qué (teoría; las hipótesis no están medidas con esta muestra):** (i) **subasta de Dalton**: la tarde es cuando el mercado "prueba"
si la excursión de la mañana o del mediodía consiguió aceptación; si el precio ya se dio vuelta desde el extremo (exceso), los que responden
tienen el control y lo llevan de vuelta al valor del día; (ii) **inventario y cobertura de cierre**: los que operan en el día achican posición
hacia las 16:00 y, cuando los creadores de mercado están comprados en gamma (lo más común en 2025-26, R20), cubren vendiendo en las subas y
comprando en las bajas, cada vez más fuerte a medida que se acerca el vencimiento del 0DTE; (iii) la última media hora revierte (baúl #24 y
#57). El 18 % de las operaciones de A3 sale al cierre de las 16:00 (en promedio −4,6 pts): parte del borde es esa deriva hacia el valor.
**6.4 Lo que se descartó y por qué:**
- **A2 ("estirado", stop ancho):** el borde no está en lo estirado sino en el giro. Desvanecer cerca del extremo con un stop de 1,5 veces la
  distancia ganó en diseño y quedó igual al azar afuera (54,9 contra 54,7 % en el chequeo).
- **A1:** es A3 más exigente (más distancia y desde las 13:30); con menos operaciones, el chequeo la dejó en cero (+1,3 puntos sobre el azar).
- **B (puerta de la tarde en días de tendencia):** el acierto es el del azar y el objetivo chico no paga el costo (−1,6 a −3,3 por operación).
- **C (puerta de media mañana):** la mañana sí empuja hacia el extremo (+4,6 puntos sobre el azar de 11:00 a 12:30 en diseño, en el mismo
  sentido que la franja de 11:00-12:25 de la R22), pero con un objetivo de ~5 pts el costo de 1 pt se come todo (−0,06 a −0,24).
- **El vecindario, mirado después en las tres muestras:** de la geometría "después del giro", 31 de 108 variantes pasan en las tres; las que
  se sostienen afuera son las de distancia 1,5 X (chequeo: 78 % de ellas pasan; cinta: todas); las de 2,5-3 X se caen. A3 está en esa meseta.

## 7. Comprobación desde los datos crudos (`r23_07_verificar.py` → `salidas/verificacion.txt`)
Código propio, sin el motor ni los scripts de las candidatas (módulo `csv`, `datetime`/`zoneinfo`, bucles de Python): rehace X, VWAP, extremos,
IB, la condición de la regla y la salida orden por orden. **Cinta: 11 de 11 coinciden** (7 de A3); **velas de MES: 12 de 12** (4 de A3).
Operaciones de A3 (hora NY de entrada):
1. **10-09-2026 13:00 VENTA a 7.611,50** (VWAP 7.603,76; 2,06 X; P 0,576): objetivo **+7,25** a las 13:30:06.
2. **22-09-2026 14:25 VENTA a 7.839,25** (X 3,50; 1,65 X; P 0,656): objetivo **+5,50** a las 14:57:58.
3. **01-10-2026 13:20 VENTA a 7.710,50** (X 4,75; 2,60 X; P 0,722): no llegó; cierre a las 15:59:59 en 7.726,00, **−15,50**.
4. **22-10-2026 15:20 COMPRA a 7.691,00** (IB extendido 0,42; P 0,580): objetivo **+11,25** a las 15:51:14.
5. **29-10-2026 14:15 COMPRA a 7.736,75**: objetivo **+9,25** a las 15:30:34.
6. **10-11-2026 14:10 VENTA a 7.533,50** (P 0,790): objetivo **+9,50** a las 14:14:35.
7. **24-11-2026 13:10 VENTA a 7.214,75** (X 9,75; VWAP 7.193,34): objetivo **+18,25** a las 14:52:06.
8. MES diseño **10-09-2025 13:20 COMPRA a 6.544,75**: stop **−8,50** en la vela de 13:50; **27-02-2026 14:50 COMPRA a 6.857,50**: **+10,50**.
9. MES chequeo **02-03-2026 13:55 VENTA a 6.881,50**: stop **−20,75** en la vela de 14:15; **31-08-2026 15:25 VENTA a 7.691,75**: **−14,25**.

## 8. El juez en vivo (armado; empieza el lunes 30-11-2026)
- **Cómo se corre:** después de bajar la cinta del día con la sonda, `python -I r23_vivo.py` en esta carpeta. Procesa todas las jornadas
  nuevas en orden, anota cada operación en `bitacora_r23_vivo.csv` (con la referencia al azar y la P geométrica) y la jornada en
  `bitacora_r23_jornadas.csv`, y muestra el estado del juez. `--resumen` solo muestra el estado; `--verificar AAAA-MM-DD` recorre una jornada
  conocida sin escribir. Si cambió cualquier archivo con huella, no corre.
- **Vara (PREREGISTRO_VIVO.md, 09:03):** lecturas decisivas a las **60 y 120 jornadas evaluadas**: acierto ≥ 60 %, neto > 0 con costo 1,00,
  t por jornada ≥ 2,00 / 1,98 (α 0,025 = 0,05 / (1 candidata × 2 lecturas)) y ≥ 40 operaciones. Futilidad a las 20 y 40 jornadas. Lectura
  descriptiva a las 15 (no decide).
- **Potencia honesta:** a las 60 jornadas, ~30 % de pasar el t si el efecto es el del diseño, ~6 % si es el del chequeo, ~60 % si es el de la
  cinta. Lo más probable a las 60 es "sin decidir" aunque la regla funcione.
- **Verificado:** reproduce exacto las operaciones de A3 de cinco jornadas conocidas, una corrida simulada del 16 al 25-11 (5 de 5) y el modo
  histórico sembrado desde las cintas (52 de 52 desde el 22-09). El roll Z6 → H7 no la afecta (solo usa rangos previos y precios del día).
- **ES 2023 (si se compra):** el mismo juez con `--carpeta`, `--estado` y `--sembrar-desde-cintas`, una sola lectura con α 0,05; el convertidor
  tbbo → cinta (una fila por operación) se valida antes con una jornada que tenga las dos fuentes.

## 9. Pistas nuevas al baúl privado (#65-#71)
Corrección del elástico (#65), la candidata A3 (#66), la franja 13:00-13:30 floja (#67), la llave del IB (#68), las puertas sin información
como reglas (#69), el contra-hallazgo del "estirado" (#70) y la lección de método de la referencia al azar (#71).

## 10. Limitaciones y notas de honestidad
1. **Selección sobre datos vistos.** El diseño eligió entre 272 variantes; el chequeo y la cinta eligieron A3 entre tres (y la cinta ya la
   habían mirado la R19-R22 con otras reglas). El t de A3 en el chequeo (0,52) está lejos de cualquier corrección.
2. **El chequeo fue flojo:** +0,66 pts por operación y +4,7 puntos de acierto sobre el azar (t 1,2); el trimestre abril-junio de 2026 dio
   62,9 % contra 62,2 % del azar (−0,58). Lo bueno está en el diseño y en sep-nov de 2026.
3. **Perfil de pérdidas:** gana en promedio +9,2 y pierde −12,7; la peor operación fue −36,25 pts (5 % de las operaciones pierde más de
   20,8). Con MES eso es poco dinero por contrato, pero la racha mala existe.
4. **MES 5 min:** la entrada es al cierre de la vela y la salida con la regla conservadora; en la cinta, la misma entrada resuelta orden por
   orden dio el mismo resultado (74,2 %). La vela de 5 min del MES y la del ES tienen 0-1 tick de diferencia (R22).
5. **Las mecánicas (gamma de los creadores de mercado, cierre de posiciones) son hipótesis de la teoría**: con esta muestra no hay GEX para
   2025-26 ni flujo firmado para medirlas.
6. Sin cambios después de congelar: las tres corridas de la cinta dieron lo mismo; las dos correcciones (ticks enteros en el diseño, hora de
   salida truncada al segundo) fueron de plomería y están anotadas en el registro (etapas `_c2` y `_c3`).

## 11. Entregables
- `RESULTADOS.md`, `PREREGISTRO_VIVO.md`, `registro_variantes.csv`, `huellas_r23.json`, `elegidas_para_chequeo.json`, `historia_rangos_semilla.json`.
- **Congelados:** `cand_A3_tarde_giro_amplia.py` (la candidata) y `r23_vivo.py` (el juez). No congelados (descartados): `cand_A1_tarde_giro.py`,
  `cand_A2_tarde_estirado.py`; plantilla: `plantilla_candidata.py`.
- Scripts del diseño: `r23_comun.py`, `r23_sim.py`, `r23_01_base5.py`, `r23_02_mapa.py`, `r23_03_variantes.py`, `r23_04_cinta.py`,
  `r23_05_posthoc.py` y `r23_06_correccion_r22.py` (post hoc), `r23_07_verificar.py` (sin el motor), `r23_08_mecanica_a3.py` (post hoc).
- `salidas/`: `mapa_diseno.txt`, `variantes_diseno*.txt/csv`, `variantes_chequeo.txt/csv`, `cinta.txt/json`, `cinta_jornadas.csv`,
  `ops-cinta-A1/A2/A3.csv`, `historia_rangos_cinta.json`, `posthoc.txt`, `posthoc_vecindario_3muestras.csv`, `correccion_r22.txt`,
  `mecanica_a3.txt`, `verificacion.txt`.
- Intermedios en `scratchpad\r23` (`puntos5.parquet`, `ruedas5.pkl`, `sim_*`, `post_*`, `ops_*`, `puntos5_cinta.parquet`, `ruedas5_cinta.pkl`).
