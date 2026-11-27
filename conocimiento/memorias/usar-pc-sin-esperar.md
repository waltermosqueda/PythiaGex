---
name: usar-pc-sin-esperar
description: "Regla del operador (23-11-2026): mis tareas tienen prioridad sobre su uso de la PC; no esperar a que quede quieta (nada de umbral de inactividad); solo parar si el lo dice explicitamente"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-27T00:48:53.165Z
---

Textual (23-11-2026 ~15:20 ART, mientras un script mio esperaba 60 s de inactividad para abrir el dialogo Indicators de ATAS):
"usa la pc no des vueltas, tus tareas son prioridad por sobre mi uso, solo si yo te freno explicitamente para en tus tareas sea
la que sea".

**Why:** esperar a que el deje el mouse/teclado demoraba las instalaciones; prefiere que avance y frenarme el si hace falta.

**How to apply:** no condicionar clics, UIA ni dialogos de ATAS a GetLastInputInfo (deja sin efecto el "esperar >= 60 s" de
[[foco-atas-atajos-trading]] y el umbral de [[modo-cortes-mouse]] para MIS tareas). Se mantiene lo barato que protege sus ordenes:
trabajar sobre graficos con "Trading is locked" cuando se pueda, devolver el foco a la ventana que lo tenia al terminar, y avisar en
el mensaje que toque ATAS. Parar solo ante un "freno"/"para" explicito suyo.
**LIMITE (26-11-2026 21:45):** esta regla NO habilita clics ni arrastres de computer-use sobre un grafico de ATAS SIN el candado de
trading: arrastrando la barra del grafico limpio (destrabado) mientras el dictaba, sus teclas mandaron 2 Buy Limit reales (canceladas
sin fill). Con el operador presente: solo UIA/archivos/capturas, o pedirle que trabe el grafico o que lo mueva el. Ver
[[foco-atas-atajos-trading]].
