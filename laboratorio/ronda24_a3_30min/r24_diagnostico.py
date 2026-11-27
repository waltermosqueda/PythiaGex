# -*- coding: utf-8 -*-
"""Por que A3-30 se aparta de A3 en el chequeo (2025-26, datos ya vistos; solo fidelidad): (1) A3 en 5 min mirando SOLO los 6 cierres
de :00/:30 (salidas en 5 min) -> efecto de mirar menos; (2) entradas de A3-30 (velas de 30) con las salidas resueltas en velas de 5
min -> efecto de la resolucion de la salida; (3) entradas de A3-30 con salida en 30 min pero la regla de camino O-H-L-C (vela que
cierra arriba: O->L->H->C; abajo: O->H->L->C) en vez de 'misma vela = stop'."""
import sys, os, math
sys.stdout.reconfigure(encoding="utf-8")
import numpy as np, pandas as pd
sys.path.insert(0, r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\ronda24_a3_30min")
import r24_a3_30 as R
TICK = 0.25
def al_tick(x): return math.floor(x / TICK + 0.5 + 1e-9) * TICK
r5 = R.rth("velas-MES-m5.csv")
r5d = {d: g.reset_index(drop=True) for d, g in r5.groupby("dia") if len(g) == 78}
a30 = R.ruedas30_desde5()
dias = sorted(r5d); rango = {x: r5d[x].h.max() - r5d[x].l.min() for x in dias}

def a3_5(solo30):
    ops = []
    for n, dia in enumerate(dias):
        if n < 10 or not ("2025-09-09" <= dia <= "2026-08-31"): continue
        X = al_tick(0.08 * float(np.median([rango[x] for x in dias[n - 10:n]])))
        g = r5d[dia]; o, h, l, c, v = (g[k].to_numpy(float) for k in ("o", "h", "l", "c", "vol"))
        mid = (h + l) / 2; vw = np.cumsum(mid * v) / np.cumsum(v); dh = np.maximum.accumulate(h); dl = np.minimum.accumulate(l)
        ibh, ibl = h[:12].max(), l[:12].min(); ibw = ibh - ibl; sig = 0; nd = 0
        for k in range(78):
            cierre = 570 + 5 * (k + 1)
            if not (780 <= cierre <= 930) or k < sig or nd >= 5: continue
            if solo30 and cierre % 30 != 0: continue
            px = c[k]; dv = px - vw[k]
            if abs(dv) < 1.5 * X: continue
            lado = -1 if dv > 0 else 1
            if lado == -1: ext = max(0.0, dh[k] - ibh) / ibw; stop = dh[k] + al_tick(X / 2)
            else: ext = max(0.0, ibl - dl[k]) / ibw; stop = dl[k] - al_tick(X / 2)
            if ext > 1: continue
            ds = abs(stop - px); P = ds / (ds + abs(dv))
            if P < 0.55: continue
            j, sp, mot = salida5(o, h, l, c, vw, k, lado, stop)
            ops.append(dict(fecha=dia, pts=(sp - px) * lado, motivo=mot)); nd += 1
            sig = j + 1 if not solo30 else j + 1
    return pd.DataFrame(ops)

def salida5(o, h, l, c, vw, k, lado, stop):
    for j in range(k + 1, 78):
        L = al_tick(vw[j - 1])
        if lado == -1: st = h[j] >= stop; tg = l[j] <= L - TICK; gap = o[j] >= stop
        else: st = l[j] <= stop; tg = h[j] >= L + TICK; gap = o[j] <= stop
        if st: return j, (o[j] if gap else stop), "stop"
        if tg: return j, L, "objetivo"
    return 77, c[77], "fin"

def a3_30(modo):
    ops = []
    for n, dia in enumerate(dias):
        if n < 10 or not ("2025-09-09" <= dia <= "2026-08-31"): continue
        X = al_tick(0.08 * float(np.median([rango[x] for x in dias[n - 10:n]])))
        g = a30[dia]; o, h, l, c, v = (g[k].to_numpy(float) for k in ("o", "h", "l", "c", "vol"))
        g5 = r5d[dia]; o5, h5, l5, c5, v5 = (g5[k].to_numpy(float) for k in ("o", "h", "l", "c", "vol"))
        mid5 = (h5 + l5) / 2; vw5 = np.cumsum(mid5 * v5) / np.cumsum(v5)
        mid = (h + l) / 2; vw = np.cumsum(mid * v) / np.cumsum(v); dh = np.maximum.accumulate(h); dl = np.minimum.accumulate(l)
        ibh, ibl = h[:2].max(), l[:2].min(); ibw = ibh - ibl; sig = 0; nd = 0
        for k in range(6, 12):
            if k < sig or nd >= 5: continue
            px = c[k]; dv = px - vw[k]
            if abs(dv) < 1.5 * X: continue
            lado = -1 if dv > 0 else 1
            if lado == -1: ext = max(0.0, dh[k] - ibh) / ibw; stop = dh[k] + al_tick(X / 2)
            else: ext = max(0.0, ibl - dl[k]) / ibw; stop = dl[k] - al_tick(X / 2)
            if ext > 1: continue
            ds = abs(stop - px); P = ds / (ds + abs(dv))
            if P < 0.55: continue
            if modo == "salida5":
                k5 = 6 * (k + 1) - 1
                j5, sp, mot = salida5(o5, h5, l5, c5, vw5, k5, lado, stop); j = (j5 + 1) // 6 - (1 if (j5 + 1) % 6 == 0 else 0)
                j = j5 // 6
            else:
                sp = None
                for j in range(k + 1, 13):
                    L = al_tick(vw[j - 1])
                    if lado == -1: st = h[j] >= stop; tg = l[j] <= L - TICK; gap = o[j] >= stop
                    else: st = l[j] <= stop; tg = h[j] >= L + TICK; gap = o[j] <= stop
                    if st and tg and modo == "camino":
                        sube = c[j] >= o[j]  # O->L->H->C si cierra arriba
                        primero_bajo = sube
                        tg_primero = (lado == -1 and primero_bajo) or (lado == 1 and not primero_bajo)
                        if gap: sp, mot = o[j], "stop"
                        elif tg_primero: sp, mot = L, "objetivo"
                        else: sp, mot = stop, "stop"
                        break
                    if st: sp, mot = (o[j] if gap else stop), "stop"; break
                    if tg: sp, mot = L, "objetivo"; break
                if sp is None: j, sp, mot = 12, c[12], "fin"
            ops.append(dict(fecha=dia, pts=(sp - px) * lado, motivo=mot)); nd += 1; sig = j + 1
    return pd.DataFrame(ops)

def linea(nom, o):
    for mit, a, b in (("diseno", "2025-09-09", "2026-02-27"), ("chequeo", "2026-03-02", "2026-08-31")):
        q = o[(o.fecha >= a) & (o.fecha <= b)]
        print("%-44s %-8s ops %3d | acierto %.1f %% | neto %+.2f | %s" % (nom, mit, len(q), 100 * (q.pts > 0).mean(), (q.pts - 1).mean(), q.motivo.value_counts().to_dict()))
linea("A3 5 min (los 31 cierres)", a3_5(False))
linea("A3 5 min solo en los 6 cierres :00/:30", a3_5(True))
linea("A3-30 entradas, salida en 5 min", a3_30("salida5"))
linea("A3-30 entradas, salida 30 min conservadora", a3_30("conservador"))
linea("A3-30 entradas, salida 30 min por camino OHLC", a3_30("camino"))
