# -*- coding: utf-8 -*-
"""RONDA 19, explorador MODELO: validacion purgada por RUEDA y procedimientos chicos de meta-etiquetado.
Pliegue externo: deja UNA rueda afuera como prueba y saca del entrenamiento la rueda anterior y la siguiente (embargo +-1).
Todo lo que el procedimiento elige (rasgos, C, umbrales, hojas, reglas, corte de probabilidad) se elige SOLO con las ruedas de entrenamiento.
Procedimientos (cada uno devuelve la mascara de eventos elegidos en la rueda de prueba + la descripcion de la regla de ese pliegue):
  ARBOL   : tamizado de rasgos (t ENTRE ruedas del AUC dentro de cada rueda) -> arbol de profundidad 2-3 -> hojas por precision de
            entrenamiento (Laplace) hasta cubrir la tasa q del entrenamiento
  L1      : tamizado (o todos) -> logistica L1 sobre rangos normalizados; C = el menor de la grilla que deja >= 4 rasgos (dentro del
            pliegue) -> corte en el cuantil (1-q) de los puntajes de entrenamiento
  RULEFIT : reglas de arboles de profundidad 2 sobre mitades de ruedas -> logistica L1 sobre las reglas -> corte q
  SUBGRUPO: busqueda en haz de conjunciones de 1 a 3 condiciones (umbrales en deciles de entrenamiento); maximiza la cota inferior de
            Wilson de la precision con soporte de smin a smax eventos por rueda y esperanza neta de entrenamiento > 0 (costo 0,50)
  DESTILA : alumno = arbol de profundidad 2-3 que imita la seleccion top-q del maestro (L1 o RULEFIT, con puntajes FUERA de pliegue
            interno: 5 bloques de ruedas contiguas) -> hojas del alumno hasta la tasa q"""
import math, os, sys, warnings
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from scipy.stats import rankdata, norm
from sklearn.tree import DecisionTreeClassifier
from sklearn.linear_model import LogisticRegression
import m0_comun as C

warnings.filterwarnings("ignore")
NR = 23400
C_GRILLA = (0.03, 0.04, 0.05, 0.07, 0.1, 0.15, 0.2, 0.3, 0.5)


# ------------------------------------------------------------------ utilidades
def wilson_lb(pos, n, z):
    n = np.maximum(n, 1e-9)
    p = pos / n
    den = 1 + z * z / n
    c = p + z * z / (2 * n)
    r = z * np.sqrt(np.maximum(p * (1 - p) / n + z * z / (4 * n * n), 0))
    return (c - r) / den


def rellenar(Xtr, Xte):
    med = np.nanmedian(Xtr, axis=0)
    med = np.where(np.isfinite(med), med, 0.0)
    return np.where(np.isnan(Xtr), med, Xtr), np.where(np.isnan(Xte), med, Xte)


def normal_rango(Xtr, Xte):
    """Normal por rangos con la distribucion de ENTRENAMIENTO."""
    out_tr = np.empty_like(Xtr); out_te = np.empty_like(Xte); n = len(Xtr)
    for j in range(Xtr.shape[1]):
        s = np.sort(Xtr[:, j])
        r_tr = (np.searchsorted(s, Xtr[:, j], "left") + np.searchsorted(s, Xtr[:, j], "right")) / 2.0
        r_te = (np.searchsorted(s, Xte[:, j], "left") + np.searchsorted(s, Xte[:, j], "right")) / 2.0
        out_tr[:, j] = norm.ppf((r_tr + 0.5) / (n + 1)); out_te[:, j] = norm.ppf((r_te + 0.5) / (n + 1))
    return out_tr, out_te


