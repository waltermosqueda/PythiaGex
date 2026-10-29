# -*- coding: utf-8 -*-
"""
Auditoria independiente (29-10): ST(10,3) vs R10 sobre MES 1 minuto.
Codigo propio, escrito desde la especificacion; NO importa nada del laboratorio.

Uso:  python auditoria.py            -> imprime todo y lo copia a salida_auditoria.txt
"""
import ctypes
import math
import sys
from datetime import date, timedelta

import numpy as np
import pandas as pd

# ---------------------------------------------------------------- prioridad baja
try:
    _k32 = ctypes.windll.kernel32
    _k32.SetPriorityClass(_k32.GetCurrentProcess(), 0x00000040)  # IDLE_PRIORITY_CLASS
except Exception as _e:  # pragma: no cover
    print("no pude bajar la prioridad:", _e)

F1 = r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\dom\ronda3\velas\velas-MES-m1.csv"
F2 = r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\tres\datos\velas_cache_2026-10-29\velas-MES-m1.csv"
LAST_SESSION = date(2026, 10, 28)
DESIGN_END = date(2026, 10, 5)
EXAM_START = date(2026, 10, 6)
ROLL_PAIR = (date(2026, 9, 11), date(2026, 9, 14))
WARM = 100

OUT_LINES = []


def out(*a):
    s = " ".join(str(x) for x in a)
    print(s)
    OUT_LINES.append(s)


# ---------------------------------------------------------------- datos
def load():
    cols = ["t", "o", "h", "l", "c", "vol", "delta"]
    a = pd.read_csv(F1, usecols=cols)
    b = pd.read_csv(F2, usecols=cols)
    a["t"] = pd.to_datetime(a["t"])
    b["t"] = pd.to_datetime(b["t"])
    last_a = a["t"].iloc[-1]
    b = b[b["t"] > last_a]
    df = pd.concat([a, b], ignore_index=True)
    df = df.sort_values("t", kind="mergesort").drop_duplicates("t", keep="first").reset_index(drop=True)
    df["session"] = (df["t"] + pd.Timedelta(hours=2)).dt.date
    df = df[df["session"] <= LAST_SESSION].reset_index(drop=True)
    ny = df["t"].dt.tz_localize("UTC").dt.tz_convert("America/New_York")
    mins = ny.dt.hour * 60 + ny.dt.minute
    df["rth"] = (mins >= 9 * 60 + 30) & (mins <= 15 * 60 + 59) & (ny.dt.date == df["session"])
    df["tm"] = (df["t"].values.astype("datetime64[m]").astype(np.int64))  # minutos desde epoch
    return df


def make_blocks(df):
    sess = df["session"].tolist()
    tm = df["tm"].tolist()
    n = len(df)
    uniq = sorted(set(sess))
    restart = {}
    for p, q in zip(uniq[:-1], uniq[1:]):
        d = p + timedelta(days=1)
        missing = []
        while d < q:
            if d.weekday() < 5:
                missing.append(d)
            d += timedelta(days=1)
        if missing:
            restart[q] = "falta dia habil " + ",".join(x.isoformat() for x in missing)
        if (p, q) == ROLL_PAIR:
            restart[q] = (restart.get(q, "") + " roll").strip()
    new = [False] * n
    new[0] = True
    reasons = [(0, "inicio")]
    for k in range(1, n):
        if sess[k] != sess[k - 1]:
            if sess[k] in restart:
                new[k] = True
                reasons.append((k, restart[sess[k]]))
        elif tm[k] - tm[k - 1] > 61:
            new[k] = True
            reasons.append((k, "hueco %d min dentro de la sesion" % (tm[k] - tm[k - 1])))
    block = np.cumsum(np.array(new, dtype=np.int64)) - 1
    pos = np.zeros(n, dtype=np.int64)
    for k in range(1, n):
        pos[k] = 0 if new[k] else pos[k - 1] + 1
    return block, pos, reasons, uniq


