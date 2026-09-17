# -*- coding: utf-8 -*-
"""absorcion_08_microestructura.py — DESCRIPTIVO (no es busqueda de gatillo; se corre despues de cerrada la confirmacion).
Que es una absorcion en MNQ y cuanto dura su efecto. Todo se muestra por separado en explorar y en confirmar para ver si repite.
  A. por minuto: absorcion neta (bid - ask) contra el retorno de ESE minuto y del SIGUIENTE (lo mismo que ya se midio con el delta).
  B. respuesta del precio MEDIO (bid+ask)/2 despues de un segundo con absorcion en el bid, contra segundos con la misma venta agresora sin absorcion.
  C. los disparos R30: que venia haciendo el precio antes, y cuanto tarda en resolverse la barrera contra el placebo.
  D. robustez: R30 con las columnas crudas de base.py (sin limpiar el 16 % dudoso)."""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd, base as B, absorcion_base as A, absorcion_variantes as V
T = A.tablas(); GR = (("explorar", B.explorar(T)), ("confirmar", B.confirmar(T)))
pd.set_option("display.width", 250)

print("A. POR MINUTO (rueda): correlacion de la absorcion neta (AB - AA) con el retorno del mismo minuto y del siguiente")
for rot, S in GR:
    M = []
    for s, seg in S.items():
        f = A.rasgos(s, seg); r = seg["rueda"].to_numpy()
        m = pd.DataFrame({"neta": (f["ab"] - f["aa"])[r], "ab": f["ab"][r], "aa": f["aa"][r], "delta": seg["delta"][r], "c": seg["ultimo"][r]}).resample("1min").agg({"neta": "sum", "ab": "sum", "aa": "sum", "delta": "sum", "c": "last"})
        m["ret"] = m["c"].diff(); m["ret_sig"] = m["ret"].shift(-1); M.append(m.dropna())
    M = pd.concat(M)
    print("  %-9s n=%d | neta~ret mismo minuto %+.3f | neta~ret siguiente %+.3f | delta~ret mismo %+.3f | delta~ret siguiente %+.3f | ab~delta %+.3f (se absorbe en el bid cuando hay venta)" % (
        rot, len(M), M["neta"].corr(M["ret"]), M["neta"].corr(M["ret_sig"]), M["delta"].corr(M["ret"]), M["delta"].corr(M["ret_sig"]), M["ab"].corr(M["delta"])))

print("\nB. RESPUESTA DEL PRECIO MEDIO despues de un segundo con absorcion limpia en la punta (firmado: + = a favor del que absorbio)")
KS = (1, 2, 5, 10, 30, 60, 120, 300)
for rot, S in GR:
    filas = []
    for s, seg in S.items():
        f = A.rasgos(s, seg); v = A.valido_de(s, seg); mid = ((seg["bid"] + seg["ask"]) / 2).to_numpy(); N = len(mid); d = seg["delta"].to_numpy()
        for lado, col in ((1, "ab"), (-1, "aa")):
            a = f[col].to_numpy(); sd = -d * lado   # venta agresora contra el bid (o compra contra el ask), positiva
            base = v & (sd > 0); q = np.digitize(sd, [5, 10, 20, 40, 80])
            for nombre, m in (("con absorcion", base & (a > 0)), ("con absorcion >= 10", base & (a >= 10)), ("sin absorcion", base & (a == 0))):
                pos = np.flatnonzero(m)
                for k in KS: filas.append(pd.DataFrame({"caso": nombre, "k": k, "q": q[pos], "mov": (mid[np.clip(pos + k, 0, N - 1)] - mid[pos]) * lado}))
    R = pd.concat(filas); g = R.groupby(["caso", "k"])["mov"].agg(["mean", "size"]).unstack("k")
    print("  ---", rot); print(g["mean"].round(3).to_string()); print("  n:", g["size"].iloc[:, 0].to_dict())
    # emparejado por cubeta de venta agresora del segundo: diferencia con absorcion - sin absorcion
    e = R[R["caso"] != "con absorcion >= 10"].groupby(["q", "caso", "k"])["mov"].mean().unstack("caso"); w = R[R["caso"] == "con absorcion"].groupby(["q", "k"]).size()
    dif = (e["con absorcion"] - e["sin absorcion"]); dif = (dif * w).groupby("k").sum() / w.groupby("k").sum()
    print("  diferencia emparejada por tamano de la venta del segundo (con - sin), puntos:", dif.round(3).to_dict())

print("\nC. DISPAROS R30: que venia haciendo el precio y cuanto tarda la barrera")
for rot, S in GR:
    f = A.disparos(S, V.R30); pre30 = []; pre120 = []; tt = []; tp = []
    rng = np.random.default_rng(3)
    for s, g in f.groupby("sesion"):
        seg = S[s]; p = seg["ultimo"].to_numpy(); pos = g["pos"].to_numpy(); l = g["lado"].to_numpy()
        pre30 += list((p[pos] - p[pos - 30]) * l); pre120 += list((p[pos] - p[pos - 120]) * l)
        _, th = B.barrera(seg, 8, 600); tt += list(th[pos]); v = np.flatnonzero(A.valido_de(s, seg)); tp += list(th[rng.choice(v, 2000)])
    tt = np.asarray(tt); tp = np.asarray(tp)
    print("  %-9s n=%d | movimiento PREVIO firmado por el lado del disparo: 30 s %+.2f pts, 120 s %+.2f pts (negativo = el precio venia EN CONTRA del que absorbe)" % (rot, len(f), np.mean(pre30), np.mean(pre120)))
    print("            segundos hasta tocar +-8: disparos mediana %.0f (p25 %.0f, p75 %.0f) | al azar mediana %.0f (p25 %.0f, p75 %.0f)" % (
        np.median(tt[np.isfinite(tt)]), np.percentile(tt[np.isfinite(tt)], 25), np.percentile(tt[np.isfinite(tt)], 75), np.median(tp[np.isfinite(tp)]), np.percentile(tp[np.isfinite(tp)], 25), np.percentile(tp[np.isfinite(tp)], 75)))

print("\nD. ROBUSTEZ: R30 con las columnas crudas de base.py (abs_bid / abs_ask sin limpiar), mismo umbral de percentil 99 (26)")
def R30_CRUDA(s, seg):
    ab = seg["abs_bid"].rolling(30, min_periods=1).sum(); aa = seg["abs_ask"].rolling(30, min_periods=1).sum()
    return V._dos((ab >= 26) & (ab > aa), (aa >= 26) & (aa > ab))
for rot, S in GR:
    f = A.disparos(S, R30_CRUDA); r = A.resumen(f, "R30 cruda"); print("  %-9s" % rot, {k: r[k] for k in ("disparos", "n8", "ac8", "z8", "dias8", "neto8")})
