# -*- coding: utf-8 -*-
"""punta_09_presente.py — DESCRIPTIVO (no gatillo): el flujo pasivo sirve para LEER el presente aunque no prediga?
  M1  cuanto del movimiento de la vela ACTUAL explica el delta solo, y cuanto delta + ofi_pas (R2 por dia, promedio).
  M2  el movimiento de la vela se parte en dos: lo que 'pago' el delta (parte agresiva) y el resto (parte que no explica el delta: limites que se corren, vacio).
      La vela SIGUIENTE devuelve distinto segun de que parte vino el movimiento? coeficiente por dia -> media y t entre dias.
Velas de 30 s, 60 s, 120 s y 300 s, solo rueda, sin huecos de datos. Por separado en explorar y confirmar."""
import sys; sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\gatillo")
import numpy as np, pandas as pd, base as B, punta_lib as L

T = B.todas_las_sesiones()


def velas(d, regla):
    r = d[d["rueda"]]; mid = (r["bid"] + r["ask"]) / 2.0; g = r.resample(regla, label="left", closed="left")
    v = pd.DataFrame({"o": mid.resample(regla).first(), "c": mid.resample(regla).last(), "delta": g["delta"].sum(), "pas": g["ofi_pas"].sum(), "n": g["n"].sum()}).dropna()
    v["ret"] = v["c"] - v["c"].shift(1); v["sig"] = v["ret"].shift(-1); v["n_sig"] = v["n"].shift(-1)
    return v[(v["n"] >= 10) & (v["n_sig"] >= 10)].dropna().iloc[3:]


def r2(X, y):
    X = np.column_stack([np.ones(len(y))] + list(X)); b = np.linalg.lstsq(X, y, rcond=None)[0]; e = y - X @ b
    return 1 - e.var() / y.var(), b


def tt(v):
    v = np.asarray(v, float); return "%+.3f (t %+.1f, %d/%d dias > 0)" % (v.mean(), v.mean() / (v.std(ddof=1) / np.sqrt(len(v))), int((v > 0).sum()), len(v))


for nombre, S in (("EXPLORAR", B.explorar(T)), ("CONFIRMAR", B.confirmar(T))):
    print("\n================ %s" % nombre)
    for regla in ("30s", "60s", "120s", "300s"):
        A, Bb, ca, cr, c0 = [], [], [], [], []
        for s, d in S.items():
            v = velas(d, regla)
            if len(v) < 50: continue
            y = v["ret"].to_numpy(); r_d, b_d = r2([v["delta"].to_numpy()], y); r_dp, _ = r2([v["delta"].to_numpy(), v["pas"].to_numpy()], y)
            A.append(r_d); Bb.append(r_dp)
            agres = b_d[1] * v["delta"].to_numpy(); resto = y - agres                     # lo que explica el delta / lo que no
            _, b = r2([agres, resto], v["sig"].to_numpy()); ca.append(b[1]); cr.append(b[2])
            _, b1 = r2([y], v["sig"].to_numpy()); c0.append(b1[1])
        print("velas de %-4s | M1: R2 delta solo %.2f -> delta + pasivo %.2f | M2: la vela siguiente devuelve, por cada punto... de la vela entera %s | de la parte AGRESIVA %s | del RESTO (no explicado por el delta) %s" % (
            regla, np.mean(A), np.mean(Bb), tt(c0), tt(ca), tt(cr)))
