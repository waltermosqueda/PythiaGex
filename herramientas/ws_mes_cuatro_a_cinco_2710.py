# -*- coding: utf-8 -*-
r"""ws_mes_cuatro_a_cinco_2710.py — 27-10-2026. En el workspace del operador ("MNQ liviano.ws"), en el grafico "MES 1m Chart", cambia la
PythiaGex 4.1 (PythiaGexCuatro.FamiliaCuatro: en MES no dibuja nada, cartel "los niveles estan en precio de NQ/MNQ") por la PythiaGex 5.0
(PythiaGexCinco.GammaCinco, la de ES/SPX/SPY), con sus ajustes de FABRICA: de la entrada vieja solo se conservan las claves genericas de
ATAS (panel, visibilidad, marco de calculo...); los ajustes propios de la 4.1 (S_*, *3*, *4*, *41*) no pasan (otra DLL, otros libros).
La 4.1 del grafico "MNQZ6 1m Chart" NO se toca.

Se corre con ATAS CERRADO (instalar_5_0.ps1 -AntesDeLanzar herramientas/ws_mes_cuatro_a_cinco_2710.py). Respalda el .ws antes en
PythiaGex/respaldos_ws/. Idempotente: si el grafico de MES ya tiene la 5.0, no hace nada. Uso: python -I herramientas/ws_mes_cuatro_a_cinco_2710.py [--ver]"""
import os, sys, json, shutil, datetime

APP = os.path.join(os.environ["APPDATA"], "ATAS")
WS = os.path.join(APP, "Workspaces_v3", "MNQ liviano.ws")
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RESP = os.path.join(RAIZ, "respaldos_ws")
TIPO_VIEJO = "PythiaGexCuatro.FamiliaCuatro, PythiaGexCuatro"
TIPO_NUEVO = "PythiaGexCinco.GammaCinco, PythiaGexCinco, Version=5.0.0.0, Culture=neutral, PublicKeyToken=null"
GENERICAS = ("CalculationTimeFrame", "AlertsEnabled", "FullScreenMode", "DenyToChangePanel", "DenyCalculationTimeFrameChange",
             "ShowDescription", "Panel", "IsVerticalIndicator", "Visible", "Locked", "AllowedInteraction")


def main():
    ver = "--ver" in sys.argv[1:]
    crudo = open(WS, "rb").read()
    bom = crudo.startswith(b"\xef\xbb\xbf")
    t = crudo.decode("utf-8-sig")
    a = t.find('"Title": "MES 1m Chart"')
    if a < 0:
        print("no encuentro el grafico 'MES 1m Chart': no toco nada"); return 1
    # el grafico termina donde empieza el titulo siguiente
    sig = [t.find('"Title": ', a + 10)]
    b = min(x for x in sig if x > 0) if sig[0] > 0 else len(t)
    seg = t[a:b]
    if "PythiaGexCinco.GammaCinco" in seg:
        print("el grafico de MES ya tiene la 5.0: no hago nada"); return 0
    # la entrada serializada es un literal JSON (string) que empieza con "{\r\n  \"Type\": \"PythiaGexCuatro.FamiliaCuatro
    i = seg.find(TIPO_VIEJO)
    if i < 0:
        print("el grafico de MES no tiene la 4.1: no hago nada"); return 0
    ini = seg.rfind('"{', 0, i)              # comienzo del literal (comilla + llave)
    j = ini + 1
    # fin del literal: la primera comilla no escapada desde ini+1
    while True:
        j = seg.find('"', j)
        k = j - 1; n = 0
        while seg[k] == "\\": n += 1; k -= 1
        if n % 2 == 0: break
        j += 1
    literal = seg[ini:j + 1]
    entrada = json.loads(json.loads(literal))          # literal -> str -> dict
    ajustes = json.loads(entrada["Settings"])
    nuevos = {k: ajustes[k] for k in GENERICAS if k in ajustes}
    nuevos["Name"] = "PythiaGex 5.0 - Gamma ES"
    entrada2 = {"Type": TIPO_NUEVO, "Settings": json.dumps(nuevos, indent=2, ensure_ascii=False).replace("\n", "\r\n")}
    literal2 = json.dumps(json.dumps(entrada2, indent=2, ensure_ascii=False).replace("\n", "\r\n"), ensure_ascii=False)
    print("grafico MES: entrada 4.1 de %d ajustes -> 5.0 con %d claves genericas (%s)" % (len(ajustes), len(nuevos), ", ".join(sorted(nuevos))))
    if ver:
        print("--ver: no escribo"); return 0
    os.makedirs(RESP, exist_ok=True)
    dst = os.path.join(RESP, "MNQ liviano_antes_5_0_%s.ws" % datetime.datetime.now().strftime("%Y%m%d_%H%M%S"))
    shutil.copy2(WS, dst)
    t2 = t[:a] + seg[:ini] + literal2 + seg[j + 1:] + t[b:]
    json.loads(t2)                                       # el workspace sigue siendo JSON valido
    with open(WS, "wb") as f:
        f.write((b"\xef\xbb\xbf" if bom else b"") + t2.encode("utf-8"))
    print("escrito; respaldo en", dst)
    return 0


if __name__ == "__main__":
    sys.exit(main())
