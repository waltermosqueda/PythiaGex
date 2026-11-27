# -*- coding: utf-8 -*-
"""Busca en una cinta de la sonda la orden n en la que la Maquina de referencia (test_ola) da EXACTAMENTE lo que se lee en un panel de
La Ola en una foto: marea (dir, delta, faltan) y ola (dir, delta, faltan). Devuelve las ordenes que coinciden y el delta de la jornada.
Uso: python -I instante_panel.py <PythiaGex> <cinta> <X> <desde_utc> <hasta_utc> <m_dir> <m_delta> <m_faltan> <o_dir> <o_delta> <o_faltan>"""
import sys, os, csv
sys.stdout.reconfigure(encoding="utf-8")
sys.path.insert(0, os.path.join(sys.argv[1], "herramientas"))
import test_ola as T
from datetime import datetime, timedelta
cinta, X, desde, hasta = sys.argv[2], float(sys.argv[3]), sys.argv[4], sys.argv[5]
quiero = {"marea": (int(sys.argv[6]), int(sys.argv[7]), float(sys.argv[8])), "ola": (int(sys.argv[9]), int(sys.argv[10]), float(sys.argv[11]))}
m = {"ola": T.Maquina(X), "marea": T.Maquina(4 * X)}
cum = 0; hits = []
with open(cinta, newline="") as fh:
    r = csv.reader(fh); next(r)
    for n, x in enumerate(r, 1):
        t = x[0]; a, b, v, lado = float(x[1]), float(x[2]), int(float(x[3])), int(x[4])
        cum += v * lado
        for k in m: m[k].paso(t, min(a, b), max(a, b), cum)
        if desde <= t <= hasta:
            est = {k: (q.dir, cum - q.pivotes[-1][4], round(max(0.0, q.X - abs(q.ext - b)), 2)) for k, q in m.items()}
            if est["marea"] == quiero["marea"] and est["ola"] == quiero["ola"]:
                rayas = {k: q.ext - q.dir * q.X for k, q in m.items()}
                hits.append((n, t, b, cum, rayas))
        if t > hasta: break
ar = lambda t: (datetime.strptime(t[:23], "%Y-%m-%dT%H:%M:%S.%f") - timedelta(hours=3)).strftime("%H:%M:%S.%f")[:-3]
print("ordenes que dan EXACTO el panel %s: %d" % (quiero, len(hits)))
for n, t, b, c, ry in hits[:5]:
    print("   orden %d  %s AR  ultimo %.2f | delta de la jornada %+d | raya ola %.2f, raya marea %.2f" % (n, ar(t), b, c, ry["ola"], ry["marea"]))
