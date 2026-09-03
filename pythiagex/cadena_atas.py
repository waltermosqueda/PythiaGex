# -*- coding: utf-8 -*-
"""La cadena comprimida que el indicador reprecia tick a tick.

POR QUE ESTO EXISTE, Y POR QUE CAMBIA TODO EL PROYECTO

Mirando los videos de GAMMAlito medi que las barras de gamma se recalculan
varias veces por segundo: en 4,2 segundos reales la barra del strike 713 pasa
de 152 px a 110 px, bajando parejo. Parecia imposible de igualar sin comprar
un feed de opciones en tiempo real.

No lo es. Mira la formula:

    GEX(K) = Gamma(S, K, T, sigma) x OI(K) x 100 x S^2 x 0.01 x signo

El interes abierto es de AYER. Para todos, incluido GEXbot: la OCC lo
consolida de noche. Lo unico que se mueve tick a tick es S -- el precio -- y
T, que corre sola. O sea que ese perfil que late en pantalla es la MISMA
cadena vieja, repreciada contra el spot en vivo.

La propia pagina de precios de GEXbot lo dice sin querer: su paquete basico
ofrece "real-time per second GEX calculation". Calculo por segundo, sobre una
cadena cuyo OI es de ayer.

Y el precio en vivo ya lo tenemos: entra por Rithmic, en ATAS, pago.

Entonces este modulo no publica niveles ya calculados. Publica los INSUMOS
para que el indicador rehaga la cuenta con el precio de cada tick:

    por cada strike y cada vencimiento: OI de call, OI de put, IV de cada
    lado, y el plazo.

Con eso el C# corre Black-Scholes localmente y arma el perfil entero, el zero
gamma, los majors y las dominantes a la velocidad del grafico. Sin pedirle
nada a nadie.

LO QUE ESTO NO RESUELVE
El volumen por strike del dia si cambia intradia, y ese si llega con quince
minutos de atraso. Sirve para el GEX por volumen, que se publica aparte y
marcado. El GEX por interes abierto -- el principal -- no pierde nada.
"""
import datetime as dt

from .exposicion import parse_occ

# Cuanto del mapa se manda. Un 5 % de SPX son unos 380 puntos: cubre de sobra
# lo que se recorre en una sesion y deja afuera el ruido lejano que solo
# engorda el archivo que ATAS tiene que parsear en el hilo de dibujo.
ANCHO = 0.05

# Vencimientos a incluir. Mas alla de dos semanas la gamma no empuja el
# intradia y cada vencimiento extra multiplica el tamano del archivo.
DIAS_MAX = 14


def construir(crudo, ahora=None, ancho=ANCHO, dias_max=DIAS_MAX):
    """Devuelve la cadena comprimida, lista para repreciar en el indicador."""
    d = crudo["data"]
    S = d["current_price"]
    ahora = ahora or dt.datetime.now(dt.timezone.utc)

    vencs, filas = {}, {}
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
        if iv <= 0:
            continue

        kd = venc.date().isoformat()
        if kd not in vencs:
            vencs[kd] = {"f": kd, "dias": round(dias, 4)}
        e = filas.setdefault((K, kd), {"k": K, "e": kd,
                                       "oc": 0, "op": 0, "vc": 0, "vp": 0,
                                       "ic": 0.0, "ip": 0.0})
        if cp == "C":
            e["oc"] += oi; e["vc"] += vol; e["ic"] = round(iv, 4)
        else:
            e["op"] += oi; e["vp"] += vol; e["ip"] = round(iv, 4)

    orden = sorted(vencs.values(), key=lambda z: z["dias"])
    idx = {v["f"]: i for i, v in enumerate(orden)}

    # se compacta a listas posicionales: el mismo dato ocupa la mitad
    # [strike, indice_venc, oi_call, oi_put, iv_call, iv_put, vol_call, vol_put]
    datos = []
    for (K, kd), e in sorted(filas.items()):
        if not (e["oc"] or e["op"]):
            continue
        datos.append([e["k"], idx[kd], e["oc"], e["op"],
                      e["ic"], e["ip"], e["vc"], e["vp"]])

    return {
        "spot_idx": round(S, 2),
        "ts": crudo.get("timestamp"),
        "ancho": ancho,
        "campos": "strike,venc,oi_call,oi_put,iv_call,iv_put,vol_call,vol_put",
        "vencimientos": [{"f": v["f"], "dias": v["dias"]} for v in orden],
        "filas": datos,
        "n_filas": len(datos),
        "aviso": ("el interes abierto es de ayer para todo el mundo; lo que se "
                  "reprecia tick a tick es el precio, no la cadena"),
    }
