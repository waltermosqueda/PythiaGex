# Cierra una ventana/dialogo de ATAS por su titulo (WindowPattern.Close; si no, el boton Close de adentro).
param([string]$Titulo = "Connections")
$ErrorActionPreference = "SilentlyContinue"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Titulo)
foreach ($w in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)) {
    $ct = $w.Current.ControlType.ProgrammaticName
    $r = $w.Current.BoundingRectangle
    if ($r.Width -le 0) { continue }
    Write-Output ("candidato '{0}' {1} en {2},{3} {4}x{5}" -f $w.Current.Name, $ct, [int]$r.X, [int]$r.Y, [int]$r.Width, [int]$r.Height)
    if ($ct -ne "ControlType.Window") { continue }
    try { $wp = $w.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern); $wp.Close(); Write-Output "CERRADA por WindowPattern"; exit 0 } catch { Write-Output "sin WindowPattern: $_" }
    $cb = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "Close")
    foreach ($b in $w.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cb)) {
        if ($b.Current.ControlType.ProgrammaticName -ne "ControlType.Button") { continue }
        try { $ip = $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern); $ip.Invoke(); Write-Output "CERRADA por boton Close"; exit 0 } catch {}
    }
}
Write-Output "no cerre nada"
