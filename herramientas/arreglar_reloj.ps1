# ARREGLAR EL RELOJ DE WINDOWS (07-10-2026). Lo corre el OPERADOR como administrador (doble clic en arreglar_reloj.bat y aceptar "Si").
# Diagnostico (solo lectura, 07-10 14:55): el servicio "Hora de Windows" (w32time) esta en arranque por disparador, como viene de fabrica:
# sincroniza y se apaga. Para diferencias chicas corrige de a poco (Config\UpdateInterval = 360000 = 1 hora) y como se apaga antes, la
# correccion no termina: la PC quedaba ~0,59 s atrasada aunque el log dice que sincronizo a las 13:33 y 14:41.
# Que hace este script:
#   1. deja el servicio en arranque AUTOMATICO (siempre prendido corrigiendo);
#   2. tres servidores de hora (time.windows.com, time.google.com, pool.ntp.org);
#   3. consulta cada 15 min y corrige rapido (UpdateInterval 100 = 1 s, ajustes recomendados por Microsoft para precision de 1 s);
#   4. sincroniza ya y mide antes / despues.
# Para volver a como estaba: reloj_volver_atras.ps1 (mismo procedimiento).
$ErrorActionPreference = "Continue"
$log = Join-Path $PSScriptRoot "arreglar_reloj.log"
function Paso($m) { $l = "{0}  {1}" -f (Get-Date -Format "HH:mm:ss"), $m; Write-Host $l; Add-Content -Path $log -Value $l -Encoding UTF8 }
function Medir($cuando) {
    $r = w32tm /stripchart /computer:time.google.com /samples:3 /dataonly 2>&1 | Select-Object -Last 3
    Paso ("desfase {0} (time.google.com; signo +: la PC esta atrasada):" -f $cuando)
    foreach ($x in $r) { Paso ("    " + $x) }
}
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { Paso "ERROR: hay que correrlo como administrador (usa arreglar_reloj.bat y acepta 'Si')."; Read-Host "Enter para cerrar"; exit 1 }
Paso "=== arreglar el reloj de Windows ==="
Medir "ANTES"
Paso "1. servicio en arranque automatico y prendido"
sc.exe config w32time start= auto | Out-Null
Start-Service w32time -ErrorAction SilentlyContinue
Paso "2. servidores de hora"
w32tm /config /manualpeerlist:"time.windows.com,0x9 time.google.com,0x9 pool.ntp.org,0x9" /syncfromflags:manual /update | Out-Null
Paso "3. consultar cada 15 min y corregir rapido"
$cfg = "HKLM:\SYSTEM\CurrentControlSet\Services\W32Time\Config"
$ntp = "HKLM:\SYSTEM\CurrentControlSet\Services\W32Time\TimeProviders\NtpClient"
Set-ItemProperty -Path $cfg -Name UpdateInterval -Value 100 -Type DWord
Set-ItemProperty -Path $cfg -Name MinPollInterval -Value 6 -Type DWord
Set-ItemProperty -Path $cfg -Name MaxPollInterval -Value 10 -Type DWord
Set-ItemProperty -Path $cfg -Name FrequencyCorrectRate -Value 2 -Type DWord
Set-ItemProperty -Path $ntp -Name SpecialPollInterval -Value 900 -Type DWord
w32tm /config /update | Out-Null
Restart-Service w32time -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3
Paso "4. sincronizar ya"
w32tm /resync /rediscover 2>&1 | ForEach-Object { Paso ("    " + $_) }
Start-Sleep -Seconds 10
Medir "DESPUES (si todavia quedan unos ms, el servicio los termina de corregir en los proximos minutos)"
$s = Get-Service w32time
Paso ("servicio: {0}, arranque {1}" -f $s.Status, (Get-CimInstance Win32_Service -Filter "Name='w32time'").StartMode)
Paso "listo. Log en $log"
Read-Host "Enter para cerrar"
