# -*- coding: utf-8 -*-
"""barridos_conteo.py — SOLO cuenta disparos por variante en las sesiones de explorar (NO mira ningun resultado). Sirve para saber,
antes de cerrar el pre-registro, si las definiciones dan muestra suficiente."""
import time
import numpy as np, pandas as pd
import barridos_lib as L
B = L.B

t0 = time.time()
T = B.explorar(B.todas_las_sesiones())
filas = {}
for s, d in T.items():
    dis, f = L.disparos(d)
    filas[s] = {v: len(i) for v, (i, l) in dis.items()}
    filas[s]["(compras %)"] = round(100 * np.mean(np.concatenate([l for i, l in dis.values() if len(l)]) > 0), 0)
c = pd.DataFrame(filas).T
print(c.to_string())
print("\nTOTAL explorar:"); print(c.drop(columns="(compras %)").sum().to_string())
print("\n%.0f s" % (time.time() - t0))
