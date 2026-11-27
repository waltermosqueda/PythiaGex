# Ronda 27 — Porcentajes de LARGO y CORTO desde el estado de La Ola — RESULTADOS

27-11-2026, 14:25 a 15:40 AR. Pre-registro con horas del reloj, 6 enmiendas y dos archivos de cierre con SHA-256: `PREREGISTRO.md`,
`cierre_confirmacion.json` (15:18:52), `cierre_final.json` (15:22:17). **Las 55 ruedas ya las habían visto las rondas 17-25**: son nuevas para
este modelo, no para el proyecto. La muestra nueva de verdad es el vivo desde el lunes 30-11 (`r27_vivo.py`). Nada de esto promete rentabilidad.

## (a) Para el operador, en criollo
1. A cada minuto de 55 ruedas le hice una sola pregunta: desde acá, ¿qué toca primero, X puntos más arriba o X puntos más abajo? (X es el
   tamaño de la ola de ese día: de 3 a 10 puntos).
2. Es como tirar una moneda: si el precio caminara al azar, saldría 50 y 50 desde cualquier lugar.
3. Armé cuatro "máquinas de porcentajes" con todo lo que tenemos: la ola y la marea, el semáforo del día, la distancia al VWAP, el delta, las
   órdenes medianas, los icebergs y los votos de las reglas ya medidas (T7, la banda con iceberg y A3). Cada una aprendió solo con días
   anteriores y la probé en semanas que no había visto.
4. Resultado: **ninguna se cumplió afuera.** La de los expertos acertó un poquito en la confirmación (27-10 a 13-11) y en la última semana volvió
   a la moneda. La que usa solo la forma de la ola salió PEOR que la moneda: lo que aprende en unas semanas se da vuelta en las siguientes.
5. Lo que más pesa no es dónde está la ola sino **el río de ese día**: hay días en que 7 de cada 10 minutos terminan arriba y días en que 7 de 10
   terminan abajo. Pero el río no se lee a tiempo: en septiembre-octubre el precio volvía hacia la apertura (seguir el lado del día acertaba
   46 %) y en noviembre seguía de largo (53 %). La misma pista cambia de signo según el mes.
6. Y ni sabiendo de antemano de qué lado cierra el día se acierta mucho a X puntos: 53 a 56 %. La ola tiene ida y vuelta adentro de cualquier día.
7. La única zona que estuvo arriba de 50 en los tres tramos es la de la tarde, cuando el precio ya giró lejos del VWAP (la zona de A3): 56, 56 y
   61 %, con márgenes que tocan el 50.
8. Por eso, lo honesto en el panel es **50 / 50, "sin ventaja medida"**, y cuando se prende una zona de los expertos, mostrar qué pasó en esa zona
   con su margen y la etiqueta "en prueba".
9. El juez en vivo queda armado: desde el 30-11 anota cada minuto y cada zona; a las 15 sesiones se lee, sin tocar nada antes.

## (b) Los números (fuera de muestra, walk-forward por rueda con selección anidada; costo 0,50 pt por operación)
**Ruedas por tramo:** calentamiento 08 y 09-09 (no se evalúan); **diseño** 33 ruedas, 10-09 a 26-10 (predicciones fuera de muestra desde la rueda
11: 24-09 a 26-10, 23 ruedas); **confirmación** 14 ruedas, 27-10 a 13-11; **final** 8 ruedas, 16-11 a 25-11 (abierta una vez). Base: 50 %
exacto (meta simétrica); minutos con Y = X sin resolver en 60 min: 2,9 / 1,5 / 0,3 %.

