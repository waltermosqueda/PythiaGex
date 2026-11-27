# -*- coding: utf-8 -*-
"""LA ETIQUETA CIRCULAR, POR FUERA (27-11-2026, mio, sin el codigo de la R25): ¿A3 "pierde en los dias de tendencia" porque el
mercado tiene esa debilidad, o porque el dia se llama "de tendencia" mirando la misma tarde que decide la operacion?
Para cada operacion de A3 (mias: a3_propio_ops_m5.csv en MES 5 min y ronda24 ops_prueba_2024_25.csv en MES 30 min), 200 tardes al azar
(cada vela posterior a la entrada reflejada o no al azar alrededor del cierre previo, mismo volumen, VWAP recalculado, mismas salidas de
A3). A CADA TARDE AL AZAR se le pone su propia etiqueta ex post con la manana real + esa tarde: tendencia si |cierre 16:00 - apertura| >=
0,6 del rango, balance si < 0,3, mixto en el medio (la misma regla que use en la R24). Se compara el acierto por tipo de dia: real vs azar.
Uso: python -I circular_propio.py"""
import sys, os, math
sys.stdout.reconfigure(encoding="utf-8")
import numpy as np, pandas as pd
TICK = 0.25; N = 200; rng = np.random.default_rng(20261127)
AQUI = os.path.dirname(os.path.abspath(__file__))
LAB = os.path.dirname(os.path.dirname(AQUI))
VELAS = os.path.join(LAB, "dom", "ronda3", "velas")
def al_tick_v(x): return np.floor(np.asarray(x) / TICK + 0.5 + 1e-9) * TICK

def ruedas(archivo, nb):
    d = pd.read_csv(os.path.join(VELAS, archivo), usecols=["t", "o", "h", "l", "c", "vol"])
    t = pd.to_datetime(d["t"]).dt.tz_localize("UTC").dt.tz_convert("America/New_York")
    d["dia"] = t.dt.strftime("%Y-%m-%d"); d["min"] = t.dt.hour * 60 + t.dt.minute
    r = d[(d["min"] >= 570) & (d["min"] < 960)]
    return {x: g.reset_index(drop=True) for x, g in r.groupby("dia") if len(g) == nb}

def tipo(f): return np.where(f >= 0.6, "tendencia", np.where(f < 0.3, "balance", "mixto"))

def una(g, k, lado, entrada, stop):
    o, h, l, c, v = (g[x].to_numpy(float) for x in ("o", "h", "l", "c", "vol"))
    mid = (h + l) / 2; cpv0 = np.cumsum(mid * v)[k]; cv0 = np.cumsum(v)[k]; vw0 = cpv0 / cv0
    O, H, L, C, V = o[k + 1:], h[k + 1:], l[k + 1:], c[k + 1:], v[k + 1:]; n = len(H)
    cprev = np.concatenate([[c[k]], C[:-1]])
    dO, dH, dL, dC = O - cprev, H - cprev, L - cprev, C - cprev
    s = rng.choice([-1.0, 1.0], size=(N, n))
    rO = s * dO; rH = np.where(s > 0, dH, -dL); rL = np.where(s > 0, dL, -dH); rC = s * dC
    base = c[k] + np.concatenate([np.zeros((N, 1)), np.cumsum(rC, axis=1)[:, :-1]], axis=1)
    so, sh, sl, sc = base + rO, base + rH, base + rL, base + rC
    cpv = cpv0 + np.cumsum((sh + sl) / 2 * V, axis=1); cv = cv0 + np.cumsum(np.broadcast_to(V, (N, n)), axis=1)
    vprev = np.concatenate([np.full((N, 1), vw0), (cpv / cv)[:, :-1]], axis=1); Lt = al_tick_v(vprev)
    if lado < 0: st = sh >= stop; tg = sl <= Lt - TICK
    else: st = sl <= stop; tg = sh >= Lt + TICK
    cual = st | tg; hay = cual.any(axis=1); j = np.argmax(cual, axis=1); f = np.arange(N)
    gap = (so[f, j] - stop) * (-lado) >= 0
    salida = np.where(~hay, sc[:, -1], np.where(st[f, j], np.where(gap, so[f, j], stop), Lt[f, j]))
    pts = (salida - entrada) * lado
    # etiqueta ex post de CADA tarde al azar: manana real (velas 0..k) + tarde simulada
    hi = np.maximum(h[:k + 1].max(), sh.max(axis=1)); lo = np.minimum(l[:k + 1].min(), sl.min(axis=1))
    fr = np.abs(sc[:, -1] - o[0]) / np.maximum(hi - lo, 1e-9)
    # etiqueta REAL de ese dia
    fr_real = abs(c[-1] - o[0]) / (h.max() - l.min())
    return pts, tipo(fr), str(tipo(np.array([fr_real]))[0])

def correr(nombre, ops, rr, paso):
    real = {"tendencia": [], "mixto": [], "balance": []}; az = {"tendencia": [], "mixto": [], "balance": []}
    for _, op in ops.iterrows():
        g = rr[op.fecha]
        k = int(op.k) if "k" in op and not pd.isna(op.get("k", np.nan)) else (int(op.hora[:2]) * 60 + int(op.hora[3:]) - 570) // paso - 1
        lado = int(op.lado)
        pts, tp, treal = una(g, k, lado, float(op.entrada), float(op.stop))
        real[treal].append(float(op.pts))
        for t_ in ("tendencia", "mixto", "balance"): az[t_].extend(pts[tp == t_].tolist())
    print("== %s (%d operaciones)" % (nombre, len(ops)))
    for t_ in ("tendencia", "mixto", "balance"):
        r = np.array(real[t_]); a = np.array(az[t_])
        print("   %-9s real: %3d ops, acierto %5.1f %%, neto %+.2f | azar: %5.1f %% de las tardes, acierto %5.1f %%, neto %+.2f | brecha real-azar %+.1f pts de acierto"
              % (t_, len(r), 100 * (r > 0).mean() if len(r) else float("nan"), (r - 1).mean() if len(r) else float("nan"),
                 100 * len(a) / sum(len(x) for x in az.values()), 100 * (a > 0).mean(), (a - 1).mean(),
                 100 * ((r > 0).mean() - (a > 0).mean()) if len(r) else float("nan")))
    rt, rb = np.array(real["tendencia"]), np.array(real["balance"]); at, ab = np.array(az["tendencia"]), np.array(az["balance"])
    print("   distancia balance - tendencia en acierto: real %.1f pts, azar %.1f pts" % (100 * ((rb > 0).mean() - (rt > 0).mean()), 100 * ((ab > 0).mean() - (at > 0).mean())))

m5 = ruedas("velas-MES-m5.csv", 78)
o5 = pd.read_csv(os.path.join(AQUI, "a3_propio_ops_m5.csv"))
correr("A3 en MES 5 min, diseno + chequeo (mias)", o5[(o5.fecha >= "2025-09-09") & (o5.fecha <= "2026-08-31")], m5, 5)
m30 = ruedas("velas-MES-m30.csv", 13)
o30 = pd.read_csv(os.path.join(LAB, "ronda24_a3_30min", "ops_prueba_2024_25.csv"))
correr("A3-30 en MES 30 min, 2024-25 (R24)", o30, m30, 30)
