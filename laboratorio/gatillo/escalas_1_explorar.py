# -*- coding: utf-8 -*-
"""Paso 1: las 12 variantes pre-registradas, SOLO en las sesiones de exploracion."""
import sys, time
import numpy as np, pandas as pd
import escalas_lib as L
B = L.B
pd.set_option("display.width", 250); pd.set_option("display.max_columns", 40)
t0 = time.time(); T = B.todas_las_sesiones(); E = B.explorar(T); U = L.cargar_umbrales()
R, det = L.juzgar_todo(E, U)
print("EXPLORACION (%d sesiones: %s .. %s)" % (len(E), min(E), max(E)))
print("empates: +-8 %.1f %% | +-5 %.1f %% | +-12 %.1f %%" % (100 * B.empate(8), 100 * B.empate(5), 100 * B.empate(12)))
cols8 = ["variante", "disparos", "b8_n", "b8_ac", "b8_z", "b8_t_dias", "b8_dias", "b8_peor"]
print("\nBARRERA PRINCIPAL +-8 / 600 s"); print(R[cols8].to_string(index=False))
print("\nSECUNDARIAS"); print(R[["variante", "b5_n", "b5_ac", "b5_z", "b5_dias", "b12_n", "b12_ac", "b12_z", "b12_dias"]].to_string(index=False))
print("\nMOVIMIENTO MEDIO FIRMADO (puntos) a 10 / 30 / 60 s"); print(R[["variante", "mov10", "mov30", "mov60"]].to_string(index=False))
R.to_csv(L.os.path.join(L.TMP, "explorar.csv"), index=False)
print("tiempo %.1f s" % (time.time() - t0))
