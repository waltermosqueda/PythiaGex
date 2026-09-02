# -*- coding: utf-8 -*-
"""BigTrades: cuando entra plata grande en las opciones, y de que lado.

DE DONDE SALE EL DATO, Y POR QUE ESTO NO EXISTIA ANTES

docs/METODO.md decia, con razon hasta hoy: "Este proyecto no puede inferir el
lado. Para eso haria falta clasificar cada transaccion por su agresor, tick a
tick, y eso requiere un feed de trades que la bolsa no publica gratis."

Es cierto que CBOE no publica la cinta de operaciones. Pero publica, por
contrato y en cada corrida:

    volume          volumen ACUMULADO del dia
    last_trade_price / last_trade_time   el ultimo precio operado y cuando
    bid / ask       las dos puntas en ese instante
    tick            'up' | 'down' | 'no_change'

Restando el volumen acumulado de dos corridas seguidas queda el volumen que
se opero ENTRE las dos. Eso es la cinta, agrupada en ventanas en vez de tick
a tick. Con una ventana de un minuto, la mayoria de los contratos operan unas
pocas veces, y el ultimo precio alcanza para decir de que lado se cruzo.

    Delta volumen  = volumen_ahora - volumen_antes
    Prima          = Delta volumen x precio x 100
    Lado           = donde se cruzo respecto de bid/ask

LO QUE ESTA APROXIMACION SI Y NO PUEDE

SI puede: decir donde entro plata grande, cuanta, en que strike, en calls o
en puts, y si el ultimo cruce fue contra la oferta o contra la demanda.

NO puede: separar una ventana en la que hubo compras Y ventas. El delta de
volumen las suma; el ultimo precio solo describe el ultimo cruce. Por eso
cada BigTrade sale con un campo `confianza`, y cuando la ventana es larga o
el contrato opero mucho, la confianza baja y se dice.

TAMPOCO puede saber si el cliente abrio o cerro. Un volumen grande sobre un
strike con poco interes abierto es posicion nueva; sobre uno con mucho
interes abierto puede ser alguien cerrando. Se publica la relacion contra el
interes abierto para que se pueda distinguir.

EL RETRASO, QUE ES EL LIMITE DE VERDAD

El CDN de CBOE sirve `delayed_quotes`. Medido el 2026-09-02 sobre catorce
corridas guardadas del 2026-09-01: el retraso es de **902 segundos exactos**,
las catorce veces, sin dispersion. Quince minutos y dos segundos.

O sea que estos BigTrades describen lo que paso hace un cuarto de hora. Sirven
para leer estructura y para estudiar la sesion despues; NO sirven como gatillo
de entrada en vivo. Cada evento sale con su hora de mercado real calculada, no
con la hora del archivo, para que nadie lo confunda.

Para el gatillo en vivo hace falta la cadena de opciones de ES que ATAS ya
recibe por Rithmic. Eso vive en el indicador, no aca.
"""
import datetime as dt

from .exposicion import parse_occ

MULT_INDICE = 100

# El retraso medido del CDN de CBOE. No es una estimacion: son 902 s en 14 de
# 14 archivos del 2026-09-01, con desviacion cero. Si algun dia cambia, esto
# se vuelve a medir con herramientas/medir_retraso.py y se corrige aca.
RETRASO_CBOE_S = 902

# Cuanta prima hace grande a una operacion. Un cuarto de millon de dolares en
# una sola ventana de un minuto sobre un solo strike no lo mueve un
# minorista. Es configurable porque el umbral util cambia con el subyacente:
# lo que es grande en RUT es ruido en SPX.
PRIMA_MINIMA = 250_000

# Y un piso de contratos, para que un contrato caro con dos lotes no entre
# solo por el precio.
CONTRATOS_MINIMOS = 25


def _precio(o):
    """El precio al que se cruzo. Se prefiere el ultimo operado; si no hay,
    el punto medio de las puntas."""
    lt = o.get("last_trade_price")
    if lt and lt > 0:
        return float(lt), "operado"
    b, a = o.get("bid") or 0.0, o.get("ask") or 0.0
    if a > 0:
        return (b + a) / 2.0, "medio"
    return 0.0, "sin precio"


def _lado(o, precio):
    """De que lado se cruzo la ultima operacion.

    Contra el ask -> alguien pago el ofrecido: comprador agresivo.
    Contra el bid -> alguien vendio al comprado: vendedor agresivo.
    En el medio  -> cruce negociado, sin agresor claro.

    Se admite un margen de un tick porque el bid/ask del archivo es el de
    AHORA y el ultimo operado puede ser de unos segundos antes.
    """
    b, a = o.get("bid") or 0.0, o.get("ask") or 0.0
    if a <= 0 or b < 0 or precio <= 0:
        return "sin puntas", 0.0
    ancho = a - b
    if ancho <= 0:
        return "sin puntas", 0.0
    # posicion relativa dentro del spread: 0 = en el bid, 1 = en el ask
    pos = (precio - b) / ancho
    if pos >= 0.65:
        return "compra agresiva", round(pos, 2)
    if pos <= 0.35:
        return "venta agresiva", round(pos, 2)
    return "en el medio", round(pos, 2)