# ---------------------------------------------------------------- detectores
def supertrend_block(h, l, c, period=10, mult=3.0):
    n = len(h)
    st = [0] * n
    if n < period:
        return st
    tr = [0.0] * n
    tr[0] = h[0] - l[0]
    for k in range(1, n):
        pc = c[k - 1]
        tr[k] = max(h[k] - l[k], abs(h[k] - pc), abs(l[k] - pc))
    atr = sum(tr[:period]) / period
    fu = fl = None
    for k in range(period - 1, n):
        if k >= period:
            atr = (atr * (period - 1) + tr[k]) / period
        mid = (h[k] + l[k]) / 2.0
        bu = mid + mult * atr
        bl = mid - mult * atr
        if k == period - 1:
            fu, fl = bu, bl
            st[k] = -1
            continue
        pc = c[k - 1]
        nfl = bl if (bl > fl or pc < fl) else fl
        nfu = bu if (bu < fu or pc > fu) else fu
        fl, fu = nfl, nfu
        if st[k - 1] == -1:
            st[k] = 1 if c[k] > fu else -1
        else:
            st[k] = -1 if c[k] < fl else 1
    return st


def r10_block(h, l, c, st, thr=10.0):
    n = len(h)
    s = [0] * n
    k0 = next((k for k in range(n) if st[k] != 0), None)
    if k0 is None:
        return s
    d = st[k0]
    E = h[k0] if d == 1 else l[k0]
    s[k0] = d
    for k in range(k0 + 1, n):
        if d == 1:
            if h[k] > E:
                E = h[k]
        else:
            if l[k] < E:
                E = l[k]
        if d * (E - c[k]) >= thr:
            d = -d
            E = l[k] if d == -1 else h[k]
        s[k] = d
    return s


def run_detectors(df, block):
    h = df["h"].tolist(); l = df["l"].tolist(); c = df["c"].tolist()
    n = len(h)
    ST = [0] * n
    R = [0] * n
    b = block.tolist()
    starts = [0] + [k for k in range(1, n) if b[k] != b[k - 1]] + [n]
    for a, z in zip(starts[:-1], starts[1:]):
        st = supertrend_block(h[a:z], l[a:z], c[a:z])
        r = r10_block(h[a:z], l[a:z], c[a:z], st)
        ST[a:z] = st
        R[a:z] = r
    return np.array(ST), np.array(R), starts


# ---------------------------------------------------------------- A) movimientos grandes
def find_moves(df, pos, strict=False, dedupe_vs_last_kept=True):
    tm = df["tm"].to_numpy(); h = df["h"].to_numpy(); l = df["l"].to_numpy()
    sess = df["session"].to_numpy()
    n = len(df)
    # limites de sesion
    sess_list = df["session"].tolist()
    s_lo = {}; s_hi = {}
    for k, s in enumerate(sess_list):
        if s not in s_lo:
            s_lo[s] = k
        s_hi[s] = k + 1
    cand = {1: [], -1: []}
    for i in range(n):
        if pos[i] < WARM:
            continue
        lo_s, hi_s = s_lo[sess_list[i]], s_hi[sess_list[i]]
        seg = tm[lo_s:hi_s]
        lo = lo_s + int(np.searchsorted(seg, tm[i] - 30, "left"))
        hi = lo_s + int(np.searchsorted(seg, tm[i] + 30, "right"))
        wl = l[lo:hi]; wh = h[lo:hi]
        if strict:
            is_low = l[i] == wl.min() and (wl == l[i]).sum() == 1
            is_high = h[i] == wh.max() and (wh == h[i]).sum() == 1
        else:
            is_low = l[i] <= wl.min()
            is_high = h[i] >= wh.max()
        if i + 1 < hi:
            if is_low and h[i + 1:hi].max() >= l[i] + 20:
                cand[1].append(i)
            if is_high and l[i + 1:hi].min() <= h[i] - 20:
                cand[-1].append(i)
    moves = []
    for d in (1, -1):
        last = None
        for i in cand[d]:
            if last is not None and tm[i] - tm[last] <= 30:
                if not dedupe_vs_last_kept:
                    last = i
                continue
            moves.append((i, d))
            last = i
    moves.sort()
    return moves


def lateness(df, block, moves, state):
    tm = df["tm"].to_numpy(); h = df["h"].to_numpy(); l = df["l"].to_numpy(); c = df["c"].to_numpy()
    n = len(df)
    late_med = []; late_mean = []; found = []
    for i, d in moves:
        ext = l[i] if d == 1 else h[i]
        j = i
        res = None
        while j < n and block[j] == block[i] and tm[j] - tm[i] <= 120:
            if state[j] == d:
                res = (c[j] - ext) * d
                break
            j += 1
        if res is not None:
            late_med.append(res); late_mean.append(res); found.append(True)
        else:
            j = i + 1
            run = None
            while j < n and block[j] == block[i] and tm[j] - tm[i] <= 120:
                v = (h[j] - ext) if d == 1 else (ext - l[j])
                run = v if run is None else max(run, v)
                j += 1
            late_med.append(math.inf); late_mean.append(run); found.append(False)
    return np.array(late_med, float), np.array(late_mean, float), np.array(found)


