# -*- coding: utf-8 -*-
"""techo_ml_05_confirmar.py — LA pasada unica por confirmar (8 sesiones, 08-09 al 17-09) con todo CONGELADO en explorar:
  finalista direccional V01 (xgb, todos los rasgos, +-8/600), umbral U del percentil 90 fuera de muestra de explorar;
  no direccional V09 (xgb sobre log R300) junto con su control V10 (persistencia + reloj), 'grande' = sobre la mediana de explorar en puntos.
No hay segundo finalista direccional: ninguna variante cumplio la regla del pre-registro (V08 quedo en 55,9 % contra 56,0 % de empate).
Se corre UNA vez. Deja todo en techo_ml_cache/confirmar_resumen.json."""
import os, sys, time, json
import numpy as np, pandas as pd
from scipy.stats import spearmanr
import techo_ml_lib as L, base as B

t0 = time.time(); marca = os.path.join(L.CACHE, "confirmar_resumen.json")
if os.path.exists(marca): print("YA SE CORRIO la confirmacion (una sola pasada). Resultado guardado en", marca); sys.exit(0)
T = B.todas_las_sesiones(); N = B.niveles_m1(); E = B.explorar(T); C = B.confirmar(T)
dfe = L.tablero(E, N, "explorar"); todos, _ = L.columnas(dfe); dfc = L.tablero(C, N, "confirmar"); objs = {s: L.objetivos(s, d) for s, d in C.items()}
res_e = json.load(open(os.path.join(L.CACHE, "explorar_resumen.json"), encoding="utf-8")); U = res_e["umbrales"]["V01_xgb_todos_8"]
print("explorar %d filas | confirmar %d filas, %d sesiones | U congelado %.4f | %.0f s" % (len(dfe), len(dfc), dfc["sesion"].nunique(), U, time.time() - t0))


def auc_bootstrap_dias(df, y01, p, valido, B_=500, semilla=5):
    """Error del AUC conjunto remuestreando DIAS enteros (los segundos de un mismo dia no son independientes)."""
    rng = np.random.default_rng(semilla); ses = df["sesion"].to_numpy(); dias = np.unique(ses); idx = {s: np.flatnonzero((ses == s) & valido) for s in dias}; out = []
    for _ in range(B_):
        k = np.concatenate([idx[s] for s in rng.choice(dias, size=len(dias))]); out.append(L.auc(y01[k], p[k]))
    return float(np.nanstd(out, ddof=1))


salida = {}
# ---------------- contexto: error del AUC fuera de muestra de explorar (dias remuestreados)
oof = pd.read_parquet(os.path.join(L.CACHE, "oof_explorar.parquet")); ye = dfe["y8"].to_numpy(); ve = ye != 0
for k in ("V01_xgb_todos_8", "V02_logit_todos_8", "V08_xgb_nucleo_8"):
    print("explorar %s: AUC %.4f +- %.4f (dias remuestreados)" % (k, L.auc((ye[ve] > 0).astype(int), oof[k].to_numpy()[ve]), auc_bootstrap_dias(dfe, (ye > 0).astype(int), oof[k].to_numpy(), ve)))

# ---------------- V01 congelado
y = dfe["y8"].to_numpy(); tr = y != 0; m = L.xgb_clf(); m.fit(dfe.loc[tr, todos].to_numpy(dtype="float32"), (y[tr] > 0).astype(int))
p = m.predict_proba(dfc[todos].to_numpy(dtype="float32"))[:, 1]; yc = dfc["y8"].to_numpy(); vc = yc != 0
a = L.auc((yc[vc] > 0).astype(int), p[vc]); ee = auc_bootstrap_dias(dfc, (yc > 0).astype(int), p, vc); ad = L.auc_por_dia(dfc, "y8", p)
disp = L.disparos_de(dfc, p, U); j = L.juicio(disp, objs, 8, "V01_xgb_todos_8 CONFIRMAR"); pm, ps = L.placebo(disp, objs, 8)
j.update(auc=round(a, 4), auc_ee_dias=round(ee, 4), auc_por_dia={k: round(v, 3) for k, v in ad.items()}, placebo=round(pm, 1), placebo_sd=round(ps, 2), z_placebo=round((j["acierto"] - pm) / ps, 2),
         filas_sobre_U_pct=round(100 * float((np.abs(p - 0.5) >= U).mean()), 1), base_arriba_pct=round(100 * float((yc[vc] > 0).mean()), 1))
