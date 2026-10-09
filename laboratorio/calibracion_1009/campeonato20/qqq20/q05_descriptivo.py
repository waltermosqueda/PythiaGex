# -*- coding: utf-8 -*-
"""q05_descriptivo.py — descriptivos de las rayas (no juzga nada): distancia R20 (2.0 tal cual) vs DOMS_QQQ_vol (misma seleccion, razon C8)
y DOMS_QQQ_esc vs DOMS_QQQ_vol, de noche (22:00Z-13:30Z), por sesion. SOLO LECTURA. Uso: python -I q05_descriptivo.py"""
import ctypes
ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
import os
import numpy as np, pandas as pd
AQUI = os.path.dirname(os.path.abspath(__file__))
Q = pd.read_pickle(os.path.join(AQUI, "datos", "rayas_qqq20.pkl"))
S = Q["series"]
def noche(t):
    h = t.hour + t.minute / 60
    return not (13.5 <= h < 20)
filas = []
for t, d in S["DOMS_QQQ_vol"].items():
    if not d or not noche(t):
        continue
    s = (t + pd.Timedelta(hours=2)).strftime("%Y-%m-%d")
    a = sorted(v[0] for v in d.values())
    e = sorted(v[0] for v in (S["DOMS_QQQ_esc"].get(t) or {}).values())
    r = sorted(v[0] for v in (S["R20_QQQ"].get(t - pd.Timedelta(minutes=1)) or {}).values())   # R20 de la vela anterior (desfase 1)
    de = np.median([abs(x - y) for x, y in zip(a, e)]) if len(a) == len(e) and a else np.nan
    # R20 vs DOMS: diferencia mediana de la raya mas cercana
    dr = np.median([min(abs(x - y) for y in r) for x in a]) if r else np.nan
    filas.append((s, de, dr))
D = pd.DataFrame(filas, columns=["sesion", "esc_vs_doms", "r20_vs_doms"])
print(D.groupby("sesion").median().round(2).to_string())
print("TOTAL mediana: esc-doms %.2f pts; r20-doms %.2f pts (p90 %.1f)" % (D["esc_vs_doms"].median(), D["r20_vs_doms"].median(), D["r20_vs_doms"].quantile(.9)))
