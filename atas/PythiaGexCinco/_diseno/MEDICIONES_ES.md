# Mediciones para PythiaGex 5.0 — Gamma ES

Medido el 27-10-2026 (madrugada en Argentina; 03:00-04:30 UTC) para el clon ES/MES de la 4.1 (NQ→ES, NDX→SPX, QQQ→SPY).
Los valores que propongo, en formato de máquina, están en `constantes_medidas.json` (misma carpeta).

**Cómo leer este documento.** Cada número trae su estado:
- **MEDIDO**: lo calculé en esta sesión. Va con archivo, fechas y n.
- **CÓDIGO**: lo leí en el código (archivo:línea).
- **MEMORIA**: viene de la memoria del proyecto y no lo volví a medir.
- **SUPUESTO / PROPUESTA**: no está medido.

Todas las horas son UTC salvo que diga NY (Nueva York) o AR (Argentina, UTC−3).

**Scripts y salidas.** Los scripts están en
`C:\Users\wmx_7\AppData\Local\Temp\claude\C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada\404dab2f-b5f6-48fa-ba08-3ab764cbbdd8\scratchpad\mediciones_es\`
y sus salidas en la subcarpeta `salidas\`. Todos corrieron con `python -I` y prioridad BELOW_NORMAL; el exportador de velas, en IDLE.
- De `%APPDATA%\ATAS` solo se leyó; no se tocó ATAS.
- Bajadas a la red: una sola de `_SPX.json` y una sola de `SPY.json`, comprimidas y sin ráfagas.

---

## 0. Resumen de lo que propongo

| Qué | NQ (4.1) | ES propuesto | Respaldo (sección) |
|---|---|---|---|
| Factor único (pts NQ / k) | — | **k = 6** (redondear a 0,25; piso 0,5) | rangos 6,1 / 6,6; cobertura SPX↔NDX 5,8 (§1) |
| Radio de selección, libro ES (Rithmic) | 100 | **25** (k = 4) | cobertura libro ES↔libro NQ k = 4,1 (§1.3) |
| Radio de selección, SPX y SPY | 100 | **17** (con OI, 20) | cobertura SPX↔NDX k = 5,8 (vol) / 5,0 (OI) (§1.3) |
| Radio único (si tiene que ser uno) | 100 | **20** | compromiso entre los dos de arriba (§1.3) |
| Grilla del zero estándar | 2,5 (±300) | **0,5 (±50)** | k = 6 redondeado a 2 ticks; SPY 0,05 USD ≈ 0,50 pts ES (§1.5) |
| ZEST_MAX | 300 | **50** | k = 6 |
| FAM_GRILLA | 5 | **1,0** | divide el paso de strike 5 de ES y SPX |
| CONF_PTS (confluencia entre libros) | 3 | **0,75** | ruido de conversión NDX/SPX 3,7× (§1.4) |
| TÚNEL | 40 | **6,5** | k = 6 |
| Retraso de CBOE para alinear | 900 s | **900 s** | 900 s minimiza el ruido; 960 s lo duplica (§3.3) |
| Base SPX→ES | C7 | **C7 tal cual** + cotas de §3.8 | ruido 0,48 pts; forwards −0,7 pts contra la sincronizada (§3) |
| Razón ES/SPY | C8 | **C8** + horas [04:00, 20:00) NY + reinicio el día del ex-dividendo | trampa de las 20:00 y del 18-09 (§3.7) |
| Paso de strike cerca del dinero | NQ 10+25 · NDX 10 · QQQ 1 USD | ES 5 · SPXW 5 · SPY 1 USD (≈ 10,08 pts ES) | §2 |
| Bajada de SPX | NDX 0,88 MB gz | **1,73 MB gz / 13,1 MB JSON** (≈ 2× NDX) | §4-5 |

---

## 1. Factor de escala NQ → ES

### 1.1 Cociente de precios (MEDIDO)

**Fuente.** La Cache_v2 de ATAS decodificada:
- export viejo, solo lectura: `laboratorio\dom\ronda3\velas` (MES m5, MNQ m5, MNQZ6 m5);
- export de hoy, al scratchpad, con `m02_exportar_velas.py`: MES m1 del 07-09 22:00 al 26-10 20:59 (48.281 velas), MNQZ6 m1/m5 y MNQ m5.

**Método.** Por sesión CME (18:00-17:00 NY), cierre de la última vela común.

**Muestra.** 58 sesiones con los dos instrumentos completos (≥ 90 % de las velas de 5 min) y sin salto de roll mayor a 0,4 %, del 31-07 al 26-10-2026.

- **NQ/ES = 3,862**. p25 3,833 · p75 3,931 · rango 3,770-4,002.

### 1.2 Cociente de rangos MNQ/MES (MEDIDO, mismas 58 sesiones; `m20_rangos.py`)

| Rango | Mediana | p25-p75 | min-max |
|---|---|---|---|
| Sesión entera | **6,14** | 5,31-7,26 | 3,91-10,98 |
| RTH (09:30-16:00 NY) | 5,91 | 5,26-6,93 | 3,13-11,64 |
| Noche (18:00-09:30 NY) | 6,79 | 6,05-7,68 | 4,55-14,95 |
| 30 min, toda la sesión (mediana por sesión de bloques fijos) | **6,63** | 5,87-7,37 | 5,18-8,88 |
| 30 min, RTH | 5,59 | 4,92-6,41 | 4,15-7,50 |
| 30 min, noche | 7,04 | 6,32-7,77 | 5,55-9,60 |

- **Rangos absolutos** (mediana): sesión MES 68,6 / MNQ 412; 30 min de RTH MES 12,1 / MNQ 68,1; 30 min de noche MES 6,8 / MNQ 45,8.
- **Por mes** (sesión / 30 min): agosto 6,93 / 7,31 (n 20), septiembre 5,30 / 5,87 (n 20), octubre 6,24 / 6,64 (n 17).
- **En porcentaje**, NQ se mueve 1,58× (sesión) y 1,73× (30 min) lo que se mueve ES. Por eso el factor en puntos (~6) es bastante más grande que el cociente de precios (3,86).

### 1.3 Radio de cobertura equivalente (MEDIDO; `m40_cobertura.py` + `m41_cobertura_pares.py`)

**Qué mide.** Del 0DTE (el vencimiento vivo más cercano) se calcula la fracción de la masa que queda a ≤ R del centro.
- Masa por strike: Σ(γ_call·vol_call + γ_put·vol_put), y lo mismo con OI.
- Gamma: Black-76 para los libros de futuros y Black-Scholes para los índices, con r = 3,75 % y T = días/365 (piso de 1 min).
- Centro: el futuro (libros Rithmic) o el spot (CBOE, solo en la rueda 09:45-16:00 NY; de noche el índice está congelado).
- Muestreo: una foto por bloque de 30 min NY. Para comparar dos libros se emparejan fotos del mismo bloque.

**Libros usados.**
- NQ Rithmic: `PythiaGex\viva\viva-NQ-*` (sept, 817 fotos) y `PythiaGex4\viva\viva3-NQ-*` (07..27-10, 442 fotos).
- ES Rithmic: `PythiaGex\viva\viva-ES-*` (07-09..05-10, 323 fotos en 15 días) y `PythiaGex4\viva\viva3-ES-2026-10-27` (5 fotos, esta noche).
- SPX y NDX de CBOE, archivados (`cboe-local`, `PythiaGex\cadenas`, `PythiaGex4\cboe`): 422 y 424 fotos de rueda en 34 días (08-09..26-10).

**Qué fracción cubre NQ con R = 100** (mediana [p25-p75]):

| Libro | Vol, rueda | Vol, noche | OI, rueda | OI, noche |
|---|---|---|---|---|
| Libro NQ, sept | 0,66 [0,53-0,78] | 0,53 [0,44-0,61] | 0,60 | 0,41 |
| Libro NQ, oct | 0,59 [0,46-0,73] | 0,44 [0,36-0,54] | 0,54 | 0,33 |
| NDX | 0,64 [0,52-0,79] | — | 0,54 | — |

**Qué radio en ES da la misma fracción** (factor k = 100 / R):

| Par emparejado | n | Días | k por volumen | R vol (pts) | k por OI | R OI (pts) |
|---|---|---|---|---|---|---|
| Libro ES ↔ libro NQ, rueda | 107 | 11 | **4,15** [3,33-4,84] | 24,1 [20,7-30,0] | 3,77 | 26,5 |
| Libro ES ↔ libro NQ, noche | 200 | 14 | **3,88** [2,97-4,75] | 25,8 | 3,73 | 26,8 |
| Ídem sin la semana del roll (13..18-09), rueda / noche | 44 / 109 | 6 / 8 | 4,50 / 4,12 | 22,2 / 24,3 | 4,14 / 4,60 | — |
| SPX ↔ NDX, rueda | 419 | 34 | **5,81** [5,28-6,55] | **17,2** [15,3-19,0] | 5,03 | 19,9 |
| SPX ↔ libro NQ sept, rueda | 198 | 19 | 5,67 | 17,6 | 4,48 | 22,3 |
| SPX ↔ libro NQ oct, rueda | 166 | 14 | 6,43 | 15,6 | 5,34 | 18,7 |
| Libro ES oct ↔ libro NQ oct, noche | 5 | 1 | 8,3 | 12,0 | 3,55 | 28,1 |

- **SPX ↔ NDX por día:** k mediana 5,81 (p10 5,22, p90 6,82, n 34 días).
- **La fila de octubre del libro ES no es representativa:** son 5 bloques de una sola noche.
- **Por qué dos factores.** El volumen 0DTE del libro de ES (Rithmic) está más abierto que el de SPX: el R50 del libro ES dividido el de SPX da 1,32 (p25 1,13, p75 1,59; n 108, 11 días). En NQ no pasa: el libro NQ y NDX dan lo mismo (0,98-1,02). Por eso en la familia NQ alcanza un solo radio y en la de ES no.
- **Control teórico.** El cociente de "ancho de gamma" F·σ_ATM·√T NQ/ES en las mismas fotos da 4,9-5,7.
- **Lo que pasa hoy con R = 100 en ES** (lo que usan la clásica y la 2.0): cubre 0,97-0,99 del volumen del libro ES y 1,00 del de SPX, o sea prácticamente todo el 0DTE. NQ con 100 cubre 0,44-0,66.
- **Cobertura con otros radios:**
  - libro ES: R 25 → 0,70 (rueda) / 0,54 (noche); R 17 → 0,54 / 0,42;
  - SPX: R 17 → 0,63; R 25 → 0,79.

### 1.4 Ruido de la conversión (MEDIDO)

MAD intradía de una muestra sincronizada:
- **NQ:** NDX (C7) 1,79 pts NQ (p25 1,68, p75 1,89; 10 días, `PythiaGex4\familia\muestras-NDX-*`, 13..26-10); QQQ (C8) 1,07 pts NQ (13 días).
- **ES:** SPX 0,48 pts ES (p95 0,73; 30 días) y SPY 0,45 pts ES (p95 0,70; 28 días).
- **Cocientes:** 3,7 (NDX/SPX) y 2,4 (QQQ/SPY).

### 1.5 Propuesta

**Factor único: k = 6.** Una constante en puntos de NQ se divide por 6, se redondea a múltiplos de 0,25 (el tick de ES) y nunca queda por debajo de 0,5 (2 ticks).
- Respaldo: rango de sesión 6,14 y de 30 min 6,63 (58 sesiones); cobertura SPX ↔ NDX 5,81 (34 días); SPX ↔ libro NQ 5,7-6,4.

**Dónde conviene apartarse:**
1. **Radio de selección en el libro ES de Rithmic: k = 4, R = 25.** Medido 4,15 (rueda) / 3,88 (noche). Coincide con el default del motor 3.0 de la 4.1 para ES (CÓDIGO `GammaHoyTres.cs:401`, radioDom ES = 25).
2. **Radio en SPX/SPY: 17** por volumen, 20 por OI. Si la 5.0 necesita un solo radio para los tres libros, 20 (k = 5): cubre ~0,60 del libro ES y ~0,69 de SPX en la rueda, contra 0,59-0,66 de NQ con 100.
3. **Tolerancias atadas al ruido de conversión** (confluencias, aceptar muestras): k ≈ 3,7, no 6. Por eso CONF_PTS 3 pasa a 0,75 y no a 0,5.
4. **Constantes atadas al paso de strike** (centroide ±12, "a N strikes"): se escalan por el paso, k ≈ 1,67 (NQ 8,33 de paso medio contra ES 5). El centroide 12 de NQ (~1,4 strikes) equivale a ~7,5 en ES. La clásica usa 12 tal cual en ES (CÓDIGO).
5. **Si una constante se separa por franja:** k = 5,5 de día y 7 de noche (rangos de 30 min de 5,59 y 7,04).
6. **Lo relativo no se escala:** porcentajes y fracciones (ISLA 0,10, FUERZA 0,25, FILTRO 3 %, empate 20 %, TolStrike 0,006).
7. **Réplicas de paridad de la clásica o la 2.0:** se copian sus números literales de ES (radio min(2 %,100) = 100, centroide 12, retraso 960 s), porque es lo que hacen hoy en ES.

**Otros valores en puntos (PROPUESTA con k = 6):**
- juez: toque ±2 → ±0,5 (piso), rebote 6 → 1,0, vara 20 → 3,5;
- juntar rótulos 1,5 → 0,25;
- dibujo del motor 3.0: 150 → 25 (k = 6). El código ya usa 40 (k = 3,75); la zona 2,5 → 0,75 y lejos 15 → 4 del código son k = 3,3-3,75 (CÓDIGO `GammaHoyTres.cs:402-404`).

---

## 2. Pasos de strike cerca del dinero (MEDIDO)

**Fuentes:**
- bajada única del 27-10 a las 03:29 UTC (sello de SPX 02:48:38, spot 7.676,77; sello de SPY 00:04:56, spot 766,28);
- los libros vivos;
- el archivo de CBOE (34 días de rueda, ventana ±5 %, solo strikes con OI o volumen).

**SPXW** (0DTE y 1-3 días):
- paso de **5 pts** hasta +258..+318 por arriba y −512 por abajo; después 10, 25, 100, 200.
- Semanal del viernes (vence el 30-10): 5 pts hasta +538 / −822.
- En el archivo de 34 días, el 0DTE se ensancha por arriba a +285 (mediana; p10 200, p90 340) y por abajo no se ensancha dentro de −5 %.

**SPX AM** (mensual y trimestral):
- 5 pts hasta +388 / −642 (vence el 18-12) y +468 / −812 (vence el 20-11).

**SPY:**
- diarios: **1 USD** hasta +46 / −44 USD (≈ ±6 %), después 5 USD; viernes (30-10): 1 USD hasta +71 / −129.
- Un strike de SPY son ≈ 10,08 pts de ES (con la razón ES/SPY 10,08 del 26-10).

**ES (Rithmic), 0DTE y siguiente:** **5 pts** en la zona densa. Lo que se ensancha es la ventana suscripta, no el listado:
- la 4.1 sobre MESZ6 suscribe ±1 % denso (5 pts) y ±2 % ralo (10 pts): se ensancha a ±77-85 pts (184 suscriptos de 1.454 en el log `pythiagex4-gammahoy.log` del 26-10 a las 22:50 AR);
- la clásica (`viva-ES`) suscribía ±~130 a 5 pts y después cada 20 hasta ±480;
- Rithmic lista entre 714 y 890 contratos por serie semanal de ES (log de la clásica, 17-09). El listado completo de CME fuera de la ventana no está en disco.

**Para comparar:**
- NQ Rithmic: grilla de 10 más los x25/x75, o sea 12 strikes cada 100 pts (paso medio 8,33).
- NDX: 10 pts (en el archivo se ensancha a ~±790-840).
- QQQ: 1 USD.

---

## 3. Conversiones

### 3.1 Base SPX→ES por forwards (MEDIDO; la `base_cruda` que anotó `pythiagex.base.medir` al bajar cada foto)

**Muestra.** 11.472 fotos reales de SPX del 07-09 al 27-10 (`m30_series_cboe.py`). Se excluyeron las de agosto: son de databento, con base teórica.

**Contrato.** `base.medir` usa el contrato por fecha (Z6 desde el 10-09). La continua MES de ATAS pasó a Z6 recién en la sesión del 14-09: el 10 y 11-09 la base sincronizada con U6 dio 6,4 / 3,6 y la de forwards (Z6) 70,9 / 73,0. **La 5.0 tiene que exigir que la base sea del contrato del gráfico.**

**Calidad en la rueda:**
- error de la recta: mediana 6,4 ticks (p05 5,2, p95 8,3);
- `confiable`: 91,9 % de las fotos;
- forwards menos la base sincronizada de 24, foto por foto: **mediana −0,72** (p05 −2,44, p95 +1,39; n 6.903);
- por día: mediana −0,73 (p05 −1,99, p95 +1,38; 30 días Z6).

**De noche:**
- error: mediana 19,5 ticks (p75 51,9, p95 132,5); `confiable` solo el 11-41 % según la hora.
- **Causa medida en la bajada de hoy:** a las 22:48 NY la foto todavía trae la serie del 26-10 ya vencida (504 opciones, bid/ask 0, IV basura). `medir` la toma como el vencimiento "cercano" y da `base_cruda` 37,18 con 51,9 ticks de error. `construir` sí la excluye.
- La 4.1 tiene lo mismo con NDX (log 27-10 03:27: `base_cruda 152.74 err 55.8 tk`).

### 3.2 Base sincronizada, regla C7 aplicada a SPX (MEDIDO; `m31_conversiones.py`)

**Regla.**
- Muestra = precio del MES en (cadena_ts − 900 s), interpolado apertura→cierre dentro de la vela m1, menos el spot.
- Solo con el spot vivo (cambió contra alguna foto de los 600 s previos) y entre 09:35 y 15:59 NY.
- De día: mediana robusta (3 MAD, piso 0,5) de las últimas 24, con ≥ 5.
- La "rueda" del día: la robusta de todas las muestras del día.

**Resultados (30 días Z6):**
- **muestras por día:** mediana 298,5 (p05 11: hay días con pocas fotos);
- **ruido:** MAD intradía mediana 0,48 (p95 0,73); muestra menos la mediana de 24: p05 −1,30, p95 +1,25;
- **la mediana de 24 se mueve en el día** 1,45 pts (mediana; p95 2,69);
- **última hora contra primera:** +0,02 (p05 −0,92, p95 +1,98). No hay deriva intradía.

**Control del precio** (`m32_retraso.py`): el MES m1 interpolado contra el `futuro` de Rithmic de `viva-ES` da diferencia mediana −0,01 (p05 −0,63, p95 +0,73, MAD 0,20; n 2.455). Se excluyeron el 13 y 14-09: ese día el libro de la clásica estaba en U6, +67 pts.

**Base de la rueda por día** (pts; forwards = mediana de `base_cruda` en la rueda; carry de los indicadores = r 3,75 %, q 1,2 %):

| Día | n | Rueda | MAD | Forwards | Carry Tesoro, q 1,2 % | Carry indicadores | Días al vto. |
|---|---|---|---|---|---|---|---|
| 14-09 | 9 | 70,03 | 0,26 | 71,19 | 53,97 | 50,54 | 94,9 |
| 16-09 | 177 | 70,24 | 0,72 | 68,80 | 55,61 | 49,35 | 92,8 |
| 21-09 | 308 | 67,50 | 0,35 | 67,18 | 54,31 | 47,60 | 87,9 |
| 28-09 | 308 | 60,96 | 0,65 | 59,93 | 49,71 | 43,48 | 80,9 |
| 01-10 | 306 | 56,04 | 0,60 | 55,72 | 48,22 | 41,64 | 77,9 |
| 05-10 | 315 | 52,44 | 0,28 | 53,26 | 45,30 | 40,10 | 73,9 |
| 08-10 | 300 | 51,05 | 0,60 | 50,50 | 43,68 | 38,50 | 70,9 |
| 13-10 | 287 | 48,25 | 0,35 | 47,37 | 40,68 | 35,90 | 65,9 |
| 16-10 | 299 | 44,34 | 0,67 | 43,49 | 38,45 | 33,79 | 62,9 |
| 20-10 | 276 | 41,96 | 0,74 | 40,78 | 36,09 | 31,75 | 58,9 |
| 23-10 | 278 | 37,20 | 0,57 | 36,56 | 33,68 | 29,82 | 55,9 |
| 26-10 | 303 | 36,38 | 0,49 | 35,11 | 31,90 | 28,37 | 52,9 |

La tabla completa, con los 34 días, está en `salidas\m31_spx_ruedas.csv` y en el JSON (`conversion_SPX_a_ES.ruedas_por_dia`).

### 3.3 Cuál es el retraso real de CBOE (MEDIDO; `m32_retraso.py`)

Probé desfases L de 600 a 1.260 s y medí la MAD intradía de MES(ts − L) − spot (30 días Z6).
- **El mínimo está en L = 900 s**, en SPX (0,49) y en SPY (0,48 pts ES).
- Con 960 s (lo que usan la clásica y la 2.0) la MAD sube a 0,86 / 0,92: casi el doble de ruido.
- Con 840 s: 1,06 / 0,95.

### 3.4 De noche (MEDIDO)

**Rueda decaída como predictor.** La rueda × (vto − t) / (vto − 16:00 NY) contra la rueda del día siguiente: error mediana −0,09, p05 −2,12, p95 +2,04, MAD 1,07 (n 32). Ese es el tamaño de la incertidumbre de la base durante la noche.

**Forwards de noche contra la rueda decaída:** mediana +0,31, p05 −4,11, p95 +5,24 (n 4.018). Con error ≤ 8 ticks: −0,37 (p05 −1,70, p95 +3,80; n 1.224). Por hora NY, |dif| p95:
- 16-19 h: 2,5;
- 20-23 h: 3,4-4,8;
- 03-08 h: 6,3-8,0;
- 09 h: 12,8.

**Spot de SPX:** vivo solo de 09:30 a 16:15 NY; fuera de ese horario 0 %. Los sellos de CBOE siguen avanzando de noche (≈ 10-11 fotos nuevas por hora de 03 a 09 NY y de 17 a 19; ≈ 8 de 20 a 23; con `cboe_local` bajando cada 300 s), pero el índice no se mueve.

### 3.5 Contra los archivos de estado (MEDIDO)

- `%APPDATA%\ATAS\PythiaGex\base-rueda-ES.json` (clásica) = **52,42** (05-10 17:20:10 UTC, 23 muestras, hace 22 días).
  - Mi mediana de 24 a esa hora: **52,63**. Mi rueda del 05-10: 52,44.
  - Diferencia de 0,2: la clásica alinea a 960 s y usa el cierre de la vela.
- `PythiaGex2\base-rueda-ES.json` (2.0) = 52,1 (16:15:52 UTC, 5 muestras). Mi mediana de 24 a esa hora: 52,51.
- Los dos archivos están fuera de su vigencia de 24 h.

### 3.6 Contra el carry teórico al vencimiento de ESZ6 (18-12-2026; MEDIDO + tasas)

**Tasas:** curva de letras del Tesoro cacheada en `datos\cache\tasas-*.json`. El 23-10: 28 d 4,00 %, 56 d 4,08 %, 91 d 4,17 %. El dividendo q = 1,2 % es el default de la clásica y la 2.0 para ES (CÓDIGO).

**Resultados (30 días Z6):**
- **(r − q) implícito de la base medida = 3,4 %** (p05 3,3 %, p95 3,6 %).
- Rueda / carry de los indicadores (3,75 % − 1,2 %): **1,345** (1,29-1,42). La fórmula de los indicadores queda corta 25-43 %, aunque entra en su cota floja de ±60 %.
- Rueda − carry con la tasa del Tesoro: **+7,8** (p05 +5,0, p95 +14,8). La diferencia se achica a medida que se acerca el vencimiento: +13 a 88 días, +4,5 a 53.
- El mercado paga ~0,5 pp más que (letra − 1,2 %). No sé si es financiamiento o dividendo: no lo puedo separar.

### 3.7 Razón ES/SPY, regla C8 aplicada a SPY (MEDIDO; `m31_conversiones.py`)

**Regla.** Muestra = MES(ts − 900 s) / spot de SPY, con el spot vivo, a cualquier hora. Mediana robusta de las últimas 24 (3 MAD, piso 0,0002 × mediana), con ≥ 5. Se reinicia si cambia el contrato.

**Valor y ruido** (28 días Z6, 15-09..26-10):
- **valor:** mediana 10,1035, rango 10,0806-10,1269; baja día a día porque decae el carry;
- **ruido intradía:** MAD 0,00059 = **0,45 pts ES** (p95 0,70);
- **after-hours menos rueda:** +0,24 pts ES (p05 −0,58, p95 +0,86).

**Error contra la muestra siguiente** (pts ES, p05 / p95):
- rueda −1,42 / +1,29 (n 6.798);
- after −0,90 / +1,32 (n 706);
- pre-mercado −3,12 / +1,15 (n 651).

**Trampa de las 20:00 NY.** A esa hora el spot de SPY vuelve al cierre oficial: las 23 muestras de 20:00 a 20:06 NY dan errores de hasta 36 pts (|p95| 35,4). De 04:00 a 19:59 NY el |p95| contra la razón de la rueda del mismo día es 1,4-2,4. **Regla: solo muestras con t_spot NY en [04:00, 20:00).**

**Cobertura horaria del spot de SPY:**
- pre-mercado 04:00-09:30: vivo 25-75 % de las fotos;
- after-hours 16:00-19:59: 96-99 %;
- 20:00-04:00: nada.

**Noche.** La razón de la tarde, sostenida hasta las 10:30 NY del día siguiente, erra mediana −1,0 pt ES (p10 −2,9, |máx| 4,2; n 26). El cambio día a día de la razón de la rueda es −1,15 pts ES (p05 −3,0, p95 +0,46). Una deriva de −1 pt por día está medida pero **no probada** como corrección.

**Ex-dividendo de SPY, 18-09-2026** (tercer viernes, MEDIDO):
- SPX/SPY pasó de 10,0155 (17-09) a 10,0401 (18-09) y 10,0393 (21-09): salto de **+0,024**, ≈ un dividendo de 1,85 USD.
- ES/SPY pasó de 10,1065 a 10,1269 (+0,0204): ≈ **+15,6 pts ES**, que son +18 por el dividendo menos 2,7 de carry.
- **El C8 sin reinicio tardó.** Las muestras nuevas aparecen desde las 05:47 NY (pre-mercado), pero la mediana robusta las trató como atípicas hasta las 11:58-12:50 NY. Esa mañana los niveles de SPY quedaron ~16 pts corridos. Fue un día con pocas fotos.
- Después del ex-dividendo, SPX/SPY baja despacio: 10,0393 → 10,0331 en 25 ruedas, porque se va acumulando el dividendo siguiente.

**Diciembre (SUPUESTO).** El próximo ex-dividendo sería el **18-12-2026**: tercer viernes, el mismo día en que vence ESZ6 a las 09:30 NY. Por analogía, con un salto del mismo orden (+0,024 en SPX/SPY). Esa semana además se rola: CME rola el 10-12 y la continua de ATAS rolaría el domingo 13-12 (por analogía con septiembre). El C8 se reinicia en el roll y tiene que volver a reiniciarse el 18-12 a las 04:00 NY.

### 3.8 Cotas para aceptar o descartar (PROPUESTA sobre lo MEDIDO)

**Lo que tiene la 4.1 (CÓDIGO):**
- **C7 (NDX):** 3 MAD con piso 0,5; MIN_DIA 5; MIN_RUEDA 20; VENTANA 24; control de roll (`ConversionNdx.cs:113-115`).
- **C8 (QQQ):** piso 0,0002 × mediana; VENTANA 24; MIN_N 5 (`ConversionQqq.cs:100-101`).
- **Sanidad de la familia:** base NDX en (0, 2000] y razón QQQ en [30, 60] (`GammaHoyTresCapas.cs:283`).
- **Capa 3.0:** forwards contra la otra base, ≤ 40 pts (`GammaHoyTresCapas.cs:255`).
- **Clásica:** |b − carry| ≤ max(0,6 |carry|, 0,0006 fut) (`GammaHoyNucleo.cs:273`, `ClasicaNdx.cs:13`).

**Base SPX→ES:**
- **Muestra:** |m − mediana| ≤ max(3 MAD, 0,5). Con 3 MAD medido = 1,45, el piso no muerde.
- **Sanidad:** 0 < base ≤ 150. Medido en Z6: 35-72; un trimestral recién rolado anda en ~65-75.
- **Carry:** |base − fut × 0,034 × días/365| ≤ max(0,25 × eso, 3 pts). Los 30 días Z6 caen dentro de ±6 %.
- **Forwards contra sincronizada:** en la rueda |d| ≤ 3. De noche, solo con error ≤ 8 ticks y |d| ≤ 4.
- **Contrato:** el de la muestra tiene que ser el del gráfico.

**Razón ES/SPY:**
- **Muestra:** |m − mediana| ≤ max(3 MAD, 0,0002 × mediana). Medido 3 MAD = 0,0018, casi igual al piso de 0,0020 (≈ 1,5 pts).
- **Sanidad:** 9,95 ≤ razón ≤ 10,25. Medido 10,081-10,127; SPX/SPY 10,015-10,040.
- **Horas:** t_spot NY en [04:00, 20:00).
- **Ex-dividendo:** reiniciar la ventana a las 04:00 NY el tercer viernes de mar/jun/sep/dic.

---

## 4. El JSON de `_SPX` y `SPY` de CBOE (una bajada por ticker, MEDIDO; `m10_bajar_cboe.py` + `m11_parsear_crudo.py`)

**Cómo se bajó.** De `https://cdn-api.cboe.com/api/global/delayed_quotes/options/<sim>.json`, con `Accept-Encoding: gzip`, en trozos de 64 KB, con pausa (tope 900 KB/s) y SO_RCVBUF de 64 KB. Los bytes quedaron tal como llegaron en `scratchpad\mediciones_es\bajadas_spx\` y `bajadas_spy\`.

