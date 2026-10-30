# -*- coding: utf-8 -*-
"""copiar_estadistica.py — 29-10-2026. Copia atas/EstadisticaVisible/bin/Release/EstadisticaVisible.dll a %APPDATA%\\ATAS\\Indicators con ATAS
CERRADO (instalar_5_0.ps1 -SinDll -AntesDeLanzar herramientas/copiar_estadistica.py). Respalda el DLL anterior al lado
(EstadisticaVisible.dll.antes-<hora>) e imprime tamaño y SHA-256 de los dos."""
import datetime
import hashlib
import os
import shutil

AQUI = os.path.dirname(os.path.abspath(__file__))
src = os.path.join(AQUI, "..", "atas", "EstadisticaVisible", "bin", "Release", "EstadisticaVisible.dll")
dst = os.path.join(os.environ["APPDATA"], "ATAS", "Indicators", "EstadisticaVisible.dll")


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest()[:12]


if os.path.exists(dst):
    resp = dst + ".antes-" + datetime.datetime.now().strftime("%H%M%S")
    shutil.copy2(dst, resp)
    print("respaldo", os.path.basename(resp), os.path.getsize(resp), "bytes sha", sha(resp))
shutil.copy2(src, dst)
print("EstadisticaVisible.dll instalado", os.path.getsize(dst), "bytes sha", sha(dst), "== origen" if sha(dst) == sha(src) else "DISTINTO DEL ORIGEN")
