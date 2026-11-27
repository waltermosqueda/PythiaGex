# Ronda 20 — El GEX como contexto del régimen — RESULTADOS

27-11-2026, de 04:35 a 05:20 AR. Pre-registro con hora y 5 enmiendas: `PREREGISTRO.md`. La enmienda 1 (04:53) y la 2 (04:56)
son previas a los resultados. La 3 (05:01), la 4 (05:06) y la 5 (05:15) son controles POST HOC, declarados después de ver G1-G3.
Toda esta data ya la había mirado la ronda 19 (y partes, la 17 y la 18): no hay muestra virgen, así que se validó dejando un
período afuera. Nada de esto promete rentabilidad. El juez final es en vivo.

## Veredicto corto
1. **El GEX dice qué tan ANCHO va a ser el día, no si va a tender o volver.** Con gamma negativa a las 9:59, el rango del día
   fue 1,45 veces la mediana de las 10 ruedas previas, contra 0,95 con positiva (+0,50, t 2,67, p 0,023). El signo se repite en
   diseño (+0,32) y en confirmación (+0,58). La eficiencia del día (cuánto del rango termina como tendencia) dio igual: 0,43
   contra 0,38. La ruptura del área de ruido no siguió más con gamma negativa (diseño −2,20 X contra −2,40 X: volvió igual).
2. **Mientras pasa, el régimen no separa "va y vuelve" de "tendencia".** Con gamma negativa, las rupturas volvieron en diseño
   (−1,36 X) y siguieron en confirmación (+1,40 X). En conjunto la diferencia fue +1,66 X, t 0,82, y el placebo dio p 0,37.
   Además, una ruptura PARA ABAJO llega casi siempre con gamma negativa (138 de 139): el signo del GEX es, en la práctica,
   "el precio cayó debajo de la masa de puts".
3. **Las finalistas de la ronda 19 NO ganaban con gamma positiva.** En diseño ganaron MÁS con negativa: 74,8 % y +1,66 por
   operación, contra 66,7 % y +1,03 con positiva. En confirmación perdieron con las dos (−0,69 con negativa, −0,30 con
   positiva, t 0,56). La diferencia "positiva mejor" que aparece al juntar todo (+0,49) es por la mezcla de períodos: el placebo
   da p 0,83. El derrumbe de la R19 lo explica el PERÍODO, no la gamma.
4. **R1 (la ola a favor de la marea) es igual con las dos gammas.** Con negativa acertó 33,7 % y dio −0,67 neto; con positiva,
   33,2 % y −0,84, en 1.687 operaciones con régimen. La ola midió 1,65 X de mediana con negativa y 1,64 X con positiva. Un paseo
   al azar mide 1 + ln 2 = 1,69 X.
5. **La mecánica, en una línea:** la cobertura de los dealers con gamma negativa empuja a favor de CADA movimiento, para arriba
   y para abajo. Eso agranda el recorrido (el doble de piernas de X por rueda: 122 contra 65, t 3,4) sin cambiar la forma de la
   ola. Es como subir el volumen de la radio: suena más fuerte, pero es la misma canción. Por eso la llave de la R21 (expansión
   del rango) aparece en el 48 % de las ruedas que abren con gamma negativa, contra el 17 % de las que abren con positiva. Pero
   en esas ruedas la ruptura al cierre da −0,02 X.

---
## 1. La tabla por minuto (para la ronda 19 y la 21)
`C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad\r20\gex_minuto.parquet`
(y `.csv.gz`). Son 22.230 filas: 57 ruedas de ES × 390 minutos (9:30-15:59 NY), una por minuto, con el dato que existía en
ese instante. Armada con `r20_01_tabla_gex.py`.
- **Campos pedidos:** `ts_utc`, `edad_s`, `base`, `spot_es`, `flip_es`, `gex_neto`, `muro_call_es`, `muro_put_es`,
  `oi_max_es`, `vol_call`, `vol_put`, `regimen`.
