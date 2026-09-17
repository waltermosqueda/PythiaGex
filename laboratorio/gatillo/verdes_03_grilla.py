# -*- coding: utf-8 -*-
"""
verdes_03_grilla.py — LO DE HOY SE REPITE? Las verdes fuertes de la capa NQ son, casi siempre, los dos STRIKES de las opciones de NQ que encierran al
precio (la gamma es maxima al dinero; hoy: 29.450 y 29.425 del contrato U6 = 29.745,8 y 29.720,9 en Z6). El historial exacto de esas rayas solo existe
desde el 16-09, pero los strikes son una grilla FIJA y conocida de antemano: multiplos de 25 puntos en precio del contrato sobre el que cotizan las opciones.
Entonces la pregunta del operador se puede llevar a TODAS las sesiones con cinta (34): cuando el precio llega a un strike viniendo de lejos,
rebota mas que cuando llega a una raya cualquiera (la misma grilla corrida 12,5 puntos)?

Medida (igual que verdes_02): toque = venia de >= 10 pts y llega a <= 1,5 pts; resultado DESDE LA RAYA: +G a favor antes que -S en contra, 15 min.
Sin elegir nada despues de mirar: grillas 25 / 50 / 100, placebo = misma grilla corrida medio paso, todas las sesiones, solo rueda.
"""
import os, sys
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B
from rayas_base import barrera_asim

TOL, LEJOS, VENTANA = 1.5, 10.0, 1800
REGLAS = ((12, 6), (10, 5), (15, 6), (20, 8), (8, 8))
# corrimiento de la grilla de strikes respecto del precio de la cinta: 0 cuando la cinta es del mismo contrato que las opciones (U6 hasta el 14-09);
# del 15 al 17-09 la cinta es Z6 y las opciones semanales seguian sobre U6: corrimiento = Z6 - U6 (medido en las rayas vivas: multiplo de 25 - 4,2)
CORRIMIENTO = {"2026-09-16": -4.2, "2026-09-17": -4.2}
SALTEAR = {"2026-09-15"}   # cinta Z6 sin medicion propia del spread ese dia: no se adivina


def toques_grilla(seg, paso, corr, delta=0.0):
    """Toques de la grilla de strikes (o de su placebo corrido 'delta'). Una raya es 'la de arriba' o 'la de abajo' del precio: siempre vigente."""
    alto = seg["alto"].to_numpy(); bajo = seg["bajo"].to_numpy(); ult = seg["ultimo"].to_numpy(); rueda = seg["rueda"].to_numpy()
    hueco = seg["hueco"].to_numpy() if "hueco" in seg else np.zeros(len(seg), bool); n = len(ult); out = []
    lo = np.floor((np.nanmin(bajo) - corr - delta) / paso) * paso; hi = np.ceil((np.nanmax(alto) - corr - delta) / paso) * paso
    ar = np.arange(n)
    for K in np.arange(lo, hi + paso, paso):
        L = K + corr + delta
        for lado in (1, -1):
            if lado > 0: lejos_m = bajo >= L + LEJOS; zona = bajo <= L + TOL
            else: lejos_m = alto <= L - LEJOS; zona = alto >= L - TOL
            entra = zona & ~np.concatenate(([False], zona[:-1]))
            ult_lejos = np.maximum.accumulate(np.where(lejos_m, ar, -1)); ult_entra = np.concatenate(([-1], np.maximum.accumulate(np.where(entra, ar, -1))[:-1]))
            ok = entra & rueda & (ult_lejos >= 0) & (ar - ult_lejos <= VENTANA) & (ult_lejos > ult_entra)
            for i in np.flatnonzero(ok):
                if hueco[max(0, i - 600):i + 900].any(): continue
                j1 = min(n, i + 901)
                if lado > 0: fav = np.flatnonzero(alto[i:j1] >= L + 10); fin = i + (fav[0] if len(fav) else j1 - i - 1); pen = L - bajo[i:fin + 1].min()
                else: fav = np.flatnonzero(bajo[i:j1] <= L - 10); fin = i + (fav[0] if len(fav) else j1 - i - 1); pen = alto[i:fin + 1].max() - L
                out.append((int(i), float(L), lado, float(max(0.0, pen)), K))
    if not out: return pd.DataFrame(columns=["i", "L", "lado", "traspaso", "K"])
    d = pd.DataFrame(out, columns=["i", "L", "lado", "traspaso", "K"]).sort_values("i").reset_index(drop=True)
    for G, S in REGLAS:
        y, _ = barrera_asim(seg, d["i"].to_numpy(), d["lado"].to_numpy(), G, S, 900, entrada=d["L"].to_numpy()); d["r%d_%d" % (G, S)] = y
    return d


