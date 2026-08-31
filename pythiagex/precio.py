# -*- coding: utf-8 -*-
"""Serie de precio intradia y cotizacion.

CBOE publica dos endpoints ademas de la cadena:

  charts/intraday/{sym}.json -> velas de 1 minuto con OHLC de la sesion,
                                y ademas volumen de calls y de puts por minuto
  quotes/{sym}.json          -> cotizacion liviana con bid, ask y ultimo

Los dos llevan el mismo retraso de 15 minutos que la cadena. Sirven para
dibujar el mapa, no para disparar la ejecucion.
"""
import gzip, json, os, urllib.request, datetime as dt

BASE = "https://cdn.cboe.com/api/global/delayed_quotes"
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/120.0 Safari/537.36")

def _get(url):
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    with urllib.request.urlopen(req, timeout=25) as r:
        return json.loads(r.read())

def cotizacion(sym: str) -> dict:
    """Ultimo precio, bid, ask y rango del dia."""
    j = _get(f"{BASE}/quotes/{sym}.json")
    d = j["data"]
    return {"timestamp": j.get("timestamp"), "simbolo": d.get("symbol"),
            "precio": d.get("current_price"), "cambio": d.get("price_change"),
            "cambio_pct": d.get("price_change_percent"),
            "bid": d.get("bid"), "ask": d.get("ask"),
            "apertura": d.get("open"), "maximo": d.get("high"),
            "minimo": d.get("low"), "cierre_previo": d.get("close")}

def intradia(sym: str, base=None) -> dict:
    """Velas de 1 minuto. Si se pasa la base, agrega el precio en futuro."""
    j = _get(f"{BASE}/charts/intraday/{sym}.json")
    velas, vc, vp = [], 0, 0
    for v in j.get("data", []):
        p = v.get("price") or {}
        vol = v.get("volume") or {}
        c = p.get("close")
        if c is None:
            continue
        vc += vol.get("calls_volume") or 0
        vp += vol.get("puts_volume") or 0
        fila = {"t": v["datetime"][11:16],
                "o": p.get("open"), "h": p.get("high"),
                "l": p.get("low"), "c": c,
                "vc": vol.get("calls_volume") or 0,
                "vp": vol.get("puts_volume") or 0}
        if base is not None:
            fila["cf"] = round(c + base, 2)
        velas.append(fila)
    return {"timestamp": j.get("timestamp"), "velas": velas,
            "n": len(velas),
            "vol_calls": vc, "vol_puts": vp,
            "pc_volumen": round(vp / vc, 3) if vc else None,
            "apertura": velas[0]["o"] if velas else None,
            "maximo": max((v["h"] for v in velas if v["h"]), default=None),
            "minimo": min((v["l"] for v in velas if v["l"]), default=None),
            "ultimo": velas[-1]["c"] if velas else None}
