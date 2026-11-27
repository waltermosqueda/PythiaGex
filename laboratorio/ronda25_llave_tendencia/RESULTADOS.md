# Ronda 25 — La llave del día de tendencia — RESULTADOS

27-11-2026, 09:28 a 10:10 AR. Pre-registro con hora en `PREREGISTRO_R25.md` (09:46; enmienda 1 a las 09:54, después de la exploración a
mano y antes de tocar el diseño). Elección única a las 09:56 (`elegido_r25.json`). **Toda la data de esta ronda ya había sido vista por
alguna ronda anterior** (R19-R24); lo único limpio es el vivo desde el 30-11-2026 y, si se compra, ES de 2023. Nada de esto promete rentabilidad.

## Veredicto en cuatro líneas
1. **No hay llave en la mañana.** Ninguno de los seis detectores sacados de la teoría (apertura en un extremo, una sola dirección, VWAP
   sin tocar, aceptación fuera del rango de ayer, rango de la mañana, día de la Fed) anticipa si la tarde se sigue alejando del VWAP: AUC
   0,46-0,52 en 2024-25 (337 ruedas; ninguno pasa su α de 0,0083) y 0,44-0,52 en 2025-26 (242). **No se congeló nada.**
2. **Como filtro de A3 ninguno mejora parejo:** ayudan en una muestra y empeoran en otra. En las tres muestras de A3 juntas, todos bajan
   o empatan el neto por rueda (A3 +1,85; con filtro +0,87 a +1,88), y ninguno le gana al placebo de saltear la misma cantidad de días al azar.
3. **Corrección a las rondas 23 y 24:** "A3 pierde en los días de tendencia" es, en su mayor parte, la vara. Con tardes al azar, sin
   ninguna memoria, A3 también "pierde en los días de tendencia" (19 de los 22 puntos de brecha). Lo que daña de verdad es la **tarde de un
   solo sentido**, y la mitad de esas tardes cae en días que terminan en balance.
4. **En octubre-noviembre de 2026 la mañana sí anticipó la tarde** (una sola dirección: AUC 0,78, p 0,005 contra el azar). Pero es un
   interruptor de régimen (por trimestre, 0,41-0,62 en los dos años anteriores) y, aun ahí, A3 ganó en esos días.

## Para el operador, en una idea
Decir "A3 pierde en los días de tendencia" se parece a decir "el equipo pierde los partidos que termina abajo en el marcador". El día
se llama "de tendencia" **mirando cómo terminó**: si la tarde volvió al VWAP, el día no termina lejos de la apertura y nadie lo llama
tendencia. Pero que la tarde vuelva al VWAP es justo lo que hace ganar a A3. Así que las dos cosas van juntas aunque el mercado no tenga
ninguna memoria: lo medimos haciendo tardes al azar, y la brecha aparece igual.

La pregunta útil era otra: **¿se puede saber a la 1 de la tarde si la tarde va a ir para un solo lado?** Miramos seis señales de los
libros de perfil de mercado (si abrió en un extremo, si cada media hora fue más arriba que la anterior, si el precio no tocó el VWAP, si
abrió fuera del rango de ayer y no volvió, si la mañana ya fue ancha, si hay Fed a las 14:00). En dos años, ninguna lo sabe: **la tarde
es una subasta nueva**. La mañana te dice que el día "ya fue de tendencia", no si lo va a seguir siendo. Lo único que sí avisa algo es una
mañana ancha: avisa que la tarde se va a mover mucho, pero no para qué lado.

En la práctica: **A3 queda como está** (su juez en vivo arranca el 30-11 sin cambios) y no se le agrega ningún filtro de la mañana. Si algún
día se la quiere proteger, la información está **después** de la 1 de la tarde, y eso es otra ronda, con su propio pre-registro.

## 1. Qué se hizo
- **Teoría primero** (`PREREGISTRO_R25.md` sección 3): seis detectores con su mecánica y un parámetro fijado por teoría (grilla de 2-3
  valores solo para robustez): **APE** apertura en un extremo del rango de la mañana (open-drive / open-test-drive), q ≤ 0,15;
  **OTF** una sola dirección (one-timeframing), ≥ 5 de 6 medias horas sin perforar la anterior; **VWA** el VWAP quedó atrás, ≥ 5 de 6
  medias horas sin tocarlo; **ACE** abre fuera del rango de ayer y no vuelve a entrar; **EXT** rango de 9:30-13:00 ≥ 1 mediana de 10 (la
  llave vieja D1, como control); **CAL** día del comunicado del FOMC (fechas de federalreserve.gov; variante con CPI y empleo de bls.gov).
  Todo con velas de 30 min hasta las 13:00, la noche, el día anterior y el calendario.
