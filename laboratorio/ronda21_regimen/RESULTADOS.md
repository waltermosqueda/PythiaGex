# Ronda 21 — El RÉGIMEN del día como llave — RESULTADOS

27-11-2026, 04:36 a 05:05 AR. Pre-registro con horas, enmiendas y dos notas de hora corregidas: `PREREGISTRO.md`. Registro de variantes:
`registro_variantes.csv` (423 filas; 91 variantes distintas, 173 contando las corridas de robustez con otras llaves). Nada de esto promete
rentabilidad. **No hay muestra virgen:** las 55 ruedas de ES ya las vio la R19. La validación es por período dejado afuera (LOPO: entrenar
en dos, probar en el tercero, los 3 cortes) y por rueda dejada afuera con embargo ±2 (LODO). El juez final es en vivo.

## Para el operador, en una idea
El semáforo es un **cuentakilómetros**: compara cuánto recorrió el precio desde las 9:30 con lo que suele recorrer a esa misma hora. Si ya
hizo 1,6 veces lo normal, el día está "desatado" (tendencia): no se pelea, se sube en los retrocesos. Si va dentro de lo normal, el día
"rota": las bandas de 2 desvíos del VWAP funcionan como elásticos. Si va por debajo de lo normal, el día está "dormido": se espera.

## Veredicto en cuatro líneas
1. **Detector elegido: D1, el rango de hoy contra lo normal a esa hora** (corte 1,6 / 1,0, el mismo en los 3 pliegues). En vivo (LOPO),
   cuando marca TENDENCIA el extremo nuevo llega antes que el toque del VWAP el 60-67 % de las veces contra 37-40 % de base, en los 3
   períodos; el desplazamiento sigue hasta el cierre 56 % contra 44 %. Lo que NO hace: su estado "chico" no anticipa la vuelta (igual a la
   base) y no le gana a su placebo con p < 0,05 en 2 de 3 pliegues (p 0,07-0,30). Separa, pero no pasa la vara pre-registrada.
2. **La estrategia pre-registrada (elegir la variante dentro de cada pliegue) NO llega:** 53,8 % de acierto y +0,05 pts juntas (LOPO,
   104 ops); la meta (≥ 60 % con neto > 0) solo en la guardada. Con la rueda afuera (LODO): 58,5 %, +0,41.
3. **La evolución post hoc COMB-T7+V4 cumple la meta en los 3 períodos (60,4 / 63,8 / 66,7 %; juntas 63,1 %, +1,20 pts por operación,
   t 2,12, 2,2 ops por rueda)**, pero fue elegida mirando todo, es filo de cuchillo en el corte (2 de 9), en diseño el placebo de régimen
   no la separa (p 0,41) y su t queda lejos del Bonferroni (3,45). **Es la candidata para el juez en vivo, no una estrategia probada.**
   **Congelada (SHA-256) y probada en muestra nueva de MNQ (25 ruedas de ago-sep), NO pasó: 52,2 % y −0,13 pts de NQ por operación; en
   las 18 ruedas estrictamente anteriores a todo lo de ES, 40,7 % (sección 11).** La prueba en vivo con ES quedó armada (sección 12).
4. **La mecánica:** la llave funciona repartiendo. En el mes de reversión (diseño) cargó la rotación (banda 2σ: 74 %) y la tendencia
   perdió (43 %); en la confirmación, con más días desatados, al revés (69 % contra 53 %). Y un mismo nivel cambia de signo según el régimen:
   debajo de un retroceso en tendencia es un escalón (63 contra 52 %); en un día dormido es una pared (42 contra 53 %).

## 1. Qué se hizo
- Datos: base de 5.895 vueltas de la ola de la R19 (154 rasgos), 291 rupturas con iniciativa (B2; la guardada armada con el mismo
  código), cintas orden por orden de ES (55 ruedas), GEX por minuto de la ronda 20 (llegó a las 04:55; la guardada solo tiene 16-19-11).
