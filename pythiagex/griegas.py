# -*- coding: utf-8 -*-
"""Griegas que CBOE no entrega: vanna y charm.

CBOE ya publica delta, gamma, vega, theta y rho por contrato, asi que no
hace falta despejar la volatilidad implicita ni correr Black-Scholes para
esas. Vanna y charm hay que derivarlas.

Las dos van normalizadas como las usa la industria:
  vanna -> cambio de delta por cada 1% de cambio de volatilidad
  charm -> cambio de delta por dia
Sin normalizar, charm explota cerca del vencimiento por su factor 1/T y
queda fuera de escala respecto del resto.
"""
import math

# La tasa NO se escribe a mano: la fija cli.py con la curva de letras del
# Tesoro del dia (ver tasas.py). Este valor es solo el respaldo por si el
# feed del Tesoro no contesta, y en ese caso el panel lo dice.
R = 0.0372
R_MEDIDA = False

def fijar_tasa(r):
    """Carga la tasa medida. Devuelve True si quedo una tasa real."""
    global R, R_MEDIDA
    if r and 0.0 < r < 0.25:
        R, R_MEDIDA = float(r), True
    return R_MEDIDA

def _npdf(x: float) -> float:
    return math.exp(-0.5 * x * x) / math.sqrt(2.0 * math.pi)

def vanna_charm(S: float, K: float, T: float, sigma: float):
    """Devuelve (vanna, charm) normalizadas. T en anios."""
    if T <= 0 or sigma <= 0 or S <= 0 or K <= 0:
        return 0.0, 0.0
    st = sigma * math.sqrt(T)
    d1 = (math.log(S / K) + (R + 0.5 * sigma * sigma) * T) / st
    d2 = d1 - st
    n  = _npdf(d1)
    vanna = (-n * d2 / sigma) / 100.0
    charm = (-n * (2 * R * T - d2 * st) / (2 * T * st)) / 365.0
    return vanna, charm

def gamma_bs(S: float, K: float, T: float, sigma: float) -> float:
    """Gamma de Black-Scholes. Solo se usa para reprecar la curva a otros
    niveles de precio; la gamma del strike viene de CBOE."""
    if T <= 0 or sigma <= 0:
        return 0.0
    st = sigma * math.sqrt(T)
    d1 = (math.log(S / K) + (R + 0.5 * sigma * sigma) * T) / st
    return _npdf(d1) / (S * st)
