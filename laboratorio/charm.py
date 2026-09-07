# -*- coding: utf-8 -*-
"""CHARM: EL FLUJO QUE MANDA EL RELOJ.

QUE ES
Charm es cuanto se le corre el delta a la mesa por el MERO PASO DEL TIEMPO,
sin que el precio se mueva. Hacia el cierre, el delta de un 0DTE se desarma
solo -- las opciones que iban a expirar sin valor pierden su delta y las que
van a expirar con valor lo llevan a uno -- y la mesa tiene que deshacer o
agregar cobertura si o si.

Es el unico flujo que se puede anticipar POR RELOJ y no por precio.

COMO SE CALCULA ACA
Numericamente, no con formula cerrada: se computa el delta a T y a T menos una
hora, con el MISMO precio y la MISMA volatilidad, y se resta. Se hace asi a
proposito -- la formula analitica de charm tiene tres convenciones de signo
distintas dando vueltas y un error de signo pasaria desapercibido.

El delta se valido primero contra el que sirve CBOE: con r = 0,0375 y un
dividendo de 1,2 % el error mediano baja a 0,0036. Sin dividendo era 0,0072.
"""
import sys, os, math, statistics as st

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cargar
import puntuar

R_TASA = 0.0375
Q_DIV = 0.012          # dividendo del indice, ajustado contra el delta de CBOE
MULT = 100.0
UNA_HORA = 1.0 / (365.0 * 24.0)


def N(x):
    return 0.5 * (1.0 + math.erf(x / math.sqrt(2.0)))


def delta_bs(S, K, T, iv, call):
    if S <= 0 or K <= 0 or T <= 0 or iv <= 0:
        return None
    v = iv * math.sqrt(T)
    d1 = (math.log(S / K) + (R_TASA - Q_DIV + 0.5 * iv * iv) * T) / v
    dq = math.exp(-Q_DIV * T)
    return dq * N(d1) if call else dq * (N(d1) - 1.0)


def charm_por_hora(S, K, T, iv, call):
    """Cuanto cambia el delta si pasa una hora y nada mas cambia."""
    if T <= UNA_HORA:
        return 0.0
    a = delta_bs(S, K, T, iv, call)
    b = delta_bs(S, K, T - UNA_HORA, iv, call)
    if a is None or b is None:
        return 0.0
    return b - a


def perfiles(f):
    """Devuelve tres mapas por strike: gamma, charm firmado y |charm|."""
    S = f["spot"]
    gex, cex, cabs = {}, {}, {}
    for x in f["filas"]:
        if x["oi"] <= 0 or x["iv"] <= 0:
            continue
        T = max(x["dias"], 1.0 / 1440.0) / 365.0
        ch = charm_por_hora(S, x["K"], T, x["iv"], x["call"])
        # delta en dolares que la mesa tiene que rehedgear por hora
        v = ch * x["oi"] * MULT * S
        cex[x["K"]] = cex.get(x["K"], 0.0) + (v if x["call"] else -v)
        cabs[x["K"]] = cabs.get(x["K"], 0.0) + abs(v)
        g = x["gamma"] * x["oi"] * MULT * S * S * 0.01
        gex[x["K"]] = gex.get(x["K"], 0.0) + (g if x["call"] else -g)
    return gex, cex, cabs


def topn(d, n=6, piso=0.15):
    if not d:
        return []
    mx = max(abs(v) for v in d.values()) or 1.0
    xs = [(k, v) for k, v in d.items() if abs(v) >= piso * mx]
    xs.sort(key=lambda kv: -abs(kv[1]))
    return [k for k, _ in xs[:n]]


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
            f["g"], f["c"], f["ca"] = perfiles(f)
            fotos.append(f)
    print("fotos dentro de la rueda: %d" % len(fotos))
    if len(fotos) < 20:
        print("muestra insuficiente"); return

    cands = {
        "Q charm firmado":      lambda f: topn(f["c"]),
        "R charm en magnitud":  lambda f: topn(f["ca"]),
        "S charm solo 0DTE":    lambda f: topn(f["c0"]),
    }
    # charm de lo que vence hoy, aparte
    for f in fotos:
        S = f["spot"]
        d = {}
        for x in f["filas"]:
            if x["oi"] <= 0 or x["iv"] <= 0 or x["dias"] >= 1.0:
                continue
            T = max(x["dias"], 1.0 / 1440.0) / 365.0
            ch = charm_por_hora(S, x["K"], T, x["iv"], x["call"])
            v = ch * x["oi"] * MULT * S
            d[x["K"]] = d.get(x["K"], 0.0) + (v if x["call"] else -v)
        f["c0"] = d

    print()
    print("formula                      niv  beta   toques  freno  placebo  ventaja")
    px = [ph[f["h"]]["c"] for f in fotos]
    for nom, fn in cands.items():
        niveles = [fn(f) for f in fotos]
        cerc = [min(ns, key=lambda k: abs(k - f["spot"])) if ns else None
                for f, ns in zip(fotos, niveles)]
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
            for f, ns in zip(fotos, niveles):
                for L in ns:
                    r = puntuar.tocar(velas, ph, L + corr, f["h"], f["h"] + puntuar.VENTANA)
                    if r:
                        t += 1; ok += r[0]
            return t, ok
        t0, ok0 = evaluar(0.0)
        tp = okp = 0
        for c in puntuar.PLACEBOS:
            a, b = evaluar(c)
            tp += a; okp += b
        nn = st.fmean(len(x) for x in niveles)
        if t0 < 10:
            print("  %-27s %3.0f  %5.2f    %4d   muestra chica" % (nom, nn, beta, t0)); continue
        tasa = 100.0 * ok0 / t0
        tpp = 100.0 * okp / tp if tp else float("nan")
        print("  %-27s %3.0f  %5.2f    %4d  %4.1f%%   %4.1f%%   %+5.1f pp"
              % (nom, nn, beta, t0, tasa, tpp, tasa - tpp))

    # ---- el charm agregado, que no es un nivel sino un flujo
    print()
    print("CHARM AGREGADO: delta en dolares que la mesa debe rehedgear por hora")
    print("  hora ET      todo         solo 0DTE      signo")
    for f in fotos[::max(1, len(fotos) // 10)]:
        tot = sum(f["c"].values()) / 1e9
        hoy = sum(f["c0"].values()) / 1e9
        print("   %02d:%02d     %+8.2f B     %+8.2f B     %s"
              % (f["h"] // 60, f["h"] % 60, tot, hoy,
                 "compra" if tot > 0 else "venta"))


if __name__ == "__main__":
    main()
