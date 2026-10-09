# -*- coding: utf-8 -*-
"""esc_07_grilla.py — ESCEPTICO 1: CONTROL SIN GAMMA para las series de strikes de NQ. Los strikes de NQ son numeros redondos (multiplos
de 25/50/100). Si una raya en el numero redondo mas cercano al precio, arriba y abajo (sin ninguna informacion de opciones), saca lo mismo
que MUROS_NQ_vol, lo que 'le gana al placebo' es el numero redondo, no el GEX. Mismo tiempo que la serie (precio = cierre de la ultima
m2 cerrada, clave t vigente en la vela t), mismas ventanas (tramos con libro NQ) y mismas noches. SOLO LECTURA.
(1) juez propio en MIRAR y PRUEBA para GRILLA_25/50/100 (2 rayas: la de arriba y la de abajo);
(2) juez20 oficial con su placebo (500 juegos) para GRILLA_50 y GRILLA_100 en MIRAR noche (¿el placebo oficial tambien la da 'ganadora'?).
Uso: python -I esc_07_grilla.py"""
import ctypes
try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass
import json
import math
import os
import sys
import time

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import numpy as np  # noqa: E402
import pandas as pd  # noqa: E402
import esc_comun as E  # noqa: E402

LOG = []


def out(s):
    print(s, flush=True); LOG.append(s)


def grilla_desde(rayas_serie, futs, paso):
    """misma grilla de claves que la serie (clave vacia donde la serie no tiene libro): arriba = ceil(fut/paso)*paso, abajo = floor."""
    g = {}
    for t, v in rayas_serie.items():
        f = futs.get(t)
        if v == {} or f is None or not (f == f):
            g[t] = {}
            continue
        a = math.ceil(f / paso) * paso; b = math.floor(f / paso) * paso
        if a == b:
            a = b + paso
        g[t] = {"G.D1": (float(a), None), "G.D2": (float(b), None)}
    return g


def main():
    J = E.j20()
    V, reg, part = E.nq_cargar()
    VV = E.preparar_velas(V)
    futs = {pd.Timestamp(r["k"] * 60, unit="s"): r["fut"] for d in reg for r in reg[d]}
    res = {}
    for vt in ("noche", "rueda"):
        for parte in ("mirar", "prueba"):
            dias = part[vt][parte]
            rs, vent = E.nq_rayas_y_ventanas(reg, dias, "MUROS_NQ_vol", vt)
            r0, _ = E.juzgar(VV, rs, vent)
            fila = {"MUROS_NQ_vol": r0}
            for paso in (25, 50, 100):
                g = grilla_desde(rs, futs, paso)
                r, _ = E.juzgar(VV, g, vent)
                fila["GRILLA_%d" % paso] = r
            res["%s|%s" % (vt, parte)] = fila
            out("NQ %-5s %-6s MUROS_NQ_vol %d %.1f %% | GRILLA_25 %d %.1f %% | GRILLA_50 %d %.1f %% | GRILLA_100 %d %.1f %%" % (
                vt, parte, r0["llegadas"], r0["pct_acierto20"], *[x for p in (25, 50, 100) for x in (fila["GRILLA_%d" % p]["llegadas"],
                                                                                                   fila["GRILLA_%d" % p]["pct_acierto20"])]))
    # placebo oficial sobre la grilla (MIRAR y PRUEBA noche)
    for parte in ("mirar", "prueba"):
        dias = part["noche"][parte]
        rs, vent = E.nq_rayas_y_ventanas(reg, dias, "MUROS_NQ_vol", "noche")
        for paso in (50, 100):
            g = grilla_desde(rs, futs, paso)
            t0 = time.time()
            r = J.evaluar(V, g, ventanas=vent, opciones={"desfase_min": 0}, n_azar=500, n_boot=0)
            m = r["metricas"]; a = r["placebo"]["azar"]["pct_acierto20"]
            res["oficial|GRILLA_%d|noche|%s" % (paso, parte)] = dict(llegadas=m["llegadas"], pct=m["pct_acierto20"], azar_p50=a["p50"],
                                                                     percentil=a["percentil"], p=a["p_valor"], corridas=r["placebo"]["tasa_base_corridas"],
                                                                     rayas_por_hora=m["rayas_por_hora"])
            out("OFICIAL juez20 GRILLA_%d noche %s: %d llegadas %.1f %% | azar p50 %.1f | percentil %.1f | p %.3f | corridas %.1f | rayas/h %.1f (%.0f s)" % (
                paso, parte, m["llegadas"], m["pct_acierto20"], a["p50"], a["percentil"], a["p_valor"], r["placebo"]["tasa_base_corridas"],
                m["rayas_por_hora"], time.time() - t0))
    # NDX: misma idea sobre las noches de la familia NDX (precio de la meta)
    import glob
    Vn, S = E.ndx_cargar()
    VN = E.preparar_velas(Vn)
    metas = pd.concat([pd.read_pickle(p)["meta"] for p in sorted(glob.glob(os.path.join(E.NDX, "datos", "series", "ndx_*.pkl")))], ignore_index=True)
    fn = dict(zip(metas["t"], metas["fut"]))
    for parte, ses in zip(("mirar", "prueba"), E.ndx_sesiones("noche")):
        vent = E.ventanas_noche(ses)
        base = S["DOMS_NDX_vol"]
        fila = {}
        for paso in (50, 100):
            g = grilla_desde({t: (v if v else {}) for t, v in base.items()}, fn, paso)
            r, _ = E.juzgar(VN, g, vent)
            fila["GRILLA_%d" % paso] = r
        res["NDX|noche|%s" % parte] = fila
        out("NDX noche %-6s GRILLA_50 %d %.1f %% | GRILLA_100 %d %.1f %%" % (parte, fila["GRILLA_50"]["llegadas"], fila["GRILLA_50"]["pct_acierto20"],
                                                                           fila["GRILLA_100"]["llegadas"], fila["GRILLA_100"]["pct_acierto20"]))
    json.dump(res, open(os.path.join(AQUI, "datos", "grilla.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=str)
    open(os.path.join(AQUI, "datos", "grilla.txt"), "w", encoding="utf-8").write("\n".join(LOG) + "\n")


if __name__ == "__main__":
    main()
