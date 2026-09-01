# -*- coding: utf-8 -*-
import math
"""Archivo liviano para el indicador de ATAS.

El JSON del panel pesa 120 KB. Un indicador que lo baja cada 30 segundos con
el mercado abierto seria un desperdicio, y ATAS lo tiene que parsear en el
hilo de dibujo. Esto arma un extracto de 2 o 3 KB con lo unico que el
grafico necesita, ya convertido a precio de futuro.

Todo lo que sale aca ya paso el control de la base. Si la base no es
confiable el archivo igual se escribe, pero con base_confiable en false: el
indicador dibuja los niveles del indice y avisa, no inventa precios de
futuro.
"""

# Que dibujar y con que prioridad. El orden importa: si dos niveles caen en
# el mismo precio, gana el de arriba.
TIPOS = ("call_wall", "put_wall", "gamma_flip", "gamma_pin",
         "major_positive", "major_negative")

def _nivel(f, base, es0=False):
    fut = f.get("futuro")
    if fut is None and base is not None and f.get("indice") is not None:
        fut = round(f["indice"] + base, 2)
    return {
        "tipo": f.get("clave"),
        "nombre": f.get("nombre"),
        "criollo": f.get("criollo"),
        "idx": f.get("indice"),
        "fut": fut,
        "gex_M": (f.get("gex_0dte_M") if es0 else f.get("gex_M")),
        "dex_M": f.get("dex_M"),
        "vex_M": f.get("vex_M"),
        "chex_M": f.get("chex_M"),
        "dias_gamma": f.get("dias_gamma"),
        "oi_c": f.get("oi_call"),
        "oi_p": f.get("oi_put"),
        "toque": f.get("prob_toque"),
        # con esto el indicador recalcula la probabilidad en vivo contra el
        # precio de ahora, en vez de mostrar la del momento de la publicacion
        "iv": f.get("iv"),
        # cociente mercado / modelo, para que el recalculo en vivo mantenga
        # el nivel que paga el mercado y no solo la forma del modelo
        "prob_factor": f.get("prob_factor"),
        # los cuatro caminos y su dispersion, para poder auditar el numero
        "prob": f.get("prob"),
        "es0dte": bool(es0),
        "alias": f.get("alias"),
        # si el segundo strike pesa casi lo mismo, la pared es una zona
        "competencia": f.get("competencia"),
    }

def _griegas(out, base):
    """Las griegas agregadas y los dos flujos que se pueden operar.

    GEX, DEX, VEX, CHEX, TEX y vega ya vienen calculados por strike y sumados.
    Lo que se agrega aca son las dos traducciones que una mesa mira:

    charm pendiente
        CHEX esta en dolares de delta POR DIA. Al vencimiento mas cercano le
        faltan `dias` dias. Entonces quedan CHEX * dias dolares de delta que
        la mesa va a tener que cubrir si el precio no se mueve, solo por el
        paso del tiempo. Es el motor del arrastre de la tarde en dias de 0DTE.

    vanna por punto de volatilidad
        VEX esta en dolares de delta por cada 1 % de cambio de la volatilidad
        implicita. Si el VIX se mueve un punto, esos son los dolares de delta
        que hay que cubrir sin que el precio se haya movido.

    Los dos se convierten a contratos dividiendo por el nocional, igual que
    la cobertura de gamma. Si falta el precio del futuro o la base, se
    devuelven en dolares y el campo de contratos queda en None: no se inventa.
    """
    T = out.get("totales") or {}
    fu = out.get("futuro") or {}
    cd = out.get("contrato_detalle") or {}
    pf = fu.get("spot")
    mult = cd.get("mult")
    dias = out.get("dias_al_vencimiento_cercano")

    def contratos(dolares):
        if dolares is None or not pf or not mult:
            return None
        noc = pf * mult
        return int(round(dolares / noc)) if noc else None

    chex_d = (T.get("net_chex_B") or 0) * 1e9
    vex_d = (T.get("net_vex_B") or 0) * 1e9
    charm_pend_d = chex_d * dias if dias is not None else None

    sk = out.get("skew") or {}
    tm = out.get("term") or {}

    return {
        "gex_B": T.get("net_gex_B"),
        "gex_volumen_B": T.get("net_gex_vol_B"),
        "dex_B": T.get("net_dex_B"),
        "vex_B": T.get("net_vex_B"),
        "chex_B": T.get("net_chex_B"),
        "tex_M": T.get("net_tex_M"),
        "vega_M": T.get("net_vega_M"),
        "put_call_oi": T.get("put_call_oi"),
        "oi_call": T.get("oi_call"),
        "oi_put": T.get("oi_put"),
        "volumen": T.get("volumen"),
        # flujos derivados
        "dias_al_vencimiento": dias,
        "charm_pendiente_B": round(charm_pend_d / 1e9, 3) if charm_pend_d is not None else None,
        "charm_pendiente_contratos": contratos(charm_pend_d),
        "vanna_contratos_por_punto_iv": contratos(vex_d),
        "dex_contratos": contratos((T.get("net_dex_B") or 0) * 1e9),
        # forma de la superficie
        "skew_pp": sk.get("pendiente_pp"),
        "skew_lectura": sk.get("lectura"),
        "skew_vencimiento": sk.get("vencimiento"),
        "term_forma": tm.get("forma"),
        "term_lectura": tm.get("lectura"),
        "iv_atm": out.get("iv_atm"),
    }

