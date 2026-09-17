# -*- coding: utf-8 -*-
"""
verdes_10_replica.py — LA FUERZA DE LA RAYA: primero NQ (donde aparecio la pista, exploratorio) y despues ES/MES (REPLICA con pre-registro, otro mercado,
otro libro de opciones, mismos dias). El pre-registro de ES se escribe en resultados/verdes.md ANTES de calcular nada de ES.
"""
import os, sys, datetime
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import base as B, verdes_lib as V

MD = os.path.join(os.path.dirname(os.path.abspath(__file__)), "resultados", "verdes.md")
PRE = """# Las verdes fuertes: la FUERZA de la raya (pista del operador del 17-09)

## Pre-registro de la replica en ES/MES (escrito %s, ANTES de calcular nada de ES)

**De donde viene:** en MNQ, 7 dias, 247 ordenes limitadas ejecutadas en las dominantes del libro de opciones de NQ: el rebote (+12 antes que -6) sube con la fuerza
de la raya (gamma x volumen del strike): 32 %% -> 36 %% -> 44 %% -> 61 %%, y las mismas rayas corridas hacen lo contrario (39 -> 28 %%). Exploratorio, cortes puestos a mano.

**Replica (otro mercado, mismo calculo):** dominantes del libro de opciones de ES (viva-ES) contra la cinta de MES. Parametros a escala de ES (1/4 de NQ): tolerancia 0,5,
venia de >= 2,5 pts, regla principal +3 / -1,5 desde la raya, orden limitada con llenado real, solo rueda, raya con foto de menos de 5 min.
- **H1 (fuerza):** con los toques partidos en CUARTILES de fuerza (calculados sobre la propia muestra de ES, sin mirar resultados), el cuartil mas fuerte rebota
  >= 10 puntos porcentuales mas que el mas debil, Y el cuartil mas fuerte de las rayas reales rebota >= 10 puntos mas que su gemelo placebo (rayas corridas +-2,5 y +-1,25).
- **H2 (velocidad):** el tercio que llega mas LENTO (|precio - precio de hace 60 s|) rebota >= 8 puntos mas que el tercio mas rapido.
- **Veredicto:** REPLICA si se cumple H1 completa; PARCIAL si se cumple solo una de sus dos mitades; NO REPLICA si ninguna. H2 se informa aparte. No se retoca nada despues de ver el resultado.
"""


def informe(raiz, R, P, regla):
    out = []; M = V.MERCADO[raiz]; col = "r%g_%g" % regla
    y = R[col]; yp = P[col]; pr = (y[y != 0] > 0).mean(); pp = (yp[yp != 0] > 0).mean()
    out.append("%s, regla +%g/-%g desde la raya, orden limitada: VERDES %.1f %% de %d | placebo %.1f %% de %d | azar puro %.1f %%" % (raiz, regla[0], regla[1], 100 * pr, (y != 0).sum(), 100 * pp, (yp != 0).sum(), 100 * regla[1] / sum(regla)))
    q = R["fuerza"].quantile([0.25, 0.5, 0.75]).to_list(); cortes = [-1] + q + [1e18]; et = ["Q1 debil", "Q2", "Q3", "Q4 fuerte"]
    out.append("   cuartiles de fuerza (cortes %.0f / %.0f / %.0f M):" % tuple(q))
    out.append("      VERDES : " + V.tabla(R, "fuerza", cortes, et, regla)); out.append("      placebo: " + V.tabla(P, "fuerza", cortes, et, regla))
    t = R["vel60"].quantile([1 / 3, 2 / 3]).to_list(); cv = [-1] + t + [1e18]; ev = ["lento", "medio", "rapido"]
    out.append("   velocidad de llegada (cortes %.2f / %.2f pts en 60 s):" % tuple(t))
    out.append("      VERDES : " + V.tabla(R, "vel60", cv, ev, regla)); out.append("      placebo: " + V.tabla(P, "vel60", cv, ev, regla))
    por_dia = R[R[col] != 0].assign(g=(R[col] > 0).astype(int)).groupby("dia")["g"].agg(["mean", "size"])
    out.append("   por dia: " + " | ".join("%s %.0f %% (%d)" % (d[5:], 100 * v["mean"], v["size"]) for d, v in por_dia.iterrows()))
    return out


if __name__ == "__main__":
    que = sys.argv[1] if len(sys.argv) > 1 else "NQ"
    if que == "NQ":
        R, P = V.correr("NQ", B.todas_las_sesiones(), refrescar="--refrescar" in sys.argv)
        lineas = informe("NQ", R, P, (12, 6)) + informe("NQ", R[R["dia"] != "2026-09-17"], P[P["dia"] != "2026-09-17"], (12, 6))
        print("\n".join(lineas)); R.to_parquet(os.path.join(B.CACHE, "verdes_lib_NQ.parquet"))
    else:
        if not os.path.exists(MD): open(MD, "w", encoding="utf-8").write(PRE % datetime.datetime.now().strftime("%Y-%m-%d %H:%M"))
        R, P = V.correr("ES", B.sesiones_de("MES"), refrescar="--refrescar" in sys.argv)
        lineas = informe("ES", R, P, (3, 1.5)) + informe("ES", R, P, (5, 2))
        print("\n".join(lineas)); R.to_parquet(os.path.join(B.CACHE, "verdes_lib_ES.parquet")); P.to_parquet(os.path.join(B.CACHE, "verdes_lib_ES_placebo.parquet"))
        open(MD, "a", encoding="utf-8").write("\n## Corrida de ES (%s)\n\n" % datetime.datetime.now().strftime("%Y-%m-%d %H:%M") + "\n".join("    " + x for x in lineas) + "\n")