- **Objetivo independiente de A3 y libre de geometría:** **TA**, "la tarde se aleja del VWAP" (s13 × (C16 − C13) > 0). Con tardes al azar
  vale 50 % para cualquier detector armado con el pasado (comprobado: 0,47-0,51 en todas las muestras), así que no tiene la trampa de la
  R22. Secundarios, informados con su AUC del azar: día de tendencia ex post (TIPO), la tarde no toca el VWAP (NV) y, desde la enmienda 1,
  la tarde de tendencia en cualquier dirección (ET).
- **Partición declarada antes de mirar:** exploración a mano en las 15 primeras ruedas de la cinta (10 al 30-09-2026); **diseño** en MES
  30 min nativas, 337 ruedas del 29-04-2024 al 08-09-2025; **validación 1** en MES 5 min agregadas a 30, 242 ruedas 2025-26; **validación 2**
  en la cinta agregada a 30, 40 ruedas de oct-nov 2026; A3F descriptivo en las cuatro muestras de A3.
- **Registro:** `registro_variantes.csv`, 890 filas (6 detectores × 4 objetivos × 2 subconjuntos con puntaje continuo, más 16 marcas × 4
  objetivos × 2 subconjuntos, en 5 corridas, más 10 filas de A3F). La prueba principal son 6 AUC: **α = 0,05 / 6 = 0,0083** de una cola.

## 2. La exploración a mano (15 ruedas, `salidas/exploracion_a_mano.txt`)
- Las 3 ruedas que terminaron en tendencia (15, 21 y 23-09) **ya eran tendencia a las 13:00**: apertura en el extremo, 6 de 6 medias horas
  en una dirección, mañana 1,0-1,6 veces lo normal. A3 no operó el 21-09 (la frenó la llave del IB), ganó el 15-09 (+8,5) y perdió poco el
  23-09 (−5,5).
- Las tres pérdidas grandes de A3 (18-09 −18,5; 25-09 −17,75; 30-09 −13,5) fueron en ruedas de **balance o mixtas** en las que **la tarde
  arrancó un movimiento nuevo**: el 18-09 desde abajo del VWAP, cruzándolo y siguiendo; el 25-09 y el 30-09 con el empujón de 15:00-16:00.
- La apertura en un extremo marcó 7 de 15 ruedas: el punto de partida de un paseo al azar suele quedar cerca de un extremo de su rango
  (ley del arcoseno). Por eso se agregó ET como objetivo secundario (enmienda 1). No se cambió ningún detector.

## 3. La clasificación
**AUC contra TA, la tarde se aleja del VWAP** (todas las ruedas; ~0,50 = no sabe nada; entre paréntesis, p de una cola):
| Detector | Diseño 2024-25 (337) | Validación 1 2025-26 (242) | Validación 2 oct-nov 2026 (40) |
|---|---|---|---|
| APE apertura en un extremo | 0,496 (0,56) | 0,501 (0,50) | 0,535 (0,36) |
| **OTF una sola dirección** | **0,523 (0,22)** | **0,497 (0,53)** | **0,783 (0,000)** |
| VWA VWAP sin tocar | 0,511 (0,35) | 0,435 (0,96) | 0,686 (0,02) |
| ACE fuera del rango de ayer | 0,511 (0,31) | 0,520 (0,19) | 0,437 (0,97) |
| EXT mañana ancha (control) | 0,459 (0,91) | 0,478 (0,75) | 0,664 (0,04) |
| CAL día de la Fed | 0,499 (0,68) | 0,508 (0,36) | 0,528 (0,44) |
Bases de TA: 56,4 / 50,4 / 45,0 %. En la "tarde con tensión" (|C13 − VWAP| ≥ 1 X) da lo mismo: 0,48-0,53 en el diseño.

