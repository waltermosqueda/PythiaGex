# -*- coding: utf-8 -*-
"""
g3_evaluar.py — corre el ARNES congelado (arnes/evaluar.py v1.0) sobre las rayas del grupo 3.

  python -I g3_evaluar.py ENTRENAMIENTO            -> resultados_ENTRENAMIENTO/<ID>.json + seleccion_ENTRENAMIENTO.json
  python -I g3_evaluar.py PRUEBA <sello> ID [ID..]  -> resultados_PRUEBA/<ID>.json (solo finalistas; abre el sello del arnes)

ENTRENAMIENTO: n_azar = 500, n_boot = 2000 (PREREGISTRO 7.0). PRUEBA: n_azar = 2000.
Dias: los de la fase donde existe el libro de la candidata (PREREGISTRO sec. 6): QQQ para C13/C14/C16/C17/C18, NQ para C15.
No toca el arnes ni el pre-registro: solo los importa.
"""
import os
import sys
import json
import time
import hashlib
from concurrent.futures import ProcessPoolExecutor

import pandas as pd

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
for _p in (os.path.join(RAIZ, "datos"), os.path.join(RAIZ, "arnes")):
    if _p not in sys.path:
        sys.path.insert(0, _p)
import cargar as D          # noqa: E402
import evaluar as EV        # noqa: E402

NIV = os.path.join(RAIZ, "probadores", "grupo3")
IDS = ("C13_IMAN_QQQ_vol", "C14_REPEL_QQQ_vol", "C15_FLUJO_NQ", "C16_CONFLUENCIA_QQQ_NDX", "C17_BANDA_EM", "C18_CW_PW_OI_QQQ")
LIBRO = {"C15_FLUJO_NQ": "NQ"}


def dias_de(cid, fase):
    lb = LIBRO.get(cid, "QQQ")
    if fase == "ENTRENAMIENTO":
        return [d for d in D.dias(lb) if d <= EV.ENTRENAMIENTO_HASTA]
    return [d for d in D.dias(lb) if d in EV.PRUEBA]


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest()


def _uno(args):
    cid, fase, n_azar, sello = args
    niv = pd.read_parquet(os.path.join(NIV, cid, "niveles_%s.parquet" % fase))
    dias = dias_de(cid, fase)
    t0 = time.time()
    kw = dict(abrir_prueba=True, sello=sello) if fase == "PRUEBA" else {}
    r = EV.evaluar(niv, dias, nombre=cid, n_azar=n_azar, n_boot=2000, **kw)
    r["meta"]["niveles_sha256"] = sha(os.path.join(NIV, cid, "niveles_%s.parquet" % fase))
    r["chequeo_futuro"] = EV.chequeo_futuro(niv, dias)
    os.makedirs(os.path.join(AQUI, "resultados_" + fase), exist_ok=True)
    ruta = os.path.join(AQUI, "resultados_" + fase, cid + ".json")
    with open(ruta, "w", encoding="utf-8") as fh:
        json.dump(EV.limpiar_para_json(r), fh, ensure_ascii=False, indent=1)
    print(cid, fase, "listo en %.0f s" % (time.time() - t0), flush=True)
    return cid, ruta


def entrenamiento():
    with ProcessPoolExecutor(max_workers=3) as ex:
        rutas = dict(ex.map(_uno, [(c, "ENTRENAMIENTO", 500, None) for c in IDS]))
    res = {c: json.load(open(rutas[c], encoding="utf-8")) for c in IDS}
    sel = {"generado_utc": pd.Timestamp.now(tz="UTC").isoformat(), "arnes_sha256": sha(os.path.join(RAIZ, "arnes", "evaluar.py")),
           "resultados_sha256": {c: sha(rutas[c]) for c in IDS}, "por_ventana": {}}
    for w in EV.VENTANAS:
        s = EV.seleccionar_finalistas(res, w)
        s["loo_elegir_la_mejor"] = EV.loo_parametro(res, w)
        sel["por_ventana"][w] = s
    with open(os.path.join(AQUI, "seleccion_ENTRENAMIENTO.json"), "w", encoding="utf-8") as fh:
        json.dump(EV.limpiar_para_json(sel), fh, ensure_ascii=False, indent=1)
    print(json.dumps(EV.limpiar_para_json({w: sel["por_ventana"][w]["finalistas"] for w in EV.VENTANAS})), flush=True)


def prueba(sello, ids):
    with ProcessPoolExecutor(max_workers=min(3, len(ids))) as ex:
        list(ex.map(_uno, [(c, "PRUEBA", 2000, sello) for c in ids]))


if __name__ == "__main__":
    if sys.argv[1] == "ENTRENAMIENTO":
        entrenamiento()
    elif sys.argv[1] == "PRUEBA":
        prueba(sys.argv[2], sys.argv[3:])
