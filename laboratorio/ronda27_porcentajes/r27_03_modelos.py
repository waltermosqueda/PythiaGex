# -*- coding: utf-8 -*-
"""RONDA 27, paso 3: los modelos M0-M3 en WALK-FORWARD por rueda (ventana creciente) con hiperparametros anidados (PREREGISTRO secciones 5-7).
Para cada rueda de prueba d: entrena con TODAS las ruedas evaluadas anteriores y predice P(arriba primero) en sus 389 cierres de minuto.
Cada 5 ruedas de prueba (indices 10, 15, 20, 25, 30, 33, 38, 43, 47, 52 de las 55) elige los hiperparametros con 4 bloques contiguos de ruedas del
entrenamiento (log-loss de las resueltas; empate -> lo mas simple, el nulo primero) y, con las predicciones fuera de pliegue, el umbral tau de la
regla R-frec (el menor tau de 0,500-0,700 que da <= 3 operaciones por rueda en el entrenamiento).
Todos los modelos son ANTISIMETRICOS (espejo arriba <-> abajo): logisticas sin intercepto con rasgos impares; XGBoost entrenado con cada fila y su
espejo y prediccion simetrizada. Ademas M_deriva (la frecuencia de 'arriba' del entrenamiento como prediccion constante).
Salidas (PythiaGex/datos/ronda27/pred): pred-<tramo>-<Y>.parquet (dia, m, modelo, p) y sel-<tramo>-<Y>.json (lo elegido en cada punto).
Uso: python -I r27_03_modelos.py --tramo diseno|confirmacion|final [--Y X Xm X2] [--modelos M0 M1 M2 M3]"""
import argparse, json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r27_comun as K
import numpy as np, pandas as pd

TAB = os.path.join(K.DATOS, "tabla_minutos.parquet")
PRED = os.path.join(K.DATOS, "pred")
os.makedirs(PRED, exist_ok=True)
SEL_IDX = (10, 15, 20, 25, 30, 33, 38, 43, 47, 52)
TRAMOS = {"diseno": (10, 33), "confirmacion": (33, 47), "final": (47, 55)}      # [desde, hasta) en indices de rueda
C_GRID = [None, 0.0003, 0.001, 0.003, 0.01, 0.03, 0.1, 1.0]                     # None = nulo (50 %)
XGB_GRID = [None] + [dict(max_depth=d, n_estimators=n, min_child_weight=w) for d in (2, 3) for n in (60, 150) for w in (50, 200)]
TAU_GRID = np.round(np.arange(0.500, 0.7001, 0.005), 3)
M_DESDE, M_HASTA, MAX_DIA = 4, 359, 5                                          # cierres 9:35 .. 15:30
SEMILLA = 27

# ------------------------------------------------------------------------------------------------ rasgos
M2_RASGOS = ["o_dir", "o_ret", "o_lar", "o_min", "o_dnorm", "m_dir", "m_ret", "m_lar", "m_min", "m_dnorm", "rrn", "tipo_n", "lado_T",
             "vw_X", "ib_net", "mins", "d1", "d5", "d15", "med5", "med15", "big5", "ice1", "zona_o_s"]
M2_IMPARES = {"o_dir", "o_dnorm", "m_dir", "m_dnorm", "lado_T", "vw_X", "ib_net", "d1", "d5", "d15", "med5", "med15", "big5", "ice1", "zona_o_s"}
M2_MONO = {"lado_T": 1}


def preparar(T):
    T = T.copy()
    T["isT"] = (T["tipo"] == "T").astype(float)
    T["tipo_n"] = T["tipo"].map({"T": 3.0, "R": 2.0, "E": 1.0, "N": 0.0})
    T["rrn"] = np.minimum(T["rrn"], 4.0)
    T["zona_o_s"] = np.maximum(T["zona_o"], T["barr_o"]) * T["o_dir"]
    return T


def matriz(T, modelo):
    """Rasgos impares (logisticas) o crudos (M2)."""
    if modelo == "M0":
        cols = {"o_dir": T.o_dir, "o_dir*o_ret": T.o_dir * T.o_ret, "o_dir*o_lar": T.o_dir * T.o_lar,
                "m_dir": T.m_dir, "m_dir*m_ret": T.m_dir * T.m_ret, "m_dir*m_lar": T.m_dir * T.m_lar}
    elif modelo == "M1":
        cols = {"m_dir": T.m_dir, "m_dir*T": T.m_dir * T.isT, "o_dir": T.o_dir, "o_dir*T": T.o_dir * T.isT, "lado_T": T.lado_T,
                "o_dir*o_ret": T.o_dir * T.o_ret, "vw_X": T.vw_X, "vw_X*tarde": T.vw_X * T.tarde, "d5": T.d5, "d5*T": T.d5 * T.isT,
                "med15": T.med15, "zona_o_s*T": T.zona_o_s * T.isT}
    elif modelo == "M3":
        cols = {"v_T7": T.v_T7, "v_V4": T.v_V4, "v_A3": T.v_A3}
    elif modelo == "M2":
        cols = {c: T[c] for c in M2_RASGOS}
    else:
        raise ValueError(modelo)
    X = np.column_stack([np.asarray(v, float) for v in cols.values()])
    return np.nan_to_num(X), list(cols)


