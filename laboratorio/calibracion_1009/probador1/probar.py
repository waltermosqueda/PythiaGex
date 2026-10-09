# -*- coding: utf-8 -*-
"""
probador1/probar.py — PRUEBA sellada del grupo 1 (PREREGISTRO sec. 9). Corre SOLO si:
  - existe probador1/finalistas.json y su sha256 coincide con probador1/finalistas.sha256;
  - el codigo del probador (candidatas.py, entrenar.py, congelar.py, probar.py, caso.py, verificar.py) es el congelado.
Despues: niveles de PRUEBA con el codigo congelado -> probadores/grupo1/<ID>/niveles_PRUEBA.parquet; evaluar(..., n_azar=2000,
n_boot=2000, abrir_prueba=True, sello=finalistas.json) -> resultados/prueba/<ID>.json; Holm (local y cota global), E1-E6
-> resultados/prueba/resumen_prueba.json.
Uso: python -I probar.py [niveles|evaluar|resumen|todo]
"""
import hashlib
import json
import os
import sys
import time
from concurrent.futures import ProcessPoolExecutor

import numpy as np
import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import candidatas as C                      # noqa: E402

E = C.E
D = C.D
SELLO = os.path.join(AQUI, "finalistas.json")
RES = os.path.join(AQUI, "resultados", "prueba")
POR_NOMBRE = {v: k for k, v in C.NOMBRES.items()}


def sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def control_sello():
    esperado = {}
    for l in open(os.path.join(AQUI, "finalistas.sha256"), encoding="utf-8"):
        h, n = l.split()
        esperado[n] = h
    if sha(SELLO) != esperado["finalistas.json"]:
        raise SystemExit("finalistas.json cambio despues de congelar")
    for n, h in esperado.items():
        if n.endswith(".py") and sha(os.path.join(AQUI, n)) != h:
            raise SystemExit("%s cambio despues de congelar" % n)
    return json.load(open(SELLO, encoding="utf-8"))


def dias_prueba(cid):
    return [d for d in E.PRUEBA if d in D.dias(C.LIBRO[cid])]


def paso_niveles(sello):
    ids = [POR_NOMBRE[x] for x in sello["a_prueba"]]
    todos = sorted(set(d for c in ids for d in dias_prueba(c)))
    res, diags = C.construir(todos, "PRUEBA", tuple(ids))
    os.makedirs(RES, exist_ok=True)
    out = {}
    for cid in ids:
        df = res[cid]
        p = os.path.join(C.SALIDA, C.NOMBRES[cid], "niveles_PRUEBA.parquet")
        out[cid] = {"filas": len(df), "minutos": int(df["t"].nunique()) if len(df) else 0, "sesiones": dias_prueba(cid),
                    "chequeo_futuro": E.chequeo_futuro(df, dias_prueba(cid)) if len(df) else {}, "sha256_niveles": sha(p),
                    "libro_dom": df["libro_dom"].value_counts().to_dict() if len(df) else {}}
        print(cid, json.dumps(out[cid], default=str), flush=True)
    with open(os.path.join(RES, "niveles_resumen.json"), "w", encoding="utf-8") as f:
        json.dump({"resumen": out, "diag": diags}, f, ensure_ascii=False, indent=1, default=str)


def _uno(cid):
    t0 = time.time()
    p = os.path.join(C.SALIDA, C.NOMBRES[cid], "niveles_PRUEBA.parquet")
    df = pd.read_parquet(p)
    r = E.evaluar(df, dias_prueba(cid), nombre=C.NOMBRES[cid], n_azar=2000, n_boot=2000, abrir_prueba=True, sello=SELLO)
    r["meta"]["niveles_sha256"] = sha(p)
    with open(os.path.join(RES, "%s.json" % C.NOMBRES[cid]), "w", encoding="utf-8") as f:
        json.dump(E.limpiar_para_json(r), f, ensure_ascii=False, indent=1)
    return cid, round(time.time() - t0, 1)


def paso_evaluar(sello, workers=3):
    ids = [POR_NOMBRE[x] for x in sello["a_prueba"]]
    with ProcessPoolExecutor(max_workers=workers) as ex:
        for cid, s in ex.map(_uno, ids):
            print("PRUEBA evaluada", cid, s, "s", flush=True)


def _nan(o):
    """JSON (NaN guardado como null) -> otra vez NaN, para que criterios_exito compare numeros."""
    if isinstance(o, dict):
        return {k: _nan(v) for k, v in o.items()}
    if isinstance(o, list):
        return [_nan(v) for v in o]
    return float("nan") if o is None else o


def paso_resumen(sello):
    sys.path.insert(0, AQUI)
    from entrenar import _fila
    noms = sello["a_prueba"]
    R = {n: _nan(json.load(open(os.path.join(RES, "%s.json" % n), encoding="utf-8"))) for n in noms}
    fam = [(n, w) for n in noms for w in E.VENTANAS]
    p = [R[n]["operador"]["tolerante"][w]["pct_sostenidos"].get("p_azar", float("nan")) for n, w in fam]
    p = [float("nan") if x is None else x for x in p]
    ph = E.holm(p)
    nfin = len([x for x in noms if x not in sello["referencias_siempre_a_prueba"]])
    m_global = (9 + nfin) * 2
    out = {"generado_utc": pd.Timestamp.now("UTC").isoformat(timespec="seconds"), "sello_sha256": sha(SELLO),
           "familia_local": ["%s|%s" % x for x in fam], "m_bonferroni_global": m_global, "filas": {}}
    for (n, w), pi, phi in zip(fam, p, ph):
        r = R[n]
        crit_local = E.criterios_exito(r, w, phi)
        pb = min(1.0, pi * m_global) if pi == pi else float("nan")
        crit_glob = E.criterios_exito(r, w, pb)
        out["filas"]["%s|%s" % (n, w)] = {"p_azar": pi, "p_holm_local": phi, "p_bonferroni_global": pb,
                                         "criterios_holm_local": crit_local, "criterios_bonferroni_global": crit_glob,
                                         "resumen": _fila(r, w), "rol": E._rol(n)}
        print(n, w, "M1 %.1f corr %.1f azar %.1f p %.4f holm %.4f | %s" % (
            out["filas"]["%s|%s" % (n, w)]["resumen"]["M1"] or float("nan"), out["filas"]["%s|%s" % (n, w)]["resumen"]["corridos"] or float("nan"),
            out["filas"]["%s|%s" % (n, w)]["resumen"]["azar"] or float("nan"), pi, phi,
            {k: v for k, v in crit_local.items()}), flush=True)
    with open(os.path.join(RES, "resumen_prueba.json"), "w", encoding="utf-8") as f:
        json.dump(E.limpiar_para_json(out), f, ensure_ascii=False, indent=1)
    return out


if __name__ == "__main__":
    que = sys.argv[1] if len(sys.argv) > 1 else "todo"
    s = control_sello()
    if que in ("niveles", "todo"):
        paso_niveles(s)
    if que in ("evaluar", "todo"):
        paso_evaluar(s)
    if que in ("resumen", "todo"):
        paso_resumen(s)
