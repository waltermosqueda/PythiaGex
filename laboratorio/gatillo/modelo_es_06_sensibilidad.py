# -*- coding: utf-8 -*-
"""modelo_es_06_sensibilidad.py - SENSIBILIDAD (no cambia el veredicto): la p del modelo M2 congelado fuera de muestra (11-09 al 17-09), calculada de dos maneras:
  'indicador'   = como GatilloModelo.cs (ventana movil de 60 velas corrida entera, incluye la noche)   [la del juicio]
  'laboratorio' = como ml_check.py (solo velas de rueda, ventana dentro del dia: asi se AJUSTO el modelo)
AUC por dia y total, y acierto de las velas con p extrema contra la tasa base. Si las dos dan lo mismo, el resultado no depende del detalle de la ventana."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
from sklearn.metrics import roc_auc_score
import modelo_es_lib as M

D, E = M.corridas(); OOS = [d for d in sorted(E) if d >= M.CORTE_OOS]
partes = []
for d in OOS:
    w = D[D["corrida"] == E[d]["ultima"]].set_index("ap").sort_index(); y = M.desenlaces(w)
    p_ind = M.p_congelada(w)["p"]
    r = w[(w.index.strftime("%Y-%m-%d") == d) & M.en_rueda(w.index) & (w["vol"] > 0)]      # laboratorio: solo la rueda de ese dia, ventana que arranca vacia
    p_lab = M.p_congelada(r)["p"]
    x = pd.DataFrame({"p_ind": p_ind.reindex(r.index), "p_lab": p_lab, "y": y["y"].reindex(r.index), "completo": y["completo"].reindex(r.index)}); x["dia"] = d
    partes.append(x[(x.index.strftime("%H:%M") >= "13:32") & x["completo"]])
X = pd.concat(partes); R = X[X["y"] != 0]
print("velas de rueda con desenlace: %d resueltas de %d | correlacion p_indicador ~ p_laboratorio: %.3f" % (len(R), len(X), X[["p_ind", "p_lab"]].corr().iloc[0, 1]))
for col in ("p_ind", "p_lab"):
    print("\n%s: AUC total %.3f | por dia: %s" % (col, roc_auc_score(R["y"] == 1, R[col]), " ".join("%s %.3f (n %d)" % (d[5:], roc_auc_score(g["y"] == 1, g[col]), len(g)) for d, g in R.groupby("dia"))))
    tarde = R[R.index.strftime("%H:%M") >= "18:00"]; print("   tarde: AUC %.3f (n %d)" % (roc_auc_score(tarde["y"] == 1, tarde[col]), len(tarde)))
    for umb in (0.60, 0.65, 0.70):
        s = R[(R[col] >= umb) | (R[col] <= 1 - umb)]; lado = np.where(s[col] > 0.5, 1, -1); ok = (lado * s["y"] > 0)
        base = np.mean([(R["y"] == 1).mean() if l > 0 else (R["y"] == -1).mean() for l in lado]) if len(s) else float("nan")
        print("   velas con p fuera de %.2f-%.2f: %3d (%d largas) | acierto %5.1f %% | tasa base del lado %5.1f %% | por dia: %s" % (1 - umb, umb, len(s), int((lado > 0).sum()), 100 * ok.mean() if len(s) else float("nan"), 100 * base,
              " ".join("%s %d/%d" % (d[5:], int(ok[s["dia"] == d].sum()), int((s["dia"] == d).sum())) for d in sorted(set(s["dia"])))))

# ------------------------------------------------------------------ cuanto tarda y cuantas veces NO se resuelve la apuesta de +-3 en 10 min (todas las velas, 21 dias)
print("\nLA APUESTA +-3 EN 10 MIN, desde cualquier vela de 2 min (todas las ruedas, corrida 'ultima' de cada dia):")
filas = []
for d in sorted(E):
    w = D[D["corrida"] == E[d]["ultima"]].set_index("ap").sort_index(); y = M.desenlaces(w)
    x = y[(y.index.strftime("%Y-%m-%d") == d) & M.en_rueda(y.index, "13:32", "20:00") & y["completo"]].copy(); x["hNY"] = (x.index.hour + 20) % 24; x["rango"] = (w["h"] - w["l"]).reindex(x.index); filas.append(x)
Y = pd.concat(filas)
for h, g in Y.groupby("hNY"):
    print("   %2d h NY: %4d velas | se resuelve %5.1f %% | las dos barreras en la misma vela %4.1f %% | de las resueltas, en la 1ra vela (2 min) %4.1f %%, mediana %d velas | rango mediano de la vela %.2f pts" % (
        h, len(g), 100 * (g["y"] != 0).mean(), 100 * g["ambigua"].mean(), 100 * (g.loc[g["y"] != 0, "k"] == 1).mean(), g.loc[g["y"] != 0, "k"].median(), g["rango"].median()))
