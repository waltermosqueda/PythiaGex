# -*- coding: utf-8 -*-
"""EL JUEZ. Puntua cada formula con la misma vara y contra el mismo placebo.

ALINEACION -- esto es lo que mas facil se rompe
La cadena de CBOE llega ~15 minutos tarde. Medido dos veces por caminos
distintos: con el reloj (902 s) y cruzando el precio de las fotos contra las
velas historicas (el ajuste salta de 23 % a 79 % corriendo 15 min). Asi que la
hora EFECTIVA de una foto es su sello menos 4 h (UTC->ET) menos 15 min.

Si esto estuviera mal, se compararian niveles contra precios de otro momento y
todo el resultado seria ruido con forma de conclusion.
"""
import sys, os, statistics as st

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cargar
import formulas

OFF_MIN = -4 * 60 - 15      # UTC -> ET, y el retraso de la cadena
TOL = 2.0                   # que tan cerca cuenta como toque, en puntos
LEJOS = 8.0                 # antes tenia que estar al menos a esto
VENTANA = 20                # minutos que se miran despues del toque
PLACEBOS = (-31.0, -19.0, -11.0, 11.0, 19.0, 31.0)


def hora_efectiva(f):
    return f["t"].hour * 60 + f["t"].minute + OFF_MIN


def tocar(velas, ph, nivel, desde, hasta):
    """Busca el primer toque del nivel entre dos minutos, y lo puntua.

    Toque = una vela cuyo rango contiene al nivel, habiendo estado lejos.
    Se puntua comparando cuanto se ALEJO contra cuanto lo ATRAVESO despues.
    """
    lejos_lado = None
    for m in range(desde, hasta):
        v = ph.get(m)
        if not v:
            continue
        if v["l"] - nivel > LEJOS:
            lejos_lado = 1          # venia de arriba
            continue
        if nivel - v["h"] > LEJOS:
            lejos_lado = -1         # venia de abajo
            continue
        if lejos_lado is None:
            continue
        if not (v["l"] - TOL <= nivel <= v["h"] + TOL):
            continue
        # toque
        aleja = cruza = 0.0
        for j in range(m, m + VENTANA):
            w = ph.get(j)
            if not w:
                continue
            if lejos_lado > 0:
                aleja = max(aleja, w["h"] - nivel)
                cruza = max(cruza, nivel - w["l"])
            else:
                aleja = max(aleja, nivel - w["l"])
                cruza = max(cruza, w["h"] - nivel)
        return (1 if aleja > cruza else 0, aleja - cruza)
    return None


def correr(dia_cache, dia_iso, cada=1, n_niveles=6):
    rutas = cargar.fotos(dia_cache)
    velas = cargar.velas(dia_iso)
    if not rutas or not velas:
        return None
    ph = {v["hora"]: v for v in velas}
    hmin, hmax = min(ph), max(ph)

    fotos = []
    for r in rutas[::cada]:
        f = cargar.leer_foto(r)
        if not f:
            continue
        h = hora_efectiva(f)
        if hmin <= h <= hmax - VENTANA:
            f["h"] = h
            fotos.append(f)
    if len(fotos) < 10:
        return None

    res = {}
    px = [ph[f["h"]]["c"] for f in fotos]
    for nombre, fn in sorted(formulas.CANDIDATAS.items()):
        niveles = []
        for f in fotos:
            try:
                niveles.append([x for x in fn(f, n_niveles) if x])
            except Exception:
                niveles.append([])
        # --- eco: cuanto sigue al precio el nivel mas cercano
        cerc = []
        for f, ns in zip(fotos, niveles):
            if ns:
                cerc.append(min(ns, key=lambda k: abs(k - f["spot"])))
            else:
                cerc.append(None)
        pares = [(p, c) for p, c in zip(px, cerc) if c]
        beta = float("nan")
        if len(pares) > 20:
            A = [a for a, _ in pares]; B = [b for _, b in pares]
            ma, mb = st.fmean(A), st.fmean(B)
            den = sum((x - ma) ** 2 for x in A)
            if den:
                beta = sum((x - ma) * (y - mb) for x, y in zip(A, B)) / den
        # --- respeto, real y placebo
        def evaluar(corr):
            t = ok = 0
            marg = []
            for f, ns in zip(fotos, niveles):
                for L in ns:
                    r = tocar(velas, ph, L + corr, f["h"], f["h"] + VENTANA)
                    if r:
                        t += 1; ok += r[0]; marg.append(r[1])
            return t, ok, marg
        t0, ok0, m0 = evaluar(0.0)
        tp = okp = 0; mp = []
        for c in PLACEBOS:
            a, b, m = evaluar(c)
            tp += a; okp += b; mp += m
        res[nombre] = dict(beta=beta, toques=t0, frena=ok0, marg=m0,
                           ptoques=tp, pfrena=okp, pmarg=mp,
                           niveles=st.fmean(len(x) for x in niveles))
    return res, len(fotos)


def informe(res, nfotos, titulo):
    print("=" * 78)
    print(titulo)
    print("=" * 78)
    print("%d fotos usadas   |   toque = %.0f pts, viniendo de %.0f, se miran %d min"
          % (nfotos, TOL, LEJOS, VENTANA))
    print()
    print("formula                        niv  beta   toques  freno   placebo   ventaja")
    filas = []
    for nom, r in res.items():
        if r["toques"] < 10:
            print("  %-28s %3.0f  %5.2f    %4d   muestra chica" % (nom, r["niveles"], r["beta"], r["toques"]))
            continue
        tasa = 100.0 * r["frena"] / r["toques"]
        tp = 100.0 * r["pfrena"] / r["ptoques"] if r["ptoques"] else float("nan")
        filas.append((tasa - tp, nom, r, tasa, tp))
    filas.sort(reverse=True)
    for vent, nom, r, tasa, tp in filas:
        print("  %-28s %3.0f  %5.2f    %4d  %4.1f%%    %4.1f%%    %+5.1f pp"
              % (nom, r["niveles"], r["beta"], r["toques"], tasa, tp, vent))
    print()
    print("  beta cerca de 1 = el nivel sigue al precio (no sirve para avisar)")
    print("  ventaja = cuanto le gana al placebo. Si no es claramente positiva,")
    print("            la formula no informa mas que una linea cualquiera.")


if __name__ == "__main__":
    for dc, di in (("20260903", "2026-09-03"), ("20260831", "2026-08-31")):
        out = correr(dc, di)
        if out:
            informe(out[0], out[1], "DIA %s" % di)
            print()
        else:
            print("dia %s: datos insuficientes" % di)
