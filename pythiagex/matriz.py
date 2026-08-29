# -*- coding: utf-8 -*-
"""Matriz Strike x DTE.

Cada fila es un strike, cada columna un vencimiento, y la primera columna
la suma de todos. Permite ver de un vistazo si la gamma de un strike viene
del vencimiento de hoy o de uno de dentro de tres semanas -- dos cosas que
se comportan de forma completamente distinta.

Es la vista "Strike + DTE" que usa Unusual Whales.
"""
import datetime as dt
from .exposicion import parse_occ
from .griegas import vanna_charm

MULT = 100

def construir(crudo: dict, campo="gex", dias_max=30, ancho=0.02, ahora=None):
    """
    campo: 'gex' | 'dex' | 'vex' | 'chex' | 'oi' | 'volumen'
    ancho: franja de strikes alrededor del spot, en tanto por uno
    """
    d = crudo["data"]
    S = d["current_price"]
    ahora = ahora or dt.datetime.now(dt.timezone.utc)
    celdas, cols = {}, {}

    for o in d["options"]:
        p = parse_occ(o["option"])
        if not p:
            continue
        venc, cp, K = p
        dias = (venc - ahora).total_seconds() / 86400.0
        if dias < 0 or dias > dias_max:
            continue
        if abs(K - S) / S > ancho:
            continue
        oi  = o.get("open_interest") or 0
        vol = o.get("volume") or 0
        if not oi and not vol:
            continue

        sgn = 1 if cp == "C" else -1
        T   = max(dias, 0.02) / 365.0
        iv  = o.get("iv") or 0.0
        van, chm = vanna_charm(S, K, T, iv)

        if   campo == "gex":     v = (o.get("gamma") or 0) * oi * MULT * S*S * 0.01 * sgn
        elif campo == "dex":     v = (o.get("delta") or 0) * oi * MULT * S * sgn
        elif campo == "vex":     v = van * oi * MULT * S * sgn
        elif campo == "chex":    v = chm * oi * MULT * S * sgn
        elif campo == "oi":      v = oi * sgn
        elif campo == "volumen": v = vol * sgn
        else: raise ValueError("campo desconocido: " + campo)

        kd = venc.date().isoformat()
        cols[kd] = round(dias)
        celdas.setdefault(K, {}).setdefault(kd, 0.0)
        celdas[K][kd] += v

    orden = sorted(cols.items(), key=lambda z: z[1])
    esc = 1e6 if campo in ("gex", "dex", "vex", "chex") else 1
    filas = []
    for K in sorted(celdas.keys(), reverse=True):
        fila = celdas[K]
        filas.append({
            "strike": K,
            "dist": round(K - S, 1),
            "total": round(sum(fila.values()) / esc),
            "celdas": [round(fila.get(f, 0) / esc) if f in fila else None
                       for f, _ in orden],
        })
    return {
        "campo": campo, "spot": S, "unidad": "M" if esc > 1 else "contratos",
        "columnas": [{"fecha": f, "dte": d_} for f, d_ in orden],
        "horizonte": {"dias": dias_max, "vencimientos": len(orden)},
        "filas": filas,
    }

def concentracion(m: dict, top=5):
    """Los strikes donde mas concentrada esta la exposicion, y en que
    vencimiento. Responde: 'este nivel, cuando pesa?'"""
    out = []
    for f in m["filas"]:
        if not f["total"]:
            continue
        vals = [(c, m["columnas"][i]) for i, c in enumerate(f["celdas"]) if c]
        if not vals:
            continue
        dom = max(vals, key=lambda z: abs(z[0]))
        out.append({
            "strike": f["strike"], "total": f["total"],
            "vencimiento_dominante": dom[1]["fecha"],
            "dte": dom[1]["dte"],
            "aporte": dom[0],
            "concentracion_pct": round(abs(dom[0]) / abs(f["total"]) * 100)
                                 if f["total"] else None,
        })
    out.sort(key=lambda z: -abs(z["total"]))
    return out[:top]
