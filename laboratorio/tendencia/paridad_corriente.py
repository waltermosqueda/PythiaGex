# -*- coding: utf-8 -*-
"""Paridad de la Corriente 1.1 (C#) con Python.
- `motor_atas`: copia linea por linea de CorrienteIndicador.OnCalculate (semilla del ATR = TR de la vela 0, direccion inicial
  arriba, trinquete, extremo del tramo, vuelta rapida).
- `python paridad_corriente.py lab`: el motor_atas sobre la serie de MES del laboratorio da la MISMA direccion que el motor de
  cal_03 (`corriente`) despues del calentamiento (las semillas difieren al principio).
- `python paridad_corriente.py [volcado.csv]`: recalcula el ultimo volcado que escribio ATAS (o el indicado) y compara cada
  vela: atr, bandas, direccion, extremo, vela del extremo y vuelta rapida."""
import glob
import os
import sys
from pathlib import Path

import numpy as np
import pandas as pd


def motor_atas(h, l, c, n=60, mult=4.0, rapida=True, v=2.5, j=3, retro=None):
    """retro = X pts: modo RETROCESO de la 1.2 (se da vuelta cuando el cierre retrocede X desde el extremo del tramo)."""
    N = len(c)
    atr, fu, fd = np.zeros(N), np.zeros(N), np.zeros(N)
    d_, ext_, iext_, rap_ = np.zeros(N, int), np.zeros(N), np.zeros(N, int), np.zeros(N, bool)
    for b in range(N):
        cp = c[b - 1] if b > 0 else c[b]
        tr = max(h[b] - l[b], abs(h[b] - cp), abs(l[b] - cp))
        atr[b] = tr if b == 0 else (atr[b - 1] * (n - 1) + tr) / n
        hl2 = (h[b] + l[b]) / 2
        up, dn = hl2 - mult * atr[b], hl2 + mult * atr[b]
        if b == 0:
            fu[b], fd[b], d_[b], ext_[b], iext_[b] = up, dn, 1, h[b], 0
            continue
        fu[b] = max(up, fu[b - 1]) if c[b - 1] > fu[b - 1] else up
        fd[b] = min(dn, fd[b - 1]) if c[b - 1] < fd[b - 1] else dn
        d0 = d_[b - 1]
        ext, iext = ext_[b - 1], iext_[b - 1]
        if d0 == 1 and h[b] > ext:
            ext, iext = h[b], b
        elif d0 == -1 and l[b] < ext:
            ext, iext = l[b], b
        if retro is not None:
            d = -d0 if d0 * (ext - c[b]) >= retro else d0
        else:
            d = -1 if (d0 == 1 and c[b] < fu[b]) else 1 if (d0 == -1 and c[b] > fd[b]) else d0
        if retro is None and d == d0 and rapida and b - iext <= j:
            if d0 == 1 and ext - c[b] >= v * atr[b]:
                d, rap_[b] = -1, True
            elif d0 == -1 and c[b] - ext >= v * atr[b]:
                d, rap_[b] = 1, True
        if d != d0:
            ext, iext = (h[b] if d == 1 else l[b]), b
        d_[b], ext_[b], iext_[b] = d, ext, iext
    linea = ext_ - d_ * retro if retro is not None else np.where(d_ == 1, fu, fd)
    return dict(atr=atr, fu=fu, fd=fd, dir=d_, ext=ext_, iext=iext_, rapida=rap_, linea=linea)


def motor_tramo(h, l, c, tramo=4.0, vivo=True, grande=10.0):
    """Copia del modo TRAMO de la 1.3: tramo rapido (en vivo = toca la linea del extremo de la vela anterior; si no, al cierre)
    y corriente grande (retroceso de `grande` pts al cierre)."""
    N = len(c)
    d_, ext_, iext_ = np.zeros(N, int), np.zeros(N), np.zeros(N, int)
    g_, eg_ = np.zeros(N, int), np.zeros(N)
    for b in range(N):
        if b == 0:
            d_[b], ext_[b], iext_[b], g_[b], eg_[b] = 1, h[b], 0, 1, h[b]
            continue
        g0, eg = g_[b - 1], eg_[b - 1]
        if g0 == 1 and h[b] > eg:
            eg = h[b]
        elif g0 == -1 and l[b] < eg:
            eg = l[b]
        gd = -g0 if g0 * (eg - c[b]) >= grande else g0
        if gd != g0:
            eg = h[b] if gd == 1 else l[b]
        g_[b], eg_[b] = gd, eg
        d0, ext, iext = d_[b - 1], ext_[b - 1], iext_[b - 1]
        if vivo:
            if d0 == 1 and l[b] <= ext - tramo:
                d = -1
            elif d0 == -1 and h[b] >= ext + tramo:
                d = 1
            else:
                d = d0
                if d0 == 1 and h[b] > ext:
                    ext, iext = h[b], b
                elif d0 == -1 and l[b] < ext:
                    ext, iext = l[b], b
        else:
            if d0 == 1 and h[b] > ext:
                ext, iext = h[b], b
            elif d0 == -1 and l[b] < ext:
                ext, iext = l[b], b
            d = -d0 if d0 * (ext - c[b]) >= tramo else d0
        if d != d0:
            ext, iext = (h[b] if d == 1 else l[b]), b
        d_[b], ext_[b], iext_[b] = d, ext, iext
    return dict(dir=d_, ext=ext_, iext=iext_, linea=ext_ - d_ * tramo, dir_g=g_, ext_g=eg_)


