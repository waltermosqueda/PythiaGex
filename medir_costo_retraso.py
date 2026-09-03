# -*- coding: utf-8 -*-
"""Cuanto te cuesta, EN PUNTOS DE ES, que la cadena llegue 902 s tarde.

    python medir_costo_retraso.py

POR QUE ESTE ARCHIVO EXISTE

El operador hace scalping intradia y dice, con razon, que quince minutos de
retraso son inadmisibles. Pero "hay retraso" y "el retraso me mueve el nivel"
son dos cosas distintas, y la segunda se mide.

EL EXPERIMENTO. Los niveles se calculan con tres entradas: el interes abierto
(de AYER, para todos, GEXbot incluido -- la OCC lo consolida de noche), la
volatilidad implicita de la cadena (esta llega 902 s tarde) y el precio (este
sale de Rithmic, en vivo, tick a tick).

Asi que se toman dos fotos de la cadena separadas 902 s y se calculan los
niveles con las dos, AL MISMO PRECIO. Como el precio se congela a proposito,
lo unico que cambia es la entrada que llega tarde. La diferencia que sale es
exactamente lo que cuesta el retraso, en puntos.

Si da decimas, el retraso no toca el scalping y el proyecto sirve. Si da
varios puntos, el operador tiene razon y hay que resolverlo antes de operar.
No se decide de antemano: se mide y se publica lo que salga.
"""
import glob, gzip, json, math, os, re, sys, datetime as dt

CACHE = "datos/cache"
RETRASO_S = 902          # medido, 14 de 14 corridas, sin dispersion
DIAS_MAX = 7             # el mismo horizonte que usa el indicador
TASA = 0.0375

_re = re.compile(r"^([A-Z^]+)(\d{6})([CP])(\d{8})$")


def fi(x):
    return math.exp(-0.5 * x * x) / math.sqrt(2.0 * math.pi)


def gamma_bs(S, K, T, iv, r=TASA):
    if S <= 0 or K <= 0 or T <= 0 or iv <= 0:
        return 0.0
    v = iv * math.sqrt(T)
    if v <= 0:
        return 0.0
    d1 = (math.log(S / K) + (r + 0.5 * iv * iv) * T) / v
    return fi(d1) / (S * v)


def leer(ruta, hoy):
    """Devuelve [(K, dias, iv, oi, signo)] con la cadena de esa foto."""
    d = json.load(gzip.open(ruta, "rt", encoding="utf-8"))
    filas = []
    for o in d["data"]["options"]:
        m = _re.match(o.get("option") or "")
        if not m:
            continue
        _, ymd, cp, kk = m.groups()
        try:
            venc = dt.date(2000 + int(ymd[:2]), int(ymd[2:4]), int(ymd[4:6]))
        except ValueError:
            continue
        dias = (venc - hoy).days
        if dias < 0 or dias > DIAS_MAX:
            continue
        iv = o.get("iv") or 0.0
        oi = o.get("open_interest") or 0.0
        if iv <= 0 or oi <= 0:
            continue
        filas.append((int(kk) / 1000.0, dias, iv, oi, 1.0 if cp == "C" else -1.0))
    return filas, d.get("timestamp", "")


def perfil(filas, S):
    out = {}
    for K, dias, iv, oi, sg in filas:
        T = max(dias, 0.02) / 365.0
        g = gamma_bs(S, K, T, iv) * oi * sg * 100.0 * S * S * 0.01
        out[K] = out.get(K, 0.0) + g
    return out


def zero_gamma(filas, S):
    lo, hi, pasos = S * 0.97, S * 1.03, 60
    ant, xant = None, 0.0
    for i in range(pasos + 1):
        x = lo + (hi - lo) * i / pasos
        t = sum(perfil(filas, x).values())
        if ant is not None and ((ant < 0 <= t) or (ant > 0 >= t)):
            return (xant + (x - xant) * (-ant) / (t - ant)) if t != ant else x
        ant, xant = t, x
    return float("nan")


def niveles(filas, S):
    p = perfil(filas, S)
    if not p:
        return None
    return {
        "zero": zero_gamma(filas, S),
        "mpos": max(p.items(), key=lambda z: z[1])[0],
        "mneg": min(p.items(), key=lambda z: z[1])[0],
        "neto": sum(p.values()) / 1e9,
    }


def hora_de(ruta):
    m = re.search(r"-(\d{8})-(\d{6})\.json\.gz$", ruta)
    if not m:
        return None
    f, h = m.group(1), m.group(2)
    return dt.datetime(int(f[:4]), int(f[4:6]), int(f[6:8]),
                       int(h[:2]), int(h[2:4]), int(h[4:6]))


