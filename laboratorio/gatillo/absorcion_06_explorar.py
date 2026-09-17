# -*- coding: utf-8 -*-
"""absorcion_06_explorar.py — corre las 12 variantes pre-registradas SOLO en las 12 sesiones de explorar."""
import sys, os, time; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd, base as B, absorcion_base as A, absorcion_variantes as V
t0 = time.time(); T = A.tablas(); E = B.explorar(T); filas = []
pd.set_option("display.width", 250); pd.set_option("display.max_columns", 40)
for nombre, regla in V.VARIANTES:
    f = A.disparos(E, regla); r = A.resumen(f, nombre); r["z_dia8"] = round(A.z_por_dia(f, 8), 2) if len(f) else np.nan; filas.append(r)
    f.to_parquet(os.path.join(A.MI_CACHE, "explorar-%s.parquet" % nombre.split()[1]))
    print("%-22s hecho en %.0f s" % (nombre, time.time() - t0), flush=True)
R = pd.DataFrame(filas)
print("\nempate: +-8 -> 55.3 | +-5 -> 58.5 | +-12 -> 53.5")
print(R[["nombre", "disparos", "n8", "ac8", "z8", "z_dia8", "dias8", "neto8", "n8L", "ac8L", "n8C", "ac8C"]].to_string(index=False))
print(R[["nombre", "n5", "ac5", "z5", "dias5", "neto5", "n12", "ac12", "z12", "dias12", "neto12", "m10", "m30", "m60", "m300"]].to_string(index=False))
R.to_csv(os.path.join(A.MI_CACHE, "explorar-resumen.csv"), index=False)
