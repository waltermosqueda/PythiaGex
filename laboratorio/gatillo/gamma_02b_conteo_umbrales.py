# -*- coding: utf-8 -*-
"""gamma_02b_conteo_umbrales.py — SOLO CUENTA (sin barreras): alternativas de umbral para las variantes que quedaron con n chico
(V06 empuje frenado, V08 pasivo gira, V12 delta extremo 5 min). Se elige por n, nunca por resultado."""
import sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, gamma_lib as L

T = B.todas_las_sesiones(); EXP, CONF = L.mitades(T); cnt = {}
for mitad, S in (("EXP", EXP), ("CONF", CONF)):
    for s, d in S.items():
        f = L.rasgos(d); nv = L.niveles_seg(d); e = f["elegible"].to_numpy(); sg = np.sign
        dd = nv[["d_dom0", "d_dom1"]].to_numpy(); ad = np.where(np.isnan(dd), np.inf, np.abs(dd)); k = ad.argmin(axis=1)
        dist = dd[np.arange(len(dd)), k]; adist = ad[np.arange(len(dd)), k]; lado_def = np.where(dist >= 0, 1, -1)
        D30 = f["D30"].to_numpy(); s30 = sg(D30); M30 = f["M30"].to_numpy(); aD = pd.Series(np.abs(D30), index=d.index)
        for q in (0.90, 0.80, 0.70):
            thr = L.q_previo(aD, 1800, q).to_numpy()
            for radio in (10, 15):
                for prog in (0.5, 1.0):
                    m = e & (np.abs(D30) >= thr) & (s30 != 0) & (adist <= radio) & (s30 == -lado_def) & (s30 * M30 <= prog)
                    cnt[("V06 q%.2f radio%d prog<=%.1f" % (q, radio, prog), mitad)] = cnt.get(("V06 q%.2f radio%d prog<=%.1f" % (q, radio, prog), mitad), 0) + len(L.desagrupar(m))
        dm = nv[["d_mp", "d_mn", "d_zero"]].to_numpy(); am = np.where(np.isnan(dm), np.inf, np.abs(dm)); km = am.argmin(axis=1)
        distm = dm[np.arange(len(dm)), km]; adistm = am[np.arange(len(dm)), km]; a = np.where(distm >= 0, -1, 1)
        P30 = f["P30"].to_numpy(); Pa = f["P30_antes"].to_numpy()
        for radio in (12, 15, 20):
            m = e & (adistm <= radio) & (a * f["M120"].to_numpy() >= 3) & (sg(P30) == -a) & (np.abs(P30) >= f["P30_q80"].to_numpy()) & (sg(Pa) == a)
            cnt[("V08 radio%d" % radio, mitad)] = cnt.get(("V08 radio%d" % radio, mitad), 0) + len(L.desagrupar(m))
        # V12
        g = d.resample("5min", label="left", closed="left"); b = pd.DataFrame({"delta": g["delta"].sum(), "vol": g["vol"].sum()}); b["r"] = b["delta"] / b["vol"].where(b["vol"] > 0)
        fin = b.index + pd.Timedelta(minutes=5) - pd.Timedelta(seconds=1); z = nv["d_zero"].to_numpy(); reg = (np.abs(z) >= L.ZONA)
        for q in (0.75, 0.70, 0.60):
            for qv in (None, 0.30, 0.50):
                thr = b["r"].abs().rolling(12, min_periods=12).quantile(q).shift(1); ok = b["r"].abs() >= thr
                if qv is not None: ok &= b["vol"] >= b["vol"].rolling(12, min_periods=12).quantile(qv).shift(1)
                mk = pd.Series(ok.to_numpy(), index=fin).reindex(d.index).fillna(False).to_numpy().astype(bool)
                m = e & mk & reg
                cnt[("V12 q%.2f vol>=%s" % (q, qv), mitad)] = cnt.get(("V12 q%.2f vol>=%s" % (q, qv), mitad), 0) + int(m.sum())
R = pd.Series(cnt).unstack(); print(R.to_string())
