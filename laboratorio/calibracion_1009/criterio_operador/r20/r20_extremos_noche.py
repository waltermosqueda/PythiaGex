# -*- coding: utf-8 -*-
"""r20_extremos_noche.py — DESCRIPTIVO (no pre-registrado): el MAXIMO y el MINIMO absolutos de cada ventana de cada noche (las dos puntas
que el operador mas quiere tener dibujadas). Para cada punta: la raya del conjunto mas cercana que estaba VIGENTE en la vela de
aproximacion (la primera vela de la ventana cuya mecha llega a +-2 de la punta; raya vigente = la dibujada al cierre de la vela anterior,
igual que el juez: se usa J.preparar y las rayas crudas sin fusionar). Placebo: las mismas rayas corridas +-11/19/31.
SOLO LECTURA. Escribe r20_extremos_noche.json/.txt. Correr: python -I r20_extremos_noche.py"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r20_comun as C  # noqa: E402

J = C.J
LINEAS = []


def p(s):
    print(s); LINEAS.append(s)


def main():
    V, niv = C.cargar()
    Ns = C.sesiones(V)
    out = {}
    for nombre, claves in C.CONJUNTOS.items():
        R = C.rayas(niv, claves)
        for tipo in ("congelada", "completa"):
            Vp, Rp = J.preparar(V, R, {"ventanas": C.ventanas(Ns, tipo)})
            P = len(Rp["pistas"])
            filas = []
            for N in Ns:
                a, b = C.ventana(N, tipo)
                tm = Vp["tm"]
                ia = int(np.searchsorted(tm, J._a_min(a))); ib = int(np.searchsorted(tm, J._a_min(b)))
                if ib - ia < 30:
                    continue
                h = Vp["h"][ia:ib]; l = Vp["l"][ia:ib]
                for tp, precio in (("max", float(h.max())), ("min", float(l.min()))):
                    w = np.flatnonzero(h >= precio - 2) if tp == "max" else np.flatnonzero(l <= precio + 2)
                    k = ia + int(w[0])
                    fila = {"noche": str(N.date()), "tipo": tp, "precio": precio, "t_aprox": str(Vp["t"][k])}
                    for d in (0, -31, -19, -11, 11, 19, 31):
                        _, val = J._rayas_en(Rp, k, np.full(P, float(d)), crudas=True)
                        dist = float(np.min(np.abs(val - precio))) if len(val) else None
                        fila["d%+d" % d if d else "dist"] = dist
                        if d == 0:
                            fila["raya"] = float(val[np.argmin(np.abs(val - precio))]) if len(val) else None
                    filas.append(fila)
            res = {}
            for tol in (2, 4, 6, 10):
                real = np.mean([f["dist"] is not None and f["dist"] <= tol for f in filas]) * 100
                cor = np.mean([[f["d%+d" % d] is not None and f["d%+d" % d] <= tol for d in (-31, -19, -11, 11, 19, 31)] for f in filas]) * 100
                res[tol] = (real, cor)
            dists = [f["dist"] for f in filas if f["dist"] is not None]
            out["%s/%s" % (nombre, tipo)] = {"filas": filas, "pct_a_tol": res, "dist_mediana": float(np.median(dists)) if dists else None}
            p("\n%s %s: %d puntas (max y min de cada noche). Distancia mediana raya-punta %.1f pts" % (nombre, tipo.upper(), len(filas), np.median(dists)))
            p("   con raya a <= tol: " + "; ".join("+-%d: real %.0f %% vs corridas %.0f %%" % (t, r_, c_) for t, (r_, c_) in res.items()))
            for f in filas:
                p("     %s %s %.2f aprox %s -> raya mas cercana %s a %s pts" % (f["noche"], f["tipo"], f["precio"], f["t_aprox"][5:16],
                                                                             C.r(f["raya"], 2), C.r(f["dist"], 2)))
    json.dump(C.limpio(out), open(os.path.join(C.AQUI, "r20_extremos_noche.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=0)
    open(os.path.join(C.AQUI, "r20_extremos_noche.txt"), "w", encoding="utf-8").write("\n".join(LINEAS) + "\n")


if __name__ == "__main__":
    main()
