# Ronda 17 — Prueba en vivo (complemento; diseño, no instalado)

Escrito el 27-11-2026. Es el complemento de la anatomía histórica (RESULTADOS.md), no un requisito para usarla. **No se tocó
ATAS ni el código de ningún indicador.** Esto describe qué tendría que grabar un indicador (el Superindicador u otro aparte,
cuando el operador lo decida) para medir HACIA ADELANTE si leer el giro y la ola mejora la entrada, y cómo se juzga.

## 1. Qué se quiere saber, en tres preguntas fijas
1. **¿La vuelta del delta después del extremo avisa el giro?** En la historia (tanda 0): con el mismo retroceso ya hecho,
   P(giro) sube ~1,1-1,2 veces si la agresión neta ya se dio vuelta a los 15-60 s del extremo. ¿Se sostiene en vivo, con
   umbrales calibrados con ruedas PREVIAS (no con la misma rueda)?
2. **¿La firma del footprint (subasta terminada, extremo flaco) sirve leída a tiempo?** En la historia no (RR 0,88-1,02): es
   el control negativo. Si en vivo da distinto, hay un error de una de las dos mediciones.
3. **¿Seguir la ola (operar a favor del lado que agrede en la pierna actual) le gana al azar después de costo?** La ola
   DESCRIBE la pierna (86-99 % de las piernas cortas van con su delta); falta saber si entrar con ella deja algo.

## 2. Qué grabar (un archivo por rueda, `%APPDATA%\ATAS\PythiaGex\anatomia\vivo-<inst>-<fecha>.csv`)
Cada SEGUNDO de la rueda (9:30:00-15:59:59 NY, con milisegundos de la PC y del exchange):
- precio último, bid/ask y sus tamaños en reposo (antes de la última orden), volumen, delta, ofi y ofi pasivo del segundo
  (mismas fórmulas que `laboratorio\gatillo\base.py`), absorción real en la punta (abs_bid/abs_ask).
- X del día (0,08 × mediana del rango de las 10 ruedas previas, nunca < 4 pts) y estado del zigzag en vivo con X y X/2:
  dirección de la pierna, extremo vigente (precio y hora), retroceso hecho en pts y en % de X.
- sumas de 60 s de volumen, delta, ofi, ofi pasivo y absorción, y su percentil contra la MISMA franja de 30 min de las 5
  ruedas previas (la calibración en vivo no puede usar la rueda del día).
- en el extremo vigente: volumen comprador y vendedor en el tick del extremo y en los 4 de adentro, estado de la subasta
  (terminada / sin terminar / otra), órdenes ≥ p99 y ≥ p99,9 (del instrumento, ruedas previas) a ≤ 2 ticks.
- lugares a ≤ 1,5 pts (nombre y distancia): ayer (H, L, C, POC, VAH, VAL), noche, IB (desde 10:30), VWAP ±1/±2, redondo de
  50, semana y mes anteriores.
- franja horaria, rango hecho / rango esperado a esa hora, tipo de día provisorio.
- estado del semáforo (abajo) y la razón en texto corto.
Por EVENTO: nace un candidato (retroceso ≥ X/4 de un extremo), sus fotos a τ = 15, 30 y 60 s, y su desenlace (GIRO ≥ X,
PAUSA X/2-X, MENOR, con la hora en que se resolvió). Y las órdenes reales del operador (de los logs de ATAS) con su hora,
para cruzarlas con el estado del semáforo en ese segundo.

## 3. Semáforo de lectura (fijado antes de grabar; describe, no promete)
- **ESPERAR** por defecto.
- **"POSIBLE GIRO ARRIBA/ABAJO"** en un candidato que sigue en pie a τ = 15 s, con retroceso ≥ 50 % de X y la agresión neta
  de la reacción ya en contra del empuje. Muestra al lado la P(giro) medida para ese tramo de retroceso (tabla de la
  historia, actualizada con las ruedas grabadas) y la de base sin la vuelta del delta.
- **"OLA ARRIBA/ABAJO"** mientras la pierna vigente tenga delta y ofi del mismo lado que el precio; **"PIERNA PASIVA"** cuando el
  precio va contra el delta (lo mueve el libro, no la agresión).
- Ningún estado dice LARGO o CORTO hasta que el punto 4 lo apruebe.

## 4. Cómo se juzga (después de N = 20 ruedas de rueda americana completas, con la grabación auditada)
- Pregunta 1: riesgo relativo de Mantel-Haenszel de GIRO con y sin la vuelta del delta, por rueda × tramo de retroceso,
  con intervalo del 90 % remuestreando ruedas. **Se confirma como lectura** si el RR ≥ 1,10 con el intervalo > 1 y a favor en
  ≥ 14 de 20 ruedas, en τ = 15 s y en τ = 60 s.
- Pregunta 2: el mismo cálculo para la firma; se espera RR ≈ 1 (control).
- Pregunta 3 y operación simulada del "posible giro": entrada al último precio del segundo τ en el sentido del giro, stop 1 tick
  detrás del extremo, objetivo X/2 (y el TP de 2,5 pts de MES del operador), salida a los 20 min; costo 0,50 pts; contra la
  misma entrada en la dirección contraria (azar en el mismo instante) y contra candidatos sin la vuelta del delta. Se informa
  neto por operación con t agrupado por rueda. "Paga sola" solo si neto > 0 con t ≥ 2; lo esperable por la historia es que NO
  pague sola (el efecto es chico frente al costo).
- Potencia: con ~200-400 candidatos por rueda a τ = 15 s, 20 ruedas dan miles; alcanza para ver una diferencia de 5 puntos de
  P(giro). Para la operación simulada, ver una ventaja de 0,25 pts por operación con t 2 necesita ~1.500 operaciones.
- Mirada intermedia a las 10 ruedas SOLO para errores de grabación (no se corta por resultado).
- Todo lo que pase o falle va al BAUL_DE_PISTAS.md con sus números (regla del baúl: "no paga sola" no es "no tiene
  información").
