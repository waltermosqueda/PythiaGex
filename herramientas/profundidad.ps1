# PROFUNDIDAD 3.0: lanza (o para) el motor, el servidor de la pagina local y la historia, SIN ventana, en prioridad BELOW_NORMAL y sin duplicar.
#
#   powershell -ExecutionPolicy Bypass -File herramientas\profundidad.ps1            lanza lo que falte y muestra la URL
#   powershell -ExecutionPolicy Bypass -File herramientas\profundidad.ps1 -estado    solo dice que esta corriendo (pid y prioridad)
#   powershell -ExecutionPolicy Bypass -File herramientas\profundidad.ps1 -parar     para el motor, el servidor, la historia y el guardian
#   powershell -ExecutionPolicy Bypass -File herramientas\profundidad.ps1 -solo historia           lanza SOLO ese (si falta); los demas ni se miran
#   powershell -ExecutionPolicy Bypass -File herramientas\profundidad.ps1 -parar -solo historia    para SOLO ese
#   opciones: -cada 2 (segundos entre vueltas del motor; sin cambios en las entradas rehace cada 15 s), -puerto 8765,
#             -cadaHistoria 60 (segundos entre escrituras de historia.json), -solo motor|servidor|historia|guardian
#
# Que lanza (desde la raiz del repo PythiaGex, con pythonw = sin consola):
#   pythonw profundidad\motor.py bucle --cada 2    -> escribe profundidad\estado\estado.json cuando algo cambio (cada 2 s como maximo; log: %APPDATA%\ATAS\profundidad-motor.log)
#   pythonw profundidad\servir.py --puerto 8765    -> sirve la pagina en http://localhost:8765/           (log: %APPDATA%\ATAS\profundidad-servidor.log)
#   pythonw profundidad\historia.py bucle --cada 60 -> escribe profundidad\estado\historia.json cada 60 s: velas m2 y estelas de las ultimas 3 sesiones
#                                                     para desplazar el grafico (log: %APPDATA%\ATAS\profundidad-historia.log; errores: profundidad\estado\historia.err.log)
# Paso usa Write-Host a proposito: Informar devuelve un bool y si escribiera por Write-Output el bool se mezclaria con las lineas.
# Los tres procesos bajan solos a BELOW_NORMAL (ctypes con restype/argtypes) y aca ademas se les fija PriorityClass = BelowNormal al nacer.
# La PC es un i3 con ATAS abierto y el operador operando: nada de aca toca ATAS, el grafico ni ningun otro proceso.
# "Ya corren" se decide por la linea de comando real (Win32_Process): si hay un python/pythonw con "profundidad" + "motor.py" (o "servir.py", "historia.py"), no se lanza otro.
param(
    [switch]$parar,
    [switch]$estado,
    [int]$cada = 2,
    [int]$puerto = 8765,
    [int]$cadaHistoria = 60,
    [int]$puertoRT = 8766,
    [ValidateSet("motor", "servidor", "historia", "tiempo_real", "guardian")]
    [string]$solo
)
$ErrorActionPreference = "SilentlyContinue"
function Paso($m) { Write-Host ("{0}  {1}" -f (Get-Date -Format "HH:mm:ss"), $m) }
function Toca($nombre) { return (-not $solo) -or ($solo -eq $nombre) }   # sin -solo: todos; con -solo X: solo X

$raiz = Split-Path $PSScriptRoot -Parent                     # ...\PythiaGex
$logDir = Join-Path $env:APPDATA "ATAS"
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Force $logDir | Out-Null }
$pythonw = (Get-Command pythonw -ErrorAction SilentlyContinue).Source
if (-not $pythonw) { $pythonw = (Get-Command python -ErrorAction SilentlyContinue).Source; Paso "aviso: sin pythonw, uso python con la ventana oculta" }
if (-not $pythonw) { Paso "ERROR: no encuentro python ni pythonw en el PATH"; exit 1 }
$env:PYTHONUTF8 = "1"                                        # los logs con enie y acentos no rompen el print

