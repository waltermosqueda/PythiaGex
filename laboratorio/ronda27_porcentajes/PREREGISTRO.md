# Ronda 27 — Porcentajes calibrados de LARGO y CORTO desde el estado de La Ola — PRE-REGISTRO

Escrito el 27-11-2026 a las 14:25 AR (12:25 NY), **antes de construir la tabla de esta ronda y antes de mirar cualquier resultado de ella**.
Las horas de cada sección nueva se anotan al escribirla, con el reloj del sistema. Nada de esto promete rentabilidad.

## 0. Lo que ya se vio (honestidad de muestra)
- Las 57 cintas de ES (08-09 a 25-11-2026) **ya las miraron las rondas 17 a 25** con otras reglas (R19 las partió en diseño, confirmación y
  guardada; R21 las usó todas con LOPO/LODO; R23 y R25 usaron 55 de ellas). Son **nuevas para este modelo, no para el proyecto**. Sé por
  esas rondas, por ejemplo, que la semana 16-25-11 fue de tendencia y rangos de ~138 pts, y que sep-oct fue de reversión.
- La única muestra realmente nueva es la **bitácora en vivo desde el lunes 30-11-2026** (juez `r27_vivo.py`, sección 10).

## 1. La pregunta
¿El estado de La Ola en un minuto dado (ola y marea), más el clima de la R21, el VWAP, el balance inicial, la hora y el flujo, permite dar
un **porcentaje de LARGO y de CORTO que se cumpla** (calibrado: cuando dice 58 %, pasa ~58 %), y alguna zona de esos porcentajes da una
**regla** que acierte ≥ 60 % con neto > 0 fuera de muestra?

## 2. Muestra y partición (fijadas antes de mirar)
- Fuente: `%APPDATA%\ATAS\PythiaGex\flujo\cinta-ES-*.csv` (con `.listo`), una por jornada CME (18:00 NY de la víspera a 17:00 NY), leídas
  **tal cual, en el orden del archivo y sin sacar duplicados** (como La Ola y `herramientas/test_ola.py`; en la cinta del 25-11 hay 44 filas
  idénticas de 661.665, 0,007 %).
- **Calentamiento:** 08-09 y 09-09 (sus tamaños de orden y rangos alimentan umbrales y X; no se evalúan, como en la R19/R21).
- **55 ruedas evaluadas, en orden de fecha:**
  - **Diseño (33, 60 %):** 10-09 a 26-10-2026.
  - **Confirmación (14, 25 %):** 27-10 a 13-11-2026.
  - **Final (8, 15 %):** 16-11 a 25-11-2026. **Se abre UNA sola vez, al final, con todo congelado.**
- Control de cordura (no diseño, no juez): la cinta cortada de hoy `cinta-ESZ6-202611262300-202611271649.csv` (rueda corta).

## 3. Unidad, entrada y resultado
- **Unidad:** el cierre de cada minuto RTH. Minuto m = 0..388 (cierres 9:31:00 a 15:59:00 NY); el de las 16:00 no tiene futuro y queda
  afuera. Estado = lo que se sabía con las órdenes de t < cierre (la "foto" de La Ola). Entrada = último precio de la última orden antes del
  cierre (c).
- **Resultado principal (Y = X del día):** desde c, ¿qué toca primero, c + Y o c − Y? Se recorren las órdenes con t ≥ cierre hasta
  min(cierre + 60 min, 16:00:00 NY). Toca arriba si el máximo de una orden (máx(primero, último)) ≥ c + Y; abajo si el mínimo ≤ c − Y.
  Etiqueta 1 = arriba primero, 0 = abajo primero; **"nada"** si no toca ninguno en el plazo; **"empate"** si una misma orden toca los dos.
  "nada" y "empate" no entran en la etiqueta binaria (se informan aparte).
- **Secundarios:** Y = X/2 (al tick, mitad para arriba) e Y = 2X. Un modelo por cada Y.
- **Base:** una caminata al azar simétrica da 50 % de "arriba primero" entre las resueltas, desde cualquier ubicación (la meta es simétrica
  alrededor del precio: no tiene la trampa de las dos metas fijas de la R22/R23).

