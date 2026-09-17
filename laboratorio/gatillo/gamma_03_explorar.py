# -*- coding: utf-8 -*-
"""gamma_03_explorar.py — las 12 variantes pre-registradas, SOLO en la mitad EXPLORAR (posiciones pares). Barrera principal +-8/600 s,
secundarias +-5/300 y +-12/900; placebo de 200 sorteos (misma sesion, misma media hora, mismos lados); desglose por regimen."""
import sys, time, json; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, gamma_lib as L

MITAD = sys.argv[1] if len(sys.argv) > 1 else "EXP"
SOLO = sys.argv[2].split(",") if len(sys.argv) > 2 else None
assert MITAD == "EXP", "este script es SOLO para explorar"
t0 = time.time(); T = B.todas_las_sesiones(); EXP, CONF = L.mitades(T); S = EXP; del T, CONF
DISP, OK, MH, Y, NV, MOV = {}, {}, {}, {x: {} for x, h in L.BARRERAS}, {}, {}
for s, d in S.items():
    disp, f, nv = L.disparos(d); DISP[s] = disp; OK[s] = f["elegible"].to_numpy(); MH[s] = L.media_hora(d)
    NV[s] = nv[["d_zero", "cuadrante"]].to_numpy()
    for x, h in L.BARRERAS: Y[x][s] = L.barrera_cache(s, d, x, h)[0]
    MOV[s] = {v: L.movimiento_firmado(d, i, lado) for v, (i, lado) in disp.items()}
print("cargado en %.0f s" % (time.time() - t0))

variantes = [v for v in next(iter(DISP.values())) if v.startswith("V")]
if SOLO: variantes = [v for v in variantes if v[:3] in SOLO]
filas = []
print("\nEXPLORAR (%d sesiones) | empate +-8: %.1f %% | +-5: %.1f %% | +-12: %.1f %%" % (len(S), 100 * B.empate(8), 100 * B.empate(5), 100 * B.empate(12)))
for v in variantes:
    disp = {s: DISP[s][v] for s in S}
    lado = np.concatenate([disp[s][1] for s in S]); dias = np.concatenate([[s] * len(disp[s][0]) for s in S])
    fila = dict(var=v)
    for x, h in L.BARRERAS:
        y = np.concatenate([Y[x][s][disp[s][0]] for s in S]); r = B.juzgar(y, lado, v, dias, x)
        if r["n"] == 0: continue
        pm, ps = L.placebo(disp, OK, MH, Y[x]); zp = (r["acierto"] - pm) / ps if ps and ps > 0 else np.nan
        fila.update({"n%d" % x: r["n"], "ac%d" % x: r["acierto"], "z%d" % x: r["z"], "pl%d" % x: round(pm, 1), "zp%d" % x: round(zp, 2), "dias%d" % x: r["dias_arriba"], "peor%d" % x: r["peor_dia"], "sinres%d" % x: r["sin_resolver"]})
    # desglose por regimen (zero) y por lado long/short, barrera principal
    y8 = np.concatenate([Y[8][s][disp[s][0]] for s in S]); z = np.concatenate([NV[s][disp[s][0], 0] for s in S]); ok = y8 != 0; g = (y8 * lado) > 0
    for nb, m in (("POS", z >= L.ZONA), ("NEG", z <= -L.ZONA), ("MEDIO", np.abs(z) < L.ZONA), ("LARGO", lado > 0), ("CORTO", lado < 0)):
        mm = m & ok; fila["n_" + nb] = int(mm.sum()); fila["ac_" + nb] = round(100 * float(g[mm].mean()), 1) if mm.any() else np.nan
        if nb in ("POS", "NEG") and mm.any():
            dd = pd.Series(g[mm]).groupby(dias[mm]).agg(["mean", "size"]); dd = dd[dd["size"] >= 5]; fila["dias_" + nb] = "%d de %d" % (int((dd["mean"] > 0.5).sum()), len(dd))
    mv = pd.DataFrame([MOV[s][v] for s in S if len(DISP[s][v][0])]); w = np.array([len(DISP[s][v][0]) for s in S if len(DISP[s][v][0])])
    for hh in mv.columns: fila["mov%d" % hh] = round(float(np.average(mv[hh], weights=w)), 2)
    filas.append(fila)
    print("%-26s n %4d | +-8: %5.1f %% z %+5.2f | placebo %5.1f -> z %+5.2f | dias %s peor %3.0f %% | +-5: %5.1f %% (pl %5.1f, z %+5.2f) | +-12: %5.1f %% (pl %5.1f, z %+5.2f)" % (
        v, fila.get("n8", 0), fila.get("ac8", np.nan), fila.get("z8", np.nan), fila.get("pl8", np.nan), fila.get("zp8", np.nan), fila.get("dias8", ""), fila.get("peor8", np.nan),
        fila.get("ac5", np.nan), fila.get("pl5", np.nan), fila.get("zp5", np.nan), fila.get("ac12", np.nan), fila.get("pl12", np.nan), fila.get("zp12", np.nan)))
    print("   regimen: POS n %4d ac %5.1f %% (dias %s) | NEG n %4d ac %5.1f %% (dias %s) | largos n %4d ac %5.1f | cortos n %4d ac %5.1f | mov firmado 10/30/60/120/300 s: %s" % (
        fila["n_POS"], fila["ac_POS"], fila.get("dias_POS", "-"), fila["n_NEG"], fila["ac_NEG"], fila.get("dias_NEG", "-"), fila["n_LARGO"], fila["ac_LARGO"], fila["n_CORTO"], fila["ac_CORTO"],
        " / ".join("%+.2f" % fila["mov%d" % hh] for hh in (10, 30, 60, 120, 300))))
pd.DataFrame(filas).to_json(L.CACHE + "/explorar_tabla.json", orient="records", indent=1)
print("\n%.0f s" % (time.time() - t0))