class Pliegue:
    """Lo que no depende de la etiqueta (se cachea y se reusa en el placebo): datos rellenados, rangos por rueda, normal por rangos."""
    def __init__(s, X, di, tr_d, te_d):
        s.mtr = np.isin(di, tr_d); s.mte = np.isin(di, te_d)
        s.Xtr, s.Xte = rellenar(X[s.mtr], X[s.mte])
        s.ditr = di[s.mtr]
        s.dias_tr = np.unique(s.ditr)
        s.ranks = [(s.ditr == d, rankdata(s.Xtr[s.ditr == d], axis=0)) for d in s.dias_tr]
        s.Ntr, s.Nte = normal_rango(s.Xtr, s.Xte)
        s.nd = len(s.dias_tr)
        s._tam = {}

    def tamizar(s, ytr, clave):
        """Orden de rasgos por |t| ENTRE ruedas de (AUC dentro de la rueda - 0,5)."""
        if clave in s._tam: return s._tam[clave]
        vals = []
        for m, R in s.ranks:
            yy = ytr[m]; n1 = yy.sum(); n0 = len(yy) - n1
            if n1 < 2 or n0 < 2: continue
            vals.append((R[yy == 1].sum(0) - n1 * (n1 + 1) / 2) / (n1 * n0) - 0.5)
        V = np.array(vals)
        sd = V.std(0, ddof=1); sd = np.where(sd > 0, sd, np.inf)
        t = V.mean(0) / sd * math.sqrt(len(V))
        out = (np.argsort(-np.abs(t)), t)
        s._tam[clave] = out
        return out


def cols_permitidas(nombres, excluir):
    return np.array([j for j, n in enumerate(nombres) if n not in excluir])


def elegir_cols(P, ytr, clave, k, permit):
    orden, t = P.tamizar(ytr, clave)
    orden = [j for j in orden if j in set(permit)]
    return np.array(orden[:k]) if k else np.array(sorted(permit))


def describir_camino(tree, nombres, hoja):
    t = tree.tree_; padre = {}
    for nodo in range(t.node_count):
        for hijo, lado in ((t.children_left[nodo], "<="), (t.children_right[nodo], ">")):
            if hijo != -1: padre[hijo] = (nodo, lado)
    conds = []; n = hoja
    while n in padre:
        p, lado = padre[n]
        conds.append("%s %s %.4g" % (nombres[t.feature[p]], lado, t.threshold[p])); n = p
    return " & ".join(reversed(conds))


def seleccion_por_hojas(tree, Atr, ytr, Ate, q, nombres):
    htr = tree.apply(Atr); hte = tree.apply(Ate)
    st = []
    for h in np.unique(htr):
        m = htr == h
        st.append((h, (ytr[m].sum() + 1) / (m.sum() + 2), int(m.sum())))
    st.sort(key=lambda z: -z[1])
    elegidas = []; cub = 0; N = len(ytr)
    for h, p, n in st:
        if cub >= q * N: break
        elegidas.append(h); cub += n
    sel = np.isin(hte, elegidas)
    desc = " | ".join("[%s] p=%.2f n=%d" % (describir_camino(tree, nombres, h), p, n) for h, p, n in st if h in elegidas)
    return sel, desc


# ------------------------------------------------------------------ procedimientos
def proc_arbol(P, ytr, pn, nombres, prm, clave):
    cols = elegir_cols(P, ytr, clave, prm["k"], prm["permit"])
    tr = DecisionTreeClassifier(max_depth=prm["depth"], min_samples_leaf=max(5, int(prm["min_hoja_dia"] * P.nd)), random_state=0)
    tr.fit(P.Xtr[:, cols], ytr)
    return seleccion_por_hojas(tr, P.Xtr[:, cols], ytr, P.Xte[:, cols], prm["q"], [nombres[c] for c in cols])


def ajustar_l1(A, y, nmin=4):
    lr = None
    for Cr in C_GRILLA:
        lr = LogisticRegression(penalty="l1", C=Cr, solver="liblinear", max_iter=3000).fit(A, y)
        if (np.abs(lr.coef_[0]) > 1e-6).sum() >= nmin: break
    return lr


def puntaje_l1(Ntr, ytr, Nte):
    lr = ajustar_l1(Ntr, ytr)
    return lr.decision_function(Ntr), lr.decision_function(Nte), lr


