# -*- coding: utf-8 -*-
"""Dominantes: zonas donde la mesa tiene un incentivo REAL, hoy.

QUE PROBLEMA RESUELVE

Un tablero de GEX dibuja el strike con mas gamma y lo llama muro. Eso miente
por omision de tres maneras, y las tres ya estan medidas en este proyecto:

  1. La gamma mas grande del mapa suele vencer dentro de tres semanas. El
     2026-08-28 el strike 7800 tenia 4.172 M -- el mayor de todos -- con el
     69 % de esa gamma en el vencimiento del 18 de septiembre. Hoy no
     empujaba nada. Un nivel decorativo dibujado como si fuera una pared.

  2. Una pared a doscientos puntos del precio no es una pared: es un adorno.
     Si el mercado no llega, el incentivo de la mesa no se activa nunca.

  3. El interes abierto es de AYER siempre. Un strike puede estar enorme por
     inventario heredado y estar vacio de actividad hoy.

Una dominante es un strike (o una banda de strikes vecinos) que pasa los tres
filtros a la vez. El puntaje NO es una caja negra: se publica el numero final
y los tres factores que lo forman, para que se pueda discutir cual falla.

    incentivo = tamano x inmediatez x alcance

    tamano     cuanta gamma hay ahi, contra la mayor del mapa       0..1
    inmediatez que fraccion de esa gamma vence dentro del horizonte 0..1
    alcance    probabilidad de que el precio la toque hoy           0..1

Los tres son necesarios. Si uno es cero, la zona no existe hoy, por grande
que sea el numero de titular.

QUE DESCRIBE Y QUE NO

El signo de la gamma dice el CARACTER del nivel, nunca la direccion:

    gamma positiva -> la mesa vende fuerza y compra debilidad -> FRENA
    gamma negativa -> la mesa persigue el movimiento          -> ACELERA

Una dominante de freno no dice que el precio va a bajar desde ahi. Dice que,
si llega, es mas probable que se pare a que la atraviese de largo. Un
acelerador no dice hacia donde: dice que ahi el movimiento se agranda.

La convencion de signo (dealers long calls, short puts) es una asuncion de
la industria, no un dato medido. Ver docs/METODO.md.
"""
import datetime as dt
import math

from .exposicion import parse_occ
from .griegas import gamma_bs
from .probabilidad import curva_probabilidad, interpolar

MULT_INDICE = 100

# Horizonte por defecto: la gamma que vence dentro de dos dias habiles es la
# que empuja el precio de hoy. Mas alla de ahi el strike existe, pero su
# cobertura se reparte entre muchas sesiones y no se siente en el intradia.
HORIZONTE_DIAS = 2.0

# Cuanto se mira alrededor del precio. Un 3 % de SPX son unos 230 puntos:
# mucho mas de lo que se recorre en una sesion normal, asi que no deja fuera
# nada relevante y no carga el mapa de ruido lejano.
ANCHO = 0.03

# Dos strikes son la misma zona si el segundo pesa al menos esto respecto
# del lider y esta pegado. Debajo de la mitad ya es otro nivel, no la misma
# pared ancha.
FRACCION_ZONA = 0.5

# Cuantos strikes de separacion maxima admite una zona. En SPX los strikes
# van de 5 en 5 cerca del dinero: tres pasos son 15 puntos, el ancho tipico
# de una banda de cobertura.
PASOS_ZONA = 3

# Ancho maximo de una zona, en pasos de strike. Sin este tope el agrupado se
# encadena solo: cada strike se compara con el anterior, nunca con el
# principio, y una fila larga de strikes parecidos termina siendo una sola
# "zona" de cincuenta puntos. Medido el 2026-09-01: [7605-7630], veinticinco
# puntos de SPX. Eso ya no es una banda de cobertura, es media sesion.
ANCHO_MAX_ZONA = 4


