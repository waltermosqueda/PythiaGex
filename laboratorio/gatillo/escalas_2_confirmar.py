# -*- coding: utf-8 -*-
"""Paso 2: el finalista (S15m) UNA vez en confirmacion, con su placebo (200 sorteos, misma sesion y media hora, mismos lados)."""
import sys, time
import numpy as np, pandas as pd
import escalas_lib as L
B = L.B
pd.set_option("display.width", 250); pd.set_option("display.max_columns", 40)
FINALISTAS = ["S15m"]
t0 = time.time(); T = B.todas_las_sesiones(); C = B.confirmar(T); U = L.cargar_umbrales()
R, det = L.juzgar_todo(C, U, variantes=FINALISTAS)
print("CONFIRMACION (%d sesiones: %s .. %s)" % (len(C), min(C), max(C)))
print(R.T.to_string())
for v in FINALISTAS:
    d = det[v]; lado = np.array(d["lado"]); dias = np.array(d["ses"])
    for (x, h) in L.BARRERAS:
        y = np.array(d["y"][(x, h)]); ok = y != 0; ac = float(((y[ok] * lado[ok]) > 0).mean())
        m, sd, acs = L.placebo(C, d, (x, h)); zp = (ac - m) / sd
        neto = (2 * ac - 1) * x - B.COSTO_PTS
        print("%s +-%d/%ds: n %d acierto %.1f %% | placebo %.1f %% +- %.1f -> z placebo %.2f | empate %.1f %% | neto por operacion %.2f pts" % (v, x, h, ok.sum(), 100 * ac, 100 * m, 100 * sd, zp, 100 * B.empate(x), neto))
    y = np.array(d["y"][(8, 600)]); ok = y != 0
    por_dia = pd.DataFrame(dict(dia=dias[ok], ok=(y[ok] * lado[ok]) > 0)).groupby("dia")["ok"].agg(["mean", "size"])
    print(por_dia.round(3).to_string())
print("tiempo %.1f s" % (time.time() - t0))
