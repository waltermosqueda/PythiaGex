# -*- coding: utf-8 -*-
"""verdes_12 — H3 (pre-registrada 17-09 18:55, despues de ver en NQ que la raya como ZONA ANCHA le gana al placebo: +30/-15 -> 35,9 % contra 29,0 %, y sin el 17-09 35,2 contra 28,6):
en ES/MES, a la misma escala (1/4): reglas +7,5/-3,75 y +6,25/-3 desde la raya, orden limitada: las verdes de ES rebotan >= 4 puntos porcentuales mas que su placebo. Una sola corrida."""
import os, sys, datetime
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B, verdes_lib as V
V.MERCADO["ES"]["reglas"] = ((7.5, 3.75), (6.25, 3.0), (3, 1.5))
R, P = V.correr("ES", B.sesiones_de("MES")); M = V.MERCADO["ES"]; out = []
for G, S in M["reglas"]:
    y = R["r%g_%g" % (G, S)]; yp = P["r%g_%g" % (G, S)]; pr = float((y[y != 0] > 0).mean()); pp = float((yp[yp != 0] > 0).mean()); nr = int((y != 0).sum()); npl = int((yp != 0).sum())
    z = (pr - pp) / np.sqrt(pr * (1 - pr) / nr + pp * (1 - pp) / npl)
    out.append("ES +%g/-%g: VERDES %.1f %% (n %d, neto %+.2f pts) | placebo %.1f %% (n %d, neto %+.2f) | diferencia %+.1f, z %+.2f | azar %.1f %%" % (
        G, S, 100 * pr, nr, pr * (G - M["costo_gana"]) - (1 - pr) * (S + M["costo_pierde"]), 100 * pp, npl, pp * (G - M["costo_gana"]) - (1 - pp) * (S + M["costo_pierde"]), 100 * (pr - pp), z, 100 * S / (G + S)))
print("\n".join(out))
open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "resultados", "verdes.md"), "a", encoding="utf-8").write("\n## H3: la raya como zona ancha en ES (pre-registrada y corrida %s)\n\n" % datetime.datetime.now().strftime("%Y-%m-%d %H:%M") + "\n".join("    " + x for x in out) + "\n")
