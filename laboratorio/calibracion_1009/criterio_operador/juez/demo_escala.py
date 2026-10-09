# -*- coding: utf-8 -*-
"""demo_escala.py — prueba de ESCALA del juez (tiempo y que el placebo corra de punta a punta) con las dominantes que dibujo la
2.0 (dom0, dom1 + ndx_dom0, ndx_dom1 donde existan) en las ventanas CONGELADAS de las noches de noches.pkl (las mismas de
c03_reacciones.py: D+1 00:35Z -> 08:30Z; los viernes domingo 22:00Z -> lunes 08:30Z). Los numeros que imprime son una primera
lectura, no el estudio (ese lo arma quien compare conjuntos). SOLO LECTURA. Correr: python -I demo_escala.py [n_azar]"""
import os
import sys
import time

import pandas as pd

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import juez_operador as J  # noqa: E402

CAL = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "calibrar")


def ventanas():
    out = []
    for n in pd.read_pickle(os.path.join(CAL, "noches.pkl")):
        D = pd.Timestamp(n["D"]); N = pd.Timestamp(n["N"])
        a = N - pd.Timedelta(hours=2) if D.weekday() == 4 else N + pd.Timedelta(minutes=35)
        out.append((a, N + pd.Timedelta(hours=8, minutes=30)))
    return out


def main():
    n_azar = int(sys.argv[1]) if len(sys.argv) > 1 else 500
    V = pd.read_csv(os.path.join(CAL, "velas_m1.csv"), parse_dates=["t"])
    niv = pd.read_pickle(os.path.join(CAL, "niv20_m1.pkl"))
    R = J.rayas_desde_niv20(niv, ("dom0", "dom1", "ndx_dom0", "ndx_dom1"))
    op = {"ventanas": ventanas()}
    t0 = time.time()
    prep = J.preparar(V, R, op)
    t1 = time.time()
    res = J.juzgar(V, R, op, _prep=prep)
    t2 = time.time()
    print("velas %d, pistas %d | preparar %.1f s, juzgar %.2f s" % (prep[0]["n"], len(prep[1]["pistas"]), t1 - t0, t2 - t1))
    print("ventanas: %d noches" % len(op["ventanas"]))
    sens = J.sensibilidad(V, R, op)
    for m, x in sens.items():
        print("  %-13s llegadas %4d base %4d sostenidos %4d (%.1f %%) falso %3d rotas %4d indef %3d exactos %4d (%.1f %%) | prec med |%.2f| | "
              "recorrido medio %.1f med %.1f tramo %.1f | llega opuesta %.0f %% (%d) | noches %d niveles %d" % (
                  m, x["llegadas"], x["base"], x["sostenidos"], x["pct_sostenidos"], x["falsos_rompimientos"], x["rotas"], x["indefinidas"],
                  x["exactos"], x["pct_exactos"], x["precision_mediana_abs"], x["recorrido_medio"], x["recorrido_mediano"],
                  x["recorrido_tramo_medio"], x["pct_llega_opuesta"], x["con_opuesta"], x["n_noches"], x["n_niveles"]))
    t3 = time.time()
    pl = J.placebo(V, R, op, n_azar=n_azar, _prep=prep)
    t4 = time.time()
    print("\nPLACEBO TOLERANTE (%d juegos al azar, 'pool'): %.1f s" % (n_azar, t4 - t3))
    for m, a in pl["azar"].items():
        print("  %-28s real %8.2f | azar p5 %8.2f p50 %8.2f p95 %8.2f | percentil %5.1f" % (m, a["real"], a["p5"], a["p50"], a["p95"], a["percentil"]))
    print("  corridas +-11/19/31 juntas: sostenidos %.1f %% exactos %.1f %% recorrido medio %.1f cobertura %.1f %%" % (
        pl["corridos_juntos"]["pct_sostenidos"], pl["corridos_juntos"]["pct_exactos"], pl["corridos_juntos"]["recorrido_medio"],
        pl["corridos_juntos"]["cobertura_pct"]))
    for d, x in pl["corridos"].items():
        print("     %+3d: llegadas %4d sostenidos %.1f %% exactos %.1f %% cobertura %.1f %%" % (d, x["llegadas"], x["pct_sostenidos"], x["pct_exactos"],
                                                                                         x["cobertura_pct"]))
    print("  IC90 bootstrap por noche del real:")
    for k, v in pl["bootstrap_ic90"].items():
        print("     %-28s %8.2f - %8.2f" % (k, v[0], v[1]))
    print("  cobertura real: %.1f %% de %d giros" % (pl["real"]["cobertura_pct"], pl["real"]["cobertura_n"]))


if __name__ == "__main__":
    main()
