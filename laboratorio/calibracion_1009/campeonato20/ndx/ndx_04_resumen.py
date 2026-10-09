# -*- coding: utf-8 -*-
"""ndx_04_resumen.py — tabla de la familia NDX (campeonato20) a partir de datos/juez/*.json (ndx_03_juez.py). SOLO LECTURA.
Por conjunto, ventana y particion: llegadas, % ACIERTO20 (IC90 por noche), tasa base del azar y de las corridas, percentil y p del
placebo (permutacion y normal), falsas y aciertos por noche, indefinidas, rayas por hora, recorrido mediano tras acierto, precision
mediana, expectativa DESCRIPTIVA. Holm DENTRO de la familia (14 conjuntos) por ventana y particion, con el p por permutacion
(placebo['azar']['pct_acierto20']['p_valor']) y aparte con p_normal (aproximacion). El Holm del campeonato entero lo hace quien junte
todas las familias: aca van los p crudos. Escribe datos/resumen_ndx.txt y datos/resumen_ndx.json."""
import glob
import json
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(AQUI, "..", "juez20")))
import juez20 as J20  # noqa: E402

ORDEN = ["MUROS_NDX_vol", "MUROS_NDX_oi", "MAJORS_NDX_vol", "MAJORS_NDX_oi", "ZTP_NDX_vol", "ZEST_NDX_vol", "DOMS_NDX_vol", "R20_NDX_vol",
         "MUROS_NDX_vol:P", "MUROS_NDX_vol:P_pisos", "MUROS_NDX_vol:C_techos", "MUROS_NDX_oi:P", "MUROS_NDX_oi:P_pisos",
         "MUROS_NDX_oi:C_techos"]


def f(x, d=1):
    return "-" if x is None else ("%.*f" % (d, x))


