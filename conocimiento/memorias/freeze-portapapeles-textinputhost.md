---
name: freeze-portapapeles-textinputhost
description: "La PC se congela al abrir Win+V por un bucle infinito en TextInputHost.exe, no por corrupción de archivos."
metadata: 
  node_type: memory
  type: project
  originSessionId: 39e678a6-19fe-4e4f-bcac-6284534defd4
  modified: 2026-08-06T20:39:40.452Z
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
