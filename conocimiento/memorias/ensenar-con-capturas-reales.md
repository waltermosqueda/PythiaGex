---
name: ensenar-con-capturas-reales
description: "Los tutoriales van con capturas reales de pantalla señalando cada cosa, paso a paso; el texto abstracto no le sirve para aprender."
metadata:
  node_type: memory
  type: feedback
  modified: 2026-08-26T17:50:00.000Z
---

Dicho textual el 2026-08-26: *"te pedi con capturas reales tipo guia de cada cosa paso a paso desde
el comienzo hasta el final... sino como voy a saber con solo palabras texto abstracto. Una imagen
real con su ubicacion instructivo paso a paso vale mas que palabras sueltas."*

**Cuando pide que le enseñe dónde está algo o cómo se hace:**

1. **Abrir la pantalla real y capturarla.** No describir de memoria ni de un JSON.
2. **Un `zoom` por cada elemento** que se explica — la barra de navegación, el encabezado de la
   tabla, la fila concreta. Recortes chicos, no la pantalla entera.
3. **La captura primero, el texto después**, y el texto refiriéndose a lo que se ve en esa imagen.
4. **Numerar los pasos** de principio a fin, sin saltos.

**El obstáculo técnico y cómo resolverlo:** el panel del navegador integrado **no puede
capturarse si no está desplegado en la pantalla** — no compone frames y `screenshot` da timeout.
Salidas: (a) abrir la página en Edge con `Start-Process <url>` desde PowerShell y capturar con
computer-use pidiendo acceso a Edge —se concede en tier `read`, alcanza para ver y hacer zoom
pero **no para scrollear ni clickear**—; o (b) pedirle que despliegue el panel. No usar la
limitación como excusa para entregar solo texto.

**Why:** dos veces seguidas entregué explicaciones correctas pero sin imagen y no le sirvieron.
Aprende mirando, no leyendo. Ya estaba en [[como-ensenarle-trading]] que prefiere visuales al
texto; esto lo endurece: **capturas reales de su propia pantalla**, no diagramas inventados.

**How to apply:** vale para CME, ATAS, cualquier web y cualquier configuración. Ver
[[como-ensenarle-trading]], [[nombrar-niveles-tecnico-y-criollo]] y
[[mirar-pantalla-antes-de-responder-atas]].
