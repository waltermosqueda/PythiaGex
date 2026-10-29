# -*- coding: utf-8 -*-
"""Pre-registro 3 (PREREGISTRO_PREDICCION.md): el limite estadistico para detectar un giro.
Filas = velas en tramos de ST(10,3) con retroceso r > 0; etiqueta = el precio sigue A pts en contra antes de volver al
extremo E; modelos M0 (geometria r/(r+A)), M1 (logistica de r), M2 (logistica con todo), M3 (HistGradientBoosting con todo);
despues la regla P(theta) simulada vela a vela con sus propios tramos, contra la base y las curvas de las rondas 1-2.
Salidas: pred_01_salida.txt, pred_filas_*.parquet, pred_reglas.csv, pred_movs.csv, pred_modelos.pkl."""
import hashlib
import pickle
import sys
from pathlib import Path

import numba as nb
import numpy as np
import pandas as pd
from sklearn.ensemble import HistGradientBoostingClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import log_loss, roc_auc_score

sys.path.insert(0, str(Path(__file__).resolve().parent))
import t_lib as T

T.idle()
LAB = T.LAB
p = T.Salida("pred_01_salida.txt")
p("PREDICCION. SHA-256 PREREGISTRO_PREDICCION.md:", hashlib.sha256((LAB / "PREREGISTRO_PREDICCION.md").read_bytes()).hexdigest())

# cargadores y medidas de la ronda 2 (sin correr la ronda)
_src = open(LAB / "cal_03.py", encoding="utf-8").read().split("# ------------------------------------------------------------------ control:")[0]
_src = _src.replace('p = T.Salida("cal_03_salida.txt")', "p = print")
G = {"__name__": "cal03", "__file__": str(LAB / "cal_03.py")}
exec(compile(_src, "cal_03_parcial", "exec"), G)
Serie, serie_mes, serie_mnq, corriente = G["Serie"], G["serie_mes"], G["serie_mnq"], G["corriente"]

RASGOS = ["r_atr", "r_pts", "k", "vel", "tramo_atr", "tramo_dur", "atr_ratio", "atr60_pts", "delta_contra", "vol_desde",
          "cuerpo", "cierre_pos", "delta_vela", "vwap_dist", "vwap_pend", "min_ses", "rueda", "dist_max", "dist_min", "cierres_contra"]
M1R = ["r_atr", "r_atr2", "log_r", "k"]


# ------------------------------------------------------------------ arreglos por serie
def arreglos(S, escala):
    v = S.v
    n = len(v)
    o = v.oa.values.astype(float)
    vol = v.vol.values.astype(float)
    dl = v.delta.values.astype(float)
    a10, a60 = S.atr(10), S.atr(60)
    volm = np.full(n, np.nan)
    for a, b in S.bl:
        volm[a:b] = pd.Series(vol[a:b]).rolling(60, min_periods=20).mean().values
    ses_id = pd.factorize(v.sesion)[0]
    tp = (S.h + S.l + S.c) / 3
    g = pd.DataFrame(dict(s=ses_id, pv=tp * vol, vv=vol, h=S.h, l=S.l))
    vw = (g.groupby("s").pv.cumsum() / g.groupby("s").vv.cumsum().replace(0, np.nan)).values
    vw = np.where(np.isnan(vw), tp, vw)
    shi = g.groupby("s").h.cummax().values
    slo = g.groupby("s").l.cummin().values
    ini = (v.sesion - pd.Timedelta(hours=6)).values
    mins = ((v.ny.values - ini) / np.timedelta64(1, "m")).astype(float)
    # ultima vela de la misma sesion y bloque (para cortar la etiqueta)
    fin = np.zeros(n, np.int64)
    clave = ses_id * 100000 + S.blq
    cambios = np.r_[np.nonzero(clave[1:] != clave[:-1])[0], n - 1]
    j = 0
    for i in range(n):
        while cambios[j] < i:
            j += 1
        fin[i] = cambios[j]
    ses_ini = np.zeros(n, np.int64)
    first = np.r_[0, np.nonzero(ses_id[1:] != ses_id[:-1])[0] + 1]
    k = 0
    for i in range(n):
        while k + 1 < len(first) and first[k + 1] <= i:
            k += 1
        ses_ini[i] = first[k]
    return dict(h=S.h.astype(float), l=S.l.astype(float), c=S.c.astype(float), o=o, vol=vol, dl=dl,
                cdl=np.r_[0.0, np.cumsum(dl)], cvol=np.r_[0.0, np.cumsum(vol)], a10=a10, a60=a60, volm=volm, vw=vw,
                shi=shi, slo=slo, mins=mins, rth=S.rth.astype(np.float64), fin=fin, ses_ini=ses_ini,
                blq_ini=S.ini.astype(np.int64), escala=float(escala))


