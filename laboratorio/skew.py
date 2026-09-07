# -*- coding: utf-8 -*-
"""NIVELES A PARTIR DE LA FORMA DE LA VOLATILIDAD.

DE DONDE SALE
De un video donde un trader usa insiderfinance.io sobre QQQ. Lo util no es su
analisis -- dice el mismo que es su primera semana -- sino las metricas que
muestra el tablero y que nosotros no calculamos:

  - la sonrisa de IV por strike, con calls y puts SEPARADAS
  - "10D SKEW +3,7 pts (crash-hedge tilt)": IV de la put 10-delta menos la de
    la call 10-delta
  - "SKEW SLOPE -0,8 IV pts por 1 % de strike": la pendiente de esa sonrisa
  - movimiento esperado por vencimiento: 0DTE 1,0 %, semanal 2,1 %, mensual 4,8 %
  - Total GEX 7,8 B contra Net GEX 196,2 M: 97,5 % de cancelacion

POR QUE ESTA VALE LA PENA PROBARLA
Todo lo que fallo hasta ahora tenia beta cerca de 1: eran ecos del precio. La
forma de la volatilidad es otra cosa -- es una propiedad de la superficie de
opciones, no de donde esta el precio. Puede ser el primer candidato que no sea
un eco.

Se prueba con el mismo juez y el mismo placebo que todo lo demas.
"""
import sys, os, math, statistics as st

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cargar
import puntuar


def curva(f, dias_max=1.5):
    """IV de call y de put por strike, para el vencimiento mas cercano."""
    c, p = {}, {}
    for x in f["filas"]:
        if x["iv"] <= 0 or x["dias"] > dias_max:
            continue
        (c if x["call"] else p)[x["K"]] = x["iv"]
    return c, p


def iv_atm(c, p, S):
    """IV al dinero: promedio de call y put del strike mas cercano al spot."""
    ks = sorted(set(c) & set(p))
    if not ks:
        return None
    k = min(ks, key=lambda z: abs(z - S))
    return (c[k] + p[k]) / 2.0


def topn(d, n=6, piso=0.15):
    if not d:
        return []
    mx = max(abs(v) for v in d.values()) or 1.0
    xs = [(k, v) for k, v in d.items() if abs(v) >= piso * mx]
    xs.sort(key=lambda kv: -abs(kv[1]))
    return [k for k, _ in xs[:n]]


def candidatas(f):
    """Cada formula devuelve una lista de precios."""
    S = f["spot"]
    c, p = curva(f, 1.5)
    comunes = sorted(set(c) & set(p))
    if len(comunes) < 10:
        return {}
    atm = iv_atm(c, p, S) or 0.0

    # T: la call paga mas que la put -> la gente puja por arriba en ese strike
    dif = {k: c[k] - p[k] for k in comunes}
    # U: cuanto se aparta la IV del strike respecto de la del dinero
    prima = {k: (c[k] + p[k]) / 2.0 - atm for k in comunes}
    # V: el quiebre de la pendiente -- donde la sonrisa cambia de caracter
    quiebre = {}
    for i in range(1, len(comunes) - 1):
        a, b, d = comunes[i - 1], comunes[i], comunes[i + 1]
        if d == a:
            continue
        m1 = (prima[b] - prima[a]) / max(1e-9, b - a)
        m2 = (prima[d] - prima[b]) / max(1e-9, d - b)
        quiebre[b] = m2 - m1
    return {
        "T call IV - put IV":      topn(dif),
        "U prima de IV vs ATM":    topn(prima),
        "V quiebre de la sonrisa": topn(quiebre),
    }


def main(dia="20260903", dia_iso="2026-09-03", cada=3):
    velas = cargar.velas(dia_iso)
    ph = {v["hora"]: v for v in velas}
    hmin, hmax = min(ph), max(ph)

    fotos = []
    for r in cargar.fotos(dia)[::cada]:
        f = cargar.leer_foto(r, dias_max=7.0, radio=150)
        if not f:
            continue
        h = f["t"].hour * 60 + f["t"].minute + puntuar.OFF_MIN
        if hmin <= h <= hmax - puntuar.VENTANA:
            f["h"] = h
            f["cand"] = candidatas(f)
            if f["cand"]:
                fotos.append(f)
    print("fotos usables: %d" % len(fotos))
    if len(fotos) < 20:
        print("muestra insuficiente"); return

    # contexto: skew agregado, para reportar
    print()
    print("SKEW A LO LARGO DE LA RUEDA (vencimiento mas cercano)")
    print("  hora ET   IV al dinero   call-put al dinero   pendiente")
    for f in fotos[::max(1, len(fotos) // 8)]:
        c, p = curva(f, 1.5)
        com = sorted(set(c) & set(p))
        if len(com) < 10:
            continue
        S = f["spot"]
        atm = iv_atm(c, p, S)
        k0 = min(com, key=lambda z: abs(z - S))
        cerca = [k for k in com if abs(k - S) / S <= 0.02]
        pend = float("nan")
        if len(cerca) > 4:
            xs = [(k - S) / S * 100 for k in cerca]
            ys = [(c[k] + p[k]) / 2.0 * 100 for k in cerca]
            mx_, my = st.fmean(xs), st.fmean(ys)
            den = sum((x - mx_) ** 2 for x in xs)
            if den:
                pend = sum((x - mx_) * (y - my) for x, y in zip(xs, ys)) / den
        print("   %02d:%02d      %6.2f%%          %+6.2f pts        %+5.2f"
              % (f["h"] // 60, f["h"] % 60, atm * 100,
                 (c[k0] - p[k0]) * 100, pend))

    print()
    print("formula                      niv  beta   toques  freno  placebo  ventaja")
    px = [ph[f["h"]]["c"] for f in fotos]
    nombres = sorted(fotos[0]["cand"].keys())
    for nom in nombres:
        niveles = [f["cand"].get(nom, []) for f in fotos]
        cerc = [min(ns, key=lambda k: abs(k - f["spot"])) if ns else None
                for f, ns in zip(fotos, niveles)]
        pares = [(a, b) for a, b in zip(px, cerc) if b]
        beta = float("nan")
        if len(pares) > 20:
            A = [a for a, _ in pares]; B = [b for _, b in pares]
            ma, mb = st.fmean(A), st.fmean(B)
            den = sum((x - ma) ** 2 for x in A)
            if den:
                beta = sum((x - ma) * (y - mb) for x, y in zip(A, B)) / den

        def evaluar(corr):
            t = ok = 0
            for f, ns in zip(fotos, niveles):
                for L in ns:
                    r = puntuar.tocar(velas, ph, L + corr, f["h"], f["h"] + puntuar.VENTANA)
                    if r:
                        t += 1; ok += r[0]
            return t, ok
        t0, ok0 = evaluar(0.0)
        tp = okp = 0
        for cc in puntuar.PLACEBOS:
            a, b = evaluar(cc)
            tp += a; okp += b
        nn = st.fmean(len(x) for x in niveles)
        if t0 < 10:
            print("  %-27s %3.0f  %5.2f    %4d   muestra chica" % (nom, nn, beta, t0)); continue
        tasa = 100.0 * ok0 / t0
        tpp = 100.0 * okp / tp if tp else float("nan")
        print("  %-27s %3.0f  %5.2f    %4d  %4.1f%%   %4.1f%%   %+5.1f pp"
              % (nom, nn, beta, t0, tasa, tpp, tasa - tpp))


if __name__ == "__main__":
    main()
