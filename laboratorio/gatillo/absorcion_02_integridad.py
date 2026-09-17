# -*- coding: utf-8 -*-
"""absorcion_02_integridad.py — chequeo de integridad: la tabla de 1 s contra la cinta cruda, sesion por sesion (solo lectura)."""
import sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, os
PQ = os.path.join(B.CACHE, "cinta.parquet"); T = B.todas_las_sesiones()
for s, seg in T.items():
    d = pd.read_parquet(PQ, filters=[("sesion", "==", s)], columns=["t", "ultimo", "vol", "rueda", "px_grupo"])
    r = d[d["rueda"]]; g = r.groupby("px_grupo").size()
    # dos contratos a la vez? en un mismo segundo, precios separados por mas de 60 puntos
    sec = r["t"].dt.floor("s"); rng = r.groupby(sec)["ultimo"].agg(["min", "max"]); doble = int(((rng["max"] - rng["min"]) > 60).sum())
    sr = seg[seg["rueda"]]
    print(s, "cinta rueda: %7d ordenes, vol %8d | tabla 1s: n %7d, vol %8d | cubre %.1f %% | grupos %s | seg con >60 pts de rango: %d | seg de rueda %d, con n>0 %d, hueco max sin ordenes %d s" % (
        len(r), r["vol"].sum(), sr["n"].sum(), sr["vol"].sum(), 100 * sr["vol"].sum() / max(1, r["vol"].sum()), g.to_dict(), doble, len(sr), int((sr["n"] > 0).sum()),
        int((sr["n"] == 0).astype(int).groupby((sr["n"] > 0).cumsum()).sum().max())))
