# -*- coding: utf-8 -*-
"""e01_juez_propio.py — esceptico2: REPRODUCCION INDEPENDIENTE de los numeros clave. Juez de ACIERTO20 escrito de cero (no importa
juez_operador ni juez20 para juzgar; solo usa e00_comun para rearmar las mismas entradas que cada familia). Reglas, de la definicion
pre-registrada: llegada = la vela toca [L-2, L+2] y la anterior no (techo si venia de abajo, piso si de arriba); ROTA (TOLERANTE) =
cierre mas alla de L+5 con cuerpo >= 4, o 3 cierres seguidos mas alla, o > 10 min con cierres mas alla; ACIERTO20 = 20 pts a favor
desde la raya antes de romperse; 120 min sin decidir = indefinida; fin de datos / hueco > 30 min = censurada. Raya vigente = clave
t - desfase con arrastre 90 min; fusion <= 1 pt; identidad de raya entre minutos <= 3 pts en 30 min; una raya no registra otra llegada
mientras la anterior no se decide.
Uso: python -I e01_juez_propio.py   -> datos/e01_reproduccion.json y .txt"""
import json
import math
import os
import sys
import time

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import e00_comun as E  # noqa: E402
E.prioridad_baja()

import numpy as np  # noqa: E402
import pandas as pd  # noqa: E402

TOL, GIRO, VMIN, ARR, FUS, IDT, MEM, HUECO = 2.0, 20.0, 120, 90, 1.0, 3.0, 30, 30


