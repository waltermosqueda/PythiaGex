# -*- coding: utf-8 -*-
"""ZONAS, paso 13 (post-hoc, sesgo de seleccion declarado): las perdedoras de C1 se juntan entre las 10:00 y las 10:35 en dias con
empuje de iniciativa a la manana (banda de 2 sd todavia angosta). Dos variantes: C1 desde las 10:30 (con el balance inicial ya
formado) y C1 con banda ancha (desvio del VWAP RTH >= X en la vuelta)."""
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import zonas_comun as Z
from z09_candidatas import REGLAS

df = Z.cargar()
f1, par1 = REGLAS["C1_banda_respetada+iceberg"]
m = f1(df)
Z.evaluar("Z13_C1_desde_10:30", "a (banda 2 sd, reversion)", par1 + " + entradas desde las 10:30", m, ("S6", "S1"), h_ini=3600,
          nota="post-hoc: las perdedoras de C1 se juntan 10:00-10:35")
Z.evaluar("Z13_C1_banda_ancha", "a (banda 2 sd, reversion)", par1 + " + desvio del VWAP RTH en la vuelta >= X (2 sd >= 2X)",
          m & (df.vwap_rth_sd >= df.X).to_numpy(), ("S6", "S1"), nota="post-hoc: banda angosta a la manana")