def _efecto_gamma(cp, lado):
    """Que le hace al mapa de gamma, bajo la convencion estandar.

    Si el cliente COMPRA una opcion, la mesa queda corta de esa opcion y por
    lo tanto corta de gamma: amplifica. Si el cliente VENDE, la mesa queda
    larga: amortigua. Vale igual para calls y para puts, porque la gamma de
    los dos es positiva para el que la tiene comprada.

    Esto es lo que conecta los BigTrades con las dominantes: dicen si la zona
    se esta REFORZANDO o COMIENDO mientras la mirás. Ningun tablero de GEX
    publico puede decir eso, porque todos se apoyan en el interes abierto de
    ayer y el interes abierto no cambia hasta la noche.
    """
    if lado == "compra agresiva":
        return "amplifica", "el cliente compro: la mesa queda corta de gamma"
    if lado == "venta agresiva":
        return "amortigua", "el cliente vendio: la mesa queda larga de gamma"
    return "indefinido", "cruce sin agresor claro"


def _hora_mercado(ts_archivo, retraso=RETRASO_CBOE_S):
    """La hora de verdad. El sello del archivo es 902 s posterior al mercado."""
    if not ts_archivo:
        return None
    if isinstance(ts_archivo, str):
        t = dt.datetime.strptime(ts_archivo, "%Y-%m-%d %H:%M:%S")
        t = t.replace(tzinfo=dt.timezone.utc)
    else:
        t = ts_archivo
    return t - dt.timedelta(seconds=retraso)


def detectar(antes, ahora, base=None, prima_minima=PRIMA_MINIMA,
             contratos_minimos=CONTRATOS_MINIMOS, dias_max=10,
             retraso=RETRASO_CBOE_S):
    """Los BigTrades ocurridos entre dos corridas.

    antes, ahora: las dos cadenas crudas, tal como las publica CBOE.
    base: la base indice->futuro ya medida, para dar tambien el precio de ES.
    """
    ta = _hora_mercado(antes.get("timestamp"), retraso)
    tb = _hora_mercado(ahora.get("timestamp"), retraso)
    if ta is None or tb is None:
        return {"error": "falta el sello de tiempo de alguna corrida"}
    ventana_s = (tb - ta).total_seconds()
    if ventana_s <= 0:
        return {"error": "las dos corridas estan en el mismo instante o al reves",
                "ventana_s": ventana_s}

    prev = {o["option"]: o for o in antes["data"]["options"]}
    S = ahora["data"]["current_price"]
    eventos = []
    total_contratos = 0
    total_prima = 0.0

    for o in ahora["data"]["options"]:
        p = parse_occ(o["option"])
        if not p:
            continue
        venc, cp, K = p
        dias = (venc - tb).total_seconds() / 86400.0
        if dias < 0 or dias > dias_max:
            continue
        v0 = prev.get(o["option"])
        if v0 is None:
            continue
        dvol = (o.get("volume") or 0) - (v0.get("volume") or 0)
        if dvol <= 0:
            # El volumen acumulado no puede bajar. Si baja, la corrida
            # anterior era de otra sesion: no es un trade, es un reinicio.
            continue
        total_contratos += dvol
        precio, origen_precio = _precio(o)
        prima = dvol * precio * MULT_INDICE
        total_prima += prima
        if prima < prima_minima or dvol < contratos_minimos:
            continue

        lado, pos = _lado(o, precio)
        efecto, porque = _efecto_gamma(cp, lado)
        oi = o.get("open_interest") or 0

        # CONFIANZA DEL LADO.
        #
        # El lado sale del ULTIMO cruce, pero el delta de volumen suma toda la
        # ventana. Cuanto mas operaciones quepan en la ventana, menos
        # representa el ultimo cruce al conjunto. Se aproxima con el tamano
        # del delta: si son pocos lotes, cabe una sola operacion y el ultimo
        # precio ES la operacion; si son miles, ahi adentro hubo de todo.
        if lado in ("sin puntas", "en el medio"):
            confianza = "sin lado"
        elif dvol <= 250 and ventana_s <= 180:
            confianza = "alta"
        elif dvol <= 2000:
            confianza = "media"
        else:
            confianza = "baja"

        ev = {
            "hora": tb.isoformat(timespec="seconds"),
            "hora_desde": ta.isoformat(timespec="seconds"),
            "ventana_s": round(ventana_s),
            "contrato": o["option"],
            "strike": K,
            "tipo": "call" if cp == "C" else "put",
            "vencimiento": venc.date().isoformat(),
            "dte": round(dias, 2),
            "es_0dte": venc.date() == tb.date(),
            "contratos": int(dvol),
            "precio": round(precio, 2),
            "precio_origen": origen_precio,
            "prima": round(prima),
            "prima_M": round(prima / 1e6, 2),
            "lado": lado,
            "posicion_en_spread": pos,
            "confianza": confianza,
            "efecto_gamma": efecto,
            "efecto_porque": porque,
            "open_interest": int(oi),
            "vol_sobre_oi": round(dvol / oi, 3) if oi else None,
            "posicion_nueva": bool(oi == 0 or (oi and dvol / oi > 0.5)),
            "dist_pts": round(K - S, 1),
            "iv": round(o.get("iv") or 0, 4) or None,
            "tick": o.get("tick"),
        }
        if base is not None:
            ev["fut"] = round(K + base, 2)
        eventos.append(ev)

    eventos.sort(key=lambda z: -z["prima"])
    return {
        "desde": ta.isoformat(timespec="seconds"),
        "hasta": tb.isoformat(timespec="seconds"),
        "ventana_s": round(ventana_s),
        "retraso_s": retraso,
        "aviso_retraso": ("el dato de CBOE llega %d s tarde (medido, no estimado): "
                          "esto describe lo que paso hace %d minutos"
                          % (retraso, round(retraso / 60))),
        "spot": S,
        "base": base,
        "prima_minima": prima_minima,
        "contratos_minimos": contratos_minimos,
        "volumen_total_ventana": int(total_contratos),
        "prima_total_ventana": round(total_prima),
        "eventos": eventos,
        "n": len(eventos),
    }


