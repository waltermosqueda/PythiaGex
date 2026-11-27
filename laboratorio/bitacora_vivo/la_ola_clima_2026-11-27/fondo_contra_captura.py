# -*- coding: utf-8 -*-
"""El fondo de marea de la captura (11:54:05) contra las fotos por vela del csv (auditadas contra la cinta: 0 diferencias).
Codigo del fondo (PythiaOlaDibujo.cs): sube con agresores = verde #00D47E, baja con agresores = rojo #F23645, sin agresores = ambar
#FFB300. 1) del csv: el estado de la marea en cada vela y sus cambios (hora AR); 2) de la captura: el color de fondo columna por
columna en una fila sin velas, y sus cambios; 3) los cambios del csv pasados a pixel con el eje de tiempo (04:00 x 82, 10:00 x 584).
Uso: python -I fondo_contra_captura.py <ola.csv> <captura.png>"""
import sys, csv
sys.stdout.reconfigure(encoding="utf-8")
from datetime import datetime, timedelta
from PIL import Image
cab = None; marea = {}
for x in csv.reader(open(sys.argv[1], encoding="utf-8")):
    if x[0] == "#F": cab = x[1:]; continue
    if x[0] != "F": continue
    d = dict(zip(cab, x[1:]))
    if d["esc"] != "4X": continue
    cod = "sin agresores" if d["con_agresores"] in ("0", "false", "False") else ("sube" if d["dir"] == "1" else "baja")
    marea[int(d["bar"])] = (d["corte"], cod, d["dir"], d["con_agresores"], d["delta_pierna"])
bars = sorted(marea)
def ar(t): return datetime.strptime(t[:19], "%Y-%m-%dT%H:%M:%S") - timedelta(hours=3)
px_min = (584 - 82) / 360.0; diez = datetime(2026, 11, 27, 10, 0)
cambios = []; prev = None
for b in bars:
    corte, cod, *_ = marea[b]
    if cod != prev: cambios.append((b, corte, cod)); prev = cod
print("csv: %d velas con foto de marea; cambios de estado del fondo:" % len(bars))
for b, corte, cod in cambios:
    ini = ar(corte) - timedelta(minutes=1)   # la vela que cierra en 'corte' empieza un minuto antes
    x = 584 + ((ini - diez).total_seconds() / 60) * px_min
    print("   vela %d (de %s a %s AR): %-14s -> empieza en x %.0f | delta pierna %s" % (b, ini.strftime("%H:%M"), ar(corte).strftime("%H:%M"), cod, x, marea[b][4]))
im = Image.open(sys.argv[2]).convert("RGB")
def clase(c):
    r, g, b = c
    if g > r + 12 and g > b: return "verde"
    if r > g + 12 and b > g + 5: return "rojo"
    if r > b + 15 and g > b + 10 and abs(r - g) < 40: return "ambar"
    return "?"
for y in (600, 620):
    fila = [clase(im.getpixel((x, y))) for x in range(30, 760)]
    tramos = []; ant = None
    for i, c in enumerate(fila):
        if c != ant: tramos.append([30 + i, c]); ant = c
    tramos = [t for t in tramos]
    print("captura fila y=%d: %s" % (y, " | ".join("x%d %s" % (x, c) for x, c in tramos if c != "?")[:600]))
for x in (100, 300, 450, 520, 560, 620, 700, 735):
    print("   pixel (%d, 600) = %s -> %s" % (x, im.getpixel((x, 600)), clase(im.getpixel((x, 600)))))
