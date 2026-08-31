# -*- coding: utf-8 -*-
"""Derivacion de niveles a partir de las exposiciones.

Los nombres siguen la convencion de la industria (Call Wall, Gamma Flip,
Major Positive/Negative) para que sean reconocibles en cualquier tablero.
"""
import math
from .griegas import gamma_bs

def curva_gamma(curva_src, spot, rango=0.06, pasos=200):
    """Repreciando la gamma a cada nivel de precio. Devuelve [(precio, GEX_B)]."""
    if not curva_src:
        return [], None
    lo, hi = spot * (1 - rango), spot * (1 + rango)
    paso = (hi - lo) / pasos
    pts, flip, ant = [], None, None
    x = lo
    while x <= hi:
        t = 0.0
        for K, cp, oi, iv, T in curva_src:
            t += (gamma_bs(x, K, T, iv) * oi * 100 * x * x * 0.01
                  * (1 if cp == "C" else -1))
        pts.append([round(x, 2), round(t / 1e9, 4)])
        if ant is not None and ((ant < 0 <= t) or (ant > 0 >= t)) and flip is None:
            # El cruce por cero casi nunca cae justo en un punto de la grilla.
            # Con 200 pasos sobre +/-6% cada paso mide unos 4,6 puntos de SPX:
            # devolver el punto de la grilla erraba hasta 18 ticks de ES.
            # Se interpola linealmente entre los dos puntos que lo encierran.
            if t != ant:
                flip = round(x - paso + paso * (-ant) / (t - ant), 2)
            else:
                flip = round(x, 2)
        ant = t
        x += paso
    return pts, flip

def expected_move(curva_src, spot):
    """1-sigma con la IV del strike mas cercano al dinero."""
    if not curva_src:
        return None
    K, cp, oi, iv, T = min(curva_src, key=lambda z: abs(z[0] - spot))
    return round(spot * iv * math.sqrt(T), 1)

def niveles_clave(strikes: dict, spot: float, campo="gex") -> dict:
    """Call Wall, Put Wall, Major Positive/Negative y Gamma Pin."""
    vals = [(k, s[campo]) for k, s in strikes.items()]
    if not vals:
        return {}
    arriba = [v for v in vals if v[0] > spot]
    abajo  = [v for v in vals if v[0] < spot]
    pos = max(vals, key=lambda z: z[1])
    neg = min(vals, key=lambda z: z[1])
    cerca = sorted((v for v in vals if abs(v[1]) > 0),
                   key=lambda z: abs(z[0] - spot))
    return {
        "call_wall":      max(arriba, key=lambda z: z[1])[0] if arriba else None,
        "put_wall":       min(abajo,  key=lambda z: z[1])[0] if abajo  else None,
        "major_positive": pos[0],
        "major_negative": neg[0],
        "gamma_pin":      cerca[0][0] if cerca else None,
    }

def skew_0dte(strikes: dict, spot: float, ancho=0.02):
    """IV de calls contra IV de puts alrededor del dinero.
    Un skew muy inclinado hacia los puts indica demanda de proteccion."""
    out = []
    for k, s in sorted(strikes.items()):
        if abs(k - spot) / spot > ancho:
            continue
        if s["iv_call"] or s["iv_put"]:
            out.append({"strike": k,
                        "iv_call": round(s["iv_call"], 4) if s["iv_call"] else None,
                        "iv_put":  round(s["iv_put"], 4)  if s["iv_put"]  else None})
    return out

def cambio_vs(anterior: dict, actual: dict, top=6):
    """Max change strikes: donde mas se movio el GEX entre dos corridas.
    Es el equivalente al 'max change' de GEXBot, que ellos calculan a
    1, 5, 15 y 30 minutos."""
    if not anterior:
        return []
    difs = []
    for k, s in actual.items():
        prev = anterior.get(k)
        if not prev:
            continue
        d = s["gex"] - prev["gex"]
        if abs(d) > 1e6:
            difs.append({"strike": k, "delta_gex": round(d / 1e6),
                         "de": round(prev["gex"] / 1e6),
                         "a": round(s["gex"] / 1e6)})
    difs.sort(key=lambda z: -abs(z["delta_gex"]))
    return difs[:top]

def alertas(spot: float, niv: dict, tol=3.0):
    """Aviso cuando el spot esta pegado a un nivel mayor."""
    out = []
    for nombre, k in niv.items():
        if k and abs(spot - k) <= tol:
            out.append({"nivel": nombre, "strike": k,
                        "dist": round(spot - k, 2)})
    return out
