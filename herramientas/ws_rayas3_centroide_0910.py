# -*- coding: utf-8 -*-
"""ws_rayas3_centroide_0910.py — pedido del operador (09-10-2026 ~16:56 ART): la NQ D1 de la 4.1 queda en el strike exacto (31.120) y la de la clasica en
el centroide (31.123): "esta desplazada con respecto a la original clasica". En la 4.1 el dibujo 3.0 (Tres41Dominantes) ahora copia a la clasica: apaga
Rayas3Independientes (3.5.0 del 08-10: strike exacto, sin promediar) SOLO en la 4.1 del workspace 'MNQ liviano' (la 3.0 no esta en este .ws).
Correr con ATAS cerrado (instalar_4_0.ps1 -AntesDeLanzar). Respalda el .ws."""
import os, re, shutil, sys, time

WS = os.path.join(os.environ["APPDATA"], "ATAS", "Workspaces_v3", "MNQ liviano.ws")
RESP = os.path.join(os.path.dirname(os.path.abspath(__file__)), "respaldos_ws")


def main(valor="false"):
    os.makedirs(RESP, exist_ok=True)
    copia = os.path.join(RESP, "MNQ liviano." + time.strftime("%Y%m%d-%H%M%S") + ".ws")
    shutil.copy2(WS, copia)
    raw = open(WS, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    txt = raw.decode("utf-8-sig") if bom else raw.decode("utf-8")
    pat = re.compile(r'(Rayas3Independientes\\\\\\": )(true|false)')
    antes = pat.findall(txt)
    txt, k = pat.subn(r'\g<1>' + valor, txt)
    open(WS, "wb").write((b"\xef\xbb\xbf" if bom else b"") + txt.encode("utf-8"))
    print("Rayas3Independientes %s -> %s en %d lugar(es) | respaldo %s" % ([a[1] for a in antes], valor, k, copia))
    return 0 if k == 1 else 3


if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else "false"))
