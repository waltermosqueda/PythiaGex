# -*- coding: utf-8 -*-
"""Informe de cal_01.py (eleccion y examen del PREREGISTRO_CALIBRACION.md) leyendo cal_resumen.csv y cal_movimientos.csv:
cal_01.py calculo todo y se corto al imprimir (dtype object en una fila). Misma logica, sin recalcular."""
import hashlib, sys
from pathlib import Path
import numpy as np
import pandas as pd
sys.path.insert(0, str(Path(__file__).resolve().parent))
import t_lib as T
LAB = T.LAB
p = T.Salida("cal_01_salida.txt")
p("CALIBRACION CORRIENTE (informe). SHA-256 PREREGISTRO_CALIBRACION.md:",
  hashlib.sha256((LAB / "PREREGISTRO_CALIBRACION.md").read_bytes()).hexdigest())
p("\n".join(x for x in open(LAB / "cal_01_consola.txt", encoding="utf-8", errors="replace").read().split("Traceback")[0]
           .splitlines() if not x.endswith(" listo")))
BASE = "F1 ST(10,3)"
Rz = pd.read_csv(LAB / "cal_resumen.csv")
movs = pd.read_csv(LAB / "cal_movimientos.csv")

D = Rz[Rz.tramo == "DISENO"].set_index("cand").drop(columns="tramo").astype(float)
E = Rz[Rz.tramo == "EXAMEN"].set_index("cand").drop(columns="tramo").astype(float)
b = D.loc[BASE]
pd.set_option("display.width", 250)
cols = ["tarde20_med", "tarde20_vol_med", "min20_med", "antes_umbral_pct", "cens_pct", "tarde10_med", "cambios",
        "falsas2.5", "falsas5", "falsas10"]
p("\nBASE en DISEÑO:", b[cols].round(2).to_dict())
p("\nTODOS en DISEÑO, ordenados por puntos tarde (movimientos de 20):")
p(D.sort_values(["tarde20_med", "tarde20_vol_med"])[cols].round(2).to_string())

# eleccion
adm = D[(D.cambios <= b.cambios) & (D.falsas5 <= b.falsas5)]
p(f"\nadmisibles sin ruido (cambios <= {b.cambios:.2f} y vueltas falsas 5 pts <= {b.falsas5:.2f} por sesion): {len(adm)}")
p(adm.sort_values(["tarde20_med", "tarde20_vol_med", "falsas5"])[cols].round(2).to_string())
C = adm.sort_values(["tarde20_med", "tarde20_vol_med", "falsas5"]).index[0]
mejora = b.tarde20_med - D.loc[C, "tarde20_med"]
p(f"\nELEGIDO C = {C} (mejora de mediana en DISEÑO {mejora:.2f} pts; pide >= 1)")
for tol in (1.10, 1.25, 1.50):
    f_ = D[(D.cambios <= tol * b.cambios) & (D.falsas5 <= tol * b.falsas5)].sort_values(["tarde20_med", "tarde20_vol_med"])
    p(f"frontera ruido <= {int(round((tol - 1) * 100))} %: {f_.index[0]} -> " + str(f_.iloc[0][cols].round(2).to_dict()))

# examen
Mv = movs
pb = Mv[(Mv.cand == BASE) & (Mv.tramo == "EXAMEN")].set_index("mov").pts
pc = Mv[(Mv.cand == C) & (Mv.tramo == "EXAMEN")].set_index("mov").pts
d = []
for q in pb.index:
    x, y = pb[q], pc[q]
    if x == y:
        continue
    d.append(1 if x > y else -1)
from scipy.stats import binomtest
gan = sum(1 for z in d if z > 0)
pval = binomtest(gan, len(d), 0.5, alternative="greater").pvalue if d else 1.0
eb, ec = E.loc[BASE], E.loc[C]
p(f"\nEXAMEN ({int(eb.n20)} movimientos de 20, {E.loc[BASE, 'n20_vol']} en dias volatiles):")
p(f"  B {BASE}: " + str(eb[cols].round(2).to_dict()))
p(f"  C {C}: " + str(ec[cols].round(2).to_dict()))
p(f"  prueba de signos: C antes que B en {gan} de {len(d)} movimientos distintos, p = {pval:.4f}")
ok1 = pval <= 0.05 and ec.tarde20_med < eb.tarde20_med
ok2 = ec.cambios <= 1.10 * eb.cambios and ec.falsas5 <= 1.10 * eb.falsas5
p(f"  1 retraso: {'PASA' if ok1 else 'NO PASA'}; 2 ruido (<= 110 %): {'PASA' if ok2 else 'NO PASA'} "
  f"(cambios {ec.cambios:.2f} vs {1.1 * eb.cambios:.2f}; falsas {ec.falsas5:.2f} vs {1.1 * eb.falsas5:.2f})")
p("EXAMEN, todos (descriptivo):")
p(E.sort_values("tarde20_med")[cols].round(2).to_string())
p.close()
