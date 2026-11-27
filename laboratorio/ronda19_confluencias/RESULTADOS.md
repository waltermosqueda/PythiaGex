# Ronda 19 — Entrada de calidad por confluencias (ola + marea + niveles + order flow) — RESULTADOS

27-11-2026, 02:48 a 04:30 AR. Pre-registro con horas y enmiendas: `PREREGISTRO.md` (las correcciones de hora están declaradas en una
nota al final). Registro de TODAS las variantes de los cuatro agentes: `registro_variantes*.csv` (1.321 de diseño). Nada de esto
promete rentabilidad.

## Veredicto en tres líneas
1. **Nada pasó.** Las 4 finalistas (una por fuente) daban 58-71 % de acierto honesto en diseño y fuera de muestra cayeron a 47-51 %
   con objetivo 1R, lo que da una moneda, y neto negativo con costo. Juntas, 29 ruedas: t por rueda de −2,17 a −0,56 contra un umbral de +2,37.
2. **Qué explica el derrumbe:** las cuatro eran apuestas a la REVERSIÓN al valor. El diseño (sep-oct) fue un período de reversión;
   en la confirmación la reversión se apagó y en la guardada (ES 16-25-11, semana de rangos de 138 pts) el mercado tendió. El borde
   estaba en el régimen, no en la huella del order flow, y la regla de régimen que usábamos llega tarde.
3. **Lo que sí queda:** el flujo de la punta (absorción del lado que defiende, big trades a favor) solo informó cuando el precio volvía
   al valor, y ni eso sobrevivió al cambio de régimen. Va al baúl como condicional, con su interruptor por construir.

## 1. Qué se probó
- **Datos:** 49 ruedas de ES orden por orden (08-09 a 13-11, sin faltantes) + 8 de ES de la guardada (16-25-11, bajadas el 27-11 desde
  ESZ6; reemplazaron a la MES por pedido del operador: el order flow se lee del ES). **Roll medido:** las cintas son U6 hasta el 11-09
  (base contra SPX 4-6 pts) y Z6 desde el 14-09 (66-71 pts); los niveles de varios días se ajustan con +67,00 pts. Huecos: uno de 143 s
  (ES 24-09 12:17); en la guardada ninguno mayor a 60 s.
- **Rasgos causales** (`r19_01_rasgos.py`; verificados contra la cinta cruda y la máquina de la R18 en 11 operaciones, al segundo): ola X,
  marea 4X, micro X/2 (pivotes idénticos a la R18; X igual a la R18 en las 17 ruedas que comparten), VWAP de jornada y RTH con bandas,
  semanal y mensual, día anterior (H/L/C, POC/VAH/VAL), Asia, Londres, balance inicial y extensiones, área de valor de la semana anterior,
  redondos de 50, absorción en la punta, icebergs, big trades (p99 por franja de ruedas previas), imbalances diagonales 300 % apilados,
  delta, divergencia, agotamiento, caudal, área de ruido de la apertura, tipo de día.
- **Mi línea (diseño, 26 ruedas):** E1 retroceso a favor de la marea en un nivel con disparo de flujo → E2 confirmada en régimen
  direccional → E3 vuelta al valor → E4 E3 + absorción o big trades → E5 fórmula mínima; familia (b) ruptura con iniciativa (base de
  18.240 cruces de nivel) y vecindario/ablaciones de E4.
- **Base de eventos común** (4.822 vueltas de la ola × 154 rasgos y resultados) sobre la que corrieron tres exploradores: ZONAS (a, d),
  DELTA (e) y MODELO (meta-etiquetado con validación purgada por rueda).