## 4. Rasgos (todo con lo anterior al cierre; familias y límites)
Escala: X = 0,08 × mediana del rango RTH de las 10 ruedas previas, al tick con mitad para arriba (ruedas previas a 08-09 desde la historia
de la R19, `scratchpad\r19\hist\resumen.json`). Se compara con la X de la R19 en las 57 ruedas.
**A. La Ola** (zigzag orden por orden desde las 18:00 NY, copia de `test_ola.Maquina`; ola = X, marea = 4X):
`o_dir` (±1), `o_ret` = |extremo − c| / X (retroceso ya hecho), `o_lar` = |extremo − origen| / X (largo de la pierna; tope 4),
`o_min` = minutos desde el extremo de origen (tope 60), `o_dnorm` = delta de la pierna / volumen de la pierna (desde el origen),
`o_con` = con agresores (signo del delta de la pierna = dirección); lo mismo para la marea: `m_dir`, `m_ret` (en X, 0-4), `m_lar` (en X,
tope 16), `m_min` (tope 240), `m_dnorm`, `m_con`; y `o_conf_min` = minutos desde la confirmación de la pierna en curso.
**B. Clima R21** (copia de `estado_minuto` de la regla congelada y de la curva f(m) fija): `rrn` (tope 4), `tipo` T/R/E/N, `lado_vw` =
signo(c − VWAP RTH), `lado_ap` = signo(c − apertura), `lado_T` = lado si TENDENCIA, si no 0. Se compara minuto a minuto con
`scratchpad\r21\estados_minuto.parquet`.
**C. VWAP RTH** (precio medio de la orden, desde las 9:30): `vw_X` = (c − VWAP) / X (−8..8), `vw_sd` = (c − VWAP) / desvío (−4..4).
**D. Balance inicial** (9:30-10:30; desde las 10:30): `ib_net` = (extensión arriba − extensión abajo) en anchos del IB (−3..3).
**E. Hora:** `mins` (minutos desde las 9:30 al cierre) y `tarde` (cierre ≥ 13:00).
**F. Flujo** (ventanas que terminan en el cierre): `d1`, `d5`, `d15` = Σ(vol × lado) / Σ vol; `med5`, `med15` = lo mismo solo con
órdenes de 10 a 49 contratos, dividido por el volumen TOTAL de la ventana; `big5` = neto de órdenes ≥ p99 de su franja de 30 min (10 ruedas
previas, mínimo 2) / volumen de 5 min; `ice1` = icebergs en el bid − icebergs en el ask en 60 s (iceberg = la agresora se comió más de lo
visible y la punta quedó en el mismo precio con tamaño ≥ 1, definición de la R19; −5..5).
**G. Fusión del baúl** (en el extremo de ORIGEN de la pierna de la ola en curso, es decir el extremo del retroceso del que salió):
`zona_o` = el extremo quedó en la zona de un nivel (definición de la R19: de L − X/4 a L + T del lado del retroceso, T = máx(2 ticks, X/8));
`barr_o` = el extremo perforó un nivel entre 0 y X/2 y el precio ya volvió del otro lado; firmados con `o_dir`. Niveles: VWAP RTH y de
jornada con ±1σ y ±2σ (en el instante del extremo), ayer máx/mín/cierre/POC/VAH/VAL RTH (con +67,00 en el roll U6→Z6 del 14-09), Asia y
Londres máx/mín, IB máx/mín y extensiones 0,5 y 1 (solo si el extremo es posterior a las 10:30), redondo de 50. (Sin VWAP semanal/mensual ni
perfil de la semana: el indicador no los tiene a mano; decisión de diseño.)
**H. Expertos ya medidos, al grano del minuto** (votos firmados = lado de la operación que harían; 0 si no):
`v_T7` = TENDENCIA, ola y marea del lado de la tendencia, VWAP RTH detrás, la ola se dio vuelta hace ≤ 5 min y (zona_o o barr_o o delta de
300 s a favor); `v_V4` = ROTACIÓN, la ola se dio vuelta hace ≤ 5 min desde un extremo entre la banda de 2σ (RTH o jornada) y X/2 adentro sin
perforarla, con iceberg del lado que defiende a ≤ X/2 del extremo en los 60 s previos a la vuelta; `v_A3` = 13:00-15:30, |c − VWAP| ≥ 1,5 X,
IB extendido ≤ 1 de ese lado, P = d_stop / (d_stop + d_VWAP) ≥ 0,55 con stop = extremo del día ± X/2 → voto hacia el VWAP.
**I. Lugar para la fusión con la R26** (cruce ES/MES, otro agente): columna reservada `es_mes` (vacía) y la función
`r27_comun.fusionar_r26(tabla, ruta)` que la llena desde una tabla (dia, m, valor) cuando exista. **No entra en ningún modelo de esta ronda.**

