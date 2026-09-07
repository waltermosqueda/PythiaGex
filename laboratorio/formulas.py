# -*- coding: utf-8 -*-
"""FORMULAS CANDIDATAS DE DOMINANTES, Y COMO SE PUNTUAN.

CADA FORMULA DEVUELVE UNA LISTA DE PRECIOS. Nada mas. Despues se puntuan
todas con la misma vara y contra el mismo placebo.

LA VARA
Dos preguntas, y las dos hacen falta:

  1. ES UN NIVEL O ES UN ECO?  Se mide cuanto se mueve el nivel por cada
     punto que se mueve el precio. Si da cerca de 1, el nivel esta siguiendo
     al precio y no puede avisar nada por adelantado.

  2. LO RESPETO EL PRECIO?  Se busca un TOQUE en las velas -- que el maximo o
     el minimo de una vela llegue al nivel viniendo de lejos -- y despues se
     mira si el precio se alejo o lo atraveso.

EL PLACEBO NO ES OPCIONAL
El precio va y vuelve solo. Cualquier linea parece respetada. Por eso cada
formula se compara contra la MISMA formula corrida unos puntos: misma zona,
mismo momento, sin informacion. Si no le gana al placebo, no informa.
"""
import math
import statistics as st

TASA = 0.0375


# ----------------------------------------------------------------------
# utilidades
# ----------------------------------------------------------------------
def por_strike(filas, peso):
    """Agrupa por strike sumando calls positivas y puts negativas."""
    d = {}
    for f in filas:
        w = peso(f)
        if not w:
            continue
        d[f["K"]] = d.get(f["K"], 0.0) + (w if f["call"] else -w)
    return d


def por_strike_abs(filas, peso):
    """Igual pero sin signo: mide CONCENTRACION, no direccion."""
    d = {}
    for f in filas:
        w = abs(peso(f))
        if not w:
            continue
        d[f["K"]] = d.get(f["K"], 0.0) + w
    return d


def topn(d, n, piso=0.0):
    if not d:
        return []
    mx = max(abs(v) for v in d.values()) or 1.0
    xs = [(k, v) for k, v in d.items() if abs(v) >= piso * mx]
    xs.sort(key=lambda kv: -abs(kv[1]))
    return [k for k, _ in xs[:n]]


def cruce_cero(d, spot):
    """Donde la suma acumulada cambia de signo, interpolando entre strikes."""
    ks = sorted(d)
    if len(ks) < 3:
        return None
    ac, prev, pk = 0.0, None, None
    for k in ks:
        ac += d[k]
        if prev is not None and ((prev < 0 <= ac) or (prev > 0 >= ac)):
            return pk + (k - pk) * (-prev) / (ac - prev) if ac != prev else k
        prev, pk = ac, k
    return None


# ----------------------------------------------------------------------
# las formulas
# ----------------------------------------------------------------------
def _gex_oi(f):
    return f["gamma"] * f["oi"]


def _gex_vol(f):
    return f["gamma"] * f["vol"]


CANDIDATAS = {}


def registrar(nombre):
    def deco(fn):
        CANDIDATAS[nombre] = fn
        return fn
    return deco


@registrar("A base: gamma x OI")
def f_a(foto, n=6):
    return topn(por_strike(foto["filas"], _gex_oi), n, 0.15)


@registrar("B volumen de hoy")
def f_b(foto, n=6):
    return topn(por_strike(foto["filas"], _gex_vol), n, 0.15)


@registrar("C OI puro, sin gamma")
def f_c(foto, n=6):
    return topn(por_strike_abs(foto["filas"], lambda f: f["oi"]), n, 0.15)


@registrar("D volumen puro, sin gamma")
def f_d(foto, n=6):
    return topn(por_strike_abs(foto["filas"], lambda f: f["vol"]), n, 0.15)


@registrar("E concentracion |gamma x OI|")
def f_e(foto, n=6):
    return topn(por_strike_abs(foto["filas"], _gex_oi), n, 0.15)


@registrar("F lejos del precio (>15 pts)")
def f_f(foto, n=6):
    d = por_strike_abs(foto["filas"], _gex_oi)
    d = {k: v for k, v in d.items() if abs(k - foto["spot"]) > 15}
    return topn(d, n, 0.15)


@registrar("G solo 0DTE")
def f_g(foto, n=6):
    fl = [f for f in foto["filas"] if f["dias"] < 1.0]
    return topn(por_strike(fl, _gex_oi), n, 0.15)


@registrar("H sin el 0DTE")
def f_h(foto, n=6):
    fl = [f for f in foto["filas"] if f["dias"] >= 1.0]
    return topn(por_strike(fl, _gex_oi), n, 0.15)


@registrar("I muros (max y min)")
def f_i(foto, n=6):
    d = por_strike(foto["filas"], _gex_oi)
    if not d:
        return []
    ar = {k: v for k, v in d.items() if k > foto["spot"]}
    ab = {k: v for k, v in d.items() if k < foto["spot"]}
    out = []
    if ar:
        out.append(max(ar, key=ar.get))
    if ab:
        out.append(min(ab, key=ab.get))
    return out


@registrar("J zero gamma")
def f_j(foto, n=6):
    z = cruce_cero(por_strike(foto["filas"], _gex_oi), foto["spot"])
    return [z] if z else []


@registrar("K delta x OI (muro de delta)")
def f_k(foto, n=6):
    return topn(por_strike_abs(foto["filas"], lambda f: abs(f["delta"]) * f["oi"]), n, 0.15)


@registrar("L gamma x (OI + volumen)")
def f_l(foto, n=6):
    return topn(por_strike(foto["filas"], lambda f: f["gamma"] * (f["oi"] + f["vol"])), n, 0.15)


@registrar("M vega x OI")
def f_m(foto, n=6):
    return topn(por_strike_abs(foto["filas"], lambda f: f["vega"] * f["oi"]), n, 0.15)
