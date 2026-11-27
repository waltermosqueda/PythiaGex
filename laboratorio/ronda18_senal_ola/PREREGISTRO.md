# Ronda 18 — Señal de entrada y salida con la ola y la marea — PRE-REGISTRO

Escrito el 27-11-2026 ~02:30 AR, ANTES de correr nada. Pedido del operador: "una señal o figura que indique el mejor momento
para entrar y salir, con esto que construiste, bien calculado".

## Máquinas (las mismas del Superindicador 2.0a, verificadas contra la ronda 17)
- Ola = zigzag de X orden por orden; marea = el mismo con 4X. X por rueda = 0,08 × mediana del rango RTH de las 10 ruedas
  previas (los valores de la ronda 17 y del arnés: MES 16-11 6,75 · 17-11 6,75 · 18/19/20-11 8,50 · 23/24/25-11 9,75; ES 02-11
  4,75 · 03-11 5,00 · 04-11 4,75 · 06-11 5,00 · 09-11 5,25 · 10-11 5,75 · 11-11 6,00 · 12-11 6,25 · 13-11 6,75).
- Las máquinas corren desde las 18:00 NY de la víspera (estado real, como en el indicador); las ENTRADAS se toman solo en RTH
  9:30-15:55 NY; toda posición abierta se cierra a las 15:59:59 NY con el último precio.

## Reglas (todas se reportan, ganen o pierdan)
- **R1, la principal: "a favor de la marea con agresores".** COMPRA cuando la ola se da vuelta para arriba (se confirma un mínimo)
  y en ese momento la marea sube CON compradores (delta de la pierna de la marea > 0). VENTA, el espejo.
  Entrada = la raya de la ola (mínimo + X / máximo − X): una orden stop en la raya.
  Salida = cuando la ola se da vuelta en contra, en su raya (máximo − X / mínimo + X): el stop que persigue al precio a X del extremo.
  Una posición por vez; no se promedia.
- **R2:** R1 sin el filtro de agresores (solo la dirección de la marea).
- **R3:** todas las vueltas de la ola, sin marea (seguir la ola pura).
- **R4:** contra la marea (compra cuando la ola se da vuelta para arriba con la marea BAJANDO, y espejo): contraste.
- Costo por operación completa: 0,50 puntos (dos ticks: comisión + deslizamiento) y, como prueba de esfuerzo, 1,00.

## Cómo se juzga
- Muestra de diseño: MES 16 a 25-11 (8 ruedas). Confirmación: ES 02 a 13-11 de la ronda 17 (9 ruedas), sin tocar nada.
- Por regla: operaciones, % ganadoras, ganancia media de las ganadoras y pérdida media de las perdedoras (en puntos y en X),
  neto por operación y por rueda con costo, t por RUEDA (no por operación).
- La cuenta de fondo: cada operación entra X después del giro y sale X antes del extremo siguiente, así que gana solo si la pierna
  mide más de 2X + costo. En un paseo al azar el esperado es −costo. Una regla "sirve" si da neto positivo en las DOS muestras con
  t por rueda ≥ 2; si no, se reporta qué pasó y por qué (qué piernas pagaron y cuáles no), sin maquillar.
- Nada de esto promete rentabilidad: es una regla mecánica medida en 17 ruedas; la prueba de verdad es en vivo.

## Enmienda 1 (27-11 ~02:45 AR, después de ver R1-R4 y ANTES de correr esto)
R1-R4 dieron (17 ruedas): R1 bruto 0,00 por operación, 34 % ganadoras, +7,84 / −4,07; con costo 0,50 pierde en 15 de 17 ruedas.
La aritmética es la del paseo al azar: cada operación necesita una pierna de más de 2X y la mediana fue 1,68X. Se agregan
cuatro variantes con fundamento, que se juzgan con la misma vara (neto > 0 en las DOS muestras, t por rueda ≥ 2):
- **V1, la marea pura:** compra cuando la MAREA se da vuelta para arriba y sale cuando se da vuelta para abajo (escala 4X):
  pocas operaciones, el costo pesa 4 veces menos por punto recorrido. Las piernas grandes van con el delta el 90 % (ronda 17).
- **V2, el primer retroceso:** R1, pero solo la primera entrada de cada pierna de la marea (el "primer pullback" de la práctica).
- **V3, retroceso sin agresores:** R1, pero solo si la ola en contra que acaba de terminar se hizo SIN agresores (su delta no fue
  en contra de la marea): el retroceso no lo empujó nadie, lo hicieron los pasivos.
- **V4, la mañana:** R1 con entradas solo de 9:30 a 11:00 NY, cuando se arman las tendencias del día.
