# -*- coding: utf-8 -*-
"""Explorador DELTA: descripcion de los rasgos de flujo SIN mirar resultados (frecuencias, cruces, correlaciones)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import d_lib as L
pd.set_option("display.width", 250); pd.set_option("display.max_columns", 60)
df = L.cargar()
print("eventos", len(df), "ruedas", df.dia.nunique())
h = df[(df.seg_rueda >= 1800) & (df.seg_rueda < 21600)]
print("10:00-15:30:", len(h), "| por rueda %.1f" % (len(h) / 26))
print("\n--- frecuencias (10:00-15:30) por a_favor_marea")
cols = ["F1", "F2", "F3", "F4", "F5", "F6", "veto_caudal", "ctx_ret_con_agresores", "giro_delta", "marea_con_agresores", "extension_fuera_ruido"]
print(h.groupby("a_favor_marea")[cols].mean().round(3).T)
print("\n--- cuantiles de derivados")
for c in ["seg_desde_ext", "leg_pts", "del_ret_contra", "del_ret_por_pt", "of_delta_60", "d60_ratio", "of_delta_300", "imb_neto_3v",
          "ice_neto_60", "big_neto", "of_abs_lado_pasivo_rel", "of_icebergs_60", "ctx_prof_X", "ctx_pierna_marea_X", "marea_del_por_X",
          "vwap_rth_dist_sd", "R_en_X", "n_niveles_en_zona", "n_barridas_y_vuelta"]:
    q = h[c].quantile([.05, .1, .25, .5, .75, .9, .95]).round(3).tolist()
    print("%-24s %s  (>0: %.2f)" % (c, q, (h[c] > 0).mean()))
print("\n--- correlaciones de Spearman entre rasgos de flujo")
cc = ["of_delta_60", "d60_ratio", "of_delta_300", "del_ret_contra", "del_ret_por_pt", "seg_desde_ext", "imb_neto_3v", "ice_neto_60", "big_neto",
      "of_abs_lado_pasivo_rel", "F3", "F4", "veto_caudal", "ctx_prof_X", "vwap_rth_dist_sd"]
print(h[cc].corr(method="spearman").round(2))
print("\n--- F3 (divergencia) cruzada con contexto (conteos 10:00-15:30)")
print(pd.crosstab([h.a_favor_marea, h.F3], h.n_niveles_en_zona > 0, margins=True))
print(pd.crosstab([h.a_favor_marea, h.F3], h.veto_caudal, margins=True))
print("\nret sin agresores por a_favor_marea:")
print(pd.crosstab(h.a_favor_marea, h.ctx_ret_con_agresores, margins=True))
print("\ngiro_delta por a_favor_marea:")
print(pd.crosstab(h.a_favor_marea, h.giro_delta, margins=True))
print("\nlado VWAP (vwap_rth_dist_pts>0 = del lado a favor) por a_favor_marea:")
print(pd.crosstab(h.a_favor_marea, h.vwap_rth_dist_pts > 0, margins=True))
print("\neventos por rueda (10:00-15:30) y regimen:")
print(h.groupby("dia").agg(n=("lado", "size"), reg=("regimen_va_y_vuelve", "first"), X=("X", "first")).T)
