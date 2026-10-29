# -*- coding: utf-8 -*-
"""Placebo de geometria con velas al azar: misma sesion, mismo r y misma orientacion d que cada fila real,
pero en una vela valida elegida al azar (5 sorteos por fila, semilla 0). Mismas reglas (empate=0, sin resultado=0)."""
import os, sys, ctypes
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import auditoria as A
from auditoria_extra import outcome

def main():
    df = A.load(); block, pos, _, _ = A.make_blocks(df)
    ST, R, starts = A.run_detectors(df, block)
    h = df.h.tolist(); l = df.l.tolist(); c = df.c.tolist(); sess = df.session.tolist(); n = len(h)
    s_hi = {}; s_valid = {}
    for k, s in enumerate(sess):
        s_hi[s] = k
        if pos[k] >= A.WARM:
            s_valid.setdefault(s, []).append(k)
    rng = np.random.default_rng(0)
    rows = []; E = None
    for k in range(n):
        d = int(ST[k])
        if d == 0:
            E = None; continue
        flip = k > 0 and block[k] == block[k-1] and ST[k-1] != 0 and ST[k-1] != d
        first = k == 0 or block[k] != block[k-1] or ST[k-1] == 0
        E = (h[k] if d == 1 else l[k]) if (flip or first) else (max(E, h[k]) if d == 1 else min(E, l[k]))
        if pos[k] < A.WARM or flip:
            continue
        r = d * (E - c[k])
        if not (0 < r <= 12):
            continue
        last = min(k + 120, s_hi[sess[k]])
        y = 1 if outcome(h, l, k, last, E, c[k] - d*10, d) == 1 else 0
        cand = s_valid[sess[k]]
        ys = []
        for kk in rng.choice(cand, 5):
            kk = int(kk); la = min(kk + 120, s_hi[sess[kk]])
            ys.append(1 if outcome(h, l, kk, la, c[kk] + d*r, c[kk] - d*10, d) == 1 else 0)
        rows.append((r, y, np.mean(ys)))
    X = np.array(rows)
    print("cubeta | n | real | placebo vela al azar | real-placebo | RW")
    for a, b in A.BUCKETS:
        m = (X[:,0] > a) & (X[:,0] <= b); mr = X[m,0].mean()
        print("(%g,%g] | %d | %.4f | %.4f | %+.4f | %.4f" % (a, b, m.sum(), X[m,1].mean(), X[m,2].mean(), X[m,1].mean()-X[m,2].mean(), mr/(mr+10)))

if __name__ == "__main__":
    main()
