# -*- coding: utf-8 -*-
"""
ubicacion_06_freno.py — DESCRIPTIVO post-confirmacion (20 sesiones). El nivel FRENA al precio mas que un precio cualquiera?
Por dia: % de toques en que el precio pasa el nivel por 4 puntos o mas en 120 s, niveles reales (REF, MOV, RED) contra la grilla placebo (3 replicas).
Prueba t pareada entre dias. No cambia ningun veredicto.
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B, ubicacion_lib as UL
import numpy as np, pandas as pd

T = B.todas_las_sesiones(); S = sorted(T)
ev = UL.eventos(T, S); ev["G"] = ev["grupo"]
pl = pd.concat([UL.eventos(T, S, placebo_corr=c, etiqueta="pla%d" % int(c)) for c in UL.PLACEBO_CORRIMIENTOS], ignore_index=True); pl["G"] = "PLA"
todo = pd.concat([ev, pl], ignore_index=True); pasa = np.zeros(len(todo)); dev = np.zeros(len(todo))
for s, g in todo.groupby("ses"):
    hi = T[s]["alto"].to_numpy("float64"); lo = T[s]["bajo"].to_numpy("float64")
    for i, t, d, L in zip(g.index, g["t"].to_numpy(), g["dir"].to_numpy(), g["L"].to_numpy()):
        a = hi[t:t + 121].max(); b = lo[t:t + 121].min(); pasa[i] = (a - L) if d > 0 else (L - b); dev[i] = (L - b) if d > 0 else (a - L)
todo["pasa"] = pasa; todo["dev"] = dev
for umbral in (4, 8):
    por_dia = (todo.assign(x=todo["pasa"] >= umbral).groupby(["ses", "G"])["x"].mean().unstack() * 100)
    print("\npasa el nivel por >= %d pts en 120 s (%% por dia, promedio de 20 dias):" % umbral, por_dia.mean().round(1).to_dict())
    for G in ("REF", "MOV", "RED"):
        d = (por_dia[G] - por_dia["PLA"]).dropna(); print("   %s menos placebo: %+.1f puntos porcentuales | t pareada entre dias %+.2f | dias con el nivel frenando mas: %d de %d" % (G, d.mean(), d.mean() / (d.std(ddof=1) / np.sqrt(len(d))), (d < 0).sum(), len(d)))
por_dia = (todo.assign(x=todo["dev"] >= 8).groupby(["ses", "G"])["x"].mean().unstack() * 100)
print("\ndevuelve >= 8 pts desde el nivel en 120 s (%% por dia):", por_dia.mean().round(1).to_dict())
for G in ("REF", "MOV", "RED"):
    d = (por_dia[G] - por_dia["PLA"]).dropna(); print("   %s menos placebo: %+.1f pp | t %+.2f" % (G, d.mean(), d.mean() / (d.std(ddof=1) / np.sqrt(len(d)))))
