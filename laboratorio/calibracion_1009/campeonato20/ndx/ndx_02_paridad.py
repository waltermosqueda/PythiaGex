# -*- coding: utf-8 -*-
"""ndx_02_paridad.py — PARIDAD de la reconstruccion (ndx_01_series.py) contra lo que GRABO la 4.1 real esta noche
(%APPDATA%/ATAS/PythiaGex4/familia/niv-2026-10-09-MNQZ6.jsonl). SOLO LECTURA. Es un control de FIDELIDAD de la copia (no mira aciertos).
Por minuto comun y por serie: misma cantidad de rayas, y |precio reconstruido - precio grabado| por lado (D1/D2/Z): identico (<= 0,01),
<= 0,5, <= 2 pts. Tambien el fut del minuto, la base C7 ('c' de NDX) y la base de la 2.0 ('mx' 2.0 NDX). La ZEST se mide con las dos
caches del zero ('tanda' y 'fresco'). Escribe datos/paridad_0910.txt y .json. Uso: python -I ndx_02_paridad.py"""
import ctypes
import json
import os
import sys

try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
JSONL = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex4", "familia", "niv-2026-10-09-MNQZ6.jsonl")
SERIES = ("MUROS_NDX_vol", "MUROS_NDX_oi", "MAJORS_NDX_vol", "MAJORS_NDX_oi", "ZTP_NDX_vol", "ZEST_NDX_vol", "DOMS_NDX_vol", "R20_NDX_vol")


def leer_jsonl():
    reg = {}
    with open(JSONL, encoding="utf-8", errors="replace") as fh:
        for ln in fh:
            ln = ln.strip()
            if not ln.endswith("}"):
                continue
            try:
                r = json.loads(ln)
            except Exception:
                continue
            if "k" not in r:
                continue
            t = pd.Timestamp(r["k"] * 60, unit="s")
            base = next((x["c"] for x in r.get("m", []) if x.get("l") == "NDX"), None)
            b20 = next((x["c"] for x in r.get("mx", []) or [] if x.get("l") == "2.0 NDX"), None)
            s = {k: {e[1]: (float(e[0]), e[3]) for e in v} for k, v in (r.get("s") or {}).items() if k in SERIES}
            reg[t] = {"f": r.get("f"), "base": base, "b20": b20, "s": s}
    return reg


def comparar(rec, grab, ts):
    n = 0; misma_n = 0; dif = []; solo_rec = 0; solo_grab = 0; ejemplos = []
    for t in ts:
        a = rec.get(t, {}) or {}
        b = grab.get(t, {}) or {}
        if not a and not b:
            continue
        n += 1
        if set(a) == set(b):
            misma_n += 1
        for lado in set(a) | set(b):
            if lado in a and lado in b:
                d = abs(a[lado][0] - b[lado][0])
                dif.append(d)
                if d > 0.5 and len(ejemplos) < 4:
                    ejemplos.append((str(t), lado, round(a[lado][0], 2), b[lado][0], a[lado][1], b[lado][1]))
            elif lado in a:
                solo_rec += 1
            else:
                solo_grab += 1
                if len(ejemplos) < 4:
                    ejemplos.append((str(t), lado, None, b[lado][0], None, b[lado][1]))
    d = np.array(dif)
    return {"minutos_con_algo": n, "mismos_lados": misma_n, "pares": len(d),
            "identico_001": float((d <= 0.011).mean()) if len(d) else None, "a_05": float((d <= 0.5).mean()) if len(d) else None,
            "a_2": float((d <= 2.0).mean()) if len(d) else None, "dif_max": float(d.max()) if len(d) else None,
            "solo_reconstruido": solo_rec, "solo_grabado": solo_grab, "ejemplos": ejemplos}


