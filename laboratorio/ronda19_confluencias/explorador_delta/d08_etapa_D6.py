# -*- coding: utf-8 -*-
"""Explorador DELTA: etapa D6 (pre-registrada 04:27 AR): combinaciones del fade y reglas dobles."""
import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import d_lib as L
df = L.cargar()
H = (df.seg_rueda >= 1800) & (df.seg_rueda < 21600)
AP = df.of_apilados_favor_3v >= 1
PAS = (df.F3 == 1) | (df.ctx_ret_con_agresores == 0)
NIV = df.n_niveles_en_zona >= 1
VAL = df.vwap_rth_dist_pts <= -df.R
D60 = df.d60_ratio >= 0.10
res = {}
def fade(nom, m, par):
    out = L.correr(df, m, nom, par, salidas=("S6m",), registrar=False); r, ops = out["S6m"]
    pA, mA, hA, _, _ = L.placebo_lado(df, m, "S6m", draws=500)
    L.anotar(nom, "S6m", par, r, placebo_p=round(pA, 3), nota="placebo A = lado sorteado S6/S6m (500)")
    print(L.linea(nom, "S6m", r), "| placebo A neto %+.2f acierto %.1f %% p %.3f" % (mA, hA, pA)); res[nom] = dict(r, pA=pA)
TXT = "; 10:00-15:30; una posicion por vez; max 5"
fade("v1_fade_ap_y_pas", H & AP & PAS, "FADE (S6m): apilados a favor >=1 Y retroceso pasivo (F3 o sin agresores)" + TXT)
fade("v2_fade_ap_o_F3niv", H & (AP | ((df.F3 == 1) & NIV)), "FADE (S6m): apilados >=1 O (F3 y nivel)" + TXT)
fade("v3_fade_ap_o_sinagr", H & (AP | (df.ctx_ret_con_agresores == 0)), "FADE (S6m): apilados >=1 O retroceso sin agresores" + TXT)
fade("v4_fade_ap_o_pas", H & (AP | PAS), "FADE (S6m): apilados >=1 O retroceso pasivo" + TXT)
fade("v5_fade_appas_noval", H & (AP | PAS) & ~VAL, "FADE (S6m): (apilados o pasivo) y NO hacia el valor" + TXT)
# reglas dobles: columna S6x
for nom, take, par in (("v6_doble_imb0", VAL & D60 & (df.imb_neto_3v <= 0), "DOBLE: fade (S6m) si apilados o pasivo; si no, tomar (S6) si VWAP>=1R + d60>=0,10 + imb<=0"),
                       ("v7_doble", VAL & D60, "DOBLE: fade (S6m) si apilados o pasivo; si no, tomar (S6) si VWAP>=1R + d60>=0,10")):
    fd = AP | PAS
    df["S6x_pts"] = np.where(fd, df.S6m_pts, np.where(take, df.S6_pts, np.nan)); df["S6x_seg_salida"] = df.S6_seg_salida
    m = H & (fd | take)
    out = L.correr(df, m, nom, par + TXT, salidas=("S6x",), registrar=False); r, ops = out["S6x"]
    pA, mA, hA, _, _ = L.placebo_lado(df, m, "S6x", draws=500)
    L.anotar(nom, "S6x", par + TXT, r, placebo_p=round(pA, 3), nota="placebo A = lado sorteado S6/S6m (500)")
    nf = int((ops.index.isin(df.index[fd])).sum())
    print(L.linea(nom, "S6x", r), "| fades %d tomas %d | placebo A neto %+.2f p %.3f" % (nf, len(ops) - nf, mA, pA)); res[nom] = dict(r, pA=pA)
out = L.correr(df, H & VAL & D60 & ~AP & ~PAS, "v8_tomar_limpia", "TOMAR: VWAP>=1R + d60>=0,10 + sin apilados + no pasivo" + TXT, salidas=("S1", "S6", "S2", "S4b"))
for sal, (r, ops) in out.items():
    print(L.linea("v8_tomar_limpia", sal, r)); res["v8-" + sal] = r
json.dump(res, open(os.path.join(L.AQUI, "res_D6.json"), "w"), indent=1, default=str)
