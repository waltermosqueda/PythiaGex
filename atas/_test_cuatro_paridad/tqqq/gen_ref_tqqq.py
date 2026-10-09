# -*- coding: utf-8 -*-
"""gen_ref_tqqq.py — B4 (TQQQ), 08-10-2026. Referencia de paridad para el modulo TQQQ de la 4.1: corre tqqq_vivo.ciclo (laboratorio/tres/tqqq,
SIN modificarlo) con el reloj congelado en <dia> 21:59:12 UTC y guarda el json que habria escrito, en referencia/tqqq_vivo-<dia>-sim.json.
No escribe nada en profundidad/ ni en el log del laboratorio (se reemplaza tqqq_vivo.log por print). Prioridad BELOW_NORMAL (tq_comun).
Uso:  python -I atas/_test_cuatro_paridad/tqqq/gen_ref_tqqq.py 2026-10-07 [2026-10-08 ...]"""
import os, sys, json
AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", "..", ".."))
sys.path.insert(0, os.path.join(RAIZ, "laboratorio", "tres", "tqqq"))
import tq_comun as C                       # noqa: E402  (baja la prioridad a BELOW_NORMAL)
import pandas as pd                        # noqa: E402
import tqqq_vivo as TV                     # noqa: E402

TV.log = lambda m: print("  [tqqq_vivo] " + m, flush=True)
_orig = pd.Timestamp.utcnow


def correr(dia, hora="21:59:12"):
    ahora = pd.Timestamp(dia + " " + hora, tz="UTC")
    pd.Timestamp.utcnow = staticmethod(lambda: ahora)
    try:
        d = TV.ciclo({})
    finally:
        pd.Timestamp.utcnow = _orig
    if not d:
        print(dia, "sin datos"); return
    sal = os.path.join(AQUI, "referencia", "tqqq_vivo-%s-sim.json" % dia)
    with open(sal, "w", encoding="utf-8") as f:
        json.dump(d, f, ensure_ascii=False, separators=(",", ":"))
    print(dia, "->", sal, "| series", {k: len(v) for k, v in d["historia"].items()}, "| conv", d["conv"])


if __name__ == "__main__":
    for dia in (sys.argv[1:] or ["2026-10-07", "2026-10-08"]):
        correr(dia)
