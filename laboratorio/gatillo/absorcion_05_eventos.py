# -*- coding: utf-8 -*-
"""absorcion_05_eventos.py — lista de TODAS las absorciones reales de la cinta (una fila por orden absorbida), por sesion, a absorcion_cache/.
Sin resultados: solo el evento (hora, punta, precio, contratos, tamano visible antes y despues, spread antes)."""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd, base as B, absorcion_base as A
T = A.tablas(); os.makedirs(A.MI_CACHE, exist_ok=True)
for s in T:
    f = os.path.join(A.MI_CACHE, "eventos-%s.parquet" % s)
    if os.path.exists(f): continue
    d = A.cinta_sesion(s); vol = d["vol"].to_numpy(); lado = d["lado"].to_numpy()
    absb = (lado == -1) & (vol >= d["abidv"]) & (d["dbid"] >= d["abid"]); absa = (lado == 1) & (vol >= d["aaskv"]) & (d["dask"] <= d["aask"])
    b = d[absb]; a = d[absa]
    ev = pd.concat([pd.DataFrame({"t": b["t"], "punta": 1, "px": b["ultimo"], "vol": b["vol"], "antes": b["abidv"], "despues": b["dbidv"], "spread": b["aask"] - b["abid"], "prints": b["prints"]}),
                    pd.DataFrame({"t": a["t"], "punta": -1, "px": a["ultimo"], "vol": a["vol"], "antes": a["aaskv"], "despues": a["daskv"], "spread": a["aask"] - a["abid"], "prints": a["prints"]})]).sort_values("t", kind="stable")
    ev.to_parquet(f); print(s, len(ev))
E = B.explorar(T)
ev = pd.concat([pd.read_parquet(os.path.join(A.MI_CACHE, "eventos-%s.parquet" % s)).assign(sesion=s) for s in E]); hm = ev["t"].dt.strftime("%H:%M"); ev = ev[(hm >= "13:32") & (hm < "20:00")]
print("\nabsorciones en rueda (explorar):", len(ev), "=", round(len(ev) / 12), "por sesion")
print("vol de la orden: p50/p90/p95/p99", ev["vol"].quantile([.5, .9, .95, .99]).to_dict())
for k in (5, 8, 10, 15, 20): print("  vol >= %2d: %6.1f por sesion" % (k, (ev["vol"] >= k).sum() / 12))
print("tamano antes: p50/p90/p99", ev["antes"].quantile([.5, .9, .99]).to_dict(), "| despues:", ev["despues"].quantile([.5, .9, .99]).to_dict())
for k in (2, 3, 4, 5): print("  antes >= %d y despues >= antes: %6.1f por sesion | y ademas vol >= 5: %6.1f" % (k, ((ev["antes"] >= k) & (ev["despues"] >= ev["antes"])).sum() / 12, ((ev["antes"] >= k) & (ev["despues"] >= ev["antes"]) & (ev["vol"] >= 5)).sum() / 12))
print("despues == 0 (la punta quedo vacia en ese precio?):", round(100 * (ev["despues"] == 0).mean(), 1), "%")
print("spread antes:", ev["spread"].value_counts(normalize=True).head(4).round(3).to_dict())
