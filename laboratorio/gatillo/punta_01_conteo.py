# -*- coding: utf-8 -*-
"""punta_01_conteo.py — ANTES del pre-registro: forma de los rasgos y CANTIDAD de disparos por variante en EXPLORAR. No mira ningun resultado futuro."""
import sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, punta_lib as L

T = B.todas_las_sesiones(); E = B.explorar(T); del T
F = {}; OK = {}
for s, d in E.items(): F[s] = L.rasgos(d); OK[s] = L.elegible(d)
R = pd.concat([F[s][OK[s]] for s in E])
q = [0.5, 0.75, 0.9, 0.95, 0.99]
for c in ("qi10", "qi30", "ret60", "ret300", "zP60", "zP300", "zD60", "zD300", "zO60", "mp_mid", "mp_ult", "prof", "spread"):
    print("%-7s |.| cuantiles %s" % (c, R[c].abs().quantile(q).round(2).to_dict()))
U = dict(qi_inst=0.8, qi_30=round(float(R["qi30"].abs().quantile(0.95)), 2), qi_10=round(float(R["qi10"].abs().quantile(0.75)), 2), mov_60=round(float(R["ret60"].abs().quantile(0.90)) * 4) / 4, div60_p=1.25, div300=1.0, ofi60=1.25)
print("\numbrales congelados:", U)
tot = {}
for s in E:
    V = L.variantes(F[s], U)
    for k, lado in V.items():
        i = L.desagrupar(lado, OK[s]); a = tot.setdefault(k, [0, 0, 0]); a[0] += len(i); a[1] += int((lado[i] > 0).sum()); a[2] += int(((lado != 0) & OK[s]).sum())
print("\nvariante                      disparos (desagrupados)  compra  | segundos crudos con condicion")
for k, (n, c, crudo) in tot.items(): print("%-28s %6d   %5.1f %% compra | %7d" % (k, n, 100.0 * c / max(1, n), crudo))