- Rasgos de régimen por minuto, todos con lo anterior a cada instante (`r21_01_regimen.py`): rango hasta ahora contra la mediana de 10 ×
  la curva f(m) de la fracción del rango ya hecha a esa hora (curva FIJA medida en 256 sesiones de MES anteriores al 08-09, fuera de las 3
  muestras; `curva_rango_por_minuto.json`), velocidad de 30 min, área de ruido de la apertura y si sostiene, extensión del balance inicial,
  apertura contra el valor y el rango de ayer, pendiente y lado del VWAP, cruces y minutos desde el último toque, piernas de la marea,
  delta acumulado contra el precio, rango de ayer, noche y hueco.
- Salidas nuevas desde la cinta (`r21_03_salidas.py`; controles: P1 = S6 en 3.029 de 3.029 stops, el fade = −S6 en 5.895 de 5.895):
  P1 mitad 1R + resto 2R con stop en la entrada, P2 resto con la raya de la marea, P3 resto al VWAP, y las del lado contrario con el mismo R.
- **Comprobación desde la cinta cruda** (`r21_08_ejemplos.py`, independiente del motor): 10 de 10 verificaciones (9 operaciones
  distintas, de los 3 períodos y las 2 familias, ganadoras y perdedoras) coinciden en precio de entrada, resultado (1R o stop, y la parcial
  P1), rango relativo del minuto y lado del VWAP.

## 2. El detector (etapa 1) — qué tan bien acierta el régimen EN VIVO (LOPO)
- Base (cada 5 min, 10:00-15:30): el desplazamiento desde el VWAP siguió hasta el cierre el 43,8 % (41,7 / 43,1 / 52,4 por período).
- **D1 en TENDENCIA (rrn ≥ 1,6):** extremo nuevo (+X/2) antes que el VWAP 60,5 / 61,6 / 66,5 % contra base 36,6 / 37,8 / 40,1; vuelve
  primero solo 8 / 34 / 20 %. Sigue al cierre 49,1 / 48,9 / 74,2 %. (Parte del primer paso es geometría: en TENDENCIA el precio suele estar
  cerca del extremo; por eso la vara principal fue la del cierre.)
- **ROTACIÓN (1,0-1,6) y ESTRECHO (≤ 1,0):** vuelven primero 52-71 % y 55-74 %, apenas sobre la base salvo en la guardada.
- **Por tipo de día:** en las 11 ruedas que terminaron en tendencia (eficiencia ≥ 0,6) el semáforo pasó a TENDENCIA entre 10:00 y 15:30
  en 8 (diseño 3/5, confirmación 3/3, guardada 2/3) y ocupó 33 / 87 / 60 % de sus minutos; en las 18 de balance apareció en 4 y ocupó
  1 / 4 / 11 % de los minutos. En diseño llegó tarde (primer TENDENCIA mediano 11:03; en confirmación 9:59).
- Los otros: D3 (puntos de Dalton) t +1,50; D2 (ruido sostenido) +1,07; D4 (logística) +1,02 en LOPO y +2,08 en LODO; **D0 (#19, la
  eficiencia de las 10 previas) −0,53: confirmado que no sirve.** Ninguno pasó la condición del placebo en 2 de 3 pliegues.

## 3. La estrategia pre-registrada (etapa 2)
| Período afuera | Elegidas en entrenamiento | Ops (por rueda) | Acierto | Neto c0,50 (t) | c1,00 | Placebos PR / PD |
|---|---|---|---|---|---|---|
| Diseño | BALANCE ninguna; TENDENCIA T1-S6 | 30 (1,15) | 56,7 % | +0,01 (0,01) | −0,49 | 0,085 / 0,18 |
| Confirmación | V4-S5 + T3-P2 | 48 (2,29) | 45,8 % | −1,10 (−1,32) | −1,60 | 0,54 / 0,73 |
| Guardada | V5-P3 + T1-S6 | 26 (3,25) | 65,4 % | +2,25 (1,13) | +1,75 | 0,13 / 0,03 |
| Juntas (55) | | 104 (1,89) | 53,8 % | +0,05 (0,08) | −0,45 | |
- LODO: 123 ops 58,5 % +0,41 (t 0,71); TENDENCIA 88 ops 60,2 % +0,76 (55,2 / 61,1 / 65,2 % por período); BALANCE 35 ops 54,3 % −0,48.
- **¿Abre la puerta la llave?** Para la continuación, sí: T1-S6 en diseño 45,4 % → 56,7 % con la llave, en la guardada 55,0 % → 68,0 %.
  Para la vuelta al valor, no con ese mapa: en estado "chico" casi no hay extensiones (V1/V2 con 1-9 operaciones) y la banda pierde.
