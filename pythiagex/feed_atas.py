# -*- coding: utf-8 -*-
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
        "oi_c": f.get("oi_call"),
        "oi_p": f.get("oi_put"),
        "toque": f.get("prob_toque"),
        "es0dte": bool(es0),
        "alias": f.get("alias"),
    }

def construir(out):
    """Recibe el diccionario completo de cli.py y devuelve el extracto."""
    bd = out.get("base_detalle") or {}
    fu = out.get("futuro") or {}
    base = out.get("base")
    T = out.get("totales") or {}

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

        "niveles": niveles,
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
