# -*- coding: utf-8 -*-
"""verdes_11 — Y SI NO SOMOS TAN ESTRICTOS? La raya como ZONA: dejarle al precio traspasarla 10 o 12 puntos (stop mas ancho) y pedirle mas recorrido.
Verdes de NQ (7 dias) contra su placebo, orden limitada en la raya, con costo. Por dia, para ver si depende del 17-09."""
import os, sys
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B, verdes_lib as V
V.MERCADO["NQ"]["reglas"] = ((12, 6), (15, 10), (20, 10), (20, 12), (25, 12), (30, 15))
R, P = V.correr("NQ", B.todas_las_sesiones())
M = V.MERCADO["NQ"]
print("\nregla (a favor / en contra, desde la raya) | VERDES: acierto, n, neto por operacion | PLACEBO | azar puro || lo mismo SIN el 17-09")
for G, S in M["reglas"]:
    fila = "  +%d / -%d" % (G, S)
    for datos_r, datos_p in ((R, P), (R[R["dia"] != "2026-09-17"], P[P["dia"] != "2026-09-17"])):
        for d in (datos_r, datos_p):
            y = d["r%g_%g" % (G, S)]; n = int((y != 0).sum()); p = float((y[y != 0] > 0).mean())
            fila += " | %.1f %% (n %d) neto %+.2f" % (100 * p, n, p * (G - M["costo_gana"]) - (1 - p) * (S + M["costo_pierde"]))
        fila += " | azar %.1f %% |" % (100.0 * S / (G + S))
    print(fila)