- **Campos extra:** `dist_flip_X`, `flip_abajo_es`, `flip_arriba_es`, `n_cruces`, `gex_0dte`, `gex_sin_mensual`, `gex_norm`,
  `mayor_pos_es`, `mayor_neg_es`, `oi_call_max_es`, `oi_put_max_es`, `oi_call`, `oi_put`, `vol_call_frac`, `vol_call_frac_30`,
  `cobertura_es_por_X`, `X`, `valido` (hay cadena y el dato tiene ≤ 60 min) y `foto_hoy`.
- **Cobertura:** hay cadena en el 96,3 % de las filas y son **válidas el 85,7 %**. Las primeras ~16 min de cada rueda usan la
  foto previa a la campana (OI nuevo, dato de ayer a las 16:14): quedan `valido = False`.
- **Huecos que no se completan (regla 5):**
  - 23-09: sin cadena en la rueda.
  - 24-09: sin cadena hasta las 13:27 NY.
  - **20, 23, 24 y 25-11: no hay cadena en ningún archivo.** La rama `cadenas` de GitHub, `cboe-local` y la 5.0 terminan el
    19-11.
  - Por eso la guardada tiene GEX en **4 de 8 ruedas (16-19-11)**, y las cuatro abrieron con gamma negativa.
- **Antigüedad del dato en minutos válidos:** mediana 988 s (16,5 min), p90 1.298 s; más de 30 min en el 2,2 %.
- **Base ES − SPX:**
  - Medida con el precio de la cinta en la hora exacta del dato. El desfase 0 s da la menor dispersión: MAD 0,29-0,48 pts,
    contra 0,56-1,06 a ±15 s.
  - Contra la base archivada (forward contra forward), la diferencia mediana por rueda es +0,85 pts (p10 −0,85, p90 +2,01,
    máximo 2,84), dentro del control de ~3 pts de la memoria.
  - Roll medido: 4,5-5,8 pts con U6 (08 a 11-09) y 70,8 con Z6 el 14-09. Después baja con el carry hasta 18,9 el 19-11.
- **Flip por barrido de spot:** la curva cruza cero UNA vez en el 94,2 % de los minutos válidos, ninguna en el 5,7 % y 3+ en el
  0,2 %. Es un nivel bien definido, no el "zero" por strike que salta entre cruces vecinos.
- **Verificación a mano** (`r20_02_verificar.py`). En tres minutos, uno por período, se recalculó todo desde la línea cruda del
  archivo, fila por fila, con `scipy.norm.pdf` y la raíz con `brentq`:
  - 21-10 11:00 NY: GEX neto +9,5500 mil M (tabla +9,5500); 0DTE +8,1556 (+8,1556); flip ES 7.768,226 (7.768,226); muro de
    call 7.841,32, muro de put 7.741,32, OI máximo 7.891,32 y volumen 437.311 / 506.029, todos idénticos. Edad a mano 991 s
    (tabla 991). Contra la ronda 14 en el mismo minuto, con su propio precio y su propia base: zero 7.726,63 contra 7.726,91
    (SPX), y mismos strikes de mayor GEX positivo y negativo y de OI máximo de calls y puts.
  - 14-10 12:50: GEX neto −51,5171, flip ES 7.816,18 (precio 7.774,75, −10,36 X).
  - 17-11 10:30: GEX neto −55,8842, flip ES 7.615,29 con el precio en 7.405,00 (−31,15 X).
  - El chequeo interno (GEX del barrido en el spot contra el directo) da una diferencia máxima de 5,7e-14.
- **Límites de la tabla:**
  - Es el GEX de **corto plazo**: los archivos guardan vencimientos de hasta 8 días; se usó una ventana uniforme de 7,75. El
    mensual o trimestral entra entero en los 8 días previos a cada tercer viernes (`mensual_en_ventana`, 29,2 % de los
    minutos; `gex_sin_mensual` lo saca).
  - El **signo es un supuesto** (dealers comprados en calls y vendidos en puts). No hay flujo firmado de opciones.

