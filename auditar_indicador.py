# -*- coding: utf-8 -*-
"""Contrasta lo que calcula el indicador de ATAS contra una cuenta independiente.

    python auditar_indicador.py

POR QUE ESTE ARCHIVO EXISTE

El indicador reprecia la gamma por su cuenta, en C#, dentro de ATAS. Que
compile y que dibuje no prueba que la cuenta este bien: podria estar dibujando
numeros equivocados con total prolijidad, y eso es peor que no dibujar nada.

Asi que el indicador vuelca lo que calculo a su log, y esto rehace la MISMA
cuenta desde la cadena cruda con la formula escrita de nuevo aca -- sin
importar dominantes.py ni exposicion.py. Si los dos caminos coinciden, el
numero es confiable. Si no, ese desacuerdo ES el hallazgo.

Un auditor que reusa el codigo que audita no audita nada.
"""
import glob, io, json, math, os, re, sys, datetime as dt

LOG = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "pythiagex-gammavivo.log")
FEED = "panel/datos/atas/ES_radar.json"
# La foto que deja el propio indicador. Se prefiere esta: es exactamente
# el archivo que uso para calcular, sin la carrera contra el vigilante.
FOTO = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "pythiagex-cadena-usada.json")


# ------------------------------------------------------------------
# Black-Scholes, escrito de nuevo a proposito
# ------------------------------------------------------------------
def fi(x):
    return math.exp(-0.5 * x * x) / math.sqrt(2.0 * math.pi)


def gamma_bs(S, K, T, iv, r):
    if S <= 0 or K <= 0 or T <= 0 or iv <= 0:
        return 0.0
    v = iv * math.sqrt(T)
    if v <= 0:
        return 0.0
    d1 = (math.log(S / K) + (r + 0.5 * iv * iv) * T) / v
    return fi(d1) / (S * v)


def perfil(filas, dias, S, r, dias_max):
    """GEX por strike al precio S. Devuelve {strike: gex}."""
    out = {}
    for f in filas:
        K, iv_, oc, op, ic, ip = f[0], f[1], f[2], f[3], f[4], f[5]
        if iv_ < 0 or iv_ >= len(dias):
            continue
        d = dias[iv_]
        if d > dias_max:
            continue
        T = max(d, 0.02) / 365.0
        gC = gamma_bs(S, K, T, ic, r)
        gP = gamma_bs(S, K, T, ip, r)
        g = (gC * oc - gP * op) * 100.0 * S * S * 0.01
        out[K] = out.get(K, 0.0) + g
    return out


def neto_a(filas, dias, S, r, dias_max):
    return sum(perfil(filas, dias, S, r, dias_max).values())


def zero_gamma(filas, dias, S, r, dias_max):
    """El precio donde la suma cruza cero. Misma grilla que usa el indicador:
    +/- 3 % en 60 pasos, con interpolacion lineal entre los dos que lo encierran."""
    lo, hi, pasos = S * 0.97, S * 1.03, 60
    ant, xant = None, 0.0
    for i in range(pasos + 1):
        x = lo + (hi - lo) * i / pasos
        t = neto_a(filas, dias, x, r, dias_max)
        if ant is not None and ((ant < 0 <= t) or (ant > 0 >= t)):
            return (xant + (x - xant) * (-ant) / (t - ant)) if t != ant else x
        ant, xant = t, x
    return float("nan")


def leer_volcado():
    """La ultima linea AUDIT que dejo el indicador."""
    if not os.path.exists(LOG):
        return None
    txt = io.open(LOG, encoding="utf-8", errors="replace").read()
    ls = [l for l in txt.splitlines() if "AUDIT" in l]
    if not ls:
        return None
    linea = ls[-1]
    m = dict(re.findall(r"(\w+)=(-?[\d.]+|NaN)", linea))
    out = {k: (float(v) if v != "NaN" else float("nan")) for k, v in m.items()}
    ts = re.search(r"cadenats=(\S+)", linea)
    out["_ts"] = ts.group(1).replace("_", " ") if ts else ""
    return out


