# -*- coding: utf-8 -*-
"""esc_08_paridad.py — ESCEPTICO 1: re-mide por mi cuenta la PARIDAD de las rayas reconstruidas contra lo que la 4.1 GRABO en vivo esta
noche (PythiaGex4/familia/niv-2026-10-09-MNQZ6.jsonl), clave por clave: MUROS/MAJORS NQ (registros-2026-10-09 de la familia NQ, armados
con todos los grabadores por prioridad, que es lo que se juzgo) y MUROS/MAJORS/ZTP/DOMS/R20 de NDX (ndx_2026-10-09.pkl). Es la unica
noche con registro en vivo. Tambien: ¿la 4.1 en vivo grabo en la clave k algo distinto de lo reconstruido con datos hasta k? (si la
reconstruccion usara datos posteriores, difiere de lo grabado). SOLO LECTURA. Uso: python -I esc_08_paridad.py"""
import ctypes
try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass
import json
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import numpy as np  # noqa: E402
import pandas as pd  # noqa: E402
import esc_comun as E  # noqa: E402

NIV = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex4", "familia", "niv-2026-10-09-MNQZ6.jsonl")
LOG = []


def out(s):
    print(s, flush=True); LOG.append(s)


def leer_niv():
    d = {}
    with open(NIV, encoding="utf-8", errors="replace") as fh:
        for ln in fh:
            ln = ln.strip()
            if not ln.endswith("}"):
                continue
            try:
                r = json.loads(ln)
            except Exception:
                continue
            if "k" in r:
                d[int(r["k"])] = r.get("s") or {}
    return d


def comparar(nombre, niv, rec, tol=0.02):
    """niv: {k: {rol: precio}}, rec: {k: {rol: precio}} -> minutos comunes con algo en alguno, identicos (mismos roles, |dif| <= tol)."""
    n = ig = 0; difs = []
    for k in sorted(set(niv) & set(rec)):
        a, b = niv[k], rec[k]
        if not a and not b:
            continue
        n += 1
        if set(a) == set(b) and all(abs(a[x] - b[x]) <= tol for x in a):
            ig += 1
        else:
            for x in set(a) & set(b):
                difs.append(abs(a[x] - b[x]))
    ks = sorted(set(niv) & set(rec))
    solo_niv = sum(1 for k in ks if niv[k] and not rec[k]); solo_rec = sum(1 for k in ks if rec[k] and not niv[k])
    amb = [k for k in ks if niv[k] and rec[k]]
    ig2 = sum(1 for k in amb if set(niv[k]) == set(rec[k]) and all(abs(niv[k][x] - rec[k][x]) <= tol for x in niv[k]))
    hs = sorted(pd.Timestamp(k * 60, unit="s").strftime("%H:%M") for k in ks if rec[k] and not niv[k])
    out("PARIDAD %-16s minutos %4d | identicos %4d (%.1f %%) | con las dos: %d, identicos %d (%.1f %%) | solo 4.1 %d | solo reconstruida %d (%s..%s) | dif mediana %s" % (
        nombre, n, ig, 100.0 * ig / n if n else float("nan"), len(amb), ig2, 100.0 * ig2 / len(amb) if amb else float("nan"), solo_niv, solo_rec,
        hs[0] if hs else "-", hs[-1] if hs else "-", "%.2f" % np.median(difs) if difs else "-"))
    return dict(minutos=n, identicos=ig, pct=100.0 * ig / n if n else None, con_las_dos=len(amb), identicos_con_las_dos=ig2,
                solo_41=solo_niv, solo_reconstruida=solo_rec)


def main():
    niv = leer_niv()
    res = {}
    reg = pd.read_pickle(os.path.join(E.NQD, "datos", "registros-2026-10-09.pkl"))
    for s in ("MUROS_NQ_vol", "MAJORS_NQ_vol", "ZTP_NQ_vol", "MUROS_NQ_oi"):
        a = {k: {e[1]: float(e[0]) for e in v.get(s, [])} for k, v in niv.items()}
        b = {r["k"]: {e: float(p) for p, e, K in r["series"].get(s, [])} for r in reg}
        res[s] = comparar(s, a, b)
    x = pd.read_pickle(os.path.join(E.NDX, "datos", "series", "ndx_2026-10-09.pkl"))
    for s in ("MUROS_NDX_vol", "MAJORS_NDX_vol", "ZTP_NDX_vol", "DOMS_NDX_vol", "R20_NDX_vol"):
        a = {k: {e[1]: float(e[0]) for e in v.get(s, [])} for k, v in niv.items()}
        b = {int(t.value // 60_000_000_000): {rol: float(p) for rol, (p, et) in (v or {}).items()} for t, v in x["series"][s].items()}
        res[s] = comparar(s, a, b)
    json.dump(res, open(os.path.join(AQUI, "datos", "paridad_propia.json"), "w", encoding="utf-8"), indent=1)
    open(os.path.join(AQUI, "datos", "paridad_propia.txt"), "w", encoding="utf-8").write("\n".join(LOG) + "\n")


if __name__ == "__main__":
    main()
