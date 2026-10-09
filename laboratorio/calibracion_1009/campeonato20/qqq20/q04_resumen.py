# -*- coding: utf-8 -*-
"""q04_resumen.py — campeonato20, familia QQQ / 2.0: junta los resultados de q03 (datos/res/*.json), aplica Holm sobre las series de la
familia (sin el control GRILLA_QQQ) por particion, y escribe datos/resumen_qqq20.{json,csv,txt}. SOLO LECTURA de datos/res.
La p que va a Holm = placebo['azar']['pct_acierto20']['p_valor'] (permutacion; MIRAR 500 juegos -> piso 0,002; PRUEBA 2000 -> 0,0005).
Tambien se informa p_normal (aproximacion normal, dicha como tal). Uso: python -I q04_resumen.py"""
import ctypes
try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass
import glob
import json
import os
import sys

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(AQUI, "..", "juez20")))
import juez20 as J20  # noqa: E402

RES = os.path.join(AQUI, "datos", "res")
CONTROLES = ("GRILLA_QQQ",)
ORDEN = ("MUROS_QQQ_vol", "MUROS_QQQ_oi", "MAJORS_QQQ_vol", "MAJORS_QQQ_oi", "ZEST_QQQ_vol", "DOMS_QQQ_vol", "DOMS_QQQ_esc", "R20_QQQ",
         "T_DOMS_vol", "T_MUROS_vol", "T_MUROS_oi", "T_ZERO_oi", "T_DOMS_raz", "GRILLA_QQQ")


def f(x, d=1):
    return "-" if x is None or x != x else ("%." + str(d) + "f") % x