def _por_strike_y_venc(crudo, ahora=None, dias_max=45, ancho=ANCHO):
    """Gamma por strike ABIERTA POR VENCIMIENTO.

    exposicion.calcular() ya suma la gamma de cada strike, pero suma TODOS
    los vencimientos en un solo numero. Para saber si un nivel pesa hoy hay
    que ver de que vencimiento viene, y eso pide abrir la suma.

    La gamma se recalcula con Black-Scholes al precio de ahora, igual que en
    exposicion.py y por el mismo motivo: CBOE la publica redondeada a cuatro
    decimales y eso mueve el agregado un 11 %.
    """
    d = crudo["data"]
    S = d["current_price"]
    ahora = ahora or dt.datetime.now(dt.timezone.utc)
    out = {}
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
        oi = o.get("open_interest") or 0
        vol = o.get("volume") or 0
        if not oi and not vol:
            continue

        iv = o.get("iv") or 0.0
        g = o.get("gamma") or 0.0
        if iv and iv > 0 and dias > 0:
            _g = gamma_bs(S, K, max(dias, 0.02) / 365.0, iv)
            if _g > 0:
                g = _g
        sgn = 1 if cp == "C" else -1
        gex = g * oi * MULT_INDICE * S * S * 0.01 * sgn

        e = out.setdefault(K, {
            "strike": K, "gex": 0.0, "gex_abs": 0.0, "gex_cerca": 0.0,
            "oi": 0, "vol": 0, "oi_call": 0, "oi_put": 0,
            "vol_call": 0, "vol_put": 0, "iv": None, "dias_gamma_num": 0.0,
            "por_venc": {},
        })
        e["gex"] += gex
        e["gex_abs"] += abs(gex)
        e["dias_gamma_num"] += abs(gex) * dias
        if dias <= HORIZONTE_DIAS:
            e["gex_cerca"] += abs(gex)
        e["oi"] += oi
        e["vol"] += vol
        e["oi_call" if cp == "C" else "oi_put"] += oi
        e["vol_call" if cp == "C" else "vol_put"] += vol
        kd = venc.date().isoformat()
        v = e["por_venc"].setdefault(kd, {"gex": 0.0, "dias": round(dias, 2)})
        v["gex"] += gex
        # La IV que se guarda es la del vencimiento mas cercano del strike:
        # es la que gobierna el movimiento de hoy.
        if iv and (e["iv"] is None or dias < 1.5):
            e["iv"] = iv
    return out, S


def _zonas(cands, paso_strike):
    """Agrupa strikes vecinos del mismo signo en una banda.

    Una pared casi nunca es un strike solo. Cuando el segundo strike pesa
    parecido y esta al lado, el precio no reacciona en una linea: reacciona
    en el ancho de los dos. Dibujar una linea donde hay una banda de quince
    puntos hace que el operador ponga el stop adentro de la zona.
    """
    if not cands:
        return []
    cands = sorted(cands, key=lambda z: z["strike"])
    zonas, actual = [], [cands[0]]
    for c in cands[1:]:
        ant = actual[-1]
        mismo_signo = (c["gex"] >= 0) == (ant["gex"] >= 0)
        pegado = abs(c["strike"] - ant["strike"]) <= paso_strike * PASOS_ZONA + 1e-9
        lider = max(abs(x["gex"]) for x in actual)
        comparable = abs(c["gex"]) >= lider * FRACCION_ZONA
        # el ancho se mide contra el PRINCIPIO de la zona, no contra el
        # vecino: comparar solo con el vecino es lo que deja encadenar sin fin
        cabe = (c["strike"] - actual[0]["strike"]) <= paso_strike * ANCHO_MAX_ZONA + 1e-9
        if mismo_signo and pegado and comparable and cabe:
            actual.append(c)
        else:
            zonas.append(actual)
            actual = [c]
    zonas.append(actual)

    out = []
    for grupo in zonas:
        nucleo = max(grupo, key=lambda z: abs(z["gex"]))
        gex_total = sum(z["gex"] for z in grupo)
        out.append({
            "strike": nucleo["strike"],
            "desde": min(z["strike"] for z in grupo),
            "hasta": max(z["strike"] for z in grupo),
            "strikes": [z["strike"] for z in grupo],
            "gex": gex_total,
            "gex_nucleo": nucleo["gex"],
            "incentivo": nucleo["incentivo"],
            "tamano": nucleo["tamano"],
            "inmediatez": nucleo["inmediatez"],
            "alcance": nucleo["alcance"],
            "frescura": nucleo["frescura"],
            "oi": sum(z["oi"] for z in grupo),
            "vol": sum(z["vol"] for z in grupo),
            "dias_gamma": nucleo["dias_gamma"],
            "vencimiento_manda": nucleo["vencimiento_manda"],
            "iv": nucleo["iv"],
        })
    return out


