# -*- coding: utf-8 -*-
"""RONDA 19, paso 6 (exploratorio, solo DISENO): tiene deriva el CONTEXTO? En chequeos cada 30 min (10:00..15:30), si el precio esta
fuera del area de ruido (Zarattini) se toma el lado de la ruptura y se mide el retorno hacia adelante a 30 y 60 min y al cierre,
en total y partido por marea a favor con agresores y lado del VWAP. Describe el regimen; no es una regla."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r19_comun as C, r19_02_motor as M

mu = sys.argv[1] if len(sys.argv) > 1 else "diseno"
dias = [d for d in C.dias("ES") if "2026-09-10" <= d <= C.DISENO_FIN] if mu == "diseno" else []
filas = []
for dia in dias:
    d = M.Dia("ES", dia); R = d.R; c = d.c; o = d.meta["rth_o"]; X = d.X
    cierre = c[-1]
    for tau in range(1800, 21601, 1800):
        t = tau - 1
        sig = R["ruido_sigma"][min(389, t // 60)]
        mov = c[t] - o
        lado = int(np.sign(mov)) if abs(mov) > sig else 0
        mdir = int(R["m_dir"][t]); mcon = (R["cum"][t] - R["m_c0"][t]) * mdir > 0
        vw = 1 if c[t] > R["vw_r"][t] else -1
        f30 = c[min(t + 1800, M.NR - 1)] - c[t]; f60 = c[min(t + 3600, M.NR - 1)] - c[t]; fc = cierre - c[t]
        filas.append(dict(dia=dia, hora=M.seg_hora(tau), X=X, lado=lado, marea_a_favor=int(lado != 0 and mdir == lado and mcon),
                          vwap_a_favor=int(lado != 0 and vw == lado), f30=f30 * (lado if lado else 1), f60=f60 * (lado if lado else 1),
                          fc=fc * (lado if lado else 1), f30X=f30 * (lado if lado else 1) / X, fcX=fc * (lado if lado else 1) / X))
df = pd.DataFrame(filas)
fuera = df[df.lado != 0]
def lin(nombre, g):
    if len(g) == 0: return
    dd = g.groupby("dia")["fc"].mean()
    print("%-55s n %4d (%2d ruedas) | 30 min %+.2f pts (%.0f %% a favor) | 60 min %+.2f | al cierre %+.2f pts = %+.2f X (%.0f %% a favor) | t por rueda al cierre %+.2f" % (
        nombre, len(g), g.dia.nunique(), g.f30.mean(), 100 * (g.f30 > 0).mean(), g.f60.mean(), g.fc.mean(), g.fcX.mean(), 100 * (g.fc > 0).mean(),
        dd.mean() / (dd.std(ddof=1) / np.sqrt(len(dd))) if len(dd) > 2 and dd.std() > 0 else 0))
print("chequeos totales", len(df), "| fuera del ruido", len(fuera), "(%.0f %%)" % (100 * len(fuera) / len(df)))
lin("fuera del ruido, lado de la ruptura", fuera)
lin("  + marea a favor con agresores", fuera[fuera.marea_a_favor == 1])
lin("  + marea NO a favor", fuera[fuera.marea_a_favor == 0])
lin("  + VWAP a favor", fuera[fuera.vwap_a_favor == 1])
lin("  + marea y VWAP a favor", fuera[(fuera.marea_a_favor == 1) & (fuera.vwap_a_favor == 1)])
for h in ("10:00:00", "10:30:00", "11:00:00", "12:00:00", "13:00:00", "14:00:00", "15:00:00", "15:30:00"):
    lin("  hora " + h, fuera[fuera.hora == h])
dentro = df[df.lado == 0]
print("dentro del ruido: n %d | |retorno al cierre| medio %.2f pts" % (len(dentro), dentro.fc.abs().mean()))
