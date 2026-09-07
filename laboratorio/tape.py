# -*- coding: utf-8 -*-
"""SE ACELERA LA CINTA CUANDO EL PRECIO LLEGA AL NIVEL?

QUE PREGUNTA CONTESTA, Y POR QUE ESTA
GAMMAlito no dice que el precio rebote en sus lineas. Dice que ahi el market
maker "va a cubrir su cartera" y que por eso "va a aumentar la velocidad del
tape". Las 19 formulas del laboratorio se juzgaron con "se da vuelta el
precio?", que es OTRA afirmacion. Todas perdieron contra el placebo. Puede que
las formulas estuvieran bien y la vara equivocada.

Esto mide la vara correcta.

DE DONDE SALE CADA DATO -- Y ESTO IMPORTA
  - La VELOCIDAD DE LA CINTA sale del centinela: el campo `ops` es
    IndicatorCandle.Ticks, o sea cuantas operaciones se imprimieron en ese
    minuto en el futuro. No es un sustituto ni una estimacion.
  - Los NIVELES salen de las fotos archivadas de la cadena de SPX de ESE MISMO
    dia. NO se usan los que grabo el centinela en el campo `niv`: durante un
    Market Replay ATAS reproduce la cinta del futuro pero NO la cadena de
    opciones, asi que el indicador dibuja el mapa de HOY sobre el precio de
    AYER. Usar ese `niv` seria medir un placebo creyendo que es el nivel.
  - El TOQUE se decide con las velas de SPX del historico, en espacio SPX, que
    es donde viven los strikes. Asi no hace falta convertir nada y no se cuela
    el error de la base.

TRES CONTROLES
  1. Normalizacion horaria: la cinta corre distinto a las 9:30 que a las 12:00.
     Cada minuto se divide por la mediana de su entorno de +-20 minutos, asi
     1,00 es "lo normal PARA ESA HORA".
  2. Placebo: el mismo test sobre el nivel corrido. Si el real no le gana al
     corrido, el nivel no informa nada.
  3. Causalidad: la foto de CBOE llega 15 minutos tarde. Con --causal solo se
     miden minutos posteriores al momento en que la foto REALMENTE estaba
     disponible. Sin eso estariamos midiendo con informacion del futuro.
"""
import sys, os, io, json, re, glob, argparse
import statistics as st
from datetime import datetime

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cargar
import formulas
import puntuar

DIR_CENT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "centinela")

TOL = 2.5            # cuanto cuenta como "estar en el nivel", en puntos SPX
VENTANA_NORM = 20    # minutos a cada lado para normalizar
VENTANA = 30         # cuantos minutos hacia adelante se mira desde cada foto
PLACEBOS = (-31.0, -19.0, -11.0, 11.0, 19.0, 31.0)
RETRASO_CBOE = 15    # minutos que tarda la cadena en estar disponible
UTC_A_ET = -240      # septiembre: Nueva York esta en UTC-4


def leer_centinela(patron):
    """Lee los JSONL del centinela.

    Tolera dos defectos de la primera corrida, que quedaron en archivo:
      - BOM al principio (el DLL viejo escribia marca de orden de bytes)
      - "spot":NaN, que no es JSON valido
    """
    filas = []
    for ruta in sorted(glob.glob(patron)):
        with io.open(ruta, "r", encoding="utf-8-sig") as f:
            for linea in f:
                linea = linea.strip()
                if not linea:
                    continue
                linea = re.sub(r":\s*NaN", ": null", linea)
                try:
                    d = json.loads(linea)
                except Exception:
                    continue
                try:
                    t = datetime.strptime(d["t"], "%Y-%m-%dT%H:%M:%S")
                except Exception:
                    continue
                filas.append(dict(
                    t=t,
                    et=t.hour * 60 + t.minute + UTC_A_ET,
                    o=d.get("o"), h=d.get("h"), l=d.get("l"), c=d.get("c"),
                    vol=float(d.get("vol") or 0),
                    ops=float(d.get("ops") or 0),
                    delta=float(d.get("delta") or 0)))
    filas.sort(key=lambda x: x["t"])
    return filas


