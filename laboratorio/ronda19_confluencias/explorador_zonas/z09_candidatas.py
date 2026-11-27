# -*- coding: utf-8 -*-
"""ZONAS, paso 9: las candidatas finales con TODAS las salidas (registradas). Definiciones como funciones regla(df, delta) para
poder correr el placebo B (niveles corridos) con la misma regla."""
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import json
import numpy as np, pandas as pd
import zonas_comun as Z

BANDA = Z.GRUPOS_LADO["bandas_rev_2sd"]     # largo: vwr-2 / vwj-2 ; corto: vwr+2 / vwj+2


def banda_toque(df, delta=None):
    """extremo a +-T (T = X/8 al tick, minimo 2 ticks) de la banda 2 sd (RTH o jornada) del lado opuesto."""
    T = df["zona_T"].to_numpy(); lado = df["lado"].to_numpy(); ext = df["ext"].to_numpy()
    m = np.zeros(len(df), bool)
    for s_, noms in BANDA.items():
        for nom in noms:
            L = ext - df["d_" + nom].to_numpy() + (0 if delta is None else delta)
            pen = (L - ext) * lado
            m |= (lado == s_) & np.isfinite(pen) & (pen >= -T - 1e-9) & (pen <= T + 1e-9)
    return m


def banda_respetada(df, delta=None):
    """extremo entre X/2 antes de la banda 2 sd (RTH o jornada) del lado opuesto y la banda, sin perforarla: pen en (-X/2, 0]."""
    return Z.contacto(df, BANDA, -0.5 + 1e-6, 0.0, delta=delta) > 0


REGLAS = {
    "C1_banda_respetada+iceberg": (lambda df, d=None: banda_respetada(df, d) & (df.F2 == 1).to_numpy(),
                                   "extremo del retroceso entre X/2 antes y la banda de 2 sd del VWAP (RTH o jornada) del lado opuesto, sin perforarla "
                                   "+ F2 iceberg del lado pasivo (recarga a <= X/2 del extremo, 60 s); entra en la vuelta de la ola (hacia el VWAP); stop extremo -/+ 2 ticks"),
    "C2_banda_toque": (lambda df, d=None: banda_toque(df, d),
                       "extremo del retroceso a +-T (2 ticks) de la banda de 2 sd del VWAP (RTH o jornada) del lado opuesto; entra en la vuelta de la ola"),
    "C3_banda_respetada+iceberg+recorrido": (lambda df, d=None: banda_respetada(df, d) & (df.F2 == 1).to_numpy() & (df.dist_vwap_R >= 1.5).to_numpy(),
                                             "C1 + el VWAP RTH queda a >= 1,5 R a favor desde la entrada (recorrido para la reversion)"),
    "C4_banda_respetada+iceberg_desde_10:30": (lambda df, d=None: banda_respetada(df, d) & (df.F2 == 1).to_numpy() & (df.seg_rueda >= 3600).to_numpy(),
                                               "C1 con entradas desde las 10:30 (balance inicial formado); post-hoc: las perdedoras de C1 se juntaban 10:00-10:35"),
}

if __name__ == "__main__":
    df = Z.cargar()
    out = {}
    for k, (f, par) in REGLAS.items():
        if len(sys.argv) > 1 and k not in sys.argv[1:]: continue
        print("==", k, "|", par)
        r = Z.evaluar("Z9_" + k, "a (banda 2 sd, reversion) - candidata", par, f(df), tuple(Z.SALIDAS), nota="candidata final")
        out[k] = {s: {kk: (vv.tolist() if isinstance(vv, np.ndarray) else vv) for kk, vv in m.items()} for s, m in r.items()}
    json.dump(out, open(os.path.join(Z.AQUI, "z09_candidatas.json"), "w"), indent=1, default=str)
