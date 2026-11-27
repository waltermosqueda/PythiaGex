# -*- coding: utf-8 -*-
r"""RONDA 20, paso 9 (C5, post hoc, despues de leer el baul de la R21): el GEX a las 9:59 contra la llave de la ronda 21
(rrn = rango hasta ahora / rango esperado a esa hora; TENDENCIA si rrn >= 1,6). Solo lectura de scratchpad\r21\estados_minuto.parquet.
Pregunta: con gamma negativa al abrir, la rueda llega mas seguido (y antes) al estado TENDENCIA despues de las 10:00?
Uso: python -I r20_09_cruce_r21.py"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r20_comun as C

E = pd.read_parquet(os.path.join(C.SCR_BASE, "r21", "estados_minuto.parquet"), columns=["dia", "m", "rrn"])
D = pd.read_csv(os.path.join(C.AQUI, "r20_03_ruedas.csv"))[["dia", "muestra", "gex_neto_0959", "e", "rango_rel", "ruptura_cierre_X"]]
dias21 = set(E.dia)
d = E[E.m >= 30]
llega = d.groupby("dia").rrn.apply(lambda s: bool((s >= 1.6).any()))
primer = d[d.rrn >= 1.6].groupby("dia").m.min()
x = D[D.dia.isin(dias21) & D.gex_neto_0959.notna()].copy()
x["llega"] = x.dia.map(llega).astype(float); x["primer_min"] = x.dia.map(primer)
x["neg"] = (x.gex_neto_0959 < 0).astype(int)
for per in ["diseno", "confirma", "guardada", "juntas"]:
    h = x if per == "juntas" else x[x.muestra == per]
    s = "  %-9s" % per
    for v, nom in ((1, "NEG"), (0, "POS")):
        k = h[h.neg == v]
        if len(k) == 0:
            s += " | %s 0 ruedas" % nom; continue
        s += " | %s %2d ruedas: llega a TENDENCIA %2d (%3.0f %%), primer minuto mediano %s; de esas, ruptura al cierre %+.2f X" % (
            nom, len(k), int(k.llega.sum()), 100 * k.llega.mean(), ("%.0f" % k.primer_min.median()) if k.llega.sum() else "-",
            k[k.llega == 1].ruptura_cierre_X.mean() if k.llega.sum() else np.nan)
    print(s)
