# -*- coding: utf-8 -*-
"""EQUIVALENCIA DE LA PORTACION: la p del modelo calculada en C# (Rebobina --modelo, el mismo
GatilloModelo.cs del indicador) contra la p de Python (los rasgos del laboratorio + el JSON
exportado), vela por vela. Si difieren, el gatillo del grafico no es el que se midio.

Uso: python laboratorio/modelo_equivalencia.py MES <modelo_p_cs.csv>
"""
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gatillo_cientifico import cargar, rasgos  # noqa: E402


def main():
    inst, csv = sys.argv[1], sys.argv[2]
    m = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "modelo_%s_10min.json" % inst)))
    vs = cargar(inst); rasgos(vs)
    py = {}
    for v in vs:
        rt = v["rt"] or 0.25
        def dist(x, sin):
            return (x - v["c"]) / rt if x else sin
        f = [v["ret15"] / rt, dist(v["zero"], 0.0), v["rango"] / rt, dist(v["mn"], -40.0), dist(v["dom_arr"], 40.0),
             v["cum15"] / (abs(v["cum15"]) + 500.0), dist(v["mc30"], 0.0), v["dz"], dist(v["mp"], 40.0), dist(v["dom_aba"], -40.0)]
        z = [(fi - mu) / sc for fi, mu, sc in zip(f, m["media"], m["escala"])]
        logit = m["intercepto"] + sum(c * zi for c, zi in zip(m["coef"], z))
        py[v["t"].strftime("%Y-%m-%dT%H:%M:%S")] = 1.0 / (1.0 + math.exp(-logit))
    cs = {}
    for l in open(csv):
        p = l.strip().split(",")
        if len(p) == 3 and p[1] and p[0] != "t":
            cs[p[0]] = float(p[1])
    comunes = [t for t in cs if t in py]
    d = np.array([cs[t] - py[t] for t in comunes])
    print("velas comparadas: %d | diferencia |C# - Python|: mediana %.4f, p90 %.4f, maxima %.4f" % (len(comunes), np.median(abs(d)), np.percentile(abs(d), 90), abs(d).max()))
    umb = 0.70
    acuerdo = sum(1 for t in comunes if (cs[t] >= umb) == (py[t] >= umb) and (cs[t] <= 1 - umb) == (py[t] <= 1 - umb))
    print("mismo veredicto (p>=%.2f o <=%.2f) en %d de %d velas (%.2f %%)" % (umb, 1 - umb, acuerdo, len(comunes), 100.0 * acuerdo / len(comunes)))
    peores = sorted(comunes, key=lambda t: -abs(cs[t] - py[t]))[:5]
    print("las 5 peores:", ", ".join("%s C# %.3f Py %.3f" % (t[5:16], cs[t], py[t]) for t in peores))


if __name__ == "__main__":
    main()