## 2. ANTES: el régimen a las 9:59 contra el tipo de rueda (G1, ruedas como unidad)
| Período | Ruedas (neg/pos) | Rango / mediana de 10: neg contra pos | Eficiencia neg / pos | Ruptura al cierre (X) neg / pos |
|---|---|---|---|---|
| Diseño (08-09..15-10) | 26 (13/13) | 1,24 / 0,93 (+0,32, t 1,54, p 0,07) | 0,38 / 0,38 | −2,20 / −2,40 |
| Confirmación (16-10..13-11) | 21 (16/5) | 1,60 / 1,02 (+0,58, t 1,89, p 0,10) | 0,42 / 0,37 | −1,21 / −5,23 |
| Guardada (16-19-11) | 4 (4/0) | 1,50 / sin positivas | 0,64 / — | +2,49 / — |
| Juntas | 51 (33/18) | 1,45 / 0,95 (+0,50, t 2,67, p 0,023) | 0,43 / 0,38 (t 0,79) | −1,27 / −3,17 (t 1,05, p 0,21) |
- **Por la vara pre-registrada:**
  - Para el TAMAÑO del día, el signo esperado aparece en los dos períodos con contraste, y juntas da t ≥ 2 con p ≤ 0,05. La
    guardada no tiene ruedas positivas para comparar: las 4 con dato abrieron negativas y su rango fue 1,50 veces la mediana.
  - Para la eficiencia y la ruptura al cierre, **no separa**.
- **Variantes:**
  - A las 9:30, con la foto previa a la campana, es más flojo: +0,41, t 1,90, p 0,10.
  - Sin el 0DTE, a las 9:59, da casi lo mismo: +0,51, t 2,75, p 0,020.
  - La medida continua (`gex_norm`) da ρ −0,25 con el rango (p 0,075) y ~0 con la eficiencia y la ruptura.
- **Controles (post hoc, enmienda 3):**
  - El efecto sobre el rango sobrevive a controlar por la rueda de ayer (su movimiento, su |movimiento| y su rango relativo):
    +0,60, t 2,33. También sobrevive al movimiento de las 5 ruedas previas: +0,53, t 2,62.
  - **La mitad ya se ve en el rango de los primeros 30 min.** Con ese rango como control, la gamma baja a +0,25 (t 1,55) para
    el día y a +0,23 (t 1,59) para el rango DESPUÉS de las 10:00, con el mismo signo en diseño y confirmación.
  - Contra la memoria (ronda 14 J: "el GEX no mejora el pronóstico ATR de rango"): allá el contraste era un ATR rápido de 5 min;
    acá, la mediana lenta de 10 ruedas. Son compatibles: el GEX al abrir lleva la parte de información que el ATR rápido ya tiene.
- **Cruce con la llave de la ronda 21** (C5, post hoc). La llave es rrn ≥ 1,6, "TENDENCIA" por expansión del rango.
  - Con gamma negativa a las 9:59, la rueda llegó a ese estado después de las 10:00 en 15 de 31 (48 %); con positiva, en 3 de
    18 (17 %). Fisher una cola p 0,026; en diseño 5/11 contra 1/13 (p 0,048), en confirmación 8/16 contra 2/5 (p 0,55).
  - Llegó antes: minuto mediano 55 contra 101 desde la apertura.
  - Pero en esas ruedas la ruptura al cierre dio −0,02 X (diseño −2,21, confirmación +0,59, guardada +3,05). El GEX al abrir
    anticipa la EXPANSIÓN que la llave de la R21 detecta más tarde, no que esa expansión siga.
- **Contraejemplos que conviene tener a mano** (al 9:59):
  - 21-09: gamma +50,6 mil M y rueda de tendencia alcista (rango 2,16 veces, eficiencia 0,82).
  - 23-10: −48,2 mil M y rueda plana (0,77 veces, eficiencia 0,01).
  - 18-11 (guardada): −44,1 mil M y rueda angosta (0,79 veces).
  - Un caso a favor: el 05-11, el derrumbe de −326 pts, abrió a −23,5 X del flip (−41,2 mil M); su rango fue 5,46 veces la
    mediana.

