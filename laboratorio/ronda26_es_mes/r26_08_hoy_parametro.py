# -*- coding: utf-8 -*-
"""RONDA 26, paso 8 (control de HOY, DESCRIPTIVO, agregado despues del pre-registro; no entra en ninguna decision): PARAMETRO CONTRA MERCADO.
Hoy el panel del MES uso X = 10,00 (su mediana 126,50) y el del ES X = 10,25 (126,625). Con las cintas de hoy cortadas a las 16:48:00 UTC:
  - giros confirmados de la ola y la marea del MES con 10,00 y con 10,25, y del ES con 10,25;
  - minutos RTH en que la direccion y el color del ES (10,25) coinciden con los del MES con 10,00 (lo que vio el operador) y con 10,25
    (la misma X: lo que queda es mercado).
Uso: python -I r26_08_hoy_parametro.py"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import r26_comun as C

C.prioridad_baja()
CORTE = C.ms_ny(C.HOY, 11, 48)


def estado(res, esc):
    F, P = res["fotos"], res["piv"][esc]
    dirr = F["dir_" + esc].to_numpy()
    npiv = F["npiv_" + esc].to_numpy()
    idx = np.clip(npiv - 1, 0, None)
    delta = F["cum"].to_numpy() - P["cum"].to_numpy()[idx]
    con = (dirr != 0) & (np.sign(delta) == dirr)
    return dirr, con, len(P)


def main():
    es = C.procesar("ES", C.HOY, 10.25, hasta_ms=CORTE)
    m25 = C.procesar("MES", C.HOY, 10.25, hasta_ms=CORTE)
    m00 = C.procesar("MES", C.HOY, 10.00, hasta_ms=CORTE)
    rth0, _ = C.rth(C.HOY)
    for esc in ("X", "4X"):
        de, ce, ne = estado(es, esc)
        d25, c25, n25 = estado(m25, esc)
        d00, c00, n00 = estado(m00, esc)
        n = min(len(de), len(d25), len(d00))
        de, ce, d25, c25, d00, c00 = de[:n], ce[:n], d25[:n], c25[:n], d00[:n], c00[:n]
        piv_rth = lambda r: int(((r["piv"][esc]["t_ext"] >= rth0) & (r["piv"][esc]["t_ext"] < CORTE)).sum())
        print("%s: giros confirmados en la jornada (desde 18:00 NY): ES 10,25 %d | MES 10,25 %d | MES 10,00 %d;  con el extremo en la rueda hasta 11:48 NY:"
              " ES %d, MES 10,25 %d, MES 10,00 %d" % ("OLA" if esc == "X" else "MAREA", ne, n25, n00, piv_rth(es), piv_rth(m25), piv_rth(m00)))
        for nom, dm, cm in (("MES con 10,00 (lo que vio el operador)", d00, c00), ("MES con 10,25 (la misma X)", d25, c25)):
            misma = (de != 0) & (de == dm)
            print("   ES 10,25 contra %-38s: %d minutos RTH; misma direccion %.1f %%; mismo color (con misma direccion) %.1f %%; ES sin y MES con %.1f %%"
                  % (nom, n, 100 * misma.mean(), 100 * (ce[misma] == cm[misma]).mean(), 100 * ((~ce[misma]) & cm[misma]).mean()))


if __name__ == "__main__":
    main()
