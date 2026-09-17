# -*- coding: utf-8 -*-
"""barridos_auditoria.py — tres controles de la maquinaria (no buscan nada):
 1. NADA MIRA ADELANTE: se corta la tabla en el segundo del disparo y se recalcula todo; el disparo tiene que seguir ahi, con el mismo lado.
 2. CONTROL POSITIVO: con un lado tramposo (el signo del movimiento de los 30 s siguientes) el acierto tiene que irse muy arriba de 50 %: prueba que
    la barrera y los indices estan bien alineados.
 3. CUANTO TARDA la apuesta del scalper en resolverse (segundos hasta tocar +-5 / +-8 / +-12 desde un segundo cualquiera de la rueda)."""
import time
import numpy as np, pandas as pd
import barridos_lib as L
B = L.B

t0 = time.time(); rng = np.random.default_rng(1)
Tt = B.todas_las_sesiones(); s = "2026-08-26"; d = Tt[s]
dis, f = L.disparos(d)

print("1. CAUSALIDAD (sesion %s): 4 disparos por variante, tabla cortada en t" % s)
mal = 0; tot = 0
for v, (i, lado) in dis.items():
    if len(i) == 0: continue
    for k in rng.choice(len(i), size=min(4, len(i)), replace=False):
        t = int(i[k]); dc = d.iloc[:t + 1]; m, l = L.candidatos(L.rasgos(dc))[v]
        ok = bool(m[t]) and int(np.asarray(l)[t]) == int(lado[k]); tot += 1; mal += (not ok)
        if not ok: print("   FALLA", v, d.index[t])
print("   %d de %d disparos se reproducen con la tabla cortada en su propio segundo" % (tot - mal, tot))

print("\n2. CONTROL POSITIVO (V01, lado tramposo = signo del movimiento de los 30 s siguientes)")
i, _ = dis["V01_BARRIDO_SEGUIR"]; p = d["ultimo"].to_numpy(); tr = np.sign(p[i + 30] - p[i]); y = B.barrera(d, 8, 600)[0][i]
print("  ", B.juzgar(y[tr != 0], tr[tr != 0], "tramposo", x=8))

print("\n3. SEGUNDOS HASTA RESOLVER la barrera desde un segundo cualquiera de la rueda (20 sesiones)")
for x, h in ((5, 300), (8, 600), (12, 900)):
    seg = []
    for ss, dd in Tt.items():
        hms = dd.index.strftime("%H:%M:%S"); e = dd["rueda"].to_numpy() & (hms >= L.INICIO_RUEDA) & (hms < L.FIN_RUEDA)
        sg = B.barrera(dd, x, h)[1][e]; seg.append(sg[::7])
    seg = np.concatenate(seg); fin = np.where(np.isinf(seg), np.nan, seg)
    print("   +-%d: p25 %3.0f s | mediana %3.0f s | p75 %3.0f s | resuelta en 60 s o menos: %.0f %% | sin resolver en %d s: %.1f %%"
          % (x, np.nanpercentile(fin, 25), np.nanmedian(fin), np.nanpercentile(fin, 75), 100 * np.mean(seg <= 60), h, 100 * np.mean(np.isinf(seg))))
print("\n%.0f s" % (time.time() - t0))
