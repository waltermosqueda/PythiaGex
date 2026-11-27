# -*- coding: utf-8 -*-
"""RONDA 23, paso 1: BASE de puntos de decision en velas de 5 min de MES (solo lectura de velas-MES-m5.csv).
Un punto = el CIERRE de cada vela de la rueda (9:30-16:00 NY). Todo lo que se guarda usa solo velas ya cerradas (causal).
Salidas (scratchpad\\r23): ruedas5.pkl (arrays por rueda) y puntos5.parquet (rasgos por punto de decision).
Ruedas: completas = 78 velas de 9:30 a 15:55; cortas (feriados, medias ruedas) fuera de todo; incompletas por hueco de datos
(>= 70 velas) solo como historia (convenciones de la R22).
Uso: python -I r23_01_base5.py"""
import os, pickle, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r23_comun as K

TK = K.TK


def leer_velas():
    v = pd.read_csv(K.M5, usecols=["t", "o", "h", "l", "c", "vol", "delta"])
    ny = pd.to_datetime(v["t"]).dt.tz_localize("UTC").dt.tz_convert(K.NY).dt.tz_localize(None)
    v["rueda"] = (ny + pd.Timedelta(hours=6)).dt.strftime("%Y-%m-%d")
    v["hm"] = ((ny - pd.to_datetime(v["rueda"])).dt.total_seconds() // 60).astype(int)
    return v.sort_values(["rueda", "hm"]).reset_index(drop=True)


def clase(hm):
    r = hm[(hm >= 570) & (hm < 960)]
    if len(r) == 78 and (np.sort(r) == 570 + 5 * np.arange(78)).all(): return "completa"
    if len(r) == 0 or r.max() <= 13 * 60 + 15: return "corta"
    return "incompleta"


def X_de(med):
    return float(np.floor(0.08 * med / TK + 0.5) * TK)


def vr_dia(c):
    """Sumas para el cociente de varianzas de la rueda: retornos de 1 vela y de 6 velas (solapados) de cierre a cierre."""
    r = np.diff(c)
    r6 = c[6:] - c[:-6]
    return float((r ** 2).sum()), len(r), float((r6 ** 2).sum()), len(r6)


def rasgos_rueda(dia, a, ctx, f):
    """a: arrays RTH (78) o,h,l,c,vol,delta. ctx: med10, med60, X, vr10, vr20. Devuelve DataFrame de los 78 puntos."""
    o, h, l, c, vol, dl = (a[x] for x in ("o", "h", "l", "c", "vol", "delta"))
    n = len(c); X = ctx["X"]
    p = (h + l) / 2.0; vi = (h - l) ** 2 / 12.0
    cpv = np.cumsum(p * vol); cp2v = np.cumsum((p * p + vi) * vol); cv = np.cumsum(vol)
    vw = cpv / cv; sd = np.sqrt(np.maximum(cp2v / cv - vw * vw, 0.0))
    Hk = np.maximum.accumulate(h); Lk = np.minimum.accumulate(l)
    iH = np.array([int(np.argmax(h[:k + 1] >= Hk[k])) for k in range(n)])      # primera vela que marco el maximo vigente
    iL = np.array([int(np.argmax(l[:k + 1] <= Lk[k])) for k in range(n)])
    O = float(o[0])
    m = (np.arange(n) + 1) * 5 - 1
    rrn = (Hk - Lk) / (ctx["med10"] * f[m])
    rrn60 = (Hk - Lk) / (ctx["med60"] * f[m]) if np.isfinite(ctx["med60"]) else np.full(n, np.nan)
    s = np.sign(c - vw).astype(int)
    lo_ = np.sign(c - O).astype(int)
    tipo = np.full(n, "N", dtype=object)
    tipo[(rrn >= 1.6) & (s == lo_) & (s != 0)] = "T"
    tipo[(rrn > 1.0) & (rrn < 1.6)] = "R"
    tipo[rrn <= 1.0] = "E"
    vprev = np.concatenate([[p[0]], vw[:-1]])                 # VWAP al empezar cada vela
    toque = (l <= vprev) & (vprev <= h); toque[0] = True
    ult_toque = np.zeros(n, int); last = 0
    for k in range(n):
        if toque[k]: last = k
        ult_toque[k] = last
    n_toques = np.cumsum(toque) - 1                           # sin contar la vela 0
    cruce = np.zeros(n, int); cruce[1:] = (s[1:] != s[:-1]) & (s[1:] != 0) & (s[:-1] != 0)
    n_cruces = np.cumsum(cruce)
    edad = np.zeros(n, int); prof = np.zeros(n); lado_cnt = np.zeros(n)
    for k in range(n):
        if k > 0 and s[k] == s[k - 1] and s[k] != 0:
            edad[k] = edad[k - 1] + 1; prof[k] = max(prof[k - 1], abs(c[k] - vw[k]) / X)
        else:
            edad[k] = 1; prof[k] = abs(c[k] - vw[k]) / X
    nib = 12
    ibh = np.full(n, np.nan); ibl = np.full(n, np.nan)
    ibh[nib - 1:] = h[:nib].max(); ibl[nib - 1:] = l[:nib].min(); ibr = ibh - ibl
    ext_up = np.maximum(Hk - ibh, 0) / ibr; ext_dn = np.maximum(ibl - Lk, 0) / ibr
    ext_lado = np.where(s > 0, ext_up, ext_dn); ext_otro = np.where(s > 0, ext_dn, ext_up)
    pend = np.full(n, np.nan); pend[6:] = s[6:] * (vw[6:] - vw[:-6]) / X
    d1 = s * dl; d3 = s * pd.Series(dl).rolling(3, min_periods=1).sum().to_numpy()
    volr = vol / (cv / (np.arange(n) + 1))
    ret1 = np.zeros(n); ret1[1:] = s[1:] * (c[1:] - c[:-1]) / X
    ret3 = np.zeros(n); ret3[3:] = s[3:] * (c[3:] - c[:-3]) / X
    frac_lado = np.array([np.mean(s[:k + 1] == s[k]) for k in range(n)])
    dx = np.where(s > 0, Hk - c, c - Lk) / X
    bext = np.where(s > 0, np.arange(n) - iH, np.arange(n) - iL)
    # cociente de varianzas de HOY hasta la vela k (desde 12 velas)
    vr_hoy = np.full(n, np.nan)
    for k in range(12, n):
        cc = c[:k + 1]; r1 = np.diff(cc); r6 = cc[6:] - cc[:-6]
        v1 = (r1 ** 2).mean()
        if v1 > 0: vr_hoy[k] = (r6 ** 2).mean() / (6 * v1)
    hora = ["%02d:%02d" % divmod(575 + 5 * k, 60) for k in range(n)]
    return pd.DataFrame(dict(dia=dia, k=np.arange(n), hora=hora, min_dia=575 + 5 * np.arange(n), c=c, vw=vw, sd=sd, H=Hk, L=Lk, O=O,
                             X=X, med10=ctx["med10"], med60=ctx["med60"], rrn=rrn, rrn60=rrn60, tipo=tipo, s=s,
                             dv=np.abs(c - vw) / X, z=np.where(sd > 0, np.abs(c - vw) / np.where(sd > 0, sd, 1), np.nan), dx=dx,
                             bext=bext, min_toque=5 * (np.arange(n) - ult_toque), n_toques=n_toques, n_cruces=n_cruces,
                             edad=edad, prof=prof, ext_lado=ext_lado, ext_otro=ext_otro, ibr_X=ibr / X, pend=pend, d1=d1, d3=d3,
                             volr=volr, ret1=ret1, ret3=ret3, frac_lado=frac_lado, vr_hoy=vr_hoy,
                             vr10=ctx["vr10"], vr20=ctx["vr20"], r10_60=ctx["med10"] / ctx["med60"] if np.isfinite(ctx["med60"]) else np.nan))


def main():
    t0 = time.time(); K.frenar_si_falta_ram(2.0); f = K.curva_f()
    v = leer_velas()
    ruedas = {}; clases = {}; resumen = {}
    for r, g in v.groupby("rueda", sort=True):
        hm = g["hm"].to_numpy(int); cl = clase(hm); clases[r] = cl
        rt = g[(g["hm"] >= 570) & (g["hm"] < 960)]
        if cl == "corta" or len(rt) == 0: continue
        a = {x: rt[x].to_numpy(float) for x in ("o", "h", "l", "c", "vol", "delta")}
        a["hm"] = rt["hm"].to_numpy(int)
        resumen[r] = dict(clase=cl, n=len(rt), rango=float(a["h"].max() - a["l"].min()), vr=vr_dia(a["c"]) if len(rt) > 7 else None)
        if cl == "completa": ruedas[r] = a
    hist = [r for r in sorted(resumen) if resumen[r]["clase"] == "completa" or (resumen[r]["clase"] == "incompleta" and resumen[r]["n"] >= 70)]
    filas = []; ctxs = {}
    for r in sorted(ruedas):
        prev = [x for x in hist if x < r]
        if len(prev) < 10: continue
        med10 = float(np.median([resumen[x]["rango"] for x in prev[-10:]]))
        med60 = float(np.median([resumen[x]["rango"] for x in prev[-60:]])) if len(prev) >= 20 else np.nan
        def vr_de(lst):
            s1 = n1 = s6 = n6 = 0.0
            for x in lst:
                q = resumen[x]["vr"]
                if q is None: continue
                s1 += q[0]; n1 += q[1]; s6 += q[2]; n6 += q[3]
            return (s6 / n6) / (6 * s1 / n1) if n1 and n6 and s1 > 0 else np.nan
        ctx = dict(med10=med10, med60=med60, X=X_de(med10), vr10=vr_de(prev[-10:]), vr20=vr_de(prev[-20:]))
        ctxs[r] = ctx
        filas.append(rasgos_rueda(r, ruedas[r], ctx, f))
    pts = pd.concat(filas, ignore_index=True)
    pts["muestra"] = pts["dia"].map(K.muestra_de)
    pts.to_parquet(os.path.join(K.SCR, "puntos5.parquet"), index=False)
    pickle.dump(dict(ruedas=ruedas, ctx=ctxs, clases=clases, resumen={k: {kk: vv for kk, vv in v_.items() if kk != "vr"} for k, v_ in resumen.items()}),
                open(os.path.join(K.SCR, "ruedas5.pkl"), "wb"))
    cnt = pts.groupby("muestra")["dia"].nunique()
    print("ruedas con contexto: %d | por muestra: %s | puntos %d | %.1f s" % (pts["dia"].nunique(), dict(cnt), len(pts), time.time() - t0))
    print("clases:", pd.Series(clases).value_counts().to_dict())


if __name__ == "__main__":
    main()
