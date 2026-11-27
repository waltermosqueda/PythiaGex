# -*- coding: utf-8 -*-
"""A3 POR FUERA en velas de 5 min de MES (27-11-2026): mi implementacion desde el PREREGISTRO (sin el codigo del agente), con la
version en velas que describe RESULTADOS.md: VWAP con (max+min)/2 x volumen, entrada al cierre de la vela, stop y objetivo en la misma
vela = stop, cierre a las 16:00. Compara los totales con lo informado: diseno 116 r / 109 ops / 74,3 % / +2,49; chequeo 126 r / 123 ops /
67,5 % / +0,66. Uso: python -I a3_propio_m5.py"""
import sys, os, math
sys.stdout.reconfigure(encoding="utf-8")
import numpy as np, pandas as pd
TICK = 0.25
V = r"C:\Users\wmx_7\OneDrive\Escritorio\ATAS nada\PythiaGex\laboratorio\dom\ronda3\velas\velas-MES-m5.csv"
def al_tick(x): return math.floor(x / TICK + 0.5 + 1e-9) * TICK
d = pd.read_csv(V, usecols=["t", "o", "h", "l", "c", "vol"])
t = pd.to_datetime(d["t"]).dt.tz_localize("UTC").dt.tz_convert("America/New_York")
d["dia"] = t.dt.strftime("%Y-%m-%d"); d["min"] = t.dt.hour * 60 + t.dt.minute
rth = d[(d["min"] >= 570) & (d["min"] < 960)].copy()
ruedas = {}
for dia, g in rth.groupby("dia"):
    if len(g) == 78 and g["min"].iloc[0] == 570 and g["min"].iloc[-1] == 955: ruedas[dia] = g.reset_index(drop=True)
dias = sorted(ruedas)
rango = {x: ruedas[x]["h"].max() - ruedas[x]["l"].min() for x in dias}
print("ruedas completas:", len(dias), dias[0], "a", dias[-1])
ops = []
for n, dia in enumerate(dias):
    if n < 10: continue
    X = al_tick(0.08 * float(np.median([rango[x] for x in dias[n - 10:n]])))
    g = ruedas[dia]; o, h, l, c, v = (g[k].to_numpy(float) for k in ("o", "h", "l", "c", "vol"))
    mid = (h + l) / 2; vw = np.cumsum(mid * v) / np.cumsum(v); dh = np.maximum.accumulate(h); dl = np.minimum.accumulate(l)
    ibh, ibl = h[:12].max(), l[:12].min(); ibw = ibh - ibl
    sig_ok = 0; nd = 0
    for k in range(78):
        cierre = 570 + 5 * (k + 1)
        if not (780 <= cierre <= 930) or k < sig_ok or nd >= 5: continue
        px = c[k]; dv = px - vw[k]
        if abs(dv) < 1.5 * X: continue
        lado = -1 if dv > 0 else 1
        if lado == -1: ext = max(0.0, dh[k] - ibh) / ibw; stop = dh[k] + al_tick(X / 2)
        else: ext = max(0.0, ibl - dl[k]) / ibw; stop = dl[k] - al_tick(X / 2)
        if ext > 1: continue
        ds = abs(stop - px); P = ds / (ds + abs(dv))
        if P < 0.55: continue
        sal = None
        for j in range(k + 1, 78):
            L = al_tick(vw[j - 1])
            if lado == -1:
                st = h[j] >= stop; tg = l[j] <= L - TICK
                if st: sal = (j, o[j] if o[j] >= stop else stop, "stop"); break
            else:
                st = l[j] <= stop; tg = h[j] >= L + TICK
                if st: sal = (j, o[j] if o[j] <= stop else stop, "stop"); break
            if tg: sal = (j, L, "objetivo"); break
        if sal is None: sal = (77, c[77], "fin")
        j, sp, mot = sal
        ops.append(dict(fecha=dia, hora="%02d:%02d" % divmod(cierre, 60), lado=lado, entrada=px, stop=stop, X=X, P=P, motivo=mot, salida=sp, pts=(sp - px) * lado))
        nd += 1; sig_ok = j + 1
o_ = pd.DataFrame(ops)
for nom, a, b in (("DISENO", "2025-09-09", "2026-02-27"), ("CHEQUEO", "2026-03-02", "2026-08-31")):
    q = o_[(o_.fecha >= a) & (o_.fecha <= b)]; nr = sum(1 for x in dias[10:] if a <= x <= b)
    por_rueda = q.groupby("fecha")["pts"].apply(lambda s: (s - 1).sum()).reindex([x for x in dias[10:] if a <= x <= b], fill_value=0.0)
    tt = por_rueda.mean() / (por_rueda.std(ddof=1) / math.sqrt(len(por_rueda)))
    print("%-8s ruedas %d | ops %d (%.2f/r) | acierto %.1f %% | neto c1 %+.3f | t por rueda %.2f | motivos %s"
          % (nom, nr, len(q), len(q) / nr, 100 * (q.pts > 0).mean(), (q.pts - 1).mean(), tt, q.motivo.value_counts().to_dict()))
    for a2, b2 in ((a, a[:5] + ("11-30" if a[5:7] == "09" else "05-31")), (a[:5] + ("12-01" if a[5:7] == "09" else "06-01"), b)):
        s = q[(q.fecha >= a2) & (q.fecha <= b2)]
        print("     %s..%s ops %d acierto %.1f %% neto %+.2f" % (a2, b2, len(s), 100 * (s.pts > 0).mean(), (s.pts - 1).mean()))
o_.to_csv(os.path.join(os.path.dirname(os.path.abspath(__file__)), "a3_propio_ops_m5.csv"), index=False)
for f, hr in (("2025-09-10", "13:20"), ("2026-02-27", "14:50"), ("2026-03-02", "13:55"), ("2026-08-31", "15:25")):
    r = o_[(o_.fecha == f) & (o_.hora == hr)]
    print("  muestra del agente", f, hr, r[["lado", "entrada", "motivo", "pts"]].to_dict("records"))
