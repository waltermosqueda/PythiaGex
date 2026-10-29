# -*- coding: utf-8 -*-
"""
Auditoria independiente 3 (29-10): swing verdadero (zigzag retrospectivo de umbral Z sobre maximos/minimos,
por bloque) contra R4 live (toque), R10 (cierre) y ST(10,3), en MES 1 minuto.

Codigo propio, escrito desde la especificacion. Reutiliza SOLO auditoria.py de esta carpeta (carga, sesiones,
bloques, calentamiento, RTH, ST(10,3) y R10), que tambien escribi yo. No abre ni importa nada del laboratorio.

Lectura PRIMARIA del zigzag, fijada ANTES de ver resultados ("V3/first"):
  - antes del primer pivote: H/L corridos INCLUYENDO la vela actual; el pivote se confirma con una vela
    estrictamente POSTERIOR a la vela del extremo (literal: "some later candle").
  - en el swing: la misma regla "vela posterior" -> si la vela hace nuevo extremo, extiende el swing; si no,
    puede confirmar el giro (zigzag clasico). Empates de extremo: se queda la PRIMERA vela.
Variantes de sensibilidad (no elegidas para calzar):
  V1 = compara con el extremo de las velas ANTERIORES (como R4 live) y recien despues actualiza;
  V2 = actualiza el extremo con la vela actual y despues compara (la misma vela puede ser pivote y confirmarlo);
  tie=last = en empates de extremo se queda la ULTIMA vela.

Uso:  python auditoria_zigzag.py      -> imprime y guarda salida_zigzag.txt
"""
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import auditoria as A  # noqa: E402  (al importarse baja la prioridad del proceso a IDLE)

LINES = []


def out(*a):
    s = " ".join(str(x) for x in a)
    print(s, flush=True)
    LINES.append(s)


# ====================================================================== swing verdadero
def ext_range(arr, a, b, is_max, tie):
    """Extremo de arr[a..b] inclusive -> (valor, indice); (None, None) si a > b."""
    if a > b:
        return None, None
    v = arr[a]; iv = a
    for j in range(a + 1, b + 1):
        x = arr[j]
        if (x > v if is_max else x < v) or (tie == "last" and x == v):
            v = x; iv = j
    return v, iv


def zigzag_block(h, l, Z, mode="V3", tie="first"):
    """Devuelve (lab, live, piv, n_both).
    lab[k]  = +1/-1 swing verdadero (retrospectivo), 0 = sin etiqueta.
    live[k] = direccion confirmada al terminar la vela k (version causal del mismo zigzag), 0 = antes del 1er pivote.
    piv     = [(indice, tipo)] tipo +1 = pivote de minimo (empieza swing alcista), -1 = de maximo.
    """
    n = len(h)
    lab = np.zeros(n, dtype=np.int8)
    live = np.zeros(n, dtype=np.int8)
    piv = []
    n_both = 0
    if n < 2:
        return lab, live, piv, n_both

    # ---------------- antes del primer pivote
    H, iH, L, iL = h[0], 0, l[0], 0
    d = 0; E = None; iE = None
    kf = None
    for k in range(1, n):
        if h[k] > H or (tie == "last" and h[k] == H):
            H, iH = h[k], k
        if l[k] < L or (tie == "last" and l[k] == L):
            L, iL = l[k], k
        up = iL < k and h[k] >= L + Z
        dn = iH < k and l[k] <= H - Z
        if up and dn:              # demostrado imposible con esta lectura; se cuenta por las dudas
            n_both += 1
            up = iL < iH           # el extremo mas viejo es el pivote
            dn = not up
        if up:
            piv.append((iL, 1)); d = 1
            E, iE = ext_range(h, iL + 1, k, True, tie)
        elif dn:
            piv.append((iH, -1)); d = -1
            E, iE = ext_range(l, iH + 1, k, False, tie)
        if d != 0:
            kf = k
            break
    if d == 0:
        return lab, live, piv, n_both

    def flip(k):
        nonlocal d, E, iE
        p = iE
        d = -d
        piv.append((p, d))
        if d == -1:
            E, iE = ext_range(l, p + 1, k, False, tie)
        else:
            E, iE = ext_range(h, p + 1, k, True, tie)

    # V2: la vela que confirmo el primer pivote ya esta dentro de E; se completa su chequeo
    if mode == "V2" and E is not None:
        if (d == 1 and l[kf] <= E - Z) or (d == -1 and h[kf] >= E + Z):
            flip(kf)
    live[kf] = d

    # ---------------- swings
    for k in range(kf + 1, n):
        if d == 1:
            if mode == "V1":
                if E is not None and l[k] <= E - Z:
                    flip(k)
                elif E is None or h[k] > E or (tie == "last" and h[k] == E):
                    E, iE = h[k], k
            elif mode == "V2":
                if E is None or h[k] > E or (tie == "last" and h[k] == E):
                    E, iE = h[k], k
                if l[k] <= E - Z:
                    flip(k)
            else:  # V3
                if E is None or h[k] > E or (tie == "last" and h[k] == E):
                    E, iE = h[k], k
                elif l[k] <= E - Z:
                    flip(k)
        else:
            if mode == "V1":
                if E is not None and h[k] >= E + Z:
                    flip(k)
                elif E is None or l[k] < E or (tie == "last" and l[k] == E):
                    E, iE = l[k], k
            elif mode == "V2":
                if E is None or l[k] < E or (tie == "last" and l[k] == E):
                    E, iE = l[k], k
                if h[k] >= E + Z:
                    flip(k)
            else:  # V3
                if E is None or l[k] < E or (tie == "last" and l[k] == E):
                    E, iE = l[k], k
                elif h[k] >= E + Z:
                    flip(k)
        live[k] = d

    # ---------------- etiquetas: (p_i, p_{i+1}] pertenece al swing que empieza en p_i
    for (p, t), (q, _) in zip(piv[:-1], piv[1:]):
        assert q > p, (p, q)
        lab[p + 1:q + 1] = t
    return lab, live, piv, n_both