def _factor_mercado(f, spot, T):
    """Cuanto se equivoca Black-Scholes contra lo que paga el mercado, en ese
    strike y en ese momento.

    El indicador necesita recalcular la probabilidad en vivo, porque el precio
    se mueve entre corrida y corrida y el tiempo se consume. Pero recalcular
    con Black-Scholes a secas devuelve un numero peor que el que ya teniamos:
    medido el 2026-08-31, en el Put Wall el modelo daba 31,6 % donde el
    mercado pagaba 42,0 %. Diez puntos de skew que el modelo no ve.

    La salida es publicar el cociente entre los dos. El indicador recalcula
    con el modelo, que le da la DINAMICA correcta contra el precio y el
    tiempo, y multiplica por este factor, que le devuelve el NIVEL que paga
    el mercado. Se topea entre 0,5 y 2 para que un strike raro no distorsione.
    """
    iv = f.get("iv")
    K = f.get("indice")
    mkt = f.get("prob_toque")
    if not (iv and K and spot and T and T > 0) or mkt is None:
        return None
    sig = iv * math.sqrt(T)
    if sig <= 0:
        return None
    d2 = (math.log(spot / K) - 0.5 * iv * iv * T) / sig
    arriba = 0.5 * (1.0 + math.erf(d2 / math.sqrt(2.0)))
    final = arriba if K >= spot else 1.0 - arriba
    bs = min(100.0, 200.0 * final)
    if bs <= 0.5:          # muy lejos: el cociente no significa nada
        return None
    return round(max(0.5, min(2.0, mkt / bs)), 4)


def _ultima_auditoria():
    """Cuantas fallas encontro la ultima auditoria, o None si no corrio.

    Se lee del registro que deja auditoria.py. Si el archivo no existe o no se
    puede leer devuelve None, y el indicador lo muestra como "sin auditar" en
    vez de como "todo bien": no es lo mismo, y confundirlos seria justamente
    la clase de silencio que este proyecto evita.
    """
    import os, json as _j, datetime as _dt
    try:
        r = os.path.join("datos", "auditoria",
                         _dt.date.today().isoformat() + ".jsonl")
        if not os.path.exists(r):
            return None
        ultima = None
        for l in open(r, encoding="utf-8"):
            l = l.strip()
            if l:
                try:
                    ultima = _j.loads(l)
                except Exception:
                    pass
        return None if ultima is None else int(ultima.get("fallas") or 0)
    except Exception:
        return None


