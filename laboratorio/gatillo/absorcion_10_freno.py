# -*- coding: utf-8 -*-
"""absorcion_10_freno.py — DESCRIPTIVO: ¿la parte absorbida del volumen agrega algo sobre lo que ya dice la vela?
Por DIA: regresion de |movimiento de los proximos 60 s| contra volumen de 30 s, rango de los ultimos 30 s y parte absorbida (abs30/vol30),
todo estandarizado. Se cuenta en cuantos dias el coeficiente de la parte absorbida es negativo (freno) y su tamano."""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd, base as B, absorcion_base as A
T = A.tablas()
for rot, S in (("explorar", B.explorar(T)), ("confirmar", B.confirmar(T))):
    coefs = []
    for s, seg in S.items():
        f = A.rasgos(s, seg); v = A.valido_de(s, seg); p = seg["ultimo"].to_numpy(); N = len(p); idx = np.flatnonzero(v)[::15]
        vol30 = seg["vol"].rolling(30, min_periods=1).sum().to_numpy(); r30 = (seg["alto"].rolling(30, min_periods=1).max() - seg["bajo"].rolling(30, min_periods=1).min()).to_numpy()
        share = ((f["AB30"] + f["AA30"]).to_numpy() / np.clip(vol30, 1, None))
        y = np.abs(p[np.clip(idx + 60, 0, N - 1)] - p[idx]); X = np.column_stack([np.log1p(vol30[idx]), np.log1p(r30[idx]), share[idx]])
        X = (X - X.mean(0)) / X.std(0); X = np.column_stack([np.ones(len(X)), X]); b = np.linalg.lstsq(X, y, rcond=None)[0]
        coefs.append(dict(sesion=s, vol=b[1], rango=b[2], parte_abs=b[3], media_y=y.mean()))
    C = pd.DataFrame(coefs); t = C["parte_abs"].mean() / (C["parte_abs"].std(ddof=1) / np.sqrt(len(C)))
    print("--- %s: coef. por desvio (puntos de |mov 60 s|): volumen %+.2f | rango previo %+.2f | parte absorbida %+.3f (t entre dias %.2f, negativa en %d de %d dias) | |mov| medio %.2f" % (
        rot, C["vol"].mean(), C["rango"].mean(), C["parte_abs"].mean(), t, (C["parte_abs"] < 0).sum(), len(C), C["media_y"].mean()))
