---
name: disco-lleno-heatmap-2810
description: "28-10: C: con 6,65 GB libres apago el heatmap de ATAS (umbral 10 GB). Limpieza de copias y caches 7,15 -> 17,46 GB; causa: grabaciones de Recortes guardadas x3 (~5,6 GB por grabacion); quedan VM macOS 30 GB, Docker 13 GB, heatmap viejo 4 GB a decision suya"
metadata:
  node_type: memory
  type: project
  originSessionId: 404dab2f-b5f6-48fa-ba08-3ab764cbbdd8
  modified: 2026-10-29T00:18:35.622Z
---

28-10 ~21:05 AR: ATAS mostro "Heatmap was disabled because free disk space is below 10 GB" (lo chequea AL ABRIR la pestaña de
heatmap; no se reactiva solo: hay que reabrir la pestaña "MNQ Heatmap" o reiniciar ATAS). Pedido: "haceme una limpieza... no me rompas windows".

**Hecho (sin perder nada unico):** 3 grabaciones de pantalla duplicadas byte a byte (se borraron las de `Videos\Recording ...`; quedan las de
`Videos\Screen Recordings\`), la copia temporal de Recortes (`Packages\Microsoft.ScreenSketch_...\TempState\Recordings`), CrashDumps viejos,
cache de pip, `Code\CachedExtensionVSIXs`, instalador viejo de LM Studio. Libre: 7,15 -> 17,46 GB. Escaneo: `scratchpad uso_disco.py` / `sub_disco.py`.

**Causa principal:** cada grabacion de los martes ~19:33 (1,6-1,9 GB) queda TRES veces (auto-guardado + "guardar como" + temporal) = ~5,6 GB.
Crecimiento fijo: heatmap de ATAS ~4,3 GB/mes (MNQ); datos PythiaGex en %APPDATA%\ATAS ~17 GB + respaldo en Inversiones 7,8 GB en el mismo disco.

**Queda a decision del operador (datos unicos, no los borro yo):** VM macOS en `OneDrive\Documentos\Virtual Machines\macOS 14` 29,8 GB (sin
uso desde 10-04; OneDrive NO la tiene en la nube: atributos solo Archive), Docker `AppData\Local\Docker\wsl\disk\docker_data.vhdx` 13 GB (sin
uso desde 27-06) + programa 4,5 GB, heatmap viejo `ATAS\Cache_v2\Heatmap` MNQ 2026-09 3,3 GB y MES 2026-08 0,8 GB, videos de YouTube en Descargas.
BlueStacks (12,8 GB) SE USA (log del 28-10): no tocar. hiberfil 6,8 GB necesita admin (la sesion no es admin).
**How to apply:** ante "disco lleno" medir con uso_disco.py; borrar solo copias verificadas byte a byte y caches; lo unico se lista para el.
Nunca reiniciar ATAS ni reabrir pestañas con posicion abierta (ver [[no-mover-mouse-si-opera]]). Ver [[cache-velas-atas-podada]].
