# INSTALADOR EN CALIENTE de La Ola 1.0 (PythiaOla.dll) y del Superindicador 1.9b (PythiaSuperindicador.dll), 27-11-2026. NO reinicia ATAS.
# Copia de herramientas/instalar_superindicador.ps1 (23-11 / 26-11), con las dos DLL:
#   - ATAS 8.0.15.306 detecta el DLL nuevo o cambiado en %APPDATA%\ATAS\Indicators ("Created/Changed library", "Debounce timer elapsed") y
#     muestra en la barra de estado, a la izquierda del icono amarillo de la esquina, un boton azul (sin nombre para UIA) que recarga los
#     indicadores: "yModulesManager: Trying to reload indicator types" + un aviso modal "Indicators were updated successfully" cuyo boton
#     dice "ОК" en letras CIRILICAS (por eso buscar "OK" no lo encuentra). Las instancias que ya estan en los graficos pasan a la version nueva.
#   - Un DLL NUEVO (PythiaOla.dll la primera vez) no aparece en el buscador del dialogo Indicators hasta que se recarga: por eso se recarga
#     aunque La Ola todavia no este en ningun grafico.
#   - 26-11: el aviso modal se lleva el foco a ATAS, y en ATAS cada letra es un atajo de trading (ese dia sus teclas mandaron dos ordenes
#     reales). Por eso: espera a que teclado y mouse esten quietos $QuietoSeg segundos y, al terminar, devuelve el foco a la ventana que lo
#     tenia (nunca queda en ATAS).
# Uso: powershell -ExecutionPolicy Bypass -File herramientas\instalar_pythiaola.ps1
#      (-Dlls para elegir que copiar; por defecto las dos: atas\PythiaOla\bin\Release\PythiaOla.dll y atas\Superindicador\bin\Release\PythiaSuperindicador.dll)
param([string[]]$Dlls = @(), [int]$EsperaLog = 40, [int]$QuietoSeg = 0, [int]$EsperaMaxSeg = 300)   # 27-11: QuietoSeg 0 por pedido del operador ("si yo te lo pido hacelo ahora")
$ErrorActionPreference = "Continue"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]; $TS = [System.Windows.Automation.TreeScope]
function Paso($m) { Write-Output ("{0}  {1}" -f (Get-Date -Format "HH:mm:ss"), $m) }
Add-Type -Namespace PyG -Name W32 -MemberDefinition @'
[StructLayout(LayoutKind.Sequential)] public struct LII { public uint cbSize; public uint dwTime; }
[DllImport("user32.dll")] public static extern bool GetLastInputInfo(ref LII p);
[DllImport("user32.dll")] public static extern System.IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(System.IntPtr h);
'@ -ErrorAction SilentlyContinue
function Quieto() { $l = New-Object PyG.W32+LII; $l.cbSize = 8; [void][PyG.W32]::GetLastInputInfo([ref]$l); return ([Environment]::TickCount - [int]$l.dwTime) / 1000.0 }

if ($Dlls.Count -eq 0) {
    $Dlls = @((Join-Path $PSScriptRoot "..\atas\PythiaOla\bin\Release\PythiaOla.dll"),
              (Join-Path $PSScriptRoot "..\atas\Superindicador\bin\Release\PythiaSuperindicador.dll"))
}
foreach ($d in $Dlls) { if (-not (Test-Path $d)) { Paso ("NO esta el DLL {0}: compilar antes (dotnet build -c Release)" -f $d); exit 4 } }

$t0 = Get-Date
while ((Quieto) -lt $QuietoSeg -and ((Get-Date) - $t0).TotalSeconds -lt $EsperaMaxSeg) { Start-Sleep -Seconds 1 }
if ((Quieto) -lt $QuietoSeg) { Paso ("el teclado/mouse no estuvo quieto {0} s en {1} s: NO instalo (el aviso se llevaria el foco)" -f $QuietoSeg, $EsperaMaxSeg); exit 3 }
$ventanaPrevia = [PyG.W32]::GetForegroundWindow()
Paso ("teclado y mouse quietos {0:0} s: instalo" -f (Quieto))

