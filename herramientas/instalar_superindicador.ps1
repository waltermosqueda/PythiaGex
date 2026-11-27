# INSTALADOR EN CALIENTE del Superindicador (23-11-2026). NO reinicia ATAS.
# Descubierto ese dia: ATAS 8.0.15.306 detecta el DLL nuevo o cambiado en %APPDATA%\ATAS\Indicators ("Created/Changed library",
# "Debounce timer elapsed") y muestra en la barra de estado, a la izquierda del icono amarillo de la esquina, un boton azul (32x32,
# sin nombre para UIA) que recarga los indicadores: "yModulesManager: Trying to reload indicator types" + un aviso modal
# "Indicators were updated successfully" cuyo boton dice "ОК" en letras CIRILICAS (por eso buscar "OK" no lo encuentra).
# Las instancias que ya estan en los graficos pasan a la version nueva.
param([string]$Dll = "", [int]$EsperaLog = 40, [int]$QuietoSeg = 20, [int]$EsperaMaxSeg = 300)
$ErrorActionPreference = "Continue"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]; $TS = [System.Windows.Automation.TreeScope]
function Paso($m) { Write-Output ("{0}  {1}" -f (Get-Date -Format "HH:mm:ss"), $m) }
# 26-11-2026: el aviso modal de la recarga se lleva el foco a ATAS, y en ATAS cada letra es un atajo de trading (ese dia sus teclas
# mandaron dos ordenes reales). Por eso: esperar a que teclado y mouse esten quietos $QuietoSeg segundos y, al terminar, devolver el
# foco a la ventana que lo tenia.
Add-Type -Namespace PyG -Name W32 -MemberDefinition @'
[StructLayout(LayoutKind.Sequential)] public struct LII { public uint cbSize; public uint dwTime; }
[DllImport("user32.dll")] public static extern bool GetLastInputInfo(ref LII p);
[DllImport("user32.dll")] public static extern System.IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(System.IntPtr h);
'@
function Quieto() { $l = New-Object PyG.W32+LII; $l.cbSize = 8; [void][PyG.W32]::GetLastInputInfo([ref]$l); return ([Environment]::TickCount - [int]$l.dwTime) / 1000.0 }
$t0 = Get-Date
while ((Quieto) -lt $QuietoSeg -and ((Get-Date) - $t0).TotalSeconds -lt $EsperaMaxSeg) { Start-Sleep -Seconds 1 }
if ((Quieto) -lt $QuietoSeg) { Paso ("el teclado/mouse no estuvo quieto {0} s en {1} s: NO instalo (el aviso se llevaria el foco)" -f $QuietoSeg, $EsperaMaxSeg); exit 3 }
$ventanaPrevia = [PyG.W32]::GetForegroundWindow()
Paso ("teclado y mouse quietos {0:0} s: instalo" -f (Quieto))
if ($Dll -eq "") { $Dll = Join-Path $PSScriptRoot "..\atas\Superindicador\bin\Release\PythiaSuperindicador.dll" }
$dst = Join-Path $env:APPDATA "ATAS\Indicators\PythiaSuperindicador.dll"
$log = Join-Path $env:APPDATA ("ATAS\Logs\app_" + (Get-Date -Format "yyyyMMdd") + ".log")
$p = Get-Process OFT.Platform -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { Paso "ATAS no corre: copio el DLL y listo (lo toma al arrancar)"; Copy-Item -Force $Dll $dst; exit 0 }
$lineas0 = (Get-Content $log -ErrorAction SilentlyContinue | Measure-Object -Line).Lines
Copy-Item -Force $Dll $dst
Paso ("DLL copiado: {0} bytes sha256 {1}" -f (Get-Item $dst).Length, (Get-FileHash $dst -Algorithm SHA256).Hash.Substring(0,12))
# esperar a que ATAS registre el cambio
$visto = $false
for ($i = 0; $i -lt $EsperaLog; $i++) {
    Start-Sleep -Seconds 1
    $nuevas = Get-Content $log -ErrorAction SilentlyContinue | Select-Object -Skip $lineas0
    if ($nuevas | Where-Object { $_ -match "PythiaSuperindicador.dll|Debounce timer elapsed" }) { $visto = $true; break }
}
Paso ("ATAS vio el cambio: {0}" -f $visto)
Start-Sleep -Seconds 2
# boton de recarga de la barra de estado: el boton sin nombre mas a la izquierda de los dos de la esquina inferior derecha
$c = New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $p.Id)
$cands = @()
foreach ($w in $A::RootElement.FindAll($TS::Children, $c)) {
    $wr = $w.Current.BoundingRectangle
    if ($wr.Width -lt 400 -or $wr.Height -lt 300 -or $wr.X -lt -10000) { Paso ("ventana de ATAS minimizada o chica ({0:0}x{1:0} en x={2:0}): ahi no se busca el boton" -f $wr.Width, $wr.Height, $wr.X); continue }   # 27-11: con ATAS minimizado eligio la flecha del scroll del grafico MES
    foreach ($e in $w.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))) {
        $r = $e.Current.BoundingRectangle
        if ($r.Width -gt 0 -and $r.Y -ge ($wr.Bottom - 60) -and $r.X -ge ($wr.Right - 140) -and -not $e.Current.Name) { $cands += ,@($e, $r) }
    }
}
Paso ("botones sin nombre en la esquina de la barra de estado: {0}" -f $cands.Count)
# 26-11: a veces el boton de recarga es el UNICO sin nombre de la esquina (el otro icono no estaba): alcanza con uno
if ($cands.Count -lt 1) { Paso "no aparecio el boton de recarga (ATAS no lo muestra si no hay cambios pendientes)"; exit 2 }
if ($cands.Count -gt 3) { Paso ("AMBIGUO: {0} botones sin nombre en la esquina: NO aprieto nada (el boton azul de la barra de estado lo aprieta el operador)" -f $cands.Count); exit 5 }   # 27-11
$b = ($cands | Sort-Object { $_[1].X } | Select-Object -First 1)
$b[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Paso ("recarga invocada (boton en x={0:0} y={1:0})" -f $b[1].X, $b[1].Y)
# aviso modal con el OK cirilico
$ok = $false
for ($i = 0; $i -lt 30 -and -not $ok; $i++) {
    Start-Sleep -Milliseconds 700
    foreach ($w in $A::RootElement.FindAll($TS::Children, $c)) {
        $t = $w.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::AutomationIdProperty, "PART_Message")))
        if ($t) {
            $btn = $w.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
            Paso ("aviso: '{0}'" -f $t.Current.Name)
            if ($btn) { $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); $ok = $true; Paso "aviso cerrado" }
        }
    }
}
# 26-11: el foco vuelve a la ventana que lo tenia antes (nunca queda en ATAS)
if ($ventanaPrevia -ne [IntPtr]::Zero) { Start-Sleep -Milliseconds 300; [void][PyG.W32]::SetForegroundWindow($ventanaPrevia); Paso "foco devuelto a la ventana anterior" }
$nuevas = Get-Content $log -ErrorAction SilentlyContinue | Select-Object -Skip $lineas0
$nuevas | Where-Object { $_ -match "reload indicator types|PythiaSuperindicador|Notification title 'Indicators'" } | ForEach-Object { "   log: " + $_.Substring(0, [Math]::Min(170, $_.Length)) }
