# -*- coding: utf-8 -*-
"""EXTRA (FUERA DEL PRE-REGISTRO, descriptivo, solo ENTRENAMIENTO): los giros de 20 y 40 pts (zigzag del juez + filtro de sanidad del
arnes), ¿caen a +-2 de un multiplo de 25 / 50 / 100 mas seguido que lo que daria una grilla uniforme (17 posiciones de 0,25 sobre 4G)?
Motivo: la cobertura contra el azar 'pool' y contra los corridos queda sesgada para rayas de grilla (ver resumen); esta es la vara limpia."""
import json, os, sys
import numpy as np
from math import comb
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI)
import candidatas  # noqa: agrega datos/ y arnes/ al path
import evaluar as EV, juez_operador_v1 as JO
from correr import ENTRENAMIENTO

def binom_sf(k, n, p):
    # P(X >= k) exacta con logs
    from math import lgamma, log, exp
    s = 0.0
    for x in range(k, n + 1):
        s += exp(lgamma(n + 1) - lgamma(x + 1) - lgamma(n - x + 1) + x * log(p) + (n - x) * log(1 - p))
    return s

v = EV.velas_sesiones(ENTRENAMIENTO)
op = JO._op(dict(EV.OPCIONES_JUEZ))
V = JO.preparar_velas(v, op)
out = {"sesiones": len(ENTRENAMIENTO)}
for th in (20.0, 40.0):
    o2 = dict(op); o2["umbral_giro_cobertura"] = th
    pv, desc = EV._pivotes_sanos(V, JO._pivotes(V, o2))
    w = EV._cod_ventana_min(np.array([V["tm"][p["k"]] for p in pv], np.int64))
    pr = np.array([p["precio"] for p in pv])
    for wn, wc in (("noche", 0), ("dia", 1)):
        x = pr[w == wc]
        for G in (25, 50, 100):
            r = np.mod(x, G); d = np.minimum(r, G - r)
            k = int((d <= 2.0 + 1e-9).sum()); n = len(x); p0 = 17.0 / (4 * G)
            out["giros%d/%s/G%d" % (th, wn, G)] = {"giros": n, "a_2_de_multiplo": k, "pct": 100.0 * k / n if n else None,
                                                   "uniforme_pct": 100 * p0, "p_binomial_mas": binom_sf(k, n, p0) if n else None}
print(json.dumps(out, indent=1))
with open(os.path.join(AQUI, "extra_redondez_giros.json"), "w", encoding="utf-8") as f:
    json.dump(out, f, indent=1)
