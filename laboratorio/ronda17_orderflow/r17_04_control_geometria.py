# -*- coding: utf-8 -*-
"""RONDA 17, paso 4 (post-hoc, control del escéptico): ¿la vuelta del delta / del OFI en la reacción es solo geometría?
Agrega el retroceso ACTUAL en tau (no solo el maximo) y estratifica por los dos; regresion logistica con efectos de rueda y
intervalo por ruedas. Uso: python -I r17_04_control_geometria.py tanda0 tanda1"""
import os, sys, warnings
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r17_01_anatomia as A1
from sklearn.linear_model import LogisticRegression
warnings.filterwarnings("ignore")


def con_actual(D):
    out = []
    for (inst, dia), g in D.groupby(["inst", "dia"]):
        A = A1.arreglos(inst, dia)
        p = np.where(g["tipo"] > 0, A["alto"][g["i"]], A["bajo"][g["i"]])
        u = A["ult"][(g["i"] + g["tau"]).to_numpy()]
        g = g.copy(); g["actual_tau"] = g["tipo"] * (p - u) / g["X"]
        out.append(g)
    return pd.concat(out, ignore_index=True)


def ajuste(T, cols):
    Xd = pd.get_dummies(T["rueda"], drop_first=True).astype(float)
    M = pd.concat([T[["hecho_tau", "actual_tau"]], (T["hecho_tau"] ** 2).rename("h2"), (T["actual_tau"] ** 2).rename("a2"),
                   (T["hecho_tau"] * T["actual_tau"]).rename("ha"), T[cols].astype(float), Xd], axis=1)
    lr = LogisticRegression(C=1e6, max_iter=8000).fit(M, T["GIRO"].astype(int))
    return dict(zip(M.columns, lr.coef_[0]))


def main():
    for carpeta in sys.argv[1:]:
        D = con_actual(pd.read_parquet(os.path.join(carpeta, "visible_tau.parquet")))
        print("\n######## %s (%s)" % (carpeta, ", ".join(sorted(D["rueda"].unique()))))
        for tau in (15, 30, 60):
            T = D[D["tau"] == tau].copy()
            T["ba"] = np.clip((T["actual_tau"] * 10).round(), -2, 10); T["bh"] = np.minimum(9, (T["hecho_tau"] * 10).astype(int))
            res = []
            for f in ("REAC_CONTRA", "REAC_OFI", "REAC_OFI_PAS"):
                num = den = 0.0
                for _, g in T.groupby(["rueda", "ba", "bh"]):
                    m = g[f]; a = g.loc[m, "GIRO"].sum(); n1 = m.sum(); c = g.loc[~m, "GIRO"].sum(); n0 = (~m).sum(); N = len(g)
                    if n1 and n0: num += a * n0 / N; den += c * n1 / N
                res.append("%s RR_MH(rueda x actual x maximo) %.2f" % (f, num / den if den else np.nan))
            co = ajuste(T, ["REAC_CONTRA", "REAC_OFI"])
            rng = np.random.default_rng(3); ruedas = T["rueda"].unique(); G = {r: T[T["rueda"] == r] for r in ruedas}; bs = []
            for _ in range(150):
                S = pd.concat([G[r] for r in rng.choice(ruedas, len(ruedas))], ignore_index=True)
                try: bs.append(ajuste(S, ["REAC_CONTRA", "REAC_OFI"]))
                except Exception: pass
            ic = {k: (np.percentile([b[k] for b in bs], 5), np.percentile([b[k] for b in bs], 95)) for k in ("REAC_CONTRA", "REAC_OFI")}
            print("tau %d (n %d): %s" % (tau, len(T), " | ".join(res)))
            print("   logistica con retroceso maximo y ACTUAL: REAC_CONTRA %+.2f (IC90 %+.2f a %+.2f), REAC_OFI %+.2f (IC90 %+.2f a %+.2f)"
                  % (co["REAC_CONTRA"], *ic["REAC_CONTRA"], co["REAC_OFI"], *ic["REAC_OFI"]))
            # cuanto del OFI en contra es simplemente 'el precio sigue lejos del extremo'
            print("   correlacion REAC_OFI con retroceso actual: %.2f | P(GIRO) con retroceso actual >= 50 %% de X: con OFI en contra %.1f %% (n %d), sin %.1f %% (n %d)"
                  % (np.corrcoef(T["REAC_OFI"].astype(float), T["actual_tau"])[0, 1],
                     100 * T.loc[(T["actual_tau"] >= .5) & T["REAC_OFI"], "GIRO"].mean(), ((T["actual_tau"] >= .5) & T["REAC_OFI"]).sum(),
                     100 * T.loc[(T["actual_tau"] >= .5) & ~T["REAC_OFI"], "GIRO"].mean(), ((T["actual_tau"] >= .5) & ~T["REAC_OFI"]).sum()))


if __name__ == "__main__":
    main()
