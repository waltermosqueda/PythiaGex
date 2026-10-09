# -*- coding: utf-8 -*-
"""a05_reproducir_v41.py — AUDITOR. SOLO LECTURA de v41/datos (rayas_v41.pkl, velas_v41.csv). Con el codigo PROPIO de a01 (sin el
juez) rehace llegadas y % sostenidos TOLERANTE de las 4 series de QQQ de la 4.1 en la ventana CONGELADA (desfase 0: la clave t ya es
lo vigente en la vela t; sesiones con >= 30 minutos 'ok', igual que v41_04_juez.ventanas). Informado: MUROS_vol 179 / 46,9 %;
MAJORS_vol 147 / 48,3 %; MUROS_oi 171 / 47,4 %; MAJORS_oi 149 / 42,3 %. Correr: python -I a05_reproducir_v41.py"""
import os
import sys

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
from a01_reproducir import decidir  # noqa: E402

V41 = os.path.join(os.path.dirname(AQUI), "v41", "datos")


def main():
    V = pd.read_csv(os.path.join(V41, "velas_v41.csv"), parse_dates=["t"])
    V = V.dropna(subset=["o", "h", "l", "c"]).sort_values("t").drop_duplicates("t", keep="last").reset_index(drop=True)
    X = pd.read_pickle(os.path.join(V41, "rayas_v41.pkl"))
    meta = X["meta"]
    vent = []
    for s in sorted(meta["sesion"].unique()):
        N = pd.Timestamp(s)
        a = N - pd.Timedelta(hours=2) if N.weekday() == 0 else N + pd.Timedelta(minutes=35)
        b = N + pd.Timedelta(hours=8, minutes=30)
        m = meta[(meta["t"] >= a) & (meta["t"] < b)]
        if int((m["motivo"] == "ok").sum()) >= 30:
            vent.append((s, a, b))
    print("noches congelada:", len(vent), [v[0] for v in vent])
    t = V["t"].to_numpy().astype("datetime64[m]").astype(np.int64)
    o, h, l, c = (V[k].to_numpy(float) for k in "ohlc")
    gap = np.ones(len(V), bool); gap[1:] = (t[1:] - t[:-1]) > 30
    enV = np.zeros(len(V), bool); noche = np.empty(len(V), object)
    for s, a, b in vent:
        mm = ((V["t"] >= a) & (V["t"] < b)).to_numpy(); enV |= mm; noche[mm] = s
    for nom, ser in X["series"].items():
        sm = {pd.Timestamp(k).value // 60_000_000_000: v for k, v in ser.items()}
        ocupado = {}; ev = []
        for i in range(1, len(V)):
            if not enV[i] or gap[i]:
                continue
            d = sm.get(t[i]) or {}
            xs = []
            for x in d.values():
                if x is None:
                    continue
                x = float(x[0] if isinstance(x, (tuple, list)) else x)
                if x == x and x != 0 and all(abs(x - y) > 1.0 for y in xs):
                    xs.append(x)
            for L in xs:
                key = round(L, 2)
                if ocupado.get(key, -1) >= i:
                    continue
                if not (h[i] >= L - 2 and l[i] <= L + 2) or (h[i - 1] >= L - 2 and l[i - 1] <= L + 2):
                    continue
                s_ = 1 if h[i - 1] < L - 2 else -1
                r, kf = decidir(o, h, l, c, t, gap, i, L, s_)
                ocupado[key] = kf
                ev.append((noche[i], r))
        base = sum(r != "cens" for _, r in ev); so = sum(r == "sostenido" for _, r in ev)
        e2 = [(n, r) for n, r in ev if n != "2026-10-09"]
        b2 = sum(r != "cens" for _, r in e2); s2 = sum(r == "sostenido" for _, r in e2)
        print("%-15s llegadas %3d base %3d sost %3d = %.1f %% | sin 10-09: %.1f %%" % (nom, len(ev), base, so, 100 * so / base, 100 * s2 / b2))


if __name__ == "__main__":
    main()
