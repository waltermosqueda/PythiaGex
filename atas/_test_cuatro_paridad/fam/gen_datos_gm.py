# -*- coding: utf-8 -*-
"""gen_datos_gm.py — 08-10-2026 (B-tres, PythiaGex 4.1.2). Foto FIJA de datos reales para la prueba 'gm' del arnes fam: el monto con signo de
las rayas 3.0 de las capas NDX/QQQ ("gm" de la estela) recalculado con el nucleo de la 3.0 sobre las MISMAS cadenas de CBOE que bajo la 4.1 y
comparado con MAJORS_*_vol de la familia del mismo minuto y strike.

Lee SOLO LECTURA de %APPDATA%\\ATAS\\PythiaGex4 (lo que escribio la 4.1 en vivo la noche del 08 al 09-10) y copia a datos/gm/ solo lo
necesario, hasta un corte fijo (asi la prueba no cambia mientras el indicador sigue escribiendo):
  estela-NDX/QQQ-<dia>.jsonl   las lineas de las capas (t <= corte)
  cadena-NQ/QQQ-<dia>.jsonl.gz las cadenas de CBOE (NQ = el _NDX, como lo archiva el descargador) con generado en [desde, corte]
  niv-<dia>-MNQZ6.jsonl        los minutos de la familia (k <= corte)
Uso: python -I gen_datos_gm.py [dia=2026-10-09] [desde=2026-10-09T01:00:00Z] [corte=2026-10-09T02:40:00Z]"""
import gzip, json, os, sys, datetime as dt

AQUI = os.path.dirname(os.path.abspath(__file__))
APP = os.path.join(os.environ["APPDATA"], "ATAS", "PythiaGex4")
DIA = sys.argv[1] if len(sys.argv) > 1 else "2026-10-09"
DESDE = sys.argv[2] if len(sys.argv) > 2 else "2026-10-09T01:00:00Z"
CORTE = sys.argv[3] if len(sys.argv) > 3 else "2026-10-09T02:40:00Z"
SAL = os.path.join(AQUI, "datos", "gm")


def utc(s):
    s = s.replace("Z", "+00:00")
    return dt.datetime.fromisoformat(s).astimezone(dt.timezone.utc)


desde, corte = utc(DESDE), utc(CORTE)
os.makedirs(SAL, exist_ok=True)
n = {}
for capa in ("NDX", "QQQ"):
    ls = [l for l in open(os.path.join(APP, "estela", "estela-%s-%s.jsonl" % (capa, DIA)), encoding="utf-8")
          if l.strip() and utc(json.loads(l)["t"]) <= corte]
    with open(os.path.join(SAL, "estela-%s-%s.jsonl" % (capa, DIA)), "w", encoding="utf-8", newline="\n") as f:
        f.writelines(ls)
    n["estela-" + capa] = len(ls)
for arch in ("NQ", "QQQ"):
    with gzip.open(os.path.join(APP, "cboe", "cadena-%s-%s.jsonl.gz" % (arch, DIA)), "rt", encoding="utf-8") as h:
        ls = [l for l in h if l.strip() and desde <= utc(json.loads(l)["generado"]) <= corte]
    with gzip.open(os.path.join(SAL, "cadena-%s-%s.jsonl.gz" % (arch, DIA)), "wt", encoding="utf-8", newline="\n") as f:
        f.writelines(ls)
    n["cadena-" + arch] = len(ls)
kc = int(corte.timestamp()) // 60
ls = []
for l in open(os.path.join(APP, "familia", "niv-%s-MNQZ6.jsonl" % DIA), encoding="utf-8"):
    if not l.strip():
        continue
    j = json.loads(l)
    if "k" not in j or j["k"] <= kc:
        ls.append(l)
with open(os.path.join(SAL, "niv-%s-MNQZ6.jsonl" % DIA), "w", encoding="utf-8", newline="\n") as f:
    f.writelines(ls)
n["niv"] = len(ls)
print("datos/gm:", json.dumps(n), "corte", CORTE)
