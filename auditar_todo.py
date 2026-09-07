# -*- coding: utf-8 -*-
"""AUDITOR INDEPENDIENTE DE TODOS LOS NIVELES.

Recalcula desde la cadena CRUDA, sin mirar ningun numero de titular, y
contrasta contra tres cosas distintas:

  1. lo que publica el indicador en su renglon AUDIT
  2. el perfil que el PRODUCTOR de la cadena calculo por su cuenta
  3. su propio resultado con otra resolucion de grilla (para saber cuanto
     del zero gamma es metodo y cuanto es dato)

La regla del proyecto: ningun numero se da por bueno si no lo reprodujo un
camino que no lo miro.
"""
import json, os, io, re, math, sys
from datetime import datetime

APP = os.environ["APPDATA"]
TASA = 0.0375
MULT_INDICE = 100.0
DIAS_MAX = 7.0
PISO_DIAS = 0.02


def fi(x):
    return math.exp(-0.5 * x * x) / math.sqrt(2.0 * math.pi)


def gamma_bs(S, K, T, iv, r=TASA):
    """Identica a GammaBs del indicador, incluida la r dentro de d1."""
    if S <= 0 or K <= 0 or T <= 0 or iv <= 0:
        return 0.0
    v = iv * math.sqrt(T)
    if v <= 0:
        return 0.0
    d1 = (math.log(S / K) + (r + 0.5 * iv * iv) * T) / v
    return fi(d1) / (S * v)


def cargar(raiz="ES"):
    p = os.path.join(APP, "ATAS", "pythiagex-cadena-usada-%s.json" % raiz)
    d = json.load(io.open(p, encoding="utf-8"))
    c = d["cadena"]
    campos = c["campos"].split(",")
    ix = {n: i for i, n in enumerate(campos)}
    dias = [v["dias"] for v in c["vencimientos"]]
    filas = []
    for f in c["filas"]:
        v = int(f[ix["venc"]])
        if v < 0 or v >= len(dias):
            continue
        filas.append(dict(
            K=float(f[ix["strike"]]), dias=dias[v],
            oiC=float(f[ix["oi_call"]] or 0), oiP=float(f[ix["oi_put"]] or 0),
            ivC=float(f[ix["iv_call"]] or 0), ivP=float(f[ix["iv_put"]] or 0),
            volC=float(f[ix["vol_call"]] or 0), volP=float(f[ix["vol_put"]] or 0)))
    return d, c, filas


def gex_total(filas, S, mult=MULT_INDICE, dias_max=DIAS_MAX):
    """GEX por strike, en dolares por 1 % de movimiento."""
    porK = {}
    for f in filas:
        if f["dias"] > dias_max:
            continue
        T = max(f["dias"], PISO_DIAS) / 365.0
        gC = gamma_bs(S, f["K"], T, f["ivC"])
        gP = gamma_bs(S, f["K"], T, f["ivP"])
        g = (gC * f["oiC"] - gP * f["oiP"]) * mult * S * S * 0.01
        if g == 0:
            continue
        porK[f["K"]] = porK.get(f["K"], 0.0) + g
    return porK


def suma_a(filas, x, mult=MULT_INDICE, dias_max=DIAS_MAX):
    """La suma de todo el GEX si el subyacente valiera x."""
    t = 0.0
    for f in filas:
        if f["dias"] > dias_max:
            continue
        T = max(f["dias"], PISO_DIAS) / 365.0
        gC = gamma_bs(x, f["K"], T, f["ivC"])
        gP = gamma_bs(x, f["K"], T, f["ivP"])
        t += (gC * f["oiC"] - gP * f["oiP"]) * mult * x * x * 0.01
    return t


def zero_gamma(filas, S, pasos=60, radio=0.03, mult=MULT_INDICE):
    """El precio donde la suma cruza cero. Mismo metodo que el indicador."""
    lo, hi = S * (1 - radio), S * (1 + radio)
    ant, xAnt = None, 0.0
    for i in range(pasos + 1):
        x = lo + (hi - lo) * i / pasos
        t = suma_a(filas, x, mult)
        if ant is not None and ((ant < 0 <= t) or (ant > 0 >= t)):
            return xAnt + (x - xAnt) * (-ant) / (t - ant) if t != ant else x
        ant, xAnt = t, x
    return float("nan")


def ultimo_audit(raiz_spot_max=12000):
    """El ultimo renglon del indicador para ES."""
    log = os.path.join(APP, "ATAS", "pythiagex-gammavivo.log")
    ult = None
    for l in io.open(log, encoding="utf-8", errors="replace"):
        if "AUDIT" not in l:
            continue
        m = re.search(r"spot_idx=([\d.]+)", l)
        if not m or float(m.group(1)) > raiz_spot_max:
            continue
        ult = l
    if ult is None:
        return {}
    out = {}
    for c in ("spot_idx", "base", "zero", "majorpos", "majorneg", "netgex",
              "strikes", "diasmax", "maxglobal", "minglobal"):
        m = re.search(r"\b" + c + r"=([\d.\-]+)", ult)
        if m:
            try:
                out[c] = float(m.group(1))
            except ValueError:
                pass
    m = re.match(r"(\S+)", ult)
    out["t"] = m.group(1) if m else "?"
    m = re.search(r"origen=(\S+)", ult)
    out["origen"] = m.group(1) if m else "?"
    return out


