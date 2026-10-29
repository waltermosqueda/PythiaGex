# -*- coding: utf-8 -*-
"""Calibracion de "PythiaGex - Corriente" (PREREGISTRO_CALIBRACION.md): 80 candidatos, retraso en movimientos grandes y
ruido (cambios y vueltas falsas por sesion) en DISEÑO (<= 10-05) y EXAMEN (10-06..10-28) de MES 1 min.
Salidas: cal_01_salida.txt, cal_resumen.csv, cal_movimientos.csv."""
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
p = T.Salida("cal_01_salida.txt")
p("CALIBRACION CORRIENTE. SHA-256 PREREGISTRO_CALIBRACION.md:",
  hashlib.sha256((LAB / "PREREGISTRO_CALIBRACION.md").read_bytes()).hexdigest())

# ------------------------------------------------------------------ serie continua (diseño + examen)
_orig = T.cargar_crudo


def cargar_ext(serie):
    v = _orig("MES_m1")
    b = T._leer_csv(NUEVA)
    b = b[b.t > v.t.max()].copy()
    b["fuente"] = "cache_2026-10-29/MES_m1"
    b["sesion"] = T._sesion(b["t"]).astype("datetime64[ns]")
    b["contrato"] = "MESZ6"
    b["seg"] = 1
    b["tf"] = 1
    b["t_fin"] = b["t"] + pd.Timedelta(minutes=1)
    v = pd.concat([v, b[v.columns]], ignore_index=True).sort_values("t").reset_index(drop=True)
    assert v.t.is_unique
    return v


T.cargar_crudo = cargar_ext
v = T.preparar("MES_m1")
v = v[v.sesion <= FIN_EXAMEN].reset_index(drop=True)
viejo = T.leer("MES_m1")
cmp = v.merge(viejo[["t", "bloque", "valido", "ca"]], on="t", suffixes=("", "_v"))
assert len(cmp) == len(viejo) and (cmp.bloque == cmp.bloque_v).all() and (cmp.valido == cmp.valido_v).all() \
    and (cmp.ca == cmp.ca_v).all(), "el tramo DISEÑO no coincide con la serie del laboratorio"
p(f"serie: {len(v)} velas, sesiones {v.sesion.nunique()} ({v.sesion.min().date()}..{v.sesion.max().date()}); "
  f"DISEÑO {v[v.sesion <= FIN_DISENO].sesion.nunique()} ses, EXAMEN {v[v.sesion > FIN_DISENO].sesion.nunique()} ses; "
  f"bloques {v.bloque.nunique()}; DISEÑO identico a velas_MES_m1.parquet: si")

h, l, c = v.ha.values, v.la.values, v.ca.values
blq = v.bloque.values
val = v.valido.values
ses = v.sesion.values
tfin = (v.t_fin.values - np.datetime64("2000-01-01T00:00")) / np.timedelta64(1, "m")
tramo = np.where(v.sesion.values <= np.datetime64(FIN_DISENO), "DISENO", "EXAMEN")
ses_ok = v.groupby("sesion").valido.all()
ses_ok = set(ses_ok[ses_ok].index)

# dias volatiles: tercio de mayor rango de la rueda dentro de cada tramo
R = v[v.rth].groupby("sesion").agg(rango=("ha", "max"), lo=("la", "min"))
R["rango"] = R.rango - R.lo
R["tramo"] = np.where(R.index <= FIN_DISENO, "DISENO", "EXAMEN")
volatil = set()
for tr_, g in R.groupby("tramo"):
    k = len(g) // 3
    volatil |= set(g.sort_values("rango", ascending=False).index[:k])
    p(f"  {tr_}: rango de la rueda mediana {g.rango.median():.2f} pts; volatiles ({k}): "
      + ", ".join(f"{d.date()} {g.rango[d]:.1f}" for d in g.sort_values('rango', ascending=False).index[:k]))


