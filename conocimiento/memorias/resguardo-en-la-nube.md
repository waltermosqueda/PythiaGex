---
name: resguardo-en-la-nube
description: "Que hay y que no hay en la nube (2026-09-11): OneDrive NO esta instalado (la carpeta 'OneDrive' del escritorio no sincroniza); el repo publico tiene codigo, DLL, instalador y conocimiento; lo privado (workspace de ATAS, plantillas, viva de Rithmic) va a PythiaGex-privado con respaldar_privado.ps1; push y gh los corre el operador porque el harness los bloquea."
metadata:
  type: project
---

Pregunta del operador (11-09 02:20): si pierde la PC, se pierde todo? Como pone los
indicadores en un ATAS virgen rapido?

- **OneDrive no esta instalado** (solo quedan UpdaterService/setup en Program Files y
  el proceso no corre). La carpeta `OneDrive\Escritorio\ATAS nada` es una carpeta local
  con ese nombre: NO sincroniza. El espejo `Inversiones\PythiaGex-respaldo` (robocopy)
  tambien es local. Lo unico en la nube es GitHub.
- **Repo publico waltermosqueda/PythiaGex** (es PUBLICO; sin cuenta ni mail, verificado
  con git grep): fuente de los dos indicadores, `PythiaGexNiveles.dll` y `PythiaVwap.dll`
  compilados (bin/Release, versionados), `atas/instalar/` (instalar_indicadores.ps1 + .bat +
  README de recuperacion), el archivo de cadenas (rama `cadenas`) y `conocimiento/`
  (memorias, bitacora, guias, CLAUDE.md via respaldar.py).
- **Repo privado PythiaGex-privado** (carpeta `Escritorio\ATAS nada\PythiaGex-privado`):
  `respaldar_privado.ps1` espeja Workspaces_v3 (trae el numero de cuenta, por eso privado),
  Chart/Templates, UnifiedTemplates, IndicatorTemplates, ClusterTemplates, DrawingObjectTemplates,
  PythiaGexiva (grabaciones Rithmic), contexto, base-*.json, los DLL instalados y las
  memorias; excluye github.token y archivos > 90 MB (local-*.jsonl). 166 archivos, 44 MB.
  Primer commit local hecho; `-SinSubir` deja solo el commit.
- **Hecho el 11-09 02:47 con permiso explicito ("hace vos, te doy permiso")**: push del
  publico OK; repo privado creado y subido (fa11be5). El harness bloqueo `gh release create`
  incluso con permiso: el zip (248 KB) quedo en `Escritorio\ATAS nada\indicadores-2026-09-11.zip`
  y el README explica recuperar con Code -> Download ZIP del repo, sin Release. Su terminal es
  PowerShell 5.1: los comandos que le paso no pueden llevar `&&` ni rutas `/c/...`.
- Restaurar en PC nueva: ATAS + Rithmic a mano; zip de la Release -> doble clic al .bat ->
  Indicators -> Gamma Hoy -> un clic -> Add to chart -> Apply -> Workspaces Save. Con el repo
  privado, copiar Workspaces_v3 y plantillas a %APPDATA%\ATAS con ATAS cerrado.

**Why:** hasta hoy creia que OneDrive respaldaba el escritorio; no. Y el DLL del VWAP no
estaba versionado.

**How to apply:** al cerrar cada sesion: `python respaldar.py` (publico) y
"Respaldar privado.bat" (privado). Ver [[respaldo-del-conocimiento]],
[[archivo-cadenas-y-respaldo]], [[compilar-indicadores-atas]].