$dirInd = Join-Path $env:APPDATA "ATAS\Indicators"
$log = Join-Path $env:APPDATA ("ATAS\Logs\app_" + (Get-Date -Format "yyyyMMdd") + ".log")
$p = Get-Process OFT.Platform -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) {
    foreach ($d in $Dlls) { Copy-Item -Force $d (Join-Path $dirInd (Split-Path $d -Leaf)) }
    Paso "ATAS no corre: copie los DLL y listo (los toma al arrancar)"; exit 0
}
$lineas0 = (Get-Content $log -ErrorAction SilentlyContinue | Measure-Object -Line).Lines
# 27-11: el boton azul se identifica por DIFERENCIA. Antes de copiar se anotan los botones (habilitados, clase Button, sin nombre) de la
# esquina inferior derecha de la ventana principal VISIBLE; despues del aviso de ATAS, el boton de recarga es el que aparecio NUEVO.
# Ese dia el unico de la esquina era el icono naranja (no la recarga) y con ATAS minimizado la busqueda vieja apreto el scroll del MES.
$c = New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $p.Id)
function BotonesEsquina {
    $res = @()
    foreach ($w in $A::RootElement.FindAll($TS::Children, $c)) {
        $wr = $w.Current.BoundingRectangle
        if ($wr.Width -lt 400 -or $wr.Height -lt 300 -or $wr.X -lt -10000) { continue }   # minimizada o chica: ahi no se busca
        foreach ($e in $w.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))) {
            $r = $e.Current.BoundingRectangle
            if ($r.Width -le 0 -or $e.Current.Name -or -not $e.Current.IsEnabled -or $e.Current.ClassName -ne "Button") { continue }
            if ($r.Y -lt ($wr.Bottom - 70) -or $r.X -lt ($wr.Right - 220) -or $r.Right -gt ($wr.Right + 1) -or $r.Bottom -gt ($wr.Bottom + 1)) { continue }
            $res += ,@($e, $r, ("{0:0},{1:0},{2:0},{3:0}" -f $r.X, $r.Y, $r.Width, $r.Height))
        }
    }
    return ,$res
}
$antes = BotonesEsquina
Paso ("botones de la esquina ANTES de copiar: {0} ({1})" -f $antes.Count, (($antes | ForEach-Object { $_[2] }) -join " ; "))
$nombres = @()
foreach ($d in $Dlls) {
    $leaf = Split-Path $d -Leaf; $nombres += [regex]::Escape($leaf)
    $dst = Join-Path $dirInd $leaf
    Copy-Item -Force $d $dst
    Paso ("DLL copiado: {0} {1} bytes sha256 {2}" -f $leaf, (Get-Item $dst).Length, (Get-FileHash $dst -Algorithm SHA256).Hash.Substring(0,12))
}
# esperar a que ATAS registre el cambio
$patron = ($nombres -join "|") + "|Debounce timer elapsed"
$visto = $false
for ($i = 0; $i -lt $EsperaLog; $i++) {
    Start-Sleep -Seconds 1
    $nuevas = Get-Content $log -ErrorAction SilentlyContinue | Select-Object -Skip $lineas0
    if ($nuevas | Where-Object { $_ -match $patron }) { $visto = $true; break }
}
Paso ("ATAS vio el cambio: {0}" -f $visto)
Start-Sleep -Seconds 2
# boton de recarga de la barra de estado: el que aparecio NUEVO en la esquina despues del aviso (ver BotonesEsquina)
$cands = @()
for ($i = 0; $i -lt 8 -and $cands.Count -eq 0; $i++) {
    $despues = BotonesEsquina
    $claves = @($antes | ForEach-Object { $_[2] })
    $cands = @($despues | Where-Object { $claves -notcontains $_[2] })
    if ($cands.Count -eq 0) { Start-Sleep -Seconds 1 }
}
Paso ("botones de la esquina DESPUES: {0} ({1}); nuevos: {2}" -f $despues.Count, (($despues | ForEach-Object { $_[2] }) -join " ; "), $cands.Count)
if ($cands.Count -lt 1) { Paso "no aparecio un boton NUEVO en la esquina: NO aprieto nada (el boton azul lo aprieta el operador)"; exit 2 }
if ($cands.Count -gt 1) { Paso ("AMBIGUO: {0} botones nuevos en la esquina: NO aprieto nada (el boton azul de la barra de estado lo aprieta el operador)" -f $cands.Count); exit 5 }   # 27-11
$b = $cands[0]
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
$nuevas | Where-Object { $_ -match ("reload indicator types|" + ($nombres -join "|") + "|Notification title 'Indicators'") } | ForEach-Object { "   log: " + $_.Substring(0, [Math]::Min(170, $_.Length)) }
