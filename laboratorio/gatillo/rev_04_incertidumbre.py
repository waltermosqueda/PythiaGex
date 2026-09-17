# -*- coding: utf-8 -*-
"""rev_04_incertidumbre.py — (3) cuanta certeza hay: bootstrap POR DIAS (los 7 dias son la muestra de verdad, no las 158 operaciones), t por operaciones y por dias,
cambio de signos por dia (exacto, 2^7), aporte de cada dia, y que queda sacando un dia o dos. Usa las operaciones guardadas por rev_01 (y la estela real de rev_03)."""
import os, sys, itertools
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rev_lib as R

rng = np.random.default_rng(7)


def boot_dias(real, plc, B=20000):
    dias = sorted(set(real["dia"]) | set(plc["dia"])); sr = real.groupby("dia")["neto"].agg(["sum", "size"]).reindex(dias).fillna(0).to_numpy(); sp = plc.groupby("dia")["neto"].agg(["sum", "size"]).reindex(dias).fillna(0).to_numpy()
    k = rng.integers(0, len(dias), size=(B, len(dias))); a = sr[k].sum(axis=1); b = sp[k].sum(axis=1)
    mr = a[:, 0] / np.maximum(1, a[:, 1]); mp = b[:, 0] / np.maximum(1, b[:, 1]); return mr, mp, mr - mp


def t_de(x): x = np.asarray(x, float); return x.mean() / (x.std(ddof=1) / np.sqrt(len(x))) if len(x) > 1 and x.std(ddof=1) > 0 else np.nan


def signos(x):
    """p de una cola, exacta: fraccion de las 2^n asignaciones de signo con suma >= la observada."""
    x = np.asarray(x, float); obs = x.sum(); c = 0; n = 0
    for s in itertools.product((1, -1), repeat=len(x)): n += 1; c += (np.dot(s, x) >= obs - 1e-9)
    return c / n


for obj in (20, 12):
    real = pd.read_parquet(os.path.join(R.REVCACHE, "real_obj%s.parquet" % obj)); plc = pd.read_parquet(os.path.join(R.REVCACHE, "plc_obj%s.parquet" % obj))
    print("\n================ objetivo %s ================" % obj)
    for nombre, rr in (("tal cual el banco (n %d)" % len(real), real), ("sin gatillos repetidos", real.drop_duplicates(["dia", "i", "lado"]))):
        mr, mp, md = boot_dias(rr, plc)
        print("[%s]" % nombre)
        print("   verdes: neto %+.2f pts/op | bootstrap por dias IC90 [%+.2f ; %+.2f] | P(neto <= 0) = %.0f %%" % (rr["neto"].mean(), np.percentile(mr, 5), np.percentile(mr, 95), 100 * (mr <= 0).mean()))
        print("   verdes - placebo: %+.2f | IC90 [%+.2f ; %+.2f] | P(dif <= 0) = %.0f %%" % (rr["neto"].mean() - plc["neto"].mean(), np.percentile(md, 5), np.percentile(md, 95), 100 * (md <= 0).mean()))
    pd_ = real.groupby("dia")["neto"].agg(["sum", "size", "mean"]); pp = plc.groupby("dia")["neto"].agg(["sum", "size", "mean"])
    print("   t por OPERACIONES (supone 158 tiradas independientes; no lo son): verdes t = %.2f | verdes contra placebo (Welch) t = %.2f" % (
        t_de(real["neto"]), (real["neto"].mean() - plc["neto"].mean()) / np.sqrt(real["neto"].var(ddof=1) / len(real) + plc["neto"].var(ddof=1) / len(plc))))
    dif = (pd_["mean"] - pp["mean"]).dropna()
    print("   t por DIAS (n = %d): media de las medias diarias %+.2f, t = %.2f | diferencia diaria con placebo %+.2f, t = %.2f | dias con verdes > placebo: %d de %d" % (
        len(pd_), pd_["mean"].mean(), t_de(pd_["mean"]), dif.mean(), t_de(dif), (dif > 0).sum(), len(dif)))
    print("   cambio de signos por dia (exacto, 128 casos): p de una cola del TOTAL de las verdes = %.3f | de (total verdes - n x media placebo del dia) = %.3f" % (
        signos(pd_["sum"]), signos((pd_["sum"] - pd_["size"] * pp["mean"].reindex(pd_.index)).dropna())))
    tot = pd_["sum"].sum()
    print("   aporte por dia al total (%+.1f): %s" % (tot, " | ".join("%s %+.1f (%.0f %%)" % (d[5:], v, 100 * v / tot) for d, v in pd_["sum"].items())))
    print("   sacando UN dia: " + " | ".join("sin %s %+.2f" % (d[5:], real[real["dia"] != d]["neto"].mean()) for d in pd_.index))
    s2 = real[~real["dia"].isin(["2026-09-16", "2026-09-17"])]; p2 = plc[~plc["dia"].isin(["2026-09-16", "2026-09-17"])]
    print("   sacando el 16 y el 17-09: verdes n %d, neto %+.2f (total %+.1f) | placebo n %d, neto %+.2f" % (len(s2), s2["neto"].mean(), s2["neto"].sum(), len(p2), p2["neto"].mean()))
    # concentracion en pocas operaciones
    o = real["neto"].sort_values(ascending=False).to_numpy()
    print("   concentracion: las 10 mejores operaciones suman %+.1f; el total es %+.1f; ganadoras %d de %d; desvio por operacion %.1f pts" % (o[:10].sum(), o.sum(), (real["gana"] == 1).sum(), len(real), real["neto"].std(ddof=1)))
    # potencia: dias de sombra para distinguir el efecto de cero, con la dispersion entre dias observada
    m = pd_["mean"].mean(); sd_d = pd_["mean"].std(ddof=1); n_dia = len(real) / len(pd_)
    sd_op = real["neto"].std(ddof=1)
    for efecto, nombre in ((real["neto"].mean(), "el medido"), (s2["neto"].mean() if s2["neto"].mean() > 0 else 0.4, "sin 16/17 (o 0,4 si es <= 0)"), (0.8, "0,8 (con la estela real)")):
        if efecto <= 0: continue
        # por operaciones independientes: n = ((1.645 + 0.84) * sd / efecto)^2  (una cola 5 %, potencia 80 %)
        n_ops = ((1.645 + 0.84) * sd_op / efecto) ** 2
        n_dias_cluster = ((1.645 + 0.84) * sd_d / efecto) ** 2
        print("   potencia (una cola 5 %%, 80 %%) para un efecto de %+.2f pts/op [%s]: %.0f operaciones independientes = %.0f ruedas a %.0f por rueda | con la dispersion ENTRE DIAS observada (desvio %.2f): %.0f ruedas" % (
            efecto, nombre, n_ops, n_ops / n_dia, n_dia, sd_d, n_dias_cluster))
