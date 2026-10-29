# -*- coding: utf-8 -*-
"""
Auditoria independiente 3, parte 2: chequeo de propiedades del zigzag primario (V3/first) y lineas de base
para la pregunta del sesgo (Z=6): color de vela, cierre contra cierre, giros al azar con la tasa de R4 live,
y la propia etiqueta retrospectiva atrasada j velas (cuanto cuesta solo el atraso).
Solo usa codigo propio de esta carpeta (auditoria.py, auditoria_zigzag.py).
"""
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import auditoria as A  # noqa: E402  (baja la prioridad a IDLE)
import auditoria_zigzag as G  # noqa: E402

LINES = []


def out(*a):
    s = " ".join(str(x) for x in a)
    print(s, flush=True)
    LINES.append(s)


def main():
    df = A.load()
    block, pos, _, uniq = A.make_blocks(df)
    n = len(df)
    h = df["h"].tolist(); l = df["l"].tolist(); c = df["c"].tolist(); o = df["o"].tolist()
    sess = df["session"].to_numpy(); rth = df["rth"].to_numpy(); valid = pos >= A.WARM
    starts = [0] + [k for k in range(1, n) if block[k] != block[k - 1]] + [n]
    bad = set(sess[~valid].tolist())
    scored = [s for s in uniq if s not in bad]
    des = [s for s in scored if s <= A.DESIGN_END]
    exa = [s for s in scored if A.EXAM_START <= s <= A.LAST_SESSION]
    sidx = {}
    for k in range(n):
        sidx.setdefault(sess[k], []).append(k)
    sidx = {s: np.array(v) for s, v in sidx.items()}

    # ---------------- propiedades del zigzag primario
    for Z in (4, 6, 8, 10):
        viol_ext = viol_first = viol_size = viol_lab = viol_start = 0; npairs = 0
        for a, z in zip(starts[:-1], starts[1:]):
            hb = h[a:z]; lb = l[a:z]
            lab, live, piv, _ = G.zigzag_block(hb, lb, float(Z), "V3", "first")
            for (p, t), (q, _) in zip(piv[:-1], piv[1:]):
                npairs += 1
                seg_h = hb[p + 1:q + 1]; seg_l = lb[p + 1:q + 1]
                if t == 1:
                    m = max(seg_h)
                    viol_ext += hb[q] != m
                    viol_first += (p + 1 + seg_h.index(m)) != q
                    viol_size += (hb[q] - lb[p]) < Z
                    viol_start += min(lb[p:q + 1]) < lb[p]
                else:
                    m = min(seg_l)
                    viol_ext += lb[q] != m
                    viol_first += (p + 1 + seg_l.index(m)) != q
                    viol_size += (hb[p] - lb[q]) < Z
                    viol_start += max(hb[p:q + 1]) > hb[p]
                viol_lab += int(np.any(lab[p + 1:q + 1] != t))
        out("Z=%d pares de pivotes %d | violaciones: extremo del fin %d, primera vela del extremo %d, tamano<Z %d, "
            "etiqueta %d | swing que pasa por debajo/encima de su pivote de inicio (velas enormes) %d"
            % (Z, npairs, viol_ext, viol_first, viol_size, viol_lab, viol_start))

    # RTH por sesion (para entender velas etiquetadas)
    short = [(s.isoformat(), int(rth[sidx[s]].sum())) for s in scored if int(rth[sidx[s]].sum()) != 390]
    out("sesiones puntuables con RTH != 390 velas:", short)

    # ---------------- lineas de base a Z=6
    lab6 = np.zeros(n, np.int8)
    ST = np.zeros(n, np.int8); R4 = np.zeros(n, np.int8)
    for a, z in zip(starts[:-1], starts[1:]):
        lb, _, _, _ = G.zigzag_block(h[a:z], l[a:z], 6.0, "V3", "first")
        lab6[a:z] = lb
        st = A.supertrend_block(h[a:z], l[a:z], c[a:z])
        ST[a:z] = st
        R4[a:z] = G.touch_block(h[a:z], l[a:z], st, 4.0)

    def mean_acc(lab, state, ss):
        v = []
        for s in ss:
            ix = sidx[s]
            m = rth[ix] & valid[ix] & (lab[ix] != 0)
            v.append(float(np.mean(state[ix][m] == lab[ix][m])))
        return 100.0 * float(np.mean(v))

    color = np.zeros(n, np.int8); c2c = np.zeros(n, np.int8)
    for a, z in zip(starts[:-1], starts[1:]):
        dc = 1; dd = 1
        for k in range(a, z):
            if c[k] > o[k]:
                dc = 1
            elif c[k] < o[k]:
                dc = -1
            color[k] = dc
            if k > a:
                if c[k] > c[k - 1]:
                    dd = 1
                elif c[k] < c[k - 1]:
                    dd = -1
            c2c[k] = dd
    out("\nZ=6 (V3/first), exactitud media por sesion %:")
    for pname, ss in (("DESIGN", des), ("EXAM", exa)):
        out("  %s R4 live %.2f | color de vela %.2f | cierre vs cierre anterior %.2f" % (
            pname, mean_acc(lab6, R4, ss), mean_acc(lab6, color, ss), mean_acc(lab6, c2c, ss)))
        # giros al azar con la tasa de giro de R4 live en RTH
        rate = float(np.mean((R4[1:] != R4[:-1])[rth[1:] & valid[1:]]))
        accs = []
        for seed in range(20):
            rng = np.random.default_rng(seed)
            flips = rng.random(n) < rate
            st = np.where(np.cumsum(flips) % 2 == 0, 1, -1).astype(np.int8)
            accs.append(mean_acc(lab6, st, ss))
        out("  %s giros al azar con la tasa de R4 live (%.3f por vela), 20 semillas: media %.2f, rango %.2f..%.2f" % (
            pname, rate, float(np.mean(accs)), min(accs), max(accs)))
        lagrow = []
        for j in (1, 2, 3, 4, 5, 8):
            lagged = np.zeros(n, np.int8)
            lagged[j:] = lab6[:-j]
            same_blk = np.zeros(n, bool); same_blk[j:] = block[j:] == block[:-j]
            lagged[~same_blk] = 0
            lagrow.append("atraso %d: %.2f" % (j, mean_acc(lab6, lagged, ss)))
        out("  %s etiqueta retrospectiva atrasada j velas (oraculo con atraso): %s" % (pname, " | ".join(lagrow)))

    with open(os.path.join(HERE, "salida_zigzag_extra.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(LINES) + "\n")


if __name__ == "__main__":
    main()