**Calibración, Y = X** (Brier skill contra 50 %: 0 = igual que la moneda; IC90 por bootstrap de ruedas; P2 = predicciones corridas en el tiempo):
| Modelo | Diseño (23 r) | Confirmación (14 r) | Final (8 r) | Confirmación + final (22 r) |
|---|---|---|---|---|
| M0 solo geometría de La Ola | −0,0011 (AUC 0,491) | −0,0017 [−0,0032, −0,0004] (AUC 0,467) | −0,0010 (AUC 0,483) | −0,0014 [−0,0025, −0,0005], t −2,21 |
| M1 logística 12 rasgos | nulo (50 %) | nulo | nulo | 0 |
| M2 árboles 24 rasgos | nulo | nulo | nulo | 0 |
| M3 expertos (T7, V4, A3) | nulo | **+0,0011 [+0,0003, +0,0021], t +2,02, P2 p 0,002, AUC 0,515** | −0,0002 [−0,0023, +0,0021], P2 p 0,67, AUC 0,495 | +0,0006 [−0,0004, +0,0017], t +1,02 (Bonferroni 3,21), P2 p 0,12 |
| M_deriva (la frecuencia del entrenamiento) | −0,0019 | −0,0004 | +0,0025 | +0,0007 |
- Y = X/2: todos nulos en confirmación y final (M0 −0,0004 en diseño). Y = 2X: M0 −0,0066 / −0,0020 / **−0,0112 (t −3,15, AUC 0,412)**; M3 nulo /
  +0,0006 / −0,0007; juntas M0 −0,0055 (t −2,76), M3 +0,0001.
- **Curva de confiabilidad de M3, confirmación + final, Y = X** (cuando dijo… pasó): 46,2 % → 44,9 % (118 minutos, IC90 28,6-57,7); 47,7 % → 45,3 %
  (351; 41,3-50,0); 50,0 % → 47,7 % (7.678; la deriva de noviembre); 52,2 % → 55,7 % (289; 43,1-65,4); 53,7 % → 56,7 % (30). ECE simetrizada 0,0025.
  Con Y = 2X el casillero de 53,7 % pasó 34,6 % (156 minutos): ruido.
- **Selección anidada:** de los 120 puntos de elección (4 modelos × 3 Y × 10 puntos), el nulo ganó en 104; eligió coeficientes 16 veces (M0 9: X, X/2
  y 2X en algunas semanas; M3 7: X desde el 03-11, 2X desde el 10-11 y en la final). Cuando ganó un modelo, su ventaja interna de log-loss sobre el
  nulo fue de 0,00003 a 0,0014 (por ejemplo 0,69309 contra 0,69315): casi nada, y afuera no se sostuvo.

**Como regla (R-frec: umbral elegido solo con el entrenamiento para ≤ 3 operaciones por rueda; una posición por vez, máximo 5)**:
| Modelo, Y | Confirmación | Final | Juntas (22 r) | Lado al azar con la misma cantidad de compras/ventas (P1) |
|---|---|---|---|---|
| M3, Y = X | 17 ops (1,2/r), 41,2 %, −1,37 | 33 ops (4,1/r), 45,5 %, −1,09 | 50 ops (2,3/r), 44,0 %, −1,19 (t −1,17) | p 0,83 |
| M3, Y = 2X | **15 ops (1,1/r; en 4 ruedas), 80,0 %, +7,68** | 38 ops (4,8/r), 57,9 %, +1,89 | 53 ops (2,4/r), 64,2 %, R medio +0,28, **+3,53 (t +1,67)**; c1,00 +3,03 | p 0,069 (final sola 0,35) |
| M0, Y = X | 4 ops, 50 %, −0,56 | 20 ops (18 ventas en la semana que cayó), 60,0 %, +1,20 | 24 ops, 58,3 %, +0,91 (t 0,56) | p 0,13 (final 0,18: es la deriva) |
| M0, Y = 2X | 14 ops, 64,3 %, +3,04 | 20 ops, 40,0 %, −4,58 | 34 ops, 50,0 %, −1,44 | p 0,68 |
| M1, M2 (todos los Y) | no operan (nulo) | no operan | — | — |
- R-60 (solo si el modelo dice ≥ 60 %): **ningún modelo llegó nunca a 60 %** (la mayor confianza fuera de muestra fue 56,5 %, M0 con 2X el 25-11).
  No operó.
- **Vara del pedido** (acierto ≥ 60 % y neto > 0 en la confirmación Y en la final, por separado): **nadie la pasa.** M3 con 2X la pasa en la
  confirmación (80 %) y se cae en la final (57,9 %). Ningún t llega a Bonferroni (24 pruebas, α 0,0021: t 3,47 con 14 ruedas, 4,17 con 8, 3,21 con 22).

