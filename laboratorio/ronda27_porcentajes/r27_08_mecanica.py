# -*- coding: utf-8 -*-
"""RONDA 27, paso 8: MECANICA (descriptiva, post hoc; PREREGISTRO seccion 9). Que explica el porcentaje de un minuto y donde.
1) Cuanto pesa el DIA: la fraccion de 'arriba primero' por rueda (su dispersion contra la de una moneda con la misma cantidad de minutos), el acierto
   de un ORACULO que supiera el lado del dia (signo de cierre - apertura) y el de seguir el lado del dia HASTA AHORA (signo de c - apertura).
2) Memoria corta: si el minuto m resolvio arriba, que paso con el m + 15 y el m + 60 (dentro de la misma rueda).
3) Coeficientes de M1 con C fijo (0,01) ajustado por tramo, con IC90 por bootstrap de ruedas: signo y tamano por tramo (estable o no). Se informa
   el cambio de P(arriba) por cada unidad RMS del rasgo alrededor del 50 % (beta / 4).
Uso: python -I r27_08_mecanica.py [--Y X]"""
import argparse, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r27_comun as K
import r27_03_modelos as MD
import numpy as np, pandas as pd

SAL = os.path.join(K.AQUI, "salidas")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--Y", nargs="+", default=["X", "X2"])
    a = ap.parse_args()
    T = MD.preparar(pd.read_parquet(MD.TAB))
    out = ["RONDA 27 - mecanica descriptiva (post hoc) sobre las 55 ruedas, por tramo"]
    for Yk in a.Y:
        y = "y_" + Yk
        out.append("\n=== Y = %s" % Yk)
        # 1) el dia
        for tr in ("diseno", "confirmacion", "final"):
            Tt = T[T.tramo == tr]
            g = Tt.groupby("dia")
            fr = g[y].mean(); n = g[y].count()
            sd_moneda = float(np.sqrt(0.25 / n).mean())
            C = g["c"].last(); O = g["O"].first()
            lado_dia = np.sign(Tt["dia"].map((C - O)).to_numpy())
            yy = Tt[y].to_numpy(); ok = np.isfinite(yy) & (lado_dia != 0)
            orac = np.where(lado_dia[ok] > 0, yy[ok], 1 - yy[ok]).mean()
            la = Tt["lado_ap"].to_numpy(); ok2 = np.isfinite(yy) & (la != 0)
            hasta = np.where(la[ok2] > 0, yy[ok2], 1 - yy[ok2]).mean()
            out.append("  %-12s %2d ruedas | arriba por rueda: media %.3f, desvio %.3f (una moneda con esos minutos: %.3f; los minutos de una rueda van juntos) "
                       "| oraculo del lado del dia %.1f %% | seguir el lado del dia hasta ahora %.1f %%" % (
                           tr, len(fr), fr.mean(), fr.std(), sd_moneda, 100 * orac, 100 * hasta))
        # 2) memoria corta
        for k in (15, 60):
            A = T[["dia", "m", y]].copy(); B = A.copy(); B["m"] = B["m"] - k
            M = A.merge(B, on=["dia", "m"], suffixes=("", "_k")).dropna()
            out.append("  memoria: si el minuto resolvio arriba, el de %d min despues resolvio arriba %.1f %% (abajo -> abajo %.1f %%), n %d" % (
                k, 100 * M[M[y] == 1][y + "_k"].mean(), 100 * (1 - M[M[y] == 0][y + "_k"].mean()), len(M)))
        # 3) coeficientes de M1 por tramo
        from sklearn.linear_model import LogisticRegression
        filas = {}
        for tr in ("diseno", "confirmacion", "final"):
            Tt = T[T.tramo == tr]
            dias = sorted(Tt.dia.unique())
            por = {d: MD.matriz(g.sort_values("m"), "M1") + (g.sort_values("m")[y].to_numpy(),) for d, g in Tt.groupby("dia")}
            nombres = por[dias[0]][1]
            def ajuste(sel):
                X = np.vstack([por[d][0][np.isfinite(por[d][2])] for d in sel]); yy = np.concatenate([por[d][2][np.isfinite(por[d][2])] for d in sel])
                rms = np.sqrt(np.mean(X * X, axis=0)); rms[rms <= 0] = 1
                lr = LogisticRegression(C=0.01, fit_intercept=False, max_iter=500).fit(X / rms, yy.astype(int))
                return lr.coef_[0]
            b0 = ajuste(dias)
            rng = np.random.default_rng(8); bb = np.array([ajuste([dias[i] for i in rng.integers(0, len(dias), len(dias))]) for _ in range(200)])
            filas[tr] = (b0, np.quantile(bb, 0.05, axis=0), np.quantile(bb, 0.95, axis=0))
        out.append("  M1 con C 0,01 por tramo: cambio de P(arriba) por unidad RMS del rasgo (beta/4, en puntos de %%), IC90 por bootstrap de ruedas")
        for j, nom in enumerate(nombres):
            out.append("    %-12s " % nom + " | ".join("%s %+.2f [%+.2f, %+.2f]" % (tr[:4], 25 * filas[tr][0][j], 25 * filas[tr][1][j], 25 * filas[tr][2][j])
                                                       for tr in ("diseno", "confirmacion", "final")))
    open(os.path.join(SAL, "mecanica.txt"), "w", encoding="utf-8").write("\n".join(out) + "\n")
    print("\n".join(out))


if __name__ == "__main__":
    main()
