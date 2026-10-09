# -*- coding: utf-8 -*-
"""
correr.py — PROBADOR 4: arma las rayas de C19..C24 y las juzga con el arnes (../arnes/evaluar.py v1.0), por fase.

  python -I correr.py ENTRENAMIENTO            -> n_azar=500, n_boot=2000 (PREREGISTRO sec. 7.0)
  python -I correr.py ENTRENAMIENTO --marco2   -> sensibilidad con velas de 2 min (sec. 2), informativa
  python -I correr.py PRUEBA <sello>           -> n_azar=2000, n_boot=2000, abrir_prueba=True con el sello congelado

Escribe: ../probadores/grupo4/<ID>/niveles_<FASE>.parquet (formato del pre-registro, sec. 7.0) y
         resultados/<ID>_<FASE>[_m2].json (salida del arnes, sin los repeticiones crudas del azar) + diag_<FASE>.json.
"""
import json
import os
import sys
import time
from concurrent.futures import ProcessPoolExecutor

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import candidatas as C      # noqa: E402
import evaluar as EV        # noqa: E402

ENTRENAMIENTO = ["2026-08-17", "2026-08-18", "2026-08-19", "2026-08-20", "2026-08-21", "2026-08-24", "2026-08-25", "2026-08-26",
                 "2026-08-27", "2026-08-28", "2026-08-31", "2026-09-01", "2026-09-02", "2026-09-03", "2026-09-04", "2026-09-07",
                 "2026-09-08", "2026-09-09", "2026-09-10", "2026-09-11", "2026-09-14", "2026-09-15", "2026-09-16", "2026-09-17",
                 "2026-09-18", "2026-09-21", "2026-09-22", "2026-09-23", "2026-09-24", "2026-09-25", "2026-09-28", "2026-09-29",
                 "2026-09-30"]
PRUEBA = ["2026-10-01", "2026-10-02", "2026-10-05", "2026-10-06", "2026-10-07", "2026-10-08"]
RES = os.path.join(AQUI, "resultados")
NIV = os.path.join(os.path.dirname(AQUI), "probadores", "grupo4")


def _sin_reps(o):
    if isinstance(o, dict):
        return {k: _sin_reps(v) for k, v in o.items() if k != "_azar_reps"}
    if isinstance(o, list):
        return [_sin_reps(x) for x in o]
    return o


def uno(args):
    cid, fase, marco, sello = args
    dias_base = ENTRENAMIENTO if fase == "ENTRENAMIENTO" else PRUEBA
    dias = C.dias_de(cid, dias_base)
    diag = {}
    niv = C.niveles(cid, dias, diag)
    os.makedirs(os.path.join(NIV, cid), exist_ok=True)
    pq = os.path.join(NIV, cid, "niveles_%s.parquet" % fase)
    if marco == 1:
        niv.to_parquet(pq, index=False)
    fut = EV.chequeo_futuro(niv, dias)
    kw = dict(nombre=cid, n_azar=(500 if fase == "ENTRENAMIENTO" else 2000), n_boot=2000, marco=marco)
    if fase == "PRUEBA":
        kw.update(abrir_prueba=True, sello=sello)
    t0 = time.time()
    r = EV.evaluar(niv, dias, **kw)
    r["probador4"] = {"dias_usados": dias, "dias_sin_libro": [d for d in dias_base if d not in dias], "filas": int(len(niv)),
                      "parquet": pq if marco == 1 else None, "chequeo_futuro": fut, "diag_por_sesion": diag,
                      "parametros": C.PARAMETROS[cid], "segundos_evaluar": round(time.time() - t0, 1)}
    suf = "" if marco == 1 else "_m2"
    out = os.path.join(RES, "%s_%s%s.json" % (cid, fase, suf))
    with open(out, "w", encoding="utf-8") as f:
        json.dump(EV.limpiar_para_json(_sin_reps(r)), f, ensure_ascii=False, indent=1)
    # las repeticiones del azar por separado (por si hace falta rehacer un p)
    reps = {}
    for modo, bm in r["operador"].items():
        for w, b in bm.items():
            s = b.get("pct_sostenidos", {})
            if "_azar_reps" in s:
                reps["%s/%s" % (modo, w)] = s["_azar_reps"]
    with open(out.replace(".json", "_azar_reps.json"), "w", encoding="utf-8") as f:
        json.dump(reps, f)
    return cid, out, round(time.time() - t0, 1)


if __name__ == "__main__":
    os.makedirs(RES, exist_ok=True)
    fase = sys.argv[1]
    marco = 2 if "--marco2" in sys.argv else 1
    sello = None
    if fase == "PRUEBA":
        sello = sys.argv[2]
    elif fase != "ENTRENAMIENTO":
        raise SystemExit("fase: ENTRENAMIENTO | PRUEBA")
    ids = [a for a in sys.argv[2:] if a in C.IDS] or list(C.IDS)
    t0 = time.time()
    with ProcessPoolExecutor(max_workers=6) as ex:
        for cid, out, seg in ex.map(uno, [(c, fase, marco, sello) for c in ids]):
            print(cid, "->", out, "(%s s)" % seg, flush=True)
    print("listo en %.0f s" % (time.time() - t0))
