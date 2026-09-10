# -*- coding: utf-8 -*-
"""¿HAY ALGO PREDECIBLE A 30 MINUTOS CON LO QUE TENEMOS? Prueba de techo con aprendizaje
automatico honesto: regresion logistica (y un bosque) sobre TODOS los rasgos por minuto
(order flow del futuro + estructura de gamma), validacion por bloques de DIAS (nunca se
entrena y se prueba en el mismo dia), y la medida es el AUC fuera de muestra para predecir si
el precio toca +G antes que -G en los 30 min siguientes.

Si el AUC fuera de muestra es ~0,50, no hay señal direccional a ese horizonte en estos datos
y ningun gatillo armado a mano la va a encontrar. Si es > 0,55 de forma estable, vale la pena
buscar la regla. Es la pregunta previa a cualquier "gatillo que acierte".

Uso: python laboratorio/ml_check.py MNQ [--g 25] [--h 30]
"""
import math
import os
import sys

import numpy as np
from sklearn.ensemble import RandomForestClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import roc_auc_score
from sklearn.model_selection import GroupKFold
from sklearn.preprocessing import StandardScaler
from sklearn.pipeline import make_pipeline

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gatillo_cientifico import cargar, rasgos, desenlace  # noqa: E402


def main():
    inst = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else "MNQ"
    arg = lambda k, d: type(d)(sys.argv[sys.argv.index(k) + 1]) if k in sys.argv else d
    es_nq = inst.startswith("MNQ") or inst.startswith("NQ")
    g = arg("--g", 25.0 if es_nq else 6.0); h = arg("--h", 30); marco = arg("--marco", "M1")
    vs = cargar(inst, marco); rasgos(vs)
    X, y, grupos, filas = [], [], [], []
    for i in range(20, len(vs) - h - 1):
        v = vs[i]
        if vs[i - 1]["dia"] != v["dia"]:
            continue
        r, _, _ = desenlace(vs, i, 1, g, g, h)          # True: +G primero (sube); False: -G primero (baja)
        if r is None:
            continue
        rt = v["rt"] or 0.25
        def dist(x):
            return (x - v["c"]) / rt if x else 0.0
        f = [v["dz"], v["vz"], v["cum15"] / (abs(v["cum15"]) + 500.0), v["ret15"] / rt, 1.0 if v["tren"] else 0.0,
             1.0 if v["conv_neg"] else 0.0, 1.0 if v["mucho"] else 0.0,
             dist(v["dom_arr"]) if v["dom_arr"] else 40.0, dist(v["dom_aba"]) if v["dom_aba"] else -40.0,
             dist(v["zero"]), dist(v["mp"]) if v["mp"] else 40.0, dist(v["mn"]) if v["mn"] else -40.0, dist(v["pico"]),
             dist(v["mc30"]) if v["mc30"] else 0.0, dist(v["mc5"]) if v["mc5"] else 0.0,
             (v["t"].hour * 60 + v["t"].minute - 810) / 390.0, v["rango"] / rt, np.sign(v["delta"]) * min(3.0, abs(v["dz"]))]
        X.append(f); y.append(1 if r else 0); grupos.append(v["dia"]); filas.append(i)
    NOMBRES = ["dz", "vz", "cum15", "ret15/rt", "tren", "conv_neg", "mucho", "d_dom_arr", "d_dom_aba", "d_zero", "d_mp", "d_mn", "d_pico",
               "d_mc30", "d_mc5", "hora", "rango/rt", "signo*dz"]
    X = np.array(X); y = np.array(y); grupos = np.array(grupos)
    IDX = list(range(len(NOMBRES)))
    if "--reducido" in sys.argv:
        # los 8 rasgos que pesaron en ES a 10 min: momentum corto + imanes (zero, major, dominante, Max Change)
        quedan = ["ret15/rt", "d_zero", "rango/rt", "d_mn", "d_dom_arr", "cum15", "d_mc30", "dz", "d_mp", "d_dom_aba"]
        IDX = [NOMBRES.index(n) for n in quedan]; NOMBRES = quedan; X = X[:, IDX]
        print("  modelo REDUCIDO a %d rasgos: %s" % (len(quedan), ", ".join(quedan)))
    filas_i = list(range(20, len(vs) - h - 1))
    print("%s: %d minutos con desenlace (+%g antes que -%g en %d min), %d dias | sube %.1f %%" % (inst, len(y), g, g, h, len(set(grupos)), 100.0 * y.mean()))
    modelos = [("logistica (L2)", make_pipeline(StandardScaler(), LogisticRegression(C=0.3, max_iter=2000))),
               ("bosque (200 arboles, hojas >= 200)", RandomForestClassifier(n_estimators=200, min_samples_leaf=200, random_state=7, n_jobs=-1))]
    gkf = GroupKFold(n_splits=min(5, len(set(grupos))))
    for nombre, m in modelos:
        aucs, accs = [], []
        for tr, te in gkf.split(X, y, grupos):
            m.fit(X[tr], y[tr]); p = m.predict_proba(X[te])[:, 1]
            aucs.append(roc_auc_score(y[te], p))
            # decision solo cuando el modelo esta seguro (p fuera de 0,4-0,6): acierto y cuantos
            seg = (p > 0.6) | (p < 0.4)
            if seg.sum() > 20:
                accs.append(((p[seg] > 0.5) == (y[te][seg] == 1)).mean())
        print("  %-36s AUC fuera de muestra por bloque de dias: %s | media %.3f | acierto cuando esta seguro (p<0,4 o >0,6): %s" % (
            nombre, " ".join("%.3f" % a for a in aucs), float(np.mean(aucs)), " ".join("%.0f%%" % (100 * a) for a in accs) if accs else "nunca seguro"))
    if "--rasgos" in sys.argv:
        # que rasgos pesan: coeficientes estandarizados de la logistica (media entre bloques) y una
        # simulacion honesta con las predicciones FUERA de bloque: operar solo cuando p<0,4 o p>0,6
        from sklearn.inspection import permutation_importance
        coefs, p_oof = [], np.full(len(y), np.nan)
        for tr, te in gkf.split(X, y, grupos):
            m = make_pipeline(StandardScaler(), LogisticRegression(C=0.3, max_iter=2000)).fit(X[tr], y[tr])
            coefs.append(m[-1].coef_[0]); p_oof[te] = m.predict_proba(X[te])[:, 1]
        c = np.mean(coefs, axis=0)
        print("  coeficientes estandarizados (logistica, + = sube): " + ", ".join("%s %+.2f" % (n, v) for n, v in sorted(zip(NOMBRES, c), key=lambda t: -abs(t[1]))[:10]))
        seg = (p_oof > 0.6) | (p_oof < 0.4)
        ok = ((p_oof[seg] > 0.5) == (y[seg] == 1))
        dias_n = len(set(grupos))
        print("  operando FUERA de bloque solo cuando esta seguro: %d disparos (%.1f por dia), acierto %.1f %% (regla simetrica, moneda 50 %%)" % (seg.sum(), seg.sum() / float(dias_n), 100.0 * ok.mean() if seg.sum() else float("nan")))
        for umb in (0.65, 0.7):
            s2 = (p_oof > umb) | (p_oof < 1 - umb)
            if s2.sum() >= 10:
                print("     umbral %.2f: %d disparos (%.1f por dia), acierto %.1f %%" % (umb, s2.sum(), s2.sum() / float(dias_n), 100.0 * ((p_oof[s2] > 0.5) == (y[s2] == 1)).mean()))
        # por dia: cuantos dias ganan
        gd = {}
        for d, ok1, sg in zip(grupos, (p_oof > 0.5) == (y == 1), seg):
            if sg: gd.setdefault(d, []).append(ok1)
        print("  por dia (acierto cuando seguro): " + " ".join("%s %d/%d" % (d[5:], sum(v), len(v)) for d, v in sorted(gd.items())))
        # por hora de Nueva York y regla 2:1 (2G a favor antes que G en contra) sobre los seguros (umbral 0,65)
        s3 = (p_oof > 0.65) | (p_oof < 0.35)
        por_hora, B = {}, []
        for k in np.where(s3)[0]:
            i = filas[k]; lado = 1 if p_oof[k] > 0.5 else -1
            hr = (vs[i]["t"].hour - 4)
            por_hora.setdefault(hr, []).append((p_oof[k] > 0.5) == (y[k] == 1))
            rB, _, _ = desenlace(vs, i, lado, 2 * g, g, int(h * 1.5))
            if rB is not None: B.append(rB)
        print("  umbral 0,65 por hora NY: " + " ".join("%dh %d/%d" % (hr, sum(v), len(v)) for hr, v in sorted(por_hora.items())))
        if B: print("  umbral 0,65 con regla 2:1 (%g a favor antes que %g en contra): %d casos, acierto %.1f %% (moneda 33 %%, punto de equilibrio 33 %%)" % (2 * g, g, len(B), 100.0 * sum(B) / len(B)))
    if "--walk" in sys.argv:
        # AVANCE HACIA ADELANTE: se entrena con los dias anteriores (minimo 5) y se opera el dia siguiente
        dias = sorted(set(grupos)); res = []
        for k in range(5, len(dias)):
            tr = np.isin(grupos, dias[:k]); te = grupos == dias[k]
            m = make_pipeline(StandardScaler(), LogisticRegression(C=0.3, max_iter=2000)).fit(X[tr], y[tr])
            p = m.predict_proba(X[te])[:, 1]; seg = (p > 0.65) | (p < 0.35)
            ok = ((p[seg] > 0.5) == (y[te][seg] == 1))
            hrs = np.array([vs[filas[j]]["t"].hour - 4 for j in np.where(te)[0]])[seg]
            tarde = hrs >= 14
            res.append((dias[k], int(seg.sum()), float(ok.mean()) if seg.sum() else float("nan"), int(tarde.sum()), float(ok[tarde].mean()) if tarde.sum() else float("nan")))
        tot = sum(r[1] for r in res); oks = sum(r[1] * r[2] for r in res if r[1]); tt = sum(r[3] for r in res); okt = sum(r[3] * r[4] for r in res if r[3])
        print("  AVANCE HACIA ADELANTE (umbral 0,65): " + " ".join("%s %d/%d" % (r[0][5:], round(r[1] * r[2]) if r[1] else 0, r[1]) for r in res))
        print("     total %d disparos, acierto %.1f %% | solo 14-16h NY: %d disparos, acierto %.1f %%" % (tot, 100.0 * oks / tot if tot else float("nan"), tt, 100.0 * okt / tt if tt else float("nan")))
    if "--placebo" in sys.argv:
        # los mismos rasgos con TODOS los niveles corridos +-15 pts (ES) / +-60 (NQ): si el acierto se mantiene, los niveles no aportan
        from gatillo_cientifico import con_niveles_corridos
        corr = 60.0 if es_nq else 15.0
        for signo in (+1, -1):
            vc = con_niveles_corridos(vs, signo * corr)
            Xc = []
            for i in filas:
                v = vc[i]; rt = v["rt"] or 0.25
                def dist(x): return (x - v["c"]) / rt if x else 0.0
                f = [v["dz"], v["vz"], v["cum15"] / (abs(v["cum15"]) + 500.0), v["ret15"] / rt, 1.0 if v["tren"] else 0.0,
                     1.0 if v["conv_neg"] else 0.0, 1.0 if v["mucho"] else 0.0,
                     dist(v["dom_arr"]) if v["dom_arr"] else 40.0, dist(v["dom_aba"]) if v["dom_aba"] else -40.0,
                     dist(v["zero"]), dist(v["mp"]) if v["mp"] else 40.0, dist(v["mn"]) if v["mn"] else -40.0, dist(v["pico"]),
                     dist(v["mc30"]) if v["mc30"] else 0.0, dist(v["mc5"]) if v["mc5"] else 0.0,
                     (v["t"].hour * 60 + v["t"].minute - 810) / 390.0, v["rango"] / rt, np.sign(v["delta"]) * min(3.0, abs(v["dz"]))]
                Xc.append(f)
            Xc = np.array(Xc)[:, IDX]
            aucs, accs = [], []
            for tr, te in gkf.split(Xc, y, grupos):
                m = make_pipeline(StandardScaler(), LogisticRegression(C=0.3, max_iter=2000)).fit(Xc[tr], y[tr]); p = m.predict_proba(Xc[te])[:, 1]
                aucs.append(roc_auc_score(y[te], p)); seg = (p > 0.65) | (p < 0.35)
                if seg.sum() > 10: accs.append(((p[seg] > 0.5) == (y[te][seg] == 1)).mean())
            print("  PLACEBO niveles corridos %+.0f: AUC media %.3f | acierto seguros %s" % (signo * corr, float(np.mean(aucs)), " ".join("%.0f%%" % (100 * a) for a in accs)))
    if "--atribucion" in sys.argv:
        # (1) placebo bien hecho: los rasgos de NIVELES permutados entre filas (rompe la relacion nivel-precio,
        # no un corrimiento constante que la escala absorbe); (2) modelo SOLO order flow + hora
        rng = np.random.RandomState(11)
        niv_idx = [k for k, n in enumerate(NOMBRES) if n.startswith("d_")]
        of_idx = [k for k, n in enumerate(NOMBRES) if not n.startswith("d_")]
        def corrida(Xv, titulo, umb=0.65):
            aucs, accs, n = [], [], 0
            for tr, te in gkf.split(Xv, y, grupos):
                m = make_pipeline(StandardScaler(), LogisticRegression(C=0.3, max_iter=2000)).fit(Xv[tr], y[tr]); p = m.predict_proba(Xv[te])[:, 1]
                aucs.append(roc_auc_score(y[te], p)); seg = (p > umb) | (p < 1 - umb); n += int(seg.sum())
                if seg.sum() > 10: accs.append(((p[seg] > 0.5) == (y[te][seg] == 1)).mean())
            print("  %-44s AUC media %.3f | seguros (%.2f): %d, acierto %s" % (titulo, float(np.mean(aucs)), umb, n, " ".join("%.0f%%" % (100 * a) for a in accs)))
        Xp = X.copy(); perm = rng.permutation(len(y)); Xp[:, niv_idx] = X[perm][:, niv_idx]
        corrida(Xp, "PLACEBO: niveles permutados entre filas")
        corrida(X[:, of_idx], "SOLO order flow + hora (sin niveles)")
        corrida(X[:, niv_idx], "SOLO niveles (sin order flow)")
        corrida(X, "completo, umbral 0,70", 0.70)
        # avance hacia adelante, tarde (14-16h NY), umbral 0,70
        dias = sorted(set(grupos)); okt = tt = 0
        for k in range(5, len(dias)):
            tr = np.isin(grupos, dias[:k]); te = grupos == dias[k]
            m = make_pipeline(StandardScaler(), LogisticRegression(C=0.3, max_iter=2000)).fit(X[tr], y[tr]); p = m.predict_proba(X[te])[:, 1]
            hrs = np.array([vs[filas[j]]["t"].hour - 4 for j in np.where(te)[0]])
            seg = ((p > 0.70) | (p < 0.30)) & (hrs >= 14)
            okt += int(((p[seg] > 0.5) == (y[te][seg] == 1)).sum()); tt += int(seg.sum())
        print("  avance hacia adelante, 14-16h NY, umbral 0,70: %d disparos, acierto %.1f %% (%.1f por dia)" % (tt, 100.0 * okt / tt if tt else float("nan"), tt / float(len(dias) - 5)))
    if "--exportar" in sys.argv:
        import json as _json
        m = make_pipeline(StandardScaler(), LogisticRegression(C=0.3, max_iter=2000)).fit(X, y)
        sc, lr = m[0], m[-1]
        out = {"instrumento": inst, "marco": marco, "g": g, "h": h, "rasgos": NOMBRES, "media": sc.mean_.tolist(), "escala": sc.scale_.tolist(),
               "coef": lr.coef_[0].tolist(), "intercepto": float(lr.intercept_[0]), "dias": sorted(set(grupos.tolist())), "n": int(len(y))}
        ruta = os.path.join(os.path.dirname(os.path.abspath(__file__)), "modelo_%s_%s_%dvelas.json" % (inst, marco, h))
        _json.dump(out, open(ruta, "w"), indent=1); print("  modelo exportado:", ruta)
    print()
    print("COMO LEERLO: AUC 0,50 = moneda. Por debajo de 0,55 estable no hay regla que valga; con esto el mejor gatillo")
    print("hecho a mano no puede hacer mas que el techo que marca el modelo con todos los rasgos juntos.")


if __name__ == "__main__":
    main()
