@echo off
rem Baja la cadena de CBOE desde esta PC cada 75 s en la rueda (cada 5 min fuera) a %APPDATA%\ATAS\PythiaGex\cboe-local.
rem Gamma Hoy la usa antes que la nube (que corre cada 8-25 min). Se lanza desde la carpeta Inicio de Windows, sin ventana.
cd /d "%~dp0.."
start "" /min pythonw herramientas\cboe_local.py --bucle
