# -*- coding: utf-8 -*-
"""ws_alto_estadistica_visible.py — 28-10-2026. Da al panel de "PythiaGex - Estadistica Visible" del espacio "MNQ liviano" el MISMO alto
que el panel del Cluster Statistic de fabrica del mismo grafico (ATAS lo crea con 60 y las filas quedan mas bajas que la letra: sin numeros).
Correr con ATAS CERRADO (instalar_5_0.ps1 -SinDll -AntesDeLanzar). Respaldo previo junto al .ws (MNQ liviano.ws.antes-alto-<hora>).
Uso: [--alto N] (por defecto ALTO; la 1.0 usaba el alto del Cluster Statistic del mismo grafico)."""
import os, re, sys, json, shutil, datetime
# 28-10 23:50 (1.2): 120 = tres renglones de letra angosta por fila: los numeros de las ultimas velas entran aun con el grafico
# mas alejado. El operador ya saco el Cluster Statistic de fabrica (92), asi que el total sigue por debajo de lo que tenia.
ALTO = 120
p = os.path.join(os.environ["APPDATA"], "ATAS", "Workspaces_v3", "MNQ liviano.ws")
b = open(p, "rb").read(); bom = b.startswith(b"\xef\xbb\xbf")
t = b.decode("utf-8-sig")
d = json.loads(t)


def paneles_de(o, titulo=None, out=None):
    """(titulo del grafico, objeto PanelSettings) en el orden del texto."""
    out = [] if out is None else out
    if isinstance(o, dict):
        if isinstance(o.get("Title"), str): titulo = o["Title"]
        if "SerializedIndicators" in o: out.append((titulo, o))
        for v in o.values(): paneles_de(v, titulo, out)
    elif isinstance(o, list):
        for v in o: paneles_de(v, titulo, out)
    return out


ps = paneles_de(d)
mio = [(i, x) for i, (tt, x) in enumerate(ps) if any("EstadisticaVisible" in s for s in x["SerializedIndicators"])]
if len(mio) != 1: sys.exit("no encontre UN panel de Estadistica Visible (%d)" % len(mio))
i_mio, pan = mio[0]; titulo = ps[i_mio][0]
fab = [x for tt, x in ps if tt == titulo and any("ClusterStatistic" in s for s in x["SerializedIndicators"])]
alto = int(sys.argv[sys.argv.index("--alto") + 1]) if "--alto" in sys.argv else ALTO
antes = pan["Height"]
# el n-esimo "SerializedIndicators" del texto es el n-esimo panel; su "Height" es el primero que aparece despues
ms = [m.start() for m in re.finditer(r'"SerializedIndicators"\s*:', t)]
if len(ms) != len(ps): sys.exit("paneles en el texto %d, en el JSON %d: no toco nada" % (len(ms), len(ps)))
h = re.compile(r'"Height"\s*:\s*\d+').search(t, ms[i_mio])
fin = ms[i_mio + 1] if i_mio + 1 < len(ms) else len(t)
if not h or h.start() > fin: sys.exit("no encontre el Height del panel: no toco nada")
nuevo = t[:h.start()] + '"Height": %d' % alto + t[h.end():]
d2 = json.loads(nuevo); ps2 = paneles_de(d2)
assert ps2[i_mio][1]["Height"] == alto
for j, (tt, x) in enumerate(ps):
    if j != i_mio: assert ps2[j][1]["Height"] == x["Height"]
shutil.copy2(p, p + ".antes-alto-" + datetime.datetime.now().strftime("%Y%m%d-%H%M%S"))
open(p, "wb").write(("﻿" if bom else "").encode("utf-8") + nuevo.encode("utf-8"))
print("%s: panel de Estadistica Visible %s -> %d (Cluster Statistic de fabrica: %s; los demas paneles sin tocar)" % (titulo, antes, alto, fab[0]["Height"] if fab else "no esta"))
