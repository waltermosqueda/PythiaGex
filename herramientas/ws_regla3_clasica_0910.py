# -*- coding: utf-8 -*-
"""ws_regla3_clasica_0910.py — pedido del operador (09-10-2026 ~16:48 ART): las dominantes de NQ de la 4.1 "estan desplazadas con respecto a la
clasica (31123)". El motor 3.0 de la 4.1 usaba la regla Tres (histeresis + strike exacto en la rueda); la clasica dibuja el CENTROIDE de +-12 pts
sin histeresis. Pone Regla3Dominantes = clasica en el workspace 'MNQ liviano' (solo el dibujo 3.0 de la 4.1 lo usa: las series TRES_* estan apagadas).
Correr con ATAS cerrado (instalar_4_0.ps1 -AntesDeLanzar). Respalda el .ws."""
import os, re, shutil, sys, time

WS = os.path.join(os.environ["APPDATA"], "ATAS", "Workspaces_v3", "MNQ liviano.ws")
RESP = os.path.join(os.path.dirname(os.path.abspath(__file__)), "respaldos_ws")


def main(valor="clasica"):
    os.makedirs(RESP, exist_ok=True)
    copia = os.path.join(RESP, "MNQ liviano." + time.strftime("%Y%m%d-%H%M%S") + ".ws")
    shutil.copy2(WS, copia)
    raw = open(WS, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    txt = raw.decode("utf-8-sig") if bom else raw.decode("utf-8")
    pat = re.compile(r'(Regla3Dominantes\\\\\\": \\\\\\")([A-Za-z]+)(\\\\\\")')
    antes = pat.findall(txt)
    txt, k = pat.subn(r'\g<1>' + valor + r'\g<3>', txt)
    open(WS, "wb").write((b"\xef\xbb\xbf" if bom else b"") + txt.encode("utf-8"))
    print("Regla3Dominantes %s -> %s en %d lugar(es) | respaldo %s" % ([a[1] for a in antes], valor, k, copia))
    return 0 if k == 1 else 3


if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else "clasica"))
