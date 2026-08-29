# -*- coding: utf-8 -*-
"""Actividad del dia: que contratos se estan moviendo.

CBOE no publica el lado agresor de cada transaccion, asi que no se puede
clasificar compra contra venta. Lo que si se puede es medir DONDE hubo
actividad inusual, que es la mitad util del asunto.

El indicador central es volumen sobre interes abierto: si en un strike se
opero mas de lo que habia vivo, ahi entro posicion nueva.
"""
import datetime as dt
from .exposicion import parse_occ

def hottest(crudo, dias_max=30, top=20, min_vol=100):
    """Contratos con mayor relacion volumen/OI. Equivale al screener
    de 'hottest chains'."""
    d = crudo["data"]
    S = d["current_price"]
    ahora = dt.datetime.now(dt.timezone.utc)
    out = []
    for o in d["options"]:
        p = parse_occ(o["option"])
        if not p:
            continue
        venc, cp, K = p
        dias = (venc - ahora).total_seconds() / 86400.0
        if dias < 0 or dias > dias_max:
            continue
        vol = o.get("volume") or 0
        oi = o.get("open_interest") or 0
        if vol < min_vol:
            continue
        # con OI cero todo el volumen es posicion nueva; se ordena por volumen
        ratio = (vol / oi) if oi else None
        out.append({"contrato": o["option"], "strike": K, "tipo": cp,
                    "vencimiento": venc.date().isoformat(), "dte": round(dias),
                    "volumen": vol, "open_interest": oi,
                    "vol_oi": round(ratio, 2) if ratio else None,
                    "sin_oi_previo": oi == 0,
                    "posicion_nueva": bool(oi == 0 or (ratio and ratio > 1)),
                    "dist": round(K - S, 1),
                    "iv": round(o.get("iv") or 0, 4)})
    # los de OI cero se ordenan entre si por volumen, despues de los que tienen ratio alto
    out = [x for x in out if x["sin_oi_previo"] or (x["vol_oi"] and x["vol_oi"] >= 0.05)]
    out.sort(key=lambda z: -(z["vol_oi"] if z["vol_oi"] is not None else 0.5), )
    return out[:top]

def actividad_por_strike(crudo, ancho=0.03, dias_max=30):
    """Volumen y OI por strike. Muestra donde se construyo hoy."""
    d = crudo["data"]
    S = d["current_price"]
    ahora = dt.datetime.now(dt.timezone.utc)
    acc = {}
    for o in d["options"]:
        p = parse_occ(o["option"])
        if not p:
            continue
        venc, cp, K = p
        dias = (venc - ahora).total_seconds() / 86400.0
        if dias < 0 or dias > dias_max or abs(K - S) / S > ancho:
            continue
        e = acc.setdefault(K, {"strike": K, "vol_call": 0, "vol_put": 0,
                               "oi_call": 0, "oi_put": 0})
        e["vol_call" if cp == "C" else "vol_put"] += o.get("volume") or 0
        e["oi_call" if cp == "C" else "oi_put"] += o.get("open_interest") or 0
    out = []
    for K, e in sorted(acc.items()):
        vol = e["vol_call"] + e["vol_put"]
        oi = e["oi_call"] + e["oi_put"]
        out.append(dict(e, dist=round(K - S, 1), volumen=vol, oi=oi,
                        vol_oi=round(vol / oi, 2) if oi else None,
                        pc_vol=round(e["vol_put"] / e["vol_call"], 2)
                               if e["vol_call"] else None))
    return out

def resumen_actividad(acts, min_vol=500):
    """Strikes donde mas posicion nueva entro hoy."""
    con = [a for a in acts
           if a["vol_oi"] and a["vol_oi"] > 1 and a["volumen"] > min_vol]
    con.sort(key=lambda z: -z["vol_oi"])
    return con[:8]