def median_inf(x):
    xs = np.sort(x)
    m = len(xs)
    if m == 0:
        return float("nan")
    if m % 2 == 1:
        return float(xs[m // 2])
    a, b = xs[m // 2 - 1], xs[m // 2]
    if math.isinf(a) or math.isinf(b):
        return math.inf
    return float((a + b) / 2)


# ---------------------------------------------------------------- B) ruido en RTH
def noise(df, block, pos, state):
    h = df["h"].to_numpy(); l = df["l"].to_numpy(); c = df["c"].to_numpy()
    rth = df["rth"].to_numpy(); sess = df["session"].tolist()
    n = len(df)
    # fin de bloque
    blk_end = {}
    for k in range(n):
        blk_end[block[k]] = k
    chg = np.zeros(n, bool)
    for k in range(1, n):
        if block[k] == block[k - 1] and state[k] != 0 and state[k - 1] != 0 and state[k] != state[k - 1]:
            chg[k] = True
    counted = np.zeros(n, bool)   # cambios validos (ambas velas fuera de calentamiento)
    false_ = np.zeros(n, bool)
    for k in range(1, n):
        if not chg[k] or pos[k - 1] < WARM or pos[k] < WARM:
            continue
        counted[k] = True
        d = state[k]
        m = k + 1
        end = blk_end[block[k]]
        best = -math.inf
        while m <= end:
            v = (h[m] - c[k]) if d == 1 else (c[k] - l[m])
            if v > best:
                best = v
            if chg[m]:
                break
            m += 1
        if not best >= 5:
            false_[k] = True
    # sesiones elegibles: todas sus velas fuera de calentamiento
    per = {}
    for k in range(n):
        s = sess[k]
        e = per.setdefault(s, [True, 0, 0, 0])
        if pos[k] < WARM:
            e[0] = False
        if rth[k]:
            e[3] += 1
            if counted[k]:
                e[1] += 1
                if false_[k]:
                    e[2] += 1
    return per


# ---------------------------------------------------------------- C) geometria
def geometry(df, block, pos, state, include_flip_candle_in_E=True, horizon=120, span=10.0):
    h = df["h"].tolist(); l = df["l"].tolist(); c = df["c"].tolist()
    sess = df["session"].tolist()
    n = len(h)
    s_hi = {}
    for k, s in enumerate(sess):
        s_hi[s] = k   # ultimo indice de la sesion
    rows = []
    E = None
    for k in range(n):
        d = state[k]
        if d == 0:
            E = None
            continue
        flip = (k > 0 and block[k] == block[k - 1] and state[k - 1] != 0 and state[k - 1] != d)
        first = (k == 0 or block[k] != block[k - 1] or state[k - 1] == 0)
        if flip or first:
            if include_flip_candle_in_E or first:
                E = h[k] if d == 1 else l[k]
            else:
                E = None
        else:
            if E is None:
                E = h[k] if d == 1 else l[k]
            elif d == 1:
                E = max(E, h[k])
            else:
                E = min(E, l[k])
        if pos[k] < WARM or flip:
            continue
        r = d * (E - c[k])
        if not r > 0 or r > 12:
            continue
        tgt = c[k] - d * span
        last = min(k + horizon, s_hi[sess[k]])
        y = 0
        for j in range(k + 1, last + 1):
            if d == 1:
                hitE = h[j] >= E
                hitT = l[j] <= tgt
            else:
                hitE = l[j] <= E
                hitT = h[j] >= tgt
            if hitE:
                y = 0
                break
            if hitT:
                y = 1
                break
        rows.append((r, y))
    return np.array(rows, float)


BUCKETS = [(0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (5, 6), (6, 8), (8, 10), (10, 12)]


def main():
    df = load()
    block, pos, reasons, uniq = make_blocks(df)
    out("velas:", len(df), "| sesiones:", len(uniq), uniq[0], "..", uniq[-1])
    nd = sum(1 for s in uniq if s <= DESIGN_END); ne = sum(1 for s in uniq if EXAM_START <= s <= LAST_SESSION)
    out("sesiones DESIGN:", nd, "| EXAM:", ne)
    out("reinicios de bloque:")
    for k, why in reasons:
        out("   ", df["t"].iloc[k], "sesion", df["session"].iloc[k], "->", why)
    ST, R, starts = run_detectors(df, block)
    df["ST"] = ST; df["R10"] = R
    sess = df["session"].to_numpy()
    in_design = np.array([s <= DESIGN_END for s in sess]); in_exam = np.array([EXAM_START <= s <= LAST_SESSION for s in sess])

    # ------------------------------------------------ A
    out("\n=== A) movimientos >= 20 pts en <= 30 min ===")
    variants = [("principal (empate permitido, dedupe vs ultimo conservado)", False, True),
                ("variante: minimo estricto unico", True, True),
                ("variante: dedupe vs ultimo inicio (cadena)", False, False)]
    for vname, strict, dvk in variants:
        moves = find_moves(df, pos, strict=strict, dedupe_vs_last_kept=dvk)
        out("--", vname)
        for pname, mask in (("DESIGN", in_design), ("EXAM", in_exam)):
            mv = [(i, d) for i, d in moves if mask[i]]
            nup = sum(1 for _, d in mv if d == 1)
            res = {}
            for dn, stt in (("ST(10,3)", ST), ("R10", R)):
                lm, lmean, fnd = lateness(df, block, mv, stt)
                res[dn] = lm
                out("   %s %-8s moves=%d (up %d / down %d) median=%s mean=%.2f p90=%.2f  sin-estado-en-120=%d" % (
                    pname, dn, len(mv), nup, len(mv) - nup, median_inf(lm), float(np.mean(lmean)),
                    float(np.percentile(lmean, 90)), int((~fnd).sum())))
            a, b = res["R10"], res["ST(10,3)"]
            earlier = int(np.sum(a < b)); later = int(np.sum(a > b)); equal = len(a) - earlier - later
            out("   %s pareado: R10 antes=%d, despues=%d, igual=%d" % (pname, earlier, later, equal))
            if vname.startswith("principal") and pname == "EXAM":
                out("   EXAM detalle (t_utc, dir, ext, late ST, late R10):")
                for (i, d), x, y in zip(mv, b, a):
                    out("     ", df["t"].iloc[i], "+1" if d == 1 else "-1",
                        df["l"].iloc[i] if d == 1 else df["h"].iloc[i], x, y)

    # ------------------------------------------------ B
    out("\n=== B) ruido en RTH (cambios de estado por sesion) ===")
    for dn, stt in (("ST(10,3)", ST), ("R10", R)):
        per = noise(df, block, pos, stt)
        for pname, cond in (("DESIGN", lambda s: s <= DESIGN_END), ("EXAM", lambda s: EXAM_START <= s <= LAST_SESSION)):
            el = [(s, v) for s, v in per.items() if cond(s) and v[0]]
            ex = [s for s, v in per.items() if cond(s) and not v[0]]
            ns = len(el)
            ch = sum(v[1] for _, v in el); fa = sum(v[2] for _, v in el)
            out("   %s %-8s sesiones=%d (excluidas %d: %s) cambios/sesion=%.2f falsos/sesion=%.2f  (total %d / %d, %% falsos %.1f)" % (
                pname, dn, ns, len(ex), ",".join(x.isoformat()[5:] for x in sorted(ex)), ch / ns, fa / ns, ch, fa,
                100.0 * fa / ch if ch else float("nan")))
            north = [s for s, v in el if v[3] == 0]
            if north:
                out("      ojo: sesiones elegibles sin velas RTH:", north)

    # ------------------------------------------------ C
    out("\n=== C) geometria ST(10,3): P(llega a close-d*10 antes de volver a E) ===")
    for gname, inc in (("principal (E incluye la vela del giro)", True), ("variante: E desde la vela siguiente al giro", False)):
        rows = geometry(df, block, pos, ST, include_flip_candle_in_E=inc)
        out("--", gname, "| filas con 0<r<=12:", len(rows))
        for a, b in BUCKETS:
            m = (rows[:, 0] > a) & (rows[:, 0] <= b)
            if m.sum() == 0:
                out("   (%g,%g] n=0" % (a, b)); continue
            mr = rows[m, 0].mean(); p = rows[m, 1].mean()
            rw = mr / (mr + 10)
            se = math.sqrt(p * (1 - p) / m.sum())
            out("   (%g,%g]  n=%6d  r medio=%.3f  P(y=1)=%.4f  (+-%.4f)  RW r/(r+10)=%.4f  dif=%+.4f" % (
                a, b, int(m.sum()), mr, p, se, rw, p - rw))

    with open(__file__.replace("auditoria.py", "salida_auditoria.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(OUT_LINES) + "\n")


if __name__ == "__main__":
    main()
