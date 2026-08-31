---
name: panel-navegador-no-frontear
description: "Nunca traer pestañas al frente en el panel del navegador mientras él lo está mirando: se le congela la vista."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 46e731fd-f0ba-466c-bd15-979f9343516d
  modified: 2026-08-20T00:22:54.164Z
---

El 2026-08-19 se le congeló dos veces la vista del panel del navegador de Claude mientras yo trabajaba. Quedaba pegado en una pestaña y no podía cambiar.

**No son las webs ni su PC.** Lo medí a fondo: latencia del hilo principal 4-6 ms en las tres páginas, memoria 11/20/100 MB contra un límite de 4096, InsiderFinance pintando a 60 FPS exactos, ninguna hace polling, ninguna roba el foco (Opensera se mantuvo visible 20 s seguidos). Su RAM al 55 % y CPU al 20 % son normales — igual que en [[freeze-portapapeles-textinputhost]], un problema puntual no mueve el promedio.

También descarté mi primera hipótesis: los 5 MB de JSON incrustados de InsiderFinance no son el problema — cinco parseos completos suman solo 9 MB.

Lo que queda es el panel de la app. Los errores lo decían: *"the Browser pane is not displayed"*, *"currently hidden… unresponsive renderer"*. Y las dos veces coincidió con que yo manejaba el panel mientras él lo miraba.

**Why:** el panel muestra una pestaña por vez y es superficie compartida. Cada `tabs_select` o `navigate` mío le mueve la vista abajo de los pies.

**How to apply:** trabajar **siempre en pestañas de fondo** (`tabs_create` con `foreground:false` y `navigate` con `tabId`) y **no llamar nunca `tabs_select`** salvo que él pida expresamente mirar algo. Toda la extracción funciona igual en pestañas ocultas — la auditoría completa de las cinco webs se hizo así. Si él quiere navegar mientras tanto, que lo haga en Edge. Ver [[rutas-y-apis-gex]].
