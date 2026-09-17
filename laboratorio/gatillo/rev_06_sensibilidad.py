# -*- coding: utf-8 -*-
"""rev_06_sensibilidad.py — (5) el signo se mantiene o depende de una combinacion? Grilla COMPLETA de los 5 parametros del gatillo (3^5 = 243) x stop a 1 o 2 pts de la mecha
x objetivo 20 / 12 x costo 0,96 / 1,25, sobre las verdes; y de a UN parametro por vez contra placebo (+-12,5 y +-37,5). Uso: python rev_06_sensibilidad.py [grilla|uno]"""
import os, sys, time, itertools
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rev_lib as R, verdes_lib as V

modo = sys.argv[1] if len(sys.argv) > 1 else "grilla"
t0 = time.time(); T = R.sesiones(); F, _ = V.fotos("NQ"); PRE = {s: (R.Cinta1s(seg), R.lineas_de(V.rayas_por_segundo(seg, F))) for s, seg in T.items()}
EJES = dict(tol=(1.0, 1.5, 2.5), lejos=(8.0, 10.0, 15.0), pen_max=(6.0, 10.0, 15.0), rec=(1.0, 1.5, 2.5), visita=(60, 90, 180))


def corre(P, corrs=(0.0,)):
    out = []
    for s, (A, lin) in PRE.items():
        for Lt in lin:
            for c in corrs:
                g = R.gatillos(A, Lt + c, P)
                for so in (1.0, 2.0):
                    for obj in (20, 12):
                        for x in R.resultado(A, g, obj, costo=0.0, stop_off=so): out.append((s, c, so, obj, x["bruto"]))
    return pd.DataFrame(out, columns=["dia", "corr", "stop", "obj", "bruto"])


if modo == "grilla":
    filas = []
    for combo in itertools.product(*EJES.values()):
        P = dict(R.P0); P.update(dict(zip(EJES.keys(), combo))); d = corre(P)
        for (so, obj), g in d.groupby(["stop", "obj"]):
            g2 = g[~g["dia"].isin(["2026-09-16", "2026-09-17"])]
            filas.append(dict(zip(EJES.keys(), combo), stop=so, obj=obj, n=len(g), bruto=g["bruto"].mean(), n_sin=len(g2), bruto_sin=g2["bruto"].mean() if len(g2) else np.nan,
                              dias_pos=int((g.groupby("dia")["bruto"].mean() > 0.96).sum())))
    G = pd.DataFrame(filas); G.to_parquet(os.path.join(R.REVCACHE, "sensibilidad_grilla.parquet")); print("grilla lista en %.0f s: %d filas" % (time.time() - t0, len(G)))
    for costo in (0.96, 1.25):
        for obj in (20, 12):
            for so in (1.0, 2.0):
                g = G[(G["obj"] == obj) & (G["stop"] == so)]; net = g["bruto"] - costo; ns = g["bruto_sin"] - costo
                print("costo %.2f | objetivo %2d | stop a %.0f pt de la mecha: %3d combinaciones | neto > 0 en %3d (%.0f %%) | neto mediano %+.2f (min %+.2f, max %+.2f) | n mediano %d || SIN 16/17-09: neto > 0 en %3d (%.0f %%), mediano %+.2f" % (
                    costo, obj, so, len(g), (net > 0).sum(), 100 * (net > 0).mean(), net.median(), net.min(), net.max(), g["n"].median(), (ns > 0).sum(), 100 * (ns > 0).mean(), ns.median()))
    print("\npor valor de cada parametro (objetivo 20, stop 1, costo 0,96): neto medio de las 81 combinaciones que lo usan | sin 16/17-09")
    g = G[(G["obj"] == 20) & (G["stop"] == 1.0)]
    for k, vals in EJES.items(): print("   %-8s %s" % (k, " | ".join("%g: %+.2f / %+.2f (n %d)" % (v, g[g[k] == v]["bruto"].mean() - 0.96, g[g[k] == v]["bruto_sin"].mean() - 0.96, g[g[k] == v]["n"].median()) for v in vals)))
    b = G[(G["obj"] == 20) & (G["stop"] == 1.0) & (G["tol"] == 1.5) & (G["lejos"] == 10) & (G["pen_max"] == 10) & (G["rec"] == 1.5) & (G["visita"] == 90)].iloc[0]
    print("   la combinacion del banco: neto %+.2f; puesto %d de 243 (1 = la mejor)" % (b["bruto"] - 0.96, 1 + (g["bruto"] > b["bruto"]).sum()))
