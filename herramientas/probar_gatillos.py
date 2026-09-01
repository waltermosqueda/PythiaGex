# -*- coding: utf-8 -*-
"""Backtest de los gatillos sobre lo que el indicador ya anoto.

QUE MIDE

Cada disparo dice LARGO o CORTO en un precio y un momento. La pregunta es
una sola y no admite interpretacion: desde ahi, ¿el precio llego GANANCIA
ticks a favor ANTES de llegar GANANCIA ticks en contra?

Es simetrica a proposito. Si fuera mas generosa de un lado que del otro
cualquier gatillo pareceria bueno.

Y CADA COMPONENTE POR SEPARADO

Ademas de juzgar el disparo completo, se prueba cada ingrediente por su
cuenta: absorcion, imbalances apilados, print grande, divergencia, esfuerzo
y barridos. Asi se ve cual aporta y cual solo suma ruido, que es lo que
decide si un componente se queda, se le sube el peso o se saca.

    python herramientas/probar_gatillos.py
    python herramientas/probar_gatillos.py --ganancia 12 --horizonte 45
"""
import glob
import json
import io
import math
import os
import sys

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TICK = 0.25
GANANCIA_TK = 8     # ticks a favor y en contra: la regla es simetrica
HORIZONTE = 30      # minutos que se le dan al disparo para resolverse
MIN_CASOS = 10


def wilson(exitos, total, z=1.96):
    if total == 0:
        return (0.0, 0.0, 1.0)
    p = exitos / total
    d = 1 + z * z / total
    c = (p + z * z / (2 * total)) / d
    m = z * math.sqrt(p * (1 - p) / total + z * z / (4 * total * total)) / d
    return (p, max(0.0, c - m), min(1.0, c + m))


def _mins(t):
    try:
        return int(t[11:13]) * 60 + int(t[14:16])
    except Exception:
        return None


def cargar():
    """Todas las anotaciones del indicador, por dia."""
    dirs = [os.path.join(RAIZ, "datos", "contexto"),
            os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "contexto")]
    por_dia = {}
    for d in dirs:
        if not d or not os.path.isdir(d):
            continue
        for r in sorted(glob.glob(os.path.join(d, "contexto-*.jsonl"))):
            fecha = os.path.basename(r)[9:19]
            filas = por_dia.setdefault(fecha, {})
            for l in io.open(r, encoding="utf-8-sig"):
                l = l.strip()
                if not l:
                    continue
                try:
                    x = json.loads(l)
                except Exception:
                    continue
                if x.get("t"):
                    filas[x["t"]] = x       # por timestamp: sin repetidos
    return {f: [v for _, v in sorted(d.items())] for f, d in por_dia.items() if d}


def camino(filas):
    """El recorrido del precio: cada tramo con su maximo y su minimo.

    Es lo unico que hay las 24 horas. CBOE solo publica el contado en horario
    de Nueva York, asi que de noche esta serie es la unica que existe.
    """
    out = []
    for x in filas:
        m = _mins(x.get("t") or "")
        if m is None or not x.get("precio"):
            continue
        out.append({"m": m, "alto": x.get("maximo") or x["precio"],
                    "bajo": x.get("minimo") or x["precio"], "cierre": x["precio"]})
    return out


def desenlace(precio, lado, minuto, ruta, ganancia, horizonte):
    """True si llego a favor primero, False si en contra, None si no se sabe."""
    meta = precio + lado * ganancia * TICK
    stop = precio - lado * ganancia * TICK
    for v in ruta:
        if v["m"] <= minuto:
            continue
        if v["m"] > minuto + horizonte:
            break
        gano = v["alto"] >= meta if lado > 0 else v["bajo"] <= meta
        perdio = v["bajo"] <= stop if lado > 0 else v["alto"] >= stop
        if gano and perdio:
            return None      # el tramo toco los dos: no se sabe cual primero
        if gano:
            return True
        if perdio:
            return False
    return None


def tabla(titulo, grupos, ganancia):
    print("\n  %s" % titulo)
    print("  %-46s %6s %8s %16s" % ("", "casos", "acerto", "intervalo"))
    for nombre, casos in grupos:
        if not casos:
            continue
        p, lo, hi = wilson(sum(1 for c in casos if c), len(casos))
        marca = ""
        if len(casos) >= MIN_CASOS:
            marca = "  <-- SIRVE" if lo > 0.5 else ("  <-- AL REVES" if hi < 0.5 else "")
        else:
            marca = "   pocos casos"
        print("  %-46s %6d %7.0f%% %16s%s"
              % (nombre[:46], len(casos), p * 100,
                 "%.0f%% a %.0f%%" % (lo * 100, hi * 100), marca))


