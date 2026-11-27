# -*- coding: utf-8 -*-
"""RONDA 23, paso 2: MAPA DEL ELASTICO en la mitad de DISENO (MES 5 min, 09-09-2025 a 27-02-2026). Descriptivo, punto por punto.
Cada punto de decision (cierre de vela de 10:00 a 15:00 NY, precio de un lado del VWAP) se simula con varios brackets fijados por teoria
(r23_sim.py) y con su referencia al azar con la misma ubicacion (M = 200 caminos reflejados). El 'borde' de una casilla es
real - azar (en pts por operacion y en puntos porcentuales de acierto), con error estandar agrupado por rueda.
Guarda los resultados por punto en scratchpad\\r23\\sim_<muestra>_<bracket>.parquet (los reusa el paso 3).
Uso: python -I r23_02_mapa.py [muestra=diseno]"""
import os, pickle, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r23_comun as K
import r23_sim as S

BRACKETS = {   # fijados ANTES de mirar resultados (teoria: objetivo en el valor = VWAP; stop detras del extremo de la subasta)
    "F1": {"lado": "fade", "stop": ("ext", 0.5), "obj": ("vw", 0.0)},           # hacia el VWAP, stop X/2 detras del extremo del dia
    "F3": {"lado": "fade", "stop": ("extmult", 0.5, 1.5), "obj": ("vw", 0.0)},  # idem, stop al menos 1,5 veces la distancia al VWAP
    "F5": {"lado": "fade", "stop": ("ext", 0.5), "obj": ("frac", 0.5)},         # mitad del camino al VWAP (fijo)
    "P1": {"lado": "puerta", "stop": ("vw", 0.0), "obj": ("ext", 0.5)},         # a favor de la extension: extremo + X/2, stop en el VWAP
    "P2": {"lado": "puerta", "stop": ("vwfijo", 0.5), "obj": ("ext", 1.0)},     # extremo + X, stop a mitad de camino al VWAP (fijo)
}
L = []


def W(s=""):
    print(s, flush=True); L.append(s)


def se_rueda(dias, x):
    """media y error estandar agrupado por rueda (sumas por rueda / n)."""
    df = pd.DataFrame({"d": dias, "x": x})
    g = df.groupby("d")["x"].agg(["sum", "size"])
    n = g["size"].sum(); m = g["sum"].sum() / n
    if len(g) < 2: return m, np.nan
    r = g["sum"] - m * g["size"]
    se = np.sqrt((r ** 2).sum() * len(g) / (len(g) - 1)) / n
    return m, se


def tabla(r, col, cortes, nombre):
    W("  por %s:" % nombre)
    b = pd.cut(r[col], cortes)
    for iv, g in r.groupby(b, observed=True):
        if len(g) < 30: continue
        m, se = se_rueda(g["dia"], (g["pts_real"] - g["azar_pts"]).to_numpy())
        W("    %-16s n %5d (%3d r) | acierto real %5.1f %% azar %5.1f %% (%+5.1f) | pts real %+6.2f azar %+6.2f | borde %+5.2f (ee %.2f) | "
          "W medio %5.1f pts" % (str(iv), len(g), g["dia"].nunique(), 100 * (g["pts_real"] > 0).mean(), 100 * g["azar_gana"].mean(),
                                 100 * ((g["pts_real"] > 0).mean() - g["azar_gana"].mean()), g["pts_real"].mean(), g["azar_pts"].mean(),
                                 m, se, g["W"].mean()))