def proc_l1(P, ytr, pn, nombres, prm, clave):
    cols = elegir_cols(P, ytr, clave, prm["k"], prm["permit"])
    s_tr, s_te, lr = puntaje_l1(P.Ntr[:, cols], ytr, P.Nte[:, cols])
    if np.std(s_tr) == 0: return np.zeros(len(s_te), bool), "sin rasgos"
    thr = np.quantile(s_tr, 1 - prm["q"])
    co = sorted([(nombres[cols[j]], lr.coef_[0][j]) for j in range(len(cols)) if abs(lr.coef_[0][j]) > 1e-6], key=lambda z: -abs(z[1]))
    return s_te >= thr, "C %.2f corte %.3f; " % (lr.C, thr) + ", ".join("%s %+.2f" % (a, b) for a, b in co[:8])


def reglas_de_arbol(tree):
    t = tree.tree_; out = []
    def rec(nodo, camino):
        if nodo != 0: out.append(list(camino))
        if t.children_left[nodo] == -1: return
        f, th = t.feature[nodo], t.threshold[nodo]
        rec(t.children_left[nodo], camino + [(f, "<=", th)]); rec(t.children_right[nodo], camino + [(f, ">", th)])
    rec(0, [])
    return out


def aplicar_regla(X, regla):
    m = np.ones(len(X), bool)
    for f, op, th in regla:
        m &= (X[:, f] <= th) if op == "<=" else (X[:, f] > th)
    return m


