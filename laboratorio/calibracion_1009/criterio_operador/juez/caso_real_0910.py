# -*- coding: utf-8 -*-
"""caso_real_0910.py — el juez del operador sobre ESTA NOCHE (10-09 00:35Z -> ultima vela) con las dominantes que DIBUJO la 2.0
(dom0, dom1 de QQQ y ndx_dom0, ndx_dom1 de NDX, sacadas de su rebobinado/centinela: niv20_m1.pkl) y las velas M1 de MNQZ6
(velas_m1.csv, validadas 98,1 % contra la cinta por el investigador previo; aca NO se re-validan). SOLO LECTURA.
Esperado a mano (mirando las velas, con la raya CRUDA 31083,5005: la banda de llegada empieza en 31081,5005):
  techo 31083,50: llegadas 02:00 (giro ya a las 02:01: minimo 31065,25 = -18,25), 02:02 (falso rompimiento: cierre 31085,25 y mecha
  31086,75 = +3,25; giro 02:08), 02:10, 02:27 (falso rompimiento: mecha 31089,25 = +5,75, cierres debajo; ESTRICTO rota), 02:45 (la
  mecha de 02:44 llega a 31081,50 y NO toca la banda por 0,0005), 02:56, 03:21 (sin datos para definirse: censurada).
  31042,06 (QQQ, fusionada con NDX 31041,83): 01:42 atravesada desde abajo -> rota; 01:45 vuelve como piso -> sostenido;
  03:10 piso con pinchazo a 31040 (-2,06) -> sostenido con falso rompimiento.
Correr: python -I caso_real_0910.py"""
import os
import sys

import pandas as pd

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import juez_operador as J  # noqa: E402

CAL = os.path.join(os.environ["LOCALAPPDATA"], "Temp", "claude", "C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada",
                   "f5879819-e2a4-405b-87b0-959ace090d7b", "scratchpad", "calibrar")
CLAVES = ("dom0", "dom1", "ndx_dom0", "ndx_dom1")


