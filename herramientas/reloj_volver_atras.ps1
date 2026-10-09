# VOLVER EL RELOJ DE WINDOWS A COMO ESTABA el 07-10-2026 antes de arreglar_reloj.ps1 (valores leidos ese dia). Correr como administrador:
#   powershell -ExecutionPolicy Bypass -File reloj_volver_atras.ps1   (desde una ventana de PowerShell abierta como administrador)
$cfg = "HKLM:\SYSTEM\CurrentControlSet\Services\W32Time\Config"
$ntp = "HKLM:\SYSTEM\CurrentControlSet\Services\W32Time\TimeProviders\NtpClient"
w32tm /config /manualpeerlist:"time.windows.com,0x9" /syncfromflags:manual /update | Out-Null
Set-ItemProperty -Path $cfg -Name UpdateInterval -Value 360000 -Type DWord
Set-ItemProperty -Path $ntp -Name SpecialPollInterval -Value 16384 -Type DWord
Remove-ItemProperty -Path $cfg -Name MinPollInterval, MaxPollInterval, FrequencyCorrectRate -ErrorAction SilentlyContinue
w32tm /config /update | Out-Null
sc.exe config w32time start= demand | Out-Null
Write-Host "Listo: valores de fabrica de este equipo (servicio manual por disparador, time.windows.com, consulta cada 4,5 h)."
Read-Host "Enter para cerrar"