@nb.njit(cache=True)
def rasgos(t, d, E, iE, li, cf, h, l, c, o, vol, cdl, cvol, a10, a60, volm, vw, shi, slo, mins, rth, ses_ini, escala, out):
    """20 rasgos de la vela t para la corriente d con extremo E (vela iE), tramo iniciado en li con cierre cf."""
    A = a60[t]
    r = d * (E - c[t])
    k = t - iE
    out[0] = r / A
    out[1] = r / escala
    out[2] = k
    out[3] = (r / A) / max(k, 1)
    out[4] = d * (E - cf) / A
    out[5] = iE - li
    out[6] = a10[t] / A
    out[7] = A / escala
    vm = volm[t] if volm[t] > 0 else 1.0
    out[8] = -d * (cdl[t + 1] - cdl[iE + 1]) / vm
    out[9] = ((cvol[t + 1] - cvol[iE + 1]) / k / vm) if k > 0 else 0.0
    rg = h[t] - l[t]
    out[10] = (-d * (c[t] - o[t]) / rg) if rg > 0 else 0.0
    if rg > 0:
        out[11] = (h[t] - c[t]) / rg if d == 1 else (c[t] - l[t]) / rg
    else:
        out[11] = 0.5
    out[12] = (-d * (cdl[t + 1] - cdl[t]) / vol[t]) if vol[t] > 0 else 0.0
    out[13] = -d * (c[t] - vw[t]) / A
    t10 = t - 10
    out[14] = (-d * (vw[t] - vw[t10]) / A) if t10 >= ses_ini[t] else 0.0
    out[15] = mins[t]
    out[16] = rth[t]
    out[17] = (shi[t] - c[t]) / A
    out[18] = (c[t] - slo[t]) / A
    cc = 0
    for q in range(max(t - 4, ses_ini[t] + 1), t + 1):
        if -d * (c[q] - c[q - 1]) > 0:
            cc += 1
    out[19] = cc


@nb.njit(cache=True)
def etiqueta(t, d, E, Apts, h, l, c, fin):
    obj = c[t] - d * Apts
    tope = min(t + 120, fin[t])
    for j in range(t + 1, tope + 1):
        if d == 1:
            ht, hb = l[j] <= obj, h[j] >= E
        else:
            ht, hb = h[j] >= obj, l[j] <= E
        if ht and hb:
            return 0
        if ht:
            return 1
        if hb:
            return 0
    return 0


