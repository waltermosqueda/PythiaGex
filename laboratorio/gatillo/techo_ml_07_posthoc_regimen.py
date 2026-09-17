# -*- coding: utf-8 -*-
"""techo_ml_07_posthoc_regimen.py — POST-HOC, descriptivo, NO es un hallazgo validado.
El flujo de 15 min (dr_900, zD_900, ret_900) paso de 'leve continuacion' en explorar (AUC conjunto 0,51-0,52) a 'reversion' en confirmar (0,466-0,472).
Explorar fue casi todo BAJO el zero gamma y confirmar casi todo ARRIBA. Es el regimen de gamma o es el periodo? Las celdas cruzadas (explorar-arriba y
confirmar-abajo) son las unicas que rompen la confusion. Tabla 2x2 de AUC conjunto con error por dias remuestreados, y cuantos dias aporta cada celda."""
import numpy as np, pandas as pd
import techo_ml_lib as L, base as B

T = B.todas_las_sesiones(); N = B.niveles_m1()
dfe = L.tablero(B.explorar(T), N, "explorar"); dfc = L.tablero(B.confirmar(T), N, "confirmar")


def celda(df, mascara, c, B_=300):
    yy = df["y8"].to_numpy(); x = df[c].to_numpy(dtype="float64"); v = (yy != 0) & ~np.isnan(x) & mascara; ses = df["sesion"].to_numpy()
    if v.sum() < 300: return None
    a = L.auc((yy[v] > 0).astype(int), x[v]); dias = [s for s in np.unique(ses) if (v & (ses == s)).sum() >= 100]; idx = {s: np.flatnonzero(v & (ses == s)) for s in dias}
    rng = np.random.default_rng(2); bs = []
    for _ in range(B_):
        k = np.concatenate([idx[s] for s in rng.choice(dias, size=len(dias))]); bs.append(L.auc((yy[k] > 0).astype(int), x[k]))
    return a, float(np.nanstd(bs, ddof=1)), int(v.sum()), len(dias)


for c in ("dr_900", "zD_900", "ret_900", "dr_300"):
    print("\n%s  (AUC conjunto contra +-8/600; > 0,5 = el flujo/precio de la ventana SIGUE; < 0,5 = se DEVUELVE)" % c)
    for nombre, df in (("explorar", dfe), ("confirmar", dfc)):
        g = df["g_zero"].to_numpy(dtype="float64")
        for reg, mk in (("BAJO el zero gamma", g < 0), ("ARRIBA del zero gamma", g > 0)):
            r = celda(df, mk, c)
            print("   %-9s %-22s %s" % (nombre, reg, "sin muestra" if r is None else "AUC %.3f +- %.3f | %5d filas | %d dias con >= 100 filas" % r))
# lo mismo por distancia al zero (lejos abajo / cerca / lejos arriba), juntando las 20 sesiones
df = pd.concat([dfe, dfc]); g = df["g_zero"].to_numpy(dtype="float64")
print("\nLas 20 sesiones juntas, dr_900 segun distancia al zero gamma:")
for reg, mk in (("mas de 100 pts ABAJO", g < -100), ("entre -100 y 0", (g >= -100) & (g < 0)), ("entre 0 y +100", (g >= 0) & (g < 100)), ("mas de 100 pts ARRIBA", g >= 100)):
    r = celda(df, mk, "dr_900"); print("   %-22s %s" % (reg, "sin muestra" if r is None else "AUC %.3f +- %.3f | %5d filas | %d dias" % r))
