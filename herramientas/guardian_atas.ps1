# GUARDIAN DE ATAS (07-10-2026): ATAS como servidor siempre encendido para "Profundidad 3.0". PowerShell 5.1, sin ventana, BELOW_NORMAL.
#
#   powershell -WindowStyle Hidden -ExecutionPolicy Bypass -File herramientas\guardian_atas.ps1            bucle: un chequeo cada 60 s (lo lanza profundidad.ps1)
#   powershell -ExecutionPolicy Bypass -File herramientas\guardian_atas.ps1 -una                           un chequeo y salir (imprime lo que ve)
#   powershell -ExecutionPolicy Bypass -File herramientas\guardian_atas.ps1 -estado                        ultimo chequeo (guardian.json) y si el bucle corre
#   powershell -ExecutionPolicy Bypass -File herramientas\guardian_atas.ps1 -parar                         mata al bucle que corre
#   opciones: -cada 60 (segundos entre chequeos), -vueltas N (N chequeos y salir; 0 = sin fin), -sinRelanzar (nunca lanza OFT.Platform.exe: solo anota)
#
# Cada chequeo:
#   (1) ATAS corre? (Get-Process OFT.Platform). Si NO corre en 2 chequeos seguidos: lanza OFT.Platform.exe y hace el login como instalar_3_0.ps1
#       (ventana "Authorization": Connect por UIA; si no, Enter con WScript.Shell; espera la principal "ATAS - [...]"). NO copia ningun DLL.
#       Resguardos: no relanza si existe profundidad\estado\guardian.pausa, si corre instalar_3_0.ps1 / reiniciar_e_instalar.ps1 (estan
#       reiniciando ATAS a proposito), si se paso -sinRelanzar, ni mas de una vez cada 10 min.
#   (2) Conectado a Rithmic? Ultima linea "Rithmic[...] connected/disconnected" de %APPDATA%\ATAS\Logs\app_<yyyyMMdd>.log. Si dice
#       disconnected hace > 5 min y ATAS corre con la ventana principal "ATAS*": abre Connections (boton por UIA), aprieta fila_en_y.ps1
#       -Nombre Connect -Y 355 (la fila Rithmic "lucid"; lucid2 es duplicado), espera 10 s y cierra con cerrar_ventana.ps1 -Titulo Connections.
#       Como maximo un intento cada 10 min. Si la ventana principal es "Authorization": login (mismo tope).
#   (3) La 3.0 escribe? Edad de la ultima linea "minuto:" de %APPDATA%\ATAS\pythiagex3-gammahoy.log. Si > 5 min con ATAS corriendo y
#       conectado: se anota (NO se reinicia ATAS por eso: puede ser que el operador cerro ese grafico).
#   (4) Escribe SIEMPRE profundidad\estado\guardian.json y una linea en %APPDATA%\ATAS\pythiagex-guardian.log.
# NUNCA cierra ATAS, NUNCA toca un grafico. Si hay una ventana del proceso distinta de la principal / Authorization / Connections, no la toca:
# la anota en "ventanas_otras" y, si estaba por relanzar o reconectar, no lo hace mientras este esa ventana.
# fila_en_y.ps1 y cerrar_ventana.ps1 terminan con `exit`: se corren como procesos hijos, nunca con punto ni con &, o matarian al guardian.
param(
    [switch]$una,
    [switch]$parar,
    [switch]$estado,
    [switch]$sinRelanzar,
    [int]$cada = 60,
    [int]$vueltas = 0
)
$ErrorActionPreference = "SilentlyContinue"
try { (Get-Process -Id $PID).PriorityClass = [System.Diagnostics.ProcessPriorityClass]::BelowNormal } catch {}

$raiz = Split-Path $PSScriptRoot -Parent                                   # ...\PythiaGex
$estadoDir = Join-Path $raiz "profundidad\estado"
$jsonRuta = Join-Path $estadoDir "guardian.json"
$pausaRuta = Join-Path $estadoDir "guardian.pausa"
$logRuta = Join-Path $env:APPDATA "ATAS\pythiagex-guardian.log"
$logsAtas = Join-Path $env:APPDATA "ATAS\Logs"
$tresLog = Join-Path $env:APPDATA "ATAS\pythiagex3-gammahoy.log"
$atasExe = "C:\Program Files (x86)\ATAS Platform\OFT.Platform.exe"
$utf8 = New-Object System.Text.UTF8Encoding $false
$ps51 = Join-Path $env:WINDIR "System32\WindowsPowerShell\v1.0\powershell.exe"
if (-not (Test-Path $estadoDir)) { New-Item -ItemType Directory -Force $estadoDir | Out-Null }

