# -*- coding: utf-8 -*-
"""ZONAS, paso 9b: C4 (= C1 desde las 10:30) con las salidas que faltaban (S6 y S1 ya estan registradas como Z13_C1_desde_10:30)."""
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zonas_comun as Z
from z09_candidatas import REGLAS
df = Z.cargar()
f, par = REGLAS["C4_banda_respetada+iceberg_desde_10:30"]
Z.evaluar("Z9_C4_banda_respetada+iceberg_desde_10:30", "a (banda 2 sd, reversion) - candidata", par, f(df), ("S2", "S3", "S4", "S4b", "S5"),
          nota="candidata final (S6 y S1 en Z13_C1_desde_10:30)")
Z.evaluar("Z9_C4_banda_respetada+iceberg_desde_10:30", "a (banda 2 sd, reversion) - candidata", par, f(df), ("S6", "S1"), registrar=False)
