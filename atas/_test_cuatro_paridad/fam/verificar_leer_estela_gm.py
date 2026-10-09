# -*- coding: utf-8 -*-
"""verificar_leer_estela_gm.py — 08-10-2026 (B-tres, PythiaGex 4.1.2). El lector Python de la estela (laboratorio/tres/extremos_rebote.py
leer_estela, IMPORTADO, no copiado) tiene que ignorar el campo nuevo "gm" (con numeros y con null): mismas filas con y sin "gm".
Usa las estelas reales de datos/gm (gen_datos_gm.py) y una carpeta temporal en resultados/; no lee ni escribe %APPDATA%.
Uso: python -I verificar_leer_estela_gm.py   (codigo 0 = verde)"""
import os, sys, json, shutil

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", "..", ".."))
sys.path.insert(0, os.path.join(RAIZ, "laboratorio", "tres"))
import extremos_rebote as E                                           # noqa: E402

DIA = "2026-10-09"
TMP = os.path.join(AQUI, "resultados", "gm_py_tmp")
fallas = 0
for capa in ("NDX", "QQQ"):
    src = os.path.join(AQUI, "datos", "gm", "estela-%s-%s.jsonl" % (capa, DIA))
    lineas = [l.rstrip("\n") for l in open(src, encoding="utf-8") if l.strip()]
    for nombre, sufijo in (("sin", None), ("con", '"gm":[-141.9,null]'), ("con2", '"gm":[null,412.7]')):
        d = os.path.join(TMP, nombre)
        os.makedirs(d, exist_ok=True)
        with open(os.path.join(d, "estela-%s-%s.jsonl" % (capa, DIA)), "w", encoding="utf-8", newline="\n") as f:
            for l in lineas:
                if sufijo is not None:
                    j = json.loads(l)
                    j.pop("gm", None)   # por si la foto ya trae gm: se reescribe sin y con
                    l = json.dumps(j, separators=(",", ":"))[:-1] + "," + sufijo + "}"
                else:
                    j = json.loads(l); j.pop("gm", None); l = json.dumps(j, separators=(",", ":"))
                f.write(l + "\n")
        E.EST[nombre] = d
    a = E.leer_estela("sin", capa, DIA)
    for nombre in ("con", "con2"):
        b = E.leer_estela(nombre, capa, DIA)
        igual = a is not None and b is not None and a.shape == b.shape and a.reset_index(drop=True).equals(b.reset_index(drop=True))
        print("%s leer_estela sin gm vs %s: filas %s / %s -> %s" % (capa, nombre, None if a is None else len(a), None if b is None else len(b), "IGUALES" if igual else "DISTINTAS"))
        if not igual:
            fallas += 1
shutil.rmtree(TMP, ignore_errors=True)
print("RESULTADO:", "VERDE" if fallas == 0 else "ROJO (%d)" % fallas)
sys.exit(0 if fallas == 0 else 1)
