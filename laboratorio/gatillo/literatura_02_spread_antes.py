# -*- coding: utf-8 -*-
"""
literatura_02_spread_antes.py — comprobacion chica (pre-registrada en resultados/literatura.md): el spread de MNQ EN REPOSO
(la punta ANTES de cada orden agresora), en ticks, en rueda, para UNA sesion de explorar. Lee solo 4 columnas con filtro de parquet.
"""
import sys, os
sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd
import base as B

SESION = "2026-09-03"
pq = os.path.join(B.CACHE, "cinta.parquet")
d = pd.read_parquet(pq, columns=["t", "abid", "aask", "rueda", "vol"], filters=[("sesion", "==", SESION)])
d = d[d["rueda"]]
sp = ((d["aask"] - d["abid"]) / B.TICK).round()
ok = (sp >= 1) & (sp <= 8)
print("sesion", SESION, "| ordenes en rueda:", len(d), "| con punta sana: %.1f %%" % (100 * ok.mean()))
print("spread ANTES de cada orden, en ticks (%% de las ordenes):")
print((sp[ok].value_counts(normalize=True).sort_index() * 100).round(1).to_string())
print("spread medio antes de la orden: %.2f ticks = %.3f pts" % (sp[ok].mean(), sp[ok].mean() * B.TICK))
# por segundo: el spread antes de la PRIMERA orden de cada segundo (lo mas parecido a 'en reposo')
s = d.loc[ok].groupby(d.loc[ok, "t"].dt.floor("s")).first()
sp1 = ((s["aask"] - s["abid"]) / B.TICK).round()
print("\nspread antes de la PRIMERA orden de cada segundo (%% de los segundos):")
print((sp1.value_counts(normalize=True).sort_index() * 100).round(1).to_string())
print("medio: %.2f ticks" % sp1.mean())
