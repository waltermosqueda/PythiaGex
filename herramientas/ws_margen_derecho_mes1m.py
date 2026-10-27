# -*- coding: utf-8 -*-
"""ws_margen_derecho_mes1m.py — 27-10-2026. Sube el margen derecho (ChartOffset, en velas) SOLO del grafico "MES 1m Chart" del espacio
"MNQ liviano" para que la columna de etiquetas de la 5.0 y la franja de barras ES entren sin pisar las ultimas velas (control de las 20:47 ART:
con el precio en el nivel, "3.0 SPX D1 1,63B · Clasica SPX D2" caia encima de las velas y no se leia). Correr con ATAS CERRADO
(instalar_5_0.ps1 -SinDll -AntesDeLanzar). Respaldo previo junto al .ws (MNQ liviano.ws.antes-margen-<hora>). Uso: [--velas 50]"""
import os, re, sys, json, shutil, datetime
VELAS = int(sys.argv[sys.argv.index("--velas") + 1]) if "--velas" in sys.argv else 50
p = os.path.join(os.environ["APPDATA"], "ATAS", "Workspaces_v3", "MNQ liviano.ws")
b = open(p, "rb").read(); bom = b.startswith(b"\xef\xbb\xbf")
t = b.decode("utf-8-sig")
d = json.loads(t)
paneles = d["Panels"]
idx = [i for i, x in enumerate(paneles) if isinstance(x, dict) and x.get("Title") == "MES 1m Chart"]
if len(idx) != 1: sys.exit("no encontre UN panel 'MES 1m Chart' (%s)" % idx)
antes = paneles[idx[0]]["Parameter"]["Template"]["colorsettings"]["ChartOffset"]
# el n-esimo "ChartOffset" del texto es el del n-esimo panel que lo tiene (mismo orden que el JSON)
con = [i for i, x in enumerate(paneles) if isinstance(x, dict) and "ChartOffset" in json.dumps(x.get("Parameter", {}).get("Template", {}).get("colorsettings", {}))]
n = con.index(idx[0])
ms = list(re.finditer(r'"ChartOffset"\s*:\s*\d+', t))
if len(ms) != len(con): sys.exit("ChartOffset en el texto %d, en los paneles %d: no toco nada" % (len(ms), len(con)))
m = ms[n]
nuevo = t[:m.start()] + '"ChartOffset": %d' % VELAS + t[m.end():]
d2 = json.loads(nuevo)
assert d2["Panels"][idx[0]]["Parameter"]["Template"]["colorsettings"]["ChartOffset"] == VELAS
for i in con:
    if i != idx[0]: assert d2["Panels"][i]["Parameter"]["Template"]["colorsettings"]["ChartOffset"] == d["Panels"][i]["Parameter"]["Template"]["colorsettings"]["ChartOffset"]
shutil.copy2(p, p + ".antes-margen-" + datetime.datetime.now().strftime("%Y%m%d-%H%M%S"))
open(p, "wb").write(("﻿" if bom else "").encode("utf-8") + nuevo.encode("utf-8"))
print("MES 1m Chart: ChartOffset %s -> %d velas (los otros graficos sin tocar)" % (antes, VELAS))
