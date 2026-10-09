# -*- coding: utf-8 -*-
"""a01_reproducir.py — AUDITOR. SOLO LECTURA. Reproduce con codigo PROPIO (sin importar el juez) dos numeros clave:
  (1) la 2.0 dibujada (dom0, dom1, ndx_dom0, ndx_dom1 de niv20_m1.pkl), ventana CONGELADA, 19 noches de noches.pkl, TOLERANTE:
      llegadas y % sostenidos (informado: 306 llegadas, 47,5 %; r20 con 09-24: 308, 47,9 %).
  (2) lo mismo corrido +11/-11/+19/-19/+31/-31 (informado: corridas juntas ~45,1-45,2 %).
Implementacion independiente: rayas vigentes en la vela t = niv20[t-1] (arrastre 90 min), fusion <= 1 pt, identidad por valor
(redondeo 0,01: en la ventana congelada la razon de la 2.0 es fija), llegada = la vela toca [L-2, L+2] y la anterior no;
decision TOLERANTE con orden intra-vela o->l->h->c (alcista) / o->h->l->c (bajista).
Correr: python -I a01_reproducir.py"""
import math
import os

import numpy as np
import pandas as pd

CAL = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "calibrar")
CLAVES = ("dom0", "dom1", "ndx_dom0", "ndx_dom1")


def ventanas():
    out = []
    for n in pd.read_pickle(os.path.join(CAL, "noches.pkl")):
        D = pd.Timestamp(n["D"]); N = pd.Timestamp(n["N"])
        a = N - pd.Timedelta(hours=2) if D.weekday() == 4 else N + pd.Timedelta(minutes=35)
        out.append((n["N"], a, N + pd.Timedelta(hours=8, minutes=30)))
    return out


def decidir(o, h, l, c, tm, gap, i, L, s):
    """-> ('sostenido'|'rota'|'indef'|'cens', k_fin)"""
    n = len(o)
    run = 0
    for k in range(i, n):
        if k > i and gap[k]:
            return "cens", k - 1
        if tm[k] > tm[i] + 60:
            return "indef", k - 1
        alc = c[k] >= o[k]
        if s > 0:
            mm, fav, cb = h[k] - L, L - l[k], c[k] - L
            if k == i and alc:
                fav = L - c[k]
        else:
            mm, fav, cb = L - l[k], h[k] - L, L - c[k]
            if k == i and not alc:
                fav = c[k] - L
        if fav >= 15:
            return "sostenido", k
        if cb > 0:
            run += 1
        else:
            run = 0
        if cb > 5 and abs(c[k] - o[k]) >= 4:
            return "rota", k
        if run >= 3:
            return "rota", k
    return "cens", n - 1


def juzgar_propio(V, niv, vent, off=0.0):
    t = V["t"].to_numpy().astype("datetime64[m]").astype(np.int64)
    o, h, l, c = (V[k].to_numpy(float) for k in "ohlc")
    gap = np.ones(len(V), bool); gap[1:] = (t[1:] - t[:-1]) > 30
    km = np.array(sorted(pd.Timestamp(k).value // 60_000_000_000 for k in niv), np.int64)
    kv = {pd.Timestamp(k).value // 60_000_000_000: v for k, v in niv.items()}
    enV = np.zeros(len(V), bool); noche = np.empty(len(V), object)
    for N, a, b in vent:
        m = (V["t"] >= a) & (V["t"] < b)
        enV |= m.to_numpy(); noche[m.to_numpy()] = N
    lin = []
    for i in range(len(V)):
        j = np.searchsorted(km, t[i] - 1, side="right") - 1
        xs = []
        if j >= 0 and t[i] - 1 - km[j] <= 90:
            d = kv[km[j]] or {}
            for k in CLAVES:
                x = d.get(k)
                if x is None or not (x == x) or x == 0:
                    continue
                x = float(x) + off
                if all(abs(x - y) > 1.0 for y in xs):
                    xs.append(x)
        lin.append(xs)
    ocupado = {}
    ev = []
    for i in range(1, len(V)):
        if not enV[i] or gap[i]:
            continue
        for L in lin[i]:
            key = round(L, 2)
            if ocupado.get(key, -1) >= i:
                continue
            toca = h[i] >= L - 2 and l[i] <= L + 2
            prev = h[i - 1] >= L - 2 and l[i - 1] <= L + 2
            if not toca or prev:
                continue
            s = 1 if h[i - 1] < L - 2 else -1
            r, kf = decidir(o, h, l, c, t, gap, i, L, s)
            ocupado[key] = kf
            ev.append((noche[i], i, L, s, r))
    return ev


def resumen(ev):
    base = sum(e[4] != "cens" for e in ev)
    so = sum(e[4] == "sostenido" for e in ev)
    return len(ev), base, so, 100 * so / base if base else float("nan")


def main():
    V = pd.read_csv(os.path.join(CAL, "velas_m1.csv"), parse_dates=["t"])
    V = V.dropna(subset=["o", "h", "l", "c"]).sort_values("t").drop_duplicates("t", keep="last").reset_index(drop=True)
    niv = pd.read_pickle(os.path.join(CAL, "niv20_m1.pkl"))
    vent = ventanas()
    ev = juzgar_propio(V, niv, vent)
    n, b, s, p = resumen(ev)
    print("PROPIO 2.0 congelada 19 noches TOLERANTE: llegadas %d base %d sostenidos %d = %.1f %%" % (n, b, s, p))
    pn = {}
    for e in ev:
        x = pn.setdefault(e[0], [0, 0]); x[0] += e[4] != "cens"; x[1] += e[4] == "sostenido"
    for N in sorted(pn):
        print("   %s base %3d sost %3d (%.0f %%)" % (N, pn[N][0], pn[N][1], 100 * pn[N][1] / pn[N][0] if pn[N][0] else float('nan')))
    for excl in (("2026-10-09",), ("2026-10-09", "2026-10-05")):
        e2 = [e for e in ev if e[0] not in excl]
        n, b, s, p = resumen(e2)
        print("   sin %s: llegadas %d base %d sost %d = %.1f %%" % ("+".join(excl), n, b, s, p))
    tot = []
    for d in (-31, -19, -11, 11, 19, 31):
        e = juzgar_propio(V, niv, vent, d)
        tot += e
        n, b, s, p = resumen(e)
        print("   corrida %+d: llegadas %d sost %.1f %%" % (d, n, p))
    n, b, s, p = resumen(tot)
    print("PROPIO corridas juntas: llegadas %d sost %.1f %%" % (n, p))
    tot2 = [e for e in tot if e[0] != "2026-10-09"]
    print("PROPIO corridas juntas sin 10-09: sost %.1f %%" % resumen(tot2)[3])


if __name__ == "__main__":
    main()
