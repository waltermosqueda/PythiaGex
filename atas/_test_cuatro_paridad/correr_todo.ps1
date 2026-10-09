# correr_todo.ps1 — corre TODOS los arneses de la 4.1 en serie (prioridad baja) y deja un resumen en resultados_todo.txt.
# Uso: powershell -File atas/_test_cuatro_paridad/correr_todo.ps1
$ErrorActionPreference = "Continue"
$aqui = Split-Path -Parent $MyInvocation.MyCommand.Path
$res = Join-Path $aqui "resultados_todo.txt"
"=== correr_todo $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ===" | Out-File -Encoding utf8 $res
try { (Get-Process -Id $PID).PriorityClass = 'BelowNormal' } catch {}

function Paso($nombre, [scriptblock]$bloque) {
    $t0 = Get-Date
    "--- $nombre" | Out-File -Append -Encoding utf8 $res
    try {
        $salida = & $bloque 2>&1 | Out-String
        $code = $LASTEXITCODE
    } catch { $salida = $_.ToString(); $code = 99 }
    $cola = ($salida -split "`n" | Select-Object -Last 12) -join "`n"
    "$cola`nexit=$code  ($([int]((Get-Date) - $t0).TotalSeconds) s)" | Out-File -Append -Encoding utf8 $res
}

function Build($dir, $extra) {
    Push-Location $dir
    $o = & dotnet build -c Release @extra 2>&1 | Out-String
    Pop-Location
    if ($o -notmatch "Build succeeded") { throw "BUILD FALLO en $dir`n$(($o -split "`n" | Select-String ' error ' | Select-Object -First 8) -join "`n")" }
}

# 4.1.2: la DLL se compila SOLO con build_serial.ps1 (candado global: puede haber otros constructores compilando en paralelo)
Paso "build PythiaGexCuatro (la DLL)" { & powershell -ExecutionPolicy Bypass -File (Join-Path $aqui "..\..\herramientas\build_serial.ps1") -Dir (Join-Path $aqui "..\PythiaGexCuatro") -Salida "bin/Release" }
Paso "adaptadores (B1)" { Push-Location (Join-Path $aqui "adaptadores"); & powershell -ExecutionPolicy Bypass -File .\correr.ps1; Pop-Location }
Paso "cboe (B2)" { Push-Location (Join-Path $aqui "cboe"); & powershell -ExecutionPolicy Bypass -File .\correr.ps1; Pop-Location }
Paso "nq (B3a)" { $d = Join-Path $aqui "nq"; Build $d @("-o","bin/Release"); Push-Location $d; & dotnet bin/Release/ParidadNq.dll; Pop-Location }
Paso "ndx (B3b)" { $d = Join-Path $aqui "ndx"; Build $d @(); Push-Location $d; & .\bin\Release\net10.0-windows\ArnesNdx.exe; Pop-Location }
Paso "qqq (B3c)" { $d = Join-Path $aqui "qqq"; Build $d @("-o","bin/Release"); Push-Location $d; & .\bin\Release\qqq_paridad.exe 2026-10-07 2026-10-08 --modo ambos; Pop-Location }
Paso "fam (B3d)" { $d = Join-Path $aqui "fam"; Build $d @(); Push-Location $d; $exe = Get-ChildItem -Recurse -Filter "fam_test.exe" bin | Select-Object -First 1; & $exe.FullName; Pop-Location }
Paso "fam integrado" { $d = Join-Path $aqui "fam\integrado"; Build $d @(); Push-Location $d; $exe = Get-ChildItem -Recurse -Filter "fam_integrado.exe" bin | Select-Object -First 1; & $exe.FullName; Pop-Location }
Paso "tqqq (B4)" { $d = Join-Path $aqui "tqqq"; Build $d @("-o","bin/Release"); Push-Location $d; & .\bin\Release\ArnesTqqq.exe; Pop-Location }
# 4.1.2 (B-pos): cambios por nivel con los datos reales (copia de PythiaGex4 en %TEMP%\pg4_posiciones; el origen solo se lee)
Paso "posiciones (B-pos 4.1.2)" { $d = Join-Path $aqui "posiciones"; Build $d @("-o","bin/Release"); Push-Location $d; & .\bin\Release\posiciones.exe; Pop-Location }
# 4.1.3: las dominantes como la 2.0 contra lo que dibujo la 2.0 (laboratorio/calibracion_1009/receta_2_0/paridad_2_0_minuto.csv, sesion 2026-10-09) y
# el motor con/sin extras (las 21 series identicas), persistencia, completado, cambios y etiqueta (datos reales copiados a %TEMP%\pg4_replica20)
Paso "replica20 (4.1.3)" { $d = Join-Path $aqui "replica20"; Build $d @("-o","bin/Release"); Push-Location $d; & .\bin\Release\replica20.exe; Pop-Location }
Paso "pantalla (B5)" { $d = Join-Path $aqui "..\PythiaGexCuatro\_modulos\pantalla\prueba"; Build $d @(); Push-Location $d; $exe = Get-ChildItem -Recurse -Filter "PruebaPantalla.exe" bin | Select-Object -First 1; & $exe.FullName; Pop-Location }
Paso "punta a punta (e2e)" { Push-Location (Join-Path $aqui "e2e"); & powershell -ExecutionPolicy Bypass -File .\correr.ps1; Pop-Location }
"=== fin $(Get-Date -Format 'HH:mm:ss') ===" | Out-File -Append -Encoding utf8 $res
