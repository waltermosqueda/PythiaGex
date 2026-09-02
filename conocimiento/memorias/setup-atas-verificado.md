---
name: setup-atas-verificado
description: "Su ATAS es licencia Ultra vitalicia, versión 8.0.14.397, feed Rithmic; opera MES/ES intradía."
metadata:
  type: project
---

Verificado en pantalla el 2026-08-17: licencia **Ultra con vencimiento 31/12/9999** (vitalicia), versión 8.0.14.397, conexión **Rithmic** (perfil "lucid"), cuenta demo <cuenta>-...-TEST005 de USD 25.000. Opera **MES** en gráficos de 1m, 5m y 30m. Tiene instalados ATAS Platform y ATAS X, más Rithmic Trader Pro y Tradovate.

El **Tablero de opciones β** ya le funciona sobre Rithmic: carga la cadena de ES con bid/ask, IV, delta, theta, y desde el ícono de configuración se pueden activar las columnas de **Open Interest y Gamma** (traen datos reales). **CORREGIDO el 2026-08-19:** sí hay vencimientos **diarios, incluido 0DTE** — estaban ocultos porque el filtro *Series Type* viene en "Regular"; hay que ponerlo en **"All Types"**. Las griegas y el OI se pueblan bien. El límite real que queda es que **no existe ninguna vista que agregue** la gamma de toda la cadena. Detalle completo en [[atas-opciones-es]].

**Why:** al ser Ultra vitalicia no necesita pagar nada de ATAS nunca más, y ya cumple el requisito de versión 8.0.14+ para el módulo de opciones. Eso descarta toda recomendación de "subí de plan".

**How to apply:** no le recomiendes upgrades de ATAS ni herramientas de order flow alternativas (Bookmap, Jigsaw, Sierra) — ya tiene lo mejor. Los indicadores anunciados *Options Key Levels* y *Options GEX Profile* todavía NO están entregados y van a requerir conexión a Interactive Brokers; verificar antes de afirmar que existen. Ver [[plan-gamma-gex]] y [[mirar-pantalla-antes-de-responder-atas]].

**Actualización 2026-08-19 (19:23 AR):** versión ahora **8.0.14.397-latest**, servidor F2, perfil Rithmic "lucid". Opera **#MESU6** (micro, septiembre) con gráficos M5 apilados y heatmap al costado, sesión ETH. Los niveles de GEX valen igual para MES y ES: mismo índice, mismo precio, solo cambia el multiplicador.

**El proceso NO se llama ATAS.** Se llama **`OFT.Platform`**, y el ejecutable es `C:\Program Files (x86)\ATAS Platform\OFT.Platform.exe`. Chequear con `Get-Process -Name "ATAS*"` devuelve vacío aunque ATAS esté abierto hace horas. Pasó el 2026-09-02: se dio por cerrada una instancia que estaba corriendo con el workspace cargado y se abrió una segunda encima. Lo correcto es `Get-Process -Name "OFT.Platform" | Select-Object Id, StartTime, MainWindowTitle` — la instancia real tiene el título `ATAS - [Default workspace]` y unos 3 GB de memoria; una recién lanzada todavía no tiene título. Si se abrió una de más, cerrarla por PID comparando `StartTime`, nunca por nombre.