## 3. MIENTRAS: el régimen en cada chequeo de la R19 (G2)
La vara es la de la R19, replicada exacta: en sus ruedas da −3,12 pts = −0,58 X, 40 % a favor, t −2,45 en diseño; +0,03 pts en
confirmación; +32,10 pts y 71 % en la guardada.
| Período | Gamma NEG en el chequeo | Gamma POS en el chequeo | NEG − POS |
|---|---|---|---|
| Diseño | n 84 (15 ruedas) −1,36 X, 36 % sigue | n 39 (9) +0,45 X, 46 % | −1,81 X (t −0,72) |
| Confirmación | n 92 (15) +1,40 X, 41 % | n 32 (6) −3,04 X, 25 % | +4,43 X (t 1,69) |
| Guardada | n 22 (3) +4,20 X, 68 % | — | — |
| Juntas | n 198 (33) +0,54 X, 42 % | n 71 (15) −1,12 X, 37 % | +1,66 X (t 0,82); **placebo p 0,37** |
- **Sin el 0DTE:** +0,92 X, placebo p 0,53.
- **Evolución** (fracción de minutos negativos ≥ 0,5): +1,06 X, t 0,52.
- **Dejar un período afuera con un umbral de `gex_norm`:** en los tres pliegues se entrenó el mismo umbral (−0,41).
  - Confirmación (dejada afuera): debajo del umbral sigue +3,67 X (57 %) contra −1,63 X (26 %) encima.
  - Guardada: 21 contra 1.
  - Diseño: **se da vuelta**, −5,44 X (0 %, n 8) contra −0,46 X.
- **Lo que de verdad separa es el lado de la ruptura:**
  - Para abajo: diseño −1,92 X (31 % sigue), confirmación +4,27 X (56 %), guardada +4,20 X (68 %). Casi todas con gamma negativa:
    138 de 139.
  - Para arriba: vuelve en confirmación con cualquier gamma (−3,04 X con positiva, −2,68 X con negativa). En diseño queda
    ~0 con las dos.
  - O sea: en octubre-noviembre las caídas siguieron y las subas fallaron, y en septiembre las dos volvieron. El GEX acompaña
    esa geografía (dónde está el precio respecto de los puts), pero no la anticipa.
- **"Gamma muy negativa" (`gex_norm` < −0,41) en las rupturas para abajo, control C2:**
  - Confirmación: +5,00 X, 61 %, n 38 en 5 ruedas.
  - Guardada: +3,63 X, 67 %, n 21 en 3 ruedas.
  - Diseño: −5,44 X, 0 %, n 8 en 4 ruedas.
  - Depende de dos días de derrumbe: 05-11 (+15,75 X) y 19-11 (+8,23 X). Va al baúl como condicional débil.

## 4. Las finalistas de la R19 según el régimen en la entrada (G3)
Las 4 juntas, costo 0,50, con régimen válido 424 de 501 (las 77 restantes no tienen cadena o el dato es viejo). Régimen
primario (signo del GEX neto en el minuto de la entrada):
| Período | Gamma POS | Gamma NEG | POS − NEG |
|---|---|---|---|
| Diseño | 63 ops (14 r) 66,7 %, +1,03 | 119 ops (16 r) **74,8 %, +1,66** | −0,63 (t −1,20), acierto −8,1 pp (t −1,43) |
| Confirmación | 52 (8 r) 51,9 %, −0,30 | 139 (19 r) 48,2 %, −0,69 | +0,38 (t 0,56), acierto +3,7 pp |
| Guardada | — | 51 (4 r) 39,2 %, −2,37 | — |
| Todo | 115 (22 r) 60,0 %, +0,43 | 309 (39 r) 57,0 %, −0,06 | +0,49 (t 0,89); **placebo p 0,83** |
(Sin el 0DTE el cuadro es el mismo: diseño 63,3 % / +0,75 contra 76,2 % / +1,78 (acierto −12,9 pp, t −2,03), confirmación
50,0 % contra 49,0 %, todo +0,21 con placebo p 0,92.)
- **Por finalista, con el total:** el fade de DELTA, ZONAS C1 y MODELO A ganan igual o más con negativa en diseño. E4 da vuelta
  el signo entre diseño (positiva peor, −4,86) y confirmación (positiva mejor, +3,64), con 6-16 operaciones por celda.
- **Dejar un período afuera:**
  - "Solo con gamma positiva" baja el diseño de 72,0 % / +1,44 a 66,7 % / +1,03. Deja la confirmación en 51,9 % / −0,30
    (contra 49,2 % / −0,58 sin filtro). En la guardada no opera.
  - Los umbrales entrenados de `gex_norm` y `dist_flip_X` no dan neto > 0 en la confirmación (−0,25 y −0,38). **No pasa.**
