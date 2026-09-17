# -*- coding: utf-8 -*-
"""punta_00_sanidad.py — mira SOLO la forma de los rasgos (punta, ofi, ofi_pas) en las sesiones de EXPLORAR. No mira ningun resultado futuro.
Sirve para fijar umbrales del pre-registro sin haber visto si la señal acierta."""
import sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B

T = B.todas_las_sesiones(); E = B.explorar(T)
print("sesiones:", len(T), "| explorar:", len(E), "| confirmar:", len(B.confirmar(T)))
for s, d in T.items():
    r = d[d["rueda"]]
    print(s, "filas", len(d), "| rueda", len(r), "| de", d.index[0], "a", d.index[-1], "| seg con ordenes en rueda %.0f %%" % (100 * (r["n"] > 0).mean()))

R = pd.concat([d[d["rueda"]] for d in E.values()])
q = [0.01, 0.05, 0.25, 0.5, 0.75, 0.95, 0.99]
sp = (R["ask"] - R["bid"]); print("\nspread (pts) cuantiles:", sp.quantile(q).round(2).to_dict(), "| <=0: %.2f %%" % (100 * (sp <= 0).mean()))
for c in ("bidv", "askv"): print(c, R[c].quantile(q).round(1).to_dict(), "media %.2f" % R[c].mean())
qi = (R["bidv"] - R["askv"]) / (R["bidv"] + R["askv"]).replace(0, np.nan)
print("QI cuantiles:", qi.quantile(q).round(2).to_dict(), "| |QI|>=0.5: %.1f %% | |QI|>=0.8: %.1f %%" % (100 * (qi.abs() >= 0.5).mean(), 100 * (qi.abs() >= 0.8).mean()))
for c in ("ofi", "ofi_pas", "delta", "vol"):
    print(c, "por segundo:", R[c].quantile(q).round(1).to_dict(), "desvio %.2f" % R[c].std())
for W in (10, 30, 60, 300):
    a = {c: pd.concat([d[c].rolling(W).sum()[d["rueda"]] for d in E.values()]) for c in ("ofi", "ofi_pas", "delta")}
    print("W=%ds  desvio ofi %.1f | ofi_pas %.1f | delta %.1f | corr(ofi_pas, delta) %.2f | corr(ofi, delta) %.2f | autocorr ofi_pas (sin solape) %.2f" % (
        W, a["ofi"].std(), a["ofi_pas"].std(), a["delta"].std(), a["ofi_pas"].corr(a["delta"]), a["ofi"].corr(a["delta"]),
        np.mean([d["ofi_pas"].rolling(W).sum()[d["rueda"]].iloc[::W].autocorr(1) for d in E.values()])))
# cuanto dura una punta sin actualizarse (la punta solo se muestrea en cada orden)
g = (R["n"] > 0).astype(int); print("\nsegundos de rueda SIN ordenes (punta vieja): %.1f %%" % (100 * (1 - g.mean())))
