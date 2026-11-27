# Ronda 24 — A3 a 30 minutos en MES 2024-25 — RESULTADOS

27-11-2026, de 09:25 a 09:40 AR. Pre-registro y huellas a las 09:31 (`PREREGISTRO.md`, `huellas_r24.json`). Una sola corrida (`PRUEBA_CORRIDA.lock`).
Nada de esto promete rentabilidad.

## Veredicto
**NO PASA.** La hermana de 30 minutos de A3 corrió sobre 337 ruedas de MES (29-04-2024 a 08-09-2025, nunca usadas para elegir A3) y no
le ganó al azar:
- acierto **59,9 %** contra **61,3 %** de un precio al azar con la misma ubicación de stop y objetivo;
- neto **−0,74** por operación con costo 1,00;
- ventaja sobre el azar **+0,60 pts/op (t 0,44)**;
- 157 operaciones, 0,47 por rueda.

| | Ruedas | Ops | Acierto (azar) | Neto | Ventaja (t) |
|---|---|---|---|---|---|
| Todo 2024-25 | 337 | 157 | 59,9 % (61,3) | −0,74 | +0,60 (0,44) |
| 2024 (desde el 29-04) | 168 | 80 | 58,8 % (59,7) | −1,40 | +0,03 (0,02) |
| 2025 (hasta el 08-09) | 169 | 77 | 61,0 % (63,0) | −0,06 | +1,20 (0,49) |

## Qué se probó y por qué no es A3
A3 es la candidata congelada de la R23. Acá se la llevó a velas de 30 minutos **sin tocar ningún número**, porque de 2024-25 la caché
de ATAS solo guarda velas de 30 y 60 minutos. Cambian dos cosas: se mira en 6 cierres en vez de 31, y las salidas se resuelven con velas
de 30 minutos.

La calibración en el año ya visto (`calibracion.txt`, `diagnostico.txt`) mostró que es una **hermana más débil**:
- En sep-2025 a feb-2026 copia bien a A3: 71 % y +2,2, contra 74,3 % y +2,49 en 5 minutos.
- En mar-ago 2026 se cae: 48-52 % y −3,5 a −4,4, contra 67,2 % y +0,59.
- Casi toda esa caída viene de mirar menos cierres. **A3 en 5 minutos, mirando solo los cierres de en punto y de y media, da en mar-ago
  2026 57,7 % y −1,65.** El resultado positivo pero flojo del chequeo de A3 depende de qué cierres se miren: es frágil.
- La forma de resolver la salida dentro de la vela pesa poco: "misma vela = stop" y "camino O-H-L-C" dan lo mismo.

## La mecánica (post hoc, `mecanica_posthoc.txt`)
Se repite lo que vio la R23. El tipo de día se mira **después** del cierre: tendencia si |cierre − apertura| ≥ 0,6 del rango, balance
si < 0,3, mixto en el medio.
- **Días de tendencia** (120 de 337): acierto 46,8 % contra 60,8 % del azar, −4,32 por operación, ventaja **−2,61**. El VWAP se arrastra
  detrás del precio y el precio nunca vuelve.
- **Días de balance** (99): 64,4 % contra 61,0 %, ventaja +1,15.
- **Días mixtos** (118): 66,2 % contra 61,9 %, neto +1,35, ventaja **+2,55**.
- Por trimestre la regla alterna: gana en 2024-T3 y 2025-T2 y pierde en 2024-T2, 2024-T4 y 2025-T1.

**La pieza que falta es reconocer el día de tendencia antes de las 13:00.** Eso lo busca la ronda 25.

## Qué cambia para el juez en vivo de A3
Nada de su pre-registro: A3 se sigue juzgando en vivo desde el 30-11 con su vara. Pero **la expectativa baja**: en dos de las cuatro
muestras la idea se comportó como el azar (mar-ago 2026 en 5 minutos y 2024-25 en 30 minutos).

## Archivos
- `r24_a3_30.py`: la regla a 30 minutos, la calibración y la prueba con candado.
- `PREREGISTRO.md`, `huellas_r24.json`, `PRUEBA_CORRIDA.lock`.
- `calibracion.txt`, `r24_diagnostico.py` → `diagnostico.txt`.
- `prueba.txt`, `resultado_prueba.json`, `ops_prueba_2024_25.csv`, `mecanica_posthoc.txt`.
