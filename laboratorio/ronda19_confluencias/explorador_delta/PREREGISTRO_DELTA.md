# Ronda 19 — Explorador DELTA (familia e: delta y flujo agresivo en el extremo) — PRE-REGISTRO

Escrito el **27-11-2026 a las 03:45 AR**, ANTES de mirar un solo resultado de esta familia. Lo único mirado antes: el diccionario,
el pre-registro general, las etapas 1-4 del agente principal (ya publicadas) y frecuencias/correlaciones de los rasgos SIN resultados
(`d01_describir.py`). Base: solo `scratchpad\r19\eventos_diseno.parquet` (2.250 vueltas, 26 ruedas). `eventos_todos.parquet` no se abre.

## Teoría (por qué el delta podría aportar donde la R17 dijo que no)
La R17 midió que el delta ACOMPAÑA a la pierna mientras la pierna corre (86-99 %): leído a tiempo no separa giro de pausa. Acá el
evento ya es una vuelta confirmada (el precio volvió X desde el extremo); la pregunta es si el flujo del FINAL de la pierna que murió y
el del arranque de la nueva dicen si la vuelta sigue 1-2 R o vuelve al extremo. Cuatro mecanismos con teoría:
1. **Sin combustible en el extremo:** divergencia (F3: extremo nuevo con MENOS delta agresivo que el anterior), agotamiento (F4) y
   clímax absorbido (`veto_caudal`: venta/compra de 30 s ≥ p99,5 en el extremo, acá leído como SEÑAL de capitulación, no como veto).
2. **Esfuerzo contra resultado (Wyckoff):** delta agresivo por punto del retroceso (`del_ret_por_pt` = delta del retroceso con su signo /
   puntos recorridos). Alto = mucho esfuerzo y poco recorrido = el pasivo absorbió. Bajo o negativo (`ret sin agresores`) = retroceso
   pasivo, nadie empuja en contra.
3. **Iniciativa del lado nuevo:** delta de 60 s a favor sobre volumen (`d60_ratio`), giro del delta (`giro_delta`: 300 s en contra y
   60 s a favor), big trades netos a favor (F6 / `netbig60_a_favor`), imbalances netos a favor en 3 velas (`imb_neto_3v`), apilados.
4. **Defensa del extremo:** recargas (icebergs) del lado que defiende por encima de las del otro (`ice_neto_60` > 0).
Contexto mínimo con sentido teórico en un período "va y vuelve" (21/26 ruedas): **hacia el valor** (VWAP RTH del lado de la ganancia a
≥ 1 R: `vwap_rth_dist_pts ≤ −R`), **nivel** (extremo en la zona de ≥ 1 nivel), **marea** (a favor o en contra: en contra = la vuelta va
contra el último empujón de la marea).

## Reglas fijas de simulación
10:00-15:30 NY (`seg_rueda` 1800-21599), una posición por vez por rueda (la entrada siguiente después de `<salida>_seg_salida`),
máximo 5 por rueda, costo 0,50 y 1,00 pt. Acierto = bruto > 0. t POR RUEDA (26 ruedas, las sin operaciones = 0). Cada regla × salida
se anota en `registro_variantes_delta.csv`. Umbrales fijados ACÁ (cuantiles de la muestra de diseño SIN resultados o valores naturales):
`d60_ratio ≥ 0,10` (≈ p75), `del_ret_por_pt ≥ 137` (≈ p75) y `≤ 49` (≈ p25), `imb_neto_3v > 0`, `ice_neto_60 > 0`, `netbig60_a_favor > 0`.

## Etapa D0 (un rasgo, sin contexto) — salidas S1 (2R) y S6 (1R)
F3; F4; veto_caudal (como señal); F1; F6; ice_neto_60 > 0; giro_delta; d60_ratio ≥ 0,10; ret sin agresores; del_ret_por_pt ≥ 137;
del_ret_por_pt ≤ 49; imb_neto_3v > 0; apilados_favor_3v ≥ 1; netbig60_a_favor > 0. Más las ablaciones de contexto solo: todas las
vueltas, hacia el valor, nivel, a favor de la marea, en contra de la marea.