function Buscar($archivo) {
    # procesos python/pythonw cuya linea de comando tenga 'profundidad' y el archivo (motor.py / servir.py)
    Get-CimInstance Win32_Process -Filter "Name like 'python%'" | Where-Object { $_.CommandLine -and $_.CommandLine -like "*profundidad*$archivo*" }
}
function NombrePrioridad($p) {
    switch ($p) { 4 { "IDLE" } 6 { "BELOW_NORMAL" } 8 { "NORMAL" } 10 { "ABOVE_NORMAL" } 13 { "HIGH" } 24 { "REALTIME" } default { "$p" } }
}
function Informar($titulo, $archivo) {
    $ps = @(Buscar $archivo)
    if ($ps.Count -eq 0) { Paso ("{0}: NO corre" -f $titulo); return $false }
    foreach ($p in $ps) { Paso ("{0}: pid {1}, prioridad {2} ({3}), desde {4}" -f $titulo, $p.ProcessId, $p.Priority, (NombrePrioridad $p.Priority), $p.CreationDate.ToString("HH:mm:ss")) }
    return $true
}
function Lanzar($titulo, $argumentos, $log) {
    $out = Join-Path $logDir ($log + ".log")
    $err = Join-Path $logDir ($log + ".err.log")
    # 08-10: se lanza por WMI (Win32_Process.Create) para que el proceso NO quede atado a la consola/sesion que corrio este script: lanzado
    # con Start-Process, al cerrarse la sesion de Claude se cerraban motor, servidor, historia y tiempo real (07-10 17:53, la pagina quedo
    # sin datos 6 h). La salida va por cmd a los mismos logs.
    $cmd = "cmd.exe /c `"`"$pythonw`" " + (($argumentos | ForEach-Object { if ($_ -match '\s') { "`"$_`"" } else { $_ } }) -join " ") + " >> `"$out`" 2>> `"$err`"`""
    $r = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = $cmd; CurrentDirectory = $raiz }
    if (-not $r -or $r.ReturnValue -ne 0) { Paso ("{0}: NO pude lanzar ({1})" -f $titulo, ($argumentos -join " ")); return }
    Start-Sleep -Milliseconds 1500
    $p = $null; foreach ($q in @(Buscar $argumentos[0].Split("\")[-1])) { $p = Get-Process -Id $q.ProcessId -ErrorAction SilentlyContinue; if ($p) { break } }
    if (-not $p) { Paso ("{0}: murio al arrancar; ver {1}" -f $titulo, $err); return }
    try { $p.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::BelowNormal } catch {}
    $w = Get-CimInstance Win32_Process -Filter "ProcessId = $($p.Id)"
    Paso ("{0}: lanzado pid {1}, prioridad {2} ({3}); log {4}" -f $titulo, $p.Id, $w.Priority, (NombrePrioridad $w.Priority), $out)
}

# --- el guardian de ATAS (07-10-2026): herramientas\guardian_atas.ps1 en bucle (cada 60 s), oculto y BELOW_NORMAL, sin duplicar.
# Mantiene ATAS encendido y conectado a Rithmic para que la pagina sea independiente de que el operador tenga ATAS a la vista
# (la unica puerta licenciada al libro de opciones es ATAS). Escribe profundidad\estado\guardian.json; log %APPDATA%\ATAS\pythiagex-guardian.log.
$guardian = Join-Path $PSScriptRoot "guardian_atas.ps1"
$ps51 = Join-Path $env:WINDIR "System32\WindowsPowerShell\v1.0\powershell.exe"
function BuscarGuardian {
    # bucles del guardian por la linea de comando real (sin contar -una / -estado / -parar)
    Get-CimInstance Win32_Process -Filter "Name like 'powershell%'" | Where-Object { $_.CommandLine -and $_.CommandLine -like "*guardian_atas.ps1*" -and $_.CommandLine -notmatch "(?i)-una\b|-estado\b|-parar\b" }
}
function InformarGuardian {
    $ps = @(BuscarGuardian)
    if ($ps.Count -eq 0) { Paso "guardian: NO corre"; return $false }
    foreach ($p in $ps) { Paso ("guardian: pid {0}, prioridad {1} ({2}), desde {3}" -f $p.ProcessId, $p.Priority, (NombrePrioridad $p.Priority), $p.CreationDate.ToString("HH:mm:ss")) }
    return $true
}
function LanzarGuardian {
    if (-not (Test-Path $guardian)) { Paso "guardian: no existe guardian_atas.ps1"; return }
    # 08-10: por WMI, desacoplado de la sesion que corre este script (igual que Lanzar)
    $r = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = "`"$ps51`" -WindowStyle Hidden -NoProfile -ExecutionPolicy Bypass -File `"$guardian`""; CurrentDirectory = $raiz }
    if (-not $r -or $r.ReturnValue -ne 0) { Paso "guardian: NO pude lanzar"; return }
    Start-Sleep -Milliseconds 1500
    $p = $null; foreach ($q in @(BuscarGuardian)) { $p = Get-Process -Id $q.ProcessId -ErrorAction SilentlyContinue; if ($p) { break } }
    if (-not $p) { Paso ("guardian: termino al arrancar; ver {0}" -f (Join-Path $logDir "pythiagex-guardian.log")); return }
    try { $p.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::BelowNormal } catch {}
    $w = Get-CimInstance Win32_Process -Filter "ProcessId = $($p.Id)"
    Paso ("guardian: lanzado pid {0}, prioridad {1} ({2}); log {3}" -f $p.Id, $w.Priority, (NombrePrioridad $w.Priority), (Join-Path $logDir "pythiagex-guardian.log"))
}

if ($parar) {
    foreach ($par in @(@("motor", "motor.py"), @("servidor", "servir.py"), @("historia", "historia.py"), @("tiempo_real", "tiempo_real.py"))) {
        if (-not (Toca $par[0])) { continue }
        $ps = @(Buscar $par[1])
        if ($ps.Count -eq 0) { Paso ("{0}: no corria" -f $par[0]); continue }
        foreach ($p in $ps) { Stop-Process -Id $p.ProcessId -Force; Paso ("{0}: parado pid {1}" -f $par[0], $p.ProcessId) }
    }
    if (Toca "guardian") {
        $ps = @(BuscarGuardian)
        if ($ps.Count -eq 0) { Paso "guardian: no corria" }
        foreach ($p in $ps) { Stop-Process -Id $p.ProcessId -Force; Paso ("guardian: parado pid {0}" -f $p.ProcessId) }
    }
    exit 0
}

if ($estado) {
    $m = Informar "motor" "motor.py"
    $s = Informar "servidor" "servir.py"
    $h = Informar "historia" "historia.py"
    $rt = Informar "tiempo_real" "tiempo_real.py"
    if ($rt) { Paso ("tiempo real: http://localhost:{0}/salud (cinta de la 3.0 al ms, /stream, /velas, /footprint, /perfiles)" -f $puertoRT) }
    $g = InformarGuardian
    $e = Join-Path $raiz "profundidad\estado\estado.json"
    if (Test-Path $e) { $f = Get-Item $e; Paso ("estado.json: {0} KB, escrito hace {1:n0} s" -f [int]($f.Length / 1024), ((Get-Date) - $f.LastWriteTime).TotalSeconds) } else { Paso "estado.json: no existe" }
    $hj = Join-Path $raiz "profundidad\estado\historia.json"
    if (Test-Path $hj) {
        $f = Get-Item $hj
        try { $d = Get-Content $hj -Raw -Encoding UTF8 | ConvertFrom-Json; Paso ("historia.json: {0} KB, escrito hace {1:n0} s; {2} velas m2 ({3} a {4} UTC), {5} sesiones, {6} avisos" -f [int]($f.Length / 1024), ((Get-Date) - $f.LastWriteTime).TotalSeconds, @($d.velas_m2).Count, $d.rango.primera, $d.rango.ultima, $d.dias, @($d.avisos).Count) } catch { Paso ("historia.json: {0} KB, escrito hace {1:n0} s (no lo pude leer)" -f [int]($f.Length / 1024), ((Get-Date) - $f.LastWriteTime).TotalSeconds) }
    } else { Paso "historia.json: no existe" }
    $he = Join-Path $raiz "profundidad\estado\historia.err.log"
    if (Test-Path $he) { $f = Get-Item $he; Paso ("historia.err.log: ultimo error hace {0:n0} s ({1})" -f ((Get-Date) - $f.LastWriteTime).TotalSeconds, $he) }
    $gj = Join-Path $raiz "profundidad\estado\guardian.json"
    if (Test-Path $gj) {
        $f = Get-Item $gj
        try { $d = Get-Content $gj -Raw -Encoding UTF8 | ConvertFrom-Json; Paso ("guardian.json: escrito hace {0:n0} s; atas_corre={1} rithmic={2} tres_latido_s={3}" -f ((Get-Date) - $f.LastWriteTime).TotalSeconds, $d.atas_corre, $d.rithmic, $d.tres_latido_s) } catch { Paso "guardian.json: no lo pude leer" }
    } else { Paso "guardian.json: no existe" }
    if ($s) { Paso ("pagina: http://localhost:{0}/" -f $puerto) }
    exit 0
}

# lanzar lo que falte (sin duplicar); con -solo X, solo X
if (Toca "motor") { if (-not (Informar "motor" "motor.py")) { Lanzar "motor" @("profundidad\motor.py", "bucle", "--cada", "$cada") "profundidad-motor" } }
if (Toca "servidor") { if (-not (Informar "servidor" "servir.py")) { Lanzar "servidor" @("profundidad\servir.py", "--puerto", "$puerto") "profundidad-servidor" } }
if (Toca "historia") { if (-not (Informar "historia" "historia.py")) { Lanzar "historia" @("profundidad\historia.py", "bucle", "--cada", "$cadaHistoria") "profundidad-historia" } }
if (Toca "tiempo_real") { if (-not (Informar "tiempo_real" "tiempo_real.py")) { Lanzar "tiempo_real" @("profundidad\tiempo_real.py", "--puerto", "$puertoRT") "profundidad-tiempo-real" } }
if (Toca "guardian") { if (-not (InformarGuardian)) { LanzarGuardian } }
Paso ("pagina: http://localhost:{0}/   (para cortar: herramientas\profundidad.ps1 -parar)" -f $puerto)
