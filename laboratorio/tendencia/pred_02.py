# -*- coding: utf-8 -*-
"""Enmienda 2 del PREREGISTRO_PREDICCION.md: familia RX ("cambiar cuando el retroceso desde el extremo del tramo llega a X
puntos"), ruido medido en la rueda, X elegido en DISEÑO (MES) y en la primera mitad (MNQ), examen en EXAMEN y en la segunda
mitad de MNQ. Salida: pred_02_salida.txt."""
import numpy as np, pandas as pd
from scipy.stats import binomtest
src = open("pred_01.py", encoding="utf-8").read()
src = src.split("DM, stM = datos(SM, AM, 10.0)")[0].replace('p = T.Salida("pred_01_salida.txt")', "p = print").replace("cache=True", "cache=False")
g = {"__name__": "pred", "__file__": "pred_01.py"}
exec(compile(src, "pred_01_parcial", "exec"), g)
SM, SQ, AM, AQ, simular, ORDEN, T, corriente = g["SM"], g["SQ"], g["AM"], g["AQ"], g["simular"], g["ORDEN"], g["T"], g["corriente"]
p = T.Salida("pred_02_salida.txt")
VACIO = (np.zeros(1), 0.0, np.zeros(1, np.int64), np.zeros(1), np.zeros(1, np.int64), np.zeros(1, np.int64),
         np.zeros(1, np.bool_), np.zeros(1), np.zeros(1, np.bool_), np.zeros(1, np.int64), 0.0)


def rx(S, A, X):
    st0 = T.det_supertrend(S.h, S.l, S.c, S.blq, 10, 3.0).astype(np.int8)
    return simular(0, X / (X + 10.0), *VACIO, st0, S.val, A["blq_ini"], *[A[k] for k in ORDEN], A["escala"], 10.0)


def colas(S, st):
    pts = S.retraso(st, S.M)
    full = pts.copy()
    for q in np.nonzero(np.isinf(pts))[0]:
        i, s, e = S.M.i.values[q], S.M.sentido.values[q], S.M.extremo.values[q]
        j1 = min(len(S.c), S.fin[i], i + 121)
        full[q] = (S.h[i:j1].max() - e) if s == 1 else (e - S.l[i:j1].min())
    return pts, full


def ruido_rueda(S, st, ses_sel, falsa):
    h, l, c, blq, val = S.h, S.l, S.c, S.blq, S.val
    prev = np.r_[0, st[:-1]]
    prev_ok = np.r_[False, (blq[1:] == blq[:-1]) & val[:-1]]
    idx = np.nonzero(val & prev_ok & (st != prev))[0]
    sig = np.r_[idx[1:], len(st)]
    mfe = np.full(len(idx), np.nan)
    for q, j in enumerate(idx):
        k = min(sig[q], S.fin[j] - 1)
        if k > j:
            mfe[q] = (h[j + 1:k + 1].max() - c[j]) if st[j] == 1 else (c[j] - l[j + 1:k + 1].min())
    df = pd.DataFrame(dict(s=S.ses[idx], r=S.rth[idx], f=mfe < falsa))
    ok = [s for s in S.ses_ok if s in set(ses_sel)]
    df = df[df.s.isin(ok) & df.r]
    return len(df) / len(ok), df.f.sum() / len(ok), len(ok)


def tabla(S, A, Xs, ses_sel, falsa, etiqueta):
    filas = {}
    reglas = [("base ST(10,3)", T.det_supertrend(S.h, S.l, S.c, S.blq, 10, 3.0).astype(np.int8)),
              ("1.1", corriente(S, S.atr(60), 4.0, v=2.5, j=3))] + [(f"R{X:g}", rx(S, A, X)) for X in Xs]
    m = S.M.sesion.isin(ses_sel).values
    for nom, st in reglas:
        pts, full = colas(S, st)
        cr, fr, ns = ruido_rueda(S, st, ses_sel, falsa)
        filas[nom] = dict(n=int(m.sum()), med=np.median(pts[m]), media=full[m].mean(), p90=np.percentile(full[m], 90),
                          cr=cr, fr=fr, ns=ns, pts=pts[m], vol=S.M.volatil.values[m])
    p(f"\n==== {etiqueta} ({filas['base ST(10,3)']['n']} movimientos, {filas['base ST(10,3)']['ns']} sesiones)")
    for k, r in filas.items():
        p(f"  {k:<14} tarde: mediana {r['med']:6.2f}  media {r['media']:6.2f}  p90 {r['p90']:6.2f} | rueda: cambios {r['cr']:5.2f}  falsas {r['fr']:5.2f}")
    return filas


def examen(F, X, etiqueta):
    b, x = F["base ST(10,3)"], F[f"R{X:g}"]
    a, z = int((b["pts"] > x["pts"]).sum()), int((b["pts"] < x["pts"]).sum())
    pv = binomtest(a, a + z, 0.5, alternative="greater").pvalue
    ok = pv <= 0.05 and x["p90"] < b["p90"] and x["cr"] <= 1.1 * b["cr"] and x["fr"] <= 1.1 * b["fr"]
    av, zv = int((b["pts"][b["vol"]] > x["pts"][b["vol"]]).sum()), int((b["pts"][b["vol"]] < x["pts"][b["vol"]]).sum())
    p(f"  EXAMEN {etiqueta} R{X:g}: antes {a}, despues {z}, p {pv:.2e} (volatiles {av} contra {zv}); p90 {x['p90']:.2f} vs {b['p90']:.2f};"
      f" rueda cambios {x['cr']:.2f} vs {b['cr']:.2f}, falsas {x['fr']:.2f} vs {b['fr']:.2f} -> {'PASA' if ok else 'NO PASA'}")
    return ok


sd = sorted(set(SM.ses[SM.tramo == "DISENO"]))
se = sorted(set(SM.ses[SM.tramo == "EXAMEN"]))
XM = [6, 7, 8, 9, 10, 11, 12, 14]
Fd = tabla(SM, AM, XM, sd, 5.0, "MES DISEÑO")
Xe = min(X for X in XM if Fd[f"R{X:g}"]["cr"] <= Fd["base ST(10,3)"]["cr"])
p(f"  X elegido en DISEÑO: {Xe} pts")
Fe = tabla(SM, AM, XM, se, 5.0, "MES EXAMEN")
ok1 = examen(Fe, Xe, "MES")
sq = sorted(set(SQ.ses))
h1, h2 = sq[:len(sq) // 2], sq[len(sq) // 2:]
XQ = [30, 35, 40, 45, 50, 55, 60, 70, 80]
Fq1 = tabla(SQ, AQ, XQ, h1, 20.0, "MNQ primera mitad")
Xq = min(X for X in XQ if Fq1[f"R{X:g}"]["cr"] <= Fq1["base ST(10,3)"]["cr"])
p(f"  X elegido en la primera mitad de MNQ: {Xq} pts")
Fq2 = tabla(SQ, AQ, XQ, h2, 20.0, "MNQ segunda mitad")
ok2 = examen(Fq2, Xq, "MNQ")
p(f"\nVEREDICTO ENMIENDA 2: {'PASA LOS DOS' if ok1 and ok2 else 'NO PASA'} (MES {ok1}, MNQ {ok2})")
p.close()
