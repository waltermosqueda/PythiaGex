# -*- coding: utf-8 -*-
"""CUANTO DEL ACIERTO DE A3 ES GEOMETRIA (27-11-2026, mio): para cada operacion de A3 (las mias de a3_propio_ops_m5.csv en MES y
a3_propio_ops_espera1.csv en la cinta), 200 caminos donde cada vela de 5 min posterior se da vuelta o no al azar alrededor del cierre
previo (mismo volumen; VWAP recalculado con (max+min)/2), con las mismas salidas de A3 en velas (stop fijo, objetivo al VWAP del cierre
previo al tick, 1 tick mas alla, misma vela = stop, hueco = apertura, 16:00 = cierre). Uso: python -I azar_a3_propio.py"""
import sys, os, glob, json, math
sys.stdout.reconfigure(encoding="utf-8")
import numpy as np, pandas as pd
TICK = 0.25; N = 200; rng = np.random.default_rng(271126)
AQUI = os.path.dirname(os.path.abspath(__file__))
def al_tick(x): return np.floor(np.asarray(x) / TICK + 0.5 + 1e-9) * TICK

def azar(lado, entrada, stop, vw0, cv0, cpv0, O, H, L, C, V):
    n = len(H)
    if n == 0: return np.zeros(N), np.zeros(N)
    cprev = np.concatenate([[entrada], C[:-1]])
    dO, dH, dL, dC = O - cprev, H - cprev, L - cprev, C - cprev
    s = rng.choice([-1.0, 1.0], size=(N, n))
    rO = s * dO; rH = np.where(s > 0, dH, -dL); rL = np.where(s > 0, dL, -dH); rC = s * dC
    base = entrada + np.concatenate([np.zeros((N, 1)), np.cumsum(rC, axis=1)[:, :-1]], axis=1)
    o, h, l, c = base + rO, base + rH, base + rL, base + rC
    cpv = cpv0 + np.cumsum((h + l) / 2 * V, axis=1); cv = cv0 + np.cumsum(np.broadcast_to(V, (N, n)), axis=1)
    vprev = np.concatenate([np.full((N, 1), vw0), (cpv / cv)[:, :-1]], axis=1); Lt = al_tick(vprev)
    if lado < 0: st = h >= stop; tg = l <= Lt - TICK
    else: st = l <= stop; tg = h >= Lt + TICK
    cual = st | tg; hay = cual.any(axis=1); j = np.argmax(cual, axis=1); f = np.arange(N)
    es_stop = st[f, j]
    gap = (o[f, j] - stop) * (-lado) >= 0
    p_stop = np.where(gap, o[f, j], stop)
    salida = np.where(~hay, c[:, -1], np.where(es_stop, p_stop, Lt[f, j]))
    pts = (salida - entrada) * lado
    return (pts > 0).astype(float), pts

def barras_m5():
    V_ = r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\dom\ronda3\velas\velas-MES-m5.csv"
    d = pd.read_csv(V_, usecols=["t", "o", "h", "l", "c", "vol"])
    t = pd.to_datetime(d["t"]).dt.tz_localize("UTC").dt.tz_convert("America/New_York")
    d["dia"] = t.dt.strftime("%Y-%m-%d"); d["min"] = t.dt.hour * 60 + t.dt.minute
    r = d[(d["min"] >= 570) & (d["min"] < 960)]
    return {x: g.reset_index(drop=True) for x, g in r.groupby("dia") if len(g) == 78}

def correr(ops, barras, nombre, tramos):
    gana, pts_az, pf = [], [], []
    for _, op in ops.iterrows():
        g = barras[op.fecha]; o, h, l, c, v = (g[k].to_numpy(float) for k in ("o", "h", "l", "c", "vol"))
        k = (int(op.hora[:2]) * 60 + int(op.hora[3:]) - 570) // 5 - 1
        mid = (h + l) / 2; cpv = np.cumsum(mid * v); cv = np.cumsum(v)
        lado = op.lado if isinstance(op.lado, (int, float, np.integer, np.floating)) else (-1 if op.lado == "VENTA" else 1)
        ga, pa = azar(lado, op.entrada, op.stop, cpv[k] / cv[k], cv[k], cpv[k], o[k + 1:], h[k + 1:], l[k + 1:], c[k + 1:], v[k + 1:])
        gana.append(ga.mean()); pts_az.append(pa.mean())
        dS = abs(op.stop - op.entrada); dV = abs(op.entrada - cpv[k] / cv[k]); pf.append(dS / (dS + dV))
    ops = ops.assign(az_gana=gana, az_pts=pts_az, Pf=pf)
    for nom, a, b in tramos:
        q = ops[(ops.fecha >= a) & (ops.fecha <= b)]
        print("%-22s ops %3d | acierto real %.1f %% | azar %.1f %% | formula P %.1f %% | neto real %+.2f | neto azar %+.2f | ventaja %+.2f pts/op"
              % (nombre + " " + nom, len(q), 100 * (q.pts > 0).mean(), 100 * q.az_gana.mean(), 100 * q.Pf.mean(), (q.pts - 1).mean(), (q.az_pts - 1).mean(), (q.pts - q.az_pts).mean()))
    return ops

b5 = barras_m5()
m = pd.read_csv(os.path.join(AQUI, "a3_propio_ops_m5.csv"))
correr(m, b5, "MES", (("diseno", "2025-09-09", "2026-02-27"), ("chequeo", "2026-03-02", "2026-08-31")))

# cinta: velas de 5 min armadas desde las ordenes (VWAP de la vela con (primero+ultimo)/2 de cada orden, como en la regla)
FLUJO = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex", "flujo")
porfecha = {}
for f in glob.glob(os.path.join(FLUJO, "cinta-ES*.csv")):
    hasta = os.path.basename(f)[:-4].split("-")[-1]
    dia = pd.Timestamp(hasta[:8] + "T" + hasta[8:], tz="UTC").tz_convert("America/New_York").strftime("%Y-%m-%d")
    clave = (os.path.exists(f[:-4] + ".listo"), hasta)
    if dia not in porfecha or clave > porfecha[dia][0]: porfecha[dia] = (clave, f)
mc = pd.read_csv(os.path.join(AQUI, "a3_propio_ops_espera1.csv"))
bc = {}
for dia in sorted(set(mc.fecha)):
    df = pd.read_csv(porfecha[dia][1], usecols=["t", "primero", "ultimo", "vol"])
    t = pd.to_datetime(df["t"], format="%Y-%m-%dT%H:%M:%S.%f").dt.tz_localize("UTC").dt.tz_convert("America/New_York")
    ini = pd.Timestamp(dia + " 09:30:00", tz="America/New_York")
    s = (t - ini).dt.total_seconds(); mk = (s >= 0) & (s < 23400)
    x = df[mk].copy(); x["b"] = (s[mk] // 300).astype(int)
    x["lo"] = x[["primero", "ultimo"]].min(axis=1); x["hi"] = x[["primero", "ultimo"]].max(axis=1)
    g = x.groupby("b").agg(o=("primero", "first"), h=("hi", "max"), l=("lo", "min"), c=("ultimo", "last"), vol=("vol", "sum")).reindex(range(78))
    g = g.ffill()
    bc[dia] = g.reset_index(drop=True)
correr(mc, bc, "CINTA", (("todo", "2026-09-10", "2026-11-25"),))
