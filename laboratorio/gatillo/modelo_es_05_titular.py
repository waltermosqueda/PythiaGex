# -*- coding: utf-8 -*-
"""modelo_es_05_titular.py - AUDITORIA POST-HOC del titular del 10-09 (NO es el veredicto; el veredicto es modelo_es_02_juicio.py).
Reproduce el avance hacia adelante de laboratorio/ml_check.py (mismos rasgos, misma logistica, mismos dias) para M1 (+3/-3 en 10 velas) y M2 (5 velas)
y le hace las dos preguntas que el 10-09 no se le hicieron:
  (1) PLACEBO de la misma media hora: por cada disparo, otra vela resuelta del MISMO dia y la MISMA media hora con el MISMO lado (200 sorteos).
  (2) RACIMOS: sin enfriamiento, velas pegadas de un mismo movimiento cuentan como aciertos separados. Cuantas celdas (dia x media hora) distintas hay,
      y cuanto da con enfriamiento (un disparo y despues H velas calladas)."""
import os, sys
import numpy as np
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.dirname(AQUI)); sys.path.insert(0, AQUI)
try:
    import ctypes; ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x00004000)
except Exception: pass
from sklearn.linear_model import LogisticRegression
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler
from gatillo_cientifico import cargar, rasgos, desenlace

G = 3.0
for marco, h, hasta in (("M1", 10, "2026-09-04"), ("M2", 5, "2026-09-10")):
    vs = [v for v in cargar("MES", marco) if v["dia"] <= hasta]; rasgos(vs)
    X, y, dia, idx = [], [], [], []
    for i in range(20, len(vs) - h - 1):
        v = vs[i]
        if vs[i - 1]["dia"] != v["dia"]: continue
        r, _, _ = desenlace(vs, i, 1, G, G, h)
        if r is None: continue
        rt = v["rt"] or 0.25
        def dist(x, sin): return (x - v["c"]) / rt if x else sin
        X.append([v["ret15"] / rt, dist(v["zero"], 0.0), v["rango"] / rt, dist(v["mn"], -40.0), dist(v["dom_arr"], 40.0), v["cum15"] / (abs(v["cum15"]) + 500.0),
                  dist(v["mc30"], 0.0), v["dz"], dist(v["mp"], 40.0), dist(v["dom_aba"], -40.0)])
        y.append(1 if r else 0); dia.append(v["dia"]); idx.append(i)
    X = np.array(X); y = np.array(y); dia = np.array(dia); idx = np.array(idx); dias = sorted(set(dia))
    p = np.full(len(y), np.nan)
    for k in range(5, len(dias)):
        tr = np.isin(dia, dias[:k]); te = dia == dias[k]
        m = make_pipeline(StandardScaler(), LogisticRegression(C=0.3, max_iter=2000)).fit(X[tr], y[tr]); p[te] = m.predict_proba(X[te])[:, 1]
    hNY = np.array([vs[i]["t"].hour - 4 for i in idx]); media = np.array(["%s|%02d:%02d" % (vs[i]["dia"], vs[i]["t"].hour, 30 * (vs[i]["t"].minute // 30)) for i in idx])
    print("\n=========== MES %s, +3/-3 en %d velas, %d dias (%s a %s), avance hacia adelante desde el dia 6 ===========" % (marco, h, len(dias), dias[0], dias[-1]))
    rng = np.random.RandomState(7)
    for nombre, franja in (("todo el dia", np.ones(len(y), bool)), ("tarde 14-16 h NY", hNY >= 14)):
        for umb in (0.65, 0.70):
            s = ~np.isnan(p) & franja & ((p >= umb) | (p <= 1 - umb))
            if s.sum() < 5: print("  %-18s umbral %.2f: %d disparos" % (nombre, umb, s.sum())); continue
            lado = np.where(p[s] > 0.5, 1, 0); ok = (lado == y[s]); celdas = len(set(media[s])); nd = len(set(dia[s]))
            # placebo: misma celda dia x media hora, mismo lado
            tasas = []
            pool = {c: y[(media == c) & ~np.isnan(p)] for c in set(media[s])}
            for _ in range(200):
                tasas.append(np.mean([pool[c][rng.randint(len(pool[c]))] == l for c, l in zip(media[s], lado)]))
            pm, ps = float(np.mean(tasas)), float(np.std(tasas))
            # con enfriamiento de h velas (un disparo y h velas calladas), por orden de aparicion
            ult = -10 ** 9; keep = []
            for j in np.where(s)[0]:
                if idx[j] - ult >= h: keep.append(j); ult = idx[j]
            keep = np.array(keep); lk = np.where(p[keep] > 0.5, 1, 0); okk = lk == y[keep]
            tk = []
            for _ in range(200):
                tk.append(np.mean([pool[c][rng.randint(len(pool[c]))] == l for c, l in zip(media[keep], lk)]))
            base = float(np.mean([y[~np.isnan(p) & franja].mean() if l else 1 - y[~np.isnan(p) & franja].mean() for l in lado]))
            print("  %-18s umbral %.2f: %3d disparos, acierto %5.1f %% | %d largos / %d cortos | tasa base del lado %5.1f %% | placebo misma media hora %5.1f +- %4.1f -> z %+5.2f | en %d celdas dia x media hora de %d dias" % (
                nombre, umb, s.sum(), 100 * ok.mean(), int(lado.sum()), int((1 - lado).sum()), 100 * base, 100 * pm, 100 * ps, (ok.mean() - pm) / ps if ps > 0 else float("nan"), celdas, nd))
            print("  %-18s              con enfriamiento %d velas: %3d disparos, acierto %5.1f %% | placebo %5.1f +- %4.1f -> z %+5.2f | por dia: %s" % (
                "", h, len(keep), 100 * okk.mean(), 100 * np.mean(tk), 100 * np.std(tk), (okk.mean() - np.mean(tk)) / np.std(tk) if np.std(tk) > 0 else float("nan"),
                " ".join("%s %d/%d" % (d[5:], int(okk[dia[keep] == d].sum()), int((dia[keep] == d).sum())) for d in sorted(set(dia[keep])))))