def main():
    fuente = FOTO if os.path.exists(FOTO) else FEED
    if not os.path.exists(fuente):
        print("no esta %s -- corre antes: python radar.py SPX" % fuente)
        return 1
    print("cadena leida de: %s" % ("la foto del indicador" if fuente == FOTO else "el archivo del feed"))
    d = json.load(io.open(fuente, encoding="utf-8"))
    cad = d.get("cadena")
    if not cad:
        print("el archivo no trae la cadena")
        return 1

    dias = [v["dias"] for v in cad["vencimientos"]]
    filas = cad["filas"]

    v = leer_volcado()
    if not v:
        print("todavia no hay linea AUDIT en el log del indicador.")
        print("Abri ATAS con el indicador puesto y espera un minuto.")
        print("Log: %s" % LOG)
        return 2

    # NO COMPARAR PERALES CON MANZANAS.
    #
    # El indicador baja la cadena en un momento y este auditor lee el archivo
    # despues. Si radar.py lo regenero en el medio, los interes abierto
    # cambiaron y la suma no puede coincidir aunque los dos calculos esten
    # perfectos. La primera corrida dio 0,81 B de diferencia por esto.
    #
    # Los niveles aguantan (son un argmax, robusto a cambios chicos); la suma
    # no. Asi que se compara la huella de la cadena antes que nada.
    ts_archivo = (cad.get("ts") or "")
    ts_ind = v.get("_ts", "")
    nfil_ind = int(v.get("cadenafilas") or 0)
    if ts_ind and ts_archivo and ts_ind != ts_archivo:
        print("NO COMPARABLE: el indicador uso la cadena de %s y el archivo de disco"
              " es de %s." % (ts_ind, ts_archivo))
        print("Volve a correr el auditor sin regenerar el feed en el medio.")
        return 4
    if nfil_ind and nfil_ind != len(filas):
        print("NO COMPARABLE: el indicador uso %d filas y el archivo tiene %d."
              % (nfil_ind, len(filas)))
        return 4

    S = v["spot_idx"]
    r = 0.0375
    dmax = int(v.get("diasmax") or 7)

    p = perfil(filas, dias, S, r, dmax)
    if not p:
        print("la cadena no dejo ningun strike con ese horizonte")
        return 1
    neto = sum(p.values()) / 1e9
    mp = max(p.items(), key=lambda z: z[1])[0]
    mn = min(p.items(), key=lambda z: z[1])[0]
    zg = zero_gamma(filas, dias, S, r, dmax)

    print("=" * 68)
    print("AUDITORIA DEL INDICADOR   (spot indice %.4f, horizonte %d dias)" % (S, dmax))
    print("=" * 68)
    print("%-16s %16s %16s   %s" % ("", "indicador C#", "auditor Python", "control"))

    filas_out = [
        ("strikes", v.get("strikes"), float(len(p)), 0.0),
        ("zero gamma", v.get("zero"), zg, 0.25),
        ("major positive", v.get("majorpos"), mp, 0.01),
        ("major negative", v.get("majorneg"), mn, 0.01),
        ("net gex (B)", v.get("netgex"), neto, 0.02),
    ]
    fallas = 0
    for nombre, a, b, tol in filas_out:
        if a is None or b is None:
            print("%-16s %16s %16s   sin dato" % (nombre, a, b)); fallas += 1; continue
        dif = abs(a - b)
        ok = dif <= max(tol, abs(b) * 0.005)
        if not ok:
            fallas += 1
        print("%-16s %16.4f %16.4f   %s (%.4f)"
              % (nombre, a, b, "ok" if ok else "NO COINCIDE", dif))

    print()
    if fallas == 0:
        print("PASA: los dos caminos dan lo mismo.")
    else:
        print("FALLA en %d control(es). El indicador no esta calculando lo mismo" % fallas)
        print("que la cuenta independiente. NO usarlo para operar hasta resolverlo.")
    return 0 if fallas == 0 else 3


if __name__ == "__main__":
    sys.exit(main())
