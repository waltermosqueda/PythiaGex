# -*- coding: utf-8 -*-
"""RONDA 17, paso 0: arma los datos de la anatomia (una vez; prioridad baja; una cinta por vez).
MES 16-20 y 23-25-11: tabla de 1 s (misma funcion que base.py), niveles del dia, footprint por segundo y tick de la rueda,
ordenes grandes. ES (ruedas de noviembre de las tandas 0 y 1): footprint por segundo y tick de la rueda y ordenes grandes
(la tabla de 1 s ya esta en datos/flujo/cache/ES). Semanas y mes anteriores desde las tablas de 1 s.
Uso: python -I r17_00_datos.py"""
import json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r17_comun as C

MES_DIAS = ["2026-11-16", "2026-11-17", "2026-11-18", "2026-11-19", "2026-11-20", "2026-11-23", "2026-11-24", "2026-11-25"]
ES_DIAS = ["2026-11-06", "2026-11-09", "2026-11-11", "2026-11-02", "2026-11-03", "2026-11-04", "2026-11-10", "2026-11-12", "2026-11-13"]
SEMANAS = {("ES", "2026-10-26"): ["2026-10-26", "2026-10-27", "2026-10-28", "2026-10-29", "2026-10-30"],
           ("ES", "2026-11-02"): ["2026-11-02", "2026-11-03", "2026-11-04", "2026-11-05", "2026-11-06"],
           ("ES", "2026-11-09"): ["2026-11-09", "2026-11-10", "2026-11-11", "2026-11-12", "2026-11-13"],
           ("MES", "2026-11-16"): ["2026-11-16", "2026-11-17", "2026-11-18", "2026-11-19", "2026-11-20"]}
OCTUBRE = ["2026-10-%02d" % d for d in (1, 2, 5, 6, 7, 8, 9, 12, 13, 14, 15, 16, 19, 20, 21, 22, 23, 26, 27, 28, 29, 30)]


def rueda(d, dia):
    t0 = pd.Timestamp(dia) + pd.Timedelta(hours=9, minutes=30); t1 = pd.Timestamp(dia) + pd.Timedelta(hours=16)
    return d[(d["t_ny"] >= t0) & (d["t_ny"] < t1)]


def grandes_y_cuantiles(r):
    v = r["vol"].to_numpy("float64")
    q = {k: float(np.quantile(v, p)) for k, p in (("q95", 0.95), ("q99", 0.99), ("q999", 0.999))}
    corte = min(q["q99"], 95.0)
    g = r[r["vol"] >= corte]
    out = pd.DataFrame({"t": g["t_ny"].dt.floor("s").to_numpy(), "lado": g["lado"].to_numpy(), "vol": g["vol"].to_numpy("float64"),
                        "pmax": g[["primero", "ultimo"]].max(axis=1).to_numpy(), "pmin": g[["primero", "ultimo"]].min(axis=1).to_numpy(),
                        "barre": (g["primero"] != g["ultimo"]).to_numpy()})
    q["n_ordenes"] = int(len(v)); q["vol_medio"] = float(v.mean()); q["mediana"] = float(np.median(v))
    return out, q


def main():
    os.makedirs(C.R17, exist_ok=True)
    cuant = {}; niv = []
    for inst, dias in (("MES", MES_DIAS), ("ES", ES_DIAS)):
        for dia in dias:
            t0 = time.time()
            f = C.archivo_cinta(inst, dia)
            if f is None:
                print("FALTA cinta", inst, dia, flush=True); continue
            d = C.leer_cinta(f)
            if inst == "MES":
                s = C.tabla_1s_de_cinta(d)
                s.to_parquet(os.path.join(C.R17, "seg-%s-%s.parquet" % (inst, dia)))
                niv.append({"inst": inst, **C.niveles_de_cinta(d, dia)})
            r = rueda(d, dia)
            fp = C.explotar(r)
            fp.to_parquet(os.path.join(C.R17, "fps-%s-%s.parquet" % (inst, dia)))
            g, q = grandes_y_cuantiles(r)
            g.to_parquet(os.path.join(C.R17, "ord-%s-%s.parquet" % (inst, dia)))
            cuant["%s %s" % (inst, dia)] = q
            print(inst, dia, "ordenes", len(d), "rueda", len(r), "fp filas", len(fp), "grandes", len(g), "q99 %.0f q99.9 %.0f" % (q["q99"], q["q999"]),
                  "%.1f s" % (time.time() - t0), flush=True)
            del d, r, fp
    pd.DataFrame(niv).to_parquet(os.path.join(C.R17, "niveles-MES.parquet"))
    json.dump(cuant, open(os.path.join(C.R17, "cuantiles_ordenes.json"), "w"), indent=1)
    filas = []
    for (inst, w), dias in SEMANAS.items():
        tablas = [C.tabla_1s(inst, x) for x in dias]
        filas.append({"inst": inst, "periodo": "semana", "desde": w, **C.niveles_periodo(tablas, dias[-1])})
    tablas = [C.tabla_1s("ES", x) for x in OCTUBRE]
    filas.append({"inst": "ES", "periodo": "mes", "desde": "2026-10-01", **C.niveles_periodo(tablas, OCTUBRE[-1])})
    p = pd.DataFrame(filas); p.to_parquet(os.path.join(C.R17, "periodos.parquet")); print(p.to_string())


if __name__ == "__main__":
    main()
