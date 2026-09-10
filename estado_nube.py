# -*- coding: utf-8 -*-
"""EL ESTADO DE GAMMA HOY, CALCULADO EN LA NUBE (GitHub Actions, cada minuto).

Es la MISMA cuenta que hace el indicador Gamma Hoy en ATAS (GammaHoyNucleo.cs), escrita en
Python para que corra sin la PC del operador: si su ATAS esta cerrado o la PC apagada, la web
(panel/) sigue mostrando el perfil, el zero gamma, los majors, las dominantes, el cuadrante,
las pelotitas (Max Change) y los vencimientos, con la cadena de CBOE que cadenas.yml baja cada
minuto. Cuando la PC esta prendida, la web prefiere lo que el indicador manda (vivo-*.json).

Entradas (en la carpeta de la rama "cadenas"):
  ultima-<RAIZ>.json            la ultima cadena flaca (archivar_cadena.py --bajar)
  cadena-<RAIZ>-<dia>.jsonl.gz  el archivo del dia, para las fotos del Max Change
Salidas (misma carpeta):
  estado-<RAIZ>.json            todo lo que dibuja la web, en strike (K) y en futuro
  velas-<RAIZ>.json             velas de 1 min del futuro y del indice (Yahoo, con retraso)
  serie-<RAIZ>-<dia>.jsonl      una linea por corrida: la historia intradia de los niveles

Regla de la casa: ningun numero sin su fuente y su edad. Cada salida lleva "generado",
"cadena_ts", "edad_min" y el origen de la base. Lo que no se pudo medir queda en null.

Uso: python estado_nube.py --destino cadenas [ES NQ] [--ahora 2026-09-10T21:43:20Z --futuro 29135.5]
"""
import datetime as dt
import glob
import gzip
import io
import json
import math
import os
import sys
import time
import urllib.request

MULT = 100.0
PISO_DIAS = 1.0 / 1440.0
VENTANAS = (1, 5, 10, 15, 30)
TASA = 0.0375
DIVIDENDO = {"NQ": 0.008, "ES": 0.012, "RTY": 0.012}
YAHOO = {"NQ": ("NQ=F", "^NDX"), "ES": ("ES=F", "^GSPC"), "RTY": ("RTY=F", "^RUT")}
PASO_STRIKE = {"NQ": 10.0, "ES": 5.0, "RTY": 5.0}


class Ajustes:
    horizonte = "Hoy"           # Hoy | Semana | Todo
    cuantas = 2
    radio_dom_pct = 2.0
    radio_centro = 12.0
    pico_pct = 0.35
    mucho_pct = 50
    convexidad = "Auto"         # Auto | Volumen | OI
    una_por_lado = True
    centroide = True
    tasa = TASA
    dividendo = 0.008
    exp_futuro = None           # datetime UTC
    exp_futuro_alt = None


def utc_ahora():
    return dt.datetime.now(dt.timezone.utc)


def parse_utc(s):
    if not s:
        return None
    s = s.replace("Z", "+00:00")
    try:
        d = dt.datetime.fromisoformat(s)
    except ValueError:
        d = dt.datetime.strptime(s[:19], "%Y-%m-%d %H:%M:%S")
    if d.tzinfo is None:
        d = d.replace(tzinfo=dt.timezone.utc)
    return d.astimezone(dt.timezone.utc)


def tercer_viernes(anio, mes):
    d = dt.date(anio, mes, 1)
    corr = (4 - d.weekday()) % 7      # viernes = 4
    return d + dt.timedelta(days=corr + 14)


