# -*- coding: utf-8 -*-
"""a06_nacimientos.py — AUDITOR. SOLO LECTURA. Cuantas llegadas de la 2.0 (REF_2_0, congelada, TOLERANTE) ocurren en las primeras
velas de vida de su pista (la raya 'aparece' encima del precio: llegada por nacimiento, no porque el precio fue a ella), y como les va.
Correr: python -I a06_nacimientos.py"""
import os, sys
import numpy as np, pandas as pd
AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(AQUI), "juez")); sys.path.insert(0, AQUI)
import juez_operador as J  # noqa: E402
from a02_placebo_sin_noches import ventanas, CAL  # noqa: E402
V = pd.read_csv(os.path.join(CAL, "velas_m1.csv"), parse_dates=["t"])
niv = pd.read_pickle(os.path.join(CAL, "niv20_m1.pkl"))
R = J.rayas_desde_niv20(niv, ("dom0", "dom1", "ndx_dom0", "ndx_dom1"))
op = {"ventanas": ventanas()}
Vp, Rp = J.preparar(V, R, op)
ev = J.juzgar(V, R, op, _prep=(Vp, Rp))["eventos"]
print("pistas %d" % len(Rp["pistas"]))
for lim in (0, 2, 5, 15):
    sub = [e for e in ev if e["i"] - Rp["pistas"][e["pista"]]["idx"][0] <= lim]
    b = sum(not e["censurada"] for e in sub); s = sum(e["resultado"] == "sostenido" for e in sub)
    print("llegadas a <= %2d velas del nacimiento de la pista: %3d de %d; sostenidas %d/%d" % (lim, len(sub), len(ev), s, b))
rest = [e for e in ev if e["i"] - Rp["pistas"][e["pista"]]["idx"][0] > 5]
b = sum(not e["censurada"] for e in rest); s = sum(e["resultado"] == "sostenido" for e in rest)
print("sin las de <= 5 velas del nacimiento: %d llegadas, %.1f %% sostenidas" % (len(rest), 100 * s / b))
# cambios de strike en la ventana congelada
cambios = 0; prev = None
for i in np.flatnonzero(Vp["en_v"]):
    a, bb = Rp["pv_ini"][i], Rp["pv_ini"][i + 1]
    cur = tuple(sorted(Rp["pv_pid"][a:bb].tolist()))
    if prev is not None and cur != prev:
        cambios += 1
    prev = cur
print("cambios del juego de pistas vigentes entre velas consecutivas en la ventana: %d (en %d velas)" % (cambios, int(Vp["en_v"].sum())))
