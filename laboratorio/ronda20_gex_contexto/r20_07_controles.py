# -*- coding: utf-8 -*-
"""RONDA 20, paso 7: controles POST HOC (Enmienda 3 del PREREGISTRO, declarados despues de ver G1-G3; exploracion, no pueden pasar).
C1: rango_rel ~ negativo a las 9:59 + rango_rel de ayer (agrupamiento de la volatilidad), ruedas como unidad.
C2: 'gamma muy negativa' (gex_norm < -0,41) en las rupturas PARA ABAJO de los chequeos de G2, por periodo y rueda por rueda.
C3: finalistas de la R19 por tramos de gex_norm.
Uso: python -I r20_07_controles.py (despues de r20_03 y r20_04)"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import pandas as pd

import r20_comun as C

PER = ["diseno", "confirma", "guardada"]
D = pd.read_csv(os.path.join(C.AQUI, "r20_03_ruedas.csv")).sort_values("dia").reset_index(drop=True)
Q = pd.read_csv(os.path.join(C.AQUI, "r20_03_chequeos.csv"))
OP = pd.read_csv(os.path.join(C.AQUI, "r20_04_finalistas_con_gex.csv"))

print("=" * 100)
print("C1: rango relativo del dia ~ gamma negativa a las 9:59 + rango relativo de AYER (OLS, ruedas como unidad, t de White)")
print("=" * 100)
D["rango_rel_ayer"] = D["rango_rel"].shift(1)
D["neg"] = np.where(D.gex_neto_0959.notna(), (D.gex_neto_0959 < 0).astype(float), np.nan)


def ols(y, Xs):
    m = np.isfinite(y)
    for x in Xs:
        m &= np.isfinite(x)
    yv = y[m]; Xm = np.column_stack([np.ones(m.sum())] + [x[m] for x in Xs])
    b = np.linalg.lstsq(Xm, yv, rcond=None)[0]
    e = yv - Xm @ b
    XtX = np.linalg.inv(Xm.T @ Xm)
    V = XtX @ (Xm.T * e * e) @ Xm @ XtX * len(yv) / (len(yv) - Xm.shape[1])
    return b, b / np.sqrt(np.diag(V)), int(m.sum())


for per in PER + ["juntas"]:
    g = D if per == "juntas" else D[D.muestra == per]
    y = g.rango_rel.to_numpy(float)
    if g.neg.dropna().nunique() < 2:
        print("  %-9s sin contraste (todas las ruedas con dato tienen el mismo signo a las 9:59: %s; rango_rel medio %.2f, n %d)" % (
            per, "negativo" if g.neg.dropna().mean() == 1 else "positivo", g[g.neg.notna()].rango_rel.mean(), g.neg.notna().sum()))
        continue
    b1, t1, n1 = ols(y, [g.neg.to_numpy(float)])
    b2, t2, n2 = ols(y, [g.neg.to_numpy(float), g.rango_rel_ayer.to_numpy(float)])
    b3, t3, n3 = ols(y, [g.rango_rel_ayer.to_numpy(float)])
    print("  %-9s solo gamma: %+.2f (t %+.2f, n %d) | gamma + ayer: gamma %+.2f (t %+.2f), ayer %+.2f (t %+.2f), n %d | solo ayer %+.2f (t %+.2f)" % (
        per, b1[1], t1[1], n1, b2[1], t2[1], b2[2], t2[2], n2, b3[1], t3[1]))
print("  correlacion entre 'gamma negativa a las 9:59' y rango_rel de ayer: %.2f" % D[["neg", "rango_rel_ayer"]].corr().iloc[0, 1])
print("  y con el movimiento de ayer en X (cierre - apertura): %.2f" % np.corrcoef(
    *D[["neg"]].assign(m=D.mov_dia_X.shift(1)).dropna().to_numpy().T)[0, 1])

print("\n" + "=" * 100)
print("C2: rupturas PARA ABAJO (lado -1) de los chequeos de G2: gamma muy negativa (gex_norm < -0,41) contra el resto")
print("=" * 100)
F = Q[(Q.lado == -1) & Q.gex_norm.notna()].copy()
F["muy_neg"] = (F.gex_norm < -0.41).astype(int)
for per in PER + ["juntas"]:
    g = F if per == "juntas" else F[F.muestra == per]
    s = "  %-9s" % per
    for v, nom in ((1, "MUY NEG"), (0, "resto")):
        h = g[g.muy_neg == v]
        if len(h) == 0:
            s += " | %s n 0" % nom; continue
        pr = h.groupby("dia")["fcX"].mean()
        s += " | %s n %3d (%2d r) fc %+.2f X a favor %2.0f %% t rueda %+.2f" % (nom, len(h), h.dia.nunique(), h.fcX.mean(),
                                                                            100 * (h.fc > 0).mean(), C.t_por_rueda(pr.values))
    b, t = C.t_cluster(g.fcX, g.muy_neg, g.dia)
    s += " | dif %+.2f X (t agr %+.2f)" % (b, t)
    print(s)
print("\n  rueda por rueda (solo rupturas para abajo con gamma muy negativa):")
print(F[F.muy_neg == 1].groupby(["muestra", "dia"]).agg(n=("fcX", "size"), fcX=("fcX", "mean"), a_favor=("fc", lambda s: (s > 0).mean()),
                                                         gex_norm=("gex_norm", "median"), dist_flip_X=("dist_flip_X", "median")).round(2).to_string())

print("\n" + "=" * 100)
print("C3: finalistas de la R19 por tramos de gex_norm en la entrada (todas juntas), por periodo")
print("=" * 100)
V = OP[OP.neg_total.notna() & OP.gex_norm.notna()].copy()
V["tramo"] = pd.cut(V.gex_norm, [-1, -0.41, -0.2, 0.0, 0.2, 1], labels=["< -0,41", "-0,41..-0,2", "-0,2..0", "0..0,2", "> 0,2"])
for per in PER + ["todo"]:
    g = V if per == "todo" else V[V.muestra == per]
    t = g.groupby("tramo", observed=False).agg(n=("gana", "size"), ruedas=("dia", "nunique"), acierto=("gana", "mean"),
                                               neto=("neto_c050", "mean"))
    t["acierto"] = (100 * t.acierto).round(1)
    print("  %s\n%s" % (per, t.round(2).to_string()))
