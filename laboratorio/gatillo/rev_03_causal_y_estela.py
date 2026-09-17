# -*- coding: utf-8 -*-
"""rev_03_causal_y_estela.py — (2) la misma cuenta con (i) la correccion de contrato CAUSAL (mediana movil de las 31 fotos PASADAS, decision foto por foto)
y (ii) las rayas que DE VERDAD se dibujaron el 16 y el 17-09 (estela real), contra las reconstruidas de esos mismos dias."""
import os, sys
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rev_lib as R, verdes_lib as V
from verdes_01_hoy import estela

PLC = (12.5, -12.5, 7.0, -7.0)


def rayas_causal(seg, F, vida_s=300):
    f = F[(F["t"] >= seg.index[0]) & (F["t"] <= seg.index[-1])].copy()
    if not len(f): return None
    px = seg["ultimo"].reindex(f["t"].dt.floor("s"), method="ffill").to_numpy(); dif = pd.Series(px - f["fut"].to_numpy())
    corr = dif.rolling(31, min_periods=1).median().to_numpy(); corr = np.where(np.abs(corr) < 3.0, 0.0, corr)     # solo pasado; 'mismo contrato' se decide foto por foto
    for c in ("d0", "d1"): f[c] = f[c].to_numpy() + corr
    f["t"] = f["t"].dt.floor("s") + pd.Timedelta(seconds=1); f = f.drop_duplicates("t", keep="last").set_index("t")
    return f[["d0", "f0", "d1", "f1", "neto", "total_cerca"]].reindex(seg.index, method="ffill", tolerance=pd.Timedelta(seconds=vida_s))


def rayas_estela(seg, F, vida_s=300):
    s = (seg.index[-1]).strftime("%Y-%m-%d"); e = estela("NQ", s); e = e[(e.index >= seg.index[0]) & (e.index <= seg.index[-1])]
    if not len(e): return None
    return e[["d0", "d1"]].reindex(seg.index, method="ffill", tolerance=pd.Timedelta(seconds=vida_s))


def informe(nombre, res):
    real = res[0.0]; plc = pd.concat([res[c] for c in PLC], ignore_index=True)
    print("   %-28s %s" % (nombre, R.resumen(real, "VERDES"))); print("   %-28s %s" % ("", R.resumen(plc, "PLACEBO +-12,5 +-7")))
    print("   %-28s por dia: %s" % ("", " | ".join("%s %+.1f (%d)" % (d[5:], g["neto"].sum(), len(g)) for d, g in real.groupby("dia"))))
    return real


if __name__ == "__main__":
    T = R.sesiones(); F, _ = V.fotos("NQ"); P = dict(R.P0)
    for obj in (20, 12):
        print("\n=== objetivo %s, cruce + delta 10 s ===" % obj)
        print(" (i) correccion de contrato, 7 dias:")
        informe("banco (centrada, 31 fotos)", R.correr(T, F, P, obj, (0.0,) + PLC))
        informe("CAUSAL (31 fotos pasadas)", R.correr(T, F, P, obj, (0.0,) + PLC, rayas=rayas_causal))
        T2 = {s: T[s] for s in ("2026-09-16", "2026-09-17")}
        print(" (ii) 16 y 17-09: reconstruidas contra DIBUJADAS (estela real):")
        a = informe("reconstruidas", R.correr(T2, F, P, obj, (0.0,) + PLC)); b = informe("ESTELA REAL (lo dibujado)", R.correr(T2, F, P, obj, (0.0,) + PLC, rayas=rayas_estela))
        # cuantos gatillos comparten (mismo dia, lado, a <= 5 s)
        com = 0
        for x in a.itertuples():
            y = b[(b["dia"] == x.dia) & (b["lado"] == x.lado) & ((b["i"] - x.i).abs() <= 5)]
            com += int(len(y) > 0)
        print("   gatillos reconstruidos que tambien da la estela real (mismo lado, <= 5 s): %d de %d (la estela da %d)" % (com, len(a), len(b)))
        b.to_parquet(os.path.join(R.REVCACHE, "estela_real_obj%s.parquet" % obj))
