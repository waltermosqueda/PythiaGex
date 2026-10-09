# -*- coding: utf-8 -*-
"""esc_05_disponibilidad.py — ESCEPTICO 1: el dataset toma como hora de disponibilidad de cada cadena de CBOE el 'generado' de la
bajada MAS TEMPRANA entre todas las carpetas (nube, nube2, cboe-local, cboe4, local). La 4.1 en vivo solo ve SU bajada (cboe4).
Se mide, por cadena_ts comun, cuanto antes que la bajada de la 4.1 aparece la mas temprana (minutos de 'ventaja' que el backtest
tiene sobre lo que vio el indicador), de dia y de noche. SOLO LECTURA (lee las cabeceras; no parsea las filas).
Uso: python -I esc_05_disponibilidad.py"""
import ctypes
try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass
import gzip
import json
import os
import re

import numpy as np
import pandas as pd

AP = os.path.join(os.environ["APPDATA"], "ATAS")
CARP = [("nube", os.path.join(AP, "PythiaGex", "cadenas"), "cadena-%s-%s.jsonl.gz"),
        ("nube2", os.path.join(AP, "PythiaGex2", "cadenas"), "cadena-%s-%s.jsonl.gz"),
        ("cboe_local", os.path.join(AP, "PythiaGex", "cboe-local"), "cadena-%s-%s.jsonl.gz"),
        ("cboe4", os.path.join(AP, "PythiaGex4", "cboe"), "cadena-%s-%s.jsonl.gz"),
        ("local1", os.path.join(AP, "PythiaGex", "cadenas"), "local-%s-%s.jsonl"),
        ("local2", os.path.join(AP, "PythiaGex2", "cadenas"), "local-%s-%s.jsonl")]
RX_TS = re.compile(r'"cadena_ts"\s*:\s*"([^"]+)"')
RX_GEN = re.compile(r'"generado"\s*:\s*"([^"]+)"')
AQUI = os.path.dirname(os.path.abspath(__file__))


def leer(tk, dia):
    out = {}
    for nom, c, pat in CARP:
        p = os.path.join(c, pat % (tk, dia))
        if not os.path.exists(p):
            continue
        op = gzip.open if p.endswith(".gz") else open
        try:
            with op(p, "rt", encoding="utf-8", errors="replace") as f:
                for l in f:
                    cab = l[:600]
                    m = RX_TS.search(cab); g = RX_GEN.search(cab)
                    if not m or not g:
                        continue
                    gen = pd.Timestamp(g.group(1).replace(chr(92) + "u002B", "+").replace(chr(92) + "u002b", "+").replace("Z", "+00:00"))
                    if gen.tzinfo is not None:
                        gen = gen.tz_convert("UTC").tz_localize(None)
                    out.setdefault(nom, {}).setdefault(m.group(1), gen)
                    if gen < out[nom][m.group(1)]:
                        out[nom][m.group(1)] = gen
        except (OSError, EOFError):
            pass
    return out


def main():
    res = {}
    for tk in ("NQ", "QQQ"):
        for dia in ("2026-10-06", "2026-10-07", "2026-10-08"):
            x = leer(tk, dia)
            if "cboe4" not in x:
                continue
            c4 = x["cboe4"]
            ventaja = []
            for ts, g4 in c4.items():
                otros = [x[n][ts] for n in x if n != "cboe4" and ts in x[n]]
                if not otros:
                    continue
                g0 = min(otros + [g4])
                hm = g4.strftime("%H:%M")
                ventaja.append(((g4 - g0).total_seconds() / 60.0, "rueda" if "13:30" <= hm < "20:00" else "otra"))
            if not ventaja:
                continue
            v = np.array([a for a, _ in ventaja]); w = np.array([b for _, b in ventaja])
            d = {}
            for f in ("rueda", "otra"):
                s = v[w == f]
                if len(s):
                    d[f] = dict(n=int(len(s)), pct_con_ventaja=float(100 * (s > 0.05).mean()), mediana_min=float(np.median(s)),
                                p90_min=float(np.percentile(s, 90)), max_min=float(s.max()))
            res["%s|%s" % (tk, dia)] = dict(fuentes=sorted(x), **d)
            print(tk, dia, sorted(x), json.dumps(d))
    json.dump(res, open(os.path.join(AQUI, "datos", "disponibilidad.json"), "w", encoding="utf-8"), indent=1)


if __name__ == "__main__":
    main()