| | `_SPX` | `SPY` |
|---|---|---|
| Hora de la bajada | 03:29:04 UTC | 03:29:13 UTC |
| Sello de la cadena | 02:48:38 | 00:04:56 |
| Comprimido | **1.725.155 B** | **826.247 B** |
| Sin comprimir | **13.088.116 B** (×7,59) | **5.673.948 B** (×6,87) |
| Tiempo de bajada | 2,467 s (27 trozos, 683 KB/s medio; cabeceras 0,58 s) | 1,121 s (13 trozos, 720 KB/s) |
| `gunzip` / `json.loads` en Python | 17 ms / **199 ms** | 7,5 ms / **75 ms** |
| Opciones | **29.424** | **12.746** |
| Fechas de vencimiento | **55** (60 series fecha+raíz); la última 2031-12-19 | 34; la última 2029-01-19 |

**Símbolos de SPX.** OCC `SPXW261027C07680000` / `SPX261218P...`:
- **SPX (AM) 9.394, SPXW 20.030.**
- 5 fechas tienen las dos raíces: 20-11, 18-12 (el trimestral: SPX con 5,98 M de OI y SPXW con 144 mil), 15-01, 19-02 y 19-03-2027.
- 15 fechas tienen solo SPX AM, de abril 2027 a 2031.

