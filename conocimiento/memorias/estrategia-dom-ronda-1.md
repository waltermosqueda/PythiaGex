---
name: estrategia-dom-ronda-1
description: "21-09: ronda 1 de la busqueda de estrategia (8 reglas de DOM/heatmap/contexto, 20 ruedas MNQ orden por orden, pre-registro, costo 0,96): 0 de 8 sobreviven; lo mas cerca fue el rebote en VWAP -1 desvio (54,5 % en la reserva, empate 56 %)."
metadata: 
  node_type: memory
  type: project
  originSessionId: 6b882b35-31bc-4131-8cd4-4b099f670c25
  modified: 2026-09-22T01:22:42.227Z
---

Workflow estrategia-dom-medida (21-09), codigo y .md en `PythiaGex\laboratorio\dom`. Esquema: explorar 20-08..07-09 (12),
confirmar 08-09..17-09 (8), reserva 31-07..19-08 (14), ruedas nuevas 18-09 y 21-09 bajadas con la sonda (sin usar).

Resultado, todo en CONFIRMAR con ejecucion realista:
- A1 iceberg vivo (1,5x lo mostrado, 5 s): noche -1,6 pts/op (n 33); el iceberg dura 3-4 s despues de la señal.
- A2 pared agotada -> ruptura: rueda -1,0, noche -1,4. A4 pared que se va: -1,4 / -1,3. B4 headfake: -3,5 / -1,7.
- C1 entrar con limitada (fila decide): ahorra 0,15-0,36 pts vs mercado pero el esceptico la tumbo (no le gana a
  "siempre limitada"; la mitad del ahorro es la salida de seleccion adversa).
- C4 rebote en VWAP -1 desvio viniendo de >= 8 pts (rueda, obj/stop 8): 59 % explorar, 57 % confirmar, 54,5 % reserva
  (n 220, -0,23 pts/op). Hay algo de direccion pero menos que el costo (empate 56 %). Reserva GASTADA para esta regla.
- D1 dominante de gamma quieta en regimen POS: 52 % vs placebo de lugar 53 %: nada.
- E1 (estudio de eventos, pelota contra el extremo de 60 min): se dio vuelta, -2,75 pts/op en confirmar.

**Why:** leccion de fondo: con stops de 2 ticks y reaccion de segundos, el costo 0,96 y el ruido de MNQ (rango mediano
1 min 11,25 rueda / 7,5 noche) se comen todo; lo unico con algo de direccion (C4, 54-57 %) necesita mas relacion
beneficio/riesgo o menos costo.
**How to apply:** no volver a proponer estas 8 sin una razon nueva; la ronda 2 (estrategia-ronda-2, `laboratorio\dom\ronda2`)
va a horizonte de minutos, stops detras de estructura, contexto de gamma/VWAP/horario y ML walk-forward. Ver
[[busqueda-estrategia-sin-parar]], [[busqueda-gatillo-2026-09-17]], [[libro-dom-mnq-armado]].
