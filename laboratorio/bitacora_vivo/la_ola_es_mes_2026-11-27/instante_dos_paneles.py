# -*- coding: utf-8 -*-
"""La captura del operador (27-11 13:46:31 AR, MES a la izquierda y ES a la derecha) contra las cintas de la sonda.
Busca en cada cinta la orden n en la que la Maquina de referencia (test_ola) da EXACTAMENTE lo que muestra el panel:
marea (dir, delta, faltan, raya), ola (dir, delta, faltan, raya), y el delta acumulado de la jornada (el 'total' del Superindicador).
Uso: python -I instante_dos_paneles.py <PythiaGex> <cinta ES> <cinta MES>"""
import sys, os, csv
sys.stdout.reconfigure(encoding="utf-8")
sys.path.insert(0, os.path.join(sys.argv[1], "herramientas"))
import test_ola as T
from datetime import datetime, timedelta

PANTALLA = {   # lo que se lee en la captura
    "ES":  {"X": 10.25, "marea": (1, -2640, 36.75, 7256.50), "ola": (1, -399, 6.00, 7287.25), "total": -3491},
    "MES": {"X": 10.00, "marea": (1, 6553, 35.75, 7257.50), "ola": (1, 976, 5.75, 7287.50), "total": 8068},
}
def ar(t): return (datetime.strptime(t[:23], "%Y-%m-%dT%H:%M:%S.%f") - timedelta(hours=3)).strftime("%H:%M:%S.%f")[:-3]
for nom, ruta in (("ES", sys.argv[2]), ("MES", sys.argv[3])):
    p = PANTALLA[nom]; X = p["X"]
    m = {"ola": T.Maquina(X), "marea": T.Maquina(4 * X)}
    cum = 0; hits = []
    with open(ruta, newline="") as fh:
        r = csv.reader(fh); next(r)
        for n, x in enumerate(r, 1):
            t = x[0]; a, b, v, lado = float(x[1]), float(x[2]), int(float(x[3])), int(x[4])
            cum += v * lado
            for k in m: m[k].paso(t, min(a, b), max(a, b), cum)
            if "2026-11-27T16:45:30" <= t <= "2026-11-27T16:46:45":
                est = {}
                for k, q in m.items():
                    o = q.pivotes[-1]; retro = abs(q.ext - b)
                    est[k] = (q.dir, cum - o[4], round(max(0.0, q.X - retro), 2), q.ext - q.dir * q.X)
                if est["marea"] == p["marea"] and est["ola"] == p["ola"]:
                    hits.append((n, t, b, cum, est))
            if t > "2026-11-27T16:46:45": break
    print("%s (X %.2f): ordenes de la cinta que dan EXACTO el panel de la captura: %d" % (nom, X, len(hits)))
    for n, t, b, c, est in hits[:6]:
        print("   orden %d  %s AR  ultimo %.2f | marea %s | ola %s | delta de la jornada %+d (pantalla 'total' %+d) %s"
              % (n, ar(t), b, est["marea"], est["ola"], c, p["total"], "IGUAL" if c == p["total"] else "DISTINTO"))
