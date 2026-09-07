# -*- coding: utf-8 -*-
"""NIVELES A PARTIR DEL FLUJO FIRMADO.

LA IDEA
Todo el modelo de GEX -- el nuestro y el de cualquiera -- se apoya en una
SUPOSICION: que las calls suman y las puts restan. Es una convencion sobre de
que lado quedaron las mesas, no un dato.

Pero si una operacion se hizo pegada al ASK la inicio un comprador, y si se
hizo pegada al BID la inicio un vendedor. Y si el cliente COMPRO una opcion, la
mesa quedo CORTA de esa opcion, o sea corta de gamma ahi. Se puede MEDIR el
signo en vez de asumirlo.

Se probo antes de escribir esto: el 87 % del volumen nuevo entre dos fotos
consecutivas se puede clasificar.

LO QUE ESTE ARCHIVO NO HACE
No promete direccion de precio. El gamma describe COMO se va a comportar el
movimiento -- rango o expansion -- nunca hacia donde. El flujo firmado dice de
que lado quedo la mesa, que es presion de cobertura en horizonte de horas.
"""
import sys, os, gzip, json, glob, math, statistics as st
from datetime import datetime

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cargar
import puntuar

RE_OK = True


def leer_crudo(ruta, radio=120.0, dias_max=7.0):
    """Solo lo necesario para firmar: volumen, puntas, ultimo precio, gamma."""
    try:
        with gzip.open(ruta, "rt", encoding="utf-8") as f:
            d = json.load(f)
    except Exception:
        return None
    dd = d.get("data") or {}
    sp = dd.get("current_price") or dd.get("close")
    if not sp:
        return None
    try:
        t = datetime.strptime(d.get("timestamp"), "%Y-%m-%d %H:%M:%S")
    except Exception:
        return None
    sp = float(sp)
    out = {}
    for o in dd.get("options", []):
        p = cargar.parsear_simbolo(o.get("option"))
        if not p or abs(p["K"] - sp) > radio:
            continue
        dias = (p["venc"] - t).total_seconds() / 86400.0
        if dias < 0 or dias > dias_max:
            continue
        out[o["option"]] = dict(
            K=p["K"], call=p["call"], dias=dias,
            vol=float(o.get("volume") or 0),
            bid=float(o.get("bid") or 0), ask=float(o.get("ask") or 0),
            ltp=float(o.get("last_trade_price") or 0),
            gamma=float(o.get("gamma") or 0),
            oi=float(o.get("open_interest") or 0))
    return dict(t=t, spot=sp, c=out)


def signo(f):
    """+1 si la operacion la inicio un COMPRADOR, -1 si un vendedor, 0 si no se sabe."""
    b, a, p = f["bid"], f["ask"], f["ltp"]
    if b <= 0 or a <= 0 or a <= b or p <= 0:
        return 0
    if p >= a - 1e-9:
        return 1
    if p <= b + 1e-9:
        return -1
    m = (a + b) / 2.0
    return 1 if p > m else (-1 if p < m else 0)


def acumular(dia, cada=2, radio=120.0):
    """Recorre la rueda acumulando el flujo firmado por strike.

    Devuelve, para cada foto, el mapa strike -> gamma de la MESA segun el
    flujo medido. Si el cliente compro, la mesa quedo corta: signo invertido.
    """
    rutas = cargar.fotos(dia)[::cada]
    ant = None
    acum = {}          # strike -> gamma de la mesa, acumulada desde la apertura
    salida = []
    for r in rutas:
        f = leer_crudo(r, radio=radio)
        if not f:
            continue
        if ant is not None:
            S = f["spot"]
            for cod, x in f["c"].items():
                y = ant["c"].get(cod)
                if not y:
                    continue
                dv = x["vol"] - y["vol"]
                if dv <= 0:
                    continue
                s = signo(x)
                if s == 0:
                    continue
                # el cliente compro (s=+1) -> la mesa quedo CORTA -> gamma negativa
                g = -s * dv * x["gamma"] * 100.0 * S * S * 0.01
                acum[x["K"]] = acum.get(x["K"], 0.0) + g
        salida.append(dict(t=f["t"], spot=f["spot"], acum=dict(acum)))
        ant = f
    return salida


