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
