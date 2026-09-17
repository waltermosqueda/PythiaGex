# -*- coding: utf-8 -*-
"""absorcion_02b_huecos.py — las tres sesiones donde la tabla de 1 s no cubre la cinta: que paso (solo lectura)."""
import sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, os
PQ = os.path.join(B.CACHE, "cinta.parquet"); T = B.todas_las_sesiones()
for s in ("2026-09-03", "2026-09-14", "2026-09-16"):
    seg = T[s]; d = pd.read_parquet(PQ, filters=[("sesion", "==", s)], columns=["t", "ultimo", "vol", "rueda", "px_grupo"])
    print("\n====", s, "tabla 1s:", seg.index[0], "->", seg.index[-1], "| cinta:", d["t"].min(), "->", d["t"].max(), "filas", len(d))
    r = d[d["rueda"]].set_index("t")
    m = r.resample("15min").agg({"ultimo": ["min", "max", "size"], "px_grupo": ["min", "max"]})
    sr = seg[seg["rueda"]]; m2 = sr.resample("15min").agg({"ultimo": ["min", "max"], "n": "sum"})
    print(pd.concat([m, m2], axis=1).to_string())
    # saltos de precio entre ordenes consecutivas de la cinta (dos contratos mezclados se verian como saltos enormes)
    dp = r["ultimo"].diff().abs(); print("saltos > 30 pts entre ordenes consecutivas:", int((dp > 30).sum()), "| max", dp.max())
    gap = r.index.to_series().diff().dt.total_seconds(); print("huecos > 30 s en la cinta (rueda):", [(str(i), int(g)) for i, g in gap[gap > 30].items()])
