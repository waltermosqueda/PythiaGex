@echo off
rem Sube cada minuto lo que Gamma Hoy escribe en esta PC a la rama "cadenas" de GitHub (para la web).
rem Se lanza desde la carpeta Inicio de Windows; corre en segundo plano sin ventana.
cd /d "%~dp0.."
start "" /min pythonw herramientas\subir_vivo.py --bucle