def construir(out):
    """Recibe el diccionario completo de cli.py y devuelve el extracto."""
    bd = out.get("base_detalle") or {}
    fu = out.get("futuro") or {}
    base = out.get("base")
    T = out.get("totales") or {}

    # se completa el factor de cada nivel, ya con el spot y el plazo a mano
    _sp = out.get("prob_spot_indice") or out.get("spot")
    _Tf = (out.get("prob_dias") or 0) / 365.0
    for _lista in (out.get("niveles_ricos") or [], out.get("niveles_ricos_0dte") or [],
                   out.get("cercanos") or []):
        for _f in _lista:
            _f["prob_factor"] = _factor_mercado(_f, _sp, _Tf)

    niveles = [_nivel(f, base) for f in (out.get("niveles_ricos") or [])]

    # Los del vencimiento de hoy solo entran si caen en otro precio: si
    # coinciden con los de la cadena completa seria la misma raya dos veces.
    ya = {n["idx"] for n in niveles}
    for f in (out.get("niveles_ricos_0dte") or []):
        if f.get("clave") in ("call_wall", "put_wall", "gamma_pin") \
           and f.get("indice") not in ya:
            niveles.append(_nivel(f, base, es0=True))

    ses = out.get("sesion") or {}
    cob = out.get("cobertura") or {}

    return {
        # de donde salio y cuando
        "generado": out.get("generado"),
        "cadena_ts": out.get("timestamp"),
        "cadena_edad_min": out.get("edad_cadena_min"),
        # LA ALARMA DE LA AUDITORIA, VIAJANDO CON EL DATO.
        #
        # De poco sirve que la auditoria detecte un desvio si hay que abrir un
        # archivo para enterarse. Va en el feed y el indicador la puede mostrar
        # en el grafico: si los calculos no cuadran, se ve donde se opera.
        #
        # null = no corrio. Cero = corrio y dio bien. No es lo mismo.
        "auditoria_fallas": _ultima_auditoria(),
        "cadena_vencida": bool(out.get("cadena_vencida")),
        "cadena_muy_vencida": bool(out.get("cadena_muy_vencida")),
        "fuente": "CBOE delayed_quotes",

        # identidad: que indice y que contrato
        "indice": out.get("simbolo", "").replace("^", ""),
        "contrato": fu.get("contrato"),
        "micro": fu.get("micro"),
        "vencimiento_futuro": fu.get("vencimiento"),

        # la conversion y su control
        "base": base,
        "base_confiable": bool(bd.get("confiable")),
        "base_error_ticks": bd.get("residuo_ticks"),
        "base_vencimientos": bd.get("vencimientos_usados"),
        "tasa_corta": bd.get("tasa_corta"),
        "dividendo_implicito": bd.get("dividendo_implicito"),
        "indice_atrasado": bool(out.get("indice_atrasado")),

        # donde esta el precio segun la cadena
        "spot_indice": out.get("spot"),
        "spot_futuro": fu.get("spot"),
        "indice_publicado": out.get("indice_publicado"),

        # regimen y cuanto tiene que cubrir la mesa
        "regimen": out.get("regimen"),
        "net_gex_B": T.get("net_gex_B"),
        "gex_0dte_B": out.get("gex_0dte_B"),
        "cobertura_contratos": cob.get("contratos"),
        "cobertura_micro": cob.get("micro"),
        "expected_move": out.get("expected_move"),
        "iv_atm": out.get("iv_atm"),
        "dias_venc_cercano": out.get("dias_al_vencimiento_cercano"),

        # ---- griegas del complejo, y los flujos que se derivan de ellas ----
        # Todo esto sale de la cadena. Nada esta escrito a mano.
        "griegas": _griegas(out, base),

        "niveles": niveles,
        # los strikes cargados pegados al precio, ordenados por cercania
        "cercanos": out.get("cercanos") or [],
        "prob_vencimiento": out.get("prob_vencimiento"),
        "prob_dias": out.get("prob_dias"),
        # el instante exacto en que muere ese vencimiento, para que el tiempo
        # se consuma solo durante la rueda. Las diarias de SPX liquidan 16:00
        # ET, no a medianoche: contra medianoche se regalaria 67% de vida.
        "prob_liquida_utc": out.get("prob_liquida_utc"),
        "prob_spot_indice": out.get("prob_spot_indice"),
        "prob_iv_atm": out.get("iv_atm"),
        # techo, piso e iman de cada vencimiento cercano por separado
        "por_vencimiento": out.get("por_vencimiento") or [],
        "huecos": out.get("huecos") or [],
        "escalera": [{"idx": e.get("indice"), "fut": e.get("futuro"),
                      "gex_B": e.get("gex_B"), "contratos": e.get("contratos")}
                     for e in (out.get("escalera") or [])],
        "sesion": {k: ses.get(k) for k in
                   ("apertura", "apertura_fut", "maximo", "maximo_fut",
                    "minimo", "minimo_fut", "ib_alto", "ib_alto_fut",
                    "ib_bajo", "ib_bajo_fut")} if ses else None,
        "vencimientos": [{"fecha": v.get("fecha"), "dias": v.get("dias"),
                          "oi": v.get("oi"), "gex_M": v.get("gex")}
                         for v in (out.get("vencimientos") or [])[:6]],
    }
