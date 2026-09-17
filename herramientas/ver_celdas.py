# -*- coding: utf-8 -*-
# Uso: python herramientas/ver_celdas.py captura.png x0 x1 yPrecio0 yPrecio1 "y1,y2,y3" "confluencia,presion,grandes"
# (las y de cada cinta se sacan contando columnas con color por fila; ver FlujoClaro.md 1.4)
# Control VISUAL automatico: por cada columna de pixeles de la captura, de que color es la vela y de que color es la celda de cada cinta.
import sys, collections
from PIL import Image
im = Image.open(sys.argv[1]).convert("RGB"); W, H = im.size; px = im.load()
x0, x1 = int(sys.argv[2]), int(sys.argv[3]); yp0, yp1 = int(sys.argv[4]), int(sys.argv[5]); filas = [int(v) for v in sys.argv[6].split(",")]
def clase(c):
    r, g, b = c; mx, mn = max(c), min(c)
    if mx < 45: return "."                       # fondo
    if mx - mn < 22: return "g"                  # gris
    if b >= g and r > g + 12 and b > 90: return "V" # violeta
    if r > g + 25 and r > b + 25: return "R"
    if g > r + 25 and b > r + 10: return "T"     # verde azulado (compra)
    return "?"
# colores de vela: los dos colores saturados mas frecuentes del area de precio
cnt = collections.Counter()
for x in range(x0, x1):
    for y in range(yp0, yp1):
        c = px[x, y]
        if max(c) - min(c) > 90: cnt[c] += 1
print("colores saturados mas frecuentes en el precio:", cnt.most_common(6))
verde = next(c for c, _ in cnt.most_common(12) if c[1] > c[0] + 40); rojo = next(c for c, _ in cnt.most_common(12) if c[0] > c[1] + 40)
print("vela verde", verde, "vela roja", rojo)
cols = []
for x in range(x0, x1):
    nv = sum(1 for y in range(yp0, yp1) if px[x, y] == verde); nr = sum(1 for y in range(yp0, yp1) if px[x, y] == rojo)
    vela = "T" if nv >= 3 and nv > 2 * nr else "R" if nr >= 3 and nr > 2 * nv else " "
    cols.append((x, vela, [clase(px[x, y]) for y in filas], nv, nr))
# agrupar columnas en velas: tramos contiguos con vela != ' '
velas = []; ini = None
for i, (x, v, cs, nv, nr) in enumerate(cols + [(0, " ", [], 0, 0)]):
    if v != " " and ini is None: ini = i
    elif (v == " " or (ini is not None and v != cols[ini][1])) and ini is not None:
        tramo = cols[ini:i]
        if len(tramo) >= 3:   # el cuerpo (la mecha es de 1 px)
            m = tramo[len(tramo) // 2]; velas.append((m[0], tramo[0][1], m[2], max(t[3] + t[4] for t in tramo)))
        ini = i if v != " " else None
print("velas con cuerpo detectadas:", len(velas))
nombres = sys.argv[7].split(","); tot = collections.Counter()
for j, nombre in enumerate(nombres):
    c = collections.Counter()
    for x, v, cs, alto in velas:
        k = cs[j]
        c["coincide" if k == v else "violeta" if k == "V" else "gris" if k == "g" else "negro" if k == "." else "OPUESTO" if k in "TR" else "otro"] += 1
    print("%-12s %s" % (nombre, dict(c)))
print("detalle (x, vela, celdas):", " ".join("%d%s:%s" % (x, v, "".join(cs)) for x, v, cs, a in velas[-40:]))
