# -*- coding: utf-8 -*-
"""absorcion_09_velocidad.py — DESCRIPTIVO: ¿el racimo de absorcion dice algo del COMPORTAMIENTO (rapido / lento) aunque no diga direccion?
A igual volumen de los ultimos 30 s, se compara el movimiento absoluto que viene (60 s) y los segundos hasta tocar +-8 con y sin absorcion.
Y a que hora caen los disparos R30."""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd, base as B, absorcion_base as A, absorcion_variantes as V
T = A.tablas()
for rot, S in (("explorar", B.explorar(T)), ("confirmar", B.confirmar(T))):
    P = []
    for s, seg in S.items():
        f = A.rasgos(s, seg); v = A.valido_de(s, seg); p = seg["ultimo"].to_numpy(); N = len(p); idx = np.flatnonzero(v)[::5]   # 1 de cada 5 s
        _, th = B.barrera(seg, 8, 600); vol30 = seg["vol"].rolling(30, min_periods=1).sum().to_numpy()
        P.append(pd.DataFrame({"vol30": vol30[idx], "abs30": (f["AB30"] + f["AA30"]).to_numpy()[idx], "mov60": np.abs(p[np.clip(idx + 60, 0, N - 1)] - p[idx]), "toca": th[idx]}))
    P = pd.concat(P); P["toca"] = P["toca"].replace(np.inf, 600); P["q"] = pd.qcut(P["vol30"], 10, labels=False, duplicates="drop")
    P["share"] = P["abs30"] / P["vol30"].clip(lower=1)
    alto = P.groupby("q")["share"].transform(lambda x: x >= x.quantile(0.8)); bajo = P.groupby("q")["share"].transform(lambda x: x <= x.quantile(0.2))
    print("---", rot, "| n =", len(P))
    print("  corr(vol30, |mov 60 s|) = %.3f | corr(abs30, |mov 60 s|) = %.3f | corr(abs30/vol30, |mov 60 s|) = %.3f" % (P["vol30"].corr(P["mov60"]), P["abs30"].corr(P["mov60"]), P["share"].corr(P["mov60"])))
    g = pd.DataFrame({"mov60 mucha abs": P[alto].groupby("q")["mov60"].mean(), "mov60 poca abs": P[bajo].groupby("q")["mov60"].mean(), "toca mucha": P[alto].groupby("q")["toca"].median(), "toca poca": P[bajo].groupby("q")["toca"].median()})
    print(g.round(2).to_string())
    f = A.disparos(S, V.R30); h = f["t"].dt.strftime("%H").value_counts().sort_index(); print("  disparos R30 por hora UTC:", h.to_dict())
