# -*- coding: utf-8 -*-
"""diag_filtros.py — cuanto recortan los filtros de las combinaciones (sin mirar resultados: solo cuenta niveles). SOLO LECTURA de
datos/rayas. Por serie: minutos con nivel, niveles-minuto, y la fraccion que conserva cada filtro contra su base:
  FAM_MUROS_vol_TOP / FAM_MUROS_vol, FAM_MUROS_oi_TOP / FAM_MUROS_oi, FAM_MUROS_oi_DOI / FAM_MUROS_oi (en los minutos con dOI conocido),
  MUROS_oi_DOI / MUROS_oi_UNION (por libro y lado). Escribe datos/diag_filtros.json."""
import ctypes
import json
import os
from collections import Counter

ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
import pandas as pd  # noqa: E402

AQUI = os.path.dirname(os.path.abspath(__file__))
DR = os.path.join(AQUI, "datos", "rayas")


def cargar(s):
    return pd.read_pickle(os.path.join(DR, "%s.pkl" % s))


def niveles_minuto(R):
    return sum(len(v) for v in R.values()), sum(1 for v in R.values() if v)


def main():
    out = {}
    for s in sorted(f[:-4] for f in os.listdir(DR) if f.endswith(".pkl")):
        R = cargar(s)
        n, m = niveles_minuto(R)
        out[s] = {"claves": len(R), "minutos_con_nivel": m, "niveles_minuto": n,
                  "sesiones": len({(t + pd.Timedelta(hours=2)).strftime("%Y-%m-%d") for t, v in R.items() if v})}
    for base, filt in (("FAM_MUROS_vol", "FAM_MUROS_vol_TOP"), ("FAM_MUROS_oi", "FAM_MUROS_oi_TOP"), ("FAM_MUROS_oi", "FAM_MUROS_oi_DOI"),
                       ("MUROS_oi_UNION", "MUROS_oi_DOI")):
        B = cargar(base); F = cargar(filt)
        c = Counter(); k = Counter()
        for t, v in B.items():
            for clave in v:
                lado = clave.split(".")[-1]
                lib = clave.split(".")[0] if "." in clave else "FAM"
                c[(lib, lado)] += 1
                if clave in F.get(t, {}):
                    k[(lib, lado)] += 1
        out["%s_sobre_%s" % (filt, base)] = {"%s_%s" % kk: {"base": c[kk], "conserva": k[kk], "pct": round(100.0 * k[kk] / c[kk], 1) if c[kk] else None}
                                             for kk in sorted(c)}
    with open(os.path.join(AQUI, "datos", "diag_filtros.json"), "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1)
    print(json.dumps(out, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    main()