- **Por tramos de `gex_norm`** (C3, descriptivo): con gamma muy negativa (< −0,41), la vuelta al valor da 43,1 % / −1,18 en
  confirmación y 39,6 % / −2,23 en la guardada. En diseño dio 71,4 % / +1,43 (21 operaciones en 3 ruedas). Sacar ese tramo sube
  la confirmación a ~52 %, todavía con neto negativo.

## 5. R1 de la ronda 18 por régimen (S1; el pedido de acierto y esperanza)
R1 corrido orden por orden en las 57 ruedas de ES con la clase `Maquina` copiada. Reproduce las 382 operaciones de ES de la
ronda 18, idénticas una por una. Total: 2.221 operaciones, 34,2 % de acierto, −0,60 neto por operación.
| Período | Gamma NEG | Gamma POS | NEG − POS |
|---|---|---|---|
| Diseño | 458 ops, 34,1 %, neto −0,64 (t rueda −2,87), ola 1,67 X | 238, 32,4 %, −1,00 (t −2,46), 1,62 X | +0,36 (t 0,88) |
| Confirmación | 684, 33,8 %, −0,70 (t −3,89), 1,65 X | 151, 34,4 %, −0,59, 1,67 X | −0,10 (t −0,19) |
| Guardada | 156, 32,7 %, −0,67, 1,67 X | — | — |
| Todo | 1.298, 33,7 %, −0,67 (t −5,30), 1,65 X, >2X 34 % | 389, 33,2 %, −0,84 (t −2,54), 1,64 X, >2X 33 % | +0,17 (t 0,53) |
- **H1 no pasa:** el largo de la ola no cambia con la gamma (+0,04 X, t 0,51) y R1 filtrada por gamma negativa pierde
  −0,64 a −0,70 en cada período. El filtro no sube el acierto: 33,7 % contra 33,2 %.
- **El ancla:** en un paseo al azar de a un tick, simulado con el mismo zigzag (X de 16 a 24 ticks), la mediana es 1,69-1,71 X y
  el 35-37 % pasa de 2X; la teoría da 1 + ln 2 = 1,693 X y e⁻¹ = 36,8 %. El ES mide 1,65 X y 33-34 % con cualquier gamma: un
  pelo de vuelta (un paseo al azar apenas frenado), nada de impulso extra.

## 6. La mecánica: quién cubre qué y cuándo
- **El supuesto:** el cliente compra puts para protegerse y vende calls para cobrar prima; el dealer queda al revés. Con el
  precio DEBAJO del flip, pesan los puts que el dealer tiene vendidos.
  - Cuando el precio baja, esos puts suman delta largo en su cartera, y para seguir cubierto vende futuros.
  - Cuando sube, el delta largo se achica y los recompra.
  - Vende en las caídas y compra en las subas: empuja A FAVOR de cada movimiento, en las dos direcciones (gamma negativa).
  - Con el precio arriba del flip pesan sus calls comprados y hace lo contrario: frena cada movimiento.
- **Lo que eso produce, medido:** más recorrido, no otra forma.
  - Con gamma negativa al abrir, la rueda tuvo 122 piernas de X contra 65 (t 3,38), el rango 1,45 contra 0,95 veces su
    mediana, y la misma ola: 1,69 contra 1,61 X de mediana, 33 % contra 33 % de piernas > 2X.
  - Un zigzag de umbral fijo no "ve" la velocidad del precio, solo el camino. Si la cobertura solo acelera el camino, cambia
    la cantidad de piernas y no su largo, y eso es lo que da. Para que hubiera tendencia, un lado tendría que ganar seguido.
  - El dealer no elige lado: vende después de la caída y compra después de la suba. Echa nafta sobre el fuego que ya hay. Si
    el fuego es una caída sostenida (la guardada), la caída corre más. Si es un ida y vuelta (septiembre), el ida y vuelta es
    más ancho.
