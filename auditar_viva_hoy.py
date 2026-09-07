# -*- coding: utf-8 -*-
"""AUDITOR DE LA CADENA VIVA (la que el indicador esta usando de verdad).

Black-76 porque son opciones sobre un FUTURO, multiplicador 50 porque es ES.
No mira ningun numero de titular: recalcula y despues compara.
"""
import json, os, io, re, math

APP = os.environ["APPDATA"]
TASA = 0.0375
MULT_ES = 50.0
DIAS_MAX = 7.0
PISO_DIAS = 0.02


def fi(x):
    return math.exp(-0.5 * x * x) / math.sqrt(2.0 * math.pi)


def gamma76(F, K, T, iv, r=TASA):
    if F <= 0 or K <= 0 or T <= 0 or iv <= 0:
        return 0.0
    v = iv * math.sqrt(T)
    d1 = (math.log(F / K) + 0.5 * v * v) / v
    return math.exp(-r * T) * fi(d1) / (F * v)


def cargar(raiz="ES"):
    p = os.path.join(APP, "ATAS", "pythiagex-cadena-viva-%s.json" % raiz)
    d = json.load(io.open(p, encoding="utf-8"))
    filas = []
    for K, dias, esCall, oi, iv, bid, ask, vol in d["filas"]:
        filas.append(dict(K=float(K), dias=float(dias), call=bool(esCall),
                          oi=float(oi), iv=float(iv), vol=float(vol)))
    return d, filas


def gex_por_strike(filas, F, mult=MULT_ES, dias_max=DIAS_MAX):
    porK = {}
    for f in filas:
        if f["dias"] > dias_max or f["oi"] <= 0 or f["iv"] <= 0:
            continue
        T = max(f["dias"], PISO_DIAS) / 365.0
        g = gamma76(F, f["K"], T, f["iv"]) * f["oi"] * mult * F * F * 0.01
        porK[f["K"]] = porK.get(f["K"], 0.0) + (g if f["call"] else -g)
    return porK


def suma_a(filas, x, mult=MULT_ES, dias_max=DIAS_MAX):
    t = 0.0
    for f in filas:
        if f["dias"] > dias_max or f["oi"] <= 0 or f["iv"] <= 0:
            continue
        T = max(f["dias"], PISO_DIAS) / 365.0
        g = gamma76(x, f["K"], T, f["iv"]) * f["oi"] * mult * x * x * 0.01
        t += g if f["call"] else -g
    return t


def zero_gamma(filas, F, pasos=60, radio=0.03):
    lo, hi = F * (1 - radio), F * (1 + radio)
    ant, xAnt = None, 0.0
    for i in range(pasos + 1):
        x = lo + (hi - lo) * i / pasos
        t = suma_a(filas, x)
        if ant is not None and ((ant < 0 <= t) or (ant > 0 >= t)):
            return xAnt + (x - xAnt) * (-ant) / (t - ant) if t != ant else x
        ant, xAnt = t, x
    return float("nan")


def audit_mas_cercano(sello):
    """El renglon del indicador mas cercano en el tiempo al sello de la cadena."""
    log = os.path.join(APP, "ATAS", "pythiagex-gammavivo.log")
    mejor, dmin = None, 1e9
    obj = sello.replace("_", "T")
    for l in io.open(log, encoding="utf-8", errors="replace"):
        if "AUDIT" not in l or "cadenats=" not in l:
            continue
        m = re.search(r"cadenats=(\S+)", l)
        sp = re.search(r"spot_idx=([\d.]+)", l)
        if not m or not sp or float(sp.group(1)) > 12000:
            continue
        if m.group(1) == sello:
            return l
        mejor = mejor or l
    return mejor


def main():
    d, filas = cargar("ES")
    F = float(d["futuro"])
    print("=" * 68)
    print("AUDITORIA DE LA CADENA VIVA  --  ES  (Black-76, multiplicador 50)")
    print("=" * 68)
    print("sello de la cadena: %s" % d["ts"])
    print("futuro            : %.3f" % F)
    vencs = sorted(set(round(f["dias"], 4) for f in filas))
    print("filas: %d   vencimientos: %s" % (len(filas), vencs[:8]))
    usables = [f for f in filas if f["dias"] <= DIAS_MAX and f["oi"] > 0 and f["iv"] > 0]
    print("filas usables (<=%.0f dias, con OI e IV): %d" % (DIAS_MAX, len(usables)))

    porK = gex_por_strike(filas, F)
    print("strikes con GEX: %d" % len(porK))
    if not porK:
        print("sin datos suficientes"); return

    arriba = {k: v for k, v in porK.items() if k > F}
    abajo = {k: v for k, v in porK.items() if k < F}
    mp = max(arriba, key=arriba.get) if arriba else None
    mn = min(abajo, key=abajo.get) if abajo else None
    mpg = max(porK, key=porK.get)
    mng = min(porK, key=porK.get)
    neto = sum(porK.values())

    print()
    print("MUROS")
    print("   call wall (arriba del precio): %8.2f   %+7.3f mil M" % (mp, arriba[mp] / 1e9))
    print("   put  wall (abajo  del precio): %8.2f   %+7.3f mil M" % (mn, abajo[mn] / 1e9))
    print("   maximo global : %8.2f  %s" % (mpg, "IGUAL" if mpg == mp else "*** DISTINTO ***"))
    print("   minimo global : %8.2f  %s" % (mng, "IGUAL" if mng == mn else "*** DISTINTO ***"))
    print("   neto          : %+.3f mil M" % (neto / 1e9))

    print()
    print("ZERO GAMMA con distintas resoluciones de grilla")
    zs = []
    for pasos in (60, 120, 240, 480):
        z = zero_gamma(filas, F, pasos=pasos)
        zs.append(z)
        print("   %3d pasos: %8.3f" % (pasos, z))
    print("   dispersion por METODO: %.3f puntos" % (max(zs) - min(zs)))

    print()
    print("LOS 8 STRIKES MAS GRANDES EN VALOR ABSOLUTO")
    for k in sorted(porK, key=lambda x: -abs(porK[x]))[:8]:
        lado = "arriba" if k > F else "abajo "
        print("   %8.0f  %s  %+8.3f mil M" % (k, lado, porK[k] / 1e9))

    l = audit_mas_cercano(d["ts"])
    print()
    if l:
        campos = {}
        for c in ("spot_idx", "base", "zero", "majorpos", "majorneg", "netgex",
                  "strikes", "cadenats"):
            m = re.search(r"\b" + c + r"=([\d.\-]+)", l)
            if m:
                campos[c] = m.group(1)
        m = re.match(r"(\S+)", l)
        print("CONTRA EL INDICADOR   renglon %s   cadenats=%s"
              % (m.group(1) if m else "?", campos.get("cadenats", "?")))
        mismo = campos.get("cadenats") == d["ts"]
        print("   %s la MISMA cadena que audite" % ("es" if mismo else "NO es"))
        if not mismo:
            print("   -> la comparacion de abajo es orientativa, no exacta")
        print()
        print("   nivel        indicador       yo      diferencia")
        for nom, mio in (("spot_idx", F), ("zero", zs[0]),
                         ("majorpos", mp), ("majorneg", mn)):
            if nom in campos:
                ind = float(campos[nom])
                print("   %-11s %9.2f %9.2f   %+8.2f" % (nom, ind, mio, mio - ind))
        if "strikes" in campos:
            print("   %-11s %9s %9d" % ("strikes", campos["strikes"], len(porK)))
    print()


if __name__ == "__main__":
    main()
