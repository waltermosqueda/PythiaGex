# -*- coding: utf-8 -*-
"""RONDA 20, paso 6: la MECANICA (quien cubre que y cuando), descriptiva. Por cada pierna de la ola (X) de la rueda (pivotes de la
ronda 19, los mismos de la R18), se compara el flujo que HARIAN los dealers por cubrir gamma (cobertura_es_por_X de la tabla, con
el supuesto de signo) contra lo que movio la pierna en la cinta: delta neto (agresores) y volumen por cada X de recorrido.
Tambien: cuanto vale el GEX y la distancia al flip por periodo y por hora, y que parte del GEX es 0DTE.
Uso: python -I r20_06_mecanica.py"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import pandas as pd

import r20_comun as C

tab = pd.read_parquet(os.path.join(C.SCR, "gex_minuto.parquet"))
tab["valido"] = tab.hay_cadena & (tab.edad_s <= 3600)
tv = tab.set_index(["dia", "minuto"])
filas = []
for dia in C.dias_es():
    R = C.rasgos19(dia, ["o_piv", "cum", "vol"])
    P = R["o_piv"]; cum = R["cum"]; vol = R["vol"]
    cv = np.concatenate([[0.0], np.cumsum(vol)])
    X = float(C.meta19(dia)["X"])
    for a, b in zip(P[:-1], P[1:]):
        s0, s1 = int(a[0]), int(b[0])
        if s0 < 0 or s1 >= 23400 or s1 <= s0:
            continue
        largo = abs(b[1] - a[1])
        mi = int(a[3]) // 60 if a[3] >= 0 else 0          # minuto de la confirmacion del pivote que arranca la pierna
        try:
            r = tv.loc[(dia, min(389, mi))]
        except KeyError:
            continue
        filas.append({"dia": dia, "muestra": C.muestra_de(dia), "X": X, "largo_X": largo / X, "sube": int(b[1] > a[1]),
                      "delta": float(b[4] - a[4]), "vol": float(cv[s1 + 1] - cv[s0]), "dur_s": s1 - s0,
                      "valido": bool(r["valido"]), "gex_neto": r["gex_neto"], "gex_0dte": r["gex_0dte"],
                      "cob_X": r["cobertura_es_por_X"], "minuto": mi})
L = pd.DataFrame(filas)
L = L[L.valido]
L["delta_por_X"] = L.delta.abs() / L.largo_X
L["vol_por_X"] = L.vol / L.largo_X
L["cob_sobre_delta"] = L.cob_X.abs() / L.delta_por_X
L["cob_sobre_vol"] = L.cob_X.abs() / L.vol_por_X
L["reg"] = np.where(L.gex_neto < 0, "neg", "pos")
print("Piernas de la ola en la rueda con regimen valido: %d en %d ruedas" % (len(L), L.dia.nunique()))
print("\nPor periodo y regimen (medianas): |cobertura teorica| por X, |delta| de la pierna por X, volumen por X, y los cocientes")
g = L.groupby(["muestra", "reg"]).agg(n=("dia", "size"), ruedas=("dia", "nunique"), cob_X=("cob_X", lambda s: s.abs().median()),
                                     delta_por_X=("delta_por_X", "median"), vol_por_X=("vol_por_X", "median"),
                                     cob_sobre_delta=("cob_sobre_delta", "median"), cob_sobre_vol=("cob_sobre_vol", "median"),
                                     largo_X=("largo_X", "median"))
print(g.round(3).to_string())
print("\nPor hora (todas las ruedas): mediana de |GEX neto|, parte 0DTE (|0DTE| / (|0DTE| + |resto|)) y % de minutos negativos")
T = tab[tab.valido].copy()
T["hora"] = T.minuto // 60
T["parte0"] = T.gex_0dte.abs() / (T.gex_0dte.abs() + (T.gex_neto - T.gex_0dte).abs())
print(T.groupby("hora").agg(gex_abs=("gex_neto", lambda s: s.abs().median()), parte_0dte=("parte0", "median"),
                            neg=("gex_neto", lambda s: 100 * (s < 0).mean()), n=("dia", "size")).round(3).to_string())
print("\nPor periodo: % de minutos con GEX neto < 0, mediana de gex_norm, de dist_flip_X, de cruces del barrido, de edad del dato")
print(T.groupby("muestra").agg(neg=("gex_neto", lambda s: 100 * (s < 0).mean()), neg_estr=("gex_0dte", lambda s: np.nan),
                               gex_norm=("gex_norm", "median"), dist_flip_X=("dist_flip_X", "median"), cruces=("n_cruces", "median"),
                               edad=("edad_s", "median"), ruedas=("dia", "nunique")).round(3).to_string())
T["neg_estr"] = (T.gex_neto - T.gex_0dte) < 0
print(T.groupby("muestra")["neg_estr"].mean().mul(100).round(1).rename("% min estructural negativo").to_string())
print("\nGeometria de la ola por rueda segun el regimen a las 9:59: cantidad de piernas de X en la rueda, largo mediano en X y")
print("% de piernas > 2X. Para un paseo al azar el largo en X NO depende de la volatilidad (el zigzag solo mira el camino en precio):")
print("si la gamma negativa solo agrega volatilidad, cambia la CANTIDAD de piernas y no su largo; si agrega persistencia, cambia el largo.")
Lall = pd.DataFrame(filas)
r959 = tab[tab.minuto == 29].set_index("dia")
por = Lall.groupby("dia").agg(muestra=("muestra", "first"), piernas=("largo_X", "size"), largo_med=("largo_X", "median"),
                              mas_2X=("largo_X", lambda s: (s > 2).mean()))
por["neg_0959"] = [(r959.loc[d, "gex_neto"] < 0) if (d in r959.index and bool(r959.loc[d, "valido"])) else np.nan for d in por.index]
por = por[por.neg_0959.notna()]
print(por.groupby(["muestra", "neg_0959"]).agg(ruedas=("piernas", "size"), piernas_por_rueda=("piernas", "mean"),
                                              largo_med_X=("largo_med", "median"), mas_2X=("mas_2X", "mean")).round(3).to_string())
print(por.groupby("neg_0959").agg(ruedas=("piernas", "size"), piernas_por_rueda=("piernas", "mean"), largo_med_X=("largo_med", "median"),
                                  mas_2X=("mas_2X", "mean")).round(3).to_string())
b, t = C.t_cluster(por.piernas.to_numpy(float), por.neg_0959.astype(float).to_numpy(), np.arange(len(por)))
b2, t2 = C.t_cluster(por.largo_med.to_numpy(float), por.neg_0959.astype(float).to_numpy(), np.arange(len(por)))
print("dif NEG-POS: piernas por rueda %+.1f (t %+.2f) | largo mediano %+.3f X (t %+.2f)" % (b, t, b2, t2))
print("\nPor rueda: % de minutos negativos (total y sin 0DTE) y GEX neto mediano")
print(T.groupby("dia").agg(muestra=("muestra", "first"), neg=("gex_neto", lambda s: round(100 * (s < 0).mean())),
                           neg_estr=("neg_estr", lambda s: round(100 * s.mean())), gex_med=("gex_neto", "median"),
                           cruces=("n_cruces", "median")).to_string())