def main():
    muestra = sys.argv[1] if len(sys.argv) > 1 else "diseno"
    t0 = time.time(); K.frenar_si_falta_ram(2.0)
    B = pickle.load(open(os.path.join(K.SCR, "ruedas5.pkl"), "rb")); ru = B["ruedas"]
    P = pd.read_parquet(os.path.join(K.SCR, "puntos5.parquet"))
    d = P[(P.muestra == muestra) & (P.s != 0) & (P.min_dia >= 600) & (P.min_dia <= 900)].copy()
    W("RONDA 23 - MAPA DEL ELASTICO (%s) %s | %d puntos en %d ruedas" % (muestra, time.strftime("%Y-%m-%d %H:%M"), len(d), d.dia.nunique()))
    for nom, spec in BRACKETS.items():
        K.frenar_si_falta_ram(2.0)
        f = os.path.join(K.SCR, "sim_%s_%s.parquet" % (muestra, nom))
        if os.path.exists(f):
            r = pd.read_parquet(f)
        else:
            r = S.correr(ru, d, spec, M=200); r.to_parquet(f, index=False)
        r["W"] = (r["stop"] - r["c"]).abs() + np.where(np.isfinite(r["obj"]), (r["obj"] - r["c"]).abs(), (r["c"] - r["vw"]).abs())
        r["P_formula"] = (r["stop"] - r["c"]).abs() / r["W"]
        W(""); W("=== %s %s" % (nom, spec))
        m, se = se_rueda(r["dia"], (r["pts_real"] - r["azar_pts"]).to_numpy())
        W("  TODOS: n %d | acierto real %.1f %% | azar %.1f %% | formula %.1f %% | pts real %+.3f azar %+.3f | borde %+.3f (ee %.3f) | W %.1f" % (
            len(r), 100 * (r.pts_real > 0).mean(), 100 * r.azar_gana.mean(), 100 * r.P_formula.mean(), r.pts_real.mean(), r.azar_pts.mean(), m, se, r.W.mean()))
        tabla(r, "dv", [0, 0.5, 1, 1.5, 2, 3, 4, 6, 99], "distancia al VWAP (X)")
        tabla(r, "dx", [-0.01, 0.25, 0.5, 1, 2, 4, 99], "distancia al extremo del dia (X)")
        tabla(r, "z", [0, 0.5, 1, 1.5, 2, 2.5, 99], "desvios del VWAP (z)")
        tabla(r, "min_dia", [599, 660, 750, 840, 901], "hora (cierre de la vela)")
        r["tipo_n"] = r["tipo"].map({"E": 0, "R": 1, "T": 2, "N": 3})
        tabla(r, "tipo_n", [-0.5, 0.5, 1.5, 2.5, 3.5], "estado D1 (0 E, 1 R, 2 T, 3 N)")
        tabla(r, "min_toque", [-1, 10, 30, 60, 120, 999], "minutos desde el ultimo toque del VWAP")
        tabla(r, "n_toques", [-1, 2, 5, 10, 20, 99], "toques del VWAP en el dia")
        tabla(r, "ext_lado", [-0.01, 0.0001, 0.5, 1, 2, 99], "extension del balance inicial del lado del precio (IB)")
        tabla(r, "pend", [-99, -0.5, 0, 0.5, 1, 2, 99], "pendiente del VWAP en 30 min a favor del lado (X)")
        tabla(r, "vr10", [0, 0.85, 0.95, 1.05, 1.2, 9], "cociente de varianzas de las 10 ruedas previas")
        tabla(r, "vr_hoy", [0, 0.7, 0.9, 1.1, 1.4, 9], "cociente de varianzas de HOY hasta la vela")
        tabla(r, "frac_lado", [0, 0.3, 0.5, 0.7, 0.9, 1.01], "fraccion de la rueda del lado del precio")
        tabla(r, "ret3", [-99, -1, -0.25, 0.25, 1, 2, 99], "empuje de 15 min hacia el extremo (X)")
        tabla(r, "bext", [-1, 0, 2, 6, 12, 99], "velas desde que se marco el extremo")
        tabla(r, "rrn", [0, 0.8, 1.0, 1.3, 1.6, 2.0, 99], "rango relativo D1 (rrn)")
    open(os.path.join(K.SAL, "mapa_%s.txt" % muestra), "w", encoding="utf-8").write("\n".join(L))
    W("%.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
