# -*- coding: utf-8 -*-
"""
literatura_01_vida_de_la_senal.py — prueba chica DESCRIPTIVA de la familia "literatura" (pre-registrada en resultados/literatura.md).

Pregunta: cuanto VIVE (en segundos) y cuanto VALE (en ticks) lo que la literatura dice que predice el proximo movimiento
(desbalance de cola, OFI, OFI pasivo, delta), medido en MNQ. Solo B.explorar(T), solo rueda sin los 2 primeros minutos.
No elige nada: no hay umbrales ni finalistas.
"""
import sys
sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd
from scipy.stats import spearmanr
import base as B

HS = [1, 2, 5, 10, 30, 60, 300, 600]
RASGOS = ["I", "OFI10", "PAS10", "D10", "D60"]


def preparar(d):
    d = d.copy()
    d["mid"] = (d["bid"] + d["ask"]) / 2.0
    prof = (d["bidv"] + d["askv"]).rolling(300, min_periods=30).mean()
    d["I"] = (d["bidv"] - d["askv"]) / (d["bidv"] + d["askv"]).replace(0, np.nan)
    d["OFI10"] = d["ofi"].rolling(10, min_periods=10).sum() / prof
    d["PAS10"] = d["ofi_pas"].rolling(10, min_periods=10).sum() / prof
    d["D10"] = d["delta"].rolling(10, min_periods=10).sum()
    d["D60"] = d["delta"].rolling(60, min_periods=60).sum()
    for h in HS: d["f%d" % h] = (d["mid"].shift(-h) - d["mid"]) / B.TICK
    r = d[d["rueda"]]
    if len(r) == 0: return r
    r = r[r.index >= r.index[0] + pd.Timedelta(minutes=2)]
    r = r[(r["ask"] > r["bid"]) & ((r["ask"] - r["bid"]) <= 5)]   # punta sana (descarta cruces y huecos)
    return r


def main():
    T = B.explorar(B.todas_las_sesiones())
    print("sesiones de explorar:", len(T), sorted(T))
    corr = {f: {h: [] for h in HS} for f in RASGOS}; dec = {f: {h: [] for h in HS} for f in RASGOS}
    spreads = []; cambios = []
    for s in sorted(T):
        r = preparar(T[s])
        if len(r) < 5000: print(s, "sin rueda suficiente:", len(r)); continue
        spreads.append(((r["ask"] - r["bid"]) / B.TICK).round().clip(0, 6).value_counts(normalize=True))
        cambios.append(float((r["mid"].diff().abs() > 0).mean() * 60))
        for f in RASGOS:
            x = r[f]
            lo, hi = x.quantile(0.1), x.quantile(0.9)
            for h in HS:
                y = r["f%d" % h]; ok = x.notna() & y.notna()
                c = spearmanr(x[ok], y[ok]).correlation
                corr[f][h].append(c)
                dec[f][h].append(float(y[ok & (x >= hi)].mean() - y[ok & (x <= lo)].mean()))
    print("\nspread de MNQ en ticks (fraccion de los segundos de rueda):")
    print((pd.concat(spreads, axis=1).fillna(0).mean(axis=1) * 100).round(1).to_string())
    print("\nsegundos por minuto en que cambia el precio medio: media %.1f (min %.1f, max %.1f) -> un cambio cada %.1f s como MUCHO"
          % (np.mean(cambios), np.min(cambios), np.max(cambios), 60 / np.mean(cambios)))
    print("\ncorrelacion de Spearman rasgo(t) -> cambio del medio de t a t+h  (media de las sesiones | sesiones con signo positivo)")
    for f in RASGOS:
        print("  %-6s " % f + "  ".join("h=%-3d %+.3f (%d/%d)" % (h, np.nanmean(corr[f][h]), int(np.sum(np.array(corr[f][h]) > 0)), len(corr[f][h])) for h in HS))
    print("\ndecil alto menos decil bajo, en TICKS del medio (costo del operador: %.1f ticks ida y vuelta)" % (B.COSTO_PTS / B.TICK))
    for f in RASGOS:
        print("  %-6s " % f + "  ".join("h=%-3d %+.2f" % (h, np.nanmean(dec[f][h])) for h in HS))


if __name__ == "__main__":
    main()
