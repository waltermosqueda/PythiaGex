# RESPALDO A LA CARPETA SINCRONIZADA (Escritorio\Inversiones\PythiaGex-respaldo).
#
# Copia espejo (robocopy /MIR) de todo lo que no vive en GitHub o que conviene
# tener a mano si se pierde la PC:
#   - el repo entero (codigo, datos/databento, datos/simulador, .git)
#   - lo que el indicador escribe en %APPDATA%\ATAS (centinelas, logs, cadenas archivadas, viva)
#   - las memorias de Claude de este proyecto
# Idempotente: correrlo de nuevo solo copia lo que cambio. Pensado para una
# tarea programada diaria o para el final de cada sesion.
$ErrorActionPreference = "Continue"
$destino = Join-Path $env:USERPROFILE "OneDrive\Escritorio\Inversiones\PythiaGex-respaldo"
$raiz    = Join-Path $env:USERPROFILE "OneDrive\Escritorio\ATAS nada"
$appdata = Join-Path $env:APPDATA "ATAS"
$mem     = Join-Path $env:USERPROFILE ".claude\projects\C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada\memory"
New-Item -ItemType Directory -Force -Path $destino | Out-Null

robocopy "$raiz\PythiaGex" "$destino\PythiaGex" /MIR /R:1 /W:1 /NFL /NDL /NJH /XD "bin" "obj" "__pycache__" | Out-Null
robocopy "$raiz" "$destino\ATAS-nada-raiz" /R:1 /W:1 /NFL /NDL /NJH /LEV:1 | Out-Null
robocopy "$appdata\PythiaGex" "$destino\appdata-ATAS-PythiaGex" /MIR /R:1 /W:1 /NFL /NDL /NJH | Out-Null
robocopy "$appdata" "$destino\appdata-ATAS-archivos" /R:1 /W:1 /NFL /NDL /NJH /LEV:1 pythiagex-*.* | Out-Null
robocopy "$mem" "$destino\memoria-claude" /MIR /R:1 /W:1 /NFL /NDL /NJH | Out-Null

$tam = (Get-ChildItem $destino -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
"{0}  respaldo en {1}: {2:N0} MB" -f (Get-Date -Format "yyyy-MM-dd HH:mm"), $destino, $tam | Tee-Object -FilePath (Join-Path $destino "ultimo-respaldo.txt") -Append
