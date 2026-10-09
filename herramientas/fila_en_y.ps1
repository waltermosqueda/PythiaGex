# Aprieta el hipervinculo $Nombre (Connect / Disconnect) del dialogo Connections que esta en la fila de altura $Y (+-6). Lista todos.
param([string]$Nombre = "Connect", [int]$Y = -1, [switch]$SoloListar)
$ErrorActionPreference = "SilentlyContinue"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::RootElement
$condT = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Hyperlink)
$els = @()
foreach ($e in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condT)) {
    $r = $e.Current.BoundingRectangle
    if ($r.Width -le 0) { continue }
    $n = $e.Current.Name
    if ($n -ne "Connect" -and $n -ne "Disconnect") { continue }
    $els += [pscustomobject]@{ Y = [int]$r.Y; Nombre = $n; El = $e }
}
foreach ($x in ($els | Sort-Object Y)) { Write-Output ("  y={0} {1}" -f $x.Y, $x.Nombre) }
if ($SoloListar -or $Y -lt 0) { exit 0 }
$obj = $els | Where-Object { $_.Nombre -eq $Nombre -and [math]::Abs($_.Y - $Y) -le 6 } | Select-Object -First 1
if (-not $obj) { Write-Output "NO HAY '$Nombre' en y=$Y"; exit 0 }
foreach ($t in @($obj.El, [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($obj.El))) {
    try { $p = $t.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern); $p.Invoke(); Write-Output "INVOCADO $Nombre en y=$Y"; exit 0 } catch {}
}
Write-Output "SIN InvokePattern para $Nombre en y=$Y"
