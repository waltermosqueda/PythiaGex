---
name: regla-auditar-varias-veces
description: "Regla del operador (15-09-2026): con cada tarea, analizar en profundidad todo lo que se construye, auditarlo varias veces (numeros contra recalculo independiente Y visualmente en pantalla), y no dar nada por sentado sin prueba cientifica/logica. Un comando lo hace: laboratorio/auditar_todo.py."
metadata:
  type: feedback
---

El operador lo dijo textual el 15-09-2026 a las 19:15, despues de una tarde de cambios visuales rapidos: "con las
tareas que te doy se profesional, analiza con profundidad todo lo que construis, siempre regla: audita varias veces,
tambien visualmente, finalmente no des nada por sentado sin pruebas cientificas logicas".

**Why:** ese mismo dia se dieron por sentadas dos cosas que no eran: que la primaria del grafico era QQQ (a las
19:14 era NDX: el operador cambia el libro sin avisar) y que ATAS "no arrancaba" por un DLL (era el login lento).
Las dos se resolvieron mirando la evidencia (log y pantalla), no la suposicion.

**How to apply:** despues de cada cambio: (1) compilar e instalar, (2) mirar la pantalla con captura y zoom, (3)
correr `python laboratorio/auditar_todo.py MNQ M2` (recalculo independiente de cada libro, primaria contra su capa
gemela en la misma vela, respeto contra placebo, lista de lo supuesto), (4) escribir en el registro que se verifico
y con que numero, y (5) decir explicitamente que queda supuesto. Nunca afirmar que algo "coincide" o "anda" sin
el numero al lado. Ver [[verificar-yo-no-el-usuario]], [[mirar-pantalla-antes-de-responder-atas]],
[[auditoria-punta-a-punta]].
