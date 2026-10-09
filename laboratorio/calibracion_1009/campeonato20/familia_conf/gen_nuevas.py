# -*- coding: utf-8 -*-
"""
gen_nuevas.py — las 8 formulas nuevas de la investigacion (PREREGISTRO.md sec. 7, grupos 1 y 3) generadas para TODAS las sesiones con
su libro, con el codigo de los probadores SIN CAMBIOS (se importa; no se escribe nada en sus carpetas):
  C04_DOS_QQQ_DERIVA, C06_REGIMEN_POS_QQQ          probador1/candidatas.sesion(dia, que=('C04', 'C06'))
  C13_IMAN_QQQ_vol, C14_REPEL_QQQ_vol, C16_CONFLUENCIA_QQQ_NDX, C17_BANDA_EM, C18_CW_PW_OI_QQQ
                                                    probador3/g3_niveles.sesion_qqq_ndx(dia, diag)
  C15_FLUJO_NQ                                      probador3/g3_niveles.sesion_nq(dia, diag)
Tambien guarda la grilla de cada sesion (minutos con F valido) para armar las claves vacias.
Salida: datos/nuevas/<ID>.parquet (t, nivel, etiqueta, K, libro, dia) y datos/nuevas/grillas.pkl, datos/nuevas/diag.json.
SOLO LECTURA del dataset. Un proceso, prioridad baja. Uso: python -I gen_nuevas.py
"""
import ctypes
import json
import os
import sys
import time

os.environ.setdefault("OMP_NUM_THREADS", "1")
try:
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
except Exception:
    pass

import pandas as pd  # noqa: E402

AQUI = os.path.dirname(os.path.abspath(__file__))
CAL = os.path.normpath(os.path.join(AQUI, "..", ".."))
for _p in (os.path.join(CAL, "datos"), os.path.join(CAL, "arnes"), os.path.join(CAL, "probador1"), os.path.join(CAL, "probador3")):
    if _p not in sys.path:
        sys.path.insert(0, _p)
import cargar as D  # noqa: E402
import evaluar as EV  # noqa: E402
import candidatas as P1  # noqa: E402
import g3_niveles as G3  # noqa: E402

SALIDA = os.path.join(AQUI, "datos", "nuevas")
COLS = ["t", "nivel", "etiqueta", "K", "libro", "dia"]


def main():
    os.makedirs(SALIDA, exist_ok=True)
    t0 = time.time()
    acc = {k: [] for k in ("C04_DOS_QQQ_DERIVA", "C06_REGIMEN_POS_QQQ", "C13_IMAN_QQQ_vol", "C14_REPEL_QQQ_vol", "C15_FLUJO_NQ",
                           "C16_CONFLUENCIA_QQQ_NDX", "C17_BANDA_EM", "C18_CW_PW_OI_QQQ")}
    diag = {"p1": {}, "g3": {}}
    grillas = {}
    dq = D.dias("QQQ"); dn = D.dias("NQ")
    for d in sorted(set(dq) | set(dn)):
        g = EV.minutos_y_precio(d)
        grillas[d] = [t for t, F in zip(g["t"], g["F"]) if F == F]
        if d in dq:
            r, dg = P1.sesion(d, ("C04", "C06"))
            diag["p1"][d] = dg
            for cid, nom in (("C04", "C04_DOS_QQQ_DERIVA"), ("C06", "C06_REGIMEN_POS_QQQ")):
                x = r[cid]
                if len(x):
                    acc[nom].append(x[COLS])
            r3 = G3.sesion_qqq_ndx(d, diag["g3"])
            for k, v in r3.items():
                if v:
                    acc[k].append(pd.DataFrame(v, columns=COLS))
        if d in dn:
            v = G3.sesion_nq(d, diag["g3"])
            if v:
                acc["C15_FLUJO_NQ"].append(pd.DataFrame(v, columns=COLS))
        print(d, {k[:3]: sum(len(x) for x in v) for k, v in acc.items()}, "%.0f s" % (time.time() - t0), flush=True)
    for k, v in acc.items():
        df = pd.concat(v, ignore_index=True) if v else pd.DataFrame(columns=COLS)
        df.to_parquet(os.path.join(SALIDA, "%s.parquet" % k), index=False)
    pd.to_pickle(grillas, os.path.join(SALIDA, "grillas.pkl"))
    with open(os.path.join(SALIDA, "diag.json"), "w", encoding="utf-8") as fh:
        json.dump(diag, fh, ensure_ascii=False, indent=1, default=str)
    print("listo %.0f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
