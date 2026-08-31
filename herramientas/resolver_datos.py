# -*- coding: utf-8 -*-
"""Resuelve los choques de git en los archivos que escriben los dos lados.

El bot de GitHub Actions y la maquina local calculan lo mismo cada quince
minutos. Cuando los dos escriben, git no sabe cual vale y marca conflicto en
archivos que NO son codigo: la serie de precio del dia, las observaciones del
centinela y el historico de fotos.

Tomar "el mio" o "el de ellos" a ciegas pierde datos reales. Cada archivo tiene
una regla de union distinta y ninguna es opinable:

  - historico (_SYM-FECHA.jsonl): una foto por timestamp. Union por `t`.
  - precio (precio-SYM-FECHA.json): gana la serie con MAS velas, porque las
    velas solo se agregan; nunca se borran.
  - observaciones (observaciones.jsonl): union por clave, y si la misma
    observacion aparece en los dos lados gana la que vio MAS rueda. Un nivel
    tocado nunca se destoca, y un desenlace resuelto nunca vuelve a abrirse.

    python herramientas/resolver_datos.py
"""
import json
import os
import subprocess
import sys

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def _etapa(n, ruta):
    """El contenido de un lado del conflicto: 2 = el de la rama, 3 = el mio."""
    r = subprocess.run(["git", "show", ":%d:%s" % (n, ruta)],
                       capture_output=True, text=True, cwd=RAIZ)
    return r.stdout if r.returncode == 0 else ""


def _lineas_json(txt):
    for l in txt.splitlines():
        l = l.strip()
        if not l:
            continue
        try:
            yield json.loads(l), l
        except Exception:
            continue


def unir_historico(ruta):
    """Una foto por timestamp. Las dos ramas aportan."""
    filas = {}
    for n in (2, 3):
        for o, cru in _lineas_json(_etapa(n, ruta)):
            filas[o.get("t")] = cru
    with open(os.path.join(RAIZ, ruta), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(filas[k] for k in sorted(filas)) + "\n")
    return "%d fotos" % len(filas)


def unir_precio(ruta):
    """Gana la serie mas larga: las velas se agregan, no se borran."""
    mejor, n_mejor = None, -1
    for n in (2, 3):
        try:
            d = json.loads(_etapa(n, ruta))
        except Exception:
            continue
        c = len(d.get("velas") or [])
        if c > n_mejor:
            mejor, n_mejor = d, c
    if mejor is None:
        return "sin contenido"
    with open(os.path.join(RAIZ, ruta), "w", encoding="utf-8", newline="\n") as f:
        json.dump(mejor, f, separators=(",", ":"))
    return "%d velas" % n_mejor


def _mejor_observacion(a, b):
    """De dos veredictos sobre la misma observacion, el que vio mas rueda.

    Tocado gana sobre no tocado: el precio no se destoca. Y entre dos tocados,
    gana el que ademas llego a resolverse.
    """
    if bool(a.get("tocado")) != bool(b.get("tocado")):
        return a if a.get("tocado") else b
    ra = bool(a.get("aguanto")) or bool(a.get("rompio"))
    rb = bool(b.get("aguanto")) or bool(b.get("rompio"))
    if ra != rb:
        return a if ra else b
    return a


def unir_observaciones(ruta):
    filas = {}
    for n in (2, 3):
        for o, _ in _lineas_json(_etapa(n, ruta)):
            k = (o.get("fecha"), o.get("hora"), o.get("tipo"),
                 o.get("nivel"), o.get("es0dte"))
            filas[k] = _mejor_observacion(o, filas[k]) if k in filas else o
    with open(os.path.join(RAIZ, ruta), "w", encoding="utf-8", newline="\n") as f:
        for k in sorted(filas, key=lambda x: tuple(str(v) for v in x)):
            f.write(json.dumps(filas[k]) + "\n")
    return "%d observaciones" % len(filas)


def unir_contexto(ruta):
    """Lo que anoto ATAS. Una anotacion por timestamp."""
    filas = {}
    for n in (2, 3):
        for o, cru in _lineas_json(_etapa(n, ruta)):
            filas[o.get("t")] = cru
    with open(os.path.join(RAIZ, ruta), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(filas[k] for k in sorted(filas)) + "\n")
    return "%d anotaciones" % len(filas)


REGLAS = (
    ("datos/historico/precio-", unir_precio),
    ("datos/historico/", unir_historico),
    ("conocimiento/centinela/observaciones", unir_observaciones),
    ("datos/contexto/", unir_contexto),
)


def main():
    r = subprocess.run(["git", "diff", "--name-only", "--diff-filter=U"],
                       capture_output=True, text=True, cwd=RAIZ)
    rutas = [x.strip() for x in r.stdout.splitlines() if x.strip()]
    if not rutas:
        print("no hay conflictos")
        return 0

    quedan = []
    for ruta in rutas:
        for pref, fn in REGLAS:
            if ruta.startswith(pref):
                try:
                    detalle = fn(ruta)
                    subprocess.run(["git", "add", ruta], cwd=RAIZ)
                    print("  unido  %-52s %s" % (ruta, detalle))
                except Exception as e:
                    print("  FALLO  %s: %s" % (ruta, e))
                    quedan.append(ruta)
                break
        else:
            quedan.append(ruta)

    if quedan:
        print("\n  quedan a mano (son codigo, no datos):")
        for q in quedan:
            print("     " + q)
        return 1
    print("\n  listo. Ahora: git rebase --continue")
    return 0


if __name__ == "__main__":
    sys.exit(main())