**Tabla de zonas (descriptiva, pre-registrada, sin elección; acierto = toca primero ±X del lado de la zona)** — diseño / confirmación / final / las 55:
- **Voto A3** (tarde, hacia el VWAP después del giro): 56,4 % (877 min) / 56,3 % (510) / 60,9 % (87, 4 ruedas) / **56,6 % (1.474 min en 40 ruedas,
  IC90 50,0-63,8)**. Con 2X: 57,6 / 51,5 / 96,6 (58 min) / 57,7 %.
- Voto T7: 51,8 / **58,7** / 46,1 / 52,4 % (IC90 47,4-57,1). Con 2X: 59,8 / 61,5 / 43,5 / 55,1 %.
- Voto V4: 56,6 (136 min) / 54,9 (51) / 52,9 (34) / 55,7 % (221 min en 41 ruedas, IC90 49,0-62,6).
- ROTACIÓN y lejos del VWAP (≥ 2X) → volver: 53,3 / 55,5 / 47,2 / 53,2 % (IC90 49,1-57,5; 24 de 38 ruedas sobre 50 %, t +2,23).
- Seguir el lado de la TENDENCIA: 51,4 / 51,4 / 52,6 / 51,7 %; con ola y marea del lado de la tendencia: 51,2 / 53,7 / 45,9 / 50,5 %.
- Seguir la marea: 51,5 / 51,1 / 48,0 / 50,9 %. Seguir la ola: 49,2 / 50,6 / 50,5 / 49,7 %. ESTRECHO lejos del VWAP → volver: 49,5 %.

## Mecánica: qué explica el porcentaje de un minuto y dónde (`salidas/mecanica.txt`, `salidas/explorar-diseno.txt`; post hoc)
1. **El día pesa más que todo.** La fracción de "arriba primero" por rueda tiene un desvío de 0,099 / 0,074 / 0,066 (Y = X) contra 0,026 de una
   moneda con esos minutos: los minutos de un día van juntos, como corchos en la misma corriente. Con 2X, 0,17 / 0,16 / 0,09 contra 0,03.
2. **Pero la corriente se ve tarde y cambia de signo con el mes.** Seguir el lado del día hasta ahora (el precio contra la apertura): 46,0 % en
   septiembre-octubre (el precio volvía: reversión), 49,3 % en la confirmación, 52,7 % en la semana de tendencia. Un oráculo que supiera de qué
   lado cierra el día llega solo a 52,9 / 53,3 / 56,2 % con X (57-59 % con 2X): adentro de cualquier día hay ida y vuelta de varias X.