- La elección dentro del pliegue se equivocó por ruido: en la confirmación eligió T3-P2 (79 % con 24 operaciones de entrenamiento) en vez de
  T1-S6 (62 % con 55), que en la confirmación daba 75 %. Robustez: con la llave D3 41,7 %, con D4 52,7 %.

## 4. La evolución post hoc (etapa 3) y su vecindario (etapa 4) — NO es validación
Mapa nuevo (teoría de subasta): TENDENCIA → continuación; ROTACIÓN (el viejo DUDOSO, rango normal) → banda de 2σ; ESTRECHO → esperar.
| Regla | Diseño | Confirmación | Guardada | Juntas (55) |
|---|---|---|---|---|
| **COMB-T7+V4** | 48 ops 60,4 % +0,43 | 47 ops 63,8 % +1,15 | 27 ops 66,7 % +2,67 | 122 ops 63,1 % +1,20 (t 2,12; 25/55 ruedas +) |
| — su parte TENDENCIA (T7) | 21 ops 42,9 % −1,27 | 32 ops 68,8 % +1,55 | 20 ops 65,0 % +2,48 | |
| — su parte ROTACIÓN (V4) | 27 ops 74,1 % +1,75 | 15 ops 53,3 % +0,30 | 7 ops 71,4 % +3,21 | |
| T6-P1 (T1 + delta 5 min a favor, parcial) | 20 ops 60,0 % +0,42 | 32 ops 62,5 % +1,75 | 22 ops 72,7 % +3,38 | 74 ops 64,9 % +1,88 (t 2,28) |
| T1-S6 (control) | 30 ops 56,7 % +0,01 | 40 ops 75,0 % +2,26 | 25 ops 68,0 % +2,75 | 95 ops 67,4 % +1,68 (t 2,19) |
- COMB con costo 1,00: −0,07 / +0,65 / +2,17. Placebo PR (estado sorteado entre ruedas) p 0,41 / 0,075 / 0,015; PD (lado al azar) p 0,04 /
  0,02 / 0,015. Racha perdedora máxima 3 / 4 / 2; caída máxima 22 / 26 / 21 pts.
- **Elección anidada LOPO entre las 8 reglas nuevas:** diseño afuera → T6-P1 (60,0 %, +0,42); confirmación afuera → V4-ROT (56,2 %);
  guardada afuera → V4-ROT (75 %, 8 ops). Juntas 44 ops 61,4 % +1,06 (t 1,09): **el procedimiento no la encuentra solo en la confirmación.**
- **Vecindario del corte:** la meta en los 3 períodos aparece en 2 de 9 cortes para COMB (1,6/1,0 y 1,8/1,0) y 3 de 9 para T6-P1 → filo de
  cuchillo para el 60 %. El SIGNO sí es zona: juntas, COMB 55,6-63,7 % con neto +0,20 a +1,20 en los 9 cortes; T6-P1 58,8-64,9 %, +0,90 a +1,88.
- GEX (ronda 20, dato ≤ 60 min): T1 en TENDENCIA con gamma negativa 57,3 % (330) contra positiva 45,2 % (42); en confirmación 56,8 contra
  28,6; en diseño al revés (51 contra 79, n 14); la guardada tiene GEX en 4 ruedas. Filtrar por gamma no salva el diseño (52,9 %).

