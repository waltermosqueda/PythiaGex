# -*- coding: utf-8 -*-
"""ws_prender_tres41_dominantes_0910.py — pedido del operador (09-10-2026 ~16:40 ART): ver en la 4.1 las dominantes de NQ "como la
clasica" (NQ D1/D2 con su estela de guiones, rotulos y majors) mientras se construye la 4.1.6. Prende SOLO el ajuste Tres41Dominantes
del motor 3.0 que la 4.1 ya trae adentro (regla Tres = la clasica a secas fuera de la rueda de NY: una por lado + empate 20 % +
centroide 12). No toca ninguna otra casilla. Correr con ATAS cerrado (instalar_4_0.ps1 -SinDll -AntesDeLanzar). Respalda el .ws."""
import os, re, shutil, sys, time

WS = os.path.join(os.environ["APPDATA"], "ATAS", "Workspaces_v3", "MNQ liviano.ws")
RESP = os.path.join(os.path.dirname(os.path.abspath(__file__)), "respaldos_ws")


def main(valor="true"):
    if not os.path.exists(WS):
        print("NO EXISTE " + WS); return 2
    os.makedirs(RESP, exist_ok=True)
    copia = os.path.join(RESP, "MNQ liviano." + time.strftime("%Y%m%d-%H%M%S") + ".ws")
    shutil.copy2(WS, copia)
    raw = open(WS, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    txt = raw.decode("utf-8-sig") if bom else raw.decode("utf-8")
    pat = re.compile(r'(Tres41Dominantes\\\\\\": )(true|false)')
    n = len(pat.findall(txt))
    txt, k = pat.subn(r'\g<1>' + valor, txt)
    open(WS, "wb").write((b"\xef\xbb\xbf" if bom else b"") + txt.encode("utf-8"))
    print("Tres41Dominantes=%s en %d lugar(es) | respaldo %s" % (valor, n, copia))
    return 0 if n == 1 else 3


if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else "true"))