def main():
    d, c, filas = cargar("ES")
    S = float(c["spot_idx"])
    base = float(d["base"])
    print("=" * 66)
    print("AUDITORIA INDEPENDIENTE  --  ES")
    print("=" * 66)
    print("cadena  : %s  (edad %.1f min)" % (c["ts"], d["edad_min"]))
    print("spot idx: %.2f     base: %+.2f     futuro: %.2f"
          % (S, base, S + base))
    print("filas   : %d   vencimientos: %s"
          % (len(filas), sorted(set(round(f["dias"], 2) for f in filas))[:6]))
    print("se usan  vencimientos hasta %.0f dias" % DIAS_MAX)

    porK = gex_total(filas, S)
    print()
    print("strikes con GEX distinto de cero: %d" % len(porK))

    # ---------------------------------------------------------- muros
    arriba = {k: v for k, v in porK.items() if k > S}
    abajo = {k: v for k, v in porK.items() if k < S}
    mp = max(arriba, key=arriba.get) if arriba else None
    mn = min(abajo, key=abajo.get) if abajo else None
    mpg = max(porK, key=porK.get)
    mng = min(porK, key=porK.get)
    print()
    print("MUROS (indice -> futuro sumando la base)")
    print("   call wall  arriba del precio : %8.2f -> %8.2f   (%.2f mil M)"
          % (mp, mp + base, arriba[mp] / 1e9))
    print("   put  wall  abajo  del precio : %8.2f -> %8.2f   (%.2f mil M)"
          % (mn, mn + base, abajo[mn] / 1e9))
    print("   maximo GLOBAL (sin condicion): %8.2f -> %8.2f   %s"
          % (mpg, mpg + base, "IGUAL" if mpg == mp else "*** DISTINTO ***"))
    print("   minimo GLOBAL (sin condicion): %8.2f -> %8.2f   %s"
          % (mng, mng + base, "IGUAL" if mng == mn else "*** DISTINTO ***"))

    # ---------------------------------------------------------- zero gamma
    print()
    print("ZERO GAMMA recalculado con distintas resoluciones")
    print("   (si el numero cambia mucho con la grilla, el metodo manda mas")
    print("    que el dato y hay que decirlo)")
    zs = []
    for pasos in (60, 120, 240, 480):
        z = zero_gamma(filas, S, pasos=pasos)
        zs.append(z)
        print("      %3d pasos: %8.2f  ->  futuro %8.2f" % (pasos, z, z + base))
    disp = max(zs) - min(zs)
    print("   dispersion por metodo: %.3f puntos" % disp)

    # ---------------------------------------------------------- contra el productor
    print()
    print("CONTRA EL PERFIL QUE CALCULO EL PRODUCTOR DE LA CADENA")
    perf = {p["idx"]: p for p in d.get("perfil", [])}
    comunes = sorted(set(perf) & set(porK))
    print("   strikes en los dos: %d" % len(comunes))
    if comunes:
        difs = []
        for k in comunes:
            mio = porK[k] / 1e6
            suyo = float(perf[k]["gex_M"])
            if abs(suyo) > 1:
                difs.append(abs(mio - suyo) / abs(suyo))
        if difs:
            difs.sort()
            print("   diferencia relativa: mediana %.1f%%  p95 %.1f%%  max %.1f%%"
                  % (100 * difs[len(difs) // 2], 100 * difs[int(len(difs) * .95)],
                     100 * difs[-1]))
        top_mio = [k for k in sorted(porK, key=lambda x: -abs(porK[x]))[:6]]
        top_suyo = [k for k in sorted(perf, key=lambda x: -abs(perf[x]["gex_M"]))[:6]]
        print("   mis 6 strikes mas grandes : %s" % top_mio)
        print("   los 6 del productor       : %s" % top_suyo)
        print("   coinciden: %s" % (set(top_mio) == set(top_suyo)))

    # ---------------------------------------------------------- contra el indicador
    a = ultimo_audit()
    print()
    print("CONTRA LO QUE PUBLICA EL INDICADOR  (%s, origen=%s)"
          % (a.get("t", "?"), a.get("origen", "?")))
    if a:
        print("   nivel        indicador     yo        diferencia")
        pares = [("spot_idx", a.get("spot_idx"), S),
                 ("base", a.get("base"), base),
                 ("zero", a.get("zero"), zs[0] + base),
                 ("majorpos", a.get("majorpos"), mp + base),
                 ("majorneg", a.get("majorneg"), mn + base)]
        for nom, ind, mio in pares:
            if ind is None:
                print("   %-12s   (no publicado)" % nom)
                continue
            print("   %-12s %9.2f %9.2f   %+8.2f" % (nom, ind, mio, mio - ind))
    print()


if __name__ == "__main__":
    main()
