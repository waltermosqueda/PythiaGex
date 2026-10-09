# -*- coding: utf-8 -*-
"""a02_placebo_sin_noches.py — AUDITOR. SOLO LECTURA. El placebo oficial del juez (500 juegos 'pool', semilla 20261009, corridas)
para la 2.0 dibujada (REF_2_0: dom0, dom1, ndx_dom0, ndx_dom1) en la ventana CONGELADA: con las 19 noches, sin 10-09 (la noche
que motivo el estudio, parcial hasta 03:22Z) y sin 10-09 ni 10-05 (las dos noches mas altas). Mide si 'la 2.0 aguanta un poco
mas que el azar' depende de 1-2 noches. Correr: python -I a02_placebo_sin_noches.py"""
import os
import sys

import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(AQUI), "juez"))
import juez_operador as J  # noqa: E402

CAL = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "calibrar")
MET = ("llegadas", "pct_sostenidos", "pct_exactos", "precision_mediana_abs", "recorrido_medio", "pct_llega_opuesta", "cobertura_pct")


def ventanas(excl=()):
    out = []
    for n in pd.read_pickle(os.path.join(CAL, "noches.pkl")):
        if n["N"] in excl:
            continue
        D = pd.Timestamp(n["D"]); N = pd.Timestamp(n["N"])
        a = N - pd.Timedelta(hours=2) if D.weekday() == 4 else N + pd.Timedelta(minutes=35)
        out.append((a, N + pd.Timedelta(hours=8, minutes=30)))
    return out


def main():
    V = pd.read_csv(os.path.join(CAL, "velas_m1.csv"), parse_dates=["t"])
    niv = pd.read_pickle(os.path.join(CAL, "niv20_m1.pkl"))
    R = J.rayas_desde_niv20(niv, ("dom0", "dom1", "ndx_dom0", "ndx_dom1"))
    for excl in ((), ("2026-10-09",), ("2026-10-09", "2026-10-05")):
        op = {"ventanas": ventanas(excl)}
        prep = J.preparar(V, R, op)
        for modo_az in ("pool", "propia"):
            pl = J.placebo(V, R, op, n_azar=500, azar_distancia=modo_az, n_boot=2000 if modo_az == "pool" else 0, _prep=prep)
            print("\n== sin %s | azar '%s' | noches %d" % ("+".join(excl) or "(ninguna)", modo_az, len(op["ventanas"])))
            for m in MET:
                a = pl["azar"][m]
                print("   %-22s real %7.2f | azar p50 %7.2f p95 %7.2f | percentil %5.1f" % (m, a["real"], a["p50"], a["p95"], a["percentil"]))
            if modo_az == "pool":
                cj = pl["corridos_juntos"]
                print("   corridas juntas: sost %.1f exactos %.1f prec %.2f rec %.1f opuesta %.1f cobertura %.2f" % (
                    cj["pct_sostenidos"], cj["pct_exactos"], cj["precision_mediana_abs"], cj["recorrido_medio"], cj["pct_llega_opuesta"],
                    cj["cobertura_pct"]))
                b = pl["bootstrap_ic90"]
                print("   IC90 real: sost %.1f-%.1f  opuesta %.1f-%.1f" % (b["pct_sostenidos"] + b["pct_llega_opuesta"]))


if __name__ == "__main__":
    main()