## Etapa D1 (2-3 condiciones; salidas S1, S6 y, si va hacia el valor, S5)
e1 F3 + nivel · e2 F3 + hacia el valor · e3 F3 + contra la marea · e4 clímax + hacia el valor · e5 esfuerzo alto + nivel ·
e6 retroceso sin agresores + a favor de la marea · e7 giro_delta + big netos a favor · e8 giro_delta + hacia el valor ·
e9 d60_ratio ≥ 0,10 + hacia el valor · e10 imb_neto_3v > 0 + hacia el valor · e11 F3 + giro_delta · e12 F3 + F6.

## Evoluciones (D2, D3)
Se pre-registran abajo, con hora, ANTES de correrlas: qué cambia y por qué. Hasta 4 condiciones en total.

## Cómo se juzga
Vara doble (acierto y neto con costo 0,50 y 1,00), t por rueda, mejor y peor rueda; corrección por el TOTAL de variantes de diseño de
todos los `registro_variantes*.csv` (Bonferroni una cola 5 %/N, gl 25, y Deflated Sharpe); placebo F (el rasgo de flujo barajado entre
eventos de la misma rueda, contexto fijo, 500 sorteos) y placebo H (cada operación cambiada por una vuelta al azar de la misma rueda a
≤ 10 min, cualquier lado, 500 sorteos). Mecánica con 3-5 operaciones reales.

---
## Resultado D0-D1 (27-11 03:50 AR) — 67 variantes anotadas
- Línea de base 10:00-15:30 (una posición por vez): S1 30,5 % / −0,09 R; S6 52,3 % / +0,05 R.
- **Lo que pega AL REVÉS (señal de que la vuelta FALLA):** F3 divergencia S1 23,5 % (−0,30 R, t −2,6); F3 + nivel 20,6 % (t −3,4);
  F3 + giro 13,9 %; retroceso sin agresores S6 36,6 % (t −4,5); apilados a favor S1 20,4 % (t −3,8); big netos a favor > 0 S1 25,2 %
  (t −3,2); imbalances netos a favor S1 25,8 % (t −2,8).
- **Lo que suma:** absorción F1 S1 39,2 % (+0,17 R); icebergs netos del lado que defiende S1 36,3 %; d60_ratio ≥ 0,10 + hacia el valor
  S6 59,8 % (+0,20 R, t +0,94); giro + hacia el valor S6 57,8 %.
- Lectura: manda el PASIVO. Cuando el precio va contra el delta agresivo (F3, retroceso sin agresores) el que mueve es el libro y la
  vuelta contra él falla; cuando la vuelta la hace una ráfaga agresiva (apilados, big netos) el pasivo la absorbe. Cuando el pasivo
  DEFIENDE el extremo (absorción, icebergs) la vuelta sigue.

## Etapa D2 — pre-registrada 27-11 03:52 AR, ANTES de correrla
1. **Tamizado por tercios** (cortes = tercios de cada rasgo en las vueltas de 10:00-15:30, sin resultados) de los rasgos de flujo:
   of_abs_lado_pasivo_rel, of_icebergs_60, ice_neto_60, d60_ratio, of_delta_60, of_delta_300, netbig60_a_favor, imb_neto_3v,
   del_ret_por_pt, seg_desde_ext; salidas S1 y S6. Cada tercio × salida se anota (30 reglas × 2 = 60 variantes).
2. **Fórmulas "manda el pasivo"** (2-4 condiciones), salidas S1, S6, S2 y S4b:
   p1 F1 + no F3 · p2 F1 + hacia el valor · p3 (F1 o ice_neto>0) + hacia el valor · p4 (F1 o ice_neto>0) + hacia el valor + no F3 ·
   p5 (F1 o ice_neto>0) + big_neto ≤ 0 · p6 hacia el valor + d60_ratio ≥ 0,10 + no F3 · p7 hacia el valor + d60_ratio ≥ 0,10 +
   retroceso CON agresores · p8 F1 + retroceso con agresores + no F3 + hacia el valor.

## Resultado D2 (27-11 03:58 AR) — 60 + 32 variantes más
- Tercios: el flujo agresivo A FAVOR de la vuelta en el último minuto pega al revés: imbalances netos a favor (tercio alto) S1 24 % y
  S6 40 %; d60_ratio alto S1 26 %; big netos a favor (tercio alto) S1 29 %. Con la agresión EN CONTRA (big netos en contra, tercio bajo)
  S1 37,9 % (+0,14 R). Rebote de 27-100 s S1 22 %; rebote lento (> 100 s) S6 55 %.
- "Manda el pasivo" (F1/icebergs) suma poco al acierto; mejor p7 = hacia el valor + d60 ≥ 0,10 + retroceso con agresores: S6 60,2 %
  (+0,20 R, t +1,14), S4b +0,32 R.
