# -*- coding: utf-8 -*-
"""RONDA 19, paso 0: compacta cada cinta (una por vez, prioridad baja) a parquet en el scratchpad e inventaria:
ordenes, duplicados, horario cubierto, huecos de la rueda, precios de apertura/cierre y saltos entre jornadas (para el roll).
Uso: python -I r19_00_inventario.py"""
import os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r19_comun as C

TIPOS = {"primero": "float64", "ultimo": "float64", "vol": "int32", "lado": "int8", "prints": "int32",
         "abid": "float64", "abidv": "float64", "aask": "float64", "aaskv": "float64",
         "dbid": "float64", "dbidv": "float64", "dask": "float64", "daskv": "float64"}


def compactar(inst, dia, f):
    d = pd.read_csv(f, dtype=TIPOS)
    n0 = len(d)
    d = d.drop_duplicates()
    t = pd.to_datetime(d["t"], format="%Y-%m-%dT%H:%M:%S.%f")
    ns = t.astype("int64").to_numpy()
    orden = np.argsort(ns, kind="stable")
    d = d.iloc[orden].reset_index(drop=True); ns = ns[orden]
    medianoche_ny_utc = (pd.Timestamp(dia) + pd.Timedelta(hours=C.ny_offset_h(dia))).value
    out = pd.DataFrame({"ns": ns, "ny_s": (ns - medianoche_ny_utc) / 1e9})
    for c in ("primero", "ultimo", "vol", "lado", "abid", "abidv", "aask", "aaskv", "dbid", "dbidv", "dask", "daskv"):
        out[c] = d[c].to_numpy()
    out.to_parquet(C.ruta_compacta(inst, dia), index=False)
    return out, n0


def resumen(inst, dia, d, n0):
    s = d["ny_s"].to_numpy(); px = d["ultimo"].to_numpy(); p0 = d["primero"].to_numpy()
    hi = np.maximum(px, p0); lo = np.minimum(px, p0)
    rth = (s >= 9.5 * 3600) & (s < 16 * 3600)
    r = {"inst": inst, "dia": dia, "ordenes": len(d), "duplicados": n0 - len(d), "contratos": int(d["vol"].sum()),
         "desde_ny_h": round(s[0] / 3600, 3), "hasta_ny_h": round(s[-1] / 3600, 3),
         "p_inicio": px[0], "p_fin": px[-1], "rth_ordenes": int(rth.sum())}
    if rth.any():
        sr = s[rth]; gaps = np.diff(sr)
        r.update({"rth_o": px[rth][0], "rth_c": px[rth][-1], "rth_h": hi[rth].max(), "rth_l": lo[rth].min(),
                  "rth_desde": round(sr[0] / 3600, 4), "rth_hasta": round(sr[-1] / 3600, 4),
                  "rth_hueco_max_s": round(float(gaps.max()), 1) if len(gaps) else None,
                  "rth_huecos_30s": int((gaps > 30).sum()), "rth_huecos_60s": int((gaps > 60).sum())})
        # huecos largos de la rueda: donde estan
        idx = np.nonzero(gaps > 60)[0]
        r["rth_huecos_donde"] = ";".join("%.2fh+%ds" % (sr[i] / 3600, gaps[i]) for i in idx[:6])
    noche = s < 9.5 * 3600
    if noche.any():
        gaps = np.diff(s[noche]); r["noche_hueco_max_s"] = round(float(gaps.max()), 1) if len(gaps) else None
        r["noche_huecos_300s"] = int((gaps > 300).sum())
    return r


def main():
    filas = []
    for inst in ("ES", "MES"):
        for dia, f in sorted(C.cintas_crudas(inst).items()):
            C.frenar_si_falta_ram(2.0)
            t0 = time.time()
            if os.path.exists(C.ruta_compacta(inst, dia)):
                d = C.leer(inst, dia); n0 = len(d)   # ya compactada (duplicados ya sacados)
            else:
                d, n0 = compactar(inst, dia, f)
            filas.append(resumen(inst, dia, d, n0))
            print(inst, dia, len(d), "ordenes", "%.1f s" % (time.time() - t0), flush=True)
            del d
    inv = pd.DataFrame(filas)
    # saltos entre jornadas consecutivas del mismo instrumento: fin de la jornada d (17:00) contra inicio de d+1 (18:00)
    inv["salto_vs_previa"] = np.nan
    for inst in ("ES", "MES"):
        m = inv["inst"] == inst
        sub = inv[m].sort_values("dia")
        prev_fin = sub["p_fin"].shift(1)
        inv.loc[sub.index, "salto_vs_previa"] = (sub["p_inicio"] - prev_fin).round(2)
    inv.to_csv(os.path.join(C.AQUI, "r19_00_inventario.csv"), index=False)
    pd.set_option("display.width", 250); pd.set_option("display.max_columns", 40)
    print(inv[["inst", "dia", "ordenes", "duplicados", "desde_ny_h", "hasta_ny_h", "p_inicio", "p_fin", "rth_o", "rth_c", "rth_h", "rth_l",
               "rth_hueco_max_s", "rth_huecos_60s", "noche_huecos_300s", "salto_vs_previa"]].to_string())


if __name__ == "__main__":
    main()
