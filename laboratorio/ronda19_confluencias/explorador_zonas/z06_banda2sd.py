# -*- coding: utf-8 -*-
"""ZONAS, paso 6: universos alrededor de la banda de 2 desvios del VWAP del lado opuesto (la operacion va hacia el VWAP) y
condiciones de flujo / contexto adentro. Universos:
  B2t = toque sin perforar de vwr-2/vwj-2 (largo) o vwr+2/vwj+2 (corto): pen en [-T, 0]
  B2z = zona ancha de la banda RTH o jornada: pen en [-X/2, +X/8]
  B2s = extremo entre -2,5 y -1,75 desvios del VWAP RTH (firmado)
Condiciones (de a una): marea, flujo F1/F2/F6, icebergs, veto, hora, iman estatico (nivel estatico sin tocar a <= X/2 detras del
extremo), estatico barrido en el extremo, regimen, ruido, retroceso con agresores, delta 300 s, velocidad, recorrido al VWAP.
Se simula y registra S6 y S1 de cada universo x condicion; para los universos solos, todas las salidas."""
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import zonas_comun as Z

df = Z.cargar()
X = df["X"].to_numpy()
B2t = Z.contacto(df, "bandas_rev_2sd", 0, 0.0, lo_T=True) > 0
B2z = Z.contacto(df, "bandas_rev_2sd", -0.5, 0.125) > 0
xsd = df["ext_vwap_sd_firmado"].to_numpy()
B2s = (xsd > -2.5) & (xsd <= -1.75)
iman = Z.contacto(df, "estaticos_sin_r50", -0.5, -1e-6) > 0          # nivel estatico sin tocar detras del extremo (a <= X/2)
est_barr = Z.contacto(df, "estaticos_sin_r50", 1e-6, 0.5, vuelve=True) > 0
UNIV = {"B2t": B2t, "B2z": B2z, "B2s": B2s}
PAR = {"B2t": "toque sin perforar (pen -T..0) de la banda 2 sd (RTH o jornada) del lado opuesto; la vuelta va hacia el VWAP",
       "B2z": "extremo a -X/2..+X/8 de la banda 2 sd (RTH o jornada) del lado opuesto; la vuelta va hacia el VWAP",
       "B2s": "extremo entre -2,5 y -1,75 desvios del VWAP RTH (lado opuesto)"}
COND = {
    "contra_marea": df.a_favor_marea == 0, "a_favor_marea": df.a_favor_marea == 1,
    "F1": df.F1 == 1, "F2": df.F2 == 1, "F6": df.F6 == 1, "F1oF6": (df.F1 == 1) | (df.F6 == 1), "F1oF2": (df.F1 == 1) | (df.F2 == 1),
    "ice_favor>contra": df.of_icebergs_60 > df.of_icebergs_contra_60, "sin_veto": df.veto_caudal == 0,
    "h10-12": df.hora < 12, "h12-15:30": df.hora >= 12,
    "sin_iman_estatico": ~iman, "con_iman_estatico": iman, "estatico_barrido": est_barr,
    "va_y_vuelve": df.regimen_va_y_vuelve == 1, "ruido_en_contra": df.ctx_ruido == "fuera_en_contra",
    "ret_con_agresores": df.ctx_ret_con_agresores == 1, "delta300<0": df.of_delta_300 < 0, "vel_ret<p75": df.ctx_vel_ret < 0.1,
    "vwap_a>=1.5R": df.dist_vwap_R >= 1.5, "prof=0": df.ctx_prof_X == 0,
}
COND = {k: np.asarray(v, bool) for k, v in COND.items()}
for u, mu in UNIV.items():
    print("== universo", u, "| eventos en horario:", int((mu & ((df.seg_rueda >= Z.H_INI) & (df.seg_rueda < Z.H_FIN)).to_numpy()).sum()))
    Z.evaluar("Z6_" + u, "a (banda 2 sd, reversion)", PAR[u], mu, tuple(Z.SALIDAS), nota="universo de la banda de 2 sd")
for u, mu in UNIV.items():
    print("== universo", u, "+ una condicion")
    for k, m in COND.items():
        Z.evaluar("Z6_%s+%s" % (u, k), "a (banda 2 sd, reversion)", PAR[u] + " + " + k, mu & m, ("S6", "S1"))
