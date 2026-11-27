---
name: mirar-pantalla-antes-de-responder-atas
description: "La pantalla de ATAS es LA fuente y se audita siempre; desde 11-2026 con foto PASIVA (xr.foto, sin foco: sus teclas operan) antes de responder y diciendo de donde sale cada numero (pantalla con hora / cinta / registro del indicador); un arreglo que no se ve en el grafico no esta resuelto"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-27T17:01:43.970Z
---

Regla del operador, repetida y endurecida el 2026-09-10: "no podes suponer que esta arreglado algo
y no mirar la pantalla de ATAS, el grafico real. Si vos arreglas el backend pero en la pantalla no se
ve resuelto, no esta resuelto, fin." Y: "como no vas a revisar la pantalla si es la fuente de todo".
Esa noche respondi una auditoria entera sin mirar ATAS porque quedo detras de la ventana del chat
y no la quise traer al frente sin su OK: eso fue un error, el lo dijo con todas las letras.

**Why:** el toma decisiones con lo que ve en el grafico, no con los logs; los logs son mi
herramienta, la pantalla es la de el.

**How to apply:** antes de responder sobre cualquier cosa de ATAS: traer la ventana de ATAS al
frente (activarla por PowerShell / UIA, o correr la del chat), captura de pantalla, mirar el
grafico real (rayas, guiones, pelotitas, rotulos, base en la cabecera), y recien ahi responder con
lo que se ve. Despues de cada instalacion/reinicio: captura obligatoria. Nunca escribir "no pude
mirar la pantalla". Ver [[no-reiniciar-atas-con-posicion]], [[verificar-yo-no-el-usuario]].

**Actualizacion 27-11-2026:** traer ATAS al frente YA NO (sus teclas son atajos de trading y opera en vivo: ver
[[foco-atas-atajos-trading]], [[no-mover-mouse-si-opera]]); la foto es PASIVA: `xr.foto(xr.ventanas(r'^ATAS - ')[0])` (PrintWindow,
devuelve un array BGRA de numpy: convertir con PIL antes de guardar). Ese dia pregunto "¿como estas revisando ATAS en vivo, contra el
backend? estoy acostumbrado a que veas mi pantalla y me das respuestas como si lo hicieras": yo habia trabajado casi todo desde los
archivos (el jsonl de La Ola, que guarda lo que dibuja; la cinta de la sonda; el log de ATAS) y una sola foto pasiva. Desde ahi:
foto pasiva ANTES de responder sobre lo que se ve, y en la respuesta decir de donde sale cada cosa ("en tu pantalla de las
13:57:54 se ve...", "en el registro de La Ola...", "en la cinta..."). El MCP de ATAS (Connect AI, puerto 4823) no conecto ese dia
(ECONNREFUSED) y de todos modos no da valores ([[atas-8-0-16-ai-connect]]).
