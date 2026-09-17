# -*- coding: utf-8 -*-
"""Paso 0 (T5m ajustado a q70 y E5m a q50 por conteo, ANTES de mirar resultados; ver el .md): umbrales (percentiles de los RASGOS, sin mirar resultados) en exploracion, y conteo de disparos por variante. Congela escalas_umbrales.json."""
import sys, json, time
import numpy as np, pandas as pd
import escalas_lib as L
B = L.B

t0 = time.time(); T = B.todas_las_sesiones(); E = B.explorar(T)
acum = {k: [] for k in ("r15c", "V15c", "r300c", "V300c", "r15", "r60", "r300", "r900", "rs60", "rl60", "rs300", "rl300", "rs900", "rl900", "vol1m")}
F = {}
for s in sorted(E):
    seg = E[s]; f = L.rasgos(seg); F[s] = f; val = f["valido"]; sod = f["sod"]
    c15 = ((sod + 1) % 15 == 0) & val; c300 = ((sod + 1) % 300 == 0) & val
    acum["r15c"].append(np.abs(f["r15"][c15])); acum["V15c"].append(f["V15"][c15]); acum["r300c"].append(np.abs(f["r300"][c300])); acum["V300c"].append(f["V300"][c300])
    for k in ("r15", "r60", "r300", "r900", "rs60", "rl60", "rs300", "rl300", "rs900", "rl900"): acum[k].append(np.abs(f[k][val]))
    c60 = ((sod + 1) % 60 == 0) & val; acum["vol1m"].append(L.rsum(seg["vol"].to_numpy("float64"), 60)[c60])
A = {k: np.concatenate(v) for k, v in acum.items()}
q = lambda k, p: float(np.nanpercentile(A[k], p))
U = dict(T15s_r=q("r15c", 95), T15s_vmin=q("V15c", 25), T5m_r=q("r300c", 70), T5m_vmin=q("V300c", 25),
         r15_q50=q("r15", 50), r60_q50=q("r60", 50), r300_q50=q("r300", 50), r900_q50=q("r900", 50), E5m_r=q("r300", 50),
         rs60_q50=q("rs60", 50), rl60_q50=q("rl60", 50), rs300_q50=q("rs300", 50), rl300_q50=q("rl300", 50), rs900_q50=q("rs900", 50), rl900_q50=q("rl900", 50),
         rl300_q90=q("rl300", 90))
U["Vb"] = float(round(np.nanmedian(A["vol1m"]) / 100.0) * 100)
rv, rr = [], []
for s in sorted(E):
    seg = E[s]; f = F[s]
    ci, dd, vv = L.velas_volumen(seg, f, U["Vb"]); m = f["valido"][ci]; rv.append(np.abs(L.cociente(dd.copy(), vv))[m])
    ci, dr, dd, vv = L.velas_rango(seg, f); m = f["valido"][ci]; rr.append(np.abs(L.cociente(dd.copy(), vv))[m])
U["VB_r_q90"] = float(np.percentile(np.concatenate(rv), 90)); U["RB_r_q50"] = float(np.percentile(np.concatenate(rr), 50))
U["_nota"] = "percentiles de |rasgo| en las 12 sesiones de exploracion, rueda desde 13:32 UTC; congelado antes de mirar resultados"
json.dump(U, open(L.UMBRALES, "w", encoding="utf-8"), indent=1)
for k, v in U.items(): print("%-12s %s" % (k, v if isinstance(v, str) else round(v, 5)))
print("velas por volumen en exploracion:", sum(len(x) for x in rv), "| velas por rango:", sum(len(x) for x in rr))
# conteo de disparos por variante (SIN resultados)
cnt = {v: [] for v in L.VARIANTES}; largos = {v: 0 for v in L.VARIANTES}
for s in sorted(E):
    dis = L.disparos(E[s], F[s], U)
    for v in L.VARIANTES: cnt[v].append(len(dis[v][0])); largos[v] += int((np.asarray(dis[v][1]) > 0).sum())
print("\nDISPAROS por variante en exploracion (12 sesiones), sin mirar resultados:")
for v in L.VARIANTES: print("  %-9s total %5d | por dia min %3d max %3d | %% largos %.0f" % (v, sum(cnt[v]), min(cnt[v]), max(cnt[v]), 100.0 * largos[v] / max(1, sum(cnt[v]))))
print("tiempo %.1f s" % (time.time() - t0))
