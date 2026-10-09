# -*- coding: utf-8 -*-
"""calibrar_tipo1.py (soportes_metodo, 09-10-2026): tasa de falsos positivos del juez de mechas (jitter) con rayas NO derivadas del precio
(6 rayas fijas al azar por sesion) sobre un paseo al azar: 200 repeticiones x 20 sesiones de 405 velas m2, 200 sorteos de jitter.
Si el juez esta bien calibrado, ~5 % de las repeticiones da p < 0,05. Salida: calibrar_tipo1.json"""
import os, sys, json, math
AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI); sys.path.insert(0, os.path.normpath(os.path.join(AQUI, "..", "..")))
import numpy as np
import juez_mechas as JM
JM.M = 200
rng = np.random.default_rng(7)
res = {u: [] for u in JM.UNIDADES}
for rep in range(200):
    acc = {u: {} for u in JM.UNIDADES}
    for s in range(20):
        n, seg = 405, 120
        x = 30000.0 + np.cumsum(rng.normal(0.0, 8.744 / math.sqrt(seg), n * seg))
        x = (np.round(x / 0.25) * 0.25).reshape(n, seg)
        o, h, l, c = x[:, 0], x.max(1), x.min(1), x[:, -1]
        Ls = [np.full(n, v) for v in rng.uniform(l.min(), h.max(), 6)]
        G = JM.giros_causal(o, h, l, c, 20.0)
        d = rng.uniform(JM.J1, JM.J2, JM.M) * rng.choice([-1.0, 1.0], JM.M)
        for u in JM.UNIDADES:
            r = JM.medir(h, l, Ls, G, np.ones(n, bool), d, u)
            if r:
                JM.acumular(acc[u], "azar", r[0], r[1], s)
    for u in JM.UNIDADES:
        t = JM.resumir(acc[u], minimo=1)
        if t:
            res[u].append(t[0]["p_jitter"])
out = {u: dict(repeticiones=len(p), rechazo_5pct=round(float(np.mean(np.array(p) < 0.05)), 3), rechazo_10pct=round(float(np.mean(np.array(p) < 0.10)), 3),
               p_mediana=round(float(np.median(p)), 3)) for u, p in res.items()}
json.dump(out, open(os.path.join(AQUI, "calibrar_tipo1.json"), "w", encoding="utf-8"), indent=1)
print(out)
