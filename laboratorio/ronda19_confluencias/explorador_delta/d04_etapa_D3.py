# -*- coding: utf-8 -*-
"""Explorador DELTA: etapa D3 (pre-registrada 04:00 AR): responsivo contra iniciativa (Dalton) con el flujo agresivo."""
import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import d_lib as L
df = L.cargar()
H = (df.seg_rueda >= 1800) & (df.seg_rueda < 21600)
CTX = {"valor": (df.vwap_rth_dist_pts <= -df.R, "hacia el valor (VWAP a >=1R del lado de la ganancia)"),
       "lejos": (df.vwap_rth_dist_pts > 0, "lejos del valor (VWAP del lado del stop)")}
FL = [("d60alto", df.d60_ratio >= 0.10, "d60_ratio >= 0,10"), ("imbfav", df.imb_neto_3v > 0, "imbalances netos a favor > 0"),
      ("bigfav", df.of_netbig60_a_favor > 0, "big netos a favor > 0"), ("F6", df.F6 == 1, "F6 big netos a favor >= p80"),
      ("giro", df.giro_delta == 1, "giro del delta (300 s en contra, 60 s a favor)"),
      ("bigcontra", df.of_netbig60_a_favor < 0, "big netos EN CONTRA < 0 (agresion absorbida)"),
      ("d60neg", df.d60_ratio <= 0, "d60_ratio <= 0 (delta del ultimo minuto en contra)"),
      ("imbcontra", df.imb_neto_3v < 0, "imbalances netos en contra < 0")]
res = {}
for fn, fm, ftxt in FL:
    for cn, (cm, ctxt) in CTX.items():
        nom = "D3_%s_%s" % (fn, cn)
        out = L.correr(df, H & fm & cm, nom, "%s + %s; 10:00-15:30; una posicion por vez; max 5" % (ftxt, ctxt), salidas=("S1", "S6"))
        for sal, (r, ops) in out.items():
            print(L.linea(nom, sal, r)); res[nom + "-" + sal] = r
json.dump(res, open(os.path.join(L.AQUI, "res_D3.json"), "w"), indent=1, default=str)
