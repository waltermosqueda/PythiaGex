# -*- coding: utf-8 -*-
"""verdes_06 — las verdes de la capa NQ RECONSTRUIDAS (misma cuenta del indicador sobre la cadena viva grabada) en todos los dias con grabacion,
con llenado realista (orden limitada EN la raya; se ejecuta solo si el precio la toca) y contra el placebo (las mismas rayas corridas)."""
import os, sys
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B
from verdes_01_hoy import estela
from verdes_02_toques import filas_de_capa, analizar, REGLAS
from verdes_04_llenado import llenados
from verdes_05_reconstruir import REC

DIAS = ["2026-09-08", "2026-09-09", "2026-09-11", "2026-09-14", "2026-09-15", "2026-09-16", "2026-09-17"]
T = B.todas_las_sesiones(); R = []; P = []
for dia in DIAS:
    seg = T[dia]; e = estela("NQ", dia, carpeta=REC); e = e[(e.index >= seg.index[0]) & (e.index <= seg.index[-1])]
    if len(e) < 50: print(dia, "sin grabacion suficiente"); continue
    # solo los tramos con grabacion fresca: la raya vale hasta 5 minutos despues de su ultima foto
    s = e.reindex(seg.index, method="ffill", tolerance=pd.Timedelta(minutes=5)); e2 = s.dropna(how="all")
    filas, vig = filas_de_capa(seg, e2)
    for K in filas:   # fuera de cobertura la raya no existe
        sin = s[["d0", "d1"]].isna().all(axis=1).to_numpy(); filas[K] = np.where(sin, np.nan, filas[K]); vig[K] = vig[K] & ~sin
    r = llenados(seg, analizar(seg, filas, vig)); r["dia"] = dia; R.append(r)
    p = pd.concat([llenados(seg, analizar(seg, filas, vig, delta=dl)) for dl in (12.5, -12.5, 7.0, -7.0)], ignore_index=True); p["dia"] = dia; P.append(p)
    cob = 100 * (~s[["d0", "d1"]].isna().all(axis=1))[seg["rueda"]].mean()
    def t(d, G=12, S=6):
        y = d["r%d_%d" % (G, S)]; n = int((y != 0).sum()); return (100 * float((y[y != 0] > 0).mean()) if n else float("nan")), n
    print("%s (cobertura de rueda %.0f %%): VERDES +12/-6 %.0f %% de %d | placebo %.0f %% de %d || +20/-8: %.0f %% de %d | placebo %.0f %% de %d" % ((dia, cob) + t(r) + t(p) + t(r, 20, 8) + t(p, 20, 8)))
R = pd.concat(R, ignore_index=True); P = pd.concat(P, ignore_index=True)
print("\nTODOS LOS DIAS JUNTOS (orden limitada en la raya, costo 0,60 de comision + 0,18 de salida a mercado si pierde):")
for G, S in REGLAS:
    out = []
    for nombre, d in (("VERDES", R), ("placebo", P)):
        y = d["r%d_%d" % (G, S)]; n = int((y != 0).sum()); p = float((y[y != 0] > 0).mean()); out.append("%s %.1f %% de %d (neto %+.2f pts/op)" % (nombre, 100 * p, n, p * (G - 0.60) - (1 - p) * (S + 0.78)))
    yr = R["r%d_%d" % (G, S)]; yp = P["r%d_%d" % (G, S)]; pr = (yr[yr != 0] > 0).mean(); pp = (yp[yp != 0] > 0).mean()
    z = (pr - pp) / np.sqrt(pr * (1 - pr) / (yr != 0).sum() + pp * (1 - pp) / (yp != 0).sum())
    print("   +%d/-%d: %s | %s | z %+.2f" % (G, S, out[0], out[1], z))
print("   sin el 17-09:")
R2 = R[R["dia"] != "2026-09-17"]; P2 = P[P["dia"] != "2026-09-17"]
for G, S in ((12, 6), (20, 8)):
    yr = R2["r%d_%d" % (G, S)]; yp = P2["r%d_%d" % (G, S)]; pr = (yr[yr != 0] > 0).mean(); pp = (yp[yp != 0] > 0).mean()
    print("   +%d/-%d: VERDES %.1f %% de %d | placebo %.1f %% de %d" % (G, S, 100 * pr, (yr != 0).sum(), 100 * pp, (yp != 0).sum()))
R.to_parquet(os.path.join(B.CACHE, "verdes_toques.parquet")); P.to_parquet(os.path.join(B.CACHE, "verdes_placebo.parquet"))