**Campos por opción:** option, bid, bid_size, ask, ask_size, iv, open_interest, volume, delta, gamma, vega, theta, rho, theo, change, open, high, low, tick, last_trade_price, last_trade_time, percent_change, prev_day_close.

**Opciones acumuladas por plazo (SPX):** ≤ 0 días 504 (la vencida del 26-10) · ≤ 1: 1.008 · ≤ 3: 1.994 · ≤ 8: 3.836 · ≤ 14: 5.682 · ≤ 30: 11.178.

**El spot de noche.**
- **SPX:** `current_price` 7.676,77 congelado en el cierre de las 16:15. El forward del 0DTE (27-10) ya estaba en 7.691,09 (12 strikes, dispersión 0,10) y el de ESZ6 (18-12, SPX AM) en 7.726,93.
- **SPY:** `current_price` 766,28 a las 20:04 NY, ya con precios del after-hours (cierre 765,37).

**El archivo flaco** (`cboe-local`, 26-10):
- SPX: 865 filas por foto (mediana), 38,4 KB por línea, 4,6 MB por día en gz.
- SPY: 456 filas, 20,8 KB, 2,5 MB por día.
- Para comparar: NDX 1.035 filas y QQQ 362.

**Cadencia en la rueda:** los sellos de SPX y SPY avanzan cada 60 s (p10 60, p90 120). La bajada llega 32 s (SPX) y 44 s (SPY) después del sello (mediana, `cboe_local`).

