# -*- coding: utf-8 -*-
"""Conversion de niveles de indice a precio de futuro.

Los tableros publican todo en SPX o NDX, que son los INDICES. Se opera ES
y NQ, que son los FUTUROS. No son el mismo numero: los separa la base.

La base se mide por paridad put-call, pero NO contra el indice: contra el
forward del vencimiento mas cercano de la misma cadena.

    forward(V) = strike + call(V) - put(V)
    base       = forward(vencimiento trimestral) - forward(vencimiento cercano)
    futuro     = indice + base

Restar el indice directamente falla fuera de horario, porque el indice
deja de cotizar 16:15 ET y las opciones no. Los dos forwards salen de la
misma cadena en el mismo instante, asi que el atraso se cancela solo.

Si doce strikes distintos dan casi el mismo forward, la cadena es real y
la base es confiable. NO usar SPY x 10: el ratio no es 10 exacto.

La base se mide todos los dias. Medido en agosto de 2026 se movio de 21,6
a 12,28 en cuatro dias.
"""
import datetime as dt
import calendar
from .exposicion import parse_occ
from .tasas import curva as _curva_tasas_raw, tasa as _interp_tasa
from .tablero import CONTRATO

_TASAS = {}
def _curva_tasas():
    if "c" not in _TASAS:
        try:
            _TASAS["c"] = _curva_tasas_raw()
        except Exception:
            _TASAS["c"] = None
    return _TASAS["c"]

def tasa_corta(dias):
    c = _curva_tasas()
    return _interp_tasa(dias, c) if c else None

# Los futuros de indices son trimestrales: marzo, junio, septiembre, diciembre.
MESES_TRIMESTRALES = (3, 6, 9, 12)
CODIGO_MES = {3: "H", 6: "M", 9: "U", 12: "Z"}

def tercer_viernes(anio: int, mes: int) -> dt.date:
    viernes = [d for d in range(1, calendar.monthrange(anio, mes)[1] + 1)
               if dt.date(anio, mes, d).weekday() == 4]
    return dt.date(anio, mes, viernes[2])

# CME corre el volumen al contrato siguiente ocho dias antes del vencimiento,
# el jueves previo al tercer viernes. Antes decia "el rollover practico es unos
# dias antes" pero el codigo devolvia el contrato hasta el dia del vencimiento:
# entre el 10 y el 18 de septiembre de 2026 habria medido la base contra un
# contrato que el mercado ya no operaba.
DIAS_ROLL = 8

def fecha_roll(anio, mes):
    return tercer_viernes(anio, mes) - dt.timedelta(days=DIAS_ROLL)

def contrato_vigente(hoy=None):
    """(vencimiento, codigo) del futuro que se esta operando de verdad."""
    hoy = hoy or dt.date.today()
    for anio in (hoy.year, hoy.year + 1):
        for mes in MESES_TRIMESTRALES:
            if hoy < fecha_roll(anio, mes):
                return tercer_viernes(anio, mes), CODIGO_MES[mes] + str(anio)[-1]
    return None, None

def _mid(o):
    b, a = o.get("bid"), o.get("ask")
    if b is None or not a:
        return None
    return (b + a) / 2.0

def _last(o):
    v = o.get("last_trade_price")
    return v if v else None

def _forward(porK, S, campo, n):
    cands = sorted(porK.keys(), key=lambda k: abs(k - S))[:n]
    fwd = []
    for k in cands:
        e = porK[k]
        c, p = e.get("C", {}).get(campo), e.get("P", {}).get(campo)
        if c is None or p is None:
            continue
        fwd.append(k + c - p)
    if len(fwd) < 3:
        return None
    return {"forward": round(sum(fwd) / len(fwd), 2),
            "muestras": len(fwd),
            "dispersion": round(max(fwd) - min(fwd), 2)}

def _porK(d, venc):
    """Strikes de un vencimiento, con call y put emparejados."""
    out = {}
    for o in d["options"]:
        p = parse_occ(o["option"])
        if not p:
            continue
        vc, cp, K = p
        if vc.date() != venc:
            continue
        out.setdefault(K, {})[cp] = {"mid": _mid(o), "last": _last(o)}
    return out

def _vencimientos(d):
    return sorted({p[0].date() for p in
                   (parse_occ(o["option"]) for o in d["options"]) if p})

