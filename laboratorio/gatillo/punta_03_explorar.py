# -*- coding: utf-8 -*-
"""punta_03_explorar.py — las 12 variantes pre-registradas contra las barreras del scalper. Por defecto SOLO en explorar.
Uso: python punta_03_explorar.py [explorar|confirmar] [sorteos] [variante1,variante2,...]
(confirmar se corre UNA vez y solo para los finalistas: lo hace punta_04_confirmar.py llamando a correr())."""
import sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, punta_lib as L


def correr(S, sorteos=50, solo=None, invertir=()):
    """S = {sesion: tabla}. Devuelve una lista de dicts, uno por variante y barrera."""
    F, OK, T0, Y = {}, {}, {}, {}
    for s, d in S.items():
        F[s] = L.variantes(L.rasgos(d), L.U); OK[s] = L.elegible(d); T0[s] = L.t0_de(d)
        Y[s] = {xh: L.barrera_cache(s, d, *xh) for xh in L.BARRERAS}
    nombres = [k for k in next(iter(F.values())).keys() if (solo is None or k in solo)]
    out = []
    for k in nombres:
        signo = -1 if k in invertir else 1
        disp = {}
        for s in S:
            i = L.desagrupar(F[s][k], OK[s]); disp[s] = (i, signo * F[s][k][i].astype(int))
        for (x, h) in L.BARRERAS:
            y = np.concatenate([Y[s][(x, h)][0][disp[s][0]] for s in S]); lado = np.concatenate([disp[s][1] for s in S])
            pto = np.concatenate([Y[s][(x, h)][1][disp[s][0]] for s in S]); dias = np.concatenate([[s] * len(disp[s][0]) for s in S])
            r = B.juzgar(y, lado, k + (" (INVERTIDA)" if signo < 0 else ""), dias, x)
            r["barrera"] = "+-%d/%ds" % (x, h); r["disparos"] = len(y); r["compra_%"] = round(100 * float((lado > 0).mean()), 1) if len(lado) else np.nan
            r["pts_netos"] = round(L.puntos_netos(y, lado, pto, x), 2)
            if sorteos and r.get("n", 0) > 0:
                m, sd = L.placebo(disp, OK, T0, {s: Y[s][(x, h)][0] for s in S}, sorteos=sorteos)
                r["placebo"] = round(m, 1); r["placebo_sd"] = round(sd, 2); r["z_placebo"] = round((r["acierto"] - m) / sd, 2) if sd > 0 else np.nan
            out.append(r)
    return out


def imprimir(out):
    df = pd.DataFrame(out)
    cols = ["nombre", "barrera", "disparos", "n", "acierto", "empate", "z", "placebo", "placebo_sd", "z_placebo", "dias_arriba", "peor_dia", "pts_netos", "compra_%"]
    print(df[[c for c in cols if c in df.columns]].to_string(index=False))
    return df


if __name__ == "__main__":
    cual = sys.argv[1] if len(sys.argv) > 1 else "explorar"
    sorteos = int(sys.argv[2]) if len(sys.argv) > 2 else 50
    solo = sys.argv[3].split(",") if len(sys.argv) > 3 else None
    if cual != "explorar": raise SystemExit("confirmar se corre con punta_04_confirmar.py, una sola vez")
    T = B.todas_las_sesiones(); S = B.explorar(T); del T
    print("EXPLORAR: %d sesiones | placebo con %d sorteos" % (len(S), sorteos))
    imprimir(correr(S, sorteos, solo))
