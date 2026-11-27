# -*- coding: utf-8 -*-
"""RONDA 20, paso 1b: resumen de la tabla por minuto (cobertura, antiguedad del dato, cruces del barrido, base). No mira resultados.
Uso: python -I r20_01b_resumen_tabla.py"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import pandas as pd

import r20_comun as C

t = pd.read_parquet(os.path.join(C.SCR, "gex_minuto.parquet"))
t["valido"] = t.hay_cadena & (t.edad_s <= 3600)
print("filas %d, ruedas %d, con cadena %d (%.1f %%), validas (dato <= 60 min) %d (%.1f %%)" % (
    len(t), t.dia.nunique(), t.hay_cadena.sum(), 100 * t.hay_cadena.mean(), t.valido.sum(), 100 * t.valido.mean()))
print("columnas:", ", ".join(t.columns))
r = t.groupby("dia").agg(muestra=("muestra", "first"), X=("X", "first"), con_cadena=("hay_cadena", "mean"), validas=("valido", "mean"),
                         edad_med_s=("edad_s", "median"), edad_max_min=("edad_s", lambda s: s.max() / 60),
                         base_med=("base", "median"), base_arch_med=("base_arch", "median"), cruces_med=("n_cruces", "median"),
                         fuente=("fuente", lambda s: s.dropna().value_counts().idxmax() if s.notna().any() else None))
r["con_cadena"] = (100 * r.con_cadena).round(0); r["validas"] = (100 * r.validas).round(0)
print(r.round(2).to_string())
v = t[t.valido]
print("\nEdad del dato en minutos validos: mediana %.0f s, p10 %.0f, p90 %.0f, > 30 min %.1f %%" % (
    v.edad_s.median(), v.edad_s.quantile(0.1), v.edad_s.quantile(0.9), 100 * (v.edad_s > 1800).mean()))
print("Cruces del barrido (+-250 SPX): 0 %.1f %%, 1 %.1f %%, 2 %.1f %%, 3+ %.1f %%" % tuple(
    100 * np.mean(f(v.n_cruces)) for f in (lambda s: s == 0, lambda s: s == 1, lambda s: s == 2, lambda s: s >= 3)))
print("Mensual/trimestral dentro de la ventana: %.1f %% de los minutos validos" % (100 * v.mensual_en_ventana.mean()))