def main():
    R = {}
    for p in glob.glob(os.path.join(AQUI, "datos", "juez", "*.json")):
        r = json.load(open(p, encoding="utf-8"))
        R[(r["conjunto"], r["ventana"], r["particion"])] = r
    filas = []
    holm = {}
    for ven in ("noche", "rueda"):
        for part in ("mirar", "prueba", "mirar_cboe"):
            ps = {c: R[(c, ven, part)]["placebo"]["azar"]["pct_acierto20"]["p_valor"] for c in ORDEN if (c, ven, part) in R}
            pn = {c: R[(c, ven, part)]["placebo"]["azar"]["pct_acierto20"]["p_normal"] for c in ORDEN if (c, ven, part) in R}
            if ps:
                holm[(ven, part)] = (J20.holm({k: v for k, v in ps.items() if v is not None}),
                                     J20.holm({k: v for k, v in pn.items() if v is not None}))
    L = []
    L.append("CAMPEONATO20 — FAMILIA NDX (cadena _NDX de CBOE; la 4.1 reconstruida minuto a minuto). Vara: ACIERTO20 TOLERANTE, juez20 sin cambios.")
    L.append("pct = ACIERTO20 sobre base (indefinidas cuentan como no acierto). azar = mediana de 500/2000 juegos con las mismas pistas; corr = corridas +-11/19/31.")
    L.append("pctl = percentil del real en el azar; p = permutacion (una cola); pN = normal (aprox.); pH = Holm dentro de la familia (14 conjuntos).")
    L.append("Expectativa = DESCRIPTIVO (+20 / -cierre de rotura), sin costos ni decision de entrar: NO es una promesa.")
    for ven in ("noche", "rueda"):
        for part in ("mirar", "prueba", "mirar_cboe"):
            claves = [c for c in ORDEN if (c, ven, part) in R]
            if not claves:
                continue
            r0 = R[(claves[0], ven, part)]
            L.append("")
            L.append("=== %s / %s: %d sesiones (%s .. %s), n_azar %d" % (ven.upper(), part.upper(), r0["n_sesiones"], r0["sesiones"][0],
                                                                       r0["sesiones"][-1], r0["n_azar"]))
            L.append("%-24s %5s %5s %5s %13s %5s %5s %5s %7s %7s %6s %5s %5s %5s %5s %5s %5s %5s %6s" % (
                "conjunto", "lleg", "acier", "falsa", "pct (IC90)", "azar", "corr", "pctl", "p", "pN", "pH", "ac/n", "fa/n", "ind/n",
                "ray/h", "recor", "prec", "rotur", "expec"))
            hp, hn = holm[(ven, part)]
            for c in claves:
                r = R[(c, ven, part)]
                m = r["metricas"]; a = r["placebo"]["azar"]["pct_acierto20"]; ic = r["bootstrap_ic90"].get("pct_acierto20", [None, None])
                L.append("%-24s %5d %5d %5d %5s(%s-%s) %5s %5s %5s %7s %7s %6s %5s %5s %5s %5s %5s %5s %5s %6s" % (
                    c, m["llegadas"], m["aciertos"], m["falsas"], f(m["pct_acierto20"]), f(ic[0], 0), f(ic[1], 0),
                    f(r["placebo"]["tasa_base_azar"]), f(r["placebo"]["tasa_base_corridas"]), f(a["percentil"]),
                    f(a["p_valor"], 4), f(a["p_normal"], 4), f(hp.get(c, {}).get("p_holm"), 3), f(m["aciertos_por_noche"], 2),
                    f(m["falsas_por_noche"], 2), f(m["indefinidas_por_noche"], 2), f(m["rayas_por_hora"]), f(m["recorrido_mediano_acierto"]),
                    f(m["precision_mediana_abs"], 2), f(m["dist_rotura_mediana"]), f(m["expectativa_pts"], 2)))
                filas.append({"conjunto": c, "ventana": ven, "particion": part, "n_sesiones": r["n_sesiones"], "llegadas": m["llegadas"],
                              "base": m["base"], "aciertos": m["aciertos"], "falsas": m["falsas"], "indefinidas": m["indefinidas"],
                              "censuradas": m["censuradas"], "pct_acierto20": m["pct_acierto20"], "ic90": ic,
                              "pct_acierto20_decididas": m["pct_acierto20_decididas"],
                              "tasa_base_azar": r["placebo"]["tasa_base_azar"], "tasa_base_corridas": r["placebo"]["tasa_base_corridas"],
                              "percentil": a["percentil"], "p_valor": a["p_valor"], "p_normal": a["p_normal"],
                              "p_holm_familia": hp.get(c, {}).get("p_holm"), "p_holm_familia_normal": hn.get(c, {}).get("p_holm"),
                              "aciertos_por_noche": m["aciertos_por_noche"], "falsas_por_noche": m["falsas_por_noche"],
                              "indefinidas_por_noche": m["indefinidas_por_noche"], "rayas_por_hora": m["rayas_por_hora"],
                              "rayas_por_hora_p90": m["rayas_por_hora_p90"], "recorrido_mediano_acierto": m["recorrido_mediano_acierto"],
                              "precision_mediana_abs": m["precision_mediana_abs"], "dist_rotura_mediana": m["dist_rotura_mediana"],
                              "expectativa_pts": m["expectativa_pts"], "expectativa_pts_fija": m["expectativa_pts_fija"],
                              "expectativa_azar_p50": r["placebo"]["azar"]["expectativa_pts"]["p50"],
                              "ic90_aciertos_por_noche": r["bootstrap_ic90"].get("aciertos_por_noche"),
                              "ic90_falsas_por_noche": r["bootstrap_ic90"].get("falsas_por_noche"),
                              "por_tipo": r["por_tipo"], "n_azar": r["n_azar"], "n_pistas": r["n_pistas"]})
    txt = "\n".join(L)
    with open(os.path.join(AQUI, "datos", "resumen_ndx.txt"), "w", encoding="utf-8") as fh:
        fh.write(txt + "\n")
    with open(os.path.join(AQUI, "datos", "resumen_ndx.json"), "w", encoding="utf-8") as fh:
        json.dump(filas, fh, ensure_ascii=False, indent=1)
    print(txt)


if __name__ == "__main__":
    main()