def topn(d, n, piso=0.15):
    if not d:
        return []
    mx = max(abs(v) for v in d.values()) or 1.0
    xs = [(k, v) for k, v in d.items() if abs(v) >= piso * mx]
    xs.sort(key=lambda kv: -abs(kv[1]))
    return [k for k, _ in xs[:n]]


def main(dia="20260903", dia_iso="2026-09-03"):
    velas = cargar.velas(dia_iso)
    ph = {v["hora"]: v for v in velas}
    hmin, hmax = min(ph), max(ph)
    print("acumulando flujo firmado de la rueda...")
    serie = acumular(dia)
    print("fotos procesadas: %d" % len(serie))

    usables = []
    for s in serie:
        h = s["t"].hour * 60 + s["t"].minute + puntuar.OFF_MIN
        if hmin <= h <= hmax - puntuar.VENTANA and s["acum"]:
            s["h"] = h
            usables.append(s)
    print("dentro de la rueda y con flujo acumulado: %d" % len(usables))
    if len(usables) < 20:
        print("muestra insuficiente"); return

    formulas = {
        "N flujo firmado (mesa)": lambda s: topn(s["acum"], 6),
        "O flujo firmado, solo negativo": lambda s: topn(
            {k: v for k, v in s["acum"].items() if v < 0}, 6),
        "P flujo firmado, solo positivo": lambda s: topn(
            {k: v for k, v in s["acum"].items() if v > 0}, 6),
    }

    print()
    print("formula                          niv  beta   toques  freno  placebo  ventaja")
    px = [ph[s["h"]]["c"] for s in usables]
    for nom, fn in formulas.items():
        niveles = [fn(s) for s in usables]
        cerc = [min(ns, key=lambda k: abs(k - s["spot"])) if ns else None
                for s, ns in zip(usables, niveles)]
        pares = [(p, c) for p, c in zip(px, cerc) if c]
        beta = float("nan")
        if len(pares) > 20:
            A = [a for a, _ in pares]; B = [b for _, b in pares]
            ma, mb = st.fmean(A), st.fmean(B)
            den = sum((x - ma) ** 2 for x in A)
            if den:
                beta = sum((x - ma) * (y - mb) for x, y in zip(A, B)) / den

        def evaluar(corr):
            t = ok = 0
            for s, ns in zip(usables, niveles):
                for L in ns:
                    r = puntuar.tocar(velas, ph, L + corr, s["h"], s["h"] + puntuar.VENTANA)
                    if r:
                        t += 1; ok += r[0]
            return t, ok
        t0, ok0 = evaluar(0.0)
        tp = okp = 0
        for c in puntuar.PLACEBOS:
            a, b = evaluar(c)
            tp += a; okp += b
        if t0 < 10:
            print("  %-30s %3.0f  %5.2f    %4d   muestra chica"
                  % (nom, st.fmean(len(x) for x in niveles), beta, t0)); continue
        tasa = 100.0 * ok0 / t0
        tpp = 100.0 * okp / tp if tp else float("nan")
        print("  %-30s %3.0f  %5.2f    %4d  %4.1f%%   %4.1f%%   %+5.1f pp"
              % (nom, st.fmean(len(x) for x in niveles), beta, t0, tasa, tpp, tasa - tpp))
    print()
    print("  comparar contra la mejor de la tanda anterior:")
    print("    D volumen puro sin gamma  ->  61,5 % contra 19,1 %  = +42,4 pp")
    print("    A base gamma x OI (actual) ->  68,6 % contra 72,6 %  =  -4,0 pp")


if __name__ == "__main__":
    main()
