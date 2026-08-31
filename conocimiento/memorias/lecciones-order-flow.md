---
name: lecciones-order-flow
description: "Las reglas de footprint, heatmap y niveles que ya aprendió operando en vivo el 2026-08-20 — no volver a explicarlas desde cero."
metadata: 
  node_type: memory
  type: project
  originSessionId: 46e731fd-f0ba-466c-bd15-979f9343516d
  modified: 2026-08-20T20:36:39.555Z
---

El 2026-08-20 hicimos una sesión completa en vivo, de la apertura al cierre. Esto **ya lo sabe** — retomar desde acá, no desde el principio.

## Lo que domina

- **Leer una celda de footprint.** Izquierda = vendedores agresivos (al bid), derecha = compradores agresivos (al ask). Delta = derecha − izquierda, **con signo**. Sacó 5 de 5 en el drill de signos.
- **Bid/ask sin confundirse.** Entendió que para comprar ya vas al ask y para vender ya vas al bid, y que el footprint registra **al agresor**, no a quien puso la orden. Dedujo solo que una celda `1|0` implica una compra pasiva ejecutada.
- **Absorción vs momentum.** Misma firma de delta negativo: si el precio **no** cede es absorción, si cede es momentum. Vio los dos casos el mismo día con horas de diferencia.
- **Volumen mínimo = leer rechazo, no delta.** Umbral operativo: menos del 10 % de la celda mayor de esa vela es ruido; más del 30 % pesa; el medio se saltea.
- **Encontrar muros solo:** `Gamma × OI` alcanza para rankear strikes, porque el resto de la fórmula es constante en el día. Lo hace con las columnas del Options Board.

## Reglas duras que ya le di

- **El stop va del otro lado del nivel, nunca delante.** Lo aprendió perdiendo: stop de 2,63 pts delante del imán de 7700 en un día de ±66.
- **Una ruptura sin delta a favor no es ruptura.** Caso real: rompió 7711 con delta −327 arriba de la línea y falló.
- **El heatmap propone, el footprint dispone.** Las órdenes se pueden retirar; solo la agresión que choca y no mueve el precio confirma.
- **Cuanto más chico el muro, menos tests aguanta.** 514 contratos aguantaron 2 tests; 218 cayeron al primero.
- **Un roce no es una ruptura:** hay que pasar por 3+ puntos y sostenerlo. Y aun así puede fallar.
- **Los niveles se mueven durante el día.** El zero gamma subió 24 pts y el max gamma bajó 110 sin que el precio se moviera, solo por el 0DTE muriéndose. **Recalcular 3 veces: apertura, mediodía y 14:30 ET.**
- **VWAP arriba del precio en gamma negativa = techo, no imán.**

## Cómo enseñarle

Analogía concreta primero, una idea por vez. **Prefiere widgets interactivos a texto** — dijo explícitamente que el texto abstracto lo olvida. Repetir cada concepto varias veces con ejemplos reales de su pantalla, no inventados. Pide que se le pregunte y se le corrija.

**Why:** ya invirtió una sesión entera de mercado en esto. Volver a explicarle lo básico sería desperdiciar lo aprendido y aburrirlo.

**How to apply:** retomar desde absorción y niveles; lo que falta es **repetición sobre casos reales** y la bitácora de 15 sesiones. Ver [[calcular-gex-propio]], [[panel-navegador-no-frontear]] y [[como-ensenarle-trading]].
