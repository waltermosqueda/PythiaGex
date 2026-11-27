# -*- coding: utf-8 -*-
"""RONDA 26, paso 10 (DESCRIPTIVO, agregado DESPUES del pre-registro; no entra en ninguna decision): QUIEN SE MUEVE PRIMERO, ES o MES.
Por segundo RTH (9:30-16:00 NY), con la cinta de cada uno: variacion del ultimo precio del segundo (relleno hacia adelante) y delta del segundo.
  - correlacion de la variacion del precio del ES en t con la del MES en t+k (k = -2..+2 s): si el ES lidera, k = +1 pesa mas que k = -1;
  - correlacion de la variacion del precio del ES en t con el DELTA del MES en t+k, y al reves (precio del MES con el delta del ES):
    si el delta del MES llega DETRAS del precio del ES (k > 0), parte de la agresion del MES es copia del movimiento del ES.
Mediana de las ruedas y rango.
Uso: python -I r26_10_quien_lidera.py <tanda>"""
import gc
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import r26_comun as C

C.prioridad_baja()
K = [-2, -1, 0, 1, 2]


def por_segundo(inst, dia):
    d, _, _ = C.leer(C.archivo(inst, dia))
    r0, r1 = C.rth(dia)
    d = d[(d["ms"] >= r0) & (d["ms"] < r1)]
    s = ((d["ms"].to_numpy() - r0) // 1000).astype(np.int64)
    delta = np.bincount(s, weights=(d["vol"] * d["lado"]).to_numpy(), minlength=23400)[:23400]
    p = np.full(23400, np.nan)
    rev = s[::-1]
    u, i = np.unique(rev, return_index=True)
    p[u] = d["ultimo"].to_numpy()[::-1][i]
    # relleno hacia adelante
    ok = ~np.isnan(p)
    idx = np.where(ok, np.arange(23400), 0)
    np.maximum.accumulate(idx, out=idx)
    p = p[idx]
    del d
    gc.collect()
    return np.diff(p), delta[1:]


def corr_k(a, b, k):
    if k > 0:
        x, y = a[:-k], b[k:]
    elif k < 0:
        x, y = a[-k:], b[:k]
    else:
        x, y = a, b
    m = np.isfinite(x) & np.isfinite(y)
    return float(np.corrcoef(x[m], y[m])[0, 1])


def main():
    tanda = sys.argv[1]
    pares = json.load(open(os.path.join(C.DATOS, "pares.json")))
    dias = sorted(d for d, v in pares.items() if v["tanda"] == tanda and v["alinea"])
    print("RONDA 26 - quien se mueve primero (descriptivo, post hoc), %s: %d ruedas" % (tanda, len(dias)))
    R = {"pp": [], "pE_dM": [], "pM_dE": [], "pE_dE": [], "pM_dM": []}
    for dia in dias:
        C.frenar_si_falta_ram()
        dpE, dE = por_segundo("ES", dia)
        dpM, dM = por_segundo("MES", dia)
        R["pp"].append([corr_k(dpE, dpM, k) for k in K])
        R["pE_dM"].append([corr_k(dpE, dM, k) for k in K])
        R["pM_dE"].append([corr_k(dpM, dE, k) for k in K])
        R["pE_dE"].append([corr_k(dpE, dE, k) for k in K])
        R["pM_dM"].append([corr_k(dpM, dM, k) for k in K])
    nombres = {"pp": "precio ES (t) con precio MES (t+k)", "pE_dM": "precio ES (t) con DELTA MES (t+k)", "pM_dE": "precio MES (t) con DELTA ES (t+k)",
               "pE_dE": "precio ES (t) con DELTA ES (t+k)", "pM_dM": "precio MES (t) con DELTA MES (t+k)"}
    print("   correlacion por segundo, mediana de las ruedas [min; max];  k =  " + "   ".join("%+d s" % k for k in K))
    for c in R:
        v = np.array(R[c])
        print("   %-36s " % nombres[c] + "  ".join("%+.3f [%+.2f;%+.2f]" % (np.median(v[:, j]), v[:, j].min(), v[:, j].max()) for j in range(len(K))))
    a, b = np.array(R["pE_dM"])[:, K.index(1)], np.array(R["pE_dE"])[:, K.index(1)]
    print("   PREDICCION (b): precio ES (t) con delta MES (t+1 s) > precio ES (t) con delta ES (t+1 s) en %d de %d ruedas (%.0f %%): %s" %
          ((a > b).sum(), len(a), 100 * (a > b).mean(), "SE CUMPLE (>= 80 %)" if (a > b).mean() >= 0.8 else "NO se cumple"))
    print("   por rueda: " + " | ".join("%s %+.3f vs %+.3f" % (d[5:], x, y) for d, x, y in zip(dias, a, b)))


if __name__ == "__main__":
    main()
