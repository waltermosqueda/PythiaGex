# -*- coding: utf-8 -*-
"""verdes_08 — por cada toque ejecutado de una verde: la FUERZA de esa raya en ese momento (gamma x volumen del strike, millones), la gamma neta del libro,
la velocidad de llegada y la hora. Rebota mas cuando la raya es fuerte / la gamma neta es muy positiva / llega despacio? Con y sin el 17-09."""
import os, sys, json, glob, bisect
from datetime import datetime
import numpy as np, pandas as pd
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI); sys.path.insert(0, os.path.dirname(AQUI))
import base as B, capas_nq as C
from verdes_05_reconstruir import a_cadena, VIVA
GEMELO = "--placebo" in sys.argv   # el mismo calculo sobre las rayas CORRIDAS, con la fuerza de la raya real de la que salieron
R = pd.read_parquet(os.path.join(B.CACHE, "verdes_placebo.parquet" if GEMELO else "verdes_toques.parquet")); T = B.todas_las_sesiones()
fotos = {}
for p in sorted(glob.glob(os.path.join(VIVA, "viva-NQ-*.jsonl"))):
    for l in open(p, encoding="utf-8", errors="replace"):
        l = l.strip()
        if len(l) < 40 or not l.endswith("}"): continue
        try: r = json.loads(l)
        except Exception: continue
        fut = float(r.get("futuro") or 0)
        if fut <= 0: continue
        try: res = C.recalcular(a_cadena(r), fut, 1.0, 1, datetime.fromisoformat(r["ts"].replace(" ", "T") + "+00:00"))
        except Exception: continue
        fotos[pd.Timestamp(r["ts"])] = (res["net_vol"] / 1e6, {round(x[1] / 5) * 5: abs(x[2]) / 1e6 for x in res["perfil"]}, sum(abs(x[2]) for x in res["perfil"] if abs(x[1] - fut) <= 100) / 1e6)
ts = sorted(fotos); filas = []
for _, r in R.iterrows():
    seg = T[r["dia"]]; t = seg.index[int(r["i"])]; j = bisect.bisect_right(ts, t) - 1
    if j < 0 or (t - ts[j]).total_seconds() > 600: continue
    neto, perfil, tot = fotos[ts[j]]; i = int(r["i"]); u = seg["ultimo"].to_numpy(); n = seg["n"].to_numpy()
    filas.append(dict(dia=r["dia"], hora=r["hora"], lado=r["lado"], y=r["r12_6"], y20=r["r20_8"], fuerza=perfil.get(round(r["K"] / 5) * 5, 0.0), neto=neto, parte=perfil.get(round(r["K"] / 5) * 5, 0.0) / tot if tot else np.nan,
                      vel60=abs(u[i] - u[max(0, i - 60)]), vel300=abs(u[i] - u[max(0, i - 300)]), cinta60=n[max(0, i - 60):i].sum(), traspaso=r["traspaso"]))
d = pd.DataFrame(filas); d = d[d["y"] != 0]; d["gana"] = (d["y"] > 0).astype(int)
print("toques ejecutados con foto fresca del libro: %d (%d sin el 17-09)\n" % (len(d), (d["dia"] != "2026-09-17").sum()))
def tabla(col, cortes, etiquetas, datos, titulo):
    g = pd.cut(datos[col], cortes, labels=etiquetas); r = datos.groupby(g, observed=True)["gana"].agg(["mean", "size"])
    print(titulo + ": " + " | ".join("%s %.0f %% (n %d)" % (k, 100 * v["mean"], v["size"]) for k, v in r.iterrows()))
for nombre, datos in (("TODOS", d), ("SIN EL 17-09", d[d["dia"] != "2026-09-17"])):
    print("== %s (rebote +12 antes que -6, orden limitada en la raya; azar puro 33 %%)" % nombre)
    tabla("fuerza", [-1, 500, 2000, 8000, 1e9], ["raya debil <0,5B", "0,5-2B", "2-8B", "fuerte >8B"], datos, "  fuerza de ESA raya")
    tabla("neto", [-1e9, 0, 3000, 10000, 1e9], ["gamma neta negativa", "0-3B", "3-10B", ">10B"], datos, "  gamma neta del libro NQ")
    tabla("vel60", [-1, 6, 12, 20, 1e9], ["llega lento <6 pts/min", "6-12", "12-20", "rapido >20"], datos, "  velocidad de llegada (60 s)")
    tabla("cinta60", [-1, 300, 600, 1200, 1e9], ["cinta fria <300 ord/min", "300-600", "600-1200", "caliente >1200"], datos, "  cinta (ordenes en 60 s)")
    print()