# ------------------------------------------------------------------ movimientos (PREREGISTRO.md seccion 4)
def movimientos(X, VENT):
    res = []
    for se in pd.unique(ses):
        idx = np.nonzero(ses == se)[0]
        tt = tfin[idx]
        hh, ll = h[idx], l[idx]
        for sentido in (1, -1):
            ultimo = -1e18
            for q in range(len(idx)):
                if not val[idx[q]]:
                    continue
                lo = np.searchsorted(tt, tt[q] - VENT, side="left")
                hi = np.searchsorted(tt, tt[q] + VENT, side="right")
                if sentido == 1:
                    if ll[q] > ll[lo:hi].min():
                        continue
                else:
                    if hh[q] < hh[lo:hi].max():
                        continue
                fut = np.arange(q + 1, hi)
                if len(fut) == 0:
                    continue
                alc = np.nonzero(hh[fut] - ll[q] >= X)[0] if sentido == 1 else np.nonzero(hh[q] - ll[fut] >= X)[0]
                if len(alc) == 0:
                    continue
                if tt[q] - ultimo <= VENT:
                    continue
                ultimo = tt[q]
                r = fut[alc[0]]
                res.append(dict(sesion=pd.Timestamp(se), i=int(idx[q]), sentido=sentido,
                                extremo=ll[q] if sentido == 1 else hh[q], i_alcanza=int(idx[r])))
    M = pd.DataFrame(res)
    M["tramo"] = np.where(M.sesion <= FIN_DISENO, "DISENO", "EXAMEN")
    M["volatil"] = M.sesion.isin(volatil)
    return M


M20 = movimientos(20.0, 30)
M10 = movimientos(10.0, 15)
for nom, M in (("20 pts/30 min", M20), ("10 pts/15 min", M10)):
    p(f"movimientos {nom}: " + ", ".join(f"{t_} {len(g)} (volatiles {g.volatil.sum()})" for t_, g in M.groupby("tramo")))


# ------------------------------------------------------------------ detectores
def er_k(k):
    out = np.zeros(len(c))
    for a, b in T._bloques(blq):
        cs = c[a:b]
        if len(cs) <= k:
            continue
        d = np.abs(np.diff(cs))
        vol = np.convolve(d, np.ones(k), mode="valid")
        dirc = np.abs(cs[k:] - cs[:-k])
        with np.errstate(invalid="ignore", divide="ignore"):
            out[a + k:b] = np.where(vol > 0, dirc / vol, 0.0)
    return out


_atr = {}


def atr(n):
    if n not in _atr:
        _atr[n] = T.rma(T.true_range(h, l, c, blq), n, blq)
    return _atr[n]


def supertrend(n, mult, fuente="hl2", confirmar=1):
    """SuperTrend con trinquete de TradingView (= t_lib.det_supertrend con mult escalar, fuente hl2 y confirmar 1).
    mult puede ser un arreglo por vela; fuente 'close' usa el cierre como centro; confirmar = cierres seguidos."""
    A = atr(n)
    mm = np.broadcast_to(np.asarray(mult, float), (len(c),))
    mid = (h + l) / 2.0 if fuente == "hl2" else c
    est = np.zeros(len(c), np.int8)
    for a, b in T._bloques(blq):
        fu = fl = np.nan
        d = None
        cnt = 0
        for j in range(a, b):
            if np.isnan(A[j]):
                continue
            bu = mid[j] + mm[j] * A[j]
            bl = mid[j] - mm[j] * A[j]
            if d is None:
                fu, fl, d = bu, bl, -1
            else:
                cp = c[j - 1]
                nfl = bl if (bl > fl or cp < fl) else fl
                nfu = bu if (bu < fu or cp > fu) else fu
                if d == -1:
                    cnt = cnt + 1 if c[j] > nfu else 0
                    if cnt >= confirmar:
                        d, cnt = 1, 0
                else:
                    cnt = cnt + 1 if c[j] < nfl else 0
                    if cnt >= confirmar:
                        d, cnt = -1, 0
                fu, fl = nfu, nfl
            est[j] = d
    return est


def chandelier(L, mult, n=10):
    A = atr(n)
    est = np.zeros(len(c), np.int8)
    for a, b in T._bloques(blq):
        d = None
        tope = np.nan
        for j in range(a, b):
            if np.isnan(A[j]) or j - a + 1 < L:
                continue
            hh = h[j - L + 1:j + 1].max()
            ll = l[j - L + 1:j + 1].min()
            ls = hh - mult * A[j]
            ss = ll + mult * A[j]
            if d is None:
                d, tope = -1, ss
            elif d == 1:
                tope = max(tope, ls)
                if c[j] < tope:
                    d, tope = -1, ss
            else:
                tope = min(tope, ss)
                if c[j] > tope:
                    d, tope = 1, ls
            est[j] = d
    return est


CANDS = []
for n in (5, 7, 10, 14, 20, 30, 60):
    for m in (1.5, 2.0, 2.5, 3.0, 3.5, 4.0):
        CANDS.append((f"F1 ST({n},{m:g})", lambda n=n, m=m: supertrend(n, m)))