## 5. Modelos (fijados ahora; todos antisimétricos)
Todos son **antisimétricos**: espejar el precio (arriba ↔ abajo) da P(largo) ↔ P(corto). Así un modelo sin información da exactamente 50 % y
no se aprende la deriva de la muestra (la caída de noviembre). Rasgos "impares" (cambian de signo al espejar): direcciones, deltas,
distancias firmadas, votos; "pares": retrocesos, largos, minutos, rrn, tipo, hora.
- **M0 — solo geometría de La Ola (6):** `o_dir`, `o_dir·o_ret`, `o_dir·o_lar`, `m_dir`, `m_dir·m_ret`, `m_dir·m_lar`.
- **M1 — logística, 12 rasgos:** `m_dir`, `m_dir·[T]`, `o_dir`, `o_dir·[T]`, `lado_T`, `o_dir·o_ret`, `vw_X`, `vw_X·tarde`, `d5`,
  `d5·[T]`, `med15`, `zona_o·o_dir·[T]` ([T] = 1 en TENDENCIA).
- **M2 — árboles con boosting (XGBoost), 24 rasgos crudos** (+ el lugar 25 reservado para `es_mes`): `o_dir`, `o_ret`, `o_lar`, `o_min`,
  `o_dnorm`, `m_dir`, `m_ret`, `m_lar`, `m_min`, `m_dnorm`, `rrn`, `tipo` (T 3, R 2, E 1, N 0), `lado_T`, `vw_X`, `ib_net`, `mins`, `d1`,
  `d5`, `d15`, `med5`, `med15`, `big5`, `ice1`, `zona_o·o_dir`. Entrena con cada fila y su espejo (etiqueta invertida) y predice
  simetrizado: p = (p(x) + 1 − p(espejo(x))) / 2. **Restricción monótona creciente en `lado_T`** (R21 #47-48: en TENDENCIA el lado de la
  tendencia sigue); en lo demás la teoría no es clara y no se restringe. base_score 0,5.
- **M3 — los expertos (3):** logística sobre `v_T7`, `v_V4`, `v_A3` (cuando no vota ninguno da 50 %).
- Logísticas sin intercepto, rasgos divididos por su RMS del entrenamiento (sin centrar, para no romper la antisimetría), L2 con C en
  {nulo, 0,0003, 0,001, 0,003, 0,01, 0,03, 0,1, 1}. XGBoost: profundidad {2, 3} × árboles {60, 150} × min_child_weight {50, 200}, más el
  nulo (0 árboles); learning_rate 0,05, subsample 0,8, colsample 0,8, lambda 10, 2 hilos, semilla fija. **"Nulo" = predice 50 %: si gana
  en la validación interna, el modelo dice "sin ventaja" ese día.**
- Referencia extra (no es un modelo del juez): **M_deriva** = la frecuencia de "arriba" del entrenamiento como predicción constante.
- **Todo modelo final se puede escribir en C#**: coeficientes (logística) o árboles de profundidad ≤ 3 en JSON.

## 6. Validación
- **Walk-forward por rueda, ventana creciente:** para cada rueda d evaluada, se entrena con TODAS las ruedas evaluadas anteriores a d y se
  predice d. Empieza en la rueda 11 del diseño (10 de entrenamiento mínimo).
- **Hiperparámetros anidados:** cada 5 ruedas de prueba (ruedas 11, 16, 21, 26, 31, 34, 39, 44, 48, 53 de las 55) se eligen con validación
  interna en el entrenamiento: 4 bloques contiguos de ruedas, log-loss de las resueltas; empate → lo más simple. El modelo se re-ajusta
  todos los días con lo elegido.
