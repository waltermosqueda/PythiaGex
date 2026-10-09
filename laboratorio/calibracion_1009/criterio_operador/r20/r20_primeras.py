# -*- coding: utf-8 -*-
"""r20_primeras.py — SENSIBILIDAD (NO pre-registrada): el juez cuenta cada re-test de una raya como otra llegada y sus recorridos se
solapan (advertencia del docstring del juez). Aca se repite el placebo TOLERANTE quedandose SOLO con la PRIMERA llegada de cada pista en
cada noche (un episodio = una raya en una noche), para el real, las corridas y el azar (mismos sorteos: semilla 20261009, 'pool').
Usa las funciones internas del juez sin modificarlo (_juzgar_core, _offsets_azar). SOLO LECTURA. Escribe r20_primeras.json/.txt.
Correr: python -I r20_primeras.py [n_azar]"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r20_comun as C  # noqa: E402

J = C.J
SEM = 20261009
METS = ("llegadas", "pct_sostenidos", "pct_exactos", "pct_rotas", "precision_mediana_abs", "recorrido_medio", "recorrido_mediano",
        "pct_llega_opuesta", "pct_falso_entre_sostenidos")
LINEAS = []


def p(s):
    print(s); LINEAS.append(s)


def primeras(evs):
    vis = set(); out = []
    for e in sorted(evs, key=lambda e: e["i"]):
        k = (e["pista"], e["noche"])
        if k in vis:
            continue
        vis.add(k); out.append(e)
    return out


def main():
    n_azar = int(sys.argv[1]) if len(sys.argv) > 1 else 500
    V, niv = C.cargar()
    Ns = C.sesiones(V)
    res = {}
    for nombre, claves in C.CONJUNTOS.items():
        for tipo in ("congelada", "completa"):
            R = C.rayas(niv, claves)
            op = {"ventanas": C.ventanas(Ns, tipo), "modo": "tolerante"}
            Vp, Rp = J.preparar(V, R, op)
            opx = J._op(op)
            nv = J._noches_ventana(Vp)
            P = len(Rp["pistas"])
            real = J.resumir(primeras(J._juzgar_core(Vp, Rp, opx)), nv, opx["tol"])
            cor = []
            for d in (-31, -19, -11, 11, 19, 31):
                cor.extend(primeras(J._juzgar_core(Vp, Rp, opx, np.full(P, float(d)))))
            corr = J.resumir(cor, nv, opx["tol"])
            rng = np.random.default_rng(SEM)
            acc = {m: [] for m in METS}
            for _ in range(n_azar):
                off = J._offsets_azar(Vp, Rp, rng, "pool")
                r = J.resumir(primeras(J._juzgar_core(Vp, Rp, opx, off)), nv, opx["tol"])
                for m in METS:
                    acc[m].append(r[m])
            az = {}
            for m in METS:
                a = np.array(acc[m], float); a = a[np.isfinite(a)]; x = real[m]
                az[m] = {"real": x, "p5": float(np.percentile(a, 5)), "p50": float(np.percentile(a, 50)), "p95": float(np.percentile(a, 95)),
                         "percentil": float(100 * ((a < x).sum() + 0.5 * (a == x).sum()) / len(a)) if x == x else None, "corridas": corr[m]}
            res["%s/%s" % (nombre, tipo)] = {"real": real, "corridas": corr, "azar": az}
            p("\n%s %s — SOLO PRIMERA LLEGADA por pista y noche (TOLERANTE, %d juegos 'pool'): llegadas %d, base %d, sostenidos %d, noches %d" % (
                nombre, tipo.upper(), n_azar, real["llegadas"], real["base"], real["sostenidos"], real["n_noches"]))
            for m in METS:
                a = az[m]
                p("   %-28s real %8s | corridas %8s | azar p5 %8s p50 %8s p95 %8s | percentil %s" % (
                    m, C.r(a["real"], 2), C.r(a["corridas"], 2), C.r(a["p5"], 2), C.r(a["p50"], 2), C.r(a["p95"], 2), C.r(a["percentil"], 1)))
    json.dump(C.limpio(res), open(os.path.join(C.AQUI, "r20_primeras.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=0)
    open(os.path.join(C.AQUI, "r20_primeras.txt"), "w", encoding="utf-8").write("\n".join(LINEAS) + "\n")


if __name__ == "__main__":
    main()
