# -*- coding: utf-8 -*-
"""Pre-registro 4 (PREREGISTRO_SCALPING.md): la Corriente a la escala del scalper. Vara = acierto del tramo actual contra el
zigzag mirado hacia atras (Z = 6 pts primaria; 4, 8, 10 secundarias), en la rueda. Candidatos: R_X (retroceso fijo al
cierre), P(f, m, X) (fraccion del tramo anterior acotada), variantes en vivo (toque), y referencias.
Salidas: scalp_01_salida.txt, scalp_resumen.csv, scalp_sesiones.csv."""
import hashlib
import sys
from pathlib import Path

import numba as nb
import numpy as np
import pandas as pd
from scipy.stats import binomtest

sys.path.insert(0, str(Path(__file__).resolve().parent))
import t_lib as T

T.idle()
LAB = T.LAB
p = T.Salida("scalp_01_salida.txt")
p("SCALPING. SHA-256 PREREGISTRO_SCALPING.md:", hashlib.sha256((LAB / "PREREGISTRO_SCALPING.md").read_bytes()).hexdigest())
_src = open(LAB / "cal_03.py", encoding="utf-8").read().split("# ------------------------------------------------------------------ control:")[0]
_src = _src.replace('p = T.Salida("cal_03_salida.txt")', "p = print")
G = {"__name__": "cal03", "__file__": str(LAB / "cal_03.py")}
exec(compile(_src, "cal_03_parcial", "exec"), G)
S = G["Serie"](G["serie_mes"](), 20.0, 30, 10.0, 15, 5.0)
corriente = G["corriente"]
h, l, c = S.h.astype(float), S.l.astype(float), S.c.astype(float)
blq_ini = S.ini.astype(np.int64)
st0 = T.det_supertrend(h, l, c, S.blq, 10, 3.0).astype(np.int8)


@nb.njit(cache=True)
def zigzag(h, l, blq_ini, Z):
    """Tramo verdadero mirando hacia atras: +1/-1 por vela; 0 = sin tramo conocido (antes del primer pivote o despues del
    ultimo de cada bloque). Un pivote se confirma cuando el precio se aleja Z del extremo; las velas despues del pivote y
    hasta el siguiente pivote (incluido) son del tramo que empieza en ese pivote."""
    n = len(h)
    lab = np.zeros(n, np.int8)
    piv = np.zeros(n, np.int8)        # +1 maximo pivote, -1 minimo pivote
    t = 0
    while t < n:
        a = blq_ini[t]
        b = t
        while b < n and blq_ini[b] == a:
            b += 1
        # bloque [a, b)
        d = 0
        H, iH, L, iL = h[a], a, l[a], a
        last = -1
        for j in range(a, b):
            if d == 0:
                if h[j] > H:
                    H, iH = h[j], j
                if l[j] < L:
                    L, iL = l[j], j
                if h[j] - L >= Z and iL < j:
                    d, last = 1, iL
                    piv[iL] = -1
                    H, iH = h[j], j
                    for q in range(iL + 1, j + 1):
                        if h[q] >= H:
                            H, iH = h[q], q
                elif H - l[j] >= Z and iH < j:
                    d, last = -1, iH
                    piv[iH] = 1
                    L, iL = l[j], j
                    for q in range(iH + 1, j + 1):
                        if l[q] <= L:
                            L, iL = l[q], q
            elif d == 1:
                if h[j] > H:
                    H, iH = h[j], j
                elif H - l[j] >= Z:
                    for q in range(last + 1, iH + 1):
                        lab[q] = 1
                    piv[iH] = 1
                    last, d = iH, -1
                    L, iL = l[iH + 1], iH + 1
                    for q in range(iH + 1, j + 1):
                        if l[q] <= L:
                            L, iL = l[q], q
            else:
                if l[j] < L:
                    L, iL = l[j], j
                elif h[j] - L >= Z:
                    for q in range(last + 1, iL + 1):
                        lab[q] = -1
                    piv[iL] = -1
                    last, d = iL, 1
                    H, iH = h[iL + 1], iL + 1
                    for q in range(iL + 1, j + 1):
                        if h[q] >= H:
                            H, iH = h[q], q
        t = b
    return lab, piv


