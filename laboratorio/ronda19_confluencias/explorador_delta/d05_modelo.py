# -*- coding: utf-8 -*-
"""Explorador DELTA: mapa de donde vive la senal con validacion DEJANDO UNA RUEDA AFUERA (26 pliegues), evento por evento
(10:00-15:30, sin la restriccion de una posicion). Logistica L2 estandarizada y arboles poco profundos; objetivo: S6 gana (1R) y
S1 gana (2R). Reporta AUC fuera de pliegue, acierto por quintil de probabilidad fuera de pliegue y la estabilidad de los coeficientes."""
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
h["vw_R"] = (h.vwap_rth_dist_pts / h.R).clip(-15, 15)             # distancia al VWAP en R (negativa = hacia el valor)
h["abs_rel_c"] = h.of_abs_lado_pasivo_rel.clip(upper=5)
h["d300_r"] = h.of_delta_300 / h.of_vol_60.clip(lower=1)
h["big_r"] = h.of_netbig60_a_favor / h.of_vol_60.clip(lower=1)
h["seg_ext_log"] = np.log1p(h.seg_desde_ext.clip(lower=0))
h["delpp_c"] = h.del_ret_por_pt.clip(-300, 500)
FLUJO = ["F1", "F2", "F3", "F4", "F6", "veto_caudal", "abs_rel_c", "of_icebergs_60", "ice_neto_60", "d60_ratio", "d300_r", "big_r",
         "imb_neto_3v", "of_apilados_favor_3v", "of_apilados_contra_3v", "delpp_c", "ctx_ret_con_agresores", "seg_ext_log", "giro_delta"]
CTX = ["vw_R", "a_favor_marea", "marea_con_agresores", "extension_fuera_ruido", "n_niveles_en_zona", "ctx_prof_X"]
grupos = h.dia.values
for objetivo in ("S6", "S1"):
    y = (h[objetivo + "_pts"] > 0).astype(int).values
    for nombre, cols in (("solo contexto", CTX), ("solo flujo", FLUJO), ("flujo + contexto", FLUJO + CTX)):
        X = h[cols].astype(float).values
        for mod_n, mk in (("logistica", lambda: make_pipeline(StandardScaler(), LogisticRegression(C=0.3, max_iter=2000))),
                          ("arboles", lambda: HistGradientBoostingClassifier(max_depth=2, learning_rate=0.05, max_iter=150, min_samples_leaf=40))):
            p = np.zeros(len(y)); coefs = []
            for d in np.unique(grupos):
                tr = grupos != d; te = ~tr
                m = mk(); m.fit(X[tr], y[tr]); p[te] = m.predict_proba(X[te])[:, 1]
                if mod_n == "logistica": coefs.append(m[-1].coef_[0])
            auc = roc_auc_score(y, p)
            q = pd.qcut(p, 5, labels=False, duplicates="drop")
            tasas = [100 * y[q == k].mean() for k in range(int(q.max()) + 1)]
            print("%s %-17s %-9s AUC fuera de pliegue %.3f | acierto por quintil de prob: %s" % (objetivo, nombre, mod_n, auc, " ".join("%.0f" % t for t in tasas)))
            if mod_n == "logistica" and nombre == "flujo + contexto":
                C = np.array(coefs); mu = C.mean(0); sd = C.std(0); signo = (np.sign(C) == np.sign(mu)).mean(0)
                orden = np.argsort(-np.abs(mu))
                for j in orden:
                    print("      %-24s coef %+.3f (sd %.3f, mismo signo en %3.0f %% de los pliegues)" % (cols[j], mu[j], sd[j], 100 * signo[j]))
