---
name: autonomia-abrir-y-buscar
description: "Permiso explícito para abrir ATAS si está cerrado, consultar CME y resolver solo, sin pedir confirmación."
metadata:
  node_type: memory
  type: feedback
  modified: 2026-08-25T18:00:00.000Z
---

Dicho textual el 2026-08-25: *"si atas esta cerrado abrilo tenes mi total permiso para buscar y
encontrar lo que te pido o buscar en cme tambien vamos se mas autonomo"*.

**Hacer sin preguntar:**
- Abrir ATAS con `open_application` si está cerrado o quedó detrás de otra ventana.
- Reabrir el panel del navegador si se cerró.
- Consultar los endpoints de CME cuando haga falta el dato.
- Buscar el dato por la vía que sea necesaria antes de decir que no está disponible.

**Lo que sigue necesitando su OK:** cerrar o reiniciar ATAS, tocar configuración pesada con el
mercado abierto (ver [[cambios-atas-de-a-uno]]), y cualquier cosa que toque órdenes o posiciones.
Sigue en pie que **no se clickea el lienzo del gráfico** — ver [[navegar-atas-sin-pedir-permiso]].

**Nota operativa:** el cache del mapa de CME se guarda en `localStorage` bajo la clave
`gexES_YYYYMMDD`, y **vive en el origen `cmegroup.com`**. Para leerlo hay que estar en una pestaña
de ese dominio: desde `opensera.com` devuelve vacío. Ese detalle ya costó una descarga repetida.

**Why:** pedirle permiso para cosas que puedo resolver solo le corta el ritmo justo cuando está
mirando el mercado en vivo. Él ya lo dijo dos veces.

**How to apply:** buscar el dato uno mismo, y recién pedirle algo si de verdad está fuera de alcance
—un login suyo, una decisión sobre su plataforma o su dinero. Ver [[mirar-pantalla-antes-de-responder-atas]].
