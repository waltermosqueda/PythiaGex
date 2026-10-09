# -*- coding: utf-8 -*-
"""comparar.py — DESCRIPTIVO: cada combinacion contra su serie base (mismo libro sin el filtro), en MIRAR y PRUEBA, noche y rueda.
Lee resultados/tabla.json. Escribe resultados/comparar.json y lo imprime. No elige nada: la PRUEBA se informa entera."""
import json
import os

AQUI = os.path.dirname(os.path.abspath(__file__))
PARES = [("FAM_MUROS_vol_TOP", "FAM_MUROS_vol"), ("FAM_MUROS_oi_TOP", "FAM_MUROS_oi"), ("FAM_MUROS_oi_DOI", "FAM_MUROS_oi"),
         ("MUROS_oi_DOI", "MUROS_oi_UNION"), ("CONF_NDX_NQ_vol", "CONF_vol"), ("CONF_todo", "CONF_vol")]


def main():
    T = json.load(open(os.path.join(AQUI, "resultados", "tabla.json"), encoding="utf-8"))
    ix = {(f["serie"], f["ventana"], f["parte"]): f for f in T}
    out = []
    for a, b in PARES:
        for w in ("noche", "rueda"):
            for p in ("MIRAR", "PRUEBA"):
                fa, fb = ix.get((a, w, p)), ix.get((b, w, p))
                if not fa or not fb:
                    continue
                r = {"combinacion": a, "base": b, "ventana": w, "parte": p,
                     "noches_iguales": fa["noches"] == fb["noches"] and fa["desde"] == fb["desde"] and fa["hasta"] == fb["hasta"],
                     "pct_a20": (fa["pct_acierto20"], fb["pct_acierto20"]),
                     "dif_pp": (fa["pct_acierto20"] - fb["pct_acierto20"]) if fa["pct_acierto20"] == fa["pct_acierto20"] else None,
                     "llegadas": (fa["llegadas"], fb["llegadas"]), "falsas_noche": (fa["falsas_noche"], fb["falsas_noche"]),
                     "aciertos_noche": (fa["aciertos_noche"], fb["aciertos_noche"]), "rayas_hora": (fa["rayas_hora"], fb["rayas_hora"]),
                     "percentil": (fa["percentil"], fb["percentil"])}
                out.append(r)
                print("%-18s vs %-15s %-5s %-6s  %%a20 %5.1f vs %5.1f (%+5.1f pp)  lleg %4d vs %4d  falsas/n %.2f vs %.2f  ray/h %.1f vs %.1f"
                      "  pctl %5.1f vs %5.1f%s" % (a, b, w, p, fa["pct_acierto20"], fb["pct_acierto20"], r["dif_pp"] or 0, fa["llegadas"],
                                                    fb["llegadas"], fa["falsas_noche"], fb["falsas_noche"], fa["rayas_hora"], fb["rayas_hora"],
                                                    fa["percentil"], fb["percentil"], "" if r["noches_iguales"] else "  (noches distintas)"))
    with open(os.path.join(AQUI, "resultados", "comparar.json"), "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
