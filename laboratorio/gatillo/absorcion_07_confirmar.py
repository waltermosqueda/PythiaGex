# -*- coding: utf-8 -*-
"""absorcion_07_confirmar.py — UNA sola corrida en las 8 sesiones de confirmar: el finalista R30 y, fuera de concurso, FALLA invertida.
Placebo de 200 sorteos (misma sesion, misma media hora, mismos lados, separados 60 s). Tambien el placebo de R30 en explorar, de contexto."""
import sys, os, time; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd, base as B, absorcion_base as A, absorcion_variantes as V
t0 = time.time(); T = A.tablas(); C = B.confirmar(T); E = B.explorar(T)
pd.set_option("display.width", 250); pd.set_option("display.max_columns", 40)


def FALLA_INV(s, seg): pos, lado = V.FALLA(s, seg); return pos, -lado


for nombre, regla in (("R30", V.R30), ("FALLA_INV (fuera de concurso)", FALLA_INV)):
    for rot, S in (("CONFIRMAR", C), ("explorar", E)):
        f = A.disparos(S, regla); r = A.resumen(f, nombre); print("\n=== %s | %s | disparos %d" % (nombre, rot, len(f)))
        print({k: r[k] for k in r if k != "nombre"})
        print("z por dia (+-8): %.2f" % A.z_por_dia(f, 8))
        ok = f["y8"] != 0; d = ((f.loc[ok, "y8"] * f.loc[ok, "lado"]) > 0).groupby(f.loc[ok, "sesion"]).agg(["mean", "size"]); print("por dia:", {k: "%.0f%% de %d" % (100 * v["mean"], v["size"]) for k, v in d.iterrows()})
        for x in (8, 5, 12):
            p = A.placebo(S, f, x=x, sorteos=200); print("placebo +-%d:" % x, p)
        f.to_parquet(os.path.join(A.MI_CACHE, "%s-%s.parquet" % (rot.lower(), nombre.split()[0])))
        print("(%.0f s)" % (time.time() - t0), flush=True)
