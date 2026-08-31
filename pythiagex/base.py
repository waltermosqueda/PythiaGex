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

# Los futuros de indices son trimestrales: marzo, junio, septiembre, diciembre.
MESES_TRIMESTRALES = (3, 6, 9, 12)
CODIGO_MES = {3: "H", 6: "M", 9: "U", 12: "Z"}

def tercer_viernes(anio: int, mes: int) -> dt.date:
    viernes = [d for d in range(1, calendar.monthrange(anio, mes)[1] + 1)
               if dt.date(anio, mes, d).weekday() == 4]
    return dt.date(anio, mes, viernes[2])

def contrato_vigente(hoy=None):
    """Devuelve (vencimiento, codigo) del futuro que se esta operando.
    El rollover practico es unos dias antes del vencimiento."""
    hoy = hoy or dt.date.today()
    for anio in (hoy.year, hoy.year + 1):
        for mes in MESES_TRIMESTRALES:
            v = tercer_viernes(anio, mes)
            if v >= hoy:
                return v, CODIGO_MES[mes] + str(anio)[-1]
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

def medir(crudo: dict, venc=None, n=12):
    """Base indice -> futuro por diferencia de forwards.

    El metodo obvio (forward del vencimiento trimestral menos el indice)
    tiene un agujero: el indice al contado deja de cotizar 16:15 ET, pero
    las opciones siguen cotizando de noche. Fuera de horario el
    current_price es el cierre del dia anterior y las opciones ya
    descuentan lo que paso despues.

    Medido el 2026-08-31 a las 02:37 ET sobre SPX: el forward del 0DTE
    daba 7679,30 y el current_price decia 7711,76. Un forward de cero dias
    ES el contado por definicion, asi que los 32 puntos de diferencia no
    eran base: era el indice atrasado dos sesiones y media.

    La solucion es no tocar el indice. Los dos forwards salen de la misma
    cadena, cotizada en el mismo instante:

        base = forward(vencimiento trimestral) - forward(vencimiento mas cercano)

    Lo que quede es carry puro, que es lo que la base es. Verificado contra
    la teoria el 2026-08-31: SPX daba +10,85 medido contra +10,3 teorico
    por tasa menos dividendos a 18 dias.
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

    pf = _porK(d, venc)
    pc = _porK(d, cercano)
    if not pf or not pc:
        return None

    # el forward del vencimiento cercano es el contado implicito por la cadena
    f_cerca = _forward(pc, S, "mid", n)
    f_fut   = _forward(pf, S, "mid", n)
    if not f_cerca or not f_fut:
        # sin bid/ask utilizables, se cae al ultimo operado
        f_cerca = f_cerca or _forward(pc, S, "last", n)
        f_fut   = f_fut   or _forward(pf, S, "last", n)
        if not f_cerca or not f_fut:
            return None

    contado = f_cerca["forward"]
    base = round(f_fut["forward"] - contado, 2)

    dias = (venc - cercano).days
    # carry teorico: tasa corta menos dividendo del indice
    q = 0.006 if "NDX" in crudo.get("symbol", "").upper() else 0.013
    carry = round(contado * (0.040 - q) * max(dias, 0) / 365.0, 2)

    desfase = round(contado - S, 2)
    abierto = mercado_abierto(crudo.get("timestamp"))

    # la base es creible si los dos forwards son coherentes entre strikes
    disp = max(f_fut["dispersion"], f_cerca["dispersion"])
    confiable = (disp / S < 0.0005
                 and f_fut["muestras"] >= 8 and f_cerca["muestras"] >= 8
                 and abs(base - carry) < max(3.0, S * 0.0008))

    return {"base": base,
            "forward": f_fut["forward"],
            "contado_implicito": contado,
            "indice_publicado": S,
            "desfase_indice": desfase,
            "indice_atrasado": abs(desfase) > max(2.0, S * 0.0004),
            "carry_teorico": carry,
            "diferencia_vs_carry": round(base - carry, 2),
            "fuente": "forward vs forward (paridad put-call)",
            "mercado_abierto": abierto,
            "indice": S,
            "vencimiento": venc.isoformat(),
            "vencimiento_cercano": cercano.isoformat(),
            "muestras": min(f_fut["muestras"], f_cerca["muestras"]),
            "dispersion": disp,
            "confiable": confiable,
            "dispersion_pct": round(disp / S * 100, 4),
            "aviso": ("el indice publicado esta atrasado " + str(desfase)
                      + " puntos; la cadena cotiza el contado en "
                      + str(contado)) if abs(desfase) > max(2.0, S * 0.0004) else None}

def mercado_abierto(timestamp: str) -> bool:
    """9:30 a 16:15 ET de lunes a viernes. El timestamp de CBOE viene en ET."""
    if not timestamp:
        return False
    try:
        t = dt.datetime.strptime(timestamp[:19], "%Y-%m-%d %H:%M:%S")
    except ValueError:
        return False
    if t.weekday() > 4:
        return False
    minutos = t.hour * 60 + t.minute
    return 9 * 60 + 30 <= minutos <= 16 * 60 + 15

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