function Iso($d) { if ($d) { $d.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ") } else { $null } }
function Log($m) {
    $linea = "{0}  {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $m
    try { [System.IO.File]::AppendAllText($logRuta, $linea + "`r`n", $utf8) } catch {}
    if ($una -or $estado -or $parar -or $vueltas -gt 0) { Write-Host $linea }
}
function OtrosGuardianes {
    # bucles del guardian que ya corren (por la linea de comando real), sin contar -una / -estado / -parar ni este proceso
    Get-CimInstance Win32_Process -Filter "Name like 'powershell%'" | Where-Object {
        $_.ProcessId -ne $PID -and $_.CommandLine -and $_.CommandLine -like "*guardian_atas.ps1*" -and
        $_.CommandLine -notmatch "(?i)-una\b|-estado\b|-parar\b"
    }
}

# ---------------------------------------------------------------- -parar / -estado
if ($parar) {
    $ps = @(OtrosGuardianes)
    if ($ps.Count -eq 0) { Log "parar: el guardian no corria" }
    foreach ($p in $ps) { Stop-Process -Id $p.ProcessId -Force; Log ("parar: guardian parado pid {0}" -f $p.ProcessId) }
    exit 0
}
if ($estado) {
    $ps = @(OtrosGuardianes)
    if ($ps.Count -eq 0) { Write-Host "guardian: NO corre (bucle)" }
    foreach ($p in $ps) { Write-Host ("guardian: pid {0}, prioridad {1}, desde {2}" -f $p.ProcessId, $p.Priority, $p.CreationDate.ToString("HH:mm:ss")) }
    if (Test-Path $jsonRuta) {
        $f = Get-Item $jsonRuta
        Write-Host ("guardian.json: escrito hace {0:n0} s" -f ((Get-Date) - $f.LastWriteTime).TotalSeconds)
        Get-Content $jsonRuta -Raw -Encoding UTF8
    } else { Write-Host "guardian.json: no existe (todavia no hubo ningun chequeo)" }
    if (Test-Path $pausaRuta) { Write-Host "PAUSA: existe guardian.pausa, el guardian no relanza ATAS" }
    exit 0
}

# ---------------------------------------------------------------- UIA (solo elementos del proceso de ATAS)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
function Elementos($nombre, $tipos, $pidAtas) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $nombre)
    $out = @()
    foreach ($e in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)) {
        $r = $e.Current.BoundingRectangle
        if ($r.Width -le 0) { continue }
        if ($pidAtas -and $e.Current.ProcessId -ne $pidAtas) { continue }
        $ct = $e.Current.ControlType.ProgrammaticName
        if ($tipos -and ($tipos -notcontains $ct)) { continue }
        $out += $e
    }
    return $out
}
function Invocar($nombre, $tipos, $pidAtas) {
    $els = @(Elementos $nombre $tipos $pidAtas)
    foreach ($e in $els) {
        $ct = $e.Current.ControlType.ProgrammaticName
        foreach ($t in @($e, [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($e))) {
            try { $p = $t.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern); $p.Invoke(); return "INVOCADO $nombre ($ct)" } catch {}
        }
    }
    return "NO ENCONTRADO $nombre (candidatos $($els.Count))"
}
function VentanasDe($pidAtas) {
    # titulos de las ventanas de primer nivel del proceso (la principal, Authorization, Connections, paneles flotantes, modales)
    $out = @()
    try {
        $root = [System.Windows.Automation.AutomationElement]::RootElement
        $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, [int]$pidAtas)
        foreach ($w in $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)) {
            $r = $w.Current.BoundingRectangle
            if ($r.Width -le 0) { continue }
            $n = $w.Current.Name
            if ($n) { $out += $n }
        }
    } catch {}
    return $out
}
function VentanasOtras($ventanas) {
    # lo que no es la principal ni Authorization ni Connections: no se toca, se anota
    @($ventanas | Where-Object { $_ -notlike "ATAS*" -and $_ -ne "Authorization" -and $_ -ne "Connections" } | Sort-Object -Unique)
}
function Hijo($script, $argumentos) {
    # corre fila_en_y.ps1 / cerrar_ventana.ps1 en un powershell hijo (tienen `exit` adentro) y devuelve su salida en una linea
    $salida = & $ps51 -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $script) @argumentos 2>&1
    return (($salida | ForEach-Object { "$_".Trim() } | Where-Object { $_ }) -join " | ")
}
function Login($p) {
    $res = Invocar "Connect" $null $p.Id
    if ($res -notlike "INVOCADO*") {
        # 8.0.15: la ventana de login nueva no expone el boton Connect por UIA. Activar la ventana y Enter (instalar_3_0.ps1).
        try { $ws = New-Object -ComObject WScript.Shell; $null = $ws.AppActivate($p.Id); Start-Sleep -Seconds 1; $ws.SendKeys("{ENTER}"); $res = "$res; Enter enviado" } catch { $res = "$res; no pude mandar Enter: $_" }
    }
    return $res
}
function EsperarPrincipal($segundos) {
    for ($i = 0; $i -lt [int]($segundos / 2); $i++) {
        Start-Sleep -Seconds 2
        $p = Get-Process OFT.Platform | Select-Object -First 1
        if ($p -and $p.MainWindowTitle -like "ATAS*") { return $p }
    }
    return (Get-Process OFT.Platform | Select-Object -First 1)
}

