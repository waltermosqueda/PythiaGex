# -*- coding: utf-8 -*-
"""
ubicacion_01_conteo.py — arma los toques de EXPLORAR (niveles reales y grilla placebo), los cuenta y CONGELA los umbrales de flujo.
NO mira ningun resultado (ni barreras ni precio futuro): solo cuantos toques hay y como se reparten los rasgos.
"""
import sys, os, json, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B, ubicacion_lib as UL
import numpy as np, pandas as pd

t0 = time.time(); T = B.todas_las_sesiones(); E = sorted(B.explorar(T))
ev = UL.eventos(T, E); print("eventos reales en explorar: %d (%.0f s)" % (len(ev), time.time() - t0))
print("\npor sesion:"); print(ev.groupby("ses").size().to_string())
print("\npor grupo (evento puede tener varios):", {g: int(ev["tiene_" + g].sum()) for g in ("REF", "MOV", "RED")})
niv = ev["niveles"].str.split("+").explode(); niv = niv.str.replace(r"^R\d+$", "R50/100", regex=True)
print("\npor nivel:"); print(niv.value_counts().to_string())
print("\ndir:", ev["dir"].value_counts().to_dict(), "| confluencia:", int(ev["conf"].sum()))
print("\nrasgos (cuantiles 10/33/50/67/90):")
for c in ("fd30", "fd60", "fd300", "a_niv30", "a_op30", "pas30", "bar30", "rompe30", "vel60", "vel300", "V30"):
    print("  %-8s" % c, np.round(ev[c].quantile([.1, .33, .5, .67, .9]).to_numpy(), 4), "| ceros: %.0f %%" % (100 * (ev[c] == 0).mean()))

mov = ev[ev["tiene_MOV"] == 1]
U = dict(a_niv30_q67=float(ev["a_niv30"].quantile(.67)), a_niv30_q33=float(ev["a_niv30"].quantile(.33)),
         fd60_q67=float(ev["fd60"].quantile(.67)), fd60_q33=float(ev["fd60"].quantile(.33)), bar30_q67=float(ev["bar30"].quantile(.67)),
         pas30_q33=float(ev["pas30"].quantile(.33)), pas30_q67=float(ev["pas30"].quantile(.67)), fd300_mov_q33=float(mov["fd300"].quantile(.33)))
json.dump(U, open(UL.UMBRALES, "w", encoding="utf-8"), indent=1); print("\numbrales congelados:", json.dumps(U, indent=1))

print("\ndisparos por variante en explorar (desagrupados, sin resultados):")
for v in UL.VARIANTES:
    if v == "MODELO": continue
    e = UL.variante(ev, v, U); print("  %-12s %5d  | por dia min %d max %d" % (v, len(e), e.groupby("ses").size().min(), e.groupby("ses").size().max()))

for c in UL.PLACEBO_CORRIMIENTOS:
    pe = UL.eventos(T, E, placebo_corr=c, etiqueta="pla%d" % int(c)); print("placebo de lugar corrido %d: %d toques" % (c, len(pe)))
print("total %.0f s" % (time.time() - t0))