- **Diseño:** las predicciones fuera de muestra de las ruedas 11-33 sirven para desarrollar (puedo mirarlas y cambiar cosas; queda anotado).
  **Confirmación y final:** con scripts congelados (SHA-256 en un archivo de cierre ANTES de correr), corridos una vez.
- Después de la confirmación se elige UNA candidata para el panel con este criterio fijado ahora: la de mayor Brier skill en la
  confirmación entre las que tengan Brier skill > 0 y ECE ≤ 0,03 en Y = X; si ninguna cumple, la candidata es "ninguna" y el panel muestra
  "sin ventaja medida". La final se corre igual para los cuatro modelos.
- El modelo para el vivo se re-ajusta con el mismo procedimiento sobre las 55 ruedas después de abrir la final, y se congela en JSON.

## 7. Métricas
- **Calibración (fuera de muestra, resueltas):** curva de confiabilidad con casilleros simétricos de P(largo) (< 0,40; 0,40-0,44; 0,44-0,47;
  0,47-0,49; 0,49-0,51; 0,51-0,53; 0,53-0,56; 0,56-0,60; ≥ 0,60), ECE (promedio ponderado de |predicho − pasó|), pendiente de calibración
  (logística de la etiqueta contra logit(p); 1 = calibrado), **Brier skill contra 0,5** (1 − Brier / 0,25) y **AUC**. Intervalos con bootstrap
  por rueda (2.000) y t por rueda del Brier skill diario.
- **Regla de decisión R-frec:** señal en el cierre de minuto (9:35-15:30 NY) si P(largo) ≥ τ (compra) o ≤ 1 − τ (venta); una posición por vez;
  máximo 5 por rueda. Bracket ±Y desde c: el objetivo cuenta lleno si una orden lo pasa 1 tick (máx ≥ c + Y + 0,25 en una compra); el stop al
  tocarlo; si los dos en la misma orden, stop; si nada, sale al último precio antes de min(cierre + 60 min, 16:00). **τ se elige SOLO con el
  entrenamiento** (predicciones fuera de pliegue de la validación interna): el menor τ de la grilla 0,500-0,700 (paso 0,005) que da ≤ 3
  operaciones por rueda en promedio. Con un modelo nulo no opera.
- **Regla R-60:** τ = 0,60 fijo (solo opera si el modelo dice ≥ 60 %). Descriptiva.
- Por operación: acierto (bruto > 0), R medio (bruto / Y), neto con costo 0,50 pt (2 ticks) y 1,00 pt (4 ticks), operaciones por rueda, t
  por rueda del neto diario, caída máxima.
- **Placebos:** (P1) lado al azar con la misma cantidad de compras y ventas en las mismas entradas (2.000 sorteos) para la regla;
  (P2) desplazamiento circular de las predicciones dentro de cada rueda (entre 60 y 329 minutos, 1.000 sorteos) para Brier skill y AUC.
  Además M_deriva. (Las trayectorias reflejadas no hacen falta para la base: la meta es simétrica y la base de una caminata sin memoria es 50 %
  exacto; P1 y P2 dan la variabilidad con la autocorrelación real.)
- **Desglose:** por tipo de clima (T/R/E/N), por franja (9:31-11:00, 11:00-13:00, 13:00-16:00) y por tramo.
- **Bonferroni:** la familia confirmatoria son 4 modelos × 3 Y × 2 pruebas (Brier skill > 0; neto de R-frec > 0) = **24 pruebas, α = 0,05/24
  = 0,0021 de una cola**, con t por rueda (gl = ruedas − 1). Todo lo demás es descriptivo.

## 8. Vara para las palabras
- **"Porcentaje calibrado":** en la confirmación Y en la final, Brier skill > 0 (t por rueda > 0) y ECE ≤ 0,03; "significativo" si además
  pasa Bonferroni en confirmación + final juntas.
- **"Regla":** acierto ≥ 60 % **y** neto > 0 con costo 0,50 en la confirmación **y** en la final, por separado; "significativa" si además el t
  de confirmación + final pasa Bonferroni y le gana a P1 (p < 0,05).
