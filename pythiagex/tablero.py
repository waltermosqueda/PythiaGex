# -*- coding: utf-8 -*-
"""Todo lo que un nivel tiene que traer encima para poder operarlo.

Una raya sola no sirve. Un nivel utilizable trae, como minimo:

  - su nombre tecnico en ingles (el que usan las mesas)
  - el strike en el indice Y su equivalente en el futuro
  - cuanta gamma hay parada ahi, para saber si es una pared o una rayita
  - el interes abierto que la sostiene, separado en calls y puts
  - la distancia al precio, en puntos y en ticks del instrumento
  - la probabilidad de que el precio lo toque en lo que queda del dia

Ese ultimo es el que casi ningun tablero retail publica y toda mesa mira.
"""
import math

# Multiplicadores y ticks de los futuros de indice de CME.
CONTRATO = {
    "ES":  {"mult": 50.0, "tick": 0.25, "micro": "MES", "mult_micro": 5.0},
    "NQ":  {"mult": 20.0, "tick": 0.25, "micro": "MNQ", "mult_micro": 2.0},
    "RTY": {"mult": 50.0, "tick": 0.10, "micro": "M2K", "mult_micro": 5.0},
}

def _norm(x):
    """Normal acumulada, sin scipy."""
    return 0.5 * (1.0 + math.erf(x / math.sqrt(2.0)))

def prob_toque(spot, nivel, iv, T):
    """Probabilidad de TOCAR un nivel antes del vencimiento.

    No es lo mismo que terminar del otro lado. Por el principio de
    reflexion de la browniana, tocar es aproximadamente el doble de
    terminar mas alla:

        P(toque) ~= 2 * N(-d)   con  d = |nivel - spot| / (spot * iv * raiz(T))

    Es una aproximacion: asume difusion sin deriva y volatilidad constante.
    Sirve para ordenar niveles por alcanzabilidad, no para tarifar opciones.
    """
    if not (spot and nivel and iv and T) or T <= 0 or iv <= 0:
        return None
    sigma = spot * iv * math.sqrt(T)
    if sigma <= 0:
        return None
    d = abs(nivel - spot) / sigma
    return round(min(1.0, 2.0 * _norm(-d)) * 100, 1)

def contratos_cobertura(gex_dolares, precio_futuro, raiz="ES"):
    """Cuantos contratos opera la mesa por cada 1% de movimiento.

    El GEX viene en dolares de delta por 1% de movimiento del subyacente.
    Dividido por el nocional de un contrato da la cantidad de contratos que
    los dealers estan forzados a comprar o vender para quedar neutrales.

    Con gamma NEGATIVA venden cuando baja y compran cuando sube: empujan.
    Con gamma POSITIVA hacen lo contrario: frenan.

    Este es el numero que traduce "GEX -28,6 B" a algo operable:
    "la mesa tiene que vender 74 mil ES por cada 1% de caida".
    """
    c = CONTRATO.get(raiz)
    if not c or not precio_futuro or gex_dolares is None:
        return None
    nocional = precio_futuro * c["mult"]
    if nocional <= 0:
        return None
    return {"contratos": int(round(gex_dolares / nocional)),
            "micro": int(round(gex_dolares / (precio_futuro * c["mult_micro"]))),
            "nocional_contrato": round(nocional, 0)}

# Nombre tecnico como lo dice una mesa, y la traduccion.
GLOSARIO = {
    "call_wall":      ("Call Wall",
                       "el techo: arriba de ahi la mesa frena las subas"),
    "put_wall":       ("Put Wall",
                       "el piso: abajo de ahi la mesa frena las bajas"),
    "gamma_pin":      ("Gamma Pin",
                       "el iman: el strike grande mas cerca del precio"),
    "major_positive": ("Major Positive Gamma",
                       "el strike que mas amortigua de toda la cadena"),
    "major_negative": ("Major Negative Gamma",
                       "el strike que mas amplifica de toda la cadena"),
    "gamma_flip":     ("Zero Gamma / Gamma Flip",
                       "el interruptor: arriba amortigua, abajo amplifica"),
}

# Cuando dos nombres caen en el mismo strike, manda el mas util para operar:
# "Call Wall" dice donde frena, "Major Positive" solo dice que es el mas grande.
ORDEN = {"call_wall": 0, "put_wall": 1, "gamma_flip": 2,
         "gamma_pin": 3, "major_positive": 4, "major_negative": 5}

def enriquecer(niveles, strikes, spot, base=None, iv_atm=None, T=None,
               raiz="ES", flip=None, sufijo=""):
    """Convierte {nombre: strike} en niveles con todos sus numeros."""
    c = CONTRATO.get(raiz, CONTRATO["ES"])
    items = list(niveles.items())
    if flip is not None:
        items.append(("gamma_flip", flip))
    out = []
    for clave, K in items:
        if K is None:
            continue
        tec, criollo = GLOSARIO.get(clave, (clave, ""))
        s = strikes.get(K) or {}
        d = K - spot
        out.append({
            "clave": clave,
            "nombre": tec + sufijo,
            "criollo": criollo,
            "indice": round(K, 2),
            "futuro": round(K + base, 2) if base is not None else None,
            "dist_pts": round(d, 2),
            "dist_ticks": int(round(d / c["tick"])),
            "dist_pct": round(d / spot * 100, 2) if spot else None,
            "gex_M": s.get("gex"),
            "gex_0dte_M": s.get("gex_0dte"),
            "dex_M": s.get("dex"),
            "vex_M": s.get("vex"),
            "chex_M": s.get("chex"),
            "oi_call": s.get("oi_call"),
            "oi_put": s.get("oi_put"),
            "vol_call": s.get("vol_call"),
            "vol_put": s.get("vol_put"),
            "prob_toque": prob_toque(spot, K, iv_atm, T),
            "orden": ORDEN.get(clave, 9),
        })

    # Un mismo strike suele ser dos cosas a la vez: el Call Wall casi siempre
    # es tambien el Major Positive. Se muestra una sola linea, con el nombre
    # principal adelante y el otro como alias, para no dibujar dos rayas
    # encima de la misma.
    porK = {}
    for f in out:
        k = f["indice"]
        if k not in porK:
            porK[k] = f
        elif f["orden"] < porK[k]["orden"]:
            f["alias"] = porK[k]["nombre"]
            porK[k] = f
        else:
            porK[k]["alias"] = f["nombre"]
    return sorted(porK.values(), key=lambda f: -f["indice"])