**El día de tendencia ex post sí se "reconoce", pero es geometría** (AUC real / AUC con tardes al azar):
| Detector | Diseño | Validación 1 | Validación 2 |
|---|---|---|---|
| APE | 0,737 / 0,716 | 0,791 / 0,780 | 0,840 / 0,785 |
| OTF | 0,608 / 0,594 | 0,635 / 0,632 | 0,721 / 0,620 |
| VWA | 0,682 / 0,630 | 0,675 / 0,687 | 0,895 / 0,703 |
| EXT | 0,573 / 0,561 | 0,512 / 0,574 | 0,824 / 0,630 |
La diferencia ("memoria") va de −0,06 a +0,05 en los dos años y solo crece en oct-nov 2026. **Precisión / recall de la marca de teoría
para el día de tendencia** (base 35,6 / 33,5 / 20,0 %): APE 61 % / 50 %, 64 % / 62 %, 43 % / 75 %; OTF 47 / 45, 46 / 57, 39 / 88;
VWA 53 / 44, 51 / 44, 64 / 88; ACE 50 / 14, 48 / 14, 0 / 0; EXT 44 / 36, 37 / 37, 32 / 75; FOMC 18 / 2, 50 / 5, 0 / 0.
**Precisión de la marca para TA** (base 56,4 / 50,4 / 45,0): APE 54 / 54 / 50; OTF 56 / 49 / 72; VWA 59 / 46 / 82; ACE 62 / 61 / 20;
EXT 52 / 48 / 58; FOMC 55 / 63 / 100 (con 11 / 8 / 1 días).
**Regla de elección:** ninguno pasa; el mejor sin aprobar es OTF (0,523, p 0,22), que en la validación 1 da 0,497. F1 falla.

## 4. A3F contra A3 (descriptivo; costo 1,00; azar = los 200 caminos reflejados con la misma ubicación de cada operación)
| Muestra | Versión | Ops (/rueda) | Acierto (azar) | Neto/op | Neto/rueda | Ventaja (t) |
|---|---|---|---|---|---|---|
| Diseño A3, MES 5 min, 116 r | A3 | 109 (0,94) | 74,3 (62,9) | +2,49 | +2,34 | +3,71 (3,86) |
| | A3F-día OTF | 73 (0,63) | 72,6 (64,3) | +2,33 | +1,47 | +3,61 (2,90) |
| | A3F-dir OTF | 87 (0,75) | 73,6 (64,6) | +2,29 | +1,72 | +3,51 (3,08) |
| Chequeo A3, MES 5 min, 126 r | A3 | 123 (0,98) | 67,5 (62,8) | +0,66 | +0,65 | +1,55 (1,24) |
| | A3F-día OTF | 79 (0,63) | 70,9 (62,2) | +1,59 | +1,00 | +2,49 (1,79) |
| | A3F-dir OTF | 94 (0,75) | 71,3 (63,2) | +2,05 | +1,53 | +2,91 (2,22) |
| Cinta ES orden x orden, 55 r | A3 | 62 (1,13) | 74,2 (62,5) | +3,17 | +3,57 | +4,13 (3,09) |
| | A3F-día OTF | 45 (0,82) | 73,3 (62,9) | +3,07 | +2,51 | +3,84 (2,30) |
| | A3F-dir OTF | 54 (0,98) | 74,1 (62,5) | +3,11 | +3,05 | +3,98 (2,87) |
| A3-30 MES 2024-25, 337 r | A3 | 157 (0,47) | 59,9 (61,3) | −0,74 | −0,35 | +0,60 (0,44) |
| | A3F-día OTF | 112 (0,33) | 59,8 (62,8) | −0,42 | −0,14 | +0,67 (0,39) |
| | A3F-dir OTF | 122 (0,36) | 59,0 (62,5) | −0,34 | −0,12 | +0,75 (0,46) |
- **Los otros cinco (A3F-día, neto por operación, diseño / chequeo / cinta / 2024-25; A3 = +2,49 / +0,66 / +3,17 / −0,74):** APE +3,16 /
  +0,61 / +3,69 / −1,67; VWA +2,09 / +0,40 / +3,49 / +0,24; ACE +2,71 / +0,37 / +3,18 / −0,40; EXT +0,74 / +1,10 / +3,72 / −2,09;
  FOMC +2,56 / +0,37 / +2,80 / −0,86. Neto por rueda, las tres muestras de A3 juntas: A3 +1,85; filtros entre +0,87 (EXT) y +1,88 (OTF-dir).