- Lo que no llegue: se separa "no paga sola / no aprueba" de "no tiene información" (esto último solo si iguala a los placebos en las dos
  muestras). Nada se rotula "descartado". Lo que quede como pista va al baúl.

## 9. Mecánica (qué explica el movimiento)
Para la explicación (descriptiva, post hoc): coeficientes de M1 en el diseño y en las 55 con IC por bootstrap de ruedas; importancia y
dependencia parcial de M2; tasa de "arriba primero" por zona (clima × franja × marea × ola); y en cada zona, qué factor sube o baja el
porcentaje y cuánto. Nunca "nada le gana al azar" a secas.

## 10. Juez en vivo (desde el 30-11)
`r27_vivo.py`: por cada cinta nueva de ES posterior al 27-11 (feriado 26-11 y ruedas cortas 27-11 y 24-12 excluidas por regla; cinta
incompleta: última orden RTH antes de las 15:59 o hueco > 300 s; roll de diciembre como en la R21), verifica las huellas SHA-256 del
modelo y del pipeline, arma la tabla del día con el pipeline congelado, aplica los modelos congelados, y anota cada minuto (predicciones de
M0-M3 y resultado) y las operaciones de R-frec y R-60 en su bitácora, con el estado de la calibración acumulada. Lectura descriptiva a las
15 sesiones; ninguna decisión antes.

