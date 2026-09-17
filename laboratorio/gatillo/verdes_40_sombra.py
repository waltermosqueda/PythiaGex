# -*- coding: utf-8 -*-
"""
verdes_40_sombra.py — EL JUEZ: la cuenta EN SOMBRA del reclamo en las verdes, leida del registro del indicador en vivo
(%APPDATA%/ATAS/PythiaGex/flujo/verdes-NQ-<dia>.jsonl, Flujo Claro >= 1.7, nucleo unico VerdesNucleo.cs).

CRITERIO ESCRITO EL 17-09-2026, ANTES DE JUNTAR DATOS (no se toca):
  - cuentan solo las ruedas POSTERIORES al 17-09, solo 13:30-20:00 UTC, con la regla congelada (cruce + delta 10 s, objetivo 20, stop 1 pt tras la mecha);
  - a las 30 ruedas: la pista SIRVE si el neto por operacion de las VERDES tiene intervalo del 90 % por dias arriba de cero Y supera al de las rayas de CONTROL
    (corridas +-37,5 / +-62,5) del mismo registro; recien ahi se discute un bot con ordenes;
  - a las 20 ruedas: si el neto acumulado por operacion de las verdes es menor que +0,5, se ABANDONA;
  - la noche (fuera de rueda) se informa aparte y no decide nada.
Uso: python laboratorio/gatillo/verdes_40_sombra.py
"""
import os, sys, json, glob
import numpy as np, pandas as pd

FLUJO = os.path.join(os.environ.get("APPDATA", ""), "ATAS", "PythiaGex", "flujo"); COSTO = 0.96; DESDE = "2026-09-18"


def cargar():
    gat = {}; filas = []
    for p in sorted(glob.glob(os.path.join(FLUJO, "verdes-NQ-*.jsonl"))):
        for l in open(p, encoding="utf-8", errors="replace"):
            try: e = json.loads(l)
            except Exception: continue
            k = (e.get("id"), e.get("corr"))
            if e.get("ev") == "gatillo": gat[k] = e
            elif e.get("ev") == "resultado" and k in gat:
                g = gat[k]; filas.append(dict(dia=g["utc"][:10], utc=g["utc"], corr=g["corr"], rueda=g["rueda"], libro=g.get("libro"), lado=g["lado"], fuerza=g.get("fuerza", 0), traspaso=g["traspaso"],
                                               puntos=e["puntos"], neto=e["puntos"] - COSTO, resultado=e["resultado"]))
    return pd.DataFrame(filas).drop_duplicates(["utc", "corr", "lado"]) if filas else pd.DataFrame()


def linea(nombre, d):
    if not len(d): return "%-36s sin operaciones" % nombre
    dias = list(d["dia"].unique()); rng = np.random.default_rng(7)
    bs = [pd.concat([d[d["dia"] == x] for x in rng.choice(dias, len(dias))])["neto"].mean() for _ in range(4000)] if len(dias) >= 3 else [np.nan]
    lo, hi = (np.percentile(bs, [5, 95]) if len(dias) >= 3 else (np.nan, np.nan)); pd_ = d.groupby("dia")["neto"].mean()
    return "%-36s n %4d | gana %4.1f %% | neto %+5.2f pts/op (total %+7.1f) | IC 90 %% por dias [%+.2f ; %+.2f] | dias positivos %d de %d" % (
        nombre, len(d), 100 * (d["puntos"] > 0).mean(), d["neto"].mean(), d["neto"].sum(), lo, hi, int((pd_ > 0).sum()), len(pd_))


if __name__ == "__main__":
    D = cargar()
    if not len(D): sys.exit("todavia no hay operaciones en sombra cerradas")
    print("registro en sombra: %d operaciones cerradas, dias %s a %s" % (len(D), D["dia"].min(), D["dia"].max()))
    J = D[(D["dia"] >= DESDE) & D["rueda"]]; ruedas = J["dia"].nunique()
    print("\n== LO QUE DECIDE: ruedas posteriores al 17-09, solo 13:30-20:00 UTC (%d ruedas de 30)" % ruedas)
    print("   " + linea("VERDES", J[J["corr"] == 0])); print("   " + linea("CONTROL (rayas corridas)", J[J["corr"] != 0]))
    v = J[J["corr"] == 0]
    if len(v): print("   por dia: " + " | ".join("%s %+.1f (%d)" % (k[5:], g["neto"].sum(), len(g)) for k, g in v.groupby("dia")))
    if ruedas >= 20 and len(v) and v["neto"].mean() < 0.5: print("   >>> 20 ruedas o mas y neto < +0,5 por operacion: segun el criterio escrito, SE ABANDONA.")
    N = D[~D["rueda"]]
    print("\n== fuera de rueda (informativo)"); print("   " + linea("VERDES", N[N["corr"] == 0])); print("   " + linea("CONTROL", N[N["corr"] != 0]))
