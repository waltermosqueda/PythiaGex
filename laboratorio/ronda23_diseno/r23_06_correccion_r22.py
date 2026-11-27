# -*- coding: utf-8 -*-
"""RONDA 23, paso 6 (POST HOC): CORRECCION A LA R22 - el 'elastico' contra una referencia al azar bien hecha.
La R22 comparo el 'extremo + X/2 antes que el VWAP' real (41,3 %) con la formula del paseo al azar entre dos metas FIJAS (48,9 %).
La formula ignora dos cosas: (1) la rueda se corta a las 16:00 (muchos puntos no llegan a ninguna de las dos metas) y (2) el VWAP se mueve
hacia el precio. Aca, en los mismos puntos (10:00-15:30, cierre de cada vela de 5 min), se mide la misma pregunta como bracket F1 (stop X/2
detras del extremo del dia; objetivo el VWAP que se mueve; regla conservadora) y su referencia al azar con los mismos caminos reflejados.
Uso: python -I r23_06_correccion_r22.py -> salidas/correccion_r22.txt"""
import os, pickle, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r23_comun as K
import r23_sim as S
import r23_03_variantes as V

L = []
def W(s=""):
    print(s, flush=True); L.append(s)

def main():
    t0 = time.time(); K.frenar_si_falta_ram(2.0)
    B = pickle.load(open(os.path.join(K.SCR, "ruedas5.pkl"), "rb"))["ruedas"]; P = pd.read_parquet(os.path.join(K.SCR, "puntos5.parquet"))
    Bc = pickle.load(open(os.path.join(K.SCR, "ruedas5_cinta.pkl"), "rb"))["ruedas"]; Pc = pd.read_parquet(os.path.join(K.SCR, "puntos5_cinta.parquet"))
    W("RONDA 23 - CORRECCION A LA R22 (post hoc) %s" % time.strftime("%Y-%m-%d %H:%M"))
    W("pregunta de la R22: desde cada cierre de vela (10:00-15:30), con el precio de un lado del VWAP, que llega antes: el extremo del dia + X/2 o el VWAP")
    for nom, ru, pts in (("diseno MES", B, P[P.muestra == "diseno"]), ("chequeo MES", B, P[P.muestra == "chequeo"]), ("todo el ano MES (las 242 de la R22)", B, P[P.muestra.isin(["diseno", "chequeo"])]), ("cinta ES (velas de la cinta)", Bc, Pc)):
        d = pts[(pts.s != 0) & (pts.min_dia >= 600) & (pts.min_dia <= 930)]
        r = S.correr(ru, d, V.BRACKETS["F1"], M=200)
        st_real = r.mot_real.isin([1, 3]); ob_real = r.mot_real == 2; fin_real = r.mot_real == 0
        dstop = (r["stop"] - r["c"]).abs(); dvw = (r["c"] - r["vw"]).abs()
        f_ext = dvw / (dstop + dvw)
        W(""); W("  %s: %d puntos en %d ruedas" % (nom, len(r), r.dia.nunique()))
        W("    REAL:  extremo+X/2 primero %.1f %% | VWAP primero %.1f %% | ninguno antes de las 16:00 %.1f %%" % (100 * st_real.mean(), 100 * ob_real.mean(), 100 * fin_real.mean()))
        W("    AZAR (mismos caminos reflejados, mismo cierre, VWAP que se mueve): extremo primero %.1f %% | VWAP primero %.1f %% | ninguno %.1f %%" % (
            100 * r.azar_stop.mean(), 100 * r.azar_obj.mean(), 100 * (1 - r.azar_stop - r.azar_obj).mean()))
        W("    FORMULA de dos metas fijas sin cierre (la de la R22): extremo primero %.1f %%" % (100 * f_ext.mean()))
        for a_, b_, h in ((600, 690, "10:00-11:30"), (691, 780, "11:35-13:00"), (781, 930, "13:05-15:30")):
            m = (r.min_dia >= a_) & (r.min_dia <= b_)
            W("      %s real %.1f %% / azar %.1f %% / formula %.1f %% (extremo primero) | VWAP primero real %.1f %% / azar %.1f %%" % (
                h, 100 * st_real[m].mean(), 100 * r.azar_stop[m].mean(), 100 * f_ext[m].mean(), 100 * ob_real[m].mean(), 100 * r.azar_obj[m].mean()))
    open(os.path.join(K.SAL, "correccion_r22.txt"), "w", encoding="utf-8").write("\n".join(L))
    W("%.1f s" % (time.time() - t0))

if __name__ == "__main__":
    main()
