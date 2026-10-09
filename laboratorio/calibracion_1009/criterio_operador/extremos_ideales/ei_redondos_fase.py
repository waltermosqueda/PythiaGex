# -*- coding: utf-8 -*-
"""ei_redondos_fase.py — prueba directa (sin rayas, sin juez de llegadas) de si las PUNTAS de los giros (el mismo zigzag de 20 pts
del juez, en las dos ventanas de las 19 noches) caen en numeros redondos de NQ mas de lo que caen en cualquier otra 'fase'.
  1) % de puntas a +-2 de un multiplo de G, contra el mismo % para las G*4 fases posibles (paso 0,25): percentil de la fase 0.
  2) Pinchazo: punta MAS ALLA del redondo por 2,25-6 pts (falso rompimiento del redondo) contra 'se freno antes' (-6 a -2,25)
     y contra lo uniforme. SOLO LECTURA. Correr: python -I ei_redondos_fase.py"""
import os
import sys

import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
sys.path.insert(0, os.path.join(os.path.dirname(AQUI), "juez"))
import ei_conjuntos as C  # noqa: E402
import ei_medir as M  # noqa: E402
import juez_operador as J  # noqa: E402


def main():
    V = C.cargar_velas(os.path.join(M.CAL, "velas_m1.csv"))
    for vn, ven in M.ventanas().items():
        op = J._op({"ventanas": [(a, b) for _, a, b in ven]})
        piv = J._pivotes(J.preparar_velas(V[["t", "o", "h", "l", "c"]], op), op)
        x = np.array([p["precio"] for p in piv])
        print("%s: %d giros (zigzag 20)" % (vn, len(x)))
        for G in (25, 50, 100):
            fases = np.arange(0, G, 0.25)
            cov = np.array([np.mean(np.abs(((x - f + G / 2) % G) - G / 2) <= 2.0) for f in fases])
            print("  G=%3d: puntas a +-2 de un multiplo %.1f %% | otras fases mediana %.1f %% (p5 %.1f, p95 %.1f) | percentil de la fase 0: "
                  "%.0f | uniforme %.1f %%" % (G, 100 * cov[0], 100 * np.median(cov), 100 * np.percentile(cov, 5),
                                               100 * np.percentile(cov, 95), 100 * (cov < cov[0]).mean(), 100 * 17 / (4 * G)))
            d = np.array([(((p["precio"] + G / 2) % G) - G / 2) * (1 if p["tipo"] == "max" else -1) for p in piv])
            fr = lambda a, b: 100 * np.mean((d >= a) & (d <= b))
            un = lambda a, b: 100 * (b - a + 0.25) / G
            print("         se freno antes (-6..-2,25) %.1f %% (unif %.1f) | en el redondo (+-2) %.1f %% (unif %.1f) | pincho y volvio "
                  "(+2,25..+6) %.1f %% (unif %.1f)" % (fr(-6, -2.25), un(-6, -2.25), fr(-2, 2), un(-2, 2), fr(2.25, 6), un(2.25, 6)))


if __name__ == "__main__":
    main()
