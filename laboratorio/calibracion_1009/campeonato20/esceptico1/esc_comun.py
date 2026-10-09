# -*- coding: utf-8 -*-
"""esc_comun.py — ESCEPTICO 1 del campeonato20 (09-10-2026). SOLO LECTURA. Modulo (la prioridad baja la pone el script que lo llama).

1) JUEZ PROPIO, escrito de cero desde la definicion pre-registrada del campeonato (NO importa juez_operador ni juez20), para reproducir
   numeros clave sin compartir codigo con el juez oficial:
     raya vigente en la vela i = la clave (t_i - desfase) o la ultima anterior hasta 90 min; rayas a <= 1 pt en el mismo minuto = una;
     pista = la misma raya si esta a <= 3 pts de una vista en los ultimos 30 min (asignacion codiciosa por distancia);
     LLEGADA: la vela i toca [L-2, L+2] y la i-1 no (misma L), sin hueco > 30 min, i dentro de la ventana; TECHO si la i-1 estaba toda
       debajo, PISO si toda encima; la misma pista no registra otra llegada hasta despues de la vela de decision;
     ACIERTO20: 20 pts a favor desde la raya antes de romperse (vela de llegada: el toque es la punta 'mas alla'; si la vela va
       primero a favor y despues toca, solo cuenta el cierre). ROTA (TOLERANTE): cierre mas alla de L+5 con cuerpo >= 4, o 3 cierres
       seguidos mas alla de L, o > 10 min seguidos con cierres mas alla. INDEFINIDA: 120 min sin decidir. CENSURADA: fin de datos/hueco.
2) Cargadores de las rayas, velas, sesiones y ventanas de cada familia, tal como las usaron (para correr sobre el MISMO insumo).
"""
import json
import math
import os
import sys

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
C20 = os.path.normpath(os.path.join(AQUI, ".."))
NDX = os.path.join(C20, "ndx")
NQD = os.path.join(C20, "familia_nq")
CONF = os.path.join(C20, "familia_conf")
QQQ = os.path.join(C20, "qqq20")
J20DIR = os.path.join(C20, "juez20")

TOL, GIRO, VENT_MIN, ARRASTRE, FUSION, IDENT, MEMORIA, HUECO = 2.0, 20.0, 120, 90, 1.0, 3.0, 30, 30


