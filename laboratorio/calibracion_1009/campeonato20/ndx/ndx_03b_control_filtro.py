# -*- coding: utf-8 -*-
"""ndx_03b_control_filtro.py — control del filtro por tipo de llegada de ndx_03_juez.py (sin mirar aciertos: solo CONTEOS y la mecanica).
Con MUROS_NDX_vol en las noches de MIRAR y 5 juegos al azar:
  (1) sin filtro, el resultado es identico al de juez20.evaluar llamado directo (mismas llegadas, mismo placebo);
  (2) techos + pisos filtrados = llegadas sin filtro, en el real y en cada corrida;
  (3) juez20._core queda restaurado despues del filtro.
Imprime solo OK/FALLA y conteos. Uso: python -I ndx_03b_control_filtro.py"""
import ctypes
import os
import sys

try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass
import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import ndx_03_juez as N3  # noqa: E402

J20 = N3.J20


def main():
    V = pd.read_csv(os.path.join(AQUI, "datos", "velas_ndx.csv"), parse_dates=["t"])[["t", "o", "h", "l", "c"]]
    series, M = N3.cargar_series()
    g = N3.sesiones(V, M, "noche")
    mirar, _ = J20.particion_noches(sorted(g.index[g["entra"]]))
    ven = N3.ventanas_de(mirar, "noche")
    ray = series["MUROS_NDX_vol"]
    ops = {"desfase_min": 0, "claves": ("D1", "D2")}
    prep = J20.preparar(V, ray, ven, ops)
    orig = J20._core
    a = J20.evaluar(V, ray, ventanas=ven, opciones=ops, n_azar=5, _prep=prep)
    with N3.filtro_tipo(None):
        b = J20.evaluar(V, ray, ventanas=ven, opciones=ops, n_azar=5, _prep=prep)
    ok1 = a["metricas"]["llegadas"] == b["metricas"]["llegadas"] and \
        a["placebo"]["azar"]["llegadas"]["media"] == b["placebo"]["azar"]["llegadas"]["media"]
    res = {}
    for tp in ("techo", "piso"):
        with N3.filtro_tipo(tp):
            res[tp] = J20.evaluar(V, ray, ventanas=ven, opciones=ops, n_azar=5, _prep=prep)
    ok2 = res["techo"]["metricas"]["llegadas"] + res["piso"]["metricas"]["llegadas"] == a["metricas"]["llegadas"]
    ok2b = all(res["techo"]["placebo"]["corridos"][d]["llegadas"] + res["piso"]["placebo"]["corridos"][d]["llegadas"]
               == a["placebo"]["corridos"][d]["llegadas"] for d in a["placebo"]["corridos"])
    ok2c = abs(res["techo"]["placebo"]["azar"]["llegadas"]["media"] + res["piso"]["placebo"]["azar"]["llegadas"]["media"]
               - a["placebo"]["azar"]["llegadas"]["media"]) < 1e-9
    ok3 = J20._core is orig
    print("noches mirar %d; llegadas %d = techos %d + pisos %d" % (len(mirar), a["metricas"]["llegadas"], res["techo"]["metricas"]["llegadas"],
                                                                  res["piso"]["metricas"]["llegadas"]))
    print("(1) sin filtro = evaluar directo: %s" % ("OK" if ok1 else "FALLA"))
    print("(2) techos + pisos = total: real %s, corridas %s, azar (media de 5 juegos) %s" % (
        "OK" if ok2 else "FALLA", "OK" if ok2b else "FALLA", "OK" if ok2c else "FALLA"))
    print("(3) _core restaurado: %s" % ("OK" if ok3 else "FALLA"))
    print("tipos en los eventos filtrados: techo %s, piso %s" % (sorted({e["tipo"] for e in res["techo"]["eventos"]}),
                                                                 sorted({e["tipo"] for e in res["piso"]["eventos"]})))


if __name__ == "__main__":
    main()
