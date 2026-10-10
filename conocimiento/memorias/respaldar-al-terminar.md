---
name: respaldar-al-terminar
description: "Regla del operador (09-10): cuando una tanda queda estable (sin cambios en curso ni nada roto), SIEMPRE respaldar al terminar: GitHub publico + privado + espejo Inversiones (Drive)"
metadata:
  node_type: memory
  type: feedback
  originSessionId: f5879819-e2a4-405b-87b0-959ace090d7b
  modified: 2026-10-09T23:51:05.653Z
---

Textual (09-10-2026 ~20:55 ART): "y acordate: una vez sin mas cambios o algo roto fixeando, al terminar siempre guardar, respaldar los
archivos, actualizacion".

**Why:** quiere poder recuperar todo si pierde la PC (ver [[resguardo-en-la-nube]]); antes el respaldo se hacia solo cuando lo pedia.

**How to apply:** al cerrar cada tanda (instalada + verificada en pantalla + arneses en verde, y ningun workflow editando el proyecto):
1. commit + push del repo publico PythiaGex (respaldar.py tacha los patrones de terceros; nada de cuenta, login LT-…, USD ni .ws);
2. `PythiaGex-privado` (respaldar_privado.ps1: workspace, DLL instaladas, memorias) commit + push;
3. `herramientas/respaldo_inversiones.ps1` (espejo en Escritorio\Inversiones\PythiaGex-respaldo, que Google Drive sube).
Nunca respaldar a mitad de una edicion de un agente. Decirle en una linea que quedo respaldado (hash de los commits).