## 11. Recursos (regla del operador)
El operador opera en vivo hasta las 15:15 AR: hasta esa hora solo diseño y una prueba chica. Después, Python en prioridad BELOW_NORMAL,
de a una cinta por vez, freno si la RAM libre baja de 2,5 GB, sin tocar ATAS, sin pedidos a la sonda, sin descargas. Intermedios en
`PythiaGex\datos\ronda27\`.

## Registro de corridas y cierres
(se agrega abajo, con hora, antes de cada corrida)

### Enmienda 1 — 27-11-2026 14:36 AR (diseño; antes de correr cualquier modelo; la hora la tomé del reloj del sistema al escribirla)
1. **Tabla construida y comprobada** (`r27_01_tabla.py` → `r27_01_salida.txt`; `r27_02_verificar.py` → `r27_02_salida.txt`): 55 ruedas,
   21.395 minutos; X y mediana de 10 iguales a la R19 en 55/55; clima igual a la R21 en 21.395/21.395 minutos (E↔B, R↔D, N↔D, T↔T); precio,
   máximo y mínimo idénticos; VWAP a ≤ 0,0011 pts de la R21 (la R19 sacaba duplicados exactos); ola y marea idénticas a la R19 en 21.395/21.395;
   15 de 15 minutos recalculados desde la cinta cruda con código propio (csv + `test_ola.Maquina`), y todos los pivotes de una rueda iguales.
2. **Lo que vi sin querer:** la línea de progreso del constructor imprime, por rueda, la fracción de "arriba primero" con Y = X y los minutos de
   cada clima, también para la confirmación y la final. Es la deriva de cada día (no el desempeño de ningún modelo, y un modelo antisimétrico no
   la puede usar); además ya sabía por la R19-R25 que noviembre cayó. Queda declarado.
3. **Aclaración de un rasgo:** `zona_o_s` = (zona_o **o** barr_o) × o_dir, como las piezas (a) y (b) de la T7 ("nivel o barrida"); así entra
   en M1 (× [T]) y en M2. No hay otro cambio de rasgos.
4. **Aclaración de la vara de calibración:** el ECE se informa crudo y **simetrizado** (cada fila junto a su espejo, p → 1 − p, y → 1 − y). La
   vara ECE ≤ 0,03 de la sección 6 y 8 se aplica al **simetrizado**: el crudo incluye la deriva de la muestra de prueba (un modelo que dice 50 %
   en una semana que cae ve 46 %), que ningún modelo antisimétrico puede ni debe aprender. El Brier skill no cambia con el espejo.
5. **Recursos:** las logísticas (M0, M1, M3) del diseño son livianas y se corren ahora; M2 (XGBoost) después de las 15:15 AR.

### Enmienda 2 — 27-11-2026 14:41 AR (diseño; reloj del sistema). Lo que mostró el diseño y lo que se fija para la confirmación
1. **Logísticas en el diseño** (`r27_03` con M0, M1, M3; `salidas/eval-diseno.txt`): la validación interna eligió el **nulo** (50 %) en casi todos
   los puntos: en bloques de ruedas dejados afuera, cualquier coeficiente empeora el log-loss contra 50 %, y más cuanto menos se regulariza.
   M0 eligió C 0,0003 una vez (del 15 al 21-10) y dio Brier skill −0,0011 (t −1,36) en las 23 ruedas de diseño fuera de muestra.
2. **Exploración descriptiva del diseño** (`r27_05_explorar.py`, `salidas/explorar-diseno.txt`): cada pieza da 46-52 % con t por rueda entre
   −2,2 y +2,2 (ninguna con signo estable); los votos de los expertos son raros (A3 877 minutos 56 %, T7 274 minutos 52 %, V4 136 minutos 57 %).
   **No cambio ningún modelo por esto** (sería elegir mirando: la lección de la R19). M2 todavía no corrió (después de las 15:15 AR).
3. **Se agrega, pre-registrada y sin elección, la TABLA DE ZONAS** (`r27_07_zonas.py`): Z1 voto T7, Z2 voto V4, Z3 voto A3, Z4 seguir el lado de
   la TENDENCIA, Z5 TENDENCIA con ola y marea del lado de la tendencia, Z6 ROTACIÓN lejos del VWAP (≥ 2X) → volver, Z7 ESTRECHO lejos del VWAP →
   volver, Z8 seguir la marea, Z9 seguir la ola; por tramo, con IC90 por bootstrap de ruedas. Descriptiva: no entra en Bonferroni; es la materia
   prima de lo que el panel podría mostrar. (Corregido antes de abrir nada: el t por rueda y el conteo de ruedas usan solo ruedas con ≥ 20 minutos
   en la zona, como `r27_05`.)
4. **Procedimiento fijo para lo que sigue:** (a) M2 en el diseño (después de las 15:15 AR); (b) `r27_cierre.py confirmacion` con las huellas;
   (c) `r27_03 --tramo confirmacion` (los 4 modelos y M_deriva, 3 Y) y `r27_04 --tramo confirmacion`, y `r27_07` en diseño + confirmación;
   (d) elección de la candidata con la regla de la sección 6 (sin otra regla); (e) `r27_cierre.py final`; (f) `r27_03 --tramo final`, `r27_04
   --tramo final`, `r27_04 --tramo confirmacion final` (juntas, para Bonferroni) y `r27_07` en los tres tramos, UNA vez; (g) `r27_06_congelar.py`
   con las 55 ruedas, `r27_vivo.py --verificar 2026-11-25` y `--control` con la cinta cortada de hoy; (h) mecánica descriptiva post hoc.

### Enmienda 3 — 27-11-2026 14:43 AR (reloj del sistema; plomería, sin mirar resultados de modelos)
- **Votos de los expertos contra las operaciones de las reglas congeladas** (solo fecha, hora y lado; nada de resultados): A3 (ops-cinta-A3.csv
  de la R23): 62 de 62 operaciones con mi voto del mismo lado en ese cierre. T7 (ops-COMB-T7+V4-detalle.csv de la R21): 59 de 73 con el voto en el
  cierre de minuto siguiente a la vuelta; V4: 42 de 49. Las que faltan: la ola ya se había vuelto a dar vuelta dentro del minuto, el clima cambió
  en ese cierre, o la pieza de nivel venía del VWAP semanal/mensual o del perfil de la semana (que dejé afuera por diseño, sección 4G). Son
  aproximaciones al grano del minuto, como dice el pre-registro.

### Enmienda 4 — 27-11-2026 15:18 AR (reloj del sistema): cierre del DISEÑO
- M2 corrió después de las 15:15 AR (15:16-15:17, BELOW_NORMAL, 2 hilos): la validación interna eligió el **nulo** en los 15 puntos (5 por Y).
- **Diseño completo, fuera de muestra (ruedas 11-33 = 24-09 a 26-10, 23 ruedas; `salidas/eval-diseno.txt`):** M1, M2 y M3 = nulo en todos
  los puntos (Brier skill 0, no operan). M0 eligió coeficientes en algunos puntos y **perdió**: Y = X Brier skill −0,0011 (t −1,36), AUC 0,491;
  Y = X/2 −0,0004; Y = 2X −0,0066 (t −1,77), AUC 0,465, R-frec 42 operaciones con 42,9 % y −2,35 por operación. M_deriva −0,0019 / −0,0003 /
  −0,0045. Lo que aprende en unas semanas se da vuelta en las siguientes.
- **No se cambia nada** de los modelos, de la grilla ni de la regla. Siguiente: cierre de la confirmación con huellas y corrida única.

- **Cierre confirmacion** (2026-11-27 15:18:52 AR, `cierre_confirmacion.json`): antes de abrir la confirmacion (27-10 a 13-11): modelos M0-M3 y M_deriva, 3 Y, como en las secciones 5-7 y las enmiendas 1-4
  - `r27_01_tabla.py` 93533b545792f48d417f14b593c2181ccd002ad2a86c2da8d91ba426e518e872
  - `r27_02_verificar.py` ce529369d07b2ee11196b0deae8ebd2e428b243066aa35199a9c6eaed9613398
  - `r27_03_modelos.py` c880aacd94a3e6049d98f215c2a31b7a3fa62a5de17d194665724e4141a2bc02
  - `r27_04_evaluar.py` 1b78f72ccaa0f5aa76ccdc3f298d375276d25a22277a99f038a98c62be6d6268
  - `r27_05_explorar.py` 7c922b38568c6fc5ea074b8b86503cb08d63b2a3c713fe6d566bf0a1f0693d4d
  - `r27_06_congelar.py` ba4eda9032626c20b79aec17df8d00355fccf910bc7ced9306d6c7974b5f35d9
  - `r27_07_zonas.py` a3cd841216948fdc3e2655559bce4e2c69e9deedf4a59a3087fdc2a1bb7e1fc2
  - `r27_08_mecanica.py` 8a470617a714cf6fa25013774e78b14abad2f64324c3586a50e595ba690f909f
  - `r27_cierre.py` 41f94438a03dea6e0232318aec8a98182646220cc4f55e51841678021005859d
  - `r27_comun.py` e23961b741dbb98fa261f377e5c7979e9bfbd9980ddcb6ce1941563919f948a5
  - `r27_vivo.py` c224b9e0f7d6bc31d84f1309f162e03bdca838db7ec3626a5c5b479eb36fd6a7
  - `tabla_minutos.parquet` 838cfc6a4107158a2d671460e16e706077ad4e1009d80bac8e4f0c2f88f4b59a
  - `curva_rango_por_minuto.json` 72589182d1533ccab0903e70469e8654dc7a201197d88064238dd1a2d7a6bc33

### Enmienda 5 — 27-11-2026 15:22 AR (reloj del sistema): lo que dio la CONFIRMACIÓN (abierta una vez, 15:19-15:21) y la candidata
- `salidas/eval-confirmacion.txt` (14 ruedas, 27-10 a 13-11). Y = X: M0 Brier skill −0,0017 (t −1,86), AUC 0,467; M1 y M2 nulos en todos los
  puntos; **M3 +0,0011 (IC90 +0,0003 a +0,0021; t por rueda +2,02 contra Bonferroni 3,47; P2 p 0,002), AUC 0,515, ECE simetrizada 0,006**;
  R-frec de M3 con Y = X: 17 operaciones, 41,2 %, −1,37 por operación. Y = 2X: M3 R-frec 15 operaciones (en 4 ruedas), 80,0 %, +7,68 neto
  (t 1,71; P1 p 0,003); M0 R-frec 14 operaciones, 64,3 %, +3,04 (t 0,93) con Brier skill −0,0020. M3 eligió coeficientes solo del 03 al 09-11
  (Y = X) y del 10 al 13-11 (Y = 2X); el resto, nulo.
- Tabla de zonas diseño → confirmación (`salidas/zonas-diseno+confirmacion.txt`), Y = X: voto T7 51,8 → 58,7 %; voto A3 56,4 → 56,3 %; ROTACIÓN
  lejos del VWAP → volver 53,3 → 55,5 %; marea 51,5 → 51,1 %; ola 49,2 → 50,6 %.
- **Candidata para el panel por la regla fija de la sección 6:** la de mayor Brier skill en la confirmación con Brier skill > 0 y ECE ≤ 0,03 en
  Y = X → **M3 (los expertos como rasgos)**, la única que cumple. La final se corre igual para los cuatro modelos, sin cambiar nada.

- **Cierre final** (2026-11-27 15:22:17 AR, `cierre_final.json`): antes de abrir la final (16-11 a 25-11), una sola vez; candidata M3 por la regla de la seccion 6
  - `r27_01_tabla.py` 93533b545792f48d417f14b593c2181ccd002ad2a86c2da8d91ba426e518e872
  - `r27_02_verificar.py` ce529369d07b2ee11196b0deae8ebd2e428b243066aa35199a9c6eaed9613398
  - `r27_03_modelos.py` c880aacd94a3e6049d98f215c2a31b7a3fa62a5de17d194665724e4141a2bc02
  - `r27_04_evaluar.py` 1b78f72ccaa0f5aa76ccdc3f298d375276d25a22277a99f038a98c62be6d6268
  - `r27_05_explorar.py` 7c922b38568c6fc5ea074b8b86503cb08d63b2a3c713fe6d566bf0a1f0693d4d
  - `r27_06_congelar.py` ba4eda9032626c20b79aec17df8d00355fccf910bc7ced9306d6c7974b5f35d9
  - `r27_07_zonas.py` a3cd841216948fdc3e2655559bce4e2c69e9deedf4a59a3087fdc2a1bb7e1fc2
  - `r27_08_mecanica.py` 8a470617a714cf6fa25013774e78b14abad2f64324c3586a50e595ba690f909f
  - `r27_cierre.py` 41f94438a03dea6e0232318aec8a98182646220cc4f55e51841678021005859d
  - `r27_comun.py` e23961b741dbb98fa261f377e5c7979e9bfbd9980ddcb6ce1941563919f948a5
  - `r27_vivo.py` c224b9e0f7d6bc31d84f1309f162e03bdca838db7ec3626a5c5b479eb36fd6a7
  - `tabla_minutos.parquet` 838cfc6a4107158a2d671460e16e706077ad4e1009d80bac8e4f0c2f88f4b59a
  - `curva_rango_por_minuto.json` 72589182d1533ccab0903e70469e8654dc7a201197d88064238dd1a2d7a6bc33

### Enmienda 6 — 27-11-2026 15:36 AR (reloj del sistema): lo que dio la FINAL (abierta una vez, 15:22-15:27) y cierre de la ronda
- `salidas/eval-final.txt` y `eval-confirmacion+final.txt`: la candidata **M3 no se sostuvo en la final** (Y = X: Brier skill −0,0002, IC90
  −0,0023 a +0,0021, P2 p 0,67, AUC 0,495; R-frec 33 operaciones, 45,5 %, −1,09). M0 peor que 50 % otra vez (2X: −0,0112, t −3,15, AUC 0,412). M1 y M2
  nulos. Juntas (22 ruedas), nada llega a Bonferroni (t 3,21): M3 Y = X +0,0006 (t 1,02); M3 R-frec 2X 53 operaciones, 64,2 %, +3,53 (t 1,67, P1 p
  0,069), pero la final sola dio 57,9 % (< 60 %): **no pasa la vara de "regla" ni la de "porcentaje calibrado"**.
- Después de la final: `r27_07` (tres tramos), mecánica post hoc (`r27_08`), congelado con las 55 ruedas (`r27_06`, 15:30: M0-M2 nulos; M3
  C 0,0003 para X y 2X), `r27_vivo.py --verificar`, `--simular 2026-11-23` y `--control` con la cinta cortada de hoy.
- **Huellas después de la final** (`salidas/huellas_despues_de_la_final.txt`): los scripts evaluados (`r27_comun`, `r27_03`, `r27_04`, `r27_07`), la
  tabla y la curva siguen IGUALES al cierre de la final. **`r27_vivo.py` cambió después del cierre** (se le agregó `--simular` y el ajuste de
  los niveles de ayer por el roll de diciembre): no interviene en ningún número evaluado y su huella nueva quedó en `huellas_r27.json` antes de
  cualquier dato en vivo.
