# -*- coding: utf-8 -*-
"""Paso 0 del estudio de las RAYAS: los niveles por minuto (rebobinado de Gamma Hoy) contra la cinta. Si el cierre de cada vela del rebobinado
y el cierre del mismo minuto en la cinta difieren en una constante por sesion (0, o el spread U6/Z6), los relojes y los precios estan alineados."""
import sys, os
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B

T = B.todas_las_sesiones(); N = B.niveles_m1()
print("niveles: %d minutos, %s -> %s" % (len(N), N.index.min(), N.index.max()))
for s, seg in sorted(T.items()):
    m = seg["ultimo"].resample("1min").last().rename("cinta")
    j = pd.concat([m, N["c"].rename("reb")], axis=1).dropna()
    j = j[(j.index >= seg.index[0]) & (j.index <= seg.index[-1])]
    if len(j) < 50: print(s, "sin niveles"); continue
    dif = j["reb"] - j["cinta"]; r = j[(j.index.strftime("%H:%M") >= "13:30") & (j.index.strftime("%H:%M") < "20:00")]; difr = r["reb"] - r["cinta"]
    print("%s  minutos con nivel %4d (rueda %3d) | reb - cinta: mediana %+8.2f  p5 %+8.2f  p95 %+8.2f | en rueda |dif - mediana| <= 2 pts: %.1f %%" % (
        s, len(j), len(r), dif.median(), dif.quantile(0.05), dif.quantile(0.95), 100 * ((difr - difr.median()).abs() <= 2).mean() if len(r) else float("nan")))
