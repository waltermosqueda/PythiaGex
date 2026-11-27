# -*- coding: utf-8 -*-
"""RONDA 19, explorador MODELO: rehace registro_variantes_modelo.csv (mismas columnas que registro_variantes.csv) con TODAS las variantes
probadas en diseno (etapa 1: 46, etapa 2: 8, mismas entradas con otras salidas: 10), con el p del placebo de etiquetas barajadas por rueda
(si ya existe juez_p.json) en la nota. Uso: python -I m7_registro.py"""
import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import m0_comun as C


def main():
    r1 = json.load(open(os.path.join(C.TMP, "real.json"))); r2 = json.load(open(os.path.join(C.TMP, "real2.json")))
    sr = json.load(open(os.path.join(C.TMP, "salidas_real.json")))
    pj = os.path.join(C.TMP, "juez_p.json")
    P = json.load(open(pj)) if os.path.exists(pj) else {}
    if os.path.exists(C.REGISTRO): os.remove(C.REGISTRO)
    for etapa, r in (("etapa 1 (46 declaradas antes de correr)", r1), ("etapa 2 (preregistro_etapa2.txt)", r2)):
        for v in r["variantes"]:
            m = r["metricas"][v["nombre"]]; ni = r["internas"][v["nombre"]]
            nota = "%s; fuera de pliegue (LORO, embargo +-1); precision por evento %s, eventos elegidos por rueda %.2f" % (
                etapa, "%.1f%%" % m["precision_eventos"] if m.get("precision_eventos") == m.get("precision_eventos") and m.get("precision_eventos") is not None else "-",
                m["eventos_elegidos_por_rueda"])
            if ni: nota += "; busqueda interna %d candidatas por pliegue (cuentan como variantes)" % ni
            if v["proc"] == "PLANTILLA": nota += "; forma elegida mirando las reglas de la etapa 1 (sesgo de seleccion declarado)"
            pp = P.get(v["nombre"])
            if pp: nota += "; placebo etiquetas barajadas por rueda (%d replicas): p_t %.3f, p_acierto %.3f, p_familia %.3f" % (pp["nrep"], pp["p_t"], pp["p_acierto"], pp["p_familia"])
            C.anotar(v["nombre"], v["salida"], v["parametros"], m, nota=nota)
    for c in sr:
        base_sal = c[-2:]
        for s, m in sr[c].items():
            if s == base_sal: continue
            nom = "%s>%s" % (c, s)
            nota = "mismas entradas fuera de pliegue de %s operadas con la salida %s (variante nueva)" % (c, s)
            pp = P.get(nom)
            if pp: nota += "; placebo (%d replicas): p_t %.3f, p_acierto %.3f" % (pp["nrep"], pp["p_t"], pp["p_acierto"])
            C.anotar(nom, s, "entradas de %s (fuera de pliegue) con salida %s; 1 posicion por vez, max 5/rueda" % (c, s), m, nota=nota)
    print("registro rehecho:", C.REGISTRO)


if __name__ == "__main__":
    main()
