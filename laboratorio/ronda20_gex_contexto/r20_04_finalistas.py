# -*- coding: utf-8 -*-
"""RONDA 20, paso 4: G3 del PREREGISTRO. Las finalistas de la ronda 19 (vuelta al valor) segun el regimen de gamma en el minuto
de la entrada (cadena publicada antes de ese minuto; dato <= 60 min). Acierto = resultado_pts > 0; neto con costo 0,50 =
neto_c050 (los dos de la R19, sin tocar). Hipotesis: gana con POS y pierde con NEG. Placebo: el regimen de otra rueda del mismo
periodo en el mismo minuto. Dejar un periodo afuera para los umbrales continuos.
Uso: python -I r20_04_finalistas.py"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import pandas as pd

import r20_comun as C

RNG = np.random.default_rng(271120262)
EDAD_MAX = 3600.0
PER = ["diseno", "confirma", "guardada"]
tab = pd.read_parquet(os.path.join(C.SCR, "gex_minuto.parquet"))
tab["gex_estr"] = tab["gex_neto"] - tab["gex_0dte"]
tab["valido"] = tab["hay_cadena"] & (tab["edad_s"] <= EDAD_MAX)
op = pd.read_csv(os.path.join(C.LAB, "ronda19_confluencias", "operaciones_finalistas.csv"))
op["ts_min"] = pd.to_datetime(op["t_utc"], utc=True).dt.floor("min")
tab["ts_min"] = pd.to_datetime(tab["ts_utc"], utc=True)
cols = ["ts_min", "dia", "minuto", "valido", "gex_neto", "gex_estr", "gex_norm", "dist_flip_X", "edad_s", "regimen",
        "dist_muro_call_X", "dist_muro_put_X", "cobertura_es_por_X"]
op = op.merge(tab[cols].rename(columns={"dia": "dia_tab", "minuto": "minuto_tab"}), on="ts_min", how="left")
op["neg_total"] = np.where(op.valido == True, (op.gex_neto < 0).astype(float), np.nan)
op["neg_estr"] = np.where(op.valido == True, (op.gex_estr < 0).astype(float), np.nan)
op["gana"] = (op.resultado_pts > 0).astype(float)
op.to_csv(os.path.join(C.AQUI, "r20_04_finalistas_con_gex.csv"), index=False)
print("operaciones %d | con regimen valido %d | sin cadena o dato viejo %d" % (len(op), op.neg_total.notna().sum(), op.neg_total.isna().sum()))
print(op.groupby(["muestra"]).apply(lambda g: "%d ops, con regimen %d" % (len(g), g.neg_total.notna().sum())).to_string())


def linea(g, col):
    g = g[g[col].notna()]
    s = ""
    for v, nom in ((0.0, "POS"), (1.0, "NEG")):
        h = g[g[col] == v]
        if len(h) == 0:
            s += " | %s n 0" % nom; continue
        pr = h.groupby("dia")["neto_c050"].sum()
        s += " | %s n %3d (%2d r) acierto %4.1f %% neto/op %+.2f (t rueda %+.2f)" % (
            nom, len(h), h.dia.nunique(), 100 * h.gana.mean(), h.neto_c050.mean(), C.t_por_rueda(pr.values))
    if len(g) > 5:
        b, t = C.t_cluster(g["neto_c050"], 1 - g[col], g["dia"])
        bh, th = C.t_cluster(g["gana"], 1 - g[col], g["dia"])
        s += " | dif POS-NEG neto %+.2f (t agr %+.2f), acierto %+.1f pp (t agr %+.2f)" % (b, t, 100 * bh, th)
    return s


def placebo(g, col, n=2000):
    reg = tab.pivot(index="dia", columns="minuto", values="gex_neto" if col == "neg_total" else "gex_estr")
    val = tab.pivot(index="dia", columns="minuto", values="valido").fillna(False).astype(bool)
    reg = (reg < 0).astype(float).where(val)
    g = g[g[col].notna()]
    b_obs, _ = C.t_cluster(g["neto_c050"], 1 - g[col], g["dia"])
    cand = {p: [d for d in sorted(tab[tab.muestra == p].dia.unique()) if reg.loc[d].notna().sum() > 100] for p in PER}
    cnt = tot = 0
    for _ in range(n):
        mapa = {}
        for p in PER:
            for d in g[g.muestra == p].dia.unique():
                if cand[p]:
                    mapa[d] = cand[p][RNG.integers(len(cand[p]))]
        x = np.array([reg.at[mapa[d], m] if d in mapa else np.nan for d, m in zip(g.dia, g.minuto_tab)], dtype=float)
        mm = np.isfinite(x)
        if mm.sum() < 10 or x[mm].std() == 0:
            continue
        b, _ = C.t_cluster(g["neto_c050"].values[mm], 1 - x[mm], g["dia"].values[mm])
        if np.isfinite(b):
            tot += 1; cnt += b >= b_obs
    return b_obs, (cnt + 1) / (tot + 1), tot


for col, nom in (("neg_total", "reg_total (primaria)"), ("neg_estr", "reg_estructural (sin 0DTE)")):
    print("\n" + "=" * 120)
    print("G3 %s: finalistas de la R19 por regimen en la entrada (costo 0,50). Se espera POS mejor que NEG." % nom)
    print("=" * 120)
    for fin in sorted(op.finalista.unique()) + ["JUNTAS"]:
        g0 = op if fin == "JUNTAS" else op[op.finalista == fin]
        print("\n %s" % fin)
        for per in PER + ["todo"]:
            g = g0 if per == "todo" else g0[g0.muestra == per]
            print("   %-9s%s" % (per, linea(g, col)))
    ob, p, nt = placebo(op, col)
    print("\n JUNTAS placebo (regimen de otra rueda del mismo periodo, %d sorteos): dif POS-NEG neto obs %+.3f, p una cola %.3f" % (nt, ob, p))

print("\n" + "=" * 120)
print("Dejar un periodo afuera: (a) filtro fijo 'solo POS' (no se entrena); (b) umbral de gex_norm y de dist_flip_X elegido en dos")
print("periodos (maximo neto/op conservando >= 30 % de las operaciones, operar si la variable >= umbral) y aplicado al tercero.")
print("=" * 120)
V = op[op.neg_total.notna()].copy()
for fuera in PER:
    te = V[V.muestra == fuera]; tr = V[V.muestra != fuera]
    if len(te) == 0:
        print("  afuera %-9s sin dato" % fuera); continue
    base = "  afuera %-9s todas: n %d acierto %.1f %% neto/op %+.2f" % (fuera, len(te), 100 * te.gana.mean(), te.neto_c050.mean())
    sp = te[te.neg_total == 0]
    base += " | solo POS: n %d (%.1f/rueda) acierto %.1f %% neto/op %+.2f" % (len(sp), len(sp) / max(1, te.dia.nunique()),
                                                                          100 * sp.gana.mean() if len(sp) else np.nan,
                                                                          sp.neto_c050.mean() if len(sp) else np.nan)
    print(base)
    for var in ("gex_norm", "dist_flip_X"):
        trv = tr[tr[var].notna()]; tev = te[te[var].notna()]
        mejor = None
        for th in np.quantile(trv[var], np.linspace(0.0, 0.7, 36)):
            k = trv[trv[var] >= th]
            if len(k) < 0.3 * len(trv):
                continue
            if mejor is None or k.neto_c050.mean() > mejor[1]:
                mejor = (th, k.neto_c050.mean(), len(k))
        th = mejor[0]
        k = tev[tev[var] >= th]
        print("      %-11s umbral >= %+.4f (entrenado: neto/op %+.2f, n %d) -> prueba: n %d acierto %.1f %% neto/op %+.2f" % (
            var, th, mejor[1], mejor[2], len(k), 100 * k.gana.mean() if len(k) else np.nan, k.neto_c050.mean() if len(k) else np.nan))
