# -*- coding: utf-8 -*-
"""g3_congelar.py — escribe finalistas_grupo3.json: el registro CONGELADO de la seleccion del grupo 3 (PREREGISTRO sec. 8), con hora
UTC y sha256 de todo lo que la produjo, ANTES de cualquier apertura de PRUEBA."""
import os
import json
import glob
import hashlib
import datetime as dt

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
CONTROLES = ("C05", "C19", "C20", "C21", "C22", "C23", "C24")


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest()


def main():
    sel = json.load(open(os.path.join(AQUI, "seleccion_ENTRENAMIENTO.json"), encoding="utf-8"))
    tab = json.load(open(os.path.join(AQUI, "tabla_ENTRENAMIENTO.json"), encoding="utf-8"))
    mot = {}
    for c, v in tab.items():
        mot[c] = {}
        for w in ("noche", "dia"):
            x = v[w]
            niveles = x["strikes_distintos"] or x["niveles_distintos_5pts"]
            fallas = []
            if x["base"] < 30:
                fallas.append("base %d < 30" % x["base"])
            if niveles < 10:
                fallas.append("niveles distintos %d < 10" % niveles)
            va = x["ventaja_vs_azar"]
            if va is None or va < 5:
                fallas.append("ventaja vs azar %s < +5 pp" % (None if va is None else round(va, 1)))
            p = x["p_azar"]
            if p is None or p >= 0.05:
                fallas.append("p_azar %s >= 0,05" % (None if p is None else round(p, 3)))
            lo = x["ic90_inf_vs_corridos"]
            if lo is None or lo <= 0:
                fallas.append("IC90 inf vs corridos %s <= 0" % (None if lo is None else round(lo, 1)))
            if c[:3] in CONTROLES:
                fallas.append("control")
            mot[c][w] = {"elegible": not fallas, "fallas": fallas}
    # coherencia con el arnes
    for w in ("noche", "dia"):
        mias = sorted(c for c in mot if mot[c][w]["elegible"])
        arnes = sorted(f["candidata"] for f in sel["por_ventana"][w]["tabla"] if f["elegible"])
        assert mias == arnes, (w, mias, arnes)
    rel = lambda p: os.path.relpath(p, RAIZ).replace(os.sep, "/")
    lista = sorted(glob.glob(os.path.join(AQUI, "g3_*.py")) + glob.glob(os.path.join(AQUI, "resultados_ENTRENAMIENTO", "*.json"))
                   + [os.path.join(AQUI, n) for n in ("seleccion_ENTRENAMIENTO.json", "tabla_ENTRENAMIENTO.json", "diag_niveles_ENTRENAMIENTO.json",
                                                      "verificacion_g3.json")]
                   + glob.glob(os.path.join(RAIZ, "probadores", "grupo3", "*", "niveles_ENTRENAMIENTO.parquet")))
    rec = {"que_es": "Registro CONGELADO del probador 3 (grupo 3: C13-C18) antes de cualquier apertura de PRUEBA (PREREGISTRO sec. 8).",
           "generado_utc": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds"),
           "parametros": "ninguno libre: todo fijado en PREREGISTRO sec. 7 (lineas 234-251); no se eligio nada con datos. Se congela la "
                         "implementacion (sha256 abajo) y los resultados de ENTRENAMIENTO.",
           "finalistas": {"noche": "ninguna", "dia": "ninguna"},
           "motivos_por_candidata": mot,
           "mitades": {w: {f["candidata"]: f["mitades"] for f in sel["por_ventana"][w]["tabla"]} for w in ("noche", "dia")},
           "loo_elegir_la_mejor": {w: sel["por_ventana"][w]["loo_elegir_la_mejor"] for w in ("noche", "dia")},
           "decision_prueba": "NO se abre PRUEBA para el grupo 3: ninguna candidata del grupo es elegible en ninguna ventana (sec. 8: si "
                              "ninguna es elegible en una ventana, se escribe 'ninguna' y en esa ventana PRUEBA solo mide las referencias). "
                              "La elegibilidad es absoluta por candidata: la seleccion global (hasta 3 por ventana sobre las 24) no puede "
                              "incluir a ninguna del grupo 3. No se escribe arnes/finalistas.json (lo escribe quien junte los cuatro grupos).",
           "hashes": dict({"PREREGISTRO.md": sha(os.path.join(RAIZ, "PREREGISTRO.md")), "arnes/evaluar.py": sha(os.path.join(RAIZ, "arnes", "evaluar.py")),
                           "arnes/juez_operador_v1.py": sha(os.path.join(RAIZ, "arnes", "juez_operador_v1.py"))},
                          **{rel(p): sha(p) for p in lista})}
    ruta = os.path.join(AQUI, "finalistas_grupo3.json")
    with open(ruta, "w", encoding="utf-8") as fh:
        json.dump(rec, fh, ensure_ascii=False, indent=1)
    print(rec["generado_utc"], "sha256", sha(ruta))
    print(json.dumps(mot, ensure_ascii=False))


if __name__ == "__main__":
    main()
