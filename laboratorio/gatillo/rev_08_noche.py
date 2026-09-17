# -*- coding: utf-8 -*-
"""rev_08_noche.py — (7) DE NOCHE (fuera de 13:30-20:00 UTC): hay gatillos? cuantos por sesion y con que resultado. OJO: de noche la capa en vivo usa el libro por INTERES ABIERTO
cuando no hay volumen; el banco reconstruye SOLO el libro por volumen (con el volumen flaco de la noche). Por eso se corre dos veces: con las rayas reconstruidas (7 noches, NO son
las que se dibujaron) y con la ESTELA REAL (lo dibujado) de las dos noches que existen (16 y 17-09)."""
import os, sys
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rev_lib as R, verdes_lib as V
from rev_03_causal_y_estela import rayas_estela

T = R.sesiones(); F, _ = V.fotos("NQ"); P = dict(R.P0); PLC = (12.5, -12.5, 37.5, -37.5, 62.5, -62.5)
noche = lambda A: ~A.rueda
print("spread en reposo (rask - rbid) y movimiento: mediana de noche contra rueda")
for s, seg in T.items():
    sp = (seg["rask"] - seg["rbid"]); r = seg["rueda"]; m1 = seg["ultimo"].diff(60).abs()
    print("   %s: spread noche %.2f / rueda %.2f | |movimiento en 60 s| noche %.1f / rueda %.1f pts | ordenes por segundo noche %.1f / rueda %.1f | horas de noche con cinta %.1f" % (
        s, sp[~r & (seg["n"] > 0)].mean(), sp[r & (seg["n"] > 0)].mean(), m1[~r].mean(), m1[r].mean(), seg["n"][~r].mean(), seg["n"][r].mean(), (~r).sum() / 3600.0))
for nombre, rayas, dias in (("RECONSTRUIDAS por volumen (no es lo que se dibuja de noche)", None, None), ("ESTELA REAL (lo dibujado)", rayas_estela, ("2026-09-16", "2026-09-17"))):
    TT = T if dias is None else {s: T[s] for s in dias}
    for obj in (20, 12):
        res = R.correr(TT, F, P, obj, (0.0,) + PLC, rayas=rayas, donde_fn=noche); real = res[0.0]; plc = pd.concat([res[c] for c in PLC], ignore_index=True)
        print("\n[%s | NOCHE | objetivo %s]" % (nombre, obj)); print("   " + R.resumen(real, "VERDES de noche")); print("   " + R.resumen(plc, "PLACEBO +-12,5 37,5 62,5"))
        if len(real):
            print("   por sesion: " + " | ".join("%s %+.1f (n %d)" % (d[5:], g["neto"].sum(), len(g)) for d, g in real.groupby("dia")))
            h = real["t"].dt.hour; print("   por franja UTC: " + " | ".join("%s n %d neto %+.2f" % (k, len(g), g["neto"].mean()) for k, g in real.groupby(pd.cut(h, [-1, 5, 11, 13, 19, 23], labels=["00-06 Asia", "06-12 Europa", "12-13:30 pre", "(rueda)", "20-24 cierre/Asia"]), observed=True)))
# cobertura de rayas de noche
print("\ncobertura: % de segundos de NOCHE con raya vigente (foto de < 5 min)")
for s, seg in T.items():
    L2 = V.rayas_por_segundo(seg, F); r = seg["rueda"].to_numpy(); print("   %s reconstruidas %.0f %%" % (s, 100 * L2["d0"].notna().to_numpy()[~r].mean()), end="")
    if s in ("2026-09-16", "2026-09-17"):
        E = rayas_estela(seg, F); d = (E["d0"] - E["d1"]).to_numpy()[~r]; print(" | estela real %.0f %% | ancho del tunel dibujado de noche: mediana %.0f pts (de dia %.0f)" % (100 * E["d0"].notna().to_numpy()[~r].mean(), np.nanmedian(np.abs(d)), np.nanmedian(np.abs((E["d0"] - E["d1"]).to_numpy()[r]))), end="")
    print()
