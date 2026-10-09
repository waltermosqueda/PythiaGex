# -*- coding: utf-8 -*-
"""v41_06_descriptivo.py — descriptivo de las 4 series de QQQ reconstruidas (datos/rayas_v41.pkl) en la ventana congelada y la completa:
cuantas rayas distintas por minuto, cuanto coinciden entre series, distancia de cada raya al cierre de la vela (sombra del precio o nivel),
cuantas veces cambia el strike por noche. SOLO LECTURA. Imprime."""
import os, sys
import numpy as np, pandas as pd
AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
from v41_04_juez import ventanas
SERIES = ("MUROS_QQQ_vol", "MAJORS_QQQ_vol", "MUROS_QQQ_oi", "MAJORS_QQQ_oi")
V = pd.read_csv(os.path.join(AQUI, "datos", "velas_v41.csv"), parse_dates=["t"]).set_index("t")
X = pd.read_pickle(os.path.join(AQUI, "datos", "rayas_v41.pkl"))
for tipo in ("congelada", "completa"):
    W = ventanas(X["meta"], tipo)
    print("\n== %s: %d noches" % (tipo, len(W)))
    mins = [t for a, b, _, _, _ in W for t in pd.date_range(a, b, freq="1min", inclusive="left") if t in V.index]
    for s in SERIES:
        R = X["series"][s]
        dist = []; nr = []; cambios = 0; prev = None; ses_prev = None
        for t in mins:
            d = R.get(t, {})
            ks = tuple(sorted(v[1] for v in d.values()))
            ses = (t + pd.Timedelta(hours=2)).date()
            if ses == ses_prev and prev is not None and ks != prev and ks:
                cambios += 1
            if ks:
                prev = ks
            ses_prev = ses
            c = V.at[t, "o"]
            ps = sorted({round(v[0], 2) for v in d.values()})
            nr.append(len(ps))
            dist += [abs(p - c) for p in ps]
        dist = np.array(dist); nr = np.array(nr)
        print("  %-15s minutos %d | rayas distintas por minuto: 0 %.0f%% 1 %.0f%% 2 %.0f%% | |raya - apertura| mediana %.1f p25 %.1f p75 %.1f | <= 5 pts %.0f%% | cambios de strike %d (%.1f por noche)" % (
            s, len(mins), 100 * (nr == 0).mean(), 100 * (nr == 1).mean(), 100 * (nr == 2).mean(), np.median(dist), np.percentile(dist, 25),
            np.percentile(dist, 75), 100 * (dist <= 5).mean(), cambios, cambios / len(W)))
    # coincidencia entre series (mismo conjunto de strikes en el minuto)
    for i, s1 in enumerate(SERIES):
        for s2 in SERIES[i + 1:]:
            ig = np.mean([set(v[1] for v in X["series"][s1].get(t, {}).values()) == set(v[1] for v in X["series"][s2].get(t, {}).values()) for t in mins])
            print("     mismo(s) strike(s) %s = %s: %.0f %% de los minutos" % (s1, s2, 100 * ig))