def puntaje_rulefit(Xtr, ytr, ditr, Xte, prm, semilla=0):
    rng = np.random.default_rng(semilla)
    dias = np.unique(ditr); nd = len(dias); reglas = {}
    for i in range(prm["arboles"]):
        sub = rng.choice(dias, size=max(2, nd // 2), replace=False); m = np.isin(ditr, sub)
        tr = DecisionTreeClassifier(max_depth=2, min_samples_leaf=max(5, int(0.75 * len(sub))), random_state=int(rng.integers(1e9)),
                                    max_features=max(2, int(0.6 * Xtr.shape[1]))).fit(Xtr[m], ytr[m])
        for r in reglas_de_arbol(tr):
            reglas[tuple((f, op, round(float(th), 4)) for f, op, th in r)] = r
    R = list(reglas.values())
    if not R: return np.zeros(len(Xtr)), np.zeros(len(Xte)), None, []
    MA = np.column_stack([aplicar_regla(Xtr, r) for r in R]).astype(float)
    MB = np.column_stack([aplicar_regla(Xte, r) for r in R]).astype(float)
    sop = MA.mean(0); ok = (sop > 0.01) & (sop < 0.99)
    MA, MB = MA[:, ok], MB[:, ok]; R = [r for r, o in zip(R, ok) if o]
    lr = ajustar_l1(MA, ytr)
    return lr.decision_function(MA), lr.decision_function(MB), lr, R


def proc_rulefit(P, ytr, pn, nombres, prm, clave):
    cols = elegir_cols(P, ytr, clave, prm["k"], prm["permit"])
    s_tr, s_te, lr, R = puntaje_rulefit(P.Xtr[:, cols], ytr, P.ditr, P.Xte[:, cols], prm)
    if lr is None or np.std(s_tr) == 0: return np.zeros(len(s_te), bool), "sin reglas"
    thr = np.quantile(s_tr, 1 - prm["q"])
    co = lr.coef_[0]
    top = sorted([(j, co[j]) for j in range(len(co)) if abs(co[j]) > 1e-6], key=lambda z: -abs(z[1]))[:6]
    desc = "corte %.3f; " % thr + " ; ".join("%+.2f [%s]" % (c, " & ".join("%s %s %.4g" % (nombres[cols[f]], op, th) for f, op, th in R[j])) for j, c in top)
    return s_te >= thr, desc


def condiciones(Xtr, cols, cuantiles):
    conds = []
    for c in cols:
        x = Xtr[:, c]; u = np.unique(x)
        if len(u) <= 2:
            if len(u) == 2:
                conds.append((c, ">", (u[0] + u[1]) / 2)); conds.append((c, "<=", (u[0] + u[1]) / 2))
            continue
        for th in np.unique(np.quantile(x, cuantiles)):
            if th <= u[0] or th >= u[-1]: continue
            conds.append((c, "<=", th)); conds.append((c, ">", th))
    return conds


def buscar_subgrupo(Xtr, ytr, pn, nd, cols, prm):
    """Devuelve (regla, cota, candidatas evaluadas). regla = lista de (col, op, umbral)."""
    L, z, beam = prm["L"], prm["z"], prm.get("beam", 300)
    conds = condiciones(Xtr, cols, prm.get("cuantiles", np.arange(0.1, 0.95, 0.1)))
    if not conds: return None, -1, 0
    Ctr = np.column_stack([(Xtr[:, c] <= th) if op == "<=" else (Xtr[:, c] > th) for c, op, th in conds]).astype(np.float32).T
    y = ytr.astype(np.float32); p = pn.astype(np.float32)
    lo, hi = prm["smin"] * nd, prm["smax"] * nd
    mejor = [-1.0, None]; n_eval = 0
    def considerar(sop, pos, net, fn):
        ok = (sop >= lo) & (sop <= hi) & (net > 0)
        if not ok.any(): return
        lb = np.where(ok, wilson_lb(pos, sop, z), -1)
        j = np.unravel_index(np.argmax(lb), lb.shape)
        if lb[j] > mejor[0]: mejor[0] = float(lb[j]); mejor[1] = fn(j)
    considerar(Ctr.sum(1), Ctr @ y, Ctr @ p, lambda j: [j[0]]); n_eval += len(conds)
    if L >= 2:
        S2 = Ctr @ Ctr.T; P2 = (Ctr * y) @ Ctr.T; N2 = (Ctr * p) @ Ctr.T
        iu = np.triu_indices(len(conds), 1); n_eval += len(iu[0])
        S2u, P2u, N2u = S2[iu], P2[iu], N2[iu]
        considerar(S2u, P2u, N2u, lambda j: [iu[0][j[0]], iu[1][j[0]]])
        if L >= 3:
            lbp = np.where(S2u >= lo, wilson_lb(P2u, S2u, z), -1)
            top = np.argsort(-lbp)[:beam]; top = top[lbp[top] > -1]
            if len(top):
                Mk = Ctr[iu[0][top]] * Ctr[iu[1][top]]
                S3 = Mk @ Ctr.T; P3 = (Mk * y) @ Ctr.T; N3 = (Mk * p) @ Ctr.T; n_eval += S3.size
                considerar(S3, P3, N3, lambda j: [iu[0][top[j[0]]], iu[1][top[j[0]]], j[1]])
    if mejor[1] is None: return None, -1, n_eval
    return [conds[j] for j in mejor[1]], mejor[0], n_eval


def proc_subgrupo(P, ytr, pn, nombres, prm, clave):
    cols = elegir_cols(P, ytr, clave, prm["k"], prm["permit"])
    regla, lb, ne = buscar_subgrupo(P.Xtr, ytr, pn, P.nd, cols, prm)
    if regla is None: return np.zeros(len(P.Xte), bool), "ninguna", ne
    sel = aplicar_regla(P.Xte, regla)
    return sel, " & ".join("%s %s %.4g" % (nombres[c], op, th) for c, op, th in regla) + "  (LB %.3f)" % lb, ne


def proc_destila(P, ytr, pn, nombres, prm, clave):
    q = prm["q"]
    bloques = np.array_split(P.dias_tr, 5)
    sc = np.full(len(ytr), np.nan)
    cols = np.array(sorted(prm["permit"]))
    for b in bloques:
        te = np.isin(P.ditr, b); tr = ~te
        if prm["maestro"] == "L1":
            A, B = normal_rango(P.Xtr[tr][:, cols], P.Xtr[te][:, cols])
            _, s_te, _ = puntaje_l1(A, ytr[tr], B)
        else:
            # maestro RULEFIT sobre los k rasgos del tamizado de las ruedas internas de entrenamiento
            vals = []
            for d in np.unique(P.ditr[tr]):
                m = P.ditr == d
                yy = ytr[m]; n1 = yy.sum(); n0 = len(yy) - n1
                if n1 < 2 or n0 < 2: continue
                R = dict(P.ranks_por_dia)[d] if hasattr(P, "ranks_por_dia") else rankdata(P.Xtr[m], axis=0)
                vals.append((R[yy == 1].sum(0) - n1 * (n1 + 1) / 2) / (n1 * n0) - 0.5)
            V = np.array(vals); sd = V.std(0, ddof=1); sd = np.where(sd > 0, sd, np.inf)
            t = V.mean(0) / sd; orden = [j for j in np.argsort(-np.abs(t)) if j in set(cols)][:prm["prm_maestro"]["k"]]
            orden = np.array(orden)
            _, s_te, _, _ = puntaje_rulefit(P.Xtr[tr][:, orden], ytr[tr], P.ditr[tr], P.Xtr[te][:, orden], prm["prm_maestro"])
        sc[te] = s_te
    obj = (sc >= np.quantile(sc, 1 - q)).astype(int)
    al = DecisionTreeClassifier(max_depth=prm["depth"], min_samples_leaf=max(5, int(prm["min_hoja_dia"] * P.nd)), random_state=0,
                                class_weight="balanced").fit(P.Xtr[:, cols], obj)
    return seleccion_por_hojas(al, P.Xtr[:, cols], obj, P.Xte[:, cols], q, [nombres[c] for c in cols])


PROCS = {"ARBOL": proc_arbol, "L1": proc_l1, "RULEFIT": proc_rulefit, "SUBGRUPO": proc_subgrupo, "DESTILA": proc_destila}


# ------------------------------------------------------------------ validacion purgada por rueda
def armar_pliegues(base, X, embargo=1):
    out = []
    for d in range(base.nd):
        fuera = set(range(d - embargo, d + embargo + 1))
        out.append(Pliegue(X, base.di, [x for x in range(base.nd) if x not in fuera], [d]))
    return out


def correr_grilla(base, pliegues, nombres, variantes, Y, PTS, guardar_desc=True, solo=None):
    """Corre TODAS las variantes en cada pliegue (comparten el tamizado). Y y PTS: dict por salida (reales o barajados).
    Devuelve {nombre_variante: (mascara OOS, [(dia, desc)], candidatas_internas)}."""
    res = {v["nombre"]: [np.zeros(len(base.t), bool), [], 0] for v in variantes if solo is None or v["nombre"] in solo}
    for fi, P in enumerate(pliegues):
        P._tam = {}
        idx_te = np.nonzero(P.mte)[0]
        for v in variantes:
            if solo is not None and v["nombre"] not in solo: continue
            sal = v["salida"]; ytr = Y[sal][P.mtr]; pn = PTS[sal][P.mtr] - 0.5
            r = PROCS[v["proc"]](P, ytr, pn, nombres, v["prm"], clave=sal)
            if v["proc"] == "SUBGRUPO":
                s, desc, ne = r; res[v["nombre"]][2] = max(res[v["nombre"]][2], ne)
            else:
                s, desc = r
            res[v["nombre"]][0][idx_te[s]] = True
            if guardar_desc: res[v["nombre"]][1].append((base.dias[fi], desc))
    return res


def evaluar(base, sel, salida, pts, sal, y):
    tk = C.simular(base, sel, salida, pts=pts, sal=sal)
    m = C.metricas(base, tk, salida, pts=pts)
    m["precision_eventos"] = float(100 * y[sel].mean()) if sel.any() else np.nan
    m["eventos_elegidos_por_rueda"] = float(sel.sum() / base.nd)
    return m, tk


def permutar(base, rng):
    """Placebo: baraja los resultados (etiqueta, pts y duracion de la operacion) entre los eventos de la MISMA rueda."""
    perm = np.arange(len(base.t))
    for ii in base.idx_dia:
        perm[ii] = rng.permutation(ii)
    return perm


def resultados_permutados(base, perm):
    Y = {}; PTS = {}; SAL = {}
    for s in ("S1", "S6"):
        p = base.pts[s][perm]
        dur = (base.sal[s] - base.t)[perm]
        Y[s] = (p > 0).astype(int); PTS[s] = p; SAL[s] = np.minimum(base.t + dur, NR - 1)
    return Y, PTS, SAL