@nb.njit(cache=True)
def filas_ref(st, val, blq_ini, h, l, c, o, vol, cdl, cvol, a10, a60, volm, vw, shi, slo, mins, rth, ses_ini, fin, escala, Apts):
    """Filas con los tramos de la corriente de referencia st (ST(10,3))."""
    n = len(c)
    X = np.zeros((n, 20))
    y = np.zeros(n, np.int8)
    idx = np.zeros(n, np.int64)
    dd = np.zeros(n, np.int8)
    m = 0
    out = np.zeros(20)
    d = 0
    E = 0.0
    iE = li = 0
    cf = 0.0
    for t in range(n):
        if t == blq_ini[t] or st[t] == 0:
            d = 0
            continue
        if d == 0 or st[t] != d:
            d = st[t]
            E = h[t] if d == 1 else l[t]
            iE = li = t
            cf = c[t]
            continue
        if d == 1 and h[t] > E:
            E, iE = h[t], t
        elif d == -1 and l[t] < E:
            E, iE = l[t], t
        if not val[t] or a60[t] != a60[t] or volm[t] != volm[t]:
            continue
        if d * (E - c[t]) <= 0:
            continue
        rasgos(t, d, E, iE, li, cf, h, l, c, o, vol, cdl, cvol, a10, a60, volm, vw, shi, slo, mins, rth, ses_ini, escala, out)
        X[m] = out
        y[m] = etiqueta(t, d, E, Apts, h, l, c, fin)
        idx[m] = t
        dd[m] = d
        m += 1
    return X[:m], y[:m], idx[:m], dd[:m]


# ------------------------------------------------------------------ modelos en forma numerica (para simular y para el C#)
def m1_x(X):
    r = X[:, 0]
    return np.c_[r, r * r, np.log1p(r), X[:, 2]]


class Logit:
    def __init__(self, X, y):
        self.mu, self.sd = X.mean(0), X.std(0) + 1e-12
        self.m = LogisticRegression(C=1.0, max_iter=2000).fit((X - self.mu) / self.sd, y)
        self.w = self.m.coef_[0] / self.sd
        self.b = float(self.m.intercept_[0] - (self.m.coef_[0] * self.mu / self.sd).sum())

    def p(self, X):
        return 1 / (1 + np.exp(-(X @ self.w + self.b)))


def arboles(model):
    """HistGradientBoosting -> arreglos planos de nodos (para numba y para exportar a C#)."""
    feats, thr, left, right, leaf, val, miss, raiz = [], [], [], [], [], [], [], []
    off = 0
    for it in model._predictors:
        nodes = it[0].nodes
        raiz.append(off)
        feats.append(nodes["feature_idx"].astype(np.int64))
        thr.append(nodes["num_threshold"].astype(float))
        left.append(nodes["left"].astype(np.int64) + off)
        right.append(nodes["right"].astype(np.int64) + off)
        leaf.append(nodes["is_leaf"].astype(np.bool_))
        val.append(nodes["value"].astype(float))
        miss.append(nodes["missing_go_to_left"].astype(np.bool_))
        off += len(nodes)
    return dict(f=np.concatenate(feats), t=np.concatenate(thr), l=np.concatenate(left), r=np.concatenate(right),
                leaf=np.concatenate(leaf), v=np.concatenate(val), miss=np.concatenate(miss), raiz=np.array(raiz, np.int64),
                base=float(np.ravel(model._baseline_prediction)[0]))


@nb.njit(cache=True)
def gbm_p(x, f, t, l, r, leaf, v, miss, raiz, base):
    s = base
    for k in range(len(raiz)):
        i = raiz[k]
        while not leaf[i]:
            xv = x[f[i]]
            if xv != xv:
                i = l[i] if miss[i] else r[i]
            elif xv <= t[i]:
                i = l[i]
            else:
                i = r[i]
        s += v[i]
    return 1.0 / (1.0 + np.exp(-s))


