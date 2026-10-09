# -*- coding: utf-8 -*-
"""e04_p_oficial.py — esceptico2: re-corre juez20.evaluar (sin tocarlo) con las MISMAS entradas, semilla y n_azar que cada familia,
para comprobar que el p informado sale tal cual (reproducibilidad del numero de titular). Solo lectura.
Uso: python -I e04_p_oficial.py <caso> [<caso> ...]  -> datos/e04_<caso>.json"""
import json
import os
import sys
import time

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import e00_comun as E  # noqa: E402
E.prioridad_baja()


def main():
    for nombre in sys.argv[1:]:
        t0 = time.time()
        cs = E.caso(nombre)
        inf = E.informado(nombre)
        res = E.J20.evaluar(cs["V"], cs["rayas"], ventanas=cs["ventanas"], opciones=cs["opciones"], n_azar=inf["n_azar"], n_boot=200)
        a = res["placebo"]["azar"]["pct_acierto20"]
        out = dict(caso=nombre, pct=res["metricas"]["pct_acierto20"], llegadas=res["metricas"]["llegadas"], p50=a["p50"], p=a["p_valor"],
                   percentil=a["percentil"], p_normal=a["p_normal"], sd=a["sd"], informado=inf, seg=round(time.time() - t0, 1))
        with open(os.path.join(AQUI, "datos", "e04_%s.json" % nombre.replace("|", "__")), "w", encoding="utf-8") as fh:
            json.dump(out, fh, ensure_ascii=False, indent=1, default=float)
        print("%-34s juez20 re-corrido: %5.2f %% llegadas %d p50 %.2f pctl %.1f p %.4f pN %.4f sd %.2f | informado: %.2f %% p50 %.2f pctl %.1f p %.4f" % (
            nombre, out["pct"], out["llegadas"], out["p50"], out["percentil"], out["p"], out["p_normal"], out["sd"], inf["pct"], inf["p50"],
            inf["pctl"], inf["p"]), flush=True)


if __name__ == "__main__":
    main()
