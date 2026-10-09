# -*- coding: utf-8 -*-
"""e05_grilla_nq.py — esceptico2: ¿lo de MUROS_NQ_vol en MIRAR es GAMMA o es la GRILLA DE STRIKES (numeros redondos)?
Medido antes: de las 195 llegadas de MUROS_NQ_vol noche MIRAR, 114 son a multiplos de 100 y 15 a multiplos de 50 (66 % en la grilla
de 50); en PRUEBA 67 + 20 de 93 (94 %). Controles SIN gamma, en las MISMAS noches, los MISMOS tramos con libro y el mismo juez:
  G100 = los dos multiplos de 100 que encierran el cierre de la vela anterior (sin mirar adelante), solo en los minutos en que la serie
         real tenia raya;  G50 = idem con multiplos de 50.
  SNAP = placebo pool oficial pero cada raya al azar redondeada a la grilla de 50 (la de los strikes): misma vida/distancia, en strike.
Uso: python -I e05_grilla_nq.py [n_juegos] -> datos/e05_grilla_nq.json / .txt"""
import json
import math
import os
import sys
import time

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import e00_comun as E  # noqa: E402
E.prioridad_baja()
import e01_juez_propio as P  # noqa: E402

import numpy as np  # noqa: E402
import pandas as pd  # noqa: E402

CASOS = ["nq|MUROS_NQ_vol|noche|mirar", "nq|MUROS_NQ_vol|noche|prueba", "nq|MAJORS_NQ_vol|noche|mirar", "nq|MUROS_NQ_vol.P|noche|mirar",
         "nq|MUROS_NQ_vol.P|rueda|prueba"]
SEM = 5052026


def grilla(cs, Vx, paso):
    tm = Vx["tm"]; c = Vx["c"]
    pos = {int(m): j for j, m in enumerate(tm)}
    out = {}
    for t, v in cs["rayas"].items():
        k = pd.Timestamp(t).value // 60_000_000_000
        if not v:
            out[t] = {}
            continue
        j = pos.get(k - 1)
        if j is None:
            out[t] = {}
            continue
        x = c[j]
        lo = math.floor(x / paso) * paso; hi = lo + paso
        out[t] = {"lo": lo, "hi": hi}
    return out


def pct(evs):
    b = [e for e in evs if e["res"] != "censurada"]
    return (100.0 * sum(e["res"] == "acierto" for e in b) / len(b) if b else float("nan")), len(b)


def pool_p(Vx, PS, real_pct, n, rng, snap=None):
    d0 = np.array([vs[0] - Vx["o"][ix[0]] for ix, vs in PS], float)
    v0 = np.array([vs[0] for ix, vs in PS], float)
    ad = np.abs(d0)
    res = []
    for _ in range(n):
        off = rng.choice([-1.0, 1.0], len(ad)) * rng.choice(ad, len(ad), replace=True) - d0
        if snap:
            off = np.round((v0 + off) / snap) * snap - v0
        res.append(pct(P.juzgar(Vx, PS, off))[0])
    a = np.array(res); a = a[np.isfinite(a)]
    return float(np.median(a)), (1.0 + (a >= real_pct).sum()) / (len(a) + 1.0), 100.0 * ((a < real_pct).sum() + 0.5 * (a == real_pct).sum()) / len(a)


def main():
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 1000
    rng = np.random.default_rng(SEM)
    out = {}; lin = []
    for nombre in CASOS:
        t0 = time.time()
        cs = E.caso(nombre)
        Vx = P.velas(cs["V"], cs["ventanas"])
        PS = P.pistas(Vx, cs["rayas"], 0, None)
        pr, nr = pct(P.juzgar(Vx, PS))
        r = dict(real=pr, n_real=nr)
        # SNAP: placebo pool redondeado a la grilla de 50
        r["snap50_p50"], r["snap50_p"], r["snap50_pctl"] = pool_p(Vx, PS, pr, n, rng, snap=50.0)
        for paso in (100.0, 50.0):
            g = grilla(cs, Vx, paso)
            PG = P.pistas(Vx, g, 0, None)
            pg, ng = pct(P.juzgar(Vx, PG))
            gp50, gp, gpctl = pool_p(Vx, PG, pg, n // 2, rng)
            r["G%d" % paso] = dict(pct=pg, n=ng, azar_p50=gp50, p=gp, percentil=gpctl,
                                   real_menos_grilla=pr - pg)
        out[nombre] = r
        s = ("%-32s real %5.1f%% (n %d) | snap50 azar p50 %5.1f p %.4f | G100 %5.1f%% (n %d; su azar %5.1f, p %.3f) | "
             "G50 %5.1f%% (n %d; su azar %5.1f, p %.3f) | %.0f s") % (
            nombre, pr, nr, r["snap50_p50"], r["snap50_p"], r["G100"]["pct"], r["G100"]["n"], r["G100"]["azar_p50"], r["G100"]["p"],
            r["G50"]["pct"], r["G50"]["n"], r["G50"]["azar_p50"], r["G50"]["p"], time.time() - t0)
        print(s, flush=True); lin.append(s)
    with open(os.path.join(AQUI, "datos", "e05_grilla_nq.json"), "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1, default=float)
    with open(os.path.join(AQUI, "datos", "e05_grilla_nq.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(lin) + "\n")


if __name__ == "__main__":
    main()
