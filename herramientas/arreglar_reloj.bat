@echo off
rem Doble clic: pide permiso de administrador (aceptar "Si") y corre arreglar_reloj.ps1 en esta misma carpeta.
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%~dp0arreglar_reloj.ps1\"'"