- Contradicción aparente: el delta a favor es MALO en general y BUENO hacia el valor.

## Etapa D3 — pre-registrada 27-11 04:00 AR, ANTES de correrla: "responsivo contra iniciativa" (Dalton)
Teoría: en balance (va y vuelve) la actividad RESPONSIVA (comprar debajo del valor, vender arriba) gana y la de INICIATIVA (comprar
arriba del valor, vender abajo) se deshace. La misma ráfaga agresiva a favor de la vuelta debería ser buena si la vuelta va HACIA el
VWAP y mala si se aleja. Prueba: cada rasgo de flujo agresivo × {hacia el valor (VWAP a ≥ 1R del lado de la ganancia), lejos del valor
(VWAP del lado del stop: vwap_rth_dist_pts > 0)}; salidas S1 y S6.
Flujos a favor: d60_ratio ≥ 0,10; imb_neto_3v > 0; netbig60_a_favor > 0; F6; giro_delta. Flujos en contra (agresión absorbida):
netbig60_a_favor < 0; d60_ratio ≤ 0; imb_neto_3v < 0. (8 × 2 contextos × 2 salidas = 32 variantes.)

## Resultado D3 y mapa de la señal (27-11 04:08 AR)
- D3 (32 variantes): Dalton se cumple en parte: big netos a favor hacia el valor S6 57,7 % contra 50,0 % lejos; giro 57,8 % contra 43,9 %;
  d60 alto 59,8 % contra 54,6 %. Los imbalances a favor son malos de los dos lados (41 %). La agresión absorbida suma LEJOS del valor
  (imbalances en contra + lejos S1 38,6 %) y resta hacia el valor.
- Mapa con validación dejando una rueda afuera (d05/d05b, evento por evento, NO es una regla: no se anota como variante): AUC dentro de
  rueda 0,55 para S6 con árboles de flujo; un rasgo por vez el más fuerte es la distancia al VWAP en R (AUC 0,553 S6 / 0,558 S1, más
  hacia el valor = más acierto); después imbalances a favor (0,461 → al revés), niveles en zona (0,458 → al revés), apilados a favor y F3
  (0,470 → al revés), d60_ratio (0,526 a favor). Señales débiles: hace falta combinar y aceptar n chico.

## Etapa D4 — pre-registrada 27-11 04:10 AR, ANTES de correrla
1. Perfil del contexto: distancia al VWAP en R por tramos (≤ −3, −3..−2, −2..−1, −1..0, > 0), S1 y S6 (10 variantes; ablación).
2. Fórmulas "responsivo sin ráfaga" (2-4 condiciones), salidas S1, S6, S2, S4b:
   r1 VWAP a ≥ 2R hacia el valor + d60_ratio ≥ 0,10 · r2 VWAP ≥ 2R + imb_neto_3v ≤ 0 · r3 VWAP ≥ 2R + d60 ≥ 0,10 + imb ≤ 0 ·
   r4 VWAP ≥ 1R + d60 ≥ 0,10 + imb ≤ 0 · r5 VWAP ≥ 1R + d60 ≥ 0,10 + imb ≤ 0 + no F3 · r6 VWAP ≥ 1R + big netos a favor > 0 + imb ≤ 0 ·
   r7 VWAP ≥ 1R + d60 ≥ 0,10 + sin niveles en zona · r8 VWAP ≥ 2R + d60 ≥ 0,10 + imb ≤ 0 + no F3.

## Resultado D4 (27-11 04:16 AR) — 10 + 32 variantes
- El perfil de la distancia al VWAP con una posición por vez NO es monótono (S6 43-54 %): la señal evento por evento se diluye.
- Lo mejor sigue en S6 ≈ 60-62 % con +0,2 R bruto (r7 VWAP ≥ 1R + d60 ≥ 0,10 + sin niveles: 62,0 %, neto +0,53, t +1,15; r1 VWAP ≥ 2R +
  d60: 61,6 %). Techo de la vuelta a favor ≈ 60 %: no es el salto.
- Asimetría: los peores subconjuntos están MÁS lejos del 50 % que los mejores (retroceso sin agresores S6 36,6 %, F3 + giro 32,4 %,
  apilados 32,1 %, pasivo + a favor de la marea 31,0 %). La información fuerte del delta está en detectar la vuelta FALSA.

