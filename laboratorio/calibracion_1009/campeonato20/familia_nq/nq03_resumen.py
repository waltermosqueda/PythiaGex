# -*- coding: utf-8 -*-
"""nq03_resumen.py — tabla de texto de la familia NQ (lee datos/juez_nq.json; no recalcula nada). Escribe datos/juez_nq.txt."""
import json
import math
import os

AQUI = os.path.dirname(os.path.abspath(__file__))
D = os.path.join(AQUI, "datos")


def f(x, d=1):
    if x is None or (isinstance(x, float) and not math.isfinite(x)):
        return "-"
    return ("%%.%df" % d) % x


def main():
    J = json.load(open(os.path.join(D, "juez_nq.json"), encoding="utf-8"))
    P = J["particion"]
    lin = ["FAMILIA NQ (libro de opciones de NQ por Rithmic, reconstruccion de la 4.1 minuto a minuto; DOMS = seleccion de la 2.0)",
           "noche: %d noches (mirar %s..%s = %d | PRUEBA %s..%s = %d); rueda: %d (mirar %d | PRUEBA %d)" % (
               len(P["noche"]["elegibles"]), P["noche"]["mirar"][0], P["noche"]["mirar"][-1], len(P["noche"]["mirar"]),
               P["noche"]["prueba"][0], P["noche"]["prueba"][-1], len(P["noche"]["prueba"]), len(P["rueda"]["elegibles"]),
               len(P["rueda"]["mirar"]), len(P["rueda"]["prueba"])),
           "CANDIDATAS (regla pre-registrada, solo MIRAR): %s" % (J["candidatas"] or "ninguna"), ""]
    cab = ("%-15s %-5s %-6s %5s %5s | %4s %4s %4s %3s | %5s [%5s-%5s] | %5s %5s %5s | %5s %6s %6s | %4s %4s | %4s %4s | %5s %4s | %5s %5s | %5s %5s"
           % ("serie", "vent", "parte", "noch", "horas", "lleg", "acie", "fals", "ind", "AC20%", "ic90", "", "azar", "corr", "perc",
              "p", "p_norm", "pHolm", "ac/n", "fa/n", "acEq", "faEq", "r/h", "sim", "recor", "prec", "expec", "expAz"))
    hol = J["holm"]
    for parte in ("mirar", "prueba"):
        for vt in ("noche", "rueda"):
            lin.append("=== %s / %s (n_azar 500) ===" % (vt.upper(), parte.upper()))
            lin.append(cab)
            hh = hol.get("todas_%s_perm" % parte, {})
            for r in J["resultados"]:
                if r["parte"] != parte or r["ventana"] != vt or r["n_azar"] != 500:
                    continue
                if r.get("vacio"):
                    lin.append("%-15s %-5s %-6s sin cobertura" % (r["serie"], vt, parte)); continue
                m = r["metricas"]; a = r["azar"].get("pct_acierto20", {}); ic = r["ic90"].get("pct_acierto20", [None, None])
                ea = r["azar"].get("expectativa_pts", {})
                lin.append("%-15s %-5s %-6s %5d %5.0f | %4d %4d %4d %3d | %5s [%5s-%5s] | %5s %5s %5s | %5s %6s %6s | %4s %4s | %4s %4s | %5s %4s | %5s %5s | %5s %5s" % (
                    r["serie"], vt, parte, len(r["noches_ventana"]), r["horas_cubiertas"], m["llegadas"], m["aciertos"], m["falsas"],
                    m["indefinidas"], f(m["pct_acierto20"]), f(ic[0]), f(ic[1]), f(a.get("p50")), f(r["corridas_juntas"]["pct_acierto20"]),
                    f(a.get("percentil")), f(a.get("p_valor"), 3), f(a.get("p_normal"), 4),
                    f(hh.get("%s|%s" % (r["serie"], vt), {}).get("p_holm"), 3), f(m["aciertos_por_noche"], 2), f(m["falsas_por_noche"], 2),
                    f(r["aciertos_por_noche_equiv"], 2), f(r["falsas_por_noche_equiv"], 2), f(m["rayas_por_hora"], 2),
                    f(m["rayas_simultaneas_mediana"], 0), f(m["recorrido_mediano_acierto"]), f(m["precision_mediana_abs"], 2),
                    f(m["expectativa_pts"], 2), f(ea.get("p50"), 2)))
            lin.append("")
    conf = [r for r in J["resultados"] if r["n_azar"] != 500]
    if conf:
        lin.append("=== CONFIRMACION EN PRUEBA (candidatas, n_azar %d) ===" % conf[0]["n_azar"])
        hc = hol.get("confirmacion", {})
        for r in conf:
            m = r["metricas"]; a = r["azar"]["pct_acierto20"]
            k = "%s|%s" % (r["serie"], r["ventana"])
            lin.append("%-15s %-5s acierto20 %s %% (azar p50 %s, corridas %s) percentil %s p %s p_normal %s p_Holm %s -> %s" % (
                r["serie"], r["ventana"], f(m["pct_acierto20"]), f(a["p50"]), f(r["corridas_juntas"]["pct_acierto20"]), f(a["percentil"], 2),
                f(a["p_valor"], 4), f(a["p_normal"], 5), f(hc.get(k, {}).get("p_holm"), 4),
                "CONFIRMA" if (hc.get(k, {}).get("rechaza_05") and m["pct_acierto20"] > (r["corridas_juntas"]["pct_acierto20"] or 1e9)) else "no confirma"))
    lin.append("")
    lin.append("Leyenda: noch = noches con velas en la ventana; horas = horas con libro (cubiertas); AC20% = aciertos / base (indefinidas = no "
               "acierto); azar = p50 del pct en 500 juegos; corr = corridas +-11/19/31 juntas; perc = percentil del real en el azar; p = p por "
               "permutacion; pHolm = Holm sobre las 24 series x ventana de esa parte (solo descriptivo); ac/n fa/n = por noche de la ventana; "
               "acEq faEq = por noche equivalente (15,5 h de noche o 6,5 h de rueda cubiertas); r/h = rayas distintas por hora; sim = rayas "
               "simultaneas (mediana); recor = recorrido mediano tras acierto (pts desde la raya); prec = |punta - raya| mediana en aciertos; "
               "expec = pts por llegada decidida (+20 / -rotura, DESCRIPTIVO); expAz = p50 del azar.")
    txt = "\n".join(lin)
    open(os.path.join(D, "juez_nq.txt"), "w", encoding="utf-8").write(txt + "\n")
    print(txt)


if __name__ == "__main__":
    main()