def _ajuste(puntos):
    """Recta por minimos cuadrados sobre (dias, forward).

    El forward de un indice sube con el tiempo a razon de (tasa - dividendo).
    Si ocho vencimientos distintos caen sobre la MISMA recta, la paridad
    put-call se esta cumpliendo en toda la cadena y los precios son reales.
    Si no caen, la cadena tiene algo roto y ningun numero derivado sirve.

    Devuelve la ordenada al origen (el contado implicito, a cero dias),
    la pendiente, el R cuadrado y el residuo mas grande en puntos.
    """
    n = len(puntos)
    if n < 3:
        return None
    sx = sum(p[0] for p in puntos)
    sy = sum(p[1] for p in puntos)
    sxx = sum(p[0] * p[0] for p in puntos)
    sxy = sum(p[0] * p[1] for p in puntos)
    den = n * sxx - sx * sx
    if abs(den) < 1e-9:
        return None
    b = (n * sxy - sx * sy) / den
    a = (sy - b * sx) / n
    ym = sy / n
    sst = sum((p[1] - ym) ** 2 for p in puntos)
    sse = sum((p[1] - (a + b * p[0])) ** 2 for p in puntos)
    r2 = 1.0 - (sse / sst) if sst > 1e-12 else 0.0
    res = max(abs(p[1] - (a + b * p[0])) for p in puntos)
    return {"contado": a, "pendiente": b, "r2": r2, "residuo": res, "n": n}

def medir(crudo: dict, venc=None, n=12, max_vencimientos=10):
    """Base indice -> futuro, y de paso el contado, la tasa y el dividendo.

    Nada de esto esta escrito a mano. Todo sale de la misma cadena mas la
    curva de letras del Tesoro.

    El metodo obvio (forward del trimestral menos el indice) tiene un
    agujero: el indice al contado deja de cotizar 16:15 ET, las opciones no.
    Fuera de horario el current_price es el cierre anterior y las opciones
    ya descuentan lo que paso despues.

    Medido el 2026-08-31 a las 02:37 ET sobre SPX: el forward del 0DTE daba
    7679,30 y el current_price decia 7711,76. Un forward de cero dias ES el
    contado por definicion, asi que los 32 puntos no eran base: era el
    indice atrasado dos sesiones y media.

    La solucion es no tocar el indice. Se toman los forwards de todos los
    vencimientos cercanos y se les pasa una recta:

        forward(T) = contado + contado * (tasa - dividendo) * T/365

    La ordenada al origen es el contado de ahora. La pendiente da el carry
    neto. Restandole la tasa del Tesoro queda el dividendo implicito, que es
    el numero que delata si algo esta mal: si da 5 % en el S&P, la medicion
    no sirve por mas lindo que se vea el resto.

    El control es la recta misma. Ocho vencimientos que caen sobre una linea
    no se ponen de acuerdo por casualidad.
    """
    d = crudo["data"]
    S = d["current_price"]
    if venc is None:
        venc, _ = contrato_vigente()
    if venc is None:
        return None

    vencs = _vencimientos(d)
    if venc not in vencs:
        return None
    cercano = vencs[0]

    # forward de cada vencimiento hasta el trimestral inclusive
    hasta = [v for v in vencs if v <= venc][:max_vencimientos]
    if venc not in hasta:
        hasta.append(venc)
    puntos, detalle = [], []
    for v in hasta:
        pk = _porK(d, v)
        if not pk:
            continue
        f = _forward(pk, S, "mid", n) or _forward(pk, S, "last", n)
        if not f:
            continue
        dias = (v - cercano).days
        puntos.append((dias, f["forward"]))
        detalle.append({"vencimiento": v.isoformat(), "dias": dias,
                        "forward": f["forward"], "muestras": f["muestras"],
                        "dispersion": f["dispersion"]})
    if len(puntos) < 3:
        return None

    aj = _ajuste(puntos)
    f_fut = next((x for x in detalle if x["vencimiento"] == venc.isoformat()), None)
    f_cer = detalle[0]
    if not f_fut:
        return None

    # el contado sale de la recta; si el ajuste fallara, del vencimiento mas cercano
    contado = round(aj["contado"], 2) if aj else f_cer["forward"]
    base = round(f_fut["forward"] - contado, 2)

    # carry neto anualizado que implica la pendiente
    carry_neto = (aj["pendiente"] * 365.0 / contado) if (aj and contado) else None
    dias_fut = f_fut["dias"]
    r = tasa_corta(max(dias_fut, 28))
    q = (r - carry_neto) if (r is not None and carry_neto is not None) else None

    desfase = round(contado - S, 2)
    atrasado = abs(desfase) > max(2.0, S * 0.0004)
    abierto = mercado_abierto(crudo.get("timestamp"))

    # El control se mide en TICKS del instrumento, que es la unidad en la que
    # se dibuja la linea. El R2 aca enganaria: el forward apenas sube 11
    # puntos en 18 dias, asi que un residuo insignificante se come un pedazo
    # grande de la varianza y el R2 baja sin que la medicion sea mala.
    r2 = aj["r2"] if aj else 0.0
    residuo = aj["residuo"] if aj else 9e9
    raiz = FUTURO.get(crudo.get("symbol", "").upper().replace("^", ""), "ES")
    tick = CONTRATO.get(raiz, CONTRATO["ES"])["tick"]
    residuo_tk = residuo / tick
    disp_tk = f_fut["dispersion"] / tick
    confiable = bool(aj
                     and residuo_tk <= 8            # 8 ticks de error en la linea
                     and disp_tk <= 12
                     and len(puntos) >= 5
                     and f_fut["muestras"] >= 8
                     and q is not None and 0.0 <= q <= 0.04)

    avisos = []
    if atrasado:
        avisos.append("el indice publicado esta atrasado %+.2f puntos; la cadena "
                      "cotiza el contado en %s" % (desfase, contado))
    if aj and residuo_tk > 8:
        avisos.append("los forwards no caen sobre una recta: %.1f ticks de error "
                      "en el peor vencimiento" % residuo_tk)
    if q is not None and not (-0.01 <= q <= 0.06):
        avisos.append("el dividendo implicito da %.2f %%, fuera de lo razonable "
                      "para un indice de acciones" % (q * 100))

    return {"base": base,
            "forward": f_fut["forward"],
            "contado_implicito": contado,
            "indice_publicado": S,
            "desfase_indice": desfase,
            "indice_atrasado": atrasado,
            # medido, no escrito a mano
            "tasa_corta": round(r, 5) if r is not None else None,
            "tasa_fuente": (_curva_tasas() or {}).get("fuente"),
            "tasa_fecha": (_curva_tasas() or {}).get("fecha"),
            "carry_neto": round(carry_neto, 5) if carry_neto is not None else None,
            "dividendo_implicito": round(q, 5) if q is not None else None,
            # controles del ajuste
            "r2": round(r2, 4),
            "residuo_max": round(residuo, 3),
            "residuo_pct": round(residuo / S * 100, 4),
            "residuo_ticks": round(residuo_tk, 1),
            "dispersion_ticks": round(disp_tk, 1),
            "tick": tick, "raiz": raiz,
            "vencimientos_usados": len(puntos),
            "curva_forward": detalle,
            "fuente": "recta sobre %d forwards (paridad put-call)" % len(puntos),
            "mercado_abierto": abierto,
            "indice": S,
            "vencimiento": venc.isoformat(),
            "vencimiento_cercano": cercano.isoformat(),
            "muestras": f_fut["muestras"],
            "dispersion": f_fut["dispersion"],
            "confiable": confiable,
            "dispersion_pct": round(f_fut["dispersion"] / S * 100, 4),
            "aviso": " · ".join(avisos) if avisos else None}

