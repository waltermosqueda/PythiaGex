# -*- coding: utf-8 -*-
"""RONDA 20, paso 8 (C4, Enmienda 4, exploratorio y post hoc): la prueba de Baltussen et al. (2021) con nuestros datos.
Retorno del resto del dia = precio 15:30 NY - cierre RTH de la rueda anterior (mismo contrato; se salta el 14-09 por el roll);
ultima media hora = cierre 16:00 - precio 15:30. Regimen = signo del GEX neto a las 15:30 (tabla por minuto, dato <= 60 min).
Se mide el acuerdo de signos y la pendiente por regimen (ruedas como unidad). Uso: python -I r20_08_ultima_media_hora.py"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, pandas as pd
import r20_comun as C

tab = pd.read_parquet(os.path.join(C.SCR, "gex_minuto.parquet")).set_index(["dia", "minuto"])
filas = []
dias = C.dias_es()
for i, d in enumerate(dias):
    if i == 0 or d == C.ROLL_RESET:
        continue
    prev = C.meta19(dias[i - 1])
    c = C.rasgos19(d, ["c"])["c"]
    p1530, p16 = c[21600 - 1], c[-1]
    try:
        r = tab.loc[(d, 360)]
    except KeyError:
        continue
    ok = bool(r["valido"])
    filas.append({"dia": d, "muestra": C.muestra_de(d), "resto": p1530 - prev["rth_c"], "ultima": p16 - p1530, "X": C.meta19(d)["X"],
                  "neg": float(r["gex_neto"] < 0) if ok else np.nan, "gex": r["gex_neto"] if ok else np.nan})
F = pd.DataFrame(filas)
F.to_csv(os.path.join(C.AQUI, "r20_08_ultima_media_hora.csv"), index=False)
for per in ["diseno", "confirma", "guardada", "juntas"]:
    g = F if per == "juntas" else F[F.muestra == per]
    s = "  %-9s ruedas %2d" % (per, len(g))
    for v, nom in ((1.0, "NEG"), (0.0, "POS"), (None, "todas")):
        h = g if v is None else g[g.neg == v]
        if len(h) < 3:
            s += " | %s n %d" % (nom, len(h)); continue
        acuerdo = (np.sign(h.resto) == np.sign(h.ultima)).mean()
        b = np.polyfit(h.resto, h.ultima, 1)[0] if h.resto.std() > 0 else np.nan
        rr = np.corrcoef(h.resto, h.ultima)[0, 1]
        s += " | %s n %2d acuerdo de signo %3.0f %% pendiente %+.3f corr %+.2f" % (nom, len(h), 100 * acuerdo, b, rr)
    print(s)