---

## 5. Comparación con lo que ya baja la 4.1 (MEDIDO en `pythiagex4-cboe.log`, 09-10 01:04 → 27-10 03:27 UTC; `m12_log_bajador41.py`)

| | Comprimido | JSON | Tiempo de bajada | Bajadas lentas |
|---|---|---|---|---|
| NDX (n 4.604) | mediana 881 KB (p95 925, máx 936) | 6,94 MB | 1,18 s rueda / 1,42 s fuera (p95 8,4 / 5,4 s, máx 51 s) | 173 > 10 s, 46 > 20 s, **8 > 30 s**, 1 > 45 s |
| QQQ (n 4.592) | 727-739 KB | 4,85-4,95 MB | 0,91 s | 18 > 30 s |
| TQQQ | 146-148 KB | — | — | — |

- Tasa efectiva mediana 600-780 KB/s, por debajo del tope de 1.250 KB/s.
- Fallos: pocos (DNS, conexiones cortadas por el servidor, un tope de 60 s el 10-10).

**SPX con el bajador de la 4.1** (tope 1.250 KB/s, trozos de 64 KB, SO_RCVBUF 64 KB; ESTIMADO a partir de lo medido):
- SPX pesa ≈ 1,96× NDX comprimido; SPY ≈ 1,1× QQQ.
- Las pausas solas suman 26 × 51 ms ≈ 1,33 s (26 trozos completos y uno parcial). A la tasa efectiva de NDX (727 KB/s), la bajada de SPX tarda **~2,4 s** de mediana y ~10-16 s en el p95.
- Superar el tope de 60 s es ~0,2 % (la frecuencia de NDX > 30 s: 8 en 4.604).
- **Ráfaga máxima: un trozo de 64 KB.** El ciclo con 5 tickers (NDX + QQQ + TQQQ + SPX + SPY) son 4,31 MB cada 75 s, ≈ 57 KB/s de promedio. **Aguanta sin ráfagas largas.**
- En memoria, SPX + SPY (1.321 filas por par de fotos) pesan como NDX + QQQ + TQQQ (1.432).

