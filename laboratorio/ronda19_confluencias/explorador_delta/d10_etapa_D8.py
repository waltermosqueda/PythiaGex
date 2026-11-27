# -*- coding: utf-8 -*-
"""Explorador DELTA: etapa D8 (pre-registrada 04:40 AR): ablaciones de w1 y w6."""
import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import d_lib as L
df = L.cargar()
H = (df.seg_rueda >= 1800) & (df.seg_rueda < 21600)
AP = df.of_apilados_favor_3v >= 1; SA = df.ctx_ret_con_agresores == 0; F3 = df.F3 == 1; NIV = df.n_niveles_en_zona >= 1
RAP = df.seg_desde_ext <= 107
TXT = "; FADE (S6m espejo de S6); 10:00-15:30; una posicion por vez; max 5"
res = {}
for nom, m, par in [("a1_fade_ap_rap", H & AP & RAP, "apilados a favor >=1 + vuelta en <=107 s"),
                    ("a2_fade_sa_rap", H & SA & RAP, "retroceso sin agresores + vuelta en <=107 s"),
                    ("a3_fade_F3niv_rap", H & F3 & NIV & RAP, "F3 + nivel + vuelta en <=107 s")]:
    out = L.correr(df, m, nom, par + TXT, salidas=("S6m",), registrar=False); r, ops = out["S6m"]
    pA, mA, hA, _, _ = L.placebo_lado(df, m, "S6m", draws=500)
    L.anotar(nom, "S6m", par + TXT, r, placebo_p=round(pA, 3), nota="placebo A = lado sorteado S6/S6m (500); ablacion de w1/w6")
    h1 = ops[ops.dia <= "2026-09-25"]; h2 = ops[ops.dia > "2026-09-25"]
    print(L.linea(nom, "S6m", r), "| pA %.3f | mitades %.0f%%/%.0f%% (n %d/%d)" % (pA, 100 * (h1.pts > 0).mean(), 100 * (h2.pts > 0).mean(), len(h1), len(h2)))
    res[nom] = dict(r, pA=pA)
json.dump(res, open(os.path.join(L.AQUI, "res_D8.json"), "w"), indent=1, default=str)
