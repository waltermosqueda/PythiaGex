---
name: auditoria-estela-2026-09-15
description: La estela "muy arriba" es el libro de la noche, no un corrimiento; el nucleo C# = Python al millon; la leyenda mentia la edad; el archivo no guardaba cada version y el auditor asumia horizonte Hoy.
metadata:
  type: project
---

Auditoria del 15-09 (20:30-21:15) sobre "los puntos dominantes se dibujan muy arriba":

- **De noche no hay tunel porque no hay gamma cerca del precio.** A las 16:00 vence el 0DTE; el libro "Hoy" pasa a
  ser el vencimiento de manana, casi vacio: NDX D1 29.553 (+270) con 233M y D2 28.903 (-380) con -62M (`mucho=False`),
  contra 29.005 (+39) con 1.600M y 28.906 (-60) con -2.200M a las 14:14. Las cuatro capas (SPX, SPY, QQQ, NDX) son el
  mismo tipo de libro, por eso sumar indices no acerca nada de noche. Ver [[dominantes-de-noche-resto-de-ayer]].
- **El dibujo esta donde la cuenta dice.** `atas/Rebobina --prueba <cadena.json> --precio X --ahora Z [--horizonte]`
  corre el mismo nucleo del indicador y reproduce la linea AUDIT al centavo con la cadena exacta (20:42); capas_nq.py
  da lo mismo que el C# al millon. La regla de dominantes (una por lado, empate 20 %) tambien coincide.
- **Lo que estaba mal (arreglado en 1.10b):** la leyenda decia "dato de hace 25 min" con una cadena de 74 min
  (EdadMin es la edad al generarse; ahora se suma hasta ahora); las capas copian el horizonte del grafico y ese
  grafico estuvo en Todo hasta las 19:22 (firma: strikes 280 y netOi positivo) mientras el auditor asumia Hoy; ni la
  nube ni el local guardaban cada version (dedupe por sello de CBOE), asi que minutos de la rueda no se pudieron
  reproducir. Ahora la capa anota `cadenaTs= gen= horizonte= fuente=` y el local guarda cada version.
- **EL LOG VA EN UTC−3 (la maquina esta en Argentina), NO EN UTC−4.** Asumir −4 corrio todos los minutos una hora y
  una hora entera parecio que la capa no cerraba; con la hora correcta Rebobina reproduce la linea de las 14:13:55
  exactamente (cadena ultima 17:06:18Z + horizonte Todo). auditar_estela.py usa ahora la zona de la maquina.
  Con eso: noche 100 % en los cuatro libros (45/45, 41/41, 43/43, 45/45); rueda NDX 96/101 con Todo, ETF mejor con
  Hoy (abierto: manana se cierra con `horizonte=` en el AUDIT).
- **"Las ambar cerca del precio de noche"** eran el libro QQQ de anoche (+5…+63 / −3…−36 con 1B); hoy la primaria
  NDX se oculta por duplicada con la capa NDX (bloque 18) y el libro QQQ de manana tiene sus barras en 708/700.
  Reiniciar ATAS de noche vacia el libro vivo de Rithmic (volumen acumulado solo con ATAS abierto): dos reinicios
  hoy, capa RITHMIC en 0,002B a las 21:02.
- **La nube "cada minuto" corre cada 8-25 min** (cron de GitHub, medido con `gh run list`): el dato de las capas es
  el retraso de CBOE (15 min) mas hasta 25 min de nube. Decirlo con el numero, no "cada minuto".
- Pendiente: correr `laboratorio/auditar_estela.py` en una rueda con 1.10b para el cierre exacto; y el modo noche
  (atenuar + tunel de cierre punteado) solo con su palabra.

**Why:** el operador pidio "todo auditado sin excusas"; la respuesta honesta fue que la estela era correcta y lo
roto era la edad y la trazabilidad.
**How to apply:** ante "los niveles estan lejos" de noche, mirar `mucho=` y los M de las dominantes antes de tocar
el dibujo; para auditar un minuto, usar Rebobina --prueba con la cadena que anota el AUDIT.