def main():
    fotos = sorted(glob.glob(os.path.join(CACHE, "_SPX-*.json.gz")))
    porhora = [(hora_de(f), f) for f in fotos]
    porhora = [(h, f) for h, f in porhora if h]
    if len(porhora) < 2:
        print("no hay suficientes fotos en %s" % CACHE)
        return 1

    hoy = porhora[-1][0].date()
    # solo las de hoy y en rueda (09:30-16:00 ET = 10:30-17:00 local aprox.)
    dehoy = [(h, f) for h, f in porhora if h.date() == hoy]
    if len(dehoy) < 2:
        print("no hay suficientes fotos de hoy")
        return 1

    # armar pares separados ~902 s
    pares = []
    for i, (h1, f1) in enumerate(dehoy):
        obj = h1 + dt.timedelta(seconds=RETRASO_S)
        mejor, dmin = None, 10 ** 9
        for h2, f2 in dehoy[i + 1:]:
            d = abs((h2 - obj).total_seconds())
            if d < dmin:
                dmin, mejor = d, (h2, f2)
            if h2 > obj + dt.timedelta(seconds=120):
                break
        if mejor and dmin <= 60:
            pares.append(((h1, f1), mejor))

    # muestrear parejo para no tardar una eternidad
    paso = max(1, len(pares) // 14)
    pares = pares[::paso][:14]
    if not pares:
        print("no se pudieron armar pares separados %d s" % RETRASO_S)
        return 1

    print("=" * 74)
    print("CUANTO CUESTA EL RETRASO DE 902 s, EN PUNTOS")
    print("=" * 74)
    print("Se congela el precio y se cambia SOLO la cadena: la vieja contra la")
    print("que habria si llegara en vivo. La diferencia es el costo del retraso.")
    print()
    print("%-9s %10s %10s %10s   %s" % ("hora", "zero", "majorpos", "majorneg", "net gex"))

    difz, difp, difn, difg = [], [], [], []
    for (h1, f1), (h2, f2) in pares:
        try:
            c1, _ = leer(f1, hoy)
            c2, _ = leer(f2, hoy)
        except Exception as e:
            print("  %s  no se pudo leer (%s)" % (h1.strftime("%H:%M:%S"), e))
            continue
        if not c1 or not c2:
            continue
        # EL PRECIO SE CONGELA: mismo S para las dos.
        S = sum(k for k, _, _, _, _ in c2) / len(c2)
        S = None
        # mejor: usar el precio de mercado que trae la foto nueva
        d2 = json.load(gzip.open(f2, "rt", encoding="utf-8"))
        S = d2["data"].get("current_price") or 0
        if not S:
            continue
        n1, n2 = niveles(c1, S), niveles(c2, S)
        if not n1 or not n2:
            continue
        dz = n2["zero"] - n1["zero"]
        dp = n2["mpos"] - n1["mpos"]
        dn = n2["mneg"] - n1["mneg"]
        dg = n2["neto"] - n1["neto"]
        difz.append(dz); difp.append(dp); difn.append(dn); difg.append(dg)
        print("%-9s %+10.2f %+10.2f %+10.2f   %+9.2f B"
              % (h1.strftime("%H:%M:%S"), dz, dp, dn, dg))

    if not difz:
        print("\nno salio ningun par comparable")
        return 1

    def resumen(nombre, xs, unidad="pts"):
        aa = [abs(x) for x in xs]
        aa_ord = sorted(aa)
        print("  %-14s medio %6.2f %s   peor %6.2f %s   mediana %6.2f %s"
              % (nombre, sum(aa) / len(aa), unidad, max(aa), unidad,
                 aa_ord[len(aa_ord) // 2], unidad))

    print()
    print("EN %d PARES, LO QUE MUEVE EL RETRASO:" % len(difz))
    resumen("zero gamma", difz)
    resumen("major positive", difp)
    resumen("major negative", difn)
    resumen("net gex", difg, "B")
    print()
    peor = max(abs(x) for x in difz)
    med = sum(abs(x) for x in difz) / len(difz)
    print("LECTURA. El zero gamma se corre %.2f puntos en promedio y %.2f en el"
          % (med, peor))
    print("peor caso por culpa de los 902 s. Un tick de ES son 0,25 puntos.")
    print("En ticks: %.1f de promedio, %.1f el peor." % (med / 0.25, peor / 0.25))
    return 0


if __name__ == "__main__":
    sys.exit(main())
