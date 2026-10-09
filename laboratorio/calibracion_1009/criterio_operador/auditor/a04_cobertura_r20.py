# -*- coding: utf-8 -*-
"""a04_cobertura_r20.py — AUDITOR. SOLO LECTURA de r20/r20_noches.json. Rehace el pareado 'real - corridas' de COBERTURA de r20 con
las columnas por noche (giros, giros_cubiertos, corridas_cobertura_pct), porque r20_noches.py divide dos veces por len(corr)
(linea 104: cor_cub += ok/len(corr); linea 156: Cco = cor_cub/len(corr)). Correr: python -I a04_cobertura_r20.py"""
import json, os
import numpy as np
AQUI = os.path.dirname(os.path.abspath(__file__))
D = json.load(open(os.path.join(os.path.dirname(AQUI), "r20", "r20_noches.json"), encoding="utf-8"))
for o in D:
    pn = o.get("por_noche") or []
    if not pn:
        continue
    g = np.array([f["giros"] or 0 for f in pn], float); cr = np.array([f["giros_cubiertos"] or 0 for f in pn], float)
    cc = np.array([(f["corridas_cobertura_pct"] or 0) * (f["giros"] or 0) / 100 for f in pn], float)
    noches = [f["noche"] for f in pn]
    def dif(w):
        return 100 * (w @ cr) / (w @ g) - 100 * (w @ cc) / (w @ g)
    rng = np.random.default_rng(1)
    v = np.array([dif(np.bincount(rng.integers(0, len(g), len(g)), minlength=len(g)).astype(float)) for _ in range(4000)])
    print("%-10s %-10s real %.2f %% corridas %.2f %% -> dif %+.2f  IC90 %+.2f a %+.2f  (r20 informo el pareado con corridas/6: %.2f %%)" % (
        o["conjunto"], o["ventana"], 100 * cr.sum() / g.sum(), 100 * cc.sum() / g.sum(), dif(np.ones(len(g))),
        np.percentile(v, 5), np.percentile(v, 95), 100 * cc.sum() / g.sum() / 6))
    m = np.array([n != "2026-10-09" for n in noches], float)
    print("           sin 10-09: dif %+.2f" % dif(m))