@nb.njit(cache=True)
def simular(tipo, theta, w, b, f, tt, ll, rr, leaf, v, miss, raiz, base, st0, val, blq_ini,
            h, l, c, o, vol, cdl, cvol, a10, a60, volm, vw, shi, slo, mins, rth, ses_ini, escala, Apts):
    """Regla P(theta) vela a vela con sus propios tramos. tipo 0 = M0, 1 = M1 (w,b sobre r, r2, log, k), 2 = M2 (w,b sobre
    los 20 rasgos), 3 = M3 (arboles). Arranca cada bloque con la direccion de st0."""
    n = len(c)
    st = np.zeros(n, np.int8)
    out = np.zeros(20)
    x1 = np.zeros(4)
    d = 0
    E = 0.0
    iE = li = 0
    cf = 0.0
    for t in range(n):
        if t == blq_ini[t] or st0[t] == 0:
            d = 0
            st[t] = st0[t]
            continue
        if d == 0:
            d = st0[t]
            E = h[t] if d == 1 else l[t]
            iE = li = t
            cf = c[t]
            st[t] = d
            continue
        if d == 1 and h[t] > E:
            E, iE = h[t], t
        elif d == -1 and l[t] < E:
            E, iE = l[t], t
        r = d * (E - c[t])
        if r > 0 and t != li and a60[t] == a60[t] and volm[t] == volm[t]:
            if tipo == 0:
                pp = r / (r + Apts)
            else:
                rasgos(t, d, E, iE, li, cf, h, l, c, o, vol, cdl, cvol, a10, a60, volm, vw, shi, slo, mins, rth, ses_ini, escala, out)
                if tipo == 1:
                    x1[0] = out[0]
                    x1[1] = out[0] * out[0]
                    x1[2] = np.log1p(out[0])
                    x1[3] = out[2]
                    z = b
                    for q in range(4):
                        z += w[q] * x1[q]
                    pp = 1.0 / (1.0 + np.exp(-z))
                elif tipo == 2:
                    z = b
                    for q in range(20):
                        z += w[q] * out[q]
                    pp = 1.0 / (1.0 + np.exp(-z))
                else:
                    pp = gbm_p(out, f, tt, ll, rr, leaf, v, miss, raiz, base)
            if pp >= theta:
                d = -d
                E = h[t] if d == 1 else l[t]
                iE = li = t
                cf = c[t]
        st[t] = d
    return st


# ------------------------------------------------------------------ series
SM = Serie(serie_mes(), 20.0, 30, 10.0, 15, 5.0)
SQ = Serie(serie_mnq(), 80.0, 30, 40.0, 15, 20.0)
AM, AQ = arreglos(SM, 1.0), arreglos(SQ, 4.0)
ORDEN = ["h", "l", "c", "o", "vol", "cdl", "cvol", "a10", "a60", "volm", "vw", "shi", "slo", "mins", "rth", "ses_ini"]


def datos(S, A, Apts):
    st = T.det_supertrend(S.h, S.l, S.c, S.blq, 10, 3.0).astype(np.int8)
    X, y, idx, dd = filas_ref(st, S.val, A["blq_ini"], *[A[k] for k in ORDEN], A["fin"], A["escala"], Apts)
    D = pd.DataFrame(X, columns=RASGOS)
    D["y"], D["i"], D["d"] = y, idx, dd
    D["sesion"] = S.ses[idx]
    D["tramo"] = S.tramo[idx]
    return D, st


DM, stM = datos(SM, AM, 10.0)
DQ, stQ = datos(SQ, AQ, 40.0)
DM.to_parquet(LAB / "pred_filas_MES.parquet", index=False)
DQ.to_parquet(LAB / "pred_filas_MNQ.parquet", index=False)
for nom, D in (("MES", DM), ("MNQ", DQ)):
    p(f"{nom}: {len(D)} filas, tasa de acierto (y=1) {D.y.mean() * 100:.1f} %; por tramo: "
      + ", ".join(f"{t_} {len(g)} ({g.y.mean() * 100:.1f} %)" for t_, g in D.groupby("tramo")))


# ------------------------------------------------------------------ la geometria contra el dato: P(acierto | r)
def curva_r(D, Apts, etiqueta_):
    D = D.assign(rp=D.r_pts * (4.0 if etiqueta_ == "MNQ" else 1.0))
    bins = [0, 1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 30]
    if etiqueta_ == "MNQ":
        bins = [b * 4 for b in bins]
    D["fr"] = pd.cut(D.rp, bins)
    g = D.groupby("fr", observed=True).agg(n=("y", "size"), real=("y", "mean"), rm=("rp", "mean"))
    g["azar"] = g.rm / (g.rm + Apts)
    p(f"\n{etiqueta_}: P(sigue {Apts:g} pts mas antes de volver al extremo | retroceso r)  real contra azar r/(r+A):")
    for k_, r_ in g.iterrows():
        p(f"  r {str(k_):<12} n {int(r_.n):>6}  real {r_.real * 100:5.1f} %  azar {r_.azar * 100:5.1f} %  diferencia {100 * (r_.real - r_.azar):+5.1f}")


