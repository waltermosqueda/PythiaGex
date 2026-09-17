# -*- coding: utf-8 -*-
"""techo_ml_04_sesgo_auc_dia.py — CONTROL DE METODO (no busca nada): los rasgos "de nivel" (puntos desde la apertura, posicion en el rango de la
rueda, distancia al VWAP, retorno de 1 h) dieron AUC POR DIA de 0,44-0,46 en 12 de 12 dias. Antes de creerlo: en un precio SIN memoria
(los mismos cambios de 1 s de cada sesion con el signo tirado a cara o cruz) cuanto da esa misma cuenta? Si da lo mismo, es un artefacto
de comparar momentos DEL MISMO DIA (el resultado de las 11:00 mueve el rasgo de las 11:10) y no una señal. Solo explorar."""
import sys, time
import numpy as np, pandas as pd
import techo_ml_lib as L, base as B

t0 = time.time(); T = B.todas_las_sesiones(); E = B.explorar(T); rng = np.random.default_rng(3); SIMS = 8
res = []
for k in range(SIMS):
    por_dia = {c: [] for c in ("d_apertura", "pos_rueda", "vwap_r_p", "ret_3600", "ret_300")}; junto = {c: ([], []) for c in por_dia}
    for s, d in sorted(E.items()):
        p = d["ultimo"].to_numpy("float64"); dp = np.diff(p, prepend=p[0]); signo = rng.choice([-1.0, 1.0], size=len(dp)); q = p[0] + np.cumsum(dp * signo)
        z = pd.DataFrame({"ultimo": q, "alto": q, "bajo": q}, index=d.index); y, _ = B.barrera(z, 8, 600)
        ok = L.elegible(d) & ((d.index.second.to_numpy() % L.PASO) == L.PASO - 1) & (y != 0); ru = d["rueda"].to_numpy()
        qs = pd.Series(q, index=d.index); hr = qs.where(ru).cummax(); lr = qs.where(ru).cummin(); v = d["vol"].astype("float64").where(ru, 0.0)
        f = {"d_apertura": qs - q[np.flatnonzero(ru)[0]], "pos_rueda": (qs - lr) / (hr - lr), "vwap_r_p": qs - (v * qs).cumsum() / v.cumsum(), "ret_3600": qs - qs.shift(3600), "ret_300": qs - qs.shift(300)}
        for c in f:
            x = f[c].to_numpy()[ok]; yy = (y[ok] > 0).astype(int); m = ~np.isnan(x); por_dia[c].append(L.auc(yy[m], x[m])); junto[c][0].append(yy[m]); junto[c][1].append(x[m])
    res.append({c: (float(np.nanmean(por_dia[c])), int(np.sum(np.array(por_dia[c]) > 0.5)), L.auc(np.concatenate(junto[c][0]), np.concatenate(junto[c][1]))) for c in por_dia})
    print("sorteo %d  %.0f s" % (k + 1, time.time() - t0))
print("\nPRECIO SIN MEMORIA (signo de cada cambio de 1 s a cara o cruz), %d sorteos x 12 sesiones:" % SIMS)
for c in res[0]:
    a = np.array([r[c][0] for r in res]); n = np.array([r[c][1] for r in res]); j = np.array([r[c][2] for r in res])
    print("  %-11s AUC por dia: media %.3f (de %.3f a %.3f) | dias sobre 0,5: %.1f de 12 | AUC conjunto: %.3f" % (c, a.mean(), a.min(), a.max(), n.mean(), j.mean()))
