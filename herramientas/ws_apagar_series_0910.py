# -*- coding: utf-8 -*-
"""ws_apagar_series_0910.py — pedido del operador (09-10-2026 ~02:50 ART): "estas dos que marque (31076) podes desactivar por defecto ...
suman ruido al ser ya superadas por la formula de la 2.0 qqq (31083)". En 31.076 (QQQ 750 con la razon sincronizada) dibujaban 4 series a la
vez y en 31.075 la familia por OI: se apagan las 5 en el workspace 'MNQ liviano' (ATAS persiste las casillas POR NOMBRE en el .ws).
Correr SOLO con ATAS cerrado (lo llama instalar_4_0.ps1 -SinDll -AntesDeLanzar). Respalda el .ws antes de tocarlo.
Uso manual: python -I ws_apagar_series_0910.py [SERIE ...]"""
import os, re, shutil, sys, time

APAGAR = ["MUROS_QQQ_vol", "MUROS_QQQ_oi", "MAJORS_QQQ_oi", "DOMS_QQQ_vol", "FAM_MUROS_oi"]
WS = os.path.join(os.environ["APPDATA"], "ATAS", "Workspaces_v3", "MNQ liviano.ws")
RESP = os.path.join(os.path.dirname(os.path.abspath(__file__)), "respaldos_ws")


def main(series):
    if not os.path.exists(WS):
        print("NO EXISTE " + WS); return 2
    os.makedirs(RESP, exist_ok=True)
    copia = os.path.join(RESP, "MNQ liviano." + time.strftime("%Y%m%d-%H%M%S") + ".ws")
    shutil.copy2(WS, copia)
    raw = open(WS, "rb").read()
    txt = raw.decode("utf-8-sig") if raw.startswith(b"\xef\xbb\xbf") else raw.decode("utf-8")
    bom = raw.startswith(b"\xef\xbb\xbf")
    hechos = []
    for sid in series:
        # dentro de Settings (json escapado dos veces): S_<id>\\\": true
        pat = re.compile(r'(S_' + re.escape(sid) + r'\\\\\\": )(true|false)')
        n = len(pat.findall(txt))
        txt, k = pat.subn(r'\1false', txt)
        hechos.append("%s:%d" % (sid, n))
    open(WS, "wb").write((b"\xef\xbb\xbf" if bom else b"") + txt.encode("utf-8"))
    print("apagadas " + ", ".join(hechos) + " | respaldo " + copia)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:] or APAGAR))
