# -*- coding: utf-8 -*-
"""techo_ml_01_tablero.py — arma el tablero de rasgos (grilla de 15 s) SOLO de las sesiones de explorar y lo deja cacheado.
Solo describe estructura (filas, faltantes, base de la barrera); no ajusta nada."""
import sys, time
import numpy as np, pandas as pd
import techo_ml_lib as L, base as B

t0 = time.time()
T = B.todas_las_sesiones(); N = B.niveles_m1(); E = B.explorar(T)
df = L.tablero(E, N, "explorar"); todos, sin_gamma = L.columnas(df)
print("tablero explorar: %d filas, %d sesiones, %d rasgos (%d sin gamma) | %.0f s" % (len(df), df["sesion"].nunique(), len(todos), len(sin_gamma), time.time() - t0))
print("filas por sesion:", df.groupby("sesion").size().to_dict())
na = df[todos].isna().mean().sort_values(ascending=False); print("rasgos con faltantes (%):"); print((100 * na[na > 0]).round(1).to_string())
for x, h in L.BARRERAS:
    y = df["y%d" % x]; print("barrera +-%d/%d: resueltas %.1f %% | de las resueltas, arriba %.1f %% | empate %.1f %%" % (x, h, 100 * (y != 0).mean(), 100 * (y[y != 0] > 0).mean(), 100 * B.empate(x)))
print("arriba %% por sesion (+-8):", (100 * df[df["y8"] != 0].groupby("sesion")["y8"].apply(lambda s: (s > 0).mean())).round(1).to_dict())
print("R300 (rango de los proximos 300 s, pts): p10 %.1f  p50 %.1f  p90 %.1f" % tuple(df["R300"].quantile([.1, .5, .9])))
print("t8 (segundos hasta resolver +-8): mediana %.0f" % df["t8"].median())
inf = np.isinf(df[todos].to_numpy(dtype="float64")).sum(); print("infinitos:", int(inf))
print(df[todos].describe().T[["mean", "std", "min", "max"]].round(3).to_string())