# ---------------------------------------------------------------- lecturas
function LeerRithmic($desdeProceso) {
    # ultima linea "Rithmic[...] connected." / "disconnected." del log de hoy (si no hay, del de ayer). Hora local -> UTC.
    $re = 'Rithmic\[[^\]]*\]\s+(connected|disconnected)\.'
    foreach ($dia in @(0, -1)) {
        $f = Join-Path $logsAtas ("app_{0}.log" -f (Get-Date).AddDays($dia).ToString("yyyyMMdd"))
        if (-not (Test-Path $f)) { continue }
        $hit = $null
        foreach ($l in (Get-Content $f -Tail 6000 -Encoding UTF8)) { if ($l -match $re) { $hit = $l } }
        if (-not $hit) {   # no estaba en la cola: recorrer el archivo entero sin cargarlo en memoria
            try { foreach ($l in [System.IO.File]::ReadLines($f)) { if ($l -match $re) { $hit = $l } } } catch {}
        }
        if ($hit -and $hit -match '^(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d),(\d{3})') {
            $t = [datetime]::ParseExact($matches[1], "yyyy-MM-dd HH:mm:ss", $null)
            $estadoR = if ($hit -match 'disconnected\.') { "disconnected" } else { "connected" }
            return @{ estado = $estadoR; t = $t; archivo = (Split-Path $f -Leaf) }
        }
    }
    return @{ estado = "?"; t = $null; archivo = $null }
}
function LeerLatidoTres {
    # ultima linea "minuto:" del log de la 3.0 (hora local). Devuelve segundos de edad o $null.
    if (-not (Test-Path $tresLog)) { return @{ s = $null; t = $null } }
    $hit = $null
    foreach ($l in (Get-Content $tresLog -Tail 400 -Encoding UTF8)) { if ($l -match '^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\.\d+\s+minuto:') { $hit = $l } }
    if (-not $hit -or $hit -notmatch '^(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)') { return @{ s = $null; t = $null } }
    $t = [datetime]::ParseExact($matches[1], "yyyy-MM-dd HH:mm:ss", $null)
    return @{ s = [int]((Get-Date) - $t).TotalSeconds; t = $t }
}
function InstaladorCorriendo {
    @(Get-CimInstance Win32_Process -Filter "Name like 'powershell%'" | Where-Object { $_.CommandLine -and $_.CommandLine -match "(?i)instalar_3_0\.ps1|reiniciar_e_instalar\.ps1|clonar_2_0" }).Count -gt 0
}

# ---------------------------------------------------------------- estado persistente (acciones y contadores sobreviven al reinicio del guardian)
$script:acciones = @()
$script:chequeos = 0
if (Test-Path $jsonRuta) {
    try {
        $prev = Get-Content $jsonRuta -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($prev.acciones) { $script:acciones = @($prev.acciones | ForEach-Object { @{ t = $_.t; que = $_.que; resultado = $_.resultado } }) }
    } catch {}
}
$script:sinAtasSeguidos = 0
$script:ultimoRelanzo = [datetime]::MinValue
$script:ultimoReconecto = [datetime]::MinValue
$script:ultimoAvisoTres = [datetime]::MinValue
function Accion($que, $resultado) {
    $script:acciones += @{ t = (Iso (Get-Date)); que = $que; resultado = $resultado }
    if ($script:acciones.Count -gt 20) { $script:acciones = @($script:acciones | Select-Object -Last 20) }
    Log ("ACCION {0}: {1}" -f $que, $resultado)
}

