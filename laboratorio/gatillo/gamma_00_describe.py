# -*- coding: utf-8 -*-
"""gamma_00_describe.py — SOLO DESCRIPTIVO (no mira resultados de barrera): cobertura de los niveles de gamma por sesion, regimen por dia,
y en que mitad (alternada) cae cada sesion. Se corre ANTES del pre-registro para saber cuanto n hay."""
import sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B

N = B.niveles_m1()
print("niveles_m1:", N.shape, N.index.min(), "->", N.index.max()); print(N.columns.tolist())
print(N.describe().T.to_string())
print("cuadrante:", N["cuadrante"].value_counts(dropna=False).to_dict())
T = B.todas_las_sesiones(); ses = sorted(T)
print("\nsesiones:", len(ses))
hm = N.index.strftime("%H:%M"); N["rueda"] = (hm >= "13:30") & (hm < "20:00"); N["ses"] = (N.index + pd.Timedelta(hours=2)).strftime("%Y-%m-%d")
print("\n%-11s %-4s %5s %5s | %6s %6s %6s | %7s %7s %7s | %5s %5s %5s %5s | %s" % ("sesion", "mit", "minR", "conNv", "%arr", "zmed", "zstd", "|dom0|", "|dom1|", "|mp|", "c5z", "c5d0", "c5d1", "c5mj", "cuadrantes"))
for k, s in enumerate(ses):
    d = T[s]; r = N[(N["ses"] == s) & N["rueda"]]
    minR = int(d["rueda"].sum() // 60); z = r["d_zero"].dropna()
    cu = r["cuadrante"].value_counts(dropna=False).to_dict()
    print("%-11s %-4s %5d %5d | %6.1f %6.0f %6.0f | %7.0f %7.0f %7.0f | %5d %5d %5d %5d | %s" % (
        s, "EXP" if k % 2 == 0 else "CONF", minR, len(z), 100 * (z > 0).mean() if len(z) else np.nan, z.median() if len(z) else np.nan, z.std() if len(z) else np.nan,
        r["d_dom0"].abs().median(), r["d_dom1"].abs().median(), r["d_mp"].abs().median(),
        (r["d_zero"].abs() <= 5).sum(), (r["d_dom0"].abs() <= 5).sum(), (r["d_dom1"].abs() <= 5).sum(), ((r["d_mp"].abs() <= 5) | (r["d_mn"].abs() <= 5)).sum(), cu))
    hu = d.loc[d["rueda"], "hueco"].mean()
    if hu > 0.01: print("            hueco en rueda: %.1f %%" % (100 * hu))
