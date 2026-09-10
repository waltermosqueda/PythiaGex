# -*- coding: utf-8 -*-
"""¿HAY ALGO PREDECIBLE A 30 MINUTOS CON LO QUE TENEMOS? Prueba de techo con aprendizaje
automatico honesto: regresion logistica (y un bosque) sobre TODOS los rasgos por minuto
(order flow del futuro + estructura de gamma), validacion por bloques de DIAS (nunca se
entrena y se prueba en el mismo dia), y la medida es el AUC fuera de muestra para predecir si
el precio toca +G antes que -G en los 30 min siguientes.

Si el AUC fuera de muestra es ~0,50, no hay señal direccional a ese horizonte en estos datos
y ningun gatillo armado a mano la va a encontrar. Si es > 0,55 de forma estable, vale la pena
buscar la regla. Es la pregunta previa a cualquier "gatillo que acierte".

Uso: python laboratorio/ml_check.py MNQ [--g 25] [--h 30]
"""
import math
import os
import sys

import numpy as np
from sklearn.ensemble import RandomForestClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import roc_auc_score
from sklearn.model_selection import GroupKFold
from sklearn.preprocessing import StandardScaler
from sklearn.pipeline import make_pipeline

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gatillo_cientifico import cargar, rasgos, desenlace  # noqa: E402


def main():
    inst = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else "MNQ"
    arg = lambda k, d: type(d)(sys.argv[sys.argv.index(k) + 1]) if k in sys.argv else d
    es_nq = inst.startswith("MNQ") or inst.startswith("NQ")
    g = arg("--g", 25.0 if es_nq else 6.0); h = arg("--h", 30)
    vs = cargar(inst); rasgos(vs)
    X, y, grupos, filas = [], [], [], []
    for i in range(20, len(vs) - h - 1):
        v = vs[i]
        if vs[i - 1]["dia"] != v["dia"]:
            continue
        r, _, _ = desenlace(vs, i, 1, g, g, h)          # True: +G primero (sube); False: -G primero (baja)
        if r is None:
            continue
        rt = v["rt"] or 0.25
        def dist(x):
            return (x - v["c"]) / rt if x else 0.0
        f = [v["dz"], v["vz"], v["cum15"] / (abs(v["cum15"]) + 500.0), v["ret15"] / rt, 1.0 if v["tren"] else 0.0,
             1.0 if v["conv_neg"] else 0.0, 1.0 if v["mucho"] else 0.0,
             dist(v["dom_arr"]) if v["dom_arr"] else 40.0, dist(v["dom_aba"]) if v["dom_aba"] else -40.0,
             dist(v["zero"]), dist(v["mp"]) if v["mp"] else 40.0, dist(v["mn"]) if v["mn"] else -40.0, dist(v["pico"]),
             dist(v["mc30"]) if v["mc30"] else 0.0, dist(v["mc5"]) if v["mc5"] else 0.0,
             (v["t"].hour * 60 + v["t"].minute - 810) / 390.0, v["rango"] / rt, np.sign(v["delta"]) * min(3.0, abs(v["dz"]))]
        X.append(f); y.append(1 if r else 0); grupos.append(v["dia"])
    X = np.array(X); y = np.array(y); grupos = np.array(grupos)
    print("%s: %d minutos con desenlace (+%g antes que -%g en %d min), %d dias | sube %.1f %%" % (inst, len(y), g, g, h, len(set(grupos)), 100.0 * y.mean()))
    modelos = [("logistica (L2)", make_pipeline(StandardScaler(), LogisticRegression(C=0.3, max_iter=2000))),
               ("bosque (200 arboles, hojas >= 200)", RandomForestClassifier(n_estimators=200, min_samples_leaf=200, random_state=7, n_jobs=-1))]
    gkf = GroupKFold(n_splits=min(5, len(set(grupos))))
    for nombre, m in modelos:
        aucs, accs = [], []
        for tr, te in gkf.split(X, y, grupos):
            m.fit(X[tr], y[tr]); p = m.predict_proba(X[te])[:, 1]
            aucs.append(roc_auc_score(y[te], p))
            # decision solo cuando el modelo esta seguro (p fuera de 0,4-0,6): acierto y cuantos
            seg = (p > 0.6) | (p < 0.4)
            if seg.sum() > 20:
                accs.append(((p[seg] > 0.5) == (y[te][seg] == 1)).mean())
        print("  %-36s AUC fuera de muestra por bloque de dias: %s | media %.3f | acierto cuando esta seguro (p<0,4 o >0,6): %s" % (
            nombre, " ".join("%.3f" % a for a in aucs), float(np.mean(aucs)), " ".join("%.0f%%" % (100 * a) for a in accs) if accs else "nunca seguro"))
    print()
    print("COMO LEERLO: AUC 0,50 = moneda. Por debajo de 0,55 estable no hay regla que valga; con esto el mejor gatillo")
    print("hecho a mano no puede hacer mas que el techo que marca el modelo con todos los rasgos juntos.")


if __name__ == "__main__":
    main()
