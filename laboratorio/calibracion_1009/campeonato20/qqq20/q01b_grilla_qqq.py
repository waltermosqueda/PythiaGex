# -*- coding: utf-8 -*-
"""q01b_grilla_qqq.py — CONTROL SIN GAMMA de la familia QQQ (como C22 GRILLA_QQQ_C41 del PREREGISTRO; INFORME 3.3: "toda serie hecha con
strikes de QQQ se compara tambien contra la grilla, no solo contra su placebo", porque los corridos +-11/19/31 caen FUERA de la grilla de
strikes de QQQ, que en NQ es una raya cada ~41,4 pts). SOLO LECTURA. No entra en Holm: es un control.

GRILLA_QQQ: en la clave t, con la MISMA foto, la MISMA razon C8 y el MISMO fut que q01 (minutos 'ok' del libro QQQ de la 4.1):
  S = fut / razon; K+ = el menor strike de QQQ presente en las filas del horizonte Hoy con K > S; K- = el mayor con K <= S;
  rayas K+ x razon (G+) y K- x razon (G-). Juez con desfase_min = 0.
Salida: datos/rayas_grilla20.pkl {'series': {'GRILLA_QQQ': {...}}, 'desfase': {'GRILLA_QQQ': 0}}. Uso: python -I q01b_grilla_qqq.py"""
import ctypes
try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass
import os
import sys
import time

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import q01_rayas_qqq as Q1  # noqa: E402


def main():
    t0 = time.time()
    V = pd.read_csv(os.path.join(AQUI, "datos", "velas_snapshot.csv"), parse_dates=["t"])
    R = pd.read_pickle(os.path.join(AQUI, "datos", "rayas_qqq20.pkl"))
    M = R["meta"]
    F = Q1.cargar_fotos(V)
    gen = np.array([f["gen"].value for f in F], np.int64)
    por_gen = {}
    out = {}
    ok = M["motivo"] == "ok"
    for t, fut, rz, fg in zip(M["t"], M["fut"], M["rz"], M["foto"]):
        out[t] = {}
    for t, fut, rz in zip(M["t"][ok], M["fut"][ok], M["rz"][ok]):
        k = int(np.searchsorted(gen, t.value, side="right")) - 1
        f = F[k]
        fl = Q1.filas_hoy(f, t)
        if fl is None:
            continue
        S = fut / rz
        K = np.unique(fl["K"])
        arriba = K[K > S]; abajo = K[K <= S]
        d = {}
        if len(arriba):
            d["G+"] = (float(arriba.min() * rz), "QQQ%g" % arriba.min())
        if len(abajo):
            d["G-"] = (float(abajo.max() * rz), "QQQ%g" % abajo.max())
        out[t] = d
    pd.to_pickle({"series": {"GRILLA_QQQ": out}, "desfase": {"GRILLA_QQQ": 0}}, os.path.join(AQUI, "datos", "rayas_grilla20.pkl"))
    print("GRILLA_QQQ: %d minutos, %d con rayas (%.0f s)" % (len(out), sum(1 for v in out.values() if v), time.time() - t0))


if __name__ == "__main__":
    main()
