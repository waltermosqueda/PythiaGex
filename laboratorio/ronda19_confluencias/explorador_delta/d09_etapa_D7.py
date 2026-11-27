# -*- coding: utf-8 -*-
"""Explorador DELTA: etapa D7 (pre-registrada 04:34 AR): el pinchazo (vuelta rapida con rafaga o tras retroceso pasivo) -> fade S6m."""
import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import d_lib as L
df = L.cargar()
H = (df.seg_rueda >= 1800) & (df.seg_rueda < 21600)
AP = df.of_apilados_favor_3v >= 1; SA = df.ctx_ret_con_agresores == 0; F3 = df.F3 == 1; NIV = df.n_niveles_en_zona >= 1
RAP = df.seg_desde_ext <= 107; RAP1 = df.seg_desde_ext <= 38; LEJ = df.vwap_rth_dist_pts > 0
TXT = "; FADE (S6m espejo de S6); 10:00-15:30; una posicion por vez; max 5"
res = {}
for nom, m, par in [("w1_fade_apsa_rap", H & (AP | SA) & RAP, "(apilados a favor >=1 o retroceso sin agresores) + vuelta en <=107 s"),
                    ("w2_fade_rap107", H & RAP, "solo vuelta en <=107 s (ablacion)"),
                    ("w3_fade_rap38", H & RAP1, "solo vuelta en <=38 s (ablacion)"),
                    ("w4_fade_apsa_lejos", H & (AP | SA) & LEJ, "(apilados o sin agresores) + lejos del valor"),
                    ("w5_fade_apsa_rap_lejos", H & (AP | SA) & RAP & LEJ, "(apilados o sin agresores) + <=107 s + lejos del valor"),
                    ("w6_fade_apF3niv_rap", H & (AP | (F3 & NIV)) & RAP, "(apilados o F3 en nivel) + <=107 s"),
                    ("w7_fade_apsaF3_rap", H & (AP | SA | F3) & RAP, "(apilados o sin agresores o F3) + <=107 s")]:
    out = L.correr(df, m, nom, par + TXT, salidas=("S6m",), registrar=False); r, ops = out["S6m"]
    pA, mA, hA, _, _ = L.placebo_lado(df, m, "S6m", draws=500)
    L.anotar(nom, "S6m", par + TXT, r, placebo_p=round(pA, 3), nota="placebo A = lado sorteado S6/S6m (500)")
    h1 = ops[ops.dia <= "2026-09-25"]; h2 = ops[ops.dia > "2026-09-25"]
    print(L.linea(nom, "S6m", r), "| pA %.3f | mitades %.0f%%/%.0f%% (n %d/%d) | mejor %+.1f (%s) peor %+.1f (%s)" % (
        pA, 100 * (h1.pts > 0).mean(), 100 * (h2.pts > 0).mean(), len(h1), len(h2), r["mejor_rueda"], r["dia_mejor"], r["peor_rueda"], r["dia_peor"]))
    res[nom] = dict(r, pA=pA)
json.dump(res, open(os.path.join(L.AQUI, "res_D7.json"), "w"), indent=1, default=str)