def escalera(curva, spot, base=None, raiz="ES", paso=None, n=11):
    """Convexity ladder: cuanta cobertura obliga cada escalon de precio.

    Es la tabla que mira una mesa antes de operar un rango. Para cada nivel
    de precio dice cuanta gamma queda parada ahi y, sobre todo, cuantos
    contratos tiene que operar el conjunto de dealers para mantenerse
    neutral si el precio llega hasta ese punto.

    Leerla es simple: donde el numero de contratos cambia de signo, cambia
    el regimen. Donde el salto entre dos escalones es grande, hay un tramo
    en el que la cobertura acelera el movimiento en vez de frenarlo.
    """
    if not curva:
        return []
    if paso is None:
        paso = max(5.0, round(spot * 0.0015 / 5) * 5)
    obj = [spot + paso * i for i in range(-(n // 2), n // 2 + 1)]
    out, ant = [], None
    for p in obj:
        pt = min(curva, key=lambda z: abs(z[0] - p))
        gex_B = pt[1]
        pf = (p + base) if base is not None else p
        cob = contratos_cobertura(gex_B * 1e9, pf, raiz)
        out.append({
            "indice": round(p, 2),
            "futuro": round(pf, 2) if base is not None else None,
            "gex_B": round(gex_B, 3),
            "contratos": cob["contratos"] if cob else None,
            "micro": cob["micro"] if cob else None,
            "regimen": "amortigua" if gex_B > 0 else "amplifica",
            "es_spot": abs(p - spot) < paso / 2,
            "delta_contratos": (None if (ant is None or not cob)
                                else cob["contratos"] - ant),
        })
        if cob:
            ant = cob["contratos"]
    out.reverse()
    return out

def huecos(strikes, spot, base=None, ancho_min=None, radio=0.02):
    """Gamma void / air pocket: tramos sin gamma que sostenga.

    Entre dos strikes cargados puede haber una franja donde nadie tiene
    posicion. Ahi la cobertura de los dealers no frena nada y el precio
    viaja rapido. Para scalping importa tanto como una pared: es donde NO
    conviene poner un objetivo corto, y donde un stop ajustado salta.
    """
    lo, hi = spot * (1 - radio), spot * (1 + radio)
    cerca = [(k, abs(s.get("gex", 0) or 0)) for k, s in strikes.items()
             if lo <= k <= hi]
    if len(cerca) < 4:
        return []
    # El umbral va relativo al barrio: un strike "cargado" es el que pesa mas
    # que la mediana de los que lo rodean. Un corte fijo en millones no sirve
    # porque la densidad cambia de simbolo en simbolo y de dia en dia.
    ms = sorted(g for _, g in cerca)
    mediana = ms[len(ms) // 2]
    cargados = sorted(k for k, g in cerca if g >= max(mediana, 1))
    if len(cargados) < 2:
        return []
    if ancho_min is None:
        ancho_min = max(10.0, spot * 0.0025)
    out = []
    for a, b in zip(cargados, cargados[1:]):
        if b - a >= ancho_min:
            out.append({
                "desde": a, "hasta": b,
                "desde_fut": round(a + base, 2) if base is not None else None,
                "hasta_fut": round(b + base, 2) if base is not None else None,
                "ancho": round(b - a, 2),
                "sobre_spot": a >= spot,
            })
    out.sort(key=lambda z: -z["ancho"])
    return out[:3]

def sesion(velas, base=None):
    """Referencias de la sesion que todo scalper marca: apertura, maximo,
    minimo e Initial Balance (el rango de la primera hora, que despues actua
    como soporte y resistencia el resto del dia)."""
    if not velas:
        return None
    cs = [v for v in velas if v.get("c") is not None]
    if not cs:
        return None
    ib = cs[:60]

    def f(x):
        return round(x + base, 2) if (base is not None and x is not None) else None

    hs = [v["h"] for v in cs if v.get("h") is not None]
    ls = [v["l"] for v in cs if v.get("l") is not None]
    ibh = [v["h"] for v in ib if v.get("h") is not None]
    ibl = [v["l"] for v in ib if v.get("l") is not None]
    if not (hs and ls and ibh and ibl):
        return None
    o, hi, lo = cs[0]["o"], max(hs), min(ls)
    a, b = max(ibh), min(ibl)
    return {
        "apertura": o, "apertura_fut": f(o),
        "maximo": hi, "maximo_fut": f(hi),
        "minimo": lo, "minimo_fut": f(lo),
        "ib_alto": a, "ib_alto_fut": f(a),
        "ib_bajo": b, "ib_bajo_fut": f(b),
        "rango": round(hi - lo, 2),
        "ib_rango": round(a - b, 2),
        "cierre": cs[-1]["c"], "cierre_fut": f(cs[-1]["c"]),
    }
