# Calibrar la Corriente: ¿se puede avisar antes sin sumar ruido? (MES 1 min, 29-10-2026)

Método fijado antes en `PREREGISTRO_CALIBRACION.md` (con la Enmienda 1, post-hoc). Datos: 62 sesiones de MES 1 min,
DISEÑO 45 (08-04..10-05) y EXAMEN 17 (10-06..10-28, nunca usadas). 80 candidatos en 5 familias. Salidas:
`cal_01_salida.txt`, `cal_resumen.csv`, `cal_movimientos.csv` (pre-registrado) y `cal_02_salida.txt` (post-hoc).

## Lo pre-registrado

- La base ST(10,3) reproduce el informe anterior: 114 movimientos de 20 pts, 6,5 min, 13 pts tarde, 36,9 cambios por sesión.
- Sin sumar ruido, el mejor en DISEÑO fue ST(60; 3,5), que mejoró solo 0,38 pts (se pedía 1). En EXAMEN llegó más tarde
  que la base (15,5 contra 13,5 pts; antes en 9 de 26 movimientos distintos, p 0,96). **NO PASA.**
- Las 4 familias que funcionan (SuperTrend con ATR de 5 a 60, fuente cierre, multiplicador por eficiencia y Chandelier)
  caen sobre la misma curva: a igual cantidad de cambios de color, el retraso es el mismo con ±1 pt. Llegar antes cuesta
  más cambios en falso, en una proporción fija. Es lo esperable si nada anticipa la dirección (INFORME.md parte a: 46-50 %).
- F5 (dos cierres) quedó mal definido y casi nunca cambia: no cuenta (Enmienda 1).

## Post-hoc (no pre-registrado; consistente en las dos muestras)

- **ST(60,3), el mismo multiplicador con un ATR lento.** Mismo ruido: en la rueda, 10,2 contra 9,8 cambios por sesión en
  DISEÑO y 9,9 contra 10,1 en EXAMEN; vueltas falsas de menos de 5 pts 4,2 contra 4,3 y 3,4 contra 3,6. En días volátiles
  tampoco suma: 10,7 contra 10,9 y 9,8 contra 9,8. Se pone del lado del movimiento antes que la base en 44 movimientos y
  después en 12 (DISEÑO), y 23 contra 8 en EXAMEN (signos p 0,005). Promedio: 1,2 y 0,9 pts antes; cuando difiere,
  mediana +3 pts. La mediana marginal de EXAMEN empeora (14,0 contra 13,5): la ganancia es chica y se reparte.
- **ST(60; 2,5).** 2,7 pts antes en promedio en las dos muestras (antes en 71 contra 5 y 36 contra 1). Cuesta en la rueda
  unos 3 cambios más por sesión (10 → 13) y unas 2 vueltas falsas más (3,6-4,3 → 5,7-6,4). En días volátiles del EXAMEN,
  14,6 cambios contra 9,8.
- **ST(60,2).** 3,6-4,0 pts antes; casi el doble de cambios en la rueda (18-19 contra 10).
- Por qué el ATR lento ayuda: el ATR(10) se infla con las velas grandes del propio movimiento y aleja la banda justo
  cuando el precio se da vuelta. El ATR(60) se infla más despacio.

## Hoy (29-10), anotado y sin conclusión

El tramo continuo del registro de la Corriente arranca 12:47 UTC (101 velas). El 10x3 recalculado coincide con la
dirección de ATAS en 41 de 41 velas. Para el ATR(60) el tramo es corto (la semilla queda a mitad de mañana), así que la
comparación de hoy no vale. Hay que rehacerla con la sesión completa cuando ATAS la guarde en su cache.

Nada de esto anticipa la dirección ni es una estrategia: mide cuán rápido se pone del lado de un movimiento que ya ocurrió.

---

# Ronda 2 (29-10, ~11:00-11:45 AR): mecanismos nuevos

Método en `PREREGISTRO_CALIBRACION_2.md` (con la Enmienda 1, post-hoc). 114 candidatos de tres mecanismos, más un segundo
examen en MNQ 1 min (35 sesiones, 238 movimientos de 80 pts). Salidas: `cal_03_salida.txt`, `cal3_resumen.csv`,
`cal3_movimientos.csv`, `cal_03b_salida.txt` (elección y examen) y `cal_03c_salida.txt` (examen de lo informativo).

- **G1 (escala que no se infla)** y **G2 (línea que se acerca con la tendencia estirada)**: caen sobre la curva de la ronda 1.
  Ganancia media contra la curva: G1 -0,66 / -0,28 / +0,32 pts (DISEÑO / EXAMEN / MNQ en pts de MES); G2 +0,06 / +0,08 / +0,39.
- **G3 (vuelta rápida por velocidad)**: única que rompe la curva, y fuera de muestra. Ganancia media +1,34 / +0,81 / +1,28;
  positiva en 44 de 54 versiones en el EXAMEN de MES y en 54 de 54 en MNQ. G3d (con delta) igual, no agrega.
