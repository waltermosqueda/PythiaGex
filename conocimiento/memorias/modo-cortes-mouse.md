---
name: modo-cortes-mouse
description: "13-11: el operador quiere usar su PC mientras trabajo; todo lo que mueve el mouse (cosechas de X-Ray, capturas por hover) corre en MODO CORTES (laboratorio/xray/cortes.py): solo con 10 min sin mouse/teclado y se frena al instante si toca algo, devolviendole su ventana"
metadata:
  node_type: memory
  type: feedback
  originSessionId: f3419dc9-e1cb-4b1b-99aa-8707c1a3c2f4
  modified: 2026-11-14T00:12:19.865Z
---

Pedido 13-11 ~21:20 ART: "quiero ocupar la pc y no me deja porque vos te adueñas del control; buscá un modo que nos deje
trabajar a ambos". Medido ese dia: ATAS lee la posicion REAL del cursor (el Scrub/FollowCursor de X-Ray no responde a
WM_MOUSEMOVE enviados a la ventana, ni al contenedor WinForms ni a la subventana OpenGL "GLFW30"), asi que no hay hover
escondido. PrintWindow(PW_RENDERFULLCONTENT) SI captura el grafico tapado (x14_fondo.py).

Frenos (13-11 21:15, tras "estuve 5 minutos tratando de escribir una palabra"): (1) mover el mouse/tocar tecla -> pausa < 1 s
y le devuelve su ventana; (2) ESCAPE mata el script al instante (solo mientras el cosechador tiene el mouse); (3) clic del operador
-> pausa; (4) `Escritorio\FRENAR CLAUDE.bat` mata cualquier script mio que use el mouse (x01/x05/x11/x14).

**Why:** un cosechador que mueve el mouse 40 min le bloquea la PC al operador.
**How to apply:** cualquier script que use el mouse importa `cortes.py` (esperar_libre / vigilar / devolver; umbral
--idle 600 s); el analisis de datos y los agentes no tocan el mouse y pueden correr siempre. No usar computer-use para
clicks mientras el operador esta usando la PC salvo que lo pida. Ver [[xray-historia-medida]].
