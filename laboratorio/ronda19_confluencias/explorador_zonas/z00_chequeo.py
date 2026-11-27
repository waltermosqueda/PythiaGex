# -*- coding: utf-8 -*-
"""Chequeo: mi calculo de contacto reproduce niveles_en_zona y barrida_y_vuelta de la base (sin registrar nada)."""
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import zonas_comun as Z
df = Z.cargar()
z = Z.contacto(df, "todos", 0, 0.25, lo_T=True)
b = Z.contacto(df, "todos", 1e-6, 0.5, vuelve=True)
print("zona: mia vs base, coincide en", (z == df["n_niveles_en_zona"]).mean(), "| difiere", int((z != df["n_niveles_en_zona"]).sum()))
print("barrida: mia vs base, coincide en", (b == df["n_barridas_y_vuelta"]).mean(), "| difiere", int((b != df["n_barridas_y_vuelta"]).sum()))
dd = df[(z != df["n_niveles_en_zona"])][["dia","hora_ny","lado","ext","X","zona_T","niveles_en_zona"]].head(10)
print(dd.to_string())
# chequeo del simulador: todas las vueltas, dia entero, max 999 por rueda, S1 sin restriccion de posicion?
idx = Z.tomar(df, np.ones(len(df), bool), "S1", max_dia=999, h_ini=0, h_fin=23400)
print("todas una por vez:", len(idx), "de", len(df))
print("horario 10:00-15:30:", int(((df.seg_rueda>=1800)&(df.seg_rueda<21600)).sum()))
