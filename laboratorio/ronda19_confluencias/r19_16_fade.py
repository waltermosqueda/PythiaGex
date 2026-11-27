# -*- coding: utf-8 -*-
"""RONDA 19, paso 16: la operacion CONTRARIA a una vuelta de la ola (familia DELTA, "fade de la vuelta falsa"), calculada DESDE LA
CINTA con el motor (no derivada de las columnas de S6). Misma entrada que la vuelta; lado = -lado de la vuelta;
objetivo = extremo del retroceso -/+ 1 tick (se llena cuando el precio pasa 1 tick mas: toca el stop de la vuelta);
stop = entrada +/- (R + 1 tick) (donde se llena el objetivo 1R de la vuelta). Columnas nuevas: S6mc_pts, S6mc_seg_salida, S6mc_motivo."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r19_02_motor as M

TK = 0.25
_DIAS = {}


def dia_obj(inst, dia):
    k = (inst, dia)
    if k not in _DIAS:
        if len(_DIAS) > 6: _DIAS.clear()
        _DIAS[k] = M.Dia(inst, dia)
    return _DIAS[k]


def agregar_fade(df, mask):
    df = df.copy()
    df["S6mc_pts"] = np.nan; df["S6mc_seg_salida"] = -1; df["S6mc_motivo"] = ""
    idx = np.nonzero(np.asarray(mask, bool))[0]
    pts = []; segs = []; mots = []
    for i in idx:
        r = df.iloc[i]
        d = dia_obj(r["inst"], r["dia"])
        s = -int(r["lado"]); E = float(r["entrada"]); R = float(r["R"]); ext = float(r["ext"])
        stop = E - s * (R + TK); obj = ext + s * TK
        ts, px, mot = M.salida(d, int(r["seg_rueda"]), s, E, stop, "obj", obj)
        pts.append((px - E) * s); segs.append(int(ts)); mots.append(mot)
    col = df.columns
    df.iloc[idx, col.get_loc("S6mc_pts")] = pts
    df.iloc[idx, col.get_loc("S6mc_seg_salida")] = segs
    df.iloc[idx, col.get_loc("S6mc_motivo")] = mots
    return df
