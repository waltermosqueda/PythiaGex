# correr.ps1 — prueba de punta a punta de la 4.1 fuera de ATAS. Compila y corre los escenarios en orden, de a uno, con prioridad BAJA
# (mercado abierto: nada en paralelo). Resultados en .\resultados\e2e-<escenario>.txt. Carpeta temporal: %TEMP%\pg4_e2e (o -RaizTmp).
# Uso: powershell -File correr.ps1 [-Escenarios ny,ny-reinicio,ny-recalculo,utc] [-RaizTmp <dir>]
param([string[]]$Escenarios = @("ny", "ny-reinicio", "ny-recalculo", "utc", "ny-union", "ny-vivo", "ny-extras"), [string]$RaizTmp = "")
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
cmd /c 'start "" /belownormal /wait /b dotnet build -c Release -o bin/Release -nologo -v:q'
if ($LASTEXITCODE -ne 0) { throw "no compila" }
foreach ($e in $Escenarios) {
    $extra = if ($RaizTmp) { " --raiz-tmp `"$RaizTmp`"" } else { "" }
    Write-Host "== $e"
    cmd /c "start `"`" /belownormal /wait /b dotnet bin\Release\e2e.dll $e$extra"
    Write-Host "   salida: resultados\e2e-$e.txt (codigo $LASTEXITCODE)"
}
