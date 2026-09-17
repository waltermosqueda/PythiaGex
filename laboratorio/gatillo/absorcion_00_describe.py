# -*- coding: utf-8 -*-
"""absorcion_00_describe.py — SOLO distribucion de los rasgos (sin mirar resultados) en las sesiones de EXPLORAR, para fijar umbrales del pre-registro."""
import sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B
T = B.todas_las_sesiones(); E = B.explorar(T)
print("sesiones:", list(T.keys())); print("explorar:", len(E), "confirmar:", len(B.confirmar(T)))
for s, d in E.items():
    r = d[d["rueda"]]
    v = r["vol"].sum()
    print(s, "filas", len(d), "rueda seg", len(r), "vol", int(v), "abs_bid %.3f%%" % (100*r["abs_bid"].sum()/v), "abs_ask %.3f%%" % (100*r["abs_ask"].sum()/v),
          "rompe_bid %.3f%%" % (100*r["rompe_bid"].sum()/v), "rompe_ask %.3f%%" % (100*r["rompe_ask"].sum()/v),
          "seg con abs_bid>0: %d" % int((r["abs_bid"]>0).sum()), "seg con rompe_bid>0: %d" % int((r["rompe_bid"]>0).sum()))
R = pd.concat([d[d["rueda"]] for d in E.values()])
for c in ("abs_bid", "abs_ask", "rompe_bid", "rompe_ask"):
    x = R[c][R[c] > 0]
    print(c, "n seg>0", len(x), "cuantiles 50/75/90/95/99/99.9:", np.percentile(x, [50, 75, 90, 95, 99, 99.9]).round(1))
# sumas moviles por sesion
for w in (10, 30, 60):
    parts = []
    for s, d in E.items():
        a = d["abs_bid"].rolling(w, min_periods=1).sum(); b = d["abs_ask"].rolling(w, min_periods=1).sum()
        parts.append(pd.DataFrame({"ab": a[d["rueda"]], "aa": b[d["rueda"]]}))
    P = pd.concat(parts)
    print("ventana", w, "abs_bid suma cuantiles 90/95/99/99.5/99.9:", np.percentile(P["ab"], [90, 95, 99, 99.5, 99.9]).round(1), "| abs_ask:", np.percentile(P["aa"], [90, 95, 99, 99.5, 99.9]).round(1))
    # conteo de segundos con absorcion dentro de la ventana
    parts = []
    for s, d in E.items():
        a = (d["abs_bid"] > 0).rolling(w, min_periods=1).sum(); parts.append(a[d["rueda"]])
    P = pd.concat(parts); print("   segundos con abs_bid>0 en ventana: cuantiles 90/99/99.9:", np.percentile(P, [90, 99, 99.9]))
print("spread medio (ask-bid) rueda:", (R["ask"] - R["bid"]).describe().round(3).to_dict())
print("bidv/askv medianos:", R["bidv"].median(), R["askv"].median(), "p90:", R["bidv"].quantile(.9), R["askv"].quantile(.9))
