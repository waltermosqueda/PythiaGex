# -*- coding: utf-8 -*-
"""
ubicacion_lib.py — familia "ubicacion": DONDE pasa (niveles) x QUE pasa (flujo). Todo causal.
El MISMO codigo corre en exploracion y en confirmacion; los umbrales salen de ubicacion_umbrales.json (congelados con explorar, sin mirar resultados).
Definiciones: resultados/ubicacion.md, seccion PRE-REGISTRO.
"""
import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B
import numpy as np, pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
UMBRALES = os.path.join(AQUI, "ubicacion_umbrales.json")
CACHE = os.path.join(AQUI, "ubicacion_cache"); os.makedirs(CACHE, exist_ok=True)

BARRERAS = [(8, 600), (5, 300), (12, 900)]
A_LEJOS = 8.0       # el precio tiene que haber estado a 8 puntos o mas del nivel
TOL = 0.5           # zona de toque: 2 ticks
MIRA = 900          # ... dentro de los ultimos 900 s
REFRACTARIO = 60
CONF_PTS = 3.0
PLACEBO_CORRIMIENTOS = (13.0, 21.0, 37.0)
VARIANTES = ["REF_toque_R", "MOV_toque_R", "RED_toque_R", "CONF_R", "ABS_R", "ABS_PAS_R", "SINABS_B", "EMPUJE_B", "AGOTA_R", "PAS_sigue", "MOV_div_R", "MODELO"]


# ------------------------------------------------------------------ utilidades
def rsum(x, W):
    """Suma movil causal de W filas que termina en t (con menos historia suma lo que hay)."""
    c = np.cumsum(np.asarray(x, "float64")); out = c.copy(); out[W:] = c[W:] - c[:-W]; return out


def lag(x, k):
    out = np.full(len(x), np.nan); out[k:] = x[:-k]; return out


def coc(a, b):
    with np.errstate(divide="ignore", invalid="ignore"): r = a / b
    r[~np.isfinite(r)] = 0.0; return r


def sesion_anterior(T_todas, s):
    ss = sorted(T_todas); i = ss.index(s); return T_todas[ss[i - 1]] if i > 0 else None


# ------------------------------------------------------------------ niveles
def niveles(seg, prev):
    """{nombre: (grupo, arreglo L[t] con NaN donde no esta definido, direcciones permitidas)}. Todo con datos <= t-1 para lo que cambia en el tiempo."""
    n = len(seg); p = seg["ultimo"].to_numpy("float64"); hi = seg["alto"].to_numpy("float64"); lo = seg["bajo"].to_numpy("float64")
    vol = seg["vol"].to_numpy("float64"); rueda = seg["rueda"].to_numpy(); ir = np.flatnonzero(rueda)
    out = {}
    if len(ir) == 0: return out
    i0 = ir[0]
    def const(v, desde):
        a = np.full(n, np.nan); a[desde:] = v; return a
    # VWAP y bandas (precio centrado para no perder precision)
    q = p - p[i0]; w = np.where(np.arange(n) >= i0, vol, 0.0)
    cw = np.cumsum(w); cq = np.cumsum(w * q); cq2 = np.cumsum(w * q * q)
    with np.errstate(divide="ignore", invalid="ignore"):
        m = cq / cw; var = cq2 / cw - m * m
    vwap = m + p[i0]; sd = np.sqrt(np.maximum(var, 0))
    vwap = lag(vwap, 1); sd = lag(sd, 1); vwap[:i0 + 900] = np.nan; sd[:i0 + 900] = np.nan
    out["VWAP"] = ("REF", vwap, (1, -1))
    for k, nom in ((1, "1"), (2, "2")):
        out["VWAPa%s" % nom] = ("REF", vwap + k * sd, (1, -1)); out["VWAPb%s" % nom] = ("REF", vwap - k * sd, (1, -1))
    # rueda anterior
    if prev is not None:
        r = prev[prev["rueda"]]
        if len(r):
            out["PDH"] = ("REF", const(float(r["alto"].max()), i0), (1, -1)); out["PDL"] = ("REF", const(float(r["bajo"].min()), i0), (1, -1))
            out["PDC"] = ("REF", const(float(r["ultimo"].iloc[-1]), i0), (1, -1))
    # noche
    if i0 > 0:
        out["ONH"] = ("REF", const(float(hi[:i0].max()), i0), (1, -1)); out["ONL"] = ("REF", const(float(lo[:i0].min()), i0), (1, -1))
    # rango de apertura
    for mins, nom in ((15, "15"), (30, "30")):
        f = i0 + mins * 60
        if f < n:
            out["ORH" + nom] = ("REF", const(float(hi[i0:f].max()), f), (1, -1)); out["ORL" + nom] = ("REF", const(float(lo[i0:f].min()), f), (1, -1))
    # extremos moviles (hasta t-1)
    H = pd.Series(hi); L_ = pd.Series(lo)
    for W, nom in ((1800, "30"), (3600, "60")):
        mx = H.rolling(W, min_periods=W).max().shift(1).to_numpy(); mn = L_.rolling(W, min_periods=W).min().shift(1).to_numpy()
        mx[:i0] = np.nan; mn[:i0] = np.nan
        out["MAX" + nom] = ("MOV", mx, (1,)); out["MIN" + nom] = ("MOV", mn, (-1,))
    # redondos
    a = np.floor(lo[ir].min() / 50.0) * 50.0 - 50.0; b = np.ceil(hi[ir].max() / 50.0) * 50.0 + 50.0
    for v in np.arange(a, b + 1, 50.0):
        out["R%d" % int(v)] = ("RED", const(float(v), i0), (1, -1))
    return out


