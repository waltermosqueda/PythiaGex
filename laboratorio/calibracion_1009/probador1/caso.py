# -*- coding: utf-8 -*-
"""
probador1/caso.py — el CASO 2026-10-09 (PREREGISTRO sec. 6 y 11): SOLO DESCRIPTIVO, n = 1, nunca decide.
La noche que motivo el pedido: la 2.0 (C01, conversion 'dos') contra la seleccion de la 2.0 con la conversion de la 4.1 (C03,
'cuatro'), y la capa NDX de la 2.0 (C02). Techos que vio el operador: 31083-31090 (y despues 31092 y 31098,75).
Corre despues de PRUEBA y del sello (control_sello de probar.py). Escribe resultados/caso/caso_1009.json.
"""
import json
import os
import sys

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import candidatas as C                      # noqa: E402
from probar import control_sello, SELLO     # noqa: E402

E = C.E
D = C.D
DIA = "2026-10-09"
OUT = os.path.join(AQUI, "resultados", "caso")


def main():
    control_sello()
    os.makedirs(OUT, exist_ok=True)
    res, dg = C.sesion(DIA, ("C01", "C02", "C03"))
    v = D.velas(DIA)
    sal = {"aviso": "CASO 10-09: descriptivo, n = 1, fuera de toda decision (PREREGISTRO sec. 6 y 11)",
           "velas": {"desde": str(v["t"].min()), "hasta": str(v["t"].max()), "minutos": len(v)}, "diag": dg}
    # conversion a las 03:00 UTC y el strike 750
    t3 = pd.Timestamp("2026-10-09 03:00")
    conv = {m: D.conversion("QQQ", DIA, t3, m) for m in ("dos", "cuatro", "dos_log", "cuatro_log")}
    sal["conversion_0300_utc"] = {m: {"valor": c["valor"], "K750_en_MNQ": 750 * c["valor"] if c["valor"] == c["valor"] else None,
                                      "origen": c["origen"]} for m, c in conv.items()}
    # techos de la noche (velas con maximo en 31080-31100)
    tech = v[(v["h"] >= 31080) & (v["h"] <= 31100)][["t", "o", "h", "l", "c"]]
    sal["velas_con_maximo_31080_31100"] = [{k: (str(x[k]) if k == "t" else float(x[k])) for k in ("t", "o", "h", "l", "c")} for _, x in tech.iterrows()]
    # rayas de cada serie en esos minutos
    for cid in ("C01", "C03", "C02"):
        df = res[cid]
        if df.empty:
            sal[cid] = {"filas": 0}
            continue
        en = df[df["t"].isin(tech["t"])]
        sal[cid] = {"filas": len(df), "minutos": int(df["t"].nunique()),
                    "strikes": sorted(df["K"].unique().tolist())[:40],
                    "rayas_en_minutos_de_techo": [{"t": str(a), "nivel": round(float(b), 2), "et": c, "K": float(k), "conv": float(cv)}
                                                  for a, b, c, k, cv in zip(en["t"], en["nivel"], en["etiqueta"], en["K"], en["conv"])][:60]}
        r = E.evaluar(df, [DIA], nombre=C.NOMBRES[cid] + "_CASO", n_azar=200, n_boot=500, incluir_caso=True, devolver_eventos=True)
        evs = r.pop("_eventos_real", []) or []
        sal[cid]["juez_noche"] = {k: r["operador"]["tolerante"]["noche"][k].get("real") for k in
                                  ("pct_sostenidos", "pct_rotas", "pct_exactos", "pct_falso_entre_sostenidos", "recorrido_por_llegada")}
        sal[cid]["juez_noche"]["muestra"] = {k: r["operador"]["tolerante"]["noche"]["muestra"][k] for k in ("llegadas", "base", "censuradas", "strikes_distintos")}
        sal[cid]["juez_noche"]["corridos_pct_sost"] = r["operador"]["tolerante"]["noche"]["pct_sostenidos"].get("corridos")
        sal[cid]["juez_noche"]["azar_pct_sost"] = r["operador"]["tolerante"]["noche"]["pct_sostenidos"].get("azar_media")
        sal[cid]["eventos"] = [{"t": str(e["t_llegada"]), "raya": round(float(e["raya"]), 2), "tipo": e.get("tipo"), "motivo": e.get("motivo"), "pinchazo_max": e.get("pinchazo_max"), "resultado": e["resultado"],
                                "falso": bool(e.get("falso_rompimiento")), "precision": e.get("precision"), "recorrido": e.get("recorrido"),
                                "etiquetas": e.get("etiquetas"), "censurada": e.get("censurada")} for e in evs]
    with open(os.path.join(OUT, "caso_1009.json"), "w", encoding="utf-8") as f:
        json.dump(E.limpiar_para_json(sal), f, ensure_ascii=False, indent=1, default=str)
    print(json.dumps(E.limpiar_para_json({k: sal[k] for k in ("velas", "conversion_0300_utc", "velas_con_maximo_31080_31100")}), indent=1, default=str)[:4000])
    for cid in ("C01", "C03", "C02"):
        print(cid, json.dumps(E.limpiar_para_json(sal[cid].get("juez_noche")), default=str))
        for e in sal[cid].get("eventos", [])[:40]:
            print("   ", e)


if __name__ == "__main__":
    main()
