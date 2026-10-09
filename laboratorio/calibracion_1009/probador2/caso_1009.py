# -*- coding: utf-8 -*-
"""
caso_1009.py — CASO 10-09 (PREREGISTRO sec. 6 y 11): SOLO DESCRIPTIVO, n = 1, NUNCA DECIDE. Se corre DESPUES de abrir PRUEBA con el
sello del grupo 2. Mide C07-C12 en la noche que motivo el pedido con el mismo arnes (incluir_caso=True, n_azar=500) y lista que
dibujaba C08 (MUROS_QQQ_oi = la serie que puso el 750 en ~31076) alrededor de los techos 31083-31090.
Salida: resultados/CASO_1009.json
"""
import os
import sys
import json

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import grupo2 as G                  # noqa: E402
import evaluar_grupo2 as E          # noqa: E402
EV = G.EV
DIA = "2026-10-09"


def main():
    out, diag = G.niveles_sesion(DIA)
    res = {"aviso": "CASO 10-09: descriptivo, n = 1 sesion (parcial: el dataset llega hasta ~04:00 UTC); no decide nada.",
           "diag": diag, "candidatas": {}, "C08_por_minuto": []}
    for c in E.ORDEN:
        df = out[c]
        if df.empty:
            res["candidatas"][G.IDS[c]] = "sin rayas"
            continue
        r = EV.evaluar(df, [DIA], nombre=G.IDS[c] + "_CASO", n_azar=500, n_boot=500, incluir_caso=True, devolver_eventos=True)
        evs = r.pop("_eventos_real") or []
        fila = {w: E.fila_resumen(r, w) for w in EV.VENTANAS}
        fila["eventos_noche"] = [{k: (str(v) if isinstance(v, (pd.Timestamp, np.datetime64)) else v) for k, v in e.items()
                                  if k in ("t_llegada", "raya", "tipo", "resultado", "censurada", "falso_rompimiento", "precision", "recorrido",
                                           "pinchazo_max", "etiquetas")}
                                 for e in evs if EV._cod_ventana_min(np.array([pd.Timestamp(e["t_llegada"]).value // 60_000_000_000]))[0] == 0]
        res["candidatas"][G.IDS[c]] = fila
    d8 = out["C08"]
    for t, g in d8.groupby("t"):
        if t.minute % 15 == 0:
            res["C08_por_minuto"].append({"t": str(t), "rayas": [(round(x, 2), e, k) for x, e, k in zip(g["nivel"], g["etiqueta"], g["K"])]})
    with open(os.path.join(AQUI, "resultados", "CASO_1009.json"), "w", encoding="utf-8") as f:
        json.dump(EV.limpiar_para_json(res), f, ensure_ascii=False, indent=1, default=str)
    for nom, x in res["candidatas"].items():
        if isinstance(x, str):
            print(nom, x); continue
        n = x["noche"]
        print(nom, "noche base", n["base"], "sost %", n["M1_real"], "corr", n["M1_corridos"], "azar", n["M1_azar_media"])
        for e in x["eventos_noche"]:
            print("   ", e)
    print(json.dumps(res["C08_por_minuto"][:30], default=str))


if __name__ == "__main__":
    main()
