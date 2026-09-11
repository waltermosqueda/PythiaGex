@echo off
rem Doble clic: instala los indicadores de PythiaGex en ATAS (copia los DLL a %APPDATA%\ATAS\Indicators).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0instalar_indicadores.ps1"
echo.
pause