def _paso_strike(strikes):
    """La separacion tipica entre strikes vecinos. En SPX son 5 puntos cerca
    del dinero y 25 lejos; se toma la mediana para no quedar preso de un
    hueco."""
    ks = sorted(strikes)
    if len(ks) < 3:
        return 5.0
    difs = sorted(b - a for a, b in zip(ks, ks[1:]) if b > a)
    if not difs:
        return 5.0
    return difs[len(difs) // 2]


def calcular(crudo, base=None, ahora=None, horizonte=HORIZONTE_DIAS,
             ancho=ANCHO, minimo_incentivo=0.02):
    """El mapa de dominantes.

    base: la base indice->futuro ya medida. Si viene, cada zona sale tambien
          en precio de futuro. Si no viene, salen solo en indice y el
          consumidor tiene que avisar que no hay conversion -- nunca
          inventarla.
    """
    ahora = ahora or dt.datetime.now(dt.timezone.utc)
    porK, S = _por_strike_y_venc(crudo, ahora=ahora, ancho=ancho)
    if not porK:
        return {"error": "sin strikes en la ventana", "spot": S}

    paso = _paso_strike(porK.keys())

    # La probabilidad de tocar sale de la cadena, no de un modelo propio:
    # es la derivada del precio del call respecto del strike.
    #
    # curva_probabilidad() pide los contratos agrupados {K: {"C":.., "P":..}}
    # de UN vencimiento, no la lista plana. Pasarle la lista devuelve {} sin
    # error: la primera version de este modulo lo hacia y el alcance se caia
    # en silencio al modelo de respaldo para todos los strikes.
    #
    # El vencimiento tiene que estar VIVO. Con menos de media hora por
    # delante la probabilidad es tan sensible al ultimo tick que deja de
    # informar, y despues del cierre se va toda a 0 o a 100.
    porVenc = {}
    for o in crudo["data"]["options"]:
        pp = parse_occ(o["option"])
        if not pp:
            continue
        vc, cp_, K_ = pp
        porVenc.setdefault(vc, {}).setdefault(K_, {})[cp_] = o

    vivos = [v for v in porVenc if (v - ahora).total_seconds() > 1800]
    curva_prob, venc_prob, dias_cerca = {}, None, None
    if vivos:
        venc_prob = min(vivos)
        dias_cerca = (venc_prob - ahora).total_seconds() / 86400.0
        try:
            curva_prob = curva_probabilidad(porVenc[venc_prob], S,
                                            dias_cerca / 365.0)
        except Exception:
            curva_prob = {}
    T = max(dias_cerca or 1.0, 0.02) / 365.0

    mayor = max(abs(e["gex"]) for e in porK.values()) or 1.0

    cands = []
    for K, e in porK.items():
        if e["gex_abs"] <= 0:
            continue
        tamano = min(1.0, abs(e["gex"]) / mayor)

        # Inmediatez: la fraccion de la gamma de ESTE strike que vence dentro
        # del horizonte. Un strike gigante cuyo peso vence en tres semanas da
        # un numero chico y deja de competir, que es exactamente lo que tiene
        # que pasar.
        inmediatez = e["gex_cerca"] / e["gex_abs"] if e["gex_abs"] else 0.0

        # interpolar() devuelve el diccionario completo de los cuatro caminos,
        # no un numero. El que interesa aca es "toque": tocar el nivel, no
        # terminar del otro lado. Un strike se puede tocar diez veces en el
        # dia y cerrar lejos, y para operarlo lo que importa es el toque.
        pr = interpolar(curva_prob, K) if curva_prob else None
        toque = pr.get("toque") if isinstance(pr, dict) else None
        dispersion = pr.get("dispersion_pp") if isinstance(pr, dict) else None
        if toque is not None:
            alcance = max(0.0, min(1.0, toque / 100.0))
            fuente_alcance = "mercado"
        else:
            # Sin curva de mercado se cae a una difusion simple con la IV del
            # strike. Se marca aparte para no mezclar medido con estimado.
            iv = e["iv"] or 0.0
            if iv > 0 and T > 0:
                sd = S * iv * math.sqrt(T)
                z = abs(K - S) / sd if sd > 0 else 9.0
                alcance = min(1.0, 2.0 * 0.5 * math.erfc(z / math.sqrt(2.0)))
                fuente_alcance = "modelo"
            else:
                alcance = 0.0
                fuente_alcance = "sin dato"

        dias_gamma = (e["dias_gamma_num"] / e["gex_abs"]) if e["gex_abs"] else None
        venc_manda = None
        if e["por_venc"]:
            venc_manda = max(e["por_venc"].items(), key=lambda z: abs(z[1]["gex"]))[0]
        frescura = (e["vol"] / e["oi"]) if e["oi"] else None

        incentivo = tamano * inmediatez * alcance
        cands.append({
            "strike": K, "gex": e["gex"],
            "tamano": round(tamano, 4),
            "inmediatez": round(inmediatez, 4),
            "alcance": round(alcance, 4),
            "alcance_fuente": fuente_alcance,
            "alcance_dispersion_pp": dispersion,
            "frescura": round(frescura, 3) if frescura is not None else None,
            "incentivo": round(incentivo, 5),
            "oi": e["oi"], "vol": e["vol"],
            "oi_call": e["oi_call"], "oi_put": e["oi_put"],
            "dias_gamma": round(dias_gamma, 2) if dias_gamma is not None else None,
            "vencimiento_manda": venc_manda,
            "iv": round(e["iv"], 4) if e["iv"] else None,
        })

    # LAS ZONAS SE ARMAN CON TODOS LOS STRIKES, NO SOLO CON LOS QUE PASAN EL
    # UMBRAL.
    #
    # La primera version filtraba antes de agrupar y despues elegia. En la
    # prueba del 2026-09-01 eso devolvio CERO zonas de freno: los strikes de
    # gamma positiva de ese dia estaban todos arriba y lejos, con alcance
    # bajo, asi que ninguno llegaba al umbral y el mapa quedaba sin techo.
    #
    # Un techo debil sigue siendo el techo. Esconderlo es peor que mostrarlo
    # flojo: el operador necesita saber donde esta aunque hoy no pese. Ahora
    # se agrupa todo, se marca cual es relevante, y la eleccion de las cuatro
    # zonas cardinales nunca se queda vacia si hay dato.
    zonas = _zonas([c for c in cands if abs(c["gex"]) > 0], paso)
    for z in zonas:
        z["relevante"] = z["incentivo"] >= minimo_incentivo
        z["signo"] = "positiva" if z["gex"] >= 0 else "negativa"
        z["caracter"] = "freno" if z["gex"] >= 0 else "acelerador"
        z["lado"] = "arriba" if z["strike"] > S else "abajo"
        if base is not None:
            z["fut"] = round(z["strike"] + base, 2)
            z["fut_desde"] = round(z["desde"] + base, 2)
            z["fut_hasta"] = round(z["hasta"] + base, 2)
        z["dist_pts"] = round(z["strike"] - S, 1)
        # dos numeros distintos que no hay que confundir: lo que pesa la banda
        # entera y lo que pesa el strike que la ancla. Los factores del
        # puntaje son siempre los del nucleo.
        z["gex_M"] = round(z["gex"] / 1e6)
        z["gex_nucleo_M"] = round(z["gex_nucleo"] / 1e6)
        z["ancho_pts"] = round(z["hasta"] - z["desde"], 1)
        z["incentivo_100"] = round(z["incentivo"] * 100, 1)
        # La traduccion viaja CON la zona, no la arma cada consumidor. El
        # panel la pedia por su cuenta y el indicador tambien: dos copias de
        # la misma regla es exactamente como se desincronizan.
        z["criollo"] = criollo(z)

    zonas.sort(key=lambda z: -z["incentivo"])

    def elegir(caracter, lado):
        c = [z for z in zonas if z["caracter"] == caracter and z["lado"] == lado]
        return c[0] if c else None

    # Que una de las cuatro casillas venga vacia NO es un fallo del calculo:
    # es un dato del dia. El 2026-09-01 no habia ninguna zona de gamma
    # positiva debajo del precio, y eso significa que nada frenaba una caida.
    # Hay que decirlo con palabras, no devolver null y que el consumidor
    # decida si dibujar algo.
    faltan = []
    for cara, lad, txt in (
            ("freno", "arriba", "no hay techo de freno arriba: nada amortigua una suba"),
            ("freno", "abajo", "no hay piso de freno abajo: nada amortigua una caida"),
            ("acelerador", "arriba", "no hay acelerador arriba"),
            ("acelerador", "abajo", "no hay acelerador abajo")):
        if elegir(cara, lad) is None:
            faltan.append(txt)

    return {
        "spot": S,
        "base": base,
        "generado": ahora.isoformat(timespec="seconds"),
        "cadena_ts": crudo.get("timestamp"),
        "horizonte_dias": horizonte,
        "paso_strike": paso,
        "dias_venc_cercano": round(dias_cerca, 3) if dias_cerca else None,
        "alcance_desde_mercado": bool(curva_prob),
        "vencimiento_probabilidad": venc_prob.date().isoformat() if venc_prob else None,
        "faltan": faltan,
        "zonas": zonas,
        "dominante_arriba": elegir("freno", "arriba"),
        "dominante_abajo": elegir("freno", "abajo"),
        "acelerador_arriba": elegir("acelerador", "arriba"),
        "acelerador_abajo": elegir("acelerador", "abajo"),
        "perfil": sorted(cands, key=lambda z: z["strike"]),
    }


def criollo(z):
    """La traduccion que va debajo del nombre tecnico.

    El nombre de experto arriba para que se pueda buscar en cualquier lado;
    abajo lo que significa, en castellano, sin jerga. Los dos siempre.
    """
    if z is None:
        return ""
    if z["caracter"] == "freno":
        if z["lado"] == "arriba":
            return "techo: si llega, la mesa vende y le cuesta pasar"
        return "piso: si llega, la mesa compra y le cuesta romper"
    if z["lado"] == "arriba":
        return "trampolin: arriba de ahi los movimientos se agrandan"
    return "resbaladilla: abajo de ahi las caidas se agrandan"