- **El número crudo no puede ser flujo literal.**
  - La "cobertura teórica" por X (`cobertura_es_por_X`) da de mediana 2.900-14.800 contratos de ES. Eso es 0,5 a 2,5 veces
    TODO el volumen que pasa en una pierna de X (3.800-7.100 contratos) y 7 a 51 veces su delta neto agresor (240-510).
  - Ejemplo, 17-11 10:30: GEX −55,9 mil M, unos 13.800 ES por cada 6,75 pts. Es 2,5 veces el volumen mediano de una pierna
    de X en la guardada (5.530).
  - Los dealers no tienen todo ese OI (hay spreads y clientes de los dos lados), cubren con rezago y en otros libros (SPY,
    opciones). Los trabajos con posiciones REALES los encuentran comprados en gamma casi siempre en SPX y 0DTE.
  - Por eso el GEX ingenuo describe sobre todo DÓNDE está el precio respecto de la masa de puts, no cuánto flujo hay.
- **Por período, el GEX sí acompaña el cambio de régimen de la R19:**

  | Período | Minutos con gamma negativa | Mediana de `dist_flip_X` | Mediana de `gex_norm` | Régimen de la R19 |
  |---|---|---|---|---|
  | Diseño | 50 % | −0,1 X (sentado en el flip) | 0,00 | vuelve |
  | Confirmación | 69 % | −5,4 X | −0,19 | neutro |
  | Guardada | 100 % | −23,5 X | −0,55 | sigue |

  - Con 3 períodos eso es una observación, no una prueba. Adentro de cada período no discrimina: el 08-09, el 09-09, el 10-09,
    el 14-09, el 15-09 y el 16-09 tuvieron 100 % de minutos negativos, a 4-22 X debajo del flip a las 9:59 (la guardada,
    16-27 X). En 5 de esas 6 ruedas las rupturas VOLVIERON (−0,5 a −4,9 X); solo el 08-09 siguió (+2,7 X).
- **La última media hora** (Baltussen et al. 2021: con gamma negativa, el resto del día predice los últimos 30 min). C4, post
  hoc, 55 ruedas:
  - Acá la última media hora REVIERTE: acuerdo de signo 27 %, correlación −0,33.
  - Revierte menos con gamma negativa (34 %, −0,18) que con positiva (17 %, −0,42). Es la dirección del trabajo, sin llegar a
    impulso.
- **La teoría, verificada** (citas revisadas en las fuentes primarias por un investigador aparte):
  - Ni, Pearson, Poteshman y White (2021, RFS 34(4):1952-1986), con posiciones REALES de creadores de mercado en acciones: más
    gamma, menos movimiento absoluto al día siguiente. Es lo que vemos en el rango.
  - Barbon y Buraschi, "Gamma Fragility": sigue siendo documento de trabajo (SSRN 3725454), con signo supuesto. Encuentran
    menos autocorrelación intradía con más gamma, de tamaño chico (~1 % de un desvío por desvío), indetectable en 50 ruedas.
  - Baltussen, Da, Lammers y Martens (2021, JFE 142(1):377-403), con signo supuesto: el resto del día predice la última media
    hora solo con NGE < 0.
  - Ningún trabajo muestra que el signo de la gamma prediga la DIRECCIÓN, ni que los muros o el flip funcionen como soporte o
    resistencia.

## 7. Al baúl (`PythiaGex-privado\BAUL_DE_PISTAS.md`, #54-#57)
La R21 ya había agregado del #47 al #53, y su #53 usa esta tabla: T1 en tendencia gana 57 % con gamma negativa contra 45 % con
positiva, y en diseño al revés.
- **#54 INFORMATIVA (tamaño):**
  - Gamma negativa a las 9:59 → rango del día +0,50 veces la mediana de 10 (t 2,67; diseño y confirmación con el mismo signo)
    y el doble de piernas de X. La mitad ya está en el rango de los primeros 30 min; el resto da +0,23 (t 1,6).
  - Llega a la TENDENCIA de la R21 48 % de las veces contra 17 %.
  - Sirve para el tamaño de objetivos y stops y para el semáforo de "día ancho"; dirección, no.
