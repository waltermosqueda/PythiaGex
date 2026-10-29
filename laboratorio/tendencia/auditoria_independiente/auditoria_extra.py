# -*- coding: utf-8 -*-
"""
Auditoria independiente, parte 2: por que la geometria (C) queda debajo de r/(r+10).
  1) desglose de resultados por cubeta: objetivo primero / E primero / empate / sin resultado
  2) P(y=1 | decidido sin empate) contra r/(r+10)
  3) placebo con las MISMAS reglas en las MISMAS velas: barrera virtual a distancia r,
     orientacion +1 y -1 promediadas (cancela la deriva), objetivo a 10
  4) auto-chequeo: ST escrito como el Pine literal (compara superTrend[1] con upperBand[1])
     y R10 escrito con indices de tramo, contra auditoria.py
Solo usa auditoria.py (codigo propio de esta carpeta).
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import auditoria as A  # noqa: E402

LINES = []


def out(*a):
    s = " ".join(str(x) for x in a)
    print(s)
    LINES.append(s)


def outcome(h, l, k, last, E, tgt, d):
    """1 objetivo primero, 0 E primero, 2 empate en la misma vela, 3 sin resultado."""
    for j in range(k + 1, last + 1):
        if d == 1:
            hitE = h[j] >= E; hitT = l[j] <= tgt
        else:
            hitE = l[j] <= E; hitT = h[j] >= tgt
        if hitE and hitT:
            return 2
        if hitT:
            return 1
        if hitE:
            return 0
    return 3


def pine_supertrend_block(h, l, c, period=10, factor=3.0):
    """Traduccion literal del Pine v5 de ta.supertrend (direction: 1 = baja, -1 = sube)."""
    n = len(h)
    tr = [h[0] - l[0]] + [max(h[k] - l[k], abs(h[k] - c[k - 1]), abs(l[k] - c[k - 1])) for k in range(1, n)]
    atr = [None] * n
    for k in range(n):
        if k == period - 1:
            atr[k] = sum(tr[:period]) / period
        elif k >= period:
            atr[k] = (1.0 / period) * tr[k] + (1 - 1.0 / period) * atr[k - 1]
    up = [None] * n; lo = [None] * n; sup = [None] * n; dr = [None] * n
    for k in range(n):
        if atr[k] is None:
            continue
        src = (h[k] + l[k]) / 2
        ub = src + factor * atr[k]; lb = src - factor * atr[k]
        plb = lo[k - 1] if (k > 0 and lo[k - 1] is not None) else 0.0
        pub = up[k - 1] if (k > 0 and up[k - 1] is not None) else 0.0
        lb = lb if (lb > plb or c[k - 1] < plb) else plb
        ub = ub if (ub < pub or c[k - 1] > pub) else pub
        lo[k] = lb; up[k] = ub
        if k == 0 or atr[k - 1] is None:
            dr[k] = 1
        elif sup[k - 1] == pub:
            dr[k] = -1 if c[k] > ub else 1
        else:
            dr[k] = 1 if c[k] < lb else -1
        sup[k] = lb if dr[k] == -1 else ub
    return [0 if x is None else (-x) for x in dr]   # a +1 sube / -1 baja


def r10_by_legs(h, l, c, st, thr=10.0):
    n = len(h)
    s = [0] * n
    k0 = next((k for k in range(n) if st[k] != 0), None)
    if k0 is None:
        return s
    d = st[k0]; leg0 = k0
    for k in range(k0, n):
        if k > k0:
            E = max(h[leg0:k + 1]) if d == 1 else min(l[leg0:k + 1])
            if d * (E - c[k]) >= thr:
                d = -d; leg0 = k
        s[k] = d
    return s


def main():
    df = A.load()
    block, pos, reasons, uniq = A.make_blocks(df)
    ST, R, starts = A.run_detectors(df, block)
    h = df["h"].tolist(); l = df["l"].tolist(); c = df["c"].tolist()
    n = len(h)

    # ---- 4) auto-chequeo de detectores
    st2 = [0] * n; r2 = [0] * n
    for a, z in zip(starts[:-1], starts[1:]):
        st2[a:z] = pine_supertrend_block(h[a:z], l[a:z], c[a:z])
        r2[a:z] = r10_by_legs(h[a:z], l[a:z], c[a:z], st2[a:z])
    out("auto-chequeo: ST Pine literal == ST auditoria:", bool(np.array_equal(np.array(st2), ST)),
        "| R10 por tramos == R10 auditoria:", bool(np.array_equal(np.array(r2), R)))
    valid = pos >= A.WARM
    out("velas validas:", int(valid.sum()), "| sin estado entre validas: ST", int((ST[valid] == 0).sum()),
        "R10", int((R[valid] == 0).sum()))

    # ---- 1-3) geometria con desglose y placebo
    sess = df["session"].tolist()
    s_hi = {}
    for k, s in enumerate(sess):
        s_hi[s] = k
    rows = []   # r, codigo real, codigo placebo +1, codigo placebo -1, sesion
    E = None
    for k in range(n):
        d = int(ST[k])
        if d == 0:
            E = None; continue
        flip = k > 0 and block[k] == block[k - 1] and ST[k - 1] != 0 and ST[k - 1] != d
        first = k == 0 or block[k] != block[k - 1] or ST[k - 1] == 0
        if flip or first:
            E = h[k] if d == 1 else l[k]
        else:
            E = max(E, h[k]) if d == 1 else min(E, l[k])
        if pos[k] < A.WARM or flip:
            continue
        r = d * (E - c[k])
        if not (0 < r <= 12):
            continue
        last = min(k + 120, s_hi[sess[k]])
        real = outcome(h, l, k, last, E, c[k] - d * 10, d)
        pls = []
        for s in (1, -1):   # barrera virtual a r del cierre, objetivo a 10 del otro lado
            pls.append(outcome(h, l, k, last, c[k] + s * r, c[k] - s * 10, s))
        rows.append((r, real, pls[0], pls[1], d))
    R_ = np.array(rows, float)
    out("\nC desglose (todas las sesiones):")
    out("cubeta | n | P(y=1) | RW | E primero | empate | sin resultado | P(y=1|decidido sin empate) | placebo mismas reglas (+-1 promedio) | real - placebo")
    for a, b in A.BUCKETS:
        m = (R_[:, 0] > a) & (R_[:, 0] <= b)
        x = R_[m]
        nn = len(x)
        mr = x[:, 0].mean(); rw = mr / (mr + 10)
        p1 = (x[:, 1] == 1).mean(); p0 = (x[:, 1] == 0).mean(); pt = (x[:, 1] == 2).mean(); pc = (x[:, 1] == 3).mean()
        dec = p1 / (p1 + p0) if (p1 + p0) > 0 else float("nan")
        plc = ((x[:, 2] == 1).mean() + (x[:, 3] == 1).mean()) / 2
        out("(%g,%g] | %d | %.4f | %.4f | %.4f | %.4f | %.4f | %.4f | %.4f | %+.4f" % (
            a, b, nn, p1, rw, p0, pt, pc, dec, plc, p1 - plc))
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "salida_auditoria_extra.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(LINES) + "\n")


if __name__ == "__main__":
    main()
