---
name: dos-controles-backend-y-pantalla
description: "REGLA INQUEBRANTABLE del operador (27-11-2026): toda tarea, chica o compleja, cierra con DOS controles auditados: el de atras (analisis/codigo/calculo desde el crudo) y el de la PANTALLA FINAL del usuario, probando como un humano profesional hasta que se vea y funcione ahi; nunca 'listo' con solo backend"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-27T19:05:29.687Z
---

Textual (27-11-2026 ~16:05 AR): "como regla inquebrantable Claude, todas tus tareas ya sean desde las menores o hasta las mas complejas
siempre deben tener dos partes de control bien auditadas, es tu principal fallo: solo vas al backend... debe comprender tanto la tarea
analisis auditoria backend y la final en pantalla final usuario, proba hasta lograrlo en pantalla final simulando ser un humano
profesional, nada de solo backend codigo calculo y decir ya esta bien, todo auditado en pantalla final usuario para decir trabajo
terminado correctamente el ciclo flujo". Refuerza la regla 8 de CLAUDE.md y [[comprobacion-fehaciente-en-pantalla]].

**Why:** ese dia di por buenas cosas mirando logs/jsonl (ej. la 1.0e "instalada" con una captura VIEJA porque ATAS estaba tapado por
Claude) y el operador encontro en su pantalla lo que yo no (el cuadro pisando los datos de ATAS, la manija lejos del cuadro).

**How to apply:**
- Cierre de cada tarea = (1) backend auditado + (2) pantalla final: captura FRESCA (comprobar que el jsonl/version dibujada es la nueva;
  si ATAS esta tapado, forzar el dibujo con cambio de pestaña por UIA) y uso real como lo haria el operador (clic, minimizar, abrir,
  arrastrar), mirando cada numero, raya y rotulo contra el dato crudo.
- Seguridad que NO se negocia mientras el esta en la PC: los clics/arrastres de prueba sobre ATAS solo en un grafico con "Trading is
  locked" (sus teclas son atajos de ordenes: [[foco-atas-atajos-trading]]); avisarle en una linea antes; devolver el foco al terminar.
  En un grafico sin candado, la prueba humana la hace el o se simula el mismo camino de codigo por archivo, y se dice cual de las dos fue.
- Nunca escribir "terminado" sin las dos partes; si falta la de pantalla, decir que falta y por que.
