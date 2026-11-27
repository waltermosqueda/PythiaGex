# -*- coding: utf-8 -*-
"""ONE-TIMEFRAMING DE LA MANANA CONTRA LA TARDE, POR FUERA (27-11-2026, mio, definicion propia sin mirar el codigo de la R25):
velas de 30 min de 9:30 a 13:00 (7 velas, 6 pasos). Paso alcista = el minimo no perfora el minimo anterior; bajista = el maximo no supera
el maximo anterior. Puntaje 'dia' = max(pasos alcistas, pasos bajistas); puntaje 'lado' = pasos en la direccion del lado del precio
respecto del VWAP de las 13:00. Objetivo TA = la tarde se aleja del VWAP: signo(C13 - VWAP13) x (C16 - C13) > 0. AUC por Mann-Whitney.
Uso: python -I otf_propio.py"""
import sys, os
sys.stdout.reconfigure(encoding="utf-8")
import numpy as np, pandas as pd
AQUI = os.path.dirname(os.path.abspath(__file__)); LAB = os.path.dirname(os.path.dirname(AQUI))
d = pd.read_csv(os.path.join(LAB, "dom", "ronda3", "velas", "velas-MES-m30.csv"), usecols=["t", "o", "h", "l", "c", "vol"])
t = pd.to_datetime(d["t"]).dt.tz_localize("UTC").dt.tz_convert("America/New_York")
d["dia"] = t.dt.strftime("%Y-%m-%d"); d["min"] = t.dt.hour * 60 + t.dt.minute
r = d[(d["min"] >= 570) & (d["min"] < 960)]
filas = []
for dia, g in r.groupby("dia"):
    if len(g) != 13: continue
    h, l, c, v = (g[k].to_numpy(float) for k in ("h", "l", "c", "vol"))
    vw13 = ((h[:7] + l[:7]) / 2 * v[:7]).sum() / v[:7].sum()
    up = int(sum(l[i] >= l[i - 1] for i in range(1, 7))); dn = int(sum(h[i] <= h[i - 1] for i in range(1, 7)))
    s13 = np.sign(c[6] - vw13)
    if s13 == 0: continue
    filas.append(dict(dia=dia, dia_score=max(up, dn), lado_score=up if s13 > 0 else dn, TA=int(s13 * (c[12] - c[6]) > 0)))
f = pd.DataFrame(filas)
def auc(score, y):
    pos = score[y == 1]; neg = score[y == 0]
    return (sum((p > neg).sum() + 0.5 * (p == neg).sum() for p in pos)) / (len(pos) * len(neg))
for nom, a, b in (("2024-04-29..2025-09-08 (diseno R25)", "2024-04-29", "2025-09-08"), ("2025-09-09..2026-08-17 (validacion R25)", "2025-09-09", "2026-08-17")):
    q = f[(f.dia >= a) & (f.dia <= b)]
    print("%-40s ruedas %d | TA base %.1f %% | AUC OTF dia %.3f | AUC OTF lado %.3f | OTF >= 5 de 6 (dia): %d ruedas, TA %.1f %%"
          % (nom, len(q), 100 * q.TA.mean(), auc(q.dia_score.to_numpy(), q.TA.to_numpy()), auc(q.lado_score.to_numpy(), q.TA.to_numpy()),
             (q.dia_score >= 5).sum(), 100 * q[q.dia_score >= 5].TA.mean()))
