# build_serial.ps1 — compila un proyecto con un candado global (Mutex) para que varios constructores en paralelo no se pisen en obj/ y bin/.
# Uso: powershell -ExecutionPolicy Bypass -File herramientas/build_serial.ps1 -Dir <carpeta del csproj> [-Salida bin/Release] [-Extra "..."]
# Sin -Salida compila con la salida por defecto del proyecto. Imprime las lineas de error y "Build succeeded" / "BUILD FALLO".
param([Parameter(Mandatory = $true)][string]$Dir, [string]$Salida = "", [string]$Extra = "")
$m = New-Object System.Threading.Mutex($false, "Global\PythiaGex4.BuildSerial")
$tengo = $false
try {
    try { $tengo = $m.WaitOne([TimeSpan]::FromMinutes(20)) } catch [System.Threading.AbandonedMutexException] { $tengo = $true }
    if (-not $tengo) { Write-Output "BUILD FALLO: no consegui el candado en 20 min"; exit 2 }
    Push-Location $Dir
    $args2 = @("build", "-c", "Release")
    if ($Salida -ne "") { $args2 += @("-o", $Salida) }
    if ($Extra -ne "") { $args2 += ($Extra -split " ") }
    $o = & dotnet @args2 2>&1 | Out-String
    Pop-Location
    $err = ($o -split "`n" | Where-Object { $_ -match " error " } | Select-Object -Unique | Select-Object -First 30) -join "`n"
    if ($o -match "Build succeeded") { Write-Output "Build succeeded"; if ($err) { Write-Output $err }; exit 0 }
    Write-Output "BUILD FALLO`n$err"; exit 1
}
finally { if ($tengo) { $m.ReleaseMutex() }; $m.Dispose() }
