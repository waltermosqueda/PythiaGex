# -*- coding: utf-8 -*-
"""Explorador DELTA: etapa D5 (pre-registrada 04:20 AR): 'la vuelta falsa' con S6m (espejo de S6)."""
import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import d_lib as L
df = L.cargar()
H = (df.seg_rueda >= 1800) & (df.seg_rueda < 21600)
PAS = (df.F3 == 1) | (df.ctx_ret_con_agresores == 0)
LEJ = df.vwap_rth_dist_pts > 0
NIV = df.n_niveles_en_zona >= 1
F = [("f1_retpasivo", H & (df.ctx_ret_con_agresores == 0), "retroceso sin agresores"),
     ("f2_F3", H & (df.F3 == 1), "F3 divergencia"),
     ("f3_apilados", H & (df.of_apilados_favor_3v >= 1), "apilados a favor de la vuelta >= 1"),
     ("f4_imbalto", H & (df.imb_neto_3v > 1), "imb_neto_3v > 1 (tercio alto)"),
     ("f5_pasivo", H & PAS, "retroceso pasivo (F3 o sin agresores)"),
     ("f6_pasivo_imb", H & PAS & (df.imb_neto_3v > 0), "retroceso pasivo + imb_neto_3v > 0"),
     ("f7_rafaga_lejos", H & ((df.of_apilados_favor_3v >= 1) | (df.imb_neto_3v > 1)) & LEJ, "(apilados o imb > 1) + lejos del valor"),
     ("f8_F3_nivel", H & (df.F3 == 1) & NIV, "F3 + nivel"),
     ("f9_bigalto_lejos", H & (df.of_netbig60_a_favor > 57) & LEJ, "big netos a favor > 57 (tercio alto) + lejos del valor")]
res = {}
for nom, m, par in F:
    out = L.correr(df, m, nom, par + "; OPERA AL REVES DE LA VUELTA (S6m espejo); 10:00-15:30; una posicion por vez; max 5", salidas=("S6m",),
                   registrar=False)
    r, ops = out["S6m"]
    pA, mA, hA, _, _ = L.placebo_lado(df, m, "S6m", draws=500)
    L.anotar(nom, "S6m", par + "; OPERA AL REVES DE LA VUELTA (S6m espejo de S6); 10:00-15:30; una posicion por vez; max 5", r, placebo_p=round(pA, 3),
             nota="placebo A = lado sorteado S6/S6m (500)")
    print(L.linea(nom, "S6m", r), "| placebo A: neto %+.2f acierto %.1f %% p %.3f" % (mA, hA, pA))
    res[nom] = dict(r, placeboA_p=pA, placeboA_neto=mA, placeboA_acierto=hA)
json.dump(res, open(os.path.join(L.AQUI, "res_D5.json"), "w"), indent=1, default=str)