- **Placebo** (saltear al azar la misma cantidad de ruedas): en las tres muestras de A3, p de 0,18 a 0,98 (OTF-día 0,60 / 0,18 / 0,55).
  De las 42 celdas (6 detectores × 7 muestras y variantes), una sola baja de 0,05: OTF-día en el chequeo con cierres :00/:30 (0,049). Es lo
  que se espera por azar.
- **Robustez (solo cierres :00/:30, donde A3 es frágil):** A3 da +2,11 / −1,80 / +3,76 (diseño / chequeo / cinta). OTF-día da +0,97 /
  +0,74 / +3,96: arregla el chequeo y rompe el diseño. Ningún filtro mejora los dos.
- **Cinta, solo las 40 ruedas de validación** (`salidas/a3f_cinta_40_validacion.txt`): A3 42 ops, 76,2 %, +4,44 por op y +4,66 por rueda.
  Con cualquier filtro, el neto por rueda baja (OTF-día +3,68). Incluso el oráculo de TA da +4,16.
- **El techo (oráculos ex post, imposibles en vivo):** si se supiera qué tardes se alejan del VWAP, A3 daría +3,26 / +4,52 / +3,99 / +4,31.
  Si se supiera qué días terminan en tendencia: +2,69 / +2,78 / +3,33 / +0,79. La información que separa los días buenos de los malos
  existe, pero la mañana no la tiene.

## 5. La mecánica: qué pesa, dónde y por qué (`salidas/mecanica.txt`, post hoc)
**5.1 El día de tendencia se hace a la mañana.** De los días que terminaron en tendencia, el 51 % (2024-25), el 62 % (2025-26) y el 75 %
(oct-nov 2026) ya tenían hechos dos tercios o más del movimiento a las 13:00. Solo el 9-13 % se hizo de tarde (0 de 8 en la cinta). Cuando A3 empieza a mirar,
el "día de tendencia" casi siempre es historia. Por eso los detectores lo reconocen tan bien: lo están leyendo, no anticipando.

**5.2 La etiqueta es circular.** Cada operación de A3 se resolvió en 200 tardes al azar (misma entrada, stop y objetivo, VWAP que se
recalcula, cierre a las 16:00; simulador propio que da el mismo azar que la R23: 63,1 contra 62,9 %), y a cada tarde se le puso su etiqueta.
| A3 (diseño + chequeo + cinta) | Tendencia | Mixto | Balance |
|---|---|---|---|
| Real: ops, acierto, neto | 52, 55,8 %, −2,83 | 104, 71,2 %, +2,43 | 139, 77,7 %, +3,33 |
| Azar: % de tardes, acierto, neto | 23 %, 48,3 %, −4,60 | 36 %, 67,0 %, −0,20 | 41 %, 67,1 %, +0,18 |
La brecha entre balance y tendencia es de 22 puntos en el mercado real y de 19 con tardes sin memoria. En A3-30 2024-25, los días de
tendencia dan 46,8 % y −4,32 en lo real, contra 47,9 % y −4,66 en el azar: idénticos. **La ventaja real sobre el azar es positiva en los
tres tipos de día** (+7,5 / +4,2 / +10,6 puntos). El problema de A3 en el chequeo y en 2024-25 no son "los días de tendencia".

**5.3 Lo que sí la daña: la tarde de un solo sentido.**
- Las tardes de tendencia (de 13:00 a 16:00 eficiencia ≥ 0,6 y ≥ 2 X) son el 24 % de las operaciones de A3 y el 43 % de lo que pierde:
  55,7 % y −2,02, con 22,9 % de stops. En el resto de las tardes: 76,3 % y +3,08.
- De esas 70 operaciones, **35 fueron en días que terminaron en balance**, 19 en mixtos y solo 16 en días de tendencia.
- Mirado por TA: si la tarde se aleja del VWAP de las 13:00, 59,2 % y −2,17 (98 operaciones); si vuelve, 77,6 % y +3,89 (196).
- El 72 % de lo que pierde A3 son stops: gana +9,1 en promedio y pierde −12,7 (−18 en el stop). Para empatar necesita ~67 % de acierto,
  y el azar con su ubicación da 63 %. Vive de 5-11 puntos de memoria: los retrocesos de la tarde hasta el VWAP.

