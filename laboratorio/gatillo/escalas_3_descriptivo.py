# -*- coding: utf-8 -*-
"""Paso 3 (descriptivo, no elige nada): en que marco el delta describe y en cual adelanta; quien mueve el precio por tamaño de orden.
Uso: python escalas_3_descriptivo.py explorar | confirmar | todas"""
import sys, time
import numpy as np, pandas as pd
import escalas_lib as L
B = L.B
pd.set_option("display.width", 250); pd.set_option("display.max_columns", 40)
cual = sys.argv[1] if len(sys.argv) > 1 else "explorar"
T = B.todas_las_sesiones(); T = B.explorar(T) if cual == "explorar" else B.confirmar(T) if cual == "confirmar" else T
U = L.cargar_umbrales(); CUB = ["d1", "d2_4", "d5_9", "d10_49", "d50"]
print("DESCRIPTIVO sobre", cual, "(%d sesiones)" % len(T))


def corr(a, b):
    a = np.asarray(a, "float64"); b = np.asarray(b, "float64"); m = np.isfinite(a) & np.isfinite(b)
    return float(np.corrcoef(a[m], b[m])[0, 1]) if m.sum() > 10 else np.nan


def pares_reloj(W):
    """Por sesion, velas de reloj de W s dentro de la rueda: delta, retorno propio, retorno siguiente, deltas por cubeta."""
    filas = []
    for s in sorted(T):
        seg = T[s]; r = seg[seg["rueda"]]
        if len(r) < 3 * W: continue
        g = r.resample("%ds" % W, label="left", closed="left")
        b = pd.DataFrame({"c": g["ultimo"].last(), "delta": g["delta"].sum(), "vol": g["vol"].sum()})
        for c in CUB: b[c] = g[c].sum()
        b = b.dropna(subset=["c"]); b["ret"] = b["c"].diff(); b["ret_sig"] = b["ret"].shift(-1); b["ses"] = s
        filas.append(b.iloc[1:-1])
    return pd.concat(filas)


print("\nD1. VELAS DE RELOJ: delta contra el retorno de la MISMA vela y de la SIGUIENTE")
print("%-7s %7s %10s %10s %9s %12s %14s %16s" % ("marco", "velas", "corr misma", "corr sig.", "+-2 err", "sig=lado %", "dias corr>0", "extremo: sig %"))
for nom, W in (("5 s", 5), ("15 s", 15), ("30 s", 30), ("1 min", 60), ("2 min", 120), ("5 min", 300), ("15 min", 900), ("30 min", 1800)):
    b = pares_reloj(W); m = (b["delta"] != 0) & (b["ret_sig"] != 0)
    sig = float((np.sign(b["delta"][m]) == np.sign(b["ret_sig"][m])).mean())
    dias = b.groupby("ses").apply(lambda x: corr(x["delta"], x["ret_sig"]), include_groups=False)
    r = (b["delta"] / b["vol"].replace(0, np.nan)).abs(); ext = m & (r >= r.quantile(0.9))
    sig_ext = float((np.sign(b["delta"][ext]) == np.sign(b["ret_sig"][ext])).mean())
    print("%-7s %7d %10.3f %10.3f %9.3f %12.1f %14s %13.1f (n %d)" % (nom, len(b), corr(b["delta"], b["ret"]), corr(b["delta"], b["ret_sig"]), 2 / np.sqrt(len(b)), 100 * sig, "%d de %d" % ((dias > 0).sum(), len(dias)), 100 * sig_ext, ext.sum()))

print("\nD2. VELAS POR VOLUMEN (%d contratos) Y POR RANGO (%g puntos)" % (U["Vb"], L.RANGO_RB))
for tipo in ("volumen", "rango"):
    dd_, rr_, rs_ = [], [], []
    for s in sorted(T):
        seg = T[s]; f = L.rasgos(seg)
        if tipo == "volumen": ci, dd, vv = L.velas_volumen(seg, f, U["Vb"])
        else: ci, dr, dd, vv = L.velas_rango(seg, f)
        if len(ci) < 5: continue
        c = f["p"][ci]; ret = np.diff(c, prepend=np.nan); sig = np.append(ret[1:], np.nan)
        dd_.append(dd[1:-1]); rr_.append(ret[1:-1]); rs_.append(sig[1:-1])
    d = np.concatenate(dd_); r = np.concatenate(rr_); sg_ = np.concatenate(rs_); m = (d != 0) & (sg_ != 0)
    print("  %-8s velas %6d | corr misma %.3f | corr siguiente %.3f (+-%.3f) | siguiente para el lado del delta %.1f %%" % (tipo, len(d), corr(d, r), corr(d, sg_), 2 / np.sqrt(len(d)), 100 * float((np.sign(d[m]) == np.sign(sg_[m])).mean())))

print("\nD3. POR TAMAÑO DE ORDEN")
for nom, W in (("1 min", 60), ("5 min", 300), ("15 min", 900)):
    b = pares_reloj(W); tot = sum(b[c].abs().sum() for c in CUB)
    print("  marco %s (%d velas)" % (nom, len(b)))
    print("    %-8s %12s %12s %12s %16s" % ("cubeta", "% del |delta|", "corr misma", "corr sig.", "corr con d1"))
    for c in CUB:
        print("    %-8s %12.1f %12.3f %12.3f %16.3f" % (c, 100 * b[c].abs().sum() / tot, corr(b[c], b["ret"]), corr(b[c], b["ret_sig"]), corr(b[c], b["d1"])))
    X = b[CUB].to_numpy("float64"); X = (X - X.mean(0)) / X.std(0); A = np.column_stack([X, np.ones(len(X))])
    for dest in ("ret", "ret_sig"):
        y = b[dest].to_numpy("float64"); coef, *_ = np.linalg.lstsq(A, y, rcond=None); pred = A @ coef; r2 = 1 - ((y - pred) ** 2).sum() / ((y - y.mean()) ** 2).sum()
        print("    regresion de %-7s sobre las 5 cubetas (puntos por 1 desvio): %s | R2 %.3f" % (dest, "  ".join("%s %+.2f" % (c, k) for c, k in zip(CUB, coef[:5])), r2))
    ch = b["d1"] + b["d2_4"]; gr = b["d10_49"] + b["d50"]; div = (np.sign(ch) == -np.sign(gr)) & (gr != 0)
    mm = div & (b["ret"] != 0); ms = div & (b["ret_sig"] != 0)
    print("    cuando chicos y grandes tiran opuesto (%.0f %% de las velas): la MISMA vela va con los grandes %.1f %% | la SIGUIENTE va con los grandes %.1f %% (n %d)" % (
        100 * div.mean(), 100 * float((np.sign(gr[mm]) == np.sign(b["ret"][mm])).mean()), 100 * float((np.sign(gr[ms]) == np.sign(b["ret_sig"][ms])).mean()), ms.sum()))