def comparar_volcado(ruta):
    v = pd.read_csv(ruta)
    nombre = os.path.basename(ruta)
    # parametros desde el nombre: ...-atr60x4-r2.5en3.csv
    import re
    mt = re.search(r"-tramo([\d.]+)(vivo|cierre)-g([\d.]+)\.csv$", nombre)
    mr = re.search(r"-retro([\d.]+)\.csv$", nombre)
    if mt:
        X, vivo, G_ = float(mt.group(1)), mt.group(2) == "vivo", float(mt.group(3))
        rt = motor_tramo(v.h.values, v.l.values, v.c.values, X, vivo, G_)
        print(f"{nombre}: {len(v)} velas ({v.hora_utc.iloc[0]} .. {v.hora_utc.iloc[-1]}), TRAMO {X} pts {'en vivo' if vivo else 'al cierre'}, grande {G_}")
        ok = True
        for k in ("dir", "iext", "dir_g"):
            dif = int((rt[k] != v[k].values).sum())
            print(f"  {k}: velas distintas {dif} de {len(v)}")
            ok &= dif == 0
        for k in ("ext", "linea", "ext_g"):
            dm = np.abs(rt[k] - v[k].values).max()
            print(f"  {k}: diferencia maxima {dm:.3e}")
            ok &= dm < 1e-9
        vu = np.nonzero(v.dir.values[1:] != v.dir.values[:-1])[0] + 1
        print(f"  vueltas del tramo: {len(vu)}; ultimas 8: " + ", ".join(f"{v.hora_utc.iloc[i][11:16]}{'+' if v.dir.iloc[i] > 0 else '-'}@{v.c.iloc[i]}" for i in vu[-8:]))
        print("PARIDAD", "EXACTA" if ok else "CON DIFERENCIAS")
        return ok
    if mr:
        X = float(mr.group(1))
        r = motor_atas(v.h.values, v.l.values, v.c.values, 60, 4.0, False, retro=X)
        print(f"{nombre}: {len(v)} velas ({v.hora_utc.iloc[0]} .. {v.hora_utc.iloc[-1]}), RETROCESO {X} pts")
    else:
        m = re.search(r"atr(\d+)x([\d.]+)-r(no|([\d.]+)en(\d+))\.csv$", nombre)
        n, mult = int(m.group(1)), float(m.group(2))
        rap = m.group(3) != "no"
        vv, jj = (float(m.group(4)), int(m.group(5))) if rap else (2.5, 3)
        r = motor_atas(v.h.values, v.l.values, v.c.values, n, mult, rap, vv, jj)
        print(f"{nombre}: {len(v)} velas ({v.hora_utc.iloc[0]} .. {v.hora_utc.iloc[-1]}), ATR {n} x {mult}, rapida {rap} {vv} en {jj}")
    ok = True
    for k in ("atr", "fu", "fd"):
        dmax = np.abs(r[k] - v[k].values).max()
        print(f"  {k}: diferencia maxima {dmax:.3e}")
        ok &= dmax < 1e-6
    for k in ("dir", "iext", "rapida"):
        dif = int((np.asarray(r[k]).astype(int) != v[k].values.astype(int)).sum())
        print(f"  {k}: velas distintas {dif} de {len(v)}")
        ok &= dif == 0
    dmax = np.abs(r["ext"] - v.ext.values).max()
    print(f"  ext: diferencia maxima {dmax:.3e}")
    ok &= dmax < 1e-9
    if "linea" in v.columns:
        dmax = np.abs(r["linea"] - v.linea.values).max()
        print(f"  linea del tunel: diferencia maxima {dmax:.3e}")
        ok &= dmax < 1e-6
    vu = np.nonzero(v.dir.values[1:] != v.dir.values[:-1])[0] + 1
    print(f"  vueltas: {len(vu)}, rapidas {int(v.rapida.values[vu].sum())}; ultimas 8: " + ", ".join(
        f"{v.hora_utc.iloc[i][11:16]}{'+' if v.dir.iloc[i] > 0 else '-'}{'R' if v.rapida.iloc[i] else 'L'}@{v.c.iloc[i]}" for i in vu[-8:]))
    print("PARIDAD", "EXACTA" if ok else "CON DIFERENCIAS")
    return ok


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "lab":
        # el motor_atas contra el motor del laboratorio (cal_03.corriente) sobre MES, sin correr la ronda entera
        sys.path.insert(0, str(Path(__file__).resolve().parent))
        import t_lib as T
        src = open(Path(__file__).resolve().parent / "cal_03.py", encoding="utf-8").read()
        src = src.split("# ------------------------------------------------------------------ control:")[0]
        src = src.replace('p = T.Salida("cal_03_salida.txt")', "p = print")
        g = {"__name__": "cal03", "__file__": str(Path(__file__).resolve().parent / "cal_03.py")}
        exec(compile(src, "cal_03_parcial", "exec"), g)
        S = g["Serie"](g["serie_mes"](), 20.0, 30, 10.0, 15, 5.0)
        lab = g["corriente"](S, S.atr(60), 4.0, v=2.5, j=3)
        tot = dif = 0
        for a, b in S.bl:
            r = motor_atas(S.h[a:b], S.l[a:b], S.c[a:b])
            sel = slice(400, None)          # despues de que las semillas del ATR(60) se olvidan
            dif += int((r["dir"][sel] != lab[a:b][sel]).sum())
            tot += len(r["dir"][sel])
        print(f"motor_atas vs motor del laboratorio (MES, G3 ST(60,4) v2.5 j3): {dif} velas distintas de {tot} (desde la vela 400 de cada bloque)")
    else:
        ruta = sys.argv[1] if len(sys.argv) > 1 else max(glob.glob(os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex", "corriente", "volcado-*.csv")), key=os.path.getmtime)
        comparar_volcado(ruta)
