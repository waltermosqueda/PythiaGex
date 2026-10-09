# -*- coding: utf-8 -*-
"""velas_hasta_ahora.py — velas M1 de MNQZ6 hasta la ultima vela CERRADA de esta noche (agente juez20, 09-10-2026). SOLO LECTURA.
Misma receta que criterio_operador/v41/v41_00_velas.py (no se cambia nada):
  base = velas_m1.csv del investigador previo (scratchpad/calibrar, 09-13 22:10Z -> 10-09 03:22Z, validadas 98,1 % contra la cinta);
  extension = centinela 'hoy' de la 2.0 (%APPDATA%/ATAS/pythiagex2-centinela-hoy-MNQZ6-TimeFrame-M1.jsonl; t = hh:mm:59 -> piso al
  minuto = inicio de la vela), descartando su ultima linea (puede ser la vela en formacion). Se mide el solape con velas_m1.csv.
Escribe datos/velas_m1_juez20.csv (t, o, h, l, c, vol). Uso: python -I velas_hasta_ahora.py"""
import ctypes
import json
import os

import pandas as pd

try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)   # prioridad baja: no trabar ATAS
except Exception:
    pass

AQUI = os.path.dirname(os.path.abspath(__file__))
AP = os.path.join(os.environ["APPDATA"], "ATAS")
CAL = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "calibrar")
SALIDA = os.path.join(AQUI, "datos", "velas_m1_juez20.csv")


def construir():
    V = pd.read_csv(os.path.join(CAL, "velas_m1.csv"), parse_dates=["t"])
    filas = []
    with open(os.path.join(AP, "pythiagex2-centinela-hoy-MNQZ6-TimeFrame-M1.jsonl"), encoding="utf-8", errors="replace") as fh:
        for ln in fh:
            ln = ln.strip()
            if not ln.endswith("}"):
                continue
            try:
                r = json.loads(ln)
            except Exception:
                continue
            filas.append((pd.Timestamp(r["t"]).floor("1min"), r["o"], r["h"], r["l"], r["c"], r.get("vol", 0)))
    H = pd.DataFrame(filas, columns=["t", "o", "h", "l", "c", "vol"]).drop_duplicates("t", keep="last").sort_values("t")
    info = {"centinela_desde": str(H["t"].min()), "centinela_hasta": str(H["t"].max()), "centinela_n": len(H)}
    H = H.iloc[:-1]
    sol = V.merge(H, on="t", suffixes=("", "_h"))
    dif = {k: (sol[k] - sol[k + "_h"]).abs() for k in "ohlc"}
    info["solape_n"] = len(sol)
    info["solape_iguales"] = int(((dif["o"] < .01) & (dif["h"] < .01) & (dif["l"] < .01) & (dif["c"] < .01)).sum())
    info["solape_max_dif"] = {k: float(dif[k].max()) if len(sol) else None for k in "ohlc"}
    nuevo = H[H["t"] > V["t"].max()]
    out = pd.concat([V[["t", "o", "h", "l", "c", "vol"]], nuevo], ignore_index=True).sort_values("t").reset_index(drop=True)
    info["nuevas"] = len(nuevo)
    info["desde"] = str(out["t"].min()); info["hasta"] = str(out["t"].max()); info["n"] = len(out)
    g = out["t"].diff().dt.total_seconds().div(60)
    tt = out["t"][out["t"] >= out["t"].max() - pd.Timedelta(hours=10)]
    gg = tt.diff().dt.total_seconds().div(60)
    info["huecos_mayores_1min_ult_10h"] = [(str(a), float(b)) for a, b in zip(tt[gg > 1], gg[gg > 1])]
    info["huecos_mayores_30min"] = int((g > 30).sum())
    return out, info


def main():
    out, info = construir()
    os.makedirs(os.path.dirname(SALIDA), exist_ok=True)
    out.to_csv(SALIDA, index=False)
    print(json.dumps(info, indent=1, ensure_ascii=False))
    print("escrito", SALIDA)


if __name__ == "__main__":
    main()
