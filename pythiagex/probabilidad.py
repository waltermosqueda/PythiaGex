# -*- coding: utf-8 -*-
"""Probabilidad de tocar un nivel, sacada del mercado y no de un modelo.

Hasta ahora se calculaba con Black-Scholes usando la volatilidad implicita
at-the-money para TODOS los strikes. Eso ignora el skew, y el skew no es un
detalle: medido el 2026-08-31 con el skew en +5,6 puntos porcentuales, el
modelo daba 78,3 % para un strike que el mercado estaba pagando a 81,5 %.
Cinco puntos de diferencia en el nivel que decide si entras o no.

Aca se calculan CUATRO numeros y se muestran los cuatro:

  1. La probabilidad que paga el mercado.
     Bajo no arbitraje, la derivada del precio del call respecto del strike
     ES la probabilidad de terminar mas arriba, cambiada de signo:

         P(S_T > K) = -dC/dK

     Se aproxima con diferencias centradas sobre strikes adyacentes, con
     precios reales de la cadena. No hay modelo: es lo que el mercado paga.
     Para strikes debajo del precio se usa la pata de puts: P(S_T < K) = dP/dK.

  2. El delta del contrato, que la mesa usa como atajo para lo mismo.
  3. Black-Scholes con la IV del propio strike (respeta el skew).
  4. Black-Scholes con la IV at-the-money (lo que haciamos antes).

Si los cuatro coinciden, el numero es solido. Si se separan, esa separacion
es la medida de cuanto confiar, y se publica.

DE TOCAR, NO DE TERMINAR
Terminar mas alla de un nivel no es lo mismo que tocarlo. Por el principio de
reflexion de la browniana, tocar es aproximadamente el doble de terminar del
otro lado. Es una aproximacion: asume difusion sin deriva. Se aplica al
numero del mercado y se topea en 100 %.

LO QUE ESTE NUMERO NO ES
Es probabilidad RIESGO NEUTRAL, no probabilidad del mundo real. Las dos no
son la misma cosa: el precio de las opciones lleva adentro la prima de riesgo
que la gente paga por cubrirse, y por eso las probabilidades de caida salen
sistematicamente mas altas de lo que despues ocurre. Sirve para comparar
niveles entre si y para saber que esta descontando el mercado. No es un
pronostico.

Tampoco hay ningun modelo macro aca. Lo que si hay es que el precio de las
opciones YA tiene adentro lo que el mercado entero piensa del macro, del
calendario y de todo lo demas. No se le agrega una capa inventada encima.
"""
import datetime as dt
import math


def _norm(x):
    return 0.5 * (1.0 + math.erf(x / math.sqrt(2.0)))


def _mid(o):
    """Punto medio si hay dos puntas; si no, el ultimo operado."""
    b = o.get("bid") or 0.0
    a = o.get("ask") or 0.0
    if a > 0 and b >= 0:
        return (b + a) / 2.0
    return o.get("last_trade_price") or 0.0


def _bs_final(S, K, iv, T):
    """P(S_T > K) por Black-Scholes: N(d2), sin deriva."""
    if not (S > 0 and K > 0 and iv > 0 and T > 0):
        return None
    s = iv * math.sqrt(T)
    d2 = (math.log(S / K) - 0.5 * iv * iv * T) / s
    return _norm(d2)


def curva_probabilidad(por_strike, S, T):
    """Probabilidad de terminar mas alla de cada strike, por los cuatro caminos.

    por_strike: {K: {"C": contrato, "P": contrato}} de UN vencimiento.
    Devuelve {K: {...}} solo para los strikes que tienen vecinos a los dos
    lados, porque la derivada necesita los dos.
    """
    ks = sorted(k for k, v in por_strike.items() if "C" in v and "P" in v)
    if len(ks) < 3 or T <= 0:
        return {}

    atm = min(ks, key=lambda k: abs(k - S))
    iv_atm = (por_strike[atm].get("C", {}).get("iv")
              or por_strike[atm].get("P", {}).get("iv") or 0.0)

    out = {}
    for i in range(1, len(ks) - 1):
        K = ks[i]
        ka, kb = ks[i - 1], ks[i + 1]
        ancho = kb - ka
        if ancho <= 0:
            continue
        e = por_strike[K]

        # 1. lo que paga el mercado
        if K >= S:
            # pata de calls: P(arriba) = -dC/dK
            dC = (_mid(por_strike[kb]["C"]) - _mid(por_strike[ka]["C"])) / ancho
            p_mkt = -dC
        else:
            # pata de puts: P(abajo) = dP/dK, y se pasa a "mas alla del nivel"
            dP = (_mid(por_strike[kb]["P"]) - _mid(por_strike[ka]["P"])) / ancho
            p_mkt = dP
        p_mkt = max(0.0, min(1.0, p_mkt))

        # 2. el delta como atajo
        if K >= S:
            p_dl = abs(e.get("C", {}).get("delta") or 0.0)
        else:
            p_dl = abs(e.get("P", {}).get("delta") or 0.0)
        p_dl = max(0.0, min(1.0, p_dl))

        # 3 y 4. Black-Scholes con la IV del strike y con la at-the-money
        ivk = (e.get("C", {}).get("iv") or e.get("P", {}).get("iv") or 0.0)
        b_k = _bs_final(S, K, ivk, T)
        b_a = _bs_final(S, K, iv_atm, T)
        if K < S:
            # los dos devuelven P(arriba); abajo del precio interesa el complemento
            b_k = None if b_k is None else 1 - b_k
            b_a = None if b_a is None else 1 - b_a

        metodos = [x for x in (p_mkt, p_dl, b_k, b_a) if x is not None]
        disp = (max(metodos) - min(metodos)) if len(metodos) > 1 else None

        out[K] = {
            "final_mercado": round(p_mkt * 100, 1),
            "final_delta": round(p_dl * 100, 1),
            "final_bs_skew": round(b_k * 100, 1) if b_k is not None else None,
            "final_bs_atm": round(b_a * 100, 1) if b_a is not None else None,
            # tocar es aproximadamente el doble de terminar del otro lado
            "toque": round(min(1.0, 2.0 * p_mkt) * 100, 1),
            "dispersion_pp": round(disp * 100, 1) if disp is not None else None,
            "iv_strike": round(ivk, 4) if ivk else None,
        }
    return out


def interpolar(curva, K):
    """Probabilidad en un strike que puede no estar en la curva."""
    if not curva:
        return None
    if K in curva:
        return curva[K]
    ks = sorted(curva)
    if K <= ks[0] or K >= ks[-1]:
        return curva[min(ks, key=lambda k: abs(k - K))]
    for a, b in zip(ks, ks[1:]):
        if a <= K <= b:
            t = (K - a) / (b - a)
            ca, cb = curva[a], curva[b]
            def mez(k):
                x, y = ca.get(k), cb.get(k)
                if x is None or y is None:
                    return x if y is None else y
                return round(x + t * (y - x), 1)
            return {k: mez(k) for k in ca}
    return None


def confianza(p):
    """Que tan de acuerdo estan los cuatro metodos, en una palabra."""
    if not p or p.get("dispersion_pp") is None:
        return "sin control"
    d = p["dispersion_pp"]
    if d <= 3:
        return "firme"
    if d <= 8:
        return "razonable"
    return "floja"
