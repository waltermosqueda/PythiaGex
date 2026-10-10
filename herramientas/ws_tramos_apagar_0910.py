# -*- coding: utf-8 -*-
"""ws_tramos_apagar_0910.py — pedido del operador (09-10-2026 ~21:30 ART): "las etiquetas del medio sacalas, quedamos en eso: solo a la
derecha". Apaga SOLO la casilla Tramos41Rotulos (rotulos de los tramos de historia) de la 4.1 en el workspace 'MNQ liviano' (el default del
codigo pasa a apagado en la 4.1.6d). Correr con ATAS cerrado (instalar_4_0.ps1 -AntesDeLanzar). Respalda el .ws."""
import os, re, shutil, sys, time

WS = os.path.join(os.environ["APPDATA"], "ATAS", "Workspaces_v3", "MNQ liviano.ws")
RESP = os.path.join(os.path.dirname(os.path.abspath(__file__)), "respaldos_ws")


def main(prop="Tramos41Rotulos", valor="false"):
    os.makedirs(RESP, exist_ok=True)
    copia = os.path.join(RESP, "MNQ liviano." + time.strftime("%Y%m%d-%H%M%S") + ".ws")
    shutil.copy2(WS, copia)
    raw = open(WS, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    txt = raw.decode("utf-8-sig") if bom else raw.decode("utf-8")
    pat = re.compile(r'(' + re.escape(prop) + r'\\\\\\": )(true|false)')
    antes = pat.findall(txt)
    txt, k = pat.subn(r'\g<1>' + valor, txt)
    open(WS, "wb").write((b"\xef\xbb\xbf" if bom else b"") + txt.encode("utf-8"))
    print("%s %s -> %s en %d lugar(es) | respaldo %s" % (prop, [a[1] for a in antes], valor, k, copia))
    return 0 if k >= 1 else 3


if __name__ == "__main__":
    sys.exit(main(*sys.argv[1:3]))
