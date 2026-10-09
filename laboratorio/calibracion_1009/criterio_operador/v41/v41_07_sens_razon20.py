# -*- coding: utf-8 -*-
"""v41_07_sens_razon20.py — SENSIBILIDAD (fuera de lo pedido como principal): las mismas 4 series de QQQ de la 4.1 en la ventana CONGELADA,
pero con la raya llevada a NQ con la razon de la 2.0 (MNQ de la vela 16:14 NY / spot de QQQ congelado; noches.pkl, campo r20) en vez de la
C8 de la 4.1. Aproximacion: se conserva el strike que eligio la 4.1 y solo se reescala el precio (precio x r20 / razon_4.1); el cambio de S
y de la ventana de +-100 pts que moveria algun argmax NO se rehace. Motivo: esta noche los techos de 02:03-03:21 quedaron en 31083-31089 y
la raya de QQQ750 de la 4.1 (31076,37) quedo 7 pts abajo; la de la 2.0 (31083,50) encima. SOLO LECTURA. Imprime y escribe
datos/sens_razon20.txt. Uso: python -I v41_07_sens_razon20.py [n_azar]"""
import os
import sys

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(AQUI), "juez"))
sys.path.insert(0, AQUI)
import juez_operador as J  # noqa: E402
from v41_04_juez import ventanas, SERIES  # noqa: E402

CAL = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "calibrar")


def main():
    n_azar = int(sys.argv[1]) if len(sys.argv) > 1 else 200
    V = pd.read_csv(os.path.join(AQUI, "datos", "velas_v41.csv"), parse_dates=["t"])
    X = pd.read_pickle(os.path.join(AQUI, "datos", "rayas_v41.pkl"))
    M = X["meta"].set_index("t")
    r20 = {n["N"]: n["r20"] for n in pd.read_pickle(os.path.join(CAL, "noches.pkl"))}
    W = [w for w in ventanas(X["meta"], "congelada") if w[2] in r20 and r20[w[2]] == r20[w[2]]]
    lin = ["ventana congelada, %d noches con r20; dif (r20 - razon 4.1) x 750 por noche: %s" % (
        len(W), ", ".join("%s %+.1f" % (s[5:], 750 * (r20[s] - M.loc[a + pd.Timedelta(minutes=30), "rz"])) for a, b, s, _, _ in W))]
    for nom in SERIES:
        R0 = X["series"][nom]; R1 = {}
        for a, b, s, _, _ in W:
            for t in pd.date_range(a, b, freq="1min", inclusive="left"):
                d = R0.get(t, {})
                rz = M.loc[t, "rz"] if t in M.index else np.nan
                R1[t] = {k: (v[0] * r20[s] / rz, v[1]) for k, v in d.items()} if rz == rz else {}
        op = {"ventanas": [(a, b) for a, b, _, _, _ in W], "desfase_min": 0}
        for etiqueta, R in (("razon 4.1", {t: R0.get(t, {}) for t in R1}), ("razon 2.0", R1)):
            prep = J.preparar(V, R, op)
            pl = J.placebo(V, R, op, n_azar=n_azar, n_boot=0, _prep=prep)
            x = pl["real"]; az = pl["azar"]
            sens = {m: J.resumir(J._juzgar_core(prep[0], prep[1], J._op(dict(op, modo=m))), J._noches_ventana(prep[0])) for m in ("estricto", "muy_tolerante")}
            lin.append("%-15s %-9s llegadas %3d sost %5.1f %% (pctil %4.1f) exactos %5.1f %% (pctil %4.1f) prec med |%.2f| (pctil %4.1f) rec medio %5.1f "
                       "(pctil %4.1f) cobertura %.2f %% (pctil %4.1f) | estricto %.1f %% muy tol %.1f %%" % (
                           nom, etiqueta, x["llegadas"], x["pct_sostenidos"], az["pct_sostenidos"]["percentil"], x["pct_exactos"],
                           az["pct_exactos"]["percentil"], x["precision_mediana_abs"], az["precision_mediana_abs"]["percentil"], x["recorrido_medio"],
                           az["recorrido_medio"]["percentil"], x["cobertura_pct"], az["cobertura_pct"]["percentil"],
                           sens["estricto"]["pct_sostenidos"], sens["muy_tolerante"]["pct_sostenidos"]))
            print(lin[-1], flush=True)
    open(os.path.join(AQUI, "datos", "sens_razon20.txt"), "w", encoding="utf-8").write("\n".join(lin) + "\n")
    print(lin[0])


if __name__ == "__main__":
    main()
