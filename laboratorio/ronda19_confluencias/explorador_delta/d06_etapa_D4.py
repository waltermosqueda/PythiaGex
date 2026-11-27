# -*- coding: utf-8 -*-
"""Explorador DELTA: etapa D4 (pre-registrada 04:10 AR): perfil de la distancia al VWAP y formulas 'responsivo sin rafaga'."""
import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import d_lib as L
df = L.cargar()
H = (df.seg_rueda >= 1800) & (df.seg_rueda < 21600)
vwR = df.vwap_rth_dist_pts / df.R
res = {}
print("=== D4.1 perfil de la distancia al VWAP (en R; negativa = hacia el valor) ===")
for nom, m, txt in [("V_le_m3", vwR <= -3, "<= -3R"), ("V_m3_m2", (vwR > -3) & (vwR <= -2), "(-3R,-2R]"), ("V_m2_m1", (vwR > -2) & (vwR <= -1), "(-2R,-1R]"),
                    ("V_m1_0", (vwR > -1) & (vwR <= 0), "(-1R,0]"), ("V_gt0", vwR > 0, "> 0 (lejos del valor)")]:
    out = L.correr(df, H & m, nom, "distancia al VWAP RTH %s; 10:00-15:30; una posicion por vez; max 5" % txt, salidas=("S1", "S6"), nota="ablacion de contexto")
    for sal, (r, ops) in out.items():
        print(L.linea(nom, sal, r)); res[nom + "-" + sal] = r
print("\n=== D4.2 responsivo sin rafaga ===")
d60 = df.d60_ratio >= 0.10; imb0 = df.imb_neto_3v <= 0; F3 = df.F3 == 1
R = [("r1_V2_d60", H & (vwR <= -2) & d60, "VWAP a >=2R hacia el valor + d60_ratio >= 0,10"),
     ("r2_V2_imb0", H & (vwR <= -2) & imb0, "VWAP >=2R + imb_neto_3v <= 0"),
     ("r3_V2_d60_imb0", H & (vwR <= -2) & d60 & imb0, "VWAP >=2R + d60 >= 0,10 + imb <= 0"),
     ("r4_V1_d60_imb0", H & (vwR <= -1) & d60 & imb0, "VWAP >=1R + d60 >= 0,10 + imb <= 0"),
     ("r5_V1_d60_imb0_noF3", H & (vwR <= -1) & d60 & imb0 & ~F3, "VWAP >=1R + d60 >= 0,10 + imb <= 0 + no F3"),
     ("r6_V1_big_imb0", H & (vwR <= -1) & (df.of_netbig60_a_favor > 0) & imb0, "VWAP >=1R + big netos a favor > 0 + imb <= 0"),
     ("r7_V1_d60_sinniv", H & (vwR <= -1) & d60 & (df.n_niveles_en_zona == 0), "VWAP >=1R + d60 >= 0,10 + sin niveles en zona"),
     ("r8_V2_d60_imb0_noF3", H & (vwR <= -2) & d60 & imb0 & ~F3, "VWAP >=2R + d60 >= 0,10 + imb <= 0 + no F3")]
for nom, m, par in R:
    out = L.correr(df, m, nom, par + "; 10:00-15:30; una posicion por vez; max 5", salidas=("S1", "S6", "S2", "S4b"))
    for sal, (r, ops) in out.items():
        print(L.linea(nom, sal, r)); res[nom + "-" + sal] = r
json.dump(res, open(os.path.join(L.AQUI, "res_D4.json"), "w"), indent=1, default=str)
