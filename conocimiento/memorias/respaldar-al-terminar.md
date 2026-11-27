---
name: respaldar-al-terminar
description: "Regla del operador (09-10): cuando una tanda queda estable (sin cambios en curso ni nada roto), SIEMPRE respaldar al terminar: GitHub publico + privado + espejo Inversiones (Drive)"
metadata:
  node_type: memory
  type: feedback
  originSessionId: f5879819-e2a4-405b-87b0-959ace090d7b
  modified: 2026-11-26T16:55:46.351Z
---

Textual (09-10-2026 ~20:55 ART): "y acordate: una vez sin mas cambios o algo roto fixeando, al terminar siempre guardar, respaldar los
archivos, actualizacion".

**Why:** quiere poder recuperar todo si pierde la PC (ver [[resguardo-en-la-nube]]); antes el respaldo se hacia solo cuando lo pedia.

**How to apply:** al cerrar cada tanda (instalada + verificada en pantalla + arneses en verde, y ningun workflow editando el proyecto):
1. commit + push del repo publico PythiaGex (respaldar.py tacha los patrones de terceros; nada de cuenta, login LT-…, USD ni .ws);
2. `PythiaGex-privado` (respaldar_privado.ps1: workspace, DLL instaladas, memorias) commit + push;
3. `herramientas/respaldo_inversiones.ps1` (espejo en Escritorio\Inversiones\PythiaGex-respaldo, que Google Drive sube).
   **SUSPENDIDO desde el 26-11-2026:** su Google Drive se lleno ("colapso", le piden pagar mas) y el espejo pesa ~9,9 GB.
   No correrlo hasta que el lo pida. Datos intermedios de investigacion (velas, eventos, pickles) van al scratchpad o
   %TEMP%, nunca a carpetas sincronizadas; al repo solo scripts y resultados chicos.
Nunca respaldar a mitad de una edicion de un agente. Decirle en una linea que quedo respaldado (hash de los commits).
