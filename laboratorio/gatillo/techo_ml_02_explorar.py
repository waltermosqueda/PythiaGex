# -*- coding: utf-8 -*-
"""techo_ml_02_explorar.py — variantes V01-V10 del pre-registro, TODO fuera de muestra (una sesion entera afuera por vuelta), solo explorar.
Deja las predicciones fuera de muestra en techo_ml_cache/oof_explorar.parquet para la importancia y la destilacion."""
import os, sys, time, json
import numpy as np, pandas as pd
import techo_ml_lib as L, base as B

t0 = time.time()
T = B.todas_las_sesiones(); N = B.niveles_m1(); E = B.explorar(T)
df = L.tablero(E, N, "explorar").reset_index(names="t"); todos, sin_gamma = L.columnas(df.drop(columns=["t"]))
objs = {s: L.objetivos(s, d) for s, d in E.items()}
print("tablero %d filas | %d rasgos | %.0f s" % (len(df), len(todos), time.time() - t0))

VAR = [("V01_xgb_todos_8", todos, 8, L.xgb_clf), ("V02_logit_todos_8", todos, 8, lambda: L.Lineal(True)),
       ("V03_xgb_todos_5", todos, 5, L.xgb_clf), ("V04_logit_todos_5", todos, 5, lambda: L.Lineal(True)),
       ("V05_xgb_todos_12", todos, 12, L.xgb_clf), ("V06_logit_todos_12", todos, 12, lambda: L.Lineal(True)),
       ("V07_xgb_singamma_8", sin_gamma, 8, L.xgb_clf), ("V08_xgb_nucleo_8", L.NUCLEO, 8, L.xgb_clf)]
oof = df[["t", "sesion", "pos"]].copy(); filas = []; umbrales = {}
for nombre, cols, x, fab in VAR:
    t1 = time.time(); ycol = "y%d" % x; p = L.fuera_de_muestra(df, cols, ycol, fab); oof[nombre] = p
    y = df[ycol].to_numpy(); r = y != 0; a = L.auc((y[r] > 0).astype(int), p[r]); ad = L.auc_por_dia(df, ycol, p); v = np.array(list(ad.values()))
    tt = (np.nanmean(v) - 0.5) / (np.nanstd(v, ddof=1) / np.sqrt(len(v)))
    U = float(np.nanpercentile(np.abs(p - 0.5), 90)); umbrales[nombre] = U
    disp = L.disparos_de(df, p, U); j = L.juicio(disp, objs, x, nombre); pm, ps = L.placebo(disp, objs, x)
    j.update(auc=round(a, 4), auc_dia_media=round(float(np.nanmean(v)), 4), auc_dia_sd=round(float(np.nanstd(v, ddof=1)), 4), auc_dias_arriba="%d de %d" % (int((v > 0.5).sum()), len(v)),
             t_dias=round(float(tt), 2), U=round(U, 4), placebo=round(pm, 1), placebo_sd=round(ps, 2), z_placebo=round((j["acierto"] - pm) / ps, 2) if j.get("n") else np.nan)
    # quintiles de p: acierto de "seguir al modelo" por nivel de confianza, sin desagrupar (solo para ver la forma)
    conf = np.abs(p - 0.5); q = pd.qcut(conf[r], 5, labels=False, duplicates="drop"); ok = ((p[r] > 0.5) == (y[r] > 0))
    j["acierto_por_quintil_conf"] = [round(100 * float(ok[q == k].mean()), 1) for k in range(5)]
    j["auc_por_dia"] = {k: round(float(vv), 3) for k, vv in ad.items()}
    filas.append(j); print("%s  %.0f s" % (nombre, time.time() - t1)); print(json.dumps(j, ensure_ascii=False))