## 5. Mecánica: qué separa ganadoras de perdedoras en cada régimen (evento por evento, 55 ruedas; signo por período d/c/g)
**TENDENCIA — la continuación (T1, retroceso de la ola a favor de la marea con el VWAP detrás, 1R):** 52,8 / 53,8 / 54,9 % en TENDENCIA
contra 44-47 % en los otros dos estados: la llave separa en los 3 períodos, pero el piso sin más piezas es ~54 %. Lo que la sube:
- **el extremo del retroceso en un nivel** (banda del VWAP, redondo de 50, ayer, Asia/Londres, IB): 63,4 % contra 51,9 % (IC 1,01-1,48, +++);
- el delta de 5 min a favor (el retroceso fue pasivo, nadie empujó en contra con agresión): 58,8 contra 50,0 (+++);
- barrida de un nivel y vuelta: 59,5 contra 52,2 (+++); marea empujada por agresores: 55,5 contra 50,0 (+++);
- con iniciativa en el último minuto (T3), la velocidad de 30 min ≥ 1,5: 60 contra 31 % (+++), y la marea con agresores 63 contra 37 (+++).
Lo que la baja: **el precio que lleva 30 min seguidos fuera del ruido** (tendencia madura) 51,8 contra 65,3 % (IC 0,66-0,96, −−−); entrar a
más de 2σ del VWAP 56,7 % contra 72,7 % entre 1,5 y 2σ; la franja 10:30-11:30 (50-55 %); los días extremos (rango ≥ 4 veces lo normal, 05-11):
el R sale de X, que mide las 10 ruedas previas, y el stop queda adentro del ruido de ese día.
**ROTACIÓN — la banda de 2σ con iceberg (V4):** 74 / 58 / 67 % en ROTACIÓN contra 67 / 44 / 33 % en ESTRECHO. En un día dormido el desvío
es chico: tocar la banda no es una extensión de verdad y el balance puede romperse. Pierde cuando el día se convierte en tendencia DESPUÉS
del primer toque (16-10, 16-11): el semáforo necesita que el rango ya se haya estirado, así que llega tarde a la primera ruptura.
**El mismo nivel cambia de signo:** en ESTRECHO, continuar desde un nivel pierde (41,8 % contra 52,6 %, −−−): ahí el nivel es la pared que
frena; en TENDENCIA es el escalón desde donde se vuelve a empujar.
**La vuelta al valor en TENDENCIA** pierde (46 %), salvo con big trades netos a favor: 61,8 % contra 41,8 % (IC 1,13-1,94, +++). Mismo
sentido que la R19: el que defiende CON TAMAÑO es el único que frena una pierna de tendencia.

## 6. Operaciones ejemplares (hora NY, precio ES; todas de COMB-T7+V4 salvo las del final)
**TENDENCIA**
1. 13-11 10:13:15 VENTA 7.543,25 (extremo 7.550,00; R 7,25), rango 2,34 veces lo normal, el redondo 7.550 en la zona, delta de 5 min +1.052
   a favor, VWAP 49,6 pts arriba: **1R +7,25**. Esa mañana: 5 de 5 entre 10:01 y 10:20.
2. 09-11 10:01:45 COMPRA 7.524,25 (extremo 7.519,25; R 5,50), rango 2,45, banda +1σ en la zona y barrida, delta +445: **+5,50**; 4 de 5 hasta 11:07.
3. 19-11 10:34:20 VENTA 7.322,75 (extremo 7.330,75; R 8,50), rango 1,82, banda −2σ barrida 0,72 pts y vuelta, delta +2.343: **+8,50**.
4. Perdedora, día extremo: 05-11 10:02:00 VENTA 7.581,25 (extremo 7.586,00; R 5,25) con el rango 4,85 veces lo normal: **stop −5,25**. La
   mañana dio 3 de 5 con R de 5 pts en un día de 335: el stop vivía adentro del ruido.
5. Perdedoras tarde: 19-11 10:39:10 y 10:40:37 VENTAS en 7.302,00 y 7.306,25, ya 81 y 75 pts debajo del VWAP (−2,2σ y −1,9σ): **−9,00 y −8,25**.
**ROTACIÓN**
6. 24-09 12:25:15 VENTA 7.762,00 en la banda +2σ (barrida 0,77 y vuelta), rango 1,27, iceberg: **+4,00**; 13:05 y 13:15 también (+3,75, +5,00).
7. 10-11 10:50:24 COMPRA 7.509,75 (extremo 7.503,75; R 6,50) a −1,98σ, absorción en el bid + iceberg, rango 1,12: **+6,50** (y 10:56, 12:47).
8. 20-11 10:20:25 COMPRA 7.150,50 (extremo 7.142,25; R 8,75) en −2σ / −1σ de jornada con barrida y absorción, rango 1,29: **+8,75**. Ese día
   terminó con 318,75 pts de rango y cerró 225 pts debajo de la apertura: a la mañana el rango todavía era normal y la banda aguantó.