- **Elegida por las reglas** (domina a la base en DISEÑO): G3 ST(60,4) v2,5 j3 = línea ATR 60 x 4 + vuelta si el precio
  retrocede 2,5 ATR desde el extremo del tramo en 3 velas o menos. Mismo ruido en la sesión entera.
  - MES DISEÑO: antes en 57 movimientos, después en 33 (p 0,007); mediana 13,0 -> 11,0; volátiles 15,4 -> 11,5.
  - MES EXAMEN: antes en 24, después en 16 (p 0,13: **no alcanzó**); mediana 13,5 -> 12,0; volátiles 15,4 -> 11,9.
  - MNQ: antes en 115, después en 81 (p 0,009); mediana 60,0 -> 51,5 pts de MNQ.
  - Ruido en la rueda: MES 10,1 -> 11,1 cambios por día y vueltas falsas 3,6 -> 4,6; MNQ 9,1 -> 11,3 y 2,8 -> 4,0.
- **Veredicto pre-registrado: NO PASA** (MNQ sí, MES no). Por pedido del operador se instala igual como Corriente 1.1,
  **marcada sin validar del todo**, con registro de cada vuelta (línea o rápida) para medirla hacia adelante (Enmienda 1).
- Paridad: el motor del C# copiado a Python (`paridad_corriente.py`) da 0 velas distintas de 84.364 contra el motor del
  laboratorio; contra ATAS se compara con el volcado que escribe la 1.1 al cargar.

---

# Ronda 3 (29-10, 12:40-13:25 AR): el límite estadístico, con modelos de predicción

Método en `PREREGISTRO_PREDICCION.md` (con las Enmiendas 1 y 2). Salidas: `pred_01_salida.txt` (filas, geometría y modelos),
`pred_01b_salida.txt` (reglas), `pred_01c_salida.txt` (modelos con la geometría servida), `pred_02_salida.txt` (familia RX).

**La pregunta.** Con la corriente para un lado y el precio r pts atrás del extremo del tramo, ¿qué probabilidad hay de que
siga A pts más en contra antes de volver al extremo? Si el precio fuera azar puro, r/(r+A) (ruina del jugador).

- **Geometría** (126.298 casos): MES real 4,0 / 10,3 / 17,5 / 22,4 / 26,2 % para r de 0-1 / 1-2 / 2-3 / 3-4 / 4-5 pts contra
  azar 5,9 / 13,8 / 20,6 / 26,4 / 31,4 %. **Corrección del auditor independiente** (`auditoria_independiente/`, código propio):
  la brecha que crece con r sale de contar como 0 los casos sin desenlace (de 2,1 % a 20,3 % de las filas según r); contando
  solo los resueltos, MES queda entre +0,04 y -3,2 pp del azar. MNQ pegado al azar. Conclusión: paseo al azar, sin ventaja.
- **Modelos** (20 datos en vivo: delta, volumen, VWAP, velocidad, hora, cuerpo, distancias...): la fórmula del azar sin
  entrenar tiene el mejor AUC y log-loss en DISEÑO (0,732), EXAMEN (0,724) y MNQ (0,719); logística 0,70-0,71, árboles 0,68-0,70.
  Con la geometría servida, la logística mejora 0,8 % en MES y empeora en MNQ; los árboles no cambian. **No hay predicción.**
- **Consecuencia.** Sin predicción, la decisión óptima usa solo r: darse vuelta cuando el retroceso desde el extremo llega
  a X pts. El SuperTrend usa (h+l)/2 ± ATR con trinquete y por eso tiene colas largas (p90 de 21-22 pts en MES).
- **Familia RX** (Enmienda 2, ruido medido en la rueda): MES X = 10 elegido en DISEÑO; en EXAMEN antes que la base en 24
  movimientos y después en 10 (p 0,012); mediana 13,5 -> 11,5, media 14,9 -> 12,6, p90 22,0 -> 16,4; cambios en la rueda
  10,1 -> 10,5 y falsos 3,6 -> 3,3. **PASA.** También supera a la 1.1 en DISEÑO y EXAMEN (menos atraso, menos ruido, colas
  más cortas). MNQ: el procedimiento eligió X = 70 y en la segunda mitad no pasó (37 antes contra 50 después).
- **Hoy (29-10)**, 6 movimientos de 20 pts: peor atraso 1.2 12,75 pts, 1.1 22,25, 1.0c 20,5; en la suba de las 11:53 AR la
  1.2 se da vuelta con 10 pts hechos (1.1 con 22,25). Costo: día muy volátil, 8 cambios en la rueda hasta las 12:48 contra 4
  de la 1.0c. Un X fijo en puntos serrucha más en días volátiles y menos en días calmos: es el canje elegido.
- **El límite, dicho simple:** para que "viene un tramo de A pts más" acierte más de la mitad de las veces, el precio ya
  tiene que haber caminado A pts. Con lo que se ve en vivo, nada lo adelanta.
- **Auditoría independiente** (agente aparte, sin abrir el código del laboratorio): reproduce exacto los movimientos (114 y
  55), las medianas, medias y p90 de ST(10,3) y R10, el 24 contra 10 del examen (DISEÑO: 68 contra 20) y el ruido en la rueda.
- **Corriente 1.2 instalada** 29-10 12:59:40 AR (operador plano): modo "Retroceso en puntos" 10 pts por defecto. Volcado de
  ATAS contra Python: 27.224 velas, dirección, extremo y línea idénticos. Pantalla 13:01: línea verde medida 7751,87 contra
  7751,75 calculada; flecha llena para arriba (precio sobre el VWAP).
