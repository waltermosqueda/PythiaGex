# -*- coding: utf-8 -*-
"""Divergencia ES / MES de La Ola en vivo (27-11-2026). Todo desde las DOS cintas de la sonda, cortadas a la MISMA hora.
1) pivotes de la ola y la marea de cada uno con su X (ES 10,25 / MES 10,00) y el MES con la X del ES (10,25): separa lo que es
   'parametro' (la X redondeada distinto) de lo que es 'mercado' (dos libros distintos);
2) la pierna en curso de la marea: delta de cada uno, por tamano de la orden agresora (MES tambien en equivalente ES = /10);
3) minuto a minuto dentro de la pierna: delta ES contra delta MES/10, correlacion;
4) la jornada RTH: en cuantos minutos el color de la marea (y de la ola) coincide entre ES y MES (fotos de cierre de minuto).
Uso: python -I divergencia_es_mes.py <PythiaGex> <cinta ES> <cinta MES> <hasta UTC 'AAAA-MM-DDTHH:MM:SS'>"""
import sys, os, csv, math
sys.stdout.reconfigure(encoding="utf-8")
sys.path.insert(0, os.path.join(sys.argv[1], "herramientas"))
import test_ola as T
from datetime import datetime, timedelta
HASTA = sys.argv[4]
RTH0 = "2026-11-27T14:30:00"

def cargar(ruta):
    f = []
    with open(ruta, newline="") as fh:
        r = csv.reader(fh); next(r)
        for x in r:
            if x[0] >= HASTA: break
            f.append((x[0], float(x[1]), float(x[2]), int(float(x[3])), int(x[4])))
    return f

def ar(t): return (datetime.strptime(t[:19], "%Y-%m-%dT%H:%M:%S") - timedelta(hours=3)).strftime("%H:%M:%S")

def correr(filas, X):
    """corre ola y marea; devuelve las maquinas y, al cierre de cada minuto RTH, (dir, con) de cada escala"""
    m = {"ola": T.Maquina(X), "marea": T.Maquina(4 * X)}
    cum = 0; fotos = {}; minuto = None
    for (t, a, b, v, lado) in filas:
        mm = t[:16]
        if minuto is not None and mm != minuto and minuto >= RTH0[:16]:
            fotos[minuto] = {k: (q.dir, q.dir != 0 and (1 if cum_prev - q.pivotes[-1][4] > 0 else -1 if cum_prev - q.pivotes[-1][4] < 0 else 0) == q.dir)
                             for k, q in m.items() if q.pivotes}
        minuto = mm
        cum += v * lado; cum_prev = cum
        for k in m: m[k].paso(t, min(a, b), max(a, b), cum)
    return m, cum, fotos

es = cargar(sys.argv[2]); mes = cargar(sys.argv[3])
print("cintas cortadas a %s UTC (%s AR): ES %d ordenes, MES %d ordenes" % (HASTA, ar(HASTA), len(es), len(mes)))
print("ultimo precio: ES %.2f (%s AR) | MES %.2f (%s AR)" % (es[-1][2], ar(es[-1][0]), mes[-1][2], ar(mes[-1][0])))
E, cumE, fotE = correr(es, 10.25)
M, cumM, fotM = correr(mes, 10.00)
M2, _, fotM2 = correr(mes, 10.25)

print("\n=== 1) PIVOTES desde las 12:00 AR (hora AR, precio, tipo) ===")
desde = "2026-11-27T15:00:00"
for esc in ("marea", "ola"):
    for nom, q in (("ES X10,25", E[esc]), ("MES X10,00", M[esc]), ("MES con X10,25", M2[esc])):
        piv = [p for p in q.pivotes if p[0] >= desde]
        print("  %-5s %-15s %s | en curso: dir %+d extremo %.2f (%s)" % (esc, nom, "  ".join("%s %.2f%s" % (ar(p[0])[:5], p[1], "^" if p[2] == "max" else "v") for p in piv),
              q.dir, q.ext, ar(q.ext_t)))

print("\n=== 2) LA PIERNA EN CURSO DE LA MAREA ===")
def tamano(v, mes):
    lim = (1, 4, 9, 19, 49) if not mes else (1, 4, 9, 49, 199)
    for i, l in enumerate(lim):
        if v <= l: return i
    return len(lim)
