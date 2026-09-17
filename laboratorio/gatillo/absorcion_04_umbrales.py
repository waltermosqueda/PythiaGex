# -*- coding: utf-8 -*-
"""absorcion_04_umbrales.py — SOLO distribuciones de los rasgos en las 12 sesiones de EXPLORAR (no mira ningun resultado): de aca salen
los umbrales que se congelan en el pre-registro."""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd, base as B, absorcion_base as A
T = A.tablas(); E = B.explorar(T)
print("sesiones explorar:", list(E))
# --- plantadas: cuantas cruzan cada escalon por sesion (rueda), segun reposiciones
C = pd.concat([pd.read_parquet(os.path.join(A.MI_CACHE, "plantadas-cruces-%s.parquet" % s)) for s in E], ignore_index=True)
C["tt"] = pd.to_datetime(C["t"], unit="s"); hm = C["tt"].dt.strftime("%H:%M"); C = C[(hm >= "13:32") & (hm < "20:00")]
print("\ncruces por sesion (media de 12), ambos lados, por escalon y reposiciones minimas R al cruzar:")
for e in (10, 15, 20, 30, 40, 60, 80, 120):
    c = C[C["escalon"] == e]
    print("  S>=%3d: todas %6.1f | R>=1 %6.1f | R>=2 %6.1f | R>=3 %6.1f | S>=3D %6.1f | R>=2 y S>=3D %6.1f | edad mediana al cruzar %.1f s" % (
        e, len(c) / 12, (c["R"] >= 1).sum() / 12, (c["R"] >= 2).sum() / 12, (c["R"] >= 3).sum() / 12, (c["S"] >= 3 * c["D"]).sum() / 12,
        ((c["R"] >= 2) & (c["S"] >= 3 * c["D"])).sum() / 12, (c["t"] - c["t0"]).median()))
F = pd.concat([pd.read_parquet(os.path.join(A.MI_CACHE, "plantadas-finales-%s.parquet" % s)) for s in E], ignore_index=True)
F["tt"] = pd.to_datetime(F["tfin"], unit="s"); hm = F["tt"].dt.strftime("%H:%M"); F = F[(hm >= "13:32") & (hm < "20:00")]
print("\nplantadas terminadas:", len(F), "| S cuantiles 50/90/99/99.9:", F["S"].quantile([.5, .9, .99, .999]).to_dict(), "| como terminan (0 pasiva, 1 por orden, 2 tiempo):", F["como"].value_counts(normalize=True).round(3).to_dict())
for e in (20, 30, 40, 60):
    g = F[F["S"] >= e]; print("  S>=%d: %.1f por sesion | rotas (0/1) %.1f %% | R mediana %.0f | duracion mediana %.1f s | rotas dentro de 60 s del ultimo golpe: %.1f por sesion" % (
        e, len(g) / 12, 100 * (g["como"] < 2).mean(), g["R"].median(), (g["tlast"] - g["t0"]).median(), ((g["como"] < 2) & (g["tfin"] - g["tlast"] <= 60)).sum() / 12))
# --- tabla de 1 s: umbrales por percentil en la rueda valida
P = []
for s, seg in E.items():
    v = A.valido_de(s, seg); f = pd.DataFrame(index=seg.index)
    for w in (10, 30, 60):
        f["AB%d" % w] = seg["abs_bid"].rolling(w, min_periods=1).sum(); f["AA%d" % w] = seg["abs_ask"].rolling(w, min_periods=1).sum()
    f["RB60"] = seg["rompe_bid"].rolling(60, min_periods=1).sum(); f["RA60"] = seg["rompe_ask"].rolling(60, min_periods=1).sum()
    f["OP30"] = seg["ofi_pas"].rolling(30, min_periods=1).sum(); f["D60"] = seg["delta"].rolling(60, min_periods=1).sum()
    f["dP60"] = seg["ultimo"] - seg["ultimo"].shift(60)
    P.append(f[v])
P = pd.concat(P); print("\nsegundos validos de rueda en explorar:", len(P))
for c in ("AB10", "AA10", "AB30", "AA30", "AB60", "AA60"): print(c, "p90/p95/p99/p99.5:", np.percentile(P[c], [90, 95, 99, 99.5]))
N60 = P["AB60"] - P["AA60"]; print("|AB60-AA60| p95/p99/p99.5:", np.percentile(N60.abs(), [95, 99, 99.5]))
rb = P["AB60"] / (P["RB60"] + 1); ra = P["AA60"] / (P["RA60"] + 1)
print("AB60/(RB60+1) p95/p99:", np.percentile(rb, [95, 99]).round(4), " AA60/(RA60+1):", np.percentile(ra, [95, 99]).round(4), "| RB60 mediana", P["RB60"].median(), "RA60 mediana", P["RA60"].median())
print("OP30 p25/p50/p75:", np.percentile(P["OP30"], [25, 50, 75]), "| D60 p10/p90:", np.percentile(P["D60"], [10, 90]), "| dP60 p25/p75:", np.percentile(P["dP60"].dropna(), [25, 75]))
