# -*- coding: utf-8 -*-
"""ZONAS, paso 14: resumen final de las candidatas (no registra variantes nuevas): metricas S6/S1/S2/S5/S4b, placebos, Bonferroni y
DSR contra el TOTAL de variantes de diseno de todos los registros, y la lista de operaciones de cada candidata (para el arbitro)."""
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import json
import numpy as np, pandas as pd
import zonas_comun as Z
from z09_candidatas import REGLAS

df = Z.cargar()
plac = json.load(open(os.path.join(Z.AQUI, "z10_placebos.json")))
N, por, srs, ruedas = Z.contar_variantes_globales()
print("VARIANTES DE DISENO (todos los registros): N = %d %s" % (N, por))
res = {"N_total": N, "por_registro": por, "candidatas": {}}
ops_all = []
for k, (f, par) in REGLAS.items():
    res["candidatas"][k] = {"regla": par, "salidas": {}}
    print("\n==", k, "|", par)
    for sal in ("S6", "S1", "S2", "S5", "S4b"):
        idx = Z.tomar(df, f(df), sal)
        m = Z.metricas(df, idx, sal)
        d = m.pop("_serie050")
        bd = Z.bonferroni_y_dsr(d)
        sin = np.delete(d, np.argmax(d))
        r = {kk: m.get(kk) for kk in ("n", "ops_por_rueda", "acierto_pct", "gana_media", "pierde_media", "bruto_op", "bruto_R", "neto_op_c050", "neto_op_c100",
                                       "t_rueda_c050", "t_rueda_c100", "ruedas_con_ops", "ruedas_positivas_c050", "mejor_rueda_c050", "peor_rueda_c050",
                                       "racha_perdedora", "caida_max_c050")}
        r["mejor_rueda_dia"] = Z.DIAS[int(np.argmax(d))]; r["peor_rueda_dia"] = Z.DIAS[int(np.argmin(d))]
        r["t_sin_mejor_rueda"] = round(float(sin.mean() / sin.std(ddof=1) * np.sqrt(len(sin))), 2)
        r.update(t_bonferroni=bd["t_bonferroni"], sharpe_diario=bd["sharpe_diario"], dsr=bd["dsr"])
        pa = plac.get(k, {}).get("A", {}).get(sal); pb = plac.get(k, {}).get("B", {}).get(sal)
        if pa: r["placeboA"] = {kk: pa[kk] for kk in ("azar_neto050_media", "azar_acierto_media", "p_neto", "p_acierto", "contrario_neto050", "contrario_acierto")}
        if pb: r["placeboB"] = {kk: pb[kk] for kk in ("plac_neto_media", "plac_acierto_media", "p_neto", "p_acierto")}
        res["candidatas"][k]["salidas"][sal] = r
        print("  %-3s n %3d (%.2f/r) acierto %5.1f %% neto050 %+.2f neto100 %+.2f t %+.2f (sin la mejor rueda %+.2f) | mejor %+.1f (%s) peor %+.1f (%s) | ruedas+ %d/%d | DSR %.3f | A %s | B %s" % (
            sal, r["n"], r["ops_por_rueda"], r["acierto_pct"], r["neto_op_c050"], r["neto_op_c100"], r["t_rueda_c050"], r["t_sin_mejor_rueda"],
            r["mejor_rueda_c050"], r["mejor_rueda_dia"], r["peor_rueda_c050"], r["peor_rueda_dia"], r["ruedas_positivas_c050"], r["ruedas_con_ops"], r["dsr"],
            r.get("placeboA"), r.get("placeboB")))
        if sal in ("S6", "S1"):
            t = df.loc[idx, ["dia", "hora_ny", "t_utc", "lado", "entrada", "ext", "stop", "R", "X", "vwap_rth", sal + "_pts", sal + "_motivo", sal + "_seg_salida"]].copy()
            t.columns = [c.replace(sal + "_", "") for c in t.columns]
            t.insert(0, "salida", sal); t.insert(0, "candidata", k)
            ops_all.append(t)
pd.concat(ops_all).to_csv(os.path.join(Z.AQUI, "operaciones_candidatas_diseno.csv"), index=False)
json.dump(res, open(os.path.join(Z.AQUI, "resumen_candidatas.json"), "w"), indent=1, default=str)
print("\nt de Bonferroni (una cola, 5 %%/N, 25 gl) con N = %d: %.2f" % (N, res["candidatas"]["C1_banda_respetada+iceberg"]["salidas"]["S6"]["t_bonferroni"]))