## Etapa D5 — pre-registrada 27-11 04:20 AR, ANTES de correrla: "la vuelta falsa" (se opera AL REVÉS de la vuelta)
- **Salida nueva S6m = espejo exacto de S6** (sin procesar cintas, derivada de S6 con 1 tick conservador por lado): lado contrario a la
  vuelta, misma entrada; objetivo = el extremo ∓ 1 tick (se llena cuando la vuelta toca su stop, ext ∓ 2 ticks); stop = entrada ± (R +
  1 tick) (donde se llena el 1R de la vuelta). Gana R − 1 tick si S6 fue "stop", pierde R + 1 tick si fue "objetivo", −S6 si fue "fin";
  sale en el mismo segundo que S6. **Si una regla con S6m llega a finalista, la confirmación debe recalcular la operación contraria desde
  la cinta** (la regla general pide salidas calculadas desde la cinta; acá es una derivación exacta salvo el tick).
- Teoría: si el retroceso lo hizo el PASIVO (precio contra el delta agresivo: F3 o retroceso sin agresores) y/o la vuelta la hizo una
  RÁFAGA agresiva (apilados, imbalances netos a favor, big netos a favor), el lado pasivo sigue al mando y el precio vuelve al extremo.
- Reglas (10:00-15:30, una posición por vez, máx. 5; salida S6m):
  f1 retroceso sin agresores · f2 F3 · f3 apilados a favor ≥ 1 · f4 imb_neto_3v > 1 (tercio alto) · f5 retroceso pasivo (F3 o sin
  agresores) · f6 retroceso pasivo + imb_neto_3v > 0 · f7 (apilados ≥ 1 o imb_neto_3v > 1) + lejos del valor · f8 F3 + nivel ·
  f9 big netos a favor > 57 (tercio alto) + lejos del valor.
- Placebo A ahora posible para S6/S6m: mismas vueltas, lado sorteado (S6 o S6m), 500 sorteos.

## Resultado D5 (27-11 04:24 AR) — 9 variantes (S6m)
- f3 apilados a favor de la vuelta → al revés: 56 operaciones (2,15 por rueda), acierto 67,9 %, neto +0,82 (0,50) y +0,32 (1,00), t +1,47,
  placebo A p 0,004. f1 retroceso sin agresores 63,4 % (p 0,04); f5 pasivo 62,2 % (n 111, p 0,008); f8 F3 + nivel 62,3 % (p 0,024).
  Estables en las dos mitades del diseño (f3 66,7 / 70,0 %; f5 62,5 / 61,8 %) y en los dos lados. Big netos altos + lejos NO sirve (52,5 %).

## Etapa D6 — pre-registrada 27-11 04:27 AR, ANTES de correrla
Fade = operar al revés de la vuelta con S6m; tomar = a favor con S6 (y S1/S2/S4b donde aplica). "pasivo" = F3 o retroceso sin agresores.
v1 fade apilados & pasivo · v2 fade apilados o (F3 & nivel) · v3 fade apilados o retroceso sin agresores · v4 fade apilados o pasivo ·
v5 fade (apilados o pasivo) & NO hacia el valor (VWAP a menos de 1R del lado de la ganancia o del lado del stop) ·
v6 DOBLE: fade si (apilados o pasivo); si no, tomar si VWAP ≥ 1R + d60 ≥ 0,10 + imb ≤ 0 (S6/S6m) ·
v7 DOBLE: fade si (apilados o pasivo); si no, tomar si VWAP ≥ 1R + d60 ≥ 0,10 (S6/S6m) ·
v8 tomar limpia: VWAP ≥ 1R + d60 ≥ 0,10 + sin apilados + no pasivo (S1, S6, S2, S4b).

## Resultado D6 (27-11 04:31 AR) — 11 variantes
- v3 fade (apilados o retroceso sin agresores): n 99 (3,81/rueda), 66,7 %, neto +0,70 / +0,20, t +1,57, placebo A p 0,002.
  v2 fade (apilados o F3 en nivel): n 100, 65,0 %, neto +0,63 / +0,13, t +1,72, p 0,002. Las dobles diluyen (59-60 %). Tomar limpia 61 %.
- Anatomía de v3 (mirada después de correrla, sesgo declarado): vuelta rápida (≤ 38 s) 70,7 % y media (38-107 s) 74,2 % contra lenta
  (> 107 s) 51,9 %; lejos del valor 70,0 % contra hacia el valor 62,5 %; F6 69 % contra 65 %.

