# -*- coding: utf-8 -*-
"""SE ACELERA LA ACTIVIDAD CUANDO EL PRECIO LLEGA AL NIVEL?

DE DONDE SALE LA PREGUNTA
De un video donde la referencia explica sus propias lineas. Lo que dicen,
textualmente, es que las barras marcan "los nodos donde esta mayormente
expuesto el market maker", que ahi "va a cubrir su cartera", y que lo que va a
pasar es que "va a aumentar la velocidad del tape".

NO dicen que el precio rebote. Dicen que ahi se ACELERA LA ACTIVIDAD.

Y nosotros veniamos midiendo lo otro: las 19 formulas se juzgaron con "se da
vuelta el precio?", que no es la afirmacion que ellos hacen. Este archivo mide
la afirmacion correcta.

COMO SE MIDE
Las velas de un minuto traen el volumen de calls y de puts de la cadena. Se
compara la actividad de los minutos en que el precio ESTA en un nivel contra la
de los minutos en que no.

DOS CONTROLES, PORQUE UNO SOLO NO ALCANZA
1. **Normalizacion horaria.** La actividad tiene forma propia: altisima en la
   apertura, baja al mediodia. Sin normalizar, un nivel que caiga cerca del
   precio de apertura pareceria activo sin serlo. Se divide cada minuto por la
   mediana de su entorno de +-20 minutos.
2. **Placebo.** El mismo test sobre el nivel corrido unos puntos. Si el nivel
   real no le gana al corrido, no informa nada.
"""
import sys, os, statistics as st

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cargar
import formulas
import puntuar

TOL = 2.5          # cuanto cuenta como "estar en el nivel", en puntos
VENTANA_NORM = 20  # minutos a cada lado para normalizar la actividad
PLACEBOS = (-31.0, -19.0, -11.0, 11.0, 19.0, 31.0)


def actividad_normalizada(velas):
    """Actividad de cada minuto dividida por la mediana de su entorno.

    Asi un minuto vale 1,0 si es tipico para su hora, 2,0 si tiene el doble de
    lo normal PARA ESE MOMENTO del dia.
    """
    act = {}
    for v in velas:
        act[v["hora"]] = float(v.get("vc", 0) or 0) + float(v.get("vp", 0) or 0)
    horas = sorted(act)
    norm = {}
    for h in horas:
        ent = [act[x] for x in horas if abs(x - h) <= VENTANA_NORM]
        m = st.median(ent) if ent else 0
        norm[h] = (act[h] / m) if m > 0 else None
    return act, norm


def main(dia="20260903", dia_iso="2026-09-03", cada=2, n_niveles=6):
    velas = cargar.velas(dia_iso)
    if not velas or "vc" not in velas[0] and True:
        pass
    # las velas de cargar.velas no traen vc/vp: se releen del archivo
    import json, io
    d = json.load(io.open(os.path.join(cargar.HIST, "precio-SPX-%s.json" % dia_iso),
                          encoding="utf-8"))
    vs = []
    for v in d.get("velas", []):
        hh, mm = v["t"].split(":")
        vs.append(dict(hora=int(hh) * 60 + int(mm), o=float(v["o"]), h=float(v["h"]),
                       l=float(v["l"]), c=float(v["c"]),
                       vc=float(v.get("vc", 0) or 0), vp=float(v.get("vp", 0) or 0)))
    vs.sort(key=lambda x: x["hora"])
    ph = {v["hora"]: v for v in vs}
    act, norm = actividad_normalizada(vs)
    hmin, hmax = min(ph), max(ph)

    fotos = []
    for r in cargar.fotos(dia)[::cada]:
        f = cargar.leer_foto(r)
        if not f:
            continue
        h = f["t"].hour * 60 + f["t"].minute + puntuar.OFF_MIN
        if hmin <= h <= hmax - 30:
            f["h"] = h
            fotos.append(f)
    print("fotos dentro de la rueda: %d   velas: %d" % (len(fotos), len(vs)))
    if len(fotos) < 20:
        print("muestra insuficiente"); return

    print()
    print("ACTIVIDAD cuando el precio esta EN el nivel, contra lo normal de esa hora")
    print("  1,00 = actividad tipica para ese momento del dia")
    print()
    print("formula                        minutos   en nivel   placebo   ventaja")

    filas = []
    for nombre, fn in sorted(formulas.CANDIDATAS.items()):
        def medir(corr):
            vals = []
            for f in fotos:
                try:
                    ns = [x + corr for x in fn(f, n_niveles) if x]
                except Exception:
                    continue
                if not ns:
                    continue
                for m in range(f["h"], min(f["h"] + 30, hmax + 1)):
                    v = ph.get(m)
                    nm = norm.get(m)
                    if not v or nm is None:
                        continue
                    # el precio TOCA el nivel en ese minuto?
                    if any(v["l"] - TOL <= L <= v["h"] + TOL for L in ns):
                        vals.append(nm)
            return vals
        real = medir(0.0)
        plac = []
        for c in PLACEBOS:
            plac += medir(c)
        if len(real) < 30 or len(plac) < 30:
            print("  %-28s %6d   muestra chica" % (nombre, len(real)))
            continue
        mr, mp = st.median(real), st.median(plac)
        filas.append((mr - mp, nombre, len(real), mr, len(plac), mp))
    filas.sort(reverse=True)
    for dif, nom, nr, mr, npl, mp in filas:
        print("  %-28s %6d    %5.3f     %5.3f    %+6.3f" % (nom, nr, mr, mp, dif))

    print()
    print("  ventaja positiva = cuando el precio esta en ESE nivel se opera mas")
    print("  de lo normal para esa hora, mas que en una linea cualquiera.")


if __name__ == "__main__":
    main()