def main():
    a = sys.argv[1:]
    ganancia = int(a[a.index("--ganancia") + 1]) if "--ganancia" in a else GANANCIA_TK
    horizonte = int(a[a.index("--horizonte") + 1]) if "--horizonte" in a else HORIZONTE

    dias = cargar()
    if not dias:
        print("no hay anotaciones del indicador todavia")
        return 1

    print("=" * 92)
    print("  GATILLOS: ¿aciertan?   regla %d ticks a favor antes que %d en contra, "
          "%d min" % (ganancia, ganancia, horizonte))
    print("=" * 92)

    disparos, comp = [], {}
    fotos_tot = 0
    for fecha, filas in sorted(dias.items()):
        ruta = camino(filas)
        fotos_tot += len(filas)
        if len(ruta) < 5:
            continue

        # ---- el disparo completo
        vistos = set()
        for x in filas:
            for e in (x.get("disparos") or []):
                k = (e.get("t"), e.get("nivel"), e.get("lado"), e.get("precio"))
                if k in vistos:
                    continue
                vistos.add(k)
                m = _mins(e.get("t") or "")
                if m is None or not e.get("precio") or not e.get("lado"):
                    continue
                r = desenlace(e["precio"], e["lado"], m, ruta, ganancia, horizonte)
                if r is None:
                    continue
                disparos.append({"ok": r, "puntaje": e.get("puntaje") or 0,
                                 "lado": e["lado"], "fecha": fecha,
                                 "razones": e.get("razones") or ""})

        # ---- cada ingrediente por su cuenta, en cada nivel de cada foto
        for x in filas:
            m = _mins(x.get("t") or "")
            if m is None:
                continue
            for nv in (x.get("niveles") or []):
                pf = nv.get("fut")
                if not pf:
                    continue
                # el lado que implica cada senal
                pruebas = []
                if nv.get("absorcion"):
                    # absorcion sola no dice lado: se prueba con el delta
                    dl = nv.get("delta") or 0
                    if dl:
                        pruebas.append(("absorcion + delta del nivel",
                                        1 if dl > 0 else -1))
                ap, la = nv.get("apilados") or 0, nv.get("lado_apilados") or 0
                if ap > 0 and la:
                    pruebas.append(("imbalances apilados", la))
                if nv.get("print_grande"):
                    dl = nv.get("delta") or 0
                    if dl:
                        pruebas.append(("print grande + delta del nivel",
                                        1 if dl > 0 else -1))
                if nv.get("divergencia") and nv.get("lado_divergencia"):
                    pruebas.append(("divergencia de delta", nv["lado_divergencia"]))
                bc, bv = nv.get("barrido_compra") or 0, nv.get("barrido_venta") or 0
                if (bc + bv) > 0 and abs(bc - bv) / (bc + bv) >= 0.25:
                    pruebas.append(("barridos de agresores", 1 if bc > bv else -1))
                dd = nv.get("desbalance_dom")
                if dd is not None and abs(dd) >= 0.20:
                    pruebas.append(("desbalance del libro", 1 if dd > 0 else -1))
                cf = nv.get("confluencia") or 0
                if cf >= 3:
                    dl = nv.get("delta") or 0
                    if dl:
                        pruebas.append(("3+ confluencias + delta", 1 if dl > 0 else -1))

                for nombre, lado in pruebas:
                    r = desenlace(pf, lado, m, ruta, ganancia, horizonte)
                    if r is not None:
                        comp.setdefault(nombre, []).append(r)

    print("  %d fotos del indicador en %d rueda(s): %s"
          % (fotos_tot, len(dias), ", ".join(sorted(dias))))

    # ---------------------------------------------------------- resultados
    if disparos:
        tabla("EL DISPARO COMPLETO, POR PUNTAJE", [
            ("puntaje 3", [d["ok"] for d in disparos if d["puntaje"] == 3]),
            ("puntaje 4", [d["ok"] for d in disparos if d["puntaje"] == 4]),
            ("puntaje 5 o mas", [d["ok"] for d in disparos if d["puntaje"] >= 5]),
            ("TODOS", [d["ok"] for d in disparos]),
            ("solo los largos", [d["ok"] for d in disparos if d["lado"] > 0]),
            ("solo los cortos", [d["ok"] for d in disparos if d["lado"] < 0]),
        ], ganancia)
    else:
        print("\n  todavia no hay disparos resueltos")

    if comp:
        tabla("CADA INGREDIENTE POR SU CUENTA",
              sorted(((k, v) for k, v in comp.items()), key=lambda z: -len(z[1])),
              ganancia)

    print("\n  Una moneda daria 50%. Un ingrediente sirve si su intervalo entero")
    print("  queda ARRIBA de 50, y hay que darlo vuelta si queda entero abajo.")
    print("  Con los intervalos cruzando el 50 no se puede afirmar nada todavia.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
