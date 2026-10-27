---
name: arranque-automatico-atas
description: "16-10-2026: tarea programada de Windows '\\PythiaGex\\ATAS - arranque' abre ATAS lun-vie 10:00 (hora AR de la PC), aprieta Connect y espera Rithmic; probada en vivo (58 s); registro pythiagex-arranque.log"
metadata:
  node_type: memory
  type: project
  originSessionId: 1c10d3db-7e2b-4280-869b-0e37fa7ec916
  modified: 2026-10-16T06:54:45.674Z
---

Pedido 16-10-2026 03:49 ART: "que arranque ATAS a las 10 am hora argentina" + probarlo con una hora futura.

- `PythiaGex/herramientas/arrancar_atas.ps1`: si ATAS ya corre no hace nada; si no, lo lanza, invoca "Connect" (UIA, buscando SOLO dentro
  de la ventana de ATAS; usa la clave recordada, no guarda credenciales), maximiza y espera en `Logs/app_<dia>.log` las lineas
  "Auth : Connected.", "Rithmic[...] connected." y "Connecting finished with state: Connected". Registro: `%APPDATA%\ATAS\pythiagex-arranque.log`.
- `programar_arranque_atas.ps1` registra `\PythiaGex\ATAS - arranque` (sin admin): lunes a viernes 10:00, sesion interactiva del usuario,
  WakeToRun (la PC tiene wake timers en AC y se suspende a las 2 h), StartWhenAvailable, tope 15 min. `-PruebaEnMinutos N` crea una prueba
  unica aparte; `-Quitar` borra ambas; `-Hora`/`-Dias` cambian el horario.
- Prueba real 16-10 03:53:00: tarea resultado 0, ATAS arranco 03:53:02, Connect 03:53:16, Rithmic conectado 03:53:36, LISTO en 58 s;
  captura con 'lucid (Rithmic)' en verde. Tarea de prueba borrada despues.
- Sin probar: con la sesion de Windows BLOQUEADA (UIA puede no llegar al boton Connect) ni con la sesion cerrada (la tarea interactiva no corre).

**Why:** el operador quiere ATAS listo a las 10 AR sin tocar nada.
**How to apply:** ante "ATAS no abrio solo", leer pythiagex-arranque.log y `Get-ScheduledTaskInfo -TaskPath \PythiaGex\ -TaskName 'ATAS - arranque'`
(LastTaskResult) antes de opinar. Ver [[no-reiniciar-atas-con-posicion]], [[reconectar-atas-uia]].