**5.4 El arrastre del VWAP: por qué la estaca no alcanza al precio.**
- A las 13:00 ya pasó el 61-67 % del volumen del día. El VWAP tiene esa inercia: lo nuevo de la tarde lo mueve poco.
- En las tardes que se alejan, el VWAP avanza 0,39-0,66 pts por cada punto que avanza el precio (eso incluye el tirón de la distancia que
  ya había a las 13:00). La distancia entre precio y VWAP **crece** +1,7 X en MES.
- En las que vuelven, se achica 4,3-5,1 X.
- O sea: el objetivo de A3 se mueve hacia el precio, pero más lento de lo que el precio se escapa. Si encima el precio hace un extremo
  nuevo, salta el stop.

**5.5 Por qué la mañana no avisa: la tarde es otra subasta.** Por trimestre, el AUC de "una sola dirección" contra TA va de 0,41 a 0,62
entre 2024T2 y 2026T3 (2025T1 0,62, 2025T2 0,41) y salta a 0,78 en 2026T4. Es el patrón del baúl (#42, #58): las señales de tendencia
se prenden cuando el mercado cambia de marcha. Pero aun en oct-nov 2026, A3 en los días marcados ganó (17 operaciones, 76,5 %, +3,43).
A3 cobra en el primer toque del VWAP que se mueve: no necesita que la tarde entera vuelva, solo un retroceso.

**5.6 Lo único que la mañana anticipa: el tamaño de la tarde.** Una mañana ancha (≥ 1 mediana) trae más tardes de tendencia que el azar.
- AUC 0,60 con memoria +0,078 (p 0,015) en 2024-25; 0,55 (p 0,28) en 2025-26; 0,70 (p 0,04) en la cinta.
- Es volatilidad, no dirección: la mitad de esas tardes va hacia el VWAP, y A3 en esos días gana más (83 % y +4,79 en el diseño).

## 6. Por qué no se congeló nada (criterios de la sección 7, fijados antes)
- **F1 falla:** ningún detector pasó el diseño (mejor p 0,22 contra α 0,0083). OTF no valida en 2025-26 (0,497).
- **F2 se cumple para todos**, pero de arrastre: A3 ya la cumple y los filtros no la rompen.
- **F3 falla para todos:** ninguno mejora el neto por operación en 2 de 3 muestras sin bajar el neto por rueda de las tres juntas. El que
  más se acerca es OTF-dir: +1,88 contra +1,85 por rueda, pero mejora una sola muestra.
- **F4:** en las tres muestras de A3 ningún placebo baja de 0,18; en las 42 celdas, una sola baja de 0,05 (lo esperable por azar).
- **F5 falla para todos:** ninguno mejora a la vez los cierres :00/:30 en el diseño y en el chequeo, y como mucho 3 de 6 mitades.
- No hay `cand_A3F_*.py`, ni `PREREGISTRO_VIVO.md` de esta ronda, ni `r25_vivo.py`. **El juez de A3 (r23_vivo.py) sigue igual**, sin tocar.

## 7. Comprobación desde los datos crudos (`r25_07_verificar.py` → `salidas/verificacion.txt`)
Código propio, sin numpy, pandas, el motor de la R25 ni el de la R23 (módulo `csv`, `zoneinfo`, `math`): rehace las velas de 30 min desde
los CSV crudos, la mediana de 10, X, el VWAP, los seis detectores y los cuatro objetivos.
- **Clasificaciones: 9 de 9 coinciden.** Las ruedas salen por regla: la primera marcada por cada detector y la primera de la Fed en cada fuente.
  MES 30 min: 13-05, 01-05, 09-05 y 15-05-2024. MES 5 min: 11-09, 20-10 y 17-09-2025. Cinta orden por orden: 01-10 y 05-11-2026.
  Ejemplo: el 05-11-2026, apertura en el extremo (q 0,017), una sola dirección 5 de 6, VWAP sin tocar 6 de 6, mañana 4,79 veces lo normal,
  TA 1 (D +8,05 X) y día de tendencia.
- **Operaciones de A3 (velas de 5 min de MES, regla escrita de nuevo): 8 de 8 coinciden**, con la marca de su día. Ejemplos: 10-09-2025
  13:20 COMPRA 6.544,75, stop −8,50 (la mañana marcó 4 de 6: A3F la toma); 02-03-2026 13:55 VENTA 6.881,50, stop −20,75 y 15:10 VENTA
  6.886,00, −1,25 (5 de 6: A3F-día las saltea); 31-08-2026 14:45 COMPRA +5,25 y 15:25 VENTA −14,25 (3 de 6: las toma).
- **El simulador de tardes al azar de 5.2 contra el de la R23:** 63,1 contra 62,9 % (diseño) y 63,0 contra 62,8 % (chequeo). Correlación
  por operación 0,87-0,92; la diferencia es el ruido de 200 caminos.
- **Datos:** X por rueda idéntica a la de la R23 (251 ruedas de MES y 36 de la cinta) y a la de la R24 (128). Velas de 5 min de MES y de la
  cinta idénticas a las de la R23 (diferencia 0). Rango de las 57 ruedas de la cinta igual a la semilla.

## 8. Al baúl privado (#73-#78)
Corrección de la etiqueta circular (#73), las seis llaves de la mañana sin información (#74), el interruptor de régimen de oct-nov 2026 (#75),
la mañana ancha que anticipa el tamaño de la tarde (#76), la mecánica del arrastre del VWAP (#77) y la lección de método (#78).

## 9. Limitaciones y notas de honestidad
1. **Todo ya estaba visto.** La validación 2 son 40 ruedas de un solo régimen (el más agitado de la muestra). El techo de los oráculos usa
   el futuro y no se puede usar en vivo.
2. **TA mira de cierre a cierre** (13:00 contra 16:00): no ve el camino de la tarde. Una tarde que cruza el VWAP y sigue cuenta como "volvió".
   Por eso se agregó ET (enmienda 1, antes del diseño); ET tampoco tiene una llave en la mañana que sirva a A3.
3. **Los detectores usan velas de 30 min en las tres fuentes** (es lo único que hay en 2024-25 y es la escala de Dalton). El VWAP del
   detector es el de esas velas, no el de A3; solo importa cuando el precio está pegado al VWAP a las 13:00.
4. **Calendario:** fechas oficiales bajadas una sola vez (federalreserve.gov y bls.gov). Octubre de 2025 no tuvo CPI ni empleo. En los
   días de la Fed A3 fue positiva en las cuatro muestras (25 operaciones): no hace falta vetarlos.
5. **Sin GEX:** la hipótesis de la R20 (gamma negativa a las 9:59 anticipa un día ancho) queda sin probar; esta ronda solo muestra que la
   mañana ancha anticipa una tarde de más tamaño, no de una dirección.
6. Hubo una sola elección (09:56). La "corrida de repaso" del diseño solo agregó el p de memoria y no tocó `elegido_r25.json`.

## 10. Entregables
- `PREREGISTRO_R25.md` (con la enmienda 1 y la nota de resultado), `RESULTADOS.md`, `registro_variantes.csv`, `elegido_r25.json`,
  `calendario_oficial.json`, `huellas_r25.json` (huellas de lo que se usó; no hay candidata congelada).
- Scripts: `r25_comun.py`, `r25_01_datos.py`, `r25_rasgos.py`, `r25_02_explorar.py`, `r25_03_clasificar.py`, `r25_05_a3f.py`,
  `r25_06_mecanica.py` y `r25_07_verificar.py` (sin el motor).
- `salidas/`: `exploracion_a_mano.txt`, `clasificacion_{explora,diseno,diseno_repaso,val1,val2}.txt`, `clasif_auc_*.csv`, `clasif_marcas_*.csv`,
  `rasgos_{explora,diseno,val1,val2}.csv`, `a3f.txt`, `a3f_tabla.csv`, `a3f_cinta_40_validacion.txt`, `mecanica.txt`, `verificacion.txt`.
- Intermedios en `scratchpad\r25`: `dias_m30.pkl`, `dias_m5.pkl`, `dias_cinta.pkl`, `control_X.py`, `control_simulador.py`, `a3f_cinta40.py`.