por_dia = {}
for s, (_, pos, lado) in disp.items():
    yy = objs[s]["y8"][pos].astype(int); r = yy != 0; por_dia[s] = "%d/%d" % (int(((yy[r] * lado[r]) > 0).sum()), int(r.sum()))
j["aciertos_por_dia"] = por_dia; salida["V01"] = j; print(json.dumps(j, ensure_ascii=False))
conf = np.abs(p - 0.5); q = pd.qcut(conf[vc], 5, labels=False, duplicates="drop"); ok = ((p[vc] > 0.5) == (yc[vc] > 0))
print("acierto de seguir al modelo por quintil de confianza (sin desagrupar):", [round(100 * float(ok[q == k].mean()), 1) for k in range(5)])

# ---------------- V09 y V10 congelados (no direccional)
Re = dfe["R300"].to_numpy(dtype="float64"); oke = ~np.isnan(Re); med = float(np.median(Re[oke])); lre = np.log(np.clip(Re, B.TICK, None))
Rc = dfc["R300"].to_numpy(dtype="float64"); okc = ~np.isnan(Rc); grande = (Rc > med).astype(int); ses = dfc["sesion"].to_numpy()
m9 = L.xgb_reg(); m9.fit(dfe.loc[oke, todos].to_numpy(dtype="float32"), lre[oke]); p9 = m9.predict(dfc[todos].to_numpy(dtype="float32"))
m10 = L.Lineal(False, C=1.0); m10.fit(dfe.loc[oke, L.CONTROL_RANGO].to_numpy(), lre[oke]); p10 = m10.predict(dfc[L.CONTROL_RANGO].to_numpy())
salida["rango"] = {"mediana_explorar_pts": med, "grande_en_confirmar_pct": round(100 * float(grande[okc].mean()), 1)}
for k, pr in (("V09_xgb_rango", p9), ("V10_lineal_rango", p10), ("persistencia_rng_300", dfc["rng_300"].to_numpy(dtype="float64"))):
    a = L.auc(grande[okc], pr[okc]); rho = float(spearmanr(pr[okc], Rc[okc])[0]); ad = {s: L.auc(grande[okc & (ses == s)], pr[okc & (ses == s)]) for s in np.unique(ses)}
    salida["rango"][k] = dict(auc=round(a, 4), spearman=round(rho, 3), auc_por_dia={s: round(v, 3) for s, v in ad.items()}, dias_auc_065=int(sum(v >= 0.65 for v in ad.values())))
    print("%-22s AUC %.3f | Spearman %.3f | por dia: %s" % (k, a, rho, {s[5:]: round(v, 2) for s, v in ad.items()}))
cortes = np.nanpercentile(oof["V09_xgb_rango"].to_numpy(), [20, 40, 60, 80]); q = np.digitize(p9, cortes); t8 = dfc["t8"].to_numpy(dtype="float64"); tabla = []
print("quintil de rango PREVISTO por V09 (cortes congelados de explorar: %s pts) -> filas | R300 mediano | %% R300 >= 16 | mediana s hasta resolver +-8 | %% <= 60 s | %% <= 120 s" % np.round(np.exp(cortes), 1))
for k in range(5):
    mk = q == k; f = dict(q=k + 1, filas_pct=round(100 * float(mk.mean()), 1), R300_med=round(float(np.nanmedian(Rc[mk])), 1), R300_16_pct=round(100 * float(np.nanmean(Rc[mk] >= 16)), 0),
                          t8_med=round(float(np.nanmedian(t8[mk])), 0), t8_60_pct=round(100 * float(np.mean(t8[mk] <= 60)), 0), t8_120_pct=round(100 * float(np.mean(t8[mk] <= 120)), 0)); tabla.append(f); print("  ", f)
salida["rango"]["quintiles"] = tabla
json.dump(salida, open(marca, "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=str); print("total %.0f s" % (time.time() - t0))