3. **Los rasgos cambian de signo de un tramo a otro** (cambio de P(arriba) por unidad típica del rasgo, en puntos; IC90 por bootstrap de ruedas):
   la distancia firmada al VWAP −3,0 [−6,1, +1,3] → +0,8 → +2,6 (de elástico a puerta); el nivel o barrida en el origen de la ola en TENDENCIA
   −1,2 [−2,1, −0,1] → +1,35 [+0,45, +2,75] → +2,6 (de pared a escalón, el #50 del baúl pero a escala de mes); la marea +2,5 → +1,0 → −3,0
   [−4,8, −0,5]; la ola en TENDENCIA +0,3 → +0,9 → −4,9 [−6,6, −2,5]; el delta de 5 min −1,0 → +3,6 → +1,4. El único con el mismo signo en los tres:
   el lado de la TENDENCIA (+1,5 / +1,3 / +0,5, intervalos que cruzan el 0).
4. **Por eso la forma de la ola sola pierde afuera:** con la meta simétrica no hay geometría que cobrar (la chance de giro "≈ lo que ya volvió / X"
   de la R17 es la geometría de las metas fijas del zigzag). Lo que queda es memoria del mercado, y esa memoria cambia de signo con el régimen.
   M0 la aprende en unas semanas y la aplica cuando ya se dio vuelta.
5. **Memoria corta de la propia etiqueta:** si un minuto resolvió arriba, el de 15 minutos después resolvió arriba 53,4 % (X) y 66,1 % (2X); a 60
   minutos 50,4 % y 52,7 %. Es superposición de trayectorias (comparten camino), no anticipación.
6. **El flujo** (delta de 1, 5 y 15 min, órdenes de 10-49, big trades, icebergs), seguido en su dirección, dio 48,8-50,6 % con X en el diseño
   (48,2-51,5 % con 2X), ninguno con signo estable entre ruedas: acompaña el precio mientras pasa (#36), no dice cuál de las dos metas toca primero.

## (c) Qué iría en el panel de La Ola
- **Por defecto: "LARGO 50 % · CORTO 50 % — sin ventaja medida (R27, 55 ruedas)"**, en gris. Ningún modelo quedó calibrado fuera de muestra:
  mostrar 53 % o 47 % sería inventar.
- **Cuando se prende una zona de los expertos (descriptivo, "en prueba", nunca como flecha):** una línea chica con lo que pasó en esa zona, su
  margen y cuántas ruedas; con los números de las 55 ruedas, a ±X:
  - "Zona A3 (tarde, hacia el VWAP): pasó 57 % (IC90 50-64), 40 ruedas · en prueba";
  - "Zona V4 (banda con iceberg en rotación): pasó 56 % (49-63), 41 ruedas · en prueba";
  - "Zona T7 (tendencia en la vuelta de la ola): pasó 52 % (47-57); cambió de signo entre tramos · en prueba".
  Regla para mostrar una zona: ≥ 200 minutos en ≥ 15 ruedas; el número se actualiza con el juez en vivo, no a mano.
- **Lo que NO va:** el porcentaje del congelado M3 como si fuera calibrado (dice 51,5-54,4 % cuando vota alguno, y en la final no se cumplió).
- Si en el vivo algún modelo pasa la vara (15 sesiones, después de leerlo), recién ahí el panel podría mostrar "LARGO 5x % · CORTO 4x % (n casos)".
  Medición pendiente; la escribe el juez.

## (d) Al baúl (sección "RONDA 27" en `PythiaGex-privado\BAUL_DE_PISTAS.md`, #84-#90; la R26 ya había usado #79-#83)
- #84 SIN INFORMACIÓN como porcentaje: la geometría de La Ola sola con meta simétrica (peor que la moneda en 3 tramos).
- #85 CONDICIONAL / PARA FUSIÓN: los votos de los expertos como porcentaje y como regla con 2X (confirmación sí, final no).
- #86 INFORMATIVA débil: la zona A3 con meta simétrica, arriba de 50 en los tres tramos.
- #87 CONDICIONAL DE RÉGIMEN: T7, la vuelta al VWAP en ROTACIÓN y el nivel en el origen en TENDENCIA cambian de signo por mes.
- #88 EXPLICATIVA: el día y el mes pesan más que el estado de la ola; techo del oráculo del día 53-56 %.
- #89-#90 LECCIONES DE MÉTODO: el nulo dentro de la selección anidada como defensa; la ECE simetrizada para separar la deriva.
- Más una línea de "sin información estable en esta forma" (M1 y M2 nunca salieron del nulo) y el lugar de fusión con la R26.

## Comprobación desde el crudo
- Tabla por minuto (`r27_02_salida.txt`): X y mediana de 10 iguales a la R19 en 55/55; clima igual a la R21 en 21.395/21.395 minutos; precio,
  máximo y mínimo idénticos; VWAP a ≤ 0,0011 pts; ola y marea iguales a la R19 en 21.395/21.395; **15 de 15 minutos recalculados desde la cinta
  cruda** con código propio (csv + `test_ola.Maquina`), con etiqueta y salidas de la regla; todos los pivotes de la ola (76) y la marea (7) del
  27-10 iguales a la referencia.
- Votos de los expertos contra las operaciones de las reglas congeladas: A3 62/62; T7 59/73 y V4 42/49 en el cierre siguiente (enmienda 3).
- Juez en vivo: el evaluador propio de los JSON reproduce los ajustes (logística exacta; árboles a 1e-7); `--verificar 2026-11-25` y `2026-10-26`:
  tabla del juez idéntica a la de la ronda (87 columnas); `--simular 2026-11-23`: 3 jornadas por el camino real del vivo en una carpeta aparte,
  3.501 etiquetas iguales; `--control` con la cinta cortada de hoy (`salidas/control_2026-11-27.txt`) corre sin errores.
- No se tocó ATAS ni la sonda; no hubo descargas. Lo pesado corrió después de las 15:15 AR, en BELOW_NORMAL; la final pausó 4 veces por RAM
  (2,40-2,50 GB, el otro agente) y siguió sola.

## El juez en vivo (desde el lunes 30-11-2026)
- `python -I r27_vivo.py` después de que la sonda baje la cinta del día: verifica las huellas (`huellas_r27.json`), excluye por regla 26-11, 27-11,
  24-12 y cintas incompletas, pide el contrato confirmado desde el 07-12 (`vivo_contratos.json`, como la R21; los niveles de ayer se corren con el
  spread medido), arma la tabla con el pipeline congelado, aplica `modelo_congelado.json` y anota `bitacora_r27_minutos.csv` (cada minuto con
  las 12 probabilidades, los votos y el resultado), `bitacora_r27_operaciones.csv` y `bitacora_r27_jornadas.csv`. `--resumen` muestra la
  calibración acumulada, las reglas y las zonas. Lectura descriptiva a las 15 sesiones; ninguna decisión antes.
- El congelado (procedimiento sobre las 55 ruedas): M0, M1 y M2 nulos para los tres Y; M3 con C 0,0003 para X y 2X (tau 0,530 y 0,520).

## Lugar para la R26 (cruce ES/MES)
Columna reservada `es_mes` en la tabla y `r27_comun.fusionar_r26(tabla, ruta)` (une una tabla dia, m, valor). El lugar 25 de M2 queda libre para
esa variable. Para usarla: pre-registrar una ronda nueva que la agregue y correr el mismo walk-forward; no entra en nada de esta ronda.

## Notas de honestidad
1. Toda la muestra ya estaba vista por las rondas 17-25; la partición interna (diseño → confirmación → final) protege la elección de ESTE modelo,
   no la del proyecto. El juez es el vivo.
2. La línea de progreso del constructor de la tabla mostró la fracción diaria de "arriba" de todos los tramos antes de modelar (enmienda 1): es la
   deriva de cada día, que un modelo antisimétrico no usa.
3. Las tres horas de las enmiendas 1-2 las escribí primero a ojo y las corregí en el lugar con el reloj del sistema, antes de correr nada.
4. `r27_vivo.py` se modificó después del cierre de la final (modo `--simular` y el ajuste de niveles por el roll), antes de congelar y antes de
   cualquier dato en vivo; no toca ningún número evaluado (enmienda 6).
5. Los expertos al grano del minuto son aproximaciones de las reglas congeladas (A3 exacto; T7 81 % y V4 86 % de coincidencia); sus números no
   reemplazan a los jueces propios de la R21 y la R23.

## Entregables
- `PREREGISTRO.md`, `RESULTADOS.md`, `cierre_confirmacion.json`, `cierre_final.json`, `modelo_congelado.json`, `huellas_r27.json`
  (`modelo_prueba_diseno.json` es solo la prueba de plomería del juez con las ruedas de diseño: NO es el congelado).
- Scripts: `r27_comun.py` (pipeline), `r27_01_tabla.py`, `r27_02_verificar.py`, `r27_03_modelos.py`, `r27_04_evaluar.py`, `r27_05_explorar.py`,
  `r27_06_congelar.py`, `r27_07_zonas.py`, `r27_08_mecanica.py`, `r27_cierre.py`, `r27_vivo.py` (juez). Salidas `.txt` de cada uno en la carpeta
  y en `salidas/` (`eval-*.txt/json`, `ops-*.csv`, `zonas-*.txt`, `explorar-diseno.txt`, `mecanica.txt`, `control_2026-11-27.txt`).
- Intermedios en `PythiaGex\datos\ronda27\`: `tabla_minutos.parquet` (21.395 minutos × 88 columnas), `minutos\ES-*.parquet`, `historia.json`,
  `hist_tam\`, `info_jornadas.json`, `pred\pred-<tramo>-<Y>.parquet` y `sel-*.json` (lo elegido en cada punto).
