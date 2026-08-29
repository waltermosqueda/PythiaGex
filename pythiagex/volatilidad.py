# -*- coding: utf-8 -*-
"""Estructura de volatilidad: skew, term structure y superficie.

Son las tres vistas que GEXBot llama "skew, term y surface" y que Unusual
Whales expone como interpolated-iv. Se arman con la IV que CBOE publica
por contrato.

  skew    -> IV por strike, a un vencimiento fijo. Mide cuanto mas caro
             esta el seguro de baja que el de suba.
  term    -> IV del dinero por vencimiento. Mide si el miedo esta en el
             corto o en el largo plazo.
  surface -> las dos cosas a la vez: strike x vencimiento.
"""
import datetime as dt
from .exposicion import parse_occ

def _recolectar(crudo, dias_max, ahora=None):
    d = crudo["data"]
    S = d["current_price"]
    ahora = ahora or dt.datetime.now(dt.timezone.utc)
    filas = []
    for o in d["options"]:
        p = parse_occ(o["option"])
        if not p:
            continue
        venc, cp, K = p
        dias = (venc - ahora).total_seconds() / 86400.0
        if dias < 0 or dias > dias_max:
            continue
        iv = o.get("iv") or 0.0
        if iv <= 0 or iv > 3:
            continue
        filas.append((venc.date().isoformat(), round(dias, 2), K, cp, iv,
                      o.get("open_interest") or 0))
    return S, filas

def skew(crudo, vencimiento=None, ancho=0.05, dias_max=60):
    """IV de calls contra IV de puts, strike por strike."""
    S, filas = _recolectar(crudo, dias_max)
    if not filas:
        return {}
    if vencimiento is None:
        vencimiento = min(filas, key=lambda z: z[1])[0]
    por = {}
    for f, dias, K, cp, iv, oi in filas:
        if f != vencimiento or abs(K - S) / S > ancho:
            continue
        e = por.setdefault(K, {"strike": K, "moneyness": round(K / S, 4)})
        e["iv_call" if cp == "C" else "iv_put"] = round(iv, 4)
    pts = sorted(por.values(), key=lambda z: z["strike"])
    bajos = [p for p in pts if p["moneyness"] < 0.98 and p.get("iv_put")]
    altos = [p for p in pts if p["moneyness"] > 1.02 and p.get("iv_call")]
    pend = None
    if bajos and altos:
        pend = round((sum(p["iv_put"] for p in bajos) / len(bajos)
                    - sum(p["iv_call"] for p in altos) / len(altos)) * 100, 2)
    if pend is None:
        lect = None
    elif pend > 0:
        lect = "los puts estan mas caros: hay demanda de proteccion"
    else:
        lect = "los calls estan mas caros: hay demanda de exposicion al alza"
    return {"vencimiento": vencimiento, "spot": S, "puntos": pts,
            "pendiente_pp": pend, "lectura": lect}

def term(crudo, dias_max=90):
    """IV del dinero por vencimiento. Ascendente es lo normal."""
    S, filas = _recolectar(crudo, dias_max)
    por = {}
    for f, dias, K, cp, iv, oi in filas:
        e = por.setdefault(f, {"fecha": f, "dias": dias, "mejor": 1e9, "iv": None})
        dd = abs(K - S)
        if dd < e["mejor"]:
            e["mejor"] = dd
            e["iv"] = round(iv, 4)
    pts = sorted((p for p in por.values() if p["iv"]), key=lambda z: z["dias"])
    for p in pts:
        p.pop("mejor", None)
    forma, lect = None, None
    if len(pts) >= 3:
        corto = sum(p["iv"] for p in pts[:2]) / 2
        largo = sum(p["iv"] for p in pts[-2:]) / 2
        forma = "contango" if largo > corto else "backwardation"
        lect = ("normal: el miedo esta repartido en el tiempo" if forma == "contango"
                else "invertida: el miedo esta en el corto plazo, suele indicar un evento cercano")
    return {"spot": S, "puntos": pts, "forma": forma, "lectura": lect}

def superficie(crudo, ancho=0.03, dias_max=45):
    """IV por strike y por vencimiento, para dibujar como mapa de calor."""
    S, filas = _recolectar(crudo, dias_max)
    cols, celdas = {}, {}
    for f, dias, K, cp, iv, oi in filas:
        if abs(K - S) / S > ancho:
            continue
        cols[f] = round(dias)
        e = celdas.setdefault(K, {})
        prev = e.get(f)
        e[f] = round(iv, 4) if prev is None else round((prev + iv) / 2, 4)
    orden = sorted(cols.items(), key=lambda z: z[1])
    return {"spot": S,
            "columnas": [{"fecha": f, "dte": d} for f, d in orden],
            "filas": [{"strike": K,
                       "celdas": [celdas[K].get(f) for f, _ in orden]}
                      for K in sorted(celdas, reverse=True)]}
