# -*- coding: utf-8 -*-
"""rev_01_replica.py — (1) confirma o refuta los numeros del banco: 'cruce + delta 10 s', objetivo 20 y 12. Implementacion propia (rev_lib) contra la original,
operacion por operacion. Guarda las operaciones en rev_cache/ para el resto de la revision."""
import os, sys, time
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rev_lib as R, verdes_lib as V
import verdes_20_reclamo as O   # solo se importan sus funciones (el main esta protegido)

t0 = time.time(); T = R.sesiones(); F, _ = V.fotos("NQ"); PLC = (12.5, -12.5, 7.0, -7.0)
print("sesiones:", " ".join(T)); P = dict(R.P0)
for obj in (20, 12):
    res = R.correr(T, F, P, objetivo=obj, corrimientos=(0.0,) + PLC)
    real = res[0.0]; plc = pd.concat([res[c].assign(corr=c) for c in PLC], ignore_index=True)
    print("\n[cruce + delta 10 s | objetivo %s]  (mio)" % obj)
    print("   " + R.resumen(real, "VERDES (7 dias)")); print("   " + R.resumen(real[real["dia"] != "2026-09-17"], "VERDES sin el 17-09")); print("   " + R.resumen(plc, "PLACEBO +-12,5 +-7"))
    print("   por dia (verdes): " + " | ".join("%s %+.1f (n %d, media %+.2f)" % (d[5:], g["neto"].sum(), len(g), g["neto"].mean()) for d, g in real.groupby("dia")))
    print("   por dia (placebo): " + " | ".join("%s %+.1f (n %d, media %+.2f)" % (d[5:], g["neto"].sum(), len(g), g["neto"].mean()) for d, g in plc.groupby("dia")))
    real.to_parquet(os.path.join(R.REVCACHE, "real_obj%s.parquet" % obj)); plc.to_parquet(os.path.join(R.REVCACHE, "plc_obj%s.parquet" % obj))
    # original, operacion por operacion
    mio = set((r.dia, r.i, r.lado) for r in real.itertuples()); su = set(); su_neto = []
    for s, seg in sorted(T.items()):
        L2 = V.rayas_por_segundo(seg, F)
        for col in ("d0", "d1"):
            Lv = L2[col].to_numpy(); K = np.round(Lv / 5.0) * 5.0
            for k in np.unique(K[~np.isnan(K)]):
                g = O.gatillos(seg, np.where(K == k, Lv, np.nan), "cruce", True); r = O.resultado(seg, g, obj)
                for a, b in zip(g, r): su.add((s, int(a[0]), int(a[1]))); su_neto.append(b[0])
    print("   ORIGINAL: n %d, neto %+.3f | MIO: n %d, neto %+.3f | gatillos en comun %d, solo original %d, solo mio %d" % (len(su), np.mean(su_neto), len(mio), real["neto"].mean(), len(mio & su), len(su - mio), len(mio - su)))
print("\n%.0f s" % (time.time() - t0))
