# -*- coding: utf-8 -*-
"""g3_tabla.py — extrae de resultados_<FASE>/<ID>.json las cifras que deciden (M1..M4, placebos, muestra, eco, toque) a
tabla_<FASE>.json y las imprime. Uso: python -I g3_tabla.py ENTRENAMIENTO|PRUEBA"""
import os
import sys
import json
import glob

AQUI = os.path.dirname(os.path.abspath(__file__))


def g(d, *ks, default=None):
    for k in ks:
        if not isinstance(d, dict) or k not in d:
            return default
        d = d[k]
    return d


def fila(r, w):
    op = r["operador"]; t = op["tolerante"][w]; s = t["pct_sostenidos"]; m = t["muestra"]
    rec = t["recorrido_por_llegada"]; cob = g(r, "cobertura", w, "cobertura20", default={}) or {}
    cob40 = g(r, "cobertura", w, "cobertura40", default={}) or {}
    tq = g(r, "toque", w, default={}) or {}
    eco = g(r, "eco", w, default={}) or {}
    return {
        "llegadas": m["llegadas"], "base": m["base"], "censuradas": m["censuradas"], "sesiones_con_llegadas": m["sesiones_con_llegadas"],
        "sesiones_con_3_o_mas": m["sesiones_con_3_o_mas"], "sesiones_ganadas": m["sesiones_ganadas"],
        "strikes_distintos": m["strikes_distintos"], "niveles_distintos_5pts": m["niveles_distintos_5pts"],
        "M1_real": s.get("real"), "M1_corridos": s.get("corridos"), "M1_azar_media": s.get("azar_media"), "M1_azar_p5": s.get("azar_p5"),
        "M1_azar_p95": s.get("azar_p95"), "p_azar": s.get("p_azar"), "ventaja_vs_corridos": s.get("ventaja_vs_corridos"),
        "ic95_vs_corridos": s.get("ic95_vs_corridos"), "ic90_inf_vs_corridos": s.get("ic90_inf_vs_corridos"),
        "ventaja_vs_azar": s.get("ventaja_vs_azar"), "ic95_vs_azar": s.get("ic95_vs_azar"),
        "corridos_por_desplazamiento": s.get("corridos_por_desplazamiento"),
        "estricto_ventaja_vs_corridos": g(op, "estricto", w, "pct_sostenidos", "ventaja_vs_corridos"),
        "muy_tolerante_ventaja_vs_corridos": g(op, "muy_tolerante", w, "pct_sostenidos", "ventaja_vs_corridos"),
        "pct_rotas": g(t, "pct_rotas", "real"), "pct_exactos": g(t, "pct_exactos", "real"),
        "pct_falso_entre_sostenidos": g(t, "pct_falso_entre_sostenidos", "real"),
        "pct_falso_corridos": g(t, "pct_falso_entre_sostenidos", "corridos"),
        "M2_real": rec.get("real"), "M2_corridos": rec.get("corridos"), "M2_azar": rec.get("azar_media"),
        "recorrido_medio": g(t, "recorrido_medio", "real"), "pct_llega_opuesta": g(t, "pct_llega_opuesta", "real"),
        "medianas": t.get("medianas"),
        "cob20_real": cob.get("real"), "cob20_azar": cob.get("azar_media"), "cob20_lift_azar": cob.get("lift_vs_azar"),
        "cob20_p_azar": cob.get("p_azar"), "cob20_giros": cob.get("den"),
        "cob40_real": cob40.get("real"), "cob40_lift_azar": cob40.get("lift_vs_azar"),
        "toques": tq.get("toques"), "rebote_real": g(tq, "pct_rebote", "real"), "rebote_azar": g(tq, "pct_rebote", "azar_media"),
        "rebote_p": g(tq, "pct_rebote", "p_azar"), "extremo_real": g(tq, "pct_extremo", "real"), "extremo_azar": g(tq, "pct_extremo", "azar_media"),
        "giro15_real": g(tq, "pct_giro15", "real"), "giro15_azar": g(tq, "pct_giro15", "azar_media"),
        "eco_dist_mediana": eco.get("dist_mediana_al_precio"), "eco_beta_pista_15": eco.get("beta_pista_15"),
        "eco_minutos_con_rayas": eco.get("minutos_con_rayas"), "eco_pistas_por_hora": eco.get("pistas_nuevas_por_hora"),
        "estratos": g(r, "estratos", w),
        "por_sesion": m.get("por_sesion"),
    }


def main(fase):
    out = {}
    for p in sorted(glob.glob(os.path.join(AQUI, "resultados_" + fase, "*.json"))):
        r = json.load(open(p, encoding="utf-8"))
        cid = os.path.basename(p)[:-5]
        out[cid] = {"chequeo_futuro": r.get("chequeo_futuro"), "segundos": r["meta"].get("segundos"), "dias": r["meta"]["dias"],
                    "pivotes_descartados": r["meta"].get("pivotes_descartados_por_sanidad"),
                    "noche": fila(r, "noche"), "dia": fila(r, "dia")}
    with open(os.path.join(AQUI, "tabla_%s.json" % fase), "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1)
    f = lambda x, d=1: "—" if x is None else ("%." + str(d) + "f") % x
    for cid, v in out.items():
        print("=" * 100)
        print(cid, "chequeo_futuro", v["chequeo_futuro"])
        for w in ("noche", "dia"):
            x = v[w]
            print("  %-5s lleg %4d base %4d cens %3d ses %2d strikes %3d niv5 %3d | M1 %s corr %s azar %s [%s,%s] p %s | v_corr %s IC95 [%s,%s] IC90inf %s | v_azar %s"
                  % (w, x["llegadas"], x["base"], x["censuradas"], x["sesiones_con_llegadas"], x["strikes_distintos"], x["niveles_distintos_5pts"],
                     f(x["M1_real"]), f(x["M1_corridos"]), f(x["M1_azar_media"]), f(x["M1_azar_p5"]), f(x["M1_azar_p95"]), f(x["p_azar"], 3),
                     f(x["ventaja_vs_corridos"]), f((x["ic95_vs_corridos"] or [None])[0]), f((x["ic95_vs_corridos"] or [None, None])[1]),
                     f(x["ic90_inf_vs_corridos"]), f(x["ventaja_vs_azar"])))
            print("        estricto %s muy_tol %s | rotas %s exactos %s falsos %s (corr %s) | M2 %s corr %s azar %s | cob20 %s/%s lift %s (giros %s) | toque %s reb %s/%s ext %s/%s | eco d %s beta %s | ganadas %s/%s"
                  % (f(x["estricto_ventaja_vs_corridos"]), f(x["muy_tolerante_ventaja_vs_corridos"]), f(x["pct_rotas"]), f(x["pct_exactos"]),
                     f(x["pct_falso_entre_sostenidos"]), f(x["pct_falso_corridos"]), f(x["M2_real"]), f(x["M2_corridos"]), f(x["M2_azar"]),
                     f(x["cob20_real"]), f(x["cob20_azar"]), f(x["cob20_lift_azar"], 2), x["cob20_giros"], x["toques"], f(x["rebote_real"]),
                     f(x["rebote_azar"]), f(x["extremo_real"]), f(x["extremo_azar"]), f(x["eco_dist_mediana"]), f(x["eco_beta_pista_15"], 2),
                     x["sesiones_ganadas"], x["sesiones_con_3_o_mas"]))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "ENTRENAMIENTO")