**Ojo:** `cboe_local.py` ya baja `_SPX` y `SPY` (además de NDX, QQQ y TQQQ) cada 75 s en la rueda y cada 300 s fuera. Lo hace en trozos de 256 KB con pausa de 50 ms, o sea con un tope de ~5 MB/s (`pythiagex\fuentes.py`). El log muestra que corre: la última línea de ES es del 27-10 a las 00:49 AR. Si la 5.0 baja SPX por su cuenta, esta PC baja SPX dos veces por ciclo. La regla de la 4.1 es "un solo indicador, nada externo"; la carpeta `cboe-local` existe como lectura extra (`CarpetaExtra`).

---

## 6. Qué hacen hoy la clásica y la 2.0 en un gráfico de ES

Leído del código por un subagente y verificado por mí en 4 citas (`GammaHoyCapas.cs:376-380`, `GammaHoyNucleo.cs:353-354`, `GammaHoy.cs:1449-1478`, 2.0 `GammaHoy.cs:88`). Los conteos de log los medí yo.

**Capas.** En ES no hay **ninguna**: `CapaActiva` devuelve false si la raíz no es NQ (clásica `GammaHoyCapas.cs:376-380`, 2.0 `:434-438`). Hay un solo libro, la primaria:
- clásica: `Libro = CBOE_SPX` por defecto (`GammaHoy.cs:81`); también puede ser SPY por razón o Rithmic ES;
- 2.0: `LibroAuto` hace que en ES sea siempre SPX (`GammaHoy.cs:88`).

