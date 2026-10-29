# -*- coding: utf-8 -*-
"""POST-HOC (descriptivo, despues de ver cal_01): la frontera retraso/ruido en la RUEDA (9:30-16:00 NY, lo que el operador ve
operando) para la base y los puntos de la frontera, en DISEÑO y EXAMEN; y la comparacion movimiento por movimiento contra la
base en EXAMEN. No elige nada: el pre-registro ya dio su veredicto (cal_01_salida.txt)."""
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
p = T.Salida("cal_02_salida.txt")
p("POST-HOC: frontera en la rueda y examen movimiento por movimiento")
_orig = T.cargar_crudo


def cargar_ext(serie):
    v = _orig("MES_m1")
    b = T._leer_csv(NUEVA)
    b = b[b.t > v.t.max()].copy()
    b["fuente"] = "cache_2026-10-29/MES_m1"
    b["sesion"] = T._sesion(b["t"]).astype("datetime64[ns]")
    b["contrato"], b["seg"], b["tf"] = "MESZ6", 1, 1
    b["t_fin"] = b["t"] + pd.Timedelta(minutes=1)
    return pd.concat([v, b[v.columns]], ignore_index=True).sort_values("t").reset_index(drop=True)


T.cargar_crudo = cargar_ext
v = T.preparar("MES_m1")
v = v[v.sesion <= pd.Timestamp("2026-10-28")].reset_index(drop=True)
h, l, c, blq, val, ses, rth = v.ha.values, v.la.values, v.ca.values, v.bloque.values, v.valido.values, v.sesion.values, v.rth.values
ses_ok = v.groupby("sesion").valido.all()
ses_ok = set(ses_ok[ses_ok].index)
CANDS = {"F1 ST(10,3)": (10, 3.0), "F1 ST(60,3)": (60, 3.0), "F1 ST(10,2.5)": (10, 2.5), "F1 ST(60,2.5)": (60, 2.5),
         "F1 ST(60,2)": (60, 2.0), "F1 ST(10,2)": (10, 2.0)}
M = pd.read_csv(LAB / "cal_movimientos.csv")
for nom, (n, m) in CANDS.items():
    st = T.det_supertrend(h, l, c, blq, n, m)
    prev = np.r_[0, st[:-1]]
    prev_ok = np.r_[False, (blq[1:] == blq[:-1]) & val[:-1]]
    cambio = val & prev_ok & (st != prev)
    idx = np.nonzero(cambio)[0]
    sig = list(idx[1:]) + [len(st)]
    fin = np.r_[np.nonzero(blq[1:] != blq[:-1])[0], len(st) - 1]
    mfe = np.full(len(idx), np.nan)
    for q, j in enumerate(idx):
        bf = fin[np.searchsorted(fin, j)]
        k = min(sig[q], bf)
        if k <= j:
            continue
        mfe[q] = (h[j + 1:k + 1].max() - c[j]) if st[j] == 1 else (c[j] - l[j + 1:k + 1].min())
    df = pd.DataFrame(dict(sesion=ses[idx], rth=rth[idx], mfe=mfe))
    df = df[df.sesion.isin(ses_ok)]
    for tr_ in ("DISENO", "EXAMEN"):
        ss = [s for s in ses_ok if (s <= FIN_DISENO) == (tr_ == "DISENO")]
        g = df[df.sesion.isin(ss) & df.rth]
        mv = M[(M.cand == nom) & (M.tramo == tr_)]
        p(f"{tr_} {nom:<15} rueda: cambios/sesion {len(g) / len(ss):5.2f}, vueltas falsas (<5 pts) {(g.mfe < 5).sum() / len(ss):5.2f}"
          f" | movimientos de 20: mediana tarde {np.median(mv.pts):5.2f} pts, volatiles {np.median(mv[mv.volatil].pts):5.2f}")
p("\nEXAMEN movimiento por movimiento contra la base (puntos tarde base - candidato; >0 = el candidato llega antes):")
b = M[(M.cand == "F1 ST(10,3)") & (M.tramo == "EXAMEN")].set_index("mov")
for nom in CANDS:
    if nom == "F1 ST(10,3)":
        continue
    x = M[(M.cand == nom) & (M.tramo == "EXAMEN")].set_index("mov")
    d = (b.pts - x.pts).replace([np.inf, -np.inf], np.nan)
    antes, despues, igual = int((b.pts > x.pts).sum()), int((b.pts < x.pts).sum()), int((b.pts == x.pts).sum())
    p(f"  {nom:<15} antes en {antes}, despues en {despues}, igual en {igual} de {len(b)}; mediana de la diferencia "
      f"{d.median():+.2f} pts (finitos), en volatiles {d[b.volatil].median():+.2f}")
p.close()