for m in (2.0, 2.5, 3.0, 3.5, 4.0, 4.5):
    CANDS.append((f"F2 STcierre(10,{m:g})", lambda m=m: supertrend(10, m, fuente="close")))
for k in (10, 20):
    for lo_, hi_ in ((1.5, 3.5), (2.0, 4.0), (1.5, 4.5), (2.0, 3.5)):
        for n in (10, 20):
            CANDS.append((f"F3 STer(n{n},k{k},{lo_:g}-{hi_:g})",
                          lambda n=n, k=k, lo_=lo_, hi_=hi_: supertrend(n, lo_ + (hi_ - lo_) * (1 - er_k(k)))))
for L in (10, 22):
    for m in (2.0, 2.5, 3.0, 3.5, 4.0):
        CANDS.append((f"F4 Chand(L{L},{m:g})", lambda L=L, m=m: chandelier(L, m)))
for n in (10, 20):
    for m in (1.5, 2.0, 2.5):
        CANDS.append((f"F5 ST2c({n},{m:g})", lambda n=n, m=m: supertrend(n, m, confirmar=2)))
assert len(CANDS) == 80, len(CANDS)
BASE = "F1 ST(10,3)"
# control: la base reproduce t_lib.det_supertrend
assert (supertrend(10, 3.0) == T.det_supertrend(h, l, c, blq, 10, 3.0)).all()
p("control: supertrend(10,3) de este archivo == t_lib.det_supertrend en todas las velas")


# ------------------------------------------------------------------ medidas
def retraso(st, M):
    pts = np.full(len(M), np.inf)
    mins = np.full(len(M), float(SEG))
    antes = np.zeros(len(M), bool)
    for q, m in enumerate(M.itertuples()):
        i = j = m.i
        while j < len(st) and blq[j] == blq[i] and tfin[j] - tfin[i] <= SEG:
            if st[j] == m.sentido:
                pts[q] = (c[j] - m.extremo) * m.sentido
                mins[q] = tfin[j] - tfin[i]
                antes[q] = j <= m.i_alcanza
                break
            j += 1
    return pts, mins, antes


def ruido(st):
    prev = np.r_[0, st[:-1]]
    prev_ok = np.r_[False, (blq[1:] == blq[:-1]) & val[:-1]]
    cambio = val & prev_ok & (st != prev)
    idx = np.nonzero(cambio)[0]
    fin_blq = {}
    for a, b in T._bloques(blq):
        fin_blq[a] = b
    ini_de = np.zeros(len(st), int)
    for a, b in T._bloques(blq):
        ini_de[a:b] = a
    mfe = np.full(len(idx), np.nan)
    sig = list(idx[1:]) + [len(st)]
    for q, j in enumerate(idx):
        b = fin_blq[ini_de[j]]
        k = min(sig[q], b - 1) if sig[q] < b else b - 1     # vela que vuelve a cambiar (incluida) o fin del bloque
        if k <= j:
            continue
        mfe[q] = (h[j + 1:k + 1].max() - c[j]) if st[j] == 1 else (c[j] - l[j + 1:k + 1].min())
    df = pd.DataFrame(dict(sesion=ses[idx], mfe=mfe))
    df = df[df.sesion.isin(ses_ok)]
    out = {}
    sesiones = pd.Index(sorted(ses_ok))
    for tr_ in ("DISENO", "EXAMEN"):
        ss = sesiones[(sesiones <= FIN_DISENO) if tr_ == "DISENO" else (sesiones > FIN_DISENO)]
        g = df[df.sesion.isin(ss)]
        out[tr_] = dict(cambios=len(g) / len(ss),
                        **{f"falsas{u:g}": (g.mfe < u).sum() / len(ss) for u in (2.5, 5.0, 10.0)})
    return out


def mediana_inf(x):
    x = np.sort(np.asarray(x, float))
    return float(np.median(x)) if len(x) else np.nan


filas, movs = [], []
for nombre, fn in CANDS:
    st = fn()
    rz = ruido(st)
    p20, m20, a20 = retraso(st, M20)
    p10, _, _ = retraso(st, M10)
    for q, m in enumerate(M20.itertuples()):
        movs.append(dict(cand=nombre, mov=q, tramo=m.tramo, volatil=m.volatil, sesion=m.sesion, sentido=m.sentido,
                         pts=p20[q], min=m20[q]))
    for tr_ in ("DISENO", "EXAMEN"):
        s20 = (M20.tramo == tr_).values
        s10 = (M10.tramo == tr_).values
        sv = s20 & M20.volatil.values
        filas.append(dict(cand=nombre, tramo=tr_, n20=int(s20.sum()), tarde20_med=mediana_inf(p20[s20]),
                          min20_med=float(np.median(m20[s20])), antes_umbral_pct=a20[s20].mean() * 100,
                          cens_pct=np.isinf(p20[s20]).mean() * 100,
                          n20_vol=int(sv.sum()), tarde20_vol_med=mediana_inf(p20[sv]),
                          n10=int(s10.sum()), tarde10_med=mediana_inf(p10[s10]), **rz[tr_]))
    print(nombre, "listo", flush=True)