9. Perdedora, el día que se volvió tendencia: 16-10 10:08:46 COMPRA 7.782,75 (−2σ de jornada, rango 1,02): **stop −4,75**; 11:46 otra −4,75.
   A las 12:08 el semáforo ya estaba en TENDENCIA (1,65) y la estrategia vendió: −4,50, −4,75, +4,25. Rueda negativa.
**Régimen equivocado (no las toma la COMB)**
10. Vuelta al valor en TENDENCIA: 20-10 10:00:03 VENTA 7.744,75 en +2σ de jornada con big trades a favor (rango 1,96): stop −4,00; 10:14:35
    VENTA 7.756,50 en +2σ: stop −4,50 sin ir nunca a favor. Esa misma mañana la COMB compró los retrocesos: +3,25, +3,75, +4,00, −3,75, +4,00.
11. Continuación en ESTRECHO: 22-09 14:05:42 COMPRA 7.836,75 sobre el VWAP, el de jornada y el cierre de ayer (rango 0,61): stop −3,75.

## 7. Candidata para el juez en vivo — especificación exacta (EN PRUEBA; elegida mirando: el vivo decide)
Como LECTURA en un indicador con registro (semáforo + señal), nunca como orden automática. Todo con lo anterior a cada instante.
- **Escala:** med10 = mediana del rango RTH (9:30-16:00) de las 10 ruedas previas; X = 0,08 × med10 al tick. Ola = zigzag de X, marea = 4X,
  orden por orden desde las 18:00 NY.
- **Semáforo, al cierre de cada minuto:** rrn = (máximo − mínimo RTH hasta ahora) / (med10 × f(m)), con f(m) de `curva_rango_por_minuto.json`
  (10:00 0,433; 10:30 0,562; 11:00 0,673; 11:30 0,759; 12:00 0,816; 12:30 0,851; 13:00 0,890; 13:30 0,942; desde 14:00 1,000).
  TENDENCIA si rrn ≥ 1,6 y el precio está del mismo lado del VWAP RTH y de la apertura (ese es el lado); si los lados no coinciden, no se
  opera. ROTACIÓN si 1,0 < rrn < 1,6. ESTRECHO si rrn ≤ 1,0: no se opera. Una señal en el segundo t usa el estado del último minuto cerrado.
- **TENDENCIA → T7:** en el segundo en que la ola se da vuelta (el precio volvió X desde el extremo del retroceso) a favor de la marea, del
  lado de la TENDENCIA, con el VWAP RTH detrás, y al menos una de: (a) el extremo del retroceso en la zona de un nivel (de L − X/4 a
  L + X/8 del lado del retroceso; niveles: VWAP RTH y de jornada con ±1σ y ±2σ, semanal, mensual, ayer máximo/mínimo/cierre/POC/VAH/VAL,
  Asia y Londres máximo/mínimo, balance inicial y sus extensiones de 0,5 y 1 vez, POC/VAH/VAL de la semana anterior, redondo de 50);
  (b) el extremo perforó un nivel hasta X/2 y el precio ya volvió del otro lado; (c) delta de los últimos 300 s a favor.
- **ROTACIÓN → V4:** en la vuelta de la ola, COMPRA si el extremo del retroceso quedó entre la banda −2σ (VWAP RTH o de jornada) y X/2 por
  encima, sin perforarla, y en los 60 s previos hubo un iceberg en el bid a ≤ X/2 del extremo (una venta agresiva que se comió más de lo que
  mostraba el bid y el bid quedó en el mismo precio con tamaño); VENTA en espejo con +2σ e iceberg en el ask.
- **Gestión:** entrada al precio de ese segundo; stop = extremo del retroceso ∓ 2 ticks (R ≈ X + 2 ticks); objetivo 1R con orden limitada
  (se cuenta llena al pasarla 1 tick). 10:00:00-15:29:59 NY, una posición por vez, máximo 5 por rueda, todo cerrado a las 15:59:59.
- **Bitácora (15 sesiones, sin tocar parámetros):** cada señal con hora, precio, estado y rrn, familia, piezas (nivel / barrida / delta /
  iceberg), R, si se tomó, resultado a 1R, recorrido máximo a favor y en contra. **Juez:** acierto ≥ 60 % y neto > 0 con costo 0,50 sobre
  todas las señales de las 15 sesiones, y por familia. Medición secundaria (no decide): la versión T6-P1 (T1 + delta de 5 min a favor;
  mitad en 1R y el resto en 2R con stop en la entrada).

