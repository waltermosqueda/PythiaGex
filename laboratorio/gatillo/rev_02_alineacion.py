# -*- coding: utf-8 -*-
"""rev_02_alineacion.py — (2) MIRADA ADELANTE: (a) que corrige 'corr' cada dia y cuanto cambia si se hace causal; (b) el reloj de la foto contra el de la cinta
(a que desfase el 'futuro' de la foto coincide mejor con la cinta); (c) cuando CAMBIA una raya reconstruida, cuantos segundos despues cambio la raya dibujada (estela real)."""
import os, sys
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rev_lib as R, verdes_lib as V
from verdes_01_hoy import estela

T = R.sesiones(); F, _ = V.fotos("NQ")
print("(a) corrimiento de contrato por dia (cinta - futuro de la foto):")
for s, seg in T.items():
    f = F[(F["t"] >= seg.index[0]) & (F["t"] <= seg.index[-1])]
    px = seg["ultimo"].reindex(f["t"].dt.floor("s"), method="ffill").to_numpy(); dif = pd.Series(px - f["fut"].to_numpy())
    cen = dif.rolling(31, center=True, min_periods=5).median().bfill().ffill(); cau = dif.rolling(31, min_periods=1).median()
    aplica = np.nanmedian(np.abs(cen)) >= 3.0
    print("   %s fotos %4d | dif mediana %+8.2f (p5 %+.2f p95 %+.2f) | el banco corrige: %s | centrada - causal: mediana |%.2f| p95 |%.2f| max |%.2f|" % (
        s, len(f), dif.median(), dif.quantile(.05), dif.quantile(.95), "SI" if aplica else "no (0)", (cen - cau).abs().median(), (cen - cau).abs().quantile(.95), (cen - cau).abs().max()))

print("\n(b) reloj de la foto contra la cinta: mediana de |cinta(ts + k) - futuro de la foto| en rueda, por desfase k (s):")
for s in ("2026-09-08", "2026-09-14", "2026-09-16", "2026-09-17"):
    seg = T[s]; f = F[(F["t"] >= seg.index[0]) & (F["t"] <= seg.index[-1])]; hm = f["t"].dt.strftime("%H:%M"); f = f[(hm >= "13:30") & (hm < "20:00")]
    base = np.median(seg["ultimo"].reindex(f["t"].dt.floor("s"), method="ffill").to_numpy() - f["fut"].to_numpy()); fila = []
    for k in (-20, -10, -5, -3, -2, -1, 0, 1, 2, 3, 5, 10, 20):
        px = seg["ultimo"].reindex(f["t"].dt.floor("s") + pd.Timedelta(seconds=k), method="ffill").to_numpy(); fila.append("%+d:%.2f" % (k, np.nanmean(np.abs(px - f["fut"].to_numpy() - base))))
    print("   %s (n %d, base %+.2f): %s" % (s, len(f), base, "  ".join(fila)))

print("\n(c) cuando cambia de strike una raya reconstruida, cuanto despues cambio la DIBUJADA (estela real):")
for s in ("2026-09-16", "2026-09-17"):
    seg = T[s]; L2 = V.rayas_por_segundo(seg, F); corr = V.rayas_por_segundo.ultimo_corrimiento
    e = estela("NQ", s); e = e[(e.index >= seg.index[0]) & (e.index <= seg.index[-1])]; E = e[["d0", "d1"]].reindex(seg.index, method="ffill", tolerance=pd.Timedelta(seconds=300))
    r = seg["rueda"].to_numpy()
    # desfase de precio entre estela y reconstruida (mismo strike): mediana de la diferencia de la raya mas cercana
    a = E.to_numpy()[r]; b = L2[["d0", "d1"]].to_numpy()[r]; ok = ~np.isnan(a).any(axis=1) & ~np.isnan(b).any(axis=1)
    d = a[ok][:, :, None] - b[ok][:, None, :]; j = np.abs(d).argmin(axis=2); dm = np.take_along_axis(d, j[:, :, None], axis=2)[:, :, 0]
    print("   %s: corrimiento aplicado por el banco %+.2f | estela - reconstruida (raya mas cercana): mediana %+.2f, |dif|<=2: %.1f %%, <=6: %.1f %%, segundos comparables %d de %d de rueda" % (
        s, corr, np.median(dm[np.abs(dm) < 8]), 100 * (np.abs(dm) <= 2).mean(), 100 * (np.abs(dm) <= 6).mean(), ok.sum(), r.sum()))
    for col in ("d0", "d1"):
        rec = L2[col]; ch = rec[(rec.diff().abs() >= 15) & r]; lags = []
        for t, v in ch.items():
            w = E[t - pd.Timedelta(seconds=180): t + pd.Timedelta(seconds=300)]
            hit = w[((w["d0"] - v).abs() <= 3) | ((w["d1"] - v).abs() <= 3)]
            antes = E[t - pd.Timedelta(seconds=181): t - pd.Timedelta(seconds=180)]
            if len(hit): lags.append((hit.index[0] - t).total_seconds())
        lags = np.array(lags)
        if len(lags): print("      %s: %d saltos de strike reconstruidos; la dibujada llega al mismo nivel: mediana %+.0f s (p25 %+.0f, p75 %+.0f); ya estaba antes (<= -60 s): %d; despues (> +5 s): %d; sin pareja: %d" % (
            col, len(ch), np.median(lags), np.percentile(lags, 25), np.percentile(lags, 75), (lags <= -60).sum(), (lags > 5).sum(), len(ch) - len(lags)))
