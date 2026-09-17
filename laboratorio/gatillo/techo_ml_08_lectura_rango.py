# -*- coding: utf-8 -*-
"""techo_ml_08_lectura_rango.py — DESCRIPTIVO: la parte no direccional llevada a algo que el operador pueda leer sin modelo.
El xgb (V09) saca casi todo de la volatilidad reciente + el reloj; el control con rangos previos + reloj da casi lo mismo. Entonces:
  tabla 1: segun el rango de los ULTIMOS 5 min (lo que ya se ve en el grafico), como vienen los PROXIMOS 5 min y cuanto tarda la apuesta +-8;
  tabla 2: lo mismo por media hora de reloj (hora de Nueva York).
Se muestra explorar y confirmar por separado para ver que repite."""
import numpy as np, pandas as pd
import techo_ml_lib as L, base as B

T = B.todas_las_sesiones(); N = B.niveles_m1()
D = {"explorar": L.tablero(B.explorar(T), N, "explorar"), "confirmar": L.tablero(B.confirmar(T), N, "confirmar")}
cortes = [0, 16, 24, 32, 48, 1e9]; nombres = ["< 16", "16-24", "24-32", "32-48", ">= 48"]
for k, df in D.items():
    print("\n%s — segun el rango de los ULTIMOS 5 min (pts): %% del tiempo | rango mediano de los PROXIMOS 5 min | %% con proximo rango >= 16 | mediana de s hasta resolver +-8 | %% resuelto en <= 60 s | %% sin resolver a los 180 s" % k.upper())
    b = pd.cut(df["rng_300"], cortes, labels=nombres, right=False)
    for nm in nombres:
        m = (b == nm).to_numpy(); R = df["R300"].to_numpy()[m]; t8 = df["t8"].to_numpy(dtype="float64")[m]
        print("   %-6s %4.0f %% | %5.1f | %3.0f %% | %4.0f s | %3.0f %% | %3.0f %%" % (nm, 100 * m.mean(), np.nanmedian(R), 100 * np.nanmean(R >= 16), np.nanmedian(t8), 100 * np.mean(t8 <= 60), 100 * np.mean(~(t8 <= 180))))
for k, df in D.items():
    print("\n%s — por media hora (hora de Nueva York): rango mediano de 5 min | %% con rango >= 16 | mediana de s hasta resolver +-8" % k.upper())
    mm = (df["mins"].to_numpy() // 30).astype(int)
    for h in range(13):
        m = mm == h; R = df["R300"].to_numpy()[m]; t8 = df["t8"].to_numpy(dtype="float64")[m]; ini = 9 * 60 + 30 + 30 * h
        print("   %02d:%02d  %5.1f | %3.0f %% | %4.0f s" % (ini // 60, ini % 60, np.nanmedian(R), 100 * np.nanmean(R >= 16), np.nanmedian(t8)))
