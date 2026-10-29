# -*- coding: utf-8 -*-
"""Ronda 2 de calibracion de la Corriente (PREREGISTRO_CALIBRACION_2.md): 114 candidatos de tres mecanismos (G1 escala
que no se infla, G2 linea que se acerca con la tendencia estirada, G3/G3d vuelta rapida por velocidad) contra la base
ST(10,3) y la curva F1. MES 1 min DISEÑO/EXAMEN y MNQ 1 min como segundo examen.
Salidas: cal_03_salida.txt, cal3_resumen.csv, cal3_movimientos.csv."""
import hashlib
import sys
from pathlib import Path

import numpy as np
import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parent))
import t_lib as T

T.idle()
LAB = T.LAB
NUEVA = T.BASE / "tres" / "datos" / "velas_cache_2026-10-29" / "velas-MES-m1.csv"
FIN_DISENO = pd.Timestamp("2026-10-05")
FIN_EXAMEN = pd.Timestamp("2026-10-28")
SEG = 120
p = T.Salida("cal_03_salida.txt")
p("CALIBRACION 2. SHA-256 PREREGISTRO_CALIBRACION_2.md:",
  hashlib.sha256((LAB / "PREREGISTRO_CALIBRACION_2.md").read_bytes()).hexdigest())


# ------------------------------------------------------------------ series
def serie_mes():
    orig = T.cargar_crudo

    def ext(serie):
        v = orig("MES_m1")
        b = T._leer_csv(NUEVA)
        b = b[b.t > v.t.max()].copy()
        b["fuente"] = "cache_2026-10-29/MES_m1"
        b["sesion"] = T._sesion(b["t"]).astype("datetime64[ns]")
        b["contrato"], b["seg"], b["tf"] = "MESZ6", 1, 1
        b["t_fin"] = b["t"] + pd.Timedelta(minutes=1)
        return pd.concat([v, b[v.columns]], ignore_index=True).sort_values("t").reset_index(drop=True)

    T.cargar_crudo = ext
    v = T.preparar("MES_m1")
    T.cargar_crudo = orig
    v = v[v.sesion <= FIN_EXAMEN].reset_index(drop=True)
    dl = []
    for f in (T.R3V / "velas-MES-m1.csv", T.CACHE / "velas-MES-m1.csv", NUEVA):
        x = pd.read_csv(f, usecols=["t", "delta"])
        x["t"] = pd.to_datetime(x["t"], utc=True)
        dl.append(x)
    dl = pd.concat(dl).drop_duplicates("t", keep="last")
    v = v.merge(dl, on="t", how="left")
    assert v.delta.notna().all(), "faltan deltas en MES"
    v["tramo"] = np.where(v.sesion <= FIN_DISENO, "DISENO", "EXAMEN")
    return v


def serie_mnq():
    v = T.leer("MNQ_m1")
    x = pd.read_parquet(T.R4 / "MNQ_m1.parquet", columns=["t", "delta"])
    v = v.merge(x, on="t", how="left")
    assert v.delta.notna().all(), "faltan deltas en MNQ"
    v["tramo"] = "MNQ"
    return v