# ======================================================================================== juez propio
def _min(x):
    return int(pd.Timestamp(x).value // 60_000_000_000)


def preparar_velas(V):
    df = pd.DataFrame(V)[["t", "o", "h", "l", "c"]].copy()
    df["t"] = pd.to_datetime(df["t"]).dt.floor("min")
    df = df.dropna().sort_values("t").drop_duplicates("t", keep="last").reset_index(drop=True)
    tm = df["t"].to_numpy().astype("datetime64[m]").astype(np.int64)
    return dict(tm=tm, o=df["o"].to_numpy(float), h=df["h"].to_numpy(float), l=df["l"].to_numpy(float), c=df["c"].to_numpy(float),
                noche=(df["t"] + pd.Timedelta(hours=2)).dt.strftime("%Y-%m-%d").to_numpy(), n=len(df))


def _precios(val, claves):
    if not val:
        return []
    it = [(k, val.get(k)) for k in claves] if claves else list(val.items())
    out = []
    for _, x in it:
        if isinstance(x, (tuple, list)):
            x = x[0]
        try:
            x = float(x)
        except (TypeError, ValueError):
            continue
        if math.isfinite(x) and x != 0.0:
            out.append(x)
    return out


def pistas(V, rayas, desfase=0, claves=None, corrimiento=0.0):
    """-> lista de pistas: (indices de vela, valores). Vigente en la vela i = clave t_i - desfase (arrastre 90 min)."""
    items = sorted(((_min(k), v) for k, v in rayas.items()), key=lambda z: z[0])
    km = np.array([k for k, _ in items], np.int64)
    vals = [v for _, v in items]
    tm = V["tm"]
    P = []
    activas = {}
    cache = {}
    for i in range(V["n"]):
        obj = tm[i] - desfase
        j = int(np.searchsorted(km, obj, side="right")) - 1 if len(km) else -1
        if j < 0 or obj - km[j] > ARRASTRE:
            lin = []
        else:
            if j not in cache:
                fus = []
                for x in _precios(vals[j], claves):
                    if all(abs(x - f) > FUSION for f in fus):
                        fus.append(x)
                cache = {j: fus}
            lin = cache[j]
        for p in [p for p, (_, ut) in activas.items() if tm[i] - ut > MEMORIA]:
            activas.pop(p)
        pares = sorted((abs(v0 - x), p, q) for q, x in enumerate(lin) for p, (v0, _) in activas.items() if abs(v0 - x) <= IDENT)
        usados, asig = set(), {}
        for _, p, q in pares:
            if p in usados or q in asig:
                continue
            usados.add(p); asig[q] = p
        for q, x in enumerate(lin):
            p = asig.get(q)
            if p is None:
                p = len(P); P.append(([], []))
            P[p][0].append(i); P[p][1].append(x + corrimiento)
            activas[p] = (x, tm[i])
    return [(np.array(a, np.int64), np.array(b, float)) for a, b in P]


def _decidir(V, i, L, s):
    """-> (resultado, k_fin): 'acierto' | 'falsa' | 'indefinida' | 'censurada'."""
    tm, o, h, l, c, n = V["tm"], V["o"], V["h"], V["l"], V["c"], V["n"]
    run = 0; t0 = None
    k = i
    while True:
        if k >= n:
            return "censurada", n - 1
        if k > i and tm[k] - tm[k - 1] > HUECO:
            return "censurada", k - 1
        if tm[k] > tm[i] + VENT_MIN:
            return "indefinida", k - 1
        sube = c[k] >= o[k]
        if s > 0:          # techo: a favor = abajo
            fav = L - l[k]; mas = c[k] - L
            if k == i and sube:          # o->l->h->c: el minimo fue ANTES del toque (maximo): solo cuenta el cierre
                fav = L - c[k]
        else:              # piso: a favor = arriba
            fav = h[k] - L; mas = L - c[k]
            if k == i and not sube:      # o->h->l->c: el maximo fue ANTES del toque (minimo)
                fav = c[k] - L
        if fav >= GIRO:
            return "acierto", k
        if mas > 0:
            run += 1
            if run == 1:
                t0 = tm[k]
        else:
            run = 0
        if mas > 5.0 and abs(c[k] - o[k]) >= 4.0:
            return "falsa", k
        if run >= 3:
            return "falsa", k
        if run > 0 and tm[k] - t0 + 1 > 10:
            return "falsa", k
        k += 1


def juzgar(V, rayas, ventanas, desfase=0, claves=None, corrimiento=0.0, tipo=None, P=None, off_vela=None):
    """Juez propio. -> (resumen, eventos). tipo: None | 'techo' | 'piso' (filtra llegadas). off_vela: corrimiento por vela (placebo)."""
    tm, h, l = V["tm"], V["h"], V["l"]
    env = np.zeros(V["n"], bool)
    for a, b in ventanas:
        env |= (tm >= _min(a)) & (tm < _min(b))
    gap = np.ones(V["n"], bool); gap[1:] = (tm[1:] - tm[:-1]) > HUECO
    if P is None:
        P = pistas(V, rayas, desfase, claves)
    evs = []
    for pid, (idx, val) in enumerate(P):
        val = val + corrimiento
        if off_vela is not None:
            val = val + off_vela[idx]
        m = idx >= 1
        idx, val = idx[m], val[m]
        toca = (h[idx] >= val - TOL) & (l[idx] <= val + TOL)
        prev = (h[idx - 1] >= val - TOL) & (l[idx - 1] <= val + TOL)
        cand = np.flatnonzero(toca & ~prev & ~gap[idx] & env[idx])
        ocupado = -1
        for q in cand:
            i = int(idx[q])
            if i <= ocupado:
                continue
            L = float(val[q])
            s = 1 if h[i - 1] < L - TOL else -1
            res, kf = _decidir(V, i, L, s)
            ocupado = kf
            tp = "techo" if s > 0 else "piso"
            if tipo and tp != tipo:
                continue
            evs.append(dict(i=i, t=pd.Timestamp(int(tm[i]) * 60, unit="s"), noche=V["noche"][i], raya=L, tipo=tp, res=res, pista=pid))
    return resumir(evs), evs


def resumir(evs):
    n = len(evs)
    cen = sum(e["res"] == "censurada" for e in evs)
    ac = sum(e["res"] == "acierto" for e in evs)
    fa = sum(e["res"] == "falsa" for e in evs)
    ind = sum(e["res"] == "indefinida" for e in evs)
    base = n - cen
    return dict(llegadas=n, censuradas=cen, aciertos=ac, falsas=fa, indefinidas=ind,
                pct_acierto20=100.0 * ac / base if base else float("nan"))


# ======================================================================================== familia NDX
def ndx_cargar():
    V = pd.read_csv(os.path.join(NDX, "datos", "velas_ndx.csv"), parse_dates=["t"])[["t", "o", "h", "l", "c"]]
    series = {}
    import glob
    for p in sorted(glob.glob(os.path.join(NDX, "datos", "series", "ndx_*.pkl"))):
        x = pd.read_pickle(p)
        for s, d in x["series"].items():
            series.setdefault(s, {}).update(d)
    return V, series


def particion(ses):
    ns = sorted(ses)
    k = int(math.ceil(len(ns) / 3.0))
    return ns[:len(ns) - k], ns[len(ns) - k:]


def ndx_sesiones(ventana):
    g = pd.read_csv(os.path.join(NDX, "datos", "juez", "sesiones_%s.csv" % ventana), dtype={"sesion": str})
    ses = sorted(g.loc[g["entra"], "sesion"].tolist())
    return particion(ses)


def ventanas_noche(ses, a=-2.0, b=13.5):
    return [(pd.Timestamp(d) + pd.Timedelta(hours=a), pd.Timestamp(d) + pd.Timedelta(hours=b)) for d in sorted(ses)]


def ventanas_rueda(ses):
    return ventanas_noche(ses, 13.5, 20.0)


# ======================================================================================== familia NQ
def nq_cargar():
    V = pd.read_csv(os.path.join(NQD, "datos", "velas_m1_nq.csv"), parse_dates=["t"])
    info = json.load(open(os.path.join(NQD, "datos", "nq01_info.json"), encoding="utf-8"))
    reg = {d: pd.read_pickle(os.path.join(NQD, "datos", "registros-%s.pkl" % d)) for d in info["sesiones"]}
    part = json.load(open(os.path.join(NQD, "datos", "particion.json"), encoding="utf-8"))
    return V, reg, part


def nq_rayas_y_ventanas(reg, dias, serie, vt):
    """Igual criterio que nq02_juez.trabajo: tramos con libro (y OI valido si es serie por OI), huecos <= 10 min unidos; rayas fuera
    de los tramos = clave vacia."""
    VENT = {"noche": (-2.0, 13.5), "rueda": (13.5, 20.0)}
    fuente = "oi" if "_oi" in serie else "vol"
    base, _, lado = serie.partition(".")
    rol = {"C": "D1", "P": "D2"}.get(lado)
    vent = []
    cub = set()
    for d in dias:
        a, b = VENT[vt]
        D0 = pd.Timestamp(d)
        ka = _min(D0 + pd.Timedelta(hours=a)); kb = _min(D0 + pd.Timedelta(hours=b))
        ks = sorted(r["k"] for r in reg[d] if ka <= r["k"] < kb and r["libro"] and (fuente == "vol" or r["oi_ok"]))
        if not ks:
            continue
        tr = [[ks[0], ks[0]]]
        for k in ks[1:]:
            if k - tr[-1][1] <= 10:
                tr[-1][1] = k
            else:
                tr.append([k, k])
        for x, y in tr:
            vent.append((pd.Timestamp(x * 60, unit="s"), pd.Timestamp((y + 1) * 60, unit="s")))
            cub.update(range(x, y + 1))
    rayas = {}
    for d in dias:
        for r in reg[d]:
            dd = {}
            if r["k"] in cub:
                for p, e, K in r["series"].get(base, []):
                    if rol and e != rol:
                        continue
                    dd["%s.%s" % (base, e)] = (float(p), None)
            rayas[pd.Timestamp(r["k"] * 60, unit="s")] = dd
    return rayas, vent


# ======================================================================================== familia CONF
def conf_cargar(serie):
    V = pd.read_pickle(os.path.join(CONF, "datos", "velas_familia.pkl"))
    R = pd.read_pickle(os.path.join(CONF, "datos", "rayas", "%s.pkl" % serie))
    return V, R


def conf_noches(V, R, ventana):
    a, b = {"noche": (-2.0, 13.5), "rueda": (13.5, 20.0)}[ventana]
    con = set()
    for t, v in R.items():
        if not v:
            continue
        d = (pd.Timestamp(t) + pd.Timedelta(hours=2)).strftime("%Y-%m-%d")
        N = pd.Timestamp(d)
        if N + pd.Timedelta(hours=a) <= t < N + pd.Timedelta(hours=b):
            con.add(d)
    out = []
    tv = V["t"]
    for d in sorted(con):
        N = pd.Timestamp(d)
        if int(((tv >= N + pd.Timedelta(hours=a)) & (tv < N + pd.Timedelta(hours=b))).sum()) >= 60:
            out.append(d)
    return out


def conf_parte(V, R, ventana, parte):
    noches = conf_noches(V, R, ventana)
    mirar, prueba = particion(noches)
    sel = mirar if parte == "MIRAR" else prueba
    ss = set(sel)
    Rs = {t: v for t, v in R.items() if (pd.Timestamp(t) + pd.Timedelta(hours=2)).strftime("%Y-%m-%d") in ss}
    Vs = V[V["noche"].isin(ss)].reset_index(drop=True)
    a, b = {"noche": (-2.0, 13.5), "rueda": (13.5, 20.0)}[ventana]
    return Vs, Rs, ventanas_noche(sel, a, b), sel


def j20():
    if J20DIR not in sys.path:
        sys.path.insert(0, J20DIR)
    import juez20 as J
    return J