def velas(V0, ventanas):
    df = pd.DataFrame(V0).copy()
    t = pd.to_datetime(df["t"])
    df["t"] = t.dt.floor("min")
    df = df.dropna(subset=["o", "h", "l", "c"]).sort_values("t").drop_duplicates("t", keep="last").reset_index(drop=True)
    tm = (df["t"].astype("int64") // 60_000_000_000).to_numpy()
    noche = df["noche"].astype(str).to_numpy() if "noche" in df.columns else (df["t"] + pd.Timedelta(hours=2)).dt.strftime("%Y-%m-%d").to_numpy()
    en = np.zeros(len(df), bool)
    for a, b in ventanas:
        a = pd.Timestamp(a).value // 60_000_000_000; b = pd.Timestamp(b).value // 60_000_000_000
        en |= (tm >= a) & (tm < b)
    gap = np.ones(len(df), bool); gap[1:] = np.diff(tm) > HUECO
    return dict(t=df["t"].to_numpy(), tm=tm, o=df["o"].to_numpy(float), h=df["h"].to_numpy(float), l=df["l"].to_numpy(float),
                c=df["c"].to_numpy(float), noche=noche, en=en, gap=gap, n=len(df))


def valores(x, claves):
    if x is None:
        return []
    if isinstance(x, dict):
        it = [x.get(k) for k in claves] if claves else list(x.values())
    elif isinstance(x, (list, tuple, np.ndarray)):
        it = list(x)
    else:
        it = [x]
    out = []
    for v in it:
        if isinstance(v, dict):
            v = v.get("precio")
        elif isinstance(v, (list, tuple)):
            v = v[0]
        try:
            v = float(v)
        except (TypeError, ValueError):
            continue
        if math.isfinite(v) and v != 0.0:
            out.append(v)
    return out


def pistas(Vx, rayas, desfase, claves):
    ks = sorted((pd.Timestamp(k).value // 60_000_000_000, v) for k, v in rayas.items())
    km = np.array([k for k, _ in ks], np.int64)
    P = []          # lista de (lista idx, lista val)
    act = {}
    for i in range(Vx["n"]):
        obj = Vx["tm"][i] - desfase
        j = int(np.searchsorted(km, obj, side="right")) - 1
        lin = []
        if j >= 0 and obj - km[j] <= ARR:
            for v in valores(ks[j][1], claves):
                if not any(abs(g - v) <= FUS for g in lin):
                    lin.append(v)
        for pid in [p for p, (_, ut) in act.items() if Vx["tm"][i] - ut > MEM]:
            del act[pid]
        cand = sorted((abs(v0 - x), pid, k) for k, x in enumerate(lin) for pid, (v0, _) in act.items() if abs(v0 - x) <= IDT)
        up, asg = set(), {}
        for _, pid, k in cand:
            if pid in up or k in asg:
                continue
            up.add(pid); asg[k] = pid
        for k, x in enumerate(lin):
            pid = asg.get(k)
            if pid is None:
                pid = len(P); P.append(([], []))
            P[pid][0].append(i); P[pid][1].append(x)
            act[pid] = (x, Vx["tm"][i])
    return P


def decidir(Vx, i, L, techo):
    o, h, l, c, tm, gap, n = Vx["o"], Vx["h"], Vx["l"], Vx["c"], Vx["tm"], Vx["gap"], Vx["n"]
    run = 0; t0 = None; k = i
    while True:
        if k >= n:
            return "censurada", n - 1
        if k > i and gap[k]:
            return "censurada", k - 1
        if tm[k] > tm[i] + VMIN:
            return "indefinida", k - 1
        if techo:
            fav = L - l[k]; cb = c[k] - L
            if k == i and c[k] >= o[k]:
                fav = L - c[k]
        else:
            fav = h[k] - L; cb = L - c[k]
            if k == i and c[k] < o[k]:
                fav = c[k] - L
        if fav >= GIRO:
            return "acierto", k
        if cb > 0:
            run += 1
            if run == 1:
                t0 = tm[k]
        else:
            run = 0
        if cb > 5 and abs(c[k] - o[k]) >= 4:
            return "falsa", k
        if run >= 3:
            return "falsa", k
        if run > 0 and tm[k] - t0 + 1 > 10:
            return "falsa", k
        k += 1


def juzgar(Vx, P, off=None):
    h, l, gap, en = Vx["h"], Vx["l"], Vx["gap"], Vx["en"]
    evs = []
    for pid, (ix, vs) in enumerate(P):
        d = 0.0 if off is None else off[pid]
        ocup = -1
        for i, v in zip(ix, vs):
            if i < 1 or i <= ocup or gap[i] or not en[i]:
                continue
            v = v + d
            if not (h[i] >= v - TOL and l[i] <= v + TOL):
                continue
            if h[i - 1] >= v - TOL and l[i - 1] <= v + TOL:
                continue
            techo = h[i - 1] < v - TOL
            res, kf = decidir(Vx, i, v, techo)
            ocup = kf
            evs.append(dict(i=i, pid=pid, raya=v, tipo="techo" if techo else "piso", res=res, noche=Vx["noche"][i], kf=kf))
    evs.sort(key=lambda e: (e["i"], e["raya"]))
    return evs


def resumen(evs):
    base = [e for e in evs if e["res"] != "censurada"]
    a = sum(e["res"] == "acierto" for e in base)
    return dict(llegadas=len(evs), base=len(base), aciertos=a, falsas=sum(e["res"] == "falsa" for e in base),
                indefinidas=sum(e["res"] == "indefinida" for e in base), pct=100.0 * a / len(base) if base else float("nan"),
                techos=sum(e["tipo"] == "techo" for e in evs), pisos=sum(e["tipo"] == "piso" for e in evs))


def main():
    out = {}
    lineas = []
    for nombre in E.CASOS:
        t0 = time.time()
        cs = E.caso(nombre)
        Vx = velas(cs["V"], cs["ventanas"])
        P = pistas(Vx, cs["rayas"], cs["opciones"].get("desfase_min", 1), cs["opciones"].get("claves"))
        evs = juzgar(Vx, P)
        r = resumen(evs)
        inf = E.informado(nombre)
        r.update(n_pistas=len(P), noches=len(cs["noches"]), informado=inf, seg=round(time.time() - t0, 1))
        out[nombre] = r
        s = "%-34s propio: llegadas %4d aciertos %4d -> %5.1f %% (techos %d, pisos %d, pistas %d) | informado: llegadas %s aciertos %s -> %s %%" % (
            nombre, r["llegadas"], r["aciertos"], r["pct"], r["techos"], r["pisos"], len(P),
            inf and inf["llegadas"], inf and inf["aciertos"], inf and round(inf["pct"], 2))
        print(s, flush=True); lineas.append(s)
        pd.to_pickle(evs, os.path.join(AQUI, "datos", "e01_eventos_%s.pkl" % nombre.replace("|", "__")))
    with open(os.path.join(AQUI, "datos", "e01_reproduccion.json"), "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1, default=str)
    with open(os.path.join(AQUI, "datos", "e01_reproduccion.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(lineas) + "\n")


if __name__ == "__main__":
    main()