## 8. Qué falta
- **Distinguir "día expandido que sigue" de "día expandido que vuelve":** en diseño la continuación con la llave dio 43 % (09-16 día de
  la Fed, 10-01, 10-14: rango grande y cierre en el medio). Falta una segunda pieza que confirme la tendencia sin llegar tarde.
- **Un stop a la escala del día:** en días de rango ≥ 3-4 veces lo normal el R de 10 ruedas queda adentro del ruido. Sin medir.
- **La primera ruptura:** la ROTACIÓN pierde el día que se vuelve tendencia después de las 10:00 (el semáforo llega tarde por construcción).
- **Muestra:** 11 ruedas de tendencia en total; GEX en solo 4 de las 8 de la guardada. El juez es el vivo.

## 9. Notas de honestidad
- La etapa 2 fue la única pre-registrada con elección dentro del pliegue: NO cumplió. Las etapas 3 y 4 se diseñaron después de verla y de la
  mecánica de las 55 ruedas; sus números por período no son validación. Las finalistas de la R19 (menú de BALANCE) salieron del diseño: el
  pliegue "diseño afuera" está contaminado por esa elección.
- Dos encabezados del pre-registro tenían la hora escrita a ojo (04:52 y 05:08); se corrigieron con notas nuevas (04:47 y 04:55), sin tocar
  lo escrito, y siempre antes de correr lo que pre-registraban.
- t por rueda de la candidata 2,12 contra Bonferroni 3,45 (91 variantes) o 3,66 (173 con las corridas de robustez): no es significativa por
  la búsqueda múltiple.

## 10. Entregables
- `PREREGISTRO.md`, `RESULTADOS.md`, `registro_variantes.csv`, `curva_rango_por_minuto.json`.
- `salidas/`: `detector.txt/.json`, `estrategia-D1/D3/D4.txt/.json`, `mecanica.txt`, `etapa3.txt/.json`, `vecindario.txt`, `ejemplos.txt`,
  `ops-r21-LOPO-*.csv`, `ops-r21-LODO-D1.csv`, `ops-r21-etapa3.csv`, `ops-COMB-T7+V4-detalle.csv` (las 122 operaciones con sus piezas).
- Scripts `r21_comun.py`, `r21_01_regimen.py` … `r21_08_ejemplos.py`. Intermedios en `scratchpad\r21` (`regimen_minuto.parquet`,
  `estados_minuto.parquet`, `eventos_r21.parquet`, `cruces_r21.parquet`, `eventos_cruces_guardada.parquet`).

## 11. La candidata CONGELADA en muestra nueva: MNQ (27-11, 05:15-05:23 AR) — NO PASÓ
- **Congelamiento antes de mirar** (PREREGISTRO, enmienda 5): `regla_congelada_comb_t7_v4.py` (SHA-256 dd8e18ad…cb8a) más la curva y los 7
  archivos del pipeline, con sus huellas en `regla_congelada_huellas.json`. Antes de congelar reprodujo exacto las 122 operaciones de ES.
  Única aclaración escrita antes de mirar: con rango ≥ 1,6 y lados en desacuerdo no se opera (en ES no cambió ninguna operación).
- **Muestra:** cintas de MNQ nunca usadas (31-07 a 14-09 en U6, 15 a 21-09 en Z6; spread del roll medido +298,00). Calentamiento: las 10
  primeras ruedas. Evaluadas: 25 (14-08 a 21-09; el 17-09 afuera por cinta incompleta). Mismo pipeline: X, umbrales y ruido con las 10
  previas de MNQ, la misma curva por minuto. Costos en puntos de NQ. Comprobación desde la cinta cruda: 6 de 6.
- **Resultado (corrido una vez):** 46 ops (1,84 por rueda), acierto **52,2 %**, neto c0,50 **−0,13** (t −0,04), c1,00 −0,63; 7 de 25 ruedas
  positivas. TENDENCIA (T7) 29 ops 58,6 % +2,47; ROTACIÓN (V4) 17 ops 41,2 % −4,56. Placebo de lado al azar p 0,45; de régimen sorteado p 0,35.
