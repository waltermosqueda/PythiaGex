# -*- coding: utf-8 -*-
"""Paso 4 (POST-HOC, sin veredicto: se corre DESPUES de cerrada la confirmacion del finalista; no elige ni cambia nada).
 a) las 12 variantes en confirmacion, solo para ver si las tendencias de exploracion repiten;
 b) velocidad: cuanto tarda en resolverse la barrera +-8 despues de cada disparo contra segundos al azar de la misma media hora;
 c) la correlacion negativa con la vela siguiente, ¿es rebote entre puntas? se repite con el precio medio (bid+ask)/2."""
import sys, os, time
import numpy as np, pandas as pd
import escalas_lib as L
B = L.B
pd.set_option("display.width", 250); pd.set_option("display.max_columns", 40)
t0 = time.time(); T = B.todas_las_sesiones(); C = B.confirmar(T); U = L.cargar_umbrales()

print("a) LAS 12 VARIANTES EN CONFIRMACION (post-hoc, sin veredicto)")
R, det = L.juzgar_todo(C, U)
print(R[["variante", "disparos", "b8_n", "b8_ac", "b8_z", "b8_t_dias", "b8_dias", "b5_ac", "b12_ac", "mov10", "mov30", "mov60"]].to_string(index=False))

print("\nb) VELOCIDAD: segundos hasta tocar +-8 (20 sesiones). Mediana tras el disparo contra mediana de segundos al azar de la misma sesion y media hora")
rng = np.random.default_rng(11); acum = {v: dict(sig=[], azar=[]) for v in L.VARIANTES}
for s in sorted(T):
    seg = T[s]; f = L.rasgos(seg); fn = os.path.join(L.TMP, "seg8-%s.npy" % s)
    if os.path.exists(fn): th = np.load(fn)
    else:
        _, th = B.barrera(seg, 8, 600); th = np.where(np.isinf(th), 601, th).astype("float32"); np.save(fn, th)
    mh = (f["sod"] - (13 * 3600 + 30 * 60)) // 1800; dis = L.disparos(seg, f, U)
    pools = {k: np.flatnonzero(f["valido"] & (mh == k)) for k in np.unique(mh[f["valido"]])}
    for v in L.VARIANTES:
        i = dis[v][0]
        if len(i) == 0: continue
        acum[v]["sig"].append(th[i])
        acum[v]["azar"].append(np.concatenate([th[rng.choice(pools[k], size=20)] for k in mh[i]]))
print("%-9s %8s %14s %14s %18s %18s" % ("variante", "n", "mediana señal", "mediana azar", "resuelve<120s señal", "resuelve<120s azar"))
for v in L.VARIANTES:
    a = np.concatenate(acum[v]["sig"]); z = np.concatenate(acum[v]["azar"])
    print("%-9s %8d %14.0f %14.0f %17.1f%% %17.1f%%" % (v, len(a), np.median(a), np.median(z), 100 * (a < 120).mean(), 100 * (z < 120).mean()))

print("\nc) ¿REBOTE ENTRE PUNTAS? correlacion delta ~ retorno de la vela SIGUIENTE con ultimo y con precio medio (20 sesiones, rueda)")
for nom, W in (("5 s", 5), ("15 s", 15), ("30 s", 30), ("1 min", 60), ("2 min", 120), ("5 min", 300)):
    cu, cm, n = [], [], 0; D, RU, RM = [], [], []
    for s in sorted(T):
        r = T[s][T[s]["rueda"]]
        if len(r) < 3 * W: continue
        g = r.resample("%ds" % W, label="left", closed="left"); medio = (r["bid"] + r["ask"]) / 2
        b = pd.DataFrame({"c": g["ultimo"].last(), "m": medio.resample("%ds" % W, label="left", closed="left").last(), "delta": g["delta"].sum()}).dropna()
        D.append(b["delta"].to_numpy()[1:-1]); RU.append(b["c"].diff().shift(-1).to_numpy()[1:-1]); RM.append(b["m"].diff().shift(-1).to_numpy()[1:-1])
    D = np.concatenate(D); RU = np.concatenate(RU); RM = np.concatenate(RM)
    print("  %-6s velas %6d | con ultimo %+.3f | con precio medio %+.3f | +-2 err %.3f" % (nom, len(D), np.corrcoef(D, RU)[0, 1], np.corrcoef(D, RM)[0, 1], 2 / np.sqrt(len(D))))
print("tiempo %.1f s" % (time.time() - t0))
