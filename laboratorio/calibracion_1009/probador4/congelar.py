# -*- coding: utf-8 -*-
"""congelar.py — escribe sello_grupo4.json (parametros congelados + sha256 de codigo y resultados de ENTRENAMIENTO) ANTES de abrir PRUEBA."""
import datetime as dt, hashlib, json, os, sys
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI)
import candidatas as C

def sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        h.update(f.read())
    return h.hexdigest()

RAIZ = os.path.dirname(AQUI)
res = {c: os.path.join(AQUI, "resultados", "%s_ENTRENAMIENTO.json" % c) for c in C.IDS}
selec = json.load(open(os.path.join(AQUI, "resumen_ENTRENAMIENTO.json"), encoding="utf-8"))["seleccion"]
ahora = dt.datetime.now(dt.timezone.utc)
s = {
    "que": "SELLO DEL GRUPO 4 (probador 4) de calibracion_1009: controles sin gamma C19..C24. Escrito ANTES de abrir PRUEBA.",
    "hora_utc": ahora.isoformat(timespec="seconds"),
    "hora_art": (ahora - dt.timedelta(hours=3)).strftime("%Y-%m-%d %H:%M:%S") + " ART",
    "finalistas": {"noche": [], "dia": []},
    "finalistas_nota": "ninguna: C19-C24 son CONTROLES (PREREGISTRO sec. 8.1, evaluar.CANDIDATAS rol 'control'): se informan, no compiten. "
                       "La regla de seleccion aplicada a los 6 da 'ninguna' en las dos ventanas y ninguno seria elegible aunque no fuera control.",
    "referencias": [],
    "controles_que_se_miden_en_prueba": list(C.IDS),
    "parametros_congelados": C.PARAMETROS,
    "parametros_libres": "ninguno (PREREGISTRO sec. 6 (3)): todo esta fijado en la sec. 7; no se eligio nada con el entrenamiento",
    "prueba": {"dias": ["2026-10-01", "2026-10-02", "2026-10-05", "2026-10-06", "2026-10-07", "2026-10-08"], "n_azar": 2000, "n_boot": 2000,
               "modos": ["tolerante", "estricto", "muy_tolerante"], "marco_min": 1},
    "sha256_codigo": {"candidatas.py": sha(os.path.join(AQUI, "candidatas.py")), "correr.py": sha(os.path.join(AQUI, "correr.py")),
                      "arnes/evaluar.py": sha(os.path.join(RAIZ, "arnes", "evaluar.py")),
                      "arnes/juez_operador_v1.py": sha(os.path.join(RAIZ, "arnes", "juez_operador_v1.py")),
                      "PREREGISTRO.md": sha(os.path.join(RAIZ, "PREREGISTRO.md"))},
    "sha256_resultados_entrenamiento": {c: sha(p) for c, p in res.items()},
    "seleccion_entrenamiento": {w: {"finalistas": v["finalistas"], "tabla": v["tabla"]} for w, v in selec.items()},
    "nota_sello": "El arnes pide un finalistas.json existente. El sello GLOBAL (arnes/finalistas.json, sec. 8.4) junta a los 4 grupos y no lo "
                  "escribe este probador; este archivo es el sello del grupo 4 y es el que se pasa como sello= al abrir PRUEBA para los controles.",
}
p = os.path.join(AQUI, "sello_grupo4.json")
if os.path.exists(p):
    raise SystemExit("el sello ya existe: no se pisa")
with open(p, "w", encoding="utf-8") as f:
    json.dump(s, f, ensure_ascii=False, indent=1)
print(p, sha(p), s["hora_utc"], s["hora_art"])
