# correr.ps1 — re-corre TODAS las pruebas del modulo cboe (B2) de PythiaGex 4.1, sin red y sin ATAS (prioridad baja).
# Uso:  powershell -ExecutionPolicy Bypass -File correr.ps1 [-Historia <carpeta con cadena-*-dia.jsonl.gz de una semana>]
# La bajada REAL (una por ticker) ya se hizo el 08-10 23:20 UTC y quedo en resultados\bajada; aca solo se re-compara.
# Para repetirla (NO con el mercado abierto ni en rafaga):  bin\Release\PruebaCboe.exe bajar resultados\bajada --otra-vez
param([string]$Historia = "")
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
[System.Diagnostics.Process]::GetCurrentProcess().PriorityClass = "BelowNormal"
$exe = Join-Path $PSScriptRoot "bin\Release\PruebaCboe.exe"
dotnet build -c Release -o bin/Release | Select-String -Pattern "error|Build succeeded"
New-Item -ItemType Directory -Force resultados | Out-Null
$fallas = 0
function Paso($nombre, [scriptblock]$b) { Write-Host "=== $nombre"; & $b; if ($LASTEXITCODE -ne 0) { $script:fallas++ ; Write-Host "  -> FALLO ($LASTEXITCODE)" } }
Paso "round/repr de Python" { & $exe py resultados\nums.txt; if ($LASTEXITCODE -eq 0) { python -I comparar_cboe.py py resultados\nums.txt } }
Paso "construir/medir/linea flaca sobre 6 crudos guardados" { & $exe crudos resultados\crudos (Get-ChildItem crudos\*.json.gz | ForEach-Object FullName); if ($LASTEXITCODE -eq 0) { python -I comparar_cboe.py crudos resultados\crudos } }
Paso "lector de lineas sobre archivos de cboe-local (copia)" { $a = (Get-ChildItem archivo\*.jsonl.gz | ForEach-Object FullName); & $exe lineas resultados\lineas_cboe_local.json $a; if ($LASTEXITCODE -eq 0) { python -I comparar_cboe.py lineas resultados\lineas_cboe_local.json $a } }
Paso "la bajada real del 08-10 23:20 UTC (re-comparacion, sin red)" { python -I comparar_cboe.py bajada resultados\bajada }
$tmp = Join-Path $env:TEMP "pythiagex4_cboe_prueba_hilo"
Paso "el hilo con HTTP falso (dedup, archivo, relectura, seguidor, fallos, 403, parar)" { & $exe hilo $tmp crudos_hilo }
if ($Historia -ne "") { Paso "carga de historia (tiempo y memoria)" { & $exe historia $Historia 2026-10-08T19:50:00Z 30; & $exe historia $Historia 2026-10-08T19:50:00Z 0 } }
if ($fallas -eq 0) { Write-Host "TODAS LAS PRUEBAS OK" } else { Write-Host "$fallas PRUEBA(S) CON FALLAS" ; exit 1 }
