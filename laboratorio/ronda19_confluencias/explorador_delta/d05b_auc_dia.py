# -*- coding: utf-8 -*-
"""AUC DENTRO de cada rueda (promedio ponderado) de las predicciones fuera de pliegue (deja una rueda afuera): saca el sesgo de la
tasa base por rueda que hunde el AUC agrupado. Tambien la logistica de UN rasgo por vez (signo estable y AUC dentro de rueda)."""
import os, sys, warnings
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import d_lib as L
from sklearn.linear_model import LogisticRegression
from sklearn.preprocessing import StandardScaler
from sklearn.pipeline import make_pipeline
from sklearn.ensemble import HistGradientBoostingClassifier
from sklearn.metrics import roc_auc_score
warnings.filterwarnings("ignore")
df = L.cargar()
h = df[(df.seg_rueda >= 1800) & (df.seg_rueda < 21600)].copy()
h["vw_R"] = (h.vwap_rth_dist_pts / h.R).clip(-15, 15)
h["abs_rel_c"] = h.of_abs_lado_pasivo_rel.clip(upper=5)
h["d300_r"] = h.of_delta_300 / h.of_vol_60.clip(lower=1)
h["big_r"] = h.of_netbig60_a_favor / h.of_vol_60.clip(lower=1)
h["seg_ext_log"] = np.log1p(h.seg_desde_ext.clip(lower=0))
h["delpp_c"] = h.del_ret_por_pt.clip(-300, 500)
FLUJO = ["F1", "F2", "F3", "F4", "F6", "veto_caudal", "abs_rel_c", "of_icebergs_60", "ice_neto_60", "d60_ratio", "d300_r", "big_r",
         "imb_neto_3v", "of_apilados_favor_3v", "of_apilados_contra_3v", "delpp_c", "ctx_ret_con_agresores", "seg_ext_log", "giro_delta"]
CTX = ["vw_R", "a_favor_marea", "marea_con_agresores", "extension_fuera_ruido", "n_niveles_en_zona", "ctx_prof_X"]
g = h.dia.values
def auc_dia(y, p):
    num = 0; den = 0
    for d in np.unique(g):
        m = g == d
        if y[m].min() == y[m].max(): continue
        a = roc_auc_score(y[m], p[m]); w = m.sum(); num += a * w; den += w
    return num / den
for objetivo in ("S6", "S1"):
    y = (h[objetivo + "_pts"] > 0).astype(int).values
    for nombre, cols in (("solo contexto", CTX), ("solo flujo", FLUJO), ("flujo + contexto", FLUJO + CTX)):
        X = h[cols].astype(float).values
        for mod_n, mk in (("logistica", lambda: make_pipeline(StandardScaler(), LogisticRegression(C=0.3, max_iter=2000))),
                          ("arboles", lambda: HistGradientBoostingClassifier(max_depth=2, learning_rate=0.05, max_iter=150, min_samples_leaf=40))):
            p = np.zeros(len(y))
            for d in np.unique(g):
                tr = g != d; m = mk(); m.fit(X[tr], y[tr]); p[~tr] = m.predict_proba(X[~tr])[:, 1]
            print("%s %-17s %-9s AUC dentro de rueda (fuera de pliegue) %.3f" % (objetivo, nombre, mod_n, auc_dia(y, p)))
    print("  un rasgo por vez (AUC dentro de rueda del rasgo crudo, sin modelo; >0,5 = mas alto, mas acierto):")
    for c in FLUJO + CTX:
        print("    %-24s %.3f" % (c, auc_dia(y, h[c].astype(float).values + 1e-9 * np.random.default_rng(1).standard_normal(len(h)))))