def trimestral_desde(ahora):
    """El vencimiento trimestral (tercer viernes de mar/jun/sep/dic, 13:30 UTC) que sigue
    a 'ahora', y el siguiente: como en GammaHoy.TrimestralDesde."""
    out = []
    a, m = ahora.year, ahora.month
    for _ in range(8):
        mq = ((m - 1) // 3 + 1) * 3
        f = dt.datetime.combine(tercer_viernes(a, mq), dt.time(13, 30), tzinfo=dt.timezone.utc)
        if f > ahora and (not out or f > out[-1]):
            out.append(f)
            if len(out) == 2:
                break
        m = mq + 1
        if m > 12:
            m = 1; a += 1
    return out[0], out[1]


# ------------------------------------------------------------------ Black-Scholes
def fi(x):
    return math.exp(-0.5 * x * x) / math.sqrt(2.0 * math.pi)


def N(x):
    s = -1.0 if x < 0 else 1.0
    x = abs(x) / math.sqrt(2.0)
    t = 1.0 / (1.0 + 0.3275911 * x)
    y = 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * math.exp(-x * x)
    return 0.5 * (1.0 + s * y)


def gamma_bs(S, K, T, iv, r):
    if S <= 0 or K <= 0 or T <= 0 or iv <= 0:
        return 0.0
    v = iv * math.sqrt(T)
    d1 = (math.log(S / K) + (r + 0.5 * iv * iv) * T) / v
    return fi(d1) / (S * v)


def d1d2(S, K, T, iv, r):
    v = iv * math.sqrt(T)
    d1 = (math.log(S / K) + (r + 0.5 * iv * iv) * T) / v
    return d1, d1 - v, v


# ------------------------------------------------------------------ la cadena
class Fila:
    __slots__ = ("K", "V", "OiC", "OiP", "IvC", "IvP", "VolC", "VolP")

    def __init__(self, a):
        self.K, self.V, self.OiC, self.OiP, self.IvC, self.IvP, self.VolC, self.VolP = float(a[0]), int(a[1]), float(a[2]), float(a[3]), float(a[4]), float(a[5]), float(a[6]), float(a[7])


def parsear(d):
    """El JSON del feed (flaco) a una cadena como Feed.Parsear."""
    c = d.get("cadena") or {}
    if not c.get("filas"):
        return None
    return dict(ts=c.get("ts", ""), spot_idx=float(c.get("spot_idx") or 0), dias=[float(v.get("dias") or 0) for v in c.get("vencimientos", [])],
                vencs=[v.get("f") for v in c.get("vencimientos", [])], filas=[Fila(a) for a in c["filas"] if len(a) >= 8],
                base=float(d.get("base") or 0), base_confiable=bool(d.get("base_confiable")), base_cruda=float(d.get("base_cruda") or 0),
                base_error_ticks=float(d.get("base_error_ticks") or 0), base_ultima_buena=float(d.get("base_ultima_buena") or 0),
                base_ultima_buena_edad=float(d.get("base_ultima_buena_edad_min") or 0), edad_min=float(d.get("edad_min") or 0),
                generado=parse_utc(d.get("generado")), ultimo_trade=c.get("ultimo_trade"), horizonte_cadena=c.get("horizonte_dias"))


def gex(f, S, T, r, por_volumen):
    gC = gamma_bs(S, f.K, T, f.IvC, r); gP = gamma_bs(S, f.K, T, f.IvP, r)
    wC = f.VolC if por_volumen else f.OiC; wP = f.VolP if por_volumen else f.OiP
    return (gC * wC - gP * wP) * MULT * S * S * 0.01


def pasa_horizonte(A, dias, mas_cerca):
    if A.horizonte == "Hoy":
        return 0 <= dias <= max(1.0, mas_cerca + 0.01)
    if A.horizonte == "Semana":
        return 0 <= dias <= max(7.0, mas_cerca + 0.01)
    return dias >= 0


def envejecer(c, ahora):
    if not c.get("generado") or ahora <= c["generado"]:
        return 0.0
    return min(2.0, (ahora - c["generado"]).total_seconds() / 86400.0)


def cruce(A, c, S, r, mas_cerca, env, por_volumen):
    lo, hi, pasos = S * 0.97, S * 1.03, 60
    ant, x_ant = None, 0.0
    filas = [(f, c["dias"][f.V] - env) for f in c["filas"] if 0 <= f.V < len(c["dias"])]
    filas = [(f, max(dd, PISO_DIAS) / 365.0) for f, dd in filas if pasa_horizonte(A, dd, mas_cerca)]
    for i in range(pasos + 1):
        x = lo + (hi - lo) * i / pasos
        t = 0.0
        for f, T in filas:
            t += gex(f, x, T, r, por_volumen)
        if ant is not None and ((ant < 0 <= t) or (ant > 0 >= t)):
            return x_ant + (x - x_ant) * (-ant) / (t - ant) if t != ant else x
        ant, x_ant = t, x
    return None


def elegir_base(A, c, futuro, ahora, base_medida_precio=None, edad_medida_min=None):
    """medida > medida reciente > medida por precio (Yahoo, como 'de la rueda') > cruda > TEORICA;
    todas acotadas con el carry del contrato (GammaHoyNucleo 1.5)."""
    carry = carry_alt = None
    if A.exp_futuro and A.exp_futuro > ahora:
        carry = futuro * (A.tasa - A.dividendo) * (A.exp_futuro - ahora).total_seconds() / 86400.0 / 365.0
    if A.exp_futuro_alt and A.exp_futuro_alt > ahora:
        carry_alt = futuro * (A.tasa - A.dividendo) * (A.exp_futuro_alt - ahora).total_seconds() / 86400.0 / 365.0

    def cerca(b, k):
        return abs(b - k) <= max(abs(k) * 0.6, futuro * 0.0006)

    def razonable_medida(b):
        return carry is None or cerca(b, carry) or (carry_alt is not None and cerca(b, carry_alt))

    def razonable(b):
        return carry is None or cerca(b, carry)

    if c["base_confiable"] and c["base"] != 0 and razonable_medida(c["base"]):
        return c["base"], "medida", carry
    if c["base_ultima_buena"] != 0 and c["base_ultima_buena_edad"] <= 360 and razonable_medida(c["base_ultima_buena"]):
        return c["base_ultima_buena"], "medida hace %.0f min" % c["base_ultima_buena_edad"], carry
    if base_medida_precio is not None and edad_medida_min is not None and edad_medida_min <= 24 * 60 and razonable(base_medida_precio):
        return base_medida_precio, "por precio (Yahoo) hace %.0f min" % edad_medida_min, carry
    if c["base_cruda"] != 0 and razonable(c["base_cruda"]):
        return c["base_cruda"], "CRUDA %.0f ticks" % c["base_error_ticks"], carry
    if carry is not None:
        return carry, "TEORICA carry %.1f" % carry + (" (cruda %.1f descartada)" % c["base_cruda"] if c["base_cruda"] != 0 else ""), carry
    if c["base_cruda"] != 0:
        return c["base_cruda"], "CRUDA %.0f ticks (sin cota)" % c["base_error_ticks"], carry
    return None, "sin base", carry


def perfil_k(A, c, S, ahora):
    """El perfil por strike en K (sin base): lo que las fotos del Max Change necesitan."""
    r = A.tasa; Sup = S * 1.01
    env = envejecer(c, ahora)
    mas = min([d - env for d in c["dias"] if d - env >= 0] or [0.0])
    por = {}
    for f in c["filas"]:
        if not (0 <= f.V < len(c["dias"])):
            continue
        dias = c["dias"][f.V] - env
        if not pasa_horizonte(A, dias, mas):
            continue
        T = max(dias, PISO_DIAS) / 365.0
        gOi, gVol = gex(f, S, T, r, False), gex(f, S, T, r, True)
        gOiUp, gVolUp = gex(f, Sup, T, r, False), gex(f, Sup, T, r, True)
        if gOi == 0 and gVol == 0:
            continue
        s = por.get(f.K)
        if s is None:
            s = por[f.K] = dict(K=f.K, gexOi=0.0, gexVol=0.0, oi=0.0, vol=0.0, ivSum=0.0, ivW=0.0, dte=1e9, convVol=0.0, convOi=0.0,
                                oiC=0.0, oiP=0.0, volC=0.0, volP=0.0, vencs=set())
        s["gexOi"] += gOi; s["gexVol"] += gVol
        s["oi"] += f.OiC + f.OiP; s["vol"] += f.VolC + f.VolP
        s["oiC"] += f.OiC; s["oiP"] += f.OiP; s["volC"] += f.VolC; s["volP"] += f.VolP
        wc, wp = f.OiC + f.VolC, f.OiP + f.VolP
        if f.IvC > 0: s["ivSum"] += f.IvC * wc; s["ivW"] += wc
        if f.IvP > 0: s["ivSum"] += f.IvP * wp; s["ivW"] += wp
        if dias < s["dte"]: s["dte"] = dias
        s["convVol"] += gVolUp - gVol; s["convOi"] += gOiUp - gOi
        s["vencs"].add(f.V)
    return sorted(por.values(), key=lambda x: x["K"]), mas, env


def calcular(A, c, futuro, ahora, fotos=None, base_medida_precio=None, edad_medida_min=None):
    """La cuenta entera para UNA cadena, UN precio del futuro y UNA hora (= CalcularAdentro)."""
    base, origen, carry = elegir_base(A, c, futuro, ahora, base_medida_precio, edad_medida_min)
    if base is None:
        return dict(sin_base=True, base_origen=origen)
    S = futuro - base
    if S <= 0:
        return None
    perfil, mas, env = perfil_k(A, c, S, ahora)
    for s in perfil:
        s["fut"] = s["K"] + base
    sumVol = sum(abs(s["gexVol"]) for s in perfil); sumOi = sum(abs(s["gexOi"]) for s in perfil)
    convPorVol = A.convexidad == "Volumen" or (A.convexidad == "Auto" and sumVol >= 0.2 * sumOi and sumVol > 0)
    for s in perfil:
        s["conv"] = s["convVol"] if convPorVol else s["convOi"]
    netVol = sum(s["gexVol"] for s in perfil); netOi = sum(s["gexOi"] for s in perfil)
    maxAbsVol = max((abs(s["gexVol"]) for s in perfil), default=0.0); maxAbsOi = max((abs(s["gexOi"]) for s in perfil), default=0.0)
    maxAbsConv = max((abs(s["conv"]) for s in perfil), default=0.0)
    zv = cruce(A, c, S, A.tasa, mas, env, True); zo = cruce(A, c, S, A.tasa, mas, env, False)
    zeroVol = zv + base if zv is not None else None; zeroOi = zo + base if zo is not None else None
    def mejor(clave, pos):
        cand = [s for s in perfil if (s[clave] > 0 if pos else s[clave] < 0)]
        if not cand: return None
        return (max(cand, key=lambda s: s[clave]) if pos else min(cand, key=lambda s: s[clave]))["fut"]
    mpVol, mnVol, mpOi, mnOi = mejor("gexVol", True), mejor("gexVol", False), mejor("gexOi", True), mejor("gexOi", False)
    # dominantes
    radio = futuro * A.radio_dom_pct / 100.0
    cuantas = max(1, A.cuantas)
    libroDom = "vol"
    en = [s for s in perfil if abs(s["fut"] - futuro) <= radio and abs(s["gexVol"]) > 0]
    cand = [(s["fut"], s["gexVol"]) for s in sorted(en, key=lambda s: -abs(s["gexVol"]))[:cuantas]]
    if not cand:
        libroDom = "OI"
        en = [s for s in perfil if abs(s["fut"] - futuro) <= radio and abs(s["gexOi"]) > 0]
        cand = [(s["fut"], s["gexOi"]) for s in sorted(en, key=lambda s: -abs(s["gexOi"]))[:cuantas]]
    peso = (lambda s: abs(s["gexVol"])) if libroDom == "vol" else (lambda s: abs(s["gexOi"]))
    gclave = "gexVol" if libroDom == "vol" else "gexOi"
    if A.una_por_lado and perfil:
        enRadio = [s for s in perfil if abs(s["fut"] - futuro) <= radio and peso(s) > 0]
        arriba = max((s for s in enRadio if s["fut"] > futuro), key=peso, default=None)
        abajo = max((s for s in enRadio if s["fut"] <= futuro), key=peso, default=None)
        lados = []
        if arriba: lados.append((arriba["fut"], arriba[gclave]))
        if abajo: lados.append((abajo["fut"], abajo[gclave]))
        for s in sorted(enRadio, key=peso, reverse=True):
            if len(lados) >= cuantas: break
            if any(l[0] == s["fut"] for l in lados): continue
            lados.append((s["fut"], s[gclave]))
        if lados: cand = lados
    doms_strike = [(f, g) for f, g in cand]
    if A.centroide and cand:
        con = []
        for f0, g0 in cand:
            sw = sx = 0.0
            for s in perfil:
                if abs(s["fut"] - f0) > A.radio_centro: continue
                w = peso(s)
                if w <= 0: continue
                sw += w; sx += w * s["fut"]
            con.append((sx / sw if sw > 0 else f0, g0))
        cand = con
    # cuadrante
    rPico = futuro * A.pico_pct / 100.0
    cerca = [s for s in perfil if abs(s["fut"] - futuro) <= rPico]
    porVolCuad = sumVol > 0 and sumVol >= 0.2 * sumOi
    gk = "gexVol" if porVolCuad else "gexOi"
    picoGex, picoFut, convPrecio = 0.0, None, 0.0
    for s in cerca:
        if abs(s[gk]) > abs(picoGex): picoGex, picoFut = s[gk], s["fut"]
        convPrecio += s["conv"]
    if not cerca and perfil:
        convPrecio = min(perfil, key=lambda s: abs(s["fut"] - futuro))["conv"]
    maxLibro = maxAbsVol if porVolCuad else maxAbsOi
    mucho = maxLibro > 0 and abs(picoGex) >= maxLibro * A.mucho_pct / 100.0
    convPos = convPrecio >= 0
    if mucho and convPos: q, nombre, corto = 1, "iman colchon: rango, reversion", "IMAN"
    elif mucho and not convPos: q, nombre, corto = 2, "nivel explosivo: ruptura, momentum", "EXPLOSIVO"
    elif not mucho and convPos: q, nombre, corto = 3, "mercado estable: rangos amplios", "ESTABLE"
    else: q, nombre, corto = 4, "salvese quien pueda: tendencia, tamaño chico", "RIESGO"
    # max change (las fotos vienen de afuera: {minuto: {K: gexVol}})
    minuto = int(ahora.timestamp() // 60)
    mc = []
    antes = {}
    if fotos:
        for w in VENTANAS:
            vieja = None
            for m in sorted(fotos):
                if m <= minuto - w: vieja = fotos[m]
            best, futM = 0.0, None
            if vieja is not None:
                for s in perfil:
                    d = s["gexVol"] - vieja.get(s["K"], 0.0)
                    if abs(d) > abs(best): best, futM = d, s["fut"]
            mc.append(dict(min=w, fut=futM, delta=best))
        for w in (1, 5, 15):
            vieja = None
            for m in sorted(fotos):
                if m <= minuto - w: vieja = fotos[m]
            if vieja is not None:
                for s in perfil:
                    antes.setdefault(s["K"], {})[w] = vieja.get(s["K"], 0.0)
    return dict(futuro=futuro, S=S, base=base, base_origen=origen, carry=carry, masCerca=mas, envejecido_dias=env,
                perfil=perfil, netVol=netVol, netOi=netOi, zeroVol=zeroVol, zeroOi=zeroOi, mpVol=mpVol, mnVol=mnVol, mpOi=mpOi, mnOi=mnOi,
                maxAbsVol=maxAbsVol, maxAbsOi=maxAbsOi, maxAbsConv=maxAbsConv, doms=cand, doms_strike=doms_strike, libroDom=libroDom,
                libroConv="vol" if convPorVol else "OI", q=q, cuadrante=nombre, corto=corto, picoFut=picoFut, picoGex=picoGex,
                convPrecio=convPrecio, mucho=mucho, mc=mc, antes=antes, strikes=len(perfil))


def audit(L, c):
    """La misma linea AUDIT del indicador, para comparar a ojo."""
    f = lambda v, d=2: ("%.*f" % (d, v)) if v is not None else "NaN"
    mc30 = next((m for m in L["mc"] if m["min"] == 30), None)
    return ("AUDIT fut=%s S=%s base=%s origen=%s strikes=%d netVol=%.3fB netOi=%.3fB zeroVol=%s zeroOi=%s mpVol=%s mnVol=%s doms=%s libroDom=%s conv=%s q=%d pico=%s picoGex=%.0fM mucho=%s convPrecio=%.0fM mc30=%s:%.0fM edadFeed=%.1fmin" % (
        f(L["futuro"]), f(L["S"]), f(L["base"]), L["base_origen"].replace(" ", "_"), L["strikes"], L["netVol"] / 1e9, L["netOi"] / 1e9, f(L["zeroVol"]), f(L["zeroOi"]),
        f(L["mpVol"]), f(L["mnVol"]), "/".join("%.2f=%.0fM" % (d[0], d[1] / 1e6) for d in L["doms"]), L["libroDom"], L["libroConv"], L["q"], f(L["picoFut"]),
        L["picoGex"] / 1e6, L["mucho"], L["convPrecio"] / 1e6, f(mc30["fut"]) if mc30 else "NaN", (mc30["delta"] / 1e6) if mc30 else 0, c["edad_min"]))


# ------------------------------------------------------------------ extras (tableros)
def extras(A, c, S, base, ahora, raiz):
    """Lo que Gamma Hoy no dibuja pero los tableros piden: por vencimiento, muros por OI,
    put/call, movimiento esperado por IV, max pain del 0DTE, sonrisa de IV, DEX y VEX por strike.
    Formulas estandar de Black-Scholes; se dicen en la web. NO son niveles auditados contra terceros."""
    r = A.tasa; env = envejecer(c, ahora)
    dias = [d - env for d in c["dias"]]
    por_v = {}
    for f in c["filas"]:
        if not (0 <= f.V < len(dias)) or dias[f.V] < 0: continue
        T = max(dias[f.V], PISO_DIAS) / 365.0
        v = por_v.setdefault(f.V, dict(i=f.V, f=c["vencs"][f.V], dias=dias[f.V], gexVol=0.0, gexOi=0.0, oiC=0.0, oiP=0.0, volC=0.0, volP=0.0, ivs=[]))
        v["gexVol"] += gex(f, S, T, r, True); v["gexOi"] += gex(f, S, T, r, False)
        v["oiC"] += f.OiC; v["oiP"] += f.OiP; v["volC"] += f.VolC; v["volP"] += f.VolP
        if abs(f.K - S) <= S * 0.01 and (f.IvC > 0 or f.IvP > 0):
            v["ivs"].append((abs(f.K - S), (f.IvC + f.IvP) / (2 if f.IvC > 0 and f.IvP > 0 else 1)))
    vencs = []
    for v in sorted(por_v.values(), key=lambda v: v["dias"]):
        atm = sorted(v["ivs"])[:4]
        iv = sum(x[1] for x in atm) / len(atm) if atm else None
        T = max(v["dias"], PISO_DIAS) / 365.0
        vencs.append(dict(f=v["f"], dias=round(v["dias"], 4), gexVol=v["gexVol"], gexOi=v["gexOi"], oiC=v["oiC"], oiP=v["oiP"], volC=v["volC"], volP=v["volP"],
                          pc_oi=(v["oiP"] / v["oiC"]) if v["oiC"] > 0 else None, pc_vol=(v["volP"] / v["volC"]) if v["volC"] > 0 else None,
                          iv_atm=iv, em_1sigma=(S * iv * math.sqrt(T)) if iv else None))
    # el 0DTE (o el mas cercano): muros por OI y por volumen, max pain, sonrisa
    cero = min((v for v in por_v.values() if v["dias"] >= 0), key=lambda v: v["dias"], default=None)
    muros = {}; maxpain = None; sonrisa = []
    if cero is not None:
        filas0 = [f for f in c["filas"] if f.V == cero["i"]]
        def muro(attr, cond):
            cand = [f for f in filas0 if getattr(f, attr) > 0 and cond(f)]
            return (lambda f: dict(K=f.K, fut=f.K + base, n=getattr(f, attr)))(max(cand, key=lambda f: getattr(f, attr))) if cand else None
        muros = dict(call_oi=muro("OiC", lambda f: True), put_oi=muro("OiP", lambda f: True), call_vol=muro("VolC", lambda f: True), put_vol=muro("VolP", lambda f: True))
        Ks = sorted(set(f.K for f in filas0 if abs(f.K - S) <= S * 0.03))
        if Ks:
            def dolor(K):
                return sum(f.OiC * max(0.0, K - f.K) + f.OiP * max(0.0, f.K - K) for f in filas0)
            kmp = min(Ks, key=dolor)
            maxpain = dict(K=kmp, fut=kmp + base)
            for f in sorted(filas0, key=lambda f: f.K):
                if abs(f.K - S) <= S * 0.03 and (f.IvC > 0 or f.IvP > 0):
                    sonrisa.append([f.K, f.IvC or None, f.IvP or None])
    # DEX y VEX por strike (horizonte del indicador), en dolares por 1 pt / por 1 % de vol
    dex = {}; vex = {}
    mas = min([d for d in dias if d >= 0] or [0.0])
    for f in c["filas"]:
        if not (0 <= f.V < len(dias)): continue
        d = dias[f.V]
        if not pasa_horizonte(A, d, mas): continue
        T = max(d, PISO_DIAS) / 365.0
        for iv, w, es_call in ((f.IvC, f.OiC, True), (f.IvP, f.OiP, False)):
            if iv <= 0 or w <= 0: continue
            d1, d2, v = d1d2(S, f.K, T, iv, r)
            delta = N(d1) if es_call else N(d1) - 1.0
            vanna = -fi(d1) * d2 / iv
            dex[f.K] = dex.get(f.K, 0.0) + delta * w * MULT * S * 0.01
            vex[f.K] = vex.get(f.K, 0.0) + vanna * w * MULT * S * 0.01
    tot_oiC = sum(f.OiC for f in c["filas"]); tot_oiP = sum(f.OiP for f in c["filas"])
    tot_vC = sum(f.VolC for f in c["filas"]); tot_vP = sum(f.VolP for f in c["filas"])
    return dict(vencimientos=vencs, muros_0dte=muros, maxpain_0dte=maxpain, sonrisa_0dte=sonrisa,
                pc_oi=(tot_oiP / tot_oiC) if tot_oiC else None, pc_vol=(tot_vP / tot_vC) if tot_vC else None,
                dex={str(k): v for k, v in dex.items() if abs(k - S) <= S * 0.04}, vex={str(k): v for k, v in vex.items() if abs(k - S) <= S * 0.04},
                nota="DEX = delta x OI x 100 x S x 1 %; VEX = vanna x OI x 100 x S x 1 % (Black-Scholes, OI de ayer). Muros = strike con mas OI/volumen del vencimiento mas cercano. EM 1 sigma = S x IV atm x raiz(T).")


# ------------------------------------------------------------------ Yahoo (velas con retraso)
def yahoo(simbolo, rango="1d", intervalo="1m", intentos=2):
    url = "https://query1.finance.yahoo.com/v8/finance/chart/%s?interval=%s&range=%s" % (urllib.parse.quote(simbolo), intervalo, rango)
    for i in range(intentos):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
            with urllib.request.urlopen(req, timeout=12) as r:
                d = json.load(r)["chart"]["result"][0]
            q = d["indicators"]["quote"][0]
            ts = d.get("timestamp") or []
            out = dict(simbolo=simbolo, t=[], o=[], h=[], l=[], c=[], v=[])
            for j, t in enumerate(ts):
                if q["close"][j] is None: continue
                out["t"].append(int(t)); out["o"].append(q["open"][j]); out["h"].append(q["high"][j]); out["l"].append(q["low"][j]); out["c"].append(q["close"][j]); out["v"].append(q["volume"][j] or 0)
            m = d.get("meta", {})
            out["ultimo"] = m.get("regularMarketPrice"); out["ultimo_t"] = m.get("regularMarketTime"); out["nombre"] = m.get("shortName")
            return out
        except Exception as e:
            err = str(e); time.sleep(2)
    return dict(simbolo=simbolo, error=err, t=[], o=[], h=[], l=[], c=[], v=[])


# ------------------------------------------------------------------ archivo del dia (fotos)
def leer_archivo(ruta):
    out = []
    try:
        with gzip.open(ruta, "rt", encoding="utf-8") as f:
            for l in f:
                if len(l) < 100: continue
                try: out.append(json.loads(l))
                except Exception: pass
    except Exception:
        pass
    return out


def fotos_del_dia(A, destino, raiz, ahora, base, minutos=35, velas_fut=None):
    """{minuto: {K: gexVol}} para CADA minuto de los ultimos 'minutos', con la cadena que estaba
    vigente en ese minuto (la ultima archivada con generado <= minuto) y al PRECIO de ese minuto
    (la vela de Yahoo del futuro menos la base; si no hay, el spot de la cadena). El indicador saca
    una foto por minuto de calculo con el precio de ese minuto aunque CBOE no haya cambiado: asi la
    foto de hace 30 min existe siempre y el Max Change compara contra lo mismo que compara el."""
    dia = ahora.strftime("%Y-%m-%d"); ayer = (ahora - dt.timedelta(days=1)).strftime("%Y-%m-%d")
    lineas = leer_archivo(os.path.join(destino, "cadena-%s-%s.jsonl.gz" % (raiz, ayer)))[-80:] + leer_archivo(os.path.join(destino, "cadena-%s-%s.jsonl.gz" % (raiz, dia)))
    cands = []
    for d in lineas:
        g = parse_utc(d.get("generado"))
        if g is None or g > ahora: continue
        cands.append((g, d))
    cands.sort(key=lambda x: x[0])
    precio_min = {}
    if velas_fut and velas_fut.get("t"):
        for t, cl in zip(velas_fut["t"], velas_fut["c"]):
            precio_min[int(t // 60)] = cl
    fotos = {}; parsed = {}
    m0 = int(ahora.timestamp() // 60)
    for m in range(m0 - minutos, m0 + 1):
        vig = None
        for g, d in cands:
            if int(g.timestamp() // 60) <= m: vig = (g, d)
            else: break
        if vig is None: continue
        g, d = vig
        k = g.isoformat()
        if k not in parsed: parsed[k] = parsear(d)
        c = parsed[k]
        if not c: continue
        # el precio del minuto: la vela de ese minuto (o la anterior mas cercana, hasta 3 min)
        S_m = None
        for mm in (m, m - 1, m - 2, m - 3):
            if mm in precio_min: S_m = precio_min[mm] - base; break
        if S_m is None or S_m <= 0: S_m = c["spot_idx"]
        p, _, _ = perfil_k(A, c, S_m, dt.datetime.fromtimestamp(m * 60, dt.timezone.utc))
        fotos[m] = {s["K"]: s["gexVol"] for s in p}
    return fotos


def compacto(x, dec=4):
    if isinstance(x, float):
        return None if (math.isnan(x) or math.isinf(x)) else round(x, dec)
    if isinstance(x, dict):
        return {k: compacto(v, dec) for k, v in x.items()}
    if isinstance(x, (list, tuple)):
        return [compacto(v, dec) for v in x]
    if isinstance(x, set):
        return sorted(x)
    return x


# ------------------------------------------------------------------ una raiz
def correr(raiz, destino, ahora=None, futuro_manual=None, escribir=True, log=print):
    A = Ajustes(); A.dividendo = DIVIDENDO.get(raiz, 0.012)
    ahora = ahora or utc_ahora()
    A.exp_futuro, A.exp_futuro_alt = trimestral_desde(ahora)
    ruta = os.path.join(destino, "ultima-%s.json" % raiz)
    try:
        d = json.load(io.open(ruta, encoding="utf-8"))
    except Exception as e:
        log("%s: no hay feed (%s)" % (raiz, e)); return None
    c = parsear(d)
    if not c:
        log("%s: feed sin filas" % raiz); return None
    # velas de Yahoo (futuro e indice): con retraso, y de noche se congelan
    velas = None
    if escribir:
        fut_s, idx_s = YAHOO[raiz]
        velas = dict(generado=ahora.isoformat(timespec="seconds"), fuente="Yahoo Finance (query1), 1 min, con retraso; no es Rithmic",
                     futuro=yahoo(fut_s), indice=yahoo(idx_s))
    # el futuro: la ultima vela de Yahoo si es fresca; si no, indice + base teorica
    futuro = futuro_manual; fut_edad = None; base_precio = None; edad_precio = None
    if futuro is None and velas and velas["futuro"]["t"]:
        vf = velas["futuro"]; t_ult = vf["t"][-1]
        fut_edad = (ahora.timestamp() - t_ult) / 60.0
        if fut_edad <= 30:
            futuro = vf["c"][-1]
        vi = velas["indice"]
        if vi["t"]:
            # la base por precio: futuro e indice en el MISMO minuto (los dos con el mismo retraso), MEDIANA de
            # los ultimos 40 minutos con indice (un minuto solo es ruido: medido 10-09, NQ 23-28 entre minutos)
            ti = {t: k for k, t in enumerate(vi["t"])}
            pares = [(vf["t"][k], vf["c"][k] - vi["c"][ti[vf["t"][k]]]) for k in range(len(vf["t"])) if vf["t"][k] in ti][-40:]
            if pares:
                xs = sorted(b for t, b in pares); base_precio = xs[len(xs) // 2]; edad_precio = (ahora.timestamp() - pares[-1][0]) / 60.0
    if futuro is None:
        _, _, carry = elegir_base(A, c, c["spot_idx"], ahora)
        futuro = c["spot_idx"] + (carry or c["base"] or c["base_cruda"] or 0.0)
        fut_origen = "indice de CBOE + base (sin precio del futuro fresco)"
    else:
        fut_origen = "manual" if futuro_manual is not None else "Yahoo %s hace %.0f min" % (YAHOO[raiz][0], fut_edad)
    base_tmp, _, _ = elegir_base(A, c, futuro, ahora, base_precio, edad_precio)
    fotos = fotos_del_dia(A, destino, raiz, ahora, base_tmp or 0.0, velas_fut=velas["futuro"] if velas else None)
    L = calcular(A, c, futuro, ahora, fotos, base_precio, edad_precio)
    if not L or L.get("sin_base"):
        log("%s: sin base (%s)" % (raiz, L and L.get("base_origen"))); return None
    log(raiz + " " + audit(L, c) + " fut_origen=" + fut_origen.replace(" ", "_"))
    S = L["S"]; base = L["base"]
    ex = extras(A, c, S, base, ahora, raiz)
    # las barras pesadas cerca (rayas punteadas) y el perfil recortado a +-4 %
    cerca = sorted((s for s in L["perfil"] if abs(s["fut"] - futuro) <= futuro * 0.006), key=lambda s: -abs(s["gexVol"]))[:4]
    perfil = [dict(K=s["K"], fut=s["fut"], gexVol=s["gexVol"], gexOi=s["gexOi"], conv=s["conv"], oi=s["oi"], vol=s["vol"], oiC=s["oiC"], oiP=s["oiP"], volC=s["volC"], volP=s["volP"],
                   iv=(s["ivSum"] / s["ivW"]) if s["ivW"] > 0 else None, dte=s["dte"], antes=[L["antes"].get(s["K"], {}).get(w) for w in (1, 5, 15)])
              for s in L["perfil"] if abs(s["K"] - S) <= S * 0.04]
    estado = dict(
        raiz=raiz, generado=ahora.isoformat(timespec="seconds"), cadena_ts=c["ts"], cadena_generada=c["generado"].isoformat(timespec="seconds") if c["generado"] else None,
        edad_min=round((ahora - c["generado"]).total_seconds() / 60.0, 1) if c["generado"] else None, ultimo_trade=c["ultimo_trade"],
        fuente="CBOE (cadena con ~15 min de retraso) via cadenas.yml; cuenta = GammaHoyNucleo portado (estado_nube.py)",
        ajustes=dict(horizonte=A.horizonte, cuantas=A.cuantas, radio_dom_pct=A.radio_dom_pct, radio_centro=A.radio_centro, pico_pct=A.pico_pct, mucho_pct=A.mucho_pct,
                     convexidad=A.convexidad, tasa=A.tasa, dividendo=A.dividendo, exp_futuro=A.exp_futuro.isoformat(), exp_futuro_alt=A.exp_futuro_alt.isoformat()),
        spot_idx=c["spot_idx"], futuro=futuro, fut_origen=fut_origen, S=S, base=base, base_origen=L["base_origen"], carry=L["carry"],
        base_feed=dict(medida=c["base"] if c["base_confiable"] else None, cruda=c["base_cruda"], error_ticks=c["base_error_ticks"]), base_por_precio=base_precio,
        masCerca=L["masCerca"], strikes=L["strikes"], netVol=L["netVol"], netOi=L["netOi"], zeroVol=L["zeroVol"], zeroOi=L["zeroOi"],
        mpVol=L["mpVol"], mnVol=L["mnVol"], mpOi=L["mpOi"], mnOi=L["mnOi"], maxAbsVol=L["maxAbsVol"], maxAbsOi=L["maxAbsOi"], maxAbsConv=L["maxAbsConv"],
        doms=[dict(fut=f, gex=g) for f, g in L["doms"]], doms_strike=[dict(fut=f, gex=g) for f, g in L["doms_strike"]], libroDom=L["libroDom"], libroConv=L["libroConv"],
        q=L["q"], cuadrante=L["cuadrante"], corto=L["corto"], picoFut=L["picoFut"], picoGex=L["picoGex"], convPrecio=L["convPrecio"], mucho=L["mucho"],
        mc=L["mc"], pesadas=[dict(K=s["K"], fut=s["fut"], gexVol=s["gexVol"], dte=s["dte"]) for s in cerca], fotos_min=len(fotos),
        perfil=perfil, vencimientos_cadena=[dict(f=f, dias=dd) for f, dd in zip(c["vencs"], c["dias"])], extras=ex, audit=audit(L, c),
    )
    if escribir:
        with io.open(os.path.join(destino, "estado-%s.json" % raiz), "w", encoding="utf-8", newline="\n") as f:
            json.dump(compacto(estado), f, ensure_ascii=False, separators=(",", ":"))
        if velas is not None and (velas["futuro"]["t"] or not os.path.exists(os.path.join(destino, "velas-%s.json" % raiz))):
            with io.open(os.path.join(destino, "velas-%s.json" % raiz), "w", encoding="utf-8", newline="\n") as f:
                json.dump(compacto(velas, 2), f, ensure_ascii=False, separators=(",", ":"))
        mc30 = next((m for m in L["mc"] if m["min"] == 30), None)
        linea = dict(t=ahora.isoformat(timespec="seconds"), cadena_ts=c["ts"], fut=futuro, S=S, base=base, origen=L["base_origen"], netVol=L["netVol"], netOi=L["netOi"],
                     zeroVol=L["zeroVol"], zeroOi=L["zeroOi"], mpVol=L["mpVol"], mnVol=L["mnVol"], dom0=L["doms"][0][0] if L["doms"] else None, dom1=L["doms"][1][0] if len(L["doms"]) > 1 else None,
                     q=L["q"], pico=L["picoFut"], mc30=mc30["fut"] if mc30 else None, em0=ex["vencimientos"][0]["em_1sigma"] if ex["vencimientos"] else None)
        with io.open(os.path.join(destino, "serie-%s-%s.jsonl" % (raiz, ahora.strftime("%Y-%m-%d"))), "a", encoding="utf-8", newline="\n") as f:
            f.write(json.dumps(compacto(linea), ensure_ascii=False, separators=(",", ":")) + "\n")
    return estado


def main():
    a = sys.argv[1:]
    def arg(k, d=None):
        return a[a.index(k) + 1] if k in a else d
    destino = arg("--destino", "cadenas")
    ahora = parse_utc(arg("--ahora")) if arg("--ahora") else None
    futuro = float(arg("--futuro")) if arg("--futuro") else None
    escribir = "--sin-escribir" not in a
    raices = [x for x in a if not x.startswith("--") and x not in (arg("--destino"), arg("--ahora"), arg("--futuro"))] or ["ES", "NQ"]
    for r in raices:
        try:
            t0 = time.time()
            correr(r, destino, ahora, futuro, escribir)
            print("%s: %.1f s" % (r, time.time() - t0))
        except Exception as e:
            import traceback; traceback.print_exc()
            print("%s: fallo %s" % (r, e))


if __name__ == "__main__":
    import urllib.parse  # noqa: E402  (para yahoo)
    main()