def espejo_M2(X):
    Xs = X.copy()
    for j, c in enumerate(M2_RASGOS):
        if c in M2_IMPARES:
            Xs[:, j] = -Xs[:, j]
    return Xs


# ------------------------------------------------------------------------------------------------ ajuste y prediccion
class Logistica:
    def __init__(s, C):
        s.C = C; s.coef = None; s.rms = None

    def ajustar(s, X, y):
        if s.C is None:
            return s
        from sklearn.linear_model import LogisticRegression
        s.rms = np.sqrt(np.mean(X * X, axis=0)); s.rms[s.rms <= 0] = 1.0
        lr = LogisticRegression(C=s.C, fit_intercept=False, solver="lbfgs", max_iter=500)
        lr.fit(X / s.rms, y.astype(int))
        s.coef = lr.coef_[0].copy()
        return s

    def predecir(s, X):
        if s.C is None or s.coef is None:
            return np.full(len(X), 0.5)
        z = (X / s.rms) @ s.coef
        return 1.0 / (1.0 + np.exp(-z))

    def exportar(s, nombres):
        return {"tipo": "logistica", "C": s.C, "rasgos": nombres, "rms": None if s.rms is None else s.rms.tolist(),
                "coef": None if s.coef is None else s.coef.tolist(), "formula": "p = 1 / (1 + exp(-sum(coef_i * x_i / rms_i)))"}


class Arboles:
    def __init__(s, hp):
        s.hp = hp; s.bst = None

    def ajustar(s, X, y):
        if s.hp is None:
            return s
        import xgboost as xgb
        Xs = espejo_M2(X)
        XX = np.vstack([X, Xs]); yy = np.r_[y, 1 - y].astype(int)
        mono = "(" + ",".join(str(M2_MONO.get(c, 0)) for c in M2_RASGOS) + ")"
        s.bst = xgb.XGBClassifier(n_estimators=s.hp["n_estimators"], max_depth=s.hp["max_depth"], min_child_weight=s.hp["min_child_weight"],
                                  learning_rate=0.05, subsample=0.8, colsample_bytree=0.8, reg_lambda=10.0, base_score=0.5,
                                  monotone_constraints=mono, tree_method="hist", n_jobs=2, random_state=SEMILLA, verbosity=0)
        s.bst.fit(XX, yy)
        return s

    def predecir(s, X):
        if s.hp is None or s.bst is None:
            return np.full(len(X), 0.5)
        a = s.bst.predict_proba(X)[:, 1]; b = s.bst.predict_proba(espejo_M2(X))[:, 1]
        return (a + 1.0 - b) / 2.0


def nuevo(modelo, hp):
    return Arboles(hp) if modelo == "M2" else Logistica(hp)


def grilla(modelo):
    return XGB_GRID if modelo == "M2" else C_GRID


def logloss(p, y):
    p = np.clip(p, 1e-6, 1 - 1e-6)
    return float(-np.mean(y * np.log(p) + (1 - y) * np.log(1 - p)))


# ------------------------------------------------------------------------------------------------ regla
def operar_dia(g, p, tau, Yk):
    """g: filas de UNA rueda ordenadas por m; p: P(arriba). Devuelve la lista de operaciones (m, lado, p, bruto, salida_ns)."""
    ops = []; libre = -1
    m = g["m"].to_numpy(); T = g["T"].to_numpy(); pL = g["pL_" + Yk].to_numpy(); pS = g["pS_" + Yk].to_numpy()
    tL = g["tL_" + Yk].to_numpy(); tS = g["tS_" + Yk].to_numpy()
    for i in range(len(m)):
        if m[i] < M_DESDE or m[i] > M_HASTA or T[i] < libre:
            continue
        if len(ops) >= MAX_DIA:
            break
        if p[i] >= tau and p[i] > 0.5:
            s = 1; bruto = pL[i]; sal = tL[i]
        elif p[i] <= 1 - tau and p[i] < 0.5:
            s = -1; bruto = pS[i]; sal = tS[i]
        else:
            continue
        if not np.isfinite(bruto):
            continue
        ops.append((int(m[i]), s, float(p[i]), float(bruto), int(sal)))
        libre = sal
    return ops


def elegir_tau(G, P, Yk, meta=3.0):
    """El menor tau de la grilla con <= meta operaciones por rueda en promedio (predicciones fuera de pliegue del entrenamiento)."""
    dias = list(G)
    for tau in TAU_GRID:
        n = sum(len(operar_dia(G[d], P[d], tau, Yk)) for d in dias)
        if n / len(dias) <= meta:
            return float(tau)
    return float(TAU_GRID[-1])