## 2. Diseño: lo que dio cada etapa (costo 0,50; t POR RUEDA; acierto = operación con bruto > 0)
| Regla | Ops (por rueda) | Acierto | Neto/op | t | Lectura |
|---|---|---|---|---|---|
| E1 S1 (2R) | 126 (4,85) | 25,4 % | −0,96 | −4,58 | entra a 2 ticks de un mínimo fresco con stop a X/2: el stop queda adentro del ruido; pierde igual con lado al azar |
| E2 S1 | 33 (1,27) | 21,2 % | −2,10 | −2,29 | seguir la ruptura en un período que vuelve: peor que el azar |
| E3 S2 (3R) | 54 (2,08) | 29,6 % | +0,08 | +0,09 | la dirección "hacia el valor" le gana al lado al azar, pero tras costo da 0 |
| E4 S1 / S6 | 30 / 36 | 50,0 % / 58,3 % | +1,61 / +0,15 | 1,29 / 0,20 | E3 + absorción o big trades del lado que defiende |
| E5b S1 (3 condiciones) | 36 (1,38) | 47,2 % | +1,19 | 1,09 | sin la marea (E5a) pierde |
| (b) B2 S4 (ruptura + marea) | 44 (1,69) | 20,5 % | +1,94 | 0,95 | acierta poco; la que no vuelve corre mucho |
| ZONAS C1 S6 (banda 2σ + iceberg) | 70 (2,69) | 70,0 % | +1,36 | 2,34 | (C4, con corte post hoc a las 10:30: 75 %, t 2,74) |
| DELTA w6 S6m (fade de la vuelta rápida) | 78 (3,00) | 70,5 % | +1,22 | 2,27 | |
| MODELO A S6 (fuera de pliegue) | 38 (1,46) | 68,4 % | +1,16 | 1,87 | dentro de muestra daba 90,6 %: inflado |
**Búsqueda múltiple:** 1.321 variantes de diseño en los registros (principal 69, ZONAS 925, DELTA 263, MODELO 64; MODELO además evaluó
~2 millones de candidatas internas). t de Bonferroni 4,73 (7,69 contando las internas); Deflated Sharpe ~0 para todas. **Nada de diseño
era significativo por sí solo.** Las cuatro fuentes llegaron por caminos distintos al mismo lugar: en este período gana el que responde
(el pasivo, el que vuelve al valor), no el que empuja.

## 3. Arbitraje y juicio fuera de muestra (abiertos UNA vez)
Regla escrita antes de ver finalistas (03:42) + enmienda (04:04) + arbitraje final (04:25): una por fuente, entre casi duplicados la de
menos ajustes elegidos mirando. **Finalistas: ZONAS C1-S6, DELTA w6-S6m (operación contraria recalculada desde la cinta), E4-S6, MODELO
A-S6.** Solapamiento entre ellas: 0-28 % (son confirmaciones distintas). Umbral: Bonferroni una cola 5 %/4 con 29 ruedas → t ≥ 2,37.

**Confirmación (ES 16-10 a 13-11, 21 ruedas)**
| Finalista | Ops (por rueda) | Acierto | Neto c0,50 (t) | Neto c1,00 (t) | Ruedas + | Placebo C p |
|---|---|---|---|---|---|---|
| ZONAS C1-S6 | 54 (2,57) | 46,3 % | −0,75 (−0,89) | −1,25 (−1,46) | 7/21 | 0,61 |
| DELTA w6-S6m | 70 (3,33) | 51,4 % | −0,57 (−1,12) | −1,07 (−2,02) | 8/21 | 0,45 |
| E4-S6 | 26 (1,24) | 50,0 % | −0,49 (−0,49) | −0,99 (−0,90) | 6/21 | 0,42 |
| MODELO A-S6 | 42 (2,00) | 47,6 % | −0,57 (−0,63) | −1,07 (−1,16) | 8/21 | 0,42 |

**Guardada (ES 16-25-11, 8 ruedas)**
| Finalista | Ops (por rueda) | Acierto | Neto c0,50 (t) | Neto c1,00 (t) | Ruedas + | Placebo C p |
|---|---|---|---|---|---|---|
| ZONAS C1-S6 | 22 (2,75) | 54,5 % | +0,40 (+0,26) | −0,10 (−0,07) | 3/8 | 0,29 |
| DELTA w6-S6m | 34 (4,25) | 38,2 % | −2,86 (−2,01) | −3,36 (−2,36) | 2/8 | 0,90 |
| E4-S6 | 14 (1,75) | 28,6 % | −4,21 (−1,62) | −4,71 (−1,74) | 1/8 | 0,95 |
| MODELO A-S6 | 23 (2,88) | 47,8 % | −0,92 (−0,66) | −1,42 (−1,02) | 2/8 | 0,51 |

**Juntas (29 ruedas):** C1 76 ops 48,7 % −0,41 (t −0,56); w6 104 ops 47,1 % −1,32 (t −2,17); E4 40 ops 42,5 % −1,79 (t −1,54); A 65 ops
47,7 % −0,69 (t −0,92). **Ninguna pasa.** Ninguna se cae "solo en la guardada": ya en la confirmación todas estaban en la moneda.
(La MES 16-25-11 queda como chequeo secundario; no se corrió porque no decide.)

