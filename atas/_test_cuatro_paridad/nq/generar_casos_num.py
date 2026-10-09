# -*- coding: utf-8 -*-
"""generar_casos_num.py — casos de prueba de NumFam (B3a, 08-10-2026): round(x, n) de Python y np.sum (suma por pares) de numpy.
Escribe casos_num.txt al lado; el arnes ParidadNq --autoprueba los verifica al bit. Prioridad BELOW_NORMAL. Uso: python -I generar_casos_num.py"""
import ctypes, os, random
ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
import numpy as np
AQUI = os.path.dirname(os.path.abspath(__file__))
rnd = random.Random(20261008)
L = []
# round: decimales de 5 cifras a 4 (dias de viva3), precios a 2, montos a 1, y empates exactos binarios (0.03125, 2.5e-05 ...)
for _ in range(3000):
    x = float("%.5f" % rnd.uniform(0, 15)); L.append("R %r 4 %r" % (x, round(x, 4)))
for _ in range(3000):
    x = rnd.uniform(-5e4, 5e4); n = rnd.choice((1, 2, 4)); L.append("R %r %d %r" % (x, n, round(x, n)))
for x in (0.03125, 0.00005, 2.675, 1.005, 0.125, 0.375, -0.03125, 31440.125, 31440.375, 1e-300, 123456789.98765, 0.1 + 0.2):
    for n in (1, 2, 3, 4):
        L.append("R %r %d %r" % (x, n, round(x, n)))
# np.sum por pares
rs = np.random.default_rng(7)
for _ in range(300):
    n = int(rs.integers(1, 700))
    a = rs.standard_normal(n) * 10.0 ** rs.integers(-5, 9, n)
    L.append("S %r %s" % (float(a.sum()), " ".join(repr(float(v)) for v in a)))
# np.median y robusta (backtest_familia.robusta)
def robusta(x, piso):
    x = np.asarray([v for v in x if v == v], float)
    if len(x) == 0: return float("nan"), 0
    med = float(np.median(x)); mad = float(np.median(np.abs(x - med)))
    b = x[np.abs(x - med) <= max(3.0 * mad, piso)]
    return (float(np.median(b)), len(b)) if len(b) else (med, len(x))
for _ in range(300):
    n = int(rs.integers(1, 30)); a = list(rs.normal(240, 3, n)); 
    if rs.random() < 0.3: a[0] = 300.0
    piso = float(rs.choice([0.5, 1.0, 0.0002 * 41.4]))
    v, k = robusta(a, piso)
    L.append("M %r %r %r %d %s" % (float(np.median(a)), piso, v, k, " ".join(repr(float(x)) for x in a)))
open(os.path.join(AQUI, "casos_num.txt"), "w", encoding="utf-8").write("\n".join(L) + "\n")
print(len(L), "casos")
