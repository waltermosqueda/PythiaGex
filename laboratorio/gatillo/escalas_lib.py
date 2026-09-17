# -*- coding: utf-8 -*-
"""
escalas_lib.py — familia "escalas" (temporalidad y delta por tamaño de orden). Rasgos y disparos, todo causal.
El MISMO codigo corre en exploracion y en confirmacion; los umbrales salen de escalas_umbrales.json (congelados con exploracion).
Definiciones: ver resultados/escalas.md, seccion PRE-REGISTRO.
"""
import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B
import numpy as np, pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
UMBRALES = os.path.join(AQUI, "escalas_umbrales.json")
TMP = os.environ.get("ESCALAS_TMP") or os.path.join(os.environ.get("TEMP", AQUI), "escalas_tmp")
os.makedirs(TMP, exist_ok=True)

VARIANTES = ["T15s", "T5m", "A_align", "A_noturn", "A_full", "S1m", "S5m", "S15m", "L5m", "E5m", "VB", "RB"]
BARRERAS = [(8, 600), (5, 300), (12, 900)]
RANGO_RB = 8.0
REFRACTARIO = 60


def rsum(x, W):
    """Suma movil causal de W filas que termina en t (NaN donde no hay W filas)."""
    c = np.cumsum(np.asarray(x, "float64")); out = np.full(len(c), np.nan)
    out[W - 1] = c[W - 1] if len(c) >= W else np.nan
    if len(c) > W: out[W:] = c[W:] - c[:-W]
    return out


def lag(x, k):
    out = np.full(len(x), np.nan); out[k:] = x[:-k]; return out


def cociente(a, b):
    with np.errstate(divide="ignore", invalid="ignore"):
        r = a / b
    r[~np.isfinite(r)] = 0.0
    return r