- **#55 CONDICIONAL débil:** gamma muy negativa (`gex_norm` < −0,41) más una ruptura para abajo sigue en confirmación (61 %) y
  en la guardada (67 %) y vuelve en diseño (0 %, n 8). La vuelta al valor con `gex_norm` < −0,41 dio 43 % y 40 % fuera de
  diseño. Fusión: semáforo "no fadear" junto con el rango en curso (#43, #47).
- **#56 OBSERVACIÓN de período:** gamma negativa en 50 % → 69 % → 100 % de los minutos, en el mismo orden que el régimen de la
  R19 (vuelve → neutro → sigue). Con 3 períodos no prueba nada.
- **#57 Última media hora:** revierte, menos con gamma negativa.
- **SIN INFORMACIÓN en esta forma:**
  - el signo del GEX como interruptor de la vuelta al valor dentro de un período (placebo p 0,83-0,92);
  - R1 por régimen;
  - el largo de la ola por régimen (1,65 X con las dos);
  - el régimen en el chequeo de la R19 (placebo p 0,37).
- **Lección de mecánica:** el GEX ingenuo no es flujo literal.
- **El dato:** las cadenas de SPX dejaron de archivarse el 19-11 en todas las fuentes. Sin reactivarlas no hay GEX hacia
  adelante.

## 8. Honestidad y límites
- Ninguna muestra es virgen: la R19 vio todos estos precios. La validación es por períodos dejados afuera. El juez es en vivo.
- **La guardada tiene GEX en 4 de 8 ruedas, y las 4 negativas:** no tiene contraste y no puede confirmar ni refutar nada sobre
  el signo.
- El signo del GEX es un supuesto. El GEX es de vencimientos de ≤ 8 días; el 0DTE es el 42-47 % de su valor absoluto, y crece
  hacia el cierre. Su valor no es flujo real (sección 6).
- **Variantes probadas:**
  - G1: 12 (3 resultados × 2 definiciones × 2 horas) + 3 continuas.
  - G2: 2 definiciones + la evolución + 3 pliegues + 2 controles.
  - G3: 5 × 2 definiciones + 6 pliegues.
  - S1: 2.
  - Post hoc: C1-C5.
- Solo el TAMAÑO del día (G1, rango) cumple la vara en los períodos con contraste. La dirección no la cumple ninguna.
- No se tocó ATAS ni ninguna rama de git, no hubo commits y todo corrió en prioridad baja, un proceso pesado por vez. En los
  chequeos la RAM libre estuvo entre 6,2 y 7,8 GB.
- Error mío corregido en el momento: en la enmienda 3 había escrito al revés lo de "138 de 139". Lo correcto es que una ruptura
  para abajo llega casi siempre con gamma negativa. No al revés: de las 198 rupturas con gamma negativa, 60 son para arriba.

## 9. Archivos
- `PREREGISTRO.md` y `RESULTADOS.md` (este documento).
- `r20_comun.py`: rutas, cadenas, gamma, base causal, t por rueda.
- `r20_00_inventario.py` (+ `_salida.txt`, `_inventario.csv`, `_alineacion_base.csv`).
- `r20_01_tabla_gex.py` (+ `_salida.txt`) y `r20_01b_resumen_tabla.py` (+ `_salida.txt`).
- `r20_02_verificar.py` (+ `_salida.txt`): verificación a mano.
- `r20_03_regimen_dia.py`: G1 y G2 (+ `_salida.txt`, `_ruedas.csv`, `_chequeos.csv`).
- `r20_04_finalistas.py`: G3 (+ `_salida.txt`, `_finalistas_con_gex.csv`).
- `r20_05_r1_por_regimen.py`: S1. Las operaciones van en `scratchpad\r20\r20_05_r1_operaciones.csv`.
- `r20_06_mecanica.py`, `r20_07_controles.py` (C1-C3), `r20_08_ultima_media_hora.py` (C4) y `r20_09_cruce_r21.py` (C5), cada
  uno con su `_salida.txt`.
- La tabla: `C:\Users\wmx_7\AppData\Local\Temp\claude\6e29e09c80928880\233bf8d4-5113-4e95-a0d9-68bff726690d\scratchpad\r20\gex_minuto.parquet`
  (+ `gex_minuto.csv.gz` y `base_muestras.csv`).