- **Por fechas:** en las 18 ruedas estrictamente anteriores al 10-09 (sin ninguna fecha en común con ES): 40,7 %, −4,94 (T7 43,8 %, V4
  36,4 %). En las 7 del 10 al 21-09 (las mismas fechas que el diseño de ES): 68,4 %, +6,72. Lo bueno de MNQ vino de días que la regla ya
  "conocía" por el ES.
- **¿Se cayó el flujo y quedó la estructura?** No: se cayeron las dos. La banda sin iceberg da 47,7 % (con iceberg 41,2 %: en MNQ el iceberg
  no suma, como avisaba el coordinador), y la continuación con régimen + estructura sola (T1 en TENDENCIA, sin la confluencia) da 57,6 % en
  las 25 y 44,4 % en las 18 limpias.
- **Mecánica de por qué:** a mediados de agosto la mediana de 10 venía inflada por las ruedas de 650-750 pts de fines de julio, así que el
  semáforo vio casi todo "dormido" (ESTRECHO 46,5 % de los minutos, TENDENCIA 9,7 % contra ~18 % en ES; el 14 y el 17-08 fueron ESTRECHO todo
  el día) y el R salido de esa mediana (X 34 pts de NQ el 14-08, ~21 desde el 21-08) quedó grande para el movimiento real. La regla mide el
  día contra los 10 anteriores: cuando esos 10 eran de otro clima, el cuentakilómetros marca mal. Es una pista para el baúl, NO un ajuste.
- **Estado:** "no pasó en MNQ". No se ajusta ni se vuelve a correr. Queda en pie solo la prueba en vivo con ES, como juez final.

## 12. La prueba en vivo con ES (armada; empieza el lunes 30-11-2026)
- **Script:** `r21_11_vivo.py` (SHA-256 0bad8775…6809). Por cada cinta nueva de ES posterior al 25-11 verifica las huellas de la regla, corre el
  pipeline congelado y agrega las operaciones a `bitacora_en_vivo.csv` (fecha, hora, régimen, regla, lado, entrada, stop, objetivo,
  resultado, motivo, rango relativo, R, X, neto con 0,50 y 1,00, las piezas y la versión de la regla) y la jornada a `bitacora_jornadas.csv`.
- **Uso:** `python -I r21_11_vivo.py` después de bajar la cinta del día con la sonda (procesa todas las pendientes, en orden; no repite).
  `python -I r21_11_vivo.py --verificar AAAA-MM-DD` recorre una jornada ya conocida sin escribir la bitácora.
- **Verificado de punta a punta desde la cinta cruda** con la historia hasta la víspera: 25-11 (5 de 5 operaciones idénticas a la etapa 3) y
  20-11 (5 de 5, cuatro de V4 y una de T7).
- **Exclusiones por regla:** 26-11 (feriado) y **27-11 (rueda corta)** ya anotadas en `bitacora_jornadas.csv`; 24-12 también. Cinta incompleta
  (última orden de la rueda antes de las 15:59 o hueco > 300 s): no se evalúa. **Roll de diciembre:** desde el 07-12 cada cinta necesita su
  contrato confirmado en `vivo_contratos.json` y, si pasa de Z6 a H7, el spread medido; si no, la jornada queda afuera (no se inventa).
- **Juez:** 15 sesiones; acierto ≥ 60 % y neto > 0 con costo 0,50, sin tocar nada. Con lo que mostró MNQ, la expectativa honesta es baja.
- Entregables de las secciones 11-12: `regla_congelada_comb_t7_v4.py`, `regla_congelada_huellas.json`, `r21_09_mnq_inventario.py`,
  `r21_10_mnq_prueba.py`, `r21_11_vivo.py`, `bitacora_en_vivo.csv`, `bitacora_jornadas.csv`, `vivo_contratos.json`; `salidas/mnq_inventario.csv`,
  `salidas/mnq_prueba.txt/.json`, `salidas/ops-MNQ-COMB-T7+V4.csv`. Intermedios de MNQ en `scratchpad\r21\mnq`; estado del vivo en
  `%LOCALAPPDATA%\PythiaGex_r21_vivo`.
