# -*- coding: utf-8 -*-
"""ndx_00_velas.py — campeonato20, familia NDX (CBOE): las velas M1 del MNQ FRENTE para juzgar y para el 'fut' de la 4.1. SOLO LECTURA.

Receta (dicha, no escondida):
  * todas las sesiones del dataset unificado (calibracion_1009/datos, cargar.velas(dia) = serie PRINCIPAL del contrato frente:
    U6 hasta la sesion del 2026-09-14, Z6 desde la del 2026-09-15). Es la misma serie de velas con la que el dataset calculo la base
    C7 de NDX (precio del MNQ del contrato frente): velas y conversion quedan en el MISMO contrato.
  * la sesion en curso (2026-10-09) se extiende con juez20/velas_hasta_ahora.construir() (velas_m1.csv + centinela 'hoy' de la 2.0,
    MNQZ6) para las velas posteriores a la ultima del dataset (el dataset se armo a las 03:58Z).
  * se mide el solape entre las dos fuentes (sesiones Z6 comunes) y se escribe en la salida.
Escribe datos/velas_ndx.csv (t, o, h, l, c, contrato, sesion) y datos/velas_ndx_info.json. Uso: python -I ndx_00_velas.py"""
import ctypes
import json
import os
import sys

try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)   # prioridad baja: no trabar ATAS
except Exception:
    pass

import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
CAL1009 = os.path.normpath(os.path.join(AQUI, "..", ".."))
sys.path.insert(0, os.path.join(CAL1009, "datos"))
sys.path.insert(0, os.path.join(CAL1009, "campeonato20", "juez20"))
import cargar as D  # noqa: E402
from velas_hasta_ahora import construir  # noqa: E402

SALIDA = os.path.join(AQUI, "datos", "velas_ndx.csv")


def main():
    partes = []
    for d in D.dias():
        v = D.velas(d)
        if len(v):
            x = v[["t", "o", "h", "l", "c", "contrato"]].copy()
            x["sesion"] = d
            partes.append(x)
    V = pd.concat(partes, ignore_index=True).drop_duplicates("t", keep="last").sort_values("t").reset_index(drop=True)
    X, info_x = construir()
    info = {"dataset_desde": str(V["t"].min()), "dataset_hasta": str(V["t"].max()), "dataset_n": len(V),
            "construir": info_x}
    # solape (sesiones Z6): mismas velas?
    z = V[V["contrato"] == "Z6"]
    sol = z.merge(X, on="t", suffixes=("", "_x"))
    dif = {k: (sol[k] - sol[k + "_x"]).abs() for k in "ohlc"}
    ig = (dif["o"] < .01) & (dif["h"] < .01) & (dif["l"] < .01) & (dif["c"] < .01)
    info["solape_z6_n"] = len(sol)
    info["solape_z6_ohlc_identicas"] = int(ig.sum())
    info["solape_z6_cierre_a_1_tick"] = int((dif["c"] <= 0.25 + 1e-9).sum())
    info["solape_z6_max_dif"] = {k: float(dif[k].max()) for k in "ohlc"}
    ult = V["t"].max()
    ext = X[X["t"] > ult][["t", "o", "h", "l", "c"]].copy()
    ext["contrato"] = "Z6"
    ext["sesion"] = (ext["t"] + pd.Timedelta(hours=2)).dt.strftime("%Y-%m-%d")
    info["extension_n"] = len(ext)
    info["extension_desde"] = str(ext["t"].min()) if len(ext) else None
    info["extension_hasta"] = str(ext["t"].max()) if len(ext) else None
    out = pd.concat([V, ext], ignore_index=True).sort_values("t").reset_index(drop=True)
    info["n"] = len(out); info["desde"] = str(out["t"].min()); info["hasta"] = str(out["t"].max())
    out.to_csv(SALIDA, index=False)
    with open(os.path.join(AQUI, "datos", "velas_ndx_info.json"), "w", encoding="utf-8") as fh:
        json.dump(info, fh, ensure_ascii=False, indent=1)
    print(json.dumps(info, ensure_ascii=False, indent=1)[:3000])


if __name__ == "__main__":
    main()
