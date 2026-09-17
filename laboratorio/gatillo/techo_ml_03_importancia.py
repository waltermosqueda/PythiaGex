# -*- coding: utf-8 -*-
"""techo_ml_03_importancia.py — importancia por permutacion FUERA DE MUESTRA (pre-registrada para V01 y V08; se agrega V09, la no direccional),
por rasgo y por GRUPO de rasgos, y una tabla descriptiva: AUC de cada rasgo SOLO (sin ajustar nada) contra la barrera +-8/600, dia por dia.
Solo explorar."""
import os, sys, time, json
import numpy as np, pandas as pd
import techo_ml_lib as L, base as B

t0 = time.time()
T = B.todas_las_sesiones(); N = B.niveles_m1(); E = B.explorar(T)
df = L.tablero(E, N, "explorar"); todos, sin_gamma = L.columnas(df); ses = df["sesion"].to_numpy(); dias = np.unique(ses)


def grupo_de(c):
    if c.startswith(("dr_", "zD_", "zO_", "zP_", "absn", "abst", "romn", "barn", "bart", "dch", "dme", "dgr", "cvdr", "div_")): return "flujo"
    if c.startswith(("ret", "ser_", "er_", "pos_", "d_apertura", "vwap")): return "precio"
    if c.startswith(("rng_", "rv_", "comp_", "act", "l_n_", "l_vol", "tam_")): return "volatilidad_y_velocidad"
    if c.startswith("mins"): return "reloj"
    if c.startswith(("qi", "spread", "prof")): return "punta"
    if c.startswith("g_"): return "gamma"
    return "otro"


def importancia(cols, y01, valido, fabrica, clase, reps=5, etiqueta=""):
    """Ajusta vuelta por vuelta; en la sesion de afuera baraja un rasgo (o un grupo entero) y mide la caida del AUC conjunto fuera de muestra."""
    X = df[cols].to_numpy(dtype="float32"); rng = np.random.default_rng(11); modelos = {}
    base_p = np.full(len(df), np.nan)
    for s in dias:
        tr = (ses != s) & valido; m = fabrica(); m.fit(X[tr], y01[tr]); modelos[s] = m
        te = ses == s; base_p[te] = m.predict_proba(X[te])[:, 1] if clase else m.predict(X[te])
    ev = valido; obj = objetivo_auc[ev]; a0 = L.auc(obj, base_p[ev]); print("%s AUC base fuera de muestra %.4f" % (etiqueta, a0))
    grupos = {}
    for j, c in enumerate(cols): grupos.setdefault(grupo_de(c), []).append(j)
    unidades = [(c, [j]) for j, c in enumerate(cols)] + [("GRUPO " + g, js) for g, js in grupos.items()]
    res = []
    for nombre, js in unidades:
        caidas = []
        for r in range(reps):
            p = base_p.copy()
            for s in dias:
                te = np.flatnonzero(ses == s); Xp = X[te].copy(); perm = rng.permutation(len(te)); Xp[:, js] = Xp[perm][:, js]
                p[te] = modelos[s].predict_proba(Xp)[:, 1] if clase else modelos[s].predict(Xp)
            caidas.append(a0 - L.auc(obj, p[ev]))
        res.append((nombre, float(np.mean(caidas)), float(np.std(caidas))))
    r = pd.DataFrame(res, columns=["rasgo", "caida_auc", "sd"]).sort_values("caida_auc", ascending=False)
    return a0, r


salida = {}
# ---- direccionales (pre-registradas): V01 todos y V08 nucleo, barrera +-8/600
y = df["y8"].to_numpy(); valido = y != 0; objetivo_auc = (y > 0).astype(int)
for nombre, cols in (("V01_xgb_todos_8", todos), ("V08_xgb_nucleo_8", L.NUCLEO)):
    a0, r = importancia(cols, (y > 0).astype(int), valido, L.xgb_clf, True, etiqueta=nombre)
    print(r[r["rasgo"].str.startswith("GRUPO")].round(4).to_string(index=False)); print(r[~r["rasgo"].str.startswith("GRUPO")].head(10).round(4).to_string(index=False))
    salida[nombre] = r.round(5).to_dict("records"); print("%.0f s" % (time.time() - t0))

# ---- no direccional: V09 (regresion de log R300); la caida se mide en el AUC de "grande" (sobre la mediana de explorar, fija en puntos)
R = df["R300"].to_numpy(dtype="float64"); valido = ~np.isnan(R); med = float(np.nanmedian(R)); objetivo_auc = (R > med).astype(int)
a0, r = importancia(todos, np.log(np.clip(np.nan_to_num(R, nan=1.0), B.TICK, None)), valido, L.xgb_reg, False, etiqueta="V09_xgb_rango")
print(r[r["rasgo"].str.startswith("GRUPO")].round(4).to_string(index=False)); print(r[~r["rasgo"].str.startswith("GRUPO")].head(15).round(4).to_string(index=False))
salida["V09_xgb_rango"] = r.round(5).to_dict("records")

# ---- descriptivo: cada rasgo SOLO contra la barrera +-8/600 (no hay ajuste: es fuera de muestra por construccion)
y = df["y8"].to_numpy(); ok = y != 0; filas = []
for c in todos:
    x = df[c].to_numpy(dtype="float64"); m = ok & ~np.isnan(x); a = L.auc((y[m] > 0).astype(int), x[m])
    ad = np.array([L.auc((y[m & (ses == s)] > 0).astype(int), x[m & (ses == s)]) for s in dias])
    filas.append((c, a, float(np.nanmean(ad)), int(np.nansum(ad > 0.5)), float((np.nanmean(ad) - 0.5) / (np.nanstd(ad, ddof=1) / np.sqrt(np.sum(~np.isnan(ad)))))))
u = pd.DataFrame(filas, columns=["rasgo", "auc", "auc_dia_media", "dias_sobre_0.5", "t_dias"]); u["lejos"] = (u["auc_dia_media"] - 0.5).abs()
u = u.sort_values("lejos", ascending=False)
print("\nRASGO SOLO contra +-8/600 (AUC > 0,5 = valores altos van con 'toca arriba primero'; < 0,5 = al reves). Los 20 mas lejos de 0,5:")
print(u.head(20).drop(columns="lejos").round(3).to_string(index=False))
print("de %d rasgos: |t| >= 2: %d (por azar se esperan ~%.0f) | |t| >= 3: %d | AUC conjunto maximo %.3f, minimo %.3f" % (len(u), int((u["t_dias"].abs() >= 2).sum()), 0.07 * len(u), int((u["t_dias"].abs() >= 3).sum()), u["auc"].max(), u["auc"].min()))
salida["rasgo_solo"] = u.drop(columns="lejos").round(4).to_dict("records")
json.dump(salida, open(os.path.join(L.CACHE, "importancia_explorar.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("total %.0f s" % (time.time() - t0))