else:
    PLC = (12.5, -12.5, 37.5, -37.5)
    print("la combinacion del banco con stop a 1 y a 2 pts de la mecha, contra placebo (costo 0,96):")
    d0 = corre(dict(R.P0), (0.0,) + PLC + (62.5, -62.5))
    for obj in (20, 12):
        for so in (1.0, 2.0):
            d = d0[(d0["obj"] == obj) & (d0["stop"] == so)]; r = d[d["corr"] == 0.0]; q = d[d["corr"] != 0.0]; sin = ~d["dia"].isin(["2026-09-16", "2026-09-17"])
            print("   objetivo %2d stop %.0f: VERDES %+.2f (n %d) | sin 16/17 %+.2f || PLACEBO (+-12,5 +-37,5 +-62,5) %+.2f (n %d) | sin 16/17 %+.2f" % (
                obj, so, r["bruto"].mean() - 0.96, len(r), d[(d["corr"] == 0.0) & sin]["bruto"].mean() - 0.96, q["bruto"].mean() - 0.96, len(q), d[(d["corr"] != 0.0) & sin]["bruto"].mean() - 0.96))
    print("de a UN parametro (los demas como el banco). objetivo 20, stop 1, costo 0,96: VERDES neto (n) | sin 16/17 | PLACEBO +-12,5 | PLACEBO +-37,5")
    for k, vals in EJES.items():
        for v in vals:
            P = dict(R.P0); P[k] = v; d = corre(P, (0.0,) + PLC); d = d[(d["obj"] == 20) & (d["stop"] == 1.0)]
            r = d[d["corr"] == 0.0]; r2 = r[~r["dia"].isin(["2026-09-16", "2026-09-17"])]; p1 = d[d["corr"].abs() == 12.5]; p2 = d[d["corr"].abs() == 37.5]
            print("   %-8s = %-5g VERDES %+.2f (n %3d) | sin 16/17 %+.2f (n %3d) | placebo +-12,5 %+.2f (n %3d) | placebo +-37,5 %+.2f (n %3d)" % (
                k, v, r["bruto"].mean() - 0.96, len(r), r2["bruto"].mean() - 0.96, len(r2), p1["bruto"].mean() - 0.96, len(p1), p2["bruto"].mean() - 0.96, len(p2)))
    # sin el filtro de delta y con otras ventanas de delta
    for nombre, kv in (("sin filtro de delta", dict(delta=False)), ("delta 5 s", dict(vent_delta=5)), ("delta 20 s", dict(vent_delta=20)), ("delta 30 s", dict(vent_delta=30))):
        P = dict(R.P0); P.update(kv); d = corre(P, (0.0,) + PLC); d = d[(d["obj"] == 20) & (d["stop"] == 1.0)]
        r = d[d["corr"] == 0.0]; r2 = r[~r["dia"].isin(["2026-09-16", "2026-09-17"])]; p1 = d[d["corr"].abs() == 12.5]; p2 = d[d["corr"].abs() == 37.5]
        print("   %-16s VERDES %+.2f (n %3d) | sin 16/17 %+.2f (n %3d) | placebo +-12,5 %+.2f (n %3d) | placebo +-37,5 %+.2f (n %3d)" % (
            nombre, r["bruto"].mean() - 0.96, len(r), r2["bruto"].mean() - 0.96, len(r2), p1["bruto"].mean() - 0.96, len(p1), p2["bruto"].mean() - 0.96, len(p2)))
print("%.0f s" % (time.time() - t0))
