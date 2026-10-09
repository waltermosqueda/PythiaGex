# -*- coding: utf-8 -*-
"""v41_03_paridad.py — PARIDAD de mi reconstruccion (datos/rayas_v41.pkl) contra lo que la 4.1 ANOTO esta noche minuto a minuto
(%APPDATA%/ATAS/PythiaGex4/familia/niv-2026-10-09-MNQZ6.jsonl: k = minutos desde 1970 = clave del minuto; f = fut; m = conversiones;
s = series {nombre: [[precio, 'D1'|'D2', monto, strike], ...]}). SOLO LECTURA. Escribe datos/paridad_0910.txt."""
import json
import os

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
AP = os.path.join(os.environ["APPDATA"], "ATAS")
NIV = os.path.join(AP, "PythiaGex4", "familia", "niv-2026-10-09-MNQZ6.jsonl")
SERIES = ("MUROS_QQQ_vol", "MAJORS_QQQ_vol", "MUROS_QQQ_oi", "MAJORS_QQQ_oi")


def leer_niv(p=NIV):
    cab = None; recs = []
    for l in open(p, encoding="utf-8"):
        l = l.strip()
        if not l:
            continue
        r = json.loads(l)
        if "k" not in r:
            cab = r; continue
        recs.append(r)
    return cab, recs


def main():
    X = pd.read_pickle(os.path.join(AQUI, "datos", "rayas_v41.pkl"))
    S = X["series"]; M = X["meta"].set_index("t")
    cab, recs = leer_niv()
    lin = ["cabecera del archivo de la 4.1: %s" % cab, "registros: %d, %s -> %s" % (
        len(recs), pd.Timestamp(recs[0]["k"] * 60, unit="s"), pd.Timestamp(recs[-1]["k"] * 60, unit="s"))]
    dfut = []; drz = []
    tot = {s: [0, 0, 0, 0] for s in SERIES}   # minutos comparados, strikes iguales, precio igual a 0,01, |dif precio| max
    difs = {s: [] for s in SERIES}
    for r in recs:
        t = pd.Timestamp(r["k"] * 60, unit="s")
        if t not in M.index:
            continue
        mf = M.loc[t, "fut"]
        dfut.append(r["f"] - mf)
        q = [m for m in r["m"] if m["l"] == "QQQ"]
        if q and M.loc[t, "motivo"] == "ok":
            drz.append(q[0]["c"] - M.loc[t, "rz"])
        for s in SERIES:
            real = {e[1]: (e[0], e[3]) for e in r["s"].get(s, [])}
            mio = S[s].get(t, {})
            tot[s][0] += 1
            kr = {k: v[1] for k, v in real.items()}; km = {k: float(v[1][3:]) for k, v in mio.items()}
            if kr == km:
                tot[s][1] += 1
                dp = max([abs(real[k][0] - mio[k][0]) for k in real] or [0.0])
                tot[s][3] = max(tot[s][3], dp)
                if dp <= 0.011:
                    tot[s][2] += 1
            else:
                difs[s].append((t, kr, km))
    dfut = np.array(dfut); drz = np.array(drz)
    lin.append("fut (cierre conocido): %d minutos, iguales %d, |dif| max %.2f" % (len(dfut), int((np.abs(dfut) < 1e-9).sum()), np.abs(dfut).max()))
    lin.append("razon QQQ: %d minutos, |dif| mediana %.6f max %.6f (x 750: %.2f / %.2f pts)" % (
        len(drz), np.median(np.abs(drz)), np.abs(drz).max(), 750 * np.median(np.abs(drz)), 750 * np.abs(drz).max()))
    for s in SERIES:
        n, ig, pr, mx = tot[s]
        lin.append("%-15s minutos %d | mismos strikes D1/D2 %d (%.1f %%) | ademas precio a 0,01: %d | |dif precio| max con mismos strikes %.2f" % (
            s, n, ig, 100 * ig / n, pr, mx))
        for t, kr, km in difs[s][:12]:
            lin.append("     %s 4.1 %s  mio %s" % (t.strftime("%m-%d %H:%M"), kr, km))
    txt = "\n".join(lin)
    print(txt)
    open(os.path.join(AQUI, "datos", "paridad_0910.txt"), "w", encoding="utf-8").write(txt + "\n")


if __name__ == "__main__":
    main()
