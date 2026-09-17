# -*- coding: utf-8 -*-
"""gamma_01_alineacion.py — SOLO DESCRIPTIVO: el 'c' de niveles_m1 (cierre de la vela del grafico) coincide con el cierre de la cinta en ese
mismo minuto? Si hay corrimiento constante es otro contrato (roll U6/Z6) y por eso los niveles se usan como DISTANCIA + movimiento de la cinta.
Tambien: la marca de tiempo del nivel es el minuto de APERTURA de la vela? (se prueba contra el cierre del mismo minuto y del anterior)."""
import sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B

N = B.niveles_m1(); T = B.todas_las_sesiones()
print("%-11s %6s | %9s %9s | %9s %9s | %9s" % ("sesion", "min", "med(c-cl)", "mad", "med(c-cl-1)", "mad", "med(c-cl+1)"))
for s, d in sorted(T.items()):
    r = d[d["rueda"]]; cl = r["ultimo"].resample("1min").last()
    n = N["c"].reindex(cl.index)
    a = (n - cl).dropna(); b = (n - cl.shift(1)).dropna(); c = (n - cl.shift(-1)).dropna()
    print("%-11s %6d | %9.2f %9.2f | %9.2f %9.2f | %9.2f %9.2f" % (s, len(a), a.median(), (a - a.median()).abs().median(), b.median(), (b - b.median()).abs().median(), c.median(), (c - c.median()).abs().median()))
# saltos del zero gamma de un minuto al otro (estabilidad del nivel)
hm = N.index.strftime("%H:%M"); R = N[(hm >= "13:30") & (hm < "20:00")]
for k in ("d_zero", "d_dom0", "d_dom1", "d_mp", "d_mn", "d_mc5"):
    niv = R["c"] - R[k]; salto = niv.diff().abs()
    print("%-7s salto del NIVEL minuto a minuto: mediana %.1f | p90 %.1f | %% de minutos que se mueve > 5 pts: %.1f | NaN %.1f %%" % (k, salto.median(), salto.quantile(0.9), 100 * (salto > 5).mean(), 100 * R[k].isna().mean()))
# tabla cruzada signo(d_zero) x cuadrante, en minutos de rueda
R = R.dropna(subset=["d_zero"]); z = np.where(R["d_zero"] >= 25, "ARRIBA>=25", np.where(R["d_zero"] <= -25, "ABAJO<=-25", "cerca"))
print(pd.crosstab(z, R["cuadrante"]).to_string())