# ------------------------------------------------------------------------------------------------ seleccion anidada
def seleccionar(modelo, D, Yk, por_dia):
    """Validacion interna con 4 bloques contiguos de las ruedas D. Devuelve (hp, logloss por hp, tau, oof por rueda)."""
    bloques = np.array_split(np.arange(len(D)), 4)
    res = []
    oofs = {}
    for hp in grilla(modelo):
        pred = {}
        for b in bloques:
            test = [D[i] for i in b]; train = [D[i] for i in range(len(D)) if i not in set(b)]
            Xtr = np.vstack([por_dia[d]["X"][por_dia[d]["ok"]] for d in train]); ytr = np.concatenate([por_dia[d]["y"][por_dia[d]["ok"]] for d in train])
            mdl = nuevo(modelo, hp).ajustar(Xtr, ytr)
            for d in test:
                pred[d] = mdl.predecir(por_dia[d]["X"])
        pp = np.concatenate([pred[d][por_dia[d]["ok"]] for d in D]); yy = np.concatenate([por_dia[d]["y"][por_dia[d]["ok"]] for d in D])
        res.append((hp, logloss(pp, yy)))
        oofs[json.dumps(hp)] = pred
    best = min(range(len(res)), key=lambda i: (round(res[i][1], 6), i))       # empate -> el primero de la grilla (lo mas simple)
    hp = res[best][0]
    oof = oofs[json.dumps(hp)]
    tau = elegir_tau({d: por_dia[d]["g"] for d in D}, oof, Yk)
    return hp, [(json.dumps(h), round(l, 6)) for h, l in res], tau, oof


def correr(tramo, Ys, modelos):
    T = preparar(pd.read_parquet(TAB))
    dias = sorted(T["dia"].unique())
    assert len(dias) == 55, len(dias)
    a, b = TRAMOS[tramo]
    t0 = time.time()
    for Yk in Ys:
        filas = []; sel = {}
        for modelo in modelos + ["M_deriva"]:
            por_dia = {}
            for d, g in T.groupby("dia"):
                g = g.sort_values("m")
                y = g["y_" + Yk].to_numpy()
                X, nombres = matriz(g, modelo if modelo != "M_deriva" else "M3")
                por_dia[d] = {"X": X, "y": np.nan_to_num(y), "ok": np.isfinite(y), "g": g}
            hp = None; tau = None; elegido = False
            for i in range(a, b):
                K.esperar_ram(2.5)
                d = dias[i]; train = dias[:i]
                if modelo == "M_deriva":
                    ytr = np.concatenate([por_dia[x]["y"][por_dia[x]["ok"]] for x in train])
                    p = np.full(len(por_dia[d]["y"]), float(ytr.mean()))
                    filas.append(pd.DataFrame({"dia": d, "m": por_dia[d]["g"]["m"].to_numpy(), "modelo": modelo, "p": p, "tau": np.nan}))
                    continue
                if i in SEL_IDX or not elegido:
                    elegido = True
                    hp, tabla_ll, tau, _ = seleccionar(modelo, train, Yk, por_dia)
                    sel.setdefault(modelo, []).append({"indice": i, "dia": d, "hp": hp, "tau": tau, "logloss": tabla_ll})
                    print("  %s %s %s sel en %s: hp %s tau %.3f (%.0f s)" % (tramo, Yk, modelo, d, json.dumps(hp), tau, time.time() - t0), flush=True)
                Xtr = np.vstack([por_dia[x]["X"][por_dia[x]["ok"]] for x in train]); ytr = np.concatenate([por_dia[x]["y"][por_dia[x]["ok"]] for x in train])
                mdl = nuevo(modelo, hp).ajustar(Xtr, ytr)
                p = mdl.predecir(por_dia[d]["X"])
                filas.append(pd.DataFrame({"dia": d, "m": por_dia[d]["g"]["m"].to_numpy(), "modelo": modelo, "p": p, "tau": tau}))
        out = pd.concat(filas, ignore_index=True)
        rp = os.path.join(PRED, "pred-%s-%s.parquet" % (tramo, Yk)); rs = os.path.join(PRED, "sel-%s-%s.json" % (tramo, Yk))
        if os.path.exists(rp):          # se suman a los modelos ya corridos de ese tramo (se reemplazan solo los que se corrieron ahora)
            viejo = pd.read_parquet(rp)
            out = pd.concat([viejo[~viejo.modelo.isin(out.modelo.unique())], out], ignore_index=True)
        if os.path.exists(rs):
            sv = json.load(open(rs)); sv.update(sel); sel = sv
        out.to_parquet(rp, index=False)
        json.dump(sel, open(rs, "w"), indent=1)
        print("%s %s listo: %d filas | %.0f s" % (tramo, Yk, len(out), time.time() - t0), flush=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tramo", required=True, choices=list(TRAMOS))
    ap.add_argument("--Y", nargs="+", default=["X", "Xm", "X2"])
    ap.add_argument("--modelos", nargs="+", default=["M0", "M1", "M2", "M3"])
    a = ap.parse_args()
    correr(a.tramo, a.Y, a.modelos)


if __name__ == "__main__":
    main()
