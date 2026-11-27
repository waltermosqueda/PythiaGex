# -*- coding: utf-8 -*-
"""ZONAS, paso 8: familia d con los extremos estaticos clasicos (minimo/maximo de ayer, Asia, Londres, balance inicial).
Barrida (perfora y la entrada vuelve adentro) con absorcion / iceberg y con el contexto de reversion. Todo se registra."""
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import zonas_comun as Z

df = Z.cargar()
F1 = (df.F1 == 1).to_numpy(); F2 = (df.F2 == 1).to_numpy()
est = (df.ext_vwap_sd_firmado <= -1.5).to_numpy()
hv = (df.dist_vwap_R >= 1.5).to_numpy()
FD = "d (barrida de extremo estatico)"
for g in ("extremos_naturales", "extremos_inversion", "est_naturales"):
    for vn, lo, hi in (("(0,X/2]", 1e-6, 0.5), ("(X/2,X]", 0.5 + 1e-6, 1.0)):
        b = Z.contacto(df, g, lo, hi, vuelve=True) > 0
        Z.evaluar("Z8_%s_%s+F1" % (g, vn), FD, "barrida %s de %s que vuelve + F1 absorcion" % (vn, g), b & F1, ("S6", "S1"))
        Z.evaluar("Z8_%s_%s+F1oF2" % (g, vn), FD, "barrida %s de %s que vuelve + (F1 o F2)" % (vn, g), b & (F1 | F2), ("S6", "S1"))
b = Z.contacto(df, "extremos_naturales", 1e-6, 1.0, vuelve=True) > 0
Z.evaluar("Z8_extnat_(0,X]+F1oF2+estirado1.5", FD, "barrida 0..X de extremo natural que vuelve + (F1 o F2) + extremo a >= 1,5 sd del VWAP", b & (F1 | F2) & est, ("S6", "S1"))
Z.evaluar("Z8_extnat_(0,X]+F1oF2+vwap>=1.5R", FD, "barrida 0..X de extremo natural que vuelve + (F1 o F2) + VWAP a >= 1,5R a favor", b & (F1 | F2) & hv, ("S6", "S1"))
