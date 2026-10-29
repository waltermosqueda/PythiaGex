# -*- coding: utf-8 -*-
"""hoy_corriente.py — 29-10-2026. Sobre las velas del ultimo volcado de la Corriente 1.1 (todas las que ATAS tiene cargadas),
compara HOY la 1.0c (ATR 10 x 3, sin vuelta rapida) con la 1.1 (ATR 60 x 4 + vuelta rapida 2,5 ATR en 3 velas): cada vuelta,
y en cada movimiento de 20 pts o mas en 30 min (definicion del PREREGISTRO.md) cuantos puntos tarde se puso de su lado cada una.
Horas en Argentina (UTC-3) y en Nueva York (UTC-4 hasta el 01-11)."""
import glob
import os
import sys
from pathlib import Path

import numpy as np
import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parent))
from paridad_corriente import motor_atas

ruta = sys.argv[1] if len(sys.argv) > 1 else max(glob.glob(os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex", "corriente", "volcado-*.csv")), key=os.path.getmtime)
v = pd.read_csv(ruta)
v["t"] = pd.to_datetime(v.hora_utc)
h, l, c = v.h.values, v.l.values, v.c.values
viejo = motor_atas(h, l, c, 10, 3.0, False)["dir"]
nuevo = motor_atas(h, l, c, 60, 4.0, True, 2.5, 3)
assert (nuevo["dir"] == v["dir"].values).all(), "el volcado no es de la 1.1 por defecto"
hoy = (v.t >= pd.Timestamp("2026-10-28 22:00")).values      # sesion de hoy: desde las 18:00 NY de ayer
print(f"{os.path.basename(ruta)}: {len(v)} velas, hoy {hoy.sum()} ({v.t[hoy].iloc[0]} .. {v.t[hoy].iloc[-1]} UTC)")
ar = lambda i: (v.t.iloc[i] - pd.Timedelta(hours=3)).strftime("%H:%M")
for nom, d, rap in (("1.0c (10x3)", viejo, None), ("1.1 (60x4 + rapida)", nuevo["dir"], nuevo["rapida"])):
    idx = [i for i in np.nonzero(d[1:] != d[:-1])[0] + 1 if hoy[i]]
    rueda = [i for i in idx if 10 * 60 + 30 <= ((v.t.iloc[i] - pd.Timedelta(hours=3)).hour * 60 + (v.t.iloc[i] - pd.Timedelta(hours=3)).minute) <= 17 * 60]
    print(f"{nom}: {len(idx)} vueltas hoy, {len(rueda)} en la rueda (10:30-17:00 AR): " + ", ".join(
        f"{ar(i)}{'+' if d[i] > 0 else '-'}{('R' if rap[i] else 'L') if rap is not None else ''}@{c[i]}" for i in rueda))
# movimientos grandes de hoy (20 pts en <= 30 min, extremo local +-30 min)
tm = ((v.t - v.t.iloc[0]).dt.total_seconds() / 60).values
movs = []
for s in (1, -1):
    ult = -1e9
    for q in np.nonzero(hoy)[0]:
        lo, hi = np.searchsorted(tm, tm[q] - 30), np.searchsorted(tm, tm[q] + 30, side="right")
        if (s == 1 and l[q] > l[lo:hi].min()) or (s == -1 and h[q] < h[lo:hi].max()):
            continue
        fut = np.arange(q + 1, hi)
        alc = fut[(h[fut] - l[q] >= 20) if s == 1 else (h[q] - l[fut] >= 20)] if len(fut) else []
        if len(alc) == 0 or tm[q] - ult <= 30:
            continue
        ult = tm[q]
        movs.append((q, s, l[q] if s == 1 else h[q]))
print("movimientos de 20 pts o mas hoy:", len(movs))
for q, s, e in sorted(movs):
    txt = f"  {'suba' if s == 1 else 'bajada'} desde {e} ({ar(q)} AR):"
    for nom, d in (("1.0c", viejo), ("1.1", nuevo["dir"])):
        j = next((k for k in range(q, min(len(d), q + 121)) if d[k] == s), None)
        txt += f"  {nom} " + (f"a las {ar(j)} con {(c[j] - e) * s:.2f} pts hechos" if j is not None else "no se dio vuelta en 2 h")
    print(txt)
