# -*- coding: utf-8 -*-
"""modelo_es_00_forma.py - SOLO LA FORMA de los datos (ningun desenlace): cuantas velas de 2 min de MES hay por dia, cuantas con niveles, cuantos disparos
modelo·es10 dejo el vivo por dia, y si el precio del disparo coincide con el cierre de la vela que le asigno (control del calce de horas)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import modelo_es_lib as M

v = M.velas_m2(); print("velas M2 del rebobinado: %d | %s -> %s" % (len(v), v.index[0], v.index[-1]))
r = M.en_rueda(v.index); vr = v[r]
g = vr.groupby(vr.index.strftime("%Y-%m-%d")).agg(velas=("c", "size"), con_niv=("con_niv", "sum"), con_zero_vol=("zero_vol", "count"), con_mc30=("mc30", "count"), con_dom0=("dom0", "count"),
                                                   primera=("c", lambda s: s.index[0].strftime("%H:%M")), ultima=("c", lambda s: s.index[-1].strftime("%H:%M")), cmin=("c", "min"), cmax=("c", "max"))
print("\nRUEDA (13:30-20:00 UTC), por dia: una rueda entera son 195 velas de 2 min"); print(g.to_string())

hoy = M.velas_hoy(); com = v.index.intersection(hoy.index)
dc = (v.loc[com, "c"] - hoy.loc[com, "c"]).abs(); dd = (v.loc[com, "delta"] - hoy.loc[com, "delta"]).abs()
print("\nrebobinado contra lo que anoto el vivo: %d velas en comun | cierre distinto en %d | delta distinto (>5) en %d" % (len(com), int((dc > 0).sum()), int((dd > 5).sum())))

for arch in (False, True):
    d = M.disparos_log(arch); print("\nDISPAROS modelo·es10 del registro %s: %d" % ("ARCHIVO (ultimo rebobinado)" if arch else "VIVO", len(d)))
    if not len(d): continue
    d["dia"] = d["ap"].dt.strftime("%Y-%m-%d"); d["hm"] = d["ap"].dt.strftime("%H:%M")
    d["cierre_vela"] = v["c"].reindex(d["ap"]).to_numpy(); d["calza"] = (d["cierre_vela"] - d["precio"]).abs() < 1e-9
    print(d.groupby("dia").agg(n=("lado", "size"), largos=("lado", lambda s: int((s > 0).sum())), cortos=("lado", lambda s: int((s < 0).sum())), desde=("hm", "min"), hasta=("hm", "max"),
                               calzan=("calza", "sum"), p_min=("p", "min"), p_max=("p", "max")).to_string())
    print("no calzan:"); print(d[~d["calza"]][["t", "ap", "lado", "precio", "cierre_vela", "p"]].to_string())
