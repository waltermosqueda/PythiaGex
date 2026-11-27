# -*- coding: utf-8 -*-
"""Busca en la cinta de la sonda la orden n en la que la Maquina de referencia (test_ola) da EXACTAMENTE los numeros que se ven en la
captura de las 11:54:05 (PrintWindow): marea dir -1 raya 7272,75 faltan 32,50 delta -3.509; ola dir -1 raya 7247,50 faltan 7,25
delta -761. Ademas: delta acumulado de la jornada (desde las 23:00 UTC) en ese n, para el "total" del Superindicador, y el CLIMA del
minuto 23 (9:53 NY, cierra 14:54:00 UTC) recalculado con codigo propio (sin la grilla por segundo: extremos de todas las ordenes).
Uso: python -I instante_de_la_foto.py <PythiaGex> <cinta.csv>"""
import sys, os, csv, json
sys.stdout.reconfigure(encoding="utf-8")
sys.path.insert(0, os.path.join(sys.argv[1], "herramientas"))
import test_ola as T
cinta = sys.argv[2]
filas = []
with open(cinta, newline="") as fh:
    r = csv.reader(fh); cab = next(r)
    for x in r: filas.append((x[0], float(x[1]), float(x[2]), float(x[3]), int(x[4])))
print("cinta", os.path.basename(cinta), "ordenes", len(filas), "columnas", cab)
FOTO = {"marea": (-1, 7272.75, 32.50, -3509), "ola": (-1, 7247.50, 7.25, -761)}
JSONL = {"marea": (-1, 7272.75, 32.75, -3513), "ola": (-1, 7247.50, 7.50, -765)}
m = {"ola": T.Maquina(10.25), "marea": T.Maquina(41.0)}
cum = 0; ult = None; hallados = []; jl = []
for i, (t, a, b, v, lado) in enumerate(filas, 1):
    cum += v * lado; ult = b
    for k in m: m[k].paso(t, min(a, b), max(a, b), cum)
    if 143400 <= i <= 144400:
        est = {}
        for k, q in m.items():
            o = q.pivotes[-1]
            est[k] = (q.dir, q.ext - q.dir * q.X, max(0.0, q.X - abs(q.ext - ult)), cum - o[4])
        if all(est[k] == FOTO[k] for k in FOTO): hallados.append((i, t, ult, v, lado, cum))
        if all(est[k] == JSONL[k] for k in JSONL): jl.append((i, t, ult, cum))
print("estado del JSONL (14:54:05.366) en n:", [(i, t[11:23], u, c) for i, t, u, c in jl][:6])
print("estado de la FOTO en n:", [(i, t[11:23], u, ("compra" if lado > 0 else "venta") + " %d" % v, c) for i, t, u, v, lado, c in hallados][:10])

# ---- clima del minuto 23 con codigo propio
F23 = 0.39186086593632   # f(23) de la curva de la R21 (OlaClima.cs, CurvaRango.F[23])
med = 126.625
rth = [(t, a, b, v) for (t, a, b, v, lado) in filas if "2026-11-27T14:30:00" <= t < "2026-11-27T14:54:00"]
previa = [x for x in filas if x[0] < "2026-11-27T14:30:00"][-1]
h = max(max(a, b) for _, a, b, _ in rth); l = min(min(a, b) for _, a, b, _ in rth)
primera_seg = rth[0][0][:19]
if primera_seg != "2026-11-27T14:30:00":   # regla R19: sin orden en el segundo 9:30:00, los extremos arrancan con el ultimo de la noche
    h = max(h, previa[2]); l = min(l, previa[2])
o = rth[0][2]; c = rth[-1][2]
vw = sum((a + b) / 2 * v for _, a, b, v in rth) / sum(v for *_, v in rth)
rrn = (h - l) / (med * F23)
lv = 1 if c > vw else (-1 if c < vw else 0); la = 1 if c > o else (-1 if c < o else 0)
tipo = "TENDENCIA" if rrn >= 1.6 and lv == la and lv != 0 else ("DUDOSO" if rrn >= 1.6 else ("ROTACIÓN" if rrn > 1.0 else "ESTRECHO"))
print("primera orden RTH %s (ultimo %.2f); ultima de la noche %s %.2f" % (rth[0][0], o, previa[0], previa[2]))
print("CLIMA m=23 (hasta 14:54:00 UTC): H %.2f L %.2f rango %.2f | apertura %.2f | cierre %.2f | VWAP %.6f | rrn %.6f -> %s, '%s' | lado VWAP %d, lado apertura %d"
      % (h, l, h - l, o, c, vw, rrn, tipo, ("%.1f" % rrn).replace(".", ",") + "×", lv, la))
