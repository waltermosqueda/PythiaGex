# -*- coding: utf-8 -*-
"""potencia_barrera.py (soportes_metodo, 09-10-2026): POTENCIA del juez de mechas frente a una barrera parcialmente reflectante
(el modelo de Osler 2003: ordenes de toma de ganancia apiladas en un precio). Paseo al azar a 1 s (sd del cierre m2 de la noche real,
8,744 pts); 2 rayas fijas por sesion; cada vez que el precio cruza una raya (a 1 s), con probabilidad RHO se refleja. Se mide cuanto
sube la tasa de giro (unidad 'candidatas', THETA 20, TOL 2) y que fraccion de 60 repeticiones detecta (p jitter < 0,05) con 5, 10 y 20
sesiones de 405 velas m2. Salida: potencia_barrera.json

CORRIDA 1 (09-10 ~00:49 hora de la PC): reflejo independiente en cada cruce a 1 s con RHO 0,02-0,15 -> NINGUN efecto (tasa de giro = placebo,
deteccion = 5 %): una barrera que refleja cruces sueltos es casi transparente porque el precio la reintenta segundos despues.
CORRIDA 2 (esta): "pared que a veces aguanta": en cada VISITA nueva (el precio se alejo >= 4 pts desde la anterior), con probabilidad
RHO la raya se vuelve impenetrable durante 300 s (5 min); si no, es transparente en esa visita."""
import os, sys, json, math
AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI); sys.path.insert(0, os.path.normpath(os.path.join(AQUI, "..", "..")))
import numpy as np
import juez_mechas as JM
JM.M = 200
rng = np.random.default_rng(11)

def camino(n, seg, sd, Ls, rho, hold=300, rearme=4.0):
    inc = rng.normal(0.0, sd / math.sqrt(seg), n * seg)
    x = np.empty(n * seg); p = 30000.0
    hasta = [-1] * len(Ls); armado = [True] * len(Ls)
    for k in range(n * seg):
        q = p + inc[k]
        for j, L in enumerate(Ls):
            if (p - L) * (q - L) < 0:
                if k < hasta[j]:
                    q = 2 * L - q
                elif armado[j]:
                    armado[j] = False
                    if rng.random() < rho:
                        hasta[j] = k + hold; q = 2 * L - q
            if abs(q - L) >= rearme:
                armado[j] = True
        x[k] = q; p = q
    x = (np.round(x / 0.25) * 0.25).reshape(n, seg)
    return x[:, 0], x.max(1), x.min(1), x[:, -1]

out = {}
for rho in (0.2, 0.5):
    for nses in (5, 10, 20):
        ps, trs, tps = [], [], []
        for rep in range(40):
            acc = {}
            for s in range(nses):
                n = 405
                Lv = list(30000.0 + rng.uniform(-40, 40, 2))
                o, h, l, c = camino(n, 120, 8.744, Lv, rho)
                G = JM.giros_causal(o, h, l, c, 20.0)
                d = rng.uniform(JM.J1, JM.J2, JM.M) * rng.choice([-1.0, 1.0], JM.M)
                r = JM.medir(h, l, [np.full(n, v) for v in Lv], G, np.ones(n, bool), d, "candidatas")
                if r:
                    JM.acumular(acc, "barrera", r[0], r[1], s)
            t = JM.resumir(acc, minimo=1)
            if t:
                ps.append(t[0]["p_jitter"]); trs.append(t[0]["tasa_giro_pct"]); tps.append(t[0]["tasa_placebo_pct"])
        out["rho=%g|sesiones=%d" % (rho, nses)] = dict(detecta_5pct=round(float(np.mean(np.array(ps) < 0.05)), 3), tasa_giro_media=round(float(np.mean(trs)), 1),
                                                       tasa_placebo_media=round(float(np.mean(tps)), 1), repeticiones=len(ps))
        print(rho, nses, out["rho=%g|sesiones=%d" % (rho, nses)], flush=True)
json.dump(out, open(os.path.join(AQUI, "potencia_barrera_pared.json"), "w", encoding="utf-8"), indent=1)
