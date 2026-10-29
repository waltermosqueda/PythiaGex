# -*- coding: utf-8 -*-
"""Eleccion y examen doble de la ronda 2 (PREREGISTRO_CALIBRACION_2.md secciones 4 y 5), leyendo cal3_resumen.csv y
cal3_movimientos.csv. Salida: cal_03b_salida.txt."""
import hashlib
import sys
from pathlib import Path

import numpy as np
import pandas as pd
from scipy.stats import binomtest

sys.path.insert(0, str(Path(__file__).resolve().parent))
import t_lib as T

LAB = T.LAB
p = T.Salida("cal_03b_salida.txt")
p("RONDA 2: eleccion y examen. SHA-256 PREREGISTRO_CALIBRACION_2.md:",
  hashlib.sha256((LAB / "PREREGISTRO_CALIBRACION_2.md").read_bytes()).hexdigest())
R = pd.read_csv(LAB / "cal3_resumen.csv")
MV = pd.read_csv(LAB / "cal3_movimientos.csv")
BASE = "F1 ST(10,3)"
COLS = ["tarde", "tarde_vol", "cens", "tarde2", "cambios", "falsas5", "falsasN", "cambios_rueda", "falsas5_rueda", "falsasN_rueda"]
pd.set_option("display.width", 260)
pd.set_option("display.max_rows", 300)


def tabla(serie, tramo):
    return R[(R.serie == serie) & (R.tramo == tramo)].set_index("cand")


D, E, Q = tabla("MES", "DISENO"), tabla("MES", "EXAMEN"), tabla("MNQ", "MNQ")
b = D.loc[BASE]
p("\nBASE en DISEÑO:", {k: round(float(b[k]), 2) for k in COLS})

# curva F1 (envolvente inferior de ST(n,m), n en {10,60}) e interpolacion en la cantidad de cambios
F = D[D.fam == "F1"][["cambios", "tarde"]].sort_values("cambios")
env = []
for x in F.itertuples():
    if not any((y.cambios <= x.cambios) and (y.tarde < x.tarde) for y in F.itertuples() if y.Index != x.Index):
        env.append((x.cambios, x.tarde))
env = sorted(env)
ex, ey = np.array([e[0] for e in env]), np.array([e[1] for e in env])
p("curva F1 (cambios por sesion -> puntos tarde):", ", ".join(f"{a:.1f}->{c:.2f}" for a, c in env))
D = D.copy()
D["ganancia"] = np.interp(D.cambios, ex, ey) - D.tarde

nuevos = D[D.fam != "F1"]
p("\nMejor de cada familia por ganancia contra la curva (DISEÑO):")
for fam, g in nuevos.groupby("fam"):
    r = g.sort_values("ganancia", ascending=False).iloc[0]
    p(f"  {fam}: {r.name} ganancia {r.ganancia:+.2f} | " + ", ".join(f"{k} {r[k]:.2f}" for k in COLS))
p("\nTop 15 por ganancia contra la curva (DISEÑO):")
p(nuevos.sort_values("ganancia", ascending=False).head(15)[["ganancia"] + COLS].round(2).to_string())

dom = nuevos[(nuevos.tarde <= b.tarde - 1.0) & (nuevos.cambios <= b.cambios) & (nuevos.falsas5 <= b.falsas5)
             & (nuevos.falsasN <= b.falsasN)]
p(f"\nDOMINAN a la base en DISEÑO (>= 1 pt antes y ruido <= en las tres medidas): {len(dom)}")
if len(dom):
    p(dom.sort_values(["tarde", "tarde_vol", "falsasN"])[["ganancia"] + COLS].round(2).to_string())


def examen(C, tabla_, serie, tramo):
    mb = MV[(MV.serie == serie) & (MV.cand == BASE) & (MV.tramo == tramo)].set_index("mov").pts
    mc = MV[(MV.serie == serie) & (MV.cand == C) & (MV.tramo == tramo)].set_index("mov").pts
    antes, despues = int((mb > mc).sum()), int((mb < mc).sum())
    pv = binomtest(antes, antes + despues, 0.5, alternative="greater").pvalue if antes + despues else 1.0
    eb, ec = tabla_.loc[BASE], tabla_.loc[C]
    ok1 = pv <= 0.05
    ok2 = all(ec[k] <= 1.10 * eb[k] for k in ("cambios", "falsas5", "falsasN"))
    d = (mb - mc)[np.isfinite(mb - mc)]
    p(f"  {serie} {tramo}: antes en {antes}, despues en {despues}, igual {int((mb == mc).sum())} de {len(mb)}; p {pv:.4f};"
      f" media de la diferencia {d.mean():+.2f} pts (finitos) -> retraso {'PASA' if ok1 else 'NO PASA'}")
    p(f"     base: " + ", ".join(f"{k} {eb[k]:.2f}" for k in COLS))
    p(f"     C   : " + ", ".join(f"{k} {ec[k]:.2f}" for k in COLS))
    p(f"     ruido <= 110 % en cambios, falsas 5 y normalizadas: {'PASA' if ok2 else 'NO PASA'}")
    return ok1 and ok2


if len(dom):
    C = dom.sort_values(["tarde", "tarde_vol", "falsasN"]).index[0]
    p(f"\nELEGIDO C = {C}")
    a = examen(C, E, "MES", "EXAMEN")
    q = examen(C, Q, "MNQ", "MNQ")
    p(f"\nVEREDICTO: {'PASA LOS DOS EXAMENES' if a and q else 'NO PASA'} (MES {'si' if a else 'no'}, MNQ {'si' if q else 'no'})")
else:
    p("\nVEREDICTO: ningun mecanismo rompe la curva por 1 pt sin sumar ruido; no se cambia nada por esta ronda.")

p("\nDescriptivo: top 15 de DISEÑO por ganancia, en EXAMEN y en MNQ")
top = nuevos.sort_values("ganancia", ascending=False).head(15).index
for nom in top:
    p(f"  {nom:<32} EXAMEN " + ", ".join(f"{k} {E.loc[nom, k]:.2f}" for k in ("tarde", "cambios", "falsas5", "falsasN"))
      + " | MNQ " + ", ".join(f"{k} {Q.loc[nom, k]:.2f}" for k in ("tarde", "cambios", "falsas5", "falsasN")))
for nom in (BASE, "F1 ST(60,3)"):
    p(f"  {nom:<32} EXAMEN " + ", ".join(f"{k} {E.loc[nom, k]:.2f}" for k in ("tarde", "cambios", "falsas5", "falsasN"))
      + " | MNQ " + ", ".join(f"{k} {Q.loc[nom, k]:.2f}" for k in ("tarde", "cambios", "falsas5", "falsasN")))
p.close()