## Etapa D7 — pre-registrada 27-11 04:34 AR, ANTES de correrla (elegida mirando la anatomía de v3: decide la confirmación)
Teoría: una vuelta de X hecha en menos de ~2 minutos con ráfaga (apilados) o después de un retroceso pasivo es un PINCHAZO: la hizo la
urgencia de unos pocos agresores y el pasivo que manda la devuelve al extremo (Dalton: la iniciativa lejos del valor se deshace en
balance). La vuelta lenta es acumulación paciente y no se revierte. Cortes = tercios de `seg_desde_ext` fijados en D2 sin resultados.
w1 fade (apilados o sin agresores) + vuelta ≤ 107 s · w2 fade solo vuelta ≤ 107 s (ablación) · w3 fade solo vuelta ≤ 38 s (ablación) ·
w4 fade (apilados o sin agresores) + lejos del valor · w5 fade (apilados o sin agresores) + ≤ 107 s + lejos del valor ·
w6 fade (apilados o F3 en nivel) + ≤ 107 s · w7 fade (apilados o sin agresores o F3) + ≤ 107 s. Todas con S6m.

## Resultado D7 (27-11 04:38 AR) — 7 variantes
- w1 fade (apilados o sin agresores) + vuelta ≤ 107 s: n 79 (3,04/rueda), 70,9 %, neto +1,03 / +0,54, t +2,02, placebo A p 0,000, mitades
  67 / 76 %. w6 fade (apilados o F3 en nivel) + ≤ 107 s: n 78, 70,5 %, neto +1,22 / +0,71, t +2,27, p 0,000, mitades 62 / 82 %.
- Ablación clave: la velocidad SOLA no sirve (w2 49,6 %): el pinchazo necesita la firma del flujo.

## Etapa D8 — pre-registrada 27-11 04:40 AR: ablaciones de w1 y w6 (la fórmula más chica), ANTES de correrlas
a1 fade apilados + ≤ 107 s (2 condiciones) · a2 fade sin agresores + ≤ 107 s (2) · a3 fade F3 + nivel + ≤ 107 s (3). Todas S6m.
Después: juicio final de las 3 mejores (placebo F barajando el rasgo de flujo dentro de la rueda, placebo H misma hora, Bonferroni sobre
el total de todos los registros, Deflated Sharpe, peor y mejor rueda, ejemplos reales). No se prueban más variantes después de D8.

## Resultado D8 y JUICIO (27-11 05:05 AR) — no se probó nada después de D8
- Ablaciones: a1 apilados + ≤ 107 s 68,8 % (n 48, t +1,52); a2 sin agresores + ≤ 107 s 68,8 % (n 48, t +1,96); a3 F3 en nivel + ≤ 107 s
  68,2 % (n 44, t +2,16). Las uniones (w1, w6) suman operaciones sin perder acierto: las huellas caen en vueltas distintas.
- Juicio (`d11_juicio.py`, `juicio_candidatas.json`; números recalculados aparte por `d13_verificar.py`, coinciden):
  w6 n 78 (3,00/rueda) 70,5 %, neto +1,22 / +0,71, t +2,27 / +1,39, sin la mejor rueda t +1,96, mitades 62,2 / 81,8 %; placebo A 50,0 %
  (p < 0,001), placebo F firma barajada 54,2 % (p neto 0,000, p t 0,006), firma + velocidad 52,7 % (p 0,000), placebo H 49,7 % (p 0,001).
  w1 n 79 70,9 %, +1,03 / +0,54, t +2,02 / +1,06, placebos p ≤ 0,004. a2 n 48 68,8 %, +0,82 / +0,32, t +1,96 / +0,76, placebos p ≤ 0,028.
- Corrección por búsqueda: N = 332 variantes de diseño en todos los registros (263 de este explorador + 69 del principal) → t de
  Bonferroni 4,19: NINGUNA pasa en diseño; Deflated Sharpe 0,02-0,04. Evento por evento (todas las vueltas que cumplen, sin la
  restricción de una posición): w6 66,7 % (IC 95 % por rueda 58,3-77,0), w1 65,5 % (56,6-77,8), a2 65,5 %; base 52,9 %.
- Decide la confirmación. S6m se puede calcular igual en `eventos_todos.parquet` (sale de S6), pero lo correcto es recalcular la
  operación contraria desde la cinta para la finalista.