curva_r(DM[DM.tramo == "DISENO"], 10.0, "MES")
curva_r(DM[DM.tramo == "EXAMEN"], 10.0, "MES")
curva_r(DQ, 40.0, "MNQ")


# ------------------------------------------------------------------ modelos
def ajustar(Dtr):
    X = Dtr[RASGOS].values
    y = Dtr.y.values
    m1 = Logit(m1_x(X), y)
    m2 = Logit(X, y)
    m3 = HistGradientBoostingClassifier(max_iter=300, learning_rate=0.05, max_leaf_nodes=31, min_samples_leaf=200,
                                        l2_regularization=1.0, random_state=0).fit(X, y)
    return m1, m2, m3


def predecir(ms, D, Apts):
    X = D[RASGOS].values
    m1, m2, m3 = ms
    rp = D.r_pts.values * (4.0 if Apts == 40.0 else 1.0)
    return dict(M0=rp / (rp + Apts), M1=m1.p(m1_x(X)), M2=m2.p(X), M3=m3.predict_proba(X)[:, 1])


def medidas(P, D, titulo):
    y = D.y.values
    p(f"\n{titulo} ({len(D)} filas, acierto {y.mean() * 100:.1f} %):")
    for k_, pr in P.items():
        pr = np.clip(pr, 1e-6, 1 - 1e-6)
        txt = f"  {k_}: AUC {roc_auc_score(y, pr):.4f}  log-loss {log_loss(y, pr):.4f}"
        for a_, b_ in ((0, 1), (1, 2), (2, 3)):
            s = (D.r_atr.values >= a_) & (D.r_atr.values < b_)
            if s.sum() > 200 and 0 < y[s].mean() < 1:
                txt += f" | r {a_}-{b_} ATR AUC {roc_auc_score(y[s], pr[s]):.4f} (n {s.sum()})"
        p(txt)


def auc_cond(y, pr, r):
    vals = []
    for a_, b_ in ((0, 1), (1, 2), (2, 3)):
        s = (r >= a_) & (r < b_)
        if s.sum() > 200 and 0 < y[s].mean() < 1:
            vals.append(roc_auc_score(y[s], pr[s]))
    return float(np.mean(vals))


dis = DM[DM.tramo == "DISENO"].reset_index(drop=True)
exa = DM[DM.tramo == "EXAMEN"].reset_index(drop=True)
# validacion cruzada por sesiones dentro de DISEÑO (5 grupos) para elegir M2 o M3 y para simular DISEÑO fuera de pliegue
ses_d = np.array(sorted(dis.sesion.unique()))
grupo = {s: i % 5 for i, s in enumerate(ses_d)}
dis["g"] = dis.sesion.map(grupo)
oof = {k_: np.zeros(len(dis)) for k_ in ("M0", "M1", "M2", "M3")}
modelos_g = {}
for g_ in range(5):
    tr, te = dis.g != g_, dis.g == g_
    ms = ajustar(dis[tr])
    modelos_g[g_] = ms
    P = predecir(ms, dis[te], 10.0)
    for k_ in oof:
        oof[k_][te.values] = P[k_]
