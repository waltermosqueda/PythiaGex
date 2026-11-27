# -*- coding: utf-8 -*-
"""Explorador DELTA: etapa D2 (pre-registrada 03:52 AR). 1) tamizado por tercios de los rasgos de flujo; 2) formulas 'manda el pasivo'."""
import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import d_lib as L

df = L.cargar()
H = (df.seg_rueda >= 1800) & (df.seg_rueda < 21600)
VAL = df.vwap_rth_dist_pts <= -df.R
F1 = df.F1 == 1; F3 = df.F3 == 1; ICE = df.ice_neto_60 > 0
res = {}
print("=== D2.1 tamizado por tercios (cortes sin resultados) ===")
feats = ["of_abs_lado_pasivo_rel", "of_icebergs_60", "ice_neto_60", "d60_ratio", "of_delta_60", "of_delta_300", "of_netbig60_a_favor",
         "imb_neto_3v", "del_ret_por_pt", "seg_desde_ext"]
cortes = {}
for f in feats:
    q1, q2 = df.loc[H, f].quantile([1 / 3, 2 / 3]).tolist()
    cortes[f] = (q1, q2)
    for k, (m, txt) in enumerate([(df[f] <= q1, "<= %.3f" % q1), ((df[f] > q1) & (df[f] <= q2), "(%.3f, %.3f]" % (q1, q2)), (df[f] > q2, "> %.3f" % q2)]):
        nom = "T_%s_t%d" % (f, k + 1)
        out = L.correr(df, H & m, nom, "tercio %d de %s %s; 10:00-15:30; una posicion por vez; max 5" % (k + 1, f, txt), salidas=("S1", "S6"),
                       nota="tamizado por tercios")
        for sal, (r, ops) in out.items():
            print(L.linea(nom, sal, r)); res[nom + "-" + sal] = r
print("\n=== D2.2 formulas 'manda el pasivo' ===")
P = [("p1_F1_noF3", H & F1 & ~F3, "F1 absorcion + no F3"),
     ("p2_F1_valor", H & F1 & VAL, "F1 + hacia el valor (VWAP a >=1R)"),
     ("p3_F1oICE_valor", H & (F1 | ICE) & VAL, "(F1 o icebergs netos > 0) + hacia el valor"),
     ("p4_F1oICE_valor_noF3", H & (F1 | ICE) & VAL & ~F3, "(F1 o icebergs netos > 0) + hacia el valor + no F3"),
     ("p5_F1oICE_bigle0", H & (F1 | ICE) & (df.of_netbig60_a_favor <= 0), "(F1 o icebergs netos > 0) + big netos a favor <= 0"),
     ("p6_valor_d60_noF3", H & VAL & (df.d60_ratio >= 0.10) & ~F3, "hacia el valor + d60_ratio >= 0,10 + no F3"),
     ("p7_valor_d60_retagr", H & VAL & (df.d60_ratio >= 0.10) & (df.ctx_ret_con_agresores == 1), "hacia el valor + d60_ratio >= 0,10 + retroceso con agresores"),
     ("p8_F1_retagr_noF3_valor", H & F1 & (df.ctx_ret_con_agresores == 1) & ~F3 & VAL, "F1 + retroceso con agresores + no F3 + hacia el valor")]
for nom, m, par in P:
    out = L.correr(df, m, nom, par + "; 10:00-15:30; una posicion por vez; max 5", salidas=("S1", "S6", "S2", "S4b"))
    for sal, (r, ops) in out.items():
        print(L.linea(nom, sal, r)); res[nom + "-" + sal] = r
json.dump(dict(res=res, cortes=cortes), open(os.path.join(L.AQUI, "res_D2.json"), "w"), indent=1, default=str)