class Serie:
    def __init__(self, v, X, VENT, X2, VENT2, falsa):
        self.v = v
        self.h, self.l, self.c = v.ha.values, v.la.values, v.ca.values
        self.dl = v.delta.values.astype(float)
        self.blq, self.val, self.ses, self.rth = v.bloque.values, v.valido.values, v.sesion.values, v.rth.values
        self.tramo = v.tramo.values
        self.tfin = (v.t_fin.values - np.datetime64("2000-01-01T00:00")) / np.timedelta64(1, "m")
        ok = v.groupby("sesion").valido.all()
        self.ses_ok = sorted(ok[ok].index)
        self.falsa = falsa
        R = v[v.rth].groupby("sesion").agg(hi=("ha", "max"), lo=("la", "min"), tramo=("tramo", "first"))
        R["rango"] = R.hi - R.lo
        self.volatil = set()
        for _, g in R.groupby("tramo"):
            self.volatil |= set(g.sort_values("rango", ascending=False).index[:len(g) // 3])
        self.bl = T._bloques(self.blq)
        self.ini = np.zeros(len(v), int)
        self.fin = np.zeros(len(v), int)
        for a, b in self.bl:
            self.ini[a:b] = a
            self.fin[a:b] = b
        self._atr = {}
        self.M = self.movs(X, VENT)
        self.M2 = self.movs(X2, VENT2)
        self.a60 = self.atr(60)

    def atr(self, n):
        if n not in self._atr:
            self._atr[n] = T.rma(T.true_range(self.h, self.l, self.c, self.blq), n, self.blq)
        return self._atr[n]

    def atr_atras(self, n, k):
        a = self.atr(n)
        out = np.full(len(a), np.nan)
        idx = np.arange(len(a))
        ok = idx - k >= self.ini
        out[ok] = a[idx[ok] - k]
        return out

    def mediana_tr(self, L):
        tr = T.true_range(self.h, self.l, self.c, self.blq)
        out = np.full(len(tr), np.nan)
        for a, b in self.bl:
            out[a:b] = pd.Series(tr[a:b]).rolling(L, min_periods=L).median().values
        return out

    def movs(self, X, VENT):
        h, l, ses, val, tfin = self.h, self.l, self.ses, self.val, self.tfin
        res = []
        for se in pd.unique(ses):
            idx = np.nonzero(ses == se)[0]
            tt, hh, ll = tfin[idx], h[idx], l[idx]
            for sentido in (1, -1):
                ultimo = -1e18
                for q in range(len(idx)):
                    if not val[idx[q]]:
                        continue
                    lo = np.searchsorted(tt, tt[q] - VENT, side="left")
                    hi = np.searchsorted(tt, tt[q] + VENT, side="right")
                    if sentido == 1 and ll[q] > ll[lo:hi].min():
                        continue
                    if sentido == -1 and hh[q] < hh[lo:hi].max():
                        continue
                    fut = np.arange(q + 1, hi)
                    if len(fut) == 0:
                        continue
                    alc = np.nonzero(hh[fut] - ll[q] >= X)[0] if sentido == 1 else np.nonzero(hh[q] - ll[fut] >= X)[0]
                    if len(alc) == 0 or tt[q] - ultimo <= VENT:
                        continue
                    ultimo = tt[q]
                    res.append(dict(sesion=pd.Timestamp(se), i=int(idx[q]), sentido=sentido,
                                    extremo=ll[q] if sentido == 1 else hh[q], i_alcanza=int(idx[fut[alc[0]]])))
        M = pd.DataFrame(res)
        M["tramo"] = self.tramo[M.i.values]
        M["volatil"] = M.sesion.isin(self.volatil)
        return M

    def retraso(self, st, M):
        c, blq, tfin = self.c, self.blq, self.tfin
        pts = np.full(len(M), np.inf)
        for q, (i, s, e) in enumerate(zip(M.i.values, M.sentido.values, M.extremo.values)):
            j = i
            while j < len(st) and blq[j] == blq[i] and tfin[j] - tfin[i] <= SEG:
                if st[j] == s:
                    pts[q] = (c[j] - e) * s
                    break
                j += 1
        return pts

    def ruido(self, st):
        h, l, c, blq, val = self.h, self.l, self.c, self.blq, self.val
        prev = np.r_[0, st[:-1]]
        prev_ok = np.r_[False, (blq[1:] == blq[:-1]) & val[:-1]]
        idx = np.nonzero(val & prev_ok & (st != prev))[0]
        sig = np.r_[idx[1:], len(st)]
        mfe = np.full(len(idx), np.nan)
        for q, j in enumerate(idx):
            k = min(sig[q], self.fin[j] - 1)
            if k <= j:
                continue
            mfe[q] = (h[j + 1:k + 1].max() - c[j]) if st[j] == 1 else (c[j] - l[j + 1:k + 1].min())
        df = pd.DataFrame(dict(sesion=self.ses[idx], rth=self.rth[idx], f5=mfe < self.falsa, fn=mfe < 2 * self.a60[idx]))
        df = df[df.sesion.isin(self.ses_ok)]
        out = {}
        for tr_ in pd.unique(self.tramo):
            ss = [s for s in self.ses_ok if self.tramo[np.searchsorted(self.ses, np.datetime64(s))] == tr_]
            g = df[df.sesion.isin(ss)]
            gr = g[g.rth]
            n = len(ss)
            out[tr_] = dict(ses=n, cambios=len(g) / n, falsas5=g.f5.sum() / n, falsasN=g.fn.sum() / n,
                            cambios_rueda=len(gr) / n, falsas5_rueda=gr.f5.sum() / n, falsasN_rueda=gr.fn.sum() / n)
        return out


def corriente(S, A, m0, mmin=None, a=0.0, v=None, j=None, usar_delta=False):
    """Motor unico: SuperTrend con trinquete; G2 (a > 0) acerca la linea con la extension; G3 (v) vuelta rapida."""
    h, l, c, dl = S.h, S.l, S.c, S.dl
    est = np.zeros(len(c), np.int8)
    for ia, ib in S.bl:
        d = 0
        fu = fl = ext = cflip = 0.0
        iext = 0
        for t in range(ia, ib):
            At = A[t]
            if At != At:
                continue
            if d != 0:
                if d == 1 and h[t] > ext:
                    ext, iext = h[t], t
                elif d == -1 and l[t] < ext:
                    ext, iext = l[t], t
            m = m0
            if a > 0 and d != 0:
                m = max(mmin, m0 - a * (ext - cflip) * d / At)
            mid = (h[t] + l[t]) / 2.0
            bu, bl_ = mid + m * At, mid - m * At
            if d == 0:
                fu, fl, d = bu, bl_, -1
                ext, iext, cflip = l[t], t, c[t]
                est[t] = d
                continue
            cp = c[t - 1]
            nfl = bl_ if (bl_ > fl or cp < fl) else fl
            nfu = bu if (bu < fu or cp > fu) else fu
            nd = d
            if d == -1 and c[t] > nfu:
                nd = 1
            elif d == 1 and c[t] < nfl:
                nd = -1
            if nd == d and v is not None and t - iext <= j:
                if d == 1 and ext - c[t] >= v * At and (not usar_delta or dl[iext + 1:t + 1].sum() < 0):
                    nd = -1
                elif d == -1 and c[t] - ext >= v * At and (not usar_delta or dl[iext + 1:t + 1].sum() > 0):
                    nd = 1
            fu, fl = nfu, nfl
            if nd != d:
                d = nd
                ext, iext, cflip = (h[t] if d == 1 else l[t]), t, c[t]
            est[t] = d
    return est


def candidatos(S):
    C = []
    for n in (10, 60):
        for m in (1.5, 2.0, 2.5, 3.0, 3.5, 4.0):
            C.append((f"F1 ST({n},{m:g})", "F1", lambda n=n, m=m: corriente(S, S.atr(n), m)))
    for k in (5, 10, 20):
        for m in (2.5, 3.0, 3.5):
            C.append((f"G1 ATR10atras{k} x{m:g}", "G1", lambda k=k, m=m: corriente(S, S.atr_atras(10, k), m)))
    for L in (30, 60):
        for m in (3.0, 3.5, 4.0, 4.5):
            C.append((f"G1 medTR{L} x{m:g}", "G1", lambda L=L, m=m: corriente(S, S.mediana_tr(L), m)))
    for m in (2.5, 3.0, 3.5):
        C.append((f"G1 min(ATR10,60) x{m:g}", "G1", lambda m=m: corriente(S, np.fmin(S.atr(10), S.atr(60)) if True else None, m)))
    for n in (10, 60):
        for m0 in (3.0, 3.5, 4.0):
            for mm in (1.5, 2.0):
                for a in (0.1, 0.25, 0.5):
                    C.append((f"G2 n{n} m{m0:g}->{mm:g} a{a:g}", "G2",
                              lambda n=n, m0=m0, mm=mm, a=a: corriente(S, S.atr(n), m0, mmin=mm, a=a)))
    for n in (10, 60):
        for m in (3.0, 3.5, 4.0):
            for vv in (1.5, 2.0, 2.5):
                for jj in (3, 5, 8):
                    C.append((f"G3 ST({n},{m:g}) v{vv:g} j{jj}", "G3",
                              lambda n=n, m=m, vv=vv, jj=jj: corriente(S, S.atr(n), m, v=vv, j=jj)))
    for vv in (1.5, 2.0):
        for jj in (5, 8):
            C.append((f"G3d ST(60,3.5) v{vv:g} j{jj}", "G3d",
                      lambda vv=vv, jj=jj: corriente(S, S.atr(60), 3.5, v=vv, j=jj, usar_delta=True)))
    return C


def evaluar(S, nombre_serie):
    filas, movs = [], []
    C = candidatos(S)
    assert len([x for x in C if x[1] != "F1"]) == 114, len(C)
    for nom, fam, fn in C:
        st = fn()
        rz = S.ruido(st)
        p1 = S.retraso(st, S.M)
        p2 = S.retraso(st, S.M2)
        for q in range(len(S.M)):
            movs.append((nombre_serie, nom, q, S.M.tramo.values[q], bool(S.M.volatil.values[q]), p1[q]))
        for tr_ in rz:
            s1 = S.M.tramo.values == tr_
            s2 = S.M2.tramo.values == tr_
            sv = s1 & S.M.volatil.values
            filas.append(dict(serie=nombre_serie, cand=nom, fam=fam, tramo=tr_, n=int(s1.sum()),
                              tarde=float(np.median(p1[s1])), tarde_vol=float(np.median(p1[sv])) if sv.any() else np.nan,
                              cens=float(np.isinf(p1[s1]).mean() * 100), tarde2=float(np.median(p2[s2])), **rz[tr_]))
        print(nombre_serie, nom, flush=True)
    return pd.DataFrame(filas), pd.DataFrame(movs, columns=["serie", "cand", "mov", "tramo", "volatil", "pts"])


# ------------------------------------------------------------------ control: el motor reproduce t_lib en la base
SM = Serie(serie_mes(), 20.0, 30, 10.0, 15, 5.0)
assert (corriente(SM, SM.atr(10), 3.0) == T.det_supertrend(SM.h, SM.l, SM.c, SM.blq, 10, 3.0)).all()
assert (corriente(SM, SM.atr(60), 3.0) == T.det_supertrend(SM.h, SM.l, SM.c, SM.blq, 60, 3.0)).all()
p("control: el motor sin G2/G3 == t_lib.det_supertrend (10,3) y (60,3) en las", len(SM.c), "velas de MES")
p("MES movimientos 20/30:", SM.M.groupby("tramo").size().to_dict(), "| volatiles:", sorted(str(d.date()) for d in SM.volatil))
SQ = Serie(serie_mnq(), 80.0, 30, 40.0, 15, 20.0)
p("MNQ movimientos 80/30:", len(SQ.M), "en", SQ.v.sesion.nunique(), "sesiones")

Rm, Mm = evaluar(SM, "MES")
Rq, Mq = evaluar(SQ, "MNQ")
R = pd.concat([Rm, Rq], ignore_index=True)
MV = pd.concat([Mm, Mq], ignore_index=True)
R.to_csv(LAB / "cal3_resumen.csv", index=False)
MV.to_csv(LAB / "cal3_movimientos.csv", index=False)
p("guardado cal3_resumen.csv y cal3_movimientos.csv")