## 4. La mecánica: por qué ganaban en diseño y por qué se cayeron
**El régimen cambió debajo de las cuatro.** Medido con la misma vara en las tres muestras (chequeos cada 30 min; el precio fuera del área
de ruido de la apertura; retorno al cierre del lado de la ruptura):
| Muestra | Rango RTH mediano | Días de tendencia (e ≥ 0,6) | La ruptura al cierre | A favor de la ruptura |
|---|---|---|---|---|
| Diseño (sep-oct) | 57 pts | 5/26 | **−3,1 pts** (t por rueda −2,45) | 40 % |
| Confirmación | 73 pts | 3/21 | +0,0 pts (t −1,42) | 37 % |
| Guardada (16-25-11) | **138 pts** | 3/8 | **+32,1 pts** | 71 % |
En diseño la extensión la hacía la agresión de un lado y la deshacía el libro (actividad responsiva): volver al VWAP, fadear la vuelta
rápida o comprar en la banda de −2σ con iceberg pagaba. En la confirmación ese "volver" se apagó (las cuatro dan ~50 %, lo que paga una
moneda con 1R) y en la guardada se dio vuelta: el fade de DELTA perdió 21 de 34 y E4 10 de 14. La regla de régimen del baúl (#19,
eficiencia mediana de las 10 ruedas previas < 0,465) marcó "va y vuelve" en las 8 ruedas de la guardada: mira para atrás y llega tarde.
La señal de que el régimen cambiaba estaba en el PRESENTE: el rango del día se duplicó (57 → 73 → 138 pts) y las rupturas empezaron a seguir.

**El flujo solo informó cuando el precio volvía al valor (diseño, 2.250 vueltas, 10:00-15:30, salida 2R / 1R):**
| Contexto de la vuelta | Sin absorción ni big trades | Con absorción del lado que defiende (F1) | Con big trades netos a favor (F6) |
|---|---|---|---|
| Todas | 29,6 % / −0,12 R | 32,4 % / −0,03 R | 33,5 % / −0,01 R |
| A favor de la marea y VWAP a ≥ 1R del lado de la ganancia | 26,5 % / −0,23 R (n 102) | **52,2 % / +0,57 R (n 23); 1R: 65,2 %** | 41,5 % / +0,24 R (n 41) |
| A favor de la marea con el VWAP detrás | 31,7 % / −0,05 R | 31,5 % / −0,06 R | 32,4 % / −0,05 R |
| Contra la marea | 28,5 % / −0,16 R | 29,8 % / −0,10 R | 32,4 % / −0,04 R |
Fuera de muestra, dentro de las operaciones de las finalistas (vistas una vez, sin pre-registro, n chico): en el fade de DELTA, cuando el
extremo había barrido un nivel y vuelto, 58,5 % contra 28,2 % (IC del cociente 1,21-3,56), y cuando había iceberg del lado que defiende
40,7 % contra 69,6 % (la recarga hacía real a la vuelta: fadearla perdía); en MODELO A, con el retroceso empujado por agresores 42 % contra
87,5 % (n 8); en E4, con big trades a favor 52 % contra 27 %. Mismo sentido que en diseño: el que defiende con tamaño gana; el que empuja
el retroceso con agresión, le gana a la reversión.

## 5. Operaciones ejemplares (hora NY, precio ES)
1. **Ganadora típica de la vuelta al valor (diseño):** 16-09 14:39:57, COMPRA en 7.659,75 en la vuelta de la ola (extremo 7.656,00, stop
   7.655,50), VWAP RTH 18,6 pts arriba, absorción en el bid 1,6 veces su p90 y +497 contratos netos de big trades: 1R +4,25 y 2R +8,50
   (a las 14:41:33); el VWAP habría dado +18,3. Balance inicial roto de los dos lados (día de balance).
2. **Perdedora con la misma huella pero tamaño en contra (diseño):** 01-10 11:21:46, COMPRA en 7.693,25 (extremo 7.688,25) con absorción
   en el bid, pero big trades netos −1.169 y delta de 60 s −1.032: stop −5,50. La absorción no alcanza si los grandes venden.
