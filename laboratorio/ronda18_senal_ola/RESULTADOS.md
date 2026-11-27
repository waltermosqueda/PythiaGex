# Ronda 18 — Señal de entrada y salida con la ola y la marea — RESULTADOS (27-11-2026 ~03:00 AR)

Pre-registro y enmienda 1: `PREREGISTRO.md`. Script: `r18_01_senal.py` (salidas `r18_01_salida.txt` con R1-R4 y
`r18_02_salida.txt` con R1-R4 + V1-V4). Máquinas = referencia del motor del Superindicador 2.0a, orden por orden.
Entrada y salida en la raya de la ola (orden stop), una posición por vez, RTH 9:30-15:55 NY, cierre 15:59:59.

## Lo que dio (17 ruedas: diseño MES 16 a 25-11, confirmación ES 02 a 13-11)
| Regla | Ops | Ganadoras | Gana media / pierde media (pts) | Bruto por op | Neto por op, costo 0,50 | Ruedas + (costo 0,50) |
|---|---|---|---|---|---|---|
| R1 a favor de la marea con agresores | 808 | 34,2 % | +7,84 / −4,07 | +0,00 | −0,50 | 2/17 |
| R2 a favor de la marea | 997 | 34,7 % | +7,55 / −4,02 | −0,00 | −0,50 | 0/17 |
| R3 todas las vueltas de la ola | 2.117 | 34,2 % | +6,93 / −4,05 | −0,29 | −0,79 | 0/17 |
| R4 contra la marea | 1.120 | 33,8 % | +6,36 / −4,07 | −0,54 | −1,04 | 2/17 |
| V1 la marea pura (4X) | 122 | 39,3 % | +22,38 / −16,96 | −1,48 | −1,98 | 6/17 |
| V2 primer retroceso | 118 | 36,4 % | +7,63 / −4,31 | +0,04 | −0,46 | 6/17 |
| V3 retroceso sin agresores | 39 | 28,2 % | +4,75 / −3,50 | −1,17 | −1,67 | 2/13 |
| V4 la mañana (9:30-11:00) | 373 | 35,1 % | +7,62 / −3,87 | +0,17 | −0,33 | 9/17 |

Por muestra: V4 casi empata en MES (−0,06 por operación, 5/8 ruedas +) y no confirma en ES (−0,62). V2 empata en ES (−0,04)
y pierde en MES (−0,78). **Ninguna pasa la vara pre-registrada** (neto > 0 en las dos muestras con t por rueda ≥ 2).

## Por qué (la mecánica)
- Cada operación entra X después del giro y sale X antes del extremo siguiente: gana solo si la ola mide más de 2X + costo.
  La mediana de las olas operadas por R1 midió 1,68X (p25 1,28X, p75 2,33X); el 34,2 % pasó de 2X y el 13,2 % de 3X. De ahí
  el 34 % de ganadoras y que las ganadoras paguen ~2 veces lo que cuestan las perdedoras: el resultado bruto es 0, la cuenta
  exacta del paseo al azar (la ruina del jugador). El costo (2 ticks) decide el signo.
- Lo que SÍ se ve: ir a favor de la marea es mejor que ir en contra (R1 0,00 contra R4 −0,54 bruto por operación, con la misma
  mecánica): "no chocar contra la ola" tiene medio punto por operación de respaldo en esta muestra. No alcanza para pagar costos.
- Operar cada ola da ~48 operaciones por rueda: el costo se come todo. La marea pura (V1) opera 7 por rueda pero pierde por
  el retroceso de 4X que regala en cada salida.

## Qué queda
- La señal va a la demo y al indicador como "en prueba", con sus números a la vista; nada de esto promete rentabilidad.
- Próximas ideas (a pre-registrar, muestra nueva): salidas que no regalen 2X (el baúl: gestión con tope), contexto de la rueda
  (tendencia contra rango, que se decide con el rango previo + la hora), y medir en vivo la lectura de la ola con la bitácora.