def tasa(d, G, S):
    y = d["r%d_%d" % (G, S)]; n = int((y != 0).sum())
    return (float((y[y != 0] > 0).mean()) if n else np.nan), n


if __name__ == "__main__":
    T = dict(B.todas_las_sesiones()); T.update(B.reserva())
    print("%d sesiones con cinta (%s a %s). Azar puro de +12/-6 = 33,3 %%.\n" % (len(T), min(T), max(T)))
    for paso in (25.0, 50.0, 100.0):
        filas = []; R = []; P = []
        for s in sorted(T):
            if s in SALTEAR: continue
            seg = T[s]; corr = CORRIMIENTO.get(s, 0.0)
            r = toques_grilla(seg, paso, corr); p = toques_grilla(seg, paso, corr, delta=paso / 2.0); r["sesion"] = s; p["sesion"] = s; R.append(r); P.append(p)
            (pr, nr), (pp, npl) = tasa(r, 12, 6), tasa(p, 12, 6)
            filas.append((s, nr, pr, npl, pp, r["traspaso"].median() if len(r) else np.nan, p["traspaso"].median() if len(p) else np.nan))
        R = pd.concat(R, ignore_index=True); P = pd.concat(P, ignore_index=True)
        print("=" * 100); print("GRILLA DE %d PUNTOS (strikes) contra la misma grilla corrida %.1f (placebo)" % (paso, paso / 2))
        for G, S in REGLAS:
            (pr, nr), (pp, npl) = tasa(R, G, S), tasa(P, G, S); se = np.sqrt(pr * (1 - pr) / nr + pp * (1 - pp) / npl)
            print("   +%d/-%d desde la raya: STRIKES %.1f %% (n %d) | PLACEBO %.1f %% (n %d) | diferencia %+.1f pts, z %+.2f | azar puro %.1f %% | valor esperado en el strike %+.2f pts/toque (sin costo)" % (
                G, S, 100 * pr, nr, 100 * pp, npl, 100 * (pr - pp), (pr - pp) / se, 100.0 * S / (G + S), pr * G - (1 - pr) * S))
        print("   traspaso mediano: strikes %.2f pts | placebo %.2f pts" % (R["traspaso"].median(), P["traspaso"].median()))
        f = pd.DataFrame(filas, columns=["sesion", "n", "strike", "n_pl", "placebo", "trasp", "trasp_pl"]); f["dif"] = f["strike"] - f["placebo"]
        print("   por sesion (+12/-6): dias con el strike ARRIBA del placebo: %d de %d | diferencia media por dia %+.1f pts (t %.2f)" % (
            int((f["dif"] > 0).sum()), int(f["dif"].notna().sum()), 100 * f["dif"].mean(), f["dif"].mean() / (f["dif"].std(ddof=1) / np.sqrt(f["dif"].notna().sum()))))
        if paso == 25.0:
            print(f.assign(strike=(100 * f["strike"]).round(0), placebo=(100 * f["placebo"]).round(0), dif=(100 * f["dif"]).round(0), trasp=f["trasp"].round(1), trasp_pl=f["trasp_pl"].round(1)).to_string(index=False))