def rasgos(seg):
    """Todos los rasgos de una sesion sobre la grilla de 1 s."""
    f = {}
    ep = (seg.index.view("int64") // 10**9).astype("int64"); f["ep"] = ep
    sod = ep % 86400; f["sod"] = sod
    f["valido"] = seg["rueda"].to_numpy() & (sod >= 13 * 3600 + 32 * 60)
    f["rueda"] = seg["rueda"].to_numpy()
    p = seg["ultimo"].to_numpy("float64"); f["p"] = p
    delta = seg["delta"].to_numpy("float64"); vol = seg["vol"].to_numpy("float64")
    chicos = (seg["d1"] + seg["d2_4"]).to_numpy("float64"); grandes = (seg["d10_49"] + seg["d50"]).to_numpy("float64")
    for W in (15, 60, 300, 900):
        f["D%d" % W] = rsum(delta, W); f["V%d" % W] = rsum(vol, W); f["r%d" % W] = cociente(f["D%d" % W], f["V%d" % W])
    for W in (15, 120, 300):
        f["ret%d" % W] = p - lag(p, W)
    for W in (60, 300, 900):
        f["rs%d" % W] = cociente(rsum(chicos, W), f["V%d" % W]); f["rl%d" % W] = cociente(rsum(grandes, W), f["V%d" % W])
    return f


def velas_volumen(seg, f, Vb):
    """Velas por volumen desde 13:30:00. Devuelve (indice de cierre en la tabla, delta, vol) por vela."""
    idx = np.flatnonzero(f["rueda"])
    if len(idx) == 0: return np.array([], int), np.array([]), np.array([])
    v = seg["vol"].to_numpy("float64")[idx]; d = seg["delta"].to_numpy("float64")[idx]
    cv = np.cumsum(v); cd = np.cumsum(d); k = np.floor(cv / Vb)
    cierre = np.flatnonzero(np.diff(k, prepend=0) > 0)
    cvc = cv[cierre]; cdc = cd[cierre]
    return idx[cierre], np.diff(cdc, prepend=0.0), np.diff(cvc, prepend=0.0)


def velas_rango(seg, f, R=RANGO_RB):
    """Velas por rango de R puntos desde 13:30:00. Devuelve (indice de cierre, direccion, delta, vol)."""
    idx = np.flatnonzero(f["rueda"])
    if len(idx) == 0: return np.array([], int), np.array([]), np.array([]), np.array([])
    hi = seg["alto"].to_numpy("float64")[idx]; lo = seg["bajo"].to_numpy("float64")[idx]; p = f["p"][idx]
    v = seg["vol"].to_numpy("float64")[idx]; d = seg["delta"].to_numpy("float64")[idx]
    cierres, dirs, ds, vs = [], [], [], []
    ap = p[0]; mx = -np.inf; mn = np.inf; sd = 0.0; sv = 0.0
    for i in range(len(idx)):
        if hi[i] > mx: mx = hi[i]
        if lo[i] < mn: mn = lo[i]
        sd += d[i]; sv += v[i]
        if mx - mn >= R:
            cierres.append(idx[i]); dirs.append(np.sign(p[i] - ap)); ds.append(sd); vs.append(sv)
            ap = p[i]; mx = -np.inf; mn = np.inf; sd = 0.0; sv = 0.0
    return np.array(cierres, int), np.array(dirs), np.array(ds), np.array(vs)


def fresco(cond, valido, k=REFRACTARIO):
    """Arranque fresco: cond verdadera en t y falsa en los k segundos anteriores."""
    c = np.nan_to_num(cond.astype("float64")); cs = np.cumsum(c)
    prev = lag(cs, 1) - lag(cs, k + 1)      # suma de cond en [t-k, t-1]
    prev = np.nan_to_num(prev, nan=1.0)     # sin historia suficiente: no dispara
    return np.flatnonzero((c > 0) & (prev == 0) & valido)


def refractario(idx, lado, k=REFRACTARIO):
    """Desagrupa: minimo k segundos entre disparos, gana el primero."""
    keep = []; ult = -10**9
    for j, i in enumerate(idx):
        if i - ult >= k: keep.append(j); ult = i
    keep = np.array(keep, int)
    return idx[keep], lado[keep]


def disparos(seg, f, U):
    """{variante: (indices en la tabla de la sesion, lados)} con los umbrales U congelados."""
    out = {}; val = f["valido"]; sod = f["sod"]
    sg = np.sign
    # --- 1-2: velas de reloj
    for nom, W, q in (("T15s", 15, "T15s_r"), ("T5m", 300, "T5m_r")):
        cierre = ((sod + 1) % W == 0) & val
        r = f["r%d" % W]; V = f["V%d" % W]
        m = cierre & (np.abs(r) >= U[q]) & (V >= U[nom + "_vmin"]) & (sg(r) != 0)
        i = np.flatnonzero(m); out[nom] = refractario(i, sg(r[i]))
    # --- 3-5: alineacion
    S = sg(f["D900"]); tend = (np.abs(f["r900"]) >= U["r900_q50"]) & (S != 0)
    c = tend & (sg(f["D300"]) == S) & (sg(f["D60"]) == S) & (sg(f["D15"]) == S) & (np.abs(f["r300"]) >= U["r300_q50"]) & (np.abs(f["r60"]) >= U["r60_q50"]) & (np.abs(f["r15"]) >= U["r15_q50"])
    i = fresco(c, val); out["A_align"] = (i, S[i])
    retro = (f["ret120"] * S <= -4.0) & (f["D60"] * S < 0)
    c = tend & retro
    i = fresco(c, val); out["A_noturn"] = (i, S[i])
    # retroceso medido en t-15 contra la tendencia de AHORA (S en t), giro en los ultimos 15 s
    ret120_l = lag(f["ret120"], 15); D60_l = lag(f["D60"], 15)
    c = tend & (ret120_l * S <= -4.0) & (D60_l * S < 0) & (f["D15"] * S > 0) & (f["ret15"] * S > 0)
    i = fresco(c, val); out["A_full"] = (i, S[i])
    # --- 6-8: divergencia chicos / grandes
    for nom, W in (("S1m", 60), ("S5m", 300), ("S15m", 900)):
        rs = f["rs%d" % W]; rl = f["rl%d" % W]
        c = (sg(rs) == -sg(rl)) & (sg(rl) != 0) & (np.abs(rs) >= U["rs%d_q50" % W]) & (np.abs(rl) >= U["rl%d_q50" % W])
        i = fresco(c, val); out[nom] = (i, sg(rl[i]))
    # --- 9: grandes solos
    rl = f["rl300"]; c = (np.abs(rl) >= U["rl300_q90"]) & (sg(rl) != 0)
    i = fresco(c, val); out["L5m"] = (i, sg(rl[i]))
    # --- 10: esfuerzo sin resultado
    s3 = sg(f["D300"]); c = (np.abs(f["r300"]) >= U["E5m_r"]) & (s3 != 0) & (f["ret300"] * s3 <= 0)
    i = fresco(c, val); out["E5m"] = (i, -s3[i])
    # --- 11: velas por volumen
    ci, dd, vv = velas_volumen(seg, f, U["Vb"])
    if len(ci):
        r = cociente(dd.copy(), vv); m = (np.abs(r) >= U["VB_r_q90"]) & val[ci] & (sg(r) != 0)
        out["VB"] = refractario(ci[m], sg(r[m]))
    else: out["VB"] = (np.array([], int), np.array([]))
    # --- 12: velas por rango
    ci, dr, dd, vv = velas_rango(seg, f)
    if len(ci):
        r = cociente(dd.copy(), vv); m = (dr != 0) & (sg(dd) == -dr) & (np.abs(r) >= U["RB_r_q50"]) & val[ci]
        out["RB"] = refractario(ci[m], dr[m])
    else: out["RB"] = (np.array([], int), np.array([]))
    return out


def barreras_sesion(nombre, seg):
    """Barreras de una sesion, cacheadas en TEMP (no en el proyecto)."""
    fn = os.path.join(TMP, "y-%s.npz" % nombre)
    if os.path.exists(fn):
        z = np.load(fn); return {(x, h): z["y_%d_%d" % (x, h)] for x, h in BARRERAS}
    d = {}
    for x, h in BARRERAS:
        y, _ = B.barrera(seg, x, h); d[(x, h)] = y.astype("int8")
    np.savez_compressed(fn, **{"y_%d_%d" % k: v for k, v in d.items()})
    return d


def t_por_dias(y, lado, dias):
    y = np.asarray(y); lado = np.asarray(lado); ok = y != 0
    if ok.sum() == 0: return np.nan
    s = pd.Series((y[ok] * lado[ok]) > 0).groupby(np.asarray(dias)[ok]).mean()
    if len(s) < 3 or s.std(ddof=1) == 0: return np.nan
    return float((s.mean() - 0.5) / (s.std(ddof=1) / np.sqrt(len(s))))


def mov_firmado(f, idx, lado, ks=(10, 30, 60)):
    p = f["p"]; n = len(p); out = {}
    for k in ks:
        j = np.minimum(idx + k, n - 1); out[k] = (p[j] - p[idx]) * lado
    return out


def juzgar_todo(T, U, variantes=VARIANTES, invertir=()):
    """Corre disparos + barreras en las sesiones de T. Devuelve (tabla de resultados, detalle de disparos)."""
    det = {v: dict(ses=[], idx=[], lado=[], sod=[], y={b: [] for b in BARRERAS}, mov={10: [], 30: [], 60: []}) for v in variantes}
    for s in sorted(T):
        seg = T[s]; f = rasgos(seg); Y = barreras_sesion(s, seg); dis = disparos(seg, f, U)
        for v in variantes:
            i, l = dis[v]; l = np.asarray(l, "float64")
            if v in invertir: l = -l
            d = det[v]; d["ses"] += [s] * len(i); d["idx"] += list(i); d["lado"] += list(l); d["sod"] += list(f["sod"][i])
            for b in BARRERAS: d["y"][b] += list(Y[b][i])
            mv = mov_firmado(f, i, l)
            for k in mv: d["mov"][k] += list(mv[k])
    filas = []
    for v in variantes:
        d = det[v]; lado = np.array(d["lado"]); dias = np.array(d["ses"])
        fila = dict(variante=v + ("(inv)" if v in invertir else ""), disparos=len(lado))
        for (x, h) in BARRERAS:
            y = np.array(d["y"][(x, h)]); r = B.juzgar(y, lado, v, dias, x)
            pre = "b%d_" % x
            fila[pre + "n"] = r.get("n", 0); fila[pre + "ac"] = r.get("acierto"); fila[pre + "z"] = r.get("z"); fila[pre + "dias"] = r.get("dias_arriba"); fila[pre + "peor"] = r.get("peor_dia")
            fila[pre + "t_dias"] = round(t_por_dias(y, lado, dias), 2) if len(lado) else np.nan
        for k in (10, 30, 60): fila["mov%d" % k] = round(float(np.mean(d["mov"][k])), 3) if len(lado) else np.nan
        filas.append(fila)
    return pd.DataFrame(filas), det


def placebo(T, det_v, x_h, n_sorteos=200, semilla=7):
    """Placebo de un finalista: mismos disparos por sesion y media hora, mismos lados, segundos al azar. Devuelve (media, desvio, lista)."""
    rng = np.random.default_rng(semilla)
    ses = np.array(det_v["ses"]); lado = np.array(det_v["lado"]); sod = np.array(det_v["sod"])
    mh = (sod - (13 * 3600 + 30 * 60)) // 1800
    pools = {}; Ys = {}
    for s in sorted(set(ses)):
        seg = T[s]; f = rasgos_min(seg); Ys[s] = barreras_sesion(s, seg)[x_h]
        b = (f["sod"] - (13 * 3600 + 30 * 60)) // 1800
        for k in np.unique(mh[ses == s]):
            pools[(s, k)] = np.flatnonzero(f["valido"] & (b == k))
    acs = []
    for _ in range(n_sorteos):
        ys = np.empty(len(lado))
        for (s, k), pool in pools.items():
            m = np.flatnonzero((ses == s) & (mh == k))
            ys[m] = Ys[s][rng.choice(pool, size=len(m), replace=len(pool) < len(m))]
        ok = ys != 0
        acs.append(float(((ys[ok] * lado[ok]) > 0).mean()))
    acs = np.array(acs)
    return float(acs.mean()), float(acs.std(ddof=1)), acs


def rasgos_min(seg):
    ep = (seg.index.view("int64") // 10**9).astype("int64"); sod = ep % 86400
    return dict(sod=sod, valido=seg["rueda"].to_numpy() & (sod >= 13 * 3600 + 32 * 60))


def cargar_umbrales():
    return json.load(open(UMBRALES, encoding="utf-8"))
