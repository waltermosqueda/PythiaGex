# -*- coding: utf-8 -*-
"""Superpone a la captura sincronizada (11:54:05) los pivotes de la ola (X 10,25) y de la marea (X 41) recalculados desde la cinta
con la Maquina de referencia hasta la orden de la foto (n = 143573). Mapeo de pixeles SIN usar nada del indicador salvo las dos rayas:
y desde las dos rayas del jsonl (precio -> pixel de la region, +187 de la ventana), x desde las etiquetas del eje de tiempo de ATAS
(04:00 en x 82, 10:00 en x 584: 1,3944 px por minuto). Uso: python -I superponer_pivotes.py <PythiaGex> <cinta> <captura.png> <salida.png>"""
import sys, os, csv
sys.stdout.reconfigure(encoding="utf-8")
sys.path.insert(0, os.path.join(sys.argv[1], "herramientas"))
import test_ola as T
from datetime import datetime, timedelta
from PIL import Image, ImageDraw
N = 143573
filas = []
with open(sys.argv[2], newline="") as fh:
    r = csv.reader(fh); next(r)
    for i, x in enumerate(r, 1):
        if i > N: break
        filas.append((x[0], float(x[1]), float(x[2]), float(x[3]), int(x[4])))
m = {"ola": T.Maquina(10.25), "marea": T.Maquina(41.0)}
cum = 0
for (t, a, b, v, lado) in filas:
    cum += v * lado
    for k in m: m[k].paso(t, min(a, b), max(a, b), cum)
def ar(t): return datetime.strptime(t[:19], "%Y-%m-%dT%H:%M:%S") - timedelta(hours=3)
x0 = 584.0; px_min = (584 - 82) / 360.0; diez = datetime(2026, 11, 27, 10, 0)
def X(t):  # centro de la vela de 1 min que contiene t
    d = ar(t); vela = d.replace(second=0)
    return x0 + ((vela - diez).total_seconds() / 60 + 0.5) * px_min
def Y(p): return 187 + 468 + (7247.5 - p) * (468 - 348) / (7272.75 - 7247.5)
im = Image.open(sys.argv[3]).convert("RGB"); g = ImageDraw.Draw(im)
desde = "2026-11-27T06:19:00"
for k, col, rad in (("marea", (190, 120, 255), 9), ("ola", (255, 255, 255), 5)):
    q = m[k]
    piv = [p for p in q.pivotes if p[0] >= desde] + [(q.ext_t, q.ext, "extremo en curso", None, q.ext_cum)]
    print("%s: %d pivotes visibles + extremo en curso" % (k, len(piv) - 1))
    for p in piv:
        x, y = X(p[0]), Y(p[1])
        g.ellipse((x - rad, y - rad, x + rad, y + rad), outline=col, width=2)
        print("   %s AR %s  %-8.2f %-16s -> pixel (%.0f, %.0f)" % (k, ar(p[0]).strftime("%H:%M:%S"), p[1], p[2], x, y))
im.crop((0, 160, 1015, 800)).save(sys.argv[4]); print("guardado", sys.argv[4])
