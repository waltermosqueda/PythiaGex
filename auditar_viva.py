# -*- coding: utf-8 -*-
"""Audita la cadena EN VIVO de Rithmic, que es el camino que hoy usa ES.

    python auditar_viva.py [ES|NQ]

POR QUE ESTE ARCHIVO EXISTE

auditar_indicador.py rehace la cuenta de la cadena de CBOE, con Black-Scholes
sobre el indice. Pero desde que ES toma la cadena viva de Rithmic el calculo va
por otro lado: strikes en precio de FUTURO, volatilidad despejada del punto
medio de las puntas y Black-76. O sea que el camino que de verdad se esta
usando NO se estaba auditando, y un auditor que no cubre el camino en uso no
sirve de nada.

Igual que el otro, la formula se escribe de nuevo aca a proposito. Un auditor
que importa el codigo que audita no audita nada.
"""
import glob, io, json, math, os, re, sys, datetime as dt

R = 0.0375
LOG = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "pythiagex-gammavivo.log")


def fi(x):
    return math.exp(-0.5 * x * x) / math.sqrt(2.0 * math.pi)


def gamma76(F, K, T, s):
    """Gamma de una opcion SOBRE FUTURO. No lleva el termino de la tasa en d1
    -- eso es lo que la distingue de Black-Scholes sobre contado."""
    if F <= 0 or K <= 0 or T <= 0 or s <= 0:
        return 0.0
    v = s * math.sqrt(T)
    d1 = (math.log(F / K) + 0.5 * s * s * T) / v
    return math.exp(-R * T) * fi(d1) / (F * v)


def perfil(filas, F, horizonte):
    """{strike: gex} al precio F. filas = [K, dias, esCall, OI, IV, bid, ask]"""
    out = {}
    for K, dias, esCall, oi, iv, _b, _a in filas:
        if dias > horizonte or oi <= 0 or iv <= 0:
            continue
        T = max(dias, 0.02) / 365.0
        g = gamma76(F, K, T, iv) * oi * (1.0 if esCall else -1.0) * 100.0 * F * F * 0.01
        out[K] = out.get(K, 0.0) + g
    return out


def zero_gamma(filas, F, horizonte):
    lo, hi, pasos = F * 0.97, F * 1.03, 60
    ant, xant = None, 0.0
    for i in range(pasos + 1):
        x = lo + (hi - lo) * i / pasos
        t = sum(perfil(filas, x, horizonte).values())
        if ant is not None and ((ant < 0 <= t) or (ant > 0 >= t)):
            return (xant + (x - xant) * (-ant) / (t - ant)) if t != ant else x
        ant, xant = t, x
    return float("nan")


def volcado(raiz):
    if not os.path.exists(LOG):
        return None
    ls = [l for l in io.open(LOG, encoding="utf-8", errors="replace").read().splitlines()
          if "AUDIT" in l and "no_hace_falta" in l]
    if raiz in ("NQ", "MNQ"):
        ls = [l for l in ls if re.search(r"spot_idx=(1|2|3)\d{4}", l)]
    elif raiz in ("ES", "MES"):
        ls = [l for l in ls if re.search(r"spot_idx=\d{4}\.", l)]
    if not ls:
        return None
    m = dict(re.findall(r"(\w+)=(-?[\d.]+|NaN)", ls[-1]))
    o = {k: (float(v) if v != "NaN" else float("nan")) for k, v in m.items()}
    ts = re.search(r"cadenats=(\S+)", ls[-1])
    o["_ts"] = ts.group(1).replace("_", " ") if ts else ""
    return o


def main():
    raiz = (sys.argv[1].upper() if len(sys.argv) > 1 else "ES")
    if raiz in ("MES",): raiz = "ES"
    if raiz in ("MNQ",): raiz = "NQ"
    foto = os.path.join(os.environ.get("APPDATA", ""), "ATAS",
                        "pythiagex-cadena-viva-%s.json" % raiz)
    if not os.path.exists(foto):
        print("no hay foto de cadena viva para %s." % raiz)
        print("El indicador la escribe solo cuando la cadena viva esta activa.")
        return 2
    d = json.load(io.open(foto, encoding="utf-8"))
    filas = d["filas"]
    v = volcado(raiz)
    if not v:
        print("no hay renglon AUDIT de cadena viva para %s todavia." % raiz)
        return 2

    # MISMA CADENA O NO SE COMPARA. El indicador reescribe la foto cada 20 s;
    # si entre su calculo y esta lectura cambio, la suma no puede coincidir
    # aunque los dos caminos esten perfectos.
    if v["_ts"] and d.get("ts") and v["_ts"] != d["ts"].replace("_", " "):
        print("NO COMPARABLE: el indicador uso la cadena de %s y la foto es de %s."
              % (v["_ts"], d["ts"]))
        return 4

    F = v["spot_idx"]           # con cadena de futuros, spot_idx ES el futuro
    dmax = int(v.get("diasmax") or 7)
    # el mismo horizonte adaptativo que usa el indicador
    masCerca = min(f[1] for f in filas) if filas else dmax
    horizonte = max(dmax, masCerca)

    p = perfil(filas, F, horizonte)
    if not p:
        print("la cadena no dejo ningun strike con ese horizonte")
        return 1
    neto = sum(p.values()) / 1e9
    mp = max(p.items(), key=lambda z: z[1])[0]
    mn = min(p.items(), key=lambda z: z[1])[0]
    zg = zero_gamma(filas, F, horizonte)

    print("=" * 70)
    print("AUDITORIA DE LA CADENA EN VIVO  (%s, futuro %.4f, horizonte %.0f d)"
          % (raiz, F, horizonte))
    print("=" * 70)
    print("%-16s %16s %16s   %s" % ("", "indicador C#", "auditor Python", "control"))
    fallas = 0
    for nombre, a, b, tol in [
        ("strikes",        v.get("strikes"),  float(len(p)), 0.0),
        ("zero gamma",     v.get("zero"),     zg,            0.25),
        ("major positive", v.get("majorpos"), mp,            0.01),
        ("major negative", v.get("majorneg"), mn,            0.01),
        ("net gex (B)",    v.get("netgex"),   neto,          0.02),
    ]:
        # LOS DOS SIN DATO ES ACUERDO, NO FALLA.
        #
        # Cuando la suma no cruza cero dentro de la grilla, el indicador
        # devuelve 0 y este auditor devuelve NaN: dicen LO MISMO, que no hay
        # cruce. Contarlo como desacuerdo hacia que la auditoria de NQ fallara
        # por un caso en que los dos caminos coincidian perfectamente.
        sinA = a is None or a == 0 or (isinstance(a, float) and math.isnan(a))
        sinB = b is None or (isinstance(b, float) and math.isnan(b))
        if sinA and sinB:
            print("%-16s %16s %16s   ok (los dos: no hay cruce)" % (nombre, "--", "--")); continue
        if sinA != sinB:
            print("%-16s %16s %16s   NO COINCIDE (uno tiene dato y el otro no)"
                  % (nombre, a, b)); fallas += 1; continue
        dif = abs(a - b)
        ok = dif <= max(tol, abs(b) * 0.005)
        if not ok: fallas += 1
        print("%-16s %16.4f %16.4f   %s (%.4f)" % (nombre, a, b, "ok" if ok else "NO COINCIDE", dif))

    print()
    print("PASA: los dos caminos dan lo mismo." if fallas == 0 else
          "FALLA en %d control(es). NO usar estos numeros." % fallas)
    return 0 if fallas == 0 else 3


if __name__ == "__main__":
    sys.exit(main())