3. **La banda que no frenó en un día de tendencia (confirmación, ZONAS C1):** 16-10, compras en la banda −2σ con iceberg a las 11:46:04
   (7.760,50), 12:19:51 (7.746,75) y 12:58:46 (7.731,00): las tres stop (−4,75 / −4,50 / −5,25). Ese día el ES abrió en 7.802 y cerró en
   7.703 (eficiencia 0,78): la banda de 2σ era un objetivo que se corría, no una pared.
4. **El fade parado en la vía (guardada, DELTA w6):** 17-11 10:45:24, la ola se dio vuelta para abajo en 7.341,50 después de un
   retroceso empujado por compradores agresivos, y la regla compró en contra (con +448 contratos netos de big trades comprando en el
   último minuto, a favor del fade): stop −9,50 con el mercado siguiendo para abajo. En la semana de tendencia ni los grandes del lado
   del fade frenaron la caída.
5. **Cuando sí volvió (guardada, ZONAS C1):** 18-11 10:50:24, COMPRA en 7.329,25 en la banda −2σ (extremo 7.321,00) con iceberg y +257 de
   big trades, después de barrer 3 niveles y volver: objetivo 1R +8,75.

## 6. Especificación de la candidata para el indicador — EN PRUEBA (no pasó la confirmación)
Como LECTURA en el indicador aparte de la ola, con registro en vivo; nunca como flecha de orden. "Vuelta al valor con flujo + semáforo de régimen":
- **Lectura (qué se ve):** cuando la ola se da vuelta hacia el VWAP con la marea, marcar si hubo (a) absorción del lado que defiende
  (ventas que pegan en el bid sin moverlo ≥ p90 de la franja, espejo en el ask), (b) big trades netos a favor ≥ p80, (c) iceberg del lado
  pasivo, (d) el extremo en la banda de ±2σ del VWAP RTH o de jornada sin perforarla, (e) barrida de un nivel y vuelta; con la distancia
  al VWAP en R y el stop detrás del extremo (2 ticks).
- **Semáforo de régimen obligatorio (lo que faltó):** rojo "hoy no se fadea" si el rango de la rueda en curso ya supera 1,5 veces el rango
  mediano de las 10 previas o si la ruptura del área de ruido viene siguiendo en la rueda (precio fuera del ruido y la marea a favor de la
  ruptura); verde solo en balance. Es una hipótesis para la próxima ronda: NO está medida como interruptor.
- **Registro para la bitácora (15 sesiones):** cada lectura con hora, precio, R, qué piezas aparecieron, color del semáforo, y el resultado
  a 1R, 2R y al VWAP. Con eso se mide hacia adelante si el semáforo separa las ruedas donde la vuelta al valor paga.

## 7. Entregables
- `RESULTADOS.md` (este), `PREREGISTRO.md`, `DICCIONARIO_EVENTOS.md`, `registro_variantes.csv` (+ los de los exploradores).
- `operaciones_finalistas.csv` (501 filas: diseño, confirmación y guardada de las 4 finalistas, con todos sus rasgos; `lado_operacion`
  es el lado operado, opuesto a `lado` en el fade de DELTA).
- `narraciones/E1-tanda0.md` y `narraciones/E4-diseno.md` (operación por operación).
- `salidas/juez/` (salidas del juez por muestra, veredicto.json), `salidas/` (anatomías, búsqueda múltiple, contexto).
- Bases para la ronda 21 (scratchpad\r19): `eventos_todos.parquet` (diseño + confirmación), `eventos_guardada.parquet`,
  `eventos_todos_con_guardada.parquet` (5.895 vueltas con la columna `muestra`), `eventos_cruces.parquet`.
- Scripts `r19_00` a `r19_17`, `finalistas.py`.

## 8. Notas de honestidad
- Las ruedas de ES 02-13-11 (dentro de la confirmación) las habían mirado las rondas 17 y 18 con otras reglas.
- La guardada pasó de MES a ES 16-25-11 a pedido del operador antes de abrirla (enmienda 03:55); la MES quedó como chequeo secundario.
- Varias horas de encabezados del pre-registro se corrigieron en el lugar para que coincidan con el reloj del sistema, siempre antes de
  correr lo que pre-registraban; está declarado en una nota del propio pre-registro.
- Los números de diseño de los exploradores salieron de búsquedas grandes (ZONAS: ventana (−X/2, 0], F2 y el corte de las 10:30 elegidos
  mirando; MODELO: forma de B elegida mirando). Por eso el juez fue fuera de muestra y una sola vez.