def normalizar(filas, campo="ops"):
    """Cada minuto dividido por la mediana de su entorno horario."""
    val = {}
    for r in filas:
        val[r["et"]] = val.get(r["et"], 0.0) + r[campo]
    horas = sorted(val)
    norm = {}
    for h in horas:
        ent = [val[x] for x in horas if abs(x - h) <= VENTANA_NORM]
        m = st.median(ent) if ent else 0.0
        norm[h] = (val[h] / m) if m > 0 else None
    return val, norm


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dia", default="20260903")
    ap.add_argument("--dia-iso", default="2026-09-03")
    ap.add_argument("--cent", default=os.path.join(DIR_CENT, "*MES*M1.jsonl"))
    ap.add_argument("--cada", type=int, default=2)
    ap.add_argument("--niveles", type=int, default=6)
    ap.add_argument("--causal", action="store_true",
                    help="solo minutos en que la foto ya estaba disponible")
    # POR QUE ESTO ES UNA OPCION Y NO UNA CONSTANTE
    # Los strikes de SPX estan TODOS en multiplos de 5. Un placebo de 11, 19 o
    # 31 puntos cae siempre ENTRE strikes, o sea sobre un numero que no es
    # redondo. Entonces el nivel real tiene dos cosas a la vez -- es un strike
    # con gamma Y es un numero redondo -- y el placebo no tiene ninguna.
    # Cualquier ventaja podria ser pura redondez y no gamma.
    # Con "strike" el placebo cae sobre OTRO strike: misma redondez, otra
    # gamma. Recien ahi la unica diferencia es la que queremos medir.
    ap.add_argument("--placebos", default="raro", choices=("raro", "strike"),
                    help="raro = 11/19/31 (cae entre strikes); "
                         "strike = 15/25/35 (cae sobre otro strike)")
    a = ap.parse_args()
    global PLACEBOS
    if a.placebos == "strike":
        PLACEBOS = (-35.0, -25.0, -15.0, 15.0, 25.0, 35.0)

    cent = leer_centinela(a.cent)
    if not cent:
        print("no hay filas del centinela en %s" % a.cent)
        return
    print("filas del centinela: %d   de %s a %s UTC"
          % (len(cent), cent[0]["t"], cent[-1]["t"]))
    # el offset a Nueva York puede dar negativo (una vela de la madrugada UTC
    # es de la noche anterior en ET); el modulo lo devuelve a un reloj legible
    def reloj(m):
        m %= 1440
        return "%02d:%02d" % (m // 60, m % 60)
    print("  en hora de Nueva York: %s a %s"
          % (reloj(cent[0]["et"]), reloj(cent[-1]["et"])))

    velas = cargar.velas(a.dia_iso)
    if not velas:
        print("no hay velas de SPX para %s" % a.dia_iso)
        return
    ph = {v["hora"]: v for v in velas}
    print("velas de SPX: %d   de %02d:%02d a %02d:%02d"
          % (len(velas), min(ph) // 60, min(ph) % 60, max(ph) // 60, max(ph) % 60))

    solape = sorted(set(ph) & set(r["et"] for r in cent))
    print("MINUTOS QUE SE SOLAPAN: %d" % len(solape))
    if len(solape) < 60:
        print()
        print("  Sin solape no hay nada que medir: la cinta del centinela y las")
        print("  velas de SPX tienen que ser de los mismos minutos. Hace falta")
        print("  un replay dentro de la rueda de Nueva York (9:30 a 16:00 ET).")
        return

    dentro = set(solape)
    _, norm = normalizar([r for r in cent if r["et"] in dentro])
    hmin, hmax = min(solape), max(solape)

    fotos = []
    for r in cargar.fotos(a.dia)[::a.cada]:
        f = cargar.leer_foto(r)
        if not f:
            continue
        h = f["t"].hour * 60 + f["t"].minute + puntuar.OFF_MIN
        if hmin <= h <= hmax - VENTANA:
            f["h"] = h
            fotos.append(f)
    print("fotos de la cadena dentro de ese tramo: %d" % len(fotos))
    if len(fotos) < 20:
        print("muestra insuficiente de fotos")
        return

    arranque = RETRASO_CBOE if a.causal else 0
    print()
    print("VELOCIDAD DE LA CINTA cuando el precio esta EN el nivel")
    print("  1,00 = las operaciones por minuto tipicas de ESE momento del dia")
    print("  modo: %s" % ("causal (solo con la foto ya disponible)"
                          if a.causal else "sin retraso"))
    print()
    print("  %-29s %6s  %8s %8s %8s  %9s %7s"
          % ("formula", "minuto", "en nivel", "placebo", "ventaja",
             "fotos +/-", "azar"))

    filas = []
    for nombre, fn in sorted(formulas.CANDIDATAS.items()):
        def medir_foto(f, corr):
            """Los minutos de UNA foto en que el precio toca el nivel."""
            try:
                ns = [x + corr for x in fn(f, a.niveles) if x]
            except Exception:
                return []
            if not ns:
                return []
            vals = []
            for m in range(f["h"] + arranque, min(f["h"] + VENTANA, hmax) + 1):
                v = ph.get(m)
                nm = norm.get(m)
                if not v or nm is None:
                    continue
                if any(v["l"] - TOL <= L <= v["h"] + TOL for L in ns):
                    vals.append(nm)
            return vals

        # POR FOTO, no por minuto. Los minutos no son independientes: las
        # ventanas de 30 minutos se pisan y un mismo minuto se cuenta una vez
        # por cada nivel que lo toca. Tratarlos como independientes infla
        # cualquier prueba de significancia. La foto es la unidad razonable.
        pares = []
        real, plac = [], []
        for f in fotos:
            r = medir_foto(f, 0.0)
            p = []
            for c in PLACEBOS:
                p += medir_foto(f, c)
            real += r
            plac += p
            if r and p:
                pares.append(st.median(r) - st.median(p))
        if len(real) < 30 or len(plac) < 30 or len(pares) < 8:
            print("  %-29s %6d  muestra chica" % (nombre, len(real)))
            continue
        mr, mp = st.median(real), st.median(plac)
        # cuantas fotos van a favor: si el efecto fuera azar, la mitad
        favor = sum(1 for d in pares if d > 0)
        contra = sum(1 for d in pares if d < 0)
        p = _binomial(favor, favor + contra) if (favor + contra) else 1.0
        filas.append((mr - mp, nombre, len(real), mr, mp, favor, contra, p))
    filas.sort(reverse=True)
    for dif, nom, nr, mr, mp, fa, co, p in filas:
        print("  %-29s %6d    %5.3f    %5.3f   %+6.3f      %3d/%-3d  %6.3f"
              % (nom, nr, mr, mp, dif, fa, co, p))

    print()
    print("  ventaja positiva = con el precio EN ese nivel se opera mas rapido")
    print("  de lo normal para esa hora, y mas que en una linea cualquiera.")
    print("  fotos +/- = en cuantas fotos gano el nivel real y en cuantas el placebo.")
    print("  azar = probabilidad de ver ese reparto si el nivel no informara nada.")
    print("  Con una sola rueda nada de esto concluye: es la primera medicion.")


def _binomial(k, n, p=0.5):
    """Probabilidad de ver k o mas exitos en n tiradas de una moneda justa.

    Es la pregunta honesta: si el nivel no informara nada, en la mitad de las
    fotos ganaria el real y en la mitad el placebo. Que tan raro es lo que
    salio?
    """
    from math import comb
    if n <= 0:
        return 1.0
    k = max(k, n - k)          # a dos colas
    cola = sum(comb(n, i) for i in range(k, n + 1)) * (p ** n)
    return min(1.0, 2.0 * cola)


if __name__ == "__main__":
    main()
