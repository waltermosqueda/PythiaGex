# -*- coding: utf-8 -*-
"""
ubicacion_05_posthoc.py — POST-HOC y DESCRIPTIVO (corrido despues de confirmar; NO es un finalista ni cambia el veredicto).
De 20 niveles mirados, el unico cuyo rebote se parecio entre explorar y confirmar fue la banda VWAP -1 desvio (59,0 % y 56,9 %).
Con 20 niveles, que uno salga asi por azar es esperable. Aca se lo mira por lado de llegada, por dia y contra el placebo del protocolo,
para dejarle al coordinador la regla exacta por si quiere gastarle la reserva. Tambien se mira su gemela de arriba (VWAP +1), que deberia
dar lo mismo si el efecto fuera real y simetrico.
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B, ubicacion_lib as UL
import numpy as np, pandas as pd

T = B.todas_las_sesiones(); S = sorted(T); ev = UL.pegar_resultados(UL.eventos(T, S), T)
for nivel in ("VWAPb1", "VWAPa1", "VWAPb2", "VWAPa2", "VWAP"):
    g = ev[ev["niveles"].str.split("+").apply(lambda x: nivel in x)].copy(); g["lado"] = -g["dir"]; g = UL.desagrupar(g)
    print("\n==== %s -> rebote" % nivel)
    for tramo, m in (("explorar", g["ses"] < B.CORTE_CONFIRMAR), ("confirmar", g["ses"] >= B.CORTE_CONFIRMAR), ("las 20", g["ses"] > "")):
        e = g[m]; j = UL.juzgar_variante(e, 8, nivel); Tt = {s: T[s] for s in e["ses"].unique()}; pm, ps, _ = UL.placebo_protocolo(Tt, e, (8, 600))
        print("  %-9s n %3d | acierto %.1f %% | dias arriba %s | t dias %+.2f | placebo %.1f +- %.1f -> z %.2f" % (tramo, j["n"], j["acierto"], j["dias_arriba"], UL.t_dias(e, 8), 100 * pm, 100 * ps, (j["acierto"] / 100 - pm) / ps))
    for d, nom in ((-1, "llega desde ARRIBA (rebote = compra)"), (1, "llega desde ABAJO (rebote = venta)")):
        e = g[g["dir"] == d]; j = UL.juzgar_variante(e, 8, nivel); print("  %-38s n %3d | acierto %.1f %% | dias %s" % (nom, j.get("n", 0), j.get("acierto", np.nan), j.get("dias_arriba")))