# ---------------------------------------------------------------- acciones
function Relanzar {
    Accion "relanzar" "ATAS no corre hace 2 chequeos: lanzo $atasExe"
    Start-Process $atasExe | Out-Null
    $p = $null
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Seconds 2
        $p = Get-Process OFT.Platform | Select-Object -First 1
        if ($p -and ($p.MainWindowTitle -eq "Authorization" -or $p.MainWindowTitle -like "ATAS*")) { break }
    }
    if (-not $p) { Accion "relanzar" "OFT.Platform no aparecio en 120 s"; return }
    Accion "relanzar" ("ventana: '{0}' (pid {1})" -f $p.MainWindowTitle, $p.Id)
    for ($k = 0; $k -lt 4 -and $p -and $p.MainWindowTitle -eq "Authorization"; $k++) {
        Start-Sleep -Seconds 3
        $res = Login $p
        Accion "login" ("intento {0}: {1}" -f ($k + 1), $res)
        $p = EsperarPrincipal 60
    }
    if ($p) { Accion "relanzar" ("principal: '{0}'" -f $p.MainWindowTitle) } else { Accion "relanzar" "ATAS murio durante el login" }
}
function Reconectar($p) {
    $res = Invocar "Connections" @("ControlType.Button") $p.Id
    Accion "reconectar" "abrir Connections: $res"
    Start-Sleep -Seconds 3
    $hay = @(Elementos "Connections" @("ControlType.Window") $p.Id).Count -gt 0
    if (-not $hay) { Accion "reconectar" "no veo la ventana Connections: no toco nada mas"; return }
    $r2 = Hijo "fila_en_y.ps1" @("-Nombre", "Connect", "-Y", "355")
    Accion "reconectar" "fila_en_y Connect y=355 (lucid): $r2"
    Start-Sleep -Seconds 10
    $r3 = Hijo "cerrar_ventana.ps1" @("-Titulo", "Connections")
    Accion "reconectar" "cerrar Connections: $r3"
}

