# -*- coding: utf-8 -*-
"""v41_05_hoy.py — ESTA NOCHE (sesion 10-09: 10-08 22:00Z -> ultima vela cerrada) con las series REALES que anoto la 4.1 minuto a minuto
(%APPDATA%/ATAS/PythiaGex4/familia/niv-2026-10-09-MNQZ6.jsonl: clave k = minuto, niveles calculados con los cierres anteriores a k ->
vigentes en la vela k -> desfase_min 0). Todas las series del archivo (cada una con sus <= 2 rayas), el juez SIN CAMBIOS, dos ventanas
(congelada 00:35Z -> 08:30Z y completa 22:00Z -> 13:30Z, cortadas por la ultima vela), tres modos, placebo (500 al azar 'pool' + corridas).
OJO (advertencia del juez): con una sola noche y pocas pistas el azar 'pool' degenera; mirar tambien las corridas. SOLO LECTURA.
Escribe datos/hoy_v41.json y datos/hoy_v41.txt. Uso: python -I v41_05_hoy.py [n_azar]"""
import json
import os
import sys
import time

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(AQUI), "juez"))
sys.path.insert(0, AQUI)
import juez_operador as J  # noqa: E402
from v41_03_paridad import leer_niv  # noqa: E402
from v41_04_juez import limpio  # noqa: E402

N = pd.Timestamp("2026-10-09")
VENT = {"congelada": (N + pd.Timedelta(minutes=35), N + pd.Timedelta(hours=8, minutes=30)),
        "completa": (N - pd.Timedelta(hours=2), N + pd.Timedelta(hours=13, minutes=30))}


def main():
    n_azar = int(sys.argv[1]) if len(sys.argv) > 1 else 500
    t0 = time.time()
    V = pd.read_csv(os.path.join(AQUI, "datos", "velas_v41.csv"), parse_dates=["t"])
    V = V[V["t"] >= N - pd.Timedelta(hours=2)].reset_index(drop=True)
    cab, recs = leer_niv()
    nombres = sorted({s for r in recs for s in r["s"]})
    rayas = {s: {} for s in nombres}
    for r in recs:
        t = pd.Timestamp(r["k"] * 60, unit="s")
        for s in nombres:
            rayas[s][t] = {e[1]: (float(e[0]), "%s%s" % (s.split("_")[1], e[3] if e[3] is not None else round(e[0], 1)))
                           for e in r["s"].get(s, [])}
    lin = ["ESTA NOCHE: velas %s -> %s (%d); niveles de la 4.1 %s -> %s (%d minutos)" % (
        V["t"].min(), V["t"].max(), len(V), pd.Timestamp(recs[0]["k"] * 60, unit="s"), pd.Timestamp(recs[-1]["k"] * 60, unit="s"), len(recs))]
    res = {"ultima_vela": str(V["t"].max()), "n_azar": n_azar, "series": {}}
    for tipo, (a, b) in VENT.items():
        lin.append("\n==== VENTANA %s %s -> %s (datos hasta %s)" % (tipo.upper(), a, b, V["t"].max()))
        lin.append("%-15s %4s | %-60s | %-60s | %s" % ("serie", "pist", "TOLERANTE: lleg base sost(%) falso rotas | exact | prec | rec med/mediano | opuesta",
                                                       "ESTRICTO / MUY TOLERANTE: sost(%)", "placebo: pctil sost / exact / prec / rec; corridas sost% exact%; cobertura"))
        for s in nombres:
            op = {"ventanas": [(a, b)], "desfase_min": 0}
            prep = J.preparar(V, rayas[s], op)
            noches = J._noches_ventana(prep[0])
            if not len(prep[1]["pistas"]):
                lin.append("%-15s sin rayas en la ventana" % s); continue
            pl = J.placebo(V, rayas[s], op, n_azar=n_azar, n_boot=0, _prep=prep)
            sens = {m: J.resumir(J._juzgar_core(prep[0], prep[1], J._op(dict(op, modo=m))), noches) for m in ("estricto", "muy_tolerante")}
            x = pl["real"]; az = pl["azar"]; cj = pl["corridos_juntos"]
            lin.append("%-15s %4d | lleg %2d base %2d sost %2d (%5.1f%%) falso %2d rotas %2d | ex %2d | %4.2f | %5.1f/%5.1f | %s/%d | "
                       "estr %5.1f%% muy %5.1f%% | pc %3.0f/%3.0f/%3.0f/%3.0f; corr %4.1f%% %4.1f%%; cob %d/%d" % (
                           s, pl["n_pistas"], x["llegadas"], x["base"], x["sostenidos"], x["pct_sostenidos"], x["falsos_rompimientos"], x["rotas"],
                           x["exactos"], x["precision_mediana_abs"], x["recorrido_medio"], x["recorrido_mediano"], x["llega_opuesta"],
                           x["con_opuesta"], sens["estricto"]["pct_sostenidos"], sens["muy_tolerante"]["pct_sostenidos"],
                           az["pct_sostenidos"]["percentil"], az["pct_exactos"]["percentil"], az["precision_mediana_abs"]["percentil"],
                           az["recorrido_medio"]["percentil"], cj["pct_sostenidos"], cj["pct_exactos"],
                           round(x["cobertura_pct"] * x["cobertura_n"] / 100) if x["cobertura_pct"] == x["cobertura_pct"] else 0, x["cobertura_n"]))
            res["series"]["%s|%s" % (s, tipo)] = limpio({
                "n_pistas": pl["n_pistas"], "real": x, "azar": az, "corridos_juntos": cj, "corridos": pl["corridos"], "sensibilidad": sens,
                "eventos": [{k: e.get(k) for k in ("t_llegada", "raya", "etiquetas", "tipo", "resultado", "motivo", "precision", "falso_rompimiento",
                                                   "recorrido", "recorrido_tramo", "opuesta", "llega_opuesta", "censurada")} for e in pl["eventos_real"]]})
        print("\n".join(lin[-(len(nombres) + 2):]), flush=True)
    # detalle de llegadas de las 4 de QQQ en la ventana congelada
    lin.append("\nDETALLE (ventana congelada, TOLERANTE) de las series de QQQ y de los muros de NDX:")
    for s in ("MUROS_QQQ_vol", "MAJORS_QQQ_vol", "MUROS_QQQ_oi", "MAJORS_QQQ_oi", "MUROS_NDX_vol", "MUROS_NDX_oi", "FAM_MUROS_oi"):
        k = "%s|congelada" % s
        if k not in res["series"]:
            continue
        lin.append("  %s:" % s)
        for e in res["series"][k]["eventos"]:
            lin.append("     %s %-5s %.2f %-10s %-9s prec %+6.2f %s rec %s opuesta %s llega %s %s" % (
                e["t_llegada"][11:16], e["tipo"], e["raya"], ",".join(e["etiquetas"] or []), e["resultado"], e["precision"] or 0,
                "FALSO-ROMP" if e["falso_rompimiento"] else "", e["recorrido"], e["opuesta"], e["llega_opuesta"],
                "(censurada)" if e["censurada"] else ""))
    txt = "\n".join(lin)
    print(txt[txt.index("DETALLE"):])
    json.dump(res, open(os.path.join(AQUI, "datos", "hoy_v41.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    open(os.path.join(AQUI, "datos", "hoy_v41.txt"), "w", encoding="utf-8").write(txt + "\n")
    print("listo %.0f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
