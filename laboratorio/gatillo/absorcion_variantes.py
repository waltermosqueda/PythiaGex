# -*- coding: utf-8 -*-
"""absorcion_variantes.py — las 12 variantes PRE-REGISTRADAS (resultados/absorcion.md, seccion 1). Cada regla devuelve (pos, lado) crudos:
todos los segundos donde la condicion se cumple; el filtro de validez y el desagrupado de 60 s los hace absorcion_base.disparos."""
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd, absorcion_base as A


def _dos(cb, ca):
    """cb = condicion de compra (+1), ca = condicion de venta (-1); si se dan las dos en el mismo segundo no dispara."""
    cb = np.asarray(cb, bool); ca = np.asarray(ca, bool); amb = cb & ca; cb = cb & ~amb; ca = ca & ~amb
    pos = np.flatnonzero(cb | ca); return pos, np.where(cb[pos], 1, -1)


def R10(s, seg): f = A.rasgos(s, seg); return _dos((f.AB10 >= 14) & (f.AB10 > f.AA10), (f.AA10 >= 14) & (f.AA10 > f.AB10))
def R30(s, seg): f = A.rasgos(s, seg); return _dos((f.AB30 >= 25) & (f.AB30 > f.AA30), (f.AA30 >= 25) & (f.AA30 > f.AB30))
def R60N(s, seg): f = A.rasgos(s, seg); n = f.AB60 - f.AA60; return _dos(n >= 34, n <= -34)


def R30_EXT(s, seg):
    f = A.rasgos(s, seg); u = seg["ultimo"]
    return _dos((f.AB30 >= 15) & (u <= f.min600 + 2.0), (f.AA30 >= 15) & (u >= f.max600 - 2.0))


def R30_MED(s, seg):
    f = A.rasgos(s, seg); m = f.pos600.between(0.35, 0.65)
    return _dos((f.AB30 >= 15) & m, (f.AA30 >= 15) & m)


def R30_OFI(s, seg): f = A.rasgos(s, seg); return _dos((f.AB30 >= 15) & (f.OP30 >= 150), (f.AA30 >= 15) & (f.OP30 <= -130))


def RATIO60(s, seg):
    f = A.rasgos(s, seg)
    return _dos((f.AB60 / (f.RB60 + 1) >= 0.035) & (f.RB60 >= 940), (f.AA60 / (f.RA60 + 1) >= 0.035) & (f.RA60 >= 940))


def FALLA(s, seg):
    f = A.rasgos(s, seg); u = seg["ultimo"]; up = u.shift(1)
    # OJO: la absorcion que falla dice CONTINUACION: se rompe el bid absorbido -> -1 ; se rompe el ask absorbido -> +1
    baja = (f.AB60 >= 24) & (u <= f.L60 - 1.0) & ~(up <= f.L60.shift(1) - 1.0)
    sube = (f.AA60 >= 24) & (u >= f.H60 + 1.0) & ~(up >= f.H60.shift(1) + 1.0)
    return _dos(sube, baja)


def ABS_CVD(s, seg):
    f = A.rasgos(s, seg)
    return _dos((f.D60 <= -584) & (f.dP60 >= -1.0) & (f.AB60 >= 24), (f.D60 >= 584) & (f.dP60 <= 1.0) & (f.AA60 >= 24))


def CVD_SOLO(s, seg):
    f = A.rasgos(s, seg)
    return _dos((f.D60 <= -584) & (f.dP60 >= -1.0), (f.D60 >= 584) & (f.dP60 <= 1.0))


def ABS_GRANDE(s, seg): f = A.rasgos(s, seg); return _dos(f.gb, f.ga)
def REPONE_FUERTE(s, seg): f = A.rasgos(s, seg); return _dos(f.rb, f.ra)


VARIANTES = [("1 R10", R10), ("2 R30", R30), ("3 R60N", R60N), ("4 R30_EXT", R30_EXT), ("5 R30_MED (control)", R30_MED), ("6 R30_OFI", R30_OFI),
             ("7 RATIO60", RATIO60), ("8 FALLA", FALLA), ("9 ABS_CVD", ABS_CVD), ("10 CVD_SOLO (control)", CVD_SOLO), ("11 ABS_GRANDE", ABS_GRANDE),
             ("12 REPONE_FUERTE", REPONE_FUERTE)]