# ====================================================================== detectores nuevos
def touch_block(h, l, st, thr):
    """R{thr} live (toque): compara con la E de la vela ANTERIOR; al girar, E = minimo/maximo de esta vela."""
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
            if l[k] <= E - thr:
                d = -1; E = l[k]
            elif h[k] > E:
                E = h[k]
        else:
            if h[k] >= E + thr:
                d = 1; E = h[k]
            elif l[k] < E:
                E = l[k]
        s[k] = d
    return s


# ====================================================================== medicion
def main():
    df = A.load()
    block, pos, reasons, uniq = A.make_blocks(df)
    n = len(df)
    h = df["h"].tolist(); l = df["l"].tolist(); c = df["c"].tolist()
    sess = df["session"].to_numpy()
    rth = df["rth"].to_numpy()
    valid = pos >= A.WARM
    starts = [0] + [k for k in range(1, n) if block[k] != block[k - 1]] + [n]

    # sesiones puntuables: ninguna vela de calentamiento
    bad = set(sess[~valid].tolist())
    scored = [s for s in uniq if s not in bad]
    des = [s for s in scored if s <= A.DESIGN_END]
    exa = [s for s in scored if A.EXAM_START <= s <= A.LAST_SESSION]
    out("velas:", n, "| sesiones:", len(uniq), uniq[0], "..", uniq[-1], "| bloques:", len(starts) - 1)
    out("excluidas por calentamiento:", sorted(x.isoformat() for x in bad))
    out("puntuables DESIGN:", len(des), "| EXAM:", len(exa))
    idx_of = {}
    for k in range(n):
        idx_of.setdefault(sess[k], []).append(k)
    sess_idx = {s: np.array(v) for s, v in idx_of.items()}

    # ---------------- detectores
    ST = np.zeros(n, np.int8); R10 = np.zeros(n, np.int8)
    fam_live = {T: np.zeros(n, np.int8) for T in (1, 2, 3, 4, 5, 6, 8, 10)}
    fam_close = {T: np.zeros(n, np.int8) for T in (4, 6, 10)}
    for a, z in zip(starts[:-1], starts[1:]):
        st = A.supertrend_block(h[a:z], l[a:z], c[a:z])
        ST[a:z] = st
        R10[a:z] = A.r10_block(h[a:z], l[a:z], c[a:z], st, 10.0)
        for T in fam_live:
            fam_live[T][a:z] = touch_block(h[a:z], l[a:z], st, float(T))
        for T in fam_close:
            fam_close[T][a:z] = A.r10_block(h[a:z], l[a:z], c[a:z], st, float(T))
    R4 = fam_live[4]
    assert np.array_equal(fam_close[10], R10)
    out("estado 0 entre velas validas: ST", int((ST[valid] == 0).sum()), "R10", int((R10[valid] == 0).sum()),
        "R4", int((R4[valid] == 0).sum()))

    # ---------------- zigzag por bloque, todas las variantes
    variants = [("V3", "first"), ("V3", "last"), ("V1", "first"), ("V1", "last"), ("V2", "first"), ("V2", "last")]
    Zs = (4, 6, 8, 10)
    LAB = {}; LIVE = {}
    for mode, tie in variants:
        for Z in Zs:
            lab = np.zeros(n, np.int8); live = np.zeros(n, np.int8)
            firsts = []; nb = 0; npiv = 0
            for a, z in zip(starts[:-1], starts[1:]):
                lb, lv, pv, both = zigzag_block(h[a:z], l[a:z], float(Z), mode, tie)
                lab[a:z] = lb; live[a:z] = lv; nb += both; npiv += len(pv)
                firsts.append((pv[0][0] if pv else None, pv[1][0] if len(pv) > 1 else None))
            LAB[(mode, tie, Z)] = lab; LIVE[(mode, tie, Z)] = live
            if (mode, tie) == ("V3", "first"):
                out("zigzag V3/first Z=%d: pivotes=%d, (1er,2do) pivote por bloque (posicion en bloque)=%s, empates 1er pivote=%d"
                    % (Z, npiv, firsts, nb))

    def acc_by_session(lab, state, sessions):
        r = {}
        for s in sessions:
            ix = sess_idx[s]
            m = rth[ix] & valid[ix] & (lab[ix] != 0)
            if m.sum() == 0:
                r[s] = float("nan"); continue
            r[s] = float(np.mean(state[ix][m] == lab[ix][m]))
        return r

    def mean_acc(lab, state, sessions):
        r = acc_by_session(lab, state, sessions)
        v = [x for x in r.values() if not math.isnan(x)]
        return 100.0 * float(np.mean(v)), r

    # ---------------- auto-chequeos
    for (mode, tie) in (("V1", "first"), ("V1", "last"), ("V3", "first")):
        lv = LIVE[(mode, tie, 4)]
        m = valid & (lv != 0)
        out("auto-chequeo: R4 live == zigzag causal %s/%s Z=4 en velas validas: %.4f %% (%d distintas de %d)" % (
            mode, tie, 100.0 * float(np.mean(R4[m] == lv[m])), int((R4[m] != lv[m]).sum()), int(m.sum())))
    for T in (6, 10):
        lv = LIVE[("V1", "first", T)]
        m = valid & (lv != 0)
        out("auto-chequeo: R%d live == zigzag causal V1/first Z=%d: %.4f %%" % (
            T, T, 100.0 * float(np.mean(fam_live[T][m] == lv[m]))))

    # ---------------- tabla principal (todas las variantes)
    exp = {("DESIGN", 6): (76.11, 55.78, 58.66), ("EXAM", 6): (75.99, 54.87, 57.36),
           ("EXAM", 4): (78.22, 51.71, None), ("EXAM", 8): (71.96, 57.84, None), ("EXAM", 10): (68.67, 64.45, None)}
    for mode, tie in variants:
        out("\n=== exactitud media por sesion (%%), zigzag %s/%s%s ===" % (mode, tie,
            "  <- PRIMARIA" if (mode, tie) == ("V3", "first") else ""))
        for pname, ss in (("DESIGN", des), ("EXAM", exa)):
            for Z in Zs:
                lab = LAB[(mode, tie, Z)]
                a4, _ = mean_acc(lab, R4, ss); a10, _ = mean_acc(lab, R10, ss); ast, _ = mean_acc(lab, ST, ss)
                nlab = int(sum(int((rth[sess_idx[s]] & (lab[sess_idx[s]] != 0)).sum()) for s in ss))
                e = exp.get((pname, Z))
                es = ""
                if e:
                    es = "   esperado R4 %.2f R10 %.2f%s" % (e[0], e[1], (" ST %.2f" % e[2]) if e[2] else "")
                out("  %-6s Z=%-2d  R4 live %6.2f   R10 %6.2f   ST %6.2f   (velas RTH etiquetadas %d)%s" % (
                    pname, Z, a4, a10, ast, nlab, es))

    # ---------------- EXAM Z=6: sesion por sesion, R4 live vs R10 (primaria y variantes)
    for mode, tie in variants:
        lab = LAB[(mode, tie, 6)]
        _, r4 = mean_acc(lab, R4, exa); _, r10 = mean_acc(lab, R10, exa)
        w = sum(1 for s in exa if r4[s] > r10[s]); lo = sum(1 for s in exa if r4[s] < r10[s])
        out("EXAM Z=6 %s/%s: R4 live gana %d, pierde %d, empata %d de %d sesiones; minima ventaja %+.2f pp" % (
            mode, tie, w, lo, len(exa) - w - lo, len(exa), 100 * min(r4[s] - r10[s] for s in exa)))
        if (mode, tie) == ("V3", "first"):
            for s in exa:
                out("     %s  R4 %.2f  R10 %.2f  dif %+.2f" % (s, 100 * r4[s], 100 * r10[s], 100 * (r4[s] - r10[s])))

    # ---------------- cambios de estado en RTH y giros falsos (EXAM)
    blk_end = {}
    for k in range(n):
        blk_end[block[k]] = k

    def changes(state, thr):
        chg = np.zeros(n, bool)
        chg[1:] = (block[1:] == block[:-1]) & (state[1:] != state[:-1]) & (state[1:] != 0) & (state[:-1] != 0)
        fal = np.zeros(n, bool)
        for k in np.nonzero(chg)[0]:
            d = state[k]; end = blk_end[block[k]]; best = -math.inf
            m = k + 1
            while m <= end:
                v = (h[m] - c[k]) if d == 1 else (c[k] - l[m])
                if v > best:
                    best = v
                if best >= thr or chg[m]:
                    break
                m += 1
            fal[k] = best < thr
        return chg, fal

    out("\n=== cambios de estado en RTH por sesion y giros falsos (umbral 2.5 pts) ===")
    for dn, stt in (("R4 live", R4), ("R10", R10), ("ST(10,3)", ST)):
        chg, fal = changes(stt, 2.5)
        for pname, ss in (("DESIGN", des), ("EXAM", exa)):
            tc = sum(int((chg[sess_idx[s]] & rth[sess_idx[s]]).sum()) for s in ss)
            tf = sum(int((fal[sess_idx[s]] & rth[sess_idx[s]]).sum()) for s in ss)
            out("  %-6s %-8s cambios/sesion %.2f  falsos/sesion %.2f  (total %d / %d, %.1f %% falsos)" % (
                pname, dn, tc / len(ss), tf / len(ss), tc, tf, 100.0 * tf / tc if tc else float("nan")))
    # sensibilidad del falso: incluir la vela del giro en la excursion
    out("  (esperado EXAM: R4 live 82.06 / falsos 39.56; R10 10.50 / falsos 1.69)")

    # ---------------- sondas de sesgo (zigzag primaria V3/first)
    out("\n=== sondas de sesgo: familia de toque R{T} live y de cierre R{T} close, Z=6, primaria V3/first ===")
    lab6 = LAB[("V3", "first", 6)]
    for pname, ss in (("DESIGN", des), ("EXAM", exa)):
        row = []
        for T in sorted(fam_live):
            a, _ = mean_acc(lab6, fam_live[T], ss)
            chg, fal = changes(fam_live[T], 2.5)
            tc = sum(int((chg[sess_idx[s]] & rth[sess_idx[s]]).sum()) for s in ss)
            row.append("R%d live %.2f (%.1f cambios)" % (T, a, tc / len(ss)))
        out("  %s toque : %s" % (pname, " | ".join(row)))
        row = []
        for T in sorted(fam_close):
            a, _ = mean_acc(lab6, fam_close[T], ss)
            row.append("R%d close %.2f" % (T, a))
        out("  %s cierre: %s" % (pname, " | ".join(row)))
    out("\n=== gemelo causal del etiquetador (R{Z} live = zigzag V1 en vivo) vs R10 close, primaria V3/first ===")
    for pname, ss in (("DESIGN", des), ("EXAM", exa)):
        row = []
        for Z in Zs:
            lab = LAB[("V3", "first", Z)]
            a, _ = mean_acc(lab, fam_live[Z], ss)
            b, _ = mean_acc(lab, R4, ss)
            cc, _ = mean_acc(lab, R10, ss)
            row.append("Z=%d: R%d live %.2f, R4 live %.2f, R10 close %.2f" % (Z, Z, a, b, cc))
        out("  %s %s" % (pname, " | ".join(row)))

    with open(os.path.join(HERE, "salida_zigzag.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(LINES) + "\n")


if __name__ == "__main__":
    main()
