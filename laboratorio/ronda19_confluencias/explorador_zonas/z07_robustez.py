# -*- coding: utf-8 -*-
"""ZONAS, paso 7: robustez de la banda de 2 sd. (1) bordes de la ventana con y sin F2; (2) banda RTH sola / jornada sola;
(3) B2z + F2 + un filtro mas; (4) familia d con la banda (barrida de la banda que vuelve, con F1 / F2). Todo se registra."""
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import zonas_comun as Z

df = Z.cargar()
F2 = (df.F2 == 1).to_numpy(); F1 = (df.F1 == 1).to_numpy()
iman = Z.contacto(df, "estaticos_sin_r50", -0.5, -1e-6) > 0
est_barr = Z.contacto(df, "estaticos_sin_r50", 1e-6, 0.5, vuelve=True) > 0
SAL = ("S6", "S1")
FA = "a (banda 2 sd, reversion)"

print("== (1) bordes de la ventana (pen en X respecto de la banda 2 sd RTH o jornada del lado opuesto)")
VENT = {"[-T,+T]": (None, "T"), "[-T,X/8]": (None, 0.125), "(-X/4,X/8]": (-0.25, 0.125), "(-X/2,0]": (-0.5, 0.0),
        "(-X/2,X/4]": (-0.5, 0.25), "(-3X/4,X/8]": (-0.75, 0.125)}
for vn, (lo, hi) in VENT.items():
    if lo is None and hi == "T":
        T = df["zona_T"].to_numpy(); X = df["X"].to_numpy()
        m = np.zeros(len(df), bool)
        for s_, noms in Z.GRUPOS_LADO["bandas_rev_2sd"].items():
            for nom in noms:
                pen = -df["d_" + nom].to_numpy() * df["lado"].to_numpy()
                m |= (df["lado"].to_numpy() == s_) & np.isfinite(pen) & (pen >= -T - 1e-9) & (pen <= T + 1e-9)
    elif lo is None:
        m = Z.contacto(df, "bandas_rev_2sd", 0, hi, lo_T=True) > 0
    else:
        m = Z.contacto(df, "bandas_rev_2sd", lo + 1e-6, hi) > 0
    Z.evaluar("Z7_B2_%s" % vn, FA, "extremo en %s de la banda 2 sd (RTH o jornada) del lado opuesto" % vn, m, SAL, nota="robustez de la ventana")
    Z.evaluar("Z7_B2_%s+F2" % vn, FA, "extremo en %s de la banda 2 sd (RTH o jornada) del lado opuesto + F2 iceberg" % vn, m & F2, SAL, nota="robustez de la ventana")

B2z = Z.contacto(df, "bandas_rev_2sd", -0.5, 0.125) > 0
print("== (2) de que VWAP sale la banda (zona -X/2..+X/8) + F2")
rth = Z.contacto(df, {1: ["vwr-2"], -1: ["vwr+2"]}, -0.5, 0.125) > 0
jor = Z.contacto(df, {1: ["vwj-2"], -1: ["vwj+2"]}, -0.5, 0.125) > 0
Z.evaluar("Z7_B2z_soloRTH+F2", FA, "zona -X/2..+X/8 de la banda 2 sd RTH (vwr-2 / vwr+2) + F2", rth & F2, SAL)
Z.evaluar("Z7_B2z_soloJornada+F2", FA, "zona -X/2..+X/8 de la banda 2 sd de la jornada (vwj-2 / vwj+2) + F2", jor & F2, SAL)
Z.evaluar("Z7_B2z_ambas+F2", FA, "zona -X/2..+X/8 de las DOS bandas 2 sd (RTH y jornada) + F2", rth & jor & F2, SAL)

print("== (3) B2z + F2 + un filtro mas")
base = B2z & F2
COND = {
    "vwap_a>=1.5R": df.dist_vwap_R >= 1.5, "sin_iman_estatico": ~iman, "estatico_barrido": est_barr, "contra_marea": df.a_favor_marea == 0,
    "h10-12": df.hora < 12, "h12-15:30": df.hora >= 12, "ruido_en_contra": df.ctx_ruido == "fuera_en_contra", "delta300<0": df.of_delta_300 < 0,
    "sin_veto": df.veto_caudal == 0, "va_y_vuelve": df.regimen_va_y_vuelve == 1, "prof=0": df.ctx_prof_X == 0, "vel_ret<p75": df.ctx_vel_ret < 0.1,
    "F1": df.F1 == 1, "F6": df.F6 == 1, "ice_favor>contra": df.of_icebergs_60 > df.of_icebergs_contra_60,
}
for k, c in COND.items():
    Z.evaluar("Z7_B2z+F2+" + k, FA, "zona -X/2..+X/8 de la banda 2 sd (RTH o jornada) del lado opuesto + F2 + " + k, base & np.asarray(c, bool), SAL)

print("== (4) familia d con la banda: barrida de la banda 2 sd que vuelve")
B2d = Z.contacto(df, "bandas_rev_2sd", 1e-6, 1.0, vuelve=True) > 0
Z.evaluar("Z7_B2d", "d (barrida de la banda 2 sd)", "extremo perforo la banda 2 sd del lado opuesto 0..X y la entrada volvio adentro", B2d, SAL)
Z.evaluar("Z7_B2d+F1", "d (barrida de la banda 2 sd)", "barrida 0..X de la banda 2 sd que vuelve + F1 absorcion", B2d & F1, SAL)
Z.evaluar("Z7_B2d+F2", "d (barrida de la banda 2 sd)", "barrida 0..X de la banda 2 sd que vuelve + F2 iceberg", B2d & F2, SAL)
Z.evaluar("Z7_B2d+F1oF2", "d (barrida de la banda 2 sd)", "barrida 0..X de la banda 2 sd que vuelve + (F1 o F2)", B2d & (F1 | F2), SAL)
