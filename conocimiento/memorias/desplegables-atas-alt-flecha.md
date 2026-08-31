---
name: desplegables-atas-alt-flecha
description: "Los combos de ATAS no se abren con clic desde automatización, pero sí con alt+Flecha abajo. No declarar imposible sin probarlo."
metadata:
  node_type: memory
  type: reference
  modified: 2026-08-24T19:00:00.000Z
---

En los diálogos de ATAS (Indicadores → Settings), los desplegables —color, `Period`,
`Calculation Mode`, `Visual Mode`— **no se despliegan con `left_click` ni `double_click`**
desde computer-use. La captura no los muestra y el valor no cambia.

**Sí se abren con `alt+Down`** una vez que la fila está seleccionada:

1. `left_click` sobre la **etiqueta** de la fila (columna izquierda), no sobre el valor.
2. `key: "alt+Down"`.
3. El menú aparece en la captura y se puede clickear la opción.

Para el selector de color: aparecen `Transparent`, una grilla de *Theme Colors*, una fila de
*Standard Colors* (10 casilleros de ~14,5 px: rojo oscuro, rojo, naranja, amarillo, verde claro,
verde, celeste, azul, azul oscuro, violeta) y `More Colors…`. El valor queda en formato `#AARRGGBB`.

Para los campos numéricos, en cambio, `triple_click` + `ctrl+a` + escribir + **`Tab`** funciona.
`Return` confirma pero **cierra el diálogo**, así que conviene `Tab`.

**Why:** el 2026-08-24 di por imposible cambiar el color después de tres intentos con clic y se lo
dije al operador. Él respondió que con la flechita se despliega, probé `alt+Down` y funcionó a la
primera. Declarar algo imposible sin agotar los caminos le hace perder una función que sí tenía.

**How to apply:** antes de decir "no se puede" en cualquier UI, probar los atajos de teclado
estándar de Windows (`alt+Down`, `F4`, `Space`). Ver [[navegar-atas-sin-pedir-permiso]] y
[[mirar-pantalla-antes-de-responder-atas]].