medidas(oof, dis, "MES DISEÑO, validacion cruzada por sesiones (fuera de pliegue)")
ac2 = auc_cond(dis.y.values, oof["M2"], dis.r_atr.values)
ac3 = auc_cond(dis.y.values, oof["M3"], dis.r_atr.values)
MEJOR = "M2" if ac2 >= ac3 else "M3"
p(f"AUC condicional media en DISEÑO: M2 {ac2:.4f}, M3 {ac3:.4f} -> se usa {MEJOR}")
MS = ajustar(dis)
medidas(predecir(MS, exa, 10.0), exa, "MES EXAMEN (modelo entrenado en todo DISEÑO)")
medidas(predecir(MS, DQ, 40.0), DQ, "MNQ con el modelo de MES (todo en ATR; r y ATR en pts / 4)")
# modelo propio de MNQ por validacion cruzada
sq = np.array(sorted(DQ.sesion.unique()))
DQ["g"] = DQ.sesion.map({s: i % 5 for i, s in enumerate(sq)})
oofq = {k_: np.zeros(len(DQ)) for k_ in ("M0", "M1", "M2", "M3")}
for g_ in range(5):
    tr, te = DQ.g != g_, DQ.g == g_
    P = predecir(ajustar(DQ[tr]), DQ[te], 40.0)
    for k_ in oofq:
        oofq[k_][te.values] = P[k_]
medidas(oofq, DQ, "MNQ con modelo propio, validacion cruzada por sesiones")
# importancia (permutacion simple sobre EXAMEN con el mejor)
pe = predecir(MS, exa, 10.0)[MEJOR]
base_auc = roc_auc_score(exa.y, pe)
imp = []
rng = np.random.default_rng(0)
for j_, nom in enumerate(RASGOS):
    Xp = exa[RASGOS].values.copy()
    Xp[:, j_] = rng.permutation(Xp[:, j_])
    pr = MS[1].p(Xp) if MEJOR == "M2" else MS[2].predict_proba(Xp)[:, 1]
    imp.append((nom, base_auc - roc_auc_score(exa.y, pr)))
p("importancia en EXAMEN (caida del AUC al mezclar el rasgo): " + ", ".join(f"{a} {b:+.4f}" for a, b in sorted(imp, key=lambda z: -z[1])[:10]))


# ------------------------------------------------------------------ reglas P(theta)
def parametros(tipo, ms):
    vacio = (np.zeros(1), 0.0, np.zeros(1, np.int64), np.zeros(1), np.zeros(1, np.int64), np.zeros(1, np.int64),
             np.zeros(1, np.bool_), np.zeros(1), np.zeros(1, np.bool_), np.zeros(1, np.int64), 0.0)
    if tipo == 0:
        return vacio
    if tipo == 1:
        return (ms[0].w, ms[0].b) + vacio[2:]
    if tipo == 2:
        return (ms[1].w, ms[1].b) + vacio[2:]
    a = arboles(ms[2])
    return (np.zeros(1), 0.0, a["f"], a["t"], a["l"], a["r"], a["leaf"], a["v"], a["miss"], a["raiz"], a["base"])


# control: los arboles en numba == predict_proba
_a = arboles(MS[2])
_x = exa[RASGOS].values[:2000]
_pn = np.array([gbm_p(_x[i], _a["f"], _a["t"], _a["l"], _a["r"], _a["leaf"], _a["v"], _a["miss"], _a["raiz"], _a["base"]) for i in range(len(_x))])
p(f"control arboles numba vs sklearn: diferencia maxima {np.abs(_pn - MS[2].predict_proba(_x)[:, 1]).max():.2e}")
_l2 = MS[1].p(_x)
p(f"control logistica plana vs sklearn: diferencia maxima {np.abs(_l2 - MS[1].m.predict_proba((_x - MS[1].mu) / MS[1].sd)[:, 1]).max():.2e}")

THETAS = [0.35, 0.4, 0.45, 0.5, 0.55, 0.6, 0.65, 0.7]
TIPO = {"M0": 0, "M1": 1, "M2": 2, "M3": 3}


def correr(S, A, st0, tipo, theta, ms, Apts):
    pars = parametros(tipo, ms)
    return simular(tipo, theta, *pars, st0, S.val, A["blq_ini"], *[A[k] for k in ORDEN], A["escala"], Apts)


