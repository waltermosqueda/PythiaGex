# INSTALADOR EN CALIENTE del Superindicador (23-11-2026). NO reinicia ATAS.
# Descubierto ese dia: ATAS 8.0.15.306 detecta el DLL nuevo o cambiado en %APPDATA%\ATAS\Indicators ("Created/Changed library",
# "Debounce timer elapsed") y muestra en la barra de estado, a la izquierda del icono amarillo de la esquina, un boton azul (32x32,
# sin nombre para UIA) que recarga los indicadores: "yModulesManager: Trying to reload indicator types" + un aviso modal
# "Indicators were updated successfully" cuyo boton dice "ОК" en letras CIRILICAS (por eso buscar "OK" no lo encuentra).
# Las instancias que ya estan en los graficos pasan a la version nueva.
param([string]$Dll = "", [int]$EsperaLog = 40)
$ErrorActionPreference = "Continue"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]; $TS = [System.Windows.Automation.TreeScope]
function Paso($m) { Write-Output ("{0}  {1}" -f (Get-Date -Format "HH:mm:ss"), $m) }
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
    foreach ($e in $w.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))) {
        $r = $e.Current.BoundingRectangle
        if ($r.Width -gt 0 -and $r.Y -ge ($wr.Bottom - 60) -and $r.X -ge ($wr.Right - 140) -and -not $e.Current.Name) { $cands += ,@($e, $r) }
    }
}
Paso ("botones sin nombre en la esquina de la barra de estado: {0}" -f $cands.Count)
# 26-11: a veces el boton de recarga es el UNICO sin nombre de la esquina (el otro icono no estaba): alcanza con uno
if ($cands.Count -lt 1) { Paso "no aparecio el boton de recarga (ATAS no lo muestra si no hay cambios pendientes)"; exit 2 }
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
$nuevas = Get-Content $log -ErrorAction SilentlyContinue | Select-Object -Skip $lineas0
$nuevas | Where-Object { $_ -match "reload indicator types|PythiaSuperindicador|Notification title 'Indicators'" } | ForEach-Object { "   log: " + $_.Substring(0, [Math]::Min(170, $_.Length)) }
