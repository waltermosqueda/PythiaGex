# -*- coding: utf-8 -*-
"""hoy_41_real.py — DESCRIPTIVO (n = 1 noche, fuera de toda decision): FAM_MUROS_vol, FAM_MUROS_oi y CONF_vol tal como los GRABO la 4.1
esta noche (%APPDATA%/ATAS/PythiaGex4/familia/niv-2026-10-09-MNQZ6.jsonl, SOLO LECTURA; clave k = vigente en la vela k -> desfase 0),
juzgados con juez20 sobre las velas MNQZ6 hasta la ultima vela cerrada (juez20/velas_hasta_ahora.construir). Ventana: desde las 22:00Z
del 10-08 hasta la ultima vela. Escribe resultados/hoy_41_real.json. Uso: python -I hoy_41_real.py [n_azar]"""
import ctypes
import json
import os
import sys

try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass
import pandas as pd  # noqa: E402

AQUI = os.path.dirname(os.path.abspath(__file__))
J20DIR = os.path.normpath(os.path.join(AQUI, "..", "juez20"))
sys.path.insert(0, J20DIR)
import juez20 as J20  # noqa: E402
from velas_hasta_ahora import construir  # noqa: E402

NIV = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex4", "familia", "niv-2026-10-09-MNQZ6.jsonl")


def main():
    n_azar = int(sys.argv[1]) if len(sys.argv) > 1 else 500
    V, info = construir()
    V = V[V["t"] >= pd.Timestamp("2026-10-08 22:00")].reset_index(drop=True)
    out = {"velas": info, "ventana": ["2026-10-08 22:00", str(V["t"].max())], "series": {}}
    for s in ("FAM_MUROS_vol", "FAM_MUROS_oi", "CONF_vol"):
        R = J20.rayas_desde_niv41(NIV, series=[s])
        # grilla completa: los minutos del niv sin la serie quedan como clave vacia (la 4.1 no la dibujaba)
        res = J20.evaluar(V, R, ventanas=[(pd.Timestamp("2026-10-08 22:00"), V["t"].max() + pd.Timedelta(minutes=1))],
                          opciones={"desfase_min": 0}, n_azar=n_azar)
        m = res["metricas"]; pl = res["placebo"]; a = pl["azar"]["pct_acierto20"]
        out["series"][s] = {"metricas": m, "tasa_base_azar": pl["tasa_base_azar"], "tasa_base_corridas": pl["tasa_base_corridas"],
                            "percentil": a["percentil"], "p_valor": a["p_valor"], "n_pistas": res["n_pistas"],
                            "eventos": [{k: (str(v) if not isinstance(v, (int, float, bool, type(None))) else v)
                                         for k, v in e.items() if k in ("t", "tipo", "raya", "resultado", "acierto20", "falsa",
                                                                        "dist_rotura", "recorrido", "precision", "min_a_decision")}
                                        for e in res["eventos"]]}
        print(s, "llegadas %d aciertos %d falsas %d indef %d cens %d | %%a20 %.1f | azar %.1f corr %.1f pctl %.1f | rayas/h %.1f" % (
            m["llegadas"], m["aciertos"], m["falsas"], m["indefinidas"], m["censuradas"], m["pct_acierto20"] if m["base"] else float("nan"),
            pl["tasa_base_azar"], pl["tasa_base_corridas"], a["percentil"], m["rayas_por_hora"]), flush=True)
    os.makedirs(os.path.join(AQUI, "resultados"), exist_ok=True)
    with open(os.path.join(AQUI, "resultados", "hoy_41_real.json"), "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1, default=str)


if __name__ == "__main__":
    main()
