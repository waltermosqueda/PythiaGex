# -*- coding: utf-8 -*-
"""EL ELASTICO POR FUERA (27-11-2026): en cada cierre de vela de 5 min de MES de 10:00 a 15:30 (242 ruedas, 09-09-2025 a 31-08-2026),
que llega primero: E = extremo del dia del lado del precio + X/2 (fijo) o V = el VWAP (que se mueve: en cada vela se usa el del cierre
previo). Misma vela = E (regla conservadora). Nada a las 16:00 = 'ninguna'. Tres referencias mias: (1) la formula de dos metas fijas
d_V/(d_E+d_V); (2) azar por REFLEXION (cada vela posterior se da vuelta o no al azar alrededor del cierre previo, mismo volumen, VWAP
recalculado); (3) azar por PERMUTACION (las velas posteriores en otro orden al azar). 200 caminos por punto. Uso: python -I elastico_propio.py"""
import sys, math
sys.stdout.reconfigure(encoding="utf-8")
import numpy as np, pandas as pd
TICK = 0.25; N = 200; rng = np.random.default_rng(20261127)
V = r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\dom\ronda3\velas\velas-MES-m5.csv"
def al_tick(x): return math.floor(x / TICK + 0.5 + 1e-9) * TICK
d = pd.read_csv(V, usecols=["t", "o", "h", "l", "c", "vol"])
t = pd.to_datetime(d["t"]).dt.tz_localize("UTC").dt.tz_convert("America/New_York")
d["dia"] = t.dt.strftime("%Y-%m-%d"); d["min"] = t.dt.hour * 60 + t.dt.minute
rth = d[(d["min"] >= 570) & (d["min"] < 960)]
ruedas = {x: g.reset_index(drop=True) for x, g in rth.groupby("dia") if len(g) == 78}
dias = sorted(ruedas); rango = {x: ruedas[x].h.max() - ruedas[x].l.min() for x in dias}

def carrera(arriba, E, vw0, cv0, cpv0, h, l, c, v, c_prev):
    """h,l,c: (caminos, nvelas). Devuelve codigo por camino: 1 = E primero, 2 = V primero, 0 = ninguna."""
    mid = (h + l) / 2
    cpv = cpv0 + np.cumsum(mid * v, axis=1); cv = cv0 + np.cumsum(v, axis=1)
    vw = cpv / cv
    vprev = np.concatenate([np.full((h.shape[0], 1), vw0), vw[:, :-1]], axis=1)  # VWAP del cierre previo a cada vela
    if arriba: hitE = h >= E; hitV = l <= vprev
    else: hitE = l <= E; hitV = h >= vprev
    cualq = hitE | hitV
    hay = cualq.any(axis=1); j = np.argmax(cualq, axis=1)
    fila = np.arange(h.shape[0])
    eE = hitE[fila, j]
    return np.where(~hay, 0, np.where(eE, 1, 2))

res = {"real": [], "formula": [], "reflejo": [], "permuta": []}; franja = []
for n, dia in enumerate(dias):
    if not ("2025-09-09" <= dia <= "2026-08-31") or n < 10: continue
    X = al_tick(0.08 * float(np.median([rango[x] for x in dias[n - 10:n]])))
    g = ruedas[dia]; o, h, l, c, v = (g[k].to_numpy(float) for k in ("o", "h", "l", "c", "vol"))
    mid = (h + l) / 2; cpv = np.cumsum(mid * v); cv = np.cumsum(v); vw = cpv / cv
    dh = np.maximum.accumulate(h); dl = np.minimum.accumulate(l)
    for k in range(5, 60):  # cierres 10:00 .. 15:30
        px = c[k]; dv = px - vw[k]
        if dv == 0 or k == 77: continue
        arriba = dv > 0
        E = dh[k] + al_tick(X / 2) if arriba else dl[k] - al_tick(X / 2)
        dE = abs(E - px); dV = abs(dv)
        res["formula"].append(dV / (dE + dV))
        H, Lw, C, Vv = h[k + 1:], l[k + 1:], c[k + 1:], v[k + 1:]
        r = carrera(arriba, E, vw[k], cv[k], cpv[k], H[None], Lw[None], C[None], Vv[None], px)[0]
        res["real"].append(r)
        # movimientos relativos al cierre previo de cada vela posterior
        cprev = np.concatenate([[px], C[:-1]])
        dH, dL, dC = H - cprev, Lw - cprev, C - cprev
        # (2) reflexion: signo al azar por vela
        s = rng.choice([-1.0, 1.0], size=(N, len(H)))
        rH = np.where(s > 0, dH, -dL); rL = np.where(s > 0, dL, -dH); rC = s * dC
        base = px + np.concatenate([np.zeros((N, 1)), np.cumsum(rC, axis=1)[:, :-1]], axis=1)
        res["reflejo"].append(carrera(arriba, E, vw[k], cv[k], cpv[k], base + rH, base + rL, base + rC, np.broadcast_to(Vv, (N, len(H))), px))
        # (3) permutacion del orden de las velas (cada vela con su volumen)
        idx = np.argsort(rng.random((N, len(H))), axis=1)
        pH, pL, pC, pV = dH[idx], dL[idx], dC[idx], Vv[idx]
        base = px + np.concatenate([np.zeros((N, 1)), np.cumsum(pC, axis=1)[:, :-1]], axis=1)
        res["permuta"].append(carrera(arriba, E, vw[k], cv[k], cpv[k], base + pH, base + pL, base + pC, pV, px))
        franja.append(570 + 5 * (k + 1))
real = np.array(res["real"]); refl = np.stack(res["reflejo"]); perm = np.stack(res["permuta"]); form = np.array(res["formula"]); fr = np.array(franja)
print("puntos: %d (ruedas evaluadas %d)" % (len(real), len(real) // 55))
def linea(nom, m):
    print("%-26s puntos %5d | E primero: real %.1f  reflejo %.1f  permuta %.1f  formula %.1f | V primero: real %.1f  reflejo %.1f  permuta %.1f | ninguna real %.1f reflejo %.1f"
          % (nom, m.sum(), 100 * (real[m] == 1).mean(), 100 * (refl[m] == 1).mean(), 100 * (perm[m] == 1).mean(), 100 * form[m].mean(),
             100 * (real[m] == 2).mean(), 100 * (refl[m] == 2).mean(), 100 * (perm[m] == 2).mean(), 100 * (real[m] == 0).mean(), 100 * (refl[m] == 0).mean()))
linea("todo el dia 10:00-15:30", np.ones(len(real), bool))
linea("manana 10:00-11:30", (fr >= 600) & (fr <= 690))
linea("mediodia 11:35-13:00", (fr > 690) & (fr <= 780))
linea("tarde 13:05-15:30", fr > 780)
