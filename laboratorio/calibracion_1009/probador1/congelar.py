# -*- coding: utf-8 -*-
"""
probador1/congelar.py — CONGELAMIENTO antes de abrir PRUEBA (PREREGISTRO sec. 8.4), grupo 1.
Escribe probador1/finalistas.json (el SELLO que se le pasa al arnes), probador1/finalistas.sha256 y probador1/CONGELADO.md.
No hay parametros libres que elegir (PREREGISTRO sec. 6 (3)): se congelan el codigo (sha256), las interpretaciones declaradas,
la lista de lo que va a PRUEBA y el sha256 de cada resultado de ENTRENAMIENTO.
Nota: el PREREGISTRO nombra arnes/finalistas.json; con cuatro probadores en paralelo cada uno escribe el suyo en su subcarpeta
(lo pide la tarea) para no pisarse. El arnes acepta cualquier ruta de sello y anota el sha256 en arnes/aperturas_prueba.log.
"""
import datetime as dt
import glob
import hashlib
import json
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
sys.path.insert(0, AQUI)
import candidatas as C                      # noqa: E402

E = C.E
RES = os.path.join(AQUI, "resultados", "entrenamiento")
SELLO = os.path.join(AQUI, "finalistas.json")


def sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def main():
    if os.path.exists(SELLO):
        raise SystemExit("ya existe %s: el congelamiento se hace UNA vez (no se pisa)" % SELLO)
    sel = json.load(open(os.path.join(RES, "seleccion.json"), encoding="utf-8"))
    fin = {w: sel["ventanas"][w]["seleccion"]["finalistas"] for w in E.VENTANAS}
    refs = ["C01_DOS_QQQ", "C02_DOS_NDX", "C03_DOS_QQQ_C41"]
    nuevos = sorted({x for w in fin for x in fin[w]} - set(refs))
    a_prueba = refs + [x for x in nuevos]
    ahora = dt.datetime.now(dt.timezone.utc)
    codigo = {os.path.basename(p): sha(p) for p in sorted(glob.glob(os.path.join(AQUI, "*.py")))}
    doc = {
        "grupo": 1, "probador": "probador1",
        "escrito_utc": ahora.isoformat(timespec="seconds"),
        "escrito_art": (ahora - dt.timedelta(hours=3)).strftime("%Y-%m-%dT%H:%M:%S") + " ART",
        "finalistas_por_ventana_grupo1": fin,
        "referencias_siempre_a_prueba": refs,
        "a_prueba": a_prueba,
        "no_van_a_prueba": {"C05_STRIKE_AZAR_QQQ": "control (sec. 5 y 8.1): no puede ser finalista ni esta en la lista de la sec. 8.3",
                            "C01s_DOS_QQQ_doslog": "sensibilidad informativa fuera de la familia (sec. 7, C01)"},
        "candidatas_no_elegibles": {k: v for k, v in
                                    {r["candidata"]: {"elegible_noche": None, "elegible_dia": None} for r in sel["ventanas"]["noche"]["seleccion"]["tabla"]}.items()},
        "familia_holm_local": [x + "|" + w for x in a_prueba for w in E.VENTANAS],
        "aviso_holm": "la familia de Holm del PREREGISTRO (sec. 9) es la de los 4 grupos (finalistas + C01-C03 + C07-C12) x 2 ventanas; "
                      "aca se informa Holm local (grupo 1) y una cota conservadora Bonferroni con m = (9 referencias + finalistas del grupo 1) x 2",
        "parametros_libres": "ninguno (PREREGISTRO sec. 6 (3)); todo fijado en la sec. 7",
        "interpretaciones": C.INTERPRETACIONES,
        "n_azar_prueba": 2000, "n_boot_prueba": 2000,
        "dias_prueba": list(E.PRUEBA),
        "sha256_resultados_entrenamiento": {os.path.basename(p): sha(p) for p in sorted(glob.glob(os.path.join(RES, "*.json")))},
        "sha256_codigo_probador1": codigo,
        "sha256_preregistro": sha(os.path.join(RAIZ, "PREREGISTRO.md")),
        "sha256_arnes": sha(os.path.join(RAIZ, "arnes", "evaluar.py")),
        "sha256_juez": sha(os.path.join(RAIZ, "arnes", "juez_operador_v1.py")),
    }
    for w in E.VENTANAS:
        for r in sel["ventanas"][w]["seleccion"]["tabla"]:
            doc["candidatas_no_elegibles"][r["candidata"]]["elegible_" + w] = r["elegible"]
    with open(SELLO, "w", encoding="utf-8") as f:
        json.dump(doc, f, ensure_ascii=False, indent=1)
    h = sha(SELLO)
    with open(os.path.join(AQUI, "finalistas.sha256"), "w", encoding="utf-8") as f:
        f.write("%s  finalistas.json\n" % h)
        for k, v in codigo.items():
            f.write("%s  %s\n" % (v, k))
    L = ["# CONGELADO — probador 1 (grupo 1)", "",
         "Escrito %s UTC (%s), ANTES de construir niveles de PRUEBA y antes de llamar al arnes con abrir_prueba=True." % (
             doc["escrito_utc"], doc["escrito_art"]), "",
         "- sello: `probador1/finalistas.json`, sha256 `%s`" % h,
         "- parametros libres: ninguno (PREREGISTRO sec. 6 (3)). Lo que se congela es el codigo y las interpretaciones de abajo.",
         "- finalistas del grupo 1 por ventana: noche %s; dia %s" % (fin["noche"] or "ninguna", fin["dia"] or "ninguna"),
         "- van a PRUEBA: %s (n_azar = 2000, n_boot = 2000)" % ", ".join(a_prueba),
         "- no van: C05 (control), C01s dos_log (sensibilidad informativa)", "",
         "## Interpretaciones declaradas (las mas conservadoras que encontre)", ""]
    for k, v in C.INTERPRETACIONES.items():
        L.append("- **%s**: %s" % (k, v))
    L += ["", "## sha256 del codigo", ""] + ["- `%s` %s" % (k, v) for k, v in codigo.items()]
    L += ["", "## sha256 de los resultados de ENTRENAMIENTO", ""] + ["- `%s` %s" % (k, v) for k, v in doc["sha256_resultados_entrenamiento"].items()]
    with open(os.path.join(AQUI, "CONGELADO.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(L) + "\n")
    print("sello", SELLO, h)
    print(json.dumps({"finalistas": fin, "a_prueba": a_prueba}, ensure_ascii=False))


if __name__ == "__main__":
    main()
