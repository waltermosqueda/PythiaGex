# ATAS SIEMPRE ARRIBA, PARA QUE LA CADENA VIVA DE RITHMIC SE GRABE SOLA.
#
# La cadena de opciones de ES por Rithmic solo existe mientras ATAS esta
# abierto y conectado con la cuenta del operador; la nube no puede grabarla.
# Este script se corre al iniciar sesion en Windows y cada 15 minutos (tarea
# programada "PythiaGex - ATAS autoarranque"): si ATAS no esta corriendo lo
# lanza; si esta en la ventana "Authorization" con la clave recordada, aprieta
# Connect (no escribe ninguna credencial: usa la que ATAS ya tiene guardada);
# y restaura la ventana si quedo minimizada. Si ATAS ya esta abierto y
# conectado, no hace nada.
#
# Para desactivarlo: schtasks /Delete /TN "PythiaGex - ATAS autoarranque" /F
$ErrorActionPreference = "SilentlyContinue"
$log = Join-Path $env:APPDATA "ATAS\pythiagex-autoarranque.log"
function Anotar($m) { ("{0}  {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $m) | Out-File -FilePath $log -Append -Encoding utf8 }

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W2 {
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
"@

$p = Get-Process OFT.Platform -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) {
    Start-Process "C:\Program Files (x86)\ATAS Platform\OFT.Platform.exe"
    Anotar "ATAS no corria: lanzado"
    Start-Sleep -Seconds 25
    $p = Get-Process OFT.Platform -ErrorAction SilentlyContinue | Select-Object -First 1
}
if (-not $p) { Anotar "no pude lanzar ATAS"; exit 1 }

# hasta 90 s para que aparezca la ventana de login o la principal
for ($i = 0; $i -lt 45; $i++) {
    $p = Get-Process OFT.Platform -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p -and ($p.MainWindowTitle -eq "Authorization" -or $p.MainWindowTitle -like "ATAS*")) { break }
    Start-Sleep -Seconds 2
}
if ($p.MainWindowTitle -eq "Authorization") {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "Connect")
    $els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    $hecho = $false
    foreach ($e in $els) {
        foreach ($t in @($e, [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($e))) {
            try { $pat = $t.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern); $pat.Invoke(); $hecho = $true; break } catch {}
        }
        if ($hecho) { break }
    }
    Anotar ("ventana de login: Connect " + $(if ($hecho) { "invocado" } else { "NO encontrado" }))
    for ($i = 0; $i -lt 45; $i++) {
        Start-Sleep -Seconds 2
        $p = Get-Process OFT.Platform -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($p -and $p.MainWindowTitle -like "ATAS*") { break }
    }
}
$p = Get-Process OFT.Platform -ErrorAction SilentlyContinue | Select-Object -First 1
if ($p -and $p.MainWindowTitle -like "ATAS*") {
    if ($p.MainWindowHandle -ne 0) { [W2]::ShowWindow($p.MainWindowHandle, 3) | Out-Null }
    Anotar ("ATAS arriba: " + $p.MainWindowTitle)
} else {
    Anotar ("ATAS en estado raro: [" + $p.MainWindowTitle + "]")
}
