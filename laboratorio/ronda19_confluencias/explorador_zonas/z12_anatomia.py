# -*- coding: utf-8 -*-
"""ZONAS, paso 12: anatomia de C1 (y C2) en diseno: ganadoras contra perdedoras (S6) por rasgo, y operaciones reales para la
explicacion mecanica. Tambien ejemplos de la trampa: toque sin perforar de un nivel estatico (vueltas que fracasan)."""
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import zonas_comun as Z
from z09_candidatas import REGLAS, BANDA
pd.set_option("display.width", 260); pd.set_option("display.max_columns", 60); pd.set_option("display.max_rows", 200)

df = Z.cargar()
lado = df["lado"].to_numpy()
# banda tocada y su penetracion
pen_r = np.where(lado == 1, -df["d_vwr-2"], df["d_vwr+2"]) / df["X"]
pen_j = np.where(lado == 1, -df["d_vwj-2"], df["d_vwj+2"]) / df["X"]
df["pen_banda_rth_X"] = pen_r; df["pen_banda_jor_X"] = pen_j
for k in ("C1_banda_respetada+iceberg", "C2_banda_toque"):
    f, par = REGLAS[k]
    idx = Z.tomar(df, f(df), "S6")
    t = df.loc[idx].copy()
    t["gana"] = (t["S6_pts"] > 0).astype(int)
    print("\n==", k, "S6: n", len(t), "aciertos", int(t.gana.sum()))
    feats = {"corto (lado -1)": t.lado == -1, "contra la marea": t.a_favor_marea == 0, "F1 absorcion": t.F1 == 1, "F6 grandes": t.F6 == 1,
             "F3 divergencia": t.F3 == 1, "veto caudal": t.veto_caudal == 1, "ruido en contra": t.ctx_ruido == "fuera_en_contra",
             "hora >= 12": t.hora >= 12, "VWAP a >= 1,5R": t.dist_vwap_R >= 1.5, "prof = 0 (extremo de la marea)": t.ctx_prof_X == 0,
             "retroceso rapido (>= p75)": t.ctx_vel_ret >= 0.1, "balance inicial roto de los 2 lados": t.ctx_ext_ib == "ambas",
             "dentro del area de valor de ayer": t.ctx_en_va_ayer == "dentro", "va y vuelve": t.regimen_va_y_vuelve == 1,
             "icebergs a favor > en contra": t.of_icebergs_60 > t.of_icebergs_contra_60, "delta 300 s en contra (<0)": t.of_delta_300 < 0}
    rows = []
    for nm, m in feats.items():
        m = m.to_numpy()
        a = t.gana.to_numpy()[m]; b = t.gana.to_numpy()[~m]
        rows.append((nm, int(m.sum()), round(100 * a.mean(), 1) if len(a) else np.nan, int((~m).sum()), round(100 * b.mean(), 1) if len(b) else np.nan))
    print(pd.DataFrame(rows, columns=["rasgo", "n_con", "acierto_con", "n_sin", "acierto_sin"]).to_string(index=False))
    cols = ["dia", "hora_ny", "lado", "entrada", "ext", "stop", "R", "vwap_rth", "pen_banda_rth_X", "pen_banda_jor_X", "dist_vwap_R", "a_favor_marea",
            "F1", "F2", "F6", "of_icebergs_60", "S6_pts", "S6_motivo", "S1_pts", "S1_motivo", "S5_pts", "mfe_R"]
    print(t[cols].round(2).to_string(index=False))
# la trampa: toque sin perforar de un nivel estatico (noche, ayer, IB): vueltas que fracasan
m = Z.contacto(df, "extremos", 0, 0.0, lo_T=True) > 0
idx = Z.tomar(df, m, "S6")
t = df.loc[idx].copy()
print("\n== trampa: toque sin perforar de un extremo estatico (S6): n", len(t), "acierto", round(100 * (t.S6_pts > 0).mean(), 1))
def cual(r):
    out = []
    for nom in Z.GRUPOS["extremos"]:
        pen = -r["d_" + nom] * r["lado"]
        if np.isfinite(pen) and -r["zona_T"] - 1e-9 <= pen <= 1e-9: out.append("%s(%.2f)" % (nom, r["ext"] - r["d_" + nom]))
    return ",".join(out)
t["nivel"] = t.apply(cual, axis=1)
print(t[["dia", "hora_ny", "lado", "entrada", "ext", "stop", "nivel", "S6_pts", "S6_motivo", "S6_seg_salida", "mae_R", "mfe_R"]].head(40).to_string(index=False))