def main():
    filas = []
    for p in sorted(glob.glob(os.path.join(RES, "*.json"))):
        d = json.load(open(p, encoding="utf-8"))
        m = d["metricas"]; ic = d["bootstrap_ic90"]; pl = d["placebo"]; az = pl["azar"]
        ev = os.path.join(RES, "%s__%s__%s__eventos.csv" % (d["serie"], d["ventana"], d["particion"]))
        pn = {}
        if os.path.exists(ev):
            E = pd.read_csv(ev)
            for n in d["noches_ventana"]:
                e = E[E["noche"] == n]
                pn[n] = (int(e["acierto20"].sum()), int(e["falsa"].sum()))
        else:
            pn = {n: (0, 0) for n in d["noches_ventana"]}
        filas.append(dict(
            serie=d["serie"], ventana=d["ventana"], particion=d["particion"], n_azar=d["n_azar"], noches=len(d["noches_ventana"]),
            noches_lista=",".join(x[5:] for x in d["noches_ventana"]), llegadas=m["llegadas"], base=m["base"], aciertos=m["aciertos"],
            falsas=m["falsas"], indefinidas=m["indefinidas"], censuradas=m["censuradas"],
            pct=m["pct_acierto20"], ic_lo=ic["pct_acierto20"][0], ic_hi=ic["pct_acierto20"][1],
            azar_p50=pl["tasa_base_azar"], azar_p5=az["pct_acierto20"]["p5"], azar_p95=az["pct_acierto20"]["p95"],
            corridas=pl["tasa_base_corridas"], percentil=az["pct_acierto20"]["percentil"], p=az["pct_acierto20"]["p_valor"],
            p_normal=az["pct_acierto20"]["p_normal"], z=az["pct_acierto20"]["z"],
            aciertos_noche=m["aciertos_por_noche"], ac_lo=ic["aciertos_por_noche"][0], ac_hi=ic["aciertos_por_noche"][1],
            falsas_noche=m["falsas_por_noche"], fa_lo=ic["falsas_por_noche"][0], fa_hi=ic["falsas_por_noche"][1],
            indef_noche=m["indefinidas_por_noche"], llegadas_noche=m["llegadas_por_noche"],
            rayas_h=m["rayas_por_hora"], rayas_h_p90=m["rayas_por_hora_p90"], simultaneas=m["rayas_simultaneas_mediana"],
            recorrido=m["recorrido_mediano_acierto"], rec_lo=ic["recorrido_mediano_acierto"][0], rec_hi=ic["recorrido_mediano_acierto"][1],
            min_a_acierto=m["min_a_acierto_mediano"], precision_abs=m["precision_mediana_abs"], precision=m["precision_mediana"],
            falsos_romp=m["falsos_rompimientos"], dist_rotura=m["dist_rotura_mediana"],
            expect=m["expectativa_pts"], exp_lo=ic["expectativa_pts"][0], exp_hi=ic["expectativa_pts"][1],
            expect_azar_p50=az["expectativa_pts"]["p50"], expect_pct=az["expectativa_pts"]["percentil"],
            falsas_noche_azar_p50=az["falsas_por_noche"]["p50"], falsas_noche_pct=az["falsas_por_noche"]["percentil"],
            aciertos_noche_azar_p50=az["aciertos_por_noche"]["p50"],
            etiquetas=d["etiquetas_distintas"], n_niveles=m.get("n_niveles"), cobertura=d["cobertura_min"] / max(1, d["minutos_ventana"]),
            pistas=d["n_pistas"], desfase=d["desfase_min"], por_noche=json.dumps(pn)))
    T = pd.DataFrame(filas)
    T["orden"] = T["serie"].map({s: i for i, s in enumerate(ORDEN)})
    T = T.sort_values(["particion", "ventana", "orden"], ascending=[True, True, True]).reset_index(drop=True)
    # Holm por particion sobre las series de la familia (sin controles)
    T["p_holm"] = np.nan; T["rechaza_holm"] = False
    for part in ("mirar", "prueba"):
        sel = T[(T["particion"] == part) & ~T["serie"].isin(CONTROLES)]
        h = J20.holm({i: r["p"] for i, r in sel.iterrows()})
        for i, v in h.items():
            T.at[i, "p_holm"] = v["p_holm"]; T.at[i, "rechaza_holm"] = v["rechaza_05"]
    T["dif_azar_pp"] = T["pct"] - T["azar_p50"]
    T["dif_corridas_pp"] = T["pct"] - T["corridas"]
    # contra la grilla de QQQ (mismo tramo y particion), solo para series de strikes de QQQ
    g = T[T["serie"] == "GRILLA_QQQ"].set_index(["ventana", "particion"])["pct"].to_dict()
    T["dif_grilla_pp"] = [r["pct"] - g.get((r["ventana"], r["particion"]), np.nan) if (r["serie"] not in CONTROLES and not r["serie"].startswith("T_"))
                          else np.nan for _, r in T.iterrows()]
    T.drop(columns=["orden"]).to_csv(os.path.join(AQUI, "datos", "resumen_qqq20.csv"), index=False)
    T.drop(columns=["orden"]).to_json(os.path.join(AQUI, "datos", "resumen_qqq20.json"), orient="records", force_ascii=False, indent=1)
    L = []
    for part in ("mirar", "prueba"):
        for ven in ("noche", "rueda"):
            S = T[(T["particion"] == part) & (T["ventana"] == ven)]
            if S.empty:
                continue
            L.append("== %s | %s | noches: %s ==" % (part.upper(), ven, S["noches_lista"].iloc[0] if S["serie"].iloc[0] != "T_DOMS_vol" else ""))
            L.append("%-14s %5s %5s %13s %6s %6s %6s %6s %7s %7s %6s %6s %6s %6s %5s %6s %6s %6s %6s %5s %5s" % (
                "serie", "lleg", "base", "ACIERTO20(IC90)", "azar", "corr", "grilla", "pctil", "p", "pHolm", "pN", "ac/n", "fa/n", "in/n",
                "ray/h", "recorr", "prec", "expec", "exAz", "niv", "cob"))
            for _, r in S.iterrows():
                L.append("%-14s %5d %5d %5s(%s-%s) %6s %6s %6s %6s %7s %7s %6s %6s %6s %6s %5s %6s %6s %6s %6s %5s %5s" % (
                    r["serie"], r["llegadas"], r["base"], f(r["pct"]), f(r["ic_lo"], 0), f(r["ic_hi"], 0), f(r["azar_p50"]), f(r["corridas"]),
                    f(r["dif_grilla_pp"]), f(r["percentil"], 0), f(r["p"], 4), f(r["p_holm"], 3), f(r["p_normal"], 4),
                    f(r["aciertos_noche"], 2), f(r["falsas_noche"], 2), f(r["indef_noche"], 2), f(r["rayas_h"], 1), f(r["recorrido"], 0),
                    f(r["precision_abs"], 1), f(r["expect"], 2), f(r["expect_azar_p50"], 2), f(r["n_niveles"], 0), f(100 * r["cobertura"], 0)))
            L.append("")
    L.append("Columnas: lleg = llegadas; base = no censuradas; ACIERTO20 = aciertos/base % (indefinidas = no acierto) con IC90 bootstrap por noche; "
             "azar = mediana del % en los juegos al azar (tasa base); corr = % de las corridas +-11/19/31 juntas; grilla = real - GRILLA_QQQ "
             "(pp); pctil = percentil del real en el azar; p = permutacion (una cola); pHolm = Holm sobre las series de la familia en esa "
             "particion; pN = aproximacion normal; ac/n, fa/n, in/n = aciertos, falsas, indefinidas por noche; ray/h = rayas distintas por "
             "hora; recorr = recorrido mediano tras acierto (pts); prec = |punta - raya| mediana en los aciertos; expec = expectativa "
             "DESCRIPTIVA pts por llegada decidida (+20 / -rotura; sin costos, no es promesa); exAz = la misma expectativa en la mediana del "
             "azar (la asimetria +20 / -5..7 la hace positiva tambien para rayas sin informacion); niv = niveles distintos (n_niveles del "
             "juez); cob = % de minutos de la ventana con alguna raya.")
    txt = "\n".join(L)
    open(os.path.join(AQUI, "datos", "resumen_qqq20.txt"), "w", encoding="utf-8").write(txt + "\n")
    print(txt)


if __name__ == "__main__":
    main()
