# -*- coding: utf-8 -*-
"""punta_07_lento.py — EXPLORATORIO, despues del veredicto (no es gatillo ni se confirma): y si el pasivo se mira LENTO, como un 'CVD de los limites'?
  pasivo / delta acumulados en 900 s y 1800 s, y anclados a la apertura (13:30 UTC), contra el movimiento del mid a 600 s y 1800 s.
Se mide con la t ENTRE DIAS, por separado en explorar y confirmar, excluyendo huecos de datos. Tambien: el pasivo lento agrega algo SOBRE el delta lento?"""
import sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, punta_lib as L

T = B.todas_las_sesiones()


def armar(S):
    partes = []
    for s, d in S.items():
        ok = L.elegible(d); mid = (d["bid"] + d["ask"]) / 2.0; f = pd.DataFrame(index=d.index); f["dia"] = s
        n = d["n"].astype("float64"); hueco = (n.rolling(30).sum() < 5)
        for c, k in (("ofi_pas", "P"), ("delta", "D")):
            x = d[c].astype("float64"); sd = x.rolling(1800, min_periods=600).std()
            for W in (900, 1800): f["%s%d" % (k, W)] = x.rolling(W).sum() / (sd * np.sqrt(W))
            xr = x.where(d["rueda"], 0.0); seg = d["rueda"].cumsum().clip(lower=1); f[k + "anc"] = xr.cumsum() / (sd * np.sqrt(seg))
        for h in (600, 1800): f["f%d" % h] = mid.shift(-h) - mid
        f["hueco_fut"] = hueco.rolling(1800).max().shift(-1800).fillna(1.0)
        g = f[ok & ~hueco.to_numpy() & (f["hueco_fut"].to_numpy() == 0)]
        partes.append(g.iloc[::30])          # una muestra cada 30 s alcanza para horizontes de 10 a 30 minutos
    return pd.concat(partes)


def t_dias(v):
    v = np.asarray(v, float); v = v[~np.isnan(v)]
    if len(v) < 3: return "sin dias"
    return "%+6.2f (t %+.1f, %d/%d dias)" % (v.mean(), v.mean() / (v.std(ddof=1) / np.sqrt(len(v))), int((v > 0).sum()), len(v))


def extremos(R, c, h, q=0.2):
    lo, hi = R[c].quantile(q), R[c].quantile(1 - q)
    dd = R[R[c] >= hi].groupby("dia")["f%d" % h].mean() - R[R[c] <= lo].groupby("dia")["f%d" % h].mean()
    return t_dias(dd / 2.0)


for nombre, S in (("EXPLORAR", B.explorar(T)), ("CONFIRMAR", B.confirmar(T)), ("LAS 20", T)):
    R = armar(S); print("\n================ %s: %d muestras (cada 30 s), %d dias. Seguir la señal en los quintiles extremos, puntos del mid:" % (nombre, len(R), R["dia"].nunique()))
    for c in ("P900", "P1800", "Panc", "D900", "D1800", "Danc"):
        print("  %-6s -> 600 s: %s | 1800 s: %s" % (c, extremos(R, c, 600), extremos(R, c, 1800)))
    print("  corr(P1800, D1800) = %.2f | corr(Panc, Danc) = %.2f" % (R["P1800"].corr(R["D1800"]), R["Panc"].corr(R["Danc"])))
    # el pasivo lento CONTRA el delta lento: delta 1800 fuerte y pasivo 1800 opuesto / a favor
    a = np.sign(R["D1800"]); fu = R["D1800"].abs() >= R["D1800"].abs().quantile(0.6); pal = R["P1800"] * a
    for nb, m in (("pasivo lento EN CONTRA del delta lento", pal <= -1.0), ("pasivo lento A FAVOR", pal >= 1.0)):
        g = R[fu & m]; print("  delta 30 min fuerte + %-40s mid a 1800 s en la direccion del delta: %s | n %d" % (nb, t_dias((g["f1800"] * a[fu & m]).groupby(g["dia"]).mean()), len(g)))
