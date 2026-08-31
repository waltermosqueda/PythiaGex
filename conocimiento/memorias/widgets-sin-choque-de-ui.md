---
name: widgets-sin-choque-de-ui
description: "En los widgets: el valor que cambia NUNCA va en la misma fila que el control. Se recorta y no se lee."
metadata:
  node_type: memory
  type: feedback
  originSessionId: 46e731fd-f0ba-466c-bd15-979f9343516d
---

Me lo marco el 2026-08-21 con una captura: el numero del slider salia cortado ("7?!5" en vez de 7715). Textual: *"tus explicaciones imagenes siempre tienen ese problema cuando las construis se chocan la ui, los puntos con el valor respectivo, y no podes ver bien el numero cuando lo corres"*. Dijo que **pasa hace varios dias**, o sea que lo vengo repitiendo.

## Reglas

1. **El valor que cambia va en su propia linea**, arriba del control y a tamaño grande (28-32px). Nunca `label + input + span` en un mismo `display:flex`: el `flex:1` del input aplasta al span y el numero se recorta.
2. **`font-variant-numeric: tabular-nums`** en todo numero que se actualiza, para que no bailen los digitos al arrastrar.
3. **Nada de texto dibujado adentro del area del grafico** (etiquetas de lineas verticales, valores sobre los puntos). Si hace falta identificar algo, va en una leyenda **afuera** del canvas.
4. **`white-space: nowrap`** en los valores de las filas etiqueta-a-la-izquierda / valor-a-la-derecha, y `min-width: 0` en la etiqueta para que ceda ella y no el numero.
5. El contenedor es de 680px pero **en su pantalla se ve mas angosto**. Probar mentalmente a ~380px antes de dar por buena una fila horizontal.

**Why:** el widget es su forma principal de aprender — dijo explicito que sin grafico interactivo no le queda nada. Un numero ilegible arruina justo la parte que hace que la explicacion funcione.

**How to apply:** apilar en vertical por defecto y usar filas horizontales solo cuando los dos elementos tienen ancho fijo y de sobra. Ver [[como-ensenarle-trading]].
