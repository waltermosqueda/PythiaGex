# -*- coding: utf-8 -*-
"""nq01b_fuentes.py — chequeo: la UNION de grabadores (viva, viva2, viva3, viva4) contra cada grabador solo, en las sesiones con mas de
uno. Si las series coinciden minuto a minuto, la union no mete parpadeo. SOLO LECTURA. Escribe datos/nq01b_fuentes.json."""
import ctypes
import json
import os
import sys

try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import nq_comun as C  # noqa: E402

SERIES = ["MUROS_NQ_vol", "MAJORS_NQ_vol", "MUROS_NQ_oi", "ZEST_NQ_vol", "ZTP_NQ_vol", "DOMS_NQ_vol"]


def main():
    out = {}
    for dia, fuentes in (("2026-10-07", ("viva4", "viva", "viva2")), ("2026-10-08", ("viva4", "viva", "viva2")),
                         ("2026-09-21", ("viva", "viva2")), ("2026-10-06", ("viva", "viva2"))):
        union = {r["k"]: r for r in pd.read_pickle(os.path.join(C.DATOS, "registros-%s.pkl" % dia))}
        v1, _ = C.velas_sesion(dia)
        res = {}
        for fu in fuentes:
            fotos, _ = C.leer_fotos(dia, fuentes=(fu,))
            reg, _ = C.minutos_sesion(dia, fotos, v1)
            r_s = {}
            for s in SERIES:
                n = ig = 0
                for r in reg:
                    u = union[r["k"]]
                    if not (r["libro"] and u["libro"]):
                        continue
                    a = sorted((round(p, 2), e) for p, e, _ in r["series"].get(s, []))
                    b = sorted((round(p, 2), e) for p, e, _ in u["series"].get(s, []))
                    if not a and not b:
                        continue
                    n += 1; ig += int(a == b)
                r_s[s] = dict(minutos=n, iguales=ig, pct=round(100.0 * ig / n, 1) if n else None)
            res[fu] = dict(minutos_con_libro=sum(r["libro"] for r in reg), series=r_s)
            print(dia, fu, res[fu]["minutos_con_libro"], {s: r_s[s]["pct"] for s in SERIES}, flush=True)
        out[dia] = res
    with open(os.path.join(C.DATOS, "nq01b_fuentes.json"), "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
