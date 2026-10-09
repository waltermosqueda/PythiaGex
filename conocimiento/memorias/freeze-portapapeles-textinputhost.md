---
name: freeze-portapapeles-textinputhost
description: "La PC se congela al abrir Win+V por un bucle infinito en TextInputHost.exe, no por corrupción de archivos."
metadata: 
  node_type: memory
  type: project
  originSessionId: 39e678a6-19fe-4e4f-bcac-6284534defd4
  modified: 2026-09-24T17:52:59.820Z
---

Problema recurrente desde hace meses (diagnosticado el 2026-08-06): al abrir el portapapeles (Win+V) la PC se freezea y solo se recupera matando "Windows Input Experience" (TextInputHost.exe).

Causa raíz medida en vivo: un único hilo de `TextInputHost.exe` gira al 98% de un núcleo dentro de `win32u.dll!NtUserPeekMessage` — bucle de mensajes infinito. Acumuló 5,73 h de CPU en 13,1 h de uptime (explorer.exe, comparado: 96 segundos). Coincide con un bug documentado de Windows 11 24H2/25H2 donde Win+V y Win+C cuelgan textinputhost.exe.

**NO es corrupción de archivos**: `sfc /scannow` corrió el 2026-08-06 17:21 y terminó en `Repair complete` sin una sola línea `Cannot repair member file` en `C:\Windows\Logs\CBS\CBS.log`. No insistir con SFC.

Hallazgos secundarios en esa PC: `nViewH64.dll` (NVIDIA nView, driver Quadro P600) inyectado como hook global en explorer.exe y `nviewMain64.exe` crasheó el 2026-08-03; `edgehtml.dll` (motor legacy) cargado dentro de TextInputHost; ~40 procesos msedgewebview2.

**Cómo aplicarlo:** ante un reporte de "se tilda el portapapeles", medir `(Get-Process TextInputHost).TotalProcessorTime` en dos momentos en vez de asumir corrupción. El script `Capturar-FreezePortapapeles.ps1` en el escritorio (carpeta "ATAS nada") captura el estado durante el freeze. Ver [[mirar-pantalla-antes-de-responder-atas]].

## Volvio el 2026-09-02, y esta vez rompio ATAS

Durante toda la sesion del VWAP los clics fallaban, ATAS congelaba el render y
el teclado del control de pantalla devolvia
`"Textinputhost" is not in the allowed applications and is currently in front`.

`Get-Process TextInputHost` mostraba **28.349 segundos de CPU acumulada** — casi
ocho horas girando en vacio. Estaba robando el foco cada pocos segundos.

**La solucion es matarlo; Windows lo relanza solo cuando hace falta:**

```powershell
Get-Process TextInputHost | Stop-Process -Force
```

**Sintoma nuevo para reconocerlo rapido:** las capturas de pantalla se
**congelan** — devuelven una imagen vieja y el reloj de la barra de tareas no
avanza entre capturas separadas por minutos. Un `mouse_move` fuerza una captura
fresca; si el reloj sigue clavado, mirar TextInputHost antes que nada.

**Chequearlo al inicio de cada sesion larga** junto con los permisos: si tiene
miles de segundos de CPU, matarlo antes de empezar. Ver
[[clics-que-no-llegan-y-loops]].

## 24-09: causa exacta y vigia automatico

Desactivar "Acciones sugeridas" y nView (06-08) NO lo arreglo: volvio (51.700 s de
CPU en 35,8 h, un solo hilo al 97 %). Pila medida sin depurador (dbghelp): el hilo
XAML de una vista de TextInputHost despacha un mensaje a la ventana
`Internet Explorer_Hidden` de **edgehtml.dll** (motor web viejo) y queda en un
PeekMessage anidado que nunca sale. Ese hilo nacio al arrancar la PC; su CoreWindow
ya no existe. Bug de Microsoft sin arreglo (foro techcommunity, mayo 2026).

Matarlo NO pierde el historial: vive en cbdhsvc (23 elementos intactos tras matarlo).

**Vigia instalado 24-09, con OK explicito del operador** ("te doy todos los permisos
pero resolvelo"; su objetivo: que lo mate ANTES de que abra Win+V, no enterarse del
bug en vivo). En `herramientas/vigia-portapapeles/` (VigiaTextInputHost.exe, C#
compilado con el csc de Windows, ~24 MB): tarea programada de usuario
`VigiaTextInputHost` (al iniciar sesion + relanzar cada 5 min). Revisa cada 5 s:
GIRO = proceso >70 % y un hilo >60 % de un nucleo en 15 s; CUELGUE = CoreWindow con
IsHungAppWindow (en pantalla ~10 s, oculta 30 s). Anota la pila en `vigia.log`,
mata, y anota cuanto tardo Windows en relanzarlo. Freno: >6 muertes/hora = pausa
30 min. En reposo las 4 CoreWindow estan visibles pero cloaked=2 y responden.
Primera version (60 s) mato al colgado real a las 14:34; el nuevo quedo en 0 %.
Reinstalar/actualizar: `instalar.ps1` (frena, recompila, arranca). Sacar: `desinstalar.ps1`.

**Probado con simuladores** (un TextInputHost.exe falso, borrado despues): giro
cazado a los 18 s, cuelgue en pantalla a los ~12 s, cuelgue oculto a los 35 s, el
sano y el real intactos, freno activado en la 6a muerte. La prueba encontro un bug
mio: con dos procesos del mismo nombre el reloj de cuelgue de uno se borraba al
revisar el otro (arreglado: limpieza global en Revisar). En el log, 14:47-14:51 son
simulacros; el unico real es 14:34. Para volver a probar, recompilar el simulador
(fuente en el scratchpad de la sesion f9910eb5, no en el repo).
