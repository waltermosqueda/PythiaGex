# CONECTAR ATAS SIN REINICIAR (06-10-2026): cuando ATAS quedo abierto pero sin proveedor de cotizaciones ("connect a quote provider").
# Abre Connections por UIA, aprieta Connect (o el boton por conexion) y lista lo que ve. Sin credenciales: usa la clave recordada.
$ErrorActionPreference = "SilentlyContinue"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
function Paso($m) { Write-Output ("{0}  {1}" -f (Get-Date -Format "HH:mm:ss"), $m) }
function Buscar($nombre, $tipos) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $nombre)
    $els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    $out = @()
    foreach ($e in $els) {
        $r = $e.Current.BoundingRectangle
        if ($r.Width -le 0) { continue }
        $ct = $e.Current.ControlType.ProgrammaticName
        if ($tipos -and ($tipos -notcontains $ct)) { continue }
        $out += $e
    }
    return $out
}
function Invocar($nombre, $tipos) {
    foreach ($e in (Buscar $nombre $tipos)) {
        $ct = $e.Current.ControlType.ProgrammaticName
        foreach ($t in @($e, [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($e))) {
            try { $p = $t.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern); $p.Invoke(); return "INVOCADO $nombre ($ct)" } catch {}
        }
    }
    return "NO ENCONTRADO $nombre"
}
function Botones($filtro) {
    # todos los botones visibles cuyo nombre contenga el filtro (para saber como se llama lo que hay que apretar)
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
    $els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    $vistos = @{}
    foreach ($e in $els) {
        $r = $e.Current.BoundingRectangle; if ($r.Width -le 0) { continue }
        $n = $e.Current.Name; if (-not $n) { continue }
        if ($filtro -and ($n -notlike "*$filtro*")) { continue }
        if (-not $vistos.ContainsKey($n)) { $vistos[$n] = 1; Paso ("  boton: '" + $n + "' en " + [int]$r.X + "," + [int]$r.Y) }
    }
}
$p = Get-Process OFT.Platform | Select-Object -First 1
if (-not $p) { Paso "ATAS no corre"; exit 1 }
Paso "ATAS pid $($p.Id) ventana '$($p.MainWindowTitle)'"
if ($p.MainWindowTitle -eq "Authorization") {
    $res = Invocar "Connect" $null; Paso "login: $res"
    if ($res -notlike "INVOCADO*") { $ws = New-Object -ComObject WScript.Shell; $null = $ws.AppActivate($p.Id); Start-Sleep -Seconds 1; $ws.SendKeys("{ENTER}"); Paso "login: Enter enviado" }
    exit 0
}
# ya en la plataforma: Connections -> Connect
Paso "botones con 'onnect' ANTES:"; Botones "onnect"
$res = Invocar "Connections" @("ControlType.Button"); Paso "Connections: $res"
Start-Sleep -Seconds 3
Paso "botones con 'onnect' DESPUES:"; Botones "onnect"
foreach ($n in @("Connect all", "Connect All", "Connect")) {
    $r2 = Invocar $n @("ControlType.Button")
    Paso "$n -> $r2"
    if ($r2 -like "INVOCADO*") { break }
}
Start-Sleep -Seconds 2
Paso "ventanas del proceso:"; Get-Process OFT.Platform | ForEach-Object { Paso ("  '" + $_.MainWindowTitle + "'") }