NOMB = {False: ("1", "2-4", "5-9", "10-19", "20-49", "50+"), True: ("1", "2-4", "5-9", "10-49", "50-199", "200+")}
for nom, q, filas, esmes in (("ES", E["marea"], es, False), ("MES", M["marea"], mes, True)):
    o = q.pivotes[-1]; t0 = o[0]
    tramo = [f for f in filas if f[0] > t0]
    dl = sum(v * l for (_, _, _, v, l) in tramo)
    compra = sum(v for (_, _, _, v, l) in tramo if l > 0); vende = sum(v for (_, _, _, v, l) in tramo if l < 0)
    print("  %s: desde %s AR (%.2f) hasta el extremo %.2f (%s AR) y ahora %.2f | ordenes %d | compras agresoras %d, ventas agresoras %d, delta %+d%s"
          % (nom, ar(t0), o[1], q.ext, ar(q.ext_t), filas[-1][2], len(tramo), compra, vende, dl, " (= %+.0f ES)" % (dl / 10) if esmes else ""))
    cub = [[0, 0, 0] for _ in NOMB[esmes]]
    for (_, _, _, v, l) in tramo:
        c = cub[tamano(v, esmes)]; c[0] += 1; c[1] += v * (l > 0); c[2] += v * (l < 0)
    for i, n in enumerate(NOMB[esmes]):
        c = cub[i]
        print("     ordenes de %-6s contratos: %6d ordenes | compran %7d | venden %7d | delta %+7d%s" % (n, c[0], c[1], c[2], c[1] - c[2], "  (= %+.0f ES)" % ((c[1] - c[2]) / 10) if esmes else ""))
    big = sorted(tramo, key=lambda f: -f[3])[:8]
    print("     las 8 ordenes agresoras mas grandes: " + "; ".join("%s %s %d a %.2f" % (ar(f[0]), "COMPRA" if f[4] > 0 else "VENDE", f[3], f[2]) for f in big))

print("\n=== 3) MINUTO A MINUTO DENTRO DE LA PIERNA (desde el origen comun) ===")
t0 = max(E["marea"].pivotes[-1][0], M["marea"].pivotes[-1][0])
def por_minuto(filas):
    d = {}; px = {}
    for (t, a, b, v, l) in filas:
        if t <= t0: continue
        k = t[:16]; d[k] = d.get(k, 0) + v * l; px[k] = b
    return d, px
dE, pE = por_minuto(es); dM, pM = por_minuto(mes)
ks = sorted(set(dE) | set(dM))
xs = [dE.get(k, 0) for k in ks]; ys = [dM.get(k, 0) / 10 for k in ks]
n = len(ks); mx = sum(xs) / n; my = sum(ys) / n
cov = sum((a - mx) * (b - my) for a, b in zip(xs, ys)); vx = sum((a - mx) ** 2 for a in xs); vy = sum((b - my) ** 2 for b in ys)
print("  %d minutos; correlacion delta ES vs delta MES/10 = %.2f; minutos con signo distinto: %d de %d"
      % (n, cov / math.sqrt(vx * vy), sum(1 for a, b in zip(xs, ys) if a * b < 0), n))
acE = acM = 0
cambio = []
for k in ks:
    acE += dE.get(k, 0); acM += dM.get(k, 0)
    cambio.append((k, dE.get(k, 0), dM.get(k, 0), acE, acM, pE.get(k), pM.get(k)))
print("  hora AR | delta ES | delta MES (eq ES) | acumulado ES | acumulado MES (eq ES) | precio ES / MES")
for (k, a, b, ca, cb, p1, p2) in cambio:
    print("  %s | %+6d | %+6d (%+5.0f) | %+6d | %+7d (%+6.0f) | %s / %s" % (ar(k + ":00")[:5], a, b, b / 10, ca, cb, cb / 10,
          "%.2f" % p1 if p1 else "-", "%.2f" % p2 if p2 else "-"))

print("\n=== 4) LA JORNADA RTH: COLOR AL CIERRE DE CADA MINUTO, ES contra MES ===")
for esc in ("marea", "ola"):
    for nomM, fM in (("MES X10,00", fotM), ("MES con X10,25", fotM2)):
        ks = sorted(k for k in fotE if k in fM and esc in fotE[k] and esc in fM[k])
        def cod(f): d, c = f; return "sube con" if (d > 0 and c) else "baja con" if (d < 0 and c) else ("sube SIN" if d > 0 else "baja SIN")
        igual_dir = sum(1 for k in ks if fotE[k][esc][0] == fM[k][esc][0])
        igual = sum(1 for k in ks if cod(fotE[k][esc]) == cod(fM[k][esc]))
        dis = [(k, cod(fotE[k][esc]), cod(fM[k][esc])) for k in ks if cod(fotE[k][esc]) != cod(fM[k][esc])]
        print("  %-5s ES vs %-15s: %d minutos | misma direccion %d (%.0f %%) | mismo color %d (%.0f %%)" % (esc, nomM, len(ks), igual_dir, 100 * igual_dir / len(ks), igual, 100 * igual / len(ks)))
        tramos = []; ant = None
        for k, a, b in dis:
            if ant and (datetime.strptime(k, "%Y-%m-%dT%H:%M") - datetime.strptime(ant[1], "%Y-%m-%dT%H:%M")).seconds == 60 and ant[2] == (a, b):
                ant[1] = k
            else:
                ant = [k, k, (a, b)]; tramos.append(ant)
        for i, j, (a, b) in tramos[:14]:
            print("      %s a %s AR: ES %s | MES %s" % (ar(i + ":00")[:5], ar(j + ":00")[:5], a, b))