def grilla_placebo(seg, corr):
    n = len(seg); rueda = seg["rueda"].to_numpy(); ir = np.flatnonzero(rueda); out = {}
    if len(ir) == 0: return out
    i0 = ir[0]; lo = seg["bajo"].to_numpy("float64")[ir].min(); hi = seg["alto"].to_numpy("float64")[ir].max()
    a = np.floor(lo / 50.0) * 50.0 - 50.0 + corr; b = np.ceil(hi / 50.0) * 50.0 + 50.0
    for v in np.arange(a, b + 1, 50.0):
        arr = np.full(n, np.nan); arr[i0:] = v; out["P%d" % int(round(v))] = ("PLA", arr, (1, -1))
    return out


def validos(seg):
    n = len(seg); ep = (seg.index.view("int64") // 10**9).astype("int64"); sod = ep % 86400
    h = seg["hueco"].to_numpy().astype("float64"); c = np.concatenate([[0.0], np.cumsum(h)])
    ini = np.maximum(np.arange(n) - 600, 0); fin = np.minimum(np.arange(n) + 900, n - 1)
    hay_hueco = (c[fin + 1] - c[ini]) > 0
    v = seg["rueda"].to_numpy() & (sod >= 13 * 3600 + 32 * 60) & ~hay_hueco & (np.arange(n) + 900 <= n - 1)
    return v, sod


def toques_nivel(p, hi, lo, L, val, dirs):
    """Lista de (t, dir) de llegadas al nivel L (arreglo). Ver PRE-REGISTRO."""
    out = []; n = len(p)
    for d in dirs:
        if d == 1:
            zona = hi >= L - TOL; prev_fuera = np.empty(n, bool); prev_fuera[0] = False; prev_fuera[1:] = hi[:-1] < (L[1:] - TOL)
        else:
            zona = lo <= L + TOL; prev_fuera = np.empty(n, bool); prev_fuera[0] = False; prev_fuera[1:] = lo[:-1] > (L[1:] + TOL)
        cand = np.flatnonzero(zona & prev_fuera & val & np.isfinite(L))
        for t in cand:
            a = max(0, t - MIRA); Lt = L[t]
            if d == 1:
                lejos = np.flatnonzero(p[a:t] <= Lt - A_LEJOS)
                if len(lejos) == 0: continue
                tau = a + lejos[-1]
                if hi[tau:t].max() >= Lt - TOL: continue
            else:
                lejos = np.flatnonzero(p[a:t] >= Lt + A_LEJOS)
                if len(lejos) == 0: continue
                tau = a + lejos[-1]
                if lo[tau:t].min() <= Lt + TOL: continue
            out.append((int(t), d))
    return out


def rasgos_flujo(seg):
    f = {}
    d = seg["delta"].to_numpy("float64"); v = seg["vol"].to_numpy("float64"); p = seg["ultimo"].to_numpy("float64")
    for W in (30, 60, 300):
        f["D%d" % W] = rsum(d, W); f["V%d" % W] = rsum(v, W)
    for c in ("abs_bid", "abs_ask", "barre_c", "barre_v", "rompe_bid", "rompe_ask", "ofi_pas"):
        f[c + "30"] = rsum(seg[c].to_numpy("float64"), 30)
    prof = (seg["rbidv"] + seg["raskv"]).to_numpy("float64"); f["prof300"] = rsum(prof, 300) / np.minimum(np.arange(len(p)) + 1, 300)
    f["p"] = p; f["ret60"] = p - np.nan_to_num(lag(p, 60), nan=p[0]); f["ret300"] = p - np.nan_to_num(lag(p, 300), nan=p[0])
    return f


def eventos_sesion(nombre, seg, prev, placebo_corr=None):
    """Tabla de eventos (un renglon por segundo+dir) con nivel, grupo, confluencia y rasgos de flujo firmados por dir."""
    p = seg["ultimo"].to_numpy("float64"); hi = seg["alto"].to_numpy("float64"); lo = seg["bajo"].to_numpy("float64")
    val, sod = validos(seg); N = niveles(seg, prev)
    fuente = N if placebo_corr is None else grilla_placebo(seg, placebo_corr)
    filas = []
    for nom, (g, L, dirs) in fuente.items():
        for t, d in toques_nivel(p, hi, lo, L, val, dirs):
            filas.append((t, d, nom, g, L[t]))
    if not filas: return pd.DataFrame()
    ev = pd.DataFrame(filas, columns=["t", "dir", "nivel", "grupo", "L"]).sort_values(["t", "dir"]).reset_index(drop=True)
    # confluencia: niveles REALES de precio distinto a CONF_PTS o menos del tocado
    nombres = list(N); M = np.vstack([N[k][1] for k in nombres])     # niveles x tiempo
    conf = []; cerca_real = []
    for t, Lv in zip(ev["t"].to_numpy(), ev["L"].to_numpy()):
        col = M[:, t]; col = col[np.isfinite(col)]
        otros = np.unique(np.round(col[(np.abs(col - Lv) <= CONF_PTS) & (np.abs(col - Lv) > 0.01)], 2))
        conf.append(len(otros)); cerca_real.append(int((np.abs(col - Lv) <= CONF_PTS).sum()))
    ev["n_conf"] = conf; ev["cerca_real"] = cerca_real
    if placebo_corr is not None: ev = ev[ev["cerca_real"] == 0].reset_index(drop=True)
    # un evento por (t, dir); se descartan segundos con los dos lados
    prio = {"REF": 0, "MOV": 1, "RED": 2, "PLA": 3}; ev["prio"] = ev["grupo"].map(prio)
    ambos = ev.groupby("t")["dir"].nunique(); ev = ev[~ev["t"].isin(ambos[ambos > 1].index)]
    g = ev.groupby("t")
    res = g.agg(dir=("dir", "first"), n_niveles=("nivel", "size"), n_conf=("n_conf", "max"),
                tiene_REF=("grupo", lambda x: int((x == "REF").any())), tiene_MOV=("grupo", lambda x: int((x == "MOV").any())),
                tiene_RED=("grupo", lambda x: int((x == "RED").any())), niveles=("nivel", lambda x: "+".join(x))).reset_index()
    prim = ev.sort_values(["t", "prio"]).drop_duplicates("t").set_index("t")
    res["grupo"] = prim.loc[res["t"], "grupo"].to_numpy(); res["nivel"] = prim.loc[res["t"], "nivel"].to_numpy(); res["L"] = prim.loc[res["t"], "L"].to_numpy()
    # rasgos de flujo
    f = rasgos_flujo(seg); t = res["t"].to_numpy(); d = res["dir"].to_numpy("float64")
    for W in (30, 60, 300): res["fd%d" % W] = d * coc(f["D%d" % W][t], f["V%d" % W][t])
    V30 = f["V30"][t]
    up = d > 0
    res["a_niv30"] = coc(np.where(up, f["abs_ask30"][t], f["abs_bid30"][t]), V30); res["a_op30"] = coc(np.where(up, f["abs_bid30"][t], f["abs_ask30"][t]), V30)
    res["bar30"] = coc(np.where(up, f["barre_c30"][t], f["barre_v30"][t]), V30); res["bar_op30"] = coc(np.where(up, f["barre_v30"][t], f["barre_c30"][t]), V30)
    res["rompe30"] = coc(np.where(up, f["rompe_ask30"][t], f["rompe_bid30"][t]), V30)
    res["pas30"] = d * coc(f["ofi_pas30"][t], f["prof300"][t])
    res["vel60"] = d * f["ret60"][t]; res["vel300"] = d * f["ret300"][t]; res["V30"] = V30
    res["min_rueda"] = (sod[t] - (13 * 3600 + 30 * 60)) / 60.0; res["sod"] = sod[t]
    vw = N["VWAP"][1][t]; sd = (N["VWAPa1"][1][t] - vw)
    res["z_vwap_dir"] = np.nan_to_num(d * coc(f["p"][t] - np.nan_to_num(vw, nan=0.0), np.where(np.isfinite(sd) & (sd > 0), sd, np.nan)), nan=0.0)
    res.loc[~np.isfinite(vw), "z_vwap_dir"] = 0.0
    res["conf"] = (res["n_conf"] >= 1).astype(int); res["ses"] = nombre
    return res


def eventos(T_todas, sesiones, placebo_corr=None, etiqueta="real"):
    """Eventos de varias sesiones, cacheados (solo rasgos, ningun resultado)."""
    partes = []
    for s in sesiones:
        fn = os.path.join(CACHE, "ev-%s-%s.parquet" % (etiqueta, s))
        if os.path.exists(fn): partes.append(pd.read_parquet(fn)); continue
        e = eventos_sesion(s, T_todas[s], sesion_anterior(T_todas, s), placebo_corr); e.to_parquet(fn); partes.append(e)
    return pd.concat(partes, ignore_index=True)


def barreras_sesion(nombre, seg):
    fn = os.path.join(CACHE, "y-%s.npz" % nombre)
    if os.path.exists(fn):
        z = np.load(fn); return {(x, h): (z["y_%d_%d" % (x, h)], z["s_%d_%d" % (x, h)]) for x, h in BARRERAS}
    d = {}
    for x, h in BARRERAS:
        y, s = B.barrera(seg, x, h); d[(x, h)] = (y.astype("int8"), np.where(np.isfinite(s), s, -1).astype("int16"))
    np.savez_compressed(fn, **{"y_%d_%d" % k: v[0] for k, v in d.items()}, **{"s_%d_%d" % k: v[1] for k, v in d.items()})
    return d


def pegar_resultados(ev, T):
    """Agrega a los eventos el resultado de las tres barreras, segundos hasta resolver y el movimiento firmado por dir a 10/30/60 s."""
    ev = ev.copy()
    for x, h in BARRERAS: ev["y%d" % x] = 0; ev["s%d" % x] = -1
    for k in (10, 30, 60): ev["mv%d" % k] = 0.0
    for s in sorted(ev["ses"].unique()):
        Y = barreras_sesion(s, T[s]); m = (ev["ses"] == s).to_numpy(); t = ev.loc[m, "t"].to_numpy(); p = T[s]["ultimo"].to_numpy("float64")
        for x, h in BARRERAS: ev.loc[m, "y%d" % x] = Y[(x, h)][0][t]; ev.loc[m, "s%d" % x] = Y[(x, h)][1][t]
        for k in (10, 30, 60): ev.loc[m, "mv%d" % k] = (p[np.minimum(t + k, len(p) - 1)] - p[t])
    return ev


# ------------------------------------------------------------------ variantes
def desagrupar(ev):
    """Minimo 60 s entre disparos dentro de cada sesion; gana el primero."""
    keep = []
    for s, g in ev.sort_values(["ses", "t"]).groupby("ses", sort=True):
        ult = -10**9
        for i, t in zip(g.index, g["t"].to_numpy()):
            if t - ult >= REFRACTARIO: keep.append(i); ult = t
    return ev.loc[keep]


def variante(ev, v, U, modelo=None):
    """Devuelve los eventos que disparan la variante v, con la columna 'lado' (+1 compra / -1 venta), ya desagrupados."""
    e = ev; d = e["dir"].to_numpy("float64")
    if v == "REF_toque_R":   m = e["tiene_REF"] == 1; lado = -d
    elif v == "MOV_toque_R": m = e["tiene_MOV"] == 1; lado = -d
    elif v == "RED_toque_R": m = e["tiene_RED"] == 1; lado = -d
    elif v == "CONF_R":      m = e["conf"] == 1; lado = -d
    elif v == "ABS_R":       m = (e["a_niv30"] >= U["a_niv30_q67"]) & (e["a_niv30"] > 0) & (e["fd30"] > 0); lado = -d
    elif v == "ABS_PAS_R":   m = (e["a_niv30"] >= U["a_niv30_q67"]) & (e["a_niv30"] > 0) & (e["fd30"] > 0) & (e["pas30"] <= U["pas30_q33"]); lado = -d
    elif v == "SINABS_B":    m = (e["a_niv30"] <= U["a_niv30_q33"]) & (e["fd30"] > 0); lado = d
    elif v == "EMPUJE_B":    m = (e["fd60"] >= U["fd60_q67"]) & (e["bar30"] >= U["bar30_q67"]); lado = d
    elif v == "AGOTA_R":     m = (e["fd60"] <= U["fd60_q33"]) & (e["fd60"] < 0); lado = -d
    elif v == "PAS_sigue":   m = (e["pas30"] <= U["pas30_q33"]) | (e["pas30"] >= U["pas30_q67"]); lado = np.where(e["pas30"].to_numpy() <= U["pas30_q33"], -d, d)
    elif v == "MOV_div_R":   m = (e["tiene_MOV"] == 1) & (e["fd300"] <= U["fd300_mov_q33"]); lado = -d
    elif v == "MODELO":
        pr = e["p_modelo"].to_numpy(); m = (pr >= U["modelo_q80"]) | (pr <= U["modelo_q20"]); lado = np.where(pr >= U["modelo_q80"], -d, d)
        m = pd.Series(m, index=e.index) & e["p_modelo"].notna()
    else: raise ValueError(v)
    out = e[np.asarray(m)].copy(); out["lado"] = np.asarray(lado)[np.asarray(m)]
    return desagrupar(out)


RASGOS_MODELO = ["g_REF", "g_MOV", "conf", "fd30", "fd60", "fd300", "a_niv30", "a_op30", "pas30", "bar30", "bar_op30", "rompe30", "vel60", "vel300", "min_rueda", "z_vwap_dir"]


def matriz_modelo(ev):
    X = ev.copy(); X["g_REF"] = X["tiene_REF"]; X["g_MOV"] = ((X["tiene_MOV"] == 1) & (X["tiene_REF"] == 0)).astype(int)
    return X[RASGOS_MODELO].to_numpy("float64")


def juzgar_variante(e, x, nombre=""):
    y = e["y%d" % x].to_numpy(); return B.juzgar(y, e["lado"].to_numpy(), nombre, e["ses"].to_numpy(), x)


def t_dias(e, x):
    y = e["y%d" % x].to_numpy(); l = e["lado"].to_numpy(); ok = y != 0
    if ok.sum() == 0: return np.nan
    s = pd.Series((y[ok] * l[ok]) > 0).groupby(e["ses"].to_numpy()[ok]).mean()
    if len(s) < 3 or s.std(ddof=1) == 0: return np.nan
    return float((s.mean() - 0.5) / (s.std(ddof=1) / np.sqrt(len(s))))


def placebo_protocolo(T, e, x_h, n_sorteos=200, semilla=11):
    """Misma cantidad de disparos en segundos al azar de la misma sesion y media hora, mismos lados. Devuelve (media, desvio, arreglo)."""
    rng = np.random.default_rng(semilla); ses = e["ses"].to_numpy(); lado = e["lado"].to_numpy("float64")
    mh = ((e["sod"].to_numpy() - (13 * 3600 + 30 * 60)) // 1800).astype(int)
    pools = {}; Ys = {}
    for s in sorted(set(ses)):
        val, sod = validos(T[s]); Ys[s] = barreras_sesion(s, T[s])[x_h][0]; b = (sod - (13 * 3600 + 30 * 60)) // 1800
        for k in np.unique(mh[ses == s]): pools[(s, k)] = np.flatnonzero(val & (b == k))
    grupos = {(s, k): np.flatnonzero((ses == s) & (mh == k)) for (s, k) in pools}
    acs = np.empty(n_sorteos)
    for j in range(n_sorteos):
        ys = np.zeros(len(lado))
        for key, pool in pools.items():
            m = grupos[key]
            if len(pool) == 0: continue
            ys[m] = Ys[key[0]][rng.choice(pool, size=len(m), replace=len(pool) < len(m))]
        ok = ys != 0; acs[j] = float(((ys[ok] * lado[ok]) > 0).mean())
    return float(acs.mean()), float(acs.std(ddof=1)), acs


def cargar_umbrales():
    return json.load(open(UMBRALES, encoding="utf-8"))
