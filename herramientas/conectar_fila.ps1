# Lista los "Connect" del dialogo Connections de ATAS (con su Y en pantalla) y aprieta el de la fila N (1 = la de arriba).
param([int]$Fila = 5, [switch]$SoloListar)
$ErrorActionPreference = "SilentlyContinue"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "Connect")
$els = @()
foreach ($e in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)) {
    $r = $e.Current.BoundingRectangle
    if ($r.Width -le 0) { continue }
    if ($e.Current.ControlType.ProgrammaticName -ne "ControlType.Hyperlink") { continue }
    $els += [pscustomobject]@{ Y = [int]$r.Y; X = [int]$r.X; Tipo = $e.Current.ControlType.ProgrammaticName; El = $e }
}
$els = $els | Sort-Object Y
$i = 0
foreach ($x in $els) { $i++; Write-Output ("{0}: Connect en x={1} y={2} {3}" -f $i, $x.X, $x.Y, $x.Tipo) }
if ($SoloListar -or $els.Count -lt $Fila) { Write-Output "sin invocar (hay $($els.Count))"; exit 0 }
$obj = $els[$Fila - 1].El
$ok = $false
foreach ($t in @($obj, [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($obj))) {
    try { $p = $t.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern); $p.Invoke(); $ok = $true; Write-Output "INVOCADO Connect de la fila $Fila"; break } catch {}
}
if (-not $ok) {
    # sin InvokePattern: clic fisico en el centro del elemento
    Add-Type @"
using System; using System.Runtime.InteropServices;
public static class M4 { [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y); [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e); }
"@
    $r = $obj.Current.BoundingRectangle
    [M4]::SetCursorPos([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2)); Start-Sleep -Milliseconds 200
    [M4]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); [M4]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Write-Output "CLIC fisico en la fila $Fila ($([int]($r.X + $r.Width / 2)),$([int]($r.Y + $r.Height / 2)))"
}