# ---------------------------------------------------------------- un chequeo
function Chequeo {
    $script:chequeos++
    $ahora = Get-Date
    $p = Get-Process OFT.Platform | Select-Object -First 1
    $corre = [bool]$p
    $pausa = Test-Path $pausaRuta
    $instalando = InstaladorCorriendo
    $ventanas = @(); $otras = @(); $titulo = $null; $desde = $null
    if ($corre) {
        $script:sinAtasSeguidos = 0
        $titulo = $p.MainWindowTitle
        try { $desde = $p.StartTime } catch {}
        $ventanas = @(VentanasDe $p.Id)
        $otras = @(VentanasOtras $ventanas)
    } else { $script:sinAtasSeguidos++ }

    # (2) Rithmic
    $r = LeerRithmic
    $rithmic = $r.estado; $rithmicDesde = $r.t
    if (-not $corre) { $rithmic = "disconnected" }                                  # sin ATAS no hay Rithmic, diga lo que diga el log
    elseif ($r.t -and $desde -and $r.t -lt $desde) { $rithmic = "?"; $rithmicDesde = $null }   # la linea es de un ATAS anterior
    $rithmicHaceS = if ($rithmicDesde) { [int]($ahora - $rithmicDesde).TotalSeconds } else { $null }

    # (3) latido de la 3.0
    $lat = LeerLatidoTres
    $tresAviso = $null
    if ($corre -and $rithmic -eq "connected" -and $lat.s -ne $null -and $lat.s -gt 300) {
        $tresAviso = "la 3.0 no escribe 'minuto:' hace $($lat.s) s con ATAS conectado (quiza el operador cerro ese grafico): NO se reinicia"
        if (($ahora - $script:ultimoAvisoTres).TotalMinutes -ge 10) { $script:ultimoAvisoTres = $ahora; Accion "aviso" $tresAviso }
    }

    # decisiones
    $nota = @()
    if ($otras.Count -gt 0) { $nota += ("ventana(s) del proceso que no toco: " + ($otras -join " / ")) }
    if (-not $corre) {
        if ($script:sinAtasSeguidos -ge 2) {
            if ($sinRelanzar) { $nota += "ATAS no corre hace $($script:sinAtasSeguidos) chequeos: lo relanzaria, pero -sinRelanzar" }
            elseif ($pausa) { $nota += "ATAS no corre hace $($script:sinAtasSeguidos) chequeos: no relanzo, existe guardian.pausa" }
            elseif ($instalando) { $nota += "ATAS no corre: no relanzo, hay un instalador/reiniciador de ATAS corriendo" }
            elseif (($ahora - $script:ultimoRelanzo).TotalMinutes -lt 10) { $nota += "ATAS no corre: ya lo relance hace < 10 min, espero" }
            else { $script:ultimoRelanzo = $ahora; Relanzar; $p = Get-Process OFT.Platform | Select-Object -First 1; $corre = [bool]$p; if ($p) { $titulo = $p.MainWindowTitle; try { $desde = $p.StartTime } catch {} } }
        } else { $nota += "ATAS no corre (chequeo $($script:sinAtasSeguidos) de 2 antes de relanzar)" }
    } elseif ($titulo -eq "Authorization") {
        if ($otras.Count -gt 0) { $nota += "Authorization abierta pero hay otra ventana: no toco" }
        elseif (($ahora - $script:ultimoReconecto).TotalMinutes -ge 10) {
            $script:ultimoReconecto = $ahora
            $res = Login $p; Accion "login" "ventana Authorization sin relanzar: $res"
            $p = EsperarPrincipal 60; if ($p) { $titulo = $p.MainWindowTitle }
        } else { $nota += "Authorization abierta: ya intente el login hace < 10 min" }
    } elseif ($rithmic -eq "disconnected" -and $rithmicHaceS -ne $null -and $rithmicHaceS -gt 300) {
        if ($titulo -notlike "ATAS*") { $nota += "Rithmic disconnected hace $rithmicHaceS s pero la ventana principal es '$titulo': no toco" }
        elseif ($otras.Count -gt 0) { $nota += "Rithmic disconnected hace $rithmicHaceS s pero hay otra ventana abierta: no toco" }
        elseif ($ventanas -contains "Connections") { $nota += "Rithmic disconnected y Connections ya esta abierta (la abrio alguien): no toco" }
        elseif (($ahora - $script:ultimoReconecto).TotalMinutes -ge 10) { $script:ultimoReconecto = $ahora; Reconectar $p }
        else { $nota += "Rithmic disconnected hace $rithmicHaceS s: ya intente reconectar hace < 10 min" }
    }

    # (4) guardian.json + log
    $doc = [ordered]@{
        t = (Iso $ahora)
        atas_corre = $corre
        pid = $(if ($p) { $p.Id } else { $null })
        desde = (Iso $desde)
        ventana = $titulo
        ventanas_otras = $otras
        rithmic = $rithmic
        rithmic_desde = (Iso $rithmicDesde)
        rithmic_hace_s = $rithmicHaceS
        rithmic_log = $r.archivo
        tres_latido_s = $lat.s
        tres_ultimo = (Iso $lat.t)
        tres_aviso = $tresAviso
        sin_atas_seguidos = $script:sinAtasSeguidos
        relanzar = (-not $sinRelanzar -and -not $pausa)
        pausa = $pausa
        instalador_corriendo = $instalando
        notas = $nota
        acciones = @($script:acciones)
        chequeos = $script:chequeos
        guardian_pid = $PID
        modo = $(if ($una) { "una" } else { "bucle" })
    }
    try {
        $tmp = "$jsonRuta.$PID.tmp"
        [System.IO.File]::WriteAllText($tmp, ($doc | ConvertTo-Json -Depth 6), $utf8)
        Move-Item -Force $tmp $jsonRuta
    } catch { Log "no pude escribir guardian.json: $_" }
    $resumen = "chequeo {0}: atas={1}{2} rithmic={3}{4} tres_latido={5}{6}" -f $script:chequeos,
        $(if ($corre) { "corre pid $($p.Id) '$titulo'" } else { "NO corre" }),
        $(if ($desde) { " desde $($desde.ToString('HH:mm:ss'))" } else { "" }),
        $rithmic, $(if ($rithmicHaceS -ne $null) { " hace $rithmicHaceS s" } else { "" }),
        $(if ($lat.s -ne $null) { "$($lat.s) s" } else { "?" }),
        $(if ($nota.Count -gt 0) { " | " + ($nota -join "; ") } else { " | todo bien" })
    Log $resumen
}

# ---------------------------------------------------------------- arranque
if ($una) { Chequeo; exit 0 }
$otros = @(OtrosGuardianes)
if ($otros.Count -gt 0) { $ids = (@($otros | ForEach-Object { $_.ProcessId }) -join ","); Log ("ya corre otro guardian (pid {0}): no lanzo un segundo" -f $ids); exit 0 }
Log ("guardian arranca pid {0}: cada {1} s{2}{3}" -f $PID, $cada, $(if ($vueltas -gt 0) { ", $vueltas vueltas" } else { "" }), $(if ($sinRelanzar) { ", -sinRelanzar" } else { "" }))
$n = 0
while ($true) {
    try { Chequeo } catch { Log "error en el chequeo: $_" }
    $n++
    if ($vueltas -gt 0 -and $n -ge $vueltas) { Log "guardian termina: $n vueltas"; break }
    Start-Sleep -Seconds $cada
}