**Radio de dominantes:** min(2 % del futuro, 100 pts) = **100 en ES** (`GammaHoyNucleo.cs:353-354`), o sea ~todo el 0DTE (§1.3).

**Regla de selección:**
- clásica: 2 por |GexVol| (en su .ws, 3), una por lado con empate del 20 %, centroide ±12;
- 2.0: las 2 mayores, sin lado ni centroide.

**Base SPX:**
- cascada medida > medida hace ≤ 360 min > de la rueda (≤ 24 h) > cruda > teórica;
- cota |b − carry| ≤ max(0,6 |carry|, 0,0006 fut), con carry = fut × (0,0375 − 0,012) × días/365;
- base de la rueda: 24 muestras vela(ts − 960 s) − spot, entre 13:50 y 20:10 UTC, tope max(3 MAD, 0,0002 × precio), ≥ 5, guardada en `base-rueda-ES.json` (`GammaHoy.cs:1449-1478`).

**Razón SPY:**
- clásica: Close(vela ts − 960 s) / spot, sin persistir;
- 2.0: la vela del `ultimo_trade` y el archivo `razon-ES-SPY.txt`, que no existe en disco.

**porBeta:** solo en las capas de NQ (apalancamiento = 1/beta de los retornos NQ/ES de 90 min). En ES no se usa.