@nb.njit(cache=True)
def detector(modo, f, m, X, h, l, c, blq_ini, st0):
    """modo 0 = R_X al cierre; 1 = P(f, m, X) al cierre; 2 = R_X en vivo (toque); 3 = P(f, m, X) en vivo.
    E = extremo del tramo; S = extremo donde empezo el tramo (el del tramo anterior)."""
    n = len(c)
    st = np.zeros(n, np.int8)
    d = 0
    E = S = 0.0
    for t in range(n):
        if t == blq_ini[t] or st0[t] == 0:
            d = 0
            st[t] = st0[t]
            continue
        if d == 0:
            d = st0[t]
            E = h[t] if d == 1 else l[t]
            S = E
            st[t] = d
            continue
        if modo >= 2:
            thr = X if modo == 2 else min(X, max(m, f * abs(E - S)))
            if d == 1 and l[t] <= E - thr:
                S = max(E, h[t])
                d, E = -1, l[t]
            elif d == -1 and h[t] >= E + thr:
                S = min(E, l[t])
                d, E = 1, h[t]
            else:
                if d == 1 and h[t] > E:
                    E = h[t]
                elif d == -1 and l[t] < E:
                    E = l[t]
        else:
            if d == 1 and h[t] > E:
                E = h[t]
            elif d == -1 and l[t] < E:
                E = l[t]
            thr = X if modo == 0 else min(X, max(m, f * abs(E - S)))
            if d * (E - c[t]) >= thr:
                S = E
                d = -d
                E = h[t] if d == 1 else l[t]
        st[t] = d
    return st


ZS = (4.0, 6.0, 8.0, 10.0)
ZZ = {Z: zigzag(h, l, blq_ini, Z) for Z in ZS}
ses = S.ses
rth = S.rth & S.val
tramo = S.tramo
sesiones = [s for s in S.ses_ok]
p(f"MES 1 min: {len(c)} velas, sesiones completas {len(sesiones)} (DISEÑO {sum(1 for s in sesiones if s <= np.datetime64('2026-10-05'))})")
for Z in ZS:
    lab, piv = ZZ[Z]
    sel = rth & (lab != 0)
    p(f"  zigzag Z {Z:g}: velas de la rueda con tramo conocido {sel.sum()}, pivotes en la rueda {int((piv != 0)[rth].sum())}")


def medir(nombre, st):
    filas = []
    for Z in ZS:
        lab, piv = ZZ[Z]
        ok = rth & (lab != 0)
        df = pd.DataFrame(dict(s=ses[ok], a=(st[ok] == lab[ok])))
        acc = df.groupby("s").a.mean()
        for s_, v_ in acc.items():
            if s_ in set(sesiones):
                filas.append(dict(regla=nombre, Z=Z, sesion=s_, acierto=v_))
    return filas


def ruido_y_pivotes(st):
    prev = np.r_[0, st[:-1]]
    prev_ok = np.r_[False, (S.blq[1:] == S.blq[:-1]) & S.val[:-1]]
    idx = np.nonzero(S.val & prev_ok & (st != prev))[0]
    sig = np.r_[idx[1:], len(st)]
    mfe = np.full(len(idx), np.nan)
    for q, j in enumerate(idx):
        k = min(sig[q], S.fin[j] - 1)
        if k > j:
            mfe[q] = (h[j + 1:k + 1].max() - c[j]) if st[j] == 1 else (c[j] - l[j + 1:k + 1].min())
    df = pd.DataFrame(dict(s=ses[idx], r=S.rth[idx], f=mfe < 2.5))
    df = df[df.s.isin(sesiones) & df.r]
    out = {}
    for tr_ in ("DISENO", "EXAMEN"):
        ss = [s for s in sesiones if (s <= np.datetime64("2026-10-05")) == (tr_ == "DISENO")]
        g = df[df.s.isin(ss)]
        out[tr_] = (len(g) / len(ss), g.f.sum() / len(ss))
    # puntos tarde en los pivotes de la rueda (Z = 6)
    lab, piv = ZZ[6.0]
    tarde = {"DISENO": [], "EXAMEN": []}
    perdidos = {"DISENO": 0, "EXAMEN": 0}
    pv = np.nonzero(piv != 0)[0]
    for k_, p0 in enumerate(pv[:-1]):
        if not (rth[p0] and ses[p0] in set(sesiones)):
            continue
        sdir = -int(piv[p0])               # minimo pivote -> tramo para arriba
        ext = l[p0] if sdir == 1 else h[p0]
        q = pv[k_ + 1]
        tr_ = tramo[p0]
        enc = -1
        for j in range(p0, q + 1):
            if st[j] == sdir:
                enc = j
                break
        if enc < 0:
            perdidos[tr_] += 1
        else:
            tarde[tr_].append(sdir * (c[enc] - ext))
    return out, tarde, perdidos


