# -*- coding: utf-8 -*-
"""
verdes_02_toques.py — cada TOQUE de las verdes fuertes (dominantes de la capa NQ, como se dibujaron EN VIVO) con la cinta orden por orden:
de donde venia, cuanto la traspaso ("unos pocos puntos", "mecha larga") y que paso despues (reboto G puntos antes de romperla por S?).
Al lado, el mismo calculo con las rayas CORRIDAS (placebo): si la raya importa, tiene que rebotar MAS que una raya cualquiera.
Uso: python verdes_02_toques.py [NQ] [2026-09-17] [--detalle]
"""
import os, sys
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B
from verdes_01_hoy import estela
from rayas_base import _entradas, barrera_asim

TOL, LEJOS, VENTANA = 1.5, 10.0, 1800
REGLAS = ((10, 5), (12, 6), (15, 6), (20, 8), (8, 8))   # (G a favor, S en contra, medido desde la RAYA)


def filas_de_capa(seg, e, paso=5.0):
    """Rayas por identidad de precio (redondeo a 'paso'): {K: vector por segundo con el valor exacto (NaN antes de nacer)}; 'vigente' = es D0/D1 ahora."""
    s = e.reindex(seg.index, method="ffill"); n = len(seg); filas = {}; vig = {}
    for col in ("d0", "d1", "d2"):
        v = s[col].to_numpy()
        K = np.round(v / paso) * paso
        for k in np.unique(K[~np.isnan(K)]):
            m = K == k; i0 = int(np.flatnonzero(m)[0])
            a = filas.get(k)
            if a is None: a = np.full(n, np.nan); filas[k] = a; vig[k] = np.zeros(n, bool)
            vals = pd.Series(np.where(m, v, np.nan)).ffill().to_numpy(); vals[:i0] = np.nan
            a[np.isnan(a)] = vals[np.isnan(a)]
            if col in ("d0", "d1"): vig[k] |= m
    return filas, vig


def analizar(seg, filas, vig, delta=0.0, solo_vigente=True):
    alto = seg["alto"].to_numpy(); bajo = seg["bajo"].to_numpy(); ult = seg["ultimo"].to_numpy(); rueda = seg["rueda"].to_numpy(); out = []
    for K, Lt0 in filas.items():
        Lt = Lt0 + delta
        for lado in (1, -1):
            idx, ult_lejos = _entradas(alto, bajo, Lt, lado, TOL, LEJOS, VENTANA)
            for i in idx:
                if not rueda[i] or (solo_vigente and not vig[K][i]): continue
                L = Lt[i]; j1 = min(len(ult), i + 1 + 900)
                # cuanto la traspaso ANTES de alejarse 10 pts a favor (o en 15 min): la "mecha"
                if lado > 0: fav = np.flatnonzero(alto[i:j1] >= L + 10); fin = i + (fav[0] if len(fav) else j1 - i - 1); pen = L - bajo[i:fin + 1].min()
                else: fav = np.flatnonzero(bajo[i:j1] <= L - 10); fin = i + (fav[0] if len(fav) else j1 - i - 1); pen = alto[i:fin + 1].max() - L
                fila = dict(i=int(i), hora=(seg.index[i] - pd.Timedelta(hours=3)).strftime("%H:%M:%S"), raya=round(float(L), 2), K=K, lado=lado, traspaso=round(float(max(0.0, pen)), 2))
                for G, S in REGLAS:
                    y, tt = barrera_asim(seg, [i], [lado], G, S, 900, entrada=[L]); fila["r%d_%d" % (G, S)] = int(y[0]); fila["t%d_%d" % (G, S)] = tt[0]
                out.append(fila)
    return pd.DataFrame(out).sort_values("i").reset_index(drop=True) if out else pd.DataFrame()


def resumen(d, nombre):
    if not len(d): return "%s: sin toques" % nombre
    partes = []
    for G, S in REGLAS:
        y = d["r%d_%d" % (G, S)]; n = int((y != 0).sum()); p = float((y[y != 0] > 0).mean()) if n else float("nan")
        partes.append("+%d/-%d: %.0f %% de %d (azar %.0f %%)" % (G, S, 100 * p, n, 100.0 * S / (G + S)))
    return "%s: %d toques | traspaso mediano %.2f pts, p75 %.2f | %s" % (nombre, len(d), d["traspaso"].median(), d["traspaso"].quantile(0.75), " | ".join(partes))


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    capa = args[0] if args else "NQ"; dia = args[1] if len(args) > 1 else "2026-09-17"
    T = B.todas_las_sesiones(); seg = T[dia]; e = estela(capa, dia); e = e[(e.index >= seg.index[0]) & (e.index <= seg.index[-1])]
    filas, vig = filas_de_capa(seg, e)
    real = analizar(seg, filas, vig)
    print("CAPA %s, sesion %s, RUEDA. Toque = el precio venia de >= %.0f pts y llega a <= %.1f pts de una raya VIGENTE (D1/D2 de ese momento)." % (capa, dia, LEJOS, TOL))
    print("Resultado medido DESDE LA RAYA: rebota G puntos a favor antes de romperla por S puntos (dentro de 15 min).\n")
    if "--detalle" in sys.argv and len(real):
        cols = ["hora", "raya", "lado", "traspaso"] + ["r%d_%d" % r for r in REGLAS]
        print(real[cols].rename(columns={"lado": "viene_de(+1=arriba)"}).to_string(index=False)); print()
    print(resumen(real, "RAYAS REALES"))
    plc = [analizar(seg, filas, vig, delta=dl) for dl in (12.5, -12.5, 7.0, -7.0)]
    print(resumen(pd.concat([p for p in plc if len(p)], ignore_index=True), "PLACEBO (mismas rayas corridas +-12,5 y +-7)"))
