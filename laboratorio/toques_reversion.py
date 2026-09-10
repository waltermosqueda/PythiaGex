# -*- coding: utf-8 -*-
"""TOQUES Y REVERSIONES EN LOS NIVELES: cuantas veces por sesion el precio se acerca a una
dominante / zero gamma / major, la toca o la traspasa un poco, y REVIERTE; a que hora se repite
mas; y si eso le gana a un nivel corrido (placebo).

Definiciones (fijadas antes de mirar):
  - Acercamiento: el precio viene de lejos (mas de LEJOS rangos tipicos del nivel hace <= 30
    min) y entra en la banda del nivel (+- TOL).
  - Traspaso: dentro de los TRAS minutos siguientes el precio cruza el nivel hasta PENETRA
    puntos como maximo (si lo pasa mas, es ruptura, no toque).
  - Reversion: despues del toque, en HORIZ minutos, el precio se aleja del nivel hacia el lado
    de donde venia por lo menos REB puntos (medidos desde el nivel) ANTES de irse REB del otro
    lado. Es el "rebote" del operador, con numero.
  - Placebo: el mismo nivel corrido +-X puntos (varios X), misma regla.
Salida: por instrumento, por tipo de nivel y por franja horaria de Nueva York: toques por dia,
% de reversion, placebo, ventaja; y el detalle del mejor caso (traspaso chico vs sin traspaso).

Uso: python laboratorio/toques_reversion.py MNQ [--reb 20] [--penetra 12] [--horiz 20]
"""
import os
import statistics as st
import sys
from collections import defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gatillo_cientifico import cargar, rasgos  # noqa: E402

NIVELES = ("dom_arr", "dom_aba", "zero", "mp", "mn")


def franja(v):
    hr = v["t"].hour - 4
    return "09:30-10:30" if hr == 9 or (hr == 10 and v["t"].minute < 30) else "10:30-12" if hr < 12 else "12-14" if hr < 14 else "14-16"


def toques(vs, tol, lejos_mult, penetra, horiz, reb, corr=0.0):
    """Lista de toques: (tipo, i, lado_de_donde_viene, traspaso_max, revirtio, franja, dia)."""
    out = []
    ultimo = {}
    for i, v in enumerate(vs):
        if i == 0 or vs[i - 1]["dia"] != v["dia"]:
            ultimo = {}
        rt = v["rt"] or 0.25
        for tipo in NIVELES:
            L = v.get(tipo)
            if not L:
                continue
            L = L + corr
            k = (tipo, round(L, 2))
            d = v["c"] - L
            if abs(d) > lejos_mult * rt + tol:
                ultimo[k] = (i, 1 if d > 0 else -1)          # +1: el precio esta ARRIBA del nivel
                continue
            dentro = v["l"] <= L + tol and v["h"] >= L - tol
            if not dentro:
                continue
            u = ultimo.pop(k, None)
            if u is None or i - u[0] > 30:
                continue
            lado = u[1]                                       # viene de arriba (+1) o de abajo (-1)
            # traspaso maximo y reversion en el horizonte
            tras = 0.0; rev = None
            for j in range(i, min(len(vs), i + horiz + 1)):
                w = vs[j]
                if w["dia"] != v["dia"]:
                    break
                pen = (L - w["l"]) if lado > 0 else (w["h"] - L)     # cuanto se metio del otro lado
                tras = max(tras, pen)
                if pen > penetra:                                    # ruptura: no es toque
                    rev = False; break
                lejos_bien = (w["h"] - L) if lado > 0 else (L - w["l"])
                if lejos_bien >= reb:
                    rev = True; break
            if rev is None:
                continue
            out.append((tipo, i, lado, tras, rev, franja(v), v["dia"]))
    return out


def main():
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    inst = a[0] if a else "MNQ"
    arg = lambda k, d: type(d)(sys.argv[sys.argv.index(k) + 1]) if k in sys.argv else d
    es_nq = inst.startswith("MNQ") or inst.startswith("NQ")
    reb = arg("--reb", 20.0 if es_nq else 5.0); penetra = arg("--penetra", 12.0 if es_nq else 3.0)
    horiz = arg("--horiz", 20); tol = arg("--tol", 8.0 if es_nq else 2.0); lejos_mult = 2.5
    placebos = (-140, -80, 80, 140) if es_nq else (-35, -20, 20, 35)
    vs = cargar(inst); rasgos(vs)
    dias = sorted(set(v["dia"] for v in vs))
    print("%s: %d velas, %d dias | toque = entra a +-%g del nivel viniendo de lejos; traspaso permitido <= %g; reversion = %g pts hacia donde venia en %d min" % (
        inst, len(vs), len(dias), tol, penetra, reb, horiz))
    real = toques(vs, tol, lejos_mult, penetra, horiz, reb)
    plc = []
    for c in placebos:
        plc += toques(vs, tol, lejos_mult, penetra, horiz, reb, c)
    def tabla(titulo, clave):
        print("  " + titulo)
        grupos = defaultdict(list); gp = defaultdict(list)
        for t in real: grupos[clave(t)].append(t[4])
        for t in plc: gp[clave(t)].append(t[4])
        for k in sorted(grupos):
            r = grupos[k]; p = gp.get(k, [])
            if len(r) < 8:
                print("    %-24s %4d toques (%.1f/dia): pocos" % (k, len(r), len(r) / float(len(dias)))); continue
            pr = 100.0 * sum(r) / len(r); pp = 100.0 * sum(p) / len(p) if p else float("nan")
            print("    %-24s %4d toques (%.1f/dia) revierte %5.1f %% | placebo %4d %5.1f %% | ventaja %+5.1f pp" % (k, len(r), len(r) / float(len(dias)), pr, len(p), pp, pr - pp))
    tabla("POR TIPO DE NIVEL:", lambda t: t[0])
    tabla("POR FRANJA HORARIA (NY), todos los niveles:", lambda t: t[5])
    tabla("POR FRANJA, solo dominantes:", lambda t: t[5] if t[0].startswith("dom") else "otros")
    tabla("TRASPASO: sin traspaso vs traspaso chico (todos los niveles):", lambda t: "traspasa un poco" if t[3] > 0.5 else "no traspasa")
    tabla("DOMINANTES por franja y traspaso:", lambda t: (t[5] + (" +traspaso" if t[3] > 0.5 else " sin")) if t[0].startswith("dom") else "otros")
    # por dia: cuantos toques y cuantos rebotes
    pd = defaultdict(lambda: [0, 0])
    for t in real:
        if t[0].startswith("dom"): pd[t[6]][1] += 1; pd[t[6]][0] += 1 if t[4] else 0
    print("  DOMINANTES por dia (rebotes/toques): " + " ".join("%s %d/%d" % (d[5:], v[0], v[1]) for d, v in sorted(pd.items())))
    print()
    print("COMO LEERLO: 'revierte' es que despues del toque el precio se fue %g pts hacia donde venia antes de irse %g" % (reb, reb))
    print("del otro lado. El placebo es el mismo calculo con el nivel corrido. Sin ventaja sobre placebo, el nivel no aporta.")


if __name__ == "__main__":
    main()
