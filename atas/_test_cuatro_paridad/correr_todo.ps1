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
# 4.1.5e: el arnes fam suma la seccion "finde" (SesionFamilia.De el fin de semana en modo NY, el modo UTC sin cambios y el aviso "mercado cerrado" del motor)
Paso "fam (B3d)" { $d = Join-Path $aqui "fam"; Build $d @(); Push-Location $d; $exe = Get-ChildItem -Recurse -Filter "fam_test.exe" bin | Select-Object -First 1; & $exe.FullName; Pop-Location }
Paso "fam integrado" { $d = Join-Path $aqui "fam\integrado"; Build $d @(); Push-Location $d; $exe = Get-ChildItem -Recurse -Filter "fam_integrado.exe" bin | Select-Object -First 1; & $exe.FullName; Pop-Location }
Paso "tqqq (B4)" { $d = Join-Path $aqui "tqqq"; Build $d @("-o","bin/Release"); Push-Location $d; & .\bin\Release\ArnesTqqq.exe; Pop-Location }
# 4.1.2 (B-pos): cambios por nivel con los datos reales (copia de PythiaGex4 en %TEMP%\pg4_posiciones; el origen solo se lee)
Paso "posiciones (B-pos 4.1.2)" { $d = Join-Path $aqui "posiciones"; Build $d @("-o","bin/Release"); Push-Location $d; & .\bin\Release\posiciones.exe; Pop-Location }
# 4.1.3: las dominantes como la 2.0 contra lo que dibujo la 2.0 (laboratorio/calibracion_1009/receta_2_0/paridad_2_0_minuto.csv, sesion 2026-10-09) y
# el motor con/sin extras (las 21 series identicas), persistencia, completado, cambios y etiqueta (datos reales copiados a %TEMP%\pg4_replica20)
Paso "replica20 (4.1.3)" { $d = Join-Path $aqui "replica20"; Build $d @("-o","bin/Release"); Push-Location $d; & .\bin\Release\replica20.exe; Pop-Location }
# 4.1.5: la estela 'NDX 0G' (Gamma) de la clasica (R10_NDX_zero) contra lo que la clasica hizo el 09-10 (AUDIT capa=NDX de laboratorio/calibracion_1009/receta_clasica
# y las muestras de su base en su log): formula, base de la rueda, todo 4.1, por minuto, motor con el compuesto, persistencia, completado, rehecho y etiqueta
# (datos reales copiados a %TEMP%\pg4_clasica)
Paso "clasica (4.1.5)" { $d = Join-Path $aqui "clasica"; Build $d @("-o","bin/Release"); Push-Location $d; & .\bin\Release\clasica.exe; Pop-Location }
# 4.1.5b: las dominantes D1-D3 de la capa NDX de la clasica (R10_NDX_dom, DominantesClasica): TODOS los minutos de niv-*.jsonl con R10_NDX_dom contra la
# cuenta en C# y el port Python verificado (laboratorio/calibracion_1009/receta_clasica_dominantes, python -I -B desde el exe), la rueda del 09-10 simulada
# con base 243,06 contra los AUDIT capa=NDX de la clasica (log, hora ART) donde la cadena coincide, y casos borde (OI, un lado, empate, ...).
# Datos reales copiados a %TEMP%\pg4_clasica_dom (los niv primero, despues las cadenas); el log de la clasica se lee compartido.
Paso "clasica_dom (4.1.5b)" { $d = Join-Path $aqui "clasica_dom"; Build $d @("-o","bin/Release"); Push-Location $d; & .\bin\Release\clasica_dom.exe; Pop-Location }
Paso "pantalla (B5)" { $d = Join-Path $aqui "..\PythiaGexCuatro\_modulos\pantalla\prueba"; Build $d @(); Push-Location $d; $exe = Get-ChildItem -Recurse -Filter "PruebaPantalla.exe" bin | Select-Object -First 1; & $exe.FullName; Pop-Location }
# 4.1.5c: compatibilidad del .ws (ATAS guarda los ajustes POR NOMBRE): la lista completa de propiedades publicas (nombre, tipo, atributos, default) de la DLL
# del primer paso (bin/Release) contra la 4.1.5 instalada (ws_compat/referencia, sha256 fijo): iguales salvo S_R10_NDX_dom; los cambios de [Display] son
# exactamente los de herramientas/grupo_arriba_415c.py. Solo reflexion e instancias recien creadas (sin OnInitialize); no toca ATAS.
# 4.1.5e: + W7 (herramientas/grupo_arriba_415e.py --verificar: el codigo coincide con la fuente de verdad y es idempotente) y W8 (la primera frase de las 101)
Paso "ws_compat (4.1.5c/e)" { $d = Join-Path $aqui "ws_compat"; Build $d @("-o","bin/Release"); Push-Location $d; & .\bin\Release\ws_compat.exe; Pop-Location }
Paso "punta a punta (e2e)" { Push-Location (Join-Path $aqui "e2e"); & powershell -ExecutionPolicy Bypass -File .\correr.ps1; Pop-Location }
"=== fin $(Get-Date -Format 'HH:mm:ss') ===" | Out-File -Append -Encoding utf8 $res
