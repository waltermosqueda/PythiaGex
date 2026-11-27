# -*- coding: utf-8 -*-
"""ZONAS, paso 11: diagnostico de las candidatas (no son variantes nuevas): serie por rueda, sin la mejor rueda, por mes, por lado,
por fuente de la banda, por hora; Bonferroni y Deflated Sharpe contra el TOTAL de variantes de diseno de todos los registros."""
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import json
import numpy as np, pandas as pd
import zonas_comun as Z
from z09_candidatas import REGLAS, BANDA

df = Z.cargar()
N, por, srs, ruedas = Z.contar_variantes_globales()
print("variantes de DISENO en todos los registros:", N, por)
out = {}
for k, (f, par) in REGLAS.items():
    out[k] = {}
    for sal in ("S6", "S1"):
        idx = Z.tomar(df, f(df), sal)
        met = Z.metricas(df, idx, sal)
        t = df.loc[idx].copy(); t["pts"] = t[sal + "_pts"]; t["neto"] = t["pts"] - 0.5
        d = met["_serie050"]
        sin_mejor = np.delete(d, np.argmax(d))
        bd = Z.bonferroni_y_dsr(d)
        t["mes"] = t["dia"].str[:7]
        t["hora_b"] = pd.cut(t["hora"], [10, 12, 14, 15.5], right=False, labels=["10-12", "12-14", "14-15:30"])
        res = dict(n=met["n"], ops_rueda=met["ops_por_rueda"], acierto=met["acierto_pct"], neto050=met["neto_op_c050"], neto100=met["neto_op_c100"],
                   t050=met["t_rueda_c050"], t100=met["t_rueda_c100"], ruedas_con_ops=met["ruedas_con_ops"], ruedas_pos=met["ruedas_positivas_c050"],
                   mejor=met["mejor_rueda_c050"], peor=met["peor_rueda_c050"], racha=met["racha_perdedora"], caida=met["caida_max_c050"],
                   gana_media=met["gana_media"], pierde_media=met["pierde_media"], R_medio=float(t["R"].mean()),
                   t_sin_mejor_rueda=round(float(sin_mejor.mean() / sin_mejor.std(ddof=1) * np.sqrt(len(sin_mejor))), 2),
                   neto_sin_mejor_rueda=round(float((t["neto"].sum() - d.max()) / max(1, met["n"] - (t.groupby("dia").size().loc[Z.DIAS[int(np.argmax(d))]] if Z.DIAS[int(np.argmax(d))] in set(t["dia"]) else 0))), 3),
                   bonferroni=bd)
        for col in ("mes", "lado", "hora_b"):
            g = t.groupby(col, observed=True).agg(n=("pts", "size"), acierto=("pts", lambda x: round(100 * (x > 0).mean(), 1)), neto050=("neto", lambda x: round(x.mean(), 2)))
            res["por_" + col] = g.reset_index().astype(str).to_dict("records")
        out[k][sal] = res
        print("==", k, sal, json.dumps({kk: vv for kk, vv in res.items() if not kk.startswith("por_")}, default=str))
        for col in ("mes", "lado", "hora_b"):
            print("    ", col, res["por_" + col])
json.dump(out, open(os.path.join(Z.AQUI, "z11_diagnostico.json"), "w"), indent=1, default=str)
