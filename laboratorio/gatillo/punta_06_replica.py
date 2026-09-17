# -*- coding: utf-8 -*-
"""punta_06_replica.py — DESPUES del veredicto: los APRENDIZAJES de microestructura (no gatillos) medidos por separado en explorar y en confirmar,
para saber cuales son estables. Lista fijada antes de mirar confirmar:
  L1 QI instantaneo -> mid a 1 s y 5 s          L2 microprecio - ultimo -> mid a 10 y 60 s      L3 delta fuerte 60 s -> reversion a 60 y 300 s
  L4 punta flaca -> mas movimiento absoluto (y barrera +-8 mas rapida), con control de hora y de volatilidad reciente
  L5 QI medio 30 s -> al reves a 30 y 300 s     L6 delta fuerte x pasivo alineado -> reversion a 60 s
Se excluyen los segundos en huecos de datos (menos de 5 ordenes en los 30 s previos o en los 60 s siguientes)."""
import sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, punta_lib as L

T = B.todas_las_sesiones()


def armar(S):
    partes = []
    for s, d in S.items():
        f = L.rasgos(d); ok = L.elegible(d)
        for h in (1, 5, 10, 30, 60, 300): f["f%d" % h] = f["mid"].shift(-h) - f["mid"]
        n = d["n"].astype("float64"); n30 = n.rolling(30).sum(); nf60 = n.rolling(60).sum().shift(-60)
        f["dia"] = s; f["balde"] = (np.arange(len(d)) + L.t0_de(d)) // 1800
        f["prof10"] = f["prof"].rolling(10).mean(); f["vol60"] = d["alto"].rolling(60).max() - d["bajo"].rolling(60).min()
        partes.append(f[ok & (n30.to_numpy() >= 5) & (nf60.fillna(0).to_numpy() >= 5)])
    return pd.concat(partes)


def t_dias(v):
    v = np.asarray(v, float); v = v[~np.isnan(v)]
    return "%+.3f (t %+.1f, %d/%d dias)" % (v.mean(), v.mean() / (v.std(ddof=1) / np.sqrt(len(v))), int((v > 0).sum()), len(v))


def extremos(R, c, h, q=0.1):
    lo, hi = R[c].quantile(q), R[c].quantile(1 - q)
    dd = R[R[c] >= hi].groupby("dia")["f%d" % h].mean() - R[R[c] <= lo].groupby("dia")["f%d" % h].mean()
    return t_dias(dd / 2.0)


for nombre, S in (("EXPLORAR", B.explorar(T)), ("CONFIRMAR", B.confirmar(T))):
    R = armar(S); print("\n================ %s: %d segundos, %d dias" % (nombre, len(R), R["dia"].nunique()))
    print("L1 QI instantaneo      ->  1 s: %s |   5 s: %s" % (extremos(R, "qi", 1), extremos(R, "qi", 5)))
    print("L2 microprecio-ultimo  -> 10 s: %s |  60 s: %s" % (extremos(R, "mp_ult", 10), extremos(R, "mp_ult", 60)))
    print("L3 delta 60 s          -> 60 s: %s | 300 s: %s" % (extremos(R, "zD60", 60), extremos(R, "zD60", 300)))
    print("L5 QI medio 30 s       -> 30 s: %s | 300 s: %s" % (extremos(R, "qi30", 30), extremos(R, "qi30", 300)))
    a = np.sign(R["zD60"]); fuerte = R["zD60"].abs() >= 1.5; pal = R["zP60"] * a
    for nb, m in (("pasivo MUY en contra (<= -1,25)", pal <= -1.25), ("pasivo neutro (-0,5 a 0,5)", pal.abs() < 0.5), ("pasivo MUY a favor (>= 1,25)", pal >= 1.25)):
        g = R[fuerte & m]; print("L6 delta fuerte, %-32s mid a 60 s en la direccion del delta: %s | n %d" % (nb, t_dias((g["f60"] * a[fuerte & m]).groupby(g["dia"]).mean()), len(g)))
    # L4: profundidad rankeada DENTRO de cada (dia, media hora) para sacar el efecto de la hora; y dentro de cada tercil de rango reciente
    R["rp"] = R.groupby(["dia", "balde"])["prof10"].rank(pct=True); R["rv"] = R.groupby(["dia", "balde"])["vol60"].rank(pct=True)
    R["qp"] = pd.cut(R["rp"], [0, 0.2, 0.4, 0.6, 0.8, 1.0], labels=["flaca", "2", "3", "4", "gruesa"]); R["tv"] = pd.cut(R["rv"], [0, 1 / 3, 2 / 3, 1.0], labels=["calmo", "medio", "movido"])
    print("L4 |movimiento del mid a 60 s| en puntos, profundidad (media 10 s) rankeada dentro de la misma media hora:")
    print(R.assign(am=R["f60"].abs()).pivot_table(index="qp", columns="tv", values="am", aggfunc="mean", observed=True).round(2).to_string())
    dd = R[R["qp"] == "flaca"].groupby("dia")["f60"].apply(lambda v: v.abs().mean()) / R[R["qp"] == "gruesa"].groupby("dia")["f60"].apply(lambda v: v.abs().mean())
    print("   razon flaca / gruesa por dia: media %.2f | minimo %.2f | dias con razon > 1: %d de %d" % (dd.mean(), dd.min(), int((dd > 1).sum()), len(dd)))
    print("   profundidad media por quintil:", R.groupby("qp", observed=True)["prof10"].mean().round(1).to_dict())