def por_strike(res, top=12):
    """Los BigTrades agrupados por strike.

    Un solo trade grande es una operacion. Cinco trades en el mismo strike en
    la misma ventana es una mesa construyendo algo, y eso es lo que hay que
    mirar contra la dominante que este ahi.
    """
    if not res or "eventos" not in res:
        return []
    acc = {}
    for e in res["eventos"]:
        a = acc.setdefault(e["strike"], {
            "strike": e["strike"], "fut": e.get("fut"), "n": 0,
            "contratos": 0, "prima": 0.0,
            "prima_call": 0.0, "prima_put": 0.0,
            "prima_amplifica": 0.0, "prima_amortigua": 0.0,
            "dist_pts": e["dist_pts"], "hay_0dte": False,
        })
        a["n"] += 1
        a["contratos"] += e["contratos"]
        a["prima"] += e["prima"]
        a["prima_call" if e["tipo"] == "call" else "prima_put"] += e["prima"]
        if e["efecto_gamma"] == "amplifica":
            a["prima_amplifica"] += e["prima"]
        elif e["efecto_gamma"] == "amortigua":
            a["prima_amortigua"] += e["prima"]
        a["hay_0dte"] = a["hay_0dte"] or e["es_0dte"]
    out = list(acc.values())
    for a in out:
        neto = a["prima_amplifica"] - a["prima_amortigua"]
        a["neto_gamma"] = round(neto)
        a["lectura"] = ("se esta comiendo el nivel" if neto > 0
                        else "se esta reforzando el nivel" if neto < 0
                        else "sin sesgo")
        for k in ("prima", "prima_call", "prima_put",
                  "prima_amplifica", "prima_amortigua"):
            a[k] = round(a[k])
    out.sort(key=lambda z: -z["prima"])
    return out[:top]


def contra_dominantes(res, dom, tolerancia_pts=None):
    """Cruza los BigTrades con las zonas dominantes.

    Esta es la pregunta que el mapa solo no contesta: la pared que estoy
    mirando, ¿la estan reforzando o se la estan comiendo AHORA?

    Un nivel de gamma positiva con compras agresivas encima se esta
    debilitando: cada call que la mesa vende la deja mas corta de gamma. El
    mismo nivel con ventas agresivas se esta endureciendo.
    """
    if not res or not dom or not dom.get("zonas"):
        return []
    grupos = por_strike(res, top=200)
    if tolerancia_pts is None:
        tolerancia_pts = max(dom.get("paso_strike") or 5.0, 5.0)
    out = []
    for z in dom["zonas"]:
        dentro = [g for g in grupos
                  if z["desde"] - tolerancia_pts <= g["strike"] <= z["hasta"] + tolerancia_pts]
        if not dentro:
            continue
        prima = sum(g["prima"] for g in dentro)
        neto = sum(g["neto_gamma"] for g in dentro)
        out.append({
            "zona": z["strike"], "desde": z["desde"], "hasta": z["hasta"],
            "fut": z.get("fut"),
            "caracter": z["caracter"], "lado": z["lado"],
            "incentivo_100": z["incentivo_100"],
            "prima": round(prima), "neto_gamma": round(neto),
            "n_trades": sum(g["n"] for g in dentro),
            "lectura": _lectura_zona(z["caracter"], neto),
        })
    out.sort(key=lambda z: -z["prima"])
    return out


def _lectura_zona(caracter, neto):
    if neto == 0:
        return "flujo repartido: la zona no cambia"
    if caracter == "freno":
        return ("le estan sacando el freno: entra flujo que deja a la mesa corta"
                if neto > 0 else
                "el freno se endurece: entra flujo que deja a la mesa larga")
    return ("el acelerador se carga mas" if neto > 0 else
            "el acelerador se esta desarmando")