reglas = {}
reglas["ST(10,3) 1.0c"] = st0
reglas["1.1"] = corriente(S, S.atr(60), 4.0, v=2.5, j=3)
for X in (2, 3, 4, 5, 6, 7, 8, 10):
    reglas[f"R{X}"] = detector(0, 0.0, 0.0, float(X), h, l, c, blq_ini, st0)
for f in (0.382, 0.5, 0.618):
    for m in (2, 3, 4, 5):
        for X in (8, 10, 12):
            reglas[f"P({f:g},{m},{X})"] = detector(1, f, float(m), float(X), h, l, c, blq_ini, st0)
for X in (3, 4, 5, 6):
    reglas[f"R{X} vivo"] = detector(2, 0.0, 0.0, float(X), h, l, c, blq_ini, st0)
for m in (2, 3, 4, 5):
    reglas[f"P(0.5,{m},10) vivo"] = detector(3, 0.5, float(m), 10.0, h, l, c, blq_ini, st0)

F, resumen = [], []
for nom, st in reglas.items():
    F += medir(nom, st)
    rz, tarde, perd = ruido_y_pivotes(st)
    for tr_ in ("DISENO", "EXAMEN"):
        resumen.append(dict(regla=nom, tramo=tr_, cambios_rueda=rz[tr_][0], falsas_rueda=rz[tr_][1],
                            tarde_med=float(np.median(tarde[tr_])) if tarde[tr_] else np.nan,
                            tarde_media=float(np.mean(tarde[tr_])) if tarde[tr_] else np.nan,
                            no_vio=perd[tr_], pivotes=perd[tr_] + len(tarde[tr_])))
F = pd.DataFrame(F)
F["tramo"] = np.where(F.sesion <= pd.Timestamp("2026-10-05"), "DISENO", "EXAMEN")
F.to_csv(LAB / "scalp_sesiones.csv", index=False)
A = F.groupby(["regla", "tramo", "Z"]).acierto.mean().unstack("Z")
A.columns = [f"acierto_Z{int(z)}" for z in A.columns]
Rz = pd.DataFrame(resumen).set_index(["regla", "tramo"]).join(A)
Rz.to_csv(LAB / "scalp_resumen.csv")
pd.set_option("display.width", 250)
pd.set_option("display.max_rows", 200)
for tr_ in ("DISENO", "EXAMEN"):
    t_ = Rz.xs(tr_, level="tramo").sort_values("acierto_Z6", ascending=False)
    t_ = t_[["acierto_Z6", "acierto_Z4", "acierto_Z8", "acierto_Z10", "tarde_med", "tarde_media", "no_vio", "pivotes", "cambios_rueda", "falsas_rueda"]]
    t_[[c_ for c_ in t_.columns if c_.startswith("acierto")]] *= 100
    p(f"\n==== {tr_} (acierto en % de velas de la rueda; tarde = pts desde el pivote, Z = 6)")
    p(t_.round(2).to_string())

D = Rz.xs("DISENO", level="tramo")
elegido = D.acierto_Z6.idxmax()
p(f"\nELEGIDO en DISEÑO (mayor acierto Z = 6): {elegido} ({D.acierto_Z6[elegido] * 100:.2f} %)")
E_ = F[F.tramo == "EXAMEN"]
ok_total = True
for ref in ("R10", "ST(10,3) 1.0c"):
    a = E_[(E_.regla == elegido) & (E_.Z == 6.0)].set_index("sesion").acierto
    b = E_[(E_.regla == ref) & (E_.Z == 6.0)].set_index("sesion").acierto
    gan, per = int((a > b).sum()), int((a < b).sum())
    pv = binomtest(gan, gan + per, 0.5, alternative="greater").pvalue
    medias = {Z: (E_[(E_.regla == elegido) & (E_.Z == Z)].acierto.mean(), E_[(E_.regla == ref) & (E_.Z == Z)].acierto.mean()) for Z in ZS}
    ok = pv <= 0.05 and medias[4.0][0] > medias[4.0][1] and medias[8.0][0] > medias[8.0][1]
    ok_total &= ok
    p(f"  EXAMEN {elegido} contra {ref}: gana en {gan} sesiones, pierde en {per}, p {pv:.4f}; medias " +
      ", ".join(f"Z{int(Z)} {medias[Z][0] * 100:.1f} vs {medias[Z][1] * 100:.1f}" for Z in ZS) + f" -> {'PASA' if ok else 'NO PASA'}")
p(f"\nVEREDICTO: {'PASA' if ok_total else 'NO PASA'}")
p.close()
