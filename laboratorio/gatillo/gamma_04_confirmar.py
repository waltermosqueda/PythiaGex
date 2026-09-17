# -*- coding: utf-8 -*-
"""gamma_04_confirmar.py — las finalistas, UNA vez, en la mitad CONFIRMAR (posiciones impares). Mismo codigo que explorar (gamma_lib).
Uso: python gamma_04_confirmar.py CONF V04,V06      (con EXP reproduce la tabla de explorar: control de que el script es el mismo)"""
import sys, time; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, gamma_lib as L

MITAD = sys.argv[1]; SOLO = sys.argv[2].split(",")
t0 = time.time(); T = B.todas_las_sesiones(); EXP, CONF = L.mitades(T); S = EXP if MITAD == "EXP" else CONF; del T
DISP, OK, MH, Y, NV, MOV = {}, {}, {}, {x: {} for x, h in L.BARRERAS}, {}, {}
for s, d in S.items():
    disp, f, nv = L.disparos(d); DISP[s] = {v: k for v, k in disp.items() if v[:3] in SOLO}; OK[s] = f["elegible"].to_numpy(); MH[s] = L.media_hora(d)
    NV[s] = nv[["d_zero", "cuadrante"]].to_numpy()
    for x, h in L.BARRERAS: Y[x][s] = L.barrera_cache(s, d, x, h)[0]
    MOV[s] = {v: L.movimiento_firmado(d, i, lado) for v, (i, lado) in DISP[s].items()}
print("%s (%d sesiones: %s)" % (MITAD, len(S), ", ".join(x[5:] for x in sorted(S))))
for v in sorted(next(iter(DISP.values()))):
    disp = {s: DISP[s][v] for s in S}; lado = np.concatenate([disp[s][1] for s in S]); dias = np.concatenate([[s] * len(disp[s][0]) for s in S])
    print("\n=== %s" % v)
    for x, h in L.BARRERAS:
        y = np.concatenate([Y[x][s][disp[s][0]] for s in S]); r = B.juzgar(y, lado, v, dias, x); pm, ps = L.placebo(disp, OK, MH, Y[x])
        neto = (r["acierto"] / 100.0) * x - (1 - r["acierto"] / 100.0) * x - B.COSTO_PTS
        print("  +-%d/%d s: n %4d | acierto %5.1f %% (empate %.1f) | z50 %+5.2f | placebo %5.1f +- %.2f -> z %+5.2f | dias arriba %s, peor %3.0f %% | sin resolver %d | neto %+.2f pts/op" % (
            x, h, r["n"], r["acierto"], 100 * B.empate(x), r["z"], pm, ps, (r["acierto"] - pm) / ps, r["dias_arriba"], r["peor_dia"], r["sin_resolver"], neto))
    y8 = np.concatenate([Y[8][s][disp[s][0]] for s in S]); z = np.concatenate([NV[s][disp[s][0], 0] for s in S]); q = np.concatenate([NV[s][disp[s][0], 1] for s in S]); ok = y8 != 0; g = (y8 * lado) > 0
    for nb, m in (("zero POS", z >= L.ZONA), ("zero NEG", z <= -L.ZONA), ("zero MEDIO", np.abs(z) < L.ZONA), ("cuad 1/3 (en contra)", (q == 1) | (q == 3)), ("cuad 2/4 (seguir)", (q == 2) | (q == 4)), ("largos", lado > 0), ("cortos", lado < 0)):
        mm = m & ok
        if not mm.any(): continue
        dd = pd.Series(g[mm]).groupby(dias[mm]).agg(["mean", "size"]); dd = dd[dd["size"] >= 5]
        print("    %-22s n %4d | acierto %5.1f %% | dias (>= 5 disparos) arriba de 50 %%: %d de %d" % (nb, int(mm.sum()), 100 * g[mm].mean(), int((dd["mean"] > 0.5).sum()), len(dd)))
    dd = pd.Series(g[ok]).groupby(dias[ok]).agg(["mean", "size"])
    print("    por dia: " + " | ".join("%s %2.0f %% (%d)" % (k[5:], 100 * r_["mean"], r_["size"]) for k, r_ in dd.iterrows()))
    mv = pd.DataFrame([MOV[s][v] for s in S if len(DISP[s][v][0])]); w = np.array([len(DISP[s][v][0]) for s in S if len(DISP[s][v][0])])
    print("    movimiento medio firmado a 10/30/60/120/300 s: " + " / ".join("%+.2f" % float(np.average(mv[hh], weights=w)) for hh in mv.columns))
print("\n%.0f s" % (time.time() - t0))
