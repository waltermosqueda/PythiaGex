# INSTALADOR EN CALIENTE generico (23-11-2026): copia CUALQUIER DLL de indicador a %APPDATA%\ATAS\Indicators y recarga sin reiniciar.
# Mismo procedimiento que instalar_superindicador.ps1 (ver ese archivo y la memoria recarga-en-caliente-atas): ATAS 8.0.15.306 detecta
# el DLL nuevo ("Created/Changed library", "Debounce timer elapsed"), muestra un boton azul sin nombre en la barra de estado (a la
# izquierda del icono de la esquina) y el aviso modal "Indicators were updated successfully" trae un "ОК" en letras CIRILICAS.
# Uso: powershell -File instalar_en_caliente.ps1 -Dll <ruta al DLL compilado> [-EsperaLog 40]
param([Parameter(Mandatory = $true)][string]$Dll, [int]$EsperaLog = 40)
$ErrorActionPreference = "Continue"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]; $TS = [System.Windows.Automation.TreeScope]
function Paso($m) { Write-Output ("{0}  {1}" -f (Get-Date -Format "HH:mm:ss"), $m) }
if (-not (Test-Path $Dll)) { Paso "no existe el DLL: $Dll"; exit 1 }
$nombre = Split-Path $Dll -Leaf
$dst = Join-Path $env:APPDATA ("ATAS\Indicators\" + $nombre)
$log = Join-Path $env:APPDATA ("ATAS\Logs\app_" + (Get-Date -Format "yyyyMMdd") + ".log")
$p = Get-Process OFT.Platform -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { Paso "ATAS no corre: copio el DLL y listo (lo toma al arrancar)"; Copy-Item -Force $Dll $dst; exit 0 }
$lineas0 = (Get-Content $log -ErrorAction SilentlyContinue | Measure-Object -Line).Lines
Copy-Item -Force $Dll $dst
Paso ("DLL copiado: {0} ({1} bytes, sha256 {2})" -f $nombre, (Get-Item $dst).Length, (Get-FileHash $dst -Algorithm SHA256).Hash.Substring(0, 12))
$visto = $false
$patron = [regex]::Escape($nombre) + "|Debounce timer elapsed"
for ($i = 0; $i -lt $EsperaLog; $i++) {
    Start-Sleep -Seconds 1
    $nuevas = Get-Content $log -ErrorAction SilentlyContinue | Select-Object -Skip $lineas0
    if ($nuevas | Where-Object { $_ -match $patron }) { $visto = $true; break }
}
Paso ("ATAS vio el cambio: {0}" -f $visto)
Start-Sleep -Seconds 2
$c = New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $p.Id)
$cands = @()
foreach ($w in $A::RootElement.FindAll($TS::Children, $c)) {
    $wr = $w.Current.BoundingRectangle
    foreach ($e in $w.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))) {
        $r = $e.Current.BoundingRectangle
        if ($r.Width -gt 0 -and $r.Y -ge ($wr.Bottom - 60) -and $r.X -ge ($wr.Right - 140) -and -not $e.Current.Name) { $cands += , @($e, $r) }
    }
}
Paso ("botones sin nombre en la esquina de la barra de estado: {0}" -f $cands.Count)
# 26-11: a veces el boton de recarga es el UNICO sin nombre de la esquina (el otro icono no estaba): alcanza con uno
if ($cands.Count -lt 1) { Paso "no aparecio el boton de recarga (ATAS no lo muestra si no hay cambios pendientes)"; exit 2 }
$b = ($cands | Sort-Object { $_[1].X } | Select-Object -First 1)
$b[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Paso ("recarga invocada (boton en x={0:0} y={1:0})" -f $b[1].X, $b[1].Y)
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
$nuevas | Where-Object { $_ -match ("reload indicator types|Notification title 'Indicators'|" + [regex]::Escape($nombre)) } | ForEach-Object { "   log: " + $_.Substring(0, [Math]::Min(170, $_.Length)) }