def main():
    G = leer_jsonl()
    X = pd.read_pickle(os.path.join(AQUI, "datos", "series", "ndx_2026-10-09.pkl"))
    M = X["meta"].set_index("t")
    ts = sorted(set(G) & set(M.index))
    out = {"jsonl": JSONL, "minutos_jsonl": len(G), "minutos_comunes": len(ts), "desde": str(ts[0]), "hasta": str(ts[-1])}
    f_rec = M.loc[ts, "fut"].to_numpy(float); f_g = np.array([G[t]["f"] if G[t]["f"] is not None else np.nan for t in ts], float)
    ok = np.isfinite(f_rec) & np.isfinite(f_g)
    out["fut_identico"] = float((np.abs(f_rec[ok] - f_g[ok]) < 0.01).mean()); out["fut_n"] = int(ok.sum())
    b_rec = M.loc[ts, "base"].to_numpy(float); b_g = np.array([G[t]["base"] if G[t]["base"] is not None else np.nan for t in ts], float)
    ok = np.isfinite(b_rec) & np.isfinite(b_g)
    out["base_c7_dif_max"] = float(np.abs(b_rec[ok] - b_g[ok]).max()) if ok.any() else None; out["base_c7_n"] = int(ok.sum())
    r_rec = M.loc[ts, "r20_base"].to_numpy(float); r_g = np.array([G[t]["b20"] if G[t]["b20"] is not None else np.nan for t in ts], float)
    ok = np.isfinite(r_rec) & np.isfinite(r_g)
    out["base_r20_dif_max"] = float(np.abs(r_rec[ok] - r_g[ok]).max()) if ok.any() else None; out["base_r20_n"] = int(ok.sum())
    out["series"] = {}
    for s in SERIES:
        grab = {t: G[t]["s"].get(s, {}) for t in ts}
        if s == "ZEST_NDX_vol":
            for mz, ser in X["zest_modos"].items():
                out["series"][s + "[" + mz + "]"] = comparar(ser, grab, ts)
        else:
            out["series"][s] = comparar(X["series"][s], grab, ts)
        # R20/DOMS: solo desde que la 4.1.3 las graba
        prim = min((t for t in ts if grab[t]), default=None)
        clave = s + "[tanda]" if s == "ZEST_NDX_vol" else s
        out["series"][clave]["primer_minuto_grabado"] = str(prim) if prim is not None else None
        if prim is not None and s in ("R20_NDX_vol", "DOMS_NDX_vol"):
            ts2 = [t for t in ts if t >= prim]
            out["series"][s + "[desde_grabado]"] = comparar(X["series"][s], grab, ts2)
    with open(os.path.join(AQUI, "datos", "paridad_0910.json"), "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1, default=str)
    lineas = ["PARIDAD reconstruccion NDX vs jsonl real de la 4.1 (sesion 2026-10-09): %d minutos comunes (%s .. %s)" % (
        len(ts), out["desde"], out["hasta"]),
        "fut identico %.1f %% de %d | base C7 dif max %s (%d min) | base 2.0 dif max %s (%d min)" % (
            100 * out["fut_identico"], out["fut_n"], out["base_c7_dif_max"], out["base_c7_n"], out["base_r20_dif_max"], out["base_r20_n"])]
    for s, r in out["series"].items():
        if r is None:
            continue
        lineas.append("%-28s min %4d mismos lados %4d | pares %4d identico %s <=0,5 %s <=2 %s max %s | solo rec %d solo grab %d | %s" % (
            s, r["minutos_con_algo"], r["mismos_lados"], r["pares"],
            "%.1f%%" % (100 * r["identico_001"]) if r["identico_001"] is not None else "-",
            "%.1f%%" % (100 * r["a_05"]) if r["a_05"] is not None else "-",
            "%.1f%%" % (100 * r["a_2"]) if r["a_2"] is not None else "-",
            "%.2f" % r["dif_max"] if r["dif_max"] is not None else "-", r["solo_reconstruido"], r["solo_grabado"], r["ejemplos"][:2]))
    txt = "\n".join(lineas)
    with open(os.path.join(AQUI, "datos", "paridad_0910.txt"), "w", encoding="utf-8") as fh:
        fh.write(txt + "\n")
    print(txt)


if __name__ == "__main__":
    main()
