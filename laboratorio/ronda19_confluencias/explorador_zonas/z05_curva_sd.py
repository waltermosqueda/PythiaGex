# -*- coding: utf-8 -*-
"""ZONAS, paso 5: acierto contra la distancia firmada del EXTREMO al VWAP (en desvios; negativo = del lado opuesto a la operacion).
Bins de 0,25 sd entre -3 y +1 para el VWAP RTH (columna ext_dist_vwap_rth_sd) y, aparte, la distancia a la banda de 2 sd (pen en X).
Cada bin se simula (S6, S1) y se registra."""
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import zonas_comun as Z

df = Z.cargar()
H = ((df.seg_rueda >= Z.H_INI) & (df.seg_rueda < Z.H_FIN)).to_numpy()


def ev(mask, sal):
    m = np.asarray(mask, bool) & H & df[sal + "_pts"].notna().to_numpy()
    pts = df[sal + "_pts"].to_numpy()[m]; R = df["R"].to_numpy()[m]
    if len(pts) < 5: return dict(n=len(pts))
    return dict(n=len(pts), acierto=round(100 * (pts > 0).mean(), 1), R_medio=round(float((pts / R).mean()), 3))


x = df["ext_vwap_sd_firmado"].to_numpy()
bins = [(-9, -2.5), (-2.5, -2.25), (-2.25, -2.0), (-2.0, -1.75), (-1.75, -1.5), (-1.5, -1.0), (-1.0, -0.5), (-0.5, 0), (0, 0.5), (0.5, 9)]
print("== extremo vs VWAP RTH en desvios (firmado: negativo = del lado opuesto a la operacion)")
for a, b in bins:
    m = (x > a) & (x <= b)
    sim = Z.evaluar("Z5_ext_sd(%.2f,%.2f]" % (a, b), "a (curva)", "extremo a (%.2f, %.2f] desvios del VWAP RTH (firmado)" % (a, b), m, ("S6", "S1"),
                    nota="curva de distancia al VWAP", imprimir=False)
    e6 = ev(m, "S6"); e1 = ev(m, "S1")
    print("  (%5.2f,%5.2f]  evento: n %4d S6 %5s %% %+.2f R | S1 %5s %% %+.2f R || simulado S6 n %3d %5s %% neto %+.2f t %+.2f | S1 %5s %% neto %+.2f t %+.2f" % (
        a, b, e6["n"], e6.get("acierto"), e6.get("R_medio", np.nan), e1.get("acierto"), e1.get("R_medio", np.nan),
        sim["S6"]["n"], sim["S6"].get("acierto_pct"), sim["S6"].get("neto_op_c050", np.nan), sim["S6"]["t_rueda_c050"],
        sim["S1"].get("acierto_pct"), sim["S1"].get("neto_op_c050", np.nan), sim["S1"]["t_rueda_c050"]))
# distancia del extremo a la banda de 2 sd del lado opuesto (pen en X: >0 perforo la banda)
p2 = Z.GRUPOS_LADO["bandas_rev_2sd"]
pen = np.where(df.lado == 1, -df["d_vwr-2"], df["d_vwr+2"]) / df["X"]
print("== pen del extremo respecto de la banda de 2 sd RTH del lado opuesto (en X; >0 = la perforo)")
for a, b in [(-1.0, -0.5), (-0.5, -0.25), (-0.25, -0.125), (-0.125, 0.0), (0.0, 0.125), (0.125, 0.25), (0.25, 0.5), (0.5, 1.0), (1.0, 2.0)]:
    m = (pen > a) & (pen <= b)
    sim = Z.evaluar("Z5_pen2sd(%.3f,%.3f]" % (a, b), "a/d (curva)", "extremo a (%.3f, %.3f] X de la banda 2 sd RTH del lado opuesto (>0 perforo)" % (a, b), m,
                    ("S6", "S1"), nota="curva de penetracion de la banda 2 sd", imprimir=False)
    e6 = ev(m, "S6"); e1 = ev(m, "S1")
    print("  (%6.3f,%6.3f]  evento: n %4d S6 %5s %% %+.2f R | S1 %5s %% %+.2f R || simulado S6 n %3d %5s %% neto %+.2f t %+.2f | S1 %5s %% neto %+.2f t %+.2f" % (
        a, b, e6["n"], e6.get("acierto"), e6.get("R_medio", np.nan), e1.get("acierto"), e1.get("R_medio", np.nan),
        sim["S6"]["n"], sim["S6"].get("acierto_pct"), sim["S6"].get("neto_op_c050", np.nan), sim["S6"]["t_rueda_c050"],
        sim["S1"].get("acierto_pct"), sim["S1"].get("neto_op_c050", np.nan), sim["S1"]["t_rueda_c050"]))
