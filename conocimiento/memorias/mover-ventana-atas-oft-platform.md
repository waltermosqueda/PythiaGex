---
name: mover-ventana-atas-oft-platform
description: "ATAS corre como proceso OFT.Platform y esta maximizada; hay que restaurarla antes de moverla, o el clic nunca llega."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 15e03ce1-51d8-43ea-8c35-a2fa1a4b8145
  modified: 2026-09-04T21:54:09.023Z
---

Cuando un clic no responde en ATAS, **mover la ventana y reintentar**. El usuario lo
pidio tres veces y aclaro que vale para cualquier contexto y circunstancia, no solo
para ATAS.

**Why:** la ventana del chat de Claude tapa la franja derecha de la pantalla. Un clic
que cae en esa franja no llega a la aplicacion de abajo. No es un bug del indicador ni
de ATAS: es superposicion de ventanas.

**How to apply:** dos datos que hacian fallar el intento de moverla:

1. El proceso **no se llama ATAS, se llama `OFT.Platform`**. `Get-Process ATAS`
   devuelve vacio. Y su `MainWindowTitle` puede ser el del dialogo de Replay, asi que
   hay que enumerar las ventanas de nivel superior del pid con `EnumWindows`, no
   confiar en `MainWindowHandle`.
2. La ventana esta **maximizada** (se reconoce porque su rect es `-8,-8` de
   `1616x868` en una pantalla de 1600x852). Una ventana maximizada ignora
   `MoveWindow`. Hay que llamar `ShowWindow(h, 9)` (SW_RESTORE) primero, esperar
   ~700 ms, y recien ahi `MoveWindow`.

Medida que funciona con el chat a la derecha: `MoveWindow(h, 0, 0, 1045, 850)`.

Hacerlo cuando el mercado o el replay estan detenidos: redimensionar reflowea todos
los graficos y con datos corriendo es justo lo que lo puede tildar. Ver
[[cambios-atas-de-a-uno]] y [[clics-que-no-llegan-y-loops]].
