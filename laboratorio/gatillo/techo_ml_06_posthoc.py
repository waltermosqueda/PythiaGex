# -*- coding: utf-8 -*-
"""techo_ml_06_posthoc.py — DIAGNOSTICO POST-HOC (no cambia ningun veredicto; nada de aca es un hallazgo validado).
El modelo congelado V01 acerto 37,7 % en confirmar (z -4,3 contra placebo): no es ruido, es una relacion aprendida en explorar que en confirmar
salio AL REVES. Pregunta: que aprendio y que se dio vuelta?
  D1. como son los disparos del modelo en confirmar (que venia haciendo el precio y el flujo cuando compro / cuando vendio);
  D2. AUC CONJUNTO (no por dia: el por dia esta sesgado, ver techo_ml_04) de rasgos clave contra +-8/600, explorar contra confirmar, con error por dias remuestreados;
  D3. acierto de los disparos por lado y por tramo horario."""
import os, sys, json
import numpy as np, pandas as pd
import techo_ml_lib as L, base as B

T = B.todas_las_sesiones(); N = B.niveles_m1(); E = B.explorar(T); C = B.confirmar(T)
dfe = L.tablero(E, N, "explorar"); dfc = L.tablero(C, N, "confirmar"); todos, _ = L.columnas(dfe); objs = {s: L.objetivos(s, d) for s, d in C.items()}
U = json.load(open(os.path.join(L.CACHE, "explorar_resumen.json"), encoding="utf-8"))["umbrales"]["V01_xgb_todos_8"]
y = dfe["y8"].to_numpy(); tr = y != 0; m = L.xgb_clf(); m.fit(dfe.loc[tr, todos].to_numpy(dtype="float32"), (y[tr] > 0).astype(int))
p = m.predict_proba(dfc[todos].to_numpy(dtype="float32"))[:, 1]; disp = L.disparos_de(dfc, p, U)
filas = np.concatenate([v[0] for v in disp.values()]); lado = np.concatenate([v[2] for v in disp.values()]); yc = dfc["y8"].to_numpy()[filas]; ok = yc != 0
print("D1. disparos de V01 en confirmar: %d (largos %d, cortos %d). Media de cada rasgo cuando COMPRO / cuando VENDIO / en todas las filas:" % (len(filas), (lado > 0).sum(), (lado < 0).sum()))
for c in ("ret_60", "ret_300", "ret_900", "ret_3600", "dr_300", "zD_900", "zP_900", "absn_900", "pos_1800", "pos_rueda", "vwap_r_z", "vwap_s_p", "d_apertura", "g_zero", "g_dom0", "g_dom1", "rng_300", "mins"):
    x = dfc[c].to_numpy(dtype="float64"); print("   %-11s %9.3f / %9.3f / %9.3f" % (c, np.nanmean(x[filas][lado > 0]), np.nanmean(x[filas][lado < 0]), np.nanmean(x)))
imp = pd.Series(m.feature_importances_, index=todos).sort_values(ascending=False); print("ganancia interna del modelo final (top 12):", imp.head(12).round(3).to_dict())

print("\nD3. acierto de los disparos por lado: largos %.1f %% (n %d) | cortos %.1f %% (n %d)" % (100 * np.mean(yc[ok & (lado > 0)] > 0), (ok & (lado > 0)).sum(), 100 * np.mean(yc[ok & (lado < 0)] < 0), (ok & (lado < 0)).sum()))
mins = dfc["mins"].to_numpy()[filas]
for a, b in ((0, 90), (90, 270), (270, 400)):
    k = ok & (mins >= a) & (mins < b); print("    minutos %3d-%3d desde 13:30: %.1f %% (n %d)" % (a, b, 100 * np.mean((yc[k] * lado[k]) > 0), k.sum()))


def auc_ee(df, c, B_=300):
    yy = df["y8"].to_numpy(); x = df[c].to_numpy(dtype="float64"); v = (yy != 0) & ~np.isnan(x); ses = df["sesion"].to_numpy(); a = L.auc((yy[v] > 0).astype(int), x[v])
    rng = np.random.default_rng(1); dias = np.unique(ses); idx = {s: np.flatnonzero(v & (ses == s)) for s in dias}; bs = []
    for _ in range(B_):
        k = np.concatenate([idx[s] for s in rng.choice(dias, size=len(dias))]); bs.append(L.auc((yy[k] > 0).astype(int), x[k]))
    return a, float(np.std(bs, ddof=1))


print("\nD2. AUC CONJUNTO de cada rasgo solo contra +-8/600 (0,5 = nada; < 0,5 = lo alto va con 'baja primero'): explorar | confirmar")
for c in ("ret_15", "ret_60", "ret_300", "ret_900", "ret_1800", "dr_60", "dr_300", "dr_900", "zD_300", "zD_900", "zO_300", "zP_300", "zP_900", "absn_900", "div_300", "pos_1800", "pos_3600", "vwap_r_z", "qi_60", "g_cerca", "g_dom0", "g_dom1"):
    ae, se = auc_ee(dfe, c); ac, sc = auc_ee(dfc, c); print("   %-9s %.3f +- %.3f | %.3f +- %.3f   %s" % (c, ae, se, ac, sc, "<-- confirmar a mas de 2 errores de 0,5" if abs(ac - 0.5) > 2 * sc else ""))
