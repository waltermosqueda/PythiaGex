# -*- coding: utf-8 -*-
"""Conversion de niveles de indice a precio de futuro.

Los tableros publican todo en SPX (el indice) pero se opera ES (el futuro).
Un nivel de SPX dibujado tal cual en ES queda corrido por la base.

La base se mide por paridad put-call sobre el vencimiento que coincide con
el del futuro:  forward = strike + call - put.  Si doce strikes distintos
dan casi el mismo forward, la cadena es real y la base es confiable.

NO usar SPY x 10: el ratio real no es 10 exacto.
"""
import datetime as dt
from .exposicion import parse_occ

def _mid(o):
    b, a = o.get("bid"), o.get("ask")
    if b is None or not a:
        return None
    return (b + a) / 2.0

def medir_base(crudo: dict, venc_futuro: dt.date, n=12):
    """Devuelve (base, forward, dispersion, muestras)."""
    d = crudo["data"]
    S = d["current_price"]
    porK = {}
    for o in d["options"]:
        p = parse_occ(o["option"])
        if not p:
            continue
        venc, cp, K = p
        if venc.date() != venc_futuro:
            continue
        m = _mid(o)
        if m is None:
            continue
        porK.setdefault(K, {})[cp] = m
    cands = sorted(porK.keys(), key=lambda k: abs(k - S))[:n]
    fwd = [k + porK[k]["C"] - porK[k]["P"]
           for k in cands if "C" in porK[k] and "P" in porK[k]]
    if not fwd:
        return None
    prom = sum(fwd) / len(fwd)
    return {"base": round(prom - S, 2), "forward": round(prom, 2),
            "spot": S, "muestras": len(fwd),
            "dispersion": round(max(fwd) - min(fwd), 2)}

def convertir(nivel: float, base: float) -> float:
    """Nivel de indice -> precio de futuro."""
    return round(nivel + base, 2)

def tercer_viernes(anio: int, mes: int) -> dt.date:
    d = dt.date(anio, mes, 1)
    v = [d.replace(day=x) for x in range(1, 32)
         if x <= 31 and _valido(anio, mes, x) and
         d.replace(day=x).weekday() == 4]
    return v[2]

def _valido(a, m, d):
    try:
        dt.date(a, m, d); return True
    except ValueError:
        return False
