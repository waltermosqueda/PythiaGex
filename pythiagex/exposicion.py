# -*- coding: utf-8 -*-
"""Calculo de exposiciones por strike y por vencimiento.

Convencion de signo: se sigue la estandar de la industria, que asume que
los dealers estan LONG calls y SHORT puts. Es una asuncion, no un dato
medido -- ver docs/METODO.md. Cuando el flujo dominante se invierte
(por ejemplo un dia en que todos compran calls), el signo miente.
"""
import math, re, datetime as dt
from .griegas import vanna_charm, gamma_bs

MULT_INDICE = 100      # multiplicador de opciones de indice de CBOE: 100 USD por punto
RX = re.compile(r"^([A-Z]+)(\d{2})(\d{2})(\d{2})([CP])(\d{8})$")

import calendar as _cal

def dst_eeuu(f: dt.date) -> bool:
    """Horario de verano de EE.UU.: 2do domingo de marzo a 1er domingo de noviembre."""
    def domingo(anio, mes, cual):
        ds = [d for d in range(1, _cal.monthrange(anio, mes)[1] + 1)
              if dt.date(anio, mes, d).weekday() == 6]
        return dt.date(anio, mes, ds[cual - 1])
    return domingo(f.year, 3, 2) <= f < domingo(f.year, 11, 1)

def hora_cierre_utc(f: dt.date) -> int:
    """La hora UTC en que son las 16:00 de Nueva York ese dia.

    EL BUG QUE ESTO ARREGLA, Y QUE IBA A APARECER SOLO EN NOVIEMBRE

    Aca estaba clavado el 20. Las 16:00 ET son las 20:00 UTC en verano y las
    21:00 en invierno, porque Nueva York cambia la hora y UTC no. Del primer
    domingo de noviembre en adelante, TODOS los vencimientos habrian quedado
    una hora corridos.

    En un 0DTE con cinco horas de vida, una hora es el 20 % del tiempo que
    queda. Como la gamma y el expected move van con raiz de T, eso es un 10 %
    de error en cada numero, todos los dias, sin que nada avisara.

    Lo peor es que el proyecto YA tenia la version correcta en base.py. Habia
    dos copias de la misma regla y solo una estaba bien. Ahora hay una sola.
    """
    return 20 if dst_eeuu(f) else 21

def parse_occ(simbolo: str):
    """SPX260918C00200000 -> (vencimiento, 'C', 7200.0)

    El vencimiento se devuelve en el instante real de liquidacion: las 16:00
    de Nueva York de ese dia, convertidas a UTC segun corresponda.
    """
    m = RX.match(simbolo)
    if not m:
        return None
    _, yy, mm, dd, cp, k = m.groups()
    f = dt.date(2000 + int(yy), int(mm), int(dd))
    venc = dt.datetime(f.year, f.month, f.day, hora_cierre_utc(f), 0,
                       tzinfo=dt.timezone.utc)
    return venc, cp, int(k) / 1000.0

def calcular(crudo: dict, dias_max=None, solo_venc=None, ahora=None) -> dict:
    """
    dias_max   -> None = todo; 18 = las proximas dos semanas y media
    solo_venc  -> 'latest' | 'next' | None   (equivalente a las vistas de GEXBot)
    """
    d = crudo["data"]
    S = d["current_price"]
    ahora = ahora or dt.datetime.now(dt.timezone.utc)

    # que vencimientos entran
    fechas = sorted({parse_occ(o["option"])[0] for o in d["options"]
                     if parse_occ(o["option"])
                     and (parse_occ(o["option"])[0] - ahora).total_seconds() > 0})
    permitidas = None
    if solo_venc == "latest" and fechas:
        permitidas = {fechas[0]}
    elif solo_venc == "next" and len(fechas) > 1:
        permitidas = {fechas[1]}

    strikes, vencs, curva_src = {}, {}, []
    tot = dict(gex=0.0, gex_vol=0.0, dex=0.0, vex=0.0, chex=0.0, tex=0.0,
               vgex=0.0,
               oi=0, oi_call=0, oi_put=0, vol=0, vol_call=0, vol_put=0)

    for o in d["options"]:
        p = parse_occ(o["option"])
        if not p:
            continue
        venc, cp, K = p
        dias = (venc - ahora).total_seconds() / 86400.0
        if dias < 0:
            continue
        if permitidas is not None and venc not in permitidas:
            continue
        if dias_max is not None and dias > dias_max:
            continue

        oi  = o.get("open_interest") or 0
        vol = o.get("volume") or 0
        if not oi and not vol:
            continue

        g   = o.get("gamma") or 0.0
        dl  = o.get("delta") or 0.0
        th  = o.get("theta") or 0.0
        vg  = o.get("vega") or 0.0
        iv  = o.get("iv") or 0.0
        sgn = 1 if cp == "C" else -1
        T   = max(dias, 0.02) / 365.0
        van, chm = vanna_charm(S, K, T, iv)

        gex     = g   * oi  * MULT_INDICE * S * S * 0.01 * sgn
        gex_vol = g   * vol * MULT_INDICE * S * S * 0.01 * sgn
        dex     = dl  * oi  * MULT_INDICE * S * sgn
        vex     = van * oi  * MULT_INDICE * S * sgn
        chex    = chm * oi  * MULT_INDICE * S * sgn
        tex     = th  * oi  * MULT_INDICE
        # vega en dolares por cada punto de volatilidad implicita. CBOE
        # publica vega por 1 punto (no por 1%), asi que no lleva division.
        vgex    = vg  * oi  * MULT_INDICE

        s = strikes.setdefault(K, dict(
            strike=K, gex=0.0, gex_vol=0.0, gex_0dte=0.0, dex=0.0, vex=0.0,
            chex=0.0, tex=0.0, oi_call=0, oi_put=0, vol_call=0, vol_put=0,
            iv_call=None, iv_put=None))
        s["gex"] += gex; s["gex_vol"] += gex_vol; s["dex"] += dex
        s["vex"] += vex; s["chex"] += chex;       s["tex"] += tex
        if dias <= 1:
            s["gex_0dte"] += gex
        if cp == "C":
            s["oi_call"] += oi; s["vol_call"] += vol; s["iv_call"] = iv
        else:
            s["oi_put"]  += oi; s["vol_put"]  += vol; s["iv_put"]  = iv

        kd = venc.date().isoformat()
        v = vencs.setdefault(kd, dict(fecha=kd, dias=round(dias, 2), n=0, oi=0,
                                      vol=0, gex=0.0, dex=0.0,
                                      oi_call=0, oi_put=0))
        v["n"] += 1; v["oi"] += oi; v["vol"] += vol
        v["gex"] += gex; v["dex"] += dex
        v["oi_call" if cp == "C" else "oi_put"] += oi

        for k_, val in (("gex", gex), ("gex_vol", gex_vol), ("dex", dex),
                        ("vex", vex), ("chex", chex), ("tex", tex),
                        ("vgex", vgex)):
            tot[k_] += val
        tot["oi"] += oi; tot["vol"] += vol
        tot["oi_call" if cp == "C" else "oi_put"] += oi
        tot["vol_call" if cp == "C" else "vol_put"] += vol
        if iv > 0 and oi:
            curva_src.append((K, cp, oi, iv, T))

    return dict(simbolo=d.get("symbol", "?"),
                timestamp=crudo.get("timestamp"),
                generado=dt.datetime.now(dt.timezone.utc)
                          .isoformat(timespec="seconds"),
                spot=S, contratos_totales=len(d["options"]),
                totales=tot, strikes=strikes, vencimientos=vencs,
                curva_src=curva_src)
