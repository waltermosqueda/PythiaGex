# Trae la ventana principal de ATAS al frente (la del chat de Claude suele quedar encima del lado derecho de sus dialogos
# y se come los clics: visto en la captura del operador del 06-10-2026). Usar ANTES de cualquier clic con computer-use.
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W4 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
}
"@
$p = Get-Process OFT.Platform -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { Write-Output "ATAS no corre"; exit 1 }
$h = $p.MainWindowHandle
if ([W4]::IsIconic($h)) { [W4]::ShowWindow($h, 9) | Out-Null }
# truco Alt para que Windows permita el cambio de foco desde otro proceso
[W4]::keybd_event(0x12,0,0,[UIntPtr]::Zero); $ok = [W4]::SetForegroundWindow($h); [W4]::keybd_event(0x12,0,2,[UIntPtr]::Zero)
Write-Output ("ATAS al frente: {0} ('{1}')" -f $ok, $p.MainWindowTitle)
