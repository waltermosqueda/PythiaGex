# -*- coding: utf-8 -*-
"""verdes_04_llenado.py — lo mismo que verdes_02 pero CON LLENADO REALISTA: una orden limitada EN la raya solo se ejecuta si el precio la TOCA de verdad
(bajo <= raya para compra; alto >= raya para venta). Los rebotes que dan la vuelta un punto antes NO se operan; las rupturas se operan todas.
Costo: solo comision (0,60 pts) + medio spread de la salida a mercado cuando pierde (0,18): la entrada limitada no paga spread."""
import os, sys
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B
from verdes_01_hoy import estela
from verdes_02_toques import filas_de_capa, analizar, REGLAS

def llenados(seg, d):
    alto = seg["alto"].to_numpy(); bajo = seg["bajo"].to_numpy(); ok = []
    for _, r in d.iterrows():
        i = int(r["i"]); j = min(len(alto), i + 120)
        ok.append(bool((bajo[i:j] <= r["raya"]).any()) if r["lado"] > 0 else bool((alto[i:j] >= r["raya"]).any()))
    return d[np.array(ok, bool)]

def linea(d, nombre):
    out = ["%s: %d ordenes ejecutadas" % (nombre, len(d))]
    for G, S in REGLAS:
        y = d["r%d_%d" % (G, S)]; n = int((y != 0).sum())
        if not n: continue
        p = float((y[y != 0] > 0).mean()); neto = p * (G - 0.60) - (1 - p) * (S + 0.78)
        out.append("+%d/-%d: %.0f %% de %d -> neto %+.2f pts/op" % (G, S, 100 * p, n, neto))
    return " | ".join(out)

if __name__ == "__main__":
    capa = sys.argv[1]; T = B.todas_las_sesiones()
    for dia in sys.argv[2:]:
        seg = T[dia]; e = estela(capa, dia); e = e[(e.index >= seg.index[0]) & (e.index <= seg.index[-1])]; filas, vig = filas_de_capa(seg, e)
        real = llenados(seg, analizar(seg, filas, vig)); plc = pd.concat([llenados(seg, analizar(seg, filas, vig, delta=dl)) for dl in (12.5, -12.5, 7.0, -7.0)], ignore_index=True)
        print("#####", capa, dia); print(linea(real, "VERDES REALES, orden limitada en la raya")); print(linea(plc, "PLACEBO (rayas corridas)"))
