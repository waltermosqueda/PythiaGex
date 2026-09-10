# Reinicia ATAS sin tocar credenciales: cierra guardando el workspace, relanza,
# aprieta Connect (la clave recordada) y restaura la ventana. Imprime cada paso.
$ErrorActionPreference = "SilentlyContinue"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W3 {
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, UIntPtr e);
}
"@
function Paso($m) { Write-Output ("{0}  {1}" -f (Get-Date -Format "HH:mm:ss"), $m) }
function Invocar($nombre, $tipos) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $nombre)
    $els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    foreach ($e in $els) {
        $r = $e.Current.BoundingRectangle
        if ($r.Width -le 0) { continue }
        $ct = $e.Current.ControlType.ProgrammaticName
        if ($tipos -and ($tipos -notcontains $ct)) { continue }
        foreach ($t in @($e, [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($e))) {
            try { $p = $t.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern); $p.Invoke(); return "INVOCADO $nombre ($ct)" } catch {}
        }
    }
    return "NO ENCONTRADO $nombre (candidatos $($els.Count))"
}

$p = Get-Process OFT.Platform | Select-Object -First 1
if ($p) {
    Paso "cerrando ATAS (pid $($p.Id), '$($p.MainWindowTitle)')"
    $p.CloseMainWindow() | Out-Null
    $res = "NO ENCONTRADO"
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Seconds 1
        $res = Invocar "Save and close" @("ControlType.Button")
        if ($res -like "INVOCADO*") { break }
    }
    Paso "dialogo de guardar: $res"
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Seconds 1
        if (-not (Get-Process OFT.Platform)) { break }
    }
    if (Get-Process OFT.Platform) { Paso "ATAS no murio en 60 s; lo mato"; Stop-Process -Name OFT.Platform -Force; Start-Sleep -Seconds 5 }
    Paso "ATAS cerrado"
} else { Paso "ATAS no corria" }

Start-Process "C:\Program Files (x86)\ATAS Platform\OFT.Platform.exe"
Paso "lanzado"
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 2
    $p = Get-Process OFT.Platform | Select-Object -First 1
    if ($p -and ($p.MainWindowTitle -eq "Authorization" -or $p.MainWindowTitle -like "ATAS*")) { break }
}
Paso "ventana: '$($p.MainWindowTitle)'"
if ($p.MainWindowTitle -eq "Authorization") {
    Start-Sleep -Seconds 2
    $res = Invocar "Connect" $null
    Paso "login: $res"
}
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 2
    $p = Get-Process OFT.Platform | Select-Object -First 1
    if ($p -and $p.MainWindowTitle -like "ATAS*") { break }
}
$p = Get-Process OFT.Platform | Select-Object -First 1
# si sigue en Authorization, Connect otra vez (hasta 3), sin esperar minutos
for ($k = 0; $k -lt 3 -and $p -and $p.MainWindowTitle -eq "Authorization"; $k++) {
    Start-Sleep -Seconds 3
    $res = Invocar "Connect" $null
    Paso "login reintento $($k+1): $res"
    for ($i = 0; $i -lt 30; $i++) { Start-Sleep -Seconds 2; $p = Get-Process OFT.Platform | Select-Object -First 1; if ($p -and $p.MainWindowTitle -like "ATAS*") { break } }
}
Paso "principal: '$($p.MainWindowTitle)'"
if ($p -and $p.MainWindowHandle -ne 0) {
    [W3]::ShowWindow($p.MainWindowHandle, 3) | Out-Null
    [W3]::keybd_event(0x12,0,0,[UIntPtr]::Zero); [W3]::SetForegroundWindow($p.MainWindowHandle) | Out-Null; [W3]::keybd_event(0x12,0,2,[UIntPtr]::Zero)
    Paso "ventana restaurada y al frente"
}
