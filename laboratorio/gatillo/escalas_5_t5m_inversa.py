# -*- coding: utf-8 -*-
"""Paso 5 (POST-HOC, FUERA DE PROTOCOLO, sin veredicto): la inversa de T5m (ir CONTRA el delta extremo de la vela de 5 min).
No fue elegible por la regla pre-registrada (en exploracion dio z -1,82 y la regla pedia <= -2,5). Se mira solo para dejar anotada la pista
y poder pre-registrarla contra sesiones NUEVAS."""
import numpy as np, pandas as pd
import escalas_lib as L
B = L.B
T = B.todas_las_sesiones(); U = L.cargar_umbrales()
for nom, S in (("exploracion", B.explorar(T)), ("confirmacion", B.confirmar(T))):
    R, det = L.juzgar_todo(S, U, variantes=["T5m"], invertir=("T5m",)); d = det["T5m"]; lado = np.array(d["lado"]); dias = np.array(d["ses"])
    print("\n== T5m INVERSA en %s ==" % nom)
    for (x, h) in L.BARRERAS:
        y = np.array(d["y"][(x, h)]); ok = y != 0; ac = float(((y[ok] * lado[ok]) > 0).mean()); m, sd, _ = L.placebo(S, d, (x, h))
        r = B.juzgar(y, lado, "", dias, x)
        print("+-%d/%ds: n %d acierto %.1f %% (z %.2f, t dias %.2f, dias arriba %s) | placebo %.1f +- %.1f -> z placebo %.2f | empate %.1f | neto %.2f pts" % (
            x, h, ok.sum(), 100 * ac, r["z"], L.t_por_dias(y, lado, dias), r["dias_arriba"], 100 * m, 100 * sd, (ac - m) / sd, 100 * B.empate(x), (2 * ac - 1) * x - B.COSTO_PTS))
    y = np.array(d["y"][(8, 600)]); ok = y != 0
    print(pd.DataFrame(dict(dia=dias[ok], ok=(y[ok] * lado[ok]) > 0)).groupby("dia")["ok"].agg(["mean", "size"]).round(2).T.to_string())
    sod = np.array(d["sod"])[ok]; g = (y[ok] * lado[ok]) > 0
    for a, b_, t in ((13.5, 15, "13:30-15:00"), (15, 18, "15:00-18:00"), (18, 20, "18:00-20:00")):
        m = (sod >= a * 3600) & (sod < b_ * 3600); print("   franja %s UTC: n %d acierto %.1f %%" % (t, m.sum(), 100 * g[m].mean() if m.sum() else float("nan")))
    print("   largos: n %d acierto %.1f %% | cortos: n %d acierto %.1f %%" % ((lado[ok] > 0).sum(), 100 * g[lado[ok] > 0].mean(), (lado[ok] < 0).sum(), 100 * g[lado[ok] < 0].mean()))

# --- las 20 sesiones juntas: t por dias y remuestreo POR DIA (los disparos de un mismo dia no son independientes)
R, det = L.juzgar_todo(T, U, variantes=["T5m"], invertir=("T5m",)); d = det["T5m"]; lado = np.array(d["lado"]); dias = np.array(d["ses"])
print("\n== T5m INVERSA, 20 sesiones juntas (post-hoc) ==")
rng = np.random.default_rng(3)
for (x, h) in L.BARRERAS:
    y = np.array(d["y"][(x, h)]); ok = y != 0; g = ((y[ok] * lado[ok]) > 0).astype(float); dd = dias[ok]; r = B.juzgar(y, lado, "", dias, x)
    por = pd.DataFrame(dict(d=dd, g=g)).groupby("d")["g"].agg(["sum", "size"]); ud = por.index.to_numpy(); bs = []
    for _ in range(2000):
        m = rng.choice(len(ud), size=len(ud)); bs.append(por["sum"].to_numpy()[m].sum() / por["size"].to_numpy()[m].sum())
    lo, hi = np.percentile(bs, [2.5, 97.5])
    print("+-%d/%ds: n %d acierto %.1f %% z %.2f | t dias %.2f | dias arriba %s | IC 95 %% remuestreando dias: %.1f a %.1f %% | empate %.1f %%" % (
        x, h, ok.sum(), 100 * g.mean(), r["z"], L.t_por_dias(y, lado, dias), r["dias_arriba"], 100 * lo, 100 * hi, 100 * B.empate(x)))