def _dst_eeuu(f: dt.date) -> bool:
    """Horario de verano: del segundo domingo de marzo al primero de noviembre."""
    def domingo(anio, mes, cual):
        ds = [d for d in range(1, calendar.monthrange(anio, mes)[1] + 1)
              if dt.date(anio, mes, d).weekday() == 6]
        return dt.date(anio, mes, ds[cual - 1])
    return domingo(f.year, 3, 2) <= f < domingo(f.year, 11, 1)

def a_et(timestamp: str):
    """El timestamp de la cadena de CBOE viene en UTC.

    Se verifico el 2026-08-31 a las 05:26 UTC: la cadena de NDX decia
    05:08:10, dieciocho minutos antes. Leido como ET habria quedado casi
    cuatro horas en el futuro. Antes se leia como ET y por eso la antiguedad
    del dato salia negativa y el horario de mercado daba cualquier cosa.

    Ojo que el last_trade_time de cada contrato SI viene en ET (el indice
    cierra 16:14:59). Son dos campos con dos zonas distintas.
    """
    if not timestamp:
        return None
    try:
        t = dt.datetime.strptime(timestamp[:19], "%Y-%m-%d %H:%M:%S")
    except ValueError:
        return None
    t = t.replace(tzinfo=dt.timezone.utc)
    return t.astimezone(dt.timezone(dt.timedelta(hours=-4 if _dst_eeuu(t.date()) else -5)))

def edad_minutos(timestamp: str):
    """Hace cuanto cotizo esta cadena. None si el timestamp no se entiende."""
    t = a_et(timestamp)
    if t is None:
        return None
    return (dt.datetime.now(dt.timezone.utc) - t).total_seconds() / 60.0

def mercado_abierto(timestamp: str) -> bool:
    """9:30 a 16:15 ET de lunes a viernes."""
    t = a_et(timestamp)
    if t is None or t.weekday() > 4:
        return False
    m = t.hour * 60 + t.minute
    return 9 * 60 + 30 <= m <= 16 * 60 + 15

def convertir(nivel, base):
    if nivel is None or base is None:
        return None
    return round(nivel + base, 2)

# Como se llama el futuro de cada indice
FUTURO = {"SPX": "ES", "NDX": "NQ", "RUT": "RTY",
          "^SPX": "ES", "^NDX": "NQ", "^RUT": "RTY"}
MICRO  = {"ES": "MES", "NQ": "MNQ", "RTY": "M2K"}

def nombre_futuro(simbolo_indice: str, codigo=None):
    raiz = FUTURO.get(simbolo_indice.upper().replace("^", ""), "?")
    return (raiz + (codigo or ""), MICRO.get(raiz, "?") + (codigo or ""))