def main():
    V = pd.read_csv(os.path.join(CAL, "velas_m1.csv"), parse_dates=["t"])
    niv = pd.read_pickle(os.path.join(CAL, "niv20_m1.pkl"))
    V = V[V["t"] >= "2026-10-08 22:00"].reset_index(drop=True)          # la sesion de esta noche
    R = J.rayas_desde_niv20({t: d for t, d in niv.items() if t >= pd.Timestamp("2026-10-08 21:00")}, CLAVES)
    ult = V["t"].iloc[-1]
    print("velas: %s -> %s (%d). Ultima vela %s UTC: el dato es de la sesion de esta noche." % (V["t"].iloc[0], ult, len(V), ult))
    op = {"ventanas": [("2026-10-09 00:35", "2026-10-09 09:00")]}
    res = {m: J.juzgar(V, R, dict(op, modo=m)) for m in ("tolerante", "estricto", "muy_tolerante")}
    print("\nLLEGADAS (TOLERANTE, el principal) y como las ve cada modo:")
    otros = {m: {(e["i"], round(e["raya"], 2)): e for e in res[m]["eventos"]} for m in ("estricto", "muy_tolerante")}
    for e in res["tolerante"]["eventos"]:
        k = (e["i"], round(e["raya"], 2))
        rec = ("rec %5.2f (tramo %5.2f, %s) opuesta %s llega %s" % (e["recorrido"], e["recorrido_tramo"], e["fin_recorrido"],
               ("%.2f" % e["opuesta"]) if e["opuesta"] else "-", e["llega_opuesta"])) if e["resultado"] == "sostenido" else ""
        print("  %s %-5s %.2f %-28s -> %-10s %-38s prec %+6.2f %s| ESTRICTO %-10s MUY TOL %-10s %s" % (
            e["t_llegada"].strftime("%H:%M"), e["tipo"], e["raya"], "+".join(e["nombres"]), e["resultado"], e["motivo"], e["precision"],
            "FALSO ROMP. " if e["falso_rompimiento"] else "", otros["estricto"].get(k, {}).get("resultado", "(no llega)"),
            otros["muy_tolerante"].get(k, {}).get("resultado", "(no llega)"), rec))
    for m in ("tolerante", "estricto", "muy_tolerante"):
        x = res[m]["metricas"]
        print("\n%s: llegadas %d (base %d, censuradas %d) sostenidos %d (%.0f %%; con falso rompimiento %d) rotas %d indef %d | "
              "exactos %d | precision mediana |%.2f| (con signo %+.2f) | recorrido medio %.1f mediano %.1f tramo medio %.1f | "
              "llega a la opuesta %d/%d" % (m.upper(), x["llegadas"], x["base"], x["censuradas"], x["sostenidos"], x["pct_sostenidos"],
                                            x["falsos_rompimientos"], x["rotas"], x["indefinidas"], x["exactos"], x["precision_mediana_abs"],
                                            x["precision_mediana"], x["recorrido_medio"], x["recorrido_mediano"], x["recorrido_tramo_medio"],
                                            x["llega_opuesta"], x["con_opuesta"]))
    print("\nCOBERTURA DE GIROS (zigzag 20 pts) esta noche:")
    for tol in (2.0, 6.0):
        cg = J.cobertura_giros(V, R, dict(op, tol_cobertura=tol))
        print("  tol +-%.0f (%s): %d de %d giros cubiertos" % (tol, "pre-registrada" if tol == 2 else "sensibilidad, = falso rompimiento",
                                                              cg["cubiertos"], cg["n_giros"]))
        if tol == 2.0:
            for g in cg["giros"]:
                print("     %s %s %.2f (izq %.1f der %.1f) aproximacion %s -> cubierto %s (raya mas cercana %s)" % (
                    g["t"].strftime("%H:%M"), g["tipo"], g["precio"], g["izq"], g["der"], g["t_aprox"].strftime("%H:%M"), g["cubierto"],
                    ("%.2f" % g["raya_mas_cercana"]) if g["raya_mas_cercana"] else "-"))
    # chequeos de lo esperado a mano
    t = {(e["t_llegada"].strftime("%H:%M"), round(e["raya"], 2)): e for e in res["tolerante"]["eventos"]}
    s = {(e["t_llegada"].strftime("%H:%M"), round(e["raya"], 2)): e for e in res["estricto"]["eventos"]}
    techo = sorted(hh for hh, L in t if L == 31083.5)
    assert techo == ["02:00", "02:02", "02:10", "02:27", "02:45", "02:56", "03:21"], techo
    for hh in techo[:-1]:
        assert t[(hh, 31083.5)]["resultado"] == "sostenido" and t[(hh, 31083.5)]["tipo"] == "techo", hh
    assert t[("03:21", 31083.5)]["censurada"]
    assert t[("02:00", 31083.5)]["min_a_decision"] == 1 and not t[("02:00", 31083.5)]["falso_rompimiento"]
    assert t[("02:02", 31083.5)]["falso_rompimiento"] and abs(t[("02:02", 31083.5)]["precision"] - 3.25) < 0.01
    assert t[("02:27", 31083.5)]["falso_rompimiento"] and abs(t[("02:27", 31083.5)]["precision"] - 5.75) < 0.01
    assert s[("02:27", 31083.5)]["resultado"] == "rota"
    assert t[("01:42", 31042.06)]["resultado"] == "rota" and t[("01:42", 31042.06)]["tipo"] == "techo"
    assert t[("01:45", 31042.06)]["resultado"] == "sostenido" and t[("01:45", 31042.06)]["tipo"] == "piso"
    assert t[("03:10", 31042.06)]["resultado"] == "sostenido" and t[("03:10", 31042.06)]["tipo"] == "piso"
    assert t[("03:10", 31042.06)]["falso_rompimiento"]
    print("\nCASO REAL OK: coincide con lo esperado a mano")


if __name__ == "__main__":
    main()
