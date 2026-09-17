# -*- coding: utf-8 -*-
"""rev_05_placebo_justo.py — (4) PLACEBO JUSTO. El del banco (+-12,5 y +-7) cae ADENTRO del tunel de 25 pts entre las dos verdes. Aca:
 (a) PERFIL DE CORRIMIENTOS: la misma cuenta con las rayas corridas de -75 a +75 cada 2,5 pts (61 corrimientos): la raya de verdad (0) sobresale o es una mas?
 (b) corrimientos grandes pedidos: +-37,5 y +-62,5 (medio strike, afuera del tunel), y +-25 / +-50 (los strikes VECINOS, que no son la dominante);
 (c) RAYAS AL AZAR: cada raya de la sesion corrida por un numero al azar entre 5 y 100 pts (signo al azar), 40 sorteos: donde cae la de verdad en esa distribucion."""
import os, sys, time
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rev_lib as R, verdes_lib as V

t0 = time.time(); T = R.sesiones(); F, _ = V.fotos("NQ"); P = dict(R.P0); rng = np.random.default_rng(11)
PRE = {}
for s, seg in T.items():
    L2 = V.rayas_por_segundo(seg, F); PRE[s] = (R.Cinta1s(seg), R.lineas_de(L2))

OBJ = (20, 12); corr = np.arange(-75, 75.01, 2.5); filas = []
for c in corr:
    for s, (A, lin) in PRE.items():
        for Lt in lin:
            g = R.gatillos(A, Lt + c, P)
            for obj in OBJ:
                for x in R.resultado(A, g, obj): filas.append((c, s, obj, x["i"], x["lado"], x["neto"]))
D = pd.DataFrame(filas, columns=["corr", "dia", "obj", "i", "lado", "neto"]); D.to_parquet(os.path.join(R.REVCACHE, "perfil_corrimientos.parquet"))
print("perfil de corrimientos listo en %.0f s" % (time.time() - t0))
for obj in OBJ:
    d = D[D["obj"] == obj]; m = d.groupby("corr")["neto"].agg(["mean", "size", "sum"]); real = m.loc[0.0, "mean"]; otros = m.drop(0.0)
    print("\n=== objetivo %s ===  VERDES (corrimiento 0): %+.2f (n %d)" % (obj, real, m.loc[0.0, "size"]))
    print("   los otros 60 corrimientos: media %+.2f, mediana %+.2f, desvio %.2f, min %+.2f, max %+.2f | corrimientos con neto >= el de las verdes: %d de 60 | con neto > 0: %d de 60" % (
        otros["mean"].mean(), otros["mean"].median(), otros["mean"].std(), otros["mean"].min(), otros["mean"].max(), (otros["mean"] >= real).sum(), (otros["mean"] > 0).sum()))
    print("   z de las verdes contra la nube de corrimientos: %.2f" % ((real - otros["mean"].mean()) / otros["mean"].std()))
    print("   perfil (corrimiento: neto/n): " + "  ".join("%+.1f:%+.2f/%d" % (c, r["mean"], r["size"]) for c, r in m.iterrows()))
    for nombre, cs in (("tipo banco +-12,5 +-7,5 (adentro del tunel)", (12.5, -12.5, 7.5, -7.5)), ("+-37,5 (medio strike, afuera)", (37.5, -37.5)), ("+-62,5 (medio strike, afuera)", (62.5, -62.5)),
                       ("+-25 (strike vecino)", (25.0, -25.0)), ("+-50 (dos strikes)", (50.0, -50.0)), ("todos los medios strikes +-12,5 37,5 62,5", (12.5, -12.5, 37.5, -37.5, 62.5, -62.5)),
                       ("todo lo que esta a >= 5 pts de un strike", tuple(c for c in corr if 5 <= (abs(c) % 25) <= 20))):
        x = d[d["corr"].isin(cs)]; pdia = x.groupby("dia")["neto"].mean(); rdia = d[d["corr"] == 0.0].groupby("dia")["neto"].mean()
        print("   placebo %-44s n %5d | neto %+.2f | sin 16/17-09: %+.2f | dias en que las verdes le ganan: %d de %d" % (nombre, len(x), x["neto"].mean(), x[~x["dia"].isin(["2026-09-16", "2026-09-17"])]["neto"].mean(), (rdia > pdia.reindex(rdia.index)).sum(), len(rdia)))
    sin = d[~d["dia"].isin(["2026-09-16", "2026-09-17"])].groupby("corr")["neto"].mean()
    print("   SIN el 16 y el 17-09: verdes %+.2f | otros corrimientos: media %+.2f, desvio %.2f | con neto >= verdes: %d de 60" % (sin.loc[0.0], sin.drop(0.0).mean(), sin.drop(0.0).std(), (sin.drop(0.0) >= sin.loc[0.0]).sum()))

# (c) rayas al azar: cada raya (identidad) con su propio corrimiento al azar
print("\n(c) RAYAS AL AZAR de la misma sesion (cada raya corrida 5-100 pts con signo al azar), 40 sorteos:")
res = {20: [], 12: []}
for k in range(40):
    acc = {20: [], 12: []}
    for s, (A, lin) in PRE.items():
        for Lt in lin:
            c = rng.uniform(5, 100) * rng.choice((-1, 1)); g = R.gatillos(A, Lt + c, P)
            for obj in OBJ: acc[obj] += [x["neto"] for x in R.resultado(A, g, obj)]
    for obj in OBJ: res[obj].append((np.mean(acc[obj]), len(acc[obj])))
for obj in OBJ:
    m = np.array([x[0] for x in res[obj]]); n = np.array([x[1] for x in res[obj]]); real = D[(D["obj"] == obj) & (D["corr"] == 0.0)]["neto"].mean()
    print("   objetivo %s: azar media %+.2f, desvio %.2f, p5 %+.2f, p95 %+.2f, max %+.2f (n medio %d) | verdes %+.2f | sorteos que igualan o superan a las verdes: %d de 40" % (obj, m.mean(), m.std(), np.percentile(m, 5), np.percentile(m, 95), m.max(), n.mean(), real, (m >= real).sum()))
print("%.0f s" % (time.time() - t0))