def colas(S, st, M):
    pts = S.retraso(st, M)
    full = pts.copy()
    for q in np.nonzero(np.isinf(pts))[0]:
        i, s, e = M.i.values[q], M.sentido.values[q], M.extremo.values[q]
        j1 = min(len(S.c), S.fin[i], i + 121)
        full[q] = (S.h[i:j1].max() - e) if s == 1 else (e - S.l[i:j1].min())
    return pts, full


filas, movs = [], []


def registrar(nombre, serie, S, st, tramos):
    rz = S.ruido(st)
    pts, full = colas(S, st, S.M)
    for tr_ in tramos:
        s1 = S.M.tramo.values == tr_
        filas.append(dict(serie=serie, regla=nombre, tramo=tr_, n=int(s1.sum()), tarde_med=float(np.median(pts[s1])),
                          tarde_media=float(full[s1].mean()), p75=float(np.percentile(full[s1], 75)),
                          p90=float(np.percentile(full[s1], 90)), **rz[tr_]))
        for q in np.nonzero(s1)[0]:
            movs.append((serie, nombre, tr_, int(q), bool(S.M.volatil.values[q]), float(pts[q]), float(full[q])))


# referencias
for nom, (n_, m_, vv, jj) in {"B ST(10,3)": (10, 3.0, None, None), "1.1 ST(60,4)+rapida": (60, 4.0, 2.5, 3),
                              "ST(60,3)": (60, 3.0, None, None), "ST(10,2.5)": (10, 2.5, None, None),
                              "ST(10,2)": (10, 2.0, None, None), "ST(60,2.5)": (60, 2.5, None, None)}.items():
    for S, serie, tr in ((SM, "MES", ("DISENO", "EXAMEN")), (SQ, "MNQ", ("MNQ",))):
        st = corriente(S, S.atr(n_), m_, v=vv, j=jj) if vv else corriente(S, S.atr(n_), m_)
        registrar(nom, serie, S, st, tr)
    print("referencia", nom, flush=True)

# DISEÑO fuera de pliegue: cada sesion se simula con el modelo que no la vio (tramos de la propia regla)
ses_vela = SM.ses
for mod in ("M0", "M1", MEJOR):
    for th in THETAS:
        nombre = f"P {mod} {th:g}"
        st_dis = np.zeros(len(SM.c), np.int8)
        if mod == "M0":
            st_dis = correr(SM, AM, stM, 0, th, None, 10.0)
        else:
            for g_ in range(5):
                stg = correr(SM, AM, stM, TIPO[mod], th, modelos_g[g_], 10.0)
                ses_g = [s for s in ses_d if grupo[s] == g_]
                sel = np.isin(ses_vela, np.array(ses_g, dtype=ses_vela.dtype))
                st_dis[sel] = stg[sel]
            # las velas de EXAMEN de st_dis no se usan: se miden con la corrida del modelo completo
        registrar(nombre, "MES", SM, st_dis, ("DISENO",))
        st_ex = correr(SM, AM, stM, TIPO[mod], th, MS, 10.0)
        registrar(nombre, "MES", SM, st_ex, ("EXAMEN",))
        st_q = correr(SQ, AQ, stQ, TIPO[mod], th, MS, 40.0)
        registrar(nombre, "MNQ", SQ, st_q, ("MNQ",))
        print(nombre, flush=True)

R = pd.DataFrame(filas)
R.to_csv(LAB / "pred_reglas.csv", index=False)
pd.DataFrame(movs, columns=["serie", "regla", "tramo", "mov", "volatil", "pts", "full"]).to_csv(LAB / "pred_movs.csv", index=False)
with open(LAB / "pred_modelos.pkl", "wb") as fh:
    pickle.dump(dict(MS=MS, MEJOR=MEJOR, RASGOS=RASGOS, arboles=arboles(MS[2])), fh)
p("\nguardado pred_reglas.csv, pred_movs.csv y pred_modelos.pkl")
