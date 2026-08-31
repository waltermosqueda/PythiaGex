# -*- coding: utf-8 -*-
"""Historico de corridas: habilita lookbacks y vista intradia.

Cada corrida se guarda como una linea de un archivo por dia y por simbolo
(JSON Lines). Con eso se puede:
  - superponer el estado de hace 10, 20 o 30 minutos sobre el actual
  - dibujar la evolucion de las metricas durante la jornada
  - calcular el modo "change": cuanto se movio cada strike

Es lo que Unusual Whales llama lookbacks y GEXBot lookback dots.
"""
import json, os, datetime as dt

DIR = "datos/historico"

def _ruta(sym, fecha=None):
    f = (fecha or dt.datetime.now(dt.timezone.utc).date()).isoformat()
    return os.path.join(DIR, f"{sym}-{f}.jsonl")

def guardar(sym: str, resumen: dict, strikes: dict):
    """Una linea por corrida: metricas globales + gamma por strike."""
    os.makedirs(DIR, exist_ok=True)
    fila = {
        "t": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds"),
        "spot": resumen.get("spot"),
        "gex": resumen.get("net_gex_B"),
        "dex": resumen.get("net_dex_B"),
        "vex": resumen.get("net_vex_B"),
        "chex": resumen.get("net_chex_B"),
        "flip": resumen.get("gamma_flip"),
        "em": resumen.get("expected_move"),
        # Los niveles con nombre se guardan para poder medir despues cuanto se
        # mueven entre corrida y corrida. Esa es la unica forma honesta de
        # saber si un retraso de 15 minutos cuesta plata o no cuesta nada.
        "niv": resumen.get("niveles"),
        "niv0": resumen.get("niveles_0dte"),
        # La probabilidad que se prometio en cada nivel. Sin esto no se puede
        # medir la calibracion, que es el control mas duro que hay: de todos
        # los niveles a los que les dimos 70%, cuantos se tocaron de verdad.
        "prob": resumen.get("probs"),
        "prob0": resumen.get("probs_0dte"),
        "ts_cadena": resumen.get("timestamp"),
        "k": {str(k): [round(v["gex"]/1e6), round(v["oi_call"]), round(v["oi_put"])]
              for k, v in strikes.items()},
    }
    with open(_ruta(sym), "a", encoding="utf-8") as f:
        f.write(json.dumps(fila, separators=(",", ":")) + "\n")
    return fila["t"]

def leer(sym: str, fecha=None) -> list:
    p = _ruta(sym, fecha)
    if not os.path.exists(p):
        return []
    out = []
    with open(p, encoding="utf-8") as f:
        for ln in f:
            ln = ln.strip()
            if ln:
                try: out.append(json.loads(ln))
                except Exception: pass
    return out

def lookbacks(sym: str, minutos=(10, 20, 30), fecha=None) -> dict:
    """Estado de hace N minutos, para superponer sobre el actual."""
    filas = leer(sym, fecha)
    if not filas:
        return {}
    ahora = dt.datetime.now(dt.timezone.utc)
    out = {}
    for m in minutos:
        objetivo = ahora - dt.timedelta(minutes=m)
        cand = [f for f in filas
                if dt.datetime.fromisoformat(f["t"]) <= objetivo]
        if cand:
            f = cand[-1]
            edad = (ahora - dt.datetime.fromisoformat(f["t"])).total_seconds()/60
            out[f"{m}m"] = {"t": f["t"], "edad_min": round(edad),
                            "niv": f.get("niv"), "niv0": f.get("niv0"),
                            "spot": f["spot"], "gex": f["gex"], "flip": f["flip"],
                            "k": f["k"]}
    return out

def intradia(sym: str, fecha=None) -> list:
    """Serie temporal de las metricas globales del dia."""
    return [{"t": f["t"][11:16], "spot": f["spot"], "gex": f["gex"],
             "dex": f["dex"], "vex": f["vex"], "chex": f["chex"],
             "flip": f["flip"]}
            for f in leer(sym, fecha)]

def cambio(actual: dict, previo: dict, top=None):
    """Modo 'change': cuanto se movio el GEX de cada strike."""
    if not previo or "k" not in previo:
        return []
    out = []
    for k, v in actual.items():
        p = previo["k"].get(str(k))
        if not p:
            continue
        d = round(v["gex"]/1e6) - p[0]
        if d:
            out.append({"strike": k, "delta": d, "de": p[0],
                        "a": round(v["gex"]/1e6)})
    out.sort(key=lambda z: -abs(z["delta"]))
    return out[:top] if top else out
