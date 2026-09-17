# -*- coding: utf-8 -*-
"""verdes_07 — QUE TUVO DE DISTINTO EL 17-09? La fuerza de las verdes (gamma x volumen del strike dominante, en millones), cuanto pesan contra el resto
del libro cercano (concentracion), la gamma neta del libro y el rango del dia: por dia, solo rueda."""
import os, sys, json, glob
from datetime import datetime
import numpy as np, pandas as pd
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI); sys.path.insert(0, os.path.dirname(AQUI))
import base as B, capas_nq as C
from verdes_05_reconstruir import a_cadena, VIVA
filas = []
for p in sorted(glob.glob(os.path.join(VIVA, "viva-NQ-*.jsonl"))):
    for l in open(p, encoding="utf-8", errors="replace"):
        l = l.strip()
        if len(l) < 40 or not l.endswith("}"): continue
        try: r = json.loads(l)
        except Exception: continue
        hm = r["ts"][11:16]
        if not ("13:30" <= hm < "20:00"): continue
        fut = float(r.get("futuro") or 0)
        if fut <= 0: continue
        try: res = C.recalcular(a_cadena(r), fut, 1.0, 1, datetime.fromisoformat(r["ts"].replace(" ", "T") + "+00:00"))
        except Exception: continue
        if not res["doms"]: continue
        cerca = [x for x in res["perfil"] if abs(x[1] - fut) <= 100]
        tot = sum(abs(x[2]) for x in cerca) or np.nan; dom = sum(abs(x[2]) for x in res["doms"])
        filas.append(dict(dia=r["ts"][:10], hm=hm, fut=fut, dom_M=dom / 1e6, conc=dom / tot, neto_M=res["net_vol"] / 1e6, dias_venc=res["mas_cerca"],
                          sep=abs(res["doms"][0][1] - res["doms"][1][1]) if len(res["doms"]) > 1 else np.nan, vol_total=sum(float(x[7]) for x in r["filas"])))
d = pd.DataFrame(filas)
g = d.groupby("dia").agg(fotos=("fut", "size"), dom_M=("dom_M", "median"), concentracion=("conc", "median"), neto_M=("neto_M", "median"), dias_al_venc=("dias_venc", "median"),
                          separacion=("sep", "median"), contratos_opc=("vol_total", "max"), rango_rueda=("fut", lambda x: x.max() - x.min()))
print(g.round(2).to_string())
print("\npor hora, 17-09 contra el resto (mediana de la fuerza de las dos verdes, en millones de USD de gamma x volumen por 1 %):")
d["h"] = d["hm"].str[:2]
print(d.assign(grupo=np.where(d["dia"] == "2026-09-17", "17-09", "otros dias")).pivot_table(index="h", columns="grupo", values="dom_M", aggfunc="median").round(1).to_string())
