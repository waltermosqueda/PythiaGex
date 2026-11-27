# -*- coding: utf-8 -*-
"""COMPROBACION DEL PANEL de La Ola 1.0 (27-11-2026), independiente de auditar_pythiaola.py: cada linea del jsonl de auditoria
(lo que MOSTRO el panel: ola y marea con dir, raya, faltan y delta, y n = ordenes que tenia el indicador) contra la Maquina de referencia
(test_ola.Maquina) corrida sobre las PRIMERAS n ordenes de la cinta de la sonda de la misma jornada. Asi se compara el mismo instante:
el control de paneles de auditar_pythiaola.py compara el panel en vivo (a mitad de minuto) con la foto del CIERRE del minuto y da
diferencias falsas (27-11: 4 "difs" que aca dieron IGUAL).
Uso: python -I herramientas/auditar_panel_ola.py <cinta-ESZ6-...csv> <auditoria-...jsonl> <X>"""
import sys, json, csv
sys.stdout.reconfigure(encoding="utf-8")
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import test_ola as T
cinta, jsonl, X = sys.argv[1], sys.argv[2], float(sys.argv[3])
filas = []
with open(cinta, newline="") as fh:
    r = csv.reader(fh); next(r)
    for x in r: filas.append((x[0], float(x[1]), float(x[2]), float(x[3]), int(x[4])))
ent = [json.loads(l) for l in open(jsonl, encoding="utf-8")]
pedidos = sorted({e["pantalla"]["ola"]["n"] for e in ent if e["pantalla"].get("ola", {}).get("hay")})
m = {"ola": T.Maquina(X), "marea": T.Maquina(4 * X)}
cum = 0; ult = None; estados = {}
for i, (t, a, b, v, lado) in enumerate(filas, 1):
    cum += v * lado; ult = b
    for k in m: m[k].paso(t, min(a, b), max(a, b), cum)
    if i in pedidos:
        est = {}
        for k, q in m.items():
            o = q.pivotes[-1]; d = cum - o[4]; retro = abs(q.ext - ult)
            est[k] = (q.dir, q.ext - q.dir * q.X, max(0.0, q.X - retro), d)
        estados[i] = (t, ult, est)
print("cinta: %d ordenes; lineas del panel con ola: %d; n pedidos %s" % (len(filas), len(pedidos), pedidos))
malos = 0
for e in ent:
    p = e["pantalla"]
    if not p.get("ola", {}).get("hay"): continue
    n = p["ola"]["n"]
    if n not in estados: print("  %s n=%d: la cinta no llega" % (e["utc"], n)); continue
    t, ult, est = estados[n]
    for k in ("ola", "marea"):
        q = p[k]; mio = (q["dir"], q["raya"], q["faltan"], q["delta"]); ref = est[k]
        ok = mio[0] == ref[0] and abs(mio[1] - ref[1]) < 1e-9 and abs(mio[2] - ref[2]) < 1e-9 and abs(mio[3] - ref[3]) < 1e-9
        malos += not ok
        print("  %s n=%d (orden %s, ultimo %.2f) %-5s panel dir %+d raya %.2f faltan %.2f delta %+.0f | cinta dir %+d raya %.2f faltan %.2f delta %+.0f  %s"
              % (e["utc"], n, t, ult, k, mio[0], mio[1], mio[2], mio[3], ref[0], ref[1], ref[2], ref[3], "IGUAL" if ok else "DISTINTO"))
    print("     textos: " + " / ".join(p["panel"]["textos"]) + " | pie: " + p["panel"]["pie"])
    for k in ("ola", "marea"):
        r_ = p["rayas"].get(k)
        if r_: print("     raya %s en pantalla: precio %.2f texto '%s'" % (k, r_["precio"], r_["texto"]))
print("RESULTADO: %d distintos" % malos)
