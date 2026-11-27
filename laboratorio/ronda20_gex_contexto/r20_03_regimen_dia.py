# -*- coding: utf-8 -*-
"""RONDA 20, paso 3: G1 (ANTES) y G2 (MIENTRAS) del PREREGISTRO. La vara es la de la ronda 19 (r19_06_contexto_deriva.py),
replicada con sus rasgos: chequeos cada 30 min de 10:00 a 15:30; fuera del area de ruido (|precio - apertura| > sigma del
minuto) el lado es el de la ruptura y fc = (cierre - precio) x lado. Por rueda: eficiencia, rango relativo a la mediana de 10.
El regimen sale de la tabla por minuto (cadena publicada antes de cada minuto; se usa si el dato tiene <= 60 min, salvo la
variante de las 9:30, que es la foto previa a la campana por construccion).
Uso: python -I r20_03_regimen_dia.py"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import pandas as pd
from scipy import stats

import r20_comun as C

RNG = np.random.default_rng(20261127)
EDAD_MAX = 3600.0
PER = ["diseno", "confirma", "guardada"]
tab = pd.read_parquet(os.path.join(C.SCR, "gex_minuto.parquet"))
tab["gex_estr"] = tab["gex_neto"] - tab["gex_0dte"]
tab["valido"] = tab["hay_cadena"] & (tab["edad_s"] <= EDAD_MAX)
tab["neg_total"] = np.where(tab["hay_cadena"], (tab["gex_neto"] < 0).astype(float), np.nan)
tab["neg_estr"] = np.where(tab["hay_cadena"], (tab["gex_estr"] < 0).astype(float), np.nan)
# evolucion: fraccion de minutos negativos desde las 9:30 hasta t (con cadena valida)
tab = tab.sort_values(["dia", "minuto"])
tab["neg_valido"] = tab["neg_total"].where(tab["valido"])
tab["frac_neg"] = tab.groupby("dia")["neg_valido"].transform(lambda s: s.expanding().mean())
T = {(d, m): i for i, (d, m) in enumerate(zip(tab.dia, tab.minuto))}
tabv = tab.set_index(["dia", "minuto"])

# --------------------------------------------------------------- por rueda y por chequeo (vara de la R19)
ruedas, cheq = [], []
for dia in C.dias_es():
    meta = C.meta19(dia)
    R = C.rasgos19(dia, ["c", "ruido_sigma"])
    c, sig = R["c"], R["ruido_sigma"]
    o, h, l, cl, X = meta["rth_o"], meta["rth_h"], meta["rth_l"], meta["rth_c"], meta["X"]
    cierre = c[-1]
    fila = {"dia": dia, "muestra": C.muestra_de(dia), "X": X, "rango": h - l, "rango_rel": (h - l) / meta["mediana_rango_10"],
            "e": abs(cl - o) / (h - l), "tendencia": int(abs(cl - o) / (h - l) >= 0.6), "mov_dia_X": (cl - o) / X}
    for nom, mi, exigir_edad in (("0959", 29, True), ("0930", 0, False)):
        try:
            r = tabv.loc[(dia, mi)]
        except KeyError:
            r = None
        ok = r is not None and bool(r["hay_cadena"]) and (r["edad_s"] <= EDAD_MAX if exigir_edad else bool(r["foto_hoy"]))
        for k in ("gex_neto", "gex_estr", "gex_norm", "dist_flip_X", "edad_s", "cobertura_es_por_X", "vol_call_frac"):
            fila[k + "_" + nom] = float(r[k]) if ok and k in r and pd.notna(r[k]) else np.nan
    fcs = []
    for tau in range(1800, 21601, 1800):
        t = tau - 1
        s = sig[min(389, t // 60)]
        mov = c[t] - o
        lado = int(np.sign(mov)) if abs(mov) > s else 0
        mi = tau // 60
        try:
            r = tabv.loc[(dia, mi)]
        except KeyError:
            r = None
        ok = r is not None and bool(r["valido"])
        q = {"dia": dia, "muestra": C.muestra_de(dia), "minuto": mi, "X": X, "lado": lado, "tam": abs(mov) / s if s > 0 else np.nan,
             "fc": (cierre - c[t]) * (lado if lado else 1), "f30": (c[min(t + 1800, 23399)] - c[t]) * (lado if lado else 1),
             "f60": (c[min(t + 3600, 23399)] - c[t]) * (lado if lado else 1),
             "neg_total": float(r["neg_total"]) if ok else np.nan, "neg_estr": float(r["neg_estr"]) if ok else np.nan,
             "gex_norm": float(r["gex_norm"]) if ok else np.nan, "dist_flip_X": float(r["dist_flip_X"]) if ok else np.nan,
             "frac_neg": float(r["frac_neg"]) if ok else np.nan, "edad_s": float(r["edad_s"]) if r is not None and bool(r["hay_cadena"]) else np.nan}
        q["fcX"] = q["fc"] / X
        cheq.append(q)
        if lado:
            fcs.append(q["fcX"])
    fila["ruptura_cierre_X"] = float(np.mean(fcs)) if fcs else np.nan
    fila["n_rupturas"] = len(fcs)
    ruedas.append(fila)
D = pd.DataFrame(ruedas)
Q = pd.DataFrame(cheq)
D.to_csv(os.path.join(C.AQUI, "r20_03_ruedas.csv"), index=False)
Q.to_csv(os.path.join(C.AQUI, "r20_03_chequeos.csv"), index=False)


def perm_p(y, grupo, estratos, n=5000, cola=+1):
    """p de permutacion (una cola) de la diferencia media(grupo 1) - media(grupo 0), permutando etiquetas dentro de cada estrato."""
    y = np.asarray(y, float); g = np.asarray(grupo, float); s = np.asarray(estratos)
    m = np.isfinite(y) & np.isfinite(g)
    y, g, s = y[m], g[m], s[m]
    if g.sum() < 1 or (1 - g).sum() < 1:
        return np.nan, np.nan
    obs = y[g == 1].mean() - y[g == 0].mean()
    cnt = 0
    for _ in range(n):
        gp = g.copy()
        for e in np.unique(s):
            i = np.where(s == e)[0]
            gp[i] = RNG.permutation(g[i])
        if (gp.sum() == 0) or (gp.sum() == len(gp)):
            continue
        d = y[gp == 1].mean() - y[gp == 0].mean()
        cnt += (d >= obs) if cola > 0 else (d <= obs)
    return obs, (cnt + 1) / (n + 1)


print("=" * 110)
print("G1 (ANTES): regimen a las 9:59 (y 9:30) contra el tipo de rueda. Unidad = rueda. Se espera NEG > POS en las tres columnas.")
print("=" * 110)
for defi in ("gex_neto", "gex_estr"):
    for hora in ("0959", "0930"):
        col = "%s_%s" % (defi, hora)
        print("\n-- regimen por signo de %s a las %s:%s --" % (defi, hora[:2], hora[2:]))
        for per in PER + ["juntas"]:
            g = D if per == "juntas" else D[D.muestra == per]
            g = g[g[col].notna()]
            if len(g) == 0:
                print("  %-9s sin dato" % per); continue
            neg = (g[col] < 0).astype(float)
            lin = "  %-9s ruedas %2d (neg %2d, pos %2d)" % (per, len(g), int(neg.sum()), int((1 - neg).sum()))
            for y in ("rango_rel", "e", "ruptura_cierre_X"):
                yy = g[y]
                a, b = yy[neg == 1].mean(), yy[neg == 0].mean()
                t = stats.ttest_ind(yy[neg == 1].dropna(), yy[neg == 0].dropna(), equal_var=False).statistic \
                    if (neg == 1).sum() >= 2 and (neg == 0).sum() >= 2 else np.nan
                _, p = perm_p(yy, neg, g.muestra, n=3000)
                lin += " | %s neg %.2f pos %.2f dif %+.2f (t %+.2f, p %.3f)" % (y, a, b, a - b, t, p)
            print(lin)
print("\nContinua: rho de Spearman entre gex_norm a las 9:59 y cada resultado (se espera negativa)")
for per in PER + ["juntas"]:
    g = D if per == "juntas" else D[D.muestra == per]
    g = g[g["gex_norm_0959"].notna()]
    out = []
    for y in ("rango_rel", "e", "ruptura_cierre_X"):
        gg = g[[y, "gex_norm_0959"]].dropna()
        rho, p = stats.spearmanr(gg["gex_norm_0959"], gg[y]) if len(gg) > 4 else (np.nan, np.nan)
        out.append("%s rho %+.2f (p %.3f, n %d)" % (y, rho, p, len(gg)))
    print("  %-9s %s" % (per, " | ".join(out)))
print("\nPor rueda (diseno/confirma/guardada): regimen 9:59, gex_norm, dist_flip_X, edad del dato, rango_rel, e, ruptura al cierre")
print(D[["dia", "muestra", "gex_neto_0959", "gex_estr_0959", "gex_norm_0959", "dist_flip_X_0959", "edad_s_0959", "gex_neto_0930",
         "rango_rel", "e", "ruptura_cierre_X", "n_rupturas"]].round(3).to_string())

print("\n" + "=" * 110)
print("G2 (MIENTRAS): chequeos fuera del ruido, fc (cierre - precio) x lado de la ruptura, segun el regimen EN el chequeo.")
print("Se espera fc > 0 con NEG (sigue) y fc < 0 con POS (vuelve). t por rueda = medias por rueda; dif con t agrupada (CR1).")
print("=" * 110)
F = Q[(Q.lado != 0)].copy()
print("chequeos fuera del ruido: %d (con regimen valido: %d) | ruedas %d" % (len(F), F.neg_total.notna().sum(), F.dia.nunique()))


def bloque(g, col, etiqueta):
    g = g[g[col].notna()]
    if len(g) == 0:
        return
    lin = "  %-10s" % etiqueta
    for v, nom in ((1.0, "NEG"), (0.0, "POS")):
        h = g[g[col] == v]
        if len(h) == 0:
            lin += " | %s n 0" % nom; continue
        pr = h.groupby("dia")["fcX"].mean()
        lin += " | %s n %3d (%2d r) fc %+.2f pts = %+.2f X, a favor %2.0f %%, t rueda %+.2f, f30 %+.2f, f60 %+.2f" % (
            nom, len(h), h.dia.nunique(), h.fc.mean(), h.fcX.mean(), 100 * (h.fc > 0).mean(), C.t_por_rueda(pr.values),
            h.f30.mean(), h.f60.mean())
    b, t = C.t_cluster(g["fcX"], g[col], g["dia"])
    lin += " | dif NEG-POS %+.2f X (t agrupada %+.2f)" % (b, t)
    print(lin)
    return b


def placebo_g2(g_full, col, n=2000):
    """Le pone a cada rueda el recorrido de regimen de otra rueda del mismo periodo (mismo minuto). p una cola (dif >= obs)."""
    reg = tab.pivot(index="dia", columns="minuto", values=col.replace("neg_total", "neg_total").replace("neg_estr", "neg_estr"))
    val = tab.pivot(index="dia", columns="minuto", values="valido")
    reg = reg.where(val.fillna(False).astype(bool))
    g = g_full.copy()
    obs_b, _ = C.t_cluster(g["fcX"], g[col], g["dia"])
    dias_per = {p: sorted(g_full[g_full.muestra == p].dia.unique()) for p in PER}
    cand = {p: [d for d in sorted(tab[tab.muestra == p].dia.unique()) if reg.loc[d].notna().sum() > 100] for p in PER}
    cnt, tot = 0, 0
    for _ in range(n):
        mapa = {}
        for p in PER:
            if not cand[p]:
                continue
            for d in dias_per[p]:
                mapa[d] = cand[p][RNG.integers(len(cand[p]))]
        x = np.array([reg.at[mapa[d], m] if d in mapa else np.nan for d, m in zip(g.dia, g.minuto)], dtype=float)
        mm = np.isfinite(x)
        if mm.sum() < 10 or x[mm].std() == 0:
            continue
        b, _ = C.t_cluster(g["fcX"].values[mm], x[mm], g["dia"].values[mm])
        if np.isfinite(b):
            tot += 1; cnt += b >= obs_b
    return obs_b, (cnt + 1) / (tot + 1), tot


for col, nom in (("neg_total", "reg_total (signo del GEX neto, primaria)"), ("neg_estr", "reg_estructural (sin 0DTE, secundaria)")):
    print("\n-- %s --" % nom)
    for per in PER + ["juntas"]:
        g = F if per == "juntas" else F[F.muestra == per]
        bloque(g, col, per)
    ob, p, nt = placebo_g2(F[F[col].notna()], col)
    print("  placebo (recorrido de regimen de otra rueda del mismo periodo, %d sorteos): dif obs %+.3f X, p una cola %.3f" % (nt, ob, p))
print("\n-- evolucion: fraccion de minutos negativos desde las 9:30 >= 0,5 (secundaria) --")
F["evo"] = np.where(F.frac_neg.notna(), (F.frac_neg >= 0.5).astype(float), np.nan)
for per in PER + ["juntas"]:
    g = F if per == "juntas" else F[F.muestra == per]
    bloque(g, "evo", per)
print("\n-- control: lado de la ruptura (las caidas caen debajo del flip mas seguido), reg_total, juntas --")
for lado, nom in ((1, "ruptura arriba"), (-1, "ruptura abajo")):
    bloque(F[F.lado == lado], "neg_total", nom)
print("\n-- control: tercios del tamano de la ruptura (|mov| / sigma), reg_total, juntas --")
cortes = F.tam.quantile([1 / 3, 2 / 3]).values
F["tercio"] = np.digitize(F.tam, cortes)
for k in range(3):
    bloque(F[F.tercio == k], "neg_total", "tercio %d" % (k + 1))
print("\n-- dejar un periodo afuera (G2): umbral de gex_norm elegido en dos periodos (maxima separacion de fcX, >= 20 %% por lado),"
      " probado en el tercero --")
G = F[F.gex_norm.notna()]
for fuera in PER:
    tr = G[G.muestra != fuera]; te = G[G.muestra == fuera]
    if len(te) == 0:
        print("  afuera %-9s sin dato" % fuera); continue
    mejor = None
    for th in np.quantile(tr.gex_norm, np.linspace(0.2, 0.8, 25)):
        a = tr[tr.gex_norm < th].fcX.mean(); b = tr[tr.gex_norm >= th].fcX.mean()
        if mejor is None or (a - b) > mejor[1]:
            mejor = (th, a - b)
    th = mejor[0]
    a = te[te.gex_norm < th]; b = te[te.gex_norm >= th]
    print("  afuera %-9s umbral %+.4f (entrenado: dif %+.2f X) | prueba: bajo el umbral n %d fc %+.2f X (a favor %.0f %%) | sobre n %d fc %+.2f X (%.0f %%) | dif %+.2f X" % (
        fuera, th, mejor[1], len(a), a.fcX.mean(), 100 * (a.fc > 0).mean(), len(b), b.fcX.mean(), 100 * (b.fc > 0).mean(),
        a.fcX.mean() - b.fcX.mean()))
print("\nEdad del dato en los chequeos con regimen: mediana %.0f s, p90 %.0f s, > 30 min: %.1f %%" % (
    F.edad_s.median(), F.edad_s.quantile(0.9), 100 * (F.edad_s > 1800).mean()))