Rz = pd.DataFrame(filas)
Rz.to_csv(LAB / "cal_resumen.csv", index=False)
pd.DataFrame(movs).to_csv(LAB / "cal_movimientos.csv", index=False)

D = Rz[Rz.tramo == "DISENO"].set_index("cand")
E = Rz[Rz.tramo == "EXAMEN"].set_index("cand")
b = D.loc[BASE]
pd.set_option("display.width", 250)
cols = ["tarde20_med", "tarde20_vol_med", "min20_med", "antes_umbral_pct", "cens_pct", "tarde10_med", "cambios",
        "falsas2.5", "falsas5", "falsas10"]
p("\nBASE en DISEÑO:", b[cols].round(2).to_dict())
p("\nTODOS en DISEÑO, ordenados por puntos tarde (movimientos de 20):")
p(D.sort_values(["tarde20_med", "tarde20_vol_med"])[cols].round(2).to_string())

# eleccion
adm = D[(D.cambios <= b.cambios) & (D.falsas5 <= b.falsas5)]
p(f"\nadmisibles sin ruido (cambios <= {b.cambios:.2f} y vueltas falsas 5 pts <= {b.falsas5:.2f} por sesion): {len(adm)}")
p(adm.sort_values(["tarde20_med", "tarde20_vol_med", "falsas5"])[cols].round(2).to_string())
C = adm.sort_values(["tarde20_med", "tarde20_vol_med", "falsas5"]).index[0]
mejora = b.tarde20_med - D.loc[C, "tarde20_med"]
p(f"\nELEGIDO C = {C} (mejora de mediana en DISEÑO {mejora:.2f} pts; pide >= 1)")
for tol in (1.10, 1.25, 1.50):
    f_ = D[(D.cambios <= tol * b.cambios) & (D.falsas5 <= tol * b.falsas5)].sort_values(["tarde20_med", "tarde20_vol_med"])
    p(f"frontera ruido <= {int(round((tol - 1) * 100))} %: {f_.index[0]} -> " + str(f_.iloc[0][cols].round(2).to_dict()))

# examen
Mv = pd.DataFrame(movs)
pb = Mv[(Mv.cand == BASE) & (Mv.tramo == "EXAMEN")].set_index("mov").pts
pc = Mv[(Mv.cand == C) & (Mv.tramo == "EXAMEN")].set_index("mov").pts
d = []
for q in pb.index:
    x, y = pb[q], pc[q]
    if x == y:
        continue
    d.append(1 if x > y else -1)
from scipy.stats import binomtest
gan = sum(1 for z in d if z > 0)
pval = binomtest(gan, len(d), 0.5, alternative="greater").pvalue if d else 1.0
eb, ec = E.loc[BASE], E.loc[C]
p(f"\nEXAMEN ({int(eb.n20)} movimientos de 20, {E.loc[BASE, 'n20_vol']} en dias volatiles):")
p(f"  B {BASE}: " + str(eb[cols].round(2).to_dict()))
p(f"  C {C}: " + str(ec[cols].round(2).to_dict()))
p(f"  prueba de signos: C antes que B en {gan} de {len(d)} movimientos distintos, p = {pval:.4f}")
ok1 = pval <= 0.05 and ec.tarde20_med < eb.tarde20_med
ok2 = ec.cambios <= 1.10 * eb.cambios and ec.falsas5 <= 1.10 * eb.falsas5
p(f"  1 retraso: {'PASA' if ok1 else 'NO PASA'}; 2 ruido (<= 110 %): {'PASA' if ok2 else 'NO PASA'} "
  f"(cambios {ec.cambios:.2f} vs {1.1 * eb.cambios:.2f}; falsas {ec.falsas5:.2f} vs {1.1 * eb.falsas5:.2f})")
p("EXAMEN, todos (descriptivo):")
p(E.sort_values("tarde20_med")[cols].round(2).to_string())
p.close()
