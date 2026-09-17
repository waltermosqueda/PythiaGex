# -*- coding: utf-8 -*-
"""gamma_02_conteo.py — SOLO CUENTA disparos por variante (NO calcula ni mira ninguna barrera): sirve para fijar umbrales con n razonable
ANTES de escribir el pre-registro. Tambien cuenta cuantos disparos caen en cada regimen y en cuantos dias."""
import sys, time; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, gamma_lib as L

t0 = time.time(); T = B.todas_las_sesiones(); EXP, CONF = L.mitades(T)
print("explorar:", sorted(EXP)); print("confirmar:", sorted(CONF))
filas = []
for mitad, S in (("EXP", EXP), ("CONF", CONF)):
    for s, d in S.items():
        disp, f, nv = L.disparos(d); z = nv["d_zero"].to_numpy(); e = f["elegible"].to_numpy()
        filas.append(dict(mitad=mitad, ses=s, var="_segundos_elegibles", n=int(e.sum()), pos=int((e & (z >= L.ZONA)).sum()), neg=int((e & (z <= -L.ZONA)).sum())))
        for v, (i, lado) in disp.items():
            filas.append(dict(mitad=mitad, ses=s, var=v, n=len(i), pos=int((z[i] >= L.ZONA).sum()), neg=int((z[i] <= -L.ZONA).sum())))
R = pd.DataFrame(filas)
g = R.groupby(["var", "mitad"]).agg(n=("n", "sum"), pos=("pos", "sum"), neg=("neg", "sum"), dias=("n", lambda x: int((x > 0).sum())), dias_pos=("pos", lambda x: int((x >= 5).sum())), dias_neg=("neg", lambda x: int((x >= 5).sum())))
print(g.to_string())
print("\nregimen por dia (segundos elegibles): %% POS / %% NEG")
e = R[R["var"] == "_segundos_elegibles"]
for _, r in e.iterrows(): print("  %s %s  POS %5.1f %%  NEG %5.1f %%" % (r["mitad"], r["ses"], 100 * r["pos"] / max(r["n"], 1), 100 * r["neg"] / max(r["n"], 1)))
print("%.0f s" % (time.time() - t0))
