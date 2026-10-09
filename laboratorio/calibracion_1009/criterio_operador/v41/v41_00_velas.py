# -*- coding: utf-8 -*-
"""v41_00_velas.py — velas M1 de MNQZ6 para juzgar la 4.1 (agente v41, 09-10-2026). SOLO LECTURA de los datos.
Base: velas_m1.csv del investigador previo (scratchpad/calibrar, 09-13 22:10Z -> 10-09 03:22Z, validadas 98,1 % contra la cinta).
Extension hasta la ultima vela CERRADA de esta noche con la MISMA fuente que uso el (centinela 'hoy' de la 2.0,
%APPDATA%/ATAS/pythiagex2-centinela-hoy-MNQZ6-TimeFrame-M1.jsonl; t = hh:mm:59 -> piso al minuto = inicio de la vela).
Se descarta la ultima linea del centinela (puede ser la vela en formacion). Se verifica el solape con velas_m1.csv.
Escribe datos/velas_v41.csv (t, o, h, l, c, vol)."""
import json
import os

import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
AP = os.path.join(os.environ["APPDATA"], "ATAS")
CAL = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "calibrar")


def main():
    V = pd.read_csv(os.path.join(CAL, "velas_m1.csv"), parse_dates=["t"])
    filas = []
    with open(os.path.join(AP, "pythiagex2-centinela-hoy-MNQZ6-TimeFrame-M1.jsonl"), encoding="utf-8", errors="replace") as fh:
        for l in fh:
            l = l.strip()
            if not l.endswith("}"):
                continue
            try:
                r = json.loads(l)
            except Exception:
                continue
            filas.append((pd.Timestamp(r["t"]).floor("1min"), r["o"], r["h"], r["l"], r["c"], r.get("vol", 0)))
    H = pd.DataFrame(filas, columns=["t", "o", "h", "l", "c", "vol"]).drop_duplicates("t", keep="last").sort_values("t")
    print("centinela hoy: %d velas %s -> %s (se descarta la ultima: puede estar en formacion)" % (len(H), H["t"].min(), H["t"].max()))
    H = H.iloc[:-1]
    sol = V.merge(H, on="t", suffixes=("", "_h"))
    dif = {k: (sol[k] - sol[k + "_h"]).abs() for k in "ohlc"}
    print("solape con velas_m1.csv: %d velas; iguales (o,h,l,c a 0,01): %d; max |dif| o %.2f h %.2f l %.2f c %.2f" % (
        len(sol), int(sum(((dif["o"] < .01) & (dif["h"] < .01) & (dif["l"] < .01) & (dif["c"] < .01))) ),
        dif["o"].max(), dif["h"].max(), dif["l"].max(), dif["c"].max()))
    nuevo = H[H["t"] > V["t"].max()]
    out = pd.concat([V[["t", "o", "h", "l", "c", "vol"]], nuevo], ignore_index=True).sort_values("t").reset_index(drop=True)
    os.makedirs(os.path.join(AQUI, "datos"), exist_ok=True)
    out.to_csv(os.path.join(AQUI, "datos", "velas_v41.csv"), index=False)
    print("velas_v41.csv: %d velas %s -> %s (+%d nuevas)" % (len(out), out["t"].min(), out["t"].max(), len(nuevo)))
    # huecos de mas de 30 min dentro de la ultima semana (informativo)
    tt = out["t"]
    g = tt.diff().dt.total_seconds().div(60)
    print("huecos > 30 min: %d (los fines de semana y la pausa de CME 21-22Z son normales)" % int((g > 30).sum()))


if __name__ == "__main__":
    main()