# ---------------- no direccional: V09 (xgb sobre log R300) y V10 (control lineal: persistencia + reloj)
ses = df["sesion"].to_numpy(); R = df["R300"].to_numpy(dtype="float64"); lr = np.log(np.clip(R, B.TICK, None)); okR = ~np.isnan(R)
pred = {"V09_xgb_rango": np.full(len(df), np.nan), "V10_lineal_rango": np.full(len(df), np.nan)}; grande = np.full(len(df), np.nan); med_tr = {}
for s in np.unique(ses):
    tr = (ses != s) & okR; te = ses == s; med = float(np.median(R[tr])); med_tr[s] = med; grande[te] = (R[te] > med).astype(float)
    m = L.xgb_reg(); m.fit(df.loc[tr, todos].to_numpy(dtype="float32"), lr[tr]); pred["V09_xgb_rango"][te] = m.predict(df.loc[te, todos].to_numpy(dtype="float32"))
    m = L.Lineal(False, C=1.0); m.fit(df.loc[tr, L.CONTROL_RANGO].to_numpy(), lr[tr]); pred["V10_lineal_rango"][te] = m.predict(df.loc[te, L.CONTROL_RANGO].to_numpy())
from scipy.stats import spearmanr
print("\n--- NO DIRECCIONAL (R300 = rango de los proximos 300 s; 'grande' = sobre la mediana del entrenamiento: %.1f a %.1f pts)" % (min(med_tr.values()), max(med_tr.values())))
balde = df["balde"].to_numpy()
for k, pr in list(pred.items()) + [("persistencia_rng_300", df["rng_300"].to_numpy(dtype="float64")), ("reloj_solo", None)]:
    if pr is None:   # reloj solo: media de log R300 por media hora en las sesiones de entrenamiento
        pr = np.full(len(df), np.nan)
        for s in np.unique(ses):
            tr = (ses != s) & okR; mm = pd.Series(lr[tr]).groupby(balde[tr] % 48).mean(); pr[ses == s] = pd.Series(balde[ses == s] % 48).map(mm).to_numpy()
    oof[k] = pr; m = okR & ~np.isnan(pr)
    a = L.auc(grande[m].astype(int), pr[m]); rho = spearmanr(pr[m], R[m])[0]
    ad = [L.auc(grande[m & (ses == s)].astype(int), pr[m & (ses == s)]) for s in np.unique(ses)]
    # AUC dentro de cada media hora (saca el efecto del reloj): promedio ponderado
    ab = []
    for s in np.unique(ses):
        for b in np.unique(balde[ses == s]):
            mk = m & (ses == s) & (balde == b)
            if mk.sum() >= 60 and len(np.unique(grande[mk])) == 2: ab.append(L.auc(grande[mk].astype(int), pr[mk]))
    print("%-22s AUC %.3f | Spearman %.3f | AUC por dia: media %.3f min %.3f | AUC dentro de la media hora %.3f" % (k, a, rho, np.nanmean(ad), np.nanmin(ad), np.nanmean(ab)))
# traduccion practica de V09: por quintil de rango previsto, que le pasa a la apuesta +-8
pr = pred["V09_xgb_rango"]; q = pd.qcut(pr, 5, labels=False); t8 = df["t8"].to_numpy(dtype="float64")
print("quintil de rango PREVISTO (V09) -> R300 mediano | %% con R300 >= 16 | mediana de s hasta resolver +-8 | %% resuelto en <= 60 s | %% en <= 120 s")
for k in range(5):
    m = q == k; print("  q%d: %.1f pts | %.0f %% | %.0f s | %.0f %% | %.0f %%" % (k + 1, np.nanmedian(R[m]), 100 * np.nanmean(R[m] >= 16), np.nanmedian(t8[m]), 100 * np.mean(t8[m] <= 60), 100 * np.mean(t8[m] <= 120)))
oof["grande"] = grande
oof.to_parquet(os.path.join(L.CACHE, "oof_explorar.parquet"))
json.dump(dict(umbrales=umbrales, filas=filas), open(os.path.join(L.CACHE, "explorar_resumen.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=str)
print("\nRESUMEN"); cols = ["nombre", "auc", "auc_dia_media", "auc_dias_arriba", "t_dias", "n", "acierto", "empate", "placebo", "z_placebo", "dias_arriba", "peor_dia", "netos", "largos_%"]
print(pd.DataFrame(filas)[cols].to_string(index=False)); print("total %.0f s" % (time.time() - t0))
