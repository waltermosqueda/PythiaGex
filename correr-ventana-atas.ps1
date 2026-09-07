# Corre la ventana principal de ATAS para que no quede debajo del chat.
# ATAS corre como OFT.Platform y arranca MAXIMIZADA: hay que restaurarla
# (SW_RESTORE) antes de moverla, o MoveWindow no hace nada.
param([int]$X = 0, [int]$Y = 0, [int]$Ancho = 1045, [int]$Alto = 850)

$sig = @'
using System;using System.Text;using System.Collections.Generic;using System.Runtime.InteropServices;
public class VentAtas {
  public delegate bool Cb(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(Cb c, IntPtr l);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int p);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int t,bool r);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  public static IntPtr Principal(int pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      int p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) {
        var sb = new StringBuilder(300); GetWindowText(h, sb, 300);
        if (sb.ToString().StartsWith("ATAS")) { found = h; return false; }
      }
      return true;
    }, IntPtr.Zero);
    return found;
  }
}
'@
Add-Type -TypeDefinition $sig -ErrorAction SilentlyContinue

$p = @(Get-Process -Name OFT.Platform -ErrorAction SilentlyContinue)
if ($p.Count -eq 0) { Write-Output "ATAS no esta abierto"; exit 1 }
$h = [VentAtas]::Principal($p[0].Id)
if ($h -eq [IntPtr]::Zero) { Write-Output "no encontre la ventana que empieza con ATAS"; exit 1 }
[void][VentAtas]::ShowWindow($h, 9)
Start-Sleep -Milliseconds 700
[void][VentAtas]::MoveWindow($h, $X, $Y, $Ancho, $Alto, $true)
Start-Sleep -Milliseconds 500
[void][VentAtas]::SetForegroundWindow($h)
Write-Output "ATAS movida a $X,$Y de ${Ancho}x${Alto}"
