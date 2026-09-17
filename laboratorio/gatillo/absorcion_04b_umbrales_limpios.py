# -*- coding: utf-8 -*-
"""absorcion_04b_umbrales_limpios.py — percentiles de los rasgos LIMPIOS (despues >= 1) en los segundos validos de EXPLORAR. Sin resultados."""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd, base as B, absorcion_base as A
T = A.tablas(); E = B.explorar(T); P = []
for s, seg in E.items():
    f = A.rasgos(s, seg); v = A.valido_de(s, seg); P.append(f[v].assign(sesion=s))
P = pd.concat(P); print("segundos validos:", len(P), "| por sesion:", P.groupby("sesion").size().to_dict())
for c in ("AB10", "AA10", "AB30", "AA30", "AB60", "AA60"): print(c, "p90/p95/p99/p99.5:", np.percentile(P[c], [90, 95, 99, 99.5]))
print("|AB60-AA60| p95/p99:", np.percentile((P["AB60"] - P["AA60"]).abs(), [95, 99]))
rb = P["AB60"] / (P["RB60"] + 1); ra = P["AA60"] / (P["RA60"] + 1)
print("AB60/(RB60+1) p99: %.4f | AA60/(RA60+1) p99: %.4f | RB60 mediana %.0f | RA60 mediana %.0f" % (np.percentile(rb, 99), np.percentile(ra, 99), P["RB60"].median(), P["RA60"].median()))
print("OP30 p25/p75:", np.percentile(P["OP30"], [25, 75]), "| D60 p10/p90:", np.percentile(P["D60"], [10, 90]))
print("segundos con abs grande (vol>=10): bid %.1f ask %.1f por sesion | repone fuerte: bid %.1f ask %.1f" % (P["gb"].sum() / 12, P["ga"].sum() / 12, P["rb"].sum() / 12, P["ra"].sum() / 12))
print("pos600 cerca del minimo (ultimo <= min600+2): %.1f %% de los segundos | en el medio [0.35,0.65]: %.1f %%" % (100 * (P["pos600"] * (P["max600"] - P["min600"]) <= 2).mean(), 100 * P["pos600"].between(.35, .65).mean()))
print("rango 600 s mediano: %.1f pts" % (P["max600"] - P["min600"]).median())