**Multiplicador:** `MULT_INDICE = 100` para todos los libros, también para Rithmic ES (el real es 50).

**Modelo:** Black-Scholes con r 3,75 % sin dividendo para SPX y SPY; Black-76 para Rithmic.

**Zero:**
- clásica: grilla de 61 precios en ±3 %, el primer cruce;
- 2.0: cruce de signo por strike interpolado, el más cercano.

**Salidas de referencia para paridad (MEDIDO en los logs):**
- **Clásica** (`pythiagex-gammahoy.log`): **13.472 AUDIT en ES**, del 07-09 14:23 al **05-10 21:32 AR**. La última: `fut=7834.50 S=7782.08 base=52.42 origen=de_la_rueda_hace_433_min zeroVol=7808.37 zeroOi=7776.84 doms=7853.45/7834.96/7860.23`. Hay además 402 líneas `base muestra` de ES, del 11-09 al 05-10.
- **2.0** (`pythiagex2-gammahoy.log`): 5 AUDIT en ES, del 05-10 entre las 13:11 y las 13:15 AR.
- Las líneas `capa=SPX/SPY` son todas de gráficos de NQ.
- **No hay salidas de ES de la clásica ni de la 2.0 después del 05-10.**
- Lo único reciente en ES es la 4.1 (motor 3.0) sobre MESZ6: 119 AUDIT3 del 26-10 22:50 al 27-10 00:55 AR, con `libro_ES_vivo_(API)`, radioDom 25, radioDibujo 40 y 0 capas SPX/SPY.

---

## 7. Lo que falta o no se pudo medir

- **SPX de noche:** con el índice congelado no se puede medir la cobertura ni la base sincronizada. Solo quedan los forwards (con mucho error) y la rueda decaída.
- **Libro ES de Rithmic:** el grueso son 15 días de septiembre, con la semana del roll y strikes corridos por el spread. Octubre tiene solo 5 bloques de esta noche.
- **Listado de CME:** no está el listado completo de strikes de ES fuera de la ventana suscripta; solo el conteo de contratos por serie.
- **Ex-dividendo de diciembre:** supuesto por analogía con septiembre. No sé el monto.
- **(r − q):** medí el implícito (3,4 %), pero no puedo separar cuánto es tasa y cuánto dividendo.
- **MES m1 antes del 07-09:** no se usó. La base sincronizada empieza el 08-09 (U6) y el 14-09 (Z6).
- **Bandas del juez y rótulos:** las de §1.5 son PROPUESTA, sin backtest en ES.
