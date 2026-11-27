# -*- coding: utf-8 -*-
"""Explorador DELTA: etapas D0 (un rasgo + ablaciones de contexto) y D1 (2-3 condiciones), tal cual PREREGISTRO_DELTA.md (03:45 AR).
Cada regla x salida se anota en registro_variantes_delta.csv."""
import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import d_lib as L

df = L.cargar()
H = (df.seg_rueda >= 1800) & (df.seg_rueda < 21600)
VAL = df.vwap_rth_dist_pts <= -df.R            # hacia el valor: VWAP RTH del lado de la ganancia a >= 1R
NIV = df.n_niveles_en_zona >= 1
AF = df.a_favor_marea == 1
CM = df.a_favor_marea == 0

D0 = [("ctx_todas", H, "todas las vueltas 10:00-15:30 (ablacion)"),
      ("ctx_valor", H & VAL, "hacia el valor: VWAP RTH a >=1R del lado de la ganancia (ablacion)"),
      ("ctx_nivel", H & NIV, "extremo en zona de >=1 nivel (ablacion)"),
      ("ctx_afavor", H & AF, "a favor de la marea (ablacion)"),
      ("ctx_contra", H & CM, "en contra de la marea (ablacion)"),
      ("D0_F3", H & (df.F3 == 1), "F3 divergencia de delta"),
      ("D0_F4", H & (df.F4 == 1), "F4 agotamiento"),
      ("D0_climax", H & (df.veto_caudal == 1), "veto_caudal leido como senal (climax en el extremo)"),
      ("D0_F1", H & (df.F1 == 1), "F1 absorcion"),
      ("D0_F6", H & (df.F6 == 1), "F6 big trades netos a favor >= p80"),
      ("D0_ice", H & (df.ice_neto_60 > 0), "icebergs del lado que defiende > del otro (60 s)"),
      ("D0_giro", H & (df.giro_delta == 1), "giro del delta: 300 s en contra, 60 s a favor"),
      ("D0_d60", H & (df.d60_ratio >= 0.10), "delta 60 s / volumen 60 s >= 0,10"),
      ("D0_retpasivo", H & (df.ctx_ret_con_agresores == 0), "retroceso sin agresores"),
      ("D0_esfuerzo_alto", H & (df.del_ret_por_pt >= 137), "delta del retroceso por punto >= 137 (esfuerzo alto)"),
      ("D0_esfuerzo_bajo", H & (df.del_ret_por_pt <= 49), "delta del retroceso por punto <= 49 (esfuerzo bajo)"),
      ("D0_imb", H & (df.imb_neto_3v > 0), "imbalances netos a favor en 3 velas > 0"),
      ("D0_apilados", H & (df.of_apilados_favor_3v >= 1), "apilados a favor en 3 velas >= 1"),
      ("D0_big", H & (df.of_netbig60_a_favor > 0), "big trades netos a favor > 0")]

D1 = [("e1_F3_nivel", H & (df.F3 == 1) & NIV, "F3 + nivel", ("S1", "S6")),
      ("e2_F3_valor", H & (df.F3 == 1) & VAL, "F3 + hacia el valor", ("S1", "S6", "S5")),
      ("e3_F3_contra", H & (df.F3 == 1) & CM, "F3 + en contra de la marea", ("S1", "S6")),
      ("e4_climax_valor", H & (df.veto_caudal == 1) & VAL, "climax + hacia el valor", ("S1", "S6", "S5")),
      ("e5_esfuerzo_nivel", H & (df.del_ret_por_pt >= 137) & NIV, "esfuerzo alto + nivel", ("S1", "S6")),
      ("e6_pasivo_afavor", H & (df.ctx_ret_con_agresores == 0) & AF, "retroceso sin agresores + a favor de la marea", ("S1", "S6")),
      ("e7_giro_big", H & (df.giro_delta == 1) & (df.of_netbig60_a_favor > 0), "giro del delta + big netos a favor", ("S1", "S6")),
      ("e8_giro_valor", H & (df.giro_delta == 1) & VAL, "giro del delta + hacia el valor", ("S1", "S6", "S5")),
      ("e9_d60_valor", H & (df.d60_ratio >= 0.10) & VAL, "d60_ratio >= 0,10 + hacia el valor", ("S1", "S6", "S5")),
      ("e10_imb_valor", H & (df.imb_neto_3v > 0) & VAL, "imbalances netos a favor + hacia el valor", ("S1", "S6", "S5")),
      ("e11_F3_giro", H & (df.F3 == 1) & (df.giro_delta == 1), "F3 + giro del delta", ("S1", "S6")),
      ("e12_F3_F6", H & (df.F3 == 1) & (df.F6 == 1), "F3 + F6", ("S1", "S6"))]

res = {}
print("=== D0 (un rasgo / ablaciones) ===")
for nom, m, par in D0:
    out = L.correr(df, m, nom, par + "; 10:00-15:30; una posicion por vez; max 5", salidas=("S1", "S6"))
    for sal, (r, ops) in out.items():
        print(L.linea(nom, sal, r)); res[nom + "-" + sal] = r
print("\n=== D1 (2-3 condiciones) ===")
for nom, m, par, sals in D1:
    out = L.correr(df, m, nom, par + "; 10:00-15:30; una posicion por vez; max 5", salidas=sals)
    for sal, (r, ops) in out.items():
        print(L.linea(nom, sal, r)); res[nom + "-" + sal] = r
json.dump(res, open(os.path.join(L.AQUI, "res_D0_D1.json"), "w"), indent=1, default=str)
