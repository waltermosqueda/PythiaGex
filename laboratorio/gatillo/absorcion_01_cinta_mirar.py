# -*- coding: utf-8 -*-
"""absorcion_01_cinta_mirar.py — mira la cinta cruda de UNA sesion de explorar para entender como se ve una reposicion (sin mirar resultados)."""
import sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, os, time
t0 = time.time()
PQ = os.path.join(B.CACHE, "cinta.parquet"); ses = sys.argv[1] if len(sys.argv) > 1 else "2026-08-24"
d = pd.read_parquet(PQ, filters=[("sesion", "==", ses)]); print("leida", ses, len(d), "filas en %.1f s" % (time.time() - t0))
g = d.groupby("px_grupo").size(); print("px_grupo:", g.to_dict())
d = d[d["rueda"]].sort_values("t", kind="stable").reset_index(drop=True); print("rueda:", len(d))
vol = d["vol"].to_numpy(); lado = d["lado"].to_numpy()
venta = lado == -1
agota_b = venta & (vol >= d["abidv"]); absb = agota_b & (d["dbid"] >= d["abid"]); romp = agota_b & (d["dbid"] < d["abid"])
print("ventas: %d | agotan el bid: %d (%.1f %%) | de esas, el bid aguanta: %d (%.2f %%)" % (venta.sum(), agota_b.sum(), 100 * agota_b.sum() / venta.sum(), absb.sum(), 100 * absb.sum() / agota_b.sum()))
print("ventas que NO agotan el bid: vol medio %.2f ; que agotan y rompe: %.2f ; que agotan y aguanta: %.2f" % (vol[venta & ~agota_b].mean(), vol[romp].mean(), vol[absb].mean()))
a = d[absb]
print("abs_bid: dbid==abid %.1f %% ; dbid>abid %.1f %%" % (100 * (a["dbid"] == a["abid"]).mean(), 100 * (a["dbid"] > a["abid"]).mean()))
print("abs_bid: tamano visible antes (abidv) cuantiles:", a["abidv"].quantile([.5, .9, .99]).to_dict(), " despues (dbidv):", a["dbidv"].quantile([.5, .9, .99]).to_dict())
print("abs_bid: vol de la orden cuantiles:", a["vol"].quantile([.5, .9, .99]).to_dict(), " vol/abidv:", (a["vol"] / a["abidv"].clip(lower=1)).quantile([.5, .9, .99]).round(2).to_dict())
print("spread antes en abs_bid:", (a["aask"] - a["abid"]).value_counts().head(5).to_dict())
print("spread antes en general:", (d["aask"] - d["abid"]).value_counts().head(5).to_dict())
# la orden siguiente a una abs_bid: a que precio pega y en cuanto tiempo
dt = d["t"].diff().dt.total_seconds()
print("tiempo entre ordenes (s) mediana %.4f p90 %.3f" % (dt.median(), dt.quantile(.9)))
print(d.loc[absb].head(15)[["t", "primero", "ultimo", "vol", "lado", "prints", "abid", "abidv", "aask", "aaskv", "dbid", "dbidv", "dask", "daskv"]].to_string())
# un ejemplo: 12 ordenes alrededor de la abs_bid mas grande
i = int(a["vol"].idxmax()); print("\nalrededor de la abs_bid mas grande (fila %d):" % i)
print(d.iloc[max(0, i - 8): i + 8][["t", "primero", "ultimo", "vol", "lado", "abid", "abidv", "aask", "aaskv", "dbid", "dbidv", "dask", "daskv"]].to_string())
# "plantadas": periodos en los que el mejor bid se queda en el mismo precio P; cuanto se vendio contra P y cuanto se mostro
print("total %.1f s" % (time.time() - t0))
